using System.Collections.Generic;
using UnityEngine;
using KazistovVvFeatures;

namespace TrajectoryCore
{
    /// <summary>Готовая траектория: сэмплы q(t) + метрики.</summary>
    public class PlannedTrajectory
    {
        public double[][] Path;      // сэмплы конфигураций
        public float[] Times;        // время каждого сэмпла, с
        public string Label = "";
        public double Time;          // полное время, с
        public double Length;        // нормированная длина пути
        public float MinClearance;   // минимальный зазор, м
        public float LimitMargin;    // минимальный запас до лимитов, град
        public double SigmaMin;      // оценка σ_min в цели
        public double Score;         // итоговый (меньше — лучше)

        /// <summary>
        /// Метка источника траектории — ветвь IK (`S-,E+,W-`) или вариация seed'а планировщика
        /// (`seed 1000+…`). Нужна оператору (подпись «колбаски») и отчёту: по ней видно,
        /// чем именно 8 вариантов отличаются друг от друга.
        /// </summary>
        public string BranchTag = "";

        /// <summary>
        /// Средняя «кривизна» пути в нормированном пространстве суставов — средний угол
        /// поворота направления движения между сэмплами (град). 0 — путь прямой;
        /// больше — путь «петляет». Один из критериев сортировки вариантов (ТЗ: длина, время, кривизна).
        /// </summary>
        public double Curvature;

        /// <summary>
        /// СУММАРНАЯ кривизна пути (град) = сумма углов поворота направления между сэмплами.
        /// Это «сколько всего путь петляет»: у прямого пути ≈0, у зигзага — сотни градусов.
        /// Метрика панели траекторий (ЗАДАЧА 5 ТЗ).
        /// </summary>
        public double CurvatureTotal;

        /// <summary>МАКСИМАЛЬНАЯ кривизна (град) — самый резкий перелом направления на пути.</summary>
        public double CurvatureMax;

        /// <summary>Сколько сэмплов участвовало в оценке кривизны (для интерпретации суммы).</summary>
        public int CurvatureSamples;

        public double[] GoalQ => Path != null && Path.Length > 0 ? Path[Path.Length - 1] : null;
    }

    /// <summary>
    /// Планировщик (E2–E3 плана): BiRRT-Connect в пространстве обобщённых координат
    /// с проверкой столкновений и лимитов, сглаживание short-cut, параметризация
    /// времени (трапеция по лимитам скоростей) и скоринг нескольких кандидатов.
    /// Детерминирован: один seed → один результат.
    /// </summary>
    public class Planner
    {
        // ПОРОГ ЗАЗОРА ПЛАНИРОВЩИКА БЕРЁТСЯ ИЗ ОДНОГО МЕСТА (P1-16, 11.11.2026).
        // ЧТО БЫЛО: здесь стояло своё число 0,02, а предохранитель проверял по 0,015 — план,
        // посчитанный по одному порогу, отклонялся (или пропускался) по другому.
        // Теперь число и коэффициент живут в `SafetyGate`: один источник, явный запас.
        public float clearance = SafetyGate.DefaultPlanningClearance;
        public float selfClearance = 0.015f;   // запас между своими звеньями
        public float minLimitMarginDeg = 3f;   // как SafetyGate.minLimitMarginDeg
        public int maxIterations = 2500;
        public double stepSizeDeg = 12.0;
        public float timeStep = 0.02f;      // шаг сэмплирования траектории
        public float accelShare = 0.25f;    // доля времени на разгон/торможение

        /// <summary>
        /// ФИКС 2 (§20). НАБОР ЛИМИТОВ ДВИЖЕНИЯ планировщика — тот же объект, что у
        /// постобработки (сглаживание / время-оптимальная / эко-профиль, см. `KvStageHub2.Limits`),
        /// поэтому время планировщика и время постобработки считаются по ОДНИМ пределам
        /// и совпадают. По умолчанию — типовые значения `KvMotionLimits` (90 °/с / 180 °/с² /
        /// 1200 °/с³), если хаб этапов не привязал общий набор.
        /// </summary>
        public KvMotionLimits MotionLimits { get; set; } = new KvMotionLimits();

        // Веса скоринга
        public double wTime = 1.0, wLength = 0.6, wClearance = 2.0, wLimit = 0.5, wSigma = 0.5;
        public double wPosture = 0.05;      // вклад стоимости позы (PostureSelector)
        public double wCurvature = 0.35;    // вклад кривизны пути (ТЗ: сортировка по длине/времени/кривизне)

        private PoseValidator v;
        private CollisionWorld world;
        private IkSolver ik;
        private PostureSelector posture;
        private double[] ranges;

        public bool Ready { get; private set; }
        public int LastIterations { get; private set; }
        public string LastDebug { get; private set; } = "";

        public void Init(PoseValidator validator, CollisionWorld collisionWorld)
        {
            v = validator;
            world = collisionWorld;
            // ФИКС 3: новый планировщик сразу подхватывает последний запрос показа дерева —
            // иначе после перепривязки робота показ оставался включён, а узлы не записывались.
            RecordTree = RecordTreeRequested;
            Ready = v != null && v.Ready && world != null;
            if (!Ready) return;
            ranges = new double[v.Dof];
            for (int i = 0; i < v.Dof; i++)
            {
                double r = v.Upper[i] - v.Lower[i];
                ranges[i] = r > 1e-6 ? r : 1.0;
            }
            ik = new IkSolver();
            ik.Init(v);
            posture = new PostureSelector();
            posture.Init(v);
        }

        /// <summary>Планирование до точки; возвращает до maxCandidates вариантов (отсортированы).</summary>
        public List<PlannedTrajectory> Plan(double[] start, Vector3 goalPoint, int maxCandidates, int seed)
        {
            var result = new List<PlannedTrajectory>();
            if (!Ready || start == null) return result;

            // 1. Конфигурации цели (аналитика IK + CCD-резерв) — единый источник правды,
            //    тот же список использует поток «8 траекторий» (SolveGoalConfigs).
            List<GoalConfig> goals = SolveGoalConfigs(start, goalPoint, maxCandidates + 1, seed);
            if (goals.Count == 0)
            {
                LastDebug = "IK: решения не найдены (ошибка " + (v.LastIkError * 1000f).ToString("0") + " мм)";
                return result;
            }

            int pathsFound = 0, filteredOut = 0;
            // 2. BiRRT для каждой конфигурации цели.
            for (int gi = 0; gi < goals.Count; gi++)
            {
                double[] goal = goals[gi].q;
                List<double[]> path = PlanBiRrt(start, goal, seed);
                if (path == null || path.Count < 2) continue;
                pathsFound++;
                path = MakeContinuous(path);
                path = Shortcut(path, seed);
                PlannedTrajectory t = BuildTrajectory(path, goalPoint);
                if (t == null) continue;
                if (t.MinClearance < 0f) { filteredOut++; continue; } // столкновение
                // Источник варианта (ветвь IK / CCD) — в подпись «колбаски» и в отчёт.
                t.BranchTag = goals[gi].tag;
                result.Add(t);
                if (result.Count >= maxCandidates * 2) break;
            }
            LastDebug = "IK-решений=" + goals.Count + ", путей=" + pathsFound +
                        ", отброшено по коллизии=" + filteredOut + ", кандидатов=" + result.Count;
            LastPathsFound = pathsFound;

            // 3. Скоринг и отбор.
            foreach (PlannedTrajectory t in result) t.Score = Score(t);
            result.Sort((a, b) => a.Score.CompareTo(b.Score));
            for (int i = 0; i < result.Count && i < maxCandidates; i++)
                result[i].Label = "Вариант " + (i + 1) + (i == 0 ? " (оптимальный)" : "");
            if (result.Count > maxCandidates) result.RemoveRange(maxCandidates, result.Count - maxCandidates);
            return result;
        }

        /// <summary>Конфигурация цели для планирования: вектор q + метка источника («IK S-,E+» / «CCD»).</summary>
        public struct GoalConfig
        {
            public double[] q;
            public string tag;
        }

        /// <summary>
        /// ВСЕ конфигурации цели, в которые планировщик готов вести робота (единый источник
        /// правды для `Plan` и для потока «8 траекторий»):
        ///   1) аналитические ветви IK — 6-осевой: до 8 (плечо влево/вправо × локоть вверх/вниз ×
        ///      запястье с переворотом/без), SCARA: до 4 (вылет вперёд/назад × локоть вверх/вниз);
        ///      ветви ранжируются селектором позы и дедуплицируются по конфигурации;
        ///   2) CCD-резерв — если аналитика не дала решений. Для SCARA это ОСНОВНОЙ путь:
        ///      её аналитика строит z_5 только в пределах хода призмы, поэтому точка на столе
        ///      (ниже хода) даёт пустой список, а конфигурации находятся CCD с доводкой высоты.
        /// </summary>
        public List<GoalConfig> SolveGoalConfigs(double[] start, Vector3 goalPoint, int maxCount, int seed)
        {
            var configs = new List<GoalConfig>();
            if (!Ready || start == null) return configs;

            var goals = new List<double[]>();
            var goalTags = new List<string>();
            // Аналитические ветви + CCD-добор до maxCount (у 6-осевого аналитика часто даёт
            // 1–3 ветви, остальные конфигурации находятся CCD-сидами; у SCARA аналитика вообще
            // не проходит лимиты на точке уровня стола — см. CCD-резерв ниже).
            List<IkSolution> branches = ik != null && ik.Ready
                ? ik.SolveAllSeeded(goalPoint, start, maxCount)
                : new List<IkSolution>();
            int analytic = Mathf.Max(0, ik != null ? ik.LastAnalyticCount : 0);

            // Ранжируем ветви селектором позы (лимиты + home + сингулярность + непрерывность).
            branches.Sort((a, b) => PostureCost(a, start).CompareTo(PostureCost(b, start)));
            for (int bi = 0; bi < branches.Count; bi++)
            {
                IkSolution s = branches[bi];
                if (!s.withinLimits) continue;
                bool dup = false;
                foreach (double[] ex in goals)
                    if (ConfigDistance(ex, s.q) < 0.02) { dup = true; break; }
                if (dup) continue;
                goals.Add(s.q);
                goalTags.Add(bi < analytic ? s.tag.ToString() : "CCD " + s.tag);
                if (goals.Count >= maxCount) break;
            }

            // CCD-резерв, если аналитика не дала решений (или робот SCARA).
            if (goals.Count == 0)
            {
                for (int k = 0; k < 4; k++)
                {
                    double[] s = (double[])start.Clone();
                    var rng = new System.Random(seed + k * 977);
                    for (int i = 0; i < v.Dof; i++)
                    {
                        if (k == 0) continue;
                        s[i] += (rng.NextDouble() * 2.0 - 1.0) * ranges[i] * 0.25;
                    }
                    if (!v.WithinLimits(s)) continue;
                    if (v.SolveIk(goalPoint, s, out double[] qg, 120, 0.008f) && v.WithinLimits(qg))
                    {
                        bool dup = false;
                        foreach (double[] ex in goals)
                            if (ConfigDistance(ex, qg) < 0.02) { dup = true; break; }
                        if (!dup) { goals.Add(qg); goalTags.Add("CCD"); }
                    }
                    if (goals.Count >= maxCount) break;
                }
            }

            // ИСПРАВЛЕНО 15.09.2026: строка о ветвях заполняется ПОСЛЕ CCD-резерва. Раньше она
            // считалась до него, и когда аналитика не давала решений, а CCD находил конфигурации,
            // оператору (и отчёту) показывалось «нет ветвей» при двух реально найденных ветвях.
            LastBranchInfo = goalTags.Count > 0 ? string.Join(" | ", goalTags.ToArray()) : "нет ветвей";

            for (int i = 0; i < goals.Count; i++)
                configs.Add(new GoalConfig { q = goals[i], tag = i < goalTags.Count ? goalTags[i] : "" });
            return configs;
        }

        /// <summary>
        /// ГЛУБИНА ОБХОДА (доля хода сустава), перебираемая по кругу вариантами набора
        /// «8 траекторий»: 0.06 — «микро-отклонение» waypoint'а (почти прямой путь с горбинкой
        /// в середине), 0.44 — рука уходит далеко в сторону. Именно перебор глубин и направлений
        /// даёт РАЗНЫЕ по форме пути: один и тот же seed без этого выпрямляется в одну прямую
        /// и все варианты отбрасываются как дубликаты (`IsSamePath`).
        /// </summary>
        private static readonly double[] ViaDeviations = { 0.06, 0.10, 0.16, 0.22, 0.30, 0.38, 0.44 };

        /// <summary>
        /// Путь ЧЕРЕЗ ПРОМЕЖУТОЧНУЮ КОНФИГУРАЦИЮ («обход») — вариация маршрута для набора
        /// «8 траекторий» (ТЗ: «разные seed'ы планировщика — если IK-конфигураций не хватает»).
        ///
        /// Зачем: в свободном пространстве BiRRT + short-cut всегда выпрямляет путь в прямую
        /// в пространстве суставов, поэтому 8 разных seed'ов дают ОДНУ И ТУ ЖЕ траекторию
        /// (и она отбрасывается как дубликат). Здесь маршрут строится явно в два участка
        /// start → via → goal со случайной допустимой промежуточной позой в стороне от прямой:
        /// рука идёт другим путём, и альтернатива действительно видна оператору.
        ///
        /// СЕССИЯ «8 ТРАЕКТОРИЙ ДЛЯ SCARA» (15.09.2026): у трёхосевой SCARA конфигураций IK всего
        /// 2–4, а свободного пространства вокруг точки много — поэтому почти все seed-вариации
        /// вырождались в прямую и набор доходил до 6 из 8. Теперь у каждой попытки СВОЯ глубина
        /// обхода (`variant`) и свой seed, а число попыток обхода увеличено с 4 до 12.
        ///
        /// Каждый участок — обычный BiRRT со своим seed, каждое ребро проверено `Free`
        /// (лимиты + запас до мира + самозазор), поэтому обход так же безопасен, как прямой путь.
        /// Возврат `null`, если обход не удался, — вызывающий обязан иметь резервный вариант.
        /// </summary>
        /// <param name="viaTries">Сколько промежуточных поз перебрать, прежде чем сдаться.</param>
        /// <param name="variant">
        /// Номер варианта набора (0…N): задаёт СТАРТОВУЮ глубину обхода из <see cref="ViaDeviations"/>
        /// и сдвигает поток случайных чисел, поэтому разные варианты дают разные формы пути.
        /// </param>
        /// <param name="requireGoalMargin">
        /// Требовать запас лимитов В ЦЕЛИ (по умолчанию — да, как и раньше). `false` нужен для
        /// «краевых» точек SCARA: призма z_5 приходит РОВНО в свой предел, запас цели меньше 3°,
        /// строгая проверка отклоняет такую ветвь — и тогда все seed-вариации вырождаются в один
        /// прямой путь (в прогоне 15.09.2026 это дало 6 попыток без единого обхода и 4 уникальных
        /// из 8). Свободный `Plan` такие цели берёт (он проверяет только `WithinLimits`), поэтому
        /// и обход к ним допустим; запас каждого ребра обхода по-прежнему проверяет
        /// <see cref="Free"/>, а сама траектория честно помечается «не прошла SafetyGate»
        /// в списке вариантов (`TrajectoryCandidate.safe`).
        /// </param>
        public PlannedTrajectory PlanViaWaypoint(double[] start, double[] goalQ, int seed, string tag,
                                                 int viaTries = 12, int variant = 0,
                                                 bool requireGoalMargin = true)
        {
            if (!Ready || start == null || goalQ == null) return null;
            if (!v.WithinLimits(goalQ)) return null;
            if (requireGoalMargin && v.LimitMargin(goalQ) < minLimitMarginDeg) return null;

            var rng = new System.Random(seed * 17 + 3 + variant * 104729);
            for (int k = 0; k < viaTries; k++)
            {
                // Глубина обхода — из таблицы по (variant + k): каждый вариант набора уходит
                // в свою сторону и на свою глубину, повторов формы пути не возникает.
                double deviation = ViaDeviations[(variant + k) % ViaDeviations.Length];

                // Промежуточная поза: около середины прямого пути, но заметно в стороне.
                double[] mid = Interp(start, goalQ, 0.25 + 0.50 * rng.NextDouble());
                var via = (double[])mid.Clone();
                for (int i = 0; i < v.Dof; i++)
                {
                    via[i] += (rng.NextDouble() * 2.0 - 1.0) * ranges[i] * deviation;
                    // Мягкий кламп в лимиты: обход обязан быть ДОПУСТИМЫМ (иначе попытка тратится
                    // зря), но не «прилипает» к границе — запас до лимитов проверяет Free().
                    double lo = v.Lower[i] + 0.03 * ranges[i];
                    double hi = v.Upper[i] - 0.03 * ranges[i];
                    if (lo < hi) via[i] = System.Math.Min(hi, System.Math.Max(lo, via[i]));
                }
                if (!Free(via, out _)) continue;
                // Обход должен РЕАЛЬНО уводить в сторону, иначе это дубликат прямого пути.
                // Порог соразмерён глубине: у «микро-отклонения» он меньше (но всё равно
                // заметно больше допуска IsSamePath ≈4° на сустав).
                double[] center = Interp(start, goalQ, 0.5);
                if (ConfigDistance(via, center) < System.Math.Max(0.05, deviation * 0.8)) continue;

                List<double[]> a = PlanBiRrt(start, via, seed + k * 131);
                if (a == null || a.Count < 2) continue;
                List<double[]> b = PlanBiRrt(via, goalQ, seed + k * 131 + 7);
                if (b == null || b.Count < 2) continue;

                // Участки сглаживаются ПООТДЕЛЬНОСТИ: общий short-cut «срезал» бы точку обхода
                // и вернул прямой путь (именно это и происходит с обычными seed-вариациями).
                a = Shortcut(MakeContinuous(a), seed + k);
                b = Shortcut(MakeContinuous(b), seed + k + 101);
                a.AddRange(b);

                PlannedTrajectory t = BuildTrajectory(a, v.TcpAt(goalQ));
                if (t == null || t.MinClearance < 0f) continue;
                t.BranchTag = tag;
                t.Score = Score(t);
                return t;
            }
            return null;
        }

        /// <summary>
        /// Планирование К КОНКРЕТНОЙ КОНФИГУРАЦИИ ЦЕЛИ (ветви IK) — ЗАДАЧА 1 ТЗ «8 траекторий»:
        /// одна ветвь (плечо/локоть/запястье) → ровно ОДИН путь BiRRT с заданным seed'ом.
        /// Так 8 ветвей дают 8 РАЗНЫХ траекторий (робот приходит в точку разными позами),
        /// в отличие от `Plan`, который сам решает, сколько ветвей взять.
        /// Возврат `null` — путь не найден/ветвь непригодна (лимиты, столкновение).
        /// </summary>
        public PlannedTrajectory PlanToGoal(double[] start, double[] goalQ, int seed, string tag = "")
        {
            if (!Ready || start == null || goalQ == null) return null;
            if (goalQ.Length < v.Dof) return null;
            if (!v.WithinLimits(goalQ)) return null;
            if (v.LimitMargin(goalQ) < minLimitMarginDeg) return null;

            List<double[]> path = PlanBiRrt(start, goalQ, seed);
            if (path == null || path.Count < 2) return null;
            path = MakeContinuous(path);
            path = Shortcut(path, seed);
            PlannedTrajectory t = BuildTrajectory(path, v.TcpAt(goalQ));
            if (t == null || t.MinClearance < 0f) return null;
            t.BranchTag = tag;
            t.Score = Score(t);
            return t;
        }

        private double Score(PlannedTrajectory t)
        {
            double clearPenalty = t.MinClearance <= 0.001 ? 1000.0 : wClearance / t.MinClearance;
            double limitPenalty = t.LimitMargin <= 0.5 ? 500.0 : wLimit * (30.0 / t.LimitMargin);
            double sigmaPenalty = t.SigmaMin <= 1e-6 ? 200.0 : wSigma * (0.05 / t.SigmaMin);
            double posturePenalty = posture != null && posture.Ready && t.GoalQ != null
                ? wPosture * posture.Cost(t.GoalQ, null) : 0.0;
            return wTime * t.Time + wLength * t.Length + clearPenalty + limitPenalty +
                   sigmaPenalty + posturePenalty + wCurvature * t.Curvature;
        }

        private double PostureCost(IkSolution s, double[] qPrev)
        {
            return posture != null && posture.Ready ? posture.Cost(s.q, qPrev) : 0.0;
        }

        public string LastBranchInfo { get; private set; } = "";

        // ================================================================== ЭТАП 15: дерево RRT
        // Запись узлов дерева для визуализации работы планировщика (ТЗ «показать дерево RRT
        // в реальном времени»). Включение флага НЕ меняет ни одной ветви алгоритма: узлы
        // только копируются в списки, чтобы их можно было нарисовать. Каждый вызов
        // PlanBiRrt начинает новую запись, поэтому видно текущее (последнее) дерево.

        /// <summary>
        /// Включить запись дерева (ставит визуализатор этапа 15).
        ///
        /// ФИКС 3 (сессия §19). Раньше это было СТАТИЧЕСКОЕ поле, а флаг ставил визуализатор
        /// у ТОГО планировщика, который был у потока В ТОТ МОМЕНТ. При любой перепривязке робота
        /// поток создаёт НОВЫЙ `Planner` (`RebindKinematics`, смена робота) — и на новом объекте
        /// запись уже не велась, хотя тумблер «Показывать дерево планировщика» оставался включён.
        /// Отсюда «версия 0 · узлов A/B: 0/0» при живом показе дерева. Теперь поле — СВОЙСТВО
        /// объекта, а последнее выставленное ЗНАЧЕНИЕ запоминается статически
        /// (<see cref="RecordTreeRequested"/>) и применяется к каждому новому планировщику
        /// (<see cref="Init"/>). Алгоритм планирования не меняется: флаг только копирует узлы.
        /// </summary>
        public bool RecordTree { get; set; }

        /// <summary>
        /// Последнее значение, выставленное визуализатором (`KvPlannerLab.SetTreeVisible`).
        /// Новый планировщик получает его в <see cref="Init"/>, поэтому показ дерева не теряется
        /// при перепривязке робота.
        /// </summary>
        public static bool RecordTreeRequested { get; private set; }

        /// <summary>Запомнить запрос показа дерева (вызывает визуализатор этапа 15).</summary>
        public static void SetRecordTreeRequested(bool value)
        {
            RecordTreeRequested = value;
        }

        /// <summary>Узлы дерева A (от старта) — конфигурации суставов.</summary>
        public readonly List<double[]> TreeNodesA = new List<double[]>();
        /// <summary>Индексы родителей узлов дерева A (−1 у корня).</summary>
        public readonly List<int> TreeParentsA = new List<int>();
        /// <summary>Узлы дерева B (от цели).</summary>
        public readonly List<double[]> TreeNodesB = new List<double[]>();
        /// <summary>Индексы родителей узлов дерева B (−1 у корня).</summary>
        public readonly List<int> TreeParentsB = new List<int>();
        /// <summary>Счётчик записанных версий дерева (для интерфейса: «дерево обновилось»).</summary>
        public int TreeVersion { get; private set; }
        /// <summary>Последняя запись закончилась найденным путём.</summary>
        public bool TreeSolved { get; private set; }
        /// <summary>Итераций в последней записи.</summary>
        public int TreeIterations { get; private set; }
        /// <summary>
        /// ФИКС 3. Запись дерева ВООБЩЕ включалась (`RecordTree`) и хотя бы один раз начиналась.
        /// Нужна, чтобы отличать «дерево не строили» от «дерево построено, но случайно пусто»:
        /// в отчёте и в диагностике это РАЗНЫЕ состояния, и путать их нельзя.
        /// </summary>
        public bool TreeRecorded { get; private set; }
        /// <summary>
        /// ФИКС 3. Сколько путей планировщик отдал в ПОСЛЕДНЕМ `Plan`: если больше нуля, а узлов
        /// нет — путь был найден напрямую (`PlanToGoal`/`PlanViaWaypoint` не записывают дерево),
        /// и оператор получает честное объяснение вместо пустого экрана.
        /// </summary>
        public int LastPathsFound { get; private set; }

        private void BeginTreeRecording(double[] start, double[] goal)
        {
            TreeNodesA.Clear();
            TreeParentsA.Clear();
            TreeNodesB.Clear();
            TreeParentsB.Clear();
            TreeVersion++;
            TreeRecorded = true;
            TreeSolved = false;
            TreeIterations = 0;
            if (start != null)
            {
                TreeNodesA.Add((double[])start.Clone());
                TreeParentsA.Add(-1);
            }
            if (goal != null)
            {
                TreeNodesB.Add((double[])goal.Clone());
                TreeParentsB.Add(-1);
            }
        }

        /// <summary>
        /// ФИКС 4. ОЧИСТИТЬ запись дерева и сбросить <see cref="TreeVersion"/> в 0.
        /// Вызывается потоком при ПЕРЕПРИВЯЗКЕ робота: дерево принадлежало прежнему роботу
        /// (другая размерность, другие лимиты), показывать его оператору нельзя, а пустой
        /// экран без объяснения выглядел как «сломалось». Алгоритм планирования не меняется —
        /// очищаются только списки визуализации.
        /// </summary>
        public void ClearTreeRecording()
        {
            TreeNodesA.Clear();
            TreeParentsA.Clear();
            TreeNodesB.Clear();
            TreeParentsB.Clear();
            TreeVersion = 0;
            TreeSolved = false;
            TreeIterations = 0;
            TreeRecorded = false;      // ФИКС 3: записи нет — статус скажет «дерево не строилось»
            LastPathsFound = 0;
        }

        private void RecordTreeNode(bool treeB, double[] q, int parent)
        {
            if (treeB)
            {
                TreeNodesB.Add(q);
                TreeParentsB.Add(parent);
            }
            else
            {
                TreeNodesA.Add(q);
                TreeParentsA.Add(parent);
            }
            TreeIterations++;
        }

        /// <summary>
        /// Нормированное расстояние между конфигурациями. Для вращательных осей берётся
        /// КРАТЧАЙШИЙ доворот (по модулю 360°), иначе базовый сустав SCARA «наворачивает»
        /// лишние 300+ градусов — траектория становится заведомо невыгодной.
        /// </summary>
        private double ConfigDistance(double[] a, double[] b)
        {
            double s = 0;
            for (int i = 0; i < v.Dof; i++)
            {
                double d = Delta(i, a[i], b[i]) / ranges[i];
                s += d * d;
            }
            return System.Math.Sqrt(s);
        }

        /// <summary>Кратчайшая разница по оси i (призматическая — обычная разность).</summary>
        private double Delta(int i, double from, double to)
        {
            if (v.IsPrismatic(i)) return to - from;
            return Mathf.DeltaAngle((float)from, (float)to);
        }

        /// <summary>Точка на кратчайшем пути a→b (промежуточные значения могут выйти
        /// за номинал лимитов, но остаются эквивалентным допустимым углом).</summary>
        private double[] Interp(double[] a, double[] b, double k)
        {
            var q = new double[v.Dof];
            for (int i = 0; i < v.Dof; i++) q[i] = a[i] + Delta(i, a[i], b[i]) * k;
            return q;
        }

        /// <summary>Разворачивает путь в непрерывную последовательность (без скачков ±360°).</summary>
        private List<double[]> MakeContinuous(List<double[]> path)
        {
            for (int j = 1; j < path.Count; j++)
            {
                double[] prev = path[j - 1];
                double[] cur = path[j];
                var q = new double[v.Dof];
                for (int i = 0; i < v.Dof; i++) q[i] = prev[i] + Delta(i, prev[i], cur[i]);
                path[j] = q;
            }
            return path;
        }

        private bool Free(double[] q, out float clearanceOut)
        {
            clearanceOut = -1f;
            if (!v.WithinLimits(q)) return false;
            // Запас до лимитов не меньше, чем требует SafetyGate: планировщик не должен
            // выдавать траекторию, которую исполнитель потом отклонит.
            if (v.LimitMargin(q) < minLimitMarginDeg) return false;
            // самоколлизия собственных звеньев
            float self = v.SelfClearance(q, out _, out _);
            if (self < selfClearance) { clearanceOut = self; return false; }
            clearanceOut = v.ClearanceAt(q, world, out _, out _);
            return clearanceOut >= clearance;
        }

        private double[] Steer(double[] from, double[] to, double stepNorm)
        {
            double dist = ConfigDistance(from, to);
            if (dist <= stepNorm) return (double[])to.Clone();
            double k = stepNorm / dist;
            return Interp(from, to, k);
        }

        private List<double[]> PlanBiRrt(double[] start, double[] goal, int seed)
        {
            var rng = new System.Random(seed * 31 + 7);
            var treeA = new List<double[]> { (double[])start.Clone() };
            var treeB = new List<double[]> { (double[])goal.Clone() };
            var parentA = new List<int> { -1 };
            var parentB = new List<int> { -1 };
            double step = stepSizeDeg / 90.0; // нормированный шаг

            // ЭТАП 15 ТЗ: запись дерева RRT для визуализации (только чтение/накопление,
            // на планирование не влияет — включается флагом RecordTree).
            if (RecordTree) BeginTreeRecording(start, goal);

            LastIterations = 0;
            for (int it = 0; it < maxIterations; it++)
            {
                LastIterations = it;
                double[] qRand = Sample(rng);
                if (it % 4 == 0) qRand = (double[])treeB[treeB.Count - 1].Clone(); // goal bias

                int ia = Nearest(treeA, qRand);
                double[] qNew = Steer(treeA[ia], qRand, step);
                if (!Free(qNew, out _)) continue;
                treeA.Add(qNew); parentA.Add(ia);
                if (RecordTree) RecordTreeNode(false, qNew, ia);

                int ib = Nearest(treeB, qNew);
                double[] qConn = Steer(treeB[ib], qNew, step);
                if (!Free(qConn, out _)) continue;
                treeB.Add(qConn); parentB.Add(ib);
                if (RecordTree) RecordTreeNode(true, qConn, ib);

                if (ConfigDistance(qNew, qConn) < step * 1.5)
                {
                    // Сшиваем: A → qNew → qConn → B
                    var pathA = Extract(treeA, parentA, treeA.Count - 1);
                    var pathB = Extract(treeB, parentB, treeB.Count - 1);
                    pathB.Reverse();
                    pathA.AddRange(pathB);
                    if (RecordTree) TreeSolved = true;
                    return pathA;
                }
            }
            return null;
        }

        private double[] Sample(System.Random rng)
        {
            var q = new double[v.Dof];
            for (int i = 0; i < v.Dof; i++)
                q[i] = v.Lower[i] + rng.NextDouble() * (v.Upper[i] - v.Lower[i]);
            return q;
        }

        private int Nearest(List<double[]> tree, double[] q)
        {
            int best = 0;
            double bestD = double.MaxValue;
            for (int i = 0; i < tree.Count; i++)
            {
                double d = ConfigDistance(tree[i], q);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private static List<double[]> Extract(List<double[]> tree, List<int> parent, int idx)
        {
            var path = new List<double[]>();
            while (idx >= 0)
            {
                path.Add(tree[idx]);
                idx = parent[idx];
            }
            path.Reverse();
            return path;
        }

        /// <summary>Short-cut сглаживание: выкидываем промежуточные точки, если прямая свободна.</summary>
        private List<double[]> Shortcut(List<double[]> path, int seed)
        {
            var rng = new System.Random(seed * 131 + 17);
            for (int attempt = 0; attempt < path.Count * 3; attempt++)
            {
                if (path.Count < 3) break;
                int i = rng.Next(0, path.Count - 1);
                int j = rng.Next(i + 1, path.Count);
                if (j - i < 2) continue;
                if (SegmentFree(path[i], path[j])) path.RemoveRange(i + 1, j - i - 1);
            }
            return path;
        }

        /// <summary>
        /// Проверка ребра с АДАПТИВНЫМ шагом: Δq ≤ δ / (2·max‖J‖∞), т.е. смещение
        /// любой точки звена на шаге не превышает половины требуемого зазора —
        /// это исключает «проскок» сквозь препятствие (дискретизация по расстоянию).
        /// </summary>
        private bool SegmentFree(double[] a, double[] b)
        {
            float jn = v.MaxJacobianNorm(a);
            double maxStepNorm = Mathf.Max(0.002f, clearance * 0.5f / Mathf.Max(1e-6f, jn));
            int steps = Mathf.Max(2, Mathf.CeilToInt((float)(ConfigDistance(a, b) / maxStepNorm)));
            steps = Mathf.Min(steps, 400);
            for (int s = 1; s < steps; s++)
            {
                double k = (double)s / steps;
                var q = Interp(a, b, k);
                if (!Free(q, out _)) return false;
            }
            return true;
        }

        /// <summary>Параметризация времени (трапеция) + ресемплирование с фиксированным шагом.</summary>
        private PlannedTrajectory BuildTrajectory(List<double[]> path, Vector3 goalPoint)
        {
            var durations = new double[path.Count - 1];
            double total = 0;
            // Время сегмента: время крейсерского прохода по самому «медленному» суставу,
            // делённое на долю времени, остающуюся после разгона и торможения
            // (`accelShare` — именно ДОЛЯ, как и написано в описании поля).
            //
            // ИСПРАВЛЕНО 18.09.2026: прежняя формула `dq/vmax + vmax*accelShare/(vmax*2)`
            // после сокращения давала ПОСТОЯННУЮ добавку `accelShare/2` = 0.125 с к КАЖДОМУ
            // сегменту пути. На пути из десятков сегментов (после short-cut дерева RRT)
            // это давало время «с запасом»: 3.999 с там, где по лимитам хватает 1.543 с.
            //
            // ФИКС 2 (§20): эта параметризация больше НЕ является итоговым временем траектории —
            // она задаёт только плотность сэмплов (шаг по параметру пути). Итоговое время
            // считает ApplySProfile тем же ядром, что и постобработка.
            double share = Mathf.Clamp(accelShare, 0f, 0.6f);
            for (int i = 0; i + 1 < path.Count; i++)
            {
                double seg = 0;
                for (int j = 0; j < v.Dof; j++)
                {
                    double dq = System.Math.Abs(Delta(j, path[i][j], path[i + 1][j]));
                    double vmax = System.Math.Max(1e-3, v.VelMax[j]);
                    double t = (dq / vmax) / System.Math.Max(0.4, 1.0 - share);
                    seg = System.Math.Max(seg, t);
                }
                durations[i] = System.Math.Max(seg, timeStep);
                total += durations[i];
            }

            var samples = new List<double[]>();
            var times = new List<float>();
            double tAcc = 0;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                int steps = Mathf.Max(1, Mathf.RoundToInt((float)(durations[i] / timeStep)));
                for (int s = 0; s < steps; s++)
                {
                    double k = (double)s / steps;
                    samples.Add(Interp(path[i], path[i + 1], k));
                    times.Add((float)(tAcc + durations[i] * k));
                }
                tAcc += durations[i];
            }
            // Последний сэмпл — ровно в конце пути, но в НЕПРЕРЫВНОМ представлении
            // (иначе исполнитель «проскочит» полный оборот на последнем шаге).
            samples.Add(Interp(path[path.Count - 2], path[path.Count - 1], 1.0));
            times.Add((float)tAcc);

            var t2 = new PlannedTrajectory
            {
                Path = samples.ToArray(),
                Times = times.ToArray(),
                Time = tAcc
            };

            // ФИКС 2 (§20): время-оптимальный S-профиль вместо «трапеции с запасом».
            ApplySProfile(t2);

            // Метрики: длина, минимальный зазор, запас лимитов, σ_min в цели.
            double len = 0;
            float minClear = float.MaxValue, minLimit = float.MaxValue;
            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0) len += ConfigDistance(samples[i - 1], samples[i]);
                float c = v.ClearanceAt(samples[i], world, out _, out _);
                float self = v.SelfClearance(samples[i], out _, out _);
                minClear = Mathf.Min(minClear, Mathf.Min(c, self));
                minLimit = Mathf.Min(minLimit, v.LimitMargin(samples[i]));
            }
            t2.Length = len;
            t2.MinClearance = minClear == float.MaxValue ? 0f : minClear;
            t2.LimitMargin = minLimit == float.MaxValue ? 0f : minLimit;
            t2.Curvature = CurvatureOf(samples, t2);

            double[][] jac = KinematicsJacobian.Allocate(v.Dof);
            KinematicsJacobian.Compute(v, path[path.Count - 1], jac, out _);
            // σ_min в м/град (нормируем радианы → градусы, чтобы порог 0.05 был осмысленным)
            t2.SigmaMin = KinematicsJacobian.SigmaMin(jac, v.Dof) * 57.29578;
            _ = goalPoint;
            return t2;
        }

        /// <summary>
        /// ФИКС 2 (§20). ПЕРЕСЧЁТ ВРЕМЕНИ ПЛАНИРОВЩИКА АНАЛИТИЧЕСКИМ S-ПРОФИЛЕМ.
        ///
        /// БЫЛО: путь проходился с ПОСТОЯННОЙ скоростью внутри сегментов и запасом
        /// `1/(1 − accelShare)` = +33 % по времени; ускорение и рывок не учитывались вовсе
        /// (в узлах сегментов скорость менялась мгновенно). Отсюда «время с большим запасом»
        /// (§13.11, §19.16): 3,999 с там, где по лимитам хватает 1,543 с, — и заметный выигрыш
        /// постобработки этапов 4–6 на ровном месте.
        ///
        /// СТАЛО: геометрия пути НЕ меняется (те же сэмплы), перепараметризуется только время —
        /// тем же ядром, что и постобработка: `KvTrajMath.Retime`, внутри которого работает
        /// аналитический S-профиль `KvTrajMath.SProfileBuild` (разгон → крейсер → торможение
        /// с ограничением скорости, ускорения И рывка). Пределы берутся из
        /// <see cref="MotionLimits"/> — того же набора, что у сглаживания и время-оптимальной,
        /// поэтому времена планировщика и постобработки совпадают.
        ///
        /// Если валидатор не готов или профиль не построился — остаётся прежняя параметризация:
        /// поведение планировщика не ломается.
        /// </summary>
        private void ApplySProfile(PlannedTrajectory t)
        {
            if (t == null || v == null || !v.Ready) return;
            if (t.Path == null || t.Times == null || t.Path.Length < 3) return;

            KvMotionLimits lim = MotionLimits != null ? MotionLimits : new KvMotionLimits();
            // publishProfileNote: false — планировщик считает профиль «внутри себя» и НЕ должен
            // переписывать строку о последнем профиле, которую показывает вкладка оператора.
            PlannedTrajectory timed = KvTrajMath.Retime(v, t, lim, 1f, 1f, t.Label, true, false);
            if (timed == null || timed.Times == null || timed.Times.Length != t.Times.Length) return;

            t.Times = timed.Times;
            t.Time = timed.Time;
        }

        /// <summary>
        /// Кривизна пути (ТЗ «сортировка по длине, времени, кривизне»): угол поворота
        /// НАПРАВЛЕНИЯ движения между соседними сэмплами в нормированном пространстве суставов,
        /// в градусах. Прямой путь ≈0, «петляющий» — десятки градусов на сэмпл.
        /// Стоящие на месте участки (нулевой шаг) в среднее не входят.
        /// Заполняет ТРИ метрики: среднюю (<see cref="PlannedTrajectory.Curvature"/>),
        /// суммарную (<see cref="PlannedTrajectory.CurvatureTotal"/>) и максимальную
        /// (<see cref="PlannedTrajectory.CurvatureMax"/>) — их показывает панель траекторий.
        /// </summary>
        private double CurvatureOf(List<double[]> samples, PlannedTrajectory into = null)
        {
            if (into != null)
            {
                into.Curvature = 0.0;
                into.CurvatureTotal = 0.0;
                into.CurvatureMax = 0.0;
                into.CurvatureSamples = 0;
            }
            if (samples == null || samples.Count < 3) return 0.0;
            double total = 0.0;
            double max = 0.0;
            int counted = 0;
            double[] prevDir = null;
            var dir = new double[v.Dof];
            for (int i = 1; i < samples.Count; i++)
            {
                double norm = 0;
                for (int j = 0; j < v.Dof; j++)
                {
                    double d = Delta(j, samples[i - 1][j], samples[i][j]) / ranges[j];
                    dir[j] = d;
                    norm += d * d;
                }
                norm = System.Math.Sqrt(norm);
                if (norm < 1e-9) continue;                  // участок «стоим» — не кривизна
                for (int j = 0; j < v.Dof; j++) dir[j] /= norm;
                if (prevDir != null)
                {
                    double dot = 0;
                    for (int j = 0; j < v.Dof; j++) dot += dir[j] * prevDir[j];
                    dot = System.Math.Min(1.0, System.Math.Max(-1.0, dot));
                    double ang = System.Math.Acos(dot) * 57.29578;
                    total += ang;
                    if (ang > max) max = ang;
                    counted++;
                }
                if (prevDir == null) prevDir = new double[v.Dof];
                System.Array.Copy(dir, prevDir, v.Dof);
            }
            if (into != null)
            {
                into.CurvatureTotal = total;
                into.CurvatureMax = max;
                into.CurvatureSamples = counted;
            }
            return counted > 0 ? total / counted : 0.0;
        }
    }
}
