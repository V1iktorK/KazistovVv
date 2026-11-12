using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace KazistovVvAr
{
    /// <summary>
    /// СТРАЖ КАМЕРЫ ПЛАНШЕТА (06.11.2026). Владелец: «Включи чтобы был запрос на использование
    /// камеры, а то ею программа не пользуется» и «на планшете ничего не работает».
    ///
    /// ЧТО ДЕЛАЕТ, ПО ПОРЯДКУ:
    ///   1) показывает ЯВНЫЙ экран «ДОСТУП К КАМЕРЕ» и сам вызывает системный диалог разрешения
    ///      (`Permission.RequestUserPermission`), если разрешения нет;
    ///   2) ждёт, пока камера реально начнёт отдавать кадры (`ARCameraManager.frameReceived`),
    ///      и пишет в журнал число кадров — это доказательство «камера используется»;
    ///   3) если кадров нет 6 с — перезапускает AR-сессию; если нет 12 с — включает РЕЗЕРВНЫЙ
    ///      живой предпросмотр через `WebCamTexture` (камера на экране, даже если ARCore не смог);
    ///   4) если разрешение запрещено — кнопка «Настройки приложения», чтобы включить вручную.
    ///
    /// ПОЧЕМУ СВОЙ ЭКРАН, А НЕ ТОЛЬКО СИСТЕМНЫЙ ДИАЛОГ. На планшете разрешение уже было выдано
    /// прошлыми установками, поэтому Android диалог не показывает — владелец видел «программа не
    /// просит камеру и не пользуется ею». Свой экран показывает состояние всегда, а системный
    /// диалог вызывается, когда разрешения действительно нет.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArCameraGate : MonoBehaviour
    {
        /// <summary>Сколько секунд ждать первый кадр камеры, прежде чем перезапустить сессию.</summary>
        public float restartAfterSeconds = 15f;

        /// <summary>Сколько секунд ждать кадры, прежде чем включить резервный предпросмотр.</summary>
        public float fallbackAfterSeconds = 25f;

        /// <summary>Сколько кадров достаточно, чтобы считать камеру работающей.</summary>
        public int framesToConfirm = 30;

        /// <summary>Сколько кадров камера реально отдала (диагностика/тесты).</summary>
        public int CameraFrames { get; private set; }

        /// <summary>
        /// ПЕРИОДИЧЕСКИЙ СНИМОК КАДРА, секунды (12.11.2026).
        ///
        /// ЧТО БЫЛО. `ar_camera_frame.png` писался РОВНО ОДИН РАЗ за сеанс — при подтверждении
        /// камеры. На приёмке в папке лежал один файл 144 КБ с одним таймштампом, и доказать, что
        /// камера видела ВО ВРЕМЯ РАБОТЫ (например, что планшет лежал экраном вверх), было нечем.
        /// ЧТО СТАЛО. Кадр пишется раз в `framePeriodSeconds`; файлов не больше `maxFrameFiles`.
        /// </summary>
        public float framePeriodSeconds = 5f;

        /// <summary>
        /// СКОЛЬКО ФАЙЛОВ КАДРА ДЕРЖИМ, максимум (12.11.2026). Десять кадров по ~150 КБ — это
        /// ~1.5 МБ, что безопасно для памяти планшета. Дальше счёт начинается сначала и старый файл
        /// с тем же именем ПЕРЕЗАПИСЫВАЕТСЯ: копить снимки «навсегда» на устройстве нельзя.
        /// </summary>
        public int maxFrameFiles = 10;

        /// <summary>Номер последнего сохранённого периодического кадра (для журнала и тестов).</summary>
        public int FrameSessionIndex { get; private set; }

        /// <summary>Сколько периодических кадров сохранено за сеанс (для журнала и тестов).</summary>
        public int FramesSaved { get; private set; }

        /// <summary>Разрешение уже запрошено в этой сессии.</summary>
        public bool PermissionRequested { get; private set; }

        /// <summary>Включён ли резервный предпросмотр (WebCamTexture).</summary>
        public bool FallbackPreviewActive { get; private set; }

        private Canvas canvas;
        private Text statusText;
        private Button actionButton;
        private Text actionLabel;
        private GameObject fallbackRoot;
        private WebCamTexture webCam;
        private RawImage fallbackImage;

        private float startedAt;
        private float nextPermissionCheck;
        private int restartAttempts;
        private bool confirmed;
        private bool awaitingContinue;
        private float continueShownAt;

        // --- ПРОВЕРКА СОДЕРЖИМОГО КАДРА (добавлено 11.11.2026): счётчик кадров сам по себе
        // ничего не доказывал — на устройстве он доходил до 31 при ПОЛНОСТЬЮ ЧЁРНОМ изображении.
        /// <summary>Содержимое кадра уже проверено.</summary>
        private bool imageChecked;
        /// <summary>Проверка содержимого идёт (корутина уже запущена).</summary>
        private bool imageCheckRunning;
        /// <summary>В кадре есть изображение (яркость выше порога).</summary>
        private bool imageHasContent;
        /// <summary>Средняя яркость проверенного кадра — для журнала.</summary>
        private float lastAverageLuminance;
        /// <summary>О пустом кадре уже сообщено (чтобы не заливать журнал каждый кадр).</summary>
        private bool emptyImageReported;

        // --- ПЕРИОДИЧЕСКИЙ СНИМОК КАДРА (добавлено 12.11.2026): доказательство того, что камера
        // видела ВО ВРЕМЯ РАБОТЫ, а не только в момент подтверждения.
        /// <summary>Когда писать следующий периодический кадр, `Time.unscaledTime`.</summary>
        private float nextFrameCaptureTime;
        /// <summary>Снимок уже пишется — второй параллельно не запускаем (защита от накладок).</summary>
        private bool frameCaptureRunning;

        private ARCameraManager cameraManager;
        private ARSession session;
        private ARCameraBackground background;

        void Awake()
        {
            BuildUi();
        }

        void Start()
        {
            startedAt = Time.unscaledTime;
            ResolveReferences();

            // ПЕРВЫЙ ПЕРИОДИЧЕСКИЙ КАДР — НЕ В ПЕРВУЮ ЖЕ СЕКУНДУ (12.11.2026): пока ARCore не отдал
            // ни одного кадра, `TryAcquireLatestCpuImage` только пишет предупреждение в журнал и
            // пугает оператора. Один период ожидания — и серия снимков начинается.
            nextFrameCaptureTime = Time.unscaledTime + Mathf.Max(1f, framePeriodSeconds);

            if (cameraManager != null)
                cameraManager.frameReceived += OnFrameReceived;

            if (session != null && !session.enabled) session.enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[KvAr] КАМЕРА: ЗАПРОС РАЗРЕШЕНИЯ (системный диалог Android)");
                RequestCamera();
            }
            else
            {
                Debug.Log("[KvAr] КАМЕРА: разрешение выдано ранее — включаю камеру AR");
            }
#endif
            SetStatus("Проверяю доступ к камере…");
        }

        /// <summary>
        /// ОСВОБОЖДЕНИЕ КАМЕРЫ (P2-9, 11.11.2026).
        ///
        /// ЧТО БЫЛО. `webCam.Stop()` — и всё. `WebCamTexture` держит НАТИВНЫЙ буфер кадров
        /// (1280×720, 4 байта на пиксель — около 3,7 МБ на кадр) плюс саму камеру устройства;
        /// `Stop()` останавливает поток, но объект не уничтожает.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. Повторный вход в AR (выход в меню и возврат, перезапуск сцены)
        /// создавал ВТОРОЙ `WebCamTexture`, оставляя первый вместе с буфером: камера оставалась
        /// занятой, предпросмотр мог не включиться вовсе, а память утекала на каждый вход.
        ///
        /// ТЕПЕРЬ после `Stop()` объект уничтожается и поле обнуляется.
        /// </summary>
        void OnDestroy()
        {
            if (cameraManager != null)
                cameraManager.frameReceived -= OnFrameReceived;
            if (webCam != null)
            {
                try { webCam.Stop(); }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[KvAr] КАМЕРА: остановка WebCamTexture не удалась: " + e.Message);
                }
                if (Application.isPlaying) Destroy(webCam);
                else DestroyImmediate(webCam);
                webCam = null;
            }
        }

        void Update()
        {
            float elapsed = Time.unscaledTime - startedAt;

            // ПЕРИОДИЧЕСКИЙ СНИМОК КАДРА (12.11.2026) — ПЕРВЫМ ДЕЛОМ И ДО ВСЕХ `return`.
            //
            // ЧТО БЫЛО. Снимок делался один раз и внутри ветки подтверждения, то есть до неё (пока
            // окно «ДОСТУП К КАМЕРЕ» висит, `confirmed == false`) не писался вовсе, а после —
            // никогда. На приёмке в папке был один файл с одним таймштампом: чем камера занималась
            // во время работы, доказать было нечем.
            // ЧЕМ ПЛОХО. Причина «плоскостей 0» (планшет лежал экраном вверх) осталась невидимой:
            // снимка этого момента просто не существовало.
            // ЧТО СТАЛО. Снимок снимается по расписанию независимо от состояния окна — состояние
            // разрешения/подтверждения к доказательству «что видела камера» отношения не имеет.
            CaptureFrameIfDue();

            // ЭКРАН ПОДТВЕРЖДЕНИЯ — ПЕРВЫМ ДЕЛОМ: любое касание снимает его. Раньше проверка
            // разрешения могла «съесть» кадр, в котором пришло касание (пропущенный таймаут) —
            // владелец нажимал, а экран не закрывался.
            if (awaitingContinue)
            {
                if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                {
                    ContinueToAr();
                    return;
                }
#if UNITY_EDITOR || UNITY_STANDALONE
                if (Input.GetMouseButtonDown(0))
                {
                    ContinueToAr();
                    return;
                }
#endif
                // АВТОПРОДОЛЖЕНИЕ: если владелец не нажал за 15 с — не держим AR заблокированным,
                // камера уже доказана кадрами.
                if (Time.unscaledTime - continueShownAt >= 15f)
                    ContinueToAr();
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // JNI-проверку разрешения делаем НЕ каждый кадр (это дорого), а 2 раза в секунду.
            if (Time.unscaledTime < nextPermissionCheck)
            {
                return;
            }
            nextPermissionCheck = Time.unscaledTime + 0.5f;
            bool granted = Permission.HasUserAuthorizedPermission(Permission.Camera);
            if (!granted)
            {
                SetStatus("КАМЕРА НЕ РАЗРЕШЕНА\nНажмите «Разрешить камеру» — без неё программа не видит стол.");
                if (elapsed > 3f) ShowSettingsButton();
                return;
            }
#endif

            if (confirmed) return;

            // ПРОВЕРКА ИЗОБРАЖЕНИЯ, А НЕ ТОЛЬКО СЧЁТЧИКА КАДРОВ (добавлено 11.11.2026).
            //
            // ЧТО БЫЛО. «Камера работает» объявлялось по `CameraFrames >= framesToConfirm`, то есть
            // по числу ПРИШЕДШИХ кадров. На живом планшете (прогон 11.11.2026) приложение получило
            // 31 кадр, написало «КАМЕРА: РАБОТАЕТ — программа действительно использует камеру», а
            // сохранённый этим же кодом кадр оказался ПОЛНОСТЬЮ ЧЁРНЫМ (320×240, все пиксели нули).
            // Оператор видел чёрный экран с интерфейсом, а журнал утверждал успех — то есть
            // предохранитель подтверждал то, чего не было.
            //
            // ЧТО СТАЛО. Перед подтверждением берётся CPU-кадр и проверяется СОДЕРЖИМОЕ (средняя
            // яркость). Пустое изображение — это не «работает», а честная строка с причиной.
            if (CameraFrames >= framesToConfirm && !imageChecked && !imageCheckRunning)
            {
                imageCheckRunning = true;
                StartCoroutine(VerifyCameraImageOnce());
            }

            if (CameraFrames >= framesToConfirm && imageChecked)
            {
                if (!imageHasContent)
                {
                    if (!emptyImageReported)
                    {
                        emptyImageReported = true;
                        Debug.LogError("[KvAr] КАМЕРА: кадры идут (" + CameraFrames +
                                       "), но изображение ПУСТОЕ (чёрный кадр, яркость " +
                                       lastAverageLuminance.ToString("0.000") +
                                       ") — камера НЕ считается рабочей");
                    }
                    SetStatus("Камера отдаёт ПУСТЫЕ кадры (чёрное изображение).\n" +
                              "Работа вслепую невозможна. Проверьте разрешение камеры и ARCore.");
                    // Резервный предпросмотр — единственный шанс показать оператору хоть что-то.
                    if (!FallbackPreviewActive) EnableFallbackPreview();
                    return;
                }

                confirmed = true;
                awaitingContinue = true;
                continueShownAt = Time.unscaledTime;
                Debug.Log("[KvAr] КАМЕРА: РАБОТАЕТ — получено кадров " + CameraFrames +
                          " · изображение НЕ пустое (яркость " + lastAverageLuminance.ToString("0.000") +
                          ") · программа действительно использует камеру");
                // ПЕРВЫЙ КАДР БЕРЁМ СРАЗУ (12.11.2026). Раньше здесь был единственный вызов
                // `CaptureCameraFrameOnce()` — и он же оставался единственным за весь сеанс.
                // Теперь расписание одно (`CaptureFrameIfDue`), а подтверждение лишь выравнивает
                // его: первый снимок серии появляется сразу, следующие — по периоду.
                nextFrameCaptureTime = Time.unscaledTime;
                // НЕ ПРЯЧЕМ САМИ: владелец должен увидеть подтверждение и нажать кнопку — иначе
                // «увидел запрос, но не успел нажать» (жалоба 06.11.2026).
                SetStatus("Камера работает: получено кадров " + CameraFrames +
                          ".\nНажмите «ПРОДОЛЖИТЬ», чтобы начать работу.");
                if (actionLabel != null) actionLabel.text = "ПРОДОЛЖИТЬ";
                return;
            }

            if (CameraFrames > 0 && !imageChecked)
            {
                SetStatus("Камера работает — получено кадров " + CameraFrames + "…");
            }

            if (elapsed >= restartAfterSeconds && restartAttempts < 2)
            {
                restartAttempts++;
                Debug.LogWarning("[KvAr] КАМЕРА: кадров нет " + elapsed.ToString("0") +
                                 " с — перезапуск AR-сессии (попытка " + restartAttempts + " из 2)");
                if (session != null) session.Reset();
                startedAt = Time.unscaledTime;
            }
            else if (elapsed >= fallbackAfterSeconds && !FallbackPreviewActive)
            {
                EnableFallbackPreview();
            }
        }

        // ================================================================ кадры и разрешение

        /// <summary>
        /// ПРОВЕРИТЬ, ЧТО В КАДРЕ ЕСТЬ ИЗОБРАЖЕНИЕ, А НЕ ТОЛЬКО ЕГО РАЗМЕР.
        ///
        /// ЧИСТАЯ ФУНКЦИЯ — намеренно: именно её отсутствие и было дефектом (см. шапку класса и
        /// комментарий в `Update`), а проверять «камера показала чёрный экран» на устройстве каждый
        /// раз заново нельзя. Тест подаёт чёрный массив и ожидает `false`, а настоящий кадр — `true`.
        ///
        /// Порог 0.02 (≈5 из 255) — заведомо ниже любого реального изображения (даже тёмная комната
        /// даёт больше) и заведомо выше шума полностью чёрного кадра, где все компоненты равны нулю.
        /// </summary>
        public static bool FrameHasContent(Color32[] pixels, float threshold = 0.02f)
        {
            if (pixels == null || pixels.Length == 0) return false;

            // Считаем по выборке: полный проход по кадру 4K×4K на главном потоке не нужен и дорог.
            int step = Mathf.Max(1, pixels.Length / 4096);
            double sum = 0;
            int counted = 0;
            for (int i = 0; i < pixels.Length; i += step)
            {
                Color32 p = pixels[i];
                // Взвешенная яркость (Rec. 709): глаз чувствительнее к зелёному.
                sum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255.0;
                counted++;
            }
            if (counted == 0) return false;
            return (sum / counted) > threshold;
        }

        /// <summary>Средняя яркость кадра 0…1 — попадает в журнал, чтобы отличить «чёрный» от «тёмный».</summary>
        public static float AverageLuminance(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0) return 0f;
            int step = Mathf.Max(1, pixels.Length / 4096);
            double sum = 0;
            int counted = 0;
            for (int i = 0; i < pixels.Length; i += step)
            {
                Color32 p = pixels[i];
                sum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255.0;
                counted++;
            }
            return counted == 0 ? 0f : (float)(sum / counted);
        }

        /// <summary>
        /// ВЗЯТЬ ОДИН CPU-КАДР И ПРОВЕРИТЬ СОДЕРЖИМОЕ. Ничего не сохраняет, кроме строки журнала:
        /// это проверка, а не диагностический снимок (для снимка есть `CaptureCameraFrameOnce`).
        /// </summary>
        private IEnumerator VerifyCameraImageOnce()
        {
            if (cameraManager == null)
            {
                imageChecked = true;
                imageHasContent = false;
                yield break;
            }

            if (!cameraManager.TryAcquireLatestCpuImage(out UnityEngine.XR.ARSubsystems.XRCpuImage image))
            {
                imageChecked = true;
                imageHasContent = false;
                Debug.LogWarning("[KvAr] КАМЕРА: CPU-кадр недоступен — содержимое кадра проверить нечем");
                yield break;
            }

            using (image)
            {
                int w = Mathf.Max(2, image.width / 4);
                int h = Mathf.Max(2, image.height / 4);
                var conversion = new UnityEngine.XR.ARSubsystems.XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0, 0, image.width, image.height),
                    outputDimensions = new Vector2Int(w, h),
                    outputFormat = TextureFormat.RGBA32,
                    transformation = UnityEngine.XR.ARSubsystems.XRCpuImage.Transformation.MirrorY
                };

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                try
                {
                    image.Convert(conversion, tex.GetRawTextureData<byte>());
                    tex.Apply();
                    Color32[] pixels = tex.GetPixels32();
                    lastAverageLuminance = AverageLuminance(pixels);
                    imageHasContent = FrameHasContent(pixels);
                    Debug.Log("[KvAr] КАМЕРА: содержимое кадра проверено · " + image.width + "×" +
                              image.height + " · яркость " + lastAverageLuminance.ToString("0.000") +
                              " · " + (imageHasContent ? "изображение ЕСТЬ" : "изображение ПУСТОЕ"));
                }
                catch (System.Exception e)
                {
                    imageHasContent = false;
                    Debug.LogWarning("[KvAr] КАМЕРА: кадр не прочитан — " + e.GetType().Name + ": " + e.Message);
                }
                finally
                {
                    Object.Destroy(tex);
                }
            }

            imageChecked = true;
            imageCheckRunning = false;
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            CameraFrames++;
            if (!confirmed && CameraFrames == 10)
                Debug.Log("[KvAr] КАМЕРА: пошли кадры (10) — ARCore видит камеру");
        }

        private void RequestCamera()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            PermissionRequested = true;
            Permission.RequestUserPermission(Permission.Camera);
            StartCoroutine(WaitPermissionThenLog());
#endif
        }

        private IEnumerator WaitPermissionThenLog()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            float deadline = Time.unscaledTime + 30f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera) &&
                   Time.unscaledTime < deadline)
                yield return null;

            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[KvAr] КАМЕРА: разрешение ВЫДАНО владельцем — включаю AR-камеру");
                startedAt = Time.unscaledTime;
                if (session != null) session.Reset();
            }
            else
            {
                Debug.LogWarning("[KvAr] КАМЕРА: разрешение НЕ выдано — кнопка «Настройки приложения»");
            }
#endif
            yield break;
        }

        // ================================================================ резервный предпросмотр

        /// <summary>
        /// ИМЯ ФАЙЛА КАДРА ПО НОМЕРУ — ЧИСТАЯ ФУНКЦИЯ (тест без устройства и без диска).
        ///
        /// ЧТО БЫЛО (12.11.2026). Имя было ОДНО — `ar_camera_frame.png`, и файл писался один раз за
        /// сеанс. На приёмке в папке лежал ровно один снимок с одним таймштампом, поэтому доказать,
        /// что камера видела ВО ВРЕМЯ РАБОТЫ (например, потолок вместо стола), было нечем.
        ///
        /// ЧЕМ ПЛОХО БЕЗ ПРАВИЛА «ПОСЛЕ `max` — СНОВА С 1». Номер, растущий без предела, копил бы
        /// файлы на устройстве до заполнения памяти; номер, замирающий на `max`, перестал бы
        /// показывать свежие кадры. Поэтому счётчик КОЛЬЦЕВОЙ: 1…max, затем снова 1, и старый файл
        /// с тем же именем перезаписывается.
        ///
        /// ГРАНИЦЫ. `index` приводится к 1…max включительно (значение вне диапазона не даёт имени
        /// вне диапазона). `max` меньше 1 — тоже 1: имя `ar_frame_001.png` существует всегда, и
        /// вызывающий код не получает пустой путь.
        /// </summary>
        public static string FrameFileName(int index, int max)
        {
            int limit = max < 1 ? 1 : max;
            // КОЛЬЦО: ((index − 1) mod limit) + 1. Для index = 1 → 1, для index = limit → limit,
            // для index = limit + 1 → 1. Оба края проверяются тестом.
            int normalized = ((index - 1) % limit + limit) % limit + 1;
            return "ar_frame_" + normalized.ToString("000", System.Globalization.CultureInfo.InvariantCulture) +
                   ".png";
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА О СОХРАНЁННОМ КАДРЕ — ЧИСТАЯ ФУНКЦИЯ (числа: номер, размер, яркость, путь).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНО. Приёмке нужен не «файл есть», а «файл №3, 320×240, яркость 0.412, путь…»:
        /// по номеру видно, что снимки идут ПЕРИОДИЧЕСКИ, по яркости — что кадр не пустой, по пути —
        /// что именно забирать через adb.
        /// </summary>
        public static string DescribeFrameSaved(int index, int width, int height, float luminance,
                                                string path)
        {
            return "[KvAr] КАДР: сохранён №" + index +
                   " · " + width + "×" + height +
                   " · яркость " + luminance.ToString("0.000",
                       System.Globalization.CultureInfo.InvariantCulture) +
                   " · путь " + (string.IsNullOrEmpty(path) ? "—" : path);
        }

        /// <summary>
        /// ЗАПИСЫВАТЬ ЛИ КАДР СЕЙЧАС — ЧИСТАЯ ФУНКЦИЯ (правило «не чаще периода и не поверх
        /// незаконченного снимка»).
        ///
        /// ВЫНЕСЕНО ОТДЕЛЬНО, потому что это ровно то правило, которое на устройстве проверить
        /// нечем: накладки двух корутин дают битый PNG, а «пишем каждый кадр» — просадку на планшете.
        /// </summary>
        public static bool ShouldCaptureFrame(float now, float nextCaptureTime, bool captureRunning)
        {
            if (captureRunning) return false;       // предыдущий снимок ещё не сохранён
            return now >= nextCaptureTime;
        }

        /// <summary>
        /// СНИМОК КАДРА ARCore ДЛЯ ДИАГНОСТИКИ — ТЕПЕРЬ ПЕРИОДИЧЕСКИЙ (12.11.2026).
        ///
        /// ЧТО БЫЛО. Метод писал ОДИН файл `ar_camera_frame.png` через 2 с после подтверждения и
        /// больше никогда. На приёмке это дало ровно один снимок 144 КБ с одним таймштампом: чем
        /// камера занималась следующие пять минут работы, оставалось неизвестным — а именно там и
        /// была причина «плоскостей 0» (планшет лежал экраном вверх, камера смотрела в потолок).
        ///
        /// ЧЕМ ПЛОХО. Один снимок отвечает на вопрос «камера когда-то видела изображение», а не
        /// «что камера видела, когда сломалось распознавание».
        ///
        /// ЧТО СТАЛО. Кадр пишется раз в `framePeriodSeconds`, имена — `ar_frame_001.png` …
        /// `ar_frame_010.png` (`FrameFileName`), после десятого счёт идёт сначала и файл
        /// ПЕРЕЗАПИСЫВАЕТСЯ (память устройства не растёт). В журнал уходит строка с номером,
        /// размером, яркостью и путём. Пока предыдущий снимок не сохранён, новый не начинается.
        /// </summary>
        private IEnumerator CaptureCameraFrameOnce()
        {
            // Защита от накладок: пока снимок пишется, второй не начинаем.
            frameCaptureRunning = true;
            try
            {
                if (cameraManager == null) yield break;
                if (!cameraManager.TryAcquireLatestCpuImage(
                        out UnityEngine.XR.ARSubsystems.XRCpuImage image))
                {
                    Debug.LogWarning("[KvAr] КАМЕРА: CPU-кадр недоступен (TryAcquireLatestCpuImage = false)");
                    yield break;
                }

                using (image)
                {
                    int w = Mathf.Max(2, image.width / 2);
                    int h = Mathf.Max(2, image.height / 2);
                    var conversion = new UnityEngine.XR.ARSubsystems.XRCpuImage.ConversionParams
                    {
                        inputRect = new RectInt(0, 0, image.width, image.height),
                        outputDimensions = new Vector2Int(w, h),
                        outputFormat = TextureFormat.RGBA32,
                        transformation = UnityEngine.XR.ARSubsystems.XRCpuImage.Transformation.MirrorY
                    };
                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    float luminance = 0f;
                    byte[] png;
                    try
                    {
                        image.Convert(conversion, tex.GetRawTextureData<byte>());
                        tex.Apply();
                        // ЯРКОСТЬ — В ТУ ЖЕ СТРОКУ, ЧТО И ФАЙЛ: чёрный кадр нельзя отличить от
                        // «камера смотрит в тёмный угол», если в журнале нет числа.
                        luminance = AverageLuminance(tex.GetPixels32());
                        png = tex.EncodeToPNG();
                    }
                    finally
                    {
                        Object.Destroy(tex);
                    }

                    // КОЛЬЦЕВОЙ НОМЕР: 1…max, затем снова 1. Старый файл с тем же именем
                    // перезаписывается — так сеанс не копит снимки на устройстве.
                    int index = FrameSessionIndex + 1;
                    string name = FrameFileName(index, maxFrameFiles);
                    string path = System.IO.Path.Combine(Application.persistentDataPath, name);
                    System.IO.File.WriteAllBytes(path, png);

                    FrameSessionIndex = index;
                    FramesSaved++;
                    Debug.Log(DescribeFrameSaved(index, w, h, luminance, path));
                }
            }
            finally
            {
                frameCaptureRunning = false;
                // Следующий снимок — через период от момента, когда ЭТОТ закончился: иначе на
                // медленном устройстве снимки шли бы подряд без пауз.
                nextFrameCaptureTime = Time.unscaledTime + Mathf.Max(1f, framePeriodSeconds);
            }
        }

        /// <summary>
        /// ЗАПУСТИТЬ ПЕРИОДИЧЕСКИЙ СНИМОК, ЕСЛИ ПРИШЛО ВРЕМЯ. Вызывается из `Update`.
        ///
        /// Условие вынесено в чистую `ShouldCaptureFrame`, чтобы правило «не чаще периода и не поверх
        /// незаконченного снимка» проверялось тестом, а не чтением `Update` глазами.
        /// </summary>
        private void CaptureFrameIfDue()
        {
            if (cameraManager == null) return;
            if (!ShouldCaptureFrame(Time.unscaledTime, nextFrameCaptureTime, frameCaptureRunning)) return;
            StartCoroutine(CaptureCameraFrameOnce());
        }

        private void EnableFallbackPreview()
        {
            FallbackPreviewActive = true;
            Debug.LogWarning("[KvAr] КАМЕРА: ARCore не отдал кадры за " + fallbackAfterSeconds.ToString("0") +
                             " с — включаю РЕЗЕРВНЫЙ предпросмотр WebCamTexture");

            if (session != null) session.enabled = false;
            if (cameraManager != null) cameraManager.enabled = false;
            if (background != null) background.enabled = false;

            StartCoroutine(StartFallbackAfterRelease());
        }

        private IEnumerator StartFallbackAfterRelease()
        {
            yield return new WaitForSecondsRealtime(0.7f);

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                SetStatus("Камера не найдена устройством.\nПроверьте разрешение в настройках Android.");
                Debug.LogError("[KvAr] КАМЕРА: устройство не сообщает ни одной камеры");
                yield break;
            }

            string chosen = devices[0].name;
            for (int i = 0; i < devices.Length; i++)
            {
                if (!devices[i].isFrontFacing) { chosen = devices[i].name; break; }
            }

            webCam = new WebCamTexture(chosen, 1280, 720, 30);
            webCam.Play();
            Debug.Log("[KvAr] КАМЕРА-РЕЗЕРВ: " + chosen + " · запрошено 1280×720@30 · играет: " + webCam.isPlaying);

            fallbackRoot = new GameObject("KvArFallbackCamera", typeof(RectTransform), typeof(RawImage));
            fallbackRoot.transform.SetParent(canvas.transform, false);
            fallbackRoot.transform.SetAsFirstSibling();   // под интерфейсом
            RectTransform rect = (RectTransform)fallbackRoot.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            fallbackImage = fallbackRoot.GetComponent<RawImage>();
            fallbackImage.texture = webCam;
            fallbackImage.color = Color.white;
            fallbackImage.raycastTarget = false;

            SetStatus("Камера включена (резервный режим).\n" +
                      "Трекинг камеры не запустился — видно камеру, но не плоскости.");
        }

        // ================================================================ интерфейс

        private void ResolveReferences()
        {
            if (cameraManager == null) cameraManager = FindAnyObjectByType<ARCameraManager>();
            if (session == null) session = FindAnyObjectByType<ARSession>();
            if (background == null) background = FindAnyObjectByType<ARCameraBackground>();
        }

        private void BuildUi()
        {
            GameObject canvasGo = new GameObject("KvArCameraGateCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;   // выше AR-пульта (300)

            GameObject panel = new GameObject("GatePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1000f, 460f);
            rect.anchoredPosition = Vector2.zero;
            Image bg = panel.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.06f, 0.96f);

            GameObject titleGo = new GameObject("GateTitle", typeof(RectTransform));
            titleGo.transform.SetParent(panel.transform, false);
            RectTransform titleRect = (RectTransform)titleGo.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -24f);
            titleRect.sizeDelta = new Vector2(-60f, 60f);
            Text title = titleGo.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.text = "ДОСТУП К КАМЕРЕ";
            title.fontSize = 40;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.95f, 0.95f, 0.95f);
            title.raycastTarget = false;

            GameObject statusGo = new GameObject("GateStatus", typeof(RectTransform));
            statusGo.transform.SetParent(panel.transform, false);
            RectTransform statusRect = (RectTransform)statusGo.transform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.offsetMin = new Vector2(30f, 130f);
            statusRect.offsetMax = new Vector2(-30f, -100f);
            statusText = statusGo.AddComponent<Text>();
            statusText.font = title.font;
            statusText.fontSize = 30;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = new Color(0.9f, 0.9f, 0.85f);
            statusText.raycastTarget = false;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;

            GameObject buttonGo = new GameObject("GateButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(panel.transform, false);
            RectTransform buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 30f);
            buttonRect.sizeDelta = new Vector2(620f, 86f);
            Image buttonBg = buttonGo.GetComponent<Image>();
            buttonBg.color = new Color(0.12f, 0.35f, 0.55f, 1f);
            actionButton = buttonGo.GetComponent<Button>();
            actionButton.onClick.AddListener(OnActionPressed);

            GameObject labelGo = new GameObject("GateButtonLabel", typeof(RectTransform));
            labelGo.transform.SetParent(buttonGo.transform, false);
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            actionLabel = labelGo.AddComponent<Text>();
            actionLabel.font = title.font;
            actionLabel.fontSize = 30;
            actionLabel.alignment = TextAnchor.MiddleCenter;
            actionLabel.color = Color.white;
            actionLabel.raycastTarget = false;
            actionLabel.text = "РАЗРЕШИТЬ КАМЕРУ";

            SetStatus("Программе нужен доступ к камере: без него не видно стол.");
        }

        private void OnActionPressed()
        {
            if (awaitingContinue)
            {
                ContinueToAr();
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                RequestCamera();
                return;
            }
            OpenAppSettings();
#endif
        }

        private void ContinueToAr()
        {
            if (!awaitingContinue) return;
            awaitingContinue = false;
            Debug.Log("[KvAr] КАМЕРА: владелец подтвердил — продолжаю AR (камера уже работает)");
            HideGate();
        }

        private void ShowSettingsButton()
        {
            if (actionLabel == null) return;
            actionLabel.text = "НАСТРОЙКИ ПРИЛОЖЕНИЯ";
        }

        private void OpenAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent",
                           "android.settings.APPLICATION_DETAILS_SETTINGS"))
                using (var uri = new AndroidJavaClass("android.net.Uri"))
                {
                    var data = uri.CallStatic<AndroidJavaObject>("fromParts", "package",
                        activity.Call<string>("getPackageName"), null);
                    intent.Call<AndroidJavaObject>("setData", data);
                    activity.Call("startActivity", intent);
                }
                Debug.Log("[KvAr] КАМЕРА: открыты настройки приложения (разрешения Android)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KvAr] КАМЕРА: не удалось открыть настройки: " + e.Message);
            }
#endif
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        private void HideGate()
        {
            if (canvas != null && fallbackRoot == null) canvas.gameObject.SetActive(false);
        }

        /// <summary>ЭКРАН ПОДТВЕРЖДЕНИЯ ЕЩЁ ЖДЁТ НАЖАТИЯ (для рантайма и журнала).</summary>
        public bool AwaitingContinue { get { return awaitingContinue; } }

        /// <summary>
        /// СНЯТЬ ПОДТВЕРЖДЕНИЕ СНАРУЖИ (07.11.2026). Дефект планшета: окно «ДОСТУП К КАМЕРЕ /
        /// ПРОДОЛЖИТЬ» висело на экране минутами, хотя кадры уже шли и трекинг работал — владельцу
        /// приходилось каждый раз нажимать кнопку. Внутренний автопродолжение живёт в `Update`
        /// этого компонента; если тот по какой-то причине не тикает, снимаем окно из `KvArRuntime`,
        /// у которого обновление доказано строкой состояния. Возвращает true, если окно снято.
        /// </summary>
        public bool ConfirmNow(string reason)
        {
            if (!awaitingContinue) return false;
            awaitingContinue = false;
            Debug.Log("[KvAr] КАМЕРА: подтверждение снято автоматически (" + reason +
                      ") — камера уже работает, кадров " + CameraFrames);
            HideGate();
            return true;
        }

        /// <summary>
        /// Строка для журнала/отчёта. Снимки добавлены 12.11.2026: раньше по этой строке нельзя было
        /// понять, есть ли вообще доказательство того, что камера видела во время работы.
        /// </summary>
        public string Describe()
        {
            return "стража камеры: кадров " + CameraFrames +
                   " · разрешение " + (PermissionRequested ? "запрошено" : "не требовалось") +
                   " · резерв " + (FallbackPreviewActive ? "включён" : "не нужен") +
                   " · снимков " + FramesSaved + " из " + maxFrameFiles +
                   " (последний " + FrameFileName(FrameSessionIndex <= 0 ? 1 : FrameSessionIndex,
                                                    maxFrameFiles) + ")";
        }
    }
}
