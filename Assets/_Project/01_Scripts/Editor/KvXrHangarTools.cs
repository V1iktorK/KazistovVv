using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// ОКРУЖЕНИЕ (АНГАР) В XR-СЦЕНЕ (сессия §35). ИЗМЕРЕНИЕ И ПОСТАНОВКА.
    ///
    /// ЗАЧЕМ. Владелец в шлеме 16.10.2026: «Мир видно, но видно только горизонт, никаких ангаров».
    /// Проверка сборки показала причину: в APK одна сцена `level0` (это `XrMain`), и в ней есть
    /// столы, свет и пол, но **ни одного объекта ангара** — а в десктопной сцене лаборатория стоит
    /// внутри ангара. Поэтому в шлеме видно пол 40×40 м и небо, то есть «горизонт» без помещения.
    ///
    /// ПОЧЕМУ СНАЧАЛА ИЗМЕРЕНИЕ, А ПОТОМ ПОСТАНОВКА. Виртуальная геометрия проекта НЕ метрическая:
    /// в журнале шлема это записано честно — «столешницы 14,4 × 9,6 юнита при высоте 0,98».
    /// То есть у геометрии разный масштаб по осям, и «поставить ангар рядом со стендами» на глаз
    /// нельзя: получится либо модель в километр, либо игрушечная. Сначала измеряем, как ангар
    /// расположен относительно стендов В ДЕСКТОПНОЙ сцене, и только потом переносим теми же
    /// пропорциями.
    ///
    /// ЧТО СЧИТАЕТСЯ ИЗМЕРЕНИЕМ. Для каждой части ангара и для стендов печатается мировой
    /// прямоугольник (bounds) и габариты в юнитах, а также отношение «габарит ангара / габарит
    /// стендов» — именно это отношение переносится в XR-сцену, а не абсолютные числа.
    /// </summary>
    public static class KvXrHangarTools
    {
        private const string MenuRoot = "Tools/KazistovVv XR/";
        private const string DesktopScenePath = "Assets/_Project/00_Scenes/MainScene.unity";
        private const string XrScenePath = "Assets/_Project/00_Scenes/XrMain.unity";
        private const string HangarPrefabFolder = "Assets/ROOMS/HQ Hangar Free/Prefabs";

        /// <summary>
        /// ИМЯ ОБЪЕКТА АНГАРА. Проверка ДОЛЖНА принимать плиты пола с суффиксом: в сцене лежат
        /// `Floor`, `Floor (1)` … `Floor (15)` — 16 плит. Прежнее правило требовало ТОЧНОГО
        /// равенства `name == "Floor"` и потому видело ОДНУ плиту из шестнадцати: габарит зала
        /// выходил 37,77 м по X вместо настоящих 40,18 м, потому что плиты, доходящие до x = 31,
        /// в измерение не попадали. Число «37,77» попало бы в отчёт как доказательство — и было бы
        /// неверным. Ошибка была найдена сравнением с габаритом, который дал инструмент постановки
        /// (он ставит все 32 объекта и видит 40,18 м).
        /// </summary>
        private static bool IsHangarName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("Hangar_part")) return true;
            if (name.StartsWith("HQ Hangar") || name.StartsWith("ROOMS")) return true;
            return name == "Floor" || name.StartsWith("Floor (");
        }

        /// <summary>
        /// ИЗМЕРИТЬ ОКРУЖЕНИЕ ДЕСКТОПНОЙ СЦЕНЫ: что там за ангар, где он стоит и какой он величины
        /// относительно стендов. Ничего не меняет — только читает и печатает.
        /// </summary>
        [MenuItem(MenuRoot + "8. Ангар: измерить окружение десктопной сцены", false, 80)]
        public static void MeasureDesktopEnvironment()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvHangar] === ИЗМЕРЕНИЕ ОКРУЖЕНИЯ ДЕСКТОПНОЙ СЦЕНЫ ===");

            Scene scene = EditorSceneManager.OpenScene(DesktopScenePath, OpenSceneMode.Single);
            sb.AppendLine("  сцена: " + scene.name + " · объектов в корне: " + scene.rootCount);

            Bounds hangarBounds = new Bounds();
            bool hangarFound = false;
            int hangarCount = 0;
            var hangarRoots = new List<string>();

            Bounds standBounds = new Bounds();
            bool standsFound = false;
            var standLines = new List<string>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null) continue;
                    string name = t.name;

                    bool isHangar = IsHangarName(name);
                    if (isHangar && t.parent == null || (isHangar && t.parent != null && !t.parent.name.StartsWith("Hangar_part")))
                    {
                        hangarCount++;
                        hangarRoots.Add(name + " @ " + t.position.ToString("0.00") +
                                        " · масштаб " + t.lossyScale.ToString("0.000"));
                    }

                    if (isHangar)
                    {
                        // ГАБАРИТ СЧИТАЕТСЯ ПО ВСЕМУ ПОДДЕРЕВУ, А НЕ ПО СОБСТВЕННОМУ РЕНДЕРЕРУ.
                        // Прежняя версия брала `t.GetComponent<Renderer>()` — рендерер САМОГО
                        // объекта. У экземпляров префабов меши лежат на ДОЧЕРНИХ объектах, поэтому
                        // часть зала в габарит не попадала: по X выходило 37,77 м вместо настоящих
                        // 40,18 м (плиты пола идут от x = −9 до x = 31). Объединение по поддереву
                        // даёт верный размер, а повторное добавление одних и тех же рендереров
                        // результат не меняет — `Bounds.Encapsulate` берёт объединение.
                        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r == null) continue;
                            if (!hangarFound) { hangarBounds = r.bounds; hangarFound = true; }
                            else hangarBounds.Encapsulate(r.bounds);
                        }
                    }

                    bool isStand = name == "Стенд_1_Стол" || name == "Стенд_2_Стол";
                    if (isStand)
                    {
                        standLines.Add(name + " @ " + t.position.ToString("0.00") +
                                       " · масштаб " + t.lossyScale.ToString("0.000"));
                        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r == null) continue;
                            if (!standsFound) { standBounds = r.bounds; standsFound = true; }
                            else standBounds.Encapsulate(r.bounds);
                        }
                    }
                }
            }

            sb.AppendLine("  объектов ангара (корней): " + hangarCount);
            foreach (string line in hangarRoots) sb.AppendLine("     " + line);
            if (hangarFound)
                sb.AppendLine("  ГАБАРИТы АНГАРА: " + hangarBounds.size.ToString("0.00") +
                              " · центр " + hangarBounds.center.ToString("0.00"));
            else
                sb.AppendLine("  ангар в десктопной сцене НЕ НАЙДЕН по именам Hangar_part*/Floor");

            foreach (string line in standLines) sb.AppendLine("     стенд: " + line);
            if (standsFound)
                sb.AppendLine("  ГАБАРИТЫ СТЕНДОВ: " + standBounds.size.ToString("0.00") +
                              " · центр " + standBounds.center.ToString("0.00"));

            if (hangarFound && standsFound)
            {
                Vector3 h = hangarBounds.size;
                Vector3 s = standBounds.size;
                sb.AppendLine("  ОТНОШЕНИЕ (ангар / стенды): " +
                              (h.x / Mathf.Max(0.0001f, s.x)).ToString("0.00") + " × " +
                              (h.z / Mathf.Max(0.0001f, s.z)).ToString("0.00") + " × " +
                              (h.y / Mathf.Max(0.0001f, s.y)).ToString("0.00"));
                sb.AppendLine("  СМЕЩЕНИЕ центра ангара от центра стендов: " +
                              (hangarBounds.center - standBounds.center).ToString("0.00"));
            }

            // Что лежит в папке префабов: из чего собирать окружение в XR-сцене.
            sb.AppendLine("  префабы окружения в " + HangarPrefabFolder + ":");
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { HangarPrefabFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                string size = "—";
                if (prefab != null)
                {
                    Renderer[] rs = prefab.GetComponentsInChildren<Renderer>(true);
                    if (rs != null && rs.Length > 0)
                    {
                        Bounds b = rs[0].bounds;
                        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                        size = b.size.ToString("0.00");
                    }
                }
                sb.AppendLine("     " + System.IO.Path.GetFileName(path) + " · габарит " + size);
            }

            Debug.Log(sb.ToString());
        }
        /// <summary>
        /// ДИАГНОСТИКА ПУСТЫХ СЛОТОВ МАТЕРИАЛОВ (§35). Нужна потому, что тест проекта
        /// `KvRenderPipelineTests.XrScene_MaterialsAreUrpCompatibleWithoutMissingLinks` нашёл
        /// «пропавшие ссылки на материал» у частей ангара в XR-сцене: пустой слот материала
        /// под URP рисуется РОЗОВЫМ, а это прямо запрещено приёмкой. Здесь печатается ТОЧНО,
        /// у какого объекта, в каком слоте и сколько подмешей у меша — и то же самое по
        /// десктопной сцене, чтобы сразу видеть, унаследован дефект от префаба или внесён постановкой.
        /// </summary>
        public static void DiagnoseHangarMaterialSlots()
        {
            DiagnoseScene(XrScenePath, "[KvHangar] XR-СЦЕНА");
            DiagnoseScene(DesktopScenePath, "[KvHangar] ДЕСКТОПНАЯ СЦЕНА");
        }

        private static void DiagnoseScene(string scenePath, string title)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title + " (" + scenePath + ")");

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int renderers = 0, emptySlots = 0, hangarRenderers = 0, hangarEmpty = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    renderers++;
                    bool inHangar = IsUnderHangar(r.transform);
                    if (inHangar) hangarRenderers++;

                    Material[] mats = r.sharedMaterials;
                    if (mats == null) continue;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] != null) continue;
                        emptySlots++;
                        if (inHangar) hangarEmpty++;
                        MeshFilter mf = r.GetComponent<MeshFilter>();
                        int subMeshes = mf != null && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : -1;
                        sb.AppendLine("   ПУСТО: " + PathOf(r.transform) +
                                      " · слот " + i + " из " + mats.Length +
                                      " · подмешей " + subMeshes +
                                      " · меш " + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "нет"));
                    }
                }
            }

            // ПОСТАТЕЙНАЯ ВЕДОМОСТЬ МАТЕРИАЛОВ АНГАРА: имена слотов по каждому рендереру.
            // Нужна, чтобы сравнить десктопную сцену и XR-сцену построчно и увидеть, какой
            // ИМЕННО материал потерялся при постановке, а не гадать.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || !IsUnderHangar(r.transform)) continue;
                    Material[] mats = r.sharedMaterials;
                    var slots = new List<string>();
                    if (mats != null)
                        for (int i = 0; i < mats.Length; i++)
                            slots.Add(mats[i] != null ? mats[i].name : "<ПУСТО>");
                    sb.AppendLine("   " + PathOf(r.transform) + " → " + string.Join(" | ", slots.ToArray()));
                }
            }

            sb.AppendLine("   всего рендереров " + renderers + " · пустых слотов " + emptySlots +
                          " · из них в ангаре: рендереров " + hangarRenderers + ", пустых слотов " + hangarEmpty);
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// ПОЧЕМУ В ЗАЛЕ НЕ ВИДНО СТОЛОВ (§35). Владелец 16.10.2026: «вижу помещение — стены,
        /// потолок, пол», но «столов или роботов не видно», хотя журнал шлема говорит, что стенды
        /// поставлены на (−1,5; 0; 3,2) и (1,5; 0; 3,2) и зал их содержит.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ ЧИСЛОМ. Оператор стоит в начале координат. Если он оказался ВНУТРИ
        /// объёма части ангара (части стоят на сетке 5 м и в двух рядах по 20,31 м глубиной,
        /// а стенды стоят ровно там же, где на компьютере — тоже внутри ряда), то столы закрыты
        /// геометрией зала, и «не видно» — это не «не поставлено», а «загорожено».
        /// Здесь печатается, какие части накрывают оператора и какие накрывают стенды.
        /// </summary>
        public static void DiagnoseHallOcclusion()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvHangar] === ПРОВЕРКА: НЕ ЗАГОРОЖЕНЫ ЛИ СТОЛЫ ГЕОМЕТРИЕЙ ЗАЛА ===");

            Scene scene = EditorSceneManager.OpenScene(XrScenePath, OpenSceneMode.Single);

            var parts = new List<Renderer>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    if (r != null && IsUnderHangar(r.transform) && r.name.StartsWith("Hangar_part"))
                        parts.Add(r);

            // Часть зала = рендерер с мешом; для наглядности считаем по объектам-частям верхнего уровня.
            var seen = new HashSet<string>();
            var topParts = new List<Renderer>();
            foreach (Renderer r in parts)
            {
                string top = TopHangarName(r.transform);
                if (top != null && seen.Add(top)) topParts.Add(r);
            }

            sb.AppendLine("  частей зала верхнего уровня: " + topParts.Count);

            // Точки, которые надо проверить: оператор и центры двух стендов.
            var points = new List<KeyValuePair<string, Vector3>>
            {
                new KeyValuePair<string, Vector3>("оператор (голова)", new Vector3(0f, 1.60f, 0f)),
                new KeyValuePair<string, Vector3>("оператор (ноги)", new Vector3(0f, 0.05f, 0f)),
                new KeyValuePair<string, Vector3>("стенд 1 (столешница)", new Vector3(-1.5f, 0.98f, 3.2f)),
                new KeyValuePair<string, Vector3>("стенд 2 (столешница)", new Vector3(1.5f, 0.98f, 3.2f))
            };

            foreach (var p in points)
            {
                var inside = new List<string>();
                var nearest = float.MaxValue;
                string nearestName = "—";
                foreach (Renderer r in topParts)
                {
                    Bounds b = r.bounds;
                    bool within = b.Contains(p.Value);
                    if (within) inside.Add(TopHangarName(r.transform) + " (" + b.size.ToString("0.0") + ")");
                    float d = Vector3.Distance(b.ClosestPoint(p.Value), p.Value);
                    if (d < nearest) { nearest = d; nearestName = TopHangarName(r.transform); }
                }
                sb.AppendLine("   " + p.Key + " " + p.Value.ToString("0.00") +
                              " · внутри частей: " + (inside.Count == 0 ? "нет" : string.Join(", ", inside.ToArray())) +
                              " · ближайшая часть «" + nearestName + "» в " + nearest.ToString("0.00") + " м");
            }

            // Свободен ли коридор вперёд от оператора до стендов.
            sb.AppendLine("  ЛУЧ ОТ ОПЕРАТОРА К СТЕНДАМ: что стоит на пути (x = 0, z от 0 до 6):");
            for (float z = 0.5f; z <= 6f; z += 0.5f)
            {
                var blockers = new List<string>();
                Vector3 pt = new Vector3(0f, 1.20f, z);
                foreach (Renderer r in topParts)
                    if (r.bounds.Contains(pt)) blockers.Add(TopHangarName(r.transform));
                sb.AppendLine("     z = " + z.ToString("0.0") + " м · 1,20 м над полом · " +
                              (blockers.Count == 0 ? "свободно" : "ЗАГОРОЖЕНО: " + string.Join(", ", blockers.ToArray())));
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>Имя части зала верхнего уровня (без хвоста «/Hangar_part_02» и суффиксов).</summary>
        private static string TopHangarName(Transform t)
        {
            Transform current = t;
            int guard = 0;
            while (current != null && guard++ < 64)
            {
                if (current.name.StartsWith("Hangar_part")) return current.name;
                current = current.parent;
            }
            return null;
        }

        /// <summary>
        /// ПОЧЕМУ ОПЕРАТОР НЕ ВИДИТ СТОЛОВ (§35): ГДЕ НА САМОМ ДЕЛЕ СТОЛЫ И РОБОТЫ.
        ///
        /// ГИПОТЕЗА, КОТОРУЮ ЭТО ПРОВЕРЯЕТ. `KvXrSceneContent` ставит стенды на 3,2 м впереди
        /// оператора. Но столешница проекта — 14,4 × 9,6 м, то есть половина ГЛУБИНЫ стола равна
        /// 4,8 м. Значит при расстоянии 3,2 м БЛИЖНЯЯ КРОМКА стола оказывается на 1,6 м ПОЗАДИ
        /// оператора: он стоит ВНУТРИ обоих столов, а не перед ними. Это ровно то, чего владелец
        /// не должен видеть: «я не внутри робота» ([S2.3]).
        ///
        /// ВТОРАЯ ГИПОТЕЗА. Компонент MR-постановки (`KvXrMrRoom`) в журнале шлема отчитался:
        /// «Робот поставлен на реальный стол: центр (0,00; 0,00) м, низ 0,78 м». То есть робота
        /// перенесли в НАЧАЛО КООРДИНАТ — туда, где стоит оператор. Робот, внутри которого стоишь,
        /// не виден: изнутри модели видны только обратные стороны граней.
        /// </summary>
        public static void DiagnoseOperatorVsStands()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvHangar] === ГДЕ СТОЛЫ И РОБОТЫ ОТНОСИТЕЛЬНО ОПЕРАТОРА ===");

            Scene scene = EditorSceneManager.OpenScene(XrScenePath, OpenSceneMode.Single);

            // Как KvXrSceneContent расставляет стенды: впереди оператора на standDistance,
            // влево и вправо на половину standSpacing.
            var content = Object.FindAnyObjectByType<KazistovVvXr.KvXrSceneContent>(FindObjectsInactive.Include);
            float distance = content != null ? content.standDistance : 3.2f;
            float spacing = content != null ? content.standSpacing : 3.0f;
            sb.AppendLine("  KvXrSceneContent: расстояние до стендов " + distance.ToString("0.00") +
                          " м · между центрами " + spacing.ToString("0.00") + " м · столешница " +
                          TrajectoryCore.StandBuilder.TopHeight.ToString("0.00") + " м");
            sb.AppendLine("  габариты столешницы проекта: ширина " + TrajectoryCore.StandBuilder.TableWidth.ToString("0.00") +
                          " м · глубина " + TrajectoryCore.StandBuilder.TableDepth.ToString("0.00") +
                          " м → половина глубины " + (TrajectoryCore.StandBuilder.TableDepth * 0.5f).ToString("0.00") + " м");

            float halfDepth = TrajectoryCore.StandBuilder.TableDepth * 0.5f;
            sb.AppendLine("  ВЫВОД О РАССТОЯНИИ: ближняя кромка стола при расстоянии " + distance.ToString("0.00") +
                          " м окажется на " + (distance - halfDepth).ToString("0.00") +
                          " м ОТ оператора (отрицательное число = стол ПОЗАДИ него, оператор ВНУТРИ стола)");

            // Где на самом деле стоят столы и роботы в сцене (то, что впечено в XrMain).
            sb.AppendLine("  ФАКТИЧЕСКИ В СЦЕНЕ XrMain:");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null) continue;
                    if (t.name != TrajectoryCore.StandBuilder.Stand1Name &&
                        t.name != TrajectoryCore.StandBuilder.Stand2Name &&
                        t.name != TrajectoryCore.StandBuilder.Robot1CopyName &&
                        t.name != TrajectoryCore.StandBuilder.Robot2CopyName) continue;

                    Bounds b = new Bounds(t.position, Vector3.zero);
                    bool any = false;
                    foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null) continue;
                        if (!any) { b = r.bounds; any = true; }
                        else b.Encapsulate(r.bounds);
                    }
                    sb.AppendLine("     " + t.name + " · позиция " + t.position.ToString("0.00") +
                                  " · габарит " + (any ? b.size.ToString("0.00") : "нет рендереров") +
                                  " · низ " + (any ? b.min.y.ToString("0.00") : "—") + " м" +
                                  (any && b.Contains(new Vector3(0f, 1.60f, 0f)) ? "  ← ОПЕРАТОР ВНУТРИ" : ""));
                }
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>Объект находится внутри ангара (по имени корня или по имени предка).</summary>
        private static bool IsUnderHangar(Transform t)
        {
            Transform current = t;
            int guard = 0;
            while (current != null && guard++ < 64)
            {
                if (current.name == "XrHangar" || current.name == "Level") return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>Путь объекта в иерархии — чтобы дефект можно было найти, а не угадывать.</summary>
        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            Transform current = t.parent;
            int guard = 0;
            while (current != null && guard++ < 32)
            {
                sb.Insert(0, current.name + "/");
                current = current.parent;
            }
            return sb.ToString();
        }
    }
}
