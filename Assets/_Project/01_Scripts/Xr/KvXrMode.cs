using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>ЧЕМ УПРАВЛЯЕТ ОПЕРАТОР ПРЯМО СЕЙЧАС (§40).</summary>
    public enum KvXrControlMode
    {
        /// <summary>Ни кистей, ни контроллеров — управлять нечем.</summary>
        None = 0,

        /// <summary>Контроллеры в руках.</summary>
        Controllers = 1,

        /// <summary>Контроллеры отложены — управление кистями и жестами.</summary>
        Hands = 2
    }

    /// <summary>
    /// ОПРЕДЕЛЕНИЕ РЕЖИМА УПРАВЛЕНИЯ (§40, слова владельца: «когда переходит управление на жесты —
    /// квадратики появляются у левой руки и лучи из указательных пальцев идут длинные как у
    /// контроллеров»).
    ///
    /// ЧТО БЫЛО. Журнал `Logs\device-s39.log`: контроллеры лежали на столе, но продолжали считаться
    /// «в игре» — панель ехала за лежащим левым контроллером (`панель в (-0.58, 0.80, -0.15)`), а
    /// лучи шли от них на 10 метров в стены. При этом кисти работали, и жесты нажимали иконки. Два
    /// режима работали одновременно и мешали друг другу.
    ///
    /// КАК РАЗЛИЧАЕМ. Единственный надёжный признак «контроллер в руке» — расстояние между кистью и
    /// контроллером: в руке оно 5–15 см, а лежащий контроллер от руки дальше 20 см. Порог
    /// <see cref="ControllerInHandMeters"/> = 0,20 м взят с запасом в обе стороны.
    /// </summary>
    public static class KvXrModeResolver
    {
        /// <summary>Ближе этого расстояния «кисть ↔ контроллер» контроллер считается взятым в руку, метры.</summary>
        public const float ControllerInHandMeters = 0.20f;

        /// <summary>
        /// ЧИСТОЕ ПРАВИЛО РЕЖИМА. Вынесено отдельно, чтобы проверялось тестом без шлема.
        /// </summary>
        public static KvXrControlMode Resolve(bool handTracked, bool controllerTracked, float handToControllerMeters)
        {
            if (!handTracked) return controllerTracked ? KvXrControlMode.Controllers : KvXrControlMode.None;
            if (!controllerTracked) return KvXrControlMode.Hands;

            return handToControllerMeters <= ControllerInHandMeters
                ? KvXrControlMode.Controllers
                : KvXrControlMode.Hands;
        }

        /// <summary>
        /// СКОЛЬКО СЕКУНД НОВЫЙ РЕЖИМ ДОЛЖЕН ДЕРЖАТЬСЯ, ПРЕЖДЕ ЧЕМ ЕГО ПРИМЕНИТЬ.
        ///
        /// ЗАЧЕМ (найдено при перечитывании кода 23.10.2026). Трекинг кистей на этом рантайме
        /// пропадает на кадр-два (в журнале это видно: `левая нет · правая есть` меняется местами).
        /// Без задержки КАЖДАЯ такая потеря переключала бы режим: луч прыгал бы с пальца на
        /// контроллер и обратно, панель перескакивала с ладони на контроллер, а в журнале был бы
        /// поток строк `РЕЖИМ:` вместо события. 0,4 с — меньше, чем человек замечает, и больше, чем
        /// длится случайный провал трекинга.
        /// </summary>
        public const float SwitchDelaySeconds = 0.40f;

        /// <summary>
        /// ЧТО ПРИМЕНИТЬ СЕЙЧАС: новый режим держится достаточно долго — применить, иначе оставить
        /// прежний. Чистая функция, проверяется тестом.
        /// </summary>
        public static KvXrControlMode Settle(KvXrControlMode current, KvXrControlMode candidate,
            float candidateAgeSeconds, float delaySeconds)
        {
            if (candidate == current) return current;
            return candidateAgeSeconds >= Mathf.Max(0f, delaySeconds) ? candidate : current;
        }

        /// <summary>Название режима для журнала и экрана «Состояние».</summary>
        public static string ModeName(KvXrControlMode mode)
        {
            switch (mode)
            {
                case KvXrControlMode.Controllers: return "контроллеры";
                case KvXrControlMode.Hands: return "кисти";
                default: return "нет управления";
            }
        }
    }

    /// <summary>
    /// ЛУЧ ИЗ КОНЧИКА УКАЗАТЕЛЬНОГО ПАЛЬЦА — РЕЖИМ КИСТЕЙ (§40).
    ///
    /// РЕШЕНИЕ ВЛАДЕЛЬЦА: «короткий луч из указательного пальца — до панели, не длиннее 1,5 м».
    /// Длинный луч на 10 м в режиме кистей упирался в стены и мешал смотреть (это и было «лучи идут
    /// длинные как у контроллеров»).
    /// </summary>
    public static class KvXrHandRay
    {
        /// <summary>Наибольшая длина луча из пальца, метры.</summary>
        public const float MaxLengthMeters = 1.5f;

        /// <summary>
        /// ПОСТРОИТЬ ЛУЧ: начало — кончик указательного, направление — от основания к кончику.
        /// Нет достоверной кисти — луча нет (выдумывать точку запрещено).
        /// </summary>
        public static bool TryGetRay(KvHandFrame frame, out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            if (!KvGestureMath.IsUsable(frame)) return false;

            Vector3 along = frame.indexTip - frame.indexProximal;
            if (along.sqrMagnitude < 1e-6f) along = frame.indexTip - frame.wrist;
            if (along.sqrMagnitude < 1e-6f) return false;

            origin = frame.indexTip;
            direction = along.normalized;
            return true;
        }

        /// <summary>Длина: попал в панель — до неё, промах — ровно предел (1,5 м по умолчанию).</summary>
        public static float LengthMeters(bool overIcon, float distanceToHitMeters, float maxLengthMeters)
        {
            float limit = Mathf.Max(0.05f, maxLengthMeters <= 0f ? MaxLengthMeters : maxLengthMeters);
            if (!overIcon) return limit;
            if (distanceToHitMeters <= 0f || distanceToHitMeters > limit) return limit;
            return distanceToHitMeters;
        }
    }

    /// <summary>
    /// ПАНЕЛЬ У ЛАДОНИ — РЕЖИМ КИСТЕЙ (§40). Решение владельца: «к левой кисти (ладонь/запястье)».
    ///
    /// Смещение то же, что у контроллера (0,12 м вперёд, 0,04 м вверх), но «вперёд» берётся от
    /// направления кисти: от запястья к кончику указательного пальца. Так панель едет за рукой и
    /// поворачивается вместе с ней, а лежащий на столе контроллер больше не тащит её за собой.
    /// </summary>
    public static class KvXrHandPanelPlacement
    {
        /// <summary>Вынос панели по направлению кисти, метры.</summary>
        public const float ForwardMeters = 0.12f;

        /// <summary>Подъём панели над ладонью, метры.</summary>
        public const float UpMeters = 0.04f;

        /// <summary>Положение и направление панели по позе кисти.</summary>
        public static bool TryGet(KvHandFrame frame, out Vector3 position, out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;

            if (!KvGestureMath.IsUsable(frame)) return false;

            Vector3 along = frame.indexTip - frame.wrist;
            if (along.sqrMagnitude < 1e-6f) along = frame.indexTip - frame.indexProximal;
            if (along.sqrMagnitude < 1e-6f) return false;

            forward = along.normalized;
            position = frame.palm + forward * ForwardMeters + Vector3.up * UpMeters;
            return true;
        }
    }

    /// <summary>
    /// ДЕЙСТВУЮЩИЙ РЕЖИМ УПРАВЛЕНИЯ (обновляется раз в кадр из `KvXrRig`).
    ///
    /// Смена режима — СОБЫТИЕ, а не мелочь: по ней переключаются луч (контроллер ↔ палец), крепление
    /// панели (контроллер ↔ ладонь) и видимость второго луча. Поэтому каждая смена печатается одной
    /// строкой с числами — по ней видно, почему в шлеме поменялось поведение.
    /// </summary>
    public static class KvXrMode
    {
        /// <summary>Режим правой руки.</summary>
        public static KvXrControlMode Right { get; private set; }

        /// <summary>Режим левой руки.</summary>
        public static KvXrControlMode Left { get; private set; }

        /// <summary>Последнее измеренное расстояние «кисть ↔ контроллер» для руки луча, метры.</summary>
        public static float LastDistance { get; private set; }

        /// <summary>Режим конкретной руки.</summary>
        public static KvXrControlMode For(KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Right ? Right : Left;
        }

        /// <summary>Режим руки, из которой идёт луч (по нему работает указатель).</summary>
        public static KvXrControlMode Current { get { return For(KvXrInputSource.RayHand); } }

        /// <summary>Строка состояния для журнала и экрана «Состояние».</summary>
        public static string Description
        {
            get
            {
                return "режим: правая — " + KvXrModeResolver.ModeName(Right) +
                       " · левая — " + KvXrModeResolver.ModeName(Left);
            }
        }

        /// <summary>
        /// ПЕРЕСЧИТАТЬ РЕЖИМ (зовётся каждый кадр). Сравнение идёт по расстоянию кисть↔контроллер,
        /// а переключение — с задержкой 0,4 с (`Settle`), чтобы провал трекинга на кадр не переключал
        /// режим туда-обратно.
        /// </summary>
        public static void Tick()
        {
            KvXrControlMode rawRight = Measure(KvXrQuickHand.Right);
            KvXrControlMode rawLeft = Measure(KvXrQuickHand.Left);

            KvXrControlMode right = SettleFor(KvXrQuickHand.Right, rawRight);
            KvXrControlMode left = SettleFor(KvXrQuickHand.Left, rawLeft);

            bool rayHandChanged = KvXrInputSource.RayHand == KvXrQuickHand.Right
                ? right != Right
                : left != Left;

            Right = right;
            Left = left;

            if (!rayHandChanged) return;

            KvXrControlMode current = Current;
            if (current == KvXrControlMode.Hands)
            {
                Debug.Log("[KvXr] РЕЖИМ: кисти активны — контроллер отложен (кисть в " +
                          LastDistance.ToString("0.00") + " м от контроллера) · луч из указательного пальца · " +
                          "панель у ладони · второй луч выключен");
            }
            else if (current == KvXrControlMode.Controllers)
            {
                Debug.Log("[KvXr] РЕЖИМ: контроллеры в руках (кисть в " + LastDistance.ToString("0.00") +
                          " м от контроллера) · луч из контроллера · панель у контроллера");
            }
            else
            {
                Debug.Log("[KvXr] РЕЖИМ: управлять нечем — ни кисти, ни контроллера");
            }
        }

        // Состояние задержки переключения — по руке.
        private static readonly KvXrControlMode[] candidate = new KvXrControlMode[2];
        private static readonly float[] candidateSince = new float[2];
        private static readonly bool[] candidateKnown = new bool[2];

        private static KvXrControlMode SettleFor(KvXrQuickHand hand, KvXrControlMode raw)
        {
            int index = hand == KvXrQuickHand.Right ? 1 : 0;
            KvXrControlMode current = For(hand);

            if (!candidateKnown[index] || candidate[index] != raw)
            {
                candidate[index] = raw;
                candidateSince[index] = Time.unscaledTime;
                candidateKnown[index] = true;
            }

            float age = Time.unscaledTime - candidateSince[index];
            return KvXrModeResolver.Settle(current, raw, age, KvXrModeResolver.SwitchDelaySeconds);
        }

        private static KvXrControlMode Measure(KvXrQuickHand hand)
        {
            KvHandFrame frame;
            bool handTracked = KvXrHandSource.TryGet(hand, out frame);

            Vector3 controllerPosition;
            Quaternion controllerRotation;
            bool controllerTracked = KvXrInputSource.TryTrackedPose(hand, out controllerPosition, out controllerRotation);

            float distance = 99f;
            if (handTracked && controllerTracked) distance = Vector3.Distance(frame.wrist, controllerPosition);

            if (hand == KvXrInputSource.RayHand) LastDistance = distance;
            return KvXrModeResolver.Resolve(handTracked, controllerTracked, distance);
        }
    }

    /// <summary>Данные для экрана «Состояние».</summary>
    public struct KvXrStatusData    {
        public string Mode;
        public string MoveMode;
        public float Fps;
        public float HeadHeight;
        public float PanelRadius;
        public float IconSizeMm;
        public int CommandCount;
        public bool SecondRay;
        public bool PlanningCandidates;
    }

    /// <summary>Данные для экрана «Настройки».</summary>
    public struct KvXrSettingsData
    {
        public float PanelRadius;
        public float SmoothSpeed;
        public float OperatorHeight;
        public float TeleportDistance;
        public bool SecondRay;
        public KvXrQuickHand RayHand;
    }

    /// <summary>Данные для экрана «Свойства».</summary>
    public struct KvXrObjectInfo
    {
        public bool Selected;
        public string Name;
        public string Kind;
        public Vector3 Position;
        public Vector3 Size;
    }

    /// <summary>
    /// ШАГ НАСТРОЙКИ (§40). «−» на минимуме не уходит ниже, «+» на максимуме — выше. Именно это
    /// нельзя проверить глазами в шлеме, поэтому проверяется тестом.
    /// </summary>
    public static class KvXrSettingStep
    {
        public static float Step(float value, float delta, float min, float max)
        {
            return Mathf.Clamp(value + delta, min, max);
        }

        public static int StepInt(int value, int delta, int min, int max)
        {
            return Mathf.Clamp(value + delta, min, max);
        }
    }

    /// <summary>
    /// СОДЕРЖИМОЕ ЭКРАНОВ ПАНЕЛИ (§40, «сразу всё перенесём»). Здесь только ЧИСТЫЕ функции:
    /// что именно попадает в строки. Рисует их панель, а проверяют — тесты без шлема.
    /// </summary>
    public static class KvXrScreenModel
    {
        /// <summary>Экран «Состояние»: что происходит прямо сейчас.</summary>
        public static List<string> BuildStatus(KvXrStatusData data)
        {
            var rows = new List<string>();
            rows.Add("Состояние");
            rows.Add("Режим управления: " + (string.IsNullOrEmpty(data.Mode) ? "—" : data.Mode));
            rows.Add("Перемещение: " + (string.IsNullOrEmpty(data.MoveMode) ? "—" : data.MoveMode));
            rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Кадров/с: {0:0.0}", data.Fps));
            rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Голова над полом: {0:0.00} м", data.HeadHeight));
            rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Радиус панели: {0:0.000} м · иконка {1:0} мм", data.PanelRadius, data.IconSizeMm));
            rows.Add("Команд в реестре: " + data.CommandCount);
            rows.Add("Второй луч: " + (data.SecondRay ? "включён" : "выключен"));
            rows.Add("Планировщик: " + (data.PlanningCandidates ? "кандидаты есть" : "кандидатов нет"));
            return rows;
        }

        /// <summary>Экран «Настройки»: текущие значения — по строке на параметр.</summary>
        public static List<string> BuildSettings(KvXrSettingsData data)
        {
            var rows = new List<string>();
            rows.Add("Настройки");
            rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Радиус панели: {0:0.000} м", data.PanelRadius));
            rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Скорость плавного хода: {0:0.0} м/с", data.SmoothSpeed));
            rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Рост оператора: {0:0.00} м", data.OperatorHeight));
            rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Дальность телепорта: {0:0.0} м", data.TeleportDistance));
            rows.Add("Второй луч: " + (data.SecondRay ? "включён" : "выключен"));
            rows.Add("Рука луча: " + (data.RayHand == KvXrQuickHand.Right ? "Правая" : "Левая"));
            return rows;
        }

        /// <summary>Экран «Объекты»: список из модели, выбранный помечен.</summary>
        public static List<string> BuildObjects(IList<string> names, int selectedIndex)
        {
            var rows = new List<string>();
            if (names == null) return rows;

            for (int i = 0; i < names.Count; i++)
            {
                string name = string.IsNullOrEmpty(names[i]) ? "(без имени)" : names[i];
                rows.Add(i == selectedIndex ? "► " + name : name);
            }
            return rows;
        }

        /// <summary>Экран «Свойства»: выбранный объект или честная строка «не выбран».</summary>
        public static List<string> BuildProperties(KvXrObjectInfo info)
        {
            var rows = new List<string>();
            if (!info.Selected)
            {
                rows.Add("Объект не выбран — откройте «Объекты» и выберите строку");
                return rows;
            }

            rows.Add("Объект: " + (string.IsNullOrEmpty(info.Name) ? "(без имени)" : info.Name));
            rows.Add("Тип: " + (string.IsNullOrEmpty(info.Kind) ? "—" : info.Kind));
            rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Положение: ({0:0.00}, {1:0.00}, {2:0.00})", info.Position.x, info.Position.y, info.Position.z));
            rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Габарит: ({0:0.00}, {1:0.00}, {2:0.00})", info.Size.x, info.Size.y, info.Size.z));
            return rows;
        }

        /// <summary>Экран «Справка»: что делают жесты, кнопки и второй луч.</summary>
        public static List<string> BuildHelp()
        {
            var rows = new List<string>();
            rows.Add("Справка");
            rows.Add("ЩИПОК (большой+указательный) — нажатие иконки под лучом");
            rows.Add("РАСКРЫТАЯ ЛАДОНЬ пальцами вверх — показать/скрыть панель");
            rows.Add("УКАЗАТЕЛЬНЫЙ ПАЛЕЦ — следующая группа");
            rows.Add("ЩИПОК ЛЕВОЙ — закрыть веер (выход)");
            rows.Add("Курок контроллера — нажатие; стик — поворот шагами");
            rows.Add("ВТОРОЙ ЛУЧ (левая рука) — только указатель, не нажимает");
            rows.Add("Панель у левой руки: есть поза руки — панель у руки, нет — у пояса");
            return rows;
        }
    }
}
