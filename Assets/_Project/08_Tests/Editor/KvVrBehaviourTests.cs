// ================================================================================================
//  KvVrBehaviourTests.cs — ПОВЕДЕНЧЕСКИЕ тесты папки `01_Scripts\VR` (P4-8).
//
//  ЧТО В ПАПКЕ. `PoseSelector` (прицел и выбор цели в VR/AR), `ARInputProvider` (ввод планшета),
//  и три класса, помеченных `[Obsolete]`: `PlacementController`, `VRInputManager`, `VRHandTracker`.
//
//  ЧТО НЕ ТРОГАЕТСЯ И ПОЧЕМУ. `VRHandTracker` — МЁРТВЫЙ код (S44): он нигде не создаётся и не
//  вызывается, отслеживание рук делает `KvXrHands`. Проверять его поведение бессмысленно: у него нет
//  поведения в проекте, и тест закреплял бы мёртвый код как рабочий. То же — `PlacementController`
//  и `VRInputManager`.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ (чистые функции `PoseSelector`, вынесенные из кадра прицела):
//    1) куда рисуется конец луча при попадании и при промахе;
//    2) когда нажатие выбора вообще срабатывает;
//    3) что вырожденный луч не даёт NaN.
//
//  ГРАНИЦА ЧЕСТНОСТИ: сам `Physics.Raycast` требует сцены с коллайдерами и слоями — в EditMode его
//  нет. Поэтому проверяется РЕШАЮЩАЯ часть прицела: что делать с результатом луча. Именно там и
//  был дефект «луч залипает на прошлом попадании, а робот получает цель в никуда».
// ================================================================================================

using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Поведение прицела VR/AR: конец луча и условие выбора.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvVrBehaviourTests
    {
        /// <summary>
        /// ПРИ ПОПАДАНИИ ЛУЧ ЗАКАНЧИВАЕТСЯ В ТОЧКЕ ПОПАДАНИЯ.
        ///
        /// ЗАЧЕМ. Длина луча — не оформление: по его концу оператор понимает, КУДА он целится. Если
        /// конец остаётся на старой точке, картинка врёт, и выбор цели становится лотереей.
        /// </summary>
        [Test]
        public void PoseSelector_LaserEndsAtHitPoint()
        {
            Vector3 origin = new Vector3(0f, 1.5f, 0f);
            Vector3 direction = Vector3.forward;
            Vector3 hitPoint = new Vector3(0f, 1.5f, 4f);

            Vector3 end = PoseSelector.LaserEndPoint(origin, direction, 15f, true, hitPoint);

            Assert.AreEqual(hitPoint, end,
                "при попадании конец луча обязан быть в точке попадания, а не на полной длине луча");
        }

        /// <summary>
        /// ПРИ ПРОМАХЕ ЛУЧ ИДЁТ НА ПОЛНУЮ ДЛИНУ И НЕ ЗАЛИПАЕТ.
        ///
        /// ЧТО ЛОВИТ. Раньше в ветке промаха конец луча считался как `origin + dir * maxDistance`, а
        /// при попадании — из точки попадания. Перепутать эти две ветки легко, и тогда при промахе
        /// луч «замирает» в точке прошлого попадания: оператор целится в пустоту, а маркер стоит на
        /// столе, где он был секунду назад.
        /// </summary>
        [Test]
        public void PoseSelector_LaserReachesMaxDistanceWithoutHit()
        {
            Vector3 origin = new Vector3(1f, 1.5f, -2f);
            Vector3 direction = Vector3.forward;
            float maxDistance = 15f;

            Vector3 end = PoseSelector.LaserEndPoint(origin, direction, maxDistance, false, Vector3.zero);

            Assert.AreEqual(origin.z + maxDistance, end.z, 1e-4f,
                "при промахе луч обязан дойти до полной длины, иначе он «залипает» на прошлом попадании");
            Assert.AreEqual(origin.x, end.x, 1e-4f, "промах не должен смещать луч вбок");
            Assert.AreEqual(origin.y, end.y, 1e-4f, "промах не должен менять высоту луча");
        }

        /// <summary>
        /// ВЫБОР СРАБАТЫВАЕТ ТОЛЬКО ПО ЦЕЛИ. Главное правило прицела.
        ///
        /// ЗАЧЕМ. `SelectDown` приходит на каждое нажатие курка/тап — в том числе когда луч ушёл в
        /// пустоту. Без проверки «есть попадание» робот получал бы цель «в никуда» (нулевую точку) и
        /// уезжал в начало координат, а оператор видел бы «робот сам поехал» и не связал бы это с
        /// промахом.
        /// </summary>
        [Test]
        public void PoseSelector_SelectNeedsHitAndPress()
        {
            Assert.IsTrue(PoseSelector.ShouldSelect(true, true),
                "попадание по цели и нажатие обязаны давать выбор");
            Assert.IsFalse(PoseSelector.ShouldSelect(false, true),
                "нажатие БЕЗ попадания не должно выбирать: робот получит цель в никуда");
            Assert.IsFalse(PoseSelector.ShouldSelect(true, false),
                "попадание без нажатия — это только прицел, выбор не выполняется");
            Assert.IsFalse(PoseSelector.ShouldSelect(false, false), "ни попадания, ни нажатия — нечего выбирать");
        }

        /// <summary>
        /// ВЫРОЖДЕННЫЙ ЛУЧ НЕ ДАЁТ NaN.
        ///
        /// ЗАЧЕМ. Направление приходит от контроллера: пока трекинг не готов, оно бывает нулевым.
        /// Нормирование такого вектора даёт `NaN`, а `NaN` в позиции маркера — это невидимый маркер и
        /// «сломанный» прицел без единой ошибки в журнале (в проекте NaN-ы уже ловили отдельными
        /// сторожами). Проверяется, что конец луча в этом случае остаётся числом (совпадает с началом).
        /// </summary>
        [Test]
        public void PoseSelector_DegenerateLaserStaysFinite()
        {
            Vector3 origin = new Vector3(2f, 3f, 4f);
            Vector3 end = PoseSelector.LaserEndPoint(origin, Vector3.zero, 15f, false, Vector3.zero);

            Assert.IsFalse(float.IsNaN(end.x) || float.IsNaN(end.y) || float.IsNaN(end.z),
                "нулевое направление дало NaN в конце луча — маркер пропадёт, и причину не будет видно");
            Assert.IsFalse(float.IsInfinity(end.x) || float.IsInfinity(end.y) || float.IsInfinity(end.z),
                "бесконечность в конце луча — то же самое: прицел перестаёт быть видимым");
            Assert.AreEqual(origin, end, "нулевое направление не должно никуда сдвигать конец луча");
        }
    }
}
