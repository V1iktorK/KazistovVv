using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace KazistovVvXr
{
    /// <summary>
    /// СОСТОЯНИЕ КОНТРОЛЛЕРОВ И ГОЛОВЫ (сессия §28, этап 2 плана XR).
    ///
    /// ЗАЧЕМ. Позы, курок и стик надо читать из ОДНОГО места: и ввод для робота
    /// (<c>VRInputProvider</c>), и перемещение (<c>KvXrLocomotion</c>), и луч по интерфейсу
    /// (<c>KvXrUiPointer</c>) должны видеть одну и ту же картину кадра. Иначе получается
    /// классический дефект VR: «курок нажал два действия» — ровно та болезнь, которую
    /// в §27 лечили на десктопе арбитражем клавиш.
    ///
    /// ПОЧЕМУ НЕ XRI. Проект обходится без компонентов XR Interaction Toolkit: они требуют
    /// настроенного префаба рига, а собрать и проверить его без шлема нечем. Здесь читается
    /// сам <see cref="InputSystem"/> — он уже подключён (пакет 1.20.0, только new Input System),
    /// и устройство PICO 4 Ultra приходит через OpenXR тем же способом, что и любое XR-устройство.
    ///
    /// ЧЕСТНОЕ ОГРАНИЧЕНИЕ: имена устройств у PICO отличаются от Quest/Vive, поэтому поиск
    /// идёт ПО ВОЗМОЖНОСТЯМ (есть стик / есть курок / есть поза), а не по имени. Это работает
    /// до тех пор, пока платформа отдаёт контроллер как XRController, что и требуется OpenXR.
    /// </summary>
    public static class KvXrInputSource
    {
        /// <summary>Сколько кадров подряд состояние устройства считается «свежим» (защита от «залипших» данных).</summary>
        private static readonly List<InputDevice> controllers = new List<InputDevice>();
        private static XRController right;
        private static XRController left;
        private static XRHMD head;
        private static float lastScanTime = -100f;

        /// <summary>Есть ли живой шлем: устройство головы найдено И XR-подсистема работает.</summary>
        public static bool HeadsetPresent
        {
            get
            {
                EnsureScanned();
                return head != null && head.added;
            }
        }

        /// <summary>Голова (шлем). null — шлема нет.</summary>
        public static XRHMD Head { get { EnsureScanned(); return head; } }

        /// <summary>Правый контроллер. null — не найден.</summary>
        public static XRController Right { get { EnsureScanned(); return right; } }

        /// <summary>Левый контроллер. null — не найден.</summary>
        public static XRController Left { get { EnsureScanned(); return left; } }

        /// <summary>Поза правого контроллера (позиция, поворот) и признак её достоверности.</summary>
        public static bool TryRightPose(out Vector3 position, out Quaternion rotation)
        {
            return TryPose(right, out position, out rotation);
        }

        /// <summary>Поза левого контроллера.</summary>
        public static bool TryLeftPose(out Vector3 position, out Quaternion rotation)
        {
            return TryPose(left, out position, out rotation);
        }

        /// <summary>Поза головы (для панели и проверки «шлем надет»).</summary>
        public static bool TryHeadPose(out Vector3 position, out Quaternion rotation)
        {
            EnsureScanned();
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return head != null && head.added && TryPose(head, out position, out rotation);
        }

        /// <summary>Курок (trigger) правого контроллера: 0…1. Нет контроллера — 0.</summary>
        public static float RightTrigger { get { return Axis(ReadTrigger(right)); } }

        /// <summary>Курок левого контроллера: 0…1.</summary>
        public static float LeftTrigger { get { return Axis(ReadTrigger(left)); } }

        /// <summary>Грип (боковая кнопка) правого контроллера: 0…1.</summary>
        public static float RightGrip { get { return Axis(ReadGrip(right)); } }

        /// <summary>Грип левого контроллера: 0…1.</summary>
        public static float LeftGrip { get { return Axis(ReadGrip(left)); } }

        /// <summary>Стик правого контроллера (поворот/телепорт).</summary>
        public static Vector2 RightStick { get { return Stick(right); } }

        /// <summary>Стик левого контроллера (движение/телепорт).</summary>
        public static Vector2 LeftStick { get { return Stick(left); } }

        /// <summary>Определён ли правый контроллер (для честного отчёта «контроллер не найден»).</summary>
        public static bool HasRight { get { EnsureScanned(); return right != null; } }

        /// <summary>Определён ли левый контроллер.</summary>
        public static bool HasLeft { get { EnsureScanned(); return left != null; } }

        /// <summary>Строка состояния для журнала: что найдено, что нет — без домыслов.</summary>
        public static string Describe()
        {
            EnsureScanned();
            return string.Format("XR-ввод: шлем {0} · правый контроллер {1} · левый контроллер {2} · устройств в системе {3}",
                head != null ? "есть" : "НЕТ",
                right != null ? "есть" : "нет",
                left != null ? "есть" : "нет",
                controllers.Count);
        }

        // ------------------------------------------------------------------ внутреннее

        private static float Axis(float value)
        {
            return Mathf.Clamp01(value);
        }

        /// <summary>
        /// ИМЕНА УПРАВЛЯЮЩИХ ЭЛЕМЕНТОВ, которые считаются курком. Порядок = порядок проверки:
        /// сначала богатый профиль (Oculus Touch — `trigger`), затем простой (KHR simple — `select`).
        ///
        /// ПОЧЕМУ ЭТО ВАЖНО. Для Android в проекте включён `KHRSimpleControllerProfile` (§31),
        /// а у него НЕТ `trigger` — есть `select`. Пока в списке не было `select`, курок на таком
        /// профиле не срабатывал бы вовсе, и это выглядело бы как «контроллеры не работают»,
        /// хотя устройства в системе есть.
        /// </summary>
        public static readonly string[] TriggerControlNames =
            { "triggerPressed", "trigger", "triggerButton", "selectPressed", "select", "primaryButton" };

        /// <summary>Имена элементов «грип» (боковая кнопка или сжатие).</summary>
        public static readonly string[] GripControlNames =
            { "gripPressed", "grip", "squeezePressed", "squeeze" };

        /// <summary>Имена элементов «стик» (`primary2DAxis` — имя из простых профилей OpenXR).</summary>
        public static readonly string[] StickControlNames = { "thumbstick", "primary2DAxis" };

        /// <summary>
        /// ПЕРВОЕ ИМЯ ИЗ СПИСКА, КОТОРОЕ ЕСТЬ У УСТРОЙСТВА. Логика чистая (принимает любую функцию
        /// «есть ли такое имя»), поэтому проверяется тестами без шлема и без контроллеров.
        /// </summary>
        public static string FirstPresent(string[] names, Func<string, bool> has)
        {
            if (names == null || has == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i])) continue;
                if (has(names[i])) return names[i];
            }
            return null;
        }

        /// <summary>Есть ли у контроллера элемент с таким именем (для поиска и для журнала).</summary>
        public static bool HasControl(XRController controller, string name)
        {
            if (controller == null || !controller.added || string.IsNullOrEmpty(name)) return false;
            return controller.TryGetChildControl<InputControl>(name) != null;
        }

        /// <summary>
        /// ЧТО РЕАЛЬНО ЕСТЬ У КОНТРОЛЛЕРА. Печатается в журнал устройства: по этой строке видно,
        /// нашёлся ли курок и стик, или контроллер пришёл «пустым» (тогда дело в профиле OpenXR).
        /// </summary>
        public static string ControlSummary(XRController controller)
        {
            if (controller == null) return "контроллера нет";

            var all = new List<string>();
            all.AddRange(TriggerControlNames);
            all.AddRange(GripControlNames);
            all.AddRange(StickControlNames);
            all.Add("devicePose");
            all.Add("devicePosition");
            all.Add("deviceRotation");
            all.Add("handedness");

            var found = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                if (found.Contains(all[i])) continue;
                if (HasControl(controller, all[i])) found.Add(all[i]);
            }
            return found.Count > 0 ? string.Join(", ", found.ToArray()) : "ни одного известного элемента";
        }

        /// <summary>
        /// Прочитать «курок», «грип» или «стик» по списку имён: сначала кнопка (нажата = 1),
        /// затем ось. Возвращает 0, если ни одного элемента нет, — это честный ноль,
        /// а не «работает». Обход списка, а не одного имени, — потому что профили OpenXR разные.
        /// </summary>
        private static float ReadFirst(XRController c, string[] names)
        {
            if (c == null || !c.added) return 0f;
            for (int i = 0; i < names.Length; i++)
            {
                var press = c.TryGetChildControl<ButtonControl>(names[i]);
                if (press != null && press.isPressed) return 1f;

                var axis = c.TryGetChildControl<AxisControl>(names[i]);
                if (axis != null) return axis.ReadValue();
            }
            return 0f;
        }

        private static float ReadTrigger(XRController c)
        {
            return ReadFirst(c, TriggerControlNames);
        }

        private static float ReadGrip(XRController c)
        {
            return ReadFirst(c, GripControlNames);
        }

        private static Vector2 Stick(XRController c)
        {
            if (c == null || !c.added) return Vector2.zero;
            string name = FirstPresent(StickControlNames, n => HasControl(c, n));
            if (string.IsNullOrEmpty(name)) return Vector2.zero;
            var stick = c.TryGetChildControl<Vector2Control>(name);
            return stick != null ? stick.ReadValue() : Vector2.zero;
        }

        /// <summary>
        /// Поза устройства трекинга.
        ///
        /// ПОЧЕМУ ТРИ ПУТИ, А НЕ ОДИН. Состав управляющих элементов у XR-устройств разный:
        /// одни отдают собранную позу (`devicePose`), другие — отдельные `devicePosition`
        /// и `deviceRotation`, третьи не выставляют признак отслеживания вообще. Если
        /// опереться только на один вариант, на части платформ «луч просто не работает»,
        /// и причину в шлеме не видно. Поэтому пробуем по очереди и запоминаем, что сработало.
        /// </summary>
        private static bool TryPose(InputDevice device, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (device == null || !device.added) return false;

            // Путь 1: собранная поза.
            PoseControl pose = device.TryGetChildControl<PoseControl>("devicePose");
            if (pose != null)
            {
                PoseState state = pose.ReadValue();
                if (state.isTracked || state.position != Vector3.zero)
                {
                    position = state.position;
                    rotation = state.rotation;
                    return true;
                }
            }

            // Путь 2: отдельные позиция и поворот.
            Vector3Control pos = device.TryGetChildControl<Vector3Control>("devicePosition");
            QuaternionControl rot = device.TryGetChildControl<QuaternionControl>("deviceRotation");
            if (pos != null && rot != null)
            {
                position = pos.ReadValue();
                rotation = rot.ReadValue();
                return true;
            }

            // Путь 3: только позиция (поворот остаётся единичным — лучше, чем ничего).
            if (pos != null)
            {
                position = pos.ReadValue();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Поиск устройств: не чаще раза в секунду (устройства появляются позже запуска —
        /// шлем надевают уже после старта приложения).
        /// </summary>
        private static void EnsureScanned()
        {
            if (Time.realtimeSinceStartup - lastScanTime < 1f) return;
            Scan();
        }

        /// <summary>Пересканировать немедленно (нужно диагностике ввода).</summary>
        public static void ForceScan()
        {
            Scan();
        }

        private static void Scan()
        {
            lastScanTime = Time.realtimeSinceStartup;

            controllers.Clear();
            left = null;
            right = null;
            head = null;

            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice d = devices[i];
                if (d == null || !d.added) continue;

                XRHMD hmd = d as XRHMD;
                if (hmd != null) { head = hmd; continue; }

                XRController c = d as XRController;
                if (c == null) continue;
                controllers.Add(d);
                if (IsRightHanded(c)) right = c;
                else if (IsLeftHanded(c)) left = c;
                else if (right == null) right = c;
                else if (left == null) left = c;
            }
        }

        // ------------------------------------------------------------------ диагностика (§32)

        private static float nextDiagnosticTime;
        private static int diagnosticsLeft = 8;
        private static bool controllersWereReported;

        /// <summary>
        /// ПЕРИОДИЧЕСКИЙ ОТЧЁТ О ВВОДЕ. Зачем: шлем надевают ПОСЛЕ запуска приложения, и
        /// контроллеры появляются в системе не сразу. Одиночная строка в `KvXrRig.Start()` этого
        /// не показывает — именно так в §31 остался без ответа вопрос «устройств в системе 0:
        /// это дефект или спящий шлем?». Здесь состав печатается несколько раз, а появление
        /// контроллера отмечается отдельной строкой с перечнем его управляющих элементов.
        /// </summary>
        public static void TickDiagnostics(float intervalSeconds = 2f, int reports = 8)
        {
            if (diagnosticsLeft <= 0) return;
            if (Time.realtimeSinceStartup < nextDiagnosticTime) return;

            nextDiagnosticTime = Time.realtimeSinceStartup + Mathf.Max(0.5f, intervalSeconds);
            diagnosticsLeft--;

            Scan();
            Debug.Log("[KvXr] " + DescribeDevices());

            if (!controllersWereReported && (right != null || left != null))
            {
                controllersWereReported = true;
                Debug.Log("[KvXr] КОНТРОЛЛЕРЫ ПОЯВИЛИСЬ: правый " +
                          (right != null ? right.name : "нет") + " (" + ControlSummary(right) + ")" +
                          " · левый " + (left != null ? left.name : "нет") + " (" + ControlSummary(left) + ")" +
                          " · курок " + RightTrigger.ToString("0.00") +
                          " · стик " + RightStick.ToString("0.00"));
            }
        }

        /// <summary>
        /// ПОДРОБНЫЙ СОСТАВ ВВОДА: сколько устройств видит Input System, какие у них layout,
        /// какие из них XR, и какие управляющие элементы нашлись у контроллеров. Это ответ
        /// на вопрос «контроллеры не обнаружены — почему»: по строке видно, пуст ли список
        /// устройств (шлем спит), или устройства есть, но у них нет нужных элементов (профиль).
        /// </summary>
        public static string DescribeDevices()
        {
            EnsureScanned();

            var sb = new System.Text.StringBuilder();
            sb.Append("XR-ввод: шлем ").Append(head != null ? "есть" : "НЕТ")
              .Append(" · правый ").Append(right != null ? "есть" : "нет")
              .Append(" · левый ").Append(left != null ? "есть" : "нет")
              .Append(" · устройств в InputSystem ").Append(InputSystem.devices.Count);

            var devices = InputSystem.devices;
            // Печатаем ВСЕ устройства: на шлеме их 24, и при ограничении в 10 строк шлем (XRHMD)
            // мог не попасть в список — именно так и вышло 13.10.2026 при разборе карточки Б14.
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice d = devices[i];
                if (d == null) continue;
                sb.Append("\n    [").Append(i).Append("] ").Append(d.displayName)
                  .Append(" · layout ").Append(d.layout)
                  .Append(" · added ").Append(d.added)
                  .Append(" · enabled ").Append(d.enabled)
                  .Append(" · XRController ").Append(d is XRController ? "да" : "нет")
                  .Append(" · XRHMD ").Append(d is XRHMD ? "да" : "нет");
            }

            if (devices.Count == 0)
                sb.Append(" — В СИСТЕМЕ НЕТ НИ ОДНОГО УСТРОЙСТВА ВВОДА");

            if (right != null) sb.Append("\n    правый: ").Append(ControlSummary(right));
            if (left != null) sb.Append("\n    левый: ").Append(ControlSummary(left));
            return sb.ToString();
        }

        /// <summary>Счётчики диагностики — для тестов: сколько отчётов уже напечатано.</summary>
        public static int DiagnosticsLeft { get { return diagnosticsLeft; } }

        private static bool IsRightHanded(XRController c)
        {
            var hand = c.TryGetChildControl<IntegerControl>("handedness");
            if (hand != null && hand.ReadValue() == 1) return true;        // Right
            return NameSays(c, "right");
        }

        private static bool IsLeftHanded(XRController c)
        {
            var hand = c.TryGetChildControl<IntegerControl>("handedness");
            if (hand != null && hand.ReadValue() == 2) return true;        // Left
            return NameSays(c, "left");
        }

        private static bool NameSays(XRController c, string needle)
        {
            return c.name != null && c.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
