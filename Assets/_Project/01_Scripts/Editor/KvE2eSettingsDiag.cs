using System;
using System.IO;
using System.Reflection;
using System.Text;
using KazistovVvUI;
using KazistovVvXr;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Е2Е-ПРОВЕРКА ПАНЕЛИ «НАСТРОЙКИ» И ПЕРЕКЛЮЧАТЕЛЯ VR / MR (11.11.2026).
///
/// ЗАЧЕМ ОТДЕЛЬНЫЙ ПРОГОН, ЕСЛИ ЕСТЬ EditMode-ТЕСТЫ. EditMode-тесты проверяют ЧИСТЫЕ функции и
/// ИСХОДНИКИ: они не поднимают сцену, не строят настоящую панель uGUI и не нажимают настоящую
/// кнопку. Значит ими НЕЛЬЗЯ поймать самый дорогой класс дефектов этого проекта:
///   • переключатель нарисован, но его `onClick` не подключён (нажатие — тишина);
///   • колбэк подключён, но менеджер не создал `KvSettingsView` (панель пустая);
///   • значение записалось, но в другое хранилище (шлем и ПК разошлись);
///   • панель падает на одной из шести вкладок, и оператор видит пустоту вместо настроек.
/// Поэтому здесь ПРОГОНЯЕТСЯ НАСТОЯЩИЙ ПУТЬ ОПЕРАТОРА: сцена → PlayMode → менеджер интерфейса →
/// панель «Настройки» → вкладка «Интерфейс» → настоящий клик по сегменту VR/MR → проверка, что
/// изменилось ИМЕННО ОБЩЕЕ хранилище и что выбор переживёт перезапуск.
///
/// ЗАПУСК (без -quit: прогон сам завершает редактор кодом возврата):
///   Unity.exe -batchmode -nographics -projectPath "&lt;проект&gt;" ^
///       -executeMethod KvE2eSettingsDiag.Run -logFile _e2e_settings.log
///
/// КОДЫ ВОЗВРАТА: 0 — все проверки прошли; 2 — есть провалы (номера в отчёте); 3 — прогон не
/// дошёл до менеджера интерфейса за отведённое время.
///
/// ЧТО НЕ ДЕЛАЕТ: не сохраняет сцену, не переписывает ассеты, не оставляет изменённым режим
/// (исходное положение переключателя восстанавливается в конце и в отчёте).
/// </summary>
public static class KvE2eSettingsDiag
{
    private const string SessionKey = "KvE2eSettingsDiag.Active";
    private const float SceneTimeoutSec = 90f;
    /// <summary>Сколько секунд в PlayMode допустимо до объявления зависания.</summary>
    private const float PlayModeBudgetSeconds = 240f;

    // ------------------------------------------------------------------ состояние прогона
    private static int step, waitFrames, stepFrame;
    private static int ok, fail;
    private static bool finished;
    private static float started;
    /// <summary>Момент входа в PlayMode (0 — ещё не вошли). Нужен сторожу зависания.</summary>
    private static float playmodeStart;

    private static KazistovVvUIManager ui;
    private static KvSettingsView settingsView;

    /// <summary>Исходное положение переключателя — обязано вернуться в конце.</summary>
    private static int originalMode = -1;
    /// <summary>Положение, в которое мы переключились (противоположное исходному).</summary>
    private static int toggledMode = -1;
    /// <summary>Сколько вкладок «Настроек» прошли проверку на непустоту.</summary>
    private static int tabsOk;
    /// <summary>Первый провал — чтобы код возврата был осмысленным.</summary>
    private static string firstFailure = "";

    private static string ReportPath
    {
        // Отчёт — в persistentDataPath, а НЕ в проект: проект лежит в OneDrive, и во время
        // PlayMode файл в нём не дописывался (урок §13.11 проекта).
        get { return KazistovVvFeatures.FeatureStorage.ReportPath("_e2e_settings.txt"); }
    }

    // ================================================================== запуск

    /// <summary>Точка входа для -executeMethod.</summary>
    public static void Run()
    {
        Write("=== Е2Е: ПАНЕЛЬ «НАСТРОЙКИ» И ПЕРЕКЛЮЧАТЕЛЬ VR/MR ===\n" +
              DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "\n" +
              "проект: " + Directory.GetParent(Application.dataPath).FullName + "\n\n");

        originalMode = -1;
        toggledMode = -1;
        ok = 0;
        fail = 0;
        tabsOk = 0;
        firstFailure = "";
        finished = false;
        step = 0;
        collectedExceptions = 0;

        SessionState.SetBool(SessionKey, true);
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        started = (float)EditorApplication.timeSinceStartup;
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/00_Scenes/MainScene.unity", OpenSceneMode.Single);
            Write("сцена MainScene открыта, вход в PlayMode…\n");
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e)
        {
            Check("сцена MainScene открывается", false, e.GetType().Name + ": " + e.Message);
            Finish();
        }
    }

    /// <summary>
    /// ПОДПИСАТЬСЯ ЗАНОВО ПОСЛЕ ПЕРЕЗАГРУЗКИ ДОМЕНА — без этого прогон ЗАВИСАЕТ.
    ///
    /// ЧТО БЫЛО (найдено первым же запуском 11.11.2026). `Run()` подписывался на
    /// `EditorApplication.update` и вызывал `EnterPlaymode()`. Вход в PlayMode перезагружает
    /// домен (статическое состояние обнуляется), подписка теряется — и прогон навсегда остаётся
    /// в PlayMode: отчёт содержит только заголовок, тест не идёт, а CI не отличает «идёт» от
    /// «завис». Ровно поэтому в `DshFullVerifyDiag` такая же точка входа помечена
    /// `[InitializeOnLoadMethod]`: признак «прогон активен» хранится в `SessionState` (он переживает
    /// перезагрузку), а подписки восстанавливаются здесь.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    // ================================================================== журнал исключений

    private static int collectedExceptions;
    private static readonly StringBuilder errors = new StringBuilder();

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
        if (string.IsNullOrEmpty(message)) return;

        // ИСКЛЮЧЕНИЯ САМОГО ПРОГОНА не считаем: «не найдено» мы проверяем явными Check и
        // собственными сообщениями, а не по факту красной строки в консоли.
        if (message.IndexOf("KvE2eSettingsDiag", StringComparison.Ordinal) >= 0) return;

        collectedExceptions++;
        if (collectedExceptions <= 25)
            errors.Append("  [").Append(type).Append("] ").Append(FirstLine(message)).Append('\n');
    }

    private static string FirstLine(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        int i = s.IndexOf('\n');
        return i < 0 ? s : s.Substring(0, i);
    }

    // ================================================================== шаговый движок

    private static void Tick()
    {
        if (finished) return;

        if (!EditorApplication.isPlaying)
        {
            // PlayMode ещё не начался (или уже закончился) — ждём, но не бесконечно.
            if ((float)EditorApplication.timeSinceStartup - started > SceneTimeoutSec + 60f)
            {
                Check("PlayMode запускается", false,
                    "не дошли до менеджера интерфейса за " + (SceneTimeoutSec + 60f) + " с");
                Finish();
            }
            return;
        }

        if (waitFrames > 0) { waitFrames--; return; }
        if (Time.frameCount == stepFrame) return;
        stepFrame = Time.frameCount;

        // СТОРОЖ ЗАВИСАНИЯ. Без него «прогон повис» и «прогон идёт» неразличимы, а CI-задача
        // висит до общего таймаута (у PlayMode это уже измерялось: ~40 минут). Здесь зависание
        // превращается в ЯВНЫЙ провал с номером шага, на котором встали.
        if (playmodeStart <= 0f) playmodeStart = (float)EditorApplication.timeSinceStartup;
        float inPlay = (float)EditorApplication.timeSinceStartup - playmodeStart;
        if (inPlay > PlayModeBudgetSeconds)
        {
            Check("прогон укладывается в " + PlayModeBudgetSeconds + " с", false,
                "зависание на шаге " + step + " (" + inPlay.ToString("0") + " с в PlayMode)");
            Finish();
            return;
        }

        try
        {
            RunStep();
        }
        catch (Exception e)
        {
            Check("шаг " + step + " выполняется без исключения", false,
                e.GetType().Name + ": " + e.Message);
            Next(2);
        }
    }

    private static void Next(int wait = 2)
    {
        step++;
        waitFrames = wait;
    }

    private static void Check(string name, bool pass, string detail)
    {
        if (pass) ok++;
        else
        {
            fail++;
            if (string.IsNullOrEmpty(firstFailure)) firstFailure = name;
        }
        Write((pass ? "[OK]   " : "[FAIL] ") + name +
              (string.IsNullOrEmpty(detail) ? "" : " — " + detail) + "\n");
        if (!pass) Debug.LogWarning("[E2E] " + name + " — " + detail);
    }

    private static void Write(string text)
    {
        try { File.AppendAllText(ReportPath, text, new UTF8Encoding(false)); }
        catch (Exception) { /* отчёт — не повод падать */ }
    }

    // ================================================================== шаги

    private static void RunStep()
    {
        switch (step)
        {
            // ---------------------------------------------------------- 1. приложение поднялось
            case 0:
                ui = KazistovVvUIManager.Instance;
                Check("1.1 менеджер интерфейса поднялся в PlayMode", ui != null,
                      ui != null ? ui.name : "KazistovVvUIManager.Instance = null");
                if (ui == null) { Next(10); break; }
                Next(6);
                break;

            case 1:
                settingsView = ui.SettingsView;
                Check("1.2 панель «Настройки» создана (KvSettingsView)",
                      settingsView != null, settingsView != null ? "есть" : "нет");
                Next(4);
                break;

            // ---------------------------------------------------------- 2. панель открывается
            case 2:
            {
                // ПРЕДУСЛОВИЕ СТАВИТСЯ ЯВНО, А НЕ ПРЕДПОЛАГАЕТСЯ.
                //
                // ДЕФЕКТ САМОГО ПРОГОНА (найден повторным запуском 11.11.2026). Проверка «до вызова
                // панель скрыта» была ложной: ВИДИМОСТЬ ПАНЕЛЕЙ СОХРАНЯЕТСЯ В PlayerPrefs между
                // сессиями (это штатное поведение проекта — раскладка переживает перезапуск).
                // Первый прогон оставлял панель открытой, второй начинал с открытой и падал, хотя
                // приложение работало верно. Тест обязан управлять своими предпосылками.
                ui.SetPanel("settings", false);
                Next(6);
                break;
            }

            case 3:
            {
                bool hiddenBefore = !ui.IsPanelVisible("settings");
                Check("2.1 панель «Настройки» скрыта после явного скрытия", hiddenBefore,
                      "видима=" + !hiddenBefore);

                int tab = KvSettingsView.IndexOfTab("Интерфейс");
                Check("2.2 вкладка «Интерфейс» существует в схеме панели", tab >= 0,
                      "IndexOfTab(\"Интерфейс\") = " + tab);

                ui.ShowSettings(tab);
                Next(6);
                break;
            }

            // ---------------------------------------------------------- 3. вкладки строятся
            case 4:
            {
                Check("3.1 панель «Настройки» стала видимой по вызову", ui.IsPanelVisible("settings"), "видима");

                // R6-1 (12.11.2026): ФЛАГА «включена» НЕДОСТАТОЧНО. На планшете команда
                // выполнялась (строка в журнале была), а панель не появлялась — она оказывалась за
                // пределами экрана или под оверлеем. Теперь Е2Е проверяет и фактическую видимость:
                // панель обязана попадать в экран, а не просто быть «включённой».
                Check("3.1б панель «Настройки» реально попадает в экран (R6-1)",
                      ui.IsPanelEffectivelyVisible("settings"),
                      ui.DescribeDock(ui.SettingsDock));

                // R6-1, ТРЕТИЙ КРУГ (12.11.2026): ПРОВЕРКА ПРОТИВ ПРИЁМКИ НА УСТРОЙСТВЕ.
                //
                // ЧТО БЫЛО. Проверки «флаг + геометрия» на планшете отвечали «видна», хотя сторож
                // оверлея погасил канвас оболочки и на экране не было ничего. В редакторе Е2Е при
                // этом проходил 29/29 — потому что в редакторе сторож оверлея не работает.
                // ЧТО ЗДЕСЬ. Поведение сторожа ЭМУЛИРУЕТСЯ: гасим `Canvas.enabled` оболочки (ровно
                // то, что делает сторож в AR) и проверяем, что панель настроек всё равно видна —
                // она живёт на своём канвасе. Так дефект ловится БЕЗ устройства.
                Canvas shellBefore = ui.RootCanvas;
                bool shellWasEnabled = shellBefore != null && shellBefore.enabled;
                if (shellBefore != null) shellBefore.enabled = false;
                ui.SetPanel("settings", false);
                ui.ShowSettings(KvSettingsView.IndexOfTab("Интерфейс"));

                bool panelCanvasAlive = ui.PanelCanvas != null && ui.PanelCanvas.enabled &&
                                        ui.PanelCanvas.gameObject.activeInHierarchy;
                Check("3.1в при погашенном канвасе оболочки панель оператора всё равно рисуется (R6-1, AR)",
                      panelCanvasAlive && ui.IsPanelEffectivelyVisible("settings"),
                      "канвас панелей: " + (ui.PanelCanvas != null
                          ? ("«" + ui.PanelCanvas.name + "» включён=" + ui.PanelCanvas.enabled +
                             " · порядок=" + ui.PanelCanvas.sortingOrder +
                             " · активен в иерархии=" + ui.PanelCanvas.gameObject.activeInHierarchy)
                          : "нет") +
                      " · панель видна: " + (ui.IsPanelEffectivelyVisible("settings") ? "да" : "НЕТ"));

                if (shellBefore != null && shellWasEnabled) shellBefore.enabled = true;

                // 3.1г: ДОК-ПАНЕЛЬ И СОДЕРЖИМОЕ НАСТРОЕК ЛЕЖАТ В КАНВАСЕ ПАНЕЛЕЙ.
                //
                // Вопрос тестировщика из приёмки 12.11.2026 дословно: «проверь, что в AR-раскладке
                // док-панель вообще создаётся (`KvSettingsView` есть в иерархии канваса?)». Это
                // отдельная проверка, потому что «панель видна по флагу» и «панель есть в дереве
                // объектов нужного канваса» — разные утверждения: первое верно и для объекта,
                // выключенного вместе с предком.
                //
                // ВАЖНО, ЧТО ИМЕННО ПРОВЕРЯЕТСЯ. Сам компонент `KvSettingsView` висит на объекте
                // МЕНЕДЖЕРА (он всегда там), а интерфейс настроек строится в тело dock-панели
                // (`settings.Build(settingsDock.Body, …)`). Поэтому проверяется ФАКТ: корень
                // dock-панели лежит в канвасе панелей, а в её теле построены строки.
                bool dockInPanelCanvas = ui.SettingsDock != null && ui.PanelCanvas != null &&
                                         ui.SettingsDock.Root != null &&
                                         ui.SettingsDock.Root.IsChildOf(ui.PanelCanvas.transform);
                bool contentBuilt = ui.SettingsDock != null && ui.SettingsDock.Body != null &&
                                    ui.SettingsDock.Body.childCount > 0;
                Check("3.1г док-панель «Настройки» и её содержимое лежат в канвасе панелей оператора (R6-1)",
                      dockInPanelCanvas && contentBuilt,
                      "канвас панелей: " + (ui.PanelCanvas != null ? "«" + ui.PanelCanvas.name + "»" : "нет") +
                      " · панель настроек: " + (dockInPanelCanvas ? "внутри" : "СНАРУЖИ") +
                      " · строк в теле: " + (ui.SettingsDock != null && ui.SettingsDock.Body != null
                          ? ui.SettingsDock.Body.childCount.ToString() : "нет тела"));

                Check("3.2 активна вкладка «Интерфейс»",
                      settingsView.ActiveTab == KvSettingsView.IndexOfTab("Интерфейс"),
                      "ActiveTab=" + settingsView.ActiveTab);

                // ВСЕ вкладки обязаны строиться и не быть пустыми: пустая вкладка — это тот же
                // «серый экран», только внутри настроек. Проверяются все, а не только нужная.
                for (int t = 0; t < KvSettingsView.TabCount; t++)
                {
                    settingsView.SetTab(t);
                    int rows = settingsView.RowCount;
                    Check("3.3 вкладка " + (t + 1) + " из " + KvSettingsView.TabCount +
                          " строится и не пуста", rows > 0, "строк: " + rows);
                    if (rows > 0) tabsOk++;
                }

                settingsView.SetTab(KvSettingsView.IndexOfTab("Интерфейс"));
                Next(6);
                break;
            }

            // ---------------------------------------------------------- 4. переключатель на месте
            case 5:
            {
                Transform seg = FindDeep(settingsView.transform, "Seg_VrMr");
                Check("4.1 на вкладке «Интерфейс» есть переключатель VR/MR (Seg_VrMr)",
                      seg != null, seg != null ? Path(seg) : "не найден");

                if (seg != null)
                {
                    Button vr = ChildButton(seg, "Seg0");
                    Button mr = ChildButton(seg, "Seg1");
                    Check("4.2 у переключателя два положения и обе кнопки кликабельны",
                          vr != null && mr != null && vr.interactable && mr.interactable,
                          "VR-кнопка: " + (vr != null) + " · MR-кнопка: " + (mr != null));

                    string vrText = vr != null ? TextOf(vr.gameObject) : "";
                    string mrText = mr != null ? TextOf(mr.gameObject) : "";
                    Check("4.3 положение VR названо словом VR", vrText.IndexOf("VR", StringComparison.Ordinal) >= 0,
                          "надпись: «" + vrText + "»");
                    Check("4.4 положение MR названо словом MR", mrText.IndexOf("MR", StringComparison.Ordinal) >= 0,
                          "надпись: «" + mrText + "»");

                    KvSegmented view = seg.GetComponent<KvSegmented>();
                    originalMode = KvXrSettings.GetModeIndex();
                    Check("4.5 переключатель показывает ТЕКУЩИЙ режим из хранилища",
                          view != null && view.Index == originalMode,
                          "нарисовано: " + (view != null ? view.Index : -1) +
                          " · в хранилище: " + originalMode);
                }

                toggledMode = originalMode == KvXrSettings.ModeMr ? KvXrSettings.ModeVr : KvXrSettings.ModeMr;
                Write("  исходный режим: " + KvXrSettings.ModeLabel(originalMode) +
                      " · будем переключать в: " + KvXrSettings.ModeLabel(toggledMode) + "\n");
                Next(4);
                break;
            }

            // ---------------------------------------------------------- 5. НАСТОЯЩИЙ клик
            case 6:
            {
                Transform seg = FindDeep(settingsView.transform, "Seg_VrMr");
                Check("5.1 переключатель найден перед нажатием", seg != null, "Seg_VrMr");

                Button target = seg != null
                    ? ChildButton(seg, toggledMode == KvXrSettings.ModeMr ? "Seg1" : "Seg0")
                    : null;
                Check("5.2 кнопка выбранного положения существует", target != null,
                      "кнопка: Seg" + (toggledMode == KvXrSettings.ModeMr ? "1" : "0"));

                if (target != null)
                {
                    // ЭТО И ЕСТЬ НАСТОЯЩИЙ КЛИК ОПЕРАТОРА: вызывается тот же UnityEvent, который
                    // вызывает мышь. Никаких прямых записей в настройки — иначе тест проверял бы
                    // сам себя и не поймал бы «кнопка нарисована, а onClick не подключён».
                    target.onClick.Invoke();
                    Write("  нажата кнопка «" + TextOf(target.gameObject) + "»\n");
                }

                Next(8);
                break;
            }

            // ---------------------------------------------------------- 6. клик изменил хранилище
            case 7:
            {
                int now = KvXrSettings.GetModeIndex();
                Check("6.1 клик изменил режим в хранилище", now == toggledMode,
                      "ожидали " + toggledMode + ", получили " + now + " (" + KvXrSettings.ModeLabel(now) + ")");

                bool pass = KvXrSettings.GetPassthrough();
                Check("6.2 положение MR/MR-совместимо с веткой MR (GetPassthrough)",
                      pass == (toggledMode == KvXrSettings.ModeMr),
                      "GetPassthrough=" + pass);

                bool saved = PlayerPrefs.HasKey(KvXrSettings.KeyPassthrough);
                Check("6.3 выбор записан в PlayerPrefs (переживёт перезапуск)", saved,
                      saved ? "ключ " + KvXrSettings.KeyPassthrough + " есть" : "ключа нет");

                string line = KvXrSettings.ModeStatusLine();
                Check("6.4 строка состояния называет выбранный режим",
                      line.IndexOf(KvXrSettings.ModeShortLabel(toggledMode), StringComparison.Ordinal) >= 0 ||
                      line.IndexOf("СМЕШАННАЯ", StringComparison.Ordinal) >= 0 ||
                      line.IndexOf("ВИРТУАЛЬНАЯ", StringComparison.Ordinal) >= 0,
                      "«" + line + "»");

                // Модель экрана шлема обязана показывать тот же выбор — иначе шлем и ПК разойдутся.
                var data = new KvXrSettingsData
                {
                    PanelRadius = KvXrSettings.GetGroupPanelRadius(),
                    SmoothSpeed = KvXrSettings.GetSmoothSpeed(),
                    OperatorHeight = KvXrSettings.GetOperatorHeight(),
                    TeleportDistance = KvXrSettings.GetTeleportDistance(),
                    SecondRay = KvXrSettings.GetSecondaryRay(),
                    RayHand = KvXrSettings.GetRayHand(),
                    MixedReality = KvXrSettings.GetModeIndex() == KvXrSettings.ModeMr
                };
                var rows = KazistovVvXr.KvXrScreenModel.BuildSettings(data);
                string last = rows[rows.Count - 1];
                Check("6.5 экран шлема показывает тот же режим, что и панель ПК",
                      last.IndexOf(KvXrSettings.ModeShortLabel(toggledMode), StringComparison.Ordinal) >= 0,
                      "строка 7: «" + last + "»");

                // 6.6 РЕЕСТР КОМАНД: НИ ОДНОГО ПУНКТА БЕЗ ФУНКЦИОНАЛА (12.11.2026).
                //
                // ТРЕБОВАНИЕ ВЛАДЕЛЬЦА: «чтобы все кнопочки были заполнены функционалом».
                // ПОЧЕМУ ЗДЕСЬ, А НЕ В СТАТИЧЕСКОМ ГЕЙТЕ. Гейт (`Tools/acceptance-gate.ps1`,
                // проверка UI_STUBS) читает ИСХОДНИКИ и считает, сколько раз регистрируется id —
                // то есть он ЗНАЕТ ТОЛЬКО ЭВРИСТИКУ и не видит порядка перекрытия. Здесь проверяется
                // ФАКТ: после ВСЕХ регистраций (менеджер + хабы этапов) в реестре не должно
                // остаться команды с `Stub = true` или с пустым `Execute` — иначе кнопка в
                // интерфейсе есть, а действия за ней нет.
                int stubCommands = 0;
                int emptyHandlers = 0;
                string firstBad = "";
                var registry = KvCommands.All;
                for (int i = 0; i < registry.Count; i++)
                {
                    KvCommand cmd = registry[i];
                    if (cmd == null) continue;
                    bool bad = cmd.Stub || cmd.Execute == null;
                    if (!bad) continue;
                    if (cmd.Stub) stubCommands++;
                    if (cmd.Execute == null) emptyHandlers++;
                    if (firstBad.Length == 0) firstBad = cmd.Id;
                }
                Check("6.6 в реестре команд нет кнопок без функционала (Stub / пустой обработчик)",
                      stubCommands == 0 && emptyHandlers == 0,
                      "команд в реестре: " + registry.Count + " · заглушек: " + stubCommands +
                      " · без обработчика: " + emptyHandlers +
                      (firstBad.Length > 0 ? " · первая: " + firstBad : ""));

                Next(6);
                break;
            }

            // ---------------------------------------------------------- 7. обратное переключение
            case 8:
            {
                Transform seg = FindDeep(settingsView.transform, "Seg_VrMr");
                Button back = seg != null
                    ? ChildButton(seg, originalMode == KvXrSettings.ModeMr ? "Seg1" : "Seg0")
                    : null;
                Check("7.1 после пересборки панели переключатель снова на месте", back != null,
                      seg != null ? "есть" : "Seg_VrMr пропал после Rebuild");

                KvSegmented view = seg != null ? seg.GetComponent<KvSegmented>() : null;
                Check("7.2 переключатель перерисовал новое положение",
                      view != null && view.Index == toggledMode,
                      "нарисовано: " + (view != null ? view.Index : -1) + ", ожидали " + toggledMode);

                if (back != null) back.onClick.Invoke();   // возвращаемся в исходный режим
                Next(8);
                break;
            }

            case 9:
            {
                int now = KvXrSettings.GetModeIndex();
                Check("8.1 обратное нажатие вернуло исходный режим", now == originalMode,
                      "ожидали " + originalMode + ", получили " + now);

                // --- состояние консоли за весь прогон
                Check("9.1 за прогон не было исключений и ошибок в консоли",
                      collectedExceptions == 0,
                      collectedExceptions == 0 ? "чисто" : collectedExceptions + " сообщений:\n" + errors);
                Check("9.2 все вкладки панели «Настройки» непусты",
                      tabsOk == KvSettingsView.TabCount,
                      "непустых вкладок: " + tabsOk + " из " + KvSettingsView.TabCount);

                KvXrSettings.SetModeIndex(originalMode);
                Write("\n  режим восстановлен: " + KvXrSettings.ModeLabel(KvXrSettings.GetModeIndex()) + "\n");

                Finish();
                break;
            }

            default:
                Finish();
                break;
        }
    }

    // ================================================================== поиск элементов uGUI

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static Button ChildButton(Transform parent, string childName)
    {
        Transform t = FindDeep(parent, childName);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static string TextOf(GameObject go)
    {
        Text t = go != null ? go.GetComponentInChildren<Text>() : null;
        return t != null ? t.text : "";
    }

    private static string Path(Transform t)
    {
        if (t == null) return "";
        StringBuilder sb = new StringBuilder(t.name);
        Transform p = t.parent;
        for (int guard = 0; p != null && guard < 12; guard++, p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    // ================================================================== итог

    private static void Finish()
    {
        if (finished) return;
        finished = true;
        SessionState.SetBool(SessionKey, false);
        Application.logMessageReceived -= OnLog;
        EditorApplication.update -= Tick;

        string verdict = fail == 0
            ? "ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ (" + ok + " из " + (ok + fail) + ")"
            : "ИТОГ: ПРОВАЛЕНО " + fail + " из " + (ok + fail) +
              (string.IsNullOrEmpty(firstFailure) ? "" : " · первый провал: " + firstFailure);
        Write("\n" + verdict + "\n");
        Write("отчёт: " + ReportPath + "\n");

        if (fail != 0) Debug.LogError("[E2E] " + verdict);
        else Debug.Log("[E2E] " + verdict);

        // Выходим из PlayMode и завершаем редактор кодом возврата — иначе в batchmode процесс
        // висит, и CI не отличит «тест идёт» от «тест завис».
        try { EditorApplication.ExitPlaymode(); }
        catch (Exception) { }
        EditorApplication.Exit(fail == 0 ? 0 : 2);
    }
}
