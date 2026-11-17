// ================================================================================================
//  KvUiSweepGroupATests.cs — EditMode-тесты ГРУППЫ А ВОЛНЫ 6a (дефекты самого обхода).
//
//  ОТКУДА ЗАДАЧА. `Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv`: группа листьев осталась незакрытой не из-за
//  кнопок, а из-за ДЕФЕКТОВ САМОГО ОБХОДА — инвентарь не пересобирался после переключения
//  вкладки, палитра команд и панель выбора робота не восстанавливались перед нажатием, полочка
//  дока не кликалась, поля статус-бара мерялись только длиной, а ветки дерева не существовали
//  в чистом прогоне. Здесь каждая из шести правок проверяется ПОВЕДЕНИЕМ живого кода:
//
//   1) смена вкладки → пересборка инвентаря и новые строки в очереди;
//   2) палитра команд открывается штатным `OpenCommandPalette` и закрывается после проверки;
//   3) панель выбора робота (`Btn_Scara`) открывается штатным `StartPlacement(SpawnKind.Robot)`;
//   4) снимок несёт ЗНАЧЕНИЯ всех семи полей статус-бара, а не только длину/хэш;
//   5) возврат панели из рейла — КЛИК ПО ПОЛОЧКЕ (`RailClick`), а не прямой `FromRail`;
//   6) ветки дерева узнаются по строкам, а команды подготовки сцены — штатные операторские.
//
//  ГРАНИЦА ЧЕСТНОСТИ. Тексты исходников файлы тестов НЕ читают (храповик P4-3): только живые
//  API, реестр команд и отражение в приватные методы обхода. Устройства тест не трогает, сцены
//  не открывает, игру не запускает. `TearDown` снимает построенные хосты, возвращает язык,
//  реестр команд и игнорирование ошибок лога — даже когда тест упал.
// ================================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KazistovVvDiagnostics;
using KazistovVvFeatures;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KazistovVvTests
{
    /// <summary>Группа А волны 6a: дефекты самого обхода, а не кнопок интерфейса.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvUiSweepGroupATests
    {
        // ------------------------------------------------------------------ инфраструктура

        /// <summary>Хосты, созданные тестом: снимаются в TearDown независимо от исхода.</summary>
        private readonly List<GameObject> hosts = new List<GameObject>();

        /// <summary>Было ли игнорирование ошибок лога до теста (возвращается в TearDown).</summary>
        private bool ignoreLogsWas;

        /// <summary>Язык до теста (подписи строк дерева и панели настроек — нужна предсказуемость).</summary>
        private string localeWas;

        /// <summary>СНИМОК РЕЕСТРА КОМАНД ДО `BuildShell` (общий статик — вернуть как был).</summary>
        private static List<KvCommand> registrySnapshot;

        [SetUp]
        public void SetUp()
        {
            localeWas = KvLoc.CurrentCode;
            if (KvLoc.CurrentCode != "ru") KvLoc.SetLanguage("ru");
            ignoreLogsWas = LogAssert.ignoreFailingMessages;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject host in hosts)
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            hosts.Clear();

            if (KvLoc.CurrentCode != localeWas) KvLoc.SetLanguage(localeWas);

            // ПОДПИСКИ СОБЫТИЙ — ПОСЛЕ УНИЧТОЖЕНИЯ ХОСТОВ: подписчик (менеджер) мёртв, остаток
            // события будит бы удал следующего теста.
            ClearStaticEvent(typeof(KvTheme), "Changed");

            LogAssert.ignoreFailingMessages = ignoreLogsWas;

            // Реестр команд возвращаем к снимку ДО сборки оболочки (OnDestroy менеджера чистит
            // общий статик — без восстановления следующие фикстуры увидели бы пустой реестр).
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
            var go = new GameObject("KvGroupA_" + name);
            hosts.Add(go);
            return go;
        }

        /// <summary>
        /// СОБРАТЬ ОБОЛОЧКУ В РЕДАКТОРЕ: `Awake` (сам singleton) и приватный `BuildShell` —
        /// теми же отражениями, которыми волна 4 поднимала оболочку. `Start` НЕ зовётся:
        /// он добавляет корутины и работу со сценой, которые EditMode не выполняет.
        /// </summary>
        private KazistovVvUIManager BuildShell()
        {
            if (registrySnapshot == null)
            {
                registrySnapshot = new List<KvCommand>();
                foreach (KvCommand command in KvCommands.All) registrySnapshot.Add(command);
            }

            // ЧИСТЫЕ ПОДПИСКИ: `Awake` в EditMode вызывает И Unity (при `AddComponent`), И тест
            // отражением — подписка на `KvTheme.Changed` получалась ДВОЙНОЙ.
            ClearStaticEvent(typeof(KvTheme), "Changed");

            GameObject host = NewHost("shell");
            var ui = host.AddComponent<KazistovVvUIManager>();

            // `Awake`/`BuildShell` в EditMode дёргают `Destroy(LayoutElement)`: Unity пишет
            // ОШИБКУ лога, которую NUnit читает как провал теста. Подавляем ТОЛЬКО на время
            // сборки — сами проверки идут уже без подавления.
            bool was = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                if (KazistovVvUIManager.Instance != ui)
                    InvokeNoArgs(ui, "Awake", "менеджер интерфейса");
                InvokeNoArgs(ui, "BuildShell", "оболочка интерфейса");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = was;
            }

            Assert.IsNotNull(ui.Palette, "после BuildShell палитры команд в оболочке нет");
            Assert.IsNotNull(ui.StatusBar, "после BuildShell статус-бара в оболочке нет");
            Assert.IsNotNull(ui.Tree, "после BuildShell дерева в оболочке нет");
            return ui;
        }

        /// <summary>ВЫЗЫВАТЬ ПРИВАТНЫЙ МЕТОД БЕЗ АРГУМЕНТОВ (в EditMode штатные события не идут).</summary>
        private static void InvokeNoArgs(object target, string method, string label)
        {
            MethodInfo info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(info, "у " + label + " больше нет метода " + method);
            try { info.Invoke(target, null); }
            catch (TargetInvocationException error)
            {
                Assert.Fail(method + " (" + label + ") упал: " + error.InnerException);
            }
        }

        /// <summary>ВЫЗЫВАТЬ ПРИВАТНЫЙ МЕТОД С АРГУМЕНТАМИ (типы — по значениям аргументов).</summary>
        private static object InvokePrivate(object target, string name, params object[] args)
        {
            var types = new Type[args.Length];
            for (int i = 0; i < args.Length; i++) types[i] = args[i].GetType();
            MethodInfo method = target.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic, null, types, null);
            Assert.IsNotNull(method, "метод " + name + " не найден — проверка потеряла смысл");
            return method.Invoke(target, args);
        }

        /// <summary>ВЫЗЫВАТЬ ПРИВАТНЫЙ СТАТИЧЕСКИЙ МЕТОД (например `RestorePanels`).</summary>
        private static object InvokeStatic(string name, params object[] args)
        {
            MethodInfo method = typeof(KvUiSweepRuntime).GetMethod(name,
                BindingFlags.Static | BindingFlags.NonPublic, null,
                GetTypes(args), null);
            Assert.IsNotNull(method, "статический метод " + name + " не найден");
            return method.Invoke(null, args);
        }

        private static Type[] GetTypes(object[] args)
        {
            var types = new Type[args.Length];
            for (int i = 0; i < args.Length; i++) types[i] = args[i].GetType();
            return types;
        }

        /// <summary>Поставить приватное поле экземпляра (например `queue` обхода).</summary>
        private static void SetInstanceField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "поле " + name + " не найдено — проверка потеряла смысл");
            field.SetValue(target, value);
        }

        /// <summary>
        /// ВЫЗОВ ПОД УЗКИМ ПОДАВЛЕНИЕМ ОШИБОК ЛОГА. Пересборка палитры/панелей в EditMode
        /// дёргает `Destroy(...)` (`KvCommandPalette.ClearRows`) — Unity пишет ОШИБКУ, которую
        /// NUnit читает как провал теста, хотя ломания нет. Приём волны 4: подавление ТОЛЬКО
        /// вокруг вызова, утверждение идёт уже без него.
        /// </summary>
        private static void Quietly(Action action)
        {
            bool was = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try { action(); }
            finally { LogAssert.ignoreFailingMessages = was; }
        }

        /// <summary>СБРОСИТЬ СТАТИЧЕСКОЕ СОБЫТИЕ (двойная подписка будит мёртвый объект).</summary>
        private static void ClearStaticEvent(Type type, string name)
        {
            FieldInfo field = type.GetField(name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return;
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) return;
            field.SetValue(null, null);
        }

        /// <summary>ПОСТРОИТЬ КАНДИДАТА ОБХОДА (вложенный приватный `Candidate`).</summary>
        private static object NewCandidate(string path, Selectable target)
        {
            Type type = typeof(KvUiSweepRuntime).GetNestedType("Candidate", BindingFlags.NonPublic);
            Assert.IsNotNull(type, "в KvUiSweepRuntime нет вложенного Candidate — зонд устарел");
            object candidate = Activator.CreateInstance(type);
            SetMember(candidate, "Path", path);
            SetMember(candidate, "Target", target);
            SetMember(candidate, "Kind", KvUiSweepKind.Button);
            SetMember(candidate, "Key", KvUiSweepNames.KeyOf(path));
            return candidate;
        }

        /// <summary>Поставить поле/свойство кандидата (поля публичные, но у вложенного класса).</summary>
        private static void SetMember(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "у Candidate нет поля " + name);
            field.SetValue(target, value);
        }

        /// <summary>
        /// ПОДГОТОВКА ЭЛЕМЕНТА (`Prepare`) — возвращает имя результата и деталь для причины.
        /// </summary>
        private static string Prepare(KvUiSweepRuntime runtime, object candidate, out string detail)
        {
            MethodInfo method = typeof(KvUiSweepRuntime).GetMethod("Prepare",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "у обхода больше нет Prepare — восстановление экрана не проверить");
            var args = new object[] { candidate, null };
            object result = method.Invoke(runtime, args);
            detail = args[1] as string ?? "";
            Assert.IsNotNull(result, "Prepare ничего не вернул");
            return result.ToString();
        }

        /// <summary>КЛЮЧИ ЭЛЕМЕНТОВ ИНВЕНТАРЯ (поле `Key` каждого кандидата).</summary>
        private static HashSet<string> InventoryKeys(KvUiSweepRuntime runtime, IList fresh)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < fresh.Count; i++)
            {
                object candidate = fresh[i];
                FieldInfo field = candidate.GetType().GetField("Key",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) continue;
                keys.Add((string)field.GetValue(candidate) ?? "");
            }
            return keys;
        }

        /// <summary>СОБРАТЬ СВЕЖИЙ СПИСОК ЦЕЛЕЙ (`CollectTargets`) ключами наверх.</summary>
        private static HashSet<string> CollectKeys(KvUiSweepRuntime runtime)
        {
            Type candidateType = typeof(KvUiSweepRuntime).GetNestedType("Candidate",
                BindingFlags.NonPublic);
            Assert.IsNotNull(candidateType, "в обходе нет Candidate — зонд устарел");
            IList list = (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(candidateType));
            InvokePrivate(runtime, "CollectTargets", list);
            return InventoryKeys(runtime, list);
        }

        /// <summary>СКОЛЬКО КЛЮЧЕЙ В ЖИВОМ byKey ОБХОДА.</summary>
        private static int ByKeyCount(KvUiSweepRuntime runtime)
        {
            FieldInfo field = typeof(KvUiSweepRuntime).GetField("byKey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "у обхода нет byKey — инвентарь не проверить");
            var map = (IDictionary)field.GetValue(runtime);
            return map == null ? 0 : map.Count;
        }

        // ================= 1. ВКЛЯДКА «ГРАФИКА»: ИНВЕНТАРЬ ПЕРЕСОБИРАЕТСЯ ПОСЛЕ ПЕРЕКЛЮЧЕНИЯ

        /// <summary>
        /// ПРАВИЛО ПЕРЕСБОРКИ: равное содержимое — не пересобираем (пересборка сканирует все
        /// канвасы и стоит кадров на каждом элементе), изменившееся — пересобираем, пустой ключ —
        /// не гадаем (оболочки нет — пересобирать нечего).
        /// </summary>
        [Test]
        public void ContentChanged_OnlyRealSwitchTriggersRebuild()
        {
            Assert.IsTrue(KvUiSweepInventory.ContentChanged("tab=0", "tab=3"),
                "смена вкладки — это изменение содержимого, инвентарь обязан пересобраться");
            Assert.IsFalse(KvUiSweepInventory.ContentChanged("tab=3", "tab=3"),
                "то же содержимое — пересборка не нужна");
            Assert.IsFalse(KvUiSweepInventory.ContentChanged("", "tab=3"),
                "пустой ключ — неизвестность, вслепую пересобирать нельзя");
            Assert.IsFalse(KvUiSweepInventory.ContentChanged("tab=0", null),
                "null второго замера — тоже неизвестность");
        }

        /// <summary>
        /// ПЕРЕКЛЮЧЕНИЕ ВКЛЯДКИ ПАНЕЛИ НАСТРОЕК СОБИРАЕТ НОВЫЕ СТРОКИ В ОЧЕРЕДЬ (листы
        /// «Настройки / Графика / …», 12 листьев).
        ///
        /// ОТКУДА ДЕФЕКТ. `MergeFreshTargets` звался ТОЛЬКО когда путь не находился, а элементы
        /// только что открытой вкладки в инвентарь не попадали вовсе: их путей не было ни в
        /// `byKey`, ни в очереди — проверить их было невозможно. Тест строит НАСТОЯЩИЙ
        /// `KvSettingsView`, переключает вкладку и требует, чтобы пересборка принесла новые
        /// ключи.
        /// </summary>
        [Test]
        public void TabSwitchAfterPress_RebuildsInventoryAndQueuesNewRows()
        {
            // ОБОЛОЧКА: ключ содержимого читает МЕНЕДЖЕР (`ui.SettingsTab`), поэтому тест
            // работает на НАСТОЯЩЕЙ панели настроек собранной оболочки, а не на её копии.
            KazistovVvUIManager ui = BuildShell();
            KvUiSweepRuntime runtime = ui.gameObject.AddComponent<KvUiSweepRuntime>();

            KazistovVvUI.KvSettingsView view = ui.SettingsView;
            Assert.IsNotNull(view, "после BuildShell панели настроек нет — вкладку не переключить");
            Assert.Greater(view.RowCount, 0, "панель настроек обязана построить строки стартовой вкладки");

            // ОЧЕРЕДЬ — как её создаёт прогон перед первым сбором.
            SetInstanceField(runtime, "queue", new KvUiSweepQueue());
            int added = (int)InvokePrivate(runtime, "MergeFreshTargets");
            Assert.Greater(added, 0, "первый сбор обязан наполнить очередь");

            HashSet<string> before = CollectKeys(runtime);
            int keysBefore = ByKeyCount(runtime);
            string contentBefore = (string)InvokePrivate(runtime, "ContentSelectionKey");
            Assert.AreNotEqual("", contentBefore,
                "ключ содержимого обязан читаться из собранной панели (менеджер в сцене есть)");

            // ПЕРЕКЛЮЧАЕМ ВКЛЯДКУ — ровно то, что делает прогон, нажимая `SegN`, — и требуем,
            // чтобы пересборка принесла НОВЫЕ ключи (раньше их путей не существовало вовсе).
            int ownTab = view.ActiveTab;
            bool rebuilt = false;
            string lastKey = contentBefore;
            for (int i = 1; i < KazistovVvUI.KvSettingsView.TabCount && !rebuilt; i++)
            {
                int candidate = (ownTab + i) % KazistovVvUI.KvSettingsView.TabCount;
                view.SetTab(candidate);
                string now = (string)InvokePrivate(runtime, "ContentSelectionKey");
                lastKey = now;
                if (now == contentBefore) continue;          // вкладка не сменилась — не наш случай

                int rebuiltCount = (int)InvokePrivate(runtime, "RebuildAfterContentChange",
                    contentBefore);
                HashSet<string> after = CollectKeys(runtime);
                var fresh = new List<string>();
                foreach (string key in after)
                    if (!before.Contains(key)) fresh.Add(key);
                if (rebuiltCount > 0 && fresh.Count > 0)
                {
                    Assert.Greater(ByKeyCount(runtime), keysBefore,
                        "byKey обязан вырасти — новые пути должны находиться перепоиском");
                    rebuilt = true;
                }
            }

            Assert.IsTrue(rebuilt,
                "после переключения вкладки инвентарь не пересобрался: новых строк не появилось " +
                "(ключ содержимого «" + contentBefore + "» → «" + lastKey + "»)");
        }

        // ================= 2–3. ПАЛИТРА КОМАНД И ПАНЕЛЬ ВЫБОРА РОБОТА: ВОССТАНОВЛЕНИЕ ЭКРАНА

        /// <summary>
        /// ПУТИ ПАЛИТРЫ И ВЫБОРА РОБОТА ВЕДУТ К ВОССТАНОВЛЕНИЮ ЭКРАНА, А НЕ К «СКРЫТОЙ ПАНЕЛИ».
        /// Правило — в `KvUiSweepShellNav`, оно же решает, звать ли `OpenShellPanelStep`.
        /// </summary>
        [Test]
        public void PaletteAndRobotChooser_RestorablePaths()
        {
            const string input =
                "KazistovVv_UI/KvCommandCanvas/KvCommandPalette/Panel/Input";
            const string recentRow =
                "KazistovVv_UI/KvCommandCanvas/KvCommandPalette/Panel/List/Pal_waypoint.add«Добавить waypoint»";
            const string scara =
                "KazistovVv_UI/KvCenterCanvas/CenterWindow/RobotChooser/Btn_Scara«SCARA  (2)»";
            const string ordinary =
                "KazistovVv_UI/KazistovVvDockCanvas/Dock_Настройки/Body/Scroll/Content/Switch_Фонарик";

            Assert.IsTrue(KvUiSweepShellNav.IsPalettePath(input), "строка ввода палитры — путь палитры");
            Assert.IsTrue(KvUiSweepShellNav.IsPalettePath(recentRow), "строка `Pal_*` — путь палитры");
            Assert.IsTrue(KvUiSweepShellNav.IsRestorablePath(input),
                "палитра обязана восстанавливаться штатным показом, иначе Input остаётся «в скрытой панели»");
            Assert.IsTrue(KvUiSweepShellNav.IsRestorablePath(recentRow),
                "то же для строк «Недавние команды»");

            Assert.IsTrue(KvUiSweepShellNav.IsRobotChooserPath(scara), "Btn_Scara — путь выборщика");
            Assert.IsTrue(KvUiSweepShellNav.IsRestorablePath(scara),
                "выбор робота обязан восстанавливаться штатным показом, иначе лист недостижим");

            Assert.IsFalse(KvUiSweepShellNav.IsPalettePath(ordinary),
                "правило узкое: строка настроек не должна становиться путём палитры");
            Assert.IsFalse(KvUiSweepShellNav.IsRobotChooserPath(ordinary),
                "правило узкое: строка настроек не должна становиться путём выборщика");
            Assert.IsFalse(KvUiSweepShellNav.IsPalettePath(""), "пустой путь — не палитра");
        }

        /// <summary>
        /// ПАЛИТРА ОТКРЫВАЕТСЯ ШТАТНЫМ ПУТЁМ ОПЕРАТОРА И ЗАКРЫВАЕТСЯ ПОСЛЕ ПРОВЕРКИ.
        ///
        /// ОТКУДА ДЕФЕКТ. Корень палитры выключен до `Open()`, обход видел «элемент в скрытой
        /// панели» и не нажимал ни `Input`, ни строки «Недавние команды» (листы 66-й строки CSV).
        /// Здесь проверяется ровно тот ход, что обязана делать правка: подготовка поднимает
        /// панель ФАКТОМ (`PaletteOpen == true`), а `RestoreShellState` возвращает мир.
        /// </summary>
        [Test]
        public void Palette_PrepareOpensByStandardPath_RestoreCloses()
        {
            KazistovVvUIManager ui = BuildShell();
            KvUiSweepRuntime runtime = ui.gameObject.AddComponent<KvUiSweepRuntime>();

            Assert.IsFalse(ui.PaletteOpen, "палитра на старте закрыта — иначе тест не о чём");

            InputField input = FindChild<InputField>(ui.Palette.transform, "Input");
            Assert.IsNotNull(input, "в палитре нет строки ввода `Input` — лист про неё");

            object candidate = NewCandidate(
                "KazistovVv_UI/KvCommandCanvas/KvCommandPalette/Panel/Input", input);

            string detail = "";
            string result = "";
            // `ClearRows` палитры зовёт `Destroy`, а в EditMode это ОШИБКА лога (среда, а не
            // поведение) — подавляем ТОЛЬКО вокруг вызова, проверки идут уже без подавления.
            Quietly(delegate { result = Prepare(runtime, candidate, out detail); });
            Assert.AreEqual("Ready", result,
                "палитра обязана открываться штатным путём, а не оставаться «скрытой панелью»: " + detail);
            Assert.IsTrue(ui.PaletteOpen, "после подготовки палитра обязана быть открытой (факт, а не надежда)");
            Assert.IsTrue(((string)GetMember(candidate, "PreparedBy")).IndexOf("палитра", StringComparison.Ordinal) >= 0,
                "способ открытия обязан попасть в отчётную строку подготовки");

            // ВОЗВРАТ МИРА: обход закрывает то, что открыл сам.
            Quietly(delegate { InvokePrivate(runtime, "RestoreShellState", candidate); });
            Assert.IsFalse(ui.PaletteOpen, "после проверки палитра обязана закрыться — иначе следующие элементы проверяются в чужом мире");
            Assert.IsFalse((bool)GetMember(candidate, "OpenedPalette"),
                "флаг «открыл обход» обязан сброситься, иначе закрытие сработает дважды");
        }

        /// <summary>
        /// «SCARA (2)»: ПАНЕЛЬ ВЫБОРА РОБОТА ОТКРЫВАЕТСЯ ШТАТНЫМ ПУТЁМ И ЗАКРЫВАЕТСЯ ПОСЛЕ.
        ///
        /// ОТКУДА ДЕФЕКТ (лист 76-й строки CSV): `chooserRoot` выключен, пока оператор не нажмёт
        /// «Добавить робота» — обход видел «элемент в скрытой панели» и не нажимал `Btn_Scara`.
        /// </summary>
        [Test]
        public void RobotChooser_PrepareOpensChooser_RestoreCancelsPlacement()
        {
            KazistovVvUIManager ui = BuildShell();
            KvUiSweepRuntime runtime = ui.gameObject.AddComponent<KvUiSweepRuntime>();

            Assert.IsFalse(ui.RobotChooserOpen, "панель выбора робота на старте закрыта");

            Button scara = FindChildByName<Button>(ui.transform, "Btn_Scara");
            Assert.IsNotNull(scara, "в оболочке нет `Btn_Scara` — лист «SCARA (2)» не о чём");

            object candidate = NewCandidate(
                "KazistovVv_UI/KvCenterCanvas/CenterWindow/RobotChooser/Btn_Scara«SCARA  (2)»", scara);

            string detail;
            string result = Prepare(runtime, candidate, out detail);
            Assert.AreEqual("Ready", result,
                "панель выбора робота обязана открываться штатным «Добавить робота»: " + detail);
            Assert.IsTrue(ui.RobotChooserOpen, "после подготовки панель обязана быть открытой (факт)");
            Assert.IsTrue(scara.gameObject.activeInHierarchy,
                "кнопка SCARA обязана стать активной — иначе нажатие уйдёт в выключенную иерархию");

            InvokePrivate(runtime, "RestoreShellState", candidate);
            Assert.IsFalse(ui.RobotChooserOpen,
                "после проверки выборщик обязан закрыться (путь Esc оператора), иначе режим размещения робота остаётся включённым");
        }

        // ================= 4. СТАТУС-БАР: СЕМЬ ПОЛЕЙ ЗНАЧЕНИЯМИ В СНИМКЕ

        /// <summary>
        /// СНИМОК НЕСЁТ ЗНАЧЕНИЯ ВСЕХ СЕМИ ПОЛЕЙ СТАТУС-БАРА (лист 64-й строки CSV: раньше
        /// снимок мерил только длину и хэш текста, и замер полей был невозможен вовсе).
        /// </summary>
        [Test]
        public void StatusBar_SevenFields_CapturedAsValues_AndVisibleInDiff()
        {
            KazistovVvUIManager ui = BuildShell();
            KvUiSweepRuntime runtime = ui.gameObject.AddComponent<KvUiSweepRuntime>();

            KvStatusBar bar = ui.StatusBar;
            bar.SetState("Idle", Color.white);
            bar.SetRobot("6-осевой");
            bar.SetCursor("0.00, 0.00, 0.00");
            bar.SetMessage("Готово", Color.white);
            bar.SetGraphics("High");
            bar.SetTheme("Тёмная");
            bar.SetFps(60f);

            KvUiSweepSnapshot first = Capture(runtime);
            StringAssert.Contains("State: Idle", first.StatusFields, "поле State обязано быть в снимке значением");
            StringAssert.Contains("Robot: 6-осевой", first.StatusFields, "поле Robot обязано быть в снимке значением");
            StringAssert.Contains("Cursor: 0.00, 0.00, 0.00", first.StatusFields, "поле Cursor обязано быть в снимке");
            StringAssert.Contains("Message: Готово", first.StatusFields, "поле Message обязано быть в снимке");
            StringAssert.Contains("Graphics: High", first.StatusFields, "поле Graphics обязано быть в снимке значением");
            StringAssert.Contains("Theme: ", first.StatusFields, "поле Theme обязано быть в снимке");
            StringAssert.Contains("Тёмная", first.StatusFields, "значение темы обязано быть в снимке");
            StringAssert.Contains("Fps: 60 FPS", first.StatusFields, "поле Fps обязано быть в снимке значением");

            // СМЕНА ЛЮБОГО ПОЛЯ ВИДНА И СНИМКУ, И ОТЧЁТУ ИМЕННО ЗНАЧЕНИЕМ.
            bar.SetRobot("SCARA");
            KvUiSweepSnapshot second = Capture(runtime);
            Assert.AreNotEqual(first.StatusFields, second.StatusFields,
                "смена поля робота обязана менять строку снимка");
            string diff = KvUiSweepDiff.Describe(first, second);
            StringAssert.Contains("поля статус-бара", diff,
                "изменение полей статус-бара обязано попадать в эффект");
            StringAssert.Contains("SCARA", diff, "в эффекте обязано быть НОВОЕ значение поля");

            // БЕЗ ПАНЕЛИ снимок не молчит: показывает хотя бы сообщение.
            Assert.AreEqual("Message: m", KvUiSweepStatusBar.FieldsOf(null, "m"),
                "без статус-бара снимок обязан нести сообщение, а не пустую строку");
        }

        /// <summary>СНЯТЬ СНИМКОМ ОБХОДА (приватный `Capture`, как делают тесты волны S45).</summary>
        private static KvUiSweepSnapshot Capture(KvUiSweepRuntime runtime)
        {
            return (KvUiSweepSnapshot)InvokePrivate(runtime, "Capture",
                new List<KvUiSweepPanelState>());
        }

        // ================= 5. DOCK: ВОЗВРАТ ИЗ РЕЙЛА КЛИКОМ ПО ПОЛОЧКЕ

        /// <summary>
        /// ПРАВИЛО ВОЗВРАТА ИЗ РЕЙЛА: панель сейчас в рейле, а состояние ДО нажатия было «не в
        /// рейле» → возврат идёт КЛИКОМ ПО ПОЛОЧКЕ (`RailClick` — тот же путь, что у оператора),
        /// а не прямым `FromRail`. Обратный случай остаётся кнопкой шапки «в рейл».
        /// </summary>
        [Test]
        public void RailAction_NamesClickForReturn_AndHeaderButtonForExit()
        {
            Assert.AreEqual(KvUiSweepPanelSig.RailActionClick,
                KvUiSweepPanelSig.RailAction(1, 0),
                "возврат из рейла — это клик по полочке (лист «Dock / Клик по полочке (InRail)»)");
            Assert.AreEqual(KvUiSweepPanelSig.RailActionToRail,
                KvUiSweepPanelSig.RailAction(0, 1),
                "уход в рейл — кнопка шапки, её нажатие и так видно снимку");
            Assert.AreEqual("", KvUiSweepPanelSig.RailAction(1, 1), "панель уже в исходном состоянии");
            Assert.AreEqual("", KvUiSweepPanelSig.RailAction(0, 0), "панель и не уходила в рейл");
        }

        /// <summary>
        /// ТО ЖЕ ПОВЕДЕНИЕМ: панель дока уводится в рейл, `RestorePanels` возвращает её ИМЕННО
        /// кликом по полочке и называет id панели — по этому списку обход пишет строку события
        /// в отчёт.
        /// </summary>
        [Test]
        public void RestorePanels_ReturnsRailPanelByShelfClick()
        {
            KazistovVvUIManager ui = BuildShell();
            KvDockPanel panel = ui.DockById("tree");
            Assert.IsNotNull(panel, "панели «tree» в оболочке нет — полочку не на чем проверить");

            // ИСХОДНОЕ СОСТОЯНИЕ — СНЯТО С САМОЙ ПАНЕЛИ, чтобы шаг «side/collapsed/shown» не
            // менял ничего лишнего.
            object state = InvokeStatic("PanelStateOf", "tree", panel);
            Assert.IsNotNull(state, "PanelStateOf ничего не вернул");

            bool was = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                panel.ToRail();
                Assert.IsTrue(panel.InRail, "панель обязана уйти в рейл — иначе полочку не проверить");

                var list = (IList)Activator.CreateInstance(
                    typeof(List<>).MakeGenericType(state.GetType()));
                list.Add(state);

                object returned = InvokeStatic("RestorePanels", list);
                var clicks = (IEnumerable)returned;
                bool named = false;
                foreach (object id in clicks)
                {
                    if (string.Equals(id as string, "tree", StringComparison.Ordinal)) named = true;
                }

                Assert.IsTrue(named,
                    "RestorePanels обязана вернуть id панели, вернувшейся КЛИКОМ ПО ПОЛОЧКЕ — " +
                    "по этому списку обход пишет событие в отчёт");
                Assert.IsFalse(panel.InRail,
                    "после возврата панель обязана быть вне рейла (клик по полочке её вернул)");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = was;
                if (panel.InRail) panel.FromRail();
            }
        }

        // ================= 6. ВЕТКИ ДЕРЕВА: ПОДГОТОВКА СЦЕНЫ ОБХОДОМ

        /// <summary>
        /// ВЕТКА ОПРЕДЕЛЯЕТСЯ ПО СТРОКЕ ДЕРЕВА, А КОМАНДА ПОДГОТОВКИ — ШТАТНАЯ КОМАНДА
        /// ОПЕРАТОРА (реестр `KvCommands`, а не прямые вызовы сервисов).
        /// </summary>
        [Test]
        public void TreeNav_BranchesCommandsAndMarkers()
        {
            const string prefix = "KazistovVv_UI/KazistovVvDockCanvas/Dock_Дерево моделей/Body/" +
                                  "KvTreeView/Scroll/Content/";

            Assert.AreEqual(KvUiSweepTreeNav.Branch.Point,
                KvUiSweepTreeNav.Of(prefix + "Row_Промежуточные точки«Промежуточные точки»"),
                "группа точек — ветка Point");
            Assert.AreEqual(KvUiSweepTreeNav.Branch.Trajectory,
                KvUiSweepTreeNav.Of(prefix + "Row_Траектории«Траектории»"),
                "группа траекторий — ветка Trajectory");
            Assert.AreEqual(KvUiSweepTreeNav.Branch.Phantom,
                KvUiSweepTreeNav.Of(prefix + "Row_Фантом 1«Фантом 1»"),
                "строка фантома — ветка Phantom");
            Assert.AreEqual(KvUiSweepTreeNav.Branch.Record,
                KvUiSweepTreeNav.Of(prefix + "Row_Записи траекторий«Записи траекторий»"),
                "группа записей — ветка Record");
            Assert.AreEqual(KvUiSweepTreeNav.Branch.Record,
                KvUiSweepTreeNav.Of(prefix + "Row_● ИДЁТ ЗАПИСЬ · 12 т.«● ИДЁТ ЗАПИСЬ · 12 т.»"),
                "живая строка записи — ветка Record (в чистом прогоне достижима только она)");
            Assert.AreEqual(KvUiSweepTreeNav.Branch.Zone,
                KvUiSweepTreeNav.Of(prefix + "Row_Зоны запрета«Зоны запрета»"),
                "группа зон — ветка Zone");
            Assert.AreEqual(KvUiSweepTreeNav.Branch.Axis,
                KvUiSweepTreeNav.Of(prefix + "Row_Ось 1«Ось 1»"),
                "ось сустава — ветка Axis");
            Assert.AreEqual(KvUiSweepTreeNav.Branch.Tcp,
                KvUiSweepTreeNav.Of(prefix + "Row_TCP«TCP»"),
                "TCP — ветка Tcp");
            Assert.AreEqual(KvUiSweepTreeNav.Branch.None,
                KvUiSweepTreeNav.Of(prefix + "Row_Столы«Столы»"),
                "обычная строка дерева не тащит за собой подготовку сцены");

            Assert.IsTrue(KvUiSweepTreeNav.IsTreeRowPath(prefix + "Row_TCP«TCP»"), "строка дерева");
            Assert.IsTrue(KvUiSweepTreeNav.IsTreeRowPath(prefix + "Row_TCP«TCP»/Eye"),
                "«глазик» строки — тоже строка дерева (его проверяет тот же обход)");
            Assert.IsFalse(KvUiSweepTreeNav.IsTreeRowPath(
                "KazistovVv_UI/KazistovVvDockCanvas/Dock_Настройки/Body/Scroll/Content/Switch_Фонарик"),
                "строка настроек — не строка дерева");
            Assert.IsFalse(KvUiSweepTreeNav.IsTreeRowPath(null), "null — не строка дерева");

            // ШТАТНЫЕ КОМАНДЫ: имя, порядок и отсутствие команды там, где её быть не может.
            Assert.AreEqual("waypoint.add", KvUiSweepTreeNav.PrepCommandFor(KvUiSweepTreeNav.Branch.Point));
            Assert.AreEqual("waypoint.build", KvUiSweepTreeNav.PrepCommandFor(KvUiSweepTreeNav.Branch.Trajectory));
            Assert.AreEqual("record.toggle", KvUiSweepTreeNav.PrepCommandFor(KvUiSweepTreeNav.Branch.Record));
            Assert.AreEqual("zone.box", KvUiSweepTreeNav.PrepCommandFor(KvUiSweepTreeNav.Branch.Zone));
            Assert.AreEqual("", KvUiSweepTreeNav.PrepCommandFor(KvUiSweepTreeNav.Branch.Phantom),
                "фантомы создаёт расчёт траектории — своей команды у ветки нет");
            Assert.AreEqual("", KvUiSweepTreeNav.PrepCommandFor(KvUiSweepTreeNav.Branch.Axis),
                "строки осей даёт сам робот в дереве — своей команды у ветки нет");
            Assert.AreEqual("", KvUiSweepTreeNav.PrepCommandFor(KvUiSweepTreeNav.Branch.None));

            KvUiSweepTreeNav.Branch[] order = KvUiSweepTreeNav.PrepOrder();
            Assert.AreEqual(4, order.Length, "готовятся ровно четыре ветки с командами");
            Assert.AreEqual(KvUiSweepTreeNav.Branch.Point, order[0],
                "первой создаётся точка — по ней строится маршрут");

            foreach (KvUiSweepTreeNav.Branch branch in new[]
            {
                KvUiSweepTreeNav.Branch.Point, KvUiSweepTreeNav.Branch.Trajectory,
                KvUiSweepTreeNav.Branch.Phantom, KvUiSweepTreeNav.Branch.Record,
                KvUiSweepTreeNav.Branch.Zone, KvUiSweepTreeNav.Branch.Axis,
                KvUiSweepTreeNav.Branch.Tcp
            })
                Assert.IsNotEmpty(KvUiSweepTreeNav.RowMarkerFor(branch),
                    "у ветки " + branch + " обязан быть признак строки в дереве");

            // КОМАНДЫ ПОДГОТОВКИ — РЕАЛЬНЫЕ (реестр): заглушек в подготовке сцены не бывает.
            foreach (KvUiSweepTreeNav.Branch branch in KvUiSweepTreeNav.PrepOrder())
            {
                string id = KvUiSweepTreeNav.PrepCommandFor(branch);
                KvCommand command = KvCommands.Get(id);
                if (command == null) continue;              // реестр ещё не поднимался — смотрит волна гейта
                Assert.IsNotNull(command.Execute,
                    "команда подготовки «" + id + "» зарегистрирована без обработчика — это заглушка");
            }
        }

        /// <summary>
        /// МИНИМАЛЬНАЯ СЦЕНА: строки подготовленных веток СУЩЕСТВУЮТ в дереве — и обход СОБИРАЕТ
        /// их в инвентарь (раньше их путей не было вовсе, и листы оставались непроверенными).
        /// </summary>
        [Test]
        public void TreeRowsOfPreparedBranches_CollectedIntoInventory()
        {
            const string content = "KazistovVv_UI/KazistovVvDockCanvas/Dock_Дерево моделей/" +
                                   "Body/KvTreeView/Scroll/Content/";
            string[] rows =
            {
                "Row_Промежуточные точки«Промежуточные точки»",
                "Row_Траектории«Траектории»",
                "Row_Записи траекторий«Записи траекторий»",
                "Row_Зоны запрета«Зоны запрета»",
                "Row_Ось 1«Ось 1»",
                "Row_TCP«TCP»"
            };

            GameObject host = NewHost("tree");
            try
            {
                var canvasGo = new GameObject("KazistovVv_UI", typeof(Canvas));
                canvasGo.transform.SetParent(host.transform, false);
                var dockCanvas = new GameObject("KazistovVvDockCanvas", typeof(RectTransform));
                dockCanvas.transform.SetParent(canvasGo.transform, false);
                var panelRoot = new GameObject("Dock_Дерево моделей", typeof(RectTransform));
                panelRoot.transform.SetParent(dockCanvas.transform, false);
                var treeBody = new GameObject("Body", typeof(RectTransform));
                treeBody.transform.SetParent(panelRoot.transform, false);
                var treeView = new GameObject("KvTreeView", typeof(RectTransform));
                treeView.transform.SetParent(treeBody.transform, false);
                var scroll = new GameObject("Scroll", typeof(RectTransform));
                scroll.transform.SetParent(treeView.transform, false);
                var viewport = new GameObject("Viewport", typeof(RectTransform));
                viewport.transform.SetParent(scroll.transform, false);
                var contentGo = new GameObject("Content", typeof(RectTransform));
                contentGo.transform.SetParent(viewport.transform, false);

                foreach (string row in rows)
                {
                    var go = new GameObject(row, typeof(RectTransform), typeof(Image), typeof(Button));
                    go.transform.SetParent(contentGo.transform, false);
                }

                KvUiSweepRuntime runtime = host.AddComponent<KvUiSweepRuntime>();
                HashSet<string> keys = CollectKeys(runtime);

                foreach (string row in rows)
                {
                    // КЛЮЧ СТРОКИ — ПО СУФФИКСУ: `PathOf` строит путь от корня иерархии, а
                    // корень у теста — свой хост; ветка же узнаётся по последнему сегменту.
                    int quote = row.IndexOf('«');
                    string name = quote > 0 ? row.Substring(0, quote) : row;
                    bool collected = false;
                    foreach (string key in keys)
                    {
                        if (key.EndsWith("/" + name, StringComparison.Ordinal) &&
                            key.IndexOf("/KvTreeView/", StringComparison.Ordinal) >= 0)
                        {
                            collected = true;
                            break;
                        }
                    }
                    Assert.IsTrue(collected,
                        "строка подготовленной ветки не попала в инвентарь: " + row);
                    Assert.AreNotEqual(KvUiSweepTreeNav.Branch.None,
                        KvUiSweepTreeNav.Of(content + row),
                        "ветка строки не распознана: " + row);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ------------------------------------------------------------------ поиск по иерархии

        /// <summary>ПОТОМК С ИМЕНЕМ `name` (тип — свой).</summary>
        private static T FindChild<T>(Transform root, string name) where T : Component
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == name)
                {
                    T component = t.GetComponent<T>();
                    if (component != null) return component;
                }
            return null;
        }

        /// <summary>ПОТОМК С ИМЕНЕМ, НАЧИНАЮЩИМСЯ С `prefix` (тип — свой).</summary>
        private static T FindChildByName<T>(Transform root, string prefix) where T : Component
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    T component = t.GetComponent<T>();
                    if (component != null) return component;
                }
            return null;
        }

        /// <summary>ПОЛЕ/СВОЙСТВО КАНДИДАТА (для проверки флагов подготовки).</summary>
        private static object GetMember(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "у Candidate нет поля " + name);
            return field.GetValue(target);
        }
    }
}
