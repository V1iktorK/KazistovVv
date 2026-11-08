// ================================================================================================
//  KvR6FixTests.cs — R6-1, ТРЕТЬИЙ КРУГ: ПАНЕЛЬ ОПЕРАТОРА ОБЯЗАНА РИСОВАТЬСЯ, А НЕ «БЫТЬ ВКЛЮЧЁННОЙ»
//
//  ОТКУДА ЗАДАЧА. Тестировщик вернул R6-1 в работу дважды. Замер на устройстве (планшет
//  Xiaomi 25099RP13G, APK AR-GLES3): команда «Настройки» логируется как выполненная
//      `[KvAr] ИНТЕРФЕЙС: выбрана «Настройки и справка» (view.panel.settings)`
//  панели на экране нет, снимки до и после совпадают, и НИ ОДНОЙ строки `ПАНЕЛИ:` в журнале тоже
//  нет. Повторный вызов даёт тот же результат.
//
//  ПОЧЕМУ ДВЕ ПРЕДЫДУЩИЕ ПРАВКИ НЕ МОГЛИ ПОМОЧЬ. Обе лечили ФЛАГ и РАСКЛАДКУ: «решать по
//  фактической видимости, а не по `Shown`» и «поднять панель над AR-оверлеем». Но на устройстве
//  не работала третья вещь, о которой в правках не было ни слова: РИСОВАНИЕ. В AR-сцене живёт
//  `KvXrDesktopOverlayGuard` — он раз в секунду гасит `Canvas.enabled` у канвасов десктопного
//  интерфейса (в журнале устройства это видно: `десктопный оверлей в шлеме: погашено рисование
//  канвасов 9`). Панели жили ВНУТРИ этого канваса, поэтому гасли вместе с ним. Прямое следствие —
//  отсутствие строк `ПАНЕЛИ:`: по флагу и прямоугольнику всё «хорошо», починка не запускалась, а
//  на экране ничего.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ. Три уровня, и каждый обязан падать на старом коде:
//    1) ЧИСТЫЕ ПРАВИЛА — «рисуется ли панель» и «гасить ли канвас панелей»: обе ветки, потому что
//       «всегда гасить» и «всегда оставлять» одинаково ломают продукт;
//    2) ОТНОШЕНИЕ КАНВАСА К ПАНЕЛЯМ — на настоящей иерархии объектов, включая вложенные канвасы
//       самих панелей;
//    3) ПОРЯДОК ШАГОВ В КОДЕ — там, где поведение нельзя оторвать от сцены (`ShowPanel` требует
//       собранной оболочки), проверяется именно ПОРЯДОК: сперва «панель запрошена» и включение
//       канваса, потом проверка видимости. Именно порядок и был дефектом.
// ================================================================================================

using System;
using System.IO;
using System.Text.RegularExpressions;
using KazistovVvUI;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>R6-1: панель оператора в AR обязана РИСОВАТЬСЯ, а не только «быть включённой».</summary>
    [Category(KvTestCategories.Fast)]
    public class KvR6FixTests
    {
        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        private static string ReadFile(string relativePath)
        {
            string path = Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "нет файла " + path);
            return File.ReadAllText(path);
        }

        /// <summary>Убрать комментарии: иначе объяснение «что было» считается вызовом.</summary>
        private static string StripComments(string source)
        {
            string withoutBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty,
                RegexOptions.Singleline);
            return Regex.Replace(withoutBlock, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }

        /// <summary>Кусок исходника от объявления метода до конца файла — с проверкой, что он есть.</summary>
        private static string AfterMethod(string code, string signature)
        {
            int index = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(index, 0, "в исходнике нет метода " + signature +
                " — проверка порядка шагов потеряла смысл");
            return code.Substring(index);
        }

        private const string ManagerPath =
            "Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs";
        private const string GuardPath =
            "Assets/_Project/01_Scripts/Xr/KvXrDesktopOverlayGuard.cs";
        private const string ArRuntimePath =
            "Assets/_Project/01_Scripts/Ar/KvArRuntime.cs";

        // ================================================================ 1. ЧИСТЫЕ ПРАВИЛА

        /// <summary>
        /// R6-1: «ВКЛЮЧЕНА» И «РИСУЕТСЯ» — РАЗНЫЕ ВЕЩИ, И РЕШЕНИЕ ПРИНИМАЕТСЯ ПО ВТОРОМУ.
        ///
        /// ЧТО БЫЛО. Проверка видимости панели смотрела только флаг `Shown` и прямоугольник. На
        /// планшете в AR этого мало: канвас погашен сторожем, и панель не рисует ни один рендерер.
        /// Тест фиксирует ОБЕ ветки: включённая панель на живом канвасе — видна; та же панель на
        /// погашенном канвасе или выключенном объекте — нет. Второй ветки в старом коде не было
        /// вовсе, поэтому он и не мог покраснеть.
        /// </summary>
        [Test]
        public void R6_Renderable_RequiresCanvasAndHierarchy_NotJustFlag()
        {
            Assert.IsTrue(KvDockGeometry.IsRenderable(true, true, true),
                "включённая панель на живом канвасе обязана считаться рисуемой");

            Assert.IsFalse(KvDockGeometry.IsRenderable(true, true, false),
                "панель на ПОГАШЕННОМ канвасе (сторож оверлея в AR) не рисуется — это и есть R6-1");
            Assert.IsFalse(KvDockGeometry.IsRenderable(true, false, true),
                "панель с выключенным объектом не рисуется, даже если канвас включён");
            Assert.IsFalse(KvDockGeometry.IsRenderable(false, true, true),
                "скрытая панель не рисуется при любом состоянии канваса");

            // Решение о починке — правило, отдельное от «рисуется»: чинить надо только
            // ВКЛЮЧЁННУЮ панель, иначе команда «показать» воевала бы с командой «скрыть».
            Assert.IsTrue(KvDockGeometry.NeedsRenderRepair(true, true, false),
                "включённую панель с погашенным канвасом обязательно надо включить (R6-1)");
            Assert.IsTrue(KvDockGeometry.NeedsRenderRepair(true, false, true),
                "включённую панель с выключенным объектом обязательно надо включить (R6-1)");
            Assert.IsFalse(KvDockGeometry.NeedsRenderRepair(true, true, true),
                "исправную панель чинить не надо — иначе каждый показ пересобирал бы раскладку");
            Assert.IsFalse(KvDockGeometry.NeedsRenderRepair(false, false, false),
                "скрытую панель не надо «чинить»: её просто не открывали");
        }

        /// <summary>
        /// R6-1: КАНВАС ПАНЕЛЕЙ ГАСИТСЯ, ПОКА ОПЕРАТОР НИЧЕГО НЕ ОТКРЫВАЛ, И ОСТАЁТСЯ ЖИВЫМ,
        /// КОГДА ПАНЕЛЬ ОТКРЫТА КОМАНДОЙ.
        ///
        /// Обе ветки обязательны. «Всегда оставлять» вернуло бы владельцу десктопный оверлей
        /// поверх камеры AR (жалоба «жёлтый экран»); «всегда гасить» — ровно тот дефект, который
        /// вернулся из приёмки.
        /// </summary>
        [Test]
        public void R6_OverlayPolicy_DockCanvasKeptOnlyForRequestedPanel()
        {
            string dock = KvXrOverlayPolicy.DockCanvasName;
            Assert.IsNotEmpty(dock, "у канваса панелей нет имени — сторож не отличит его от оболочки");
            Assert.AreNotEqual(KvXrOverlayPolicy.DesktopCanvasName, dock,
                "канвас панелей назван как канвас оболочки — сторож будет путать их (R6-1)");

            Assert.IsTrue(KvXrOverlayPolicy.IsDockCanvas(dock), "имя канваса панелей не распознаётся");
            Assert.IsFalse(KvXrOverlayPolicy.IsDockCanvas(KvXrOverlayPolicy.DesktopCanvasName),
                "канвас оболочки принят за канвас панелей — панели окажутся под ним");

            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.ClassifyDockCanvas(true),
                "канвас с ОТКРЫТОЙ ОПЕРАТОРОМ панелью гасить нельзя: команда выполнена, панели нет (R6-1)");
            Assert.AreEqual(KvXrOverlayDisposition.HideRendering,
                KvXrOverlayPolicy.ClassifyDockCanvas(false),
                "канвас без запрошенных панелей обязан гаситься, иначе в AR поверх камеры висит " +
                "десктопный интерфейс");

            // В ШЛЕМЕ — ГАСИТЬ ВСЕГДА, даже если панель открыта командой: экранный канвас в
            // стерео-режиме и есть тот десктопный оверлей, который владелец запретил. В шлеме
            // настройки открываются мировым окном (`KvXrWindowHost`).
            Assert.AreEqual(KvXrOverlayDisposition.HideRendering,
                KvXrOverlayPolicy.ClassifyDockCanvas(true, true),
                "в шлеме канвас панелей оставлен включённым — вернётся десктопный оверлей в шлеме");
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.ClassifyDockCanvas(true, false),
                "на планшете (шлема нет) открытая оператором панель обязана остаться видимой (R6-1)");

            // Расширенная классификация: по одному имени канвас панелей — десктопный интерфейс
            // (так решают все, кто не заглядывает внутрь), а с запрошенной панелью — остаётся.
            Assert.AreEqual(KvXrOverlayDisposition.HideRendering, KvXrOverlayPolicy.Classify(dock),
                "по имени канвас панелей обязан считаться десктопным интерфейсом");
            Assert.AreEqual(KvXrOverlayDisposition.Keep, KvXrOverlayPolicy.Classify(dock, true),
                "классификация с признаком запрошенной панели не учитывает её (R6-1)");
            Assert.AreEqual(KvXrOverlayDisposition.HideRendering,
                KvXrOverlayPolicy.Classify(KvXrOverlayPolicy.DesktopCanvasName, true),
                "признак запрошенной панели не должен спасать канвас ВСЕЙ оболочки: закроет камеру AR");

            // Ничего прежнего не поломано: интерфейс шлема и якорь панели решаются как раньше.
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.Classify(KvXrOverlayPolicy.VrPanelCanvasName, true),
                "интерфейс шлема обязан оставаться нетронутым при любых признаках");
        }

        /// <summary>
        /// R6-1: СТОРОЖ ГАСИТ КАНВАС ПО ПРАВИЛУ, А НЕ ПО ПОРЯДКУ СТРОК.
        ///
        /// `Decide` — та самая функция, которой `Guard()` пользуется в сцене. Проверяются все
        /// четыре исхода, которые сторож обязан различать: интерфейс шлема, канвас панелей с
        /// запрошенной панелью, канвас панелей без неё и обычный десктопный канвас.
        /// </summary>
        [Test]
        public void R6_Guard_Decide_KeepsRequestedPanelCanvas()
        {
            // Интерфейс шлема — не трогать никогда, даже если он «похож на десктопный».
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrDesktopOverlayGuard.Decide(KvXrOverlayPolicy.VrGroupCanvasName, true, false),
                "сторож погасил бы интерфейс шлема — владелец остался бы без панели управления");

            // Канвас панелей: решает признак запрошенной панели, а не «десктопность».
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrDesktopOverlayGuard.Decide(KvXrOverlayPolicy.DockCanvasName, true, true),
                "панель открыта оператором, а сторож всё равно гасит канвас — это и есть R6-1");
            Assert.AreEqual(KvXrOverlayDisposition.HideRendering,
                KvXrDesktopOverlayGuard.Decide(KvXrOverlayPolicy.DockCanvasName, true, false),
                "канвас панелей без запрошенной панели обязан гаситься (десктопный интерфейс в AR)");

            // В ШЛЕМЕ — гасим всегда. Это не «на всякий случай»: кнопка «Настройки» стартового
            // меню (`KvXrStartMenu`) зовёт `ShowSettings(0)` и в шлеме тоже, без этой оговорки она
            // вытащила бы десктопную панель поверх стерео-картинки.
            Assert.AreEqual(KvXrOverlayDisposition.HideRendering,
                KvXrDesktopOverlayGuard.Decide(KvXrOverlayPolicy.DockCanvasName, true, true, true),
                "сторож оставил канвас панелей включённым в шлеме — владелец увидит десктопный оверлей");

            // Оболочка и прочий десктопный интерфейс — как было.
            Assert.AreEqual(KvXrOverlayDisposition.HideRendering,
                KvXrDesktopOverlayGuard.Decide(KvXrOverlayPolicy.DesktopCanvasName, true, false),
                "канвас оболочки обязан гаситься в AR и в шлеме");
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrDesktopOverlayGuard.Decide("XrRig", false, false),
                "незнакомый объект сцены трогать нельзя");
        }

        // ================================================================ 2. ИЕРАРХИЯ КАНВАСОВ

        /// <summary>
        /// R6-1: ПАНЕЛИ ОПЕРАТОРА — ЭТО КАНВАС И ВСЁ, ЧТО ВНУТРИ НЕГО, ВКЛЮЧАЯ ВЛОЖЕННЫЕ КАНВАСЫ.
        ///
        /// ЗАЧЕМ ПРОВЕРЯТЬ ИЕРАРХИЮ. `KvDockPanel` создаёт на корне каждой панели ВЛОЖЕННЫЙ
        /// канвас (порядок сортировки). Сторож обходит ВСЕ канвасы сцены, поэтому видел и его.
        /// Если считать «панелями оператора» только корневой канвас, сторож погасит вложенный — и
        /// панель снова не нарисуется, хотя корневой канвас включён. Проверка на настоящих
        /// объектах: обе стороны этого правила.
        /// </summary>
        [Test]
        public void R6_OperatorPanelCanvas_CoversNestedCanvases()
        {
            GameObject root = null;
            GameObject nested = null;
            GameObject foreign = null;
            try
            {
                root = new GameObject(KvXrOverlayPolicy.DockCanvasName, typeof(Canvas));
                nested = new GameObject("Dock_Настройки и справка", typeof(Canvas));
                nested.transform.SetParent(root.transform, false);
                GameObject inner = new GameObject("Inner", typeof(RectTransform));
                inner.transform.SetParent(nested.transform, false);

                foreign = new GameObject(KvXrOverlayPolicy.DesktopCanvasName, typeof(Canvas));

                Assert.IsTrue(KvXrOperatorPanelCanvas.IsOperatorPanelCanvas(root.GetComponent<Canvas>()),
                    "корневой канвас панелей не распознан как канвас панелей оператора");
                Assert.IsTrue(KvXrOperatorPanelCanvas.IsOperatorPanelCanvas(nested.GetComponent<Canvas>()),
                    "ВЛОЖЕННЫЙ канвас панели не распознан — сторож погасит его и панель исчезнет (R6-1)");
                Assert.IsTrue(KvXrOperatorPanelCanvas.IsOperatorPanelCanvas(inner.transform),
                    "объект внутри панели не отнесён к панелям оператора");

                Assert.IsTrue(KvXrOperatorPanelCanvas.IsDockRootCanvas(root.GetComponent<Canvas>()),
                    "корневой канвас панелей не признан корневым");
                Assert.IsFalse(KvXrOperatorPanelCanvas.IsDockRootCanvas(nested.GetComponent<Canvas>()),
                    "вложенный канвас принят за корневой: его судьбу решал бы он сам (R6-1)");

                Assert.IsFalse(KvXrOperatorPanelCanvas.IsOperatorPanelCanvas(foreign.GetComponent<Canvas>()),
                    "канвас оболочки принят за канвас панелей — оболочка перестанет гаситься в шлеме");
                Assert.IsFalse(KvXrOperatorPanelCanvas.IsOperatorPanelCanvas((Canvas)null),
                    "null-канвас не может быть канвасом панелей");
            }
            finally
            {
                if (foreign != null) UnityEngine.Object.DestroyImmediate(foreign);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ================================================================ 3. ПОРЯДОК ШАГОВ В КОДЕ

        /// <summary>
        /// R6-1: ПАНЕЛИ СТРОЯТСЯ НА СВОЁМ КАНВАСЕ, А НЕ В КАНВАСЕ ОБОЛОЧКИ.
        ///
        /// Это и есть суть правки: пока панель лежит внутри канваса, который сторож гасит целиком,
        /// у сторожа нет третьего варианта — либо десктопный оверлей поверх камеры AR, либо панель
        /// вместе с ним. Проверяются ОБЕ стороны: «строится в своём» и «больше не строится в
        /// канвасе оболочки».
        /// </summary>
        [Test]
        public void R6_Manager_BuildsDockPanelsOnOwnCanvas()
        {
            string code = StripComments(ReadFile(ManagerPath));
            Assert.Greater(code.Length, 50000,
                "исходник менеджера подозрительно мал (" + code.Length +
                " символов без комментариев) — проверка была бы вакуумной (урок P4-2)");

            StringAssert.Contains("BuildPanelOverlay", code,
                "менеджер не создаёт отдельный канвас панелей (R6-1)");
            StringAssert.Contains("KvXrOverlayPolicy.DockCanvasName", code,
                "канвас панелей создаётся без имени из политики: сторож его не узнает (R6-1)");
            StringAssert.Contains("EnsurePanelCanvasRendering", code,
                "показ панели не включает её рисование — «команда выполнена, панели нет» (R6-1)");
            StringAssert.Contains("panel.Build(dockParent, dockOwner", code,
                "панель строится не в канвасе панелей (R6-1)");
            Assert.IsFalse(Regex.IsMatch(code, @"panel\.Build\(\s*canvasRect\s*,\s*canvas\b"),
                "панель снова строится в канвасе ОБОЛОЧКИ — в AR её погасит сторож (R6-1)");

            // Канвас панелей обязан быть ВЫШЕ AR-оверлея планшета, иначе панель под ним.
            StringAssert.Contains("panelCanvas.sortingOrder = DockSortingOrder", code,
                "порядок канваса панелей не задан — панель может оказаться под AR-оверлеем (R6-1)");
        }

        /// <summary>
        /// R6-1: ПОРЯДОК ШАГОВ В `ShowPanel` — СПЕРВА ЗАПРОС И РИСОВАНИЕ, ПОТОМ ПРОВЕРКА.
        ///
        /// ЧТО БЫЛО. Проверка видимости шла сразу после включения флага. На устройстве она
        /// отвечала «видна» (флаг есть, прямоугольник в экране), и функция выходила, не включив
        /// рисование. Отсюда в журнале НИ ОДНОЙ строки `ПАНЕЛИ:` — починка не запускалась.
        /// Порядок здесь — не стиль, а суть дефекта, поэтому он и проверяется.
        /// </summary>
        [Test]
        public void R6_ShowPanel_RequestsRenderingBeforeCheckingVisibility()
        {
            string code = StripComments(ReadFile(ManagerPath));
            string showPanel = AfterMethod(code, "public void ShowPanel(string id)");

            int mark = showPanel.IndexOf("MarkOperatorRequested(true)", StringComparison.Ordinal);
            int ensure = showPanel.IndexOf("EnsurePanelCanvasRendering()", StringComparison.Ordinal);
            int check = showPanel.IndexOf("IsPanelEffectivelyVisible(id)", StringComparison.Ordinal);

            Assert.GreaterOrEqual(mark, 0,
                "`ShowPanel` не помечает панель как запрошенную оператором — сторож её погасит (R6-1)");
            Assert.GreaterOrEqual(ensure, 0,
                "`ShowPanel` не включает рисование панели (R6-1)");
            Assert.GreaterOrEqual(check, 0,
                "`ShowPanel` не проверяет фактическую видимость — команда снова врёт (R6-1)");

            Assert.Less(mark, check,
                "признак «панель запрошена» ставится ПОСЛЕ проверки видимости (R6-1)");
            Assert.Less(ensure, check,
                "рисование включается ПОСЛЕ проверки видимости — на устройстве проверка уже " +
                "скажет «видна» и функция выйдет, не включив канвас (R6-1)");

            // И обратная сторона: закрытие обязано снимать признак, иначе сторож навсегда
            // оставит канвас включённым (десктопный интерфейс вернётся в шлем).
            string toggle = AfterMethod(code, "public void TogglePanel(string id)");
            StringAssert.Contains("MarkOperatorRequested(false)", toggle,
                "при закрытии панели признак «запрошена» не снимается — сторож не погасит канвас (R6-1)");
        }

        /// <summary>
        /// R6-1: ОБА СТОРОЖА ПРИНИМАЮТ РЕШЕНИЕ ПО ОДНОМУ ПРАВИЛУ.
        ///
        /// ЧТО БЫЛО (и почему это отдельная проверка). Канвасы десктопного интерфейса гасят ДВА
        /// разных места: сторож по таймеру (`KvXrDesktopOverlayGuard`) и разовый гаситель в AR
        /// (`KvArRuntime.HideForeignCanvases`). Пока правило записано в каждом по-своему, они
        /// расходятся: один уже не гасит канвас панелей, другой гасит — и панель снова невидима,
        /// причём воспроизводится это только при старте AR-сцены.
        /// </summary>
        [Test]
        public void R6_BothGuards_UseTheSameOperatorPanelRule()
        {
            string guard = StripComments(ReadFile(GuardPath));
            string ar = StripComments(ReadFile(ArRuntimePath));

            Assert.Greater(guard.Length, 3000, "сторож оверлея подозрительно мал — проверка вакуумна");
            Assert.Greater(ar.Length, 20000, "исходник AR-рантайма подозрительно мал — проверка вакуумна");

            StringAssert.Contains("KvXrOperatorPanelCanvas.IsOperatorPanelCanvas", guard,
                "сторож оверлея не узнаёт канвас панелей оператора (R6-1)");
            StringAssert.Contains("Decide(", guard,
                "сторож оверлея решает судьбу канваса не общей функцией (R6-1)");

            StringAssert.Contains("HideForeignCanvases", ar,
                "в AR-рантайме нет гасителя посторонних канвасов — проверка потеряла адресата");
            string hider = AfterMethod(ar, "private void HideForeignCanvases()");
            StringAssert.Contains("KvXrOperatorPanelCanvas.IsOperatorPanelCanvas", hider,
                "гаситель посторонних канвасов в AR НЕ исключает канвас панелей — он погасит " +
                "панель настроек при старте сцены (R6-1, третья попытка)");
        }
    }
}
