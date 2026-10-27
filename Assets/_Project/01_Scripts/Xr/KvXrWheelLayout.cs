using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// КОЛЕСО СИМВОЛОВ НА СТИКЕ — ТРЕТИЙ СПОСОБ ВВОДА (задание [S1]).
    ///
    /// КАК ПОЛЬЗОВАТЬСЯ (задание [A3]): стик переводит выбор между символами, курок или щипок
    /// вставляет выбранный символ, второй щипок левой рукой — «ввод».
    ///
    /// ПОЧЕМУ КРУГ, А НЕ СПИСОК. Круг читается с любого места одинаково, и в него без прокрутки
    /// влезает 13 секторов — ровно столько, сколько нужно для чисел: десять цифр, точка, минус и
    /// «отбой». Порядок — по кругу от правой точки против часовой стрелки (та же система координат,
    /// что у панели: 0° — вправо).
    ///
    /// ЗАХВАТ СТИКА. Стик в шлеме уже занят: правый — поворот шагами (`KvXrLocomotion.HandleSnapTurn`,
    /// порог 0,7, шаг 30°), левый — плавный ход. Если колесо не захватит стик, то выбор символа будет
    /// ещё и разворачивать оператора. Поэтому правило захвата — здесь, чистой функцией, и оно же
    /// используется локомоцией.
    /// </summary>
    public static class KvXrWheelLayout
    {
        /// <summary>Расстояние символов от центра колеса, метры.</summary>
        public const float RadiusMeters = 0.16f;

        /// <summary>Сколько секторов на колесе: 10 цифр + точка + минус + «отбой».</summary>
        public const int SymbolCount = 13;

        /// <summary>Порог стика, после которого считается шаг (как у поворота шагами).</summary>
        public const float StickThreshold = 0.7f;

        /// <summary>Сектор шагом в градусах.</summary>
        public const float SectorDegrees = 360f / SymbolCount;

        /// <summary>Символ сектора (для «отбоя» — '\0').</summary>
        public static char Symbol(int index)
        {
            int i = Wrap(index);
            if (i < 10) return (char)('0' + i);
            if (i == 10) return '.';
            if (i == 11) return '-';
            return '\0';
        }

        /// <summary>Подпись сектора: «7», «.», «−», «отбой».</summary>
        public static string SymbolLabel(int index)
        {
            int i = Wrap(index);
            if (i < 10) return ((char)('0' + i)).ToString();
            if (i == 10) return ".";
            if (i == 11) return "−";
            return "отбой";
        }

        /// <summary>Угол сектора в градусах: 0 — вправо, отсчёт против часовой стрелки (как у панели).</summary>
        public static float AngleDegrees(int index)
        {
            return Wrap(index) * SectorDegrees;
        }

        /// <summary>Смещение центра сектора от центра колеса, метры.</summary>
        public static Vector2 OffsetMeters(int index)
        {
            float radians = AngleDegrees(index) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * RadiusMeters;
        }

        /// <summary>
        /// ШАГ ВЫБОРА. `direction` > 0 — следующий символ, < 0 — предыдущий; по кругу, без краёв:
        /// на последнем символе шаг вперёд возвращает к первому, а не «упирается».
        /// </summary>
        public static int Step(int index, int direction)
        {
            return Wrap(Wrap(index) + (direction >= 0 ? 1 : -1));
        }

        /// <summary>Сектор, ближайший к точке на плоскости панели (метры).</summary>
        public static int IndexAt(Vector2 flatPointMeters)
        {
            if (flatPointMeters.sqrMagnitude < 1e-8f) return 0;

            float degrees = Mathf.Atan2(flatPointMeters.y, flatPointMeters.x) * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;

            int index = Mathf.RoundToInt(degrees / SectorDegrees);
            return Wrap(index);
        }

        /// <summary>
        /// ЗАХВАТЫВАЕТ ЛИ ВВОД СТИК. Правило: пока открыт экран ввода и выбран способ «колесо»,
        /// стик принадлежит колесу; в остальных случаях — локомоции. Отдельная функция, потому что
        /// это правило проверяется тестом и используется двумя файлами сразу.
        /// </summary>
        public static bool CapturesStick(bool inputOpen, KvXrInputMethod method)
        {
            return inputOpen && method == KvXrInputMethod.Wheel;
        }

        /// <summary>Привести номер сектора к диапазону 0…SymbolCount−1 (по кругу).</summary>
        private static int Wrap(int index)
        {
            int value = index % SymbolCount;
            if (value < 0) value += SymbolCount;
            return value;
        }
    }
}
