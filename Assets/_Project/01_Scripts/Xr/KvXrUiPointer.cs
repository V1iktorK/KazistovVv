using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KazistovVvFeatures;

namespace KazistovVvXr
{
    /// <summary>
    /// ЛУЧ КОНТРОЛЛЕРА ПО ИНТЕРФЕЙСУ (решение владельца [D1], этап 2–3 плана XR).
    ///
    /// ЗАЧЕМ. В шлеме нет мыши, и весь проект, построенный вокруг системного курсора,
    /// перестаёт управляться. Здесь контроллер превращается в «мышь»: из руки идёт луч,
    /// он попадает в панель интерфейса, курок нажимает кнопку. Это и есть минимальный
    /// рабочий ввод, после которого шлем перестаёт быть кинозалом.
    ///
    /// КАК ЭТО РАБОТАЕТ БЕЗ XRI. Используется штатный `GraphicRaycaster` канваса: он умеет
    /// пускать луч в мировом пространстве (для Canvas в режиме World Space). Положение
    /// указателя передаётся в мировых координатах — этого достаточно, чтобы кнопки
    /// подсвечивались и нажимались. Ни одного нового пакета для этого не нужно.
    ///
    /// ПОЧЕМУ УЧАСТИЕ В ТЕЛЕПОРТЕ. Курок в VR — единственная удобная кнопка. Если он будет
    /// одновременно нажимать кнопку интерфейса И телепортировать оператора, получится ровно
    /// тот дефект «одно нажатие — два действия», который в §27 лечили на десктопе.
    /// Поэтому при наведении на интерфейс телепорт молчит (<see cref="AimingAtUi"/>).
    ///
    /// ЧЕСТНОЕ ОГРАНИЧЕНИЕ: без живого шлема проверено только на стенде — расчёт луча,
    /// наведение и нажатие. Что кнопки в шлеме попадают в цель «с первого раза»,
    /// проверяется на устройстве по чек-листу приёмки.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrUiPointer : MonoBehaviour
    {
        /// <summary>Длина луча, метры (в помещении достаточно 10).</summary>
        public float rayLength = 10f;

        /// <summary>Длина луча ИЗ ПАЛЬЦА в режиме кистей (§40): не больше 1,5 м.</summary>
        public float handRayLength = KvXrHandRay.MaxLengthMeters;

        /// <summary>Толщина луча, метры.</summary>
        public float rayWidth = 0.008f;

        /// <summary>Размер метки-курсора на конце луча, метры (число из правил §39 — через `KvXrCursor`).</summary>
        public float cursorSizeMeters = KvXrCursor.RayMarkerMeters;

        /// <summary>Камера шлема — по ней разворачивается метка-курсор (иначе её видно «с ребра»).</summary>
        public Camera headCamera;

        /// <summary>Цвет луча, когда он ни во что не попал.</summary>
        public Color idleColor = new Color(0.55f, 0.75f, 1f, 0.55f);

        /// <summary>Цвет луча при наведении на элемент интерфейса.</summary>
        public Color hoverColor = new Color(0.30f, 0.95f, 0.55f, 0.95f);

        /// <summary>Порог курка, при котором считается «нажато».</summary>
        public float triggerThreshold = 0.5f;

        /// <summary>
        /// УПРАВЛЕНИЕ ВЗГЛЯДОМ, КОГДА КОНТРОЛЛЕРОВ НЕТ. Найдено на живом шлеме 15.10.2026:
        /// приложению не отдаётся ни один контроллер, и прежний код просто выключал луч —
        /// владелец не мог навестись ни на что. Теперь луч идёт из глаз, а нажатие делается
        /// задержкой взгляда на кнопке.
        /// </summary>
        public bool useGazeWhenNoController = true;

        /// <summary>Сколько смотреть на кнопку, чтобы она нажалась (секунды).</summary>
        public float gazeDwellSeconds = KvGazeDwell.DefaultSeconds;

        /// <summary>Насколько вынести начало луча вперёд от глаз, метры (чтобы луч не «рос из головы»).</summary>
        public float gazeOriginForward = 0.10f;

        /// <summary>Насколько опустить начало луча ниже уровня глаз, метры.</summary>
        public float gazeOriginDown = 0.10f;

        /// <summary>
        /// ВЕРХНЯЯ ГРАНИЦА ОПУСКАНИЯ ЛУЧА ВЗГЛЯДА, метры. На дистанции панели 0,40 м 3 см дают
        /// ≈4° вниз; 10 см давали 14° — и луч проходил НИЖЕ иконки, на которую смотрел человек.
        /// </summary>
        public const float MaxGazeDownMeters = 0.01f;

        /// <summary>
        /// СКОЛЬКО СЕКУНД «ПРОЩАЕТСЯ» ПОТЕРЯ ИКОНКИ, чтобы счёт удержания не сбрасывался от
        /// дрожания руки и шлема. 0,25 с — меньше, чем порог подписи (0,5 с), поэтому подпись
        /// по-прежнему не может появиться «сама»; но микродрожание не отнимает накопленное.
        /// </summary>
        public float hoverGraceSeconds = 0.25f;

        private int lastHitIndex = -1;
        private float lostSince = -1f;

        /// <summary>
        /// НОМЕР ПЕРЕСБОРКИ ПАНЕЛИ, при котором считалось наведение (карточка Б51). Разошёлся с
        /// текущим — значит номера иконок сменили смысл, и «прощённый» номер больше не годится.
        /// </summary>
        private int lastPanelVersion = -1;

        /// <summary>
        /// НАВЕДЕНИЕ НА ПРОЧИЕ КАНВАСЫ ЧЕРЕЗ uGUI. По умолчанию ВЫКЛЮЧЕНО, и это не забывчивость.
        ///
        /// ПОЧЕМУ (найдено 16–18.10.2026, сессии §35–§36). `GraphicRaycaster` в мировом канвасе
        /// ждёт ЭКРАННЫЕ ПИКСЕЛИ: `GraphicRaycaster.cs:184` → `eventCamera.ScreenPointToRay(...)`,
        /// а затем `RectTransformUtility.RectangleContainsScreenPoint`. Мировая точка панели
        /// (0.4; 1.2; 0.5) читалась как пиксель у левого нижнего угла — луч шёл в угол экрана,
        /// а не в иконку. Единственные попадания, которые тогда случались, приходились на
        /// полноэкранный «Blocker» десктопного канваса (то есть на невидимые кнопки).
        /// Поэтому панель групп наводится ГЕОМЕТРИЕЙ (метры, плоскость панели), а этот
        /// путь остаётся только как выключаемый запасной вариант.
        /// </summary>
        public bool useEventSystemFallback = false;

        /// <summary>Панель групп: по ней считается наведение и нажатие. Пусто — найдётся сама.</summary>
        public KvXrGroupPanel groupPanel;

        private LineRenderer line;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private PointerEventData pointerData;
        private GameObject currentHover;
        private bool triggerWasDown;
        private bool aimingAtUi;
        private bool gazeMode;
        private float dwellTime;
        private GameObject dwellTarget;

        /// <summary>Луч сейчас наведён на элемент интерфейса (нужно локомоции: не телепортировать).</summary>
        public bool AimingAtUi { get { return aimingAtUi; } }

        /// <summary>Имя элемента под лучом (для журнала и ручной проверки в шлеме).</summary>
        public string HoverName { get { return currentHover != null ? currentHover.name : "—"; } }

        /// <summary>
        /// НОМЕР ИКОНКИ ПОД ЛУЧОМ (−1 — ни одной). Нужен жестам (§37): щипок обязан нажимать
        /// ИМЕННО ту иконку, на которую наведён луч, а не «первую попавшуюся».
        /// </summary>
        public int HoveredIndex { get { return hoveredIndex; } }

        private KvXrQuickHand loggedRayHand = (KvXrQuickHand)(-1);

        /// <summary>
        /// СТРОКА О РУКЕ ЛУЧА ([S2.4]): «луч идёт из ПРАВОЙ руки, курок нажимает; в настройках руку
        /// можно сменить». Печатается один раз на старте и заново при смене руки — по ней владелец
        /// сверяет то, что видит, двигая рукой.
        /// </summary>
        private void LogRayHandOnce()
        {
            KvXrQuickHand hand = KvXrInputSource.RayHand;
            if (hand == loggedRayHand) return;
            loggedRayHand = hand;

            KvXrQuickHand panelHand = KvXrSettings.GetQuickPanelHand();
            Debug.Log("[KvXr] Указатель: луч из " +
                      (hand == KvXrQuickHand.Right ? "ПРАВОЙ" : "ЛЕВОЙ") + " руки (курок " +
                      (hand == KvXrQuickHand.Right ? "правой" : "левой") + ") · панель у " +
                      (panelHand == KvXrQuickHand.Right ? "ПРАВОЙ" : "ЛЕВОЙ") + " руки · " +
                      "рука луча — настройка (ray.hand в файле подстройки)");
        }

        /// <summary>Сколько нажатий по интерфейсу сделано — метрика проверки ввода.</summary>
        public int ClickCount { get; private set; }

        void Awake()
        {
            EnsureLine();
            EnsurePointerData();
        }

        void OnDisable()
        {
            if (line != null) line.enabled = false;
            HideCursor();
            if (currentHover != null) ClearHover();
        }

        /// <summary>Спрятать метку-курсор (луча нет — метки быть не должно).</summary>
        private void HideCursor()
        {
            if (cursorMarker != null && cursorMarker.gameObject.activeSelf) cursorMarker.gameObject.SetActive(false);
        }

        void Update()
        {
            if (KvXrSettings.GetShowPanel() == false)
            {
                if (line != null) line.enabled = false;
                HideCursor();
                aimingAtUi = false;
                ClearHover();
                return;
            }

            // ПОИСК ПАНЕЛИ — `FindAnyObjectByType`: `FindFirstObjectByType` в Unity 6 объявлен
            // устаревшим (CS0618), а гейт проекта требует НОЛЬ предупреждений компилятора.
            if (groupPanel == null) groupPanel = UnityEngine.Object.FindAnyObjectByType<KvXrGroupPanel>();
            if (groupPanel == null && !useEventSystemFallback)
            {
                if (line != null) line.enabled = false;
                HideCursor();
                aimingAtUi = false;
                return;
            }

            // РЕЖИМ КИСТЕЙ (§40): контроллер отложен — луч идёт ИЗ КОНЧИКА УКАЗАТЕЛЬНОГО ПАЛЬЦА
            // и не длиннее 1,5 м. Раньше в этом режиме продолжал работать луч от лежащего на столе
            // контроллера: он уходил на 10 м в стены («лучи идут длинные как у контроллеров»).
            if (KvXrMode.For(KvXrInputSource.RayHand) == KvXrControlMode.Hands)
            {
                KvHandFrame handFrame;
                Vector3 handOrigin;
                Vector3 handDirection;
                if (KvXrHandSource.TryGet(KvXrInputSource.RayHand, out handFrame) &&
                    KvXrHandRay.TryGetRay(handFrame, out handOrigin, out handDirection))
                {
                    if (gazeMode)
                    {
                        gazeMode = false;
                        Debug.Log("[KvXr] Указатель: контроллер отложен — луч идёт из указательного пальца.");
                    }
                    LogRayHandOnce();
                    if (useEventSystemFallback) Aiming(handOrigin, handDirection);
                    else AimAlongPanel(handOrigin, handDirection, false, handRayLength);
                    return;
                }
            }

            // ОСНОВНОЙ ПУТЬ — КОНТРОЛЛЕР: луч из ПОЗЫ УКАЗАНИЯ (не кисти), нажатие курком.
            //
            // §37: РУКА — НАСТРОЙКА. До §37 здесь стоял прямой вызов `TryRightAimPose`, то есть рука
            // луча была вшита в код и сменить её было нечем. Теперь сторона берётся из
            // `KvXrInputSource.RayHand` (настройка → файл подстройки), по умолчанию ПРАВАЯ.
            if (KvXrInputSource.TryRayAimPose(out Vector3 handPosition, out Quaternion handRotation))
            {
                if (gazeMode)
                {
                    gazeMode = false;
                    Debug.Log("[KvXr] Указатель: контроллер появился — вернулся луч из руки.");
                }
                LogRayHandOnce();
                Vector3 direction = handRotation * Vector3.forward;
                if (useEventSystemFallback) Aiming(handPosition, direction);
                else AimAlongPanel(handPosition, direction, false);
                return;
            }

            // ЗАПАСНОЙ ПУТЬ — ВЗГЛЯД (найдено на живом шлеме 15.10.2026: контроллеров нет вовсе,
            // и раньше здесь луч просто исчезал — «навестись нечем»). Луч идёт из глаз чуть вниз
            // и вперёд, а нажатие делается задержкой взгляда на иконке (KvGazeDwell).
            if (useGazeWhenNoController &&
                KvXrInputSource.TryHeadPose(out Vector3 headPosition, out Quaternion headRotation))
            {
                if (!gazeMode)
                {
                    gazeMode = true;
                    Debug.Log("[KvXr] Указатель: контроллеров НЕТ — луч идёт ПО ВЗГЛЯДУ, " +
                              "нажатие по задержке " + gazeDwellSeconds.ToString("0.0") + " с.");
                }

                // ОПУСКАНИЕ ЛУЧА ОГРАНИЧЕНО 3 СМ (≈4° на дистанции панели 0,40 м). В сцене
                // записано 0,10 м — это смещало линию взгляда на 14° вниз: человек смотрел В
                // иконку, а луч уходил ниже неё. Смотреть на панель и не попадать в неё — ровно
                // то, из-за чего подпись и веер в шлеме не проверены. Значение берётся из поля,
                // но сверху ограничено: правится числом в коде, а не «на глаз» в сцене.
                float down = Mathf.Clamp(gazeOriginDown, 0f, MaxGazeDownMeters);
                Vector3 origin = headPosition + headRotation * new Vector3(0f, -down, gazeOriginForward);
                Vector3 direction = headRotation * Vector3.forward;
                if (useEventSystemFallback) Aiming(origin, direction);
                else AimAlongPanel(origin, direction, true);
                return;
            }

            gazeMode = false;
            if (line != null) line.enabled = false;
            HideCursor();
            aimingAtUi = false;
            ClearHover();
        }

        // ==================================================== НАВЕДЕНИЕ ГЕОМЕТРИЕЙ (§36)

        private int hoveredIndex = -1;
        private string hoveredTitle;

        /// <summary>
        /// ПАНЕЛЬ, В КОТОРУЮ ПОПАЛ ЛУЧ НА ЭТОМ ШАГЕ (панель у руки, меню или окно). Пусто — попадения
        /// нет, и тогда работает «своя» панель `groupPanel` как раньше.
        /// </summary>
        private KvXrGroupPanel aimedPanel;

        /// <summary>ПЕРЕНОС ОКНА — ОДИН МЕХАНИЗМ НА ОБЕ РУКИ (KvXrWindowDrag).</summary>
        private readonly KvXrWindowDrag windowDrag = new KvXrWindowDrag();

        /// <summary>
        /// ОДИН ШАГ ЛУЧА ПО ПАНЕЛИ ГРУПП. Считается пересечением луча с плоскостью панели
        /// в метрах (см. `KvXrGroupPanelLayout.TryHitIcon`), а не экранными пикселями uGUI.
        ///
        /// ЧТО ДЕЛАЕТ: подсвечивает иконку (подпись по удержанию 0,5 с ведёт сама панель),
        /// нажимает по курку (контроллер) или по задержке взгляда (1,3 с), и пишет строки
        /// журнала — по ним владелец и проверяет, что произошло.
        /// </summary>
        private void AimAlongPanel(Vector3 origin, Vector3 direction, bool byGaze, float maxLength = -1f)
        {
            EnsureLine();

            float limit = maxLength > 0f ? maxLength : rayLength;
            float limitBeforeSurface = limit;

            int index = -1;
            float fromCenter = 0f;
            float along = limit;
            Vector3 hitPoint = origin + direction.normalized * limit;

            // ПАНЕЛЕЙ ТЕПЕРЬ НЕСКОЛЬКО (панель у руки, стартовое меню, окна в мире) — луч выбирает ту,
            // в которую реально попал. Раньше здесь стояла одна `groupPanel`, и любое окно в мире было
            // бы для луча невидимым.
            aimedPanel = ResolvePanel(origin, direction, limit,
                out index, out fromCenter, out along, out hitPoint);

            bool realHit = index >= 0;

            KvXrGroupPanel target = aimedPanel != null ? aimedPanel : groupPanel;

            // СМЕНА УРОВНЯ ПАНЕЛИ — ЭТО НОВЫЕ НОМЕРА ИКОНОК (карточка Б51). Номер пересборки читается
            // каждый шаг: если панель пересобрана, прошлый номер больше ничего не значит, и журнал
            // не должен писать «луч навёл «» — 0.0 см», а телепорт — держать запрет 0,5 с.
            int panelVersion = target != null ? target.RebuildVersion : 0;
            bool versionChanged = panelVersion != lastPanelVersion;
            lastPanelVersion = panelVersion;

            // ДРОЖАНИЕ РУКИ И ШЛЕМА НЕ ДОЛЖНО СБРАСЫВАТЬ УДЕРЖАНИЕ. Найдено 18.10.2026 в журнале:
            // подпись появлялась (0,5 с накоплено), а нажатие — нет: иконка терялась и находилась
            // снова, счёт начинался заново. Иконка на 0,45 м — это конус ~4°, и микродрожание
            // выбивает из него. Поэтому потеря иконки «прощается» на `hoverGraceSeconds` — но только
            // пока панель та же и номер под ним существует (правило — `KvXrAimGuard`, проверено тестом).
            if (realHit)
            {
                lastHitIndex = index;
                lostSince = -1f;
            }
            else if (lastHitIndex >= 0)
            {
                if (lostSince < 0f) lostSince = Time.unscaledTime;

                float lostFor = hoverGraceSeconds > 0f ? Time.unscaledTime - lostSince : float.MaxValue;
                bool keep = target != null && KvXrAimGuard.KeepsForgivenTarget(
                    false, versionChanged, lastHitIndex, target.RayTargetCount,
                    target.TitleAt(lastHitIndex), lostFor, hoverGraceSeconds);

                if (keep) index = lastHitIndex;
                else
                {
                    lastHitIndex = -1;
                    lostSince = -1f;
                }
            }
            else
            {
                // Прощать нечего: номер обнуляется сразу, а не по таймауту.
                lastHitIndex = -1;
                lostSince = -1f;
            }

            // ОВЕРЛЕЙ НЕ ПРОЗРАЧЕН ДЛЯ ЛУЧА (указание владельца 24.10.2026: «вне кнопок лазер
            // пролетает сквозь оверлей»). Притяжение к кнопкам остаётся, но если луч не попал ни в
            // одну кнопку, а попал в саму плиту — он обрывается на ней, а не летит дальше. Расстояние
            // до плиты уже посчитано в `ResolvePanel` и лежит в `along` при промахе.
            if (!realHit && along > 0f && along < limit) limit = Mathf.Max(0.05f, along);

            // ЛУЧ УПЁРСЯ В ИНТЕРФЕЙС — и кнопка, и просто плита меню/окна. Метка (шарик) на
            // интерфейсе не показывается (указание владельца 24.10.2026: «шарик только при выходе из
            // меню»). Раньше скрывался только случай «попал в кнопку», а на плите шарик оставался.
            bool stoppedOnInterface = realHit || limit < limitBeforeSurface - 0.0005f;

            // ПЕРЕНОС ОКНА (указание владельца 24.10.2026): «перемещение окна должно осуществляться
            // либо за нажатие кнопки указательным пальцем при прицеле на ВЕРХНЮЮ часть окна, либо
            // нажатием средним пальцем при прицеле на ЛЮБУЮ точку окна».
            windowDrag.Tick(target, origin, direction, KvXrInputSource.RayGrip >= KvXrWindowDrag.ButtonThreshold, KvXrInputSource.RayTrigger >= KvXrWindowDrag.ButtonThreshold);

            aimingAtUi = index >= 0;

            // ШАРИК — ТОЛЬКО ВНЕ ИНТЕРФЕЙСА (указание владельца 24.10.2026: «шарики только при выходе
            // из меню»). Над меню и окнами шарика нет: там и так видно подсветку кнопки, а шарик
            // закрывал бы её. Луч в интерфейсе заканчивается точно в точке касания — это же число
            // используется и для метки, поэтому они не могут разойтись.
            float cursorDistance = KvXrCursor.RayLengthMeters(realHit, along, limit);
            Vector3 cursorPoint = realHit ? hitPoint : origin + direction.normalized * cursorDistance;

            // ПРИ ПЕРЕНОСЕ ОКНА ЛАЗЕР СЛЕДИТ ЗА ПЕРЕМЕЩЕНИЕМ (указание владельца 24.10.2026: «надо,
            // чтобы лазер следил за перемещением»). Кончик луча едет вместе с той точкой окна, за
            // которую взяли: окно уехало на Δ — кончик уехал на то же Δ. В центр кнопки он при этом
            // не прыгает (предыдущее указание), поэтому «замороженная» точка — это точка ЗАХВАТА,
            // а не точка экрана.
            if (windowDrag.Active) cursorPoint = windowDrag.CurrentPoint;

            if (line != null)
            {
                line.enabled = true;
                line.SetPosition(0, origin);
                line.SetPosition(1, cursorPoint);
                line.startColor = KvXrLaserColors.Ray(KvXrInputSource.RayHand, aimingAtUi);
                line.endColor = line.startColor;
            }

            UpdateCursor(cursorPoint, KvXrCursor.State(!stoppedOnInterface, false));

            // НАВЕДЕНИЕ: меняем цель только при смене иконки — иначе журнал утонул бы. Во время
            // переноса окна подсветка не перескакивает (см. заморозку метки выше).
            if (!windowDrag.Active && index != hoveredIndex)
            {
                hoveredIndex = index;
                if (target != null) target.SetHoverIndex(index);

                if (index >= 0)
                {
                    hoveredTitle = target != null ? target.TitleAt(index) : null;
                    Debug.Log(KvXrAimGuard.AimLogLine(true, hoveredTitle, fromCenter, along, SourceName(byGaze)));
                }
                else
                {
                    // ЧЕСТНАЯ СТРОКА О ПОТЕРЕ (карточка Б51): раньше здесь печаталось «навёл «»»,
                    // потому что номер прошлой иконки подставлялся в пересобранную панель.
                    if (!string.IsNullOrEmpty(hoveredTitle))
                        Debug.Log(KvXrAimGuard.AimLogLine(false, null, 0f, along, SourceName(byGaze)));
                    hoveredTitle = null;
                }
            }

            // НАЖИМАЕМ ТОЛЬКО ПО РЕАЛЬНОМУ ПОПАДАНИЮ: «прощённая» иконка нужна для удержания,
            // но нажимать по ней, когда луч уже ушёл, нельзя — это было бы ложное нажатие.
            if (realHit)
            {
                if (byGaze) HandleGazePress(index);
                else HandleTriggerPress(index);
                return;
            }

            // КОЛЕСО СИМВОЛОВ (§41): курок вставляет ВЫБРАННЫЙ стиком сектор, даже когда луч между
            // секторами. Стик листает (это делает панель), курок подтверждает — как записано в [A3].
            if (target != null && target.StickCaptured)
            {
                bool wheelDown = KvXrInputSource.RayTrigger >= triggerThreshold;
                if (wheelDown && !triggerWasDown) target.AppendWheelSymbol();
                triggerWasDown = wheelDown;
                return;
            }

            // Курок отпущен без цели — сбрасываем фронт, чтобы следующее нажатие было новым.
            triggerWasDown = KvXrInputSource.RayTrigger >= triggerThreshold;
        }

        /// <summary>
        /// В КАКУЮ ПАНЕЛЬ ПОПАЛ ЛУЧ. Панелей теперь несколько: панель у руки, стартовое меню и окна,
        /// которые открываются из меню и остаются висеть в воздухе (указание владельца: «окна не
        /// должны крепиться ко взгляду, если персонаж будет отходить — окна останутся висеть»).
        ///
        /// ПРАВИЛО ВЫБОРА — ПО ЛУЧУ, А НЕ ПО ПОРЯДКУ. Сначала ищется КНОПКА: побеждает ближайшая к
        /// руке. Если кнопок под лучом нет ни у одной панели, ищется ПЛИТА — та, в которую луч
        /// упирается, чтобы он не пролетал сквозь интерфейс (то же указание владельца). Ничего не
        /// найдено — возвращается пусто, и указатель ведёт себя как раньше.
        /// </summary>
        private KvXrGroupPanel ResolvePanel(Vector3 origin, Vector3 direction, float limit,
                                            out int index, out float fromCenter, out float along,
                                            out Vector3 hitPoint)
        {
            index = -1;
            fromCenter = 0f;
            along = limit;
            hitPoint = origin + direction.normalized * limit;

            KvXrGroupPanel bestPanel = null;
            float bestAlong = float.MaxValue;

            // ШАГ 1 — КНОПКИ.
            for (int i = 0; i < KvXrPanels.Count; i++)
            {
                KvXrGroupPanel panel = KvXrPanels.At(i);
                if (panel == null || !panel.isActiveAndEnabled) continue;

                int pIndex;
                float pFromCenter;
                float pAlong;
                Vector3 pPoint;

                if (!panel.TryAim(origin, direction, out pIndex, out pFromCenter, out pAlong, out pPoint)) continue;
                if (pIndex < 0) continue;
                if (pAlong <= 0f || pAlong > limit) continue;
                if (pAlong >= bestAlong) continue;

                bestAlong = pAlong;
                bestPanel = panel;
                index = pIndex;
                fromCenter = pFromCenter;
                along = pAlong;
                hitPoint = pPoint;
            }

            if (bestPanel != null) return bestPanel;

            // ШАГ 2 — ПЛИТА (луч должен упираться в интерфейс, а не пролетать сквозь него).
            for (int i = 0; i < KvXrPanels.Count; i++)
            {
                KvXrGroupPanel panel = KvXrPanels.At(i);
                if (panel == null || !panel.isActiveAndEnabled) continue;

                float pAlong;
                if (!panel.TryHitPanelSurface(origin, direction, out pAlong)) continue;
                if (pAlong <= 0f || pAlong > limit) continue;
                if (pAlong >= bestAlong) continue;

                bestAlong = pAlong;
                bestPanel = panel;
            }

            if (bestPanel != null)
            {
                index = -1;
                along = bestAlong;
                hitPoint = origin + direction.normalized * bestAlong;
            }

            return bestPanel;
        }

        /// <summary>
        /// ЧЕМ НАВОДИЛИСЬ — со стороной руки (§37). Владелец проверяет сторону движением руки, а
        /// строка обязана называть ровно то, что происходит: «источник КОНТРОЛЛЕР (правая рука)».
        /// </summary>
        private static string SourceName(bool byGaze)
        {
            if (byGaze) return "ВЗГЛЯД";
            return KvXrInputSource.RayHand == KvXrQuickHand.Right
                ? "КОНТРОЛЛЕР (правая рука)"
                : "КОНТРОЛЛЕР (левая рука)";
        }

        /// <summary>НАЖАТИЕ КУРКОМ: по переднему фронту, только по иконке под лучом.</summary>
        private void HandleTriggerPress(int index)
        {
            bool down = KvXrInputSource.RayTrigger >= triggerThreshold;
            bool edge = down && !triggerWasDown;
            triggerWasDown = down;
            if (!edge || index < 0) return;

            ClickCount++;
            KvXrGroupPanel pressed = aimedPanel != null ? aimedPanel : groupPanel;
            if (pressed != null) pressed.PressIndex(index);
            ResetHoverAfterPress();
        }

        /// <summary>
        /// ПОСЛЕ НАЖАТИЯ ЦЕЛЬ СБРАСЫВАЕТСЯ. Нажатие на иконку группы пересобирает панель:
        /// номера иконок меняют смысл, и без сброса луч «наводился» бы на иконку с прежним
        /// номером, не печатая строку наведения — то есть журнал врал бы о том, что под лучом.
        /// </summary>
        private void ResetHoverAfterPress()
        {
            hoveredIndex = -1;
            hoveredTitle = null;
            dwellIndex = -1;
            dwellTime = 0f;
            if (groupPanel != null) groupPanel.SetHoverIndex(-1);
            if (aimedPanel != null && aimedPanel != groupPanel) aimedPanel.SetHoverIndex(-1);
        }

        private int dwellIndex = -1;

        /// <summary>
        /// НАЖАТИЕ ВЗГЛЯДОМ: копим время, пока цель не меняется. Подпись (0,5 с) покажет сама
        /// панель, нажатие — на пороге `gazeDwellSeconds` (1,3 с). Отвёл взгляд — счёт заново.
        ///
        /// ВРЕМЯ УДЕРЖАНИЯ — общее поле с путём через uGUI (см. `dwellTime` в полях класса):
        /// двух счётчиков быть не должно, иначе один из путей нажимал бы дважды.
        /// </summary>
        private void HandleGazePress(int index)
        {
            if (index < 0)
            {
                dwellIndex = -1;
                dwellTime = 0f;
                return;
            }

            if (index != dwellIndex)
            {
                dwellIndex = index;
                dwellTime = 0f;
            }

            dwellTime = KvGazeDwell.Advance(dwellTime, Time.deltaTime, gazeDwellSeconds);

            if (line != null && line.enabled)
            {
                float progress = KvGazeDwell.Progress01(dwellTime, gazeDwellSeconds);
                line.startColor = KvXrLaserColors.RayProgress(KvXrInputSource.RayHand, progress);
                line.endColor = line.startColor;
            }

            if (!KvGazeDwell.IsReady(dwellTime, gazeDwellSeconds)) return;

            dwellTime = 0f;
            ClickCount++;
            KvXrGroupPanel pressed = aimedPanel != null ? aimedPanel : groupPanel;
            if (pressed != null) pressed.PressIndex(index);
            ResetHoverAfterPress();
        }

        /// <summary>
        /// Один шаг луча: найти цель, подсветить, нажать при курке.
        /// Вынесено отдельным методом, чтобы это можно было проверить без шлема
        /// (подставив произвольные позицию и направление).
        /// </summary>
        public void Aiming(Vector3 handPosition, Vector3 direction)
        {
            EnsureLine();
            EnsurePointerData();

            float length = rayLength;
            GameObject target = null;
            Vector3 endPoint = handPosition + direction.normalized * rayLength;

            if (EventSystem.current != null && DirectionalUi(handPosition, direction))
            {
                target = pointerData.pointerCurrentRaycast.gameObject;
                endPoint = pointerData.pointerCurrentRaycast.worldPosition;
                length = Vector3.Distance(handPosition, endPoint);
            }

            aimingAtUi = target != null;

            // ЛУЧ УПИРАЕТСЯ В МИР (дефект Д16, найдено в шлеме 25.10.2026: «лазеры не упирались в
            // текстуры, всё тряслось»). До этой правки проверка попадания была ТОЛЬКО по интерфейсу
            // (`EventSystem.RaycastAll`), а `Physics.Raycast` не вызывался ни разу: луч пролетал
            // сквозь стол, робота и пол на всю свою длину. Отсюда сразу три симптома владельца:
            // метка на конце гуляла (тряска), луч не останавливался на объектах и яркость «на цели»
            // не включалась — то есть лазер не вёл себя как в ПК-версии.
            //
            // Теперь берётся БЛИЖАЙШЕЕ из двух попаданий: интерфейс или геометрия сцены. Слой
            // `IgnoreRaycast` (контроллеры, второй луч) в проверку не входит — он для этого и заведён.
            Vector3 worldHitPoint;
            if (HitWorld(handPosition, direction, length, out worldHitPoint))
            {
                endPoint = worldHitPoint;
                length = Vector3.Distance(handPosition, endPoint);
                aimingAtWorld = true;
            }
            else
            {
                aimingAtWorld = false;
            }

            if (line != null)
            {
                line.enabled = true;
                line.SetPosition(0, handPosition);
                line.SetPosition(1, endPoint);
                bool onTarget = aimingAtUi || aimingAtWorld;
                line.startColor = KvXrLaserColors.Ray(KvXrInputSource.RayHand, onTarget);
                line.endColor = line.startColor;
            }

            SetHover(target);
            HandleClick(target, gazeMode);

            LogWorldHit(endPoint);
        }

        /// <summary>
        /// СТРОКА-ДОКАЗАТЕЛЬСТВО ПОПАДАНИЯ ЛУЧА В МИР (§42, Д16). Печатается только при СМЕНЕ цели:
        /// «во что попал» и на каком расстоянии. Без неё проверить «луч упирается» можно только глазами,
        /// а правило проекта — пункт без строки журнала считается непроверенным.
        /// </summary>
        private void LogWorldHit(Vector3 point)
        {
            string what = null;
            if (aimingAtUi) what = hoveringTitle();
            else if (aimingAtWorld) what = lastWorldName;

            if (what == lastLoggedHit) return;      // без спама: пишем только смену цели
            lastLoggedHit = what;

            if (string.IsNullOrEmpty(what))
            {
                Debug.Log("[KvXr] ЛУЧ: ушёл с объектов — попадания нет, луч идёт на полную длину");
                return;
            }

            float distance = Vector3.Distance(lastRayOrigin, point);
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[KvXr] ЛУЧ: попадание в «{0}» на {1:0.00} м ({2})", what, distance,
                aimingAtUi ? "интерфейс" : "геометрия сцены"));
        }

        private string lastLoggedHit;
        private string lastWorldName;
        private Vector3 lastRayOrigin;

        /// <summary>Название иконки под лучом (для строки журнала).</summary>
        private string hoveringTitle()
        {
            return HoverName;
        }

        /// <summary>Луч попал в геометрию сцены (стол, робот, пол, стена).</summary>
        public bool AimingAtWorld { get { return aimingAtWorld; } }

        private bool aimingAtWorld;
        private string worldName;

        /// <summary>БЛИЖАЙШАЯ ГЕОМЕТРИЯ ПОД ЛУЧОМ: точка попадания в метрах от руки.</summary>
        private bool HitWorld(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 point)
        {
            point = origin;

            int mask = ~(1 << KvXrControllerVisuals.IgnoreRaycastLayer);
            RaycastHit hit;
            if (!Physics.Raycast(origin, direction, out hit, Mathf.Max(0.05f, maxDistance), mask,
                    QueryTriggerInteraction.Ignore))
                return false;

            point = hit.point;
            return true;
        }

        /// <summary>
        /// Пройти лучом по канвасу и заполнить данные указателя.
        ///
        /// Ключевая тонкость: в мировом пространстве `GraphicRaycaster` ожидает координаты
        /// указателя В МИРОВЫХ единицах, а не в пикселях экрана. Поэтому в `position`
        /// кладётся точка попадания, а не позиция мыши.
        /// </summary>
        private bool DirectionalUi(Vector3 origin, Vector3 direction)
        {
            pointerData.position = origin;
            pointerData.delta = Vector2.zero;
            pointerData.scrollDelta = Vector2.zero;
            pointerData.pressPosition = origin;
            pointerData.pointerCurrentRaycast = default(RaycastResult);
            pointerData.pointerEnter = null;

            hits.Clear();
            EventSystem.current.RaycastAll(pointerData, hits);

            for (int i = 0; i < hits.Count; i++)
            {
                GameObject go = hits[i].gameObject;
                if (go == null) continue;
                if (go.GetComponent<Selectable>() == null) continue;   // интересуют только органы управления
                pointerData.pointerCurrentRaycast = hits[i];
                pointerData.pointerEnter = go;
                pointerData.position = hits[i].worldPosition;
                return true;
            }
            return false;
        }

        private void SetHover(GameObject target)
        {
            if (target == currentHover) return;
            ClearHover();
            currentHover = target;
            if (currentHover == null) return;

            pointerData.pointerEnter = currentHover;
            ExecuteEvents.ExecuteHierarchy(currentHover, pointerData, ExecuteEvents.pointerEnterHandler);
        }

        private void ClearHover()
        {
            if (currentHover != null && pointerData != null)
                ExecuteEvents.ExecuteHierarchy(currentHover, pointerData, ExecuteEvents.pointerExitHandler);
            currentHover = null;
            if (pointerData != null) pointerData.pointerEnter = null;
        }

        /// <summary>
        /// Нажатие курком. Событие уходит ИЕРАРХИИ: кнопка часто лежит внутри строки,
        /// и обработчик живёт выше по дереву — как `ExecuteEvents.ExecuteHierarchy` и делают.
        /// </summary>
        private void HandleClick(GameObject target, bool byGaze)
        {
            if (byGaze)
            {
                HandleGazeClick(target);
                return;
            }

            bool down = KvXrInputSource.RayTrigger >= triggerThreshold;

            if (down && !triggerWasDown && target != null)
            {
                pointerData.pointerPress = target;
                pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;
                pointerData.eligibleForClick = true;
                ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerDownHandler);
            }
            else if (!down && triggerWasDown)
            {
                GameObject press = pointerData.pointerPress;
                pointerData.pointerPress = null;
                pointerData.eligibleForClick = false;
                if (press != null)
                {
                    ExecuteEvents.ExecuteHierarchy(press, pointerData, ExecuteEvents.pointerUpHandler);
                    if (target == press)
                    {
                        ExecuteEvents.ExecuteHierarchy(press, pointerData, ExecuteEvents.pointerClickHandler);
                        ClickCount++;
                    }
                }
            }

            triggerWasDown = down;
        }

        /// <summary>
        /// НАЖАТИЕ ВЗГЛЯДОМ: пока взгляд держится на одной и той же цели — копится время,
        /// на пороге кнопка нажимается (нажатие и отпускание подряд — как щелчок мышью).
        /// Отвели взгляд или сменили цель — счётчик обнуляется: «случайных» нажатий не будет.
        /// </summary>
        private void HandleGazeClick(GameObject target)
        {
            if (target == null || target != dwellTarget)
            {
                dwellTarget = target;
                dwellTime = 0f;
                if (target != null)
                    Debug.Log("[KvXr] Указатель: взгляд на «" + target.name + "» — держите " +
                              gazeDwellSeconds.ToString("0.0") + " с для нажатия");
                return;
            }

            dwellTime = KvGazeDwell.Advance(dwellTime, Time.deltaTime, gazeDwellSeconds);

            // Подсветка «сколько ещё смотреть»: луч плавно переходит в цвет наведения.
            if (line != null && line.enabled)
            {
                float progress = KvGazeDwell.Progress01(dwellTime, gazeDwellSeconds);
                line.startColor = KvXrLaserColors.RayProgress(KvXrInputSource.RayHand, progress);
                line.endColor = line.startColor;
            }

            if (!KvGazeDwell.IsReady(dwellTime, gazeDwellSeconds)) return;

            dwellTime = 0f;

            pointerData.pointerPress = target;
            pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;
            pointerData.eligibleForClick = true;
            ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerClickHandler);

            pointerData.pointerPress = null;
            pointerData.eligibleForClick = false;
            ClickCount++;

            Debug.Log("[KvXr] НАЖАТИЕ ВЗГЛЯДОМ: «" + target.name + "» (всего нажатий " + ClickCount + ")");
        }

        private void EnsureLine()
        {
            if (line != null) return;
            line = GetComponent<LineRenderer>();
            if (line == null) line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = rayWidth;
            line.endWidth = rayWidth * 0.4f;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            // Материал луча — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP для шлема,
            // HDRP для компьютера). Жёсткий `Shader.Find("HDRP/Unlit")` под URP дал бы
            // розовый луч — то есть «сломанный указатель» в шлеме.
            //
            // МАТЕРИАЛ БЕЛЫЙ, ЦВЕТ ДАЮТ ВЕРШИНЫ (`startColor`/`endColor`) — ЭТО ИСПРАВЛЕНИЕ
            // 24.10.2026. Раньше материал создавался сразу с цветом луча (голубой 0,55; 0,75; 1,00) и
            // ЕЩЁ подкрашивался вершинами: цвета перемножались, и «зелёный» правый лазер вне оверлея
            // выглядел синим. Слова владельца: «лазер из правого контроллера не зелёного цвета во
            // время работы не с оверлеем».
            Material material = KvRenderPipelineShaders.CreateUnlit("Материал_луча_XR", Color.white);
            if (material != null) line.material = material;
        }

        private void EnsurePointerData()
        {
            if (pointerData == null) pointerData = new PointerEventData(EventSystem.current);
        }

        // ==================================================== КУРСОР НА КОНЦЕ ЛУЧА (§39)

        private Transform cursorMarker;
        private Renderer cursorRenderer;
        private Material cursorIdleMaterial;
        private Material cursorHoverMaterial;

        /// <summary>Для какой руки сделаны материалы метки (меняется — материалы пересоздаются).</summary>
        private KvXrQuickHand cursorMaterialHand = (KvXrQuickHand)(-1);

        /// <summary>
        /// ПОСТАВИТЬ И ОКРАСИТЬ МЕТКУ. Квадратик всегда развёрнут к оператору (иначе его видно
        /// «с ребра» и он исчезает — ровно так было с кистями до §37).
        /// </summary>
        private void UpdateCursor(Vector3 point, KvCursorState state)
        {
            EnsureCursor();
            if (cursorMarker == null) return;

            bool visible = state != KvCursorState.Hidden;
            if (cursorMarker.gameObject.activeSelf != visible) cursorMarker.gameObject.SetActive(visible);
            if (!visible) return;

            cursorMarker.position = point;

            Camera camera = headCamera != null ? headCamera : Camera.main;
            if (camera != null)
            {
                Vector3 toEye = camera.transform.position - point;
                if (toEye.sqrMagnitude > 0.0001f)
                    cursorMarker.rotation = Quaternion.LookRotation(toEye.normalized, camera.transform.up);
            }

            if (cursorRenderer != null)
            {
                // ЦВЕТ МЕТКИ — ПО РУКЕ (§41): сменили руку луча в настройках — материалы
                // пересоздаются, иначе метка осталась бы прежнего цвета и снова путала бы владельца.
                if (cursorMaterialHand != KvXrInputSource.RayHand)
                    BuildCursorMaterials(KvXrInputSource.RayHand);

                Material material = state == KvCursorState.OverIcon ? cursorHoverMaterial : cursorIdleMaterial;
                if (material != null && cursorRenderer.sharedMaterial != material)
                    cursorRenderer.sharedMaterial = material;
            }
        }

        private void EnsureCursor()
        {
            if (cursorMarker != null) return;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "KvXrRayCursor";
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            go.layer = KvXrControllerVisuals.IgnoreRaycastLayer;

            float size = Mathf.Max(0.004f, cursorSizeMeters);
            go.transform.localScale = new Vector3(size, size, size);
            go.SetActive(false);

            cursorRenderer = go.GetComponent<Renderer>();
            if (cursorRenderer != null)
            {
                BuildCursorMaterials(KvXrInputSource.RayHand);
                if (cursorIdleMaterial != null) cursorRenderer.sharedMaterial = cursorIdleMaterial;
                cursorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cursorRenderer.receiveShadows = false;
            }

            cursorMarker = go.transform;
        }

        /// <summary>
        /// ЦВЕТА МЕТКИ — ПО РУКЕ (§41, указание владельца). Правая рука — зелёный, левая — красный,
        /// как в компьютерной версии; наведение делает тот же цвет ярче.
        /// </summary>
        private void BuildCursorMaterials(KvXrQuickHand hand)
        {
            cursorMaterialHand = hand;

            if (cursorIdleMaterial != null) Destroy(cursorIdleMaterial);
            if (cursorHoverMaterial != null) Destroy(cursorHoverMaterial);

            cursorIdleMaterial = KvRenderPipelineShaders.CreateUnlit("Материал_курсора_XR",
                KvXrLaserColors.Ray(hand, false));
            cursorHoverMaterial = KvRenderPipelineShaders.CreateUnlit("Материал_курсора_наведения_XR",
                KvXrLaserColors.Ray(hand, true));
        }

        /// <summary>
        /// СБРОСИТЬ СОСТОЯНИЕ УКАЗАТЕЛЯ (§37). После пробуждения шлема: фронт курка, «прощённая»
        /// иконка, счёт удержания и режим взгляда — всё это могло остаться от прошлого сеанса и
        /// выглядеть как «указка не работает». Здесь всё возвращается в исходное.
        /// </summary>
        public void ResetState()
        {
            triggerWasDown = false;
            hoveredIndex = -1;
            hoveredTitle = null;
            lastHitIndex = -1;
            lostSince = -1f;
            dwellIndex = -1;
            dwellTime = 0f;
            gazeMode = false;
            dwellTarget = null;
            aimingAtUi = false;
            ClearHover();
            if (line != null) line.enabled = false;
        }

        /// <summary>Строка состояния: куда смотрит луч и сколько нажатий сделано.</summary>
        public string Describe()
        {
            return string.Format("XR-указатель: цель «{0}» · нажатий {1} · на интерфейсе: {2}",
                HoverName, ClickCount, aimingAtUi ? "да" : "нет");
        }
    }
}

