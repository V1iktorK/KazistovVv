using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVv.Tests
{
    /// <summary>
    /// СТОРОЖ ПОЛНОТЫ МЕНЮ ШЛЕМА (часть 1 задачи 02.11.2026).
    ///
    /// ЗАЧЕМ. Владелец: «добавить весь функционал к кнопкам на меню левой руки». До этой правки
    /// меню шлема знало 63 команды из 110, а в группах лежало 15 идентификаторов, которых в
    /// реестре НЕТ ВООБЩЕ (мёртвые ссылки). Такое расхождение копится незаметно: добавили
    /// команду на компьютер — в шлеме её нет, и никто не замечает, пока владелец не спросит.
    ///
    /// ПОЧЕМУ СТАТИЧЕСКИЙ РАЗБОР, А НЕ `KvCommands.All`. Команды регистрируются в рантайме
    /// UI-менеджером (`FeatureHub.RegisterCommands` и т.п.), а EditMode-тест выполняется БЕЗ
    /// запуска сцены — список команд в нём был бы пуст, и тест «проходил» бы ничего не проверив.
    /// Поэтому читаем ИСХОДНИКИ: файлы с `KvCommands.Register(new KvCommand {...})` дают полный
    /// перечень того, что существует, а `KvToolbarGroups.Build()` — что разложено в меню.
    ///
    /// ЕСЛИ ЭТОТ ТЕСТ УПАЛ — значит добавили команду и забыли положить её в группу. Лечится
    /// одной строкой в `KvToolbarGroups`.
    /// </summary>
    public class KvXrMenuCoverageTests
    {
        private static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        private static string ScriptsRoot
        {
            get { return Path.Combine(ProjectRoot, "Assets", "_Project"); }
        }

        /// <summary>
        /// Все id команд, зарегистрированных где-либо в проекте (из исходников).
        /// </summary>
        private static HashSet<string> RegisteredCommandIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (!Directory.Exists(ScriptsRoot)) return ids;

            foreach (string file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch { continue; }
                if (text.IndexOf("KvCommands.Register", StringComparison.Ordinal) < 0) continue;

                // Блок регистрации: KvCommands.Register(new KvCommand { ... });
                foreach (Match m in Regex.Matches(text,
                             @"KvCommands\.Register\s*\(\s*new\s+KvCommand\s*\{(.*?)\}\s*\)\s*;",
                             RegexOptions.Singleline))
                {
                    Match id = Regex.Match(m.Groups[1].Value, @"\bId\s*=\s*""([^""]+)""");
                    if (id.Success && id.Groups[1].Value.Length > 0)
                        ids.Add(id.Groups[1].Value);
                }

                // Краткая перегрузка: KvCommands.Register("id", "title", ...).
                // ВАЖНО: строки-комментарии пропускаем. Иначе пример в комментарии
                // («KvCommands.Register("id", "title", ...)») попадал в реестр как команда «id»,
                // и тест требовал для неё место в меню.
                foreach (Match m in Regex.Matches(text, @"KvCommands\.Register\s*\(\s*""([^""]+)"""))
                {
                    int lineStart = text.LastIndexOf('\n', m.Index) + 1;
                    string linePrefix = text.Substring(lineStart, m.Index - lineStart);
                    if (linePrefix.Contains("//") || linePrefix.Contains("*")) continue;

                    string id = m.Groups[1].Value;
                    if (id.Length > 0) ids.Add(id);
                }
            }
            return ids;
        }

        /// <summary>
        /// Все id, разложенные по группам меню шлема (из `KvToolbarGroup(...)`).
        /// </summary>
        private static HashSet<string> MenuCommandIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            string file = Path.Combine(ScriptsRoot, "06_KazistovVv_UI", "03_Scripts", "Zones",
                "KvToolbarGroups.cs");
            if (!File.Exists(file))
            {
                // Запасной поиск: файл могли переложить.
                string[] found = Directory.GetFiles(ScriptsRoot, "KvToolbarGroups.cs",
                    SearchOption.AllDirectories);
                if (found.Length == 0) return ids;
                file = found[0];
            }

            string text = File.ReadAllText(file);
            int buildStart = text.IndexOf("private static KvToolbarGroup[] Build()",
                StringComparison.Ordinal);
            if (buildStart < 0) return ids;
            string build = text.Substring(buildStart);

            foreach (Match m in Regex.Matches(build,
                         @"new\s+KvToolbarGroup\s*\(\s*""[^""]+""\s*,\s*""[^""]+""\s*,\s*""[^""]+""\s*,(.*?)\)\s*,",
                         RegexOptions.Singleline))
            {
                // Id команд — любые строки в остатке конструктора. Раньше шаблон требовал точку
                // («robot.switch»), и простые id вроде «estop» выпадали из проверки, из-за чего
                // тест ложно считал их отсутствующими в меню.
                foreach (Match id in Regex.Matches(m.Groups[1].Value,
                             @"""([a-z][a-z0-9]*(?:\.[a-z0-9]+)*)"""))
                {
                    ids.Add(id.Groups[1].Value);
                }
            }
            return ids;
        }

        /// <summary>
        /// КАЖДАЯ КОМАНДА РЕЕСТРА ОБЯЗАНА БЫТЬ В МЕНЮ ШЛЕМА.
        ///
        /// Это и есть смысл части 1: «весь функционал к кнопкам меню левой руки».
        /// </summary>
        [Test]
        public void EveryRegisteredCommand_HasMenuGroup()
        {
            HashSet<string> registered = RegisteredCommandIds();
            HashSet<string> menu = MenuCommandIds();

            Assert.Greater(registered.Count, 50,
                "Найдено подозрительно мало команд (" + registered.Count + ") — вероятно, сломался " +
                "разбор исходников, а не проект. Проверьте путь Assets/_Project и шаблон Register.");

            var missing = new List<string>();
            foreach (string id in registered)
                if (!menu.Contains(id)) missing.Add(id);
            missing.Sort(StringComparer.Ordinal);

            Assert.AreEqual(0, missing.Count,
                "Эти команды ЕСТЬ в реестре, но НЕДОСТУПНЫ из меню шлема (" + missing.Count + " шт): " +
                string.Join(", ", missing.ToArray()) +
                ". Добавьте их в KvToolbarGroups.Build() в подходящую группу.");
        }

        /// <summary>
        /// В ГРУППАХ НЕ ДОЛЖНО БЫТЬ МЁРТВЫХ ССЫЛОК.
        ///
        /// До правки 02.11.2026 в меню лежало 15 id, которых в реестре нет: `valid.run`,
        /// `forces.tab`, `lab.tree`, `lab.run`, `collision.toggle`, `collision.tab`,
        /// `report.make`, `report.pdf`, `net.tab`, `web.toggle`, `mobile.toggle`, `script.tab`,
        /// `bt.tab`, `cameras.tab`, `show.trajectories`, `fail.tab`. Такая кнопка выглядит
        /// рабочей, но ничего не делает — и это неотличимо от «сломалось».
        /// </summary>
        [Test]
        public void Menu_HasNoDeadCommandIds()
        {
            HashSet<string> registered = RegisteredCommandIds();
            HashSet<string> menu = MenuCommandIds();

            var dead = new List<string>();
            foreach (string id in menu)
                if (!registered.Contains(id)) dead.Add(id);
            dead.Sort(StringComparer.Ordinal);

            Assert.AreEqual(0, dead.Count,
                "В меню шлема есть команды, которых НЕТ в реестре (" + dead.Count + " шт): " +
                string.Join(", ", dead.ToArray()) + ". Такие кнопки ничего не делают — удалите их " +
                "из KvToolbarGroups.Build() или зарегистрируйте команду.");
        }

        /// <summary>
        /// ГРУППА «Прочее» НЕ ДОЛЖНА БЫТЬ СВАЛКОЙ.
        ///
        /// Это не эстетика: команда, попавшая в «Прочее», для оператора фактически потеряна —
        /// он ищет её по смыслу. Порог 20 оставляет место редким командам, но не даёт
        /// свалке расти незаметно.
        /// </summary>
        [Test]
        public void MiscGroup_IsNotADumpingGround()
        {
            string file = Path.Combine(ScriptsRoot, "06_KazistovVv_UI", "03_Scripts", "Zones",
                "KvToolbarGroups.cs");
            if (!File.Exists(file))
                Assert.Ignore("KvToolbarGroups.cs не найден по ожидаемому пути");
            string text = File.ReadAllText(file);

            Match m = Regex.Match(text,
                @"new\s+KvToolbarGroup\s*\(\s*""misc""\s*,(.*?)\)\s*,",
                RegexOptions.Singleline);
            Assert.IsTrue(m.Success, "Группа «misc» не найдена в KvToolbarGroups.Build()");

            int count = Regex.Matches(m.Groups[1].Value, @"""[a-z][a-z0-9]*(?:\.[a-z0-9]+)+""").Count;
            Assert.LessOrEqual(count, 20,
                "В группе «Прочее» " + count + " команд — это уже свалка. Разложите их по смыслу: " +
                "постобработка в «Постобработку», зоны в «Визуализацию», файлы в «Запись и экспорт».");
        }
    }
}
