// ================================================================================================
//  KvLeafWave2bTests.cs — EditMode-тесты ВОЛНЫ 2b КАМПАНИИ «каждый листочек путеводителя закрыт».
//
//  ОТКУДА ЗАДАЧА. `Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv` — после волны 2 осталось 66 листьев со статусом
//  «по коду, есть дефект» (все — «Реестр KvCommands»). Волна 2b закрывает их той же тактикой:
//  массовая триажа → ОБЩИЕ КОРНИ → батчи правок с тестами.
//
//    КОРЕНЬ 1 — «отказ/состояние доходит только до консоли» (и та на устройстве выключена).
//      `KvActionLog` — единственный след, который оператор видит. Листья: estop, compare.mark,
//      wp.orient/speed/detour/pause/clear, waypoint.delete, waypoint.build, post.apply.*,
//      ui.palette, view.theme, ui.resetlayout, help.open/about, ui.access, light.next,
//      scara.recalibrate, graphics.detect/benchmark/reset, valid.mannequins, session.load,
//      file.export, export.lang, eta.toggle, presentation.toggle, collision.toggle, lab.run,
//      web/voice/hands/eyes.toggle, env.next, cine.toggle, fail.*, report.make, logtools.export.
//
//    КОРЕНЬ 2 — «нет `IsEnabled`/`IsChecked` на кнопке». Листья: valid.run (IsEnabled),
//      pendant.variant/attach/jogmode, lab.tab/lab.run, forces.tab, heat.time, report.pdf,
//      fail.tab, fail.comms/fail.joint, bt.tab.
//
//    КОРЕНЬ 3 — «дефекта нет, закрепить поведение» (листья с карточкой «Дефект: нет» или с
//      дефектом, закрытым волнами 1–2): joints.panel, gripper.toggle, waypoint.move,
//      waypoint.clear, graph.open, view.clearance, zone.discard, tool.measure.reset,
//      tool.grid.clear, import.scan, obstacle.evaluate и др.
//
//  ЧЕМ ПРОВЕРЯЕТСЯ (P4-3). ТОЛЬКО ПОВЕДЕНИЕМ живого кода: команды нажимаются штатным
//  `KvCommands.Invoke`, состояние читается геттерами `KvCommand.Checked`/`Enabled`, журнал —
//  из `KvActionLog.Entries`. Тексты исходников файлы тестов НЕ читают. Ни один тест не
//  открывает сцену, не запускает игру и не ходит в сеть.
//
//  ГРАНИЦА ЧЕСТНОСТИ. Устройства не трогаем и APK не собираем — листья закываются в EditMode,
//  на той же логике, что в приложении.
//
//  ЧИСТКА. `TearDown` возвращает пустую сцену: хосты, окно «Панель функций», пояс окон и
//  снимок реестра команд снимаются всегда, даже когда тест упал.
// ================================================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using KazistovVvDiagnostics;
using KazistovVvFeatures;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Волна 2b: листья «по коду, есть дефект» из Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvLeafWave2bTests
    {
        // ------------------------------------------------------------------ инфраструктура

        /// <summary>Хосты, созданные тестом: снимаются в TearDown независимо от исхода.</summary>
        private readonly List<GameObject> hosts = new List<GameObject>();

        /// <summary>
        /// БЫЛО ЛИ ОТСЕЧЕНИЕ ПОВТОРОВ ВКЛЮЧЕНО ДО ТЕСТА (возвращается в TearDown).
        /// НУЖНО ПОТОМУ, что ключ журнала вырезает числа и одинаковые события: два нажатия
        /// разных команд с ОДИНАКОВОЙ причиной — это одно событие, и второе в журнале не
        /// появится. Проверяемая правда была бы не поведением кода, а правилом журнала.
        /// </summary>
        private bool repeatsWereOn;

        /// <summary>
        /// СНИМОК РЕЕСТРА КОМАНД ДО ШТАТНОЙ РЕГИСТРАЦИИ. `KvCommands` — общий на весь прогон
        /// статик: наполнив его командами с обработчиками, тест обязан вернуть набор таким,
        /// каким нашёл (правило Wave1/KvGapClosureTests).
        /// </summary>
        private static List<KvCommand> registrySnapshot;

        [SetUp]
        public void SetUp()
        {
            repeatsWereOn = KvActionLog.Instance.suppressRepeats;
            KvActionLog.Instance.suppressRepeats = false;

            if (registrySnapshot == null)
            {
                registrySnapshot = new List<KvCommand>();
                foreach (KvCommand command in KvCommands.All) registrySnapshot.Add(command);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject host in hosts)
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            hosts.Clear();

            // Окно «Панель функций» при построении ложится на пояс окон: убитое окно не должно
            // пережить тест, иначе следующая фикстура увидит ссылку на уничтоженный объект.
            KazistovVvXr.KvWindowBelt.Unregister(KvFeatureWindow.WindowId);

            KvActionLog.Instance.suppressRepeats = repeatsWereOn;

            // Реестр команд возвращаем к снимку, снятому ДО штатной регистрации.
            if (registrySnapshot != null)
            {
                KvCommands.Clear();
                foreach (KvCommand command in registrySnapshot) KvCommands.Register(command);
                registrySnapshot = null;
            }
        }

        /// <summary>ХОСТ С КОМПОНЕНТОМ (имя — с префиксом, чтобы TearDown находил его).</summary>
        private GameObject NewHost(string name)
        {
            var host = new GameObject("KvLeafWave2b_" + name);
            hosts.Add(host);
            return host;
        }

        /// <summary>НОВЫЕ ЗАПИСИ ЖУРНАЛА ПОСЛЕ `from` (нумерация от начала журнала).</summary>
        private static List<string> LogSince(int from)
        {
            var lines = new List<string>();
            IReadOnlyList<KvLogEntry> entries = KvActionLog.Instance.Entries;
            for (int i = from; i < entries.Count; i++) lines.Add(entries[i].text);
            return lines;
        }

        private static int LogCount() { return KvActionLog.Instance.Entries.Count; }

        /// <summary>ЕСТЬ ЛИ СРЕДИ СТРОК ПОДСТРОКА (без учёта регистра).</summary>
        private static bool AnyContains(List<string> lines, string needle)
        {
            foreach (string line in lines)
                if (line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>СТРОКИ ЖУРНАЛА, ПОЯВИВШИЕСЯ ПОСЛЕ НАЖАТИЯ `id` (вместе с причиной отказа).</summary>
        private static List<string> InvokeAndReadLog(string id)
        {
            KvCommand command = KvCommands.Get(id);
            Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована — лист не о чём проверять");
            Assert.IsNotNull(command.Execute, "у «" + id + "» нет обработчика");
            int before = LogCount();
            KvCommands.Invoke(id);
            return LogSince(before);
        }

        /// <summary>
        /// ШТАТНАЯ РЕГИСТРАЦИЯ КОМАНД ОБОЛОЧКИ (приватный метод шелла — отражением,
        /// как в `KvGapClosureTests.RegisterAllCommands`).
        /// </summary>
        private static void RegisterManagerCommands(GameObject host)
        {
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            System.Reflection.MethodInfo register = typeof(KazistovVvUIManager).GetMethod(
                "RegisterCommands",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(register,
                "у KazistovVvUIManager больше нет RegisterCommands() — штатная регистрация недоступна");
            try
            {
                register.Invoke(ui, null);
            }
            catch (System.Reflection.TargetInvocationException error)
            {
                Assert.Fail("штатная регистрация команд упала: " + error.InnerException);
            }
        }

        /// <summary>УСТАНОВИТЬ СВОЙСТВО С ЧАСТНЫМ СЕТТЕРОМ (службы живут в `Awake`, его в EditMode нет).</summary>
        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, "у " + target.GetType().Name + " нет свойства " + name);
            property.SetValue(target, value, null);
        }

        /// <summary>
        /// СОСТОЯНИЕ ОКНА «ПАНЕЛЬ ФУНКЦИЙ» НАПРЯМУЮ (видимость + номер вкладки).
        ///
        /// ЗАЧЕМ НЕ `Show()`. В EditMode шелл не собирается: службы окна создаются в `Awake`,
        /// которого здесь не бывает, и сборка содержимой вкладки падает — окно показать целиком
        /// нельзя. А галке нужны ровно два факта, которые `Show`/`SetTab` пишут ДО сборки
        /// содержимого: `visible` и `tab` (их читают публичные `Visible`/`VisibleTab`).
        /// </summary>
        private static void SetWindowState(KvFeatureWindow window, bool visible, int tab)
        {
            Type type = typeof(KvFeatureWindow);
            type.GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(window, visible);
            type.GetField("tab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(window, tab);
        }

        // ==================== 1. КОРЕНЬ 1: ОТКАЗ И СОСТОЯНИЕ — В ЖУРНАЛЕ, А НЕ В КОНСОЛИ

        /// <summary>
        /// КОРЕНЬ 1 (хабы 1–2 и оболочка): отказы и переключения оставляют строку в `KvActionLog`.
        ///
        /// ОТКУДА ДЕФЕКТ. `estop` и `compare.mark` заканчивались `if (flow == null) return;`,
        /// `post.apply.smooth/topt/energy` — `if (служба == null) return;`, `ui.palette` —
        /// `if (палитра == null) return;`, а `view.theme`, `ui.resetlayout`, `help.open/about`,
        /// `ui.access` писали событие методом `KazistovVvUIManager.Log`, который пишет ТОЛЬКО в
        /// консоль и ТОЛЬКО при включённом `logUiEvents` (на устройстве он выключен). Волна 2
        /// этот же корень закрыла для `post.auto`, `lang.cycle`, `view.workspace` и др.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Каждое нажатие без службы/потока и каждое событие настройки оставляет
        /// в журнале названную причину или состояние — тихого выхода и «следа только в консоли»
        /// больше нет.
        /// </summary>
        [Test]
        public void Hubs12AndShell_RefusalsAndEvents_ReachJournal()
        {
            // --- хаб 1: estop, compare.mark, presentation.toggle, file.export, eta.toggle
            GameObject featureHost = NewHost("featurehub");
            KazistovVvUIManager featureUi = featureHost.AddComponent<KazistovVvUIManager>();
            FeatureHub.RegisterCommands(featureUi);

            Assert.IsTrue(AnyContains(InvokeAndReadLog("estop"), "поток траекторий в сцене нет"),
                "estop молча вышла без потока — аварийная остановка без объяснения");
            Assert.IsTrue(AnyContains(InvokeAndReadLog("compare.mark"), "поток траекторий в сцене нет"),
                "compare.mark молча вышла без потока");
            Assert.IsTrue(AnyContains(InvokeAndReadLog("presentation.toggle"), "презентационный режим невозможен"),
                "presentation.toggle молча вышла без службы режима");
            Assert.IsTrue(AnyContains(InvokeAndReadLog("file.export"), "не экспорт файлов"),
                "file.export не объясняет, что показывает путь, а не выгружает файлы");
            // eta.toggle: панель ВКЛЮЧЕНА по умолчанию, поэтому первое нажатие её выключает
            // (строка «выкл»), а второе — включает вместе с объяснением, почему при простое
            // на панели ничего нет. Проверяется именно объяснение.
            List<string> etaLines = InvokeAndReadLog("eta.toggle");
            etaLines.AddRange(InvokeAndReadLog("eta.toggle"));
            Assert.IsTrue(AnyContains(etaLines, "когда пойдёт траектория"),
                "eta.toggle не объясняет, почему панель пуста при простое: " +
                string.Join(" | ", etaLines.ToArray()));
            Assert.IsTrue(AnyContains(InvokeAndReadLog("session.load"), "служба сессий в сцене нет"),
                "session.load молча вышла без службы сессий (в приложении без сессий — «сессий нет»)");

            // --- хаб 2: постобработка (тот же `Refuse`, что волна 2 дала хабу)
            GameObject stage2Host = NewHost("stage2");
            KazistovVvUIManager stage2Ui = stage2Host.AddComponent<KazistovVvUIManager>();
            KvStageHub2.RegisterCommands(stage2Ui);

            foreach (string id in new[] { "post.apply.smooth", "post.apply.topt", "post.apply.energy" })
                Assert.IsTrue(AnyContains(InvokeAndReadLog(id), "в сцене нет"),
                    "«" + id + "» молча вышла без службы постобработки — отказа в журнале нет");

            // --- оболочка: настройки, тема, раскладка, палитра
            GameObject shellHost = NewHost("shell");
            RegisterManagerCommands(shellHost);

            Assert.IsTrue(AnyContains(InvokeAndReadLog("ui.palette"), "палитра команд не открыта"),
                "ui.palette молча вышла без оболочки");
            Assert.IsTrue(AnyContains(InvokeAndReadLog("view.theme"), "тема интерфейса"),
                "view.theme не оставила следа в журнале — только в консоль");
            Assert.IsTrue(AnyContains(InvokeAndReadLog("ui.resetlayout"), "раскладка окон сброшена"),
                "ui.resetlayout не оставила следа в журнале — только в консоль");
            foreach (string id in new[] { "help.open", "help.about", "ui.access" })
                Assert.IsTrue(AnyContains(InvokeAndReadLog(id), "настройки открыты"),
                    "«" + id + "» не оставила следа в журнале — открытие вкладки настроек без trace");
        }

        /// <summary>
        /// КОРЕНЬ 1 (хаб 3): ограничения точки и язык экспорта — причина и состояние в журнале.
        ///
        /// ОТКУДА ДЕФЕКТ. `KvStageHub3.SelectedWaypoint` писал «waypoint не выбран…» одним
        /// `Debug.Log`, а `RebuildWaypointRoute` — состояние пересборки; `export.lang` при
        /// отсутствии службы экспорта падал бы без объяснения. Листья: wp.orient, wp.speed,
        /// wp.detour, wp.pause, wp.clear, export.lang.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Нажатие без редактора точек/службы экспорта оставляет в журнале
        /// причину с названием «waypoint»/«экспорт» — консоли на устройстве нет.
        /// </summary>
        [Test]
        public void StageHub3_WaypointAndExport_RefusalsReachJournal()
        {
            GameObject host = NewHost("stage3");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            KvStageHub3.RegisterCommands(ui);

            foreach (string id in new[] { "wp.orient", "wp.speed", "wp.detour", "wp.pause", "wp.clear" })
                Assert.IsTrue(AnyContains(InvokeAndReadLog(id), "waypoint"),
                    "«" + id + "» молча вышла без выбранной точки — причина только в консоли");

            Assert.IsTrue(AnyContains(InvokeAndReadLog("export.lang"), "язык экспорта не переключен"),
                "export.lang молча вышла без службы экспорта");
        }

        /// <summary>
        /// КОРЕНЬ 1 (хаб 4): отказы и состояния команд этапов 13–36 — в журнале.
        ///
        /// ОТКУДА ДЕФЕКТ. `graphics.detect/benchmark/reset` заканчивались `if (graphics != null)`,
        /// `scara.recalibrate` — `if (flow != null)`, `valid.mannequins` — `if (манекены == null)`,
        /// `light.next` писал причину только в консоль, а переключатели (`collision.toggle`,
        /// `lab.run`, `web/voice/hands/eyes/env/cine.toggle`, `fail.*`, `report.make`,
        /// `logtools.export`) не оставляли ни строки, ни объяснения. Волна 2 этот же корень
        /// закрыла в `KvStageHub4.OpenTab` — здесь остальные действия хаба.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Каждое нажатие без службы/устройства оставляет причину, а
        /// переключение — состояние; тихого выхода нет.
        /// </summary>
        [Test]
        public void StageHub4_Actions_RefusalsAndStatesReachJournal()
        {
            GameObject host = NewHost("stage4");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            KvStageHub4.RegisterCommands(ui);

            var refusals = new Dictionary<string, string>
            {
                { "light.next", "сервис освещения в сцене нет" },
                { "scara.recalibrate", "поток траекторий в сцене нет" },
                { "graphics.detect", "служба графики недоступна" },
                { "graphics.benchmark", "служба графики недоступна" },
                { "graphics.reset", "служба графики недоступна" },
                { "valid.mannequins", "служба манекенов в сцене нет" },
                { "collision.toggle", "служба коллизионных прокси в сцене нет" },
                { "lab.run", "стенд планировщиков не запущен" },
                { "lab.tree", "служба стенда планировщиков в сцене нет" },
                { "web.toggle", "веб-дашборд не переключен" },
                { "voice.toggle", "голосовые команды не переключены" },
                { "hands.toggle", "отслеживание рук не переключено" },
                { "eyes.toggle", "отслеживание взгляда не переключено" },
                { "env.next", "служба окружения в сцене нет" },
                { "cine.toggle", "служба кинорежима в сцене нет" },
                { "fail.comms", "служба имитации отказов в сцене нет" },
                { "fail.joint", "служба имитации отказов в сцене нет" },
                { "fail.clear", "служба имитации отказов в сцене нет" },
                { "report.make", "служба отчёта в сцене нет" },
                { "logtools.export", "служба журнала в сцене нет" },
                { "heat.time", "Карта времени достижимости" },
            };

            foreach (KeyValuePair<string, string> pair in refusals)
            {
                List<string> lines = InvokeAndReadLog(pair.Key);
                Assert.IsTrue(AnyContains(lines, pair.Value),
                    "«" + pair.Key + "» не оставила в журнале строки с «" + pair.Value + "»: " +
                    string.Join(" | ", lines.ToArray()));
            }
        }

        // ==================== 2. КОРЕНЬ 2: НА КНОПКЕ ЕСТЬ СОСТОЯНИЕ ИЛИ ДОСТУПНОСТЬ

        /// <summary>
        /// КОРЕНЬ 2: команды, у которых не было функции состояния, теперь её имеют — и галка
        /// НЕ ВРЁТ (при отсутствии окна/служб она погашена, а не горит константой).
        ///
        /// ОТКУДА ДЕФЕКТ (карточки в `Docs/ДЕРЕВЬЯ_КНОПОК_КОМАНДЫ.md`):
        ///   • `pendant.variant/attach/jogmode` — «вариант/положение/режим не видны на кнопке,
        ///     цикл вслепую»;
        ///   • `lab.tab`/`forces.tab`/`heat.time`/`report.pdf`/`fail.tab`/`bt.tab` — у
        ///     команд-вкладок не было галки, в отличие от соседей `graph.open`/`joints.panel`;
        ///   • `lab.run` — «нет индикации „идёт прогон“ на кнопке»;
        ///   • `fail.comms/fail.joint` — «состояние аварии нигде не „залипает“ в UI».
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. У каждой команды есть `IsChecked`, и без окна/служб галка погашена.
        /// </summary>
        [Test]
        public void StateCommands_HaveIsCheckedAndDoNotLieWithoutServices()
        {
            GameObject featureHost = NewHost("statefeatures");
            KazistovVvUIManager featureUi = featureHost.AddComponent<KazistovVvUIManager>();
            FeatureHub.RegisterCommands(featureUi);

            GameObject stageHost = NewHost("statestage");
            KazistovVvUIManager stageUi = stageHost.AddComponent<KazistovVvUIManager>();
            KvStageHub.RegisterCommands(stageUi);

            GameObject stage4Host = NewHost("statestage4");
            KazistovVvUIManager stage4Ui = stage4Host.AddComponent<KazistovVvUIManager>();
            KvStageHub4.RegisterCommands(stage4Ui);

            var ids = new[]
            {
                "pendant.variant", "pendant.attach", "pendant.jogmode",
                "lab.tab", "lab.run", "forces.tab", "heat.time", "report.pdf",
                "fail.tab", "fail.comms", "fail.joint", "bt.tab"
            };

            foreach (string id in ids)
            {
                KvCommand command = KvCommands.Get(id);
                Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована");
                Assert.IsNotNull(command.IsChecked,
                    "«" + id + "» без IsChecked — оператор не видит состояния кнопки (лист «" + id + "»)");
                Assert.IsFalse(command.Checked,
                    "«" + id + "» показывает галку, хотя ни окна, ни службы в сцене нет");
            }
        }

        /// <summary>
        /// КОРЕНЬ 2 (продолжение): `valid.run` — «не имеет `IsEnabled`, кнопка активна в любом
        /// состоянии потока».
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Доступность функции есть и без потока траекторий команда недоступна:
        /// нажатие в этом состоянии не притворяется работой (`KvCommands.Invoke` откажет вслух).
        /// </summary>
        [Test]
        public void ValidRun_IsDisabledWithoutTrajectoryFlow()
        {
            GameObject host = NewHost("validrun");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            KvStageHub4.RegisterCommands(ui);

            KvCommand command = KvCommands.Get("valid.run");
            Assert.IsNotNull(command, "valid.run не зарегистрирована");
            Assert.IsNotNull(command.IsEnabled,
                "valid.run без IsEnabled — кнопка активна в любом состоянии потока (лист «valid.run»)");
            Assert.IsFalse(command.Enabled,
                "valid.run доступна без потока траекторий — пустить нечего");
        }

        /// <summary>
        /// КОРЕНЬ 2 (регресс): галки окна «Панель функций» читают САМО окно.
        ///
        /// ПОЧЕМУ ЗДЕСЬ. Листья `joints.panel` и `graph.open` закрыты карточкой «Дефект: нет» —
        /// но ни один тест не закреплял, что их галка не константа: окно закрыто — погашена,
        /// окно открыто на НУЖНОЙ вкладке — горит.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Обе команды следят за `Visible && VisibleTab` без констант.
        /// </summary>
        [Test]
        public void FeatureWindowTabs_GalkaFollowsRealWindowState()
        {
            var tabs = new Dictionary<string, int> { { "joints.panel", 2 }, { "graph.open", 5 } };

            GameObject host = NewHost("windowtabs");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            FeatureHub.RegisterCommands(ui);

            KvFeatureWindow window = host.GetComponent<KvFeatureWindow>();
            Assert.IsNotNull(window, "штатная регистрация не построила окно «Панель функций»");

            foreach (KeyValuePair<string, int> pair in tabs)
            {
                KvCommand command = KvCommands.Get(pair.Key);
                Assert.IsNotNull(command, "команда «" + pair.Key + "» не зарегистрирована");
                Assert.IsNotNull(command.IsChecked, "«" + pair.Key + "» без IsChecked");

                SetWindowState(window, false, -1);
                Assert.IsFalse(command.Checked, "«" + pair.Key + "» показывает галку при закрытом окне");

                SetWindowState(window, true, pair.Value);
                Assert.IsTrue(command.Checked,
                    "«" + pair.Key + "» не показывает галку, когда окно открыто на вкладке " + pair.Value);

                SetWindowState(window, true, pair.Value + 1);
                Assert.IsFalse(command.Checked,
                    "«" + pair.Key + "» горит на ЧУЖОЙ вкладке — галка не читает окно");
            }
        }

        // ==================== 3. КОРЕНЬ 3: ПОВЕДЕНИЕ ЗАКРЕПЛЕНО (ЛИСТЫ БЕЗ ДЕФЕКТА)

        /// <summary>
        /// КОРЕНЬ 3: листья, чья карточка говорит «Дефект: нет» или «подтверждено», закреплены
        /// проверкой ЖИВОГО поведения: команда зарегистрирована, обработчик на месте, а там, где
        /// карточка обещает галку, — галка есть и погашена без окна/служб.
        ///
        /// ОТКУДА ЛИСТЫ. Они висели со статусом «по коду, есть дефект» на коде, где дефекта уже
        /// нет (закрыт волнами 1–16.11 или отсутствовал по карточке) — ровно та ситуация, что
        /// волна 2 назвала «устранено ранее, закрыто регрессионным тестом».
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Реестр, обработчики и обещанные галки на месте — вернуться
        /// «молчаливому отказу» или «кнопке без состояния» больше неоткуда.
        /// </summary>
        [Test]
        public void PinnedLeaves_RegisteredWithHandlersAndPromisedState()
        {
            GameObject featureHost = NewHost("pinfeatures");
            KazistovVvUIManager featureUi = featureHost.AddComponent<KazistovVvUIManager>();
            FeatureHub.RegisterCommands(featureUi);

            GameObject stageHost = NewHost("pinstage");
            KazistovVvUIManager stageUi = stageHost.AddComponent<KazistovVvUIManager>();
            KvStageHub.RegisterCommands(stageUi);

            GameObject stage2Host = NewHost("pinstage2");
            KazistovVvUIManager stage2Ui = stage2Host.AddComponent<KazistovVvUIManager>();
            KvStageHub2.RegisterCommands(stage2Ui);

            GameObject stage3Host = NewHost("pinstage3");
            KazistovVvUIManager stage3Ui = stage3Host.AddComponent<KazistovVvUIManager>();
            KvStageHub3.RegisterCommands(stage3Ui);

            GameObject stage4Host = NewHost("pinstage4");
            KazistovVvUIManager stage4Ui = stage4Host.AddComponent<KazistovVvUIManager>();
            KvStageHub4.RegisterCommands(stage4Ui);

            GameObject shellHost = NewHost("pinshell");
            RegisterManagerCommands(shellHost);

            // --- обработчик на месте
            var withHandler = new[]
            {
                "joints.panel", "gripper.toggle", "waypoint.move", "waypoint.clear",
                "view.clearance", "zone.discard", "tool.measure.reset", "tool.grid.clear",
                "import.scan", "obstacle.evaluate", "shot.screenshot", "export.lang",
                "session.load", "session.save", "post.apply.smooth", "view.theme",
                "ui.resetlayout", "ui.access", "help.open", "help.about", "ui.palette",
                "cine.toggle", "env.next", "light.next", "collision.toggle", "lab.tab",
                "lab.tree", "forces.tab", "report.pdf", "bt.tab", "fail.tab",
                "logtools.export", "graphics.metrics", "valid.tab", "eta.toggle",
                "presentation.toggle", "pendant.toggle", "waypoint.delete", "waypoint.build"
            };
            foreach (string id in withHandler)
            {
                KvCommand command = KvCommands.Get(id);
                Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована (лист не закрыт)");
                Assert.IsNotNull(command.Execute, "у «" + id + "» нет обработчика");
            }

            // --- галка там, где её обещает карточка; без окна/служб — погашена, не константа
            var withState = new[]
            {
                "joints.panel", "graph.open", "gripper.toggle", "view.clearance",
                "zone.discard", "cine.toggle", "collision.toggle", "lab.tree",
                "web.toggle", "voice.toggle", "hands.toggle", "eyes.toggle",
                "valid.mannequins", "presentation.toggle", "pendant.toggle",
                "graphics.metrics", "session.panel"
            };
            foreach (string id in withState)
            {
                KvCommand command = KvCommands.Get(id);
                Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована");
                Assert.IsNotNull(command.IsChecked, "у «" + id + "» нет IsChecked (лист «" + id + "»)");
                Assert.IsFalse(command.Checked, "«" + id + "» горит без окна/служб — галка врёт");
            }

            // --- `eta.toggle` и `galka по нажатию`: панель ETA включена ПО УМОЛЧАНИЮ (ждёт
            //     траекторию), поэтому её галка проверяется не «погашена ли», а тем, что она
            //     СЛЕДУЕТ ЗА НАЖАТИЕМ и возвращается в исходное состояние.
            KvCommand eta = KvCommands.Get("eta.toggle");
            Assert.IsNotNull(eta, "eta.toggle не зарегистрирована");
            Assert.IsNotNull(eta.IsChecked, "eta.toggle без IsChecked (лист «eta.toggle»)");
            bool etaWas = eta.Checked;
            KvCommands.Invoke("eta.toggle");
            Assert.AreNotEqual(etaWas, eta.Checked, "галка eta.toggle не последовала за нажатием");
            KvCommands.Invoke("eta.toggle");
            Assert.AreEqual(etaWas, eta.Checked, "eta.toggle не вернулась в исходное состояние");
        }

        /// <summary>
        /// КОРЕНЬ 1 (продолжение): отказы САМОГО менеджера точек доходят до подписчика.
        ///
        /// ОТКУДА ДЕФЕКТ. `KvWaypointManager.Remove` возвращал `false` без единой строки
        /// (лист `waypoint.delete`: «тот же тихий отказ, что у earlier/later»), а `RebuildRoute`
        /// при отсутствии робота/точек ставил `routeNote` и молча выходил (лист
        /// `waypoint.build`: «отказы не сопровождаются `Say`»).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Оба отказа произносятся вслух — подписчик (`Message`) получает
        /// текст с причиной, а в приложении подписчик этот текст кладёт в журнал.
        /// </summary>
        [Test]
        public void WaypointManager_RefusalsAreSpoken()
        {
            var manager = new KvWaypointManager();
            var said = new List<string>();
            manager.Message += delegate(string text) { said.Add(text); };

            said.Clear();
            Assert.IsFalse(manager.Remove(-1), "удаление без выбранной точки вернуло true");
            Assert.IsTrue(said.Count > 0 && said[0].IndexOf("не выбрана", StringComparison.OrdinalIgnoreCase) >= 0,
                "удаление без выбранной точки промолчало: " + string.Join(" | ", said.ToArray()));

            said.Clear();
            Assert.IsFalse(manager.RebuildRoute(), "пересборка без робота вернула true");
            Assert.IsTrue(said.Count > 0 && said[0].IndexOf("робот не определён", StringComparison.OrdinalIgnoreCase) >= 0,
                "пересборка без робота промолчала: " + string.Join(" | ", said.ToArray()));
        }
    }
}
