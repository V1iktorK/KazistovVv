using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvXr
{
    /// <summary>
    /// ВИНЬЕТКА КОМФОРТА (шаг 2 плана VR-управления, 05.11.2026).
    ///
    /// ЗАЧЕМ. Плавный ход — главный источник «укачивания» в VR: картинка едет, а вестибулярный
    /// аппарат молчит. Стандартное лечение — затемнить края экрана во время движения: у глаза
    /// остаётся только центральная зона, и расхождение «картинка/тело» переживается легче.
    /// Владелец просил «как во всех играх» — виньетка есть в большинстве VR-игр именно поэтому.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ И ПОЧЕМУ:
    ///   • шейдера и текстур — четыре чёрных полосы по краям дают тот же эффект без новых ассетов
    ///     (и без риска «розового экрана» от чужого шейдера в HDRP/URP/GLES);
    ///   • виньетки в телепорте и MR — она нужна только при непрерывном движении; в телепорте
    ///     экран не «едет», а MR — реальная комната, затемнять её незачем.
    ///
    /// НАСТРОЙКИ. Включение и сила — `KvXrSettings.GetVignetteEnabled/GetVignetteStrength`
    /// (PlayerPrefs), поэтому подбираются без пересборки APK.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrComfortVignette : MonoBehaviour
    {
        /// <summary>Доля экрана, которую занимает затемнение с каждого края.</summary>
        public float bandFraction = 0.16f;

        /// <summary>Скорость нарастания/угасания затемнения (доля в секунду).</summary>
        public float fadeSpeed = 2.5f;

        /// <summary>Создать виньетку, если её ещё нет (сцена не редактируется — только рантайм).</summary>
        public static KvXrComfortVignette Ensure(Transform parent, Camera camera)
        {
            KvXrComfortVignette existing = Object.FindAnyObjectByType<KvXrComfortVignette>();
            if (existing != null) return existing;

            var go = new GameObject("KvXrComfortVignette");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<KvXrComfortVignette>();
        }

        private Canvas canvas;
        private CanvasGroup group;
        private float currentAlpha;
        private int bandCount;

        void Awake()
        {
            Build();
        }

        void LateUpdate()
        {
            if (group == null) return;

            float target = 0f;
            if (KvXrSettings.GetVignetteEnabled() &&
                KvXrSettings.GetMoveMode() == KvXrMoveMode.Smooth)
            {
                KvXrLocomotion locomotion = KvXrLocomotion.Active;
                if (locomotion != null)
                {
                    // Нарастает не с первого сантиметра, а с половины скорости: иначе виньетка
                    // мешает осмотреться при медленном подкрадывании к столу.
                    float ratio = Mathf.InverseLerp(0.5f, 1f, locomotion.SmoothSpeedRatio);
                    target = KvXrSettings.GetVignetteStrength() * ratio;
                }
            }

            currentAlpha = Mathf.MoveTowards(currentAlpha, target,
                Mathf.Max(0.01f, fadeSpeed) * Time.unscaledDeltaTime);
            group.alpha = currentAlpha;
        }

        /// <summary>СОБРАТЬ ОВЕРЛЕЙ: канвас поверх всего, 4 полосы по краям, без перехвата касаний.</summary>
        /// <summary>СОБРАТЬ ОВЕРЛЕЙ: канвас поверх всего, 4 полосы по краям, без перехвата касаний.</summary>
        private void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;                      // поверх интерфейса
            group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            bandCount = 0;
            float band = Mathf.Clamp(bandFraction, 0.05f, 0.35f);
            AddBand("Сверху", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, Screen.height * band));
            AddBand("Снизу", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, Screen.height * band));
            AddBand("Слева", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(Screen.width * band, 0f));
            AddBand("Справа", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(Screen.width * band, 0f));
        }

        private void AddBand(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                             Vector2 size)
        {
            var go = new GameObject("Band_" + name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 1f);
            image.raycastTarget = false;

            bandCount++;
        }

        /// <summary>Строка для журнала: сколько полос собрано и какая сила.</summary>
        public string Status
        {
            get
            {
                return string.Format("виньетка комфорта: полос {0} · включена {1} · сила {2:0.00}",
                    bandCount, KvXrSettings.GetVignetteEnabled() ? "да" : "нет",
                    KvXrSettings.GetVignetteStrength());
            }
        }
    }
}
