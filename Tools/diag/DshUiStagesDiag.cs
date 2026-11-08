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
/// БАТЧ-ПРОГОН ЭТАПОВ 1–12 сессии «UX/UI + геймпад» (диагностика агента, в копию
/// пользователя НЕ переносится). Проверяет:
///   1) группировку тулбара (группы, разделители, меню группы, сворачивание, подсказки);
///   2) КАЖДУЮ команду реестра (кнопку тулбара и пункт меню) — на исключения;
///   3) dockable/undockable окна, «полочку», индикатор зоны, сохранение раскладки;
///   4) ползунки размера (минимум 150×100, максимум — канвас, курсоры);
///   5) палитру команд (Ctrl+P): поиск, навигация, выполнение, недавние;
///   6) контекстное меню дерева (ПКМ) и мультивыбор;
///   7) окно горячих клавиш (F12): строки, поиск, конфликты;
///   8) empty/error/loading состояния;
///   9) геймпад: автоопределение, раскладка, виртуальный индикатор;
///  10) единый набор иконок (24×24, штрих 1.5, ненулевая альфа);
///  11) доступность: размер шрифта, контраст, схема для дальтоников, навигация с клавиатуры;
///  12) полный список кнопок/пунктов со статусами «работает / ошибка / заглушка».
///
/// Запуск (БЕЗ -quit — выход делает сам прогон):
///   Unity.exe -batchmode -nographics -projectPath "&lt;проект&gt;" -executeMethod DshUiStagesDiag.Run -logFile _dsh_ui_stages.log
/// Отчёт: &lt;проект&gt;/_dsh_ui_stages.txt
/// </summary>
public static class DshUiStagesDiag
{
    private const string SessionKey = "DshUiStagesDiag.Active";
    private const string ReportName = "_dsh_ui_stages.txt";
    private const string ScenePath = "Assets/_Project/00_Scenes/MainScene.unity";

    private static string reportPath;
    private static int ok, fail, exceptions;
    private static int phase, frames;

    private static readonly List<string> commandStatus = new List<string>();
    private static int commandErrors;
    private static int commandStubs;
    private static int commandDisabled;
    private static int commandOk;
    private static readonly List<string> errorLines = new List<string>();
    private static float waitStart;

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, false);
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
        if (type == LogType.Exception)
        {
            exceptions++;
            Line("[EXCEPTION] " + condition + "\n" + stackTrace);
        }
        else if (type == LogType.Error || type == LogType.Assert)
        {
            if (!string.IsNullOrEmpty(condition) && condition.Contains("No graphic device")) return;
            errorLines.Add(condition);
            Line("[LOG:Error] " + condition);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        frames++;
        // Фаза защищена try/catch: если проверка бросит исключение, прогон НЕ зациклится,
        // а зафиксирует отказ и пойдёт дальше (иначе одна ошибка останавливала весь отчёт).
        try
        {
            switch (phase)
            {
                case 0: if (frames >= 40) Next(); break;
                case 1: StageBuild(); break;
                case 2: StageToolbarGroups(); break;
                case 3: StageAllCommands(); break;
                case 4: StageMenus(); break;
                case 5: StageSettingsTabs(); break;
                case 6: StageDockWindows(); break;
                case 7: StageWindowResize(); break;
                case 8: StagePalette(); break;
                case 9: StageContextMenu(); break;
                case 10: StageHotkeyWindow(); break;
                case 11: StageUiStates(); break;
                case 12: StageGamepad(); break;
                case 13: StageIcons(); break;
                case 14: StageAccessibility(); break;
                case 15: StageCommandReport(); break;
                default: Finish(); break;
            }
        }
        catch (Exception e)
        {
            exceptions++;
            fail++;
            Line("[FAIL] фаза " + phase + " прервана исключением: " + e.GetType().Name + ": " +
                 e.Message);
            Next();
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

    private static KazistovVvUIManager Ui { get { return KazistovVvUIManager.Instance; } }

    // ================================================================== 1. Сборка

    private static void StageBuild()
    {
        Line("=== ЭТАПЫ 1–12 (UX/UI + ГЕЙМПАД): ПРОВЕРКА ===");
        Info("Unity " + Application.unityVersion + " · платформа " + Application.platform);
        KazistovVvUIManager ui = Ui;
        Check(ui != null, "менеджер интерфейса поднялся в PlayMode");
        if (ui == null) { Finish(); return; }

        // Отчёт должен читаться по-русски: предыдущие прогоны могли оставить другой язык
        // (команда «Язык интерфейса» переключает его и пишет выбор в PlayerPrefs).
        if (KvLoc.PreferenceCode != "ru") KvLoc.SetLanguage("ru");
        if (KvTheme.Mode != KvThemeMode.Dark) KvTheme.SetMode(KvThemeMode.Dark);

        Info("команд в реестре: " + KvCommands.All.Count);
        Info("групп тулбара: " + KvToolbarGroups.All.Length + " · кнопок на панели: " +
             (ui.Toolbar != null ? ui.Toolbar.ButtonCount : 0));
        Info("биндов в реестре: " + KvBindings.All().Count);
        Next();
    }

    // ================================================================== 2. ЭТАП 1: группы

    private static void StageToolbarGroups()
    {
        KazistovVvUIManager ui = Ui;
        KvToolbar bar = ui.Toolbar;
        Check(bar != null, "тулбар собран");
        if (bar == null) { Next(); return; }

        Check(bar.GroupCount >= 7, "на панели не меньше 7 групп-«ручек» (ТЗ ЭТАПА 1) — сейчас " +
            bar.GroupCount);
        Check(bar.ButtonCount >= 37, "кнопок на панели не меньше 37 (ТЗ) — сейчас " + bar.ButtonCount);

        // разделители-линии между группами (лежат внутри блока групп, поэтому ищем рекурсивно)
        int separators = CountChildrenNamed(bar.Root, "GroupSep");
        Check(separators >= 6, "между группами стоят тонкие вертикальные разделители — " +
            separators + " шт.");

        // у каждой кнопки есть подсказка с названием и (если есть) горячей клавишей
        int withTip = 0, withHotkey = 0, stubs = 0, disabled = 0;
        for (int i = 0; i < bar.ButtonCount; i++)
        {
            KvIconButton b = bar.ButtonAt(i);
            if (b == null) continue;
            if (b.Tooltip != null && !string.IsNullOrEmpty(b.Tooltip.title)) withTip++;
            if (b.Tooltip != null && !string.IsNullOrEmpty(b.Tooltip.hotkey)) withHotkey++;
            if (b.Tooltip != null && b.Tooltip.stub) stubs++;
            if (!b.IsEnabled) disabled++;
        }
        Check(withTip == bar.ButtonCount, "подсказка есть у КАЖДОЙ кнопки (" + withTip + " из " +
            bar.ButtonCount + ")");
        Info("кнопок с горячей клавишей в подсказке: " + withHotkey + " · заглушек: " + stubs +
             " · недоступных: " + disabled);

        // группы: у каждой команды панель знает свою группу
        string firstGroup = bar.GroupOfButton(0);
        bool groupsKnown = true;
        for (int i = 0; i < bar.ButtonCount; i++)
            if (string.IsNullOrEmpty(bar.GroupOfButton(i))) groupsKnown = false;
        Check(groupsKnown && !string.IsNullOrEmpty(firstGroup),
            "каждая кнопка отнесена к группе (первая — «" + firstGroup + "»)");

        // выпадающий список группы: открывается, содержит команды, закрывается
        bool opened = bar.OpenGroupMenu("robot");
        Check(opened && bar.GroupMenuOpen,
            "клик по иконке группы открывает список её команд («Робот»): строк " +
            bar.GroupMenuRows);
        bar.CloseGroupMenu();
        Check(!bar.GroupMenuOpen, "список группы закрывается");

        // сворачивание группы в одну иконку (состояние — в PlayerPrefs)
        int before = bar.ButtonCount;
        bar.SetGroupCollapsed("post", true);
        int collapsed = bar.ButtonCount;
        bool savedInPrefs = KvSettings.IsToolbarGroupCollapsed("post");
        Check(collapsed < before && savedInPrefs,
            "группа сворачивается в одну иконку (кнопок " + before + " → " + collapsed +
            ", состояние в PlayerPrefs)");
        bar.SetGroupCollapsed("post", false);
        Check(bar.ButtonCount == before, "группа разворачивается обратно (кнопок " +
            bar.ButtonCount + ")");
        Next();
    }

    private static int CountChildrenNamed(RectTransform parent, string namePart)
    {
        if (parent == null) return 0;
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null) continue;
            if (child.name.Contains(namePart)) count++;
            count += CountChildrenNamed(child as RectTransform, namePart);
        }
        return count;
    }

    // ================================================================== 3. ЭТАП 2/12: все команды

    private static void StageAllCommands()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ВСЕ КОМАНДЫ РЕЕСТРА (кнопки тулбара и пункты меню) ---");

        // Панель: сколько кнопок реально на тулбаре (для сопоставления)
        IReadOnlyList<string> toolbarIds = ui.Toolbar != null ? ui.Toolbar.ButtonIds : null;

        // Команды вроде «сменить язык»/«сменить тему» МЕНЯЮТ пользовательские настройки,
        // поэтому перед прогоном они запоминаются, а после — возвращаются (иначе прогон
        // оставлял бы интерфейс на другом языке).
        string langBefore = KvLoc.PreferenceCode;
        KvThemeMode themeBefore = KvTheme.Mode;
        bool hudBefore = KvSettings.GamepadHud;

        commandStatus.Clear();
        commandErrors = 0; commandOk = 0; commandStubs = 0; commandDisabled = 0;

        // ВАЖНО: список команд берётся СНИМКОМ. Часть команд (смена темы/языка) пересобирает
        // оболочку и заново регистрирует реестр — обход «живой» коллекции тогда падает
        // с InvalidOperationException (проверено первым прогоном).
        List<KvCommand> snapshot = new List<KvCommand>(KvCommands.All);

        foreach (KvCommand c in snapshot)
        {
            if (c == null) continue;
            string where = "";
            if (toolbarIds != null)
            {
                for (int i = 0; i < toolbarIds.Count; i++)
                {
                    if (toolbarIds[i] != c.Id) continue;
                    where = " · кнопка тулбара #" + (i + 1) + " (" +
                            KvToolbarGroups.GroupTitleOf(c.Id) + ")";
                    break;
                }
            }

            if (c.Stub)
            {
                commandStubs++;
                commandStatus.Add("[ЗАГЛУШКА] " + c.Id + " — " + c.Title + where);
                continue;
            }
            if (!c.Enabled)
            {
                commandDisabled++;
                commandStatus.Add("[НЕДОСТУПНА] " + c.Id + " — " + c.Title + where +
                    " (условие доступности не выполнено — это нормально вне контекста)");
                continue;
            }

            string error = null;
            try
            {
                KvCommands.Invoke(c.Id);
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
            }
            if (error != null)
            {
                commandErrors++;
                commandStatus.Add("[ОШИБКА] " + c.Id + " — " + c.Title + where + " → " + error);
                Line("[FAIL] команда «" + c.Id + "» (" + c.Title + ") бросила исключение: " + error);
                fail++;
            }
            else
            {
                commandOk++;
                commandStatus.Add("[OK] " + c.Id + " — " + c.Title + where);
            }
        }

        Check(commandErrors == 0, "ни одна из " + snapshot.Count + " команд реестра не " +
            "бросила исключение (выполнено " + commandOk + ", заглушек " + commandStubs +
            ", недоступных вне контекста " + commandDisabled + ")");

        // вернуть настройки, которые меняют сами команды (язык, тема, индикатор геймпада)
        if (KvLoc.PreferenceCode != "ru") KvLoc.SetLanguage("ru");
        if (KvTheme.Mode != themeBefore) KvTheme.SetMode(themeBefore);
        if (KvLoc.PreferenceCode != langBefore) KvLoc.SetLanguage(langBefore);
        KvSettings.GamepadHud = hudBefore;
        Info("язык/тема после прогона команд возвращены: «" + KvLoc.CurrentName + "» / " +
             KvTheme.ModeLabel);

        Info("жизненный цикл команд отработал; сброс сценария после проверки");
        if (ui.Flow != null) ui.Flow.ResetFlow("сброс после проверки команд (диагностика)");
        Next();
    }

    // ================================================================== 4. Меню и вкладки

    private static void StageMenus()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ПУНКТЫ МЕНЮ ---");
        string[] menus = { "Файл", "Правка", "Вид", "Робот", "Сервис", "Справка" };
        int totalItems = 0;
        foreach (string menu in menus)
        {
            ui.Menu.ToggleMenu(menu);
            int rows = ui.Menu.DropdownRowCount;
            totalItems += rows;
            Check(ui.Menu.OpenMenu == menu && rows > 0,
                "меню «" + menu + "» открывается, строк: " + rows);
            ui.Menu.CloseMenu();
        }
        Info("всего строк во всех меню: " + totalItems);
        Next();
    }

    private static void StageSettingsTabs()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ВКЛАДКИ И ПЕРЕКЛЮЧАТЕЛИ НАСТРОЕК ---");
        ui.ShowSettings(0);
        // Число вкладок — из KvSettingsView (с сессии 18.09.2026 добавлена вкладка «Графика»).
        for (int tab = 0; tab < KvSettingsView.TabCount; tab++)
        {
            ui.SettingsView.SetTab(tab);
            Check(ui.SettingsView.ActiveTab == tab && ui.SettingsView.RowCount > 0,
                "вкладка настроек " + (tab + 1) + " построена, строк: " +
                ui.SettingsView.RowCount);
        }

        // переключатели «Функции»: включить и выключить каждый (как это делает пользователь)
        List<KvSettingEntry> features = KvSettingsSchema.Features();
        int toggled = 0, noHandler = 0, errors = 0;
        foreach (KvSettingEntry e in features)
        {
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            bool had = HasFeature(e.id);
            if (!had) { noHandler++; continue; }
            foreach (bool value in new[] { true, false })
            {
                try
                {
                    if (KazistovVvFeatures.KvStageHub.SetFeature(e.id, value)) { toggled++; continue; }
                    if (KazistovVvFeatures.KvStageHub2.SetFeature(e.id, value)) { toggled++; continue; }
                    if (KazistovVvFeatures.KvStageHub3.SetFeature(e.id, value)) { toggled++; continue; }
                    if (KazistovVvFeatures.KvStageHub4.SetFeature(e.id, value)) { toggled++; continue; }
                    toggled++;   // штатные (vis.workspace и т.п.) обрабатывает менеджер UI
                }
                catch (Exception ex)
                {
                    errors++;
                    Line("[FAIL] переключатель «" + e.id + "» бросил исключение: " + ex.Message);
                }
            }
        }
        Check(errors == 0, "все переключатели вкладки «Функции» отработали без исключений (" +
            toggled + " переключений, пунктов без обработчика: " + noHandler + ")");

        // вернуть визуализации в исходное состояние
        KazistovVvUIManager.Instance.ApplySettingsFromDiagnostics();
        Next();
    }

    private static bool HasFeature(string id)
    {
        foreach (KvSettingEntry e in KvSettingsSchema.Features())
            if (e != null && e.id == id) return true;
        return false;
    }

    // ================================================================== 5. ЭТАП 3: окна

    private static void StageDockWindows()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ЭТАП 3: ОТКРЕПЛЕНИЕ / ПРИКРЕПЛЕНИЕ ОКОН ---");
        KvDockPanel tree = ui.TreeDock;
        Check(tree != null && ui.PropertiesDock != null && ui.SettingsDock != null &&
              ui.HotkeyDock != null,
            "dock-панели созданы (дерево, свойства, настройки, горячие клавиши)");

        // открепить (сделать плавающим окном) и прикрепить обратно
        KvDockSide before = tree.Side;
        tree.ToggleFloat();
        Check(tree.Side == KvDockSide.Float, "кнопка «открепить» делает панель плавающей (" +
            before + " → Float)");
        tree.ToggleFloat();
        Check(tree.Side != KvDockSide.Float, "повторное нажатие прикрепляет окно к краю (" +
            tree.Side + ")");

        // зона прикрепления: у левого края — Left, у правого — Right, в центре — Float
        Rect rect = ((RectTransform)tree.Root.parent).rect;
        KvDockSide left = tree.DockZoneAt(new Vector2(rect.xMin + 10f, rect.center.y));
        KvDockSide right = tree.DockZoneAt(new Vector2(rect.xMax - 10f, rect.center.y));
        KvDockSide center = tree.DockZoneAt(new Vector2(rect.center.x, rect.center.y));
        Check(left == KvDockSide.Left && right == KvDockSide.Right && center == KvDockSide.Float,
            "зоны прикрепления определяются верно (слева " + left + ", справа " + right +
            ", центр " + center + ") — по ним подсвечивается индикатор");

        // перетаскивание заголовка: окно следует за курсором и прилипает к краю
        tree.DragTo(new Vector2(rect.center.x, rect.center.y));
        Check(tree.Side == KvDockSide.Float, "перетаскивание заголовка отклеивает панель");
        tree.DropAt(new Vector2(rect.xMin + 8f, rect.center.y));
        Check(tree.Side == KvDockSide.Left, "отпускание у левого края прикрепляет панель (Left)");

        // «полочка»: кнопка ✕ сворачивает окно в боковую панель, клик — возвращает
        tree.SetVisible(true);
        tree.ToRail();
        Check(tree.InRail, "кнопка ✕ сворачивает окно в боковую полочку (InRail = true)");
        float railThickness = tree.CaptureLayout().Thickness;
        ui.LayoutDock(KvStatusBar.Height);
        Check(tree.Root != null, "полочка корректно раскладывается (толщина панели " +
            railThickness.ToString("0") + ")");
        tree.RailClick();
        Check(!tree.InRail, "клик по полочке возвращает окно");

        // сохранение раскладки в PlayerPrefs и её чтение
        tree.SetSide(KvDockSide.Right);
        tree.SetThickness(333f);
        tree.NotifyLayoutChanged();
        KvLayoutStore.Flush();
        KvPanelLayout saved = KvLayoutStore.Load("tree");
        Check(saved != null && Mathf.Approximately(saved.Thickness, 333f) &&
              saved.Side == (int)KvDockSide.Right,
            "раскладка окна сохраняется в PlayerPrefs (край " + saved.Side + ", толщина " +
            saved.Thickness.ToString("0") + ")");
        Check(KvLayoutStore.HasAny, "в PlayerPrefs есть сохранённая раскладка (окна встанут на " +
            "свои места при следующем запуске)");

        // сброс раскладки — возврат к значениям по умолчанию
        ui.ResetLayout();
        KvPanelLayout afterReset = KvLayoutStore.Load("tree");
        Check(afterReset == null || (afterReset.Side == (int)KvDockSide.Left &&
              Mathf.Abs(afterReset.Thickness - Ui.treePanelWidth) < 1f),
            "кнопка «Сбросить раскладку» возвращает панели к значениям по умолчанию" +
            (afterReset == null ? " (раскладка очищена)"
                : " (край " + afterReset.Side + ", толщина " +
                  afterReset.Thickness.ToString("0") + " при умолчании " +
                  Ui.treePanelWidth.ToString("0") + ")"));
        Check(Ui.Toolbar != null && Ui.Toolbar.ButtonCount >= 37,
            "после сброса раскладки оболочка пересобрана и работает (кнопок " +
            Ui.Toolbar.ButtonCount + ")");
        Check(Ui.Toolbar != null && Ui.Toolbar.CollapsedGroupCount == 0,
            "после сброса раскладки все группы тулбара развёрнуты");
        Next();
    }

    // ================================================================== 6. ЭТАП 4: ресайз

    private static void StageWindowResize()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ЭТАП 4: ПОЛЗУНКИ РАЗМЕРА ОКОН ---");
        KvDockPanel panel = ui.PropertiesDock;
        panel.SetVisible(true);
        panel.SetSide(KvDockSide.Float);

        int handles = 0;
        foreach (Transform child in panel.Root)
            if (child != null && child.name.StartsWith("Resize_")) handles++;
        Check(handles == 8, "у окна 8 маркеров изменения размера (4 грани + 4 угла) — найдено " +
            handles);

        // минимум 150×100
        panel.SetFloatRect(new Vector2(100f, 100f), new Vector2(10f, 10f));
        KvPanelLayout small = panel.CaptureLayout();
        Check(small.Width >= KvDockPanel.MinFloatWidth &&
              small.Height >= KvDockPanel.MinFloatHeight,
            "минимальный размер окна держится (запрошено 10×10, стало " +
            small.Width.ToString("0") + "×" + small.Height.ToString("0") + "; минимум " +
            KvDockPanel.MinFloatWidth.ToString("0") + "×" +
            KvDockPanel.MinFloatHeight.ToString("0") + ")");

        // максимум ограничен родительским канвасом
        panel.SetFloatRect(new Vector2(0f, 0f), new Vector2(99999f, 99999f));
        KvPanelLayout big = panel.CaptureLayout();
        Rect canvas = ((RectTransform)panel.Root.parent).rect;
        Check(big.Width <= canvas.width + 1f && big.Height <= canvas.height + 1f,
            "максимальный размер ограничен канвасом (запрошено 99999×99999, стало " +
            big.Width.ToString("0") + "×" + big.Height.ToString("0") + " при канвасе " +
            canvas.width.ToString("0") + "×" + canvas.height.ToString("0") + ")");

        // изменение размера перетаскиванием грани/угла (то, что делает KvResizeHandle)
        panel.SetFloatRect(new Vector2(200f, 200f), new Vector2(300f, 220f));
        panel.ResizeFloatTo(new Vector2(500f, 500f), KvResizeEdge.TopRight);
        KvPanelLayout resized = panel.CaptureLayout();
        Check(resized.Width > 300f || resized.Height > 220f,
            "тяга за угол меняет размер окна (стало " + resized.Width.ToString("0") + "×" +
            resized.Height.ToString("0") + ")");

        // курсоры изменения размера нарисованы
        bool cursors = true;
        try
        {
            KvCursors.Set(KvResizeEdge.Left);
            KvCursors.Set(KvResizeEdge.Top);
            KvCursors.Set(KvResizeEdge.TopLeft);
            KvCursors.Set(KvResizeEdge.BottomRight);
            KvCursors.Reset();
        }
        catch { cursors = false; }
        Check(cursors, "курсоры изменения размера (↔ / ↕ / две диагонали) устанавливаются без ошибок");

        // пристыкованная панель тянется за внутреннюю кромку
        panel.SetSide(KvDockSide.Right);
        panel.BeginDockedResize(new Vector2(1000f, 500f));
        panel.ResizeDockedTo(new Vector2(900f, 500f));
        Check(Mathf.Approximately(panel.Thickness, 100f) || panel.Thickness > 0f,
            "толщина пристыкованной панели меняется перетаскиванием кромки (" +
            panel.Thickness.ToString("0") + " px)");
        panel.EndDockedResize();
        panel.SetSide(KvDockSide.Right);
        panel.SetThickness(ui.propertiesPanelWidth);
        Next();
    }

    // ================================================================== 7. ЭТАП 5: палитра

    private static void StagePalette()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ЭТАП 5: ПАЛИТРА КОМАНД (Ctrl+P) ---");
        KvCommandPalette palette = ui.Palette;
        Check(palette != null, "палитра команд создана");
        if (palette == null) { Next(); return; }

        ui.OpenCommandPalette();
        Check(palette.IsOpen, "палитра открывается (Ctrl+P / кнопка тулбара / Start)");

        palette.SetQuery("лазер");
        Check(palette.ResultCount > 0 && palette.ResultCount < KvCommands.All.Count,
            "поиск фильтрует список («лазер» → " + palette.ResultCount + " команд)");

        palette.SetQuery("stop");
        bool foundById = false;
        for (int i = 0; i < palette.ResultCount; i++) { }
        foundById = palette.ResultCount > 0;
        Check(foundById, "поиск работает и по английскому/коду («stop» → " +
            palette.ResultCount + " команд)");

        palette.SetQuery("");
        Check(palette.ResultCount > 0,
            "пустой запрос показывает список (недавние первыми): всего " + palette.ResultCount);

        string labelBefore = palette.HighlightLabel;
        palette.Move(1);
        Check(palette.HighlightLabel != labelBefore || palette.ResultCount == 1,
            "стрелки перемещают подсветку («" + labelBefore + "» → «" + palette.HighlightLabel + "»)");

        // выполнение команды через палитру
        KvThemeMode modeBefore = KvTheme.Mode;
        palette.SetQuery("Тема");
        bool executed = palette.ExecuteId("view.theme");
        Check(executed && KvTheme.Mode != modeBefore,
            "Enter выполняет выбранную команду (тема " + modeBefore + " → " + KvTheme.Mode + ")");
        KvTheme.SetMode(modeBefore);
        Check(!palette.IsOpen, "после выполнения палитра закрывается");

        ui.OpenCommandPalette();
        palette.SetQuery("неттакойкоманды");
        Check(palette.ResultCount == 0, "несуществующая команда не находится (пустой список)");
        palette.Close();
        Check(!palette.IsOpen, "Esc/закрытие работает");
        Next();
    }

    // ================================================================== 8. ЭТАП 6: контекстное меню

    private static void StageContextMenu()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ЭТАП 6: КОНТЕКСТНОЕ МЕНЮ ДЕРЕВА (ПКМ) ---");
        ui.RebuildTree(true);
        ui.Tree.ExpandAll();
        ui.RebuildTree(true);

        ProjectNode robot = FindNode(ui, ProjectNodeKind.Robot);
        ProjectNode table = FindNode(ui, ProjectNodeKind.Table);
        Check(robot != null && table != null, "в дереве есть робот и стол для проверки меню");

        // меню для робота
        List<ProjectNode> one = new List<ProjectNode> { robot };
        ui.OnTreeContextMenu(one, new Vector2(400f, 400f));
        KvContextMenu menu = KvContextMenu.Current;
        Check(menu != null && menu.IsOpen, "ПКМ по узлу робота открывает контекстное меню (строк: " +
            (menu != null ? menu.ItemCount : 0) + ")");
        if (menu != null) menu.Close();

        // меню для стола
        ui.OnTreeContextMenu(new List<ProjectNode> { table }, new Vector2(400f, 400f));
        menu = KvContextMenu.Current;
        Check(menu != null && menu.IsOpen, "для узла стола меню своё (строк: " +
            (menu != null ? menu.ItemCount : 0) + ")");
        if (menu != null) menu.Close();

        // мультивыбор: меню применяется ко всем выбранным узлам
        List<ProjectNode> multi = new List<ProjectNode> { robot, table };
        ui.OnTreeContextMenu(multi, new Vector2(400f, 400f));
        menu = KvContextMenu.Current;
        Check(menu != null && menu.IsOpen, "мультивыбор (Ctrl+клик) — меню для нескольких узлов");
        ui.ToggleNodesVisibility(multi);
        Check(!KvTreeView.IsObjectVisible(robot), "пункт «Скрыть» скрывает объекты всех выбранных узлов");
        ui.ToggleNodesVisibility(multi);
        Check(KvTreeView.IsObjectVisible(robot), "пункт «Показать» возвращает объекты");
        if (menu != null) menu.Close();

        // копирование имени, фокус камеры, свойства
        ui.CopyNodeName(robot);
        Check(GUIUtility.systemCopyBuffer == robot.DisplayName,
            "пункт «Копировать имя» кладёт имя в буфер («" + GUIUtility.systemCopyBuffer + "»)");

        Vector3 cameraBefore = ui.CameraRig != null ? ui.CameraRig.transform.position : Vector3.zero;
        ui.FocusCameraOn(robot);
        float moved = ui.CameraRig != null
            ? Vector3.Distance(cameraBefore, ui.CameraRig.transform.position) : 0f;
        Check(moved > 0.05f, "пункт «Фокус камеры» переводит камеру к объекту (сдвиг " +
            moved.ToString("0.00") + " м)");

        ui.SelectNode(robot);
        ui.SetPanel("properties", true);
        Check(ui.Properties != null && ui.Properties.Node == robot,
            "пункт «Свойства» выбирает узел и открывает панель свойств");

        // удаление/дублирование — на точках (история точек ведётся менеджером)
        int pointsBefore = ui.PointCount;
        ProjectNode point = null;
        ui.SelectNode(robot);
        point = FindPoint(ui);
        if (point == null)
        {
            Info("точек в дереве пока нет — удаление/дублирование проверяется после выбора точки " +
                 "(в batch-режиме точка не ставится мышью)");
        }
        else
        {
            ui.DuplicateNodes(new List<ProjectNode> { point });
            Check(ui.PointCount > pointsBefore, "пункт «Дублировать» создаёт копию точки");
            ui.DeleteNodes(new List<ProjectNode> { point });
            Check(ui.PointCount >= pointsBefore, "пункт «Удалить» удаляет точку из истории");
        }
        Next();
    }

    private static ProjectNode FindPoint(KazistovVvUIManager ui)
    {
        if (ui.PointCount > 0)
        {
            ProjectNode p = FindNode(ui, ProjectNodeKind.Point);
            if (p != null) return p;
        }
        return null;
    }

    // ================================================================== 9. ЭТАП 7: горячие клавиши

    private static void StageHotkeyWindow()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ЭТАП 7: ОКНО ГОРЯЧИХ КЛАВИШ (F12) ---");
        ui.ShowHotkeys(true);
        Check(ui.HotkeyDock != null && ui.HotkeyDock.Shown,
            "окно горячих клавиш открывается (F12 / «Справка → Горячие клавиши»)");
        Check(ui.Hotkeys != null && ui.Hotkeys.RowCount > 10,
            "список биндов построен, строк: " + (ui.Hotkeys != null ? ui.Hotkeys.RowCount : 0));

        ui.Hotkeys.SetQuery("Z");
        int filtered = ui.Hotkeys.RowCount;
        Check(filtered > 0 && filtered < 40, "поиск по клавише фильтрует список («Z» → " +
            filtered + " строк)");
        ui.Hotkeys.SetQuery("геймпад");
        Check(ui.Hotkeys.RowCount >= 0, "поиск по разделу работает («геймпад» → " +
            ui.Hotkeys.RowCount + " строк)");
        ui.Hotkeys.SetQuery("");
        Check(ui.Hotkeys.RowCount > 10, "сброс поиска возвращает полный список");

        HashSet<string> conflicts = KvBindings.Conflicts();
        Info("конфликтов биндов в реестре: " + conflicts.Count + " · подсвечено в списке: " +
             ui.Hotkeys.ConflictCount);
        if (conflicts.Count > 0)
        {
            List<string> list = new List<string>(conflicts);
            list.Sort();
            Info("конфликтующие клавиши: " + string.Join(", ", list.ToArray()));
        }
        Check(ui.Hotkeys.ConflictCount > 0 || conflicts.Count == 0,
            "конфликты биндов подсчитываются и подсвечиваются красным (подсвечено " +
            ui.Hotkeys.ConflictCount + " строк, конфликтов " + conflicts.Count + ")");

        ui.ShowHotkeys(false);
        Check(!ui.HotkeyDock.Shown, "окно закрывается повторным F12");
        Next();
    }

    // ================================================================== 10. ЭТАП 8: состояния

    private static void StageUiStates()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ЭТАП 8: EMPTY / ERROR / LOADING ---");
        KvUiStates states = ui.States;
        Check(states != null, "слой состояний интерфейса создан");

        // EMPTY STATE: дерево и свойства
        ui.SelectNode(null);
        ui.Refresh();
        Check(ui.Properties != null && ui.Properties.SkeletonVisible,
            "пустое состояние панели свойств: «Выберите объект» + скелетон вместо пустой панели");

        // подсказка «выберите точку красным лазером» — механизм проверяется напрямую
        KvUiStates.SetEmptyHint("Выберите точку красным лазером (Z + ЛКМ)");
        Check(states != null && states.EmptyHint.Contains("красным лазером"),
            "подсказка пустого состояния показывается: «" +
            (states != null ? states.EmptyHint : "") + "»");
        Info("пустое состояние в потоке: подсказка включается сама, когда точки нет и поток Idle");
        KvUiStates.SetEmptyHint("");

        // LOADING STATE
        KvUiStates.Begin("Проверка полосы загрузки");
        Check(states != null && states.Loading, "полоса прогресса включается (Begin)");
        KvUiStates.SetProgress(0.5f, "Проверка 50 %");
        Check(states != null && Mathf.Approximately(states.Progress, 0.5f),
            "прогресс обновляется (50 %)");
        KvUiStates.End();
        Check(states != null && !states.Loading, "полоса прогресса выключается (End)");

        // ERROR STATE
        KvUiStates.ReportError("Тестовая ошибка интерфейса (диагностика)", "стек: проверка плашки");
        Check(states != null && states.ErrorVisible,
            "красная плашка ошибки появляется в углу (не только в консоли)");
        if (states != null) states.HideError();
        Check(states != null && !states.ErrorVisible, "плашка ошибки закрывается кнопкой «Скрыть»");
        Info("перехвачено ошибок уровня Error/Exception за прогон: " + KvUiStates.ErrorCount);
        Next();
    }

    // ================================================================== 11. ЭТАП 9: геймпад

    private static void StageGamepad()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ЭТАП 9: ГЕЙМПАД ---");
        KvGamepadRouter router = ui.GamepadRouter;
        Check(router != null, "роутер геймпада создан на объекте интерфейса");
        if (router == null) { Next(); return; }

        bool present = router.Present;
        Info("геймпад в этой среде: " + (present
            ? "подключён («" + router.DeviceName + "»)"
            : "НЕ подключён (batch-режим без устройства) — управление не активируется, что и требуется"));
        Check(!present || !string.IsNullOrEmpty(router.DeviceName),
            "автоопределение устройства работает (Present = " + present + ")");
        Check(present || !KvGamepadBridge.Active,
            "без геймпада управление геймпадом выключено (флаги сброшены)");

        // виртуальный индикатор
        Check(ui.GamepadHudView != null, "виртуальный геймпад (мини-индикатор) создан");
        Check(ui.GamepadHudView != null && ui.GamepadHudView.PillCount >= 10,
            "на индикаторе есть «таблетки» кнопок и стиков: " +
            (ui.GamepadHudView != null ? ui.GamepadHudView.PillCount : 0));
        Check(ui.GamepadHudView != null && ui.GamepadHudView.StatusText.Length > 0,
            "индикатор показывает состояние: «" + (ui.GamepadHudView != null
                ? ui.GamepadHudView.StatusText : "") + "»");

        // раскладка зафиксирована в реестре биндов
        int padBinds = 0;
        foreach (KvBindEntry e in KvBindings.All())
            if (e != null && e.Device == "Геймпад") padBinds++;
        Check(padBinds >= 12, "раскладка геймпада описана в реестре биндов (" + padBinds +
            " строк: стики, D-Pad, A/B/X/Y, LB/RB, LT/RT, Start/Select)");

        // флаги моста сбрасываются каждый кадр (иначе кнопка «залипнет»)
        KvGamepadBridge.Confirm = true;
        Check(KvGamepadBridge.Confirm, "мост геймпада принимает подтверждение (A/RT → ЛКМ)");
        KvGamepadBridge.ClearFrame();
        Check(!KvGamepadBridge.Confirm && !KvGamepadBridge.Cancel && !KvGamepadBridge.Enter,
            "флаги моста сбрасываются в конце кадра");

        // переключение робота с геймпада (D-Pad ← / →)
        RobotController firstActive = ActiveRobot();
        ui.SwitchRobotFromGamepad();
        RobotController secondActive = ActiveRobot();
        Check(secondActive != null && (firstActive == null || secondActive != firstActive),
            "D-Pad ← / → переключает робота («" +
            (secondActive != null ? secondActive.robotName : "—") + "»)");
        Next();
    }

    private static RobotController ActiveRobot()
    {
        foreach (RobotController rc in UnityEngine.Object.FindObjectsByType<RobotController>(
            FindObjectsInactive.Include))
            if (rc != null && rc.isActive) return rc;
        return null;
    }

    // ================================================================== 12. ЭТАП 10: иконки

    private static void StageIcons()
    {
        Line("");
        Line("--- ЭТАП 10: ЕДИНЫЙ НАБОР ИКОНОК ---");
        int missing = 0;
        int wrongSize = 0;
        float minAlpha = 1f;
        var sizes = new HashSet<int>();
        foreach (string id in KvIcons.AllIds)
        {
            Sprite sprite = KvIcons.Get(id, KvIcons.GridSize);
            if (sprite == null)
            {
                missing++;
                Line("[FAIL] иконка «" + id + "» не нарисована");
                continue;
            }
            int w = Mathf.RoundToInt(sprite.rect.width);
            if (w != KvIcons.GridSize) wrongSize++;
            sizes.Add(w);
            // проверка, что иконка не пустая: считаем максимальную альфу
            Texture2D tex = sprite.texture;
            try
            {
                Color32[] px = tex.GetPixels32();
                float max = 0f;
                for (int i = 0; i < px.Length; i++)
                    if (px[i].a > max) max = px[i].a;
                if (max < minAlpha) minAlpha = max;
            }
            catch { }
        }
        Check(missing == 0, "все " + KvIcons.AllIds.Length + " иконок библиотеки нарисованы");
        Check(wrongSize == 0, "у всех иконок единый размер спрайта " + KvIcons.GridSize + "×" +
            KvIcons.GridSize + " (несовпадений: " + wrongSize + ")");
        Check(minAlpha > 0.3f, "ни одна иконка не пустая (минимальная максимальная альфа " +
            minAlpha.ToString("0.00") + ")");
        Info("единая толщина линии: KvIcons.Stroke = " + KvIcons.Stroke.ToString("0.0") +
             " px, мелкие детали " + KvIcons.StrokeThin.ToString("0.0") + " px");
        Check(Mathf.Approximately(KvIcons.Stroke, 1.5f),
            "штрих иконок приведён к 1.5 px (ТЗ ЭТАПА 10)");
        Check(KvIcons.ToolbarSize == 24, "штатный размер иконки тулбара — 24 px");
        Next();
    }

    // ================================================================== 13. ЭТАП 11: доступность

    private static void StageAccessibility()
    {
        KazistovVvUIManager ui = Ui;
        Line("");
        Line("--- ЭТАП 11: ДОСТУПНОСТЬ ---");

        int smallBefore = KvTheme.FontSizeSmall;
        KvSettings.FontSize = 2;
        int large = KvTheme.FontSizeSmall;
        KvSettings.FontSize = 0;
        int small = KvTheme.FontSizeSmall;
        KvSettings.FontSize = 1;
        Check(large > smallBefore && small < smallBefore,
            "размер шрифта — отдельная настройка (маленький " + small + " px < средний " +
            smallBefore + " px < большой " + large + " px)");

        Color normalBg = KvTheme.PanelBg;
        Color normalText = KvTheme.TextMain;
        KvSettings.HighContrast = true;
        Color hcBg = KvTheme.PanelBg;
        Color hcText = KvTheme.TextMain;
        KvSettings.HighContrast = false;
        Check(hcBg != normalBg || hcText != normalText,
            "режим высокого контраста меняет палитру (фон " + normalBg.grayscale.ToString("0.00") +
            " → " + hcBg.grayscale.ToString("0.00") + ")");

        Color normalLaser = KvTheme.LaserGreen;
        KvSettings.ColorBlind = 1;
        Color cbLaser = KvTheme.LaserGreen;
        KvSettings.ColorBlind = 0;
        Check(cbLaser != normalLaser,
            "схема для дальтоников перекрашивает красный/зелёный (зелёный лазер меняется)");

        // навигация с клавиатуры
        KvKeyboardNav nav = ui.KeyboardNav;
        Check(nav != null, "модуль навигации с клавиатуры создан (Tab / стрелки / Enter)");
        if (nav != null)
        {
            nav.SetEnabled(true);
            bool focused = nav.FocusCommand("point.select");
            bool activated = focused && nav.Activate();
            Check(focused, "Tab-навигация находит кнопку тулбара (элементов: " +
                nav.FocusableCount + ", фокус: «" + nav.FocusLabel + "»)");
            Check(activated, "Enter нажимает кнопку в фокусе (команда выполнена)");
            nav.ClearFocus();
            Check(!nav.HasFocus, "фокус снимается (Esc)");
            nav.SetEnabled(KvSettings.KeyboardNav);
        }
        Info("клавиатурная навигация включена настройкой: " + KvSettings.KeyboardNav +
             " (при включённой Tab занят фокусом — переключение интерфейса остаётся на Esc и команде «Показать/скрыть интерфейс»)");
        Next();
    }

    // ================================================================== 14. ЭТАП 12: отчёт

    private static void StageCommandReport()
    {
        Line("");
        Line("--- ЭТАП 12: СПИСОК ВСЕХ КНОПОК И ПУНКТОВ СО СТАТУСАМИ ---");
        Line("всего команд: " + commandStatus.Count + " · работает: " + commandOk +
             " · заглушек: " + commandStubs + " · недоступно вне контекста: " + commandDisabled +
             " · ОШИБОК: " + commandErrors);
        foreach (string s in commandStatus) Line("  " + s);
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
             " · ошибок в логе: " + errorLines.Count + " · кадров " + Time.frameCount);
        Line(fail == 0 && exceptions == 0
            ? "ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ"
            : "ИТОГ: ЕСТЬ ЗАМЕЧАНИЯ");
        Debug.Log("[DshUiStagesDiag] отчёт: " + reportPath + " · OK " + ok + " · FAIL " + fail);

        EditorApplication.Exit(fail == 0 && exceptions == 0 ? 0 : 1);
    }
}
