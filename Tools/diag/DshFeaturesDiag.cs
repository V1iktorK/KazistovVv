using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KazistovVvFeatures;
using KazistovVvUI;
using TrajectoryCore;

/// <summary>
/// Диагностический прогон этапов 1–20 (запись, позы, суставы, аварийный стоп, зоны,
/// сравнение, графики, тепловые карты, гриппер, pick-and-place, ETA, журнал, замер,
/// Undo/Redo, звук, вибрация, сценарии, презентация, сессии).
///
/// ЗАПУСК: Unity.exe -batchmode -nographics -projectPath "&lt;проект&gt;" -executeMethod DshFeaturesDiag.Run -logFile _dsh_features_diag.log
/// (БЕЗ -quit). Отчёт: `_dsh_features_verify.txt` в корне проекта — строки пишутся СРАЗУ
/// (домен перезагружается при входе в PlayMode, буфер в памяти потерялся бы).
///
/// ВАЖНО: это диагностика агента; в копию пользователя НЕ переносится.
/// </summary>
public static class DshFeaturesDiag
{
    private const string SessionKey = "DshFeaturesDiag.Active";

    /// <summary>Путь отчёта вычисляется КАЖДЫЙ раз: статическое поле не переживает
    /// перезагрузку домена при входе в PlayMode (иначе писали бы в null).</summary>
    private static string ReportPath
    {
        get { return FeatureStorage.ReportPath("_dsh_features_verify.txt"); }
    }

    private static int step;
    private static int waitFrames;
    private static int stepFrame;
    private static int ok, fail, info;
    private static bool finished;

    private static FeatureHub hub;
    private static KazistovVvUIManager ui;
    private static TrajectoryFlowController flow;

    private static KvZone box, sphere, cylinder;
    private static KvPosePreset pose;
    private static KvTrajectoryRecord record;
    private static string recordPath;
    private static bool planned;

    public static void Run()
    {
        File.WriteAllText(ReportPath, "=== ПРОВЕРКА ЭТАПОВ 1–20 (KazistovVv) ===\n" +
                                      FeatureStorage.IsoNow() + "\n\n", new UTF8Encoding(false));
        SessionState.SetBool(SessionKey, true);
        CleanTestData();
        Debug.Log("[FeaturesDiag] открываю MainScene и вхожу в PlayMode…");
        EditorSceneManager.OpenScene("Assets/_Project/00_Scenes/MainScene.unity",
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>Убрать данные предыдущих прогонов (файлы с «Дигност»/«Тест» в имени).</summary>
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
        Debug.Log("[FeaturesDiag] удалено тестовых файлов прошлых прогонов: " + removed);
    }

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (finished) return;
        if (!EditorApplication.isPlaying) return;

        if (waitFrames > 0)
        {
            waitFrames--;
            return;
        }
        if (Time.frameCount == stepFrame) return;   // один шаг на игровой кадр
        stepFrame = Time.frameCount;

        try
        {
            RunStep();
        }
        catch (Exception e)
        {
            Check("шаг " + step + " без исключения", false, e.GetType().Name + ": " + e.Message);
            Next(2);
        }
    }

    private static void RunStep()
    {
        switch (step)
        {
            case 0:
                ui = KazistovVvUIManager.Instance;
                Check("менеджер интерфейса поднялся", ui != null, ui != null ? "есть" : "нет");
                Next(2);
                break;

            case 1:   // ждём, пока камера/поток создадут хаб и привяжут сервисы
                hub = FeatureHub.Instance;
                flow = ui != null ? ui.Flow : null;
                bool ready = hub != null && flow != null && flow.Validator != null && flow.Validator.Ready;
                Check("хаб новых функций создан", hub != null, hub != null ? hub.Dump() : "нет");
                Check("поток этапов и валидатор готовы", ready,
                    ready ? "робот: " + flow.Validator.RobotName + " · осей " + flow.Validator.Dof : "нет");
                if (!ready && Time.frameCount < 600) { Next(0); break; }
                Next(2);
                break;

            // ---------------------------------------------------------- этапы 1–4: интерфейс и команды
            case 2:
                Check("тулбар расширен до 20 кнопок",
                    ui.ToolbarButtonCount == 20, "кнопок: " + ui.ToolbarButtonCount);
                IReadOnlyList<string> ids = ui.Toolbar.ButtonIds;
                Check("в тулбаре есть кнопка «Записать»", ids.Contains("record.toggle"), "record.toggle");
                Check("в тулбаре есть «АВАРИЙНАЯ ОСТАНОВКА»", ids.Contains("estop"), "estop");
                Check("в тулбаре есть тепловая карта достижимости", ids.Contains("view.heatmap"), "view.heatmap");
                Check("в тулбаре есть тепловая карта зазоров", ids.Contains("view.clearance"), "view.clearance");
                Check("в тулбаре есть панель функций", ids.Contains("features.open"), "features.open");
                Next(0);
                break;

            case 3:
                string[] required =
                {
                    "record.toggle", "record.play", "record.speed", "pose.save", "pose.goto",
                    "joints.panel", "estop", "zone.box", "zone.sphere", "zone.cylinder", "zone.panel",
                    "compare.mark", "compare.open", "graph.open", "view.heatmap", "view.clearance",
                    "gripper.toggle", "demo.pickplace", "eta.toggle", "log.open", "log.export",
                    "perf.open", "edit.undo", "edit.redo", "scenarios.open", "scenarios.run",
                    "presentation.toggle", "session.save", "session.load", "session.panel", "features.open"
                };
                int missing = 0;
                StringBuilder miss = new StringBuilder();
                for (int i = 0; i < required.Length; i++)
                {
                    if (KvCommands.Get(required[i]) == null)
                    {
                        missing++;
                        miss.Append(required[i]).Append(' ');
                    }
                }
                Check("все команды этапов 1–20 зарегистрированы", missing == 0,
                    missing == 0 ? required.Length + " команд" : "нет: " + miss);
                Check("«Отменить» больше не заглушка",
                    KvCommands.Get("edit.undo") != null && !KvCommands.Get("edit.undo").Stub, "edit.undo");
                Check("«Вернуть» больше не заглушка",
                    KvCommands.Get("edit.redo") != null && !KvCommands.Get("edit.redo").Stub, "edit.redo");
                Next(0);
                break;

            case 4:
                string[] icons =
                {
                    "record", "estop", "zone", "zone-box", "zone-sphere", "zone-cylinder", "compare",
                    "graph", "heatmap", "clearance", "gripper", "pickplace", "eta", "log", "scenario",
                    "presentation", "session", "features"
                };
                int badIcons = 0;
                for (int i = 0; i < icons.Length; i++) if (!KvIcons.Has(icons[i])) badIcons++;
                Check("новые иконки нарисованы", badIcons == 0,
                    badIcons == 0 ? icons.Length + " иконок" : "не найдено: " + badIcons);
                Check("панель функций собрана",
                    hub.Window != null && hub.Window.GetComponentInChildren<Canvas>(true) != null,
                    hub.Window != null ? "окно есть" : "нет окна");
                hub.Window.Show(0);
                Check("панель функций открывается на вкладке «Запись»",
                    hub.Window.Visible && hub.Window.VisibleTab == 0,
                    "вкладка: " + hub.Window.VisibleTab);
                hub.Window.Hide();
                Next(0);
                break;

            // ---------------------------------------------------------- этап 13: журнал
            case 5:
                Check("журнал действий ведётся", hub.Log.Count > 0, "записей: " + hub.Log.Count);
                Check("файл журнала создан", File.Exists(hub.Log.FilePath), hub.Log.FilePath);
                hub.Log.Add(KvLogKind.System, "проверка журнала из дигноста");
                hub.Log.Filter = KvLogKind.System;
                Check("фильтр журнала по типу работает", hub.Log.Filtered().Count > 0,
                    "системных записей: " + hub.Log.Filtered().Count);
                hub.Log.Filter = null;
                Next(0);
                break;

            // ---------------------------------------------------------- этап 16: звук, этап 17: вибрация
            case 6:
                bool audioOk = true;
                string audioError = "";
                try
                {
                    hub.Audio.Play(KvSound.PointSelected, Vector3.zero);
                    hub.Audio.Play(KvSound.TrajectoryConfirmed, Vector3.zero);
                    hub.Audio.Play(KvSound.MotionStarted, Vector3.zero);
                    hub.Audio.Play(KvSound.Error, Vector3.zero);
                    hub.Audio.Play(KvSound.EmergencyStop, Vector3.zero);
                }
                catch (Exception e) { audioOk = false; audioError = e.Message; }
                AudioSource source = hub.GetComponent<AudioSource>();
                Check("звуковые сигналы синтезированы и проигрываются", audioOk && source != null,
                    audioOk ? "источник звука есть, 3D-blend " + source.spatialBlend : audioError);
                bool hapticOk = true;
                try
                {
                    hub.Haptics.Pulse(KvHapticStrength.Light, "дигност");
                    hub.Haptics.OnError();
                }
                catch (Exception e) { hapticOk = false; audioError = e.Message; }
                Check("вибрация-заготовка не падает без VR/MR", hapticOk,
                    "контроллеров найдено: " + hub.Haptics.ControllerCount);
                Next(0);
                break;

            // ---------------------------------------------------------- этап 10: гриппер
            case 7:
                Check("захват собран на роботе", hub.Gripper.Attached,
                    hub.Gripper.Attached ? "TCP: " + hub.Gripper.GraspPoint : "нет");
                hub.Gripper.SetOpen(true);
                Next(0);
                break;

            case 8:
                gripperOpenWidth = hub.Gripper.Width;
                hub.Gripper.SetOpen(false);
                Next(200);          // в batch-режиме deltaTime крошечный: меряем по факту смыкания
                break;

            case 9:
                Check("пальцы захвата смыкаются", hub.Gripper.Width < gripperOpenWidth - 0.005f,
                    "раскрытие: " + (gripperOpenWidth * 1000f).ToString("0") + " мм → " +
                    (hub.Gripper.Width * 1000f).ToString("0") + " мм");
                hub.Gripper.SetOpen(true);
                Next(0);
                break;

            // ---------------------------------------------------------- этап 5: зоны запрета
            case 10:
                Vector3 basePoint = Vector3.zero;
                if (flow != null && flow.Validator != null && flow.Validator.Ready)
                    basePoint = flow.Validator.BasePosition;
                Vector3 zoneCenter = basePoint + new Vector3(0.55f, 0.30f, 0.20f);
                zonesAtStart = hub.Zones.Count;

                box = hub.Zones.Create(KvZoneShape.Box, zoneCenter, new Vector3(0.25f, 0.25f, 0.25f), "КубТест");
                sphere = hub.Zones.Create(KvZoneShape.Sphere, zoneCenter + new Vector3(0.8f, 0f, 0f),
                    new Vector3(0.15f, 0.15f, 0.15f), "СфераТест");
                cylinder = hub.Zones.Create(KvZoneShape.Cylinder, zoneCenter + new Vector3(0f, 0f, 0.8f),
                    new Vector3(0.12f, 0.35f, 0.12f), "ЦилиндрТест");
                expectedZones = zonesAtStart + 3;

                Check("зоны запрета трёх форм созданы", hub.Zones.Count == expectedZones,
                    "зон: " + hub.Zones.Count + " (было " + zonesAtStart + ")");
                Check("куб содержит свой центр", box != null && box.ContainsPoint(zoneCenter), "точка центра");
                Check("куб не содержит далёкую точку",
                    box != null && !box.ContainsPoint(zoneCenter + Vector3.up * 2f), "+2 м по Y");
                Check("сфера содержит свой центр",
                    sphere != null && sphere.ContainsPoint(zoneCenter + new Vector3(0.8f, 0f, 0f)), "центр сферы");
                Check("цилиндр содержит свой центр",
                    cylinder != null && cylinder.ContainsPoint(zoneCenter + new Vector3(0f, 0f, 0.8f)), "центр цилиндра");
                Check("ZoneAt находит зону по точке", hub.Zones.ZoneAt(zoneCenter) != null,
                    hub.Zones.ZoneAt(zoneCenter) != null
                        ? hub.Zones.ZoneAt(zoneCenter).ZoneName : "не найдена");
                Collider[] zoneColliders = box != null ? box.GetComponentsInChildren<Collider>(true)
                    : new Collider[0];
                bool anyActiveCollider = false;
                for (int i = 0; i < zoneColliders.Length; i++)
                    if (zoneColliders[i] != null && zoneColliders[i].enabled) anyActiveCollider = true;
                Check("у зоны нет активного коллайдера (мир столкновений не тронут)", !anyActiveCollider,
                    "коллайдеров всего " + zoneColliders.Length + ", активных " +
                    (anyActiveCollider ? "есть" : "нет"));
                Next(0);
                break;

            case 11:
                Vector3 before = box.Data.Center;
                hub.Zones.Move(box, before + Vector3.up * 0.1f);
                hub.Zones.Resize(box, new Vector3(0.4f, 0.4f, 0.4f));
                Check("зону можно переместить",
                    (box.Data.Center - (before + Vector3.up * 0.1f)).magnitude < 0.001f,
                    "центр: " + box.Data.Center);
                Check("зону можно изменить по размеру",
                    Mathf.Abs(box.Data.Size.x - 0.4f) < 0.001f, box.Data.SizeText);
                Next(0);
                break;

            // ---------------------------------------------------------- этап 2: позы
            case 12:
                pose = hub.Poses.SaveCurrent("ДигностПоза");
                Check("поза сохранена", pose != null && hub.Poses.Count > 0,
                    pose != null ? pose.name + " · осей " + pose.q.Length : "не сохранилась");
                Check("файл позы создан", pose != null && File.Exists(pose.filePath),
                    pose != null ? pose.filePath : "—");
                Next(0);
                break;

            case 13:
                List<KvPosePreset> forRobot = hub.Poses.ForCurrentRobot();
                Check("позы фильтруются по активному роботу", forRobot.Count > 0,
                    "поз для робота: " + forRobot.Count);
                string moveResult = hub.Poses.MoveTo(pose);       // переезд «в себя»: путь нулевой длины
                info++;
                Line("  [info] переезд в позу: " + moveResult);
                Next(20);
                break;

            // ---------------------------------------------------------- этап 15: Undo / Redo
            case 14:
                if (flow != null && flow.Validator != null && flow.Validator.Ready)
                {
                    flow.EndExternalMotion();
                    if (flow.Motion != null && flow.Motion.IsRunning) flow.Motion.Stop();
                    if (flow.State.phase != FlowState.Idle) flow.ResetFlow("дигност: чистая фаза");

                    // Точку ищем ПЕРЕБОРОМ по сетке вокруг базы (радиус × направление × высота):
                    // так проверка не зависит от того, куда «смотрит» рабочая зона этого робота
                    // и какая у него длина звеньев. Первая же точка, где IK сходится и поза
                    // в лимитах, используется для проверки планирования/Undo.
                    // Опорная точка для перебора — БАЗА РОБОТА, а не BasePosition: у 6-осевого
                    // робота BasePosition до этой сессии возвращал (0,0,0) (робот стоит на столе
                    // в z ≈ −24), поэтому перебор вокруг него уходил на 24 м от руки.
                    Vector3 basePos = flow.Robot != null ? flow.Robot.transform.position
                        : flow.Validator.BasePosition;
                    info++;
                    Line("  [info] опора перебора: transform робота " + basePos +
                         " · BasePosition " + flow.Validator.BasePosition);
                    float[] radii = { 0.6f, 1.0f, 1.4f, 1.8f, 2.2f };
                    float[] heights = { 0.10f, 0.45f, 0.85f };
                    Vector3[] dirs =
                    {
                        new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f),
                        new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f),
                        new Vector3(0.7f, 0f, -0.7f), new Vector3(-0.7f, 0f, -0.7f),
                        new Vector3(0.7f, 0f, 0.7f), new Vector3(-0.7f, 0f, 0.7f)
                    };

                    Vector3 target = Vector3.zero;
                    bool found = false;
                    int tried = 0;
                    float bestError = float.MaxValue;

                    // Диагностика кинематики: если IK не сходится нигде — показываем состояние
                    // робота (поза, лимиты, TCP, включён ли компонент) прямо в отчёте.
                    double[] cur = flow.Validator.CopyCurrent();
                    Vector3 curTcp = flow.Validator.TcpAt(cur);
                    StringBuilder dq = new StringBuilder();
                    for (int i = 0; i < cur.Length; i++)
                        dq.Append(cur[i].ToString("0.0")).Append(i + 1 < cur.Length ? "/" : "");
                    info++;
                    Line("  [info] робот: enabled=" + (flow.Robot != null ? flow.Robot.enabled.ToString() : "?") +
                         " · selfCollision=" + (flow.Robot as SixAxisController != null
                             ? (flow.Robot as SixAxisController).SelfCollisionBlocked.ToString() : "—"));
                    Line("  [info] текущая поза: " + dq + " · TCP " + curTcp +
                         " · база " + flow.Validator.BasePosition + " · Dof " + flow.Validator.Dof);

                    double[] probeQ;
                    bool probeIk = flow.Validator.SolveIk(basePos + new Vector3(0f, 0.45f, -1.0f),
                        cur, out probeQ);
                    Line("  [info] пробный IK в базу+(0,0.45,−1.0): " + probeIk +
                         " · ошибка " + (flow.Validator.LastIkError * 1000f).ToString("0") + " мм");

                    for (int r = 0; r < radii.Length && !found; r++)
                    {
                        for (int d = 0; d < dirs.Length && !found; d++)
                        {
                            for (int h = 0; h < heights.Length && !found; h++)
                            {
                                Vector3 probe = basePos + dirs[d].normalized * radii[r] +
                                                Vector3.up * heights[h];
                                tried++;
                                double[] q;
                                bool ik = flow.Validator.SolveIk(probe, flow.Validator.CopyCurrent(), out q);
                                if (ik && flow.Validator.LastIkError < bestError)
                                    bestError = flow.Validator.LastIkError;
                                if (ik && q != null && flow.Validator.WithinLimits(q))
                                {
                                    target = probe;
                                    found = true;
                                }
                            }
                        }
                    }
                    Check("найдена достижимая точка для проверки планирования", found,
                        found ? "перебрано " + tried + " точек · цель " + target
                              : "IK не сошлась ни в одной из " + tried +
                                " точек (лучшая ошибка " + (bestError * 1000f).ToString("0") + " мм)");

                    bool locked = found && flow.LockPointFromUi(target, target, Vector3.up, false);
                    lastTarget = target;
                    Check("точка зафиксирована из интерфейса (вход для Undo)", locked,
                        locked ? target.ToString() : "не удалось");
                    Next(6);
                    break;
                }
                Next(2);
                break;

            case 15:
                Check("смена точки попала в историю отмен", hub.Undo.CanUndo,
                    "в истории: " + hub.Undo.UndoCount + " · следующая: " + hub.Undo.NextUndoTitle);
                bool undone = hub.Undo.Undo();
                Check("отмена выполнена", undone, "отменено: " + hub.Undo.NextRedoTitle);
                Next(4);
                break;

            case 16:
                Check("после отмены робот не в RobotMoving",
                    flow.State.phase != FlowState.RobotMoving, "фаза: " + flow.State.phase);
                bool redone = hub.Undo.Redo();
                Check("повтор выполнен", redone, "в истории: " + hub.Undo.UndoCount);
                Next(2);
                break;

            // ---------------------------------------------------------- этап 14: замер планировщика
            case 17:
                // Планирование идёт тайм-слайсами: в batch-режиме кадры «короткие», поэтому
                // ждём не число кадров, а окончание генерации. ВАЖНО: это ожидание НЕ двигает
                // шаг (иначе проверка планирования была бы пропущена).
                if (flow != null && flow.Generating && Time.frameCount < 900)
                {
                    Wait(20);
                    break;
                }
                planned = flow != null && flow.State.candidates.Count > 0;
                Check("планирование после правок работает (есть траектории)", planned,
                    planned ? "вариантов: " + flow.State.candidates.Count +
                               " · фаза " + flow.State.phase
                            : "нет вариантов (точка могла быть недостижима) · фаза " +
                              (flow != null ? flow.State.phase.ToString() : "?"));
                if (!planned && flow != null && flow.State.candidates.Count == 0 &&
                    flow.State.phase == FlowState.Idle && retryPlan < 3)
                {
                    // Точка могла «не досчитаться» — повторяем фиксацию до трёх раз.
                    retryPlan++;
                    flow.LockPointFromUi(lastTarget, lastTarget, Vector3.up, false);
                    Wait(30);
                    break;
                }
                List<string> perfLines = hub.Performance.Lines();
                Check("панель замера планировщика наполняется", perfLines.Count > 3,
                    perfLines.Count > 0 ? perfLines[0] : "пусто");
                info++;
                Line("  [info] замер: " + KvPlannerPerformance.Summary(hub.Performance.Last));
                Next(0);
                break;

            case 18:
                if (planned)
                {
                    Check("сессия замера записана после генерации", hub.Performance.Last != null,
                        "прогонов: " + hub.Performance.History.Count +
                        " · среднее " + hub.Performance.AverageSeconds().ToString("0.000") + " с");
                }
                else
                {
                    info++;
                    Line("  [info] замер не проверялся: траекторий не было");
                }
                Next(0);
                break;

            // ---------------------------------------------------------- этап 6: сравнение и этап 7: графики
            case 19:
                if (planned && flow.State.candidates.Count >= 2)
                {
                    string markA = hub.Comparison.Mark(0);
                    string markB = hub.Comparison.Mark(1);
                    Check("две траектории отмечены для сравнения (A и B)", hub.Comparison.Ready,
                        markA + "; " + markB);
                    List<string[]> rows = hub.Comparison.SideBySide();
                    Check("метрики бок о бок построены", rows.Count >= 8,
                        "строк: " + rows.Count + (rows.Count > 2 ? " · " + rows[0][0] + ": " +
                        rows[2][1] + " / " + rows[2][2] : ""));
                    info++;
                    Line("  [info] вывод сравнения: " + hub.Comparison.Verdict());
                }
                else
                {
                    info++;
                    Line("  [info] сравнение не проверялось: траекторий меньше двух");
                }
                Next(0);
                break;

            case 20:
                if (planned)
                {
                    GameObject host = new GameObject("GraphTest", typeof(RectTransform));
                    KvJointGraph graph = host.AddComponent<KvJointGraph>();
                    graph.Build((RectTransform)host.transform, 600f, 240f);
                    TrajectoryCandidate candidate = flow.State.candidates[0];
                    if (hub.Comparison.Ready && flow.State.candidates.Count >= 2)
                        graph.SetPlanPair(candidate.plan, "A",
                            flow.State.candidates[1].plan, "B");
                    else
                        graph.SetPlan(candidate.plan, candidate.label);
                    Check("график углов построен по траектории", graph.HasData, "линий: суставов");
                    UnityEngine.Object.Destroy(host);
                }
                else
                {
                    info++;
                    Line("  [info] график не проверялся: траекторий нет");
                }
                Next(0);
                break;

            // ---------------------------------------------------------- этап 9: тепловая карта зазоров
            case 21:
                if (planned)
                {
                    hub.Clearance.SetVisible(true);
                    hub.Clearance.Rebuild(true);
                    Check("тепловая карта зазоров построена",
                        !string.IsNullOrEmpty(hub.Clearance.LastSummary), hub.Clearance.LastSummary);
                    hub.Clearance.SetVisible(false);
                }
                else
                {
                    info++;
                    Line("  [info] карта зазоров не проверялась: траекторий нет");
                }
                Next(0);
                break;

            // ---------------------------------------------------------- этап 8: тепловая карта достижимости
            case 22:
                hub.Heatmap.SetVisible(true);
                Next(40);
                break;

            case 23:
                Check("тепловая карта достижимости строится",
                    hub.Heatmap.BuiltCount > 0 || hub.Heatmap.Building,
                    hub.Heatmap.Building ? "идёт построение" : hub.Heatmap.LastSummary);
                Next(60);
                break;

            case 24:
                Check("построение карты достижимости завершилось", hub.Heatmap.BuiltCount > 0,
                    hub.Heatmap.LastSummary);
                hub.Heatmap.SetVisible(false);
                Next(0);
                break;

            // ---------------------------------------------------------- этап 1: запись и воспроизведение
            case 25:
                hub.Window.Show(0);
                bool started = hub.Recording.StartRecording("ДигностЗапись", "live");
                Check("запись начата", started && hub.Recording.IsRecording,
                    started ? "сэмплов: " + hub.Recording.Recording.SampleCount : "не началась");
                Next(250);          // 250 кадров batch-режима ≈ 1 c реального времени
                break;

            case 26:
                int samplesBefore = hub.Recording.Recording.SampleCount;
                Check("сэмплы пишутся (углы + время + TCP)", samplesBefore >= 8,
                    "сэмплов за 250 кадров: " + samplesBefore +
                    " · длительность " + hub.Recording.Recording.duration.ToString("0.00") + " с");
                KvRecordSample first = hub.Recording.Recording.samples[0];
                Check("в сэмпле есть углы суставов и TCP",
                    first.q != null && first.q.Length > 0 && first.tcp != null && first.tcp.Length == 3,
                    "осей: " + (first.q != null ? first.q.Length : 0) + " · TCP: " + first.TcpVector);
                Next(0);
                break;

            case 27:
                // подвигаем робота руками (слайдеры) — запись должна увидеть движение
                if (flow != null && flow.Validator != null && flow.Validator.Ready)
                {
                    double[] q = flow.Validator.CopyCurrent();
                    if (q != null && q.Length > 0)
                    {
                        double[] moved = (double[])q.Clone();
                        moved[0] += 4.0;
                        if (flow.Validator.WithinLimits(moved))
                        {
                            flow.Validator.Apply(moved);
                            hub.Recording.SampleNow(true);
                            info++;
                            Line("  [info] робот сдвинут вручную на 4° (этап 3: ручное управление)");
                        }
                    }
                }
                Next(10);
                break;

            case 28:
                record = hub.Recording.StopRecording(true);
                recordPath = record != null ? record.filePath : null;
                Check("запись остановлена и сохранена", record != null && record.SampleCount >= 2,
                    record != null ? record.SampleCount + " сэмплов · " + record.duration.ToString("0.00") +
                                     " с · путь " + record.length.ToString("0.000") + " м" : "нет записи");
                Check("файл записи создан", !string.IsNullOrEmpty(recordPath) && File.Exists(recordPath),
                    recordPath ?? "—");
                Next(0);
                break;

            case 29:
                KvTrajectoryRecord loaded = !string.IsNullOrEmpty(recordPath)
                    ? KvRecordStore.Load(recordPath) : null;
                Check("запись читается с диска", loaded != null && loaded.SampleCount >= 2,
                    loaded != null ? loaded.ShortLabel : "не прочиталась");
                Check("запись в списке записей", KvRecordStore.LoadAll().Count > 0,
                    "записей: " + KvRecordStore.LoadAll().Count);
                Next(0);
                break;

            case 30:
                bool playing = hub.Recording.Play(record, 2f);
                Check("воспроизведение запущено с множителем ×2", playing && hub.Recording.IsPlaying,
                    playing ? "скорость ×" + hub.Recording.SpeedMultiplier.ToString("0.00") : "не запустилось");
                Next(20);
                break;

            case 31:
                Check("воспроизведение идёт (прогресс растёт)",
                    hub.Recording.Progress01 > 0.001f,
                    "прогресс " + (hub.Recording.Progress01 * 100f).ToString("0") + " % · осталось " +
                    hub.Recording.RemainingSeconds.ToString("0.0") + " с");
                hub.Recording.SetPaused(true);
                info++;
                Line("  [info] пауза воспроизведения: " + hub.Recording.Paused);
                Next(5);
                break;

            case 32:
                pausedProgress = hub.Recording.Progress01;
                Next(15);
                break;

            case 33:
                Check("пауза держит прогресс",
                    Mathf.Abs(hub.Recording.Progress01 - pausedProgress) <= 0.001f,
                    "прогресс замер на " + (pausedProgress * 100f).ToString("0") + " %");
                hub.Recording.StopPlayback("проверка окончена");
                Check("воспроизведение остановлено", !hub.Recording.IsPlaying, "остановлено");
                Next(0);
                break;

            // ---------------------------------------------------------- этап 12: ETA
            case 34:
                bool etaWorked = false;
                if (flow != null && flow.Validator != null && flow.Validator.Ready)
                {
                    flow.EndExternalMotion();
                    if (flow.Motion != null && flow.Motion.IsRunning) flow.Motion.Stop();

                    // Целевая поза — сдвиг сустава 2 на 12° (или −12°, если упирается в лимит):
                    // так проверка не зависит от того, совпала ли сохранённая поза с текущей.
                    double[] current = flow.Validator.CopyCurrent();
                    if (current != null && current.Length > 1)
                    {
                        double[] goal = (double[])current.Clone();
                        goal[1] += 12.0;
                        if (!flow.Validator.WithinLimits(goal)) goal[1] = current[1] - 12.0;

                        if (flow.Validator.WithinLimits(goal))
                        {
                            double[] start = flow.Validator.ContinueFrom(current, goal);
                            PlannedTrajectory plan = KvPlanKit.MakeJointPlan(flow.Validator, hub.World,
                                start, goal, "Дигност: переезд сустава", 0.12f, 24);
                            etaWorked = plan != null &&
                                        flow.PlayExternalPlan(plan, goal, "Дигност: переезд сустава");
                            info++;
                            Line("  [info] план внешнего движения: " + (plan != null
                                ? "сэмплов " + plan.Path.Length + " · время " + plan.Time.ToString("0.00") +
                                  " с · зазор " + (plan.MinClearance * 1000f).ToString("0") + " мм"
                                : "не построен"));
                        }
                    }
                }
                Check("внешний план (поза/сценарий) запущен штатным исполнителем", etaWorked,
                    etaWorked ? "идёт движение" : "запуск отклонён (Safety/лимиты) либо робот не готов");
                Next(10);
                break;

            case 35:
                Check("панель ETA показывает остаток времени", hub.EtaVisible || hub.EtaTotal > 0f,
                    "видима: " + hub.EtaVisible + " · осталось " + hub.EtaRemaining.ToString("0.0") +
                    " с из " + hub.EtaTotal.ToString("0.0") + " с · прогресс " +
                    (hub.EtaProgress * 100f).ToString("0") + " %");
                info++;
                Line("  [info] прогресс исполнителя: " +
                     (flow.Executor != null
                         ? "running " + flow.Executor.IsRunning + " · " + flow.Executor.Progress01.ToString("0.00")
                         : "исполнитель ещё не создан"));
                Next(0);
                break;

            // ---------------------------------------------------------- этап 4: аварийная остановка
            case 36:
                hub.EmergencyStop();
                Next(3);
                break;

            case 37:
                Check("аварийная остановка: движение остановлено",
                    flow.Motion == null || !flow.Motion.IsRunning, "Motion.IsRunning = " +
                    (flow.Motion != null ? flow.Motion.IsRunning.ToString() : "?"));
                Check("аварийная остановка: поток вернулся в Idle",
                    flow.State.phase == FlowState.Idle, "фаза: " + flow.State.phase);
                Check("аварийная остановка записана в журнал", LogHas("АВАРИЙНАЯ ОСТАНОВКА"),
                    "ищем «АВАРИЙНАЯ ОСТАНОВКА» в журнале");
                Check("аварийная остановка: запись/воспроизведение остановлены",
                    !hub.Recording.IsPlaying && !hub.Recording.IsRecording, "остановлено");
                Next(0);
                break;

            // ---------------------------------------------------------- этап 18: сценарии
            case 38:
                Check("сценарии зарегистрированы (4 шт.)", hub.Scenarios.All.Count == 4,
                    "сценариев: " + hub.Scenarios.All.Count + " · " +
                    (hub.Scenarios.All.Count > 0 ? hub.Scenarios.All[0].Title : "—"));
                bool startedScenario = hub.Scenarios.Start("show.limits");
                Check("сценарий «Показать лимиты» запущен", startedScenario && hub.Scenarios.Running,
                    startedScenario ? "шаг: " + hub.Scenarios.CurrentStepText : "не запустился");
                Next(10);
                break;

            case 39:
                bool pausedOk = hub.Scenarios.TogglePause();
                Check("сценарий можно поставить на паузу", pausedOk && hub.Scenarios.Paused, "пауза");
                hub.Scenarios.Stop("проверка окончена");
                Check("сценарий можно отменить", !hub.Scenarios.Running, "остановлен");
                Next(0);
                break;

            // ---------------------------------------------------------- этап 19: презентационный режим
            case 40:
                bool entered = hub.Presentation.Enter();
                Check("презентационный режим включается", entered && hub.Presentation.Active,
                    entered ? "кадр: " + hub.Presentation.CurrentLine : "не включился");
                Next(5);
                break;

            case 41:
                Check("в презентационном режиме строка рассказа не пустая",
                    !string.IsNullOrEmpty(hub.Presentation.CurrentLine), hub.Presentation.CurrentLine);
                Next(5);
                break;

            case 42:
                hub.Presentation.Exit();
                Check("презентационный режим выключается", !hub.Presentation.Active, "выключен");
                Next(0);
                break;

            // ---------------------------------------------------------- этап 20: сессии
            case 43:
                KvSession session = hub.Sessions.Save("ДигностСессия");
                Check("сессия сохранена", session != null && File.Exists(session.filePath),
                    session != null ? session.Summary : "не сохранилась");
                Check("в сессии есть позы роботов и зоны",
                    session != null && session.robots.Count > 0 && session.zones.Count > 0,
                    session != null ? "роботов " + session.robots.Count + " · зон " + session.zones.Count +
                                      " · точек " + session.points.Count : "—");
                Next(0);
                break;

            case 44:
                int zonesBeforeLoad = hub.Zones.Count;
                hub.Zones.Clear();
                int afterClear = hub.Zones.Count;
                KvSession newest = hub.Sessions.Newest;
                bool sessionRestored = hub.Sessions.Load(newest);
                Check("сессия загружается (зоны восстанавливаются)",
                    sessionRestored && hub.Zones.Count == expectedZones,
                    "зон до очистки: " + zonesBeforeLoad + " · после очистки: " + afterClear +
                    " · после загрузки: " + hub.Zones.Count + " (ожидалось " + expectedZones + ")");
                Next(0);
                break;

            case 45:
                Check("журнал содержит события всех типов",
                    LogHas("точка выбрана") || LogHas("траектория подтверждена") || LogHas("запись"),
                    "записей в журнале: " + hub.Log.Count);
                Check("сводка хаба собрана", !string.IsNullOrEmpty(hub.Dump()), hub.Dump());
                Finish();
                break;

            default:
                Finish();
                break;
        }
    }

    private static int zonesAtStart;
    private static int expectedZones;
    private static float pausedProgress;
    private static float gripperOpenWidth;
    private static int retryPlan;
    private static Vector3 lastTarget;

    /// <summary>Подождать N игровых кадров, НЕ двигая шаг (для повторных попыток внутри шага).</summary>
    private static void Wait(int frames)
    {
        waitFrames = frames;
    }

    private static void Next(int wait)
    {
        step++;
        waitFrames = wait;
    }

    private static bool LogHas(string needle)
    {
        if (hub == null) return false;
        for (int i = 0; i < hub.Log.Count; i++)
            if (hub.Log.Entries[i].text.Contains(needle)) return true;
        return false;
    }

    private static void Check(string name, bool passed, string detail)
    {
        if (passed) ok++; else fail++;
        Line((passed ? "[OK]   " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : " — " + detail));
    }

    private static void Line(string text)
    {
        try
        {
            File.AppendAllText(ReportPath, text + Environment.NewLine, new UTF8Encoding(false));
        }
        catch (Exception) { }
        Debug.Log("[FeaturesDiag] " + text);
    }

    private static void Finish()
    {
        if (finished) return;
        finished = true;
        Line("");
        Line("ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · [info] " + info);
        Line(fail == 0 ? "ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ИТОГ: ЕСТЬ ПРОВАЛЫ (" + fail + ")");
        SessionState.SetBool(SessionKey, false);
        EditorApplication.update -= Tick;

        if (hub != null)
        {
            hub.Log.Add(KvLogKind.System, "диагностический прогон этапов 1–20 завершён: OK " + ok +
                                          ", FAIL " + fail);
        }
        Debug.Log("[FeaturesDiag] ИТОГ: OK " + ok + " · FAIL " + fail);
        EditorApplication.Exit(fail == 0 ? 0 : 1);
    }
}
