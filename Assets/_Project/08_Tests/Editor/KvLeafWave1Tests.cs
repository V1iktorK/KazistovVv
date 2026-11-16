// ================================================================================================
//  KvLeafWave1Tests.cs — EditMode-тесты ВОЛНЫ 1 КАМПАНИИ «каждый листочек путеводителя закрыт».
//
//  ОТКУДА ЗАДАЧА. `Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv` (554 листа) вёл список незакрытых листьев.
//  Волна 1 берёт 15 листьев со статусом «дефект» (13) и «заглушка/дефект» (2):
//
//    слайд 141  ArUiCanvas / PcToolbar/Tool_<id> (13)          — вызов команды реестра
//    слайд 141  ArUiCanvas / PcCommandPanel/GroupTab_<группа>  — открывает панель команд группы
//    слайд 142  ArUiCanvas / ArMessageCancel «ОТМЕНА»          — отменяет действие плашки
//    слайд 151  ArUiCanvas / ArButtons / ArButton_Команды      — открывает/закрывает панель
//    слайд 153  ArUiCanvas / ArMessage / ArMessageButton       — закрывает окно и выполняет действие
//    слайд 171  CmdPrev/CmdNext, группы robot/path/post/view/export/ui/misc (7 листьев)
//    слайд 193  KvArCameraGateCanvas / ArMessageButton         — сообщение «Установить ARCore…»
//    слайд 207  Реестр KvCommands / Визуализация / tool.measure
//    слайд 219  Реестр KvCommands / Прочее / calib.tab
//
//  ЧЕМ ПРОВЕРЯЕТСЯ (P4-3). ТОЛЬКО ПОВЕДЕНИЕМ живого кода: кнопки нажимаются их же `onClick`,
//  состояние читается геттерами самого интерфейса, журнал — из `KvActionLog.Entries`. Тексты
//  исходников файлы тестов НЕ читают: храповик в гейте считает такие файлы и не даёт набору
//  расти. Ни один тест не открывает сцену, не запускает игру и не ходит в сеть.
//
//  ГРАНИЦА ЧЕСТНОСТИ. Самопрогон на планшете (`Tools/ui-sweep-device.ps1`) в этой волне НЕ
//  запускался (устройства не трогаем), поэтому листья закываются здесь — в EditMode, на той же
//  логике, что в приложении.
//
//  ЧИСТКА. `TearDown` возвращает пустую сцену: хосты тестов, окно сообщений, канвас AR и
//  EventSystem снимаются всегда, даже когда тест упал.
// ================================================================================================

using System;
using System.Collections.Generic;
using KazistovVvAr;
using KazistovVvDiagnostics;
using KazistovVvFeatures;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvTests
{
    /// <summary>Волна 1: 15 листьев «дефект»/«заглушка-дефект» из Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvLeafWave1Tests
    {
        // ------------------------------------------------------------------ инфраструктура

        /// <summary>Хосты, созданные тестом: снимаются в TearDown независимо от исхода.</summary>
        private readonly List<GameObject> hosts = new List<GameObject>();

        /// <summary>Было ли отсечение повторов включено до теста (возвращается в TearDown).</summary>
        private bool repeatsWereOn;

        [SetUp]
        public void SetUp()
        {
            // ОТСЕЧЕНИЕ ПОВТОРОВ МЕШАЕТ ПРОВЕРКЕ СОСТОЯНИЯ: ключ журнала вырезает числа, поэтому
            // «первая точка 0,00» и «первая точка 3,00» — одно событие, и второе не пишется.
            // Проверяемая ложь была бы не поведением, а правилом журнала.
            repeatsWereOn = KvActionLog.Instance.suppressRepeats;
            KvActionLog.Instance.suppressRepeats = false;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject host in hosts)
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            hosts.Clear();

            // Канвас AR и EventSystem создаёт `KvArUi.EnsureBuilt` — без снятия они переживут
            // тест и попадут в следующую фикстуру, где уже будут чужими.
            foreach (string name in new[] { "ArUiCanvas", "ArEventSystem" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);
            }

            KvActionLog.Instance.suppressRepeats = repeatsWereOn;
        }

        /// <summary>ХОСТ С КОМПОНЕНТОМ (имя — с префиксом, чтобы TearDown находил его).</summary>
        private GameObject NewHost(string name)
        {
            var host = new GameObject("KvLeafWave1_" + name);
            hosts.Add(host);
            return host;
        }

        /// <summary>ПОСТРОЕННЫЙ AR-ИНТЕРФЕЙС (в EditMode `Start` не вызывается — строим сами).</summary>
        private KvArUi BuildUi()
        {
            KvArUi ui = NewHost("ui").AddComponent<KvArUi>();
            ui.EnsureBuilt();
            Assert.IsTrue(ui.Built, "интерфейс планшета не построился — проверять нечего");
            return ui;
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

        // ============================================ 1. ТУЛБАР: ПРОИНАДЛЕЖНОСТЬ ПАНЕЛЯМ ПРОЕКТА

        /// <summary>
        /// ЛИСТ 141 «PcToolbar/Tool_<id> (13)» — 13 кнопок тулбара вызывают команды реестра.
        ///
        /// ОТКУДА ДЕФЕКТ. Единственная кнопка, которую самопрогон НЕ нажал в принципе, —
        /// `Tool_ui.settings«Настройки»`: правило «системный пункт» сработало по подписи и
        /// посчитало кнопку посторонней (`exempt=вне панелей проекта: возможно системный экран`).
        /// По коду это кнопка проекта: `Execute("ui.settings") → ShowSettings(0)` — вкладка
        /// штатной панели «Настройки и справка» (ДЕРЕВЬЯ_КНОПОК_AR §4.5).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Признак «своя панель» у AR-канваса: тулбар планшета принадлежит
        /// приложению, а предполётная заставка `KvArCameraGateCanvas` (её кнопка реально уводит
        /// в настройки Android) — нет. Один признак на оба случая оставлял бы либо непроверенной
        /// кнопку проекта, либо нажатой кнопку, уводящую из приложения.
        /// </summary>
        [Test]
        public void Toolbar_ArUiCanvasIsProjectPanel_GateCanvasIsNot()
        {
            var arRoot = new GameObject("KvLeafWave1_ArRuntime");
            hosts.Add(arRoot);

            var canvas = new GameObject("ArUiCanvas");
            canvas.transform.SetParent(arRoot.transform, false);
            canvas.AddComponent<KvArUi>();            // принадлежность важна, построение — нет
            var toolbarButton = new GameObject("Tool_ui.settings");
            toolbarButton.transform.SetParent(canvas.transform, false);

            Assert.IsTrue(KvUiSweepRuntime.IsProjectOwned(toolbarButton.transform),
                "тулбар планшета не считается панелью приложения — «Настройки» снова не нажмутся, " +
                "и единственная непроверенная кнопка тулбара останется без проверки");

            var gate = new GameObject("KvArCameraGateCanvas");
            gate.transform.SetParent(arRoot.transform, false);
            var gateButton = new GameObject("ОТКРЫТЬ НАСТРОЙКИ");
            gateButton.transform.SetParent(gate.transform, false);

            Assert.IsFalse(KvUiSweepRuntime.IsProjectOwned(gateButton.transform),
                "предполётная заставка камеры посчитана панелью приложения — её кнопка уводит " +
                "в настройки Android, и прогон повёл бы приложение из себя");
        }

        // ================================================== 2. ВКЛАДКИ ГРУПП ПАНЕЛИ КОМАНД

        /// <summary>
        /// ЛИСТ 141 «PcCommandPanel/GroupTab_<группа> (8)» — вкладка ОТКРЫВАЕТ панель команд
        /// своей группы и НЕ закрывает её.
        ///
        /// ОТКУДА ДЕФЕКТ. Самопрогон нажал только `GroupTab_robot` и записал остальные семь
        /// «панель так и не открылась» (ДЕРЕВЬЯ_КНОПОК_AR §4.7). Панель в тот момент была уже
        /// открыта той же группой, а повторное нажатие вкладки по коду ВЫЗЫВАЛО `ClosePanels()` —
        /// вкладка закрывала окно, вместо того чтобы переключить группу. Назначение листа —
        /// «открывает панель команд группы», поэтому закрытие по вкладке — нарушение листа.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. ВСЕ восемь групп: вкладка открывает панель, заголовок называет
        /// группу, счётчик страниц совпадает с числом команд (`ceil(N/8)`), на странице не
        /// больше восьми строк, и повторное нажатие той же вкладки панель НЕ закрывает.
        /// </summary>
        [Test]
        public void GroupTabs_EveryGroupOpensItsPanel_AndRepeatPressNeverClosesIt()
        {
            KvArUi ui = BuildUi();
            KvToolbarGroup[] groups = KvToolbarGroups.All;
            Assert.GreaterOrEqual(groups.Length, 8, "в панели команд обязано быть 8 групп");

            foreach (KvToolbarGroup group in groups)
            {
                Button tab = ui.UiButton("PcCommandPanel/GroupTab_" + group.Id);
                Assert.IsNotNull(tab, "нет вкладки группы «" + group.Id + "»");

                tab.onClick.Invoke();
                Assert.IsTrue(ui.CommandPanelVisible,
                    "вкладка «" + group.Id + "» не открыла панель команд");
                int pages = Mathf.Max(1, Mathf.CeilToInt(group.Commands.Length / 8f));
                Assert.AreEqual("1 / " + pages, ui.CommandPageLabel,
                    "группа «" + group.Id + "»: неверный счётчик страниц");
                StringAssert.Contains(group.Title, ui.CommandTitleLabel,
                    "заголовок панели не называет группу: «" + ui.CommandTitleLabel + "»");
                Assert.Greater(ui.CommandRowCount, 0,
                    "группа «" + group.Id + "» не показала ни одной строки команд");
                Assert.LessOrEqual(ui.CommandRowCount, 8,
                    "группа «" + group.Id + "» показала больше восьми строк на странице");

                // ПОВТОРНОЕ НАЖАТИЕ ТОЙ ЖЕ ВКЛАДКИ: вкладка ПЕРЕКЛЮЧАЕТ группу, а не закрывает окно.
                tab.onClick.Invoke();
                Assert.IsTrue(ui.CommandPanelVisible,
                    "вкладка «" + group.Id + "» закрыла панель команд — лист «открывает панель " +
                    "команд группы» нарушен");
                Assert.AreEqual("1 / " + pages, ui.CommandPageLabel,
                    "группа «" + group.Id + "»: после повторного нажатия счётчик страниц сбился");
            }
        }

        // ============================================================ 3. «ОТМЕНА» ПОДТВЕРЖДЕНИЯ

        /// <summary>
        /// ЛИСТ 142 «ArMessageCancel «ОТМЕНА»» — отменяет действие плашки.
        ///
        /// ОТКУДА ДЕФЕКТ. Поле `messageCancelAction` присваивалось в `ShowConfirm`, а обработчик
        /// `OnMessageCancelClicked` его игнорировал: окно закрывалось, но СВОЁ действие отмены
        /// не выполнялось (ДЕРЕВЬЯ_КНОПОК_AR §4.1, пункт 2 — «сейчас эквивалентно, при
        /// расширении станет дефектом»). Самопрогон кнопку не нажимал вовсе.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Нажатие «ОТМЕНА»: выполняется действие ОТМЕНЫ, действие
        /// подтверждения НЕ выполняется, окно закрывается.
        /// </summary>
        [Test]
        public void MessageCancel_RunsCancelAction_DiscardsConfirm_AndClosesWindow()
        {
            KvArUi ui = BuildUi();
            bool confirmed = false;
            bool cancelled = false;
            ui.ShowConfirm("Очистить рабочее пространство?", "ОЧИСТИТЬ",
                delegate { confirmed = true; },
                delegate { cancelled = true; });

            Button cancel = ui.UiButton("ArMessage/ArMessageCancel");
            Assert.IsNotNull(cancel, "кнопки «ОТМЕНА» нет в окне подтверждения");
            Assert.IsTrue(cancel.gameObject.activeInHierarchy,
                "в подтверждении «ОТМЕНА» скрыта — опасному действию нечего остановить");

            cancel.onClick.Invoke();

            Assert.IsTrue(cancelled, "«ОТМЕНА» не выполнила действие отмены — отмены нет");
            Assert.IsFalse(confirmed, "подтверждение выполнилось, хотя нажали «ОТМЕНА»");
            Assert.IsFalse(ui.MessageVisible, "окно подтверждения не закрылось по «ОТМЕНА»");
        }

        // ================================== 4. КНОПКА «КОМАНДЫ»: ПЕРЕКЛЮЧАТЕЛЬ С ИНДИКАЦИЕЙ

        /// <summary>
        /// ЛИСТ 151 «ArButtons / ArButton_Команды» — открывает панель команд; второе нажатие
        /// закрывает её (переключатель, KvArUi.OpenCommands).
        ///
        /// ОТКУДА ДЕФЕКТ. Замер самопрогона давал только подсветку, а второе нажатие МОЛЧА
        /// закрывало окно: «переключатель без индикации» (ДЕРЕВЬЯ_КНОПОК_AR §2.1.4). Оператор
        /// не видел ни того, что панель открыта, ни того, что нажатие её закрыло.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Первое нажатие открывает панель и оставляет видимую строку
        /// «Панель команд открыта», второе закрывает и оставляет «Панель команд закрыта».
        /// </summary>
        [Test]
        public void CommandsButton_TogglesPanel_AndLeavesVisibleIndicationEachTime()
        {
            KvArUi ui = BuildUi();
            // Кнопку в приложении создаёт KvArRuntime.BuildScreenButtons тем же вызовом.
            Button commands = ui.AddButton("Команды", delegate { ui.OpenCommands("robot"); });
            Assert.IsNotNull(commands, "кнопка «Команды» не создалась в правом ряду");

            commands.onClick.Invoke();
            Assert.IsTrue(ui.CommandPanelVisible, "«Команды» не открыла панель команд");
            StringAssert.Contains("Панель команд открыта", ui.HintText,
                "открытие панели не оставлено видимым: «" + ui.HintText + "»");

            commands.onClick.Invoke();
            Assert.IsFalse(ui.CommandPanelVisible, "повторное «Команды» не закрыло панель");
            StringAssert.Contains("Панель команд закрыта", ui.HintText,
                "закрытие панели не оставлено видимым: «" + ui.HintText + "»");
        }

        // ======================================================= 5. КНОПКА ОКНА СООБЩЕНИЙ

        /// <summary>
        /// ЛИСТ 153 «ArMessage / ArMessageButton» — «закрывает окно и выполняет действие».
        ///
        /// ОТКУДА ДЕФЕКТ. Кнопка показывалась только когда переданы И подпись, И действие;
        /// без действия она пряталась вместе с «ОТМЕНА», и окно оставалось открытым навсегда —
        /// закрыть его было нечем (KvArUi.ShowMessage, KvArBoot/KvArSessionController зовут
        /// `ShowMessage(текст)`). Назначение листа при этом — «закрывает окно».
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Сообщение с подписью, но без действия: кнопка видна, нажатие
        /// ЗАКРЫВАЕТ окно (действие выполняется, если оно есть).
        /// </summary>
        [Test]
        public void MessageButton_WithLabelButNoAction_IsVisible_AndClosesWindow()
        {
            KvArUi ui = BuildUi();
            ui.ShowMessage("Сообщение без действия", "ПОНЯТНО", null);

            Assert.IsTrue(ui.MessageButtonVisible,
                "кнопка окна скрыта — окно нечем закрыть, оно останется висеть поверх интерфейса");

            Button button = ui.UiButton("ArMessage/ArMessageButton");
            Assert.IsNotNull(button, "кнопки сообщения нет в окне");
            button.onClick.Invoke();

            Assert.IsFalse(ui.MessageVisible, "нажатие кнопки сообщения не закрыло окно");
        }

        // ============================================ 6–12. ПАГИНАЦИЯ СТРОК КОМАНД (7 ЛИСТОВ)

        /// <summary>
        /// ЛИСТЫ 171 «CmdPrev/CmdNext» — группы robot, path, post, view, export, ui, misc.
        ///
        /// ОТКУДА ДЕФЕКТ (по коду, не по замеру). Позиция пагинации была константой
        /// `y = −4 − 12·48 = −580`, а шаг строк считался от экрана. На планшете 2560×1600
        /// `rowPitch ≈ 74`, восьмая строка занимает `−522…−586` — кнопки страниц НАЕЗЖАЛИ на
        /// последнюю строку (ДЕРЕВЬЯ_КНОПОК_ПК/AR §2.3). Самопрогон их и не видел: под кнопками
        /// физически лежала строка команды.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Раскладка для геометрии планшета: верх кнопок страниц НИЖЕ дна
        /// восьмой строки, и кнопки не уходят за низ панели. Проверяется для КАЖДОЙ группы с
        /// листанием — это ровно семь листов этого слайда.
        /// </summary>
        [Test]
        public void Pagination_LayoutSitsBelowLastRow_AndFitsPanel_AllMultiPageGroups()
        {
            // Геометрия планшета 2560×1600 (замер в ДЕРЕВЬЯ_КНОПОК_AR §2.3): масштаб CanvasScaler
            // ≈ 1.403 → канвас ≈ 1140 ед.; строка под палец 74 ед.; свободное место под строки
            // 807 ед. (канвас − каркас − вкладки − строка состояния).
            const float rowsSpace = 807f;
            const float touchRow = 74f;
            const int pageSize = 8;

            int multiPageGroups = 0;
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
            {
                int pages = Mathf.Max(1, Mathf.CeilToInt(group.Commands.Length / (float)pageSize));
                if (pages < 2) continue;
                multiPageGroups++;

                float rowPitch;
                float rowHeight;
                float pagerY;
                KvArUi.CommandRowsLayout(rowsSpace, touchRow, pageSize, true,
                                         out rowPitch, out rowHeight, out pagerY);

                float lastRowBottom = -4f - (pageSize - 1) * rowPitch - rowHeight;
                Assert.LessOrEqual(pagerY, lastRowBottom,
                    "группа «" + group.Id + "» (" + pages + " стр.): кнопки страниц наезжают на " +
                    "восьмую строку — pagerY " + pagerY.ToString("0") + " против дна строки " +
                    lastRowBottom.ToString("0"));
                Assert.GreaterOrEqual(pagerY - KvArUi.PagerHeightUi, -rowsSpace,
                    "группа «" + group.Id + "»: кнопки страниц уходят за низ панели команд");
            }

            Assert.GreaterOrEqual(multiPageGroups, 7,
                "в реестре меньше семи групп с листанием — проверка не про те листья");
        }

        // ==================================== 13. СООБЩЕНИЕ ОБ ОТКАЗЕ УСТАНОВКИ ARCORE

        /// <summary>
        /// ЛИСТ 193 «ArMessageButton на сообщении “Установить ARCore не удалось”».
        ///
        /// ОТКУДА ДЕФЕКТ. `ARSession.Install()` бросил исключение → `ui.ShowMessage(текст)` БЕЗ
        /// подписи и БЕЗ действия: `ShowMessage` прятал и основную кнопку, и «ОТМЕНА», то есть
        /// окно было нечем закрыть (нет тапа вне окна, нет Escape) — ловушка из
        /// ДЕРЕВЬЯ_КНОПОК_AR §2.5. Текст сообщения вынесен в константу `ArCoreInstallFailedText`,
        /// чтобы проверка листа и код не могли разойтись.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Сообщение без подписи и действия обязано иметь подписанную кнопку,
        /// и нажатие обязано закрыть окно.
        /// </summary>
        [Test]
        public void ArCoreInstallFailure_MessageWithoutAction_HasClosableButton()
        {
            KvArUi ui = BuildUi();
            ui.ShowMessage(KvArSessionController.ArCoreInstallFailedText);

            Assert.IsTrue(ui.MessageVisible, "сообщение об отказе не показано");
            Assert.IsTrue(ui.MessageButtonVisible,
                "сообщение без подписи и действия не имеет кнопки — окно нечем закрыть, " +
                "оператор остаётся перед текстом без выхода");
            StringAssert.Contains("ЗАКРЫТЬ", ui.MessageButtonLabel,
                "кнопка сообщения без действия должна быть подписана, а не пустой: «" +
                ui.MessageButtonLabel + "»");

            Button button = ui.UiButton("ArMessage/ArMessageButton");
            Assert.IsNotNull(button, "кнопки сообщения нет в окне");
            button.onClick.Invoke();
            Assert.IsFalse(ui.MessageVisible, "сообщение об отказе не закрылось по кнопке");
        }

        // ==================================================== 14. КОМАНДА ИЗМЕРЕНИЯ (ЛИНЕЙКА)

        /// <summary>
        /// ЛИСТ 207 «Реестр KvCommands / Визуализация / tool.measure» (заглушка/дефект).
        ///
        /// ОТКУДА ДЕФЕКТ. Назначение листа: состояние «две точки» — первое нажатие запоминает
        /// точку, второе считает и печатает `KvMeasureKit.Describe`, ТРЕТИЙ удар в ту же точку
        /// (`sqrMagnitude < 1e-8`) снимает замер с честным сообщением «замер отменён». В коде
        /// после удачного замера состояние СБРАСЫВАЛОСЬ, и третий удар начинал НОВЫЙ замер
        /// («первая точка …») — отмены по назначению листа не существовало.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Цепочка из четырёх нажатий: запомнить → посчитать → отменить тем же
        /// ударом → начать заново. Плюс отказ без рига назван словами, а не «ноль метров».
        /// </summary>
        [Test]
        public void Measure_ThirdHitOnSamePoint_CancelsWithHonestMessage()
        {
            FeatureHub hub = NewHost("measure").AddComponent<FeatureHub>();  // Awake не зовётся
            int before = LogCount();

            hub.MeasureAt(new Vector3(0f, 0f, 0f));      // первая точка
            hub.MeasureAt(new Vector3(1f, 0f, 0f));      // замер
            hub.MeasureAt(new Vector3(1f, 0f, 0f));      // третий удар в ту же точку — отмена
            hub.MeasureAt(new Vector3(3f, 0f, 0f));      // после отмены — новая первая точка

            List<string> lines = LogSince(before);
            Assert.AreEqual(4, lines.Count,
                "линейка написала " + lines.Count + " строк вместо четырёх: " +
                string.Join(" | ", lines.ToArray()));
            StringAssert.Contains("первая точка", lines[0], "первое нажатие не запомнило точку");
            StringAssert.Contains("1.000 м", lines[1],
                "второе нажатие не посчитало расстояние: «" + lines[1] + "»");
            StringAssert.Contains("замер отменён", lines[2],
                "третий удар в ту же точку не снял замер — назначение листа не выполнено: «" +
                lines[2] + "»");
            StringAssert.Contains("первая точка", lines[3],
                "после отмены линейка не начала новый замер: «" + lines[3] + "»");

            // Попадание луча обязательно: без рига — честный отказ, а не «ноль метров».
            Vector3 aim;
            Assert.IsFalse(hub.TryAim(out aim),
                "без рига прицел обязан не попадать — иначе замер считал бы из начала координат");
        }

        // =========================================== 15. ВКЛАДКА КАЛИБРОВКИ (calib.tab)

        /// <summary>
        /// ЛИСТ 219 «Реестр KvCommands / Прочее / calib.tab» (заглушка/дефект).
        ///
        /// ОТКУДА ДЕФЕКТ. Обработчик `KvStageHub3.OpenTab` при отсутствии окна верстака ВОЗВРАЩАЛСЯ
        /// МОЛЧА (`if (KvWorkbenchWindow.Instance == null) return;`) — нажатие не давало ни
        /// результата, ни объяснения (ДЕРЕВЬЯ_КНОПОК_КОМАНДЫ, карточка `calib.tab`: «тихий
        /// отказ»). По принятому принципу строка обязана либо сработать, либо объясниться.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Нажатие без окна верстака оставляет в журнале причину с названием
        /// окна — тихого выхода больше нет.
        /// </summary>
        [Test]
        public void CalibTab_MissingWorkbench_LogsHonestReasonInsteadOfSilentExit()
        {
            KvStageHub3 hub = NewHost("stage3").AddComponent<KvStageHub3>();  // Awake не зовётся
            int before = LogCount();

            hub.OpenTab("calibration");

            List<string> lines = LogSince(before);
            Assert.Greater(lines.Count, 0,
                "вкладка калибровки молча вышла, когда окна верстака нет — нажатие без причины");
            bool explained = false;
            foreach (string line in lines)
            {
                if (line.IndexOf("верстак", StringComparison.OrdinalIgnoreCase) >= 0)
                    explained = true;
            }
            Assert.IsTrue(explained,
                "причина не названа окном верстака: " + string.Join(" | ", lines.ToArray()));
        }
    }
}
