using System;
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
    }
}
