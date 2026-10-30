using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЭТАП 3 ТЗ — ЭКСПОРТ ДЕМОНСТРАЦИЙ: скриншоты (PNG) и видеозапись (MP4).
    ///
    /// КУДА СОХРАНЯЕТСЯ (ТЗ): стандартная папка Windows «Видео» — берётся системно
    /// (`Environment.SpecialFolder.MyVideos`, на этой машине `C:\Users\Ольга\Videos`),
    /// а если система папку не отдала — `%USERPROFILE%\Videos`.
    /// Имена файлов строго по ТЗ:
    ///   `KazistovVv_screenshot_YYYY-MM-DD_HH-MM-SS.png`
    ///   `KazistovVv_recording_YYYY-MM-DD_HH-MM-SS.mp4`
    ///
    /// СКРИНШОТ: подпись (дата, состояние State Machine, выбранный робот, тема, язык)
    /// рисуется ПОВЕРХ кадра отдельным служебным Canvas (sortingOrder 30000, HideInHierarchy,
    /// `raycastTarget = false`), затем кадр снимается `ScreenCapture.CaptureScreenshotAsTexture`
    /// в конце кадра и пакуется в PNG. Так на картинке оказывается НАСТОЯЩИЙ текст интерфейсным
    /// шрифтом (в том числе CJK), без шрифтового растеризатора в коде.
    ///
    /// ВИДЕО: используется Unity Recorder из Package Manager (`com.unity.recorder`, в проекте
    /// уже разрешён — `Library/PackageCache/com.unity.recorder@…`): `RecorderController` +
    /// `MovieRecorderSettings` с `CoreEncoderSettings` (H.264, MP4). Это редакторский API,
    /// поэтому вызов идёт под `#if UNITY_EDITOR`; в сборке (и если рекордер недоступен)
    /// включается ЧЕСТНЫЙ РЕЗЕРВ — последовательность PNG-кадров в подпапке записи с
    /// манифестом (FPS/разрешение/битрейт), а оператор получает понятное сообщение.
    /// Разрешение, FPS и битрейт — ПУБЛИЧНЫЕ параметры (инспектор/код/настройки).
    /// </summary>
    public class KvCaptureService : MonoBehaviour
    {
        // ------------------------------------------------------------------ публичные параметры (ТЗ)
        [Header("Скриншоты (ТЗ этап 3.1)")]
        [Tooltip("Рисовать подпись поверх скриншота (дата, состояние, выбранный робот).")]
        public bool annotate = true;
        [Tooltip("Клавиша скриншота (свободная: F8 не занята ни одним биндом проекта).")]
        public KeyCode screenshotKey = KeyCode.F8;

        [Header("Видео (ТЗ этап 3.2) — публичные параметры")]
        [Tooltip("Разрешение записи по ширине (0 — как у окна).")]
        public int videoWidth = 1920;
        [Tooltip("Разрешение записи по высоте (0 — как у окна).")]
        public int videoHeight = 1080;
        [Tooltip("Частота кадров записи.")]
        public int videoFps = 30;
        [Tooltip("Битрейт видео, кбит/с (переводится в Мбит/с для Unity Recorder).")]
        public int videoBitrateKbps = 12000;
        [Tooltip("Клавиша старт/стоп записи (F10 свободна).")]
        public KeyCode videoKey = KeyCode.F10;

        [Header("Диагностика")]
        public bool logEvents = true;

        /// <summary>Сообщение для журнала/статуса (подписка — хабом).</summary>
        public event Action<string> Message;

        // ------------------------------------------------------------------ состояние
        private string lastScreenshot = "";
        private string lastVideo = "";
        private bool recording;
        private int videoFrames;
        private float recordStartedAt;
        private string recordPath = "";
        private bool externalRecorder;
        private bool shotInProgress;

        public bool IsRecording { get { return recording; } }
        public string LastScreenshotPath { get { return lastScreenshot; } }
        public string LastVideoPath { get { return lastVideo; } }
        public string LastRecordingFolder { get { return recordPath; } }
        public int RecordedFrames { get { return videoFrames; } }

        /// <summary>
        /// Момент начала текущей видеозаписи (`Time.realtimeSinceStartup`), 0 — запись не идёт.
        /// Добавлено на этапе 33 ТЗ: запись голоса диктора сохраняет СМЕЩЕНИЕ относительно
        /// начала видео, чтобы на монтаже звук выставлялся одним движением.
        /// </summary>
        public float RecordStartedAt { get { return recording ? recordStartedAt : 0f; } }

        /// <summary>Папка экспорта (стандартная «Видео» Windows).</summary>
        public string Folder
        {
            get
            {
                try
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                    if (!string.IsNullOrEmpty(dir)) return dir;
                }
                catch (Exception e)
                {
                    // ПУСТОЙ CATCH ЗАМЕНЁН (30.10.2026, аудит S43, пункт 5.7). Здесь это не ошибка:
                    // у пользователя может не быть папки «Видео», и тогда работает запасной путь
                    // ниже. Но причина обязана попасть в журнал — иначе «видео не сохраняется»
                    // будет выглядеть как поломка записи, а не как отсутствующая папка.
                    UnityEngine.Debug.Log("[Captures] папка «Видео» недоступна (" + e.GetType().Name +
                                          ": " + e.Message + ") — сохраню рядом с профилем");
                }
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(profile)) return Path.Combine(profile, "Videos");
                return Application.persistentDataPath;
            }
        }

        /// <summary>Создать сервис на скрытом служебном объекте (сцена не меняется).</summary>
        public static KvCaptureService Create(Transform parent)
        {
            GameObject go = new GameObject("KvCaptures");
            go.hideFlags = HideFlags.HideInHierarchy;
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<KvCaptureService>();
        }

        private void Awake()
        {
            EnsureFolder();
        }

        private void EnsureFolder()
        {
            try
            {
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Capture] папка «" + Folder + "» недоступна: " + e.Message);
            }
        }

        private void Say(string message)
        {
            if (logEvents) Debug.Log("[Capture] " + message);
            if (Message != null) Message(message);
        }

        // ================================================================== ЭТАП 3.1: СКРИНШОТ

        /// <summary>Имя файла по ТЗ: KazistovVv_screenshot_YYYY-MM-DD_HH-MM-SS.png</summary>
        public static string ScreenshotName(DateTime time)
        {
            return "KazistovVv_screenshot_" + time.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
        }

        /// <summary>Имя файла записи по ТЗ: KazistovVv_recording_YYYY-MM-DD_HH-MM-SS</summary>
        public static string RecordingName(DateTime time)
        {
            return "KazistovVv_recording_" + time.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        /// <summary>
        /// Сделать скриншот. `stateLabel` и `robotName` попадают в подпись (ТЗ: «опционально:
        /// аннотации (дата, состояние, выбранный робот)»). Возвращает false, если снимок
        /// уже делается или экран недоступен.
        /// </summary>
        public bool TakeScreenshot(string stateLabel, string robotName)
        {
            if (shotInProgress)
            {
                Say("скриншот уже делается — подождите кадр");
                return false;
            }
            StartCoroutine(ScreenshotRoutine(stateLabel, robotName));
            return true;
        }

        private IEnumerator ScreenshotRoutine(string stateLabel, string robotName)
        {
            shotInProgress = true;
            EnsureFolder();

            // Подпись строится ДО ожидания, а снимок делается ПОСЛЕ отрисовки кадра
            // (иначе в PNG попадёт предыдущий кадр и подписи на нём не будет).
            // `yield` нельзя держать внутри try/catch — поэтому кадр снимается вне try.
            GameObject overlay = null;
            if (annotate)
            {
                try { overlay = BuildAnnotation(stateLabel, robotName); }
                catch (Exception e) { Say("подпись не построена: " + e.Message); }
            }

            yield return new WaitForEndOfFrame();

            try
            {
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex == null)
                {
                    Say("снимок экрана недоступен (нет графического устройства)");
                }
                else
                {
                    byte[] png = tex.EncodeToPNG();
                    int w = tex.width, h = tex.height;
                    Destroy(tex);

                    DateTime now = DateTime.Now;
                    string path = Path.Combine(Folder, ScreenshotName(now));
                    File.WriteAllBytes(path, png);
                    lastScreenshot = path;
                    Say("скриншот сохранён: " + path + " · " + png.Length / 1024 + " КБ · " + w + "×" + h);
                }
            }
            catch (Exception e)
            {
                Say("скриншот не сделан: " + e.Message);
            }
            finally
            {
                if (overlay != null) Destroy(overlay);
                shotInProgress = false;
            }
        }

        /// <summary>
        /// Служебный Canvas с подписью. Скрыт из иерархии, кликов не перехватывает
        /// (raycastTarget = false), поверх всех окон (sortingOrder 30000) и живёт ровно
        /// до снятия кадра, поэтому в обычной работе интерфейс не меняется.
        /// </summary>
        private GameObject BuildAnnotation(string stateLabel, string robotName)
        {
            GameObject canvasGo = new GameObject("KvScreenshotAnnotation", typeof(Canvas),
                typeof(CanvasScaler));
            canvasGo.hideFlags = HideFlags.HideInHierarchy;
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = (RectTransform)canvasGo.transform;

            // Полупрозрачная плашка снизу слева — подпись читается на любом фоне.
            GameObject plate = new GameObject("Plate", typeof(Image));
            plate.transform.SetParent(root, false);
            Image bg = plate.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(0.05f, 0.06f, 0.08f, 0.72f);
            bg.raycastTarget = false;
            RectTransform pr = (RectTransform)plate.transform;
            pr.anchorMin = new Vector2(0f, 0f);
            pr.anchorMax = new Vector2(0f, 0f);
            pr.pivot = new Vector2(0f, 0f);
            pr.sizeDelta = new Vector2(720f, 132f);
            pr.anchoredPosition = new Vector2(24f, 24f);

            DateTime now = DateTime.Now;
            string[] lines =
            {
                KvLoc.T("app.title", "KazistovVv") + "  ·  " + now.ToString("yyyy-MM-dd HH:mm:ss"),
                KvLoc.T("status.robot", "Робот") + ": " + (string.IsNullOrEmpty(robotName) ? "—" : robotName),
                "State: " + (string.IsNullOrEmpty(stateLabel) ? "—" : stateLabel),
                KvLoc.T("status.theme", "Тема") + ": " + KvTheme.ModeLabel +
                    "   ·   " + KvLoc.T("status.language", "Язык") + ": " + KvLoc.CurrentName
            };

            for (int i = 0; i < lines.Length; i++)
            {
                Text t = KvTheme.CreateText(pr, "Line" + i, lines[i],
                    i == 0 ? KvTheme.FontSizeTitle : KvTheme.FontSize,
                    TextAnchor.MiddleLeft, i == 0 ? KvTheme.TextMain : KvTheme.TextDim);
                t.rectTransform.anchorMin = new Vector2(0f, 1f);
                t.rectTransform.anchorMax = new Vector2(1f, 1f);
                t.rectTransform.pivot = new Vector2(0f, 1f);
                t.rectTransform.sizeDelta = new Vector2(-24f, 22f);
                t.rectTransform.anchoredPosition = new Vector2(14f, -(10f + i * 26f));
            }

            // Тонкая акцентная линия слева — «инженерный» вид подписи.
            GameObject bar = new GameObject("Bar", typeof(Image));
            bar.transform.SetParent(pr, false);
            Image barImg = bar.GetComponent<Image>();
            barImg.sprite = KvTheme.WhiteSprite;
            barImg.color = KvTheme.Accent;
            barImg.raycastTarget = false;
            RectTransform br = (RectTransform)bar.transform;
            br.anchorMin = new Vector2(0f, 0f);
            br.anchorMax = new Vector2(0f, 1f);
            br.pivot = new Vector2(0f, 0.5f);
            br.sizeDelta = new Vector2(4f, 0f);
            br.anchoredPosition = Vector2.zero;

            return canvasGo;
        }

        // ================================================================== ЭТАП 3.2: ВИДЕО

        /// <summary>Старт/стоп записи (кнопка тулбара и клавиша).</summary>
        public bool ToggleRecording()
        {
            if (recording) { StopRecording(); return false; }
            return StartRecording();
        }

        /// <summary>
        /// Начать запись вида оператора. Возвращает true, если запись действительно началась
        /// (через Unity Recorder либо через резервный режим кадров).
        /// </summary>
        public bool StartRecording()
        {
            if (recording) return true;
            EnsureFolder();

            DateTime now = DateTime.Now;
            string baseName = RecordingName(now);
            videoFrames = 0;
            recordStartedAt = Time.realtimeSinceStartup;

            bool ok = false;
            externalRecorder = false;

#if UNITY_EDITOR
            ok = StartRecorderMovie(Path.Combine(Folder, baseName));
            externalRecorder = ok;
#endif
            if (!ok)
            {
                // Резерв: последовательность PNG-кадров (MP4 требует Unity Recorder).
                recordPath = Path.Combine(Folder, baseName + "_frames");
                try
                {
                    Directory.CreateDirectory(recordPath);
                    WriteFrameManifest(recordPath, baseName);
                    ok = true;
                }
                catch (Exception e)
                {
                    Say("запись не начата: " + e.Message);
                    return false;
                }
            }
            else
            {
                recordPath = Path.Combine(Folder, baseName + ".mp4");
            }

            recording = true;
            lastVideo = recordPath;
            Say("ЗАПИСЬ НАЧАТА · " + videoWidth + "×" + videoHeight + " · " + videoFps + " FPS · " +
                videoBitrateKbps + " кбит/с · " + recordPath +
                (externalRecorder ? " (Unity Recorder, MP4)" : " (резерв: кадры PNG + манифест)"));
            return true;
        }

        /// <summary>Остановить запись.</summary>
        public void StopRecording()
        {
            if (!recording) return;
            recording = false;
            float seconds = Time.realtimeSinceStartup - recordStartedAt;

#if UNITY_EDITOR
            if (externalRecorder) StopRecorderMovie();
#endif
            string tail = externalRecorder
                ? " · файл: " + recordPath
                : " · кадров: " + videoFrames + " в " + recordPath;
            Say("ЗАПИСЬ ОСТАНОВЛЕНА · " + seconds.ToString("0.0") + " с" + tail);
        }

        /// <summary>
        /// Кадровое обслуживание РЕЗЕРВНОГО режима (без Unity Recorder): кадры снимаются
        /// с частотой `videoFps`. При работающем рекордере здесь делать нечего — он сам
        /// пишет MP4 из вида игры.
        /// </summary>
        public void Tick()
        {
            if (!recording || externalRecorder) return;

            float target = (float)(videoFrames + 1) / Mathf.Max(1, videoFps);
            if (Time.realtimeSinceStartup - recordStartedAt < target) return;
            if (frameWritePending) return;          // по одному кадру за раз

            frameWritePending = true;
            videoFrames++;
            StartCoroutine(SaveFrameRoutine(videoFrames));
        }

        private bool frameWritePending;

        private IEnumerator SaveFrameRoutine(int index)
        {
            yield return new WaitForEndOfFrame();
            try
            {
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex != null)
                {
                    byte[] png = tex.EncodeToPNG();
                    int w = tex.width, h = tex.height;
                    Destroy(tex);
                    string file = Path.Combine(recordPath, "frame_" + index.ToString("D6") + ".png");
                    File.WriteAllBytes(file, png);
                    if (index == 1)
                        Debug.Log("[Capture] первый кадр записи: " + w + "×" + h + " → " + file);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Capture] кадр не сохранён: " + e.Message);
            }
            finally
            {
                frameWritePending = false;
            }
        }

        private void WriteFrameManifest(string dir, string baseName)
        {
            string json = "{\n" +
                "  \"project\": \"KazistovVv\",\n" +
                "  \"kind\": \"video-recording-fallback\",\n" +
                "  \"baseName\": \"" + baseName + "\",\n" +
                "  \"width\": " + videoWidth + ",\n" +
                "  \"height\": " + videoHeight + ",\n" +
                "  \"fps\": " + videoFps + ",\n" +
                "  \"bitrateKbps\": " + videoBitrateKbps + ",\n" +
                "  \"note\": \"Кадры PNG. Для MP4 включите Unity Recorder (Window > Package Manager > Unity Recorder) — запись автоматически пойдёт в MP4.\",\n" +
                "  \"created\": \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"\n" +
                "}\n";
            File.WriteAllText(Path.Combine(dir, "recording.json"), json);
        }

        // ------------------------------------------------------------------ Unity Recorder (редактор)

#if UNITY_EDITOR
        private UnityEditor.Recorder.RecorderController recorderController;
        private UnityEditor.Recorder.RecorderControllerSettings recorderSettings;

        /// <summary>
        /// Запись MP4 средствами Unity Recorder (ТЗ: «использовать Unity Recorder из Package Manager»).
        /// Разрешение/FPS/битрейт берутся из публичных полей этого компонента.
        /// </summary>
        private bool StartRecorderMovie(string pathWithoutExtension)
        {
            try
            {
                recorderSettings = ScriptableObject.CreateInstance<
                    UnityEditor.Recorder.RecorderControllerSettings>();
                recorderSettings.SetRecordModeToManual();
                recorderSettings.FrameRate = Mathf.Clamp(videoFps, 1, 120);
                recorderSettings.FrameRatePlayback = UnityEditor.Recorder.FrameRatePlayback.Constant;
                recorderSettings.CapFrameRate = true;

                var movie = ScriptableObject.CreateInstance<UnityEditor.Recorder.MovieRecorderSettings>();
                movie.name = "KazistovVv Movie";
                movie.Enabled = true;
                movie.OutputFile = pathWithoutExtension;
                movie.FrameRate = recorderSettings.FrameRate;
                movie.FrameRatePlayback = UnityEditor.Recorder.FrameRatePlayback.Constant;
                movie.CapFrameRate = true;

                var encoder = new UnityEditor.Recorder.Encoder.CoreEncoderSettings();
                encoder.Codec = UnityEditor.Recorder.Encoder.CoreEncoderSettings.OutputCodec.MP4;
                encoder.EncodingQuality =
                    UnityEditor.Recorder.Encoder.CoreEncoderSettings.VideoEncodingQuality.High;
                encoder.TargetBitRate = Mathf.Clamp(videoBitrateKbps / 1000f, 0.1f, 100f);  // Мбит/с
                movie.EncoderSettings = encoder;

                var input = new UnityEditor.Recorder.Input.GameViewInputSettings();
                input.OutputWidth = videoWidth > 0 ? videoWidth : 0;
                input.OutputHeight = videoHeight > 0 ? videoHeight : 0;
                movie.ImageInputSettings = input;

                recorderSettings.AddRecorderSettings(movie);

                recorderController = new UnityEditor.Recorder.RecorderController(recorderSettings);
                recorderController.PrepareRecording();
                if (!recorderController.StartRecording())
                {
                    Debug.LogWarning("[Capture] Unity Recorder не запустился — включён резервный режим кадров");
                    recorderController = null;
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Capture] Unity Recorder недоступен (" + e.Message +
                                 ") — включён резервный режим кадров PNG");
                recorderController = null;
                return false;
            }
        }

        private void StopRecorderMovie()
        {
            try
            {
                if (recorderController != null && recorderController.IsRecording())
                    recorderController.StopRecording();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Capture] остановка рекордера: " + e.Message);
            }
            finally
            {
                recorderController = null;
            }
        }
#endif

        private void OnDisable()
        {
            if (recording) StopRecording();
        }

        /// <summary>Строка состояния для панели/подсказки.</summary>
        public string Status
        {
            get
            {
                string mode = recording
                    ? (externalRecorder ? "MP4 (Recorder)" : "кадры PNG (резерв)")
                    : "готово";
                return "Экспорт: " + mode + " · папка: " + Folder +
                       (string.IsNullOrEmpty(lastScreenshot) ? "" : " · последний снимок: " +
                        Path.GetFileName(lastScreenshot));
            }
        }
    }
}
