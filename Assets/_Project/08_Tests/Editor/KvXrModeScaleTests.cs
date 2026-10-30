// ================================================================================================
//  KvXrModeScaleTests.cs — EditMode-тесты МАСШТАБА В СТРОКЕ РЕЖИМА (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Строка журнала «РЕЖИМ: … · масштаб мира 1.00» — это то, по чему наладчик сверяет
//  масштаб перед замером в MR (карточки Б4/Б5): в смешанной реальности виртуальный стол обязан
//  совпасть с реальным.
//
//  ЧТО НАЙДЕНО 30.10.2026. Строка печатала КОНСТАНТУ `DefaultWorldScale` (1.00) даже тогда, когда
//  применялся другой масштаб. То есть журнал ВСЕГДА говорил «1.00» — и замер по нему подтверждал
//  записанное число, а не фактическое. Дефект того же рода, что «мёртвая проверка»: выглядит как
//  измерение, но ничего не измеряет.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) строка называет ФАКТИЧЕСКИЙ масштаб, а не константу;
//    2) масштаб печатается С ТОЧКОЙ, а не с запятой (в русской локали «1,00» ломает разбор логов);
//    3) нулевой и отрицательный масштаб заменяются безопасным (геометрия не схлопывается);
//    4) режимы VR/MR/AR различаются в строке.
// ================================================================================================

using System.Text.RegularExpressions;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Строка режима: фактический масштаб, разделитель, безопасные значения.</summary>
    public class KvXrModeScaleTests
    {
        /// <summary>
        /// СТРОКА НАЗЫВАЕТ ФАКТИЧЕСКИЙ МАСШТАБ. Это главный тест файла: до исправления здесь стояла
        /// константа, и замер в MR подтверждал бы сам себя.
        /// </summary>
        [Test]
        public void DescribeNamesActualScale()
        {
            string line = KvXrPlatformModeSelector.Describe(
                KvXrPlatformMode.Mr, true, false, true, 0.069f);

            StringAssert.Contains("0.069", line,
                "строка не называет ФАКТИЧЕСКИЙ масштаб 0.069 — в журнале будет константа, и замер " +
                "в MR подтвердит сам себя: " + line);

            // И не должна показывать константу вместо него.
            StringAssert.DoesNotContain("1.000", line,
                "строка показывает масштаб по умолчанию вместо фактического: " + line);
        }

        /// <summary>
        /// МАСШТАБ ПО УМОЛЧАНИЮ ВСЁ РАВНО ПЕЧАТАЕТСЯ. Обратная ошибка — «строка без числа» — тоже
        /// дефект: по ней нечего сверять.
        /// </summary>
        [Test]
        public void DescribePrintsDefaultScaleWhenNotSpecified()
        {
            string line = KvXrPlatformModeSelector.Describe(
                KvXrPlatformMode.Vr, false, false, false);

            Assert.IsTrue(Regex.IsMatch(line, @"масштаб мира \d"),
                "в строке нет числа масштаба: " + line);
        }

        /// <summary>
        /// ЧИСЛО С ТОЧКОЙ, А НЕ С ЗАПЯТОЙ. В русской локали `ToString("0.000")` даёт «0,069» —
        /// запятая в журнале путается с разделителем списка, и скрипт разбора логов (а такой в
        /// проекте есть) прочитает строку неверно.
        /// </summary>
        [Test]
        public void ScaleUsesDotNotComma()
        {
            string line = KvXrPlatformModeSelector.Describe(
                KvXrPlatformMode.Mr, true, false, true, 0.069f);

            Assert.IsFalse(Regex.IsMatch(line, @"масштаб мира \d+,\d"),
                "масштаб напечатан с ЗАПЯТОЙ — скрипт разбора логов прочитает его неверно: " + line);
            Assert.IsTrue(Regex.IsMatch(line, @"масштаб мира \d+\.\d"),
                "масштаб напечатан не в виде числа с точкой: " + line);
        }

        /// <summary>
        /// НУЛЕВОЙ И ОТРИЦАТЕЛЬНЫЙ МАСШТАБ БЕЗОПАСНЫ. Масштаб 0 «схлопывает» всю геометрию в точку:
        /// оператор увидит пустую комнату и решит, что приложение сломалось. Отрицательный зеркалит
        /// мир. Оба значения — ошибка данных, а не настройка.
        /// </summary>
        [Test]
        public void ZeroAndNegativeScaleAreSafe()
        {
            var values = new[] { 0f, -1f, -0.5f, 1e-9f };

            foreach (float value in values)
            {
                string line = KvXrPlatformModeSelector.Describe(
                    KvXrPlatformMode.Mr, true, false, true, value);

                Assert.IsTrue(Regex.IsMatch(line, @"масштаб мира 0\.\d{3}|масштаб мира [1-9]"),
                    "при масштабе " + value + " в строке небезопасное число: " + line);
                StringAssert.DoesNotContain("масштаб мира -", line,
                    "при масштабе " + value + " в строке отрицательное число: " + line);
                StringAssert.DoesNotContain("масштаб мира 0.000", line,
                    "при масштабе " + value + " геометрия схлопнется в точку: " + line);
            }
        }

        /// <summary>
        /// РЕЖИМЫ РАЗЛИЧАЮТСЯ В СТРОКЕ. Если VR, MR и AR дают одинаковый текст, наладчик по журналу
        /// не поймёт, в каком режиме запустилось приложение, — а это первый вопрос при разборе.
        /// </summary>
        [Test]
        public void ModesAreDistinguishableInLine()
        {
            string vr = KvXrPlatformModeSelector.Describe(KvXrPlatformMode.Vr, false, false, false);
            string mr = KvXrPlatformModeSelector.Describe(KvXrPlatformMode.Mr, true, false, true);
            string ar = KvXrPlatformModeSelector.Describe(KvXrPlatformMode.Ar, false, true, true);

            StringAssert.Contains("ВИРТУАЛЬНАЯ", vr, "в строке VR нет слова ВИРТУАЛЬНАЯ: " + vr);
            StringAssert.Contains("СМЕШАННАЯ", mr, "в строке MR нет слова СМЕШАННАЯ: " + mr);
            StringAssert.Contains("ДОПОЛНЕННАЯ", ar, "в строке AR нет слова ДОПОЛНЕННАЯ: " + ar);

            Assert.AreNotEqual(vr, mr, "строки VR и MR совпали");
            Assert.AreNotEqual(mr, ar, "строки MR и AR совпали");
        }

        /// <summary>
        /// В СТРОКЕ ПЕРЕЧИСЛЕНЫ ВСЕ ТРИ ВОЗМОЖНОСТИ. «Почему приложение в VR, хотя шлем умеет MR» —
        /// главный вопрос наладчика; ответ должен быть виден сразу, без догадок.
        /// </summary>
        [Test]
        public void LineListsAllThreeCapabilities()
        {
            string line = KvXrPlatformModeSelector.Describe(KvXrPlatformMode.Vr, true, false, true);

            StringAssert.Contains("сквозной вид", line, line);
            StringAssert.Contains("якоря", line, line);
            StringAssert.Contains("камера", line, line);
        }
    }
}
