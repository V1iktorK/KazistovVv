// ================================================================================================
//  KvMrTests.cs — EditMode-тесты ПЕРЕНОСА В СМЕШАННУЮ РЕАЛЬНОСТЬ (сессия S45, 02.11.2026).
//
//  ОТКУДА ЗАДАЧА. `Docs/ПРОМТ_АГЕНТУ_MR.md` требует: настоящий сквозной вид, робот на РЕАЛЬНОМ
//  столе, привязка к миру (не к голове), масштаб 1:1, автоопределение режима и сохранение всего
//  VR-функционала. Главный риск — «MR на бумаге»: код есть, а на устройстве ничего не меняется.
//  Ровно это и было до сессии: проба сквозного вида всегда отвечала «неизвестно», фичи PICO были
//  выключены, а `MrActive` всегда false — то есть вся ветка MR была МЁРТВОЙ.
//
//  ЧТО ПРОВЕРЯЕТСЯ (по блокам приёмки из ТЗ):
//    A. Сквозной вид: проба спрашивает РАНТАЙМ (расширения OpenXR), а не гадает; включение идёт
//       вендорским API PICO / AR Foundation Meta, а не подменой цвета фона.
//    B. Привязка к реальному столу: постановка идёт в ТОЧКУ попадания луча по реальной
//       поверхности, а не в начало координат; мировой корень НЕ подребёнок рига.
//    В. Масштаб 1:1: постановка ставит масштаб ровно 1; инструмент замера габаритов существует.
//    Г. Автоопределение режима: режим включается сам (тумблер по умолчанию ВКЛЮЧЁН), а решает
//       факт возможностей устройства.
//    Е. Отказ: без возможностей MR приложение остаётся VR и не падает.
//    Документы: ПЛАН_MR / ОТЧЁТ_MR / ЖУРНАЛ_РЕШЕНИЙ_MR существуют и ссылаются друг на друга.
//
//  СТИЛЬ — КАК В ПРОЕКТЕ: там, где сцену не запустить, проверяется ИСХОДНИК и ЧИСТЫЕ функции.
//  Это не «тесты для галочки»: каждый ловит конкретный уже случавшийся дефект.
// ================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KazistovVvXr;
using KazistovVvXrEditor;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>MR: пробы, выбор бэкенда, сквозной вид, постановка на реальную поверхность, документы.</summary>
    public class KvMrTests
    {
        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        private static string ScriptsDir
        {
            get { return Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", "Xr"); }
        }

        private static string ReadScript(string fileName)
        {
            string path = Path.Combine(ScriptsDir, fileName);
            Assert.IsTrue(File.Exists(path), "не найден исходник: " + path);
            return File.ReadAllText(path);
        }

        // ==================================================== A. СКВОЗНОЙ ВИД

        /// <summary>
        /// ПРОБА СПРАШИВАЕТ РАНТАЙМ, А НЕ ВЫДУМЫВАЕТ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. Первая версия пробы лезла отражением в
        /// `OpenXRSettings.GetSettings()`/`ActiveFeatures` — таких членов в OpenXR 1.15.1 НЕТ, и
        /// ответом всегда было «неизвестно». На шлеме это выглядело как «устройство не умеет MR».
        /// Настоящий источник правды — список расширений, ВЫДАННЫХ рантаймом.
        /// </summary>
        [Test]
        public void CapabilityProbe_AsksRuntimeExtensions()
        {
            string source = ReadScript("KvXrCapabilities.cs");

            StringAssert.Contains("GetEnabledExtensions", source,
                "проба не читает список выданных расширений — значит опять отвечает «неизвестно»");
            StringAssert.DoesNotContain("Type.GetType(", source,
                "проба снова полезла в отражение: у OpenXR 1.15.1 нет OpenXRSettings.GetSettings()/" +
                "ActiveFeatures, и такой код честно ничего не узнаёт");
            StringAssert.Contains("Unknown", source,
                "проба обязана различать «не удалось спросить» и «нет»");
        }

        /// <summary>
        /// ВКЛЮЧЕНИЕ PASSTHROUGH ИДЁТ ВЕНДОРСКИМ API, А НЕ ПОДМЕНОЙ ЦВЕТА.
        /// Прямо запрещено ТЗ (S3.1): `Camera.backgroundColor = gray` — это не сквозной вид.
        /// </summary>
        [Test]
        public void Passthrough_UsesVendorApi_NotBackgroundColor()
        {
            string source = ReadScript("KvMrRuntime.cs");

            StringAssert.Contains("isExtensionEnable", source,
                "перед включением обязана быть проверка, что рантайм ВЫДАЛ расширение сквозного вида");
            StringAssert.Contains("IsPassthroughSupported", source,
                "перед включением обязана быть проба поддержки железом (иначе «серый экран» вместо комнаты)");
            StringAssert.Contains("EnableVideoSeeThrough", source,
                "включение сквозного вида идёт штатным API PICO, а не сменой цвета фона");
            StringAssert.Contains("ARCameraManager", source,
                "Meta-ветка обязана включать камеру-подсистему (именно она создаёт passthrough-слой)");
            StringAssert.DoesNotContain("AddComponent<UnityEngine.XR.ARFoundation.ARCameraBackground", source,
                "ARCameraBackground на Quest не работает (пакет 2.4.1 не отдаёт cameraMaterial) — " +
                "его добавление только перезапишет проекционную матрицу камеры");

            // «Подмена цвета фона вместо MR» — прямой запрет ТЗ. Фон делается ПРОЗРАЧНЫМ
            // (alpha 0) как обязательное условие пасс-слоя, а не серым «вместо комнаты».
            StringAssert.Contains("Color(0f, 0f, 0f, 0f)", source,
                "фон камеры под пасс-слоем обязан быть чёрным с нулевой альфой (требование пакета PICO)");
        }

        /// <summary>
        /// БЕЗ ВОЗМОЖНОСТЕЙ MR ПРИЛОЖЕНИЕ ЧЕСТНО ОСТАЁТСЯ В VR (блок Е).
        /// Проверяется чистая функция выбора бэкенда: неизвестный рантайм → None, а не «включим
        /// что-нибудь и посмотрим».
        /// </summary>
        [Test]
        public void Backend_UnknownRuntimeIsNone()
        {
            Assert.AreEqual(KvMrBackend.None,
                KvMrRuntime.ChooseBackend("Unknown Runtime", "Unknown Device", false, false),
                "неизвестный рантайм обязан давать None: сквозной вид не включается «на авось»");
        }

        /// <summary>БЭКЕНД ОПРЕДЕЛЯЕТСЯ ПО РАНТАЙМУ/УСТРОЙСТВУ, а не по настройке в приложении.</summary>
        [Test]
        public void Backend_DetectsPicoAndMeta()
        {
            Assert.AreEqual(KvMrBackend.Pico,
                KvMrRuntime.ChooseBackend("PICO OpenXR Runtime", "A9210", false, false),
                "PICO-рантайм обязан определяться как Pico");
            Assert.AreEqual(KvMrBackend.Pico,
                KvMrRuntime.ChooseBackend("", "PICO 4 Ultra", false, false),
                "модель PICO обязана определяться как Pico");
            Assert.AreEqual(KvMrBackend.Meta,
                KvMrRuntime.ChooseBackend("Meta Quest", "", false, false),
                "Meta-рантайм обязан определяться как Meta");
            Assert.AreEqual(KvMrBackend.Meta,
                KvMrRuntime.ChooseBackend("", "Quest 3", false, false),
                "модель Quest обязана определяться как Meta");

            // PICO-специфичное расширение решает спор, даже если имя рантайма «чужое».
            Assert.AreEqual(KvMrBackend.Pico,
                KvMrRuntime.ChooseBackend("Unknown", "Unknown", true, false),
                "расширение XR_BD/XR_PICO — достаточный признак PICO");
        }

        // ==================================================== Б. ПРИВЯЗКА К РЕАЛЬНОМУ СТОЛУ

        /// <summary>
        /// ПОСТАНОВКА ИДЁТ В ТОЧКУ ПОВЕРХНОСТИ, А НЕ В НАЧАЛО КООРДИНАТ.
        ///
        /// ИСТОРИЯ ДЕФЕКТА. До сессии `KvXrMrRoom.PlaceRobot()` ставил робота в
        /// `tableCenter = (0,0,0)` на высоту 0,78 — то есть В МЕСТО ОПЕРАТОРА. Изнутри модели
        /// видны только обратные стороны граней, поэтому робот «исчезал».
        /// </summary>
        [Test]
        public void Placement_UsesSurfacePoint()
        {
            string source = ReadScript("KvXrMrRoom.cs");

            StringAssert.Contains("PlaceRobotAtSurface", source,
                "нет постановки по точке реальной поверхности — робот снова окажется в начале координат");
            StringAssert.DoesNotContain("PlaceRobot();", source,
                "остался старый вызов постановки без поверхности — он ставит робота в место оператора");
        }

        /// <summary>
        /// МИРОВОЙ КОРЕНЬ НЕ ПОДЧИНЁН РИГУ. Если робота положить под риг, то при любом
        /// искусственном перемещении (телепорт/ход) он поедет вместе с оператором — это ровно то
        /// «робот плывёт за головой», которое ТЗ запрещает.
        /// </summary>
        [Test]
        public void WorldRoot_IsNotChildOfRig()
        {
            Transform root = KvXrMrRoom.EnsureWorldRoot();
            try
            {
                Assert.IsNotNull(root, "мировой корень MR не создаётся");
                Assert.AreEqual("KvMrWorld", root.name, "имя мирового корня изменено — его ищут по имени");
                Assert.IsNull(root.parent,
                    "мировой корень оказался подчинён другому объекту (ригу?) — робот будет «плыть» за оператором");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        /// <summary>
        /// ЛУЧ ЦЕПЛЯЕТ ТОЛЬКО РЕАЛЬНЫЕ ПОВЕРХНОСТИ. Виртуальные коллайдеры (робот, панели) не
        /// должны перехватывать луч постановки — иначе робот встанет «на самого себя» или на панель.
        /// </summary>
        [Test]
        public void SurfaceRaycast_FiltersToRealWorldColliders()
        {
            string source = ReadScript("KvMrSurfaces.cs");

            StringAssert.Contains("realWorldColliders.Contains", source,
                "луч постановки обязан фильтровать попадания по списку коллайдеров реального мира");
            StringAssert.Contains("GenerateMeshAsync", source,
                "коллайдеры реальных поверхностей должны строиться из меша комнаты");
            StringAssert.Contains("XRMeshSubsystem", source,
                "источник реальных поверхностей — стандартная подсистема меша Unity XR");
        }

        /// <summary>ВИРТУАЛЬНОЕ ОКРУЖЕНИЕ ОПОЗНАЁТСЯ ПО ИМЕНИ — чистая функция, таблица случаев.</summary>
        [Test]
        public void VirtualEnvironment_IsRecognizedByName()
        {
            Assert.IsTrue(KvXrMrRoom.IsVirtualEnvironmentRoot("Стенд_1_Стол"),
                "стенд проекта обязан скрываться в MR");
            Assert.IsTrue(KvXrMrRoom.IsVirtualEnvironmentRoot("XrHangar"),
                "виртуальный зал обязан скрываться в MR — иначе он закрывает комнату");
            Assert.IsTrue(KvXrMrRoom.IsVirtualEnvironmentRoot("XrFloor"),
                "виртуальный пол обязан скрываться в MR");

            Assert.IsFalse(KvXrMrRoom.IsVirtualEnvironmentRoot("XrRig"), "риг скрывать нельзя");
            Assert.IsFalse(KvXrMrRoom.IsVirtualEnvironmentRoot("KazistovVv_UI"), "интерфейс скрывать нельзя");
            Assert.IsFalse(KvXrMrRoom.IsVirtualEnvironmentRoot(null), "null — не окружение");
        }

        // ==================================================== В. МАСШТАБ 1:1

        /// <summary>
        /// МАСШТАБ 1:1 — ЭТО ФАКТ, А НЕ ПОДБОР. Постановка сохраняет масштаб, в котором робот
        /// стоит в сцене (если модель метрическая, это 1), и печатает в журнал ФАКТИЧЕСКИЕ
        /// габариты в метрах. Подбирать коэффициент «на глаз» в шлеме запрещено: именно так
        /// получается «робот ростом с чайник».
        /// </summary>
        [Test]
        public void Placement_PreservesScaleAndLogsMeasuredSize()
        {
            string source = ReadScript("KvXrMrRoom.cs");
            StringAssert.Contains("robotAuthoredScale", source,
                "постановка обязана сохранять авторский масштаб робота, а не придумывать свой");
            StringAssert.Contains("RobotSize = MeasureSize", source,
                "после постановки габариты обязаны замеряться — иначе «1:1» нечем подтвердить");
            StringAssert.Contains("габариты", source,
                "в журнал обязаны печататься габариты числом (документ без числа — мнение)");
            Assert.IsTrue(source.Contains("масштаб"), "в журнале обязано печататься число масштаба");
        }

        /// <summary>
        /// ЗАМЕР ГАБАРИТОВ СУЩЕСТВУЕТ. Утверждение «робот 1:1» проверяется числом (высота в
        /// метрах), а не словами. Инструмент замера — часть доказательной базы отчёта.
        /// </summary>
        [Test]
        public void ScaleMeasurement_ToolExists()
        {
            string path = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", "Editor",
                "KvMrSetupTools.cs");
            Assert.IsTrue(File.Exists(path), "нет инструмента настройки/замера MR: " + path);
            string source = File.ReadAllText(path);
            StringAssert.Contains("MeasureRobotScale", source, "нет метода замера габаритов роботов");
            StringAssert.Contains("KvRobotCatalog", source, "замер обязан идти по каталогу моделей проекта");
        }

        /// <summary>
        /// ЗАПАСНОЙ ИСТОЧНИК СТОЛА — РАЗМЕЧЕННАЯ КОМНАТА PICO, С ТАЙМ-АУТОМ И КОНВЕРСИЕЙ КООРДИНАТ.
        ///
        /// ЗАЧЕМ. Меш комнаты требует надетого шлема и разметки; семантическая сцена даёт стол
        /// параллелепипедом. Две тонкости, которые легко потерять при правке: (1) у пакета PICO
        /// поза БОКСА не переводится в Unity (нужна инверсия Z), (2) нативные запросы могут не
        /// ответить вовсе — без тайм-аута ожидание станет вечным, и робот не встанет на стол.
        /// </summary>
        [Test]
        public void SurfaceFallback_UsesSceneAnchorsWithTimeoutAndZConversion()
        {
            string source = ReadScript("KvMrSurfaces.cs");

            StringAssert.Contains("QuerySceneAnchorAsync", source,
                "нет запроса размеченных объектов комнаты (стол из семантической сцены)");
            StringAssert.Contains("StartSenseDataProvider", source,
                "не запускается провайдер сцены PICO — запрос стола не сработает");
            StringAssert.Contains("Task.WhenAny", source,
                "нет тайм-аута: нативный запрос PICO может не ответить, и ожидание станет вечным");
            StringAssert.Contains("-boxPos.z", source,
                "не выполнена конверсия координат бокса (у пакета PICO поза Box3D не переводится)");
            StringAssert.Contains("BoxCollider", source,
                "из габаритов стола не строится коллайдер — луч постановки не увидит стол");
        }

        /// <summary>
        /// ДЛЯ QUEST СПИСОК ФИЧ ВКЛЮЧАЕТ ОБЯЗАТЕЛЬНУЮ SESSION. Без неё сборка падает на
        /// валидации OpenXR (`error = true`): камера/плоскости/якоря Meta зависят от Session.
        /// </summary>
        [Test]
        public void FeatureSetup_MetaListHasSessionDependency()
        {
            string joined = string.Join(" ", KvMrSetupTools.MetaMrFeatureTypeNames);

            StringAssert.Contains("ARSessionFeature", joined,
                "нет обязательной зависимости Meta Quest: Session — сборка под Quest упадёт на валидации");
            StringAssert.Contains("ARCameraFeature", joined,
                "нет фичи камеры Meta — сквозной вид на Quest не включится");
            StringAssert.Contains("MetaQuestFeature", joined, "нет поддержки Meta Quest");
        }

        // ==================================================== Г. АВТООПРЕДЕЛЕНИЕ РЕЖИМА

        /// <summary>
        /// РЕЖИМ ВКЛЮЧАЕТСЯ САМ. Тумблер «сквозной вид» по умолчанию ВКЛЮЧЁН, иначе `MrActive`
        /// недостижим (именно это и было: `SetPassthrough` не вызывался ниоткуда, значение 0).
        /// При этом решает не тумблер, а фактические возможности устройства.
        /// </summary>
        [Test]
        public void Mode_IsAutomatic_PassthroughOnByDefault()
        {
            FieldInfo field = typeof(KvXrSettings).GetField("DefaultPassthrough",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field, "в KvXrSettings нет DefaultPassthrough");
            Assert.AreEqual(1, (int)field.GetRawConstantValue(),
                "сквозной вид обязан быть включён по умолчанию: иначе MR не включится сам (ТЗ S1.7)");

            string source = ReadScript("KvXrMrRoom.cs");
            StringAssert.Contains("KvXrPlatformModeSelector.Current == KvXrPlatformMode.Mr", source,
                "MrActive обязан опираться на ФАКТИЧЕСКИЙ режим, а не на настройку");
        }

        /// <summary>
        /// ФИЧИ OPENXR ДЛЯ ANDROID ВКЛЮЧАЮТСЯ ИНСТРУМЕНТОМ, И СПИСОК ПОЛНЫЙ.
        /// Без `PassthroughFeature` сквозного вида не будет, без `OpenXRExtensions` не заработает
        /// меш (и валидация OpenXR даст ошибку сборки).
        /// </summary>
        [Test]
        public void FeatureSetup_ListsRequiredPicoFeatures()
        {
            string joined = string.Join(" ", KvMrSetupTools.PicoMrFeatureTypeNames);

            StringAssert.Contains("PassthroughFeature", joined, "нет фичи сквозного вида");
            StringAssert.Contains("OpenXRExtensions", joined,
                "нет обязательной зависимости PICO (без неё сборка валится и меш не приходит)");
            StringAssert.Contains("PICOSpatialMesh", joined, "нет фичи реальных поверхностей");
        }

        // ==================================================== Е. ОТКАЗ

        /// <summary>
        /// ЕСЛИ СКВОЗНОГО ВИДА НЕТ — ПРИЛОЖЕНИЕ РАБОТАЕТ. Включение MR обязано быть под защитой
        /// try/catch и не бросать: сбой вендорского API не должен ронять приложение.
        /// </summary>
        [Test]
        public void Failure_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => KvMrRuntime.Detect(),
                "определение бэкенда не должно бросать без XR (в редакторе это норма)");
            Assert.DoesNotThrow(() => KvXrCapabilities.Passthrough(),
                "проба сквозного вида не должна бросать без XR");

            string source = ReadScript("KvMrRuntime.cs");
            StringAssert.Contains("catch (Exception", source,
                "включение сквозного вида обязано переживать сбой вендорского API");
        }

        // ==================================================== ДОКУМЕНТЫ

        /// <summary>
        /// ДОКУМЕНТЫ СЕССИИ СУЩЕСТВУЮТ И ССЫЛАЮТСЯ ДРУГ НА ДРУГА. ТЗ (S6) требует вести три
        /// документа, а не только читать их. Ссылка обязана быть вида `Docs/Имя.md` — иначе
        /// `KvDocsLinkTests` её даже не проверит, и битая ссылка пройдёт.
        /// </summary>
        [Test]
        public void MrDocuments_ExistAndCrossReference()
        {
            string docs = Path.Combine(ProjectRoot, "Docs");
            string plan = Path.Combine(docs, "ПЛАН_MR.md");
            string report = Path.Combine(docs, "ОТЧЁТ_MR.md");
            string journal = Path.Combine(docs, "ЖУРНАЛ_РЕШЕНИЙ_MR.md");

            Assert.IsTrue(File.Exists(plan), "нет Docs/ПЛАН_MR.md — план работ по MR не ведётся");
            Assert.IsTrue(File.Exists(report), "нет Docs/ОТЧЁТ_MR.md — отчёт по MR не ведётся");
            Assert.IsTrue(File.Exists(journal), "нет Docs/ЖУРНАЛ_РЕШЕНИЙ_MR.md — решения не фиксируются");

            string planText = File.ReadAllText(plan);
            string reportText = File.ReadAllText(report);
            string journalText = File.ReadAllText(journal);

            StringAssert.Contains("Docs/ОТЧЁТ_MR.md", planText,
                "план не ссылается на отчёт — читатель не найдёт результаты");
            StringAssert.Contains("Docs/ЖУРНАЛ_РЕШЕНИЙ_MR.md", planText,
                "план не ссылается на журнал решений");
            StringAssert.Contains("Docs/ПЛАН_MR.md", reportText,
                "отчёт не ссылается на план");
            StringAssert.Contains("Docs/ПЛАН_MR.md", journalText,
                "журнал решений не ссылается на план");
            StringAssert.Contains("Docs/ОТЧЁТ_MR.md", journalText,
                "журнал решений не ссылается на отчёт");
        }

        // ==================================================== ЗАЩИТА VR (Г)

        /// <summary>
        /// В VR НИЧЕГО НЕ МЕНЯЕТСЯ. Правило проекта: MR включается ТОЛЬКО при фактическом режиме
        /// MR; в VR виртуальная лаборатория остаётся видимой и робот не переносится.
        /// </summary>
        [Test]
        public void VrMode_KeepsVirtualLab()
        {
            Assert.IsFalse(KvXrMrRoom.HidesVirtualStands(false, true, false),
                "в VR стенды обязаны остаться видимыми (регрессия §35: «столов или роботов не видно»)");
            Assert.IsFalse(KvXrMrRoom.ShouldPlaceRobotOnRealTable(false),
                "в VR робота на реальный стол не ставим — реального стола нет");
        }
    }
}
