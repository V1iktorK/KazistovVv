using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>
    /// Хранилище настроек интерфейса (PlayerPrefs): включение/отключение функций,
    /// масштаб и плотность интерфейса. Значения переживают перезапуск и доступны
    /// панели «Настройки» (вкладка «Функции») без правок кода.
    ///
    /// Это ЧИСТОЕ хранилище: оно ничего не применяет само — применение делает
    /// <see cref="KazistovVvUIManager"/> (при старте) и панель настроек (по клику),
    /// через существующие публичные методы потока/визуализаций.
    /// </summary>
    public static class KvSettings
    {
        public const string KeyWorkspace = "KazistovVv.Show.Workspace";
        public const string KeyJointLimits = "KazistovVv.Show.JointLimits";
        public const string KeyMetrics = "KazistovVv.Show.Metrics";
        public const string KeyFlashlight = "KazistovVv.Show.Flashlight";
        public const string KeyAutoRefreshTree = "KazistovVv.Show.AutoRefreshTree";
        public const string KeyPhantoms = "KazistovVv.Show.Phantoms";
        public const string KeyUiScale = "KazistovVv.UI.Scale";
        public const string KeyDensity = "KazistovVv.UI.Density";
        public const string KeyToolbarRows = "KazistovVv.UI.ToolbarRows";
        /// <summary>ФИКС 8: раскладка тулбара (0 — авто, 1 — широко, 2 — 5 в ряд).</summary>
        public const string KeyToolbarLayout = "KazistovVv.UI.ToolbarLayout";
        /// <summary>ФИКС 9: компактный тулбар (0/1).</summary>
        public const string KeyToolbarCompact = "KazistovVv.UI.ToolbarCompact";
        /// <summary>ЭТАП 11: размер шрифта интерфейса (0 — маленький, 1 — средний, 2 — большой).</summary>
        public const string KeyFontSize = "KazistovVv.UI.FontSize";
        /// <summary>ЭТАП 11: режим высокого контраста (0/1).</summary>
        public const string KeyHighContrast = "KazistovVv.UI.HighContrast";
        /// <summary>ЭТАП 11: схема для дальтоников (0 — нет, 1 — дейтеранопия, 2 — протанопия, 3 — тританопия).</summary>
        public const string KeyColorBlind = "KazistovVv.UI.ColorBlind";
        /// <summary>ЭТАП 11: навигация с клавиатуры (Tab/стрелки/Enter) включена (0/1).</summary>
        public const string KeyKeyboardNav = "KazistovVv.UI.KeyboardNav";
        /// <summary>ЭТАП 9: виртуальный геймпад (мини-индикатор) включён (0/1).</summary>
        public const string KeyGamepadHud = "KazistovVv.UI.GamepadHud";
        /// <summary>ЭТАП 8: показывать красную плашку ошибок в углу (0/1).</summary>
        public const string KeyErrorPlate = "KazistovVv.UI.ErrorPlate";
        /// <summary>Префикс ключа «группа тулбара свёрнута» (ЭТАП 1).</summary>
        public const string KeyGroupPrefix = "KazistovVv.UI.ToolbarGroup.";

        // --- значения по умолчанию (как в проекте до UI)
        public const bool DefaultWorkspace = true;
        public const bool DefaultJointLimits = true;
        public const bool DefaultMetrics = true;
        public const bool DefaultFlashlight = false;
        public const bool DefaultAutoRefreshTree = true;
        public const bool DefaultPhantoms = true;
        public const float DefaultUiScale = 1f;
        public const int DefaultDensity = 0;      // 0 — компактная, 1 — обычная
        public const int DefaultToolbarLayout = 0;   // 0 — авто (по ширине канваса)
        public const bool DefaultToolbarCompact = false;
        public const int DefaultFontSize = 1;        // 1 — средний (как было)
        public const bool DefaultHighContrast = false;
        public const int DefaultColorBlind = 0;
        public const bool DefaultKeyboardNav = true;
        public const bool DefaultGamepadHud = true;
        public const bool DefaultErrorPlate = true;

        private static bool loaded;

        /// <summary>Показывать зону достижимости.</summary>
        public static bool ShowWorkspace { get { Ensure(); return workspace; } set { workspace = value; Save(KeyWorkspace, value ? 1 : 0); } }
        /// <summary>Показывать индикаторы лимитов суставов.</summary>
        public static bool ShowJointLimits { get { Ensure(); return jointLimits; } set { jointLimits = value; Save(KeyJointLimits, value ? 1 : 0); } }
        /// <summary>Показывать панель метрик траекторий.</summary>
        public static bool ShowMetrics { get { Ensure(); return metrics; } set { metrics = value; Save(KeyMetrics, value ? 1 : 0); } }
        /// <summary>Фонарик включён.</summary>
        public static bool Flashlight { get { Ensure(); return flashlight; } set { flashlight = value; Save(KeyFlashlight, value ? 1 : 0); } }
        /// <summary>Автообновление дерева моделей.</summary>
        public static bool AutoRefreshTree { get { Ensure(); return autoTree; } set { autoTree = value; Save(KeyAutoRefreshTree, value ? 1 : 0); } }
        /// <summary>Показывать фантомы (задел: пока только флаг, см. PROJECT_CONTEXT).</summary>
        public static bool ShowPhantoms { get { Ensure(); return phantoms; } set { phantoms = value; Save(KeyPhantoms, value ? 1 : 0); } }
        /// <summary>Масштаб интерфейса (0.85 / 1 / 1.15 / 1.3).</summary>
        public static float UiScale { get { Ensure(); return uiScale; } set { uiScale = Mathf.Clamp(value, 0.6f, 1.6f); Save(KeyUiScale, uiScale); } }
        /// <summary>Плотность строк: 0 — компактная, 1 — обычная.</summary>
        public static int Density { get { Ensure(); return density; } set { density = Mathf.Clamp(value, 0, 1); Save(KeyDensity, density); } }
        /// <summary>Сколько рядов кнопок в тулбаре (3 — базовый интерфейс, 4 — с кнопками этапов 1–20).</summary>
        public static int ToolbarRows { get { Ensure(); return toolbarRows; } set { toolbarRows = Mathf.Clamp(value, 1, 6); Save(KeyToolbarRows, toolbarRows); } }
        /// <summary>
        /// ФИКС 8: раскладка тулбара — 0 «авто» (колонки считаются от ширины канваса),
        /// 1 «широко» (занять всю доступную ширину), 2 «5 в ряд» (исторический вид).
        /// </summary>
        public static int ToolbarLayout { get { Ensure(); return toolbarLayout; } set { toolbarLayout = Mathf.Clamp(value, 0, 2); Save(KeyToolbarLayout, toolbarLayout); } }
        /// <summary>ФИКС 9: компактный тулбар — мелкие иконки и скрытие редко используемых кнопок.</summary>
        public static bool ToolbarCompact { get { Ensure(); return toolbarCompact; } set { toolbarCompact = value; Save(KeyToolbarCompact, value ? 1 : 0); } }
        /// <summary>ЭТАП 11: размер шрифта (0 маленький … 2 большой).</summary>
        public static int FontSize { get { Ensure(); return fontSize; } set { fontSize = Mathf.Clamp(value, 0, 2); Save(KeyFontSize, fontSize); } }
        /// <summary>ЭТАП 11: высокий контраст.</summary>
        public static bool HighContrast { get { Ensure(); return highContrast; } set { highContrast = value; Save(KeyHighContrast, value ? 1 : 0); } }
        /// <summary>ЭТАП 11: схема для дальтоников (0 — выключена, 1…3 — тип).</summary>
        public static int ColorBlind { get { Ensure(); return colorBlind; } set { colorBlind = Mathf.Clamp(value, 0, 3); Save(KeyColorBlind, colorBlind); } }
        /// <summary>ЭТАП 11: навигация по интерфейсу с клавиатуры.</summary>
        public static bool KeyboardNav { get { Ensure(); return keyboardNav; } set { keyboardNav = value; Save(KeyKeyboardNav, value ? 1 : 0); } }
        /// <summary>ЭТАП 9: мини-индикатор геймпада на экране.</summary>
        public static bool GamepadHud { get { Ensure(); return gamepadHud; } set { gamepadHud = value; Save(KeyGamepadHud, value ? 1 : 0); } }
        /// <summary>ЭТАП 8: красная плашка ошибок в углу экрана.</summary>
        public static bool ErrorPlate { get { Ensure(); return errorPlate; } set { errorPlate = value; Save(KeyErrorPlate, value ? 1 : 0); } }

        /// <summary>Множитель размера шрифта для текущей настройки (ЭТАП 11).</summary>
        public static float FontScale
        {
            get
            {
                switch (FontSize)
                {
                    case 0: return 0.88f;
                    case 2: return 1.18f;
                    default: return 1f;
                }
            }
        }

        /// <summary>
        /// ЭТАП 1: свёрнута ли группа тулбара в одну иконку. Состояние каждой группы —
        /// отдельный ключ PlayerPrefs, поэтому оно переживает перезапуск и пересборку.
        /// </summary>
        public static bool IsToolbarGroupCollapsed(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return false;
            return PlayerPrefs.GetInt(KeyGroupPrefix + groupId, 0) != 0;
        }

        /// <summary>ЭТАП 1: свернуть/развернуть группу тулбара.</summary>
        public static void SetToolbarGroupCollapsed(string groupId, bool collapsed)
        {
            if (string.IsNullOrEmpty(groupId)) return;
            PlayerPrefs.SetInt(KeyGroupPrefix + groupId, collapsed ? 1 : 0);
        }

        private static bool workspace = DefaultWorkspace;
        private static bool jointLimits = DefaultJointLimits;
        private static bool metrics = DefaultMetrics;
        private static bool flashlight = DefaultFlashlight;
        private static bool autoTree = DefaultAutoRefreshTree;
        private static bool phantoms = DefaultPhantoms;
        private static float uiScale = DefaultUiScale;
        private static int density = DefaultDensity;
        private static int toolbarRows = 3;
        private static int toolbarLayout = DefaultToolbarLayout;
        private static bool toolbarCompact = DefaultToolbarCompact;
        private static int fontSize = DefaultFontSize;
        private static bool highContrast = DefaultHighContrast;
        private static int colorBlind = DefaultColorBlind;
        private static bool keyboardNav = DefaultKeyboardNav;
        private static bool gamepadHud = DefaultGamepadHud;
        private static bool errorPlate = DefaultErrorPlate;

        /// <summary>Высота строки дерева с учётом плотности.</summary>
        public static float RowHeight { get { return Density == 0 ? 18f : 22f; } }

        /// <summary>Сторона квадратной кнопки-иконки тулбара.</summary>
        public static float IconButtonSize { get { return Density == 0 ? 24f : 28f; } }

        /// <summary>Перечитать настройки из PlayerPrefs.</summary>
        public static void Reload()
        {
            loaded = true;
            workspace = PlayerPrefs.GetInt(KeyWorkspace, DefaultWorkspace ? 1 : 0) != 0;
            jointLimits = PlayerPrefs.GetInt(KeyJointLimits, DefaultJointLimits ? 1 : 0) != 0;
            metrics = PlayerPrefs.GetInt(KeyMetrics, DefaultMetrics ? 1 : 0) != 0;
            flashlight = PlayerPrefs.GetInt(KeyFlashlight, DefaultFlashlight ? 1 : 0) != 0;
            autoTree = PlayerPrefs.GetInt(KeyAutoRefreshTree, DefaultAutoRefreshTree ? 1 : 0) != 0;
            phantoms = PlayerPrefs.GetInt(KeyPhantoms, DefaultPhantoms ? 1 : 0) != 0;
            uiScale = PlayerPrefs.GetFloat(KeyUiScale, DefaultUiScale);
            density = PlayerPrefs.GetInt(KeyDensity, DefaultDensity);
            toolbarRows = PlayerPrefs.GetInt(KeyToolbarRows, 3);
            toolbarLayout = PlayerPrefs.GetInt(KeyToolbarLayout, DefaultToolbarLayout);
            toolbarCompact = PlayerPrefs.GetInt(KeyToolbarCompact, DefaultToolbarCompact ? 1 : 0) != 0;
            fontSize = PlayerPrefs.GetInt(KeyFontSize, DefaultFontSize);
            highContrast = PlayerPrefs.GetInt(KeyHighContrast, DefaultHighContrast ? 1 : 0) != 0;
            colorBlind = PlayerPrefs.GetInt(KeyColorBlind, DefaultColorBlind);
            keyboardNav = PlayerPrefs.GetInt(KeyKeyboardNav, DefaultKeyboardNav ? 1 : 0) != 0;
            gamepadHud = PlayerPrefs.GetInt(KeyGamepadHud, DefaultGamepadHud ? 1 : 0) != 0;
            errorPlate = PlayerPrefs.GetInt(KeyErrorPlate, DefaultErrorPlate ? 1 : 0) != 0;
        }

        /// <summary>Сбросить настройки интерфейса к значениям по умолчанию.</summary>
        public static void ResetToDefaults()
        {
            workspace = DefaultWorkspace;
            jointLimits = DefaultJointLimits;
            metrics = DefaultMetrics;
            flashlight = DefaultFlashlight;
            autoTree = DefaultAutoRefreshTree;
            phantoms = DefaultPhantoms;
            uiScale = DefaultUiScale;
            density = DefaultDensity;
            toolbarRows = 3;
            toolbarLayout = DefaultToolbarLayout;
            toolbarCompact = DefaultToolbarCompact;
            fontSize = DefaultFontSize;
            highContrast = DefaultHighContrast;
            colorBlind = DefaultColorBlind;
            keyboardNav = DefaultKeyboardNav;
            gamepadHud = DefaultGamepadHud;
            errorPlate = DefaultErrorPlate;
            Save(KeyWorkspace, workspace ? 1 : 0);
            Save(KeyJointLimits, jointLimits ? 1 : 0);
            Save(KeyMetrics, metrics ? 1 : 0);
            Save(KeyFlashlight, flashlight ? 1 : 0);
            Save(KeyAutoRefreshTree, autoTree ? 1 : 0);
            Save(KeyPhantoms, phantoms ? 1 : 0);
            Save(KeyUiScale, uiScale);
            Save(KeyDensity, density);
            Save(KeyToolbarRows, toolbarRows);
            Save(KeyToolbarLayout, toolbarLayout);
            Save(KeyToolbarCompact, toolbarCompact ? 1 : 0);
            Save(KeyFontSize, fontSize);
            Save(KeyHighContrast, highContrast ? 1 : 0);
            Save(KeyColorBlind, colorBlind);
            Save(KeyKeyboardNav, keyboardNav ? 1 : 0);
            Save(KeyGamepadHud, gamepadHud ? 1 : 0);
            Save(KeyErrorPlate, errorPlate ? 1 : 0);
        }

        private static void Ensure()
        {
            if (!loaded) Reload();
        }

        // ============================================================================================
        // СОХРАНЕНИЕ НА ДИСК (P1-1, 11.11.2026).
        //
        // ЧТО БЫЛО. Оба хелпера делали только `PlayerPrefs.Set*` — БЕЗ `PlayerPrefs.Save()`. Unity
        // пишет `PlayerPrefs` на диск не сразу, а при штатном выходе; при падении или
        // принудительной остановке приложения настройки (масштаб интерфейса, плотность, кегль,
        // контраст, схема для дальтоников, клавиатурная навигация, HUD геймпада) терялись — при
        // том что doc-комментарий класса обещает, что они «переживают перезапуск».
        //
        // ЧЕМ ЭТО ГРОЗИЛО. Наладчик настраивал читаемость интерфейса, закрывал приложение, а на
        // следующем запуске всё возвращалось к заводским — «настройки не работают».
        //
        // Теперь каждая запись завершается `PlayerPrefs.Save()`.
        // ============================================================================================
        private static void Save(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        private static void Save(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }
    }
}
