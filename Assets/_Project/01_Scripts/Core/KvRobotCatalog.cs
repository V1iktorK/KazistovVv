using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KazistovVv.Core
{
    /// <summary>
    /// ОПИСАНИЕ ОДНОЙ МОДЕЛИ РОБОТА (часть 3 задачи владельца 02.11.2026).
    ///
    /// СЛОВА ВЛАДЕЛЬЦА: «роботов всех перенеси» + выбранный вариант «Выбор из списка — один
    /// активный».
    ///
    /// ЧТО ЗДЕСЬ ЛЕЖИТ. Не «загрузка файла», а ОПИСАНИЕ: как из префаба модели в `Resources`
    /// получить работающего робота. Ключевое — имена звеньев в иерархии: по ним
    /// `KvRobotSpawner` находит суставы и цепляет `SixAxisController`.
    ///
    /// ПОЧЕМУ ИМЕНА, А НЕ DH-ПАРАМЕТРЫ. В проекте обратная задача решается ОСЕВЫМ CCD
    /// (`RobotDH.DHInverse`): оси берутся из локальных осей суставов, а DH-параметры измеряются
    /// из иерархии автоматически. Значит для новой модели достаточно правильно найти шесть
    /// трансформов — всё остальное система измерения сделает сама. Это и делает перенос
    /// моделей реальным, а не «нарисуем список и пусть стоит».
    /// </summary>
    public class KvRobotModel
    {
        /// <summary>Идентификатор в каталоге (латиницей, без пробелов: «ur5», «kuka_iiwa7»).</summary>
        public string Id;

        /// <summary>Название для меню шлема.</summary>
        public string Title;

        /// <summary>Короткое описание для подписи: тип, число осей, вылет.</summary>
        public string Description;

        /// <summary>Путь к префабу в Resources (без расширения).</summary>
        public string PrefabPath;

        /// <summary>
        /// ИМЕНА СУСТАВОВ В ПОРЯДКЕ ЦЕПОЧКИ (от основания к инструменту).
        ///
        /// Берутся из URDF-моделей: у большинства импортированных роботов звенья названы
        /// `link_1…link_6` или `link0…link7`. Совпадение ищется по ТОЧНОМУ имени, копии
        /// `*_LOD*` (упрощённые меши для рендера) отбрасываются — иначе контроллер схватил бы
        /// LOD-двойника и робот дёргался бы.
        /// </summary>
        public string[] JointNames;

        /// <summary>Имя объекта инструмента (TCP). Пусто — берётся последний сустав.</summary>
        public string EndEffectorName;

        /// <summary>
        /// ОСИ ВРАЩЕНИЯ СУСТАВОВ В ИХ ЛОКАЛЬНЫХ ОСЯХ (по одному на сустав, как `JointNames`).
        ///
        /// Пусто/`null` — движок оставляет свои оси по умолчанию (так настроены импортированные
        /// URDF-модели). Заполняется там, где геометрия собрана кодом и оси известны точно:
        /// сейчас это THOR (`KvThorModel.JointAxesLocal`) — его суставы `Axis1…Axis6` стоят с
        /// локальным поворотом identity, поэтому ось в локальных координатах = мировая ось в
        /// нулевой позе. `KvRobotSpawner` применяет массив к контроллеру после создания.
        /// </summary>
        public Vector3[] JointAxesLocal;

        /// <summary>Сколько осей у модели (для подписи и проверок).</summary>
        public int Axes;

        /// <summary>Тип: «6-осевой», «SCARA», «двурукий», «5-осевой».</summary>
        public string Kind;

        public bool IsDualArm { get { return Kind == "двурукий"; } }
        public bool IsScara { get { return Kind == "SCARA"; } }
    }

    /// <summary>
    /// КАТАЛОГ ВСЕХ МОДЕЛЕЙ РОБОТОВ (часть 3, 02.11.2026).
    ///
    /// СОСТАВЛЕН ПО ФАКТУ: имена звеньев взяты из самих префабов в
    /// `Assets/Resources/Models/Robots/`, а не придуманы. Поэтому запись каталога либо работает,
    /// либо тест `KvRobotCatalogTests` падает с точным именем модели, у которой разошлись звенья.
    ///
    /// ЧТО НЕ ВОШЛО И ПОЧЕМУ:
    ///   • `moveo`, `so_arm100` — модели есть, но имена звеньев не образуют явной цепочки
    ///     (артикуляции, сервоприводы). Оставлены на потом: сначала те, что заведомо
    ///     работоспособны, иначе «перенос всех» превратится в список неработающих кнопок.
    ///
    /// THOR — ОСНОВНОЙ РОБОТ (17.11.2026). Раньше `thor` здесь был исключён: в репозитории
    /// лежала механическая сборка (шестерни, натяжители), а не шестиосевой манипулятор. Теперь
    /// владелец собирает настоящего Thor (github.com/AngelLM/Thor), и в каталоге появилась его
    /// МОДЕЛЬ: иерархия `Axis1…Axis6`, оси и длины звеньев — из `KvThorModel`, геометрия — из
    /// официальных мешей сборки FreeCAD (`KvThorPrefabBuilder`). Thor стоит ПЕРВЫМ в списке и
    /// является моделью по умолчанию — это решение владельца, зафиксированное здесь же.
    /// </summary>
    public static class KvRobotCatalog
    {
        private static KvRobotModel[] all;

        /// <summary>Все модели в порядке показа в меню.</summary>
        public static KvRobotModel[] All
        {
            get
            {
                if (all == null) all = Build();
                return all;
            }
        }

        private static KvRobotModel[] Build()
        {
            return new[]
            {
                // --- ОСНОВНОЙ РОБОТ ПРОГРАММЫ (17.11.2026): THOR первым.
                //
                // Владелец собирает робота THOR и хочет видеть его в программе главным: и в
                // стартовом меню шлема, и в верстаке, и в списке моделей. Поэтому запись идёт
                // первой (порядок = порядок показа в меню), а `Default` ниже указывает на неё.
                new KvRobotModel
                {
                    Id = KvThorModel.Id,
                    Title = KvThorModel.Title,
                    Description = KvThorModel.Description,
                    PrefabPath = "Resources/Models/Robots/thor",
                    JointNames = KvThorModel.JointNames,
                    EndEffectorName = KvThorModel.EndEffectorName,
                    JointAxesLocal = KvThorModel.JointAxesLocal,
                    Axes = 6,
                    Kind = "6-осевой"
                },

                // --- БАЗОВЫЕ: уже работают в проекте, остаются первыми, чтобы владелец
                //     не искал «своего» робота в конце списка.
                new KvRobotModel
                {
                    Id = "stand_6axis",
                    Title = "6-осевой (стандартный)",
                    Description = "6 осей · текущая модель проекта",
                    PrefabPath = "Resources/Models/Robots/stand_6axis",
                    JointNames = new[] { "Axis1", "Axis2", "Axis3", "Axis4", "Axis5", "Axis6" },
                    EndEffectorName = "Robot",
                    Axes = 6,
                    Kind = "6-осевой"
                },

                new KvRobotModel
                {
                    Id = "scara_ls10b702s",
                    Title = "SCARA LS10-B702S",
                    Description = "4 оси · плоскостной манипулятор",
                    PrefabPath = "Resources/Models/Robots/scara_ls10b702s",
                    JointNames = new[] { "LS10-B702S_J1_3", "LS10-B702S_J2_4", "LS10-B702S_z_5" },
                    EndEffectorName = "LS10-B702S_z_5",
                    Axes = 3,
                    Kind = "SCARA"
                },

                // --- ПРОМЫШЛЕННЫЕ 6-ОСЕВЫЕ (URDF-имена link_1…link_6)
                new KvRobotModel
                {
                    Id = "ur5",
                    Title = "Universal Robots UR5",
                    Description = "6 осей · вылет ~0,85 м",
                    PrefabPath = "Resources/Models/Robots/ur5",
                    JointNames = new[]
                    {
                        "shoulder_link", "upper_arm_link", "forearm_link",
                        "wrist_1_link", "wrist_2_link", "wrist_3_link"
                    },
                    EndEffectorName = "ft_frame",
                    Axes = 6,
                    Kind = "6-осевой"
                },

                new KvRobotModel
                {
                    Id = "kuka_iiwa7",
                    Title = "KUKA iiwa 7",
                    Description = "7 осей · вылет ~0,80 м",
                    PrefabPath = "Resources/Models/Robots/kuka_iiwa7",
                    JointNames = new[]
                    {
                        "iiwa_link_1", "iiwa_link_2", "iiwa_link_3", "iiwa_link_4",
                        "iiwa_link_5", "iiwa_link_6", "iiwa_link_7"
                    },
                    EndEffectorName = "iiwa_link_ee",
                    Axes = 7,
                    Kind = "6-осевой"
                },

                new KvRobotModel
                {
                    Id = "kuka_iiwa14",
                    Title = "KUKA iiwa 14",
                    Description = "7 осей · вылет ~0,82 м",
                    PrefabPath = "Resources/Models/Robots/kuka_iiwa14",
                    JointNames = new[]
                    {
                        "link1", "link2", "link3", "link4", "link5", "link6", "link7"
                    },
                    EndEffectorName = "",
                    Axes = 7,
                    Kind = "6-осевой"
                },

                new KvRobotModel
                {
                    Id = "abb_irb6640",
                    Title = "ABB IRB 6640",
                    Description = "6 осей · тяжёлый промышленный",
                    PrefabPath = "Resources/Models/Robots/abb_irb6640",
                    JointNames = new[]
                    {
                        "link_1", "link_2", "link_3", "link_4", "link_5", "link_6"
                    },
                    EndEffectorName = "tool0",
                    Axes = 6,
                    Kind = "6-осевой"
                },

                new KvRobotModel
                {
                    Id = "fanuc_m20ib",
                    Title = "FANUC M-20iB",
                    Description = "6 осей · вылет ~1,8 м",
                    PrefabPath = "Resources/Models/Robots/fanuc_m20ib",
                    JointNames = new[]
                    {
                        "link_1", "link_2", "link_3", "link_4", "link_5", "link_6"
                    },
                    EndEffectorName = "",
                    Axes = 6,
                    Kind = "6-осевой"
                },

                new KvRobotModel
                {
                    Id = "franka_fr3",
                    Title = "Franka FR3",
                    Description = "7 осей · вылет ~0,86 м",
                    PrefabPath = "Resources/Models/Robots/franka_fr3",
                    JointNames = new[]
                    {
                        "fr3_link0", "fr3_link1", "fr3_link2", "fr3_link3",
                        "fr3_link4", "fr3_link5", "fr3_link6", "fr3_link7"
                    },
                    EndEffectorName = "fr3_link7",
                    Axes = 7,
                    Kind = "6-осевой"
                },

                new KvRobotModel
                {
                    Id = "franka_fr3_v2",
                    Title = "Franka FR3 (v2)",
                    Description = "7 осей · вылет ~0,86 м",
                    PrefabPath = "Resources/Models/Robots/franka_fr3_v2",
                    JointNames = new[]
                    {
                        "fr3v2_link0", "fr3v2_link1", "fr3v2_link2", "fr3v2_link3",
                        "fr3v2_link4", "fr3v2_link5", "fr3v2_link6", "fr3v2_link7"
                    },
                    EndEffectorName = "fr3v2_link8",
                    Axes = 7,
                    Kind = "6-осевой"
                },

                new KvRobotModel
                {
                    Id = "kinova_gen3",
                    Title = "Kinova Gen3",
                    Description = "7 осей · вылет ~0,90 м",
                    PrefabPath = "Resources/Models/Robots/kinova_gen3",
                    JointNames = new[]
                    {
                        "shoulder_link", "half_arm_1_link", "half_arm_2_link",
                        "forearm_link", "spherical_wrist_1_link",
                        "spherical_wrist_2_link", "bracelet_link"
                    },
                    EndEffectorName = "bracelet_link",
                    Axes = 7,
                    Kind = "6-осевой"
                },

                new KvRobotModel
                {
                    Id = "comau_edo",
                    Title = "COMAU e.DO",
                    Description = "6 осей · настольный",
                    PrefabPath = "Resources/Models/Robots/comau_edo",
                    JointNames = new[]
                    {
                        "link_1", "link_2", "link_3", "link_4", "link_5", "link_6"
                    },
                    EndEffectorName = "",
                    Axes = 6,
                    Kind = "6-осевой"
                },

                // --- ДВУРУКИЙ (часть 4 задачи)
                new KvRobotModel
                {
                    Id = "openarm",
                    Title = "OpenArm (две руки)",
                    Description = "2 × 7 осей · двурукий",
                    PrefabPath = "Resources/Models/Robots/openarm",
                    JointNames = new string[0],   // см. KvDualArmController: две отдельные цепочки
                    EndEffectorName = "",
                    Axes = 14,
                    Kind = "двурукий"
                }
            };
        }

        /// <summary>Модель по id (null — нет такой).</summary>
        public static KvRobotModel Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (KvRobotModel m in All)
                if (string.Equals(m.Id, id, StringComparison.Ordinal)) return m;
            return null;
        }

        /// <summary>
        /// МОДЕЛЬ ПО УМОЛЧАНИЮ — THOR (решение владельца 17.11.2026: «робот Thor — основной»).
        ///
        /// Раньше здесь стоял `stand_6axis` — робот, который физически стоит в сцене проекта.
        /// Теперь основной робот тот, что владелец собирает руками: THOR. Значение — ссылка на
        /// ЗАПИСЬ КАТАЛОГА (а не копия), поэтому поиск, меню и спавнер видят одну и ту же модель.
        /// </summary>
        public static KvRobotModel Default { get { return Find(KvThorModel.Id); } }

        /// <summary>Имена звеньев модели строкой БЕЗ КОПИЙ LOD — ровно то, что ищет `KvRobotSpawner`.
        ///
        /// Вынесено отдельным методом, чтобы генератор отчётов и тесты видели ТУ ЖЕ фильтрацию,
        /// что и рантайм: иначе тест мог бы «найти» звено, которого рантайм не видит.
        /// </summary>
        public static string[] JointNamesOf(KvRobotModel model)
        {
            return model != null && model.JointNames != null ? model.JointNames : new string[0];
        }

        // ============================================================================================
        // ДОСТУПНОСТЬ МОДЕЛИ: ЕСТЬ ЛИ У НЕЁ ГЕОМЕТРИЯ (найдено тестом сессии S46, 12.11.2026).
        //
        // ЧТО БЫЛО. Каталог отвечал на вопрос «какие модели мы перенесли» списком из 12 имён, и
        // интерфейс предлагал их ВСЕ как рабочие. Фактическая проверка показала: у 11 из 12 моделей
        // ссылки `m_Mesh` в префабах ведут на меши, которых в проекте НЕТ (в репозитории всего два
        // FBX роботов: `Assets/Robots/Robot.fbx` и `Assets/Robots/ScaraRobot.fbx`). Геометрия
        // разрешается только у `stand_6axis`.
        //
        // ЧЕМ ЭТО ПЛОХО ДЛЯ ОПЕРАТОРА. «Выбрал UR5» → `KvRobotSpawner.Create` создаёт объект с
        // суставами, контроллером и именем, но НИ ОДНОГО меша: на столе и в шлеме — ничего.
        // Для оператора это ровно «нажал, и ничего не произошло», то есть нарушение требования
        // владельца «каждая кнопка выполняет реальную работу». Хуже: `ApplyModel` и
        // `KvArContent.SpawnModel` сначала УДАЛЯЮТ текущего робота, поэтому выбор недоступной
        // модели уносил рабочего робота и не приводил на его место никакого.
        //
        // ЧТО СТАЛО. Каталог отвечает на вопрос «можно ли эту модель ПОКАЗАТЬ» отдельно от вопроса
        // «есть ли она в списке»: `HasGeometry` / `Available` / `Unavailable`. Основание — ФАКТ
        // разрешения мешей префаба (`KvRobotSpawner.LoadPrefab` + `MeshFilter.sharedMesh != null`),
        // а не ручной список в коде: починят модели — каталог сам станет «доступным», без правок.
        //
        // ПОЧЕМУ С КЭШЕМ. `HasGeometry` спрашивается на отрисовке списка моделей и меню левой руки,
        // а обход иерархии префаба в каждом кадре — удар по бюджету 72 Гц (раздел P2). Кэш
        // сбрасывается `InvalidateAvailability()` — после импорта или исправления моделей.
        // ============================================================================================

        /// <summary>Сколько мешей с разрешаемым ассетом в префабе модели (0 — геометрии нет).</summary>
        private static readonly Dictionary<string, int> meshCountCache = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// СКОЛЬКО В МОДЕЛИ МЕШЕЙ, КОТОРЫЕ РЕАЛЬНО РАЗРЕШАЮТСЯ. Ноль означает «модель невидима»:
        /// суставы и контроллер будут, а нарисовать нечего.
        ///
        /// Считается по `MeshFilter.sharedMesh != null` у ВСЕХ потомков (включая выключенные: в
        /// префабах роботов часть звеньев выключена и включается контроллером). Кэш — по пути префаба,
        /// потому что именно он определяет результат (`KvRobotModel` может быть собран и вручную).
        /// </summary>
        public static int GeometryMeshCount(KvRobotModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.PrefabPath)) return 0;

            int cached;
            if (meshCountCache.TryGetValue(model.PrefabPath, out cached)) return cached;

            int meshes = 0;
            GameObject prefab = KvRobotSpawner.LoadPrefab(model);
            if (prefab != null)
            {
                MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i] == null) continue;
                    if (filters[i].sharedMesh != null) meshes++;
                }
            }

            meshCountCache[model.PrefabPath] = meshes;
            return meshes;
        }

        /// <summary>
        /// МОЖНО ЛИ ПОКАЗЫВАТЬ МОДЕЛЬ КАК РАБОЧУЮ. Ложь означает «выбор ничего не даст»: робот
        /// создастся без единого меша, то есть не появится.
        /// </summary>
        public static bool HasGeometry(KvRobotModel model)
        {
            return GeometryMeshCount(model) > 0;
        }

        /// <summary>Модели, которые РЕАЛЬНО можно поставить в сцену (есть геометрия).</summary>
        public static KvRobotModel[] Available { get { return WithGeometry(true); } }

        /// <summary>
        /// Модели каталога БЕЗ геометрии: их НЕЛЬЗЯ предлагать как рабочие. Список публичный, чтобы
        /// долг был виден (журнал, отчёт, панель диагностики), а не спрятан внутри проверки.
        /// </summary>
        public static KvRobotModel[] Unavailable { get { return WithGeometry(false); } }

        private static KvRobotModel[] WithGeometry(bool hasGeometry)
        {
            var result = new List<KvRobotModel>();
            KvRobotModel[] all = All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (HasGeometry(all[i]) == hasGeometry) result.Add(all[i]);
            }
            return result.ToArray();
        }

        /// <summary>
        /// СБРОСИТЬ КЭШ ДОСТУПНОСТИ — после импорта, починки модели или в тестах. Результат зависит
        /// от содержимого проекта, поэтому «навсегда запомнить» его нельзя.
        /// </summary>
        public static void InvalidateAvailability()
        {
            meshCountCache.Clear();
        }

        /// <summary>Строка для журнала: сколько моделей и какие.</summary>
        public static string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("моделей роботов: ").Append(All.Length).Append(" · ");
            for (int i = 0; i < All.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(All[i].Title);
            }
            return sb.ToString();
        }

        /// <summary>
        /// ЧЕСТНАЯ СТРОКА О ГЕОМЕТРИИ: сколько моделей можно показать и какие нельзя.
        ///
        /// Нужна там, где раньше молча печаталось «моделей 12»: 12 моделей в каталоге и 12
        /// РАБОТАЮЩИХ — разные вещи, и разница здесь одиннадцать. Имя отличается от
        /// `KvRobotSpawner.DescribeAvailability` (та строка про НАЛИЧИЕ ПРЕФАБА, эта — про меши).
        /// </summary>
        public static string DescribeGeometry()
        {
            KvRobotModel[] available = Available;
            KvRobotModel[] unavailable = Unavailable;

            var sb = new System.Text.StringBuilder();
            sb.Append("моделей ").Append(All.Length)
              .Append(" · с геометрией ").Append(available.Length)
              .Append(" · недоступны ").Append(unavailable.Length);
            if (unavailable.Length > 0)
            {
                sb.Append(" (");
                for (int i = 0; i < unavailable.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(unavailable[i].Id);
                }
                sb.Append(") — их префабы ссылаются на меши, которых нет в проекте");
            }
            return sb.ToString();
        }
    }
}
