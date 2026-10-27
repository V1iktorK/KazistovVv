using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KazistovVvFeatures;

namespace KazistovVvXr
{
    /// <summary>
    /// ЧЕМ ПОКАЗЫВАТЬ КОНТРОЛЛЕРЫ В ШЛЕМЕ (сессия §37, пункт 1 задания).
    ///
    /// ЗАДАЧА ВЛАДЕЛЬЦА ДОСЛОВНО: «Сейчас в шлеме контроллеры "невидимые": я держу их в руках,
    /// приложение их знает, но на картинке нет ни моделей, ни значков, которые показывали бы, где
    /// мои руки и куда направлен луч». Выбор способа — за владельцем, и он выбран ОПРОСОМ §37:
    /// готовый меш PICO 4 Ultra из SDK, материал наш, при неудаче — примитивы.
    ///
    /// ПОЧЕМУ МАТЕРИАЛ НАШ, А НЕ ИЗ SDK. Префабы SDK (`Assets/Resources/Controller/PICO 4U L|R`)
    /// собраны на шейдере `o_com_shader_code` — это шейдер ВСТРОЕННОГО конвейера Unity. Сцена
    /// шлема собирается на URP (решение [D9]), и на чужом конвейере такой материал даёт ровно то,
    /// что владелец один раз уже видел: розовую поверхность. Поэтому ВСЕ материалы префаба
    /// заменяются нашим (`KvRenderPipelineShaders`), а не «настраиваются».
    ///
    /// ПОЧЕМУ СЦЕНА НЕ МЕНЯЕТСЯ. Компонент создаётся в РАНТАЙМЕ из `KvXrRig` — файл `XrMain.unity`
    /// не правится вовсе, и хеш сцены остаётся прежним. Меньше правок — меньше риска сломать то,
    /// что уже принято ([F1]).
    ///
    /// ТРИ ЛОВУШКИ, КОТОРЫЕ ЗДЕСЬ ЗАКРЫТЫ ЯВНО:
    ///   1. ЛОЖНАЯ ПОЗА ([F2]): поза устройства может прийти «нулевой», формально достоверной.
    ///      Модель в начале координат — это «контроллер на полу», поэтому такая поза ОТВЕРГАЕТСЯ.
    ///   2. ДВОЕНИЕ: на руку создаётся РОВНО ОДНА модель, повторный вызов `Ensure` переиспользует
    ///      прежнюю; при `isTracked = false` модель СКРЫВАЕТСЯ, а не остаётся висеть в воздухе.
    ///   3. ЛУЧ ТЕЛЕПОРТА: у префаба SDK есть коллайдеры — они перехватили бы луч телепорта и
    ///      «панель перестала телепортировать». Коллайдеры удаляются, слой — `Ignore Raycast`.
    ///
    /// ЧЕГО ЭТО НЕ ДЕЛАЕТ: не анимирует нажатие курка. Аниматоры префаба выключаются: они стоят
    /// кадров, а владелец просил «без просадки кадров» ([S2.8]). Нажатие видно по иконке панели.
    /// </summary>
    public enum KvXrControllerVisualMode
    {
        /// <summary>Готовый меш PICO 4 Ultra из SDK, материалы наши (выбор владельца §37).</summary>
        Mesh = 0,

        /// <summary>Модель из примитивов: цилиндр-рукоять, сфера-торец, кольцо. Нулевой риск розового.</summary>
        Primitives = 1,

        /// <summary>Плоский значок у кисти, всегда развёрнут к оператору.</summary>
        Billboard = 2,

        /// <summary>Не показывать ничего (аварийное отключение настройкой/файлом подстройки).</summary>
        Off = 3
    }

    [DisallowMultipleComponent]
    public class KvXrControllerVisuals : MonoBehaviour
    {
        /// <summary>Слой `Ignore Raycast`: модель руки не должна перехватывать луч телепорта.</summary>
        public const int IgnoreRaycastLayer = 2;

        /// <summary>Путь правой модели в папке `Resources` пакета SDK.</summary>
        public const string SdkModelRight = "Controller/PICO 4U R";

        /// <summary>Путь левой модели в папке `Resources` пакета SDK.</summary>
        public const string SdkModelLeft = "Controller/PICO 4U L";

        /// <summary>Габарит, ниже которого модель считается «не контроллером», метры.</summary>
        public const float MinPlausibleSizeMeters = 0.04f;

        /// <summary>Габарит, выше которого модель считается «не контроллером», метры.</summary>
        public const float MaxPlausibleSizeMeters = 0.60f;

        /// <summary>Сколько секунд модель остаётся видимой после потери трекинга (защита от мигания).</summary>
        public const float HideGraceSeconds = 0.30f;

        /// <summary>Камера шлема — по ней разворачиваются значки-билборды.</summary>
        public Camera headCamera;

        /// <summary>Сообщать в журнал о смене состояния (появление, пропажа, смена режима).</summary>
        public bool logOnChange = true;

        private readonly Dictionary<KvXrQuickHand, GameObject> models = new Dictionary<KvXrQuickHand, GameObject>();
        private readonly Dictionary<KvXrQuickHand, float> lostSince = new Dictionary<KvXrQuickHand, float>();
        private KvXrControllerVisualMode mode = KvXrControllerVisualMode.Off;
        private Material sharedMaterial;
        private Material rightTint;
        private Material leftTint;
        private string modelDescription = "не построено";
        private bool built;
        private int shownCount = -1;
        private string lastState;

        /// <summary>Сколько моделей видно прямо сейчас (0, 1 или 2).</summary>
        public int ShownCount { get; private set; }

        /// <summary>Чем построены модели — строка для журнала и отчёта.</summary>
        public string ModelDescription { get { return modelDescription; } }

        /// <summary>Действующий режим визуала.</summary>
        public KvXrControllerVisualMode Mode { get { return mode; } }

        // ==================================================== ЧИСТЫЕ ФУНКЦИИ (проверяются тестами)

        /// <summary>Индекс настройки → режим. Мусор приводит к мешу, а не к «ничего не видно».</summary>
        public static KvXrControllerVisualMode ModeFromIndex(int index)
        {
            switch (index)
            {
                case (int)KvXrControllerVisualMode.Primitives: return KvXrControllerVisualMode.Primitives;
                case (int)KvXrControllerVisualMode.Billboard: return KvXrControllerVisualMode.Billboard;
                case (int)KvXrControllerVisualMode.Off: return KvXrControllerVisualMode.Off;
                default: return KvXrControllerVisualMode.Mesh;
            }
        }

        /// <summary>Название режима для журнала.</summary>
        public static string ModeName(KvXrControllerVisualMode value)
        {
            switch (value)
            {
                case KvXrControllerVisualMode.Mesh: return "меш PICO 4 Ultra (SDK)";
                case KvXrControllerVisualMode.Primitives: return "примитивы";
                case KvXrControllerVisualMode.Billboard: return "значок у кисти";
                default: return "выключено";
            }
        }

        /// <summary>Путь к мешу SDK для руки.</summary>
        public static string SdkModelPath(KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Left ? SdkModelLeft : SdkModelRight;
        }

        /// <summary>
        /// ГОДИТСЯ ЛИ ПОЗА ДЛЯ МОДЕЛИ. Три отказа, и все три взяты из живых дефектов проекта:
        /// нет признака отслеживания; поза нулевая (ловушка [F2] — «контроллер на полу»);
        /// поза невозможная (дальше трёх метров от оператора в комнате не бывает).
        /// </summary>
        public static bool IsPoseUsable(bool isTracked, Vector3 position)
        {
            if (!isTracked) return false;
            float distance = position.magnitude;
            if (distance < 0.01f) return false;
            if (distance > 3f) return false;
            return true;
        }

        // ==================================================== СОЗДАНИЕ

        /// <summary>
        /// НАЙТИ ИЛИ СОЗДАТЬ ВИЗУАЛ. Идемпотентно: второй вызов возвращает тот же компонент,
        /// второй модели на руку не появляется (это и есть защита от «двоения»).
        /// </summary>
        public static KvXrControllerVisuals Ensure(Transform parent, Camera camera)
        {
            KvXrControllerVisuals existing = Object.FindAnyObjectByType<KvXrControllerVisuals>();
            if (existing != null)
            {
                if (existing.headCamera == null) existing.headCamera = camera;
                return existing;
            }

            var go = new GameObject("XrControllerVisuals");
            if (parent != null) go.transform.SetParent(parent, false);
            KvXrControllerVisuals visuals = go.AddComponent<KvXrControllerVisuals>();
            visuals.headCamera = camera;
            return visuals;
        }

        void Start()
        {
            if (headCamera == null) headCamera = Camera.main;
            Build(KvXrTuning.VisualsMode);
        }

        void LateUpdate()
        {
            KvXrControllerVisualMode wanted = KvXrTuning.VisualsMode;
            if (!built || wanted != mode) Build(wanted);
            if (mode == KvXrControllerVisualMode.Off) return;

            bool rightShown = Place(KvXrQuickHand.Right);
            bool leftShown = Place(KvXrQuickHand.Left);

            int count = (rightShown ? 1 : 0) + (leftShown ? 1 : 0);
            ShownCount = count;

            if (logOnChange && count != shownCount)
            {
                shownCount = count;
                LogState(count);
            }
        }

        /// <summary>ПОСТРОИТЬ МОДЕЛИ ПО РЕЖИМУ (полностью заново — прежние убираются).</summary>
        public void Build(KvXrControllerVisualMode wanted)
        {
            Clear();
            mode = wanted;

            if (wanted == KvXrControllerVisualMode.Off)
            {
                modelDescription = "выключено настройкой";
                built = true;
                return;
            }

            if (wanted == KvXrControllerVisualMode.Mesh)
            {
                bool ok = BuildSdkModels();
                if (!ok)
                {
                    // МЕШ НЕ ГОДИТСЯ — ЭТО НЕ ПРОВАЛ, А ОТКАТ. Владелец выбрал вариант A «с откатом
                    // на примитивы», и в журнале будет прямо сказано, почему откат случился.
                    Debug.LogWarning("[KvXr] ИКОНКИ КОНТРОЛЛЕРОВ: меш SDK не годится (" + modelDescription +
                                     ") — включаю примитивы, чтобы контроллеры были видны");
                    Clear();
                    mode = KvXrControllerVisualMode.Primitives;
                    BuildPrimitiveModels();
                    return;
                }
                return;
            }

            if (wanted == KvXrControllerVisualMode.Primitives) BuildPrimitiveModels();
            else BuildBillboards();

            built = true;
        }

        private bool BuildSdkModels()
        {
            GameObject prefabRight = LoadSdkPrefab(KvXrQuickHand.Right);
            GameObject prefabLeft = LoadSdkPrefab(KvXrQuickHand.Left);
            if (prefabRight == null || prefabLeft == null)
            {
                modelDescription = "префаб не загрузился из Resources (пути " + SdkModelRight + " / " + SdkModelLeft + ")";
                return false;
            }

            GameObject right = InstantiateModel(prefabRight, KvXrQuickHand.Right);
            GameObject left = InstantiateModel(prefabLeft, KvXrQuickHand.Left);
            if (right == null || left == null)
            {
                modelDescription = "префаб загрузился, но экземпляр не создан";
                return false;
            }

            // ГАБАРИТ ПРОВЕРЯЕТСЯ ЧИСЛОМ, А НЕ «НА ГЛАЗ»: если меш придёт в сантиметрах или в
            // футах, модель окажется размером с дом или с муравья. Масштаб мы НЕ выдумываем —
            // такой меш отбрасывается, и это прямо пишется в журнал.
            float size = BoundsSize(right);
            if (size < MinPlausibleSizeMeters || size > MaxPlausibleSizeMeters)
            {
                modelDescription = "габарит меша " + size.ToString("0.000") + " м не похож на контроллер";
                return false;
            }

            models[KvXrQuickHand.Right] = right;
            models[KvXrQuickHand.Left] = left;
            modelDescription = "меш SDK " + SdkModelRight + " / " + SdkModelLeft +
                               " (габарит " + size.ToString("0.000") + " м), материал наш, аниматоры выключены";
            built = true;
            return true;
        }

        private static GameObject LoadSdkPrefab(KvXrQuickHand hand)
        {
            try { return Resources.Load<GameObject>(SdkModelPath(hand)); }
            catch (System.Exception) { return null; }
        }

        /// <summary>
        /// ЭКЗЕМПЛЯР МОДЕЛИ: коллайдеры убираются, аниматоры выключаются, материалы — наши.
        /// Тени выключены: в шлеме они стоят кадров, а пользы в ангаре не дают.
        /// </summary>
        private GameObject InstantiateModel(GameObject prefab, KvXrQuickHand hand)
        {
            GameObject model;
            try { model = Instantiate(prefab, transform, false); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KvXr] ИКОНКИ КОНТРОЛЛЕРОВ: экземпляр не создан (" + e.GetType().Name + ")");
                return null;
            }

            model.name = "KvControllerModel_" + (hand == KvXrQuickHand.Left ? "Left" : "Right");
            model.SetActive(false);
            SetLayerRecursive(model, IgnoreRaycastLayer);

            int colliders = 0;
            Collider[] found = model.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null) continue;
                Destroy(found[i]);
                colliders++;
            }

            int animators = 0;
            Animator[] anims = model.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i] == null) continue;
                anims[i].enabled = false;
                animators++;
            }

            Material material = SidelessMaterial();
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                int slots = Mathf.Max(1, r.sharedMaterials.Length);
                var array = new Material[slots];
                for (int k = 0; k < slots; k++) array[k] = material;
                r.sharedMaterials = array;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            if (logOnChange && (colliders > 0 || animators > 0))
            {
                Debug.Log("[KvXr] ИКОНКИ КОНТРОЛЛЕРОВ: у модели " + model.name + " убрано коллайдеров " +
                          colliders + " (иначе перехватывали бы луч телепорта) · выключено аниматоров " + animators);
            }
            return model;
        }

        private void BuildPrimitiveModels()
        {
            models[KvXrQuickHand.Right] = BuildPrimitive(KvXrQuickHand.Right);
            models[KvXrQuickHand.Left] = BuildPrimitive(KvXrQuickHand.Left);
            modelDescription = "примитивы: рукоять 3.4 см × 11 см, торец-сфера 2.2 см, кольцо у торца, материал наш";
            built = true;
        }

        private GameObject BuildPrimitive(KvXrQuickHand hand)
        {
            var root = new GameObject("KvControllerPrimitive_" + (hand == KvXrQuickHand.Left ? "Left" : "Right"));
            root.transform.SetParent(transform, false);
            root.SetActive(false);

            Material material = hand == KvXrQuickHand.Left ? LeftTint() : RightTint();

            // Рукоять: цилиндр вдоль «вперёд» контроллера.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.034f, 0.055f, 0.034f);

            // Торец: сфера на конце (её видно как «куда смотрит контроллер»).
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Tip";
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, 0.095f);
            tip.transform.localScale = new Vector3(0.022f, 0.022f, 0.022f);

            // Кольцо: плоский диск под большим пальцем.
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0.02f);
            ring.transform.localScale = new Vector3(0.03f, 0.003f, 0.03f);

            PreparePrimitive(body, material);
            PreparePrimitive(tip, material);
            PreparePrimitive(ring, material);
            SetLayerRecursive(root, IgnoreRaycastLayer);
            return root;
        }

        private void PreparePrimitive(GameObject go, Material material)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void BuildBillboards()
        {
            models[KvXrQuickHand.Right] = BuildBillboard(KvXrQuickHand.Right);
            models[KvXrQuickHand.Left] = BuildBillboard(KvXrQuickHand.Left);
            modelDescription = "значки у кисти 8 см, развёрнуты к оператору, материал наш";
            built = true;
        }

        private GameObject BuildBillboard(KvXrQuickHand hand)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "KvControllerBadge_" + (hand == KvXrQuickHand.Left ? "Left" : "Right");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            go.SetActive(false);

            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = hand == KvXrQuickHand.Left ? LeftTint() : RightTint();
                if (material != null) renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            SetLayerRecursive(go, IgnoreRaycastLayer);
            return go;
        }

        // ==================================================== РАЗМЕЩЕНИЕ

        /// <summary>
        /// ПОСТАВИТЬ МОДЕЛЬ РУКИ ПО ПОЗЕ. Возвращает true, если модель видна.
        /// Нет контроллера, нет трекинга или поза нулевая — модель СКРЫВАЕТСЯ (и «свет в воздухе»
        /// не висит). Небольшая задержка скрытия гасит мигание на одном потерянном кадре.
        /// </summary>
        private bool Place(KvXrQuickHand hand)
        {
            GameObject model;
            if (!models.TryGetValue(hand, out model) || model == null) return false;

            Vector3 position;
            Quaternion rotation;
            bool tracked = KvXrInputSource.TryTrackedPose(hand, out position, out rotation);
            bool usable = tracked && IsPoseUsable(true, position);

            if (!usable)
            {
                float since;
                if (!lostSince.TryGetValue(hand, out since)) since = -1f;
                if (since < 0f) lostSince[hand] = Time.unscaledTime;

                if (Time.unscaledTime - lostSince[hand] > HideGraceSeconds)
                {
                    if (model.activeSelf) model.SetActive(false);
                    return false;
                }
                return model.activeSelf;
            }

            lostSince[hand] = -1f;

            if (!model.activeSelf) model.SetActive(true);

            if (mode == KvXrControllerVisualMode.Billboard && headCamera != null)
            {
                // Значок всегда развёрнут к оператору: иначе его видно «с ребра» и он исчезает.
                Vector3 toHead = headCamera.transform.position - position;
                if (toHead.sqrMagnitude > 0.0001f)
                    model.transform.rotation = Quaternion.LookRotation(toHead.normalized, Vector3.up);
                model.transform.position = position;
            }
            else
            {
                model.transform.SetPositionAndRotation(position, rotation);
            }
            return true;
        }

        /// <summary>СТРОКА-ДОКАЗАТЕЛЬСТВО ([S2.2]): сколько моделей видно и на каких местах.</summary>
        private void LogState(int count)
        {
            Vector3 rightPosition;
            Quaternion ignored;
            bool rightTracked = KvXrInputSource.TryTrackedPose(KvXrQuickHand.Right, out rightPosition, out ignored);

            Vector3 leftPosition;
            bool leftTracked = KvXrInputSource.TryTrackedPose(KvXrQuickHand.Left, out leftPosition, out ignored);

            string state = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[KvXr] ИКОНКИ КОНТРОЛЛЕРОВ: показано {0} · модель «{1}» · правая {2} · левая {3} · " +
                "материал наш (URP) · коллайдеров нет, слой Ignore Raycast",
                count, modelDescription,
                rightTracked ? rightPosition.ToString("0.00") : "НЕТ ПОЗЫ",
                leftTracked ? leftPosition.ToString("0.00") : "НЕТ ПОЗЫ");

            if (state == lastState) return;
            lastState = state;
            Debug.Log(state);
        }

        // ==================================================== СЛУЖЕБНОЕ

        private void Clear()
        {
            foreach (KeyValuePair<KvXrQuickHand, GameObject> pair in models)
            {
                if (pair.Value == null) continue;
                if (Application.isPlaying) Destroy(pair.Value);
                else DestroyImmediate(pair.Value);
            }
            models.Clear();
            lostSince.Clear();
            shownCount = -1;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        /// <summary>Наибольший габарит модели по ограничивающим объёмам всех рендереров, метры.</summary>
        private static float BoundsSize(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 0f;

            bool has = false;
            Bounds bounds = new Bounds();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (!has) { bounds = renderers[i].bounds; has = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
            if (!has) return 0f;
            return Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        }

        private Material SidelessMaterial()
        {
            if (sharedMaterial != null) return sharedMaterial;
            sharedMaterial = KvRenderPipelineShaders.CreateLit("Материал_контроллера_XR",
                new Color(0.82f, 0.84f, 0.88f, 1f));
            if (sharedMaterial == null)
                sharedMaterial = KvRenderPipelineShaders.CreateUnlit("Материал_контроллера_XR",
                    new Color(0.82f, 0.84f, 0.88f, 1f));
            return sharedMaterial;
        }

        private Material RightTint()
        {
            if (rightTint != null) return rightTint;
            rightTint = KvRenderPipelineShaders.CreateUnlit("Материал_правого_контроллера_XR",
                new Color(0.28f, 0.62f, 1.00f, 1f));
            if (rightTint != null) return rightTint;
            return SidelessMaterial();
        }

        private Material LeftTint()
        {
            if (leftTint != null) return leftTint;
            leftTint = KvRenderPipelineShaders.CreateUnlit("Материал_левого_контроллера_XR",
                new Color(0.35f, 0.92f, 0.55f, 1f));
            if (leftTint != null) return leftTint;
            return SidelessMaterial();
        }

        /// <summary>Строка состояния для журнала и отчёта.</summary>
        public string Describe()
        {
            return "визуал контроллеров: режим " + ModeName(mode) + " · видно " + ShownCount +
                   " · " + modelDescription;
        }
    }
}
