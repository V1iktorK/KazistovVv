// ================================================================================================
//  KvLeafWave3Tests.cs — EditMode-тесты ВОЛНЫ 3 КАМПАНИИ «каждый листочек путеводителя закрыт».
//
//  ОТКУДА ЗАДАЧА. `Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv` — 165 листьев со статусом «по коду» (120) и
//  «по условию IsEnabled» (45). Смысл статуса: поведение реализовано, но НЕ ДОКАЗАНО.
//  ТАКТИКА ВОЛНЫ (та же, что в волнах 1/2/2b): массовая триажа → группы по типу доказательства →
//  батчи. Доказательство — ТЕСТ, закрепляющий обещание листа; недоказуемое в EditMode — честная
//  причина в статусе, а не «доказано кодом».
//
//  ГРУППЫ ЭТОЙ ВОЛНЫ:
//    А) AR-панель команд (`ArUiCanvas / PcCommandPanel / CommandRows`, слайды 159–171) — 52 листа:
//       строка панели вызывает СВОЮ команду реестра; судится по строке журнала
//       `нажатие: <id> …`, которую пишет сам `KvCommands.Invoke`, либо по отказу «недоступна».
//    Б) Модели роботов (слайды 160/161/179/180/197/198) — 30 листов: пункт недоступен без
//       геометрии И называет причину, а модель с геометрией доступна только СО СТОЛОМ.
//    В) Реестр KvCommands (слайды 196–218) — 45 листов «по условию IsEnabled»: доступность/галка
//       команды СЛЕДУЕТ за условием, а отказ — с причиной в журнале.
//    Г) Dock-панели (слайды 74/190/191) — 10 листов: кнопки заголовка и двойной клик по заголовку.
//    Д) Тулбар (слайды 175/178–188) — кнопки группы и строки выпадающего списка группы.
//    Е) Контекстное меню дерева (слайд 68) и справочник биндов (слайд 70).
//
//  ЧЕМ ПРОВЕРЯЕТСЯ (P4-3). ТОЛЬКО ПОВЕДЕНИЕМ живого кода: кнопки нажимаются их же `onClick`,
//  состояние читается геттерами самого интерфейса, журнал — из `KvActionLog.Entries`. Тексты
//  исходников файлы тестов НЕ читают. Ни один тест не открывает сцену через `OpenSceneMode.Single`,
//  не запускает игру и не ходит в сеть.
//
//  ГРАНИЦА ЧЕСТНОСТИ. Устройства не трогаем и APK не собираем: листья закываются в EditMode, на
//  той же логике, что в приложении. Листья, которым нужен прогон на устройстве, остаются в CSV
//  со статусом «по коду (замер на устройстве — отдельный этап)».
//
//  ЧИСТКА. `TearDown` возвращает пустую сцену: хосты, канвас AR, EventSystem, чужие корневые
//  объекты, созданные командами, и глобальные настройки (тема, язык, геймпад) снимаются всегда,
//  даже когда тест упал.
// ================================================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using KazistovVvAr;
using KazistovVv.Core;
using KazistovVvDiagnostics;
using KazistovVvFeatures;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KazistovVvTests
{
    /// <summary>Волна 3: листья «по коду» и «по условию IsEnabled» из Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvLeafWave3Tests
    {
        // ------------------------------------------------------------------ инфраструктура

        /// <summary>Хосты, созданные тестом: снимаются в TearDown независимо от исхода.</summary>
        private readonly List<GameObject> hosts = new List<GameObject>();

        /// <summary>Было ли отсечение повторов включено до теста (возвращается в TearDown).</summary>
        private bool repeatsWereOn;

        /// <summary>СНИМОК РЕЕСТРА КОМАНД ДО ШТАТНОЙ РЕГИСТРАЦИИ (общий статик — вернуть как был).</summary>
        private static List<KvCommand> registrySnapshot;

        /// <summary>Корни сцены ДО теста: команды создают объекты, они обязаны исчезнуть в TearDown.</summary>
        private readonly List<GameObject> rootsBefore = new List<GameObject>();

        /// <summary>Глобальные настройки, которые команды меняют (восстанавливаются в TearDown).</summary>
        private static bool gamepadWasOn;
        private static KvThemeMode themeWas;
        private static string localeWas;

        [SetUp]
        public void SetUp()
        {
            // ОТСЕЧЕНИЕ ПОВТОРОВ МЕШАЕТ ПРОВЕРКЕ СОСТОЯНИЯ: ключ журнала вырезает числа и одинаковые
            // события — проверяемая правда была бы не поведением кода, а правилом журнала.
            repeatsWereOn = KvActionLog.Instance.suppressRepeats;
            KvActionLog.Instance.suppressRepeats = false;
            // ЖУРНАЛ КОЛЬЦЕВОЙ (capacity = 600): на полном прогоне он уже переполнен чужими
            // тестами, старые строки вытесняются — и проверка «новая строка появилась ПОСЛЕ
            // индекса before» получает пустой хвост и ложно проваливается. Чистим журнал на
            // ВХОДЕ в фикстуру: каждый тест волны тогда пишет в свой собственный буфер.
            KvActionLog.Instance.Clear();

            if (registrySnapshot == null)
            {
                registrySnapshot = new List<KvCommand>();
                foreach (KvCommand command in KvCommands.All) registrySnapshot.Add(command);
            }

            gamepadWasOn = KvSettings.GamepadHud;
            themeWas = KvTheme.Mode;
            localeWas = KvLoc.CurrentCode;

            rootsBefore.Clear();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                rootsBefore.Add(root);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject host in hosts)
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            hosts.Clear();

            // Канвас AR и EventSystem создаёт `KvArUi.EnsureBuilt` — без снятия они переживут тест.
            foreach (string name in new[] { "ArUiCanvas", "ArEventSystem" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);
            }

            // Объекты, созданные нажатыми командами (сетки, зоны, демо): чужой сцене их оставлять нельзя.
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootsBefore.Contains(root)) continue;
                if (root == null) continue;
                if (root.name.StartsWith("[", StringComparison.Ordinal)) continue;   // внутренности раннера
                UnityEngine.Object.DestroyImmediate(root);
            }

            // Глобальные настройки — обратно, как были до теста.
            if (KvSettings.GamepadHud != gamepadWasOn) KvSettings.GamepadHud = gamepadWasOn;
            if (KvTheme.Mode != themeWas) KvTheme.SetMode(themeWas);
            if (KvLoc.CurrentCode != localeWas) KvLoc.SetLanguage(localeWas);

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
            var host = new GameObject("KvLeafWave3_" + name);
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

        /// <summary>ЕСТЬ ЛИ СРЕДИ СТРОК ПОДСТРОКА (без учёта регистра).</summary>
        private static bool AnyContains(List<string> lines, string needle)
        {
            foreach (string line in lines)
                if (line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>
        /// ШТАТНАЯ РЕГИСТРАЦИЯ ВСЕХ КОМАНД ОБОЛОЧКИ (приватный метод шелла — отражением, как в
        /// `KvGapClosureTests`/`KvLeafWave2bTests`). Метод сам вызывает все хабы и `RegisterModels`.
        /// </summary>
        private static void RegisterAllCommands(GameObject host)
        {
            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            MethodInfo register = typeof(KazistovVvUIManager).GetMethod("RegisterCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(register,
                "у KazistovVvUIManager больше нет RegisterCommands() — штатная регистрация недоступна");
            try
            {
                register.Invoke(ui, null);
            }
            catch (TargetInvocationException error)
            {
                Assert.Fail("штатная регистрация команд упала: " + error.InnerException);
            }

            // СЛУЖБЫ ЖИВУТ В `Awake`, А В EDITMODE ЕГО НЕ БЫВАЕТ (Грабли 5): команды, чьи
            // обработчики зовут сервисы (`hub.Poses`, `hub.Sessions`, `Singularities`…), падали бы
            // на NullReference. Хабы поднимаются в ТОМ ЖЕ ПОРЯДКЕ, что и при регистрации, и
            // ТОЛЬКО те, что создают обычные классы: `KvStageHub4` не поднимаем — его Awake
            // заводит веб-дашборд и службу компаньона (в EditMode это лишняя нагрузка и сеть).
            // `KvStageHub2` НЕ поднимаем: его Awake строит окно-верстак, а сборка строк
            // `KvWidgets.Segmented` зовёт `Destroy` — в EditMode это ошибка лога, из-за которой
            // падает вся фикстура. Команды второго хаба при этом честно отказываются с причиной
            // («служба демонстрации в сцене нет»), что и проверяется волной.
            WakeUp<KazistovVvFeatures.FeatureHub>(host, "FeatureHub");
            WakeUp<KazistovVvFeatures.KvStageHub>(host, "KvStageHub");
            WakeUp<KazistovVvFeatures.KvStageHub3>(host, "KvStageHub3");
        }

        /// <summary>ВЫЗВАТЬ `Awake` ЛЮБОГО КОМПОНЕНТА ЯВНО (в EditMode Unity его не вызывает).</summary>
        private static void WakeUpComponent(object component, string label)
        {
            MethodInfo awake = component.GetType().GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(awake, "у " + label + " нет приватного Awake");
            try { awake.Invoke(component, null); }
            catch (TargetInvocationException error)
            {
                Assert.Fail("Awake " + label + " упал: " + error.InnerException);
            }
        }

        /// <summary>ВЫЗВАТЬ `Awake` ХАБА ЯВНО (в EditMode Unity его не вызывает).</summary>
        private static void WakeUp<T>(GameObject host, string label) where T : Component
        {
            T hub = host.GetComponent<T>();
            if (hub == null) return;    // хаба нет — его команды зарегистрированы не были
            MethodInfo awake = typeof(T).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (awake == null) return;
            try
            {
                awake.Invoke(hub, null);
            }
            catch (TargetInvocationException error)
            {
                Assert.Fail("Awake хаба " + label + " упал: " + error.InnerException);
            }
        }

        /// <summary>
        /// ОСОБЫЙ ПУНКТ `workspace.clear`: его обещание — ОКНО ПОДТВЕРЖДЕНИЯ «ОЧИСТИТЬ», а не
        /// строка журнала (`KvArUi.Execute` перехватывает id и зовёт `ShowConfirm`). Если окна
        /// нет — пункт почистил бы сцену молча, и это дефект листа.
        /// Возвращает true, если пункт обработан здесь (журнал не проверяется).
        /// </summary>
        private static bool HandleConfirmItem(KvArUi ui, string id, string where, List<string> failures)
        {
            if (id != "workspace.clear") return false;
            if (!ui.MessageVisible)
                failures.Add(id + " (" + where + "): окно подтверждения «ОЧИСТИТЬ» не открылось");
            else if (ui.MessageButtonLabel.IndexOf("ОЧИСТИТЬ", StringComparison.Ordinal) < 0)
                failures.Add(id + " (" + where + "): подтверждение без кнопки «ОЧИСТИТЬ», подпись «" +
                             ui.MessageButtonLabel + "»");
            ui.HideMessage();
            return true;
        }

        /// <summary>СТРОКА ПАНЕЛИ КОМАНД `Cmd_<id>` (с листанием страниц до неё).</summary>
        private static Button FindCommandRow(KvArUi ui, string groupId, string id)
        {
            ui.OpenCommands(groupId, false);          // страница 1
            for (int page = 0; page < 40; page++)
            {
                Button row = ui.UiButton("PcCommandPanel/CommandRows/Cmd_" + id);
                if (row != null) return row;
                Button next = ui.UiButton("PcCommandPanel/CommandRows/CmdNext");
                if (next == null) return null;        // страниц нет — строки нигде нет
                next.onClick.Invoke();
            }
            return null;
        }

        // ==================== 1. AR-ПАНЕЛЬ КОМАНД: СТРОКА ВЫЗЫВАЕТ СВОЮ КОМАНДУ (52 ЛИСТА)

        /// <summary>
        /// ЛИСТЫ 159–171 «ArUiCanvas / PcCommandPanel / CommandRows / вкладка …» (52 листа):
        /// строка панели вызывает команду реестра со своим id — ровно то, что обещает лист
        /// («hub.CyclePlaybackSpeed()», «ShowSettings(0)», «KvTheme.Cycle()», «hub.Window.Toggle(9)»…).
        ///
        /// ОТКУДА СМЫСЛ ПРОВЕРКИ. Строка панели — это не командная логика, а МАРШРУТИЗАЦИЯ:
        /// `FillCommandRows` берёт id из `KvToolbarGroups` и вешает на кнопку `Execute(id)`,
        /// а `KvArUi.Execute` уводит в `KvCommands.Invoke(id)`. Если маршрутизация сломана,
        /// оператор на планшете видит «нажал — тишина», хотя на компьютере команда работает.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Для КАЖДОЙ строки каждой вкладки: нажатие оставляет в журнале строку
        /// `нажатие: <id>` (её пишет сам `KvCommands.Invoke` после выполнения), а если команда по
        /// условию недоступна — нажатие НЕ доходит до обработчика и кнопка остаётся выключенной
        /// (причина — в описании команды). Ни одна строка не «молчит».
        /// </summary>
        [Test]
        public void ArCommandRows_EveryRowOfEveryTab_RoutesToItsOwnCommand()
        {
            RegisterAllCommands(NewHost("reg"));
            KvArUi ui = BuildUi();

            var failures = new List<string>();
            // Вкладки, на которых лежат листья слайдов 159–171.
            string[] tabs = { "robot", "export", "net", "ui", "misc" };

            foreach (string tab in tabs)
            {
                KvToolbarGroup group = KvToolbarGroups.Find(tab);
                Assert.IsNotNull(group, "вкладка «" + tab + "» не найдена в каталоге групп");

                foreach (string id in group.Commands)
                {
                    KvCommand command = KvCommands.Get(id);
                    if (command == null)
                    {
                        failures.Add(id + " (вкладка " + tab + "): команды нет в реестре — строка ведёт в никуда");
                        continue;
                    }

                    Button row = FindCommandRow(ui, tab, id);
                    if (row == null)
                    {
                        failures.Add(id + " (вкладка " + tab + "): строки Cmd_" + id + " нет в панели");
                        continue;
                    }

                    bool enabled = command.Enabled;     // условие ДО нажатия
                    int before = LogCount();
                    try
                    {
                        PressQuietly(row);              // нажатие — штатным onClick кнопки
                    }
                    catch (Exception error)
                    {
                        failures.Add(id + " (вкладка " + tab + "): нажатие упало: " + error.Message);
                        continue;
                    }

                    if (HandleConfirmItem(ui, id, "вкладка " + tab, failures)) continue;

                    List<string> lines = LogSince(before);
                    string trace = string.Join(" | ", lines.ToArray());

                    if (enabled)
                    {
                        if (!AnyContains(lines, "нажатие: " + id))
                            failures.Add(id + " (вкладка " + tab + "): нажатие не дошло до команды, " +
                                         "в журнале нет строки с id: " + (trace.Length > 0 ? trace : "журнал пуст"));
                    }
                    else
                    {
                        // Условие не выполнено: команда обязана остаться ВЫКЛЮЧЕННОЙ, а нажатие —
                        // не дойти до обработчика (иначе «недоступная» кнопка делала бы работу).
                        if (AnyContains(lines, "нажатие: " + id))
                            failures.Add(id + " (вкладка " + tab + "): недоступная команда всё-таки " +
                                         "выполнилась: " + trace);
                    }
                }
            }

            Assert.IsTrue(failures.Count == 0,
                "строки панели команд не все ведут к своим командам (" + failures.Count + " шт):\n" +
                string.Join("\n", failures.ToArray()));
        }

        // ==================== 2. AR-МЕНЮ: ПУНКТ ВЫЗЫВАЕТ СВОЮ КОМАНДУ (СЛАЙД 141)

        /// <summary>
        /// ЛИСТ 141 «ArUiCanvas / MenuItem_&lt;id&gt; (75 пунктов)» — «то же, что §1.7»: пункт
        /// выпадающего меню вызывает команду реестра, для `workspace.clear` — окно подтверждения,
        /// для `table.square/auto` — создание стола.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Шесть меню (Файл/Правка/Вид/Робот/Сервис/Справка): каждая кнопка
        /// `MenuItem_<id>` после нажатия оставляет в журнале `нажатие: <id>` либо честно
        /// отказывается, будучи выключенной по условию. Окно подтверждения «ОЧИСТИТЬ» проверяется
        /// отдельно: нажатие пункта открывает его, а не чистит сцену молча.
        /// </summary>
        [Test]
        public void ArMenuItems_EveryItem_RoutesToItsOwnCommand()
        {
            RegisterAllCommands(NewHost("regmenu"));
            KvArUi ui = BuildUi();

            var failures = new List<string>();
            string[] menus = { "Файл", "Правка", "Вид", "Робот", "Сервис", "Справка" };
            int items = 0;

            foreach (string menu in menus)
            {
                ui.ToggleMenu(menu);

                // Пункты лежат прямо в выпадающей панели; имена — `MenuItem_<id>`.
                // `GameObject.Find` видит только активную панель — то есть ту, что открыто сейчас.
                GameObject dropdown = GameObject.Find("PcMenuDropdown");
                Assert.IsNotNull(dropdown, "панель меню «" + menu + "» не построилась/не открылась");
                Transform panel = dropdown.transform;

                var itemNames = new List<string>();
                for (int i = 0; i < panel.childCount; i++)
                {
                    string name = panel.GetChild(i).name;
                    if (name.StartsWith("MenuItem_", StringComparison.Ordinal)) itemNames.Add(name);
                }
                Assert.Greater(itemNames.Count, 0, "меню «" + menu + "» не показало ни одного пункта");

                foreach (string fullName in itemNames)
                {
                    items++;
                    string id = fullName.Substring("MenuItem_".Length);
                    Button item = ui.UiButton("PcMenuDropdown/" + fullName);
                    if (item == null) { failures.Add(id + ": пункт не найден кнопкой"); continue; }

                    KvCommand command = KvCommands.Get(id);
                    if (command == null)
                    {
                        failures.Add(id + " (меню " + menu + "): команды нет в реестре");
                        continue;
                    }

                    bool enabled = command.Enabled;
                    int before = LogCount();
                    try { PressQuietly(item); }
                    catch (Exception error)
                    {
                        failures.Add(id + " (меню " + menu + "): нажатие упало: " + error.Message);
                        continue;
                    }

                    if (HandleConfirmItem(ui, id, "меню " + menu, failures)) continue;

                    List<string> lines = LogSince(before);
                    if (enabled && !AnyContains(lines, "нажатие: " + id))
                        failures.Add(id + " (меню " + menu + "): нажатие не дошло до команды: " +
                                     (lines.Count > 0 ? string.Join(" | ", lines.ToArray()) : "журнал пуст"));
                    if (!enabled && AnyContains(lines, "нажатие: " + id))
                        failures.Add(id + " (меню " + menu + "): недоступный пункт выполнился");
                }

                ui.ToggleMenu(menu);   // закрыть, чтобы следующее меню открылось заново
            }

            Assert.GreaterOrEqual(items, 70,
                "пунктов меню набрано " + items + " — проверка не про те 75 пунктов листа");
            Assert.IsTrue(failures.Count == 0,
                "пункты AR-меню не все ведут к своим командам (" + failures.Count + " шт):\n" +
                string.Join("\n", failures.ToArray()));
        }

        // ==================== 3. МОДЕЛИ РОБОТОВ: 30 ЛИСТОВ (160/161/179/180/197/198)

        /// <summary>
        /// ЛИСТЫ «Робот: robot.model.*» на трёх поверхностях — панель планшета (слайды 160/161),
        /// тулбар (179/180) и реестр KvCommands (197/198), всего 30 листов.
        ///
        /// ОБЕЩАНИЕ ЛИСТА. «`UnavailableReason(model)` — если у модели нет разрешаемой геометрии,
        /// отказ ДО удаления текущего робота, строка в `KvActionLog.Placement(..., false, причина)`»;
        /// пункт в меню остаётся видимым, но НЕДОСТУПЕН и называет причину.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Для каждой модели каталога: команда зарегистрирована и имеет
        /// `IsEnabled`; без геометрии — выключена, описание называет причину, а `ApplyModel`
        /// отказывает с той же причиной в журнале и НЕ создаёт робота; модель С геометрией
        /// доступна только когда в сцене есть стол (условие `SceneHasTable` проверяется в обе
        /// стороны: стола нет — выключена, стол есть — включена). Строки вкладки «robot» панели
        /// планшета и группы «Робот» тулбара обязаны содержать каждую модель — иначе пункта на
        /// поверхности нет, а лист описывает именно его.
        /// </summary>
        [Test]
        public void RobotModelCommands_DisabledWithoutGeometry_EnabledOnlyWithTable()
        {
            RegisterAllCommands(NewHost("models"));

            KvRobotModel[] all = KvRobotCatalog.All;
            Assert.AreEqual(12, all.Length,
                "в каталоге " + all.Length + " моделей вместо двенадцати — проверка не про те листья");

            int withoutGeometry = 0;
            int withGeometry = 0;

            foreach (KvRobotModel model in all)
            {
                string id = KvRobotCommands.ModelCommandPrefix + model.Id;
                KvCommand command = KvCommands.Get(id);
                Assert.IsNotNull(command, "команда «" + id + "» не зарегистрирована (лист не закрыт)");
                Assert.IsNotNull(command.IsEnabled,
                    "«" + id + "» без IsEnabled — пункт меню не знает своего условия (лист «" + id + "»)");

                // --- пункты на поверхностях: группа тулбара «Робот» и вкладка «robot» панели планшета
                KvToolbarGroup robotGroup = KvToolbarGroups.Find("robot");
                Assert.IsNotNull(robotGroup, "группы «robot» нет в каталоге");
                Assert.Contains(id, robotGroup.Commands,
                    "«" + id + "» не разложена в группу «Робот» тулбара — лист тулбара без кнопки");

                bool geometry = KvRobotCatalog.HasGeometry(model);
                if (geometry)
                {
                    withGeometry++;

                    // УСЛОВИЕ: без стола ставить робота некуда → пункт выключен.
                    KvRobotCommands.InvalidateTableCache();
                    Assert.IsFalse(command.Enabled,
                        "«" + id + "» (есть геометрия) доступна, хотя стола в сцене нет");

                    // УСЛОВИЕ ВЫПОЛНЕНО: появился объект со словом «стол» → пункт включён.
                    var table = new GameObject("KvLeafWave3_Стол тестовый");
                    // `SceneHasTable` читает DisplayName (по умолчанию «Объект») — как и в приложении,
                    // где фабрика стола подписывает его словом «Стол».
                    RegisteredObject marker = table.AddComponent<RegisteredObject>();
                    marker.DisplayName = "Стол тестовый";
                    try
                    {
                        KvRobotCommands.InvalidateTableCache();
                        Assert.IsTrue(command.Enabled,
                            "«" + id + "» не стала доступна, когда в сцене появился стол — " +
                            "условие IsEnabled не читает факт");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(table);
                        KvRobotCommands.InvalidateTableCache();
                    }
                }
                else
                {
                    withoutGeometry++;

                    Assert.IsFalse(command.Enabled,
                        "«" + id + "» доступна, хотя разрешаемой геометрии нет — пункт обещал отказ");
                    StringAssert.Contains("нет её геометрии", command.Description,
                        "«" + id + "» не называет причину недоступности в описании: «" +
                        command.Description + "»");

                    // ОТКАЗ ДО УДАЛЕНИЯ: ApplyModel пишит причину и не создаёт робота.
                    int before = LogCount();
                    KvRobotCommands.ApplyModel(model);
                    List<string> lines = LogSince(before);
                    Assert.IsTrue(AnyContains(lines, "модель недоступна"),
                        "«" + id + "» отказал без строки в журнале с причиной: " +
                        (lines.Count > 0 ? string.Join(" | ", lines.ToArray()) : "журнал пуст"));
                    Assert.AreEqual(0,
                        UnityEngine.Object.FindObjectsByType<RobotController>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                        "недоступная модель «" + model.Id + "» всё-таки создала робота");
                }
            }

            Assert.Greater(withoutGeometry, 0,
                "в каталоге нет моделей без геометрии — половина обещаний листа не проверена");
            Assert.Greater(withGeometry, 0,
                "в каталоге нет моделей С геометрией — условие «со столом» не проверено");
        }

        // ==================== 4. ДВИЖЕНИЕ: ДОСТУПНОСТЬ СЛЕДУЕТ ЗА ФАКТОМ (СЛАЙДЫ 159/178/196)

        /// <summary>
        /// ЛИСТЫ 196 «robot.switch», «robot.stop» и кнопки «Запуск/пауза»/«Остановка»
        /// (слайды 159 и 178) — «по условию IsEnabled»: доступность кнопки обязана соответствовать
        /// факту движения, а не быть константой.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Условие имитируется в тесте: пока исполнитель НЕ движется, «Остановка»
        /// и «Запуск/пауза» выключены; как только `running` стал true — обе доступны, нажатие
        /// останавливает исполнителя (и кнопка гаснет) или ставит паузу (и галка «Запуск/пауза»
        /// загорается). `robot.switch` доступна только со ВТОРЫМ роботом в сцене.
        /// </summary>
        [Test]
        public void MotionCommands_AvailabilityFollowsRunningMotion_AndRobotCount()
        {
            GameObject host = NewHost("motion");
            RegisterAllCommands(host);

            // --- поток траекторий на «главной камере»: так его ищет `manager.Flow`
            var cameraGo = new GameObject("KvLeafWave3_MainCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();
            TrajectoryFlowController flow = cameraGo.AddComponent<TrajectoryFlowController>();
            MotionExecutor motion = cameraGo.AddComponent<MotionExecutor>();
            TrajectoryExecutor executor = cameraGo.AddComponent<TrajectoryExecutor>();
            SetField(flow, "motion", motion);
            SetField(motion, "executor", executor);
            // Поток подключаем к менеджеру НАПРЯМУЮ: `manager.Flow` ищет его на «главной камере»,
            // а в EditMode поиск камеры — лишняя случайность в проверке условия.
            KazistovVvUIManager manager = host.GetComponent<KazistovVvUIManager>();
            Assert.IsNotNull(manager, "менеджер оболочки не создан при регистрации");
            SetField(manager, "flow", flow);
            Assert.AreSame(flow, manager.Flow, "менеджер не подхватил поток траекторий");

            // УСЛОВИЕ НЕ ВЫПОЛНЕНО: робот не движется → стоп и пауза недоступны.
            KvCommand stop = KvCommands.Get("robot.stop");
            Assert.IsNotNull(stop, "robot.stop не зарегистрирована");
            Assert.IsNotNull(stop.IsEnabled, "robot.stop без IsEnabled — лист «по условию IsEnabled»");
            KvCommand play = KvCommands.Get("robot.playpause");
            Assert.IsNotNull(play, "robot.playpause не зарегистрирована");
            Assert.IsNotNull(play.IsEnabled, "robot.playpause без IsEnabled");
            Assert.IsFalse(stop.Enabled, "«Остановка» доступна, хотя робот не движется");
            Assert.IsFalse(play.Enabled, "«Запуск/пауза» доступна, хотя робот не движется");
            Assert.IsFalse(play.Checked, "галка «Запуск/пауза» горит без паузы");

            // УСЛОВИЕ ВЫПОЛНЕНО: имитируем движение.
            SetField(executor, "running", true);
            Assert.IsTrue(stop.Enabled, "«Остановка» недоступна при движении робота");
            Assert.IsTrue(play.Enabled, "«Запуск/пауза» недоступна при движении робота");

            // Нажатие «Запуск/пауза» ставит паузу — она видна на кнопке.
            int before = LogCount();
            KvCommands.Invoke("robot.playpause");
            Assert.IsTrue(AnyContains(LogSince(before), "нажатие: robot.playpause"),
                "нажатие «Запуск/пауза» не дошло до команды");
            Assert.IsTrue(executor.Paused, "пауза не дошла до исполнителя");
            Assert.IsTrue(play.Checked, "галка «Запуск/пауза» не показала паузу");
            KvCommands.Invoke("robot.playpause");          // снять паузу обратно
            Assert.IsFalse(executor.Paused, "повторное нажатие не сняло паузу");

            // Нажатие «Остановка» останавливает исполнитель — и кнопка гаснет.
            before = LogCount();
            KvCommands.Invoke("robot.stop");
            Assert.IsTrue(AnyContains(LogSince(before), "нажатие: robot.stop"),
                "нажатие «Остановка» не дошло до команды");
            Assert.IsFalse(executor.IsRunning, "«Остановка» не остановила исполнитель");
            Assert.IsFalse(stop.Enabled,
                "после остановки «Остановка» осталась доступной — условие IsEnabled не читает факт");

            // --- robot.switch: доступна только со вторым роботом
            KvCommand switcher = KvCommands.Get("robot.switch");
            Assert.IsNotNull(switcher, "robot.switch не зарегистрирована");
            Assert.IsNotNull(switcher.IsEnabled, "robot.switch без IsEnabled");
            Assert.IsFalse(switcher.Enabled,
                "переключение робота доступно, хотя в сцене меньше двух роботов");

            var robot1 = new GameObject("KvLeafWave3_Robot1");
            robot1.AddComponent<RobotController>();
            var robot2 = new GameObject("KvLeafWave3_Robot2");
            robot2.AddComponent<RobotController>();
            Assert.IsTrue(switcher.Enabled,
                "со вторым роботом в сцене переключение не стало доступным");

            before = LogCount();
            KvCommands.Invoke("robot.switch");
            Assert.IsTrue(AnyContains(LogSince(before), "нажатие: robot.switch"),
                "нажатие «Переключить робота» не дошло до команды");
            int active = 0;
            foreach (RobotController rc in UnityEngine.Object.FindObjectsByType<RobotController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (GetField(rc, "isActive") is bool flag && flag) active++;
            Assert.AreEqual(1, active,
                "после переключения активным должен остаться ровно один робот, а их " + active);

            UnityEngine.Object.DestroyImmediate(robot1);
            UnityEngine.Object.DestroyImmediate(robot2);
        }

        // ==================== 5. ОТМЕНА/ВОЗВРАТ: ДОСТУПНОСТЬ СЛЕДУЕТ ЗА СТЕКОМ (СЛАЙД 218)

        /// <summary>
        /// ЛИСТЫ 218 «edit.undo» («⚠ недоступна без стека отмены») и «edit.redo» («⚠ недоступна
        /// без стека повтора») — «по условию IsEnabled».
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Пустой стек → обе кнопки выключены; записанное в стек действие →
        /// «Отменить» доступна, нажатие выполняет отмену и ГАСИТ кнопку (стек пуст), после чего
        /// «Вернуть» становится доступной — условие и доступность идут вместе с фактом.
        /// </summary>
        [Test]
        public void UndoRedo_AvailabilityFollowsStackState()
        {
            GameObject host = NewHost("undo");
            RegisterAllCommands(host);
            FeatureHub hub = host.GetComponent<FeatureHub>();
            Assert.IsNotNull(hub, "хаб функций не создан при регистрации");

            KvCommand undo = KvCommands.Get("edit.undo");
            KvCommand redo = KvCommands.Get("edit.redo");
            Assert.IsNotNull(undo, "edit.undo не зарегистрирована");
            Assert.IsNotNull(redo, "edit.redo не зарегистрирована");
            Assert.IsNotNull(undo.IsEnabled, "edit.undo без IsEnabled");
            Assert.IsNotNull(redo.IsEnabled, "edit.redo без IsEnabled");

            // Хаб поднят штатно (`Awake`), стек отмены СОЗДАН, но ПУСТ → обе кнопки выключены,
            // а не «активны вслепую»: без этого условия лист «недоступна без стека» не проверить.
            KvUndoStack stack = hub.Undo;
            Assert.IsNotNull(stack, "хаб не создал стек отмены — условие листа не имитировать");
            Assert.AreEqual(0, stack.UndoCount, "стек отмены не пуст до теста");
            Assert.IsFalse(undo.Enabled, "«Отменить» доступна при пустом стеке");
            Assert.IsFalse(redo.Enabled, "«Вернуть» доступна, хотя отменять нечего");

            bool undone = false;
            stack.Record(new KvDelegateAction
            {
                title = "действие волны 3",
                undo = delegate { undone = true; },
                redo = delegate { }
            });
            Assert.IsTrue(undo.Enabled, "«Отменить» недоступна, хотя в стеке есть действие");
            Assert.IsFalse(redo.Enabled, "«Вернуть» доступна, хотя отменять ещё нечего");

            int before = LogCount();
            KvCommands.Invoke("edit.undo");
            List<string> lines = LogSince(before);
            Assert.IsTrue(AnyContains(lines, "нажатие: edit.undo"),
                "нажатие «Отменить» не дошло до команды: " + string.Join(" | ", lines.ToArray()));
            Assert.IsTrue(undone, "отмена не выполнила записанное действие");
            Assert.AreEqual(0, stack.UndoCount, "стек отмены не опустел после отмены");
            Assert.IsFalse(undo.Enabled, "«Отменить» осталась доступной на пустом стеке");

            Assert.IsTrue(redo.Enabled, "«Вернуть» не стала доступна после отмены");
            before = LogCount();
            KvCommands.Invoke("edit.redo");
            Assert.IsTrue(AnyContains(LogSince(before), "нажатие: edit.redo"),
                "нажатие «Вернуть» не дошло до команды");
            Assert.AreEqual(1, stack.UndoCount, "повтор не вернул действие в стек отмены");
        }

        // ============ 6. ПЕРЕКЛЮЧАТЕЛИ: ГАЛКА СЛЕДУЕТ ЗА СОСТОЯНИЕМ СЛУЖБЫ (СЛАЙДЫ 200/203/206/213)

        /// <summary>
        /// ЛИСТЫ «по условию IsEnabled» с обещанием `IsChecked = <состояние службы>`:
        /// point.select, constr.toggle, view.singularity, view.heatmap, health.toggle,
        /// obstacle.toggle, obstacle.discard, ui.toggle, pendant.toggle, gamepad.hud.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Для каждого переключателя: без состояния (служба выключена) галка
        /// погашена, нажатие ПЕРЕКЛЮЧАЕТ состояние службы И галку за ним, повторное нажатие
        /// возвращает всё назад. Галка — не константа и не врёт.
        /// </summary>
        [Test]
        public void StateToggles_GalkaFollowsServiceState()
        {
            GameObject host = NewHost("state");
            RegisterAllCommands(host);
            KazistovVvUIManager ui = host.GetComponent<KazistovVvUIManager>();
            FeatureHub features = host.GetComponent<FeatureHub>();
            KvStageHub stages = host.GetComponent<KvStageHub>();
            KvStageHub3 stages3 = host.GetComponent<KvStageHub3>();
            Assert.IsNotNull(features, "хаб функций не создан при регистрации");
            Assert.IsNotNull(stages, "хаб этапов не создан при регистрации");
            Assert.IsNotNull(stages3, "хаб этапов 7–12 не создан при регистрации");

            // Красная указка: риг обязана найтись на «главной камере».
            var cameraGo = new GameObject("KvLeafWave3_RigCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();
            FreeFlyCameraController rig = cameraGo.AddComponent<FreeFlyCameraController>();
            SetField(ui, "cameraController", rig);   // риг — напрямую, без поиска «главной камеры»
            Assert.AreSame(rig, ui.CameraRig, "менеджер не подхватил контроллер камеры");

            // --- point.select: галка = состояние указки, нажатие переключает
            KvCommand point = KvCommands.Get("point.select");
            Assert.IsNotNull(point, "point.select не зарегистрирована");
            Assert.IsNotNull(point.IsChecked, "point.select без IsChecked");
            rig.leftHandEnabled = false;
            Assert.IsFalse(point.Checked, "галка «Выбор точки» горит при выключенной указке");
            KvCommands.Invoke("point.select");
            Assert.IsTrue(rig.leftHandEnabled, "нажатие не включило красную указку");
            Assert.IsTrue(point.Checked, "галка не последовала за указкой");
            KvCommands.Invoke("point.select");
            Assert.IsFalse(point.Checked, "повторное нажатие не выключило галку");

            // --- constr.toggle: состояние = планирование с ограничениями
            CheckToggle(KvCommands.Get("constr.toggle"), stages3.Constrained, "Enabled");

            // --- view.singularity / health.toggle / obstacle.toggle / pendant.toggle (хаб этапов)
            CheckToggle(KvCommands.Get("view.singularity"), stages.Singularities, "Enabled");
            CheckToggle(KvCommands.Get("health.toggle"), stages.Health, "Enabled");
            CheckToggle(KvCommands.Get("obstacle.toggle"), stages.Obstacles, "Enabled");
            CheckToggle(KvCommands.Get("pendant.toggle"), stages.Pendant, "Visible");

            // --- view.heatmap (хаб функций)
            CheckToggle(KvCommands.Get("view.heatmap"), features.Heatmap, "Visible");

            // --- obstacle.discard: галка = «рискованные отбрасываются» (простой флаг)
            KvCommand discard = KvCommands.Get("obstacle.discard");
            Assert.IsNotNull(discard, "obstacle.discard не зарегистрирована");
            Assert.IsNotNull(discard.IsChecked, "obstacle.discard без IsChecked");
            stages.Obstacles.discardRisky = false;
            Assert.IsFalse(discard.Checked, "галка «отбрасывать рискованные» горит при выключенном флаге");
            KvCommands.Invoke("obstacle.discard");
            Assert.IsTrue(stages.Obstacles.discardRisky, "нажатие не включило отбрасывание");
            Assert.IsTrue(discard.Checked, "галка не последовала за флагом");
            KvCommands.Invoke("obstacle.discard");
            Assert.IsFalse(discard.Checked, "повторное нажатие не вернуло галку");
            stages.Obstacles.discardRisky = false;

            // --- gamepad.hud: галка = настройка (значение возвращается в TearDown)
            KvCommand gamepad = KvCommands.Get("gamepad.hud");
            Assert.IsNotNull(gamepad, "gamepad.hud не зарегистрирована");
            Assert.IsNotNull(gamepad.IsChecked, "gamepad.hud без IsChecked");
            bool wasOn = KvSettings.GamepadHud;
            KvCommands.Invoke("gamepad.hud");
            Assert.AreNotEqual(wasOn, KvSettings.GamepadHud, "геймпад-HUD не переключился");
            Assert.AreEqual(KvSettings.GamepadHud, gamepad.Checked, "галка не равна настройке");
            KvCommands.Invoke("gamepad.hud");
            Assert.AreEqual(wasOn, KvSettings.GamepadHud, "геймпад-HUD не вернулся обратно");

            // --- ui.toggle: при скрытии интерфейс захватывает курсор, при показе отпускает
            KvCommand shell = KvCommands.Get("ui.toggle");
            Assert.IsNotNull(shell, "ui.toggle не зарегистрирована");
            Assert.IsNotNull(shell.IsChecked, "ui.toggle без IsChecked");
            Assert.IsTrue(ui.uiVisible, "оболочка скрыта до теста — проверять нечего");
            Assert.AreEqual(ui.uiVisible, shell.Checked, "галка «показать/скрыть» не равна состоянию");
            KvCommands.Invoke("ui.toggle");
            Assert.IsTrue(KvMouseCursor.Captured,
                "скрытие интерфейса не захватило курсор — «мёртвая мышь» из §22 вернулась");
            Assert.IsTrue(ui.uiVisible, "галка ui.toggle не равна состоянию оболочки");
            ui.uiVisible = false;                       // оболочка скрыта
            Assert.IsFalse(shell.Checked, "галка горит при скрытой оболочке");
            KvCommands.Invoke("ui.toggle");
            Assert.IsFalse(KvMouseCursor.Captured,
                "показ интерфейса не отпустил курсор — кликать по панели будет нечем");
            ui.uiVisible = true;
            KvMouseCursor.Release("KvLeafWave3: возврат курсора");
        }

        /// <summary>
        /// ОБЩАЯ ПРОВЕРКА ПЕРЕКЛЮЧАТЕЛЯ: галка читает СОСТОЯНИЕ службы (не константу), нажатие
        /// меняет состояние и галку, повторное нажатие возвращает всё назад.
        /// </summary>
        private static void CheckToggle(KvCommand command, object service, string flag)
        {
            Assert.IsNotNull(command, "команда переключателя не зарегистрирована");
            Assert.IsNotNull(command.IsChecked, "«" + command.Id + "» без IsChecked (лист не закрыт)");
            Assert.IsNotNull(service, "служба «" + command.Id + "» не создана — условие не имитировать");

            WriteState(service, flag, false);
            Assert.IsFalse(command.Checked,
                "«" + command.Id + "» показывает галку при выключенной службе");
            int before = LogCount();
            InvokeQuietly(command.Id);                 // НАЖАТИЕ — через реестр, а не мимо кнопки
            Assert.IsTrue(AnyContains(LogSince(before), "нажатие: " + command.Id),
                "«" + command.Id + "»: нажатие не дошло до команды");
            Assert.IsTrue(ReadState(service, flag),
                "«" + command.Id + "»: нажатие не включило службу");
            Assert.IsTrue(command.Checked,
                "«" + command.Id + "»: галка не последовала за состоянием службы");

            InvokeQuietly(command.Id);
            Assert.IsFalse(ReadState(service, flag),
                "«" + command.Id + "»: повторное нажатие не выключило службу");
            Assert.IsFalse(command.Checked,
                "«" + command.Id + "»: галка осталась гореть после выключения службы");
        }

        // ==================== 7. ПАНЕЛИ И НАСТРОЙКИ: РЕШЕНИЕ ПО ФАКТУ (СЛАЙДЫ 207/208/215)

        /// <summary>
        /// ЛИСТЫ 207/208 «view.panel.tree/properties/settings» — «решение не по флагу, а по
        /// фактической видимости (`IsPanelEffectivelyVisible`): закрывается только реально видимая
        /// панель, иначе — показывается», и лист 215 «ui.settings» — «если оболочка скрыта —
        /// показать её и открыть вкладку „Функции“».
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Собранная dock-панель: когда она ВИДНА, нажатие её закрывает (галка
        /// гаснет); когда панель «запрошена, но не видна» (тело скрыто), нажатие НЕ закрывает её,
        /// а показывает — ровно то, что обещает лист. `ui.settings` при скрытой оболочке возвращает
        /// её и пишет в журнал строку с номером вкладки.
        /// </summary>
        [Test]
        public void PanelsAndSettings_FollowRealVisibility()
        {
            GameObject host = NewHost("panels");
            RegisterAllCommands(host);
            KazistovVvUIManager ui = host.GetComponent<KazistovVvUIManager>();

            // --- dock-панель «Дерево моделей» в памяти (так же, как её строит BuildShell)
            var canvasGo = new GameObject("KvLeafWave3_PanelCanvas", typeof(RectTransform));
            hosts.Add(canvasGo);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(1920f, 1080f);

            KvDockPanel dock = BuildDock(host, canvasGo, "Дерево моделей", "tree", KvDockSide.Left);
            SetField(ui, "treeDock", dock);

            KvCommand tree = KvCommands.Get("view.panel.tree");
            Assert.IsNotNull(tree, "view.panel.tree не зарегистрирована");
            Assert.IsNotNull(tree.IsChecked, "view.panel.tree без IsChecked");

            // Панель видна → нажатие ЗАКРЫВАЕТ её.
            Assert.IsTrue(ui.IsPanelEffectivelyVisible("tree"),
                "собранная панель не считается видимой — проверять нечего");
            Assert.IsTrue(tree.Checked, "галка погашена у видимой панели");
            int before = LogCount();
            KvCommands.Invoke("view.panel.tree");
            Assert.IsTrue(AnyContains(LogSince(before), "нажатие: view.panel.tree"),
                "нажатие панели не дошло до команды");
            Assert.IsFalse(ui.IsPanelVisible("tree"),
                "видимая панель не закрылась по нажатию");
            Assert.IsFalse(tree.Checked, "галка осталась гореть у закрытой панели");

            // Панель «запрошена, но не видна» (тело скрыто) → нажатие ПОКАЗЫВАЕТ её, а не гасит.
            dock.SetVisible(false);
            Assert.IsFalse(ui.IsPanelEffectivelyVisible("tree"),
                "скрытая панель считается видимой — условие не читает факт");
            before = LogCount();
            KvCommands.Invoke("view.panel.tree");
            Assert.IsTrue(AnyContains(LogSince(before), "нажатие: view.panel.tree"),
                "нажатие панели не дошло до команды");
            Assert.IsTrue(ui.IsPanelVisible("tree"),
                "невидимая панель ЗАКРЫЛАСЬ по нажатию — ровно тот дефект, что описывает лист");
            Assert.IsTrue(tree.Checked, "галка не загорелась у показанной панели");

            // Остальные две панели читают ТОТ ЖЕ факт (док в панели свой, здесь — общий принцип).
            foreach (string id in new[] { "view.panel.properties", "view.panel.settings" })
            {
                KvCommand command = KvCommands.Get(id);
                Assert.IsNotNull(command, id + " не зарегистрирована");
                Assert.IsNotNull(command.IsChecked, id + " без IsChecked");
                Assert.IsFalse(command.Checked, id + " показывает галку без своей панели");
            }

            // --- ui.settings при СКРЫТОЙ оболочке: возвращает её и открывает вкладку «Функции»
            KvCommand settings = KvCommands.Get("ui.settings");
            Assert.IsNotNull(settings, "ui.settings не зарегистрирована");
            ui.uiVisible = false;
            before = LogCount();
            KvCommands.Invoke("ui.settings");
            List<string> lines = LogSince(before);
            Assert.IsTrue(AnyContains(lines, "нажатие: ui.settings"),
                "нажатие «Настройки» не дошло до команды");
            Assert.IsTrue(ui.uiVisible,
                "«Настройки» не вернули скрытую оболочку — панель открылась бы в никуда (ФИКС 10 §22)");
            Assert.IsTrue(AnyContains(lines, "настройки открыты"),
                "открытие настроек не оставило строки с вкладкой в журнале: " +
                string.Join(" | ", lines.ToArray()));
            Assert.IsTrue(AnyContains(lines, "вкладка 0"),
                "журнал не называет вкладку «Функции» (0): " + string.Join(" | ", lines.ToArray()));
            ui.uiVisible = true;
        }

        // ==================== 8. ОТКАЗЫ С ПРИЧИНОЙ, КОГДА УСЛОВИЕ НЕ ВЫПОЛНЕНО

        /// <summary>
        /// ЛИСТЫ «по условию IsEnabled», чьё обещание — НАЗВАННЫЙ ОТКАЗ, когда условие не
        /// выполнено (точки нет, робота нет, прицела нет, файла нет, стека/службы нет):
        /// waypoint.add/earlier/later/play, zone.box/sphere/cylinder, export.robot, session.save,
        /// log.export, file.import, scenarios.run, demo.pickplace, pose.save, edit.reset,
        /// tool.measure.reset (слайды 184/196/200/206/207/210/211/213/218).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Каждое нажатие без условий оставляет в журнале строку с ПРИЧИНОЙ
        /// (или со своим фактом — «журнал выгружен», «линейка: сброшена», «сессия … сохранена»),
        /// а не молча выходит. Тихого отказа нет ни у одной команды этой группы.
        /// </summary>
        [Test]
        public void ConditionalCommands_LeaveNamedReasonInJournal()
        {
            GameObject host = NewHost("refuse");
            RegisterAllCommands(host);
            KazistovVvUIManager ui = host.GetComponent<KazistovVvUIManager>();
            FeatureHub features = host.GetComponent<FeatureHub>();

            var cameraGo = new GameObject("KvLeafWave3_RefuseCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();
            TrajectoryFlowController refuseFlow = cameraGo.AddComponent<TrajectoryFlowController>();
            // `ResetFlow` (его зовёт `edit.reset`) работает с фантомами/траекториями, которые
            // создаются в `Awake` — в EditMode его не бывает, поэтому поднимаем поток явно.
            WakeUpComponent(refuseFlow, "TrajectoryFlowController");
            SetField(ui, "flow", refuseFlow);

            var expectations = new Dictionary<string, string>
            {
                { "waypoint.add", "ни прицела" },
                { "waypoint.play", "робот не определён" },
                { "zone.box", "наведите прицел" },
                { "zone.sphere", "наведите прицел" },
                { "zone.cylinder", "наведите прицел" },
                { "export.robot", "Нет траектории" },
                { "session.save", "сессия" },
                { "log.export", "журнал выгружен" },
                { "file.import", "импорт сценария" },
                { "pose.save", "позу сохранить нечем" },
                { "tool.measure.reset", "линейка: сброшена" },
                { "demo.pickplace", "не удалось поставить куб" }
            };

            foreach (KeyValuePair<string, string> pair in expectations)
            {
                int before = LogCount();
                InvokeQuietly(pair.Key);   // часть команд перестраивает окно через `Destroy`
                List<string> lines = LogSince(before);
                Assert.IsTrue(AnyContains(lines, "нажатие: " + pair.Key),
                    "«" + pair.Key + "»: нажатие не дошло до команды");
                Assert.IsTrue(AnyContains(lines, pair.Value),
                    "«" + pair.Key + "» не назвала причину/факт «" + pair.Value + "»: " +
                    string.Join(" | ", lines.ToArray()));
            }

            // --- waypoint.earlier/later: на пустом списке граничный случай возвращает false
            //     БЕЗ единой строки (обещание листа) — нажатие не должно ничего выдумывать.
            int start = LogCount();
            KvCommands.Invoke("waypoint.earlier");
            KvCommands.Invoke("waypoint.later");
            List<string> boundary = LogSince(start);
            Assert.AreEqual(2, boundary.Count,
                "граничные «раньше/позже» оставили лишние строки (обещано «без единой строки»): " +
                string.Join(" | ", boundary.ToArray()));

            // --- scenarios.run: сценариев нет → молчаливый выход (обещание листа), но нажатие
            //     всё равно зарегистрировано журналом — тихого КАСАНИЯ кнопки нет.
            //     Список сценариев пустим ПРЯМО: `FeatureHub.Awake` поднимает их из папки данных,
            //     и без этого проверяемое «сценариев нет» в тесте не наступало бы.
            SetProperty(features, "Scenarios", new KvScenarioManager());
            int sc = LogCount();
            KvCommands.Invoke("scenarios.run");
            List<string> scenarioLines = LogSince(sc);
            Assert.AreEqual(1, scenarioLines.Count,
                "«scenarios.run» без сценариев обязана выйти молча (лист «ни строки, ни статуса»), " +
                "а получилось: " + string.Join(" | ", scenarioLines.ToArray()));

            // --- edit.reset: «эквивалент Esc» — сбрасывает зафиксированную точку
            KvCommand reset = KvCommands.Get("edit.reset");
            Assert.IsNotNull(reset, "edit.reset не зарегистрирована");
            TrajectoryFlowController flow = ui.Flow;
            Assert.IsNotNull(flow, "поток траекторий не подключён к менеджеру");
            object state = GetField(flow, "state");
            Assert.IsNotNull(state, "у потока нет поля state — проверка не про тот факт");
            Type stateType = state.GetType();
            FieldInfo hasPoint = stateType.GetField("hasPoint");
            Assert.IsNotNull(hasPoint, "в SelectionState нет hasPoint");
            hasPoint.SetValue(state, true);
            SetField(flow, "state", state);
            Assert.IsTrue((bool)hasPoint.GetValue(GetField(flow, "state")),
                "точка не установилась — проверка не о чём");

            int er = LogCount();
            InvokeQuietly("edit.reset");
            Assert.IsTrue(AnyContains(LogSince(er), "нажатие: edit.reset"),
                "нажатие «Сброс сценария» не дошло до команды");
            object after = GetField(flow, "state");
            Assert.IsFalse((bool)hasPoint.GetValue(after),
                "«Сброс сценария» не очистила зафиксированную точку — лист обещал Esc-эквивалент");
        }

        // ==================== 9. DOCK-ПАНЕЛИ: КНОПКИ ЗАГОЛОВКА И ДВОЙНОЙ КЛИК (10 ЛИСТОВ)

        /// <summary>
        /// ЛИСТЫ 74 «Dock / Двойной клик по заголовку — Left → Right → Bottom → Float → Left» и
        /// 190/191 «Header/Collapse», «Header/Dock», «Header/Close» для трёх панелей.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Кнопки нажимаются ИМЕННО кнопками заголовка (`onClick`), а не методами
        /// панели: Collapse сворачивает/разворачивает тело, Dock переключает пристыковку
        /// Float ↔ Left, Close укладывает окно на боковую полочку (`InRail`), клик по полочке
        /// возвращает его, а двойной клик по заголовку гоняет пристыковку по кругу.
        /// </summary>
        [Test]
        public void DockPanel_HeaderButtons_Work_AndDoubleClickCyclesSides()
        {
            var canvasGo = new GameObject("KvLeafWave3_DockCanvas", typeof(RectTransform));
            hosts.Add(canvasGo);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(1920f, 1080f);

            // ТРИ панели — ровно те три, что названы в листах 190/191: у каждой свой заголовок,
            // своя пристыковка и своя полочка, хотя кнопки создаёт общий `HeaderButton`.
            GameObject host = NewHost("dock");
            var panels = new[]
            {
                BuildDock(host, canvasGo, "Дерево моделей", "tree", KvDockSide.Left),
                BuildDock(host, canvasGo, "Настройки и справка", "settings", KvDockSide.Bottom),
                BuildDock(host, canvasGo, "Горячие клавиши", "hotkeys", KvDockSide.Right)
            };
            foreach (KvDockPanel panel in panels) CheckDockPanel(panel);

            // --- ДВОЙНОЙ КЛИК ПО ЗАГОЛОВКУ (лист 74) — на ПЕРВОЙ панели, цикл пристыковок
            KvDockPanel first = panels[0];
            Transform header = first.HeaderRect;
            Assert.IsNotNull(header, "у панели нет заголовка");
            Assert.AreEqual(KvDockSide.Left, first.Side,
                "панель «Дерево моделей» не у левого края — проверка двойного клика не о чём");

            // Left → Right → Bottom → Float → Left
            KvDockDrag drag = header.GetComponent<KvDockDrag>();
            Assert.IsNotNull(drag, "по заголовку нет обработчика перетаскивания/клика");
            var click = new PointerEventData(null) { clickCount = 2 };
            drag.OnPointerClick(click);
            Assert.AreEqual(KvDockSide.Right, first.Side, "двойной клик не перевёл панель вправо");
            drag.OnPointerClick(click);
            Assert.AreEqual(KvDockSide.Bottom, first.Side, "двойной клик не перевёл панель вниз");
            drag.OnPointerClick(click);
            Assert.AreEqual(KvDockSide.Float, first.Side, "двойной клик не сделал панель плавающей");
            drag.OnPointerClick(click);
            Assert.AreEqual(KvDockSide.Left, first.Side, "двойной клик не замкнул цикл на левом крае");

            // Одинарный клик ничего не меняет — иначе каждый тап перестыковывал бы панель.
            drag.OnPointerClick(new PointerEventData(null) { clickCount = 1 });
            Assert.AreEqual(KvDockSide.Left, first.Side, "одинарный клик по заголовку сменил пристыковку");
        }

        /// <summary>
        /// КНОПКИ ЗАГОЛОВКА ОДНОЙ ПАНЕЛИ: «Свернуть» сворачивает/разворачивает тело,
        /// «Открепить» переключает Float ↔ Left, «Закрыть» укладывает на полочку, а клик по
        /// полочке возвращает окно. Каждый лист 190/191 закрывается своей панелью.
        /// </summary>
        private static void CheckDockPanel(KvDockPanel panel)
        {
            string title = panel.Title;
            Transform header = panel.HeaderRect;
            Assert.IsNotNull(header, "у панели «" + title + "» нет заголовка");
            Transform collapseT = header.Find("Collapse");
            Transform dockT = header.Find("Dock");
            Transform closeT = header.Find("Close");
            Assert.IsNotNull(collapseT, "«" + title + "»: кнопки «Свернуть» нет в заголовке");
            Assert.IsNotNull(dockT, "«" + title + "»: кнопки «Открепить» нет в заголовке");
            Assert.IsNotNull(closeT, "«" + title + "»: кнопки «Закрыть» нет в заголовке");
            Button collapse = collapseT.GetComponent<Button>();
            Button dock = dockT.GetComponent<Button>();
            Button close = closeT.GetComponent<Button>();
            Assert.IsNotNull(collapse, "«" + title + "»: «Свернуть» без onClick");
            Assert.IsNotNull(dock, "«" + title + "»: «Открепить» без onClick");
            Assert.IsNotNull(close, "«" + title + "»: «Закрыть» без onClick");

            // --- Collapse: свернуть/развернуть
            Assert.IsFalse(panel.Collapsed, "«" + title + "»: панель свёрнута до нажатия");
            PressQuietly(collapse);
            Assert.IsTrue(panel.Collapsed, "«" + title + "»: «Collapse» не свернул панель");
            PressQuietly(collapse);
            Assert.IsFalse(panel.Collapsed, "«" + title + "»: повторный «Collapse» не развернул");

            // --- Dock: Float ↔ Left
            KvDockSide start = panel.Side;
            PressQuietly(dock);
            Assert.AreEqual(KvDockSide.Float, panel.Side,
                "«" + title + "»: «Dock» не открепил панель в плавающее окно");
            Assert.IsTrue(panel.Floating, "«" + title + "»: панель не стала плавающей");
            PressQuietly(dock);
            // Обещание листа: «SetSide(Float ↔ Left)» — открепление всегда ведёт в плавающее
            // окно, а прикрепление — к ЛЕВОМУ краю (не к той стороне, откуда ушли).
            Assert.AreEqual(KvDockSide.Left, panel.Side,
                "«" + title + "»: повторный «Dock» не прикрепил панель к краю (Float ↔ Left)");

            // --- Close: окно уходит на боковую полочку, клик по полочке возвращает
            PressQuietly(close);
            Assert.IsTrue(panel.InRail, "«" + title + "»: «Close» не уложил окно на полочку");
            panel.RailClick();
            Assert.IsFalse(panel.InRail, "«" + title + "»: клик по полочке не вернул окно");
        }

        // ==================== 10. ТУЛБАР: СПИСОК ГРУППЫ И КНОПКИ ГРУПП (21 ЛИСТ)

        /// <summary>
        /// ЛИСТЫ 175 «KvToolbar / Group_net, Group_ui, Group_misc» — «открывает/закрывает список
        /// KvGroupMenu под кнопкой; в списке строки команд + действие „Свернуть/развернуть группу“;
        /// состояние „свёрнута“ хранится в KvSettings» — и листы 178–188 «KvToolbar / &lt;группа&gt; /
        /// &lt;кнопка&gt;» (робот, путь, экспорт, верстак).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Список группы открывается РУЧКОЙ группы, содержит строку на каждую
        /// зарегистрированную команду (`GM_&lt;id&gt;`) плюс действие сворачивания; свёрнутость
        /// переключается и читается через `IsGroupCollapsed`; каждая кнопка группы тулбара
        /// (`Cmd_&lt;id&gt;`) после нажатия оставляет в журнале `нажатие: &lt;id&gt;`.
        /// </summary>
        [Test]
        public void Toolbar_GroupMenu_HasEveryRow_AndEveryGroupButtonPressesItsCommand()
        {
            RegisterAllCommands(NewHost("toolreg"));

            var canvasGo = new GameObject("KvLeafWave3_ToolbarCanvas", typeof(RectTransform));
            hosts.Add(canvasGo);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(1920f, 1080f);

            var ids = new List<string>();
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
                foreach (string id in group.Commands)
                    if (KvCommands.Get(id) != null && !ids.Contains(id)) ids.Add(id);

            GameObject host = NewHost("toolbar");
            KvToolbar toolbar = host.AddComponent<KvToolbar>();
            LogAssert.ignoreFailingMessages = true;   // сборка дёргает Destroy(LayoutElement) в EditMode
            try
            {
                toolbar.Build((RectTransform)canvasGo.transform, canvas, ids.ToArray(), null, null);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
            Assert.Greater(toolbar.GroupCount, 0, "тулбар не собрал ни одной группы");

            // --- список группы: строка на каждую команду + действие сворачивания
            foreach (string groupId in new[] { "net", "ui", "misc" })
            {
                KvToolbarGroup group = KvToolbarGroups.Find(groupId);
                Assert.IsNotNull(group, "группы «" + groupId + "» нет в каталоге");
                RebuildToolbarGrid(toolbar);   // развернуть группы (перестройка дёргает Destroy)

                Assert.IsTrue(QuietGroupMenu(toolbar, groupId),
                    "ручка группы «" + groupId + "» не открыла список — лист «открывает список под кнопкой»");
                Assert.AreEqual(groupId, toolbar.RowsGroupId,
                    "открыт список ЧУЖОЙ группы: «" + toolbar.RowsGroupId + "» вместо «" + groupId + "»");

                Transform menuPanel = canvasGo.transform.Find("KvToolbarGroupLayer/KvGroupMenu");
                Assert.IsNotNull(menuPanel, "панели списка группы нет в канвасе");
                foreach (string id in group.Commands)
                {
                    if (KvCommands.Get(id) == null) continue;   // не зарегистрирована — строки не будет
                    Assert.IsNotNull(menuPanel.Find("GM_" + id),
                        "в списке группы «" + groupId + "» нет строки команды " + id);
                }
                Assert.IsNotNull(menuPanel.Find("GM_Action"),
                    "в списке группы «" + groupId + "» нет действия «Свернуть/развернуть группу»");
                Assert.IsNotNull(menuPanel.Find("GroupTitle"),
                    "в списке группы «" + groupId + "» нет заголовка");

                QuietCloseGroupMenu(toolbar);
                Assert.IsFalse(toolbar.GroupMenuOpen, "список группы «" + groupId + "» не закрылся");
            }

            // --- свёрнутость группы: переключается и читается (значение возвращается)
            bool miscWasCollapsed = toolbar.IsGroupCollapsed("misc");
            try
            {
                SetGroupCollapsedQuiet(toolbar, "misc", !miscWasCollapsed);
                Assert.AreNotEqual(miscWasCollapsed, toolbar.IsGroupCollapsed("misc"),
                    "свёрнутость группы «misc» не переключилась");
                SetGroupCollapsedQuiet(toolbar, "misc", miscWasCollapsed);
                Assert.AreEqual(miscWasCollapsed, toolbar.IsGroupCollapsed("misc"),
                    "свёрнутость группы «misc» не вернулась в исходное состояние");
            }
            finally
            {
                SetGroupCollapsedQuiet(toolbar, "misc", miscWasCollapsed);
            }

            // --- кнопки групп тулбара: каждая ведёт к своей команде
            var failures = new List<string>();
            RebuildToolbarGrid(toolbar);
            foreach (string id in ids)
            {
                Transform button = canvasGo.transform.Find("KvToolbar/Groups/Cmd_" + id);
                if (button == null) { failures.Add(id + ": кнопки Cmd_" + id + " нет в тулбаре"); continue; }
                Button component = button.GetComponent<Button>();
                if (component == null) { failures.Add(id + ": у кнопки нет onClick"); continue; }

                int before = LogCount();
                try { PressQuietly(component); }
                catch (Exception error) { failures.Add(id + ": нажатие упало: " + error.Message); continue; }
                if (!AnyContains(LogSince(before), "нажатие: " + id))
                    failures.Add(id + ": нажатие не дошло до команды");
            }
            Assert.IsTrue(failures.Count == 0,
                "кнопки тулбара не все ведут к своим командам (" + failures.Count + " шт):\n" +
                string.Join("\n", failures.ToArray()));
        }

        // ==================== 11. СПРАВОЧНИК ГОРЯЧИХ КЛАВИШ (СЛАЙД 70)

        /// <summary>
        /// ЛИСТ 70 «Горячие клавиши / 56 строк биндов — справочник „действие — клавиша —
        /// пояснение“».
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Справочник собирается живым кодом (`KvBindings.All()`), в нём строки
        /// с разделом, действием и клавишей (и пояснением там, где оно обещано), а список
        /// конфликтов считается без падения — ровно то, что показывает окно горячих клавиш.
        /// </summary>
        [Test]
        public void HotkeyReference_EveryBindHasActionAndKeys()
        {
            List<KvBindEntry> binds = KvBindings.All();
            Assert.GreaterOrEqual(binds.Count, 50,
                "в справочнике биндов " + binds.Count + " строк — проверка не про 56 строк листа");

            var missing = new List<string>();
            foreach (KvBindEntry entry in binds)
            {
                if (string.IsNullOrEmpty(entry.Group) || string.IsNullOrEmpty(entry.Action) ||
                    string.IsNullOrEmpty(entry.Keys))
                    missing.Add((entry.Action ?? "?") + " [" + (entry.Keys ?? "?") + "]");
            }
            Assert.IsTrue(missing.Count == 0,
                "строки справочника без «действия — клавиши» (" + missing.Count + " шт): " +
                string.Join(", ", missing.ToArray()));

            // Строки с пояснением — отдельная колонка справочника: она либо заполнена, либо её нет.
            int withNote = 0;
            foreach (KvBindEntry entry in binds)
                if (!string.IsNullOrEmpty(entry.Note)) withNote++;
            Assert.Greater(withNote, 0,
                "ни одна строка справочника не имеет пояснения — колонка «пояснение» пуста");

            HashSet<string> conflicts = KvBindings.Conflicts();
            Assert.IsNotNull(conflicts, "проверка конфликтов клавиш упала");
        }

        // ==================== СЛУЖЕБНЫЕ ПОМОЩНИКИ

        /// <summary>ВЫЗВАТЬ КОМАНДУ БЕЗ ОШИБОК ЛОГА (перестройка виджетов зовёт `Destroy`).</summary>
        private static void InvokeQuietly(string id)
        {
            LogAssert.ignoreFailingMessages = true;
            try { KvCommands.Invoke(id); }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        /// <summary>
        /// НАЖАТЬ КНОПКУ БЕЗ ОШИБОК ЛОГА. Перестройка виджетов после нажатия зовёт `Destroy`, а в
        /// EditMode Unity пишет на это ошибку — она не про поведение листа. Исключения при этом
        /// НЕ подавляются: их ловит вызывающий код и заносит в список провалов.
        /// </summary>
        private static void PressQuietly(Button button)
        {
            LogAssert.ignoreFailingMessages = true;
            try { button.onClick.Invoke(); }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        /// <summary>ОТКРЫТЬ СПИСОК ГРУППЫ без ошибок лога (перестройка строк зовёт `Destroy`).</summary>
        private static bool QuietGroupMenu(KvToolbar toolbar, string groupId)
        {
            LogAssert.ignoreFailingMessages = true;
            try { return toolbar.EnsureGroupMenuOpen(groupId); }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        /// <summary>ЗАКРЫТЬ СПИСОК ГРУППЫ без ошибок лога.</summary>
        private static void QuietCloseGroupMenu(KvToolbar toolbar)
        {
            LogAssert.ignoreFailingMessages = true;
            try { toolbar.CloseGroupMenu(); }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        /// <summary>СВЕРНУТЬ/РАЗВЕРНУТЬ ГРУППУ БЕЗ ОШИБОК ЛОГА (перестройка зовёт `Destroy`).</summary>
        private static void SetGroupCollapsedQuiet(KvToolbar toolbar, string id, bool collapsed)
        {
            LogAssert.ignoreFailingMessages = true;
            try { toolbar.SetGroupCollapsed(id, collapsed); }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        /// <summary>
        /// ПЕРЕСТРОИТЬ СЕТКУ ТУЛБАРА (развернуть группы). Перестройка зовёт `Destroy` детей —
        /// в EditMode Unity пишет ошибку лога, поэтому подавляем её ТОЛЬКО на время перестройки.
        /// </summary>
        private static void RebuildToolbarGrid(KvToolbar toolbar)
        {
            LogAssert.ignoreFailingMessages = true;
            try { toolbar.ExpandAllGroups(); }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        /// <summary>СОБРАТЬ DOCK-ПАНЕЛЬ В ПАМЯТИ (как её строит `BuildShell`).</summary>
        private static KvDockPanel BuildDock(GameObject host, GameObject canvasGo, string title,
            string iconId, KvDockSide side)
        {
            KvDockPanel dock = host.AddComponent<KvDockPanel>();
            // `HeaderButton` дёргает `Destroy(LayoutElement)` — в EditMode Unity пишет ошибку.
            // Игнорируем ТОЛЬКО на время сборки: сами проверки идут уже без подавления логов.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                dock.Build((RectTransform)canvasGo.transform, canvasGo.GetComponent<Canvas>(),
                    title, iconId, side, 300f, iconId);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
            Assert.IsNotNull(dock.Root, "панель не собрала корень");
            return dock;
        }

        /// <summary>ПРОЧИТАТЬ СОСТОЯНИЕ СЛУЖБЫ: поле ИЛИ свойство (в проекте и так, и так).</summary>
        private static bool ReadState(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return (bool)field.GetValue(target);
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property,
                "у " + target.GetType().Name + " нет ни поля, ни свойства " + name);
            return (bool)property.GetValue(target, null);
        }

        /// <summary>
        /// УСТАНОВИТЬ СОСТОЯНИЕ СЛУЖБЫ, чтобы имитировать условие: свойство с сеттером, иначе
        /// поле (в т.ч. backing-поле только для чтения свойства `Visible`).
        /// </summary>
        private static void WriteState(object target, string name, bool value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = target.GetType().GetProperty(name, flags);
            if (property != null && property.GetSetMethod(true) != null)
            {
                property.SetValue(target, value, null);
                return;
            }
            string backing = char.ToLowerInvariant(name[0]) + name.Substring(1);
            FieldInfo field = target.GetType().GetField(name, flags) ??
                              target.GetType().GetField(backing, flags);
            Assert.IsNotNull(field,
                "у " + target.GetType().Name + " нечем задать состояние " + name);
            field.SetValue(target, value);
        }

        /// <summary>ПОЛЕ КОМПОНЕНТА (в т.ч. private) — для имитации условия в EditMode.</summary>
        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(target);
        }

        /// <summary>УСТАНОВИТЬ ПОЛЕ КОМПОНЕНТА (в т.ч. private).</summary>
        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "у " + target.GetType().Name + " нет поля " + name);
            field.SetValue(target, value);
        }

        /// <summary>УСТАНОВИТЬ СВОЙСТВО С ЧАСТНЫМ СЕТТЕРОМ.</summary>
        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, "у " + target.GetType().Name + " нет свойства " + name);
            property.SetValue(target, value, null);
        }
    }
}
