using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Роль узла в режиме совместной работы (этап 20 ТЗ).</summary>
    public enum KvNetRole
    {
        Off = 0,
        /// <summary>Оператор: управляет роботом и рассылает состояние.</summary>
        Operator = 1,
        /// <summary>Наблюдатель: принимает состояние и повторяет его у себя.</summary>
        Observer = 2
    }

    /// <summary>
    /// ЭТАП 20 ТЗ: МУЛЬТИПЛЕЕР (COLLABORATION MODE) — ДВА ПОЛЬЗОВАТЕЛЯ В ОДНОЙ СЦЕНЕ.
    ///
    /// Реализация без внешних пакетов (Netcode/Mirror в проект не добавлялись — это решение
    /// зафиксировано в контексте): обмен идёт по UDP компактными текстовыми пакетами.
    ///   • ОПЕРАТОР рассылает состояние 10 раз в секунду: имя робота, углы суставов, фазу
    ///     State Machine, выбранный вариант и время траектории;
    ///   • НАБЛЮДАТЕЛЬ принимает пакеты и применяет позу у себя — видит ту же сцену и то же
    ///     движение робота; свои команды движения он не выполняет (режим «только смотреть»).
    ///
    /// HEARTBEAT И КОНТРОЛЬ СВЯЗИ (ФИКС 11.В). Кроме пакетов состояния обе стороны каждые
    /// `heartbeatInterval` (по умолчанию 0.5 с) отправляют короткий пакет `HB|время`, а партнёр
    /// отвечает `HBACK|время` — по возвращённому времени измеряется задержка «туда-обратно».
    /// Если от партнёра не было НИ ОДНОГО пакета дольше `timeoutSeconds` (по умолчанию 2 с),
    /// состояние связи переходит в «потеряна»: в журнал пишется ОДНА строка (без спама — только
    /// на смену состояния), а наблюдатель ПЕРЕСТАЁТ применять чужие позы, чтобы робот не выглядел
    /// «зависшим» на последней позе. Когда пакеты снова пошли — связь возвращается и в журнал
    /// идёт тоже одна строка. Формат пакетов позы/фазы/варианта не менялся, поэтому партнёр
    /// старой версии работает как раньше: платформа один раз сообщает, что heartbeat
    /// не поддерживается, и продолжает обмен состоянием.
    ///
    /// Сетевые операции идут в ФОНОВОМ потоке, а сцена меняется только в главном (через
    /// очередь) — это обязательное правило Unity: из потока нельзя трогать Transform.
    /// </summary>
    public class KvCollaborationService
    {
        public const int DefaultPort = 47777;
        public const string RolePrefsKey = "KazistovVv.Net.Role";
        public const string PortPrefsKey = "KazistovVv.Net.Port";
        public const string PeerPrefsKey = "KazistovVv.Net.Peer";

        /// <summary>Состояние связи с партнёром (ФИКС 11.В).</summary>
        private enum KvLinkState
        {
            /// <summary>Связь ещё не подтверждена (роль только включена / партнёр старой версии).</summary>
            Unknown = 0,
            /// <summary>Пакеты от партнёра идут — связь есть.</summary>
            Up = 1,
            /// <summary>Пакетов нет дольше порога — связь потеряна.</summary>
            Down = 2
        }

        /// <summary>Как часто отправлять короткий пакет heartbeat, секунды (ФИКС 11.В).</summary>
        [Tooltip("Период отправки пакета heartbeat партнёру, секунды (по умолчанию 0,5 с)")]
        public float heartbeatInterval = 0.5f;

        /// <summary>Молчание партнёра дольше этого времени считается потерей связи, секунды (ФИКС 11.В).</summary>
        [Tooltip("Сколько секунд молчания партнёра считать потерей связи (по умолчанию 2 с)")]
        public float timeoutSeconds = 2f;

        public event Action<string> Message;

        private TrajectoryFlowController flow;

        private KvNetRole role = KvNetRole.Off;
        private int port = DefaultPort;
        private string peer = "127.0.0.1";

        private UdpClient sender;
        private UdpClient receiver;
        private Thread thread;
        private volatile bool running;
        private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();

        private float sendTimer;
        private float stateTimer;
        private int packetsSent, packetsReceived;
        private float lastPacketTime = -10f;
        private string lastRemote = "";
        private string log = "";

        // --- heartbeat и контроль связи (ФИКС 11.В) ---
        private KvLinkState link = KvLinkState.Unknown;
        private float heartbeatTimer;
        private int heartbeatsSent, heartbeatsReceived;
        private bool peerHeartbeat;            // партнёр отвечает на heartbeat (новая версия)
        private bool peerHeartbeatReported;    // «heartbeat не поддерживается» сказано один раз
        private bool sendErrorReported;        // про ошибку отправки служебного пакета — тоже один раз
        private float roundTripMs = -1f;       // задержка «туда-обратно» по heartbeat, мс
        private int remotePosesIgnored;        // сколько чужих поз не применено из-за потери связи
        private volatile IPEndPoint peerEndpoint;   // адрес партнёра, с которого пришёл пакет

        public KvNetRole Role { get { return role; } }
        public int Port { get { return port; } }
        public string Peer { get { return peer; } }
        public int PacketsSent { get { return packetsSent; } }
        public int PacketsReceived { get { return packetsReceived; } }
        public float HeartbeatInterval { get { return heartbeatInterval; } }
        public float TimeoutSeconds { get { return timeoutSeconds; } }
        public int HeartbeatsSent { get { return heartbeatsSent; } }
        public int HeartbeatsReceived { get { return heartbeatsReceived; } }
        public float RoundTripMs { get { return roundTripMs; } }
        public int RemotePosesIgnored { get { return remotePosesIgnored; } }
        public bool LinkAlive { get { return link == KvLinkState.Up; } }

        /// <summary>
        /// Строка состояния связи для вкладки (ФИКС 11.В): «связь есть, задержка N мс» либо
        /// «связь потеряна». Задержка измеряется по heartbeat и является задержкой «туда-обратно».
        /// </summary>
        public string LinkStatus()
        {
            if (role == KvNetRole.Off) return "сеть выключена";
            switch (link)
            {
                case KvLinkState.Up:
                    if (roundTripMs >= 0f)
                        return "связь есть, задержка " + roundTripMs.ToString("0") + " мс";
                    return peerHeartbeat
                        ? "связь есть, задержка ещё не измерена"
                        : "связь есть, задержка не измеряется (heartbeat не поддерживается партнёром)";
                case KvLinkState.Down:
                    return role == KvNetRole.Observer
                        ? "связь потеряна · чужие позы не применяются (робот в последней принятой позе)"
                        : "связь потеряна";
                default:
                    return peerHeartbeat
                        ? "связь ещё не подтверждена"
                        : "связь не подтверждается: heartbeat не поддерживается партнёром (обмен как раньше)";
            }
        }

        public string Status()
        {
            switch (role)
            {
                case KvNetRole.Operator:
                    return "оператор · порт " + port + " · отправлено пакетов " + packetsSent +
                           " · " + LinkStatus();
                case KvNetRole.Observer:
                    return "наблюдатель · " + peer + ":" + port + " · принято " + packetsReceived +
                           " · " + LinkStatus();
                default:
                    return "выключено";
            }
        }

        public string Log { get { return log; } }

        public void Bind(TrajectoryFlowController controller)
        {
            flow = controller;
            role = (KvNetRole)Mathf.Clamp(PlayerPrefs.GetInt(RolePrefsKey, 0), 0, 2);
            port = PlayerPrefs.GetInt(PortPrefsKey, DefaultPort);
            peer = PlayerPrefs.GetString(PeerPrefsKey, "127.0.0.1");
        }

        /// <summary>Установить роль (Off/Operator/Observer) — с перезапуском сокетов.</summary>
        public void SetRole(KvNetRole value)
        {
            if (role == value) return;
            Stop();
            role = value;
            PlayerPrefs.SetInt(RolePrefsKey, (int)role);
            PlayerPrefs.Save();
            if (role != KvNetRole.Off) Start();
            Report("режим совместной работы: " + Status());
        }

        public void SetPort(int value)
        {
            port = Mathf.Clamp(value, 1024, 65000);
            PlayerPrefs.SetInt(PortPrefsKey, port);
            PlayerPrefs.Save();
            if (role != KvNetRole.Off) { Stop(); Start(); }
        }

        public void SetPeer(string value)
        {
            peer = string.IsNullOrEmpty(value) ? "127.0.0.1" : value.Trim();
            PlayerPrefs.SetString(PeerPrefsKey, peer);
            PlayerPrefs.Save();
        }

        private void Start()
        {
            try
            {
                if (role == KvNetRole.Operator)
                {
                    sender = new UdpClient();
                    sender.EnableBroadcast = true;
                    sender.Connect(new IPEndPoint(IPAddress.Parse(peer), port));
                }
                else
                {
                    receiver = new UdpClient(port);
                }
                running = true;

                // ФИКС 11.В: связь начинается «неизвестной», отсчёт тишины — с момента запуска,
                // поэтому сразу после включения роли ложного «связь потеряна» не будет.
                link = KvLinkState.Unknown;
                heartbeatTimer = 0f;
                heartbeatsSent = 0;
                heartbeatsReceived = 0;
                peerHeartbeat = false;
                peerHeartbeatReported = false;
                sendErrorReported = false;
                roundTripMs = -1f;
                remotePosesIgnored = 0;
                peerEndpoint = null;
                lastPacketTime = Time.realtimeSinceStartup;

                thread = new Thread(Loop) { IsBackground = true, Name = "KvNet" };
                thread.Start();
                Report("сеть запущена: " + Status());
            }
            catch (Exception e)
            {
                running = false;
                Report("сеть не запущена: " + e.Message);
                role = KvNetRole.Off;
            }
        }

        public void Stop()
        {
            running = false;
            try { if (sender != null) sender.Close(); } catch (Exception) { }
            try { if (receiver != null) receiver.Close(); } catch (Exception) { }
            sender = null;
            receiver = null;
            if (thread != null && thread.IsAlive) thread.Join(200);
            thread = null;

            // ФИКС 11.В: после остановки состояние связи сбрасывается, иначе в интерфейсе
            // осталось бы «связь есть» от прошлого сеанса.
            link = KvLinkState.Unknown;
            peerEndpoint = null;
            roundTripMs = -1f;
        }

        /// <summary>Фоновый поток: приём пакетов (сцена здесь НЕ трогается).</summary>
        private void Loop()
        {
            while (running)
            {
                try
                {
                    // Оператор тоже принимает: ответы heartbeat приходят на тот же сокет,
                    // которым он рассылает состояние (сокет подключён к партнёру, поэтому
                    // посторонние датаграммы отбрасывает сама система).
                    UdpClient inbox = receiver != null
                        ? receiver
                        : (role == KvNetRole.Operator ? sender : null);

                    if (inbox != null)
                    {
                        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                        byte[] data = inbox.Receive(ref remote);
                        if (data != null && data.Length > 0)
                        {
                            string text = Encoding.UTF8.GetString(data);
                            incoming.Enqueue(text);
                            lastRemote = remote.Address.ToString();
                            peerEndpoint = remote;   // по этому адресу наблюдатель отвечает HBACK
                        }
                    }
                    else Thread.Sleep(50);
                }
                catch (SocketException)
                {
                    if (!running) break;
                    Thread.Sleep(80);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { Thread.Sleep(120); }
            }
        }

        /// <summary>Кадровое обслуживание: отправка состояния и heartbeat или применение принятого.</summary>
        public void Tick(float deltaTime)
        {
            if (role == KvNetRole.Off || flow == null || flow.Validator == null ||
                !flow.Validator.Ready) return;

            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                stateTimer = 0.1f;
                if (role == KvNetRole.Operator) SendState();
            }

            // ФИКС 11.В: heartbeat — короткий пакет со своим временем; партнёр отвечает HBACK,
            // по нему и измеряется задержка. Отправляют обе роли (у наблюдателя — после того,
            // как он узнал адрес оператора из первого принятого пакета).
            heartbeatTimer -= deltaTime;
            if (heartbeatTimer <= 0f)
            {
                heartbeatTimer = Mathf.Max(0.1f, heartbeatInterval);
                SendHeartbeat();
            }

            string packet;
            int guard = 0;
            while (incoming.TryDequeue(out packet) && guard++ < 8)
            {
                packetsReceived++;
                lastPacketTime = Time.realtimeSinceStartup;

                if (IsHeartbeat(packet))
                {
                    HandleHeartbeat(packet);      // служебный пакет — это не поза
                    continue;
                }
                if (role != KvNetRole.Observer) continue;

                // Связь потеряна — чужие позы НЕ применяем: иначе робот «замирает» на последней
                // позе, и это выглядит как зависание платформы (ФИКС 11.В).
                if (link == KvLinkState.Down)
                {
                    remotePosesIgnored++;
                    continue;
                }
                ApplyState(packet);
            }

            UpdateLink();
        }

        /// <summary>Короткий пакет heartbeat: «HB|время» — партнёр отвечает «HBACK|время» (ФИКС 11.В).</summary>
        private void SendHeartbeat()
        {
            string time = Time.realtimeSinceStartup.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture);

            if (role == KvNetRole.Operator) SendRaw("HB|" + time, null);
            else if (receiver != null && peerEndpoint != null) SendRaw("HB|" + time, peerEndpoint);
            else return;      // наблюдатель ещё не знает адрес партнёра — ждём первый пакет

            heartbeatsSent++;
        }

        /// <summary>Служебный пакет heartbeat или ответ на него (не поза и не состояние).</summary>
        private static bool IsHeartbeat(string packet)
        {
            return packet != null && (packet.StartsWith("HB|", StringComparison.Ordinal) ||
                                     packet.StartsWith("HBACK|", StringComparison.Ordinal));
        }

        /// <summary>Обработать heartbeat: на «HB» ответить «HBACK» с тем же временем, на «HBACK» — измерить задержку.</summary>
        private void HandleHeartbeat(string packet)
        {
            heartbeatsReceived++;
            peerHeartbeat = true;

            if (!packet.StartsWith("HBACK|", StringComparison.Ordinal))
            {
                // Пришёл HB — отвечаем тем же временем, чтобы партнёр посчитал задержку.
                string echo = packet.Substring(3);
                if (role == KvNetRole.Operator) SendRaw("HBACK|" + echo, null);
                else SendRaw("HBACK|" + echo, peerEndpoint);
                return;
            }

            double sent;
            if (!double.TryParse(packet.Substring(6), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out sent)) return;
            double ms = (Time.realtimeSinceStartup - sent) * 1000.0;
            if (ms >= 0.0) roundTripMs = (float)ms;
        }

        /// <summary>
        /// Отправить служебный пакет. `to` — адрес партнёра для наблюдателя (у оператора сокет
        /// уже подключён к нему), при null используется подключённый сокет оператора.
        /// </summary>
        private void SendRaw(string text, IPEndPoint to)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            try
            {
                if (to != null && receiver != null) receiver.Send(data, data.Length, to);
                else if (sender != null) sender.Send(data, data.Length);
            }
            catch (Exception e)
            {
                // Про сбой отправки сообщаем один раз: heartbeat идёт дважды в секунду,
                // и повторять одну и ту же ошибку в журнале было бы спамом.
                if (sendErrorReported) return;
                sendErrorReported = true;
                Report("служебный пакет «" + text + "» не отправлен: " + e.Message);
            }
        }

        /// <summary>
        /// Контроль связи (ФИКС 11.В): если от партнёра не было ни одного пакета дольше
        /// `timeoutSeconds`, состояние переходит в «потеряна»; когда пакеты снова пошли —
        /// возвращается «есть». В журнал пишется ОДНА строка на смену состояния, без спама.
        /// </summary>
        private void UpdateLink()
        {
            float silence = Time.realtimeSinceStartup - lastPacketTime;
            bool fresh = silence <= Mathf.Max(0.2f, timeoutSeconds);

            KvLinkState next;
            if (fresh) next = KvLinkState.Up;
            // Оператор рассылает состояние в одну сторону: у партнёра старой версии подтвердить
            // связь нечем, поэтому «потеряна» ему не приписывается — пишем честно, что heartbeat
            // не поддерживается, и работаем как раньше.
            else if (role == KvNetRole.Operator && !peerHeartbeat) next = KvLinkState.Unknown;
            else next = KvLinkState.Down;

            // Партнёр не отвечает на heartbeat вовсе (ни HBACK, ни своего HB) — говорим об этом
            // один раз и продолжаем работать как раньше: так и выглядит партнёр старой версии.
            if (!peerHeartbeat && heartbeatsReceived == 0 && heartbeatsSent >= 4 && !peerHeartbeatReported)
            {
                peerHeartbeatReported = true;
                Report("heartbeat не поддерживается партнёром (похоже, у него версия без heartbeat) — " +
                       "обмен состоянием и позами работает как раньше");
            }

            SetLink(next, silence);
        }

        /// <summary>Сменить состояние связи и сказать об этом в журнал ровно один раз на смену.</summary>
        private void SetLink(KvLinkState value, float silence)
        {
            if (link == value) return;
            KvLinkState previous = link;
            link = value;

            if (value == KvLinkState.Up)
            {
                if (previous == KvLinkState.Down)
                    Report("связь восстановлена: пакеты от партнёра идут снова" +
                           (role == KvNetRole.Observer ? " · чужие позы снова применяются" : ""));
                return;
            }
            if (value != KvLinkState.Down) return;

            string tail = silence.ToString("0.0") + " с без пакетов (порог " +
                          timeoutSeconds.ToString("0.0") + " с)";
            if (packetsReceived == 0)
                Report("связь не установлена: от партнёра не принято ни одного пакета — " + tail);
            else
                Report("связь потеряна: от партнёра нет пакетов — " + tail +
                       (role == KvNetRole.Observer
                           ? " · чужие позы не применяются (робот остаётся в последней принятой позе)"
                           : ""));
        }

        private void SendState()
        {
            if (sender == null) return;
            try
            {
                double[] q = flow.Validator.CopyCurrent();
                var sb = new StringBuilder();
                sb.Append("K|").Append(flow.Robot != null ? flow.Robot.robotName : "-").Append('|');
                for (int i = 0; i < q.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(q[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                }
                sb.Append('|').Append(flow.State.phase)
                  .Append('|').Append(flow.State.selectedTrajectory)
                  .Append('|').Append(flow.State.hasPoint ? 1 : 0)
                  .Append('|').Append(Time.realtimeSinceStartup.ToString("0.00",
                      System.Globalization.CultureInfo.InvariantCulture));
                byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
                sender.Send(data, data.Length);
                packetsSent++;
            }
            catch (Exception e)
            {
                Report("пакет не отправлен: " + e.Message);
                Stop();
                role = KvNetRole.Off;
            }
        }

        /// <summary>
        /// Применить принятый пакет состояния (только роль «наблюдатель» и только при живой связи —
        /// проверку делает <see cref="Tick"/>). Формат пакета не менялся: «K|робот|углы|фаза|вариант|…».
        /// </summary>
        private void ApplyState(string packet)
        {
            string[] parts = packet.Split('|');
            if (parts.Length < 6 || parts[0] != "K")
            {
                if (parts.Length > 0 && parts[0] == "H") ApplyHello(packet);
                return;
            }

            string robot = parts[1];
            string[] qParts = parts[2].Split(',');
            double[] q = new double[qParts.Length];
            for (int i = 0; i < qParts.Length; i++)
            {
                double v;
                if (!double.TryParse(qParts[i], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v)) return;
                q[i] = v;
            }

            if (flow.Validator.Dof != q.Length) return;
            if (flow.State.phase == FlowState.RobotMoving) return;   // наблюдатель не мешает своему роботу
            flow.Validator.Apply(flow.Validator.ContinueFrom(flow.Validator.CopyCurrent(), q));

            if (log.Length > 400) log = log.Substring(log.Length - 300);
            log = "принято от " + lastRemote + ": " + robot + " · " + parts[3] +
                  " · вариант " + parts[4];
        }

        private void ApplyHello(string packet)
        {
            log = "приветствие наблюдателя: " + packet;
        }

        /// <summary>Текстовое приветствие (наблюдатель может «представиться»).</summary>
        public void SendHello()
        {
            if (role != KvNetRole.Operator || sender == null) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes("H|observer-here");
                sender.Send(data, data.Length);
            }
            catch (Exception) { }
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Net] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ЭТАП 21 ТЗ: ВЕБ-ДАШБОРД — мониторинг состояния робота через браузер.
    ///
    /// Поднимается МАЛЕНЬКИЙ HTTP-СЕРВЕР на «сыром» сокете (`TcpListener`): так не нужны
    /// права администратора на резервирование URL (чего требует `HttpListener`).
    /// Отдаёт страницу мониторинга (`/`) и состояние в JSON (`/api/state`): фаза State Machine,
    /// робот, координаты TCP, метрики выбранной траектории, здоровье суставов и состояние
    /// зарядки/калибровки. Данные готовит ГЛАВНЫЙ поток (в фоне их только отдают).
    /// </summary>
    public class KvWebDashboard
    {
        public const int DefaultPort = 47800;

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;
        private KvStageHub3 stage3;

        private TcpListener listener;
        private Thread thread;
        private volatile bool running;
        private volatile string stateJson = "{}";
        private int port = DefaultPort;
        private int requests;
        private float timer;

        public bool Running { get { return running; } }
        public int Port { get { return port; } }
        public int Requests { get { return requests; } }
        public string Address
        {
            get { return "http://127.0.0.1:" + port + "/"; }
        }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvStageHub3 hub3)
        {
            flow = controller;
            features = hub;
            stage3 = hub3;
        }

        public void SetPort(int value)
        {
            port = Mathf.Clamp(value, 1024, 65000);
            if (running) { Stop(); Start(); }
        }

        public bool Start()
        {
            if (running) return true;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                running = true;
                thread = new Thread(Loop) { IsBackground = true, Name = "KvWeb" };
                thread.Start();
                Report("веб-дашборд запущен: " + Address);
                return true;
            }
            catch (Exception e)
            {
                running = false;
                Report("веб-дашборд не запущен: " + e.Message);
                return false;
            }
        }

        public void Stop()
        {
            running = false;
            try { if (listener != null) listener.Stop(); } catch (Exception) { }
            listener = null;
            if (thread != null && thread.IsAlive) thread.Join(200);
            thread = null;
            Report("веб-дашборд остановлен");
        }

        public void Toggle()
        {
            if (running) Stop();
            else Start();
        }

        private void Loop()
        {
            while (running)
            {
                try
                {
                    using (TcpClient client = listener.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = 2000;
                        client.SendTimeout = 2000;
                        Interlocked.Increment(ref requests);
                        using (NetworkStream stream = client.GetStream())
                        {
                            string request = ReadRequest(stream);
                            string path = "/";
                            if (!string.IsNullOrEmpty(request))
                            {
                                string[] parts = request.Split(' ');
                                if (parts.Length > 1) path = parts[1];
                            }
                            string body;
                            string type;
                            if (path.StartsWith("/api/state"))
                            {
                                body = stateJson;
                                type = "application/json; charset=utf-8";
                            }
                            else if (path.StartsWith("/api/robow"))
                            {
                                body = stateJson;
                                type = "application/json; charset=utf-8";
                            }
                            else
                            {
                                body = Page();
                                type = "text/html; charset=utf-8";
                            }
                            byte[] payload = Encoding.UTF8.GetBytes(body);
                            string header = "HTTP/1.1 200 OK\r\nContent-Type: " + type +
                                            "\r\nContent-Length: " + payload.Length +
                                            "\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n";
                            byte[] head = Encoding.ASCII.GetBytes(header);
                            stream.Write(head, 0, head.Length);
                            stream.Write(payload, 0, payload.Length);
                            stream.Flush();
                        }
                    }
                }
                catch (SocketException) { if (!running) break; Thread.Sleep(50); }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { Thread.Sleep(80); }
            }
        }

        private static string ReadRequest(NetworkStream stream)
        {
            var buffer = new byte[2048];
            int total = 0;
            try
            {
                while (total < buffer.Length)
                {
                    int read = stream.Read(buffer, total, buffer.Length - total);
                    if (read <= 0) break;
                    total += read;
                    string soFar = Encoding.ASCII.GetString(buffer, 0, total);
                    if (soFar.Contains("\r\n")) return soFar;
                }
            }
            catch (Exception) { }
            return total > 0 ? Encoding.ASCII.GetString(buffer, 0, total) : "";
        }

        /// <summary>Кадровое обслуживание: главный поток готовит JSON состояния.</summary>
        public void Tick(float deltaTime)
        {
            if (!running) return;
            timer -= deltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            stateJson = BuildJson();
        }

        private string BuildJson()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
                return "{\"ready\":false}";

            PoseValidator v = flow.Validator;
            double[] q = v.CopyCurrent();
            Vector3 tcp = v.TcpAt(q);
            var sb = new StringBuilder();
            sb.Append("{\"ready\":true");
            sb.Append(",\"robot\":\"").Append(flow.Robot != null ? flow.Robot.robotName : "-").Append('"');
            sb.Append(",\"dof\":").Append(v.Dof);
            sb.Append(",\"phase\":\"").Append(flow.State.phase.ToString()).Append('"');
            sb.Append(",\"hasPoint\":").Append(flow.State.hasPoint ? "true" : "false");
            sb.Append(",\"tcp\":[").Append(Num(tcp.x)).Append(',').Append(Num(tcp.y)).Append(',')
              .Append(Num(tcp.z)).Append(']');
            sb.Append(",\"joints\":[");
            for (int i = 0; i < q.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Num((float)q[i]));
            }
            sb.Append(']');
            sb.Append(",\"limitMargin\":").Append(Num(v.LimitMargin(q)));
            sb.Append(",\"candidates\":").Append(flow.State.candidates.Count);
            sb.Append(",\"selected\":").Append(flow.State.selectedTrajectory);
            if (flow.State.selectedTrajectory >= 0 &&
                flow.State.selectedTrajectory < flow.State.candidates.Count)
            {
                TrajectoryCandidate c = flow.State.candidates[flow.State.selectedTrajectory];
                if (c != null)
                {
                    sb.Append(",\"trajectory\":{\"label\":\"").Append(EscapeJson(c.label)).Append('"');
                    sb.Append(",\"time\":").Append(Num(c.timeS));
                    sb.Append(",\"length\":").Append(Num(c.lengthM));
                    sb.Append(",\"clearance\":").Append(Num(c.minClearance));
                    sb.Append(",\"safe\":").Append(c.safe ? "true" : "false").Append('}');
                }
            }
            KvHealthMonitor health = HealthMonitor();
            if (health != null && health.Temperature != null && health.Temperature.Length > 0)
            {
                float maxTemp = MaxOf(health.Temperature);
                float maxCurrent = MaxOf(health.Current);
                float maxWear = MaxOf(health.Wear);
                int worst;
                float life = health.ForecastHours(false, out worst);
                sb.Append(",\"health\":{\"maxTemp\":").Append(Num(maxTemp))
                  .Append(",\"maxCurrent\":").Append(Num(maxCurrent))
                  .Append(",\"wear\":").Append(Num(maxWear))
                  .Append(",\"lifeHours\":").Append(Num(life)).Append('}');
            }
            if (stage3 != null && stage3.Constrained != null)
                sb.Append(",\"constraint\":\"").Append(EscapeJson(stage3.Constrained.Profile.Describe()))
                  .Append('"');
            if (stage3 != null && stage3.Calibration != null)
                sb.Append(",\"calibration\":\"").Append(
                    stage3.Calibration.TcpSolved ? "ok" : "none").Append('"');
            sb.Append(",\"uptime\":").Append(Num(Time.realtimeSinceStartup));
            sb.Append('}');
            return sb.ToString();
        }

        private static string Num(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Монитор здоровья суставов (этап 12 ТЗ) — источник телеметрии для JSON.</summary>
        private static KvHealthMonitor HealthMonitor()
        {
            KvStageHub hub = KvStageHub.Current;
            return hub != null ? hub.Health : null;
        }

        private static float MaxOf(float[] values)
        {
            if (values == null || values.Length == 0) return 0f;
            float max = values[0];
            for (int i = 1; i < values.Length; i++) if (values[i] > max) max = values[i];
            return max;
        }

        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
        }

        /// <summary>Страница мониторинга (обновляется сама, рисует таблицу из JSON).</summary>
        private string Page()
        {
            return "<!DOCTYPE html><html lang=\"ru\"><head><meta charset=\"utf-8\">" +
                   "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                   "<title>KazistovVv · мониторинг</title><style>" +
                   "body{background:#14161c;color:#e6e8ee;font:14px/1.45 system-ui,Segoe UI,Arial;" +
                   "margin:0;padding:24px}h1{font-size:20px;margin:0 0 4px}" +
                   ".card{background:#1d2028;border:1px solid #2b2f3a;border-radius:10px;padding:14px 16px;" +
                   "margin:12px 0;max-width:820px}" +
                   "table{border-collapse:collapse;width:100%}td{padding:4px 8px;border-bottom:1px solid #262a34}" +
                   ".k{color:#9aa3b5;width:42%}.v{font-variant-numeric:tabular-nums}" +
                   ".ok{color:#5ad07a}.warn{color:#f0b429}.err{color:#ef5b4c}" +
                   "code{background:#11131a;padding:2px 6px;border-radius:6px}</style></head><body>" +
                   "<h1>KazistovVv · веб-дашборд</h1>" +
                   "<div>Обновление каждые 2 секунды · API: <code>/api/state</code></div>" +
                   "<div class=\"card\" id=\"main\">Загрузка…</div>" +
                   "<script>async function tick(){try{const r=await fetch('/api/state');" +
                   "const s=await r.json();render(s);}catch(e){document.getElementById('main').innerHTML=" +
                   "'<span class=err>нет связи</span>';}}function f(x,n){return Number(x).toFixed(n);}" +
                   "function render(s){if(!s.ready){document.getElementById('main').innerHTML=" +
                   "'робот не определён';return;}let h='<table>';" +
                   "h+='<tr><td class=k>Робот</td><td class=v>'+s.robot+' ('+s.dof+' осей)</td></tr>';" +
                   "h+='<tr><td class=k>Состояние</td><td class=v>'+s.phase+'</td></tr>';" +
                   "h+='<tr><td class=k>Точка</td><td class=v>'+(s.hasPoint?'выбрана':'нет')+'</td></tr>';" +
                   "h+='<tr><td class=k>TCP</td><td class=v>'+f(s.tcp[0],3)+', '+f(s.tcp[1],3)+', '" +
                   "+f(s.tcp[2],3)+'</td></tr>';" +
                   "h+='<tr><td class=k>Запас лимитов</td><td class=v>'+f(s.limitMargin,1)+' °</td></tr>';" +
                   "h+='<tr><td class=k>Углы</td><td class=v>'+s.joints.map(v=>f(v,1)).join(' · ')+" +
                   "'</td></tr>';h+='<tr><td class=k>Вариантов</td><td class=v>'+s.candidates+" +
                   "' (выбран '+(s.selected>=0?s.selected+1:'—')+')</td></tr>';" +
                   "if(s.trajectory){h+='<tr><td class=k>Траектория</td><td class=v>'+s.trajectory.label+" +
                   "' · '+f(s.trajectory.time,2)+' с · '+f(s.trajectory.length,3)+' м · зазор '+" +
                   "f(s.trajectory.clearance*1000,0)+' мм '+(s.trajectory.safe?'':'(риск)')+'</td></tr>';}" +
                   "if(s.health){h+='<tr><td class=k>Температура</td><td class=v>'+f(s.health.maxTemp,1)+" +
                   "' °C</td></tr>';h+='<tr><td class=k>Ток</td><td class=v>'+f(s.health.maxCurrent,2)+" +
                   "' А</td></tr>';h+='<tr><td class=k>Износ</td><td class=v>'+f(s.health.wear*100,1)+" +
                   "' %</td></tr>';h+='<tr><td class=k>Ресурс</td><td class=v>'+f(s.health.lifeHours,1)+" +
                   "' ч</td></tr>';}if(s.constraint){h+='<tr><td class=k>Ограничение</td><td class=v>'+" +
                   "s.constraint+'</td></tr>';}h+='<tr><td class=k>Калибровка</td><td class=v>'+" +
                   "(s.calibration==='ok'?'есть':'нет')+'</td></tr>';" +
                   "h+='</table>';document.getElementById('main').innerHTML=h;}" +
                   "tick();setInterval(tick,2000);</script></body></html>";
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[WebDashboard] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ЭТАП 22 ТЗ: МОБИЛЬНОЕ ПРИЛОЖЕНИЕ-КОМПАНЬОН (ПЛАНШЕТ/ТЕЛЕФОН КАК ПУЛЬТ).
    ///
    /// Заготовка с рабочим каналом: по UDP принимаются текстовые команды
    ///   `PING` · `STOP` · `HOME` · `SELECT 3` · `STATUS` · `RUN` · `PAUSE`,
    /// они ставятся в очередь и выполняются в главном потоке ТЕМИ ЖЕ обработчиками, что
    /// кнопки интерфейса (аварийная остановка, переезд домой, выбор варианта, пуск/пауза).
    /// В ответ на `STATUS`/`PING` уходит компактная строка состояния — так планшет видит,
    /// что происходит на роботе.
    ///
    /// АУТЕНТИФИКАЦИЯ (15.11.2026, волна A). КАК БЫЛО. Канал принимал команду с ЛЮБОГО адреса
    /// сети, где открыт порт: `STOP` останавливал робота, `HOME` вёл его в позу — без единого
    /// признака того, кто отправил пакет. ЧЕМ ПЛОХО: аварийная остановка и переезд — действия,
    /// которые не должен диктовать чужой пакет в той же подсети. ЧТО СТАЛО. При включении
    /// выдаётся токен (`IssueToken`), он печатается в журнал и виден в статусе вкладки
    /// «Сеть» (`Status`), и каждая команда обязана начинаться с него: `<токен> STOP`.
    /// Без токена — отказ с названной причиной и без токена в ответе. Совместимость
    /// сохранена именно так: токен не надо искать по коду, он в журнале и в статусе сразу
    /// при включении; старые клиенты получают честный отказ вместо молчаливого выполнения.
    /// </summary>
    public class KvCompanionServer
    {
        public const int DefaultPort = 47810;

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;
        private KvStageHub3 stage3;

        private UdpClient socket;
        private Thread thread;
        private volatile bool running;
        private readonly ConcurrentQueue<KeyValuePair<string, IPEndPoint>> queue =
            new ConcurrentQueue<KeyValuePair<string, IPEndPoint>>();
        private int port = DefaultPort;
        private int commands;
        private string lastCommand = "";
        private string token = "";

        public bool Running { get { return running; } }
        public int Port { get { return port; } }
        public int Commands { get { return commands; } }
        public string LastCommand { get { return lastCommand; } }

        /// <summary>Текущий токен (пусто, пока пульт выключен — тогда команды не принимаются).</summary>
        public string Token { get { return token; } }

        /// <summary>
        /// ВЫДАТЬ НОВЫЙ ТОКЕН. Зовётся при включении пульта (`Start`), поэтому токен ротируется
        /// каждым включением: прошлый клиент при отключении перестаёт быть валидным. Публичен,
        /// чтобы поведение проверялось без открытия сокета (тест не ходит в сеть).
        /// </summary>
        public string IssueToken()
        {
            token = Guid.NewGuid().ToString("N").Substring(0, 16);
            return token;
        }

        /// <summary>
        /// СТРОКА СОСТОЯНИЯ для вкладки «Сеть» и статуса: порт, токен, счётчик команд.
        /// Токен показывается ЗДЕСЬ (и в журнале при включении) — оператор собирает команду
        /// `<токен> STOP` сразу, не вскрывая код.
        /// </summary>
        public string Status()
        {
            string text = running
                ? "порт " + port + " · команд принято " + commands +
                  (string.IsNullOrEmpty(lastCommand) ? "" : " · последняя: " + lastCommand)
                : "выключено";
            if (!string.IsNullOrEmpty(token)) text += " · токен: " + token;
            return text;
        }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvStageHub3 hub3)
        {
            flow = controller;
            features = hub;
            stage3 = hub3;
        }

        public void SetPort(int value)
        {
            port = Mathf.Clamp(value, 1024, 65000);
            if (running) { Stop(); Start(); }
        }

        public bool Start()
        {
            if (running) return true;
            try
            {
                socket = new UdpClient(port);
                running = true;
                // ТОКЕН ВЫДАЁТСЯ ПРИ ВКЛЮЧЕНИИ и печатается сразу: без него в журнале и в
                // статусе оператор не смог бы собрать команду `<токен> STOP`.
                IssueToken();
                thread = new Thread(Loop) { IsBackground = true, Name = "KvCompanion" };
                thread.Start();
                Report("мобильный пульт слушает порт " + port + " · токен: " + token +
                       " (команда: «" + token + " STOP») · команды PING/STOP/HOME/SELECT n/STATUS " +
                       "принимаются только с токеном");
                return true;
            }
            catch (Exception e)
            {
                running = false;
                Report("мобильный пульт не запущен: " + e.Message);
                return false;
            }
        }

        public void Stop()
        {
            running = false;
            try { if (socket != null) socket.Close(); } catch (Exception) { }
            socket = null;
            if (thread != null && thread.IsAlive) thread.Join(200);
            thread = null;
            // ТОКЕН СНИМАЕТСЯ ВМЕСТЕ С ВКЛЮЧЕНИЕМ: пока пульт выключен, команды не принимаются,
            // и прошлый клиент с прежним токеном получает отказ «пульт выключен».
            token = "";
            Report("мобильный пульт остановлен");
        }

        public void Toggle()
        {
            if (running) Stop();
            else Start();
        }

        private void Loop()
        {
            while (running)
            {
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = socket.Receive(ref remote);
                    if (data == null || data.Length == 0) continue;
                    string text = Encoding.UTF8.GetString(data).Trim();
                    if (text.Length == 0) continue;
                    queue.Enqueue(new KeyValuePair<string, IPEndPoint>(text, remote));
                }
                catch (SocketException) { if (!running) break; Thread.Sleep(50); }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { Thread.Sleep(80); }
            }
        }

        /// <summary>Кадровое обслуживание: выполнение команд в главном потоке.</summary>
        public void Tick(float deltaTime)
        {
            if (!running) return;
            KeyValuePair<string, IPEndPoint> item;
            int guard = 0;
            while (queue.TryDequeue(out item) && guard++ < 8)
            {
                commands++;
                lastCommand = item.Key;
                string reply = Execute(item.Key);
                if (!string.IsNullOrEmpty(reply) && item.Value != null) Reply(item.Value, reply);
            }
        }

        /// <summary>
        /// ВЫПОЛНИТЬ ВХОДЯЩУЮ КОМАНДУ (в главном потоке) — С ПРОВЕРКОЙ ТОКЕНА.
        ///
        /// Формат: `&lt;токен&gt; &lt;команда&gt;`, например `9f3c… STOP`. Публичен, чтобы отказ без
        /// токена проверялся тестом напрямую, без сокета и без сети.
        /// </summary>
        public string Execute(string raw)
        {
            string command = raw == null ? "" : raw.Trim();
            if (command.Length == 0) return "ERR|пустая команда";

            // --- АУТЕНТИФИКАЦИЯ. Первое слово — токен, выданный при включении пульта.
            // Проверка идёт ПЕРЕД разбором (и для PING/STATUS тоже): иначе «прочитать состояние
            // робота» мог бы любой пакет в сети, а отказ обязан называть причину, а не молчать.
            int space = command.IndexOf(' ');
            string given = space < 0 ? command : command.Substring(0, space);
            string body = space < 0 ? "" : command.Substring(space + 1).Trim();

            if (string.IsNullOrEmpty(token))
                return "ERR|пульт выключен: токена нет — включите пульт (токен печатается в " +
                       "журнале и в статусе вкладки «Сеть»)";
            if (!string.Equals(given, token, StringComparison.Ordinal))
                return "ERR|нет токена: команда отклонена; формат «<токен> STOP» — токен " +
                       "печатается при включении пульта";
            if (body.Length == 0)
                return "ERR|нет команды после токена: формат «<токен> STOP»";

            string upper = body.ToUpperInvariant();

            if (upper == "PING") return "PONG|" + State();
            if (upper == "STATUS") return State();
            if (flow == null) return "ERR|робот не привязан";

            if (upper == "STOP")
            {
                if (features != null) features.EmergencyStop();
                Report("команда с планшета: АВАРИЙНАЯ ОСТАНОВКА");
                return "OK|STOP";
            }
            if (upper == "HOME")
            {
                bool ok = GoHome();
                Report("команда с планшета: ДОМОЙ (" + (ok ? "принято" : "отклонено") + ")");
                return ok ? "OK|HOME" : "ERR|робот занят или план не построен";
            }
            if (upper == "RUN")
            {
                bool ok = flow.State.phase == FlowState.PhantomsMoving &&
                          flow.ConfirmSelectedTrajectory();
                Report("команда с планшета: ПУСК (" + (ok ? "принято" : "нечего запускать") + ")");
                return ok ? "OK|RUN" : "ERR|нечего запускать";
            }
            if (upper == "PAUSE")
            {
                if (flow.Motion != null)
                {
                    bool paused = flow.Motion.Paused;
                    flow.Motion.SetPaused(!paused);
                    return "OK|" + (paused ? "RESUME" : "PAUSE");
                }
                return "ERR|движение недоступно";
            }
            if (upper.StartsWith("SELECT"))
            {
                int index;
                string tail = body.Length > 6 ? body.Substring(6).Trim() : "";
                if (int.TryParse(tail, out index))
                {
                    bool ok = flow.SelectCandidateByIndex(index - 1);
                    Report("команда с планшета: выбран вариант №" + index + " (" +
                           (ok ? "принято" : "нет такого варианта") + ")");
                    return ok ? "OK|SELECT " + index : "ERR|нет варианта " + index;
                }
                return "ERR|формат: SELECT 3";
            }
            return "ERR|неизвестная команда: " + body;
        }

        /// <summary>Переезд в домашнюю позу штатным планировщиком (та же логика, что кнопка ДОМОЙ).</summary>
        private bool GoHome()
        {
            if (flow == null || !flow.Validator.Ready) return false;
            if (flow.ExternalMotionRunning || flow.State.phase == FlowState.RobotMoving) return false;

            PoseValidator v = flow.Validator;
            double[] start = v.CopyCurrent();
            double[] home = new double[v.Dof];
            for (int i = 0; i < home.Length; i++)
                home[i] = v.IsPrismatic(i) ? v.Lower[i] + (v.Upper[i] - v.Lower[i]) * 0.5 : 0.0;
            home = v.ContinueFrom(start, home);

            PlannedTrajectory plan = KvPlanKit.MakeJointPlan(v,
                features != null ? features.World : null, start, home,
                "ДОМОЙ (с планшета)", 0.08f, 60);
            if (plan == null) return false;
            return flow.PlayExternalPlan(plan, plan.GoalQ, "переезд в домашнюю позу (планшет)");
        }

        private string State()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return "STATE|нет робота";
            Vector3 tcp = flow.Validator.TcpAt(flow.Validator.CopyCurrent());
            string extra = stage3 != null && stage3.Constrained != null && stage3.Constrained.Enabled
                ? "|ограничение активно" : "";
            return "STATE|" + (flow.Robot != null ? flow.Robot.robotName : "-") +
                   "|" + flow.State.phase +
                   "|вариантов " + flow.State.candidates.Count +
                   "|выбран " + (flow.State.selectedTrajectory + 1) +
                   "|TCP " + tcp.x.ToString("0.00") + "," + tcp.y.ToString("0.00") + "," +
                   tcp.z.ToString("0.00") + extra;
        }

        private void Reply(IPEndPoint remote, string text)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(text);
                if (socket != null) socket.Send(data, data.Length, remote);
            }
            catch (Exception) { }
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Companion] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>ВКЛАДКА «СЕТЬ И МОНИТОРИНГ» (ЭТАПЫ 20–22 ТЗ).</summary>
    public class KvNetTab : IKvWorkbenchTab
    {
        private readonly KvCollaborationService collab;
        private readonly KvWebDashboard web;
        private readonly KvCompanionServer companion;
        private KvSegmented roleSegment;

        public KvNetTab(KvCollaborationService collaboration, KvWebDashboard dashboard,
            KvCompanionServer mobile)
        {
            collab = collaboration;
            web = dashboard;
            companion = mobile;
        }

        public string Key { get { return "net"; } }
        public string Title { get { return KvLocExtra3.T("net.title", "Сеть: совместная работа"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra3.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;

            // ---------------------------------------------------------- этап 20
            kit.Section(T("net.title", "Сеть: совместная работа"));
            string[] roles =
            {
                T("net.role.off", "выключено"),
                T("net.role.operator", "оператор (управляет)"),
                T("net.role.observer", "наблюдатель (смотрит)")
            };
            KvNetRole[] order = { KvNetRole.Off, KvNetRole.Operator, KvNetRole.Observer };
            int current = 0;
            for (int i = 0; i < order.Length; i++) if (order[i] == collab.Role) current = i;
            roleSegment = kit.Segmented(T("net.role", "Роль"), roles, current,
                delegate (int i)
                {
                    collab.SetRole(order[Mathf.Clamp(i, 0, order.Length - 1)]);
                });
            kit.Info(delegate { return collab.Status(); }, KvTheme.Accent);

            // ФИКС 11.В: состояние связи отдельной строкой — «связь есть, задержка N мс»
            // либо «связь потеряна» (и тогда наблюдатель чужие позы не применяет).
            kit.Info(delegate { return collab.LinkStatus(); },
                collab.LinkAlive ? KvTheme.Ok : KvTheme.Warn);
            kit.Info(delegate
            {
                return T("net.heartbeat", "Heartbeat") + ": " +
                       collab.HeartbeatInterval.ToString("0.0") + " с · " +
                       T("net.timeout", "порог потери связи") + ": " +
                       collab.TimeoutSeconds.ToString("0.0") + " с · отправлено " +
                       collab.HeartbeatsSent + " · принято " + collab.HeartbeatsReceived;
            }, KvTheme.TextDim);

            kit.Info(delegate { return T("net.port", "Порт") + ": " + collab.Port +
                                       " · " + T("net.host", "Адрес партнёра") + ": " + collab.Peer; },
                KvTheme.TextDim);
            kit.Buttons(new[]
            {
                T("net.connect", "Подключиться") + " (" + collab.Peer + ")",
                T("net.role", "Роль") + " → " + T("net.role.operator", "оператор")
            }, new Action[]
            {
                delegate { collab.SendHello(); },
                delegate
                {
                    collab.SetRole(collab.Role == KvNetRole.Operator
                        ? KvNetRole.Observer : KvNetRole.Operator);
                }
            });
            kit.Note(T("net.info",
                "Обмен состоянием идёт по UDP: оператор рассылает позу робота, состояние State Machine " +
                "и выбранную траекторию, наблюдатель их применяет и видит ту же сцену."), KvTheme.TextDim);
            kit.Note(T("net.heartbeat.info",
                "Связь проверяется пакетом heartbeat: он идёт каждые 0,5 с, партнёр отвечает, и по ответу " +
                "считается задержка. Если пакетов нет дольше порога (2 с), состояние переходит в «связь " +
                "потеряна» — в журнал идёт одна строка, а наблюдатель перестаёт применять чужие позы, " +
                "чтобы робот не выглядел зависшим. С партнёром старой версии (без heartbeat) обмен " +
                "работает как раньше, о чём платформа сообщает один раз. Это по-прежнему синхронизация " +
                "состояния по UDP, а не Netcode/Mirror: блокировки мира, прав и предсказания нет."),
                KvTheme.TextDim);

            // ---------------------------------------------------------- этап 21
            kit.Divider();
            kit.Section(T("web.title", "Веб-дашборд"));
            kit.Buttons(new[]
            {
                web.Running ? T("web.stop", "Остановить сервер") : T("web.start", "Запустить сервер")
            }, new Action[] { delegate { web.Toggle(); } });
            kit.Info(delegate
            {
                return web.Running
                    ? T("web.address", "Адрес страницы") + ": " + web.Address +
                      " · запросов " + web.Requests
                    : T("web.info", "Сервер остановлен");
            }, web.Running ? KvTheme.Ok : KvTheme.TextDim);
            kit.Note(T("web.info",
                "REST-подобный сервер на встроенном сокете: страница мониторинга и JSON с состоянием, " +
                "метриками, здоровьем суставов и текущей траекторией."), KvTheme.TextDim);

            // ---------------------------------------------------------- этап 22
            kit.Divider();
            kit.Section(T("mobile.title", "Мобильный пульт"));
            kit.Toggle(T("mobile.enable", "Принимать команды с планшета (UDP)"),
                companion.Running, delegate (bool v)
                {
                    if (v) companion.Start();
                    else companion.Stop();
                });
            kit.Info(delegate
            {
                // При включении — статус с токеном (его видно сразу, пока он выдан); при
                // выключении — прежняя локализованная строка.
                return companion.Running ? companion.Status() : T("net.role.off", "выключено");
            }, KvTheme.TextMain);
            kit.Note(T("mobile.info",
                "Канал команд: `<токен> PING`, `<токен> STOP`, `<токен> HOME`, `<токен> SELECT 3`, " +
                "`<токен> STATUS`. Токен выдаётся при включении пульта и печатается в журнал и в эту " +
                "строку статуса — команды без него отклоняются с причиной. Ответ — состояние робота. " +
                "Команды выполняются теми же обработчиками, что кнопки интерфейса."), KvTheme.TextDim);
        }

        public void Tick() { }

        public void Refresh()
        {
            if (roleSegment == null) return;
            int current = 0;
            switch (collab.Role)
            {
                case KvNetRole.Operator: current = 1; break;
                case KvNetRole.Observer: current = 2; break;
                default: current = 0; break;
            }
            if (roleSegment.Index != current) roleSegment.Set(current);
        }
    }
}
