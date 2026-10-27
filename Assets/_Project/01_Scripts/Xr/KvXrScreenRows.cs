using System.Collections.Generic;

namespace KazistovVvXr
{
    /// <summary>
    /// СТРОКИ ЭКРАНОВ-ОКОН (§42, вариант Б). Чистые функции: что именно попадёт в строки, решает этот
    /// файл, а рисует панель — так строки проверяются тестом без шлема.
    /// ПРАВИЛО: пустого списка НЕ БЫВАЕТ. Если данных нет — первая строка честно называет причину,
    /// иначе владелец видит пустое окно и не отличает дефект от замысла.
    /// </summary>
    public static class KvXrScreenRows
    {
        /// <summary>Честная строка «данных нет» с причиной.</summary>
        public static string NoData(string reason)
        {
            if (string.IsNullOrEmpty(reason)) reason = "данных нет";
            return "данных нет: " + reason;
        }

        /// <summary>Строки вкладки `tab` экрана `screen` по данным `data` (никогда не пусто).</summary>
        public static List<string> Build(KvXrPanelLevel screen, int tab, KvXrScreenData data)
        {
            string key = KvXrDesktopTabs.KeyOf(screen, tab);
            var rows = new List<string>();
            rows.Add(KvXrDesktopTabs.TitleOf(screen, tab));

            switch (screen)
            {
                case KvXrPanelLevel.DesktopSettingsScreen: BuildSettings(key, data, rows); break;
                case KvXrPanelLevel.FeaturesScreen: BuildFeatures(key, data, rows); break;
                case KvXrPanelLevel.WorkbenchScreen: BuildWorkbench(key, data, rows); break;
                case KvXrPanelLevel.HotkeysScreen: BuildHotkeys(data, rows); break;
                case KvXrPanelLevel.PendantScreen: BuildPendant(data, rows); break;
                default: rows.Add(NoData("у этого экрана нет вкладок компьютера")); break;
            }

            return rows;
        }

        private static string F(string format, params object[] args)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
        }

        private static void BuildSettings(string key, KvXrScreenData d, List<string> rows)
        {
            rows.Add("Версия: " + (string.IsNullOrEmpty(d.Version) ? "—" : d.Version));
            rows.Add("Язык: " + (string.IsNullOrEmpty(d.Language) ? "—" : d.Language));
            rows.Add(F("Масштаб интерфейса: {0:0.00}", d.UiScale));

            if (key == "Графика")
            {
                rows.Add("Пресет: " + (string.IsNullOrEmpty(d.GraphicsPreset) ? "—" : d.GraphicsPreset));
                rows.Add(F("Кадров/с: {0:0.0} · кадр {1:0.0} мс", d.Fps, d.FrameMs));
            }
            if (key == "Интерфейс")
                rows.Add("Числовые поля — кнопкой «Ввести значение» на экране «Интерфейс»");
        }

        private static void BuildFeatures(string key, KvXrScreenData d, List<string> rows)
        {
            if (key == "Запись")
            {
                rows.Add("Записей: " + Count(d.Records));
                rows.Add(d.Records != null && d.Records.Length > 0 ? "Последняя: " + d.Records[0]
                                                                  : NoData("записей ещё нет"));
                return;
            }
            if (key == "Позы")
            {
                rows.Add("Сохранённых поз: " + Count(d.Poses));
                rows.Add(d.Poses != null && d.Poses.Length > 0 ? "Последняя: " + d.Poses[0]
                                                              : NoData("поз ещё нет"));
                return;
            }
            if (key == "Суставы")
            {
                if (!d.JointsReady || d.JointAngles == null || d.JointAngles.Length == 0)
                {
                    rows.Add(NoData("робот не выбран лучом"));
                    return;
                }
                rows.Add("Робот: " + (string.IsNullOrEmpty(d.RobotName) ? "—" : d.RobotName));
                for (int i = 0; i < d.JointAngles.Length; i++)
                    rows.Add(F("Ось {0}: {1:0.0}°", i + 1, d.JointAngles[i]));
                return;
            }
            if (key == "Зоны")
            {
                rows.Add("Запретных зон: " + Count(d.Zones));
                rows.Add(Count(d.Zones) > 0 ? "Первая: " + d.Zones[0] : NoData("зон ещё нет"));
                return;
            }
            if (key == "Журнал")
            {
                rows.Add("Записей в журнале: " + d.LogTotal);
                if (d.LogLines == null || d.LogLines.Length == 0) { rows.Add(NoData("журнал пуст")); return; }
                for (int i = 0; i < d.LogLines.Length; i++) rows.Add(d.LogLines[i]);
                return;
            }
            if (key == "Сценарии")
            {
                rows.Add("Сценариев: " + Count(d.Scenarios));
                rows.Add(Count(d.Scenarios) > 0 ? "Первый: " + d.Scenarios[0] : NoData("сценариев нет"));
                return;
            }
            if (key == "Сессии")
            {
                rows.Add("Сессий: " + Count(d.Sessions));
                rows.Add(Count(d.Sessions) > 0 ? "Последняя: " + d.Sessions[0] : NoData("сессий нет"));
                return;
            }
            if (key == "Замер")
            {
                rows.Add(F("Кадров/с: {0:0.0} · кадр {1:0.0} мс", d.Fps, d.FrameMs));
                rows.Add("Кадров всего: " + d.FrameCount);
                rows.Add(F("Время работы: {0:0} с", d.UptimeSeconds));
                return;
            }

            rows.Add(NoData("вкладка «" + key + "» в шлеме пока показывает только заголовок"));
        }

        private static void BuildWorkbench(string key, KvXrScreenData d, List<string> rows)
        {
            switch (key)
            {
                case "waypoints":
                    rows.Add("Точек: " + d.Waypoints);
                    break;
                case "constraints":
                    rows.Add("Ограничений: " + d.Constraints);
                    break;
                case "calibration":
                    rows.Add(F("Опорная плоскость: {0:0.00} м", d.ReferencePlaneHeight));
                    break;
                case "payload":
                    rows.Add(F("Груз: {0:0.0} кг", d.PayloadKg));
                    break;
                case "planner":
                    rows.Add("Узлов RRT: " + d.RrtNodes);
                    break;
                case "collision":
                    rows.Add("Коллизионные меши: " + (d.CollisionProxies ? "включены" : "выключены"));
                    break;
                case "forces":
                    rows.Add("Векторы сил: " + (d.ForcesVisible ? "включены" : "выключены"));
                    rows.Add("Тепловая карта времени: " + (d.HeatmapVisible ? "включена" : "выключена"));
                    break;
                default:
                    rows.Add(NoData("вкладка «" + key + "» в шлеме пока показывает только заголовок"));
                    break;
            }
            rows.Add("Кнопки вкладки — те же команды, что на компьютере");
        }

        private static void BuildHotkeys(KvXrScreenData d, List<string> rows)
        {
            int perPage = d.BindingsPerPage > 0 ? d.BindingsPerPage : 8;
            if (d.Bindings == null || d.Bindings.Length == 0) { rows.Add(NoData("реестр клавиш пуст")); return; }

            int total = d.Bindings.Length;
            rows.Add("Клавиш в реестре: " + total);
            int first = d.BindingsPage * perPage;
            for (int i = first; i < first + perPage && i < total; i++) rows.Add(d.Bindings[i]);
        }

        private static void BuildPendant(KvXrScreenData d, List<string> rows)
        {
            rows.Add("Пульт: " + (d.PendantVisible ? "показан" : "скрыт"));
            rows.Add("Режим: " + (string.IsNullOrEmpty(d.PendantMode) ? "—" : d.PendantMode));
            rows.Add("Робот: " + (string.IsNullOrEmpty(d.RobotName) ? "не выбран" : d.RobotName));
            rows.Add(F("Шаг перемещения: {0:0.0} мм", d.JogStepMm));
        }

        private static int Count(string[] items)
        {
            return items != null ? items.Length : 0;
        }
    }
}
