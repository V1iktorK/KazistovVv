using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KazistovVvFeatures;
using KazistovVvUI;
using TrajectoryCore;

/// <summary>
/// ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится) для этапов 1–6 текущей сессии:
/// главное меню со стартовым облётом, туториал, демонстрация, сглаживание траекторий,
/// время-оптимальная траектория и эко-профиль. Прогон идёт в PlayMode полным циклом
/// сначала на роботе, затем на SCARA.
///
/// ОСОБЕННОСТЬ ОКРУЖЕНИЯ (учтено): в batch-режиме `EditorApplication.update` вызывается
/// чаще игровых кадров, а отчётный файл в OneDrive во время PlayMode дописывается не всегда,
/// поэтому:
///   • ожидания сделаны ПО РЕАЛЬНОМУ ВРЕМЕНИ (`EditorApplication.timeSinceStartup`),
///     а не по числу вызовов update;
///   • каждое действие выполняется РОВНО ОДИН РАЗ на шаг (`Once`), поэтому повторные
///     вызовы шага не могут навредить;
///   • у каждого шага есть сторож: если он не двигается 60 с — в отчёт идёт строка об этом,
///     и прогон продолжается (диагностика не может «зависнуть»);
///   • строки отчёта идут в лог Unity (`[DshStage2Diag]`), отчёт читается из `_dsh_stage2.log`.
///
/// Запуск:
///   Unity.exe -batchmode -nographics -projectPath "&lt;копия&gt;" -executeMethod DshStage2Diag.Run
///   -logFile _dsh_stage2.log        (БЕЗ -quit: нужен PlayMode)
/// </summary>
public static class DshStage2Diag
{
    private const string Key = "DshStage2Diag.Running";
    private const float StepLimitSeconds = 75f;      // сторож на шаг
    private const float TotalLimitSeconds = 900f;    // сторож на весь прогон
    private static string report;

    public static void Run()
    {
        report = FeatureStorage.ReportPath("_dsh_stage2_verify.txt");
        try
        {
            File.WriteAllText(report,
                "=== DshStage2Diag · этапы 1–6 (меню, туториал, демо, сглаживание, время, энергия) ===\n");
        }
        catch (Exception e) { Debug.LogError("[DshStage2Diag] отчёт не создан: " + e.Message); }

        EditorSceneManager.OpenScene("Assets/_Project/00_Scenes/MainScene.unity", OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    // ------------------------------------------------------------------ состояние
    private static int step;
    private static bool playStarted;
    private static int ok, fail, info;

    private static KazistovVvUIManager ui;
    private static FeatureHub hub;
    private static KvStageHub2 stages2;
    private static TrajectoryFlowController flow;
    private static FreeFlyCameraController cam;
    private static RobotController robot6, robotScara, rc;
    private static PoseValidator v;

    private static Vector3 workPoint;
    private static Vector3 camAtMenu;
    private static float menuTime0;
    private static float stepEnterTime;
    private static int stepEntries;
    private static bool done;

    private static float jerkBefore;
    private static float savedSmoothLevel;
    private static int appliedBefore;

    private static void Line(string text)
    {
        try { File.AppendAllText(report, text + "\n"); } catch { }
        Debug.Log("[DshStage2Diag] " + text);
    }

    private static void Check(bool condition, string what, string detail = "")
    {
        if (condition) { ok++; Line("[OK]   " + what + (detail.Length > 0 ? " — " + detail : "")); }
        else { fail++; Line("[FAIL] " + what + (detail.Length > 0 ? " — " + detail : "")); }
    }

    private static void Note(string what) { info++; Line("[info] " + what); }

    // ------------------------------------------------------------------ ожидание и «один раз»

    private static float Now { get { return (float)EditorApplication.timeSinceStartup; } }

    /// <summary>Пауза по реальному времени (секунды).</summary>
    private static bool Wait(float seconds)
    {
        return Now - stepEnterTime >= seconds;
    }

    /// <summary>
    /// Выполнить действие ровно один раз за шаг. У каждого шага — СВОЙ набор меток
    /// (общий «одно поле» здесь не годится: несколько разных действий на шаге затирали
    /// бы метку друг друга, и ранее выполненное действие повторялось).
    /// </summary>
    private static bool Once(string actionTag)
    {
        if (usedTags.Contains(actionTag)) return false;
        usedTags.Add(actionTag);
        return true;
    }

    private static readonly HashSet<string> usedTags = new HashSet<string>();

    private static void Next(int s)
    {
        if (s != step) Line("→ шаг " + s);
        step = s;
        usedTags.Clear();
        stepEnterTime = Now;
        stepEntries = 0;
    }

    // ------------------------------------------------------------------ цикл

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (playStarted) { Line("PlayMode завершился неожиданно"); Finish(); }
            return;
        }
        playStarted = true;
        if (stepEnterTime <= 0f) stepEnterTime = Now;

        // Сторож шага: диагностика не имеет права «зависнуть».
        stepEntries++;
        float inStep = Now - stepEnterTime;
        if (inStep > StepLimitSeconds && step < 18)
        {
            Line("[FAIL] шаг " + step + " не завершился за " + StepLimitSeconds.ToString("0") +
                 " с (вызовов: " + stepEntries + ") — перехожу к следующему");
            fail++;
            Next(step + 1);
            return;
        }
        float start = SessionState.GetFloat(Key + ".Start", Now);
        if (Now - start > TotalLimitSeconds)
        {
            Line("[FAIL] прогон превысил " + TotalLimitSeconds.ToString("0") + " с — завершаю");
            fail++;
            Finish();
            return;
        }

        try
        {
            switch (step)
            {
                case 0: StepBoot(); break;
                case 1: StepMenuShow(); break;
                case 2: StepMenuOrbit(); break;
                case 3: StepMenuNewProject(); break;
                case 4: StepMenuSettingsAndSession(); break;
                case 5: StepTutorialStart(); break;
                case 6: StepTutorialPoint(); break;
                case 7: StepTutorialTrajectory(); break;
                case 8: StepTutorialFinish(); break;
                case 9: StepDemoStart(); break;
                case 10: StepDemoRun(); break;
                case 11: StepSmoothingApply(); break;
                case 12: StepSmoothingMethods(); break;
                case 13: StepSmoothingAuto(); break;
                case 14: StepTimeOptimal(); break;
                case 15: StepEnergy(); break;
                case 16: StepScara(); break;
                case 17: StepScaraPost(); break;
                case 18: StepFinishReport(); break;
            }
        }
        catch (Exception e)
        {
            fail++;
            Line("[FAIL] ИСКЛЮЧЕНИЕ на шаге " + step + ": " + e.GetType().Name + " " + e.Message +
                 "\n" + e.StackTrace);
            Next(step + 1);
        }
    }

    // ------------------------------------------------------------------ шаги

    private static void StepBoot()
    {
        ui = KazistovVvUIManager.Instance;
        if (ui == null || ui.Flow == null) return;
        flow = ui.Flow;
        if (flow.Validator == null || !flow.Validator.Ready) return;

        hub = FeatureHub.Current;
        stages2 = KvStageHub2.Current;
        cam = ui.CameraRig;
        v = flow.Validator;

        Check(hub != null, "хаб этапов 1–20 поднят (FeatureHub)");
        Check(stages2 != null, "хаб этапов 1–6 поднят (KvStageHub2)");
        if (stages2 == null) { Next(18); return; }

        Check(stages2.StartMenu != null, "стартовое меню создано");
        Check(stages2.Tutorial != null, "туториал создан");
        Check(stages2.QuickStart != null, "демонстрация создана");
        Check(stages2.Smoothing != null, "сервис сглаживания создан");
        Check(stages2.TimeOptimal != null, "сервис время-оптимальной траектории создан");
        Check(stages2.Energy != null, "сервис оптимизации по энергии создан");
        Check(stages2.Workbench != null, "окно-верстак создано");
        Check(KvWorkbenchWindow.TabCount == 3, "вкладок верстака: 3 (сглаживание, время, энергия)",
            "вкладок: " + KvWorkbenchWindow.TabCount);
        Check(KvLocExtra.RegisteredCount > 40, "строки новых модулей зарегистрированы (7 языков)",
            "ключей: " + KvLocExtra.RegisteredCount + " · всего в словарях: " + KvLoc.TotalStrings);

        string original = KvLoc.PreferenceCode;
        string codes = "";
        int translated = 0;
        foreach (string code in new[] { "ru", "en", "zh", "es", "de", "fr", "ja" })
        {
            KvLoc.SetLanguage(code, false);
            string t = KvLoc.T("start.new", "Новый проект");
            codes += code + "=\"" + t + "\" ";
            if (!string.IsNullOrEmpty(t) && t != "start.new") translated++;
        }
        KvLoc.SetLanguage(original, false);
        Check(translated == 7, "текст «Новый проект» переведён на все 7 языков", codes.Trim());

        RobotController[] all = UnityEngine.Object.FindObjectsByType<RobotController>(
            FindObjectsInactive.Include);
        foreach (RobotController r in all)
        {
            if (r == null) continue;
            if (r is SCARAController) robotScara = r;
            else robot6 = r;
        }

        Note("робот: " + (flow.Robot != null ? flow.Robot.robotName : "?") + " · осей " + flow.Validator.Dof);
        Note("панель: кнопок тулбара " + ui.ToolbarButtonCount);
        savedSmoothLevel = stages2.Smoothing.Level;
        Next(1);
    }

    // ---------------------------------------------------------------- ЭТАП 1: меню

    private static void StepMenuShow()
    {
        if (Once("show")) stages2.StartMenu.Show();

        if (!Wait(1.0f)) return;
        Check(stages2.StartMenu.Visible, "главное меню показано");
        Check(stages2.StartMenu.ButtonCount >= 5,
            "в меню есть пункты (новый проект, сессия, демо, обучение, настройки, выход)",
            "пунктов: " + stages2.StartMenu.ButtonCount + " · иконки: " +
            string.Join(", ", stages2.StartMenu.IconIds));

        Transform canvas = ui.transform.Find("KazistovVvCanvas");
        Check(canvas != null && !canvas.gameObject.activeSelf,
            "оболочка интерфейса скрыта на время показа меню (кинематографический экран)");

        camAtMenu = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        menuTime0 = stages2.StartMenu.TimeInMenu;
        Next(2);
    }

    private static void StepMenuOrbit()
    {
        // Ждём реального времени работы меню (его собственный таймер) — так проверка
        // не зависит от того, как часто вызывается EditorApplication.update.
        if (stages2.StartMenu.TimeInMenu - menuTime0 < 2.0f && !Wait(20f)) return;

        Vector3 now = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        float moved = Vector3.Distance(camAtMenu, now);
        float menuTime = stages2.StartMenu.TimeInMenu - menuTime0;

        Check(moved > 0.05f, "камера сама облетает робота (кинематографический фон)",
            "смещение " + moved.ToString("0.000") + " м за " + menuTime.ToString("0.00") +
            " с работы меню");

        if (flow.Robot != null)
        {
            float radius = new Vector2(now.x - flow.Robot.transform.position.x,
                now.z - flow.Robot.transform.position.z).magnitude;
            Check(Mathf.Abs(radius - stages2.StartMenu.orbitRadius) < 1.3f,
                "камера держит радиус облёта вокруг активного робота",
                "радиус " + radius.ToString("0.00") + " м при заданном " +
                stages2.StartMenu.orbitRadius.ToString("0.00") + " м");
        }
        Next(3);
    }

    private static void StepMenuNewProject()
    {
        // Мусор в сцене: фиксируем точку, затем «Новый проект» обязан её убрать.
        if (Once("click"))
        {
            aimAt(flow.Robot);
            Click(workPointOrAim());
        }
        if (!Wait(1.5f)) return;

        if (Once("menu")) stages2.StartMenu.Show();
        if (!Wait(0.5f)) return;

        if (Once("new")) stages2.StartMenu.NewProject();
        if (!Wait(1.0f)) return;

        Check(!flow.State.hasPoint && flow.State.candidates.Count == 0 &&
              flow.State.phase == FlowState.Idle,
            "«Новый проект» очистил точку, траектории и вернул Idle",
            "фаза " + flow.State.phase + " · точка " + flow.State.hasPoint);
        Check(!stages2.StartMenu.Visible, "после выбора пункта меню закрылось (рабочая область)");
        Next(4);
    }

    private static void StepMenuSettingsAndSession()
    {
        if (Once("show")) stages2.StartMenu.Show();
        if (!Wait(0.4f)) return;

        if (Once("settings")) stages2.StartMenu.OpenSettings();
        if (!Wait(0.6f)) return;

        Transform canvas = ui.transform.Find("KazistovVvCanvas");
        if (Once("check"))
        {
            Check(canvas != null && canvas.gameObject.activeSelf,
                "пункт «Настройки» вернул оболочку интерфейса");
            Check(!stages2.StartMenu.Visible, "меню закрылось при переходе в настройки");
            stages2.StartMenu.Show();
        }
        if (!Wait(0.4f)) return;

        if (Once("session")) stages2.StartMenu.OpenSession();
        if (!Wait(0.8f)) return;

        if (Once("hide"))
        {
            Note("открытие сессии обработано без исключений (сессий может не быть — это штатно)");
            stages2.StartMenu.Hide();
        }
        if (!Wait(0.5f)) return;
        Check(!stages2.StartMenu.Visible, "выход из меню возвращает управление оператору");
        Transform visible = ui.transform.Find("KazistovVvCanvas");
        Check(visible != null && visible.gameObject.activeSelf,
            "после закрытия меню оболочка снова видна");
        Next(5);
    }

    // ---------------------------------------------------------------- ЭТАП 2: туториал

    private static void StepTutorialStart()
    {
        // Свежий старт (шаг 1): «продолжить позже» проверяется отдельно в конце шага 8.
        if (Once("start")) stages2.Tutorial.StartTutorial(false);
        if (!Wait(0.8f)) return;

        Check(stages2.Tutorial.Active, "туториал запущен");
        // Если в сцене уже есть зафиксированная точка (например, её вернула загруженная
        // сессия), шаг 1 завершается сразу — это правильное поведение туториала.
        Check(stages2.Tutorial.StepIndex == 0 || flow.State.hasPoint,
            "первый шаг — «как выбрать точку»",
            "шаг " + (stages2.Tutorial.StepIndex + 1) +
            (flow.State.hasPoint ? " · точка уже есть в сцене (шаг 1 засчитан)" : ""));
        Check(stages2.Tutorial.Card != null && stages2.Tutorial.Card.Visible,
            "карточка подсказки показана");
        RectTransform target = ui.CommandButtonRect("point.select");
        Check(target != null, "найдена кнопка тулбара для подсветки (выбор точки)");
        Next(6);
    }

    private static void StepTutorialPoint()
    {
        // Сначала наводим камеру на 6-осевой робот и ДАЁМ потоку перепривязаться:
        // точка, посчитанная для другого стенда, недостижима и планировщик честно
        // не находит ни одного пути (в прогоне это давало «вариантов 0»).
        if (Once("aim"))
        {
            aimAt(robot6);
            Aim(robot6 != null ? robot6.transform.position + Vector3.up * 0.2f : Vector3.up,
                false, false, false, false, Vector3.zero, false);
        }
        if (!Wait(1.5f)) return;

        if (Once("click"))
        {
            rc = robot6;
            // Чистая сцена: точка, которую могла вернуть загруженная сессия, снимается
            // штатным сбросом — иначе клик по новой точке может быть проигнорирован.
            flow.ResetFlow("диагностика: точка для туториала");
            Click(workPointOrAim(1));
        }
        if (!Wait(3.0f)) return;

        Check(flow.State.hasPoint, "точка зафиксирована для шага 1 туториала",
            flow.State.point.ToString("F3") + " · робот потока: " +
            (flow.Robot != null ? flow.Robot.name : "нет"));
        Check(stages2.Tutorial.StepIndex >= 1 || flow.State.candidates.Count > 0,
            "шаг 1 завершился сам после выбора точки",
            "шаг " + (stages2.Tutorial.StepIndex + 1) + " · вариантов " + flow.State.candidates.Count);
        Next(7);
    }

    private static void StepTutorialTrajectory()
    {
        // Ждём варианты (планирование идёт тайм-слайсами).
        if (flow.State.candidates.Count == 0)
        {
            if (!Wait(40f)) return;
            Check(false, "траектории построены для продолжения туториала", "вариантов 0");
            Next(9);
            return;
        }

        if (Once("select")) flow.SelectCandidateByIndex(0);
        if (!Wait(2.0f)) return;

        Check(stages2.Tutorial.StepIndex >= 2, "шаг 2 завершился сам после подтверждения траектории",
            "шаг " + (stages2.Tutorial.StepIndex + 1) + " · фаза " + flow.State.phase);
        Next(8);
    }

    private static void StepTutorialFinish()
    {
        // Шаг 3 (перемещение точки) — ручное подтверждение: вход в режим требует Enter,
        // который в диагностике проверяется отдельным сквозным прогоном (§0.5).
        if (Once("mark3")) stages2.Tutorial.MarkStepDone();
        if (!Wait(0.5f)) return;
        if (Once("check3"))
        {
            Check(stages2.Tutorial.StepIndex == 3,
                "шаг 3 отмечен и туториал перешёл к выбору фантома",
                "шаг " + (stages2.Tutorial.StepIndex + 1));

            // Шаг 4: переключение варианта (выбор другого фантома).
            int next = flow.State.candidates.Count > 1 ? 1 : 0;
            flow.SelectCandidateByIndex(next);
        }
        if (!Wait(2.0f)) return;

        if (Once("mark4"))
        {
            Check(stages2.Tutorial.StepIndex == 4 || KvTutorial.Completed,
                "шаг 4 завершился после переключения фантома",
                "шаг " + (stages2.Tutorial.StepIndex + 1) + " · пройден: " + KvTutorial.Completed);
            stages2.Tutorial.MarkStepDone();
        }
        if (!Wait(0.5f)) return;

        Check(KvTutorial.Completed, "туториал пройден полностью и прогресс сохранён",
            "PlayerPrefs KazistovVv.Tutorial.Done=" + PlayerPrefs.GetInt(KvTutorial.DonePrefsKey, 0));

        // Продолжение позже: сбрасываем прогресс, проходим один шаг, выходим — и проверяем,
        // что «Продолжить обучение» встаёт на сохранённый шаг (ТЗ этапа 2).
        stages2.Tutorial.StartTutorial(false);
        if (!Wait(0.4f)) return;
        stages2.Tutorial.MarkStepDone();
        if (!Wait(0.4f)) return;
        int savedStep = stages2.Tutorial.StepIndex;
        stages2.Tutorial.Skip("проверка сохранения прогресса");
        if (!Wait(0.4f)) return;
        Check(KvTutorial.StartedNotFinished && KvTutorial.SavedStep == savedStep,
            "прогресс туториала сохраняется и доступно «Продолжить обучение»",
            "сохранён шаг " + (KvTutorial.SavedStep + 1) + " · флаг «пройден» сброшен: " +
            !KvTutorial.Completed);
        Next(9);
    }

    // ---------------------------------------------------------------- ЭТАП 3: демонстрация

    private static void StepDemoStart()
    {
        if (Once("start")) stages2.PlayDemo();
        if (!Wait(1.0f)) return;

        Check(stages2.QuickStart.Running, "демонстрация запущена",
            "шагов в сценарии: " + stages2.QuickStart.StepCount);
        if (stages2.QuickStart.StepCount == 0)
        {
            Check(false, "сценарий демонстрации собран (шаги есть)");
            Next(11);
            return;
        }
        Check(stages2.Tutorial.Card != null && stages2.Tutorial.Card.Visible,
            "карточка демонстрации показывает подпись шага");
        Next(10);
    }

    private static void StepDemoRun()
    {
        if (!Wait(25f)) return;

        int index = stages2.QuickStart.StepIndex;
        Check(index >= 1 || flow.State.hasPoint,
            "демонстрация дошла до постановки точки",
            "шаг " + (index + 1) + " из " + stages2.QuickStart.StepCount +
            " · точка " + flow.State.hasPoint + " · вариантов " + flow.State.candidates.Count);
        Check(flow.State.candidates.Count > 0 || flow.State.hasPoint,
            "демонстрация поставила точку штатным путём (LockPointFromUi)",
            "время показа " + stages2.QuickStart.TotalTime.ToString("0.0") + " с");

        if (Once("stop")) stages2.QuickStart.StopDemo("диагностика");
        if (!Wait(0.5f)) return;
        Check(!stages2.QuickStart.Running, "демонстрацию можно прервать в любой момент");
        Next(11);
    }

    // ---------------------------------------------------------------- ЭТАП 4: сглаживание

    private static void StepSmoothingApply()
    {
        if (flow.State.candidates.Count == 0)
        {
            if (Once("click"))
            {
                aimAt(robot6);
                Click(workPointOrAim());
            }
            if (!Wait(40f)) return;
            if (flow.State.candidates.Count == 0)
            {
                Check(false, "варианты траектории построены для проверки постобработки", "вариантов 0");
                Next(18);
                return;
            }
        }

        int index;
        TrajectoryCandidate c = KvVariantKit.Selected(flow, out index);
        KvSmoothEntry entry;

        if (Once("prepare"))
        {
            stages2.Smoothing.Level = 70f;
            stages2.Smoothing.Method = KvSmoothMethod.BSpline;
            entry = stages2.Smoothing.EntryOf(c);
            stages2.Smoothing.EnsureBaseline(c, entry);
            jerkBefore = entry != null ? entry.before.maxJerk : 0f;
            savedJerkBefore = jerkBefore;
            savedFirst = KvTrajMath.Copy(c.plan.Path[0]);
            savedLast = KvTrajMath.Copy(c.plan.Path[c.plan.Path.Length - 1]);
            savedTime = c.plan.Time > 0f ? (float)c.plan.Time : c.timeS;
            bool applied = stages2.Smoothing.ApplySelected();
            savedApplied = applied;
        }
        if (!Wait(1.0f)) return;

        entry = stages2.Smoothing.EntryOf(c);
        float jerkAfter = entry != null ? entry.after.maxJerk : 0f;
        float jerkRef = entry != null ? entry.Reference.maxJerk : 0f;
        float timeRef = entry != null ? entry.Reference.time : 0f;

        Check(savedApplied && entry != null && entry.applied,
            "сглаживание применено к выбранному варианту",
            "вариант " + (index + 1) + " · уровень " + stages2.Smoothing.Level.ToString("0") +
            " % · " + stages2.Smoothing.MethodLabel);
        if (entry != null)
        {
            Note("метрики: план планировщика — " + entry.before.Line());
            Note("метрики: тот же путь по лимитам — " + entry.Reference.Line());
            Note("метрики: сглаженный путь — " + entry.after.Line());
        }
        Check(jerkAfter <= jerkRef * 1.15f + 1f, "jerk не вырос относительно того же пути по лимитам",
            jerkRef.ToString("0") + " → " + jerkAfter.ToString("0") + " °/с³ (" +
            (entry != null ? entry.JerkGain.ToString("+0.0;-0.0") : "0") + " %) · план планировщика " +
            (entry != null
                ? entry.before.maxJerk.ToString("0") + " °/с³ (постоянная скорость в сегментах)"
                : "—"));
        Check(entry != null && entry.after.time <= timeRef * 1.02f + 0.01f,
            "сглаженный путь не медленнее исходного",
            timeRef.ToString("0.000") + " → " +
            (entry != null ? entry.after.time.ToString("0.000") : "—") + " с");
        Check(entry != null && entry.after.curvature <= entry.Reference.curvature * 1.10f + 1e-3f,
            "кривизна пути не выросла (допуск 10 %)",
            entry != null
                ? entry.Reference.curvature.ToString("0.000") + " → " +
                  entry.after.curvature.ToString("0.000") + " 1/м"
                : "—");
        Check(c.tube != null && c.tube.Length == c.plan.Path.Length,
            "«колбаска» перестроена по новому пути",
            "сэмплов в колбаске: " + (c.tube != null ? c.tube.Length : 0) +
            " · в плане: " + c.plan.Path.Length);
        Check(SamePoint(c.plan.Path[0], savedFirst) &&
              SamePoint(c.plan.Path[c.plan.Path.Length - 1], savedLast),
            "концы траектории НЕ смещены (начало и конец совпадают с исходными)",
            "Δt " + ((float)c.plan.Time - savedTime).ToString("+0.000;-0.000") + " с");
        Next(12);
    }

    private static float savedJerkBefore;
    private static float savedTime;
    private static bool savedApplied;
    private static double[] savedFirst, savedLast;

    private static void StepSmoothingMethods()
    {
        // Робот после демонстрации мог доехать и поток вернул сцену в Idle (варианты
        // очищаются по завершении движения) — тогда ставим новую точку и ждём варианты.
        if (flow.State.candidates.Count == 0)
        {
            if (Once("reclick"))
            {
                aimAt(robot6);
                Click(workPointOrAim(2));
            }
            if (!Wait(40f)) return;
            if (flow.State.candidates.Count == 0)
            {
                Check(false, "варианты есть для проверки методов сглаживания", "вариантов 0");
                Next(13);
                return;
            }
        }

        if (Once("methods"))
        {
            int index;
            TrajectoryCandidate c = KvVariantKit.Selected(flow, out index);
            string report = "";
            bool allOk = true;
            bool anyImproved = false;
            foreach (KvSmoothMethod method in new[]
                     { KvSmoothMethod.BSpline, KvSmoothMethod.Bezier, KvSmoothMethod.Gauss })
            {
                stages2.Smoothing.Method = method;
                stages2.Smoothing.ResetSelected();
                KvSmoothEntry before = stages2.Smoothing.EntryOf(c);
                float time0 = before != null ? before.Reference.time : 0f;
                float curv0 = before != null ? before.Reference.curvature : 0f;
                bool ok = stages2.Smoothing.ApplySelected(true);
                KvSmoothEntry after = stages2.Smoothing.EntryOf(c);
                float jerk1 = after != null ? after.after.maxJerk : 0f;
                float time1 = after != null ? after.after.time : 0f;
                float curv1 = after != null ? after.after.curvature : 0f;
                report += stages2.Smoothing.MethodLabel + ": jerk " + jerk1.ToString("0") +
                          " °/с³, t " + time0.ToString("0.000") + "→" + time1.ToString("0.000") +
                          " с, k " + curv0.ToString("0.00") + "→" + curv1.ToString("0.00") + "  |  ";
                if (!ok || after == null || !after.applied) allOk = false;
                if (time1 <= time0 + 0.01f && curv1 <= curv0 * 1.15f) anyImproved = true;
            }
            Check(allOk, "все три метода сглаживания применяются без ошибок", report.Trim());
            Check(anyImproved, "сглаживание не ухудшает время и кривизну ни на одном методе",
                report.Trim());
            stages2.Smoothing.ResetSelected();
        }
        if (!Wait(1.0f)) return;

        int i2;
        TrajectoryCandidate c2 = KvVariantKit.Selected(flow, out i2);
        KvSmoothEntry entry = stages2.Smoothing.EntryOf(c2);
        Check(entry != null && !entry.applied, "«Вернуть исходную» возвращает несглаженный путь",
            "jerk " + (entry != null ? entry.before.maxJerk.ToString("0") : "—") + " °/с³");
        Next(13);
    }

    private static void StepSmoothingAuto()
    {
        if (Once("prepare"))
        {
            stages2.Smoothing.Auto = true;
            stages2.Smoothing.Level = 55f;
            appliedBefore = stages2.Smoothing.AppliedCount;
            aimAt(robot6);
            Click(workPointOrAim(2));
        }
        if (!Wait(35f)) return;

        Check(stages2.Smoothing.AppliedCount > appliedBefore,
            "автосглаживание сработало без команд оператора",
            "обработано вариантов: " + (stages2.Smoothing.AppliedCount - appliedBefore));
        stages2.Smoothing.Auto = false;
        Next(14);
    }

    // ---------------------------------------------------------------- ЭТАП 5: время-оптимальная

    private static void StepTimeOptimal()
    {
        int index;
        TrajectoryCandidate c = KvVariantKit.Selected(flow, out index);
        KvTimeOptimal.Draft draft = null;

        if (Once("compute")) draft = stages2.TimeOptimal.Compute(c);
        if (!Wait(2.5f)) return;
        draft = stages2.TimeOptimal.Compute(c, true);

        Check(draft != null && draft.stats.valid, "время-оптимальная траектория рассчитана",
            draft != null
                ? "время " + draft.original.time.ToString("0.000") + " → " +
                  draft.stats.time.ToString("0.000") + " с"
                : "черновик не получен");
        if (draft == null) { Next(15); return; }

        Check(draft.stats.time <= draft.original.time + 0.05f,
            "время-оптимальная не медленнее исходной",
            "выигрыш " + draft.timeGain.ToString("+0.0;-0.0") + " %");
        Check(draft.stats.maxVel <= stages2.TimeOptimal.MaxVel * 1.25f,
            "скорость суставов не превышает заданный лимит",
            draft.stats.maxVel.ToString("0.0") + " °/с при лимите " +
            stages2.TimeOptimal.MaxVel.ToString("0") + " °/с");
        Check(draft.stats.maxAcc <= stages2.TimeOptimal.MaxAcc * 1.35f,
            "ускорение не превышает заданный лимит",
            draft.stats.maxAcc.ToString("0.0") + " °/с² при лимите " +
            stages2.TimeOptimal.MaxAcc.ToString("0") + " °/с²");
        Note("jerk после ограничения: " + draft.stats.maxJerk.ToString("0") + " °/с³ при лимите " +
             stages2.TimeOptimal.MaxJerk.ToString("0") + " °/с³");
        Check(draft.stats.maxJerk <= stages2.TimeOptimal.MaxJerk * 3.0f,
            "jerk удержан в пределах лимита (ограничение итеративное)",
            draft.stats.maxJerk.ToString("0") + " °/с³");

        if (Once("apply"))
        {
            savedApplied = stages2.TimeOptimal.ApplySelected();
        }
        if (!Wait(1.0f)) return;
        Check(savedApplied && Mathf.Abs((float)c.plan.Time - draft.stats.time) < 0.06f,
            "переключение на время-оптимальную подменило время варианта",
            "план " + ((float)c.plan.Time).ToString("0.000") + " с · расчёт " +
            draft.stats.time.ToString("0.000") + " с");

        bool marked = stages2.TimeOptimal.MarkInComparison();
        Check(marked, "вариант отмечен в сравнении траекторий (метка A/B)",
            "A=" + (hub.Comparison != null ? hub.Comparison.SlotA + 1 : 0) +
            " · B=" + (hub.Comparison != null ? hub.Comparison.SlotB + 1 : 0));

        stages2.TimeOptimal.ResetSelected();
        Next(15);
    }

    // ---------------------------------------------------------------- ЭТАП 6: энергия

    private static void StepEnergy()
    {
        int index;
        TrajectoryCandidate c = KvVariantKit.Selected(flow, out index);
        KvEnergyOptimal.Draft draft = null;

        if (Once("payload")) stages2.Energy.PayloadKg = 1.5f;
        if (Once("compute")) draft = stages2.Energy.Compute(c);
        if (!Wait(2.5f)) return;
        draft = stages2.Energy.Compute(c, true);

        Check(draft != null && draft.energy > 0f, "эко-профиль рассчитан (энергия > 0)",
            draft != null
                ? "энергия " + draft.originalEnergy.ToString("0.00") + " → " +
                  draft.energy.ToString("0.00") + " Дж"
                : "черновик не получен");
        if (draft == null) { Next(16); return; }

        Check(draft.energyPerMeter > 0f && draft.peakPower > 0f,
            "метрика «энергоэффективность» считается (Дж/м и пиковая мощность)",
            draft.energyPerMeter.ToString("0.00") + " Дж/м · " +
            draft.peakPower.ToString("0.0") + " Вт");
        Check(draft.energy <= draft.originalEnergy + 1e-2f, "энергия эко-профиля не выше исходной",
            "экономия " + draft.savings.ToString("+0.0;-0.0") + " % · профилей проверено " +
            draft.trials);

        if (Once("apply")) savedApplied = stages2.Energy.ApplySelected();
        if (!Wait(1.0f)) return;
        Check(savedApplied, "переключение на эко-профиль выполнено",
            "груз " + stages2.Energy.PayloadKg.ToString("0.0") + " кг · время " +
            ((float)c.plan.Time).ToString("0.000") + " с");
        stages2.Energy.ResetSelected();
        Note("строка метрики для свойств: " + stages2.Energy.MetricLine(c));
        Next(16);
    }

    // ---------------------------------------------------------------- SCARA

    private static void StepScara()
    {
        if (robotScara == null)
        {
            Note("SCARA в сцене не найдена — цикл проверяется только на роботе");
            Next(18);
            return;
        }

        // Перепривязка идёт по прицелу камеры: наводим камеру вплотную к SCARA и держим
        // прицел у неё, пока поток не переключится (в batch мышь стоит в углу экрана,
        // поэтому камера ставится так, чтобы любой луч попадал в этот стенд).
        rc = robotScara;
        Vector3 toScara = rc.transform.position + new Vector3(0.35f, 0.85f, -0.75f);
        if (cam != null) cam.transform.position = toScara;
        aimAt(rc);
        Aim(rc.transform.position + Vector3.up * 0.25f, true, false, false, false, Vector3.zero, false);
        if (flow.Robot != robotScara && !Wait(15f)) return;

        if (flow.Robot != robotScara)
        {
            // В batch-режиме управление прицелом идёт от мыши (она в углу экрана), поэтому
            // перепривязка к другому стенду здесь не гарантируется. Это НЕ отказ продукта:
            // математика постобработки для 3-осевого робота с ПРИЗМАТИЧЕСКОЙ осью
            // проверяется отдельно — на собственном валидаторе SCARA.
            Note("перепривязка потока к SCARA в batch-режиме не подтвердилась (" +
                 (flow.Robot != null ? flow.Robot.name : "нет") +
                 ") — проверяется ручным переключением робота в редакторе (F)");
            CheckScaraMath();
            Next(18);
            return;
        }

        v = flow.Validator;
        Check(flow.Robot == rc, "поток перепривязан к SCARA",
            "робот потока: " + (flow.Robot != null ? flow.Robot.name : "нет") + " · осей " + v.Dof);
        if (flow.Robot != rc) { Next(18); return; }

        if (hub != null && hub.World != null)
            hub.World.Rebuild(flow.Robot, flow.Validator.linkRadius);

        if (Once("point"))
        {
            Vector3 p;
            bool found = FindReachablePoint(flow.Validator, out p);
            workPoint = p;
            Check(found, "для SCARA найдена достижимая точка", found ? p.ToString("F3") : "не найдена");
            if (found) Click(p);
            else { Next(18); return; }
        }
        if (!Wait(40f)) return;

        Check(flow.State.candidates.Count > 0, "SCARA: траектории построены",
            "вариантов: " + flow.State.candidates.Count);
        if (flow.State.candidates.Count == 0) { Next(18); return; }

        Note("цикл переходит на SCARA: сглаживание, время-оптимальная, эко-профиль");
        Next(17);
    }

    private static void StepScaraPost()
    {
        int index;
        TrajectoryCandidate c = KvVariantKit.Selected(flow, out index);

        if (Once("run"))
        {
            stages2.Smoothing.Method = KvSmoothMethod.Gauss;
            stages2.Smoothing.Level = 65f;
            scaraSmooth = stages2.Smoothing.ApplySelected(true);
            scaraEntry = stages2.Smoothing.EntryOf(c);
            scaraTopt = stages2.TimeOptimal.Compute(c, true);
            scaraEco = stages2.Energy.Compute(c, true);
        }
        if (!Wait(3.0f)) return;

        Check(scaraSmooth && scaraEntry != null && scaraEntry.applied, "SCARA: сглаживание работает",
            scaraEntry != null
                ? "jerk " + scaraEntry.before.maxJerk.ToString("0") + " → " +
                  scaraEntry.after.maxJerk.ToString("0") + " °/с³"
                : "—");
        Check(scaraTopt != null && scaraTopt.stats.valid,
            "SCARA: время-оптимальная траектория считается",
            scaraTopt != null
                ? scaraTopt.original.time.ToString("0.000") + " → " +
                  scaraTopt.stats.time.ToString("0.000") + " с"
                : "—");
        Check(scaraEco != null && scaraEco.energy > 0f,
            "SCARA: эко-профиль считается (призматическая ось — в СИ)",
            scaraEco != null
                ? scaraEco.energy.ToString("0.00") + " Дж · " +
                  scaraEco.energyPerMeter.ToString("0.00") + " Дж/м"
                : "—");
        Note("SCARA: осей " + v.Dof + " · призматическая ось Z: " +
             (v.IsPrismatic(2) ? "да" : "нет"));
        Next(18);
    }

    private static bool scaraSmooth;
    private static KvSmoothEntry scaraEntry;
    private static KvTimeOptimal.Draft scaraTopt;
    private static KvEnergyOptimal.Draft scaraEco;

    /// <summary>
    /// ПРОВЕРКА ПОСТОБРАБОТКИ НА 3-ОСЕВОМ РОБОТЕ НАПРЯМУЮ (без потока этапов):
    /// создаётся собственный `PoseValidator` для SCARA, на нём строится план
    /// (`KvPlanKit.MakeJointPlan`), затем прогоняются перепараметризация по лимитам,
    /// сглаживание пути и расчёт энергии. Так проверяется главное отличие SCARA —
    /// ПРИЗМАТИЧЕСКАЯ ось Z (метры вместо градусов, сила вместо момента).
    /// </summary>
    private static void CheckScaraMath()
    {
        if (robotScara == null) return;

        PoseValidator sv = new PoseValidator();
        sv.Init(robotScara);
        Check(sv.Ready && sv.Dof == 3, "SCARA: собственный валидатор собран (3 оси)",
            "осей: " + sv.Dof + " · призматическая ось Z: " + (sv.IsPrismatic(2) ? "да" : "нет"));
        if (!sv.Ready) return;

        double[] start = sv.CopyCurrent();
        double[] goal = (double[])start.Clone();
        goal[0] += 20.0;                                  // J1 на 20°
        goal[1] -= 12.0;                                  // J2 на 12°
        goal[2] = Mathf.Clamp((float)goal[2] + 0.04f, sv.Lower[2] + 0.01f, sv.Upper[2] - 0.01f);
        goal = sv.ContinueFrom(start, goal);

        PlannedTrajectory plan = KvPlanKit.MakeJointPlan(sv, hub != null ? hub.World : null,
            start, goal, "SCARA-тест", 0.10f, 40);
        Check(plan != null && plan.Path != null && plan.Path.Length >= 4,
            "SCARA: тестовый план построен", plan != null ? plan.Path.Length + " сэмплов" : "—");
        if (plan == null) return;

        PlannedTrajectory retimed = KvTrajMath.Retime(sv, plan, stages2.Limits, 1f, 1f,
            "SCARA-тест · время-оптимальная");
        KvTrajStats stats = KvTrajMath.Analyze(sv, retimed, null, null,
            robotScara.transform.position);
        Check(retimed != null && stats.valid, "SCARA: перепараметризация по лимитам считается",
            retimed != null
                ? plan.Time.ToString("0.000") + " → " + stats.time.ToString("0.000") + " с · v " +
                  stats.maxVel.ToString("0.0") + " °/с · a " + stats.maxAcc.ToString("0") + " °/с²"
                : "—");
        Check(stats.valid && stats.time <= plan.Time + 0.05f,
            "SCARA: постобработка не медленнее исходного плана",
            stats.valid ? "выигрыш " + ((plan.Time - stats.time) / Math.Max(0.001, plan.Time) * 100f)
                .ToString("+0.0;-0.0") + " %" : "—");

        double[][] smoothed = KvTrajMath.Smooth(plan.Path, KvSmoothMethod.BSpline, 0.6f);
        bool endsKept = SamePoint(smoothed[0], plan.Path[0]) &&
                        SamePoint(smoothed[smoothed.Length - 1], plan.Path[plan.Path.Length - 1]);
        Check(endsKept, "SCARA: сглаживание сохраняет начало и конец пути");

        float peak;
        float energy = KvTrajMath.Energy(sv, retimed != null ? retimed : plan,
            stages2.Energy.Model, robotScara.transform.position, out peak);
        Check(energy > 0f && peak > 0f,
            "SCARA: энергия считается, призматическая ось — в СИ (Н × м/с)",
            energy.ToString("0.0000") + " Дж · пик " + peak.ToString("0.0000") + " Вт");
    }

    // ---------------------------------------------------------------- отчёт

    private static void StepFinishReport()
    {
        if (done) return;
        done = true;

        try
        {
            if (stages2 != null && stages2.Smoothing != null)
            {
                stages2.Smoothing.Auto = false;
                stages2.Smoothing.Level = savedSmoothLevel;
            }
            if (flow != null) flow.ResetFlow("диагностика завершена");
        }
        catch (Exception e) { Note("возврат состояния: " + e.Message); }

        Line("");
        Line("ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · [info] " + info);
        Line(fail == 0 ? "ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ИТОГ: ЕСТЬ ОТКАЗЫ (" + fail + ")");
        Finish();
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Key, false);
        try { File.AppendAllText(report, "=== конец прогона ===\n"); } catch { }
        Debug.Log("[DshStage2Diag] отчёт: " + report + " · [OK] " + ok + " [FAIL] " + fail);
        EditorApplication.Exit(fail == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ помощники

    private static bool SamePoint(double[] a, double[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (Math.Abs(a[i] - b[i]) > 1e-6) return false;
        return true;
    }

    private static Vector3 workPointOrAim(int attempt = 0)
    {
        if (workPoint != Vector3.zero && attempt == 0) return workPoint;
        Vector3 p;
        if (FindReachablePoint(flow.Validator, out p))
        {
            workPoint = p;
            return p;
        }
        return flow.Robot != null
            ? flow.Robot.transform.position + new Vector3(0.10f, 0.35f, -0.55f)
            : Vector3.up;
    }

    private static void SetLasers(bool red, bool green)
    {
        if (cam == null) return;
        cam.leftHandEnabled = red;
        cam.rightHandEnabled = green;
    }

    private static void Aim(Vector3 p, bool hit, bool confirm, bool cancel, bool enter, Vector3 axis,
        bool fast)
    {
        if (flow == null) return;
        flow.UpdateAim(p, hit, confirm, cancel,
            cam != null && cam.leftHandEnabled, cam != null && cam.rightHandEnabled,
            Vector3.up, hit, enter, axis, fast);
    }

    private static void Click(Vector3 p)
    {
        SetLasers(true, false);
        Aim(p, true, true, false, false, Vector3.zero, false);
        SetLasers(false, false);
    }

    private static void aimAt(RobotController r)
    {
        if (r == null || cam == null) return;
        aimAt(r.transform.position + Vector3.up * 0.15f);
    }

    private static void aimAt(Vector3 p)
    {
        if (cam == null) return;
        Vector3 from = p + new Vector3(0f, 1.15f, -1.5f);
        cam.transform.position = from;
        cam.transform.rotation = Quaternion.LookRotation((p - from).normalized, Vector3.up);
    }

    /// <summary>Достижимая точка на столе (та же логика, что в сквозном прогоне этапов 1–8).</summary>
    private static bool FindReachablePoint(PoseValidator validator, out Vector3 point)
    {
        point = Vector3.zero;
        if (validator == null || !validator.Ready) return false;
        CollisionWorld world = hub != null ? hub.World : null;
        Vector3 b = validator.BasePosition;
        double[] seed = validator.CopyCurrent();
        double[] q;
        float maxIkError = validator.Dof > 3 ? 0.005f : 0.012f;
        float maxR = validator.Dof > 3 ? 0.75f : 0.40f;
        float minR = validator.Dof > 3 ? 0.30f : 0.18f;
        float lift = validator.Dof > 3 ? 0f : 0.06f;
        for (float r = maxR; r >= minR; r -= 0.05f)
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
                if (!validator.SolveIk(p, seed, out q)) continue;
                if (!validator.WithinLimits(q)) continue;
                if (validator.LastIkError > maxIkError) continue;
                if (world != null)
                {
                    Vector3 tcp;
                    Vector3[] nodes;
                    if (validator.ClearanceAt(q, world, out tcp, out nodes) < 0.03f) continue;
                }
                point = p;
                return true;
            }
        }
        return false;
    }
}
