using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KazistovVvXr
{
    /// <summary>
    /// ПЛОСКАЯ ПАНЕЛЬ ИНТЕРФЕЙСА В МИРОВОМ ПРОСТРАНСТВЕ (решение владельца [D1], этап 3).
    ///
    /// ПРОБЛЕМА, КОТОРУЮ ЭТО РЕШАЕТ. Интерфейс проекта — Canvas в режиме Screen Space
    /// Overlay: он рисуется ПОВЕРХ кадра, минуя камеры. В стерео это не работает: каждый
    /// глаз должен получить свою картинку, а overlay рисуется один раз на экран. В шлеме
    /// такой интерфейс либо не виден, либо виден плоско и «мимо глаз».
    ///
    /// РЕШЕНИЕ (вариант А, выбранный владельцем): канвас переводится в World Space и
    /// вешается перед оператором как накладной экран. Все панели, дерево, свойства,
    /// тулбар и настройки остаются ровно теми же объектами — меняется только способ
    /// их отображения. Это и есть «плоская панель» из решения [D1].
    ///
    /// ПОЧЕМУ ГЕОМЕТРИЯ СЧИТАЕТСЯ В <see cref="KvXrUiPanel"/>. Размеры канваса перестают
    /// быть пикселями и становятся метрами, а ошибиться тут легко на порядок (панель
    /// шириной «1920» — это стена в 1,9 км). Расчёты заперты в проверенный тестами тип,
    /// здесь только применение.
    ///
    /// ЧТО ДЕЛАЕТСЯ С МАСШТАБОМ КАНВАСА. `CanvasScaler` в режиме ScaleWithScreenSize
    /// в мировом пространстве не нужен и вреден — он умножает масштаб на неизвестное
    /// число. Он переводится в ConstantPixelSize с множителем 1, а нужный размер задаётся
    /// масштабом самого канваса: 1 единица канваса = worldScale метров.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrPanelAnchor : MonoBehaviour
    {
        /// <summary>Камера шлема. Пусто — найдётся автоматически.</summary>
        public Camera targetCamera;

        /// <summary>Следовать за головой (панель «прилипает» к взгляду).</summary>
        public bool followHead = true;

        /// <summary>Скорость догона при следовании: 0 — мгновенно, иначе — плавно.</summary>
        public float followSmoothSeconds = 0.35f;

        /// <summary>Опорная ширина канваса проекта (та же, что у оболочки интерфейса).</summary>
        public float referenceWidth = 1920f;

        /// <summary>Опорная высота канваса проекта.</summary>
        public float referenceHeight = 1080f;

        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform rect;
        private RenderMode previousMode;
        private bool applied;

        /// <summary>Панель уже переведена в мировое пространство (для журнала и проверок).</summary>
        public bool Applied { get { return applied; } }

        /// <summary>Фактическая ширина панели в метрах после применения.</summary>
        public float WidthMeters { get; private set; }

        /// <summary>Последний применённый расчёт (для журнала и отчёта).</summary>
        public KvXrUiPanelSettings LastSettings { get; private set; }

        void OnEnable()
        {
            KvXrSettingsChanged.SettingsChanged += OnSettingsChanged;
        }

        void OnDisable()
        {
            KvXrSettingsChanged.SettingsChanged -= OnSettingsChanged;
        }

        private void OnSettingsChanged()
        {
            // Смена расстояния панели в настройках должна примениться сразу: иначе оператор
            // крутит ползунок и не понимает, почему ничего не изменилось.
            if (applied) Apply();
        }

        void Start()
        {
            TryAcquireCanvas();
        }

        void LateUpdate()
        {
            // Канвас интерфейса создаётся менеджером оболочки в ЕГО `Start`, а порядок
            // выполнения `Start` между объектами Unity не гарантирует. Поэтому если канваса
            // ещё нет — не отключаемся, а пробуем снова каждый кадр, пока он не появится.
            // Дефект был бы такой: панель «молча не работает» в шлеме, и причину не видно.
            if (canvas == null && !acquireFailed)
            {
                TryAcquireCanvas();
                if (canvas == null) return;
            }

            if (!applied || !followHead) return;
            Camera cam = ResolveCamera();
            if (cam == null) return;

            KvXrUiPanelSettings s = KvXrUiPanel.SettingsFor(
                cam.transform.position, cam.transform.rotation,
                KvXrSettings.BuildRigConfig().eyeHeightMeters,
                KvXrSettings.GetPanelDistance());

            Transform t = canvas.transform;
            float k = followSmoothSeconds <= 0f ? 1f
                : 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, followSmoothSeconds));
            t.position = Vector3.Lerp(t.position, s.position, k);
            t.rotation = Quaternion.Slerp(t.rotation, s.rotation, k);
            LastSettings = s;
        }

        private bool acquireFailed;

        /// <summary>Найти канвас и применить панель. Возвращает true, если панель применена.</summary>
        private bool TryAcquireCanvas()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null) canvas = FindProjectCanvas();

            if (canvas == null)
            {
                // Ждём разумное время: если интерфейса в сцене нет вовсе — сообщаем честно,
                // а не молчим (в VR отсутствие панели выглядит как «шлем не работает»).
                if (Time.timeSinceLevelLoad > 5f && !acquireFailed)
                {
                    acquireFailed = true;
                    Debug.LogWarning("[KvXr] Плоская панель: канвас интерфейса не найден за 5 с — " +
                        "интерфейс в шлеме показан не будет. Ожидается KazistovVv_UI в сцене.");
                }
                return false;
            }

            Apply();
            return true;
        }

        /// <summary>
        /// Канвас оболочки живёт на отдельном объекте (`KazistovVv_UI`), а не на панели,
        /// поэтому его надо найти в сцене по имени, которое задаёт менеджер интерфейса.
        /// </summary>
        private static Canvas FindProjectCanvas()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] == null) continue;
                if (canvases[i].name == "KazistovVvCanvas") return canvases[i];
            }
            return null;
        }

        /// <summary>Перевести канвас в мировое пространство и поставить перед оператором.</summary>
        public void Apply()
        {
            if (canvas == null) return;

            previousMode = canvas.renderMode;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = ResolveCamera();

            scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                scaler.dynamicPixelsPerUnit = 1f;
            }

            rect = canvas.transform as RectTransform;
            if (rect != null) rect.sizeDelta = new Vector2(referenceWidth, referenceHeight);

            // Raycaster нужен, чтобы луч контроллера видел кнопки в мировом пространстве.
            EnsureRaycaster(canvas.gameObject);

            PlaceAtOnce();
            applied = true;
        }

        /// <summary>Поставить панель немедленно, без сглаживания (старт, смена настроек).</summary>
        public void PlaceAtOnce()
        {
            Camera cam = ResolveCamera();
            if (cam == null || canvas == null) return;

            KvXrUiPanelSettings s = KvXrUiPanel.SettingsFor(
                cam.transform.position, cam.transform.rotation,
                KvXrSettings.BuildRigConfig().eyeHeightMeters,
                KvXrSettings.GetPanelDistance());

            Transform t = canvas.transform;
            t.position = s.position;
            t.rotation = s.rotation;

            // Масштаб: панель заданной ширины в метрах. Мировой масштаб берётся из расчёта,
            // а не из CanvasScaler — иначе размер зависел бы от разрешения экрана.
            float scale = s.worldScale > 0f ? s.worldScale : 0.001f;
            t.localScale = new Vector3(scale, scale, scale);

            WidthMeters = s.widthMeters;
            LastSettings = s;
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null) return targetCamera;
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                Camera[] all = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].enabled) { targetCamera = all[i]; break; }
                }
            }
            return targetCamera;
        }

        /// <summary>
        /// Луч контроллера ищет элементы интерфейса только через графический райкастер.
        /// В проектном канвасе он уже есть, но проверка нужна: у канваса, созданного кодом
        /// без райкастера, кнопки «не нажимаются», и причина не видна.
        /// </summary>
        private static void EnsureRaycaster(GameObject canvasGo)
        {
            if (canvasGo.GetComponent<GraphicRaycaster>() == null &&
                canvasGo.GetComponent<BaseRaycaster>() == null)
                canvasGo.AddComponent<GraphicRaycaster>();
        }

        /// <summary>Строка состояния для журнала: размер панели и расстояние до неё.</summary>
        public string Describe()
        {
            return string.Format("XR-панель: {0:0.00}×{1:0.00} м на расстоянии {2:0.00} м (режим {3})",
                WidthMeters,
                KvXrUiPanel.HeightMeters(WidthMeters, referenceWidth, referenceHeight),
                KvXrSettings.GetPanelDistance(),
                canvas != null ? canvas.renderMode.ToString() : "нет канваса");
        }
    }
}
