using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Стратегия планирования для бенчмарка (этап 14 ТЗ).</summary>
    public enum KvPlanStrategy
    {
        /// <summary>Быстрый двунаправленный RRT (штатный `Planner`, как в 8 траекториях).</summary>
        BiRrt = 0,
        /// <summary>BiRRT + анитайм-улучшения (несколько перезапусков и выбор лучшего пути) — аналог RRT*.</summary>
        RrtStar = 1,
        /// <summary>BiRRT + пост-оптимизация пути и времени (сокращение, сглаживание, перепараметризация) — аналог TrajOpt.</summary>
        TrajOpt = 2
    }

    /// <summary>Результат одной задачи бенчмарка (этап 14 ТЗ).</summary>
    public class KvBenchTask
    {
        public int index;
        public KvPlanStrategy strategy;
        public bool success;
        public float timeMs;
        public float lengthM;
        public float limitMarginDeg;
        public int samples;
        public int iterations;
        public string note = "";
    }

    /// <summary>Итог бенчмарка по одной стратегии.</summary>
    public class KvBenchResult
    {
        public KvPlanStrategy strategy;
        public int tasks;
        public int success;
        public float avgTimeMs;
        public float avgLengthM;
        public float avgMarginDeg;
        public float minTimeMs = float.MaxValue;
        public float maxTimeMs;

        public float SuccessRate { get { return tasks > 0 ? success / (float)tasks : 0f; } }
        public string StrategyLabel
        {
            get
            {
                switch (strategy)
                {
                    case KvPlanStrategy.RrtStar: return "RRT* (анитайм)";
                    case KvPlanStrategy.TrajOpt: return "TrajOpt (пост-оптимизация)";
                    default: return "BiRRT (штатный)";
                }
            }
        }

        public string Line()
        {
            return StrategyLabel + ": успех " + (SuccessRate * 100f).ToString("0") + " % (" + success +
                   "/" + tasks + ") · среднее время " + avgTimeMs.ToString("0.0") + " мс · средняя длина " +
                   avgLengthM.ToString("0.000") + " м · запас " + avgMarginDeg.ToString("0.0") + "°";
        }
    }

    /// <summary>
    /// ЭТАП 14 ТЗ: БЕНЧМАРК ПЛАНИРОВЩИКА и ЭТАП 15 ТЗ: ВИЗУАЛИЗАЦИЯ ДЕРЕВА RRT.
    ///
    /// БЕНЧМАРК: прогоняет N задач (по умолчанию 100) — каждая задача это случайная достижимая
    /// цель вокруг базы робота; для каждой цели запускаются три стратегии:
    ///   • BiRRT — штатный `Planner.PlanToGoal` (как в 8 траекториях);
    ///   • RRT* (анитайм) — тот же BiRRT, но с несколькими перезапусками и выбором ЛУЧШЕГО пути
    ///     по длине (асимптотическое улучшение: чем больше попыток, тем короче путь);
    ///   • TrajOpt (пост-оптимизация) — путь BiRRT проходит сокращение (short-cut),
    ///     сглаживание путей (`KvTrajMath.Smooth`) и перепараметризацию по лимитам
    ///     (`KvTrajMath.Retime`) — так оценивается выигрыш оптимизации поверх RRT.
    /// Считаются: среднее время, success rate, средняя длина, запас до лимитов; таблица и
    /// выгрузка в CSV/JSON. Прогон идёт ПОРЦИЯМИ (по задачам за кадр), чтобы не морозить кадр.
    ///
    /// ВИЗУАЛИЗАЦИЯ ДЕРЕВА: планировщик записывает узлы дерева (флаг `Planner.RecordTree`),
    /// сервис рисует оба дерева (от старта и от цели) в сцене: точки TCP узлов и рёбра к
    /// родителям. Включается/выключается кнопкой (ТЗ) — эффектно для демонстраций:
    /// видно, как планировщик «щупает» пространство и как деревья срастаются.
    /// </summary>
    public class KvPlannerLab
    {
        public const string StrategiesPrefsKey = "KazistovVv.Bench.Strategies";
        public const string TasksPrefsKey = "KazistovVv.Bench.Tasks";

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private CollisionWorld world;
        private FeatureHub features;

        private int tasksPerRun = 100;
        private float radiusMin = 0.30f;
        private float radiusMax = 0.62f;
        private int rerunsForRrtStar = 4;

        // --- прогон
        private bool running;
        private int cursor;
        private int totalTasks;
        private double[] startPose;
        private readonly List<double[]> goals = new List<double[]>();
        private readonly List<KvBenchTask> currentTasks = new List<KvBenchTask>();
        private readonly List<KvBenchResult> results = new List<KvBenchResult>();
        private int strategyCursor;
        private float runStartTime;

        // --- визуализация дерева
        private bool treeVisible;
        private LineRenderer treeLines;
        private Transform treeRoot;
        private int lastTreeVersion = -1;
        private bool emptyTreeReported;    // ФИКС 4: «дерево пусто» сказано один раз на состояние
        private float treeTimer;

        public bool Running { get { return running; } }
        public int Progress { get { return cursor; } }
        public int Total { get { return totalTasks; } }
        public IReadOnlyList<KvBenchResult> Results { get { return results; } }
        public bool TreeVisible { get { return treeVisible; } }
        public string LastReport { get; private set; } = "";

        /// <summary>
        /// Подпись стратегии последнего прогона (для свойств варианта траектории и дерева моделей).
        /// Если прогонов ещё не было — возвращается «—».
        /// </summary>
        public string LastStrategyLabel
        {
            get
            {
                if (results.Count == 0) return "—";
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < results.Count; i++)
                {
                    if (i > 0) sb.Append(" / ");
                    sb.Append(results[i].StrategyLabel).Append(' ')
                      .Append((results[i].SuccessRate * 100f).ToString("0")).Append('%');
                }
                return sb.ToString();
            }
        }

        public int TasksPerRun
        {
            get { return tasksPerRun; }
            set { tasksPerRun = Mathf.Clamp(value, 5, 500); }
        }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,
            FeatureHub hub)
        {
            flow = controller;
            world = collisionWorld;
            features = hub;
            tasksPerRun = Mathf.Clamp(PlayerPrefs.GetInt(TasksPrefsKey, tasksPerRun), 5, 500);
        }

        // ================================================================== бенчмарк

        /// <summary>Начать прогон бенчмарка (N задач × 3 стратегии).</summary>
        public bool Start()
        {
            if (running) return false;
            if (flow == null || flow.Planner == null || !flow.Planner.Ready)
            {
                Report("бенчмарк невозможен: планировщик не готов");
                return false;
            }

            PoseValidator v = flow.Validator;
            startPose = v.CopyCurrent();
            goals.Clear();
            results.Clear();
            currentTasks.Clear();

            int made = 0;
            int attempts = 0;
            while (made < tasksPerRun && attempts < tasksPerRun * 40)
            {
                attempts++;
                double[] goal = RandomGoal(v);
                if (goal == null) continue;
                goals.Add(goal);
                made++;
            }
            if (goals.Count == 0)
            {
                Report("бенчмарк: не удалось построить достижимые цели");
                return false;
            }

            totalTasks = goals.Count;
            cursor = 0;
            strategyCursor = 0;
            allTasks.Clear();
            running = true;
            runStartTime = Time.realtimeSinceStartup;
            Report("бенчмарк запущен: задач " + totalTasks + " × стратегий 3 (BiRRT · RRT* · TrajOpt)");
            return true;
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            Report("бенчмарк остановлен на " + cursor + " из " + totalTasks + " задач");
        }

        private double[] RandomGoal(PoseValidator v)
        {
            Vector3 basePos = v.BasePosition;
            double[] seed = startPose != null ? startPose : v.CopyCurrent();
            for (int k = 0; k < 12; k++)
            {
                float ang = UnityEngine.Random.value * Mathf.PI * 2f;
                float r = Mathf.Lerp(radiusMin, radiusMax, UnityEngine.Random.value);
                float h = TrajectoryCore.StandBuilder.TopHeight +
                          Mathf.Lerp(0.06f, 0.28f, UnityEngine.Random.value);
                Vector3 point = basePos + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                point.y = h;

                double[] q;
                if (!v.SolveIk(point, seed, out q)) continue;
                if (!v.WithinLimits(q)) continue;
                if (v.LimitMargin(q) < 6f) continue;
                if (world != null && v.ClearanceAt(q, world, out _, out _) < 0.03f) continue;
                return q;
            }
            return null;
        }

        /// <summary>Кадровое обслуживание: порция задач за кадр (прогон не морозит кадр).</summary>
        public void Tick(float deltaTime)
        {
            if (running) TickBenchmark();
            if (treeVisible) TickTree(deltaTime);
        }

        private void TickBenchmark()
        {
            if (flow == null || flow.Planner == null || !flow.Planner.Ready)
            {
                Stop();
                return;
            }
            float sliceStart = Time.realtimeSinceStartup;
            const float sliceMs = 12f;

            while (running && (Time.realtimeSinceStartup - sliceStart) * 1000f < sliceMs)
            {
                if (cursor >= goals.Count)
                {
                    cursor = 0;
                    strategyCursor++;
                    if (strategyCursor >= 3)
                    {
                        FinishBenchmark();
                        return;
                    }
                    currentTasks.Clear();
                    continue;
                }

                KvPlanStrategy strategy = (KvPlanStrategy)strategyCursor;
                KvBenchTask task = RunTask(strategy, cursor);
                currentTasks.Add(task);
                cursor++;

            }
        }

        private void FinishBenchmark()
        {
            running = false;
            // Итоги по стратегиям собираются из накопленных результатов.
            for (int s = 0; s < 3; s++)
            {
                var result = new KvBenchResult { strategy = (KvPlanStrategy)s };
                result.tasks = totalTasks;
                float sumTime = 0f, sumLen = 0f, sumMargin = 0f;
                int ok = 0;
                foreach (KvBenchTask t in allTasks)
                {
                    if (t.strategy != (KvPlanStrategy)s) continue;
                    if (!t.success) continue;
                    ok++;
                    sumTime += t.timeMs;
                    sumLen += t.lengthM;
                    sumMargin += t.limitMarginDeg;
                    if (t.timeMs < result.minTimeMs) result.minTimeMs = t.timeMs;
                    if (t.timeMs > result.maxTimeMs) result.maxTimeMs = t.timeMs;
                }
                result.success = ok;
                result.avgTimeMs = ok > 0 ? sumTime / ok : 0f;
                result.avgLengthM = ok > 0 ? sumLen / ok : 0f;
                result.avgMarginDeg = ok > 0 ? sumMargin / ok : 0f;
                if (result.minTimeMs == float.MaxValue) result.minTimeMs = 0f;
                results.Add(result);
            }

            var sb = new StringBuilder();
            sb.Append("бенчмарк завершён за ").Append((Time.realtimeSinceStartup - runStartTime)
                .ToString("0.0")).Append(" с:\n");
            foreach (KvBenchResult r in results) sb.Append("  · ").Append(r.Line()).Append('\n');
            LastReport = sb.ToString();
            Report(LastReport);
        }

        private readonly List<KvBenchTask> allTasks = new List<KvBenchTask>();

        /// <summary>Одна задача: план выбранной стратегией от текущей позы к цели.</summary>
        private KvBenchTask RunTask(KvPlanStrategy strategy, int index)
        {
            var task = new KvBenchTask { index = index, strategy = strategy };
            if (index >= goals.Count) { task.note = "нет цели"; return task; }
            Planner planner = flow.Planner;
            PoseValidator v = flow.Validator;
            double[] goal = goals[index];

            float t0 = Time.realtimeSinceStartup;
            PlannedTrajectory plan = null;

            switch (strategy)
            {
                case KvPlanStrategy.BiRrt:
                    plan = planner.PlanToGoal(startPose, goal, 1000 + index, "bench");
                    break;

                case KvPlanStrategy.RrtStar:
                {
                    // Анитайм: несколько перезапусков с разными seed — берём САМЫЙ КОРОТКИЙ путь.
                    double bestLen = double.MaxValue;
                    for (int k = 0; k < Mathf.Max(1, rerunsForRrtStar); k++)
                    {
                        PlannedTrajectory candidate = planner.PlanToGoal(startPose, goal,
                            2000 + index * 17 + k * 131, "bench*");
                        if (candidate == null) continue;
                        if (candidate.Length < bestLen) { bestLen = candidate.Length; plan = candidate; }
                    }
                    break;
                }

                default:
                {
                    // TrajOpt: путь BiRRT + сокращение + сглаживание + перепараметризация.
                    PlannedTrajectory basePlan = planner.PlanToGoal(startPose, goal, 3000 + index, "bench-opt");
                    if (basePlan != null && basePlan.Path != null && basePlan.Path.Length >= 4)
                    {
                        double[][] smoothed = KvTrajMath.Smooth(basePlan.Path, KvSmoothMethod.BSpline, 0.5f);
                        PlannedTrajectory draft = KvTrajMath.Clone(basePlan, basePlan.Label);
                        draft.Path = smoothed;
                        PlannedTrajectory retimed = KvTrajMath.Retime(v, draft,
                            KvStageHub2.Current != null ? KvStageHub2.Current.Limits : new KvMotionLimits(),
                            1f, 1f, basePlan.Label, true);
                        plan = retimed != null ? retimed : basePlan;
                    }
                    break;
                }
            }

            task.timeMs = (Time.realtimeSinceStartup - t0) * 1000f;
            task.success = plan != null && plan.Path != null && plan.Path.Length >= 2;
            if (task.success)
            {
                task.lengthM = KvTrajMath.PathLength(v, plan.Path);
                task.limitMarginDeg = plan.LimitMargin;
                task.samples = plan.Path.Length;
                task.iterations = planner.LastIterations;
            }
            else
            {
                task.note = string.IsNullOrEmpty(planner.LastDebug) ? "путь не найден" : planner.LastDebug;
            }
            allTasks.Add(task);
            return task;
        }

        /// <summary>Выгрузить таблицу результатов в CSV и JSON (для защиты/презентации).</summary>
        public string Export()
        {
            if (results.Count == 0)
            {
                Report("выгружать нечего: бенчмарк ещё не выполнялся");
                return "";
            }
            string dir = FeatureStorage.EnsureDir(Path.Combine(FeatureStorage.Root, "Bench"));
            string stamp = FeatureStorage.TimeStamp();
            string csv = Path.Combine(dir, "planner_bench_" + stamp + ".csv");
            var sb = new StringBuilder();
            sb.Append("index;strategy;success;time_ms;length_m;margin_deg;samples;iterations;note\n");
            foreach (KvBenchTask t in allTasks)
            {
                sb.Append(t.index).Append(';').Append(t.strategy).Append(';')
                  .Append(t.success ? 1 : 0).Append(';')
                  .Append(t.timeMs.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(';')
                  .Append(t.lengthM.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(';')
                  .Append(t.limitMarginDeg.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(';').Append(t.samples).Append(';').Append(t.iterations)
                  .Append(';').Append(t.note.Replace('\n', ' ')).Append('\n');
            }
            foreach (KvBenchResult r in results)
                sb.Append("# ").Append(r.Line().Replace('\n', ' ')).Append('\n');

            try { File.WriteAllText(csv, sb.ToString(), new UTF8Encoding(false)); }
            catch (Exception e) { Report("CSV не записан: " + e.Message); return ""; }

            Report("таблица бенчмарка выгружена: " + csv);
            return csv;
        }

        // ================================================================== дерево RRT

        /// <summary>Показать/скрыть дерево RRT (ТЗ этапа 15: включается кнопкой).</summary>
        public void SetTreeVisible(bool value)
        {
            treeVisible = value;
            // ФИКС 3: запрос запоминается СТАТИЧЕСКИ и применяется и к текущему планировщику,
            // и к каждому следующему (при перепривязке робота поток создаёт новый Planner —
            // раньше на нём запись дерева молча выключалась).
            Planner.SetRecordTreeRequested(value);
            Planner planner = flow != null ? flow.Planner : null;
            if (planner != null) planner.RecordTree = value;
            if (!value)
            {
                if (treeRoot != null) treeRoot.gameObject.SetActive(false);
                Report("визуализация дерева RRT выключена");
                return;
            }
            if (treeRoot != null) treeRoot.gameObject.SetActive(true);
            Report("визуализация дерева RRT включена: узлы и рёбра деревьев от старта и от цели " +
                   "(планировщик записывает их при следующем планировании)");
        }

        public void ToggleTree()
        {
            SetTreeVisible(!treeVisible);
        }

        private void TickTree(float deltaTime)
        {
            treeTimer -= deltaTime;
            if (treeTimer > 0f) return;
            treeTimer = 0.1f;      // обновление 10 раз в секунду — «в реальном времени», но без затрат

            // Показ дерева — это трёхмерные линии: без графики (пакетный режим) запись в дерево
            // продолжает работать, а рисование пропускается, чтобы не сорвать цикл кадров.
            if (!KvGraphics.Available) return;

            Planner planner = flow != null ? flow.Planner : null;
            if (planner == null || flow.Validator == null || !flow.Validator.Ready) return;
            if (planner.TreeVersion == lastTreeVersion) return;
            lastTreeVersion = planner.TreeVersion;

            EnsureTreeRoot();
            if (treeLines == null) return;

            PoseValidator v = flow.Validator;
            var pts = new List<Vector3>();

            AddTree(pts, v, planner.TreeNodesA, planner.TreeParentsA);
            AddTree(pts, v, planner.TreeNodesB, planner.TreeParentsB);

            treeLines.positionCount = pts.Count;
            if (pts.Count > 0) treeLines.SetPositions(pts.ToArray());

            // ФИКС 4: пустое дерево — это НЕ «сломалось» и не повод оставить оператора
            // перед пустым экраном. Причина и действие называются прямо (в сцене рисовать
            // дерево нечего, поэтому сообщение идёт в строку состояния вкладки и в журнал).
            // ФИКС 3: причин ровно три (см. TreeStatus), и каждая называется своей формулировкой.
            if (pts.Count == 0)
            {
                if (!emptyTreeReported)
                {
                    emptyTreeReported = true;
                    string why;
                    if (planner.TreeVersion == 0 && !planner.TreeRecorded)
                        why = "запись дерева не велась — включите показ дерева " +
                              "(тумблер «Показывать дерево планировщика» или «Вид → Показать дерево»), " +
                              "затем постройте траекторию";
                    else if (!planner.TreeRecorded && planner.LastPathsFound > 0)
                        why = "путь найден напрямую (без RRT), поэтому дерево не строилось — " +
                              "это штатный результат, дерево появится на пути «через обход»";
                    else if (planner.TreeVersion == 0)
                        why = "после перепривязки робота дерево очищено: оно принадлежало прежнему роботу" +
                              " · постройте траекторию заново (выберите точку и подтвердите)";
                    else
                        why = "планировщик ещё не записал дерево · постройте траекторию заново";
                    Report(EmptyTreeText + " — узлов нет (" + why + ")");
                }
            }
            else
            {
                emptyTreeReported = false;
            }
        }

        private static void AddTree(List<Vector3> pts, PoseValidator v, List<double[]> nodes,
            List<int> parents)
        {
            if (nodes == null || parents == null) return;
            for (int i = 0; i < nodes.Count; i++)
            {
                int p = i < parents.Count ? parents[i] : -1;
                if (p < 0 || p >= nodes.Count) continue;
                pts.Add(v.TcpAt(nodes[p]));
                pts.Add(v.TcpAt(nodes[i]));
            }
        }

        private void EnsureTreeRoot()
        {
            if (treeRoot != null) return;
            GameObject go = new GameObject("KvRrtTree");
            go.hideFlags = HideFlags.HideInHierarchy;
            treeRoot = go.transform;
            treeLines = go.AddComponent<LineRenderer>();
            treeLines.useWorldSpace = true;
            treeLines.numCapVertices = 0;
            treeLines.widthMultiplier = 0.0035f;
            Material treeMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLine(
                "Материал_дерева_RRT", Color.white);
            if (treeMaterial != null)
            {
                treeLines.material = treeMaterial;
                treeLines.startColor = new Color(0.35f, 1f, 0.75f, 0.85f);
                treeLines.endColor = new Color(1f, 0.6f, 0.15f, 0.35f);
            }
        }

        /// <summary>
        /// Строка состояния для вкладки.
        /// ФИКС 4: пустое дерево (после перепривязки робота `TreeVersion = 0`) больше не
        /// выглядит как «ничего нет» — оператор видит прямое указание, что делать.
        /// </summary>
        public string Status()
        {
            Planner planner = flow != null ? flow.Planner : null;
            string tree = planner != null ? TreeStatus(planner) : "планировщик недоступен";
            if (running) return "прогон: " + cursor + "/" + totalTasks;
            if (results.Count > 0) return "последний прогон: " + results.Count + " стратегий · " + tree;
            return tree;
        }

        /// <summary>
        /// ФИКС 4. Короткий и честный статус дерева RRT: узлов, версия — а если дерево ПУСТО
        /// (узлов нет вовсе или версия 0 после перепривязки робота), вместо пустого экрана
        /// оператор получает «дерево пусто, выполните планирование».
        ///
        /// ФИКС 3 (этой сессии). Пустое дерево бывает по ТРЁМ разным причинам, и раньше все три
        /// выглядели одинаково:
        ///   1) показ дерева не включали — запись вообще не велась (`TreeRecorded == false`);
        ///   2) путь найден НАПРЯМУЮ (`PlanToGoal`/`PlanViaWaypoint` — там дерево не записывается),
        ///      поэтому узлов нет, хотя планирование прошло;
        ///   3) дерево очищено при перепривязке робота (`TreeVersion == 0`).
        /// Статус называет причину прямо — оператор видит, что делать, а не «пусто».
        /// </summary>
        public static string TreeStatus(Planner planner)
        {
            if (planner == null) return "планировщик недоступен";

            int nodes = planner.TreeNodesA.Count + planner.TreeNodesB.Count;
            if (nodes > 0 && planner.TreeVersion != 0)
                return "дерево: узлов A " + planner.TreeNodesA.Count + " · B " +
                       planner.TreeNodesB.Count + " · версия " + planner.TreeVersion +
                       (planner.TreeSolved ? " · путь найден" : "");

            if (planner.TreeVersion == 0 && !planner.TreeRecorded)
                return EmptyTreeText + " — запись не велась: включите показ дерева планировщика";

            if (!planner.TreeRecorded && planner.LastPathsFound > 0)
                return DirectPathText;

            return EmptyTreeText;
        }

        /// <summary>
        /// ФИКС 3. Честная строка для случая «путь найден напрямую»: RRT в этом проходе не
        /// строил дерево (режим обхода целиком в пространстве суставов), поэтому узлов нет —
        /// это НЕ провал планирования и НЕ поломка визуализации.
        /// </summary>
        public const string DirectPathText =
            "дерево не строилось — путь найден напрямую";

        /// <summary>Текст для пустого дерева (одно место — им пользуются и вкладка, и оверлей).</summary>
        public const string EmptyTreeText =
            "дерево пусто, выполните планирование";

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[PlannerLab] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «БЕНЧМАРК И ДЕРЕВО RRT» (ЭТАПЫ 14–15 ТЗ).
    /// </summary>
    public class KvPlannerLabTab : IKvWorkbenchTab
    {
        private readonly KvPlannerLab lab;

        public KvPlannerLabTab(KvPlannerLab service)
        {
            lab = service;
        }

        public string Key { get { return "planner"; } }
        public string Title { get { return KvLocExtra3.T("bench.title", "Бенчмарк планировщика"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra3.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (lab == null || kit == null) return;

            kit.Section(Title);
            kit.Slider(T("bench.tasks", "Задач в прогоне"), 5f, 300f, lab.TasksPerRun, "0",
                delegate (float v) { lab.TasksPerRun = Mathf.RoundToInt(v); });

            kit.Buttons(new[]
            {
                T("bench.start", "Запустить прогон"),
                T("bench.stop", "Остановить"),
                T("bench.export", "Выгрузить таблицу (CSV)")
            }, new Action[]
            {
                delegate { lab.Start(); },
                delegate { lab.Stop(); },
                delegate { lab.Export(); }
            });

            kit.Info(delegate { return lab.Status(); }, KvTheme.Accent);

            kit.Section(T("bench.table", "Результаты"));
            kit.Table("", delegate { return T("bench.strategy", "Стратегия"); },
                delegate { return T("bench.values", "Успех · время · длина"); });
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                kit.Table("#" + (index + 1),
                    delegate
                    {
                        KvBenchResult r = index < lab.Results.Count ? lab.Results[index] : null;
                        return r != null
                            ? r.StrategyLabel
                            : ((KvPlanStrategy)index == KvPlanStrategy.RrtStar
                                ? "RRT* (анитайм)"
                                : (KvPlanStrategy)index == KvPlanStrategy.TrajOpt
                                    ? "TrajOpt (пост-оптимизация)" : "BiRRT (штатный)");
                    },
                    delegate
                    {
                        KvBenchResult r = index < lab.Results.Count ? lab.Results[index] : null;
                        if (r == null) return "—";
                        return (r.SuccessRate * 100f).ToString("0") + " % · " +
                               r.avgTimeMs.ToString("0.0") + " мс · " +
                               r.avgLengthM.ToString("0.000") + " м";
                    });
            }

            kit.Divider();
            kit.Section(T("rrt.title", "Дерево RRT"));
            kit.Toggle(T("rrt.show", "Показывать дерево планировщика в сцене"),
                lab.TreeVisible, delegate (bool v) { lab.SetTreeVisible(v); });
            kit.Note(T("bench.info",
                "BiRRT — штатный планировщик проекта; RRT* (анитайм) — несколько перезапусков и выбор " +
                "самого короткого пути; TrajOpt — сокращение, сглаживание и перепараметризация пути по " +
                "лимитам. Прогон идёт порциями, поэтому интерфейс не замирает."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
