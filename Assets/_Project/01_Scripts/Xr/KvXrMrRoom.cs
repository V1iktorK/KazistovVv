using System.Collections.Generic;
using UnityEngine;
using KazistovVvFeatures;

namespace KazistovVvXr
{
    /// <summary>
    /// MR-КОМНАТА: РЕАЛЬНЫЙ СТОЛ, РОБОТ 1:1 И ВЫКЛЮЧАЕМЫЕ СЛОИ ВИРТУАЛЬНОГО
    /// (решения владельца [D5], [D7], [D8]; этап 4 плана XR, сессия §28).
    ///
    /// ЧТО ЗДЕСЬ ПРОИСХОДИТ. В MR оператор видит СВОЮ комнату, а виртуальный робот должен
    /// стоять на ЕГО столе — 100 см поперёк, 70 см в длину, столешница на 78 см от пола
    /// (решение [D7]). Этот компонент:
    ///   1) гасит виртуальную лабораторию (столы-стенды), чтобы она не перекрывала комнату;
    ///   2) ставит робота на реальный стол в масштабе 1:1;
    ///   3) включает и выключает слои виртуального ПО ТУМБЛЕРАМ ([D8]): панель интерфейса,
    ///      сетка рабочей зоны, достижимость руки, виртуальные стенды. По умолчанию включена
    ///      только панель — комната оператора не зарастает слоями, которые он не просил.
    ///
    /// ЧЕСТНО О ГЛАВНОМ ОГРАНИЧЕНИИ. Виртуальная геометрия проекта НЕ метрическая:
    /// столешницы сцены — 14,4 × 9,6 юнита при высоте 0,98 юнита. Значит «поставить робота
    /// на реальный стол» требует пересчёта масштаба, и делать это наугад нельзя — получится
    /// робот размером с чайник или со шкаф. Поэтому масштаб — ЯВНАЯ настройка
    /// (<see cref="worldScale"/>), а компонент печатает в журнал фактические габариты
    /// поставленного робота, чтобы расхождение с реальностью было видно числом.
    ///
    /// ЧТО НЕ СДЕЛАНО И ПОЧЕМУ: постоянный пространственный якорь (сохранение места между
    /// запусками) — отдельный шаг. Он требует API якорей от платформы и проверки на устройстве;
    /// сейчас место робота задаётся относительно начала отсчёта трекинга, и это записано
    /// в отчёте как «ждёт железа».
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrMrRoom : MonoBehaviour
    {
        /// <summary>Камера оператора — нужна, чтобы понимать, где он находится.</summary>
        public Camera headCamera;

        /// <summary>Реальный стол оператора. По умолчанию — из решения [D7].</summary>
        public KvXrTableSize table = new KvXrTableSize(1.00f, 0.70f, 0.78f);

        /// <summary>Свободное место в комнате оператора. По умолчанию — из решения [D7].</summary>
        public KvXrRoomSize room = new KvXrRoomSize(3.0f, 2.0f);

        /// <summary>Смещение робота от центра стола, метры (ограничивается кромками стола).</summary>
        public Vector2 robotOffset = Vector2.zero;

        /// <summary>Поворот робота вокруг вертикали, градусы.</summary>
        public float robotYawDegrees = 0f;

        /// <summary>Масштаб виртуального мира. 1 — как в сцене; подбирается по факту замера.</summary>
        public float worldScale = 1f;

        /// <summary>Гасить виртуальные стенды лаборатории в MR.</summary>
        public bool hideVirtualStands = true;

        /// <summary>Радиус рабочей зоны робота для сетки и проверки «помещается ли», метры.</summary>
        public float workZoneRadiusMeters = 1.0f;

        /// <summary>Центр столешницы в мире, метры (X — поперёк, Z — вдоль).</summary>
        public Vector3 tableCenter = Vector3.zero;

        /// <summary>Робот, поставленный на стол (найден автоматически или задан вручную).</summary>
        public Transform robotOnTable;

        /// <summary>Игровой объект сетки рабочей зоны (создаётся лениво).</summary>
        private GameObject workZoneGrid;

        /// <summary>Игровой объект круга достижимости руки.</summary>
        private GameObject reachCircle;

        /// <summary>Габариты поставленного робота — для честного отчёта о масштабе.</summary>
        public Vector3 RobotSize { get; private set; }

        /// <summary>
        /// MR РЕАЛЬНО АКТИВЕН (исправлено 30.10.2026, S44).
        ///
        /// ЧТО БЫЛО. Свойство читало `KvXrSettings.GetPassthrough()` — то есть ТУМБЛЕР из
        /// `PlayerPrefs`. Это НАСТРОЙКА, а не факт: оператор мог включить «показывать комнату», а
        /// шлем (или текущая сборка) сквозного вида не дать — разрешение не выдано, рантайм не
        /// поддерживает, устройство другое.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. Настройка включена → `MrActive = true` → робот ставится «на реальный
        /// стол», виртуальные стенды гасятся, слои MR включаются. А сквозного вида нет: оператор
        /// видит ПУСТУЮ КОМНАТУ без стендов и без робота на столе. Ровно этот класс дефекта уже был
        /// в §28 («MR оказался выключенным флагом в трёх местах»).
        ///
        /// ЧТО СТАЛО. Два условия сразу: настройка ВКЛЮЧЕНА и режим, определённый по фактическим
        /// возможностям устройства, — действительно MR. Если устройство MR не даёт, приложение
        /// остаётся в VR и показывает виртуальную лабораторию, то есть работает.
        ///
        /// ПОЧЕМУ НЕ ОДНО только второе условие: если оператор ВЫКЛЮЧИЛ комнату в настройках, это
        /// его осознанный выбор, и навязывать MR нельзя. Нужны оба.
        /// </summary>
        public bool MrActive
        {
            get
            {
                if (!KvXrSettings.GetPassthrough()) return false;          // оператор выключил
                return KvXrPlatformModeSelector.Current == KvXrPlatformMode.Mr;   // устройство даёт
            }
        }

        /// <summary>
        /// ГАСИТЬ ЛИ ВИРТУАЛЬНЫЕ СТЕНДЫ. Правило вынесено отдельной чистой функцией, потому что
        /// здесь и была ошибка: прежняя строка `!hideVirtualStands || GetShowVirtualStands()`
        /// при `hideVirtualStands = true` и выключенном тумблере давала ЛОЖЬ и прятала столы
        /// В VR, где они — главное содержимое. Владелец 16.10.2026: «столов или роботов не видно».
        ///
        /// Правило: прятать ТОЛЬКО в MR, только если это задано полем и оператор не включил
        /// тумблер «виртуальные стенды» вручную. В VR стенды видны ВСЕГДА.
        /// </summary>
        public static bool HidesVirtualStands(bool mrActive, bool hideField, bool showToggle)
        {
            return mrActive && hideField && !showToggle;
        }

        /// <summary>
        /// СТАВИТЬ ЛИ РОБОТА НА «РЕАЛЬНЫЙ СТОЛ». Только в MR: в VR реального стола нет, и
        /// постановка уводила робота в НАЧАЛО КООРДИНАТ — туда, где стоит оператор, отчего
        /// робот становился невидимым (изнутри модели видны только обратные стороны граней).
        /// </summary>
        public static bool ShouldPlaceRobotOnRealTable(bool mrActive)
        {
            return mrActive;
        }

        void OnEnable()
        {
            KvXrSettingsChanged.SettingsChanged += ApplyLayers;
        }

        void OnDisable()
        {
            KvXrSettingsChanged.SettingsChanged -= ApplyLayers;
        }

        void Start()
        {
            if (headCamera == null) headCamera = Camera.main;
            if (!table.IsValid) table = KvXrMrPlacement.DefaultTable;
            if (!room.IsValid) room = KvXrMrPlacement.DefaultRoom;

            // РОБОТА СТАВИМ НА «РЕАЛЬНЫЙ СТОЛ» ТОЛЬКО В MR. В VR реального стола нет, и постановка
            // переносила робота В НАЧАЛО КООРДИНАТ — то есть туда, где стоит оператор. Робот,
            // внутри которого стоишь, не виден: изнутри модели видны только обратные стороны граней.
            // Именно это и записал журнал шлема 16.10.2026: «Робот поставлен на реальный стол:
            // центр (0,00; 0,00) м, низ 0,78 м», — при режиме «виртуальная лаборатория (VR)».
            if (ShouldPlaceRobotOnRealTable(MrActive)) PlaceRobot();
            else Debug.Log("[KvXr] Робот оставлен НА СВОЁМ СТЕНДЕ: режим не MR, реального стола нет. " +
                           "Постановка «на реальный стол» в VR увела бы робота в начало координат — " +
                           "в место, где стоит оператор.");
            ApplyLayers();

            Debug.Log("[KvXr] " + KvXrMrPlacement.Describe(table, room, workZoneRadiusMeters));
            if (!KvXrMrPlacement.VirtualGeometryIsMetric)
            {
                Debug.LogWarning("[KvXr] ЧЕСТНОЕ ОГРАНИЧЕНИЕ: виртуальная геометрия проекта не метрическая " +
                    "(столешницы 14,4 × 9,6 юнита при высоте 0,98). Постановка робота «на реальный стол» " +
                    "требует масштаба: сейчас worldScale = " + worldScale.ToString("0.000") +
                    ". Реальный стол: " + table.Describe() + ".");
            }
        }

        void Update()
        {
            // Слои — не раз в секунду, а каждый кадр: их переключает оператор из панели,
            // и задержка была бы заметна как «кнопка не работает».
            ApplyLayersIfChanged();
        }

        private int lastLayerMask = -1;

        private void ApplyLayersIfChanged()
        {
            int mask = (KvXrSettings.GetShowWorkZone() ? 1 : 0)
                     | (KvXrSettings.GetShowReach() ? 2 : 0)
                     | (KvXrSettings.GetShowVirtualStands() ? 4 : 0)
                     | (KvXrSettings.GetShowPanel() ? 8 : 0);
            if (mask == lastLayerMask) return;
            lastLayerMask = mask;
            ApplyLayers();
        }

        /// <summary>
        /// Применить слои виртуального по тумблерам ([D8]).
        /// Каждый слой создаётся ПРИ ВКЛЮЧЕНИИ и уничтожается при выключении: так включённый
        /// слой не жрёт кадры впустую, а выключенный не остаётся «призраком» в сцене.
        /// </summary>
        public void ApplyLayers()
        {
            // 1. Виртуальные стенды лаборатории гасятся ТОЛЬКО В MR, иначе они перекрывали бы
            //    реальную комнату. Это и было написано в замысле — но выполнялось ВСЕГДА.
            //
            // ДЕФЕКТ, ИСПРАВЛЕННЫЙ 16.10.2026 (§35). Прежняя строка была
            //     SetVirtualStandsVisible(!hideVirtualStands || KvXrSettings.GetShowVirtualStands());
            // При `hideVirtualStands = true` (значение поля по умолчанию) и выключенном тумблере
            // «виртуальные стенды» (его умолчание — 0) она даёт `false || false` = ЛОЖЬ, то есть
            // СТОЛЫ ПРЯЧУТСЯ И В ШЛЕМЕ В РЕЖИМЕ VR, где они и есть главное содержимое.
            // Владелец 16.10.2026: «столов или роботов не видно» — при том, что в журнале шлема
            // стенды стоят на своих местах и ничем не загорожены. Вот почему их не было видно.
            bool hideStandsInMr = HidesVirtualStands(MrActive, hideVirtualStands,
                KvXrSettings.GetShowVirtualStands());
            SetVirtualStandsVisible(!hideStandsInMr);

            // 2. Сетка рабочей зоны робота.
            if (KvXrSettings.GetShowWorkZone())
            {
                if (workZoneGrid == null) workZoneGrid = BuildWorkZoneGrid();
                workZoneGrid.SetActive(true);
                workZoneGrid.transform.position = TableTopCenter();
                float s = Mathf.Max(0.01f, worldScale);
                workZoneGrid.transform.localScale = new Vector3(s, s, s);
            }
            else if (workZoneGrid != null)
            {
                workZoneGrid.SetActive(false);
            }

            // 3. Достижимость руки — круг вокруг робота.
            if (KvXrSettings.GetShowReach())
            {
                if (reachCircle == null) reachCircle = BuildReachCircle();
                reachCircle.SetActive(true);
                reachCircle.transform.position = TableTopCenter();
                float s = Mathf.Max(0.01f, worldScale);
                reachCircle.transform.localScale = new Vector3(s, s, s);
            }
            else if (reachCircle != null)
            {
                reachCircle.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ постановка робота

        /// <summary>
        /// Найти робота в сцене и поставить его на реальный стол.
        /// Поиск идёт по компонентам-контроллерам роботов проекта (6-осевой и SCARA) —
        /// так мы не зависим от имён объектов в сцене, которые уже менялись (§0.7).
        /// </summary>
        public void PlaceRobot()
        {
            if (robotOnTable == null) robotOnTable = FindRobotRoot();
            if (robotOnTable == null)
            {
                Debug.LogWarning("[KvXr] Робот в сцене не найден — ставить на стол нечего. " +
                    "MR-сцена ожидает робота в загруженной сцене или в поле robotOnTable.");
                return;
            }

            KvXrPlacement placement = KvXrMrPlacement.PlaceRobot(
                table, robotOffset, table.HeightMeters, tableCenter, robotYawDegrees);

            float s = Mathf.Max(0.01f, worldScale);
            robotOnTable.localScale = new Vector3(s, s, s);
            robotOnTable.position = new Vector3(placement.center.x, placement.groundY, placement.center.y);
            robotOnTable.rotation = Quaternion.Euler(0f, placement.yawDegrees, 0f);

            RobotSize = MeasureSize(robotOnTable);
            Debug.Log(string.Format(
                "[KvXr] Робот поставлен на реальный стол: {0} · габариты робота {1:0.00}×{2:0.00}×{3:0.00} м · масштаб {4:0.000}",
                placement.Describe(), RobotSize.x, RobotSize.y, RobotSize.z, worldScale));
        }

        /// <summary>Габариты объекта по всем визуальным частям (без коллайдеров и служебного).</summary>
        public static Vector3 MeasureSize(Transform root)
        {
            if (root == null) return Vector3.zero;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return Vector3.zero;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.size;
        }

        private Transform FindRobotRoot()
        {
            var six = Object.FindObjectsByType<SixAxisController>(FindObjectsInactive.Include);
            if (six != null && six.Length > 0) return RootOf(six[0].transform);

            var scara = Object.FindObjectsByType<SCARAController>(FindObjectsInactive.Include);
            if (scara != null && scara.Length > 0) return RootOf(scara[0].transform);

            var generic = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            if (generic != null && generic.Length > 0) return RootOf(generic[0].transform);
            return null;
        }

        /// <summary>
        /// Корень робота: поднимаемся вверх, пока родитель тоже принадлежит роботу.
        /// Иначе «поставить робота» означало бы переставить одну деталь.
        /// </summary>
        private static Transform RootOf(Transform t)
        {
            Transform current = t;
            while (current.parent != null &&
                   (current.parent.GetComponentInChildren<SixAxisController>(true) != null ||
                    current.parent.GetComponentInChildren<SCARAController>(true) != null ||
                    current.parent.GetComponentInChildren<RobotController>(true) != null))
            {
                // Не поднимаемся в общий контейнер сцены (там окажется вся сцена).
                if (current.parent.GetComponent<SixAxisController>() != null ||
                    current.parent.GetComponent<SCARAController>() != null ||
                    current.parent.GetComponent<RobotController>() != null)
                {
                    current = current.parent;
                    continue;
                }
                break;
            }
            return current;
        }

        private void SetVirtualStandsVisible(bool visible)
        {
            // Виртуальные стенды — это объекты по имени (столы проекта строит StandBuilder).
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.parent != null) continue;          // только корневые объекты
                string name = t.name.ToLowerInvariant();
                bool isStand = name.Contains("стенд") || name.Contains("stand") || name.Contains("table");
                if (!isStand) continue;
                if (t.gameObject == gameObject) continue;
                t.gameObject.SetActive(visible);
            }
        }

        private Vector3 TableTopCenter()
        {
            return new Vector3(tableCenter.x, table.HeightMeters, tableCenter.z);
        }

        // ------------------------------------------------------------------ вспомогательные фигуры

        /// <summary>
        /// Сетка рабочей зоны: линии по краям квадрата со стороной 2·радиус. Рисуется
        /// обычными линиями (LineRenderer), а не мешем: линии в шлеме читаются лучше
        /// и не требуют отдельного шейдера.
        /// </summary>
        private GameObject BuildWorkZoneGrid()
        {
            var root = new GameObject("KvXrWorkZoneGrid");
            root.transform.SetParent(transform, false);

            const int lines = 4;
            for (int i = 0; i < lines; i++)
            {
                var segment = new GameObject("Edge" + i);
                segment.transform.SetParent(root.transform, false);
                var lr = segment.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 2;
                lr.startWidth = 0.02f;
                lr.endWidth = 0.02f;
                lr.material = MakeLineMaterial(new Color(0.15f, 0.85f, 0.35f, 0.8f));

                float r = Mathf.Max(0.1f, workZoneRadiusMeters);
                Vector3 a, b;
                switch (i)
                {
                    case 0: a = new Vector3(-r, 0f, -r); b = new Vector3(r, 0f, -r); break;
                    case 1: a = new Vector3(r, 0f, -r); b = new Vector3(r, 0f, r); break;
                    case 2: a = new Vector3(r, 0f, r); b = new Vector3(-r, 0f, r); break;
                    default: a = new Vector3(-r, 0f, r); b = new Vector3(-r, 0f, -r); break;
                }
                lr.SetPosition(0, a);
                lr.SetPosition(1, b);
            }
            return root;
        }

        /// <summary>Круг достижимости руки: 48 отрезков — глазу достаточно, кадру не тяжело.</summary>
        private GameObject BuildReachCircle()
        {
            var root = new GameObject("KvXrReachCircle");
            root.transform.SetParent(transform, false);

            const int segments = 48;
            const float radius = 1.0f;      // единичный круг, масштаб задаёт вызывающий
            var lrGo = new GameObject("Circle");
            lrGo.transform.SetParent(root.transform, false);
            var lr = lrGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            lr.startWidth = 0.015f;
            lr.endWidth = 0.015f;
            lr.material = MakeLineMaterial(new Color(0.25f, 0.65f, 1f, 0.8f));

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            return root;
        }

        /// <summary>
        /// Материал для линий — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP для шлема,
        /// HDRP для компьютера). Раньше шейдер искался как `HDRP/Unlit` с запасными вариантами:
        /// под URP это давало ровно то, от чего запас и спасал, — розовую линию. Теперь выбор
        /// делает фабрика, и запасные варианты остаются в ТОМ ЖЕ конвейере.
        /// </summary>
        private static Material MakeLineMaterial(Color color)
        {
            return KvRenderPipelineShaders.CreateUnlit("Материал_линии_MR", color);
        }

        /// <summary>Строка состояния слоёв — для панели и отчёта.</summary>
        public string DescribeLayers()
        {
            return string.Format("Слои MR: панель {0} · рабочая зона {1} · достижимость {2} · стенды {3} (масштаб {4:0.000})",
                KvXrSettings.GetShowPanel() ? "вкл" : "выкл",
                KvXrSettings.GetShowWorkZone() ? "вкл" : "выкл",
                KvXrSettings.GetShowReach() ? "вкл" : "выкл",
                KvXrSettings.GetShowVirtualStands() ? "вкл" : "выкл",
                worldScale);
        }
    }
}
