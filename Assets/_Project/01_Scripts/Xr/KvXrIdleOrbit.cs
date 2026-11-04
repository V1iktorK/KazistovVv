using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ОБЛЁТ ПРОСТОЯ В ШЛЕМЕ (просьба владельца 04.11.2026: «камеру летающую оставь, но после
    /// продолжительного простоя; после шевеления контроллерами — нормальная камера»).
    ///
    /// ЧТО БЫЛО. «Летающая камера» на тесте оказалась багом, а не эффектом: зал ставился по
    /// объекту-камере, пока трекинг головы ещё не готов, и мир ездил каждый кадр. Это исправлено
    /// в `KvXrHangarAnchor` (зал ставится только по достоверной позе шлема и один раз).
    ///
    /// ЧТО СТАЛО. Настоящий облёт — ОСОЗНАННЫЙ режим: если оператор долго ничего не делает
    /// (не двигает контроллерами и головой), камера медленно объезжает активного робота. Любое
    /// движение мгновенно возвращает управление, и камера остаётся там, где оператор её оставил.
    /// Порог простоя намеренно большой: в шлеме самодвижущаяся картинка переносится плохо, поэтому
    /// это именно «режим простоя», а не постоянное поведение.
    ///
    /// ГДЕ НЕ РАБОТАЕТ. В MR (`KvMrRoom.MrActive`) облёт выключен: комната реальная и привязана
    /// к миру, двигать оператора вокруг стола нельзя — «вторая реальность» остаётся на месте.
    /// </summary>
    public class KvXrIdleOrbit : MonoBehaviour
    {
        /// <summary>Сколько секунд полного покоя нужно, чтобы включился облёт (3 минуты).</summary>
        public float idleSeconds = 180f;

        /// <summary>Радиус облёта вокруг активного робота, метры.</summary>
        public float orbitRadius = 2.6f;

        /// <summary>Скорость облёта, градусов в секунду.</summary>
        public float orbitSpeedDegrees = 5f;

        /// <summary>Шаг движения контроллера/головы, считающийся активностью, метры.</summary>
        public float activityMoveMeters = 0.012f;

        /// <summary>Облёт идёт прямо сейчас (диагностика и тесты).</summary>
        public bool Orbiting { get; private set; }

        private float lastActivityTime;
        private float angle;
        private float nextSeek;
        private Vector3 orbitCenter;
        private Vector3 rigStart;
        private Vector3 lastLeft;
        private Vector3 lastRight;
        private Vector3 lastHead;
        private bool hasLastPoses;
        private KvXrMrRoom cachedRoom;

        /// <summary>Оператор двигался за последний кадр (для журнала и тестов).</summary>
        public bool LastFrameHadActivity { get; private set; }

        void Start()
        {
            lastActivityTime = Time.unscaledTime;
            nextSeek = Time.unscaledTime;

            // КОМНАТА ИЩЕТСЯ ОДИН РАЗ, А НЕ КАЖДЫЙ КАДР: поиск по сцене в `Update` — 90 обращений
            // в секунду и украденный бюджет кадра (сторож `KvFrameBudgetTests` ловит именно это).
            cachedRoom = Object.FindAnyObjectByType<KvXrMrRoom>();
        }

        void Update()
        {
            // В MR облёт запрещён: комната реальная, оператор должен стоять на месте.
            // `MrActive` — свойство экземпляра комнаты; ссылка взята ОДИН раз в `Start`, потому что
            // поиск по сцене в `Update` — это 90 обращений в секунду (сторож `KvFrameBudgetTests`).
            bool mrActive = (cachedRoom != null && cachedRoom.MrActive) || KvXrSettings.GetPassthrough();
            if (mrActive)
            {
                if (Orbiting) StopOrbit("MR: мир привязан к реальной комнате");
                return;
            }

            LastFrameHadActivity = DetectActivity();
            if (LastFrameHadActivity)
            {
                lastActivityTime = Time.unscaledTime;
                if (Orbiting) StopOrbit("оператор двигается — управление камерой вернулось");
                return;
            }

            if (!Orbiting && Time.unscaledTime - lastActivityTime >= Mathf.Max(10f, idleSeconds))
            {
                BeginOrbit();
            }

            if (!Orbiting) return;

            if (Time.unscaledTime >= nextSeek)
            {
                nextSeek = Time.unscaledTime + 2f;
                Transform robot = FindActiveRobot();
                if (robot != null) orbitCenter = robot.position;
            }

            angle += Mathf.Max(0.1f, orbitSpeedDegrees) * Time.deltaTime;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 position = orbitCenter + new Vector3(
                Mathf.Cos(radians) * orbitRadius, rigStart.y - orbitCenter.y,
                Mathf.Sin(radians) * orbitRadius);

            // Двигаем РИГ (а не камеру): позу головы переписывать нельзя, её ведёт трекинг.
            transform.position = Vector3.Lerp(transform.position, position, 1f - Mathf.Exp(-1.5f * Time.deltaTime));
        }

        /// <summary>Начать облёт: запомнить, откуда стартовали, и выйти на орбиту.</summary>
        private void BeginOrbit()
        {
            Transform robot = FindActiveRobot();
            orbitCenter = robot != null ? robot.position : transform.position + transform.forward * orbitRadius;
            rigStart = transform.position;
            angle = Mathf.Atan2(rigStart.z - orbitCenter.z, rigStart.x - orbitCenter.x);
            Orbiting = true;
            Debug.Log("[KvXr] ОБЛЁТ ПРОСТОЯ: включён после " + Mathf.RoundToInt(Time.unscaledTime - lastActivityTime) +
                      " с покоя · центр — робот " + (robot != null ? robot.name : "не найден") +
                      " · скорость " + orbitSpeedDegrees.ToString("0") + " град/с");
        }

        /// <summary>
        /// Остановить облёт; позицию НЕ откатываем (иначе был бы рывок-телепорт).
        /// </summary>
        private void StopOrbit(string reason)
        {
            Orbiting = false;
            Debug.Log("[KvXr] ОБЛЁТ ПРОСТОЯ: остановлен — " + reason + " · камера снова обычная");
        }

        /// <summary>Движение контроллеров ИЛИ головы — активность оператора.</summary>
        private bool DetectActivity()
        {
            // ОТСЕВ НЕЧИСЛА (правило E-21): сравнение длины с NaN всегда даёт false, и мусорная
            // поза «навсегда» выглядела бы как покой. Поэтому порог и позы сначала проверяются.
            if (float.IsNaN(activityMoveMeters) || float.IsInfinity(activityMoveMeters)) return true;
            if (float.IsNaN(orbitSpeedDegrees) || float.IsInfinity(orbitSpeedDegrees)) return true;

            Vector3 left = Vector3.zero, right = Vector3.zero, head = Vector3.zero;
            bool haveLeft = KvXrInputSource.TryPose(KvXrQuickHand.Left, out left, out _);
            bool haveRight = KvXrInputSource.TryPose(KvXrQuickHand.Right, out right, out _);
            bool haveHead = KvXrInputSource.TryHeadPose(out head, out _);

            haveLeft = haveLeft && IsFinite(left);
            haveRight = haveRight && IsFinite(right);
            haveHead = haveHead && IsFinite(head);

            bool active = false;
            float threshold = activityMoveMeters * activityMoveMeters;
            if (hasLastPoses)
            {
                if (haveLeft && IsFinite(lastLeft) && (left - lastLeft).sqrMagnitude > threshold) active = true;
                if (haveRight && IsFinite(lastRight) && (right - lastRight).sqrMagnitude > threshold) active = true;
                if (haveHead && IsFinite(lastHead) && (head - lastHead).sqrMagnitude > threshold) active = true;
            }

            if (haveLeft) lastLeft = left;
            if (haveRight) lastRight = right;
            if (haveHead) lastHead = head;
            hasLastPoses = haveLeft || haveRight || haveHead || hasLastPoses;
            return active;
        }

        /// <summary>Вектор без NaN/бесконечности (проектное правило отсева нечисла).</summary>
        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        /// <summary>Число без NaN/бесконечности.</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>Активный робот: сначала рабочий (isActive), иначе первый найденный.</summary>
        private static Transform FindActiveRobot()
        {
            RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < robots.Length; i++)
                if (robots[i] != null && robots[i].isActive) return robots[i].transform;
            return robots.Length > 0 && robots[0] != null ? robots[0].transform : null;
        }
    }
}
