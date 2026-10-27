using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// ОСВЕЩЕНИЕ И ФОН XR-СЦЕНЫ (сессия §33, дефект, найденный на живом шлеме 15.10.2026).
    ///
    /// ЧТО УВИДЕЛ ВЛАДЕЛЕЦ В ШЛЕМЕ: «всё чёрное, только оверлей программы с квадратиками».
    /// Интерфейс — единственное, что было видно, и это объяснимо: интерфейс рисуется БЕЗ освещения
    /// (unlit), а вся геометрия сцены — нет.
    ///
    /// ПРИЧИНА, найденная разбором сцены (два независимых дефекта, оба в `XrMain.unity`):
    ///   1) **в сцене НОЛЬ источников света** (`!u!108` — ни одного; в десктопной `MainScene` их 3).
    ///      Роботы и столы стоят, но освещать их нечем: под URP материал без света рисуется чёрным;
    ///   2) **камера чистит фон в чёрный цвет** (`m_ClearFlags: 2` — Solid Color, цвет `{0,0,0,1}`),
    ///      поэтому за объектами не видно ни неба, ни окружения — только чернота.
    ///
    /// ПОЧЕМУ ЭТО НЕ ПОЙМАЛИ РАНЬШЕ. Сцена собиралась кодом (§28: риг, панель, MR-комната;
    /// §29: стенды и роботы), а свет в неё никто не добавлял: на компьютере XR-сцена не запускалась,
    /// а тесты проверяли состав компонентов и материалы — но не освещённость. Первый запуск
    /// на устройстве показал это сразу: «видно только интерфейс».
    ///
    /// ЧТО ДЕЛАЕТ ИНСТРУМЕНТ:
    ///   • добавляет два направленных источника света (ключевой и заполняющий) — как студийная
    ///     схема десктопа, но средствами URP;
    ///   • переводит фон камеры на небо (Skybox) и включает окружающий свет от него — вместо
    ///     чёрной пустоты;
    ///   • идемпотентен: повторный запуск не создаёт дубликаты (ищет объекты по именам);
    ///   • печатает отчёт «до/после» числами.
    /// </summary>
    public static class KvXrLightingTools
    {
        private const string MenuRoot = "Tools/KazistovVv XR/";
        private const string XrScenePath = "Assets/_Project/00_Scenes/XrMain.unity";

        private const string KeyLightName = "XrKeyLight";
        private const string FillLightName = "XrFillLight";

        /// <summary>Ключевой свет: солнце сцены, даёт тени и объём.</summary>
        private static readonly Vector3 KeyRotation = new Vector3(45f, -35f, 0f);

        /// <summary>Заполняющий свет: подсвечивает теневую сторону, теней не отбрасывает.</summary>
        private static readonly Vector3 FillRotation = new Vector3(20f, 150f, 0f);

        /// <summary>
        /// ТОЧКА ВХОДА ПАКЕТНОГО ПРОГОНА: привести освещение XR-сцены в рабочее состояние.
        /// </summary>
        public static void EnsureXrSceneLighting()
        {
            if (!System.IO.File.Exists(XrScenePath))
            {
                Debug.LogError("[KvLight] Нет XR-сцены " + XrScenePath + " — освещение не добавлено.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[KvLight] === ОСВЕЩЕНИЕ XR-СЦЕНЫ ===");

            Scene scene = EditorSceneManager.OpenScene(XrScenePath, OpenSceneMode.Single);
            try
            {
                int before = CountLights(scene);
                sb.AppendLine("  источников света до правки: " + before);

                Light key = EnsureLight(scene, KeyLightName, KeyRotation, 1.35f, LightShadows.Soft, sb);
                Light fill = EnsureLight(scene, FillLightName, FillRotation, 0.45f, LightShadows.None, sb);

                // ФОН КАМЕРЫ: небо вместо чёрной пустоты.
                foreach (Camera camera in Object.FindObjectsByType<Camera>(
                             FindObjectsInactive.Include))
                {
                    if (camera == null || camera.gameObject.scene != scene) continue;
                    CameraClearFlags was = camera.clearFlags;
                    camera.clearFlags = CameraClearFlags.Skybox;
                    EditorUtility.SetDirty(camera);
                    sb.AppendLine("  камера «" + camera.name + "»: фон " + was + " → Skybox");
                }

                // ОКРУЖАЮЩИЙ СВЕТ ОТ НЕБА: без него теневые стороны становятся чёрными.
                UnityEngine.Rendering.AmbientMode wasAmbient = RenderSettings.ambientMode;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1f;
                sb.AppendLine("  окружающий свет: " + wasAmbient + " → Skybox (интенсивность 1)");

                int after = CountLights(scene);
                sb.AppendLine("  источников света после правки: " + after);
                sb.AppendLine("  ключевой: " + (key != null ? Describe(key) : "НЕ СОЗДАН"));
                sb.AppendLine("  заполняющий: " + (fill != null ? Describe(fill) : "НЕ СОЗДАН"));

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                sb.AppendLine("  сцена сохранена: " + saved);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded && !scene.isDirty)
                    EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem(MenuRoot + "8. Освещение XR-сцены (свет и фон)", false, 80)]
        public static void EnsureXrSceneLightingMenu()
        {
            EnsureXrSceneLighting();
        }

        // ---------------------------------------------------------------- вспомогательное

        private static int CountLights(Scene scene)
        {
            int count = 0;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (light != null && light.gameObject.scene == scene) count++;
            return count;
        }

        private static Light EnsureLight(Scene scene, string name, Vector3 rotation, float intensity,
            LightShadows shadows, StringBuilder sb)
        {
            Light found = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light == null || light.gameObject.scene != scene) continue;
                if (light.name != name) continue;
                found = light;
                break;
            }

            if (found == null)
            {
                var go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, scene);
                found = go.AddComponent<Light>();
                Undo.RegisterCreatedObjectUndo(go, "Освещение XR-сцены");
                sb.AppendLine("  создан «" + name + "»");
            }
            else
            {
                sb.AppendLine("  «" + name + "» уже есть — настройки обновлены");
            }

            found.type = LightType.Directional;
            found.transform.rotation = Quaternion.Euler(rotation);
            found.intensity = intensity;
            found.shadows = shadows;
            found.color = Color.white;
            found.lightmapBakeType = LightmapBakeType.Realtime;
            EditorUtility.SetDirty(found);
            return found;
        }

        private static string Describe(Light light)
        {
            return string.Format("«{0}» {1} · поворот ({2:0}°, {3:0}°, {4:0}°) · интенсивность {5:0.00} · тени {6}",
                light.name, light.type,
                light.transform.rotation.eulerAngles.x, light.transform.rotation.eulerAngles.y,
                light.transform.rotation.eulerAngles.z,
                light.intensity, light.shadows);
        }
    }
}
