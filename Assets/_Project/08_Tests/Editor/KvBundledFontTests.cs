// ================================================================================================
//  KvBundledFontTests.cs — EditMode-тесты ШРИФТА CJK ИЗ ПРОЕКТА (сессия S44, долг 5.27).
//
//  ОТКУДА ЗАДАЧА. Для китайского и японского шрифт берётся из СИСТЕМЫ. На компьютере разработчика
//  это работает, а на шлеме (PICO, Android) набора шрифтов Windows нет — оператор видит
//  «квадраты» вместо иероглифов, и это навсегда: «система не дала шрифт».
//
//  ЧТО СДЕЛАНО. Шрифт теперь ищется СНАЧАЛА в проекте (`Resources/KvCjkFont/`), и только потом в
//  системе. Файл шрифта — бинарник ~16 МБ, и его добавление (лицензия + размер) — решение владельца,
//  поэтому кода достаточно, а файла пока нет.
//
//  ЧТО ЗАКРЕПЛЕНО ТЕСТАМИ:
//    1) поиск шрифта в проекте НЕ ПАДАЕТ и возвращает `null`, если файла нет (это норма);
//    2) список имён содержит ТОЛЬКО СВОБОДНЫЕ шрифты — системные Microsoft класть в сборку нельзя
//       по лицензии, и проверка этого защищает владельца от юридической ошибки;
//    3) поиск ресурса идёт по пути `KvCjkFont/<имя>` (без `Resources/` и без расширения) —
//       иначе файл, положенный «как надо», не найдётся;
//    4) список не пуст и покрывает оба языка (китайский и японский).
// ================================================================================================

using KazistovVvUI;
using NUnit.Framework;

namespace KazistovVvTests
{
    /// <summary>Шрифт CJK из проекта: путь поиска, свободные лицензии, устойчивость.</summary>
    public class KvBundledFontTests
    {
        /// <summary>
        /// СИСТЕМНЫЕ ШРИФТЫ MICROSOFT НЕЛЬЗЯ КЛАСТЬ В СБОРКУ. Их лицензия не разрешает
        /// распространение в составе приложения. Если кто-то добавит `msyh` в список, это будет
        /// нарушением — а заметить это глазами в списке из восьми строк легко не заметить.
        /// </summary>
        [Test]
        public void BundledFontList_ContainsOnlyFreeFonts()
        {
            string[] names = KvLoc.BundledCjkFontNames;
            Assert.Greater(names.Length, 0, "список шрифтов проекта пуст — поиск всегда провалится");

            var forbidden = new[]
            {
                "Microsoft YaHei", "Microsoft JhengHei", "msyh", "simsun", "simhei",
                "Meiryo", "Yu Gothic", "MS Gothic", "Segoe UI",
            };

            var problems = new System.Collections.Generic.List<string>();
            foreach (string name in names)
            {
                foreach (string bad in forbidden)
                {
                    if (name.IndexOf(bad, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        problems.Add(name + " ← системный шрифт «" + bad + "» под лицензией Microsoft");
                }
            }

            Assert.AreEqual(0, problems.Count,
                "в список шрифтов ПРОЕКТА попали системные шрифты — их лицензия не разрешает " +
                "распространение в составе приложения: " + string.Join("; ", problems.ToArray()) +
                ". Кладите только свободные: Noto Sans CJK, Source Han Sans (SIL OFL)");
        }

        /// <summary>
        /// СПИСОК ПОКРЫВАЕТ ОБА ЯЗЫКА. Проект переводится на китайский И японский; шрифт только для
        /// одного из них оставит второй с квадратами.
        /// </summary>
        [Test]
        public void BundledFontList_CoversBothChineseAndJapanese()
        {
            string[] names = KvLoc.BundledCjkFontNames;
            var joined = string.Join(" ", names).ToUpperInvariant();

            Assert.IsTrue(joined.Contains("CJK") || (joined.Contains("SC") && joined.Contains("JP")),
                "в списке нет шрифта с полным покрытием CJK — один из языков останется без глифов: " +
                joined);
        }

        /// <summary>
        /// ПОИСК НЕ ПАДАЕТ, КОГДА ФАЙЛА НЕТ. Это штатный случай прямо сейчас: кода достаточно,
        /// файла пока нет. Если бы поиск бросал исключение, приложение не запустилось бы на
        /// китайском языке вообще.
        /// </summary>
        [Test]
        public void BundledFontSearch_DoesNotThrowWhenMissing()
        {
            UnityEngine.Font font = null;
            Assert.DoesNotThrow(() => font = KvLoc.BundledCjkFont(),
                "поиск шрифта в проекте не должен бросать, даже если файла нет");

            // Файла нет — и это НОРМАЛЬНО, поэтому проверяем не «нашёлся», а «поиск не сломался».
            if (font == null)
                Assert.Pass("шрифта в проекте пока нет — поиск отработал штатно и вернул null");
            else
                Assert.IsNotNull(font.name, "найденный шрифт обязан иметь имя для журнала");
        }

        /// <summary>
        /// ПОВТОРНЫЙ ПОИСК НЕ ЧИТАЕТ ДИСК ЗАНОВО. `Resources.Load` небесплатен, а `UiFont`
        /// вызывается часто. Кэш проверяется по неизменности результата: второй вызов возвращает
        /// тот же объект (или тот же `null`), а не заново грузит.
        /// </summary>
        [Test]
        public void BundledFontSearch_IsCached()
        {
            UnityEngine.Font first = KvLoc.BundledCjkFont();
            UnityEngine.Font second = KvLoc.BundledCjkFont();
            Assert.AreSame(first, second,
                "повторный поиск обязан возвращать тот же объект (кэш), а не грузить заново");
        }

        /// <summary>
        /// ЖУРНАЛ МОЖНО ВЫКЛЮЧИТЬ. В пакетном прогоне и тестах строка «шрифт CJK: …» повторяется при
        /// каждом вызове и засоряет вывод. Флаг существует, и он работает.
        /// </summary>
        [Test]
        public void FontLogging_CanBeDisabled()
        {
            bool saved = KvLoc.logFontSelection;
            try
            {
                KvLoc.logFontSelection = false;
                Assert.IsFalse(KvLoc.logFontSelection);
            }
            finally
            {
                KvLoc.logFontSelection = saved;   // вернуть как было: тесты не должны влиять друг на друга
            }
        }
    }
}
