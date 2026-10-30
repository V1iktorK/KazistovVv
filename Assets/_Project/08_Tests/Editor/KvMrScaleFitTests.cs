// ================================================================================================
//  KvMrScaleFitTests.cs — EditMode-тесты РАСЧЁТА МАСШТАБА ДЛЯ MR
//  (сессия S44, карточки Б4 и Б5: «масштаб виртуального мира не совпадает с реальным»).
//
//  ОТКУДА ЗАДАЧА. Б4/Б5 висят с 28.09.2026 с формулировкой «нужен замер на устройстве».
//  Замер требуется не «вообще», а КОНКРЕТНОГО ЧИСЛА — и это число можно посчитать заранее из
//  известных габаритов. Тогда в шлеме остаётся ПОДТВЕРДИТЬ, а не подбирать на глаз.
//
//  ЧТО ЗАКРЕПЛЕНО ТЕСТАМИ:
//    1) МЕТРИЧЕСКИЙ мир даёт масштаб 1 по всем трём осям (это и значит «1 юнит = 1 метр»);
//    2) НЕМЕТРИЧЕСКИЙ виртуальный стол даёт масштаб МЕНЬШЕ единицы, и он считается по арифметике;
//    3) РАСХОЖДЕНИЕ ПРОПОРЦИИ распознаётся отдельно — тогда единый масштаб не поможет;
//    4) ВЫРОЖДЕННЫЕ габариты (ноль, отрицательные) не дают деления на ноль и бесконечностей;
//    5) СТРОКА ЖУРНАЛА называет все три числа — по ней видно, ЧТО подбирать.
//
//  ЧЕМ ЭТО ПОЛЕЗНО В СУББОТУ. Если в шлеме виртуальный стол окажется крупнее реального, у нас уже
//  есть посчитанный масштаб и понимание, СРАБОТАЕТ ли он. Без этого пришлось бы подбирать вслепую.
// ================================================================================================

using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Расчёт масштаба виртуального стола под реальный.</summary>
    public class KvMrScaleFitTests
    {
        // Реальный стол владельца (решение [D7]): 100 × 70 см, высота 78 см.
        private const float RealWidthM = 1.00f;
        private const float RealDepthM = 0.70f;
        private const float RealHeightM = 0.78f;

        // Виртуальная столешница проекта (замер 02.10.2026): 14,4 × 9,6 юнита при высоте 0,98.
        private const float VirtualWidth = 14.4f;
        private const float VirtualDepth = 9.6f;
        private const float VirtualHeight = 0.98f;

        // ---------------------------------------------------------------- 1. МЕТРИЧЕСКИЙ МИР

        /// <summary>
        /// МЕТРИЧЕСКИЙ МИР ДАЁТ МАСШТАБ 1. Если виртуальный стол уже совпадает с реальным, ни один
        /// коэффициент не нужен и применять его нельзя: иначе «исправление» испортит рабочую сцену.
        /// </summary>
        [Test]
        public void MetricWorld_NeedsScaleOfOne()
        {
            KvScaleFit fit = KvXrMrPlacement.FitTableScale(1f, 0.7f, 0.78f, RealWidthM, RealDepthM, RealHeightM);

            Assert.AreEqual(1f, fit.scale, 1e-4f, "совпадающие габариты дают масштаб 1");
            Assert.AreEqual(1f, fit.byWidth, 1e-4f);
            Assert.AreEqual(1f, fit.byDepth, 1e-4f);
            Assert.AreEqual(1f, fit.byHeight, 1e-4f);
            Assert.IsTrue(fit.proportionMatches, "при совпадающих габаритах пропорция совпадает");
        }

        // ---------------------------------------------------------------- 2. РЕАЛЬНЫЙ СЛУЧАЙ

        /// <summary>
        /// РЕАЛЬНЫЙ СЛУЧАЙ ПРОЕКТА: виртуальный стол 14,4 юнита против реального 1,0 м.
        /// Масштаб = 1,0 / 14,4 = 0,0694 — то есть виртуальный мир надо УМЕНЬШИТЬ почти в 14 раз.
        ///
        /// Число посчитано здесь заново и записано вместе с выкладкой: если формула изменится,
        /// расхождение будет видно сразу, а не после ручного подбора в шлеме.
        /// </summary>
        [Test]
        public void ProjectTable_NeedsScaleAroundFourteenfoldReduction()
        {
            KvScaleFit fit = KvXrMrPlacement.FitTableScale(VirtualWidth, VirtualDepth, VirtualHeight,
                RealWidthM, RealDepthM, RealHeightM);

            Assert.AreEqual(1f / 14.4f, fit.scale, 1e-4f,
                "масштаб по ширине = 1,0 / 14,4 = 0,0694; получено " + fit.scale.ToString("0.0000"));
            Assert.Less(fit.scale, 0.1f,
                "виртуальный стол обязан УМЕНЬШАТЬСЯ почти в 14 раз, иначе он не влезет в комнату");
            Assert.Greater(fit.scale, 0f, "масштаб обязан быть положительным");

            // Пропорция: 0,70 / 9,6 = 0,0729 против 0,0694 по ширине. Отношение расходится на 5 %,
            // граница — ровно на краю. Это и показывает тест: у реального стола пропорция 10:7,
            // у виртуального 14,4:9,6 = 1,5:1 (3:2), то есть ФОРМА отличается.
            float ratio = fit.byDepth / fit.byWidth;
            Assert.AreEqual(1.05f, ratio, 0.01f,
                "отношение масштабов по глубине и ширине ≈ 1,05 — форма столов различается на 5 %");
        }

        // ---------------------------------------------------------------- 3. ПРОПОРЦИЯ

        /// <summary>
        /// СОВПАДАЮЩАЯ ПРОПОРЦИЯ РАСПОЗНАЁТСЯ. Виртуальный стол 2:1 и реальный 2:1 отличаются только
        /// размером — единый масштаб приведёт их друг к другу точно.
        /// </summary>
        [Test]
        public void MatchingProportion_IsRecognised()
        {
            KvScaleFit fit = KvXrMrPlacement.FitTableScale(2f, 1f, 0.8f, 1f, 0.5f, 0.4f);

            Assert.AreEqual(0.5f, fit.scale, 1e-4f, "масштаб = 1 / 2 = 0,5");
            Assert.AreEqual(0.5f, fit.byDepth, 1e-4f, "по глубине тот же 0,5 — пропорция 2:1 совпала");
            Assert.IsTrue(fit.proportionMatches,
                "при одинаковой пропорции 2:1 единый масштаб применим");
        }

        /// <summary>
        /// РАСХОЖДЕНИЕ ПРОПОРЦИИ ВИДНО. Виртуальный стол 2:1, реальный 1:1 — масштаб по ширине 0,5,
        /// по глубине 1,0. Единым числом это не привести, и признак обязан это сказать: иначе
        /// в шлеме будут подбирать коэффициент, который никогда не сойдётся.
        /// </summary>
        [Test]
        public void DifferentProportion_IsFlagged()
        {
            KvScaleFit fit = KvXrMrPlacement.FitTableScale(2f, 1f, 0.8f, 1f, 1f, 0.4f);

            Assert.AreEqual(0.5f, fit.byWidth, 1e-4f);
            Assert.AreEqual(1.0f, fit.byDepth, 1e-4f);
            Assert.IsFalse(fit.proportionMatches,
                "пропорции разные (2:1 против 1:1) — единый масштаб не поможет, признак обязан это показать");
        }

        // ---------------------------------------------------------------- 4. ВЫРОЖДЕННЫЕ ВХОДЫ

        /// <summary>
        /// НУЛЕВЫЕ И ОТРИЦАТЕЛЬНЫЕ ГАБАРИТЫ НЕ ДАЮТ ДЕЛЕНИЯ НА НОЛЬ. Такой вход — ошибка данных
        /// (модель не загрузилась), и она не должна давать `Infinity`/`NaN`: такое число потом
        /// попадёт в масштаб сцены и «схлопнет» мир.
        /// </summary>
        [Test]
        public void DegenerateSizes_DoNotProduceInfinity()
        {
            KvScaleFit fit = KvXrMrPlacement.FitTableScale(0f, -1f, 0f, 1f, 0.7f, 0.78f);

            Assert.IsFalse(float.IsInfinity(fit.byWidth) || float.IsNaN(fit.byWidth),
                "нулевая ширина не должна давать бесконечность");
            Assert.AreEqual(0f, fit.byWidth, 1e-6f, "при нулевом знаменателе отношение = 0");
            Assert.IsFalse(fit.proportionMatches,
                "при вырожденных размерах о совпадении пропорции говорить нельзя");
        }

        // ---------------------------------------------------------------- 5. СТРОКА ЖУРНАЛА

        /// <summary>
        /// СТРОКА НАЗЫВАЕТ ВСЕ ТРИ ЧИСЛА. По ней в шлеме видно, ЧТО подбирать: если бы в строке было
        /// только одно число, расхождение по высоте осталось бы незамеченным.
        /// </summary>
        [Test]
        public void DescribeTableScale_ListsAllThreeNumbers()
        {
            KvScaleFit fit = KvXrMrPlacement.FitTableScale(VirtualWidth, VirtualDepth, VirtualHeight,
                RealWidthM, RealDepthM, RealHeightM);
            string text = KvXrMrPlacement.DescribeTableScale(fit);

            StringAssert.Contains("по ширине", text, "в строке нет масштаба по ширине: " + text);
            StringAssert.Contains("по глубине", text, "в строке нет масштаба по глубине: " + text);
            StringAssert.Contains("по высоте", text, "в строке нет масштаба по высоте: " + text);
            StringAssert.Contains("0.069", text, "в строке нет самого числа масштаба: " + text);
            // Проверяем именно ДЕСЯТИЧНУЮ запятую (между цифрами), а не запятую в тексте:
            // «пропорция совпадает, единый масштаб» — законная запятая, и валить тест из-за неё
            // значило бы запретить нормальную пунктуацию.
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, @"\d,\d"),
                "в строке появилась десятичная запятая вместо точки: " + text);
        }

        /// <summary>
        /// ПРИ РАСХОЖДЕНИИ ПРОПОРЦИИ СТРОКА ГОВОРИТ ПРЯМО. Молчаливое «единый масштаб 0,069» увело бы
        /// наладчика в подбор коэффициента, который не сойдётся никогда.
        /// </summary>
        [Test]
        public void DescribeTableScale_WarnsAboutProportionMismatch()
        {
            KvScaleFit fit = KvXrMrPlacement.FitTableScale(2f, 1f, 0.8f, 1f, 1f, 0.4f);
            string text = KvXrMrPlacement.DescribeTableScale(fit);

            StringAssert.Contains("ПРОПОРЦИЯ НЕ СОВПАДАЕТ", text,
                "строка обязана предупредить, что единым масштабом не привести: " + text);
        }
    }
}
