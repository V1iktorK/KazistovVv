using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Описание окружения (этап 28 ТЗ).</summary>
    public class KvEnvironmentPreset
    {
        public string id = "";
        public string title = "";
        public string note = "";
        public Color floor = Color.grey;
        public Color wall = Color.grey;
        public Color ceiling = Color.grey;
        public Color accent = Color.grey;       // цвет разметки/технических полос
        public float sizeX = 14f;
        public float sizeZ = 14f;
        public float height = 4.5f;
        public bool hasCeiling = true;
        public float smoothness = 0.25f;
        public float metallic;
        public int props;                        // сколько объектов обстановки добавить
    }

    /// <summary>
    /// ЭТАП 28 ТЗ: ПРЕСЕТЫ ОКРУЖЕНИЯ (АНГАР, ЛАБОРАТОРИЯ, ЦЕХ, ЧИСТОЕ ПОМЕЩЕНИЕ).
    ///
    /// Помещение строится из простых плоскостей (пол, четыре стены, потолок), к нему
    /// добавляются элементы обстановки: ящики, стеллаж, ограждение, разметка на полу.
    /// Всё это — ОФОРМЛЕНИЕ: коллайдеров нет, объекты служебные (`HideFlags.HideInHierarchy`),
    /// в расчёт столкновений (`CollisionWorld`) они не попадают, роботов в сцене по-прежнему ровно
    /// столько, сколько было. Пресет — это набор цветов, размеров и числа объектов, поэтому
    /// переключение мгновенное и не требует загрузки сцен.
    /// </summary>
    public class KvEnvironmentStudio
    {
        public event Action<string> Message;

        private TrajectoryFlowController flow;

        private readonly List<KvEnvironmentPreset> presets = new List<KvEnvironmentPreset>();
        private GameObject root;
        private int index;
        private bool visible;
        private Vector3 center;
        private string builtFor = "";
        private bool graphicsReported;

        public IList<KvEnvironmentPreset> Presets { get { return presets; } }
        public int Index { get { return index; } }
        public bool Visible { get { return visible; } }

        /// <summary>Сколько объектов построено в помещении (для проверок и свойств).</summary>
        public int ObjectCount { get { return root != null ? root.transform.childCount : 0; } }
        public string CurrentTitle
        {
            get { return presets.Count == 0 ? "—" : presets[Mathf.Clamp(index, 0, presets.Count - 1)].title; }
        }

        public KvEnvironmentStudio()
        {
            presets.Add(new KvEnvironmentPreset
            {
                id = "hangar", title = "Ангар",
                note = "большое помещение, бетонный пол, фермы под потолком",
                floor = new Color(0.20f, 0.20f, 0.21f), wall = new Color(0.26f, 0.27f, 0.29f),
                ceiling = new Color(0.16f, 0.16f, 0.17f), accent = new Color(0.85f, 0.65f, 0.10f),
                sizeX = 20f, sizeZ = 22f, height = 7f, props = 7, smoothness = 0.12f
            });
            presets.Add(new KvEnvironmentPreset
            {
                id = "lab", title = "Лаборатория",
                note = "светлые панели, ровный пол, минимум обстановки",
                floor = new Color(0.68f, 0.69f, 0.71f), wall = new Color(0.82f, 0.83f, 0.85f),
                ceiling = new Color(0.90f, 0.91f, 0.92f), accent = new Color(0.20f, 0.45f, 0.85f),
                sizeX = 12f, sizeZ = 12f, height = 3.4f, props = 3, smoothness = 0.55f
            });
            presets.Add(new KvEnvironmentPreset
            {
                id = "factory", title = "Цех",
                note = "промышленный пол, металлические стены, техника вокруг",
                floor = new Color(0.28f, 0.29f, 0.30f), wall = new Color(0.35f, 0.36f, 0.38f),
                ceiling = new Color(0.22f, 0.23f, 0.24f), accent = new Color(0.90f, 0.35f, 0.10f),
                sizeX = 16f, sizeZ = 18f, height = 5.5f, props = 9, smoothness = 0.30f, metallic = 0.35f
            });
            presets.Add(new KvEnvironmentPreset
            {
                id = "clean", title = "Чистое помещение",
                note = "белый глянец, панели с сеткой, ничего лишнего",
                floor = new Color(0.88f, 0.89f, 0.90f), wall = new Color(0.93f, 0.94f, 0.95f),
                ceiling = new Color(0.96f, 0.97f, 0.98f), accent = new Color(0.30f, 0.70f, 0.65f),
                sizeX = 10f, sizeZ = 10f, height = 3.0f, props = 2, smoothness = 0.80f
            });
        }

        public void Bind(TrajectoryFlowController controller)
        {
            flow = controller;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (!value && root != null) root.SetActive(false);
            if (value) Rebuild();
            Report("окружение " + (value ? "показано" : "скрыто") + ": " + CurrentTitle);
        }

        public void SetPreset(int value)
        {
            index = Mathf.Clamp(value, 0, presets.Count - 1);
            builtFor = "";
            if (visible) Rebuild();
            Report("пресет окружения: " + CurrentTitle + " — " + presets[index].note);
        }

        public void Next()
        {
            SetPreset((index + 1) % presets.Count);
        }

        /// <summary>Пересобрать помещение (вызывается при смене пресета/робота).</summary>
        public void Rebuild()
        {
            if (presets.Count == 0) return;
            KvEnvironmentPreset preset = presets[Mathf.Clamp(index, 0, presets.Count - 1)];

            // Помещение — это показ: без графики (пакетный режим) объекты не создаются,
            // чтобы не сорвать цикл кадров; выбор пресета и состояние сохраняются.
            if (!KvGraphics.Available)
            {
                if (!graphicsReported)
                {
                    graphicsReported = true;
                    Report("окружение не построено: нет графики (пресет «" + preset.title +
                           "» выбран и будет построен при наличии видеокарты)");
                }
                center = ResolveCenter();
                return;
            }

            center = ResolveCenter();
            string signature = preset.id + "|" + center.x.ToString("0.0") + "|" + center.z.ToString("0.0");
            if (root != null && builtFor == signature) { root.SetActive(true); return; }
            if (root != null) { UnityEngine.Object.Destroy(root); root = null; }
            builtFor = signature;

            root = new GameObject("Окружение_" + preset.id);
            root.hideFlags = HideFlags.HideInHierarchy;
            root.transform.position = center;

            Material floorMat = MakeMaterial("Пол", preset.floor, preset.smoothness, preset.metallic);
            Material wallMat = MakeMaterial("Стены", preset.wall, preset.smoothness * 0.6f, preset.metallic);
            Material ceilMat = MakeMaterial("Потолок", preset.ceiling, preset.smoothness * 0.4f, 0f);
            Material accentMat = MakeMaterial("Разметка", preset.accent, 0.35f, 0f);

            // Плоскость-квад смотрит по своей оси +Z, поэтому пол разворачиваем «вверх»,
            // а потолок — «вниз»: иначе поверхность видна только с изнанки.
            Plane(root.transform, "Пол", new Vector3(0f, 0f, 0f),
                new Vector3(preset.sizeX, 1f, preset.sizeZ), Quaternion.Euler(-90f, 0f, 0f), floorMat);
            if (preset.hasCeiling)
                Plane(root.transform, "Потолок", new Vector3(0f, preset.height, 0f),
                    new Vector3(preset.sizeX, 1f, preset.sizeZ), Quaternion.Euler(90f, 0f, 0f), ceilMat);

            Wall(root.transform, "Стена_С", new Vector3(0f, preset.height * 0.5f, preset.sizeZ * 0.5f),
                new Vector3(preset.sizeX, preset.height, 1f), 180f, wallMat);
            Wall(root.transform, "Стена_Ю", new Vector3(0f, preset.height * 0.5f, -preset.sizeZ * 0.5f),
                new Vector3(preset.sizeX, preset.height, 1f), 0f, wallMat);
            Wall(root.transform, "Стена_В", new Vector3(preset.sizeX * 0.5f, preset.height * 0.5f, 0f),
                new Vector3(1f, preset.height, preset.sizeZ), -90f, wallMat);
            Wall(root.transform, "Стена_З", new Vector3(-preset.sizeX * 0.5f, preset.height * 0.5f, 0f),
                new Vector3(1f, preset.height, preset.sizeZ), 90f, wallMat);

            // Разметка рабочей зоны (жёлтая/акцентная рамка вокруг робота) — видно границы.
            Marking(root.transform, preset, accentMat);

            // Обстановка: ящики по углам детерминированно (без случайности — вид не «дрожит»).
            System.Random random = new System.Random(preset.id.GetHashCode());
            for (int i = 0; i < preset.props; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                float radius = Mathf.Lerp(preset.sizeX * 0.22f, preset.sizeX * 0.42f, (float)random.NextDouble());
                float sx = Mathf.Lerp(0.3f, 0.9f, (float)random.NextDouble());
                float sy = Mathf.Lerp(0.3f, 1.1f, (float)random.NextDouble());
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, sy * 0.5f, Mathf.Sin(angle) * radius);
                if (position.magnitude < 1.2f) position *= 2f;         // не ставим ящик внутрь робота
                Box(root.transform, "Обстановка_" + i, position,
                    new Vector3(sx, sy, sx * Mathf.Lerp(0.7f, 1.3f, (float)random.NextDouble())),
                    (float)(random.NextDouble() * 360.0), i % 3 == 0 ? accentMat : wallMat);
            }

            root.SetActive(visible);
            Report("окружение собрано: " + CurrentTitle + " · " + root.transform.childCount + " объектов");
        }

        /// <summary>Центр помещения — основание робота (помещение строится вокруг него).</summary>
        private Vector3 ResolveCenter()
        {
            if (flow != null && flow.Robot != null)
                return new Vector3(flow.Robot.transform.position.x, 0f, flow.Robot.transform.position.z);
            return Vector3.zero;
        }

        private static Material MakeMaterial(string name, Color color, float smoothness, float metallic)
        {
            // Материал окружения — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP).
            Material material = KvRenderPipelineShaders.CreateLit("Окружение_" + name, color);
            if (material == null) return null;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            if (material.HasProperty("_DoubleSidedEnable")) material.SetFloat("_DoubleSidedEnable", 1f);
            return material;
        }

        private static GameObject Plane(Transform parent, string name, Vector3 position, Vector3 scale,
            Quaternion rotation, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            go.name = name;
            go.hideFlags = HideFlags.HideInHierarchy;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = true;
            }
            return go;
        }

        private static void Wall(Transform parent, string name, Vector3 position, Vector3 scale,
            float yaw, Material material)
        {
            GameObject go = Plane(parent, name, position, new Vector3(scale.x, scale.y, 1f),
                Quaternion.Euler(0f, yaw, 0f), material);
            go.transform.localPosition = position;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale,
            float yaw, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            go.name = name;
            go.hideFlags = HideFlags.HideInHierarchy;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = material;
            return go;
        }

        private static void Marking(Transform parent, KvEnvironmentPreset preset, Material material)
        {
            float half = 1.6f;
            Plane(parent, "Разметка_1", new Vector3(0f, 0.005f, half),
                new Vector3(half * 2f, 0.12f, 1f), Quaternion.Euler(-90f, 0f, 0f), material);
            Plane(parent, "Разметка_2", new Vector3(0f, 0.005f, -half),
                new Vector3(half * 2f, 0.12f, 1f), Quaternion.Euler(-90f, 0f, 0f), material);
            Plane(parent, "Разметка_3", new Vector3(half, 0.005f, 0f),
                new Vector3(0.12f, half * 2f, 1f), Quaternion.Euler(-90f, 0f, 0f), material);
            Plane(parent, "Разметка_4", new Vector3(-half, 0.005f, 0f),
                new Vector3(0.12f, half * 2f, 1f), Quaternion.Euler(-90f, 0f, 0f), material);
        }

        public string Status()
        {
            if (presets.Count == 0) return "пресетов нет";
            KvEnvironmentPreset preset = presets[Mathf.Clamp(index, 0, presets.Count - 1)];
            return (visible ? "показано" : "скрыто") + " · " + preset.title + " · " +
                   preset.sizeX.ToString("0") + "×" + preset.sizeZ.ToString("0") + " м, высота " +
                   preset.height.ToString("0.0") + " м · объектов " +
                   (root != null ? root.transform.childCount : 0);
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Env] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>Описание светового пресета (этап 29 ТЗ).</summary>
    public class KvLightingPreset
    {
        public string id = "";
        public string title = "";
        public string note = "";
        public Color sunColor = Color.white;
        public float sunLux = 20000f;
        public float sunElevation = 45f;
        public float sunAzimuth = 40f;
        public float keyLux = 20000f;
        public float fillLux = 8000f;
        public float rimLux = 6000f;
        public float exposureEv = 0f;
        public float bloom;
        public bool fog;
        public float fogDistance = 400f;
        public Color fogColor = Color.grey;
        public Color skyTop = Color.blue;
        public Color skyMiddle = new Color(0.3f, 0.7f, 1f);
        public Color skyBottom = Color.white;
    }

    /// <summary>
    /// ЭТАП 29 ТЗ: ПРЕСЕТЫ ОСВЕЩЕНИЯ (ДЕНЬ, НОЧЬ, СТУДИЯ, ДРАМАТИЧНЫЙ СВЕТ).
    ///
    /// Пресет меняет РЕАЛЬНЫЕ параметры сцены HDRP:
    ///   • солнце (направленный источник) — цвет, яркость в люксах, положение (высота/азимут);
    ///   • студийный свет проекта (`HDRPAutoLighting`: ключевой/заливающий/контровой) — яркости;
    ///   • глобальный Volume — экспозиция, bloom, туман (`Fog`) и градиентное небо (`GradientSky`);
    ///   • экспозиция солнца/камеры для компенсации «пересвета» или «темноты».
    /// Переход к пресету плавный (0.6 с), поэтому кадр не «прыгает», а глаз не устаёт.
    /// Если солнца в сцене нет, оно создаётся служебным объектом и помечается, чтобы не путаться
    /// с ручными источниками.
    /// </summary>
    public class KvLightingStudio
    {
        public const string SunName = "KvPresetSun";

        public event Action<string> Message;

        private readonly List<KvLightingPreset> presets = new List<KvLightingPreset>();
        private int index;
        private bool applied;
        private bool transitioning;
        private float transitionTime;
        private float transitionDuration = 0.6f;

        private LightingSnapshot from = new LightingSnapshot();
        private LightingSnapshot to = new LightingSnapshot();
        private Light sun;
        private HDRPAutoLighting studio;

        private class LightingSnapshot
        {
            public Color sunColor = Color.white;
            public float sunLux;
            public float elevation, azimuth;
            public float key, fill, rim;
            public float exposure;
            public float bloom;
            public bool fog;
            public float fogDistance = 400f;
            public Color fogColor = Color.grey;
            public Color skyTop = Color.blue, skyMiddle = Color.cyan, skyBottom = Color.white;
        }

        public IList<KvLightingPreset> Presets { get { return presets; } }
        public int Index { get { return index; } }
        public bool Applied { get { return applied; } }

        /// <summary>Направленный источник, которым управляет пресет (для свойств и проверок).</summary>
        public Light Sun
        {
            get
            {
                if (sun == null) sun = FindSun();
                return sun;
            }
        }
        public string CurrentTitle
        {
            get { return presets.Count == 0 ? "—" : presets[Mathf.Clamp(index, 0, presets.Count - 1)].title; }
        }

        public KvLightingStudio()
        {
            presets.Add(new KvLightingPreset
            {
                id = "day", title = "День",
                note = "яркое солнце 5600 K, светлое небо, лёгкая дымка",
                sunColor = new Color(1f, 0.97f, 0.92f), sunLux = 26000f, sunElevation = 52f, sunAzimuth = 35f,
                keyLux = 26000f, fillLux = 9000f, rimLux = 5000f,
                fog = true, fogDistance = 900f, fogColor = new Color(0.75f, 0.82f, 0.92f),
                skyTop = new Color(0.25f, 0.45f, 0.85f), skyMiddle = new Color(0.55f, 0.72f, 0.95f),
                skyBottom = new Color(0.85f, 0.88f, 0.92f)
            });
            presets.Add(new KvLightingPreset
            {
                id = "night", title = "Ночь",
                note = "лунный свет 4100 K, тёмное небо, плотная дымка",
                sunColor = new Color(0.62f, 0.72f, 1f), sunLux = 900f, sunElevation = 28f, sunAzimuth = 210f,
                keyLux = 1200f, fillLux = 500f, rimLux = 800f, exposureEv = 0.6f,
                fog = true, fogDistance = 220f, fogColor = new Color(0.10f, 0.13f, 0.22f),
                skyTop = new Color(0.02f, 0.03f, 0.09f), skyMiddle = new Color(0.05f, 0.07f, 0.16f),
                skyBottom = new Color(0.10f, 0.12f, 0.20f)
            });
            presets.Add(new KvLightingPreset
            {
                id = "studio", title = "Студия",
                note = "три мягких источника 5600 K, нейтральный серый фон без тумана",
                sunColor = new Color(1f, 0.99f, 0.97f), sunLux = 12000f, sunElevation = 62f, sunAzimuth = 0f,
                keyLux = 18000f, fillLux = 12000f, rimLux = 9000f,
                fog = false, fogDistance = 1200f,
                skyTop = new Color(0.55f, 0.57f, 0.60f), skyMiddle = new Color(0.62f, 0.64f, 0.67f),
                skyBottom = new Color(0.70f, 0.71f, 0.73f)
            });
            presets.Add(new KvLightingPreset
            {
                id = "dramatic", title = "Драматичный",
                note = "низкий контровой свет, тёмный фон, высокий контраст",
                sunColor = new Color(1f, 0.82f, 0.62f), sunLux = 6000f, sunElevation = 12f, sunAzimuth = 300f,
                keyLux = 3000f, fillLux = 700f, rimLux = 14000f, exposureEv = -0.4f, bloom = 0.35f,
                fog = true, fogDistance = 120f, fogColor = new Color(0.05f, 0.05f, 0.07f),
                skyTop = new Color(0.01f, 0.01f, 0.02f), skyMiddle = new Color(0.04f, 0.03f, 0.05f),
                skyBottom = new Color(0.10f, 0.07f, 0.06f)
            });
        }

        public void Apply()
        {
            Apply(0f, 0f);
        }

        public void SetPreset(int value, bool smooth = true)
        {
            index = Mathf.Clamp(value, 0, presets.Count - 1);
            Apply(smooth ? transitionDuration : 0f, smooth ? 0.05f : 0f);
            Report("пресет освещения: " + CurrentTitle + " — " + presets[index].note);
        }

        public void Next()
        {
            SetPreset((index + 1) % presets.Count);
        }

        /// <summary>Применить пресет: `duration` — время плавного перехода в секундах.</summary>
        public void Apply(float duration, float delay)
        {
            if (presets.Count == 0) return;
            KvLightingPreset preset = presets[Mathf.Clamp(index, 0, presets.Count - 1)];

            sun = FindSun();
            studio = UnityEngine.Object.FindAnyObjectByType<HDRPAutoLighting>();

            from = Capture();
            to = new LightingSnapshot
            {
                sunColor = preset.sunColor,
                sunLux = preset.sunLux,
                elevation = preset.sunElevation,
                azimuth = preset.sunAzimuth,
                key = preset.keyLux,
                fill = preset.fillLux,
                rim = preset.rimLux,
                exposure = preset.exposureEv,
                bloom = preset.bloom,
                fog = preset.fog,
                fogDistance = preset.fogDistance,
                fogColor = preset.fogColor,
                skyTop = preset.skyTop,
                skyMiddle = preset.skyMiddle,
                skyBottom = preset.skyBottom
            };

            transitionTime = -delay;
            transitionDuration = Mathf.Max(0.01f, duration);
            transitioning = duration > 0.01f;
            if (!transitioning) Push(to);
            applied = true;
        }

        /// <summary>Кадровое обслуживание: плавный переход между пресетами.</summary>
        public void Tick(float deltaTime)
        {
            if (!transitioning) return;
            transitionTime += deltaTime;
            if (transitionTime < 0f) return;
            float t = Mathf.Clamp01(transitionTime / transitionDuration);
            float eased = t * t * (3f - 2f * t);       // сглаживание «туда-обратно»
            Push(Lerp(from, to, eased));
            if (t >= 1f) transitioning = false;
        }

        private LightingSnapshot Capture()
        {
            LightingSnapshot s = new LightingSnapshot();
            if (sun != null)
            {
                s.sunColor = sun.color;
                s.sunLux = sun.intensity;
                s.elevation = sun.transform.eulerAngles.x;
                s.azimuth = sun.transform.eulerAngles.y;
            }
            if (studio != null)
            {
                s.key = studio.mainLightIntensity;
                s.fill = studio.fillLightIntensity;
                s.rim = studio.rimLightIntensity;
                s.exposure = studio.cameraExposureCompensation;
                s.bloom = studio.enableBloom ? 0.2f : 0f;
            }
            Fog fog;
            if (TryGetVolumeOverride<Fog>(out fog))
            {
                s.fog = fog.enabled.value;
                s.fogDistance = fog.meanFreePath.value;
                s.fogColor = fog.color.value;
            }
            GradientSky sky;
            if (TryGetVolumeOverride<GradientSky>(out sky))
            {
                s.skyTop = sky.top.value;
                s.skyMiddle = sky.middle.value;
                s.skyBottom = sky.bottom.value;
            }
            return s;
        }

        private static LightingSnapshot Lerp(LightingSnapshot a, LightingSnapshot b, float t)
        {
            return new LightingSnapshot
            {
                sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
                sunLux = Mathf.Lerp(a.sunLux, b.sunLux, t),
                elevation = Mathf.LerpAngle(a.elevation, b.elevation, t),
                azimuth = Mathf.LerpAngle(a.azimuth, b.azimuth, t),
                key = Mathf.Lerp(a.key, b.key, t),
                fill = Mathf.Lerp(a.fill, b.fill, t),
                rim = Mathf.Lerp(a.rim, b.rim, t),
                exposure = Mathf.Lerp(a.exposure, b.exposure, t),
                bloom = Mathf.Lerp(a.bloom, b.bloom, t),
                fog = t > 0.5f ? b.fog : a.fog,
                fogDistance = Mathf.Lerp(a.fogDistance, b.fogDistance, t),
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                skyTop = Color.Lerp(a.skyTop, b.skyTop, t),
                skyMiddle = Color.Lerp(a.skyMiddle, b.skyMiddle, t),
                skyBottom = Color.Lerp(a.skyBottom, b.skyBottom, t)
            };
        }

        private void Push(LightingSnapshot s)
        {
            if (sun == null) sun = FindSun();
            if (sun != null)
            {
                sun.enabled = true;
                sun.color = s.sunColor;
                sun.intensity = Mathf.Clamp(s.sunLux, 0f, 40000f);
                sun.transform.rotation = Quaternion.Euler(s.elevation, s.azimuth, 0f);
            }

            if (studio == null) studio = UnityEngine.Object.FindAnyObjectByType<HDRPAutoLighting>();
            if (studio != null)
            {
                studio.enableKeyLight = s.key > 0f;
                studio.mainLightIntensity = Mathf.Clamp(s.key, 0f, 40000f);
                studio.fillLightIntensity = Mathf.Clamp(s.fill, 0f, 40000f);
                studio.rimLightIntensity = Mathf.Clamp(s.rim, 0f, 40000f);
                studio.keyLightColor = s.sunColor;
                studio.cameraExposureCompensation = s.exposure;
                studio.enableBloom = s.bloom > 0.01f;
                studio.Apply();
            }

            Fog fog;
            if (TryGetVolumeOverride<Fog>(out fog))
            {
                fog.enabled.value = s.fog;
                fog.colorMode.value = FogColorMode.ConstantColor;
                fog.color.value = s.fogColor;
                fog.tint.value = s.fogColor;
                fog.meanFreePath.value = Mathf.Max(10f, s.fogDistance);
                fog.baseHeight.value = 0f;
                fog.maximumHeight.value = 200f;
            }
            else if (s.fog)
            {
                // Единый туман Unity — запасной путь, если в профиле HDRP нет раздела Fog.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = s.fogColor;
                RenderSettings.fogDensity = Mathf.Clamp(12f / Mathf.Max(20f, s.fogDistance), 0.0002f, 0.06f);
            }
            else
            {
                RenderSettings.fog = false;
            }

            VisualEnvironment environment;
            GradientSky sky;
            if (TryGetVolumeOverride<GradientSky>(out sky))
            {
                if (TryGetVolumeOverride<VisualEnvironment>(out environment))
                    environment.skyType.value = (int)SkyType.Gradient;
                sky.top.value = s.skyTop;
                sky.middle.value = s.skyMiddle;
                sky.bottom.value = s.skyBottom;
            }

            Exposure exposure;
            if (TryGetVolumeOverride<Exposure>(out exposure))
            {
                exposure.compensation.value = s.exposure;
                if (!TryGetVolumeOverride<Fog>(out fog)) exposure.fixedExposure.value = 11f + s.exposure;
            }

            Bloom bloom;
            if (TryGetVolumeOverride<Bloom>(out bloom))
            {
                bloom.intensity.value = Mathf.Clamp(s.bloom, 0f, 1f);
                bloom.active = s.bloom > 0.01f;
            }
        }

        private static bool TryGetVolumeOverride<T>(out T value) where T : VolumeComponent
        {
            value = null;
            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include);
            for (int i = 0; i < volumes.Length; i++)
            {
                if (!volumes[i].isGlobal || volumes[i].profile == null) continue;
                T component;
                if (volumes[i].profile.TryGet<T>(out component) && component != null)
                {
                    value = component;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Направленный источник-солнце: сначала запечённый по имени, затем служебный.</summary>
        private static Light FindSun()
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional && lights[i].gameObject.name == "Directional Light")
                    return lights[i];
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional && lights[i].gameObject.name == SunName)
                    return lights[i];
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional) return lights[i];

            GameObject go = new GameObject(SunName, typeof(Light));
            go.hideFlags = HideFlags.HideInHierarchy;
            Light created = go.GetComponent<Light>();
            created.type = LightType.Directional;
            created.intensity = 20000f;
            created.transform.rotation = Quaternion.Euler(50f, 35f, 0f);
            return created;
        }

        public string Status()
        {
            if (presets.Count == 0) return "пресетов нет";
            KvLightingPreset preset = presets[Mathf.Clamp(index, 0, presets.Count - 1)];
            string sunInfo = sun == null ? "солнце не найдено"
                : "солнце " + sun.intensity.ToString("0") + " лк, " +
                  sun.transform.eulerAngles.x.ToString("0") + "° над горизонтом";
            return preset.title + " · " + sunInfo + " · туман " +
                   (preset.fog ? preset.fogDistance.ToString("0") + " м" : "выключен") +
                   (transitioning ? " · переход…" : "");
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Light] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// Сохранённая правка материала (этап 30 ТЗ). Атрибут <see cref="Serializable"/> нужен
    /// для записи в JSON штатным <see cref="JsonUtility"/> (ФИКС 11.Б): JsonUtility пишет
    /// только помеченные типы, без атрибута файл получался бы пустым.
    /// </summary>
    [Serializable]
    public class KvMaterialEdit
    {
        public string target = "";
        public Color baseColor = Color.white;
        public float metallic;
        public float smoothness = 0.5f;
        public Color emission = Color.black;
        public float emissionIntensity;
        public bool hasBase, hasMetallic, hasSmoothness, hasEmission;
    }

    /// <summary>
    /// ФАЙЛ ПРАВОК МАТЕРИАЛОВ (ФИКС 11.Б): `materials_edits.json` в каталоге
    /// `FeatureStorage.ConfigDir`. Хранит тот же список правок, что и редактор материалов,
    /// чтобы они не терялись между запусками. В АССЕТЫ материалов правки по-прежнему
    /// не пишутся — только в этот JSON рядом с остальными данными функций.
    /// </summary>
    [Serializable]
    public class KvMaterialEditsFile
    {
        /// <summary>Когда файл записан (ISO-время) — для человека, читающего JSON.</summary>
        public string savedAt = "";
        /// <summary>Правки: «имя объекта # слот» + цвет, металличность, гладкость, свечение.</summary>
        public List<KvMaterialEdit> edits = new List<KvMaterialEdit>();
    }

    /// <summary>
    /// ЭТАП 30 ТЗ: РЕДАКТОР МАТЕРИАЛОВ В РЕЖИМЕ РЕАЛЬНОГО ВРЕМЕНИ.
    ///
    /// Выбирается объект (пипеткой из центра экрана или перебором списка видимых частей),
    /// показываются его материалы и правятся их свойства — цвет, металличность, гладкость,
    /// свечение. Правка делается на КОПИИ материала (`Renderer.materials`, а не `sharedMaterials`),
    /// поэтому исходные файлы проекта не портятся, а на «Сброс» возвращается исходный вид.
    ///
    /// СОХРАНЕНИЕ ПРАВОК (ФИКС 11.Б). Список правок («имя объекта # слот» + цвет, металличность,
    /// гладкость, свечение) пишется в `materials_edits.json` в каталоге `FeatureStorage.ConfigDir`:
    ///   • по кнопке «Сохранить правки»;
    ///   • автоматически при выходе из PlayMode (хаб вызывает <see cref="SaveEdits"/> из
    ///     `OnApplicationQuit`, и только если правки действительно менялись);
    ///   • при следующем запуске правки возвращаются на место (`LoadEdits`, вызывается хабом
    ///     после сбора списка объектов).
    /// Кнопка «Забыть сохранённые правки» удаляет файл и возвращает материалы к исходному виду —
    /// без неё отказаться от сохранённых правок было бы нельзя.
    /// В АССЕТЫ материалов правки НЕ пишутся (прежнее осознанное ограничение): только JSON рядом
    /// с остальными данными функций.
    /// </summary>
    public class KvMaterialStudio
    {
        /// <summary>Имя файла правок в каталоге <see cref="FeatureStorage.ConfigDir"/>.</summary>
        public const string EditsFileName = "materials_edits.json";

        public event Action<string> Message;

        private readonly List<Renderer> targets = new List<Renderer>();
        private readonly List<KvMaterialEdit> saved = new List<KvMaterialEdit>();
        private readonly Dictionary<string, KvMaterialEdit> originals =
            new Dictionary<string, KvMaterialEdit>();

        private int index = -1;
        private int materialIndex;
        private string lastError = "";
        private bool restoring;         // правки применяет сброс/восстановление, а не оператор
        private bool dirty;             // были ли правки после последнего сохранения в файл

        public IList<Renderer> Targets { get { return targets; } }
        public int Index { get { return index; } }
        public int MaterialIndex { get { return materialIndex; } }
        public string LastError { get { return lastError; } }
        public IList<KvMaterialEdit> Saved { get { return saved; } }

        /// <summary>Полный путь файла правок (для подписи в интерфейсе и проверок).</summary>
        public string EditsPath { get { return Path.Combine(FeatureStorage.ConfigDir, EditsFileName); } }

        /// <summary>Сколько правок записано в списке (сколько уйдёт в файл при сохранении).</summary>
        public int SavedCount { get { return saved.Count; } }

        /// <summary>Есть ли на диске сохранённый файл правок.</summary>
        public bool HasSavedFile
        {
            get
            {
                try { return File.Exists(EditsPath); }
                catch (Exception) { return false; }
            }
        }

        /// <summary>Есть ли несохранённые изменения (по ним и работает автосохранение при выходе).</summary>
        public bool Dirty { get { return dirty; } }

        public Renderer Current
        {
            get { return index >= 0 && index < targets.Count ? targets[index] : null; }
        }

        public string CurrentLabel
        {
            get
            {
                Renderer r = Current;
                if (r == null) return "не выбран";
                return r.gameObject.name + " · материалов: " + r.materials.Length +
                       " · слот " + (materialIndex + 1);
            }
        }

        /// <summary>Обновить список доступных объектов (части роботов и стенда).</summary>
        public void Refresh(Transform root)
        {
            targets.Clear();
            if (root != null)
            {
                Renderer[] found = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < found.Length; i++)
                    if (found[i] != null && found[i].sharedMaterial != null) targets.Add(found[i]);
            }
            if (targets.Count == 0)
            {
                Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
                for (int i = 0; i < all.Length && targets.Count < 60; i++)
                    if (all[i] != null && all[i].sharedMaterial != null) targets.Add(all[i]);
            }
            index = targets.Count > 0 ? Mathf.Clamp(index, 0, targets.Count - 1) : -1;
            materialIndex = 0;
            lastError = targets.Count == 0 ? "объекты с материалами не найдены" : "";
            Report("объектов для правки: " + targets.Count);
        }

        public void Select(int value)
        {
            if (targets.Count == 0) return;
            index = ((value % targets.Count) + targets.Count) % targets.Count;
            materialIndex = 0;
            Report("выбран объект: " + CurrentLabel);
        }

        public void Next(int delta) { Select(index + delta); }

        public void SelectMaterial(int value)
        {
            Renderer r = Current;
            if (r == null) return;
            materialIndex = Mathf.Clamp(value, 0, Mathf.Max(0, r.materials.Length - 1));
        }

        /// <summary>Выбрать объект лучом из центра экрана (пипетка).</summary>
        public bool PickFromCenter(Camera camera)
        {
            if (camera == null) { lastError = "камера не найдена"; return false; }
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 60f))
            {
                lastError = "в центре экрана нет объекта с коллайдером — выберите из списка";
                Report(lastError);
                return false;
            }
            Renderer renderer = hit.collider.GetComponentInParent<Renderer>();
            if (renderer == null) { lastError = "у объекта нет материала"; return false; }
            int found = targets.IndexOf(renderer);
            if (found < 0) { targets.Add(renderer); found = targets.Count - 1; }
            index = found;
            materialIndex = 0;
            Report("выбрано пипеткой: " + CurrentLabel);
            return true;
        }

        private Material MaterialOf(out string key)
        {
            key = "";
            Renderer r = Current;
            if (r == null) { lastError = "объект не выбран"; return null; }
            Material[] materials = r.materials;
            if (materials == null || materials.Length == 0) { lastError = "у объекта нет материалов"; return null; }
            materialIndex = Mathf.Clamp(materialIndex, 0, materials.Length - 1);
            key = r.gameObject.name + "#" + materialIndex;
            Material material = materials[materialIndex];
            if (!originals.ContainsKey(key)) originals[key] = Read(material, key);
            return material;
        }

        private static KvMaterialEdit Read(Material material, string key)
        {
            KvMaterialEdit edit = new KvMaterialEdit { target = key };
            if (material == null) return edit;
            if (material.HasProperty("_BaseColor"))
            {
                edit.baseColor = material.GetColor("_BaseColor");
                edit.hasBase = true;
            }
            else if (material.HasProperty("_Color"))
            {
                edit.baseColor = material.GetColor("_Color");
                edit.hasBase = true;
            }
            if (material.HasProperty("_Metallic"))
            {
                edit.metallic = material.GetFloat("_Metallic");
                edit.hasMetallic = true;
            }
            if (material.HasProperty("_Smoothness"))
            {
                edit.smoothness = material.GetFloat("_Smoothness");
                edit.hasSmoothness = true;
            }
            if (material.HasProperty("_EmissiveColor"))
            {
                edit.emission = material.GetColor("_EmissiveColor");
                edit.hasEmission = true;
            }
            if (material.HasProperty("_EmissiveIntensity"))
                edit.emissionIntensity = material.GetFloat("_EmissiveIntensity");
            return edit;
        }

        public KvMaterialEdit CurrentValues()
        {
            string key;
            Material material = MaterialOf(out key);
            return Read(material, key);
        }

        public bool SetBaseColor(Color color)
        {
            string key;
            Material material = MaterialOf(out key);
            if (material == null) return false;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            Remember(key, material);
            return true;
        }

        public bool SetMetallic(float value)
        {
            string key;
            Material material = MaterialOf(out key);
            if (material == null || !material.HasProperty("_Metallic")) return false;
            material.SetFloat("_Metallic", Mathf.Clamp01(value));
            Remember(key, material);
            return true;
        }

        public bool SetSmoothness(float value)
        {
            string key;
            Material material = MaterialOf(out key);
            if (material == null || !material.HasProperty("_Smoothness")) return false;
            material.SetFloat("_Smoothness", Mathf.Clamp01(value));
            Remember(key, material);
            return true;
        }

        public bool SetEmission(Color color, float intensity)
        {
            string key;
            Material material = MaterialOf(out key);
            if (material == null) return false;
            if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", color * intensity);
            if (material.HasProperty("_EmissiveIntensity")) material.SetFloat("_EmissiveIntensity", intensity);
            if (material.HasProperty("_EmissiveColorLDR")) material.SetColor("_EmissiveColorLDR", color);
            material.EnableKeyword("_EMISSIVE_COLOR_MAP");
            Remember(key, material);
            return true;
        }

        // ------------------------------------------------------------------ сохранение правок (ФИКС 11.Б)

        /// <summary>
        /// Запомнить правку материала в списке (список и уходит в `materials_edits.json`).
        /// Вызывается из сеттеров, поэтому «снимок» правки всегда соответствует тому, что
        /// реально стоит на материале. Сброс и восстановление в список не пишут.
        /// </summary>
        private void Remember(string key, Material material)
        {
            if (restoring || string.IsNullOrEmpty(key) || material == null) return;

            KvMaterialEdit edit = Read(material, key);
            for (int i = 0; i < saved.Count; i++)
            {
                if (saved[i].target != key) continue;
                saved[i] = edit;
                dirty = true;
                return;
            }
            saved.Add(edit);
            dirty = true;
        }

        /// <summary>
        /// Сохранить правки в `materials_edits.json` (каталог `FeatureStorage.ConfigDir`).
        /// Вызывается кнопкой «Сохранить правки» и автоматически при выходе из PlayMode.
        /// </summary>
        public bool SaveEdits(bool announce = true)
        {
            KvMaterialEditsFile file = new KvMaterialEditsFile
            {
                savedAt = FeatureStorage.IsoNow(),
                edits = new List<KvMaterialEdit>(saved)
            };
            bool ok = FeatureStorage.SaveJson(EditsPath, file);
            if (ok) dirty = false;
            if (announce)
            {
                Report(ok
                    ? "правки материалов сохранены: " + saved.Count + " → " + EditsPath
                    : "правки материалов не сохранены (см. предупреждение в консоли)");
            }
            return ok;
        }

        /// <summary>
        /// Вернуть сохранённые правки на место (вызывается хабом при инициализации сервиса,
        /// то есть при следующем запуске). Для каждой правки ищется объект с тем же именем и
        /// слотом материала; возвращается число применённых правок.
        /// </summary>
        public int LoadEdits(bool announce = true)
        {
            KvMaterialEditsFile file = FeatureStorage.LoadJson<KvMaterialEditsFile>(EditsPath);
            if (file == null || file.edits == null || file.edits.Count == 0)
            {
                if (announce) Report("сохранённых правок материалов нет — редактор начинает с исходного вида");
                return 0;
            }

            saved.Clear();
            for (int i = 0; i < file.edits.Count; i++)
                if (file.edits[i] != null && !string.IsNullOrEmpty(file.edits[i].target))
                    saved.Add(file.edits[i]);

            int applied = ApplySaved();
            dirty = false;
            if (announce)
            {
                Report("правки материалов восстановлены: " + applied + " из " + saved.Count +
                       (applied < saved.Count ? " (часть объектов в сцене не найдена)" : "") +
                       " · файл " + EditsPath);
            }
            return applied;
        }

        /// <summary>
        /// Применить список правок к объектам сцены. Сопоставление — по ключу «имя объекта # слот»,
        /// то есть по тому же ключу, которым правки записаны; возвращается число применённых правок.
        /// </summary>
        private int ApplySaved()
        {
            int applied = 0;
            restoring = true;
            for (int e = 0; e < saved.Count; e++)
            {
                KvMaterialEdit edit = saved[e];
                if (edit == null || string.IsNullOrEmpty(edit.target)) continue;

                int before = applied;
                for (int t = 0; t < targets.Count; t++)
                    if (TryApply(targets[t], edit)) applied++;

                // Объект мог не попасть в список редактора (например, правился пипеткой по столу
                // или стенду) — ищем его по имени среди всех отрисовываемых объектов сцены.
                if (applied == before) applied += ApplyOnScene(edit);
            }
            restoring = false;
            return applied;
        }

        /// <summary>
        /// Применить одну правку к одному объекту, если совпали имя и слот материала.
        /// `true` — правка применена. Материал берётся через `materials` (копии), поэтому
        /// ассеты проекта остаются нетронутыми.
        /// </summary>
        private bool TryApply(Renderer renderer, KvMaterialEdit edit)
        {
            if (renderer == null || edit == null) return false;
            string prefix = renderer.gameObject.name + "#";
            if (edit.target.Length <= prefix.Length) return false;
            if (string.CompareOrdinal(edit.target, 0, prefix, 0, prefix.Length) != 0) return false;

            int slot;
            if (!int.TryParse(edit.target.Substring(prefix.Length), out slot)) return false;
            if (slot < 0 || slot >= renderer.sharedMaterials.Length) return false;

            Material[] materials = renderer.materials;
            if (slot >= materials.Length) return false;
            if (!originals.ContainsKey(edit.target)) originals[edit.target] = Read(materials[slot], edit.target);
            ApplyEdit(materials[slot], edit);
            return true;
        }

        /// <summary>Поиск объекта правки по всей сцене (когда объекта нет в списке редактора).</summary>
        private int ApplyOnScene(KvMaterialEdit edit)
        {
            Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            int applied = 0;
            for (int i = 0; i < all.Length; i++)
                if (TryApply(all[i], edit)) applied++;
            return applied;
        }

        /// <summary>
        /// Забыть сохранённые правки: файл удаляется, а материалы возвращаются к исходному виду.
        /// Без этой кнопки отказаться от сохранённых правок было бы невозможно (ФИКС 11.Б).
        /// </summary>
        public bool ForgetEdits(bool announce = true)
        {
            bool removed = FeatureStorage.DeleteFile(EditsPath);
            int forgotten = saved.Count;
            saved.Clear();
            dirty = false;
            ResetAll();
            if (announce)
            {
                Report(removed
                    ? "сохранённые правки материалов забыты (файл удалён), правок было " + forgotten
                    : "файл правок материалов не найден — материалы возвращены к исходному виду");
            }
            return removed;
        }

        /// <summary>Строка состояния файла правок для вкладки.</summary>
        public string EditsStatus()
        {
            string file = HasSavedFile ? "файл: " + EditsPath : "файла правок нет";
            return "правок в списке " + saved.Count + " · " + file +
                   (dirty ? " · есть несохранённые изменения" : "");
        }

        /// <summary>
        /// Применить одну сохранённую правку к материалу. Свечение восстанавливается по
        /// записанным значениям (`_EmissiveColor` уже содержит цвет, умноженный на интенсивность,
        /// поэтому повторно умножать его нельзя — иначе свечение «разгоралось» бы при каждом
        /// восстановлении).
        /// </summary>
        private static void ApplyEdit(Material material, KvMaterialEdit edit)
        {
            if (material == null || edit == null) return;
            if (edit.hasBase)
            {
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", edit.baseColor);
                if (material.HasProperty("_Color")) material.SetColor("_Color", edit.baseColor);
            }
            if (edit.hasMetallic && material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", Mathf.Clamp01(edit.metallic));
            if (edit.hasSmoothness && material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", Mathf.Clamp01(edit.smoothness));
            if (!edit.hasEmission) return;

            if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", edit.emission);
            if (material.HasProperty("_EmissiveIntensity"))
                material.SetFloat("_EmissiveIntensity", edit.emissionIntensity);
            if (material.HasProperty("_EmissiveColorLDR"))
            {
                Color ldr = edit.emissionIntensity > 0.001f
                    ? edit.emission / edit.emissionIntensity
                    : edit.emission;
                material.SetColor("_EmissiveColorLDR", ldr);
            }
            material.EnableKeyword("_EMISSIVE_COLOR_MAP");
        }

        /// <summary>Быстрые готовые варианты: подсветить, сделать матовым, вернуть исходное.</summary>
        public void PresetHighlight()
        {
            SetEmission(new Color(0.25f, 0.75f, 1f), 4f);
            Report("материал подсвечен (голубое свечение)");
        }

        public void PresetMatte()
        {
            SetSmoothness(0.05f);
            SetMetallic(0f);
            Report("материал сделан матовым (блики убраны)");
        }

        public void ResetCurrent()
        {
            string key;
            Material material = MaterialOf(out key);
            if (material == null) return;
            KvMaterialEdit edit;
            if (originals.TryGetValue(key, out edit))
            {
                restoring = true;          // это сброс, а не правка оператора — в список не пишем
                if (edit.hasBase) SetBaseColor(edit.baseColor);
                if (edit.hasMetallic) SetMetallic(edit.metallic);
                if (edit.hasSmoothness) SetSmoothness(edit.smoothness);
                if (edit.hasEmission) SetEmission(edit.emission, edit.emissionIntensity > 0f ? edit.emissionIntensity : 1f);
                restoring = false;
                Unremember(key);           // материал вернулся к исходному виду — правку забываем
                Report("материал возвращён к исходному виду: " + key);
            }
        }

        public void ResetAll()
        {
            foreach (KeyValuePair<string, KvMaterialEdit> pair in originals)
            {
                KvMaterialEdit edit = pair.Value;
                for (int i = 0; i < targets.Count; i++)
                {
                    Material[] materials = targets[i].materials;
                    for (int m = 0; m < materials.Length; m++)
                    {
                        if (targets[i].gameObject.name + "#" + m != pair.Key) continue;
                        Material material = materials[m];
                        if (edit.hasBase)
                        {
                            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", edit.baseColor);
                            if (material.HasProperty("_Color")) material.SetColor("_Color", edit.baseColor);
                        }
                        if (edit.hasMetallic && material.HasProperty("_Metallic"))
                            material.SetFloat("_Metallic", edit.metallic);
                        if (edit.hasSmoothness && material.HasProperty("_Smoothness"))
                            material.SetFloat("_Smoothness", edit.smoothness);
                        if (edit.hasEmission && material.HasProperty("_EmissiveColor"))
                            material.SetColor("_EmissiveColor", edit.emission);
                    }
                }
            }
            originals.Clear();

            // Правок больше нет — список тоже очищается, иначе они вернулись бы из файла
            // при следующем запуске (ФИКС 11.Б).
            if (saved.Count > 0) dirty = true;
            saved.Clear();
            Report("все правки материалов сброшены");
        }

        /// <summary>Убрать правку из списка: материал возвращён к исходному виду, сохранять нечего.</summary>
        private void Unremember(string key)
        {
            for (int i = 0; i < saved.Count; i++)
            {
                if (saved[i].target != key) continue;
                saved.RemoveAt(i);
                dirty = true;
                return;
            }
        }

        public string Status()
        {
            if (string.IsNullOrEmpty(lastError))
                return CurrentLabel + " · правок сохранено " + originals.Count;
            return CurrentLabel + " · " + lastError;
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Material] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>ВКЛАДКА «ОКРУЖЕНИЕ» (ЭТАП 28 ТЗ).</summary>
    public class KvEnvironmentTab : IKvWorkbenchTab
    {
        private readonly KvEnvironmentStudio studio;
        private KvSegmented segment;

        public KvEnvironmentTab(KvEnvironmentStudio environment) { studio = environment; }

        public string Key { get { return "env"; } }
        public string Title { get { return KvLocExtra3.T("env.title", "Окружение"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("env.title", "Окружение (помещение)"));
            kit.Toggle(T("env.show", "Показывать помещение"), studio.Visible, studio.SetVisible);

            string[] titles = new string[studio.Presets.Count];
            for (int i = 0; i < studio.Presets.Count; i++) titles[i] = studio.Presets[i].title;
            segment = kit.Segmented(T("env.preset", "Пресет"), titles, studio.Index,
                delegate (int i) { studio.SetPreset(i); });
            kit.Info(delegate { return studio.Status(); }, KvTheme.Accent);
            kit.Info(delegate
            {
                if (studio.Presets.Count == 0) return "";
                KvEnvironmentPreset preset = studio.Presets[Mathf.Clamp(studio.Index, 0,
                    studio.Presets.Count - 1)];
                return preset.note;
            }, KvTheme.TextDim);
            kit.Buttons(new[] { T("env.next", "Следующий пресет"), T("env.rebuild", "Пересобрать") },
                new Action[] { delegate { studio.Next(); }, delegate { studio.Rebuild(); } });
            kit.Note(T("env.info",
                "Помещение — оформление: у объектов нет коллайдеров, в расчёт столкновений они не " +
                "попадают, роботов в сцене не прибавляется. Смена пресета мгновенная."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh()
        {
            if (segment != null && segment.Index != studio.Index) segment.Set(studio.Index);
        }
    }

    /// <summary>ВКЛАДКА «ОСВЕЩЕНИЕ» (ЭТАП 29 ТЗ).</summary>
    public class KvLightingTab : IKvWorkbenchTab
    {
        private readonly KvLightingStudio studio;
        private KvSegmented segment;

        public KvLightingTab(KvLightingStudio lighting) { studio = lighting; }

        public string Key { get { return "light"; } }
        public string Title { get { return KvLocExtra3.T("light.title", "Освещение"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("light.title", "Освещение (пресеты)"));
            string[] titles = new string[studio.Presets.Count];
            for (int i = 0; i < studio.Presets.Count; i++) titles[i] = studio.Presets[i].title;
            segment = kit.Segmented(T("light.preset", "Пресет"), titles, studio.Index,
                delegate (int i) { studio.SetPreset(i); });
            kit.Info(delegate { return studio.Status(); }, KvTheme.Accent);
            kit.Info(delegate
            {
                if (studio.Presets.Count == 0) return "";
                return studio.Presets[Mathf.Clamp(studio.Index, 0, studio.Presets.Count - 1)].note;
            }, KvTheme.TextDim);
            kit.Buttons(new[]
            {
                T("light.next", "Следующий"),
                T("light.apply", "Применить"),
                T("light.instant", "Без перехода")
            }, new Action[]
            {
                delegate { studio.Next(); },
                delegate { studio.Apply(0.6f, 0f); },
                delegate { studio.Apply(0f, 0f); }
            });
            kit.Note(T("light.info",
                "Меняются реальные параметры сцены: солнце (цвет, люксы, высота), студийный свет " +
                "проекта, туман, градиентное небо и экспозиция в глобальном Volume HDRP. " +
                "Переход плавный — 0,6 с."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh()
        {
            if (segment != null && segment.Index != studio.Index) segment.Set(studio.Index);
        }
    }

    /// <summary>ВКЛАДКА «МАТЕРИАЛЫ» (ЭТАП 30 ТЗ).</summary>
    public class KvMaterialTab : IKvWorkbenchTab
    {
        private readonly KvMaterialStudio studio;
        private readonly Func<Transform> rootProvider;
        private readonly Func<Camera> cameraProvider;
        private float colorR = 0.8f, colorG = 0.8f, colorB = 0.8f;
        private float metallic, smoothness = 0.5f;
        private float emission = 0f;

        public KvMaterialTab(KvMaterialStudio materials, Func<Transform> targetRoot,
            Func<Camera> camera)
        {
            studio = materials;
            rootProvider = targetRoot;
            cameraProvider = camera;
        }

        public string Key { get { return "material"; } }
        public string Title { get { return KvLocExtra3.T("mat.title", "Материалы"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("mat.title", "Редактор материалов"));
            kit.Buttons(new[]
            {
                T("mat.refresh", "Обновить список"),
                T("mat.pick", "Пипетка"),
                T("mat.prev", "◀"),
                T("mat.next", "▶")
            }, new Action[]
            {
                delegate { studio.Refresh(rootProvider != null ? rootProvider() : null); LoadValues(); },
                delegate { studio.PickFromCenter(cameraProvider != null ? cameraProvider() : null); LoadValues(); },
                delegate { studio.Next(-1); LoadValues(); },
                delegate { studio.Next(1); LoadValues(); }
            });
            kit.Info(delegate { return studio.Status(); },
                string.IsNullOrEmpty(studio.LastError) ? KvTheme.Accent : KvTheme.Warn);

            kit.Slider(T("mat.red", "Красный"), 0f, 1f, colorR, "0.00", delegate (float v)
            {
                colorR = v;
                studio.SetBaseColor(new Color(colorR, colorG, colorB));
            });
            kit.Slider(T("mat.green", "Зелёный"), 0f, 1f, colorG, "0.00", delegate (float v)
            {
                colorG = v;
                studio.SetBaseColor(new Color(colorR, colorG, colorB));
            });
            kit.Slider(T("mat.blue", "Синий"), 0f, 1f, colorB, "0.00", delegate (float v)
            {
                colorB = v;
                studio.SetBaseColor(new Color(colorR, colorG, colorB));
            });
            kit.Slider(T("mat.metallic", "Металличность"), 0f, 1f, metallic, "0.00",
                delegate (float v) { metallic = v; studio.SetMetallic(v); });
            kit.Slider(T("mat.smooth", "Гладкость"), 0f, 1f, smoothness, "0.00",
                delegate (float v) { smoothness = v; studio.SetSmoothness(v); });
            kit.Slider(T("mat.emission", "Свечение"), 0f, 8f, emission, "0.0", delegate (float v)
            {
                emission = v;
                studio.SetEmission(new Color(0.25f, 0.75f, 1f), v);
            });

            kit.Buttons(new[]
            {
                T("mat.highlight", "Подсветить"),
                T("mat.matte", "Сделать матовым"),
                T("mat.reset", "Сброс"),
                T("mat.resetall", "Сброс всех")
            }, new Action[]
            {
                delegate { studio.PresetHighlight(); LoadValues(); },
                delegate { studio.PresetMatte(); LoadValues(); },
                delegate { studio.ResetCurrent(); LoadValues(); },
                delegate { studio.ResetAll(); LoadValues(); }
            });

            // Сохранение правок между запусками (ФИКС 11.Б): файл рядом с проектом, не в ассетах.
            kit.Buttons(new[]
            {
                T("mat.save", "Сохранить правки"),
                T("mat.forget", "Забыть сохранённые правки")
            }, new Action[]
            {
                delegate { studio.SaveEdits(); },
                delegate { studio.ForgetEdits(); LoadValues(); }
            });
            kit.Info(delegate { return studio.EditsStatus(); },
                studio.HasSavedFile ? KvTheme.Ok : KvTheme.TextDim);
            kit.Note(T("mat.persist",
                "Правки сохраняются в JSON рядом с остальными данными функций (каталог Config) и " +
                "возвращаются на место при следующем запуске; при выходе из режима игры они " +
                "сохраняются сами. «Забыть сохранённые правки» удаляет файл и возвращает материалы " +
                "к исходному виду. В сами файлы материалов проекта правки по-прежнему НЕ пишутся."),
                KvTheme.TextDim);

            kit.Note(T("mat.info",
                "Правки идут по КОПИИ материала: файлы проекта не меняются, «Сброс» возвращает исходный " +
                "вид. Полезно перед снимками и отчётом — убрать блики или подсветить деталь."),
                KvTheme.TextDim);
        }

        private void LoadValues()
        {
            KvMaterialEdit edit = studio.CurrentValues();
            if (edit == null) return;
            colorR = edit.baseColor.r;
            colorG = edit.baseColor.g;
            colorB = edit.baseColor.b;
            metallic = edit.metallic;
            smoothness = edit.hasSmoothness ? edit.smoothness : 0.5f;
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
