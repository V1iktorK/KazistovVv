using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Одна промежуточная точка маршрута (ЭТАП 5 ТЗ).</summary>
    public class KvWaypoint
    {
        public int Index;                 // позиция в маршруте (0 — первая)
        public Vector3 Position;          // точка в мире (цель TCP)
        public bool Reachable;            // решена ли IK и есть ли зазор
        public string Note = "";          // причина недостижимости
        public double[] Pose;             // поза прохода (для планировщика)
        public float Clearance;           // минимальный зазор в позе, м
        public GameObject Marker;
        public Renderer MarkerRenderer;

        /// <summary>
        /// ОГРАНИЧЕНИЯ ТОЧКИ (ЭТАП 7 ТЗ): ориентация TCP, скорость в точке, обязательный
        /// обход препятствия и пауза. Живут вместе с точкой, переживают перемещение,
        /// смену порядка и пересчёт маршрута.
        /// </summary>
        public KvWaypointLimits Limits = new KvWaypointLimits();

        /// <summary>Фактическое нарушение ориентации в позе прохода, ° (0 — соблюдено).</summary>
        public float OrientationError;

        public bool HasLimits { get { return Limits != null && Limits.Any; } }

        public string Short
        {
            get
            {
                return "#" + (Index + 1) + " (" + Position.x.ToString("0.000") + ", " +
                       Position.y.ToString("0.000") + ", " + Position.z.ToString("0.000") + ")" +
                       (HasLimits ? " " + Limits.Short() : "");
            }
        }
    }

    /// <summary>
    /// ЭТАП 5 ТЗ — WAYPOINT EDITOR (промежуточные точки маршрута).
    ///
    /// ЧТО ДЕЛАЕТ:
    ///   * ставит промежуточные точки: кнопка «Добавить waypoint» берёт ТЕКУЩУЮ точку
    ///     (зафиксированную красным лазером) или точку прицела — как и требует ТЗ;
    ///   * каждая точка проверяется на достижимость (IK + зазор до мира тем же
    ///     `PoseValidator`, что и весь проект); недостижимая помечается КРАСНЫМ маркером,
    ///     и маршрут по ней НЕ строится (ТЗ);
    ///   * маршрут перестраивается С УЧЁТОМ waypoints: точки идут как INTERMEDIATE GOALS
    ///     существующего планировщика (`Planner.PlanToGoal`/`PlanViaWaypoint`, тот же BiRRT,
    ///     что и штатные 8 вариантов) — сегментами «старт → WP1 → … → WPN → цель»;
    ///   * маршрут рисуется «колбасками» (`TrajectoryTube`) и запускается ШТАТНЫМ
    ///     исполнителем через `TrajectoryFlowController.PlayExternalPlan` — State Machine,
    ///     лазеры, IK и валидатор не переписываются;
    ///   * точки можно переместить (в точку прицела), удалить и изменить порядок;
    ///     список живёт ОТДЕЛЬНОЙ ВЕТКОЙ в дереве моделей (ТЗ).
    ///
    /// ДЛЯ SCARA всё то же самое, но с учётом её 3 осей (J1, J2, Z) — планирование и проверка
    /// идут через тот же `PoseValidator`/`Planner`, поэтому ограничения SCARA учитываются
    /// автоматически (включая «точку на уровне стола», см. §0.5).
    ///
    /// Служебный визуал (маркеры, трубки) — `HideFlags.HideInHierarchy`, без коллайдеров,
    /// поэтому он не попадает ни в иерархию, ни в мир столкновений (правило проекта).
    /// </summary>
    public class KvWaypointManager
    {
        public event Action<string> Message;
        public event Action Changed;

        // ------------------------------------------------------------------ параметры
        public float markerRadius = 0.042f;
        public int routeVariants = 3;         // сколько маршрутов-вариантов показывать
        public float tubeRadius = 0.035f;
        public float tubeSpread = 0.045f;     // боковое разведение маршрутов, м
        public int maxWaypoints = 8;
        public bool logEvents = true;

        public Color colorOk = new Color(0.20f, 0.95f, 0.40f);
        public Color colorBad = new Color(1f, 0.20f, 0.18f);
        public Color colorSelected = new Color(0.30f, 0.85f, 1f);
        public Color colorLimits = new Color(0.75f, 0.45f, 1f);        // точка с ограничениями (этап 7)
        public Color routeColor = new Color(0.95f, 0.55f, 0.10f);      // оранжевый, как «колбаски»

        /// <summary>
        /// Лимиты движения (скорость/ускорение/jerk) — те же, что у этапов 4–6; нужны для
        /// пересчёта профиля времени маршрута при ограничении скорости в точках (этап 7).
        /// </summary>
        public KvMotionLimits limits;

        /// <summary>
        /// Учитывать ли ограничения точек (этап 7) при проверке точки и построении маршрута.
        /// Выключается переключателем «Ограничения waypoint» в настройках.
        /// </summary>
        public bool useWaypointLimits = true;

        /// <summary>Задать лимиты движения (вызывает хаб этапов 7–12).</summary>
        public void SetMotionLimits(KvMotionLimits value)
        {
            limits = value;
        }

        // ------------------------------------------------------------------ состояние
        private TrajectoryFlowController flow;
        private FreeFlyCameraController rig;
        private CollisionWorld world;

        private readonly List<KvWaypoint> items = new List<KvWaypoint>();
        private readonly List<TrajectoryTube> tubes = new List<TrajectoryTube>();
        private Transform root;
        private Material matOk, matBad, matSelected, matLimits, matTube;

        private int selected = -1;
        private PlannedTrajectory route;          // последний построенный маршрут
        private string routeNote = "";
        private float highlightTimer;

        public IReadOnlyList<KvWaypoint> Items { get { return items; } }
        public int Count { get { return items.Count; } }
        public int SelectedIndex { get { return selected; } }
        public bool HasRoute { get { return route != null; } }
        public PlannedTrajectory Route { get { return route; } }
        public string RouteNote { get { return routeNote; } }
        public int UnreachableCount
        {
            get
            {
                int n = 0;
                foreach (KvWaypoint w in items) if (!w.Reachable) n++;
                return n;
            }
        }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,
                         FreeFlyCameraController cameraRig)
        {
            flow = controller;
            world = collisionWorld;
            rig = cameraRig;
            EnsureRoot();
        }

        private void Say(string message)
        {
            if (logEvents) Debug.Log("[Waypoint] " + message);
            if (Message != null) Message(message);
        }

        private void Notify()
        {
            if (Changed != null) Changed();
        }

        // ================================================================== список точек

        /// <summary>
        /// Добавить waypoint в ТЕКУЩЕЙ точке: если красным лазером зафиксирована точка — она,
        /// иначе — точка прицела (шарик лазера). Это и есть «выбрать точку + Добавить waypoint».
        /// </summary>
        public bool AddFromAim()
        {
            Vector3 point;
            string source;
            if (flow != null && flow.State.hasPoint)
            {
                point = flow.State.point;
                source = "зафиксированная точка";
            }
            else if (rig != null)
            {
                point = rig.AimPointPublic;
                source = "точка прицела";
            }
            else
            {
                Say("нет ни зафиксированной точки, ни прицела — наведите красный лазер");
                return false;
            }
            return Add(point, source);
        }

        /// <summary>Добавить waypoint в конкретной точке мира.</summary>
        public bool Add(Vector3 point, string source)
        {
            if (flow == null || !flow.Validator.Ready)
            {
                Say("робот не определён — валидатор не готов");
                return false;
            }
            if (items.Count >= maxWaypoints)
            {
                Say("достигнут предел промежуточных точек (" + maxWaypoints + ")");
                return false;
            }

            KvWaypoint wp = new KvWaypoint();
            wp.Position = point;
            items.Add(wp);
            Renumber();
            EvaluateAll();
            CreateMarker(wp);

            selected = wp.Index;
            Say("waypoint " + wp.Short + " добавлен (" + source + ") · " + items.Count +
                " в маршруте · " + (wp.Reachable ? "достижим" : "НЕДОСТИЖИМ: " + wp.Note));

            RebuildRoute();
            Notify();
            return true;
        }

        /// <summary>Удалить точку по индексу.</summary>
        public bool Remove(int index)
        {
            if (index < 0 || index >= items.Count) return false;
            KvWaypoint wp = items[index];
            KillMarker(wp);
            items.RemoveAt(index);
            Renumber();
            EvaluateAll();
            selected = items.Count == 0 ? -1 : Mathf.Clamp(index, 0, items.Count - 1);

            Say("waypoint удалён · осталось " + items.Count);
            RebuildRoute();
            Notify();
            return true;
        }

        public bool RemoveSelected() { return Remove(selected); }

        /// <summary>Удалить последнюю точку.</summary>
        public bool RemoveLast() { return items.Count > 0 && Remove(items.Count - 1); }

        /// <summary>Очистить все точки (маршрут снимается).</summary>
        public void Clear(string why = "очищено оператором")
        {
            foreach (KvWaypoint wp in items) KillMarker(wp);
            items.Clear();
            selected = -1;
            HideTubes();
            route = null;
            routeNote = "";
            Say("промежуточные точки: " + why + " · маршрут снят");
            Notify();
        }

        /// <summary>Изменить порядок: сдвинуть точку раньше (ТЗ: «изменить порядок»).</summary>
        public bool MoveUp(int index)
        {
            if (index <= 0 || index >= items.Count) return false;
            KvWaypoint wp = items[index];
            items.RemoveAt(index);
            items.Insert(index - 1, wp);
            Renumber();
            EvaluateAll();
            selected = index - 1;
            Say("порядок изменён: " + wp.Short + " → позиция " + (index));
            RebuildRoute();
            Notify();
            return true;
        }

        /// <summary>Изменить порядок: сдвинуть точку позже.</summary>
        public bool MoveDown(int index)
        {
            if (index < 0 || index >= items.Count - 1) return false;
            KvWaypoint wp = items[index];
            items.RemoveAt(index);
            items.Insert(index + 1, wp);
            Renumber();
            EvaluateAll();
            selected = index + 1;
            Say("порядок изменён: " + wp.Short + " → позиция " + (index + 2));
            RebuildRoute();
            Notify();
            return true;
        }

        /// <summary>Переместить выбранную точку в точку прицела (ТЗ: «переместить»).</summary>
        public bool MoveSelectedToAim()
        {
            if (selected < 0 || selected >= items.Count)
            {
                Say("waypoint не выбран — выберите узел в дереве моделей (ветка «Промежуточные точки»)");
                return false;
            }
            Vector3 point;
            if (flow != null && flow.State.hasPoint) point = flow.State.point;
            else if (rig != null) point = rig.AimPointPublic;
            else
            {
                Say("нет точки прицела");
                return false;
            }
            return SetPosition(selected, point);
        }

        /// <summary>Задать новую позицию точки.</summary>
        public bool SetPosition(int index, Vector3 position)
        {
            if (index < 0 || index >= items.Count) return false;
            KvWaypoint wp = items[index];
            wp.Position = position;
            EvaluateAll();
            UpdateMarker(wp);
            Say("waypoint " + wp.Short + " перемещён · " +
                (wp.Reachable ? "достижим" : "НЕДОСТИЖИМ: " + wp.Note));
            RebuildRoute();
            Notify();
            return true;
        }

        /// <summary>Выбрать точку (из дерева моделей).</summary>
        public void Select(int index)
        {
            selected = (index >= 0 && index < items.Count) ? index : -1;
            for (int i = 0; i < items.Count; i++) UpdateMarker(items[i]);
        }

        /// <summary>Сдвинуть точку на вектор (кнопки/клавиши интерфейса).</summary>
        public bool NudgeSelected(Vector3 delta)
        {
            if (selected < 0 || selected >= items.Count) return false;
            return SetPosition(selected, items[selected].Position + delta);
        }

        private void Renumber()
        {
            for (int i = 0; i < items.Count; i++)
            {
                items[i].Index = i;
                if (items[i].Marker != null) items[i].Marker.name = "Waypoint_" + (i + 1);
            }
        }

        // ================================================================== достижимость

        /// <summary>
        /// Проверить все точки: IK (тем же способом, что pick-and-place и позы), лимиты
        /// и зазор до мира. Недостижимая точка помечается и в маршрут не пускается.
        ///
        /// ВАЖНО (найдено прогоном 15.09.2026): одиночная IK часто даёт позу, стоящую РОВНО
        /// на пределе лимита сустава (запас 0.0°), а планировщик требует ≥3°
        /// (`Planner.minLimitMarginDeg`) — точка при этом формально «решается», но ни один
        /// маршрут через неё не строится. Поэтому поза выбирается из ВЕТВЕЙ IK штатного
        /// планировщика (`SolveGoalConfigs`) по МАКСИМАЛЬНОМУ запасу до лимитов, а если
        /// запаса нет ни у одной ветви — точка честно помечается недостижимой с причиной.
        /// </summary>
        public void EvaluateAll()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return;
            PoseValidator v = flow.Validator;
            Planner planner = flow.Planner;
            double[] seed = v.CopyCurrent();

            foreach (KvWaypoint wp in items)
            {
                float margin;
                string note;
                double[] q = BestPoseForPoint(v, planner, wp.Position, seed, wp, out margin, out note);
                if (q == null)
                {
                    wp.Reachable = false;
                    wp.Pose = null;
                    // Ограничение могло не дать позу — уточняем причину для оператора (этап 7).
                    wp.Note = wp.HasLimits && wp.Limits.Orientation.Active
                        ? KvLocExtra2.T("wp.orient.fail", "ориентация не достигнута") +
                          (string.IsNullOrEmpty(note) ? "" : ": " + note)
                        : note;
                    UpdateMarker(wp);
                    continue;
                }

                wp.Pose = q;
                Vector3 tcp;
                Vector3[] nodes;
                float clearance = world != null ? v.ClearanceAt(q, world, out tcp, out nodes) : 1f;
                wp.Clearance = clearance;

                bool clearOk = clearance > v.linkRadius * 0.5f;
                bool marginOk = planner == null || margin >= planner.minLimitMarginDeg;
                // ОГРАНИЧЕНИЕ ОРИЕНТАЦИИ (этап 7): поза обязана его соблюдать с допуском.
                float orientTol = useWaypointLimits && wp.Limits != null && wp.Limits.Orientation != null
                    ? Mathf.Max(0.1f, wp.Limits.Orientation.toleranceDeg) + 0.5f
                    : 180f;
                bool orientOk = !useWaypointLimits || !wp.HasLimits || wp.Limits.Orientation == null ||
                                !wp.Limits.Orientation.Active || wp.OrientationError <= orientTol;
                wp.Reachable = clearOk && marginOk && orientOk;

                string limitsNote = wp.HasLimits ? " · " + wp.Limits.Describe() : "";
                wp.Note = !clearOk
                    ? "пересечение со сцене (зазор " + (clearance * 1000f).ToString("0") + " мм)"
                    : !marginOk
                        ? "запас до лимитов " + margin.ToString("0.0") + "° < " +
                          planner.minLimitMarginDeg.ToString("0.0") + "° (планировщик точку не пройдёт)"
                        : !orientOk
                            ? KvLocExtra2.T("wp.orient.fail", "ориентация не достигнута") + " (" +
                              wp.OrientationError.ToString("0.0") + "° > " +
                              orientTol.ToString("0.0") + "°)"
                            : "зазор " + (clearance * 1000f).ToString("0") + " мм · запас " +
                              margin.ToString("0.0") + "°" +
                              (wp.HasLimits && wp.Limits.Orientation != null &&
                               wp.Limits.Orientation.Active
                                  ? " · " + KvLocExtra2.T("wp.solved", "поза с ограничением найдена") +
                                    " (" + wp.OrientationError.ToString("0.0") + "°) "
                                  : "") + limitsNote;

                seed = q;                       // следующая точка — от предыдущей (не рвём маршрут)
                UpdateMarker(wp);
            }
        }

        /// <summary>
        /// Поза прохода через точку: перебор ветвей IK планировщика (плечо/локоть/запястье)
        /// с выбором максимального запаса до лимитов; при неудаче — одиночная IK
        /// (`KvPlanKit.SolvePoseForPoint`, тот же путь, что у поз и pick-and-place).
        /// </summary>
        private double[] BestPoseForPoint(PoseValidator v, Planner planner, Vector3 point,
            double[] seed, out float margin, out string note)
        {
            margin = 0f;
            note = "";

            // ОГРАНИЧЕНИЯ ТОЧКИ (этап 7): если у waypoint задана ориентация инструмента,
            // поза ищется решателем с дополнительным условием (ветви IK планировщика —
            // только те, что соблюдают ограничение).
            return BestPoseForPoint(v, planner, point, seed, null, out margin, out note);
        }

        private double[] BestPoseForPoint(PoseValidator v, Planner planner, Vector3 point,
            double[] seed, KvWaypoint waypoint, out float margin, out string note)
        {
            margin = 0f;
            note = "";
            KvOrientConstraint orient = useWaypointLimits && waypoint != null && waypoint.Limits != null
                ? waypoint.Limits.Orientation
                : null;

            if (orient != null && orient.Active)
            {
                float angle;
                double[] q = KvToolKinematics.SolveWaypointPose(v, world, planner, point, orient,
                    seed, planner != null ? planner.clearance : 0.01f,
                    planner != null ? planner.selfClearance : 0.01f,
                    planner != null ? planner.minLimitMarginDeg : 3f,
                    0.006f, out margin, out angle, out note);
                if (waypoint != null) waypoint.OrientationError = angle;
                if (q != null) return q;
                return null;
            }

            if (planner != null && planner.Ready)
            {
                List<Planner.GoalConfig> branches = planner.SolveGoalConfigs(seed, point, 8, 4242);
                double[] best = null;
                float bestMargin = -1f;
                if (branches != null)
                {
                    foreach (Planner.GoalConfig branch in branches)
                    {
                        if (branch.q == null || branch.q.Length < v.Dof) continue;
                        if (!v.WithinLimits(branch.q)) continue;
                        float m = v.LimitMargin(branch.q);
                        if (m > bestMargin) { bestMargin = m; best = branch.q; }
                    }
                }
                if (best != null && bestMargin >= planner.minLimitMarginDeg)
                {
                    margin = bestMargin;
                    if (waypoint != null) waypoint.OrientationError = 0f;
                    return best;
                }
                if (best != null)
                {
                    // Запоминаем худший случай: поза есть, но запаса нет.
                    margin = bestMargin;
                    note = "запас до лимитов " + bestMargin.ToString("0.0") + "° < " +
                           planner.minLimitMarginDeg.ToString("0.0") + "°";
                    return best;
                }
            }

            double[] q2;
            string why;
            if (!KvPlanKit.SolvePoseForPoint(v, world, point, Vector3.down, seed, out q2, out why))
            {
                note = string.IsNullOrEmpty(why) ? "IK не сходится" : why;
                return null;
            }
            margin = v.LimitMargin(q2);
            return q2;
        }

        // ================================================================== маршрут

        /// <summary>
        /// Построить маршрут через waypoints существующим планировщиком.
        /// Возвращает false, если маршрут не построен (нет точки, есть недостижимая точка,
        /// планировщик не нашёл путь) — по ТЗ траектория в этом случае НЕ строится.
        /// </summary>
        public bool RebuildRoute()
        {
            route = null;
            routeNote = "";
            HideTubes();

            if (flow == null || !flow.Validator.Ready || flow.Planner == null)
            {
                routeNote = "робот не определён";
                return false;
            }
            if (items.Count == 0)
            {
                routeNote = "точек нет";
                return false;
            }
            if (!flow.State.hasPoint)
            {
                routeNote = "нет цели: сначала выберите точку красным лазером";
                Say("маршрут не построен: " + routeNote);
                return false;
            }
            EvaluateAll();
            if (UnreachableCount > 0)
            {
                foreach (KvWaypoint wp in items)
                {
                    if (wp.Reachable) continue;
                    routeNote = "waypoint " + wp.Short + " недостижим (" + wp.Note + ")";
                    break;
                }
                Say("маршрут НЕ построен: " + routeNote);
                return false;
            }

            PoseValidator v = flow.Validator;
            double[] start = v.CopyCurrent();
            Planner planner = flow.Planner;

            // --- 1) префикс «старт → точки»: сегменты планируются по очереди.
            //     ОГРАНИЧЕНИЯ ЭТАПА 7: обязательный обход препятствия (сегмент строится
            //     «через сторону» штатным `PlanViaWaypoint`), пауза и ограничение скорости
            //     запоминаются вместе с индексом сэмпла, где стоит точка.
            var prefix = new List<double[]>();
            var waypointSamples = new List<int>();      // индекс сэмпла точки в склеенном пути
            var dwells = new List<KvDwell>();
            var caps = new List<KvSpeedCap>();
            string constraintNote = "";

            double[] cursor = start;
            int seed = 17;
            for (int i = 0; i < items.Count; i++)
            {
                KvWaypoint wp = items[i];
                string why;
                PlannedTrajectory seg = PlanSegment(v, planner, cursor, wp.Pose,
                    seed + i * 131, "WP" + (i + 1), wp, out why);
                if (seg == null)
                {
                    routeNote = "путь к waypoint " + wp.Short + " не найден: " + why;
                    Say("маршрут НЕ построен: " + routeNote);
                    return false;
                }

                // --- обязательный обход препятствия: проверяем, что сегмент реально обошёл
                if (useWaypointLimits && wp.Limits != null && wp.Limits.Detour)
                {
                    int at;
                    float min = KvWaypointRouteKit.MinDistanceToPoint(v, seg, wp.Limits.DetourCenter, out at);
                    float need = wp.Limits.DetourRadius + wp.Limits.DetourMargin;
                    if (min < need)
                    {
                        routeNote = KvLocExtra2.T("wp.detour.fail", "обход препятствия не найден") +
                                    ": у точки " + wp.Short + " маршрут подходит на " +
                                    (min * 1000f).ToString("0") + " мм (нужно ≥ " +
                                    (need * 1000f).ToString("0") + " мм)";
                        Say("маршрут НЕ построен: " + routeNote);
                        return false;
                    }
                    constraintNote += " · обход #" + (i + 1) + " " + (min * 1000f).ToString("0") + " мм";
                }

                AppendPath(prefix, seg.Path);
                cursor = seg.GoalQ ?? wp.Pose;

                int sampleIndex = prefix.Count - 1;
                waypointSamples.Add(sampleIndex);

                if (useWaypointLimits && wp.Limits != null && wp.Limits.Pause)
                {
                    dwells.Add(new KvDwell
                    {
                        AfterSample = sampleIndex,
                        Seconds = Mathf.Clamp(wp.Limits.PauseSeconds, 0.1f, 60f),
                        Label = "#" + (i + 1)
                    });
                    constraintNote += " · " + KvLocExtra2.T("wp.pause.done", "пауза") + " #" + (i + 1) +
                                      " " + wp.Limits.PauseSeconds.ToString("0.0") + " с";
                }
                if (useWaypointLimits && wp.Limits != null && wp.Limits.LimitSpeed)
                {
                    caps.Add(new KvSpeedCap
                    {
                        Sample = sampleIndex,
                        MaxMps = Mathf.Clamp(wp.Limits.MaxSpeedMps, 0.005f, 5f),
                        Label = "waypoint #" + (i + 1)
                    });
                }
            }

            // --- 2) финальный участок: варианты до цели (как штатные 8 траекторий)
            List<PlannedTrajectory> finals = null;
            for (int k = 0; k < 3 && (finals == null || finals.Count == 0); k++)
                finals = planner.Plan(cursor, flow.State.point,
                    Mathf.Clamp(routeVariants, 1, 8), seed + k * 977);
            if (finals == null || finals.Count == 0)
            {
                routeNote = "путь к цели не найден: планировщик не нашёл ни одного варианта" +
                            (string.IsNullOrEmpty(planner.LastDebug) ? "" : " (" + planner.LastDebug + ")");
                Say("маршрут НЕ построен: " + routeNote);
                return false;
            }

            // --- 3) собираем цельные маршруты: префикс + финальный участок
            var built = new List<PlannedTrajectory>();
            for (int k = 0; k < finals.Count; k++)
            {
                var full = new List<double[]>(prefix);
                AppendPath(full, finals[k].Path);
                PlannedTrajectory merged = Merge(full, finals[k],
                    "Маршрут через " + items.Count + " waypoint" + (items.Count == 1 ? "" : "s"));
                if (merged != null) built.Add(merged);
            }
            if (built.Count == 0)
            {
                routeNote = "маршрут не собран";
                return false;
            }

            built.Sort(delegate (PlannedTrajectory a, PlannedTrajectory b)
            {
                return a.Score.CompareTo(b.Score);
            });
            route = built[0];

            // --- 4) ОГРАНИЧЕНИЯ ЭТАПА 7 в собранном маршруте: паузы, скорость, профиль времени.
            if (dwells.Count > 0)
            {
                route = KvWaypointRouteKit.InsertDwells(route, dwells, 0.05f);
                for (int k = 1; k < built.Count; k++)
                    built[k] = KvWaypointRouteKit.InsertDwells(built[k], dwells, 0.05f);
            }
            if (caps.Count > 0)
            {
                // Профиль по лимитам суставов, затем проверка скорости в самих точках.
                if (limits != null && v.Ready)
                {
                    PlannedTrajectory retimed = KvTrajMath.Retime(v, route, limits, 1f, 1f,
                        route.Label, true);
                    if (retimed != null) route = retimed;
                }
                string speedNote;
                KvWaypointRouteKit.EnforceSpeedCaps(v, route, limits, caps, out speedNote);
                if (!string.IsNullOrEmpty(speedNote))
                    constraintNote += " · " + speedNote;
            }

            ShowTubes(built);

            routeNote = "маршрут построен: точек " + items.Count + " · вариантов " + built.Count +
                        " · длина " + route.Length.ToString("0.00") + " · запас лимитов " +
                        route.LimitMargin.ToString("0.0") + "° · зазор " +
                        (route.MinClearance * 1000f).ToString("0") + " мм" +
                        (constraintNote.Length > 0 ? " · ограничения:" + constraintNote : "");
            Say(routeNote);
            Notify();
            return true;
        }

        /// <summary>
        /// Один сегмент маршрута «из позы A в позу B» ШТАТНЫМ планировщиком (BiRRT) с
        /// НЕСКОЛЬКИМИ seed'ами: у одного seed'а случайное дерево может не сойтись, поэтому
        /// делается до 4 попыток — так же, как это делает сам поток этапов при подборе
        /// 8 вариантов. Возвращает null и понятную причину, если путь не найден.
        ///
        /// ЕСЛИ У ТОЧКИ ЗАДАН ОБЯЗАТЕЛЬНЫЙ ОБХОД (этап 7), сегмент строится «через сторону»
        /// штатным `Planner.PlanViaWaypoint` — он специально уводит путь от прямой линии,
        /// поэтому окрестность препятствия остаётся в стороне. Прямой путь используется
        /// только как резерв, если обход не нашёлся и обход НЕ обязателен.
        /// </summary>
        private PlannedTrajectory PlanSegment(PoseValidator v, Planner planner, double[] from,
            double[] to, int seed, string tag, KvWaypoint waypoint, out string why)
        {
            why = "";
            if (to == null || to.Length < v.Dof)
            {
                why = "поза точки не посчитана (IK)";
                return null;
            }
            if (!v.WithinLimits(to))
            {
                why = "поза точки вне лимитов суставов";
                return null;
            }
            float margin = v.LimitMargin(to);
            if (margin < planner.minLimitMarginDeg)
            {
                why = "запас до лимитов " + margin.ToString("0.0") + "° < " +
                      planner.minLimitMarginDeg.ToString("0.0") + "°";
                return null;
            }

            // --- обязательный обход препятствия (этап 7): путь «через сторону»
            if (useWaypointLimits && waypoint != null && waypoint.Limits != null && waypoint.Limits.Detour)
            {
                for (int k = 0; k < 5; k++)
                {
                    PlannedTrajectory detour = planner.PlanViaWaypoint(from, to, seed + k * 613,
                        tag + " (обход)", 6);
                    if (detour == null || detour.Path == null || detour.Path.Length < 2) continue;
                    int at;
                    float min = KvWaypointRouteKit.MinDistanceToPoint(v, detour,
                        waypoint.Limits.DetourCenter, out at);
                    if (min < waypoint.Limits.DetourRadius + waypoint.Limits.DetourMargin) continue;
                    return detour;
                }
                why = KvLocExtra2.T("wp.detour.fail", "обход препятствия не найден") +
                      " (нужен путь в стороне от окрестности " +
                      waypoint.Limits.DetourRadius.ToString("0.00") + " м)";
                return null;
            }

            for (int k = 0; k < 4; k++)
            {
                PlannedTrajectory seg = planner.PlanToGoal(from, to, seed + k * 917, tag);
                if (seg != null && seg.Path != null && seg.Path.Length >= 2) return seg;
            }
            why = "BiRRT не нашёл путь за 4 попытки (нужен зазор ≥ " +
                  (planner.clearance * 1000f).ToString("0") + " мм; самозазор ≥ " +
                  (planner.selfClearance * 1000f).ToString("0") + " мм)";
            return null;
        }

        private static void AppendPath(List<double[]> target, double[][] path)        {
            if (path == null) return;
            int from = target.Count > 0 ? 1 : 0;         // стык сегментов не дублируем
            for (int i = from; i < path.Length; i++) target.Add(path[i]);
        }

        /// <summary>Собрать один `PlannedTrajectory` из склеенного пути.</summary>
        private PlannedTrajectory Merge(List<double[]> path, PlannedTrajectory tail, string label)
        {
            if (path == null || path.Count < 2) return null;
            PoseValidator v = flow.Validator;

            PlannedTrajectory plan = new PlannedTrajectory();
            plan.Label = label;
            plan.Path = path.ToArray();
            plan.Times = new float[plan.Path.Length];
            plan.BranchTag = "BiRRT (штатный планировщик), сегменты через waypoints";

            float length = 0f;
            float margin = float.MaxValue;
            float clearance = float.MaxValue;
            Vector3 prev = v.TcpAt(plan.Path[0]);
            for (int i = 0; i < plan.Path.Length; i++)
            {
                Vector3 p = v.TcpAt(plan.Path[i]);
                if (i > 0) length += Vector3.Distance(prev, p);
                prev = p;
                margin = Mathf.Min(margin, v.LimitMargin(plan.Path[i]));
                if (world != null)
                {
                    Vector3 tcp;
                    Vector3[] nodes;
                    clearance = Mathf.Min(clearance, v.ClearanceAt(plan.Path[i], world, out tcp, out nodes));
                }
                plan.Times[i] = i;
            }

            // Профиль времени: средняя скорость берётся у финального участка (он посчитан
            // штатным планировщиком), по ней раскладывается весь склеенный маршрут.
            float tailTime = tail != null && tail.Times != null && tail.Times.Length > 0
                ? tail.Times[tail.Times.Length - 1] : 0f;
            float tailLen = tail != null ? (float)tail.Length : 0f;
            float speed = tailLen > 0.01f && tailTime > 0.01f ? tailLen / tailTime : 1f;
            float total = Mathf.Max(0.5f, length / Mathf.Max(0.01f, speed));
            for (int i = 0; i < plan.Path.Length; i++)
                plan.Times[i] = total * (i / (float)(plan.Path.Length - 1));

            plan.Time = total;
            plan.Length = length;
            plan.MinClearance = clearance == float.MaxValue ? 0.05f : clearance;
            plan.LimitMargin = margin == float.MaxValue ? 180f : margin;
            plan.SigmaMin = tail != null ? tail.SigmaMin : 0.0;
            plan.Score = (tail != null ? tail.Score : 0.0) + items.Count * 0.01;
            return plan;
        }

        /// <summary>Проиграть маршрут штатным исполнителем (этап 4 потока, внешний план).</summary>
        public bool PlayRoute()
        {
            if (route == null && !RebuildRoute()) return false;
            if (route == null) return false;
            if (flow.ExternalMotionRunning)
            {
                Say("робот уже выполняет внешний план — сначала остановите его");
                return false;
            }
            bool ok = flow.PlayExternalPlan(route, route.GoalQ,
                "маршрут через промежуточные точки (" + items.Count + ")");
            Say(ok
                ? "маршрут запущен: " + route.Path.Length + " сэмплов · " +
                  route.Length.ToString("0.00") + " юнита · " + route.Time.ToString("0.0") + " с"
                : "маршрут не запущен (робот занят или план отклонён)");
            return ok;
        }

        // ================================================================== визуал

        private void EnsureRoot()
        {
            if (root != null) return;
            GameObject go = new GameObject("KvWaypoints");
            go.hideFlags = HideFlags.HideInHierarchy;
            root = go.transform;

            matOk = MakeMaterial(colorOk);
            matBad = MakeMaterial(colorBad);
            matSelected = MakeMaterial(colorSelected);
            matLimits = MakeMaterial(colorLimits);
            matTube = MakeMaterial(routeColor);
        }

        private static Material MakeMaterial(Color color)
        {
            // Unlit-материал под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP).
            Material m = KvRenderPipelineShaders.CreateUnlit("Материал_путевой_точки", color);
            if (m == null) return null;
            m.hideFlags = HideFlags.HideAndDontSave;
            if (m.HasProperty("_EmissiveColor"))
            {
                m.SetColor("_EmissiveColor", color * 2.0f);
                m.EnableKeyword("_EMISSION");
            }
            return m;
        }

        private void CreateMarker(KvWaypoint wp)
        {
            EnsureRoot();
            if (root == null || wp.Marker != null) return;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Waypoint_" + (wp.Index + 1);
            sphere.hideFlags = HideFlags.HideInHierarchy;
            Collider col = sphere.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destroy(col); }
            sphere.transform.SetParent(root, false);
            sphere.transform.position = wp.Position;
            sphere.transform.localScale = Vector3.one * (markerRadius * 2f);

            Renderer r = sphere.GetComponent<Renderer>();
            if (r != null)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            wp.Marker = sphere;
            wp.MarkerRenderer = r;
            UpdateMarker(wp);
        }

        private void UpdateMarker(KvWaypoint wp)
        {
            if (wp == null) return;
            if (wp.Marker == null) { CreateMarker(wp); return; }
            wp.Marker.transform.position = wp.Position;
            float k = wp.Index == selected ? markerRadius * 2.8f : markerRadius * 2f;
            wp.Marker.transform.localScale = Vector3.one * k;
            if (wp.MarkerRenderer != null)
            {
                // Точка с ограничениями (этап 7) выделяется своим цветом, пока не выбрана:
                // так оператор сразу видит, где маршрут ведёт себя не «как обычно».
                wp.MarkerRenderer.sharedMaterial = !wp.Reachable ? matBad
                    : (wp.Index == selected ? matSelected
                        : (wp.HasLimits ? matLimits : matOk));
                wp.MarkerRenderer.enabled = true;
            }
        }

        private void KillMarker(KvWaypoint wp)
        {
            if (wp == null || wp.Marker == null) return;
            Kill(wp.Marker);
            wp.Marker = null;
            wp.MarkerRenderer = null;
        }

        private void HideTubes()
        {
            foreach (TrajectoryTube t in tubes)
                if (t != null) Kill(t.gameObject);
            tubes.Clear();
        }

        private void ShowTubes(List<PlannedTrajectory> plans)
        {
            HideTubes();
            if (flow == null || !flow.Validator.Ready) return;
            PoseValidator v = flow.Validator;

            for (int i = 0; i < plans.Count; i++)
            {
                PlannedTrajectory plan = plans[i];
                if (plan == null || plan.Path == null || plan.Path.Length < 2) continue;

                int n = plan.Path.Length;
                var pts = new Vector3[n];
                for (int k = 0; k < n; k++) pts[k] = v.TcpAt(plan.Path[k]);
                if (i > 0) pts = Spread(pts, tubeSpread * i);

                GameObject go = new GameObject("WaypointRoute_" + (i + 1));
                go.hideFlags = HideFlags.HideInHierarchy;
                if (root != null) go.transform.SetParent(root, false);
                TrajectoryTube tube = go.AddComponent<TrajectoryTube>();
                tube.radius = tubeRadius;
                tube.baseColor = routeColor;
                tube.Build(pts, routeColor, false);
                tubes.Add(tube);
            }

            // Маркеры точек — поверх трубок, чтобы их было видно всегда.
            foreach (KvWaypoint wp in items) UpdateMarker(wp);
        }

        private static Vector3[] Spread(Vector3[] path, float offset)
        {
            if (path == null || path.Length < 2) return path;
            var result = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 dir = i == 0 ? path[1] - path[0] : path[i] - path[i - 1];
                Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
                result[i] = path[i] + side.normalized * offset;
            }
            return result;
        }

        /// <summary>Кадровое обслуживание: пульсация выбранного маркера.</summary>
        public void Tick(float dt)
        {
            if (items.Count == 0) return;
            highlightTimer -= dt;
            if (highlightTimer > 0f) return;
            highlightTimer = 0.2f;
            for (int i = 0; i < items.Count; i++)
            {
                if (i != selected) continue;
                KvWaypoint wp = items[i];
                if (wp.Marker == null) continue;
                float k = markerRadius * 2.4f + Mathf.Sin(Time.time * 4f) * markerRadius * 0.4f;
                wp.Marker.transform.localScale = Vector3.one * k;
            }
        }

        /// <summary>Строка состояния для панели/статуса.</summary>
        public string Status
        {
            get
            {
                if (items.Count == 0) return KvLoc.T("waypoint.title", "Промежуточные точки") +
                    ": " + KvLoc.T("common.none", "нет");
                int bad = UnreachableCount;
                return KvLoc.T("waypoint.title", "Промежуточные точки") + ": " + items.Count +
                       (bad > 0 ? " · " + KvLoc.T("waypoint.unreachable", "недостижима") + ": " + bad : "") +
                       (route != null ? " · маршрут: " + route.Path.Length + " сэмплов" : " · маршрут не построен");
            }
        }

        public void Dispose()
        {
            Clear("модуль выключен");
            HideTubes();
            if (root != null) Kill(root.gameObject);
            if (matOk != null) Kill(matOk);
            if (matBad != null) Kill(matBad);
            if (matSelected != null) Kill(matSelected);
            if (matTube != null) Kill(matTube);
            root = null;
        }

        private static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        private static void Destroy(UnityEngine.Object o) { Kill(o); }
    }
}
