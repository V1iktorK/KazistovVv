using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KazistovVvXr;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// НАСТРОЙКА ИНТЕРФЕЙСА XR-СЦЕНЫ (сессия §35, карточки Б19 и Б20). РЕДАКТОРНЫЙ ИНСТРУМЕНТ.
    ///
    /// ЧТО ДЕЛАЕТ, ПО ШАГАМ:
    ///   1. МЕНЯЕТ ПАНЕЛЬ. Снимает с объекта `XrQuickPanel` прежний компонент `KvXrQuickPanel`
    ///      (8 плоских кнопок с постоянными ПОДПИСЯМИ, показ ПЕРЕД ГЛАЗАМИ при отсутствии руки)
    ///      и ставит `KvXrGroupPanel` — обобщающие ИКОНКИ групп, подпись по удержанию 0,5 с,
    ///      веер иконок группы по нажатию, крепление к контроллеру и пояс как запасное положение.
    ///      Имя объекта СОХРАНЯЕТСЯ: по нему панель ищут другие части проекта.
    ///   2. УБИРАЕТ ДЕСКТОПНЫЙ ОВЕРЛЕЙ. Удаляет компонент `KvXrPanelAnchor` — это он переводил
    ///      проектный канвас (дерево, окна, тулбар) в мировое пространство и вешал его перед
    ///      оператором. Именно это владелец и назвал «интерфейсом для ПК» в шлеме.
    ///   3. СТАВИТ СТОРОЖА `KvXrDesktopOverlayGuard`. Он гасит РИСОВАНИЕ проектного канваса,
    ///      но НЕ выключает менеджер интерфейса: на нём же наполняется РЕЕСТР КОМАНД, и его
    ///      выключение оставило бы панель из пункта 1 без единой команды — молча, без ошибок.
    ///
    /// ВСЁ ЭТО ДЕЛАЕТСЯ ТОЛЬКО В `XrMain`. Десктопная сцена (`MainScene`) не открывается даже
    /// на запись, поэтому десктопный режим этим инструментом не затрагивается ([S3.1], [S3.2]).
    /// </summary>
    public static class KvXrInterfaceSetupTools
    {
        private const string MenuRoot = "Tools/KazistovVv XR/";
        private const string XrScenePath = "Assets/_Project/00_Scenes/XrMain.unity";

        /// <summary>Имя объекта-сторожа в XR-сцене.</summary>
        public const string GuardObjectName = "XrOverlayGuard";

        [MenuItem(MenuRoot + "10. Интерфейс XR: панель групп и скрытие десктопного оверлея", false, 82)]
        public static void SetupXrInterface()
        {
            var log = new StringBuilder();
            log.AppendLine("[KvXr] === НАСТРОЙКА ИНТЕРФЕЙСА XR-СЦЕНЫ ===");

            Scene xr = EditorSceneManager.OpenScene(XrScenePath, OpenSceneMode.Single);

            // --- 1. ПАНЕЛЬ
            Camera xrCamera = FindXrCamera();
            log.AppendLine("  камера шлема: " + (xrCamera != null ? xrCamera.name : "НЕ НАЙДЕНА"));

            var oldPanels = Object.FindObjectsByType<KvXrQuickPanel>(FindObjectsInactive.Include);
            foreach (KvXrQuickPanel old in oldPanels)
            {
                if (old == null) continue;
                GameObject host = old.gameObject;
                string hostName = host.name;          // имя читается ДО уничтожения компонента
                Object.DestroyImmediate(old, true);
                var panel = host.GetComponent<KvXrGroupPanel>();
                if (panel == null) panel = host.AddComponent<KvXrGroupPanel>();
                if (xrCamera != null) panel.headCamera = xrCamera;
                EditorUtility.SetDirty(panel);
                log.AppendLine("  панель: на «" + hostName + "» поставлен " + nameof(KvXrGroupPanel) +
                               " вместо " + nameof(KvXrQuickPanel) + " (имя объекта сохранено)");
            }
            if (oldPanels.Length == 0)
            {
                log.AppendLine("  прежней панели в сцене нет — проверяю, есть ли уже панель групп");
                var existing = Object.FindObjectsByType<KvXrGroupPanel>(FindObjectsInactive.Include);
                foreach (KvXrGroupPanel p in existing)
                {
                    if (p == null) continue;
                    if (xrCamera != null) p.headCamera = xrCamera;
                    EditorUtility.SetDirty(p);
                    log.AppendLine("  панель групп уже стоит на «" + p.gameObject.name + "»");
                }
            }

            // --- 2. ДЕСКТОПНЫЙ ОВЕРЛЕЙ
            var anchors = Object.FindObjectsByType<KvXrPanelAnchor>(FindObjectsInactive.Include);
            foreach (KvXrPanelAnchor anchor in anchors)
            {
                if (anchor == null) continue;
                GameObject host = anchor.gameObject;
                // ИМЯ ЗАПОМИНАЕТСЯ ДО УНИЧТОЖЕНИЯ. Прошлая версия читала `host.name` ПОСЛЕ
                // `DestroyImmediate(host)` — и пакетный запуск падал с MissingReferenceException,
                // так что до сохранения сцены дело не доходило вовсе.
                string hostName = host.name;

                Object.DestroyImmediate(anchor, true);

                bool stillUsed = host.GetComponents<Component>().Length > 1;   // остался только Transform
                if (!stillUsed)
                {
                    Object.DestroyImmediate(host);
                    log.AppendLine("  оверлей: объект «" + hostName + "» убран целиком (в нём не осталось компонентов)");
                }
                else
                {
                    log.AppendLine("  оверлей: с «" + hostName + "» снят компонент " + nameof(KvXrPanelAnchor));
                }
            }
            if (anchors.Length == 0)
                log.AppendLine("  оверлей: " + nameof(KvXrPanelAnchor) + " в сцене нет (уже убран)");

            // --- 3. СТОРОЖ
            KvXrDesktopOverlayGuard guard = null;
            var guards = Object.FindObjectsByType<KvXrDesktopOverlayGuard>(FindObjectsInactive.Include);
            if (guards.Length > 0) guard = guards[0];
            if (guard == null)
            {
                var go = new GameObject(GuardObjectName);
                SceneManager.MoveGameObjectToScene(go, xr);
                guard = go.AddComponent<KvXrDesktopOverlayGuard>();
                log.AppendLine("  сторож оверлея: создан объект «" + GuardObjectName + "»");
            }
            else
            {
                log.AppendLine("  сторож оверлея: уже стоит на «" + guard.gameObject.name + "»");
            }
            guard.neutralizePanelAnchor = true;
            guard.checkIntervalSeconds = 1f;
            guard.logOnGuard = true;
            EditorUtility.SetDirty(guard);

            // --- 4. СОХРАНЕНИЕ
            EditorSceneManager.MarkSceneDirty(xr);
            EditorSceneManager.SaveScene(xr);
            AssetDatabase.SaveAssets();
            log.AppendLine("  XrMain сохранена: " + XrScenePath);

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// ОДНА КОМАНДА НА ВСЮ ПОДГОТОВКУ XR-СЦЕНЫ: поставить ангар и настроить интерфейс.
        /// Нужна потому, что запуск редактора стоит минуты и память, а сборка APK — час:
        /// две отдельные подготовки к одной сборке смысла не имеют.
        /// </summary>
        public static void BuildHangarAndInterfaceForXr()
        {
            KvXrHangarBuilder.BuildHangarInXrScene();
            SetupXrInterface();
            SetupHeadTracking();
        }

        /// <summary>
        /// КАМЕРА ШЛЕМА СЛЕДУЕТ ЗА ГОЛОВОЙ (18.10.2026). В XR-сцене НЕ БЫЛО ни
        /// `TrackedPoseDriver`, ни `XROrigin` — проверено поиском по файлу сцены (0 вхождений),
        /// поэтому позу головы на камеру не переносил никто. Журнал шлема показал прямо:
        ///     камеры: «XrCamera» (0.00, 0.00, 0.00) · поза шлема (-0.73, 1.54, -0.22)
        /// — то есть мир рисовался с точки на ПОЛУ, а голова была на 1,54 м в другом месте.
        /// Отсюда все жалобы владельца: «камера не в той позиции», «я в 0 по высоте»,
        /// «плохо поворачивается».
        /// </summary>
        [MenuItem(MenuRoot + "11. Камера: следовать за головой (KvXrHeadTracking)", false, 83)]
        public static void SetupHeadTracking()
        {
            var log = new StringBuilder();
            log.AppendLine("[KvXr] === КАМЕРА СЛЕДУЕТ ЗА ГОЛОВОЙ ===");

            Scene xr = EditorSceneManager.OpenScene(XrScenePath, OpenSceneMode.Single);

            Camera cam = FindXrCamera();
            if (cam == null)
            {
                log.AppendLine("  ОШИБКА: камера шлема в сцене не найдена");
                Debug.LogError(log.ToString());
                return;
            }

            var tracking = cam.GetComponent<KvXrHeadTracking>();
            if (tracking == null) tracking = cam.gameObject.AddComponent<KvXrHeadTracking>();
            tracking.headCamera = cam;
            tracking.logOnFirstApply = true;
            EditorUtility.SetDirty(tracking);

            log.AppendLine("  на камеру «" + cam.name + "» поставлен " + nameof(KvXrHeadTracking) +
                           " (в сцене не было ни TrackedPoseDriver, ни XROrigin)");

            EditorSceneManager.MarkSceneDirty(xr);
            EditorSceneManager.SaveScene(xr);
            AssetDatabase.SaveAssets();
            log.AppendLine("  XrMain сохранена: " + XrScenePath);

            Debug.Log(log.ToString());
        }

        /// <summary>Камера шлема в XR-сцене: по имени `XrCamera`, иначе любая активная.</summary>
        private static Camera FindXrCamera()
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].name == "XrCamera") return cameras[i];
            }
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].enabled) return cameras[i];
            }
            return cameras.Length > 0 ? cameras[0] : null;
        }
    }
}
