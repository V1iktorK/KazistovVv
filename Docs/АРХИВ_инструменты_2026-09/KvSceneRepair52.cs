#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// §52. ВОССТАНОВЛЕНИЕ MainScene ПОСЛЕ АВАРИИ 22.09.2026 — ШТАТНЫМИ СРЕДСТВАМИ UNITY.
///
/// ЧТО СЛУЧИЛОСЬ (доказано файлами и логами, см. §52 в PROJECT_CONTEXT.md).
/// 1) §51 удалил из сцены дефолтные столы и перевёл роботов в корень, но сделал это
///    ТЕКСТОВЫМ инструментом (`_tools\kv51_scene_strip.ps1`), и тот испортил формат файла:
///    потерял две первые строки заголовка YAML (`%YAML 1.1`, `%TAG !u! …`) и у всех
///    46 «stripped»-документов убрал пробел перед `stripped` (`&1076390680stripped`).
///    Unity такой файл не читает: в `Logs\Editor.log` (и 20 раз в логах клона) стоит
///    `Failed to load '…MainScene.unity'. File may be corrupted`.
/// 2) Восстановив бэкап и удалив столы вручную, оператор получил сцену БЕЗ роботов:
///    в бэкапе роботы были ДЕТЬМИ столов (инцидент §39.1), а на момент правки компиляция
///    была сломана (CS0246), поэтому префаб-инстансы роботов не разрешились.
///
/// ПОЧЕМУ ПРАВКА ИДЁТ ИЗ РЕДАКТОРА, А НЕ ПРАВКОЙ YAML. Ровно потому, что текстовый
/// инструмент уже один раз испортил этот файл. Здесь всё делает САМА Unity: она загружает
/// сцену, отвязывает роботов, удаляет два стола и сериализует результат — артефакт формата
/// невозможен ПО ПОСТРОЕНИЮ. До и после правки проверяются пред- и постусловия; при любом
/// расхождении сцена НЕ сохраняется (файл остаётся валидной версией §51-бэкапа).
///
/// ЧТО ИМЕННО ДЕЛАЕТ `Rebuild`:
///   1) копирует `MainScene.unity.s51_bak` (версия §51 ДО текстовой правки: столы + роботы,
///      фикс §50 сохранён) поверх `Assets/_Project/00_Scenes/MainScene.unity` (байтовая копия,
///      guid сцены не меняется — `.meta` не трогается);
///   2) открывает сцену и проверяет предпосылки: роботов 2, столов 2;
///   3) ОТВЯЗЫВАЕТ роботов (`SetParent(null, worldPositionStays: true)`) — мировые координаты
///      сохраняются: (0; 0,98; −24) и (0; 0,98; −28);
///   4) удаляет ТОЛЬКО объекты `Стенд_1_Стол` и `Стенд_2_Стол` — предварительно убедившись,
///      что в их поддеревьях НЕ осталось ни одного `RobotController` (защита от инцидента §39.1);
///   5) проверяет постусловия (роботов 2 и они корневые, столов 0, позиции на месте,
///      компонентов с потерянным скриптом 0), и только тогда сохраняет сцену;
///   6) перечитывает файл с диска, проверяет КАНОНИЧНОСТЬ формата (`%YAML 1.1`, `&ID stripped`)
///      и печатает отчёт (в лог и в `_s52_runs\main_scene_repair.txt`).
///
/// ЗАПУСК: меню `Tools → KazistovVv → Сцена (§52) → …` (с подтверждением) либо из батча
/// `-executeMethod KvSceneRepair52.BatchRebuild52` / `KvSceneRepair52.BatchProbe52`.
/// САМО НИЧЕГО НЕ ЗАПУСКАЕТСЯ: ни `[InitializeOnLoad]`, ни автосохранение сцены здесь нет.
/// </summary>
public static class KvSceneRepair52
{
    public const string SceneAssetPath = "Assets/_Project/00_Scenes/MainScene.unity";

    /// <summary>
    /// Бэкап сцены §51, снятый ПЕРЕД текстовой правкой (единственная копия со столами и роботами).
    /// Лежит ВНЕ проекта — в каталоге откатов, чтобы точечные копии не попадали в репозиторий.
    /// </summary>
    public const string S51BackupPath =
        @"C:\Users\Ольга\_kv_rollback_2026-09-24\bak_copies\MainScene.unity.s51_bak";

    public const string ReportPath = @"C:\Users\Ольга\_s52_runs\main_scene_repair.txt";

    private const string MenuRoot = "Tools/KazistovVv/Сцена (§52)/";
    private const string LogTag = "[§52 SceneRepair]";

    private const string Robot6Name = "Робот_6ос_Стенд1";
    private const string RobotScaraName = "SCARA_Стенд2";
    private const string Stand1Name = "Стенд_1_Стол";
    private const string Stand2Name = "Стенд_2_Стол";

    private const float PosTolerance = 1e-3f;

    private static readonly Vector3 Robot6Pos = new Vector3(0f, 0.98f, -24f);
    private static readonly Vector3 RobotScaraPos = new Vector3(0f, 0.98f, -28f);

    // ================================================================== меню

    /// <summary>Диагностика: что реально лежит в сцене. НИЧЕГО не меняет и не сохраняет.</summary>
    [MenuItem(MenuRoot + "Отчёт по MainScene (что в сцене сейчас)", false, 10)]
    public static void MenuProbe()
    {
        OpenSceneIfNothingLoaded();
        string report = Inventory("ОТЧЁТ (только чтение)");
        Debug.Log(LogTag + "\n" + report);
        WriteReport(report);
    }

    /// <summary>Восстановление сцены — с подтверждением оператора (в батче подтверждения нет).</summary>
    [MenuItem(MenuRoot + "Восстановить сцену из §51-бэкапа (роботы в корень, столы удалить)", false, 11)]
    public static void MenuRebuild()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("KazistovVv · §52",
                "Правку сцены нельзя делать в PlayMode: сначала остановите игру.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("KazistovVv · §52 · восстановление сцены",
                "Будет сделано:\n" +
                "  • сцена заменена версией из §51-бэкапа («" + Path.GetFileName(S51BackupPath) + "»);\n" +
                "  • оба робота отвязаны от столов (мировые координаты сохраняются);\n" +
                "  • удалены ТОЛЬКО два стола — «" + Stand1Name + "» и «" + Stand2Name + "»;\n" +
                "  • сцена сохранена средствами Unity.\n\n" +
                "Стол вернётся командой: Tools → KazistovVv → Стенды → Поставить стенды.",
                "Восстановить", "Отмена"))
            return;

        Rebuild();
    }

    // ================================================================== батч-точки входа

    /// <summary>Точка входа для `-executeMethod` (диалогов нет). Сцену открывает сама.</summary>
    public static void BatchProbe52()
    {
        FailIf(!File.Exists(SceneAssetPath), "нет файла сцены: " + SceneAssetPath);
        OpenSceneIfNothingLoaded();
        string report = Inventory("БАТЧ-ПРОБА ЗАГРУЗКИ (только чтение)");
        Debug.Log(LogTag + "\n" + report);
        WriteReport(report);
    }

    /// <summary>Точка входа для `-executeMethod`: восстановление сцены. Ошибка → исключение → ненулевой код выхода.</summary>
    public static void BatchRebuild52()
    {
        Rebuild();
    }

    /// <summary>
    /// В пакетном режиме Unity стартует с ПУСТОЙ безымянной сценой (последняя сцена не открывается),
    /// поэтому для пробы её надо открыть из файла явно. В живом редакторе открытая сцена не трогается
    /// (кроме случая пустой безымянной — тогда тоже открываем MainScene).
    /// </summary>
    private static void OpenSceneIfNothingLoaded()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(active.path)) return;
        if (!File.Exists(SceneAssetPath)) return;
        Scene opened = EditorSceneManager.OpenScene(SceneAssetPath, OpenSceneMode.Single);
        Debug.Log(LogTag + " сцена открыта из файла: path=«" + opened.path + "» корней=" + opened.rootCount +
                  " (в батче Unity сама сцену не открывает)");
    }

    // ================================================================== восстановление

    public static void Rebuild()
    {
        var log = new StringBuilder();
        string header = "§52 — восстановление MainScene: " + DateTime.Now.ToString("s") + "\n" +
                        "проект: " + Directory.GetParent(Application.dataPath).FullName + "\n" +
                        "бэкап §51: " + S51BackupPath + "\n" +
                        "PlayMode: " + Application.isPlaying + ", batch: " + Application.isBatchMode + "\n";
        log.Append(header);
        Debug.Log(LogTag + " СТАРТ восстановления сцены");

        try
        {
            FailIf(Application.isPlaying, "правка сцены запрещена в PlayMode");
            FailIf(!File.Exists(S51BackupPath), "не найден бэкап сцены §51: " + S51BackupPath);
            FailIf(!File.Exists(SceneAssetPath), "не найден файл сцены: " + SceneAssetPath);

            Trace(log, "ФАЙЛ СЦЕНЫ ДО: sha256=" + Sha256(SceneAssetPath) +
                       ", байт=" + new FileInfo(SceneAssetPath).Length);

            // 1. Уходим с текущей сцены: чтобы Unity не держала в памяти старую версию файла.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Байтовая копия бэкапа поверх сцены проекта. `.meta` и guid сцены не меняются.
            File.Copy(S51BackupPath, SceneAssetPath, overwrite: true);
            AssetDatabase.ImportAsset(SceneAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            string shaCopied = Sha256(SceneAssetPath);
            Trace(log, "после копии бэкапа: sha256=" + shaCopied +
                       ", байт=" + new FileInfo(SceneAssetPath).Length);
            FailIf(shaCopied != Sha256(S51BackupPath), "скопированный файл не совпал с бэкапом (sha256)");

            // 3. Открываем сцену и проверяем ПРЕДпосылки.
            Scene scene = EditorSceneManager.OpenScene(SceneAssetPath, OpenSceneMode.Single);
            FailIf(!scene.IsValid() || !scene.isLoaded, "сцена не открылась: " + SceneAssetPath);
            FailIf(scene.rootCount == 0, "сцена открылась пустой (файл не читается Unity?)");

            List<GameObject> robots = FindByName(Robot6Name);
            robots.AddRange(FindByName(RobotScaraName));
            List<GameObject> stands = new List<GameObject>();
            stands.AddRange(FindByName(Stand1Name));
            stands.AddRange(FindByName(Stand2Name));

            Trace(log, "ПРЕДпосылки: роботов найдено " + robots.Count + ", столов найдено " + stands.Count +
                       ", корней сцены " + scene.rootCount);
            foreach (GameObject r in robots)
                Trace(log, "  робот «" + r.name + "»: родитель " +
                           (r.transform.parent != null ? "«" + r.transform.parent.name + "»" : "<корень>") +
                           ", локальная " + Fmt(r.transform.localPosition) +
                           ", мировая " + Fmt(r.transform.position));
            FailIf(robots.Count != 2, "в бэкапе должно быть 2 робота, найдено " + robots.Count);
            FailIf(stands.Count != 2, "в бэкапе должно быть 2 стола, найдено " + stands.Count);

            // 4. Отвязываем роботов — мировые координаты сохраняются.
            foreach (GameObject robot in robots)
            {
                Vector3 worldBefore = robot.transform.position;
                string parentBefore = robot.transform.parent != null ? robot.transform.parent.name : "<корень>";
                robot.transform.SetParent(null, worldPositionStays: true);

                Vector3 worldAfter = robot.transform.position;
                Trace(log, "  ОТВЯЗАН «" + robot.name + "»: родитель " + parentBefore + " → корень; " +
                           "мировая " + Fmt(worldBefore) + " → " + Fmt(worldAfter));
                FailIf(robot.transform.parent != null, "робот «" + robot.name + "» остался внутри родителя");
                FailIf((worldBefore - worldAfter).magnitude > 1e-4f,
                    "у робота «" + robot.name + "» сместились мировые координаты: " +
                    Fmt(worldBefore) + " → " + Fmt(worldAfter));
            }

            // 5. Удаляем ТОЛЬКО столы — и только убедившись, что роботов внутри них НЕ осталось.
            //    ВНИМАНИЕ: строку сообщения нельзя собирать через `inside.name` до проверки на null —
            //    при пустом столе это давало NullReferenceException (поймано прогоном 22.09.2026).
            foreach (GameObject stand in stands)
            {
                RobotController inside = stand.GetComponentInChildren<RobotController>(true);
                string insideName = inside != null ? inside.name : "<нет>";
                string standName = stand.name;
                int standChildren = stand.transform.childCount;
                Trace(log, "  стол «" + standName + "»: детей " + standChildren +
                           ", роботов внутри " + (inside != null ? 1 : 0));
                FailIf(inside != null,
                    "в столе «" + standName + "» остался робот «" + insideName + "» — удаление унесло бы его " +
                    "(инцидент §39.1); правка остановлена, сцена НЕ сохранена");

                // Имя берём ДО удаления: после DestroyImmediate объект уже уничтожен,
                // и обращение к stand.name бросает «has been destroyed but you are still trying to access it»
                // (поймано прогоном 22.09.2026 06:49).
                if (Application.isBatchMode) UnityEngine.Object.DestroyImmediate(stand);
                else Undo.DestroyObjectImmediate(stand);
                Trace(log, "  УДАЛЁН стол «" + standName + "»");
            }

            // 6. ПОСТусловия — проверяются ДО сохранения.
            EditorSceneManager.MarkSceneDirty(scene);
            RobotController[] robotsNow = UnityEngine.Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            int standsNow = FindByName(Stand1Name).Count + FindByName(Stand2Name).Count;
            int broken = CountBrokenComponents();

            Trace(log, "ПОСТусловия: роботов в сцене " + robotsNow.Length + ", столов " + standsNow +
                       ", компонентов с потерянным скриптом " + broken);

            FailIf(robotsNow.Length != 2, "роботов в сцене " + robotsNow.Length + ", ожидалось 2");
            FailIf(standsNow != 0, "столов в сцене " + standsNow + ", ожидалось 0");
            FailIf(broken != 0, "компонентов с потерянным скриптом " + broken + ", ожидалось 0");
            foreach (RobotController r in robotsNow)
                FailIf(r.transform.parent != null, "робот «" + r.name + "» не корневой после правки");

            AssertPosition(log, "робот 6-осевой", Robot6Name, Robot6Pos);
            AssertPosition(log, "SCARA", RobotScaraName, RobotScaraPos);

            // 7. Сохранение — штатным сериализатором Unity (именно этого не хватало §51).
            FailIf(!EditorSceneManager.SaveScene(scene), "EditorSceneManager.SaveScene вернул false");
            AssetDatabase.Refresh();

            string shaAfter = Sha256(SceneAssetPath);
            Trace(log, "ФАЙЛ СЦЕНЫ ПОСЛЕ: sha256=" + shaAfter +
                       ", байт=" + new FileInfo(SceneAssetPath).Length);
            FailIf(shaAfter == shaCopied, "файл сцены не изменился — сохранение не сработало");

            // 8. Формат файла: то, что §51 потерял. Проверяем ПОСЛЕ сохранения, по файлу на диске.
            CheckCanonicalFormat(log);

            // 9. Перечитываем файл с диска заново — доказательство, что Unity его читает.
            Scene reopened = EditorSceneManager.OpenScene(SceneAssetPath, OpenSceneMode.Single);
            FailIf(!reopened.IsValid() || reopened.rootCount == 0,
                "сцена после сохранения не читается Unity");
            log.AppendLine();
            log.Append(Inventory("ПОСТ-ПРОВЕРКА (файл прочитан заново с диска)"));

            Debug.Log(LogTag + " ВОССТАНОВЛЕНИЕ ЗАВЕРШЕНО\n" + log);
            WriteReport(log.ToString());
        }
        catch (Exception e)
        {
            log.AppendLine();
            log.AppendLine("ОШИБКА: " + e.GetType().Name + ": " + e.Message);
            log.AppendLine(e.StackTrace);
            log.AppendLine("СЦЕНА НЕ СОХРАНЕНА (файл остался версией §51-бэкапа).");
            WriteReport(log.ToString());
            Debug.LogError(LogTag + " восстановление остановлено: " + e.Message);
            throw;
        }
    }

    // ================================================================== проверки

    /// <summary>
    /// Каноничность формата файла сцены. Ровно то, что испортил текстовый инструмент §51:
    /// отсутствие заголовка YAML и пробела перед `stripped`.
    /// </summary>
    private static void CheckCanonicalFormat(StringBuilder log)
    {
        string[] lines = File.ReadAllLines(SceneAssetPath);
        FailIf(lines.Length < 3, "файл сцены подозрительно короткий: " + lines.Length + " строк");
        FailIf(!lines[0].StartsWith("%YAML 1.1", StringComparison.Ordinal),
            "первая строка файла сцены — «" + lines[0] + "», ожидалось «%YAML 1.1»");
        FailIf(!lines[1].StartsWith("%TAG !u!", StringComparison.Ordinal),
            "вторая строка файла сцены — «" + lines[1] + "», ожидалось «%TAG !u! …»");

        int bad = 0;
        int stripped = 0;
        foreach (string line in lines)
        {
            if (!line.StartsWith("--- !u!", StringComparison.Ordinal)) continue;
            if (line.IndexOf("stripped", StringComparison.Ordinal) < 0) continue;
            stripped++;
            if (!line.EndsWith(" stripped", StringComparison.Ordinal)) bad++;
        }
        Trace(log, "ФОРМАТ: заголовок YAML на месте; stripped-документов " + stripped +
                   ", из них без пробела перед «stripped» — " + bad);
        FailIf(bad != 0, "в файле сцены " + bad + " заголовков вида «&IDstripped» без пробела " +
                         "(артефакт §51) — Unity такой файл не читает");
    }

    /// <summary>Факты о сцене — то, что видит Unity, а не то, что написано в тексте файла.</summary>
    private static string Inventory(string title)
    {
        var sb = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        sb.AppendLine(title);
        sb.AppendLine("  активная сцена: name=«" + scene.name + "» path=«" + scene.path + "» " +
                      "isLoaded=" + scene.isLoaded + " isValid=" + scene.IsValid() +
                      " isDirty=" + scene.isDirty + " корней=" + (scene.IsValid() ? scene.rootCount : -1));

        var roots = new List<string>();
        if (scene.IsValid())
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go != null) roots.Add(go.name);
        sb.AppendLine("  корневые объекты (" + roots.Count + "): " + string.Join(", ", roots.ToArray()));

        RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
        sb.AppendLine("  роботов (RobotController): " + robots.Length);
        foreach (RobotController r in robots)
        {
            if (r == null) continue;
            sb.AppendLine("    · «" + r.name + "» [" + r.GetType().Name + "] родитель=" +
                          (r.transform.parent != null ? r.transform.parent.name : "<корень>") +
                          " мировая=" + Fmt(r.transform.position));
        }

        List<GameObject> stands = FindByName(Stand1Name);
        stands.AddRange(FindByName(Stand2Name));
        sb.AppendLine("  столов (Стенд_*): " + stands.Count);
        sb.AppendLine("  компонентов с потерянным скриптом: " + CountBrokenComponents());
        sb.AppendLine("  Camera.main: " + (Camera.main != null ? "«" + Camera.main.name + "»" : "НЕТ"));
        sb.AppendLine("  Canvas: " + UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length);
        sb.AppendLine("  рендереров: " + UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Length);
        return sb.ToString();
    }

    /// <summary>Все объекты сцены с компонентами, которые не разрешились (оба критерия §50).</summary>
    private static int CountBrokenComponents()
    {
        int total = 0;
        GameObject[] all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject go in all)
        {
            if (go == null) continue;
            total += KvMissingScriptRepair.CountBrokenComponents(go);
        }
        return total;
    }

    private static void AssertPosition(StringBuilder log, string label, string name, Vector3 expected)
    {
        List<GameObject> found = FindByName(name);
        FailIf(found.Count != 1, label + ": объектов с именем «" + name + "» — " + found.Count + ", ожидался 1");
        Vector3 actual = found[0].transform.position;
        Trace(log, "  " + label + " («" + name + "»): " + Fmt(actual) + " (ожидалось " + Fmt(expected) + ")");
        FailIf((actual - expected).magnitude > PosTolerance,
            label + " («" + name + "») стоит в " + Fmt(actual) + ", ожидалось " + Fmt(expected));
    }

    private static List<GameObject> FindByName(string name)
    {
        var result = new List<GameObject>();
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null) continue;
            if (string.Equals(t.name, name, StringComparison.Ordinal)) result.Add(t.gameObject);
        }
        return result;
    }

    // ================================================================== служебное

    /// <summary>Строка и в отчёт, и СРАЗУ в лог: иначе при падении батча не видно, где остановились.</summary>
    private static void Trace(StringBuilder log, string line)
    {
        log.AppendLine(line);
        Debug.Log(LogTag + " " + line);
    }

    private static void FailIf(bool condition, string message)
    {
        if (condition) throw new InvalidOperationException(LogTag + " " + message);
    }

    private static string Fmt(Vector3 v)
    {
        return "(" + v.x.ToString("0.####") + "; " + v.y.ToString("0.####") + "; " + v.z.ToString("0.####") + ")";
    }

    private static string Sha256(string path)
    {
        using (FileStream fs = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static void WriteReport(string text)
    {
        try
        {
            string dir = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(ReportPath, text, new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning(LogTag + " не удалось записать отчёт в " + ReportPath + ": " + e.Message);
        }
    }
}
#endif
