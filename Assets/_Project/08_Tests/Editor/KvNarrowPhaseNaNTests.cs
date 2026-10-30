// ================================================================================================
//  KvNarrowPhaseNaNTests.cs — EditMode-тесты НЕЧИСЛОВЫХ ДАННЫХ В GJK (сессия S44, урок E-21).
//
//  ОТКУДА ЗАДАЧА. `NarrowPhase.Intersect` — это GJK: проверка столкновения двух тел. Её результат
//  решает, поедет робот или планировщик откажет с «препятствием». Отсюда — цена ошибки.
//
//  ЧТО НАЙДЕНО 30.10.2026. `Support` вызывает `dir.normalized`. Для направления с NaN результат
//  тоже NaN — и дальше ВСЕ сравнения в GJK дают `false`:
//    • `dir.sqrMagnitude < Eps` — не срабатывает (NaN не меньше);
//    • `Vector3.Dot(p, dir) < 0f` — не срабатывает, значит РАЗДЕЛЯЮЩАЯ ОСЬ НЕ ОБНАРУЖИВАЕТСЯ;
//    • цикл доходит до `return true` — «считаем контактом (консервативно)».
//
//  Итог: тела, которые нигде не соприкасаются, объявляются столкнувшимися. Оператор видит отказ
//  планировщика и ищет причину в роботе, а причина — в нечисловых данных.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ:
//    1) примитив с NaN-координатой НЕ объявляется столкнувшимся;
//    2) примитив с бесконечностью — тоже;
//    3) НОРМАЛЬНЫЕ тела по-прежнему работают: пересекающиеся — да, разделённые — нет;
//    4) защита не «глушит» GJK: разделённые тела разных форм по-прежнему различимы.
// ================================================================================================

using NUnit.Framework;
using TrajectoryCore;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Нечисловые данные в GJK: отказ вместо ложного препятствия.</summary>
    public class KvNarrowPhaseNaNTests
    {
        /// <summary>
        /// ПРИМИТИВ С NaN НЕ СТАЛКИВАЕТСЯ. Главный тест: до исправления GJK «находил» контакт,
        /// потому что ни одно сравнение с NaN не срабатывало.
        /// </summary>
        [Test]
        public void NaNPrimitive_DoesNotCollide()
        {
            var good = Prim.Sphere(Vector3.zero, 1f);
            var nan = Prim.Sphere(new Vector3(float.NaN, 0f, 0f), 1f);

            Assert.IsFalse(NarrowPhase.Intersect(nan, good),
                "тело с NaN признано столкнувшимся — планировщик откажет с ЛОЖНЫМ препятствием");
            Assert.IsFalse(NarrowPhase.Intersect(good, nan),
                "тело с NaN (вторым аргументом) признано столкнувшимся");

            // Сфера с NaN-РАДИУСОМ — тоже нечисловые данные.
            var nanRadius = Prim.Sphere(Vector3.zero, float.NaN);
            Assert.IsFalse(NarrowPhase.Intersect(nanRadius, good),
                "сфера с NaN-радиусом признана столкнувшейся");
        }

        /// <summary>
        /// БЕСКОНЕЧНОСТЬ В КООРДИНАТЕ — тоже отказ. После вычитания она даёт NaN, и GJK получает
        /// тот же тупик.
        /// </summary>
        [Test]
        public void InfinitePrimitive_DoesNotCollide()
        {
            var good = Prim.Sphere(Vector3.zero, 1f);
            var inf = Prim.Sphere(new Vector3(float.PositiveInfinity, 0f, 0f), 1f);

            Assert.IsFalse(NarrowPhase.Intersect(inf, good),
                "тело с бесконечностью признано столкнувшимся");
        }

        /// <summary>
        /// НАСТОЯЩЕЕ СТОЛКНОВЕНИЕ ПО-ПРЕЖНЕМУ ОБНАРУЖИВАЕТСЯ. Обратная ошибка («отсекаем всё»)
        /// была бы опаснее исходной: робот поехал бы сквозь препятствие.
        /// </summary>
        [Test]
        public void RealCollisionIsStillDetected()
        {
            var a = Prim.Sphere(Vector3.zero, 1f);
            var b = Prim.Sphere(new Vector3(0.5f, 0f, 0f), 1f);

            Assert.IsTrue(NarrowPhase.Intersect(a, b),
                "две пересекающиеся сферы больше не считаются столкнувшимися — робот поедет сквозь");
        }

        /// <summary>
        /// РАЗДЕЛЁННЫЕ ТЕЛА ПО-ПРЕЖНЕМУ РАЗЛИЧИМЫ. Без этого проверка «нет столкновения» ничего не
        /// доказывает.
        /// </summary>
        [Test]
        public void SeparatedBodiesAreStillDistinguishable()
        {
            var a = Prim.Sphere(Vector3.zero, 1f);
            var far = Prim.Sphere(new Vector3(10f, 0f, 0f), 1f);

            Assert.IsFalse(NarrowPhase.Intersect(a, far),
                "разделённые тела считаются столкнувшимися — ложное препятствие");
        }

        /// <summary>
        /// ЗАЩИТА РАБОТАЕТ И ДЛЯ СЛОЖНЫХ ФОРМ. Оболочка с NaN в ОДНОЙ вершине — реалистичный случай
        /// (повреждённая модель): проверяется, что она отсекается, а целая оболочка работает.
        /// </summary>
        [Test]
        public void HullWithNaNVvertex_IsRejected()
        {
            var goodHull = Prim.Hull(new[]
            {
                new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f),
                new Vector3(1f, 1f, -1f), new Vector3(-1f, 1f, -1f),
                new Vector3(-1f, -1f, 1f), new Vector3(1f, -1f, 1f),
                new Vector3(1f, 1f, 1f), new Vector3(-1f, 1f, 1f),
            });

            var brokenHull = Prim.Hull(new[]
            {
                new Vector3(-1f, -1f, -1f), new Vector3(float.NaN, -1f, -1f),
                new Vector3(1f, 1f, -1f), new Vector3(-1f, 1f, -1f),
            });

            Assert.IsFalse(NarrowPhase.Intersect(brokenHull, goodHull),
                "оболочка с NaN в ОДНОЙ вершине признана столкнувшейся — повреждённая модель " +
                "остановит планирование ложным препятствием");

            // А целая оболочка с той же сферой — работает как обычно.
            Assert.IsTrue(NarrowPhase.Intersect(goodHull, Prim.Sphere(Vector3.zero, 0.5f)),
                "целая оболочка и сфера внутри неё больше не считаются столкнувшимися");
        }
    }
}
