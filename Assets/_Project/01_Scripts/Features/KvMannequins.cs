using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>
    /// МАНЕКЕНЫ ДЛЯ ПРОВЕРКИ ПЕРЕД ПУСКОМ (ФИКС 9).
    ///
    /// ЧТО БЫЛО. В проверке перед пуском «человек» — это ТОЛЬКО оператор (точка камеры/шлема).
    /// Из-за этого «траектория проходит близко к человеку» срабатывало лишь тогда, когда сам
    /// оператор стоял в рабочей зоне, а людей рядом со стендом проверка не видела вовсе.
    ///
    /// ЧТО СДЕЛАНО. В сцене появляются ДВА манекена — простые капсулы (2 шт., как в ТЗ),
    /// собранные кодом: префабы, ассеты и правка сцены не нужны. Требования ТЗ выполнены
    /// буквально:
    ///   * `HideFlags.HideInHierarchy` — манекены не мешают оператору в дереве иерархии
    ///     (и не попадают в проверку инвентаря роботов, там тот же флаг);
    ///   * коллайдер — ТРИГГЕР (`isTrigger = true`): он не толкает робота и не участвует
    ///     в физике, но объект остаётся настоящим объектом сцены, а не «картинкой»;
    ///   * позиции — рядом с рабочей зоной: 1.5 м в сторону от робота и 1.5 м в сторону
    ///     прохода между стендами, на полу (манекен стоит на уровне пола, высота 1.75 м);
    ///   * манекены можно ПЕРЕСТАВИТЬ: `SetDistance` (вынос от робота), `SetPosition`,
    ///     `Move` — и ползунком «Манекены: вынос от робота» на вкладке «Проверка перед пуском».
    ///
    /// ГЕОМЕТРИЯ СТЕНДОВ не меняется, роботы, планировщик и State Machine не затрагиваются:
    /// это отдельные объекты сцены, которые читает ТОЛЬКО проверка перед пуском.
    /// </summary>
    public class KvMannequins
    {
        /// <summary>Ключ PlayerPrefs: учитывать ли манекены в проверке (по умолчанию — да).</summary>
        public const string IncludePrefsKey = "KazistovVv.Validate.Mannequins";
        /// <summary>Ключ PlayerPrefs: вынос манекенов от робота, метры.</summary>
        public const string DistancePrefsKey = "KazistovVv.Validate.MannequinDistance";

        /// <summary>Сколько манекенов создаётся (ТЗ ФИКС 9: два).</summary>
        public const int Count = 2;

        /// <summary>Вынос манекенов от робота по умолчанию, м (ТЗ: «на 1.5 м от стендов»).</summary>
        public const float DefaultDistance = 1.5f;

        /// <summary>Высота манекена, м (капсула Unity: 2 юнита при масштабе 1).</summary>
        public const float Height = 1.75f;
        /// <summary>Диаметр манекена, м.</summary>
        public const float Diameter = 0.42f;

        public event System.Action<string> Message;

        private readonly List<Transform> roots = new List<Transform>();
        private readonly List<Vector3> offsets = new List<Vector3>();
        private RobotController robot;
        private Material material;
        private bool included = true;
        private float distance = DefaultDistance;
        private bool loaded;

        /// <summary>Учитывать ли манекены в проверке перед пуском (по умолчанию — да).</summary>
        public bool Include
        {
            get { Load(); return included; }
            set
            {
                Load();
                included = value;
                PlayerPrefs.SetInt(IncludePrefsKey, included ? 1 : 0);
                PlayerPrefs.Save();
                Report("манекены " + (included ? "учитываются" : "НЕ учитываются") +
                       " в проверке перед пуском");
            }
        }

        /// <summary>Вынос манекенов от робота, метры (0.5…4). Сохраняется в PlayerPrefs.</summary>
        public float Distance
        {
            get { Load(); return distance; }
            set
            {
                Load();
                distance = Mathf.Clamp(value, 0.5f, 4f);
                PlayerPrefs.SetFloat(DistancePrefsKey, distance);
                PlayerPrefs.Save();
                BuildOffsets();
                ApplyOffsets();
            }
        }

        /// <summary>Сколько манекенов реально создано.</summary>
        public int Created { get { return roots.Count; } }
        /// <summary>Созданы ли манекены.</summary>
        public bool Ready { get { return roots.Count == Count; } }

        /// <summary>
        /// Робот, у рабочего места которого стоят манекены (тот, которого отдал поток в момент
        /// расстановки). Нужен интерфейсу и диагностике: сравнивать позиции манекенов надо
        /// именно с НИМ, а не с «текущим» роботом потока (поток перепривязывается по прицелу).
        /// </summary>
        public RobotController RobotBase { get { return robot; } }

        /// <summary>Применить сохранённую расстановку (после смены робота или загрузки настроек).</summary>
        public void ApplyLayout()
        {
            Load();
            BuildOffsets();
            ApplyOffsets();
        }

        private void Load()
        {
            if (loaded) return;
            loaded = true;
            included = PlayerPrefs.GetInt(IncludePrefsKey, 1) != 0;                 // по умолчанию — да
            distance = Mathf.Clamp(PlayerPrefs.GetFloat(DistancePrefsKey, DefaultDistance), 0.5f, 4f);
        }

        /// <summary>
        /// Создать манекены (идемпотентно). `robotController` — робот, рядом с рабочим местом
        /// которого они ставятся; при смене робота достаточно вызвать Ensure повторно.
        ///
        /// ВАЖНО (найдено прогоном): манекены создаются **корневыми объектами сцены**, без
        /// родителя. Первая версия вешала их на корень интерфейса — и мировая позиция
        /// «уезжала» на смещение этого корня (в прогоне манекены оказались в 29 м от робота
        /// и на высоте 2 м вместо 0,88 м). Корневой объект исключает влияние положения,
        /// поворота и масштаба интерфейса; `HideInHierarchy` и так скрывает их от оператора.
        /// </summary>
        public void Ensure(RobotController robotController)
        {
            Load();
            robot = robotController;

            if (roots.Count == Count)
            {
                ApplyOffsets();
                return;
            }

            Dispose();
            for (int i = 0; i < Count; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Манекен_" + (i + 1);

                // HideInHierarchy — «служебный» объект: в дереве иерархии оператора его нет
                // (и RobotInventory его не считает роботом — там тот же флаг).
                go.hideFlags = HideFlags.HideInHierarchy;
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    if (t != null) t.gameObject.hideFlags = HideFlags.HideInHierarchy;

                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = new Vector3(Diameter, Height * 0.5f, Diameter);

                // Коллайдер — ТРИГГЕР: манекен не толкает робота и не меняет физику сцены,
                // но остаётся полноценным объектом с коллайдером (требование ФИКС 9).
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) collider.isTrigger = true;

                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (material == null)
                    {
                        material = KvRenderPipelineShaders.CreateLit("Материал_манекена", new Color(0.95f, 0.72f, 0.18f));
                        if (material != null && material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.05f);
                        if (material != null && material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.25f);
                    }
                    renderer.sharedMaterial = material;
                }

                roots.Add(go.transform);
            }

            BuildOffsets();
            ApplyOffsets();
            Report("манекенов создано: " + roots.Count +
                   " (простые капсулы, HideInHierarchy, коллайдер-триггер) · вынос от робота " +
                   distance.ToString("0.0") + " м · в проверке " +
                   (included ? "учитываются" : "не учитываются"));
        }

        /// <summary>
        /// Расстановка манекенов: по обе стороны от робота, на 1.5 м в сторону и на 1.5 м
        /// в сторону прохода между стендами (там, где реально стоят люди), на полу.
        /// </summary>
        private void BuildOffsets()
        {
            offsets.Clear();
            float towardGap = GapDirection();
            for (int i = 0; i < Count; i++)
            {
                float side = i == 0 ? 1f : -1f;
                offsets.Add(new Vector3(side * distance, 0f, towardGap * distance));
            }
        }

        /// <summary>Направление «к проходу между стендами» (+1 / −1 по мировой оси Z).</summary>
        private float GapDirection()
        {
            if (robot == null) return 1f;
            float center = (TrajectoryCore.StandBuilder.Stand1Pos.z +
                            TrajectoryCore.StandBuilder.Stand2Pos.z) * 0.5f;
            return robot.transform.position.z < center ? 1f : -1f;
        }

        private void ApplyOffsets()
        {
            if (robot == null) return;
            Vector3 basePos = robot.transform.position;
            for (int i = 0; i < roots.Count && i < offsets.Count; i++)
            {
                if (roots[i] == null) continue;
                roots[i].position = new Vector3(basePos.x + offsets[i].x, Height * 0.5f,
                                                basePos.z + offsets[i].z);
            }
        }

        /// <summary>Поставить манекен i в точку мира (ось Y — уровень пола).</summary>
        public bool SetPosition(int index, Vector3 worldPosition)
        {
            if (index < 0 || index >= roots.Count || roots[index] == null) return false;
            Vector3 basePos = robot != null ? robot.transform.position : Vector3.zero;
            roots[index].position = new Vector3(worldPosition.x, Height * 0.5f, worldPosition.z);
            offsets[index] = new Vector3(roots[index].position.x - basePos.x, 0f,
                                         roots[index].position.z - basePos.z);
            Report("манекен " + (index + 1) + " переставлен: " +
                   roots[index].position.ToString("0.00"));
            return true;
        }

        /// <summary>Сдвинуть манекен i на вектор смещения (мировые оси).</summary>
        public bool Move(int index, Vector3 delta)
        {
            if (index < 0 || index >= roots.Count || roots[index] == null) return false;
            return SetPosition(index, roots[index].position + delta);
        }

        /// <summary>Точки манекенов для проверки близости (пусто, если учёт выключен).</summary>
        public Vector3[] Positions()
        {
            Load();
            if (!included) return new Vector3[0];
            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i < roots.Count; i++)
                if (roots[i] != null) points.Add(roots[i].position);
            return points.ToArray();
        }

        /// <summary>Строка состояния для интерфейса и журнала.</summary>
        public string Status()
        {
            Load();
            if (!Ready) return "манекены не созданы";
            return "манекенов " + roots.Count + " · в проверке " +
                   (included ? "учитываются" : "НЕ учитываются") +
                   " · вынос " + distance.ToString("0.0") + " м · у робота «" +
                   (robot != null ? robot.robotName : "нет") + "» " +
                   (robot != null ? robot.transform.position.ToString("0.0") : "") +
                   " · позиции " +
                   (roots[0] != null ? roots[0].position.ToString("0.0") : "—") + " и " +
                   (roots.Count > 1 && roots[1] != null ? roots[1].position.ToString("0.0") : "—");
        }

        /// <summary>
        /// ФИКС 10. Технический отчёт о манекенах для интерфейса и диагностики: сколько создано,
        /// стоят ли они на полу, помечены ли коллайдеры как ТРИГГЕР и скрыты ли из иерархии.
        /// Именно эти четыре свойства и означают «манекены не мешают остальному»: триггер не
        /// участвует в физике и в лучах (в проекте везде `QueryTriggerInteraction.Ignore`),
        /// а `HideInHierarchy` исключает их из мира столкновений (`CollisionWorld.IsServiceObject`)
        /// и из инвентаря роботов.
        /// </summary>
        public string LayoutReport()
        {
            Load();
            if (roots.Count == 0) return "манекены не созданы";
            int triggers = 0, hidden = 0, onFloor = 0;
            for (int i = 0; i < roots.Count; i++)
            {
                if (roots[i] == null) continue;
                Collider c = roots[i].GetComponent<Collider>();
                if (c != null && c.isTrigger) triggers++;
                if ((roots[i].gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) hidden++;
                if (Mathf.Abs(roots[i].position.y - Height * 0.5f) < 0.02f) onFloor++;
            }
            return "манекенов " + roots.Count + " · коллайдеров-триггеров " + triggers +
                   " · скрыто из иерархии " + hidden + " · на полу " + onFloor +
                   " · в проверке " + (included ? "учитываются" : "НЕ учитываются");
        }

        /// <summary>
        /// Учесть ли манекены в этой проверке (для честной подписи в отчёте проверки).
        /// </summary>
        public string SourceNote()
        {
            Load();
            if (!Ready) return "манекены не созданы";
            return included
                ? "учтены манекены: " + roots.Count
                : "манекены (" + roots.Count + ") исключены переключателем";
        }

        public void Dispose()
        {
            for (int i = 0; i < roots.Count; i++)
                if (roots[i] != null) Object.Destroy(roots[i].gameObject);
            roots.Clear();
            if (material != null) Object.Destroy(material);
            material = null;
        }

        private void Report(string text)
        {
            Debug.Log("[Mannequins] " + text);
            if (Message != null) Message(text);
        }
    }
}
