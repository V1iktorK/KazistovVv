// ================================================================================================
//  KvLocalizationCoverageTests.cs — EditMode-тесты ПОКРЫТИЯ ЯЗЫКОВ (сессия S46).
//
//  ОТКУДА ЗАДАЧА. Проект переводится на семь языков (RU / EN / ZH / ES / DE / FR / JA). Словари
//  лежат в `StreamingAssets/kazistovvv_i18n/*.json` и читаются рантаймом `KvLoc`, который держит
//  ИХ ЖЕ в памяти (`KeysOf`, `CountOf`, `T`, `AllLanguages`, `SetLanguage`).
//
//  ЧЕМ ЭТОТ ФАЙЛ ОТЛИЧАЕТСЯ ОТ `KvLocalizationParityTests`. Тот файл разбирает JSON-ФАЙЛЫ сам
//  (свой разбор `key`/`text` регуляркой). Здесь проверяется то, что реально видит оператор:
//  КАТАЛОГИ, собранные `KvLoc` в памяти, — то есть включая встроенные строки и строки, которые
//  модули функций добавляют во время работы через `KvLoc.AddRuntimeStrings`. Разница
//  принципиальная: файл может быть правильным, а каталожный слой — потерять его (ровно так
//  на шлеме 13.10.2026 журнал показал «файлов прочитано: 0», и интерфейс остался на русских
//  литералах). Тест «на файлах» такое не поймает.
//
//  ЧТО БЫЛО И ЧЕМ ПЛОХО. Добавить ключ в один язык и забыть в остальных — самая частая ошибка
//  при переводе. Оператор видит в этом месте либо русский текст, либо пустое место, и понять,
//  почему кнопка безымянная, невозможно. Отдельная беда — «перевод» копией: латиница остаётся
//  латиницей на всех языках, и проверка «язык переключается» зеленеет на пустом переключении.
//
//  ЧТО СТАЛО И ЧТО ЗДЕСЬ ЗАКРЕПЛЕНО:
//    1) у всех семи языков одинаковое и НЕНУЛЕВОЕ число строк;
//    2) ни один язык не хранит пустое значение там, где в русском есть текст;
//    3) наборы ключей совпадают с русским (расхождения называются по именам, до 10);
//    4) переключение языка РЕАЛЬНО меняет строку, и переключение обратимо;
//    5) неизвестный код языка не ломает локализацию, пустой код означает «как в системе»;
//    6) в готовом тексте не остаётся незаполненных числовых подстановок, а там, где подстановка
//       предполагается, вызов с аргументом даёт осмысленный текст;
//    7) латиница не превращается в кириллицу и обратно «случайно».
//
//  ГРАНИЦА ЧЕСТНОСТИ. Насколько ПЕРЕВОД хорош, тест не судит: «Robot» и «Export» пишутся
//  одинаково на нескольких языках, и запрещать это значило бы требовать перевода названий.
//  Проверяются паритет, непустота, обратимость и отсутствие русского текста в нерусских языках.
//  `KvLoc.Reload()` здесь НЕ зовётся: он пересобирает общие каталоги, на которых стоят другие
//  фикстуры, и тест покрытия не имеет права их ронять.
// ================================================================================================

using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using KazistovVvFeatures;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Семь языков: одинаковый объём, паритет ключей, реальное и обратимое переключение.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvLocalizationCoverageTests
    {
        /// <summary>Коды языков проекта — из публичного списка `KvLoc`, а не из литералов теста.</summary>
        private static string[] Codes { get { return KvLoc.KnownFileCodes; } }

        /// <summary>Разделитель, которым `KvLoc.AllLanguages` склеивает переводы одного ключа.</summary>
        private const string JoinSeparator = " | ";

        /// <summary>Сколько расхождений паритета показывать в сообщении об ошибке.</summary>
        private const int MaxReportedDiffs = 10;

        private string originalLanguagePreference;

        // ---------------------------------------------------------------- сохранение окружения
        //
        // ТЕСТ НЕ ИМЕЕТ ПРАВА МЕНЯТЬ ЯЗЫК МАШИНЫ. Выбранный язык лежит в PlayerPrefs и в статическом
        // поле `KvLoc`, то есть это общее состояние: без восстановления следующие фикстуры
        // работали бы на языке, который выбрал ЭТОТ тест.

        [SetUp]
        public void SaveEnvironment()
        {
            originalLanguagePreference = PlayerPrefs.HasKey(KvLoc.PrefsKey)
                ? PlayerPrefs.GetString(KvLoc.PrefsKey)
                : KvLoc.SystemCode;
        }

        [TearDown]
        public void RestoreEnvironment()
        {
            KvLoc.SetLanguage(originalLanguagePreference);
        }

        // ---------------------------------------------------------------- вспомогательное

        /// <summary>Наборы ключей всех языков — берутся один раз: на 500+ ключей иначе это O(n²).</summary>
        private static List<HashSet<string>> KeySets()
        {
            var sets = new List<HashSet<string>>();
            foreach (string code in Codes) sets.Add(new HashSet<string>(KvLoc.KeysOf(code)));
            return sets;
        }

        /// <summary>У скольких языков этот ключ вообще есть (значение может быть пустым).</summary>
        private static int LanguagesWithKey(List<HashSet<string>> sets, string key)
        {
            int count = 0;
            foreach (HashSet<string> set in sets)
                if (set.Contains(key)) count++;
            return count;
        }

        /// <summary>
        /// СКОЛЬКО ЯЗЫКОВ ДАЛИ НЕПУСТОЙ ТЕКСТ. `KvLoc.AllLanguages` склеивает только НЕПУСТЫЕ
        /// значения через « | », поэтому пустая ячейка языка видна как «частей меньше, чем языков».
        /// </summary>
        private static int LanguagesWithText(string joined)
        {
            if (string.IsNullOrEmpty(joined)) return 0;
            string[] parts = joined.Split(new[] { JoinSeparator }, System.StringSplitOptions.None);
            int count = 0;
            foreach (string part in parts)
                if (!string.IsNullOrEmpty(part.Trim())) count++;
            return count;
        }

        /// <summary>
        /// Текст ключа в конкретном языке; null — ключа нет (иначе `T` вернул бы сам ключ).
        ///
        /// Язык переключается БЕЗ записи в хранилище (`save: false`): проверка покрытия не должна
        /// оставлять в настройках машины чужой язык — это делает TearDown один раз.
        /// </summary>
        private static string TextIn(string code, string key)
        {
            KvLoc.SetLanguage(code, false);
            string text = KvLoc.T(key, null);
            return string.IsNullOrEmpty(text) || string.Equals(text, key, System.StringComparison.Ordinal)
                ? null
                : text;
        }

        /// <summary>Есть ли в тексте кириллица (признак непереведённого русского текста).</summary>
        private static bool HasCyrillic(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
                if (c >= '\u0400' && c <= '\u04FF') return true;
            return false;
        }

        /// <summary>Есть ли в тексте подстановка вида «{0}», «{1:0.0}» или оборванная «{0».</summary>
        private static bool HasPlaceholder(string text)
        {
            return HighestPlaceholderIndex(text) >= 0;
        }

        /// <summary>
        /// МАКСИМАЛЬНЫЙ ИНДЕКС ПОДСТАНОВКИ в тексте («{0}» → 0, «шаг {0} из {5}» → 5).
        /// −1 — подстановок нет. Литеральные скобки БЕЗ числа («цикл `repeat N { … }`») не считаются:
        /// это документация синтаксиса, а не шаблон.
        /// </summary>
        private static int HighestPlaceholderIndex(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;

            int highest = -1;
            foreach (Match match in Regex.Matches(text, @"\{(\d+)"))
            {
                int index;
                if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out index))
                    continue;
                if (index > highest) highest = index;
            }
            return highest;
        }

        /// <summary>
        /// ПРОБЛЕМА ЛИ ПОДСТАНОВКА. null — всё в порядке.
        ///
        /// ЧТО БЫЛО (нашёл ПРОГОН, а не я). Помощник форматировал ЛЮБУЮ строку ЧЕТЫРЬМЯ аргументами.
        /// На строке «Графика: {0} · … · предел {5}» это давало `FormatException` — и тест обвинял
        /// КОРРЕКТНУЮ строку в том, что она «не форматируется». Проверка получалась не про
        /// незаполненные подстановки, а про число аргументов у самого теста.
        ///
        /// ЧЕМ ЭТО ПЛОХО. Ложное обвинение хуже пропуска: оно заставляет «чинить» исправный перевод,
        /// а настоящий дефект (оборванная скобка) тонет в шуме из семи одинаковых строк.
        ///
        /// КАК ПРОВЕРЯЕТСЯ ТЕПЕРЬ:
        ///   1) находим МАКСИМАЛЬНЫЙ индекс подстановки и подставляем РОВНО столько аргументов —
        ///      «{0}…{5}» получает шесть, «шаг {0} из {1}» — два;
        ///   2) исключения быть не должно (иначе шаблон сломан: скобка не закрыта или индекс битый);
        ///   3) в ГОТОВОМ тексте не должно остаться подстановки вида «{число}» — именно её оператор
        ///      и увидел бы на экране.
        /// </summary>
        private static string PlaceholderProblem(string text)
        {
            int highest = HighestPlaceholderIndex(text);
            if (highest < 0) return null;

            var arguments = new object[highest + 1];
            for (int i = 0; i < arguments.Length; i++)
                arguments[i] = i.ToString(CultureInfo.InvariantCulture);

            string formatted;
            try
            {
                formatted = string.Format(CultureInfo.InvariantCulture, text, arguments);
            }
            catch (System.FormatException e)
            {
                return "шаблон не форматируется (" + e.Message + "), подстановок до {" + highest + "}: " + text;
            }

            int leftover = HighestPlaceholderIndex(formatted);
            if (leftover >= 0)
                return "после подстановки в готовом тексте осталось «{" + leftover + "}»: " + formatted;
            if (formatted.Trim().Length == 0)
                return "после подстановки текст пуст";
            return null;
        }

        // ==================================================== 1. ОБЪЁМ ЯЗЫКОВ

        /// <summary>
        /// У ВСЕХ СЕМИ ЯЗЫКОВ ОДИНАКОВОЕ И НЕНУЛЕВОЕ ЧИСЛО СТРОК.
        ///
        /// ЧТО БЫЛО. Число строк — единственный дешёвый признак того, что словарь вообще прочитан.
        /// На шлеме 13.10.2026 каталог не прочитался вовсе («файлов прочитано: 0»), и все семь
        /// языков оказались на встроенном минимуме из семи строк — интерфейс остался русским.
        ///
        /// ЧЕМ ПЛОХО. «Язык есть в списке» ещё не значит «язык есть в памяти». Оператор
        /// переключает язык, и ничего не меняется — а сообщения об ошибке нет.
        ///
        /// ЧТО СТАЛО. Все семь каталогов обязаны иметь столько же строк, сколько русский эталон,
        /// и это число должно быть заведомо большим (иначе проверка была бы вакуумной).
        /// </summary>
        [Test]
        public void EveryLanguageHasSameStringCountAsRussian()
        {
            Assert.AreEqual(7, Codes.Length,
                "в проекте семь языков словарей — если число изменилось, тесты этого файла надо пересмотреть");
            Assert.IsTrue(System.Array.IndexOf(Codes, "ru") >= 0,
                "русского языка нет в списке кодов — эталон для сравнения не определён");

            int russian = KvLoc.CountOf("ru");
            Assert.Greater(russian, 500,
                "в русском каталоге " + russian + " строк — словари не прочитаны, и сравнивать нечего " +
                "(проверка была бы вакуумной)");

            var problems = new List<string>();
            foreach (string code in Codes)
            {
                if (code == "ru") continue;
                int count = KvLoc.CountOf(code);
                if (count == 0)
                    problems.Add(code + ": 0 строк (язык есть в списке, но каталог пуст)");
                else if (count != russian)
                    problems.Add(code + ": " + count + " строк против " + russian + " в русском");
            }

            Assert.AreEqual(0, problems.Count,
                "объём языков разошёлся — часть строк в этих языках отсутствует целиком: " +
                string.Join("; ", problems.ToArray()));
        }

        // ==================================================== 2. ПУСТЫЕ ЗНАЧЕНИЯ

        /// <summary>
        /// НИ ОДИН ЯЗЫК НЕ ХРАНИТ ПУСТОЕ ЗНАЧЕНИЕ ТАМ, ГДЕ В РУССКОМ ЕСТЬ ТЕКСТ.
        ///
        /// ЧТО БЫЛО. Пустая ячейка — это «ключ завели, перевод не вписали». В интерфейсе на месте
        /// подписи появляется пустое место: кнопка без названия, строка состояния без текста.
        /// Найти причину по внешнему виду невозможно — выглядит как «верстка поехала».
        ///
        /// ЧЕМ ПЛОХО. Именно так и появляется «перевод на 90 %»: 535 ключей есть, а 17 из них
        /// пустые, и заметить это глазами на семи языках нельзя.
        ///
        /// ЧТО СТАЛО. Для каждого русского ключа число языков, ДАВШИХ ТЕКСТ, обязано совпадать
        /// с числом языков, у которых этот ключ заведён. Разница означает ровно пустую ячейку.
        /// </summary>
        [Test]
        public void NoLanguageHasEmptyValueWhereRussianHasText()
        {
            List<HashSet<string>> sets = KeySets();
            List<string> russianKeys = KvLoc.KeysOf("ru");
            Assert.Greater(russianKeys.Count, 500,
                "русских ключей " + russianKeys.Count + " — проверять непустоту не на чем");

            var problems = new List<string>();
            int checkedKeys = 0;

            foreach (string key in russianKeys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                checkedKeys++;

                int withKey = LanguagesWithKey(sets, key);
                int withText = LanguagesWithText(KvLoc.AllLanguages(key));

                if (withText == 0)
                    problems.Add(key + ": пусто во ВСЕХ языках");
                else if (withText < withKey)
                    problems.Add(key + ": текст есть у " + withText + " из " + withKey + " языков");
                else if (withText > withKey)
                    problems.Add(key + ": частей склейки больше, чем языков (" + withText + " против " +
                                 withKey + ") — в переводе остался разделитель « | », он ломает журнал");
            }

            Assert.AreEqual(0, problems.Count,
                "в языках есть пустые ячейки (в интерфейсе будет пустое место): " +
                string.Join("; ", problems.ToArray()));
            Assert.Greater(checkedKeys, 500,
                "проверено " + checkedKeys + " ключей — обход почти ничего не проверил (P4-2)");

            // АНТИ-ВАКУУМ: сам разбор склейки обязан РАЗЛИЧАТЬ «есть текст» и «пусто», иначе
            // проверка выше проходила бы при любой потере строк.
            Assert.AreEqual(2, LanguagesWithText("Файл" + JoinSeparator + "File"),
                "разбор склейки не видит двух непустых переводов — проверка непустоты бессмысленна");
            Assert.AreEqual(1, LanguagesWithText("Файл" + JoinSeparator + " " + JoinSeparator + " "),
                "разбор склейки не видит ПУСТУЮ ячейку — ровно этот дефект он и должен ловить");
            Assert.AreEqual(0, LanguagesWithText(""),
                "пустая склейка обязана давать ноль языков");
        }

        // ==================================================== 3. ПАРИТЕТ КЛЮЧЕЙ

        /// <summary>
        /// НАБОРЫ КЛЮЧЕЙ ВСЕХ ЯЗЫКОВ РАВНЫ РУССКОМУ. Расхождения называются по именам.
        ///
        /// ЧТО БЫЛО. Паритет «на глаз» не проверяется: 535 ключей × 7 языков глазами не сверить,
        /// а расхождение в один ключ выглядит в интерфейсе как один непереведённый текст, который
        /// никто не найдёт.
        ///
        /// ЧЕМ ПЛОХО. Ключ, забытый в немецком, ищется только тогда, когда владелец случайно
        /// откроет именно эту панель на именно этом языке.
        ///
        /// ЧТО СТАЛО. Наборы сверяются множествами, а в сообщении — до десяти конкретных имён.
        /// </summary>
        [Test]
        public void KeySetsMatchRussian()
        {
            var russian = new HashSet<string>(KvLoc.KeysOf("ru"));
            Assert.Greater(russian.Count, 500,
                "русских ключей " + russian.Count + " — паритет проверять не на чем");
            Assert.IsTrue(russian.Contains("menu.file"),
                "в русском каталоге нет ключа menu.file — значит сверяются не те наборы");

            var problems = new List<string>();

            foreach (string code in Codes)
            {
                if (code == "ru") continue;
                var other = new HashSet<string>(KvLoc.KeysOf(code));

                foreach (string key in russian)
                {
                    if (problems.Count >= MaxReportedDiffs) break;
                    if (!other.Contains(key)) problems.Add(code + ": НЕТ ключа «" + key + "»");
                }
                if (problems.Count >= MaxReportedDiffs) break;

                foreach (string key in other)
                {
                    if (problems.Count >= MaxReportedDiffs) break;
                    if (!russian.Contains(key)) problems.Add(code + ": ЛИШНИЙ ключ «" + key + "»");
                }
                if (problems.Count >= MaxReportedDiffs) break;
            }

            Assert.AreEqual(0, problems.Count,
                "наборы ключей разошлись (показаны первые " + MaxReportedDiffs + " расхождений): " +
                string.Join("; ", problems.ToArray()) +
                ". Такой ключ оператор увидит на чужом языке, и это не будет ошибкой перевода");
        }

        // ==================================================== 4. ПЕРЕКЛЮЧЕНИЕ МЕНЯЕТ ТЕКСТ

        /// <summary>
        /// ПЕРЕКЛЮЧЕНИЕ ЯЗЫКА РЕАЛЬНО МЕНЯЕТ СТРОКУ, и строка непустая на каждом языке.
        ///
        /// ЧТО БЫЛО. Проверка «язык переключается» через одно поле состояния зеленеет и тогда,
        /// когда каталог не прочитан: код языка меняется, а текст остаётся русским.
        ///
        /// ЧЕМ ПЛОХО. Оператор выбирает «Deutsch», видит «Файл» — и не понимает, сломано
        /// переключение или перевод.
        ///
        /// ЧТО СТАЛО. Сравнивается САМ ТЕКСТ известного ключа на всех семи языках, и отдельно
        /// проверяется путь, которым пользуется строка меню (`KvLoc.Menu`).
        /// </summary>
        [Test]
        public void SwitchingLanguageChangesKnownString()
        {
            const string key = "menu.file";

            var texts = new Dictionary<string, string>();
            var problems = new List<string>();

            foreach (string code in Codes)
            {
                string text = TextIn(code, key);
                if (string.IsNullOrEmpty(text))
                {
                    problems.Add(code + ": строки нет");
                    continue;
                }
                texts[code] = text;
            }

            Assert.AreEqual(0, problems.Count,
                "ключ «" + key + "» отсутствует в этих языках: " + string.Join(", ", problems.ToArray()));

            Assert.AreNotEqual(texts["ru"], texts["en"],
                "русская и английская строки одного ключа совпали («" + texts["ru"] + "»): " +
                "переключение языка ничего не меняет, и «перевод» — копия");
            Assert.IsTrue(HasCyrillic(texts["ru"]),
                "русская строка ключа «" + key + "» без кириллицы: «" + texts["ru"] + "»");
            Assert.IsFalse(HasCyrillic(texts["en"]),
                "в английской строке остался русский текст: «" + texts["en"] + "»");

            // АНТИ-ВАКУУМ: семь языков не могут давать ОДИН текст — иначе сравнивать нечего.
            var distinct = new HashSet<string>();
            foreach (KeyValuePair<string, string> pair in texts) distinct.Add(pair.Value);
            Assert.GreaterOrEqual(distinct.Count, 5,
                "из " + texts.Count + " языков получилось всего " + distinct.Count +
                " различных строк — похоже, каталоги не различаются");

            // ПУТЬ МЕНЮ: подпись пункта меню переводится тем же каталогом, а не литералом панели.
            KvLoc.SetLanguage("ru");
            Assert.AreEqual(texts["ru"], KvLoc.Menu("Файл"),
                "`KvLoc.Menu(\"Файл\")` на русском не совпал с каталогом — меню и словарь разошлись");
            KvLoc.SetLanguage("en");
            Assert.AreEqual(texts["en"], KvLoc.Menu("Файл"),
                "`KvLoc.Menu(\"Файл\")` на английском не перевёлся: меню осталось русским");
        }

        // ==================================================== 5. НЕИЗВЕСТНЫЙ ЯЗЫК

        /// <summary>
        /// НЕИЗВЕСТНЫЙ КОД ЯЗЫКА НЕ ЛОМАЕТ ЛОКАЛИЗАЦИЮ; ПУСТОЙ КОД = «КАК В СИСТЕМЕ».
        ///
        /// ЧТО БЫЛО. Код языка приходит из хранилища и из интерфейса, то есть может быть чем угодно:
        /// «klingon», пустая строка, null после сброса настроек.
        ///
        /// ЧЕМ ПЛОХО. Если бы `SetLanguage` принимал неизвестный код, текущий язык стал бы «ничем»:
        /// каталога нет, `T` возвращает ключи, и весь интерфейс превращается в «cmd.point.select».
        ///
        /// ЧТО СТАЛО. Неизвестный код отклоняется с предупреждением, прежний язык остаётся;
        /// пустой код документированно означает «язык как в системе».
        /// </summary>
        [Test]
        public void UnknownLanguageCodeKeepsCurrentLanguage()
        {
            KvLoc.SetLanguage("de");
            string before = KvLoc.CurrentCode;
            string textBefore = KvLoc.T("menu.file", null);
            Assert.AreEqual("de", before, "подготовка сорвана: немецкий не применился");
            Assert.IsFalse(string.IsNullOrEmpty(textBefore), "подготовка сорвана: строка пуста");

            // (1) НЕИЗВЕСТНЫЙ КОД.
            Assert.DoesNotThrow(() => KvLoc.SetLanguage("klingon"),
                "неизвестный код языка бросил исключение — вызов идёт из панели настроек");
            Assert.AreEqual(before, KvLoc.CurrentCode,
                "неизвестный код «klingon» сменил язык на «" + KvLoc.CurrentCode + "»");
            Assert.AreEqual(textBefore, KvLoc.T("menu.file", null),
                "неизвестный код языка изменил текст интерфейса");

            // (2) ПУСТОЙ КОД И null — «как в системе» (документированное значение).
            Assert.DoesNotThrow(() => KvLoc.SetLanguage(""), "пустой код языка бросил исключение");
            Assert.IsFalse(string.IsNullOrEmpty(KvLoc.CurrentCode),
                "пустой код оставил приложение без языка");
            Assert.AreNotEqual(KvLoc.SystemCode, KvLoc.CurrentCode,
                "применённый язык не может быть служебным «system» — оператору нужно название языка");
            Assert.AreNotEqual("menu.file", KvLoc.T("menu.file", null),
                "после пустого кода `T` вернул сам ключ — каталог потерян");

            Assert.DoesNotThrow(() => KvLoc.SetLanguage(null), "null вместо кода бросил исключение");

            // АНТИ-ВАКУУМ: известный код ЯЗЫК МЕНЯЕТ. Без этого «неизвестный не меняет язык»
            // проходило бы и на реализации, которая не переключает ничего.
            KvLoc.SetLanguage("ja");
            Assert.AreNotEqual(before, KvLoc.CurrentCode,
                "переключение на ИЗВЕСТНЫЙ язык не сработало — тогда проверка «неизвестный не " +
                "ломает» ничего не значит");
        }

        // ==================================================== 6. ПОДСТАНОВКИ

        /// <summary>
        /// В ГОТОВОМ ТЕКСТЕ НЕ ОСТАЁТСЯ НЕЗАПОЛНЕННЫХ ЧИСЛОВЫХ ПОДСТАНОВОК.
        ///
        /// ЧТО БЫЛО. Часть строк — шаблоны: в них есть `{0}`, `{1}`, иногда с форматом (`{0:0.0}`).
        /// Подставляет их `KvLocExtra.F`, и при ошибке форматирования он МОЛЧА возвращает шаблон —
        /// то есть оператор увидит на экране «шаг {0} из {1}» и решит, что сломан перевод.
        ///
        /// ЧЕМ ПЛОХО. Ошибка в шаблоне (незакрытая скобка, лишний аргумент) не видна ни в одном
        /// тесте: `F` её проглатывает, а строка выглядит «как есть».
        ///
        /// ЧТО СТАЛО. Каждая строка каждого языка с числовой подстановкой обязана форматироваться
        /// и терять подстановку в результате.
        /// </summary>
        [Test]
        public void ReadyTextHasNoUnfilledPlaceholders()
        {
            // СТРОКИ-ШАБЛОНЫ ЖИВУТ В МОДУЛЯХ ФУНКЦИЙ, А НЕ В JSON-СЛОВАРЯХ: их добавляет
            // `KvLocExtra.Install()` (так же поступают `KvGraphicsTests`). Без установки сканировать
            // было бы нечего, и проверка формы подстановок стала бы вакуумной.
            KvLocExtra.Install();

            var problems = new List<string>();
            int scanned = 0;
            int withPlaceholders = 0;

            foreach (string code in Codes)
            {
                KvLoc.SetLanguage(code);
                foreach (string key in KvLoc.KeysOf(code))
                {
                    string text = KvLoc.T(key, null);
                    if (string.IsNullOrEmpty(text) || string.Equals(text, key, System.StringComparison.Ordinal))
                        continue;
                    scanned++;
                    if (!HasPlaceholder(text)) continue;
                    withPlaceholders++;

                    string problem = PlaceholderProblem(text);
                    if (problem != null && problems.Count < MaxReportedDiffs)
                        problems.Add(code + ":" + key + " — " + problem);
                }
            }

            Assert.AreEqual(0, problems.Count,
                "строки с незаполненными подстановками (оператор увидит скобки на экране): " +
                string.Join("; ", problems.ToArray()));
            Assert.Greater(scanned, 500, "просмотрено " + scanned + " строк — проверка была бы вакуумной");
            Assert.Greater(withPlaceholders, 0,
                "ни одной строки с подстановкой не найдено: проверка формы подстановок ничего не " +
                "проверила (в проекте такие строки есть — `KvLocExtra.F`)");

            // АНТИ-ВАКУУМ: сам разбор обязан отличать годную подстановку от сломанной.
            Assert.IsNull(PlaceholderProblem("Время: {0:0.0} с"),
                "годная подстановка признана ошибкой — тест выше краснел бы на живом тексте");
            Assert.IsNull(PlaceholderProblem("Путь {0} · шаг {1}"),
                "годная подстановка из двух аргументов признана ошибкой");

            // РЕГРЕССИЯ ПРОГОНА 12.11.2026: строка с ШЕСТЬЮ подстановками (`graphics.applied`)
            // обвинялась в том, что «не форматируется», — потому что разбор подставлял четыре
            // аргумента. Здесь это ложное обвинение закреплено как НЕДОПУСТИМОЕ.
            Assert.IsNull(PlaceholderProblem(
                    "Графика: {0} · масштаб {1} · тени {2} · AA {3} · VSync {4} · предел {5}"),
                "строка с шестью подстановками признана сломанной — это то самое ложное обвинение, " +
                "из-за которого корректный перевод «чинили» вместо исправления теста");
            Assert.AreEqual(5, HighestPlaceholderIndex("шаг {0} из {5}"),
                "разбор не находит максимальный индекс подстановки — аргументов снова не хватит");

            Assert.IsNotNull(PlaceholderProblem("Время: {0"),
                "ОБОРВАННАЯ подстановка не поймана — ровно этот дефект и должен ловиться");
            Assert.IsNotNull(PlaceholderProblem("шаг {0} из {1"),
                "оборванная вторая подстановка не поймана");

            // Подстановка через публичный путь форматирования даёт ОСМЫСЛЕННЫЙ текст, а не шаблон.
            string formatted = KvLocExtra.F("selfcheck.placeholder.probe", "шаг {0} из {1}", 3, 7);
            Assert.AreEqual("шаг 3 из 7", formatted,
                "`KvLocExtra.F` не подставил аргументы: оператор увидит на экране шаблон");
        }

        // ==================================================== 7. ЛАТИНИЦА / КИРИЛЛИЦА

        /// <summary>
        /// В НЕРУССКИХ ЯЗЫКАХ НЕТ РУССКОГО ТЕКСТА, А В РУССКОМ ЕСТЬ.
        ///
        /// ЧТО БЫЛО. Самый быстрый способ «добавить перевод» — скопировать русскую строку во все
        /// языки. Ключи при этом на месте, число строк совпадает, и все проверки объёма зелёные.
        ///
        /// ЧЕМ ПЛОХО. Проверка «язык переключается» тоже зелёная: строка формально меняется на
        /// языке, где перевода нет (или не меняется — и тогда непонятно, чья это ошибка).
        ///
        /// ЧТО СТАЛО. На выборке известных ключей интерфейса проверяется направление письма:
        /// в русском обязана быть кириллица, в остальных шести языках её быть не должно.
        ///
        /// ГРАНИЦА ЧЕСТНОСТИ: выборка намеренно маленькая и состоит из ключей СЛОВАРЕЙ. Модули
        /// функций добавляют строки кодом, и там есть осознанные долги (`KvLocExtra6` держит
        /// русский текст во всех языках, пока перевода нет). Придираться к ним здесь значило бы
        /// падать на названном долге, а не на дефекте.
        /// </summary>
        [Test]
        public void SampleKeysAreNotTransliteratedWrongWay()
        {
            string[] keys =
            {
                "menu.file", "menu.view", "menu.help", "status.language",
                "common.on", "common.off", "settings.header.language"
            };
            Assert.GreaterOrEqual(keys.Length, 5, "выборка ключей слишком мала для проверки");

            var problems = new List<string>();
            int compared = 0;

            foreach (string key in keys)
            {
                string russian = TextIn("ru", key);
                if (string.IsNullOrEmpty(russian)) { problems.Add(key + ": нет русского текста"); continue; }
                if (!HasCyrillic(russian))
                    problems.Add(key + ": русский текст без кириллицы («" + russian + "»)");

                foreach (string code in Codes)
                {
                    if (code == "ru") continue;
                    string text = TextIn(code, key);
                    if (string.IsNullOrEmpty(text)) { problems.Add(code + ":" + key + ": текста нет"); continue; }
                    if (HasCyrillic(text))
                        problems.Add(code + ":" + key + " = «" + text + "» (русский текст в нерусском языке)");
                    else compared++;
                }
            }

            Assert.AreEqual(0, problems.Count,
                "направление письма перепутано — это пропущенный перевод или копия русского текста: " +
                string.Join("; ", problems.ToArray()));
            Assert.GreaterOrEqual(compared, 20,
                "сравнений " + compared + " — выборка почти ничего не проверила (P4-2)");
        }

        // ==================================================== 8. ОБРАТИМОСТЬ

        /// <summary>
        /// ПЕРЕКЛЮЧЕНИЕ ЯЗЫКА ОБРАТИМО: вернулись на прежний — строки совпали с исходными.
        ///
        /// ЧТО БЫЛО. Смена языка пересобирает оболочку и освобождает кэш шрифта
        /// (`KvLoc.ReleaseFont`). Побочный эффект — «испорченный» словарь после возврата: часть
        /// строк осталась от предыдущего языка, и оператор видит смесь.
        ///
        /// ЧЕМ ПЛОХО. Владелец переключает язык «посмотреть, как будет», возвращается — и часть
        /// интерфейса остаётся на чужом языке до перезапуска.
        ///
        /// ЧТО СТАЛО. Проверяется выборка ключей: после «туда и обратно» значения обязаны быть
        /// ровно теми же, и при этом на промежуточном языке они ДОЛЖНЫ отличаться (иначе
        /// обратимость выполнялась бы на переключении, которое ничего не делает).
        /// </summary>
        [Test]
        public void LanguageSwitchIsReversible()
        {
            KvLoc.SetLanguage("ru");
            List<string> keys = KvLoc.KeysOf("ru");
            Assert.Greater(keys.Count, 500, "русских ключей " + keys.Count + " — выборку не построить");

            int step = System.Math.Max(1, keys.Count / 25);
            var baseline = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < keys.Count && baseline.Count < 25; i += step)
            {
                string text = KvLoc.T(keys[i], null);
                if (string.IsNullOrEmpty(text)) continue;
                baseline.Add(new KeyValuePair<string, string>(keys[i], text));
            }

            Assert.GreaterOrEqual(baseline.Count, 10,
                "в выборке всего " + baseline.Count + " строк — проверка обратимости была бы вакуумной");

            // (1) УШЛИ на английский: строки обязаны измениться.
            KvLoc.SetLanguage("en");
            int different = 0;
            foreach (KeyValuePair<string, string> pair in baseline)
                if (!string.Equals(KvLoc.T(pair.Key, null), pair.Value, System.StringComparison.Ordinal))
                    different++;

            // (2) ВЕРНУЛИСЬ: строки обязаны совпасть с исходными.
            KvLoc.SetLanguage("ru");
            var mismatches = new List<string>();
            foreach (KeyValuePair<string, string> pair in baseline)
            {
                string text = KvLoc.T(pair.Key, null);
                if (!string.Equals(text, pair.Value, System.StringComparison.Ordinal))
                    mismatches.Add(pair.Key + ": было «" + pair.Value + "», стало «" + text + "»");
            }

            Assert.Greater(different, baseline.Count / 2,
                "переключение на английский изменило только " + different + " строк из " +
                baseline.Count + " — похоже, каталоги не различаются, и обратимость проверять не на чем");
            Assert.AreEqual(0, mismatches.Count,
                "после возврата на русский строки НЕ совпали с исходными (словарь испорчен " +
                "переключением): " + string.Join("; ", mismatches.ToArray()));
        }

        // ==================================================== 9. СПИСОК ЯЗЫКОВ

        /// <summary>
        /// СПИСОК ЯЗЫКОВ ПРИГОДЕН ДЛЯ ИНТЕРФЕЙСА: у каждого есть код и название, и только
        /// китайский с японским требуют CJK-шрифта.
        ///
        /// ЧТО БЫЛО. Панель настроек строит список языков из `KvLoc.Languages`. Если у языка пустое
        /// название, оператор видит пустую строку в списке и не может выбрать язык.
        ///
        /// ЧЕМ ПЛОХО. «Язык есть, но выбрать его нельзя» — тупик без объяснения.
        ///
        /// ЧТО СТАЛО. Проверяются код, название и подпись каждого языка, а также флаг CJK: без него
        /// китайский и японский показываются квадратами (у встроенного шрифта нет этих глифов).
        /// </summary>
        [Test]
        public void LanguageListIsUsableForTheUi()
        {
            IReadOnlyList<KvLangInfo> languages = KvLoc.Languages;
            Assert.GreaterOrEqual(languages.Count, 7, "в списке языков меньше семи");

            var problems = new List<string>();
            var seen = new HashSet<string>();

            foreach (KvLangInfo info in languages)
            {
                if (info == null) { problems.Add("(пустая запись языка)"); continue; }
                if (string.IsNullOrEmpty(info.code)) problems.Add("язык без кода");
                if (string.IsNullOrEmpty(info.name)) problems.Add(info.code + ": без названия");
                if (string.IsNullOrEmpty(info.Label)) problems.Add(info.code + ": пустая подпись для списка");
                if (!seen.Add(info.code)) problems.Add(info.code + ": повтор в списке");

                bool expectsCjk = info.code == "zh" || info.code == "ja";
                if (info.needsCjk != expectsCjk)
                    problems.Add(info.code + ": флаг CJK = " + info.needsCjk + ", ожидался " + expectsCjk);
            }

            Assert.AreEqual(0, problems.Count,
                "список языков непригоден для панели настроек: " + string.Join("; ", problems.ToArray()));

            // АНТИ-ВАКУУМ: флаг CJK влияет на выбор шрифта, и это видно через публичное свойство.
            KvLoc.SetLanguage("ja");
            Assert.IsTrue(KvLoc.NeedsCjk, "японский язык не требует CJK-шрифта — иероглифы будут квадратами");
            KvLoc.SetLanguage("de");
            Assert.IsFalse(KvLoc.NeedsCjk, "немецкий язык потребовал CJK-шрифт — лишняя ветка выбора шрифта");
            Assert.IsFalse(string.IsNullOrEmpty(KvLoc.Status),
                "строка диагностики локализации пуста — по ней нельзя понять, что загружено");
        }
    }
}
