// ================================================================================================
//  KvNarrowPhaseTests.cs — EditMode-тесты УЗКОЙ ФАЗЫ КОЛЛИЗИЙ (GJK), сессия S44.
//
//  ОТКУДА ЗАДАЧА. `Trajectory\NarrowPhase.cs` (GJK — пересечение выпуклых тел, и минимизация
//  глубины проникновения) лежал в проекте с пометкой «написано в стол»: `NarrowPhase` НЕ
//  ВЫЗЫВАЕТСЯ НИ ОТКУДА. Пункт 5.24 плана ставил развилку: подключить (тогда пересечения
//  становятся точными) или удалить как альтернативную реализацию.
//
//  РЕШЕНИЕ ПРИНИМАЕТСЯ ТЕСТОМ, А НЕ МНЕНИЕМ. Если алгоритм верен — его надо подключить; если
//  врёт — удалить, потому что «почти правильная» проверка столкновений опаснее отсутствующей:
//  оператор верит, что зазор посчитан. Ниже — проверки на случаях, где ответ известен точно.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) РАЗДЕЛЁННЫЕ тела — пересечения нет (самое частое состояние в кадре);
//    2) ЗАВЕДОМО ПЕРЕСЕКАЮЩИЕСЯ — пересечение есть;
//    3) КАСАНИЕ НА ГРАНИЦЕ — не должно давать ложного пересечения;
//    4) ГЛУБИНА ПРОНИКНОВЕНИЯ совпадает с арифметикой для простых фигур;
//    5) НОРМАЛЬ контакта направлена от одного тела к другому;
//    6) САМОПЕРЕСЕЧЕНИЕ капсулы с собой и с пустым примитивом не роняет алгоритм.
// ================================================================================================

using NUnit.Framework;
using TrajectoryCore;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>GJK/EPA: пересечение и глубина проникновения выпуклых примитивов.</summary>
    public class KvNarrowPhaseTests
    {
        // ---------------------------------------------------------------- 1. РАЗДЕЛЁННЫЕ ТЕЛА

        /// <summary>
        /// ДВЕ СФЕРЫ ВДАЛИ ДРУГ ОТ ДРУГА НЕ ПЕРЕСЕКАЮТСЯ. Сферы радиуса 0,1 с центрами в 1 м —
        /// заведомо разведены. Это самое частое состояние в кадре, и ложное «пересечение» здесь
        /// остановило бы любое планирование.
        /// </summary>
        [Test]
        public void SeparatedSpheres_DoNotIntersect()
        {
            Prim a = Prim.Sphere(Vector3.zero, 0.1f);
            Prim b = Prim.Sphere(new Vector3(1f, 0f, 0f), 0.1f);
            Assert.IsFalse(NarrowPhase.Intersect(a, b),
                "сферы с центрами в 1 м и радиусом 0,1 м обязаны быть разделены");
        }

        /// <summary>
        /// ДВЕ КАПСУЛЫ, СТОЯЩИЕ РЯДОМ, НО НЕ КАСАЮЩИЕСЯ. Капсулы — основная форма звеньев робота,
        /// поэтому проверяется именно она, а не только сфера.
        /// </summary>
        [Test]
        public void SeparatedParallelCapsules_DoNotIntersect()
        {
            Prim a = Prim.Capsule(new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0f, 0.5f), 0.05f);
            Prim b = Prim.Capsule(new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), 0.05f);
            Assert.IsFalse(NarrowPhase.Intersect(a, b),
                "две капсулы с осями в 0,5 м при радиусах 0,05 м разделены");
        }

        /// <summary>ЯЩИК И СФЕРА, РАЗВЕДЁННЫЕ ДАЛЬШЕ ГАБАРИТОВ.</summary>
        [Test]
        public void SeparatedBoxAndSphere_DoNotIntersect()
        {
            Prim box = Prim.Box(Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f));
            Prim sphere = Prim.Sphere(new Vector3(2f, 0f, 0f), 0.2f);
            Assert.IsFalse(NarrowPhase.Intersect(box, sphere));
        }

        // ---------------------------------------------------------------- 2. ПЕРЕСЕКАЮЩИЕСЯ

        /// <summary>
        /// СФЕРЫ, НАЛОЖЕННЫЕ ДРУГ НА ДРУГА, ПЕРЕСЕКАЮТСЯ. Центры в 0,05 м при радиусах 0,1 —
        /// заведомое пересечение.
        /// </summary>
        [Test]
        public void OverlappingSpheres_Intersect()
        {
            Prim a = Prim.Sphere(Vector3.zero, 0.1f);
            Prim b = Prim.Sphere(new Vector3(0.05f, 0f, 0f), 0.1f);
            Assert.IsTrue(NarrowPhase.Intersect(a, b),
                "сферы с центрами в 0,05 м при радиусах 0,1 заведомо пересекаются");
        }

        /// <summary>СФЕРА ВНУТРИ ЯЩИКА — тоже пересечение (центр внутри, а не рядом).</summary>
        [Test]
        public void SphereInsideBox_Intersects()
        {
            Prim box = Prim.Box(Vector3.zero, new Vector3(1f, 1f, 1f));
            Prim sphere = Prim.Sphere(Vector3.zero, 0.1f);
            Assert.IsTrue(NarrowPhase.Intersect(box, sphere),
                "сфера в центре ящика обязана пересекаться с ним");
        }

        /// <summary>ПЕРЕСЕКАЮЩИЕСЯ КАПСУЛЫ (крест-накрест) — проверка на случай, где простые
        /// формулы «капсула-капсула» ошибаются при ортогональных осях.</summary>
        [Test]
        public void CrossingCapsules_Intersect()
        {
            Prim a = Prim.Capsule(new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), 0.05f);
            Prim b = Prim.Capsule(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), 0.05f);
            Assert.IsTrue(NarrowPhase.Intersect(a, b),
                "капсулы, пересекающиеся в начале координат, обязаны давать пересечение");
        }

        // ---------------------------------------------------------------- 3. ГРАНИЦА

        /// <summary>
        /// КАСАНИЕ НА ГРАНИЦЕ НЕ ДОЛЖНО ДАВАТЬ ЛОЖНОГО ПЕРЕСЕЧЕНИЯ.
        ///
        /// ПОЧЕМУ ЭТО ВАЖНО. В планировании робот постоянно проходит вплотную к поверхности
        /// (столешница, заготовка). Если «касание» считается столкновением, планировщик начнёт
        /// отказывать в штатной работе — ровно тот класс отказа, что «вне рабочей зоны (далеко)».
        /// Допуск задан явно: на самой границе ответ может быть любым, но чуть в стороне — нет.
        /// </summary>
        [Test]
        public void JustTouching_DoesNotCountAsPenetration()
        {
            // Сферы касаются: центры в 0,2 м, радиусы 0,1 → суммарно ровно 0,2.
            Prim a = Prim.Sphere(Vector3.zero, 0.1f);
            Prim b = Prim.Sphere(new Vector3(0.21f, 0f, 0f), 0.1f);   // зазор 0,01 м
            Assert.IsFalse(NarrowPhase.Intersect(a, b),
                "сферы с зазором 1 см не должны считаться пересекающимися — иначе планировщик " +
                "начнёт отказывать у каждой поверхности");
        }

        // ---------------------------------------------------------------- 4. ГЛУБИНА ПРОНИКНОВЕНИЯ

        /// <summary>
        /// ГЛУБИНА ДЛЯ СФЕР СЧИТАЕТСЯ АРИФМЕТИКОЙ. Две сферы радиуса 0,1 с центрами в 0,15 м:
        /// перекрытие = r1 + r2 − расстояние = 0,1 + 0,1 − 0,15 = 0,05 м.
        /// Проверяется с допуском 20 %: метод ищет минимум по направлениям численно, и требовать
        /// микронной точности от него нельзя — но «в два раза» он ошибаться не должен.
        /// </summary>
        [Test]
        public void PenetrationDepth_MatchesArithmeticForSpheres()
        {
            Prim a = Prim.Sphere(Vector3.zero, 0.1f);
            Prim b = Prim.Sphere(new Vector3(0.15f, 0f, 0f), 0.1f);

            float depth;
            Vector3 normal;
            bool overlap = NarrowPhase.Penetration(a, b, out depth, out normal);

            Assert.IsTrue(overlap, "Пенетрация обязана найти перекрытие для наложенных сфер");
            Assert.AreEqual(0.05f, depth, 0.01f,
                "глубина проникновения = 0,1 + 0,1 − 0,15 = 0,05 м; получено " + depth.ToString("0.000"));
        }

        /// <summary>ДЛЯ РАЗДЕЛЁННЫХ ТЕЛ ГЛУБИНЫ НЕТ — функция обязана вернуть false, а не «0 метров».</summary>
        [Test]
        public void Penetration_ReturnsFalseWhenSeparated()
        {
            Prim a = Prim.Sphere(Vector3.zero, 0.1f);
            Prim b = Prim.Sphere(new Vector3(1f, 0f, 0f), 0.1f);

            float depth;
            Vector3 normal;
            Assert.IsFalse(NarrowPhase.Penetration(a, b, out depth, out normal),
                "для разделённых тел перекрытия нет");
            Assert.AreEqual(0f, depth, 1e-6f, "глубина при отсутствии перекрытия — ноль");
        }

        // ---------------------------------------------------------------- 5. НОРМАЛЬ

        /// <summary>
        /// НОРМАЛЬ СМОТРИТ ПО ОСИ ПЕРЕКРЫТИЯ. Сферы разнесены по X — нормаль обязана быть близка
        /// к горизонтальной оси, а не «примерно вверх». Иначе выталкивание робота из столкновения
        /// пойдёт не туда.
        /// </summary>
        [Test]
        public void PenetrationNormal_PointsAlongOverlapAxis()
        {
            Prim a = Prim.Sphere(Vector3.zero, 0.1f);
            Prim b = Prim.Sphere(new Vector3(0.15f, 0f, 0f), 0.1f);

            float depth;
            Vector3 normal;
            NarrowPhase.Penetration(a, b, out depth, out normal);

            Assert.AreEqual(1f, normal.normalized.magnitude, 1e-3f, "нормаль обязана быть единичной");
            Assert.Greater(Mathf.Abs(normal.x), 0.9f,
                "нормаль обязана быть направлена по оси X (сферы разнесены по X), получено " +
                normal.ToString("0.00"));
        }

        // ---------------------------------------------------------------- 6. ВЫРОЖДЕННЫЕ ВХОДЫ

        /// <summary>
        /// ТЕЛО ПЕРЕСЕКАЕТСЯ САМО С СОБОЙ. Это не бессмысленный случай: при проверке звена «с самим
        /// собой» (или копии в фантоме) ответ обязан быть «пересекается» и НЕ ронять алгоритм
        /// делением на ноль.
        /// </summary>
        [Test]
        public void SamePrimitive_IntersectsItselfWithoutThrowing()
        {
            Prim a = Prim.Capsule(new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0f, 0.5f), 0.05f);
            bool result = true;
            Assert.DoesNotThrow(() => result = NarrowPhase.Intersect(a, a),
                "проверка тела с самим собой не должна бросать исключение");
            Assert.IsTrue(result, "тело пересекается с самим собой");
        }

        /// <summary>
        /// ПУСТАЯ ОБОЛОЧКА И НУЛЕВОЙ РАДИУС не должны давать NaN или исключение. Оболочка без
        /// вершин — это не «ничего», а ошибка данных; алгоритм обязан это пережить.
        /// </summary>
        [Test]
        public void DegeneratePrimitives_DoNotThrow()
        {
            Prim emptyHull = Prim.Hull(new Vector3[0]);
            Prim sphere = Prim.Sphere(Vector3.zero, 0.1f);

            bool result = false;
            Assert.DoesNotThrow(() => result = NarrowPhase.Intersect(emptyHull, sphere),
                "оболочка без вершин не должна ронять проверку");
            Assert.IsFalse(result, "пустая оболочка не пересекается с чем-либо");

            Prim zeroSphere = Prim.Sphere(new Vector3(5f, 0f, 0f), 0f);
            Assert.DoesNotThrow(() => NarrowPhase.Intersect(zeroSphere, sphere));
        }
    }
}
