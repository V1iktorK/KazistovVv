using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ВКЛАДКИ ЭКРАНОВ-ОКОН (§42). Ряд вкладок внутри окна — четыре слота и две кнопки листания:
    /// у «Верстака» 23 вкладки, и все они обязаны быть достижимы, не вылезая за панель.
    /// Короткие подписи — для кнопок; полное название вкладки компьютера печатается в строке.
    /// </summary>
    public static class KvXrDesktopTabs
    {
        /// <summary>Сколько вкладок видно одновременно (остальные — листанием).</summary>
        public const int VisibleSlots = 4;

        /// <summary>Шаг между вкладками по X, метры (4 × 0,13 + листание = 0,58 м ≤ 0,62 м окна).</summary>
        public const float StepMeters = 0.13f;

        private static readonly string[] SettingsTitles =
            { "Функции", "Управление", "Интерфейс", "Графика", "О программе", "Справка" };

        private static readonly string[] FeatureTitles =
            { "Запись", "Позы", "Суставы", "Зоны", "Сравнение", "Графики", "Замер", "Журнал", "Сценарии", "Сессии" };

        private static readonly string[] WorkbenchTitles =
        {
            "Сглаживание", "Время", "Энергия", "Точки", "Ограничения", "Калибровка", "Груз", "Экспорт",
            "Импорт", "Коллизии", "Планировщик", "Камеры", "Силы", "Отчёт", "Сеть", "Макросы",
            "Поведение", "Материалы", "Титры", "Озвучка", "Отказы", "Проверка", "Журнал"
        };

        private static readonly string[] WorkbenchKeys =
        {
            "smooth", "topt", "energy", "waypoints", "constraints", "calibration", "payload", "export",
            "import", "collision", "planner", "cameras", "forces", "report", "net", "script",
            "behavior", "material", "titles", "voiceover", "failures", "validate", "logtools"
        };

        private static string[] TitlesOf(KvXrPanelLevel screen)
        {
            switch (screen)
            {
                case KvXrPanelLevel.DesktopSettingsScreen: return SettingsTitles;
                case KvXrPanelLevel.FeaturesScreen: return FeatureTitles;
                case KvXrPanelLevel.WorkbenchScreen: return WorkbenchTitles;
                case KvXrPanelLevel.HotkeysScreen: return new[] { "Клавиши" };
                case KvXrPanelLevel.PendantScreen: return new[] { "Пульт" };
                default: return new string[0];
            }
        }

        /// <summary>Сколько вкладок у экрана (0 — экран без вкладок).</summary>
        public static int CountOf(KvXrPanelLevel screen)
        {
            return TitlesOf(screen).Length;
        }

        /// <summary>Короткое имя вкладки для кнопки.</summary>
        public static string TabTitle(KvXrPanelLevel screen, int index)
        {
            string[] titles = TitlesOf(screen);
            if (titles.Length == 0) return KvXrGroupPanel.ScreenTitle(screen);
            return titles[Mathf.Clamp(index, 0, titles.Length - 1)];
        }

        /// <summary>Ключи вкладок панели функций — как у компьютера (по ним ищутся кнопки вкладки).</summary>
        private static readonly string[] FeatureKeys =
        {
            "record", "poses", "joints", "zones", "compare", "graphs", "perf", "log", "scenarios", "sessions"
        };

        /// <summary>Ключи вкладок настроек ПК — как у компьютера.</summary>
        private static readonly string[] SettingsKeys =
        {
            "features", "controls", "interface", "graphics", "about", "help"
        };

        /// <summary>Ключ вкладки в терминах компьютера (для строк и команд). ВСЕГДА латиницей.</summary>
        public static string KeyOf(KvXrPanelLevel screen, int index)
        {
            if (screen == KvXrPanelLevel.WorkbenchScreen)
                return WorkbenchKeys[Mathf.Clamp(index, 0, WorkbenchKeys.Length - 1)];
            if (screen == KvXrPanelLevel.FeaturesScreen)
                return FeatureKeys[Mathf.Clamp(index, 0, FeatureKeys.Length - 1)];
            if (screen == KvXrPanelLevel.DesktopSettingsScreen)
                return SettingsKeys[Mathf.Clamp(index, 0, SettingsKeys.Length - 1)];
            return TabTitle(screen, index);
        }

        /// <summary>Полный заголовок окна: «Верстак · Точки».</summary>
        public static string TitleOf(KvXrPanelLevel screen, int index)
        {
            if (CountOf(screen) == 0) return KvXrGroupPanel.ScreenTitle(screen);
            return KvXrGroupPanel.ScreenTitle(screen) + " · " + TabTitle(screen, index);
        }

        /// <summary>Сколько страниц у ряда вкладок.</summary>
        public static int PageCount(int tabs)
        {
            if (tabs <= 0) return 1;
            return (tabs + VisibleSlots - 1) / VisibleSlots;
        }

        /// <summary>Номер страницы, на которой лежит вкладка `tab`.</summary>
        public static int PageOf(int tab)
        {
            if (tab <= 0) return 0;
            return tab / VisibleSlots;
        }

        /// <summary>Первая вкладка страницы.</summary>
        public static int FirstOnPage(int page)
        {
            return Mathf.Max(0, page) * VisibleSlots;
        }

        /// <summary>Влезает ли ряд вкладок с листанием в ширину окна.</summary>
        public static bool Fits(KvXrPanelLevel screen, float halfWidthMeters)
        {
            float row = VisibleSlots * StepMeters + 0.06f;      // 4 вкладки + две кнопки листания
            return row <= 2f * Mathf.Max(0.0001f, halfWidthMeters) + 0.0001f;
        }
    }
}
