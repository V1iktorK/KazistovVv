// ================================================================================================
//  KvCommandLocalizationTests.cs — EditMode-тесты ПЕРЕВОДОВ КОМАНД (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Подписи команд ищутся по СОБРАННОМУ ключу: `T("cmd." + id)` и
//  `T("cmd." + id + ".desc")` (см. `KvLocalization.Cmd`/`CmdDesc`). Из-за этого общий тест полноты
//  словарей (`KeysRequestedByCode_ExistInDictionaries`) их НЕ ВИДИТ: он ищет литералы вида
//  `T("cmd.что-то", …)`, а тут строка склеивается на ходу.
//
//  СЛЕДСТВИЕ, НАЙДЕННОЕ 30.10.2026: 102 из 140 нужных ключей отсутствовали в словаре. На русском
//  это незаметно (в коде есть русский запасной текст), а на остальных шести языках оператор видел
//  РУССКИЕ подписи команд — то есть перевод «работал» только наполовину.
//
//  ЧТО ЗАКРЕПЛЯЕТ ЭТОТ ТЕСТ: он находит ID команд там, где они РЕАЛЬНО объявлены
//  (`Id = "…"` и `Command("…"`, `Row("…"`), и требует, чтобы для каждого были ОБА ключа —
//  заголовок и пояснение.
//
//  ГРАНИЦА ЧЕСТНОСТИ: ключи для команд, которых нет в коде, тест не проверяет (их и не должно
//  быть). А вот пропущенный перевод у существующей команды — это ровно то, что он ловит.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Переводы команд: заголовок и пояснение для каждого идентификатора.</summary>
    public class KvCommandLocalizationTests
    {
        /// <summary>
        /// ИДЕНТИФИКАТОРЫ КОМАНД, КОТОРЫЕ НЕ ТРЕБУЮТ ПЕРЕВОДА — с причиной у каждого.
        ///
        /// ПУСТО, если таких нет. Строка здесь — это обещание, что отсутствие перевода осознано.
        /// </summary>
        private static readonly Dictionary<string, string> Exempt = new Dictionary<string, string>();

        private static string ScriptsRoot
        {
            get { return Path.Combine(Application.dataPath, "_Project", "01_Scripts"); }
        }

        /// <summary>Все ключи русского словаря (он — источник истины по составу).</summary>
        private static HashSet<string> RussianKeys()
        {
            var keys = new HashSet<string>();
            string path = Path.Combine(Application.dataPath, "StreamingAssets", "kazistovvv_i18n", "ru.json");
            if (!File.Exists(path)) return keys;

            // Формат — JSON-массив объектов {"key": …, "text": …}; читаем регуляркой, без парсера:
            // так тест не зависит от версии JSON-библиотеки в проекте.
            foreach (Match m in Regex.Matches(File.ReadAllText(path), @"""key""\s*:\s*""([^""]+)"""))
                keys.Add(m.Groups[1].Value);
            return keys;
        }

        /// <summary>
        /// ИДЕНТИФИКАТОРЫ КОМАНД ИЗ КОДА. Три формы объявления, которые в проекте реально есть:
        ///   • `Id = "…"` — свойства `KvCommand`;
        ///   • `Command("…"` — помощник регистрации;
        ///   • `Row("…"` — строки списка окон (у них свой `CommandId`).
        ///
        /// Отбрасывается суффикс `.desc`: это уже ключ ПОЯСНЕНИЯ, а не отдельная команда.
        /// </summary>
        private static HashSet<string> CommandIds()
        {
            var ids = new HashSet<string>();
            var patterns = new[]
            {
                new Regex(@"\bId\s*=\s*""([a-z][\w]*\.[\w.]+)"""),
                new Regex(@"\bCommand\s*\(\s*""([a-z][\w]*\.[\w.]+)"""),
                new Regex(@"\bRow\s*\(\s*""([a-z][\w]*\.[\w.]+)"""),
            };

            if (!Directory.Exists(ScriptsRoot)) return ids;
            foreach (string file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                // Словари переводов и тесты — не объявления команд.
                if (name.StartsWith("KvLocExtra")) continue;
                if (name.Contains("Tests")) continue;

                string text = File.ReadAllText(file);
                foreach (Regex pattern in patterns)
                {
                    foreach (Match m in pattern.Matches(text))
                    {
                        string id = m.Groups[1].Value;
                        if (id.EndsWith(".desc")) id = id.Substring(0, id.Length - 5);
                        ids.Add(id);
                    }
                }
            }
            return ids;
        }

        // ---------------------------------------------------------------- 1. ЗАГОЛОВКИ

        /// <summary>
        /// У КАЖДОЙ КОМАНДЫ ЕСТЬ ПЕРЕВОД ЗАГОЛОВКА (`cmd.&lt;id&gt;`).
        ///
        /// Это и есть исправление найденного дефекта: 102 ключа отсутствовали, и на шести языках
        /// оператор видел русские названия команд.
        /// </summary>
        [Test]
        public void EveryCommand_HasLocalizedTitle()
        {
            var known = RussianKeys();
            Assert.Greater(known.Count, 100, "словарь не прочитан — проверка бессмысленна");

            var ids = CommandIds();
            Assert.Greater(ids.Count, 20,
                "не найдено ни одной команды — разбор сломан, проверка бессмысленна (найдено " +
                ids.Count + ")");

            var missing = new List<string>();
            foreach (string id in ids)
            {
                if (Exempt.ContainsKey(id)) continue;
                string key = "cmd." + id;
                if (!known.Contains(key)) missing.Add(key);
            }
            missing.Sort();

            Assert.AreEqual(0, missing.Count,
                "у этих команд нет перевода ЗАГОЛОВКА — на всех языках, кроме русского, будет " +
                "русское название: " + string.Join(", ", missing.ToArray()));
        }

        // ---------------------------------------------------------------- 2. ПОЯСНЕНИЯ

        /// <summary>
        /// У КАЖДОЙ КОМАНДЫ ЕСТЬ ПЕРЕВОД ПОЯСНЕНИЯ (`cmd.&lt;id&gt;.desc`).
        ///
        /// ПОЧЕМУ ОТДЕЛЬНО ОТ ЗАГОЛОВКА. Пояснение показывается в подсказке и в поиске команд;
        /// если переведён только заголовок, поиск по смыслу на другом языке не работает — а именно
        /// поиском команд пользуются в шлеме, где набор текста дорог.
        /// </summary>
        [Test]
        public void EveryCommand_HasLocalizedDescription()
        {
            var known = RussianKeys();
            var ids = CommandIds();
            Assert.Greater(ids.Count, 20);

            var missing = new List<string>();
            foreach (string id in ids)
            {
                if (Exempt.ContainsKey(id)) continue;
                string key = "cmd." + id + ".desc";
                if (!known.Contains(key)) missing.Add(key);
            }
            missing.Sort();

            Assert.AreEqual(0, missing.Count,
                "у этих команд нет перевода ПОЯСНЕНИЯ — в подсказке и в поиске будет русский " +
                "текст: " + string.Join(", ", missing.ToArray()));
        }

        // ---------------------------------------------------------------- 3. ВСЕ ЯЗЫКИ

        /// <summary>
        /// ПЕРЕВОДЫ ЕСТЬ НА ВСЕХ ЯЗЫКАХ, А НЕ ТОЛЬКО В РУССКОМ. Русский словарь — источник состава,
        /// но если ключ есть только в нём, остальные шесть языков вернут русский запасной текст.
        ///
        /// Проверяются все семь файлов сразу: расхождение между словарями — это и есть «перевод,
        /// который работает наполовину».
        /// </summary>
        [Test]
        public void CommandTranslationsExistInAllLanguages()
        {
            var ids = CommandIds();
            Assert.Greater(ids.Count, 20);

            string folder = Path.Combine(Application.dataPath, "StreamingAssets", "kazistovvv_i18n");
            string[] languages = { "ru", "en", "zh", "es", "de", "fr", "ja" };

            var problems = new List<string>();
            foreach (string language in languages)
            {
                string path = Path.Combine(folder, language + ".json");
                if (!File.Exists(path)) { problems.Add(language + ": файла нет"); continue; }

                var keys = new HashSet<string>();
                foreach (Match m in Regex.Matches(File.ReadAllText(path), @"""key""\s*:\s*""([^""]+)"""))
                    keys.Add(m.Groups[1].Value);

                int missing = 0;
                foreach (string id in ids)
                {
                    if (Exempt.ContainsKey(id)) continue;
                    if (!keys.Contains("cmd." + id) || !keys.Contains("cmd." + id + ".desc")) missing++;
                }
                if (missing > 0) problems.Add(language + ": не хватает " + missing + " переводов команд");
            }

            Assert.AreEqual(0, problems.Count,
                "переводы команд неполные: " + string.Join("; ", problems.ToArray()));
        }

        // ---------------------------------------------------------------- 4. ИСКЛЮЧЕНИЯ ОБОСНОВАНЫ

        /// <summary>
        /// КАЖДОЕ ИСКЛЮЧЕНИЕ ИМЕЕТ ПРИЧИНУ. Пустая строка в `Exempt` — это «промолчали», то есть
        /// то же самое, что `Assert.Ignore`: дефект остаётся, но о нём не говорят (урок E-16).
        /// </summary>
        [Test]
        public void EveryExemptCommandHasReason()
        {
            var vague = new List<string>();
            foreach (var pair in Exempt)
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length < 10)
                    vague.Add(pair.Key);

            Assert.AreEqual(0, vague.Count,
                "исключения без причины: " + string.Join(", ", vague.ToArray()));
        }
    }
}
