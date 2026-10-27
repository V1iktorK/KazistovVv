using UnityEngine;
using TrajectoryCore;

namespace KazistovVvXr
{
    /// <summary>
    /// ПРИ ЗАПУСКЕ В ШЛЕМЕ ПУСТО (§42, пункт 3). Гасит столы сцены вместе с их роботами и печатает
    /// строку с числами; повторяет гашение, если кто-то включил их снова (в VR это делает
    /// `KvXrMrRoom.ApplyLayers` — правило MR-скрытия не трогаем, оно закреплено тестом).
    ///
    /// СЦЕНА НЕ МЕНЯЕТСЯ: `XrMain.unity` остаётся байт в байт.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public class KvXrSceneEmpty : MonoBehaviour
    {
        /// <summary>Гасить столы и роботов сцены при запуске в шлеме.</summary>
        public bool hideSceneStandsOnStart = true;

        /// <summary>Как часто перепроверять, что их не включили снова (секунды).</summary>
        public float checkIntervalSeconds = 1f;

        /// <summary>Печатать строку-доказательство в журнал.</summary>
        public bool logOnHide = true;

        private float nextCheck;
        private int hiddenTables;
        private int hiddenRobots;
        private bool reported;

        /// <summary>
        /// ПОРЯДОК ПЕРВОГО ГАШЕНИЯ (§42, дефект Д12). На старте меню строилось РАНЬШЕ гашения и
        /// показывало «столов 2 · роботов 2», хотя сцена пустая: `10:13:28.476 МЕНЮ: открыто · столов 2`
        /// → `10:13:28.794 СЦЕНА: выключено роботов 2 · столов 2`. Найдено в шлеме 25.10.2026.
        ///
        /// Почему Awake, а не Start: меню и сторож интерфейса строятся в `Start`, а `Awake`
        /// выполняется у всех компонентов раньше любого `Start` — значит к моменту построения меню
        /// столы уже выключены, и список честно показывает 0.
        /// </summary>
        void Awake()
        {
            Apply();
        }

        void Start()
        {
            // Повтор после Start: MR-слой (`KvXrMrRoom`) мог включить стенды снова.
            Apply();
        }

        void LateUpdate()
        {
            if (!hideSceneStandsOnStart) return;
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + Mathf.Max(0.25f, checkIntervalSeconds);

            // Если MR-слой снова включил столы — гасим и говорим об этом ОДИН раз (без спама).
            if (AnyStandVisible()) Apply();
        }

        /// <summary>ПОГАСИТЬ СТОЛЫ СЦЕНЫ И ИХ РОБОТОВ. Идемпотентно.</summary>
        public void Apply()
        {
            if (!hideSceneStandsOnStart) return;

            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            int tables = 0;
            int robots = 0;

            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.parent != null) continue;                       // только корни сцены
                if (!IsSceneStand(t.name)) continue;

                tables++;
                RobotController[] inside = t.GetComponentsInChildren<RobotController>(true);
                robots += inside != null ? inside.Length : 0;

                if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
            }

            hiddenTables = tables;
            hiddenRobots = robots;

            if (logOnHide && !reported)
            {
                reported = true;
                Debug.Log(Describe(hiddenTables, hiddenRobots));
            }
        }

        /// <summary>Есть ли хоть один включённый стол сцены (значит гашение сняли).</summary>
        public bool AnyStandVisible()
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.parent != null) continue;
                if (!IsSceneStand(t.name)) continue;
                if (t.gameObject.activeSelf) return true;
            }
            return false;
        }

        /// <summary>Стенд ли это (стол сцены вместе с его роботами).</summary>
        public static bool IsSceneStand(string objectName)
        {
            return KvXrSceneEmptyState.IsSceneStand(objectName);
        }

        /// <summary>Строка журнала: сколько выключено (числа обязательны).</summary>
        public static string Describe(int tables, int robots)
        {
            return KvXrSceneEmptyState.Describe(tables, robots);
        }
    }
}
