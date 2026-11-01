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
/// §56. СЦЕНА: ПОЧЕМУ РОБОТЫ «ВИСЯТ В ВОЗДУХЕ» И ЧТО БУДЕТ, ЕСЛИ УБРАТЬ ИХ СОВСЕМ.
///
/// ЭТОТ ФАЙЛ — ТОЛЬКО ЧТЕНИЕ (кроме отдельного метода правки, который добавляется по решению
/// оператора). Здесь нет ни `[InitializeOnLoad]`, ни автосохранения: меню и батч-точка
/// считают и печатают отчёт, сцену НЕ меняют.
///
/// ЧТО СЧИТАЕТСЯ (ответы на вопросы ТЗ §56 и на вопрос «убрать роботов совсем»):
///   1) ОПОРА И ПИВОТ каждого робота: мировая позиция (origin), габарит рендереров
///      (низ/центр/верх), где пивот относительно низа модели, есть ли ПОД роботом геометрия
///      (луч вниз, свои коллайдеры исключены) и на какой высоте её верх;
///   2) ПОЛ по правилу самого проекта (`CollisionWorld`: объект «низкий и широкий»
///      — `size.y &lt; 1 &amp;&amp; size.x &gt; 8 &amp;&amp; size.z &gt; 8` — учитывается как плоскость пола);
///   3) ССЫЛКИ НА РОБОТОВ в сцене: кто (объект → компонент → свойство) держит ссылку на робота
///      или его потомка. Это и есть точный список того, что станет `null`, если роботов удалить;
///   4) КАТАЛОГ: у префабов-роботов каталога (`Assets/Resources/Models/Robots`) есть ли
///      `RobotController` — то есть можно ли поставить РАБОЧЕГО робота из каталога/мастера;
///   5) СОСТАВ СЦЕНЫ: корни, число `RobotController`, компоненты с потерянным скриптом.
///
/// ЗАПУСК: меню `Tools → KazistovVv → Сцена (§56) → Отчёт (только чтение)`
/// либо из батча `-executeMethod KvRobotsScene56.BatchDiag56`.
/// </summary>
public static class KvRobotsScene56
{
    public const string SceneAssetPath = "Assets/_Project/00_Scenes/MainScene.unity";
    public const string ReportPath = @"C:\Users\Ольга\_s56_diag.txt";

    private const string MenuRoot = "Tools/KazistovVv/Сцена (§56)/";
    private const string LogTag = "[§56 Scene]";

    private const string CatalogRobotsFolder = "Assets/Resources/Models/Robots";

    // ================================================================== меню

    /// <summary>Диагностика: что лежит в сцене и что держит роботов. НИЧЕГО не меняет и не сохраняет.</summary>
    [MenuItem(MenuRoot + "Отчёт (только чтение)", false, 10)]
    public static void MenuDiag()
    {
        EnsureSceneLoaded();
        string report = Diag("ОТЧЁТ §56 (только чтение)");
        Debug.Log(LogTag + "\n" + report);
        WriteReport(report);
    }

    /// <summary>§56: сохранить обоих роботов как РАБОЧИЕ префабы (звенья, IK, контроллер).</summary>
    [MenuItem(MenuRoot + "1. Сделать префабы рабочих роботов", false, 20)]
    public static void MenuMakePrefabs()
    {
        if (!EditorUtility.DisplayDialog("KazistovVv · §56 · префабы роботов",
                "Будут созданы (или перезаписаны) два префаба:\n" +
                "  • " + PrefabFolder + "/" + Robot6PrefabFile + ".prefab\n" +
                "  • " + PrefabFolder + "/" + ScaraPrefabFile + ".prefab\n\n" +
                "Префаб берётся с ТЕКУЩИХ роботов сцены вместе со звеньями, IK и контроллером —\n" +
                "это и есть «рабочий» робот (в каталоге такие ВИЗУАЛЬНЫЕ, без кинематики).\n" +
                "Сцена НЕ меняется и НЕ сохраняется.",
                "Сделать префабы", "Отмена"))
            return;
        Report("ПРЕФАБЫ §56 (по команде оператора)", MakePrefabs());
    }

    /// <summary>§56: убрать роботов из сцены (сцена без роботов и столов при запуске).</summary>
    [MenuItem(MenuRoot + "2. Убрать роботов из сцены", false, 21)]
    public static void MenuRemoveRobots()
    {
        if (!EditorUtility.DisplayDialog("KazistovVv · §56 · убрать роботов",
                "Оба робота будут УДАЛЕНЫ из сцены, сцена сохранена средствами Unity.\n\n" +
                "Перед этим обязательно должны существовать рабочие префабы\n" +
                "(пункт 1 меню). Вернуть роботов: пункт 3 (ставит стенды и роботов на столешницы).\n\n" +
                "Сцена сохраняется — это осознанное действие.",
                "Убрать и сохранить", "Отмена"))
            return;
        Report("УДАЛЕНИЕ РОБОТОВ §56 (по команде оператора)", RemoveRobots(true));
    }

    /// <summary>§56: поставить стенды и рабочих роботов (обратная команда).</summary>
    [MenuItem(MenuRoot + "3. Поставить стенды и роботов", false, 22)]
    public static void MenuPlaceStandsAndRobots()
    {
        if (!EditorUtility.DisplayDialog("KazistovVv · §56 · поставить стенды и роботов",
                "Будут поставлены два стенда (штатная команда проекта) и ДВА РАБОЧИХ робота\n" +
                "из префабов §56 — на столешницы (верх 0,98 м), у внутренней кромки:\n" +
                "«Робот_6ос_Стенд1» ≈ (0; 0,98; −23,9) и «SCARA_Стенд2» ≈ (0; 0,98; −28,1)\n" +
                "(паспортные координаты сцены §50–§55 — (0; 0,98; −24) и (0; 0,98; −28)).\n\n" +
                "Сцена сохраняется.",
                "Поставить и сохранить", "Отмена"))
            return;
        Report("СТЕНДЫ И РОБОТЫ §56 (по команде оператора)", PlaceStandsAndRobots(true));
    }

    // ================================================================== батч-точки входа

    /// <summary>Точка входа для `-executeMethod` (диалогов нет). Сцену открывает сама.</summary>
    public static void BatchDiag56()
    {
        if (!File.Exists(SceneAssetPath))
            throw new Exception(LogTag + " нет файла сцены: " + SceneAssetPath);

        string report = Diag("БАТЧ-ДИАГНОСТИКА §56 (только чтение)");
        Debug.Log(LogTag + "\n" + report);
        WriteReport(report);
    }

    /// <summary>§56: префабы рабочих роботов (сцена не меняется, не сохраняется).</summary>
    public static void BatchMakeRobotPrefabs56()
    {
        Report("ПРЕФАБЫ §56 (батч)", MakePrefabs());
    }

    /// <summary>§56: убрать роботов из сцены и СОХРАНИТЬ сцену.</summary>
    public static void BatchRemoveRobots56()
    {
        Report("УДАЛЕНИЕ РОБОТОВ §56 (батч)", RemoveRobots(true));
    }

    /// <summary>§56: поставить стенды и рабочих роботов и СОХРАНИТЬ сцену.</summary>
    public static void BatchPlaceStandsAndRobots56()
    {
        Report("СТЕНДЫ И РОБОТЫ §56 (батч)", PlaceStandsAndRobots(true));
    }

    /// <summary>§56: проба без сохранения — можно ли поставить роботов (сцена на диске не меняется).</summary>
    public static void BatchProbePlaceRobots56()
    {
        Report("ПРОБА ПОСТАНОВКИ §56 (без сохранения)", PlaceStandsAndRobots(false));
    }

    // ================================================================== отчёт

    private static string Diag(string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine("§56 ДИАГНОСТИКА СЦЕНЫ (read-only): опора, пивот, ссылки на роботов, каталог");
        sb.AppendLine(title);
        sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Проект: " + ProjectRoot);
        sb.AppendLine();

        string full = FullPath(SceneAssetPath);
        sb.AppendLine("MainScene.unity ДО открытия: " + FileHash(full) + " · байт " +
                      (File.Exists(full) ? new FileInfo(full).Length.ToString() : "—"));
        sb.AppendLine();

        Scene scene = EnsureSceneLoaded();

        // ---------- 1. роботы: origin, пивот, опора ----------
        sb.AppendLine("=== РОБОТЫ СЦЕНЫ: ORIGIN, ПИВОТ, ОПОРА ===");
        RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(
            FindObjectsInactive.Include);
        int shown = 0;
        foreach (RobotController rc in robots)
        {
            if (rc == null) continue;
            if ((rc.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
            shown++;
            GameObject go = rc.gameObject;
            Transform t = go.transform;

            sb.AppendLine("• «" + go.name + "» тип=" + rc.GetType().Name +
                          " robotName=«" + rc.robotName + "» activeSelf=" + go.activeSelf +
                          " parent=" + (t.parent == null ? "(корень сцены)" : "«" + t.parent.name + "»"));
            sb.AppendLine("    origin (transform.position): " + V(t.position));
            sb.AppendLine("    localScale " + V(t.localScale) + " · lossyScale " + V(t.lossyScale));

            Bounds b = BoundsOf(go);
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            sb.AppendLine("    габарит рендереров: " + N(b.size.x) + " × " + N(b.size.y) + " × " +
                          N(b.size.z) + " · низ y = " + N(b.min.y) + " · центр " + V(b.center) +
                          " · верх y = " + N(b.max.y) + " · рендереров " + rs.Length);

            // Пивот: где центр габарита и низ модели относительно origin.
            Vector3 centerLocal = t.InverseTransformPoint(b.center);
            sb.AppendLine("    пивот: центр габарита в локальных координатах " + V(centerLocal) +
                          " · низ модели относительно origin: dy = " + N(b.min.y - t.position.y) +
                          (Mathf.Abs(b.min.y - t.position.y) <= 0.02f
                              ? "  → ПИВОТ В ОСНОВАНИИ (origin совпадает с низом модели)"
                              : "  → ПИВОТ НЕ В ОСНОВАНИИ"));

            // Опора: луч вниз, свои коллайдеры исключены.
            float supportY;
            string supportWhat;
            FindSupport(go, out supportY, out supportWhat);
            sb.AppendLine("    ОПОРА ПОД РОБОТОМ (луч вниз из origin + 0,5 м, свои коллайдеры исключены): " +
                          supportWhat);
            if (!float.IsNaN(supportY))
            {
                float gap = b.min.y - supportY;
                sb.AppendLine("      верх опоры y = " + N(supportY) + " · низ модели y = " +
                              N(b.min.y) + " · зазор " + N(gap) + " м " +
                              (Mathf.Abs(gap) <= 0.02f ? "→ СТОИТ НА ОПОРЕ"
                                  : (gap > 0 ? "→ ВИСИТ В ВОЗДУХЕ на " + N(gap) + " м"
                                             : "→ УТОПЛЕН в опору на " + N(-gap) + " м")));
            }
            sb.AppendLine();
        }
        sb.AppendLine("роботов (RobotController) в сцене: " + shown + " из " + robots.Length +
                      " найденных (скрытые из иерархии не считаются)");
        sb.AppendLine();

        // ---------- 2. пол по правилу проекта ----------
        sb.AppendLine("=== «ПОЛ» ПО ПРАВИЛУ CollisionWorld (низкий и широкий: y<1, x>8, z>8) ===");
        int floorFound = 0;
        foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (r == null) continue;
            if (r.GetComponentInParent<RobotController>() != null) continue;
            Bounds b = r.bounds;
            Vector3 s = b.size;
            if (s.y < 1f && s.x > 8f && s.z > 8f)
            {
                floorFound++;
                sb.AppendLine("• «" + NameOf(r.transform) + "» размер " + N(s.x) + " × " + N(s.y) +
                              " × " + N(s.z) + " · верх y = " + N(b.max.y) + " (FloorY проекта)");
            }
        }
        if (floorFound == 0)
            sb.AppendLine("объектов-«пол» по правилу проекта НЕТ (значит FloorY в CollisionWorld " +
                          "остаётся −∞ и зазор до пола не считается)");
        sb.AppendLine();

        // ---------- 3. кто в сцене держит ссылки на роботов ----------
        sb.AppendLine("=== ССЫЛКИ НА РОБОТОВ В СЦЕНЕ (что станет null, если роботов удалить) ===");
        var refs = new List<string>();
        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include))
        {
            if (go == null) continue;
            bool insideRobot = go.GetComponentInParent<RobotController>() != null;
            if (insideRobot) continue;                       // ссылки ВНУТРИ самого робота не интересны
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                SerializedObject so;
                try { so = new SerializedObject(c); }
                catch { continue; }
                SerializedProperty it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    UnityEngine.Object val = it.objectReferenceValue;
                    if (val == null) continue;
                    RobotController owner = OwnerRobot(val);
                    if (owner == null) continue;
                    refs.Add("• «" + PathOf(go.transform) + "» → " + c.GetType().Name + "." +
                             it.propertyPath + " → " + (owner == null ? "?" : "«" + owner.name + "»"));
                }
            }
        }
        if (refs.Count == 0) sb.AppendLine("внешних ссылок на роботов в сцене НЕТ");
        foreach (string s in refs) sb.AppendLine(s);
        sb.AppendLine();

        // ---------- 4. каталог: есть ли «рабочий» робот среди префабов ----------
        sb.AppendLine("=== КАТАЛОГ: ПРЕФАБЫ-РОБОТЫ И НАЛИЧИЕ КИНЕМАТИКИ (RobotController) ===");
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { CatalogRobotsFolder });
        var rows = new List<string>();
        int withKinematics = 0;
        foreach (string g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (prefab == null) continue;
            RobotController rc = prefab.GetComponentInChildren<RobotController>(true);
            if (rc != null) withKinematics++;
            rows.Add("• " + Path.GetFileName(p) + " → RobotController: " +
                     (rc != null ? "ЕСТЬ (" + rc.GetType().Name + ")" : "НЕТ (визуальная модель)"));
        }
        rows.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string s in rows) sb.AppendLine(s);
        sb.AppendLine("префабов-роботов: " + rows.Count + " · с кинематикой: " + withKinematics);
        sb.AppendLine();

        // ---------- 5. состав сцены ----------
        sb.AppendLine("=== СОСТАВ СЦЕНЫ ===");
        sb.AppendLine("корней в сцене: " + scene.rootCount);
        var roots = new List<string>();
        foreach (GameObject go in scene.GetRootGameObjects())
            roots.Add("• «" + go.name + "» " + V(go.transform.position) +
                      (go.GetComponentInChildren<RobotController>(true) != null
                          ? "  [содержит робота]" : ""));
        roots.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string s in roots) sb.AppendLine(s);

        int missing = 0;
        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include))
        {
            if (go == null) continue;
            foreach (Component c in go.GetComponents<Component>()) if (c == null) missing++;
        }
        sb.AppendLine("компонентов с потерянным скриптом (Missing): " + missing);
        sb.AppendLine();
        sb.AppendLine("MainScene.unity ПОСЛЕ отчёта: " + FileHash(full) + " · байт " +
                      (File.Exists(full) ? new FileInfo(full).Length.ToString() : "—") +
                      " (отчёт НИЧЕГО не менял — хеш обязан совпасть с хешем ДО)");
        return sb.ToString();
    }

    // ================================================================== константы правки

    /// <summary>Префабы РАБОЧИХ роботов (§56). Не путать с каталогом: там модели ВИЗУАЛЬНЫЕ.</summary>
    public const string PrefabFolder = "Assets/Resources/Robots";
    public const string Robot6PrefabFile = "robot_6axis";
    public const string ScaraPrefabFile = "scara_ls10b702s";

    public const string Robot6Name = "Робот_6ос_Стенд1";
    public const string ScaraName = "SCARA_Стенд2";

    /// <summary>Мировые координаты роботов в сцене (X; Z) — контракт §50–§55. Y — верх столешницы.</summary>
    private static readonly Vector2 Robot6XZ = new Vector2(0f, -24f);
    private static readonly Vector2 ScaraXZ = new Vector2(0f, -28f);

    private const float PosTolerance = 0.02f;

    // ================================================================== правка: префабы

    private static string MakePrefabs()
    {
        var sb = new StringBuilder();
        sb.AppendLine("§56 ПРЕФАБЫ РАБОЧИХ РОБОТОВ (сцена НЕ меняется)");
        sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();

        EnsureSceneLoaded();
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                throw new Exception(LogTag + " нет папки Assets/Resources");
            string g = AssetDatabase.CreateFolder("Assets/Resources", "Robots");
            sb.AppendLine("создана папка " + PrefabFolder + " (guid " + g + ")");
            sb.AppendLine();
        }

        var problems = new List<string>();
        var created = new List<string>();
        string[,] want = { { Robot6Name, Robot6PrefabFile }, { ScaraName, ScaraPrefabFile } };

        for (int i = 0; i < 2; i++)
        {
            string robotName = want[i, 0], file = want[i, 1];
            string path = PrefabFolder + "/" + file + ".prefab";
            RobotController rc = FindRobot(robotName);
            if (rc == null)
            {
                problems.Add("в сцене нет робота «" + robotName + "»");
                sb.AppendLine("ОШИБКА: в сцене нет робота «" + robotName + "»");
                continue;
            }

            GameObject go = rc.gameObject;
            Bounds before = BoundsOf(go);
            int renderersBefore = go.GetComponentsInChildren<Renderer>(true).Length;
            Vector3 scaleBefore = go.transform.localScale;
            int jointsBefore = MissingJoints(rc);

            sb.AppendLine("• «" + robotName + "» → " + path);
            sb.AppendLine("    ДО: тип " + rc.GetType().Name + " · рендереров " + renderersBefore +
                          " · габарит " + V(before.size) + " · корень scale " + V(scaleBefore) +
                          " · звеньев без ссылки " + jointsBefore + " · tcp " + TcpState(rc));

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            created.Add(path);

            bool ok = asset != null;
            RobotController rc2 = ok ? asset.GetComponentInChildren<RobotController>(true) : null;
            bool typeOk = rc2 != null && rc2.GetType() == rc.GetType();
            int renderersAfter = ok ? asset.GetComponentsInChildren<Renderer>(true).Length : -1;
            Bounds after = ok ? BoundsOf(asset) : new Bounds();
            bool sizeOk = ok && (after.size - before.size).magnitude <= 0.005f;
            bool scaleOk = ok && (asset.transform.localScale - scaleBefore).sqrMagnitude < 1e-10f;
            int jointsAfter = rc2 != null ? MissingJoints(rc2) : -1;
            bool jointsOk = rc2 != null && jointsAfter == jointsBefore;
            // ВАЖНО (проверено фактом в этом же прогоне): `tcp` у штатных роботов в ФАЙЛЕ не задан —
            // он привязывается В РАНТАЙМЕ (`RobotController`/`PoseValidator`: `tcp = endEffector`
            // и поиск по имени звена). Поэтому проверяется не «tcp != null», а «как в источнике»:
            // префаб обязан сохранить ровно то же состояние сериализации.
            bool tcpOk = rc2 != null && (rc2.tcp == null) == (rc.tcp == null);

            sb.AppendLine("    ПОСЛЕ: префаб " + (ok ? "сохранён" : "НЕ сохранён") +
                          " · тип " + (typeOk ? rc.GetType().Name : "НЕ СОВПАЛ") +
                          " · рендереров " + renderersAfter +
                          " · габарит " + (ok ? V(after.size) : "—") +
                          " · корень scale " + (ok ? V(asset.transform.localScale) : "—") +
                          " · звеньев без ссылки " + jointsAfter +
                          " · tcp " + (rc2 != null ? TcpState(rc2) : "—"));
            sb.AppendLine("    ПРОВЕРКИ: тип " + Yes(typeOk) + " · размер " + Yes(sizeOk) +
                          " · масштаб " + Yes(scaleOk) + " · звенья " + Yes(jointsOk) +
                          " · tcp как в источнике " + Yes(tcpOk));

            if (!(ok && typeOk && renderersAfter == renderersBefore && sizeOk && scaleOk &&
                  jointsOk && tcpOk))
                problems.Add("префаб «" + path + "» не прошёл проверки");
            sb.AppendLine();
        }

        if (problems.Count > 0)
        {
            // Не оставляем после себя неполноценный префаб: удаляем то, что создали в этом прогоне.
            foreach (string p in created) AssetDatabase.DeleteAsset(p);
            AssetDatabase.Refresh();
            sb.AppendLine("ИТОГ: ОШИБКА — префабы удалены, сцена не тронута");
            foreach (string p in problems) sb.AppendLine("  · " + p);
            WriteReport2("_s56_prefabs.txt", sb.ToString());
            Debug.LogError(LogTag + "\n" + sb);
            throw new Exception(LogTag + " префабы не созданы: " + string.Join(" | ", problems));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        sb.AppendLine("ИТОГ: префабы готовы, сцена НЕ менялась и НЕ сохранялась");
        return sb.ToString();
    }

    // ================================================================== правка: убрать роботов

    private static string RemoveRobots(bool save)
    {
        var sb = new StringBuilder();
        sb.AppendLine("§56 УБРАТЬ РОБОТОВ ИЗ СЦЕНЫ (сцена без роботов и столов при запуске)");
        sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();

        string full = FullPath(SceneAssetPath);
        string hashBefore = FileHash(full);
        sb.AppendLine("MainScene.unity ДО: " + hashBefore + " · байт " + new FileInfo(full).Length);
        sb.AppendLine();

        var problems = new List<string>();

        // 1. предпосылка: рабочие префабы уже есть (иначе роботов будет неоткуда вернуть)
        foreach (string file in new[] { Robot6PrefabFile, ScaraPrefabFile })
        {
            string p = PrefabFolder + "/" + file + ".prefab";
            if (!File.Exists(FullPath(p)))
                problems.Add("нет рабочего префаба " + p + " — сначала выполните пункт 1 (префабы)");
        }
        if (problems.Count > 0)
        {
            foreach (string p in problems) sb.AppendLine("ОШИБКА: " + p);
            WriteReport2("_s56_remove.txt", sb.ToString());
            throw new Exception(LogTag + " " + string.Join(" | ", problems));
        }

        Scene scene = EnsureSceneLoaded();

        // 2. что потеряет ссылки (печатаем ДО удаления — это и есть список последствий)
        var refs = CollectRobotRefs();
        sb.AppendLine("=== ССЫЛКИ НА РОБОТОВ, КОТОРЫЕ СТАНУТ null ===");
        if (refs.Count == 0) sb.AppendLine("внешних ссылок на роботов в сцене нет");
        foreach (string s in refs) sb.AppendLine(s);
        sb.AppendLine();

        int rootsBefore = scene.rootCount;
        int robotsBefore = CountRobots();
        sb.AppendLine("ДО: корней " + rootsBefore + " · RobotController " + robotsBefore);

        // 3. удаление — ТОЛЬКО два объекта по имени и типу контроллера
        int removed = 0;
        foreach (string name in new[] { Robot6Name, ScaraName })
        {
            RobotController rc = FindRobot(name);
            if (rc == null)
            {
                sb.AppendLine("• «" + name + "» — в сцене НЕТ (возможно, уже убран)");
                continue;
            }
            string path = PathOf(rc.transform);
            Vector3 pos = rc.transform.position;
            UnityEngine.Object.DestroyImmediate(rc.gameObject);
            removed++;
            sb.AppendLine("• удалён «" + path + "» (был " + V(pos) + ")");
        }
        sb.AppendLine();

        // 4. постусловия (при любом расхождении сцена НЕ сохраняется)
        int robotsAfter = CountRobots();
        int rootsAfter = scene.rootCount;
        sb.AppendLine("ПОСЛЕ: корней " + rootsAfter + " · RobotController " + robotsAfter);
        if (robotsAfter != 0) problems.Add("в сцене остались RobotController: " + robotsAfter);
        if (rootsAfter != rootsBefore - removed)
            problems.Add("корней " + rootsAfter + " вместо " + (rootsBefore - removed));

        foreach (string need in new[] { "Main Camera", "Directional Light", "KOMPAS_UI" })
            if (FindTransformByName(need) == null)
                problems.Add("в сцене пропал объект окружения «" + need + "»");

        int missing = CountMissingComponents();
        if (missing != 0) problems.Add("компонентов с потерянным скриптом: " + missing);
        sb.AppendLine("компонентов с потерянным скриптом: " + missing);

        if (problems.Count > 0 && save)
        {
            sb.AppendLine();
            sb.AppendLine("ИТОГ: ОШИБКА — сцена НЕ сохранена (файл на диске не тронут):");
            foreach (string p in problems) sb.AppendLine("  · " + p);
            WriteReport2("_s56_remove.txt", sb.ToString());
            Debug.LogError(LogTag + "\n" + sb);
            throw new Exception(LogTag + " постусловия не выполнены: " + string.Join(" | ", problems));
        }

        // 5. сохранение штатным сериализатором + перечитка с диска
        bool saved = false;
        if (save)
        {
            saved = EditorSceneManager.SaveScene(scene, SceneAssetPath);
            sb.AppendLine();
            sb.AppendLine("сцена сохранена: " + Yes(saved));
            string hashAfter = FileHash(full);
            sb.AppendLine("MainScene.unity ПОСЛЕ: " + hashAfter + " · байт " + new FileInfo(full).Length);
            sb.AppendLine("файл изменился: " + Yes(hashBefore != hashAfter));
            sb.AppendLine();
            sb.AppendLine(ReReadFile(full));
        }
        else
        {
            sb.AppendLine("СОХРАНЕНИЕ ПРОПУЩЕНО (проба): файл на диске остался " + hashBefore);
        }

        sb.AppendLine();
        sb.AppendLine("ИТОГ: роботы убраны (" + removed + "), проблем " + problems.Count);
        return sb.ToString();
    }

    // ================================================================== правка: поставить стенды и роботов

    private static string PlaceStandsAndRobots(bool save)
    {
        var sb = new StringBuilder();
        sb.AppendLine("§56 ПОСТАВИТЬ СТЕНДЫ И РАБОЧИХ РОБОТОВ (обратная команда)");
        sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();

        string full = FullPath(SceneAssetPath);
        string hashBefore = FileHash(full);
        sb.AppendLine("MainScene.unity ДО: " + hashBefore);
        sb.AppendLine();

        var problems = new List<string>();
        foreach (string file in new[] { Robot6PrefabFile, ScaraPrefabFile })
        {
            string p = PrefabFolder + "/" + file + ".prefab";
            if (!File.Exists(FullPath(p))) problems.Add("нет рабочего префаба " + p);
        }
        if (problems.Count > 0)
        {
            foreach (string p in problems) sb.AppendLine("ОШИБКА: " + p);
            WriteReport2("_s56_place.txt", sb.ToString());
            throw new Exception(LogTag + " " + string.Join(" | ", problems));
        }

        Scene scene = EnsureSceneLoaded();
        int robotsBefore = CountRobots();

        // Штатная команда проекта: два стенда + роботы (робот берётся из префаба §56, если
        // в сцене шаблона нет). Роботов ставит САМА команда — у внутренней кромки столешницы,
        // как и было до §51; здесь только проверяем результат и ничего не подправляем.
        TrajectoryCore.StandBuilder.EnsureStands(TrajectoryCore.StandBuilder.TopHeight);

        var want = new[] { new { Name = Robot6Name, XZ = Robot6XZ },
                           new { Name = ScaraName,  XZ = ScaraXZ } };
        foreach (var w in want)
        {
            RobotController rc = FindRobot(w.Name);
            if (rc == null)
            {
                problems.Add("команда не поставила робота «" + w.Name + "»");
                sb.AppendLine("• «" + w.Name + "» — НЕ ПОСТАВЛЕН");
                continue;
            }
            Vector3 p = rc.transform.position;
            sb.AppendLine("• «" + w.Name + "» поставлен: " + V(p) +
                          " · scale " + V(rc.transform.localScale) +
                          " · звеньев без ссылки " + MissingJoints(rc) +
                          " · tcp " + TcpState(rc));

            // «Паспортные» координаты сцены §50–§55: (0; 0,98; −24) и (0; 0,98; −28).
            // Команда ставит робота у ВНУТРЕННЕЙ кромки столешницы (−23,9 и −28,1), поэтому
            // допуск 0,5 м: важно «на своём стенде и по центру», а не сантиметры.
            if (Mathf.Abs(p.x - w.XZ.x) > 0.5f || Mathf.Abs(p.z - w.XZ.y) > 0.5f)
                problems.Add("«" + w.Name + "» поставлен в " + V(p) + " — далеко от паспортных (" +
                             N(w.XZ.x) + "; 0,98; " + N(w.XZ.y) + ")");
        }
        sb.AppendLine();

        // постусловия
        int robotsAfter = CountRobots();
        sb.AppendLine("ДО роботов: " + robotsBefore + " · ПОСЛЕ: " + robotsAfter);
        if (robotsAfter != 2) problems.Add("роботов в сцене " + robotsAfter + ", ожидалось 2");

        foreach (var w in want)
        {
            RobotController rc = FindRobot(w.Name);
            if (rc == null) continue;
            float top = SupportTopUnder(rc.gameObject);
            Bounds b = BoundsOf(rc.gameObject);
            float gap = b.min.y - top;
            sb.AppendLine("    «" + w.Name + "»: низ модели y = " + N(b.min.y) + " · верх опоры y = " +
                          (float.IsNaN(top) ? "—" : N(top)) + " · зазор " + N(gap) + " м · высота " +
                          N(b.size.y) + " м");
            if (float.IsNaN(top))
                problems.Add("под «" + w.Name + "» не найдена геометрия-опора");
            else if (Mathf.Abs(gap) > PosTolerance)
                problems.Add("«" + w.Name + "» не на опоре: зазор " + N(gap) + " м");
            if (b.size.y > 5f)
                problems.Add("«" + w.Name + "» высота " + N(b.size.y) + " м — забыт масштаб корня");
            if (MissingJoints(rc) != 0) problems.Add("«" + w.Name + "»: звено без ссылки");
        }

        GameObject stand1 = FindTransformByName(TrajectoryCore.StandBuilder.Stand1Name) != null
            ? FindTransformByName(TrajectoryCore.StandBuilder.Stand1Name).gameObject : null;
        GameObject stand2 = FindTransformByName(TrajectoryCore.StandBuilder.Stand2Name) != null
            ? FindTransformByName(TrajectoryCore.StandBuilder.Stand2Name).gameObject : null;
        foreach (var st in new[] { new { Go = stand1, N = TrajectoryCore.StandBuilder.Stand1Name },
                                   new { Go = stand2, N = TrajectoryCore.StandBuilder.Stand2Name } })
        {
            if (st.Go == null) { problems.Add("нет стенда «" + st.N + "»"); continue; }
            Bounds b = BoundsOf(st.Go);
            sb.AppendLine("    стенд «" + st.N + "»: верх столешницы y = " + N(b.max.y));
            if (Mathf.Abs(b.max.y - TrajectoryCore.StandBuilder.TopHeight) > 0.05f)
                problems.Add("стенд «" + st.N + "»: верх " + N(b.max.y) + " вместо " +
                             N(TrajectoryCore.StandBuilder.TopHeight));
        }

        if (problems.Count > 0 && save)
        {
            sb.AppendLine();
            sb.AppendLine("ИТОГ: ОШИБКА — сцена НЕ сохранена:");
            foreach (string p in problems) sb.AppendLine("  · " + p);
            WriteReport2("_s56_place.txt", sb.ToString());
            Debug.LogError(LogTag + "\n" + sb);
            throw new Exception(LogTag + " постусловия не выполнены: " + string.Join(" | ", problems));
        }

        if (save)
        {
            bool saved = EditorSceneManager.SaveScene(scene, SceneAssetPath);
            sb.AppendLine();
            sb.AppendLine("сцена сохранена: " + Yes(saved));
            sb.AppendLine("MainScene.unity ПОСЛЕ: " + FileHash(full) + " · байт " +
                          new FileInfo(full).Length);
            sb.AppendLine(ReReadFile(full));
        }
        else
        {
            sb.AppendLine("СОХРАНЕНИЕ ПРОПУЩЕНО (проба): файл на диске остался " + hashBefore);
        }

        sb.AppendLine();
        sb.AppendLine("ИТОГ: роботов в сцене " + robotsAfter + ", проблем " + problems.Count);
        return sb.ToString();
    }

    // ================================================================== вспомогательное

    /// <summary>Верх геометрии ПОД роботом: луч вниз, коллайдеры самого робота исключены.</summary>
    private static void FindSupport(GameObject robot, out float topY, out string what)
    {
        topY = float.NaN;
        what = "геометрии под роботом НЕ найдено (ни одного коллайдера)";
        Vector3 from = robot.transform.position + Vector3.up * 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 200f, ~0,
            QueryTriggerInteraction.Ignore);
        var lines = new List<string>();
        float best = float.NegativeInfinity;
        string bestWhat = null;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(robot.transform)) continue;   // сам робот
            if (h.collider.GetComponentInParent<RobotController>() != null) continue;
            lines.Add("«" + NameOf(h.collider.transform) + "» y " + N(h.point.y));
            if (h.point.y > best) { best = h.point.y; bestWhat = NameOf(h.collider.transform); }
        }
        if (bestWhat != null)
        {
            topY = best;
            what = "верхняя опора «" + bestWhat + "» на y = " + N(best);
            if (lines.Count > 1) what += " (все попадания: " + string.Join(" · ", lines) + ")";
        }
    }

    // ================================================================== вспомогательное: поиск

    private static RobotController FindRobot(string name)
    {
        foreach (RobotController rc in UnityEngine.Object.FindObjectsByType<RobotController>(
                     FindObjectsInactive.Include))
        {
            if (rc == null) continue;
            if (rc.gameObject.name == name) return rc;
        }
        return null;
    }

    private static int CountRobots()
    {
        int n = 0;
        foreach (RobotController rc in UnityEngine.Object.FindObjectsByType<RobotController>(
                     FindObjectsInactive.Include))
            if (rc != null) n++;
        return n;
    }

    private static Transform FindTransformByName(string name)
    {
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include))
            if (t != null && t.name == name) return t;
        return null;
    }

    private static int CountMissingComponents()
    {
        int n = 0;
        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include))
        {
            if (go == null) continue;
            foreach (Component c in go.GetComponents<Component>()) if (c == null) n++;
        }
        return n;
    }

    /// <summary>Сколько звеньев робота осталось БЕЗ ссылки (0 — кинематика цела).</summary>
    private static int MissingJoints(RobotController rc)
    {
        int missing = 0;
        SixAxisController six = rc as SixAxisController;
        SCARAController scara = rc as SCARAController;
        if (six != null)
        {
            if (six.jointTransforms == null) return 99;
            foreach (Transform t in six.jointTransforms) if (t == null) missing++;
            if (six.baseTransform == null) missing++;
        }
        else if (scara != null)
        {
            if (scara.joint1 == null) missing++;
            if (scara.joint2 == null) missing++;
            if (scara.joint3 == null) missing++;
        }
        return missing;
    }

    /// <summary>Внешние ссылки на роботов в сцене (что станет null, если роботов удалить).</summary>
    private static List<string> CollectRobotRefs()
    {
        var list = new List<string>();
        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include))
        {
            if (go == null) continue;
            if (go.GetComponentInParent<RobotController>() != null) continue;
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                SerializedObject so;
                try { so = new SerializedObject(c); }
                catch { continue; }
                SerializedProperty it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    UnityEngine.Object val = it.objectReferenceValue;
                    if (val == null) continue;
                    RobotController owner = OwnerRobot(val);
                    if (owner == null) continue;
                    list.Add("• «" + PathOf(go.transform) + "» → " + c.GetType().Name + "." +
                             it.propertyPath + " → «" + owner.name + "»");
                }
            }
        }
        return list;
    }

    /// <summary>Верх геометрии-опоры ПОД объектом (свои коллайдеры исключены), NaN — если опоры нет.</summary>
    private static float SupportTopUnder(GameObject go)
    {
        float top;
        string what;
        FindSupport(go, out top, out what);
        return top;
    }

    // ================================================================== вспомогательное: файл

    /// <summary>Перечитка файла с диска: формат, висячие ссылки, следы роботов (по guid FBX).</summary>
    private static string ReReadFile(string full)
    {
        var sb = new StringBuilder();
        if (!File.Exists(full)) { sb.AppendLine("ПЕРЕЧИТКА: файла нет"); return sb.ToString(); }
        string text = File.ReadAllText(full);
        sb.AppendLine("ПЕРЕЧИТКА ФАЙЛА С ДИСКА:");
        sb.AppendLine("  первая строка: " + (text.StartsWith("%YAML 1.1")
            ? "«%YAML 1.1» — канонично" : "НЕ КАНОН (нет «%YAML 1.1»)"));
        sb.AppendLine("  заголовок «%TAG !u!»: " + (text.Contains("%TAG !u!") ? "есть" : "НЕТ"));
        int badStripped = System.Text.RegularExpressions.Regex.Matches(text, @"&\d+stripped").Count;
        sb.AppendLine("  stripped-заголовков без пробела (неканоничных): " + badStripped);
        List<string> dangling = DanglingFileIds(text);
        sb.AppendLine("  висячих fileID: " + dangling.Count +
                      (dangling.Count > 0 ? " → " + string.Join(", ", dangling) : ""));
        sb.AppendLine("  документов в файле: " +
                      System.Text.RegularExpressions.Regex.Matches(text, @"(?m)^--- !u!").Count);
        foreach (string fbx in new[] { "Assets/Robots/Robot.fbx", "Assets/Robots/ScaraRobot.fbx" })
        {
            string guid = AssetDatabase.AssetPathToGUID(fbx);
            int n = string.IsNullOrEmpty(guid) ? -1 : CountOf(text, guid);
            sb.AppendLine("  упоминаний guid " + Path.GetFileName(fbx) + " (" + guid + "): " + n);
        }
        return sb.ToString();
    }

    private static int CountOf(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    /// <summary>
    /// Ссылки сцены на несуществующие объекты (тот же разбор, что у сторожа
    /// `KvStandsOnDemandTests.Scene_HasNoDanglingFileIds`): ссылка `{fileID: N}` без guid
    /// обязана указывать на документ этого же файла.
    /// </summary>
    private static List<string> DanglingFileIds(string sceneText)
    {
        var ids = new HashSet<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                     sceneText, @"(?m)^--- !u!\d+ &(-?\d+)"))
            ids.Add(m.Groups[1].Value);

        var bad = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                     sceneText, @"\{fileID: (-?\d+)(, guid: ([0-9a-fA-F]{32}))?\}"))
        {
            string id = m.Groups[1].Value;
            bool external = m.Groups[2].Success &&
                            m.Groups[3].Value != "00000000000000000000000000000000";
            if (external || id == "0" || id.StartsWith("-") || ids.Contains(id)) continue;
            if (!bad.Contains(id)) bad.Add(id);
        }
        return bad;
    }

    private static void Report(string title, string text)
    {
        Debug.Log(LogTag + " " + title + "\n" + text);
        WriteReport2(ReportFileFor(title), text);
    }

    private static string ReportFileFor(string title)
    {
        if (title.Contains("ПРЕФАБ")) return "_s56_prefabs.txt";
        if (title.Contains("УДАЛЕНИЕ")) return "_s56_remove.txt";
        if (title.Contains("СТЕНДЫ")) return "_s56_place.txt";
        return "_s56_diag.txt";
    }

    private static void WriteReport2(string fileName, string text)
    {
        try { File.WriteAllText(Path.Combine(ProjectRoot, fileName), text, new UTF8Encoding(false)); }
        catch (Exception e) { Debug.LogWarning(LogTag + " отчёт " + fileName + " не записан: " + e.Message); }
    }

    private static string Yes(bool v) { return v ? "ДА" : "НЕТ"; }

    /// <summary>Состояние `tcp` словами (в файле он не задан — привязывается в рантайме).</summary>
    private static string TcpState(RobotController rc)
    {
        if (rc == null) return "—";
        return rc.tcp != null ? "«" + rc.tcp.name + "»" : "не задан в файле (привязывается в рантайме)";
    }

    /// <summary>Робот, которому принадлежит объект (сам объект или любой предок).</summary>
    private static RobotController OwnerRobot(UnityEngine.Object o)
    {
        GameObject go = o as GameObject;
        if (go == null)
        {
            Component c = o as Component;
            if (c != null) go = c.gameObject;
        }
        if (go == null) return null;
        return go.GetComponentInParent<RobotController>();
    }

    private static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.2f);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static Scene EnsureSceneLoaded()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isLoaded &&
            active.path == SceneAssetPath)
            return active;
        return EditorSceneManager.OpenScene(SceneAssetPath, OpenSceneMode.Single);
    }

    private static string NameOf(Transform t) { return t == null ? "?" : t.name; }

    private static string PathOf(Transform t)
    {
        if (t == null) return "?";
        string p = t.name;
        for (Transform c = t.parent; c != null; c = c.parent) p = c.name + "/" + p;
        return p;
    }

    private static string ProjectRoot
    {
        get { return Path.GetDirectoryName(Application.dataPath); }
    }

    private static string FullPath(string assetPath)
    {
        return Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string FileHash(string fullPath)
    {
        if (!File.Exists(fullPath)) return "—";
        using (var sha = SHA256.Create())
        using (var s = File.OpenRead(fullPath))
            return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "");
    }

    private static void WriteReport(string text)
    {
        try { File.WriteAllText(ReportPath, text, new UTF8Encoding(false)); }
        catch (Exception e) { Debug.LogWarning(LogTag + " отчёт не записан: " + e.Message); }
    }

    private static string N(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }

    private static string V(Vector3 v)
    {
        return "(" + N(v.x) + ", " + N(v.y) + ", " + N(v.z) + ")";
    }
}
#endif
