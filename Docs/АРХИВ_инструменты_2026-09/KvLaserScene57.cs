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
/// §57. ЛАЗЕР, ВЫБОР И КОЛЛАЙДЕРЫ МОДЕЛЕЙ РОБОТОВ — ДИАГНОСТИКА (ТОЛЬКО ЧТЕНИЕ).
///
/// ЗАЧЕМ. Оператор: «лазер не попадает в новые модели (robot_6axis, scara_ls10b702s и 15 других
/// из каталога), Mouse3 (snap) не срабатывает, F не выбирает робота». Лазер и F работают через
/// `Physics.Raycast`, а у моделей роботов коллайдеров, судя по коду импорта, нет вовсе:
/// `KazistovVvMenu.cs:165` — `mi.addCollider = false` («коллайдеры НЕ генерируются, модели
/// декоративные») для `Assets/Robots|Tools|WorkTables`. Этот отчёт подтверждает (или опровергает)
/// причину ФАКТОМ, а не чтением кода: сколько коллайдеров у каждого префаба, на каком слое,
/// какие маски у камеры, и что реально возвращает `Physics.Raycast` при наведении на модель.
///
/// ЧТО ЕЩЁ СЧИТАЕТСЯ: настройки импортёра исходных FBX (`addCollider`, `isReadable`, `globalScale`),
/// наличие `RobotSelfCollision` (его капсулы r = 0,08 на слое 15), записи каталога (24/15,
/// `RealHeight`, `Scara`, префаб/иконка), компоненты `AimIndicator`/`LaserManager` и их ссылки.
///
/// БЕЗОПАСНОСТЬ: сцена НЕ сохраняется; для живого луча по модели делается ВРЕМЕННЫЙ инстанс
/// префаба далеко от сцены (y = 5000), который тут же уничтожается; хеш файла сцены сверяется
/// до и после отчёта. Сам файл ничего не запускает: ни `[InitializeOnLoad]`, ни автосохранения.
///
/// ЗАПУСК: меню `Tools → KazistovVv → Сцена (§57) → Отчёт (только чтение)`
/// либо из батча `-executeMethod KvLaserScene57.BatchDiag57`.
/// </summary>
public static class KvLaserScene57
{
    public const string SceneAssetPath = "Assets/_Project/00_Scenes/MainScene.unity";
    public const string ReportPath = @"C:\Users\Ольга\_s57_diag.txt";

    private const string MenuRoot = "Tools/KazistovVv/Сцена (§57)/";
    private const string LogTag = "[§57 Laser]";

    private const string FunctionalRobotsFolder = "Assets/Resources/Robots";
    private const string CatalogRobotsFolder = "Assets/Resources/Models/Robots";

    /// <summary>Дальняя точка для временного инстанса: вне сцены (пол — 1000 × 1000 в нуле).</summary>
    private static readonly Vector3 ProbeOrigin = new Vector3(0f, 5000f, 0f);

    // ================================================================== меню

    [MenuItem(MenuRoot + "Отчёт (только чтение)", false, 10)]
    public static void MenuDiag()
    {
        EnsureSceneLoaded();
        string report = Diag("ОТЧЁТ §57 (только чтение)");
        Debug.Log(LogTag + "\n" + report);
        WriteReport(report);
    }

    // ================================================================== батч

    public static void BatchDiag57()
    {
        if (!File.Exists(SceneAssetPath))
            throw new Exception(LogTag + " нет файла сцены: " + SceneAssetPath);

        string report = Diag("БАТЧ-ДИАГНОСТИКА §57 (только чтение)");
        Debug.Log(LogTag + "\n" + report);
        WriteReport(report);
    }

    // ================================================================== отчёт

    private static string Diag(string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine("§57 ДИАГНОСТИКА (read-only): коллайдеры моделей, слои, лучи, выбор робота");
        sb.AppendLine(title);
        sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Проект: " + ProjectRoot);
        sb.AppendLine();

        string full = FullPath(SceneAssetPath);
        string hashBefore = FileHash(full);
        sb.AppendLine("MainScene.unity ДО: " + hashBefore + " · байт " +
                      (File.Exists(full) ? new FileInfo(full).Length.ToString() : "—"));
        sb.AppendLine();

        Scene scene = EnsureSceneLoaded();

        // ---------- 1. слои проекта ----------
        sb.AppendLine("=== СЛОИ ПРОЕКТА (занятые) ===");
        for (int i = 0; i < 32; i++)
        {
            string name = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(name)) sb.AppendLine("  " + i + " = «" + name + "»");
        }
        // ВНИМАНИЕ: `Physics.DefaultRaycastLayers` — это int (маска), а не LayerMask,
        // поэтому `.value` у него нет (ошибка компиляции, поймана проверкой Roslyn).
        sb.AppendLine("  DefaultRaycastLayers = " + Physics.DefaultRaycastLayers +
                      " (" + MaskNames(Physics.DefaultRaycastLayers) + ")");
        sb.AppendLine();

        // ---------- 2. камера: маски лучей ----------
        sb.AppendLine("=== КАМЕРА: МАСКИ ЛУЧЕЙ (сериализованные значения в сцене) ===");
        FreeFlyCameraController cam = UnityEngine.Object.FindAnyObjectByType<FreeFlyCameraController>(
            FindObjectsInactive.Include);
        if (cam == null)
        {
            sb.AppendLine("ВНИМАНИЕ: в сцене нет FreeFlyCameraController — лазер и F не с чем связать!");
        }
        else
        {
            sb.AppendLine("• FreeFlyCameraController на «" + cam.gameObject.name + "» (слой «" +
                          LayerMask.LayerToName(cam.gameObject.layer) + "»)");
            sb.AppendLine("    laserLayers   = " + cam.laserLayers.value + " (" +
                          MaskNames(cam.laserLayers.value) + ")");
            sb.AppendLine("    surfaceLayer  = " + cam.surfaceLayer.value + " (" +
                          MaskNames(cam.surfaceLayer.value) + ")");
            int surfaceMask = cam.surfaceLayer.value != 0 ? cam.surfaceLayer.value : cam.laserLayers.value;
            sb.AppendLine("    SurfaceMask() = " + surfaceMask + " (" + MaskNames(surfaceMask) + ")" +
                          (cam.surfaceLayer.value == 0 ? "  (surfaceLayer пуст → берётся laserLayers)" : ""));
            sb.AppendLine("    laserLength = " + Num(cam.laserLength) + " м · min/max шарика " +
                          Num(cam.minDistance) + "/" + Num(cam.maxDistance));
        }
        foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include))
        {
            if (mb == null) continue;
            string tn = mb.GetType().Name;
            if (tn != "AimIndicator" && tn != "LaserManager") continue;
            sb.AppendLine("• " + tn + " на «" + mb.gameObject.name + "» активен=" + mb.isActiveAndEnabled);
            SerializedObject so = new SerializedObject(mb);
            SerializedProperty it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                UnityEngine.Object val = it.objectReferenceValue;
                if (val == null) continue;
                sb.AppendLine("    " + it.propertyPath + " → " + val.GetType().Name +
                              " «" + val.name + "»");
            }
        }
        sb.AppendLine();

        // ---------- 3. префабы роботов: коллайдеры ----------
        sb.AppendLine("=== ПРЕФАБЫ РОБОТОВ: КОЛЛАЙДЕРЫ, СЛОИ, ИСХОДНЫЕ МОДЕЛИ ===");
        List<string> prefabs = RobotPrefabs();
        int withoutColliders = 0;
        foreach (string path in prefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("• " + path + " — НЕ ЗАГРУЖАЕТСЯ"); continue; }

            Collider[] cols = prefab.GetComponentsInChildren<Collider>(true);
            Renderer[] rs = prefab.GetComponentsInChildren<Renderer>(true);
            RobotController rc = prefab.GetComponentInChildren<RobotController>(true);
            bool selfCollision = prefab.GetComponentInChildren<RobotSelfCollision>(true) != null;
            Bounds rb = BoundsOf(prefab);
            Bounds cb = BoundsOfColliders(cols);

            sb.AppendLine("• " + path);
            sb.AppendLine("    рендереров " + rs.Length + " · габарит " + V(rb.size) +
                          " · RobotController: " + (rc != null ? rc.GetType().Name : "НЕТ") +
                          (rc != null ? " · isDualArm=" + rc.isDualArm : "") +
                          " · RobotSelfCollision: " + (selfCollision ? "ЕСТЬ" : "нет"));
            if (cols.Length == 0)
            {
                withoutColliders++;
                sb.AppendLine("    КОЛЛАЙДЕРОВ: 0  ← лазер, Mouse3 и F по этой модели работать не могут");
            }
            else
            {
                sb.AppendLine("    КОЛЛАЙДЕРОВ: " + cols.Length + " · габарит коллайдеров " + V(cb.size) +
                              " · покрывают рендереры: " +
                              (Covers(cb, rb) ? "ДА" : "НЕТ (лучи промахиваются мимо частей модели)"));
                var byLayer = new Dictionary<string, int>();
                foreach (Collider c in cols)
                {
                    string ln = c.gameObject.layer + " «" + LayerMask.LayerToName(c.gameObject.layer) + "»";
                    byLayer[ln] = byLayer.ContainsKey(ln) ? byLayer[ln] + 1 : 1;
                }
                foreach (KeyValuePair<string, int> kv in byLayer)
                    sb.AppendLine("      слой " + kv.Key + ": " + kv.Value + " шт · в laserLayers: " +
                                  (cam != null && (cam.laserLayers.value & (1 << LayerOf(kv.Key))) != 0 ? "ДА" : "?"));
            }

            sb.AppendLine("    исходные модели: " + DescribeModelDeps(path));
            sb.AppendLine();
        }
        sb.AppendLine("префабов роботов: " + prefabs.Count + " · без коллайдеров: " + withoutColliders);
        sb.AppendLine();

        // ---------- 4. живой луч ----------
        sb.AppendLine("=== ЖИВОЙ ЛУЧ (временный инстанс префаба вне сцены, сцена НЕ сохраняется) ===");
        sb.AppendLine("  контроль: луч вниз по полу сцены из (0; 5; −24): " + ProbeFloor(cam));
        foreach (string path in prefabs)
        {
            if (!path.StartsWith(FunctionalRobotsFolder, StringComparison.Ordinal)) continue;
            sb.AppendLine("• " + Path.GetFileName(path) + ": " + ProbePrefab(path, cam));
        }
        sb.AppendLine();

        // ---------- 5. каталог ----------
        sb.AppendLine("=== КАТАЛОГ МОДЕЛЕЙ (KvModelCatalog) ===");
        KvModelEntry[] all = KvModelCatalog.All;
        List<KvModelEntry> robots = KvModelCatalog.OfKind(KvModelKind.Robot);
        sb.AppendLine("записей всего: " + (all != null ? all.Length : 0) +
                      " · роботов: " + (robots != null ? robots.Count : 0) +
                      " · столов: " + KvModelCatalog.CountOf(KvModelKind.Table));
        if (robots != null)
        {
            foreach (KvModelEntry e in robots)
            {
                if (e == null) continue;
                GameObject res = Resources.Load<GameObject>(e.ResourcesPath);
                Sprite icon = KvModelCatalog.LoadIconSprite(e.IconResourcesPath);
                sb.AppendLine("  • " + e.Id + " «" + e.Title + "» · RealHeight " + Num(e.RealHeight) +
                              " · Scara=" + e.Scara + " · Fbx=" + e.Fbx +
                              " · префаб: " + (res != null ? "есть" : "НЕТ (" + e.ResourcesPath + ")") +
                              " · иконка: " + (icon != null ? "есть" : "НЕТ") +
                              (res != null ? " · RobotController: " +
                                  (res.GetComponentInChildren<RobotController>(true) != null ? "ЕСТЬ" : "нет") : ""));
            }
        }
        sb.AppendLine();

        // ---------- 6. алгоритмы взаимодействия ----------
        sb.AppendLine("=== АЛГОРИТМЫ ВЗАИМОДЕЙСТВИЯ (состояние EditMode) ===");
        var anyFlow = UnityEngine.Object.FindAnyObjectByType<TrajectoryFlowController>(
            FindObjectsInactive.Include);
        sb.AppendLine("• TrajectoryFlowController: " + (anyFlow != null ? "есть" : "НЕТ"));
        RobotController[] sceneRobots = UnityEngine.Object.FindObjectsByType<RobotController>(
            FindObjectsInactive.Include);
        sb.AppendLine("• роботов в сцене: " + sceneRobots.Length +
                      " (в §56 сцена стартует без них — ставит команда)");
        sb.AppendLine("• KvRobotSelection.Count = " + KazistovVvUI.KvRobotSelection.Count);
        sb.AppendLine("• PoseValidator/ReachabilityOracle инициализируются в рантайме — их проверка в PlayMode-стороже");
        sb.AppendLine("• коллайдеров в сцене всего: " +
                      UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length);
        sb.AppendLine();

        sb.AppendLine("MainScene.unity ПОСЛЕ отчёта: " + FileHash(full) + " · байт " +
                      (File.Exists(full) ? new FileInfo(full).Length.ToString() : "—") +
                      " (отчёт НИЧЕГО не менял — хеш обязан совпасть с хешем ДО)");
        return sb.ToString();
    }

    // ================================================================== правка: коллайдеры

    /// <summary>§57: включить генерацию коллайдеров у моделей роботов, переимпортировать и проверить.</summary>
    [MenuItem(MenuRoot + "1. Включить коллайдеры моделей роботов (переимпорт + проверка)", false, 20)]
    public static void MenuApplyColliders()
    {
        if (!EditorUtility.DisplayDialog("KazistovVv · §57 · коллайдеры роботов",
                "Будет сделано:\n" +
                "  • у моделей `Assets/Robots/*.fbx` включена генерация коллайдеров\n" +
                "    (сейчас она выключена — поэтому лазер, Mouse3 и F «не видят» роботов);\n" +
                "  • модели переимпортированы (ForceUpdate);\n" +
                "  • проверено: у всех 17 префабов роботов есть коллайдеры, они покрывают модель,\n" +
                "    лежат на слое, входящем в `laserLayers`, и живой луч попадает в модель.\n\n" +
                "Столы и инструменты (`Assets/Tools/`, `Assets/WorkTables/`) не затрагиваются.\n" +
                "Сцена НЕ меняется и НЕ сохраняется.",
                "Включить и проверить", "Отмена"))
            return;
        Report57("КОЛЛАЙДЕРЫ §57 (по команде оператора)", ApplyColliders());
    }

    /// <summary>Точка входа для `-executeMethod`.</summary>
    public static void BatchApplyColliders57()
    {
        Report57("КОЛЛАЙДЕРЫ §57 (батч)", ApplyColliders());
    }

    private static string ApplyColliders()
    {
        var sb = new StringBuilder();
        sb.AppendLine("§57 КОЛЛАЙДЕРЫ МОДЕЛЕЙ РОБОТОВ (лазер, Mouse3, выбор по F)");
        sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Проект: " + ProjectRoot);
        sb.AppendLine();

        string full = FullPath(SceneAssetPath);
        sb.AppendLine("MainScene.unity ДО: " + FileHash(full));
        sb.AppendLine();

        EnsureSceneLoaded();
        FreeFlyCameraController cam = UnityEngine.Object.FindAnyObjectByType<FreeFlyCameraController>(
            FindObjectsInactive.Include);
        int laserMask = cam != null ? cam.laserLayers.value : Physics.DefaultRaycastLayers;

        List<string> prefabs = RobotPrefabs();

        // ---------- 1. модели-источники: настройка импортёра + переимпорт ----------
        var sources = new List<string>();
        foreach (string prefab in prefabs)
        {
            string[] deps = AssetDatabase.GetDependencies(prefab, true);
            foreach (string d in deps)
            {
                if (!d.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                if (!sources.Contains(d)) sources.Add(d);
            }
        }
        sources.Sort(StringComparer.OrdinalIgnoreCase);
        sb.AppendLine("=== МОДЕЛИ-ИСТОЧНИКИ (" + sources.Count + ") ===");
        foreach (string path in sources)
        {
            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null)
            {
                sb.AppendLine("• " + path + " — НЕ ModelImporter, пропуск");
                continue;
            }
            bool want = KazistovVvUI.EditorTools.KvModelImportPostprocessor.InColliderRoot(path);
            bool before = mi.addCollider;
            if (before != want)
            {
                mi.addCollider = want;
                mi.SaveAndReimport();
                sb.AppendLine("• " + path + ": addCollider " + before + " → " + want + " (переимпорт)");
            }
            else
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                sb.AppendLine("• " + path + ": addCollider уже " + before + " (переимпорт ForceUpdate)");
            }
        }
        sb.AppendLine();

        // ---------- 2. проверка префабов (+ запасной вариант, если коллайдеров нет) ----------
        sb.AppendLine("=== ПРАВКА И ПРОВЕРКА ПРЕФАБОВ ===");
        int ok = 0;
        var problems = new List<string>();
        foreach (string path in prefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { problems.Add(path + ": не загружается"); continue; }
            sb.AppendLine("• " + path);

            // 1) правка на уровне ПРЕФАБА: убрать вырожденные корневые боксы (след прогона
            //    с нулевым габаритом) и добить по одному BoxCollider на каждый меш без коллайдера.
            sb.AppendLine("    правка префаба: " + FixPrefabColliders(path));

            // 2) проверка на ВРЕМЕННОМ ИНСТАНСЕ, а не на ассете: у префаб-ассета `Collider.bounds`
            //    пустой (PhysX ещё не знает объект) — именно из-за этого первый прогон напечатал
            //    «габарит коллайдеров (0, 0, 0)». Мерить надо живьём.
            sb.AppendLine(CheckOnInstance(path, laserMask, out bool good, out string detail));
            if (good) ok++; else problems.Add(path + ": " + detail);
        }
        sb.AppendLine();
        sb.AppendLine("префабов роботов: " + prefabs.Count + " · прошли проверку: " + ok +
                      " · проблем: " + problems.Count);
        foreach (string p in problems) sb.AppendLine("  · " + p);
        sb.AppendLine();

        sb.AppendLine("MainScene.unity ПОСЛЕ: " + FileHash(full) +
                      " (сцена не менялась и не сохранялась)");
        return sb.ToString();
    }

    // ================================================================== правка: префабы

    /// <summary>
    /// Правка префаба: убрать вырожденные корневые боксы и поставить BoxCollider на каждый меш,
    /// у которого коллайдера нет. Работает через `LoadPrefabContents` — сцена не затрагивается.
    /// </summary>
    private static string FixPrefabColliders(string path)
    {
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return "не удалось открыть префаб";

            int removed = 0, added = 0;

            // 1) вырожденные боксы на корне (след прогона, где габарит посчитался нулевым)
            foreach (BoxCollider bc in root.GetComponents<BoxCollider>())
            {
                if (bc == null) continue;
                if (bc.size.magnitude < 0.01f)
                {
                    UnityEngine.Object.DestroyImmediate(bc);
                    removed++;
                }
            }

            // 2) по одному BoxCollider на каждый меш без коллайдера
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

            // 3) грубый бокс на КОРНЕ больше не нужен, если коллайдеры есть на детях: он описывает
            //    габаритную коробку модели, и лазер «прилипал» бы к её грани, а не к самой модели
            //    (у 6-осевого это ~0,3 м перед роботом). Убираем ТОЛЬКО BoxCollider корня;
            //    MeshCollider корня (если корень сам является мешем) остаётся.
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
            return "вырожденных корневых боксов убрано " + removed + " · боксов по мешам добавлено " +
                   added + " · коллайдеров стало " + total + (changed ? " (сохранено)" : " (изменений нет)");
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
    /// Проверка на ЖИВОМ инстансе: коллайдеры, покрытие габарита, слои и лучи с шести сторон.
    /// Инстанс ставится далеко от сцены (y = 5000) и тут же уничтожается.
    /// </summary>
    private static string CheckOnInstance(string path, int laserMask, out bool good, out string detail)
    {
        good = false;
        detail = "";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { detail = "префаб не загружается"; return "    инстанс: не загружается"; }

        GameObject inst = null;
        try
        {
            inst = UnityEngine.Object.Instantiate(prefab);
            inst.name = "§57 check";
            inst.transform.position = ProbeOrigin;

            // §57. У части каталожных моделей есть LODGroup, и ВДАЛИ ОТ КАМЕРЫ он гасит все уровни:
            // тогда коллайдеры не попадают в физику и луч «промахивается» — ровно это поймано на
            // `moveo` (128 коллайдеров, покрытие есть, попаданий 0). Для замера выключаем LODGroup
            // и включаем объекты инстанса. Для игры поведение не меняется: у камеры активен LOD0,
            // и коллайдеры на нём работают.
            int lodGroups = 0;
            foreach (LODGroup lg in inst.GetComponentsInChildren<LODGroup>(true))
            {
                if (lg == null) continue;
                lodGroups++;
                lg.enabled = false;
            }
            foreach (Transform tr in inst.GetComponentsInChildren<Transform>(true))
                if (tr != null && !tr.gameObject.activeSelf) tr.gameObject.SetActive(true);

            Physics.SyncTransforms();

            Bounds mb = MeshBounds(inst);
            Collider[] cols = inst.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0)
            {
                detail = "коллайдеров 0";
                return "    инстанс: КОЛЛАЙДЕРОВ 0 (лучи промахиваются — лазер/snap/F не работают)";
            }

            Bounds cb = new Bounds(cols[0].bounds.center, cols[0].bounds.size);
            for (int i = 1; i < cols.Length; i++) cb.Encapsulate(cols[i].bounds);
            bool covers = Covers(cb, mb);

            var badLayers = new List<string>();
            foreach (Collider c in cols)
            {
                int l = c.gameObject.layer;
                if ((laserMask & (1 << l)) != 0) continue;
                string ln = l + "«" + LayerMask.LayerToName(l) + "»";
                if (!badLayers.Contains(ln)) badLayers.Add(ln);
            }

            // §57. ЛУЧИ — В КАЖДУЮ ДЕТАЛЬ, А НЕ «ЧЕРЕЗ ЦЕНТР ГАБАРИТА». Шесть лучей через центр
            // AABB давали ложный промах на плоских «разложенных» моделях (`moveo`: детали разнесены
            // по 1,1 × 2,26 м, и центральная линия проходит сквозь пустоты, хотя `OverlapBox` видит
            // все 128 коллайдеров). Оператор же наводит лазер НА ДЕТАЛЬ — это и проверяем:
            // сверху вниз в центр каждой выбранной детали, луч обязан попасть в ту же модель.
            int sampled = 0, hitsMask = 0, hitsAny = 0, hitsOwn = 0;
            int step = Mathf.Max(1, cols.Length / 12);
            for (int i = 0; i < cols.Length && sampled < 12; i += step)
            {
                Collider c = cols[i];
                if (c == null) continue;
                sampled++;
                Vector3 to = c.bounds.center;
                Vector3 from = to + Vector3.up * (mb.extents.y + mb.size.magnitude + 1f);
                Vector3 dir = (to - from).normalized;
                float len = (to - from).magnitude + 0.5f;

                RaycastHit h1;
                if (Physics.Raycast(from, dir, out h1, len, laserMask, QueryTriggerInteraction.Ignore))
                {
                    hitsMask++;
                    if (h1.collider != null && h1.collider.transform.IsChildOf(inst.transform)) hitsOwn++;
                }
                RaycastHit h2;
                if (Physics.Raycast(from, dir, out h2, len, ~0, QueryTriggerInteraction.Ignore)) hitsAny++;
            }

            good = covers && badLayers.Count == 0 && hitsOwn > 0;
            if (!covers) detail = "коллайдеры не покрывают габарит модели";
            else if (hitsOwn == 0)
                detail = "луч не попадает в модель" + (hitsAny > 0
                    ? " (маской «всё» попадает — слой вне маски лазера)"
                    : " (и маской «всё» тоже — коллайдеры не в физике)");

            string dump = hitsOwn == 0 ? DumpColliders(inst, cols, mb) : "";

            return "    инстанс: коллайдеров " + cols.Length + " · активных " + ActiveColliders(cols) +
                   (lodGroups > 0 ? " · LODGroup " + lodGroups + " (в замере отключён)" : "") +
                   " · габарит по мешам " + V(mb.size) +
                   " · габарит коллайдеров " + V(cb.size) + " · покрытие " + (covers ? "ДА" : "НЕТ") +
                   " · слои " + (badLayers.Count == 0 ? "все в laserLayers" : string.Join(", ", badLayers)) +
                   " · лучи в детали (" + sampled + "): попали в модель " + hitsOwn +
                   " (маской камеры " + hitsMask + ", маской «всё» " + hitsAny + ")" + dump;
        }
        finally
        {
            if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    /// <summary>
    /// Разбор случая «коллайдеры есть, а луч не видит»: первые три коллайдера подробно,
    /// плюс `OverlapBox` по габариту — это отвечает, попали ли они в физическую сцену вообще.
    /// </summary>
    private static string DumpColliders(GameObject inst, Collider[] cols, Bounds mb)
    {
        var sb = new StringBuilder();
        int overlap = Physics.OverlapBox(mb.center, mb.extents + Vector3.one * 0.1f, Quaternion.identity,
            ~0, QueryTriggerInteraction.Ignore).Length;
        sb.Append("\n      разбор: OverlapBox по габариту видит " + overlap + " коллайдеров");
        sb.Append("\n      корень инстанса: active=" + inst.activeInHierarchy +
                  " · scale " + V(inst.transform.localScale) + " · слой " + inst.layer);
        for (int i = 0; i < cols.Length && i < 3; i++)
        {
            Collider c = cols[i];
            string type = c.GetType().Name;
            string extra = "";
            BoxCollider bc = c as BoxCollider;
            if (bc != null) extra = " size " + V(bc.size) + " center " + V(bc.center);
            MeshCollider mc = c as MeshCollider;
            if (mc != null) extra = " mesh " + (mc.sharedMesh != null
                ? "«" + mc.sharedMesh.name + "» (читаемый=" + mc.sharedMesh.isReadable + ")"
                : "НЕТ");
            sb.Append("\n      • " + type + " на «" + PathOf(c.transform) + "» active=" +
                      c.gameObject.activeInHierarchy + " enabled=" + c.enabled + " слой " + c.gameObject.layer +
                      " · мировые границы " + V(c.bounds.size) + " центр " + V(c.bounds.center) + extra);
        }
        return sb.ToString();
    }

    private static string PathOf(Transform t)
    {
        if (t == null) return "?";
        string p = t.name;
        for (Transform c = t.parent; c != null; c = c.parent) p = c.name + "/" + p;
        return p;
    }

    private static int ActiveColliders(Collider[] cols)
    {
        int n = 0;
        foreach (Collider c in cols)
            if (c != null && c.enabled && c.gameObject.activeInHierarchy) n++;
        return n;
    }

    /// <summary>Габарит модели ПО МЕШАМ: не зависит от того, «проснулся» ли рендерер.</summary>
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

    private static void Report57(string title, string text)
    {
        Debug.Log(LogTag + " " + title + "\n" + text);
        try
        {
            File.WriteAllText(Path.Combine(ProjectRoot, "_s57_colliders.txt"), text,
                new UTF8Encoding(false));
        }
        catch (Exception e) { Debug.LogWarning(LogTag + " отчёт не записан: " + e.Message); }
    }

    // ================================================================== вспомогательное

    /// <summary>Все префабы роботов: 2 рабочих (§56) + 15 каталожных.</summary>
    private static List<string> RobotPrefabs()
    {
        var list = new List<string>();
        foreach (string folder in new[] { FunctionalRobotsFolder, CatalogRobotsFolder })
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                if (!list.Contains(p)) list.Add(p);
            }
        }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    /// <summary>Исходные модели (.fbx) префаба и настройки их импортёра.</summary>
    private static string DescribeModelDeps(string prefabPath)
    {
        string[] deps = AssetDatabase.GetDependencies(prefabPath, true);
        var parts = new List<string>();
        foreach (string d in deps)
        {
            if (!d.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                !d.EndsWith(".FBX", StringComparison.OrdinalIgnoreCase)) continue;
            ModelImporter mi = AssetImporter.GetAtPath(d) as ModelImporter;
            parts.Add(Path.GetFileName(d) +
                      (mi != null ? " (addCollider=" + mi.addCollider + ", isReadable=" + mi.isReadable +
                                    ", globalScale=" + Num(mi.globalScale) +
                                    ", useFileScale=" + mi.useFileScale + ")" : " (не ModelImporter)"));
        }
        return parts.Count > 0 ? string.Join(" · ", parts) : "НЕТ .fbx в зависимостях";
    }

    /// <summary>Луч вниз по полу сцены — контроль: маска и коллайдеры окружения.</summary>
    private static string ProbeFloor(FreeFlyCameraController cam)
    {
        int mask = cam != null
            ? (cam.surfaceLayer.value != 0 ? cam.surfaceLayer.value : cam.laserLayers.value)
            : Physics.DefaultRaycastLayers;
        RaycastHit hit;
        bool ok = Physics.Raycast(new Vector3(0f, 5f, -24f), Vector3.down, out hit, 100f, mask,
            QueryTriggerInteraction.Ignore);
        return ok
            ? "попадание в «" + hit.collider.name + "» на y = " + Num(hit.point.y) +
              " (слой «" + LayerMask.LayerToName(hit.collider.gameObject.layer) + "»)"
            : "ПРОМАХ (маска " + mask + " = " + MaskNames(mask) + " не видит пол!)";
    }

    /// <summary>
    /// Живой луч по префабу: временный инстанс далеко от сцены, луч сверху вниз через центр модели.
    /// Проверяются три маски: маска камеры, DefaultRaycastLayers и «всё».
    /// </summary>
    private static string ProbePrefab(string prefabPath, FreeFlyCameraController cam)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return "префаб не загружается";

        GameObject inst = null;
        try
        {
            inst = UnityEngine.Object.Instantiate(prefab);
            inst.name = "§57 probe";
            inst.transform.position = ProbeOrigin;
            Physics.SyncTransforms();
            // Габарит считаем ПО МЕШАМ: `Renderer.bounds`/`Collider.bounds` у только что созданного
            // объекта бывают ещё не готовы (на этом и спотыкался первый прогон).
            Bounds b = MeshBounds(inst);
            if (b.size.sqrMagnitude < 1e-6f) return "у префаба нет рендереров — мерить нечего";

            Vector3 from = new Vector3(b.center.x, b.max.y + 0.5f, b.center.z);
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> m in new[]
                     {
                         new KeyValuePair<string, int>("маска камеры", SurfaceMaskOf(cam)),
                         new KeyValuePair<string, int>("DefaultRaycastLayers", Physics.DefaultRaycastLayers),
                         new KeyValuePair<string, int>("всё (−1)", ~0)
                     })
            {
                RaycastHit hit;
                bool ok = Physics.Raycast(from, Vector3.down, out hit, 5f, m.Value,
                    QueryTriggerInteraction.Ignore);
                parts.Add(m.Key + ": " + (ok
                    ? "«" + hit.collider.name + "» слоя «" + LayerMask.LayerToName(hit.collider.gameObject.layer) +
                      "» на y = " + Num(hit.point.y)
                    : "ПРОМАХ"));
            }
            return string.Join(" · ", parts);
        }
        finally
        {
            if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    private static int SurfaceMaskOf(FreeFlyCameraController cam)
    {
        if (cam == null) return Physics.DefaultRaycastLayers;
        return cam.surfaceLayer.value != 0 ? cam.surfaceLayer.value : cam.laserLayers.value;
    }

    private static int LayerOf(string layerLabel)
    {
        int space = layerLabel.IndexOf(' ');
        return space > 0 ? int.Parse(layerLabel.Substring(0, space)) : 0;
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
        if (names.Count == 0) return "ничего не выбрано";
        return string.Join(" + ", names);
    }

    private static bool Covers(Bounds outer, Bounds inner)
    {
        const float tol = 0.02f;
        return outer.min.x <= inner.min.x + tol && outer.min.y <= inner.min.y + tol &&
               outer.min.z <= inner.min.z + tol && outer.max.x >= inner.max.x - tol &&
               outer.max.y >= inner.max.y - tol && outer.max.z >= inner.max.z - tol;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static Bounds BoundsOfColliders(Collider[] cols)
    {
        if (cols == null || cols.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
        Bounds b = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
        return b;
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

    private static void WriteReport(string text)
    {
        try { File.WriteAllText(ReportPath, text, new UTF8Encoding(false)); }
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
