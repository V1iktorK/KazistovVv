using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ПУНКТ 8 ЗАДАНИЯ ВЛАДЕЛЬЦА (02.11.2026): АВТОСТОЛ ШИРЕ МАКСИМАЛЬНО ДОСТИГАЕМОЙ ТОЧКИ.
    ///
    /// СМЫСЛ. Радиус автостола считается от ПОЛНОГО вылета руки робота (длина цепочки пивотов
    /// до TCP — та же величина, что у зоны достижимости), плюс запас 0,10 м. Стол обязан быть
    /// ШИРЕ рабочей зоны: рука не должна доставать за кромку столешницы.
    ///
    /// ЧТО БЫЛО НЕ ТАК. Прежняя формула `0,95 × вылет + 0,10` у 6-осевого робота давала
    /// 0,95 × 2,368 + 0,10 = 2,3496 м — МЕНЬШЕ вылета 2,368 м: стол был уже максимально
    /// достигаемой точки на 18 мм. Тест падает на любой формуле, которая уменьшает вылет.
    /// </summary>
    public class KvXrP8AutoTableTests
    {
        /// <summary>Вылет 6-осевого робота сцены, метры (KvReachMath / журнал зон достижимости).</summary>
        private const float SixAxisReachMeters = 2.368f;

        /// <summary>Вылет SCARA LS10-B702S, метры.</summary>
        private const float ScaraReachMeters = 0.713f;

        /// <summary>Допуск сравнения чисел, метры (формулы дают точные значения).</summary>
        private const float Tolerance = 0.0001f;

        /// <summary>
        /// ГЛАВНАЯ ПРОВЕРКА ПУНКТА 8: радиус автостола БОЛЬШЕ вылета руки у обоих роботов сцены.
        /// Проверяются оба, потому что автостол строится и для 6-осевого, и для SCARA.
        /// </summary>
        [Test]
        public void AutoTable_RadiusIsWiderThanRobotReach()
        {
            CheckRadiusIsWider("6-осевой робот", SixAxisReachMeters);
            CheckRadiusIsWider("SCARA LS10-B702S", ScaraReachMeters);
        }

        /// <summary>
        /// При нескольких роботах в сцене автостол берёт САМЫЙ БОЛЬШОЙ вылет: иначе стол окажется
        /// уже рабочей зоны другого робота. Пустая сцена роботов не даёт ложного «гигантского» значения.
        /// </summary>
        [Test]
        public void AutoTable_TakesLargestReachOfSceneRobots()
        {
            float max = KvReachMath.MaxReach(new[] { ScaraReachMeters, SixAxisReachMeters });
            Assert.AreEqual(SixAxisReachMeters, max, Tolerance,
                "из двух роботов берётся больший вылет (2,368 м, а не 0,713 м)");

            Assert.Greater(KvReachMath.AutoRadius(max), SixAxisReachMeters,
                "стол по максимальному вылету накрывает самую дальнюю точку рабочей зоны");

            Assert.AreEqual(0f, KvReachMath.MaxReach(null), Tolerance,
                "роботов нет — вылет 0, а не выдуманное число");
            Assert.AreEqual(0f, KvReachMath.MaxReach(new float[0]), Tolerance,
                "пустой список — вылет 0");
        }

        /// <summary>
        /// Проверка одного робота: радиус строго больше вылета ровно на запас, и меню с построителем
        /// берут одно и то же число (второй формулы радиуса в проекте быть не должно).
        /// </summary>
        private static void CheckRadiusIsWider(string robotName, float reachMeters)
        {
            float radius = KvReachMath.AutoRadius(reachMeters);

            Assert.Greater(radius, reachMeters,
                robotName + ": радиус автостола " + radius.ToString("F4") +
                " м обязан быть БОЛЬШЕ вылета руки " + reachMeters.ToString("F4") +
                " м — стол шире рабочей зоны, а не уже неё");

            Assert.AreEqual(reachMeters + KvXrTableKinds.AutoMarginMeters, radius, Tolerance,
                robotName + ": радиус = полный вылет + запас 0,10 м, без коэффициента уменьшения");

            Assert.AreEqual(radius, KvXrTableKinds.Radius(KvXrTableKind.Auto, reachMeters), Tolerance,
                robotName + ": меню и построитель автостола берут радиус из одной формулы");
        }
    }
}
