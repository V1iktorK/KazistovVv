using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>Способ перемещения оператора в шлеме (решение владельца [D2]).</summary>
    public enum KvXrMoveMode
    {
        /// <summary>ТЕЛЕПОРТ — основной способ: прицел, подтверждение, мгновенный перенос. Не укачивает.</summary>
        Teleport = 0,

        /// <summary>ПЛАВНЫЙ ХОД — включается в настройках: непрерывное движение по стику.</summary>
        Smooth = 1
    }

    /// <summary>
    /// НАСТРОЙКИ XR — единственное хранилище значений шлема (сессия §28).
    ///
    /// ПОЧЕМУ ОТДЕЛЬНЫЙ КЛАСС, А НЕ ПОЛЯ В НАСТРОЙКАХ СЦЕНЫ. Значения обязаны
    /// переживать перезапуск шлема, читаться без сцены (в том числе в EditMode-тестах)
    /// и задаваться до создания объектов — поэтому хранилище PlayerPrefs, как у
    /// <c>KazistovVvUI.KvSettings</c>, и никакой зависимости от GameObject.
    ///
    /// ЧТО ЗДЕСЬ РЕШЕНО ВЛАДЕЛЬЦЕМ:
    ///   • [D2] перемещение — ТЕЛЕПОРТ по умолчанию, плавный ход включается в настройках;
    ///   • [D8] виртуальное в MR включается тумблерами, ПО УМОЛЧАНИЮ ВЫКЛЮЧЕНО
    ///     (кроме панели интерфейса — она видна всегда).
    ///
    /// ВАЖНО ПРО ЧТЕНИЕ: геттеры PlayerPrefs не пишут и не требуют инициализации —
    /// поэтому их можно звать откуда угодно, включая тесты.
    /// </summary>
    public static class KvXrSettings
    {
        // ------------------------------------------------------------------ ключи хранения

        public const string KeyMoveMode = "KazistovVv.XR.MoveMode";
        public const string KeySmoothSpeed = "KazistovVv.XR.SmoothSpeed";
        public const string KeyTeleportDistance = "KazistovVv.XR.TeleportDistance";
        public const string KeyPanelDistance = "KazistovVv.XR.PanelDistance";
        public const string KeyWorldScale = "KazistovVv.XR.WorldScale";
        public const string KeyOperatorHeight = "KazistovVv.XR.OperatorHeight";
        public const string KeyShowPanel = "KazistovVv.XR.Layer.Panel";
        public const string KeyShowWorkZone = "KazistovVv.XR.Layer.WorkZone";
        public const string KeyShowReach = "KazistovVv.XR.Layer.Reach";
        public const string KeyShowVirtualStands = "KazistovVv.XR.Layer.VirtualStands";
        public const string KeyPassthrough = "KazistovVv.XR.Passthrough";

        // Настройки панели быстрого выбора у руки (решение владельца [D20], сессия §32).
        public const string KeyQuickPanelVisible = "KazistovVv.XR.Quick.Visible";
        public const string KeyQuickPanelShape = "KazistovVv.XR.Quick.Shape";
        public const string KeyQuickPanelHand = "KazistovVv.XR.Quick.Hand";
        public const string KeyQuickPanelRadius = "KazistovVv.XR.Quick.Radius";
        public const string KeyQuickPanelButtonCount = "KazistovVv.XR.Quick.Buttons";

        // ------------------------------------------------------------------ значения по умолчанию

        /// <summary>[D2] по умолчанию — телепорт.</summary>
        public const int DefaultMoveMode = (int)KvXrMoveMode.Teleport;
        /// <summary>Скорость плавного хода, м/с. Ограничена: быстрее — укачивает.</summary>
        public const float DefaultSmoothSpeed = 1.4f;
        /// <summary>Дальность телепорта, метры.</summary>
        public const float DefaultTeleportDistance = 6f;
        /// <summary>Расстояние плоской панели от глаз, метры.</summary>
        public const float DefaultPanelDistance = KvXrUiPanel.DefaultDistanceMeters;
        /// <summary>Масштаб мира: 1 — как в сцене.</summary>
        public const float DefaultWorldScale = 1f;
        /// <summary>Рост оператора по умолчанию, метры.</summary>
        public const float DefaultOperatorHeight = 1.70f;
        /// <summary>[D8] панель интерфейса видна всегда.</summary>
        public const int DefaultShowPanel = 1;
        /// <summary>[D8] сетка рабочей зоны по умолчанию ВЫКЛЮЧЕНА.</summary>
        public const int DefaultShowWorkZone = 0;
        /// <summary>[D8] достижимость руки по умолчанию ВЫКЛЮЧЕНА.</summary>
        public const int DefaultShowReach = 0;
        /// <summary>[D8] виртуальные стенды лаборатории в MR по умолчанию ВЫКЛЮЧЕНЫ.</summary>
        public const int DefaultShowVirtualStands = 0;
        /// <summary>Пасс-сквозь (реальная комната) по умолчанию включён для MR-сцены.</summary>
        public const int DefaultPassthrough = 0;

        // ------------------------------------------------------------------ границы

        public const float MinSmoothSpeed = 0.5f;
        public const float MaxSmoothSpeed = 3f;
        public const float MinTeleportDistance = 1f;
        public const float MaxTeleportDistance = 20f;
        public const float MinPanelDistance = 0.4f;
        public const float MaxPanelDistance = 4f;
        public const float MinWorldScale = 0.1f;
        public const float MaxWorldScale = 1f;
        public const float MinOperatorHeight = 1.33f;
        public const float MaxOperatorHeight = 2.13f;

        // ---------------------------------------------------- панель у руки ([D20], сессия §32)

        /// <summary>[D20] панель быстрого выбора у руки по умолчанию ПОКАЗАНА: это основной способ
        /// нажать команду в шлеме, когда луч контроллера занят прицеливанием.</summary>
        public const int DefaultQuickPanelVisible = 1;

        /// <summary>[D20] форма по умолчанию — ПОЛУМЕСЯЦ: компактнее и не закрывает обзор.</summary>
        public const int DefaultQuickPanelShape = (int)KvXrQuickShape.Crescent;

        /// <summary>[D20] рука по умолчанию — ЛЕВАЯ: правой оператор целится и подтверждает курком.</summary>
        public const int DefaultQuickPanelHand = (int)KvXrQuickHand.Left;

        /// <summary>Радиус панели по умолчанию, метры.</summary>
        public const float DefaultQuickPanelRadius = KvXrQuickPanelLayout.DefaultRadiusMeters;

        /// <summary>Сколько кнопок на панели по умолчанию (в диапазоне 2…12).</summary>
        public const int DefaultQuickPanelButtons = 8;

        /// <summary>Сколько всего форм панели — по этому числу строится переключатель настроек.</summary>
        public const int QuickShapeCount = 2;

        /// <summary>Сколько всего вариантов руки — по этому числу строится переключатель настроек.</summary>
        public const int QuickHandCount = 2;

        /// <summary>Показывать ли панель быстрого выбора у руки.</summary>
        public static bool GetQuickPanelVisible()
        {
            return PlayerPrefs.GetInt(KeyQuickPanelVisible, DefaultQuickPanelVisible) != 0;
        }

        /// <summary>Показать или скрыть панель у руки.</summary>
        public static void SetQuickPanelVisible(bool visible)
        {
            PlayerPrefs.SetInt(KeyQuickPanelVisible, visible ? 1 : 0);
        }

        /// <summary>Форма панели. Значение вне диапазона приводится к полумесяцу, а не падает.</summary>
        public static KvXrQuickShape GetQuickPanelShape()
        {
            return PlayerPrefs.GetInt(KeyQuickPanelShape, DefaultQuickPanelShape) == (int)KvXrQuickShape.Ring
                ? KvXrQuickShape.Ring
                : KvXrQuickShape.Crescent;
        }

        public static void SetQuickPanelShape(KvXrQuickShape shape)
        {
            PlayerPrefs.SetInt(KeyQuickPanelShape, (int)shape);
        }

        /// <summary>Индекс формы — для сегментированного переключателя настроек.</summary>
        public static int GetQuickPanelShapeIndex()
        {
            return (int)GetQuickPanelShape();
        }

        public static void SetQuickPanelShapeIndex(int index)
        {
            SetQuickPanelShape(QuickShapeFromIndex(index));
        }

        /// <summary>Индекс → форма. Чистая функция (без PlayerPrefs) — проверяется тестами.</summary>
        public static KvXrQuickShape QuickShapeFromIndex(int index)
        {
            return index == (int)KvXrQuickShape.Ring ? KvXrQuickShape.Ring : KvXrQuickShape.Crescent;
        }

        /// <summary>Название формы по индексу — то, что видит оператор (перевод на 7 языков).</summary>
        public static string QuickShapeLabel(int index)
        {
            return KvXrQuickPanelLayout.ShapeLabel(QuickShapeFromIndex(index));
        }

        /// <summary>У какой руки панель быстрого выбора.</summary>
        public static KvXrQuickHand GetQuickPanelHand()
        {
            return PlayerPrefs.GetInt(KeyQuickPanelHand, DefaultQuickPanelHand) == (int)KvXrQuickHand.Right
                ? KvXrQuickHand.Right
                : KvXrQuickHand.Left;
        }

        public static void SetQuickPanelHand(KvXrQuickHand hand)
        {
            PlayerPrefs.SetInt(KeyQuickPanelHand, (int)hand);
        }

        /// <summary>Индекс руки — для переключателя настроек.</summary>
        public static int GetQuickPanelHandIndex()
        {
            return (int)GetQuickPanelHand();
        }

        public static void SetQuickPanelHandIndex(int index)
        {
            SetQuickPanelHand(QuickHandFromIndex(index));
        }

        /// <summary>Индекс → рука. Чистая функция — проверяется тестами.</summary>
        public static KvXrQuickHand QuickHandFromIndex(int index)
        {
            return index == (int)KvXrQuickHand.Right ? KvXrQuickHand.Right : KvXrQuickHand.Left;
        }

        /// <summary>Название руки по индексу (перевод на 7 языков).</summary>
        public static string QuickHandLabel(int index)
        {
            return KvXrQuickPanelLayout.HandLabel(QuickHandFromIndex(index));
        }

        /// <summary>Радиус панели, метры. Ограничен диапазоном 0,10…0,35 м.</summary>
        public static float GetQuickPanelRadius()
        {
            return KvXrQuickPanelLayout.ClampRadius(
                PlayerPrefs.GetFloat(KeyQuickPanelRadius, DefaultQuickPanelRadius));
        }

        public static void SetQuickPanelRadius(float meters)
        {
            PlayerPrefs.SetFloat(KeyQuickPanelRadius, KvXrQuickPanelLayout.ClampRadius(meters));
        }

        /// <summary>Число кнопок на панели. Ограничено диапазоном 2…12.</summary>
        public static int GetQuickPanelButtonCount()
        {
            return KvXrQuickPanelLayout.ClampCount(
                PlayerPrefs.GetInt(KeyQuickPanelButtonCount, DefaultQuickPanelButtons));
        }

        public static void SetQuickPanelButtonCount(int count)
        {
            PlayerPrefs.SetInt(KeyQuickPanelButtonCount, KvXrQuickPanelLayout.ClampCount(count));
        }

        /// <summary>Строка состояния панели для журнала и вкладки настроек.</summary>
        public static string DescribeQuickPanel()
        {
            return KvXrQuickPanelLayout.Describe(GetQuickPanelShape(), GetQuickPanelHand(),
                GetQuickPanelButtonCount(), GetQuickPanelRadius());
        }

        // ------------------------------------------------------------------ перемещение [D2]

        /// <summary>Сколько всего способов перемещения (панель настроек строится по этому числу).</summary>
        public const int MoveModeCount = 2;

        /// <summary>Название способа перемещения по индексу — то же, что видит оператор.</summary>
        public static string MoveModeLabel(int index)
        {
            return index == (int)KvXrMoveMode.Smooth ? "Плавный ход" : "Телепорт";
        }

        /// <summary>Текущий способ перемещения. По умолчанию — телепорт ([D2]).</summary>
        public static KvXrMoveMode GetMoveMode()
        {
            int v = PlayerPrefs.GetInt(KeyMoveMode, DefaultMoveMode);
            return v == (int)KvXrMoveMode.Smooth ? KvXrMoveMode.Smooth : KvXrMoveMode.Teleport;
        }

        /// <summary>Задать способ перемещения (0 — телепорт, 1 — плавный ход).</summary>
        public static void SetMoveMode(KvXrMoveMode mode)
        {
            PlayerPrefs.SetInt(KeyMoveMode, (int)mode);
        }

        /// <summary>Индекс способа перемещения — для сегментированного переключателя настроек.</summary>
        public static int GetMoveModeIndex()
        {
            return (int)GetMoveMode();
        }

        /// <summary>Индекс → способ. Значения вне диапазона приводятся к телепорту, а не падают.</summary>
        public static void SetMoveModeIndex(int index)
        {
            SetMoveMode(index == (int)KvXrMoveMode.Smooth ? KvXrMoveMode.Smooth : KvXrMoveMode.Teleport);
        }

        /// <summary>Вернуть способ перемещения к значению по умолчанию (нужно сбросу настроек и тестам).</summary>
        public static void ResetMoveMode()
        {
            PlayerPrefs.DeleteKey(KeyMoveMode);
        }

        /// <summary>Скорость плавного хода, м/с (0,5…3).</summary>
        public static float GetSmoothSpeed()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(KeySmoothSpeed, DefaultSmoothSpeed),
                MinSmoothSpeed, MaxSmoothSpeed);
        }

        public static void SetSmoothSpeed(float metersPerSecond)
        {
            PlayerPrefs.SetFloat(KeySmoothSpeed, Mathf.Clamp(metersPerSecond, MinSmoothSpeed, MaxSmoothSpeed));
        }

        /// <summary>Дальность телепорта, метры (1…20).</summary>
        public static float GetTeleportDistance()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(KeyTeleportDistance, DefaultTeleportDistance),
                MinTeleportDistance, MaxTeleportDistance);
        }

        public static void SetTeleportDistance(float meters)
        {
            PlayerPrefs.SetFloat(KeyTeleportDistance, Mathf.Clamp(meters, MinTeleportDistance, MaxTeleportDistance));
        }

        // ------------------------------------------------------------------ панель и мир

        /// <summary>Расстояние плоской панели от глаз, метры (0,4…4).</summary>
        public static float GetPanelDistance()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(KeyPanelDistance, DefaultPanelDistance),
                MinPanelDistance, MaxPanelDistance);
        }

        public static void SetPanelDistance(float meters)
        {
            PlayerPrefs.SetFloat(KeyPanelDistance, Mathf.Clamp(meters, MinPanelDistance, MaxPanelDistance));
        }

        /// <summary>Масштаб мира (0,1…1): единица — «как в сцене».</summary>
        public static float GetWorldScale()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(KeyWorldScale, DefaultWorldScale),
                MinWorldScale, MaxWorldScale);
        }

        public static void SetWorldScale(float scale)
        {
            PlayerPrefs.SetFloat(KeyWorldScale, Mathf.Clamp(scale, MinWorldScale, MaxWorldScale));
        }

        /// <summary>Рост оператора, метры (1,33…2,13) — от него считается уровень глаз.</summary>
        public static float GetOperatorHeight()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(KeyOperatorHeight, DefaultOperatorHeight),
                MinOperatorHeight, MaxOperatorHeight);
        }

        public static void SetOperatorHeight(float meters)
        {
            PlayerPrefs.SetFloat(KeyOperatorHeight, Mathf.Clamp(meters, MinOperatorHeight, MaxOperatorHeight));
        }

        /// <summary>Готовая конфигурация рига по сохранённым настройкам.</summary>
        public static KvXrRigConfig BuildRigConfig()
        {
            KvXrRigConfig c = KvXrRigConfig.ForOperatorHeight(GetOperatorHeight());
            c.worldScale = GetWorldScale();
            c.passthrough = GetPassthrough();
            c.singlePassInstanced = true;
            c.originMode = KvXrOriginMode.Floor;
            return c;
        }

        /// <summary>Показывать реальную комнату (MR) вместо виртуальной лаборатории.</summary>
        public static bool GetPassthrough()
        {
            return PlayerPrefs.GetInt(KeyPassthrough, DefaultPassthrough) != 0;
        }

        public static void SetPassthrough(bool on)
        {
            PlayerPrefs.SetInt(KeyPassthrough, on ? 1 : 0);
        }

        // ------------------------------------------------------------------ слои MR [D8]

        /// <summary>Сколько всего слоёв виртуального в MR (панель настроек строится по этому числу).</summary>
        public const int MrLayerCount = 4;

        /// <summary>
        /// Название слоя виртуального в MR по индексу.
        /// Порядок фиксирован: 0 — панель, 1 — рабочая зона, 2 — достижимость, 3 — виртуальные стенды.
        /// </summary>
        public static string MrLayerLabel(int index)
        {
            switch (index)
            {
                case 0: return "Панель интерфейса";
                case 1: return "Рабочая зона робота";
                case 2: return "Достижимость руки";
                case 3: return "Виртуальные стенды лаборатории";
                default: return "Слой " + index;
            }
        }

        /// <summary>Панель интерфейса видна всегда ([D8]).</summary>
        public static bool GetShowPanel()
        {
            return PlayerPrefs.GetInt(KeyShowPanel, DefaultShowPanel) != 0;
        }

        public static void SetShowPanel(bool on)
        {
            PlayerPrefs.SetInt(KeyShowPanel, on ? 1 : 0);
        }

        /// <summary>Сетка рабочей зоны робота — [D8] по умолчанию выключена.</summary>
        public static bool GetShowWorkZone()
        {
            return PlayerPrefs.GetInt(KeyShowWorkZone, DefaultShowWorkZone) != 0;
        }

        public static void SetShowWorkZone(bool on)
        {
            PlayerPrefs.SetInt(KeyShowWorkZone, on ? 1 : 0);
        }

        /// <summary>Достижимость руки — [D8] по умолчанию выключена.</summary>
        public static bool GetShowReach()
        {
            return PlayerPrefs.GetInt(KeyShowReach, DefaultShowReach) != 0;
        }

        public static void SetShowReach(bool on)
        {
            PlayerPrefs.SetInt(KeyShowReach, on ? 1 : 0);
        }

        /// <summary>Виртуальные стенды лаборатории в MR — [D8] по умолчанию выключены.</summary>
        public static bool GetShowVirtualStands()
        {
            return PlayerPrefs.GetInt(KeyShowVirtualStands, DefaultShowVirtualStands) != 0;
        }

        public static void SetShowVirtualStands(bool on)
        {
            PlayerPrefs.SetInt(KeyShowVirtualStands, on ? 1 : 0);
        }

        /// <summary>Чтение слоя по индексу — единый путь для панели настроек.</summary>
        public static bool GetLayer(int index)
        {
            switch (index)
            {
                case 0: return GetShowPanel();
                case 1: return GetShowWorkZone();
                case 2: return GetShowReach();
                case 3: return GetShowVirtualStands();
                default: return false;
            }
        }

        /// <summary>Запись слоя по индексу — единый путь для панели настроек.</summary>
        public static void SetLayer(int index, bool on)
        {
            switch (index)
            {
                case 0: SetShowPanel(on); break;
                case 1: SetShowWorkZone(on); break;
                case 2: SetShowReach(on); break;
                case 3: SetShowVirtualStands(on); break;
            }
        }

        /// <summary>Строка состояния XR: режим перемещения, панель, включённые слои MR.</summary>
        public static string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("XR: перемещение — ").Append(MoveModeLabel(GetMoveModeIndex()));
            if (GetMoveMode() == KvXrMoveMode.Teleport)
                sb.Append(string.Format(" ({0:0.0} м)", GetTeleportDistance()));
            else
                sb.Append(string.Format(" ({0:0.0} м/с)", GetSmoothSpeed()));
            sb.Append(string.Format(" · панель {0:0.0} м", GetPanelDistance()));
            sb.Append(" · слои: панель ").Append(GetShowPanel() ? "вкл" : "выкл");
            sb.Append(", рабочая зона ").Append(GetShowWorkZone() ? "вкл" : "выкл");
            sb.Append(", достижимость ").Append(GetShowReach() ? "вкл" : "выкл");
            sb.Append(", стенды ").Append(GetShowVirtualStands() ? "вкл" : "выкл");
            return sb.ToString();
        }
    }
}
