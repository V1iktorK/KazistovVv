#if UNITY_EDITOR
using System.IO;
using KazistovVvUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>СНИМОК РАНТАЙМ-ИНТЕРФЕЙСА (диагностика, рабочий).</summary>
///
/// ПОЧЕМУ ТАК, А НЕ ScreenCapture.CaptureScreenshot: в -batchmode нет окна/swapchain —
/// запрос CaptureScreenshot молча не создаёт файл (проверено 27.10.2026), а выход через
/// EditorApplication.ExitPlaymode() зависает (§0.7.7). Поэтому:
///   1) вход в PlayMode — паттерн рабочего DshDesktopUiDiag (явное открытие MainScene → EnterPlaymode);
///   2) снимок — рендер во временный RenderTexture тем же паттерном, что DshScreenshotsDiag.Render:
///      Canvas «KazistovVvCanvas» временно переводится в ScreenSpaceCamera, одна камера
///      рендерит СЦЕНУ + ИНТЕРФЕЙС в RT → ReadPixels → PNG;
///   3) выход — EditorApplication.Exit(0) (как в рабочих диагах; без ExitPlaymode).
///
/// ЗАПУСК (без -nographics и без -quit):
///   Unity.exe -batchmode -projectPath "&lt;проект&gt;" -executeMethod KvUiShotDiag.Run -logFile _kv_ui_shot.log
///
/// РЕЗУЛЬТАТ: &lt;проект&gt;/Docs/Screenshots/runtime_UI.png
/// Безопасность: сцена НЕ сохраняется, режим канваса восстанавливается, ничего не пишется в проект.
/// </summary>
public static class KvUiShotDiag
{
    private const string SessionKey = "KvUiShotDiag.Active";
    private const string ScenePath = "Assets/_Project/00_Scenes/MainScene.unity";
    private const int WaitFrames = 180;   // кадров на построение оболочки (запас)
    private const int MaxFrames = 1200;
    private const int Width = 1600;
    private const int Height = 900;

    private static int frames;
    private static bool shotDone;
    private static int phase;          // 0: ждём построения → 1: показали UI, ждём проявления → 2: снимок

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        frames++;

        if (!shotDone && phase == 0 && frames >= WaitFrames)
        {
            // Фаза 0: оболочка построилась — принудительно показываем UI
            // (в batchmode он стартует скрытым: graphic.color.a = 0 у всех 875 элементов).
            try
            {
                KazistovVvUIManager.SetUiVisible(true);
                Debug.Log("[KvUiShot] SetUiVisible(true) вызван, ждём проявления...");
            }
            catch (System.Exception e) { Debug.LogError("[KvUiShot] SetUiVisible: " + e); }
            phase = 1;
            frames = 0;
            return;
        }

        if (!shotDone && phase == 1 && frames >= 90)
        {
            // Фаза 1: даём 90 кадров на завершение проявления панелей
            shotDone = true;
            try { Shot(); }
            catch (System.Exception e) { Debug.LogError("[KvUiShot] " + e); }
            Finish(0);
            return;
        }

        if (frames >= MaxFrames)
        {
            Debug.LogError("[KvUiShot] таймаут " + MaxFrames + " кадров, фаза " + phase);
            Finish(2);
        }
    }

    private static void Shot()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            // 30.10.2026: `FindObjectOfType` и `FindObjectsByType(…, FindObjectsSortMode)` помечены
            // устаревшими в Unity 6 — это давало 10 предупреждений CS0618 на каждую сборку (два
            // прохода компиляции). Замена эквивалентна: прежний `FindObjectsSortMode.None`
            // сортировку и не выполнял.
            cam = Object.FindAnyObjectByType<Camera>();
        }
        if (cam == null)
        {
            Debug.LogError("[KvUiShot] камера не найдена");
            return;
        }

        // Канвас — дочерний объект менеджера (паттерн DshStage2Diag: ui.transform.Find).
        Canvas canvas = null;
        KazistovVvUIManager ui = KazistovVvUIManager.Instance;
        if (ui != null)
        {
            Transform t = ui.transform.Find("KazistovVvCanvas");
            if (t != null) canvas = t.GetComponent<Canvas>();
            Debug.Log("[KvUiShot] менеджер: " + (ui != null) + ", дочерний канвас: " + (canvas != null));
        }
        if (canvas == null)
        {
            Canvas[] all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            foreach (Canvas c in all)
            {
                Debug.Log("[KvUiShot] найден Canvas: " + c.gameObject.name +
                          " mode=" + c.renderMode + " active=" + c.gameObject.activeInHierarchy);
                if (canvas == null && c.gameObject.name.Contains("KazistovVvCanvas")) canvas = c;
            }
        }
        if (canvas == null)
        {
            Debug.LogError("[KvUiShot] KazistovVvCanvas не найден (Canvas всего: " +
                Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Length +
                ", менеджер: " + (KazistovVvUIManager.Instance != null) + ")");
            return;
        }

        RenderMode prevMode = canvas.renderMode;
        Camera prevWorldCam = canvas.worldCamera;
        float prevPlane = canvas.planeDistance;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        bool scalerWasEnabled = scaler != null && scaler.enabled;
        Vector3 prevScale = canvas.transform.localScale;

        // -- ДИАГНОСТИКА: почему magenta / где интерфейс --------------------
        {
            string srp = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.GetType().Name : "builtin";
            Debug.Log("[KvUiShot] диаг: уровень качества " + QualitySettings.GetQualityLevel() +
                "/" + QualitySettings.names.Length + " (" + string.Join(",", QualitySettings.names) + ")" +
                " · SRP=" + srp +
                " · Screen=" + Screen.width + "x" + Screen.height +
                " · cam.pixel=" + cam.pixelWidth + "x" + cam.pixelHeight +
                " · fov=" + cam.fieldOfView);
        }

        RenderTexture rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 2;
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = cam.targetTexture;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = Mathf.Max(1f, cam.nearClipPlane + 0.5f);
            // CanvasScaler в batchmode получает Screen=0x0 → scaleFactor=0 → UI схлопывается.
            // На время снимка скалер выключается, масштаб ставится вручную по FOV камеры.
            if (scaler != null) scaler.enabled = false;
            Canvas.ForceUpdateCanvases();

            RectTransform rtRect = canvas.transform as RectTransform;
            float rectH = rtRect != null && rtRect.rect.height > 1f ? rtRect.rect.height : 1080f;
            float visibleH = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * canvas.planeDistance;
            float uiScale = visibleH / rectH;
            canvas.transform.localScale = Vector3.one * uiScale;
            Canvas.ForceUpdateCanvases();
            Debug.Log("[KvUiShot] канвас rect=" + (rtRect != null ? rtRect.rect.ToString() : "?") +
                " · visibleH=" + visibleH.ToString("0.###") + " · scale=" + uiScale.ToString("0.#####"));

            // -- РАЗВЕДКА: что внутри канваса и почему UI не виден -------------
            {
                int total = canvas.transform.childCount;
                int active = 0;
                string names = "";
                for (int i = 0; i < total && i < 8; i++)
                {
                    Transform ch = canvas.transform.GetChild(i);
                    if (ch.gameObject.activeSelf) active++;
                    names += ch.name + (ch.gameObject.activeSelf ? "" : "(выкл)") + " ";
                }
                CanvasGroup[] groups = canvas.GetComponentsInChildren<CanvasGroup>(true);
                float minAlpha = 1f;
                foreach (CanvasGroup g in groups) minAlpha = Mathf.Min(minAlpha, g.alpha);
                UnityEngine.UI.Graphic[] graphics =
                    canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                int visibleGraphics = 0;
                foreach (UnityEngine.UI.Graphic gr in graphics)
                    if (gr.gameObject.activeInHierarchy && gr.color.a > 0.01f) visibleGraphics++;
                Debug.Log("[KvUiShot] канвас детей=" + total + " активных=" + active +
                    " · CanvasGroup=" + groups.Length + " мин.alpha=" + minAlpha +
                    " · Graphic=" + graphics.Length + " видимых=" + visibleGraphics +
                    " · near=" + cam.nearClipPlane + " far=" + cam.farClipPlane +
                    " · дети: " + names);
            }

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();

            string dir = Path.Combine(Application.dataPath, "..", "Docs", "Screenshots");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "runtime_UI.png");
            File.WriteAllBytes(file, png);
            Debug.Log("[KvUiShot] СНЯТО: " + Path.GetFullPath(file) + " (" + png.Length + " байт)");

            // Если поднялся URP-уровень (batch-PlayMode) — материалы сцены magenta.
            // Пробуем уровни по порядку, пока SRP не сменится с Universal; проверка — по факту.
            string srpNow = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.GetType().Name : "builtin";
            if (srpNow.Contains("Universal"))
            {
                int prevLevel = QualitySettings.GetQualityLevel();
                for (int cand = 1; cand <= 3; cand++)
                {
                    if (cand == prevLevel) continue;
                    try { QualitySettings.SetQualityLevel(cand, false); }
                    catch (System.Exception e) { Debug.Log("[KvUiShot] уровень " + cand + " недоступен: " + e.Message); continue; }
                    string srpCheck = GraphicsSettings.currentRenderPipeline != null
                        ? GraphicsSettings.currentRenderPipeline.GetType().Name : "builtin";
                    Debug.Log("[KvUiShot] проба уровня " + cand + " -> SRP=" + srpCheck);
                    if (srpCheck.Contains("Universal")) continue;

                    Debug.Log("[KvUiShot] HDRP-уровень найден (" + cand + ") — переснимок");
                    Canvas.ForceUpdateCanvases();
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    Texture2D tex2 = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    tex2.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    tex2.Apply();
                    byte[] png2 = tex2.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(dir, "runtime_UI_pc.png"), png2);
                    Debug.Log("[KvUiShot] ПЕРЕСНЯТО: runtime_UI_pc.png (" + png2.Length + " байт)");
                    Object.DestroyImmediate(tex2);
                    break;
                }
            }

            Object.DestroyImmediate(tex);
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            canvas.renderMode = prevMode;
            canvas.worldCamera = prevWorldCam;
            canvas.planeDistance = prevPlane;
            canvas.transform.localScale = prevScale;
            if (scaler != null) scaler.enabled = scalerWasEnabled;
            Object.DestroyImmediate(rt);
        }
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(SessionKey, false);
        EditorApplication.Exit(code);
    }
}
#endif
