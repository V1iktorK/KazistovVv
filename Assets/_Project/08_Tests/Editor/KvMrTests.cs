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
using System.Text.RegularExpressions;
using KazistovVvXr;
using KazistovVvXrEditor;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>MR: пробы, выбор бэкенда, сквозной вид, постановка на реальную поверхность, документы.</summary>
        [Category(KvTestCategories.Fast)]
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
        /// ПО УМОЛЧАНИЮ — VR, MR ВКЛЮЧАЕТСЯ В НАСТРОЙКАХ (решение владельца 04.11.2026:
        /// «сначала прога запускается как VR, но в настройках можно сделать MR»).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ: (1) по умолчанию режим комнаты ВЫКЛЮЧЕН; (2) в настройках шлема есть
        /// переключатель, который пишет настройку через `SetPassthrough` (иначе «включить MR»
        /// негде — ровно этот дефект был в S44, когда `SetPassthrough` не вызывался ниоткуда);
        /// (3) `MrActive` по-прежнему требует ФАКТИЧЕСКОГО режима устройства, а не только настройки.
        /// </summary>
        [Test]
        public void Mode_DefaultsToVr_WithMixedRealitySwitchInSettings()
        {
            FieldInfo field = typeof(KvXrSettings).GetField("DefaultPassthrough",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field, "в KvXrSettings нет DefaultPassthrough");
            Assert.AreEqual(0, (int)field.GetRawConstantValue(),
                "запуск по умолчанию должен быть в VR: MR — экспериментальная ветка и включается в настройках");

            string panel = ReadScript("KvXrGroupPanel.cs");
            // 12.11.2026. ЧТО БЫЛО: здесь искалось «Режим комнаты (MR)» — так назывался пункт,
            // пока владелец не сказал дословно: «нет переключателя VR/MR — добавь». Слово «комнаты»
            // и повтор «(MR)» не давали прочитать пункт как переключатель ДВУХ режимов.
            // ЧТО СТАЛО: пункт называется ровно так, как его искал владелец, — «Режим VR/MR».
            // Требование теста ПРЕЖНЕЕ и не ослаблено: в настройках шлема обязан быть пункт режима,
            // и он обязан ПИСАТЬ настройку. Изменилось только имя пункта.
            StringAssert.Contains("Режим VR/MR", panel,
                "в настройках шлема нет переключателя VR/MR — MR негде включить");
            // 11.11.2026: переключатель больше не пишет `SetPassthrough` НАПРЯМУЮ — он пишет через
            // общий `KvXrSettings.SetModeIndex` (одна логика на шлем и на ПК). Требование «панель
            // обязана ПИСАТЬ настройку, а не только показывать надпись» осталось тем же, поэтому
            // проверяется запись через общий путь; что этот путь доходит до `SetPassthrough`,
            // отдельно проверяет `KvVrMrSwitchTests.Switch_PersistsToPlayerPrefs`.
            StringAssert.Contains("KvXrSettings.ToggleMode()", panel,
                "переключатель режима должен ПИСАТЬ настройку, а не только показывать надпись");
            StringAssert.Contains("ModeLine", panel,
                "в меню нет строки режима — владелец не отличит VR-версию от MR");

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

        // ==================================================== Ж. РАСПОЗНАВАНИЕ ВИДНО И ДОКАЗУЕМО (S46)

        // ОТКУДА ЭТИ ТЕСТЫ (журнал шлема PICO 4 Ultra, свежая сборка, запуск В РЕЖИМЕ MR).
        // MR-конвейер стартовал успешно: «подсистема меша найдена (запускаю)», разрешение на
        // пространственные данные выдано, системный журнал показывает `ipc_sense_client_data_provider_create
        // scene capture` и `oxr_start_sense_data_provider_async_pico`. А ДАЛЬШЕ — ТИШИНА: за 2+ минуты
        // НИ ОДНОЙ строки о результате. Причина по коду: весь результат жил за `await` внутри пакета
        // PICO (`while (true) { PollFuture; if (state == Ready) … }`), у всего запроса не было одного
        // жёсткого предела, а итоговую строку не печатал никто. Оператор в шлеме не знал: ждать,
        // настраивать комнату или это поломка.
        //
        // ЧТО ПРОВЕРЯЕТСЯ (по приёмке): три исхода запроса различимы и с числами; при нуле столов
        // оператор получает указание, что делать; ожидание наблюдаемо (растёт время, есть подсказка);
        // повторного запроса в той же сессии нет; ни одной строки без числа.

        /// <summary>
        /// ТРИ ИСХОДА ЗАПРОСА РАЗМЕТКИ РАЗЛИЧИМЫ, И В КАЖДОМ ЕСТЬ ЧИСЛА.
        ///
        /// ПОЧЕМУ ЧИСТАЯ ФУНКЦИЯ. На устройстве строку иначе не проверить: журнал шлема читают
        /// глазами. Здесь решение о тексте отделено от `Debug.Log`, поэтому «найдено N», «столов 0»
        /// и «не ответил за X с» можно доказать машинно — и нельзя «поправить комментарием».
        /// </summary>
        [Test]
        public void SceneQueryResult_ThreeOutcomesAreDistinguishableAndNumeric()
        {
            string found = KvMrSurfaces.DescribeSceneQuery(
                KvSceneQueryOutcome.Found, 2, true, 12.4f, "PxrSemanticLabel.Table");
            string empty = KvMrSurfaces.DescribeSceneQuery(
                KvSceneQueryOutcome.Empty, 0, false, 9.1f, "PxrSemanticLabel.Table");
            string timeout = KvMrSurfaces.DescribeSceneQuery(
                KvSceneQueryOutcome.Timeout, 0, false, 15f, string.Empty);

            // 1. ФОРМА ОДНА НА ВСЕ ИСХОДЫ — по ней строку ищут в журнале шлема.
            StringAssert.StartsWith("[KvMr] РЕАЛЬНЫЙ СТОЛ: запрос выполнен за ", found);
            StringAssert.StartsWith("[KvMr] РЕАЛЬНЫЙ СТОЛ: запрос выполнен за ", empty);
            StringAssert.StartsWith("[KvMr] РЕАЛЬНЫЙ СТОЛ: запрос выполнен за ", timeout);

            // 2. ИСХОДЫ РАЗЛИЧИМЫ: три разных состояния не должны читаться одинаково.
            Assert.AreNotEqual(found, empty, "«столы найдены» и «столов нет» дали одинаковый текст");
            Assert.AreNotEqual(found, timeout, "«столы найдены» и «тайм-аут» дали одинаковый текст");
            Assert.AreNotEqual(empty, timeout, "«столов нет» и «тайм-аут» дали одинаковый текст");

            // 3. ЧИСЛА. Успех: секунды, число столов, пол.
            StringAssert.Contains("за 12.40 с", found);
            StringAssert.Contains("столов 2", found);
            StringAssert.Contains("пол есть", found);
            StringAssert.Contains("найдены", found);
            // Ноль: те же числа, но столов 0 и пола нет.
            StringAssert.Contains("за 9.10 с", empty);
            StringAssert.Contains("столов 0", empty);
            StringAssert.Contains("пол нет", empty);
            // Тайм-аут: время названо и слово «не ответил» присутствует.
            StringAssert.Contains("за 15.00 с", timeout);
            StringAssert.Contains("не ответил за 15.00 с", timeout);
            StringAssert.Contains("ТАЙМ-АУТ", timeout);

            Assert.IsTrue(Regex.IsMatch(found, @"\d"), "в строке об успехе нет числа: " + found);
            Assert.IsTrue(Regex.IsMatch(empty, @"\d"), "в строке о нуле столов нет числа: " + empty);
            Assert.IsTrue(Regex.IsMatch(timeout, @"\d"), "в строке о тайм-ауте нет числа: " + timeout);
        }

        /// <summary>
        /// ПРИ НУЛЕ СТОЛОВ И ПРИ ТАЙМ-АУТЕ ОПЕРАТОР УЗНАЁТ, ЧТО ДЕЛАТЬ.
        ///
        /// ЧЕГО НЕЛЬЗЯ ДЕЛАТЬ. Печатать «включите разметку» поверх «стол найден»: это читается как
        /// поломка. Поэтому подсказка выдаётся по отдельному решению
        /// (`SceneQueryNeedsOperatorAction`), а не всегда.
        /// </summary>
        [Test]
        public void SceneQueryResult_ZeroTablesSaysWhatToDo()
        {
            string empty = KvMrSurfaces.DescribeSceneQuery(
                KvSceneQueryOutcome.Empty, 0, false, 9.1f, "PxrSemanticLabel.Table");

            // «0 столов» — это не «в комнате нет стола», а «комната не размечена».
            StringAssert.Contains("разметки комнаты нет", empty);
            StringAssert.Contains("столов 0", empty);

            Assert.IsTrue(KvMrSurfaces.SceneQueryNeedsOperatorAction(KvSceneQueryOutcome.Empty),
                "при нуле столов оператор обязан получить указание, что делать");
            Assert.IsTrue(KvMrSurfaces.SceneQueryNeedsOperatorAction(KvSceneQueryOutcome.Timeout),
                "при тайм-ауте оператор обязан получить указание, что делать");
            Assert.IsFalse(KvMrSurfaces.SceneQueryNeedsOperatorAction(KvSceneQueryOutcome.Found),
                "поверх «столы найдены» подсказка «включите разметку» читается как поломка");

            string hint = KvMrSurfaces.SceneQueryOperatorHint();
            StringAssert.Contains("включите разметку комнаты в настройках PICO", hint);
            StringAssert.Contains("нельзя", hint, "подсказка обязана объяснять ПОСЛЕДСТВИЕ, а не только действие");
            StringAssert.Contains("постановка идёт по поверхностям меша", hint,
                "подсказка обязана сказать, что происходит ВМЕСТО разметки");

            // ПУТЬ В МЕНЮ НЕ ВЫДУМАН. В пакете SDK, который лежит в проекте, пунктов меню нет —
            // значит и в подсказке их быть не должно (выдуманный путь хуже отсутствующего).
            StringAssert.DoesNotContain("→", hint);
        }

        /// <summary>
        /// ОЖИДАНИЕ НАБЛЮДАЕМО: время растёт, числа есть, подсказка непустая.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. Пока поверхностей не было, в журнал не уходило НИЧЕГО
        /// (`UpdateRealTableReport` на нуле молча выходил): «ждём» и «сломалось» выглядели одинаково.
        /// </summary>
        [Test]
        public void WaitingStatus_GrowsWithTimeAndSurfacesAndHintsAreNotEmpty()
        {
            string at5 = KvMrSurfaces.DescribeWaitingSurfaces(0, 0, 5f);
            string at9 = KvMrSurfaces.DescribeWaitingSurfaces(0, 0, 9f);

            // 1. ВРЕМЯ РАСТЁТ в самой строке — по нему видно, что приложение живо.
            Assert.AreNotEqual(at5, at9,
                "строка ожидания не меняется со временем — «ждём» снова не отличить от «сломалось»");
            StringAssert.Contains("меш 0", at5);
            StringAssert.Contains("семантика 0", at5);
            StringAssert.Contains("ждём 5.00 с", at5);
            StringAssert.Contains("ждём 9.00 с", at9);
            StringAssert.StartsWith("[KvMr] реальные поверхности: ", at5);

            // 2. ПОДСКАЗКА НЕПУСТАЯ: строка обязана говорить, что делать.
            StringAssert.Contains("что делать:", at5);
            int tailStart = at5.IndexOf("что делать:", StringComparison.Ordinal) + "что делать:".Length;
            Assert.Greater(at5.Length - tailStart, 20, "подсказка в строке ожидания пустая: " + at5);

            // 3. ПОВЕРХНОСТИ ПОЯВИЛИСЬ — строка ДРУГАЯ, ждать больше нечего.
            string withSurfaces = KvMrSurfaces.DescribeWaitingSurfaces(3, 1, 5f);
            Assert.AreNotEqual(at5, withSurfaces,
                "появление поверхностей не изменило строку — оператор продолжит ждать на пустом месте");
            StringAssert.Contains("меш 3", withSurfaces);
            StringAssert.Contains("семантика 1", withSurfaces);

            // 4. НЕ ЧАЩЕ РАЗА В СЕКУНДУ: иначе журнал забивается и в нём тонет остальное.
            float[] moments = { 0f, 1f, 19.9f, 20f, 59f, 60f, 600f, float.NaN };
            for (int i = 0; i < moments.Length; i++)
                Assert.GreaterOrEqual(KvMrSurfaces.WaitingStatusIntervalSeconds(moments[i]), 1f,
                    "строка ожидания печатается чаще раза в секунду при waited=" + moments[i]);
        }

        /// <summary>
        /// ПОВТОРНЫЙ ЗАПРОС НЕ СПАМИТ: признак «уже спрашивали» блокирует второй запрос в сессии.
        ///
        /// ЧТО БЫЛО. Условие запроса стояло прямо в `Update` и включало `!Ready`: стоило появиться
        /// одному коллайдеру меша — запрос не выполнялся больше НИКОГДА, и в журнале не было ни
        /// «столов N», ни «разметки нет». Обратная ошибка (спрашивать каждый кадр) так же плоха:
        /// это нативные вызовы PICO.
        /// </summary>
        [Test]
        public void SceneQueryGate_AsksOncePerSessionAndNeverSpams()
        {
            Assert.IsTrue(KvMrSurfaces.ShouldRequestSceneAnchors(false, false, true, 8f, 8f),
                "через задержку запрос разметки обязан выполняться");
            Assert.IsFalse(KvMrSurfaces.ShouldRequestSceneAnchors(false, false, true, 7.9f, 8f),
                "запрос раньше задержки — лишний нативный вызов");
            Assert.IsFalse(KvMrSurfaces.ShouldRequestSceneAnchors(false, false, false, 100f, 8f),
                "подсистема меша ещё не выяснена — порядок строк в журнале был бы непонятным");
            Assert.IsFalse(KvMrSurfaces.ShouldRequestSceneAnchors(true, false, true, 100f, 8f),
                "признак «уже спрашивали» НЕ блокирует повторный запрос — это спам нативных вызовов");
            Assert.IsFalse(KvMrSurfaces.ShouldRequestSceneAnchors(false, true, true, 100f, 8f),
                "второй запрос поверх уже выполняющегося — спам");
            Assert.IsFalse(KvMrSurfaces.ShouldRequestSceneAnchors(false, false, true, float.NaN, 8f),
                "по нечислу времени запрос не решается: мусор на входе не должен ничего запускать");

            // СЕССИЯ ЦЕЛИКОМ: 10 минут (600 с) — ровно ОДИН запрос.
            bool asked = false;
            int requests = 0;
            for (int second = 0; second < 600; second++)
            {
                if (!KvMrSurfaces.ShouldRequestSceneAnchors(asked, false, true, second, 8f)) continue;
                asked = true;                 // так же, как в `UpdateSceneAnchorQuery`: ДО запуска
                requests++;
            }
            Assert.AreEqual(1, requests,
                "запрос разметки выполнился " + requests + " раз за сессию — обязан ровно один");
        }

        /// <summary>
        /// АНТИ-ВАКУУМ: В КАЖДОЙ СТРОКЕ ЕСТЬ ЧИСЛО.
        ///
        /// Зачем: строка без числа не доказывает ничего. «Стол не найден» и «стол не найден, ждём 42.0 с»
        /// читаются по-разному — первое похоже на поломку, второе показывает, что приложение работает.
        /// Проверяется ВЕСЬ набор исходов, а не выбранные примеры.
        /// </summary>
        [Test]
        public void ReportLines_AllCarryNumbers_NoVacuum()
        {
            var lines = new List<string>();
            int[] tableCounts = { 0, 1, 3 };
            bool[] floors = { false, true };

            foreach (KvSceneQueryOutcome outcome in Enum.GetValues(typeof(KvSceneQueryOutcome)))
                for (int i = 0; i < tableCounts.Length; i++)
                    for (int f = 0; f < floors.Length; f++)
                        lines.Add(KvMrSurfaces.DescribeSceneQuery(outcome, tableCounts[i], floors[f], 7.5f, "код"));

            float[] moments = { 0f, 1.5f, 25f, 300f };
            for (int i = 0; i < moments.Length; i++)
            {
                lines.Add(KvMrSurfaces.DescribeWaitingSurfaces(0, 0, moments[i]));
                lines.Add(KvMrSurfaces.DescribeWaitingSurfaces(2, 1, moments[i]));
            }

            Assert.Greater(lines.Count, 10, "строк для проверки слишком мало — проверка была бы вакуумной");

            var mute = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (!Regex.IsMatch(lines[i], @"\d")) mute.Add("без числа: " + lines[i]);
                else if (lines[i].IndexOf("стол", StringComparison.Ordinal) < 0 &&
                         lines[i].IndexOf("меш", StringComparison.Ordinal) < 0)
                    mute.Add("ни о чём: " + lines[i]);
            }
            Assert.AreEqual(0, mute.Count,
                "строки без чисел (или без предмета) — тестировщик не отличит «ждём» от «сломано»: " +
                string.Join(" | ", mute.ToArray()));

            // Итог запроса обязан нести «столов <число>» — это и есть доказательство распознавания.
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].StartsWith("[KvMr] РЕАЛЬНЫЙ СТОЛ:", StringComparison.Ordinal))
                    StringAssert.IsMatch(@"столов\s+\d+", lines[i]);
        }
    }
}

