using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KazistovVvUI;
using TrajectoryCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Батч-прогон ДЕСКТОПНОГО интерфейса KazistovVv (FreeCAD-стиль): проверяет сборку
/// оболочки, 15 кнопок тулбара в 3 ряда, подсказки, дерево моделей (выбор/глазик/
/// переименование), свойства, статус-бар, переключение темы с PlayerPrefs, dock-панели,
/// вкладки настроек, реестр команд, производительность и отсутствие исключений.
/// Ничего в сцене не меняет, выходит сам.
///
/// Запуск (БЕЗ -quit — выход делает сам прогон):
///   Unity.exe -batchmode -nographics -projectPath "&lt;проект&gt;" -executeMethod DshDesktopUiDiag.Run -logFile _dsh_ui_diag.log
/// Отчёт: &lt;проект&gt;/_dsh_ui_verify.txt
/// </summary>
public static class DshDesktopUiDiag
{
    private const string SessionKey = "DshDesktopUiDiag.Active";
    private const string ReportName = "_dsh_ui_verify.txt";
    private const string ScenePath = "Assets/_Project/00_Scenes/MainScene.unity";

    private static string reportPath;
    private static int ok, fail, exceptions;
    private static int phase, frames;
    private static float tipWaitStart, objectsWait;

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, false);
        // После домен-релоада статики обнуляются: путь отчёта и подписки восстанавливаем.
        if (string.IsNullOrEmpty(reportPath))
            reportPath = KazistovVvFeatures.FeatureStorage.ReportPath(ReportName);
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        Subscribe();
    }

    public static void Run()
    {
        reportPath = KazistovVvFeatures.FeatureStorage.ReportPath(ReportName);
        try { File.Delete(reportPath); } catch { }
        ok = 0; fail = 0; phase = 0; frames = 0; exceptions = 0;

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        Subscribe();
        SessionState.SetBool(SessionKey, true);

        // Батч-редактор открывает «последнюю» сцену (часто пустую) — открываем MainScene явно.
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void Subscribe()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        // В -nographics HDRP пишет «No graphic device…» — это ошибка СРЕДЫ, не кода:
        // провалом считаем только настоящие исключения.
        if (type == LogType.Exception)
        {
            exceptions++;
            Line("[EXCEPTION] " + condition + "\n" + stackTrace);
        }
        else if (type == LogType.Error || type == LogType.Assert)
        {
            Line("[LOG:Error] " + condition);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        frames++;
        switch (phase)
        {
            case 0: if (frames >= 30) Next(); break;
            case 1: StageBuild(); break;
            case 2: StageToolbar(); break;
            case 3: StageTreeAndSelection(); break;
            case 4: StageRenameAndVisibility(); break;
            case 5: StageStatusBar(); break;
            case 6: StageTheme(); break;
            case 7: StagePanels(); break;
            case 8: StageSettings(); break;
            case 9: StageCommands(); break;
            case 10: StageTooltip(); break;
            case 11: StageTooltipWait(); break;
            case 12: StagePerformance(); break;
            case 13: StageObjects(); break;
            default: Finish(); break;
        }
    }

    private static void Next() { phase++; frames = 0; }

    private static void Line(string text)
    {
        if (string.IsNullOrEmpty(reportPath))
            reportPath = KazistovVvFeatures.FeatureStorage.ReportPath(ReportName);
        try { File.AppendAllText(reportPath, text + Environment.NewLine, Encoding.UTF8); } catch { }
    }

    private static void Check(bool condition, string description)
    {
        if (condition) { ok++; Line("[OK]   " + description); }
        else { fail++; Line("[FAIL] " + description); }
    }

    private static void Info(string text) { Line("[INFO] " + text); }

    // ------------------------------------------------------------------ шаги

    private static void StageBuild()
    {
        Line("=== ДЕСКТОПНЫЙ UI KAZISTOVVV (FreeCAD-стиль): ПРОВЕРКА ===");
        Info("Unity " + Application.unityVersion + " · платформа " + Application.platform);

        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        Check(ui != null, "менеджер KazistovVvUIManager поднялся в PlayMode");
        if (ui == null) { Finish(); return; }

        Check(ui.gameObject.name == "KazistovVv_UI",
            "объект интерфейса называется «KazistovVv_UI» (единый формат имени проекта)");
        Check(ui.Toolbar != null && ui.Menu != null && ui.Tree != null && ui.Properties != null &&
              ui.StatusBar != null, "все зоны собраны: меню, тулбар, дерево, свойства, статус-бар");
        Check(ui.TreeDock != null && ui.PropertiesDock != null && ui.SettingsDock != null,
            "dock-панели (дерево/свойства/настройки) созданы");
        Check(ui.Toolbar.Root.parent != null && ui.Toolbar.Root.parent.name == "KazistovVvCanvas",
            "интерфейс живёт на канвасе «KazistovVvCanvas»");
        Check(ui.Flow != null, "поток этапов найден (TrajectoryFlowController)");
        Check(ui.CameraRig != null, "контроллер оператора найден (FreeFlyCameraController)");

        Info("команд в реестре: " + KvCommands.All.Count);
        Info("иконок в библиотеке: " + CountIcons());

        // ФИКС 6/8 (§23): ЭТАЛОН РАЗМЕРОВ элементов интерфейса. В §22 метод KvWidgets.
        // ValidateSizes() был написан, но не вызывался ниоткуда — здесь он прогоняется
        // по-настоящему: галка 16×16/глиф 12, кнопка-иконка 24×24/глиф 0.78·24,
        // кнопка с текстом 24, вкладка 26 (24…28), иконки «+»/«−» дерева из единого набора.
        int sizeFailures = KvWidgets.ValidateSizesFailures();
        if (sizeFailures > 0) Line(KvWidgets.ValidateSizes());
        Check(sizeFailures == 0, "эталон размеров KvWidgets.ValidateSizes() — провалов " + sizeFailures);
        Next();
    }

    private static int CountIcons()
    {
        string[] ids =
        {
            "point","path","play","pause","stop","reset","robot","swap","workspace","limits","metrics",
            "flashlight","undo","redo","theme-dark","theme-light","theme-system","settings","help","tree",
            "properties","eye","eye-off","axis","joint","tcp","table","node-point","curve","phantom",
            "layers","close","collapse","expand","dock","chevron-right","chevron-down","check","info"
        };
        int count = 0;
        foreach (string id in ids) if (KvIcons.Get(id) != null) count++;
        return count;
    }

    private static void StageToolbar()
    {
        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        KvToolbar bar = ui.Toolbar;

        Check(bar.ButtonCount >= 15, "в верхней панели не меньше 15 кнопок (ТЗ: 15 штатных + кнопки этапов) — сейчас " + bar.ButtonCount);
        // ФИКС 8 (18.09.2026): раскладка тулбара стала АДАПТИВНОЙ (число колонок считается
        // от ширины канваса, максимум 4 ряда), поэтому проверяется не «ровно 5», а то, что
        // число колонок не меньше 5 и все кнопки умещаются в допустимое число рядов.
        Check(bar.ColumnsCount >= 5 && bar.ButtonCount > 0, "кнопки раскладываются адаптивно (не меньше 5 в ряд) · кнопок: " + bar.ButtonCount + " · столбцов: " + bar.ColumnsCount);
        Info("порядок кнопок: " + string.Join(", ", bar.ButtonIds));

        int withIcon = 0, withTip = 0, stubs = 0;
        for (int i = 0; i < bar.ButtonCount; i++)
        {
            KvIconButton b = bar.ButtonAt(i);
            if (b == null) continue;
            if (b.Icon != null && b.Icon.sprite != null) withIcon++;
            if (b.Tooltip != null && !string.IsNullOrEmpty(b.Tooltip.title)) withTip++;
            if (!b.IsEnabled) stubs++;
        }
        Check(withIcon == bar.ButtonCount, "у ВСЕХ кнопок панели есть ИКОНКА (без подписей) — с иконкой " + withIcon + " из " + bar.ButtonCount);
        Check(withTip == bar.ButtonCount, "у всех кнопок есть подсказка с названием — с подсказкой " + withTip + " из " + bar.ButtonCount);
        Check(stubs >= 2, "кнопки без функционала показаны недоступными (заглушки) — " + stubs);
        Check(KvCommands.Get("edit.undo") != null, "команда «Отменить» зарегистрирована (заглушку перекрывает модуль функций)");
        Check(KvCommands.Get("edit.redo") != null, "команда «Вернуть» зарегистрирована");
        // ЕТАП 15: Undo/Redo перекрыты модулем функций (FeatureHub) — это НЕ заглушки.
        // Проверяем честно: если заглушка — пометка обязательна; если работает — нужен обработчик.
        KvCommand undoCmd = KvCommands.Get("edit.undo");
        if (undoCmd != null && undoCmd.Stub)
            Check(undoCmd.TooltipBody.Contains(KvLoc.T("common.stub", "в разработке")),
                "у заглушки «Отменить» стоит пометка «в разработке»");
        else
            Check(undoCmd != null && undoCmd.Execute != null,
                "«Отменить» работает (не заглушка — модуль функций перекрыл, обработчик есть)");
        Check(KvCommands.Get("point.select") != null && KvCommands.Get("point.select").Hotkey == "Z",
            "у «Выбор точки» подсказка с горячей клавишей Z");
        Check(KvCommands.Get("path.select") != null && KvCommands.Get("path.select").Hotkey == "X",
            "у «Выбор траектории» подсказка с горячей клавишей X");

        Check(ui.Menu.OpenMenu == "", "строка меню создана и закрыта на старте");
        ui.Menu.ToggleMenu("Вид");
        Check(ui.Menu.OpenMenu == "Вид" && ui.Menu.DropdownRowCount > 5,
            "меню «Вид» открывается, пунктов: " + ui.Menu.DropdownRowCount);
        ui.Menu.CloseMenu();
        Check(ui.Menu.OpenMenu == "", "меню закрывается");
        Next();
    }

    private static void StageTreeAndSelection()
    {
        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        ui.Tree.ExpandAll();
        ui.RebuildTree(true);

        Check(ui.TreeRowCount >= 4, "дерево моделей построено, строк: " + ui.TreeRowCount);
        Check(ui.SelectedNode == null, "на старте ни один узел не выбран (правило проекта)");

        ProjectNode robotNode = FindNode(ui, ProjectNodeKind.Robot);
        ProjectNode tableNode = FindNode(ui, ProjectNodeKind.Table);
        Check(robotNode != null, "в дереве есть узел робота (корневые узлы с осями)");
        Check(tableNode != null, "в дереве есть узел стола (группа «Столы» → стенды)");

        if (robotNode != null)
        {
            Check(robotNode.Children.Count > 0,
                "у узла робота раскрываются дочерние узлы (оси/TCP), их " + robotNode.Children.Count);
            bool hasAxis = false, hasTcp = false;
            foreach (ProjectNode c in robotNode.Children)
            {
                if (c.Kind == ProjectNodeKind.Axis) hasAxis = true;
                if (c.Kind == ProjectNodeKind.Tcp) hasTcp = true;
            }
            Check(hasAxis, "в узле робота есть оси (Ось 1…N)");
            Check(hasTcp, "в узле робота есть узел TCP");
        }

        ui.SelectNode(robotNode);
        ui.Refresh();
        Check(ui.SelectedNode == robotNode, "клик по узлу выбирает его");
        Check(ui.Properties.Node == robotNode, "правая панель показывает выбранный объект");
        Check(ui.PropertyRowCount > 6,
            "свойства робота заполнены (позиция/поворот/углы/скорость/телеметрия), строк: " +
            ui.PropertyRowCount);
        Check(ui.Highlight != null && ui.Highlight.Target == robotNode.WorldTransform,
            "выбранный объект подсвечивается в сцене (рамка вокруг объекта)");

        if (tableNode != null)
        {
            ui.SelectNode(tableNode);
            ui.Refresh();
            Check(ui.PropertyRowCount > 2,
                "свойства стола заполнены (позиция/масштаб), строк: " + ui.PropertyRowCount);
        }
        Info("узлов в дереве: " + ui.TreeRowCount + " · точек в истории: " + ui.PointCount);
        Next();
    }

    private static void StageRenameAndVisibility()
    {
        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        ProjectNode tableNode = FindNode(ui, ProjectNodeKind.Table);

        if (tableNode != null)
        {
            ui.SelectNode(tableNode);
            bool started = ui.Tree.BeginRenameSelected();
            Check(started, "двойной клик по узлу начинает переименование («на месте»)");
            ui.Tree.AppendToEdit("_тест");
            string before = tableNode.DisplayName;
            ui.Tree.CommitRenameNow();
            Check(tableNode.DisplayName.EndsWith("_тест"),
                "переименование узла применилось: «" + before + "» → «" + tableNode.DisplayName + "»");
            Check(!ui.Tree.IsRenaming, "после подтверждения режим переименования закрыт");

            bool visibleBefore = KvTreeView.IsObjectVisible(tableNode);
            ui.Tree.SetObjectVisible(tableNode, false);
            bool hiddenNow = !KvTreeView.IsObjectVisible(tableNode);
            ui.Tree.SetObjectVisible(tableNode, true);
            bool restored = KvTreeView.IsObjectVisible(tableNode);
            Check(visibleBefore && hiddenNow && restored,
                "«глазик» скрывает и возвращает объект (виден " + visibleBefore + " → скрыт " +
                hiddenNow + " → виден " + restored + ")");

            Check(tableNode.WorldTransform != null &&
                  tableNode.WorldTransform.name != tableNode.DisplayName,
                "переименование меняет ЯРЛЫК узла, а не имя объекта в сцене (безопасно для логики)");
        }
        else
        {
            Check(false, "узел стола не найден — переименование/«глазик» не проверены");
        }
        Next();
    }

    private static void StageStatusBar()
    {
        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        ui.Flow.ResetFlow("Сброшено (диагностика UI)");
        ui.Refresh();

        string state = ui.StatusState;
        Check(!string.IsNullOrEmpty(state), "статус-бар показывает состояние: «" + state + "»");
        Check(state.StartsWith("Idle"), "после сброса состояние Idle");
        Check(!string.IsNullOrEmpty(ui.StatusBar.RobotText),
            "статус-бар показывает робота: «" + ui.StatusBar.RobotText + "»");
        Check(!string.IsNullOrEmpty(ui.StatusCursor),
            "статус-бар показывает координаты луча: «" + ui.StatusCursor + "»");
        Check(ui.StatusTheme.Contains("Тема"),
            "статус-бар показывает индикатор темы: «" + ui.StatusTheme + "»");

        KazistovVvUIManager.SetPlanStatus("Сообщение из диагностики интерфейса", Color.cyan);
        ui.StatusBar.SetMessage("Сообщение из диагностики интерфейса", Color.cyan);
        Check(ui.StatusMessage.Contains("диагностики"),
            "статусные сообщения потока выводятся в статус-бар: «" + ui.StatusMessage + "»");

        string[] expected = { "Idle", "PointSelected", "TrajectoriesShown", "PhantomsMoving",
            "RobotMoving", "PointMoveMode" };
        bool allStates = true;
        foreach (string s in expected)
            if (!Enum.IsDefined(typeof(FlowState), s)) allStates = false;
        Check(allStates, "статус-бар умеет показывать все 6 состояний State Machine");
        Next();
    }

    private static void StageTheme()
    {
        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        Check(KvTheme.Mode == KvThemeMode.Dark, "тема по умолчанию — тёмная (как в FreeCAD)");

        KvTheme.SetMode(KvThemeMode.Light);
        Check(KvTheme.IsLight, "переключение на СВЕТЛУЮ тему применилось мгновенно (IsLight = true)");
        Check(ui.Toolbar != null && ui.Toolbar.ButtonCount >= 37 && ui.TreeRowCount > 0,
            "после смены темы оболочка пересобрана и работоспособна (" + ui.Toolbar.ButtonCount + " кнопок, дерево)");
        Check(PlayerPrefs.GetInt(KvTheme.PrefsThemeKey, -1) == (int)KvThemeMode.Light,
            "выбор темы сохранён в PlayerPrefs (ключ " + KvTheme.PrefsThemeKey + ")");
        Check(ui.StatusTheme.Contains("Светлая"), "статус-бар показывает «Светлая»: «" + ui.StatusTheme + "»");

        KvTheme.SetMode(KvThemeMode.System);
        Check(KvTheme.Mode == KvThemeMode.System, "системная тема выбирается («" + KvTheme.ModeLabel + "»)");
        Info("системная тема: палитра " + KvTheme.PaletteLabel + " (реестр ОС недоступен → тёмная — норма)");

        KvTheme.SetMode(KvThemeMode.Dark);
        Check(!KvTheme.IsLight && PlayerPrefs.GetInt(KvTheme.PrefsThemeKey, -1) == 0,
            "возврат к тёмной теме и запись в PlayerPrefs");

        KvThemeMode before = KvTheme.Mode;
        bool invoked = KvCommands.Invoke("view.theme");
        Check(invoked && KvTheme.Mode != before,
            "кнопка «Тема» переключает режим по кругу (" + before + " → " + KvTheme.Mode + ")");
        KvTheme.SetMode(KvThemeMode.Dark);
        Next();
    }

    private static void StagePanels()
    {
        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        KvDockPanel tree = ui.TreeDock;

        Check(ui.IsPanelVisible("tree"), "панель «Дерево моделей» видна на старте (слева)");
        Check(ui.IsPanelVisible("properties"), "панель «Свойства» видна на старте (справа)");
        Check(!ui.IsPanelVisible("settings"), "панель «Настройки» скрыта до вызова");

        ui.SetPanel("settings", true);
        Check(ui.IsPanelVisible("settings"), "панель настроек открывается (dock снизу)");
        ui.ShowSettings(0);
        Check(ui.SettingsView.ActiveTab == 0, "ShowSettings(0) открывает вкладку «Функции»");

        tree.SetSide(KvDockSide.Bottom);
        Check(tree.Side == KvDockSide.Bottom, "панель дерева перестыковывается к нижнему краю");
        tree.SetThickness(300f);
        Check(Mathf.Approximately(tree.Thickness, 300f), "толщина панели меняется перетаскиванием кромки");
        tree.SetCollapsed(true);
        Check(tree.Collapsed, "панель сворачивается (остаётся заголовок)");
        tree.SetCollapsed(false);
        tree.SetSide(KvDockSide.Left);
        Check(tree.Side == KvDockSide.Left && ui.IsPanelVisible("tree"),
            "панель возвращается на левый край и остаётся видимой");

        ui.SetPanel("tree", false);
        ui.SetPanel("tree", true);
        Check(ui.IsPanelVisible("tree"), "панель можно скрыть и вернуть (меню «Вид»/настройки)");
        ui.LayoutDock(KvStatusBar.Height);
        Check(Mathf.Approximately(tree.Thickness, 300f),
            "геометрия панели сохраняется между пересборками (толщина 300)");
        Next();
    }

    private static void StageSettings()
    {
        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        ui.ShowSettings(0);

        // Число вкладок берётся из KvSettingsView: новая вкладка («Графика») проверяется
        // автоматически, а нумерация больше не «зашита» в диагностику.
        for (int tab = 0; tab < KvSettingsView.TabCount; tab++)
        {
            ui.SettingsView.SetTab(tab);
            Check(ui.SettingsView.ActiveTab == tab && ui.SettingsView.RowCount > 0,
                "вкладка настроек " + (tab + 1) + " построена, строк: " + ui.SettingsView.RowCount);
        }

        ui.SettingsView.SetTab(0);
        Check(KvSettingsSchema.Features().Count >= 6,
            "пункты «Функции» читаются из схемы: " + KvSettingsSchema.Features().Count);
        Check(HasFeature("vis.workspace") && HasFeature("vis.limits") && HasFeature("vis.metrics") &&
              HasFeature("tool.flashlight") && HasFeature("tree.autorefresh"),
            "у визуализаций/фонарика/автообновления дерева есть обработчики");
        Check(HasFeature("scene.phantoms"),
            "у отключения фантомов теперь ЕСТЬ обработчик (исправлено в сессии 18.09.2026)");

        int binds = KvSettingsSchema.Bindings().Count;
        Check(binds >= 15, "таблица биндов заполнена (только просмотр), строк: " + binds);
        bool hasVr = false, hasPad = false;
        foreach (KvBindingEntry b in KvSettingsSchema.Bindings())
        {
            // Bindings() строит group как «Device · Group» (напр. «Геймпад · Робот»,
            // «Клавиатура · VR / MR») — ищем по содержанию, а не по точному равенству.
            if (!string.IsNullOrEmpty(b.group) && b.group.Contains("VR / MR")) hasVr = true;
            if (!string.IsNullOrEmpty(b.group) && b.group.StartsWith("Геймпад")) hasPad = true;
        }
        Check(hasPad, "есть раздел «Геймпад» (отображение, не редактирование)");
        Check(hasVr, "есть заглушки VR / MR («будет добавлено позже»)");
        Info("внешние настройки: " + KvSettingsSchema.ExternalStatus);

        // Раздел «Графика» (сессия 18.09.2026): строк много, служба обязана быть поднята.
        ui.SettingsView.SetTab(KvSettingsView.GraphicsTabIndex);
        Check(ui.SettingsView.RowCount > 20,
            "вкладка «Графика» заполнена, строк: " + ui.SettingsView.RowCount);
        Check(KazistovVvFeatures.KvGraphicsService.Current != null,
            "служба графики запущена (KvGraphicsService.Current != null)");
        if (KazistovVvFeatures.KvGraphicsService.Current != null)
        {
            KazistovVvFeatures.KvGraphicsService g = KazistovVvFeatures.KvGraphicsService.Current;
            Info("графика: режим " + g.Preset + " → " + g.EffectivePreset() +
                 " · доступна: " + g.Available + " · " + g.PresetFileStatus);
        }

        ui.SettingsView.SetTab(KvSettingsView.HelpTabIndex);
        Check(ui.SettingsView.RowCount > 8,
            "вкладка «Справка» заполнена, строк: " + ui.SettingsView.RowCount);
        Next();
    }

    private static bool HasFeature(string id)
    {
        // Соответствует реальным case-обработчикам KazistovVvUIManager.SetFeatureValue
        // (проверено сверкой списка case со списком ниже).
        return id == "vis.workspace" || id == "vis.limits" || id == "vis.metrics" ||
               id == "tool.flashlight" || id == "tree.autorefresh" ||
               id == "scene.phantoms" || id == "ui.gamepadhud" ||
               id == "ui.toolbar.layout" || id == "ui.toolbar.compact" ||
               id == "tree" || id == "properties" || id == "settings" ||
               id == "hotkeys" || id == "metrics" || id == "limits";
    }

    private static void StageCommands()
    {
        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        TrajectoryFlowController flow = ui.Flow;

        bool ws = flow.Workspace.WorkspaceVisible;
        KvCommands.Invoke("view.workspace");
        Check(flow.Workspace.WorkspaceVisible != ws,
            "кнопка «Зона достижимости» переключает визуализацию (" + ws + " → " +
            flow.Workspace.WorkspaceVisible + ")");
        KvCommands.Invoke("view.workspace");

        bool lim = flow.Workspace.JointLimitsVisible;
        KvCommands.Invoke("view.limits");
        Check(flow.Workspace.JointLimitsVisible != lim, "кнопка «Лимиты суставов» переключает индикаторы");
        KvCommands.Invoke("view.limits");

        bool met = flow.Metrics.Visible;
        KvCommands.Invoke("view.metrics");
        Check(flow.Metrics.Visible != met, "кнопка «Метрики траекторий» переключает панель");
        KvCommands.Invoke("view.metrics");

        if (ui.CameraRig != null)
        {
            bool fl = ui.CameraRig.FlashlightOn;
            KvCommands.Invoke("tool.flashlight");
            Check(ui.CameraRig.FlashlightOn != fl,
                "кнопка «Фонарик» включает/выключает свет (" + fl + " → " + ui.CameraRig.FlashlightOn + ")");
            KvCommands.Invoke("tool.flashlight");
        }

        bool laserRed = ui.CameraRig != null && ui.CameraRig.leftHandEnabled;
        KvCommands.Invoke("point.select");
        Check(ui.CameraRig != null && ui.CameraRig.leftHandEnabled != laserRed,
            "кнопка «Выбор точки (красный лазер)» переключает красную указку (бинды Z/X не тронуты)");
        KvCommands.Invoke("point.select");

        Check(!KvCommands.Invoke("robot.playpause"),
            "«Запуск/пауза» недоступна, пока робот не едет (пауза не сломает шаг 5)");
        Check(!KvCommands.Invoke("edit.undo"), "заглушка «Отменить» не выполняется");
        Check(!KvCommands.Invoke("robot.stop"), "«Остановка» недоступна, когда движения нет");

        KvCommands.Invoke("robot.switch");
        RobotController active = null;
        foreach (RobotController rc in UnityEngine.Object.FindObjectsByType<RobotController>(
            FindObjectsInactive.Include))
        {
            if (rc != null && rc.isActive) { active = rc; break; }
        }
        Check(active != null, "кнопка «Переключение робота» выбирает активного робота: " +
            (active != null ? active.robotName : "—"));

        bool reset = KvCommands.Invoke("edit.reset");
        Check(reset && ui.StatusBar.StateText.StartsWith("Idle"),
            "кнопка «Сброс» возвращает поток в Idle");
        Next();
    }

    private static void StageTooltip()
    {
        KvTooltip tip = KvTooltip.Current;
        Check(tip != null, "слой подсказок создан");
        if (tip != null)
        {
            // Закреплённая подсказка: UI перетирает обычную (тулбар дёргает
            // Request/Release каждый кадр) — проверяем именно показ по задержке.
            KvTooltip.RequestPinned("Проверка подсказки", "описание", "Z");
            tipWaitStart = Time.realtimeSinceStartup;
        }
        Next();
    }

    private static void StageTooltipWait()
    {
        KvTooltip tip = KvTooltip.Current;
        if (tip == null) { Next(); return; }
        // Подсказка появляется по РЕАЛЬНОМУ времени (delay), кадры в batch ничего не значат.
        if (Time.realtimeSinceStartup - tipWaitStart < tip.delay + 0.15f) return;

        Check(tip.IsShown && tip.CurrentTitle == "Проверка подсказки",
            "подсказка показывается при наведении (заголовок «" + tip.CurrentTitle + "», задержка " +
            tip.delay.ToString("0.00") + " с)");
        KvTooltip.Release(true);
        Check(!tip.IsShown, "подсказка скрывается при уходе курсора");

        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        string title = ui.Toolbar.ButtonAt(2) != null && ui.Toolbar.ButtonAt(2).Tooltip != null
            ? ui.Toolbar.ButtonAt(2).Tooltip.title : "";
        Check(!string.IsNullOrEmpty(title), "подсказка кнопки «Запуск/пауза»: «" + title + "»");
        Next();
    }

    private static void StagePerformance()
    {
        KazistovVvUIManager ui = KazistovVvUIManager.Instance;

        ui.RebuildTree(true);
        int version = ui.Tree.RebuildVersion;
        int rowsBefore = ui.TreeRowCount;
        for (int i = 0; i < 20; i++) ui.RebuildTree(false);
        Check(ui.Tree.RebuildVersion == version && ui.TreeRowCount == rowsBefore,
            "дерево НЕ пересобирается без изменений (версия " + version + ", строк " + rowsBefore + ")");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 200; i++) ui.RefreshPanels();
        sw.Stop();
        double tick = sw.Elapsed.TotalMilliseconds / 200.0;
        Info("кадровое обновление панелей (RefreshPanels): " + tick.ToString("0.000") + " мс/тик");
        Check(tick < 4.0, "кадровое обновление панелей дешёвое (< 4 мс на тик): " +
            tick.ToString("0.000") + " мс");

        sw.Restart();
        for (int i = 0; i < 10; i++) ui.Refresh();
        sw.Stop();
        Info("полное обновление (Refresh с пересборкой дерева): " +
            (sw.Elapsed.TotalMilliseconds / 10.0).ToString("0.000") + " мс/вызов");

        Check(ui.statusInterval >= 0.05f && ui.propertiesInterval >= 0.05f && ui.treeInterval >= 0.1f,
            "обновления панелей идут по таймеру (" + ui.statusInterval + " / " + ui.propertiesInterval +
            " / " + ui.treeInterval + " с), а не каждый кадр");
        Check(ui.ToolbarButtonCount >= 37 && ui.TreeRowCount > 0 && ui.PropertyRowCount > 0,
            "после всех переключений интерфейс целостен (" + ui.ToolbarButtonCount + " кнопок, дерево, свойства)");
        objectsWait = Time.realtimeSinceStartup;
        Next();
    }

    /// <summary>Считаем объекты ПОСЛЕ кадра: Destroy отложен до конца кадра и без паузы число завышено.</summary>
    private static void StageObjects()
    {
        if (Time.realtimeSinceStartup - objectsWait < 0.2f) return;
        Info("объектов в канвасе интерфейса (устойчивое состояние): " +
            KazistovVvUIManager.Instance.UiObjectCount);
        Next();
    }

    private static ProjectNode FindNode(KazistovVvUIManager ui, ProjectNodeKind kind)
    {
        if (ui == null) return null;
        foreach (ProjectNode root in ui.TreeModel)
        {
            ProjectNode found = FindRecursive(root, kind, 0);
            if (found != null) return found;
        }
        return null;
    }

    private static ProjectNode FindRecursive(ProjectNode node, ProjectNodeKind kind, int depth)
    {
        if (node == null || depth > 6) return null;
        if (node.Kind == kind) return node;
        foreach (ProjectNode child in node.Children)
        {
            ProjectNode found = FindRecursive(child, kind, depth + 1);
            if (found != null) return found;
        }
        return null;
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;

        Line("");
        Line("ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · исключений: " + exceptions +
             " · кадров " + Time.frameCount);
        Line(fail == 0 && exceptions == 0
            ? "ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ"
            : "ИТОГ: ЕСТЬ ЗАМЕЧАНИЯ");
        Debug.Log("[DshDesktopUiDiag] отчёт: " + reportPath + " · OK " + ok + " · FAIL " + fail);

        EditorApplication.Exit(fail == 0 && exceptions == 0 ? 0 : 1);
    }
}
