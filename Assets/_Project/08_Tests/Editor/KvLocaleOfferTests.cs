// ================================================================================================
//  KvLocaleOfferTests.cs — EditMode-тесты СПИСКА ЯЗЫКОВ В ВЫБОРЕ ИНТЕРФЕЙСА (13.11.2026).
//
//  ОТКУДА ЗАДАЧА. Красный список тестировщика, §7: «Пять из семи языков интерфейса пусты».
//  В списке «Настройки → Интерфейс → Язык интерфейса» пять пунктов показывали `· 0` строк:
//  пункт выглядел рабочим, но выбор ничего не менял — это ровно то, что владелец запретил
//  формулировкой «каждая кнопка должна выполнять реальное действие».
//
//  ЧТО ЗДЕСЬ ЗАКРЕПЛЕНО:
//    1) в список выбора попадает только язык с НЕНУЛЕВЫМ числом строк (`KvLoc.Offered`);
//    2) фильтр не прячет рабочие языки: все семь словарей проекта предлагаются оператору;
//    3) ЧИСЛО СТРОК В СПИСКЕ ЖИВОЕ: строки, подгруженные ПОСЛЕ первого показа списка
//       (на устройстве словари читаются из пакета асинхронно), доезжают до подписи — ровно
//       тот дефект, из-за которого оператор видел `· 0` у переведённого языка;
//    4) выбор языка из списка РЕАЛЬНО меняет текст интерфейса (не только код состояния).
//
//  ГРАНИЦА ЧЕСТНОСТИ. Сборку самой панели настроек этот файл не поднимает — её состав
//  проверяет Е2Е-прогон (`KvE2eSettingsDiag`, проверки 9.3–9.5: кнопки `Lang_*` на вкладке
//  «Интерфейс»). Здесь проверяется ЛОГИКА отсева и живости счётчика — то, что можно
//  проверить чистой функцией, не строя uGUI.
// ================================================================================================

using System.Collections.Generic;
using KazistovVvUI;
using NUnit.Framework;

namespace KazistovVvTests
{
    [Category(KvTestCategories.Fast)]
    public class KvLocaleOfferTests
    {
        /// <summary>Исходный выбор языка — возвращается в TearDown, чтобы не ломать другие фикстуры.</summary>
        private string originalPreference;

        /// <summary>Ключ строк, которым проверяется смена языка (есть во всех словарях).</summary>
        private const string SampleKey = "menu.file";

        /// <summary>Признак «живой счётчик уже проверяли»: строка добавляется один раз за прогон.</summary>
        private static bool liveCounterProved;

        [SetUp]
        public void RememberLanguage()
        {
            originalPreference = KvLoc.PreferenceCode;
        }

        [TearDown]
        public void RestoreLanguage()
        {
            if (string.IsNullOrEmpty(originalPreference)) return;
            KvLoc.SetLanguage(originalPreference);
        }

        /// <summary>Подложка списка: код → число строк.</summary>
        private static List<KvLangInfo> Planted(params int[] counts)
        {
            string[] codes = { "ru", "zh", "de", "fr" };
            var list = new List<KvLangInfo>();
            for (int i = 0; i < counts.Length; i++)
            {
                var info = new KvLangInfo();
                info.code = codes[i];
                info.name = codes[i];
                info.englishName = codes[i];
                info.strings = counts[i];
                list.Add(info);
            }
            return list;
        }

        // ------------------------------------------------------------------ 1. ОТСЕВ

        /// <summary>
        /// ЯЗЫК БЕЗ СТРОК НЕ ПРЕДЛАГАЕТСЯ В ВЫБОРЕ.
        ///
        /// ЧТО БЫЛО. Список строился из `KvLoc.Languages` целиком: язык с нулём словаря
        /// нарисован рядовым пунктом, выбор его не менял ничего — «кнопка без функционала».
        ///
        /// ЧТО СТАЛО. `KvLoc.Offered` отсекает языки с пустым каталогом. Проверяется на
        /// подложке, где пустой язык ЕСТЬ во входном списке: иначе «его нет в результате»
        /// зеленело бы и на методе, который возвращает пустоту.
        /// </summary>
        [Test]
        public void OfferedFrom_DropsLanguagesWithoutStrings()
        {
            List<KvLangInfo> planted = Planted(7, 0, 12, 0);
            Assert.AreEqual(4, planted.Count, "подготовка сорвана: подложка построена не полностью");
            Assert.AreEqual(0, planted[1].strings, "подготовка сорвана: в подложке нет пустого языка");

            List<KvLangInfo> offered = KvLoc.OfferedFrom(planted);

            Assert.IsNotNull(offered, "отсев не вернул списка");
            Assert.AreEqual(2, offered.Count,
                "после отсева " + offered.Count + " языков (ожидались «ru» и «de»): " +
                "предложены " + string.Join(",", offered.ConvertAll(i => i.code).ToArray()));
            Assert.AreEqual("ru", offered[0].code, "порядок языков изменился");
            Assert.AreEqual("de", offered[1].code, "язык с ненулевым словарём пропал из списка");
            foreach (KvLangInfo info in offered)
                Assert.Greater(info.strings, 0,
                    "в список выбора попал язык «" + info.code + "» с нулём строк — оператор " +
                    "увидит рабочий пункт, который ничего не меняет");
        }

        /// <summary>Отсев НЕ меняет исходный список (его перестраивает панель настроек).</summary>
        [Test]
        public void OfferedFrom_DoesNotMutateTheSourceList()
        {
            List<KvLangInfo> planted = Planted(7, 0);
            List<KvLangInfo> offered = KvLoc.OfferedFrom(planted);

            Assert.AreEqual(2, planted.Count,
                "исходный список языков уменьшился до " + planted.Count +
                " — отсев пишет в чужой список, и панель настроек потеряла бы языки");
            Assert.AreEqual(1, offered.Count, "подготовка сорвана: отсев не отфильтровал пустой язык");
        }

        /// <summary>Пустые записи в списке языков не роняют отсев и не попадают в выбор.</summary>
        [Test]
        public void OfferedFrom_SkipsBrokenEntries()
        {
            List<KvLangInfo> planted = Planted(7, 0);
            planted.Insert(1, null);

            List<KvLangInfo> offered = null;
            Assert.DoesNotThrow(() => offered = KvLoc.OfferedFrom(planted),
                "отсев упал на пустой записи языка — панель настроек получила бы исключение");
            Assert.IsNotNull(offered, "отсев не вернул списка");
            Assert.AreEqual(1, offered.Count, "в выбор попала пустая запись или пропал рабочий язык");
            Assert.AreEqual("ru", offered[0].code, "порядок/состав выбора изменился");
        }

        // ------------------------------------------------------------------ 2. РЕАЛЬНЫЙ СПИСОК

        /// <summary>
        /// В РЕАЛЬНОМ ВЫБОРЕ НЕТ НИ ОДНОГО ЯЗЫКА С НУЛЁМ СТРОК.
        ///
        /// Это машинная форма требования тестировщика: «в списке нет ни одного языка `· 0`».
        /// Считается по каталогу в памяти (`CountOf`), а не по кэшированному полю: именно
        /// устаревшее поле и давало оператору `· 0` у переведённого языка.
        /// </summary>
        [Test]
        public void EveryOfferedLanguageHasStringsInTheRealRegistry()
        {
            List<KvLangInfo> offered = KvLoc.Offered();
            Assert.IsNotNull(offered, "список выбора не построился");
            Assert.GreaterOrEqual(offered.Count, 2,
                "в выборе " + offered.Count + " языков — список был бы нечем заполнить");

            foreach (KvLangInfo info in offered)
            {
                Assert.IsNotNull(info, "в выборе пустая запись языка");
                Assert.Greater(KvLoc.CountOf(info.code), 0,
                    "язык «" + info.code + "» предложен оператору с нулём строк — пункт списка " +
                    "будет выглядеть рабочим и ничего не изменит");
                Assert.Greater(info.strings, 0,
                    "счётчик языка «" + info.code + "» отстал от каталога (" + info.strings +
                    " против " + KvLoc.CountOf(info.code) + ") — подпись в списке соврёт");
            }
        }

        /// <summary>
        /// ВСЕ СЕМЬ СЛОВАРЕЙ ПРОЕКТА ПРЕДЛАГАЮТСЯ (анти-вакуум отсева).
        ///
        /// Фильтр «только с непустым словарём» опасен в другую сторону: он может спрятать и
        /// рабочие языки — тогда выбор сузится до одного пункта, и оператор решит, что других
        /// языков нет. Все `KnownFileCodes` обязаны присутствовать.
        /// </summary>
        [Test]
        public void AllSevenProjectLanguagesAreOffered()
        {
            List<KvLangInfo> offered = KvLoc.Offered();
            var offeredCodes = new HashSet<string>();
            foreach (KvLangInfo info in offered)
                if (info != null) offeredCodes.Add(info.code);

            Assert.AreEqual(7, KvLoc.KnownFileCodes.Length,
                "в проекте семь языков словарей — если число изменилось, тест надо пересмотреть");

            var missing = new List<string>();
            foreach (string code in KvLoc.KnownFileCodes)
            {
                Assert.Greater(KvLoc.CountOf(code), 0,
                    "словарь «" + code + "» пуст: язык не предложат, и его перевод недоступен");
                if (!offeredCodes.Contains(code)) missing.Add(code);
            }

            Assert.AreEqual(0, missing.Count,
                "рабочие языки пропали из списка выбора: " + string.Join(", ", missing.ToArray()));
        }

        // ------------------------------------------------------------------ 3. ЖИВОЙ СЧЁТЧИК

        /// <summary>
        /// ЧИСЛО СТРОК В СПИСКЕ ОБНОВЛЯЕТСЯ, КОГДА СТРОКИ ПРИХОДЯТ ПОЗЖЕ.
        ///
        /// ЧТО БЫЛО. Поле `KvLangInfo.strings` заполнялось ОДИН раз — в `KvLoc.Ensure`, сразу
        /// после чтения с диска. На устройстве диска нет (`StreamingAssets` живёт внутри
        /// пакета), словари подгружаются асинхронно и добавляют строки в каталог ПОЗЖЕ, а поле
        /// оставалось прежним. Оператор видел в списке `Русский · 7`, `English · 7`,
        /// `中文 · 0` — у переведённых языков, у которых на деле 535 строк.
        ///
        /// ЧТО СТАЛО. Каждая добавленная строка обновляет счётчик языка. Проверяется на всех
        /// семи языках сразу: ключ добавляется во ВСЕ каталоги, поэтому паритет ключей
        /// (на него смотрят `KvLocalizationCoverageTests` и `KvGraphicsTests`) не нарушается.
        /// </summary>
        [Test]
        public void LanguageStringCountStaysLiveWhenStringsArriveLate()
        {
            // Строки приходят и в рантайме (KvLocExtra*), и из пакета на устройстве — здесь
            // эмулируется тот же путь `AddRuntimeStrings`.
            if (!liveCounterProved)
            {
                foreach (string code in KvLoc.KnownFileCodes)
                    KvLoc.AddRuntimeStrings(code, "selfcheck.live.count", "строка для счётчика");
                liveCounterProved = true;
            }

            var problems = new List<string>();
            foreach (string code in KvLoc.KnownFileCodes)
            {
                int live = KvLoc.CountOf(code);
                int shown = 0;
                foreach (KvLangInfo info in KvLoc.Languages)
                    if (info != null && info.code == code) shown = info.strings;

                if (shown <= 0)
                    problems.Add(code + ": в списке 0 строк при " + live + " в каталоге");
                else if (shown != live)
                    problems.Add(code + ": в списке " + shown + " строк при " + live + " в каталоге");
            }

            Assert.AreEqual(0, problems.Count,
                "счётчик списка языков отстал от каталога — оператор увидит «· 0» у рабочего " +
                "языка: " + string.Join("; ", problems.ToArray()));
        }

        // ------------------------------------------------------------------ 4. ВЫБОР МЕНЯЕТ СТРОКИ

        /// <summary>
        /// ВЫБОР ЯЗЫКА ИЗ СПИСКА РЕАЛЬНО МЕНЯЕТ ТЕКСТ ИНТЕРФЕЙСА.
        ///
        /// ЧТО БЫЛО. Пять пунктов списка отвечали `· 0` и, будучи выбранными, оставляли
        /// интерфейс на прежнем языке: код состояния менялся, текст — нет.
        ///
        /// ЧТО СТАЛО. Для КАЖДОГО предложенного языка строка пункта меню обязана измениться.
        /// Проверяется по предложенному списку (а не по списку из теста): если язык не
        /// предлагают, его нельзя и проверить, а если предлагают — он обязан работать.
        /// </summary>
        [Test]
        public void ChoosingAnOfferedLanguageChangesInterfaceText()
        {
            List<KvLangInfo> offered = KvLoc.Offered();
            Assert.GreaterOrEqual(offered.Count, 2,
                "в выборе " + offered.Count + " языков — сравнивать нечего");

            KvLoc.SetLanguage("ru");
            string baseline = KvLoc.T(SampleKey, null);
            Assert.IsFalse(string.IsNullOrEmpty(baseline),
                "русская строка «" + SampleKey + "» пуста — сравнивать нечего");
            Assert.AreNotEqual(SampleKey, baseline,
                "`T` вернул сам ключ: русский каталог не прочитан");

            int compared = 0;
            var unchanged = new List<string>();
            foreach (KvLangInfo info in offered)
            {
                if (info == null || info.code == "ru") continue;
                KvLoc.SetLanguage(info.code);

                string text = KvLoc.T(SampleKey, null);
                if (string.IsNullOrEmpty(text))
                {
                    unchanged.Add(info.code + ": пустая строка");
                    continue;
                }
                if (string.Equals(text, baseline, System.StringComparison.Ordinal))
                    unchanged.Add(info.code + ": «" + text + "»");
                compared++;
            }

            Assert.AreEqual(0, unchanged.Count,
                "выбор языка не меняет текст интерфейса: " + string.Join("; ", unchanged.ToArray()));
            Assert.Greater(compared, 4,
                "сравнений " + compared + " — проверка охватила меньше пяти языков и была бы " +
                "неполной");
        }
    }
}
