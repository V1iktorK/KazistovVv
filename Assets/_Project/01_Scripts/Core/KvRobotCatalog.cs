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
    ///   • `thor` — это НЕ манипулятор, а механическая сборка (шестерни, натяжители, корпуса):
    ///     звеньев вида `Art56SmallGear` там сотни, кинематической цепочки из шести осей нет.
    ///     Поставить его в список «выбери робота» — значит дать кнопку, которая не работает.
    ///   • `moveo`, `so_arm100` — модели есть, но имена звеньев не образуют явной цепочки
    ///     (артикуляции, сервоприводы). Оставлены на потом: сначала те, что заведомо
    ///     работоспособны, иначе «перенос всех» превратится в список неработающих кнопок.
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

        /// <summary>Модель по умолчанию — та, что стоит в сцене сейчас.</summary>
        public static KvRobotModel Default { get { return Find("stand_6axis"); } }

        /// <summary>
        /// Имена звеньев модели строкой БЕЗ КОПИЙ LOD — ровно то, что ищет `KvRobotSpawner`.
        ///
        /// Вынесено отдельным методом, чтобы генератор отчётов и тесты видели ТУ ЖЕ фильтрацию,
        /// что и рантайм: иначе тест мог бы «найти» звено, которого рантайм не видит.
        /// </summary>
        public static string[] JointNamesOf(KvRobotModel model)
        {
            return model != null && model.JointNames != null ? model.JointNames : new string[0];
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
    }
}
