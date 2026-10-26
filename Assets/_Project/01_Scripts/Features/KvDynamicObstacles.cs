using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Оценка риска траектории из-за движущегося препятствия (ЭТАП 7 ТЗ).</summary>
    public enum KvPlanRisk
    {
        Unknown = 0,
        Clear,        // пересечения нет
        Risky,        // траектория проходит близко к пути препятствия — «рискованная»
        Collision     // траектория пересекает путь препятствия — отбрасывается
    }

    /// <summary>Результат проверки одной траектории.</summary>
    public struct KvRiskResult
    {
        public KvPlanRisk level;
        public float minDistance;      // минимальное расстояние «звено ↔ препятствие», м
        public float timeOfClosest;    // когда это произошло, с (от начала траектории)
        public Vector3 cartAtClosest;  // где в этот момент было препятствие
        public string reason;
    }

    /// <summary>Режим движения препятствия по маршруту.</summary>
    public enum KvObstacleMode
    {
        BackAndForth = 0,   // туда-обратно по отрезку (по умолчанию)
        Loop,               // по кругу
        OnceForward         // один проход и остановка
    }

    /// <summary>
    /// ЭТАП 7 ТЗ — ДИНАМИЧЕСКИЕ ПРЕПЯТСТВИЯ.
    ///
    /// ЧТО ДЕЛАЕТ:
    ///   * создаёт ДВИЖУЩИЙСЯ объект (тележка: платформа + мачта + маркерный огонь),
    ///     который едет по настраиваемому маршруту (`mode`: туда-обратно / по кругу / один проход,
    ///     `speed` м/с, `pauseSeconds` на концах);
    ///   * тележка — НАСТОЯЩЕЕ препятствие проекта: её рендереры попадают в `CollisionWorld`
    ///     штатным путём (мир собирается по рендерерам), поэтому планировщик учитывает её
    ///     при построении траекторий — как обычную геометрию сцены. При выключении
    ///     (`SetEnabled(false)`) объект выключается, а `CollisionWorld` собирается с
    ///     `FindObjectsInactive.Exclude` — значит препятствие исчезает из мира расчётов;
    ///   * ПРОГНОЗ движения: для каждой траектории считается, где препятствие будет в момент
    ///     прохода каждого сэмпла (`plan.Times[i]`), и берётся минимальное расстояние от цепочки
    ///     звеньев робота до габарита препятствия. Траектория помечается «рискованной»
    ///     (`Risky`) или отбрасывается (`Collision`) — по ТЗ, флагом `discardRisky`;
    ///   * РЕАКЦИЯ НА ПРИБЛИЖЕНИЕ: пока робот едет, проверяется, не подходит ли препятствие
    ///     к его текущей цепочке ближе `stopDistance`; тогда — предупреждение либо
    ///     АВАРИЙНАЯ ОСТАНОВКА (`autoStop`, по умолчанию включена) через переданный хабом
    ///     обработчик (тот же, что у кнопки «АВАРИЙНАЯ ОСТАНОВКА») — поток этапов не трогается.
    ///
    /// ДЛЯ SCARA всё то же самое: проверка идёт по цепочке звеньев из `PoseValidator`,
    /// поэтому 3 оси SCARA учитываются автоматически.
    ///
    /// МАРШРУТ виден оператору: линия маршрута рисуется служебным объектом
    /// (`HideFlags.HideInHierarchy`, без коллайдеров) и в мир столкновений не входит.
    /// </summary>
    public class KvDynamicObstacleService
    {
        public event Action<string> Message;

        // ------------------------------------------------------------------ параметры (ТЗ: настраиваемые)
        [Header("Маршрут (публичные параметры)")]
        public KvObstacleMode mode = KvObstacleMode.BackAndForth;
        public Vector3 pathA = new Vector3(-3.0f, 0f, -21.6f);
        public Vector3 pathB = new Vector3(3.0f, 0f, -21.6f);
        public float circleRadius = 2.5f;
        public float speed = 0.55f;              // м/с
        public float pauseSeconds = 1.0f;        // пауза на концах маршрута
        public float startPhase = 0f;            // 0..1 — где начинать

        [Header("Габарит препятствия")]
        public Vector3 cartSize = new Vector3(0.80f, 0.26f, 0.60f);
        public float mastHeight = 0.42f;

        [Header("Оценка риска")]
        public float warningDistance = 0.30f;    // ближе — «рискованная», м
        public float stopDistance = 0.10f;       // ближе — аварийная остановка, м
        public bool discardRisky = false;        // true — рискованные отбрасываются, false — помечаются
        public float checkInterval = 0.4f;       // как часто проверять варианты траекторий, с

        [Header("Реакция")]
        public bool autoStop = true;
        public bool logEvents = true;

        /// <summary>Аварийная остановка (передаётся хабом — это та же кнопка, что в тулбаре).</summary>
        public Func<bool> stopRequest;

        // ------------------------------------------------------------------ состояние
        private TrajectoryFlowController flow;
        private CollisionWorld world;
        private Transform root;
        private Transform cart;
        private Transform routeLine;
        private Material matBody, matMast, matLamp, matRoute;
        private readonly List<Transform> routeSegments = new List<Transform>();

        private float phase;             // 0..1 положение на маршруте
        private float pauseTimer;
        private float checkTimer;
        private float traveled;
        private bool enabled;
        private bool warned;
        private Vector3 lastPos;

        public bool Enabled { get { return enabled; } }
        public Vector3 Position { get { return cart != null ? cart.position : pathA; } }
        public string LastRisk { get; private set; }
        public float Traveled { get { return traveled; } }

        public KvDynamicObstacleService()
        {
            LastRisk = "";
        }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)
        {
            flow = controller;
            world = collisionWorld;
            Build();
        }

        // ================================================================== геометрия

        private void Build()
        {
            if (root != null) return;

            GameObject rootGo = new GameObject("KvDynamicObstacles");
            rootGo.hideFlags = HideFlags.HideInHierarchy;   // сама обёртка — служебная
            root = rootGo.transform;

            matBody = MakeMaterial(new Color(0.55f, 0.58f, 0.62f));
            matMast = MakeMaterial(new Color(0.30f, 0.32f, 0.36f));
            matLamp = MakeMaterial(new Color(1f, 0.55f, 0.10f));
            matRoute = MakeMaterial(new Color(1f, 0.75f, 0.20f));

            // --- тележка: платформа (плита → в CollisionWorld попадает БОКСОМ) + мачта + лампа.
            // ВАЖНО: тележка НЕ прячется в иерархии и НЕ парентится под служебный корень:
            // `CollisionWorld.IsServiceObject` считает служебным всё, у чего есть предок
            // с `HideInHierarchy`, и тогда препятствие молча выпало бы из мира расчётов.
            // Тележка — настоящее препятствие сцены: она видна и в иерархии, и в расчётах.
            GameObject cartGo = new GameObject("ДинамическоеПрепятствие_Тележка");
            cart = cartGo.transform;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ДинамическоеПрепятствие_Корпус";
            body.transform.SetParent(cart, false);
            body.transform.localPosition = new Vector3(0f, cartSize.y * 0.5f + 0.06f, 0f);
            body.transform.localScale = cartSize;
            Strip(body);
            Paint(body, matBody);

            GameObject mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "ДинамическоеПрепятствие_Мачта";
            mast.transform.SetParent(cart, false);
            mast.transform.localPosition = new Vector3(0f, cartSize.y + 0.06f + mastHeight * 0.5f, 0f);
            mast.transform.localScale = new Vector3(0.07f, mastHeight * 0.5f, 0.07f);
            Strip(mast);
            Paint(mast, matMast);

            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "ДинамическоеПрепятствие_Огонь";
            lamp.transform.SetParent(cart, false);
            lamp.transform.localPosition = new Vector3(0f, cartSize.y + 0.06f + mastHeight + 0.05f, 0f);
            lamp.transform.localScale = Vector3.one * 0.11f;
            Strip(lamp);
            Paint(lamp, matLamp);

            BuildRouteLine();
            cartGo.SetActive(false);
            if (routeLine != null) routeLine.gameObject.SetActive(false);
            lastPos = CartPositionAt(phase);
            if (cart != null) cart.position = lastPos;
        }

        private static void Strip(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) { c.enabled = false; Destroy(c); }   // коллайдеры не нужны: мир строится по рендерерам
        }

        private static void Paint(GameObject go, Material m)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;
            if (m != null) r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
        }

        private static Material MakeMaterial(Color color)
        {
            // ПАЙПЛАЙН БЕРЁТСЯ У ПРОЕКТА, А НЕ ЖЁСТКО. В проекте их два (решение [D9]: URP для
            // шлема, HDRP для компьютера), и шейдер чужого конвейера даёт розовую поверхность.
            // Фабрика выбирает шейдер и запасные варианты ТОГО ЖЕ конвейера и сама кладёт цвет
            // в правильное свойство (`_BaseColor` у URP/HDRP, `_Color` у встроенного Standard).
            Material m = KvRenderPipelineShaders.CreateLit("Материал_препятствия", color);
            if (m == null) return null;
            m.hideFlags = HideFlags.HideAndDontSave;
            if (m.HasProperty("_EmissiveColor") && color.r > 0.9f)
            {
                m.SetColor("_EmissiveColor", color * 2.5f);
                m.EnableKeyword("_EMISSION");
            }
            return m;
        }

        /// <summary>Линия маршрута — служебный объект (в мир столкновений не попадает).</summary>
        private void BuildRouteLine()
        {
            GameObject lineGo = new GameObject("KvObstacleRoute");
            lineGo.hideFlags = HideFlags.HideInHierarchy;
            routeLine = lineGo.transform;
            if (root != null) routeLine.SetParent(root, false);

            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "Seg" + i;
                seg.hideFlags = HideFlags.HideInHierarchy;
                Strip(seg);
                seg.transform.SetParent(routeLine, false);
                Renderer r = seg.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = matRoute;
                routeSegments.Add(seg.transform);
            }
        }

        private void UpdateRouteLine()
        {
            if (routeSegments.Count == 0) return;
            int n = routeSegments.Count;
            for (int i = 0; i < n; i++)
            {
                float u0 = i / (float)n;
                float u1 = (i + 1) / (float)n;
                Vector3 a = CartPositionAt(u0) + Vector3.up * 0.01f;
                Vector3 b = CartPositionAt(u1) + Vector3.up * 0.01f;
                Transform t = routeSegments[i];
                t.position = (a + b) * 0.5f;
                t.rotation = Quaternion.LookRotation(b - a, Vector3.up);
                t.localScale = new Vector3(0.05f, 0.006f, Vector3.Distance(a, b));
            }
        }

        // ================================================================== маршрут

        /// <summary>Позиция препятствия при фазе 0..1 (используется и прогнозом движения).</summary>
        public Vector3 CartPositionAt(float u)
        {
            u = Mathf.Repeat(u, 1f);
            switch (mode)
            {
                case KvObstacleMode.Loop:
                {
                    Vector3 c = (pathA + pathB) * 0.5f;
                    float r = circleRadius > 0.01f ? circleRadius : Vector3.Distance(pathA, pathB) * 0.5f;
                    float a = u * Mathf.PI * 2f;
                    return c + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                }
                default:
                {
                    // туда-обратно: u 0..0.5 — вперёд, 0.5..1 — назад
                    float k = u <= 0.5f ? u * 2f : (1f - u) * 2f;
                    return Vector3.Lerp(pathA, pathB, k);
                }
            }
        }

        /// <summary>Где препятствие будет через `seconds` секунд (прогноз для риска).</summary>
        public Vector3 CartPositionIn(float seconds)
        {
            return CartPositionAt(phase + (speed * seconds) / Mathf.Max(0.05f, RouteLength()));
        }

        private float RouteLength()
        {
            if (mode == KvObstacleMode.Loop)
                return Mathf.Max(0.5f, 2f * Mathf.PI * (circleRadius > 0.01f ? circleRadius : 2f));
            return Mathf.Max(0.5f, Vector3.Distance(pathA, pathB) * 2f);   // туда и обратно
        }

        // ================================================================== кадровое обслуживание

        public void Tick(float dt)
        {
            if (!enabled) return;
            if (cart == null) return;

            if (pauseTimer > 0f)
            {
                pauseTimer -= dt;
            }
            else
            {
                float len = Mathf.Max(0.05f, RouteLength());
                float du = (speed * dt) / len;
                phase += du;

                if (mode == KvObstacleMode.OnceForward)
                {
                    if (phase >= 1f) { phase = 1f; pauseTimer = float.MaxValue; }
                }
                else if (phase >= 1f)
                {
                    phase -= 1f;
                    if (pauseSeconds > 0f) pauseTimer = pauseSeconds;
                }
            }

            Vector3 p = CartPositionAt(phase);
            traveled += Vector3.Distance(p, lastPos);
            lastPos = p;
            cart.position = p;
            if (mode != KvObstacleMode.Loop)
            {
                Vector3 dir = pathB - pathA;
                if (dir.sqrMagnitude > 1e-5f) cart.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            checkTimer -= dt;
            if (checkTimer <= 0f)
            {
                checkTimer = Mathf.Max(0.05f, checkInterval);
                CheckFlowRisk();
            }
        }

        /// <summary>Проверка текущей ситуации: не мешает ли препятствие движению робота.</summary>
        private void CheckFlowRisk()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return;

            bool moving = flow.ExternalMotionRunning || flow.State.phase == FlowState.RobotMoving;
            float distance = DistanceFromChainToCart(flow.Validator, null);

            if (!moving)
            {
                warned = false;
                return;
            }

            if (distance <= stopDistance)
            {
                string text = KvLoc.T("obstacle.title", "Динамическое препятствие") +
                              ": расстояние до звеньев " + (distance * 100f).ToString("0") + " см — " +
                              (autoStop ? "АВАРИЙНАЯ ОСТАНОВКА" : "предупреждение");
                Say(text);
                if (autoStop && stopRequest != null) stopRequest();
                warned = true;
            }
            else if (distance <= warningDistance && !warned)
            {
                warned = true;
                Say(KvLoc.T("obstacle.title", "Динамическое препятствие") + ": приближается к роботу (" +
                    (distance * 100f).ToString("0") + " см) — траектория помечена как " +
                    KvLoc.T("obstacle.risky", "рискованная"));
            }
            else if (distance > warningDistance * 1.4f)
            {
                warned = false;
            }
        }

        /// <summary>
        /// Минимальное расстояние от цепочки звеньев робота (в позе `q` или в текущей) до
        /// габарита препятствия в его позиции `cartPos` (по умолчанию — текущей).
        /// </summary>
        public float DistanceFromChainToCart(PoseValidator v, double[] q)
        {
            Vector3 cartPos = Position;
            return DistanceFromChainToCartAt(v, q, cartPos);
        }

        private float DistanceFromChainToCartAt(PoseValidator v, double[] q, Vector3 cartPos)
        {
            if (v == null || !v.Ready) return float.MaxValue;

            double[] pose = q;
            if (pose == null)
            {
                pose = new double[v.Dof];
                v.CopyCurrentInto(pose);
            }

            // Габарит препятствия как AABB (маршруты в проекте осевые/круговые → этого достаточно)
            float halfY = (cartSize.y + 0.06f + mastHeight + 0.11f) * 0.5f;
            Vector3 center = cartPos + new Vector3(0f, halfY, 0f);
            Vector3 half = new Vector3(cartSize.x * 0.5f, halfY, cartSize.z * 0.5f) +
                           Vector3.one * v.linkRadius;

            float best = float.MaxValue;
            for (int i = 0; i < v.Dof; i++)
            {
                Vector3 p = v.PivotAt(i, pose);
                best = Mathf.Min(best, DistanceToBox(p, center, half));
            }
            best = Mathf.Min(best, DistanceToBox(v.TcpAt(pose), center, half));
            return best;
        }

        private static float DistanceToBox(Vector3 point, Vector3 center, Vector3 half)
        {
            Vector3 d = point - center;
            float dx = Mathf.Max(0f, Mathf.Abs(d.x) - half.x);
            float dy = Mathf.Max(0f, Mathf.Abs(d.y) - half.y);
            float dz = Mathf.Max(0f, Mathf.Abs(d.z) - half.z);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // ================================================================== оценка траекторий

        /// <summary>
        /// Оценить траекторию с учётом ДВИЖЕНИЯ препятствия: для каждого сэмпла берётся время
        /// `plan.Times[i]`, считается прогноз позиции препятствия и расстояние до звеньев робота
        /// в этой позе. Возвращает уровень риска и минимальное расстояние.
        /// </summary>
        public KvRiskResult Evaluate(PlannedTrajectory plan)
        {
            var result = new KvRiskResult();
            result.level = KvPlanRisk.Unknown;
            result.minDistance = float.MaxValue;
            result.reason = "";

            if (!enabled || plan == null || plan.Path == null || plan.Path.Length == 0)
                return result;
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return result;

            PoseValidator v = flow.Validator;
            float baseTime = plan.Times != null && plan.Times.Length == plan.Path.Length ? plan.Times[0] : 0f;
            float minDist = float.MaxValue;
            float closestTime = 0f;
            Vector3 closestCart = Position;

            for (int i = 0; i < plan.Path.Length; i++)
            {
                if (plan.Path[i] == null) continue;
                float t = plan.Times != null && i < plan.Times.Length ? plan.Times[i] - baseTime : 0f;
                Vector3 cartPos = CartPositionIn(t);
                float d = DistanceFromChainToCartAt(v, plan.Path[i], cartPos);
                if (d < minDist)
                {
                    minDist = d;
                    closestTime = t;
                    closestCart = cartPos;
                }
            }

            result.minDistance = minDist;
            result.timeOfClosest = closestTime;
            result.cartAtClosest = closestCart;
            if (minDist <= 0f)
            {
                result.level = KvPlanRisk.Collision;
                result.reason = "траектория пересекает путь препятствия";
            }
            else if (minDist <= warningDistance)
            {
                result.level = KvPlanRisk.Risky;
                result.reason = "проходит в " + (minDist * 100f).ToString("0") + " см от препятствия";
            }
            else
            {
                result.level = KvPlanRisk.Clear;
                result.reason = "минимальный интервал " + (minDist * 100f).ToString("0") + " см";
            }
            return result;
        }

        /// <summary>
        /// Оценить все текущие варианты траекторий потока и записать результат в журнал.
        /// Помеченные «рискованными» видны в статусе; при `discardRisky` сообщается об отбраковке.
        /// </summary>
        public void EvaluateVariants()
        {
            if (!enabled || flow == null) return;
            SelectionState state = flow.State;
            if (state == null || state.candidates == null || state.candidates.Count == 0) return;

            int risky = 0, bad = 0;
            string worst = "";
            float worstDistance = float.MaxValue;
            for (int i = 0; i < state.candidates.Count; i++)
            {
                PlannedTrajectory plan = state.candidates[i].plan;
                if (plan == null) continue;
                KvRiskResult r = Evaluate(plan);
                if (r.level == KvPlanRisk.Collision) { bad++; if (r.minDistance < worstDistance) worstDistance = r.minDistance; }
                else if (r.level == KvPlanRisk.Risky) { risky++; if (r.minDistance < worstDistance) worstDistance = r.minDistance; }
                if (r.level != KvPlanRisk.Clear && string.IsNullOrEmpty(worst))
                    worst = "Траектория " + (i + 1) + ": " + r.reason;
            }

            if (risky == 0 && bad == 0)
            {
                LastRisk = "";
                return;
            }

            LastRisk = KvLoc.T("obstacle.title", "Динамические препятствия") + ": " +
                       (bad > 0 ? "пересечение: " + bad + " · " : "") +
                       (risky > 0 ? "рискованных: " + risky : "") + " · " + worst;

            if (bad > 0 || risky > 0)
                Say(LastRisk + (discardRisky
                    ? " · рискованные ОТБРАСЫВАЮТСЯ (флаг)"
                    : " · траектории помечены, движение разрешено"));

            if (discardRisky && (bad > 0 || risky > 0) && !flow.ExternalMotionRunning)
            {
                // Отбраковка: снимаем выбор варианта и уходим к списку траекторий заново.
                flow.ResetFlow("опасные траектории отброшены: препятствие на пути");
            }
        }

        // ================================================================== управление

        public void SetEnabled(bool value)
        {
            enabled = value;
            Build();
            if (cart != null) cart.gameObject.SetActive(value);
            if (routeLine != null) routeLine.gameObject.SetActive(value);
            if (value)
            {
                UpdateRouteLine();
                lastPos = CartPositionAt(phase);
                if (cart != null) cart.position = lastPos;
                checkTimer = 0f;
                Say(KvLoc.T("obstacle.title", "Динамические препятствия") + ": " +
                    KvLoc.T("common.on", "включено") + " · " + ModeLabel() + " · " +
                    speed.ToString("0.00") + " м/с · маршрут " + pathA.ToString("0.0") + " → " +
                    pathB.ToString("0.0"));
            }
            else
            {
                Say(KvLoc.T("obstacle.title", "Динамические препятствия") + ": " +
                    KvLoc.T("common.off", "выключено") + " · препятствие убрано из мира расчётов");
            }
        }

        public bool Toggle()
        {
            SetEnabled(!enabled);
            return enabled;
        }

        private string ModeLabel()
        {
            switch (mode)
            {
                case KvObstacleMode.Loop: return "по кругу";
                case KvObstacleMode.OnceForward: return "один проход";
                default: return "туда-обратно";
            }
        }

        /// <summary>Сменить маршрут (публичные параметры меняются кодом/инспектором).</summary>
        public void SetRoute(Vector3 a, Vector3 b, float newSpeed)
        {
            pathA = a;
            pathB = b;
            speed = Mathf.Max(0.02f, newSpeed);
            UpdateRouteLine();
            Say("маршрут препятствия изменён: " + a.ToString("0.00") + " → " + b.ToString("0.00") +
                " · " + speed.ToString("0.00") + " м/с");
        }

        /// <summary>Строка состояния (статус-бар, панель, отчёт).</summary>
        public string Status
        {
            get
            {
                if (!enabled) return KvLoc.T("obstacle.title", "Динамические препятствия") +
                    ": " + KvLoc.T("common.off", "выключено");
                return KvLoc.T("obstacle.title", "Динамические препятствия") + ": " + ModeLabel() +
                       " · " + speed.ToString("0.00") + " м/с · пройдено " + traveled.ToString("0.0") +
                       " м · до звеньев " +
                       (flow != null && flow.Validator != null && flow.Validator.Ready
                           ? (DistanceFromChainToCart(flow.Validator, null) * 100f).ToString("0") + " см"
                           : "—") +
                       (string.IsNullOrEmpty(LastRisk) ? "" : " · " + LastRisk);
            }
        }

        public void Dispose()
        {
            SetEnabled(false);
            if (cart != null) Destroy(cart.gameObject);
            if (root != null) Destroy(root.gameObject);
            if (matBody != null) Destroy(matBody);
            if (matMast != null) Destroy(matMast);
            if (matLamp != null) Destroy(matLamp);
            if (matRoute != null) Destroy(matRoute);
            root = null;
            cart = null;
            routeLine = null;
            routeSegments.Clear();
        }

        private static void Destroy(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        private void Say(string message)
        {
            if (logEvents) Debug.Log("[Obstacle] " + message);
            if (Message != null) Message(message);
        }
    }
}
