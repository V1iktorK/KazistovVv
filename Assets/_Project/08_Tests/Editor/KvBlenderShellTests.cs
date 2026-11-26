// ================================================================================================
//  KvBlenderShellTests.cs — EditMode-тесты РАСКЛАДКИ BLENDER (утверждённый дизайн: «Blender-
//  раскладка + стеклянные окна», тёмная тема основная).
//
//  ОТКУДА ЗАДАЧА. Владелец утвердил дизайн интерфейса и потребовал перенести функционал
//  оверлея: каркас окна (кант + меню Файл/Правка/Вид/Робот/Сценарии/Сервис/Справка), тулбар
//  8 групп с 126 НАСТОЯЩИМИ иконками/командами, доки с автоскрытием у края, стеклянные
//  панели, тёмную/светлую тему. Старая раскладка остаётся: переключатель «Раскладка:
//  Классика/Blender» (команда в группе ui), существующие элементы не тронуты.
//
//  ЧЕМ ПРОВЕРЯЕТСЯ (P4-3). ТОЛЬКО поведением живого кода: кнопки нажимаются их же onClick,
//  наведение доков считается через RectContainsScreenPoint тем же методом, что и в рантайме,
//  команды судятся по их же Execute (шпион подменяет обработчик, id — нет). Тексты исходников
//  файлы тестов НЕ читают (храповик P4-3: порог 56). Сцены не открывают, в сеть не ходят.
//
//  ЧИСТКА. TearDown возвращает ПУСТУЮ сцену и глобальное состояние: реестр команд
//  (снимок + перерегистрация), тема, язык, преференсы (раскладка, крупные иконки, избранное)
//  восстанавливаются даже при упавшем тесте. Оболочка разбирается всегда.
// ================================================================================================

using System;
using System.Collections.Generic;
using KazistovVvFeatures;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvTests
{
    /// <summary>Раскладка Blender: каркас, тулбар (126 команд), доки, стеклянные панели, тема.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvBlenderShellTests
    {
        // ------------------------------------------------------------------ инфраструктура

        /// <summary>Снимок реестра команд до теста (возвращается в TearDown).</summary>
        private static List<KvCommand> registrySnapshot;

        /// <summary>Снимки глобальных настроек (возвращаются в TearDown).</summary>
        private static bool themeHad;
        private static int themeWas;
        private static bool layoutHad;
        private static int layoutWas;
        private static bool largeHad;
        private static int largeWas;
        private static bool favoritesHad;
        private static string favoritesWas;

        /// <summary>Шпион нажатий: какой id реально выполнился (а не «просто кнопка есть»).</summary>
        private static string lastInvokedId;
        private static int invokeCount;

        [SetUp]
        public void SetUp()
        {
            lastInvokedId = "";
            invokeCount = 0;

            themeHad = PlayerPrefs.HasKey(KvTheme.PrefsThemeKey);
            themeWas = PlayerPrefs.GetInt(KvTheme.PrefsThemeKey, 0);
            layoutHad = PlayerPrefs.HasKey(KvLayoutMode.PrefsKey);
            layoutWas = PlayerPrefs.GetInt(KvLayoutMode.PrefsKey, 0);
            largeHad = PlayerPrefs.HasKey(KvBlenderToolbar.LargeIconsPrefKey);
            largeWas = PlayerPrefs.GetInt(KvBlenderToolbar.LargeIconsPrefKey, 0);
            favoritesHad = PlayerPrefs.HasKey(KvFavoritesStore.PrefsKey);
            favoritesWas = PlayerPrefs.GetString(KvFavoritesStore.PrefsKey, "");

            registrySnapshot = new List<KvCommand>(KvCommands.All);
            KvCommands.Clear();

            // Шпионы на ВСЕ команды групп тулбара (126 id, включая robot.model.*):
            // кнопка обязана вызвать СВОЮ команду — шпион пишет id, который дошёл до Execute.
            RegisterSpiesForToolbarGroups();
        }

        [TearDown]
        public void TearDown()
        {
            KvBlenderShell.TearDown();

            // Чужие корни сцены, созданные оболочкой (EventSystem), снимаются всегда.
            foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                if (go == null) continue;
                if (go.name == "KvBlenderEventSystem" || go.name == "KvBlenderShellCanvas" ||
                    go.name.StartsWith("TestHost"))
                    UnityEngine.Object.DestroyImmediate(go);
            }

            KvCommands.Clear();
            if (registrySnapshot != null)
                foreach (KvCommand command in registrySnapshot) KvCommands.Register(command);

            RestorePref(KvTheme.PrefsThemeKey, themeHad, themeWas);
            RestorePref(KvLayoutMode.PrefsKey, layoutHad, layoutWas);
            RestorePref(KvBlenderToolbar.LargeIconsPrefKey, largeHad, largeWas);
            if (favoritesHad) PlayerPrefs.SetString(KvFavoritesStore.PrefsKey, favoritesWas);
            else PlayerPrefs.DeleteKey(KvFavoritesStore.PrefsKey);
            PlayerPrefs.Save();
            KvFavoritesStore.ResetCache();
            KvGlassStyle.ClearTracking();
            KvBlenderToasts.Clear();
            KvTaskQueue.Clear();
        }

        private static void RestorePref(string key, bool had, int value)
        {
            if (had) PlayerPrefs.SetInt(key, value);
            else PlayerPrefs.DeleteKey(key);
        }

        private static void RegisterSpiesForToolbarGroups()
        {
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
                foreach (string id in group.Commands) RegisterSpy(id);
        }

        /// <summary>
        /// Зарегистрировать команду-шпион под тем же id. `Id` передаётся ПЕРЕМЕННОЙ:
        /// разборы исходников ищут литералы `Id = "…"`, и тестовые шпионы не должны
        /// выглядеть как команды продукта.
        /// </summary>
        private static void RegisterSpy(string id)
        {
            string captured = id;
            KvCommands.Register(new KvCommand
            {
                Id = captured,
                Title = "spy " + captured,
                Execute = delegate
                {
                    lastInvokedId = captured;
                    invokeCount++;
                }
            });
        }

        private static KvBlenderShell BuildShell()
        {
            KvBlenderShell shell = KvBlenderShell.Ensure();
            Canvas.ForceUpdateCanvases();
            return shell;
        }

        private static void Click(Button button)
        {
            Assert.IsNotNull(button, "кнопка не создана");
            button.onClick.Invoke();
        }

        /// <summary>Экранная точка в центре прямоугольника (та же математика, что у наведения).</summary>
        private static Vector2 CenterOf(RectTransform rt)
        {
            Vector3 world = rt.TransformPoint(new Vector3(rt.rect.center.x, rt.rect.center.y, 0f));
            return RectTransformUtility.WorldToScreenPoint(null, world);
        }

        // ------------------------------------------------------------------ 1. КАРКАС

        [Test]
        public void Shell_TitleBar_UsesApprovedTitle()
        {
            KvBlenderShell shell = BuildShell();
            Assert.AreEqual(
                KvLoc.T("blender.shell.title", "KazistovVv · Тренажёр операторов"),
                shell.Title, "заголовок канта — «KazistovVv · Тренажёр операторов»");
            StringAssert.Contains("KazistovVv", shell.Title);
        }

        [Test]
        public void Shell_MenuStrip_SevenMenus_EachBoundToRealMenuCommands()
        {
            // Реальные меню-команды: по одной на каждое верхнее меню (как в классике MenuPath).
            string file = "spy.file"; RegisterMenuSpy(file, "Файл/Тест-пункт");
            string edit = "spy.edit"; RegisterMenuSpy(edit, "Правка/Тест-пункт");
            string view = "spy.view"; RegisterMenuSpy(view, "Вид/Тест-пункт");
            string robot = "spy.robot"; RegisterMenuSpy(robot, "Робот/Тест-пункт");
            string scen = "spy.scenarios"; RegisterMenuSpy(scen, "Сервис/Сценарии/Тест-пункт");
            string service = "spy.service"; RegisterMenuSpy(service, "Сервис/Тест-пункт");
            string help = "spy.help"; RegisterMenuSpy(help, "Справка/Тест-пункт");

            KvBlenderShell shell = BuildShell();
            KvBlenderMenuStrip strip = shell.MenuStrip;
            Assert.IsNotNull(strip, "строка меню не построена");

            CollectionAssert.AreEqual(
                new[] { "Файл", "Правка", "Вид", "Робот", "Сценарии", "Сервис", "Справка" },
                strip.MenuTitles, "меню каркаса: Файл/Правка/Вид/Робот/Сценарии/Сервис/Справка");

            // Каждое меню привязано к реестру: id — реальные команды, «Сценарии» собирают
            // сценарные команды из путей «Сервис/Сценарии…».
            CollectionAssert.Contains(strip.CommandIdsFor("Файл"), file);
            CollectionAssert.Contains(strip.CommandIdsFor("Правка"), edit);
            CollectionAssert.Contains(strip.CommandIdsFor("Вид"), view);
            CollectionAssert.Contains(strip.CommandIdsFor("Робот"), robot);
            CollectionAssert.Contains(strip.CommandIdsFor("Сценарии"), scen);
            CollectionAssert.Contains(strip.CommandIdsFor("Сервис"), service);
            CollectionAssert.Contains(strip.CommandIdsFor("Справка"), help);
            CollectionAssert.DoesNotContain(strip.CommandIdsFor("Сценарии"), service);

            foreach (string menu in strip.MenuTitles)
                foreach (string id in strip.CommandIdsFor(menu))
                    Assert.IsNotNull(KvCommands.Get(id), "меню ссылается на незарегистрированную команду " + id);
        }

        [Test]
        public void Shell_MenuStrip_Click_InvokesItsOwnCommand()
        {
            string file = "spy.file"; RegisterMenuSpy(file, "Файл/Тест-пункт");
            KvBlenderShell shell = BuildShell();
            KvBlenderMenuStrip strip = shell.MenuStrip;

            strip.OpenMenu("Файл");
            Assert.IsTrue(strip.AnyOpen, "меню не открылось");
            GameObject row = GameObject.Find("MM_" + file);
            Assert.IsNotNull(row, "пункт меню не построен");
            Click(row.GetComponent<Button>());
            Assert.AreEqual(file, lastInvokedId, "пункт меню вызвал не свою команду");
        }

        [Test]
        public void Shell_WindowButtons_OnPanels_MinimizeMaximizeClose()
        {
            KvBlenderShell shell = BuildShell();
            KvGlassWindow window = shell.FindWindow("zones");
            Assert.IsNotNull(window, "панель «Зоны» не построена");
            Assert.IsNotNull(window.MinimizeButton);
            Assert.IsNotNull(window.MaximizeButton);
            Assert.IsNotNull(window.CloseButton);

            Click(window.MinimizeButton);
            Assert.IsTrue(window.Minimized, "свернуть не сработало");
            Click(window.MaximizeButton);
            Assert.IsTrue(window.Maximized, "развернуть не сработало");
            Click(window.MaximizeButton);
            Assert.IsFalse(window.Maximized, "возврат размера не сработал");
            Click(window.CloseButton);
            Assert.IsFalse(window.Visible, "крестик не скрыл окно");
            window.Show();
            Assert.IsTrue(window.Visible, "окно не показалось снова");
        }

        // ------------------------------------------------------------------ 2. ТУЛБАР

        [Test]
        public void Toolbar_126Buttons_EachInvokesItsOwnCommand()
        {
            KvBlenderShell shell = BuildShell();
            KvBlenderToolbar toolbar = shell.Toolbar;
            Assert.IsNotNull(toolbar, "тулбар не построен");

            int expected = 0;
            foreach (KvToolbarGroup group in KvToolbarGroups.All) expected += group.Commands.Length;
            int spriteBacked = 0;
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
                foreach (string id in group.Commands)
                    if (KvSpriteIcons.Has(id)) spriteBacked++;
            Assert.AreEqual(126, spriteBacked,
                "126 кнопок тулбара берут спрайты владельца (126 файлов в Resources/UI/Icons/Toolbar)");
            Assert.AreEqual(expected, toolbar.ButtonIds.Count, "тулбар построил не все кнопки");

            foreach (string id in toolbar.ButtonIds)
            {
                Button button = toolbar.ButtonFor(id);
                Assert.IsNotNull(button, "нет кнопки для " + id);
                lastInvokedId = "";
                Click(button);
                Assert.AreEqual(id, lastInvokedId, "кнопка " + id + " вызвала не свою команду");
            }
        }

        [Test]
        public void Toolbar_OrderFollowsGroups_ThorFirst_SeparatorsBetweenGroups()
        {
            KvBlenderShell shell = BuildShell();
            KvBlenderToolbar toolbar = shell.Toolbar;

            // Порядок кнопок — строго как в KvToolbarGroups; THOR — первый среди моделей.
            int index = 0;
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
            {
                for (int i = 0; i < group.Commands.Length; i++)
                {
                    Assert.AreEqual(group.Commands[i], toolbar.ButtonIds[index],
                        "порядок кнопок разошёлся с каталогом групп");
                    index++;
                }
            }
            string firstModel = "";
            foreach (string id in toolbar.ButtonIds)
                if (id.StartsWith("robot.model."))
                {
                    firstModel = id;
                    break;
                }
            Assert.AreEqual("robot.model.thor", firstModel, "THOR должен идти первым среди моделей роботов");

            Assert.AreEqual(toolbar.GroupCount - 1, toolbar.SeparatorCount,
                "разделители — между группами");
        }

        [Test]
        public void Toolbar_Sprites_LoadFromResourcesFolder()
        {
            int withSprite = 0;
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
                foreach (string id in group.Commands)
                    if (KvSpriteIcons.Has(id)) withSprite++;
            Assert.AreEqual(126, withSprite,
                "все 126 кнопок тулбара берут НАСТОЯЩИЕ спрайты из Resources/UI/Icons/Toolbar");
        }

        [Test]
        public void Toolbar_LargeIconMode_DoublesTheTarget()
        {
            KvBlenderShell shell = BuildShell();
            KvBlenderToolbar toolbar = shell.Toolbar;
            Assert.IsFalse(toolbar.LargeIcons, "по умолчанию обычные иконки");
            Assert.AreEqual(KvBlenderToolbar.RegularIconSize, toolbar.IconSize);
            Button button = toolbar.ButtonFor("robot.playpause");
            float regular = ((RectTransform)button.transform).rect.width;

            toolbar.LargeIcons = true;
            Assert.AreEqual(KvBlenderToolbar.LargeIconSize, toolbar.IconSize);
            button = toolbar.ButtonFor("robot.playpause");
            float large = ((RectTransform)button.transform).rect.width;
            Assert.Greater(large, regular, "крупный режим не увеличил цель нажатия");

            // Команда реестра делает то же самое (она в группе ui и зовётся из палитры).
            KvBlenderCommands.Register();
            KvCommands.Invoke(KvBlenderCommands.LargeIconCommandId);
            Assert.IsFalse(toolbar.LargeIcons, "команда крупных иконок не переключила режим");
        }

        // ------------------------------------------------------------------ 3. ДОКИ

        [Test]
        public void Dock_HoverAtEdge_SlidesOut_PointerLeaves_Hides()
        {
            KvBlenderShell shell = BuildShell();
            KvAutoHideDock dock = shell.Docks[0];
            Assert.IsNotNull(dock, "доки не построены");
            Assert.IsFalse(dock.IsOpen, "док стартует убранным");

            // Наведение НА ПОЛОСУ У КРАЯ — той же проверкой, что в рантайме.
            dock.EvaluatePointer(CenterOf(dock.Strip));
            Assert.IsTrue(dock.IsOpen, "наведение на край не выдвинуло док");

            // Курсор внутри выдвинутого дока — док остаётся.
            dock.EvaluatePointer(CenterOf(dock.Panel));
            Assert.IsTrue(dock.IsOpen, "док закрылся при курсоре внутри");

            // Курсор в центре экрана (вне дока) — док убирается.
            dock.EvaluatePointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Assert.IsFalse(dock.IsOpen, "док не убрался при уходе курсора");
        }

        [Test]
        public void Dock_Windows_SnapMagneticallyToScreenEdges()
        {
            KvBlenderShell shell = BuildShell();
            KvGlassWindow window = shell.FindWindow("commands");
            Assert.IsNotNull(window, "плавающее окно не построено");

            KvBlenderWindowDrag drag = window.Drag;
            drag.BeginDragAt(CenterOf(window.Root));
            // Тянем к ЛЕВОМУ краю канваса ближе дистанции магнита.
            Vector2 leftEdge = RectTransformUtility.WorldToScreenPoint(null,
                shell.CanvasRect.TransformPoint(new Vector3(
                    shell.CanvasRect.rect.xMin + 4f, shell.CanvasRect.rect.center.y, 0f)));
            drag.DragTo(leftEdge);
            drag.EndDrag();

            Assert.AreEqual("left", drag.LastSnapEdge, "окно не защёлкнулось у левого края");
            Assert.AreEqual(0f, window.Root.anchoredPosition.x, 0.5f,
                "снап не прижал окно к левому краю (anchoredPosition.x должен быть 0)");
        }

        // ------------------------------------------------------------------ 4. ПАНЕЛИ

        [Test]
        public void Panels_AllThirteen_ExistAsGlassWindows()
        {
            KvBlenderShell shell = BuildShell();
            string[] ids =
            {
                "theme", "zones", "hotkeys", "gestures", "commands",
                "joints", "toasts", "queue", "minimap", "cameragizmo",
                "timeline", "favorites", "console"
            };
            foreach (string id in ids)
            {
                KvGlassWindow window = shell.FindWindow(id);
                Assert.IsNotNull(window, "панель «" + id + "» не построена");
                Assert.IsTrue(window.Visible, "панель «" + id + "» не видна");
                Assert.IsNotNull(window.TitleText, "у панели «" + id + "» нет заголовка");
            }
            Assert.IsNotNull(shell.StatusBar, "статус-строка не построена");
        }

        [Test]
        public void Panels_ZonesAndGestures_RowsCallRealCommands()
        {
            KvBlenderShell shell = BuildShell();
            KvGlassWindow zones = shell.FindWindow("zones");
            KvGlassWindow gestures = shell.FindWindow("gestures");

            foreach (string id in KvBlenderPanels.ZoneCommandIds)
            {
                // Строки живут внутри дока (панель может быть убрана) — ищем в окне, не в сцене.
                Transform row = zones.Content.Find("Row_" + id);
                Assert.IsNotNull(row, "строка панели зон не построена: " + id);
                lastInvokedId = "";
                Click(row.GetComponent<Button>());
                Assert.AreEqual(id, lastInvokedId, "строка зон вызвала не свою команду: " + id);
            }
            foreach (string id in KvBlenderPanels.GestureCommandIds)
            {
                Transform row = gestures.Content.Find("Row_" + id);
                Assert.IsNotNull(row, "строка панели жестов не построена: " + id);
                lastInvokedId = "";
                Click(row.GetComponent<Button>());
                Assert.AreEqual(id, lastInvokedId, "строка жестов вызвала не свою команду: " + id);
            }
        }

        [Test]
        public void Panels_Joints_ShowHonestTelemetryState()
        {
            KvBlenderShell shell = BuildShell();
            shell.RefreshAll();
            KvGlassWindow window = shell.FindWindow("joints");
            Assert.IsNotNull(window);
            // В EditMode робота нет — панель обязана честно сказать «нет данных», а не молчать.
            string honestText = KvLoc.T("blender.joints.nodata", "нет данных: робот не выбран");
            bool honest = false;
            foreach (Text text in window.Content.GetComponentsInChildren<Text>(true))
                if (text.text != null && text.text.Contains(honestText)) honest = true;
            Assert.IsTrue(honest, "панель суставов не назвала причину «нет данных»");
        }

        [Test]
        public void Panels_Console_ReflectsJournalWithSeverityColors()
        {
            KvBlenderShell shell = BuildShell();
            KvActionLog log = KvActionLog.Instance;
            bool repeatsWereOn = log.suppressRepeats;
            log.suppressRepeats = false;
            try
            {
                log.Add(KvLogKind.Error, "тестовая ошибка консоли");
                log.Add(KvLogKind.Warning, "тестовое предупреждение консоли");
                shell.RefreshAll();

                KvGlassWindow window = shell.FindWindow("console");
                Text[] rows = window.Content.GetComponentsInChildren<Text>(true);
                bool sawError = false;
                foreach (Text row in rows)
                {
                    if (row.text == null || !row.text.Contains("тестовая ошибка")) continue;
                    sawError = true;
                    Assert.AreEqual(KvTheme.Error, row.color, "ошибка в консоли подсвечена не красным");
                }
                Assert.IsTrue(sawError, "консоль не показала запись журнала");
            }
            finally
            {
                log.suppressRepeats = repeatsWereOn;
            }
        }

        [Test]
        public void Panels_Favorites_PinAndInvoke()
        {
            KvBlenderShell shell = BuildShell();
            KvFavoritesStore.Add("robot.playpause");
            shell.RefreshAll();

            KvGlassWindow window = shell.FindWindow("favorites");
            bool found = false;
            foreach (Button row in window.Content.GetComponentsInChildren<Button>(true))
            {
                if (row.gameObject.name != "Row_robot.playpause") continue;
                if (!row.gameObject.activeSelf) continue;
                found = true;
                lastInvokedId = "";
                Click(row);
                Assert.AreEqual("robot.playpause", lastInvokedId, "избранное вызвало не свою команду");
            }
            Assert.IsTrue(found, "закреплённая команда не показана в избранном");

            KvFavoritesStore.Remove("robot.playpause");
            Assert.IsFalse(KvFavoritesStore.Contains("robot.playpause"), "команда не открепилась");
        }

        [Test]
        public void Panels_Toasts_FedByReportMechanism()
        {
            KvBlenderShell shell = BuildShell();
            KvBlenderToasts.Clear();
            KvUiStates.ReportError("тестовый отчёт механизма Report", "");
            shell.RefreshAll();

            Assert.Greater(KvBlenderToasts.History.Count, 0, "Report не дошёл до тостов");
            KvGlassWindow window = shell.FindWindow("toasts");
            bool shown = false;
            foreach (Text text in window.Content.GetComponentsInChildren<Text>(true))
                if (text.text != null && text.text.Contains("тестовый отчёт")) shown = true;
            Assert.IsTrue(shown, "панель тостов не показала отчёт Report");
        }

        // ------------------------------------------------------------------ 5. ТЕМА

        [Test]
        public void Theme_Switch_ReallyChangesThemeAndGlass()
        {
            KvBlenderShell shell = BuildShell();
            KvTheme.SetMode(KvThemeMode.Dark, false);
            shell.RefreshAll();
            Color darkFill = KvGlassStyle.FillColor;

            KvGlassWindow window = shell.FindWindow("theme");
            Assert.IsNotNull(window, "панель темы не построена");

            // Кнопка «Светлая» переключает тему по-настоящему.
            Button light = FindTextButton(window, KvLoc.T("blender.theme.light", "Светлая"));
            Assert.IsNotNull(light, "кнопка «Светлая» не найдена");
            Click(light);
            Assert.IsTrue(KvTheme.IsLight, "переключатель не включил светлую тему");
            Assert.AreNotEqual(darkFill, KvGlassStyle.FillColor, "стекло не перекрасилось");

            Button dark = FindTextButton(window, KvLoc.T("blender.theme.dark", "Тёмная"));
            Click(dark);
            Assert.IsFalse(KvTheme.IsLight, "переключатель не вернул тёмную тему");
        }

        [Test]
        public void Theme_GlassUsesSemiTransparentFill()
        {
            Color fill = KvGlassStyle.FillColor;
            Assert.Less(fill.a, 1f, "стекло должно быть полупрозрачным");
            Assert.Greater(fill.a, 0.5f, "стекло слишком прозрачное — текст не прочитается");
            Assert.IsNotNull(KvGlassStyle.Rounded, "скруглённая рамка не построена");
        }

        // ------------------------------------------------------------------ 6. РАСКЛАДКА

        [Test]
        public void LayoutSwitch_Command_TogglesBlenderAndBack_ClassicKeepsElements()
        {
            KvBlenderCommands.Register();
            Assert.IsFalse(KvLayoutMode.IsBlender, "по умолчанию остаётся Классика");

            KvCommands.Invoke(KvLayoutMode.CommandId);
            Assert.IsTrue(KvLayoutMode.IsBlender, "команда не включила раскладку Blender");
            Assert.IsNotNull(KvBlenderShell.Instance, "оболочка Blender не построена");

            KvCommands.Invoke(KvLayoutMode.CommandId);
            Assert.IsFalse(KvLayoutMode.IsBlender, "команда не вернула классику");
            Assert.IsNull(KvBlenderShell.Instance, "оболочка Blender не разобрана");
        }

        [Test]
        public void LayoutSwitch_LargeIconsCommand_IsInUiGroup()
        {
            // Команды раскладки обязаны лежать в группе ui (правило: реестр ⊆ меню).
            KvToolbarGroup ui = KvToolbarGroups.Find("ui");
            Assert.IsNotNull(ui);
            CollectionAssert.Contains(ui.Commands, KvLayoutMode.CommandId);
            CollectionAssert.Contains(ui.Commands, KvBlenderCommands.LargeIconCommandId);
        }

        // ------------------------------------------------------------------ 7. ЛОКАЛИЗАЦИЯ

        [Test]
        public void Localization_NewLabels_ExistInAllSevenDictionaries()
        {
            string[] languages = { "ru", "en", "zh", "es", "de", "fr", "ja" };
            string[] keys =
            {
                "blender.shell.title", "cmd.ui.layout.blender", "cmd.ui.layout.blender.desc",
                "cmd.ui.toolbar.large", "cmd.ui.toolbar.large.desc"
            };
            foreach (string language in languages)
            {
                List<string> known = KvLoc.KeysOf(language);
                Assert.Greater(known.Count, 50, "словарь " + language + " не загружен");
                foreach (string key in keys)
                    Assert.IsTrue(known.Contains(key),
                        "ключ «" + key + "» отсутствует в словаре " + language);
            }
        }

        // ------------------------------------------------------------------ помощники

        private static void RegisterMenuSpy(string id, string menuPath)
        {
            string captured = id;
            KvCommands.Register(new KvCommand
            {
                Id = captured,
                Title = "spy " + captured,
                MenuPath = menuPath,
                Execute = delegate
                {
                    lastInvokedId = captured;
                    invokeCount++;
                }
            });
        }

        private static Button FindTextButton(KvGlassWindow window, string label)
        {
            foreach (Text text in window.Content.GetComponentsInChildren<Text>(true))
                if (text.text == label && text.gameObject.name == "Label")
                {
                    // Родитель ищется НАПРЯМУЮ (`GetComponent`), а не `GetComponentInParent`:
                    // последний ПРОПУСКАЕТ неактивные объекты, а окна живут в закрытом доке
                    // (панель неактивна по дизайну) — кнопка существует, но поиск её не видел.
                    Button button = text.transform.parent != null
                        ? text.transform.parent.GetComponent<Button>()
                        : null;
                    if (button != null) return button;
                }
            return null;
        }
    }
}
