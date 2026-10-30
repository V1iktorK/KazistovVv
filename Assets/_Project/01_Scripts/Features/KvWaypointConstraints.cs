using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ОГРАНИЧЕНИЯ ПРОМЕЖУТОЧНОЙ ТОЧКИ (ЭТАП 7 ТЗ). Пользователь может задать для waypoint:
    ///   • требуемую ОРИЕНТАЦИЮ TCP (направление оси инструмента в мире + допуск);
    ///   • максимальную СКОРОСТЬ TCP в этой точке;
    ///   • обязательный ОБХОД препятствия в окрестности (центр + радиус + запас);
    ///   • ПАУЗУ (остановиться и подождать) — в маршрут вставляется реальная выдержка.
    ///
    /// Объект живёт прямо в <see cref="KvWaypoint"/>, поэтому ограничения переживают
    /// перемещение точки, смену порядка и пересчёт маршрута.
    /// </summary>
    public class KvWaypointLimits
    {
        /// <summary>Ограничение ориентации инструмента (этап 7, тот же тип, что у этапа 8).</summary>
        public KvOrientConstraint Orientation = new KvOrientConstraint();

        /// <summary>Ограничивать скорость TCP в точке.</summary>
        public bool LimitSpeed;
        /// <summary>Максимальная скорость TCP в точке, м/с.</summary>
        public float MaxSpeedMps = 0.10f;

        /// <summary>Обязательный обход препятствия в окрестности точки.</summary>
        public bool Detour;
        /// <summary>Центр препятствия (откуда берётся — из точки прицела или задаётся вручную).</summary>
        public Vector3 DetourCenter = Vector3.zero;
        /// <summary>Радиус окрестности препятствия, м.</summary>
        public float DetourRadius = 0.12f;
        /// <summary>Дополнительный запас, м (насколько ближе радиуса подходить нельзя).</summary>
        public float DetourMargin = 0.04f;

        /// <summary>Пауза в точке: остановиться и подождать.</summary>
        public bool Pause;
        /// <summary>Длительность паузы, с.</summary>
        public float PauseSeconds = 2f;

        /// <summary>Есть ли хотя бы одно ограничение.</summary>
        public bool Any
        {
            get { return Orientation.Active || LimitSpeed || Detour || Pause; }
        }

        public KvWaypointLimits Clone()
        {
            return new KvWaypointLimits
            {
                Orientation = Orientation != null ? Orientation.Clone() : new KvOrientConstraint(),
                LimitSpeed = LimitSpeed,
                MaxSpeedMps = MaxSpeedMps,
                Detour = Detour,
                DetourCenter = DetourCenter,
                DetourRadius = DetourRadius,
                DetourMargin = DetourMargin,
                Pause = Pause,
                PauseSeconds = PauseSeconds
            };
        }

        public void Clear()
        {
            Orientation = new KvOrientConstraint();
            LimitSpeed = false;
            Detour = false;
            Pause = false;
        }

        /// <summary>Короткое описание для дерева, свойств и журнала.</summary>
        public string Describe()
        {
            if (!Any) return "нет";
            var parts = new List<string>();
            if (Orientation.Active) parts.Add("ориентация: " + Orientation.Describe());
            if (LimitSpeed) parts.Add("скорость ≤ " + MaxSpeedMps.ToString("0.00") + " м/с");
            if (Detour) parts.Add("обход препятствия R " + DetourRadius.ToString("0.00") +
                                 " м в (" + DetourCenter.x.ToString("0.00") + ", " +
                                 DetourCenter.y.ToString("0.00") + ", " +
                                 DetourCenter.z.ToString("0.00") + ")");
            if (Pause) parts.Add("пауза " + PauseSeconds.ToString("0.0") + " с");
            return string.Join(" · ", parts.ToArray());
        }

        /// <summary>Короткая метка для маркера/строки списка.</summary>
        public string Short()
        {
            if (!Any) return "";
            string s = "";
            if (Orientation.Active) s += "◈";
            if (LimitSpeed) s += "↓";
            if (Detour) s += "⟲";
            if (Pause) s += "❚❚";
            return s;
        }
    }

    /// <summary>Ограничение скорости в конкретном сэмпле маршрута.</summary>
    public struct KvSpeedCap
    {
        public int Sample;
        public float MaxMps;
        public string Label;
    }

    /// <summary>Пауза в конкретном сэмпле маршрута (вставляется как выдержка).</summary>
    public struct KvDwell
    {
        public int AfterSample;
        public float Seconds;
        public string Label;
    }

    /// <summary>
    /// ИНСТРУМЕНТЫ МАРШРУТА С ОГРАНИЧЕНИЯМИ (этап 7): измерение расстояния до препятствия,
    /// ограничение скорости в точках и вставка пауз. Все расчёты — на готовом
    /// `PlannedTrajectory` штатного планировщика, ядро не меняется.
    /// </summary>
    public static class KvWaypointRouteKit
    {
        /// <summary>Минимальное расстояние цепочки TCP до точки (препятствия), м.</summary>
        public static float MinDistanceToPoint(PoseValidator v, PlannedTrajectory plan,
            Vector3 center, out int index)
        {
            index = -1;
            if (v == null || !v.Ready || plan == null || plan.Path == null) return float.MaxValue;
            float best = float.MaxValue;
            for (int i = 0; i < plan.Path.Length; i++)
            {
                float d = Vector3.Distance(v.TcpAt(plan.Path[i]), center);
                if (d < best) { best = d; index = i; }
            }
            return best;
        }

        /// <summary>Скорость TCP (м/с) в сэмпле плана: по полилинии TCP и временам плана.</summary>
        public static float TcpSpeedAt(PoseValidator v, PlannedTrajectory plan, int index, int window = 1)
        {
            if (v == null || !v.Ready || plan == null || plan.Path == null) return 0f;
            if (plan.Times == null || plan.Times.Length != plan.Path.Length) return 0f;
            int n = plan.Path.Length;
            int a = Mathf.Max(0, index - Mathf.Max(1, window));
            int b = Mathf.Min(n - 1, index + Mathf.Max(1, window));
            float dt = plan.Times[b] - plan.Times[a];
            if (dt < 1e-4f || a == b) return 0f;
            float dist = Vector3.Distance(v.TcpAt(plan.Path[a]), v.TcpAt(plan.Path[b]));
            return dist / dt;
        }

        /// <summary>
        /// ОГРАНИЧЕНИЕ СКОРОСТИ В ТОЧКАХ: если фактическая скорость TCP в точке больше заданной,
        /// весь профиль времени пересчитывается по лимитам с подходящим масштабом (до 3 попыток).
        /// Профиль считается тем же `KvTrajMath.Retime`, что и время-оптимальные траектории
        /// (этап 5), поэтому ограничения суставов соблюдаются.
        /// </summary>
        public static bool EnforceSpeedCaps(PoseValidator v, PlannedTrajectory plan,
            KvMotionLimits limits, List<KvSpeedCap> caps, out string note)
        {
            note = "";
            if (v == null || !v.Ready || plan == null || caps == null || caps.Count == 0) return true;

            float scale = 1f;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float worst = 1f;
                string worstLabel = "";
                foreach (KvSpeedCap cap in caps)
                {
                    if (cap.Sample < 0 || cap.Sample >= plan.Path.Length) continue;
                    // Скорость меряется по ОКНУ из трёх сэмплов: одиночная разность на
                    // плотной выборке даёт локальные «языки», которые оператор не увидит.
                    float speed = Mathf.Max(TcpSpeedAt(v, plan, cap.Sample, 1),
                                            TcpSpeedAt(v, plan, cap.Sample, 2));
                    if (cap.MaxMps <= 1e-4f) continue;
                    float ratio = speed / cap.MaxMps;
                    if (ratio > worst) { worst = ratio; worstLabel = cap.Label; }
                }
                if (worst <= 1.02f)
                {
                    note = caps.Count + " огранич. скорости соблюдено (масштаб профиля " +
                           scale.ToString("0.00") + ")";
                    return true;
                }

                // Запас 8 %: после перепараметризации шаг сэмплов меняется и «язык» может
                // оказаться чуть выше лимита — лучше замедлить чуть сильнее.
                float nextScale = scale * (1f / worst) * 0.92f;
                PlannedTrajectory retimed = KvTrajMath.Retime(v, plan,
                    limits != null ? limits : new KvMotionLimits(), 1f, nextScale,
                    plan.Label, true);
                if (retimed == null)
                {
                    note = "профиль по лимитам не пересчитан (планировщик вернул null)";
                    return false;
                }
                // Геометрия та же — переносим только времена.
                plan.Times = retimed.Times;
                plan.Time = retimed.Time;
                scale = nextScale;
                note = worstLabel + ": скорость была выше заданной — профиль замедлен до ×" +
                       scale.ToString("0.00");
            }
            return false;
        }

        /// <summary>
        /// ВСТАВИТЬ ПАУЗЫ В МАРШРУТ: в позицию сэмпла добавляется столько же сэмплов той же
        /// позы, сколько нужно на выдержку (шаг 0.05 с). Исполнитель ведёт робота по времени,
        /// поэтому дубли позы = реальная остановка и ожидание (ТЗ этапа 7).
        ///
        /// ИСПРАВЛЕНО 30.10.2026 (аудит S43, дефект «пауза не работает как пауза»):
        ///
        /// 1) ДУБЛЬ ПОЗЫ БЫЛ ТОЙ ЖЕ ССЫЛКОЙ. Стояло `path.Add(plan.Path[i])` — массив `double[]`
        ///    клался в список ЕЩЁ РАЗ, а не копировался. Дальше любой код, который правит позу в
        ///    одном месте, правит её «во всех дублях» — выдержка превращается в незаметное
        ///    изменение всей траектории. Теперь поза КОПИРУЕТСЯ.
        ///
        /// 2) ВРЕМЕНА ДУБЛЕЙ НЕ ОТСЧИТЫВАЛИСЬ ОТ САМОГО ПЛАНА. Стояло `последнее + step` для
        ///    КАЖДОГО сэмпла: время маршрута пересобиралось с нуля шагом 0,05 с, независимо от
        ///    того, каким шагом шёл план. То есть функция заодно меняла время ВСЕГО маршрута, а
        ///    не только вставляла паузу. Теперь время сэмпла сохраняется, а пауза добавляется
        ///    сверху — ровно столько, сколько просили.
        ///
        /// ЧЕГО ЭТА ФУНКЦИЯ НЕ ДЕЛАЕТ: не нормирует выдержку под скорость исполнителя — это
        /// отдельный шаг (`MotionTiming.RescaleToSpeed`), и он масштабирует весь прогон вместе с
        /// паузой. Абсолютная пауза в секундах реального движения получается только ПОСЛЕ
        /// нормировки; здесь выдержка — в единицах времени маршрута.
        /// </summary>
        public static PlannedTrajectory InsertDwells(PlannedTrajectory plan, List<KvDwell> dwells,
            float timeStep = 0.05f)
        {
            if (plan == null || plan.Path == null || dwells == null || dwells.Count == 0) return plan;
            if (plan.Times == null || plan.Times.Length != plan.Path.Length) KvTrajMath.EnsureTimes(plan);

            var path = new List<double[]>(plan.Path.Length + dwells.Count * 20);
            var times = new List<float>(plan.Path.Length + dwells.Count * 20);
            float step = Mathf.Max(0.02f, timeStep);

            // СДВИГ ВРЕМЕНИ, НАКОПЛЕННЫЙ ВЫДЕРЖКАМИ (исправлено 30.10.2026 по прогону тестов).
            //
            // ПОЧЕМУ ОН НУЖЕН. Если брать время каждого сэмпла прямо из плана (`plan.Times[i]`),
            // то время после вставленной паузы оказывается МЕНЬШЕ времени последнего дубля:
            // сэмпл 5 приходил в 0,02 с, хотя предыдущая пауза уже довела счёт до 0,20 с. Время
            // шло НАЗАД — а исполнитель ведёт робота по времени и на убывающем времени зависает
            // или дёргается.
            //
            // Правило: время сэмпла = его время в плане + всё, что уже добавили паузы до него.
            // Тогда порядок сохраняется, а приращение между сэмплами остаётся тем, что построил
            // планировщик (функция вставляет паузу, а не переписывает тайминг маршрута).
            float shift = 0f;

            for (int i = 0; i < plan.Path.Length; i++)
            {
                // КОПИЯ, А НЕ ССЫЛКА: иначе правка позы в одном месте меняет все её дубли.
                path.Add(CopyPose(plan.Path[i]));
                times.Add(plan.Times[i] + shift);

                // Пауза ПОСЛЕ этого сэмпла: дублируем позу и растягиваем время.
                foreach (KvDwell dwell in dwells)
                {
                    if (dwell.AfterSample != i) continue;
                    if (dwell.Seconds <= 0f) continue;
                    int copies = Mathf.Clamp(Mathf.RoundToInt(dwell.Seconds / step), 1, 400);
                    float previous = times[times.Count - 1];
                    for (int k = 0; k < copies; k++)
                    {
                        path.Add(CopyPose(plan.Path[i]));
                        previous += step;
                        times.Add(previous);
                    }
                    shift = previous - plan.Times[i];
                }
            }

            PlannedTrajectory result = KvTrajMath.Clone(plan, plan.Label);
            result.Path = path.ToArray();
            result.Times = times.ToArray();
            result.Time = times[times.Count - 1];
            return result;
        }

        /// <summary>
        /// КОПИЯ ПОЗЫ. `double[]` — ссылочный тип: без явной копии все дубли паузы оказываются
        /// ОДНИМ И ТЕМ ЖЕ массивом, и любая последующая правка (нормировка, ограничение лимитов)
        /// меняет их разом. Именно это превращало выдержку в незаметный сдвиг всей траектории
        /// (найдено аудитом S43).
        /// </summary>
        public static double[] CopyPose(double[] pose)
        {
            if (pose == null) return new double[0];
            double[] copy = new double[pose.Length];
            System.Array.Copy(pose, copy, pose.Length);
            return copy;
        }
    }

    /// <summary>
    /// ВКЛАДКА «ОГРАНИЧЕНИЯ WAYPOINT» (ЭТАП 7 ТЗ): для выбранной промежуточной точки
    /// задаются ориентация TCP, скорость, обход препятствия и пауза; маршрут перестраивается
    /// штатным планировщиком с учётом этих ограничений.
    /// </summary>
    public class KvWaypointTab : IKvWorkbenchTab
    {
        private readonly KvWaypointManager waypoints;
        private readonly Func<TrajectoryFlowController> flowProvider;
        private readonly Func<Vector3> aimProvider;
        private readonly Action<int> selectWaypoint;

        public KvWaypointTab(KvWaypointManager manager, Func<TrajectoryFlowController> flow,
            Func<Vector3> aimPoint, Action<int> onSelect)
        {
            waypoints = manager;
            flowProvider = flow;
            aimProvider = aimPoint;
            selectWaypoint = onSelect;
        }

        public string Key { get { return "waypoints"; } }
        public string Title { get { return KvLocExtra2.T("wp.limits", "Ограничения точки"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra2.T(key, fallback);
        }

        private KvWaypoint Current()
        {
            if (waypoints == null) return null;
            int index = waypoints.SelectedIndex;
            if (index < 0 || index >= waypoints.Count) return null;
            return waypoints.Items[index];
        }

        public void Build(KvTabKit kit)
        {
            if (waypoints == null || kit == null) return;

            kit.Section(Title);
            KvWaypoint wp = Current();
            if (wp == null)
            {
                kit.Note(waypoints.Count == 0
                        ? T("wp.none", "Промежуточных точек нет: добавьте waypoint в точке прицела")
                        : T("wp.select", "Выберите точку в списке (кнопки ◀ ▶) или добавьте новую"),
                    KvTheme.Warn);
                kit.Buttons(new[] { "+ waypoint" },
                    new Action[] { delegate { waypoints.AddFromAim(); } });
                return;
            }

            kit.Info(delegate
            {
                KvWaypoint w = Current();
                return w == null ? "—" : w.Short + " · " + (w.Reachable ? "✓" : "✗ " + w.Note);
            }, KvTheme.TextMain);
            kit.Info(delegate
            {
                KvWaypoint w = Current();
                return w == null ? "—" : T("wp.status", "Ограничения точки") + ": " + w.Limits.Describe();
            }, KvTheme.Accent);

            // --- список точек: выбрать/переставить/удалить
            kit.Buttons(new[]
            {
                "◀", "▶", T("common.cancel", "Отмена") + " ✕", "+ waypoint"
            }, new Action[]
            {
                delegate { Move(-1); },
                delegate { Move(1); },
                delegate { waypoints.RemoveSelected(); },
                delegate { waypoints.AddFromAim(); }
            });

            // ---------------------------------------------------------- ориентация
            kit.Section(T("wp.orient", "Требуемая ориентация TCP"));
            kit.Toggle(T("wp.orient", "Требуемая ориентация TCP"),
                wp.Limits.Orientation.Active, delegate (bool v)
                {
                    KvWaypoint w = Current();
                    if (w == null) return;
                    w.Limits.Orientation.mode = v ? KvOrientMode.ToolDirection : KvOrientMode.None;
                    if (v && w.Limits.Orientation.direction.sqrMagnitude < 1e-6f)
                        w.Limits.Orientation.direction = Vector3.down;
                    Apply();
                });
            kit.Slider(T("wp.orient.tol", "Допуск ориентации, °"), 1f, 45f,
                wp.Limits.Orientation.toleranceDeg, "0.0", delegate (float v)
                {
                    KvWaypoint w = Current();
                    if (w != null) w.Limits.Orientation.toleranceDeg = v;
                });
            kit.Info(delegate
            {
                KvWaypoint w = Current();
                if (w == null) return "—";
                Vector3 d = w.Limits.Orientation.direction;
                return T("wp.orient.dir", "Направление оси инструмента") + ": (" +
                       d.x.ToString("0.00") + ", " + d.y.ToString("0.00") + ", " +
                       d.z.ToString("0.00") + ")";
            }, KvTheme.TextMain);
            kit.Buttons(new[] { "−Y ↓", "+Y ↑", "−Z", "+Z", "−X", "+X" }, new Action[]
            {
                delegate { SetDirection(Vector3.down); },
                delegate { SetDirection(Vector3.up); },
                delegate { SetDirection(Vector3.back); },
                delegate { SetDirection(Vector3.forward); },
                delegate { SetDirection(Vector3.left); },
                delegate { SetDirection(Vector3.right); }
            });
            kit.Buttons(new[] { T("wp.orient.now", "Ориентация ← как сейчас") },
                new Action[] { delegate { TakeCurrentOrientation(); } });

            // ---------------------------------------------------------- скорость
            kit.Section(T("wp.speed", "Ограничение скорости"));
            kit.Toggle(T("wp.speed.value", "Макс. скорость TCP в точке, м/с"),
                wp.Limits.LimitSpeed, delegate (bool v)
                {
                    KvWaypoint w = Current();
                    if (w == null) return;
                    w.Limits.LimitSpeed = v;
                    Apply();
                });
            kit.Slider(T("wp.speed.value", "Макс. скорость TCP в точке, м/с"), 0.01f, 0.6f,
                wp.Limits.MaxSpeedMps, "0.00", delegate (float v)
                {
                    KvWaypoint w = Current();
                    if (w != null) w.Limits.MaxSpeedMps = v;
                });

            // ---------------------------------------------------------- обход препятствия
            kit.Section(T("wp.detour", "Обязательный обход препятствия"));
            kit.Toggle(T("wp.detour", "Обязательный обход препятствия"),
                wp.Limits.Detour, delegate (bool v)
                {
                    KvWaypoint w = Current();
                    if (w == null) return;
                    w.Limits.Detour = v;
                    if (v) w.Limits.DetourCenter = AimPoint();
                    Apply();
                });
            kit.Slider(T("wp.detour.radius", "Радиус окрестности препятствия, м"), 0.03f, 0.6f,
                wp.Limits.DetourRadius, "0.00", delegate (float v)
                {
                    KvWaypoint w = Current();
                    if (w != null) w.Limits.DetourRadius = v;
                });
            kit.Slider(T("wp.detour.radius", "Радиус") + " + " + T("payload.safety", "запас"),
                0.0f, 0.2f, wp.Limits.DetourMargin, "0.00", delegate (float v)
                {
                    KvWaypoint w = Current();
                    if (w != null) w.Limits.DetourMargin = v;
                });
            kit.Buttons(new[] { T("wp.detour.point", "Центр из точки прицела") },
                new Action[] { delegate { SetDetourCenterFromAim(); } });

            // ---------------------------------------------------------- пауза
            kit.Section(T("wp.pause", "Пауза в точке"));
            kit.Toggle(T("wp.pause", "Пауза в точке"), wp.Limits.Pause, delegate (bool v)
            {
                KvWaypoint w = Current();
                if (w == null) return;
                w.Limits.Pause = v;
                Apply();
            });
            kit.Slider(T("wp.pause.value", "Длительность паузы, с"), 0.2f, 20f,
                wp.Limits.PauseSeconds, "0.0", delegate (float v)
                {
                    KvWaypoint w = Current();
                    if (w != null) w.Limits.PauseSeconds = v;
                });

            // ---------------------------------------------------------- действия
            kit.Divider();
            kit.Buttons(new[]
            {
                T("wp.apply", "Применить ограничения"),
                T("wp.clear", "Снять ограничения"),
                T("waypoint.build", "Перестроить маршрут")
            }, new Action[]
            {
                delegate { Apply(); },
                delegate
                {
                    KvWaypoint w = Current();
                    if (w != null) { w.Limits.Clear(); Apply(); }
                },
                delegate { waypoints.RebuildRoute(); }
            });

            kit.Info(delegate
            {
                return waypoints.HasRoute
                    ? waypoints.RouteNote
                    : T("waypoint.unreachable", "маршрут не построен") + ": " + waypoints.RouteNote;
            }, waypoints.HasRoute ? KvTheme.Ok : KvTheme.Warn);
        }

        private Vector3 AimPoint()
        {
            if (aimProvider != null)
            {
                Vector3 p = aimProvider();
                if (p.sqrMagnitude > 1e-6f) return p;
            }
            TrajectoryFlowController f = flowProvider != null ? flowProvider() : null;
            if (f != null && f.State != null && f.State.hasPoint) return f.State.point;
            return Vector3.zero;
        }

        private void SetDirection(Vector3 dir)
        {
            KvWaypoint w = Current();
            if (w == null) return;
            w.Limits.Orientation.mode = KvOrientMode.ToolDirection;
            w.Limits.Orientation.direction = dir.normalized;
            Apply();
        }

        /// <summary>Взять требуемую ориентацию из текущей позы робота (как сейчас стоит инструмент).</summary>
        private void TakeCurrentOrientation()
        {
            KvWaypoint w = Current();
            TrajectoryFlowController f = flowProvider != null ? flowProvider() : null;
            if (w == null || f == null || f.Validator == null || !f.Validator.Ready) return;
            double[] q = f.Validator.CopyCurrent();
            w.Limits.Orientation.mode = KvOrientMode.ToolDirection;
            w.Limits.Orientation.direction = KvToolKinematics.ToolAxis(f.Validator, q);
            Apply();
        }

        private void SetDetourCenterFromAim()
        {
            KvWaypoint w = Current();
            if (w == null) return;
            w.Limits.Detour = true;
            w.Limits.DetourCenter = AimPoint();
            Apply();
        }

        private void Move(int delta)
        {
            if (waypoints == null) return;
            int index = waypoints.SelectedIndex + delta;
            index = Mathf.Clamp(index, 0, Mathf.Max(0, waypoints.Count - 1));
            waypoints.Select(index);
            if (selectWaypoint != null) selectWaypoint(index);
        }

        /// <summary>Применить ограничения: пересчитать достижимость точки и перестроить маршрут.</summary>
        private void Apply()
        {
            if (waypoints == null) return;
            waypoints.EvaluateAll();
            waypoints.RebuildRoute();
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
