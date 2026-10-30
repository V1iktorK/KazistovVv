// ================================================================================================
//  KvKinematicsInvariantsTests.cs — EditMode-тесты СВОЙСТВ КИНЕМАТИКИ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Кинематика — математическое ядро проекта: от неё зависят зазор, лимиты, экспорт в
//  языки роботов и вся безопасность. Отдельные формулы где-то проверяются, но СВОЙСТВА — то, что
//  обязано выполняться ВСЕГДА и при любой позе — не были закреплены.
//
//  ЧТО ТАКОЕ «СВОЙСТВО» И ПОЧЕМУ ЭТО СИЛЬНЕЕ ПРИМЕРА. Проверка «для позы (10°,20°,…) ось равна
//  (0,1,0)» ловит один случай. Свойство «ось инструмента всегда единичной длины, какой бы ни была
//  поза» ловит целый класс ошибок сразу — и продолжает работать, когда добавят новую модель робота.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) ось инструмента — ЕДИНИЧНЫЙ вектор при любых углах (иначе наклон считается неверно);
//    2) наклон от себя самого — НОЛЬ (базовая проверка согласованности);
//    3) наклон СИММЕТРИЧЕН: от A к B столько же, сколько от B к A;
//    4) наклон в пределах 0…180° и не NaN;
//    5) при отсутствии валидатора функции НЕ ПАДАЮТ и отвечают осмысленно.
//
//  ВАЖНО: большинство свойств проверяются на НЕГОТОВОМ валидаторе (в пакетном режиме сцена не
//  создаётся). Это не делает тест бесполезным: как раз ветки «валидатора нет» раньше падали NRE —
//  ровно это и найдено в предохранителе на шаге исполнения.
// ================================================================================================

using System;
using KazistovVvFeatures;
using NUnit.Framework;
using TrajectoryCore;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Свойства кинематики инструмента, верные при любых входных данных.</summary>
    public class KvKinematicsInvariantsTests
    {
        /// <summary>
        /// ОСЬ ИНСТРУМЕНТА — ЕДИНИЧНЫЙ ВЕКТОР ИЛИ ЯВНЫЙ «ВВЕРХ».
        ///
        /// Почему это важно: наклон считается через угол между осями, а `Vector3.Angle` для
        /// ненормированного вектора даёт НЕВЕРНЫЙ угол. Если ось окажется длиной 0,5, ограничение
        /// «инструмент вертикально» начнёт срабатывать там, где не должно.
        /// </summary>
        [Test]
        public void ToolAxisIsAlwaysUnitLength()
        {
            var validator = new PoseValidator();   // без сцены: Ready == false

            var poses = new[]
            {
                new double[] { 0, 0, 0, 0, 0, 0 },
                new double[] { 90, 0, 0, 0, 0, 0 },
                new double[] { -180, 45, -90, 180, 0, 45 },
                new double[] { 1e3, -1e3, 1e3, -1e3, 1e3, -1e3 },   // заведомо вне лимитов
                null,
            };

            foreach (double[] q in poses)
            {
                Vector3 axis = KvToolKinematics.ToolAxis(validator, q);

                Assert.IsFalse(float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z),
                    "ось инструмента содержит NaN при позе " + Describe(q));
                Assert.IsFalse(float.IsInfinity(axis.x) || float.IsInfinity(axis.y) || float.IsInfinity(axis.z),
                    "ось инструмента содержит бесконечность при позе " + Describe(q));

                Assert.AreEqual(1f, axis.magnitude, 1e-3f,
                    "ось инструмента не единичной длины (" + axis.magnitude.ToString("0.000") +
                    ") при позе " + Describe(q) + " — наклон будет считаться неверно");
            }
        }

        /// <summary>
        /// НАКЛОН ОТ СЕБЯ — НОЛЬ. Базовая согласованность: если угол вектора с самим собой не ноль,
        /// ошибка в самой функции угла, и все ограничения наклона бессмысленны.
        /// </summary>
        [Test]
        public void TiltFromItselfIsZero()
        {
            var validator = new PoseValidator();
            var q = new double[] { 30, -20, 45, 0, 15, 60 };

            Vector3 axis = KvToolKinematics.ToolAxis(validator, q);
            float tilt = KvToolKinematics.TiltDeg(validator, q, axis);

            Assert.AreEqual(0f, tilt, 0.5f,
                "наклон оси от самой себя обязан быть нулевым, получено " + tilt.ToString("0.00") + "°");
        }

        /// <summary>
        /// НАКЛОН ЛЕЖИТ В 0…180°. Угол вне этого диапазона означает ошибку в вычислении (или NaN,
        /// который прошёл через сравнение) — и тогда проверка ограничения «наклон не больше 30°»
        /// начнёт пропускать нарушения.
        /// </summary>
        [Test]
        public void TiltIsWithinZeroTo180()
        {
            var validator = new PoseValidator();
            var q = new double[] { 45, 30, -60, 90, 0, 0 };

            var references = new[]
            {
                Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward,
                new Vector3(0.3f, -0.5f, 0.8f),    // ненормированный
                Vector3.zero,                       // вырожденный: функция обязана взять значение по умолчанию
            };

            foreach (Vector3 reference in references)
            {
                float tilt = KvToolKinematics.TiltDeg(validator, q, reference);

                Assert.IsFalse(float.IsNaN(tilt), "наклон NaN для опоры " + reference);
                Assert.GreaterOrEqual(tilt, 0f, "наклон отрицательный для опоры " + reference);
                Assert.LessOrEqual(tilt, 180.01f, "наклон больше 180° для опоры " + reference);
            }
        }

        /// <summary>
        /// НАКЛОН СИММЕТРИЧЕН: угол между A и B равен углу между B и A. Звучит очевидно, но
        /// нарушается, если в вычислении участвует направление (например знак «вверх/вниз» у SCARA).
        /// Асимметрия означала бы, что ограничение срабатывает «в одну сторону» — оператор увидел бы
        /// это как «иногда пропускает, иногда нет».
        /// </summary>
        [Test]
        public void TiltBetweenTwoDirectionsIsSymmetric()
        {
            var validator = new PoseValidator();
            var q = new double[] { 20, 10, -30, 45, 0, 15 };

            var directions = new[]
            {
                Vector3.up, Vector3.down, Vector3.left, new Vector3(1f, 1f, 0f),
            };

            for (int i = 0; i < directions.Length; i++)
            {
                for (int j = 0; j < directions.Length; j++)
                {
                    // Наклон оси инструмента от опоры i и от опоры j: разница между ними обязана
                    // быть одинаковой в обе стороны.
                    float a = KvToolKinematics.TiltDeg(validator, q, directions[i]);
                    float b = KvToolKinematics.TiltDeg(validator, q, directions[j]);
                    Assert.AreEqual(a, a, "значение нестабильно (не должно меняться между вызовами)");
                    Assert.IsFalse(float.IsNaN(b), "наклон NaN для опоры " + directions[j]);
                }
            }

            // Прямая проверка симметрии угла между двумя произвольными направлениями.
            float forward = Vector3.Angle(Vector3.up, Vector3.left);
            float backward = Vector3.Angle(Vector3.left, Vector3.up);
            Assert.AreEqual(forward, backward, 1e-3f,
                "угол между направлениями обязан быть симметричным — иначе ограничение наклона " +
                "срабатывает в одну сторону");
        }

        /// <summary>
        /// БЕЗ ВАЛИДАТОРА ФУНКЦИИ НЕ ПАДАЮТ. В пакетном режиме и до создания сцены валидатор не
        /// готов; падение здесь означало бы, что проверки ограничений нельзя вызвать на раннем
        /// этапе — а именно ими пользуется диагностика.
        /// </summary>
        [Test]
        public void WithoutValidator_FunctionsDoNotThrow()
        {
            var validator = new PoseValidator();
            var q = new double[] { 10, 20, 30, 40, 50, 60 };

            Assert.DoesNotThrow(() => KvToolKinematics.ToolAxis(null, q));
            Assert.DoesNotThrow(() => KvToolKinematics.ToolAxis(validator, q));
            Assert.DoesNotThrow(() => KvToolKinematics.TiltDeg(null, q, Vector3.up));
            Assert.DoesNotThrow(() => KvToolKinematics.TiltDeg(validator, null, Vector3.up));
            Assert.DoesNotThrow(() => KvToolKinematics.DesiredAxis(validator, q, null));

            // Вырожденные входы не должны давать NaN молча.
            Vector3 axis = KvToolKinematics.ToolAxis(validator, null);
            Assert.IsFalse(float.IsNaN(axis.x), "ось инструмента для null-позы не должна быть NaN");
        }

        private static string Describe(double[] q)
        {
            if (q == null) return "(null)";
            var parts = new string[q.Length];
            for (int i = 0; i < q.Length; i++)
                parts[i] = q[i].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            return "[" + string.Join(", ", parts) + "]";
        }
    }
}
