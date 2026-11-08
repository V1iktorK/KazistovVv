using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Вид имитируемого отказа (этап 34 ТЗ).</summary>
    public enum KvFailureKind
    {
        None = 0,
        /// <summary>Потеря момента в суставе: звено «провисает» под собственным весом.</summary>
        JointLoss = 1,
        /// <summary>Потеря связи с контроллером: команды не проходят, телеметрия устарела.</summary>
        CommsLoss = 2,
        /// <summary>Перегрузка: груз больше допустимого для текущей позы.</summary>
        Overload = 3
    }

    /// <summary>
    /// Уточнение отказа сустава (ФИКС 11.А): что именно происходит с отказавшей осью.
    /// Это не отдельные отказы, а три состояния одного и того же отказа «отказ сустава»,
    /// поэтому вид выбирается заранее, а запускается всё той же кнопкой «Отказ сустава».
    /// </summary>
    public enum KvJointFailureMode
    {
        /// <summary>Потеря момента (штатное поведение): звено провисает под собственным весом.</summary>
        Droop = 0,
        /// <summary>Потеря управления: ось «обесточена», её цель игнорируется, робот работает остальными осями.</summary>
        LostControl = 1,
        /// <summary>Отключение сустава с фиксацией: тормоз сработал, угол заморожен до сброса аварии.</summary>
        BrakeLocked = 2
    }

    /// <summary>
    /// ЭТАП 34 ТЗ: ИМИТАЦИЯ ОТКАЗОВ И ПОВЕДЕНИЕ СИСТЕМЫ БЕЗОПАСНОСТИ.
    ///
    /// ЧТО ЗДЕСЬ ЕСТЬ (и это работает):
    ///   • ОТКАЗ СУСТАВА — выбранная ось теряет момент. Момент силы тяжести считается ШТАТНОЙ
    ///     моделью нагрузки (`KvPayloadCalculator.JointTorques`: вес звеньев в своих центрах плюс
    ///     вес груза в точке инструмента, спроецированные на ось сустава, Н·м и со знаком),
    ///     а провисание считается по МОДЕЛИ МАЯТНИКА С ВЯЗКИМ ТРЕНИЕМ (ФИКС 11.А):
    ///         q̇ += (τ_веса − k_d · q̇) / I · dt,   q += q̇ · dt
    ///     с ограничением по упорам сустава и остановкой при |q̇| &lt; ε. Поза применяется к роботу,
    ///     поэтому последствие видно глазами. Это по-прежнему УПРОЩЁННАЯ модель: динамика
    ///     Ньютона–Эйлера, трение в редукторах и упругость звеньев НЕ считаются (момент инерции
    ///     и трение заданы одним числом на все оси — поля `inertiaKgM2` и `viscousFriction`);
    ///   • тот же отказ в двух других состояниях (ФИКС 11.А): ПОТЕРЯ УПРАВЛЕНИЯ — ось помечена
    ///     «обесточена», её цель игнорируется, робот продолжает работать остальными осями;
    ///     ОТКЛЮЧЕНИЕ С ФИКСАЦИЕЙ — тормоз сработал, угол заморожен и не меняется до сброса аварии;
    ///   • ПОТЕРЯ СВЯЗИ — команды движения блокируются, телеметрия помечается как устаревшая,
    ///     а если робот в этот момент ехал — выполняется контролируемая остановка;
    ///   • ПЕРЕГРУЗКА — груз в модели нагрузки умножается на коэффициент, считается требуемый
    ///     момент по суставам и сравнивается с номиналом; при превышении робот останавливается
    ///     и система требует повторного ввода в работу.
    ///
    /// ПОВЕДЕНИЕ БЕЗОПАСНОСТИ общее для всех отказов: аварийная остановка, запись в журнал
    /// уровнем «ошибка», баннер на экране и ЗАПРЕТ ПУСКА до нажатия «Сброс аварии» (после сброса
    /// требуется переезд в домашнюю позу — как на реальном производстве). «Сброс аварии»
    /// возвращает в норму ВСЕ три состояния отказа сустава.
    /// </summary>
    public class KvFailureSimulator
    {
        /// <summary>Порог остановки провисания по угловой скорости, рад/с: |q̇| &lt; ε — движение прекратилось.</summary>
        private const double StopOmega = 0.001;

        /// <summary>
        /// ФИКС 8. Полоса МЯГКОГО подхода к упору, рад (≈1°): в её пределах шаг провисания
        /// тормозится плавно, а не обрывается нулём в самый момент касания упора.
        /// </summary>
        private const double SoftStopBandRad = 0.0175;

        /// <summary>ФИКС 8. Постоянная затухания «дребезга» в конце провисания, с.</summary>
        private const double SettleTau = 0.35;

        /// <summary>ФИКС 8. Сколько секунд держать порог |q̇| &lt; ε, чтобы признать остановку.</summary>
        private const double StopHoldSeconds = 0.15;

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;
        private KvStageHub3 stage3;

        private KvFailureKind kind = KvFailureKind.None;
        private KvJointFailureMode jointMode = KvJointFailureMode.Droop;
        private int joint;
        private float since;
        private bool requiresReset;
        private float payloadFactor = 1f;
        private float savedPayloadMass = -1f;
        private float overloadStopAt = -1f;
        private string banner = "";
        private float droopRateDeg = 12f;
        private string lastReport = "";
        private int events;

        // --- состояние модели провисания (ФИКС 11.А) ---
        private double droopOmega;          // текущая угловая скорость провисания, рад/с
        private bool droopStopped;          // провисание остановилось: равновесие или упор
        private bool droopStopReported;     // строка «провисание остановилось» уже сказана
        private double heldAngle;           // угол, на котором «замерла» обесточенная ось
        private bool holding;               // удерживаем угол (потеря управления или тормоз)

        // --- ФИКС 8: поза ОСТАЛЬНЫХ суставов и плавное затухание в конце провисания ---
        private double[] frozenPose;        // поза ВСЕХ осей в момент отказа (остальные — фиксированы)
        private double frozenTorque;        // момент веса в этой позе, Н·м (для отчёта)
        private double stopHold;            // сколько секунд скорость держится ниже порога
        private double settleFactor = 1.0;  // множитель плавного затухания (1 → 0 в конце)

        /// <summary>
        /// Приведённый момент инерции distal-части (звеньев после отказавшего сустава вместе
        /// с грузом) относительно оси сустава, кг·м². Чем больше значение, тем медленнее
        /// сустав разгоняется при провисании. Одно число на все оси — это упрощение модели.
        /// </summary>
        [Tooltip("Приведённый момент инерции звена относительно оси отказавшего сустава, кг·м²: " +
                 "чем больше значение, тем медленнее сустав разгоняется при провисании")]
        public float inertiaKgM2 = 20f;

        /// <summary>
        /// Коэффициент вязкого трения в суставе (демпфер редуктора), Н·м·с/рад. Именно он
        /// не даёт провисанию разгоняться бесконечно: установившаяся скорость ω ≈ τ_веса / k_d.
        /// </summary>
        [Tooltip("Вязкое трение (демпфер) в суставе, Н·м·с/рад: задаёт установившуюся скорость " +
                 "провисания ω = момент веса / трение")]
        public float viscousFriction = 140f;

        public KvFailureKind Kind { get { return kind; } }
        public KvJointFailureMode JointMode { get { return jointMode; } }
        public int Joint { get { return joint; } }
        public float Since { get { return since; } }
        public bool RequiresReset { get { return requiresReset; } }
        public string Banner { get { return banner; } }
        public float DroopRate { get { return droopRateDeg; } }
        public int Events { get { return events; } }
        public string LastReport { get { return lastReport; } }
        public float PayloadFactor { get { return payloadFactor; } }
        public bool Active { get { return kind != KvFailureKind.None; } }
        /// <summary>Держится ли угол оси (потеря управления или отключение с фиксацией).</summary>
        public bool Holding { get { return holding; } }
        /// <summary>Остановилось ли провисание (равновесие груза под осью или упор сустава).</summary>
        public bool DroopStopped { get { return droopStopped; } }
        /// <summary>Текущая скорость провисания, °/с (для подписи в интерфейсе).</summary>
        public float DroopSpeedDeg { get { return (float)(droopOmega * Mathf.Rad2Deg); } }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvStageHub3 hub3)
        {
            flow = controller;
            features = hub;
            stage3 = hub3;
        }

        public void SetDroopRate(float value)
        {
            droopRateDeg = Mathf.Clamp(value, 0f, 90f);
        }

        /// <summary>Название состояния отказа сустава (для подписей в интерфейсе и журнала).</summary>
        public static string JointModeLabel(KvJointFailureMode value)
        {
            switch (value)
            {
                case KvJointFailureMode.LostControl:
                    return "потеря управления (ось обесточена, цель игнорируется)";
                case KvJointFailureMode.BrakeLocked:
                    return "отключение с фиксацией (тормоз сработал, угол заморожен)";
                default:
                    return "потеря момента (провисание под весом)";
            }
        }

        /// <summary>
        /// Выбрать, что произойдёт с осью при следующем «отказе сустава» (ФИКС 11.А):
        /// провисание под весом, потеря управления или отключение с фиксацией.
        /// </summary>
        public void SetJointMode(KvJointFailureMode value)
        {
            jointMode = value;
            Say("вид отказа сустава: " + JointModeLabel(value));
        }

        /// <summary>
        /// Запустить отказ. Для отказа сустава указывается номер сустава (1…N); что именно
        /// произойдёт с осью — определяет выбранный вид отказа (`SetJointMode`, ФИКС 11.А).
        /// </summary>
        public void Start(KvFailureKind value, int jointNumber = 1)
        {
            if (flow == null || !flow.Validator.Ready)
            {
                Say("робот не определён — имитация невозможна");
                return;
            }
            Clear(false);
            kind = value;
            joint = Mathf.Clamp(jointNumber - 1, 0, flow.Validator.Dof - 1);
            since = 0f;
            events++;

            switch (kind)
            {
                case KvFailureKind.JointLoss:
                    PrepareJointFailure();
                    break;
                case KvFailureKind.CommsLoss:
                    banner = "ПОТЕРЯ СВЯЗИ: команды не проходят, телеметрия устарела";
                    StopMotion("потеря связи с контроллером");
                    break;
                case KvFailureKind.Overload:
                    savedPayloadMass = PayloadMass();
                    payloadFactor = 2.5f;
                    SetPayloadMass(savedPayloadMass * payloadFactor);
                    banner = "ПЕРЕГРУЗКА: масса груза " + PayloadMass().ToString("0.0") +
                             " кг (в " + payloadFactor.ToString("0.0") + " раза больше заданной)";
                    overloadStopAt = 0f;
                    break;
            }

            requiresReset = true;

            // Останов нужен везде, кроме «потери управления»: там робот по условию продолжает
            // работать остальными осями, а отказавшая ось просто не отрабатывает свою цель.
            if (kind == KvFailureKind.JointLoss && jointMode != KvJointFailureMode.LostControl)
                StopMotion("отказ сустава " + (joint + 1) + " — " + JointModeLabel(jointMode));

            Log(KvLogKind.Error, "имитация отказа: " + banner);
            Say(banner);
        }

        /// <summary>
        /// Подготовить отказ сустава (ФИКС 11.А): запомнить угол, на котором отказ застал ось,
        /// и обнулить состояние модели провисания. Для «потери управления» и «отключения
        /// с фиксацией» ось перестаёт отрабатывать команды и удерживает этот угол.
        /// </summary>
        private void PrepareJointFailure()
        {
            droopOmega = 0.0;
            droopStopped = false;
            droopStopReported = false;
            holding = jointMode != KvJointFailureMode.Droop;
            lastReport = "";
            stopHold = 0.0;
            settleFactor = 1.0;

            double[] q = flow.Validator.CopyCurrent();
            heldAngle = joint >= 0 && joint < q.Length ? q[joint] : 0.0;

            // ФИКС 8. Поза ОСТАЛЬНЫХ суставов запоминается и далее считается ФИКСИРОВАННОЙ:
            // провисает только отказавшая ось, а вклад остальных в момент веса пересчитывается
            // на каждом шаге по этой замороженной позе (раньше момент брался от «текущей» позы
            // робота, которая могла меняться по другим осям — и модель «плыла»).
            frozenPose = q != null ? (double[])q.Clone() : null;
            frozenTorque = GravityTorque(frozenPose, joint);

            switch (jointMode)
            {
                case KvJointFailureMode.LostControl:
                    banner = "ПОТЕРЯ УПРАВЛЕНИЯ СУСТАВОМ " + (joint + 1) +
                             ": ось обесточена, её цель игнорируется — робот работает остальными осями";
                    break;
                case KvJointFailureMode.BrakeLocked:
                    banner = "ОТКЛЮЧЕНИЕ СУСТАВА " + (joint + 1) + " С ФИКСАЦИЕЙ: тормоз сработал, " +
                             "угол " + (heldAngle * Mathf.Rad2Deg).ToString("0.0") +
                             "° заморожен до сброса аварии";
                    break;
                default:
                    banner = "ОТКАЗ СУСТАВА " + (joint + 1) + ": потеря момента, звено провисает";
                    break;
            }
        }

        /// <summary>Сброс аварии: снимает запрет пуска, возвращает модель нагрузки и все состояния отказа сустава.</summary>
        public void Clear(bool announce = true)
        {
            if (savedPayloadMass >= 0f)
            {
                SetPayloadMass(savedPayloadMass);
                savedPayloadMass = -1f;
            }
            kind = KvFailureKind.None;
            since = 0f;
            banner = "";
            overloadStopAt = -1f;
            payloadFactor = 1f;
            requiresReset = false;

            // ФИКС 11.А: сброс аварии возвращает в норму ВСЕ три состояния отказа сустава —
            // провисание останавливается, «обесточенная» ось и ось с тормозом снова управляются.
            droopOmega = 0.0;
            droopStopped = false;
            droopStopReported = false;
            holding = false;
            heldAngle = 0.0;
            lastReport = "";

            // ФИКС 8: снимок позы остальных суставов и плавное затухание тоже сбрасываются.
            frozenPose = null;
            frozenTorque = 0.0;
            stopHold = 0.0;
            settleFactor = 1.0;

            if (announce)
            {
                Log(KvLogKind.System, "авария сброшена: выполните ДОМОЙ перед продолжением работы");
                Say("авария сброшена · требуется переезд в домашнюю позу (вручную или командой ДОМОЙ)");
            }
        }

        public void Tick(float deltaTime)
        {
            if (kind == KvFailureKind.None) return;
            since += deltaTime;

            if (kind == KvFailureKind.JointLoss) TickJointLoss(deltaTime);
            else if (kind == KvFailureKind.Overload) TickOverload();
            else if (kind == KvFailureKind.CommsLoss) TickComms();
        }

        /// <summary>
        /// Обслуживание отказа сустава (ФИКС 11.А). Что именно происходит с осью — определяет
        /// выбранный вид отказа: провисание под весом (маятник с вязким трением), потеря
        /// управления (цель оси игнорируется, робот работает остальными осями) или отключение
        /// с фиксацией (тормоз сработал — угол заморожен до сброса аварии).
        /// </summary>
        private void TickJointLoss(float deltaTime)
        {
            PoseValidator v = flow.Validator;
            if (!v.Ready) return;

            if (jointMode != KvJointFailureMode.Droop)
            {
                HoldJointAngle(v);
                return;
            }

            double[] q = v.CopyCurrent();
            if (joint < 0 || joint >= q.Length) return;

            if (v.IsPrismatic(joint)) TickPrismaticDroop(v, q, deltaTime);
            else TickPendulumDroop(v, q, deltaTime);
        }

        /// <summary>
        /// ПРОВИСАНИЕ ПО МОДЕЛИ МАЯТНИКА С ВЯЗКИМ ТРЕНИЕМ (ФИКС 11.А, уточнено в ФИКСЕ 8).
        ///
        /// Момент силы тяжести τ берётся у штатной модели нагрузки
        /// (`KvPayloadCalculator.JointTorques`): вес звеньев в своих центрах плюс вес груза
        /// в точке инструмента, спроецированные на ось сустава, в Н·м и СО ЗНАКОМ — то есть
        /// направление, куда вес тянет звено, получается само, без «угадывания» знака.
        /// Дальше интегрируется уравнение маятника с вязким трением:
        ///     q̇ += (τ − k_d · q̇) / I · dt,   q += q̇ · dt
        ///
        /// ФИКС 8 — ДВА УТОЧНЕНИЯ МОДЕЛИ:
        ///   1) ПОЗА ОСТАЛЬНЫХ СУСТАВОВ. Провисает ровно одна ось; остальные считаются
        ///      ЗАФИКСИРОВАННЫМИ в позе отказа, но их ВКЛАД В МОМЕНТ пересчитывается на
        ///      каждом шаге (момент берётся от позы «замороженные остальные + текущий угол
        ///      отказавшей оси»), поэтому по мере провисания плечо и момент меняются честно.
        ///   2) ПЛАВНОЕ ЗАТУХАНИЕ В КОНЦЕ. Раньше движение обрывалось условием |q̇| &lt; ε.
        ///      Теперь в полосе ≈1° перед упором ход плавно тормозится (smoothstep), а когда
        ///      момент веса перестаёт разгонять ось в сторону движения, остаточная скорость
        ///      гасится экспоненциально (τ ≈ 0.35 с). Остановка признаётся только если порог
        ///      держится `StopHoldSeconds` — без «мигания» состояния.
        ///
        /// Предел скорости из ползунка «Скорость провисания» сохранён — он не даёт модели
        /// разгонять звено быстрее, чем было видно раньше. Это по-прежнему УПРОЩЁННАЯ МОДЕЛЬ,
        /// а не физический движок (см. помету на вкладке «Имитация отказов»).
        /// </summary>
        private void TickPendulumDroop(PoseValidator v, double[] q, float deltaTime)
        {
            // Поза модели: остальные оси заморожены, отказавшая — текущая.
            double[] pose = PoseForDroop(v, q);
            double current = pose != null && joint >= 0 && joint < pose.Length ? pose[joint] : q[joint];

            float torque = GravityTorque(pose, joint);
            if (float.IsNaN(torque))
            {
                // Модель нагрузки недоступна — остаётся прежнее поведение: провисание
                // с постоянной скоростью в ту сторону, куда звено тянет вес.
                TickRateDroop(v, q, deltaTime);
                return;
            }

            double inertia = Mathf.Max(0.01f, inertiaKgM2);
            double friction = Mathf.Max(0f, viscousFriction);
            double omega = droopOmega + (torque - friction * droopOmega) / inertia * deltaTime;

            double limit = droopRateDeg * Mathf.Deg2Rad;
            omega = Math.Max(-limit, Math.Min(limit, omega));

            double lower = v.Lower[joint];
            double upper = v.Upper[joint];
            double next = current + omega * deltaTime;

            // 1) МЯГКИЙ ПОДХОД К УПОРУ: в полосе ~1° ход плавно тормозится, поэтому звено
            //    «доезжает» до упора, а не щёлкает в него.
            bool nearLower = omega < 0.0 && next - lower < SoftStopBandRad;
            bool nearUpper = omega > 0.0 && upper - next < SoftStopBandRad;
            if (nearLower || nearUpper)
            {
                double gap = nearLower ? next - lower : upper - next;
                double k = Mathf.Clamp01((float)(gap / SoftStopBandRad));
                settleFactor = k * k * (3.0 - 2.0 * k);        // smoothstep: 1 → 0 у упора
                omega *= settleFactor;
                next = current + omega * deltaTime;
            }

            // 2) ПЛАВНОЕ ЗАТУХАНИЕ ОСТАТКА: момент веса больше не разгоняет ось в сторону
            //    её движения (звено прошло низ и тормозит) — скорость гасится экспоненциально.
            if (torque * omega < 0.0) omega /= 1.0 + deltaTime / SettleTau;

            if (next <= lower) { next = lower; omega = 0.0; }
            else if (next >= upper) { next = upper; omega = 0.0; }

            stopHold = Math.Abs(omega) < StopOmega ? stopHold + deltaTime : 0.0;
            bool stopped = stopHold >= StopHoldSeconds;
            if (stopped) omega = 0.0;
            droopStopped = stopped;

            droopOmega = omega;
            pose[joint] = next;
            v.Apply(v.ContinueFrom(v.CopyCurrent(), pose));

            if (stopped && !droopStopReported)
            {
                droopStopReported = true;
                Log(KvLogKind.Warning, "сустав " + (joint + 1) + ": провисание остановилось на " +
                                       (next * Mathf.Rad2Deg).ToString("0.0") +
                                       "° (равновесие или упор сустава) · остальные оси " +
                                       "зафиксированы в позе отказа, момент пересчитан: " +
                                       TorqueText(frozenTorque) + " → " + TorqueText(torque) + " Н·м");
            }
        }

        /// <summary>Момент для журнала: «н/д» вместо NaN, если модель нагрузки недоступна.</summary>
        private static string TorqueText(double torque)
        {
            return double.IsNaN(torque) ? "н/д" : torque.ToString("0.0");
        }

        /// <summary>
        /// ФИКС 8. Поза для модели провисания: ВСЕ оси, кроме отказавшей, берутся из снимка
        /// момента отказа (`frozenPose`) и считаются неподвижными; отказавшая ось — текущая.
        /// Если снимка нет (модель нагрузки недоступна), возвращается текущая поза робота.
        /// </summary>
        private double[] PoseForDroop(PoseValidator v, double[] current)
        {
            if (frozenPose == null || current == null || frozenPose.Length != current.Length)
                return current;
            double[] pose = (double[])frozenPose.Clone();
            if (joint >= 0 && joint < pose.Length) pose[joint] = current[joint];
            return pose;
        }

        /// <summary>Короткая подпись состояния провисания для статуса вкладки.</summary>
        private string DroopSummary()
        {
            bool prismatic = flow != null && flow.Validator != null && flow.Validator.Ready &&
                             flow.Validator.IsPrismatic(joint);
            if (prismatic)
                return droopStopped ? "каретка опустилась до нижнего упора" : "каретка опускается";
            return droopStopped
                ? "провисание остановилось (равновесие или упор)"
                : "провисание " + DroopSpeedDeg.ToString("0.0") + " °/с";
        }

        /// <summary>
        /// Прежнее поведение провисания (постоянная скорость в сторону момента веса) —
        /// запасной путь, если модель нагрузки почему-то недоступна.
        /// </summary>
        private void TickRateDroop(PoseValidator v, double[] q, float deltaTime)
        {
            Vector3 pivot = v.PivotAt(joint, q);
            Vector3 axis = v.AxisWorld(joint, q);
            Vector3 distal = v.TcpAt(q) - pivot;
            Vector3 gravityTorque = Vector3.Cross(distal, Vector3.down);   // r × F (F вниз)
            float sign = Mathf.Sign(Vector3.Dot(gravityTorque, axis));
            if (Mathf.Abs(sign) < 0.01f) sign = 1f;

            double speed = sign * droopRateDeg * Mathf.Deg2Rad;
            double step = speed * deltaTime;
            q[joint] = Mathf.Clamp((float)(q[joint] + step),
                (float)v.Lower[joint], (float)v.Upper[joint]);
            droopOmega = speed;
            droopStopped = false;

            // Поза применяется пошагово и с ограничением, чтобы «провисание» шло плавно.
            v.Apply(v.ContinueFrom(v.CopyCurrent(), q));
        }

        /// <summary>
        /// Призматическая ось (SCARA): маятника у неё нет — каретка под весом руки опускается
        /// до нижнего упора. ФИКС 8: остальные оси берутся из позы отказа и считаются
        /// неподвижными (раньше в расчёт попадала «текущая» поза робота целиком).
        /// </summary>
        private void TickPrismaticDroop(PoseValidator v, double[] q, float deltaTime)
        {
            double[] pose = PoseForDroop(v, q);
            double current = pose != null && joint >= 0 && joint < pose.Length ? pose[joint] : q[joint];

            double step = droopRateDeg * 0.001f * deltaTime;
            double next = Math.Max(v.Lower[joint], current - step);
            droopOmega = -droopRateDeg * 0.001f;

            stopHold = Math.Abs(next - v.Lower[joint]) < 1e-6 ? stopHold + deltaTime : 0.0;
            droopStopped = stopHold >= StopHoldSeconds;
            if (droopStopped) droopOmega = 0.0;

            pose[joint] = next;
            v.Apply(v.ContinueFrom(v.CopyCurrent(), pose));
        }

        /// <summary>
        /// Ось не отрабатывает команды: её цель игнорируется, а угол удерживается на значении,
        /// которое было в момент отказа. Так выглядит «обесточенный» сустав (потеря управления,
        /// робот продолжает работать остальными осями) и сустав, у которого сработал тормоз
        /// (отключение с фиксацией — угол заморожен до сброса аварии).
        /// </summary>
        private void HoldJointAngle(PoseValidator v)
        {
            double[] q = v.CopyCurrent();
            if (joint < 0 || joint >= q.Length) return;
            if (Math.Abs(q[joint] - heldAngle) < 1e-9) return;   // ось уже на месте — робота не трогаем

            q[joint] = heldAngle;
            v.Apply(v.ContinueFrom(v.CopyCurrent(), q));
        }

        /// <summary>
        /// Момент силы тяжести на оси сустава в текущей позе, Н·м (со знаком). Считает штатная
        /// модель нагрузки: вес каждого звена в своём центре плюс вес груза в точке инструмента,
        /// спроецированные на ось сустава. `NaN` — модель недоступна (тогда работает запасной путь).
        /// </summary>
        private float GravityTorque(double[] q, int index)
        {
            KvStageHub3 hub3 = stage3 ?? KvStageHub3.Current;
            if (hub3 == null || hub3.Payload == null) return float.NaN;

            float[] torque;
            float[] load;
            Vector3 toolForce;
            // Дополнительный груз не добавляем: в модели уже есть масса инструмента (toolMassKg),
            // а перегрузка — отдельный отказ, в один момент времени активен только один.
            if (!hub3.Payload.JointTorques(q, 0f, out torque, out load, out toolForce))
                return float.NaN;
            if (torque == null || index < 0 || index >= torque.Length) return float.NaN;
            return torque[index];
        }


        private void TickOverload()
        {
            if (overloadStopAt >= 0f) overloadStopAt += Time.deltaTime;
            PoseValidator v = flow.Validator;
            if (!v.Ready) return;

            double[] q = v.CopyCurrent();
            float[] torque;
            float[] load;
            Vector3 toolForce;
            if (!stage3.Payload.JointTorques(q, PayloadMass(), out torque, out load, out toolForce))
                return;

            int worst = 0;
            for (int i = 1; i < load.Length; i++) if (load[i] > load[worst]) worst = i;
            float percent = load[worst] * 100f;
            lastReport = "сустав " + (worst + 1) + " загружен на " + percent.ToString("0") + " % от номинала";

            if (percent > 100f && overloadStopAt > 2f)
            {
                StopMotion("перегрузка сустава " + (worst + 1));
                overloadStopAt = -1000f;
                Log(KvLogKind.Error, "перегрузка: " + lastReport + " — движение остановлено");
                Say("ПЕРЕГРУЗКА: " + lastReport + " — робот остановлен");
            }
        }

        private void TickComms()
        {
            if (Time.frameCount % 120 != 0) return;
            lastReport = "телеметрия устарела на " + since.ToString("0.0") + " с";
        }

        /// <summary>Разрешать ли запуск движения (используется проверкой перед пуском, этап 35).</summary>
        public bool MotionAllowed(out string reason)
        {
            reason = "";
            if (kind == KvFailureKind.CommsLoss)
            {
                reason = "потеря связи: команды движения не выполняются";
                return false;
            }
            if (kind == KvFailureKind.Overload)
            {
                reason = "перегрузка: движение заблокировано до сброса аварии";
                return false;
            }
            if (kind == KvFailureKind.JointLoss)
            {
                // «Потеря управления» (ось обесточена) — единственное состояние отказа сустава,
                // в котором работа продолжается: робот едет остальными осями, а отказавшая ось
                // просто не отрабатывает цель. Провисание и тормоз движение запрещают.
                if (jointMode == KvJointFailureMode.LostControl)
                {
                    reason = "сустав " + (joint + 1) + " обесточен: его цель игнорируется, " +
                             "робот работает остальными осями";
                    return true;
                }
                reason = jointMode == KvJointFailureMode.BrakeLocked
                    ? "сустав " + (joint + 1) + " отключён с фиксацией: угол заморожен, " +
                      "движение заблокировано до сброса аварии"
                    : "отказ сустава: движение заблокировано до сброса аварии";
                return false;
            }
            if (requiresReset)
            {
                reason = "после аварии требуется сброс аварии и переезд домой";
                return false;
            }
            return true;
        }

        private void StopMotion(string why)
        {
            if (features != null) features.EmergencyStop();
            if (flow != null && flow.Motion != null) flow.Motion.SetPaused(true);
            Log(KvLogKind.Stop, "останов по причине: " + why);
        }

        private float PayloadMass()
        {
            KvStageHub3 hub3 = stage3 ?? KvStageHub3.Current;
            return hub3 != null && hub3.Payload != null ? hub3.Payload.Model.toolMassKg : 0f;
        }

        private void SetPayloadMass(float value)
        {
            KvStageHub3 hub3 = stage3 ?? KvStageHub3.Current;
            if (hub3 != null && hub3.Payload != null)
                hub3.Payload.Model.toolMassKg = Mathf.Clamp(value, 0f, 50f);
        }

        private void Log(KvLogKind level, string text)
        {
            if (features != null && features.Log != null) features.Log.Add(level, "отказы: " + text);
        }

        private void Say(string text)
        {
            lastReport = text;
            Debug.Log("[Failure] " + text);
            if (Message != null) Message(text);
        }

        public string Status()
        {
            if (kind == KvFailureKind.None)
                return requiresReset
                    ? "авария сброшена, требуется переезд домой"
                    : "отказов нет · имитаций выполнено " + events;

            string text = banner + " · " + since.ToString("0.0") + " с";
            if (kind == KvFailureKind.JointLoss)
            {
                text += jointMode == KvJointFailureMode.Droop
                    ? " · " + DroopSummary()
                    : " · угол удерживается на " + (heldAngle * Mathf.Rad2Deg).ToString("0.0") + "°";
            }
            if (!string.IsNullOrEmpty(lastReport)) text += " · " + lastReport;
            return text;
        }
    }

    /// <summary>Важность замечания проверки перед пуском (этап 35 ТЗ).</summary>
    public enum KvSeverity { Info = 0, Warning = 1, Critical = 2 }

    /// <summary>Одно замечание проверки перед пуском.</summary>
    public class KvFinding
    {
        public KvSeverity severity;
        public string title = "";
        public string details = "";
        public bool requiresConfirmation;
        public bool blocks;

        public string Describe()
        {
            string mark = severity == KvSeverity.Critical ? "КРИТИЧНО" :
                          severity == KvSeverity.Warning ? "ВНИМАНИЕ" : "СПРАВКА";
            return mark + ": " + title + (string.IsNullOrEmpty(details) ? "" : " — " + details);
        }
    }

    /// <summary>
    /// ЭТАП 35 ТЗ: ПРОВЕРКА ПЕРЕД ПУСКОМ И ПОДТВЕРЖДЕНИЕ ОПЕРАТОРОМ.
    ///
    /// Перед запуском движения проверяется выбранная траектория целиком (по сэмплам плана):
    ///   • ЗАЗОР до препятствий по всей траектории;
    ///   • БЛИЗОСТЬ К ЧЕЛОВЕКУ — самый важный пункт ТЗ: считается расстояние от звеньев робота
    ///     на каждом сэмпле до человека. Положение человека берётся у оператора (камера/шлем) —
    ///     это единственный источник, который в проекте реально есть; когда в сцену добавят
    ///     манекенов, их точки можно подать тем же списком;
    ///   • ЗОНЫ ЗАПРЕТА (пересечение звеньев с зонами);
    ///   • ЗАПАС ДО ЛИМИТОВ суставов и близость к особенности (σ_min);
    ///   • ПЕРЕГРУЗКА по модели нагрузки в целевой позе;
    ///   • НАГРЕВ суставов по монитору здоровья;
    ///   • ОТКАЗ/АВАРИЯ (этап 34) — движение запрещено до сброса.
    ///
    /// Если есть критические замечания — пуск не выполняется. Если есть предупреждения,
    /// требующие решения человека, показывается окно подтверждения с обратным отсчётом:
    /// без явного нажатия «ПУСК ПОДТВЕРЖДАЮ» робот не поедет (по истечении времени — отмена).
    /// </summary>
    public class KvPreRunValidator
    {
        public const float PersonWarningDistance = 1.2f;    // м: ближе — предупреждение
        public const float PersonCriticalDistance = 0.7f;   // м: ближе — критично
        public const float ConfirmSeconds = 20f;

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;
        private KvStageHub3 stage3;
        private KvFailureSimulator failures;
        private Func<Vector3[]> personProvider;

        private readonly List<KvFinding> findings = new List<KvFinding>();
        private Action pendingAction;
        private float confirmTimer;
        private bool pending;
        private Vector3 closestPersonPoint;
        private float closestPersonDistance = float.MaxValue;
        private string lastSummary = "проверка не выполнялась";

        private Canvas canvas;
        private RectTransform panel;
        private Text summaryText, countText;
        private int validationRuns, confirmations, cancellations, blockedRuns;

        public IList<KvFinding> Findings { get { return findings; } }
        public bool Pending { get { return pending; } }
        public float Countdown { get { return Mathf.Max(0f, confirmTimer); } }
        public string LastSummary { get { return lastSummary; } }
        public float ClosestPersonDistance
        {
            get { return closestPersonDistance == float.MaxValue ? -1f : closestPersonDistance; }
        }
        public int ValidationRuns { get { return validationRuns; } }
        public int Confirmations { get { return confirmations; } }
        public int Cancellations { get { return cancellations; } }
        public int BlockedRuns { get { return blockedRuns; } }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvStageHub3 hub3,
            KvFailureSimulator failureSimulator, Func<Vector3[]> people, Transform canvasParent)
        {
            flow = controller;
            features = hub;
            stage3 = hub3;
            failures = failureSimulator;
            personProvider = people;
            Build(canvasParent);
        }

        /// <summary>Человек = оператор у камеры (плюс то, что вернёт поставщик точек).</summary>
        private Vector3[] People()
        {
            List<Vector3> points = new List<Vector3>();
            if (personProvider != null)
            {
                Vector3[] extra = personProvider();
                if (extra != null) points.AddRange(extra);
            }
            // P1-20 (11.11.2026). ЧТО БЫЛО: две ветки `if (points.Count == 0 …) … else if (…)` с
            // ОДИНАКОВЫМ телом — проверка `Count == 0` не влияла ни на что, а `Camera.main`
            // запрашивался до четырёх раз за вызов (это поиск по сцене, не поле).
            // ПОЧЕМУ ПРОСТО ОБЪЕДИНЕНО, А НЕ ПЕРЕПИСАНО: задумывалось ли Различие (например, «если
            // поставщик дал точки — оператора не добавлять») — вопрос владельцу; см. раздел 8
            // отчёта. Пока поведение сохранено ровно тем, что код делал на самом деле: камера
            // добавляется ВСЕГДА, когда она есть.
            Camera camera = Camera.main;
            if (camera != null) points.Add(camera.transform.position);
            return points.ToArray();
        }

        /// <summary>
        /// ФИКС 9. Подпись источников «человека» для отчёта проверки: сколько точек учтено
        /// и вошли ли в них манекены (ставит хаб этапов 13–36).
        /// </summary>
        public string PersonSourceNote { get; set; }

        // ------------------------------------------------------------------ проверка

        public List<KvFinding> Validate()
        {
            findings.Clear();
            validationRuns++;
            closestPersonDistance = float.MaxValue;

            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Add(KvSeverity.Critical, "робот не определён", "проверка невозможна", false, true);
                Finish();
                return findings;
            }

            PoseValidator v = flow.Validator;
            string failureReason;
            if (failures != null && !failures.MotionAllowed(out failureReason))
                Add(KvSeverity.Critical, "система в аварийном состоянии", failureReason, false, true);

            if (flow.State.phase == FlowState.RobotMoving || flow.ExternalMotionRunning ||
                (flow.Motion != null && flow.Motion.IsRunning))
                Add(KvSeverity.Critical, "робот уже выполняет движение",
                    "дождитесь остановки", false, true);

            if (flow.State.selectedTrajectory < 0 ||
                flow.State.selectedTrajectory >= flow.State.candidates.Count)
            {
                Add(KvSeverity.Critical, "траектория не выбрана",
                    "выберите вариант траектории", false, true);
                Finish();
                return findings;
            }

            TrajectoryCandidate candidate = flow.State.candidates[flow.State.selectedTrajectory];
            if (candidate == null || candidate.plan == null)
            {
                Add(KvSeverity.Critical, "у варианта нет плана", "пересчитайте траекторию", false, true);
                Finish();
                return findings;
            }

            // 1. Зазор до препятствий по всей траектории.
            float clearance = candidate.plan.MinClearance;
            if (clearance < 0f)
                Add(KvSeverity.Critical, "траектория пересекает препятствие",
                    "зазор " + (clearance * 1000f).ToString("0") + " мм", false, true);
            else if (clearance < 0.03f)
                Add(KvSeverity.Warning, "очень малый зазор до препятствия",
                    (clearance * 1000f).ToString("0") + " мм — рекомендуем пересчитать", true, false);
            else
                Add(KvSeverity.Info, "зазор до препятствий в норме",
                    (clearance * 1000f).ToString("0") + " мм", false, false);

            // 2. Близость к человеку: идём по сэмплам плана и считаем расстояние от звеньев.
            CheckPerson(v, candidate);

            // 3. Зоны запрета.
            if (features != null && features.Zones != null && features.Zones.Count > 0)
            {
                KvZoneHit hit = features.Zones.CheckPlan(candidate.plan, v, v.Dof);
                if (hit.hit)
                    Add(KvSeverity.Critical, "траектория входит в зону запрета", hit.Describe(), false, true);
                else
                    Add(KvSeverity.Info, "зоны запрета не задеты",
                        "проверено зон: " + features.Zones.Count, false, false);
            }

            // 4. Запас до лимитов и близость к особенности.
            if (candidate.plan.LimitMargin < 3f)
                Add(KvSeverity.Warning, "малый запас до лимитов суставов",
                    candidate.plan.LimitMargin.ToString("0.0") + "°", true, false);
            if (candidate.plan.SigmaMin < 0.02)
                Add(KvSeverity.Warning, "траектория проходит у особенности",
                    "σ_min = " + candidate.plan.SigmaMin.ToString("0.000"), true, false);

            // 5. Перегрузка в целевой позе.
            if (stage3 != null && stage3.Payload != null)
            {
                double[] goal = candidate.plan.GoalQ;
                if (goal != null)
                {
                    KvPayloadResult payload = stage3.Payload.Evaluate(goal);
                    if (payload != null)
                    {
                        float mass = stage3.Payload.Model.toolMassKg;
                        if (payload.maxKg < mass)
                            Add(KvSeverity.Critical, "перегрузка в конечной позе",
                                "допустимо " + payload.maxKg.ToString("0.0") + " кг при грузе " +
                                mass.ToString("0.0") + " кг", false, true);
                        else if (payload.maxKg < mass * 1.2f)
                            Add(KvSeverity.Warning, "нагрузка близка к пределу",
                                "запас " + (payload.maxKg - mass).ToString("0.0") + " кг", true, false);
                    }
                }
            }

            // 6. Нагрев суставов.
            KvStageHub hub = KvStageHub.Current;
            if (hub != null && hub.Health != null && hub.Health.Temperature != null)
            {
                float hottest = 0f;
                int index = -1;
                for (int i = 0; i < hub.Health.Temperature.Length; i++)
                    if (hub.Health.Temperature[i] > hottest) { hottest = hub.Health.Temperature[i]; index = i; }
                if (index >= 0 && hottest > hub.Health.maxTemperature * 0.8f)
                    Add(KvSeverity.Warning, "сустав " + (index + 1) + " перегрет",
                        hottest.ToString("0.0") + " °C — дайте остыть", true, false);
            }

            Finish();
            return findings;
        }

        private void CheckPerson(PoseValidator v, TrajectoryCandidate candidate)
        {
            Vector3[] people = People();
            // ФИКС 9: в отчёте прямо сказано, СКОЛЬКО точек человека проверено и вошли ли
            // манекены — иначе «человека нет в зоне» звучит как «людей рядом нет», хотя
            // проверка просто не знала о них.
            string sources = string.IsNullOrEmpty(PersonSourceNote)
                ? ""
                : " · " + PersonSourceNote;
            if (people.Length == 0)
            {
                Add(KvSeverity.Info, "человек в зоне не определён",
                    "нет точки наблюдателя (шлем/камера не найдены) — проверка близости пропущена" +
                    sources,
                    false, false);
                return;
            }

            double[][] path = candidate.plan.Path;
            if (path == null || path.Length == 0) return;

            int samples = Mathf.Min(path.Length, 60);
            int step = Mathf.Max(1, path.Length / samples);
            Vector3 tcp;
            Vector3[] nodes;
            string worstWhere = "";
            int evaluated = 0;
            bool chainAvailable = false;

            for (int i = 0; i < path.Length; i += step)
            {
                if (path[i] == null) continue;
                float clearance = v.ClearanceAt(path[i], features != null ? features.World : null,
                    out tcp, out nodes);
                if (clearance == float.NegativeInfinity)
                {
                    // Мир столкновений недоступен — считаем хотя бы по точке инструмента:
                    // расстояние до человека важнее, чем полнота обхода звеньев.
                    nodes = new[] { v.TcpAt(path[i]) };
                }
                else chainAvailable = true;
                evaluated++;
                for (int n = 0; n < nodes.Length; n++)
                {
                    for (int p = 0; p < people.Length; p++)
                    {
                        float distance = Vector3.Distance(nodes[n], people[p]);
                        if (distance >= closestPersonDistance) continue;
                        closestPersonDistance = distance;
                        closestPersonPoint = nodes[n];
                        worstWhere = "сэмпл " + i + " из " + path.Length + ", звено " + (n + 1) +
                                     (chainAvailable ? "" : " (только инструмент)");
                    }
                }
            }

            if (closestPersonDistance == float.MaxValue)
            {
                Add(KvSeverity.Info, "расстояние до человека не посчитано",
                    "проверено сэмплов: " + evaluated + sources, false, false);
                return;
            }

            string text = closestPersonDistance.ToString("0.00") + " м (" + worstWhere + ")" +
                          " · точек человека в проверке: " + people.Length + sources;
            if (closestPersonDistance < PersonCriticalDistance)
                Add(KvSeverity.Critical, "траектория проходит близко к человеку", text, false, true);
            else if (closestPersonDistance < PersonWarningDistance)
                Add(KvSeverity.Warning, "траектория проходит близко к человеку", text, true, false);
            else
                Add(KvSeverity.Info, "расстояние до человека достаточное", text, false, false);
        }

        private void Add(KvSeverity severity, string title, string details, bool confirm, bool blocks)
        {
            findings.Add(new KvFinding
            {
                severity = severity,
                title = title,
                details = details,
                requiresConfirmation = confirm,
                blocks = blocks
            });
        }

        private void Finish()
        {
            int critical = 0, warnings = 0;
            bool confirm = false;
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].severity == KvSeverity.Critical) critical++;
                else if (findings[i].severity == KvSeverity.Warning) warnings++;
                if (findings[i].requiresConfirmation) confirm = true;
            }
            lastSummary = critical > 0
                ? "проверка не пройдена: критичных замечаний " + critical
                : warnings > 0
                    ? "проверка пройдена с предупреждениями: " + warnings +
                      (confirm ? " · требуется подтверждение оператора" : "")
                    : "проверка пройдена без замечаний";
            if (features != null && features.Log != null)
                features.Log.Add(critical > 0 ? KvLogKind.Error : warnings > 0 ? KvLogKind.Warning
                        : KvLogKind.System,
                    "проверка перед пуском: " + lastSummary);
            Say(lastSummary);
        }

        private bool AnyBlocking()
        {
            for (int i = 0; i < findings.Count; i++) if (findings[i].blocks) return true;
            return false;
        }

        private bool AnyConfirmable()
        {
            for (int i = 0; i < findings.Count; i++) if (findings[i].requiresConfirmation) return true;
            return false;
        }

        // ------------------------------------------------------------------ запуск с проверкой

        /// <summary>
        /// Запуск движения «через проверку»: либо выполняет немедленно, либо показывает окно
        /// подтверждения (возвращает false — движение ещё не начато).
        /// </summary>
        public bool RequestRun(Action start)
        {
            Validate();
            if (AnyBlocking())
            {
                blockedRuns++;
                Say("пуск запрещён: " + FirstBlockReason());
                return false;
            }
            if (!AnyConfirmable())
            {
                if (start != null) start();
                return true;
            }

            pendingAction = start;
            pending = true;
            confirmTimer = ConfirmSeconds;
            ShowPanel(true);
            Say("требуется подтверждение оператора (" + ConfirmSeconds.ToString("0") + " с)");
            return false;
        }

        private string FirstBlockReason()
        {
            for (int i = 0; i < findings.Count; i++)
                if (findings[i].blocks) return findings[i].title + " — " + findings[i].details;
            return "неизвестная причина";
        }

        public void Confirm()
        {
            if (!pending) return;
            Action action = pendingAction;
            pendingAction = null;
            pending = false;
            ShowPanel(false);
            confirmations++;
            if (features != null && features.Log != null)
                features.Log.Add(KvLogKind.Motion, "оператор подтвердил пуск после предупреждений");
            Say("пуск подтверждён оператором");
            if (action != null) action();
        }

        public void Cancel()
        {
            if (!pending) return;
            pendingAction = null;
            pending = false;
            ShowPanel(false);
            cancellations++;
            if (features != null && features.Log != null)
                features.Log.Add(KvLogKind.Stop, "пуск отменён оператором после предупреждений");
            Say("пуск отменён оператором");
        }

        /// <summary>Обратный отсчёт окна подтверждения (вызывать каждый кадр).</summary>
        public void Tick(float deltaTime)
        {
            if (!pending) return;
            confirmTimer -= deltaTime;
            if (summaryText != null)
                summaryText.text = BuildText();
            if (countText != null)
                countText.text = "Автоматическая отмена через " + Mathf.CeilToInt(confirmTimer) +
                                 " с — без нажатия кнопки робот не поедет";
            if (confirmTimer <= 0f)
            {
                Say("окно подтверждения закрыто по времени — пуск отменён");
                Cancel();
            }
        }

        // ------------------------------------------------------------------ окно подтверждения

        private void Build(Transform parent)
        {
            if (canvas != null || parent == null) return;
            canvas = KvOverlayKit.CreateCanvas(parent, "KvConfirmCanvas", 52);
            RectTransform canvasRect = (RectTransform)canvas.transform;

            Image dim = KvTheme.CreatePanel(canvasRect, "Dim", new Color(0f, 0f, 0f, 0.55f));
            KvTheme.Stretch(dim.rectTransform, 0f, 0f, 0f, 0f);
            dim.raycastTarget = true;      // модальное окно: клики «сквозь» не проходят

            GameObject go = new GameObject("Panel", typeof(Image));
            go.transform.SetParent(canvasRect, false);
            panel = (RectTransform)go.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(720f, 400f);
            panel.anchoredPosition = Vector2.zero;
            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(KvTheme.WindowBg.r, KvTheme.WindowBg.g, KvTheme.WindowBg.b, 0.98f);

            Text title = KvTheme.CreateText(panel, "Title", "ПРОВЕРКА ПЕРЕД ПУСКОМ", 20,
                TextAnchor.MiddleLeft, KvTheme.Warn);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-24f, 28f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            summaryText = KvTheme.CreateText(panel, "Summary", "", KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, KvTheme.TextMain);
            KvTheme.Stretch(summaryText.rectTransform, 14f, 14f, 44f, 60f);
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Truncate;

            countText = KvTheme.CreateText(panel, "Countdown", "", KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextDim);
            countText.rectTransform.anchorMin = new Vector2(0f, 0f);
            countText.rectTransform.anchorMax = new Vector2(1f, 0f);
            countText.rectTransform.pivot = new Vector2(0.5f, 0f);
            countText.rectTransform.sizeDelta = new Vector2(-24f, 20f);
            countText.rectTransform.anchoredPosition = new Vector2(0f, 42f);

            Button confirm = KvTheme.CreateButton(panel, "Confirm", "ПУСК ПОДТВЕРЖДАЮ",
                delegate { Confirm(); }, 30, false);
            RectTransform cr = (RectTransform)confirm.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.sizeDelta = new Vector2(260f, 30f);
            cr.anchoredPosition = new Vector2(140f, 6f);

            Button cancel = KvTheme.CreateButton(panel, "Cancel", "ОТМЕНА", delegate { Cancel(); }, 30, false);
            RectTransform xr = (RectTransform)cancel.transform;
            xr.anchorMin = xr.anchorMax = new Vector2(0.5f, 0f);
            xr.pivot = new Vector2(0.5f, 0f);
            xr.sizeDelta = new Vector2(180f, 30f);
            xr.anchoredPosition = new Vector2(-150f, 6f);

            canvas.gameObject.SetActive(false);
        }

        private string BuildText()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < findings.Count; i++)
            {
                KvFinding finding = findings[i];
                if (finding.severity == KvSeverity.Info) continue;
                sb.AppendLine("• " + finding.Describe());
            }
            if (sb.Length == 0) sb.AppendLine("замечаний нет");
            if (closestPersonDistance != float.MaxValue)
                sb.AppendLine();
            if (closestPersonDistance != float.MaxValue)
                sb.Append("Ближайшая точка робота к человеку: " +
                          closestPersonDistance.ToString("0.00") + " м");
            return sb.ToString();
        }

        private void ShowPanel(bool value)
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(value);
            if (value && summaryText != null) summaryText.text = BuildText();
        }

        private void Say(string text)
        {
            Debug.Log("[PreRun] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>Уровень записи журнала (этап 36 ТЗ).</summary>
    public enum KvLogLevel { Info = 0, Warning = 1, Error = 2 }

    /// <summary>
    /// ЭТАП 36 ТЗ: УРОВНИ ЖУРНАЛА, ФИЛЬТРАЦИЯ, ПОИСК И ЦВЕТА.
    ///
    /// Журнал проекта (`KvActionLog`) хранит событие с типом. Здесь типы сводятся к УРОВНЯМ
    /// «информация / предупреждение / ошибка», добавляется ПОИСК по тексту и фильтр по уровню,
    /// цвет строки берётся из типа события. Есть счётчики по уровням, автоматическая прокрутка
    /// к последней записи и выгрузка ТОЛЬКО отфильтрованного списка в файл — удобно приложить
    /// к отчёту именно то, что видно на экране.
    /// </summary>
    public class KvLogTools
    {
        public event Action<string> Message;

        private KvLogLevel minLevel = KvLogLevel.Info;
        private string query = "";
        private bool autoScroll = true;
        private int shown;
        private int info, warnings, errors;

        public KvLogLevel MinLevel { get { return minLevel; } }
        public string Query { get { return query; } }
        public bool AutoScroll { get { return autoScroll; } set { autoScroll = value; } }
        public int Shown { get { return shown; } }
        public int InfoCount { get { return info; } }
        public int WarningCount { get { return warnings; } }
        public int ErrorCount { get { return errors; } }

        public void SetLevel(KvLogLevel value)
        {
            minLevel = value;
            Report("уровень журнала: " + LevelLabel(value));
        }

        public void SetQuery(string value)
        {
            query = value == null ? "" : value.Trim().ToLowerInvariant();
            Report(query.Length == 0 ? "поиск сброшен" : "поиск: «" + value + "»");
        }

        /// <summary>Уровень записи по типу события.</summary>
        public static KvLogLevel LevelOf(KvLogKind kind)
        {
            if (kind == KvLogKind.Error) return KvLogLevel.Error;
            if (kind == KvLogKind.Warning || kind == KvLogKind.Stop || kind == KvLogKind.Zone)
                return KvLogLevel.Warning;
            return KvLogLevel.Info;
        }

        public static string LevelLabel(KvLogLevel level)
        {
            switch (level)
            {
                case KvLogLevel.Error: return "ошибки";
                case KvLogLevel.Warning: return "предупреждения";
                default: return "всё";
            }
        }

        /// <summary>Отфильтрованный список (новые в конце). Считает счётчики по уровням.</summary>
        public List<KvLogEntry> View()
        {
            List<KvLogEntry> all = KvActionLog.Instance.Entries != null
                ? new List<KvLogEntry>(KvActionLog.Instance.Entries)
                : new List<KvLogEntry>();

            info = warnings = errors = 0;
            for (int i = 0; i < all.Count; i++)
            {
                KvLogLevel level = LevelOf(all[i].kind);
                if (level == KvLogLevel.Error) errors++;
                else if (level == KvLogLevel.Warning) warnings++;
                else info++;
            }

            List<KvLogEntry> result = new List<KvLogEntry>();
            for (int i = 0; i < all.Count; i++)
            {
                if (LevelOf(all[i].kind) < minLevel) continue;
                if (query.Length > 0 && (all[i].text == null ||
                    all[i].text.ToLowerInvariant().IndexOf(query, StringComparison.Ordinal) < 0)) continue;
                result.Add(all[i]);
            }
            shown = result.Count;
            return result;
        }

        /// <summary>Последние `count` записей отфильтрованного списка (для панели).</summary>
        public List<KvLogEntry> Tail(int count)
        {
            List<KvLogEntry> view = View();
            if (view.Count <= count) return view;
            return view.GetRange(view.Count - count, count);
        }

        public string LastLine()
        {
            List<KvLogEntry> view = View();
            return view.Count == 0 ? "нет записей по фильтру" : view[view.Count - 1].Line;
        }

        /// <summary>Выгрузить то, что видно на экране (с учётом уровня и поиска).</summary>
        public string ExportVisible()
        {
            List<KvLogEntry> view = View();
            try
            {
                string path = Path.Combine(FeatureStorage.LogsDir,
                    "log_filtered_" + FeatureStorage.TimeStamp() + ".log");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Журнал KazistovVv · фильтр: уровень " + LevelLabel(minLevel) +
                              (query.Length > 0 ? ", поиск «" + query + "»" : "") +
                              " · записей " + view.Count);
                sb.AppendLine("информация: " + info + " · предупреждения: " + warnings +
                              " · ошибки: " + errors);
                sb.AppendLine();
                for (int i = 0; i < view.Count; i++) sb.AppendLine(view[i].Line);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Report("выгружено записей: " + view.Count + " → " + path);
                return path;
            }
            catch (Exception e)
            {
                Report("выгрузка не удалась: " + e.Message);
                return "";
            }
        }

        public string Status()
        {
            return "уровень: " + LevelLabel(minLevel) +
                   (query.Length > 0 ? " · поиск: «" + query + "»" : " · поиск не задан") +
                   " · показано " + shown + " из " + (info + warnings + errors) +
                   " (инф. " + info + ", предупр. " + warnings + ", ошибок " + errors + ")";
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[LogTools] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>ВКЛАДКА «ОТКАЗЫ» (ЭТАП 34 ТЗ).</summary>
    public class KvFailureTab : IKvWorkbenchTab
    {
        private readonly KvFailureSimulator failures;
        private InputField jointField;
        private KvSegmented modeSegment;
        private float jointNumber = 1f;

        public KvFailureTab(KvFailureSimulator simulator) { failures = simulator; }

        public string Key { get { return "failures"; } }
        public string Title { get { return KvLocExtra3.T("fail.title", "Имитация отказов"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("fail.title", "Имитация отказов и безопасность"));
            kit.Slider(T("fail.joint", "Номер сустава"), 1f, 6f, jointNumber, "0",
                delegate (float v) { jointNumber = Mathf.Round(v); });

            // ФИКС 11.А: вид отказа сустава — провисание, потеря управления или фиксация тормозом.
            modeSegment = kit.Segmented(T("fail.jointmode", "Вид отказа сустава"),
                new[]
                {
                    T("fail.mode.droop", "провисание"),
                    T("fail.mode.lost", "потеря управления"),
                    T("fail.mode.brake", "с фиксацией")
                }, (int)failures.JointMode,
                delegate (int i) { failures.SetJointMode((KvJointFailureMode)i); });

            kit.Slider(T("fail.rate", "Скорость провисания, °/с"), 0f, 90f, failures.DroopRate, "0",
                failures.SetDroopRate);
            kit.Buttons(new[]
            {
                T("fail.jointloss", "Отказ сустава"),
                T("fail.comms", "Потеря связи"),
                T("fail.overload", "Перегрузка")
            }, new Action[]
            {
                delegate { failures.Start(KvFailureKind.JointLoss, Mathf.RoundToInt(jointNumber)); },
                delegate { failures.Start(KvFailureKind.CommsLoss); },
                delegate { failures.Start(KvFailureKind.Overload); }
            });

            kit.Info(delegate { return failures.Status(); },
                failures.Active ? KvTheme.Error : KvTheme.Ok);
            kit.Info(delegate
            {
                string reason;
                bool allowed = failures.MotionAllowed(out reason);
                return allowed
                    ? T("fail.motion.ok", "движение разрешено")
                    : T("fail.motion.no", "движение запрещено") + ": " + reason;
            }, failures.MotionAllowed(out string _) ? KvTheme.TextMain : KvTheme.Warn);

            kit.Buttons(new[] { T("fail.reset", "Сброс аварии"), T("fail.log", "Проверить журнал") },
                new Action[]
                {
                    delegate { failures.Clear(); },
                    delegate
                    {
                        KvActionLog log = KvActionLog.Instance;
                        string last = log.Tail(1).Count > 0 ? log.Tail(1)[0].Line : "журнал пуст";
                        Debug.Log("[Failure] последняя запись журнала: " + last);
                    }
                });
            kit.Note(T("fail.info",
                "Отказ сустава: момент веса звеньев и груза берётся у штатной модели нагрузки, а звено " +
                "провисает по модели МАЯТНИКА С ВЯЗКИМ ТРЕНИЕМ — скорость набирается по τ/I, тормозится " +
                "трением k_d и плавно затухает у упора или в равновесии (поля inertiaKgM2 и " +
                "viscousFriction). Поза ОСТАЛЬНЫХ суставов считается зафиксированной в момент отказа, " +
                "но их вклад в момент пересчитывается на каждом шаге. Это по-прежнему УПРОЩЁННАЯ МОДЕЛЬ, " +
                "а НЕ физический движок: динамика Ньютона–Эйлера, трение в редукторах, люфты и упругость " +
                "звеньев не считаются. «Потеря управления» — ось обесточена, её цель игнорируется, робот " +
                "работает остальными осями; «с фиксацией» — тормоз сработал, угол заморожен до сброса " +
                "аварии. Потеря связи блокирует команды и останавливает движение, перегрузка увеличивает " +
                "массу груза и останавливает робота. После любого отказа нужен «Сброс аварии» и переезд " +
                "домой."), KvTheme.TextDim);
        }

        public void Tick() { }

        public void Refresh()
        {
            if (modeSegment != null && modeSegment.Index != (int)failures.JointMode)
                modeSegment.Set((int)failures.JointMode);
        }
    }

    /// <summary>ВКЛАДКА «ПРОВЕРКА ПЕРЕД ПУСКОМ» (ЭТАП 35 ТЗ; ФИКС 9 — манекены).</summary>
    public class KvValidateTab : IKvWorkbenchTab
    {
        private readonly KvPreRunValidator validator;
        private readonly Func<bool> startAction;
        private readonly KvMannequins mannequins;

        public KvValidateTab(KvPreRunValidator preRun, Func<bool> start, KvMannequins mannequinsService = null)
        {
            validator = preRun;
            startAction = start;
            mannequins = mannequinsService;
        }

        public string Key { get { return "validate"; } }
        public string Title { get { return KvLocExtra3.T("valid.title", "Проверка перед пуском"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("valid.title", "Проверка перед пуском"));
            kit.Buttons(new[]
            {
                T("valid.run", "Проверить и пустить"),
                T("valid.check", "Только проверить"),
                T("valid.confirm", "Подтвердить пуск"),
                T("valid.cancel", "Отмена")
            }, new Action[]
            {
                delegate
                {
                    validator.RequestRun(delegate
                    {
                        if (startAction != null) startAction();
                    });
                },
                delegate { validator.Validate(); },
                delegate { validator.Confirm(); },
                delegate { validator.Cancel(); }
            });
            kit.Info(delegate
            {
                return validator.LastSummary + " · проверок " + validator.ValidationRuns +
                       " · подтверждений " + validator.Confirmations +
                       " · отмен " + validator.Cancellations +
                       " · запретов " + validator.BlockedRuns;
            }, validator.Pending ? KvTheme.Warn : KvTheme.Accent);
            kit.Info(delegate
            {
                float distance = validator.ClosestPersonDistance;
                return distance < 0f
                    ? T("valid.person.none", "расстояние до человека не считалось")
                    : T("valid.person", "ближайшее расстояние до человека") + ": " +
                      distance.ToString("0.00") + " м";
            }, KvTheme.TextMain);
            kit.Table(T("valid.count", "Замечаний"), delegate
            {
                int critical = 0, warnings = 0;
                for (int i = 0; i < validator.Findings.Count; i++)
                {
                    if (validator.Findings[i].severity == KvSeverity.Critical) critical++;
                    else if (validator.Findings[i].severity == KvSeverity.Warning) warnings++;
                }
                return "критичных " + critical + " · предупреждений " + warnings;
            }, delegate { return validator.Pending ? "окно подтверждения открыто" : ""; });

            for (int slot = 0; slot < 6; slot++)
            {
                int index = slot;
                kit.Info(delegate
                {
                    if (index >= validator.Findings.Count) return "";
                    KvFinding finding = validator.Findings[index];
                    return finding.Describe();
                }, KvTheme.TextMain);
            }

            // ФИКС 9: манекены рядом с рабочей зоной. Переключатель по умолчанию ВКЛЮЧЁН —
            // проверка видит и оператора, и манекенов. Ползунок переставляет их от робота.
            if (mannequins != null)
            {
                kit.Divider();
                kit.Section(T("valid.mannequins", "Манекены в зоне"));
                kit.Toggle(T("valid.mannequins.include", "Учитывать манекены в проверке"),
                    mannequins.Include, delegate (bool v)
                    {
                        mannequins.Include = v;
                        validator.PersonSourceNote = mannequins.SourceNote();
                    });
                kit.Slider(T("valid.mannequins.distance", "Вынос манекенов от робота, м"),
                    0.5f, 4f, mannequins.Distance, "0.0",
                    delegate (float v) { mannequins.Distance = v; });
                kit.Info(delegate { return mannequins.Status(); }, KvTheme.TextDim);
                kit.Note(T("valid.mannequins.note",
                    "Два манекена (простые капсулы, коллайдер-триггер) стоят на полу рядом с рабочей " +
                    "зоной: 1.5 м в сторону от робота и 1.5 м в сторону прохода между стендами. " +
                    "Они скрыты в дереве иерархии; переставлять — ползунком выше. Манекены " +
                    "учитываются вместе с оператором (камера/шлем)."), KvTheme.TextDim);
            }

            kit.Note(T("valid.info",
                "Проверяются: зазор до препятствий, БЛИЗОСТЬ К ЧЕЛОВЕКУ, зоны запрета, запас до " +
                "лимитов, особенность, перегрузка, нагрев и аварийное состояние. Критичные " +
                "замечания пуск запрещают; предупреждения требуют нажатия «Подтвердить пуск» — " +
                "через 20 с окно закрывается само, и робот не поедет."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh() { }
    }

    /// <summary>ВКЛАДКА «ЖУРНАЛ: УРОВНИ И ПОИСК» (ЭТАП 36 ТЗ).</summary>
    public class KvLogToolsTab : IKvWorkbenchTab
    {
        private readonly KvLogTools tools;
        private readonly KvActionLog log;
        private KvSegmented levelSegment;
        private InputField searchField;

        public KvLogToolsTab(KvLogTools logTools, KvActionLog actionLog)
        {
            tools = logTools;
            log = actionLog;
        }

        public string Key { get { return "logtools"; } }
        public string Title { get { return KvLocExtra3.T("logtools.title", "Журнал: уровни и поиск"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("logtools.title", "Журнал: уровни, фильтр, поиск"));
            levelSegment = kit.Segmented(T("logtools.level", "Уровень"),
                new[]
                {
                    T("logtools.level.all", "всё"),
                    T("logtools.level.warn", "предупреждения"),
                    T("logtools.level.err", "ошибки")
                }, (int)tools.MinLevel, delegate (int i) { tools.SetLevel((KvLogLevel)i); });

            searchField = KvInputKit.Single(kit.Content, T("logtools.search", "Поиск по тексту"), "",
                "например: траектория", delegate (string text) { tools.SetQuery(text); });
            kit.Toggle(T("logtools.autoscroll", "Показывать последние записи"), tools.AutoScroll,
                delegate (bool v) { tools.AutoScroll = v; });

            kit.Info(delegate { return tools.Status(); }, KvTheme.Accent);
            kit.Info(delegate { return tools.LastLine(); }, KvTheme.TextMain);

            for (int slot = 0; slot < 10; slot++)
            {
                int index = slot;
                kit.Info(delegate
                {
                    List<KvLogEntry> tail = tools.Tail(200);
                    if (index >= tail.Count) return "";
                    int position = tail.Count - 1 - index;
                    if (position < 0) return "";
                    KvLogEntry entry = tail[position];
                    string kind = KvActionLog.ShortKind(entry.kind);
                    string text = entry.text ?? "";
                    if (text.Length > 96) text = text.Substring(0, 93) + "…";
                    return entry.stamp + "  " + kind + "  " + text;
                }, KvTheme.TextMain);
            }

            kit.Buttons(new[]
            {
                T("logtools.export", "Выгрузить видимое"),
                T("logtools.test", "Проверка уровней"),
                T("logtools.clear", "Очистить журнал")
            }, new Action[]
            {
                delegate { tools.ExportVisible(); },
                delegate
                {
                    log.Add(KvLogKind.System, "проверка уровней: информационная запись");
                    log.Warning("проверка уровней: предупреждение");
                    log.Error("проверка уровней: ошибка");
                },
                delegate { log.Clear(); }
            });
            kit.Note(T("logtools.info",
                "Уровни: информация (система, точки, траектории, движение), предупреждения (стоп, " +
                "зоны, предупреждения), ошибки. Цвет строки берётся из типа события, поиск идёт по " +
                "тексту, выгрузка сохраняет только то, что видно на экране."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh()
        {
            if (levelSegment != null && levelSegment.Index != (int)tools.MinLevel)
                levelSegment.Set((int)tools.MinLevel);
        }
    }
}
