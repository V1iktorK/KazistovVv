#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ЭТАП 7 (аддитивная диагностика агента): снимки сцен и ключевых объектов.
///
/// ЗАПУСК (БЕЗ -nographics — нужен настоящий графический контекст):
/// <code>
/// Unity.exe -batchmode -projectPath "&lt;проект&gt;" -executeMethod DshScreenshotsDiag.Run -logFile _dsh_shots.log
/// </code>
///
/// ЧТО ДЕЛАЕТ:
///   1) открывает КАЖДУЮ сцену проекта ПРИСОЕДИНЁННО (Additive) и рендерит её в PNG
///      двумя ракурсами — «обзор» (по габаритам всех рендереров) и «сверху»;
///   2) для MainScene дополнительно снимает КРУПНО каждый корневой объект с рендерерами
///      (робот, SCARA, стенды, столы, свет) — это «ключевые объекты сцены»;
///   3) затем входит в PlayMode и снимает РАНТАЙМ-ИНТЕРФЕЙС через ScreenCapture
///      (оболочка строится кодом и является ScreenSpaceOverlay, поэтому снять её
///      рендером камеры невозможно);
///   4) пишет подписи в Docs/Screenshots/CAPTIONS.md и полный журнал в Docs/Screenshots/_log.txt.
///
/// БЕЗОПАСНОСТЬ:
///   * ни одна сцена НЕ сохраняется — открытые сцены закрываются с removeScene: true,
///     все изменения (позиция камеры, временные объекты) отбрасываются;
///   * создаваемые объекты помечены HideFlags.HideAndDontSave и уничтожаются;
///   * логика проекта не меняется — файл только читает сцену и рендерит её.
/// </summary>
public static class DshScreenshotsDiag
{
    private const string OutDir = "Docs/Screenshots";
    private const int Width = 1600;
    private const int Height = 900;

    private static readonly string[] Scenes =
    {
        "Assets/_Project/00_Scenes/MainScene.unity",
        "Assets/_Project/00_Scenes/Boot.unity",
        "Assets/_Project/00_Scenes/Main Menu.unity",
        "Assets/_Project/00_Scenes/Simulation.unity",
        "Assets/_Recovery/0.unity",
        "Assets/_Recovery/0 (1).unity",
        "Assets/_Recovery/0 (2).unity",
        "Assets/_Recovery/0 (3).unity",
        "Assets/_Recovery/0 (4).unity",
        "Assets/_Recovery/0 (5).unity"
    };

    private class Shot
    {
        public string File;
        public string Scene;
        public string What;
        public string Note;
    }

    private static readonly List<Shot> shots = new List<Shot>();
    private static readonly StringBuilder log = new StringBuilder();
    private static int sceneIndex;
    private static int subShot;
    private static int phase;          // 0 = сцены, 1 = playmode, 2 = готово
    private static int waitFrames;
    private static int playShots;
    private static int idleTicks;
    private static bool wantPlayMode;
    private static GameObject tempCamGo;
    private static Camera tempCam;
    private static string lastSceneName = "";

    // ------------------------------------------------------------------ запуск

    /// <summary>
    /// Основной вход: снимки ВСЕХ сцен (без PlayMode).
    /// Именно так снимался отчёт 15.09.2026 — 16 PNG в Docs/Screenshots.
    /// </summary>
    public static void Run()
    {
        wantPlayMode = false;
        Start();
    }

    /// <summary>
    /// Дополнительный вход: дополнительно пытается снять рантайм-интерфейс в PlayMode.
    ///
    /// ВНИМАНИЕ (проверено 15.09.2026): в batch-режиме БЕЗ -nographics вход в PlayMode на этой
    /// машине не завершается — процесс Unity уходит в бесконечный цикл плеера и не возвращается
    /// в EditorApplication.update, поэтому снимки интерфейса получить не удалось (процесс пришлось
    /// снять вручную после ~40 минут). Используйте этот вход только для ручной проверки.
    /// </summary>
    public static void RunWithPlayMode()
    {
        wantPlayMode = true;
        Start();
    }

    private static void Start()
    {
        Directory.CreateDirectory(OutDir);
        Line("=== DshScreenshotsDiag · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
        Line("режим: " + (wantPlayMode ? "сцены + PlayMode" : "только сцены"));
        Line("проект: " + Application.dataPath);
        Line("графическое устройство: " + SystemInfo.graphicsDeviceType + " · " + SystemInfo.graphicsDeviceName);
        Line("сцен в списке: " + Scenes.Length);
        EditorApplication.update += Tick;
        Tick();
    }

    private static void Line(string s)
    {
        log.AppendLine(s);
        Debug.Log("[Shots] " + s);
    }

    // ------------------------------------------------------------------ автомат

    private static void Tick()
    {
        try
        {
            if (phase == 0) { StepScenes(); return; }
            if (phase == 1) { StepPlayMode(); return; }
            Finish(0);
        }
        catch (Exception e)
        {
            Line("ИСКЛЮЧЕНИЕ: " + e);
            Finish(2);
        }
    }

    // ------------------------------------------------------------------ фаза 1: сцены

    private static void StepScenes()
    {
        if (sceneIndex >= Scenes.Length)
        {
            if (!wantPlayMode)
            {
                Line("--- все сцены отсняты (PlayMode не запрошен) ---");
                Finish(0);
                return;
            }
            phase = 1;
            waitFrames = 5;
            Line("--- сцены отсняты, переход в PlayMode ---");
            EditorApplication.EnterPlaymode();
            return;
        }

        string path = Scenes[sceneIndex];
        if (!File.Exists(path))
        {
            Line("нет файла сцены: " + path);
            sceneIndex++; subShot = 0;
            return;
        }

        Scene scene = default(Scene);
        try { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
        catch (Exception e) { Line("не открылась " + path + ": " + e.Message); sceneIndex++; subShot = 0; return; }

        lastSceneName = scene.name;

        if (subShot == 0)
        {
            Bounds b;
            if (!TryBounds(scene, out b))
            {
                Line("в сцене " + scene.name + " нет рендереров — пропуск");
                EditorSceneManager.CloseScene(scene, true);
                sceneIndex++; subShot = 0;
                return;
            }

            Camera cam = AcquireCamera(scene);
            if (cam == null) { Line("нет камеры для " + scene.name); EditorSceneManager.CloseScene(scene, true); sceneIndex++; subShot = 0; return; }

            // 1) обзор по габаритам
            Render(cam, b.center, new Vector3(1f, 0.55f, -1f), b, scene.name + "_обзор",
                   "общий вид сцены (камера по габаритам всех рендереров)");
            // 2) вид сверху
            Render(cam, b.center, new Vector3(0.001f, 1f, 0.001f), b, scene.name + "_сверху",
                   "вид сверху (планировка стендов и столов)");
            subShot = 1;
            return;
        }

        // 3) для MainScene — крупные планы корневых объектов
        if (scene.name == "MainScene")
        {
            int shots = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (shots >= 8) break;
                if (!HasRenderer(root)) continue;
                Bounds rb;
                if (!TryBounds(root, out rb)) continue;
                if (rb.size.magnitude < 0.05f) continue;

                Camera cam = AcquireCamera(scene);
                Render(cam, rb.center, new Vector3(1f, 0.5f, -1f), rb,
                       "MainScene_объект_" + Sanitize(root.name),
                       "крупный план корневого объекта сцены «" + root.name + "»");
                shots++;
            }
            Line("MainScene: крупных планов " + shots);
        }

        EditorSceneManager.CloseScene(scene, true);
        sceneIndex++;
        subShot = 0;
    }

    // ------------------------------------------------------------------ фаза 2: PlayMode

    private static void StepPlayMode()
    {
        if (waitFrames > 0) { waitFrames--; return; }

        if (!EditorApplication.isPlaying)
        {
            // ждём входа; если долго не входит — прекращаем (idleTicks растёт ТОЛЬКО здесь,
            // чтобы не путать его со счётчиком снимков playShots)
            idleTicks++;
            if (idleTicks > 3000) { Line("PlayMode не запустился — интерфейс не отснят"); Finish(0); }
            return;
        }

        // ждём, пока оболочка интерфейса соберётся
        if (playShots == 0)
        {
            Line("PlayMode: " + Screen.width + "x" + Screen.height + " · интерфейс: " +
                 (KazistovVvUI.KazistovVvUIManager.Instance != null ? "есть" : "нет"));
            waitFrames = 90; playShots = 1;
            return;
        }

        if (playShots <= 3)
        {
            string name = "Интерфейс_" + playShots + "_" + (playShots == 1 ? "старт" : playShots == 2 ? "через_2с" : "через_5с");
            string file = Path.Combine(OutDir, name + ".png");
            try
            {
                ScreenCapture.CaptureScreenshot(file, 1);
                shots.Add(new Shot
                {
                    File = name + ".png",
                    Scene = "MainScene (PlayMode)",
                    What = "рантайм-интерфейс: строка меню, тулбар, дерево, свойства, статус-бар",
                    Note = playShots == 1 ? "сразу после сборки оболочки" : (playShots == 2 ? "через ~2 с (после таймеров панелей)" : "через ~5 с")
                });
                Line("снимок интерфейса: " + file);
            }
            catch (Exception e) { Line("ScreenCapture не сработал: " + e.Message); }

            waitFrames = 120;              // ~2 c при 60 FPS
            playShots++;
            return;
        }

        Line("--- снимки интерфейса готовы, выход из PlayMode ---");
        EditorApplication.ExitPlaymode();
        Finish(0);
    }

    // ------------------------------------------------------------------ рендер

    private static Camera AcquireCamera(Scene scene)
    {
        if (tempCam != null) return tempCam;

        // сначала пробуем собственную камеру сцены (в ней уже есть HDRP-данные)
        Camera found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera c in root.GetComponentsInChildren<Camera>(true))
            {
                if (c == null || !c.enabled) continue;
                if (found == null || c.CompareTag("MainCamera")) found = c;
                if (c.CompareTag("MainCamera")) break;
            }
            if (found != null && found.CompareTag("MainCamera")) break;
        }
        if (found != null)
        {
            savedCamName = found.name;
            savedPos = found.transform.position;
            savedRot = found.transform.rotation;
            savedFov = found.fieldOfView;
            savedNear = found.nearClipPlane;
            savedFar = found.farClipPlane;
            savedClear = found.clearFlags;
            savedBg = found.backgroundColor;
            useSceneCam = true;
            tempCam = found;
            Line("камера сцены: " + found.name);
            return tempCam;
        }

        tempCamGo = new GameObject("DshShotCam");
        tempCamGo.hideFlags = HideFlags.HideAndDontSave;
        tempCam = tempCamGo.AddComponent<Camera>();
        tempCam.clearFlags = CameraClearFlags.SolidColor;
        tempCam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
        tempCam.fieldOfView = 50f;
        tempCam.nearClipPlane = 0.05f;
        tempCam.farClipPlane = 5000f;

        // HDRP требует HDAdditionalCameraData — добавляем через рефлексию, без ссылки на пакет
        Type t = Type.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime");
        if (t != null && tempCamGo.GetComponent(t) == null) tempCamGo.AddComponent(t);
        Line("камера создана диагностикой (HDAdditionalCameraData: " + (t != null ? "есть" : "нет") + ")");
        return tempCam;
    }

    private static string savedCamName;
    private static Vector3 savedPos;
    private static Quaternion savedRot;
    private static float savedFov, savedNear, savedFar;
    private static CameraClearFlags savedClear;
    private static Color savedBg;
    private static bool useSceneCam;

    private static void Render(Camera cam, Vector3 center, Vector3 dir, Bounds b, string fileBase, string what)
    {
        if (cam == null) return;
        dir = dir.normalized;

        float radius = Mathf.Max(b.extents.magnitude, 0.5f);
        float dist = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f;
        dist = Mathf.Max(dist, radius * 1.2f + 0.5f);

        cam.transform.position = center + dir * dist;
        cam.transform.rotation = Quaternion.LookRotation(center - cam.transform.position, Vector3.up);
        cam.nearClipPlane = Mathf.Max(0.05f, dist - radius * 2f);
        cam.farClipPlane = dist + radius * 6f + 100f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);

        RenderTexture rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 2;
        RenderTexture prev = RenderTexture.active;
        RenderTexture prevTarget = cam.targetTexture;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            string file = Path.Combine(OutDir, fileBase + ".png");
            File.WriteAllBytes(file, png);
            UnityEngine.Object.DestroyImmediate(tex);

            shots.Add(new Shot
            {
                File = fileBase + ".png",
                Scene = lastSceneName,
                What = what,
                Note = "ракурс (" + dir.x.ToString("0.##", CultureInfo.InvariantCulture) + ", " +
                       dir.y.ToString("0.##", CultureInfo.InvariantCulture) + ", " +
                       dir.z.ToString("0.##", CultureInfo.InvariantCulture) + ") · " +
                       Width + "x" + Height + " · кадров: " + Time.frameCount
            });
            Line("снимок: " + fileBase + ".png (" + png.Length + " байт)");
        }
        catch (Exception e) { Line("рендер не удался (" + fileBase + "): " + e.Message); }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prev;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    // ------------------------------------------------------------------ вспомогательное

    private static bool TryBounds(Scene scene, out Bounds b)
    {
        var all = new List<Bounds>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer) continue;
                all.Add(r.bounds);
            }
        }
        return Robust(all, out b);
    }

    private static bool TryBounds(GameObject root, out Bounds b)
    {
        var all = new List<Bounds>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !r.enabled) continue;
            all.Add(r.bounds);
        }
        return Robust(all, out b);
    }

    /// <summary>
    /// Габариты с отбрасыванием выбросов: огромная плоскость пола (или небо) растягивает
    /// общие границы так, что вся остальная сцена становится меньше пикселя — кадр выходит
    /// чёрным. Поэтому объекты, чей размер более чем в 4 раза превышает МЕДИАННЫЙ, в расчёт
    /// габаритов не берутся (сами по себе они всё равно снимаются крупным планом).
    /// </summary>
    private static bool Robust(List<Bounds> all, out Bounds b)
    {
        b = new Bounds();
        if (all.Count == 0) return false;

        var sizes = new List<float>(all.Count);
        foreach (Bounds x in all) sizes.Add(x.size.magnitude);
        sizes.Sort();
        float median = sizes[sizes.Count / 2];
        float limit = Mathf.Max(median * 4f, 0.001f);

        bool any = false;
        foreach (Bounds x in all)
        {
            if (sizes.Count > 3 && x.size.magnitude > limit && median > 0.001f) continue;   // выброс
            if (!any) { b = x; any = true; } else b.Encapsulate(x);
        }
        if (!any)   // всё оказалось выбросами — берём как есть
        {
            foreach (Bounds x in all) { if (!any) { b = x; any = true; } else b.Encapsulate(x); }
        }
        return any;
    }

    private static bool HasRenderer(GameObject root)
    {
        return root != null && root.GetComponentInChildren<Renderer>(true) != null;
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "объект";
        StringBuilder sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
            else if (c == ' ') sb.Append('_');
        }
        string r = sb.ToString().Trim('_');
        return r.Length == 0 ? "объект" : r;
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;

        // вернуть камеру сцены как было
        if (useSceneCam && tempCam != null)
        {
            tempCam.transform.position = savedPos;
            tempCam.transform.rotation = savedRot;
            tempCam.fieldOfView = savedFov;
            tempCam.nearClipPlane = savedNear;
            tempCam.farClipPlane = savedFar;
            tempCam.clearFlags = savedClear;
            tempCam.backgroundColor = savedBg;
        }
        if (tempCamGo != null) UnityEngine.Object.DestroyImmediate(tempCamGo);
        tempCam = null;

        try { WriteCaptions(); } catch (Exception e) { Line("подписи не записаны: " + e.Message); }
        try { File.WriteAllText(Path.Combine(OutDir, "_log.txt"), log.ToString(), new UTF8Encoding(false)); } catch { }

        Debug.Log("[Shots] ГОТОВО: снимков " + shots.Count + " · код " + code);
        EditorApplication.Exit(code);
    }

    private static void WriteCaptions()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# Снимки сцен и объектов проекта KazistovVv");
        sb.AppendLine();
        sb.AppendLine("_Сгенерировано автоматически диагностикой `Assets/_Project/01_Scripts/Editor/DshScreenshotsDiag.cs`._");
        sb.AppendLine();
        sb.AppendLine("- **Дата съёмки:** " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
        sb.AppendLine("- **Разрешение:** " + Width + "×" + Height + ", PNG");
        sb.AppendLine("- **Графическое устройство:** " + SystemInfo.graphicsDeviceType + " · " + SystemInfo.graphicsDeviceName);
        sb.AppendLine("- **Unity:** " + Application.unityVersion);
        sb.AppendLine("- **Способ:** batch-режим **без** `-nographics` (нужен настоящий графический контекст); сцены открывались присоединённо и **не сохранялись**.");
        sb.AppendLine("- **Всего снимков:** " + shots.Count);
        sb.AppendLine();
        sb.AppendLine("| Файл | Сцена | Что на снимке | Примечание |");
        sb.AppendLine("|---|---|---|---|");
        foreach (Shot s in shots)
        {
            sb.AppendLine("| `" + s.File + "` | " + s.Scene + " | " + s.What + " | " + s.Note + " |");
        }
        sb.AppendLine();
        sb.AppendLine("## Чего на снимках НЕТ и почему");
        sb.AppendLine();
        sb.AppendLine("- **Префабов нет вообще** — каталоги `Assets/_Project/02_Prefabs/{Robot,UI,Environment,FX}` пусты;");
        sb.AppendLine("  модели и стенды лежат прямо в `MainScene.unity`, поэтому сняты их крупные планы.");
        sb.AppendLine("- **Интерфейс (меню, тулбар, дерево, свойства) строится кодом в рантайме** и является");
        sb.AppendLine("  `ScreenSpaceOverlay`-канвасом — рендером камеры он не снимается, поэтому для него");
        sb.AppendLine("  использован `ScreenCapture` в PlayMode (файлы `Интерфейс_*.png`).");
        sb.AppendLine("- Сцены `Assets/ROOMS`, `Assets/TABLES`, `Assets/Premises` — сторонние демо-сцены из");
        sb.AppendLine("  купленных ассетов, к проекту управления роботами не относятся и не снимались.");
        File.WriteAllText(Path.Combine(OutDir, "CAPTIONS.md"), sb.ToString(), new UTF8Encoding(false));
        Line("подписи: " + Path.Combine(OutDir, "CAPTIONS.md") + " (" + shots.Count + " записей)");
    }
}
#endif
