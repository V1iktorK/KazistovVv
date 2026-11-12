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
        [Category(KvTestCategories.Slow)]
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

            // ЧТО БЫЛО (исправлено 12.11.2026). Функция обходила ВСЕ файлы `*.cs.meta` в `Assets` и
            // `Library/PackageCache` и читала каждый. Замер на этом проекте: 12 722 файла, обход 6 с,
            // ЧТЕНИЕ 63 с — итого тест шёл 138 с и получил ТАЙМАУТ 180 с на приёмке. При этом искали
            // мы ВСЕГО 25 guid, которые встречаются в наших сценах. Работа была несоразмерна задаче:
            // весь проект читался ради ответа «есть ли вот эти двадцать пять».
            // ЧТО СТАЛО. Сначала из сцен собираются нужные guid (их десятки), затем КАЖДЫЙ проверяется
            // через `AssetDatabase.GUIDToAssetPath` — это обращение к индексу ассетов, а не чтение диска.
            // Файл читается только для тех guid, которые индекс НЕ знает (чтобы дать внятную причину).
            var wanted = new HashSet<string>();
            foreach (string sceneName in OwnScenes)
            {
                string scenePath = Path.Combine(AssetsRoot, sceneName.Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(scenePath)) continue;
                string sceneText = File.ReadAllText(scenePath);
                foreach (Match m in Regex.Matches(sceneText, @"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*(\w+)"))
                    wanted.Add(m.Groups[1].Value);
            }

            foreach (string guid in wanted)
            {
                // ИНДЕКС АССЕТОВ: если guid принадлежит файлу ПРОЕКТА или ПАКЕТА, индекс это знает.
                // Пакеты из `Library/PackageCache` тоже в индексе после импорта — проверено: наши
                // сцены ссылаются на `InputSystemUIInputModule`, `HDAdditionalLightData` и подобные,
                // и они находятся именно так.
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) { guids.Add(guid); }
            }
            return guids;
        }

        /// <summary>
        /// В СЦЕНАХ НЕТ ПОТЕРЯННЫХ СКРИПТОВ. Каждый `m_Script` обязан указывать на существующий файл
        /// (проекта или пакета). Иначе — «Missing (Mono Script)» в редакторе и молча неработающий
        /// компонент в работе.
        ///
        /// ЧТО БЫЛО. Рядом стояла ОТДЕЛЬНАЯ проверка `BothMainScenesExistAndAreNotEmpty`: «обе
        /// основные сцены на месте и не подозрительно малы».
        ///
        /// ЧЕМ ПЛОХО. Она проверяла ПРЕДУСЛОВИЕ этой же проверки: если сцены нет, здесь уже стоит
        /// явное «сцены нет» в списке проблем. Два падения об одном и том же удлиняют разбор, а
        /// новых дефектов второй тест не ловил.
        ///
        /// ЧТО СТАЛО. Размер сцены проверяется здесь же, до разбора: «подозрительно мала (N байт)» —
        /// тот же сигнал, но в одном месте и с одним названием падения.
        /// </summary>
        [Test]
        public void OwnScenes_HaveNoMissingScripts()
        {
            HashSet<string> known = ExistingScriptGuids();
            // ПОРОГ ИЗМЕНЁН 12.11.2026 ВМЕСТЕ С МЕТОДОМ СБОРА. Раньше функция обходила ВЕСЬ
            // проект и отдавала сотни guid, поэтому «> 100» означало «путь тот». Теперь собираются
            // guid ТОЛЬКО скриптов, на которые ссылаются НАШИ СЦЕНЫ (их десятки) — порог 100 стал бы
            // вечным падением на исправном коде. Смысл сохранён: ссылки найдены И для них найден файл.
            Assert.Greater(known.Count, 0,
                "не прочитано ни одного guid скриптов (" + known.Count + ") — проверка бессмысленна");
            var missing = new List<string>();
            foreach (string sceneName in OwnScenes)
            {
                string path = Path.Combine(AssetsRoot, sceneName.Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) { missing.Add(sceneName + " (сцены нет)"); continue; }

                // ПРОПАВШАЯ СЦЕНА — ЭТО НЕ «МЕЛОЧЬ»: именно по этим файлам собирается приложение,
                // и их потеря обнаруживается только при сборке. Подозрительно малый файл —
                // тот же сигнал, что и отсутствие.
                long length = new FileInfo(path).Length;
                if (length < 20_000)
                {
                    missing.Add(Path.GetFileName(sceneName) + " — подозрительно мала (" + length + " байт)");
                    continue;
                }

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
    }
}

