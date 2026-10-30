using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Результат расчёта нагрузки (ЭТАП 10 ТЗ).</summary>
    public class KvPayloadResult
    {
        public bool valid;
        public float maxKg;               // максимальная нагрузка в текущей позе, кг
        public int limitingJoint = -1;    // какой сустав ограничивает
        public float limitingValue;       // момент (Н·м) или сила (Н) в limitingJoint
        public float limitingRating;      // номинальное значение этого сустава
        public float leverM;              // плечо до TCP у ограничивающего сустава, м
        public float tcpDistance;         // расстояние TCP от базы, м
        public string why = "";

        public float[] jointLoad;         // доля использования каждого сустава при 1 кг, 0..∞
        public float[] distances;         // кривая: расстояние от базы, м
        public float[] payloads;          // кривая: нагрузка, кг

        public string Line()
        {
            if (!valid) return why;
            return maxKg.ToString("0.00") + " кг · ограничивает ось " + (limitingJoint + 1) +
                   " (" + limitingValue.ToString("0.0") + " из " +
                   limitingRating.ToString("0.0") + ") · плечо " + leverM.ToString("0.000") +
                   " м · TCP " + tcpDistance.ToString("0.000") + " м от базы";
        }
    }

    /// <summary>Модель привода: номинальные моменты (Н·м) и массы звеньев (кг).</summary>
    public class KvPayloadModel
    {
        /// <summary>Номинальный момент сустава (Н·м) либо усилие призматической оси (Н).</summary>
        public float[] rating = { 95f, 95f, 55f, 16f, 11f, 7f };
        /// <summary>Масса, приводимая к суставу (звенья, кг).</summary>
        public float[] linkMass = { 9.0f, 6.5f, 3.6f, 1.5f, 0.8f, 0.4f };
        /// <summary>Номинальное усилие призматической оси (Н): у SCARA призма держит вес руки.</summary>
        public float ratingPrismaticN = 600f;
        /// <summary>Коэффициент запаса (ТЗ: инженерная оценка).</summary>
        public float safety = 1.5f;
        /// <summary>Масса самого захвата/инструмента, кг (считается как часть нагрузки).</summary>
        public float toolMassKg = 0.4f;

        public void CopyFrom(KvPayloadModel other)
        {
            if (other == null) return;
            rating = (float[])other.rating.Clone();
            ratingPrismaticN = other.ratingPrismaticN;
            linkMass = (float[])other.linkMass.Clone();
            safety = other.safety;
            toolMassKg = other.toolMassKg;
        }
    }

    /// <summary>
    /// ЭТАП 10 ТЗ: КАЛЬКУЛЯТОР НАГРУЗКИ (PAYLOAD CALCULATOR).
    ///
    /// Что считает:
    ///   • МАКСИМАЛЬНУЮ НАГРУЗКУ в ТЕКУЩЕЙ ПОЗЕ: для каждого сустава берётся момент от веса
    ///     груза (масса × g × плечо до TCP, спроецированный на ось сустава) плюс вклад веса
    ///     звеньев, и сравнивается с номинальным моментом с коэффициентом запаса. Нагрузка
    ///     ограничена САМЫМ нагруженным суставом — он и показывается;
    ///   • ГРАФИК зависимости от РАССТРОЯНИЯ ОТ БАЗЫ (ТЗ): для ряда расстояний решается IK
    ///     в том же направлении и на той же высоте, что текущий TCP, и для каждой позы
    ///     считается допустимая нагрузка. Если IK для расстояния не сходится, точка кривой
    ///     пропускается (это честно видно по графику);
    ///   • результат показывается во вкладке верстака И В ПАНЕЛИ СВОЙСТВ РОБОТА (ТЗ).
    ///
    /// Модель — инженерная оценка (статика, без динамики и трения): годится для планирования
    /// захвата и для сравнения поз, а не для паспортных расчётов. Это написано и в интерфейсе.
    /// </summary>
    public class KvPayloadCalculator
    {
        public const string SafetyPrefsKey = "KazistovVv.Payload.Safety";
        public const string ToolMassPrefsKey = "KazistovVv.Payload.ToolMass";

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private CollisionWorld world;
        private readonly KvPayloadModel model = new KvPayloadModel();
        private bool loaded;

        private KvPayloadResult last = new KvPayloadResult();
        private float timer;
        private string signature = "";
        private int curveSamples = 14;
        private float curveMaxDistance = 0.9f;

        public KvPayloadModel Model { get { Load(); return model; } }
        public KvPayloadResult Last { get { return last; } }
        public TrajectoryFlowController Flow() { return flow; }
        public int CurveSamples { get { return curveSamples; } set { curveSamples = Mathf.Clamp(value, 4, 40); } }

        private void Load()
        {
            if (loaded) return;
            loaded = true;
            model.safety = Mathf.Clamp(PlayerPrefs.GetFloat(SafetyPrefsKey, model.safety), 1f, 5f);
            model.toolMassKg = Mathf.Clamp(PlayerPrefs.GetFloat(ToolMassPrefsKey, model.toolMassKg),
                0f, 20f);
        }

        public void Save()
        {
            Load();
            PlayerPrefs.SetFloat(SafetyPrefsKey, model.safety);
            PlayerPrefs.SetFloat(ToolMassPrefsKey, model.toolMassKg);
            PlayerPrefs.Save();
        }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)
        {
            flow = controller;
            world = collisionWorld;
            Load();
        }

        /// <summary>Кадровое обслуживание: пересчёт при заметном изменении позы (не каждый кадр).</summary>
        public void Tick(float deltaTime)
        {
            timer -= deltaTime;
            if (timer > 0f) return;
            timer = 0.4f;

            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                last = new KvPayloadResult { why = "робот не определён" };
                return;
            }
            double[] q = flow.Validator.CopyCurrent();
            string current = Signature(q);
            if (current == signature) return;
            signature = current;
            Evaluate(q);
        }

        private string Signature(double[] q)
        {
            if (q == null) return "";
            string s = "";
            for (int i = 0; i < q.Length; i++) s += q[i].ToString("0.0") + ",";
            return s + "|" + model.safety.ToString("0.00") + "|" + model.toolMassKg.ToString("0.00");
        }

        /// <summary>Пересчитать нагрузку для конкретной позы (используется и вкладкой, и свойствами).</summary>
        public KvPayloadResult Evaluate(double[] q)
        {
            KvPayloadResult result = new KvPayloadResult();
            if (flow == null || flow.Validator == null || !flow.Validator.Ready || q == null)
            {
                result.why = "робот не определён";
                last = result;
                return result;
            }

            PoseValidator v = flow.Validator;
            Vector3 basePos = v.BasePosition;
            Vector3 tcp = v.TcpAt(q);
            const float g = 9.81f;

            result.tcpDistance = new Vector2(tcp.x - basePos.x, tcp.z - basePos.z).magnitude;
            result.jointLoad = new float[v.Dof];
            float best = float.MaxValue;
            int limiting = -1;
            float limitingValue = 0f, limitingRating = 1f, limitingLever = 0f;

            float safety = Mathf.Max(1f, model.safety);

            for (int j = 0; j < v.Dof; j++)
            {
                float rating = RatingOf(j);
                if (rating <= 0.01f) { result.jointLoad[j] = 0f; continue; }

                // --- момент/сила от ВЕСА ЗВЕНЬЕВ (приближённо: масса на плече до TCP)
                float linkLoad = 0f;
                float lever = 0f;
                Vector3 pivot = v.PivotAt(j, q);

                if (v.IsPrismatic(j))
                {
                    // Призматическая ось держит вес целиком (вертикальная призма SCARA):
                    // номинальное значение здесь — СИЛА в ньютонах, а не момент.
                    linkLoad = DistalMass(v, q, j) * g;
                    rating = model.ratingPrismaticN;
                    lever = 0f;
                    float forcePerKg = g;
                    float free = rating / safety - linkLoad;
                    float kg = free / forcePerKg;
                    result.jointLoad[j] = 1f / Mathf.Max(0.001f, kg);
                    if (kg < best)
                    {
                        best = kg;
                        limiting = j;
                        limitingValue = linkLoad + kg * forcePerKg;
                        limitingRating = rating;
                        limitingLever = 0f;
                    }
                    continue;
                }

                // Момент от точечной массы в TCP: |r × F|, спроецированный на ось сустава.
                Vector3 axis = v.AxisWorld(j, q).normalized;
                Vector3 r = tcp - pivot;
                float torquePerKg = Mathf.Abs(Vector3.Dot(Vector3.Cross(r, Vector3.up * g), axis));
                lever = Vector3.ProjectOnPlane(r, axis).magnitude;

                // Вклад звеньев: каждое звено берётся в СВОЁМ центре (середина между его
                // пивотом и следующим) — «середина пути до TCP» для всех звеньев сразу
                // сильно завышала момент и обнуляла допустимую нагрузку (найдено прогоном).
                for (int k = j; k < v.Dof; k++)
                {
                    float mass = model.linkMass[Math.Min(k, model.linkMass.Length - 1)];
                    if (mass <= 0.001f) continue;
                    Vector3 rk = LinkCenter(v, q, k) - pivot;
                    linkLoad += mass * Mathf.Abs(Vector3.Dot(Vector3.Cross(rk, Vector3.up * g), axis));
                }

                float freeTorque = rating / safety - linkLoad;
                float kgJoint = torquePerKg > 1e-5f ? freeTorque / torquePerKg : float.MaxValue;
                result.jointLoad[j] = torquePerKg / Mathf.Max(1e-5f, rating / safety);

                if (kgJoint < best)
                {
                    best = kgJoint;
                    limiting = j;
                    limitingValue = linkLoad + kgJoint * torquePerKg;
                    limitingRating = rating;
                    limitingLever = lever;
                }
            }

            result.valid = limiting >= 0 && best < float.MaxValue;
            result.maxKg = result.valid ? Mathf.Max(0f, best - Mathf.Max(0f, model.toolMassKg)) : 0f;
            result.limitingJoint = limiting;
            result.limitingValue = limitingValue;
            result.limitingRating = limitingRating;
            result.leverM = limitingLever;
            if (!result.valid) result.why = "не удалось оценить нагрузку (нет данных о суставах)";

            BuildCurve(q, result);
            last = result;
            return result;
        }

        /// <summary>Центр звена k: середина между его пивотом и следующим пивотом (или TCP).</summary>
        private static Vector3 LinkCenter(PoseValidator v, double[] q, int k)
        {
            Vector3 a = v.PivotAt(k, q);
            Vector3 b = k + 1 < v.Dof ? v.PivotAt(k + 1, q) : v.TcpAt(q);
            return Vector3.Lerp(a, b, 0.5f);
        }

        // ================================================================== ЧИСТОЕ ЯДРО РАСЧЁТА

        /// <summary>
        /// МОМЕНТ ОТ ГРУЗА НА ВРАЩАТЕЛЬНОМ СУСТАВЕ (Н·м на килограмм груза).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНОЙ ФУНКЦИЕЙ (30.10.2026, аудит S43). Раньше эта формула — `|(r × F) · axis|`
        /// — жила ТОЛЬКО внутри `Evaluate`, который без живой сцены всегда выходит по охране
        /// `flow.Validator.Ready`. Поэтому всё физическое ядро расчёта нагрузки не проверялось
        /// НИ ОДНИМ тестом: в файле тестов стоял `Assert.Ignore` с длинным объяснением, почему это
        /// «невозможно». Формула стала чистой и проверяется напрямую — и арифметикой, и граничными
        /// случаями (груз над осью, груз на оси, сила вдоль оси).
        ///
        /// ФИЗИКА. Вес груза `m·g` действует вниз в точке TCP. Момент силы относительно сустава
        /// равен `r × F`, где `r` — вектор от пивота до TCP, а `F = (0, -mg, 0)`. На ось сустава
        /// берётся проекция. Отсюда: груз НАД осью вращения момента не даёт (плечо нулевое),
        /// а груз в стороне — даёт ровно `m·g·плечо`.
        /// </summary>
        /// <param name="pivot">положение сустава (мир)</param>
        /// <param name="tcp">положение инструмента с грузом (мир)</param>
        /// <param name="axis">ось вращения сустава (мир, не обязательно единичная)</param>
        /// <param name="gravity">ускорение свободного падения, обычно 9.81</param>
        /// <returns>абсолютный момент на килограмм груза; 0 при вырожденной оси</returns>
        public static float TorquePerKgOnAxis(Vector3 pivot, Vector3 tcp, Vector3 axis, float gravity = 9.81f)
        {
            Vector3 a = axis;
            // Нулевая ось — это отсутствие данных, а не «нулевой момент»: возвращаем 0 честно,
            // вызывающий увидит его как «плечо не определено» (см. degnerate-ветку Evaluate).
            if (a.sqrMagnitude < 1e-10f) return 0f;
            a = a.normalized;

            Vector3 r = tcp - pivot;
            Vector3 force = Vector3.down * gravity;
            return Mathf.Abs(Vector3.Dot(Vector3.Cross(r, force), a));
        }

        /// <summary>
        /// ПЛЕЧО ГРУЗА ОТНОСИТЕЛЬНО ОСИ (м) — расстояние от оси вращения до TCP в плоскости,
        /// перпендикулярной оси. Это то число, которое видит наладчик в подсказке «груз на плече».
        ///
        /// СВЯЗЬ С МОМЕНТОМ: для горизонтальной оси `TorquePerKgOnAxis == gravity · lever`, но не
        /// наоборот: если сила направлена ВДОЛЬ оси, плечо есть, а момента нет. Поэтому оба числа
        /// выдаются отдельно и не подменяют друг друга.
        /// </summary>
        public static float LeverOnAxis(Vector3 pivot, Vector3 tcp, Vector3 axis)
        {
            if (axis.sqrMagnitude < 1e-10f) return 0f;
            Vector3 r = tcp - pivot;
            return Vector3.ProjectOnPlane(r, axis.normalized).magnitude;
        }

        /// <summary>
        /// ДОПУСТИМЫЙ ГРУЗ ПО ОДНОМУ СУСТАВУ (кг) — вся арифметика «сколько можно повесить».
        ///
        /// ЗАЧЕМ ЗДЕСЬ. Три числа (`rating` — номинал, `safety` — запас, `linkLoad` — момент от
        /// веса самих звеньев, `torquePerKg` — момент от груза) полностью определяют ответ:
        /// `(rating / safety − linkLoad) / torquePerKg`. Раньше это было размазано по трём циклам
        /// (`Evaluate`, `MaxPayloadForPose`, `JointTorques`) и проверялось только через сцену.
        ///
        /// ЧЕГО ЭТА ФУНКЦИЯ НЕ ДЕЛАЕТ: не учитывает массу инструмента (её вычитает вызывающий),
        /// не знает про призматические оси (для них `torquePerKg` — это `g`, сила на килограмм,
        /// и формула та же). Знак `linkLoad` — по модулю: момент от веса звеньев всегда отнимает.
        /// </summary>
        /// <param name="rating">номинал сустава: Н·м для вращательного, Н для призматического</param>
        /// <param name="safety">коэффициент запаса (≥ 1)</param>
        /// <param name="linkLoad">момент/усилие от веса звеньев, положительное число</param>
        /// <param name="torquePerKg">момент/усилие на килограмм груза</param>
        /// <returns>допустимая масса груза; 0, если запаса нет вовсе</returns>
        public static float AllowedKg(float rating, float safety, float linkLoad, float torquePerKg)
        {
            if (rating <= 0.01f) return 0f;
            float s = Mathf.Max(1f, safety);
            float free = rating / s - Mathf.Max(0f, linkLoad);
            if (free <= 0f) return 0f;
            // Груз, стоящий ровно на оси, момента не даёт — ограничение по этой оси не наступает
            // никогда. Возвращаем очень большое число, а не 0: 0 здесь значил бы «нельзя ничего»,
            // хотя наоборот — можно сколько угодно (ограничат другие оси).
            if (torquePerKg <= 1e-5f) return float.MaxValue;
            return free / torquePerKg;
        }

        /// <summary>Суммарная масса, которую несёт сустав j (сам и все дистальные звенья), кг.</summary>
        private float DistalMass(PoseValidator v, double[] q, int j)
        {
            float sum = 0f;
            for (int k = j; k < v.Dof; k++)
                sum += model.linkMass[Math.Min(k, model.linkMass.Length - 1)];
            return sum;
        }
        private float RatingOf(int joint)
        {
            if (model.rating == null || model.rating.Length == 0) return 0f;
            return model.rating[Mathf.Clamp(joint, 0, model.rating.Length - 1)];
        }

        /// <summary>Кривая «нагрузка против расстояния от базы»: IK в том же направлении и на той же высоте.</summary>
        private void BuildCurve(double[] q, KvPayloadResult result)
        {
            PoseValidator v = flow.Validator;
            Vector3 basePos = v.BasePosition;
            Vector3 tcp = v.TcpAt(q);

            float height = tcp.y;
            Vector3 dir = new Vector3(tcp.x - basePos.x, 0f, tcp.z - basePos.z);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();

            int samples = curveSamples;
            var distances = new List<float>();
            var payloads = new List<float>();
            double[] seed = q;

            for (int i = 0; i < samples; i++)
            {
                float d = Mathf.Lerp(0.15f, curveMaxDistance, i / (float)(samples - 1));
                Vector3 point = basePos + dir * d + Vector3.up * (height - basePos.y);
                double[] qq;
                if (!v.SolveIk(point, seed, out qq)) continue;
                if (!v.WithinLimits(qq)) continue;

                float kg = MaxPayloadForPose(qq, v);
                if (kg < 0f) continue;
                distances.Add(d);
                payloads.Add(Mathf.Max(0f, kg - Mathf.Max(0f, model.toolMassKg)));
                seed = qq;
            }

            result.distances = distances.ToArray();
            result.payloads = payloads.ToArray();
        }

        /// <summary>Максимальная нагрузка для произвольной позы (без построения кривой).</summary>
        private float MaxPayloadForPose(double[] q, PoseValidator v)
        {
            const float g = 9.81f;
            float safety = Mathf.Max(1f, model.safety);
            Vector3 tcp = v.TcpAt(q);
            float best = float.MaxValue;

            for (int j = 0; j < v.Dof; j++)
            {
                float rating = RatingOf(j);
                if (rating <= 0.01f) continue;
                Vector3 pivot = v.PivotAt(j, q);

                if (v.IsPrismatic(j))
                {
                    float linkLoad = DistalMass(v, q, j) * g;
                    float kg = (model.ratingPrismaticN / safety - linkLoad) / g;
                    if (kg < best) best = kg;
                    continue;
                }

                Vector3 axis = v.AxisWorld(j, q).normalized;
                Vector3 r = tcp - pivot;
                float torquePerKg = Mathf.Abs(Vector3.Dot(Vector3.Cross(r, Vector3.up * g), axis));
                float linkTorque = 0f;
                for (int k = j; k < v.Dof; k++)
                {
                    float mass = model.linkMass[Math.Min(k, model.linkMass.Length - 1)];
                    if (mass <= 0.001f) continue;
                    Vector3 rk = LinkCenter(v, q, k) - pivot;
                    linkTorque += mass * Mathf.Abs(Vector3.Dot(Vector3.Cross(rk, Vector3.up * g), axis));
                }
                float freeTorque = rating / safety - linkTorque;
                float kgJoint = torquePerKg > 1e-5f ? freeTorque / torquePerKg : float.MaxValue;
                if (kgJoint < best) best = kgJoint;
            }
            return best == float.MaxValue ? -1f : best;
        }

        /// <summary>
        /// МОМЕНТЫ НА ОСЯХ В ТЕКУЩЕЙ ПОЗЕ (используется визуализацией сил, этап 17 ТЗ):
        /// для вращательных суставов — момент от веса звеньев и груза, спроецированный на ось
        /// сустава (со знаком, Н·м); для призматических — усилие вдоль оси (Н). Дополнительно
        /// выдаётся сила на инструменте (вес груза и захвата, Н) и доля от номинала (0…1+).
        /// </summary>
        public bool JointTorques(double[] q, float payloadKg, out float[] torque,
            out float[] load01, out Vector3 toolForce)
        {
            torque = new float[0];
            load01 = new float[0];
            toolForce = Vector3.zero;
            if (flow == null || flow.Validator == null || !flow.Validator.Ready || q == null)
                return false;

            Load();
            PoseValidator v = flow.Validator;
            const float g = 9.81f;
            float safety = Mathf.Max(1f, model.safety);
            float payload = Mathf.Max(0f, payloadKg) + Mathf.Max(0f, model.toolMassKg);

            torque = new float[v.Dof];
            load01 = new float[v.Dof];
            Vector3 tcp = v.TcpAt(q);
            toolForce = Vector3.down * (payload * g);

            for (int j = 0; j < v.Dof; j++)
            {
                Vector3 pivot = v.PivotAt(j, q);
                if (v.IsPrismatic(j))
                {
                    float force = DistalMass(v, q, j) * g + payload * g;
                    torque[j] = force;
                    float ratingN = Mathf.Max(1f, model.ratingPrismaticN);
                    load01[j] = force / (ratingN / safety);
                    continue;
                }

                Vector3 axis = v.AxisWorld(j, q).normalized;
                // Вес звеньев: каждое звено в своём центре.
                float tau = 0f;
                for (int k = j; k < v.Dof; k++)
                {
                    float mass = model.linkMass[Math.Min(k, model.linkMass.Length - 1)];
                    if (mass <= 0.001f) continue;
                    Vector3 rk = LinkCenter(v, q, k) - pivot;
                    tau += Vector3.Dot(Vector3.Cross(rk, Vector3.down * (mass * g)), axis);
                }
                // Вес груза и инструмента в TCP.
                tau += Vector3.Dot(Vector3.Cross(tcp - pivot, toolForce), axis);
                torque[j] = tau;

                float rating = Mathf.Max(1f, RatingOf(j));
                load01[j] = Mathf.Abs(tau) / (rating / safety);
            }
            return true;
        }

        /// <summary>Строка метрики для свойств робота и статус-бара.</summary>
        public string StatusLine()
        {
            if (last == null || !last.valid) return KvLocExtra2.T("payload.now",
                "Максимальная нагрузка в текущей позе, кг") + ": —";
            return KvLocExtra2.T("payload.now", "Максимальная нагрузка в текущей позе, кг") + ": " +
                   last.maxKg.ToString("0.00") + " кг · " +
                   KvLocExtra2.T("payload.limit", "Ограничивающий сустав") + " " +
                   (last.limitingJoint + 1) + " · " +
                   last.limitingValue.ToString("0.0") + "/" + last.limitingRating.ToString("0.0") +
                   " · " + KvLocExtra2.T("payload.distance", "Расстояние от базы, м") + " " +
                   last.tcpDistance.ToString("0.000");
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Payload] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «КАЛЬКУЛЯТОР НАГРУЗКИ» (ЭТАП 10 ТЗ): максимальная нагрузка в текущей позе,
    /// график зависимости от расстояния до базы и настройка модели приводов.
    /// </summary>
    public class KvPayloadTab : IKvWorkbenchTab
    {
        private readonly KvPayloadCalculator service;
        private KvMiniPlot plot;
        private RectTransform content;
        private float lastPlotTime;

        public KvPayloadTab(KvPayloadCalculator payload)
        {
            service = payload;
        }

        public string Key { get { return "payload"; } }
        public string Title { get { return KvLocExtra2.T("payload.title", "Калькулятор нагрузки"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra2.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (service == null || kit == null) return;
            content = kit.Content;

            kit.Section(Title);
            kit.Info(delegate { return service.StatusLine(); }, KvTheme.Accent);
            kit.Info(delegate
            {
                KvPayloadResult r = service.Last;
                if (r == null || !r.valid) return "";
                string perJoint = "";
                for (int i = 0; i < r.jointLoad.Length; i++)
                {
                    if (i > 0) perJoint += " · ";
                    perJoint += "J" + (i + 1) + " " + (r.jointLoad[i] * 100f).ToString("0") + "%";
                }
                return "загрузка суставов при номинале: " + perJoint;
            }, KvTheme.TextDim);

            // --- график «нагрузка от расстояния»
            kit.Section(T("payload.graph", "Нагрузка от расстояния от базы"));
            plot = KvMiniPlot.Create(kit.Content, "PayloadPlot", 420f, 150f);
            UpdatePlot(true);

            // --- модель
            kit.Section(T("payload.rating", "Номинальный момент суставов, Н·м"));
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                kit.Slider("J" + (index + 1), 1f, 300f, RatingAt(index), "0",
                    delegate (float v) { SetRating(index, v); });
            }
            kit.Slider(T("payload.safety", "Коэффициент запаса"), 1f, 4f,
                service.Model.safety, "0.00", delegate (float v)
                {
                    service.Model.safety = v;
                    service.Save();
                });
            kit.Slider(T("energy.payload", "Масса груза, кг") + " ← " + T("wb.metric", "инструмент"),
                0f, 10f, service.Model.toolMassKg, "0.00", delegate (float v)
                {
                    service.Model.toolMassKg = v;
                    service.Save();
                });

            kit.Buttons(new[] { T("wb.value", "Значение") + " ↻" },
                new Action[] { delegate { service.Evaluate(null); } });

            kit.Divider();
            kit.Note(T("payload.info",
                "Оценка: момент груза считается по фактическим плечам звеньев (узлы кинематики) " +
                "и номинальным моментам суставов, с учётом массы звеньев и коэффициента запаса. " +
                "Это инженерная оценка для планирования, а не паспортный расчёт."), KvTheme.TextDim);
        }

        private float RatingAt(int index)
        {
            float[] rating = service.Model.rating;
            return rating != null && index < rating.Length ? rating[index] : 10f;
        }

        private void SetRating(int index, float value)
        {
            float[] rating = service.Model.rating;
            if (rating == null || index >= rating.Length) return;
            rating[index] = value;
            service.Save();
        }

        private void UpdatePlot(bool force)
        {
            if (plot == null) return;
            if (!force && Time.unscaledTime - lastPlotTime < 0.5f) return;
            lastPlotTime = Time.unscaledTime;

            KvPayloadResult r = service.Last;
            plot.SetData(r != null ? r.distances : null, r != null ? r.payloads : null,
                T("payload.distance", "Расстояние от базы, м"),
                T("payload.now", "Максимальная нагрузка в текущей позе, кг"));
        }

        public void Tick() { UpdatePlot(false); }
        public void Refresh() { UpdatePlot(false); }
    }

    /// <summary>
    /// МИНИ-ГРАФИК (процедурная текстура): оси, подписи, ломаная по точкам.
    /// Используется калькулятором нагрузки (этап 10) — файлов-ассетов не требуется.
    /// </summary>
    public class KvMiniPlot : MonoBehaviour
    {
        private RawImage image;
        private Texture2D texture;
        private RectTransform root;
        private int width = 420;
        private int height = 150;

        private float[] xs;
        private float[] ys;
        private string xLabel = "";
        private string yLabel = "";

        public static KvMiniPlot Create(RectTransform parent, string name, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RawImage), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            KvMiniPlot plot = go.AddComponent<KvMiniPlot>();
            plot.Build(width, height);
            return plot;
        }

        private void Build(float w, float h)
        {
            root = (RectTransform)transform;
            width = Mathf.Clamp(Mathf.RoundToInt(w), 120, 900);
            height = Mathf.Clamp(Mathf.RoundToInt(h), 60, 400);

            image = GetComponent<RawImage>();
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            image.texture = texture;

            LayoutElement le = GetComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.minWidth = width;
            le.preferredWidth = width;
            root.sizeDelta = new Vector2(width, height);
            Redraw();
        }

        public void SetData(float[] distances, float[] payloads, string xTitle, string yTitle)
        {
            xs = distances;
            ys = payloads;
            xLabel = xTitle;
            yLabel = yTitle;
            Redraw();
        }

        private void Redraw()
        {
            if (texture == null) return;
            Color bg = KvTheme.InputBg;
            Color grid = KvTheme.Separator;
            Color line = KvTheme.Accent;
            Color text = KvTheme.TextDim;

            var px = new Color[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            // рамка и сетка
            for (int x = 0; x < width; x++) { px[x] = grid; px[(height - 1) * width + x] = grid; }
            for (int y = 0; y < height; y++) { px[y * width] = grid; px[y * width + width - 1] = grid; }
            for (int k = 1; k < 4; k++)
            {
                int y = height * k / 4;
                for (int x = 2; x < width - 2; x++) px[y * width + x] = grid * 0.7f;
            }

            if (ys != null && ys.Length > 1)
            {
                float maxY = 0f;
                for (int i = 0; i < ys.Length; i++) maxY = Mathf.Max(maxY, ys[i]);
                maxY = Mathf.Max(maxY, 0.25f) * 1.15f;

                float minX = float.MaxValue, maxX = float.MinValue;
                for (int i = 0; i < xs.Length; i++)
                {
                    minX = Mathf.Min(minX, xs[i]);
                    maxX = Mathf.Max(maxX, xs[i]);
                }
                if (maxX - minX < 1e-4f) maxX = minX + 1f;

                int prevX = -1, prevY = -1;
                for (int i = 0; i < ys.Length; i++)
                {
                    int pxX = 4 + Mathf.RoundToInt((xs[i] - minX) / (maxX - minX) * (width - 12));
                    int pxY = 4 + Mathf.RoundToInt(Mathf.Clamp01(ys[i] / maxY) * (height - 12));
                    if (prevX >= 0) DrawLine(px, prevX, prevY, pxX, pxY, line);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            Put(px, pxX + dx, pxY + dy, line);
                    prevX = pxX;
                    prevY = pxY;
                }
            }

            texture.SetPixels(px);
            texture.Apply(false);
        }

        private void DrawLine(Color[] px, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            int guard = 0;
            while (guard++ < 4000)
            {
                Put(px, x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        private void Put(Color[] px, int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            px[y * width + x] = color;
        }

        /// <summary>Подписи осей (текстом поверх графика — отдельными Text-элементами).</summary>
        public string AxisTitles { get { return xLabel + " / " + yLabel; } }
    }
}
