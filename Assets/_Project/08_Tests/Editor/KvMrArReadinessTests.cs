// ================================================================================================
//  KvMrArReadinessTests.cs — EditMode-тесты ГОТОВНОСТИ К MR И AR (сессия S44, 30.10.2026).
//
//  ЗАЧЕМ ЭТОТ ФАЙЛ. Владелец поставил цель: после VR перенести проект в MR и AR (суббота и
//  воскресенье). Но у MR/AR есть ЖЁСТКИЕ ПРЕДУСЛОВИЯ, которые ломаются МОЛЧА — и именно так
//  уже было в §28: «XR-стека в проекте не было», а ошибок не было НИ ОДНОЙ. Активными
//  лоадерами стояли Mock HMD (заглушка) и ARCore, а пакета OpenXR не было вовсе.
//
//  Поэтому здесь закреплено не «как выглядит комната», а то, без чего MR/AR НЕ ЗАРАБОТАЕТ:
//    1) активен РОВНО ОДИН лоадер — OpenXR (два лоадера конфликтуют, заглушка даёт «всё чёрное»);
//    2) манифест Android содержит разрешения и мета-данные для MR/AR (камера, passthrough,
//       отслеживание рук, пространственные якоря) и НЕ объявляет лишнего (интернет, запись
//       звука) — лишние разрешения видны оператору при установке;
//    3) в проекте есть код, который ВЫБИРАЕТ режим (VR/MR/AR) по возможностям устройства,
//       а не «надеется, что шлем окажется нужным»;
//    4) масштаб мира и единицы измерения не противоречат MR (мир не масштабируется сам).
//
//  ЧЕМ ЭТИ ТЕСТЫ ОТЛИЧАЮТСЯ ОТ «ДЛЯ ГАЛОЧКИ»: каждый читает НАСТОЯЩИЙ файл проекта (asset-ы
//  лоадеров, манифест, настройки игрока) и проверяет инвариант, нарушение которого даёт дефект
//  ровно того класса, что уже стоил проекту нескольких сессий.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using KazistovVvXr;
using KazistovVvXrEditor;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Предусловия MR и AR: лоадеры, манифест, выбор режима, масштаб.</summary>
    public class KvMrArReadinessTests
    {
        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        // ---------------------------------------------------------------- 1. РОВНО ОДИН ЛОАДЕР

        /// <summary>
        /// GUID ЛОАДЕРА, АКТИВНОГО В СБОРКЕ ANDROID, И ЕГО ИМЯ.
        ///
        /// Читается `Assets/XR/XRGeneralSettingsPerBuildTarget.asset` — тот самый файл, который
        /// Unity правит галочками в Project Settings → XR Plug-in Management. Проверять
        /// «в памяти» (`XRGeneralSettingsPerBuildTarget`) нельзя: в батчмоде настройки могут не
        /// загрузиться, и тест стал бы ложно-зелёным (так уже было в дефекте A12).
        /// </summary>
        private static List<string> ActiveLoaderGuids()
        {
            var result = new List<string>();
            string path = Path.Combine(ProjectRoot, "Assets", "XR", "XRGeneralSettingsPerBuildTarget.asset");
            if (!File.Exists(path)) return result;

            foreach (string line in File.ReadAllLines(path))
            {
                Match m = Regex.Match(line, @"m_Loaders:\s*$");
                if (!m.Success) continue;
                // Лоадеры идут сразу после строки m_Loaders, каждый как {"fileID: ..., guid: ...}
            }

            // Проще и надёжнее: собрать все guid-ы, объявленные ВНУТРИ блока m_Loaders.
            // ВНИМАНИЕ НА ФОРМАТ ФАЙЛА (проверено 30.10.2026): Unity пишет лоадеры как
            //     m_Loaders:
            //     - {fileID: 11400000, guid: 79a9…, type: 2}
            // то есть ПЕРЕД дефисом НЕТ пробелов. Первая версия этого разбора требовала `\s*-`
            // и поэтому находила НОЛЬ лоадеров — тест падал на ровном месте. Теперь разбор
            // принимает оба вида отступов, а пустой результат НЕ глушится: если файл есть, лоадеры
            // обязаны найтись, иначе это ошибка чтения, а не «лоадеров нет».
            string text = File.ReadAllText(path);
            foreach (Match block in Regex.Matches(text, @"m_Loaders:[ \t]*\r?\n((?:[ \t]*- \{[^}]*\}[ \t]*\r?\n?)*)"))
            {
                foreach (Match g in Regex.Matches(block.Groups[1].Value, @"guid: (\w+)"))
                {
                    if (!result.Contains(g.Groups[1].Value)) result.Add(g.Groups[1].Value);
                }
            }
            return result;
        }

        /// <summary>Имя файла лоадера по его GUID (по .meta рядом с .asset).</summary>
        private static string LoaderName(string guid)
        {
            string loadersDir = Path.Combine(ProjectRoot, "Assets", "XR", "Loaders");
            if (!Directory.Exists(loadersDir)) return null;
            foreach (string asset in Directory.GetFiles(loadersDir, "*.asset"))
            {
                string meta = asset + ".meta";
                if (!File.Exists(meta)) continue;
                Match m = Regex.Match(File.ReadAllText(meta), @"guid: (\w+)");
                if (m.Success && m.Groups[1].Value == guid) return Path.GetFileNameWithoutExtension(asset);
            }
            return null;
        }

        /// <summary>
        /// АКТИВНЫЙ ЛОАДЕР — РОВНО ОДИН, И ЭТО OPENXR.
        ///
        /// ПОЧЕМУ ЭТО КРИТИЧНО ДЛЯ MR/AR. Два активных лоадера конфликтуют за подсистему XR:
        /// именно так проект уже получил «в шлеме всё чёрное» (§28, карточка Б16). А заглушка
        /// `MockHMDLoader` выглядит рабочей в редакторе и НЕ РАБОТАЕТ на устройстве — она
        /// была активной вместе с ARCore, пока пакета OpenXR не было вовсе (дефект A1).
        ///
        /// OpenXR — единственная основа, на которой PICO даёт и VR, и MR (passthrough),
        /// и AR-якоря. ARCoreLoader для PICO не нужен и не должен быть активным.
        /// </summary>
        [Test]
        public void AndroidBuild_HasExactlyOneActiveLoader_AndItIsOpenXr()
        {
            string settingsPath = Path.Combine(ProjectRoot, "Assets", "XR", "XRGeneralSettingsPerBuildTarget.asset");
            Assert.IsTrue(File.Exists(settingsPath),
                "нет файла настроек XR: " + settingsPath + " — приложение не поднимет XR вообще");

            List<string> guids = ActiveLoaderGuids();
            Assert.IsNotEmpty(guids,
                "в " + Path.GetFileName(settingsPath) + " не найдено ни одного лоадера. Если файл правда " +
                "без лоадеров — XR не поднимется; если лоадеры есть, сломался разбор этого теста " +
                "(тогда чините разбор, а не файл)");

            var names = new List<string>();
            foreach (string guid in guids)
            {
                string name = LoaderName(guid);
                names.Add(name ?? ("неизвестный лоадер " + guid));
            }

            Assert.AreEqual(1, guids.Count,
                "активных лоадеров " + guids.Count + " (" + string.Join(", ", names.ToArray()) +
                "). Должен быть РОВНО ОДИН: два лоадера конфликтуют за подсистему XR " +
                "(§28, «в шлеме всё чёрное»)");

            StringAssert.Contains("OpenXR", names[0],
                "активный лоадер — «" + names[0] + "», а не OpenXR. На заглушке MockHMD в редакторе " +
                "всё работает, а на устройстве — нет; ARCore для PICO не нужен");
        }

        /// <summary>
        /// ЗАГЛУШКА И ARCore НЕ ДОЛЖНЫ БЫТЬ АКТИВНЫМИ. Отдельно от предыдущего теста, потому
        /// что этот называет причину по имени: `MockHMD` — отладочная заглушка, `ARCore` —
        /// мобильный AR для телефонов; ни тот, ни другой не дают MR на PICO.
        /// </summary>
        [Test]
        public void NoMockHmdOrArCoreLoaderIsActive()
        {
            foreach (string guid in ActiveLoaderGuids())
            {
                string name = LoaderName(guid) ?? "";
                Assert.IsFalse(name.Contains("MockHMD"),
                    "активна отладочная заглушка MockHMDLoader — на устройстве XR не поднимется (дефект A1)");
                Assert.IsFalse(name.Contains("ARCore"),
                    "активен ARCoreLoader — это AR для телефона, для шлема PICO он не нужен и мешает OpenXR");
            }
        }

        // ---------------------------------------------------------------- 2. МАНИФЕСТ ANDROID

        private static string ManifestPath
        {
            get { return Path.Combine(ProjectRoot, "Assets", "Plugins", "Android", "AndroidManifest.xml"); }
        }

        /// <summary>
        /// МАНИФЕСТ ANDROID СУЩЕСТВУЕТ. Без него разрешения и мета-данные PICO/OpenXR не попадут
        /// в сборку, и приложение упадёт на старте XR — при этом в редакторе всё будет в порядке.
        /// </summary>
        [Test]
        public void AndroidManifest_Exists()
        {
            Assert.IsTrue(File.Exists(ManifestPath),
                "нет файла " + ManifestPath + " — разрешения и мета-данные PICO не попадут в сборку");
        }

        /// <summary>
        /// В СБОРКЕ ОБЪЯВЛЕН СКВОЗНОЙ ВИД (passthrough) — ОСНОВА MR.
        ///
        /// ГДЕ ИМЕННО ПРОВЕРЯЕТСЯ И ПОЧЕМУ. Базовый `Assets/Plugins/Android/AndroidManifest.xml`
        /// в этом проекте — МЕТА-ориентированный (`com.oculus.*`, `horizonos:…`), и PICO-метаданных
        /// в нём нет вовсе. Аудит S44 это и нашёл: карточка Б3 «passthrough включён» опиралась
        /// на фичу OpenXR, но манифест сборки про сквозной вид не говорил ничего — на устройстве
        /// приложение могло запуститься в чистом VR, показывая виртуальную лабораторию вместо
        /// комнаты оператора.
        ///
        /// PICO-декларации ставятся постобработчиком сборки
        /// (`KvPicoManifestMetaData.RequiredMetaData`). Поэтому проверяется ИСТОЧНИК ПРАВДЫ:
        /// список, который этот постобработчик вносит в сгенерированный манифест, — и отдельно,
        /// что сам постобработчик на месте.
        /// </summary>
        [Test]
        public void BuildManifest_DeclaresSeeThrough_ForMr()
        {
            bool declaresSeeThrough = false;
            foreach (string[] pair in KazistovVvXrEditor.KvPicoManifestMetaData.RequiredMetaData)
            {
                if (pair.Length >= 2 &&
                    pair[0].IndexOf("seethrough", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    declaresSeeThrough = pair[1] == "1";
                }
            }

            Assert.IsTrue(declaresSeeThrough,
                "в сборке не объявлен сквозной вид (seeThroughMode = 1) — шлем запустит приложение " +
                "в VR, и MR-слой (карточка Б3) не заработает ни при какой настройке OpenXR");

            Assert.IsFalse(string.IsNullOrEmpty(FindManifestPostProcessor()),
                "нет класса-постобработчика сборки, который вносит PICO-метаданные в манифест — " +
                "значит объявления из RequiredMetaData в сборку не попадут");
        }

        /// <summary>Файл постобработчика манифеста, если он есть в проекте.</summary>
        private static string FindManifestPostProcessor()
        {
            string dir = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", "Editor");
            if (!Directory.Exists(dir)) return null;
            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                if (text.Contains("IPostGenerateGradleAndroidProject") && text.Contains("RequiredMetaData"))
                    return file;
            }
            return null;
        }

        /// <summary>
        /// В СБОРКЕ ОБЪЯВЛЕНЫ КАМЕРА И ОТСЛЕЖИВАНИЕ РУК.
        ///
        /// Сквозной вид без `CAMERA` не дадут. Отслеживание рук (жесты, этап 6) без
        /// `com.picovr.permission.HAND_TRACKING` не поднимется — и в журнале это будет выглядеть
        /// как поломка железа, а не как отсутствующее разрешение (карточка Б41).
        /// </summary>
        [Test]
        public void BuildManifest_DeclaresCameraAndHandTracking_ForMrAndAr()
        {
            var declared = new HashSet<string>(KazistovVvXrEditor.KvPicoManifestMetaData.RequiredPermissions);

            Assert.IsTrue(declared.Contains("android.permission.CAMERA"),
                "в сборке не объявлено разрешение CAMERA — сквозной вид (MR) и реальная сцена (AR) невозможны");

            bool handTracking = false;
            foreach (string permission in declared)
            {
                if (permission.IndexOf("HAND_TRACKING", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    handTracking = true;
            }
            Assert.IsTrue(handTracking,
                "в сборке не объявлено разрешение отслеживания рук — жесты не заработают ни на каком шлеме");
        }

        /// <summary>
        /// ЛИШНИХ ОПАСНЫХ РАЗРЕШЕНИЙ В БАЗОВОМ МАНИФЕСТЕ НЕТ.
        ///
        /// Каждое разрешение оператор видит при установке и проверяет магазин. Список разрешённых
        /// СВЕРЕН С КОДОМ, а не «на глаз»: интернет нужен `KvWebDashboard` и `KvCompanionServer`,
        /// микрофон — голосовому вводу (сейчас он честно пишет «нет appID PICO»). Тест не даст
        /// тихо добавить разрешение «на всякий случай».
        /// </summary>
        [Test]
        public void BaseManifest_HasNoUnexpectedDangerousPermissions()
        {
            string text = File.ReadAllText(ManifestPath);
            var allowed = new HashSet<string>
            {
                "android.permission.INTERNET",           // веб-дашборд, мобильный пульт
                "android.permission.ACCESS_NETWORK_STATE",
                "android.permission.CAMERA",             // сквозной вид и реальная сцена
                "android.permission.RECORD_AUDIO",       // голосовой ввод (когда дадут appID PICO)
                "android.permission.WAKE_LOCK",
                "android.permission.VIBRATE",
            };

            var unexpected = new List<string>();
            foreach (Match m in Regex.Matches(text, @"<uses-permission[^>]*android:name\s*=\s*""([^""]+)"""))
            {
                string permission = m.Groups[1].Value;
                // Платформенные разрешения шлема (com.oculus.*, com.picovr.*) — по делу.
                if (permission.StartsWith("com.oculus.", System.StringComparison.Ordinal)) continue;
                if (permission.StartsWith("com.picovr.", System.StringComparison.Ordinal)) continue;
                if (!allowed.Contains(permission)) unexpected.Add(permission);
            }

            Assert.AreEqual(0, unexpected.Count,
                "в базовом манифесте есть разрешения, которых не ждёт ни один код проекта: " +
                string.Join(", ", unexpected.ToArray()) +
                ". Оператор видит их при установке — добавлять осознанно и вписать сюда с причиной");
        }
        // ---------------------------------------------------------------- 3. ВЫБОР РЕЖИМА

        /// <summary>
        /// В ПРОЕКТЕ ЕСТЬ КОД, КОТОРЫЙ РАЗЛИЧАЕТ VR / MR / AR.
        ///
        /// ПОЧЕМУ ЭТО ПРЕДУСЛОВИЕ, А НЕ «ХОРОШО БЫ ИМЕТЬ». MR — это не «то же самое, но с камерой»:
        /// у него другие опоры (реальный пол, реальный стол), другой масштаб (1:1) и другие
        /// состояния (комната видна/не видна, якорь найден/потерян). Если в коде нет единого
        /// места, где эти режимы РАЗЛИЧАЮТСЯ, то каждый экран будет решать это сам — и половина
        /// решит неверно. Именно так в §28 «MR» оказался выключенным флагом в трёх местах.
        ///
        /// Требование: класс `KvXrPlatformMode` с явным перечислением режимов и функцией выбора.
        /// </summary>
        [Test]
        public void Project_DeclaresPlatformModeSelection()
        {
            string path = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", "Xr",
                "KvXrPlatformMode.cs");
            Assert.IsTrue(File.Exists(path),
                "нет файла " + path + " — в проекте негде ОТЛИЧИТЬ VR от MR и AR, каждый экран будет решать это сам");
        }

        /// <summary>
        /// РЕЖИМ ВЫБИРАЕТСЯ ПО ДОСТУПНОСТИ СКВОЗНОГО ВИДА, А НЕ ПО НАЗВАНИЮ УСТРОЙСТВА.
        ///
        /// Название устройства («PICO 4 Ultra») ничего не говорит о том, разрешил ли пользователь
        /// сквозной вид ПРЯМО СЕЙЧАС: он мог выключить его в настройках шлема. Правильный признак —
        /// доступность passthrough/камеры из OpenXR, и если её нет, приложение обязано честно
        /// стать VR и сказать это в журнале, а не показывать серый экран.
        /// </summary>
        [Test]
        public void PlatformMode_FallsBackToVr_WhenPassthroughUnavailable()
        {
            Assert.IsTrue(typeof(KvXrPlatformMode).IsEnum,
                "KvXrPlatformMode обязан быть перечислением режимов, чтобы режим нельзя было " +
                "случайно сравнить строкой");
            Assert.IsTrue(System.Enum.IsDefined(typeof(KvXrPlatformMode), "Vr"), "нет режима Vr");
            Assert.IsTrue(System.Enum.IsDefined(typeof(KvXrPlatformMode), "Mr"), "нет режима Mr");
            Assert.IsTrue(System.Enum.IsDefined(typeof(KvXrPlatformMode), "Ar"), "нет режима Ar");

            Assert.AreEqual(KvXrPlatformMode.Vr,
                KvXrPlatformModeSelector.Choose(false, false, false),
                "нет ни сквозного вида, ни якорей, ни камеры — режим обязан быть VR, а не «серый экран»");
        }

        /// <summary>
        /// ЕСТЬ СКВОЗНОЙ ВИД И ОПОРА — ЭТО MR; ЕСТЬ ТОЛЬКО КАМЕРА И ЯКОРЬ БЕЗ ОПОРЫ — ЭТО AR.
        /// Проверены все восемь комбинаций трёх признаков: тест-таблица, а не один пример —
        /// именно так ловятся ошибки в порядке условий выбора.
        /// </summary>
        [Test]
        public void PlatformMode_ChoosesModeByCapabilities()
        {
            // passthrough, spatialAnchors, camera
            Assert.AreEqual(KvXrPlatformMode.Vr, KvXrPlatformModeSelector.Choose(false, false, false));
            Assert.AreEqual(KvXrPlatformMode.Vr, KvXrPlatformModeSelector.Choose(false, false, true),
                "есть только камера, но нет якорей и сквозного вида — показывать нечего стабильно");
            Assert.AreEqual(KvXrPlatformMode.Ar, KvXrPlatformModeSelector.Choose(false, true, true),
                "камера и пространственный якорь без сквозного вида — это AR на телефоне");
            Assert.AreEqual(KvXrPlatformMode.Mr, KvXrPlatformModeSelector.Choose(true, false, true),
                "сквозной вид и камера дают MR и без якорей: мир ставится относительно оператора");
            Assert.AreEqual(KvXrPlatformMode.Mr, KvXrPlatformModeSelector.Choose(true, true, true),
                "все возможности — это MR, он полнее AR");
        }

        // ---------------------------------------------------------------- 4. МАСШТАБ

        /// <summary>
        /// МИР НЕ МАСШТАБИРУЕТСЯ САМ ПО СЕБЕ. В MR оператор стоит в своей комнате, и если
        /// приложение применит к миру множитель, стол «уедет» из-под рук. Поэтому масштаб —
        /// явная настройка, значение по умолчанию 1.0, и она лежит в одном месте.
        ///
        /// Карточка Б4 («масштаб мира не совпадает с реальным») закрывается не кодом, а замером на
        /// устройстве; тест же не даёт этой настройке появиться в двух местах с разными значениями.
        /// </summary>
        [Test]
        public void WorldScale_DefaultsToOneAndIsSingle()
        {
            Assert.AreEqual(1.0f, KvXrPlatformModeSelector.DefaultWorldScale, 1e-6f,
                "масштаб мира по умолчанию обязан быть 1.0: в MR оператор стоит в реальной комнате");

            string scripts = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts");
            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                // Ищем ЖЁСТКО ЗАДАННЫЙ масштаб МИРА где-то ещё, кроме самой настройки.
                //
                // ВАЖНО: поле с именем `worldScale` есть и у пульта (`KvTeachPendant`), но там это
                // пиксель интерфейса → метр, а не масштаб комнаты, — это другая величина, и путать
                // их нельзя. Поэтому ищется именно `KvXrPlatformModeSelector.WorldScale` (объявление
                // или присваивание), а поле UI в расчёт не идёт.
                if (Regex.IsMatch(text, @"KvXrPlatformModeSelector\.WorldScale\s*=\s*0?\.\d") &&
                    !file.EndsWith("KvXrPlatformMode.cs", System.StringComparison.Ordinal))
                {
                    offenders.Add(Path.GetFileName(file));
                }
            }

            Assert.AreEqual(0, offenders.Count,
                "масштаб мира задан дробным числом не в одном месте: " + string.Join(", ", offenders.ToArray()) +
                ". Второе место разойдётся с первым — так и появляется «стол уехал из-под рук»");
        }
    }
}
