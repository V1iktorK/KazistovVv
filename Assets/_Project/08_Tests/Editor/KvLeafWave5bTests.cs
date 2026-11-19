// ================================================================================================
//  KvLeafWave5bTests.cs — EditMode-тесты ВОЛНЫ 5 КАМПАНИИ «каждый листочек путеводителя закрыт»
//  (часть 2: веер шлема, строки стартового меню/экрана настроек и панель планшета).
//
//  ОТКУДА ЗАДАЧА. `Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv` — 213 листьев «не проверено». Эта часть закрывает:
//    • ВЕЕР (слайды 116–135, 31 лист) — иконки команд в `KvXrGroupPanel`: каждая иконка
//      ведёт РОВНО на свою команду реестра;
//    • ДЕРЕВО A/D (слайды 99/105, 2 листа) — строка «Добавить SCARA» стартового меню и
//      переключатель «Переключить» экрана настроек с тремя ветками (4/5/6);
//    • AR-ПАНЕЛЬ (слайды 141/153/155/159/161/165/166/167, 19 листьев) — вкладки групп,
//      кнопки «Закрыть», окно подтверждения «ОТМЕНА» и строки команд вкладок.
//
//  ЧЕМ ПРОВЕРЯЕТСЯ (P4-3). ТОЛЬКО ПОВЕДЕНИЕМ живого кода: панели собираются штатными
//  `Rebuild()`/`EnsureBuilt()`, кнопки нажимаются ИХ ЖЕ `onClick`, состояние читается
//  геттерами самого интерфейса, журнал — из `KvActionLog.Entries`, консоль — из
//  `Application.logMessageReceived` (веер пишет отказы именно в консоль). Тексты исходников
//  файлы тестов НЕ читают. Сцены не открываются, игра не запускается, устройства и сеть не
//  трогаются, APK не собирается.
//
//  ЧИСКА. `TearDown` возвращает пустую сцену и прежние глобальные настройки (язык, режим
//  VR/MR, второй луч, рука луча), даже когда тест упал.
// ================================================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using KazistovVvAr;
using KazistovVvFeatures;
using KazistovVvUI;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KazistovVvTests
{
    /// <summary>Волна 5 (часть 2): веер шлема, стартовое меню, экран настроек и панель планшета.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvLeafWave5bTests
    {
        // ------------------------------------------------------------------ инфраструктура

        /// <summary>Хосты, созданные тестом: снимаются в TearDown независимо от исхода.</summary>
        private readonly List<GameObject> hosts = new List<GameObject>();

        /// <summary>Корни сцены ДО теста: команды создают объекты, они обязаны исчезнуть в TearDown.</summary>
        private readonly List<GameObject> rootsBefore = new List<GameObject>();

        private bool repeatsWereOn;
        private bool ignoreLogsWas;
        private static List<KvCommand> registrySnapshot;
        private string localeWas;
        private bool secondaryRayWas;
        private KvXrQuickHand rayHandWas;
        private int modeWas;

        [SetUp]
        public void SetUp()
        {
            repeatsWereOn = KvActionLog.Instance.suppressRepeats;
            KvActionLog.Instance.suppressRepeats = false;
            KvActionLog.Instance.Clear();
            ignoreLogsWas = LogAssert.ignoreFailingMessages;

            if (registrySnapshot == null)
            {
                registrySnapshot = new List<KvCommand>();
                foreach (KvCommand command in KvCommands.All) registrySnapshot.Add(command);
            }

            localeWas = KvLoc.CurrentCode;
            if (KvLoc.CurrentCode != "ru") KvLoc.SetLanguage("ru");

            secondaryRayWas = KvXrSettings.GetSecondaryRay();
            rayHandWas = KvXrSettings.GetRayHand();
            modeWas = KvXrSettings.GetModeIndex();

            rootsBefore.Clear();
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                rootsBefore.Add(root);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject host in hosts)
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            hosts.Clear();

            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootsBefore.Contains(root)) continue;
                if (root == null) continue;
                if (root.name.StartsWith("[", StringComparison.Ordinal)) continue;
                UnityEngine.Object.DestroyImmediate(root);
            }

            foreach (string name in new[] { "ArUiCanvas", "ArEventSystem" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);
            }

            // Настройки шлема — обратно, как были (переключатели пишут в PlayerPrefs).
            // Подавление: смена режима будит панель, а её пересборка зовёт `Destroy` детей —
            // в EditMode это ошибка лога, не про поведение листа.
            Quietly(delegate
            {
                if (KvXrSettings.GetSecondaryRay() != secondaryRayWas)
                    KvXrSettings.SetSecondaryRay(secondaryRayWas);
                if (KvXrSettings.GetRayHand() != rayHandWas) KvXrSettings.SetRayHand(rayHandWas);
                if (KvXrSettings.GetModeIndex() != modeWas) KvXrSettings.SetModeIndex(modeWas);
                if (KvLoc.CurrentCode != localeWas) KvLoc.SetLanguage(localeWas);
            });
            KvActionLog.Instance.suppressRepeats = repeatsWereOn;
            LogAssert.ignoreFailingMessages = ignoreLogsWas;

            if (registrySnapshot != null)
            {
                KvCommands.Clear();
                foreach (KvCommand command in registrySnapshot) KvCommands.Register(command);
                registrySnapshot = null;
            }
        }

        private GameObject NewHost(string name)
        {
            var go = new GameObject("KvLeafWave5b_" + name);
            hosts.Add(go);
            return go;
        }

        /// <summary>ШТАТНАЯ РЕГИСТРАЦИЯ КОМАНД ОБОЛОЧКИ + ХАБЫ (приём волны 3, `Awake` в EditMode нет).</summary>
        private static void RegisterAllCommands(GameObject host)
        {
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            MethodInfo register = typeof(KazistovVvUIManager).GetMethod("RegisterCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(register,
                "у KazistovVvUIManager больше нет RegisterCommands() — штатная регистрация недоступна");
            try { register.Invoke(ui, null); }
            catch (TargetInvocationException error)
            {
                Assert.Fail("штатная регистрация команд упала: " + error.InnerException);
            }

            // Службы живут в `Awake`, а в EditMode его не бывает: хабы поднимаются явно
            // (те же, что в волнах 3/4 — без `KvStageHub4`, чей Awake заводит сеть).
            WakeUp<KazistovVvFeatures.FeatureHub>(host, "FeatureHub");
            WakeUp<KazistovVvFeatures.KvStageHub>(host, "KvStageHub");
            WakeUp<KazistovVvFeatures.KvStageHub3>(host, "KvStageHub3");
        }

        private static void WakeUp<T>(GameObject host, string label) where T : Component
        {
            T hub = host.GetComponent<T>();
            if (hub == null) return;
            MethodInfo awake = typeof(T).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (awake == null) return;
            try { awake.Invoke(hub, null); }
            catch (TargetInvocationException error)
            {
                Assert.Fail("Awake " + label + " упал: " + error.InnerException);
            }
        }

        /// <summary>ПОСТРОЕННЫЙ AR-ИНТЕРФЕЙС (в EditMode `Start` не вызывается — строим сами).</summary>
        private KvArUi BuildUi()
        {
            KvArUi ui = NewHost("ui").AddComponent<KvArUi>();
            ui.EnsureBuilt();
            Assert.IsTrue(ui.Built, "интерфейс планшета не построился — проверять нечего");
            return ui;
        }

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

        private static Transform FindChild(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        private static void Quietly(Action action)
        {
            bool was = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try { action(); }
            finally { LogAssert.ignoreFailingMessages = was; }
        }

        private static List<string> LogSince(int from)
        {
            var lines = new List<string>();
            IReadOnlyList<KvLogEntry> entries = KvActionLog.Instance.Entries;
            for (int i = from; i < entries.Count; i++) lines.Add(entries[i].text);
            return lines;
        }

        private static int LogCount() { return KvActionLog.Instance.Entries.Count; }

        private static bool AnyContains(List<string> lines, string needle)
        {
            foreach (string line in lines)
                if (line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>ПОЛЕ КОМПОНЕНТА (в т.ч. private).</summary>
        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "у " + target.GetType().Name + " нет поля " + name);
            field.SetValue(target, value);
        }

        /// <summary>СТРОКА ПАНЕЛИ КОМАНД `Cmd_&lt;id&gt;` (с листанием страниц до неё — приём волны 3).</summary>
        private static Button FindCommandRow(KvArUi ui, string groupId, string id)
        {
            ui.OpenCommands(groupId, false);
            for (int page = 0; page < 40; page++)
            {
                Button row = ui.UiButton("PcCommandPanel/CommandRows/Cmd_" + id);
                if (row != null) return row;
                Button next = ui.UiButton("PcCommandPanel/CommandRows/CmdNext");
                if (next == null) return null;
                next.onClick.Invoke();
            }
            return null;
        }

        // ==================== 1. ВЕЕР ШЛЕМА: 31 ЛИСТ (СЛАЙДЫ 116–135)

        /// <summary>Листья веера: id команды из пути `Веер / <группа> / <id>`.</summary>
        private static readonly string[] FanLeaves =
        {
            "robot.model.kuka_iiwa7", "robot.model.kuka_iiwa14", "robot.model.abb_irb6640",
            "robot.model.fanuc_m20ib", "robot.model.franka_fr3", "robot.model.franka_fr3_v2",
            "robot.model.kinova_gen3", "robot.model.comau_edo", "robot.model.openarm",
            "waypoint.clear", "wp.orient", "wp.speed", "wp.detour", "wp.pause", "wp.clear",
            "tool.measure", "tool.measure.reset", "tool.grid", "tool.grid.clear",
            "obstacle.discard", "tool.flashlight", "view.panel.tree", "view.panel.properties",
            "view.panel.settings", "ui.toggle", "shot.annotate", "export.lang", "export.tab",
            "import.scan", "file.import", "ui.access"
        };

        /// <summary>
        /// ЛИСТЫ 116–135 «Веер / &lt;группа&gt; / &lt;id&gt;» (31 лист): иконка команды есть в веере
        /// СВОЕЙ группы (в т.ч. на нужной странице) и её нажатие доходит до команды. Судится по
        /// строке журнала `нажатие: &lt;id&gt;` (доступная команда) либо по строке консоли самой
        /// панели: «команда недоступна (заглушка): &lt;id&gt;» (условие не выполнено) или
        /// «ОКНО: команда &lt;id&gt;» (панель открывает окно в мире вместо выполнения — §42).
        /// Тихого «нажал — тишина» нет ни у одной иконки.
        /// </summary>
        [Test]
        public void W5_Fan_EveryLeafIcon_RoutesToItsOwnCommand()
        {
            RegisterAllCommands(NewHost("fanreg"));

            GameObject host = NewHost("fan");
            KvXrGroupPanel panel = host.AddComponent<KvXrGroupPanel>();
            panel.logOnBuild = false;
            panel.followHand = false;
            Quietly(() => panel.Rebuild());
            Assert.AreEqual(KvXrPanelLevel.Groups, panel.Level,
                "панель не на уровне иконок групп — веер не проверить");

            var failures = new List<string>();
            int proved = 0;

            foreach (string id in FanLeaves)
            {
                KvCommand command = KvCommands.Get(id);
                if (command == null)
                {
                    failures.Add(id + ": команды нет в реестре — иконки в веере не будет");
                    continue;
                }

                int groupIndex = -1;
                KvToolbarGroup[] groups = KvToolbarGroups.All;
                for (int i = 0; i < groups.Length && groupIndex < 0; i++)
                    foreach (string commandId in groups[i].Commands)
                        if (commandId == id) { groupIndex = i; break; }
                if (groupIndex < 0)
                {
                    failures.Add(id + ": команда не разложена ни в одну группу веера");
                    continue;
                }

                Quietly(() => panel.OpenGroup(groupIndex));
                Assert.AreEqual(KvXrPanelLevel.GroupFan, panel.Level,
                    id + ": веер группы «" + groups[groupIndex].Title + "» не раскрылся");

                Transform icon = FindFanIcon(panel, id);
                if (icon == null)
                {
                    failures.Add(id + ": иконки Cmd_" + id + " нет в веере (ни на одной странице)");
                    continue;
                }
                Button button = icon.GetComponent<Button>();
                if (button == null)
                {
                    failures.Add(id + ": иконка Cmd_" + id + " не кнопка");
                    continue;
                }

                // ПЕРЕХВАТ КОНСОЛИ: веер пишет отказы и открытие окон именно в консоль.
                var console = new List<string>();
                Application.LogCallback capture = delegate(string condition, string stackTrace,
                    LogType type) { console.Add(condition); };
                int before = LogCount();
                try
                {
                    Application.logMessageReceived += capture;
                    Quietly(() => button.onClick.Invoke());
                }
                catch (Exception error)
                {
                    failures.Add(id + ": нажатие иконки упало: " + error.Message);
                    continue;
                }
                finally
                {
                    Application.logMessageReceived -= capture;
                }

                List<string> lines = LogSince(before);
                bool journal = AnyContains(lines, "нажатие: " + id);
                bool consoleHit = false;
                foreach (string line in console)
                    if (line.IndexOf(id, StringComparison.Ordinal) >= 0) { consoleHit = true; break; }

                bool windowCommand = false;
                try { windowCommand = KazistovVvXr.KvXrDesktopWindows.IsWindowCommand(id); }
                catch (Exception) { windowCommand = false; }

                if (command.Enabled && !windowCommand)
                {
                    if (!journal)
                        failures.Add(id + ": иконка не дошла до команды: журнал «" +
                                     (lines.Count > 0 ? string.Join(" | ", lines.ToArray()) : "пуст") +
                                     "» · консоль «" + (console.Count > 0 ? string.Join(" | ", console.ToArray()) : "пуста") + "»");
                    else proved++;
                }
                else
                {
                    // Недоступна либо открывает окно: отказ/окно обязано быть НАЗВАНЫМ.
                    if (!consoleHit && !journal)
                        failures.Add(id + ": ни журнал, ни консоль не называют команду после нажатия");
                    else proved++;
                }
            }

            Assert.IsTrue(failures.Count == 0,
                "иконки веера не все ведут к своим командам (" + failures.Count + " шт):\n" +
                string.Join("\n", failures.ToArray()));
            Assert.AreEqual(FanLeaves.Length, proved, "проверено не все листья веера");
        }

        /// <summary>ИКОНКА КОМАНДЫ В ВЕЕРЕ — по всем страницам текущей группы.</summary>
        private static Transform FindFanIcon(KvXrGroupPanel panel, string id)
        {
            for (int page = 0; page < Mathf.Max(1, panel.FanPageCount); page++)
            {
                Transform icon = FindDeep(panel.transform, "Cmd_" + id);
                if (icon != null && icon.gameObject.activeInHierarchy) return icon;
                if (page + 1 < panel.FanPageCount) Quietly(() => panel.FanTurnPage(1));
            }
            return null;
        }

        // ==================== 2. ДЕРЕВО A: СТРОКА «Добавить SCARA» (СЛАЙД 99, 1 ЛИСТ)

        /// <summary>
        /// ЛИСТ 99 «Дерево A / MenuRow_2 «Добавить SCARA»»: в разделе «Робот» стартового меню
        /// при одном роботе в сцене СТРОКА С ТАКОЙ ПОДПИСЬЮ стоит под номером 2 (0 — «Робот: 1»,
        /// 1 — «Добавить робота THOR»), а её нажатие идёт в `CreateStartRobot(scara)` — то есть
        /// ставит (или заменяет на) SCARA, а не молчит. Если фабрика не может создать робота,
        /// она обязана назвать причину в консоли/журнале — тихого выхода не бывает.
        /// </summary>
        [Test]
        public void W5_StartMenuRow2_IsAddScara_AndPresses()
        {
            RegisterAllCommands(NewHost("startreg"));

            // КАМЕРА НУЖНА ФАБРИКЕ (место спавна — перед оператором).
            GameObject cameraHost = NewHost("headcamera");
            cameraHost.tag = "MainCamera";
            cameraHost.AddComponent<Camera>();

            GameObject panelHost = NewHost("startmenu");
            KvXrGroupPanel panel = panelHost.AddComponent<KvXrGroupPanel>();
            panel.logOnBuild = false;
            panel.followHand = false;
            panel.headCamera = cameraHost.GetComponent<Camera>();

            // РОБОТ В СЦЕНЕ ОБЯЗАН БЫТЬ: раздел «Робот» показывает существующие роботы, а под
            // ними — строки создания. Считает их САМА панель (`startRobots` — тот же список, по
            // которому строятся строки): если роботов нет, ставим своего.
            Quietly(() => panel.Rebuild());
            Quietly(panel.OpenStartMenu);
            panel.SelectStartSection((int)KvXrStartSection.Robots);
            int existing = StartRobots(panel);
            if (existing == 0)
            {
                GameObject robotHost = NewHost("robot");
                robotHost.AddComponent<RobotController>();
                RegisteredObject marker = robotHost.AddComponent<RegisteredObject>();
                marker.DisplayName = "Робот: тест волны 5";
                Quietly(() => panel.Rebuild());
                Quietly(panel.OpenStartMenu);
                panel.SelectStartSection((int)KvXrStartSection.Robots);
                existing = StartRobots(panel);
            }
            Assert.Greater(existing, 0,
                "в сцене нет ни одного робота — раздел «Робот» не на чем проверить");

            // ПОДПИСЬ СТРОКИ ЕСТЬ НА ПАНЕЛИ (цели луча включают и иконки разделов).
            int target = FindStartTarget(panel, "Добавить SCARA");
            Assert.GreaterOrEqual(target, 0,
                "в разделе «Робот» нет строки «Добавить SCARA» — лист 99 без строки");
            Assert.AreEqual("Добавить SCARA", panel.TitleAt(target),
                "цель с подписью «Добавить SCARA» подписана иначе");

            // НОМЕР СТРОКИ СЧИТАЕТ ТОТ ЖЕ КОД, ЧТО СТРОИТ РАЗДЕЛ: `PressStartRow` берёт
            // действие строки из `KvXrStartMenuRows.ActionFor` — им и пользуемся, а не
            // смещением от иконок (в целях луча есть и заголовок раздела).
            int row = -1;
            for (int r = 0; r < 10; r++)
                if (KvXrStartMenuRows.ActionFor(KvXrStartSection.Robots, r, 0, existing) ==
                    KvXrStartRowAction.CreateScaraRobot) { row = r; break; }
            Assert.GreaterOrEqual(row, 0, "ActionFor не нашёл строки создания SCARA");

            // ПОРЯДОК РАЗДЕЛА (`KvXrStartMenuRows.Robots`): существующие роботы (не больше
            // четырёх), затем «Добавить робота THOR», «Добавить SCARA», «Сменить робота»,
            // «Переместить робота». При ОДНОМ роботе — состояние листа — это и есть строка 2.
            int shown = Mathf.Min(existing, KvXrStartMenuRows.MaxExistingRows);
            Assert.AreEqual(shown + 1, row,
                "«Добавить SCARA» стоит под номером " + row + ", а по составу раздела " +
                "ожидается " + (shown + 1) + " (роботов в сцене: " + existing + ")");
            if (existing == 1)
                Assert.AreEqual(2, row,
                    "при одном роботе «Добавить SCARA» обязана быть строкой 2 (MenuRow_2)");
            Assert.AreEqual("Добавить SCARA", panel.TitleAt(target),
                "строка под номером " + row + " подписана не так, как обещает лист");

            int scaraBefore = CountScara();

            // НАЖАТИЕ: путь строки — CreateStartRobot(scara).
            var console = new List<string>();
            Application.LogCallback capture = delegate(string condition, string stackTrace,
                LogType type) { console.Add(condition); };
            int before = LogCount();
            try
            {
                Application.logMessageReceived += capture;
                Quietly(() => panel.PressStartRow(row));
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            bool created = CountScara() > scaraBefore;

            List<string> lines = LogSince(before);
            if (!created)
            {
                // Фабрика отказалась — отказ обязан быть назван (иначе «нажал и ничего»).
                bool named = AnyContains(lines, "SCARA") || AnyContains(lines, "создан") ||
                             AnyContains(lines, "не созд");
                foreach (string line in console)
                    if (line.IndexOf("SCARA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        line.IndexOf("не создан", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        line.IndexOf("создан", StringComparison.OrdinalIgnoreCase) >= 0) named = true;
                Assert.IsTrue(named,
                    "строка «Добавить SCARA» не создала робота и не назвала причину: журнал «" +
                    (lines.Count > 0 ? string.Join(" | ", lines.ToArray()) : "пуст") +
                    "» · консоль «" + (console.Count > 0 ? string.Join(" | ", console.ToArray()) : "пуста") + "»");
                Assert.Inconclusive("SCARA не создана в EditMode, но причина названа честно — " +
                                    "см. консоль/журнал выше");
            }

            Assert.IsTrue(created, "после нажатия строки SCARA не появилась в сцене");
            Assert.IsTrue(AnyContains(lines, "создан") ||
                          console.Exists(l => l.IndexOf("создан", StringComparison.OrdinalIgnoreCase) >= 0),
                "создание SCARA не оставило строки о факте: " + string.Join(" | ", lines.ToArray()));
        }

        /// <summary>СКОЛЬКО РОБОТОВ ВИДИТ ПАНЕЛЬ (список, по которому строятся строки раздела).</summary>
        private static int StartRobots(KvXrGroupPanel panel)
        {
            object value = GetField(panel, "startRobots");
            Assert.IsNotNull(value, "у панели нет поля startRobots — состав раздела не проверить");
            return ((System.Collections.ICollection)value).Count;
        }

        /// <summary>НОМЕР ЦЕЛИ ЛУЧА ПО ТОЧНОЙ ПОДПИСИ (−1 — нет).</summary>
        private static int FindStartTarget(KvXrGroupPanel panel, string title)
        {
            for (int i = 0; i < panel.RayTargetCount; i++)
                if (panel.TitleAt(i) == title) return i;
            return -1;
        }

        /// <summary>СКОЛЬКО SCARA В СЦЕНЕ СЕЙЧАС.</summary>
        private static int CountScara()
        {
            return UnityEngine.Object.FindObjectsByType<SCARAController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        // ==================== 3. ДЕРЕВО D: ПЕРЕКЛЮЧАТЕЛЬ «Переключить» (СЛАЙД 105, 1 ЛИСТ)

        /// <summary>
        /// ЛИСТ 105 «Дерево D / Set_Toggle «Переключить»»: кнопка экрана настроек переключает
        /// ТОЛЬКО свою ветку — строка 4 → второй луч, 5 → рука луча, 6 → режим VR/MR, иначе
        /// `LogWarning «ветка не назначена»`. Каждая ветка проверяется на ФАКТЕ настройки
        /// (значение меняется и возвращается обратно).
        /// </summary>
        [Test]
        public void W5_SettingsScreenToggle_SwitchesOnlyItsOwnBranch()
        {
            GameObject host = NewHost("settings");
            KvXrGroupPanel panel = host.AddComponent<KvXrGroupPanel>();
            panel.logOnBuild = false;
            panel.followHand = false;
            Quietly(() => panel.Rebuild());

            // --- СТРОКА 4: ВТОРОЙ ЛУЧ
            SetField(panel, "settingsIndex", 4);
            Quietly(() => panel.OpenScreen(KvXrPanelLevel.SettingsScreen));
            Transform toggle = FindDeep(panel.transform, "Set_Toggle");
            Assert.IsNotNull(toggle, "на экране настроек (строка 5) нет кнопки «Переключить»");
            Button button = toggle.GetComponent<Button>();
            Assert.IsNotNull(button, "«Переключить» не кнопка");

            bool secondaryRay = KvXrSettings.GetSecondaryRay();
            KvXrQuickHand hand = KvXrSettings.GetRayHand();
            int mode = KvXrSettings.GetModeIndex();

            Quietly(() => button.onClick.Invoke());
            Assert.AreNotEqual(secondaryRay, KvXrSettings.GetSecondaryRay(),
                "строка 4 не переключила второй луч — ветка case 4 не работает");
            Assert.AreEqual(hand, KvXrSettings.GetRayHand(),
                "строка 4 зацепила руку луча — переключатель ушёл не в свою ветку");
            Assert.AreEqual(mode, KvXrSettings.GetModeIndex(),
                "строка 4 зацепила режим VR/MR — переключатель ушёл не в свою ветку");

            // --- СТРОКА 5: РУКА ЛУЧА
            SetField(panel, "settingsIndex", 5);
            Quietly(() => panel.OpenScreen(KvXrPanelLevel.SettingsScreen));
            toggle = FindDeep(panel.transform, "Set_Toggle");
            Assert.IsNotNull(toggle, "на экране настроек (строка 6) нет кнопки «Переключить»");
            button = toggle.GetComponent<Button>();
            Assert.IsNotNull(button, "«Переключить» не кнопка");
            secondaryRay = KvXrSettings.GetSecondaryRay();

            Quietly(() => button.onClick.Invoke());
            Assert.AreNotEqual(hand, KvXrSettings.GetRayHand(),
                "строка 5 не переключила руку луча — ветка case 5 не работает");
            Assert.AreEqual(secondaryRay, KvXrSettings.GetSecondaryRay(),
                "строка 5 зацепила второй луч — переключатель ушёл не в свою ветку");
            Assert.AreEqual(mode, KvXrSettings.GetModeIndex(),
                "строка 5 зацепила режим VR/MR — переключатель ушёл не в свою ветку");

            // --- СТРОКА 6: РЕЖИМ VR/MR
            SetField(panel, "settingsIndex", 6);
            Quietly(() => panel.OpenScreen(KvXrPanelLevel.SettingsScreen));
            toggle = FindDeep(panel.transform, "Set_Toggle");
            Assert.IsNotNull(toggle, "на экране настроек (строка 7) нет кнопки «Переключить»");
            button = toggle.GetComponent<Button>();
            Assert.IsNotNull(button, "«Переключить» не кнопка");
            hand = KvXrSettings.GetRayHand();

            Quietly(() => button.onClick.Invoke());
            Assert.AreNotEqual(mode, KvXrSettings.GetModeIndex(),
                "строка 6 не переключила режим VR/MR — ветка case 6 не работает");
            Assert.AreEqual(hand, KvXrSettings.GetRayHand(),
                "строка 6 зацепила руку луча — переключатель ушёл не в свою ветку");

            // --- СТРОКА БЕЗ ВЕТКИ (0): обязан следовать Warning «ветка не назначена», а не молчать.
            SetField(panel, "settingsIndex", 0);
            Quietly(() => panel.OpenScreen(KvXrPanelLevel.SettingsScreen));
            toggle = FindDeep(panel.transform, "Set_Toggle");
            Assert.IsNull(toggle,
                "на строке 0 (есть шаг −/+) кнопка «Переключить» — ветки переключателя там нет");
        }

        // ==================== 4. AR-ПАНЕЛЬ: ВКЛАДКИ ГРУПП И КНОПКИ «ЗАКРЫТЬ» (9 ЛИСТЬЕВ)

        /// <summary>Вкладки-листьи: id группы и обещанное листом число команд.</summary>
        private static readonly string[][] TabLeaves =
        {
            new[] { "path", "13" },
            new[] { "post", "17" },
            new[] { "view", "22" },
            new[] { "export", "17" },
            new[] { "net", "7" },
            new[] { "ui", "13" },
            new[] { "misc", "12" }
        };

        /// <summary>
        /// ЛИСТЫ 155 «ArUiCanvas / PcCommandPanel / GroupTab_&lt;группа&gt;» (7 листьев) и
        /// 141/155 «CloseCommands «Закрыть»» (2 листа).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Вкладка открывает ПАНЕЛЬ СВОЕЙ группы: заголовок называет группу и
        /// ЧИСЛО КОМАНД (не меньше того, что обещает лист — каталог вырос: `ar.place.marker`),
        /// и на месте СТРОКА каждой зарегистрированной команды (`Cmd_&lt;id&gt;`, с листанием
        /// страниц). Кнопка «Закрыть» гасит и панель команд, и выпадающее меню
        /// (`SetActive(false)` для обоих — ровно обещание листов 141/155).
        ///
        /// ГРАНИЦА ЧЕСТНОСТИ. Клаусула листа «повторное нажатие той же группы закрывает панель»
        /// в волне 1 СОЗНАТЕЛЬНО заменена: вкладки зовут `OpenCommands(id, false)` (закрытие
        /// оставлено кнопкам «Закрыть» и «Команды» — комментарий в `KvArUi.OpenCommands`),
        /// переключатель остался у `OpenCommands(id)` (без флага). Здесь проверяется ОБЕ
        /// половины: вкладка открывает, а сам метод — переключает.
        /// </summary>
        [Test]
        public void W5_ArPanel_GroupTabsOpenTheirGroup_AndCloseClosesBoth()
        {
            RegisterAllCommands(NewHost("arreg"));
            KvArUi ui = BuildUi();

            var failures = new List<string>();

            foreach (string[] leaf in TabLeaves)
            {
                string tab = leaf[0];
                int promised = int.Parse(leaf[1]);

                KvToolbarGroup group = KvToolbarGroups.Find(tab);
                Assert.IsNotNull(group, "группы «" + tab + "» нет в каталоге");
                int declared = group.Commands.Length;
                Assert.GreaterOrEqual(declared, promised,
                    "группа «" + tab + "» меньше, чем обещает лист (" + promised + ")");

                Button tabButton = ui.UiButton("PcCommandPanel/GroupTab_" + tab);
                if (tabButton == null)
                {
                    failures.Add(tab + ": вкладки GroupTab_" + tab + " нет в панели");
                    continue;
                }

                int before = LogCount();
                try { Quietly(() => tabButton.onClick.Invoke()); }
                catch (Exception error)
                {
                    failures.Add(tab + ": нажатие вкладки упало: " + error.Message);
                    continue;
                }

                if (GameObject.Find("PcCommandPanel") == null)
                {
                    failures.Add(tab + ": вкладка не открыла панель команд");
                    continue;
                }

                // ЗАГОЛОВОК: «Команды · <группа> (N)» — число из каталога, как в листе.
                Button anyRow = FindCommandRow(ui, tab, group.Commands[0]);
                if (anyRow == null && KvCommands.Get(group.Commands[0]) != null)
                {
                    failures.Add(tab + ": в панели нет ни одной строки команды группы");
                    continue;
                }

                foreach (string commandId in group.Commands)
                {
                    if (KvCommands.Get(commandId) == null) continue;
                    if (FindCommandRow(ui, tab, commandId) == null)
                        failures.Add(tab + ": строки Cmd_" + commandId + " нет в панели — " +
                                     "лист обещает строку на каждую команду");
                }
            }
            Assert.IsTrue(failures.Count == 0,
                "вкладки панели планшета не все открывают свои группы (" + failures.Count + " шт):\n" +
                string.Join("\n", failures.ToArray()));

            // --- САМ МЕТОД ПЕРЕКЛЮЧАЕТ (вторая половина обещания листа 155).
            ui.OpenCommands("net");
            Assert.IsTrue(GameObject.Find("PcCommandPanel") != null,
                "OpenCommands(\"net\") не открыла панель");
            ui.OpenCommands("net");               // повторное нажатие ТОГО ЖЕ вызова
            Assert.IsTrue(GameObject.Find("PcCommandPanel") == null,
                "повторный вызов OpenCommands той же группы не закрыл панель — " +
                "переключатель из листа 155 не работает");

            // --- «ЗАКРЫТЬ» (листы 141/155): гасит и панель команд, и выпадающее меню.
            ui.ToggleMenu("Файл");
            Assert.IsNotNull(GameObject.Find("PcMenuDropdown"),
                "меню «Файл» не открылось — закрывать нечего");
            ui.OpenCommands("robot");
            Assert.IsNotNull(GameObject.Find("PcCommandPanel"),
                "панель команд не открылась — закрывать нечего");

            Button close = ui.UiButton("PcCommandPanel/CloseCommands");
            Assert.IsNotNull(close, "кнопки «Закрыть» в панели команд нет — листы 141/155 без неё");
            Quietly(() => close.onClick.Invoke());
            Assert.IsNull(GameObject.Find("PcCommandPanel"),
                "«Закрыть» не погасила панель команд (SetActive(false) обещан листом)");
            Assert.IsNull(GameObject.Find("PcMenuDropdown"),
                "«Закрыть» не погасила выпадающее меню (SetActive(false) обещан листом 141)");
        }

        // ==================== 5. AR-СООБЩЕНИЕ: КНОПКА «ОТМЕНА» (СЛАЙД 153, 1 ЛИСТ)

        /// <summary>
        /// ЛИСТ 153 «ArUiCanvas / ArMessage / ArMessageCancel «ОТМЕНА»»: кнопка закрывает окно,
        /// ВЫПОЛНЯЕТ действие отмены (не только `HideMessage`) и обнуляет `messageCancelAction` —
        /// повторное нажатие после закрытия ничего не делает.
        /// </summary>
        [Test]
        public void W5_ArMessageCancel_RunsCancelActionAndCloses()
        {
            RegisterAllCommands(NewHost("msgreg"));
            KvArUi ui = BuildUi();

            bool confirmed = false;
            bool cancelled = false;
            ui.ShowConfirm("Очистить рабочее пространство?", "ОЧИСТИТЬ",
                delegate { confirmed = true; },
                delegate { cancelled = true; });
            Assert.IsTrue(ui.MessageVisible, "окно подтверждения не открылось");

            Transform cancel = null;
            // Кнопка лежит в окне сообщения; путь — как в листе (ArMessage/ArMessageCancel).
            Transform message = FindDeep(ui.transform, "ArMessage");
            Assert.IsNotNull(message, "окна ArMessage нет в интерфейсе — лист 153 без окна");
            cancel = FindChild(message, "ArMessageCancel");
            Assert.IsNotNull(cancel, "кнопки ArMessageCancel в окне нет — лист 153 без кнопки");
            Button button = cancel.GetComponent<Button>();
            Assert.IsNotNull(button, "ArMessageCancel не кнопка");

            Quietly(() => button.onClick.Invoke());
            Assert.IsFalse(ui.MessageVisible, "«ОТМЕНА» не закрыла окно сообщения");
            Assert.IsTrue(cancelled, "«ОТМЕНА» не выполнила действие отмены — только HideMessage");
            Assert.IsFalse(confirmed, "«ОТМЕНА» выполнила ДЕЙСТВИЕ ПОДТВЕРЖДЕНИЯ вместо отмены");

            // Поле обнулено: повторное нажатие по мёртвой кнопке ничего не запускает.
            cancelled = false;
            Quietly(() => button.onClick.Invoke());
            Assert.IsFalse(cancelled,
                "после закрытия действие отмены осталось присвоенным — окно закрылось, а отмена жива");

            // ДЕЙСТВИЕ ПОДТВЕРЖДЕНИЯ при этом не задето: окно открывается заново и работает.
            ui.ShowConfirm("Очистить?", "ОЧИСТИТЬ", delegate { confirmed = true; },
                delegate { cancelled = true; });
            confirmed = false;
            Button confirmButton = ui.UiButton("ArMessage/ArMessageButton");
            if (confirmButton == null)
            {
                Transform messageRoot = FindDeep(ui.transform, "ArMessage");
                Transform confirm = FindChild(messageRoot, "ArMessageButton");
                Assert.IsNotNull(confirm, "кнопки подтверждения в окне нет");
                confirmButton = confirm.GetComponent<Button>();
            }
            Assert.IsNotNull(confirmButton, "кнопка подтверждения не найдена");
            Quietly(() => confirmButton.onClick.Invoke());
            Assert.IsTrue(confirmed, "кнопка подтверждения не выполнила своё действие");
            Assert.IsFalse(ui.MessageVisible, "окно подтверждения не закрылось после «ОЧИСТИТЬ»");
        }

        // ==================== 6. AR-ПАНЕЛЬ: СТРОКИ КОМАНД ВКЛАДОК (9 ЛИСТЬЕВ)

        /// <summary>Строки-листьи: вкладка, id команды и причина/факт, который лист обещает в журнале.</summary>
        private static readonly string[][] RowLeaves =
        {
            new[] { "robot", "edit.reset", "" },
            new[] { "path", "waypoint.delete", "" },
            new[] { "view", "tool.measure.reset", "линейка: сброшена" },
            new[] { "export", "shot.screenshot", "" },
            new[] { "export", "video.toggle", "" },
            new[] { "export", "export.robot", "Нет траектории" },
            new[] { "export", "import.tab", "" },
            new[] { "export", "session.save", "сессия" },
            new[] { "export", "session.load", "" }
        };

        /// <summary>
        /// ЛИСТЫ 159/161/165/166/167 «ArUiCanvas / PcCommandPanel / CommandRows / вкладка …»
        /// (9 листьев): строка панели вызывает СВОЮ команду реестра (`нажатие: &lt;id&gt;`), а там,
        /// где лист называет конкретный факт («линейка: сброшена», «Нет траектории», «сессия»),
        /// — и сам факт в журнале. Строки ищутся с листанием страниц, как их строит
        /// `FillCommandRows` (по 8 строк на странице).
        /// </summary>
        [Test]
        public void W5_ArCommandRows_EveryLeafRow_RoutesToItsOwnCommand()
        {
            RegisterAllCommands(NewHost("arrows"));
            KvArUi ui = BuildUi();

            var failures = new List<string>();
            int proved = 0;

            foreach (string[] leaf in RowLeaves)
            {
                string tab = leaf[0];
                string id = leaf[1];
                string fact = leaf[2];

                KvCommand command = KvCommands.Get(id);
                if (command == null)
                {
                    failures.Add(id + " (вкладка " + tab + "): команды нет в реестре");
                    continue;
                }

                Button row = FindCommandRow(ui, tab, id);
                if (row == null)
                {
                    failures.Add(id + " (вкладка " + tab + "): строки Cmd_" + id + " нет в панели");
                    continue;
                }

                bool enabled = command.Enabled;
                int before = LogCount();
                try { Quietly(() => row.onClick.Invoke()); }
                catch (Exception error)
                {
                    failures.Add(id + " (вкладка " + tab + "): нажатие упало: " + error.Message);
                    continue;
                }

                List<string> lines = LogSince(before);
                string trace = lines.Count > 0 ? string.Join(" | ", lines.ToArray()) : "журнал пуст";
                if (!AnyContains(lines, "нажатие: " + id))
                {
                    failures.Add(id + " (вкладка " + tab + "): нажатие не дошло до команды: " + trace);
                    continue;
                }
                if (enabled)
                {
                    proved++;
                    if (fact.Length > 0 && !AnyContains(lines, fact))
                        failures.Add(id + " (вкладка " + tab + "): в журнале нет факта «" +
                                     fact + "» из листа: " + trace);
                }
                else
                {
                    proved++;
                    if (AnyContains(lines, "результат: выполнена"))
                        failures.Add(id + " (вкладка " + tab + "): недоступная строка выполнилась: " + trace);
                    else if (!AnyContains(lines, "недоступна"))
                        failures.Add(id + " (вкладка " + tab + "): отказ без причины: " + trace);
                }
            }

            Assert.IsTrue(failures.Count == 0,
                "строки панели планшета не все ведут к своим командам (" + failures.Count + " шт):\n" +
                string.Join("\n", failures.ToArray()));
            Assert.AreEqual(RowLeaves.Length, proved, "проверено не все листья строк панели");
        }
    }
}
