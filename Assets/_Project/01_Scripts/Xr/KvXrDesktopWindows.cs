using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// СООТВЕТСТВИЕ «ID КОМАНДЫ → ЭКРАН ШЛЕМА» (§42, пункт 1 задания; решение владельца 25.10.2026 —
    /// вариант Б: у каждого окна компьютера СВОЙ экран в шлеме, построенный нативно).
    ///
    /// 50 команд-окон закрываются одиннадцатью экранами: пять новых (11–15) и шесть уже принятых.
    /// Таблица — ДАННЫЕ, а не пятьдесят веток `switch`: одна правка, один тест на полноту, и новая
    /// команда не может молча открыть «Справку» (как было бы с `default`).
    /// </summary>
    public static class KvXrDesktopWindows
    {
        private static KvXrDesktopWindowInfo[] table;

        /// <summary>Сколько команд компьютера открывают окно.</summary>
        public static int Count { get { return Table.Length; } }

        /// <summary>Таблица целиком (строится один раз).</summary>
        private static KvXrDesktopWindowInfo[] Table
        {
            get
            {
                if (table == null) table = Build();
                return table;
            }
        }

        private static KvXrDesktopWindowInfo Row(string id, KvXrPanelLevel screen, int tab, string title)
        {
            var info = new KvXrDesktopWindowInfo();
            info.CommandId = id;
            info.Screen = screen;
            info.Tab = tab;
            info.Title = title;
            return info;
        }

        private static KvXrDesktopWindowInfo[] Build()
        {
            return new[]
            {
                // ---------------------------------------------- ДОКИ (4)
                Row("view.panel.tree", KvXrPanelLevel.ObjectsScreen, 0, "Дерево моделей"),
                Row("view.panel.properties", KvXrPanelLevel.PropertiesScreen, 0, "Свойства"),
                Row("view.panel.settings", KvXrPanelLevel.DesktopSettingsScreen, 0, "Настройки ПК"),
                Row("help.hotkeys", KvXrPanelLevel.HotkeysScreen, 0, "Горячие клавиши"),

                // ---------------------------------------------- ОКНО НАСТРОЕК ПК (5)
                // Номера вкладок — ФАКТИЧЕСКИЕ у компьютера (дефект Д4): help.about открывает
                // «Графику» (3), help.open — «О программе» (4).
                Row("ui.settings", KvXrPanelLevel.DesktopSettingsScreen, 0, "Настройки ПК · Функции"),
                Row("ui.access", KvXrPanelLevel.DesktopSettingsScreen, 2, "Настройки ПК · Интерфейс"),
                Row("help.about", KvXrPanelLevel.DesktopSettingsScreen, 3, "Настройки ПК · Графика"),
                Row("help.open", KvXrPanelLevel.DesktopSettingsScreen, 4, "Настройки ПК · О программе"),
                Row("graphics.tab", KvXrPanelLevel.DesktopSettingsScreen, 3, "Настройки ПК · Графика"),

                // ---------------------------------------------- ПАЛИТРА (1)
                Row("ui.palette", KvXrPanelLevel.SearchScreen, 0, "Палитра команд"),

                // ---------------------------------------------- ПАНЕЛЬ ФУНКЦИЙ (11)
                Row("features.open", KvXrPanelLevel.FeaturesScreen, 0, "Панель функций · Запись"),
                Row("pose.save", KvXrPanelLevel.FeaturesScreen, 1, "Панель функций · Позы"),
                Row("pose.goto", KvXrPanelLevel.FeaturesScreen, 1, "Панель функций · Позы"),
                Row("joints.panel", KvXrPanelLevel.FeaturesScreen, 2, "Панель функций · Суставы"),
                Row("zone.panel", KvXrPanelLevel.FeaturesScreen, 3, "Панель функций · Зоны"),
                Row("compare.open", KvXrPanelLevel.FeaturesScreen, 4, "Панель функций · Сравнение"),
                Row("graph.open", KvXrPanelLevel.FeaturesScreen, 5, "Панель функций · Графики"),
                Row("perf.open", KvXrPanelLevel.FeaturesScreen, 6, "Панель функций · Замер"),
                Row("log.open", KvXrPanelLevel.FeaturesScreen, 7, "Панель функций · Журнал"),
                Row("scenarios.open", KvXrPanelLevel.FeaturesScreen, 8, "Панель функций · Сценарии"),
                Row("session.panel", KvXrPanelLevel.FeaturesScreen, 9, "Панель функций · Сессии"),

                // ---------------------------------------------- ВЕРСТАК (25)
                Row("workbench.toggle", KvXrPanelLevel.WorkbenchScreen, 0, "Верстак · Сглаживание"),
                Row("post.smooth", KvXrPanelLevel.WorkbenchScreen, 0, "Верстак · Сглаживание"),
                Row("post.timeoptimal", KvXrPanelLevel.WorkbenchScreen, 1, "Верстак · Время"),
                Row("post.energy", KvXrPanelLevel.WorkbenchScreen, 2, "Верстак · Энергия"),
                Row("wp.tab", KvXrPanelLevel.WorkbenchScreen, 3, "Верстак · Точки"),
                Row("constr.tab", KvXrPanelLevel.WorkbenchScreen, 4, "Верстак · Ограничения"),
                Row("calib.tab", KvXrPanelLevel.WorkbenchScreen, 5, "Верстак · Калибровка"),
                Row("payload.tab", KvXrPanelLevel.WorkbenchScreen, 6, "Верстак · Груз"),
                Row("export.tab", KvXrPanelLevel.WorkbenchScreen, 7, "Верстак · Экспорт"),
                Row("import.tab", KvXrPanelLevel.WorkbenchScreen, 8, "Верстак · Импорт"),
                Row("collision.tab", KvXrPanelLevel.WorkbenchScreen, 9, "Верстак · Коллизии"),
                Row("lab.tab", KvXrPanelLevel.WorkbenchScreen, 10, "Верстак · Планировщик"),
                Row("cameras.tab", KvXrPanelLevel.WorkbenchScreen, 11, "Верстак · Камеры"),
                Row("forces.tab", KvXrPanelLevel.WorkbenchScreen, 12, "Верстак · Силы"),
                Row("heat.time", KvXrPanelLevel.WorkbenchScreen, 12, "Верстак · Силы"),
                Row("report.pdf", KvXrPanelLevel.WorkbenchScreen, 13, "Верстак · Отчёт"),
                Row("net.tab", KvXrPanelLevel.WorkbenchScreen, 14, "Верстак · Сеть"),
                Row("script.tab", KvXrPanelLevel.WorkbenchScreen, 15, "Верстак · Макросы"),
                Row("bt.tab", KvXrPanelLevel.WorkbenchScreen, 16, "Верстак · Поведение"),
                Row("mat.tab", KvXrPanelLevel.WorkbenchScreen, 17, "Верстак · Материалы"),
                Row("titles.tab", KvXrPanelLevel.WorkbenchScreen, 18, "Верстак · Титры"),
                Row("vo.tab", KvXrPanelLevel.WorkbenchScreen, 19, "Верстак · Озвучка"),
                Row("fail.tab", KvXrPanelLevel.WorkbenchScreen, 20, "Верстак · Отказы"),
                Row("valid.tab", KvXrPanelLevel.WorkbenchScreen, 21, "Верстак · Проверка"),
                Row("logtools.tab", KvXrPanelLevel.WorkbenchScreen, 22, "Верстак · Журнал"),

                // ---------------------------------------------- ПРОЧИЕ ОКНА (4)
                Row("startmenu.show", KvXrPanelLevel.StartMenu, 0, "Стартовое меню"),
                Row("tut.toggle", KvXrPanelLevel.HelpScreen, 0, "Обучение"),
                Row("health.toggle", KvXrPanelLevel.StatusScreen, 0, "Состояние"),
                Row("pendant.toggle", KvXrPanelLevel.PendantScreen, 0, "Виртуальный пульт")
            };
        }

        /// <summary>ЭКРАН ДЛЯ КОМАНДЫ. `false` — команда окна не открывает (её надо выполнить).</summary>
        public static bool TryGet(string commandId, out KvXrDesktopWindowInfo info)
        {
            info = new KvXrDesktopWindowInfo();
            if (string.IsNullOrEmpty(commandId)) return false;

            KvXrDesktopWindowInfo[] rows = Table;
            for (int i = 0; i < rows.Length; i++)
            {
                if (!string.Equals(rows[i].CommandId, commandId, System.StringComparison.Ordinal)) continue;
                info = rows[i];
                return true;
            }
            return false;
        }

        /// <summary>Открывает ли команда окно.</summary>
        public static bool IsWindowCommand(string commandId)
        {
            KvXrDesktopWindowInfo info;
            return TryGet(commandId, out info);
        }

        /// <summary>Все id команд-окон.</summary>
        public static List<string> AllIds()
        {
            var list = new List<string>(Table.Length);
            for (int i = 0; i < Table.Length; i++) list.Add(Table[i].CommandId);
            return list;
        }

        /// <summary>Сколько РАЗНЫХ экранов обслуживают эти команды (для журнала и теста).</summary>
        public static int ScreenCount()
        {
            var seen = new List<KvXrPanelLevel>();
            for (int i = 0; i < Table.Length; i++)
                if (!seen.Contains(Table[i].Screen)) seen.Add(Table[i].Screen);
            return seen.Count;
        }

        /// <summary>Строка журнала о таблице.</summary>
        public static string Describe()
        {
            return "[KvXr] ОКНА: команд-окон " + Count + " · экранов " + ScreenCount() +
                   " · новых экранов 5 (Настройки ПК, Панель функций, Верстак, Горячие клавиши, Пульт)";
        }
    }
}
