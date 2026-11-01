#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using KazistovVvUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// §58. ШАРИК ПРИЦЕЛА, СТОЛЫ-СТОЛЕШНИЦЫ И ВЫБОР РОБОТА — ДИАГНОСТИКА (ТОЛЬКО ЧТЕНИЕ).
///
/// ЗАЧЕМ. Оператор сообщил четыре симптома на живом PlayMode:
///   1) лазер/прицел СТАЛ наводиться на роботов (§57 — коллайдеры моделей, это работает);
///   2) ШАРИК на конце лазера ПРОПАЛ («раньше был видим, теперь нет»);
///   3) лазер НЕ «прилипает» к столам (столешницам);
///   4) F не выбирает робота, из-за чего не работают алгоритмы управления.
///
/// ЧТО ИЗМЕРЯЕТСЯ ЗДЕСЬ (EditMode, без сохранения сцены). Три гипотезы проверяются ФАКТОМ,
/// а не чтением кода:
///   A. У СТОЛОВ ЕСТЬ КОЛЛАЙДЕРЫ? Считаются коллайдеры у 5 каталожных префабов столов
///      (`Assets/Resources/Models/Tables/*.prefab`) и настройка `addColliders` у их моделей-источников
///      (`Assets/WorkTables/**/*.fbx`), после чего живой луч сверху вниз проверяет столешницу.
///   B. ЧТО СТАВИТ МАСТЕР «НОВЫЙ ПРОЕКТ»? Вызывается РОВНО тот путь, что у оператора
///      (`KvModelCatalog.Spawn` — его вызывает `KvNewProjectWizard.CreateProject`), и у поставленных
///      объектов считается: коллайдеры, `RobotController`, попадания луча. Всё поставленное
///      уничтожается тут же, сцена НЕ сохраняется (хеш файла сверяется до и после).
///   C. КОГО МОЖЕТ ВЫБРАТЬ F? Считаются роботы, которых видит `RobotController`
///      (только они выбираемы и управляемы) и что вернут три резерва выбора (`FindRobotUnderAim`
///      недоступен из EditMode — проверяется попадание луча в коллайдер и попадание в габарит).
///
/// БЕЗОПАСНОСТЬ: ничего не пишет в ассеты и сцену; временные объекты ставятся далеко (y = 5000)
/// или в нулевой точке проекта и уничтожаются; файл сцены хешируется до и после.
///
/// ЗАПУСК: меню `Tools → KazistovVv → Сцена (§58) → Отчёт (только чтение)`
/// либо из батча `-executeMethod KvAimScene58.BatchDiag58`.
/// </summary>
public static class KvAimScene58
{
    public const string SceneAssetPath = "Assets/_Project/00_Scenes/MainScene.unity";
    public const string ReportPath = @"C:\Users\Ольга\_s58_diag.txt";

    private const string MenuRoot = "Tools/KazistovVv/Сцена (§58)/";
    private const string LogTag = "[§58 Aim]";

    private const string FunctionalRobotsFolder = "Assets/Resources/Robots";
    private const string CatalogRobotsFolder = "Assets/Resources/Models/Robots";
    private const string CatalogTablesFolder = "Assets/Resources/Models/Tables";
    private const string TableModelsFolder = "Assets/WorkTables";

    /// <summary>Дальняя точка для временных инстансов: вне сцены (пол — 1000 × 1000 в нуле).</summary>
    private static readonly Vector3 ProbeOrigin = new Vector3(0f, 5000f, 0f);

    // ================================================================== меню

    [MenuItem(MenuRoot + "Отчёт (только чтение)", false, 10)]
    public static void MenuDiag()
    {
        EnsureSceneLoaded();
        string report = Diag("ОТЧЁТ §58 (только чтение)");
        Debug.Log(LogTag + "\n" + report);
        WriteReport(ReportPath, report);
    }

    // ================================================================== батч

    public static void BatchDiag58()
    {
        if (!File.Exists(FullPath(SceneAssetPath)))
            throw new Exception(LogTag + " нет файла сцены: " + SceneAssetPath);

        string report = Diag("БАТЧ-ДИАГНОСТИКА §58 (только чтение)");
        Debug.Log(LogTag + "\n" + report);
        WriteReport(ReportPath, report);
    }

    // ================================================================== правка: коллайдеры столов

    [MenuItem(MenuRoot + "1. Включить коллайдеры столов (модели + префабы, с проверкой)", false, 20)]
    public static void MenuApplyTableColliders()
    {
        if (!EditorUtility.DisplayDialog("KazistovVv · §58 · коллайдеры столов",
                "Будет сделано:\n" +
                "  • у моделей столов `Assets/WorkTables/**/*.fbx` включена генерация коллайдеров\n" +
                "    (сейчас выключена — поэтому лазер, Mouse3 и шарик прицела «не видят» столешниц);\n" +
                "  • модели переимпортированы (ForceUpdate);\n" +
                "  • в 5 префабах столов каталога добавлено по одному BoxCollider на каждый меш\n" +
                "    (префабы — «плоские» копии модели, генерация коллайдеров импортёра до них не доходит);\n" +
                "  • проверено: коллайдеры покрывают модель, лежат на слое «Default» и луч сверху вниз\n" +
                "    попадает в СТОЛЕШНИЦУ, а не в пол за ней.\n\n" +
                "Сцена НЕ меняется и НЕ сохраняется.",
                "Включить и проверить", "Отмена"))
            return;
        Report("КОЛЛАЙДЕРЫ СТОЛОВ §58 (по команде оператора)", ApplyTableColliders());
    }

    /// <summary>Точка входа для `-executeMethod`.</summary>
    public static void BatchApplyTableColliders58()
    {
        Report("КОЛЛАЙДЕРЫ СТОЛОВ §58 (батч)", ApplyTableColliders());
    }

    private static string ApplyTableColliders()
    {
        var sb = new StringBuilder();
        sb.AppendLine("§58 КОЛЛАЙДЕРЫ СТОЛОВ (лазер, Mouse3, шарик прицела)");
        sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Проект: " + ProjectRoot);
        sb.AppendLine();

        string full = FullPath(SceneAssetPath);
        sb.AppendLine("MainScene.unity ДО: " + FileHash(full));
        sb.AppendLine();

        EnsureSceneLoaded();

        // ---------- 1. модели-источники: правило импортёра + переимпорт ----------
        var models = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { TableModelsFolder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(p) && !models.Contains(p)) models.Add(p);
        }
        models.Sort(StringComparer.OrdinalIgnoreCase);

        sb.AppendLine("=== МОДЕЛИ СТОЛОВ (" + models.Count + ") ===");
        foreach (string path in models)
        {
            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) { sb.AppendLine("• " + path + " — НЕ ModelImporter, пропуск"); continue; }
            bool want = KazistovVvUI.EditorTools.KvModelImportPostprocessor.InColliderRoot(path);
            bool before = mi.addCollider;
            if (before != want)
            {
                mi.addCollider = want;
                mi.SaveAndReimport();
                sb.AppendLine("• " + path + ": addColliders " + before + " → " + want + " (переимпорт)");
            }
            else
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                sb.AppendLine("• " + path + ": addColliders уже " + before + " (переимпорт ForceUpdate)");
            }
            if (!want)
                sb.AppendLine("  ВНИМАНИЕ: путь не входит в ColliderRoots — коллайдеров у стола не будет");
        }
        sb.AppendLine();

        // ---------- 2. префабы столов: по боксу на меш ----------
        sb.AppendLine("=== ПРЕФАБЫ СТОЛОВ (" + CatalogTablesFolder + ") ===");
        List<string> prefabs = PrefabsIn(CatalogTablesFolder);
        int ok = 0;
        var problems = new List<string>();
        foreach (string path in prefabs)
        {
            sb.AppendLine("• " + path);
            sb.AppendLine("    правка: " + FixPrefabColliders(path));
            sb.AppendLine(CheckTableInstance(path, out bool good, out string detail));
            if (good) ok++; else problems.Add(Path.GetFileName(path) + ": " + detail);
        }
        sb.AppendLine();
        sb.AppendLine("префабов столов: " + prefabs.Count + " · прошли проверку: " + ok +
                      " · проблем: " + problems.Count);
        foreach (string p in problems) sb.AppendLine("  · " + p);
        sb.AppendLine();

        sb.AppendLine("MainScene.unity ПОСЛЕ: " + FileHash(full) +
                      " (сцена не менялась и не сохранялась)");
        return sb.ToString();
    }

    /// <summary>
    /// По одному `BoxCollider` на каждый меш без коллайдера + уборка вырожденных корневых боксов.
    /// Тот же приём, что в §57 для префабов роботов: префабы каталога — «плоские» копии модели,
    /// генерация коллайдеров импортёра до них не доходит. Работает через `LoadPrefabContents`
    /// (сцена не затрагивается).
    /// </summary>
    private static string FixPrefabColliders(string path)
    {
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return "не удалось открыть префаб";

            int removed = 0, added = 0;
            foreach (BoxCollider bc in root.GetComponents<BoxCollider>())
            {
                if (bc == null) continue;
                if (bc.size.magnitude < 0.01f)
                {
                    UnityEngine.Object.DestroyImmediate(bc);
                    removed++;
                }
            }

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r.GetComponent<Collider>() != null) continue;
                Mesh mesh = MeshOf(r);
                if (mesh == null) continue;
                BoxCollider box = r.gameObject.AddComponent<BoxCollider>();
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size;
                added++;
            }

            // Грубый бокс на КОРНЕ описывает габаритную коробку стола: шарик «прилипал» бы к её
            // грани, а не к столешнице. Убираем, если коллайдеры есть на детях (как в §57).
            bool childColliders = false;
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c == null) continue;
                if (c.transform != root.transform) { childColliders = true; break; }
            }
            if (childColliders)
            {
                foreach (BoxCollider bc in root.GetComponents<BoxCollider>())
                {
                    if (bc == null) continue;
                    UnityEngine.Object.DestroyImmediate(bc);
                    removed++;
                }
            }

            bool changed = removed > 0 || added > 0;
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            int total = root.GetComponentsInChildren<Collider>(true).Length;
            return "корневых боксов убрано " + removed + " · боксов по мешам добавлено " + added +
                   " · коллайдеров стало " + total + (changed ? " (сохранено)" : " (изменений нет)");
        }
        catch (Exception e)
        {
            return "ОШИБКА: " + e.GetType().Name + " — " + e.Message;
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Проверка на ЖИВОМ инстансе: коллайдеры покрывают модель, лежат на слое «Default», луч
    /// сверху вниз по столешнице и наклонный луч оператора попадают ИМЕННО в стол.
    /// </summary>
    private static string CheckTableInstance(string path, out bool good, out string detail)
    {
        good = false;
        detail = "";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { detail = "префаб не загружается"; return "    инстанс: не загружается"; }

        GameObject inst = null;
        try
        {
            inst = UnityEngine.Object.Instantiate(prefab);
            inst.name = "§58 table check";
            inst.transform.position = ProbeOrigin;
            foreach (Transform tr in inst.GetComponentsInChildren<Transform>(true))
                if (tr != null && !tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
            Physics.SyncTransforms();

            Bounds mb = MeshBounds(inst);
            Collider[] cols = inst.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0)
            {
                detail = "коллайдеров 0";
                return "    инстанс: КОЛЛАЙДЕРОВ 0 (луч уходит в пол за столом)";
            }

            Bounds cb = new Bounds(cols[0].bounds.center, cols[0].bounds.size);
            for (int i = 1; i < cols.Length; i++) cb.Encapsulate(cols[i].bounds);
            bool covers = Covers(cb, mb);

            var badLayers = new List<string>();
            foreach (Collider c in cols)
            {
                int l = c.gameObject.layer;
                if (l == 0) continue;
                string ln = l + "«" + LayerMask.LayerToName(l) + "»";
                if (!badLayers.Contains(ln)) badLayers.Add(ln);
            }

            Vector3 top = new Vector3(mb.center.x, mb.max.y, mb.center.z);
            bool down = RayHits(top + Vector3.up * 1.5f, Vector3.down, 4f, inst);
            Vector3 from = top + new Vector3(0f, 0.8f, -1.4f);
            bool slant = RayHits(from, (top - from).normalized, 4f, inst);

            good = covers && badLayers.Count == 0 && down && slant;
            if (!covers) detail = "коллайдеры не покрывают модель";
            else if (!down) detail = "луч сверху вниз по столешнице не попал в стол";
            else if (!slant) detail = "наклонный луч оператора не попал в стол";
            else if (badLayers.Count > 0) detail = "коллайдеры не на слое Default: " + string.Join(", ", badLayers);

            return "    инстанс: коллайдеров " + cols.Length + " · габарит по мешам " + V(mb.size) +
                   " · верх столешницы y = " + Num(mb.max.y) + " · покрытие " + (covers ? "ДА" : "НЕТ") +
                   " · слои " + (badLayers.Count == 0 ? "Default" : string.Join(", ", badLayers)) +
                   " · луч вниз " + (down ? "в стол" : "МИМО") +
                   " · луч оператора " + (slant ? "в стол" : "МИМО");
        }
        finally
        {
            if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    private static bool RayHits(Vector3 from, Vector3 dir, float len, GameObject inst)
    {
        RaycastHit[] hits = Physics.RaycastAll(from, dir, len, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Collider first = null;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null) continue;
            if (h.distance < best) { best = h.distance; first = h.collider; }
        }
        return first != null && first.transform.IsChildOf(inst.transform);
    }

    private static bool Covers(Bounds outer, Bounds inner)
    {
        const float tol = 0.02f;
        return outer.min.x <= inner.min.x + tol && outer.min.y <= inner.min.y + tol &&
               outer.min.z <= inner.min.z + tol && outer.max.x >= inner.max.x - tol &&
               outer.max.y >= inner.max.y - tol && outer.max.z >= inner.max.z - tol;
    }

    private static void Report(string title, string text)
    {
        Debug.Log(LogTag + " " + title + "\n" + text);
        try
        {
            File.WriteAllText(Path.Combine(ProjectRoot, "_s58_tables.txt"), text,
                new UTF8Encoding(false));
        }
        catch (Exception e) { Debug.LogWarning(LogTag + " отчёт не записан: " + e.Message); }
    }

    // ================================================================== отчёт

    private static string Diag(string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine("§58 ДИАГНОСТИКА (read-only): шарик прицела, коллайдеры столов, выбор робота (F)");
        sb.AppendLine(title);
        sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Проект: " + ProjectRoot);
        sb.AppendLine();

        string full = FullPath(SceneAssetPath);
        sb.AppendLine("MainScene.unity ДО: " + FileHash(full) + " · байт " + FileSize(full));
        sb.AppendLine();

        Scene scene = EnsureSceneLoaded();

        FreeFlyCameraController cam = UnityEngine.Object.FindAnyObjectByType<FreeFlyCameraController>(
            FindObjectsInactive.Include);
        int laserMask = cam != null ? cam.laserLayers.value : Physics.DefaultRaycastLayers;
        int surfaceMask = cam != null
            ? (cam.surfaceLayer.value != 0 ? cam.surfaceLayer.value : cam.laserLayers.value)
            : Physics.DefaultRaycastLayers;

        SectionState(sb, scene, cam, laserMask, surfaceMask);
        SectionTablePrefabs(sb, cam, laserMask, surfaceMask);
        SectionTableModels(sb);
        SectionWizardPath(sb, cam, laserMask, surfaceMask);
        SectionSelection(sb);
        SectionAimIndicatorCode(sb);

        sb.AppendLine("MainScene.unity ПОСЛЕ отчёта: " + FileHash(full) + " · байт " + FileSize(full));
        sb.AppendLine("(отчёт НИЧЕГО не менял — хеш обязан совпасть с хешем ДО; поставленные для замера");
        sb.AppendLine(" объекты уничтожены, сцена не сохранялась)");
        return sb.ToString();
    }

    // ---------- 1. состояние сцены ----------

    private static void SectionState(StringBuilder sb, Scene scene, FreeFlyCameraController cam,
                                     int laserMask, int surfaceMask)
    {
        sb.AppendLine("=== 1. СОСТОЯНИЕ СЦЕНЫ (как её открыл оператор) ===");
        sb.AppendLine("сцена: " + scene.name + " · корней: " + scene.rootCount +
                      " · коллайдеров всего: " +
                      UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length);

        RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(
            FindObjectsInactive.Include);
        sb.AppendLine("роботов (RobotController) в сцене: " + robots.Length +
                      (robots.Length == 0
                          ? "  ← ВЫБИРАТЬ (F) И УПРАВЛЯТЬ НЕКЕМ: это модели без кинематики"
                          : ""));
        foreach (RobotController rc in robots)
            if (rc != null)
                sb.AppendLine("  • «" + rc.name + "» " + rc.GetType().Name + " на " + V(rc.transform.position));

        var roots = new List<string>();
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go == null) continue;
            int cols = go.GetComponentsInChildren<Collider>(true).Length;
            bool isTable = LooksLikeTable(go.name);
            roots.Add("  • «" + go.name + "» · коллайдеров " + cols + (isTable ? "  ← ПОХОЖ НА СТОЛ" : ""));
        }
        sb.AppendLine("корневые объекты (" + roots.Count + "):");
        foreach (string r in roots) sb.AppendLine(r);

        int tableLike = 0;
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t != null && LooksLikeTable(t.name)) tableLike++;
        sb.AppendLine("объектов с «стол/table/верстак» в имени: " + tableLike +
                      " (сцена §56 стартует БЕЗ столов — их ставит команда/мастер)");

        if (cam == null)
        {
            sb.AppendLine("ВНИМАНИЕ: в сцене нет FreeFlyCameraController — лазеру и F не с чем работать!");
        }
        else
        {
            sb.AppendLine("камера «" + cam.gameObject.name + "»:");
            sb.AppendLine("  laserLayers  = " + laserMask + " (" + MaskNames(laserMask) + ")");
            sb.AppendLine("  surfaceLayer = " + surfaceMask + " (" + MaskNames(surfaceMask) +
                          (cam.surfaceLayer.value == 0 ? "; пусто → берётся laserLayers" : "") + ")");
            sb.AppendLine("  laserLength = " + Num(cam.laserLength) +
                          " · min/max шарика " + Num(cam.minDistance) + "/" + Num(cam.maxDistance) +
                          " · шарик прицела рисует AimIndicator (компонент создаётся в Awake)");
        }

        // AimIndicator живёт на камере, создаётся в Awake — в EditMode его может не быть.
        AimIndicator aim = UnityEngine.Object.FindAnyObjectByType<AimIndicator>(FindObjectsInactive.Include);
        sb.AppendLine("AimIndicator в сцене: " + (aim != null
            ? "есть (markerSize " + Num(aim.markerSize) + " · эмиссия " + Num(aim.markerEmission) + ")"
            : "НЕТ — он создаётся в Awake камеры, то есть только в PlayMode"));
        sb.AppendLine();
    }

    // ---------- 2. префабы столов: коллайдеры ----------

    private static void SectionTablePrefabs(StringBuilder sb, FreeFlyCameraController cam,
                                            int laserMask, int surfaceMask)
    {
        sb.AppendLine("=== 2. ПРЕФАБЫ СТОЛОВ: КОЛЛАЙДЕРЫ И ЖИВОЙ ЛУЧ ПО СТОЛЕШНИЦЕ ===");
        List<string> prefabs = PrefabsIn(CatalogTablesFolder);
        if (prefabs.Count == 0) sb.AppendLine("(в " + CatalogTablesFolder + " нет префабов)");
        int without = 0;
        foreach (string path in prefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("• " + path + " — НЕ ЗАГРУЖАЕТСЯ"); continue; }
            Collider[] cols = prefab.GetComponentsInChildren<Collider>(true);
            sb.AppendLine("• " + path);
            sb.AppendLine("    рендереров " + prefab.GetComponentsInChildren<Renderer>(true).Length +
                          " · КОЛЛАЙДЕРОВ: " + cols.Length);
            if (cols.Length == 0)
            {
                without++;
                sb.AppendLine("    ← ЛАЗЕР НЕ МОЖЕТ «ПРИЛИПНУТЬ» К ЭТОМУ СТОЛУ: Physics.Raycast его не видит");
            }
            sb.AppendLine("    исходные модели: " + DescribeModelDeps(path));
            sb.AppendLine("    живой луч по столешнице: " +
                          ProbeTableTop(path, surfaceMask, laserMask));
            sb.AppendLine();
        }
        sb.AppendLine("префабов столов: " + prefabs.Count + " · БЕЗ коллайдеров: " + without);
        sb.AppendLine();
    }

    private static void SectionTableModels(StringBuilder sb)
    {
        sb.AppendLine("=== 3. МОДЕЛИ СТОЛОВ (Assets/WorkTables): НАСТРОЙКА ИМПОРТЁРА ===");
        var found = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { TableModelsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) continue;
            found++;
            sb.AppendLine("• " + path + ": addColliders=" + mi.addCollider + " · isReadable=" + mi.isReadable +
                          " · globalScale=" + Num(mi.globalScale) + " · useFileScale=" + mi.useFileScale);
        }
        sb.AppendLine("моделей столов найдено: " + found + " · правило импортёра ColliderRoots: " +
                      string.Join(", ", KazistovVvUI.EditorTools.KvModelImportPostprocessor.ColliderRoots) +
                      " (путь стола входит: " +
                      (KazistovVvUI.EditorTools.KvModelImportPostprocessor.InColliderRoot(
                          "Assets/WorkTables/x.fbx") ? "ДА" : "НЕТ") + ")");
        sb.AppendLine();
    }

    // ---------- 4. путь мастера «Новый проект» ----------

    private static void SectionWizardPath(StringBuilder sb, FreeFlyCameraController cam,
                                          int laserMask, int surfaceMask)
    {
        sb.AppendLine("=== 4. ЧТО СТАВИТ МАСТЕР «НОВЫЙ ПРОЕКТ» (тот же путь, что у оператора) ===");
        sb.AppendLine("нулевая точка проекта (SceneZero): " + V(KazistovVvFeatures.KvNewProjectWizard.SceneZero()));

        KvModelEntry table = KvModelCatalog.Find("kenney_worktable");
        if (table == null) table = FirstOfKind(KvModelKind.Table);
        KvModelEntry robot = KvModelCatalog.Find("stand_6axis");
        if (robot == null) robot = FirstOfKind(KvModelKind.Robot);
        if (table == null || robot == null)
        {
            sb.AppendLine("ВНИМАНИЕ: каталог пуст — путь мастера не воспроизвести");
            sb.AppendLine();
            return;
        }
        sb.AppendLine("выбран стол: «" + table.Title + "» (" + table.Id + ")");
        sb.AppendLine("выбран робот: «" + robot.Title + "» (" + robot.Id + ")");
        sb.AppendLine();

        Vector3 zero = KazistovVvFeatures.KvNewProjectWizard.SceneZero();
        RegisteredObject tableObj = null;
        RegisteredObject robotObj = null;
        var created = new List<GameObject>();
        try
        {
            // ПОРЯДОК ВАЖЕН: сначала СТОЛ и луч по нему, и только потом робот. Иначе луч сверху
            // вниз попадает в деталь уже поставленного робота (первый прогон §58 так и сделал)
            // и замер столешницы перестаёт что-либо доказывать.
            tableObj = KvModelCatalog.Spawn(table, zero, 0f);
            if (tableObj != null) created.Add(tableObj.gameObject);
            Physics.SyncTransforms();

            // --- стол ---
            sb.AppendLine("— СТОЛ после постановки:");
            if (tableObj == null) sb.AppendLine("    НЕ ПОСТАВЛЕН (см. консоль)");
            else
            {
                GameObject t = tableObj.gameObject;
                Collider[] cols = t.GetComponentsInChildren<Collider>(true);
                Bounds rb = MeshBounds(t);
                sb.AppendLine("    «" + t.name + "» · коллайдеров: " + cols.Length +
                              " · габарит по мешам " + V(rb.size) + " · верх мешей y = " + Num(rb.max.y));
                sb.AppendLine("    луч вниз из точки над столешницей: " +
                              RayFrom(new Vector3(rb.center.x, rb.max.y + 1.5f, rb.center.z), Vector3.down,
                                      5f, surfaceMask) + "  ← если тут ПРОМАХ, шарик «не липнет» к столу");
                sb.AppendLine("    луч вниз маской «всё»: " +
                              RayFrom(new Vector3(rb.center.x, rb.max.y + 1.5f, rb.center.z), Vector3.down,
                                      5f, ~0));
            }
            sb.AppendLine();

            // --- робот ---
            Vector3 robotPos = new Vector3(zero.x, zero.y + Mathf.Max(0.1f, table.RealHeight), zero.z);
            robotObj = KvModelCatalog.Spawn(robot, robotPos, 0f);
            if (robotObj != null) created.Add(robotObj.gameObject);
            Physics.SyncTransforms();

            sb.AppendLine("— РОБОТ после постановки:");
            if (robotObj == null) sb.AppendLine("    НЕ ПОСТАВЛЕН (см. консоль)");
            else
            {
                GameObject r = robotObj.gameObject;
                RobotController rc = r.GetComponentInChildren<RobotController>(true);
                Collider[] cols = r.GetComponentsInChildren<Collider>(true);
                Bounds rb = MeshBounds(r);
                sb.AppendLine("    «" + r.name + "» · RobotController: " + (rc != null ? rc.GetType().Name : "НЕТ") +
                              " · коллайдеров " + cols.Length + " · габарит " + V(rb.size));
                // Наводимся на самую крупную деталь — как оператор наводит лазер на робота.
                Vector3 aim = cols.Length > 0 ? Biggest(cols).bounds.center : rb.center;
                Vector3 from = aim + new Vector3(0.1f, 0.25f, -1.4f);
                Vector3 dir = (aim - from).normalized;
                sb.AppendLine("    луч из позиции оператора (" + V(from) + ") в деталь: " +
                              RayFrom(from, dir, 10f, laserMask));
                sb.AppendLine("    габарит робота ловит луч (резерв `FindRobotByAimRay`): " +
                              (BoundsIntersect(rb, from, dir, 50f) ? "ДА" : "НЕТ"));
                if (rc == null)
                {
                    sb.AppendLine("    ← F (SelectRobotByAim) НЕ МОЖЕТ ВЫБРАТЬ ЭТОГО РОБОТА: он не RobotController,");
                    sb.AppendLine("      это ВИЗУАЛЬНАЯ модель каталога. Алгоритмы управления (PoseValidator,");
                    sb.AppendLine("      ReachabilityOracle, TrajectoryFlowController) собираются ТОЛЬКО по");
                    sb.AppendLine("      RobotController — то есть управлять тоже нечем.");
                }
            }
            sb.AppendLine();

            sb.AppendLine("— ИТОГ ПО ПУТИ МАСТЕРА: роботов (RobotController) в сцене стало " +
                          UnityEngine.Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include).Length +
                          " · KvRobotSelection.Count = " + KvRobotSelection.Count);
            sb.AppendLine("   (KvRobotSelection §49 видит только RobotController — цифры 1/2 на визуальной модели не работают)");
        }
        catch (Exception e)
        {
            sb.AppendLine("ОШИБКА воспроизведения пути мастера: " + e.GetType().Name + " — " + e.Message);
        }
        finally
        {
            foreach (GameObject go in created)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            Physics.SyncTransforms();
        }
        sb.AppendLine();
    }

    // ---------- 5. Ф и список роботов ----------

    private static void SectionSelection(StringBuilder sb)
    {
        sb.AppendLine("=== 5. КОГО ВИДИТ ВЫБОР РОБОТА ===");
        sb.AppendLine("KvRobotSelection.Count = " + KvRobotSelection.Count +
                      " (§49: список строится по RobotController)");
        sb.AppendLine("RobotController-ов в сцене: " +
                      UnityEngine.Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include).Length);
        sb.AppendLine("F (`SelectRobotByAim`) работает в три попытки:");
        sb.AppendLine("  1) `FindRobotUnderAim` — попадание луча в коллайдер, у которого в родителях RobotController;");
        sb.AppendLine("  2) `FindRobotByAimRay` (§57) — луч × габарит RobotController;");
        sb.AppendLine("  3) `FindRobotByAimVolume` — точка прицела внутри габарита RobotController.");
        sb.AppendLine("Все три перебирают ТОЛЬКО RobotController-ов: если их в сцене 0 — выбрать некого,");
        sb.AppendLine("и F молчит (в консоль ничего не пишется — это и выглядит как «клавиша не работает»).");
        sb.AppendLine();
    }

    private static void SectionAimIndicatorCode(StringBuilder sb)
    {
        sb.AppendLine("=== 6. ШАРИК ПРИЦЕЛА: КОГДА ОН ПРЯЧЕТСЯ (по коду AimIndicator) ===");
        string src = ReadSource("Assets/_Project/01_Scripts/Trajectory/AimIndicator.cs");
        if (src == null) { sb.AppendLine("исходник AimIndicator не прочитан"); sb.AppendLine(); return; }
        int line = 0;
        foreach (string raw in src.Split('\n'))
        {
            line++;
            string t = raw.Trim();
            if (t.Contains("marker.SetActive(false)"))
                sb.AppendLine("  строка " + line + ": " + t + "   ← ПРЯЧЕТ ШАРИК");
            if (t.Contains("marker.SetActive(true)"))
                sb.AppendLine("  строка " + line + ": " + t + "   ← показывает шарик");
            if (t.Contains("if (lastRobot == null"))
                sb.AppendLine("  строка " + line + ": " + t + "   ← условие: без РОБОТА шарика нет");
            if (t.Contains("if (robot == null)"))
                sb.AppendLine("  строка " + line + ": " + t + "   ← то же в Update()");
        }
        sb.AppendLine("ВЫВОД ПО КОДУ: шарик показывается ТОЛЬКО когда найден RobotController и оракул готов.");
        sb.AppendLine("Если в сцене нет RobotController (модели каталога) — шарик исчезает целиком,");
        sb.AppendLine("хотя луч прицела и лазеры продолжают работать. Это и есть симптом §58 «шарик пропал».");
        sb.AppendLine();
    }

    // ================================================================== вспомогательное

    private static string ProbeTableTop(string prefabPath, int surfaceMask, int laserMask)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return "префаб не загружается";
        GameObject inst = null;
        try
        {
            inst = UnityEngine.Object.Instantiate(prefab);
            inst.name = "§58 table probe";
            inst.transform.position = ProbeOrigin;
            foreach (Transform tr in inst.GetComponentsInChildren<Transform>(true))
                if (tr != null && !tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
            Physics.SyncTransforms();

            Bounds b = MeshBounds(inst);
            if (b.size.sqrMagnitude < 1e-6f) return "нет мешей — мерить нечего";
            Vector3 from = new Vector3(b.center.x, b.max.y + 1.5f, b.center.z);
            return "столешница (верх мешей y = " + Num(b.max.y) + ") · маска шарика: " +
                   RayFrom(from, Vector3.down, 5f, surfaceMask) + " · маска лазера: " +
                   RayFrom(from, Vector3.down, 5f, laserMask) + " · «всё»: " +
                   RayFrom(from, Vector3.down, 5f, ~0);
        }
        finally
        {
            if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    private static string RayFrom(Vector3 from, Vector3 dir, float len, int mask)
    {
        RaycastHit hit;
        if (!Physics.Raycast(from, dir, out hit, len, mask, QueryTriggerInteraction.Ignore))
            return "ПРОМАХ (маска " + mask + " — " + MaskNames(mask) + ")";
        return "«" + hit.collider.name + "» на " + V(hit.point) + " (дистанция " + Num(hit.distance) +
               ", слой «" + LayerMask.LayerToName(hit.collider.gameObject.layer) + "»)";
    }

    private static bool BoundsIntersect(Bounds b, Vector3 from, Vector3 dir, float maxDist)
    {
        b.Expand(0.02f);
        float d;
        if (!b.IntersectRay(new Ray(from, dir), out d)) return false;
        return d >= 0f && d <= maxDist;
    }

    private static Collider Biggest(Collider[] cols)
    {
        Collider best = cols[0];
        for (int i = 1; i < cols.Length; i++)
            if (cols[i].bounds.size.sqrMagnitude > best.bounds.size.sqrMagnitude) best = cols[i];
        return best;
    }

    private static bool LooksLikeTable(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.ToLowerInvariant();
        return n.Contains("стол") || n.Contains("table") || n.Contains("верстак") || n.Contains("bench");
    }

    private static KvModelEntry FirstOfKind(KvModelKind kind)
    {
        List<KvModelEntry> list = KvModelCatalog.OfKind(kind);
        return list.Count > 0 ? list[0] : null;
    }

    private static List<string> PrefabsIn(string folder)
    {
        var list = new List<string>();
        if (!AssetDatabase.IsValidFolder(folder)) return list;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
            if (!list.Contains(p)) list.Add(p);
        }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    private static string DescribeModelDeps(string prefabPath)
    {
        string[] deps = AssetDatabase.GetDependencies(prefabPath, true);
        var parts = new List<string>();
        foreach (string d in deps)
        {
            if (!d.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
            ModelImporter mi = AssetImporter.GetAtPath(d) as ModelImporter;
            parts.Add(Path.GetFileName(d) + (mi != null
                ? " (addColliders=" + mi.addCollider + ", isReadable=" + mi.isReadable + ")"
                : " (не ModelImporter)"));
        }
        return parts.Count > 0 ? string.Join(" · ", parts) : "НЕТ .fbx в зависимостях";
    }

    /// <summary>Габарит модели ПО МЕШАМ — не зависит от того, «проснулся» ли рендерер.</summary>
    private static Bounds MeshBounds(GameObject go)
    {
        bool any = false;
        Bounds res = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            Mesh mesh = MeshOf(r);
            if (mesh == null) continue;
            Bounds wb = TransformBounds(r.transform.localToWorldMatrix, mesh.bounds);
            if (!any) { res = wb; any = true; }
            else res.Encapsulate(wb);
        }
        return res;
    }

    private static Mesh MeshOf(Renderer r)
    {
        MeshFilter mf = r.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;
        SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
        return smr != null ? smr.sharedMesh : null;
    }

    private static Bounds TransformBounds(Matrix4x4 m, Bounds b)
    {
        Vector3 e = b.extents;
        var res = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = b.center + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                               (i & 2) == 0 ? -e.y : e.y,
                                               (i & 4) == 0 ? -e.z : e.z);
            res.Encapsulate(m.MultiplyPoint3x4(c));
        }
        return res;
    }

    private static string MaskNames(int mask)
    {
        var names = new List<string>();
        for (int i = 0; i < 32; i++)
            if ((mask & (1 << i)) != 0)
            {
                string n = LayerMask.LayerToName(i);
                names.Add(i + (string.IsNullOrEmpty(n) ? "" : "«" + n + "»"));
            }
        return names.Count == 0 ? "ничего не выбрано" : string.Join(" + ", names);
    }

    private static Scene EnsureSceneLoaded()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isLoaded && active.path == SceneAssetPath) return active;
        return EditorSceneManager.OpenScene(SceneAssetPath, OpenSceneMode.Single);
    }

    private static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

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

    private static string FileSize(string fullPath)
    {
        return File.Exists(fullPath) ? new FileInfo(fullPath).Length.ToString() : "—";
    }

    private static string ReadSource(string assetPath)
    {
        string p = FullPath(assetPath);
        return File.Exists(p) ? File.ReadAllText(p, Encoding.UTF8) : null;
    }

    private static void WriteReport(string path, string text)
    {
        try { File.WriteAllText(path, text, new UTF8Encoding(false)); }
        catch (Exception e) { Debug.LogWarning(LogTag + " отчёт не записан: " + e.Message); }
    }

    private static string Num(float v)
    {
        return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string V(Vector3 v)
    {
        return "(" + Num(v.x) + ", " + Num(v.y) + ", " + Num(v.z) + ")";
    }
}
#endif
