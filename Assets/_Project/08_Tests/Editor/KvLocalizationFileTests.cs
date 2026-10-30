using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KazistovVvUI;
using KazistovVvFeatures;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты СЛОВАРЕЙ ЛОКАЛИЗАЦИИ (сессия §32, карточка **Б15**).
    ///
    /// ЗАЧЕМ. На живом шлеме 13.10.2026 приложение написало:
    /// `[KazistovVv] Язык интерфейса: Русский · языков: 7 · **файлов прочитано: 0** · строк всего: 14`.
    /// Причина: на Android `Application.streamingAssetsPath` — это адрес ВНУТРИ пакета
    /// (`jar:file:///data/app/…/base.apk!/assets`), а не папка. `Directory.Exists` его не видит,
    /// `File.ReadAllText` не читает, и семь словарей (по ~187 строк каждый) на устройстве
    /// не подставлялись: интерфейс в шлеме оставался на русских литералах кода.
    ///
    /// ЧТО ИМЕННО ЗАКРЕПЛЕНО ЗДЕСЬ (и почему именно это, а не «чтение с Android» — его в EditMode
    /// не воспроизвести):
    ///   1) **в редакторе асинхронный путь НЕ включается** — папка словарей видна, значит
    ///      десктопный путь остался прежним (правило [K3]: компьютер не трогаем);
    ///   2) **список кодов = список файлов**: если добавят восьмой язык и забудут код в таблице
    ///      `KnownFileCodes`, на устройстве этот язык молча не загрузится — тест это назовёт;
    ///   3) **файлы словарей существуют и не пусты** — иначе «прочитано 7» на устройстве
    ///      превратилось бы в «прочитано 7, строк 0».
    ///
    /// Ни одного GameObject, ни одной записи, ни зависимости от времени.
    /// </summary>
    public class KvLocalizationFileTests
    {
        private static string LanguageFolder
        {
            get { return Path.Combine(Application.dataPath, "StreamingAssets", KvLoc.FolderName); }
        }

        /// <summary>В редакторе словари читаются с диска — асинхронная догрузка не нужна.</summary>
        [Test]
        public void Editor_DoesNotNeedAsyncFileLoad()
        {
            Assert.IsTrue(Directory.Exists(LanguageFolder),
                "не найдена папка словарей " + LanguageFolder + " — тогда и в редакторе их не прочитать");
            Assert.IsFalse(KvLoc.NeedsAsyncFileLoad(),
                "в редакторе папка словарей видна, значит асинхронный путь (для Android) включаться НЕ должен: " +
                "иначе мы меняем поведение компьютера");
        }

        /// <summary>
        /// КАЖДЫЙ КОД ИЗ ТАБЛИЦЫ ИМЕЕТ ФАЙЛ, И НАОБОРОТ. На устройстве каталог перечислить нельзя
        /// (он внутри пакета), поэтому файлы запрашиваются по именам из `KnownFileCodes`:
        /// забытый код = язык, которого в шлеме не будет.
        /// </summary>
        [Test]
        public void KnownCodes_MatchFilesOnDisk()
        {
            Assert.Greater(KvLoc.KnownFileCodes.Length, 0, "таблица кодов словарей пуста");

            string[] onDisk = Directory.GetFiles(LanguageFolder, "*.json");
            var diskCodes = new List<string>();
            foreach (string file in onDisk)
                diskCodes.Add(Path.GetFileNameWithoutExtension(file).ToLowerInvariant());

            var missingFiles = new List<string>();
            foreach (string code in KvLoc.KnownFileCodes)
            {
                string expected = code.ToLowerInvariant();
                if (!diskCodes.Contains(expected)) missingFiles.Add(expected);
            }

            Assert.AreEqual(0, missingFiles.Count,
                "эти коды есть в таблице, но файлов словарей нет: " + string.Join(", ", missingFiles.ToArray()));

            var missingCodes = new List<string>();
            foreach (string code in diskCodes)
            {
                bool known = false;
                foreach (string knownCode in KvLoc.KnownFileCodes)
                    if (knownCode.ToLowerInvariant() == code) { known = true; break; }
                if (!known) missingCodes.Add(code);
            }

            Assert.AreEqual(0, missingCodes.Count,
                "в проекте есть словари, которых НЕТ в таблице кодов — на устройстве они не загрузятся " +
                "(Android не перечисляет каталог внутри пакета): " + string.Join(", ", missingCodes.ToArray()));
        }

        /// <summary>Файлы словарей не пусты: иначе «прочитано 7» даст ноль строк.</summary>
        [Test]
        public void LanguageFiles_AreNotEmpty()
        {
            var empty = new List<string>();
            foreach (string code in KvLoc.KnownFileCodes)
            {
                string file = Path.Combine(LanguageFolder, code + ".json");
                if (!File.Exists(file)) { empty.Add(code + " (нет файла)"); continue; }
                string text = File.ReadAllText(file);
                if (string.IsNullOrEmpty(text) || text.Length < 100) empty.Add(code + " (пустой)");
            }

            Assert.AreEqual(0, empty.Count,
                "эти словари пусты или отсутствуют: " + string.Join(", ", empty.ToArray()));
        }

        /// <summary>
        /// ПУТЬ К ФАЙЛУ СЛОВАРЯ — это адрес, а не строка склейки. На устройстве он начинается
        /// с `jar:file://` (внутри пакета), и `UnityWebRequest` его понимает; на диске — обычный
        /// путь. Проверяется, что имя каталога берётся из одного места (`KvLoc.FolderName`).
        /// </summary>
        [Test]
        public void FolderName_IsUsedForLanguageFolder()
        {
            Assert.AreEqual("kazistovvv_i18n", KvLoc.FolderName,
                "имя каталога словарей изменилось — тогда на Android файлы будут запрашиваться не там");
        }

        // ============================================================ 2. ПОЛНОТА ПЕРЕВОДА (30.10.2026)
        //
        // ЗАЧЕМ. Аудит 30.10.2026 показал: сами файлы в порядке (406 ключей в каждом, наборы
        // совпадают), но 174 ключа код ЗАПРАШИВАЛ, а их не было ни в словарях, ни в рантайм-
        // таблицах — и на всех семи языках показывался русский fallback из кода. Прежние тесты
        // этого не видели: KvLocalizationFileTests проверял «файл существует и не пуст», а
        // `Localization_AllModulesAllKeysInSevenLanguages` — только таблицы модулей.
        //
        // ЧТО ЗДЕСЬ. Читаем сами JSON и составляем их ключи; читаем все таблицы KvLocExtra*..6
        // и добавляем их ключи; затем проверяем, что ключи, запрошенные кодом через `T("…", "…")`,
        // существуют. Проверка идёт по ИСХОДНИКАМ, поэтому видит и те ключи, которые никто
        // никогда не регистрировал.

        /// <summary>Ключи из всех словарей (объединение) вместе с ключами таблиц KvLocExtra*.</summary>
        private static HashSet<string> AllKnownKeys()
        {
            var keys = new HashSet<string>();
            var tables = new[]
            {
                KvLocExtra.RowCount, KvLocExtra2.RowCount, KvLocExtra3.RowCount,
                KvLocExtra4.RowCount, KvLocExtra5.RowCount, KvLocExtra6.RowCount,
            };
            for (int t = 0; t < tables.Length; t++)
                for (int i = 0; i < tables[t]; i++) keys.Add(KeyOfTable(t, i));

            foreach (string code in KvLoc.KnownFileCodes)
            {
                string file = Path.Combine(LanguageFolder, code + ".json");
                if (!File.Exists(file)) continue;
                string text = File.ReadAllText(file);
                // Строки словаря имеют вид { "key": "…", "text": "…" } — берём первый строковый
                // литерал каждой пары "key".
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(text, "\"key\"\\s*:\\s*\"([^\"]+)\""))
                    keys.Add(m.Groups[1].Value);
            }
            return keys;
        }

        private static string KeyOfTable(int table, int index)
        {
            switch (table)
            {
                case 0: return KvLocExtra.KeyAt(index);
                case 1: return KvLocExtra2.KeyAt(index);
                case 2: return KvLocExtra3.KeyAt(index);
                case 3: return KvLocExtra4.KeyAt(index);
                case 4: return KvLocExtra5.KeyAt(index);
                default: return KvLocExtra6.KeyAt(index);
            }
        }

        /// <summary>
        /// НАБОРЫ КЛЮЧЕЙ ВО ВСЕХ СЕМИ СЛОВАРЯХ СОВПАДАЮТ. Аудит подтвердил это замером (406/406,
        /// 0 недостающих) — теперь это закреплено тестом, а не отчётом.
        /// </summary>
        [Test]
        public void LanguageFiles_HaveMirrorKeySets()
        {
            var perLang = new Dictionary<string, HashSet<string>>();
            foreach (string code in KvLoc.KnownFileCodes)
            {
                string file = Path.Combine(LanguageFolder, code + ".json");
                if (!File.Exists(file)) continue;
                string text = File.ReadAllText(file);
                var keys = new HashSet<string>();
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(text, "\"key\"\\s*:\\s*\"([^\"]+)\""))
                    keys.Add(m.Groups[1].Value);
                perLang[code] = keys;
            }

            Assert.Greater(perLang.Count, 0, "ни один словарь не прочитан");
            string reference = null;
            foreach (string code in perLang.Keys) { reference = code; break; }
            var baseKeys = perLang[reference];
            foreach (var pair in perLang)
            {
                var missing = new List<string>();
                foreach (string key in baseKeys) if (!pair.Value.Contains(key)) missing.Add(key);
                Assert.AreEqual(0, missing.Count,
                    "в словаре " + pair.Key + " нет ключей, которые есть в " + reference + ": " +
                    string.Join(", ", missing.GetRange(0, System.Math.Min(10, missing.Count)).ToArray()));
            }
        }

        /// <summary>
        /// КЛЮЧИ, ЗАПРОШЕННЫЕ КОДОМ, СУЩЕСТВУЮТ. Это и есть защита от «показался русский текст
        /// посреди английского интерфейса»: каждый `T("ключ", "русский")` обязан иметь строку
        /// в словарях или в таблицах модулей.
        /// </summary>
        [Test]
        public void KeysRequestedByCode_ExistInDictionaries()
        {
            var known = AllKnownKeys();
            Assert.Greater(known.Count, 100, "словари не прочитаны — проверка бессмысленна");

            string scriptsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project", "01_Scripts"));
            Assert.IsTrue(Directory.Exists(scriptsRoot), "нет папки скриптов: " + scriptsRoot);

            var missing = new List<string>();
            var callPattern = new System.Text.RegularExpressions.Regex(
                "\\b[TFP]\\(\\s*\"([A-Za-z0-9_]+\\.[A-Za-z0-9_.\\-]+)\"\\s*,");

            foreach (string file in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (System.Text.RegularExpressions.Match m in callPattern.Matches(text))
                {
                    string key = m.Groups[1].Value;
                    if (known.Contains(key)) continue;
                    string entry = key + " (" + Path.GetFileName(file) + ")";
                    if (!missing.Contains(entry)) missing.Add(entry);
                }
            }

            Assert.AreEqual(0, missing.Count,
                "код запрашивает ключи, которых нет ни в одном словаре — на всех языках покажется " +
                "русский текст из кода: " + string.Join("; ",
                    missing.GetRange(0, System.Math.Min(15, missing.Count)).ToArray()));
        }

        /// <summary>
        /// У КАЖДОЙ СТРОКИ НОВОГО МОДУЛЯ ЗАПОЛНЕНЫ ВСЕ СЕМЬ ЯЗЫКОВ. Пустая ячейка — это ровно
        /// тот дефект, который нашёл аудит; тест не даёт вернуть его молча.
        /// </summary>
        [Test]
        public void MissingKeyModule_HasAllSevenLanguages()
        {
            Assert.Greater(KvLocExtra6.RowCount, 0, "модуль недостающих ключей пуст");
            var bad = new List<string>();
            for (int i = 0; i < KvLocExtra6.RowCount; i++)
            {
                if (KvLocExtra6.LanguageCount(i) != 7) bad.Add(KvLocExtra6.KeyAt(i));
                if (string.IsNullOrEmpty(KvLocExtra6.KeyAt(i))) bad.Add("(пустой ключ №" + i + ")");
            }
            Assert.AreEqual(0, bad.Count,
                "у этих строк не семь языков: " + string.Join(", ", bad.ToArray()));
        }

        // ============================================================ 3. ШРИФТ CJK (30.10.2026)
        //
        // ЗАЧЕМ. Для zh/ja интерфейс берёт СИСТЕМНЫЙ шрифт с CJK-глифами: во встроенном
        // LegacyRuntime.ttf их нет, и текст превращается в квадраты. Правило выбора шрифта жило
        // внутри цикла и нигде не проверялось — ошибка в нём означала бы «шрифт не найден» при
        // живом шрифте на машине. Теперь правило — чистая функция, и оно проверяется здесь.

        /// <summary>
        /// ПРАВИЛО ВЫБОРА CJK-ШРИФТА УЗНАЁТ НАСТОЯЩИЕ ИМЕНА и НЕ УЗНАЁТ посторонние.
        /// Проверяются имена, которые реально встречаются на Windows, macOS и Linux.
        /// </summary>
        [Test]
        public void CjkFontRule_RecognisesRealFontNames()
        {
            string[] cjk =
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "Meiryo UI", "Yu Gothic", "MS Gothic",
                "SimHei", "SimSun", "Noto Sans CJK SC", "Noto Sans CJK JP", "Source Han Sans SC",
                "MS Mincho", "FangSong"
            };
            foreach (string name in cjk)
            {
                Assert.IsTrue(KvLoc.LooksLikeCjkFont(name),
                    "шрифт «" + name + "» обязан считаться CJK — иначе на этом языке интерфейс " +
                    "покажет квадраты");
            }

            string[] notCjk = { "Arial", "Times New Roman", "Segoe UI", "Consolas", "", null };
            foreach (string name in notCjk)
            {
                Assert.IsFalse(KvLoc.LooksLikeCjkFont(name),
                    "«" + (name ?? "null") + "» НЕ CJK-шрифт, но правило его приняло — тогда " +
                    "китайский текст будет квадратами при «найденном» шрифте");
            }
        }

        /// <summary>
        /// ПОРЯДОК ПРЕДПОЧТЕНИЯ: универсальный китайский шрифт идёт РАНЬШЕ японского Gothic.
        ///
        /// ПОЧЕМУ. `MS Gothic` даёт японские формы иероглифов для китайского текста — носитель
        /// это замечает сразу. Поэтому `Microsoft YaHei` обязан стоять в списке выше `MS Gothic`;
        /// если кто-то переставит строки, тест назовёт это.
        /// </summary>
        [Test]
        public void CjkFontCandidates_PreferUniversalChineseFont()
        {
            var list = new List<string>(KvLoc.CjkFontCandidates);
            int yahei = list.IndexOf("Microsoft YaHei UI");
            int gothic = list.IndexOf("MS Gothic");

            Assert.GreaterOrEqual(yahei, 0, "в списке нет Microsoft YaHei UI — основного CJK-шрифта Windows");
            Assert.GreaterOrEqual(gothic, 0, "в списке нет MS Gothic — запасного японского шрифта");
            Assert.Less(yahei, gothic,
                "Microsoft YaHei обязан стоять ВЫШЕ MS Gothic: иначе китайский текст получит " +
                "японские формы иероглифов");
            Assert.Greater(list.Count, 5, "список шрифтов подозрительно короткий: " + list.Count);
        }

        /// <summary>
        /// НА РУССКОМ CJK-ШРИФТ НЕ НУЖЕН. Проверка дешёвая, но ловит регресс, при котором
        /// интерфейс начнёт тянуть тяжёлый системный шрифт там, где хватает встроенного.
        /// </summary>
        [Test]
        public void RussianDoesNotRequireCjkFont()
        {
            string previous = KvLoc.CurrentCode;
            try
            {
                KvLoc.SetLanguage("ru");
                Assert.IsFalse(KvLoc.NeedsCjk, "русскому языку CJK-шрифт не нужен");
                KvLoc.SetLanguage("zh");
                Assert.IsTrue(KvLoc.NeedsCjk, "китайскому языку CJK-шрифт ОБЯЗАТЕЛЕН");
            }
            finally
            {
                KvLoc.SetLanguage(previous);
            }
        }
    }
}
