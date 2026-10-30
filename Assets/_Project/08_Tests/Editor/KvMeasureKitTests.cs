// ================================================================================================
//  KvMeasureKitTests.cs — EditMode-тесты ЛИНЕЙКИ И СЕТКИ РАБОЧЕЙ ЗОНЫ (сессия S44, 30.10.2026).
//
//  ОТКУДА ЗАДАЧА. В меню «Сервис/Инструменты» два пункта — «Измерение» и «Сетка рабочей зоны» —
//  стояли ЗАГЛУШКАМИ (`Stub = true`) с подписью «пункт задел». Кнопки были, нажатие отвечало
//  «в разработке». Это худший вид долга: оператор видит функцию, которой нет.
//
//  ЧТО СДЕЛАНО. Арифметика вынесена в `KvMeasureKit` и проверяется здесь: расстояние,
//  проекции по осям, подбор шага сетки под размер области, число линий. Команды меню теперь
//  вызывают эти методы (см. `FeatureHub.RegisterCommands`).
//
//  ЧЕМ ЭТИ ТЕСТЫ ОТЛИЧАЮТСЯ ОТ «ДЛЯ ГАЛОЧКИ»: проверяются ровно те свойства, где ошибаются
//  на практике — знак (по модулю), единицы (метры, не миллиметры), границы (крайние линии входят
//  в сетку), масштаб (шаг растёт вместе с областью) и локаль (точка, а не запятая).
// ================================================================================================

using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Линейка и сетка: числа, которые видит оператор.</summary>
    public class KvMeasureKitTests
    {
        // ---------------------------------------------------------------- 1. ЛИНЕЙКА

        /// <summary>
        /// РАССТОЯНИЕ — В МЕТРАХ И ПО ТЕОРЕМЕ ПИФАГОРА. Точка (3, 4, 0) от начала координат даёт
        /// ровно 5 м — число, которое можно проверить в уме.
        /// </summary>
        [Test]
        public void Distance_IsEuclidean_InMeters()
        {
            Assert.AreEqual(5f, KvMeasureKit.Distance(Vector3.zero, new Vector3(3f, 4f, 0f)), 1e-4f,
                "расстояние (0,0,0)→(3,4,0) обязано быть 5 м");
            Assert.AreEqual(0f, KvMeasureKit.Distance(Vector3.one, Vector3.one), 1e-6f,
                "расстояние от точки до себя — ноль");
        }

        /// <summary>
        /// ПРОЕКЦИИ — ПО МОДУЛЮ. Оператору важно «насколько сдвинуто», а не знак: при замере
        /// стойки слева-справа знак только сбивает. Проверяется на точках с разными знаками.
        /// </summary>
        [Test]
        public void Delta_IsAbsoluteOnEveryAxis()
        {
            Vector3 d = KvMeasureKit.DeltaAbs(new Vector3(1f, 2f, 3f), new Vector3(-1f, 5f, 0f));
            Assert.AreEqual(2f, d.x, 1e-5f, "сдвиг по X = |−1 − 1| = 2");
            Assert.AreEqual(3f, d.y, 1e-5f, "сдвиг по Y = |5 − 2| = 3");
            Assert.AreEqual(3f, d.z, 1e-5f, "сдвиг по Z = |0 − 3| = 3 — обязан быть положительным");
        }

        /// <summary>
        /// СТРОКА ПОКАЗАНИЯ: миллиметры точности, точка вместо запятой, все три оси на месте.
        ///
        /// ПРО ЗАПЯТУЮ. На машине с русской локалью `ToString("0.000")` даёт «1,234». Эти числа
        /// потом ищут в журнале устройства и в отчёте — запятая ломает и то, и другое. Тест
        /// проверяет именно точку (та же ошибка уже была поймана в другом месте, см. S44).
        /// </summary>
        [Test]
        public void Describe_UsesDecimalPointAndShowsAxes()
        {
            string text = KvMeasureKit.Describe(Vector3.zero, new Vector3(0.25f, 1.2f, 0.1f));

            // √(0.25² + 1.2² + 0.1²) = √(0.0625 + 1.44 + 0.01) = √1.5125 = 1.2298… → «1.230».
            // Число посчитано вручную и записано здесь ВМЕСТЕ с выкладкой: если формула изменится,
            // расхождение будет видно сразу, а не после того, как оператор отмерит не то.
            StringAssert.Contains("1.230", text, "итоговое расстояние обязано быть с точкой и тремя знаками: " + text);
            StringAssert.Contains("0.250", text, "проекция X обязана быть в строке: " + text);
            StringAssert.Contains("1.200", text, "проекция Y обязана быть в строке: " + text);
            Assert.IsFalse(text.Contains(","), "в показании появилась десятичная запятая: " + text);
            StringAssert.Contains("м", text, "единицы измерения обязаны быть указаны");
        }

        // ---------------------------------------------------------------- 2. СЕТКА: ШАГ

        /// <summary>
        /// ШАГ РАСТЁТ ВМЕСТЕ С ОБЛАСТЬЮ. На столе 1×0,7 м годится 10 см, на виртуальном стенде
        /// 14×9 м — уже нет: 140 линий на сторону это заливка, а не сетка.
        ///
        /// Тест проверяет ГЛАВНОЕ СВОЙСТВО: чем больше область, тем крупнее шаг (шаг не убывает).
        /// Конкретные значения — ряд «человеческих» чисел, он тоже закреплён ниже.
        /// </summary>
        [Test]
        public void GridStep_GrowsWithArea()
        {
            float previous = 0f;
            for (float side = 0.2f; side <= 60f; side *= 2f)
            {
                float step = KvMeasureKit.GridStep(side);
                Assert.GreaterOrEqual(step, previous,
                    "шаг сетки уменьшился при росте области с " + side.ToString("0.0") + " м");
                previous = step;
            }
            Assert.Greater(KvMeasureKit.GridStep(14f), KvMeasureKit.GridStep(1f),
                "на стенде 14 м шаг обязан быть крупнее, чем на столе 1 м");
        }

        /// <summary>
        /// ЧИСЛО ЛИНИЙ НЕ ПРЕВЫШАЕТ ЗАДАННОГО ПОТОЛКА — иначе сетка превращается в заливку на
        /// больших стендах. Проверяется на всём диапазоне размеров при потолке 20.
        /// </summary>
        [Test]
        public void GridStep_KeepsLineCountUnderLimit()
        {
            for (float side = 0.2f; side <= 100f; side *= 1.5f)
            {
                float step = KvMeasureKit.GridStep(side, 20);
                int lines = KvMeasureKit.GridLines(side, step);
                Assert.LessOrEqual(lines, 21,
                    "при области " + side.ToString("0.0") + " м и шаге " + step.ToString("0.00") +
                    " м получилось " + lines + " линий — больше потолка 20 (+1 на границу)");
            }
        }

        /// <summary>
        /// ШАГ — ИЗ «ЧЕЛОВЕЧЕСКОГО» РЯДА. Оператор должен узнавать клетку с первого взгляда:
        /// 10, 20, 50 см, 1, 2, 5 м. Шаг «0,37 м» технически тоже работает и совершенно
        /// бесполезен на практике.
        /// </summary>
        [Test]
        public void GridStep_IsFromHumanSeries()
        {
            float[] human = { 0.1f, 0.2f, 0.5f, 1f, 2f, 5f, 10f, 20f, 50f };
            for (float side = 0.2f; side <= 80f; side *= 1.3f)
            {
                float step = KvMeasureKit.GridStep(side);
                bool found = false;
                foreach (float h in human) if (Mathf.Abs(h - step) < 1e-5f) { found = true; break; }
                Assert.IsTrue(found, "шаг " + step.ToString("0.000") + " м не из человеческого ряда");
            }
        }

        /// <summary>
        /// ВЫРОЖДЕННЫЕ ВХОДЫ: ноль, отрицательный размер, нулевой шаг. Сетка не должна ни падать,
        /// ни выдавать бесконечное число линий — это риск зависания интерфейса.
        /// </summary>
        [Test]
        public void Grid_HandlesDegenerateInputs()
        {
            Assert.Greater(KvMeasureKit.GridStep(0f), 0f, "при нулевой области шаг обязан быть положительным");
            Assert.Greater(KvMeasureKit.GridStep(-5f), 0f, "при отрицательной области шаг обязан быть положительным");
            Assert.AreEqual(0, KvMeasureKit.GridLines(1f, 0f), "при нулевом шаге линий нет");
            Assert.AreEqual(0, KvMeasureKit.GridLines(0f, 0.1f), "при нулевой стороне линий нет");
            Assert.AreEqual(0, KvMeasureKit.GridLines(-1f, 0.1f), "при отрицательной стороне линий нет");
        }

        // ---------------------------------------------------------------- 3. СЕТКА: ЛИНИИ

        /// <summary>
        /// КРАЙНИЕ ЛИНИИ ВХОДЯТ В СЕТКУ. Стол 1,0 м с шагом 0,25 м — это ПЯТЬ линий
        /// (0 / 0,25 / 0,5 / 0,75 / 1,0), а не четыре. По крайним оператор совмещает сетку
        /// с краем стола; без них сетка «не доходит» до края и её невозможно приложить.
        /// </summary>
        [Test]
        public void GridLines_IncludeBothBorders()
        {
            Assert.AreEqual(5, KvMeasureKit.GridLines(1.0f, 0.25f),
                "стол 1,0 м с шагом 0,25 м — пять линий, включая обе границы");
            Assert.AreEqual(2, KvMeasureKit.GridLines(0.25f, 0.25f),
                "ровно один шаг — две линии: начало и конец");
            Assert.AreEqual(1, KvMeasureKit.GridLines(0.1f, 0.25f),
                "если шаг больше стороны — остаётся только линия в начале");
        }

        /// <summary>
        /// ДРОБНЫЙ КРАЙ НЕ ТЕРЯЕТСЯ ИЗ-ЗА ПОГРЕШНОСТИ float. Классическая ловушка: 0,3 / 0,1
        /// в float даёт 2,9999999, и `FloorToInt` вернул бы 2 линии вместо 3. Проверяется именно
        /// этот случай — на нём сетка «не доходила» бы до края.
        /// </summary>
        [Test]
        public void GridLines_DoNotLoseBorderToFloatError()
        {
            Assert.AreEqual(4, KvMeasureKit.GridLines(0.3f, 0.1f),
                "0,3 м с шагом 0,1 м — четыре линии (0 / 0,1 / 0,2 / 0,3), а не три");
        }

        /// <summary>
        /// ПОДПИСЬ СЕТКИ НАЗЫВАЕТ РАЗМЕР, ШАГ И ЧИСЛО ЛИНИЙ. По ней в журнале видно, что команда
        /// действительно построила сетку, а не «выполнилась успешно». Числа — с точкой.
        /// </summary>
        [Test]
        public void DescribeGrid_ReportsSizeStepAndCounts()
        {
            string text = KvMeasureKit.DescribeGrid(1.0f, 0.7f);

            StringAssert.Contains("1.000", text, "в подписи нет ширины: " + text);
            StringAssert.Contains("0.700", text, "в подписи нет высоты: " + text);
            StringAssert.Contains("шаг 0.100", text, "на столе 1×0,7 м шаг обязан быть 10 см: " + text);
            StringAssert.Contains("11 × 8", text, "линий обязано быть 11 по ширине и 8 по высоте: " + text);
            Assert.IsFalse(text.Contains(","), "в подписи появилась десятичная запятая: " + text);
        }
    }
}
