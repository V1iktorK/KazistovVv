using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ СЕССИИ §39 — КУРСОРЫ, ВТОРОЙ ЛАЗЕР, СКЕЛЕТ КИСТЕЙ.
    ///
    /// ОТКУДА ВЗЯТЫ ПРЕДМЕТЫ — ИЗ СЛОВ ВЛАДЕЛЬЦА после прогона сборки §38-хотфикс:
    /// «руки начали отображаться! НО! они как наборы квадратиков… не видно курсора кисти и курсор
    /// контроллера во что-то упирается + нет лазера из левого контроллера».
    ///
    /// ЧТО ЗДЕСЬ ПРОВЕРЯЕТСЯ:
    ///   1) «УПИРАЕТСЯ ВО ЧТО-ТО» — это луч, который обрывался на ПЛОСКОСТИ ПАНЕЛИ (канвас 1,05 м)
    ///      даже когда мимо иконок. Теперь при промахе луч идёт на полную длину, при попадании — до
    ///      центра иконки; на конце есть метка, у пальца — своя.
    ///   2) ВТОРОЙ ЛАЗЕР (левая рука) — ТОЛЬКО УКАЗАТЕЛЬ: нажимать им нельзя ни при каких условиях.
    ///   3) СКЕЛЕТ КИСТИ — 21 кость между суставами, все индексы в допустимом диапазоне, без дублей.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: «видно ли курсор глазами» — это ваш взгляд в шлеме.
    /// </summary>
    public class KvXrS39Tests
    {
        // ============================================================ 1. КУРСОР ЛУЧА

        /// <summary>
        /// ГЛАВНЫЙ ДЕФЕКТ §39: луч НЕ должен обрываться на невидимой плоскости панели при промахе.
        /// Обрыв на плоскости и был тем, что владелец назвал «курсор во что-то упирается».
        /// </summary>
        [Test]
        public void Cursor_RayGoesFullLengthWhenMissing()
        {
            Assert.AreEqual(10f, KvXrCursor.RayLengthMeters(false, 0.41f, 10f), 0.001f,
                "при промахе луч обязан идти на полную длину, а не обрываться на плоскости панели");
            Assert.AreEqual(0.41f, KvXrCursor.RayLengthMeters(true, 0.41f, 10f), 0.001f,
                "при попадании луч обязан заканчиваться в центре иконки");
        }

        /// <summary>Метка на конце луча видна только когда луч есть, и меняет состояние над иконкой.</summary>
        [Test]
        public void Cursor_StateFollowsRayAndHover()
        {
            Assert.AreEqual(KvCursorState.Hidden, KvXrCursor.State(false, false),
                "луча нет — метки быть не должно");
            Assert.AreEqual(KvCursorState.Idle, KvXrCursor.State(true, false),
                "луч есть, иконки под ним нет — спокойное состояние");
            Assert.AreEqual(KvCursorState.OverIcon, KvXrCursor.State(true, true),
                "луч навёл иконку — метка обязана это показывать");
        }

        /// <summary>Размеры меток — числами: 14 мм на конце луча, 22 мм у кончика пальца.</summary>
        [Test]
        public void Cursor_MarkerSizesAreDocumented()
        {
            Assert.AreEqual(0.014f, KvXrCursor.RayMarkerMeters, 1e-4f);
            Assert.AreEqual(0.022f, KvXrCursor.FingerMarkerMeters, 1e-4f);
            Assert.Greater(KvXrCursor.FingerMarkerMeters, KvXrCursor.RayMarkerMeters,
                "метка у пальца должна быть заметнее метки на конце луча");
        }

        // ============================================================ 2. ВТОРОЙ ЛАЗЕР

        /// <summary>
        /// ВТОРОЙ ЛАЗЕР ИЗ ЛЕВОЙ РУКИ — ТОЛЬКО УКАЗАТЕЛЬ. Владелец выбрал именно это, поэтому
        /// функция «может нажимать» обязана быть ложной ВСЕГДА — независимо от настроек.
        /// </summary>
        [Test]
        public void SecondaryRay_NeverPresses()
        {
            Assert.IsFalse(KvXrSecondaryRayLogic.CanPress(), "второй луч не должен нажимать ничего");
            Assert.IsFalse(KvXrSecondaryRayLogic.CanPress(), "и при повторном запросе тоже");
        }

        /// <summary>
        /// ИСТОЧНИК ВТОРОГО ЛУЧА — РУКА, КОТОРАЯ НЕ ЯВЛЯЕТСЯ РУКОЙ ЛУЧА. Иначе оба луча шли бы из
        /// одной руки и «лазера из левого контроллера» не было бы.
        /// </summary>
        [Test]
        public void SecondaryRay_ComesFromTheOtherHand()
        {
            Assert.AreEqual(KvXrQuickHand.Left,
                KvXrSecondaryRayLogic.SourceHand(KvXrQuickHand.Right),
                "при правой руке луча второй луч идёт из ЛЕВОЙ");
            Assert.AreEqual(KvXrQuickHand.Right,
                KvXrSecondaryRayLogic.SourceHand(KvXrQuickHand.Left),
                "при левой руке луча второй луч идёт из ПРАВОЙ");
        }

        /// <summary>Длина второго луча — по тем же правилам (промах → полная, попадание → центр иконки).</summary>
        [Test]
        public void SecondaryRay_LengthRulesMatchMainRay()
        {
            Assert.AreEqual(10f, KvXrSecondaryRayLogic.LengthMeters(false, 0.41f, 10f), 0.001f);
            Assert.AreEqual(0.41f, KvXrSecondaryRayLogic.LengthMeters(true, 0.41f, 10f), 0.001f);
            Assert.AreEqual(0.005f, KvXrSecondaryRayLogic.WidthMeters, 1e-4f,
                "второй луч обязан быть тоньше основного");
        }

        /// <summary>Второй луч выключается файлом подстройки без пересборки (по умолчанию — включён).</summary>
        [Test]
        public void SecondaryRay_CanBeDisabledFromTuningFile()
        {
            Assert.IsTrue(KvXrTuning.ResolveSecondaryRay(new Dictionary<string, string>()),
                "по умолчанию второй луч включён");
            Assert.IsFalse(KvXrTuning.ResolveSecondaryRay(
                new Dictionary<string, string> { { KvXrTuning.KeySecondaryRay, "0" } }));
            Assert.IsTrue(KvXrTuning.ResolveSecondaryRay(
                new Dictionary<string, string> { { KvXrTuning.KeySecondaryRay, "1" } }));
        }

        // ============================================================ 3. СКЕЛЕТ КИСТИ

        /// <summary>
        /// СКЕЛЕТ: 25 КОСТЕЙ. Пересчитано по дереву суставов XR Hands: запястье–ладонь (1),
        /// большой палец (4: ладонь→пясть→проксимальная→дистальная→кончик) и по пять костей на
        /// каждый из четырёх пальцев (20). В первой редакции плана стояло 21 — это была арифметическая
        /// ошибка, найденная на тестах: костей ровно 25, и тест это фиксирует.
        /// </summary>
        [Test]
        public void Skeleton_HasTwentyFiveBones()
        {
            Assert.AreEqual(25, KvHandSkeleton.BoneCount,
                "скелет кисти обязан состоять из 25 костей");
            Assert.AreEqual(25, KvHandSkeleton.Bones.Length);
        }

        /// <summary>Все кости ссылаются на ДЕЙСТВИТЕЛЬНЫЕ суставы кисти и не рисуют «кость в себя».</summary>
        [Test]
        public void Skeleton_BonesUseValidJoints()
        {
            for (int i = 0; i < KvHandSkeleton.Bones.Length; i++)
            {
                KvHandBone bone = KvHandSkeleton.Bones[i];
                Assert.AreNotEqual(bone.From, bone.To, "кость не может соединять сустав с самим собой (№" + i + ")");
                Assert.IsTrue(KvHandSkeleton.IsValidJoint(bone.From), "начало кости №" + i + " вне суставов кисти");
                Assert.IsTrue(KvHandSkeleton.IsValidJoint(bone.To), "конец кости №" + i + " вне суставов кисти");
            }
        }

        /// <summary>Дублей нет: одна и та же пара суставов не должна рисоваться дважды.</summary>
        [Test]
        public void Skeleton_HasNoDuplicateBones()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < KvHandSkeleton.Bones.Length; i++)
            {
                KvHandBone bone = KvHandSkeleton.Bones[i];
                int low = Mathf.Min(bone.From, bone.To);
                int high = Mathf.Max(bone.From, bone.To);
                string key = low + ":" + high;
                Assert.IsFalse(seen.Contains(key), "кость " + key + " встречается дважды");
                seen.Add(key);
            }
        }

        /// <summary>
        /// «НЕТ СУСТАВА — НЕТ КОСТИ». Если поза какого-то сустава не пришла, кость не рисуется:
        /// иначе в шлеме появлялись бы «резиновые» линии в пустоту.
        /// </summary>
        [Test]
        public void Skeleton_BoneSkippedWhenJointMissing()
        {
            bool[] present = new bool[KvHandSkeleton.JointCount];
            for (int i = 0; i < present.Length; i++) present[i] = true;
            Assert.AreEqual(25, KvHandSkeleton.CountDrawableBones(present),
                "при всех суставах рисуются все 25 костей");

            KvHandBone first = KvHandSkeleton.Bones[0];
            present[KvHandSkeleton.ToLocalIndex(first.From)] = false;
            Assert.AreEqual(24, KvHandSkeleton.CountDrawableBones(present),
                "без одного сустава кость, которая на него опирается, рисоваться не должна");
        }

        /// <summary>Толщина кости — число из правил (4 мм), и она тоньше точки сустава (12 мм).</summary>
        [Test]
        public void Skeleton_BoneWidthIsThin()
        {
            Assert.AreEqual(0.004f, KvHandSkeleton.BoneWidthMeters, 1e-4f);
            Assert.AreEqual(0.012f, KvHandSkeleton.JointSizeMeters, 1e-4f);
            Assert.Less(KvHandSkeleton.BoneWidthMeters, KvHandSkeleton.JointSizeMeters);
        }

        /// <summary>
        /// «УКАЗАТЕЛЬНЫЙ» ОТРЕЗОК ОТ КОНЧИКА ПАЛЬЦА — то, чем видно, куда направлена кисть.
        /// Число проверяется, чтобы «курсор у пальца» не пропал при правках.
        /// </summary>
        [Test]
        public void Skeleton_FingerPointingSegmentIsDocumented()
        {
            Assert.AreEqual(0.060f, KvHandSkeleton.FingerPointerMeters, 1e-4f,
                "длина указательного отрезка от кончика пальца — 6 см");
        }
    }
}
