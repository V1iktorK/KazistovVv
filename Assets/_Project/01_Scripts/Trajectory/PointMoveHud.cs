using TrajectoryCore;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;

/// <summary>
/// Индикатор РЕЖИМА ПЕРЕМЕЩЕНИЯ ТОЧКИ (ТЗ: этапы 2, 4, 6).
///
/// Состоит из двух частей:
///   1) HUD (screen-space overlay, uGUI в стиле KazistovVv): крупная панель с координатами точки
///      ОТНОСИТЕЛЬНО НУЛЯ РОБОТА (его Base) и статусом достижимости — зелёный/жёлтый/красный.
///      Панель стоит сверху по центру (ниже верхней панели KazistovVv) и не перекрывает точку и робота.
///   2) Маркер (world space): неоновый шарик в самой перемещаемой точке, видимый СКВОЗЬ геометрию
///      («рентген», как у фантомов) — точка может уходить в стену, и её всё равно видно.
///
/// Компонент создаётся потоком этапов (`TrajectoryFlowController.Awake`) и не требует сцены.
/// </summary>
public class PointMoveHud : MonoBehaviour
{
    [Header("Маркер точки (world space)")]
    public float markerSize = 0.085f;
    public float markerAlpha = 0.85f;
    public float markerPulse = 0.15f;

    [Header("HUD (screen space, в стиле KazistovVv)")]
    public float panelWidth = 700f;
    public float panelHeight = 156f;
    [Tooltip("Отступ панели от верхнего края экрана (ниже верхней панели KazistovVv).")]
    public float topOffset = 92f;
    public int sortingOrder = 60;      // выше канваса KazistovVv (0)

    // --- цвета статуса (та же неоновая палитра, что у шарика прицела) ---
    private static readonly Color ColorSafe = new Color(0.45f, 1f, 0.02f);
    private static readonly Color ColorMarginal = new Color(1f, 0.72f, 0f);
    private static readonly Color ColorBad = new Color(1f, 0.03f, 0.42f);

    private GameObject root;           // контейнер HUD (канвас)
    private GameObject marker;
    private Renderer markerRenderer;
    private Material markerMaterial;
    private Image strip;               // цветная полоса статуса
    private Text titleText;
    private Text coordText;
    private Text deltaText;
    private Text statusText;
    private Text hintText;
    private bool built;
    private bool visible;              // виден ли HUD-индикатор режима (панель)
    private bool markerVisible;        // виден ли маркер самой точки
    private float pulsePhase;
    private Color currentColor = ColorSafe;
    private Vector3 originLocal;       // позиция входа в режим (в системе робота)
    private Vector3 localNow;          // текущая позиция (в системе робота)

    private const string Title = "РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ";
    private const string Hint = "Q/E — вверх/вниз · W/S — вперёд/назад · A/D — влево/вправо · " +
                                "Shift — быстрее · Enter — подтвердить · Esc — отмена";

    /// <summary>Виден ли крупный индикатор режима (панель).</summary>
    public bool IsVisible => visible;
    /// <summary>Виден ли маркер точки (точка видна всегда, когда она зафиксирована).</summary>
    public bool IsMarkerVisible => markerVisible;
    /// <summary>Текущие координаты в панели (диагностика/проверки).</summary>
    public string CoordsText => coordText != null ? coordText.text : "";
    /// <summary>Строка смещения от исходной позиции (диагностика/проверки).</summary>
    public string DeltaText => deltaText != null ? deltaText.text : "";
    /// <summary>Строка статуса достижимости (диагностика/проверки).</summary>
    public string StatusText => statusText != null ? statusText.text : "";

    private void Awake()
    {
        Build();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (markerMaterial != null) Destroy(markerMaterial);
        if (marker != null) Destroy(marker);
        if (root != null) Destroy(root);
    }

    // ------------------------------------------------------------------ сборка

    private void Build()
    {
        if (built) return;
        built = true;

        // --- HUD (screen space overlay) ---
        root = new GameObject("PointMoveHud", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        root.hideFlags = HideFlags.HideInHierarchy;   // служебный объект рантайма — не в иерархии
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform canvasRect = (RectTransform)root.transform;

        Image panel = KvTheme.CreatePanel(canvasRect, "Panel", new Color(0.10f, 0.11f, 0.13f, 0.88f));
        // Панель HUD — только индикация: она НЕ должна перехватывать клики и «быть под курсором»
        // для EventSystem (иначе колесо/средняя кнопка в этой зоне считались бы «над панелью KazistovVv»,
        // а кнопки верхней панели под ней не нажимались бы).
        panel.raycastTarget = false;
        RectTransform prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 1f);
        prt.anchorMax = new Vector2(0.5f, 1f);
        prt.pivot = new Vector2(0.5f, 1f);
        prt.anchoredPosition = new Vector2(0f, -topOffset);
        prt.sizeDelta = new Vector2(panelWidth, panelHeight);

        // Цветная полоса статуса слева — «лампочка» режима.
        strip = KvTheme.CreatePanel(prt, "StatusStrip", ColorSafe);
        strip.raycastTarget = false;
        RectTransform srt = strip.rectTransform;
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(0f, 1f);
        srt.pivot = new Vector2(0f, 0.5f);
        srt.anchoredPosition = Vector2.zero;
        srt.sizeDelta = new Vector2(10f, 0f);

        titleText = KvTheme.CreateText(prt, "Title", Title, 16, TextAnchor.UpperLeft, KvTheme.TextDim);
        RectTransform trt = titleText.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(-36f, 20f);
        trt.anchoredPosition = new Vector2(4f, -8f);

        coordText = KvTheme.CreateText(prt, "Coords", "X: 0.000  Y: 0.000  Z: 0.000", 28,
            TextAnchor.MiddleLeft, KvTheme.TextMain);
        RectTransform crt = coordText.rectTransform;
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.sizeDelta = new Vector2(-36f, 38f);
        crt.anchoredPosition = new Vector2(4f, -32f);

        // Смещение относительно позиции входа в режим: видно, куда оператор сдвинул точку.
        deltaText = KvTheme.CreateText(prt, "Delta", "Δ от исходной: 0.000 / 0.000 / 0.000", 13,
            TextAnchor.MiddleLeft, KvTheme.TextDim);
        RectTransform drt = deltaText.rectTransform;
        drt.anchorMin = new Vector2(0f, 1f);
        drt.anchorMax = new Vector2(1f, 1f);
        drt.pivot = new Vector2(0.5f, 1f);
        drt.sizeDelta = new Vector2(-36f, 20f);
        drt.anchoredPosition = new Vector2(4f, -70f);

        statusText = KvTheme.CreateText(prt, "Status", "—", 15, TextAnchor.MiddleLeft, ColorSafe);
        RectTransform strt = statusText.rectTransform;
        strt.anchorMin = new Vector2(0f, 0f);
        strt.anchorMax = new Vector2(1f, 0f);
        strt.pivot = new Vector2(0.5f, 0f);
        strt.sizeDelta = new Vector2(-36f, 22f);
        strt.anchoredPosition = new Vector2(4f, 26f);

        hintText = KvTheme.CreateText(prt, "Hint", Hint, 12, TextAnchor.MiddleLeft, KvTheme.TextDim);
        RectTransform hrt = hintText.rectTransform;
        hrt.anchorMin = new Vector2(0f, 0f);
        hrt.anchorMax = new Vector2(1f, 0f);
        hrt.pivot = new Vector2(0.5f, 0f);
        hrt.sizeDelta = new Vector2(-36f, 20f);
        hrt.anchoredPosition = new Vector2(4f, 6f);

        // --- маркер точки (world space, «рентген») ---
        marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "PointMoveMarker";
        marker.hideFlags = HideFlags.HideInHierarchy;
        Collider col = marker.GetComponent<Collider>();
        if (col != null)
        {
            // Коллайдер выключаем СРАЗУ (Destroy — только в конце кадра): маркер не должен
            // попадать в луч прицела и в CollisionWorld даже в первом кадре.
            col.enabled = false;
            Destroy(col);
        }
        marker.transform.localScale = Vector3.one * markerSize;

        // Материал метки — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP).
        markerMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLit(
            "Материал_метки_перемещения", Color.white);
        markerRenderer = marker.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            markerRenderer.material = markerMaterial;
            markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
        }
        ApplyColor(ColorSafe);
        marker.SetActive(false);

        // Служебные объекты рантайма не должны появляться в Hierarchy (правило проекта).
        HideRecursive(root);
        SetVisible(false);
    }

    /// <summary>Скрыть объект и всех потомков из окна Hierarchy (HideFlags не наследуются).</summary>
    private static void HideRecursive(GameObject go)
    {
        if (go == null) return;
        go.hideFlags = HideFlags.HideInHierarchy;
        foreach (Transform child in go.transform) HideRecursive(child.gameObject);
    }

    // ------------------------------------------------------------------ управление

    /// <summary>Показать индикатор: маркер в мировой точке, координаты — в системе робота.</summary>
    public void Show(Vector3 worldPoint, Vector3 pointInRobotFrame)
    {
        Build();
        SetVisible(true);
        SetPoint(worldPoint, pointInRobotFrame);
    }

    /// <summary>Показывать/скрывать индикатор целиком (HUD режима + маркер точки).</summary>
    public void SetVisible(bool value)
    {
        SetHudVisible(value);
        SetMarkerVisible(value);
    }

    /// <summary>
    /// Крупный индикатор РЕЖИМА (панель). Виден только в `PointMoveMode`
    /// (ТЗ: «Индикатор режима … виден только в PointMoveMode»).
    /// </summary>
    public void SetHudVisible(bool value)
    {
        visible = value;
        if (root != null) root.SetActive(value);
    }

    /// <summary>
    /// Маркер зафиксированной точки. Точка «видна всегда» (ТЗ): маркер остаётся
    /// и после выхода из режима, скрывается только при сбросе/завершении сценария.
    /// </summary>
    public void SetMarkerVisible(bool value)
    {
        markerVisible = value;
        if (marker != null) marker.SetActive(value);
    }

    /// <summary>Исходная позиция точки (в системе робота) — для строки «Δ от исходной».</summary>
    public void SetOrigin(Vector3 originInRobotFrame)
    {
        originLocal = originInRobotFrame;
        RefreshDelta();
    }

    /// <summary>Позиция точки: маркер ставится в мире, координаты — в системе робота.</summary>
    public void SetPoint(Vector3 worldPoint, Vector3 pointInRobotFrame)
    {
        if (!built) Build();
        if (marker != null) marker.transform.position = worldPoint;
        localNow = pointInRobotFrame;
        if (coordText != null)
            coordText.text = string.Format("X: {0:F3}   Y: {1:F3}   Z: {2:F3}",
                pointInRobotFrame.x, pointInRobotFrame.y, pointInRobotFrame.z);
        RefreshDelta();
    }

    /// <summary>Строка смещения относительно позиции входа в режим (онлайн, каждый кадр).</summary>
    private void RefreshDelta()
    {
        if (deltaText == null) return;
        Vector3 d = localNow - originLocal;
        deltaText.text = string.Format("Δ от исходной: {0:+0.000;-0.000;0.000} / {1:+0.000;-0.000;0.000} / {2:+0.000;-0.000;0.000}",
            d.x, d.y, d.z);
    }

    /// <summary>Статус достижимости (этап 4): цвет полосы и шарика + текст причины.</summary>
    public void SetVerdict(ReachVerdict verdict, string reason)
    {
        Color c = verdict == ReachVerdict.Safe ? ColorSafe
                : verdict == ReachVerdict.Marginal ? ColorMarginal : ColorBad;
        string label = verdict == ReachVerdict.Safe ? "ДОСТИЖИМО"
                     : verdict == ReachVerdict.Marginal ? "БЛИЗКО К ПРЕДЕЛУ"
                     : verdict == ReachVerdict.Collision ? "НЕДОСТИЖИМО (столкновение)"
                     : "НЕДОСТИЖИМО";
        if (statusText != null)
        {
            statusText.text = label + (string.IsNullOrEmpty(reason) ? "" : " · " + reason);
            statusText.color = c;
        }
        ApplyColor(c);
    }

    /// <summary>Дополнительная строка вместо подсказки (например, «нет робота»).</summary>
    public void SetHint(string text)
    {
        if (hintText != null) hintText.text = string.IsNullOrEmpty(text) ? Hint : text;
    }

    private void ApplyColor(Color c)
    {
        currentColor = c;
        if (strip != null) strip.color = c;
        if (markerMaterial != null) GhostMaterial.MakeGhost(markerMaterial, c, markerAlpha);
    }

    private void Update()
    {
        if (!markerVisible || marker == null) return;
        // Лёгкая пульсация — маркер не «сливается» с фоном.
        pulsePhase += Time.deltaTime * 5f;
        float k = 1f + markerPulse * Mathf.Sin(pulsePhase);
        marker.transform.localScale = Vector3.one * (markerSize * k);
    }
}
