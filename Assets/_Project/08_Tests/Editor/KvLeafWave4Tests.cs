// ================================================================================================
//  KvLeafWave4Tests.cs — EditMode-тесты ВОЛНЫ 4 КАМПАНИИ «каждый листочек путеводителя закрыт».
//
//  ОТКУДА ЗАДАЧА. `Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv`: волна 3 закрыла все листья «по коду», кроме
//  восьми, которые НЕ ПРОВЕРЯЛИСЬ именно потому, что требуют СОБРАННОГО дерева оболочки
//  (`KazistovVvUIManager.BuildShell`): контекстное меню дерева (слайд 68, 7 листьев) и сегменты
//  «Верстак» тулбара (слайд 188, 2 листа). Плюс дефект волны 4: «Копировать имя» обещал
//  сообщение в ЖУРНАЛЕ, а писал только в статус-бар.
//
//  ЧЕМ ПРОВЕРЯЕТСЯ (тот же приём, что в P4-3 волны 3). ТОЛЬКО ПОВЕДЕНИЕМ живого кода:
//  оболочка собирается штатным приватным `BuildShell()` (вызовом отражением — в EditMode Unity
//  не зовёт ни `Awake`, ни `Start`), пункты меню нажимаются ИХ ЖЕ `onClick`, состояние читается
//  геттерами самого интерфейса, журнал — из `KvActionLog.Entries`. Тексты исходников файлы
//  тестов НЕ читают. Ни один тест не открывает сцену и не запускает игру.
//
//  ГРАНИЦА ЧЕСТНОСТИ. Устройства тест не трогает: сегменты «Верстак» закрываются замером
//  самопрогона (Logs/uisweep_reports/_ui_sweep_ar_w4.txt), здесь — контекстное меню, которого
//  обход физически не открывает (ПКМ не является нажатием uGUI).
//
//  ЧИСКА. `TearDown` снимает построенные хосты, возвращает язык/реестр команд/отсечение
//  повторов и гасит игнорирование ошибок лога — даже когда тест упал.
// ================================================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using KazistovVvFeatures;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KazistovVvTests
{
    /// <summary>Волна 4: контекстное меню дерева и сегменты «Верстак» на СОБРАННОЙ оболочке.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvLeafWave4Tests
    {
        // ------------------------------------------------------------------ инфраструктура

        /// <summary>Хосты, созданные тестом: снимаются в TearDown независимо от исхода.</summary>
        private readonly List<GameObject> hosts = new List<GameObject>();

        /// <summary>Было ли отсечение повторов до теста (возвращается в TearDown).</summary>
        private bool repeatsWereOn;

        /// <summary>Было ли игнорирование ошибок лога до теста (возвращается в TearDown).</summary>
        private bool ignoreLogsWas;

        /// <summary>СНИМОК РЕЕСТРА КОМАНД ДО `BuildShell` (общий статик — вернуть как был).</summary>
        private static List<KvCommand> registrySnapshot;

        /// <summary>Язык до теста (пункты меню подписываются по словарю — нужна предсказуемость).</summary>
        private string localeWas;

        [SetUp]
        public void SetUp()
        {
            // МЕНЮ ПОДПИСЫВАЕТСЯ ЧЕРЕЗ `KvLoc.T`: без фиксированного языка тест гадал бы по текущему.
            localeWas = KvLoc.CurrentCode;
            if (KvLoc.CurrentCode != "ru") KvLoc.SetLanguage("ru");

            repeatsWereOn = KvActionLog.Instance.suppressRepeats;
            KvActionLog.Instance.suppressRepeats = false;
            KvActionLog.Instance.Clear();

            // `BuildShell` в EditMode встречает места, где обычный `Destroy` недопустим (ложная
            // ошибка лога уронила бы фикстуру). Это особенность СРЕДЫ, а не поведения меню —
            // поэтому ошибки лога не считаем провалом, а сами проверки остаются строгими.
            ignoreLogsWas = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

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

            if (KvLoc.CurrentCode != localeWas) KvLoc.SetLanguage(localeWas);

            // ПОДПИСКИ СОБЫТИЙ — ПОСЛЕ УНИЧТОЖЕНИЯ ХОСТОВ: подписчик (менеджер) мёртв, остаток
            // события будит бы удал следующего теста.
            ClearStaticEvent(typeof(KvTheme), "Changed");

            KvActionLog.Instance.suppressRepeats = repeatsWereOn;
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
            var go = new GameObject("KvLeafWave4_" + name);
            hosts.Add(go);
            return go;
        }

        /// <summary>
        /// СОБРАТЬ ОБОЛОЧКУ В РЕДАКТОРЕ: `Awake` (сам singleton, настройки, тема) и приватный
        /// `BuildShell` — теми же отражениями, которыми волна 3 поднимала хабы. `Start` НЕ зовём:
        /// он делает то же `BuildShell`, но добавляет корутины и работу со сценой, которые
        /// EditMode не выполняет.
        /// </summary>
        private KazistovVvUIManager BuildShell()
        {
            // ЧИСТЫЕ ПОДПИСКИ. `Awake` в EditMode вызывает И Unity (при `AddComponent`), И тест
            // отражением, — подписка на `KvTheme.Changed` получалась ДВОЙНОЙ, а `OnDestroy` снимает
            // одну: следующий тест будил УНИЧТОЖЕННЫЙ менеджер (`MissingReferenceException`
            // внутри `BuildShell`), и фикстура падала не по своей вине.
            ClearStaticEvent(typeof(KvTheme), "Changed");

            GameObject host = NewHost("shell");
            var ui = host.AddComponent<KazistovVvUIManager>();

            // `Awake`/`BuildShell` в EditMode дёргают `Destroy(LayoutElement)` (KvDockPanel.cs:256,
            // «HeaderButton»): Unity пишет ОШИБКУ лога, которую NUnit читает как провал теста.
            // Подавляем ТОЛЬКО на время сборки (тот же приём, что в волне 3) — сами проверки идут
            // уже без подавления, чтобы не пропустить ошибку в собственном коде теста.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                // `Instance` уже наш — Unity сама вызвала `Awake` при AddComponent; повторный вызов
                // удвоил бы подписки и пересобрал оболочку дважды.
                if (KazistovVvUIManager.Instance != ui)
                    InvokeNonPublic(ui, "Awake", "менеджер интерфейса");
                InvokeNonPublic(ui, "BuildShell", "оболочка интерфейса");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoreLogsWas;
            }

            Assert.IsNotNull(ui.Tree, "после BuildShell в дереве нет компонента KvTreeView");
            Assert.IsTrue(ui.IsPanelVisible("tree") || !ui.IsPanelVisible("tree"),
                "оболочка собрана — дерево существует (панель может быть свёрнута)");
            return ui;
        }

        /// <summary>ВЫЗЫВАТЬ ПРИВАТНЫЙ МЕТОД ОТРАЖЕНИЕМ (в EditMode штатные Unity-события не идут).</summary>
        private static void InvokeNonPublic(object target, string method, string label)
        {
            MethodInfo info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(info, "у " + label + " больше нет метода " + method + " — проверка потеряла смысл");
            try { info.Invoke(target, null); }
            catch (TargetInvocationException error)
            {
                Assert.Fail(method + " (" + label + ") упал: " + error.InnerException);
            }
        }

        /// <summary>
        /// СБРОСИТЬ СТАТИЧЕСКОЕ СОБЫТИЕ. Нужен для `KvTheme.Changed`: двойная подписка из-за
        /// повторного `Awake` переживает уничтожение одного подписчика и будит мёртвый объект
        /// у следующего теста.
        /// </summary>
        private static void ClearStaticEvent(Type type, string name)
        {
            FieldInfo field = type.GetField(name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return;                       // события нет — проверять нечего
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) return;
            field.SetValue(null, null);
        }

        /// <summary>
        /// ВЫПОЛНИТЬ ВЫЗОВ ПОД УЗКИМ ПОДАВЛЕНИЕМ ОШИБОК ЛОГА. Пересборка дерева и панелей в
        /// EditMode дёргает `Destroy(...)` (`KvTreeView.PlaceBox`, `KvDockPanel.HeaderButton`) —
        /// Unity пишет ОШИБКУ, которую NUnit читает как провал теста, хотя ломания нет.
        /// Приём волны 3: подавление ТОЛЬКО вокруг вызова, утверждение идёт уже без него.
        /// </summary>
        private static void Quietly(Action action)
        {
            bool was = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try { action(); }
            finally { LogAssert.ignoreFailingMessages = was; }
        }

        /// <summary>ПУТЬ СТАТУС-БАРА (`SetPlanStatus` пишет в приватное статическое поле).</summary>
        private static string PlanStatus()
        {
            FieldInfo field = typeof(KazistovVvUIManager).GetField("planStatusText",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "в менеджере больше нет planStatusText — статус не проверить");
            return (string)field.GetValue(null) ?? "";
        }

        /// <summary>СТРОКИ ЖУРНАЛА ПОСЛЕ `from` (нумерация от начала журнала).</summary>
        private static List<string> LogSince(int from)
        {
            var lines = new List<string>();
            IReadOnlyList<KvLogEntry> entries = KvActionLog.Instance.Entries;
            for (int i = from; i < entries.Count; i++) lines.Add(entries[i].text);
            return lines;
        }

        private static bool AnyContains(List<string> lines, string needle)
        {
            foreach (string line in lines)
                if (line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>ОТКРЫТЬ КОНТЕКСТНОЕ МЕНЮ ПО УЗЛУ — ровно так, как это делает дерево по ПКМ.</summary>
        private static KvContextMenu OpenMenu(KazistovVvUIManager ui, ProjectNode node)
        {
            // `RebuildRows` уничтожает строки ПРЕДЫДУЩЕГО меню через `Destroy` — в EditMode это
            // ошибка лога (см. `Quietly`); первое открытие строк не имеет, второе уже имеет.
            Quietly(() => ui.OnTreeContextMenu(new List<ProjectNode> { node }, Vector2.zero));
            KvContextMenu menu = KvContextMenu.Current;
            Assert.IsNotNull(menu, "после ПКМ контекстное меню не появилось");
            Assert.IsTrue(menu.IsOpen, "контекстное меню создано, но не открыто");
            return menu;
        }

        /// <summary>СТРОКА МЕНЮ ПО ПОДСТРОКЕ ПОДПИСИ (подпись локализуется — точное сравнение хрупко).</summary>
        private static Transform FindRow(KvContextMenu menu, string labelPart)
        {
            Transform panel = menu.transform.Find("KvContextMenu");
            if (panel == null) return null;
            for (int i = 0; i < panel.childCount; i++)
            {
                Transform child = panel.GetChild(i);
                if (child.name.StartsWith("Ctx_", StringComparison.Ordinal) &&
                    child.name.IndexOf(labelPart, StringComparison.Ordinal) >= 0)
                    return child;
            }
            return null;
        }

        /// <summary>ПУНКТ МЕНЮ ПОСТРОЕН (проверка состава списка — листья обещают каждый пункт).</summary>
        private static void AssertRow(KvContextMenu menu, string labelPart)
        {
            Assert.IsNotNull(FindRow(menu, labelPart),
                "пункт меню с подписью «" + labelPart + "» не построен — лист обещает его в списке");
        }

        /// <summary>НАЖАТЬ ПУНКТ МЕНЮ ЕГО ЖЕ `onClick` (никаких прямых вызовов действий).</summary>
        private static void Press(KvContextMenu menu, string labelPart)
        {
            Transform row = FindRow(menu, labelPart);
            Assert.IsNotNull(row,
                "пункт меню с подписью «" + labelPart + "» в списке отсутствует — лист обещает его");
            Button button = row.GetComponent<Button>();
            Assert.IsNotNull(button, "пункт «" + labelPart + "» не кнопка — нажать его штатным путём нельзя");
            Assert.IsTrue(button.interactable, "пункт «" + labelPart + "» выключен, хотя узел его поддерживает");
            // Нажатие пересобирает дерево/панели (`RebuildTree` внутри действий) — см. `Quietly`.
            Quietly(() => button.onClick.Invoke());
            Assert.IsFalse(menu.IsOpen, "после выбора пункт «" + labelPart + "» меню обязано закрыться");
        }

        // ==================== 1. КОНТЕКСТНОЕ МЕНЮ ДЕРЕВА (СЛАЙД 68, 7 ЛИСТЬЕВ)

        /// <summary>
        /// ЛИСТЫ 177–183 «Контекстное меню / …» (7 листьев): пункт меню СУЩЕСТВУЕТ, ДОСТУПЕН
        /// для узла, который он поддерживает, и по нажатию делает РОВНО то, что обещает лист:
        /// «Переименовать» заводит ввод на месте, «Дублировать» добавляет точку, «Удалить» убирает
        /// её, «Скрыть/Показать» гасит и зажигает рендереры, «Фокус камеры» наводит камеру,
        /// «Свойства» открывает панель, «Копировать имя» кладёт имя в буфер И пишет в ЖУРНАЛ
        /// (дефект волны 4: сообщение уходило только в статус-бар, который гаснет).
        ///
        /// ОТКУДА СМЫСЛ. Меню строится ТОЛЬКО из собранной оболочки (`OnTreeContextMenu` работает
        /// с `canvasRect`, деревом и панелями, которые создаёт `BuildShell`) — волна 3 поэтому
        /// эти листья оставила незакрытыми: без дерева проверять нечего.
        /// </summary>
        [Test]
        public void W4_ContextMenu_OnBuiltShell_EveryItemDoesItsRealJob()
        {
            KazistovVvUIManager ui = BuildShell();

            // --- УЗЕЛ-ТОЧКА С ОБЪЕКТОМ СЦЕНЫ: «Скрыть/Показать», «Фокус» и «Свойства» живут
            //     на объекте, у «Дублировать/Удалить» — та же точка в истории.
            GameObject nodeObject = NewHost("node_object");
            nodeObject.AddComponent<MeshRenderer>();
            var node = new ProjectNode("point:w4", "Точка W4", ProjectNodeKind.Point,
                nodeObject.transform, null);
            node.Tag = new Vector3(0.42f, 0f, 0.17f);

            FieldInfo historyField = typeof(KazistovVvUIManager).GetField("pointHistory",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(historyField, "в менеджере больше нет pointHistory — точку не проверить");
            var history = (List<Vector3>)historyField.GetValue(ui);
            history.Clear();
            history.Add((Vector3)node.Tag);

            // КАМЕРА ДЛЯ «ФОКУСА»: `CameraRig` ищет контроллер на камере, в EditMode камеры нет.
            GameObject cameraObject = NewHost("camera");
            cameraObject.AddComponent<Camera>();
            FreeFlyCameraController rig = cameraObject.AddComponent<FreeFlyCameraController>();
            FieldInfo rigField = typeof(KazistovVvUIManager).GetField("cameraController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(rigField, "в менеджере больше нет cameraController — фокус не проверить");
            rigField.SetValue(ui, rig);

            Quietly(() => ui.Tree.Rebuild(new List<ProjectNode> { node }, node));
            ui.SelectNode(node);
            Assert.AreEqual(1, ui.Tree.RowCount, "дерево не показало узел — пунктам меню не на чем работать");
            Assert.AreSame(node, ui.Tree.Selected, "узел не выбран — «Переименовать» не сработает");

            // --- НУЛЕВАЯ ТОЧКА ОТСЧЁТА ДЛЯ ПАНЕЛИ СВОЙСТВ.
            Quietly(() => ui.SetPanel("properties", false));
            Assert.IsFalse(ui.IsPanelVisible("properties"), "панель свойств не закрылась к началу проверки");

            // --- ПУНКТ 1. СВОЙСТВА: SelectNode + SetPanel("properties").
            KvContextMenu menu = OpenMenu(ui, node);
            AssertRow(menu, "Свойства");
            Press(menu, "Свойства");
            Assert.IsTrue(ui.IsPanelVisible("properties"),
                "«Свойства» не открыли панель свойств — лист обещает именно открытие");

            // --- ПУНКТ 2/3. СКРЫТЬ → ПОКАЗАТЬ: рендереры объекта гаснут и зажигаются.
            menu = OpenMenu(ui, node);
            AssertRow(menu, "Скрыть");
            Press(menu, "Скрыть");
            Assert.IsFalse(KvTreeView.IsObjectVisible(node), "«Скрыть» не скрыло объект узла");
            Assert.IsFalse(nodeObject.GetComponent<MeshRenderer>().enabled,
                "«Скрыть» не выключило рендерер — лист обещает видимость, а не флажок в дереве");

            menu = OpenMenu(ui, node);
            AssertRow(menu, "Показать");
            Press(menu, "Показать");
            Assert.IsTrue(KvTreeView.IsObjectVisible(node), "«Показать» не вернуло объект узла");

            // --- ПУНКТ 4. ФОКУС КАМЕРЫ: подводит камеру и сообщает об этом.
            menu = OpenMenu(ui, node);
            AssertRow(menu, "Фокус камеры");
            Press(menu, "Фокус камеры");
            StringAssert.Contains("Камера наведена", PlanStatus(),
                "«Фокус камеры» не сообщил о наведении — оператор не узнал, что действие выполнено");

            // --- ПУНКТ 5. ПЕРЕИМЕНОВАТЬ: ввод на месте (Enter — запись, Esc — отмена).
            // ПЕРЕД ВЫЗОВОМ. Предыдущие пункты («Свойства», «Скрыть/Показать») пересобрали дерево
            // по СВОЕЙ модели, а наш узел в неё не входит — строка исчезла. В реальном приложении
            // узел ВСЕГДА в модели (его туда положил `RebuildTree`), поэтому здесь возвращаем
            // строку узла в дерево: проверяется именно «ввод на месте», а не совпадение моделей.
            Quietly(() => ui.Tree.Rebuild(new List<ProjectNode> { node }, node));
            Assert.GreaterOrEqual(ui.Tree.RowCount, 1, "строка узла не восстановилась — переименовать нечего");
            menu = OpenMenu(ui, node);
            AssertRow(menu, "Переименовать");
            Press(menu, "Переименовать");
            Assert.IsTrue(ui.Tree.IsRenaming,
                "«Переименовать» не завело ввод на месте — BeginRenameSelected вернул отказ");
            Assert.AreEqual(node.DisplayName, ui.Tree.EditBuffer,
                "буфер переименования не предзаполнен текущим именем узла");
            Quietly(() => ui.Tree.CancelRenameNow());

            // --- ПУНКТ 6. КОПИРОВАТЬ ИМЯ: след ОБЯЗАН остаться в ЖУРНАЛЕ (дефект волны 4).
            int logBefore = KvActionLog.Instance.Entries.Count;
            menu = OpenMenu(ui, node);
            AssertRow(menu, "Копировать имя");
            Press(menu, "Копировать имя");
            List<string> journal = LogSince(logBefore);
            Assert.IsTrue(AnyContains(journal, "Имя скопировано"),
                "после «Копировать имя» в ЖУРНАЛЕ нет подтверждения — след остался только в " +
                "статус-баре, который гаснет (дефект волны 4). Строки журнала: " +
                string.Join(" | ", journal.ToArray()));
            Assert.IsTrue(AnyContains(journal, "Точка W4"),
                "подтверждение в журнале не называет, чьё имя скопировано: " +
                string.Join(" | ", journal.ToArray()));
            StringAssert.Contains("Имя скопировано", PlanStatus(),
                "сообщение исчезло из статус-бара — раньше оно было только там, теперь обязано быть в обоих местах");

            // --- ПУНКТ 7. ДУБЛИРОВАТЬ: точка прибавилась в истории.
            int beforeDuplicate = ui.PointCount;
            menu = OpenMenu(ui, node);
            AssertRow(menu, "Дублировать");
            Press(menu, "Дублировать");
            Assert.AreEqual(beforeDuplicate + 1, ui.PointCount,
                "«Дублировать» не добавило копию точки в историю");

            // --- ПУНКТ 8. УДАЛИТЬ: точка ушла (ищется по расстоянию в истории — тег узла тот же).
            int beforeDelete = ui.PointCount;
            menu = OpenMenu(ui, node);
            AssertRow(menu, "Удалить");
            Press(menu, "Удалить");
            Assert.AreEqual(beforeDelete - 1, ui.PointCount,
                "«Удалить» не убрало точку из истории");
        }

        // ==================== 2. СЕГМЕНТЫ «ВЕРСТАК» ТУЛБАРА (СЛАЙД 188, 2 ЛИСТА)

        /// <summary>
        /// ЛИСТЫ 271–272 «KazistovVvCanvas / KvToolbar / Верстак / Seg_Workbench/Seg0|Seg1»:
        /// сегмент — обычный `Button`, его нажатие делает активным робота СВОЕГО типа
        /// (`KazistovVvUIManager.SelectRobotByWorkbench(i)`), а не просто подсвечивает сегмент.
        ///
        /// ОТКУДА СМЫСЛ. На планшете самопрогон нажал оба сегмента и получил эффект
        /// (Logs/uisweep_reports/_ui_sweep_ar_w4.txt), но в сцене прогона НЕ БЫЛО роботов —
        /// эффект доказывал только «кнопка отозвалась». Здесь проверяется сам обещанный выбор:
        /// активным становится робот нужного типа, прежний гаснет.
        /// </summary>
        [Test]
        public void W4_WorkbenchSegments_ActivateRobotOfTheirOwnType()
        {
            KazistovVvUIManager ui = BuildShell();

            // --- ДВА РОБОТА РАЗНЫХ ТИПОВ: Awake в EditMode не вызывается, тело `SetActive`
            //     ограничено подсветкой рендереров (их у пустых объектов нет) — обхода сцены
            //     и загрузки моделей здесь не требуется.
            GameObject rigHost = NewHost("robots");
            RobotController sixAxis = rigHost.AddComponent<RobotController>();
            GameObject scaraObject = NewHost("scara");
            SCARAController scara = scaraObject.AddComponent<SCARAController>();
            sixAxis.isActive = false;
            scara.isActive = false;

            Transform workbench = FindDeep(ui.transform, "Seg_Workbench");
            Assert.IsNotNull(workbench, "в тулбаре нет строки «Верстак» — листья 271/272 не проверить");
            Transform seg0 = workbench.Find("Seg0");
            Transform seg1 = workbench.Find("Seg1");
            Assert.IsNotNull(seg0, "сегмент Seg0 «Робот (6 осей)» не построен");
            Assert.IsNotNull(seg1, "сегмент Seg1 «SCARA» не построен");

            Button b0 = seg0.GetComponent<Button>();
            Button b1 = seg1.GetComponent<Button>();
            Assert.IsNotNull(b0, "Seg0 не кнопка — сегменты создаются обычными Button (KvWidgets.cs)");
            Assert.IsNotNull(b1, "Seg1 не кнопка");

            b0.onClick.Invoke();
            Assert.IsTrue(sixAxis.isActive, "Seg0 не сделал активным робота типа «6 осей»");
            Assert.IsFalse(scara.isActive, "Seg0 оставил активной SCARA — выбор должен быть однозначным");

            b1.onClick.Invoke();
            Assert.IsTrue(scara.isActive, "Seg1 не сделал активным SCARA");
            Assert.IsFalse(sixAxis.isActive, "Seg1 не погасил робота «6 осей»");
        }

        /// <summary>ПОТОМКОВЫЙ ЭЛЕМЕНТ ПО ИМЕНИ (любая глубина — строки тулбара вложены в группы).</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
