// ================================================================================================
//  KvDeadCodeTests.cs — EditMode-тест-СТОРОЖ МЁРТВОГО КОДА (сессия S44, пункт 5.25).
//
//  ОТКУДА ЗАДАЧА. В проекте лежали десять скриптов, которые НИКТО НЕ ВЫЗЫВАЕТ и которых нет ни в
//  одной сцене: старые `MainMenu`, `SettingsMenu`, `VRInputManager`, `TrajectoryPlayer` и другие.
//  Проверено 30.10.2026 обходом всех `.cs` и всех сцен/префабов по `guid`.
//
//  ЧЕМ МЁРТВЫЙ КОД МЕШАЕТ. Это не «лишние файлы». Это:
//    • ложные улики при разборе: правишь `SettingsMenu`, а настройки живут в `KvSettingsView`;
//    • мнимые дубли: два класса с одинаковым назначением, и неясно, какой настоящий;
//    • цена компиляции и поиска;
//    • и главное — новичок правит мёртвый код и удивляется, что ничего не меняется.
//
//  ЧТО СДЕЛАНО. Каждый помечен `[Obsolete]` С ПРИЧИНОЙ и указанием, кто теперь делает эту работу.
//  Удалять сразу не стал: правило проекта — сначала метка с причиной (как с
//  `TrajectoryPlannerController`), удаление — отдельным решением владельца.
//
//  ЧТО ЗАКРЕПЛЯЕТ ЭТОТ ТЕСТ:
//    1) каждый из десяти остаётся ПОМЕЧЕННЫМ (снимут метку — прогон покраснеет);
//    2) у каждой метки ЕСТЬ ПРИЧИНА (пустая метка равна «промолчали»);
//    3) в причине назван ЗАМЕНИТЕЛЬ — иначе метка бесполезна тому, кто ищет «что использовать»;
//    4) помеченные файлы НЕ в сценах (иначе пометили живой код — это опаснее, чем мёртвый).
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Мёртвый код: помечен, обоснован, не привязан к сценам.</summary>
    public class KvDeadCodeTests
    {
        /// <summary>
        /// ФАЙЛЫ, ПРИЗНАННЫЕ МЁРТВЫМИ 30.10.2026 (проверено обходом всех `.cs` и всех сцен/префабов).
        /// Список — контракт: файл из него обязан оставаться помеченным `[Obsolete]`.
        /// </summary>
        private static readonly string[] DeadFiles =
        {
            "TrajectoryPlayer.cs",
            "TrajectoryRecorder.cs",
            "VRHandTracker.cs",
            "VRInputManager.cs",
            "PlacementController.cs",
            "MainMenu.cs",
            "SettingsMenu.cs",
            "SettingsSaver.cs",
            "StandsMenu.cs",
            "KeyboardController.cs",
        };

        private static string ScriptsRoot
        {
            get { return Path.Combine(Application.dataPath, "_Project", "01_Scripts"); }
        }

        /// <summary>Путь к файлу по имени (ищется под `01_Scripts`).</summary>
        private static string Find(string fileName)
        {
            if (!Directory.Exists(ScriptsRoot)) return null;
            foreach (string path in Directory.GetFiles(ScriptsRoot, fileName, SearchOption.AllDirectories))
                return path;
            return null;
        }

        // ---------------------------------------------------------------- 1. МЕТКИ НА МЕСТЕ

        /// <summary>
        /// ВСЕ ДЕСЯТЬ ОСТАЮТСЯ ПОМЕЧЕННЫМИ. Если кто-то снимет `[Obsolete]`, файл снова выглядит
        /// «живым» — и следующий разработчик потратит время на правку кода, который ни на что не
        /// влияет. Это ровно тот случай, ради которого тест и написан.
        /// </summary>
        [Test]
        public void AllKnownDeadFiles_StayMarkedObsolete()
        {
            var unmarked = new List<string>();
            var missing = new List<string>();

            foreach (string name in DeadFiles)
            {
                string path = Find(name);
                if (path == null) { missing.Add(name); continue; }
                string text = File.ReadAllText(path);
                if (!text.Contains("[System.Obsolete(") && !text.Contains("[Obsolete("))
                    unmarked.Add(name);
            }

            Assert.AreEqual(0, missing.Count,
                "файлы из списка мёртвого кода не найдены: " + string.Join(", ", missing.ToArray()) +
                ". Удалили? Тогда уберите из списка и запишите это в отчёт");
            Assert.AreEqual(0, unmarked.Count,
                "эти файлы снова БЕЗ метки [Obsolete] — они выглядят живыми, хотя никем не " +
                "вызываются: " + string.Join(", ", unmarked.ToArray()));
        }

        // ---------------------------------------------------------------- 2. ПРИЧИНА ЕСТЬ

        /// <summary>
        /// У КАЖДОЙ МЕТКИ ЕСТЬ ПРИЧИНА. `[Obsolete]` без текста — это «помечено, но непонятно
        /// почему». Правило проекта (как с `TrajectoryPlannerController`): у метки обязана быть
        /// причина, иначе через месяц её снимут «за ненадобностью».
        /// </summary>
        [Test]
        public void EveryObsoleteMark_HasAReason()
        {
            var silent = new List<string>();

            foreach (string name in DeadFiles)
            {
                string path = Find(name);
                if (path == null) continue;                 // уже названо выше
                string text = File.ReadAllText(path);

                Match m = Regex.Match(text, @"\[(?:System\.)?Obsolete\s*\(\s*""([^""]*)""");
                if (!m.Success) { silent.Add(name + " (нет текста причины)"); continue; }
                if (m.Groups[1].Value.Trim().Length < 20)
                    silent.Add(name + " (причина короче 20 символов: «" + m.Groups[1].Value + "»)");
            }

            Assert.AreEqual(0, silent.Count,
                "у этих пометок нет внятной причины — через месяц метку снимут как «лишнюю»: " +
                string.Join("; ", silent.ToArray()));
        }

        /// <summary>
        /// В ПРИЧИНЕ НАЗВАН ЗАМЕНИТЕЛЬ. Смысл метки — не «не трогай», а «используй вон то». Без
        /// замены метка только пугает, но не помогает.
        /// </summary>
        [Test]
        public void EveryObsoleteMark_NamesTheReplacement()
        {
            var vague = new List<string>();

            foreach (string name in DeadFiles)
            {
                string path = Find(name);
                if (path == null) continue;
                string text = File.ReadAllText(path);

                Match m = Regex.Match(text, @"\[(?:System\.)?Obsolete\s*\(\s*""([^""]*)""");
                if (!m.Success) { vague.Add(name + " (нет причины)"); continue; }

                string reason = m.Groups[1].Value;
                // Замена названа, если в причине есть ИМЯ ТИПА (CamelCase с заглавной буквы внутри),
                // например `KvSettingsView`, `MotionExecutor`, `TrajectoryFlowController`.
                //
                // СНАЧАЛА БЫЛО УЖЕ: только «Kv…», и тест справедливо падал на `MotionExecutor` —
                // класс без префикса Kv, но совершенно настоящий. Слишком узкое правило проверки
                // даёт ложные провалы ровно так же, как слишком широкое — пропуски.
                bool namesReplacement = Regex.IsMatch(reason, @"\b[A-Z][a-z]+[A-Z]\w*") ||
                                        reason.IndexOf("делает", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!namesReplacement) vague.Add(name + " («" + reason + "»)");
            }

            Assert.AreEqual(0, vague.Count,
                "в этих причинах не назван ЗАМЕНИТЕЛЬ — читателю не ясно, чем пользоваться вместо: " +
                string.Join("; ", vague.ToArray()));
        }

        // ---------------------------------------------------------------- 3. НЕ В СЦЕНАХ

        /// <summary>
        /// ПОМЕЧЕННЫЕ ФАЙЛЫ НЕ ПРИВЯЗАНЫ К СЦЕНАМ. Это самая важная проверка файла: пометить
        /// `[Obsolete]` скрипт, который ВИСИТ НА ОБЪЕКТЕ СЦЕНЫ, — значит пометить живой код. Тогда
        /// «мёртвый» по описанию скрипт на самом деле работает, и разбор идёт по ложному следу.
        ///
        /// Проверяется по `guid` из `.meta`: если ни в одной сцене/префабе его нет — скрипт не висит.
        /// </summary>
        [Test]
        public void DeadFiles_AreNotAttachedToAnyScene()
        {
            var guidByName = new Dictionary<string, string>();
            foreach (string name in DeadFiles)
            {
                string path = Find(name);
                if (path == null) continue;
                string meta = path + ".meta";
                if (!File.Exists(meta)) continue;
                Match g = Regex.Match(File.ReadAllText(meta), @"guid: (\w+)");
                if (g.Success) guidByName[name] = g.Groups[1].Value;
            }

            var attached = new List<string>();
            string assets = Application.dataPath;
            foreach (string file in Directory.GetFiles(assets, "*.*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".unity") && !file.EndsWith(".prefab")) continue;
                string text;
                try { text = File.ReadAllText(file); }
                catch { continue; }

                foreach (var pair in guidByName)
                {
                    if (text.Contains(pair.Value))
                        attached.Add(Path.GetFileName(pair.Key) + " в " + Path.GetFileName(file));
                }
            }

            Assert.AreEqual(0, attached.Count,
                "эти «мёртвые» скрипты НА САМОМ ДЕЛЕ висят в сцене — метка [Obsolete] поставлена " +
                "живому коду, это опаснее мёртвого: " + string.Join("; ", attached.ToArray()));
        }
    }
}
