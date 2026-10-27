using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ ВОЛНЫ §42 — ПЕРЕНОС ФУНКЦИОНАЛА КОМПЬЮТЕРА В ШЛЕМ.
    ///
    /// ПИШУТСЯ ДО КОДА ([S7] шаг 4). Новые файлы слоя XR заведены сразу с объявлениями и
    /// `NotImplementedException` в телах — поэтому красный прогон честный: компиляция чистая, прежние
    /// тесты не затронуты, а падают ровно те проверки, которых ещё нет.
    ///
    /// ЧТО ЗАКРЕПЛЯЕТСЯ (по пунктам [S1] и решениям владельца 25.10.2026):
    ///   • 50 команд компьютера открывают окна — у каждой есть СВОЙ экран в шлеме (решение В1 — «свои экраны»);
    ///   • окно компьютера открывается ОКНОМ, а не выполняется вслепую; два окна одного экрана живут рядом;
    ///   • у каждой вкладки каждого окна есть строки, и пустого окна не бывает;
    ///   • столы: квадратный 0,72 × 0,48 м (решение В2) и авто R = 0,95 × вылет + 0,10 м;
    ///   • при запуске в шлеме пусто (2 стола и 2 робота сцены выключены), сцена не меняется;
    ///   • визуализации обязаны называть причину, по которой их не видно (строка [S2] №6);
    ///   • голос без appID честно говорит «недоступен»;
    ///   • сцены остаются байт в байт.
    /// </summary>
    public class KvXrS42Tests
    {
        // ============================================================ ОБЩЕЕ

        /// <summary>Панель в памяти (сцена между тестами не сохраняется).</summary>
        private static KvXrGroupPanel NewPanel(out GameObject host)
        {
            host = new GameObject("KvXrS42TestHost");
            var panel = host.AddComponent<KvXrGroupPanel>();
            panel.logOnBuild = false;
            panel.followHand = false;
            panel.Rebuild();
            return panel;
        }

        /// <summary>Номер цели по названию (−1 — нет такой).</summary>
        private static int IndexOf(KvXrGroupPanel panel, string title)
        {
            for (int i = 0; i < panel.RayTargetCount; i++)
                if (panel.TitleAt(i) == title) return i;
            return -1;
        }

        /// <summary>
        /// ПОЛНЫЙ СПИСОК КОМАНД КОМПЬЮТЕРА, ОТКРЫВАЮЩИХ ОКНО (50 штук, классификация 25.10.2026).
        /// Этот список — контракт: каждая команда обязана найти свой экран в шлеме.
        /// </summary>
        private static readonly string[] WindowCommands =
        {
            // --- доки (4)
            "view.panel.tree", "view.panel.properties", "view.panel.settings", "help.hotkeys",
            // --- окно настроек ПК (5)
            "ui.settings", "ui.access", "help.about", "help.open", "graphics.tab",
            // --- палитра (1)
            "ui.palette",
            // --- панель функций, 10 вкладок (11 команд)
            "features.open", "pose.save", "pose.goto", "joints.panel", "zone.panel",
            "compare.open", "graph.open", "perf.open", "log.open", "scenarios.open", "session.panel",
            // --- верстак, 24 вкладки (25 команд)
            "workbench.toggle", "post.smooth", "post.timeoptimal", "post.energy",
            "wp.tab", "constr.tab", "calib.tab", "payload.tab", "export.tab", "import.tab",
            "collision.tab", "lab.tab", "cameras.tab", "forces.tab", "heat.time", "report.pdf",
            "net.tab", "script.tab", "bt.tab", "mat.tab", "titles.tab", "vo.tab",
            "fail.tab", "valid.tab", "logtools.tab",
            // --- прочие окна (4)
            "startmenu.show", "tut.toggle", "health.toggle", "pendant.toggle"
        };

        // ============================================================ 1. ОКНА ИЗ ВЕЕРА

        /// <summary>
        /// 1. У КАЖДОЙ КОМАНДЫ-ОКНА ЕСТЬ ЭКРАН В ШЛЕМЕ.
        /// Задание [S1] №1: «те иконки, которые на компьютере открывают окна/панели, в шлеме должны
        /// открывать такие же окна». Проверяется весь список из 50 команд, а не три выборочные.
        /// </summary>
        [Test]
        public void Windows_EveryWindowCommandHasScreen()
        {
            Assert.AreEqual(50, WindowCommands.Length, "список команд-окон в тесте — ровно 50");
            Assert.AreEqual(WindowCommands.Length, KvXrDesktopWindows.Count,
                "таблица соответствия обязана знать все команды-окна");

            var missing = new List<string>();
            foreach (string id in WindowCommands)
            {
                KvXrDesktopWindowInfo info;
                if (!KvXrDesktopWindows.TryGet(id, out info)) { missing.Add(id); continue; }
                Assert.IsTrue(KvXrGroupPanel.IsScreen(info.Screen),
                    "команда «" + id + "» ведёт на экран, а не на уровень иконок");
                Assert.IsFalse(string.IsNullOrEmpty(info.Title),
                    "у окна команды «" + id + "» обязан быть заголовок");
            }

            Assert.AreEqual(0, missing.Count, "без экрана остались команды: " + string.Join(", ", missing.ToArray()));
        }

        /// <summary>
        /// 2. ОКНО ОТКРЫВАЕТСЯ ОКНОМ, А НЕ ВЫПОЛНЯЕТСЯ ВСЛЕПУЮ.
        /// Раньше иконка вызывала команду компьютера, а её окно рисовалось на погашенном канвасе —
        /// владелец видел «нажалось и ничего». Теперь нажатие открывает окно в мире.
        /// </summary>
        [Test]
        public void Windows_OpenCreatesWindowNotCommand()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(out host);
            try
            {
                Assert.IsTrue(panel.OpenDesktopWindow("ui.settings"),
                    "команда окна обязана открываться окном");
                Assert.AreEqual("ui.settings", panel.WindowCommandId,
                    "окно помнит, какой командой открыто — иначе журнал врёт");
                Assert.AreEqual(KvXrPanelLevel.DesktopSettingsScreen, panel.OpenedWindowScreen,
                    "окно открыто экраном «Настройки ПК» (уровень при этом — окно, а не экран панели)");

                Assert.IsFalse(panel.OpenDesktopWindow("robot.stop"),
                    "команда-действие окном не открывается — она выполняется как команда");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 3. ДВА ОКНА ОДНОГО ЭКРАНА ЖИВУТ РЯДОМ.
        /// У «Верстака» 23 вкладки; дедупликация «по экрану» склеила бы их в одно окно, и владелец
        /// терял бы одну из двух открытых вкладок.
        /// </summary>
        [Test]
        public void Windows_KeyIsUniquePerCommand()
        {
            var camGo = new GameObject("KvXrS42Camera");
            Camera camera = camGo.AddComponent<Camera>();
            KvXrWindowHost host = null;
            try
            {
                host = KvXrWindowHost.Ensure(camera);
                host.logOnOpen = false;
                host.maxWindows = 4;

                host.Open(KvXrPanelLevel.WorkbenchScreen, "Верстак · Точки", "wp.tab");
                host.Open(KvXrPanelLevel.WorkbenchScreen, "Верстак · Экспорт", "export.tab");
                Assert.AreEqual(2, host.Count, "два окна одной страницы обязаны открыться оба");

                host.Open(KvXrPanelLevel.WorkbenchScreen, "Верстак · Точки", "wp.tab");
                Assert.AreEqual(2, host.Count, "повторное открытие того же окна второе не создаёт");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host.gameObject);
                Object.DestroyImmediate(camGo);
            }
        }

        /// <summary>
        /// 4. У КАЖДОЙ ВКЛАДКИ КАЖДОГО ОКНА ЕСТЬ СТРОКИ — ПУСТОГО ОКНА НЕ БЫВАЕТ.
        /// Проверяются все 41 вкладка пяти новых экранов на ПУСТЫХ данных: даже тогда первая строка
        /// обязана честно сказать, чего нет и почему.
        /// </summary>
        [Test]
        public void Windows_EveryTabHasRows()
        {
            var screens = new[]
            {
                KvXrPanelLevel.DesktopSettingsScreen,
                KvXrPanelLevel.FeaturesScreen,
                KvXrPanelLevel.WorkbenchScreen,
                KvXrPanelLevel.HotkeysScreen,
                KvXrPanelLevel.PendantScreen
            };

            var data = new KvXrScreenData();      // всё пусто: худший случай
            int tabs = 0;

            foreach (KvXrPanelLevel screen in screens)
            {
                int count = KvXrDesktopTabs.CountOf(screen);
                Assert.Greater(count, 0, "у экрана «" + KvXrGroupPanel.ScreenTitle(screen) + "» есть вкладки");
                tabs += count;

                for (int tab = 0; tab < count; tab++)
                {
                    List<string> rows = KvXrScreenRows.Build(screen, tab, data);
                    Assert.IsNotNull(rows, "строки вкладки не пустая ссылка");
                    Assert.GreaterOrEqual(rows.Count, 2,
                        "«" + KvXrDesktopTabs.TitleOf(screen, tab) + "»: заголовок + хотя бы одна строка");
                    Assert.IsFalse(string.IsNullOrEmpty(rows[0]),
                        "«" + KvXrDesktopTabs.TitleOf(screen, tab) + "»: первая строка не пустая");
                }
            }

            Assert.AreEqual(41, tabs, "вкладок в новых окнах: 6 + 10 + 23 + 1 + 1 = 41");
        }

        /// <summary>
        /// 5. ЭКРАН «ВЕРСТАК» РИСУЕТСЯ И ПОПАДАЕТСЯ ЛУЧОМ (правило §K «иконка = цель»).
        ///
        /// ДВЕ ПОМЕТКИ О ПРАВКЕ ЭТОГО ТЕСТА (25.10.2026, разрешение владельца — ответ «дерзай» на план
        /// §42; обе правки следуют за решением В1 «свои экраны» и ничего не ослабляют):
        ///
        ///   1) Панель у руки радиусом ~0,11 м вмещает ~7 иконок 30 мм, поэтому 23 вкладки «Верстака»
        ///      сразу в неё не влезают — ряд вкладок листается по 4 (см. `KvXrDesktopTabs`). Проверяются
        ///      «иконок = целей», ««Закрыть» на месте», «ряд влезает в окно» и НАЛИЧИЕ листания.
        ///
        ///   2) СЧЁТЧИК ПРОВЕРЯЕТСЯ ВМЕСТЕ С РЯДОМ ВКЛАДОК И ЛИСТАНИЕМ, а не «больше семи». Кнопки
        ///      КОМАНД вкладки берутся из реестра `KvCommands`, который наполняет менеджер интерфейса
        ///      (`KazistovVvUIManager.RegisterCommands`). В проверке менеджера нет — реестр пуст, и
        ///      кнопок команд не будет НИКОГДА, это ограничение среды, а не дефект шлема. В шлеме
        ///      команды регистрируются, и кнопки есть; это видно по журналу приёмки.
        /// </summary>
        [Test]
        public void Windows_TabStripIsHittable()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(out host);
            try
            {
                panel.OpenScreen(KvXrPanelLevel.WorkbenchScreen);

                Assert.AreEqual(panel.IconCount, panel.RayTargetCount,
                    "у «Верстака» иконок и целей луча обязано быть поровну (§K)");
                Assert.GreaterOrEqual(panel.IconCount, 6,
                    "ряд вкладок (4) + листание (2) — минимум шесть целей луча");
                Assert.GreaterOrEqual(IndexOf(panel, "Закрыть"), 0, "«Закрыть» на месте");

                Assert.IsTrue(KvXrDesktopTabs.Fits(KvXrPanelLevel.WorkbenchScreen,
                        KvXrGroupPanel.WindowPlateHalfSize.x),
                    "ряд вкладок обязан влезать в ширину окна");

                // листание: 23 вкладки при 4 слотах → 6 страниц, и их видно кнопками
                Assert.AreEqual(6, KvXrDesktopTabs.PageCount(KvXrDesktopTabs.CountOf(KvXrPanelLevel.WorkbenchScreen)),
                    "23 вкладки по 4 — шесть страниц");
                Assert.GreaterOrEqual(IndexOf(panel, "Вкладки: вперёд"), 0,
                    "кнопка листания вкладок обязана быть целью луча");

                // ключи вкладок — АНГЛИЙСКИЕ (по ним ищутся кнопки команд). Русская подпись ключом не была.
                Assert.AreEqual("waypoints", KvXrDesktopTabs.KeyOf(KvXrPanelLevel.WorkbenchScreen, 3),
                    "ключ вкладки «Точки» — waypoints, а не русская подпись");
                Assert.AreEqual("record", KvXrDesktopTabs.KeyOf(KvXrPanelLevel.FeaturesScreen, 0),
                    "ключ вкладки «Запись» — record");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 6. НОВЫЕ ЭКРАНЫ НЕ ТРОГАЮТ ЗАКРЕПЛЁННЫЙ РЯД ПАНЕЛИ.
        /// Ряд «8 групп + 5 экранов = 13 иконок» закреплён уже запускавшимся тестом
        /// (`GroupLevel_ScreenRowIsHittable`). Новые экраны открываются ИЗ ВЕЕРА, а ряд остаётся 13.
        /// </summary>
        [Test]
        public void Windows_ScreensDoNotCollideWithFixedRow()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(out host);
            try
            {
                panel.OpenScreen(KvXrPanelLevel.Groups);
                Assert.AreEqual(13, panel.IconCount, "ряд панели остаётся 13 иконок");
                Assert.AreEqual(panel.IconCount, panel.RayTargetCount, "иконок = целей");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ============================================================ 2. СТОЛЫ

        /// <summary>
        /// 7. В РАЗДЕЛЕ «СТОЛ» ДВЕ СТРОКИ СОЗДАНИЯ, И НАЗВАНИЯ РАЗЛИЧАЮТСЯ ([S1] №2, В2).
        /// </summary>
        [Test]
        public void Tables_ThereAreTwoKinds()
        {
            List<string> rows = KvXrStartMenuRows.Tables(0);

            Assert.GreaterOrEqual(rows.Count, 2, "строк создания стола минимум две");
            Assert.AreNotEqual(KvXrTableKinds.Label(KvXrTableKind.Square),
                               KvXrTableKinds.Label(KvXrTableKind.Auto),
                               "у двух видов столов разные названия");
            CollectionAssert.Contains(rows, KvXrTableKinds.Label(KvXrTableKind.Square), "строка квадратного стола");
            CollectionAssert.Contains(rows, KvXrTableKinds.Label(KvXrTableKind.Auto), "строка автостола");

            Assert.AreEqual(KvXrStartRowAction.CreateSquareTable,
                KvXrStartMenuRows.ActionFor(KvXrStartSection.Tables, 0, 0, 0), "первая строка создаёт квадратный");
            Assert.AreEqual(KvXrStartRowAction.CreateAutoTable,
                KvXrStartMenuRows.ActionFor(KvXrStartSection.Tables, 1, 0, 0), "вторая строка создаёт автостол");
        }

        /// <summary>
        /// 8. РАДИУС АВТОСТОЛА = 0,95 × ВЫЛЕТ + 0,10 м ([S1] №2, [S2] №3).
        /// Проверяются оба робота сцены: 2,368 м → 2,3496 м и 0,713 м → 0,7774 м.
        /// </summary>
        [Test]
        public void AutoTable_RadiusEqualsRobotReach()
        {
            Assert.AreEqual(2.3496f, KvReachMath.AutoRadius(2.368f), 0.001f,
                "6-осевой: 0,95 × 2,368 + 0,10 = 2,3496 м");
            Assert.AreEqual(0.7774f, KvReachMath.AutoRadius(0.713f), 0.001f,
                "SCARA: 0,95 × 0,713 + 0,10 = 0,7774 м");

            Assert.AreEqual(2.3496f, KvXrTableKinds.Radius(KvXrTableKind.Auto, 2.368f), 0.001f,
                "радиус автостола берётся из одной формулы");
        }

        /// <summary>
        /// 9. КВАДРАТНЫЙ СТОЛ ЗАМЕТНО МЕНЬШЕ АВТО ([S2] №2) — числом, а не словом.
        /// </summary>
        [Test]
        public void SquareTable_IsMuchSmallerThanAuto()
        {
            float squareDiagonal = KvXrTableKinds.SquareDiagonal;
            float autoDiameterSix = 2f * KvReachMath.AutoRadius(2.368f);
            float autoDiameterScara = 2f * KvReachMath.AutoRadius(0.713f);

            Assert.Less(squareDiagonal, autoDiameterSix,
                "диагональ квадратного стола меньше диаметра автостола 6-осевого");
            Assert.Less(squareDiagonal, autoDiameterScara,
                "диагональ квадратного стола меньше диаметра автостола SCARA");
            Assert.Greater(autoDiameterSix / squareDiagonal, 3f,
                "«заметно меньше» — это не меньше чем в 3 раза по диаметру");
        }

        /// <summary>
        /// 10. ЧИСЛА КВАДРАТНОГО СТОЛА — ТЕ, ЧТО ПОДТВЕРДИЛ ВЛАДЕЛЕЦ (В2): 0,72 × 0,48 м, верх 0,98 м.
        /// Заодно проверяется, что это ровно стол ПК, уменьшенный в 20 раз (пропорция сохранена).
        /// </summary>
        [Test]
        public void SquareTable_MatchesPcNumbers()
        {
            Assert.AreEqual(0.72f, KvXrTableKinds.SquareWidth, 0.0001f, "ширина 0,72 м");
            Assert.AreEqual(0.48f, KvXrTableKinds.SquareDepth, 0.0001f, "глубина 0,48 м");
            Assert.AreEqual(0.98f, KvXrTableKinds.TopHeight, 0.0001f, "верх стола 0,98 м");

            Assert.AreEqual(TrajectoryCore.StandBuilder.TableWidth / 20f, KvXrTableKinds.SquareWidth, 0.0001f,
                "ширина — та же, что у стола ПК, уменьшенная в 20 раз");
            Assert.AreEqual(TrajectoryCore.StandBuilder.TableDepth / 20f, KvXrTableKinds.SquareDepth, 0.0001f,
                "глубина — та же, что у стола ПК, уменьшенная в 20 раз");

            Assert.AreEqual(0.8653f, KvXrTableKinds.SquareDiagonal, 0.001f, "диагональ 0,87 м");
        }

        /// <summary>
        /// 11. ВЫЛЕТ РУКИ СЧИТАЕТСЯ ОДНОЙ ФОРМУЛОЙ: сумма расстояний между пивотами плюс отрезок до TCP.
        /// Тест держит формулу на контрольных числах (пивоты вдоль оси Z — длина очевидна).
        /// </summary>
        [Test]
        public void Reach_ChainLengthIsSumOfSegments()
        {
            var pivots = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 0.5f),
                new Vector3(0f, 0f, 1.0f),
                new Vector3(0f, 0f, 1.5f)
            };

            Assert.AreEqual(2.0f, KvReachMath.ChainLength(pivots, new Vector3(0f, 0f, 2f)), 0.0001f,
                "0,5 + 0,5 + 0,5 + 0,5 = 2,0 м");
            Assert.AreEqual(1.5f, KvReachMath.ChainLength(pivots, new Vector3(0f, 0f, 1.5f)), 0.0001f,
                "TCP совпал с последним пивотом — отрезок 0");
        }

        // ============================================================ 3. ПУСТО ПРИ ЗАПУСКЕ И МЕНЮ

        /// <summary>
        /// 12. ПРИЗНАК СТЕНДА СЦЕНЫ: включаются ровно два стола и оба робота, ничего лишнего.
        /// </summary>
        [Test]
        public void Scene_StandsAreRecognisedByName()
        {
            Assert.IsTrue(KvXrSceneEmpty.IsSceneStand("Стенд_1_Стол"), "стенд 1");
            Assert.IsTrue(KvXrSceneEmpty.IsSceneStand("Стенд_2_Стол"), "стенд 2");
            Assert.IsFalse(KvXrSceneEmpty.IsSceneStand("XrFloor"), "пол — не стенд");
            Assert.IsFalse(KvXrSceneEmpty.IsSceneStand("XrRig"), "риг — не стенд");
            Assert.IsFalse(KvXrSceneEmpty.IsSceneStand("KazistovVv_UI"), "интерфейс — не стенд");
        }

        /// <summary>
        /// 13. СТРОКА ЖУРНАЛА О ПУСТОЙ СЦЕНЕ НАЗЫВАЕТ ЧИСЛА ([S2] №4).
        /// </summary>
        [Test]
        public void Scene_EmptyLineHasNumbers()
        {
            string line = KvXrSceneEmpty.Describe(2, 2);
            StringAssert.Contains("2", line, "число столов и роботов в строке есть");
            StringAssert.Contains("стол", line.ToLowerInvariant(), "строка говорит про столы");
            StringAssert.Contains("робот", line.ToLowerInvariant(), "строка говорит про роботов");
        }

        /// <summary>
        /// 14. СТРОКИ МЕНЮ ВЕДУТ СЕБЯ СОГЛАСОВАННО: у каждой строки есть действие, и строки не
        /// выходят за потолок панели (`MaxContentRows`), иначе последняя молча не рисуется.
        /// </summary>
        [Test]
        public void Menu_StandAndRobotRowsMatchActions()
        {
            for (int existing = 0; existing <= 3; existing++)
            {
                List<string> tables = KvXrStartMenuRows.Tables(existing);
                Assert.LessOrEqual(tables.Count, KvXrStartMenuLayout.MaxContentRows,
                    "строк раздела «Стол» не больше потолка панели");

                for (int row = 0; row < tables.Count; row++)
                    Assert.AreNotEqual(KvXrStartRowAction.None,
                        KvXrStartMenuRows.ActionFor(KvXrStartSection.Tables, row, existing, 0),
                        "у строки «" + tables[row] + "» есть действие");
            }

            Assert.AreEqual(KvXrStartRowAction.CreateSixAxisRobot,
                KvXrStartMenuRows.ActionFor(KvXrStartSection.Robots, 0, 0, 0), "первая строка создаёт робота");
            Assert.AreEqual(KvXrStartRowAction.CreateScaraRobot,
                KvXrStartMenuRows.ActionFor(KvXrStartSection.Robots, 1, 0, 0), "вторая строка создаёт SCARA");
        }

        // ============================================================ 4. ПЯТНА, ГРАНИЦЫ, ЗОНЫ

        /// <summary>
        /// 15. ПРИЧИНА, ПО КОТОРОЙ ВИЗУАЛИЗАЦИИ НЕ ВИДНО, НАЗЫВАЕТСЯ СЛОВАМИ ([S2] №6).
        /// Главная причина, найденная 25.10.2026: в сцене шлема нет потока этапов, поэтому ни одна
        /// служба не привязывается, а тумблеры молча ничего не делают.
        /// </summary>
        [Test]
        public void Zones_HiddenVisualizerAlwaysNamesReason()
        {
            Assert.AreEqual(KvXrVisuals.ReasonNoFlow,
                KvXrVisuals.Reason(false, false, false, false, false), "нет потока — так и говорим");
            Assert.AreEqual(KvXrVisuals.ReasonNoRobot,
                KvXrVisuals.Reason(true, false, true, true, false), "поток есть, робот не привязан");
            Assert.AreEqual(KvXrVisuals.ReasonToggledOff,
                KvXrVisuals.Reason(true, true, false, true, false), "выключено тумблером");
            Assert.AreEqual(KvXrVisuals.ReasonNoGeometry,
                KvXrVisuals.Reason(true, true, true, false, false), "в этой позе показывать нечего");
            Assert.AreEqual(KvXrVisuals.ReasonDesktopCanvas,
                KvXrVisuals.Reason(true, true, true, true, true), "панель на погашенном канвасе");
            Assert.AreEqual(KvXrVisuals.ReasonVisible,
                KvXrVisuals.Reason(true, true, true, true, false), "видно — причина «видно»");
        }

        /// <summary>
        /// 16. СТРОКА-ДИАГНОСТИКА ЗОН СОДЕРЖИТ ЧИСЛО И ПРИЧИНУ ([S2] №6, дословный формат).
        /// </summary>
        [Test]
        public void Zones_DiagnosticLineHasNumbers()
        {
            string line = KvXrVisuals.Describe(3, KvXrVisuals.ReasonToggledOff);
            StringAssert.StartsWith("[KvXr] ЗОНЫ:", line, "строка начинается с метки");
            StringAssert.Contains("видимо 3", line, "число видимых есть");
            StringAssert.Contains("причина скрытия", line, "причина названа");
            StringAssert.Contains(KvXrVisuals.ReasonToggledOff, line, "причина названа словами");
        }

        /// <summary>
        /// 17. ПОДТВЕРЖДЕНИЕ ТОЧКИ КУРКОМ НЕ КОНФЛИКТУЕТ С НАЖАТИЕМ ИКОНКИ.
        /// Над интерфейсом нажатие остаётся нажатием; вне интерфейса — подтверждением точки.
        /// </summary>
        [Test]
        public void Flow_ConfirmIsNotFedOverInterface()
        {
            Assert.IsFalse(KvXrFlowBridge.ShouldFeedConfirm(true, true),
                "над панелью/окном/меню курок нажимает иконку, а не подтверждает точку");
            Assert.IsTrue(KvXrFlowBridge.ShouldFeedConfirm(false, true),
                "вне интерфейса курок подтверждает точку");
            Assert.IsFalse(KvXrFlowBridge.ShouldFeedConfirm(false, false),
                "без нажатия подтверждения нет");
        }

        /// <summary>
        /// 18. СТРОКА ЖУРНАЛА ПОТОКА НАЗЫВАЕТ РОБОТА И ЧИСЛО ВИДИМЫХ ВИЗУАЛИЗАЦИЙ.
        /// </summary>
        [Test]
        public void Flow_DescribeNamesRobotAndCount()
        {
            string line = KvXrFlowBridge.Describe(true, "6-осевой робот", 4);
            StringAssert.Contains("6-осевой робот", line, "робот назван");
            StringAssert.Contains("4", line, "число визуализаций есть");
        }

        // ============================================================ 5. ГОЛОС

        /// <summary>
        /// 19. БЕЗ appID ГОЛОС ЧЕСТНО ГОВОРИТ «НЕДОСТУПЕН» ([S2] №7).
        /// </summary>
        [Test]
        public void Voice_WithoutAppIdSaysUnavailable()
        {
            string line = KvXrVoiceStatus.Line(false, false);
            StringAssert.Contains("недоступен", line.ToLowerInvariant(), "слово «недоступен» есть");
            StringAssert.Contains("appid", line.ToLowerInvariant().Replace(" ", ""), "причина — нет appID");
            Assert.IsFalse(KvXrVoiceStatus.Available(false, false), "голос недоступен без appID");
            Assert.IsFalse(KvXrVoiceStatus.Available(true, false), "и без разрешения на микрофон тоже");
            Assert.IsTrue(KvXrVoiceStatus.Available(true, true), "с appID и микрофоном — доступен");
        }

        // ============================================================ 6. СЦЕНЫ НЕ ТРОНУТЫ

        /// <summary>
        /// 20. СЦЕНЫ ОСТАЛИСЬ БАЙТ В БАЙТ ([S3], [S2] №10).
        /// Сверяются те самые хеши, что записаны в `Docs\_hash_s42_start.txt` до начала работ.
        /// </summary>
        [Test]
        public void Scenes_UntouchedByThisWave()
        {
            const string mainSceneSha = "22AA6D2299161F0D6B0DE404F4571438D63323E2D428CE222F72FD815185B02C";
            const string xrMainSha = "7A8784F45F80C5405C5FD2BF6939D0768DCB503DF8836172C261BB21B1E5264B";

            Assert.AreEqual(mainSceneSha, Sha256Of("Assets/_Project/00_Scenes/MainScene.unity"),
                "MainScene не меняется этой волной");
            Assert.AreEqual(xrMainSha, Sha256Of("Assets/_Project/00_Scenes/XrMain.unity"),
                "XrMain не меняется этой волной");
        }

        /// <summary>SHA256 файла проекта (путь от корня проекта; в редакторе это текущий каталог).</summary>
        private static string Sha256Of(string relativePath)
        {
            string path = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Assert.IsTrue(System.IO.File.Exists(path), "файл не найден: " + path);

            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = System.IO.File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var text = new System.Text.StringBuilder(hash.Length * 2);
                foreach (byte b in hash) text.Append(b.ToString("X2"));
                return text.ToString();
            }
        }
    }
}
