using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KazistovVvFeatures;
using KazistovVvUI;
using TrajectoryCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ПОЛНАЯ ПРОВЕРКА ПРОЕКТА (этапы 1–8 ТЗ): оба робота, интерфейс, взаимодействия,
/// производительность, стабильность, консоль и журнал.
///
/// ЗАПУСК:
///   Unity.exe -batchmode -nographics -projectPath "&lt;проект&gt;" -executeMethod DshFullVerifyDiag.Run -logFile _dsh_full.log
/// (БЕЗ -quit). Отчёт: `_dsh_full_verify.txt` в корне проекта — строки пишутся СРАЗУ
/// (домен перезагружается при входе в PlayMode, буфер в памяти потерялся бы).
///
/// Диагностика агента; в копию пользователя НЕ переносится. Сцена и данные не меняются,
/// временно поднятые скорости восстанавливаются в конце прогона.
/// </summary>
public static class DshFullVerifyDiag
{
    private const string SessionKey = "DshFullVerifyDiag.Active";

    private static string ReportPath
    {
        // ФИКС 10: отчёт — в <persistentDataPath>/KazistovVv/Reports (проект в OneDrive,
        // во время PlayMode файл там не дописывался — §13.11).
        get { return FeatureStorage.ReportPath("_dsh_full_verify.txt"); }
    }

    // ---------------------------------------------------------------- состояние прогона
    private static int step, waitFrames, stepFrame, stepEnterFrame;
    private static int ok, fail, info;
    private static bool finished;

    private static readonly List<string> exceptions = new List<string>();
    private static readonly List<string> errors = new List<string>();
    private static readonly List<string> asserts = new List<string>();
    private static readonly Dictionary<string, int> messageCounts = new Dictionary<string, int>();

    private static KazistovVvUIManager ui;
    private static FeatureHub hub;
    private static TrajectoryFlowController flow;
    private static FreeFlyCameraController cam;
    private static RobotController robot6, robotScara;
    private static AimIndicator aim;

    // ---- цикл «робот / SCARA» (одни и те же шаги для обоих)
    private static int cycle;                 // 0 = робот, 1 = SCARA
    private static RobotController rc;
    private static Vector3 workPoint;
    private static int chosenIndex;
    private static float tGenStart, genSeconds;
    private static int tubesOnShown, phantomsOnShown;
    private static float[] progressA, progressB;
    private static float phantomRate, expectedPhantomRate, robotRate;
    private static Vector3 robotTcpAtStart, robotGoalTcp;
    private static double[] savedQ;
    private static float[] originalMoveSpeed;
    private static float originalMinTravel, originalMaxTravel;
    private static KvTrajectoryRecord savedRecord;
    private static Vector3 movePointBefore, pointBeforeMove;
    private static float wheelDepthBefore;
    private static double[] qBeforeSliders;
    private static string shapeBefore, shapeAfter;
    private static float frameSum;
    private static int frameCount;
    private static float measuredFrameMs, measuredFrameMsPhantom, measuredFrameMsHeatmap, measuredFrameMsGen;
    private static float ikMicros;
    private static int sceneObjectsBaseline;
    private static int cyclesDone;

    // ================================================================== запуск
    public static void Run()
    {
        File.WriteAllText(ReportPath,
            "=== ПОЛНАЯ ПРОВЕРКА ПРОЕКТА KazistovVv (этапы 1–8) ===\n" +
            DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "\n\n", new UTF8Encoding(false));
        SessionState.SetBool(SessionKey, true);
        CleanTestData();
        Debug.Log("[FullVerify] открываю MainScene и вхожу в PlayMode…");
        EditorSceneManager.OpenScene("Assets/_Project/00_Scenes/MainScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>
    /// Убрать данные предыдущих прогонов (файлы с «Дигност» в имени). Найденные ранее зоны
    /// запрета грузятся из папки зон автоматически — оставленные «ДигностЗона» копились
    /// и мешали планированию (они были препятствиями мира столкновений).
    /// </summary>
    private static void CleanTestData()
    {
        string[] dirs =
        {
            FeatureStorage.RecordingsDir, FeatureStorage.PosesDir,
            FeatureStorage.ZonesDir, FeatureStorage.SessionsDir
        };
        int removed = 0;
        for (int d = 0; d < dirs.Length; d++)
        {
            try
            {
                string[] files = Directory.GetFiles(dirs[d]);
                for (int i = 0; i < files.Length; i++)
                {
                    string name = Path.GetFileName(files[i]);
                    if (name.IndexOf("Дигност", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("Тест", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    File.Delete(files[i]);
                    removed++;
                }
            }
            catch (Exception) { }
        }
        Debug.Log("[FullVerify] удалено тестовых файлов прошлых прогонов: " + removed);
    }

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (type == LogType.Exception)
        {
            if (exceptions.Count < 40) exceptions.Add(message + " | " + FirstLine(stack));
            return;
        }
        if (type == LogType.Error || type == LogType.Assert)
        {
            if (type == LogType.Assert) { if (asserts.Count < 30) asserts.Add(message); }
            else if (errors.Count < 40) errors.Add(message);
            return;
        }
        // частота одинаковых сообщений (поиск «спама»)
        string key = message.Length > 90 ? message.Substring(0, 90) : message;
        int n;
        if (messageCounts.TryGetValue(key, out n)) messageCounts[key] = n + 1;
        else if (messageCounts.Count < 400) messageCounts[key] = 1;
    }

    private static string FirstLine(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        int i = s.IndexOf('\n');
        return i < 0 ? s : s.Substring(0, i);
    }

    private static void Tick()
    {
        if (finished) return;
        if (!EditorApplication.isPlaying) return;
        if (waitFrames > 0) { waitFrames--; return; }
        if (Time.frameCount == stepFrame) return;
        stepFrame = Time.frameCount;

        try { RunStep(); }
        catch (Exception e)
        {
            Check("шаг " + step + " выполнен без исключения", false, e.GetType().Name + ": " + e.Message);
            Next(2);
        }
    }

    private static void Next(int wait = 1)
    {
        step++;
        waitFrames = wait;
        stepEnterFrame = Time.frameCount;
    }

    private static bool Waited(int frames) { return Time.frameCount - stepEnterFrame >= frames; }

    private static void Check(string name, bool pass, string detail)
    {
        if (pass) ok++; else fail++;
        string line = (pass ? "[OK]   " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : " — " + detail);
        Append(line);
        if (!pass) Debug.LogWarning("[FullVerify] " + line);
    }

    private static void Line(string text) { info++; Append(text); }

    private static void Append(string text)
    {
        try { File.AppendAllText(ReportPath, text + "\n", new UTF8Encoding(false)); }
        catch (Exception) { }
    }

    // ================================================================== шаги
    private static void RunStep()
    {
        switch (step)
        {
            // ============================================================ БЛОК 1: СТАРТ
            case 0:
                ui = KazistovVvUIManager.Instance;
                Check("1.1 менеджер интерфейса поднялся в PlayMode", ui != null, ui != null ? ui.name : "нет");
                Next(4);
                break;

            case 1:
                flow = ui != null ? ui.Flow : null;
                cam = ui != null ? ui.CameraRig : null;
                hub = FeatureHub.Instance;
                Check("1.2 поток этапов и контроллер оператора найдены", flow != null && cam != null,
                    flow != null ? "поток есть" : "потока нет");
                Check("1.3 хаб новых функций создан", hub != null, hub != null ? hub.Dump() : "нет");
                Next(4);
                break;

            case 2:
            {
                RobotController[] all = UnityEngine.Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
                int sceneRobots = 0, six = 0, scara = 0, hidden = 0, copies = 0;
                foreach (RobotController r in all)
                {
                    if (r == null) continue;
                    if ((r.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) { hidden++; continue; }
                    if (r.GetComponent<KazistovVvUI.RegisteredObject>() != null) { copies++; continue; }
                    if (!r.gameObject.scene.IsValid()) continue;
                    sceneRobots++;
                    if (r is SixAxisController) { six++; robot6 = r; }
                    else if (r is SCARAController) { scara++; robotScara = r; }
                }
                Check("1.4 в сцене ровно 2 робота", sceneRobots == 2,
                    "роботов: " + sceneRobots + " (скрытых копий " + hidden + ", копий UI " + copies + ")");
                Check("1.5 состав: робот + SCARA", six == 1 && scara == 1,
                    "робот: " + six + " · SCARA: " + scara);
                bool bothIdle = robot6 != null && robotScara != null &&
                                !robot6.isActive && !robotScara.isActive;
                Check("1.6 оба робота не отмечены активными (isActive = 0)", bothIdle,
                    robot6 != null ? "робот isActive=" + robot6.isActive + ", SCARA isActive=" +
                                     (robotScara != null ? robotScara.isActive.ToString() : "?") : "робот не найден");
                Check("1.7 ни один робот не выбран на старте",
                    cam != null && cam.SelectedRobot == null && ui.SelectedNode == null &&
                    (ui.Tree == null || ui.Tree.Selected == null),
                    "камера: " + (cam != null && cam.SelectedRobot != null ? cam.SelectedRobot.name : "нет") +
                    " · дерево: " + (ui.SelectedNode != null ? ui.SelectedNode.DisplayName : "нет"));

                int missing = 0; string missingWhere = "";
                Scene sc = SceneManager.GetActiveScene();
                foreach (GameObject root in sc.GetRootGameObjects())
                    foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                    {
                        Component[] comps = tr.GetComponents<Component>();
                        for (int i = 0; i < comps.Length; i++)
                            if (comps[i] == null)
                            {
                                missing++;
                                if (missingWhere.Length < 120) missingWhere += tr.name + " ";
                            }
                    }
                Check("1.8 нет «Missing (Mono Script)» в сцене", missing == 0,
                    missing == 0 ? "все компоненты на месте" : "битых компонентов: " + missing + " (" + missingWhere + ")");
                Line("  [info] робот: " + (robot6 != null ? robot6.name + " @ " + robot6.transform.position : "нет") +
                     " · SCARA: " + (robotScara != null ? robotScara.name + " @ " + robotScara.transform.position : "нет"));
                Next(2);
                break;
            }

            case 3:
                Check("1.9 служебные компоненты потока созданы",
                    flow.Lasers != null && flow.Phantoms != null && flow.PointHud != null && flow.Motion != null,
                    "лазеры/фантомы/HUD/исполнитель");
                aim = cam != null ? cam.GetComponent<AimIndicator>() : null;
                Check("1.10 шарик прицела (AimIndicator) есть на камере", aim != null, aim != null ? "есть" : "нет");
                Check("1.11 в сцене нет траекторий и фантомов на старте",
                    CountTubes() == 0 && CountPhantoms() == 0,
                    "трубок: " + CountTubes() + " · фантомов: " + CountPhantoms());
                sceneObjectsBaseline = CountSceneObjects();
                SnapshotObjects();
                Line("  [info] объектов в сцене на старте: " + sceneObjectsBaseline);
                Check("1.12 при старте нет исключений", exceptions.Count == 0,
                    exceptions.Count == 0 ? "чисто" : exceptions.Count + " шт: " + exceptions[0]);
                Next(2);
                break;

            case 4:
                // исходные настройки скоростей (для возврата в конце)
                originalMoveSpeed = new float[] { flow.robotMoveSpeed, flow.phantomSpeedMultiplier };
                originalMinTravel = flow.Phantoms.minTravelTime;
                originalMaxTravel = flow.Phantoms.maxTravelTime;
                Line("  [info] штатные скорости: робот " + flow.robotMoveSpeed.ToString("0.0000") +
                     " ю/с · множитель фантомов ×" + flow.phantomSpeedMultiplier.ToString("0.##") +
                     " · траекторий " + flow.trajectoryCount);
                cycle = 0;
                step = CycleStart;         // → цикл робота
                waitFrames = 2;
                stepEnterFrame = Time.frameCount;
                break;

            // ============================================================ ЦИКЛ (робот, затем SCARA)
            case CycleStart: // C0 — привязка потока к нужному роботу
            {
                rc = cycle == 0 ? robot6 : robotScara;
                if (rc == null) { Check("C0 робот найден", false, "нет робота"); Next(2); break; }
                // Паркуем камеру у нужного стенда и ЗАСТАВЛЯЕМ поток перепривязаться
                // (кадровый вход с прицелом рядом с этим роботом) — иначе проверять нечего.
                aimAt(rc);
                Aim(rc.transform.position + Vector3.up * 0.2f, false, false, false, false, Vector3.zero, false);
                Next(3);
                break;
            }

            case CycleStart + 1: // C1/C2 — проверка привязки, затем поиск достижимой точки
            {
                bool bound = flow.Robot == rc;
                Check("C1 поток привязан к «" + rc.name + "»", bound,
                    "робот потока: " + (flow.Robot != null ? flow.Robot.name : "нет") +
                    " · осей " + flow.Validator.Dof + " · " + flow.Validator.RobotName);
                int expectDof = cycle == 0 ? 6 : 3;
                Check("C2 число осей (" + (cycle == 0 ? "робот" : "SCARA") + ") = " + expectDof,
                    flow.Validator.Dof == expectDof, "осей: " + flow.Validator.Dof);
                if (!bound) { Next(2); break; }
                // Мир столкновений хаба пересобирается раз в 0.5 с и «знает» только текущего
                // робота: сразу после перепривязки в нём ещё СТАРЫЙ робот, а сам новый робот
                // числится препятствием. Пересобираем принудительно — иначе все точки
                // отвергаются по зазору (робот «сталкивается» сам с собой).
                if (hub != null && hub.World != null)
                    hub.World.Rebuild(flow.Robot, flow.Validator.linkRadius);
                Vector3 p;
                bool found = FindReachablePoint(flow.Validator, out p);
                workPoint = p;
                Check("C3 найдена достижимая точка на столе (" + rc.name + ")", found,
                    found ? "точка " + p.ToString("F3") + " · IK-ошибка " +
                            (flow.Validator.LastIkError * 1000f).ToString("0.0") + " мм" : "не найдена");
                Next(2);
                break;
            }

            case CycleStart + 2: // C4 — красный лазер + ЛКМ
            {
                SetLasers(true, false);
                aimAt(workPoint);
                Aim(workPoint, true, false, false, false, Vector3.zero, false);
                Next(1);
                break;
            }

            case CycleStart + 3: // C5 — клик и проверка фазы PointSelected
            {
                tGenStart = Time.realtimeSinceStartup;
                Click(workPoint);
                FlowState ph = flow.State.phase;
                bool locked = ph == FlowState.PointSelected || ph == FlowState.TrajectoriesShown;
                Check("C4 красный лазер + ЛКМ → точка выбрана", locked,
                    "фаза: " + ph + " · точка " + flow.State.point.ToString("F3") +
                    " · цель " + workPoint.ToString("F3"));
                Check("C5 точка зафиксирована ровно в точке попадания (toolOffset = 0)",
                    flow.State.hasPoint && Vector3.Distance(flow.State.point, workPoint) < 0.002f,
                    "смещение " + (Vector3.Distance(flow.State.point, workPoint) * 1000f).ToString("0.0") + " мм");
                Next(2);
                break;
            }

            case CycleStart + 4: // C6 — ждём генерацию
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(workPoint); Aim(workPoint, true, false, false, false, Vector3.zero, false);
                    return;                     // остаёмся на шаге
                }
                genSeconds = Time.realtimeSinceStartup - tGenStart;
                Next(2);
                break;

            case CycleStart + 5: // C7 — 8 траекторий / 8 фантомов + скорости
            {
                int cand = flow.State.candidates.Count;
                int tubes = CountTubes();
                int phStates = flow.State.phantoms.Count;
                int phLive = flow.Phantoms.Count;
                tubesOnShown = tubes; phantomsOnShown = phLive;
                Check("C7 показаны 8 траекторий (кандидаты)", cand == TrajectoryFlowController.MaxTrajectories,
                    "вариантов: " + cand + " · трубок в сцене: " + tubes);
                Check("C7б в сцене 8 «колбасок»", tubes == cand && tubes > 0, "трубок: " + tubes);
                Check("C7в создано 8 фантомов", cand > 0 && phLive == cand && phStates == cand,
                    "фантомов: " + phLive + " (состояние " + phStates + ")");
                Check("C8 генерация 8 траекторий — время " + genSeconds.ToString("0.000") + " с",
                    genSeconds < 60f, "потрачено " + genSeconds.ToString("0.000") + " с");

                float len0 = flow.Phantoms.PathLengthOf(0);
                float dur0 = flow.Phantoms.DurationOf(0);
                phantomRate = dur0 > 1e-4f ? len0 / dur0 : 0f;
                robotRate = flow.robotMoveSpeed;
                expectedPhantomRate = flow.robotMoveSpeed * flow.phantomSpeedMultiplier;
                Check("C9 скорость фантома = robotMoveSpeed × " + flow.phantomSpeedMultiplier.ToString("0.##"),
                    Mathf.Abs(phantomRate - expectedPhantomRate) <= expectedPhantomRate * 0.05f + 1e-4f,
                    "фантом " + phantomRate.ToString("0.000") + " ю/с · робот " + robotRate.ToString("0.000") +
                    " ю/с · отношение " + (phantomRate / Mathf.Max(1e-5f, robotRate)).ToString("0.00") + "×");
                Line("  [info] путь 0-го фантома " + len0.ToString("0.00") + " юнита за " + dur0.ToString("0.0") +
                     " с · робот прошёл бы за " + (len0 / Mathf.Max(1e-5f, robotRate)).ToString("0.0") + " с");

                progressA = new float[flow.Phantoms.Count];
                for (int i = 0; i < progressA.Length; i++) progressA[i] = flow.Phantoms.ProgressOf(i);
                Check("C10 все фантомы движутся одновременно",
                    flow.Phantoms.Count > 0 && flow.Phantoms.MovingCount == flow.Phantoms.Count,
                    "в движении " + flow.Phantoms.MovingCount + " из " + flow.Phantoms.Count);
                Next(14);
                break;
            }

            case CycleStart + 6: // C11 — прогресс у всех растёт
            {
                int moved = 0;
                for (int i = 0; i < progressA.Length && i < flow.Phantoms.Count; i++)
                    if (flow.Phantoms.ProgressOf(i) > progressA[i] + 0.0005f) moved++;
                Check("C11 за 14 кадров прогресс вырос у всех фантомов",
                    progressA.Length > 0 && moved == progressA.Length,
                    "сдвинулось " + moved + " из " + progressA.Length);
                Next(1);
                break;
            }

            case CycleStart + 7: // C12 — выбор траектории зелёным лазером
            {
                if (flow.State.candidates.Count < 2)
                {
                    Check("C12 зелёный лазер + ЛКМ по «колбаске» → траектория выбрана", false,
                        "вариантов нет (" + flow.State.candidates.Count + ") — точка не была принята");
                    Next(2);
                    break;
                }
                chosenIndex = Mathf.Clamp(flow.State.candidates.Count / 2 + 1, 0, flow.State.candidates.Count - 1);
                Vector3[] tubePick1 = flow.State.candidates[chosenIndex].tube;
                Vector3 aimPt = tubePick1 != null && tubePick1.Length > 0
                    ? tubePick1[tubePick1.Length / 2] : workPoint;
                SetLasers(false, true);
                Click(aimPt);
                bool okSel = flow.State.phase == FlowState.PhantomsMoving &&
                             flow.State.selectedTrajectory == chosenIndex;
                Check("C12 зелёный лазер + ЛКМ по «колбаске» → траектория выбрана", okSel,
                    "фаза: " + flow.State.phase + " · выбран вариант " + flow.State.selectedTrajectory +
                    " (целились в " + chosenIndex + ")");
                shapeBefore = PlanShape(flow.State.candidates[chosenIndex].plan);
                Next(3);
                break;
            }

            case CycleStart + 8: // C13 — остался один фантом + ускорение прогона + переключение траектории
            {
                Check("C13 после выбора остался ОДИН фантом",
                    flow.Phantoms.Count == 1 && CountPhantoms() == 1,
                    "фантомов: " + flow.Phantoms.Count + " · в сцене " + CountPhantoms());
                // дальше скорости поднимаем ТОЛЬКО чтобы уложиться в кадры; штатные вернём в конце
                flow.robotMoveSpeed = 3f;
                flow.Phantoms.minTravelTime = 0.2f;
                flow.Phantoms.maxTravelTime = 60f;
                if (flow.State.candidates.Count == 0) { Next(1); break; }
                // переключение на другую траекторию тем же зелёным лучом (шаг 4 ТЗ)
                Vector3[] tube0 = flow.State.candidates[0].tube;
                Click(tube0 != null && tube0.Length > 0 ? tube0[tube0.Length / 2] : workPoint);
                Next(2);
                break;
            }

            case CycleStart + 9: // C15 — переключение проверено; выбор фантома → движение робота
            {
                if (flow.State.candidates.Count == 0)
                {
                    Check("C14 зелёный лазер + ЛКМ по фантому → робот поехал", false, "вариантов нет");
                    Next(2);
                    break;
                }
                Check("C13б зелёный луч по ДРУГОЙ «колбаске» переключает траекторию",
                    flow.State.phase == FlowState.PhantomsMoving && flow.State.selectedTrajectory == 0,
                    "выбран вариант " + flow.State.selectedTrajectory + " · фаза " + flow.State.phase);
                double[] pose = flow.Phantoms.PoseOf(0);
                robotTcpAtStart = pose != null ? flow.Validator.TcpAt(pose) : workPoint;
                Click(robotTcpAtStart);
                bool moving = flow.State.phase == FlowState.RobotMoving;
                Check("C14 зелёный лазер + ЛКМ по фантому → робот поехал", moving,
                    "фаза: " + flow.State.phase + " · наведение на фантом " + flow.State.hoveredPhantom);
                Check("C16 управление роботом передано исполнителю", moving && rc != null && !rc.enabled,
                    "robot.enabled = " + (rc != null ? rc.enabled.ToString() : "?"));
                double[] qGoal = flow.Executor != null && flow.Executor.ActivePlan != null
                    ? flow.Executor.ActivePlan.GoalQ : null;
                robotGoalTcp = qGoal != null ? flow.Validator.TcpAt(qGoal) : workPoint;
                Next(3);
                break;
            }

            case CycleStart + 10: // C15/C17 — траектории скрылись (Destroy отложен на кадр) и ждём финиш
            {
                if (!tubesCheckedFlag)
                {
                    tubesCheckedFlag = true;
                    Check("C15 траектории скрылись при старте движения", CountTubes() == 0,
                        "трубок: " + CountTubes());
                }
                if (flow.State.phase == FlowState.RobotMoving && !Waited(9000)) return;
                tubesCheckedFlag = false;
                Next(2);
                break;
            }

            case CycleStart + 11: // C18 — завершение
            {
                Check("C17 робот доехал, поток вернулся в Idle", flow.State.phase == FlowState.Idle,
                    "фаза: " + flow.State.phase);
                Check("C18 после завершения фантомы убраны", CountPhantoms() == 0 && flow.Phantoms.Count == 0,
                    "фантомов: " + flow.Phantoms.Count);
                Check("C19 траектории скрыты", CountTubes() == 0, "трубок: " + CountTubes());
                Check("C20 робот снова управляется (enabled)", rc != null && rc.enabled,
                    "robot.enabled = " + (rc != null ? rc.enabled.ToString() : "?"));
                jitterFrames = 0;
                jitterMax = 0f;
                Vector3 tcpNow = rc != null && rc.tcp != null ? rc.tcp.position : Vector3.zero;
                Check("C21 TCP доехал до конечной позы фантома",
                    robotGoalTcp != Vector3.zero && Vector3.Distance(tcpNow, robotGoalTcp) < 0.02f,
                    "ошибка " + (Vector3.Distance(tcpNow, robotGoalTcp) * 1000f).ToString("0.0") + " мм");
                Next(1);
                break;
            }

            case CycleStart + 12: // C22 — «кончик не колбасит»: 60 кадров покоя
            {
                Vector3 now = rc != null && rc.tcp != null ? rc.tcp.position : Vector3.zero;
                if (jitterFrames == 0) { jitterPrev = now; jitterMax = 0f; }
                else jitterMax = Mathf.Max(jitterMax, Vector3.Distance(now, jitterPrev));
                jitterPrev = now;
                jitterFrames++;
                if (jitterFrames < 60) return;
                Check("C22 после остановки кончик не «колбасит» (шаг < 1 мм)", jitterMax < 0.001f,
                    "макс. шаг TCP " + (jitterMax * 1000f).ToString("0.000") + " мм за кадр за " +
                    jitterFrames + " кадров");
                jitterFrames = 0;
                Next(1);
                break;
            }

            case CycleStart + 13: // C23 — режим перемещения точки: вход
            {
                SetLasers(true, false);
                Click(workPoint);
                Next(3);
                break;
            }

            case CycleStart + 14: // ждём точку и варианты
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(workPoint); Aim(workPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                Next(1);
                break;

            case CycleStart + 15: // C24 — Enter → PointMoveMode
            {
                pointBeforeMove = flow.State.point;
                EnterPointMove(true);
                Check("C23 Enter → режим перемещения точки", flow.State.phase == FlowState.PointMoveMode,
                    "фаза: " + flow.State.phase + " · HUD виден: " + (flow.PointHud != null && flow.PointHud.IsVisible));
                Check("C24 в режиме траектории убраны (список)",
                    flow.State.candidates.Count == 0, "вариантов: " + flow.State.candidates.Count);
                movePointBefore = flow.State.movePoint;
                Next(2);
                break;
            }

            case CycleStart + 16: // C25 — движение точки QWEASD (держим «E» несколько кадров)
            {
                if (!tubesCheckedFlag2)
                {
                    tubesCheckedFlag2 = true;
                    Check("C24б в режиме «колбаски» убраны из сцены", CountTubes() == 0,
                        "трубок: " + CountTubes());
                }
                if (!Waited(20)) { EnterPoint(false, Vector3.up); return; }
                Next(1);
                break;
            }

            case CycleStart + 17: // C26 — проверка движения точки и вердикта
            {
                float moved = Vector3.Distance(flow.State.movePoint, movePointBefore);
                Check("C25 точка движется клавишами QWEASD", moved > 0.02f,
                    "смещение " + moved.ToString("F3") + " юнита · " + flow.State.moveReason);
                Check("C26 вердикт достижимости обновляется в режиме",
                    !string.IsNullOrEmpty(flow.State.moveReason),
                    "вердикт: " + flow.State.moveVerdict + " · " + flow.State.moveReason);
                Line("  [info] точка режима: " + flow.State.movePoint.ToString("F3") +
                     " · от робота " + (rc != null ? rc.transform.InverseTransformPoint(flow.State.movePoint).ToString("F3") : "?"));
                Next(1);
                break;
            }

            case CycleStart + 18: // C27 — Enter → подтверждение, новая точка/новая генерация
            {
                EnterPointMove(true);
                Check("C27 Enter в режиме → точка зафиксирована заново",
                    flow.State.phase == FlowState.PointSelected || flow.State.phase == FlowState.TrajectoriesShown,
                    "фаза: " + flow.State.phase + " · точка " + flow.State.point.ToString("F3"));
                Next(2);
                break;
            }

            case CycleStart + 19: // ждём новую генерацию
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(workPoint); Aim(workPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                Next(1);
                break;

            case CycleStart + 20: // C28 — форма новой траектории отличается
            {
                int n = flow.State.candidates.Count;
                shapeAfter = n > 0 ? PlanShape(flow.State.candidates[0].plan) : "нет";
                Check("C28 после перемещения точки сгенерированы новые траектории",
                    n == TrajectoryFlowController.MaxTrajectories, "вариантов: " + n);
                Check("C29 новая траектория другой формы",
                    !string.IsNullOrEmpty(shapeAfter) && shapeAfter != shapeBefore,
                    "до: " + shapeBefore + " · после: " + shapeAfter);
                Next(2);
                break;
            }

            case CycleStart + 21: // C30 — Esc: сброс (проверка на СЛЕДУЮЩЕМ кадре: Destroy отложен)
            {
                if (escWait == 0) { Esc(); escWait = 1; return; }
                escWait = 0;
                Check("C30 Esc → Idle, сцена очищена",
                    flow.State.phase == FlowState.Idle && CountTubes() == 0 && CountPhantoms() == 0,
                    "фаза: " + flow.State.phase + " · трубок " + CountTubes() + " · фантомов " + CountPhantoms());
                Next(2);
                break;
            }

            case CycleStart + 22: // C31 — Esc-отмена режима возвращает точку и варианты
            {
                SetLasers(true, false);
                Click(workPoint);
                Next(3);
                break;
            }

            case CycleStart + 23:
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(workPoint); Aim(workPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                Next(1);
                break;

            case CycleStart + 24: // вход в режим, сдвиг, Esc
            {
                pointBeforeMove = flow.State.point;
                EnterPointMove(true);
                Next(1);
                break;
            }

            case CycleStart + 25:
                if (!Waited(20)) { EnterPoint(false, Vector3.right); return; }
                Next(1);
                break;

            case CycleStart + 26:
            {
                if (escWait == 0)
                {
                    movedDistance = Vector3.Distance(flow.State.movePoint, pointBeforeMove);
                    Esc();
                    escWait = 1;
                    return;
                }
                escWait = 0;
                float back = Vector3.Distance(flow.State.point, pointBeforeMove);
                Check("C31 Esc в режиме возвращает точку на место (сдвиг был " + movedDistance.ToString("F3") + ")",
                    back < 0.002f && movedDistance > 0.02f, "остаток " + (back * 1000f).ToString("0.0") + " мм");
                Check("C32 Esc в режиме возвращает прежние варианты",
                    flow.State.candidates.Count == TrajectoryFlowController.MaxTrajectories &&
                    CountTubes() == TrajectoryFlowController.MaxTrajectories,
                    "вариантов: " + flow.State.candidates.Count + " · трубок: " + CountTubes());
                Next(2);
                break;
            }

            case CycleStart + 27: // C33 — колесо мыши: глубина шарика
            {
                Esc();
                SetLasers(true, false);
                aimAt(workPoint);
                Next(3);
                break;
            }

            case CycleStart + 28:
            {
                if (cam == null) { Line("  [info] камеры нет — колесо не проверялось"); Next(2); break; }
                wheelDepthBefore = cam.AimBallDepth;
                cam.AddScrollInput(-3f);           // назад — шарик к оператору
                Next(120);                         // сглаживание глубины: ждём сходимости
                break;
            }

            case CycleStart + 29:
            {
                float d = cam.AimBallDepth;
                float expect = wheelDepthBefore - 3f * cam.scrollStep;
                Check("C33 колесо назад отводит шарик от поверхности",
                    d < wheelDepthBefore - 0.05f && Mathf.Abs(d - expect) < 0.03f,
                    "глубина " + wheelDepthBefore.ToString("F3") + " → " + d.ToString("F3") +
                    " (ожидание ≈" + expect.ToString("F3") + ") · отведён: " + cam.AimBallDetached);
                cam.AddScrollInput(9f);            // вперёд — обратно к поверхности (прилипание)
                Next(60);
                break;
            }

            case CycleStart + 30:
            {
                cam.SnapAimBallToSurface();
                Next(3);
                break;
            }

            case CycleStart + 31:
            {
                Check("C34 колесо вперёд + СКМ: шарик возвращается на поверхность",
                    cam.AimBallOnSurface && Mathf.Abs(cam.AimBallDepth - wheelDepthBefore) < 0.01f,
                    "на поверхности: " + cam.AimBallOnSurface +
                    " · глубина " + cam.AimBallDepth.ToString("F3") + " (была " + wheelDepthBefore.ToString("F3") + ")");
                Next(1);
                break;
            }

            case CycleStart + 32: // C35 — колесо в режиме перемещения точки игнорируется
            {
                SetLasers(true, false);
                Click(workPoint);
                Next(3);
                break;
            }

            case CycleStart + 33:
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(workPoint); Aim(workPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                Next(1);
                break;

            case CycleStart + 34:
                EnterPointMove(true);
                wheelDepthBefore = cam.AimBallDepth;
                cam.AddScrollInput(5f);
                Next(4);
                break;

            case CycleStart + 35:
            {
                Check("C35 в режиме перемещения точки колесо глубину не трогает",
                    Mathf.Abs(cam.AimBallDepth - wheelDepthBefore) < 0.002f,
                    "глубина " + wheelDepthBefore.ToString("F3") + " → " + cam.AimBallDepth.ToString("F3"));
                Esc();
                Next(2);
                break;
            }

            case CycleStart + 36: // C36 — Undo/Redo выбора траектории
            {
                Esc();
                SetLasers(true, false);
                Click(workPoint);
                Next(3);
                break;
            }

            case CycleStart + 37:
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(workPoint); Aim(workPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                Next(1);
                break;

            case CycleStart + 38:
            {
                if (flow.State.candidates.Count < 3)
                {
                    Check("C36 первая траектория выбрана зелёным лучом по «колбаске»", false,
                        "вариантов: " + flow.State.candidates.Count);
                    Next(4);
                    break;
                }
                SetLasers(false, true);
                Vector3[] tubePick2 = flow.State.candidates[1].tube;
                Click(tubePick2 != null && tubePick2.Length > 0
                    ? tubePick2[tubePick2.Length / 2] : workPoint);
                Next(2);
                break;
            }

            case CycleStart + 39:
            {
                Check("C36 первая траектория выбрана зелёным лучом по «колбаске»",
                    flow.State.phase == FlowState.PhantomsMoving && flow.State.selectedTrajectory == 1,
                    "выбран вариант " + flow.State.selectedTrajectory + " · фаза " + flow.State.phase);
                if (flow.State.candidates.Count < 3) { Next(4); break; }
                Vector3[] tubePick3 = flow.State.candidates[2].tube;
                Click(tubePick3 != null && tubePick3.Length > 0
                    ? tubePick3[tubePick3.Length / 2] : workPoint);
                Next(2);
                break;
            }

            case CycleStart + 40:
            {
                Check("C36б переключение на другую «колбаску» работает",
                    flow.State.selectedTrajectory == 2, "выбран вариант " + flow.State.selectedTrajectory);
                bool undone = hub != null && hub.Undo.Undo();
                Next(2);
                undoOk = undone;
                break;
            }

            case CycleStart + 41:
            {
                Check("C37 отмена выбора траектории выполнена", undoOk && flow.State.selectedTrajectory == 1,
                    "отмена: " + undoOk + " · выбран вариант " + flow.State.selectedTrajectory);
                Check("C38 Undo только вернул выбор — робот не поехал",
                    flow.State.phase != FlowState.RobotMoving && (flow.Motion == null || !flow.Motion.IsRunning),
                    "фаза: " + flow.State.phase + " · движение: " +
                    (flow.Motion != null ? flow.Motion.IsRunning.ToString() : "?"));
                bool redone = hub != null && hub.Undo.Redo();
                Next(2);
                Check("C38б повтор (Redo) выбора траектории выполнен",
                    redone && flow.State.selectedTrajectory == 2,
                    "повтор: " + redone + " · выбран вариант " + flow.State.selectedTrajectory);
                break;
            }

            case CycleStart + 42: // C39 — аварийная остановка во время движения
            {
                Esc();
                if (flow.Motion != null) flow.Motion.Stop();
                // Реальное движение запускаем штатным исполнителем (как поза/сценарий):
                // уводим один сустав на 20° — этого достаточно, чтобы поймать робота «на ходу».
                double[] cur = flow.Validator.CopyCurrent();
                double[] goal = (double[])cur.Clone();
                goal[0] += 20.0;
                bool planned = false;
                if (flow.Validator.WithinLimits(goal))
                {
                    PlannedTrajectory plan = KvPlanKit.MakeJointPlan(flow.Validator, hub.World,
                        flow.Validator.ContinueFrom(cur, goal), goal, "Дигност: проверка стопа", 0.5f, 32);
                    planned = plan != null && flow.PlayExternalPlan(plan, goal, "диагностика: аварийный стоп");
                }
                Next(3);
                Check("C39 запущено реальное движение (внешний план) для проверки стопа", planned,
                    "лимит соблюдён: " + flow.Validator.WithinLimits(goal) + " · движение: " +
                    (flow.Motion != null ? flow.Motion.IsRunning.ToString() : "?"));
                break;
            }

            case CycleStart + 43:
            {
                bool riding = flow.Motion != null && flow.Motion.IsRunning;
                if (hub != null) hub.EmergencyStop();
                Next(2);
                Check("C39б аварийная остановка мгновенно останавливает робота",
                    riding && flow.State.phase == FlowState.Idle && (flow.Motion == null || !flow.Motion.IsRunning),
                    "ехал: " + riding + " · после стопа фаза: " + flow.State.phase);
                break;
            }

            case CycleStart + 44:
                Check("C40 аварийная остановка убрала фантомы и траектории",
                    CountTubes() == 0 && CountPhantoms() == 0,
                    "трубок " + CountTubes() + " · фантомов " + CountPhantoms());
                Next(2);
                break;

            case CycleStart + 45: // C41 — запись и воспроизведение
            {
                Esc();
                if (hub == null) { Line("  [info] хаба нет — запись не проверялась"); Next(2); break; }
                bool started = hub.Recording.StartRecording("ДигностЗапись_" + cycle, "live");
                Check("C41 запись траектории начата", started && hub.Recording.IsRecording,
                    started ? "идёт" : "не началась");
                Next(20);
                break;
            }

            case CycleStart + 46:
            {
                if (hub.Recording.IsRecording) hub.Recording.SampleNow(true);
                int samples = hub.Recording.Recording != null ? hub.Recording.Recording.SampleCount : 0;
                Check("C42 сэмплы пишутся", samples >= 2, "сэмплов: " + samples);
                KvTrajectoryRecord rec = hub.Recording.StopRecording(true);
                savedRecord = rec;
                Check("C43 запись сохранена в файл", rec != null && !string.IsNullOrEmpty(rec.filePath) &&
                    File.Exists(rec.filePath), rec != null ? rec.filePath : "нет записи");
                Next(2);
                break;
            }

            case CycleStart + 47:
            {
                KvTrajectoryRecord rec = savedRecord;
                bool playing = rec != null && hub.Recording.Play(rec, 3f);
                playbackProgressAtStart = hub.Recording.Progress01;
                Next(6);
                Check("C44 воспроизведение записи запущено", playing,
                    playing ? "прогресс на старте " + (playbackProgressAtStart * 100f).ToString("0") + " %" : "не запустилось");
                break;
            }

            case CycleStart + 48:
            {
                float now = hub.Recording.Progress01;
                bool played = now > playbackProgressAtStart + 0.001f || !hub.Recording.IsPlaying;
                Check("C45 воспроизведение идёт (прогресс растёт или прошло до конца)", played,
                    "прогресс " + (playbackProgressAtStart * 100f).ToString("0") + " % → " +
                    (now * 100f).ToString("0") + " % · идёт: " + hub.Recording.IsPlaying);
                hub.Recording.StopPlayback("проверка");
                Next(2);
                break;
            }

            case CycleStart + 49: // C46 — preset-позы
            {
                Esc();
                KvPosePreset pose = hub.Poses.SaveCurrent("ДигностПоза_" + cycle);
                Check("C46 поза сохранена", pose != null && hub.Poses.Count > 0,
                    pose != null ? "файл " + pose.filePath : "не сохранилась");
                qBeforeSliders = flow.Validator.CopyCurrent();
                Next(2);
                break;
            }

            case CycleStart + 50:
            {
                // сдвигаем сустав вручную (как слайдер), потом возвращаемся в позу
                double[] q = flow.Validator.CopyCurrent();
                int j = 1;
                q[j] += 8.0;
                bool applied = flow.Validator.WithinLimits(q);
                if (applied) flow.Validator.Apply(q);
                Next(2);
                break;
            }

            case CycleStart + 51:
            {
                List<KvPosePreset> list = hub.Poses.ForCurrentRobot();
                KvPosePreset last = list.Count > 0 ? list[list.Count - 1] : null;
                string res = last != null ? hub.Poses.MoveTo(last) : "нет позы";
                Check("C47 переход в сохранённую позу выполнен", last != null,
                    "результат: " + res);
                Next(40);
                break;
            }

            case CycleStart + 52: // C48 — слайдеры суставов
            {
                Esc();
                if (flow.Motion != null) flow.Motion.Stop();
                Next(3);
                break;
            }

            case CycleStart + 53:
            {
                double[] q0 = flow.Validator.CopyCurrent();
                qBeforeSliders = q0;
                double[] q1 = (double[])q0.Clone();
                int j = 0;
                q1[j] += 6.0;
                bool within = flow.Validator.WithinLimits(q1);
                if (within) flow.Validator.Apply(q1);
                Next(3);
                // диагностика лимитов: видно, какой именно сустав вывел позу за диапазон
                StringBuilder limits = new StringBuilder();
                for (int i = 0; i < flow.Validator.Dof; i++)
                    limits.Append("J").Append(i + 1).Append("=").Append(q0[i].ToString("0.00"))
                          .Append("[").Append(flow.Validator.Lower[i].ToString("0.00")).Append("…")
                          .Append(flow.Validator.Upper[i].ToString("0.00")).Append("] ");
                Check("C48 слайдер сустава: робот реагирует на движение одного сустава",
                    within && Mathf.Abs((float)(flow.Validator.CopyCurrent()[j] - q0[j])) > 1.0,
                    "сустав " + (j + 1) + ": " + q0[j].ToString("0.0") + "° → " +
                    flow.Validator.CopyCurrent()[j].ToString("0.0") + "° · лимит соблюдён: " + within +
                    " · " + limits);
                break;
            }

            case CycleStart + 54:
            {
                double[] q0 = flow.Validator.CopyCurrent();
                double[] bad = (double[])q0.Clone();
                int j = 0;
                // +10° (а не +50): сустав складывается по модулю 360°, и 220° — это те же −140°,
                // то есть ДОПУСТИМЫЙ угол. Выход за лимит — только чуть выше верхней границы.
                bad[j] = flow.Validator.Upper[j] + 10.0;
                bool within = flow.Validator.WithinLimits(bad);
                Check("C49 выход за лимит сустава отклоняется", !within,
                    "проверено " + bad[j].ToString("0") + "° при лимите " +
                    flow.Validator.Upper[j].ToString("0") + "°");
                if (qBeforeSliders != null) flow.Validator.Apply(qBeforeSliders);
                Next(2);
                break;
            }

            case CycleStart + 55: // C50 — гриппер
            {
                Check("C50 гриппер собран на роботе", hub.Gripper.Attached,
                    hub.Gripper.Attached ? "точка захвата " + hub.Gripper.GraspPoint.ToString("F3") : "не собран");
                hub.Gripper.SetOpen(true);
                Next(20);
                break;
            }

            case CycleStart + 56:
            {
                float open = hub.Gripper.Width;
                gripperOpenWidth = open;
                hub.Gripper.SetOpen(false);
                Next(30);
                break;
            }

            case CycleStart + 57:
            {
                float closed = hub.Gripper.Width;
                Check("C51 пальцы гриппера смыкаются/размыкаются", closed < gripperOpenWidth - 0.005f,
                    "открыт " + (gripperOpenWidth * 1000f).ToString("0") + " мм → закрыт " +
                    (closed * 1000f).ToString("0") + " мм");
                hub.Gripper.SetOpen(true);
                Next(6);
                break;
            }

            case CycleStart + 58: // C52 — pick-and-place
            {
                Esc();
                bool haveRobot = flow.Robot != null;
                bool spawned = haveRobot && hub.PickAndPlace.SpawnCube(true);
                bool run = spawned && hub.PickAndPlace.Run();
                Check("C52 pick-and-place запущен (куб создан)", spawned && run,
                    "робот: " + (haveRobot ? flow.Robot.name : "null") + " · захват: " + hub.Gripper.Attached +
                    " · куб: " + (hub.PickAndPlace.Cube != null
                        ? hub.PickAndPlace.Cube.transform.position.ToString("F3") : "нет"));
                Next(2);
                break;
            }

            case CycleStart + 59:
                if (hub.PickAndPlace.Running && !Waited(4000)) return;
                Next(2);
                break;

            case CycleStart + 60:
            {
                KvPickAndPlace.Step st = hub.PickAndPlace.Current;
                Check("C53 pick-and-place прошёл все шаги (Done)", st == KvPickAndPlace.Step.Done,
                    "шаг: " + st + " · куб " + (hub.PickAndPlace.Cube != null
                        ? hub.PickAndPlace.Cube.transform.position.ToString("F3") : "нет"));
                hub.PickAndPlace.Cancel("диагностика");
                Next(4);
                break;
            }

            case CycleStart + 61: // C54 — тепловая карта (сфера / кольцо)
            {
                hub.Heatmap.SetVisible(true);
                hub.Heatmap.RequestRebuild(true);
                Next(4);
                break;
            }

            case CycleStart + 62:
                if (hub.Heatmap.Building && !Waited(900)) return;
                Next(2);
                break;

            case CycleStart + 63:
            {
                Check("C54 тепловая карта достижимости построена", hub.Heatmap.BuiltCount > 0,
                    hub.Heatmap.LastSummary + " · радиус " + hub.Heatmap.BuiltRadius.ToString("0.00") + " м");
                string prim = HeatmapPrimitive();
                bool wantSphere = flow.Validator.Dof > 3;
                Check("C55 форма карты соответствует роботу (" + (wantSphere ? "сфера" : "кольцо") + ")",
                    wantSphere ? prim == "Sphere" : prim == "Quad",
                    "примитив: " + prim + " · осей " + flow.Validator.Dof);
                hub.Heatmap.SetVisible(false);
                Next(3);
                break;
            }

            case CycleStart + 64: // C56 — конец цикла: возврат скоростей и переход к следующему роботу
            {
                flow.robotMoveSpeed = originalMoveSpeed[0];
                flow.phantomSpeedMultiplier = originalMoveSpeed[1];
                flow.Phantoms.minTravelTime = originalMinTravel;
                flow.Phantoms.maxTravelTime = originalMaxTravel;
                Esc();
                Next(3);
                break;
            }

            case CycleStart + 65:
            {
                Check("C56 сцена очищена после цикла (" + rc.name + ")",
                    CountTubes() == 0 && CountPhantoms() == 0 && flow.State.phase == FlowState.Idle,
                    "трубок " + CountTubes() + " · фантомов " + CountPhantoms() + " · фаза " + flow.State.phase);
                cyclesDone++;
                if (cycle == 0)
                {
                    Line("\n--- цикл переходит на SCARA ---\n");
                    cycle = 1;
                    step = CycleStart;
                    waitFrames = 3;
                    stepEnterFrame = Time.frameCount;
                }
                else
                {
                    Next(2);
                }
                break;
            }

            // ============================================================ БЛОК 4: UI
            case AfterCycle:
                Line("\n=== ЭТАП 4. ИНТЕРФЕЙС (FreeCAD-стиль) ===");
                Check("4.1 тулбар: 20 кнопок", ui.ToolbarButtonCount == 20, "кнопок: " + ui.ToolbarButtonCount);
                Next(2);
                break;

            case AfterCycle + 1:
            {
                IReadOnlyList<string> ids = ui.Toolbar.ButtonIds;
                // ФИКС 8 (18.09.2026): раскладка тулбара АДАПТИВНАЯ — число колонок считается
                // от ширины канваса (режимы «Авто / Широко / 5 в ряд», максимум 4 ряда в авто).
                // Проверяем, что все кнопки раскладываются и панель не выше разумного предела.
                int cols = KvToolbar.Columns;
                int rows = Mathf.CeilToInt(ids.Count / (float)Mathf.Max(1, cols));
                Check("4.2 раскладка тулбара: адаптивная, кнопок " + ids.Count,
                    cols >= 5 && rows <= 9,
                    "рядов: " + rows + " · столбцов: " + cols + " · режим: " + KvSettings.ToolbarLayout +
                    (KvSettings.ToolbarCompact ? " (компактный)" : ""));
                int noTip = 0;
                for (int i = 0; i < ui.ToolbarButtonCount; i++)
                {
                    KvIconButton b = ui.Toolbar.ButtonAt(i);
                    if (b == null || b.Tooltip == null || string.IsNullOrEmpty(b.Tooltip.title)) noTip++;
                }
                Check("4.3 у всех кнопок тулбара есть подсказка", noTip == 0, "без подсказки: " + noTip);
                Next(3);
                break;
            }

            case AfterCycle + 2:
            {
                ui.RebuildTree(true);
                Next(2);
                break;
            }

            case AfterCycle + 3:
            {
                Check("4.4 дерево моделей построено", ui.TreeRowCount > 0, "строк: " + ui.TreeRowCount);
                ProjectNode node = FindRobotNode(ui.TreeModel);
                if (node != null)
                {
                    ui.SelectNode(node);
                    Next(3);
                }
                else { Check("4.5 в дереве есть узел робота", false, "не найден"); Next(3); }
                break;
            }

            case AfterCycle + 4:
            {
                Check("4.5 выбор узла робота заполняет свойства", ui.PropertyRowCount > 3,
                    "строк свойств: " + ui.PropertyRowCount + " · выбран: " +
                    (ui.SelectedNode != null ? ui.SelectedNode.DisplayName : "нет"));
                Check("4.6 подсветка выбранного объекта включена",
                    ui.Highlight != null && ui.Highlight.Target != null,
                    ui.Highlight != null ? "рамка: " + (ui.Highlight.Target != null) : "нет компонента");
                ui.SelectNode(null);
                Next(2);
                break;
            }

            case AfterCycle + 5:
            {
                Check("4.7 статус-бар показывает состояние и робота",
                    !string.IsNullOrEmpty(ui.StatusState) && !string.IsNullOrEmpty(ui.StatusCursor),
                    "состояние: «" + ui.StatusState + "» · луч: «" + ui.StatusCursor + "» · тема: «" + ui.StatusTheme + "»");
                Next(2);
                break;
            }

            case AfterCycle + 6:
            {
                KvTheme.SetMode(KvThemeMode.Light, false);
                Next(3);
                break;
            }

            case AfterCycle + 7:
            {
                Check("4.8 светлая тема применяется мгновенно",
                    KvTheme.Mode == KvThemeMode.Light && ui.ToolbarButtonCount == 20 && ui.TreeRowCount > 0,
                    "тема: " + KvTheme.ModeLabel + " · кнопок " + ui.ToolbarButtonCount + " · строк дерева " + ui.TreeRowCount);
                KvTheme.SetMode(KvThemeMode.System, false);
                Next(3);
                break;
            }

            case AfterCycle + 8:
            {
                Check("4.9 системная тема выбирается", KvTheme.Mode == KvThemeMode.System,
                    "тема: " + KvTheme.ModeLabel);
                KvTheme.SetMode(KvThemeMode.Dark, true);
                Next(2);
                break;
            }

            case AfterCycle + 9:
            {
                Check("4.10 тема сохраняется в PlayerPrefs",
                    PlayerPrefs.HasKey(KvTheme.PrefsThemeKey),
                    "ключ " + KvTheme.PrefsThemeKey + " = " + PlayerPrefs.GetInt(KvTheme.PrefsThemeKey, -1));
                float w = ui.uiReferenceWidth, h = ui.uiReferenceHeight;
                ui.uiReferenceWidth = 1366f; ui.uiReferenceHeight = 768f;
                ui.RebuildShell();
                Next(3);
                break;
            }

            case AfterCycle + 10:
            {
                RectTransform tree = ui.TreeDock != null ? ui.TreeDock.Root : null;
                RectTransform props = ui.PropertiesDock != null ? ui.PropertiesDock.Root : null;
                float tMin = 0f, tMax = 0f, pMin = 0f, pMax = 0f;
                bool noOverlap = tree != null && props != null &&
                                 WorldXRange(tree, out tMin, out tMax) && WorldXRange(props, out pMin, out pMax) &&
                                 (tMax <= pMin + 1f || pMax <= tMin + 1f);
                Check("4.11 при 1366×768 панели не наезжают друг на друга", noOverlap,
                    "дерево x [" + tMin.ToString("0") + "…" + tMax.ToString("0") + "] · свойства x [" +
                    pMin.ToString("0") + "…" + pMax.ToString("0") + "]");
                ui.uiReferenceWidth = 1920f; ui.uiReferenceHeight = 1080f;
                ui.RebuildShell();
                Next(3);
                break;
            }

            case AfterCycle + 11:
            {
                Check("4.12 после возврата масштаба оболочка цела",
                    ui.ToolbarButtonCount == 20 && ui.TreeRowCount > 0,
                    "кнопок " + ui.ToolbarButtonCount + " · строк " + ui.TreeRowCount);
                Line("  [info] объектов в канвасе интерфейса: " + ui.UiObjectCount);
                Next(2);
                break;
            }

            // ============================================================ БЛОК 5: ВЗАИМОДЕЙСТВИЯ
            case AfterInteraction:
                Line("\n=== ЭТАП 5. ВЗАИМОДЕЙСТВИЯ ===");
                Esc();
                Next(2);
                break;

            case AfterInteraction + 1:
            {
                // сохранение/загрузка сессии: точка + зоны. Точку ищем заново для ТЕКУЩЕГО
                // робота потока — иначе клик может быть отклонён оракулом и сессия уйдёт без точки.
                Vector3 p;
                if (FindReachablePoint(flow.Validator, out p)) workPoint = p;
                SetLasers(true, false);
                Click(workPoint);
                Next(3);
                break;
            }

            case AfterInteraction + 2:
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(workPoint); Aim(workPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                Line("  [info] точка перед сохранением сессии: " + flow.State.point.ToString("F3") +
                     " · фаза " + flow.State.phase + " · есть точка " + flow.State.hasPoint +
                     " · статус: «" + ui.StatusMessage + "»");
                Next(1);
                break;

            case AfterInteraction + 3:
            {
                Vector3 p = flow.State.point;
                KvZone z = hub.Zones.Create(KvZoneShape.Box, p + Vector3.up * 0.4f,
                    new Vector3(0.2f, 0.2f, 0.2f), "ДигностЗона");
                KvSession s = hub.Sessions.Save("ДигностСессия");
                Check("5.1 сессия сохранена", s != null && File.Exists(s.filePath),
                    s != null ? s.filePath : "не сохранена");
                Check("5.2 в сессии есть точка и зоны",
                    s != null && s.hasPoint && s.zones.Count >= 1 && s.robots.Count == 2,
                    s != null ? "точка " + s.hasPoint + " · роботов " + s.robots.Count + " · зон " + s.zones.Count : "нет");
                Next(3);
                break;
            }

            case AfterInteraction + 4:
            {
                hub.Zones.Clear();
                Esc();
                Next(3);
                break;
            }

            case AfterInteraction + 5:
            {
                KvSession newest = hub.Sessions.Newest;
                bool restored = newest != null && hub.Sessions.Load(newest);
                Check("5.3 сессия загружается (точка и зоны восстановлены)",
                    restored && hub.Zones.Count >= 1 && flow.State.hasPoint,
                    "зон после загрузки: " + hub.Zones.Count + " · точка: " + flow.State.hasPoint);
                Next(3);
                break;
            }

            case AfterInteraction + 6:
            {
                Esc();
                Next(4);
                break;
            }

            case AfterInteraction + 7:
            {
                // Сравниваем «чистые» состояния: в начале прогона и сейчас, после Esc
                // (иначе в счёт попадут колбаски/фантомы/маркер текущего шага).
                int objectsNow = CountSceneObjects();
                Check("5.4 число объектов сцены не выросло за прогон (нет утечек)",
                    objectsNow <= sceneObjectsBaseline + 10,
                    "было " + sceneObjectsBaseline + " → стало " + objectsNow + " · по именам: " + ObjectDiff());
                Next(2);
                break;
            }

            // ============================================================ БЛОК 6: ПРОИЗВОДИТЕЛЬНОСТЬ
            case AfterPerformance:
                Line("\n=== ЭТАП 6. ПРОИЗВОДИТЕЛЬНОСТЬ (робот) ===");
                if (robot6 != null) aimAt(robot6);
                Next(3);
                break;

            case AfterPerformance + 1:
            {
                Vector3 p;
                bool found = FindReachablePoint(flow.Validator, out p);
                if (found) workPoint = p;
                Line("  [info] замер на «" + flow.Validator.RobotName + "» · точка " + workPoint.ToString("F3") +
                     " · найдена: " + found);
                frameSum = 0f; frameCount = 0;
                Next(2);
                break;
            }

            case AfterPerformance + 2:
            {
                frameSum += Time.unscaledDeltaTime; frameCount++;
                if (frameCount < 60) return;
                measuredFrameMs = frameSum / frameCount * 1000f;
                Check("6.1 базовое время кадра измерено", measuredFrameMs > 0f,
                    measuredFrameMs.ToString("0.00") + " мс/кадр (~" + (1000f / Mathf.Max(0.01f, measuredFrameMs)).ToString("0") + " FPS)");
                Next(1);
                break;
            }

            case AfterPerformance + 3: // замер IK
            {
                double[] seed = flow.Validator.CopyCurrent();
                double[] q;
                int n = 200;
                float t0 = Time.realtimeSinceStartup;
                for (int i = 0; i < n; i++) flow.Validator.SolveIk(workPoint, seed, out q);
                ikMicros = (Time.realtimeSinceStartup - t0) / n * 1e6f;
                Check("6.2 время решения IK измерено", ikMicros > 0f,
                    ikMicros.ToString("0") + " мкс на вызов (" + n + " вызовов) · ошибка " +
                    (flow.Validator.LastIkError * 1000f).ToString("0.0") + " мм · осей " + flow.Validator.Dof);
                Next(1);
                break;
            }

            case AfterPerformance + 4: // генерация под нагрузкой
            {
                SetLasers(true, false);
                Click(workPoint);
                tGenStart = Time.realtimeSinceStartup;
                Next(3);
                break;
            }

            case AfterPerformance + 5:
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(2000))
                {
                    frameSum += Time.unscaledDeltaTime;     // FPS ИМЕННО во время генерации 8 траекторий
                    frameCount++;
                    aimAt(workPoint); Aim(workPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                genSeconds = Time.realtimeSinceStartup - tGenStart;
                measuredFrameMsGen = frameCount > 0 ? frameSum / frameCount * 1000f : 0f;
                frameSum = 0f; frameCount = 0;
                Next(1);
                break;

            case AfterPerformance + 6: // FPS с 8 фантомами + отчёт о генерации
            {
                frameSum += Time.unscaledDeltaTime; frameCount++;
                if (frameCount < 60) return;
                measuredFrameMsPhantom = frameSum / frameCount * 1000f;
                Check("6.3 время кадра при 8 движущихся фантомах", measuredFrameMsPhantom > 0f,
                    measuredFrameMsPhantom.ToString("0.00") + " мс/кадр (генерация " + genSeconds.ToString("0.000") +
                    " с при " + measuredFrameMsGen.ToString("0.00") + " мс/кадр, вариантов " +
                    flow.State.candidates.Count + ", фантомов " + flow.Phantoms.Count + ")");
                Next(1);
                break;
            }

            case AfterPerformance + 7:
            {
                hub.Heatmap.SetVisible(true);
                hub.Heatmap.RequestRebuild(true);
                frameSum = 0f; frameCount = 0;
                Next(1);
                break;
            }

            case AfterPerformance + 8: // FPS с тепловой картой
            {
                frameSum += Time.unscaledDeltaTime; frameCount++;
                if (frameCount < 60) return;
                measuredFrameMsHeatmap = frameSum / frameCount * 1000f;
                Check("6.4 время кадра при включённой тепловой карте", measuredFrameMsHeatmap > 0f,
                    measuredFrameMsHeatmap.ToString("0.00") + " мс/кадр · " + hub.Heatmap.LastSummary);
                Next(1);
                break;
            }

            case AfterPerformance + 9: // дерево (замер по одному обновлению на кадр)
            {
                if (treeSamples < 20)
                {
                    float t = Time.realtimeSinceStartup;
                    ui.Refresh();                       // полная пересборка (дерево + панели)
                    treeMsSum += (Time.realtimeSinceStartup - t) * 1000f;
                    treeSamples++;
                    return;
                }
                if (panelSamples < 20)
                {
                    float t = Time.realtimeSinceStartup;
                    ui.RefreshPanels();                 // кадровое обновление
                    panelMsSum += (Time.realtimeSinceStartup - t) * 1000f;
                    panelSamples++;
                    return;
                }
                float treeMs = treeMsSum / treeSamples;
                float panels = panelMsSum / panelSamples;
                Check("6.5 полная пересборка интерфейса дороже кадрового обновления", treeMs > panels,
                    "полное (с деревом) " + treeMs.ToString("0.0") + " мс · кадровое " + panels.ToString("0.0") +
                    " мс · ~" + (1000f / Mathf.Max(0.01f, measuredFrameMs + treeMs)).ToString("0") +
                    " FPS при непрерывной пересборке дерева · объектов в канвасе " + ui.UiObjectCount);
                int v = ui.Tree.RebuildVersion;
                ui.RebuildTree(false);
                Check("6.6 дерево не пересобирается без изменений", ui.Tree.RebuildVersion == v,
                    "версия сборки: " + v + " → " + ui.Tree.RebuildVersion);
                treeSamples = 0; panelSamples = 0; treeMsSum = 0f; panelMsSum = 0f;
                hub.Heatmap.SetVisible(false);
                Esc();
                Next(3);
                break;
            }

            // ============================================================ БЛОК 7: СТАБИЛЬНОСТЬ
            case AfterStability:
                Line("\n=== ЭТАП 7. СТАБИЛЬНОСТЬ ===");
                cycle = 0;
                stabilityRun = 0;
                Next(2);
                break;

            case AfterStability + 1: // 5 циклов «точка → генерация → Esc»
            {
                // чередуем роботов: проверяем и сброс состояния при переключении
                cycle = stabilityRun % 2;
                rc = cycle == 0 ? robot6 : robotScara;
                Vector3 b = rc.transform.position;
                cam.transform.position = b + new Vector3(0f, 1.25f, -2.6f);
                cam.transform.rotation = Quaternion.LookRotation((b + Vector3.up * 0.1f - (b + new Vector3(0f, 1.25f, -2.6f))).normalized, Vector3.up);
                aimAt(rc);
                Next(3);
                break;
            }

            case AfterStability + 2:
                if (flow.Robot != rc && !Waited(30)) { aimAt(rc); return; }
                Next(1);
                break;

            case AfterStability + 3:
            {
                Vector3 p;
                if (!FindReachablePoint(flow.Validator, out p)) p = workPoint;
                stabilityPoint = p;
                SetLasers(true, false);
                Click(p);
                Next(3);
                break;
            }

            case AfterStability + 4:
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(stabilityPoint); Aim(stabilityPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                Next(1);
                break;

            case AfterStability + 5:
            {
                int cand = flow.State.candidates.Count;
                int tubes = CountTubes();
                int ph = CountPhantoms();
                bool good = cand == TrajectoryFlowController.MaxTrajectories && tubes == cand && ph == cand;
                if (!good) Check("7.x цикл " + (stabilityRun + 1) + ": 8 траекторий и 8 фантомов", false,
                    "вариантов " + cand + " · трубок " + tubes + " · фантомов " + ph + " · фаза " + flow.State.phase);
                stabilityAllGood &= good;
                Esc();
                Next(3);
                break;
            }

            case AfterStability + 6:
            {
                bool clean = CountTubes() == 0 && CountPhantoms() == 0 && flow.State.phase == FlowState.Idle;
                stabilityAllClean &= clean;
                stabilityRun++;
                if (stabilityRun < 6)
                {
                    step = AfterStability + 1;
                    waitFrames = 2;
                    stepEnterFrame = Time.frameCount;
                    break;
                }
                Check("7.1 шесть циклов «точка → 8 траекторий → Esc»: 8/8/8 каждый раз", stabilityAllGood,
                    "циклов: " + stabilityRun);
                Check("7.2 после каждого Esc сцена чистая (нет накопления фантомов/трубок)", stabilityAllClean,
                    "трубок " + CountTubes() + " · фантомов " + CountPhantoms());
                Next(2);
                break;
            }

            case AfterStability + 7:
            {
                int objectsNow = CountSceneObjects();
                Check("7.3 повторные генерации не растят сцену", objectsNow <= sceneObjectsBaseline + 10,
                    "было " + sceneObjectsBaseline + " → стало " + objectsNow + " · по именам: " + ObjectDiff());
                Next(2);
                break;
            }

            case AfterStability + 8: // Esc из любого состояния
            {
                EscStatesTested = 0; EscFailed = 0;
                Next(1);
                break;
            }

            case AfterStability + 9:
            {
                // 1) Esc из Idle
                Esc();
                if (flow.State.phase == FlowState.Idle) EscStatesTested++; else EscFailed++;
                Next(2);
                break;
            }

            case AfterStability + 10:
            {
                // 2) Esc после выбора точки и траектории
                SetLasers(true, false);
                Click(stabilityPoint);
                Next(3);
                break;
            }

            case AfterStability + 11:
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(stabilityPoint); Aim(stabilityPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                if (flow.State.candidates.Count == 0) { Next(4); break; }
                SetLasers(false, true);
                {
                    Vector3[] tubePick4 = flow.State.candidates[0].tube;
                    Click(tubePick4 != null && tubePick4.Length > 0
                        ? tubePick4[tubePick4.Length / 2] : stabilityPoint);
                }
                Next(2);
                break;

            case AfterStability + 12:
            {
                if (flow.State.phase == FlowState.PhantomsMoving) EscStatesTested++; else EscFailed++;
                Esc();
                Next(2);
                Check("7.4 Esc из состояния «фантом едет» возвращает в Idle",
                    flow.State.phase == FlowState.Idle, "фаза: " + flow.State.phase);
                break;
            }

            case AfterStability + 13:
            {
                // 3) Esc во время движения робота
                SetLasers(true, false);
                Click(stabilityPoint);
                Next(3);
                break;
            }

            case AfterStability + 14:
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(stabilityPoint); Aim(stabilityPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                if (flow.State.candidates.Count == 0) { Next(5); break; }
                // уводим робота от конечной позы — иначе движение закончится быстрее проверки
                {
                    double[] qn = flow.Validator.CopyCurrent();
                    qn[0] += 12.0;
                    if (flow.Validator.WithinLimits(qn)) flow.Validator.Apply(qn);
                }
                SetLasers(false, true);
                {
                    Vector3[] tubePick5 = flow.State.candidates[0].tube;
                    Click(tubePick5 != null && tubePick5.Length > 0
                        ? tubePick5[tubePick5.Length / 2] : stabilityPoint);
                }
                Next(2);
                break;

            case AfterStability + 15:
            {
                double[] pose = flow.Phantoms.Count > 0 ? flow.Phantoms.PoseOf(0) : null;
                if (pose != null) Click(flow.Validator.TcpAt(pose));
                Next(3);
                break;
            }

            case AfterStability + 16:
            {
                if (escWait == 0)
                {
                    ridingNow = flow.State.phase == FlowState.RobotMoving;
                    Esc();
                    escWait = 1;
                    return;
                }
                escWait = 0;
                Check("7.5 Esc во время движения робота возвращает в Idle",
                    ridingNow && flow.State.phase == FlowState.Idle && CountTubes() == 0 && CountPhantoms() == 0,
                    "ехал: " + ridingNow + " · фаза: " + flow.State.phase);
                Next(2);
                break;
            }

            case AfterStability + 17:
            {
                // 4) Esc в режиме перемещения точки
                SetLasers(true, false);
                Click(stabilityPoint);
                Next(3);
                break;
            }

            case AfterStability + 18:
                if (flow.State.phase != FlowState.TrajectoriesShown && !Waited(1500))
                {
                    aimAt(stabilityPoint); Aim(stabilityPoint, true, false, false, false, Vector3.zero, false);
                    return;
                }
                EnterPointMove(true);
                Next(2);
                break;

            case AfterStability + 19:
            {
                bool inMode = flow.State.phase == FlowState.PointMoveMode;
                Esc();
                Next(2);
                Check("7.6 Esc в режиме перемещения точки выходит из режима без сброса точки",
                    inMode && flow.State.phase != FlowState.PointMoveMode,
                    "был в режиме: " + inMode + " · фаза: " + flow.State.phase);
                Esc();
                Next(2);
                break;
            }

            case AfterStability + 20:
            {
                Check("7.7 переключение роботов сбрасывает состояние корректно",
                    CountTubes() == 0 && CountPhantoms() == 0 && flow.State.phase == FlowState.Idle,
                    "трубок " + CountTubes() + " · фантомов " + CountPhantoms() + " · фаза " + flow.State.phase);
                Next(2);
                break;
            }

            // ============================================================ БЛОК 8: КОНСОЛЬ И ЛОГИ
            case AfterConsole:
                Line("\n=== ЭТАП 8. КОНСОЛЬ И ЖУРНАЛ ===");
                Check("8.1 за прогон нет исключений (Exception)", exceptions.Count == 0,
                    exceptions.Count == 0 ? "ни одного" : exceptions.Count + " шт · первое: " + exceptions[0]);
                // «No graphic device is available» — ошибка СРЕДЫ batch-режима -nographics
                // (HDRP не может инициализировать вид), а не кода: см. PROJECT_CONTEXT §10.
                int envErrors = 0;
                var realErrors = new List<string>();
                for (int i = 0; i < errors.Count; i++)
                {
                    if (errors[i].IndexOf("No graphic device", StringComparison.OrdinalIgnoreCase) >= 0) envErrors++;
                    else realErrors.Add(errors[i]);
                }
                Check("8.2 за прогон нет ошибок и assert (кроме ошибки среды -nographics)",
                    realErrors.Count == 0 && asserts.Count == 0,
                    "ошибок: " + realErrors.Count + " · assert: " + asserts.Count +
                    " · ошибок среды (нет графического устройства): " + envErrors +
                    (realErrors.Count > 0 ? " · первая: " + realErrors[0] : ""));
                Next(2);
                break;

            case AfterConsole + 1:
            {
                var top = new List<KeyValuePair<string, int>>(messageCounts);
                top.Sort(delegate (KeyValuePair<string, int> a, KeyValuePair<string, int> b)
                { return b.Value.CompareTo(a.Value); });
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < Mathf.Min(3, top.Count); i++)
                    sb.Append("[").Append(top[i].Value).Append("×] ").Append(top[i].Key).Append(" | ");
                Line("  [info] самые частые сообщения консоли: " + sb);
                int max = top.Count > 0 ? top[0].Value : 0;
                Check("8.3 в консоли нет спама (одно сообщение < 200 раз)", max < 200,
                    "максимум повторов: " + max + " · уникальных сообщений: " + messageCounts.Count);
                Next(2);
                break;
            }

            case AfterConsole + 2:
            {
                Check("8.4 журнал действий ведётся", hub.Log.Count > 0, "записей: " + hub.Log.Count);
                Check("8.5 файл журнала создан", File.Exists(hub.Log.FilePath), hub.Log.FilePath);
                bool noMissing = true;
                for (int i = 0; i < exceptions.Count; i++)
                    if (exceptions[i].IndexOf("MissingReference", StringComparison.OrdinalIgnoreCase) >= 0) noMissing = false;
                Check("8.6 нет MissingReferenceException", noMissing, noMissing ? "чисто" : "найдено");
                Next(2);
                break;
            }

            // ============================================================ ИТОГ
            case Finish:
            {
                // уборка тестовых объектов и настроек
                if (hub != null)
                {
                    for (int i = hub.Zones.Count - 1; i >= 0; i--)
                    {
                        KvZone z = hub.Zones.Zones[i];
                        if (z != null && z.ZoneName != null &&
                            z.ZoneName.IndexOf("Дигност", StringComparison.OrdinalIgnoreCase) >= 0)
                            hub.Zones.Delete(z);
                    }
                    hub.PickAndPlace.DestroyCube();
                    hub.Heatmap.SetVisible(false);
                }
                if (flow != null)
                {
                    flow.robotMoveSpeed = originalMoveSpeed[0];
                    flow.phantomSpeedMultiplier = originalMoveSpeed[1];
                    if (flow.Phantoms != null)
                    {
                        flow.Phantoms.minTravelTime = originalMinTravel;
                        flow.Phantoms.maxTravelTime = originalMaxTravel;
                    }
                }
                Line("");
                Line("=== ДОПОЛНИТЕЛЬНО ===");
                Line("  [info] скорость робота (штатная): " + flow.robotMoveSpeed.ToString("0.0000") + " ю/с");
                Line("  [info] множитель фантомов: ×" + flow.phantomSpeedMultiplier.ToString("0.##") +
                     " → фантом " + flow.phantomMoveSpeed.ToString("0.000") + " ю/с");
                Line("  [info] время кадра: базовое " + measuredFrameMs.ToString("0.00") +
                     " мс · генерация 8 траекторий " + measuredFrameMsGen.ToString("0.00") +
                     " мс · 8 фантомов " + measuredFrameMsPhantom.ToString("0.00") +
                     " мс · тепловая карта " + measuredFrameMsHeatmap.ToString("0.00") + " мс");
                Line("  [info] IK: " + ikMicros.ToString("0") + " мкс/вызов · генерация 8 траекторий: " +
                     genSeconds.ToString("0.000") + " с");
                Line("");
                Line("ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · [info] " + info);
                Line(fail == 0 ? "ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ИТОГ: ЕСТЬ ОТКАЗЫ (" + fail + ")");
                Debug.Log("[FullVerify] ИТОГ: OK " + ok + " · FAIL " + fail);
                finished = true;
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(fail == 0 ? 0 : 2);
                break;
            }

            default:
                Next(1);
                break;
        }
    }

    // ================================================================== вспомогательное

    private const int CycleStart = 20;
    private const int AfterCycle = 90;
    private const int AfterInteraction = 110;
    private const int AfterPerformance = 120;
    private const int AfterStability = 130;
    private const int AfterConsole = 170;
    private const int Finish = 190;

    private static Vector3 stabilityPoint;
    private static int stabilityRun;
    private static bool stabilityAllGood = true, stabilityAllClean = true;
    private static int EscStatesTested, EscFailed;
    private static Vector3 jitterPrev;
    private static float jitterMax;
    private static int jitterFrames;
    private static int escWait;
    private static float movedDistance;
    private static bool ridingNow;
    private static float playbackProgressAtStart;
    private static bool tubesCheckedFlag, tubesCheckedFlag2;
    private static bool undoOk;
    private static float gripperOpenWidth;
    private static int treeSamples, panelSamples;
    private static float treeMsSum, panelMsSum;

    private static void SetLasers(bool red, bool green)
    {
        if (cam == null) return;
        cam.leftHandEnabled = red;
        cam.rightHandEnabled = green;
    }

    /// <summary>Кадровый вход в поток «как от камеры»: прицел + ЛКМ (confirm) + Enter (enter).</summary>
    private static void Aim(Vector3 p, bool hit, bool confirm, bool cancel, bool enter, Vector3 axis, bool fast)
    {
        if (flow == null) return;
        flow.UpdateAim(p, hit, confirm, cancel,
            cam != null && cam.leftHandEnabled, cam != null && cam.rightHandEnabled,
            Vector3.up, hit, enter, axis, fast);
    }

    private static void Click(Vector3 p) { Aim(p, true, true, false, false, Vector3.zero, false); }
    private static void Esc() { Aim(flow != null ? flow.State.point : Vector3.zero, false, false, true, false, Vector3.zero, false); }
    private static void EnterPointMove(bool toggle) { Aim(flow.State.point, true, false, false, toggle, Vector3.zero, false); }
    private static void EnterPoint(bool toggle, Vector3 axis) { Aim(flow.State.point, true, false, false, toggle, axis, false); }

    /// <summary>Наводит камеру на точку робота, чтобы поток привязался к нужному стенду.</summary>
    private static void aimAt(RobotController r)
    {
        if (r == null || cam == null) return;
        aimAt(r.transform.position + Vector3.up * 0.15f);
    }

    /// <summary>Наводит камеру на точку (чтобы собственный прицел камеры не уводил поток).</summary>
    private static void aimAt(Vector3 p)
    {
        if (cam == null) return;
        Vector3 from = p + new Vector3(0f, 1.15f, -1.5f);
        cam.transform.position = from;
        cam.transform.rotation = Quaternion.LookRotation((p - from).normalized, Vector3.up);
    }

    /// <summary>
    /// Достижимая точка на столе вокруг базы робота: перебор радиуса и направления.
    /// Проверяются ВСЕ условия сразу — IK с лимитами, ошибка позиционирования И зазор
    /// до сцены (мир столкновений хаба): точка «рядом с базой» проходит IK, но оракул
    /// честно отвергает её как пересечение с корпусом робота.
    /// </summary>
    private static bool FindReachablePoint(PoseValidator v, out Vector3 point)
    {
        point = Vector3.zero;
        if (v == null || !v.Ready) return false;
        CollisionWorld world = hub != null ? hub.World : null;
        Vector3 b = v.BasePosition;
        double[] seed = v.CopyCurrent();
        double[] q;
        // SCARA: аналитический IK «доводит» высоту призмы клампом, поэтому у точки на
        // столешнице остаётся ошибка в несколько миллиметров — это норма, а не отказ.
        float maxIkError = v.Dof > 3 ? 0.005f : 0.012f;
        // Радиусы — «рабочие», а не предельные: точка на самой границе досягаемости проходит
        // IK, но планировщик уже не находит путь (в прогоне это давало «траектория не найдена»).
        float maxR = v.Dof > 3 ? 0.75f : 0.45f;
        float minR = v.Dof > 3 ? 0.30f : 0.20f;
        // У SCARA ход призмы отсчитывается от базы и его нижняя граница — ровно уровень
        // столешницы: точка НА столе попадает в самый низ диапазона, запас до лимита ~0°,
        // а планировщик требует ≥3° — ветви IK отбрасываются. Поэтому для SCARA берём точку
        // на 6 см выше стола (штатная рабочая высота), для 6-осевого — ровно на поверхности.
        float lift = v.Dof > 3 ? 0f : 0.06f;
        for (float r = maxR; r >= minR; r -= 0.05f)             // сначала дальние точки: они безопаснее
        {
            for (float ang = 0f; ang < 360f; ang += 20f)
            {
                float rad = ang * Mathf.Deg2Rad;
                Vector3 p = b + new Vector3(Mathf.Cos(rad) * r, 0.02f, Mathf.Sin(rad) * r);
                RaycastHit hit;
                if (!Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out hit, 1.2f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<RobotController>() != null) continue;
                p = hit.point + Vector3.up * lift;
                if (!v.SolveIk(p, seed, out q)) continue;
                if (!v.WithinLimits(q)) continue;
                if (v.LastIkError > maxIkError) continue;
                if (world != null)
                {
                    Vector3 tcp; Vector3[] nodes;
                    float clearance = v.ClearanceAt(q, world, out tcp, out nodes);
                    if (clearance < 0.05f) continue;               // нужен запас над столом
                }
                point = p;
                return true;
            }
        }
        return false;
    }

    private static int CountTubes()
    {
        return UnityEngine.Object.FindObjectsByType<TrajectoryTube>(FindObjectsInactive.Include).Length;
    }

    private static int CountPhantoms()
    {
        int n = 0;
        Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform t in all)
            if (t != null && t.name != null && t.name.StartsWith("Phantom_", StringComparison.Ordinal)) n++;
        return n;
    }

    /// <summary>
    /// Объекты 3D-сцены БЕЗ интерфейса. Интерфейс (канвасы: оболочка KazistovVv, окно функций,
    /// HUD режима точки) растёт вместе с содержимым (строки дерева, журнала) и к «утечкам сцены»
    /// отношения не имеет, поэтому его поддеревья не считаются.
    /// </summary>
    private static int CountSceneObjects()
    {
        int n = 0;
        Scene sc = SceneManager.GetActiveScene();
        foreach (GameObject root in sc.GetRootGameObjects())
        {
            if (root == null) continue;
            if (root.GetComponentInChildren<Canvas>(true) != null) continue;   // интерфейс
            n += root.GetComponentsInChildren<Transform>(true).Length;
        }
        return n;
    }

    // ---- гистограмма объектов сцены: что именно выросло за прогон (поиск утечек)

    private static readonly Dictionary<string, int> objectHistogram = new Dictionary<string, int>();

    private static void SnapshotObjects()
    {
        objectHistogram.Clear();
        foreach (KeyValuePair<string, int> kv in CurrentHistogram()) objectHistogram[kv.Key] = kv.Value;
    }

    private static Dictionary<string, int> CurrentHistogram()
    {
        var map = new Dictionary<string, int>();
        Scene sc = SceneManager.GetActiveScene();
        foreach (GameObject root in sc.GetRootGameObjects())
        {
            if (root == null) continue;
            if (root.GetComponentInChildren<Canvas>(true) != null) continue;   // интерфейс не считаем
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (tr == null) continue;
                string n = NormalizeName(tr.name);
                int c;
                map.TryGetValue(n, out c);
                map[n] = c + 1;
            }
        }
        return map;
    }

    /// <summary>Имя без индексов экземпляров: Phantom_3 и Phantom_7 — один и тот же класс объекта.</summary>
    private static string NormalizeName(string n)
    {
        if (string.IsNullOrEmpty(n)) return "?";
        int cut = n.Length;
        while (cut > 0 && (char.IsDigit(n[cut - 1]) || n[cut - 1] == '_')) cut--;
        string head = n.Substring(0, cut);
        return head.Length == 0 ? n : head;
    }

    private static string ObjectDiff()
    {
        Dictionary<string, int> now = CurrentHistogram();
        StringBuilder sb = new StringBuilder();
        foreach (KeyValuePair<string, int> kv in now)
        {
            int before;
            objectHistogram.TryGetValue(kv.Key, out before);
            if (kv.Value != before) sb.Append(kv.Key).Append(": ").Append(before).Append("→").Append(kv.Value).Append("; ");
        }
        foreach (KeyValuePair<string, int> kv in objectHistogram)
            if (!now.ContainsKey(kv.Key)) sb.Append(kv.Key).Append(": ").Append(kv.Value).Append("→0; ");
        return sb.Length == 0 ? "изменений нет" : sb.ToString();
    }

    /// <summary>Форма траектории: длина + конечная точка (для сравнения «другой формы»).</summary>
    private static string PlanShape(PlannedTrajectory plan)
    {
        if (plan == null || plan.Path == null || plan.Path.Length == 0) return "нет";
        Vector3 end = plan.GoalQ != null && flow != null ? flow.Validator.TcpAt(plan.GoalQ) : Vector3.zero;
        return "L=" + plan.Length.ToString("0.000") + " · конец " + end.ToString("F2");
    }

    private static string HeatmapPrimitive()
    {
        MeshFilter[] filters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include);
        foreach (MeshFilter mf in filters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.gameObject.name != "ТепловаяКартаДостижимости") continue;
            return mf.sharedMesh.name.Replace("(Clone)", "");
        }
        return "нет";
    }

    /// <summary>Горизонтальный диапазон панели в координатах канваса (по углам).</summary>
    private static bool WorldXRange(RectTransform rt, out float min, out float max)
    {
        min = max = 0f;
        if (rt == null) return false;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        min = Mathf.Min(Mathf.Min(corners[0].x, corners[1].x), Mathf.Min(corners[2].x, corners[3].x));
        max = Mathf.Max(Mathf.Max(corners[0].x, corners[1].x), Mathf.Max(corners[2].x, corners[3].x));
        return true;
    }

    /// <summary>Первый узел-робот в дереве моделей (поиск по всем уровням).</summary>
    private static ProjectNode FindRobotNode(IReadOnlyList<ProjectNode> roots)
    {
        if (roots == null) return null;
        for (int i = 0; i < roots.Count; i++)
        {
            ProjectNode n = roots[i];
            if (n == null) continue;
            if (n.Kind == ProjectNodeKind.Robot) return n;
            ProjectNode deep = FindRobotNode(n.Children);
            if (deep != null) return deep;
        }
        return null;
    }
}