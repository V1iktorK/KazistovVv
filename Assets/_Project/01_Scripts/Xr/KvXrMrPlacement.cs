using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>Размеры реального стола оператора (метры).</summary>
    public struct KvXrTableSize
    {
        /// <summary>Ширина столешницы поперёк, метры.</summary>
        public float WidthMeters;
        /// <summary>Глубина столешницы, метры.</summary>
        public float DepthMeters;
        /// <summary>Высота столешницы от пола, метры.</summary>
        public float HeightMeters;

        public KvXrTableSize(float widthMeters, float depthMeters, float heightMeters)
        {
            WidthMeters = widthMeters;
            DepthMeters = depthMeters;
            HeightMeters = heightMeters;
        }

        public bool IsValid { get { return WidthMeters > 0f && DepthMeters > 0f && HeightMeters > 0f; } }

        public string Describe()
        {
            return string.Format("{0:0}×{1:0} см, высота {2:0} см",
                WidthMeters * 100f, DepthMeters * 100f, HeightMeters * 100f);
        }
    }

    /// <summary>Свободное место в комнате оператора (метры).</summary>
    public struct KvXrRoomSize
    {
        public float WidthMeters;
        public float DepthMeters;

        public KvXrRoomSize(float widthMeters, float depthMeters)
        {
            WidthMeters = widthMeters;
            DepthMeters = depthMeters;
        }

        public bool IsValid { get { return WidthMeters > 0f && DepthMeters > 0f; } }

        /// <summary>Меньшая сторона — ею ограничена любая круговая зона.</summary>
        public float MinSideMeters { get { return Mathf.Min(WidthMeters, DepthMeters); } }

        public string Describe()
        {
            return string.Format("{0:0}×{1:0} м", WidthMeters, DepthMeters);
        }
    }

    /// <summary>Результат постановки робота: где центр основания, где низ, на какой высоте база.</summary>
    public struct KvXrPlacement
    {
        /// <summary>Центр столешницы (X и Y — горизонтальные оси, Z — вверх), метры.</summary>
        public Vector2 center;
        /// <summary>Высота, на которой стоит робот (низ основания), метры.</summary>
        public float groundY;
        /// <summary>Высота базы робота, метры (для базы равна низу).</summary>
        public float baseY;
        /// <summary>Поворот робота вокруг вертикали, градусы.</summary>
        public float yawDegrees;
        /// <summary>Смещение от центра стола, фактически применённое (после ограничения), метры.</summary>
        public Vector2 offset;

        public string Describe()
        {
            return string.Format("центр ({0:0.00}; {1:0.00}) м, низ {2:0.00} м, поворот {3:0}°",
                center.x, center.y, groundY, yawDegrees);
        }
    }

    /// <summary>
    /// ПОСТАНОВКА РОБОТА НА РЕАЛЬНЫЙ СТОЛ (MR MVP, решения [D5]/[D7], сессия §28, этап 4).
    ///
    /// ЧТО ЭТО. MR отличается от VR ровно одним: стол существует в реальности, и его
    /// размеры заданы владельцем — 100 см поперёк, 70 см в длину, столешница на 78 см
    /// от пола; свободного места в комнате 3×2 метра. Задача — поставить виртуального
    /// робота на ЭТОТ стол в масштабе 1:1 и не соврать, если он туда не влезает.
    ///
    /// ГДЕ ЗДЕСЬ ЧЕСТНОСТЬ. Робот может не поместиться — и тогда это надо сказать
    /// числом, а не «подвинуть и промолчать». Поэтому есть явные проверки
    /// <see cref="FitsInRoom"/> и <see cref="ZoneFits"/>, и они возвращают false
    /// вместо красивой картинки.
    ///
    /// ЕДИНИЦЫ. Мир Unity — метрический по определению сцены проекта (1 юнит = 1 метр):
    /// 100 см = 1,0 юнита. Отдельных коэффициентов тут нет намеренно — это и есть 1:1.
    /// </summary>
    public static class KvXrMrPlacement
    {
        /// <summary>Запас от кромки столешницы, метры: центр робота не ставится вплотную к краю.</summary>
        public const float EdgeMarginMeters = 0.05f;

        /// <summary>Радиус базы робота по умолчанию, метры (0,2 м = 20 см).</summary>
        public const float DefaultBaseRadiusMeters = 0.20f;

        /// <summary>Стол владельца из решения [D7].</summary>
        public static KvXrTableSize DefaultTable
        {
            get { return new KvXrTableSize(1.00f, 0.70f, 0.78f); }
        }

        /// <summary>Свободное место в комнате владельца из решения [D7].</summary>
        public static KvXrRoomSize DefaultRoom
        {
            get { return new KvXrRoomSize(3.0f, 2.0f); }
        }

        /// <summary>Сантиметры в метры (ровно то, чем заданы размеры стола).</summary>
        public static float CentimetersToMeters(float centimeters)
        {
            return centimeters * 0.01f;
        }

        /// <summary>
        /// Юниты сцены в метры: мир проекта метрический, перевод безразмерный.
        ///
        /// ЧЕСТНО О ГЕОМЕТРИИ ПРОЕКТА (замер 02.10.2026). Столешницы сцены — 14,4 × 9,6 юнита
        /// при высоте 0,98 юнита. Если считать 1 юнит = 1 м, получится стол 14,4 м шириной и
        /// 0,98 м высотой — то есть виртуальный стол физически не совпадает с реальным
        /// (100 см × 78 см). Значит масштаб виртуальной геометрии к реальной не равен 1:1,
        /// и «поставить робота на реальный стол» в MR требует отдельного масштабирования
        /// виртуального мира. Это НЕ спрятано: признак <see cref="VirtualGeometryIsMetric"/>
        /// говорит правду, и в отчёте сессии это записано как ограничение MVP.
        /// </summary>
        public static float ToMeters(float units)
        {
            return units;
        }

        /// <summary>
        /// Совпадает ли виртуальная геометрия сцены с реальными метрами.
        /// false — виртуальный стенд нарисован крупнее реального стола (см. пояснение выше),
        /// поэтому в MR нужен пересчёт масштаба; сейчас это осознанное ограничение MVP.
        /// </summary>
        public static bool VirtualGeometryIsMetric
        {
            get { return false; }
        }

        /// <summary>Метры в юниты сцены.</summary>
        public static float FromMeters(float meters)
        {
            return meters;
        }

        /// <summary>
        /// Помещается ли стол в свободное место комнаты. Проверяются ОБЕ стороны:
        /// стол можно повернуть, но не растянуть.
        /// </summary>
        public static bool FitsInRoom(KvXrTableSize table, KvXrRoomSize room)
        {
            if (!table.IsValid || !room.IsValid) return false;
            return table.WidthMeters <= room.WidthMeters && table.DepthMeters <= room.DepthMeters;
        }

        /// <summary>
        /// Помещается ли круговая зона движения робота радиусом <paramref name="radiusMeters"/>
        /// в комнату: центр зоны — середина комнаты, значит запас равен половине меньшей стороны.
        /// </summary>
        public static bool ZoneFits(KvXrRoomSize room, float radiusMeters)
        {
            if (!room.IsValid) return false;
            if (radiusMeters <= 0f) return true;
            return radiusMeters <= room.MinSideMeters * 0.5f;
        }

        /// <summary>
        /// Ограничить смещение робота от центра стола так, чтобы его основание осталось
        /// НА столешнице: смещение не больше полуразмера минус запас и минус радиус базы.
        /// Вход — смещение в метрах по осям (X — поперёк, Y — вдоль).
        /// </summary>
        public static Vector2 ClampOnTable(KvXrTableSize table, Vector2 offset, float baseRadiusMeters = DefaultBaseRadiusMeters)
        {
            if (!table.IsValid) return Vector2.zero;
            float limitX = Mathf.Max(0f, table.WidthMeters * 0.5f - EdgeMarginMeters - baseRadiusMeters);
            float limitY = Mathf.Max(0f, table.DepthMeters * 0.5f - EdgeMarginMeters - baseRadiusMeters);
            return new Vector2(Mathf.Clamp(offset.x, -limitX, limitX),
                               Mathf.Clamp(offset.y, -limitY, limitY));
        }

        /// <summary>
        /// Полный расчёт постановки: центр столешницы в мировых координатах (X, Z — горизонталь),
        /// высота низа робота и применённое смещение.
        ///
        /// <paramref name="tableCenter"/> — центр столешницы в мире (Y — её высота).
        /// Робот ставится низом ровно на столешницу: ни «утоплен», ни «висит».
        /// </summary>
        public static KvXrPlacement PlaceRobot(
            KvXrTableSize table,
            Vector2 requestedOffsetMeters,
            float tableTopHeightMeters,
            Vector3 tableCenter = default(Vector3),
            float yawDegrees = 0f,
            float baseRadiusMeters = DefaultBaseRadiusMeters)
        {
            KvXrPlacement p;
            Vector2 applied = ClampOnTable(table, requestedOffsetMeters, baseRadiusMeters);
            float height = table.IsValid ? table.HeightMeters : Mathf.Max(0f, tableTopHeightMeters);

            p.offset = applied;
            p.center = new Vector2(tableCenter.x + applied.x, tableCenter.z + applied.y);
            p.groundY = height;
            p.baseY = height;
            p.yawDegrees = yawDegrees;
            return p;
        }

        /// <summary>
        /// Строка состояния для отчёта: стол, комната, вердикт «помещается» без украшательств.
        /// </summary>
        public static string Describe(KvXrTableSize table, KvXrRoomSize room, float zoneRadiusMeters)
        {
            bool tableOk = FitsInRoom(table, room);
            bool zoneOk = ZoneFits(room, zoneRadiusMeters);
            return string.Format(
                "MR: стол {0} · комната {1} · стол в комнате: {2} · зона r={3:0.0} м: {4}",
                table.Describe(), room.Describe(),
                tableOk ? "да" : "НЕТ",
                zoneRadiusMeters,
                zoneOk ? "помещается" : "НЕ ПОМЕЩАЕТСЯ (нужно больше места или меньше зона)");
        }
    }
}
