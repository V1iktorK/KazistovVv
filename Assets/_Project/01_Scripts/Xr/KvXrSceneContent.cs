using UnityEngine;
using TrajectoryCore;
using KazistovVvFeatures;

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

        // ==================================================== ГДЕ СТОИТ ЛАБОРАТОРИЯ (§33)

        /// <summary>
        /// ПРИДВИГАТЬ СТЕНДЫ К ОПЕРАТОРУ. Найдено на живом шлеме 15.10.2026: владелец надел
        /// шлем и увидел «только сцену с горизонтом, никаких ангаров». Причина оказалась
        /// арифметической: `StandBuilder` ставит стенды на z = −20,7 и −31,3 (это удобно для
        /// десктопной сцены, где камера летает), а игрок в VR стоит в начале координат — то есть
        /// столы с роботами оказывались в **20–30 метрах**, мелкими силуэтами у горизонта.
        /// Здесь стенды ставятся на расстояние вытянутой руки-взгляда: впереди и по сторонам.
        ///
        /// ИСПРАВЛЕНО 16.10.2026 (§35) — ВТОРОЙ ДЕФЕКТ ТОГО ЖЕ МЕСТА. Прежняя версия ставила
        /// стенды «влево и вправо» на 3,2 м от оператора. Но столешница проекта — **14,4 × 9,6 м**,
        /// то есть половина ГЛУБИНЫ равна 4,8 м. При расстоянии 3,2 м ближняя кромка стола
        /// оказывалась на **1,6 м ПОЗАДИ оператора**: он стоял ВНУТРИ обоих столов, а два стола
        /// шириной 14,4 м, поставленные в 3 м друг от друга, почти полностью накладывались.
        /// Владелец 16.10.2026: «столов или роботов не видно» — и это ровно то, чего он не должен
        /// видеть по своему пункту «я не внутри робота» ([S2.3]).
        ///
        /// КАК СТАВИТСЯ ТЕПЕРЬ. Пара стендов переносится ЦЕЛИКОМ, одним смещением, поэтому их
        /// взаимное расположение остаётся ровно таким, как на компьютере (расстояние между ними
        /// и зазор 1 м не меняются — «на своих местах»). Смещение считается по ФАКТИЧЕСКОМУ
        /// общему габариту, а не по константе: скамья ставится так, чтобы её БЛИЖНЯЯ КРОМКА
        /// оказалась в `standNearEdgeMeters` перед оператором, а сама она — по центру оператора.
        /// </summary>
        public bool bringStandsNearPlayer = true;

        /// <summary>
        /// РАССТОЯНИЕ ОТ ОПЕРАТОРА ДО БЛИЖНЕЙ КРОМКИ СТОЛОВ, метры. Полметра — это поза
        /// «стою у верстака»: столешница на высоте 0,98 м оказывается примерно по пояс и на
        /// расстоянии вытянутой руки, как и требует владелец ([S2.3]).
        /// </summary>
        public float standNearEdgeMeters = 0.5f;

        /// <summary>
        /// УСТАРЕЛО, ДЛЯ РАССТАНОВКИ НЕ ИСПОЛЬЗУЕТСЯ. Оставлено, чтобы не ломать сохранённые
        /// значения сцены и внешние вызовы. Расстояние теперь считается от габарита столов
        /// (см. `standNearEdgeMeters`): константа 3,2 м была МЕНЬШЕ половины глубины стола (4,8 м)
        /// и ставила оператора ВНУТРЬ стола.
        /// </summary>
        public float standDistance = 3.2f;

        /// <summary>
        /// УСТАРЕЛО, ДЛЯ РАССТАНОВКИ НЕ ИСПОЛЬЗУЕТСЯ: два стола идут ДРУГ ЗА ДРУГОМ
        /// (как на компьютере), а не влево-вправо.
        /// </summary>
        public float standSpacing = 3.0f;

        /// <summary>Создать пол под ногами: без него сцена «висит в небе» — нет ни опоры, ни масштаба.</summary>
        public bool createFloor = true;

        /// <summary>Сторона квадрата пола, метры.</summary>
        public float floorSize = 40f;

        /// <summary>
        /// НАСКОЛЬКО ПОЛ ОПУСКАЕТСЯ НИЖЕ НУЛЯ, метры (§35). У ангара есть СВОЙ пол: плиты 10×10 м
        /// лежат ровно на y = 0, и в середине зала у них проём 10 м. Пол, созданный здесь, лежит
        /// в ТОЙ ЖЕ плоскости — две совпадающие поверхности дают мерцание («двоение» пола), которое
        /// владелец заметил бы сразу. Поэтому этот пол опускается на 3 см: он остаётся опорой
        /// и закрывает проём, но спорить за одну плоскость с плитами ангара больше не может.
        /// </summary>
        public float floorDropMeters = 0.03f;

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

                if (bringStandsNearPlayer) PlaceStandsAroundPlayer();
            }

            if (createFloor) EnsureFloor();

            RobotCount = CountRobots();
            RobotRoot = FindRobotRoot();

            if (RobotCount == 0)
                Debug.LogWarning("[KvXr] В XR-сцене нет ни одного робота — показывать в шлеме нечего. " +
                    "Проверьте, что StandBuilder нашёл модели роботов (они берутся из сцены проекта).");
            else
                Debug.Log("[KvXr] Робот для показа найден: " + (RobotRoot != null ? RobotRoot.name : "—") +
                    " (всего роботов в сцене: " + RobotCount + ")");
        }

        /// <summary>
        /// ПОСТАВИТЬ СТЕНДЫ ВОКРУГ ОПЕРАТОРА (впереди слева и справа). Роботы стоят на столах,
        /// столы — дети стендов, поэтому перенос корня стенда двигает всю группу целиком.
        /// Поворот стендов НЕ меняется: на десктопе они смотрят друг на друга, и ломать это
        /// в шлеме незачем.
        /// </summary>
        private void PlaceStandsAroundPlayer()
        {
            // Оператор нужен только чтобы убедиться, что он вообще есть: расстановка теперь
            // не зависит от того, куда он смотрит, — лаборатория просто сдвигается на него.
            Transform player = PlayerTransform();
            if (player == null)
            {
                Debug.LogWarning("[KvXr] Оператор не найден (нет камеры) — стенды оставлены на своих " +
                    "местах: в шлеме они окажутся далеко.");
                return;
            }

            GameObject stand1 = FindByName(StandBuilder.Stand1Name);
            GameObject stand2 = FindByName(StandBuilder.Stand2Name);

            // ЕДИНОЕ СМЕЩЕНИЕ НА ВСЮ ЛАБОРАТОРИЮ. Столы НЕ переставляются относительно друг
            // друга и относительно зала: они уже стоят в сцене в своих десктопных координатах,
            // и к ним применяется тот же сдвиг, что и к залу (`KvXrHangarAnchor`). Так в шлеме
            // получается РОВНО десктопная картина, а оператор оказывается в центре стендов —
            // то есть в зазоре между двумя столами.
            //
            // ПРЕЖНЯЯ ОШИБКА (исправлено 16.10.2026). Здесь столы ставились «влево и вправо»
            // на 3,2 м от оператора. Столешница проекта — 14,4 × 9,6 м, половина глубины 4,8 м,
            // поэтому ближняя кромка оказывалась на 1,6 м ПОЗАДИ него: оператор стоял ВНУТРИ
            // обоих столов, а два стола шириной 14,4 м с центрами в 3 м почти совпадали.
            // Владелец: «столов или роботов не видно», «я не внутри робота».
            Vector3 shift = KvHangarPlacement.LabShiftToOperator;

            bool moved1 = stand1 != null && MoveRoot(stand1, ShiftedAuthored(stand1, shift));
            bool moved2 = stand2 != null && MoveRoot(stand2, ShiftedAuthored(stand2, shift));

            if (stand1 == null && stand2 == null)
            {
                Debug.LogWarning("[KvXr] Стенды не найдены — расстановка не выполнена. " +
                                 "Ожидались «" + StandBuilder.Stand1Name + "» и «" + StandBuilder.Stand2Name + "».");
                return;
            }

            Bounds bench = new Bounds();
            bool any = false;
            CollectBounds(stand1, ref bench, ref any);
            CollectBounds(stand2, ref bench, ref any);

            float separation = stand1 != null && stand2 != null
                ? Vector3.Distance(stand1.transform.position, stand2.transform.position) : 0f;
            float nearEdge = any ? bench.min.z : 0f;
            float farEdge = any ? bench.max.z : 0f;

            Debug.Log(string.Format(
                "[KvXr] Лаборатория сдвинута на оператора: стендов перенесено {0} · сдвиг {1} · " +
                "«{2}» {3} · «{4}» {5} · расстояние между стендами {6:0.00} м (десктопное) · " +
                "столы занимают z от {7:0.00} до {8:0.00} м · оператор в зазоре между столами",
                (moved1 ? 1 : 0) + (moved2 ? 1 : 0), shift.ToString("0.00"),
                StandBuilder.Stand1Name, stand1 != null ? stand1.transform.position.ToString("0.0") : "—",
                StandBuilder.Stand2Name, stand2 != null ? stand2.transform.position.ToString("0.0") : "—",
                separation, nearEdge, farEdge));
        }

        /// <summary>
        /// КУДА ВСТАНЕТ СТОЛ: его ДЕСКТОПНАЯ координата плюс единый сдвиг лаборатории.
        /// Считается от исходной координаты, а не от текущей позиции, — иначе повторный запуск
        /// сдвигал бы столы ВТОРОЙ раз (метод вызывается при каждом старте сцены).
        /// </summary>
        private static Vector3 ShiftedAuthored(GameObject stand, Vector3 shift)
        {
            Vector3 authored = stand.name == StandBuilder.Stand1Name
                ? StandBuilder.Stand1Pos
                : StandBuilder.Stand2Pos;
            return authored + shift;
        }

        /// <summary>Добавить габарит стенда в общий габарит скамьи (null — пропустить).</summary>
        private static void CollectBounds(GameObject stand, ref Bounds bench, ref bool any)
        {
            if (stand == null) return;
            foreach (Renderer r in stand.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!any) { bench = r.bounds; any = true; }
                else bench.Encapsulate(r.bounds);
            }
        }

        private static bool MoveRoot(GameObject root, Vector3 position)
        {
            if (root == null) return false;
            root.transform.position = position;
            return true;
        }

        /// <summary>Кто «игрок»: камера шлема, а если её нет — любая активная камера сцены.</summary>
        private static Transform PlayerTransform()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                foreach (Camera candidate in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                {
                    if (candidate == null || !candidate.isActiveAndEnabled) continue;
                    camera = candidate;
                    break;
                }
            }
            return camera != null ? camera.transform : null;
        }

        /// <summary>
        /// ПОЛ ПОД НОГАМИ. Без него оператор «висит в небе»: нет ни опоры, ни масштаба, ни места,
        /// куда наводить телепорт. Пол даёт и то, и другое (у примитива есть коллайдер).
        /// </summary>
        private void EnsureFloor()
        {
            if (FindByName(FloorName) != null) return;

            Transform player = PlayerTransform();
            Vector3 center = player != null
                ? new Vector3(player.position.x, 0f, player.position.z)
                : Vector3.zero;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = FloorName;
            // Опущен на floorDropMeters: плиты пола ангара лежат ровно на y = 0, и совпадение
            // плоскостей дало бы мерцание. Подробнее — в описании поля.
            floor.transform.position = center - Vector3.up * Mathf.Max(0f, floorDropMeters);
            floor.transform.localScale = Vector3.one * (floorSize / 10f);   // примитив Plane = 10×10 м

            var renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = KvRenderPipelineShaders.CreateLit("Материал_пола_XR",
                    new Color(0.20f, 0.21f, 0.23f));
                if (material != null) renderer.sharedMaterial = material;
            }

            Debug.Log("[KvXr] Пол XR-сцены: " + floorSize + "×" + floorSize +
                      " м под оператором на y = " + (-Mathf.Max(0f, floorDropMeters)).ToString("0.00") +
                      " м (опора, масштаб, поверхность для телепорта; ниже пола ангара, чтобы не мерцало)");
        }

        /// <summary>Имя пола — по нему проверяется идемпотентность.</summary>
        public const string FloorName = "XrFloor";

        private static int CountRobots()
        {
            var all = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            return all != null ? all.Length : 0;
        }

        /// <summary>Найти объект сцены по имени (включая выключенные).</summary>
        private static GameObject FindByName(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && t.name == name) return t.gameObject;
            return null;
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
