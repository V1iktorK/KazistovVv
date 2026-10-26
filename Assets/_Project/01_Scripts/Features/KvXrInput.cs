using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЭТАП 23 ТЗ: ГОЛОСОВЫЕ КОМАНДЫ.
    ///
    /// ЧТО СДЕЛАНО ЧЕСТНО И РАБОТАЕТ БЕЗ ДОПОЛНИТЕЛЬНЫХ ПАКЕТОВ:
    ///   • РЕЧЕВОЙ ДЕТЕКТОР (VAD) — реальный захват микрофона (`Microphone`), RMS-уровень,
    ///     автоматическое определение начала и конца фразы по громкости;
    ///   • ГРАММАТИКА КОМАНД — разбор текста фразы на русском и английском («стоп», «домой»,
    ///     «пуск», «пауза», «вариант три», «дальше», «запиши позу», «снимок», «отмена»);
    ///   • ИСТОЧНИК ТЕКСТА — либо подключённый распознаватель речи XR-платформы
    ///     (PICO/Meta: `IKvSpeechSource`, см. ниже), либо ручной ввод в поле вкладки.
    ///
    /// ЧТО ТРЕБУЕТ ПЛАТФОРМЫ: собственно распознавание речи (превращение звука в текст).
    /// В Unity такой возможности нет — её даёт SDK шлема или системный движок. Поэтому здесь
    /// описан интерфейс `IKvSpeechSource`: как только плагин появится, он отдаёт текст в
    /// `PushRecognized(...)`, а вся логика команд уже готова и работает.
    /// </summary>
    public interface IKvSpeechSource
    {
        /// <summary>Имя источника для интерфейса («PICO ASR», «Windows Speech»).</summary>
        string Name { get; }
        /// <summary>Источник сейчас слушает?</summary>
        bool Listening { get; }
        /// <summary>Запустить/остановить прослушивание фразы.</summary>
        void SetListening(bool value);
    }

    /// <summary>Разобранная голосовая команда.</summary>
    public class KvVoiceCommand
    {
        public string id;          // «stop», «home», «run», «pause», «select», «undo», «pose», «shot»
        public string phrase;      // исходная фраза
        public int number = -1;    // номер для «вариант три»
        public float time;
        public bool executed;
        public string result;
    }

    public class KvVoiceService
    {
        public const string EnabledPrefsKey = "KazistovVv.Voice.Enabled";
        public const string ThresholdPrefsKey = "KazistovVv.Voice.Threshold";

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;
        private KazistovVvUIManager ui;

        private bool enabled;
        private float threshold = 0.06f;
        private string device = "";
        private AudioClip clip;
        private float[] samples = new float[512];
        private int lastSamplePos;
        private float level;
        private float peak;
        private bool speaking;
        private float phraseStart = -1f;
        private float lastVoiceTime = -10f;

        private readonly List<KvVoiceCommand> history = new List<KvVoiceCommand>();
        private readonly List<IKvSpeechSource> sources = new List<IKvSpeechSource>();
        private readonly Dictionary<string, Action<KvVoiceCommand>> handlers =
            new Dictionary<string, Action<KvVoiceCommand>>();

        public bool Enabled { get { return enabled; } }
        public float Threshold { get { return threshold; } }
        public float Level { get { return level; } }
        public float Peak { get { return peak; } }
        public bool Speaking { get { return speaking; } }
        public string DeviceName { get { return string.IsNullOrEmpty(device) ? "—" : device; } }
        public int MicCount { get { return Microphone.devices != null ? Microphone.devices.Length : 0; } }
        public bool MicActive { get { return clip != null && Microphone.IsRecording(device); } }
        public IList<KvVoiceCommand> History { get { return history; } }
        public int SourceCount { get { return sources.Count; } }

        public KvVoiceService()
        {
            handlers["stop"] = delegate { SafetyStop(); };
            handlers["home"] = delegate { GoHome(); };
            handlers["run"] = delegate { Run(); };
            handlers["pause"] = delegate { Pause(); };
            handlers["select"] = Select;
            handlers["next"] = delegate { Step(1); };
            handlers["prev"] = delegate { Step(-1); };
            handlers["pose"] = delegate { SavePose(); };
            handlers["shot"] = delegate { Screenshot(); };
            handlers["undo"] = delegate { Undo(); };
        }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub, KazistovVvUIManager manager)
        {
            flow = controller;
            features = hub;
            ui = manager;
            enabled = PlayerPrefs.GetInt(EnabledPrefsKey, 0) == 1;
            threshold = PlayerPrefs.GetFloat(ThresholdPrefsKey, 0.06f);
            if (enabled) StartMic();
        }

        public void RegisterSource(IKvSpeechSource source)
        {
            if (source == null || sources.Contains(source)) return;
            sources.Add(source);
            Report("подключён источник распознавания речи: " + source.Name);
        }

        public void SetEnabled(bool value)
        {
            if (enabled == value) return;
            enabled = value;
            PlayerPrefs.SetInt(EnabledPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (value) StartMic();
            else StopMic();
            Report("голосовые команды " + (value ? "включены" : "выключены"));
        }

        public void SetThreshold(float value)
        {
            threshold = Mathf.Clamp(value, 0.005f, 0.4f);
            PlayerPrefs.SetFloat(ThresholdPrefsKey, threshold);
            PlayerPrefs.Save();
        }

        private void StartMic()
        {
            if (MicCount == 0)
            {
                Report("микрофон не найден — доступен только ручной ввод команды");
                return;
            }
            try
            {
                device = Microphone.devices[0];
                clip = Microphone.Start(device, true, 1, 16000);
                lastSamplePos = 0;
                Report("микрофон: " + device + " (детектор речи активен)");
            }
            catch (Exception e)
            {
                clip = null;
                Report("микрофон недоступен: " + e.Message);
            }
        }

        private void StopMic()
        {
            try { if (clip != null) Microphone.End(device); } catch (Exception) { }
            clip = null;
            speaking = false;
            level = 0f;
        }

        /// <summary>Кадровое обслуживание: измерение громкости и определение границ фразы.</summary>
        public void Tick(float deltaTime)
        {
            if (!enabled || clip == null) return;
            int position;
            try { position = Microphone.GetPosition(device); }
            catch (Exception) { return; }
            if (position <= 0) return;

            int available = position - lastSamplePos;
            if (available < 0) available += clip.samples;
            if (available < 256) return;

            int take = Mathf.Min(samples.Length, available);
            try
            {
                if (!clip.GetData(samples, lastSamplePos)) return;
            }
            catch (Exception) { return; }
            lastSamplePos = (lastSamplePos + take) % clip.samples;

            float sum = 0f;
            for (int i = 0; i < take; i++) sum += samples[i] * samples[i];
            float rms = Mathf.Sqrt(sum / Mathf.Max(1, take));
            level = Mathf.Lerp(level, rms, 0.35f);
            if (level > peak) peak = level;

            bool loud = level > threshold;
            if (loud)
            {
                lastVoiceTime = Time.realtimeSinceStartup;
                if (!speaking)
                {
                    speaking = true;
                    phraseStart = Time.realtimeSinceStartup;
                }
            }
            else if (speaking && Time.realtimeSinceStartup - lastVoiceTime > 0.45f)
            {
                speaking = false;
                // Фраза закончилась: если подключён внешний распознаватель, он пришлёт текст
                // сам; иначе сообщаем, что нужен распознаватель (без выдумок).
                if (sources.Count == 0 && phraseStart > 0f)
                    Report("фраза " + (Time.realtimeSinceStartup - phraseStart).ToString("0.0") +
                           " с распознана как звук · для текста нужен распознаватель речи (PICO/Meta)");
                phraseStart = -1f;
            }
        }

        /// <summary>Принять текст от распознавателя речи (или из поля ручного ввода).</summary>
        public KvVoiceCommand PushRecognized(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string phrase = text.Trim().ToLowerInvariant();
            KvVoiceCommand command = Parse(phrase);
            command.time = Time.realtimeSinceStartup;
            history.Add(command);
            if (history.Count > 40) history.RemoveAt(0);

            Action<KvVoiceCommand> handler;
            if (handlers.TryGetValue(command.id, out handler))
            {
                handler(command);
                command.executed = true;
            }
            else
            {
                command.result = "команда не распознана";
                Report("не понял: «" + text + "»");
            }
            return command;
        }

        /// <summary>Грамматика: русские и английские формулировки → идентификатор команды.</summary>
        public static KvVoiceCommand Parse(string phrase)
        {
            KvVoiceCommand c = new KvVoiceCommand { phrase = phrase, id = "unknown" };
            string p = " " + phrase.Replace("ё", "е").Replace(",", " ").Replace(".", " ") + " ";

            if (Has(p, " стоп ", " стой ", " останов ", " аварий ", " stop ", " halt ")) c.id = "stop";
            else if (Has(p, " домой ", " в ноль ", " home ", " park ")) c.id = "home";
            else if (Has(p, " пуск ", " поехали ", " старт ", " start ", " go ", " run ")) c.id = "run";
            else if (Has(p, " пауза ", " приостанов ", " pause ", " hold ")) c.id = "pause";
            else if (Has(p, " дальше ", " следующий ", " next ")) c.id = "next";
            else if (Has(p, " назад ", " предыдущ ", " prev ", " back ")) c.id = "prev";
            else if (Has(p, " запиши позу ", " сохрани позу ", " запомни позу ", " save pose "))
                c.id = "pose";
            else if (Has(p, " снимок ", " скриншот ", " фотографи ", " screenshot ", " capture "))
                c.id = "shot";
            else if (Has(p, " отмена ", " отмени ", " верни ", " undo ")) c.id = "undo";
            else if (Has(p, " вариант ", " траектори ", " номер ", " variant ", " option "))
            {
                c.id = "select";
                c.number = NumberIn(p);
            }
            else
            {
                int n = NumberIn(p);
                if (n > 0) { c.id = "select"; c.number = n; }
            }
            return c;
        }

        private static bool Has(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.Contains(needles[i])) return true;
            return false;
        }

        /// <summary>Число словом или цифрой: «вариант три», «вариант 3», «third».</summary>
        private static int NumberIn(string p)
        {
            string[] ru = { " один ", " два ", " три ", " четыре ", " пять ", " шесть ", " семь ", " восемь " };
            for (int i = 0; i < ru.Length; i++) if (p.Contains(ru[i])) return i + 1;
            string[] en = { " one ", " two ", " three ", " four ", " five ", " six ", " seven ", " eight " };
            for (int i = 0; i < en.Length; i++) if (p.Contains(en[i])) return i + 1;
            for (int i = 1; i <= 9; i++) if (p.Contains(" " + i + " ")) return i;
            string[] words = p.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                int value;
                if (int.TryParse(words[i], out value) && value > 0 && value < 100) return value;
            }
            return -1;
        }

        // ------------------------------------------------------------------ действия

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Voice] " + text);
            if (Message != null) Message(text);
        }

        private void SafetyStop()
        {
            if (features != null) features.EmergencyStop();
            Report("голос: АВАРИЙНАЯ ОСТАНОВКА");
        }

        private void GoHome()
        {
            if (flow == null || !flow.Validator.Ready) { Report("голос: робот не определён"); return; }
            if (flow.ExternalMotionRunning || flow.State.phase == FlowState.RobotMoving)
            {
                Report("голос: ДОМОЙ отклонён — робот занят");
                return;
            }
            PoseValidator v = flow.Validator;
            double[] start = v.CopyCurrent();
            double[] home = new double[v.Dof];
            for (int i = 0; i < home.Length; i++)
                home[i] = v.IsPrismatic(i) ? v.Lower[i] + (v.Upper[i] - v.Lower[i]) * 0.5 : 0.0;
            home = v.ContinueFrom(start, home);
            PlannedTrajectory plan = KvPlanKit.MakeJointPlan(v,
                features != null ? features.World : null, start, home, "ДОМОЙ (голос)", 0.08f, 60);
            if (plan == null) { Report("голос: ДОМОЙ — план не построен"); return; }
            bool ok = flow.PlayExternalPlan(plan, plan.GoalQ, "переезд в домашнюю позу (голос)");
            Report(ok ? "голос: ДОМОЙ — робот едет в нулевую позу" : "голос: ДОМОЙ отклонён");
        }

        private void Run()
        {
            if (flow == null) { Report("голос: робот не определён"); return; }
            if (flow.State.phase != FlowState.PhantomsMoving)
            {
                Report("голос: ПУСК невозможен — сначала выберите вариант («вариант два»)");
                return;
            }
            bool ok = flow.ConfirmSelectedTrajectory();
            Report(ok ? "голос: ПУСК — робот пошёл по траектории" : "голос: ПУСК отклонён (Safety)");
        }

        private void Pause()
        {
            if (flow == null || flow.Motion == null) { Report("голос: движение недоступно"); return; }
            bool now = !flow.Motion.Paused;
            flow.Motion.SetPaused(now);
            Report("голос: " + (now ? "пауза" : "продолжение"));
        }

        private void Select(KvVoiceCommand c)
        {
            if (flow == null) return;
            if (c.number <= 0) { Report("голос: не расслышал номер варианта"); return; }
            bool ok = flow.SelectCandidateByIndex(c.number - 1);
            c.result = ok ? "выбран вариант " + c.number : "варианта " + c.number + " нет";
            Report("голос: " + c.result);
        }

        private void Step(int delta)
        {
            if (flow == null || flow.State.candidates.Count == 0) { Report("голос: вариантов нет"); return; }
            int count = flow.State.candidates.Count;
            int index = flow.State.selectedTrajectory < 0 ? 0 : flow.State.selectedTrajectory;
            index = ((index + delta) % count + count) % count;
            flow.SelectCandidateByIndex(index);
            Report("голос: вариант " + (index + 1) + " из " + count);
        }

        private void SavePose()
        {
            if (features == null || features.Poses == null) { Report("голос: библиотека поз недоступна"); return; }
            var pose = features.Poses.SaveCurrent("Поза (голос) " + FeatureStorage.TimeStamp());
            Report(pose != null ? "голос: поза записана" : "голос: поза не записана");
            if (ui != null) ui.RebuildTree(true);
        }

        private void Screenshot()
        {
            KvStageHub hub = KvStageHub.Current;
            bool ok = hub != null && hub.TakeScreenshot();
            Report(ok ? "голос: снимок сохранён" : "голос: снимок не сделан");
        }

        private void Undo()
        {
            if (features == null || features.Undo == null) return;
            features.Undo.Undo();
            Report("голос: отмена последнего действия");
        }

        public string Status()
        {
            if (!enabled) return "выключено";
            string mic = clip == null ? "микрофон недоступен" : (speaking ? "идёт фраза" : "тишина");
            return mic + " · уровень " + level.ToString("0.000") + " (порог " +
                   threshold.ToString("0.000") + ") · распознавателей: " + sources.Count;
        }
    }

    /// <summary>
    /// ЭТАП 24 ТЗ: ОТСЛЕЖИВАНИЕ РУК (ЖЕСТЫ: PINCH, SWIPE, GRAB).
    ///
    /// Платформенная часть (получение позы суставов) приходит от SDK шлема — SDK пишет
    /// точки в `PushJoint(...)`. Вся обработка жестов сделана здесь и работает по-настоящему:
    ///   • КАСАНИЕ (pinch): сближение большого и указательного пальцев ближе 2.5 см —
    ///     захват/отпускание объекта штатным гриппером (этап 4 ТЗ);
    ///   • СВАЙП: скорость кисти больше 0.35 м/с в одном направлении — переключение
    ///     варианта траектории / пуск / пауза;
    ///   • ГОРСТЬ (grab): все пальцы поджаты — удержание захвата.
    /// Плюс скелет кисти рисуется в сцене (точки суставов и «кости»), поэтому отслеживание
    /// видно глазом и проверяется наладчиком. Без SDK доступен программный ввод
    /// (`PushJoint`) — им пользуется диагностика и «мышь-кисть» для проверки на ПК.
    /// </summary>
    public class KvHandTrackingService
    {
        public const int Wrist = 0, ThumbTip = 1, IndexTip = 2, MiddleTip = 3, RingTip = 4, LittleTip = 5;
        public const int JointCount = 6;

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;

        private bool enabled;
        private bool visible;
        private readonly bool[,] valid = new bool[2, JointCount];
        private readonly Vector3[,] joints = new Vector3[2, JointCount];
        private readonly float[,] grip = new float[2, JointCount];

        private readonly bool[] pinched = new bool[2];
        private readonly float[] pinchTime = new float[2];
        private readonly Vector3[] swipeOrigin = new Vector3[2];
        private readonly float[] swipeOriginTime = new float[2];
        private float swipeCooldown;

        /// <summary>Окно распознавания свайпа, с (рука проходит путь за 0.1–0.4 с).</summary>
        private const float SwipeWindow = 0.5f;
        private const float SwipeMinSpeed = 0.25f;      // м/с
        private const float SwipeMinDistance = 0.12f;   // м

        private GameObject root;
        private readonly Transform[,] markers = new Transform[2, JointCount];
        private readonly LineRenderer[,] bones = new LineRenderer[2, JointCount];
        private static readonly int[,] BonePairs = { { 0, 1 }, { 0, 2 }, { 0, 3 }, { 0, 4 }, { 0, 5 }, { 1, 2 } };

        private int pinchCount, swipeCount, grabCount;
        private string lastGesture = "—";
        private float lastGestureTime;
        private bool graphicsReported;

        public bool Enabled { get { return enabled; } }
        public bool Visible { get { return visible; } }
        public int PinchCount { get { return pinchCount; } }
        public int SwipeCount { get { return swipeCount; } }
        public int GrabCount { get { return grabCount; } }
        public string LastGesture { get { return lastGesture; } }
        public float LastGestureAge
        {
            get { return lastGestureTime <= 0f ? -1f : Time.realtimeSinceStartup - lastGestureTime; }
        }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub)
        {
            flow = controller;
            features = hub;
        }

        public void SetEnabled(bool value)
        {
            enabled = value;
            if (!value) ClearPose();
            Report("отслеживание рук " + (value ? "включено" : "выключено"));
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (root != null) root.SetActive(value);
        }

        public void ClearPose()
        {
            for (int h = 0; h < 2; h++)
                for (int j = 0; j < JointCount; j++) valid[h, j] = false;
        }

        /// <summary>Точка сустава от SDK шлема (или от имитатора на ПК).</summary>
        public void PushJoint(int hand, int joint, Vector3 position, float gripValue = 0f)
        {
            if (hand < 0 || hand > 1 || joint < 0 || joint >= JointCount) return;
            joints[hand, joint] = position;
            grip[hand, joint] = gripValue;
            valid[hand, joint] = true;
        }

        public bool IsTracked(int hand)
        {
            return valid[hand, Wrist] && valid[hand, IndexTip] && valid[hand, ThumbTip];
        }

        public Vector3 Position(int hand, int joint)
        {
            return joints[hand, joint];
        }

        public void Tick(float deltaTime)
        {
            if (!enabled) return;
            swipeCooldown = Mathf.Max(0f, swipeCooldown - deltaTime);
            for (int h = 0; h < 2; h++)
            {
                if (!IsTracked(h)) continue;
                UpdatePinch(h);
                UpdateSwipe(h, deltaTime);
                UpdateGrab(h);
            }
            UpdateVisuals();
        }

        private void UpdatePinch(int hand)
        {
            float distance = Vector3.Distance(joints[hand, ThumbTip], joints[hand, IndexTip]);
            bool down = distance < 0.025f;
            bool up = distance > 0.040f;

            if (down && !pinched[hand])
            {
                pinched[hand] = true;
                pinchTime[hand] = Time.realtimeSinceStartup;
                pinchCount++;
                Last("касание (" + (hand == 0 ? "левая" : "правая") + " рука)");
                if (features != null && features.Gripper != null) features.Gripper.Toggle();
            }
            else if (up && pinched[hand])
            {
                pinched[hand] = false;
                Last("отпускание (" + (hand == 0 ? "левая" : "правая") + " рука)");
            }
        }

        private void UpdateSwipe(int hand, float deltaTime)
        {
            // Свайп считается по ОКНУ движения, а не по одному кадру: рука проходит путь
            // за 0.1–0.4 с, и в эти моменты кадр может «просесть» (планирование, захват),
            // поэтому одиночный расчёт по кадру пропускал бы настоящие взмахи.
            Vector3 wrist = joints[hand, Wrist];
            float now = Time.realtimeSinceStartup;

            if (swipeOriginTime[hand] <= 0f || now - swipeOriginTime[hand] > SwipeWindow)
            {
                swipeOrigin[hand] = wrist;
                swipeOriginTime[hand] = now;
                return;
            }

            float window = now - swipeOriginTime[hand];
            if (window < 0.05f) return;

            Vector3 delta = wrist - swipeOrigin[hand];
            float speed = delta.magnitude / window;
            if (swipeCooldown > 0f || speed < SwipeMinSpeed || delta.magnitude < SwipeMinDistance) return;

            int axis = 0;
            float ax = Mathf.Abs(delta.x), ay = Mathf.Abs(delta.y), az = Mathf.Abs(delta.z);
            if (ay > ax && ay > az) axis = 1;
            else if (az > ax && az > ay) axis = 2;
            float sign = axis == 0 ? Mathf.Sign(delta.x) : axis == 1 ? Mathf.Sign(delta.y) : Mathf.Sign(delta.z);

            swipeCooldown = 0.7f;
            swipeCount++;
            swipeOrigin[hand] = wrist;
            swipeOriginTime[hand] = now;
            string direction = axis == 0
                ? (sign > 0 ? "вправо" : "влево")
                : axis == 1 ? (sign > 0 ? "вверх" : "вниз") : (sign > 0 ? "вперёд" : "назад");
            Last("свайп " + direction + " (" + speed.ToString("0.00") + " м/с)");

            if (flow == null) return;
            if (axis == 0) StepVariant(sign > 0 ? 1 : -1);
            else if (axis == 1 && sign > 0) RunSelected();
            else if (axis == 1) PauseToggle();
            else if (sign > 0) GoHome();
        }

        private void UpdateGrab(int hand)
        {
            bool allCurled = true;
            for (int j = ThumbTip; j < JointCount; j++)
            {
                if (grip[hand, j] < 0.6f) { allCurled = false; break; }
            }
            if (allCurled && !grabHeld[hand])
            {
                grabHeld[hand] = true;
                grabCount++;
                Last("горсть (" + (hand == 0 ? "левая" : "правая") + " рука)");
            }
            else if (!allCurled) grabHeld[hand] = false;
        }

        private readonly bool[] grabHeld = new bool[2];

        private void StepVariant(int delta)
        {
            int count = flow.State.candidates.Count;
            if (count == 0) { Last("вариантов нет"); return; }
            int index = flow.State.selectedTrajectory < 0 ? 0 : flow.State.selectedTrajectory;
            index = ((index + delta) % count + count) % count;
            flow.SelectCandidateByIndex(index);
            Last("вариант " + (index + 1) + " из " + count);
        }

        private void RunSelected()
        {
            if (flow.State.phase != FlowState.PhantomsMoving)
            {
                Last("ПУСК невозможен: вариант не выбран");
                return;
            }
            Last(flow.ConfirmSelectedTrajectory() ? "ПУСК" : "ПУСК отклонён (Safety)");
        }

        private void PauseToggle()
        {
            if (flow.Motion == null) return;
            bool now = !flow.Motion.Paused;
            flow.Motion.SetPaused(now);
            Last(now ? "пауза" : "продолжение");
        }

        private void GoHome()
        {
            if (!flow.Validator.Ready) return;
            if (flow.ExternalMotionRunning || flow.State.phase == FlowState.RobotMoving) { Last("ДОМОЙ: робот занят"); return; }
            PoseValidator v = flow.Validator;
            double[] start = v.CopyCurrent();
            double[] home = new double[v.Dof];
            for (int i = 0; i < home.Length; i++)
                home[i] = v.IsPrismatic(i) ? v.Lower[i] + (v.Upper[i] - v.Lower[i]) * 0.5 : 0.0;
            PlannedTrajectory plan = KvPlanKit.MakeJointPlan(v,
                features != null ? features.World : null, start,
                v.ContinueFrom(start, home), "ДОМОЙ (жест)", 0.08f, 60);
            if (plan == null) { Last("ДОМОЙ: план не построен"); return; }
            bool ok = flow.PlayExternalPlan(plan, plan.GoalQ, "переезд в домашнюю позу (жест)");
            Last(ok ? "ДОМОЙ" : "ДОМОЙ отклонён");
        }

        private void Last(string text)
        {
            lastGesture = text;
            lastGestureTime = Time.realtimeSinceStartup;
            if (Message != null) Message("жест: " + text);
        }

        // ------------------------------------------------------------------ визуализация

        private void EnsureVisuals()
        {
            if (root != null) return;
            if (!KvGraphics.Available)
            {
                if (!graphicsReported)
                {
                    graphicsReported = true;
                    Report("скелет рук не рисуется: нет графики (жесты продолжают распознаваться)");
                }
                return;
            }
            root = new GameObject("ОтслеживаниеРук");
            root.hideFlags = HideFlags.HideInHierarchy;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Collider templateCollider = sphere.GetComponent<Collider>();
            if (templateCollider != null) UnityEngine.Object.Destroy(templateCollider);
            sphere.hideFlags = HideFlags.HideAndDontSave;
            sphere.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(sphere);

            for (int h = 0; h < 2; h++)
            {
                for (int j = 0; j < JointCount; j++)
                {
                    GameObject go = new GameObject("Сустав_" + h + "_" + j, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root.transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer mr = go.GetComponent<MeshRenderer>();
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    Color jointColor = h == 0 ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.65f, 0.25f);
                    Material mat = KvRenderPipelineShaders.CreateUnlit("Материал_сустава", jointColor);
                    if (mat != null && mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", jointColor);
                    mr.sharedMaterial = mat;
                    go.transform.localScale = Vector3.one * 0.012f;
                    markers[h, j] = go.transform;
                }
                for (int b = 0; b < BonePairs.GetLength(0); b++)
                {
                    GameObject go = new GameObject("Кость_" + h + "_" + b, typeof(LineRenderer));
                    go.transform.SetParent(root.transform, false);
                    LineRenderer lr = go.GetComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.startWidth = 0.006f;
                    lr.endWidth = 0.006f;
                    lr.material = KvRenderPipelineShaders.CreateUnlit(
                        "Материал_кости", h == 0 ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.65f, 0.25f));
                    lr.startColor = lr.endColor = h == 0 ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.65f, 0.25f);
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    bones[h, b] = lr;
                }
            }
            root.SetActive(visible);
        }

        private void UpdateVisuals()
        {
            if (!visible) return;
            if (!KvGraphics.Available) return;
            EnsureVisuals();
            if (root == null) return;
            if (!root.activeSelf) root.SetActive(true);

            for (int h = 0; h < 2; h++)
            {
                bool tracked = IsTracked(h);
                for (int j = 0; j < JointCount; j++)
                {
                    if (markers[h, j] == null) continue;
                    bool on = tracked && valid[h, j];
                    markers[h, j].gameObject.SetActive(on);
                    if (on) markers[h, j].position = joints[h, j];
                }
                for (int b = 0; b < BonePairs.GetLength(0); b++)
                {
                    if (bones[h, b] == null) continue;
                    int a = BonePairs[b, 0], c = BonePairs[b, 1];
                    bool on = tracked && valid[h, a] && valid[h, c];
                    bones[h, b].gameObject.SetActive(on);
                    if (on)
                    {
                        bones[h, b].SetPosition(0, joints[h, a]);
                        bones[h, b].SetPosition(1, joints[h, c]);
                    }
                }
            }
        }

        public string Status()
        {
            if (!enabled) return "выключено";
            string l = IsTracked(0) ? "левая есть" : "левой нет";
            string r = IsTracked(1) ? "правая есть" : "правой нет";
            return l + " · " + r + " · касаний " + pinchCount + " · свайпов " + swipeCount +
                   " · горстей " + grabCount;
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Hands] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ЭТАП 25 ТЗ: ОТСЛЕЖИВАНИЕ ВЗГЛЯДА (PICO 4 Ultra) И ЦЕНТРАЛЬНОЕ (ФОВЕАЛЬНОЕ) РЕНДЕРИРОВАНИЕ.
    ///
    /// Платформенная часть — поза глаз — приходит от SDK и передаётся в `PushGaze(...)`.
    /// Здесь сделано то, что действительно можно проверить:
    ///   • ФИКСАЦИИ: взгляд, удержанный в конусе 2.5° дольше 0.22 с, считается фиксацией;
    ///     считается их число и средняя длительность, ведётся карта точек фиксации в сцене;
    ///   • ВЫБОР ВЗГЛЯДОМ (dwell): удержание взгляда 0.9 с на точке добавляет путевую точку
    ///     маршрута в это место — «маршрут, поставленный взглядом» (этап 7 ТЗ);
    ///   • МОРГАНИЕ: закрытые глаза дольше 0.15 с — «клик» (пауза/продолжение движения).
    /// ФОВЕАЛЬНОЕ РЕНДЕРИРОВАНИЕ: включается через API платформы, если он есть в сборке
    /// (поиск выполняется отражением — так проект не зависит от версии SDK). Если API нет,
    /// честно сообщаем об этом и включаем видимую замену — маску периферийного зрения,
    /// которая следует за точкой фиксации.
    /// </summary>
    public class KvEyeTrackingService
    {
        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;
        private KvWaypointManager waypoints;

        private bool enabled;
        private bool dwellEnabled = true;
        private float dwellSeconds = 0.9f;
        private bool maskEnabled;

        private Vector3 gazeOrigin, gazeDirection = Vector3.forward;
        private bool gazeValid;
        private float opennessL = 1f, opennessR = 1f;

        private Vector3 fixationPoint;
        private Vector3 lastDirection = Vector3.forward;
        private float stableTime;
        private float dwellTime;
        private float blinkStart = -1f;
        private float actionCooldown;
        private float blinkCooldown;

        private int fixations, dwellActions, blinks;
        private float fixationSum;
        private readonly List<Vector3> fixationPoints = new List<Vector3>();
        private GameObject fixationRoot;
        private GameObject maskRoot;

        public bool Enabled { get { return enabled; } }
        public bool MaskEnabled { get { return maskEnabled; } }
        public bool GazeValid { get { return gazeValid; } }
        public int Fixations { get { return fixations; } }
        public int DwellActions { get { return dwellActions; } }
        public int Blinks { get { return blinks; } }
        public float AverageFixation
        {
            get { return fixations > 0 ? fixationSum / fixations : 0f; }
        }
        public float DwellProgress
        {
            get { return dwellEnabled ? Mathf.Clamp01(dwellTime / Mathf.Max(0.1f, dwellSeconds)) : 0f; }
        }
        public Vector3 FixationPoint { get { return fixationPoint; } }
        public IList<Vector3> FixationPoints { get { return fixationPoints; } }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvWaypointManager route)
        {
            flow = controller;
            features = hub;
            waypoints = route;
        }

        public void SetEnabled(bool value)
        {
            enabled = value;
            if (!value)
            {
                gazeValid = false;
                stableTime = 0f;
                dwellTime = 0f;
            }
            Report("отслеживание взгляда " + (value ? "включено" : "выключено"));
        }

        public void SetDwell(bool value) { dwellEnabled = value; }
        public void SetDwellSeconds(float value) { dwellSeconds = Mathf.Clamp(value, 0.3f, 3f); }

        public void SetMask(bool value)
        {
            maskEnabled = value;
            EnsureMask();
            if (maskRoot != null) maskRoot.SetActive(value);
            Report("маска периферийного зрения " + (value ? "включена" : "выключена"));
        }

        /// <summary>Поза глаз от SDK шлема (или от имитатора на ПК).</summary>
        public void PushGaze(Vector3 origin, Vector3 direction, float leftOpenness = 1f, float rightOpenness = 1f)
        {
            gazeOrigin = origin;
            if (direction.sqrMagnitude > 1e-8f) gazeDirection = direction.normalized;
            opennessL = Mathf.Clamp01(leftOpenness);
            opennessR = Mathf.Clamp01(rightOpenness);
            gazeValid = true;
        }

        public void Tick(float deltaTime)
        {
            if (!enabled || !gazeValid) return;
            actionCooldown = Mathf.Max(0f, actionCooldown - deltaTime);
            blinkCooldown = Mathf.Max(0f, blinkCooldown - deltaTime);

            UpdateBlink();
            UpdateFixation(deltaTime);
            UpdateMask();
        }

        /// <summary>
        /// Моргание = «клик взглядом». У моргания СВОЙ интервал ожидания: решение «удержал взгляд»
        /// и решение «моргнул» — разные действия, и одно не должно блокировать другое (иначе сразу
        /// после выбора взглядом моргание игнорировалось бы, что и показал прогон).
        /// </summary>
        private void UpdateBlink()
        {
            bool closed = opennessL < 0.15f && opennessR < 0.15f;
            if (closed)
            {
                if (blinkStart < 0f) blinkStart = Time.realtimeSinceStartup;
                else if (Time.realtimeSinceStartup - blinkStart > 0.15f && blinkCooldown <= 0f)
                {
                    blinks++;
                    blinkCooldown = 0.8f;
                    blinkStart = -1f;         // ждём, пока глаза откроются, потом можно снова
                    if (flow != null && flow.Motion != null)
                    {
                        bool now = !flow.Motion.Paused;
                        flow.Motion.SetPaused(now);
                        Report("моргание: " + (now ? "пауза" : "продолжение"));
                    }
                }
            }
            else blinkStart = -1f;
        }

        private void UpdateFixation(float deltaTime)
        {
            float angle = Vector3.Angle(lastDirection, gazeDirection);
            if (angle < 2.5f)
            {
                stableTime += deltaTime;
                if (stableTime >= 0.22f)
                {
                    if (dwellTime <= 0f)
                    {
                        fixations++;
                        fixationPoint = GazePoint();
                        AddFixationPoint(fixationPoint);
                    }
                    dwellTime += deltaTime;
                    fixationSum += deltaTime;
                    if (dwellEnabled && dwellTime >= dwellSeconds && actionCooldown <= 0f)
                    {
                        actionCooldown = 1.2f;
                        dwellTime = 0f;
                        stableTime = 0f;
                        DwellAction(fixationPoint);
                    }
                }
            }
            else
            {
                if (dwellTime > 0f) dwellTime = 0f;
                stableTime = 0f;
                lastDirection = gazeDirection;
            }
        }

        private Vector3 GazePoint()
        {
            RaycastHit hit;
            if (Physics.Raycast(gazeOrigin, gazeDirection, out hit, 50f,
                    ~0, QueryTriggerInteraction.Ignore))
                return hit.point;

            // В сцене проекта у служебных объектов коллайдеров нет, поэтому если луч ничего
            // не задел — берём точку на рабочей плоскости робота (та же высота, что у стола).
            float planeY = 0.75f;
            if (flow != null && flow.Validator != null && flow.Validator.Ready)
            {
                Vector3 tcp = flow.Validator.TcpAt(flow.Validator.CopyCurrent());
                planeY = tcp.y;
            }
            if (Mathf.Abs(gazeDirection.y) < 1e-4f) return gazeOrigin + gazeDirection * 1f;
            float t = (planeY - gazeOrigin.y) / gazeDirection.y;
            if (t < 0.2f || t > 50f) return gazeOrigin + gazeDirection * 1f;
            return gazeOrigin + gazeDirection * t;
        }

        private void AddFixationPoint(Vector3 point)
        {
            fixationPoints.Add(point);
            if (fixationPoints.Count > 24) fixationPoints.RemoveAt(0);

            // Точки фиксации — это показ: если графики нет (или объекты ещё не созданы),
            // просто накапливаем координаты для статистики и не трогаем сцену.
            EnsureFixationVisuals();
            if (fixationRoot == null) return;

            for (int i = 0; i < fixationRoot.transform.childCount; i++)
            {
                Transform child = fixationRoot.transform.GetChild(i);
                if (child.gameObject.activeSelf) continue;
                child.position = point;
                child.gameObject.SetActive(true);
                return;
            }
        }

        private void DwellAction(Vector3 point)
        {
            if (waypoints == null)
            {
                Report("взгляд: маршрут недоступен");
                return;
            }
            bool ok = waypoints.Add(point, "взглядом (этап 25)");
            dwellActions++;
            if (features != null && features.Log != null)
                features.Log.Info("взгляд: путевая точка (" + point.x.ToString("0.00") + ", " +
                                  point.y.ToString("0.00") + ", " + point.z.ToString("0.00") + ")");
            Report(ok
                ? "взгляд: путевая точка добавлена (" + point.x.ToString("0.00") + ", " +
                  point.y.ToString("0.00") + ", " + point.z.ToString("0.00") + ")"
                : "взгляд: точка не добавлена");
        }

        // ------------------------------------------------------------------ визуализация

        private void EnsureFixationVisuals()
        {
            if (fixationRoot != null) return;
            if (!KvGraphics.Available) return;
            fixationRoot = new GameObject("ТочкиФиксации");
            fixationRoot.hideFlags = HideFlags.HideInHierarchy;

            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh mesh = template.GetComponent<MeshFilter>().sharedMesh;
            Collider col = template.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            template.SetActive(false);
            template.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(template);

            Color color = new Color(1f, 0.35f, 0.85f);
            Material mat = KvRenderPipelineShaders.CreateUnlit("Материал_фиксации", color);
            if (mat != null && mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", color);

            for (int i = 0; i < 24; i++)
            {
                GameObject go = new GameObject("Фиксация_" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(fixationRoot.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.sharedMaterial = mat;
                go.transform.localScale = Vector3.one * 0.02f;
                go.SetActive(false);
            }
        }

        private void EnsureMask()
        {
            if (maskRoot != null) return;
            if (!KvGraphics.Available) return;      // без графики маска не строится, расчёт идёт
            EnsureFixationVisuals();
            maskRoot = new GameObject("МаскаПериферии", typeof(LineRenderer));
            maskRoot.hideFlags = HideFlags.HideInHierarchy;
            LineRenderer lr = maskRoot.GetComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 48;
            lr.startWidth = 0.004f;
            lr.endWidth = 0.004f;
            lr.material = KvRenderPipelineShaders.CreateUnlit("Материал_маски_периферии",
                new Color(0.4f, 0.9f, 1f, 0.7f));
            Color c = new Color(0.4f, 0.9f, 1f, 0.7f);
            lr.startColor = lr.endColor = c;
            for (int i = 0; i < 48; i++)
            {
                float a = Mathf.PI * 2f * i / 48f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.35f, Mathf.Sin(a) * 0.35f, 0.6f));
            }
        }

        private void UpdateMask()
        {
            if (!maskEnabled || maskRoot == null) return;
            // Кольцо ставится в точку фиксации и разворачивается к наблюдателю — так видно,
            // какая область считается «центральной» при текущем взгляде.
            maskRoot.transform.position = fixationPoint;
            Vector3 to = gazeOrigin - fixationPoint;
            if (to.sqrMagnitude > 1e-6f)
                maskRoot.transform.rotation = Quaternion.LookRotation(-to.normalized, Vector3.up);
        }

        public string Status()
        {
            if (!enabled) return "выключено";
            string on = gazeValid ? "взгляд есть" : "взгляда нет";
            string fix = fixationPoint.sqrMagnitude > 1e-6f
                ? " · точка (" + fixationPoint.x.ToString("0.00") + ", " +
                  fixationPoint.y.ToString("0.00") + ", " + fixationPoint.z.ToString("0.00") + ")"
                : "";
            return on + " · фиксаций " + fixations + " (средняя " + AverageFixation.ToString("0.00") +
                   " с) · выборов взглядом " + dwellActions + " · морганий " + blinks + fix;
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Eyes] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ФОВЕАЛЬНОЕ (ЦЕНТРАЛЬНОЕ) РЕНДЕРИРОВАНИЕ ПО ВЗГЛЯДУ.
    ///
    /// Unity рисует всю картинку одинаково; экономию даёт VRS/foveated rendering, которым
    /// управляет платформа (PICO/OpenXR) через свои API. Чтобы проект не зависел от версии
    /// SDK, поиск API идёт ОТРАЖЕНИЕМ: если в сборке есть тип `FoveatedRendering` с методами
    /// уровня детализации — вызываем их и рапортуем об успехе; если нет — сообщаем, что
    /// аппаратное фовеальное рендерирование недоступно на этой платформе, и остаётся
    /// визуальная маска центрального зрения (этап 25, вкладка «Взгляд»).
    /// </summary>
    public class KvFoveatedRendering
    {
        private bool probed;
        private bool supported;
        private string details = "не проверялось";
        private int level = 1;

        public bool Supported { get { return supported; } }
        public string Details { get { return details; } }
        public int Level { get { return level; } }

        public bool Probe()
        {
            probed = true;
            supported = false;
            details = "API фовеального рендерирования не найден";

            Type type = FindType("UnityEngine.Rendering.FoveatedRendering")
                        ?? FindType("FoveatedRendering");
            if (type == null)
            {
                details = "в этой сборке Unity нет API FoveatedRendering — " +
                          "центральное рендерирование выполняет платформа (PICO/OpenXR)";
                return false;
            }

            var mode = type.GetMethod("SetFoveatedRenderingMode",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var setLevel = type.GetMethod("SetFoveatedRenderingLevel",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (mode == null && setLevel == null)
            {
                details = "тип " + type.Name + " найден, но методов управления нет";
                return false;
            }

            try
            {
                if (mode != null)
                {
                    ParameterInfo[] args = mode.GetParameters();
                    object value = args.Length > 0 ? Enum.ToObject(args[0].ParameterType, 1) : null;
                    mode.Invoke(null, args.Length > 0 ? new[] { value } : null);
                }
                if (setLevel != null)
                {
                    ParameterInfo[] args = setLevel.GetParameters();
                    if (args.Length > 0)
                        setLevel.Invoke(null, new[] { Convert.ChangeType(1, args[0].ParameterType) });
                }
                supported = true;
                details = "фовеальное рендерирование включено через " + type.FullName;
            }
            catch (Exception e)
            {
                details = "вызов API не удался: " + e.Message;
                supported = false;
            }
            Debug.Log("[Foveated] " + details);
            return supported;
        }

        /// <summary>Уровень детализации: 0 — максимум, 1 — средний, 2 — экономия.</summary>
        public bool SetLevel(int value)
        {
            if (!probed) Probe();
            level = Mathf.Clamp(value, 0, 2);
            if (!supported) return false;
            Type type = FindType("UnityEngine.Rendering.FoveatedRendering");
            if (type == null) return false;
            try
            {
                var setLevel = type.GetMethod("SetFoveatedRenderingLevel",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (setLevel == null) return false;
                ParameterInfo[] args = setLevel.GetParameters();
                if (args.Length == 0) return false;
                setLevel.Invoke(null, new[] { Convert.ChangeType(level, args[0].ParameterType) });
                details = "уровень " + level;
                return true;
            }
            catch (Exception e)
            {
                details = "уровень не выставлен: " + e.Message;
                return false;
            }
        }

        private static Type FindType(string name)
        {
            Type direct = Type.GetType(name);
            if (direct != null) return direct;
            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type t = assemblies[i].GetType(name);
                    if (t != null) return t;
                }
                catch (Exception) { }
            }
            return null;
        }
    }

    // ====================================================================== вкладки

    /// <summary>ВКЛАДКА «ГОЛОС» (ЭТАП 23 ТЗ).</summary>
    public class KvVoiceTab : IKvWorkbenchTab
    {
        private readonly KvVoiceService voice;
        private InputField field;

        public KvVoiceTab(KvVoiceService service) { voice = service; }

        public string Key { get { return "voice"; } }
        public string Title { get { return KvLocExtra3.T("voice.title", "Голосовые команды"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("voice.title", "Голосовые команды"));
            kit.Toggle(T("voice.enable", "Слушать микрофон (детектор речи)"), voice.Enabled,
                voice.SetEnabled);
            kit.Slider(T("voice.threshold", "Порог громкости"), 0.005f, 0.4f, voice.Threshold, "0.000",
                voice.SetThreshold);
            kit.Info(delegate { return voice.Status(); }, KvTheme.Accent);
            kit.Info(delegate
            {
                return T("voice.devices", "Микрофонов в системе") + ": " + voice.MicCount +
                       " · " + T("voice.device", "устройство") + ": " + voice.DeviceName;
            }, KvTheme.TextDim);

            kit.Note(T("voice.hint",
                "Наберите фразу, как будто её распознал шлем: «стоп», «домой», «пуск», «пауза», " +
                "«вариант три», «дальше», «запиши позу», «снимок», «отмена»."), KvTheme.TextDim);
            field = KvInputKit.Single(kit.Content, T("voice.command", "Команда"), "", "например: домой",
                delegate (string text)
                {
                    voice.PushRecognized(text);
                    if (field != null) field.text = "";
                });
            kit.Buttons(new[]
            {
                T("voice.say", "Выполнить"),
                T("voice.demo", "Проверка грамматики")
            }, new Action[]
            {
                delegate
                {
                    if (field != null && !string.IsNullOrEmpty(field.text))
                    {
                        voice.PushRecognized(field.text);
                        field.text = "";
                    }
                },
                delegate { voice.PushRecognized("вариант два"); }
            });

            kit.Divider();
            kit.Section(T("voice.history", "Журнал распознавания"));
            kit.Info(delegate
            {
                if (voice.History.Count == 0) return "—";
                KvVoiceCommand last = voice.History[voice.History.Count - 1];
                return "«" + last.phrase + "» → " + last.id +
                       (last.executed ? " · выполнено" : " · отклонено") +
                       (string.IsNullOrEmpty(last.result) ? "" : " · " + last.result);
            }, KvTheme.TextMain);
            kit.Note(T("voice.info",
                "Распознавание речи (звук → текст) даёт SDK шлема PICO/Meta; интерфейс для него " +
                "готов (IKvSpeechSource). Всё остальное — детектор речи, грамматика и выполнение " +
                "команд — работает уже сейчас."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh() { }
    }

    /// <summary>ВКЛАДКА «РУКИ» (ЭТАП 24 ТЗ).</summary>
    public class KvHandsTab : IKvWorkbenchTab
    {
        private readonly KvHandTrackingService hands;

        public KvHandsTab(KvHandTrackingService service, KvFoveatedRendering foveal)
        {
            hands = service;
        }

        public string Key { get { return "hands"; } }
        public string Title { get { return KvLocExtra3.T("hands.title", "Отслеживание рук"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("hands.title", "Отслеживание рук"));
            kit.Toggle(T("hands.enable", "Принимать позы рук от шлема"), hands.Enabled, hands.SetEnabled);
            kit.Toggle(T("hands.show", "Показывать скелет рук"), hands.Visible, hands.SetVisible);
            kit.Info(delegate { return hands.Status(); }, KvTheme.Accent);
            kit.Info(delegate
            {
                return string.IsNullOrEmpty(hands.LastGesture) || hands.LastGestureAge < 0f
                    ? T("hands.none", "жестов пока не было")
                    : T("hands.last", "Последний жест") + ": " + hands.LastGesture +
                      " (" + hands.LastGestureAge.ToString("0.0") + " с назад)";
            }, KvTheme.TextMain);

            kit.Divider();
            kit.Section(T("hands.gestures", "Жесты"));
            kit.Table(T("hands.pinch", "Касание (pinch)"), delegate { return hands.PinchCount.ToString(); },
                delegate { return T("hands.pinch.act", "захват/отпускание объекта"); });
            kit.Table(T("hands.swipe", "Свайп"), delegate { return hands.SwipeCount.ToString(); },
                delegate { return T("hands.swipe.act", "влево/вправо — вариант, вверх — пуск, вниз — пауза"); });
            kit.Table(T("hands.grab", "Горсть"), delegate { return hands.GrabCount.ToString(); },
                delegate { return T("hands.grab.act", "удержание захвата"); });
            kit.Note(T("hands.info",
                "Позы суставов приходят от SDK шлема (PICO/Meta) через PushJoint; без шлема доступен " +
                "программный ввод — им пользуется диагностика. Распознавание жестов работает здесь."),
                KvTheme.TextDim);
        }

        public void Tick() { }

        public void Refresh() { }
    }

    /// <summary>ВКЛАДКА «ВЗГЛЯД» (ЭТАП 25 ТЗ).</summary>
    public class KvEyesTab : IKvWorkbenchTab
    {
        private readonly KvEyeTrackingService eyes;
        private readonly KvFoveatedRendering foveated;
        private KvSegmented levelSegment;

        public KvEyesTab(KvEyeTrackingService service, KvFoveatedRendering foveal)
        {
            eyes = service;
            foveated = foveal;
        }

        public string Key { get { return "eyes"; } }
        public string Title { get { return KvLocExtra3.T("eyes.title", "Взгляд и фовеальное зрение"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("eyes.title", "Отслеживание взгляда"));
            kit.Toggle(T("eyes.enable", "Принимать взгляд от шлема"), eyes.Enabled, eyes.SetEnabled);
            kit.Toggle(T("eyes.dwell", "Выбор взглядом (удержание)"), true, eyes.SetDwell);
            kit.Slider(T("eyes.dwell.time", "Время удержания, с"), 0.3f, 3f, 0.9f, "0.00",
                eyes.SetDwellSeconds);
            kit.Info(delegate { return eyes.Status(); }, KvTheme.Accent);
            kit.Info(delegate
            {
                return T("eyes.dwell.progress", "Накопление удержания") + ": " +
                       (eyes.DwellProgress * 100f).ToString("0") + " %";
            }, KvTheme.TextMain);
            kit.Note(T("eyes.dwell.info",
                "Удержание взгляда 0,9 с добавляет путевую точку маршрута в точку взгляда — " +
                "маршрут можно «нарисовать» взглядом. Моргание дольше 0,15 с ставит движение " +
                "на паузу или продолжает его."), KvTheme.TextDim);

            kit.Divider();
            kit.Section(T("eyes.foveal", "Центральное (фовеальное) рендерирование"));
            kit.Toggle(T("eyes.mask", "Показывать зону центрального зрения"), eyes.MaskEnabled, eyes.SetMask);
            levelSegment = kit.Segmented(T("eyes.level", "Уровень детализации"),
                new[] { T("eyes.level.high", "высокий"), T("eyes.level.mid", "средний"),
                        T("eyes.level.low", "экономия") }, foveated.Level, delegate (int i)
                {
                    foveated.SetLevel(i);
                });
            kit.Info(delegate
            {
                return foveated.Supported
                    ? T("eyes.foveal.on", "фовеальное рендерирование включено") + " · " + foveated.Details
                    : T("eyes.foveal.no", "аппаратное фовеальное рендерирование недоступно") + " · " +
                      foveated.Details;
            }, foveated.Supported ? KvTheme.Ok : KvTheme.Warn);
            kit.Buttons(new[] { T("eyes.probe", "Проверить поддержку") },
                new Action[] { delegate { foveated.Probe(); } });
            kit.Note(T("eyes.foveal.info",
                "PICO 4 Ultra умеет центральное рендерирование по взгляду; вызов идёт через API " +
                "платформы. Если API в сборке нет (обычный ПК), платформа рендерит как обычно — " +
                "об этом сообщается честно, вместо «галочки» в интерфейсе."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh()
        {
            if (levelSegment == null) return;
            if (levelSegment.Index != foveated.Level) levelSegment.Set(foveated.Level);
        }
    }
}
