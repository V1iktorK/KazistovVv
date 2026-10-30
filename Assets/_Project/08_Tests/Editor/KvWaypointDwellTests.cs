// ================================================================================================
//  KvWaypointDwellTests.cs — EditMode-тесты ПАУЗ В МАРШРУТЕ (сессия S44, 30.10.2026).
//
//  ОТКУДА ЗАДАЧА. Аудит S43 нашёл в `KvWaypointRouteKit.InsertDwells` два дефекта, которых
//  не видел ни один тест:
//    1) дубль позы добавлялся ТОЙ ЖЕ ССЫЛКОЙ (`path.Add(plan.Path[i])`), а не копией;
//    2) время маршрута пересобиралось с нуля шагом 0.05 с, независимо от исходного шага плана.
//  Снаружи это выглядело как «пауза не работает как пауза»: маршрут ехал не туда и не так,
//  как его построил планировщик.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ (без сцены — чистые данные):
//    1) пауза ДОБАВЛЯЕТ время ровно на свою длительность, а не пересобирает маршрут;
//    2) дубли позы — НЕЗАВИСИМЫЕ массивы (правка одного не меняет другие);
//    3) нулевая и отрицательная пауза ничего не добавляют;
//    4) сэмпл, указанный в паузе, реально дублируется, а остальные — нет;
//    5) маршрут без пауз возвращается как есть.
// ================================================================================================

using System.Collections.Generic;
using KazistovVvFeatures;
using NUnit.Framework;
using TrajectoryCore;

namespace KazistovVvTests
{
    /// <summary>Вставка выдержек в маршрут: время, копии поз, границы.</summary>
    public class KvWaypointDwellTests
    {
        /// <summary>
        /// ПЛАН ИЗ N СЭМПЛОВ С ЗАДАННЫМ ШАГОМ ВРЕМЕНИ. Первый сэмпл — нулевая поза, каждый
        /// следующий отличается одной осью, чтобы дубли были видны по значению.
        /// </summary>
        private static PlannedTrajectory MakePlan(int samples, float timeStep)
        {
            var path = new double[samples][];
            var times = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                path[i] = new double[] { i * 1.0, 0, 0, 0, 0, 0 };
                times[i] = i * timeStep;
            }
            var plan = new PlannedTrajectory
            {
                Path = path,
                Times = times,
                Time = times[samples - 1],
                Label = "тест"
            };
            return plan;
        }

        // ---------------------------------------------------------------- 1. ВРЕМЯ

        /// <summary>
        /// ПАУЗА ДОБАВЛЯЕТ РОВНО СВОЁ ВРЕМЯ, А НЕ ПЕРЕСОБИРАЕТ МАРШРУТ.
        ///
        /// ДО ПРАВКИ здесь было бы неверно: время пересобиралось шагом 0,05 с, поэтому маршрут
        /// из 4 сэмплов с шагом 0,01 с (полное время 0,03 с) после вставки одной паузы 1 с
        /// получал бы время... 0,01 → 0,05 → 0,10 → 0,15 и так далее. Тест сравнивает
        /// ПРИРАЩЕНИЕ времени, а не абсолютное значение: так он ловит именно пересборку.
        /// </summary>
        [Test]
        public void Dwell_AddsItsOwnDuration_AndDoesNotRebuildTiming()
        {
            PlannedTrajectory plan = MakePlan(4, 0.01f);          // полное время 0,03 с
            var dwells = new List<KvDwell>
            {
                new KvDwell { AfterSample = 1, Seconds = 1.0f }
            };

            PlannedTrajectory result = KvWaypointRouteKit.InsertDwells(plan, dwells, 0.05f);

            Assert.IsNotNull(result, "вставка паузы обязан вернуть план");
            double added = result.Time - plan.Time;
            Assert.AreEqual(1.0, added, 0.06,
                "пауза 1 с обязана добавить ровно ~1 с (допуск на округление копий по 0,05 с); " +
                "добавилось " + added.ToString("0.000") + " с");
        }

        /// <summary>
        /// ВРЕМЯ НЕ УБЫВАЕТ и начинается с того же нуля — базовая корректность тайминга после
        /// вставки. Если это сломается, исполнитель «поедет назад» или зависнет.
        /// </summary>
        [Test]
        public void Dwell_KeepsTimesMonotonicAndStartsAtZero()
        {
            PlannedTrajectory plan = MakePlan(5, 0.02f);
            var dwells = new List<KvDwell>
            {
                new KvDwell { AfterSample = 0, Seconds = 0.2f },
                new KvDwell { AfterSample = 3, Seconds = 0.3f }
            };

            PlannedTrajectory result = KvWaypointRouteKit.InsertDwells(plan, dwells, 0.05f);

            Assert.AreEqual(0f, result.Times[0], 1e-6f, "время обязано начинаться с нуля");
            for (int i = 1; i < result.Times.Length; i++)
            {
                Assert.GreaterOrEqual((double)result.Times[i], (double)result.Times[i - 1],
                    "время обязано не убывать (сэмпл " + i + ")");
            }
            Assert.AreEqual((double)result.Times[result.Times.Length - 1], (double)result.Time, 1e-5,
                "поле Time обязано совпадать с последним временем");
        }

        // ---------------------------------------------------------------- 2. КОПИИ ПОЗ

        /// <summary>
        /// ДУБЛИ ПОЗЫ — НЕЗАВИСИМЫЕ МАССИВЫ (главный дефект аудита S43).
        ///
        /// Проверка прямая: взять позу сэмпла, в котором стоит пауза, изменить её — и убедиться,
        /// что ОСТАЛЬНЫЕ дубли не изменились и исходный план не изменился. Со ссылками вместо
        /// копий все три «позы» оказались бы одним массивом.
        /// </summary>
        [Test]
        public void Dwell_DuplicatedPosesAreIndependentCopies()
        {
            PlannedTrajectory plan = MakePlan(3, 0.05f);
            double originalValue = plan.Path[1][0];

            var dwells = new List<KvDwell>
            {
                new KvDwell { AfterSample = 1, Seconds = 0.15f }   // три копии по 0,05 с
            };

            PlannedTrajectory result = KvWaypointRouteKit.InsertDwells(plan, dwells, 0.05f);

            // Индексы: 0 — сэмпл 0, 1 — сэмпл 1, 2..4 — три копии сэмпла 1, 5 — сэмпл 2.
            Assert.Greater(result.Path.Length, 5,
                "копии паузы обязаны появиться в пути: было " + plan.Path.Length +
                ", стало " + result.Path.Length);

            result.Path[2][0] = 999.0;                             // правим ОДНУ копию

            Assert.AreEqual(originalValue, result.Path[3][0], 1e-9,
                "правка одной копии изменила соседнюю — дубли ссылаются на один массив");
            Assert.AreEqual(originalValue, plan.Path[1][0], 1e-9,
                "правка копии изменила ИСХОДНЫЙ план — поза не скопирована");
        }

        /// <summary>ИСХОДНЫЙ ПЛАН НЕ МЕНЯЕТСЯ ВООБЩЕ: функция возвращает новый объект, старый цел.</summary>
        [Test]
        public void Dwell_DoesNotMutateSourcePlan()
        {
            PlannedTrajectory plan = MakePlan(4, 0.05f);
            int sourcePathLength = plan.Path.Length;
            double sourceTime = plan.Time;

            var dwells = new List<KvDwell> { new KvDwell { AfterSample = 2, Seconds = 0.5f } };
            KvWaypointRouteKit.InsertDwells(plan, dwells, 0.05f);

            Assert.AreEqual(sourcePathLength, plan.Path.Length, "исходный план получил новые сэмплы");
            Assert.AreEqual((double)sourceTime, (double)plan.Time, 1e-6, "исходный план получил новое время");
        }

        // ---------------------------------------------------------------- 3. ГРАНИЦЫ

        /// <summary>
        /// ПАУЗА НОЛЬ И ОТРИЦАТЕЛЬНАЯ НИЧЕГО НЕ ДОБАВЛЯЮТ. Отрицательное значение — ошибка
        /// ввода; она не должна ни удлинять, ни укорачивать маршрут.
        /// </summary>
        [Test]
        public void Dwell_ZeroOrNegative_AddsNothing()
        {
            PlannedTrajectory plan = MakePlan(4, 0.05f);
            var dwells = new List<KvDwell>
            {
                new KvDwell { AfterSample = 1, Seconds = 0f },
                new KvDwell { AfterSample = 2, Seconds = -3f }
            };

            PlannedTrajectory result = KvWaypointRouteKit.InsertDwells(plan, dwells, 0.05f);

            Assert.AreEqual(plan.Path.Length, result.Path.Length,
                "пауза ноль и отрицательная не должны добавлять сэмплов");
            Assert.AreEqual((double)plan.Time, (double)result.Time, 1e-5,
                "пауза ноль и отрицательная не должны менять время");
        }

        /// <summary>
        /// НЕТ ПАУЗ — ПЛАН ВОЗВРАЩАЕТСЯ КАК ЕСТЬ (тот же объект). Функция не должна ничего
        /// копировать зря: её вызывают на каждом маршруте, в том числе без пауз.
        /// </summary>
        [Test]
        public void NoDwells_ReturnsSamePlan()
        {
            PlannedTrajectory plan = MakePlan(3, 0.05f);
            Assert.AreSame(plan, KvWaypointRouteKit.InsertDwells(plan, null),
                "без списка пауз план обязан вернуться тем же объектом");
            Assert.AreSame(plan, KvWaypointRouteKit.InsertDwells(plan, new List<KvDwell>()),
                "с пустым списком пауз план обязан вернуться тем же объектом");
        }

        /// <summary>
        /// ПАУЗА СТАВИТСЯ ПОСЛЕ УКАЗАННОГО СЭМПЛА. Номер в `AfterSample` — не «где-то», а
        /// конкретный сэмпл: если сдвинуть его хоть на один, пауза окажется не там, где просил
        /// оператор. Проверяется по значению позы в точке, где начинаются копии.
        /// </summary>
        [Test]
        public void Dwell_AppearsAfterRequestedSample()
        {
            PlannedTrajectory plan = MakePlan(5, 0.05f);          // позы 0,1,2,3,4
            var dwells = new List<KvDwell> { new KvDwell { AfterSample = 3, Seconds = 0.1f } };

            PlannedTrajectory result = KvWaypointRouteKit.InsertDwells(plan, dwells, 0.05f);

            // Копии сэмпла 3 обязаны идти подряд ДВА раза (0,1 с / 0,05 = 2 копии).
            int dwellPoseIndex = -1;
            int repeats = 0;
            for (int i = 1; i < result.Path.Length; i++)
            {
                if (result.Path[i][0] == result.Path[i - 1][0])
                {
                    if (dwellPoseIndex < 0) dwellPoseIndex = i;
                    repeats++;
                }
            }

            Assert.Greater(repeats, 0, "пауза не дала ни одного повтора позы");
            Assert.AreEqual(3.0, result.Path[dwellPoseIndex][0], 1e-9,
                "пауза встала не после сэмпла 3, а после позы " + result.Path[dwellPoseIndex][0]);
        }
    }
}
