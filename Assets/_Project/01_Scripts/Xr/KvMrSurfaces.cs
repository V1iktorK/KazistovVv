using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.PXR;
using KazistovVvRealWorld;

namespace KazistovVvXr
{
    /// <summary>
    /// РЕАЛЬНЫЕ ПОВЕРХНОСТИ КОМНАТЫ → КОЛЛАЙДЕРЫ → ЛУЧ (создано 02.11.2026, сессия S45).
    ///
    /// ЗАЧЕМ. В VR «стол» — это объект сцены с коллайдером, и луч попадает в него через обычный
    /// `Physics.Raycast`. В MR стол НАСТОЯЩИЙ: коллайдера у него нет, и `Physics.Raycast` его не
    /// видит вовсе. Именно поэтому «навести луч на свой стол и поставить робота» до сих пор не
    /// работало: попадать было некуда.
    ///
    /// КАК ЭТО РЕШАЕТСЯ. В шлеме есть подсистема пространственного меша (`XRMeshSubsystem`) —
    /// это СТАНДАРТНЫЙ Unity XR API, а не вендорский: PICO создаёт её фичей `PICOSpatialMesh`,
    /// Meta — фичей `arfoundation-meta-mesh`. Здесь меши комнаты превращаются в невидимые
    /// `MeshCollider`-объекты, и лучи начинают попадать в реальную столешницу как в обычную
    /// виртуальную поверхность. Ни один полигон невидим: рендерера у коллайдеров нет.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ. Нет рендера комнаты («серые стены поверх passthrough» не нужны) и нет
    /// подмены сквозного вида: картинку комнаты даёт passthrough, этот компонент даёт только
    /// ФИЗИКУ реального мира.
    ///
    /// ЧЕСТНО ОБ ОТКАЗЕ. Если меша нет (устройство не умеет, разрешение не выдано, фича выключена),
    /// компонент пишет причину в журнал и говорит `Ready = false`. Тогда постановка робота честно
    /// переходит на запасной путь, а не «висит в воздухе».
    /// </summary>
    [DisallowMultipleComponent]
    public class KvMrSurfaces : MonoBehaviour
    {
        /// <summary>Имя корневого объекта, под которым живут коллайдеры реального мира.</summary>
        public const string ContainerName = "KvMrRealWorld";

        /// <summary>
        /// ТОЛЩИНА, ДО КОТОРОЙ КУСОК МЕША СЧИТАЕТСЯ ГОРИЗОНТАЛЬНОЙ ПОВЕРХНОСТЬЮ, м (0.10).
        ///
        /// Почему 0.10: столешница и пол в меше — плоские «блины»; всё, что выше 10 см по вертикали,
        /// это шкаф, стена или ножки стола, и горизонтальной поверхностью для робота оно не является.
        /// </summary>
        private const float MeshFlatHeightMeters = 0.10f;

        /// <summary>
        /// Как часто пересчитывать «стол найден / пол найден» и писать строку в журнал, секунды.
        ///
        /// 20.11.2026 (деградация MR 86 → 3 FPS): строка менялась КАЖДЫЙ раз, потому что в ней
        /// число коллайдеров, а оно росло каждую секунду — в журнале 140 копий за прогон, и
        /// каждая это `Debug.Log` на главном потоке. Требование владельца «не чаще раза в
        /// секунду» — это ПОРОЛ, а не цель: раз в 5 с оператор всё так же видит числа, а кадровый
        /// бюджет от журнала возвращается. Приёмка `REAL_TABLE` строку ищет по префиксу
        /// «[KvMr] РЕАЛЬНЫЙ СТОЛ:» — он не изменился.
        /// </summary>
        private const float RealTableReportSeconds = 5f;

        /// <summary>Сколько мешей комнаты максимум принимать (защита памяти).</summary>
        public int maxMeshes = 400;

        /// <summary>
        /// НАКАКОЙ ИНТЕРВАЛ ПЕРЕСОБИРАТЬ УЖЕ ИЗВЕСТНЫЙ МЕШ, секунды (1).
        ///
        /// ЗАЧЕМ. `MeshChangeState.Updated` шлёт PICO для каждого куска комнаты очень часто, а
        /// генерация меша — это ещё и ВАРКА ФИЗИКИ (`MeshCollider`) на главном потоке. Раз в
        /// секунду покрытие всё равно перерисовывается (`KvMrSurfacePainter`), поэтому более
        /// частые обновления не дают ничего видимого, но стоят кадров. НЕ влияет на качество
        /// картинки: коллайдеры невидимы, а геометрия покрытия обновляется тем же шагом.
        /// </summary>
        public float meshRefreshSeconds = 1f;

        /// <summary>Писать в журнал каждый построенный меш (для наладки; по умолчанию — только итог).</summary>
        public bool logEveryMesh = false;

        /// <summary>Сколько ждать подсистему меша, прежде чем честно сказать «её нет» (секунды).</summary>
        public float waitForSubsystemSeconds = 10f;

        /// <summary>
        /// ЗАПАСНОЙ ИСТОЧНИК ПОВЕРХНОСТИ — СЕМАНТИЧЕСКАЯ СЦЕНА PICO, секунды (S45).
        ///
        /// ЗАЧЕМ. Меш комнаты на PICO 4 Ultra отдаёт полигоны не всегда: он требует надетого шлема
        /// и разметки помещения. У PICO есть ВТОРОЙ источник — размеченные объекты комнаты
        /// (семантическая сцена), где стол приходит как параллелепипед с габаритами. Если меш
        /// молчит, этот источник позволяет поставить робота на РЕАЛЬНЫЙ стол, а не на пол.
        /// </summary>
        public float sceneAnchorDelaySeconds = 8f;

        /// <summary>
        /// ЖЁСТКИЙ ПРЕДЕЛ ОЖИДАНИЯ ОТВЕТА РАЗМЕТКИ, секунды (15).
        ///
        /// ЧТО БЫЛО. Тайм-аут стоял ТОЛЬКО на отдельных нативных вызовах
        /// (`WithTimeout(StartSenseDataProvider, 8000)`, `WithTimeout(QuerySceneAnchorAsync, 12000)`),
        /// а у ВСЕГО запроса целиком предела не было, и итоговой строки о результате не печатал
        /// никто. Пока вызовы не вернулись — в журнале была тишина: ни «запрос стола вернул …»,
        /// ни «столов N», ни «комната не размечена».
        /// ЧЕМ ПЛОХО (журнал шлема, свежая сборка): после строки «подсистема меша найдена
        /// (запускаю)» за 2+ минуты работы не появилось ни одной строки о результате. Оператор в
        /// шлеме не знал, ждать ему, настраивать комнату или это поломка.
        /// ЧТО СТАЛО. У запроса есть ОДИН предел ожидания, и отсчитывает его ГЛАВНЫЙ ПОТОК
        /// (`UpdateSceneAnchorQuery`), а не задача PICO: зависший `while (true) { PollFuture }`
        /// внутри пакета PICO уже не может утащить отчёт за собой.
        /// </summary>
        public float sceneAnchorTimeoutSeconds = 15f;

        /// <summary>Готово ли принимать лучи: есть хотя бы один коллайдер реального мира.</summary>
        public bool Ready { get; private set; }

        /// <summary>Число коллайдеров реального мира (для журнала и экрана состояния).</summary>
        public int ColliderCount { get { return realWorldColliders.Count; } }

        /// <summary>
        /// СКОЛЬКО ПОВЕРХНОСТЕЙ РЕАЛЬНОГО МИРА ПРИЗНАНО СТОЛОМ классификатором.
        ///
        /// ЗАЧЕМ ОТДЕЛЬНЫЙ СЧЁТЧИК. ЧТО БЫЛО: «готово принимать лучи» означало «есть хоть один
        /// коллайдер», и это одинаково звучало и когда в комнате найден стол, и когда найден только
        /// ПОЛ: в MR пол — тоже поверхность с коллайдером, и луч постановки попадает в него как в
        /// стол. Оператор не мог понять, на столе робот или на полу. ТЕПЕРЬ по этому числу видно,
        /// проверенный стол найден (≥ 1) или нет (0).
        /// </summary>
        public int TableCount { get; private set; }

        /// <summary>
        /// СКОЛЬКО КОЛЛАЙДЕРОВ ПРИЗНАНО ПОЛОМ. Пол намеренно НЕ получает отдельного коллайдера
        /// (иначе луч постановки цеплял бы пол как стол), поэтому счётчик считает поверхности,
        /// которые классификатор отверг как пол, — по нему видно, что дело именно в поле, а не в
        /// отсутствии данных.
        /// </summary>
        public int FloorColliders { get; private set; }

        /// <summary>Уровень пола известен: из разметки PICO (`PxrSemanticLabel.Floor`) или выведен по двум уровням.</summary>
        public bool FloorFound { get; private set; }

        /// <summary>Высота пола в координатах трекинга, метры (имеет смысл только при `FloorFound`).</summary>
        public float FloorHeight { get; private set; }

        /// <summary>
        /// ЧЕСТНАЯ СТРОКА О РЕАЛЬНОМ СТОЛЕ: что найдено, чем подтверждено и с какими числами.
        /// Ровно она печатается в журнал (не чаще раза в секунду) — по ней тестировщик в шлеме
        /// проверяет результат, а не догадывается по картинке.
        /// </summary>
        public string DescribeRealTable { get; private set; } =
            "стол не найден: реальные поверхности ещё не получены · уверенность 0.00";

        /// <summary>Строка состояния одной строкой.</summary>
        public string Status
        {
            get
            {
                if (subsystem == null) return "реальные поверхности: подсистема меша не найдена (" + noSubsystemReason + ")";
                if (!Ready) return "реальные поверхности: меш запущен, ждём первые полигоны (коллайдеров 0)";
                return "реальные поверхности: коллайдеров " + realWorldColliders.Count +
                       " · мешей " + builtMeshes + " · столов " + TableCount +
                       " · пол " + (FloorFound ? "найден" : "не найден") +
                       " · размер комнаты " + RoomSizeText();
            }
        }

        private readonly List<XRMeshSubsystem> subsystemBuffer = new List<XRMeshSubsystem>();
        private readonly List<MeshInfo> meshInfos = new List<MeshInfo>();
        private readonly HashSet<MeshId> knownMeshIds = new HashSet<MeshId>();
        private readonly HashSet<MeshId> pendingMeshIds = new HashSet<MeshId>();
        private readonly HashSet<Collider> realWorldColliders = new HashSet<Collider>();
        private readonly Dictionary<MeshId, Mesh> meshesById = new Dictionary<MeshId, Mesh>();
        private readonly Dictionary<MeshId, MeshCollider> collidersById = new Dictionary<MeshId, MeshCollider>();
        private readonly List<MeshId> removedBuffer = new List<MeshId>();

        /// <summary>
        /// КОГДА КАЖДЫЙ МЕШ ГОТОВИЛИ ПОСЛЕДНИЙ РАЗ — КЛЮЧ ОТ НАКОПЛЕНИЯ ДУБЛИКАТОВ (см.
        /// `ShouldBuildMesh`). Без интервала `Updated` пере-собирал объекты каждый кадр.
        /// </summary>
        private readonly Dictionary<MeshId, float> lastBuiltAt = new Dictionary<MeshId, float>();

        /// <summary>
        /// ЧИСЛОВОЕ ОПИСАНИЕ КАЖДОГО КОЛЛАЙДЕРА РЕАЛЬНОГО МИРА — вход классификатора.
        ///
        /// ПОЧЕМУ ОТДЕЛЬНО ОТ КОЛЛАЙДЕРОВ. Классификатор не знает ни про меши, ни про PICO: он
        /// получает габариты, нормаль, высоту и «что говорит платформа». Здесь хранится именно это,
        /// поэтому решение «стол или пол» принимается ОДНИМ кодом и в шлеме, и на планшете.
        /// </summary>
        private readonly Dictionary<Collider, RealSurfaceCandidate> candidatesByCollider =
            new Dictionary<Collider, RealSurfaceCandidate>();

        /// <summary>Буфер для вывода уровня пола по всем поверхностям (переиспользуется).</summary>
        private readonly List<RealSurfaceCandidate> reportCandidates = new List<RealSurfaceCandidate>();

        private float nextReportTime;
        private string lastReportLine = string.Empty;
        private bool floorFromSemantics;

        /// <summary>Камера оператора: её высота нужна правилу «стол ниже глаз» (кэш, чтобы не искать по сцене).</summary>
        private Camera eyeCamera;

        private XRMeshSubsystem subsystem;
        private Transform container;
        private bool permissionGranted;
        private bool permissionRequested;
        private float startedAt;
        private bool gaveUp;
        private string noSubsystemReason = "ещё не искали";
        private int builtMeshes;
        private int failedMeshes;
        private bool sceneAnchorRequested;
        private bool sceneAnchorRunning;
        private int tableColliders;

        /// <summary>
        /// ИТОГ ЗАПРОСА РАЗМЕТКИ УЖЕ НАПЕЧАТАН (одна строка на сессию).
        ///
        /// ЗАЧЕМ ФЛАГ. Итог может прийти из двух мест: от самой задачи PICO (когда она всё-таки
        /// вернулась) и от жёсткого предела ожидания на главном потоке. Первый пришедший печатает
        /// строку, второй — только диагностику. Иначе в журнале оказались бы ДВА разных «итога», и
        /// тестировщик не знал бы, какому верить.
        /// </summary>
        private bool sceneQueryReported;

        /// <summary>Когда начался запрос разметки (`Time.unscaledTime`) — от него считается предел.</summary>
        private float sceneQueryStartedAt;

        /// <summary>Когда печатать строку ожидания в следующий раз и какой она была в прошлый раз.</summary>
        private float nextWaitingStatusTime;
        private string lastWaitingStatusLine = string.Empty;

        /// <summary>Текущий экземпляр. Один на приложение: иначе два компонента построят по копии
        /// коллайдеров, и луч будет видеть комнату дважды (и память уйдёт вдвое).</summary>
        public static KvMrSurfaces Instance { get; private set; }

        /// <summary>
        /// КОЛЛАЙДЕРЫ РЕАЛЬНОГО МИРА ДЛЯ ОТРИСОВКИ — ТОЛЬКО ЧТЕНИЕ (пункт 1 владельца 12.11.2026).
        ///
        /// ЗАЧЕМ ЭТОТ СВОЙСТВО-СПИСОК, А НЕ ПУБЛИЧНОЕ ПОЛЕ. ЧТО БЫЛО: `realWorldColliders` —
        /// приватный набор, и нарисовать поверхности было нечем: `KvMrSurfacePainter` не мог бы
        /// обойти комнату, не заведя ВТОРУЮ копию списка (а копия всегда расходится с оригиналом).
        /// ЧТО СТАЛО: наружу отдан тот же набор, но только на чтение — снаружи его нельзя ни
        /// очистить, ни дополнить, то есть источник правды остался один.
        /// </summary>
        public IEnumerable<Collider> RealWorldColliders { get { return realWorldColliders; } }

        /// <summary>
        /// ГАБАРИТЫ И НОРМАЛЬ ПОВЕРХНОСТИ — то, по чему рисуется контур/сетка/заливка.
        ///
        /// ВОЗВРАЩАЕТСЯ ЛИ КАНДИДАТ. Только для поверхностей, которые классификатор уже разобрал:
        /// у коллайдера меша это ограничивающий параллелепипед куска, у стола из разметки —
        /// верхняя грань. Для чужих коллайдеров (не наших) ответ `false`: рисовать «поверхность
        /// реального мира» по случайному коллайдеру сцены нельзя.
        /// </summary>
        public bool TryGetSurfaceShape(Collider collider, out RealSurfaceCandidate candidate)
        {
            candidate = default(RealSurfaceCandidate);
            if (collider == null) return false;
            return candidatesByCollider.TryGetValue(collider, out candidate);
        }

        /// <summary>
        /// СОЗДАТЬ КОМПОНЕНТ В РАНТАЙМЕ (сцена не правится — её SHA256 закреплён тестом).
        /// </summary>
        public static KvMrSurfaces Ensure(Transform parent)
        {
            if (Instance != null) return Instance;

            var go = new GameObject("KvMrSurfaces");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<KvMrSurfaces>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Второй экземпляр не нужен: убираем, чтобы коллайдеры не удвоились.
                Destroy(this);
                return;
            }
            Instance = this;
            // НАЧАЛО ОТСЧЁТА ЖДЁТ ОТ `StartTracking`, НО НОЛЬ ЗДЕСЬ НЕЛЬЗЯ. ЧТО БЫЛО: `startedAt`
            // оставался нулём до `StartTracking`, а `Time.unscaledTime - 0` — это «приложение работает
            // N секунд», то есть строка ожидания соврала бы про время и запрос разметки счёл бы, что
            // ждал уже давно. ЧТО СТАЛО: отсчёт стоит с момента создания компонента, а `StartTracking`
            // его только уточняет.
            startedAt = Time.unscaledTime;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Коллайдеры реального мира — наши объекты; убираем за собой, чтобы повторный запуск
            // не наткнулся на «двойников» и не удвоил память.
            if (container != null) Destroy(container.gameObject);
        }

        /// <summary>
        /// НАЧАТЬ ОТСЛЕЖИВАНИЕ РЕАЛЬНЫХ ПОВЕРХНОСТЕЙ.
        ///
        /// Порядок важен и повторяется в проекте PICO (`PXR_SpatialMeshManager`): сначала
        /// РАЗРЕШЕНИЕ на пространственные данные, только потом `subsystem.Start()`. Без разрешения
        /// подсистема молча не стартует — и это выглядит как «меша нет».
        /// </summary>
        public void StartTracking()
        {
            if (subsystem != null) return;
            startedAt = Time.unscaledTime;
            tracking = true;

            if (container == null)
            {
                var go = new GameObject(ContainerName);
                go.transform.SetParent(transform, false);
                container = go.transform;
            }

            NextSubsystemScan = 0f;
            Debug.Log("[KvMr] реальные поверхности: начинаю отслеживание");
        }

        /// <summary>
        /// ОСТАНОВИТЬ ОТСЛЕЖИВАНИЕ КОМНАТЫ — ВОЗВРАТ В VR БЕЗ ПЕРЕЗАПУСКА (20.11.2026).
        ///
        /// ЗАЧЕМ. Жалоба владельца: «режим VR не переносит на сцену VR, а продолжает работать в
        /// MR». Переключатель гасил только настройку, а конвейер MR жил дальше: подсистема меша
        /// продолжала присылать обновления, физика комнаты варилась на главном потоке, а строка
        /// «РЕАЛЬНЫЙ СТОЛ» печаталась в журнал, пока её не убили троттлингом.
        ///
        /// ЧТО ОСТАЁТСЯ. Коллайдеры комнаты невидимы и кадров не стоят (пересборка мешей и
        /// покрытие выключены отдельно), поэтому очищать их не нужно — а при следующем входе в MR
        /// `StartTracking` поднимает ту же самую комнату без пересоздания объектов.
        /// </summary>
        public void StopTracking()
        {
            if (!tracking)
            {
                Debug.Log("[KvMr] реальные поверхности: отслеживание и так выключено");
                return;
            }

            tracking = false;
            subsystem = null;      // иначе `StartTracking` выйдет рано по «подсистема уже найдена»
            NextSubsystemScan = 0f;
            Debug.Log("[KvMr] реальные поверхности: отслеживание ВЫКЛЮЧЕНО (режим VR)");
        }

        /// <summary>
        /// ИДЁТ ЛИ ОТСЛЕЖИВАНИЕ КОМНАТЫ. По умолчанию `true` — прежнее поведение (компонент могли
        /// создать и без `StartTracking`), выключается только возвратом в VR.
        /// </summary>
        public bool Tracking { get { return tracking; } }

        private bool tracking = true;

        private float NextSubsystemScan;

        void Update()
        {
            // ВОЗВРАТ В VR (20.11.2026): пока отслеживание выключено, комната не опрашивается —
            // ни подсистема меша, ни разметка, ни строки «РЕАЛЬНЫЙ СТОЛ» (см. `StopTracking`).
            if (!tracking) return;

            // ЧТО БЫЛО. Первой строкой стояло `if (gaveUp && (Ready || sceneAnchorRunning)) return;` —
            // один молчаливый выход из ВСЕГО кадра. ЧЕМ ПЛОХО: этот выход и был одной из причин
            // тишины в журнале — как только подсистема меша не появлялась за `waitForSubsystemSeconds`
            // (gaveUp), а `Ready` приходил из разметки, ни запрос разметки, ни строка «РЕАЛЬНЫЙ
            // СТОЛ» больше не выполнялись НИКОГДА, без единого слова оператору.
            // ЧТО СТАЛО. Выхода нет: «сдаться» по подсистеме умеет `PumpSubsystem` (он идемпотентен),
            // а отчётность живёт отдельно и не может быть выключена чужой веткой.

            // Подсистему ищем не каждый кадр, а раз в секунду: она появляется только после
            // создания OpenXR-сессии, и опрос каждый кадр ничего не ускорит, но стоит времени.
            if (subsystem == null)
            {
                if (Time.unscaledTime < NextSubsystemScan) return;
                NextSubsystemScan = Time.unscaledTime + 1f;
            }

            PumpSubsystem();

            // РАЗМЕТКА КОМНАТЫ — ГАРАНТИРОВАННО ОДИН РАЗ И ВСЕГДА С ИТОГОМ (`UpdateSceneAnchorQuery`).
            UpdateSceneAnchorQuery();

            // РАЗБОР «СТОЛ ИЛИ ПРОСТО ПОВЕРХНОСТЬ» — не чаще раза в секунду (см. `UpdateRealTableReport`).
            UpdateRealTableReport();

            // ОЖИДАНИЕ СДЕЛАНО НАБЛЮДАЕМЫМ: «ждём» отличается от «сломалось» числом секунд в журнале.
            UpdateWaitingStatus();
        }

        /// <summary>
        /// ЗАПРОС РАЗМЕТКИ КОМНАТЫ: ГАРАНТИРОВАННО ОДИН РАЗ, С ЖЁСТКИМ ПРЕДЕЛОМ ИТОГА (S46).
        ///
        /// ЧТО БЫЛО. Запрос вызывался из `Update` УСЛОВНО (`!Ready && !sceneAnchorRequested && …`) и
        /// дальше целиком жил внутри `async void RequestSceneAnchors`. Результат печатался только
        /// если ВСЕ нативные ожидания вернулись сами.
        ///
        /// ПОЧЕМУ ЗАПРОС НЕ ОТЧИТЫВАЛСЯ (найдено по коду, подтверждено журналом шлема):
        ///   1) сам запрос живёт ЗА `await` вызовов пакета PICO, а те устроены как
        ///      `while (true) { UPxr_PollFuture(future, out state); if (state == Ready) … }`
        ///      (`Packages/com.unity.xr.openxr.picoxr/…/PXR_MixedReality.cs`, строки 80-104 и
        ///      491-624) плюс `while (isPermissionMR == -1) { await Task.Delay(200); }` (строки 65-68).
        ///      Пока `Ready` не наступит, из этого цикла НИКАКОЙ нашей строки не печатается:
        ///      ни успеха, ни отказа, ни «комната не размечена» — ровно та тишина, что в журнале;
        ///   2) наш собственный тайм-аут стоял на КАЖДОМ вызове по отдельности и не покрывал
        ///      запрос целиком, а `catch (Exception)` печатал лишь общее предупреждение;
        ///   3) условие `!Ready` выключало запрос навсегда, стоило появиться ОДНОМУ коллайдеру.
        ///
        /// ЧТО СТАЛО. «Спрашивать ли» решает чистая функция `ShouldRequestSceneAnchors` (её
        /// проверяет тест), признак «уже спрашивали» ставится ДО запуска, а САМ ПРЕДЕЛ ОЖИДАНИЯ
        /// отсчитывает главный поток по `Time.unscaledTime`. Если ответа нет — итоговая строка
        /// печатается всё равно (`ReportSceneQuery`), и оператор видит числа: сколько ждали, сколько
        /// столов, есть ли пол, по какой причине нет ответа.
        /// </summary>
        private void UpdateSceneAnchorQuery()
        {
            if (sceneQueryReported) return;

            float elapsed = Time.unscaledTime - startedAt;
            // «Вопрос о подсистеме меша закрыт»: она либо найдена, либо мы уже честно сдались по ней.
            // Запрос разметки не зависит от подсистемы нативно, но порядок в журнале должен быть
            // понятным: сначала «чем кончился меш», потом «что сказала разметка».
            bool subsystemSettled = subsystem != null || gaveUp;

            if (!sceneAnchorRequested)
            {
                if (!ShouldRequestSceneAnchors(sceneAnchorRequested, sceneAnchorRunning, subsystemSettled,
                        elapsed, sceneAnchorDelaySeconds)) return;

                // ПРИЗНАК «УЖЕ СПРАШИВАЛИ» СТАВИТСЯ ДО ЗАПУСКА: повторного запроса в этой сессии не
                // будет, даже если задача PICO не вернётся никогда.
                sceneAnchorRequested = true;
                sceneQueryStartedAt = Time.unscaledTime;
                RequestSceneAnchors();
                return;
            }

            float waited = Time.unscaledTime - sceneQueryStartedAt;
            if (waited < sceneAnchorTimeoutSeconds) return;

            ReportSceneQuery(KvSceneQueryOutcome.Timeout, tableColliders, FloorFound, waited,
                "провайдер сцены PICO не сообщил о готовности");
        }

        /// <summary>
        /// СТРОКА ОЖИДАНИЯ, ПОКА РЕАЛЬНЫХ ПОВЕРХНОСТЕЙ НЕТ (S46).
        ///
        /// ЧТО БЫЛО. Пока поверхностей не было, в журнал не уходило НИЧЕГО: `UpdateRealTableReport`
        /// на нуле поверхностей молча выходил. Оператор в шлеме видел пустую комнату без объяснений
        /// и не мог отличить «ждём» от «сломалось».
        /// ЧТО СТАЛО. Раз в секунду (и реже по мере ожидания, см. `WaitingStatusIntervalSeconds`)
        /// печатается строка с ЧИСЛАМИ: сколько мешей, сколько семантики, сколько ждём, и что делать.
        /// Печатается только при изменении текста — журнал не забивается копиями.
        /// </summary>
        private void UpdateWaitingStatus()
        {
            if (Time.unscaledTime < nextWaitingStatusTime) return;

            // Поверхности появились — эта строка больше не нужна: дальше говорит «РЕАЛЬНЫЙ СТОЛ».
            if (builtMeshes > 0 || tableColliders > 0) return;

            float waited = Time.unscaledTime - startedAt;
            nextWaitingStatusTime = Time.unscaledTime + WaitingStatusIntervalSeconds(waited);

            string line = DescribeWaitingSurfaces(builtMeshes, tableColliders, waited);
            if (line == lastWaitingStatusLine) return;
            lastWaitingStatusLine = line;
            Debug.Log(line);
        }

        /// <summary>Поиск подсистемы, разрешение, старт и чтение мешей — по шагам, идемпотентно.</summary>
        private void PumpSubsystem()
        {
            if (gaveUp) return;          // уже сказали «меша нет» — не спамим одним и тем же предупреждением

            if (subsystem == null)
            {
                if (Time.unscaledTime - startedAt > waitForSubsystemSeconds)
                {
                    gaveUp = true;
                    noSubsystemReason = "меш не появился за " + waitForSubsystemSeconds.ToString("0") +
                                        " с — устройство не умеет меш, фича выключена или не выдано разрешение";
                    Debug.LogWarning("[KvMr] реальные поверхности: " + noSubsystemReason);
                    return;
                }

                subsystemBuffer.Clear();
                SubsystemManager.GetSubsystems(subsystemBuffer);
                if (subsystemBuffer.Count == 0) return;

                subsystem = subsystemBuffer[0];
                noSubsystemReason = "найдена, но не запущена";
                Debug.Log("[KvMr] реальные поверхности: подсистема меша найдена (" +
                          (subsystem.running ? "уже работает" : "запускаю") + ")");
            }

            if (!permissionGranted)
            {
                RequestSpatialPermission();
                if (!permissionGranted) return;
            }

            if (!subsystem.running)
            {
                try { subsystem.Start(); }
                catch (Exception e)
                {
                    noSubsystemReason = "старт меша бросил " + e.GetType().Name + ": " + e.Message;
                    Debug.LogWarning("[KvMr] реальные поверхности: " + noSubsystemReason);
                    return;
                }
                if (!subsystem.running) return;
                Debug.Log("[KvMr] реальные поверхности: подсистема меша запущена");
            }

            meshInfos.Clear();
            if (!subsystem.TryGetMeshInfos(meshInfos)) return;

            float now = Time.unscaledTime;
            for (int i = 0; i < meshInfos.Count; i++)
            {
                MeshInfo info = meshInfos[i];
                switch (info.ChangeState)
                {
                    case MeshChangeState.Added:
                    case MeshChangeState.Updated:
                        // КОРЕНЬ ДЕГРАДАЦИИ MR (журнал шлема 20.11.2026: 86 → 3 FPS за 20 с).
                        // ЧТО БЫЛО: для `Updated` уже известного меша вызывался `BuildMesh`, а он
                        // СОЗДАВАЛ НОВЫЙ `GameObject` + `MeshCollider` + `Mesh` и перезаписывал
                        // записи в `collidersById`/`meshesById`. Старый коллайдер оставался в
                        // `realWorldColliders` НАВСЕГДА (убирал его только `RemoveMesh`, который
                        // знал лишь о новом объекте). Результат в журнале: «поверхностей 9383 ·
                        // коллайдеров 9383» при пределе `maxMeshes = 400` — то есть каждый кусок
                        // комнаты был продублирован ~20 раз, и дубликаты росли каждую секунду.
                        // Каждый дубликат = ещё один объект физики И ЕЩЁ ОДНА «плитка покрытия»
                        // у `KvMrSurfacePainter` (2 LineRenderer + заливка на объект), поэтому
                        // FPS падал линейно с ростом числа: 420 → 33, 1204 → 18, 2799 → 3.
                        // ЧТО СТАЛО. Повторная генерация идёт в ТОТ ЖЕ объект (см.
                        // `EnsureMeshObject`), а сама она разрешена не чаще `meshRefreshSeconds`
                        // на меш — переготовка физики каждые 100 мс больше не нужна: меш комнаты
                        // обновляется волнами, а не каждый кадр.
                        if (!ShouldBuildMesh(knownMeshIds.Contains(info.MeshId),
                                             pendingMeshIds.Contains(info.MeshId), now,
                                             LastBuiltAt(info.MeshId), meshRefreshSeconds)) continue;
                        BuildMesh(info.MeshId);
                        break;

                    case MeshChangeState.Removed:
                        removedBuffer.Add(info.MeshId);
                        break;
                }
            }

            for (int i = 0; i < removedBuffer.Count; i++) RemoveMesh(removedBuffer[i]);
            removedBuffer.Clear();
        }

        /// <summary>Запросить разрешение на пространственные данные (только Android).</summary>
        private void RequestSpatialPermission()
        {
            if (permissionRequested) return;
            permissionRequested = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            string permission = KvMrRuntime.Backend == KvMrBackend.Meta
                ? "com.oculus.permission.USE_SCENE"
                : "com.picovr.permission.SPATIAL_DATA";

            try
            {
                if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
                {
                    permissionGranted = true;
                    Debug.Log("[KvMr] разрешение на пространственные данные уже выдано: " + permission);
                    return;
                }

                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ =>
                {
                    permissionGranted = true;
                    Debug.Log("[KvMr] разрешение на пространственные данные выдано: " + permission);
                };
                callbacks.PermissionDenied += _ =>
                    Debug.LogWarning("[KvMr] разрешение на пространственные данные ОТКЛОНЕНО: " + permission +
                                     " — реальные поверхности недоступны, постановка на стол повиснет");
                callbacks.PermissionDeniedAndDontAskAgain += _ =>
                    Debug.LogWarning("[KvMr] разрешение на пространственные данные ОТКЛОНЕНО НАВСЕГДА: " + permission);

                UnityEngine.Android.Permission.RequestUserPermission(permission, callbacks);
                Debug.Log("[KvMr] запрошено разрешение на пространственные данные: " + permission);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KvMr] не удалось запросить разрешение на пространственные данные: " +
                                 e.GetType().Name + ": " + e.Message);
            }
#else
            // В редакторе и на ПК пространственных данных нет — это не ошибка.
            permissionGranted = true;
#endif
        }

        /// <summary>
        /// СТРОИТЬ ЛИ ЭТОТ МЕШ В ЭТОТ МОМЕНТ — ЧИСТОЕ ПРАВИЛО (проверяется тестом без шлема).
        ///
        /// Три входа и одна цель: НИКОГДА не допускать двух объектов одного `MeshId`.
        ///   • меш уже генерируется (`pending`) — повтор не нужен;
        ///   • меш новый — строим сразу (иначе лучу не во что попадать);
        ///   • меш известен — перестраиваем не чаще `refreshSeconds`, чтобы не варить физику
        ///     каждый кадр; до истечения интервала — молчим, а не создаём дубликат.
        /// </summary>
        public static bool ShouldBuildMesh(bool known, bool pending, float now, float lastBuiltAt,
            float refreshSeconds)
        {
            if (pending) return false;
            if (!known) return true;
            if (float.IsNaN(lastBuiltAt)) return true;
            if (refreshSeconds <= 0f) return true;
            if (float.IsNaN(now)) return false;
            return now - lastBuiltAt >= refreshSeconds;
        }

        /// <summary>Когда этот меш готовили в последний раз (NaN — ещё не готовили).</summary>
        private float LastBuiltAt(MeshId meshId)
        {
            float value;
            return lastBuiltAt.TryGetValue(meshId, out value) ? value : float.NaN;
        }

        /// <summary>
        /// ОДИН `GameObject` + `MeshCollider` НА `MeshId` — объект ПЕРЕИСПОЛЬЗУЕТСЯ.
        ///
        /// ЗАЧЕМ ЭТОТ МЕТОД (исправление деградации MR 20.11.2026). Раньше каждый вызов делал
        /// новый объект, а словарь `collidersById` перезаписывался — старый коллайдер оставался
        /// в `realWorldColliders` навсегда и рос вместе с покрытием (9383 коллайдера при пределе
        /// 400 мешей). Теперь повторная генерация меша пишет НОВУЮ геометриЮ В СТАРЫЙ объект:
        /// число объектов физики и число «плиток покрытия» ограничено числом уникальных мешей.
        /// </summary>
        internal MeshCollider EnsureMeshObject(MeshId meshId)
        {
            if (container == null)
            {
                var root = new GameObject(ContainerName);
                root.transform.SetParent(transform, false);
                container = root.transform;
            }

            MeshCollider existing;
            if (collidersById.TryGetValue(meshId, out existing) && existing != null) return existing;

            var go = new GameObject("KvMrMesh_" + meshId);
            go.transform.SetParent(container, false);
            // Коллайдер живёт в начале координат трекинга: вершины меша от Unity уже в этой системе.
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.convex = false;
            collidersById[meshId] = collider;
            return collider;
        }

        /// <summary>ОДИН `Mesh` НА `MeshId` — тот же принцип переиспользования, что у коллайдера.</summary>
        internal Mesh EnsureMeshData(MeshId meshId)
        {
            Mesh mesh;
            if (meshesById.TryGetValue(meshId, out mesh) && mesh != null) return mesh;

            mesh = new Mesh();
            mesh.name = "KvMrMesh_" + meshId;
            meshesById[meshId] = mesh;
            return mesh;
        }

        /// <summary>СКОЛЬКО ОБЪЕКТОВ ФИЗИКИ ПОСТРОЕНО (для теста: не больше числа мешей).</summary>
        internal int MeshObjectCount { get { return collidersById.Count; } }

        /// <summary>
        /// ЗАЧЕСТАТЬ УСПЕШНУЮ ГЕНЕРАЦИЮ МЕША — ОДИН РАЗ НА КОЛЛАЙДЕР.
        ///
        /// Возвращает `true`, если это была НОВАЯ поверхность (для счётчика «мешей N»).
        /// `realWorldColliders` — `HashSet`, поэтому повторное зачисление того же коллайдера
        /// не увеличивает число поверхностей: это и есть страховка от накопления.
        /// </summary>
        internal bool RecordGeneratedMesh(MeshId meshId, MeshCollider collider, Mesh mesh)
        {
            if (collider == null) return false;
            if (mesh != null) collider.sharedMesh = mesh;
            bool isNew = realWorldColliders.Add(collider);

            // РАЗБОР МЕША КАК ПОВЕРХНОСТИ. Куски пространственного меша приходят без разметки:
            // «что это» приходится понимать по габаритам. Плоский в высоту кусок (≤ 0.10 м) —
            // кандидат в горизонтальную поверхность (стол/пол), высокий — стена или мебель.
            candidatesByCollider[collider] = BuildMeshCandidate(mesh, Time.unscaledTime - startedAt);
            knownMeshIds.Add(meshId);
            Ready = realWorldColliders.Count > 0;
            return isNew;
        }

        /// <summary>Построить невидимый коллайдер из меша комнаты.</summary>
        private void BuildMesh(MeshId meshId)
        {
            if (knownMeshIds.Count >= maxMeshes && !knownMeshIds.Contains(meshId))
            {
                Debug.LogWarning("[KvMr] реальные поверхности: достигнут предел " + maxMeshes +
                                 " мешей — новые не принимаются (защита памяти)");
                return;
            }

            pendingMeshIds.Add(meshId);
            lastBuiltAt[meshId] = Time.unscaledTime;

            MeshCollider collider = EnsureMeshObject(meshId);
            Mesh mesh = EnsureMeshData(meshId);

            try
            {
                subsystem.GenerateMeshAsync(meshId, mesh, collider, MeshVertexAttributes.Normals,
                    result => OnMeshGenerated(result, collider, mesh));
            }
            catch (Exception e)
            {
                pendingMeshIds.Remove(meshId);
                failedMeshes++;
                Debug.LogWarning("[KvMr] реальные поверхности: построение меша бросило " +
                                 e.GetType().Name + ": " + e.Message);
            }
        }

        private void OnMeshGenerated(MeshGenerationResult result, MeshCollider collider, Mesh mesh)
        {
            pendingMeshIds.Remove(result.MeshId);

            if (result.Status != MeshGenerationStatus.Success)
            {
                failedMeshes++;
                if (failedMeshes <= 3)
                    Debug.LogWarning("[KvMr] реальные поверхности: меш не построен, статус " + result.Status);
                RemoveMesh(result.MeshId);
                return;
            }

            bool wasReady = Ready;
            bool isNew = RecordGeneratedMesh(result.MeshId, collider, mesh);
            if (isNew) builtMeshes++;

            if (logEveryMesh)
                Debug.Log("[KvMr] реальные поверхности: меш " + result.MeshId + " · вершин " + mesh.vertexCount);

            if (!wasReady)
                Debug.Log("[KvMr] " + Status);
        }

        /// <summary>
        /// ГАБАРИТЫ КУСКА МЕША → КАНДИДАТ В СТОЛ.
        ///
        /// ЧЕСТНО О ГРАНИЦЕ: у меша нет ни семантики, ни нормали поверхности, поэтому берётся
        /// ограничивающий параллелепипед (`mesh.bounds`) в системе координат трекинга — объект
        /// коллайдера стоит в её начале. Плоский кусок считается горизонтальной поверхностью, а его
        /// «площадь» — площадь проекции. Этого достаточно, чтобы ОТЛИЧИТЬ пол от стола по высоте, но
        /// недостаточно, чтобы ручаться за каждый кусок; поэтому такие кандидаты помечены источником
        /// `Mesh` и получают меньшую уверенность, чем разметка PICO.
        /// </summary>
        private static RealSurfaceCandidate BuildMeshCandidate(Mesh mesh, float trackedSeconds)
        {
            Bounds bounds = mesh != null ? mesh.bounds : new Bounds();
            bool flat = bounds.size.y <= MeshFlatHeightMeters;

            RealSurfaceCandidate candidate;
            candidate.Kind = flat ? KvRealSurfaceKind.Unknown : KvRealSurfaceKind.Wall;
            candidate.Source = KvRealSurfaceSource.Mesh;
            candidate.Center = bounds.center;
            // Не горизонтальная поверхность: нормаль вбок — правило наклона её отвергнет.
            candidate.Up = flat ? Vector3.up : Vector3.forward;
            candidate.SideA = bounds.size.x;
            candidate.SideB = bounds.size.z;
            candidate.TrackedSeconds = trackedSeconds;
            candidate.IsLargestHorizontal = false;
            return candidate;
        }

        private void RemoveMesh(MeshId meshId)
        {
            MeshCollider collider;
            if (collidersById.TryGetValue(meshId, out collider) && collider != null)
            {
                realWorldColliders.Remove(collider);
                candidatesByCollider.Remove(collider);
                Destroy(collider.gameObject);
            }
            collidersById.Remove(meshId);

            Mesh mesh;
            if (meshesById.TryGetValue(meshId, out mesh) && mesh != null) Destroy(mesh);
            meshesById.Remove(meshId);

            knownMeshIds.Remove(meshId);
            lastBuiltAt.Remove(meshId);
            Ready = realWorldColliders.Count > 0;
        }

        /// <summary>
        /// ПОПАДАНИЕ ЛУЧА В РЕАЛЬНУЮ ПОВЕРХНОСТЬ.
        ///
        /// ПОЧЕМУ НЕ ПРОСТО Physics.Raycast. В сцене есть виртуальные коллайдеры (робот, панели);
        /// луч по столу не должен цеплять их. Поэтому берутся ВСЕ попадания и из них выбирается
        /// ближайшее, чей коллайдер принадлежит НАШЕМУ списку реальных поверхностей. Никаких
        /// изменений слоёв проекта (это задело бы VR).
        /// </summary>
        public bool TryRaycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            if (!Ready || realWorldColliders.Count == 0) return false;

            // ОТСЕВ НЕЧИСЛА (класс E-21). Данные приходят ИЗВНЕ — от позы контроллера; при потере
            // отслеживания там штатно появляется NaN. Любое сравнение длины с NaN даёт false, то
            // есть «проверка» пропустила бы мусор, а луч ушёл бы в никуда (и предмет мог бы
            // «дрожать»). Явные IsNaN/IsInfinity — единственная надёжная отбраковка.
            if (!IsFiniteVector(origin) || !IsFiniteVector(direction) || float.IsNaN(maxDistance) ||
                float.IsInfinity(maxDistance))
                return false;

            if (direction.sqrMagnitude < 0.0001f) return false;

            // БЕЗ АЛЛОКАЦИЙ НА КАЖДЫЙ КАДР. `Physics.RaycastAll` возвращает НОВЫЙ массив, а лучи
            // в шлеме проверяются каждый кадр (правый, левый, постановка, выбор места робота) —
            // на длинном прогоне это постоянный поток мусора и остановки сборщика, то есть
            // ровно та «просадка во времени». Взял `NonAlloc` с переиспользуемым буфером:
            // буфер растёт редко и только вверх.
            int count = Physics.RaycastNonAlloc(origin, direction.normalized, hitsBuffer, maxDistance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (count >= raycastBuffer.Length)
            {
                // Буфер переполнен — попадания обрезаны. Растим и повторяем один раз, иначе луч
                // «не дотянул» бы до поверхности, лежащей за краем буфера.
                var bigger = new Collider[raycastBuffer.Length * 2];
                System.Array.Copy(raycastBuffer, bigger, raycastBuffer.Length);
                raycastBuffer = bigger;
                hitsBuffer = new RaycastHit[raycastBuffer.Length];
                count = Physics.RaycastNonAlloc(origin, direction.normalized, hitsBuffer, maxDistance,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
            }

            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                Collider c = raycastBuffer[i];
                if (c == null || !realWorldColliders.Contains(c)) continue;
                RaycastHit current = hitsBuffer[i];
                if (current.distance < best)
                {
                    best = current.distance;
                    hit = current;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Буферы попаданий луча — переиспользуются между кадрами (сборка мусора: ноль).</summary>
        private Collider[] raycastBuffer = new Collider[64];
        private RaycastHit[] hitsBuffer = new RaycastHit[64];

        /// <summary>ЧИСЛО ЛИ ЭТО (отсев NaN/бесконечности — класс E-21).</summary>
        private static bool IsFiniteVector(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        // ---------------------------------------------------------------- стол это или просто поверхность

        /// <summary>
        /// ЧТО ЗА ПОВЕРХНОСТЬ ПОПАЛА ПОД ЛУЧ — решение классификатора, а не догадка.
        ///
        /// ЗАЧЕМ. ЧТО БЫЛО: под лучом в MR была «реальная поверхность», и всё, что горизонтально,
        /// называлось столом. Но пол в шлеме — тоже горизонтальная поверхность с коллайдером,
        /// поэтому «постановка на реальную поверхность» с равным успехом означала «робот на полу».
        /// ТЕПЕРЬ `KvMrPlacement` спрашивает у этого метода, подтверждён ли стол, и подписывает
        /// постановку честно.
        /// </summary>
        public bool TryGetSurfaceVerdict(Collider collider, out RealTableVerdict verdict)
        {
            RealSurfaceCandidate candidate;
            if (collider == null || !candidatesByCollider.TryGetValue(collider, out candidate))
            {
                verdict.IsTable = false;
                verdict.Confidence = 0f;
                verdict.Kind = KvRealSurfaceKind.Unknown;
                verdict.Reason = "поверхность не разобрана классификатором · уверенность 0.00";
                return false;
            }

            verdict = KvRealTableClassifier.Classify(candidate, EyeHeightMeters(), FloorHeight, FloorFound);
            return true;
        }

        /// <summary>Строка о поверхности под лучом — для журнала постановки.</summary>
        public string DescribeSurface(Collider collider)
        {
            RealTableVerdict verdict;
            RealSurfaceCandidate candidate;
            if (!TryGetSurfaceVerdict(collider, out verdict)) return verdict.Reason;
            if (!candidatesByCollider.TryGetValue(collider, out candidate)) return verdict.Reason;
            return KvRealTableClassifier.Describe(candidate, verdict, FloorHeight, FloorFound) +
                   " · источник " + candidate.Source;
        }

        /// <summary>
        /// ВЫСОТА ГЛАЗ ДЛЯ ПРАВИЛА «СТОЛ НИЖЕ ГЛАЗ». Берётся у главной камеры: её поза — это поза
        /// головы оператора в координатах трекинга, в которых приходят и меши, и разметка PICO.
        /// Нет камеры — 0 («высота глаз неизвестна»), и правило уровня глаз просто не применяется.
        ///
        /// ПОЧЕМУ НЕ ТОЛЬКО `Camera.main`: в шлеме камера может быть без метки MainCamera, и тогда
        /// высота глаз всегда была бы «неизвестна», а правило уровня глаз — выключено молча.
        /// Ссылка запоминается: поиск по сцене в каждом вызове — лишняя работа на кадре.
        /// </summary>
        private float EyeHeightMeters()
        {
            if (eyeCamera == null) eyeCamera = Camera.main;
            if (eyeCamera == null) eyeCamera = FindAnyObjectByType<Camera>();
            return eyeCamera != null ? eyeCamera.transform.position.y : 0f;
        }

        /// <summary>
        /// ПЕРЕСЧЁТ «СТОЛ НАЙДЕН / ПОЛ НАЙДЕН» И ОДНА СТРОКА В ЖУРНАЛ — не чаще раза в секунду.
        ///
        /// ЧТО БЫЛО. В журнале появлялись строки «меш построен», «получено столов N», и ни одна не
        /// говорила, найден ли РЕАЛЬНЫЙ стол: N считалось по запросу, а не по проверке. Тестировщик
        /// в шлеме видел робота на полу и не мог отличить это от успеха.
        /// ЧТО СТАЛО. Раз в секунду классификатор проходит по всем поверхностям, считает столы и
        /// пол, и в журнал уходит ОДНА строка с числами — только если она изменилась.
        /// </summary>
        private void UpdateRealTableReport()
        {
            if (Time.unscaledTime < nextReportTime) return;
            nextReportTime = Time.unscaledTime + RealTableReportSeconds;
            if (candidatesByCollider.Count == 0)
            {
                DescribeRealTable = "стол не найден: реальные поверхности ещё не получены · уверенность 0.00";
                return;
            }

            // УРОВЕНЬ ПОЛА. Разметка PICO — точный ответ; если её нет, выводим по двум уровням
            // поверхностей (см. `TryInferFloorHeight`), иначе правило «стол выше пола» не работает.
            if (!floorFromSemantics)
            {
                reportCandidates.Clear();
                foreach (KeyValuePair<Collider, RealSurfaceCandidate> pair in candidatesByCollider)
                    if (pair.Key != null) reportCandidates.Add(pair.Value);

                float inferred;
                if (KvRealTableClassifier.TryInferFloorHeight(reportCandidates, out inferred))
                {
                    FloorHeight = inferred;
                    FloorFound = true;
                }
            }

            float eye = EyeHeightMeters();
            int tables = 0;
            int floors = 0;
            bool hasTable = false;
            bool hasReject = false;
            RealSurfaceCandidate bestTable = default(RealSurfaceCandidate);
            RealTableVerdict bestTableVerdict = default(RealTableVerdict);
            RealSurfaceCandidate strongestReject = default(RealSurfaceCandidate);
            RealTableVerdict strongestRejectVerdict = default(RealTableVerdict);

            foreach (KeyValuePair<Collider, RealSurfaceCandidate> pair in candidatesByCollider)
            {
                if (pair.Key == null) continue;

                // СТЕНЫ И ВЕРТИКАЛЬНЫЕ КУСКИ МЕША НЕ РАЗБИРАЕМ ВОВСЕ. Их отказ известен заранее
                // (нормаль вбок), а разбор строит строку с числами: на 400 кусках комнаты это
                // 400 строк в секунду на ровном месте. Экономия — не «оптимизация ради галочки»:
                // в шлеме кадровый бюджет общий с отрисовкой.
                if (pair.Value.Kind == KvRealSurfaceKind.Wall ||
                    pair.Value.Kind == KvRealSurfaceKind.Ceiling) continue;

                RealTableVerdict verdict = KvRealTableClassifier.Classify(pair.Value, eye, FloorHeight, FloorFound);
                if (verdict.IsTable)
                {
                    tables++;
                    if (!hasTable || verdict.Confidence > bestTableVerdict.Confidence)
                    {
                        hasTable = true;
                        bestTableVerdict = verdict;
                        bestTable = pair.Value;
                    }
                    continue;
                }

                if (verdict.Kind == KvRealSurfaceKind.Floor) floors++;
                if (!hasReject || verdict.Confidence > strongestRejectVerdict.Confidence)
                {
                    hasReject = true;
                    strongestRejectVerdict = verdict;
                    strongestReject = pair.Value;
                }
            }

            TableCount = tables;
            FloorColliders = floors;

            if (TableCount > 0)
            {
                DescribeRealTable = KvRealTableClassifier.Describe(bestTable, bestTableVerdict,
                    FloorHeight, FloorFound) + " · таких поверхностей " + TableCount;
            }
            else if (FloorFound)
            {
                DescribeRealTable = "стол не найден: найдены только поверхности на уровне пола (" +
                                    Num(FloorHeight) + " м), их " + FloorColliders +
                                    " — пол столом не считается · уверенность 0.00";
            }
            else if (hasReject)
            {
                DescribeRealTable = KvRealTableClassifier.Describe(strongestReject, strongestRejectVerdict,
                    FloorHeight, FloorFound) + " · поверхностей " + candidatesByCollider.Count;
            }
            else
            {
                DescribeRealTable = "стол не найден: поверхности не разобраны · поверхностей " +
                                    candidatesByCollider.Count + " · уверенность 0.00";
            }

            if (DescribeRealTable == lastReportLine) return;
            lastReportLine = DescribeRealTable;
            Debug.Log("[KvMr] РЕАЛЬНЫЙ СТОЛ: " + DescribeRealTable +
                      " · пол " + (FloorFound ? "найден на " + Num(FloorHeight) + " м" : "не найден") +
                      " · коллайдеров " + realWorldColliders.Count);
        }

        private static string Num(float value)
        {
            if (float.IsNaN(value)) return "нечисло";
            if (float.IsInfinity(value)) return "бесконечность";
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- запасной источник: сцена PICO

        /// <summary>
        /// СПРОСИТЬ У PICO РАЗМЕЧЕННЫЕ ОБЪЕКТЫ КОМНАТЫ (семантическая сцена) — запасной путь,
        /// когда меш комнаты не дал полигонов.
        ///
        /// ЗАЧЕМ. Меш требует надетого шлема и разметки помещения, а у PICO есть второй источник:
        /// размеченные объекты комнаты, где СТОЛ приходит параллелепипедом с габаритами в метрах.
        /// Для задачи «поставить робота на свой стол» этого достаточно: строим невидимый
        /// BoxCollider по габаритам стола, и луч постановки начинает в него попадать.
        ///
        /// КОНВЕРСИЯ КООРДИНАТ — ТОНКОЕ МЕСТО (проверено по исходникам пакета PICO 1.4.1):
        /// поза СУЩНОСТИ пакет переводит в Unity сам (инверсия Z), а позу БОКСА — НЕТ. Поэтому
        /// бокс переводится здесь: позиция с -z, кватернион с -z/-w, затем накладывается поза
        /// сущности. Иначе стол оказался бы зеркально отражён относительно оператора.
        ///
        /// ЧТО ИЗМЕНИЛОСЬ (S46, наблюдаемость). Тело больше не печатает «по месту»: каждый исход
        /// заворачивается в `KvSceneQueryOutcome` и уходит В ОДНУ ВОРОНКУ `ReportSceneQuery`, которая
        /// печатает ровно одну итоговую строку — с секундами, числом столов, полом и причиной.
        /// Причина: при прежнем устройстве любой неотвеченный `await` означал ПОЛНУЮ тишину, а
        /// повторный `Debug.Log` в каждой ветке дал бы две разные «правды» в одном журнале.
        /// </summary>
        private async void RequestSceneAnchors()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                ReportSceneQuery(KvSceneQueryOutcome.NotApplicable, 0, FloorFound,
                    Time.unscaledTime - sceneQueryStartedAt, "платформа " + Application.platform);
                return;
            }
            if (KvMrRuntime.Backend != KvMrBackend.Pico)
            {
                ReportSceneQuery(KvSceneQueryOutcome.NotApplicable, 0, FloorFound,
                    Time.unscaledTime - sceneQueryStartedAt, "бэкенд " + KvMrRuntime.Backend);
                return;
            }

            sceneAnchorRunning = true;
            KvSceneQueryOutcome outcome = KvSceneQueryOutcome.Failed;
            string detail = "ответа нет";
            int tables = 0;
            // ЧТО БЫЛО ЗДЕСЬ ДО ПРАВКИ. Запрос вызывался только при `!Ready`, то есть вопрос
            // «что говорит разметка» вообще не задавался, если меш уже дал хотя бы один коллайдер.
            // ЧТО СТАЛО. Спрашиваем ВСЕГДА (один раз за сессию): оператор должен видеть число столов
            // и когда меш работает. А вот ПОСТРОЕНИЕ КОЛЛАЙДЕРОВ по-прежнему зависит от `!Ready` —
            // поведение постановки не меняется.
            bool meshAlreadyReady = Ready;
            try
            {
                // СТРОКА О НАЧАЛЕ ЗАПРОСА. Нужна, чтобы ожидание было видно СРАЗУ: тестировщик
                // знает, что приложение спросило разметку и сколько ждать ответа, — и не гадает,
                // работает оно или уже сломалось.
                Debug.Log("[KvMr] РЕАЛЬНЫЙ СТОЛ: спрашиваю у PICO разметку комнаты (PxrSemanticLabel.Table + Floor), " +
                          "предел ожидания " + Num(sceneAnchorTimeoutSeconds) + " с");

                PxrResult start = await WithTimeout(
                    PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.SceneCapture), 8000);
                if (start != PxrResult.SUCCESS)
                {
                    outcome = KvSceneQueryOutcome.Failed;
                    detail = "провайдер сцены не запустился, код " + start;
                }
                else
                {
                    // УРОВЕНЬ ПОЛА СПРАШИВАЕМ ПЕРВЫМ: он нужен правилу «стол обязан быть выше пола на
                    // 0.25 м», и без него стол можно спутать с полом.
                    await RequestFloorHeight();

                    var query = await WithTimeout(
                        PXR_MixedReality.QuerySceneAnchorAsync(new[] { PxrSemanticLabel.Table }), 12000);
                    if (query.result != PxrResult.SUCCESS)
                    {
                        outcome = KvSceneQueryOutcome.Failed;
                        detail = "запрос PxrSemanticLabel.Table вернул " + query.result;
                    }
                    else
                    {
                        // ПОСТАНОВКА НЕ МЕНЯЕТСЯ. Коллайдеры стола строятся ТОЛЬКО если меш комнаты
                        // поверхностей не дал — как и было до правки. Разметка комнаты могла
                        // устареть (стол переставили после разметки), и её параллелепипед спорил бы
                        // с живым мешем. Но ОТЧИТЫВАЕТСЯ ответ всегда: число столов оператор видит,
                        // даже когда строить ничего не нужно.
                        tables = CreateTableColliders(query.anchorHandleList, !meshAlreadyReady);
                        outcome = tables > 0 ? KvSceneQueryOutcome.Found : KvSceneQueryOutcome.Empty;
                        detail = meshAlreadyReady
                            ? "PxrSemanticLabel.Table; коллайдеры не строил — поверхности уже даёт меш"
                            : "PxrSemanticLabel.Table";
                    }
                }
            }
            catch (TimeoutException)
            {
                outcome = KvSceneQueryOutcome.Timeout;
                detail = "нативный вызов PICO не вернулся";
            }
            catch (Exception e)
            {
                outcome = KvSceneQueryOutcome.Failed;
                detail = "ошибка запроса сцены " + e.GetType().Name + ": " + e.Message;
            }
            finally
            {
                sceneAnchorRunning = false;
            }

            // ОДНА ИТОГОВАЯ СТРОКА — каким бы путём запрос ни закончился (см. `ReportSceneQuery`).
            ReportSceneQuery(outcome, tables, FloorFound, Time.unscaledTime - sceneQueryStartedAt, detail);
        }

        /// <summary>
        /// ПОСЧИТАТЬ СТОЛЫ В ОТВЕТЕ РАЗМЕТКИ И, ЕСЛИ НУЖНО, ПОСТРОИТЬ ДЛЯ НИХ КОЛЛАЙДЕРЫ.
        /// Возвращает число столов в разметке (0 — разметка не дала ни одного).
        ///
        /// ЧТО БЫЛО. Этот цикл жил внутри `RequestSceneAnchors`, а счётчик `tableColliders`
        /// присваивался ОДИН раз ПОСЛЕ цикла — поэтому все объекты назывались `KvMrRealTable_0`, и
        /// счётчик нельзя было читать во время создания.
        /// ЧТО СТАЛО. Цикл вынесен в метод, счётчик живой (по нему называются объекты и считается
        /// строка статуса), а `Ready` поднимается здесь же — ровно как раньше.
        ///
        /// `buildColliders = false` — это случай «меш уже даёт поверхности»: тогда столы ТОЛЬКО
        /// СЧИТАЮТСЯ (оператор видит число), а коллайдеры не строятся, чтобы устаревшая разметка не
        /// спорила с живым мешем. Постановка робота от этого не меняется.
        /// </summary>
        private int CreateTableColliders(List<ulong> handles, bool buildColliders)
        {
            if (handles == null) return 0;

            int created = 0;
            for (int i = 0; i < handles.Count; i++)
            {
                ulong handle = handles[i];

                PxrSceneComponentType[] types;
                if (PXR_MixedReality.GetSceneAnchorComponentTypes(handle, out types) != PxrResult.SUCCESS) continue;

                bool hasBox = false;
                for (int k = 0; k < types.Length; k++)
                    if (types[k] == PxrSceneComponentType.Box3D) hasBox = true;
                if (!hasBox) continue;

                Vector3 boxPos, boxExtent;
                Quaternion boxRot;
                Vector3 entityPos;
                Quaternion entityRot;
                if (PXR_MixedReality.GetSceneBox3DData(handle, out boxPos, out boxRot, out boxExtent) != PxrResult.SUCCESS)
                    continue;
                if (PXR_MixedReality.LocateAnchor(handle, out entityPos, out entityRot) != PxrResult.SUCCESS)
                    continue;

                if (buildColliders) CreateTableCollider(entityPos, entityRot, boxPos, boxRot, boxExtent);
                created++;
                tableColliders = created;
            }

            // Столы из разметки — такие же реальные поверхности для луча, как и меш комнаты.
            if (buildColliders && created > 0) Ready = true;
            return created;
        }

        /// <summary>
        /// ЕДИНСТВЕННАЯ ТОЧКА, ГДЕ ПЕЧАТАЕТСЯ ИТОГ ЗАПРОСА РАЗМЕТКИ (S46).
        ///
        /// ПОЧЕМУ ВОРОНКА, А НЕ `Debug.Log` В КАЖДОЙ ВЕТКЕ. Итог может прийти из ДВУХ мест: от самой
        /// задачи PICO (когда она всё-таки вернулась) и от жёсткого предела ожидания на главном
        /// потоке (`UpdateSceneAnchorQuery`). Первый пришедший печатает итоговую строку; второй —
        /// только диагностику. Так в журнале не бывает двух разных «итогов», и оператору не надо
        /// гадать, какому верить.
        /// </summary>
        private void ReportSceneQuery(KvSceneQueryOutcome outcome, int tables, bool floorFound,
            float seconds, string detail)
        {
            if (sceneQueryReported)
            {
                Debug.Log("[KvMr] РЕАЛЬНЫЙ СТОЛ: после итога пришёл поздний ответ разметки (" +
                          SceneQueryOutcomeText(outcome) + ", столов " + (tables > 0 ? tables : 0) +
                          ", за " + Num(seconds) + " с) — постановка не меняется");
                return;
            }

            sceneQueryReported = true;
            string line = DescribeSceneQuery(outcome, tables, floorFound, seconds, detail);
            if (outcome == KvSceneQueryOutcome.Found) Debug.Log(line);
            else Debug.LogWarning(line);

            // ЧТО ДЕЛАТЬ ОПЕРАТОРУ — отдельной строкой и только когда есть что делать. Без неё
            // «столов 0» читается как «в комнате нет стола», а не как «включите разметку комнаты».
            if (SceneQueryNeedsOperatorAction(outcome)) Debug.LogWarning(SceneQueryOperatorHint());
        }

        // ---------------------------------------------------------------- итог запроса: чистые функции

        /// <summary>
        /// ИТОГ ЗАПРОСА РАЗМЕТКИ ОДНОЙ СТРОКОЙ — ЧИСТАЯ ФУНКЦИЯ (проверяется тестом без шлема).
        ///
        /// ЗАЧЕМ ЧИСТАЯ. На устройстве строку иначе не проверить: журнал шлема читается глазами, а
        /// «три исхода различимы» и «в каждой строке есть числа» нужно доказать машинно. Здесь нет ни
        /// `Time`, ни `Debug`, ни состояния — только вход и текст.
        ///
        /// ФОРМА (одна на все исходы, как требует приёмка):
        ///     [KvMr] РЕАЛЬНЫЙ СТОЛ: запрос выполнен за 12.40 с · столов 2 · пол есть · причина …
        /// </summary>
        public static string DescribeSceneQuery(KvSceneQueryOutcome outcome, int tables, bool floorFound,
            float seconds, string detail)
        {
            int found = tables > 0 ? tables : 0;
            string reason;
            switch (outcome)
            {
                case KvSceneQueryOutcome.Found:
                    reason = "столы найдены — разметка комнаты PICO отдала " + found + " шт. (" +
                             DetailText(detail) + ")";
                    break;
                case KvSceneQueryOutcome.Empty:
                    reason = "разметки комнаты нет — запрос " + DetailText(detail) + " вернул 0 объектов";
                    break;
                case KvSceneQueryOutcome.Timeout:
                    reason = "ТАЙМ-АУТ: не ответил за " + Num(seconds) + " с (" + DetailText(detail) + ")";
                    break;
                case KvSceneQueryOutcome.Failed:
                    reason = "запрос не выполнился (" + DetailText(detail) + ")";
                    break;
                default:
                    reason = "спрашивать нечем (" + DetailText(detail) + ")";
                    break;
            }

            return "[KvMr] РЕАЛЬНЫЙ СТОЛ: запрос выполнен за " + Num(seconds) + " с · столов " + found +
                   " · пол " + (floorFound ? "есть" : "нет") + " · причина " + reason;
        }

        /// <summary>
        /// НУЖНО ЛИ ОТ ОПЕРАТОРА ДЕЙСТВИЕ. Отдельная функция, потому что подсказку нельзя печатать при
        /// успехе: «включите разметку» поверх «стол найден» читается как поломка.
        /// </summary>
        public static bool SceneQueryNeedsOperatorAction(KvSceneQueryOutcome outcome)
        {
            return outcome == KvSceneQueryOutcome.Empty ||
                   outcome == KvSceneQueryOutcome.Timeout ||
                   outcome == KvSceneQueryOutcome.Failed ||
                   outcome == KvSceneQueryOutcome.NotApplicable;
        }

        /// <summary>
        /// ЧТО ОПЕРАТОРУ СДЕЛАТЬ, ЕСЛИ СТОЛОВ 0 ИЛИ ТАЙМ-АУТ — ОДНА СТРОКА (S46).
        ///
        /// ПОЧЕМУ ПУТЬ В МЕНЮ НЕ НАЗВАН. Пункт меню — факт про прошивку шлема, а не про наш код.
        /// В документации SDK, которая лежит в проекте (`Packages/com.unity.xr.openxr.picoxr`),
        /// пунктов меню нет вовсе (проверено поиском по пакету: `room setup`, `space setup`,
        /// «пространственные данные» не встречаются ни разу). Выдумать путь проще всего — и хуже
        /// всего: оператор пойдёт по несуществующему меню и решит, что сломано приложение. Поэтому
        /// сказано нейтрально и точно: «включите разметку комнаты в настройках PICO».
        /// </summary>
        public static string SceneQueryOperatorHint()
        {
            return "[KvMr] РЕАЛЬНЫЙ СТОЛ: разметки помещения нет — включите разметку комнаты в настройках PICO, " +
                   "иначе поставить робота на ваш стол нельзя; пока постановка идёт по поверхностям меша.";
        }

        /// <summary>
        /// СТРОКА ОЖИДАНИЯ ПОВЕРХНОСТЕЙ — ЧИСТАЯ ФУНКЦИЯ (проверяется тестом без шлема).
        ///
        /// ФОРМА: `[KvMr] реальные поверхности: меш 0 · семантика 0 · ждём 12.40 с · что делать: …`
        /// В каждом исходе есть ЧИСЛА (мешей, семантики, секунд) — иначе тестировщик не отличит
        /// «ждём» от «сломалось», а приёмка `REAL_TABLE` требует вердикта с числом.
        /// </summary>
        public static string DescribeWaitingSurfaces(int meshSurfaces, int semanticSurfaces, float waitedSeconds)
        {
            int mesh = meshSurfaces > 0 ? meshSurfaces : 0;
            int semantic = semanticSurfaces > 0 ? semanticSurfaces : 0;

            string hint;
            if (mesh > 0 || semantic > 0)
            {
                // Поверхности уже есть: подсказка меняется, чтобы оператор не продолжал «ждать».
                hint = "поверхности есть — наведите луч на столешницу и нажмите A";
            }
            else if (waitedSeconds >= 20f)
            {
                hint = "поводите шлемом по комнате, держа стол в поле зрения: меш строится только по видимым " +
                       "поверхностям; если так и тихо — включите разметку комнаты в настройках PICO";
            }
            else
            {
                hint = "ждём первые полигоны меша — поводите шлемом по комнате (стол должен быть в поле зрения)";
            }

            return "[KvMr] реальные поверхности: меш " + mesh + " · семантика " + semantic +
                   " · ждём " + Num(waitedSeconds) + " с · что делать: " + hint;
        }

        /// <summary>
        /// ЧЕРЕЗ СКОЛЬКО ПЕЧАТАТЬ СЛЕДУЮЩУЮ СТРОКУ ОЖИДАНИЯ, секунды.
        ///
        /// ПОЧЕМУ НЕ «РАЗ В СЕКУНДУ ВСЕГДА». Пять минут ожидания — это 300 одинаковых строк, в
        /// которых утонет всё остальное. Поэтому интервал растёт: до 20 с — раз в секунду, до
        /// минуты — раз в 5 с, дальше — раз в 15 с. Ни одно значение не меньше 1 с: «не чаще раза в
        /// секунду» — это требование, а не пожелание.
        /// </summary>
        public static float WaitingStatusIntervalSeconds(float waitedSeconds)
        {
            // Нечисло не должно превращаться в «печатать каждый кадр»: NaN в сравнениях даёт false.
            if (float.IsNaN(waitedSeconds) || waitedSeconds < 0f) return 1f;
            if (waitedSeconds < 20f) return 1f;
            if (waitedSeconds < 60f) return 5f;
            return 15f;
        }

        /// <summary>
        /// ПОРА ЛИ СПРАШИВАТЬ РАЗМЕТКУ — ЧИСТАЯ ФУНКЦИЯ (S46).
        ///
        /// ЧТО БЫЛО. Условие стояло прямо в `Update` и включало `!Ready`: стоило появиться ОДНОМУ
        /// коллайдеру меша — запрос разметки не выполнялся больше никогда, и в журнале не было ни
        /// «столов N», ни «разметки нет».
        /// ЧТО СТАЛО. Решение вынесено в функцию с явными входами и проверяется тестом, включая
        /// главное: ПРИЗНАК «УЖЕ СПРАШИВАЛИ» БЛОКИРУЕТ ВТОРОЙ ЗАПРОС В ТОЙ ЖЕ СЕССИИ.
        /// </summary>
        public static bool ShouldRequestSceneAnchors(bool alreadyRequested, bool alreadyRunning,
            bool subsystemSettled, float secondsSinceTrackingStart, float delaySeconds)
        {
            if (alreadyRequested || alreadyRunning) return false;   // не чаще раза в сессию
            if (!subsystemSettled) return false;                    // ждём, чем кончился старт подсистемы
            if (float.IsNaN(secondsSinceTrackingStart) || float.IsNaN(delaySeconds)) return false;
            return secondsSinceTrackingStart >= delaySeconds;
        }

        /// <summary>Слово для журнала: чем кончился запрос (для диагностики позднего ответа).</summary>
        public static string SceneQueryOutcomeText(KvSceneQueryOutcome outcome)
        {
            switch (outcome)
            {
                case KvSceneQueryOutcome.Found: return "столы найдены";
                case KvSceneQueryOutcome.Empty: return "столов нет";
                case KvSceneQueryOutcome.Timeout: return "тайм-аут";
                case KvSceneQueryOutcome.Failed: return "ошибка запроса";
                default: return "запрос не применим";
            }
        }

        /// <summary>Подробность для итоговой строки: пустая строка — это «промолчали», так нельзя.</summary>
        private static string DetailText(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return "подробностей нет";
            return detail.Trim();
        }

        /// <summary>
        /// СПРОСИТЬ У PICO УРОВЕНЬ ПОЛА (`PxrSemanticLabel.Floor`).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНЫЙ ЗАПРОС. Правило «стол обязан быть выше пола на 0.25 м» работает, только
        /// если пол известен. Разметка PICO умеет отдавать пол отдельной сущностью — это точнее
        /// любого вывода по мешам, поэтому спрашиваем её ДО столов: уровень пола нужен уже при
        /// разборе первой столешницы.
        ///
        /// КОЛЛАЙДЕР ДЛЯ ПОЛА НЕ СТРОИТСЯ намеренно: луч постановки не должен цеплять пол как стол
        /// (иначе «поставил на стол» означало бы «поставил на пол»). Берём только высоту верхней
        /// грани параллелепипеда — это и есть уровень пола.
        /// </summary>
        private async Task RequestFloorHeight()
        {
            try
            {
                var floorQuery = await WithTimeout(
                    PXR_MixedReality.QuerySceneAnchorAsync(new[] { PxrSemanticLabel.Floor }), 12000);
                if (floorQuery.result != PxrResult.SUCCESS || floorQuery.anchorHandleList.Count == 0)
                {
                    Debug.Log("[KvMr] реальные поверхности: пол в разметке PICO не найден (" +
                              floorQuery.result + ", объектов " + floorQuery.anchorHandleList.Count +
                              ") — уровень пола выведу по двум уровням поверхностей");
                    return;
                }

                for (int i = 0; i < floorQuery.anchorHandleList.Count; i++)
                {
                    ulong handle = floorQuery.anchorHandleList[i];

                    PxrSceneComponentType[] types;
                    if (PXR_MixedReality.GetSceneAnchorComponentTypes(handle, out types) != PxrResult.SUCCESS) continue;

                    bool hasBox = false;
                    for (int k = 0; k < types.Length; k++)
                        if (types[k] == PxrSceneComponentType.Box3D) hasBox = true;
                    if (!hasBox) continue;

                    Vector3 boxPos, boxExtent;
                    Quaternion boxRot;
                    Vector3 entityPos;
                    Quaternion entityRot;
                    if (PXR_MixedReality.GetSceneBox3DData(handle, out boxPos, out boxRot, out boxExtent) != PxrResult.SUCCESS)
                        continue;
                    if (PXR_MixedReality.LocateAnchor(handle, out entityPos, out entityRot) != PxrResult.SUCCESS)
                        continue;

                    Vector3 localPos = new Vector3(boxPos.x, boxPos.y, -boxPos.z);
                    Quaternion localRot = new Quaternion(boxRot.x, boxRot.y, -boxRot.z, -boxRot.w);
                    Vector3 worldPos = entityPos + entityRot * localPos;
                    // Верхняя грань параллелепипеда = уровень пола (робот стоит на ней, а не в центре).
                    float top = worldPos.y + Mathf.Abs(boxExtent.y) * 0.5f;

                    if (!FloorFound || top < FloorHeight)
                    {
                        FloorHeight = top;
                        FloorFound = true;
                        floorFromSemantics = true;
                        Debug.Log("[KvMr] реальные поверхности: уровень пола из разметки PICO — " +
                                  Num(FloorHeight) + " м");
                    }
                    return;
                }
            }
            catch (TimeoutException)
            {
                Debug.LogWarning("[KvMr] реальные поверхности: запрос пола не ответил за отведённое время — " +
                                 "уровень пола выведу по двум уровням поверхностей");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KvMr] реальные поверхности: ошибка запроса пола: " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>Построить невидимый коллайдер стола по данным семантической сцены.</summary>
        private void CreateTableCollider(Vector3 entityPos, Quaternion entityRot, Vector3 boxPos,
            Quaternion boxRot, Vector3 extent)
        {
            Vector3 localPos = new Vector3(boxPos.x, boxPos.y, -boxPos.z);
            Quaternion localRot = new Quaternion(boxRot.x, boxRot.y, -boxRot.z, -boxRot.w);
            Vector3 worldPos = entityPos + entityRot * localPos;
            Quaternion worldRot = entityRot * localRot;

            var go = new GameObject("KvMrRealTable_" + tableColliders);
            go.transform.SetParent(container, false);
            go.transform.position = worldPos;
            go.transform.rotation = worldRot;

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = extent;
            realWorldColliders.Add(box);

            // РАЗМЕТКА PICO — САМЫЙ СИЛЬНЫЙ ИСТОЧНИК. Классификатор получает `Kind = Table` от
            // платформы, и это даёт уверенность 0.95 — выше любой геометрической догадки. Центром
            // поверхности берём ВЕРХНЮЮ грань параллелепипеда: именно на неё встанет робот, а не
            // внутрь стола.
            RealSurfaceCandidate candidate;
            candidate.Kind = KvRealSurfaceKind.Table;
            candidate.Source = KvRealSurfaceSource.Semantic;
            candidate.Center = worldPos + worldRot * (Vector3.up * (Mathf.Abs(extent.y) * 0.5f));
            candidate.Up = worldRot * Vector3.up;
            candidate.SideA = Mathf.Abs(extent.x);
            candidate.SideB = Mathf.Abs(extent.z);
            candidate.TrackedSeconds = Time.unscaledTime - startedAt;
            candidate.IsLargestHorizontal = false;
            candidatesByCollider[box] = candidate;

            var c = System.Globalization.CultureInfo.InvariantCulture;
            Debug.Log(string.Format(c,
                "[KvMr] реальный стол из разметки: центр ({0:0.00}; {1:0.00}; {2:0.00}) м · " +
                "размеры {3:0.00}×{4:0.00}×{5:0.00} м",
                worldPos.x, worldPos.y, worldPos.z, extent.x, extent.y, extent.z));
        }

        /// <summary>
        /// ЧТО ЗНАЧИТ МЕТКА PICO (чистая функция — проверяется тестом без шлема).
        ///
        /// Публично и статически специально: без устройства иначе не доказать, что «Table» из
        /// разметки действительно доходит до классификатора как семантика, а не теряется по дороге.
        /// </summary>
        public static KvRealSurfaceKind KindFromSemanticLabel(PxrSemanticLabel label)
        {
            switch (label)
            {
                case PxrSemanticLabel.Table: return KvRealSurfaceKind.Table;
                case PxrSemanticLabel.Floor: return KvRealSurfaceKind.Floor;
                case PxrSemanticLabel.Ceiling: return KvRealSurfaceKind.Ceiling;
                case PxrSemanticLabel.Wall:
                case PxrSemanticLabel.VirtualWall:
                case PxrSemanticLabel.Door:
                case PxrSemanticLabel.Window:
                    return KvRealSurfaceKind.Wall;
                case PxrSemanticLabel.Sofa:
                case PxrSemanticLabel.Chair:
                case PxrSemanticLabel.Opening:
                case PxrSemanticLabel.Human:
                    return KvRealSurfaceKind.Other;
                default: return KvRealSurfaceKind.Unknown;
            }
        }

        /// <summary>Ждать задачу не дольше тайм-аута: нативные запросы PICO могут не ответить вовсе.</summary>
        private static async Task<T> WithTimeout<T>(Task<T> task, int timeoutMs)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed != task) throw new TimeoutException();
            return await task;
        }

        /// <summary>Габарит комнаты по построенным мешам — для честной строки журнала.</summary>
        private string RoomSizeText()
        {
            if (realWorldColliders.Count == 0) return "0×0×0 м";
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            foreach (Collider c in realWorldColliders)
            {
                if (c == null) continue;
                Bounds b = c.bounds;
                min = Vector3.Min(min, b.min);
                max = Vector3.Max(max, b.max);
            }
            Vector3 size = max - min;
            return size.x.ToString("0.0") + "×" + size.y.ToString("0.0") + "×" + size.z.ToString("0.0") + " м";
        }
    }

    /// <summary>
    /// ЧЕМ КОНЧИЛСЯ ЗАПРОС РАЗМЕТКИ КОМНАТЫ (S46). Пять состояний вместо «повезло / не повезло»:
    ///
    ///   Found         — PICO отдал хотя бы один размеченный стол; постановка на реальный стол возможна;
    ///   Empty         — запрос выполнился, столов 0: комната не размечена (это НЕ «в комнате нет стола»);
    ///   Timeout       — ответа не было за жёсткий предел ожидания: ждать дальше бессмысленно;
    ///   Failed        — запрос вернул ошибку (например, не вышло разрешение на пространственные данные);
    ///   NotApplicable — спрашивать нечем: платформа не Android или бэкенд не PICO.
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ ТИП, А НЕ СТРОКА. Решение о тексте — чистая функция `DescribeSceneQuery`, её
    /// проверяет тест; а решение «нужно ли действие оператору» — `SceneQueryNeedsOperatorAction`.
    /// Со строкой в руках и то и другое пришлось бы угадывать по подстрокам.
    /// </summary>
    public enum KvSceneQueryOutcome
    {
        /// <summary>Разметка отдала столы.</summary>
        Found = 0,

        /// <summary>Запрос выполнился, но столов 0 — разметки комнаты нет.</summary>
        Empty = 1,

        /// <summary>Ответа не было за отведённое время.</summary>
        Timeout = 2,

        /// <summary>Провайдер сцены не запустился или запрос вернул ошибку.</summary>
        Failed = 3,

        /// <summary>Спрашивать нечем: не Android или не PICO.</summary>
        NotApplicable = 4
    }
}
