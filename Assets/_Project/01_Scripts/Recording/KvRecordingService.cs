using System;
using System.Collections.Generic;
using UnityEngine;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЗАПИСЬ И ВОСПРОИЗВЕДЕНИЕ ТРАЕКТОРИИ (ЭТАП 1 ТЗ).
    ///
    /// Запись: с заданной частотой (`rate`, по умолчанию 20 Гц) снимаются углы суставов
    /// робота потока, время от начала записи и TCP-координаты. Источник — реальный робот
    /// (позы применяет поток/исполнитель) ИЛИ фантом (его текущая поза в полёте).
    ///
    /// Воспроизведение: собственный покадровый проигрыватель по записанным временам
    /// (ровно то же, что делает <c>TrajectoryExecutor</c> для планов: интерполяция q(t) и
    /// <c>PoseValidator.Apply</c>), поэтому доступны пауза, множитель скорости и точный
    /// остаток времени. Параллельно проверяются лимиты суставов и (опционально) зазор —
    /// при нарушении воспроизведение останавливается, как «аварийный» случай.
    ///
    /// Связь с потоком этапов: воспроизведение НЕ трогает State Machine и не запускается,
    /// пока робот едет по траектории этапа 4 (<c>flow.Motion.IsRunning</c>); если поток
    /// начинает движение во время воспроизведения — воспроизведение останавливается.
    /// </summary>
    public class KvRecordingService
    {
        /// <summary>Частота сэмплирования при записи, Гц.</summary>
        public float rate = 20f;
        /// <summary>Проверять лимиты суставов на каждом шаге воспроизведения.</summary>
        public bool checkLimits = true;
        /// <summary>Проверять зазор до сцены (дороже; раз в `clearanceInterval` секунд).</summary>
        public bool checkClearance = true;
        public float clearanceInterval = 0.25f;
        public float minClearance = 0.01f;

        public event Action<KvTrajectoryRecord> RecordingStarted;
        public event Action<KvTrajectoryRecord> RecordingFinished;   // запись сохранена (или null)
        public event Action<KvTrajectoryRecord> PlaybackStarted;
        public event Action<KvTrajectoryRecord> PlaybackFinished;    // по концу ИЛИ остановке
        public event Action<string> Failed;                          // причина отказа (для журнала)

        private TrajectoryFlowController flow;
        private CollisionWorld world;

        private KvTrajectoryRecord recording;
        private float recordSampleTimer;
        private float recordClock;

        private KvTrajectoryRecord playing;
        private double playhead;
        private bool playPaused;
        private float playSpeed = 1f;
        private float clearanceTimer;
        private int playSampleIndex;

        /// <summary>Идёт запись.</summary>
        public bool IsRecording { get { return recording != null; } }
        /// <summary>Идёт воспроизведение.</summary>
        public bool IsPlaying { get { return playing != null; } }
        public bool Paused { get { return playPaused; } }

        /// <summary>Записываемая (ещё не сохранённая) запись.</summary>
        public KvTrajectoryRecord Recording { get { return recording; } }
        /// <summary>Воспроизводимая запись.</summary>
        public KvTrajectoryRecord Playing { get { return playing; } }

        /// <summary>Множитель скорости воспроизведения (0.1…8; 1 — как записано).</summary>
        public float SpeedMultiplier
        {
            get { return playSpeed; }
            set { playSpeed = Mathf.Clamp(value, 0.1f, 8f); }
        }

        public void Bind(TrajectoryFlowController controller)
        {
            flow = controller;
        }

        /// <summary>Мир столкновений (для проверки зазора; владелец — хаб функций).</summary>
        public void BindWorld(CollisionWorld collisionWorld)
        {
            world = collisionWorld;
        }

        // ------------------------------------------------------------------ запись

        /// <summary>Начать запись. Источник: live (робот) / phantom / manual.</summary>
        public bool StartRecording(string name, string source = "live")
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Fail("запись невозможна: робот потока не готов");
                return false;
            }
            if (IsPlaying) StopPlayback("начата запись");

            recording = new KvTrajectoryRecord
            {
                id = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrEmpty(name) ? "Запись " + FeatureStorage.TimeStamp() : name,
                robot = flow.Validator.RobotName,
                created = FeatureStorage.IsoNow(),
                source = source,
                rate = Mathf.Max(1f, rate)
            };
            recordSampleTimer = 0f;
            recordClock = 0f;

            SampleNow(true);
            Debug.Log("[Record] запись начата · «" + recording.name + "» · робот " + recording.robot +
                      " · источник " + recording.SourceLabel + " · " + recording.rate.ToString("0") + " Гц");
            if (RecordingStarted != null) RecordingStarted(recording);
            return true;
        }

        /// <summary>Остановить запись; сохранить в файл (save=true) и вернуть запись.</summary>
        public KvTrajectoryRecord StopRecording(bool save = true)
        {
            if (recording == null) return null;
            KvTrajectoryRecord done = recording;
            recording = null;

            done.Normalize();
            done.duration = done.SampleCount > 0 ? done.samples[done.SampleCount - 1].t : 0f;
            done.length = done.ComputeLength();

            if (done.SampleCount < 2)
            {
                Fail("запись слишком короткая (сэмплов " + done.SampleCount + ") — не сохранена");
                if (RecordingFinished != null) RecordingFinished(null);
                return null;
            }

            if (save)
            {
                string path = KvRecordStore.Save(done);
                if (string.IsNullOrEmpty(path))
                {
                    Fail("не удалось сохранить запись «" + done.name + "»");
                    if (RecordingFinished != null) RecordingFinished(null);
                    return null;
                }
                Debug.Log("[Record] запись сохранена: " + path + " · сэмплов " + done.SampleCount +
                          " · " + done.duration.ToString("0.00") + " с · путь " +
                          done.length.ToString("0.000") + " м");
            }
            if (RecordingFinished != null) RecordingFinished(done);
            return done;
        }

        /// <summary>Записать один сэмпл вручную (используется также при ручном ведении суставов).</summary>
        public bool SampleNow(bool force = false)
        {
            if (recording == null) return false;
            if (!force && recordSampleTimer > 0f) return false;

            double[] q = SourcePose();
            if (q == null) return false;

            Vector3 tcpPoint = flow.Validator.TcpAt(q);
            Transform tcpTransform = flow.Robot != null ? flow.Robot.tcp : null;

            KvRecordSample s = new KvRecordSample
            {
                t = recordClock,
                q = ToFloats(q),
                tcp = new[] { tcpPoint.x, tcpPoint.y, tcpPoint.z }
            };
            if (tcpTransform != null)
            {
                Quaternion r = tcpTransform.rotation;
                s.rot = new[] { r.x, r.y, r.z, r.w };
            }

            recording.samples.Add(s);
            recordSampleTimer = 1f / Mathf.Max(1f, recording.rate);
            return true;
        }

        private double[] SourcePose()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return null;

            if (recording != null && recording.source == "phantom" &&
                flow.Phantoms != null && flow.Phantoms.Count > 0)
            {
                double[] pose = flow.Phantoms.PoseOf(0);
                if (pose != null) return pose;
            }
            return flow.Validator.CopyCurrent();
        }

        private static float[] ToFloats(double[] q)
        {
            if (q == null) return new float[0];
            float[] result = new float[q.Length];
            for (int i = 0; i < q.Length; i++) result[i] = (float)q[i];
            return result;
        }

        // ------------------------------------------------------------------ воспроизведение

        /// <summary>Проиграть запись (с множителем скорости).</summary>
        public bool Play(KvTrajectoryRecord rec, float speedMultiplier = -1f)
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Fail("воспроизведение невозможно: робот потока не готов");
                return false;
            }
            if (rec == null || rec.SampleCount < 2)
            {
                Fail("в записи меньше двух сэмплов — воспроизводить нечего");
                return false;
            }
            if (flow.Motion != null && flow.Motion.IsRunning)
            {
                Fail("робот едет по траектории этапа 4 — дождитесь остановки или нажмите «Стоп»");
                return false;
            }
            if (rec.Dof > 0 && flow.Validator.Dof > 0 && rec.Dof != flow.Validator.Dof)
            {
                Fail("запись сделана для робота с " + rec.Dof + " осями, а активный робот — с " +
                     flow.Validator.Dof + " («" + flow.Validator.RobotName + "»)");
                return false;
            }
            if (!string.IsNullOrEmpty(rec.robot) && !string.IsNullOrEmpty(flow.Validator.RobotName) &&
                rec.robot != flow.Validator.RobotName)
            {
                Fail("запись сделана на роботе «" + rec.robot + "», активный — «" +
                     flow.Validator.RobotName + "» (выберите того же робота)");
                return false;
            }
            if (IsRecording) StopRecording(true);

            // ПРОВЕРКА БЕЗОПАСНОСТИ ПЕРЕД ВОСПРОИЗВЕДЕНИЕМ (30.10.2026, аудит S43).
            //
            // ЧТО БЫЛО. Воспроизведение записи — единственный маршрут движения, который ОБХОДИЛ
            // `SafetyGate` (шаг 4) и `MotionExecutor` (шаг 5): позы шли напрямую в
            // `flow.Validator.Apply(q)`. Ограничения суставов и зазор проверялись ПОСТФАКТУМ, раз
            // в `clearanceInterval`, а предполётной проверки не было вовсе.
            //
            // ЧЕМ ЭТО ПЛОХО. Запись сделана раньше, и с тех пор могли поменять робота, рабочую
            // зону или стол. Воспроизведение «вслепую» везёт робота туда, куда сейчас уже нельзя.
            //
            // ЧТО СДЕЛАНО. Перед стартом проверяются КАЖДЫЙ сэмпл записи на лимиты суставов и
            // зазор до мира (если есть). Отказ — честный, с числом: оператор видит, на каком
            // сэмпле и сколько миллиметров. Это не полная замена проверкам в темпе (они остались
            // в `Tick`), а гарантия «мы не начнём движение, которое заведомо незаконно».
            if (checkLimits || (checkClearance && world != null))
            {
                for (int i = 0; i < rec.SampleCount; i++)
                {
                    // Сэмпл записи хранит позу как float[] — приводим к double[] ОДИН раз здесь:
                    // дальше `WithinLimits` и `ClearanceAt` принимают double[] (как и весь
                    // планировщик), и приведение в двух местах разошлось бы.
                    float[] raw = rec.samples[i].q;
                    if (raw == null) continue;
                    var q = new double[raw.Length];
                    for (int k = 0; k < raw.Length; k++) q[k] = raw[k];

                    if (checkLimits && !flow.Validator.WithinLimits(q))
                    {
                        Fail("воспроизведение отменено: сэмпл " + (i + 1) + " из " + rec.SampleCount +
                             " вне лимитов суставов (запись сделана при других настройках робота)");
                        return false;
                    }

                    if (checkClearance && world != null)
                    {
                        Vector3 tcpPoint;
                        Vector3[] nodes;
                        float clearance = flow.Validator.ClearanceAt(q, world, out tcpPoint, out nodes);
                        if (clearance < minClearance)
                        {
                            Fail("воспроизведение отменено: сэмпл " + (i + 1) + " из " + rec.SampleCount +
                                 " проходит в " + (clearance * 1000f).ToString("0") + " мм от сцены (нужно " +
                                 (minClearance * 1000f).ToString("0") + " мм) — стол или зона изменились");
                            return false;
                        }
                    }
                }
            }

            playing = rec;
            if (speedMultiplier > 0f) SpeedMultiplier = speedMultiplier;
            playhead = 0.0;
            playSampleIndex = 0;
            playPaused = false;
            clearanceTimer = 0f;

            Debug.Log("[Record] воспроизведение «" + rec.name + "» · сэмплов " + rec.SampleCount +
                      " · " + rec.duration.ToString("0.00") + " с · скорость ×" +
                      playSpeed.ToString("0.00"));
            if (PlaybackStarted != null) PlaybackStarted(rec);
            return true;
        }

        /// <summary>Пауза/продолжение воспроизведения.</summary>
        public bool TogglePause()
        {
            if (!IsPlaying) return false;
            playPaused = !playPaused;
            Debug.Log("[Record] воспроизведение " + (playPaused ? "на паузе" : "продолжено") +
                      " · позиция " + (Progress01 * 100f).ToString("0") + " %");
            return true;
        }

        public void SetPaused(bool value)
        {
            if (IsPlaying) playPaused = value;
        }

        /// <summary>Остановить воспроизведение (штатная остановка оператора).</summary>
        public void StopPlayback(string why = "остановлено оператором")
        {
            if (playing == null) return;
            KvTrajectoryRecord done = playing;
            playing = null;
            playPaused = false;
            Debug.Log("[Record] воспроизведение «" + done.name + "» " + why);
            if (PlaybackFinished != null) PlaybackFinished(done);
        }

        /// <summary>Прогресс воспроизведения 0…1.</summary>
        public float Progress01
        {
            get
            {
                if (playing == null || playing.duration <= 0.0001f) return 0f;
                return Mathf.Clamp01((float)(playhead / playing.duration));
            }
        }

        /// <summary>Осталось секунд (по множителю скорости) — для панели ETA (этап 12).</summary>
        public float RemainingSeconds
        {
            get
            {
                if (playing == null) return 0f;
                float left = (float)(playing.duration - playhead);
                return Mathf.Max(0f, left / Mathf.Max(0.05f, playSpeed));
            }
        }

        /// <summary>Истекло секунд от начала воспроизведения.</summary>
        public float ElapsedSeconds { get { return (float)playhead; } }

        /// <summary>Покадровое ведение воспроизведения (вызывает хаб функций).</summary>
        public void Tick(float deltaTime)
        {
            if (recording != null)
            {
                recordClock += deltaTime;
                recordSampleTimer -= deltaTime;
                if (recordSampleTimer <= 0f) SampleNow();
            }

            if (playing == null) return;

            // Поток начал движение робота — воспроизведение уступает ему (без конфликта поз).
            if (flow != null && flow.Motion != null && flow.Motion.IsRunning)
            {
                StopPlayback("прервано: робот поехал по траектории этапа 4");
                return;
            }

            if (playPaused) return;

            playhead += deltaTime * playSpeed;

            int n = playing.SampleCount;
            while (playSampleIndex + 1 < n && playing.samples[playSampleIndex + 1].t <= playhead)
                playSampleIndex++;

            double[] q;
            if (playSampleIndex + 1 >= n)
            {
                q = playing.samples[n - 1].CloneQ();
            }
            else
            {
                KvRecordSample a = playing.samples[playSampleIndex];
                KvRecordSample b = playing.samples[playSampleIndex + 1];
                float span = b.t - a.t;
                float k = span > 0.0001f ? Mathf.Clamp01((float)((playhead - a.t) / span)) : 0f;
                q = new double[a.q.Length];
                for (int i = 0; i < q.Length; i++) q[i] = a.q[i] + (b.q[i] - a.q[i]) * k;
            }

            if (checkLimits && !flow.Validator.WithinLimits(q))
            {
                Fail("шаг воспроизведения вне лимитов суставов — воспроизведение остановлено");
                StopPlayback("СТОП: лимит сустава");
                return;
            }

            clearanceTimer -= deltaTime;
            if (checkClearance && world != null && clearanceTimer <= 0f)
            {
                clearanceTimer = Mathf.Max(0.05f, clearanceInterval);
                Vector3 tcpPoint;
                Vector3[] nodes;
                float clearance = flow.Validator.ClearanceAt(q, world, out tcpPoint, out nodes);
                if (clearance < minClearance)
                {
                    Fail("зазор " + (clearance * 1000f).ToString("0") + " мм — воспроизведение остановлено");
                    StopPlayback("СТОП: малый зазор");
                    return;
                }
            }

            flow.Validator.Apply(q);

            if (playhead >= playing.duration)
            {
                KvTrajectoryRecord done = playing;
                playing = null;
                playPaused = false;
                Debug.Log("[Record] воспроизведение «" + done.name + "» завершено");
                if (PlaybackFinished != null) PlaybackFinished(done);
            }
        }

        /// <summary>Остановить всё (Esc/аварийный стоп/сброс).</summary>
        public void StopAll(string why = "сброшено")
        {
            if (IsRecording) StopRecording(true);
            if (IsPlaying) StopPlayback(why);
        }

        private void Fail(string message)
        {
            Debug.LogWarning("[Record] " + message);
            if (Failed != null) Failed(message);
        }

        /// <summary>Записи, доступные на диске (для дерева моделей и списков).</summary>
        public static List<KvTrajectoryRecord> ListRecords()
        {
            return KvRecordStore.LoadAll();
        }
    }
}
