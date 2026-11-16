using System;
using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ПРЕЗЕНТАЦИОННЫЙ РЕЖИМ (ЭТАП 19 ТЗ).
    ///
    /// Камера сама облетает робота по кругу, на экране идёт текст-рассказ, весь интерфейс
    /// скрыт. Выход — Esc или кнопка в углу (управление в режиме: ПРОБЕЛ — следующий кадр,
    /// ←/→ — предыдущий/следующий, Esc — выход).
    ///
    /// ВАЖНО про совместимость: `FreeFlyCameraController` НЕ выключается — его `Update`
    /// каждый кадр передаёт прицел в поток (`TrajectoryFlowController.UpdateAim`), и без него
    /// перестали бы двигаться фантомы и обрабатываться планирование. Вместо отключения
    /// обнуляются скорости оператора и выключаются лазеры (чтобы случайный клик не сбил
    /// показ), а сама камера ведётся в `LateUpdate` — порядок кадров сохраняется.
    /// </summary>
    public class KvPresentationMode
    {
        public const string KeyNext = "SPACE — следующий кадр · ← / → — перелистывание · Esc — выход";

        /// <summary>Радиус облёта, м.</summary>
        public float radius = 3.2f;
        /// <summary>Высота камеры, м (над базой робота).</summary>
        public float height = 1.9f;
        /// <summary>Скорость облёта, градусов в секунду.</summary>
        public float orbitSpeed = 12f;
        /// <summary>Длительность одного «кадра» рассказа, с.</summary>
        public float lineSeconds = 6f;

        public event Action<string> Message;

        private readonly List<string> narration = new List<string>();

        /// <summary>Текст по умолчанию: если хаб не задал рассказ, показ всё равно осмысленный.</summary>
        public static List<string> DefaultNarration()
        {
            return new List<string>
            {
                "KazistovVv — VR/MR-платформа управления роботами: SCARA и робот на двух стендах",
                "Красный лазер (Z) + ЛКМ — выбор точки; планировщик строит до 8 вариантов траектории",
                "BiRRT-Connect, IK с лимитами суставов, проверка зазоров и SafetyGate перед движением",
                "Зелёный лазер (X) выбирает траекторию: фантом проходит её, затем едет робот",
                "Зоны запрета и тепловые карты достижимости и зазоров показывают безопасность",
                "Запись движения, позы, сравнение траекторий и графики углов — для отладки",
                "Аварийная остановка, журнал действий и сессии — контроль и воспроизводимость"
            };
        }
        private bool active;
        private int line;
        private float lineTimer;
        private float angle;

        private Camera camera;
        private TrajectoryFlowController flow;
        private FreeFlyCameraController rig;

        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedMoveSpeed, savedSprintSpeed, savedVerticalSpeed, savedLook, savedGamepadLook;
        private bool savedLeftLaser, savedRightLaser, savedSuppress;

        public bool Active { get { return active; } }
        public int LineIndex { get { return line; } }
        public int LineCount { get { return narration.Count; } }
        public string CurrentLine
        {
            get
            {
                if (narration.Count == 0) return "KazistovVv — платформа управления роботами";
                return narration[Mathf.Clamp(line, 0, narration.Count - 1)];
            }
        }

        public void Bind(Camera mainCamera, TrajectoryFlowController controller,
            FreeFlyCameraController cameraRig)
        {
            camera = mainCamera;
            flow = controller;
            rig = cameraRig;
        }

        /// <summary>Текст рассказа (по одному пункту на «кадр»).</summary>
        public void SetNarration(List<string> lines)
        {
            narration.Clear();
            if (lines != null) narration.AddRange(lines);
            if (narration.Count == 0)
            {
                narration.Add("KazistovVv — VR/MR-платформа управления роботами");
                narration.Add("Планирование траекторий: BiRRT-Connect, IK, SafetyGate");
                narration.Add("Восемь вариантов траектории на каждую точку");
            }
        }

        /// <summary>Включить презентационный режим.</summary>
        public bool Enter()
        {
            if (active) return false;
            if (camera == null) camera = Camera.main;
            if (camera == null)
            {
                Report("презентационный режим невозможен: в сцене нет камеры");
                return false;
            }
            if (rig == null)
            {
                // ЛИСТ `presentation.toggle`: «тихая деградация при непривязанных ссылках» —
                // режим включался, но риг камеры оператора оставался со своими настройками
                // и это нигде не проговаривалось.
                Report("презентационный режим включён, но риг камеры не привязан — " +
                       "настройки камеры оператора не блокируются");
            }

            active = true;
            line = 0;
            lineTimer = 0f;
            if (narration.Count == 0) narration.AddRange(DefaultNarration());

            savedPosition = camera.transform.position;
            savedRotation = camera.transform.rotation;

            if (rig != null)
            {
                savedMoveSpeed = rig.moveSpeed;
                savedSprintSpeed = rig.sprintSpeed;
                savedVerticalSpeed = rig.verticalSpeed;
                savedLook = rig.lookSensitivity;
                savedGamepadLook = rig.gamepadLookSensitivity;
                savedLeftLaser = rig.leftHandEnabled;
                savedRightLaser = rig.rightHandEnabled;
                savedSuppress = rig.suppressDirectTeleop;

                rig.moveSpeed = 0f;              // оператор не должен «уезжать» из кадра
                rig.sprintSpeed = 0f;
                rig.verticalSpeed = 0f;
                rig.lookSensitivity = 0f;
                rig.gamepadLookSensitivity = 0f;
                rig.leftHandEnabled = false;     // случайный клик не собьёт показ
                rig.rightHandEnabled = false;
                rig.suppressDirectTeleop = true;
            }

            // Стартовый угол — со стороны оператора, чтобы не было «прыжка» камеры.
            Vector3 center = OrbitCenter();
            Vector3 dir = camera.transform.position - center;
            angle = Mathf.Atan2(dir.z, dir.x);

            // ФИКС 1 (§23): оболочка и КУРСОР переключаются ВМЕСТЕ и через один источник
            // правды. Раньше здесь гасился только канвас, а режим курсора не менялся:
            // в презентационном режиме оставался системный курсор при скрытых панелях
            // (и, наоборот, после выхода можно было «потерять» мышь) — ровно то, на что
            // жаловался оператор. Режим курсора ведёт только KvMouseCursor.
            KazistovVvUI.KvMouseCursor.ModeToggleSuspended = true;
            KazistovVvUI.KvMouseCursor.Capture("презентационный режим: панели скрыты");
            Report("презентационный режим включён · " + KeyNext);
            return true;
        }

        /// <summary>Выйти из режима (Esc или кнопка).</summary>
        public void Exit()
        {
            if (!active) return;
            active = false;

            if (camera != null)
            {
                camera.transform.position = savedPosition;
                camera.transform.rotation = savedRotation;
            }
            if (rig != null)
            {
                rig.moveSpeed = savedMoveSpeed;
                rig.sprintSpeed = savedSprintSpeed;
                rig.verticalSpeed = savedVerticalSpeed;
                rig.lookSensitivity = savedLook;
                rig.gamepadLookSensitivity = savedGamepadLook;
                rig.leftHandEnabled = savedLeftLaser;
                rig.rightHandEnabled = savedRightLaser;
                rig.suppressDirectTeleop = savedSuppress;
            }
            // ФИКС 1 (§23): выход возвращает РОВНО прежний режим — оболочку показывает
            // системный курсор (KvMouseCursor.Release), а не просто «включает канвас».
            // §26: клавиша переключения режима (Tab) во время режима была заблокирована,
            // поэтому снимаем блокировку до Release.
            KazistovVvUI.KvMouseCursor.ModeToggleSuspended = false;
            KazistovVvUI.KvMouseCursor.Release("презентационный режим выключен: системный курсор");
            Report("презентационный режим выключен");
        }

        public void Toggle()
        {
            if (active) Exit();
            else Enter();
        }

        /// <summary>Следующий кадр рассказа.</summary>
        public void Next()
        {
            if (!active || narration.Count == 0) return;
            line = (line + 1) % narration.Count;
            lineTimer = 0f;
        }

        public void Previous()
        {
            if (!active || narration.Count == 0) return;
            line = (line - 1 + narration.Count) % narration.Count;
            lineTimer = 0f;
        }

        /// <summary>Ведение камеры (вызывать из LateUpdate хаба).</summary>
        public void Tick(float deltaTime, bool nextPressed, bool prevPressed)
        {
            if (!active || camera == null) return;

            if (nextPressed) Next();
            if (prevPressed) Previous();

            lineTimer += deltaTime;
            if (lineTimer >= Mathf.Max(1f, lineSeconds) && narration.Count > 1)
            {
                lineTimer = 0f;
                line = (line + 1) % narration.Count;
            }

            Vector3 center = OrbitCenter();
            angle += Mathf.Deg2Rad * Mathf.Max(0.5f, orbitSpeed) * deltaTime;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            pos.y = center.y + height;

            camera.transform.position = Vector3.Lerp(camera.transform.position, pos,
                1f - Mathf.Exp(-6f * deltaTime));
            Quaternion look = Quaternion.LookRotation((center + Vector3.up * 0.15f) - camera.transform.position,
                Vector3.up);
            camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, look,
                1f - Mathf.Exp(-6f * deltaTime));
        }

        private Vector3 OrbitCenter()
        {
            if (flow != null && flow.Robot != null) return flow.Robot.transform.position;
            RobotController any = UnityEngine.Object.FindAnyObjectByType<RobotController>();
            return any != null ? any.transform.position : Vector3.zero;
        }

        private void Report(string text)
        {
            Debug.Log("[Presentation] " + text);
            if (Message != null) Message(text);
            // ЛИСТ `presentation.toggle`: подписка `Presentation.Message` живёт в `Awake` хаба —
            // без неё след уходил ТОЛЬКО в консоль (на устройстве её нет). Без подписчика
            // строка идёт в журнал напрямую, чтобы нажатие не осталось без объяснения.
            else KvActionLog.Instance.Add(KvLogKind.Ui, text);
        }
    }
}
