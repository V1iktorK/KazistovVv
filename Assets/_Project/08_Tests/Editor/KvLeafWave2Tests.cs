// ================================================================================================
//  KvLeafWave2Tests.cs — EditMode-тесты ВОЛНЫ 2 КАМПАНИИ «каждый листочек путеводителя закрыт».
//
//  ОТКУДА ЗАДАЧА. `Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv` — список незакрытых листьев конспекта кнопок.
//  Волна 2 берёт 110 листьев со статусом «по коду, есть дефект» (все — «Реестр KvCommands»),
//  массовой триажей сгруппированных по ОБЩИМ КОРНЯМ дефекта (одна правка = много листьев):
//
//    КОРЕНЬ 1 — «тихий отказ вкладки». `KvStageHub4.OpenTab` при отсутствии окна «Верстак»
//      молча возвращался (`if (KvWorkbenchWindow.Instance == null) return;`) — то самое
//      поведение, что волна 1 лечила в `KvStageHub3.OpenTab` (лист calib.tab), только в
//      ВТОРОМ хабе. Листья: collision.tab, lab.tab, cameras.tab, forces.tab, heat.time,
//      report.pdf, net.tab, script.tab, bt.tab, mat.tab, titles.tab, vo.tab, fail.tab,
//      valid.tab, logtools.tab. Тот же корень у `OpenGraphicsTab` (graphics.tab): раздел
//      «Графика» не открывался МОЛЧА, когда панели настроек в сцене нет.
//
//    КОРЕНЬ 2 — «нет `IsChecked`». Семь команд «панель функций» (`hub.Window.Toggle(n)`)
//      не имели функции состояния, в отличие от соседей `graph.open`/`joints.panel`:
//      оператор не видел, открыта ли панель. Листья: features.open (0), zone.panel (3),
//      compare.open (4), perf.open (6), log.open (7), scenarios.open (8), session.panel (9).
//
//  ЧЕМ ПРОВЕРЯЕТСЯ (P4-3). ТОЛЬКО ПОВЕДЕНИЕМ живого кода: команды нажимаются штатным
//  `KvCommands.Invoke`/`Execute`, состояние читается геттером `KvCommand.Checked`, журнал —
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
using KazistovVvDiagnostics;
using KazistovVvFeatures;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Волна 2: листья «по коду, есть дефект» из Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvLeafWave2Tests
    {
        // ------------------------------------------------------------------ инфраструктура

        /// <summary>Хосты, созданные тестом: снимаются в TearDown независимо от исхода.</summary>
        private readonly List<GameObject> hosts = new List<GameObject>();

        /// <summary>
        /// БЫЛО ЛИ ОТСЕЧЕНИЕ ПОВТОРОВ ВКЛЮЧЕНО ДО ТЕСТА (возвращается в TearDown).
        /// НУЖНО ПОТОМУ, что ключ журнала вырезает числа и одинаковые события: два нажатия
        /// разных команд с ОДИНАКОВОЙ причиной («вкладка «forces» не открыта…») — это одно
        /// событие, и второе в журнале не появится. Проверяемая правда была бы не поведением
        /// кода, а правилом журнала.
        /// </summary>
        private bool repeatsWereOn;

        /// <summary>
        /// СНИМОК РЕЕСТРА КОМАНД ДО ШТАТНОЙ РЕГИСТРАЦИИ. `KvCommands` — общий на весь прогон
        /// статик: наполнив его сотней команд с обработчиками, тест обязан вернуть набор таким,
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
            var host = new GameObject("KvLeafWave2_" + name);
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

        /// <summary>
        /// СОСТОЯНИЕ ОКНА «ПАНЕЛЬ ФУНКЦИЙ» НАПРЯМУЮ (видимость + номер вкладки).
        ///
        /// ЗАЧЕМ НЕ `Show()`. В EditMode шелл не собирается: службы окна (`hub.Sessions`,
        /// `hub.Recording`, …) создаются в `Awake`, которого здесь не бывает, и сборка
        /// содержимой вкладки падает — окно показать целиком нельзя. А галке нужны ровно два
        /// факта, которые `Show`/`SetTab` пишут ДО сборки содержимого: `visible` и `tab`
        /// (их читают публичные `Visible`/`VisibleTab`). Состояние задаётся теми же полями,
        /// что и штатный показ; содержимое вкладки проверка не трогает.
        /// </summary>
        private static void SetWindowState(KvFeatureWindow window, bool visible, int tab)
        {
            Type type = typeof(KvFeatureWindow);
            type.GetField("visible", System.Reflection.BindingFlags.NonPublic |
                          System.Reflection.BindingFlags.Instance).SetValue(window, visible);
            type.GetField("tab", System.Reflection.BindingFlags.NonPublic |
                          System.Reflection.BindingFlags.Instance).SetValue(window, tab);
        }

        /// <summary>ЕСТЬ ЛИ СРЕДИ СТРОКО ПОДСТРОКА (без учёта регистра).</summary>
        private static bool AnyContains(List<string> lines, string needle)
        {
            foreach (string line in lines)
                if (line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // ================================== 1. ВКЛАДКИ: ТИХИЙ ОТКАЗ ВХОДИТ В ЖУРНАЛ

        /// <summary>
        /// КОРЕНЬ 1: 15 команд-вкладок `KvStageHub4` ОБЪЯСНЯЮТ отказ, когда окна «Верстак» нет.
        ///
        /// ОТКУДА ДЕФЕКТ. `KvStageHub4.OpenTab` заканчивался строкой
        /// `if (KvWorkbenchWindow.Instance == null) return;` — нажатие «Материалы», «Титры»,
        /// «Сеть и мониторинг», «Проверка перед пуском» и т.д. не давало НИЧЕГО: ни окна, ни
        /// строки в журнале, ни причины. Это тот же дефект, что волна 1 закрыла в
        /// `KvStageHub3.OpenTab` (лист calib.tab), только во втором хабе — правка одна, а
        /// листьев пятнадцать.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Каждая из команд-вкладок, нажатая без окна, оставляет в журнале
        /// причину с названием окна «Верстак».
        /// </summary>
        [Test]
        public void StageHub4Tabs_MissingWorkbench_LogHonestReasonInsteadOfSilentExit()
        {
            var ids = new[]
            {
                "collision.tab", "lab.tab", "cameras.tab", "forces.tab", "heat.time",
                "report.pdf", "net.tab", "script.tab", "bt.tab", "mat.tab", "titles.tab",
                "vo.tab", "fail.tab", "valid.tab", "logtools.tab"
            };

            GameObject host = NewHost("stage4ui");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            KvStageHub4.RegisterCommands(ui);

            foreach (string id in ids)
            {
                KvCommand command = KvCommands.Get(id);
                Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована — лист не о чём проверять");
                Assert.IsNotNull(command.Execute, "у «" + id + "» нет обработчика");

                int before = LogCount();
                KvCommands.Invoke(id);
                List<string> lines = LogSince(before);

                Assert.IsTrue(AnyContains(lines, "верстак"),
                    "«" + id + "» молча вышла без окна верстака — нажатие без причины: " +
                    string.Join(" | ", lines.ToArray()));
            }
        }

        /// <summary>
        /// КОРЕНЬ 1 (продолжение): `graphics.tab` — раздел «Графика» панели настроек.
        ///
        /// ОТКУДА ДЕФЕКТ. `KvStageHub4.OpenGraphicsTab` при отсутствии панели настроек
        /// (`KazistovVvUIManager.Instance == null`) возвращался МОЛЧА: оператор нажимал
        /// «Графика» и не видел ни раздела, ни объяснения (ДЕРЕВЬЯ_КНОПОК_КОМАНДЫ, карточка
        /// `graphics.tab`: «тихие отказы»).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Нажатие без панели настроек оставляет в журнале причину с названием
        /// раздела — тихого выхода больше нет.
        /// </summary>
        [Test]
        public void GraphicsTab_MissingSettingsPanel_LogsHonestReason()
        {
            GameObject host = NewHost("graphicsui");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            KvStageHub4.RegisterCommands(ui);

            // Анти-вакуум: синглтон панели настроек в EditMode пуст — ровно то состояние,
            // в котором раздел «Графика» нечем открыть.
            Assert.IsNull(KazistovVvUIManager.Instance,
                "синглтон панели настроек не пуст — проверка не о том состоянии");

            int before = LogCount();
            KvCommands.Invoke("graphics.tab");
            List<string> lines = LogSince(before);

            Assert.IsTrue(AnyContains(lines, "не открыт"),
                "раздел «Графика» молча не открылся — нажатие без причины: " +
                string.Join(" | ", lines.ToArray()));
        }

        // ================================== 2. ГАЛКА ПАНЕЛИ ФУНКЦИЙ (7 листьев)

        /// <summary>
        /// КОРЕНЬ 2: семь команд «панель функций» показывают состояние кнопкой (`IsChecked`).
        ///
        /// ОТКУДА ДЕФЕКТ. `features.open` (F5), `zone.panel`, `compare.open` (F7), `perf.open`,
        /// `log.open` (F6), `scenarios.open` и `session.panel` открывали одно и то же окно
        /// (`hub.Window.Toggle(n)`), но не имели функции состояния — в отличие от соседей
        /// `graph.open`/`joints.panel`, чья галка «честная». Оператор не видел по кнопке,
        /// открыта ли панель, и нажатие второй раз читалось как «ничего не произошло»
        /// (ДЕРЕВЬЯ_КНОПОК_КОМАНДЫ: «нет `IsChecked`»).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Галка каждой команды отражает ФАКТ окна: окно открыто на её
        /// вкладке — галка горит; окно закрыто — галка погашена. Никаких констант: состояние
        /// читается из самого окна.
        /// </summary>
        [Test]
        public void WindowTabCommands_IsCheckedFollowsRealWindowState()
        {
            // id → номер вкладки окна «Панель функций», который открывает команда.
            var tabs = new Dictionary<string, int>
            {
                { "features.open", 0 },
                { "zone.panel", 3 },
                { "compare.open", 4 },
                { "perf.open", 6 },
                { "log.open", 7 },
                { "scenarios.open", 8 },
                { "session.panel", 9 }
            };

            GameObject host = NewHost("featurewindow");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            FeatureHub.RegisterCommands(ui);

            KvFeatureWindow window = host.GetComponent<KvFeatureWindow>();
            Assert.IsNotNull(window, "штатная регистрация не построила окно «Панель функций»");

            foreach (KeyValuePair<string, int> pair in tabs)
            {
                KvCommand command = KvCommands.Get(pair.Key);
                Assert.IsNotNull(command, "команда «" + pair.Key + "» не зарегистрирована");
                Assert.IsNotNull(command.IsChecked,
                    "«" + pair.Key + "» без IsChecked — оператор не видит, открыта ли панель (лист «" +
                    pair.Key + "»)");

                // Окно закрыто — галка погашена (проверка, что функция не константа true).
                SetWindowState(window, false, -1);
                Assert.IsFalse(command.Checked,
                    "«" + pair.Key + "» показывает галку при закрытом окне");

                // Окно открыто НА ЕЁ вкладке — галка горит.
                SetWindowState(window, true, pair.Value);
                Assert.IsTrue(command.Checked,
                    "«" + pair.Key + "» не показывает галку, когда окно открыто на вкладке " +
                    pair.Value);
            }
        }

        // ================================== 3. СЛЕД В ЖУРНАЛЕ, А НЕ ТОЛЬКО В КОНСОЛИ

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

        /// <summary>
        /// КОРЕНЬ 3: переключатели постобработки пишут отказ и состояние в ЖУРНАЛ, а не в консоль.
        ///
        /// ОТКУДА ДЕФЕКТ. `post.auto` и `post.auto.selected` при отсутствии службы сглаживания
        /// МОЛЧА возвращались, а своё переключение писали только `Debug.Log("[Stages2] …")` —
        /// на устройстве консоли нет (ДЕРЕВЬЯ_КНОПОК_КОМАНДЫ, карточки `post.auto`,
        /// `post.auto.selected`: «только консоль Unity»). Листья: постобработка / автосглаживание.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Нажатие без службы обязано оставить в журнале причину с названием
        /// службы — тихого выхода и «следа только в консоли» больше нет.
        /// </summary>
        [Test]
        public void SmoothingToggles_RefusalIsJournalled()
        {
            var ids = new[] { "post.auto", "post.auto.selected" };

            GameObject host = NewHost("stage2ui");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            KvStageHub2.RegisterCommands(ui);

            foreach (string id in ids)
            {
                KvCommand command = KvCommands.Get(id);
                Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована");

                int before = LogCount();
                KvCommands.Invoke(id);
                List<string> lines = LogSince(before);

                Assert.IsTrue(AnyContains(lines, "служба сглаживания"),
                    "«" + id + "» молча вышла без службы сглаживания — отказа в журнале нет: " +
                    string.Join(" | ", lines.ToArray()));
            }
        }

        /// <summary>
        /// КОРЕНЬ 3 (продолжение): `shot.annotate` — тумблер подписи на скриншоте.
        ///
        /// ОТКУДА ДЕФЕКТ. Переключение писалось только `Debug.Log("[Capture] …")`, а отказ при
        /// отсутствии службы захвата не проговаривался вовсе (ДЕРЕВЬЯ_КНОПОК_КОМАНДЫ, карточка
        /// `shot.annotate`: «переключение — только в консоль, не в журнал»).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Нажатие без службы захвата оставляет в журнале причину — и не падает.
        /// </summary>
        [Test]
        public void ShotAnnotate_RefusalIsJournalled()
        {
            GameObject host = NewHost("stage1ui");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            KvStageHub.RegisterCommands(ui);

            KvCommand command = KvCommands.Get("shot.annotate");
            Assert.IsNotNull(command, "shot.annotate не зарегистрирована");

            int before = LogCount();
            KvCommands.Invoke("shot.annotate");
            List<string> lines = LogSince(before);

            Assert.IsTrue(AnyContains(lines, "служба захвата"),
                "shot.annotate молча вышла без службы захвата — отказа в журнале нет: " +
                string.Join(" | ", lines.ToArray()));
        }

        /// <summary>
        /// КОРЕНЬ 3 (продолжение): `lang.cycle` — смена языка интерфейса.
        ///
        /// ОТКУДА ДЕФЕКТ. След смены уходил только в консоль (`Log(...)`, а он ещё и выключен
        /// по умолчанию на устройстве), в `KvActionLog` — ничего (ДЕРЕВЬЯ_КНОПОК_КОМАНДЫ,
        /// карточка `lang.cycle`: «след — только в консоль»). Оператор, сменивший язык, не видит
        /// ни факта смены, ни нового языка нигде, кроме… самого языка.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Нажатие пишет в журнал строку с новым языком И меняет подпись самой
        /// кнопки (заголовок берётся из словаря текущего языка) — состояние видно и в журнале,
        /// и на кнопке. Язык возвращается в TearDown-безопасном `finally`.
        /// </summary>
        [Test]
        public void LangCycle_WritesChangeToJournalAndRelabelsButton()
        {
            string original = KvLoc.CurrentCode;
            try
            {
                GameObject host = NewHost("langui");
                RegisterManagerCommands(host);

                KvCommand command = KvCommands.Get("lang.cycle");
                Assert.IsNotNull(command, "lang.cycle не зарегистрирована");
                string titleBefore = command.LocalizedTitle;
                Assert.IsNotEmpty(titleBefore, "у кнопки нет подписи — сравнивать нечего");

                int before = LogCount();
                KvCommands.Invoke("lang.cycle");

                Assert.AreNotEqual(original, KvLoc.CurrentCode,
                    "язык не сменился — команду проверять не на чём");

                List<string> lines = LogSince(before);
                Assert.IsTrue(AnyContains(lines, "смена языка"),
                    "смена языка не записана в журнал — след только в консоли: " +
                    string.Join(" | ", lines.ToArray()));

                Assert.AreNotEqual(titleBefore, command.LocalizedTitle,
                    "подпись кнопки не изменилась с языком — текущий язык на кнопке не виден");
            }
            finally
            {
                KvLoc.SetLanguage(original);
            }
        }

        // ============================ 4. НАСТРОЙКА БЕЗ ПОТОКА: ПРЕДУПРЕЖДЕНИЕ В ЖУРНАЛЕ

        /// <summary>
        /// КОРЕНЬ 4: визуализации предупреждают, когда «настройка включена, а сцена не тронута».
        ///
        /// ОТКУДА ДЕФЕКТ. `view.workspace`, `view.limits`, `view.metrics` писали настройку
        /// (`KvSettings.*`) и вызывали `ApplyVisualization`, который при отсутствии потока этапов
        /// (`Flow == null`) ограничивался консольным `Log(...)`: оператор видел горящую галку и
        /// картину, которая не изменилась, без единого объяснения (ДЕРЕВЬЯ_КНОПОК_КОМАНДЫ,
        /// карточки `view.workspace`/`view.limits`/`view.metrics`: «молчаливый разрыв»).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Нажатие без потока оставляет в журнале причину с фактом «в сцене нет»;
        /// настройка при этом применяется, как и раньше (галка не врёт).
        /// </summary>
        [Test]
        public void VisualizationToggles_WarnWhenFlowIsMissing()
        {
            var ids = new[] { "view.workspace", "view.limits", "view.metrics" };
            bool workspaceWas = KvSettings.ShowWorkspace;
            bool limitsWas = KvSettings.ShowJointLimits;
            bool metricsWas = KvSettings.ShowMetrics;

            try
            {
                GameObject host = NewHost("visui");
                RegisterManagerCommands(host);

                foreach (string id in ids)
                {
                    KvCommand command = KvCommands.Get(id);
                    Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована");

                    int before = LogCount();
                    KvCommands.Invoke(id);
                    List<string> lines = LogSince(before);

                    Assert.IsTrue(AnyContains(lines, "в сцене нет"),
                        "«" + id + "» включила настройку без предупреждения о пустом потоке: " +
                        string.Join(" | ", lines.ToArray()));
                }

                // ГАЛКА НЕ ВРЁТ: настройка действительно применилась.
                Assert.IsTrue(KvSettings.ShowWorkspace != workspaceWas,
                    "view.workspace не поменяла настройку — проверка не о том состоянии");
            }
            finally
            {
                KvSettings.ShowWorkspace = workspaceWas;
                KvSettings.ShowJointLimits = limitsWas;
                KvSettings.ShowMetrics = metricsWas;
            }
        }

        /// <summary>
        /// КОРЕНЬ 4 (продолжение): лазеры не молчат без рига.
        ///
        /// ОТКУДА ДЕФЕКТ. `path.select` (зелёная указка) и `tool.flashlight` (фонарик) при
        /// отсутствии рига камеры делали ровно одно — `if (cam == null) return;`: нажатие без
        /// результата и без объяснения, а успешный след писался только в консоль
        /// (ДЕРЕВЬЯ_КНОПОК_КОМАНДЫ, карточки `path.select`, `tool.flashlight`: «тихий отказ»).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Нажатие без рига оставляет в журнале причину с фактом «рига в сцене
        /// нет» и не падает.
        /// </summary>
        [Test]
        public void LaserCommands_RefusalIsJournalled()
        {
            var ids = new[] { "path.select", "tool.flashlight" };

            GameObject host = NewHost("laserui");
            RegisterManagerCommands(host);

            foreach (string id in ids)
            {
                KvCommand command = KvCommands.Get(id);
                Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована");

                int before = LogCount();
                KvCommands.Invoke(id);
                List<string> lines = LogSince(before);

                Assert.IsTrue(AnyContains(lines, "рига в сцене нет"),
                    "«" + id + "» молча вышла без рига — отказа в журнале нет: " +
                    string.Join(" | ", lines.ToArray()));
            }
        }

        // ================================== 5. ОБРАБОТЧИКИ ХАБА: ТИХИЙ ОТКАЗ СТАЛ ПРИЧИНОЙ

        /// <summary>
        /// КОРЕНЬ 1 (продолжение): восемь обработчиков `KvStageHub2` объясняют отказ.
        ///
        /// ОТКУДА ДЕФЕКТ. Все они заканчивались строкой `if (сервис == null) return;` — нажатие
        /// не давало НИЧЕГО (ДЕРЕВЬЯ_КНОПОК_КОМАНДЫ: «тихий отказ» у `startmenu.show`,
        /// `workbench.toggle`, `post.smooth`, `tut.toggle`, `tut.restart`, `demo.quick`; у
        /// `post.timeoptimal`/`post.energy` — тот же общий для вкладок корень). На устройстве
        /// консоли нет, поэтому «нажал — и ничего» оставалось без объяснения.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Каждое нажатие без службы оставляет в журнале причину с фактом
        /// «в сцене нет» и не падает.
        /// </summary>
        [Test]
        public void StageHub2Handlers_MissingService_LogHonestReason()
        {
            var ids = new[]
            {
                "startmenu.show", "workbench.toggle",
                "post.smooth", "post.timeoptimal", "post.energy",
                "tut.toggle", "tut.restart", "demo.quick"
            };

            GameObject host = NewHost("stage2handlers");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            KvStageHub2.RegisterCommands(ui);

            foreach (string id in ids)
            {
                KvCommand command = KvCommands.Get(id);
                Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована");

                int before = LogCount();
                KvCommands.Invoke(id);
                List<string> lines = LogSince(before);

                Assert.IsTrue(AnyContains(lines, "в сцене нет"),
                    "«" + id + "» молча вышла без службы — отказа в журнале нет: " +
                    string.Join(" | ", lines.ToArray()));
            }
        }

        /// <summary>
        /// КОРЕНЬ 1 (регресс): пять вкладок `KvStageHub3` объясняют отказ УЖЕ СЕЙЧАС.
        ///
        /// ПОЧЕМУ ЗДЕСЬ. «Тихий отказ (общий для всех `*.tab`)» у `constr.tab`, `wp.tab`,
        /// `payload.tab`, `export.tab`, `import.tab` устранён волной 1 правкой
        /// `KvStageHub3.OpenTab` (лист calib.tab), но ни один тест этого не закреплял: пять
        /// листьев висели со статусом «по коду, есть дефект» на уже починенном коде. Проверка
        /// ниже — регрессионная: она не даёт молчаливому выходу вернуться.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Каждая из пяти вкладок без окна верстака оставляет в журнале причину
        /// с названием окна.
        /// </summary>
        [Test]
        public void StageHub3Tabs_MissingWorkbench_LogHonestReason()
        {
            var ids = new[] { "constr.tab", "wp.tab", "payload.tab", "export.tab", "import.tab" };

            GameObject host = NewHost("stage3tabs");
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            KvStageHub3.RegisterCommands(ui);

            foreach (string id in ids)
            {
                KvCommand command = KvCommands.Get(id);
                Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована");

                int before = LogCount();
                KvCommands.Invoke(id);
                List<string> lines = LogSince(before);

                Assert.IsTrue(AnyContains(lines, "верстак"),
                    "«" + id + "» молча вышла без окна верстака — нажатие без причины: " +
                    string.Join(" | ", lines.ToArray()));
            }
        }
    }
}
