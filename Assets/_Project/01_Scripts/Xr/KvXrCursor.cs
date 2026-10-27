using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace KazistovVvXr
{
    /// <summary>СОСТОЯНИЕ МЕТКИ-КУРСОРА на конце луча.</summary>
    public enum KvCursorState
    {
        /// <summary>Луча нет — метки тоже нет.</summary>
        Hidden = 0,

        /// <summary>Луч есть, иконки под ним нет — спокойный цвет.</summary>
        Idle = 1,

        /// <summary>Луч навёл иконку — метка показывает попадание.</summary>
        OverIcon = 2
    }

    /// <summary>
    /// КУРСОР НА КОНЦЕ ЛУЧА (§39, слова владельца: «не видно курсора» и «курсор контроллера во
    /// что-то упирается»).
    ///
    /// В ЧЁМ БЫЛА ПРИЧИНА «УПИРАЕТСЯ». Луч рисовался до ПЛОСКОСТИ ПАНЕЛИ: `TryAim` возвращает точку
    /// пересечения луча с плоскостью канваса, а канвас — 1,05 × 1,05 м, то есть заметно больше дуги
    /// иконок. Когда луч проходил МИМО иконок, линия всё равно обрывалась на этой невидимой
    /// плоскости — снаружи это выглядит как «уперся во что-то». Плюс на конце не было никакой метки,
    /// поэтому точку попадания не было видно вообще.
    ///
    /// ЧТО ТЕПЕРЬ: промах — луч идёт на полную длину (видны стены и объекты), попадание — до центра
    /// иконки; на конце точка 14 мм (спокойная → зелёная над иконкой), у кончика указательного
    /// пальца — своя точка 22 мм.
    /// </summary>
    public static class KvXrCursor
    {
        /// <summary>Размер метки на конце луча, метры.</summary>
        public const float RayMarkerMeters = 0.014f;

        /// <summary>Размер метки у кончика указательного пальца, метры.</summary>
        public const float FingerMarkerMeters = 0.022f;

        /// <summary>Состояние метки: нет луча → скрыта, есть луч → спокойная/над иконкой.</summary>
        public static KvCursorState State(bool rayActive, bool overIcon)
        {
            if (!rayActive) return KvCursorState.Hidden;
            return overIcon ? KvCursorState.OverIcon : KvCursorState.Idle;
        }

        /// <summary>
        /// ДЛИНА ЛУЧА. Попал в иконку — до неё. Промах — ПОЛНАЯ длина, а не обрыв на плоскости панели.
        /// </summary>
        public static float RayLengthMeters(bool overIcon, float distanceToHitMeters, float fullLengthMeters)
        {
            float full = Mathf.Max(0.01f, fullLengthMeters);
            if (!overIcon) return full;
            if (distanceToHitMeters <= 0f || distanceToHitMeters > full) return full;
            return distanceToHitMeters;
        }
    }

    /// <summary>
    /// ВТОРОЙ ЛАЗЕР — ИЗ РУКИ, КОТОРАЯ НЕ ЯВЛЯЕТСЯ РУКОЙ ЛУЧА (§39).
    ///
    /// РЕШЕНИЕ ВЛАДЕЛЬЦА: «второй лазер — только указатель (не нажимает)». Поэтому `CanPress()`
    /// возвращает false ВСЕГДА, и это не настройка, а правило: нажатие остаётся у руки луча
    /// (по умолчанию правой), иначе один курок делал бы два действия.
    /// </summary>
    public static class KvXrSecondaryRayLogic
    {
        /// <summary>Толщина второго луча, метры — заметно тоньше основного (8 мм).</summary>
        public const float WidthMeters = 0.005f;

        /// <summary>Размер метки на конце второго луча, метры — меньше основной.</summary>
        public const float MarkerMeters = 0.010f;

        /// <summary>Может ли второй луч нажимать. НЕТ — по решению владельца.</summary>
        public static bool CanPress() { return false; }

        /// <summary>Из какой руки идёт второй луч: всегда из противоположной руке луча.</summary>
        public static KvXrQuickHand SourceHand(KvXrQuickHand rayHand)
        {
            return rayHand == KvXrQuickHand.Right ? KvXrQuickHand.Left : KvXrQuickHand.Right;
        }

        /// <summary>Длина второго луча — по тем же правилам, что и у основного.</summary>
        public static float LengthMeters(bool overIcon, float distanceToHitMeters, float fullLengthMeters)
        {
            return KvXrCursor.RayLengthMeters(overIcon, distanceToHitMeters, fullLengthMeters);
        }
    }

    /// <summary>ОДНА «КОСТЬ» КИСТИ: пара суставов (индексы — значения `XRHandJointID`).</summary>
    public struct KvHandBone
    {
        public int From;
        public int To;

        public KvHandBone(int from, int to)
        {
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// СКЕЛЕТ КИСТИ — ЛИНИИ МЕЖДУ СУСТАВАМИ (§39, слова владельца: «они как наборы квадратиков, а не
    /// как кисти»).
    ///
    /// ТОПОЛОГИЯ ЗАДАНА ЯВНО, а не «по порядку индексов»: у кисти 26 суставов, и связь между ними —
    /// это анатомия, а не арифметика. Список: запястье–ладонь (1), большой палец (4), по пять костей
    /// на указательный, средний, безымянный и мизинец (20) — **25 костей**. Число пересчитано по
    /// дереву XR Hands: ладонь соединяется с запястьем и с пятью пястями, дальше идут цепочки пальцев.
    ///
    /// ПОЧЕМУ НЕ МЕШ КИСТИ. Владелец выбрал скелет из линий (меш из SDK — вопрос на будущее). Скелет
    /// рисуется В ТОМ ЖЕ меше, что и точки суставов, поэтому на руку по-прежнему ОДИН вызов отрисовки.
    ///
    /// ПРАВИЛО «НЕТ СУСТАВА — НЕТ КОСТИ»: если поза сустава не пришла, кость не рисуется, иначе в
    /// шлеме появлялись бы «резиновые» линии в пустоту.
    /// </summary>
    public static class KvHandSkeleton
    {
        /// <summary>Толщина «кости», метры.</summary>
        public const float BoneWidthMeters = 0.004f;

        /// <summary>Размер точки сустава, метры (тот же, что в отрисовке кисти).</summary>
        public const float JointSizeMeters = 0.012f;

        /// <summary>Длина «указательного» отрезка от кончика пальца, метры (видно, куда смотрит кисть).</summary>
        public const float FingerPointerMeters = 0.060f;

        /// <summary>Сколько суставов в кисти.</summary>
        public static readonly int JointCount = (int)XRHandJointID.EndMarker - (int)XRHandJointID.Wrist;

        /// <summary>Первый допустимый индекс сустава.</summary>
        public static readonly int MinJointIndex = (int)XRHandJointID.Wrist;

        /// <summary>Первый индекс ЗА пределами суставов.</summary>
        public static readonly int MaxJointIndex = (int)XRHandJointID.EndMarker;

        private static readonly KvHandBone[] bones = BuildBones();

        /// <summary>Кости кисти (21 штука).</summary>
        public static KvHandBone[] Bones { get { return bones; } }

        /// <summary>Сколько костей.</summary>
        public static int BoneCount { get { return bones.Length; } }

        /// <summary>Ссылается ли индекс на настоящий сустав кисти.</summary>
        public static bool IsValidJoint(int jointIndex)
        {
            return jointIndex >= MinJointIndex && jointIndex < MaxJointIndex;
        }

        /// <summary>Индекс сустава → индекс в массиве из 26 элементов (для отметок «поза пришла»).</summary>
        public static int ToLocalIndex(int jointIndex)
        {
            return jointIndex - MinJointIndex;
        }

        /// <summary>Сколько костей можно нарисовать при таком наборе пришедших суставов.</summary>
        public static int CountDrawableBones(bool[] jointPresent)
        {
            if (jointPresent == null) return 0;

            int count = 0;
            for (int i = 0; i < bones.Length; i++)
            {
                if (!IsValidJoint(bones[i].From) || !IsValidJoint(bones[i].To)) continue;
                int from = ToLocalIndex(bones[i].From);
                int to = ToLocalIndex(bones[i].To);
                if (from < 0 || from >= jointPresent.Length) continue;
                if (to < 0 || to >= jointPresent.Length) continue;
                if (jointPresent[from] && jointPresent[to]) count++;
            }
            return count;
        }

        private static KvHandBone[] BuildBones()
        {
            var list = new List<KvHandBone>();

            // Запястье — ладонь: основа кисти.
            list.Add(new KvHandBone((int)XRHandJointID.Wrist, (int)XRHandJointID.Palm));

            // Большой палец: ладонь → пясть → проксимальная → дистальная → кончик (4 кости, но
            // первая идёт от ЛАДОНИ, поэтому «три кости большого пальца» + связь с ладонью).
            list.Add(new KvHandBone((int)XRHandJointID.Palm, (int)XRHandJointID.ThumbMetacarpal));
            list.Add(new KvHandBone((int)XRHandJointID.ThumbMetacarpal, (int)XRHandJointID.ThumbProximal));
            list.Add(new KvHandBone((int)XRHandJointID.ThumbProximal, (int)XRHandJointID.ThumbDistal));
            list.Add(new KvHandBone((int)XRHandJointID.ThumbDistal, (int)XRHandJointID.ThumbTip));

            // Четыре пальца: ладонь → пясть → проксимальная → промежуточная → дистальная → кончик.
            AddFinger(list, XRHandJointID.Palm, XRHandJointID.IndexMetacarpal,
                XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate,
                XRHandJointID.IndexDistal, XRHandJointID.IndexTip);

            AddFinger(list, XRHandJointID.Palm, XRHandJointID.MiddleMetacarpal,
                XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate,
                XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip);

            AddFinger(list, XRHandJointID.Palm, XRHandJointID.RingMetacarpal,
                XRHandJointID.RingProximal, XRHandJointID.RingIntermediate,
                XRHandJointID.RingDistal, XRHandJointID.RingTip);

            AddFinger(list, XRHandJointID.Palm, XRHandJointID.LittleMetacarpal,
                XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate,
                XRHandJointID.LittleDistal, XRHandJointID.LittleTip);

            return list.ToArray();
        }

        private static void AddFinger(List<KvHandBone> list, XRHandJointID palm, XRHandJointID metacarpal,
            XRHandJointID proximal, XRHandJointID intermediate, XRHandJointID distal, XRHandJointID tip)
        {
            list.Add(new KvHandBone((int)palm, (int)metacarpal));
            list.Add(new KvHandBone((int)metacarpal, (int)proximal));
            list.Add(new KvHandBone((int)proximal, (int)intermediate));
            list.Add(new KvHandBone((int)intermediate, (int)distal));
            list.Add(new KvHandBone((int)distal, (int)tip));
        }
    }
}
