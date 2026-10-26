using UnityEngine;
using TrajectoryCore;

namespace KazistovVvXr
{
    /// <summary>
    /// НАЧИНКА XR-СЦЕНЫ: СТОЛЫ И РОБОТЫ (решение владельца: «добавить роботов и столы», сессия §28).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ КОМПОНЕНТ. По решению [D6] XR-сцена сделана ОТДЕЛЬНОЙ и сначала была
    /// пустой (риг, интерфейс, MR-комната) — начинку предполагалось подгружать. Проверка
    /// показала, что в `XrMain.unity` нет ни одного робота, то есть в шлеме было нечего
    /// показывать. Этот компонент создаёт начинку ТЕМ ЖЕ способом, что и десктопный проект —
    /// через <see cref="StandBuilder"/>, а не копированием объектов из `MainScene`.
    ///
    /// ПОЧЕМУ ЧЕРЕЗ StandBuilder. Три причины:
    ///   1) это ровно тот код, которым построены стенды в `MainScene` — значит геометрия,
    ///      высоты и расположение роботов совпадают с проверенными;
    ///   2) он идемпотентен (`EnsureStands` не создаёт дубликаты) — повторный запуск безопасен;
    ///   3) копирование объектов из другой сцены дало бы скрытые ссылки между сценами
    ///      и разъехалось бы при первой же правке сцены проекта.
    ///
    /// ПОРЯДОК РАБОТЫ. `[DefaultExecutionOrder]` ставит компонент РАНЬШЕ `KvXrMrRoom`: сначала
    /// стенды и роботы появляются, потом MR-постановка переносит робота на реальный стол.
    /// Иначе MR искал бы робота, которого ещё нет.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    [DisallowMultipleComponent]
    public class KvXrSceneContent : MonoBehaviour
    {
        /// <summary>Создавать стенды (столы + роботы), если их нет в сцене.</summary>
        public bool createStands = true;

        /// <summary>Высота столешницы стендов, метры (как в проекте — 0.98).</summary>
        public float tableTopHeight = StandBuilder.TopHeight;

        /// <summary>Высота, на которой строится начинка (World — как в MainScene).</summary>
        public bool buildOnStart = true;

        /// <summary>Робот, найденный в сцене после подготовки (для MR-постановки и отчёта).</summary>
        public Transform RobotRoot { get; private set; }

        /// <summary>Сколько роботов найдено в сцене — для журнала.</summary>
        public int RobotCount { get; private set; }

        void Start()
        {
            if (!buildOnStart) return;
            Build();
        }

        /// <summary>
        /// Подготовить начинку: создать стенды, найти робота, доложить о результате одной строкой.
        /// Вынесено публичным методом, чтобы это можно было вызвать из диагностики.
        /// </summary>
        public void Build()
        {
            if (createStands)
            {
                int before = CountRobots();
                StandBuilder.EnsureStands(tableTopHeight);
                int after = CountRobots();
                Debug.Log(string.Format(
                    "[KvXr] Начинка XR-сцены: стенды обеспечены (столешница {0:0.00} м) · роботов было {1}, стало {2}",
                    tableTopHeight, before, after));
            }

            RobotCount = CountRobots();
            RobotRoot = FindRobotRoot();

            if (RobotCount == 0)
                Debug.LogWarning("[KvXr] В XR-сцене нет ни одного робота — показывать в шлеме нечего. " +
                    "Проверьте, что StandBuilder нашёл модели роботов (они берутся из сцены проекта).");
            else
                Debug.Log("[KvXr] Робот для показа найден: " + (RobotRoot != null ? RobotRoot.name : "—") +
                    " (всего роботов в сцене: " + RobotCount + ")");
        }

        private static int CountRobots()
        {
            var all = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            return all != null ? all.Length : 0;
        }

        private static Transform FindRobotRoot()
        {
            var six = Object.FindObjectsByType<SixAxisController>(FindObjectsInactive.Include);
            if (six != null && six.Length > 0) return six[0].transform;

            var scara = Object.FindObjectsByType<SCARAController>(FindObjectsInactive.Include);
            if (scara != null && scara.Length > 0) return scara[0].transform;

            var generic = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            return generic != null && generic.Length > 0 ? generic[0].transform : null;
        }
    }
}
