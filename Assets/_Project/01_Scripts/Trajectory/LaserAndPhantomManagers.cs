using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Материалы-«призраки» (бирюзовые, полупрозрачные) и неоновые материалы (HDRP).
    /// Прозрачность включается ровно так, как это делает сам HDRP (см. BaseUnlitAPI):
    ///   _SurfaceType = 1, ключ _SURFACE_TYPE_TRANSPARENT, _BlendMode = Alpha,
    ///   _SrcBlend = One, _DstBlend = OneMinusSrcAlpha, _ZWrite = 0, очередь 3000.
    /// Иначе материал остаётся непрозрачным и «фантом» выглядит как обычная модель.
    /// </summary>
    public static class GhostMaterial
    {
        // Значения из UnityEngine.Rendering.BlendMode
        private const float OneBlend = 1f;                  // One
        private const float OneMinusSrcAlphaBlend = 10f;    // OneMinusSrcAlpha

        public static void MakeGhost(Material m, Color tint, float alpha)
        {
            if (m == null) return;

            m.SetOverrideTag("RenderType", "Transparent");

            if (m.HasProperty("_SurfaceType")) m.SetFloat("_SurfaceType", 1f);          // Transparent
            // URP-признак прозрачности: у URP/Lit и URP/Unlit «прозрачный» материал — это
            // `_Surface = 1` и `_Blend = 0` (Alpha). Без этих двух строк под URP призраки,
            // зоны и теплокарты остались бы НЕПРОЗРАЧНЫМИ: `_SurfaceType` у URP-шейдеров нет.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);                   // URP: Transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);                       // URP: Alpha
            if (m.HasProperty("_BlendMode")) m.SetFloat("_BlendMode", 0f);             // Alpha
            if (m.HasProperty("_AlphaCutoffEnable")) m.SetFloat("_AlphaCutoffEnable", 0f);
            if (m.HasProperty("_AlphaCutoff")) m.SetFloat("_AlphaCutoff", 0f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_TransparentZWrite")) m.SetFloat("_TransparentZWrite", 0f);
            if (m.HasProperty("_EnableFogOnTransparent")) m.SetFloat("_EnableFogOnTransparent", 1f);
            // Фантом рождается РОВНО в текущей позе робота, поэтому при обычном
            // ZTest (LEqual) он целиком прятался за непрозрачной моделью и «не появлялся».
            // Always (8) = «рентген»: бирюзовый призрак читается сквозь робота и стол.
            if (m.HasProperty("_ZTestTransparent")) m.SetFloat("_ZTestTransparent", 8f);

            // Смешивание цвета и альфы (HDRP: color = src·src_a + dst·(1−src_a)).
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", OneBlend);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", OneMinusSrcAlphaBlend);
            if (m.HasProperty("_AlphaSrcBlend")) m.SetFloat("_AlphaSrcBlend", OneBlend);
            if (m.HasProperty("_AlphaDstBlend")) m.SetFloat("_AlphaDstBlend", OneMinusSrcAlphaBlend);
            if (m.HasProperty("_DstBlend2")) m.SetFloat("_DstBlend2", OneMinusSrcAlphaBlend);

            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_BLENDMODE_ALPHA");
            m.DisableKeyword("_BLENDMODE_PREMULTIPLY");
            m.DisableKeyword("_BLENDMODE_ADD");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            Color c = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(alpha));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", c);
            // Мягкое бирюзовое свечение — «фантом» читается даже на тёмном фоне.
            if (m.HasProperty("_EmissiveColor"))
                m.SetColor("_EmissiveColor", new Color(tint.r, tint.g, tint.b, 1f) * 0.45f);
            if (m.HasProperty("_EmissiveIntensity")) m.SetFloat("_EmissiveIntensity", 1f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.55f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        }

        /// <summary>Подсветка/затемнение фантома без пересоздания материала.</summary>
        public static void SetGhostGlow(Material m, Color tint, float alpha, float glow)
        {
            if (m == null) return;
            Color c = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(alpha));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_EmissiveColor"))
                m.SetColor("_EmissiveColor", new Color(tint.r, tint.g, tint.b, 1f) * Mathf.Max(0f, glow));
        }

        /// <summary>«Ядовитый» неоновый материал (для шарика прицела и линий).</summary>
        public static void MakeNeon(Material m, Color color, float emission)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", color * emission);
            if (m.HasProperty("_EmissiveColor"))
            {
                m.SetColor("_EmissiveColor", color * emission);
                m.EnableKeyword("_EMISSION");
            }
        }
    }

    /// <summary>
    /// Менеджер лазеров (два луча): красный — выбор точки на поверхности,
    /// зелёный — подтверждение траектории и фантома.
    /// Лучи берутся из позы камеры и латеральных смещений рук — ровно там же,
    /// где рисованы сами лучи, поэтому «куда смотрю, туда и выбираю».
    /// </summary>
    public class LaserManager : MonoBehaviour
    {
        [Header("Геометрия лучей (как у визуальных лучей)")]
        public float handOffset = 0.35f;
        public float downOffset = 0.15f;
        public float maxDistance = 50f;

        [Header("Выбор")]
        public float tubePickRadius = 0.05f;   // радиус «колбаски» (1/20 юнита)
        public float phantomPickRadius = 0.18f;

        public Ray RedRay { get; private set; }
        public Ray GreenRay { get; private set; }
        public bool RedAimValid { get; private set; }
        public bool GreenAimValid { get; private set; }
        public Vector3 RedHitPoint { get; private set; }
        public Vector3 GreenHitPoint { get; private set; }

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
        }

        /// <summary>Обновить оба луча по текущей позе камеры (вызывается каждый кадр).</summary>
        public void UpdateRays(Vector3 aimPoint, bool aimHit)
        {
            Transform t = cam != null ? cam.transform : transform;
            Vector3 redOrigin = t.position - t.right * handOffset - t.up * downOffset;
            Vector3 greenOrigin = t.position + t.right * handOffset - t.up * downOffset;
            Vector3 dir = aimHit ? (aimPoint - redOrigin).normalized : t.forward;

            RedRay = new Ray(redOrigin, dir);
            RedAimValid = aimHit;
            RedHitPoint = aimPoint;

            Vector3 gdir = aimHit ? (aimPoint - greenOrigin).normalized : t.forward;
            GreenRay = new Ray(greenOrigin, gdir);
            GreenAimValid = aimHit;
            GreenHitPoint = aimPoint;
        }
    }

    /// <summary>
    /// Менеджер фантомов: полупрозрачные БИРЮЗОВЫЕ копии модели.
    ///   * ShowAllAlongPaths — НОВЫЙ АЛГОРИТМ (шаг 2 ТЗ): по ОДНОМУ фантому на КАЖДУЮ
    ///     сгенерированную траекторию; все стартуют одновременно из текущей позы робота
    ///     и едут каждый по своему пути со скоростью phantomMoveSpeed;
    ///   * KeepOnly(index) — шаг 3 ТЗ: остаётся только фантом выбранной траектории
    ///     (остальные копии удаляются), его прогресс СОХРАНЯЕТСЯ: он продолжает движение
    ///     или уже стоит в конечной позе;
    ///   * ShowAlongPath — один фантом по траектории (переключение траектории на шаге 4:
    ///     «фантом перезапускается, либо создаётся заново»);
    ///   * Show — несколько копий по конфигурациям IK (старый задел) с переездом по прямой;
    ///   * остаются видимыми до Esc / подтверждения; выбирать можно и в пути, и после;
    ///   * контейнер «Phantoms» и все копии скрыты из иерархии Unity
    ///     (HideFlags.HideInHierarchy — БЕЗ DontSave: такие объекты уничтожаются
    ///     вместе с PlayMode-сценой и не «протекают» в редактор после остановки Play),
    ///     в дерево проекта KazistovVv не попадают (у копии уничтожаются
    ///     RobotController/RegisteredObject, коллайдеры и Rigidbody);
    ///   * выбор — зелёным лучом, доступен сразу после появления.
    ///
    /// ТЗ «фантомы»: перед созданием новых фантомов выполняется ПОЛНАЯ уборка
    /// (ClearPhantoms: уничтожаются и зарегистрированные копии, и всё, что осталось
    /// в контейнере, и «осиротевшие» объекты Phantom_*/Phantoms в сцене, и материалы-инстансы),
    /// поэтому в сцене живут только актуальные копии (по одной на траекторию).
    /// Копия создаётся сразу выключенной («тихий» Instantiate): её скрипты ни одного кадра
    /// не работают, поэтому копия — чистая геометрия без TCP-прокси и перепарентовки мешей.
    /// Движение ведёт сам менеджер (Update → Tick), ровно один шаг на кадр: фантомы едут
    /// независимо от того, дошёл ли поток этапов до своего Update.
    /// </summary>
    public class PhantomManager : MonoBehaviour
    {
        [Header("Параметры фантомов")]
        public float pickRadius = 0.20f;      // радиус захвата лучом

        [Header("Внешний вид фантома (ЭТАП 1: без визуальной разницы)")]
        [Tooltip("Фантом выглядит РОВНО как реальный робот: те же sharedMaterial и текстуры, " +
                 "никакой прозрачности и отдельного (бирюзового) цвета. Материалы НЕ копируются " +
                 "и НЕ изменяются — префабы, материалы и текстуры сцены не ломаются, " +
                 "утечек материал-инстансов нет.")]
        public bool matchRealAppearance = true;
        [Tooltip("Тени у фантома — как у реального робота (действует при matchRealAppearance). " +
                 "Выключить только если 8 копий слишком дорого стоят по теням.")]
        public bool castShadowsLikeRobot = true;
        [Tooltip("Тонкий контур вокруг фантома, на который наведён зелёный луч — единственное " +
                 "визуальное отличие и только при наведении (материалы самой модели не трогаются).")]
        public bool outlineOnHover = true;

        [Header("Оставлено для совместимости (при matchRealAppearance не используется)")]
        public float ghostAlpha = 0.55f;      // полупрозрачность старого «рентген»-призрака
        public float ghostGlow = 0.45f;       // свечение старого «призрака»
        public Color ghostTint = new Color(0.10f, 0.92f, 0.95f, 1f);  // бирюзовый (старый)
        [Tooltip("Показывать фантом сразу в конечной позе (только для Show — множественные копии IK). " +
                 "Путь по траектории (ShowAlongPath) работает независимо от этого флага.")]
        public bool appearAtTargetPose = false;
        public float moveSpeed = 1f / 30f;    // 1 юнит за 30 секунд (переезд в Show)
        public int pathSamples = 24;          // сэмплов для оценки длины переезда/пути
        public float pulseHz = 1.6f;          // «дыхание» фантома в полёте

        [Header("Движение по траектории (ShowAlongPath)")]
        [Tooltip("РЕЗЕРВНАЯ скорость фантома, юнитов/с — применяется ТОЛЬКО если поток передал " +
                 "скорость ≤ 0. Рабочая скорость фантома = robotMoveSpeed × phantomSpeedMultiplier " +
                 "(Траектория → TrajectoryFlowController), отдельной скорости у фантомов нет.")]
        public float pathSpeed = 0.5f;
        [Tooltip("Минимальное время прохода по траектории, с (короткий путь не «моргает»). " +
                 "Держать маленьким: этот кламп ИСКАЖАЕТ отношение скоростей робот/фантом " +
                 "(фантом обязан быть ровно в multiplier раз быстрее), поэтому при срабатывании " +
                 "в консоль пишется предупреждение.")]
        public float minTravelTime = 0.25f;
        [Tooltip("Максимальное время прохода по траектории, с. Ограничивает только совсем " +
                 "медленные проходы (защита от «фантом застыл»): при 3× от robotMoveSpeed = 1/15 " +
                 "эталонный путь 3.3 юнита занимает ≈16.5 с, поэтому предел должен быть заметно " +
                 "больше. Кламп, если он сработал, пишется в консоль.")]
        public float maxTravelTime = 240f;

        private readonly List<PhantomConfig> configs = new List<PhantomConfig>();
        private readonly List<double[]> startPoses = new List<double[]>();
        private readonly List<double[]> targetPoses = new List<double[]>();
        private readonly List<float> progress = new List<float>();
        private readonly List<float> durations = new List<float>();

        // Путь фантома по подтверждённой траектории (параллельные списки, индекс = индекс фантома).
        private readonly List<double[][]> paths = new List<double[][]>();
        private readonly List<float[]> pathTimes = new List<float[]>();   // нормированные времена 0..1
        private readonly List<int> pathCursor = new List<int>();
        private readonly List<ScaraBaseline> scaraBaselines = new List<ScaraBaseline>();
        private readonly List<Transform[]> copyJoints = new List<Transform[]>();  // суставы копии (кэш)
        private readonly List<Renderer[]> ghostRenderers = new List<Renderer[]>(); // рендереры копии (кэш)
        private readonly List<bool> arrived = new List<bool>();                   // фантом доехал
        private readonly List<double[]> lastPoses = new List<double[]>();         // последняя поза фантома

        /// <summary>Кадр последнего шага движения: Tick зовут и поток, и Update менеджера.</summary>
        private int lastTickFrame = -1;

        /// <summary>Эталонная поза копии SCARA (снята сразу после Instantiate).</summary>
        private class ScaraBaseline
        {
            public Transform j1, j2, z;
            public Quaternion r1, r2;
            public Vector3 pz;
        }

        private RobotController template;
        private PoseValidator validator;
        private Transform root;
        private GameObject rootGo;

        /// <summary>Кадров «доклейки» HideFlags после создания копии (см. ReassertHideFlags).</summary>
        private int hideFlagsReassertFrames;
        /// <summary>Флаги служебных объектов: скрыто из Hierarchy, но НЕ DontSave.</summary>
        public const HideFlags GhostHideFlags = HideFlags.HideInHierarchy;

        public IReadOnlyList<PhantomConfig> Configs => configs;
        public int Count => configs.Count;

        /// <summary>Доля переезда i-го фантома (0..1).</summary>
        public float ProgressOf(int index)
            => index >= 0 && index < progress.Count ? progress[index] : 0f;

        /// <summary>Длительность прохода i-го фантома по своей траектории, с (диагностика/HUD).</summary>
        public float DurationOf(int index)
            => index >= 0 && index < durations.Count ? durations[index] : 0f;

        /// <summary>Длина TCP-пути i-го фантома, м (диагностика/HUD).</summary>
        public float PathLengthOf(int index)
        {
            double[][] p = index >= 0 && index < paths.Count ? paths[index] : null;
            return p == null ? 0f : PathLength(p);
        }

        /// <summary>Дошёл ли i-й фантом до конечной позы.</summary>
        public bool ArrivedOf(int index)
            => index >= 0 && index < arrived.Count && arrived[index];

        /// <summary>
        /// Текущая КОНФИГУРАЦИЯ i-го фантома (последняя применённая поза) — диагностика и HUD:
        /// по ней проверяется, что фантом идёт РОВНО по своей траектории и не «телепортируется»
        /// (см. `validator.TcpAt(PoseOf(i))` — точка инструмента фантома).
        /// Массив отдаётся по ссылке и вызывающим кодом не изменяется.
        /// </summary>
        public double[] PoseOf(int index)
            => index >= 0 && index < lastPoses.Count ? lastPoses[index] : null;

        /// <summary>Какие фантомы ещё едут (для подсказок и проверок).</summary>
        public int MovingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < arrived.Count && i < configs.Count; i++)
                    if (!arrived[i]) n++;
                return n;
            }
        }

        /// <summary>Все фантомы стоят в своих конечных позах.</summary>
        public bool AllArrived
        {
            get
            {
                if (configs.Count == 0) return false;
                for (int i = 0; i < arrived.Count; i++)
                    if (!arrived[i]) return false;
                return true;
            }
        }

        public void Init(RobotController robotTemplate, PoseValidator poseValidator)
        {
            template = robotTemplate;
            validator = poseValidator;
            EnsureRoot();
        }

        /// <summary>
        /// Контейнер фантомов — служебный объект: в иерархии Unity не показывается.
        /// Флаг DontSave НЕ ставится намеренно: объект с DontSave не уничтожается
        /// при выгрузке PlayMode-сцены и остаётся «фантомной» записью в редакторе
        /// (виден в Hierarchy, но не находится поиском по сцене). Фантомы существуют
        /// только в PlayMode, в сохранённую сцену они попасть не могут.
        /// </summary>
        private void EnsureRoot()
        {
            if (root != null) return;
            GameObject go = new GameObject("Phantoms");
            go.hideFlags = GhostHideFlags;
            root = go.transform;
            rootGo = go;
        }

        /// <summary>Уничтожить контейнер фантомов (вызывается при выключении менеджера).</summary>
        private void DestroyRoot()
        {
            Kill(rootGo);
            rootGo = null;
            root = null;
        }

        /// <summary>Удаление служебного объекта: Destroy в PlayMode, DestroyImmediate в редакторе.</summary>
        private static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        /// <summary>
        /// Удаление копии-фантома: сначала объект ВЫКЛЮЧАЕТСЯ (в тот же кадр исчезает
        /// из сцены: не рисуется, не выбирается лучом), затем уничтожается —
        /// Destroy в PlayMode, DestroyImmediate в редакторе. «Висяков» не остаётся.
        /// </summary>
        private static void KillGhost(GameObject g)
        {
            if (g == null) return;
            if (g.activeSelf) g.SetActive(false);
            Kill(g);
        }

        // При выключении/уничтожении менеджера обход сцены не делаем (идёт выгрузка),
        // но свои копии, контейнер и контур наведения уничтожаем обязательно — «висяков» нет.
        private void OnDisable() { outline.Destroy(); DestroyGhosts(false); }
        private void OnDestroy() { outline.Destroy(); DestroyGhosts(false); }

        /// <summary>Шаг движения ведёт сам менеджер: фантом едет, даже если поток этапов
        /// в этом кадре вышел раньше (робот не найден, идёт планирование и т. п.).
        /// Повторный Tick в том же кадре игнорируется, поэтому вызов из потока не удваивает шаг.</summary>
        private void Update() { Tick(Time.deltaTime); }

        /// <summary>
        /// Страховка от «протёкших» контейнеров прошлых сессий (старый код ставил
        /// HideAndDontSave): перед загрузкой сцены убираем такие объекты.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DestroyLeftoverContainers() { PurgeOrphans(); }

        /// <summary>
        /// Уборка «осиротевших» объектов фантомов на верхнем уровне сцены
        /// (контейнер `Phantoms`, копии `Phantom_*`). Нужна как страховка: если копия
        /// почему-то не попала в учёт (создана другим путём, пережила перезагрузку домена),
        /// она всё равно не останется в сцене — при следующем показе фантома сцена чистая.
        /// </summary>
        private static void PurgeOrphans()
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null || t.parent != null) continue;
                string n = t.name;
                if (n != "Phantoms" && !n.StartsWith("Phantom_")) continue;
                KillGhost(t.gameObject);
            }
        }

        /// <summary>
        /// Показать фантомы по списку конфигураций IK (задел на несколько фантомов).
        /// Плавный переезд из стартовой позы в конечную; при appearAtTargetPose = true
        /// копия ставится сразу в конечную позу. Рабочий путь одного фантома по траектории —
        /// ShowAlongPath (ниже).
        /// </summary>
        public void Show(List<IkSolution> solutions, List<int> indices, double[] startQ, float speedUnitsPerSec)
        {
            ClearPhantoms();
            if (template == null || solutions == null || indices == null || startQ == null) return;
            EnsureRoot();

            float speed = Mathf.Max(0.0001f, speedUnitsPerSec > 0f ? speedUnitsPerSec : moveSpeed);

            for (int k = 0; k < indices.Count; k++)
            {
                if (indices[k] < 0 || indices[k] >= solutions.Count) continue;
                IkSolution s = solutions[indices[k]];

                var cfg = new PhantomConfig
                {
                    index = configs.Count,
                    q = s.q,
                    tag = s.tag,
                    fkError = (float)s.fkError,
                    tint = CyanShade(configs.Count, indices.Count)
                };
                cfg.ghost = CreateGhost(cfg, configs.Count);
                configs.Add(cfg);
                int slot = NewSlot();
                copyJoints[slot] = CaptureJoints(cfg.ghost);
                ghostRenderers[slot] = cfg.ghost.GetComponentsInChildren<Renderer>(true);
                scaraBaselines[slot] = CaptureScaraBaseline(cfg.ghost);

                startPoses[slot] = (double[])startQ.Clone();
                if (appearAtTargetPose)
                {
                    // Конечная поза — сразу: фантом рождается в ней и анимации не имеет.
                    targetPoses[slot] = (double[])s.q.Clone();
                    durations[slot] = 0.05f;
                    progress[slot] = 1f;
                    arrived[slot] = true;
                    ApplyPose(slot, s.q);
                }
                else
                {
                    // Плавный переезд из текущей позы робота.
                    targetPoses[slot] = (double[])s.q.Clone();
                    durations[slot] = EstimateDuration(startQ, s.q, speed);
                    progress[slot] = 0f;
                    arrived[slot] = false;
                    ApplyPose(slot, startQ);
                }
                Activate(cfg.ghost);      // копия создана выключенной — включаем уже в позе
            }
        }

        /// <summary>
        /// ФАНТОМЫ ПО КАЖДОЙ ТРАЕКТОРИИ (новый алгоритм, шаг 2 ТЗ): по одному фантому
        /// на каждую сгенерированную траекторию. Все стартуют ОДНОВРЕМЕННО из текущей позы
        /// робота и едут каждый по своему пути с одной и той же скоростью (phantomMoveSpeed).
        /// ПЕРЕД созданием — полная уборка: в сцене живут ровно эти копии.
        /// </summary>
        public bool ShowAllAlongPaths(List<PlannedTrajectory> plans, double[] startQ, float speedUnitsPerSec)
        {
            ClearPhantoms();
            if (template == null || validator == null || !validator.Ready) return false;
            if (plans == null || plans.Count == 0) return false;
            EnsureRoot();

            int made = 0;
            for (int i = 0; i < plans.Count; i++)
                if (AddAlongPath(plans[i], startQ, speedUnitsPerSec, made, plans.Count, true)) made++;

            if (made == 0) { ClearPhantoms(); return false; }

            Debug.Log("[Phantom] фантомов создано: " + made + " (по одному на траекторию) · скорость " +
                      Mathf.Max(0.01f, speedUnitsPerSec).ToString("0.00") + " ю/с · все стартуют из позы робота");
            return true;
        }

        /// <summary>
        /// ОДИН фантом по ВЫБРАННОЙ траектории (используется при ПЕРЕКЛЮЧЕНИИ траектории —
        /// шаг 4 ТЗ: «фантом перезапускается, либо создаётся заново»). Полная уборка +
        /// один новый фантом, который начинает анимацию с текущей позы робота.
        /// </summary>
        public bool ShowAlongPath(PlannedTrajectory plan, double[] startQ, float speedUnitsPerSec)
        {
            ClearPhantoms();
            if (template == null || validator == null || !validator.Ready) return false;
            if (plan == null || plan.Path == null || plan.Path.Length < 2) return false;
            if (startQ == null || startQ.Length < validator.Dof) return false;
            EnsureRoot();

            if (!AddAlongPath(plan, startQ, speedUnitsPerSec, 0, 1, true)) return false;
            Debug.Log("[Phantom] фантом создан (один) · сэмплов " + plan.Path.Length +
                      " · путь " + PathLength(plan.Path).ToString("0.00") + " юнита за " +
                      durations[0].ToString("0.0") + " с (" +
                      (PathLength(plan.Path) / Mathf.Max(0.01f, durations[0])).ToString("0.00") +
                      " ю/с) · старт в позе робота");
            return true;
        }

        /// <summary>
        /// Создать ОДНУ копию-фантом и запустить её по сэмплам пути траектории.
        /// Старт — текущая поза робота (ближайший доворот к началу пути), финиш — конец пути;
        /// время прохода = длина TCP-пути / скорость в границах [minTravelTime, maxTravelTime].
        /// Возвращает false, если траектория/поза непригодны (копия при этом не создаётся).
        /// </summary>
        private bool AddAlongPath(PlannedTrajectory plan, double[] startQ, float speedUnitsPerSec,
                                  int tintIndex, int tintTotal, bool activate)
        {
            if (validator == null || !validator.Ready || template == null) return false;
            if (plan == null || plan.Path == null || plan.Path.Length < 2) return false;
            if (startQ == null || startQ.Length < validator.Dof) return false;

            double[][] path = plan.Path;
            double[] goalQ = plan.GoalQ ?? path[path.Length - 1];

            var cfg = new PhantomConfig
            {
                index = configs.Count,
                q = (double[])goalQ.Clone(),   // финальная поза = конец траектории (её же выберет ЛКМ)
                tag = new IkBranchTag(),
                cost = 0.0,
                sigmaMin = (float)plan.SigmaMin,
                fkError = 0f,                  // IK здесь не решается: поза берётся из траектории
                tint = CyanShade(tintIndex, tintTotal)
            };
            cfg.ghost = CreateGhost(cfg, configs.Count);
            configs.Add(cfg);
            int slot = NewSlot();
            copyJoints[slot] = CaptureJoints(cfg.ghost);          // суставы копии — один раз
            ghostRenderers[slot] = cfg.ghost.GetComponentsInChildren<Renderer>(true);
            scaraBaselines[slot] = CaptureScaraBaseline(cfg.ghost);

            // Старт — ровно текущая поза робота; доворот к началу пути — ближайший
            // (без «полного оборота» на первом же кадре).
            startPoses[slot] = validator.ContinueFrom(path[0], startQ);
            targetPoses[slot] = (double[])goalQ.Clone();
            paths[slot] = path;
            pathTimes[slot] = NormalizedTimes(plan, path.Length);
            pathCursor[slot] = 0;
            durations[slot] = TravelTimeFromLength(PathLength(path), speedUnitsPerSec);
            progress[slot] = 0f;
            arrived[slot] = false;

            ApplyPose(slot, startPoses[slot]);              // старт — ровно поза робота
            if (activate) Activate(cfg.ghost);              // копия создана выключенной
            return true;
        }

        /// <summary>
        /// ОСТАВИТЬ ТОЛЬКО ОДИН ФАНТОМ (шаг 3 ТЗ: «остальные траектории и фантомы
        /// скрываются/удаляются»). Остальные копии удаляются, а у выбранного
        /// СОХРАНЯЕТСЯ прогресс — он продолжает движение (или уже доехал).
        /// </summary>
        public bool KeepOnly(int index)
        {
            if (index < 0 || index >= configs.Count) return false;
            if (configs.Count == 1 && index == 0) return true;

            SlotData keep = TakeSlot(index);
            for (int i = 0; i < configs.Count; i++)
                if (i != index) KillGhost(configs[i].ghost);

            ClearLists();
            PutSlot(keep);
            lastHighlight = -2;
            return true;
        }

        /// <summary>Длина TCP-полилинии пути (м) — оценка пути фантома (поза робота восстанавливается).</summary>
        private float PathLength(double[][] path)
        {
            if (path == null || path.Length < 2 || validator == null || !validator.Ready) return 0f;
            int n = path.Length;
            int stride = Mathf.Max(1, n / Mathf.Clamp(pathSamples, 4, 64));
            float len = 0f;
            Vector3 prev = validator.TcpAt(path[0]);
            for (int i = stride; i < n; i += stride)
            {
                Vector3 p = validator.TcpAt(path[i]);
                len += Vector3.Distance(prev, p);
                prev = p;
            }
            len += Vector3.Distance(prev, validator.TcpAt(path[n - 1]));
            return len;
        }

        /// <summary>Включить копию (после «тихого» Instantiate и установки стартовой позы).</summary>
        private static void Activate(GameObject ghost)
        {
            if (ghost != null && !ghost.activeSelf) ghost.SetActive(true);
        }

        /// <summary>Слот в параллельных списках: все списки всегда одной длины с configs.</summary>
        private int NewSlot()
        {
            startPoses.Add(null);
            targetPoses.Add(null);
            paths.Add(null);
            pathTimes.Add(null);
            pathCursor.Add(0);
            durations.Add(0.05f);
            progress.Add(1f);
            scaraBaselines.Add(null);
            copyJoints.Add(null);
            ghostRenderers.Add(null);
            arrived.Add(false);
            lastPoses.Add(null);
            return configs.Count - 1;
        }

        /// <summary>Все данные одного фантома (нужны, чтобы «оставить только один» без потери прогресса).</summary>
        private class SlotData
        {
            public PhantomConfig cfg;
            public double[] start, target;
            public double[][] path;
            public float[] times;
            public int cursor;
            public float duration, progress;
            public bool arrived;
            public double[] pose;
            public ScaraBaseline scara;
            public Transform[] joints;
            public Renderer[] renderers;
        }

        private SlotData TakeSlot(int i) => new SlotData
        {
            cfg = configs[i],
            start = startPoses[i],
            target = targetPoses[i],
            path = paths[i],
            times = pathTimes[i],
            cursor = pathCursor[i],
            duration = durations[i],
            progress = progress[i],
            arrived = arrived[i],
            pose = lastPoses[i],
            scara = scaraBaselines[i],
            joints = copyJoints[i],
            renderers = ghostRenderers[i]
        };

        private void PutSlot(SlotData d)
        {
            d.cfg.index = configs.Count;
            configs.Add(d.cfg);
            startPoses.Add(d.start);
            targetPoses.Add(d.target);
            paths.Add(d.path);
            pathTimes.Add(d.times);
            pathCursor.Add(d.cursor);
            durations.Add(d.duration);
            progress.Add(d.progress);
            scaraBaselines.Add(d.scara);
            copyJoints.Add(d.joints);
            ghostRenderers.Add(d.renderers);
            arrived.Add(d.arrived);
            lastPoses.Add(d.pose);
        }

        /// <summary>Очистить все параллельные списки (длины всегда совпадают с configs).</summary>
        private void ClearLists()
        {
            configs.Clear();
            startPoses.Clear();
            targetPoses.Clear();
            progress.Clear();
            durations.Clear();
            paths.Clear();
            pathTimes.Clear();
            pathCursor.Clear();
            scaraBaselines.Clear();
            copyJoints.Clear();
            ghostRenderers.Clear();
            arrived.Clear();
            lastPoses.Clear();
        }

        /// <summary>Суставы копии (Axis1..Axis6) для покадрового применения позы; SCARA — null.</summary>
        private Transform[] CaptureJoints(GameObject ghost)
        {
            if (ghost == null || validator == null || !validator.Ready) return null;
            if (validator.Dof == 3) return null;           // SCARA применяется по именам деталей
            Transform[] j = validator.FindCopyJoints(ghost.transform);
            return j != null && j.Length > 0 ? j : null;
        }

        /// <summary>
        /// Время прохода по пути: длина TCP-полилинии / скорость, зажатая в
        /// [minTravelTime, maxTravelTime]. Робот едет 1 юнит за 30 с — фантом быстрее,
        /// но движение остаётся видимым (не мгновенным).
        /// </summary>
        private float TravelTime(double[][] path, float speedUnitsPerSec)
            => TravelTimeFromLength(PathLength(path), speedUnitsPerSec);

        private float TravelTimeFromLength(float len, float speedUnitsPerSec)
        {
            float speed = speedUnitsPerSec > 0f ? speedUnitsPerSec : Mathf.Max(0.01f, pathSpeed);
            float min = Mathf.Max(0.05f, minTravelTime);
            float max = Mathf.Max(min, maxTravelTime);
            float exact = len / Mathf.Max(0.0001f, speed);
            float clamped = Mathf.Clamp(exact, min, max);

            // Кламп ИСКАЖАЕТ главное правило проекта «фантом ровно в N раз быстрее робота»,
            // поэтому о срабатывании сообщаем один раз на создание — молчаливое искажение
            // скорости выглядело бы как «фантомы едут не в 3 раза быстрее».
            if (Mathf.Abs(clamped - exact) > 0.01f)
            {
                Debug.LogWarning("[Phantom] время прохода зажато границами [" + min.ToString("0.##") +
                                 "…" + max.ToString("0.##") + " с]: путь " + len.ToString("0.00") +
                                 " юнита при " + speed.ToString("0.000") + " ю/с требует " +
                                 exact.ToString("0.0") + " с — фантом пойдёт " +
                                 (len / Mathf.Max(0.0001f, clamped)).ToString("0.000") +
                                 " ю/с (отношение к скорости робота отличается от заданного множителя).");
            }
            return clamped;
        }

        /// <summary>
        /// Сколько времени займёт проход пути при заданной скорости (без клампов) —
        /// нужно проверкам, что фантом действительно быстрее робота ровно в multiplier раз.
        /// </summary>
        public float ExpectedTravelTime(float pathLength, float speedUnitsPerSec)
        {
            float speed = speedUnitsPerSec > 0f ? speedUnitsPerSec : Mathf.Max(0.01f, pathSpeed);
            return pathLength / Mathf.Max(0.0001f, speed);
        }

        /// <summary>Нормированные времена траектории (0..1): форма профиля скоростей сохраняется.</summary>
        private static float[] NormalizedTimes(PlannedTrajectory plan, int count)
        {
            var prof = new float[count];
            bool ok = plan.Times != null && plan.Times.Length == count;
            float total = ok ? plan.Times[count - 1] : 0f;
            for (int i = 0; i < count; i++)
            {
                if (ok && total > 1e-4f) prof[i] = Mathf.Clamp01(plan.Times[i] / total);
                else prof[i] = count > 1 ? (float)i / (count - 1) : 1f;
            }
            return prof;
        }

        /// <summary>
        /// Оттенок бирюзового для фантома k из total: cyan → turquoise → aqua. Разные оттенки —
        /// фантомы визуально различимы между собой. При 8 фантомах (ТЗ «8 траекторий») одного
        /// тона мало, поэтому соседние номера различаются ещё и светлотой — иначе 8 бирюзовых
        /// копий читались бы как одно пятно.
        /// </summary>
        public static Color CyanShade(int index, int total)
        {
            float k = total > 1 ? Mathf.Clamp01((float)index / (total - 1)) : 0.5f;
            float hue = Mathf.Lerp(0.455f, 0.560f, k);              // 164°..202° — бирюза/циан
            float sat = Mathf.Lerp(0.95f, 0.60f, k);
            float val = (index % 2 == 0) ? 1.00f : 0.84f;            // чередование светлоты
            return Color.HSVToRGB(Mathf.Repeat(hue, 1f), sat, val);
        }

        /// <summary>Длительность переезда: длина пути TCP / скорость (юнитов в секунду).</summary>
        private float EstimateDuration(double[] from, double[] to, float speed)
        {
            if (validator == null || !validator.Ready) return 0.5f;
            int n = Mathf.Clamp(pathSamples, 4, 64);
            float len = 0f;
            Vector3 prev = validator.TcpAt(from);
            for (int i = 1; i <= n; i++)
            {
                var q = LerpPose(from, to, (float)i / n);
                Vector3 p = validator.TcpAt(q);
                len += Vector3.Distance(prev, p);
                prev = p;
            }
            if (len < 1e-4f) return 0.05f;         // уже в нужной позе — стоим на месте
            return Mathf.Max(0.05f, len / Mathf.Max(0.0001f, speed));
        }

        /// <summary>
        /// Кадровое обновление фантомов. Ровно ОДИН шаг на кадр (Tick зовут и поток, и Update):
        ///   * фантом с ПУТЁМ (ShowAlongPath) идёт по сэмплам подтверждённой траектории
        ///     с её профилем времени — движение видимое и не по прямой; флаг
        ///     appearAtTargetPose на него НЕ влияет (он только для многофантомного задела);
        ///   * остальные (Show) — плавный переезд по кратчайшим доворотам;
        ///   * доехавший фантом больше не двигается: стоит и остаётся видимым,
        ///     зелёный луч наводится на него и в пути, и после остановки.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (Time.frameCount == lastTickFrame) return;    // один шаг движения на кадр
            lastTickFrame = Time.frameCount;

            if (hideFlagsReassertFrames > 0)
            {
                hideFlagsReassertFrames--;
                ReassertHideFlags();
            }

            outline.Tick(deltaTime);     // контур под наведением следует за движущимся фантомом

            bool anyMoving = false;
            for (int i = 0; i < configs.Count; i++)
            {
                GameObject ghost = configs[i].ghost;
                if (ghost == null || i >= progress.Count || i >= arrived.Count) continue;
                if (arrived[i]) continue;                    // доехал — стоит и остаётся видимым

                double[][] path = i < paths.Count ? paths[i] : null;
                bool hasPath = path != null && path.Length >= 2;

                if (!hasPath && appearAtTargetPose)
                {
                    // Мгновенный показ — только для многофантомного задела (Show).
                    progress[i] = 1f;
                    arrived[i] = true;
                    ApplyPose(i, targetPoses[i]);
                    continue;
                }

                anyMoving = true;
                float dur = Mathf.Max(0.05f, durations[i]);
                progress[i] = Mathf.Clamp01(progress[i] + Mathf.Max(0f, deltaTime) / dur);
                ApplyPose(i, PoseAt(i, progress[i]));        // при progress = 1 — точно конечная поза

                if (progress[i] >= 1f)
                {
                    arrived[i] = true;
                    ApplyPose(i, hasPath ? path[path.Length - 1] : targetPoses[i]);
                    Debug.Log("[Phantom] фантом " + (i + 1) + " из " + configs.Count +
                              " доехал до конечной позы и остановился");
                }
            }

            if (!anyMoving) return;
            PulseMovingGhosts(deltaTime);
        }

        /// <summary>
        /// Поза i-го фантома при прогрессе u (0..1): по пути траектории, если он задан,
        /// иначе — интерполяция стартовой и конечной поз. При u = 1 — ровно конечная поза.
        /// </summary>
        private double[] PoseAt(int i, float u)
        {
            double[][] path = i < paths.Count ? paths[i] : null;
            if (path == null || path.Length < 2 || pathTimes[i] == null)
                return LerpPose(startPoses[i], targetPoses[i], u);
            if (u >= 1f) return path[path.Length - 1];

            float[] prof = pathTimes[i];
            int seg = SegmentAt(i, prof, u);
            float t0 = prof[seg], t1 = prof[seg + 1];
            float f = t1 - t0 > 1e-6f ? Mathf.Clamp01((u - t0) / (t1 - t0)) : 0f;
            return LerpPose(path[seg], path[seg + 1], f);
        }

        /// <summary>Индекс участка пути по нормированному времени (курсор: прогресс монотонен).</summary>
        private int SegmentAt(int i, float[] prof, float u)
        {
            int c = Mathf.Clamp(pathCursor[i], 0, prof.Length - 2);
            while (c < prof.Length - 2 && prof[c + 1] < u) c++;
            pathCursor[i] = c;
            return c;
        }

        /// <summary>«Дыхание» контура у летящего фантома — материалы модели НЕ трогаются
        /// (ЭТАП 1: фантом обязан выглядеть как реальный робот, поэтому подсветка живёт
        /// только на отдельном контуре-наведении, а не на материалах копии).</summary>
        private void PulseMovingGhosts(float deltaTime)
        {
            pulseTimer -= deltaTime;
            if (pulseTimer > 0f) return;
            pulseTimer = 0.1f;
            if (!outlineOnHover) return;
            float k = 0.8f + 0.2f * Mathf.Sin(Time.time * Mathf.PI * 2f * Mathf.Max(0.1f, pulseHz));
            if (lastHighlight >= 0) outline.Pulse(k);
        }

        private float pulseTimer;

        /// <summary>Интерполяция позы по кратчайшему довороту каждого сустава.</summary>
        private double[] LerpPose(double[] a, double[] b, float t)
        {
            var q = new double[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                double d = b[i] - a[i];
                if (validator != null && validator.Ready && !validator.IsPrismatic(i))
                    d = Mathf.DeltaAngle((float)a[i], (float)b[i]);
                q[i] = a[i] + d * t;
            }
            return q;
        }

        /// <summary>
        /// Поставить фантом i в конфигурацию q. 6-осевой — углы задаются АБСОЛЮТНО
        /// (AngleAxis(q)·q0), поэтому многократные вызовы не накапливают поворот.
        /// SCARA — через ApplyScaraToCopy: он доворачивает звенья ОТ ТЕКУЩЕГО состояния
        /// копии, поэтому перед каждой позой копия возвращается в эталонную (снятую
        /// с робота при создании). Семантика применения та же, что была при одном
        /// вызове на конечную позу, но без накопления поворота кадр за кадром.
        /// </summary>
        private void ApplyPose(int i, double[] q)
        {
            if (i < 0 || i >= configs.Count || q == null) return;
            GameObject ghost = configs[i].ghost;
            if (ghost == null || validator == null || !validator.Ready) return;
            // Последняя поза фантома — для диагностики («фантом идёт ровно по траектории»).
            // Ссылка, без копирования: массив дальше только читается.
            if (i < lastPoses.Count) lastPoses[i] = q;

            if (validator.Dof == 3)
            {
                ScaraBaseline b = i < scaraBaselines.Count ? scaraBaselines[i] : null;
                if (b != null)
                {
                    if (b.j1 != null) b.j1.localRotation = b.r1;
                    if (b.j2 != null) b.j2.localRotation = b.r2;
                    if (b.z != null) b.z.localPosition = b.pz;
                }
                validator.ApplyScaraToCopy(ghost.transform, q);
                return;
            }

            // Суставы копии ищутся один раз при создании (кэш в copyJoints):
            // в кадре поза ставится сразу в найденные трансформы, без обхода модели.
            Transform[] joints = i < copyJoints.Count ? copyJoints[i] : null;
            if (joints == null || joints.Length == 0)
            {
                joints = validator.FindCopyJoints(ghost.transform);   // страховка: кэш не снялся
                if (i < copyJoints.Count) copyJoints[i] = joints;
            }
            if (joints != null && joints.Length > 0) validator.ApplyToCopy(joints, q);
        }

        /// <summary>
        /// Эталонная поза копии SCARA (J1_3 / J2_4 / z_5) — снимается сразу после
        /// Instantiate, когда копия стоит ровно в позе робота. Для 6-осевого — null.
        /// </summary>
        private static ScaraBaseline CaptureScaraBaseline(GameObject ghost)
        {
            if (ghost == null) return null;
            Transform j1 = FindPart(ghost.transform, "J1_3");
            Transform j2 = FindPart(ghost.transform, "J2_4");
            Transform jz = FindPart(ghost.transform, "z_5");
            if (j1 == null || j2 == null || jz == null) return null;
            return new ScaraBaseline
            {
                j1 = j1, j2 = j2, z = jz,
                r1 = j1.localRotation, r2 = j2.localRotation, pz = jz.localPosition
            };
        }

        /// <summary>Поиск детали копии по фрагменту имени (как в PoseValidator).</summary>
        private static Transform FindPart(Transform root, string part)
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            return null;
        }

        /// <summary>
        /// Копия модели для фантома. Создаётся «тихо»: сразу после Instantiate объект
        /// ВЫКЛЮЧАЕТСЯ, поэтому ни один кадр его скриптов не выполняется — копия не успевает
        /// создать TCP-прокси, перепарентить меши и добавить себе коллайдеры/поведение
        /// (именно из-за этого детали копии раньше «выныривали» в окне Hierarchy).
        /// Компоненты поведения/коллайдеры/Rigidbody у копии уничтожаются, флаги
        /// HideInHierarchy ставятся на копию и всех потомков, материалы делаются
        /// бирюзовыми полупрозрачными («рентген», ZTest Always).
        /// </summary>
        private GameObject CreateGhost(PhantomConfig cfg, int index)
        {
            // Копия создаётся «тихим» Instantiate, но её Awake (RobotController → RobotInventory.Guard)
            // успевает отработать ДО выставления флагов — на время создания проверку инвентаря
            // приостанавливаем, иначе в консоль летит ложное «Роботов в сцене: 3».
            RobotInventory.Suppress = true;
            GameObject ghost;
            try { ghost = Instantiate(template.gameObject); }
            finally { RobotInventory.Suppress = false; }
            ghost.SetActive(false);              // «тихий» Instantiate: скрипты копии не работают
            ghost.name = "Phantom_" + index;
            // HideInHierarchy — копия не видна в дереве иерархии. Без DontSave:
            // объект с DontSave переживает выход из PlayMode и остаётся «фантомной»
            // записью в редакторе (виден в Hierarchy, поиском по сцене не находится).
            ghost.hideFlags = GhostHideFlags;
            ghost.transform.SetParent(root, false);
            ghost.transform.position = template.transform.position;
            ghost.transform.rotation = template.transform.rotation;
            ghost.transform.localScale = Vector3.one;

            // Копия — чистая геометрия: ни поведения, ни регистрации в дереве KazistovVv.
            foreach (MonoBehaviour mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) Destroy(mb);
            foreach (Collider c in ghost.GetComponentsInChildren<Collider>(true))
                if (c != null) Destroy(c);
            foreach (Rigidbody rb in ghost.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) Destroy(rb);
            foreach (Transform t in ghost.GetComponentsInChildren<Transform>(true))
                if (t != null) t.gameObject.hideFlags = GhostHideFlags;
            ghost.hideFlags = GhostHideFlags;   // SetParent мог сбросить флаги
            hideFlagsReassertFrames = 2;        // страховка: флаги доклеиваются пару кадров

            // ЭТАП 1: фантом ВЫГЛЯДИТ КАК РЕАЛЬНЫЙ РОБОТ.
            // Материалы копии НЕ трогаются вообще: ни копий (r.material), ни подмены
            // (r.sharedMaterial = …), ни прозрачности, ни бирюзового тона, ни ZTest Always.
            // Копия Instantiate уже несёт те же sharedMaterial, что и робот, поэтому текстуры
            // и вид совпадают идеально, «пробелов» в текстурах нет (они появлялись ровно
            // из-за полупрозрачного HDRP-прохода с выключенным ZWrite), префабы и материалы
            // сцены не ломаются, а материал-инстансов (утечка на каждый показ) не возникает.
            if (!matchRealAppearance)
            {
                // Совместимость: старый режим «бирюзовый рентген-призрак» (по умолчанию выключен).
                foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true))
                {
                    Material m = r.material;
                    if (m == null) continue;
                    GhostMaterial.MakeGhost(m, cfg.tint, ghostAlpha);
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.allowOcclusionWhenDynamic = false;
                }
            }
            else if (!castShadowsLikeRobot)
            {
                // Только тени — по требованию (габарит робота тот же, вид тот же).
                foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true))
                    if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return ghost;                       // включается вызывающим (Activate) уже в позе
        }

        /// <summary>
        /// Копии больше не «живые модели» (Instantiate «тихий»), поэтому флаги достаточно
        /// доклеить пару кадров — как страховку от любого кода, который мог их сбросить.
        /// </summary>
        private void ReassertHideFlags()
        {
            for (int i = 0; i < configs.Count; i++)
            {
                GameObject g = configs[i].ghost;
                if (g == null) continue;
                g.hideFlags = GhostHideFlags;
                foreach (Transform t in g.GetComponentsInChildren<Transform>(true))
                    if (t != null) t.gameObject.hideFlags = GhostHideFlags;
            }
        }

        /// <summary>
        /// Полная уборка фантомов (Esc, выбор, новая точка красным лазером).
        /// Делает то же, что ClearPhantoms: уничтожаются и зарегистрированные копии,
        /// и всё, что осталось в контейнере, и сам контейнер, и «осиротевшие»
        /// объекты Phantom_*/Phantoms верхнего уровня сцены, и материалы-инстансы.
        /// </summary>
        public void Hide() { DestroyGhosts(true); }

        /// <summary>
        /// Тот же полный сброс, но по смыслу «перед созданием нового фантома»
        /// (ТЗ: если используется пул — очищай его перед созданием нового).
        /// После неё в сцене не остаётся ни одной копии — фантом всегда ОДИН.
        /// </summary>
        public void ClearPhantoms() { DestroyGhosts(true); }

        private void DestroyGhosts(bool sweepScene)
        {
            for (int i = 0; i < configs.Count; i++) KillGhost(configs[i].ghost);

            if (root != null)
            {
                var leftovers = new List<GameObject>();
                foreach (Transform t in root)
                    if (t != null) leftovers.Add(t.gameObject);
                foreach (GameObject g in leftovers) KillGhost(g);
            }
            DestroyRoot();                       // контейнер пересоздастся в EnsureRoot
            if (sweepScene) PurgeOrphans();

            outline.Hide();                      // контур наведения тоже убирается

            ClearLists();
            lastHighlight = -2;
        }

        /// <summary>
        /// Ближайший фантом к лучу. Учитывается и луч (объём модели), и точка прицела
        /// (шарик лазера) — выбор работает и в движении, и после остановки.
        /// </summary>
        public int HoverIndex(Ray ray, Vector3 aimPoint)
        {
            int best = -1;
            float bestDist = pickRadius;
            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i].ghost == null) continue;
                float d = DistanceToGhost(ray, aimPoint, i);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        public int HoverIndex(Ray ray) => HoverIndex(ray, ray.origin + ray.direction * 10f);

        /// <summary>Рендереры i-й копии: из кэша (без GetComponentsInChildren в кадре).</summary>
        private Renderer[] RenderersOf(int i)
        {
            Renderer[] rs = i >= 0 && i < ghostRenderers.Count ? ghostRenderers[i] : null;
            if (rs != null) return rs;
            GameObject g = i >= 0 && i < configs.Count ? configs[i].ghost : null;
            if (g == null) return new Renderer[0];
            rs = g.GetComponentsInChildren<Renderer>(true);     // страховка: кэш не снялся
            if (i < ghostRenderers.Count) ghostRenderers[i] = rs;
            return rs;
        }

        /// <summary>Расстояние «луч ↔ фантом» по объёмам деталей (0 — если шарик внутри модели).</summary>
        private float DistanceToGhost(Ray ray, Vector3 aimPoint, int i)
        {
            Renderer[] rs = RenderersOf(i);
            if (BoundsOf(rs).Contains(aimPoint)) return 0f;

            float best = float.MaxValue;
            foreach (Renderer r in rs)
            {
                if (r == null) continue;
                Bounds b = r.bounds;
                float radius = Mathf.Max(0.03f, b.extents.magnitude * 0.5f);
                float d = DistanceRaySphere(ray, b.center, radius);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Подсветка фантома, на который наведён зелёный луч (ЭТАП 1): вокруг копии включается
        /// ТОНКИЙ КОНТУР (12 прутьев, HDRP/Unlit), а материалы модели не изменяются ни на йоту —
        /// поэтому без наведения фантом неотличим от реального робота.
        /// </summary>
        public void SetHighlight(int index)
        {
            if (index == lastHighlight) return;
            lastHighlight = index;

            if (!outlineOnHover || index < 0 || index >= configs.Count || configs[index].ghost == null)
            {
                outline.Hide();
                return;
            }
            outline.Show(configs[index].ghost.transform);
        }

        private int lastHighlight = -2;

        /// <summary>
        /// Тонкий контур вокруг фантома под наведением. Служебный объект рантайма:
        /// `HideFlags.HideInHierarchy`, без коллайдеров — в CollisionWorld не попадает
        /// (правило проекта). Геометрия создаётся лениво, при первом наведении.
        /// </summary>
        private sealed class PhantomOutline
        {
            private Transform container;
            private Transform[] edges;
            private Material material;
            private Transform target;
            private float timer;
            private Renderer[] renderers;
            private bool built;

            public void Show(Transform value)
            {
                if (!built) Build();
                if (container == null) return;
                target = value;
                renderers = null;
                timer = 0f;
                container.gameObject.SetActive(target != null);
                if (target != null) Update(0f);
            }

            public void Hide()
            {
                target = null;
                renderers = null;
                if (container != null) container.gameObject.SetActive(false);
            }

            public void Pulse(float k)
            {
                if (target == null || material == null) return;
                Color c = new Color(0.35f, 1f, 1f) * Mathf.Clamp(k, 0.4f, 1.6f);
                if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", c);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
                if (material.HasProperty("_Color")) material.SetColor("_Color", c);
                if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", c * 2.4f);
            }

            public void Tick(float dt)
            {
                if (target == null) return;
                timer -= dt;
                if (timer > 0f) return;
                Update(0.1f);
            }

            private void Update(float interval)
            {
                timer = interval;
                if (target == null || edges == null) return;
                if (renderers == null) renderers = target.GetComponentsInChildren<Renderer>(true);

                Bounds b = new Bounds(target.position, Vector3.one * 0.25f);
                bool any = false;
                foreach (Renderer r in renderers)
                {
                    if (r == null || !r.enabled) continue;
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
                if (!any) b = new Bounds(target.position, Vector3.one * 0.3f);

                Vector3 c = b.center;
                Vector3 e = b.extents + Vector3.one * 0.010f;
                const float t = 0.010f;
                Set(0, c + new Vector3(-e.x, 0f, -e.z), new Vector3(t, e.y * 2f, t));
                Set(1, c + new Vector3(e.x, 0f, -e.z), new Vector3(t, e.y * 2f, t));
                Set(2, c + new Vector3(-e.x, 0f, e.z), new Vector3(t, e.y * 2f, t));
                Set(3, c + new Vector3(e.x, 0f, e.z), new Vector3(t, e.y * 2f, t));
                Set(4, c + new Vector3(0f, -e.y, -e.z), new Vector3(e.x * 2f, t, t));
                Set(5, c + new Vector3(0f, -e.y, e.z), new Vector3(e.x * 2f, t, t));
                Set(6, c + new Vector3(0f, e.y, -e.z), new Vector3(e.x * 2f, t, t));
                Set(7, c + new Vector3(0f, e.y, e.z), new Vector3(e.x * 2f, t, t));
                Set(8, c + new Vector3(-e.x, -e.y, 0f), new Vector3(t, t, e.z * 2f));
                Set(9, c + new Vector3(e.x, -e.y, 0f), new Vector3(t, t, e.z * 2f));
                Set(10, c + new Vector3(-e.x, e.y, 0f), new Vector3(t, t, e.z * 2f));
                Set(11, c + new Vector3(e.x, e.y, 0f), new Vector3(t, t, e.z * 2f));
            }

            private void Set(int i, Vector3 pos, Vector3 scale)
            {
                if (edges == null || i >= edges.Length || edges[i] == null) return;
                edges[i].position = pos;
                edges[i].rotation = Quaternion.identity;
                edges[i].localScale = scale;
            }

            private void Build()
            {
                built = true;
                GameObject rootGo = new GameObject("PhantomOutline");
                rootGo.hideFlags = GhostHideFlags;
                container = rootGo.transform;

                // Материал контура — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP).
                material = KazistovVvFeatures.KvRenderPipelineShaders.CreateUnlit("Материал_контура", Color.white);
                if (material != null)
                {
                    material.hideFlags = GhostHideFlags;
                    Pulse(1f);
                }

                edges = new Transform[12];
                for (int i = 0; i < edges.Length; i++)
                {
                    GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.name = "Edge" + i;
                    bar.hideFlags = GhostHideFlags;
                    Collider col = bar.GetComponent<Collider>();
                    if (col != null)
                    {
                        col.enabled = false;          // до Destroy — чтобы не попал в луч/мир
                        Object.Destroy(col);
                    }
                    bar.transform.SetParent(container, false);
                    Renderer r = bar.GetComponent<Renderer>();
                    if (r != null)
                    {
                        if (material != null) r.sharedMaterial = material;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                    }
                    edges[i] = bar.transform;
                }
                container.gameObject.SetActive(false);
            }

            public void Destroy()
            {
                if (container != null) Object.Destroy(container.gameObject);
                if (material != null) Object.Destroy(material);
                container = null;
                edges = null;
                material = null;
                built = false;
            }
        }

        private readonly PhantomOutline outline = new PhantomOutline();

        private static Bounds BoundsOf(Renderer[] rs, Vector3 fallback = default)
        {
            if (rs == null || rs.Length == 0) return new Bounds(fallback, Vector3.one);
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in rs)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? b : new Bounds(fallback, Vector3.one);
        }

        private static float DistanceRaySphere(Ray ray, Vector3 center, float radius)
        {
            Vector3 m = center - ray.origin;
            float t = Vector3.Dot(m, ray.direction);
            if (t < 0f) t = 0f;
            return Vector3.Distance(center, ray.origin + ray.direction * t) - radius;
        }
    }
}
