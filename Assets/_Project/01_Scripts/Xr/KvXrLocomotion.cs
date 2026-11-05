using UnityEngine;
using UnityEngine.InputSystem;
using KazistovVvFeatures;

namespace KazistovVvXr
{
    /// <summary>
    /// ПЕРЕМЕЩЕНИЕ ОПЕРАТОРА В ВИРТУАЛЬНОМ ПРОСТРАНСТВЕ (решение владельца [D2], этап 2 плана XR).
    ///
    /// ДВА СПОСОБА, ОБА — ПО РЕШЕНИЮ ВЛАДЕЛЬЦА:
    ///   • ТЕЛЕПОРТ (по умолчанию) — прицел из контроллера, подтверждение курком, мгновенный
    ///     перенос на выбранное место. Не укачивает: это главная причина, почему телепорт
    ///     выбран основным для человека, который впервые надел шлем.
    ///   • ПЛАВНЫЙ ХОД (включается в настройках) — непрерывное движение по стику.
    ///
    /// ЧТО ЗДЕСЬ ГЛАВНЫЙ РИСК. Плавный ход в VR — источник «укачивания»: движения не
    /// совпадают с тем, что чувствует вестибулярный аппарат. Поэтому скорость жёстко
    /// ограничена (0,5…3 м/с), а режим по умолчанию — телепорт. Это не перестраховка,
    /// а прямое следствие решения [D2].
    ///
    /// ПОВОРОТ. С 02.11.2026 (пункт 1 задания владельца) по умолчанию — ПЛАВНЫЙ: скорость поворота
    /// пропорциональна отклонению стика (90°/с при полном отклонении). Владелец сказал прямо:
    /// «мне нужно нормальное управление и перемещение как во всех остальных играх», — ступенчатые
    /// шаги его не устроили. Ступени остались вторым режимом (`useSmoothTurn = false`): в VR
    /// плавный поворот — самый частый источник укачивания, и вернуть «шаги» надо одной галочкой,
    /// а не новой правкой кода. Поворот идёт вокруг ГОЛОВЫ, а не вокруг центра рига — иначе
    /// оператора «уводит» вбок.
    ///
    /// СТИКИ. Левый — перемещение, правый — поворот: то же распределение, что в VR-играх. Обратное
    /// (`useRightStickForMove`) остаётся настройкой, но и тогда ход и поворот садятся на РАЗНЫЕ
    /// стики — одно действие никогда не наезжает на другое.
    ///
    /// Компонент вешается на корень XR-рига (на нём же — камера шлема).
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrLocomotion : MonoBehaviour
    {
        /// <summary>
        /// ПЛАВНЫЙ ПОВОРОТ ВМЕСТО ШАГОВ (по умолчанию включён — пункт 1 задания владельца, 02.11.2026).
        ///
        /// ПОЧЕМУ ПЛАВНЫЙ. Владелец: «меня не устраивает поворот по ±45 градусов на левый сосок —
        /// убираем. Мне нужно нормальное управление и перемещение как во всех остальных играх».
        /// Ступени разворачивают рывком и «перескакивают» цель; плавный поворот повторяет движение
        /// руки, как камера в обычной игре.
        ///
        /// ПОЧЕМУ ФЛАГ, А НЕ ПРОСТО УДАЛИТЬ СТАРЫЙ РЕЖИМ. Плавный поворот в VR укачивает сильнее
        /// всего остального: картинка едет, а вестибулярный аппарат молчит. Если на живом шлеме
        /// владельцу не понравится, «шаги» должны включаться одной галочкой в инспекторе, без
        /// переписывания кода и новой сборки APK.
        /// </summary>
        public bool useSmoothTurn = true;

        /// <summary>
        /// СКОРОСТЬ ПЛАВНОГО ПОВОРОТА при ПОЛНОМ отклонении стика, градусы в секунду.
        ///
        /// ПОЧЕМУ 90. Это привычный диапазон VR-игр (обычно 60…120°/с): разворот на 180° занимает
        /// две секунды и не даёт мгновенного «кувырка» картинки, а полное отклонение стика
        /// позволяет пройти нужный угол коротким движением. Медленнее — оператор ждёт,
        /// быстрее — возвращается то укачивание, из-за которого делали шаги.
        /// </summary>
        public float smoothTurnDegreesPerSecond = 90f;

        /// <summary>
        /// МЁРТВАЯ ЗОНА СТИКА ПОВОРОТА, доля отклонения (0,15 — как у хода).
        ///
        /// ЗАЧЕМ. У шлемовых стиков заметный дребезг в нуле. Без зоны камера «плывёт» при
        /// опущенном пальце — это и раздражает, и укачивает. 0,15 совпадает с зоной хода,
        /// чтобы «стик до упора» означал одно и то же в обоих действиях.
        /// </summary>
        public float smoothTurnDeadzone = 0.15f;

        /// <summary>Шаг поворота вокруг головы, градусы. Работает ТОЛЬКО при useSmoothTurn = false.</summary>
        public float snapTurnDegrees = 30f;

        /// <summary>Порог стика для шага поворота. Работает ТОЛЬКО при useSmoothTurn = false.</summary>
        public float snapTurnThreshold = 0.7f;

        /// <summary>Пауза между шагами поворота, секунды. Работает ТОЛЬКО при useSmoothTurn = false.</summary>
        public float snapTurnCooldown = 0.25f;

        /// <summary>
        /// СТИК ПЕРЕМЕЩЕНИЯ: true — правый, false — левый (по умолчанию левый, как в VR-играх).
        ///
        /// ПОВОРОТ ПРИ ЭТОМ УХОДИТ НА ДРУГОЙ СТИК (см. TurnStick): одно действие никогда не
        /// наезжает на другое, поэтому и левша, и шлем с перепутанными контроллерами остаются
        /// с полным управлением.
        /// </summary>
        public bool useRightStickForMove = false;

        /// <summary>Порог нажатия курка для телепорта/подтверждения.</summary>
        public float triggerThreshold = 0.5f;

        /// <summary>Слои, по которым разрешён телепорт. Пусто — все, кроме слоя самого рига.</summary>
        public LayerMask groundMask = ~0;

        /// <summary>Максимальный наклон поверхности для телепорта, градусы (на стену не телепортируемся).</summary>
        public float maxSlopeDegrees = 45f;

        /// <summary>Цвет метки телепорта, когда место подходит.</summary>
        public Color validMarkerColor = new Color(0.20f, 0.85f, 0.35f, 0.75f);

        /// <summary>Цвет метки телепорта, когда место НЕ подходит (склон, ничего не найдено).</summary>
        public Color invalidMarkerColor = new Color(0.90f, 0.25f, 0.20f, 0.60f);

        /// <summary>
        /// ОГРАНИЧЕНИЕ ОДНОГО КАДРА ДЛЯ ПЛАВНОГО ПОВОРОТА, секунды.
        ///
        /// ЗАЧЕМ. После выхода шлема из сна, подгрузки ресурса или просадки кадра `Time.deltaTime`
        /// может быть 0,2…0,5 с. При 90°/с это 18…45° за ОДИН кадр — ровно тот рывок, из-за
        /// которого ступенчатый поворот и убирали. Ограничение оставляет «скачок» не больше 9°,
        /// а следующий кадр продолжает поворот с нормальной скоростью.
        /// </summary>
        private const float MaxTurnFrameSeconds = 0.1f;

        private Transform rigRoot;
        private Transform head;
        private KvXrUiPointer pointer;
        private float lastSnapTurnTime = -10f;
        private bool teleportStickWasDown;
        private GameObject teleportMarker;
        private bool teleportValid;
        private Vector3 teleportPoint;

        /// <summary>Прицел телепорта сейчас наведён на допустимое место.</summary>
        public bool TeleportTargetValid { get { return teleportValid; } }

        /// <summary>Точка, куда уйдёт оператор при подтверждении (мировые координаты).</summary>
        public Vector3 TeleportPoint { get { return teleportPoint; } }

        /// <summary>Сколько телепортов выполнено — для журнала и диагностики.</summary>
        public int TeleportCount { get; private set; }

        /// <summary>Сколько шагов поворота выполнено (только в ступенчатом режиме).</summary>
        public int SnapTurnCount { get; private set; }

        /// <summary>
        /// Сколько секунд оператор поворачивался ПЛАВНО — метрика «укачивания», как у хода.
        ///
        /// ЗАЧЕМ ОТДЕЛЬНЫЙ СЧЁТЧИК. `SnapTurnCount` считает шаги и в плавном режиме всегда 0;
        /// по нему нельзя понять, крутил ли оператор камеру вообще. Время поворота показывает
        /// это честно и в тех же единицах, что `SmoothMovingSeconds`.
        /// </summary>
        public float SmoothTurnSeconds { get; private set; }

        /// <summary>Сколько секунд оператор двигался плавным ходом — метрика «укачивания».</summary>
        public float SmoothMovingSeconds { get; private set; }

        /// <summary>ЖИВАЯ ЛОКОМОЦИЯ (для виньетки и подсказок): одна на сцену.</summary>
        public static KvXrLocomotion Active { get; private set; }

        /// <summary>ТЕКУЩАЯ скорость плавного хода, м/с — для виньетки комфорта (0 в покое).</summary>
        public float SmoothSpeedMetersPerSecond { get { return smoothVelocity.magnitude; } }

        /// <summary>0…1: доля скорости от максимума — виньетка нарастает пропорционально.</summary>
        public float SmoothSpeedRatio
        {
            get
            {
                float max = Mathf.Max(0.01f, KvXrSettings.GetSmoothSpeed());
                return Mathf.Clamp01(smoothVelocity.magnitude / max);
            }
        }

        private Vector3 smoothVelocity;

        void Awake()

        {
            rigRoot = transform;
            Active = this;
        }

        void OnEnable()
        {
            KvXrSettingsChanged.SettingsChanged += OnSettingsChanged;
        }

        void OnDisable()
        {
            KvXrSettingsChanged.SettingsChanged -= OnSettingsChanged;
            if (teleportMarker != null) teleportMarker.SetActive(false);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void OnSettingsChanged()
        {
            // Смена способа перемещения в настройках должна убирать метку телепорта немедленно,
            // иначе она остаётся висеть в воздухе при плавном ходе — выглядит как дефект.
            if (KvXrSettings.GetMoveMode() != KvXrMoveMode.Teleport && teleportMarker != null)
                teleportMarker.SetActive(false);
        }

        void Update()
        {
            if (rigRoot == null) rigRoot = transform;
            if (head == null) head = FindHead();

            // СТИК ЗАНЯТ ВВОДОМ (§41). Пока открыто колесо символов, стик принадлежит ему: иначе
            // выбор символа ещё и разворачивал бы оператора — стик в шлеме это поворот и ход.
            if (StickOwnedByInput()) return;

            // РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ (п.9, 02.11.2026): пока оператор двигает точку стиком,
            // он СТОИТ. Иначе точка уезжала бы вместе с ним — цель и наблюдатель двигались бы
            // одновременно, и попасть в точку повторно было бы невозможно.
            if (IsPointMoveActive()) return;

            Quaternion rigYaw = Quaternion.Euler(0f, rigRoot.eulerAngles.y, 0f);

            HandleTurn();
            if (KvXrSettings.GetMoveMode() == KvXrMoveMode.Teleport)
                HandleTeleport(rigYaw);
            else
                HandleSmooth(rigYaw);
        }

        /// <summary>
        /// ЗАБРАЛ ЛИ КТО-ТО СТИК. Спрашивается у панели ввода, а не решается здесь: правило захвата
        /// одно (`KvXrWheelLayout.CapturesStick`) и проверено тестом.
        /// </summary>
        private static bool StickOwnedByInput()
        {
            KvXrGroupPanel panel = KvXrGroupPanel.Active;
            return panel != null && panel.StickCaptured;
        }

        // ------------------------------------------------------------------ поворот

        /// <summary>
        /// ВЫБОР РЕЖИМА ПОВОРОТА — в одном месте. Оба режима читают ОДИН стик и крутят риг вокруг
        /// ГОЛОВЫ через один метод, поэтому развилка идёт здесь, а не в `Update`.
        /// </summary>
        private void HandleTurn()
        {
            Vector2 stick = TurnStick;
            if (useSmoothTurn)
                HandleSmoothTurn(stick);
            else
                HandleSnapTurn(stick);
        }

        /// <summary>
        /// СТИК ПЕРЕМЕЩЕНИЯ. По умолчанию ЛЕВЫЙ — как во всех VR-играх, где правая рука целится,
        /// а левая ведёт оператора. Телепорт читает ЭТОТ ЖЕ стик: иначе при включённом
        /// `useRightStickForMove` ход и телепорт оказались бы на разных стиках — и «перемещение»
        /// в двух способах означало бы разные руки.
        /// </summary>
        private Vector2 MoveStick
        {
            get { return useRightStickForMove ? KvXrInputSource.RightStick : KvXrInputSource.LeftStick; }
        }

        /// <summary>
        /// СТИК ПОВОРОТА — ВСЕГДА ПРОТИВОПОЛОЖНЫЙ СТИКУ ХОДА.
        ///
        /// ПОЧЕМУ НЕ «ВСЕГДА ПРАВЫЙ». Пока поворот был жёстко правым, включение
        /// `useRightStickForMove` клало ход и поворот на ОДИН стик: оператор шёл бы и
        /// одновременно разворачивался. Противоположный стик гарантирует, что действия
        /// не пересекаются ни при какой настройке.
        /// </summary>
        private Vector2 TurnStick
        {
            get { return useRightStickForMove ? KvXrInputSource.LeftStick : KvXrInputSource.RightStick; }
        }

        /// <summary>
        /// ПЛАВНЫЙ ПОВОРОТ: угол пропорционален отклонению стика И времени кадра.
        ///
        /// ФОРМУЛА: угол = отклонение × скорость × deltaTime. Именно так поворачивают камеру в
        /// обычных играх: стик наполовину — вдвое медленнее, полное отклонение — 90°/с. Поворот
        /// кадровый, поэтому итог от частоты кадров не зависит: за секунду накопится ровно 90°,
        /// сколько бы кадров в ней ни было.
        /// </summary>
        private void HandleSmoothTurn(Vector2 stick)
        {
            // МЁРТВАЯ ЗОНА — ДО РАСЧЁТА УГЛА. Сырой стик при отпущенном пальце даёт 0,03…0,08,
            // и камера «плыла» бы сама по себе. ApplyDeadzone вычитает зону с пересчётом остатка,
            // поэтому на её границе нет ступеньки: скорость нарастает от нуля плавно.
            float axis = ApplyDeadzone(new Vector2(stick.x, 0f), smoothTurnDeadzone).x;
            if (Mathf.Abs(axis) < 0.001f) return;

            // ОГРАНИЧЕНИЕ СКАЧКА ВРЕМЕНИ — см. MaxTurnFrameSeconds: один «долгий» кадр не должен
            // провернуть оператора на десятки градусов.
            float dt = Mathf.Min(Time.deltaTime, MaxTurnFrameSeconds);

            float degrees = axis * smoothTurnDegreesPerSecond * dt;
            if (Mathf.Abs(degrees) < 1e-4f) return;

            RotateRigAroundHead(degrees);
            SmoothTurnSeconds += dt;
        }

        /// <summary>
        /// СТУПЕНЧАТЫЙ ПОВОРОТ — прежний режим, включается `useSmoothTurn = false`.
        ///
        /// Оставлен намеренно: если плавный поворот укачает, владелец вернёт «шаги» галочкой.
        /// Порог 0,7 и пауза 0,25 с не дают одному касанию стика выдать несколько шагов подряд.
        /// </summary>
        private void HandleSnapTurn(Vector2 stick)
        {
            if (Time.time - lastSnapTurnTime < snapTurnCooldown) return;
            if (Mathf.Abs(stick.x) < snapTurnThreshold) return;

            float dir = stick.x > 0f ? 1f : -1f;
            RotateRigAroundHead(dir * snapTurnDegrees);
            lastSnapTurnTime = Time.time;
            SnapTurnCount++;
        }

        /// <summary>
        /// ПОВЕРНУТЬ РИГ ВОКРУГ ГОЛОВЫ ОПЕРАТОРА. Угол со знаком: плюс — вправо, минус — влево.
        ///
        /// ПОЧЕМУ ОТДЕЛЬНЫЙ МЕТОД. Разбор дефекта «всё крутится» ниже описывает геометрию, нужную
        /// ОБОИМ режимам. Пока поворот был один, разбор жил внутри него; с плавным режимом точек
        /// поворота стало две, и повторять объяснение в каждой — значит однажды починить в одной
        /// и забыть во второй.
        ///
        /// ИСПРАВЛЕНО 01.11.2026 (S44, живой шлем): ПОВОРОТ ВОКРУГ КАМЕРЫ = ЗАМКНУТЫЙ КРУГ.
        ///
        /// СИМПТОМ: «всё крутится, картинка не стабильная». В журнале курс шлема прыгал
        /// 0.0° → 357° → 219° без участия оператора.
        ///
        /// ЧТО БЫЛО: `pivot = head.position` — мировая позиция КАМЕРЫ, которая лежит внутри рига
        /// и уже включает его позицию и поворот. `RotateAround` крутит риг вокруг точки, которая
        /// сама привязана к ригу: после каждого поворота камера уезжает, точка опоры уезжает
        /// вместе с ней, и следующий поворот крутит уже вокруг нового места. Это раскручивает
        /// систему — ровно то «всё крутится», что видит оператор.
        ///
        /// ТЕПЕРЬ: точку опоры берём в МИРОВЫХ координатах, но НЕ от камеры, а от позы головы,
        /// переведённой в локальные координаты рига. Голова при таком повороте остаётся на месте,
        /// а не «наматывается по спирали».
        ///
        /// ДЛЯ ПЛАВНОГО РЕЖИМА ЭТО ОСОБЕННО ВАЖНО: он вызывает поворот каждый кадр. Если бы точка
        /// опоры «уезжала», за секунду набежали бы десятки микросдвигов, и оператора унесло бы
        /// по спирали — ошибка накапливалась бы, а не исчезала.
        /// </summary>
        private void RotateRigAroundHead(float degrees)
        {
            if (rigRoot == null) rigRoot = transform;

            Vector3 pivot = rigRoot.position;

            Vector3 trackedPosition;
            Quaternion trackedRotation;
            if (KvXrInputSource.TryHeadPose(out trackedPosition, out trackedRotation))
            {
                Vector3 local = rigRoot.InverseTransformPoint(trackedPosition);
                local.y = 0f;
                pivot = rigRoot.TransformPoint(local);
            }
            else if (head != null)
            {
                // Трекинга нет — берём локальную позицию камеры: она уже внутри рига,
                // поэтому двойного применения нет.
                Vector3 local = rigRoot.InverseTransformPoint(head.position);
                local.y = 0f;
                pivot = rigRoot.TransformPoint(local);
            }

            rigRoot.RotateAround(pivot, Vector3.up, degrees);
        }

        // ------------------------------------------------------------------ телепорт

        private void HandleTeleport(Quaternion rigYaw)
        {
            // ТЕЛЕПОРТ НА СТИКЕ ПЕРЕМЕЩЕНИЯ (стандарт Quest/PICO, 29.10.2026).
            // Владелец: «кнопки под указательный палец + лазеры заняты под зелёный и красный лазер».
            // Курок теперь ТОЛЬКО для лазеров (красный/зелёный), телепорт — на стике:
            //   • толкнуть стик перемещения вперёд — показать метку телепорта;
            //   • отпустить стик — перенестись на метку.
            // Это стандарт Quest/PICO: стик = передвижение, курок = действие/выбор.
            // Стик тот же, что и у плавного хода (`MoveStick`), — «перемещение» не может означать
            // в одном способе одну руку, а в другом другую.
            //
            // ПОКА ЛУЧ НАВЕДЁН НА ПАНЕЛЬ — НЕ ТЕЛЕПОРТИРОВАТЬ (правило §37).
            if (pointer == null) pointer = UnityEngine.Object.FindAnyObjectByType<KvXrUiPointer>();
            if (pointer != null && pointer.AimingAtUi)
            {
                SetMarker(false, Vector3.zero, rigYaw);
                teleportStickWasDown = false;
                return;
            }

            // РУКА ТЕЛЕПОРТА — ТА ЖЕ, ЧТО У ЛУЧА (§37).
            bool haveAim = KvXrInputSource.TryRayAimPose(out Vector3 handPos, out Quaternion handRot);
            if (!haveAim)
            {
                SetMarker(false, Vector3.zero, rigYaw);
                teleportStickWasDown = false;
                return;
            }

            Vector3 dir = handRot * Vector3.forward;
            float maxDistance = KvXrSettings.GetTeleportDistance();
            teleportValid = AimGroundPoint(handPos, dir, maxDistance,
                out Vector3 ground, out Vector3 normal);

            teleportPoint = ground;

            // СТИК ПЕРЕМЕЩЕНИЯ: толкнуть вперёд — показать метку, отпустить — телепорт.
            Vector2 stick = MoveStick;
            bool stickDown = stick.y > 0.5f;   // порог «вперёд»
            SetMarker(stickDown && teleportValid, ground, rigYaw, normal);

            // Телепорт при ОТПУСКАНИИ стика (как в Quest: толкнул → отпустил → перенёсся).
            if (!stickDown && teleportStickWasDown && teleportValid)
                TeleportTo(ground);
            teleportStickWasDown = stickDown;
        }

        /// <summary>
        /// Точка на полу под прицелом. Проверяются три вещи: попадание в коллайдер,
        /// наклон поверхности и то, что точка не «под ногами» (иначе оператор телепортируется
        /// в себя и не заметит этого).
        /// </summary>
        public bool AimGroundPoint(Vector3 origin, Vector3 direction, float maxDistance,
            out Vector3 point, out Vector3 normal)
        {
            point = origin + direction.normalized * maxDistance;
            normal = Vector3.up;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance,
                    groundMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
            }
            else
            {
                // Промах: пробуем плоскость пола на нулевой высоте — оператор часто целится
                // в пустоту между объектами, и «ничего не нашлось» здесь излишне строго.
                Plane floor = new Plane(Vector3.up, Vector3.zero);
                if (!floor.Raycast(new Ray(origin, direction), out float distance)) return false;
                if (distance <= 0f || distance > maxDistance) return false;
                point = origin + direction.normalized * distance;
                normal = Vector3.up;
            }

            float slope = Vector3.Angle(normal, Vector3.up);
            if (slope > maxSlopeDegrees) return false;
            if (Vector3.Distance(point, origin) < 0.25f) return false;   // слишком близко — это «в себя»
            return true;
        }

        /// <summary>Перенести риг так, чтобы точка пола оказалась под головой оператора.</summary>
        public void TeleportTo(Vector3 groundPoint)
        {
            if (rigRoot == null) rigRoot = transform;

            // ИСПРАВЛЕНО 01.11.2026 (S44, живой шлем): ЗАМКНУТЫЙ КРУГ «РИГ ↔ КАМЕРА».
            //
            // СИМПТОМ У ОПЕРАТОРА (дословно): «всё крутится», «картинка не стабильная».
            // ЖУРНАЛ ШЛЕМА, три строки подряд после старта:
            //     панель от глаз 1.34 м  →  3.68 м  →  9.92 м
            //     панель в (0.03, 1.17, 0.65) — НЕ МЕНЯЕТСЯ ни в одной строке
            // То есть панель стоит на месте, а камера уезжает от неё рывками по ~3 метра.
            //
            // ЧТО БЫЛО. Смещение головы считалось от МИРОВОЙ позиции камеры:
            //     headOffset = head.position - rigRoot.position;
            // Но `head` — это камера, лежащая ВНУТРИ рига. Её мировую позицию ставит
            // `KvXrHeadTracking` как «поза трекинга + позиция рига» (через `ToWorld`).
            // Значит `head.position` УЖЕ включает `rigRoot.position`, и вычитание давало
            // не смещение головы от центра рига, а что-то бессмысленное.
            //
            // ДАЛЬШЕ ПОЛУЧАЛСЯ КРУГ:
            //     rigRoot.position := groundPoint − (поза_трекинга + rigRoot.position − rigRoot.position)
            //     ...в следующем кадре ToWorld СНОВА прибавляет новую позицию рига к позе трекинга,
            //     камера уезжает, и телепорт пересчитывается от уехавшей камеры.
            // Каждый кадр — новый сдвиг. Отсюда «всё крутится».
            //
            // ТЕПЕРЬ. Берём смещение головы В СИСТЕМЕ ОТСЛЕЖИВАНИЯ — то есть ДО того, как к ней
            // прибавили риг. Это и есть «где оператор стоит относительно центра рига»:
            //     смещение = поза_трекинга (по горизонтали)
            // Тогда формула становится САМОСОГЛАСОВАННОЙ: риг ставится так, чтобы под точкой пола
            // оказалась поза трекинга, и следующий кадр не сдвигает ничего.
            Vector3 headOffset = Vector3.zero;

            Vector3 trackedPosition;
            Quaternion trackedRotation;
            if (KvXrInputSource.TryHeadPose(out trackedPosition, out trackedRotation))
            {
                // Поза трекинга УЖЕ переведена `ToWorld` в мировые координаты. Чтобы получить
                // смещение ВНУТРИ рига, переводим её обратно в локальные координаты рига.
                headOffset = rigRoot.InverseTransformPoint(trackedPosition);
                headOffset.y = 0f;
            }
            else if (head != null)
            {
                // Трекинга нет — берём локальную позицию камеры. Она уже внутри рига, поэтому
                // прибавления позиции рига здесь нет и круг не замыкается.
                headOffset = rigRoot.InverseTransformPoint(head.position);
                headOffset.y = 0f;
            }

            rigRoot.position = new Vector3(groundPoint.x - headOffset.x, rigRoot.position.y,
                groundPoint.z - headOffset.z);
            TeleportCount++;
            KvHapticsBridge.Pulse(KvXrQuickHand.Right, true);
        }

        // ------------------------------------------------------------------ плавный ход

        private void HandleSmooth(Quaternion rigYaw)
        {
            // СТИК ХОДА — тот же, что и у телепорта (MoveStick): по умолчанию левый, как в играх.
            Vector2 stick = MoveStick;
            float deadzone = KvXrSettings.GetWalkDeadzone();
            Vector2 deadzoned = ApplyDeadzone(stick, deadzone);

            // ЦЕЛЕВАЯ СКОРОСТЬ: направление по взгляду оператора, величина — доля отклонения стика.
            Vector3 forward = rigYaw * Vector3.forward;
            Vector3 right = rigYaw * Vector3.right;
            Vector3 move = forward * deadzoned.y + right * deadzoned.x;
            float speed = KvXrSettings.GetSmoothSpeed();
            Vector3 targetVelocity = move.sqrMagnitude > 1e-6f
                ? move.normalized * (speed * Mathf.Clamp01(deadzoned.magnitude))
                : Vector3.zero;

            // РАЗГОН/ТОРМОЖЕНИЕ (05.11.2026): мгновенная скорость — второй источник дискомфорта
            // после поворота. `MoveTowards` даёт постоянное ускорение и мягкий останов.
            float acceleration = KvXrSettings.GetMoveAcceleration();
            smoothVelocity = Vector3.MoveTowards(smoothVelocity, targetVelocity,
                acceleration * Mathf.Min(Time.deltaTime, MaxTurnFrameSeconds));
            if (smoothVelocity.sqrMagnitude < 1e-6f) return;

            Vector3 step = smoothVelocity * Mathf.Min(Time.deltaTime, MaxTurnFrameSeconds);

            // ГРАНИЦЫ И СТОЛКНОВЕНИЯ — только в VR (в MR зал скрыт, комната 1:1 — ходьба свободная).
            Vector3 current = rigRoot.position;
            Vector3 desired = current + step;
            if (KvXrWalkRules.EnabledInCurrentMode)
            {
                desired = KvXrWalkRules.ClampToHangar(desired);
                desired = KvXrWalkRules.ResolveObstacles(current, desired, 0.30f, 1.70f);
            }

            rigRoot.position = desired;
            SmoothMovingSeconds += Mathf.Min(Time.deltaTime, MaxTurnFrameSeconds);
        }

        private static bool IsSmoothMode()
        {
            return KvXrSettings.GetMoveMode() == KvXrMoveMode.Smooth;
        }

        /// <summary>
        /// ВЕРНУТЬСЯ НА РАБОЧУЮ ПОЗИЦИЮ (удержание левого стика): аварийный выход из «ушёл и
        /// потерялся». Ставит риг в начало отсчёта — там стоят стенды (см. `KvXrSceneContent`).
        /// </summary>
        public void ReturnToStart()
        {
            TeleportTo(Vector3.zero);
            Debug.Log("[KvXr] ХОД: возврат на рабочую позицию (центр ангара)");
        }

        /// <summary>Мёртвая зона стика: у шлемовых стиков заметный дребезг в нуле.</summary>
        public static Vector2 ApplyDeadzone(Vector2 stick, float deadzone)
        {
            float magnitude = stick.magnitude;
            if (magnitude <= deadzone) return Vector2.zero;
            float scaled = (magnitude - deadzone) / (1f - deadzone);
            return stick.normalized * Mathf.Clamp01(scaled);
        }

        // ------------------------------------------------------------------ метка телепорта

        private void SetMarker(bool valid, Vector3 point, Quaternion rigYaw, Vector3 normal = default(Vector3))
        {
            if (point == Vector3.zero && !valid)
            {
                if (teleportMarker != null) teleportMarker.SetActive(false);
                return;
            }

            if (teleportMarker == null) teleportMarker = BuildMarker();
            teleportMarker.SetActive(valid);
            teleportMarker.transform.position = point + Vector3.up * 0.01f;
            teleportMarker.transform.rotation = Quaternion.identity;

            var renderer = teleportMarker.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                renderer.sharedMaterial.color = valid ? validMarkerColor : invalidMarkerColor;
        }

        /// <summary>
        /// Метка — плоский круг над полом. Материал берётся у шейдера HDRP/Unlit, а если его
        /// нет (другой пайплайн) — у обычного непрозрачного шейдера: падение из-за материала
        /// в VR выглядит как «телепорт не работает», хотя дело в розовом квадрате.
        /// </summary>
        private GameObject BuildMarker()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "KvXrTeleportMarker";
            go.transform.SetParent(rigRoot, true);
            go.transform.localScale = new Vector3(0.35f, 0.005f, 0.35f);

            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Материал метки — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP):
            // жёсткий HDRP-шейдер под URP дал бы в шлеме розовый квадрат вместо метки.
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = KvRenderPipelineShaders.CreateUnlit("Материал_метки_телепорта", validMarkerColor);
                if (material != null) renderer.material = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return go;
        }

        private Transform FindHead()
        {
            Camera cam = GetComponentInChildren<Camera>();
            return cam != null ? cam.transform : null;
        }

        /// <summary>
        /// СПОСОБ ПОВОРОТА СЛОВАМИ — для журнала и отчёта. Без этой строки по журналу нельзя
        /// понять, каким режимом владелец проверял управление: «поворотов 0» одинаково выглядит
        /// и у плавного режима, и у шагов, которых просто не было.
        /// </summary>
        public string TurnModeLabel()
        {
            return useSmoothTurn
                ? string.Format("плавный {0:0}°/с", smoothTurnDegreesPerSecond)
                : string.Format("шагами {0:0}°", snapTurnDegrees);
        }

        /// <summary>Строка состояния для журнала и отчёта.</summary>
        public string Describe()
        {
            return string.Format(
                "XR-перемещение: способ {0} · поворот {1} · телепортов {2} · шагов поворота {3} · " +
                "плавный ход {4:0.0} с · плавный поворот {5:0.0} с · {6}",
                KvXrSettings.MoveModeLabel(KvXrSettings.GetMoveModeIndex()),
                TurnModeLabel(), TeleportCount, SnapTurnCount, SmoothMovingSeconds,
                SmoothTurnSeconds, KvXrInputSource.Describe());
        }

        /// <summary>
        /// ИДЁТ ЛИ СЕЙЧАС ПЕРЕМЕЩЕНИЕ ТОЧКИ (п.9, 02.11.2026).
        ///
        /// Спрашивается у потока через мост, а не хранится копией: состояние режима — один
        /// источник правды (TrajectoryFlowController.state.phase). Копия в локомоции
        /// разошлась бы с ним при первом же сбросе.
        /// </summary>
        private static bool IsPointMoveActive()
        {
            KvXrFlowBridge bridge = KvXrFlowBridge.Active;
            return bridge != null && bridge.Flow != null && bridge.Flow.IsPointMoveMode;
        }
    }


}
