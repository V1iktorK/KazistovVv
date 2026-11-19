using System.Globalization;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using KazistovVvFeatures;

namespace KazistovVvAr
{
    /// <summary>СОСТОЯНИЕ НЕПРЕРЫВНОГО ЯКОРЯ ПО МАРКЕРУ (по нему строится честный статус на экране).</summary>
    public enum KvArAnchorState
    {
        /// <summary>Переключатель «якорь: маркер» выключен — работает ПРЕЖНЕЕ поведение сцены.</summary>
        Disabled = 0,

        /// <summary>Мира ещё нет (стол не поставлен) — якорю нечего держать.</summary>
        NoWorld = 1,

        /// <summary>Мир поставлен, но QR ещё ни разу не увидели — ждём маркер.</summary>
        Waiting = 2,

        /// <summary>Маркер виден: поза мира обновляется каждый кадр.</summary>
        Found = 3,

        /// <summary>Маркера нет короткое время: поза держится (dead reckoning от гироскопа).</summary>
        Lost = 4,

        /// <summary>Маркера нет давно: поза держится, но сцена может плыть — это сказано вслух.</summary>
        LostLong = 5
    }

    /// <summary>
    /// МАТЕМАТИКА ЯКОРЯ ПО QR-МАРКЕРУ — чистые функции без сцены и без датчиков (проверяются тестами
    /// на синтетике).
    ///
    /// ЧТО ЗДЕСЬ РЕШАЕТСЯ. Планшет без ARCore-трекинга не отчитывается о своей позе, поэтому «где
    /// в мире лежит стол» известно только из маркера: позу маркера в кадре (`markerInCamera`)
    /// умножаем на позу камеры в мире (`cameraWorld`) и получаем позу маркера в мире. Корень сцены
    /// ставится так, чтобы маркер сцены совпал с измеренным:
    ///
    ///     корень = cameraWorld · markerInCamera · (корень₀ · markerWorld₀)⁻¹
    ///
    /// Из этого следуют два поведения, которые проверяются отдельно:
    ///   • камера отчиталась о движении СОГЛАСОВАННО с маркером → корень возвращается в исходную
    ///     позу (мир стоит, планшет «летает» вокруг);
    ///   • камера НЕ отчиталась (планшет без трекинга) → корень встаёт ровно туда, где маркер
    ///     измерен, то есть виртуальный стол накладывается на реальный QR, а не плывёт за экраном.
    ///
    /// ПОТЕРЯ МАРКЕРА. Поза не обрывается: `DeadReckon` доворачивает корень вокруг позиции камеры
    /// на ту часть поворота, которую не отчитал AR (`UntrackedRotation` = гироскоп − AR). Положение
    /// при этом держится последним известным — честный статус сообщает и то, и другое.
    /// </summary>
    public static class KvMarkerAnchorMath
    {
        /// <summary>ПОЗА МАРКЕРА В МИРЕ: поза камеры в мире, применённая к позе маркера в камере.</summary>
        public static Pose MarkerWorld(Pose cameraWorld, Pose markerInCamera)
        {
            return new Pose(
                cameraWorld.position + cameraWorld.rotation * markerInCamera.position,
                cameraWorld.rotation * markerInCamera.rotation);
        }

        /// <summary>ПОЗА МАРКЕРА ОТНОСИТЕЛЬНО КОРНЯ СЦЕНЫ (постоянна, пока мир не переставлен).</summary>
        public static Pose MarkerRelativeTo(Pose rootPose, Pose markerWorld)
        {
            Quaternion inverse = Quaternion.Inverse(rootPose.rotation);
            return new Pose(inverse * (markerWorld.position - rootPose.position),
                inverse * markerWorld.rotation);
        }

        /// <summary>
        /// ПОЗА КОРНЯ, ПРИ КОТОРОЙ МАРКЕР СЦЕНЫ ЛЕЖИТ В ИЗМЕРЕННОЙ ТОЧКЕ. Это и есть якорь:
        /// `корень · markerRelative = cameraWorld · markerInCamera`.
        /// </summary>
        public static Pose RootPoseFor(Pose cameraWorld, Pose markerInCamera, Pose markerRelativeToRoot)
        {
            Pose markerWorld = MarkerWorld(cameraWorld, markerInCamera);
            Quaternion inverseRotation = Quaternion.Inverse(markerRelativeToRoot.rotation);
            Vector3 inversePosition = inverseRotation * (-markerRelativeToRoot.position);
            return new Pose(
                markerWorld.position + markerWorld.rotation * inversePosition,
                markerWorld.rotation * inverseRotation);
        }

        /// <summary>ДОЛЯ ШАГА ЭКСПОНЕНЦИАЛЬНОГО ФИЛЬТРА: 1 − e^(−dt/tau). tau ≤ 0 — фильтр выключен.</summary>
        public static float Alpha(float deltaTime, float timeConstant)
        {
            if (deltaTime <= 0f) return 0f;
            if (timeConstant <= 0f) return 1f;
            return 1f - Mathf.Exp(-deltaTime / timeConstant);
        }

        /// <summary>СГЛАЖИВАНИЕ ПОЗЫ: позиция — линейно по доле, ориентация — сферполярно.</summary>
        public static Pose Smooth(Pose current, Pose target, float deltaTime,
            float positionTimeConstant, float rotationTimeConstant)
        {
            float positionPart = Alpha(deltaTime, positionTimeConstant);
            float rotationPart = Alpha(deltaTime, rotationTimeConstant);
            if (positionPart <= 0f && rotationPart <= 0f) return current;
            return new Pose(
                Vector3.Lerp(current.position, target.position, positionPart),
                Quaternion.Slerp(current.rotation, target.rotation, rotationPart));
        }

        /// <summary>
        /// НЕПРОТРЕКИРОВАННАЯ ЧАСТЬ ВРАЩЕНИЯ КАМЕРЫ: что видит гироскоп минус то, что уже дал AR
        /// (оба — в кадре камеры). AR отчитал всё → компенсировать нечего; AR мёртв → компенсация
        /// вся от гироскопа. Порядок важен только на больших углах, кадровые дельты малы.
        /// </summary>
        public static Quaternion UntrackedRotation(Quaternion gyroDeltaLocal, Quaternion arDeltaLocal)
        {
            return gyroDeltaLocal * Quaternion.Inverse(arDeltaLocal);
        }

        /// <summary>
        /// ДЕЛЬТА ГИРОСКОПА между двумя сырыми позами датчика, приведённая к кадру Unity.
        ///
        /// Стандартная конверсия Unity — `Euler(90,0,0) · (−x, −y, z, w)`; в ДЕЛЬТЕ постоянная
        /// рамка сокращается, а оставшееся сопряжение `q → (−x, −y, z, w)` — гомоморфизм
        /// (сопряжение на 180° вокруг Z), поэтому дельта считается как сопряжение разности
        /// сырых поз. Свойство гомоморфизма (дельта через промежуточную позу = произведение дельт)
        /// закреплено тестом.
        /// </summary>
        public static Quaternion GyroDelta(Quaternion previousRaw, Quaternion currentRaw)
        {
            Quaternion raw = Quaternion.Inverse(previousRaw) * currentRaw;
            return new Quaternion(-raw.x, -raw.y, raw.z, raw.w);
        }

        /// <summary>
        /// МЁРТВОЕ ПЛАВАНИЕ: поворот камеры, который AR не отчитал, переносится на мир —
        /// вокруг ПОЗИЦИИ камеры (расстояние до камеры сохраняется), в мировых координатах.
        /// </summary>
        public static Pose DeadReckon(Pose root, Pose cameraWorld, Quaternion untrackedRotationLocal)
        {
            Quaternion worldRotation = cameraWorld.rotation * untrackedRotationLocal *
                                      Quaternion.Inverse(cameraWorld.rotation);
            Vector3 position = cameraWorld.position +
                               worldRotation * (root.position - cameraWorld.position);
            return new Pose(position, worldRotation * root.rotation);
        }

        /// <summary>
        /// СОСТОЯНИЕ ЯКОРЯ ПО ФАКТАМ. Порядок честный: выключенный якорь — всегда `Disabled`
        /// (оператор обязан видеть, что следение снято), дальше — мира нет, маркера не было,
        /// маркер свежий, маркер потерян (удержание), маркер потерян давно (сцена может плыть).
        /// Пороги включают границу: ровно на пороге состояние ещё предыдущее.
        /// </summary>
        public static KvArAnchorState StateFor(bool enabled, bool worldPlaced, bool anchored,
            float secondsSinceMarker, float lostHoldSeconds, float lostLongSeconds)
        {
            if (!enabled) return KvArAnchorState.Disabled;
            if (!worldPlaced) return KvArAnchorState.NoWorld;
            if (!anchored) return KvArAnchorState.Waiting;

            float hold = Mathf.Max(0f, lostHoldSeconds);
            float drift = Mathf.Max(hold, lostLongSeconds);
            if (secondsSinceMarker <= hold) return KvArAnchorState.Found;
            if (secondsSinceMarker <= drift) return KvArAnchorState.Lost;
            return KvArAnchorState.LostLong;
        }

        /// <summary>КЛЮЧ СЛОВАРЯ для состояния (перевод — во всех семи языках).</summary>
        public static string StatusKey(KvArAnchorState state)
        {
            switch (state)
            {
                case KvArAnchorState.Disabled: return "ar.anchor.disabled";
                case KvArAnchorState.NoWorld: return "ar.anchor.noworld";
                case KvArAnchorState.Waiting: return "ar.anchor.waiting";
                case KvArAnchorState.Found: return "ar.anchor.found";
                case KvArAnchorState.Lost: return "ar.anchor.lost";
                default: return "ar.anchor.drift";
            }
        }

        /// <summary>РУССКИЙ ШАБЛОН состояния (fallback: если ключа в словаре ещё нет).</summary>
        public static string StatusPattern(KvArAnchorState state)
        {
            switch (state)
            {
                case KvArAnchorState.Disabled:
                    return "якорь: маркер выключен — прежнее поведение (мир не следит за камерой)";
                case KvArAnchorState.NoWorld:
                    return "якорь: ждёт постановки мира по маркеру — «По маркеру»";
                case KvArAnchorState.Waiting:
                    return "якорь: маркер ещё не найден — наведите камеру на QR";
                case KvArAnchorState.Found:
                    return "якорь: маркер найден · дистанция {0} м";
                case KvArAnchorState.Lost:
                    return "якорь: маркер потерян — поза удерживается";
                default:
                    return "якорь: маркер долго нет — сцена может плыть";
            }
        }

        /// <summary>Число для статуса: точка, без локали (иначе «0,85» vs «0.85» ломает сверку).</summary>
        public static string FormatMeters(float meters)
        {
            return meters.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>ГОТОВЫЙ СТАТУС: переведённый шаблон с подставленной дистанцией.</summary>
        public static string StatusText(KvArAnchorState state, float distanceMeters)
        {
            return KvLocExtra.F(StatusKey(state), StatusPattern(state), FormatMeters(distanceMeters));
        }
    }

    /// <summary>
    /// НЕПРЕРЫВНЫЙ ЯКОРЬ СЦЕНЫ ПО QR-МАРКЕРУ (19.11.2026, запрос владельца — «Pokémon GO»).
    ///
    /// ЗАЧЕМ. Планшет-инициатор не ARCore-сертифицирован: плоскостей нет, позы устройства трекинг
    /// не даёт, поэтому сцена «приклеена к экрану» — при движении планшета стол и робот плывут
    /// следом за камерой. Единственный надёжный источник положения мира — QR на столешнице.
    ///
    /// ЧТО ДЕЛАЕТ. С разумной частотой (`detectionIntervalSeconds`, по умолчанию 15 Гц — нижняя
    /// граница бюджета 15–30 Гц) берёт свежий кадр AR-камеры, находит четыре угла QR
    /// (`KvQrMarkerDetector`), считает позу маркера (`KvMarkerPose`), переводит её в позу корня
    /// сцены (`KvMarkerAnchorMath.RootPoseFor`) и применяет позу СГЛАЖЕННО в `LateUpdate` — после
    /// того, как в этом кадре позу записал AR Foundation. Пока маркера нет, поза держится, а
    /// непротрекированное вращение камеры компенсируется гироскопом (`DeadReckon`).
    ///
    /// ПЕРЕКЛЮЧАТЕЛЬ. `anchorEnabled` (команда `ar.anchor.marker`, меню «Сервис») возвращает
    /// ПРЕЖНЕЕ поведение: якорь не читает кадры и не трогает позу — сценой снова владеет то,
    /// что владело до волны. Статус при этом честно говорит «якорь: маркер выключен».
    ///
    /// ПОЧЕМУ `LateUpdate`, А НЕ `Update`. Позу AR-камеры пишет подсистема в своём цикле; копировать
    /// и применять позу мира нужно ПОСЛЕ этого, иначе мир отстаёт на кадр и «дёргается».
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArMarkerAnchor : MonoBehaviour
    {
        /// <summary>Активный экземпляр: дешёвый доступ (гиро-фолбэк, команда меню) без поиска по сцене.</summary>
        public static KvArMarkerAnchor Active { get; private set; }

        /// <summary>AR-постановка: оттуда берутся мир (`world`) и физический размер маркера.</summary>
        public KvArPlacement placement;

        /// <summary>Экранный интерфейс (подсказка о переключении якоря).</summary>
        public KvArUi ui;

        /// <summary>AR-камера: её поза — вход якоря.</summary>
        public Camera arCamera;

        /// <summary>ЯКОРЬ ВКЛЮЧЕН (переключатель «якорь: маркер»). false — прежнее поведение сцены.</summary>
        public bool anchorEnabled = true;

        /// <summary>Как часто сканировать кадр на QR, секунды (0.0667 ≈ 15 Гц).</summary>
        public float detectionIntervalSeconds = 0.0667f;

        /// <summary>Временная постоянная сглаживания позиции, секунды (0 — без сглаживания).</summary>
        public float positionSmoothTime = 0.10f;

        /// <summary>Временная постоянная сглаживания ориентации, секунды.</summary>
        public float rotationSmoothTime = 0.12f;

        /// <summary>Сколько секунд без маркера статус остаётся «найден» (короткая потеря), секунды.</summary>
        public float lostHoldSeconds = 0.5f;

        /// <summary>Сколько секунд без маркера статус становится «сцена может плыть», секунды.</summary>
        public float lostLongSeconds = 3f;

        /// <summary>Компенсировать непротрекированное вращение камеры гироскопом, пока маркера нет.</summary>
        public bool gyroDeadReckoning = true;

        /// <summary>Текущее состояние (для статуса и журнала).</summary>
        public KvArAnchorState State { get; private set; }

        /// <summary>Локализованная строка статуса — её видит оператор в нижней строке.</summary>
        public string StatusLine { get; private set; }

        /// <summary>Дистанция до маркера на последнем принятом кадре, метры.</summary>
        public float DistanceMeters { get; private set; }

        /// <summary>Сколько секунд прошло с последнего принятого маркера.</summary>
        public float SecondsSinceMarker { get; private set; }

        /// <summary>Сколько кадров с маркером принято (для журнала).</summary>
        public int AcceptedFrames { get; private set; }

        /// <summary>Сколько сканов кадра сделано (для журнала).</summary>
        public int ScanCount { get; private set; }

        /// <summary>Причина последнего отказа скана (null — скан прошёл).</summary>
        public string LastDetectReason { get; private set; }

        /// <summary>Якорь зацепился: маркер измерен хотя бы один раз.</summary>
        public bool Anchored { get { return hasRelative; } }

        /// <summary>Якорь ВЕДЁТ позу мира прямо сейчас (гиро-фолбэк обязан уступить).</summary>
        public bool AppliesPose { get { return anchorEnabled && hasRelative && WorldRoot != null; } }

        private bool hasRelative;
        private Pose markerRelative;
        private Pose targetRoot;
        private Pose smoothedRoot;
        private bool markerFresh;
        private float lastMarkerTime;
        private float nextScanTime;
        private float nextCameraSearch;

        private bool hasGyro;
        private Quaternion previousGyro = Quaternion.identity;
        private bool hasCameraRotation;
        private Quaternion previousCameraRotation = Quaternion.identity;

        private ARCameraManager cameraManager;
        private readonly KvMarkerFrame.Buffers frameBuffers = new KvMarkerFrame.Buffers();

        private Transform WorldRoot
        {
            get { return placement != null && placement.world != null ? placement.world.LabRoot : null; }
        }

        void Awake()
        {
            if (placement == null) placement = GetComponent<KvArPlacement>();
            if (placement == null) placement = FindAnyObjectByType<KvArPlacement>();
            if (ui == null) ui = GetComponent<KvArUi>();
            if (ui == null) ui = FindAnyObjectByType<KvArUi>();
            ResolveCamera();
            Active = this;
            State = ComputeState(Time.unscaledTime);
            RefreshStatus();
        }

        void OnEnable()
        {
            Active = this;
            hasGyro = false;
            hasCameraRotation = false;
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            hasGyro = false;
            hasCameraRotation = false;
        }

        /// <summary>
        /// ПЕРЕКЛЮЧАТЬ ЯКОРЬ. false — честное «прежнее поведение»: кадры не читаются, позу не двигаем.
        /// true — следование возобновляется со следующего кадра (счётчики датчиков обнуляются,
        /// иначе первая дельта была бы размером с всё время простоя).
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            KvArAnchorState previous = State;
            anchorEnabled = enabled;
            ResolveCamera();

            if (!enabled)
            {
                State = KvArAnchorState.Disabled;
                Debug.Log("[KvAr] ЯКОРЬ: маркерное следение ВЫКЛЮЧЕНО — прежнее поведение сцены " +
                          "(мир не подстраивается под камеру)");
            }
            else
            {
                hasGyro = false;
                hasCameraRotation = false;
                State = ComputeState(Time.unscaledTime);
                Debug.Log("[KvAr] ЯКОРЬ: маркерное следение ВКЛЮЧЕНО — сцена будет держаться по QR " +
                          "(скан " + ScanRateForLog() + " Гц)");
            }

            RefreshStatus();
            if (State != previous) LogTransition();
            if (ui != null) ui.SetHint(StatusLine);
        }

        /// <summary>
        /// ПРИНЯТЬ КАДР С МАРКЕРОМ. Поза мира пересчитывается так, чтобы маркер сцены лёг
        /// ровно в измеренную точку. Возвращает false, если якорь выключен или мира нет —
        /// кадры в этом случае НЕ копятся (иначе «выключенный» якорь продолжал бы считать).
        /// </summary>
        public bool FeedMarker(KvMarkerPoseResult pose, Pose cameraPose, float now)
        {
            if (!anchorEnabled) return false;
            Transform root = WorldRoot;
            if (root == null || !pose.usable) return false;

            Pose rootPose = new Pose(root.position, root.rotation);
            Pose markerInCamera = new Pose(pose.position, pose.rotation);

            if (!hasRelative)
            {
                // ПЕРВЫЙ КАДР: относительная поза маркера берётся ОТ ТЕКУЩЕГО корня, поэтому
                // якорь стартует без единого прыжка сцены (прыжок при старте = мигание).
                markerRelative = KvMarkerAnchorMath.MarkerRelativeTo(rootPose,
                    KvMarkerAnchorMath.MarkerWorld(cameraPose, markerInCamera));
                hasRelative = true;
                smoothedRoot = rootPose;
            }

            targetRoot = KvMarkerAnchorMath.RootPoseFor(cameraPose, markerInCamera, markerRelative);
            markerFresh = true;
            lastMarkerTime = now;
            DistanceMeters = pose.distance;
            AcceptedFrames++;
            return true;
        }

        /// <summary>
        /// ОДИН ШАГ ЯКОРЯ: мёртвое плавание (если маркер в этом кадре не пришёл), сглаживание,
        /// применение позы к корню сцены, состояние и честный статус. Вызывается из `LateUpdate`
        /// и из тестов (туда же приходят синтетические кадры).
        /// </summary>
        public void Tick(float deltaTime, float now, Pose cameraPose, Quaternion untrackedRotationLocal)
        {
            KvArAnchorState previous = State;
            Transform root = WorldRoot;

            if (!anchorEnabled)
            {
                markerFresh = false;
                State = KvArAnchorState.Disabled;
                FinishTick(previous);
                return;
            }

            if (root == null)
            {
                hasRelative = false;
                markerFresh = false;
                State = KvArAnchorState.NoWorld;
                FinishTick(previous);
                return;
            }

            // МЁРТВОЕ ПЛАВАНИЕ: маркер в этом кадре не пришёл — доворачиваем цель на ту часть
            // вращения камеры, которую не отчитал AR (чистое вращение: положение держится).
            if (hasRelative && !markerFresh && gyroDeadReckoning)
                targetRoot = KvMarkerAnchorMath.DeadReckon(targetRoot, cameraPose,
                    untrackedRotationLocal);

            if (hasRelative)
            {
                smoothedRoot = KvMarkerAnchorMath.Smooth(smoothedRoot, targetRoot, deltaTime,
                    positionSmoothTime, rotationSmoothTime);
                root.SetPositionAndRotation(smoothedRoot.position, smoothedRoot.rotation);
            }

            markerFresh = false;
            SecondsSinceMarker = hasRelative ? Mathf.Max(0f, now - lastMarkerTime) : 0f;
            State = KvMarkerAnchorMath.StateFor(true, true, hasRelative, SecondsSinceMarker,
                lostHoldSeconds, lostLongSeconds);
            FinishTick(previous);
        }

        /// <summary>Строка для журнала: состояние, статус, счётчики и последний отказ скана.</summary>
        public string Describe()
        {
            string line = "якорь: " + State + " · «" + StatusLine + "»" +
                          " · сканов " + ScanCount + " · принято кадров " + AcceptedFrames +
                          " · без маркера " +
                          SecondsSinceMarker.ToString("0.0", CultureInfo.InvariantCulture) + " с" +
                          (string.IsNullOrEmpty(LastDetectReason)
                              ? string.Empty
                              : " · последний отказ: " + LastDetectReason);
            return anchorEnabled ? line : line + " · ВЫКЛЮЧЕН";
        }

        // ---------------------------------------------------------------- кадр камеры (реальный путь)

        /// <summary>Сканировать кадр и, если QR найден, накормить якорь. Вызывается по таймеру.</summary>
        private void ScanOnce()
        {
            ScanCount++;
            if (cameraManager == null && Time.unscaledTime >= nextCameraSearch)
            {
                // Поиск — не каждый кадр: 1 раз в секунду, пока менеджера нет.
                nextCameraSearch = Time.unscaledTime + 1f;
                cameraManager = FindAnyObjectByType<ARCameraManager>();
            }
            if (cameraManager == null)
            {
                LastDetectReason = KvMarkerFrame.NoCameraReason;
                return;
            }

            ResolveCamera();
            if (arCamera == null)
            {
                LastDetectReason = "нет AR-камеры — позу маркера не перенести в мир";
                return;
            }

            float markerSize = placement != null && placement.markerSizeMeters > 0f
                ? placement.markerSizeMeters
                : 0.10f;

            KvMarkerPoseResult pose;
            string reason;
            if (!KvMarkerFrame.TryPoseFromCamera(cameraManager, markerSize, frameBuffers,
                    false, out pose, out reason))
            {
                LastDetectReason = reason;
                return;
            }

            LastDetectReason = null;
            Pose cameraPose = new Pose(arCamera.transform.position, arCamera.transform.rotation);
            FeedMarker(pose, cameraPose, Time.unscaledTime);
        }

        /// <summary>Найти AR-камеру (один раз; повтор — только когда её ещё нет).</summary>
        private void ResolveCamera()
        {
            if (arCamera == null) arCamera = KvArRigBridge.FindArCamera();
        }

        /// <summary>ПОЗА КАМЕРЫ ДЛЯ ЯКОРЯ. Нет камеры — поза нулевая (в AR-сцене камера есть).</summary>
        private Pose CurrentCameraPose()
        {
            if (arCamera == null) return Pose.identity;
            return new Pose(arCamera.transform.position, arCamera.transform.rotation);
        }

        /// <summary>
        /// НЕПРОТРЕКИРОВАННАЯ ЧАСТЬ ВРАЩЕНИЯ ЭТОГО КАДРА: гироскоп (сырые позы приводятся к кадру
        /// Unity) минус то, что уже отчитал AR. Счётчики датчиков держатся здесь, а не в `Tick`, —
        /// `Tick` тестируется синтетикой и датчиков не касается.
        /// </summary>
        private Quaternion ReadUntrackedRotation(Pose cameraPose)
        {
            Quaternion gyroDelta = Quaternion.identity;
            // `Input.gyro` на Android включается в `KvArRuntime.Awake` (без разрешений);
            // отдельного «available» у `Gyroscope` в Unity нет — фактом доступности считаем
            // включённый датчик (на настольной платформе он молчит, дельта остаётся нулевой).
            if (gyroDeadReckoning && Input.gyro.enabled)
            {
                Quaternion raw = Input.gyro.attitude;
                if (hasGyro) gyroDelta = KvMarkerAnchorMath.GyroDelta(previousGyro, raw);
                previousGyro = raw;
                hasGyro = true;
            }

            Quaternion arDelta = Quaternion.identity;
            if (hasCameraRotation)
                arDelta = Quaternion.Inverse(previousCameraRotation) * cameraPose.rotation;
            previousCameraRotation = cameraPose.rotation;
            hasCameraRotation = true;

            return KvMarkerAnchorMath.UntrackedRotation(gyroDelta, arDelta);
        }

        /// <summary>
        /// КАДР: скан по таймеру, потом поза. `LateUpdate` — после того, как AR Foundation
        /// записал позу камеры в этом кадре (иначе мир отстаёт на кадр и дрожит).
        /// </summary>
        void LateUpdate()
        {
            if (anchorEnabled && WorldRoot != null && Time.unscaledTime >= nextScanTime)
            {
                nextScanTime = Time.unscaledTime + Mathf.Max(0.033f, detectionIntervalSeconds);
                ScanOnce();
            }

            Pose cameraPose = CurrentCameraPose();
            Tick(Time.unscaledDeltaTime, Time.unscaledTime, cameraPose,
                ReadUntrackedRotation(cameraPose));
        }

        // ---------------------------------------------------------------- статус и журнал

        private KvArAnchorState ComputeState(float now)
        {
            return KvMarkerAnchorMath.StateFor(anchorEnabled, WorldRoot != null, hasRelative,
                hasRelative ? Mathf.Max(0f, now - lastMarkerTime) : 0f,
                lostHoldSeconds, lostLongSeconds);
        }

        private void RefreshStatus()
        {
            StatusLine = KvMarkerAnchorMath.StatusText(State, DistanceMeters);
        }

        private void FinishTick(KvArAnchorState previous)
        {
            RefreshStatus();
            if (State != previous) LogTransition();
        }

        /// <summary>ЧЕСТНЫЙ ЖУРНАЛ: переход состояния печатается с числом (секунды/дистанция).</summary>
        private void LogTransition()
        {
            string seconds = SecondsSinceMarker.ToString("0.0", CultureInfo.InvariantCulture);
            switch (State)
            {
                case KvArAnchorState.Found:
                    Debug.Log("[KvAr] ЯКОРЬ: маркер найден · дистанция " +
                              KvMarkerAnchorMath.FormatMeters(DistanceMeters) +
                              " м · сцена закреплена в пространстве (сканов " + ScanCount +
                              ", кадров с маркером " + AcceptedFrames + ")");
                    break;
                case KvArAnchorState.Lost:
                    Debug.LogWarning("[KvAr] ЯКОРЬ: маркер потерян — поза удерживается · " + seconds +
                                     " с без маркера" + ReasonSuffix());
                    break;
                case KvArAnchorState.LostLong:
                    Debug.LogWarning("[KvAr] ЯКОРЬ: маркер долго нет — сцена может плыть · " + seconds +
                                     " с без маркера · наведите камеру на QR" + ReasonSuffix());
                    break;
                case KvArAnchorState.Waiting:
                    Debug.Log("[KvAr] ЯКОРЬ: маркер ещё не найден — наведите камеру на QR " +
                              "(мир поставлен, якорь пока не зацепился)");
                    break;
                case KvArAnchorState.NoWorld:
                    Debug.Log("[KvAr] ЯКОРЬ: мира нет — якорь ждёт постановки по маркеру");
                    break;
                case KvArAnchorState.Disabled:
                    Debug.Log("[KvAr] ЯКОРЬ: маркерное следение выключено — прежнее поведение сцены");
                    break;
            }

        }

        /// <summary>Причина отказа скана в строку журнала (пусто — причины нет).</summary>
        private string ReasonSuffix()
        {
            return string.IsNullOrEmpty(LastDetectReason)
                ? string.Empty
                : " · последний отказ скана: " + LastDetectReason;
        }

        /// <summary>Частота скана для журнала, Гц.</summary>
        public string ScanRateForLog()
        {
            float interval = Mathf.Max(0.033f, detectionIntervalSeconds);
            return (1f / interval).ToString("0", CultureInfo.InvariantCulture);
        }
    }
}
