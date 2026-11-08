using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KazistovVv.Core;
using KazistovVvXr;

namespace KazistovVvAr
{
    /// <summary>
    /// СОДЕРЖИМОЕ AR-МИРА: РОБОТ НА РЕАЛЬНОМ СТОЛЕ.
    ///
    /// ЧТО СОЗДАЁТСЯ. Копия штатного робота проекта — тем же способом, что в шлеме
    /// (`KvXrObjectFactory.CreateRobot` → `ObjectSpawner.SpawnRobot` → копия сценного эталона).
    /// Второй фабрики роботов не заводится: модель, суставы, контроллер и материалы должны быть
    /// ровно такие же, как проверены в VR (иначе «в AR робот не тот», и это ищут неделю).
    ///
    /// МАСШТАБ 1:1 (пункт В1 приёмки). Робот НЕ масштабируется: `localScale = 1`, родитель —
    /// якорь с масштабом 1. В журнал печатается фактическая высота в метрах — её владелец
    /// сверяет с настоящей ручкой рядом со столом. Число, а не слово «нормально».
    ///
    /// ГДЕ СТОИТ. Позиция — нижняя точка попадания в плоскость (основание робота на столешнице),
    /// поворот — к устройству (оператор видит переднюю сторону робота, а не спину).
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArContent : MonoBehaviour
    {
        /// <summary>
        /// ОЖИДАЕМАЯ ВЫСОТА ШТАТНОГО 6-ОСЕВОГО РОБОТА, метры. Замер сцены в сессии S45
        /// (MR-агент, `ОТЧЁТ_MR.md`): 1,61 м. Служит стражем: если в AR робот окажется другого
        /// размера, строка журнала и тест это покажут.
        /// </summary>
        public const float ExpectedDefaultRobotHeightMeters = 1.61f;

        /// <summary>Допуск на измерение габарита, метры (правило приёмки — 5 мм; берём 20 мм на парсер мешей).</summary>
        public const float HeightToleranceMeters = 0.02f;

        /// <summary>Текущий робот AR-мира (null — не создан).</summary>
        public RobotController CurrentRobot { get; private set; }

        /// <summary>Фактическая измеренная высота текущего робота, метры (0 — не измерена).</summary>
        public float CurrentHeightMeters { get; private set; }

        /// <summary>Как создан робот: «эталон сцены» или «модель каталога &lt;id&gt;» — для журнала.</summary>
        public string SpawnSource { get; private set; }

        /// <summary>Писать в журнал о создании (обязательные строки-доказательства).</summary>
        public bool logOnSpawn = true;

        /// <summary>Корень мира, под который поставлен текущий робот (для строки журнала).</summary>
        private Transform currentRoot;

        /// <summary>
        /// ПОСТАВИТЬ ШТАТНОГО РОБОТА (6-осевой эталон сцены) НА ЯКОРЬ.
        /// `localPosition` — в системе координат якоря (0,0,0 = точка попадания в плоскость).
        /// </summary>
        public bool SpawnDefaultRobot(Transform labRoot, Vector3 localPosition, float yawDegrees)
        {
            if (labRoot == null)
            {
                Debug.LogWarning("[KvAr] РОБОТ: нет корня мира — создать некого");
                return false;
            }

            Vector3 worldPosition = labRoot.TransformPoint(localPosition);
            if (!IsOnRootPlane(labRoot, worldPosition))
                Debug.LogWarning("[KvAr] РОБОТ: точка создания не на плоскости якоря — проверьте постановку");

            RobotController robot = KvXrObjectFactory.CreateRobot(false, worldPosition, yawDegrees);
            if (robot == null)
            {
                Debug.LogError("[KvAr] РОБОТ: не создан — в сцене нет шаблона (смотрите строку выше)");
                return false;
            }

            // РОДИТЕЛЬ — ЯКОРЬ, И ТОЛЬКО ОН. Объект, оставшийся в мировых координатах, выглядел бы
            // стоящим на месте, но при восстановлении трекинга «уехал» бы относительно стола.
            robot.transform.SetParent(labRoot, worldPositionStays: true);
            currentRoot = labRoot;
            CurrentRobot = robot;
            SpawnSource = "эталон сцены";
            CurrentHeightMeters = MeasureHeightMeters(robot.gameObject);

            if (logOnSpawn) Debug.Log("[KvAr] " + DescribeLastSpawn());
            CheckHeight();
            LogMaterials(robot.gameObject);
            ApplyFallbackLookIfPlaceholder(robot.gameObject);
            return true;
        }

        /// <summary>
        /// ПОСТАВИТЬ МОДЕЛЬ ИЗ КАТАЛОГА (12 моделей, включая двурукого). Прямой путь для
        /// команд `robot.model.*` и для проверки «выбор модели заменяет робота».
        /// </summary>
        public bool SpawnModel(string modelId, Transform labRoot, Vector3 localPosition, float yawDegrees)
        {
            KvRobotModel model = KvRobotCatalog.Find(modelId);
            if (model == null)
            {
                Debug.LogWarning("[KvAr] РОБОТ: модели «" + modelId + "» нет в каталоге");
                return false;
            }
            if (labRoot == null)
            {
                Debug.LogWarning("[KvAr] РОБОТ: нет корня мира — создать некого");
                return false;
            }

            if (CurrentRobot != null)
            {
                Debug.Log("[KvAr] РОБОТ: замена «" + CurrentRobot.robotName + "» на модель «" +
                          model.Title + "» (второй робот не добавляется)");
                UnityEngine.Object.Destroy(CurrentRobot.gameObject);
                CurrentRobot = null;
            }

            Vector3 worldPosition = labRoot.TransformPoint(localPosition);
            RobotController robot = KvRobotSpawner.Create(model, worldPosition, yawDegrees, labRoot);
            if (robot == null)
            {
                Debug.LogError("[KvAr] РОБОТ: модель «" + model.Title + "» не создана");
                return false;
            }

            currentRoot = labRoot;
            CurrentRobot = robot;
            SpawnSource = "модель каталога " + model.Id;
            CurrentHeightMeters = MeasureHeightMeters(robot.gameObject);
            if (logOnSpawn) Debug.Log("[KvAr] " + DescribeLastSpawn());
            CheckHeight();
            LogMaterials(robot.gameObject);
            ApplyFallbackLookIfPlaceholder(robot.gameObject);
            return true;
        }

        /// <summary>
        /// СТРОКА-ДОКАЗАТЕЛЬСТВО О МАТЕРИАЛАХ. Пишется при каждой постановке робота, чтобы по
        /// журналу планшета было видно ФАКТ: дошли ли материалы/текстуры до AR-сборки.
        /// Предупреждение — если видимая геометрия похожа на заглушку («белые панели»).
        /// </summary>
        public void LogMaterials(GameObject robotRoot)
        {
            if (!logOnSpawn) return;
            string summary = DescribeMaterials(robotRoot);
            if (LooksLikePlaceholder(robotRoot))
            {
                Debug.LogWarning("[KvAr] МАТЕРИАЛЫ: " + summary +
                                 " · ВНИМАНИЕ: похоже на заглушку (нет текстур/материала по " +
                                 "умолчанию) — робот будет однотонным; проверить материалы в " +
                                 "AR-сборке");
            }
            else
            {
                Debug.Log("[KvAr] МАТЕРИАЛЫ: " + summary);
            }
        }

        /// <summary>
        /// ЕСЛИ МАТЕРИАЛЫ НЕ ДОШЛИ ДО СБОРКИ — ДАТЬ РОБОТУ УЗНАВАЕМЫЙ ВИД (07.11.2026).
        /// На фото планшета робот был ОДНОТОННОЙ СВЕТЛОЙ ПАНЕЛЬЮ без окраски и суставов. Когда
        /// разбор материалов показывает заглушку (нет текстур / материал по умолчанию), модель
        /// остаётся без окраски и «не читается» как робот. Красим видимые рендереры СОБСТВЕННЫМ
        /// материалом AR (тот же URP-конвейер, что у остального AR-контента) и пишем это в журнал.
        /// Нормальные материалы НЕ ТРОГАЕМ: правится только случай «белых панелей».
        /// </summary>
        public bool ApplyFallbackLookIfPlaceholder(GameObject robotRoot)
        {
            if (robotRoot == null) return false;
            if (!LooksLikePlaceholder(robotRoot)) return false;

            Material material = KazistovVvFeatures.KvRenderPipelineShaders.CreateLit(
                "Материал_робота_AR (запасной)", new Color(0.93f, 0.45f, 0.08f));
            if (material == null) return false;

            Renderer[] renderers = robotRoot.GetComponentsInChildren<Renderer>(true);
            int painted = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                Material current = renderer.sharedMaterial;
                // Правим ТОЛЬКО «пустые» места: материал по умолчанию или его отсутствие.
                // Части с настоящими материалами остаются как есть.
                if (current != null && current.shader != null && !IsDefaultMaterial(current)) continue;
                renderer.sharedMaterial = material;
                painted++;
            }
            if (painted == 0) return false;
            Debug.Log("[KvAr] МАТЕРИАЛЫ: роботу назначен запасной материал AR (оранжевый, URP/Lit) " +
                      "на " + painted + " рендереров — исходные материалы в сборку не дошли");
            return true;
        }

        /// <summary>ЗАБЫТЬ РОБОТА (мир переставляется/удаляется).</summary>
        public void ForgetCurrentRobot()
        {
            CurrentRobot = null;
            currentRoot = null;
            CurrentHeightMeters = 0f;
            SpawnSource = string.Empty;
        }

        /// <summary>Строка-доказательство о создании: модель, высота, масштаб, источник.</summary>
        public string DescribeLastSpawn()
        {
            if (CurrentRobot == null) return "РОБОТ: не создан";
            Vector3 scale = CurrentRobot.transform.lossyScale;
            StringBuilder sb = new StringBuilder();
            sb.Append("РОБОТ: «").Append(CurrentRobot.robotName).Append("»");
            sb.Append(" · источник: ").Append(SpawnSource);
            sb.Append(" · высота ").Append(CurrentHeightMeters.ToString("0.000",
                System.Globalization.CultureInfo.InvariantCulture)).Append(" м");
            sb.Append(" · масштаб ").Append(scale.x.ToString("0.000",
                System.Globalization.CultureInfo.InvariantCulture)).Append(";")
              .Append(scale.y.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(";")
              .Append(scale.z.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(" · основание ").Append(CurrentRobot.transform.position.ToString("0.000"));
            sb.Append(currentRoot != null && CurrentRobot.transform.IsChildOf(currentRoot) ? " · под якорем мира" : " · ПОД ЯКОРЕМ: НЕТ");
            return sb.ToString();
        }

        /// <summary>
        /// СВЕРКА ВЫСОТЫ СО ШТАТНОЙ. Не бросает исключение и не останавливает приложение:
        /// реальный робот может быть другой моделью — она честно называется в журнале.
        /// </summary>
        public void CheckHeight()
        {
            if (CurrentRobot == null || CurrentHeightMeters <= 0f) return;
            float deviation = Mathf.Abs(CurrentHeightMeters - ExpectedDefaultRobotHeightMeters);
            if (deviation > HeightToleranceMeters)
                Debug.LogWarning("[KvAr] МАСШТАБ: высота робота " +
                                 CurrentHeightMeters.ToString("0.000",
                                     System.Globalization.CultureInfo.InvariantCulture) +
                                 " м отличается от штатных " +
                                 ExpectedDefaultRobotHeightMeters.ToString("0.000",
                                     System.Globalization.CultureInfo.InvariantCulture) +
                                 " м на " + deviation.ToString("0.000") +
                                 " м — проверьте масштаб (1:1)");
            else
                Debug.Log("[KvAr] МАСШТАБ: высота робота " +
                          CurrentHeightMeters.ToString("0.000",
                              System.Globalization.CultureInfo.InvariantCulture) +
                          " м совпадает со штатной (отклонение " +
                          deviation.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " м)");
        }

        /// <summary>
        /// ИЗМЕРИТЬ ВЫСОТУ ОБЪЕКТА ПО ГАБАРИТАМ ВИДИМОЙ ГЕОМЕТРИИ, метры.
        /// Только ВКЛЮЧЁННЫЕ рендереры: выключенные «эталоны» внутри стенда не должны попадать
        /// в замер (иначе высота была бы от пола ангара до крыши).
        /// </summary>
        public static float MeasureHeightMeters(GameObject root)
        {
            if (root == null) return 0f;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            bool any = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return any ? bounds.size.y : 0f;
        }

        /// <summary>ТОЧКА ЛЕЖИТ НА ПЛОСКОСТИ ЯКОРЯ (допуск 1 мм) — проверка «основание на столе».</summary>
        public static bool IsOnRootPlane(Transform labRoot, Vector3 worldPoint)
        {
            if (labRoot == null) return false;
            return Mathf.Abs(worldPoint.y - labRoot.position.y) <= 0.001f;
        }

        /// <summary>ОТКЛОНЕНИЕ ОСНОВАНИЯ ОТ ПЛОСКОСТИ, метры (пункт Б2/Б3 приёмки, порог 5 мм).</summary>
        public static float BaseHeightErrorMeters(Vector3 robotBaseWorld, float planeY)
        {
            return Mathf.Abs(robotBaseWorld.y - planeY);
        }

        /// <summary>ВЫСОТА ПРАВДОПОДОБНА (0,3…2,5 м): защита от «робота-игрушки» и «робота-гиганта».</summary>
        public static bool IsPlausibleRobotHeight(float heightMeters)
        {
            return heightMeters >= 0.30f && heightMeters <= 2.50f;
        }

        /// <summary>
        /// МАТЕРИАЛЫ ВИДИМОЙ ГЕОМЕТРИИ — СТРОКА В ЖУРНАЛ (07.11.2026).
        ///
        /// ЗАЧЕМ. На фотографиях планшета робот выглядел двумя-тремя однотонными светлыми
        /// панелями без окраски и суставов — «белые плоские панели вместо частей робота». По
        /// снимку нельзя отличить три причины: (1) материалы/текстуры не попали в AR-сборку,
        /// (2) в шаблоне сцены остался материал по умолчанию, (3) всё в порядке, а виноват свет.
        /// Эта строка даёт ФАКТ вместо догадки: сколько рендереров, какие шейдеры, у скольких
        /// нет текстуры и у скольких материал по умолчанию.
        /// </summary>
        public static string DescribeMaterials(GameObject root, int maxSamples = 6)
        {
            if (root == null) return "рендереров 0";
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return "рендереров 0";

            var shaderCounts = new StringBuilder();
            var seen = new Dictionary<string, int>();
            var order = new List<string>();
            int noTexture = 0;
            int defaultMaterial = 0;
            int noShader = 0;
            int disabled = 0;
            int samples = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                if (!renderer.enabled) { disabled++; continue; }
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null)
                {
                    noShader++;
                    continue;
                }
                string shaderName = material.shader.name;
                if (!seen.ContainsKey(shaderName)) { seen[shaderName] = 0; order.Add(shaderName); }
                seen[shaderName] = seen[shaderName] + 1;

                if (IsDefaultMaterial(material)) defaultMaterial++;
                if (material.mainTexture == null) noTexture++;

                if (samples < maxSamples && samples == 0)
                {
                    shaderCounts.Append(" · пример: «").Append(renderer.name).Append("» → ")
                                .Append(shaderName).Append(" / «").Append(material.name).Append("»")
                                .Append(material.mainTexture != null ? " текстура есть"
                                    : " ТЕКСТУРЫ НЕТ");
                }
                samples++;
            }

            for (int i = 0; i < order.Count; i++)
            {
                if (i > 0) shaderCounts.Append(", ");
                else shaderCounts.Append(" · шейдеры: ");
                shaderCounts.Append("«").Append(order[i]).Append("»×").Append(seen[order[i]]);
            }

            return "рендереров " + renderers.Length +
                   " (выключено " + disabled + ")" +
                   " · без текстуры " + noTexture +
                   " · материал по умолчанию " + defaultMaterial +
                   " · без материала " + noShader +
                   shaderCounts;
        }

        /// <summary>
        /// ЭТО ЗАГЛУШКА, А НЕ МОДЕЛЬ. Дефект планшета 06.11.2026: вместо робота на экране
        /// однотонные светлые панели. Признак заглушки — большинство видимых рендереров с
        /// МАТЕРИАЛОМ ПО УМОЛЧАНИЮ (или вовсе без материала): значит материалы шаблона не дошли
        /// до сборки. Отсутствие текстуры САМО ПО СЕБЕ признаком не считается: у промышленного
        /// робота окраска часто задана цветом материала без картинки.
        /// </summary>
        public static bool LooksLikePlaceholder(GameObject root)
        {
            if (root == null) return false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            if (renderers == null || renderers.Length == 0) return false;

            int suspicious = 0;
            int counted = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;
                counted++;
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null) { suspicious++; continue; }
                if (IsDefaultMaterial(material)) suspicious++;
            }
            if (counted == 0) return false;
            return suspicious * 2 >= counted;      // ≥ половины видимой геометрии
        }

        /// <summary>МАТЕРИАЛ ПО УМОЛЧАНИЮ (заглушка Unity) или служебный шейдер вместо окраски.</summary>
        private static bool IsDefaultMaterial(Material material)
        {
            if (material == null) return true;
            if (material.shader == null) return true;

            string name = material.name ?? string.Empty;
            string shader = material.shader.name ?? string.Empty;

            // 1) встроенный материал Unity (у него стоит флаг «не сохранять»)
            if ((material.hideFlags & HideFlags.DontSave) != 0) return true;
            // 2) имя материала по умолчанию
            if (name.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            // 3) шейдер чужого конвейера или служебный
            if (shader == "Standard") return true;
            if (shader == "Sprites/Default") return true;
            if (shader == "Unlit/Color") return true;
            if (shader == "Unlit/Texture") return true;
            if (shader.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            // 4) у материала вообще нет цвета — это не окраска, а заготовка
            if (!material.HasProperty("_BaseColor") && !material.HasProperty("_Color")) return true;
            return false;
        }
    }
}
