using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KazistovVvXr;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// ПОСТАНОВКА АНГАРА В XR-СЦЕНУ (сессия §35, карточка Б17). РЕДАКТОРНЫЙ ИНСТРУМЕНТ.
    ///
    /// ЗАЧЕМ. Владелец в шлеме 16.10.2026: «видно только горизонт, никакого помещения нет».
    /// Проверка сборки объяснила причину: в APK одна сцена (`XrMain`), и в ней есть столы, свет
    /// и пол, но **ни одного объекта ангара** — хотя в десктопной сцене лаборатория стоит ВНУТРИ
    /// ангара. Этот инструмент переносит зал в XR-сцену.
    ///
    /// ЧТО ИМЕННО ДЕЛАЕТСЯ, ПО ШАГАМ:
    ///   1. ЧИТАЕТ десктопную сцену, чтобы узнать, из чего собран зал и как части стоят друг
    ///      относительно друга. Сцена открывается ДОПОЛНИТЕЛЬНОЙ и закрывается БЕЗ СОХРАНЕНИЯ —
    ///      `MainScene.unity` не меняется (запрет [S3.1]), что доказывается SHA256 до и после.
    ///   2. Ставит в `XrMain` экземпляры ТЕХ ЖЕ префабов из `Assets/ROOMS/HQ Hangar Free/Prefabs`.
    ///      Не копированием объектов из чужой сцены: прежнее решение [D6] это запрещает — вышли бы
    ///      скрытые межсценные ссылки, которые разъезжаются при первой правке сцены.
    ///   3. ЗАМЕНЯЕТ МАТЕРИАЛЫ на копии под URP. Это обязательно, а не «на всякий случай»:
    ///      материалы ангара — `HDRP/Lit`, а уровень качества для Android — `Mobile`, то есть URP.
    ///      Без замены зал был бы РОЗОВЫМ, что прямо запрещено приёмкой владельца. Общие `.mat`
    ///      при этом НЕ трогаются: на них работает десктоп (HDRP), и их правка сломала бы его ([S3.2]).
    ///   4. Кладёт на сцену компонент `KvXrHangarAnchor`, который во время работы ставит зал
    ///      по фактическому положению стендов и пишет строку журнала.
    ///
    /// ПОЧЕМУ КАНАЛЫ КАРТЫ МАСКИ НЕ ПЕРЕСЧИТЫВАЮТСЯ. У HDRP карта маски упакована как
    /// R — металличность, G — затенение, B — детали, A — гладкость. У URP нужно ровно то же:
    /// `_MetallicGlossMap` читает металличность из R и гладкость из A, а `_OcclusionMap` —
    /// затенение из G. Раскладка совпадает ПОБАЙТОВО, поэтому одна и та же текстура годится
    /// на обе карты, и никакой пересчёт пикселей не нужен (это и проверяет
    /// `KvXrHangarPlacementTests.MaterialMask_ChannelsAreMappedCorrectly`).
    /// </summary>
    public static class KvXrHangarBuilder
    {
        private const string MenuRoot = "Tools/KazistovVv XR/";
        private const string DesktopScenePath = "Assets/_Project/00_Scenes/MainScene.unity";
        private const string XrScenePath = "Assets/_Project/00_Scenes/XrMain.unity";
        private const string UrpMaterialFolder = "Assets/_Project/04_Materials/XrHangar";

        /// <summary>Имя корня зала в XR-сцене. По нему его находит `KvXrHangarAnchor`.</summary>
        public const string HangarRootName = "XrHangar";

        /// <summary>Часть ангара: что поставить и как оно стояло относительно корня зала.</summary>
        private class Part
        {
            public GameObject prefab;          // исходный префаб; null — обычный объект сцены
            public bool fromPrefab;            // объект пришёл из префаба и создаётся вместе с ним
            public string name;
            public string path;                // путь от корня зала — по нему находится пара в XR-сцене
            public Material[] materials;       // материалы ТАК, КАК ОНИ НАЗНАЧЕНЫ В ДЕСКТОПНОЙ СЦЕНЕ
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
            public readonly List<Part> children = new List<Part>();
        }

        // ==================================================== ГЛАВНАЯ КОМАНДА

        [MenuItem(MenuRoot + "9. Ангар: поставить в XR-сцену (материалы под URP)", false, 81)]
        public static void BuildHangarInXrScene()
        {
            var log = new StringBuilder();
            log.AppendLine("[KvHangar] === ПОСТАНОВКА АНГАРА В XR-СЦЕНУ ===");

            // --- 1. ЧТЕНИЕ ДЕСКТОПНОЙ СЦЕНЫ (только чтение, без сохранения)
            Part layout;
            int desktopParts;
            if (!TryReadDesktopLayout(out layout, out desktopParts, log))
            {
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine("  прочитано из десктопной сцены узлов дерева зала: " + desktopParts);

            // --- 2. ОТКРЫТИЕ XR-СЦЕНЫ
            Scene xr = EditorSceneManager.OpenScene(XrScenePath, OpenSceneMode.Single);

            RemoveExistingHangar(log);

            // --- 3. ПОСТАНОВКА ГЕОМЕТРИИ
            var rootGo = new GameObject(HangarRootName);
            SceneManager.MoveGameObjectToScene(rootGo, xr);
            int created = 0;
            InstantiateParts(layout, rootGo.transform, xr, ref created);
            log.AppendLine("  поставлено объектов ангара: " + created);

            // НОМИНАЛЬНОЕ ПОЛОЖЕНИЕ — сразу правильное для обычного старта. Во время работы корень
            // переставит `KvXrHangarAnchor` по фактическому положению стендов, но сцена обязана быть
            // осмысленной и сама по себе: если якорь почему-то не сработает, зал всё равно окажется
            // вокруг оператора, а не в стороне.
            Vector3 nominalStandsCenter = new Vector3(0f, 1.30f, 3.20f);
            rootGo.transform.position = KvHangarPlacement.RootPosition(nominalStandsCenter);
            log.AppendLine("  номинальное положение корня: " + rootGo.transform.position.ToString("0.00") +
                           " (по центру стендов " + nominalStandsCenter.ToString("0.00") + ")");

            // --- 4. МАТЕРИАЛЫ ПОД URP
            var renderers = rootGo.GetComponentsInChildren<Renderer>(true);
            var copies = new Dictionary<Material, Material>();
            int remapped = 0;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material src = mats[i];
                    if (src == null) continue;
                    Material dst;
                    if (!copies.TryGetValue(src, out dst))
                    {
                        dst = CreateUrpCopy(src, log);
                        copies[src] = dst;
                    }
                    if (dst != null && !ReferenceEquals(dst, src))
                    {
                        mats[i] = dst;
                        changed = true;
                        remapped++;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
            log.AppendLine("  материалов заменено на URP-копии: " + remapped +
                           " · уникальных исходных материалов " + copies.Count);

            // --- 4б. СВЕРКА МАТЕРИАЛОВ С ДЕСКТОПНОЙ СЦЕНОЙ (обязательный шаг, а не «на всякий случай»)
            int restored = ApplyDesktopMaterials(layout, rootGo.transform, copies, log);
            log.AppendLine("  материалов сверено с десктопной сценой: " + restored + " рендереров");

            // Пустой слот материала под URP рисуется РОЗОВЫМ. Если после сверки пустые слоты
            // всё же остались, это не «мелочь»: говорим в журнал как об ошибке и считаем их,
            // чтобы приёмка «ни одной розовой поверхности» опиралась на число, а не на заверение.
            int emptySlots = CountEmptyMaterialSlots(rootGo);
            if (emptySlots > 0)
                Debug.LogError("[KvHangar] ВНИМАНИЕ: пустых слотов материалов в зале " + emptySlots +
                               " — эти поверхности будут РОЗОВЫМИ в шлеме.");
            else
                log.AppendLine("  пустых слотов материалов: 0 (розовых поверхностей быть не должно)");

            // Габарит того, что получилось, — сразу видно, если поставлено не то.
            Bounds bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (any)
                log.AppendLine("  габарит поставленного зала: " + bounds.size.ToString("0.00") +
                               " · центр " + bounds.center.ToString("0.00"));

            // --- 5. КОМПОНЕНТ, КОТОРЫЙ ПОСТАВИТ ЗАЛ ПО СТЕНДАМ ВО ВРЕМЯ РАБОТЫ
            EnsureAnchorComponent(rootGo);

            // --- 6. СОХРАНЕНИЕ
            EditorSceneManager.MarkSceneDirty(xr);
            EditorSceneManager.SaveScene(xr);
            AssetDatabase.SaveAssets();
            log.AppendLine("  XrMain сохранена: " + XrScenePath);

            Debug.Log(log.ToString());
        }

        // ==================================================== ЧТЕНИЕ ДЕСКТОПНОЙ СЦЕНЫ

        /// <summary>
        /// ПРОЧИТАТЬ, ИЗ ЧЕГО СОБРАН ЗАЛ, И ЗАКРЫТЬ СЦЕНУ БЕЗ СОХРАНЕНИЯ.
        /// Сцена открывается ДОПОЛНИТЕЛЬНОЙ, поэтому уже открытая работа не теряется.
        /// </summary>
        private static bool TryReadDesktopLayout(out Part layout, out int count, StringBuilder log)
        {
            layout = null;
            count = 0;

            bool openedByUs = false;
            Scene desktop = SceneManager.GetSceneByPath(DesktopScenePath);
            if (!desktop.IsValid() || !desktop.isLoaded)
            {
                desktop = EditorSceneManager.OpenScene(DesktopScenePath, OpenSceneMode.Additive);
                openedByUs = true;
            }
            if (!desktop.IsValid() || !desktop.isLoaded)
            {
                log.AppendLine("  ОШИБКА: не удалось открыть " + DesktopScenePath);
                return false;
            }

            try
            {
                var parts = new List<Transform>();
                foreach (GameObject sceneRoot in desktop.GetRootGameObjects())
                {
                    foreach (Transform t in sceneRoot.GetComponentsInChildren<Transform>(true))
                        if (t != null && IsHangarName(t.name)) parts.Add(t);
                }

                if (parts.Count == 0)
                {
                    log.AppendLine("  ОШИБКА: в десктопной сцене не найдено ни одного объекта ангара");
                    return false;
                }

                Transform common = FindCommonRoot(parts);
                if (common == null)
                {
                    log.AppendLine("  ОШИБКА: у объектов ангара нет общего корня");
                    return false;
                }
                log.AppendLine("  корень ангара на компьютере: «" + common.name + "» · " +
                               common.position.ToString("0.00") + " · масштаб " + common.lossyScale.ToString("0.000"));

                layout = ReadHierarchy(common, "", ref count, true);
                return layout != null;
            }
            finally
            {
                // ЗАКРЫТИЕ БЕЗ СОХРАНЕНИЯ: требование [S3.1] — десктопная сцена не меняется.
                if (openedByUs && desktop.IsValid()) EditorSceneManager.CloseScene(desktop, true);
            }
        }

        /// <summary>Имя объекта ангара: части зала и плиты пола (с учётом суффиксов «(1)», «(2)»).</summary>
        private static bool IsHangarName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith("Hangar_part") || name == "Floor" || name.StartsWith("Floor ");
        }

        /// <summary>Самый глубокий общий предок всех частей — это и есть корень зала.</summary>
        private static Transform FindCommonRoot(List<Transform> parts)
        {
            Transform candidate = parts[0];
            while (candidate != null)
            {
                bool all = true;
                foreach (Transform t in parts)
                {
                    if (t == candidate || t.IsChildOf(candidate)) continue;
                    all = false;
                    break;
                }
                if (all) return candidate;
                candidate = candidate.parent;
            }
            return null;
        }

        /// <summary>
        /// Прочитать поддерево: что за префаб, как стоял и КАКИЕ МАТЕРИАЛЫ НАЗНАЧЕНЫ.
        ///
        /// ПУТЬ СЧИТАЕТСЯ ОТ КОРНЯ ЗАЛА, А НЕ ОТ ЕГО ИМЕНИ. Первая версия включала в путь имя
        /// корневого объекта десктопной сцены («Level/…»), и сверка материалов не находила ни
        /// одного объекта — в XR-сцене корень называется `XrHangar`. Поэтому у корня путь пустой,
        /// а у его детей — просто их имена.
        /// </summary>
        private static Part ReadHierarchy(Transform source, string parentPath, ref int count, bool isRoot = false)
        {
            string path = isRoot
                ? ""
                : (string.IsNullOrEmpty(parentPath) ? source.name : parentPath + "/" + source.name);

            var part = new Part
            {
                name = source.name,
                path = path,
                localPosition = source.localPosition,
                localRotation = source.localRotation,
                localScale = source.localScale,
                // Принадлежит ли объект префабу: нужно, чтобы НЕ воссоздавать повторно то,
                // что уже появится вместе с экземпляром префаба-предка.
                fromPrefab = PrefabUtility.GetCorrespondingObjectFromSource(source.gameObject) != null
            };

            // МАТЕРИАЛЫ ЗАПОМИНАЮТСЯ С ЖИВОГО ДЕСКТОПНОГО ОБЪЕКТА. Это обязательно: у части
            // `Hangar_part_02` третий материал в префабе ссылается на подобъект (`type: 3`),
            // и при постановке из префаба этот слот оказывается ПУСТЫМ, хотя в десктопной сцене
            // там лежит `Glass`. Пустой слот материала под URP рисуется РОЗОВЫМ — то есть
            // владелец увидел бы розовую стену, а приёмка запрещает розовые поверхности.
            // Запоминание живой раскладки убирает саму возможность такого расхождения.
            Renderer own = source.GetComponent<Renderer>();
            if (own != null) part.materials = own.sharedMaterials;

            // Корень экземпляра префаба запоминается как источник: в XR-сцену встанет экземпляр
            // ТОГО ЖЕ префаба, а не копия объекта из чужой сцены (запрет прежнего решения [D6]).
            if (PrefabUtility.IsAnyPrefabInstanceRoot(source.gameObject))
                part.prefab = PrefabUtility.GetCorrespondingObjectFromSource(source.gameObject) as GameObject;

            count++;

            foreach (Transform child in source)
            {
                if (child == null) continue;
                part.children.Add(ReadHierarchy(child, path, ref count));
            }
            return part;
        }

        // ==================================================== ПОСТАНОВКА

        private static void RemoveExistingHangar(StringBuilder log)
        {
            GameObject existing = GameObject.Find(HangarRootName);
            if (existing == null) return;
            Object.DestroyImmediate(existing);
            log.AppendLine("  прежний зал «" + HangarRootName + "» убран (повторный запуск безопасен)");
        }

        /// <summary>
        /// ПОСТАВИТЬ ЧАСТИ ЗАЛА. Каждая часть, бывшая в десктопной сцене экземпляром префаба,
        /// ставится экземпляром ТОГО ЖЕ префаба; обычные объекты воссоздаются пустыми.
        /// </summary>
        private static void InstantiateParts(Part part, Transform parent, Scene scene, ref int created)
        {
            foreach (Part child in part.children)
            {
                GameObject instance = null;

                if (child.prefab != null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(child.prefab, scene);
                }
                else if (child.fromPrefab)
                {
                    // ЭТУ ЧАСТЬ УЖЕ СОЗДАЛ ПРЕФАБ-ПРЕДОК. Воссоздавать её вручную значило бы
                    // получить удвоенные стены и вдвое больше полигонов без единой ошибки в журнале.
                    continue;
                }
                else
                {
                    instance = new GameObject(child.name);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                }

                if (instance == null) continue;

                // Имя сохраняется как в десктопной сцене (вместе с суффиксами «(1)», «(2)»):
                // по нему владелец и я узнаём части зала в журнале и в файле уровня.
                instance.name = child.name;
                instance.transform.SetParent(parent, false);
                instance.transform.localPosition = child.localPosition;
                instance.transform.localRotation = child.localRotation;
                instance.transform.localScale = child.localScale;
                created++;

                InstantiateParts(child, instance.transform, scene, ref created);
            }
        }

        // ==================================================== МАТЕРИАЛЫ ПОД URP

        /// <summary>
        /// СОЗДАТЬ (ИЛИ ОБНОВИТЬ) КОПИЮ МАТЕРИАЛА ПОД URP. Исходные материалы ангара не трогаются:
        /// они нужны десктопу, который работает на HDRP.
        /// </summary>
        private static Material CreateUrpCopy(Material source, StringBuilder log)
        {
            Shader urpLit = Shader.Find(KvHdrpToUrpMaterial.UrpLitShader);
            if (urpLit == null)
            {
                Debug.LogError("[KvHangar] Не найден шейдер «" + KvHdrpToUrpMaterial.UrpLitShader +
                               "» — зал в шлеме будет розовым. Проверьте пакет Universal RP.");
                return source;
            }

            EnsureFolder(UrpMaterialFolder);
            string path = UrpMaterialFolder + "/" + Sanitize(source.name) + "_URP.mat";

            Material dst = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = dst == null;
            if (isNew)
            {
                dst = new Material(urpLit);
                AssetDatabase.CreateAsset(dst, path);
            }
            else
            {
                dst.shader = urpLit;
            }

            CopyProperties(source, dst);
            EditorUtility.SetDirty(dst);

            log.AppendLine("     " + KvHdrpToUrpMaterial.Describe(source.name,
                dst.GetTexture(KvHdrpToUrpMaterial.UrpBaseMap) != null,
                dst.GetTexture(KvHdrpToUrpMaterial.UrpBumpMap) != null,
                dst.GetTexture(KvHdrpToUrpMaterial.UrpMetallicGlossMap) != null,
                dst.GetFloat(KvHdrpToUrpMaterial.Metallic),
                dst.GetFloat(KvHdrpToUrpMaterial.Smoothness)) + (isNew ? " · создан" : " · обновлён"));

            return dst;
        }

        /// <summary>Переложить свойства из материала HDRP в материал URP.</summary>
        private static void CopyProperties(Material src, Material dst)
        {
            // --- цвет
            Color baseColor = src.HasProperty(KvHdrpToUrpMaterial.BaseColor)
                ? src.GetColor(KvHdrpToUrpMaterial.BaseColor)
                : (src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
            dst.SetColor(KvHdrpToUrpMaterial.BaseColor, baseColor);

            // --- текстуры: имена свойств у конвейеров разные, поэтому переносим явно
            CopyTexture(src, dst, KvHdrpToUrpMaterial.HdrpBaseColorMap, KvHdrpToUrpMaterial.UrpBaseMap);
            CopyTexture(src, dst, KvHdrpToUrpMaterial.HdrpNormalMap, KvHdrpToUrpMaterial.UrpBumpMap);
            // Карта маски HDRP годится и как карта «металличность+гладкость» URP (R→R, A→A),
            // и как карта затенения URP (G→G) — раскладка совпадает, пересчёт не нужен.
            CopyTexture(src, dst, KvHdrpToUrpMaterial.HdrpMaskMap, KvHdrpToUrpMaterial.UrpMetallicGlossMap);
            CopyTexture(src, dst, KvHdrpToUrpMaterial.HdrpMaskMap, KvHdrpToUrpMaterial.UrpOcclusionMap);

            // --- скаляры
            dst.SetFloat(KvHdrpToUrpMaterial.Metallic, src.HasProperty(KvHdrpToUrpMaterial.Metallic)
                ? src.GetFloat(KvHdrpToUrpMaterial.Metallic) : 0f);
            dst.SetFloat(KvHdrpToUrpMaterial.Smoothness, src.HasProperty(KvHdrpToUrpMaterial.Smoothness)
                ? src.GetFloat(KvHdrpToUrpMaterial.Smoothness) : 0.5f);
            dst.SetFloat("_SmoothnessTextureChannel", 0f);   // гладкость берётся из альфы карты
            dst.SetFloat("_BumpScale", src.HasProperty("_NormalScale") ? src.GetFloat("_NormalScale") : 1f);

            // --- включения карт: без ключевых слов URP карты игнорирует, и вид теряется молча
            SetKeyword(dst, "_METALLICGLOSSMAP", dst.GetTexture(KvHdrpToUrpMaterial.UrpMetallicGlossMap) != null);
            SetKeyword(dst, "_OCCLUSIONMAP", dst.GetTexture(KvHdrpToUrpMaterial.UrpOcclusionMap) != null);
            SetKeyword(dst, "_NORMALMAP", dst.GetTexture(KvHdrpToUrpMaterial.UrpBumpMap) != null);

            ApplyTransparency(src, dst);
        }

        /// <summary>
        /// ПРОЗРАЧНОСТЬ. У HDRP она задаётся `_SurfaceType` = 1, у URP — `_Surface` = 1 плюс
        /// режим смешивания и выключенная запись в глубину. Стекло ангара без этого стало бы
        /// непрозрачной заливкой и закрыло бы вид.
        /// </summary>
        private static void ApplyTransparency(Material src, Material dst)
        {
            bool transparent = src.HasProperty("_SurfaceType") && src.GetFloat("_SurfaceType") > 0.5f;
            if (!transparent) return;

            dst.SetFloat("_Surface", 1f);
            dst.SetFloat("_Blend", 0f);
            dst.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            dst.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            dst.SetFloat("_ZWrite", 0f);
            dst.SetFloat("_AlphaClip", 0f);
            dst.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            SetKeyword(dst, "_SURFACE_TYPE_TRANSPARENT", true);
            SetKeyword(dst, "_ALPHAPREMULTIPLY_ON", false);
        }

        private static void CopyTexture(Material src, Material dst, string from, string to)
        {
            if (!src.HasProperty(from))
            {
                if (dst.HasProperty(to)) dst.SetTexture(to, null);
                return;
            }
            dst.SetTexture(to, src.GetTexture(from));

            // Масштаб и сдвиг текстуры тоже переносятся: иначе текстура «поедет» по поверхности.
            Vector2 scale = src.GetTextureScale(from);
            Vector2 offset = src.GetTextureOffset(from);
            if (dst.HasProperty(to)) dst.SetTextureScale(to, scale);
            if (dst.HasProperty(to)) dst.SetTextureOffset(to, offset);
        }

        private static void SetKeyword(Material m, string keyword, bool on)
        {
            if (on) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }

        // ==================================================== СВЕРКА МАТЕРИАЛОВ

        /// <summary>
        /// ПРИВЕСТИ МАТЕРИАЛЫ XR-ЗАЛА К ТОЧНО ТАКОЙ ЖЕ РАСКЛАДКЕ, КАК В ДЕСКТОПНОЙ СЦЕНЕ.
        ///
        /// ЗАЧЕМ ЭТО ОТДЕЛЬНЫМ ШАГОМ. Экземпляр префаба не всегда сохраняет материалы префаба
        /// один в один: у `Hangar_part_02` третий материал ссылается на подобъект (`type: 3`),
        /// и при постановке из префаба этот слот оказался ПУСТЫМ (`Sheet_metal | Metals_trim | ПУСТО`),
        /// тогда как в десктопной сцене там лежит `Glass`. Пустой слот материала под URP —
        /// это РОЗОВАЯ поверхность, прямо запрещённая приёмкой. Дефект нашёл тест проекта
        /// `KvRenderPipelineTests.XrScene_MaterialsAreUrpCompatibleWithoutMissingLinks`.
        ///
        /// КАК ИСПРАВЛЯЕТСЯ. Раскладка материалов запоминается с ЖИВЫХ объектов десктопной сцены
        /// (см. `ReadHierarchy`) и переносится по ПУТИ объекта в иерархии. Это не «заплатка»:
        /// так зал в шлеме получает ровно ту же раскладку, что и на компьютере, — чего владелец
        /// и требует словами «как на компьютере».
        /// </summary>
        private static int ApplyDesktopMaterials(Part part, Transform root,
            Dictionary<Material, Material> copies, StringBuilder log)
        {
            int applied = 0;

            foreach (Part child in part.children)
            {
                if (child.materials != null && child.materials.Length > 0 && !string.IsNullOrEmpty(child.path))
                {
                    Transform found = root.Find(child.path);
                    Renderer renderer = found != null ? found.GetComponent<Renderer>() : null;

                    if (renderer != null)
                    {
                        var mats = new Material[child.materials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            Material src = child.materials[i];
                            if (src == null) { mats[i] = null; continue; }

                            Material dst;
                            if (!copies.TryGetValue(src, out dst))
                            {
                                dst = CreateUrpCopy(src, log);
                                copies[src] = dst;
                            }
                            mats[i] = dst;
                        }
                        renderer.sharedMaterials = mats;
                        applied++;
                    }
                    else if (found == null)
                    {
                        log.AppendLine("     ВНИМАНИЕ: в XR-сцене не найден объект «" + child.path +
                                       "» — его материалы перенести не удалось");
                    }
                }

                applied += ApplyDesktopMaterials(child, root, copies, log);
            }
            return applied;
        }

        /// <summary>Сколько слотов материалов осталось пустыми (каждый такой слот — розовая поверхность).</summary>
        private static int CountEmptyMaterialSlots(GameObject root)
        {
            int empty = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == null) empty++;
            }
            return empty;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string Sanitize(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }

        // ==================================================== КОМПОНЕНТ-ЯКОРЬ

        private static void EnsureAnchorComponent(GameObject rootGo)
        {
            var content = Object.FindAnyObjectByType<KvXrSceneContent>(FindObjectsInactive.Include);
            GameObject host = content != null ? content.gameObject : rootGo;

            var anchor = host.GetComponent<KvXrHangarAnchor>();
            if (anchor == null) anchor = host.AddComponent<KvXrHangarAnchor>();
            anchor.hangarRoot = rootGo.transform;
            anchor.forceFloorAtZero = true;
            anchor.logOnPlace = true;
            EditorUtility.SetDirty(anchor);
        }
    }
}
