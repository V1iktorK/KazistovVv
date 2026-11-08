// ================================================================================================
//  KvR1FixTests.cs — ТЕСТЫ НАХОДОК ПРИЁМКИ R1 И ДЕФЕКТА УСТРОЙСТВА R6 (11.11.2026).
//
//  ОТКУДА ЗАДАЧА. Тестировщик вернул в работу три пункта приёмки (раздел 9.2 распоряжения) и
//  добавил новый дефект, видимый только на планшете (раздел 9.3):
//    R1-1  артефакт шлема не заменяется свежим — сборщик кладёт свежий файл ПОД ДРУГИМ ИМЕНЕМ;
//    R1-2  после прогона `git status` не чист — остатки P2-7 (`renderer.material` вне RobotController);
//    R1-3  P4-4 (`asmdef`) не начат — вынесен в отдельный файл тестов (KvP4FixTests);
//    R6-1  на планшете в AR команда «Настройки» выполняется, а панель не появляется.
//
//  ПРАВИЛО ТЕСТОВ ЗДЕСЬ ТО ЖЕ, ЧТО И В P1–P3: каждый обязан ПАДАТЬ на старом коде. Там, где
//  поведение нельзя оторвать от сцены (сборщик — это PowerShell; раскладка панели требует канваса),
//  проверяется ПРАВИЛО по исходнику — и всегда с НЕВАКУУМНОЙ страховкой: пустой или переименованный
//  файл обязан давать провал, а не зелёный (урок P4-2).
// ================================================================================================

using System.IO;
using System.Text.RegularExpressions;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Находки приёмки R1-1/R1-2 и дефект устройства R6-1.</summary>
        [Category(KvTestCategories.Fast)]
    public class KvR1FixTests
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

        /// <summary>
        /// ВЫЗОВЫ `renderer.material` (копия материала), НО НЕ `sharedMaterial`.
        ///
        /// Регулярка названа отдельно, потому что это ЕДИНСТВЕННОЕ, что здесь проверяется: разница
        /// между `material` и `sharedMaterial` — в точности разница между «своя копия на рендерер»
        /// и «общий материал». Опечатка в регулярке сделала бы проверку вечно зелёной.
        /// </summary>
        private static readonly Regex MaterialCopy = new Regex(
            @"\.(material|materials)\b", RegexOptions.Compiled);

        /// <summary>Строки с обращением к копии материала; пусто — правильно.</summary>
        private static string MaterialCopyLines(string relativePath)
        {
            string code = StripComments(ReadFile(relativePath));
            Assert.Greater(code.Length, 200,
                "исходник " + relativePath + " подозрительно мал (" + code.Length +
                " символов без комментариев) — проверка была бы вакуумной (урок P4-2)");

            string result = "";
            string[] lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (!MaterialCopy.IsMatch(line)) continue;
                if (line.Contains("sharedMaterial")) continue;   // общий материал — это правильно
                result += relativePath + ":" + (i + 1) + " → " + line.Trim() + "\n";
            }
            return result;
        }

        // ================================================================ R1-2: остатки P2-7

        /// <summary>
        /// R1-2: НИ ОДНОГО `renderer.material` В МЕСТАХ, КОТОРЫЕ НАЗВАЛ ТЕСТИРОВЩИК.
        ///
        /// ЧТО БЫЛО. P2-7 закрыли только в `RobotController`. Остались: усиление света оболочки
        /// (`BoostRenderers`), превью окна выбора объекта (`CenterWindow`), материалы фантомов
        /// (`LaserAndPhantomManagers`) и маркер цели (`TargetMarker`). Прогон после этого оставлял
        /// изменённый ассет материала робота, и критерий приёмки № 6 (`git status` чист) не
        /// выполнялся — находка R1-2.
        ///
        /// ЧЕМ ЭТО ГРОЗИТ. `renderer.material` создаёт ЛИЧНУЮ копию материала на каждый рендерер;
        /// копии никто не уничтожает, и правка «на месте» меняет уже невидимую копию, а не то, что
        /// видит оператор. В редакторе вдобавок на каждую копию печатается предупреждение Unity.
        /// </summary>
        [Test]
        public void R1_2_NoMaterialCopies_InNamedRuntimeFiles()
        {
            string[] files =
            {
                "Assets/_Project/01_Scripts/Core/RobotController.cs",
                "Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs",
                "Assets/_Project/06_KazistovVv_UI/03_Scripts/Zones/CenterWindow.cs",
                "Assets/_Project/01_Scripts/Trajectory/LaserAndPhantomManagers.cs",
                "Assets/_Project/01_Scripts/Integration/TargetMarker.cs",
                "Assets/_Project/01_Scripts/Xr/KvXrLocomotion.cs",
                "Assets/_Project/01_Scripts/Features/KvForceHeat.cs"
            };

            string bad = "";
            foreach (string file in files) bad += MaterialCopyLines(file);

            Assert.AreEqual("", bad,
                "копия материала через `renderer.material` (P2-7 / R1-2 → изменённый ассет и утечки):\n" + bad);
        }

        /// <summary>
        /// R1-2: ПОДСВЕТКА РОБОТА И ПРЕВЬЮ ИДУТ ЧЕРЕЗ `MaterialPropertyBlock`.
        ///
        /// Отдельная проверка от предыдущей: там запрещён ВРЕДНЫЙ приём, здесь требуется
        /// ПРАВИЛЬНЫЙ. Без этого можно было бы «починить» удалением покраски вовсе — и тест выше
        /// остался бы зелёным, хотя подсветка выделения исчезла.
        /// </summary>
        [Test]
        public void R1_2_HighlightUsesPropertyBlock()
        {
            string robot = StripComments(ReadFile("Assets/_Project/01_Scripts/Core/RobotController.cs"));
            StringAssert.Contains("SetPropertyBlock", robot,
                "подсветка выделенного робота не использует MaterialPropertyBlock (P2-7)");

            string window = StripComments(
                ReadFile("Assets/_Project/06_KazistovVv_UI/03_Scripts/Zones/CenterWindow.cs"));
            StringAssert.Contains("SetPropertyBlock", window,
                "покраска превью не использует MaterialPropertyBlock (P2-7 / R1-2)");
            // Материал превью создан нами — значит и уничтожать его обязаны мы.
            StringAssert.Contains("Destroy(previewMaterial)", window,
                "материал превью не уничтожается — утечка на каждое пересоздание призрака (P2-7)");
        }

        /// <summary>
        /// R1-2: КОПИИ МАТЕРИАЛОВ ФАНТОМА УЧИТЫВАЮТСЯ И УНИЧТОЖАЮТСЯ.
        ///
        /// В режиме «рентген-призрак» меняются свойства самого материала (прозрачность, очередь
        /// отрисовки), поэтому блоком свойств там не обойтись — но копии обязаны быть учтены,
        /// иначе каждый показ фантома оставляет десятки материалов-сирот.
        /// </summary>
        [Test]
        public void R1_2_PhantomMaterials_AreOwnedAndDestroyed()
        {
            string phantoms = StripComments(
                ReadFile("Assets/_Project/01_Scripts/Trajectory/LaserAndPhantomManagers.cs"));
            StringAssert.Contains("PhantomMaterialOwner", phantoms,
                "у копий материалов фантома нет владельца — они переживут фантом (P2-7 / R1-2)");
            StringAssert.Contains("owner.Track(", phantoms,
                "копии материалов фантома создаются, но не учитываются (P2-7 / R1-2)");
            StringAssert.Contains("Destroy(created[i])", phantoms,
                "учтённые копии материалов фантома не уничтожаются (P2-7 / R1-2)");
        }

        // ================================================================ R1-1: свежий APK

        /// <summary>
        /// R1-1: СБОРЩИК КЛАДЁТ СВЕЖИЙ APK ПОД ПРАВИЛЬНЫМ ИМЕНЕМ И ПРОВЕРЯЕТ ВРЕМЯ.
        ///
        /// ЧТО БЫЛО. Сборка шлема (Vulkan) собиралась, но результат оставался в КАТАЛОГЕ СБОРКИ под
        /// прежним общим именем, а в `Builds` продолжал лежать файл от 05.11.2026. То есть
        /// «артефакт для шлема» формально был, а по факту это была сборка БЕЗ правок.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Сборщик обязан: (1) знать ОБА имени артефактов; (2) публиковать
        /// результат в `Builds` ИСХОДНОГО проекта; (3) сравнивать время файла с началом прогона.
        /// </summary>
        [Test]
        public void R1_1_BuildScript_PublishesFreshArtifactUnderCorrectName()
        {
            string script = StripComments(ReadFile("Tools/xr-build-apk-ascii.ps1"));
            Assert.Greater(script.Length, 4000,
                "скрипт сборки подозрительно мал — проверка была бы вакуумной (урок P4-2)");

            StringAssert.Contains("KazistovVv-VR-vulkan.apk", script,
                "сборщик не знает имени артефакта для шлема (P4-19 п.1 / R1-1)");
            StringAssert.Contains("KazistovVv-AR-gles3.apk", script,
                "сборщик не знает имени артефакта для планшета (P4-19 п.1 / R1-1)");

            StringAssert.Contains("$publishDir", script,
                "сборщик не публикует артефакт в каталог Builds исходного проекта (R1-1)");
            StringAssert.Contains("$buildStarted", script,
                "сборщик не сравнивает время файла с началом прогона (R1-1)");
            StringAssert.Contains("LastWriteTime", script,
                "нет проверки ВРЕМЕНИ файла — «свежесть» снова подменена наличием (R1-1)");
            StringAssert.Contains("APK ОПУБЛИКОВАН", script,
                "нет отдельной строки об успешной публикации — по журналу не отличить от старого файла (R1-1)");
        }

        // ================================================================ R6-1: панель в AR

        /// <summary>
        /// R6-1: «ПОКАЗАНА» И «ВИДНА» — РАЗНЫЕ ВЕЩИ, И РЕШЕНИЕ О ПЕРЕКЛЮЧЕНИИ ПРИНИМАЕТСЯ ПО ВТОРОМУ.
        ///
        /// ЧТО БЫЛО. `TogglePanel` решал по флагу (`Shown`). Панель, включённая но стоящая за
        /// экраном или нулевого размера, при первом нажатии ЗАКРЫВАЛАСЬ: команда выполнена, на
        /// экране ничего — ровно то, что тестировщик увидел на планшете в AR.
        /// </summary>
        [Test]
        public void R6_1_DockGeometry_DistinguishesShownFromVisible()
        {
            Vector2 screen = new Vector2(1800f, 1100f);

            // Панель на своём месте — видна.
            Assert.IsTrue(KvDockGeometry.IsVisibleOnScreen(new Rect(0f, 30f, 1800f, 240f), screen),
                "панель на штатном месте признана невидимой");

            // Панель уехала за верхний край экрана — НЕ видна.
            Assert.IsFalse(KvDockGeometry.IsVisibleOnScreen(new Rect(0f, 5000f, 1800f, 240f), screen),
                "панель за пределами экрана признана видимой (R6-1)");

            // Панель нулевого размера — НЕ видна, даже если её координаты «в экране».
            Assert.IsFalse(KvDockGeometry.IsVisibleOnScreen(new Rect(10f, 10f, 0f, 0f), screen),
                "панель нулевого размера признана видимой (R6-1)");

            // Виден только уголок (2 % площади) — для оператора это «не показана».
            Assert.IsFalse(KvDockGeometry.IsVisibleOnScreen(new Rect(1790f, 30f, 1800f, 240f), screen),
                "панель, у которой виден один уголок, признана видимой (R6-1)");

            // Решение о починке раскладки — правило, а не повтор геометрии.
            Assert.IsTrue(KvDockGeometry.NeedsPlacementRepair(true, new Rect(0f, 5000f, 1800f, 240f), screen),
                "включённая, но невидимая панель не требует починки раскладки (R6-1)");
            Assert.IsFalse(KvDockGeometry.NeedsPlacementRepair(false, new Rect(0f, 5000f, 1800f, 240f), screen),
                "выключенная панель не требует починки — её просто не открывали (R6-1)");
            Assert.IsFalse(KvDockGeometry.NeedsPlacementRepair(true, new Rect(0f, 30f, 1800f, 240f), screen),
                "видимую панель чинить не нужно — иначе раскладка оператора сбрасывалась бы на каждом открытии");
        }

        /// <summary>
        /// R6-1: КОМАНДА ПАНЕЛИ ПРИНИМАЕТ РЕШЕНИЕ ПО ЭФФЕКТИВНОЙ ВИДИМОСТИ, А ПАНЕЛИ РИСУЮТСЯ
        /// ПОВЕРХ ОВЕРЛЕЕВ.
        ///
        /// Вторая половина дефекта: в AR поверх экрана висит ВТОРОЙ канвас (`KvArUi`,
        /// `sortingOrder = 300`), и широкая нижняя панель настроек пряталась ПОД ним. Обе половины
        /// обязаны быть в коде — по отдельности каждая оставляет дефект воспроизводимым.
        /// </summary>
        [Test]
        public void R6_1_ToggleUsesEffectiveVisibility_AndDocksRenderOnTop()
        {
            string ui = StripComments(ReadFile(
                "Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs"));

            StringAssert.Contains("IsPanelEffectivelyVisible", ui,
                "переключение панели снова решает по флагу, а не по фактической видимости (R6-1)");
            Assert.IsFalse(Regex.IsMatch(ui, @"TogglePanel\s*\(\s*string\s+id\s*\)\s*\{\s*SetPanel\("),
                "TogglePanel снова переключает флаг напрямую — дефект R6-1 вернулся");

            StringAssert.Contains("overrideSorting", ui,
                "dock-панели не поднимаются над сторонними оверлеями — в AR их закрывает KvArUi (R6-1)");
            StringAssert.Contains("DockSortingOrder", ui,
                "порядок сортировки панелей не задан явно (R6-1)");

            // Порядок панелей обязан быть ВЫШЕ AR-оверлея, иначе починка бессмысленна.
            string ar = StripComments(ReadFile("Assets/_Project/01_Scripts/Ar/KvArUi.cs"));
            Match arOrder = Regex.Match(ar, @"sortingOrder\s*=\s*(\d+)");
            Assert.IsTrue(arOrder.Success, "не найден порядок сортировки AR-оверлея — проверка потеряла смысл");
            int arSorting = int.Parse(arOrder.Groups[1].Value);
            Assert.Greater(KazistovVvUIManager.DockSortingOrder, arSorting,
                "порядок dock-панелей (" + KazistovVvUIManager.DockSortingOrder +
                ") не выше AR-оверлея (" + arSorting + ") — панель снова окажется под ним (R6-1)");
        }
    }
}

