// ================================================================================================
//  KvIntegrationBehaviourTests.cs — ПОВЕДЕНЧЕСКИЕ тесты папки `01_Scripts\Integration` (P4-8).
//
//  ОТКУДА ЗАДАЧА. Папка `Integration` (предохранитель движения, выбор активного робота, маркер
//  цели) не имела ни одного теста поведения: всё, что про неё было написано, проверяло ИСХОДНИК
//  поиском строк. Такой тест ломается от переформулировки комментария и, что хуже, ПРОХОДИТ на
//  сломанном коде: строка «distance < 1e-4f» может остаться, а условие — перестать работать.
//
//  ЧТО ЗДЕСЬ. Правила, которые раньше жили внутри методов, привязанных к сцене, вынесены в ЧИСТЫЕ
//  статические функции рантайм-кода (`CollisionGuard.ShouldCastRay`, `RobotSelector.ResolveActiveIndex`,
//  `TargetMarker.ColorFor`), и тесты вызывают ИХ — то есть проверяют результат, а не текст.
//
//  ГРАНИЦА ЧЕСТНОСТИ: `CanMoveTo` требует сцены с коллайдерами и роботом, поэтому проверяется
//  РЕШАЮЩАЯ часть (кастовать ли луч вообще) плюс настоящее поведение компонента без робота —
//  «нет данных, значит движение пропускается».
// ================================================================================================

using NUnit.Framework;
using UnityEngine;

// P1-15: `RobotSelector` помечен [Obsolete] (единственный путь выбора робота — FreeFlyCameraController/
// дерево). Гасим предупреждение точечно: тесты проверяют ПРАВИЛО выбора индекса, и это честнее, чем
// убрать проверку из-за предупреждения компилятора.
#pragma warning disable 0612, 0618

namespace KazistovVvTests
{
    /// <summary>Поведение папки Integration: предохранитель, выбор робота, маркер цели.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvIntegrationBehaviourTests
    {
        // ---------------------------------------------------------------- CollisionGuard

        /// <summary>
        /// ГРАНИЦА «КАСТОВАТЬ ЛУЧ ИЛИ НЕТ» (P3-11). Главный тест предохранителя.
        ///
        /// ЧТО БЫЛО. Если цель совпадала с TCP, `direction` получался нулевым, `Physics.Raycast`
        /// с нулевым направлением писал ошибку в консоль и возвращал `false` — предохранитель МОЛЧА
        /// отвечал «можно» именно в вырожденном случае, то есть проверка «препятствие на пути» не
        /// выполнялась вовсе.
        ///
        /// ПРОВЕРЯЮТСЯ ОБЕ СТОРОНЫ ГРАНИЦЫ, иначе тест был бы обойдён «оптимизацией» до постоянного
        /// `true` (тогда предохранитель перестал бы проверять что-либо вообще).
        /// Граница 1e-4 м ВКЛЮЧИТЕЛЬНА: ровно на пороге путь уже считается настоящим.
        /// </summary>
        [Test]
        public void CollisionGuard_ZeroPathDoesNotCastRay()
        {
            Assert.IsFalse(CollisionGuard.ShouldCastRay(0f),
                "цель совпала с TCP: луч нулевой длины кастовать нельзя — Unity пишет ошибку, " +
                "а предохранитель молча отвечает «можно» на невыполненную проверку");
            Assert.IsFalse(CollisionGuard.ShouldCastRay(5e-5f),
                "путь меньше 0,1 мм — тоже вырожденный: физика такого отрезка не различает");
            Assert.IsFalse(CollisionGuard.ShouldCastRay(-1f),
                "отрицательная длина — не путь: каст по ней бессмысленен");

            Assert.IsTrue(CollisionGuard.ShouldCastRay(1e-4f), "путь ровно на пороге — уже настоящий");
            Assert.IsTrue(CollisionGuard.ShouldCastRay(0.25f), "путь 25 см обязан проверяться лучом");
            Assert.IsTrue(CollisionGuard.ShouldCastRay(12f), "дальняя цель тоже проверяется");
        }

        /// <summary>
        /// БЕЗ РОБОТА ДВИЖЕНИЕ ПРОПУСКАЕТСЯ — настоящее поведение компонента (без сцены).
        ///
        /// ПРАВИЛО: «нет данных — не блокируем». Обратное (запрещать движение, пока робот не
        /// назначен) остановило бы работу сразу после загрузки сцены, до первого выбора робота.
        /// Здесь же проверяется, что предохранитель не пытается лезть в физику при пустой ссылке:
        /// `CanMoveTo` возвращает `true`, не дойдя до каста.
        /// </summary>
        [Test]
        public void CollisionGuard_WithoutRobotAllowsMovement()
        {
            var go = new GameObject("KvTestCollisionGuard");
            try
            {
                CollisionGuard guard = go.AddComponent<CollisionGuard>();
                Assert.IsTrue(guard.CanMoveTo(new Vector3(1f, 0f, 1f)),
                    "без назначенного робота предохранитель обязан пропускать движение, а не падать");
                Assert.IsNull(guard.robot, "поле робота не должно заполняться само");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------------------------------------------------------- RobotSelector

        /// <summary>
        /// ВЫБОР АКТИВНОГО РОБОТА И ПОВТОРНЫЙ ВЫБОР ТОГО ЖЕ ИНДЕКСА (P1-15; чистая функция).
        ///
        /// ДВА ПРАВИЛА В ОДНОМ ТЕСТЕ, потому что они про одно и то же — про индекс:
        ///   1) индекс живёт ВНУТРИ списка роботов. `robots[activeIndex]` без прижатия — это
        ///      `IndexOutOfRangeException` ровно тогда, когда робота пересоздали или список
        ///      сократили: оператор получает исключение вместо «активного робота нет»;
        ///   2) повторный выбор того же индекса даёт ТОГО ЖЕ робота. Выбор приходит из двух путей
        ///      (клавиша/прицел и панель) и может быть запрошен несколько раз за кадр; если бы
        ///      правило «сдвигало» выбор, оператор получал бы «выбрал второго, а поехал третий».
        /// </summary>
        [Test]
        public void RobotSelector_ActiveIndexStaysInsideListAndRepeats()
        {
            Assert.AreEqual(-1, RobotSelector.ResolveActiveIndex(0, 0),
                "роботов нет — активного быть не может");
            Assert.AreEqual(-1, RobotSelector.ResolveActiveIndex(3, -2),
                "отрицательное число роботов — тоже «нет активного», а не исключение");
            Assert.AreEqual(0, RobotSelector.ResolveActiveIndex(0, 3), "первый робот остаётся первым");
            Assert.AreEqual(2, RobotSelector.ResolveActiveIndex(2, 3), "последний робот доступен");
            Assert.AreEqual(2, RobotSelector.ResolveActiveIndex(9, 3),
                "индекс за списком прижимается к последнему роботу, а не выходит за массив");
            Assert.AreEqual(0, RobotSelector.ResolveActiveIndex(-4, 3),
                "отрицательный индекс прижимается к первому роботу");

            int first = RobotSelector.ResolveActiveIndex(1, 4);
            Assert.AreEqual(1, first);
            Assert.AreEqual(first, RobotSelector.ResolveActiveIndex(1, 4),
                "повторный выбор того же индекса обязан давать того же робота");
            Assert.AreEqual(first, RobotSelector.ResolveActiveIndex(first, 4),
                "выбор «текущего» индекса не должен смещать выбор");
        }

        // ---------------------------------------------------------------- TargetMarker

        /// <summary>
        /// СМЕНА СОСТОЯНИЯ «ВАЛИДНА / НЕВАЛИДНА» ДАЁТ РАЗНЫЕ ЦВЕТА.
        ///
        /// ЗАЧЕМ. Маркер цели переключается по нескольку раз в секунду при прицеливании, и от
        /// состояния зависит РОВНО цвет (`SetValid` пишет его в `MaterialPropertyBlock`). Если
        /// ветка «валидна/невалидна» перестанет различаться, оператор увидит «можно» там, где
        /// нельзя, — то есть маркер начнёт врать.
        /// </summary>
        [Test]
        public void TargetMarker_ValidAndInvalidUseDifferentColors()
        {
            Color valid = new Color(0.15f, 0.9f, 0.3f, 1f);
            Color invalid = new Color(0.9f, 0.2f, 0.2f, 1f);

            Assert.AreEqual(valid, TargetMarker.ColorFor(true, valid, invalid),
                "валидная цель обязана крашиться в цвет «можно»");
            Assert.AreEqual(invalid, TargetMarker.ColorFor(false, valid, invalid),
                "невалидная цель обязана краситься в цвет «нельзя»");
            Assert.AreNotEqual(TargetMarker.ColorFor(true, valid, invalid),
                TargetMarker.ColorFor(false, valid, invalid),
                "оба состояния дали ОДИН цвет — маркер перестал сообщать оператору, можно ли ставить цель");
        }
    }
}
