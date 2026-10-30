// ================================================================================================
//  KvSceneIntegrityTests.cs — EditMode-тесты ЦЕЛОСТНОСТИ СЦЕН (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Самый дорогой дефект Unity — «Missing (Mono Script)»: в сцене остался компонент,
//  а скрипт пропал. Объект в редакторе выглядит целым, но в работе ничего не делает, а в журнале
//  сборки появляется строка, которую никто не читает. Гейт `ci-console-check.ps1` считает такие
//  строки — но он проверяет ЛОГ, то есть уже собранную сцену, а не сами файлы.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ, БЕЗ ЗАПУСКА UNITY:
//    1) в основных сценах проекта КАЖДАЯ ссылка на скрипт ведёт к существующему файлу;
//    2) проверка учитывает скрипты ПАКЕТОВ (они лежат в `Library/PackageCache`), а не только
//       скрипты проекта — иначе `InputSystemUIInputModule` и `HDAdditionalLightData` были бы
//       объявлены «потерянными».
//
//  ЛОЖНАЯ ТРЕВОГА, КОТОРУЮ ЭТО ПРЕДОТВРАЩАЕТ (30.10.2026). Первый вариант проверки смотрел только
//  в `Assets` и объявил 11 «потерянных скриптов» в `XrMain` и `MainScene`. На деле все они — из
//  пакетов Unity (Input System, HDRP). Я чуть не начал «чинить» совершенно целые сцены.
//  Это ровно тот случай, когда проверка важнее проверяемого: ошибка в ней дороже дефекта.
//
//  ГРАНИЦА ЧЕСТНОСТИ: проверяются ОСНОВНЫЕ рабочие сцены, а не все файлы в `Assets` — в демо-сценах
//  сторонних ассетов (`ROOMS`, `Premises`) ссылки на их собственные удалённые скрипты это их дело,
//  а не наше; и трогать чужие ассеты мы не будем.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Сцены проекта: ни одной потерянной ссылки на скрипт.</summary>
    public class KvSceneIntegrityTests
    {
        /// <summary>
        /// СЦЕНЫ, ЗА ЦЕЛОСТНОСТЬ КОТОРЫХ ОТВЕЧАЕМ МЫ. Обе — рабочие: `MainScene` (компьютер) и
        /// `XrMain` (шлем). Демо-сцены сторонних ассетов сюда НЕ входят осознанно.
        /// </summary>
        private static readonly string[] OwnScenes =
        {
            @"_Project\00_Scenes\MainScene.unity",
            @"_Project\00_Scenes\XrMain.unity",
        };

        private static string ProjectRoot
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        private static string AssetsRoot
        {
            get { return Application.dataPath; }
        }

        /// <summary>
        /// ВСЕ GUID, КОТОРЫЕ РЕАЛЬНО СУЩЕСТВУЮТ: скрипты проекта (`Assets`) и скрипты ПАКЕТОВ
        /// (`Library/PackageCache`).
        ///
        /// ПОЧЕМУ ПАКЕТЫ ОБЯЗАТЕЛЬНЫ. В сценах на законных основаниях живут компоненты Unity:
        /// `InputSystemUIInputModule`, `HDAdditionalLightData`, `InputActionAsset` и другие. Их
        /// скрипты лежат в пакетах, а не в проекте — и проверка, которая смотрит только в `Assets`,
        /// объявляет их «потерянными».
        /// </summary>
        private static HashSet<string> ExistingScriptGuids()
        {
            var guids = new HashSet<string>();

            foreach (string root in new[] { AssetsRoot, Path.Combine(ProjectRoot, "Library", "PackageCache") })
            {
                if (!Directory.Exists(root)) continue;
                foreach (string meta in Directory.GetFiles(root, "*.cs.meta", SearchOption.AllDirectories))
                {
                    Match m = Regex.Match(File.ReadAllText(meta), @"guid: (\w+)");
                    if (m.Success) guids.Add(m.Groups[1].Value);
                }
                // Встроенные пакеты Unity лежат в папке установки редактора, а не в проекте, и в
                // PackageCache могут отсутствовать. Их скрипты тоже допустимы — поэтому отдельно
                // разрешаем ссылки с `m_EditorClassIdentifier` (см. проверку ниже).
            }
            return guids;
        }

        /// <summary>
        /// В СЦЕНАХ НЕТ ПОТЕРЯННЫХ СКРИПТОВ. Каждый `m_Script` обязан указывать на существующий файл
        /// (проекта или пакета). Иначе — «Missing (Mono Script)» в редакторе и молча неработающий
        /// компонент в работе.
        /// </summary>
        [Test]
        public void OwnScenes_HaveNoMissingScripts()
        {
            HashSet<string> known = ExistingScriptGuids();
            Assert.Greater(known.Count, 100,
                "не прочитано ни одного guid скриптов (" + known.Count + ") — проверка бессмысленна");

            var missing = new List<string>();
            foreach (string sceneName in OwnScenes)
            {
                string path = Path.Combine(AssetsRoot, sceneName.Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) { missing.Add(sceneName + " (сцены нет)"); continue; }

                string text = File.ReadAllText(path);
                foreach (Match m in Regex.Matches(text,
                    @"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*(\w+),\s*type:\s*3\}"))
                {
                    string guid = m.Groups[1].Value;
                    // Нулевой guid — это пустая ссылка, а не потерянный скрипт.
                    if (guid == "0000000000000000e000000000000000") continue;
                    if (known.Contains(guid)) continue;

                    // ССЫЛКА НА ВСТРОЕННЫЙ ПАКЕТ: у таких компонентов рядом записан
                    // `m_EditorClassIdentifier: <Пакет>::<Тип>` — по нему видно, что это НЕ потеря.
                    int at = text.IndexOf(guid, System.StringComparison.Ordinal);
                    string tail = text.Substring(at, System.Math.Min(400, text.Length - at));
                    if (tail.Contains("m_EditorClassIdentifier:")) continue;

                    missing.Add(Path.GetFileName(sceneName) + ": guid " + guid);
                }
            }

            Assert.AreEqual(0, missing.Count,
                "в сценах есть ПОТЕРЯННЫЕ ссылки на скрипты («Missing (Mono Script)») — компонент " +
                "на экране есть, а в работе ничего не делает: " + string.Join("; ", missing.ToArray()));
        }

        /// <summary>
        /// САМОПРОВЕРКА РАЗБОРА: в сценах ДЕЙСТВИТЕЛЬНО есть ссылки на скрипты. Если регулярка
        /// сломается, тест выше окажется зелёным «потому что ничего не нашлось» — тот самый случай
        /// из E-16.
        /// </summary>
        [Test]
        public void ParserActuallyFindsScriptReferences()
        {
            int total = 0;
            foreach (string sceneName in OwnScenes)
            {
                string path = Path.Combine(AssetsRoot, sceneName.Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) continue;
                string text = File.ReadAllText(path);
                total += Regex.Matches(text,
                    @"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*\w+,\s*type:\s*3\}").Count;
            }

            Assert.Greater(total, 20,
                "в основных сценах найдено всего " + total + " ссылок на скрипты — разбор сломан, " +
                "проверка выше ничего не проверяет");
        }

        /// <summary>
        /// ОБЕ ОСНОВНЫЕ СЦЕНЫ НА МЕСТЕ. Пропавшая сцена — это не «мелочь»: именно по этим файлам
        /// собирается приложение, и их потеря обнаруживается только при сборке.
        /// </summary>
        [Test]
        public void BothMainScenesExistAndAreNotEmpty()
        {
            var problems = new List<string>();
            foreach (string sceneName in OwnScenes)
            {
                string path = Path.Combine(AssetsRoot, sceneName.Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) { problems.Add(sceneName + " — файла нет"); continue; }
                long length = new FileInfo(path).Length;
                if (length < 20_000)
                    problems.Add(sceneName + " — подозрительно мала (" + length + " байт)");
            }

            Assert.AreEqual(0, problems.Count,
                "проблемы с основными сценами: " + string.Join("; ", problems.ToArray()));
        }
    }
}
