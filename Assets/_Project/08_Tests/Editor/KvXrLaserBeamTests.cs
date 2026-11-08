using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ПУНКТ 2 ВЛАДЕЛЬЦА (02.11.2026): «лазеры не разноцветные как на ПК версии и не упираются в
    /// роботов и столы и пол».
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ БЕЗ ШЛЕМА И БЕЗ СЦЕНЫ:
    ///   1) курок включает СВОЙ лазер: левый — красный, правый — зелёный (как Z/X на компьютере);
    ///   2) цвета лучей — красный/зелёный, и наведение не подменяет цвет руки;
    ///   3) строка журнала потока больше НЕ говорит «лазеры выключены», а называет оба лазера и обрыв;
    ///   4) правило длины луча: ближайшее из попаданий (интерфейс или мир), а без попаданий — полная
    ///      длина. Именно это и есть «упирается в робота/стол/пол, а не проходит сквозь».
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: «видно ли это глазами в шлеме» — проверяется на устройстве. Но числа и строки,
    /// от которых зависит видимое поведение, закреплены здесь: если кто-то снова сделает луч
    /// фиксированной длины, упадёт конкретный тест, а не «владелец снова скажет».
    /// </summary>
        [Category(KvTestCategories.Fast)]
    public class KvXrLaserBeamTests
    {
        /// <summary>
        /// КУРОК = ЛАЗЕР (как Z/X на ПК). Порог один на обе руки, и он же стоит значением по
        /// умолчанию в мосте потока — иначе журнал и цвет говорили бы разное.
        /// </summary>
        [Test]
        public void TriggerTurnsTheLaserOn()
        {
            Assert.IsTrue(KvXrFlowBridge.LaserHeld(0.9f), "нажатый курок обязан включать лазер");
            Assert.IsFalse(KvXrFlowBridge.LaserHeld(0.2f), "отпущенный курок лазер не включает");
            Assert.IsTrue(KvXrFlowBridge.LaserHeld(KvXrFlowBridge.LaserTriggerThreshold),
                "порог включения — само значение порога");
        }

        /// <summary>ЛЕВАЯ РУКА — КРАСНЫЙ лазер (новая точка), ПРАВАЯ — ЗЕЛЁНЫЙ (выбор/фантом).</summary>
        [Test]
        public void LeftIsRedRightIsGreen()
        {
            Color left = KvXrLaserColors.Ray(KvXrQuickHand.Left, false);
            Color right = KvXrLaserColors.Ray(KvXrQuickHand.Right, false);

            Assert.Greater(left.r, left.g, "левый лазер обязан быть красным");
            Assert.Greater(right.g, right.r, "правый лазер обязан быть зелёным");
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА ПОТОКА: лазеры включены и обрываются. Прежняя редакция говорила
        /// «лазеры потока выключены (в шлеме свои)» — по ней владелец и решил, что лазеров нет.
        /// </summary>
        [Test]
        public void FlowLineSaysLasersAreOnAndStop()
        {
            string line = KvXrFlowBridge.Describe(true, "6-осевой робот", 2);

            StringAssert.Contains("красный", line, "в строке назван красный лазер");
            StringAssert.Contains("зелёный", line, "в строке назван зелёный лазер");
            StringAssert.Contains("обрыв", line, "в строке сказано, что луч обрывается на попадании");
            StringAssert.DoesNotContain("выключены", line,
                "прежнее «лазеры потока выключены» обязано исчезнуть из журнала");
        }

        /// <summary>
        /// ДЛИНА ЛУЧА — ПО ПЕРВОМУ ПОПАДАНИЮ. Три случая: интерфейс ближе мира, мир единственное
        /// попадание (робот/стол/пол), попаданий нет (полная длина, как и было).
        /// </summary>
        [Test]
        public void RayEndsAtFirstHitOrAtFullLength()
        {
            Assert.AreEqual(0.9f, KvXrLaserRay.EndMeters(true, 0.9f, true, 2.5f, 10f), 1e-4f,
                "интерфейс ближе мира — луч обрывается на интерфейсе");
            Assert.AreEqual(2.5f, KvXrLaserRay.EndMeters(false, 0f, true, 2.5f, 10f), 1e-4f,
                "мир — единственное попадание: обрыв на роботе/столе/полу");
            Assert.AreEqual(10f, KvXrLaserRay.EndMeters(false, 0f, false, 0f, 10f), 1e-4f,
                "попаданий нет — полная длина, луч не «обрублен» без причины");
        }

        /// <summary>
        /// ЛУЧ НЕ МОЖЕТ БЫТЬ КОРОЧЕ НУЛЯ и не должен «пропадать»: нулевая длина невидима, и это
        /// выглядело бы как «лазер мигнул».
        /// </summary>
        [Test]
        public void RayNeverShorterThanVisibleMinimum()
        {
            Assert.AreEqual(KvXrLaserRay.MinLengthMeters,
                KvXrLaserRay.EndMeters(true, 0f, false, 0f, 0f), 1e-5f,
                "нулевая длина поднимается до минимальной видимой");
        }
    }
}

