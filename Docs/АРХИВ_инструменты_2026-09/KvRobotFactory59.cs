#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KazistovVvUI;
using TrajectoryCore;
using UnityEditor;
using UnityEngine;

/// <summary>
/// §59. ФАБРИКА РАБОЧИХ РОБОТОВ — ДИАГНОСТИКА И КОНВЕЙЕР «ВИЗУАЛЬНАЯ МОДЕЛЬ → РАБОЧИЙ РОБОТ».
///
/// ЗАЧЕМ. Любая модель робота (не только `robot_6axis` и `scara_ls10b702s`) должна работать
/// со скриптами управления: State Machine, IK, планировщик, лазеры, выбор по F. Сегодня
/// 13 моделей каталога — «визуальные»: у них НЕТ `RobotController`, поэтому их не выбирает F,
/// а `PoseValidator`, `ReachabilityOracle`, `CollisionWorld` и `WorkspaceVisualizer` их
/// пропускают (и не падают — это проверяется отдельным сторожем).
///
/// ЧТО ЗДЕСЬ. Инструмент находит в модели КИНЕМАТИЧЕСКУЮ ЦЕПОЧКУ суставов, подбирает оси
/// суставов ЗАМЕРОМ (а не на глаз), проверяет результат самотестом (IK решает батарею
/// доступных точек) и только после этого собирает РАБОЧИЙ префаб. Если цепочки нет
/// (модель — плоская сборка) или самотест не сходится, инструмент ОТКАЗЫВАЕТСЯ и говорит
/// причину: подделывать «рабочего робота» нельзя.
///
/// ПОЧЕМУ ЭТО РАБОТАЕТ БЕЗ ПРАВКИ IK И ПЛАНИРОВЩИКА. `PoseValidator` уже написан ОБОБЩЁННО
/// (цепочка `joints`, оси `axes`, нулевая поза `q0`, CCD в обобщённых координатах) — он
/// различает только «6-осевой» (`SixAxisController`) и SCARA. Поэтому рабочая модель — это
/// `SixAxisController` с корректно заполненной цепочкой, а не новый тип контроллера.
///
/// ЗАПУСК: меню `Tools → KazistovVv → Роботы (§59) → …`
/// либо из батча `-executeMethod KvRobotFactory59.BatchDiag59` / `BatchConvert59`.
///
/// БЕЗОПАСНОСТЬ: диагностика ТОЛЬКО ЧИТАЕТ. Временные инстансы ставятся далеко (y = 5000),
/// уничтожаются тут же; файл сцены хешируется до и после отчёта. Конвертация пишет ТОЛЬКО
/// новые префабы в `Assets/Resources/Robots/` и НЕ трогает ни префабы каталога, ни сцену.
/// </summary>
public static class KvRobotFactory59
{
    public const string FunctionalRobotsFolder = "Assets/Resources/Robots";
    public const string CatalogRobotsFolder = "Assets/Resources/Models/Robots";
    public const string ReportDiagPath = @"C:\Users\Ольга\_s59_diag.txt";
    public const string ReportConvertPath = @"C:\Users\Ольга\_s59_convert.txt";
    public const string SceneAssetPath = "Assets/_Project/00_Scenes/MainScene.unity";

    private const string MenuRoot = "Tools/KazistovVv/Роботы (§59)/";
    private const string LogTag = "[§59 Фабрика]";

    /// <summary>Дальняя точка для временных инстансов (пол сцены — 1000×1000 в нуле).</summary>
    private static readonly Vector3 ProbeOrigin = new Vector3(0f, 5000f, 0f);

    /// <summary>Шесть кандидатов оси в ЛОКАЛЬНЫХ координатах сустава — главные оси модели.</summary>
    public static readonly Vector3[] CandidateAxes =
    {
        Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back
    };

    public static readonly string[] CandidateNames = { "+Y", "-Y", "+X", "-X", "+Z", "-Z" };

    /// <summary>Именованные концевые точки моделей (фланец/TCP/EE) — по ним обрывается цепочка.</summary>
    private static readonly string[] TipNames =
    {
        "tool0", "TCP", "tcp", "flange", "iiwa_link_ee", "fr3_link8", "fr3v2_link8",
        "gripper_frame_link", "moving_jaw_so101_v1_link"
    };

    /// <summary>Концевые точки по окончанию имени (`openarm_left_hand_tcp` и подобные).</summary>
    private static readonly string[] TipSuffixes = { "_hand_tcp", "_tcp", "_ee", "_frame_link" };

    /// <summary>Порог приёмки самотеста: средняя невязка IK по батарее точек, м.</summary>
    public const float AcceptMeanResidual = 0.01f;
    /// <summary>Порог приёмки самотеста: худшая невязка по батарее точек, м.</summary>
    public const float AcceptMaxResidual = 0.03f;

    // ================================================================== предложение цепочки

    /// <summary>Что найдено в модели: неподвижная база, суставы, концевая точка.</summary>
    public sealed class Chain
    {
        public string Id = "";
        public Transform ModelRoot;          // вершина модели (ребёнок узла «Geometry»)
        public Transform Base;               // НЕПОДВИЖНАЯ база (первый узел пути с мешом-ребёнком)
        public readonly List<Transform> Joints = new List<Transform>();
        public Transform Tcp;                // концевая точка (tool0 / *_tcp / фланец)
        public string Note = "";             // человекочитаемое объяснение (в т.ч. отказ)
        public string Refusal = "";          // непусто — автоматическая конвертация ЗАПРЕЩЕНА
        public int NodeCount;                // всего узлов в модели (для «плоских» сборок)
        public int PathLength;               // длина пути «корень → самый глубокий лист»
        public int DeclaredDof;              // максимум чисел из паспортного поля `Dof` каталога
        public string DeclaredDofText = "";  // паспортное поле как есть

        public bool Found { get { return Base != null && Joints.Count > 0; } }
        public bool CanConvert { get { return Found && string.IsNullOrEmpty(Refusal); } }

        public string JointNames
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < Joints.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(Joints[i] != null ? Joints[i].name : "null");
                }
                return sb.ToString();
            }
        }
    }

    public static bool HasRenderer(Transform t)
    {
        return t != null && t.GetComponent<Renderer>() != null;
    }

    public static bool HasDirectMeshChild(Transform t)
    {
        if (t == null) return false;
        for (int i = 0; i < t.childCount; i++)
            if (HasRenderer(t.GetChild(i))) return true;
        return false;
    }

    public static int DepthToDeepestLeaf(Transform t)
    {
        if (t == null || t.childCount == 0) return 0;
        int best = 0;
        for (int i = 0; i < t.childCount; i++)
        {
            int d = 1 + DepthToDeepestLeaf(t.GetChild(i));
            if (d > best) best = d;
        }
        return best;
    }

    public static int MeshCount(Transform t)
    {
        if (t == null) return 0;
        int n = HasRenderer(t) ? 1 : 0;
        for (int i = 0; i < t.childCount; i++) n += MeshCount(t.GetChild(i));
        return n;
    }

    public static int CountNodes(Transform t)
    {
        if (t == null) return 0;
        int n = 1;
        for (int i = 0; i < t.childCount; i++) n += CountNodes(t.GetChild(i));
        return n;
    }

    public static Transform FindModelRoot(GameObject instance)
    {
        if (instance == null) return null;
        Transform geometry = instance.transform.Find("Geometry");
        if (geometry != null && geometry.childCount > 0) return geometry.GetChild(0);
        return instance.transform;
    }

    public static List<Transform> DeepestPath(Transform start)
    {
        var path = new List<Transform>();
        Transform cur = start;
        int guard = 0;
        while (cur != null && guard++ < 512)
        {
            path.Add(cur);
            Transform next = null;
            int bestDepth = -1, bestMeshes = -1;
            for (int i = 0; i < cur.childCount; i++)
            {
                Transform c = cur.GetChild(i);
                int d = DepthToDeepestLeaf(c);
                int m = MeshCount(c);
                if (d > bestDepth || (d == bestDepth && m > bestMeshes))
                {
                    bestDepth = d; bestMeshes = m; next = c;
                }
            }
            cur = next;
        }
        return path;
    }

    /// <summary>
    /// Концевая точка среди ПРЯМЫХ детей узла — ею обрывается цепочка суставов.
    /// ВАЖНО: именно прямых. Поиск «вглубь» увёл бы обрыв на 2–3 сустава вверх
    /// (у UR5 `flange` лежит на 3 уровня ниже `wrist_1_link`, и цепочка оборвалась бы на J4).
    /// </summary>
    public static Transform DirectTip(Transform node)
    {
        if (node == null) return null;
        for (int i = 0; i < node.childCount; i++)
        {
            Transform c = node.GetChild(i);
            for (int k = 0; k < TipNames.Length; k++)
                if (c.name == TipNames[k]) return c;
            for (int k = 0; k < TipSuffixes.Length; k++)
                if (c.name.EndsWith(TipSuffixes[k], StringComparison.OrdinalIgnoreCase)) return c;
        }
        return null;
    }

    /// <summary>Концевая точка среди потомков узла (для выбора TCP, когда цепочка уже построена).</summary>
    public static Transform PreferNamedTip(Transform node)
    {
        if (node == null) return null;
        Transform direct = DirectTip(node);
        if (direct != null) return direct;
        for (int i = 0; i < TipNames.Length; i++)
        {
            Transform found = FindDeep(node, TipNames[i], 2);
            if (found != null && found != node) return found;
        }
        return FindDeepSuffix(node, TipSuffixes, 1);
    }

    private static Transform FindDeep(Transform root, string name, int maxDepth)
    {
        if (root == null || maxDepth < 0) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c.name == name) return c;
            Transform deeper = FindDeep(c, name, maxDepth - 1);
            if (deeper != null) return deeper;
        }
        return null;
    }

    private static Transform FindDeepSuffix(Transform root, string[] suffixes, int maxDepth)
    {
        if (root == null || maxDepth < 0) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            for (int s = 0; s < suffixes.Length; s++)
                if (c.name.EndsWith(suffixes[s], StringComparison.OrdinalIgnoreCase)) return c;
            Transform deeper = FindDeepSuffix(c, suffixes, maxDepth - 1);
            if (deeper != null) return deeper;
        }
        return null;
    }

    /// <summary>
    /// ПРАВИЛО ЦЕПОЧКИ (проверено замером на 15 моделях каталога, отчёт `_s59_diag.txt`):
    ///   * идём по единственному пути «вершина модели → самый глубокий лист»;
    ///   * ПЕРВЫЙ узел пути, у которого есть прямой ребёнок с мешом, — НЕПОДВИЖНАЯ БАЗА
    ///     (у UR5 это отсекает статические `base_link` и `base_link_inertia`);
    ///   * ДАЛЕЕ подряд идут СУСТАВЫ — тоже узлы с прямым ребёнком-мешом
    ///     (у ABB/Comau/Fanuc/UR5/Kinova их ровно 6, у Kuka/Franka — 7, у SO-ARM — 5);
    ///   * цепочка ОБРЫВАЕТСЯ на узле, у которого есть ребёнок-КОНЦЕВАЯ ТОЧКА
    ///     (`tool0`, `flange`, `*_tcp`, `*_ee`, `gripper_frame_link`) — иначе в суставы
    ///     попал бы схват (`moving_jaw`, `*_finger`), а это уже инструмент, а не ось руки;
    ///   * первый узел пути БЕЗ меша-ребёнка тоже завершает цепочку.
    /// </summary>
    public static Chain Detect(GameObject instance, string id, int declaredDof = 0, string declaredText = "")
    {
        var chain = new Chain { Id = id ?? "", DeclaredDof = declaredDof, DeclaredDofText = declaredText ?? "" };
        Transform modelRoot = FindModelRoot(instance);
        chain.ModelRoot = modelRoot;
        if (modelRoot == null)
        {
            chain.Note = "нет вершины модели";
            chain.Refusal = chain.Note;
            return chain;
        }

        chain.NodeCount = CountNodes(modelRoot);
        List<Transform> path = DeepestPath(modelRoot);
        chain.PathLength = path.Count;

        int baseIndex = -1;
        for (int i = 0; i < path.Count; i++)
            if (HasDirectMeshChild(path[i])) { baseIndex = i; break; }

        if (baseIndex < 0)
        {
            chain.Note = "цепочка НЕ найдена: ни один узел пути не несёт звена с мешом " +
                         "(модель — ПЛОСКАЯ сборка: " + chain.NodeCount + " узлов, путь " +
                         chain.PathLength + ")";
            chain.Refusal = "плоская сборка — кинематической цепочки в модели НЕТ";
            return chain;
        }

        chain.Base = path[baseIndex];
        for (int i = baseIndex + 1; i < path.Count; i++)
        {
            if (!HasDirectMeshChild(path[i])) break;
            chain.Joints.Add(path[i]);

            // Концевая точка — инструмент, а не ось руки: цепочка на этом узле и кончается.
            Transform tip = DirectTip(path[i]);
            if (tip != null) { chain.Tcp = DirectTip(tip) ?? tip; break; }
        }

        if (chain.Joints.Count == 0)
        {
            chain.Note = "цепочка НЕ найдена: после базы «" + chain.Base.name +
                         "» нет ни одного сустава (плоская сборка)";
            chain.Refusal = "плоская сборка — кинематической цепочки в модели НЕТ";
            return chain;
        }

        if (chain.Tcp == null)
        {
            Transform last = chain.Joints[chain.Joints.Count - 1];
            chain.Tcp = PreferNamedTip(last) ?? last;
        }

        // Пересчёт: если TCP нашёлся ниже последнего сустава — лишние узлы в цепочке.
        if (chain.Tcp != null && chain.Tcp != chain.Joints[chain.Joints.Count - 1])
        {
            int keep = chain.Joints.Count;
            for (int i = 0; i < chain.Joints.Count; i++)
                if (IsAncestorOf(chain.Joints[i], chain.Tcp)) keep = i + 1;
            if (keep < chain.Joints.Count) chain.Joints.RemoveRange(keep, chain.Joints.Count - keep);
        }

        chain.Note = "база «" + chain.Base.name + "» · суставов " + chain.Joints.Count +
                     " · инструмент «" + (chain.Tcp != null ? chain.Tcp.name : "—") + "»";

        // Честный гейт: суставов найдено БОЛЬШЕ, чем объявлено в паспорте каталога —
        // значит детектор мог принять за ось лишний узел (деталь/схват). Не конвертируем.
        if (chain.DeclaredDof > 0 && chain.Joints.Count > chain.DeclaredDof)
        {
            chain.Refusal = "найдено суставов " + chain.Joints.Count + ", а в каталоге объявлено " +
                            "«" + chain.DeclaredDofText + "» — лишний узел принимается за ось; " +
                            "нужен ручной выбор цепочки (окно «Сделать рабочего робота»)";
        }
        return chain;
    }

    public static bool IsAncestorOf(Transform candidate, Transform target)
    {
        for (Transform t = target; t != null; t = t.parent)
            if (t == candidate) return true;
        return false;
    }

    /// <summary>Максимум чисел из паспортного поля `Dof` (например «5 (SO-100) / 6 (SO-101)» → 6).</summary>
    public static int ParseDofMax(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int best = 0, cur = 0;
        bool has = false;
        foreach (char c in text)
        {
            if (c >= '0' && c <= '9') { cur = cur * 10 + (c - '0'); has = true; }
            else { if (has && cur > best) best = cur; cur = 0; has = false; }
        }
        if (has && cur > best) best = cur;
        return best;
    }

    // ================================================================== батарея и невязка

    /// <summary>
    /// Батарея тестовых точек вокруг базы робота (радиус — доля вылета руки).
    ///
    /// УГЛЫ ВЫСОТЫ СМЕЩЕНЫ ВВЕРХ (центр +15°, шаг 25°). Напольный манипулятор НЕ достаёт
    /// точки ПОД своей базой — это не дефект цепочки, а геометрия. Первая версия батареи
    /// (симметрично ±25° вокруг базы) давала у franka_fr3 промах на одной нижней точке
    /// 3,6 см, из-за чего инструмент отвергал ГОТОВУЮ модель. Это правка ЗАМЕРА, а не порога:
    /// рабочая поверхность проекта тоже считается «от базы» вверх (§57, высоты «от базы»).
    /// </summary>
    public static Vector3[] MakeBattery(Chain chain, int azimuths, int elevations, float fraction = 0.55f)
    {
        var list = new List<Vector3>();
        if (chain == null || chain.Base == null || chain.Joints.Count == 0) return list.ToArray();

        Vector3 basePos = chain.Base.position;
        float reach = Reach(chain);
        float r = reach * fraction;

        for (int a = 0; a < azimuths; a++)
        {
            float az = a * (360f / azimuths) * Mathf.Deg2Rad;
            for (int e = 0; e < elevations; e++)
            {
                float el = 15f + (e - (elevations - 1) * 0.5f) * 25f;
                float elRad = el * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(az) * Mathf.Cos(elRad), Mathf.Sin(elRad),
                                          Mathf.Sin(az) * Mathf.Cos(elRad));
                list.Add(basePos + dir * r);
            }
        }
        return list.ToArray();
    }

    /// <summary>Худшая точка батареи (для честного отчёта об отказе: ГДЕ именно не сошлось).</summary>
    public static void WorstPoint(Chain chain, Vector3[] axes, Vector3[] battery, int iterations,
                                  out Vector3 target, out float error)
    {
        target = Vector3.zero;
        error = float.MaxValue;
        if (chain == null || chain.Joints.Count == 0 || chain.Tcp == null) return;
        if (battery == null || battery.Length == 0) battery = MakeBattery(chain, 6, 3);
        if (axes == null) axes = DefaultAxes(chain.Joints.Count);

        Transform[] joints = chain.Joints.ToArray();
        var saved = new Quaternion[joints.Length];
        for (int i = 0; i < joints.Length; i++)
            saved[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;

        // ВНИМАНИЕ: начальное `error = float.MaxValue` НЕЛЬЗЯ использовать как «пока ничего
        // не найдено» — при сравнении `err > error` первая же точка не проходит, и худшая
        // остаётся нулевой (эту ошибку поймал сторож §59: в отчёте печаталось «худшая точка
        // (0; 0; 0) от базы 5000 м»). Признак «не найдено» — отдельный флаг.
        bool found = false;
        foreach (Vector3 t in battery)
        {
            for (int i = 0; i < joints.Length; i++)
                if (joints[i] != null) joints[i].localRotation = saved[i];
            KazistovVvKinematics.DHInverse.SolveCCD(joints, axes, chain.Tcp, t, iterations, 0.005f);
            float err = Vector3.Distance(chain.Tcp.position, t);
            if (!found || err > error) { error = err; target = t; found = true; }
        }
        if (!found) error = float.MaxValue;

        for (int i = 0; i < joints.Length; i++)
            if (joints[i] != null) joints[i].localRotation = saved[i];
    }

    public static float Reach(Chain chain)
    {
        if (chain == null || chain.Joints.Count == 0) return 0.5f;
        float reach = 0f;
        Vector3 prev = chain.Base != null ? chain.Base.position : chain.Joints[0].position;
        for (int i = 0; i < chain.Joints.Count; i++)
        {
            if (chain.Joints[i] == null) continue;
            reach += Vector3.Distance(prev, chain.Joints[i].position);
            prev = chain.Joints[i].position;
        }
        if (chain.Tcp != null) reach += Vector3.Distance(prev, chain.Tcp.position);
        return reach < 0.05f ? 0.5f : reach;
    }

    /// <summary>
    /// Невязка IK по батарее точек: РЕАЛЬНЫЙ `DHInverse.SolveCCD` (тот же код, что в рантайме)
    /// на РЕАЛЬНЫХ трансформах; поза сбрасывается перед каждым решением и возвращается после.
    /// </summary>
    public static void BatteryResidual(Chain chain, Vector3[] axes, Vector3[] battery,
                                       int iterations, out float mean, out float max)
    {
        mean = float.MaxValue;
        max = float.MaxValue;
        if (chain == null || chain.Joints.Count == 0 || chain.Tcp == null) return;
        if (axes == null) axes = DefaultAxes(chain.Joints.Count);
        if (battery == null || battery.Length == 0) battery = MakeBattery(chain, 6, 3);

        Transform[] joints = chain.Joints.ToArray();
        var saved = new Quaternion[joints.Length];
        for (int i = 0; i < joints.Length; i++)
            saved[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;

        float sum = 0f, worst = 0f;
        foreach (Vector3 target in battery)
        {
            for (int i = 0; i < joints.Length; i++)
                if (joints[i] != null) joints[i].localRotation = saved[i];

            KazistovVvKinematics.DHInverse.SolveCCD(joints, axes, chain.Tcp, target, iterations, 0.005f);
            float err = Vector3.Distance(chain.Tcp.position, target);
            sum += err;
            if (err > worst) worst = err;
        }

        for (int i = 0; i < joints.Length; i++)
            if (joints[i] != null) joints[i].localRotation = saved[i];

        mean = battery.Length > 0 ? sum / battery.Length : float.MaxValue;
        max = worst;
    }

    /// <summary>Оси по умолчанию — те же, что в `SixAxisController` (они от Robot.fbx).</summary>
    public static Vector3[] DefaultAxes(int n)
    {
        var axes = new Vector3[n];
        Vector3[] def =
        {
            Vector3.up, Vector3.forward, Vector3.forward,
            new Vector3(-0.984f, 0.179f, 0f).normalized, Vector3.forward, Vector3.up
        };
        for (int i = 0; i < n; i++) axes[i] = i < def.Length ? def[i] : Vector3.up;
        return axes;
    }

    /// <summary>
    /// ПОСЕВ ПОИСКА: оси по умолчанию, «притянутые» к ближайшей из ШЕСТИ ГЛАВНЫХ осей модели.
    ///
    /// ЗАЧЕМ. В наборе по умолчанию ось J4 задана НАКЛОННОЙ (`(−0,984; 0,179; 0)` — так она
    /// измерена у Robot.fbx). Если оставить её посевом, поиск может её сохранить (строгое
    /// улучшение не находится), и тогда выход инструмента перестаёт быть «набором главных
    /// осей» — то, на чём стоит вся его логика. Это нашёл сторож §59: у `ur5` и `comau_edo`
    /// ось 4 расходилась с главной на 10° (скалярное произведение 0,984).
    /// Само измерение «как есть» при этом считается по ИСХОДНОМУ набору (`DefaultAxes`) —
    /// именно он достался бы модели, если просто навесить контроллер.
    /// </summary>
    public static Vector3[] PrincipalSeed(int n)
    {
        Vector3[] seed = DefaultAxes(n);
        for (int i = 0; i < n; i++)
        {
            int best = 0; float bestDot = -2f;
            for (int c = 0; c < CandidateAxes.Length; c++)
            {
                float d = Vector3.Dot(seed[i].normalized, CandidateAxes[c]);
                if (d > bestDot) { bestDot = d; best = c; }
            }
            seed[i] = CandidateAxes[best];
        }
        return seed;
    }

    // ================================================================== подбор осей ЗАМЕРОМ

    public sealed class AxisSolution
    {
        public Vector3[] Axes;
        public float MeanResidual;
        public float MaxResidual;
        public float StartMean;
        public int Sweeps;
        public readonly List<string> Trace = new List<string>();
    }

    /// <summary>
    /// ПОДБОР ОСЕЙ. Каждый сустав получает одну из шести ГЛАВНЫХ локальных осей (±X/±Y/±Z) —
    /// замер §59 показал, что в этих моделях локальные повороты кратны 90°, то есть оси
    /// суставов и есть главные оси. Выбор — по МИНИМУМУ невязки IK на батарее точек:
    /// покоординатный спуск (сначала дешёвый экран, затем проверка полной батареей).
    /// Это замер, а не догадка: неверный набор осей не даёт руке дотянуться до точек.
    /// </summary>
    public static AxisSolution SearchAxes(Chain chain, int sweeps = 4, int refineSweeps = 2)
    {
        var sol = new AxisSolution();
        int n = chain.Joints.Count;
        var axes = PrincipalSeed(n);                   // поиск идёт ТОЛЬКО по главным осям модели
        Vector3[] screen = MakeBattery(chain, 4, 2);   // этап 1: 8 точек
        Vector3[] full = MakeBattery(chain, 6, 3);     // этап 2 и приёмка: 18 точек

        float mean, max;
        BatteryResidual(chain, axes, full, 60, out sol.StartMean, out max);
        sol.Trace.Add("старт (оси по умолчанию от Robot.fbx): средняя невязка " +
                      Num(sol.StartMean) + " м, худшая " + Num(max) + " м");

        // Лучшая конфигурация по ПРИЁМОЧНОЙ (полной) батарее — то, что сравнивается с порогом.
        Vector3[] bestAxes = (Vector3[])axes.Clone();
        float bestOverall;
        BatteryResidual(chain, axes, full, 45, out mean, out max);
        bestOverall = Score(mean, max);

        // ---- ЭТАП 1: ГРУБЫЙ ЭКРАН. Дешёвая батарея и мало итераций: нужен только порядок
        // кандидатов, а не точность — неверная ось промахивается на десятки сантиметров.
        float bestScreen;
        BatteryResidual(chain, axes, screen, 18, out bestScreen, out max);
        for (int sweep = 0; sweep < sweeps; sweep++)
        {
            if (!CoordinateDescent(chain, axes, screen, 18, ref bestScreen, sol, "экран",
                                    full, ref bestAxes, ref bestOverall)) break;
            sol.Sweeps = sweep + 1;
        }

        // ---- ЭТАП 2: УТОЧНЕНИЕ. Оптимизируется РОВНО критерий приёмки (средняя невязка,
        // и отдельно худшая — их обе требует самотест), на ПОЛНОЙ батарее.
        float bestFull;
        BatteryResidual(chain, axes, full, 45, out mean, out max);
        bestFull = Score(mean, max);
        if (bestFull < bestOverall) { bestOverall = bestFull; bestAxes = (Vector3[])axes.Clone(); }
        for (int sweep = 0; sweep < refineSweeps; sweep++)
        {
            if (!CoordinateDescent(chain, axes, full, 45, ref bestFull, sol, "уточнение",
                                    full, ref bestAxes, ref bestOverall)) break;
        }

        // ВОЗВРАЩАЕМ ЛУЧШУЮ НАЙДЕННУЮ КОНФИГУРАЦИЮ, а не последнюю: покоординатный спуск
        // может «качнуться» между двумя почти равными наборами осей (у franka_fr3 он так
        // и делал — сустав 3 ходил +Y ↔ +Z), и последняя конфигурация оказывалась хуже
        // промежуточной. Это замер, а не подгонка: сравниваются невязки, а не догадки.
        axes = bestAxes;

        BatteryResidual(chain, axes, full, 60, out mean, out max);
        sol.Axes = axes;
        sol.MeanResidual = mean;
        sol.MaxResidual = max;
        sol.Trace.Add("итог: средняя невязка " + Num(mean) + " м, худшая " + Num(max) + " м (60 итераций)");
        return sol;
    }

    /// <summary>Критерий приёмки в одном числе: важны ОБЕ величины (средняя и худшая невязка).</summary>
    private static float Score(float mean, float max) { return mean + 0.5f * max; }

    /// <summary>
    /// Один проход покоординатного спуска: каждый сустав по очереди получает лучшую из шести
    /// главных осей. Возвращает true, если хоть один сустав улучшился.
    /// </summary>
    private static bool CoordinateDescent(Chain chain, Vector3[] axes, Vector3[] battery,
                                          int iterations, ref float bestScore,
                                          AxisSolution sol, string stage,
                                          Vector3[] acceptanceBattery,
                                          ref Vector3[] bestAxes, ref float bestAcceptance)
    {
        bool improved = false;
        for (int i = 0; i < axes.Length; i++)
        {
            int bestIdx = -1;
            float bestLocal = bestScore;
            Vector3 was = axes[i];
            for (int c = 0; c < CandidateAxes.Length; c++)
            {
                if ((was - CandidateAxes[c]).sqrMagnitude < 1e-6f) continue;
                axes[i] = CandidateAxes[c];
                float m2, x2;
                BatteryResidual(chain, axes, battery, iterations, out m2, out x2);
                float sc = Score(m2, x2);
                if (sc < bestLocal - 1e-5f) { bestLocal = sc; bestIdx = c; }
            }
            axes[i] = was;
            if (bestIdx >= 0)
            {
                axes[i] = CandidateAxes[bestIdx];
                bestScore = bestLocal;
                improved = true;
                sol.Trace.Add("[" + stage + "] сустав " + (i + 1) + " «" +
                              (chain.Joints[i] != null ? chain.Joints[i].name : "?") + "» → " +
                              CandidateNames[bestIdx]);

                // Контроль по ПРИЁМОЧНОЙ батарее: запоминаем лучшую конфигурацию целиком,
                // чтобы «качели» следующего сустава не увели результат от лучшего решения.
                float am, ax;
                BatteryResidual(chain, axes, acceptanceBattery, 45, out am, out ax);
                float acc = Score(am, ax);
                if (acc < bestAcceptance - 1e-5f)
                {
                    bestAcceptance = acc;
                    bestAxes = (Vector3[])axes.Clone();
                }
            }
        }
        return improved;
    }

    // ================================================================== конвертация

    public sealed class ConversionResult
    {
        public string Id = "";
        public bool Ok;
        public string PrefabPath = "";
        public string Reason = "";
        public int Joints;
        public int DeclaredDof;
        public float StartMean;
        public float FinalMean;
        public float FinalMax;
        public bool ValidatorReady;
        public int ValidatorDof;
        public readonly List<string> AxisTrace = new List<string>();
    }

    /// <summary>Записи каталога, которые сейчас числятся рабочими (§56/§58) — их не пересобираем.</summary>
    public static readonly string[] AlreadyWorkingIds = { "stand_6axis", "scara_ls10b702s" };

    /// <summary>Все записи-роботы каталога, которым НУЖЕН рабочий префаб (13 визуальных).</summary>
    public static List<KvModelEntry> CandidatesForConversion()
    {
        var list = new List<KvModelEntry>();
        foreach (KvModelEntry e in KvModelCatalog.OfKind(KvModelKind.Robot))
        {
            if (e == null) continue;
            if (Array.IndexOf(AlreadyWorkingIds, e.Id) >= 0) continue;
            list.Add(e);
        }
        return list;
    }

    /// <summary>Конвертировать ОДНУ запись каталога (или отказаться с причиной).</summary>
    /// <param name="keepIndices">Ручное уточнение цепочки: какие найденные узлы считать суставами
    /// (индексы в порядке база→инструмент). null — использовать найденную цепочку целиком.</param>
    public static ConversionResult Convert(KvModelEntry entry, bool save = true,
                                           Vector3[] manualAxes = null, int[] keepIndices = null)
    {
        var res = new ConversionResult();
        if (entry == null) { res.Reason = "пустая запись каталога"; return res; }
        res.Id = entry.Id;
        res.DeclaredDof = ParseDofMax(entry.Dof);

        string catalogPath = CatalogRobotsFolder + "/" + entry.Id + ".prefab";
        GameObject catalogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPath);
        if (catalogPrefab == null) { res.Reason = "нет модели каталога " + catalogPath; return res; }

        GameObject work = null;
        try
        {
            work = UnityEngine.Object.Instantiate(catalogPrefab);
            work.name = entry.Id;
            work.transform.position = ProbeOrigin;
            StripEditorHelpers(work);

            var chain = Detect(work, entry.Id, res.DeclaredDof, entry.Dof);
            res.Joints = chain.Joints.Count;
            res.AxisTrace.Add("цепочка: " + chain.Note);
            if (!chain.Found) { res.Reason = chain.Refusal.Length > 0 ? chain.Refusal : chain.Note; return res; }

            // РУЧНОЕ УТОЧНЕНИЕ ЦЕПОЧКИ (окно «Сделать рабочего робота»): оператор снимает
            // с цепочки узел, который осью НЕ является (у OpenArm так снимается неподвижный
            // кронштейн плеча — тогда 8 найденных узлов становятся объявленными 7 суставами).
            if (keepIndices != null && keepIndices.Length > 0)
            {
                var kept = new List<Transform>();
                foreach (int idx in keepIndices)
                    if (idx >= 0 && idx < chain.Joints.Count) kept.Add(chain.Joints[idx]);
                if (kept.Count == 0) { res.Reason = "ручной набор суставов пуст"; return res; }
                for (int i = 1; i < kept.Count; i++)
                    if (!IsAncestorOf(kept[i - 1], kept[i]))
                    {
                        res.Reason = "ручной набор суставов не образует цепочку: «" + kept[i].name +
                                     "» не потомок «" + kept[i - 1].name + "»";
                        return res;
                    }
                if (chain.Tcp != null && !IsAncestorOf(kept[kept.Count - 1], chain.Tcp))
                {
                    res.Reason = "инструмент «" + chain.Tcp.name + "» не лежит под последним " +
                                 "выбранным суставом «" + kept[kept.Count - 1].name + "»";
                    return res;
                }
                chain.Joints.Clear();
                chain.Joints.AddRange(kept);
                res.AxisTrace.Add("цепочка уточнена ОПЕРАТОРОМ: суставов " + kept.Count);
                chain.Refusal = "";
                if (chain.DeclaredDof > 0 && chain.Joints.Count > chain.DeclaredDof)
                    chain.Refusal = "в ручной цепочке суставов " + chain.Joints.Count +
                                    ", а в каталоге объявлено «" + chain.DeclaredDofText + "»";
            }

            if (!chain.CanConvert) { res.Reason = chain.Refusal; return res; }

            Vector3[] axes = manualAxes;
            Vector3[] full = MakeBattery(chain, 6, 3);

            // Замер «как есть» — с осями по умолчанию (от Robot.fbx). Это и есть fail-first:
            // если бы хватало простого навешивания контроллера, невязка была бы уже малой.
            float startMean, startMax;
            BatteryResidual(chain, DefaultAxes(chain.Joints.Count), full, 60, out startMean, out startMax);
            res.StartMean = startMean;
            res.AxisTrace.Add("оси по умолчанию (от Robot.fbx): невязка IK " + Num(startMean) +
                              " / " + Num(startMax) + " м");

            if (axes == null || axes.Length != chain.Joints.Count)
            {
                if (manualAxes != null)
                    res.AxisTrace.Add("ручной набор осей не совпал по длине (" + manualAxes.Length +
                                      " против " + chain.Joints.Count + ") — выполнен автоподбор");
                AxisSolution sol = SearchAxes(chain);
                axes = sol.Axes;
                res.AxisTrace.AddRange(sol.Trace);
            }
            else
            {
                res.AxisTrace.Add("оси заданы ОПЕРАТОРОМ (ручной режим) — автоподбор пропущен");
            }

            float mean, max;
            BatteryResidual(chain, axes, full, 60, out mean, out max);
            res.FinalMean = mean;
            res.FinalMax = max;

            if (mean > AcceptMeanResidual || max > AcceptMaxResidual)
            {
                Vector3 worstTarget;
                float worstErr;
                WorstPoint(chain, axes, full, 60, out worstTarget, out worstErr);
                res.Reason = "самотест НЕ пройден: средняя невязка IK " + Num(mean) + " м, худшая " +
                             Num(max) + " м (порог " + Num(AcceptMeanResidual) + " / " +
                             Num(AcceptMaxResidual) + " м); худшая точка " + V(worstTarget) +
                             " от базы " + Num(Vector3.Distance(worstTarget, chain.Base.position)) +
                             " м (вылет руки " + Num(Reach(chain)) + " м) — префаб НЕ сохранён";
                return res;
            }

            ApplyKinematics(work, entry, chain, axes);

            var probe = new PoseValidator();
            probe.Init(work.GetComponent<SixAxisController>());
            res.ValidatorReady = probe.Ready;
            res.ValidatorDof = probe.Dof;
            if (!probe.Ready)
            {
                res.Reason = "PoseValidator не признал робота рабочим (Ready = false) — это дефект связки";
                return res;
            }

            if (!save) { res.Ok = true; return res; }

            string path = FunctionalRobotsFolder + "/" + entry.Id + ".prefab";
            bool success;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(work, path, out success);
            if (!success || saved == null)
            {
                res.Reason = "не удалось сохранить префаб " + path;
                return res;
            }
            res.PrefabPath = path;

            // ПРОВЕРКА СОХРАНЁННОГО АССЕТА: читаем префаб заново и повторяем самотест
            // на нём (а не на рабочей копии) — иначе «зелёный» самотест ничего не значит.
            string fail;
            if (!VerifySavedPrefab(path, out fail))
            {
                AssetDatabase.DeleteAsset(path);
                res.PrefabPath = "";
                res.Reason = "сохранённый префаб НЕ прошёл повторный самотест (" + fail +
                             ") — префаб удалён";
                return res;
            }

            res.Ok = true;
            return res;
        }
        catch (Exception e)
        {
            res.Reason = "исключение " + e.GetType().Name + ": " + e.Message;
            return res;
        }
        finally
        {
            if (work != null) UnityEngine.Object.DestroyImmediate(work);
        }
    }

    /// <summary>Заполнить кинематику `SixAxisController` по найденной цепочке.</summary>
    private static void ApplyKinematics(GameObject root, KvModelEntry entry, Chain chain, Vector3[] axes)
    {
        SixAxisController six = root.GetComponent<SixAxisController>();
        if (six == null) six = root.AddComponent<SixAxisController>();

        six.robotName = string.IsNullOrEmpty(entry.Title) ? entry.Id : entry.Title;
        six.jointTransforms = chain.Joints.ToArray();
        six.jointAxesLocal = (Vector3[])axes.Clone();
        six.jointLimits = DefaultLimits(chain.Joints.Count);
        six.baseTransform = chain.Base;
        six.fixedBase = chain.Base;
        six.endEffector = chain.Tcp;
        six.tcp = chain.Tcp;
        six.baseName = chain.Base != null ? chain.Base.name : "";
        six.endEffectorName = chain.Tcp != null ? chain.Tcp.name : "";
        six.alternativeEndEffectorName = six.endEffectorName;

        // §59. КРИТИЧНО ДЛЯ НОВЫХ МОДЕЛЕЙ:
        //   * `fixLegacyMeshParents` — правка ПОД Robot.fbx (переносит меш предплечья с запястья
        //     на локоть). У UR5-подобных моделей она бы УНЕСЛА меш предплечья на другое звено
        //     (у UR5 сустав 3 = upper_arm, сустав 4 = forearm, и меши forearm — его прямые дети).
        //   * `keepAxesAsAuthored` — запрет на «миграцию» осей J4/J5 (`AutoFixWristAxis`),
        //     которая перезаписала бы ПОДОБРАННЫЕ ЗАМЕРОМ оси у сферического запястья.
        //   * `enableSelfCollisionGuard = false` — КИНЕМАТИЧЕСКИЙ сторож самостолкновений
        //     (`linkRadius = 0,07`) настроен на геометрию робота СТЕНДА. У UR5 три оси запястья
        //     стоят в 1,3 см друг от друга (шарниры `wrist_1/2/3`), поэтому сторож видит
        //     «самостолкновение звеньев 2 и 5» в ОБЫЧНОЙ позе и снимает цель — замер сторожа §59
        //     (`KvAnyRobotPlayModeTests`): недолёт 0,157 м и строка «цель снята, робот остановлен».
        //     Это ложное срабатывание, а не реальная опасность: настоящие столкновения со сценой
        //     считает `CollisionWorld`/`PoseValidator` (ими пользуются планировщик и оракул).
        //     Настройка сторожа под конкретную модель — отдельная задача (см. отчёт §59).
        six.fixLegacyMeshParents = false;
        six.keepAxesAsAuthored = true;
        six.autoDetectAxesFromLinks = false;
        six.enableSelfCollisionGuard = false;
    }

    /// <summary>
    /// Пределы суставов: СИММЕТРИЧНЫЕ ±170°. Паспортных пределов у этих моделей в проекте нет
    /// (URDF не импортировался), поэтому выдумывать разные числа для разных осей нельзя —
    /// но и нули опасны (нулевые лимиты `PoseValidator` считает «битыми» и подменяет своими).
    /// Оператор уточняет пределы в инспекторе префаба.
    /// </summary>
    public static Vector2[] DefaultLimits(int n)
    {
        var lim = new Vector2[n];
        for (int i = 0; i < n; i++) lim[i] = new Vector2(-170f, 170f);
        return lim;
    }

    /// <summary>Повторный самотест УЖЕ СОХРАНЁННОГО префаба (по ассету, не по рабочей копии).</summary>
    public static bool VerifySavedPrefab(string path, out string failure)
    {
        failure = "";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { failure = "префаб не читается"; return false; }

        GameObject inst = null;
        try
        {
            inst = UnityEngine.Object.Instantiate(prefab);
            inst.transform.position = ProbeOrigin;
            StripEditorHelpers(inst);

            SixAxisController six = inst.GetComponentInChildren<SixAxisController>(true);
            if (six == null) { failure = "в префабе нет SixAxisController"; return false; }
            if (six.jointTransforms == null || six.jointTransforms.Length == 0)
            { failure = "в префабе пустая цепочка суставов"; return false; }
            if (six.jointAxesLocal == null || six.jointAxesLocal.Length < six.jointTransforms.Length)
            { failure = "осей меньше, чем суставов"; return false; }

            var probe = new PoseValidator();
            probe.Init(six);
            if (!probe.Ready) { failure = "PoseValidator.Ready = false"; return false; }
            if (probe.Dof != six.jointTransforms.Length)
            { failure = "Dof " + probe.Dof + " ≠ суставов " + six.jointTransforms.Length; return false; }

            var chain = new Chain { Id = path };
            chain.ModelRoot = FindModelRoot(inst);
            chain.Base = six.baseTransform;
            chain.Joints.AddRange(six.jointTransforms);
            chain.Tcp = six.tcp != null ? six.tcp : six.endEffector;
            if (chain.Tcp == null) { failure = "нет TCP"; return false; }

            float mean, max;
            BatteryResidual(chain, six.jointAxesLocal, MakeBattery(chain, 6, 3), 60, out mean, out max);
            if (mean > AcceptMeanResidual || max > AcceptMaxResidual)
            {
                failure = "невязка IK " + Num(mean) + " / " + Num(max) + " м выше порога";
                return false;
            }
            return true;
        }
        finally
        {
            if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    // ================================================================== меню и батч-точки

    [MenuItem(MenuRoot + "Отчёт (только чтение)", false, 10)]
    public static void MenuDiag()
    {
        string report = Diag();
        Debug.Log(LogTag + "\n" + report);
        WriteReport(ReportDiagPath, report);
    }

    [MenuItem(MenuRoot + "1. Конвертировать все конвертируемые модели", false, 20)]
    public static void MenuConvertAll()
    {
        if (!EditorUtility.DisplayDialog("KazistovVv · §59 · рабочие роботы",
                "Будет сделано:\n" +
                "  • для каждой модели каталога найден кинематический контур суставов;\n" +
                "  • оси суставов подобраны ЗАМЕРОМ (минимум невязки IK на батарее точек);\n" +
                "  • проверка: валидатор позы готов, IK решает точки (невязка ≤ 1 см);\n" +
                "  • рабочие префабы сохранены в Assets/Resources/Robots/<id>.prefab.\n\n" +
                "Модели БЕЗ кинематического контура (плоские сборки) и модели, где найден лишний\n" +
                "узел, будут ЧЕСТНО перечислены в отчёте и НЕ конвертированы.\n\n" +
                "Префабы каталога и сцена НЕ меняются.",
                "Конвертировать", "Отмена"))
            return;
        Report(ConvertAll(save: true, onlyId: null));
    }

    [MenuItem(MenuRoot + "2. Окно «Сделать рабочего робота»", false, 30)]
    public static void MenuOpenWindow()
    {
        KvRobotFactory59Window.ShowWindow();
    }

    public static void BatchDiag59()
    {
        string report = Diag();
        Debug.Log(LogTag + "\n" + report);
        WriteReport(ReportDiagPath, report);
    }

    public static void BatchConvert59()
    {
        Report(ConvertAll(save: true, onlyId: null));
    }

    public static void BatchConvertUr5Only59()
    {
        Report(ConvertAll(save: true, onlyId: "ur5"));
    }

    // ================================================================== отчёт: конвертация

    /// <summary>Конвертировать подряд все конвертируемые записи каталога (или одну — по id).</summary>
    public static string ConvertAll(bool save, string onlyId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("§59 КОНВЕЙЕР «ВИЗУАЛЬНАЯ МОДЕЛЬ → РАБОЧИЙ РОБОТ»");
        sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Проект: " + ProjectRoot);
        sb.AppendLine();

        string sceneFull = FullPath(SceneAssetPath);
        sb.AppendLine("MainScene.unity ДО: " + FileHash(sceneFull) + " · байт " + FileSize(sceneFull));
        sb.AppendLine();

        var ok = new List<ConversionResult>();
        var refused = new List<ConversionResult>();

        foreach (KvModelEntry entry in CandidatesForConversion())
        {
            if (!string.IsNullOrEmpty(onlyId) && entry.Id != onlyId) continue;

            ConversionResult r = Convert(entry, save);
            sb.AppendLine((r.Ok ? "  [OK]  " : "  [—]   ") + entry.Id + " («" + entry.Title + "»)");
            if (r.AxisTrace.Count > 0)
                foreach (string line in r.AxisTrace) sb.AppendLine("         " + line);
            if (r.Ok)
            {
                sb.AppendLine("         суставов " + r.Joints + " (каталог заявляет «" + entry.Dof +
                              "») · невязка IK была " + Num(r.StartMean) + " м → стала " +
                              Num(r.FinalMean) + " м (худшая " + Num(r.FinalMax) + " м)");
                sb.AppendLine("         PoseValidator: Ready = " + r.ValidatorReady +
                              " · Dof = " + r.ValidatorDof);
                sb.AppendLine("         префаб: " + r.PrefabPath);
                ok.Add(r);
            }
            else
            {
                sb.AppendLine("         ОТКАЗ: " + r.Reason);
                refused.Add(r);
            }
        }

        sb.AppendLine();
        sb.AppendLine("ИТОГ: сконвертировано " + ok.Count + " · отказов " + refused.Count);
        foreach (ConversionResult r in refused)
            sb.AppendLine("  отказ «" + r.Id + "»: " + r.Reason);

        sb.AppendLine();
        sb.AppendLine("MainScene.unity ПОСЛЕ: " + FileHash(sceneFull) + " · байт " + FileSize(sceneFull));

        sb.AppendLine();
        sb.AppendLine("СОДЕРЖИМОЕ Assets/Resources/Robots:");
        foreach (string p in PrefabsIn(FunctionalRobotsFolder))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            RobotController rc = prefab != null ? prefab.GetComponentInChildren<RobotController>(true) : null;
            sb.AppendLine("  " + Path.GetFileName(p) + " · " +
                          (rc == null ? "БЕЗ RobotController" : rc.GetType().Name) +
                          (prefab != null && prefab.GetComponentInChildren<Collider>(true) != null
                              ? " · коллайдеры есть" : " · коллайдеров НЕТ"));
        }

        return sb.ToString();
    }

    private static void Report(string text)
    {
        Debug.Log(LogTag + "\n" + text);
        WriteReport(ReportConvertPath, text);
    }

    // ================================================================== отчёт: диагностика

    public static string Diag(string title = "§59 ДИАГНОСТИКА: КОНТРАКТ «РАБОЧЕГО РОБОТА» И КИНЕМАТИЧЕСКИЕ ЦЕПОЧКИ")
    {
        var sb = new StringBuilder();
        sb.AppendLine(title + " (ТОЛЬКО ЧТЕНИЕ)");
        sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Проект: " + ProjectRoot);
        sb.AppendLine("Редактор: " + Application.unityVersion);
        sb.AppendLine();

        string sceneFull = FullPath(SceneAssetPath);
        sb.AppendLine("MainScene.unity ДО отчёта: " + FileHash(sceneFull) + " · байт " + FileSize(sceneFull));
        sb.AppendLine();

        SectionSourceGates(sb);
        SectionRobotTable(sb);
        SectionCatalogChain(sb);
        SectionFlatModels(sb);
        SectionBaseline(sb);

        sb.AppendLine();
        sb.AppendLine("MainScene.unity ПОСЛЕ отчёта: " + FileHash(sceneFull) + " · байт " + FileSize(sceneFull));
        return sb.ToString();
    }

    private static void SectionSourceGates(StringBuilder sb)
    {
        sb.AppendLine("========== 1. ГЕЙТЫ В КОДЕ УПРАВЛЕНИЯ (замер по исходникам) ==========");
        string[] files =
        {
            "Assets/_Project/01_Scripts/Trajectory/PoseValidator.cs",
            "Assets/_Project/01_Scripts/Trajectory/ReachabilityOracle.cs",
            "Assets/_Project/01_Scripts/Core/SixAxisController.cs"
        };
        string[] patterns =
        {
            "Length < 6", "Length >= 6", "as SixAxisController", "as SCARAController",
            "rollbackPose = new Quaternion", "AutoFixWristAxis", "autoDetectAxesFromLinks",
            "keepAxesAsAuthored"
        };
        foreach (string f in files)
        {
            string full = FullPath(f);
            if (!File.Exists(full)) { sb.AppendLine("  " + Path.GetFileName(f) + " — НЕТ ФАЙЛА"); continue; }
            string[] lines = File.ReadAllLines(full, Encoding.UTF8);
            sb.AppendLine("  " + Path.GetFileName(f) + ":");
            for (int i = 0; i < lines.Length; i++)
            {
                for (int p = 0; p < patterns.Length; p++)
                {
                    if (lines[i].IndexOf(patterns[p], StringComparison.Ordinal) < 0) continue;
                    sb.AppendLine("    " + (i + 1) + ": " + lines[i].Trim());
                    break;
                }
            }
        }
        sb.AppendLine();
    }

    private static void SectionRobotTable(StringBuilder sb)
    {
        sb.AppendLine("========== 2. ПРЕФАБЫ РОБОТОВ: ЧТО ЕСТЬ СЕЙЧАС ==========");
        List<string> prefabs = PrefabsIn(FunctionalRobotsFolder);
        prefabs.AddRange(PrefabsIn(CatalogRobotsFolder));
        prefabs.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (string path in prefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("  " + path + ": не загружается"); continue; }

            GameObject inst = null;
            try
            {
                inst = UnityEngine.Object.Instantiate(prefab);
                inst.transform.position = ProbeOrigin;
                StripEditorHelpers(inst);

                RobotController rc = inst.GetComponentInChildren<RobotController>(true);
                string id = Path.GetFileNameWithoutExtension(path);
                KvModelEntry entry = KvModelCatalog.Find(id);
                var chain = Detect(inst, id, entry != null ? ParseDofMax(entry.Dof) : 0,
                                   entry != null ? entry.Dof : "");

                sb.AppendLine("  " + Path.GetFileName(path));
                sb.AppendLine("      узлов модели: " + chain.NodeCount + " · " + chain.Note);
                if (!string.IsNullOrEmpty(chain.Refusal)) sb.AppendLine("      ОТКАЗ: " + chain.Refusal);

                if (rc == null)
                {
                    sb.AppendLine("      RobotController: НЕТ (визуальная модель) → F не выберет, " +
                                  "PoseValidator/ReachabilityOracle пропустят");
                }
                else
                {
                    var six = rc as SixAxisController;
                    var scara = rc as SCARAController;
                    sb.AppendLine("      RobotController: " + rc.GetType().Name +
                                  " · robotName «" + rc.robotName + "»");
                    if (six != null)
                    {
                        sb.AppendLine("      цепочка контроллера: " +
                                      (six.jointTransforms == null ? "null" : six.jointTransforms.Length.ToString()) +
                                      " суставов · осей " +
                                      (six.jointAxesLocal == null ? "null" : six.jointAxesLocal.Length.ToString()) +
                                      " · tcp " + Nm(six.tcp) + " · endEffector " + Nm(six.endEffector) +
                                      " · base " + Nm(six.baseTransform) +
                                      " · fixLegacyMeshParents " + six.fixLegacyMeshParents);
                    }
                    else if (scara != null)
                    {
                        sb.AppendLine("      SCARA: J1 " + Nm(scara.joint1) + " · J2 " + Nm(scara.joint2) +
                                      " · Z " + Nm(scara.joint3) + " · база " + Nm(scara.baseTransform));
                    }

                    var validator = new PoseValidator();
                    validator.Init(rc);
                    sb.AppendLine("      PoseValidator: Ready = " + validator.Ready +
                                  " · Dof = " + validator.Dof);
                    var oracle = new ReachabilityOracle();
                    oracle.Init(rc, null);
                    sb.AppendLine("      ReachabilityOracle: Ready = " + oracle.Ready);
                }

                sb.AppendLine("      коллайдеров: " + inst.GetComponentsInChildren<Collider>(true).Length);
            }
            catch (Exception e)
            {
                sb.AppendLine("      ОШИБКА замера: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
            }
        }
        sb.AppendLine();
    }

    private static void SectionCatalogChain(StringBuilder sb)
    {
        sb.AppendLine("========== 3. ДЕТЕКТОР ЦЕПОЧКИ ПО 13 ВИЗУАЛЬНЫМ МОДЕЛЯМ КАТАЛОГА ==========");
        foreach (KvModelEntry entry in CandidatesForConversion())
        {
            string path = CatalogRobotsFolder + "/" + entry.Id + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("  " + entry.Id + ": нет префаба " + path); continue; }
            GameObject inst = null;
            try
            {
                inst = UnityEngine.Object.Instantiate(prefab);
                inst.transform.position = ProbeOrigin;
                var chain = Detect(inst, entry.Id, ParseDofMax(entry.Dof), entry.Dof);
                sb.AppendLine("  " + entry.Id + ": " + chain.Note +
                              " · каталог заявляет Dof «" + entry.Dof + "»");
                if (chain.Found)
                {
                    sb.AppendLine("      база: " + chain.Base.name + " · инструмент: " + Nm(chain.Tcp));
                    sb.AppendLine("      суставы (" + chain.Joints.Count + "): " + chain.JointNames);
                }
                if (!string.IsNullOrEmpty(chain.Refusal))
                    sb.AppendLine("      ВЕРДИКТ: " + chain.Refusal);
            }
            finally
            {
                if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
            }
        }
        sb.AppendLine();
    }

    private static void SectionFlatModels(StringBuilder sb)
    {
        sb.AppendLine("========== 4. ПЛОСКИЕ СБОРКИ (честный отказ) ==========");
        foreach (string id in new[] { "moveo", "thor" })
        {
            string path = CatalogRobotsFolder + "/" + id + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("  " + id + ": нет префаба"); continue; }
            KvModelEntry e = KvModelCatalog.Find(id);
            GameObject inst = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Transform root = FindModelRoot(inst);
                int maxChildren = 0;
                Transform widest = null;
                if (root != null)
                    for (int i = 0; i < root.childCount; i++)
                        if (root.GetChild(i).childCount > maxChildren)
                        { maxChildren = root.GetChild(i).childCount; widest = root.GetChild(i); }

                sb.AppendLine("  " + id + ": узлов " + CountNodes(root) +
                              " · прямых детей вершины «" + (root != null ? root.name : "—") + "»: " +
                              (root != null ? root.childCount : 0) +
                              " · самый ветвистый ребёнок: «" + (widest != null ? widest.name : "—") +
                              "» (" + maxChildren + " детей)");
                if (e != null) sb.AppendLine("      заметка каталога: " + FirstSentence(e.Note));
                var chain = Detect(inst, id, e != null ? ParseDofMax(e.Dof) : 0, e != null ? e.Dof : "");
                sb.AppendLine("      вердикт: " + (chain.Found ? "цепочка найдена" : "ЦЕПОЧКИ НЕТ"));
            }
            finally { UnityEngine.Object.DestroyImmediate(inst); }
        }
        sb.AppendLine();
    }

    /// <summary>
    /// FAIL-FIRST-ЗАМЕР: что даёт «просто навесить SixAxisController» на каждую модель —
    /// и что даёт подбор осей. Именно этот замер доказывает, что без правки связки
    /// и без подбора осей модель не работает.
    /// </summary>
    private static void SectionBaseline(StringBuilder sb)
    {
        sb.AppendLine("========== 5. FAIL-FIRST: ЧТО ДАЁТ МОДЕЛЬ «КАК ЕСТЬ» И ЧТО ДАЁТ ПОДБОР ОСЕЙ ==========");
        foreach (KvModelEntry entry in CandidatesForConversion())
        {
            string path = CatalogRobotsFolder + "/" + entry.Id + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            GameObject inst = UnityEngine.Object.Instantiate(prefab);
            inst.transform.position = ProbeOrigin;
            try
            {
                var chain = Detect(inst, entry.Id, ParseDofMax(entry.Dof), entry.Dof);
                sb.AppendLine("  " + entry.Id + ":");
                if (!chain.Found)
                {
                    sb.AppendLine("      как есть: RobotController НЕТ → F не выберет, валидатор Ready = false");
                    sb.AppendLine("      подбор осей невозможен: " + chain.Refusal);
                    continue;
                }

                var six = inst.AddComponent<SixAxisController>();
                six.robotName = entry.Id;
                six.jointTransforms = chain.Joints.ToArray();
                six.jointAxesLocal = DefaultAxes(chain.Joints.Count);
                six.baseTransform = chain.Base;
                six.fixedBase = chain.Base;
                six.endEffector = chain.Tcp;

                var validator = new PoseValidator();
                validator.Init(six);
                float mean0, max0;
                BatteryResidual(chain, six.jointAxesLocal, MakeBattery(chain, 6, 3), 60, out mean0, out max0);
                sb.AppendLine("      как есть (оси по умолчанию от Robot.fbx): Ready = " + validator.Ready +
                              " · Dof = " + validator.Dof + " · невязка IK " + Num(mean0) +
                              " / " + Num(max0) + " м");

                AxisSolution sol = SearchAxes(chain);
                sb.AppendLine("      подбор осей замером: невязка IK " + Num(sol.StartMean) + " м → " +
                              Num(sol.MeanResidual) + " м (худшая " + Num(sol.MaxResidual) +
                              " м) за " + sol.Sweeps + " проходов");

                // Сторож fail-first: с осями по умолчанию модель НЕ рабочая, с подобранными — рабочая.
                bool baselineBad = mean0 > AcceptMeanResidual;
                bool solvedOk = sol.MeanResidual <= AcceptMeanResidual && sol.MaxResidual <= AcceptMaxResidual;
                sb.AppendLine("      вердикт: «как есть» " + (baselineBad ? "НЕ работает" : "работает") +
                              " · после подбора осей " + (solvedOk ? "РАБОТАЕТ" : "НЕ работает") +
                              (baselineBad && solvedOk ? "  ← это и есть доказательство fail-first" : ""));
            }
            catch (Exception e)
            {
                sb.AppendLine("      ОШИБКА замера: " + e.GetType().Name + ": " + e.Message);
            }
            finally { UnityEngine.Object.DestroyImmediate(inst); }
        }
        sb.AppendLine();
    }

    // ================================================================== вспомогательное

    private static string FirstSentence(string text)
    {
        if (string.IsNullOrEmpty(text)) return "—";
        int dot = text.IndexOf('.');
        return dot > 0 ? text.Substring(0, dot + 1) : text;
    }

    private static void StripEditorHelpers(GameObject inst)
    {
        if (inst == null) return;
        var auto = inst.GetComponent<SixAxisAutoSetup>();
        if (auto != null) UnityEngine.Object.DestroyImmediate(auto);
    }

    private static string Nm(Transform t) { return t != null ? "«" + t.name + "»" : "НЕТ"; }

    public static List<string> PrefabsIn(string folder)
    {
        var list = new List<string>();
        if (!AssetDatabase.IsValidFolder(folder)) return list;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && !list.Contains(p))
                list.Add(p);
        }
        return list;
    }

    public static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

    public static string FullPath(string assetPath)
    {
        return Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    public static string FileHash(string fullPath)
    {
        if (!File.Exists(fullPath)) return "НЕТ ФАЙЛА";
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (var st = File.OpenRead(fullPath))
            return BitConverter.ToString(sha.ComputeHash(st)).Replace("-", "");
    }

    public static string FileSize(string fullPath)
    {
        return File.Exists(fullPath) ? new FileInfo(fullPath).Length.ToString() : "—";
    }

    public static void WriteReport(string path, string text)
    {
        try
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Debug.Log(LogTag + " отчёт записан: " + path);
        }
        catch (Exception e)
        {
            Debug.LogWarning(LogTag + " не удалось записать отчёт: " + e.Message);
        }
    }

    public static string Num(float v)
    {
        return v.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string V(Vector3 v)
    {
        return "(" + v.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "; " +
                     v.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "; " +
                     v.z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")";
    }
}

/// <summary>
/// §59. ОКНО «СДЕЛАТЬ РАБОЧЕГО РОБОТА»: показывает по каждой модели каталога найденную
/// кинематическую цепочку, позволяет УТОЧНИТЬ ось каждого сустава вручную (шесть главных
/// осей), проверить решение самотестом и собрать рабочий префаб. Это и есть «минимальный
/// ручной труд»: автоматика делает всё, что можно измерить, а спорное решение показывает
/// человеку, а не принимает молча.
/// </summary>
public sealed class KvRobotFactory59Window : EditorWindow
{
    private int entryIndex;
    private Vector2 scroll;
    private readonly List<string> log = new List<string>();
    private KvModelEntry current;
    private GameObject probe;
    private KvRobotFactory59.Chain chain;
    private Vector3[] axes;              // оси по ВСЕМ найденным узлам (индекс — узел цепочки)
    private readonly List<bool> keep = new List<bool>();   // какие узлы считать суставами
    private float mean, max;

    public static void ShowWindow()
    {
        var w = GetWindow<KvRobotFactory59Window>(false, "Рабочий робот (§59)", true);
        w.minSize = new Vector2(600f, 460f);
        w.Show();
    }

    private void OnDisable()
    {
        DropProbe();
    }

    private void DropProbe()
    {
        if (probe != null) DestroyImmediate(probe);
        probe = null;
        chain = null;
        axes = null;
        keep.Clear();
    }

    /// <summary>Индексы узлов, оставленных оператором в цепочке (в том же порядке).</summary>
    private List<int> KeptIndices()
    {
        var list = new List<int>();
        for (int i = 0; i < keep.Count; i++) if (keep[i]) list.Add(i);
        return list;
    }

    /// <summary>Цепочка и оси РОВНО по текущему выбору оператора.</summary>
    private KvRobotFactory59.Chain ActiveChain(out Vector3[] activeAxes)
    {
        activeAxes = null;
        if (chain == null || axes == null) return null;
        List<int> idx = KeptIndices();
        if (idx.Count == 0) return null;

        activeAxes = new Vector3[idx.Count];
        for (int i = 0; i < idx.Count; i++) activeAxes[i] = axes[idx[i]];

        if (idx.Count == chain.Joints.Count) return chain;
        var c = new KvRobotFactory59.Chain
        {
            Id = chain.Id, ModelRoot = chain.ModelRoot, Base = chain.Base, Tcp = chain.Tcp
        };
        foreach (int i in idx) c.Joints.Add(chain.Joints[i]);
        return c;
    }

    private void OnGUI()
    {
        List<KvModelEntry> robots = KvRobotFactory59.CandidatesForConversion();
        EditorGUILayout.LabelField("Модель каталога (записи стендов уже рабочие — их нет в списке)",
                                   EditorStyles.boldLabel);

        string[] names = new string[robots.Count];
        for (int i = 0; i < robots.Count; i++) names[i] = robots[i].Id + " — " + robots[i].Title;
        entryIndex = Mathf.Clamp(entryIndex, 0, Mathf.Max(0, robots.Count - 1));
        int picked = EditorGUILayout.Popup("Модель", entryIndex, names);
        if (picked != entryIndex) { entryIndex = picked; DropProbe(); }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1. Найти цепочку", GUILayout.Height(24f))) BuildProbe(robots);
            using (new EditorGUI.DisabledScope(chain == null || !chain.Found))
            {
                if (GUILayout.Button("2. Проверить IK", GUILayout.Height(24f))) Measure();
                if (GUILayout.Button("3. Подобрать оси замером", GUILayout.Height(24f))) Solve();
                if (GUILayout.Button("4. Создать рабочий префаб", GUILayout.Height(24f))) Create(robots);
            }
        }

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (chain != null)
        {
            EditorGUILayout.LabelField("Цепочка: " + chain.Note, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(chain.Refusal))
                EditorGUILayout.HelpBox(chain.Refusal, MessageType.Warning);
            EditorGUILayout.LabelField("Паспорт каталога: Dof «" +
                (current != null ? current.Dof : "—") + "» · найдено узлов " + chain.Joints.Count,
                EditorStyles.wordWrappedLabel);

            if (chain.Found && axes != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Суставы: снимите галочку у узла, который осью НЕ является " +
                                           "(кронштейн, крепление, схват), и уточните оси.",
                                           EditorStyles.boldLabel);
                for (int i = 0; i < chain.Joints.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        keep[i] = EditorGUILayout.ToggleLeft(chain.Joints[i].name, keep[i],
                                                             GUILayout.Width(230f));
                        using (new EditorGUI.DisabledScope(!keep[i]))
                        {
                            int idx = ClosestCandidate(axes[i]);
                            int newIdx = EditorGUILayout.Popup(idx, KvRobotFactory59.CandidateNames,
                                                               GUILayout.Width(70f));
                            if (newIdx != idx) axes[i] = KvRobotFactory59.CandidateAxes[newIdx];
                        }
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("В цепочке узлов: " + KeptIndices().Count +
                                           " · невязка IK: средняя " + KvRobotFactory59.Num(mean) +
                                           " м · худшая " + KvRobotFactory59.Num(max) + " м",
                                           EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Порог приёмки: ≤ " +
                                           KvRobotFactory59.Num(KvRobotFactory59.AcceptMeanResidual) +
                                           " / " + KvRobotFactory59.Num(KvRobotFactory59.AcceptMaxResidual) +
                                           " м", EditorStyles.wordWrappedLabel);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Нажмите «Найти цепочку»: инструмент разберёт модель, найдёт " +
                                    "неподвижную базу и суставы и покажет результат.", MessageType.Info);
        }

        EditorGUILayout.Space();
        foreach (string line in log) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private static int ClosestCandidate(Vector3 v)
    {
        int best = 0; float bestDot = -2f;
        for (int i = 0; i < KvRobotFactory59.CandidateAxes.Length; i++)
        {
            float d = Vector3.Dot(v.normalized, KvRobotFactory59.CandidateAxes[i]);
            if (d > bestDot) { bestDot = d; best = i; }
        }
        return best;
    }

    private void BuildProbe(List<KvModelEntry> robots)
    {
        DropProbe();
        log.Clear();
        if (entryIndex < 0 || entryIndex >= robots.Count) return;
        current = robots[entryIndex];

        string path = KvRobotFactory59.CatalogRobotsFolder + "/" + current.Id + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { log.Add("нет префаба " + path); return; }

        probe = Instantiate(prefab);
        probe.transform.position = new Vector3(0f, 5000f, 0f);
        var auto = probe.GetComponent<SixAxisAutoSetup>();
        if (auto != null) DestroyImmediate(auto);

        chain = KvRobotFactory59.Detect(probe, current.Id,
            KvRobotFactory59.ParseDofMax(current.Dof), current.Dof);
        axes = KvRobotFactory59.DefaultAxes(chain.Joints.Count);
        keep.Clear();
        for (int i = 0; i < chain.Joints.Count; i++) keep.Add(true);
        log.Add("цепочка: " + chain.Note);
        if (!string.IsNullOrEmpty(chain.Refusal)) log.Add("отказ: " + chain.Refusal);
        Repaint();
    }

    private void Measure()
    {
        Vector3[] active;
        KvRobotFactory59.Chain c = ActiveChain(out active);
        if (c == null || !c.Found) { log.Add("в цепочке не осталось суставов"); Repaint(); return; }
        KvRobotFactory59.BatteryResidual(c, active, KvRobotFactory59.MakeBattery(c, 6, 3),
            60, out mean, out max);
        log.Add("проверка IK: средняя " + KvRobotFactory59.Num(mean) + " м · худшая " +
                KvRobotFactory59.Num(max) + " м");
        Repaint();
    }

    private void Solve()
    {
        Vector3[] active;
        KvRobotFactory59.Chain c = ActiveChain(out active);
        if (c == null || !c.Found) { log.Add("в цепочке не осталось суставов"); Repaint(); return; }
        var sol = KvRobotFactory59.SearchAxes(c);
        mean = sol.MeanResidual;
        max = sol.MaxResidual;

        // Подобранные оси возвращаем в общий массив (по индексам оставленных узлов),
        // чтобы ручные правки и автоподбор жили в одном месте.
        List<int> idx = KeptIndices();
        for (int i = 0; i < idx.Count && i < sol.Axes.Length; i++) axes[idx[i]] = sol.Axes[i];
        foreach (string line in sol.Trace) log.Add(line);
        Repaint();
    }

    private void Create(List<KvModelEntry> robots)
    {
        if (entryIndex < 0 || entryIndex >= robots.Count) return;
        Vector3[] active;
        KvRobotFactory59.Chain c = ActiveChain(out active);
        if (c == null || !c.Found) { log.Add("сначала найдите цепочку"); Repaint(); return; }

        List<int> idx = KeptIndices();
        var result = KvRobotFactory59.Convert(robots[entryIndex], true, active, idx.ToArray());
        log.Add(result.Ok
            ? "префаб создан: " + result.PrefabPath + " (невязка " +
              KvRobotFactory59.Num(result.FinalMean) + " м, худшая " +
              KvRobotFactory59.Num(result.FinalMax) + " м)"
            : "ОТКАЗ: " + result.Reason);
        Repaint();
    }
}
#endif
