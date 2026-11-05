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
        public const string KeyMoveAcceleration = "KazistovVv.XR.MoveAcceleration";
        public const string KeyWalkDeadzone = "KazistovVv.XR.WalkDeadzone";
        public const string KeyVignette = "KazistovVv.XR.ComfortVignette";
        public const string KeyVignetteStrength = "KazistovVv.XR.ComfortVignetteStrength";
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

        /// <summary>ВЛАДЕЛЕЦ 05.11.2026: по умолчанию — ПЛАВНЫЙ ХОД («как во всех играх»), телепорт остаётся переключателем.</summary>
        public const int DefaultMoveMode = (int)KvXrMoveMode.Smooth;
        /// <summary>Скорость плавного хода, м/с. Ограничена: быстрее — укачивает.</summary>
        public const float DefaultSmoothSpeed = 1.8f;
        /// <summary>Дальность телепорта, метры.</summary>
        public const float DefaultTeleportDistance = 6f;
        /// <summary>РАЗГОН хода, м/с²: без него старт/стоп «дёргает» — второй источник дискомфорта.</summary>
        public const float DefaultMoveAcceleration = 3.0f;
        /// <summary>Мёртвая зона стика ХОДА (у поворота своя — 0,15 в компоненте).</summary>
        public const float DefaultWalkDeadzone = 0.15f;
        /// <summary>Виньетка комфорта включена: затемняет края при плавном ходе (против укачивания).</summary>
        public const int DefaultVignette = 1;
        /// <summary>Сила виньетки 0…0,8 (доля затемнения краёв).</summary>
        public const float DefaultVignetteStrength = 0.35f;
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
        /// <summary>
        /// РЕЖИМ КОМНАТЫ (MR) ПО УМОЛЧАНИЮ ВЫКЛЮЧЕН — СТАРТ ВСЕГДА В VR (решение владельца
        /// 04.11.2026: «сначала прога запускается как VR, но в настройках можно сделать MR»).
        ///
        /// ПОЧЕМУ ТАК РАЦИОНАЛЬНЕЕ. MR — экспериментальная ветка: пока комната не размечена и нет
        /// данных о реальных поверхностях, робот ставится запасным путём (на пол), а сквозной вид
        /// зависит от разрешений шлема. VR-версия проверена и стабильна, поэтому запуск по
        /// умолчанию — VR, а MR включается ОСОЗНАННО в настройках шлема
        /// («Настройки → Режим комнаты (MR)»), после чего применяется при следующем запуске.
        ///
        /// ЧТО НЕ МЕНЯЕТСЯ. Тумблер по-прежнему не единственный источник правды: `MrActive`
        /// требует ещё и фактический режим по возможностям устройства
        /// (`KvXrPlatformModeSelector.Current == Mr`). На шлеме, который не умеет MR, включение
        /// тумблера ничего не сломает — приложение останется в VR.
        /// </summary>
        public const int DefaultPassthrough = 0;

        // ------------------------------------------------------------------ границы

        public const float MinSmoothSpeed = 0.5f;
        public const float MaxSmoothSpeed = 3f;
        public const float MinMoveAcceleration = 1f;
        public const float MaxMoveAcceleration = 10f;
        public const float MinWalkDeadzone = 0.05f;
        public const float MaxWalkDeadzone = 0.40f;
        public const float MinVignetteStrength = 0f;
        public const float MaxVignetteStrength = 0.80f;
        public const float MinTeleportDistance = 1f;
        public const float MaxTeleportDistance = 20f;
        public const float MinPanelDistance = 0.4f;
        public const float MaxPanelDistance = 4f;
        public const float MinWorldScale = 0.1f;
        public const float MaxWorldScale = 1f;
        public const float MinOperatorHeight = 1.33f;
        public const float MaxOperatorHeight = 2.13f;

        // ------------------------------------ ВИЗУАЛИЗАЦИИ VR ПО УМОЛЧАНИЮ (05.11.2026)

        /// <summary>Ключ «зона достижимости включена владельцем».</summary>
        public const string KeyWorkspaceVisible = "KazistovVv.XR.Workspace";

        /// <summary>Ключ «лимиты суставов включены владельцем».</summary>
        public const string KeyJointLimitsVisible = "KazistovVv.XR.JointLimits";

        /// <summary>
        /// ЗОНА ДОСТИЖИМОСТИ ПО УМОЛЧАНИЮ ВЫКЛЮЧЕНА (жалоба владельца 05.11.2026: «появились зоны
        /// достижимости сами по себе»). Флаг хранится в PlayerPrefs: включение из настроек шлема
        /// переживает перезапуск, а `KvXrFlowBridge` следит, чтобы визуализация не включилась сама.
        /// </summary>
        public const int DefaultWorkspaceVisible = 0;

        /// <summary>ЛИМИТЫ СУСТАВОВ ПО УМОЛЧАНИЮ ВЫКЛЮЧЕНЫ — по той же причине.</summary>
        public const int DefaultJointLimitsVisible = 0;

        /// <summary>Зона достижимости включена владельцем?</summary>
        public static bool GetWorkspaceVisible()
        {
            return PlayerPrefs.GetInt(KeyWorkspaceVisible, DefaultWorkspaceVisible) != 0;
        }

        /// <summary>Включить/выключить зону достижимости (запомнить).</summary>
        public static void SetWorkspaceVisible(bool visible)
        {
            PlayerPrefs.SetInt(KeyWorkspaceVisible, visible ? 1 : 0);
        }

        /// <summary>Лимиты суставов включены владельцем?</summary>
        public static bool GetJointLimitsVisible()
        {
            return PlayerPrefs.GetInt(KeyJointLimitsVisible, DefaultJointLimitsVisible) != 0;
        }

        /// <summary>Включить/выключить лимиты суставов (запомнить).</summary>
        public static void SetJointLimitsVisible(bool visible)
        {
            PlayerPrefs.SetInt(KeyJointLimitsVisible, visible ? 1 : 0);
        }

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

        /// <summary>РАЗГОН/ТОРМОЖЕНИЕ плавного хода, м/с² (1…10).</summary>
        public static float GetMoveAcceleration()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(KeyMoveAcceleration, DefaultMoveAcceleration),
                MinMoveAcceleration, MaxMoveAcceleration);
        }

        public static void SetMoveAcceleration(float metersPerSecondSquared)
        {
            PlayerPrefs.SetFloat(KeyMoveAcceleration,
                Mathf.Clamp(metersPerSecondSquared, MinMoveAcceleration, MaxMoveAcceleration));
        }

        /// <summary>Мёртвая зона стика хода, доля отклонения (0,05…0,4).</summary>
        public static float GetWalkDeadzone()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(KeyWalkDeadzone, DefaultWalkDeadzone),
                MinWalkDeadzone, MaxWalkDeadzone);
        }

        public static void SetWalkDeadzone(float fraction)
        {
            PlayerPrefs.SetFloat(KeyWalkDeadzone, Mathf.Clamp(fraction, MinWalkDeadzone, MaxWalkDeadzone));
        }

        /// <summary>Виньетка комфорта включена (по умолчанию да).</summary>
        public static bool GetVignetteEnabled()
        {
            return PlayerPrefs.GetInt(KeyVignette, DefaultVignette) != 0;
        }

        public static void SetVignetteEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(KeyVignette, enabled ? 1 : 0);
        }

        /// <summary>Сила виньетки 0…0,8 (доля затемнения краёв экрана).</summary>
        public static float GetVignetteStrength()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(KeyVignetteStrength, DefaultVignetteStrength),
                MinVignetteStrength, MaxVignetteStrength);
        }

        public static void SetVignetteStrength(float strength)
        {
            PlayerPrefs.SetFloat(KeyVignetteStrength,
                Mathf.Clamp(strength, MinVignetteStrength, MaxVignetteStrength));
        }

        /// <summary>
        /// ПЕРЕКЛЮЧИТЬ СПОСОБ ПЕРЕМЕЩЕНИЯ (двойное нажатие X в шлеме). Возвращает новый способ —
        /// вызывающий пишет его в журнал, чтобы переключение было видно, а не «что-то щёлкнуло».
        /// </summary>
        public static KvXrMoveMode ToggleMoveMode()
        {
            KvXrMoveMode next = GetMoveMode() == KvXrMoveMode.Smooth ? KvXrMoveMode.Teleport : KvXrMoveMode.Smooth;
            SetMoveMode(next);
            PlayerPrefs.Save();
            return next;
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
        {            var sb = new System.Text.StringBuilder();
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

        // ------------------------------------------------------------------ §37: рука луча, радиус панели, визуал

        /// <summary>
        /// РУКА, ИЗ КОТОРОЙ ИДЁТ ЛУЧ (задание владельца [S1.3]). До §37 луч был ЖЁСТКО правым:
        /// `KvXrUiPointer` звал `TryRightAimPose` напрямую, и сменить руку было нечем. Теперь
        /// сторона — настройка, и по умолчанию ПРАВАЯ, как владелец и просил («основная — правая»).
        /// </summary>
        public const string KeyRayHand = "KazistovVv.XR.Ray.Hand";

        /// <summary>Радиус панели групп в метрах — настройка (§37, опрос владельца).</summary>
        public const string KeyGroupPanelRadius = "KazistovVv.XR.Panel.Radius";

        /// <summary>Чем показывать контроллеры в шлеме (0 — меш SDK, 1 — примитивы, 2 — значок, 3 — выкл).</summary>
        public const string KeyControllerVisuals = "KazistovVv.XR.Visuals.Controllers";

        /// <summary>Распознавание жестов кистями включено (0/1).</summary>
        public const string KeyGestures = "KazistovVv.XR.Gestures";

        /// <summary>Второй луч из второй руки (§39/§40): 0 — выключен, 1 — включён.</summary>
        public const string KeySecondaryRay = "KazistovVv.XR.Ray.Secondary";

        /// <summary>По умолчанию второй луч включён.</summary>
        public const int DefaultSecondaryRay = 1;

        /// <summary>Показывать ли второй луч (настройка; файл подстройки `ray.secondary` её перекрывает).</summary>
        public static bool GetSecondaryRay()
        {
            return PlayerPrefs.GetInt(KeySecondaryRay, DefaultSecondaryRay) != 0;
        }

        public static void SetSecondaryRay(bool on)
        {
            PlayerPrefs.SetInt(KeySecondaryRay, on ? 1 : 0);
        }

        /// <summary>По умолчанию луч идёт из ПРАВОЙ руки ([S1.3]).</summary>
        public const int DefaultRayHand = (int)KvXrQuickHand.Right;

        /// <summary>По умолчанию модели контроллеров включены и берутся из SDK.</summary>
        public const int DefaultControllerVisuals = (int)KvXrControllerVisualMode.Mesh;

        /// <summary>По умолчанию жесты включены: это согласованный набор §37.</summary>
        public const int DefaultGestures = 1;

        /// <summary>Сколько вариантов руки — по этому числу строится переключатель настроек.</summary>
        public const int RayHandCount = 2;

        /// <summary>Индекс → рука луча. Значения вне диапазона приводятся к правой, а не падают.</summary>
        public static KvXrQuickHand RayHandFromIndex(int index)
        {
            return index == (int)KvXrQuickHand.Left ? KvXrQuickHand.Left : KvXrQuickHand.Right;
        }

        /// <summary>Название руки луча по индексу — то, что видит владелец в настройках и журнале.</summary>
        public static string RayHandLabel(int index)
        {
            return KvXrQuickPanelLayout.HandLabel(RayHandFromIndex(index));
        }

        /// <summary>Из какой руки идёт луч.</summary>
        public static KvXrQuickHand GetRayHand()
        {
            return RayHandFromIndex(PlayerPrefs.GetInt(KeyRayHand, DefaultRayHand));
        }

        public static void SetRayHand(KvXrQuickHand hand)
        {
            PlayerPrefs.SetInt(KeyRayHand, (int)hand);
        }

        public static int GetRayHandIndex()
        {
            return (int)GetRayHand();
        }

        public static void SetRayHandIndex(int index)
        {
            SetRayHand(RayHandFromIndex(index));
        }

        /// <summary>Радиус панели групп, метры. Ограничен 0.080…0.420 — так же, как в файле подстройки.</summary>
        public static float GetGroupPanelRadius()
        {
            return KvXrGroupPanelLayout.ClampPanelRadius(
                PlayerPrefs.GetFloat(KeyGroupPanelRadius, KvXrGroupPanelLayout.DefaultPanelRadiusMeters));
        }

        public static void SetGroupPanelRadius(float meters)
        {
            PlayerPrefs.SetFloat(KeyGroupPanelRadius, KvXrGroupPanelLayout.ClampPanelRadius(meters));
        }

        /// <summary>Чем показывать контроллеры. Мусор в значении приводит к мешу, а не к «ничего не видно».</summary>
        public static int GetControllerVisualMode()
        {
            return (int)KvXrControllerVisuals.ModeFromIndex(
                PlayerPrefs.GetInt(KeyControllerVisuals, DefaultControllerVisuals));
        }

        public static void SetControllerVisualMode(int modeIndex)
        {
            PlayerPrefs.SetInt(KeyControllerVisuals, (int)KvXrControllerVisuals.ModeFromIndex(modeIndex));
        }

        /// <summary>Включены ли жесты (по умолчанию — да).</summary>
        public static bool GetGesturesEnabled()
        {
            return PlayerPrefs.GetInt(KeyGestures, DefaultGestures) != 0;
        }

        public static void SetGesturesEnabled(bool on)
        {
            PlayerPrefs.SetInt(KeyGestures, on ? 1 : 0);
        }

        /// <summary>
        /// СТРОКА О ЛУЧЕ И ПАНЕЛИ для журнала: какая рука у луча, какая у панели, какой радиус.
        /// Владелец проверяет сторону ДВИЖЕНИЕМ руки в шлеме, а строкой — что код думает то же самое.
        /// </summary>
        public static string DescribePointer()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "луч из {0} руки · панель у {1} руки · радиус панели {2:0.000} м",
                GetRayHand() == KvXrQuickHand.Right ? "ПРАВОЙ" : "ЛЕВОЙ",
                GetQuickPanelHand() == KvXrQuickHand.Right ? "ПРАВОЙ" : "ЛЕВОЙ",
                GetGroupPanelRadius());
        }
    }
}
