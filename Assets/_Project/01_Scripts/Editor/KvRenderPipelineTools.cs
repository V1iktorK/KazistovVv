using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using TrajectoryCore;
using KazistovVvFeatures;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// ИНСТРУМЕНТЫ ПАЙПЛАЙНА РЕНДЕРА (сессия §29, карточка Б1: HDRP мешает сборке под шлем).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ ФАЙЛ. Решение [D9] — «шлем на URP, компьютер на HDRP» — требует
    /// ПЕРЕНЕСТИ глобальное умолчание графики с HDRP на URP. Это правка проектной настройки
    /// (`ProjectSettings/GraphicsSettings.asset`), а не сцены, поэтому:
    ///   • делается кодом (воспроизводимо и проверяемо), а не «руками в окне»;
    ///   • печатает состояние ДО и ПОСЛЕ — иначе нечем доказать, что десктоп не поехал;
    ///   • ничего не сохраняет, пока не попросят (у инструмента чтения нет побочных эффектов).
    ///
    /// ЧТО ЗДЕСЬ ЕЩЁ И ПОЧЕМУ. «Готовые текстуры без ошибок» — это не только настройка
    /// пайплайна: материал, сделанный на шейдере HDRP, под URP рисуется РОЗОВЫМ, а материал
    /// с ПРОПАВШЕЙ ссылкой — розовым под любым пайплайном. Поэтому в файле есть АУДИТ
    /// материалов: он перечисляет по каждому материалу (ассет, модель, сцена) шейдер,
    /// доступность шейдера и совместимость с пайплайном — и честно называет «розовые»
    /// кандидаты. Аудит ничего не меняет: он только называет числа и имена.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ. Логика выбора шейдера живёт в РАНТАЙМ-типе
    /// `KazistovVvFeatures.KvRenderPipelineShaders` — он нужен и в плеере (материалы,
    /// создаваемые кодом, обязаны совпадать с пайплайном). Здесь — только редакторские
    /// операции и отчёты.
    /// </summary>
    public static class KvRenderPipelineTools
    {
        private const string MenuRoot = "Tools/KazistovVv Рендер/";

        /// <summary>Путь URP-ассета, который берётся глобальным умолчанием (решение [D9]).</summary>
        public const string UrpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";

        /// <summary>Путь HDRP-ассета, который обязан остаться конвейером уровня «PC».</summary>
        public const string HdrpAssetPath = "Assets/ROOMS/HQ Hangar Free/Scene/HDRPHighQuality.asset";

        private const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
        private const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";
        private const string XrScenePath = "Assets/_Project/00_Scenes/XrMain.unity";
        private const string MainScenePath = "Assets/_Project/00_Scenes/MainScene.unity";

        // ============================================================ РАЗВЕДКА В ОДИН ВЫЗОВ

        /// <summary>
        /// ТОЧКА ВХОДА ДЛЯ ПАКЕТНОГО ПРОГОНА: отчёт о состоянии + аудит материалов.
        /// Ничего не меняет. Печатает всё в журнал Unity (Logs\…log), чтобы числа можно было
        /// положить в отчёт, а не пересказывать по памяти.
        /// </summary>
        public static void Recon()
        {
            ReportState();
            AuditAll();
        }

        // ================================================================== СОСТОЯНИЕ

        /// <summary>Отчёт о состоянии пайплайнов: умолчание, уровни качества, платформы, API.</summary>
        [MenuItem(MenuRoot + "1. Отчёт о состоянии пайплайнов", false, 10)]
        public static void ReportState()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvRender] === СОСТОЯНИЕ ПАЙПЛАЙНОВ РЕНДЕРА ===");

            RenderPipelineAsset def = GraphicsSettings.defaultRenderPipeline;
            sb.AppendLine("Глобальное умолчание графики: " + DescribeAsset(def));

            RenderPipelineAsset current = GraphicsSettings.currentRenderPipeline;
            sb.AppendLine("Активный сейчас (для редактора): " + DescribeAsset(current));
            sb.AppendLine("Текущий уровень качества: индекс " + QualitySettings.GetQualityLevel() +
                          " («" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "»)");

            foreach (string line in DescribeQualityLevels()) sb.AppendLine(line);
            foreach (string line in DescribeGlobalSettingsMap()) sb.AppendLine(line);
            foreach (string line in DescribePlatforms()) sb.AppendLine(line);
            foreach (string line in DescribeShaders()) sb.AppendLine(line);

            Debug.Log(sb.ToString());
        }

        private static string DescribeAsset(RenderPipelineAsset asset)
        {
            if (asset == null) return "НЕТ (встроенный конвейер)";
            string path = AssetDatabase.GetAssetPath(asset);
            return asset.name + " [" + asset.GetType().Name + "] " + path;
        }

        private static IEnumerable<string> DescribeQualityLevels()
        {
            var lines = new List<string>();
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(QualitySettingsPath);
            if (assets == null || assets.Length == 0)
            {
                lines.Add("Уровни качества: НЕ ПРОЧИТАНЫ (" + QualitySettingsPath + ")");
                return lines;
            }

            var so = new SerializedObject(assets[0]);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");
            if (levels == null)
            {
                lines.Add("Уровни качества: свойство m_QualitySettings не найдено");
                return lines;
            }

            lines.Add("Уровней качества: " + levels.arraySize);
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = level.FindPropertyRelative("name");
                SerializedProperty pipeProp = level.FindPropertyRelative("customRenderPipeline");
                SerializedProperty excluded = level.FindPropertyRelative("excludedTargetPlatforms");

                var asset = pipeProp != null ? pipeProp.objectReferenceValue as RenderPipelineAsset : null;
                string excludedList = "нет";
                if (excluded != null && excluded.arraySize > 0)
                {
                    var names = new List<string>();
                    for (int k = 0; k < excluded.arraySize; k++)
                        names.Add(excluded.GetArrayElementAtIndex(k).stringValue);
                    excludedList = string.Join(", ", names);
                }

                lines.Add(string.Format("  [{0}] «{1}» → {2} · исключён из: {3}",
                    i, nameProp != null ? nameProp.stringValue : "?", DescribeAsset(asset), excludedList));
            }
            return lines;
        }

        private static IEnumerable<string> DescribeGlobalSettingsMap()
        {
            var lines = new List<string>();
            string text = ReadProjectSettings(GraphicsSettingsPath);
            if (text == null)
            {
                lines.Add("Карта глобальных настроек: НЕ ПРОЧИТАН " + GraphicsSettingsPath);
                return lines;
            }

            List<string> block = YamlBlock(text, "m_RenderPipelineGlobalSettingsMap");
            if (block.Count == 0)
            {
                lines.Add("Карта глобальных настроек: записи в файле нет");
                return lines;
            }

            lines.Add("Глобальные настройки конвейеров (" + block.Count + "):");
            foreach (string line in block)
            {
                int colon = line.IndexOf(':');
                string key = colon > 0 ? line.Substring(0, colon).Trim() : line.Trim();
                string guid = ExtractGuid(line);
                string assetPath = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
                lines.Add("  " + key + " → " + (string.IsNullOrEmpty(assetPath) ? "НЕТ" : assetPath));
            }
            return lines;
        }

        /// <summary>
        /// ОБХОД СЕРИАЛИЗОВАННОГО ПОДДЕРЕВА. Для СЛОВАРЕЙ НЕ ПОДХОДИТ: в Unity 6
        /// `m_PerPlatformDefaultQuality` и `m_RenderPipelineGlobalSettingsMap` через свойства
        /// читаются так, что значения теряются (проверено §29: отчёт печатал «? → НЕТ» на живых
        /// настройках). Словари читаются ИЗ ФАЙЛА проекта, а этот обход используется там, где
        /// свойства обычные — при инспекции компонентов.
        /// </summary>
        private static IEnumerable<SerializedProperty> Descendants(SerializedProperty root)
        {
            var found = new List<SerializedProperty>();
            if (root == null) return found;

            SerializedProperty end = root.GetEndProperty();
            SerializedProperty it = root.Copy();
            bool enterChildren = true;
            while (it.Next(enterChildren) && !SerializedProperty.EqualContents(it, end))
            {
                found.Add(it.Copy());
                enterChildren = true;
            }
            return found;
        }

        /// <summary>Текст файла проекта (ProjectSettings/…): отчёт обязан видеть то, что на диске.</summary>
        private static string ReadProjectSettings(string relativePath)
        {
            string root = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string path = System.IO.Path.Combine(root,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
        }

        /// <summary>
        /// Строки YAML-блока по имени ключа: от строки «имя:» до первой строки с меньшим или
        /// равным отступом. Разбор намеренно простой: файл проектных настроек — плоский YAML,
        /// а полноценный парсер был бы лишней зависимостью ради двух словарей.
        /// </summary>
        private static List<string> YamlBlock(string text, string key)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            string[] all = text.Replace("\r\n", "\n").Split('\n');
            int start = -1;
            int indent = 0;
            for (int i = 0; i < all.Length; i++)
            {
                string trimmed = all[i].TrimStart();
                if (!trimmed.StartsWith(key + ":", StringComparison.Ordinal)) continue;
                start = i;
                indent = all[i].Length - trimmed.Length;
                break;
            }
            if (start < 0) return result;

            for (int i = start + 1; i < all.Length; i++)
            {
                string line = all[i];
                if (line.Trim().Length == 0) continue;
                int lineIndent = line.Length - line.TrimStart().Length;
                if (lineIndent <= indent) break;
                result.Add(line.Trim());
            }
            return result;
        }

        /// <summary>Первый guid из строки вида `{fileID: …, guid: 32 hex, type: 2}`.</summary>
        private static string ExtractGuid(string line)
        {
            int at = line.IndexOf("guid: ", StringComparison.Ordinal);
            if (at < 0) return null;
            string tail = line.Substring(at + 6).TrimStart();
            int end = 0;
            while (end < tail.Length && IsHex(tail[end])) end++;
            return end == 32 ? tail.Substring(0, 32) : null;
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static IEnumerable<string> DescribePlatforms()
        {
            var lines = new List<string>();
            string qualityText = ReadProjectSettings(QualitySettingsPath);
            if (qualityText != null)
            {
                var parts = new List<string>();
                foreach (string line in YamlBlock(qualityText, "m_PerPlatformDefaultQuality"))
                {
                    int colon = line.IndexOf(':');
                    if (colon <= 0) continue;
                    string platform = line.Substring(0, colon).Trim();
                    if (platform != "Android" && platform != "Standalone") continue;
                    int index;
                    if (!int.TryParse(line.Substring(colon + 1).Trim(), out index)) continue;
                    string levelName = index >= 0 && index < QualitySettings.names.Length
                        ? QualitySettings.names[index] : "индекс " + index;
                    parts.Add(platform + " → " + levelName + " (" + index + ")");
                }
                lines.Add("Уровень качества по умолчанию на платформу: " +
                          (parts.Count > 0 ? string.Join(" · ", parts) : "НЕ ПРОЧИТАНЫ"));
            }

            try
            {
                GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
                lines.Add("Графический API Android: " +
                          string.Join(", ", Array.ConvertAll(apis, a => a.ToString())));
            }
            catch (Exception e)
            {
                lines.Add("Графический API Android: не прочитан (" + e.Message + ")");
            }

            lines.Add("Активная платформа редактора: " + EditorUserBuildSettings.activeBuildTarget);
            return lines;
        }

        private static IEnumerable<string> DescribeShaders()
        {
            var lines = new List<string>();
            string[] probes =
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "HDRP/Lit",
                "HDRP/Unlit",
                "Standard",
                "Unlit/Color",
                "Sprites/Default"
            };

            lines.Add("Шейдеры в проекте (Shader.Find):");
            foreach (string name in probes)
            {
                Shader shader = Shader.Find(name);
                lines.Add("  " + name + " → " + (shader != null ? "есть" : "НЕ НАЙДЕН"));
            }
            return lines;
        }

        // ================================================================== АУДИТ МАТЕРИАЛОВ

        /// <summary>
        /// АУДИТ МАТЕРИАЛОВ: что на чём нарисовано и что из этого станет розовым.
        /// Смотрит три источника: ассеты-материалы, материалы ВНУТРИ моделей (под-ассеты FBX/GLB)
        /// и материалы рендереров в сценах проекта. Ничего не меняет.
        /// </summary>
        [MenuItem(MenuRoot + "2. Аудит материалов (что станет розовым)", false, 20)]
        public static void AuditAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvRender] === АУДИТ МАТЕРИАЛОВ ===");
            sb.AppendLine("Конвейер активного уровня качества редактора: " +
                          KvRenderPipelineShaders.ActiveKind() +
                          " (по нему оцениваются материалы-ассеты; сцены оцениваются по своим: " +
                          "XR → URP, десктоп → HDRP)");

            AuditMaterialAssets(sb);
            AuditModelMaterials(sb);
            AuditScenes(sb);

            Debug.Log(sb.ToString());
        }

        private static void AuditMaterialAssets(StringBuilder sb)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            sb.AppendLine("Материалов-ассетов в Assets: " + guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                sb.AppendLine("  " + Verdict(material, path));
            }
        }

        private static void AuditModelMaterials(StringBuilder sb)
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
            int found = 0;
            var lines = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
                if (all == null) continue;
                foreach (UnityEngine.Object o in all)
                {
                    var material = o as Material;
                    if (material == null) continue;
                    found++;
                    lines.Add("  " + Verdict(material, path + " → " + material.name));
                }
            }

            sb.AppendLine("Материалов ВНУТРИ моделей (под-ассеты FBX/GLB): " + found);
            foreach (string line in lines) sb.AppendLine(line);
        }

        private static void AuditScenes(StringBuilder sb)
        {
            // КАЖДАЯ СЦЕНА ОЦЕНИВАЕТСЯ ПО СВОЕМУ КОНВЕЙЕРУ. Первый прогон §29 оценивал обе по
            // активному пайплайну редактора (уровень «PC» → HDRP) и объявил «розовыми» ровно те
            // материалы XR-сцены, которые были правильно переведены на URP. Отчёт, который врёт
            // в обе стороны, хуже отсутствия отчёта: XR-сцена собирается под шлем (URP, [D9]),
            // десктопная — под компьютер (HDRP).
            AuditScene(sb, XrScenePath, KvPipelineKind.Urp);
            AuditScene(sb, MainScenePath, KvPipelineKind.Hdrp);
        }

        private static void AuditScene(StringBuilder sb, string path, KvPipelineKind kind)
        {
            if (!System.IO.File.Exists(path))
            {
                sb.AppendLine("Сцена " + path + ": файла нет");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                    FindObjectsInactive.Include);
                var renderersInScene = new List<Renderer>();
                foreach (Renderer r in renderers)
                    if (r != null && r.gameObject.scene == scene) renderersInScene.Add(r);

                var materialNames = new Dictionary<string, int>();
                int missing = 0;
                var problems = new List<string>();

                foreach (Renderer r in renderersInScene)
                {
                    Material[] mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (Material m in mats)
                    {
                        if (m == null)
                        {
                            missing++;
                            problems.Add("    ПРОПАВШАЯ ССЫЛКА на материале: " +
                                         FullPath(r.transform) + " (" + r.GetType().Name + ")");
                            continue;
                        }
                        string key = m.name + " · " + ShaderName(m);
                        if (!materialNames.ContainsKey(key)) materialNames[key] = 0;
                        materialNames[key]++;

                        if (!KvRenderPipelineShaders.IsCompatible(m.shader, kind))
                            problems.Add("    НЕСОВМЕСТИМ: " + m.name + " (" + ShaderName(m) + ") на " +
                                         FullPath(r.transform));
                    }
                }

                sb.AppendLine("Сцена " + path + " (ожидается " + kind + "): рендереров " +
                              renderersInScene.Count + ", пропавших ссылок на материалы " + missing);
                foreach (KeyValuePair<string, int> kv in materialNames)
                    sb.AppendLine("    " + kv.Value + "× " + kv.Key);
                if (problems.Count == 0)
                    sb.AppendLine("    → все материалы совместимы с " + kind + " (розовых нет)");
                foreach (string p in problems) sb.AppendLine(p);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// ВЕРДИКТ ПО МАТЕРИАЛУ-АССЕТУ. У него НЕТ одного правильного ответа: годность материала
        /// зависит от конвейера, а в проекте их два (решение [D9]). Поэтому печатаются ОБА
        /// вердикта: первый прогон §29 оценивал всё по активному конвейеру редактора (HDRP) и
        /// объявил «розовыми» ровно те материалы, которые правильно переведены на URP для шлема.
        /// </summary>
        private static string Verdict(Material material, string where)
        {
            if (material == null) return "ПРОПАВШИЙ МАТЕРИАЛ: " + where;

            bool urp = KvRenderPipelineShaders.IsCompatible(material.shader, KvPipelineKind.Urp);
            bool hdrp = KvRenderPipelineShaders.IsCompatible(material.shader, KvPipelineKind.Hdrp);
            string mark = urp && hdrp ? "везде " : (urp ? "URP   " : (hdrp ? "HDRP  " : "НИГДЕ "));

            return mark + material.name + " · шейдер " + ShaderName(material) +
                   " · URP:" + (urp ? "да" : "нет") + " HDRP:" + (hdrp ? "да" : "нет") + " · " + where;
        }

        private static string ShaderName(Material material)
        {
            if (material == null) return "—";
            if (material.shader == null) return "ПРОПАВШИЙ ШЕЙДЕР";
            return material.shader.name;
        }

        private static string FullPath(Transform t)
        {
            if (t == null) return "?";
            string path = t.name;
            Transform p = t.parent;
            while (p != null) { path = p.name + "/" + path; p = p.parent; }
            return path;
        }

        // ================================================================== ИНСПЕКЦИЯ СЦЕН

        /// <summary>
        /// ТОЧКА ВХОДА ПАКЕТНОГО ПРОГОНА №2: что на самом деле лежит в сценах.
        /// Нужна, чтобы НЕ УГАДЫВАТЬ при переносе роботов в XR-сцену: шаблон робота — это
        /// не только модель, но и компоненты (шестиосевик, контроллер, IK), а у них есть
        /// ссылки. Если ссылка ведёт на объект ВНЕ иерархии робота, то при переносе в другую
        /// сцену она станет пустой — и робот в шлеме окажется нерабочим.
        /// </summary>
        public static void InspectScenes()
        {
            InspectScene(MainScenePath, "ДЕСКТОПНАЯ (MainScene)");
            InspectScene(XrScenePath, "XR (XrMain)");
        }

        /// <summary>Что в сцене: корневые объекты, роботы, компоненты и «внешние» ссылки.</summary>
        [MenuItem(MenuRoot + "5. Инспекция сцен (роботы и ссылки)", false, 50)]
        public static void InspectScene(string scenePath, string title)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.Log("[KvRender] Инспекция: сцены " + scenePath + " нет.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[KvRender] === ИНСПЕКЦИЯ СЦЕНЫ: " + title + " (" + scenePath + ") ===");

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var roots = new List<GameObject>();
                foreach (GameObject go in scene.GetRootGameObjects()) roots.Add(go);
                sb.AppendLine("Корневых объектов: " + roots.Count + " → " +
                              string.Join(", ", roots.ConvertAll(r => r.name)));

                RobotController[] robots =
                    UnityEngine.Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
                var robotsInScene = new List<RobotController>();
                foreach (RobotController rc in robots)
                    if (rc != null && rc.gameObject.scene == scene) robotsInScene.Add(rc);

                sb.AppendLine("Роботов (RobotController) в сцене: " + robotsInScene.Count);
                foreach (RobotController rc in robotsInScene)
                    DescribeRobot(sb, rc);

                int renderers = 0;
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                {
                    if (r == null || r.gameObject.scene != scene) continue;
                    renderers++;
                }
                sb.AppendLine("Рендереров в сцене: " + renderers);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log(sb.ToString());
        }

        private static void DescribeRobot(StringBuilder sb, RobotController robot)
        {
            GameObject go = robot.gameObject;
            var components = go.GetComponents<Component>();
            var names = new List<string>();
            foreach (Component c in components)
                names.Add(c == null ? "ПРОПАВШИЙ_СКРИПТ" : c.GetType().Name);

            sb.AppendLine("  РОБОТ «" + go.name + "» · тип " + robot.GetType().Name +
                          " · активен: " + go.activeInHierarchy +
                          " · дочерних объектов: " + go.GetComponentsInChildren<Transform>(true).Length);
            sb.AppendLine("     компоненты на корне: " + string.Join(", ", names));

            // ВНЕШНИЕ ССЫЛКИ — то, что потеряется при переносе в другую сцену.
            var outside = new List<string>();
            foreach (Component c in components)
            {
                if (c == null) continue;
                SerializedObject so;
                try { so = new SerializedObject(c); }
                catch (Exception) { continue; }

                foreach (SerializedProperty p in Descendants(so.GetIterator()))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    UnityEngine.Object value = p.objectReferenceValue;
                    if (value == null) continue;

                    var valueGo = value as GameObject;
                    if (valueGo == null && value is Component comp) valueGo = comp.gameObject;
                    if (valueGo == null) continue;                       // ассет — нормально
                    if (valueGo.scene != go.scene) continue;             // из другой сцены — уже проблема, но не наша
                    if (valueGo.transform.IsChildOf(go.transform)) continue;

                    outside.Add(c.GetType().Name + "." + p.propertyPath + " → " + valueGo.name +
                                " (сцена)");
                }
            }

            if (outside.Count == 0)
                sb.AppendLine("     внешних ссылок на объекты сцены НЕТ — робот переносится целиком");
            else
            {
                sb.AppendLine("     ВНЕШНИХ ссылок на объекты сцены: " + outside.Count);
                for (int i = 0; i < outside.Count && i < 20; i++) sb.AppendLine("        " + outside[i]);
            }
        }

        // ============================================================ ПЕРЕКЛЮЧЕНИЕ УМОЛЧАНИЯ

        /// <summary>
        /// СДЕЛАТЬ URP ГЛОБАЛЬНЫМ УМОЛЧАНИЕМ ГРАФИКИ (решение [D9], полный вариант).
        ///
        /// ПОЧЕМУ ЭТО ГЛАВНЫЙ ШАГ ЗАКРЫТИЯ Б1. Пакет HDRP не поддерживает Android вообще
        /// (`HDUtils.IsSupportedBuildTarget` — жёсткий список платформ, Android в нём нет),
        /// и `HDRPPreprocessBuild` обрывает сборку. Валидатор смотрит НЕ только уровни качества,
        /// но и глобальное умолчание — поэтому оно и переводится на URP.
        ///
        /// ЧТО ПРИ ЭТОМ НЕ МЕНЯЕТСЯ (проверяется отчётом): уровни качества остаются как были —
        /// «Mobile» на URP для шлема, «PC» на HDRP для компьютера; наборы платформ, графические
        /// API и сам пакет HDRP не трогаются. Десктоп выглядит как прежде, потому что уровень
        /// «PC» перекрывает умолчание своим HDRP-ассетом.
        ///
        /// ПОЧЕМУ ЧЕРЕЗ SerializedObject, А НЕ ЧЕРЕЗ СВОЙСТВО. Запись в проектный файл
        /// проектных настроек — операция, которую нужно уметь проверять по факту (файл на диске),
        /// а не доверять тому, что свойство «вроде применилось». Здесь значение пишется в
        /// сериализованное поле `m_CustomRenderPipeline`, сохраняется и ПЕРЕЧИТЫВАЕТСЯ.
        /// </summary>
        [MenuItem(MenuRoot + "3. URP — глобальным умолчанием (Б1, решение [D9])", false, 30)]
        public static void SwitchGlobalDefaultToUrp()
        {
            var urp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
            {
                Debug.LogError("[KvRender] НЕ НАЙДЕН URP-ассет " + UrpAssetPath +
                               " — глобальное умолчание не изменено.");
                return;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[KvRender] НЕ ПРОЧИТАН " + GraphicsSettingsPath +
                               " — глобальное умолчание не изменено.");
                return;
            }

            var so = new SerializedObject(assets[0]);
            SerializedProperty prop = so.FindProperty("m_CustomRenderPipeline");
            if (prop == null)
            {
                Debug.LogError("[KvRender] В " + GraphicsSettingsPath +
                               " нет свойства m_CustomRenderPipeline — не изменено.");
                return;
            }

            var before = prop.objectReferenceValue as RenderPipelineAsset;
            if (before == urp)
            {
                Debug.Log("[KvRender] Глобальное умолчание УЖЕ URP (" + urp.name + ") — правка не нужна.");
                return;
            }

            prop.objectReferenceValue = urp;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ПЕРЕЧИТЫВАНИЕ ПО ФАКТУ: настройка считается применённой только если её видно
            // в сохранённом файле, а не только в памяти редактора.
            var check = GraphicsSettings.defaultRenderPipeline;
            bool ok = check == urp;

            Debug.Log("[KvRender] ГЛОБАЛЬНОЕ УМОЛЧАНИЕ ГРАФИКИ: " + DescribeAsset(before) +
                      " → " + DescribeAsset(check) + (ok ? " · ПРИМЕНЕНО" : " · НЕ ПРИМЕНИЛОСЬ"));

            if (!ok)
            {
                Debug.LogError("[KvRender] Глобальное умолчание не стало URP — проверьте " +
                               GraphicsSettingsPath + " вручную.");
                return;
            }

            ReportState();
        }

        // ==================================================== НАЧИНКА XR-СЦЕНЫ И URP-МАТЕРИАЛЫ

        /// <summary>
        /// ТОЧКА ВХОДА ПАКЕТНОГО ПРОГОНА №3: закрытие Б1 целиком.
        /// 1) URP — глобальным умолчанием (HDRP остаётся уровнем «PC» для компьютера);
        /// 2) в XR-сцене появляется начинка (столы и роботы) и компонент `KvXrSceneContent`;
        /// 3) материалы XR-сцены переводятся на URP (копии свойств — оригиналы не портятся);
        /// 4) отчёт и аудит — чтобы числа были в журнале, а не в пересказе.
        /// </summary>
        public static void ApplyUrpSwitch()
        {
            SwitchGlobalDefaultToUrp();
            EnsureXrSceneContentAndMaterials();
            ReportState();
            AuditAll();
        }

        /// <summary>
        /// НАЧИНКА XR-СЦЕНЫ. Отчёт §28 утверждал, что компонент `KvXrSceneContent` добавлен
        /// в сцену; проверка по факту (§29) показала: в `XrMain.unity` нет ни компонента,
        /// ни роботов, ни столов — рендереров НОЛЬ. То есть в шлеме показывать было нечего,
        /// а «готовых текстур» не существовало вовсе.
        ///
        /// ЧТО ДЕЛАЕТСЯ. В `XrMain` переносятся ГОТОВЫЕ стенды (`Стенд_1_Стол`, `Стенд_2_Стол`
        /// вместе с роботами) из десктопной сцены — копией, а не ссылкой: инспекция показала,
        /// что у роботов нет ни одной ссылки на объекты вне их собственной иерархии, поэтому
        /// копия самодостаточна. Дополнительно ставится компонент `KvXrSceneContent`: он
        /// идемпотентен и, если стенды когда-нибудь удалят, построит их тем же кодом, что и
        /// десктоп (`StandBuilder.EnsureStands`). `MainScene` при этом НЕ меняется ([D6]).
        /// </summary>
        [MenuItem(MenuRoot + "4. Начинка XR-сцены + URP-материалы", false, 40)]
        public static void EnsureXrSceneContentAndMaterials()
        {
            if (!System.IO.File.Exists(XrScenePath))
            {
                Debug.LogError("[KvRender] Нет XR-сцены " + XrScenePath + " — начинка не добавлена.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[KvRender] === НАЧИНКА XR-СЦЕНЫ ===");

            Scene xr = EditorSceneManager.OpenScene(XrScenePath, OpenSceneMode.Single);
            int copiedStands;
            int materialsChanged;
            try
            {
                copiedStands = CopyStandsFromDesktop(sb);
                EnsureContentComponent(sb);

                materialsChanged = ConvertSceneMaterials(xr, KvPipelineKind.Urp, sb);

                EditorSceneManager.MarkSceneDirty(xr);
                bool saved = EditorSceneManager.SaveScene(xr);
                sb.AppendLine("XR-сцена сохранена: " + saved + " · стендов перенесено: " + copiedStands +
                              " · материалов переведено на URP: " + materialsChanged);
            }
            finally
            {
                // Сцена закрывается БЕЗ сохранения только при аварии: сохранение уже сделано выше.
                if (xr.IsValid() && xr.isLoaded && xr.isDirty)
                    sb.AppendLine("ВНИМАНИЕ: сцена осталась несохранённой (проверьте вручную)");
            }

            Debug.Log(sb.ToString());
        }

        private static int CopyStandsFromDesktop(StringBuilder sb)
        {
            Scene desktop = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            int copied = 0;
            try
            {
                foreach (string standName in new[] { "Стенд_1_Стол", "Стенд_2_Стол" })
                {
                    GameObject source = FindRootObject(desktop, standName);
                    if (source == null)
                    {
                        sb.AppendLine("  в десктопной сцене нет «" + standName + "» — пропущено");
                        continue;
                    }
                    if (FindRootObject(EditorSceneManager.GetActiveScene(), standName) != null)
                    {
                        sb.AppendLine("  «" + standName + "» уже есть в XR-сцене — пропущено");
                        continue;
                    }

                    GameObject copy = UnityEngine.Object.Instantiate(source);
                    copy.name = source.name;
                    copy.transform.position = source.transform.position;
                    copy.transform.rotation = source.transform.rotation;
                    SceneManager.MoveGameObjectToScene(copy, EditorSceneManager.GetActiveScene());
                    copied++;
                    sb.AppendLine("  перенесён «" + standName + "» (рендереров внутри: " +
                                  copy.GetComponentsInChildren<Renderer>(true).Length + ")");
                }
            }
            finally
            {
                if (desktop.IsValid() && desktop.isLoaded)
                    EditorSceneManager.CloseScene(desktop, true);   // БЕЗ сохранения: десктоп не менялся
            }
            return copied;
        }

        private static void EnsureContentComponent(StringBuilder sb)
        {
            KazistovVvXr.KvXrSceneContent[] found =
                UnityEngine.Object.FindObjectsByType<KazistovVvXr.KvXrSceneContent>(FindObjectsInactive.Include);
            if (found != null && found.Length > 0)
            {
                sb.AppendLine("  компонент KvXrSceneContent уже есть на «" + found[0].gameObject.name + "»");
                return;
            }

            var go = new GameObject("XrSceneContent");
            go.AddComponent<KazistovVvXr.KvXrSceneContent>();
            Undo.RegisterCreatedObjectUndo(go, "Добавление начинки XR-сцены");
            sb.AppendLine("  добавлен объект «XrSceneContent» с компонентом KvXrSceneContent");
        }

        private static GameObject FindRootObject(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go != null && go.name == name) return go;
            return null;
        }

        /// <summary>
        /// ПЕРЕВЕСТИ МАТЕРИАЛЫ РЕНДЕРЕРОВ СЦЕНЫ НА НУЖНЫЙ КОНВЕЙЕР.
        /// Работает только с указанной сценой: десктопные материалы HDRP не трогаются вовсе —
        /// иначе компьютер потерял бы вид, который владелец уже принял.
        /// </summary>
        private static int ConvertSceneMaterials(Scene scene, KvPipelineKind kind, StringBuilder sb)
        {
            var variants = new Dictionary<Material, Material>();
            var lines = new List<string>();
            int changedRenderers = 0;

            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (r == null || r.gameObject.scene != scene) continue;

                Material[] materials = r.sharedMaterials;
                if (materials == null || materials.Length == 0) continue;

                bool dirty = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;
                    if (KazistovVvFeatures.KvRenderPipelineShaders.IsCompatible(source.shader, kind)) continue;

                    Material variant = GetOrCreateVariant(source, kind, variants);
                    if (variant == null) continue;
                    materials[i] = variant;
                    dirty = true;
                }

                if (!dirty) continue;
                r.sharedMaterials = materials;
                EditorUtility.SetDirty(r);
                changedRenderers++;
                if (lines.Count < 20)
                    lines.Add("    " + r.name + " → " + DescribeMaterials(materials));
            }

            sb.AppendLine("  рендереров с заменёнными материалами: " + changedRenderers);
            foreach (string line in lines) sb.AppendLine(line);
            return changedRenderers;
        }

        private static string DescribeMaterials(Material[] materials)
        {
            var names = new List<string>();
            foreach (Material m in materials)
                names.Add(m == null ? "ПУСТО" : m.name + " (" + (m.shader != null ? m.shader.name : "ПРОПАЛ") + ")");
            return string.Join(", ", names);
        }

        /// <summary>
        /// СОЗДАТЬ КОПИЮ МАТЕРИАЛА ДЛЯ НУЖНОГО КОНВЕЙЕРА (или взять уже созданную).
        /// Копируются только «вещественные» свойства поверхности: цвет, текстура, металличность,
        /// гладкость, карта нормалей. Имена свойств у конвейеров разные, поэтому берутся
        /// кандидаты (`_BaseColor`/`_Color`, `_BaseMap`/`_BaseColorMap`/`_MainTex`) — иначе
        /// робот в шлеме оказался бы серым «по умолчанию», а не своим.
        /// </summary>
        private static Material GetOrCreateVariant(Material source, KvPipelineKind kind,
                                                   Dictionary<Material, Material> cache)
        {
            if (source == null) return null;
            if (cache.TryGetValue(source, out Material cached)) return cached;

            string folder = "Assets/_Project/04_Materials/" + (kind == KvPipelineKind.Urp ? "URP" : "HDRP");
            EnsureFolder(folder);

            string fileName = Sanitize(source.name) + "_" + (kind == KvPipelineKind.Urp ? "URP" : "HDRP") + ".mat";
            string path = folder + "/" + fileName;

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                cache[source] = existing;
                return existing;
            }

            Material variant = KazistovVvFeatures.KvRenderPipelineShaders.CreateLit(
                kind, source.name + "_" + (kind == KvPipelineKind.Urp ? "URP" : "HDRP"), Color.white);
            if (variant == null)
            {
                Debug.LogWarning("[KvRender] нет Lit-шейдера для конвейера " + kind +
                                 " — материал «" + source.name + "» не переведён.");
                return null;
            }

            CopySurface(source, variant);
            variant.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(variant, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();

            cache[source] = variant;
            return variant;
        }

        private static void CopySurface(Material source, Material target)
        {
            // Цвет
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : (source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            KazistovVvFeatures.KvRenderPipelineShaders.ApplyColor(target, color, false);

            // Основная текстура
            Texture albedo = source.HasProperty("_BaseColorMap") ? source.GetTexture("_BaseColorMap")
                : (source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap")
                : (source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null));
            KazistovVvFeatures.KvRenderPipelineShaders.ApplyTexture(target, albedo, false);

            // Карта нормалей
            Texture normal = source.HasProperty("_NormalMap") ? source.GetTexture("_NormalMap")
                : (source.HasProperty("_BumpMap") ? source.GetTexture("_BumpMap") : null);
            if (normal != null)
            {
                if (target.HasProperty("_BumpMap")) target.SetTexture("_BumpMap", normal);
                if (target.HasProperty("_NormalMap")) target.SetTexture("_NormalMap", normal);
            }

            // Числа поверхности
            if (target.HasProperty("_Metallic"))
                target.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
            if (target.HasProperty("_Smoothness"))
            {
                float smoothness = source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness")
                    : (source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.5f);
                target.SetFloat("_Smoothness", smoothness);
            }

            // Прозрачность: у URP это `_Surface`/`_Blend`, у HDRP — `_SurfaceType`/`_BlendMode`.
            if (source.HasProperty("_SurfaceType") && source.GetFloat("_SurfaceType") > 0.5f)
            {
                if (target.HasProperty("_Surface")) target.SetFloat("_Surface", 1f);
                if (target.HasProperty("_Blend")) target.SetFloat("_Blend", 0f);
                target.renderQueue = (int)RenderQueue.Transparent;
            }
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
            if (string.IsNullOrEmpty(name)) return "Материал";
            var sb = new StringBuilder(name.Length);
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in name)
            {
                bool bad = false;
                foreach (char i in invalid) if (c == i) { bad = true; break; }
                sb.Append(bad ? '_' : c);
            }
            return sb.ToString();
        }

        // ============================================================ МАТЕРИАЛ РОБОТА (§30)

        private const string RobotModelPath = "Assets/Robots/Robot.fbx";
        private const string RobotSampleModelPath = "Assets/Robots/ScaraRobot.fbx";
        private const string RobotMaterialFolder = "Assets/_Project/04_Materials/Robot";
        private const string RobotObjectName = "Робот_6ос_Стенд1";

        /// <summary>
        /// МАТЕРИАЛ РОБОТА — КАК У ОСТАЛЬНЫХ РОБОТОВ (задача владельца после §29).
        ///
        /// ЧТО БЫЛО. `Robot.fbx` переназначала свой материал на guid `b90d9d26…`, которого
        /// в проекте НЕТ (ассет утрачен), поэтому 17 рендереров робота рисовались
        /// `DefaultHDMaterial` — материалом конвейера «по умолчанию». Он проекту не принадлежит:
        /// его нельзя ни настроить, ни перевести на URP, а под URP он вообще розовый.
        ///
        /// ЧТО ДЕЛАЕТСЯ. Образец внешнего вида берётся у ДРУГОГО робота — материала SCARA из
        /// `ScaraRobot.fbx` (владелец: «назначь материалы как и у остальных роботов — там все
        /// одинаковые»). Из образца делаются ДВА материала проекта: HDRP для компьютера и URP
        /// для шлема — по одному на конвейер, потому что один материал не может быть корректен
        /// в обоих. Затем материалы назначаются рендерерам робота в обеих сценах, и правится
        /// переназначение в самой модели, чтобы битой ссылки в проекте не осталось.
        /// </summary>
        [MenuItem(MenuRoot + "6. Материал робота — как у остальных роботов", false, 60)]
        public static void EnsureRobotMaterials()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvRender] === МАТЕРИАЛ РОБОТА ===");

            Material sample = FindSampleMaterial(sb);
            if (sample == null)
            {
                Debug.LogError("[KvRender] Не найден образец материала в " + RobotSampleModelPath +
                               " — материал робота не изменён.");
                return;
            }

            Material hdrp = CreateRobotMaterial(KvPipelineKind.Hdrp, sample, sb);
            Material urp = CreateRobotMaterial(KvPipelineKind.Urp, sample, sb);
            if (hdrp == null || urp == null)
            {
                Debug.LogError("[KvRender] Не удалось создать материалы робота — ничего не изменено.");
                return;
            }

            int onDesktop = AssignToRobot(MainScenePath, hdrp, sb);
            int inXr = AssignToRobot(XrScenePath, urp, sb);
            FixModelRemap(hdrp, sb);

            sb.AppendLine("  ИТОГ: рендереров робота — на компьютере " + onDesktop +
                          ", в шлеме " + inXr + " · материалы проекта: " + hdrp.name + ", " + urp.name);
            Debug.Log(sb.ToString());

            AuditAll();
        }

        /// <summary>
        /// Образец внешнего вида — материал другого робота (SCARA) из его модели.
        ///
        /// ПОЧЕМУ НЕ «ПЕРВЫЙ ПОПАВШИЙСЯ». У модели робота материалов несколько, и они не
        /// обязаны быть одинаковыми: у SCARA шесть штук, и один из них — деталь своего цвета
        /// (первый прогон взял `Material_6` и покрасил робота в синий). Владелец сказал:
        /// «назначь материалы как и у остальных роботов — там все одинаковые», поэтому образец
        /// выбирается ПО ДАННЫМ: материалы группируются по внешнему виду (цвет, металличность,
        /// гладкость) и берётся САМЫЙ ЧАСТЫЙ вид — то есть то, из чего робот состоит в основном.
        /// В журнал печатается вся таблица, чтобы выбор можно было проверить, а не поверить.
        /// </summary>
        private static Material FindSampleMaterial(StringBuilder sb)
        {
            UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(RobotSampleModelPath);
            if (all == null) return null;

            var candidates = new List<Material>();
            foreach (UnityEngine.Object o in all)
            {
                var material = o as Material;
                if (material == null || material.shader == null) continue;
                candidates.Add(material);
            }
            if (candidates.Count == 0) return null;

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var first = new Dictionary<string, Material>(StringComparer.Ordinal);

            sb.AppendLine("  материалы образца (" + RobotSampleModelPath + "):");
            foreach (Material m in candidates)
            {
                string key = Appearance(m);
                if (!counts.ContainsKey(key)) { counts[key] = 0; first[key] = m; }
                counts[key]++;
                sb.AppendLine("    «" + m.name + "» · " + key + " · шейдер " + ShaderName(m));
            }

            string bestKey = null;
            int best = -1;
            foreach (KeyValuePair<string, int> kv in counts)
            {
                if (kv.Value <= best) continue;
                best = kv.Value;
                bestKey = kv.Key;
            }

            Material sample = bestKey != null ? first[bestKey] : candidates[0];
            sb.AppendLine("  выбран образец: «" + sample.name + "» — самый частый внешний вид (" +
                          best + " из " + candidates.Count + " материалов)");
            return sample;
        }

        /// <summary>Внешний вид материала одной строкой: по нему и группируются образцы.</summary>
        private static string Appearance(Material m)
        {
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
            float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            float smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness")
                : (m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f);
            bool hasMap = (m.HasProperty("_BaseColorMap") && m.GetTexture("_BaseColorMap") != null) ||
                          (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) ||
                          (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null);
            return string.Format("цвет rgb({0:F2}; {1:F2}; {2:F2}) · металличность {3:F2} · гладкость {4:F2} · текстура: {5}",
                c.r, c.g, c.b, metallic, smoothness, hasMap ? "есть" : "нет");
        }

        private static Material CreateRobotMaterial(KvPipelineKind kind, Material sample, StringBuilder sb)
        {
            EnsureFolder(RobotMaterialFolder);
            string suffix = kind == KvPipelineKind.Urp ? "_URP" : "_HDRP";
            string path = RobotMaterialFolder + "/Robot" + suffix + ".mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                CopySurface(sample, existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                sb.AppendLine("  материал " + kind + ": «" + existing.name + "» обновлён по образцу");
                return existing;
            }

            Material material = KvRenderPipelineShaders.CreateLit(kind, "Robot" + suffix, Color.white);
            if (material == null) return null;

            CopySurface(sample, material);
            material.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            sb.AppendLine("  материал " + kind + ": «" + material.name + "» создан (" + path + ")");
            return material;
        }

        /// <summary>Назначить материал ВСЕМ рендерерам робота в сцене (и сохранить сцену).</summary>
        private static int AssignToRobot(string scenePath, Material material, StringBuilder sb)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                sb.AppendLine("  сцены " + scenePath + " нет — пропущено");
                return 0;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int changed = 0;
            try
            {
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                {
                    if (r == null || r.gameObject.scene != scene) continue;
                    if (!IsInsideObject(r.transform, RobotObjectName)) continue;

                    Material[] materials = r.sharedMaterials;
                    if (materials == null || materials.Length == 0) continue;

                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    r.sharedMaterials = materials;
                    EditorUtility.SetDirty(r);
                    changed++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                sb.AppendLine("  " + scenePath + ": рендереров робота переведено на «" + material.name +
                              "» — " + changed + " · сцена сохранена: " + saved);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded && !scene.isDirty)
                    EditorSceneManager.CloseScene(scene, true);
            }
            return changed;
        }

        private static bool IsInsideObject(Transform transform, string rootName)
        {
            Transform t = transform;
            while (t != null)
            {
                if (t.name == rootName) return true;
                t = t.parent;
            }
            return false;
        }

        /// <summary>
        /// ПОЧИНИТЬ ПЕРЕНАЗНАЧЕНИЕ В САМОЙ МОДЕЛИ. Пока в `Robot.fbx.meta` остаётся guid
        /// утраченного материала, ЛЮБОЙ новый экземпляр модели снова получит материал конвейера
        /// «по умолчанию» — то есть дефект вернётся в другой сцене. Переназначение меняется
        /// штатным API импортёра, а не правкой `.meta` текстом.
        /// </summary>
        private static void FixModelRemap(Material material, StringBuilder sb)
        {
            try
            {
                var importer = AssetImporter.GetAtPath(RobotModelPath) as ModelImporter;
                if (importer == null)
                {
                    sb.AppendLine("  ВНИМАНИЕ: импортёр " + RobotModelPath + " не найден — переназначение не изменено");
                    return;
                }

                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(
                    typeof(Material), "No Name"), material);
                importer.SaveAndReimport();
                sb.AppendLine("  переназначение в модели: «No Name» → «" + material.name + "» (" + RobotModelPath + ")");
            }
            catch (Exception e)
            {
                sb.AppendLine("  ВНИМАНИЕ: переназначение в модели не изменено (" + e.GetType().Name + ": " +
                              e.Message + "). Материал в сценах назначен явно — это главное.");
            }
        }
        // ================================================================== Б2: источники вариантов

        /// <summary>Путь к ассету глобальных настроек URP (в нём живёт настройка шейдеров террейна).</summary>
        public const string UrpGlobalSettingsPath = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";

        /// <summary>Путь к профилю объёма по умолчанию (URP применяет его ко всем камерам).</summary>
        public const string UrpDefaultVolumeProfilePath = "Assets/Settings/DefaultVolumeProfile.asset";

        /// <summary>
        /// ОТСЕЧЬ ЛИШНИЕ ИСТОЧНИКИ ВАРИАНТОВ ШЕЙДЕРОВ (карточка Б2, Этап А).
        ///
        /// ЗАЧЕМ. Сборка §30 шла 46 часов и не дошла до конца: из ~122 000 вариантов шейдеров
        /// ~105 000 приходилось на постобработку (`UberPost` — **92 160 вариантов на каждый
        /// из двух проходов**), ещё **17 472** — на шейдеры ТЕРРЕЙНА, которого в проекте нет
        /// ни одного (замер: проход `ForwardLit` — 13 824 варианта, 18 836 с). Это не «слабый
        /// компьютер»: это настройки проекта, и обе видны в логе сборки.
        ///
        /// ЧТО ДЕЛАЕТСЯ ЗДЕСЬ:
        ///   1) выключаются шейдеры террейна;
        ///   2) из профиля объёма по умолчанию убирается мусор: компоненты из тестовых сборок
        ///      Unity и посторонние объекты с потерянным скриптом.
        ///
        /// ЧЕГО ЗДЕСЬ НЕТ И ПОЧЕМУ. Профили HDRP и демо-сцены не чистятся: на них рабочий стол
        /// (`Assets/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset`), и его правка
        /// изменила бы вид на компьютере — этого владелец не разрешал. Варианты постобработки
        /// отсекает отдельный проход сборки (`KvPostProcessVariantStripper`, `IPreprocessShaders`),
        /// и он срабатывает ТОЛЬКО если в сценах сборки ни одна камера не рисует постобработку.
        ///
        /// Оба действия подтверждаются ПЕРЕЧИТЫВАНИЕМ, а не намерением (правило §29: «правка
        /// сделана только если её видно в сохранённом файле»).
        /// </summary>
        [MenuItem(MenuRoot + "7. Отсечь лишние варианты шейдеров (Б2)", false, 70)]
        public static void StripUnusedVariantSources()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvStrip] === ОТСЕЧЕНИЕ ЛИШНИХ ИСТОЧНИКОВ ВАРИАНТОВ (Б2) ===");
            sb.AppendLine("  шейдеры террейна: " + DisableTerrainShaders());
            sb.AppendLine("  профиль объёма по умолчанию: " + CleanDefaultVolumeProfile());
            sb.AppendLine("  варианты постобработки при сборке: " +
                (KvPostProcessVariantStripper.ProjectHasNoPostProcessingCameras()
                    ? "ОТСЕКАЮТСЯ — в сценах сборки постобработку никто не рисует"
                    : "НЕ отсекаются — в сценах сборки есть камера с постобработкой"));
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// ВЫКЛЮЧИТЬ ШЕЙДЕРЫ ТЕРРЕЙНА. Пробуются ДВА пути, и это не перестраховка: замер
        /// 08.10.2026 показал, что штатный доступ `GraphicsSettings.TryGetRenderPipelineSettings
        /// &lt;URPTerrainShaderSetting&gt;` в этом наборе пакетов возвращает **false** — настройка
        /// в ассете есть (`m_IncludeTerrainShaders`), а через API не находится. Правило проекта:
        /// «в файле написано» и «видно из кода» — разные ответы, и оба надо уметь получать.
        /// </summary>
        public static string DisableTerrainShaders()
        {
            // Путь 1 — штатный доступ к настройке URP.
            URPTerrainShaderSetting terrain;
            if (GraphicsSettings.TryGetRenderPipelineSettings<URPTerrainShaderSetting>(out terrain))
            {
                if (!terrain.includeTerrainShaders) return "уже выключены (правка не нужна)";

                terrain.includeTerrainShaders = false;
                AssetDatabase.SaveAssets();

                URPTerrainShaderSetting check;
                bool stillOn = GraphicsSettings.TryGetRenderPipelineSettings<URPTerrainShaderSetting>(out check) &&
                               check.includeTerrainShaders;
                if (!stillOn) return "выключены через API URP (подтверждено перечитыванием)";
            }

            // Путь 2 — правка самого ассета настроек (как в EnsureAndroidHasNoHdrpLevels).
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(UrpGlobalSettingsPath);
            if (asset == null) return "ассет настроек URP не найден: " + UrpGlobalSettingsPath;

            var so = new SerializedObject(asset);
            SerializedProperty property = FindPropertyByName(so.GetIterator(), "m_IncludeTerrainShaders");
            if (property == null)
                return "в настройках URP нет поля m_IncludeTerrainShaders — проверьте версию пакета";

            bool before = property.boolValue;
            if (!before) return "уже выключены (правка не нужна)";

            property.boolValue = false;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(UrpGlobalSettingsPath, ImportAssetOptions.ForceUpdate);

            var reread = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(UrpGlobalSettingsPath));
            SerializedProperty after = FindPropertyByName(reread.GetIterator(), "m_IncludeTerrainShaders");
            bool value = after != null && after.boolValue;

            return value
                ? "НЕ ВЫКЛЮЧИЛОСЬ (осталось включённым) — проверьте вручную"
                : "выключены правкой ассета настроек: было " + before + " → стало " + value +
                  " (террейнов в сценах сборки нет — им нечего рисовать)";
        }

        /// <summary>Найти свойство по имени во всём дереве сериализованного объекта.</summary>
        private static SerializedProperty FindPropertyByName(SerializedProperty iterator, string name)
        {
            while (iterator.Next(true))
                if (iterator.name == name) return iterator;
            return null;
        }

        /// <summary>
        /// УБРАТЬ ИЗ ПРОФИЛЯ ОБЪЁМА ПО УМОЛЧАНИЮ МУСОР. Мусор здесь ДВУХ видов, и второй нашёлся
        /// только при работе инструмента (08.10.2026):
        ///   1) компоненты СПИСКА профиля из тестовых сборок Unity (`…Editor.Tests`);
        ///   2) **посторонние объекты в самом ассете** — компоненты с потерянным скриптом
        ///      (`m_Script: {fileID: 0}`). Их НЕТ в списке `components` (в файле 28 блоков,
        ///      а компонентов профиля 19), поэтому чтение профиля их не видит: они лежат
        ///      в настройках проекта мёртвым грузом и находятся только через `LoadAllAssetsAtPath`.
        /// </summary>
        public static string CleanDefaultVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(UrpDefaultVolumeProfilePath);
            if (profile == null) return "профиль не найден: " + UrpDefaultVolumeProfilePath;

            int before = profile.components.Count;
            var removed = new List<string>();

            // 1. Компоненты списка профиля: убираем пришедшие из тестовых сборок.
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                VolumeComponent component = profile.components[i];

                if (component == null)
                {
                    removed.Add("пустая запись в списке компонентов");
                    profile.components.RemoveAt(i);
                    continue;
                }

                string type = component.GetType().FullName ?? string.Empty;
                if (type.IndexOf(".Tests", StringComparison.Ordinal) < 0) continue;

                removed.Add("компонент из тестовой сборки: " + component.GetType().Name);
                profile.components.RemoveAt(i);
                UnityEngine.Object.DestroyImmediate(component, true);
            }

            // 2. Посторонние объекты в ассете (потерянные скрипты) — то, чего не видно в списке.
            var all = AssetDatabase.LoadAllAssetsAtPath(UrpDefaultVolumeProfilePath);
            if (all != null)
            {
                foreach (UnityEngine.Object obj in all)
                {
                    if (obj == null) continue;
                    if (obj is VolumeProfile) continue;
                    if (obj is VolumeComponent) continue;

                    removed.Add("посторонний объект в ассете: " + obj.GetType().Name);
                    UnityEngine.Object.DestroyImmediate(obj, true);
                }
            }

            if (removed.Count == 0) return "мусора нет (компонентов " + before + ")";

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(UrpDefaultVolumeProfilePath, ImportAssetOptions.ForceUpdate);

            var check = AssetDatabase.LoadAssetAtPath<VolumeProfile>(UrpDefaultVolumeProfilePath);
            int after = check != null ? check.components.Count : -1;
            return string.Format("убрано {0} (компонентов было {1}, стало {2}): {3}",
                removed.Count, before, after, string.Join(" · ", removed.ToArray()));
        }

        // ============================================================ ШЕЙДЕРЫ ДЛЯ СБОРКИ (§32/Э2)

        /// <summary>
        /// ШЕЙДЕРЫ, КОТОРЫЕ СОЗДАЮТСЯ КОДОМ ВО ВРЕМЯ РАБОТЫ, — В СПИСОК «ВСЕГДА ВКЛЮЧЁННЫХ».
        ///
        /// КАКУЮ ЗАДАЧУ РЕШАЕТ. Лучи, метки, зоны достижимости, теплокарты, призраки, подсветка
        /// выбора и материал трубы создаются В РАНТАЙМЕ через `Shader.Find`. Он находит только те
        /// шейдеры, что вошли в сборку, а встроенные шейдеры стриппинг вырезает, если их не
        /// использует ни один материал сцены. Симптом на устройстве: на компьютере лучи и метки
        /// видны, в шлеме — нет; а `new Material(null)` вообще бросает ArgumentNullException
        /// (ровно это и произошло 08.10.2026 в `KvSelectionHighlight.Build`).
        ///
        /// ЧТО ДЕЛАЕТ. Дописывает в `ProjectSettings/GraphicsSettings.asset` →
        /// `m_AlwaysIncludedShaders` имена из `KvRenderPipelineShaders.RuntimeShaderNamesForBuild()`
        /// (единственный источник правды — он же у теста-«сторожа»). Печатает состояние ДО и ПОСЛЕ
        /// и отдельно называет то, чего в проекте нет вовсе (например, HDRP-шейдеров, если пакет
        /// вырезан): для таких имён фабрика возьмёт запасной шейдер того же конвейера.
        ///
        /// ЧЕГО НЕ ДЕЛАЕТ. Не трогает конвейеры, уровни качества и сцены — только список шейдеров.
        /// Пакетный прогон:
        /// `-batchmode -quit -projectPath … -executeMethod KazistovVvXrEditor.KvRenderPipelineTools.EnsureRuntimeShadersInBuild`
        /// </summary>
        [MenuItem(MenuRoot + "8. Шейдеры для материалов из кода — в сборку", false, 80)]
        public static void EnsureRuntimeShadersInBuild()
        {
            Debug.Log("[KvShaders] " + EnsureRuntimeShadersInBuildReport());
        }

        /// <summary>То же, но отчётом строкой: так его читают тесты и пакетные прогоны.</summary>
        public static string EnsureRuntimeShadersInBuildReport()
        {
            UnityEngine.Object graphics = GraphicsSettings.GetGraphicsSettings();
            if (graphics == null) return "НЕ НАЙДЕН объект GraphicsSettings — правка невозможна";

            var so = new SerializedObject(graphics);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return "в GraphicsSettings нет свойства m_AlwaysIncludedShaders";

            var already = new List<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var existing = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (existing != null) already.Add(existing.name);
            }
            int before = already.Count;

            var added = new List<string>();
            var missing = new List<string>();
            foreach (string name in KvRenderPipelineShaders.RuntimeShaderNamesForBuild())
            {
                if (already.Contains(name)) continue;

                Shader shader = Shader.Find(name);
                if (shader == null) { missing.Add(name); continue; }

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                added.Add(name);
            }

            if (added.Count > 0)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }

            var text = new StringBuilder();
            text.Append("шейдеры для рантайм-материалов: было ").Append(before)
                .Append(", добавлено ").Append(added.Count)
                .Append(", стало ").Append(before + added.Count);
            text.Append(added.Count > 0
                ? " · добавлены: " + string.Join(", ", added.ToArray())
                : " · добавлять нечего (все уже в списке)");
            if (missing.Count > 0)
            {
                text.Append(" · НЕТ В ПРОЕКТЕ: ").Append(string.Join(", ", missing.ToArray()))
                    .Append(" — для этих имён фабрика возьмёт запасной шейдер того же конвейера");
            }
            return text.ToString();
        }
    }
}

