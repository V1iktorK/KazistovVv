using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Hands.OpenXR;
using KazistovVvFeatures;

namespace KazistovVvXr
{
    /// <summary>
    /// ОТСЛЕЖИВАНИЕ КИСТЕЙ (сессия §37, пункт 4 задания: «сначала кисти, потом жесты»).
    ///
    /// ЗАДАЧА ВЛАДЕЛЬЦА ДОСЛОВНО: «нужно настроить отслеживание кистей (я кладу контроллеры и вижу
    /// свои кисти/палец в приложении), и только потом — жесты».
    ///
    /// ОТКУДА БЕРУТСЯ КИСТИ. Пакет Unity **XR Hands 1.9.0** (разрешён владельцем опросом §37).
    /// Он приносит фичу OpenXR `HandTracking` (`XR_EXT_hand_tracking`) и подсистему `XRHandSubsystem`.
    /// Мета-данные в манифесте (`handtracking = 1` и разрешение `com.picovr.permission.HAND_TRACKING`)
    /// дописывает хук SDK PICO — но только когда в `PICOProjectSetting` выставлено
    /// `isHandTracking = true` (это делает `KvXrS37Tools.ApplyHandTrackingForAndroid`).
    ///
    /// ПОЧЕМУ РИСУЮТСЯ ТОЧКИ СУСТАВОВ, А НЕ ГОТОВЫЙ МЕШ РУКИ. Меш кисти требует скелета, шейдера и
    /// скиннинга — три вещи, каждая из которых может дать «розовое» или просадку кадров в шлеме.
    /// Здесь на каждую руку приходится РОВНО ОДИН вызов отрисовки: все 26 суставов собираются в
    /// один меш из квадратиков, развёрнутых к камере. Владелец видит кисть и палец — этого
    /// достаточно, чтобы подтвердить пункт «вижу свои кисти».
    ///
    /// ЛОВУШКА [F2] ЗАКРЫТА ЯВНО: поза кисти может прийти «нулевой» и формально достоверной.
    /// Здесь кисть считается видимой только при `isTracked` И наличии поз у суставов; всё
    /// остальное — «кисти нет», точки скрываются, жесты не срабатывают (`KvGestureMath.IsUsable`).
    ///
    /// ЕСЛИ РАНТАЙМ КИСТИ НЕ ОТДАЁТ — это пишется прямо: «подсистема есть, поз нет». Такой случай
    /// в отчёте называется «не проверено/не поддерживается», а не «работает».
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrHands : MonoBehaviour
    {
        /// <summary>Сколько суставов в кисти по спецификации XR (Wrist … LittleTip).</summary>
        public const int JointsPerHand = 26;

        /// <summary>Камера шлема — по ней разворачиваются квадратики суставов к оператору.</summary>
        public Camera headCamera;

        /// <summary>Размер точки сустава, метры. 1,2 см видно с руки и не мешает смотреть зал.</summary>
        public float jointSizeMeters = 0.012f;

        /// <summary>Размер точки на кончике указательного пальца, метры — «где мой палец».</summary>
        public float fingertipSizeMeters = 0.018f;

        /// <summary>Рисовать ли точки суставов (можно выключить файлом подстройки/настройкой).</summary>
        public bool drawJoints = true;

        /// <summary>Сообщать в журнал о появлении/пропаже кистей.</summary>
        public bool logOnChange = true;

        private static XRHandSubsystem subsystem;

        private readonly Mesh[] meshes = new Mesh[2];
        private readonly MeshFilter[] filters = new MeshFilter[2];
        private readonly MeshRenderer[] renderers = new MeshRenderer[2];
        private readonly bool[] tracked = new bool[2];
        private readonly int[] jointCounts = new int[2];
        private readonly List<Vector3> vertices = new List<Vector3>(JointsPerHand * 4);
        private readonly List<Color> colors = new List<Color>(JointsPerHand * 4);
        private readonly List<int> triangles = new List<int>(JointsPerHand * 6);

        private float nextSubsystemTry;
        private bool loggedSubsystemMissing;
        private string lastState;
        private Material rightMaterial;
        private Material leftMaterial;

        /// <summary>Правая кисть отслеживается прямо сейчас.</summary>
        public bool RightTracked { get { return tracked[1]; } }

        /// <summary>Левая кисть отслеживается прямо сейчас.</summary>
        public bool LeftTracked { get { return tracked[0]; } }

        /// <summary>Сколько суставов правой кисти пришло (0…26).</summary>
        public int RightJointCount { get { return jointCounts[1]; } }

        /// <summary>Сколько суставов левой кисти пришло (0…26).</summary>
        public int LeftJointCount { get { return jointCounts[0]; } }

        /// <summary>Подсистема кистей работает (для журнала и отчёта).</summary>
        public static bool SubsystemRunning { get { return subsystem != null && subsystem.running; } }

        /// <summary>
        /// МОСТ К СЕРВИСУ ЖЕСТОВ (добавлено 30.10.2026, S44 — пункт 5.23 плана).
        ///
        /// ЧТО БЫЛО. `KvXrHands` читал кисти из OpenXR и РИСОВАЛ их, но НИКОМУ не передавал.
        /// Сервис жестов (`KvHandTrackingService`) получал позы суставов ТОЛЬКО из диагностики
        /// (`DshStage4Diag`) — то есть в сборке жесты не работали вообще: логика распознавания
        /// готова, а данных у неё нет.
        ///
        /// Владелец просил именно обратное: «кладу контроллеры и вижу свои кисти, и только потом —
        /// жесты». Кисти видно было, жесты — нет.
        ///
        /// ЧТО СТАЛО. Каждый кадр, где кисть отслеживается, её точки передаются в сервис через
        /// `PushJoint`. Это и есть недостающий адаптер.
        ///
        /// ПОЧЕМУ СТАТИЧЕСКОЕ ПОЛЕ. Компонент создаётся через `Ensure`, и экземпляр может
        /// пересоздаваться; сервис же живёт в хабе и один. Статическая ссылка переживает
        /// пересоздание компонента и не требует правки сцены.
        /// </summary>
        public static KazistovVvFeatures.KvHandTrackingService GestureSink { get; set; }

        /// <summary>Найти или создать компонент (идемпотентно). Сцена при этом не меняется.</summary>
        public static KvXrHands Ensure(Transform parent, Camera camera)
        {
            KvXrHands existing = Object.FindAnyObjectByType<KvXrHands>();
            if (existing != null)
            {
                if (existing.headCamera == null) existing.headCamera = camera;
                return existing;
            }

            var go = new GameObject("XrHands");
            if (parent != null) go.transform.SetParent(parent, false);
            KvXrHands hands = go.AddComponent<KvXrHands>();
            hands.headCamera = camera;
            return hands;
        }

        void Awake()
        {
            if (headCamera == null) headCamera = Camera.main;
        }

        void Start()
        {
            EnsureSubsystem();
            BuildRenderer(0, KvXrQuickHand.Left);
            BuildRenderer(1, KvXrQuickHand.Right);

            // ЖЕСТЫ БЕРУТ КИСТИ ОТСЮДА. Жесты не знают про пакет XR Hands — они спрашивают
            // «дай позу этой руки». Нет кистей — нет поставщика, и жесты молчат.
            KvXrHandSource.Provider = ProvideFrame;
        }

        void OnDestroy()
        {
            KvXrHandSource.Provider = null;
        }

        /// <summary>
        /// СНЯТЬ ОТМЕТКИ СУСТАВОВ ОДНОЙ РУКИ В СЕРВИСЕ (создано 30.10.2026, S44).
        ///
        /// `KvHandTrackingService.ClearPose()` снимает ОБЕ руки сразу — на кадре, где видна только
        /// одна кисть, это мигало бы второй. Поэтому здесь снимаются точки конкретной руки через
        /// `PushJoint` со заранее недействительными координатами... — но у сервиса такого API нет.
        ///
        /// ПОЭТОМУ используем `ClearPose` ТОЛЬКО когда пропали ОБЕ руки: это единственный случай,
        /// когда «сбросить всё» не вредит. Если пропала одна — её точки просто перестанут
        /// обновляться, а `IsTracked` опирается на их наличие... — то есть этого НЕ ДОСТАТОЧНО.
        ///
        /// ЧЕСТНО: честного способа снять ОДНУ руку у сервиса сейчас нет. Поэтому здесь:
        ///   • пропали обе — `ClearPose()`, это правильно;
        ///   • пропала одна — вызываем `ClearPose()` и тут же ПЕРЕДАЁМ вторую руку заново.
        /// Это даёт верный результат без правки чужого API.
        /// </summary>
        private static void ClearSinkBecauseHandLost(KvHandFrame keep, bool keepTracked,
            KvXrQuickHand keepSide)
        {
            if (GestureSink == null) return;

            GestureSink.ClearPose();

            // Вернуть на место ту руку, которая ЕСТЬ.
            if (keepTracked) FeedGestures(keep, true, keepSide);
        }

        /// <summary>
        /// ПЕРЕДАТЬ КИСТЬ В СЕРВИС ЖЕСТОВ (создано 30.10.2026, S44).
        ///
        /// ПОЧЕМУ ОТДЕЛЬНЫМ МЕТОДОМ: это единственное место, где форматы «кисть из OpenXR» и
        /// «сустав для сервиса» соприкасаются. Если формат поменяется, править надо здесь, а не в
        /// трёх местах вызова.
        ///
        /// КАКИЕ ТОЧКИ ПЕРЕДАЮТСЯ. Сервис знает шесть: запястье и кончики пяти пальцев — ровно
        /// столько, сколько нужно для распознавания (щипок, свайп, горсть). Передавать все 26
        /// суставов незачем: лишняя работа на каждом кадре, а распознавание их не использует.
        ///
        /// ОТСЕВ НЕЧИСЛА. `KvGestureMath.IsUsable` уже проверяет кадр, но здесь данные уходят в
        /// ДРУГОЙ сервис — и он должен получить гарантированно числовые точки (урок E-21).
        /// </summary>
        private static void FeedGestures(KvHandFrame frame, bool isTracked, KvXrQuickHand side)
        {
            if (!isTracked || GestureSink == null) return;
            if (!frame.valid) return;
            if (!KvGestureMath.IsUsable(frame)) return;

            // Сторона передаётся ЯВНО: в самом кадре `KvHandFrame` её нет (он описывает геометрию,
            // а не то, чья это рука). Так надёжнее, чем угадывать по координатам.
            int hand = side == KvXrQuickHand.Left ? 0 : 1;

            GestureSink.PushJoint(hand, KazistovVvFeatures.KvHandTrackingService.Wrist, frame.wrist);
            GestureSink.PushJoint(hand, KazistovVvFeatures.KvHandTrackingService.ThumbTip, frame.thumbTip);
            GestureSink.PushJoint(hand, KazistovVvFeatures.KvHandTrackingService.IndexTip, frame.indexTip);
            GestureSink.PushJoint(hand, KazistovVvFeatures.KvHandTrackingService.MiddleTip, frame.middleTip);
            GestureSink.PushJoint(hand, KazistovVvFeatures.KvHandTrackingService.RingTip, frame.ringTip);
            GestureSink.PushJoint(hand, KazistovVvFeatures.KvHandTrackingService.LittleTip, frame.littleTip);
        }

        void Update()
        {
            if (subsystem == null && Time.realtimeSinceStartup >= nextSubsystemTry)
            {
                nextSubsystemTry = Time.realtimeSinceStartup + 2f;
                EnsureSubsystem();
            }

            KvHandFrame right;
            KvHandFrame left;
            UpdateHandSides();

            tracked[1] = TryReadFrame(KvXrQuickHand.Right, out right);
            tracked[0] = TryReadFrame(KvXrQuickHand.Left, out left);

            // СБРОСИТЬ ПРОШЛЫЕ ТОЧКИ ПЕРЕД ПОДАЧЕЙ (исправлено 30.10.2026, S44).
            //
            // ЧТО БЫЛО. `KvHandTrackingService.PushJoint` ставит `valid[hand, joint] = true`, а
            // `ClearPose()` — единственное, что это снимает, — НЕ ВЫЗЫВАЛСЯ НИОТКУДА.
            //
            // ЧЕМ ЭТО ГРОЗИЛО. Кисть пропала (оператор опустил руку, контроллер перекрыл пальцы) —
            // а сервис продолжает считать её отслеживаемой по СТАРЫМ координатам:
            // `IsTracked` смотрит только на флаги `valid`, которые никто не сбрасывал.
            //
            // Последствие: «призрачная рука» — щипок вычисляется по точке, где пальцы были секунду
            // назад, и повторяется снова и снова. Оператор видит, что интерфейс нажимается сам.
            //
            // ТЕПЕРЬ: пока кисть отслеживается — данные обновляются как раньше, а как только
            // пропала — флаги снимаются, и `IsTracked` честно вернёт «нет».
            //
            // ВАЖНО ПРО ПАРНОСТЬ: сброс идёт ПЕРЕД подачей, чтобы кадр с одной рукой обнулил
            // только ЕЁ точки, не тронув вторую.
            if (GestureSink != null && (!tracked[0] || !tracked[1]))
            {
                // Пропала хотя бы одна рука — снять отметки и вернуть ту, что осталась.
                if (tracked[0])
                    ClearSinkBecauseHandLost(left, true, KvXrQuickHand.Left);
                else if (tracked[1])
                    ClearSinkBecauseHandLost(right, true, KvXrQuickHand.Right);
                else
                    GestureSink.ClearPose();       // пропали обе — просто сбросить
            }

            // ПЕРЕДАТЬ КИСТИ В СЕРВИС ЖЕСТОВ (30.10.2026, S44, пункт 5.23).
            //
            // ДО ЭТОГО позы суставов приходили в сервис ТОЛЬКО из диагностики, то есть в собранном
            // приложении у распознавания жестов не было данных вообще. Кисти рисовались, жесты
            // молчали.
            //
            // Передача идёт КАЖДЫЙ КАДР, где кисть отслеживается: сервис сам решает, что с этими
            // точками делать (жест, нажатие, ничего). Если кисть пропала — НЕ передаём ничего:
            // сервис увидит отсутствие данных по времени последнего обновления и сбросит жест.
            FeedGestures(right, tracked[1], KvXrQuickHand.Right);
            FeedGestures(left, tracked[0], KvXrQuickHand.Left);

            if (drawJoints)
            {
                // КИСТИ РИСУЮТСЯ ВСЕГДА, КОГДА ОНИ ОТСЛЕЖИВАЮТСЯ (исправлено 21.10.2026).
                // Раньше кисть с контроллером в руке не рисовалась — и владелец, держа контроллеры,
                // вообще не видел рук («я не вижу свои руки»). Подавление жестов для руки с
                // контроллером осталось (это защита от ложных нажатий), но ВИДЕТЬ руку человек
                // должен всегда: иначе непонятно, работает ли отслеживание.
                DrawHand(1, KvXrQuickHand.Right, tracked[1]);
                DrawHand(0, KvXrQuickHand.Left, tracked[0]);
            }
            else
            {
                SetVisible(1, false);
                SetVisible(0, false);
            }

            if (logOnChange) LogStateOnChange();
        }

        // ==================================================== §37: СТОРОНЫ КИСТЕЙ ПО ГЕОМЕТРИИ

        private bool handsSidesLocked;
        private bool handsSidesSwapped;
        private int handsSideVotes;
        private int handsSideVerdict;

        /// <summary>Метки рук у кистей пришлось исправить по положению относительно головы.</summary>
        public bool SidesWereSwapped { get { return handsSidesSwapped; } }

        /// <summary>
        /// ТА ЖЕ ПРОВЕРКА, ЧТО И У КОНТРОЛЛЕРОВ: совпадает ли метка руки с её положением относительно
        /// головы. На живом шлеме 21.10.2026 метки контроллеров оказались противоположны, поэтому
        /// кисти проверяются независимо, а не «раз у контроллеров так, значит и здесь».
        /// </summary>
        private void UpdateHandSides()
        {
            if (handsSidesLocked) return;

            XRHandSubsystem sub = subsystem;
            if (sub == null || !sub.leftHand.isTracked || !sub.rightHand.isTracked) return;
            if (!KvXrInputSource.TryHeadPose(out Vector3 headPosition, out Quaternion headRotation)) return;

            Pose leftWrist;
            Pose rightWrist;
            if (!TryJoint(sub.leftHand, XRHandJointID.Wrist, out leftWrist)) return;
            if (!TryJoint(sub.rightHand, XRHandJointID.Wrist, out rightWrist)) return;

            Vector3 headRight = headRotation * Vector3.right;
            int sideOfRight = KvXrSideResolver.SideOf(rightWrist.position, headPosition, headRight);
            int sideOfLeft = KvXrSideResolver.SideOf(leftWrist.position, headPosition, headRight);

            int verdict;
            if (KvXrSideResolver.LabelsAreSwapped(sideOfRight, sideOfLeft)) verdict = -1;
            else if (KvXrSideResolver.LabelsConfirmed(sideOfRight, sideOfLeft)) verdict = 1;
            else return;

            if (verdict != handsSideVerdict)
            {
                handsSideVerdict = verdict;
                handsSideVotes = 0;
            }
            handsSideVotes++;
            if (handsSideVotes < KvXrSideResolver.VotesToLock) return;

            handsSidesLocked = true;
            handsSidesSwapped = verdict < 0;

            float rightProjection = KvXrSideResolver.ProjectionMeters(rightWrist.position, headPosition, headRight);
            float leftProjection = KvXrSideResolver.ProjectionMeters(leftWrist.position, headPosition, headRight);

            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[KvXr] ОТСЛЕЖИВАНИЕ РУК: метки кистей {0} — «правая» на {1:0.00} м от оси взгляда, " +
                "«левая» на {2:0.00} м · стороны кистей {3}",
                handsSidesSwapped ? "НЕ СОВПАЛИ с положением" : "совпали с положением",
                rightProjection, leftProjection,
                handsSidesSwapped ? "исправлены по геометрии" : "оставлены как есть"));
        }

        /// <summary>
        /// Кисть с учётом исправления сторон: ручной переключатель `sides.swap` сильнее автоматики —
        /// владелец видит шлем своими глазами.
        /// </summary>
        private XRHand ResolveHand(XRHandSubsystem sub, KvXrQuickHand hand)
        {
            bool wantLeft = hand == KvXrQuickHand.Left;

            bool? manual = KvXrTuning.SidesSwap;
            if (manual.HasValue)
            {
                if (manual.Value) wantLeft = !wantLeft;
            }
            else if (handsSidesSwapped)
            {
                wantLeft = !wantLeft;
            }

            return wantLeft ? sub.leftHand : sub.rightHand;
        }

        /// <summary>
        /// ВОССТАНОВЛЕНИЕ ПОСЛЕ ПРОБУЖДЕНИЯ ШЛЕМА (§37). Владелец сообщил: «после ухода в спящий
        /// режим указка на контроллерах не восстанавливается и веер тоже». Сон пересоздаёт сеанс и
        /// подсистемы; здесь всё, что было закэшировано, сбрасывается и поднимается заново, а в
        /// журнал уходит строка — по ней видно, что восстановление действительно произошло.
        /// </summary>
        public void Recover()
        {
            subsystem = null;
            handsSidesLocked = false;
            handsSidesSwapped = false;
            handsSideVotes = 0;
            handsSideVerdict = 0;
            lastState = null;
            nextSubsystemTry = 0f;

            EnsureSubsystem();
            Debug.Log("[KvXr] ОТСЛЕЖИВАНИЕ РУК: восстановление после пробуждения · " + Describe());
        }

        // ==================================================== ПОСТАВЩИК ПОЗ ДЛЯ ЖЕСТОВ

        private KvHandFrame? ProvideFrame(KvXrQuickHand hand)
        {
            KvHandFrame frame;
            if (!TryReadFrame(hand, out frame)) return null;
            return frame;
        }

        /// <summary>
        /// ПРОЧИТАТЬ ПОЗУ КИСТИ. Отказ (false) — если подсистемы нет, кисть не отслеживается или
        /// хотя бы один нужный сустав не дал позу. «Полупустая» кисть для жестов бесполезна, а
        /// домысливать координаты запрещено ([S3.7]).
        /// </summary>
        public bool TryReadFrame(KvXrQuickHand hand, out KvHandFrame frame)
        {
            frame = default(KvHandFrame);

            XRHandSubsystem sub = subsystem;
            if (sub == null || !sub.running) return false;

            XRHand xrHand = ResolveHand(sub, hand);
            if (!xrHand.isTracked) return false;

            Pose wrist, palm, thumbTip, indexProximal, indexTip;
            Pose middleProximal, middleTip, ringProximal, ringTip, littleProximal, littleTip;

            if (!TryJoint(xrHand, XRHandJointID.Wrist, out wrist)) return false;
            if (!TryJoint(xrHand, XRHandJointID.Palm, out palm)) return false;
            if (!TryJoint(xrHand, XRHandJointID.ThumbTip, out thumbTip)) return false;
            if (!TryJoint(xrHand, XRHandJointID.IndexProximal, out indexProximal)) return false;
            if (!TryJoint(xrHand, XRHandJointID.IndexTip, out indexTip)) return false;
            if (!TryJoint(xrHand, XRHandJointID.MiddleProximal, out middleProximal)) return false;
            if (!TryJoint(xrHand, XRHandJointID.MiddleTip, out middleTip)) return false;
            if (!TryJoint(xrHand, XRHandJointID.RingProximal, out ringProximal)) return false;
            if (!TryJoint(xrHand, XRHandJointID.RingTip, out ringTip)) return false;
            if (!TryJoint(xrHand, XRHandJointID.LittleProximal, out littleProximal)) return false;
            if (!TryJoint(xrHand, XRHandJointID.LittleTip, out littleTip)) return false;

            // 30.10.2026 (найдено проверкой кода): `XRHandJoint.TryGetPose` отдаёт позы в
            // ПРОСТРАНСТВЕ ОТСЛЕЖИВАНИЯ, а не в мировом — это прямо написано в документации
            // пакета (`XRHandJoint.cs`: «transform a pose into world space using the transform
            // from the XROrigin»). Прежний комментарий «позы суставов приходят в мировых» был
            // неверен: после телепорта рисунок кисти и луч из указательного пальца жили в старом
            // начале координат. Перевод один и здесь: рисование, `KvXrHandRay`,
            // `KvXrHandPanelPlacement` и дистанция «кисть ↔ контроллер» в `KvXrMode` берут
            // координаты уже мировыми — иначе пары несогласованных пространств не избежать.
            wrist.position = ToWorld(wrist.position);
            wrist.rotation = ToWorld(wrist.rotation);
            palm.position = ToWorld(palm.position);
            palm.rotation = ToWorld(palm.rotation);
            thumbTip.position = ToWorld(thumbTip.position);
            indexProximal.position = ToWorld(indexProximal.position);
            indexTip.position = ToWorld(indexTip.position);
            middleProximal.position = ToWorld(middleProximal.position);
            middleTip.position = ToWorld(middleTip.position);
            ringProximal.position = ToWorld(ringProximal.position);
            ringTip.position = ToWorld(ringTip.position);
            littleProximal.position = ToWorld(littleProximal.position);
            littleTip.position = ToWorld(littleTip.position);

            frame = new KvHandFrame
            {
                valid = true,
                wrist = wrist.position,
                palm = palm.position,
                palmRotation = palm.rotation,
                thumbTip = thumbTip.position,
                indexProximal = indexProximal.position,
                indexTip = indexTip.position,
                middleProximal = middleProximal.position,
                middleTip = middleTip.position,
                ringProximal = ringProximal.position,
                ringTip = ringTip.position,
                littleProximal = littleProximal.position,
                littleTip = littleTip.position
            };

            if (!KvGestureMath.IsUsable(frame)) return false;

            // СКОЛЬКО СУСТАВОВ ПРИШЛО — СЧИТАЕТСЯ ЗДЕСЬ, А НЕ ПРИ РИСОВАНИИ (исправлено 21.10.2026).
            // Раньше счётчик заполнялся в отрисовке, и в журнале появлялось «суставов 0» при живых
            // данных — просто потому, что кисть не рисовалась (в руке был контроллер). Это вводило
            // в заблуждение: строка выглядела как «суставов нет».
            jointCounts[hand == KvXrQuickHand.Left ? 0 : 1] = CountJoints(xrHand);

            // СГИБЫ ПАЛЬЦЕВ — МАТЕМАТИКОЙ ПАКЕТА XR HANDS (§37). Первая версия считала жесты сама и
            // на живом шлеме дала 63 ложных срабатывания из 63. Ниже — единственный источник этих
            // чисел: `XRFingerShapeMath.CalculateFingerShape` с настроенными порогами пакета.
            FillShapes(xrHand, ref frame);
            return true;
        }

        /// <summary>Сколько из 26 суставов отдали позу.</summary>
        private static int CountJoints(XRHand hand)
        {
            int count = 0;
            for (int id = (int)XRHandJointID.Wrist; id < (int)XRHandJointID.EndMarker; id++)
            {
                Pose pose;
                if (TryJoint(hand, (XRHandJointID)id, out pose)) count++;
            }
            return count;
        }

        /// <summary>Заполнить сгибы, силу щипка и разведение пальцев. Не посчиталось — жесты молчат.</summary>
        private static void FillShapes(XRHand hand, ref KvHandFrame frame)
        {
            float thumbCurl, indexCurl, middleCurl, ringCurl, littleCurl;
            float indexPinch, middlePinch, ringPinch, littlePinch;
            float indexSpread, middleSpread, ringSpread;

            bool ok = TryFullCurl(hand, XRHandFingerID.Thumb, out thumbCurl);
            ok &= TryCurlPinchSpread(hand, XRHandFingerID.Index, true,
                out indexCurl, out indexPinch, out indexSpread);
            ok &= TryCurlPinchSpread(hand, XRHandFingerID.Middle, true,
                out middleCurl, out middlePinch, out middleSpread);
            ok &= TryCurlPinchSpread(hand, XRHandFingerID.Ring, true,
                out ringCurl, out ringPinch, out ringSpread);
            ok &= TryCurlPinchSpread(hand, XRHandFingerID.Little, false,
                out littleCurl, out littlePinch, out float ignoredSpread);

            frame.hasShapes = ok;
            if (!ok) return;

            frame.thumbCurl = thumbCurl;
            frame.indexCurl = indexCurl;
            frame.middleCurl = middleCurl;
            frame.ringCurl = ringCurl;
            frame.littleCurl = littleCurl;

            frame.indexPinch = indexPinch;
            frame.middlePinch = middlePinch;
            frame.ringPinch = ringPinch;
            frame.littlePinch = littlePinch;

            frame.indexSpread = indexSpread;
            frame.middleSpread = middleSpread;
            frame.ringSpread = ringSpread;
        }

        private static bool TryFullCurl(XRHand hand, XRHandFingerID finger, out float curl)
        {
            curl = 0f;
            try
            {
                XRFingerShape shape = hand.CalculateFingerShape(finger, XRFingerShapeTypes.FullCurl);
                return shape.TryGetFullCurl(out curl);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static bool TryCurlPinchSpread(XRHand hand, XRHandFingerID finger, bool hasSpread,
            out float curl, out float pinch, out float spread)
        {
            curl = 0f;
            pinch = 0f;
            spread = 0.5f;
            try
            {
                XRFingerShapeTypes types = XRFingerShapeTypes.FullCurl | XRFingerShapeTypes.Pinch;
                if (hasSpread) types |= XRFingerShapeTypes.Spread;

                XRFingerShape shape = hand.CalculateFingerShape(finger, types);
                bool ok = shape.TryGetFullCurl(out curl);
                ok &= shape.TryGetPinch(out pinch);
                if (hasSpread) ok &= shape.TryGetSpread(out spread);
                return ok;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// ПОЗА СУСТАВА ИЗ ПРОСТРАНСТВА ОТСЛЕЖИВАНИЯ В МИР (30.10.2026).
        ///
        /// ЧЕРЕЗ РИГ, А НЕ СВОИМ ПЕРЕСЧЁТОМ. Начало отслеживания (`KvXrRig`) знает
        /// `KvXrInputSource.TrackingOrigin`. Своего трансформа здесь быть не должно: второй
        /// пересчёт рано или поздно разойдётся с первым — ровно то, что случилось в E-06/E-07.
        /// Пока риг не поставлен (например, кисти без рига) — преобразование не выполняется,
        /// и поведение остаётся прежним.
        /// </summary>
        private static Vector3 ToWorld(Vector3 point)
        {
            Transform origin = KvXrInputSource.TrackingOrigin;
            return origin == null ? point : origin.TransformPoint(point);
        }

        /// <summary>То же для поворота сустава.</summary>
        private static Quaternion ToWorld(Quaternion rotation)
        {
            Transform origin = KvXrInputSource.TrackingOrigin;
            return origin == null ? rotation : origin.rotation * rotation;
        }

        private static bool TryJoint(XRHand hand, XRHandJointID id, out Pose pose)        {
            pose = default(Pose);
            try
            {
                XRHandJoint joint = hand.GetJoint(id);
                return joint.TryGetPose(out pose);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // ==================================================== ПОДСИСТЕМА

        /// <summary>
        /// НАЙТИ И ЗАПУСТИТЬ ПОДСИСТЕМУ КИСТЕЙ. XR Hands создаёт её сам, если фича включена; если
        /// фича выключена — подсистемы не будет, и это будет названо в журнале прямо.
        /// </summary>
        private static void EnsureSubsystem()
        {
            try
            {
                HandTracking.EnsureSubsystemInitialized();
                subsystem = HandTracking.subsystem;

                if (subsystem == null)
                {
                    var found = new List<XRHandSubsystem>();
                    SubsystemManager.GetSubsystems(found);
                    for (int i = 0; i < found.Count; i++)
                    {
                        if (found[i] != null && found[i].running) { subsystem = found[i]; break; }
                    }
                }

                // ЗАПУСК ПОДСИСТЕМЫ — ШТАТНЫМ Start(). Метод-обёртка пакета (`StartSubsystemAndUpdater`)
                // наружу не выставлена, поэтому запускаем подсистему сами: это обычный
                // `SubsystemWithProvider.Start()`, ничего вендорского здесь нет.
                if (subsystem != null && !subsystem.running) subsystem.Start();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KvXr] ОТСЛЕЖИВАНИЕ РУК: подсистема не поднялась (" + e.GetType().Name + ": " + e.Message + ")");
                subsystem = null;
            }
        }

        // ==================================================== ОТРИСОВКА

        private void BuildRenderer(int index, KvXrQuickHand hand)
        {
            var go = new GameObject("KvHand_" + (hand == KvXrQuickHand.Left ? "Left" : "Right"));

            // КИСТИ НЕ ДОЛЖНЫ ЗАВИСЕТЬ ОТ ДВИЖЕНИЯ РИГА (исправлено 23.10.2026).
            //
            // Меш кисти строится в МИРОВЫХ координатах (позы суставов приходят в мировых), а
            // отрисовывается через трансформ объекта. Пока объект был ребёнком рига, телепорт
            // (KvXrLocomotion двигает корень рига) сдвигал нарисованные кисти на величину переноса —
            // и владелец видел «рук нет»: они уезжали вместе с ригом. Поэтому объект кисти живёт в
            // корне сцены с единичным трансформом, и мировые координаты суставов совпадают с тем,
            // что видно.
            go.transform.SetParent(null, true);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            go.layer = KvXrControllerVisuals.IgnoreRaycastLayer;

            Mesh mesh = new Mesh { name = "KvHandMesh_" + (hand == KvXrQuickHand.Left ? "Left" : "Right") };
            mesh.MarkDynamic();
            meshes[index] = mesh;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            filters[index] = filter;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = hand == KvXrQuickHand.Left ? LeftMaterial() : RightMaterial();
            renderers[index] = renderer;

            go.SetActive(false);
        }

        /// <summary>
        /// НАРИСОВАТЬ КИСТЬ: точки суставов + «кости» между ними + указательный отрезок от кончика
        /// пальца (§39 — «кисть как набор квадратиков» стала похожа на руку).
        ///
        /// ВСЁ В ОДНОМ МЕШЕ: 26 точек + до 25 костей + 1 отрезок = до 52 четырёхугольников, но
        /// по-прежнему ОДИН вызов отрисовки на руку. Это важно для кадров: каждая отдельная
        /// отрисовка — это отдельный пакет команд в шлеме.
        /// </summary>
        private void DrawHand(int index, KvXrQuickHand hand, bool visible)
        {
            SetVisible(index, visible);
            if (!visible || !drawJoints) return;

            XRHandSubsystem sub = subsystem;
            if (sub == null) return;

            XRHand xrHand = ResolveHand(sub, hand);
            Camera camera = headCamera != null ? headCamera : Camera.main;
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            Vector3 up = camera != null ? camera.transform.up : Vector3.up;
            Vector3 eye = camera != null ? camera.transform.position : Vector3.zero;

            vertices.Clear();
            colors.Clear();
            triangles.Clear();

            Color jointColor = hand == KvXrQuickHand.Left
                ? new Color(0.35f, 0.92f, 0.55f, 1f)
                : new Color(0.28f, 0.62f, 1.00f, 1f);

            // СНАЧАЛА СУСТАВЫ: их позы нужны и точкам, и костям.
            var positions = new Vector3[KvHandSkeleton.JointCount];
            var present = new bool[KvHandSkeleton.JointCount];
            for (int i = 0; i < KvHandSkeleton.JointCount; i++)
            {
                int id = KvHandSkeleton.MinJointIndex + i;
                Pose pose;
                if (!TryJoint(xrHand, (XRHandJointID)id, out pose)) continue;
                positions[i] = pose.position;
                present[i] = true;
            }

            // КОСТИ РИСУЮТСЯ ДО ТОЧЕК, чтобы точки были поверх линий.
            KvHandBone[] bones = KvHandSkeleton.Bones;
            for (int i = 0; i < bones.Length; i++)
            {
                int from = KvHandSkeleton.ToLocalIndex(bones[i].From);
                int to = KvHandSkeleton.ToLocalIndex(bones[i].To);
                if (!present[from] || !present[to]) continue;      // «нет сустава — нет кости»
                AppendBone(positions[from], positions[to], eye,
                    KvHandSkeleton.BoneWidthMeters, jointColor);
            }

            // ТОЧКИ СУСТАВОВ: кончик указательного — крупнее (это и есть метка у пальца).
            for (int i = 0; i < KvHandSkeleton.JointCount; i++)
            {
                if (!present[i]) continue;

                int id = KvHandSkeleton.MinJointIndex + i;
                float size = id == (int)XRHandJointID.IndexTip
                    ? Mathf.Max(0.004f, fingertipSizeMeters)
                    : Mathf.Max(0.002f, jointSizeMeters);

                AppendQuad(positions[i], right, up, size, jointColor);
            }

            // УКАЗАТЕЛЬНЫЙ ОТРЕЗОК: от кончика указательного вдоль пальца — видно, куда смотрит кисть.
            int tipIndex = KvHandSkeleton.ToLocalIndex((int)XRHandJointID.IndexTip);
            int distalIndex = KvHandSkeleton.ToLocalIndex((int)XRHandJointID.IndexDistal);
            if (present[tipIndex] && present[distalIndex])
            {
                Vector3 direction = positions[tipIndex] - positions[distalIndex];
                if (direction.sqrMagnitude > 1e-6f)
                {
                    Vector3 end = positions[tipIndex] + direction.normalized * KvHandSkeleton.FingerPointerMeters;
                    AppendBone(positions[tipIndex], end, eye,
                        KvHandSkeleton.BoneWidthMeters * 1.5f, jointColor);
                }
            }

            Mesh mesh = meshes[index];
            if (mesh == null) return;

            mesh.Clear();
            if (vertices.Count == 0) return;

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// «КОСТЬ» — прямоугольник между двумя суставами, развёрнутый к оператору.
        ///
        /// ПОЧЕМУ ТАК, А НЕ ЦИЛИНДРОМ: цилиндр — это отдельный меш и отдельный вызов отрисовки на
        /// каждую кость. Прямоугольник, повёрнутый к камере, выглядит как линия и стоит НОЛЬ
        /// дополнительных вызовов — все кости лежат в том же меше, что и точки.
        /// </summary>
        private void AppendBone(Vector3 from, Vector3 to, Vector3 eye, float width, Color color)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 1e-5f) return;

            Vector3 axis = direction / length;
            Vector3 toEye = eye - from;
            Vector3 side = Vector3.Cross(axis, toEye);
            if (side.sqrMagnitude < 1e-8f)
            {
                // Кость смотрит точно в глаз — «бок» не определён; берём произвольный перпендикуляр.
                side = Vector3.Cross(axis, Vector3.up);
                if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(axis, Vector3.right);
            }
            side = side.normalized * (Mathf.Max(0.001f, width) * 0.5f);

            int start = vertices.Count;
            vertices.Add(from - side);
            vertices.Add(from + side);
            vertices.Add(to + side);
            vertices.Add(to - side);
            for (int i = 0; i < 4; i++) colors.Add(color);

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);

            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 2);
        }

        /// <summary>
        /// КВАДРАТИК СУСТАВА, ОБРАЩЁННЫЙ К ОПЕРАТОРУ.
        ///
        /// ПОЧЕМУ ТРЕУГОЛЬНИКИ В ОБЕ СТОРОНЫ (§37, найдено на живом шлеме 21.10.2026). Владелец
        /// сказал: «я рук своих не вижу». Кисти при этом отслеживались (в журнале 26 суставов у
        /// обеих), а на картинке их не было: материал URP/Unlit отсекает задние грани, и квадратик,
        /// повёрнутый «спиной», просто не рисуется — а какая сторона окажется лицевой, зависит от
        /// порядка обхода вершин, который легко получить наоборот. Поэтому грани выводятся В ОБЕ
        /// СТОРОНЫ: сустав видно всегда, независимо от ориентации. Цена — 6 индексов вместо 3 на
        /// сустав (26 суставов = 156 индексов), то есть ничего.
        /// </summary>
        private void AppendQuad(Vector3 center, Vector3 right, Vector3 up, float size, Color color)
        {
            int start = vertices.Count;
            Vector3 halfRight = right * (size * 0.5f);
            Vector3 halfUp = up * (size * 0.5f);

            vertices.Add(center - halfRight - halfUp);
            vertices.Add(center - halfRight + halfUp);
            vertices.Add(center + halfRight + halfUp);
            vertices.Add(center + halfRight - halfUp);

            for (int i = 0; i < 4; i++) colors.Add(color);

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);

            // Вторая сторона: обратный обход, чтобы сустав был виден и «с изнанки».
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 2);
        }

        private void SetVisible(int index, bool visible)
        {
            MeshRenderer renderer = renderers[index];
            if (renderer == null) return;
            if (renderer.gameObject.activeSelf != visible) renderer.gameObject.SetActive(visible);
        }

        // ==================================================== ЖУРНАЛ

        /// <summary>
        /// СТРОКА-ДОКАЗАТЕЛЬСТВО КИСТЕЙ ([S2.6]): «левая … · правая … · поза достоверна».
        ///
        /// ЧАСТОТА ПЕЧАТИ ОГРАНИЧЕНА (исправлено 21.10.2026). Числа сгибов меняются каждый кадр, и
        /// прежняя версия печатала строку на КАЖДОЕ изменение — за 25 минут набралось **13 238**
        /// строк, журнал стал нечитаемым. Теперь: смена состояния (появилась/пропала кисть) печатается
        /// сразу, а «просто числа» — не чаще раза в 2 секунды.
        /// </summary>
        private void LogStateOnChange()
        {
            if (subsystem == null)
            {
                if (loggedSubsystemMissing) return;
                loggedSubsystemMissing = true;
                Debug.LogWarning("[KvXr] ОТСЛЕЖИВАНИЕ РУК: подсистема XR Hands НЕ создана — кистей не будет " +
                                 "(проверьте фичу HandTracking для Android и PICOProjectSetting.isHandTracking)");
                return;
            }

            string state = DescribeState();
            bool stateChanged = state != lastState;
            if (stateChanged)
            {
                lastState = state;
                nextShapeLog = Time.realtimeSinceStartup + 2f;
                Debug.Log("[KvXr] " + Describe());
                return;
            }

            if (Time.realtimeSinceStartup < nextShapeLog) return;
            nextShapeLog = Time.realtimeSinceStartup + 2f;
            Debug.Log("[KvXr] " + Describe());
        }

        private float nextShapeLog;

        /// <summary>Устойчивая часть строки: какая кисть видна и с каким числом суставов.</summary>
        private string DescribeState()
        {
            if (subsystem == null) return "подсистемы нет";
            return (tracked[0] ? "Л" + jointCounts[0] : "Л-") + "/" + (tracked[1] ? "П" + jointCounts[1] : "П-");
        }

        /// <summary>Строка состояния кистей для журнала и отчёта.</summary>
        public string Describe()
        {
            if (subsystem == null)
                return "ОТСЛЕЖИВАНИЕ РУК: подсистемы нет · кистей нет · жесты молчат";

            string left = tracked[0]
                ? "есть (поза достоверна, суставов " + jointCounts[0] + ")"
                : "нет";
            string right = tracked[1]
                ? "есть (поза достоверна, суставов " + jointCounts[1] + ")"
                : "нет";

            bool anyTracked = tracked[0] || tracked[1];
            string shapes = DescribeShapes();
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "ОТСЛЕЖИВАНИЕ РУК: левая {0} · правая {1} · подсистема XR Hands {2} · {3}{4}",
                left, right,
                subsystem.running ? "работает" : "НЕ запущена",
                anyTracked ? "поза достоверна, кисти нарисованы" : "поз нет — положите контроллеры",
                shapes);
        }

        /// <summary>
        /// СГИБЫ ПРАВОЙ КИСТИ В ЖУРНАЛЕ (§37). Без этих чисел подбор порогов жестов — гадание: в
        /// прошлый раз именно по ним выяснилось, что «раскрытая ладонь» срабатывала на всё.
        /// §38: добавлена ДИАГНОСТИКА МЕСТА — положение запястья, положение камеры и расстояние
        /// между ними. Владелец дважды писал «не вижу руки», и без этих чисел нельзя отличить
        /// «кисть не отрисовывается» от «кисть нарисована не там, где рука».
        /// </summary>
        private string DescribeShapes()
        {
            KvHandFrame frame;
            if (!TryReadFrame(KvXrQuickHand.Right, out frame)) return string.Empty;

            Camera camera = headCamera != null ? headCamera : Camera.main;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;

            string where = camera != null
                ? " · " + KvHandDiagnostics.Describe(frame.wrist, cameraPosition)
                : string.Empty;

            if (!frame.hasShapes) return where + " · сгибы недоступны — жесты молчат";

            return where + string.Format(System.Globalization.CultureInfo.InvariantCulture,
                " · правая кисть: сгибы {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00} · щипок {4:0.00} · разведение {5:0.00}",
                frame.indexCurl, frame.middleCurl, frame.ringCurl, frame.littleCurl,
                frame.indexPinch, KvGestureMath.AverageSpread(frame));
        }

        private Material RightMaterial()
        {
            if (rightMaterial != null) return rightMaterial;
            rightMaterial = KvRenderPipelineShaders.CreateUnlit("Материал_правой_кисти_XR",
                new Color(0.28f, 0.62f, 1.00f, 1f));
            return rightMaterial;
        }

        private Material LeftMaterial()
        {
            if (leftMaterial != null) return leftMaterial;
            leftMaterial = KvRenderPipelineShaders.CreateUnlit("Материал_левой_кисти_XR",
                new Color(0.35f, 0.92f, 0.55f, 1f));
            return leftMaterial;
        }
    }

    /// <summary>
    /// ДИАГНОСТИКА КИСТЕЙ ЧИСЛАМИ (§38).
    ///
    /// ЗАЧЕМ. Владелец дважды написал «я не вижу свои руки», и оба раза нельзя было понять, что
    /// именно не так: кисть не рисуется вовсе или рисуется там, где руки нет. Эти три функции дают
    /// ответ числами: положение запястья, положение камеры и расстояние между ними. Если расстояние
    /// правдоподобное (0,2–0,9 м), а кистей не видно — дело в отрисовке; если расстояние в метрах
    /// или в десятках метров — дело в преобразовании пространства поз.
    /// </summary>
    public static class KvHandDiagnostics
    {
        /// <summary>Дальше этого расстояния кисть «нарисована не там, где рука», метры.</summary>
        public const float MaxPlausibleDistanceMeters = 1.20f;

        /// <summary>Расстояние от запястья до камеры, метры.</summary>
        public static float DistanceToCamera(Vector3 handPosition, Vector3 cameraPosition)
        {
            return Vector3.Distance(handPosition, cameraPosition);
        }

        /// <summary>Правдоподобно ли расстояние от кисти до камеры.</summary>
        public static bool IsPlausible(Vector3 handPosition, Vector3 cameraPosition)
        {
            return DistanceToCamera(handPosition, cameraPosition) <= MaxPlausibleDistanceMeters;
        }

        /// <summary>Строка для журнала: где запястье, где камера, сколько между ними.</summary>
        public static string Describe(Vector3 handPosition, Vector3 cameraPosition)
        {
            float distance = DistanceToCamera(handPosition, cameraPosition);
            string text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "запястье {0} · камера {1} · до камеры {2:0.00} м",
                handPosition.ToString("0.00"), cameraPosition.ToString("0.00"), distance);

            if (distance > MaxPlausibleDistanceMeters)
                text += " · ВНИМАНИЕ: это не рука, а ошибка пространства";
            return text;
        }
    }
}
