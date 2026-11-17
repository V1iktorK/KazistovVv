using UnityEngine;

namespace KazistovVv.Core
{
    /// <summary>
    /// THOR — ОСНОВНОЙ РОБОТ ПРОГРАММЫ (задача владельца 17.11.2026).
    ///
    /// ЧТО ЭТО ЗА РОБОТ. Thor (Ángel LM, github.com/AngelLM/Thor) — открытый 6-осевой
    /// манипулятор, собранный из 3D-печатных деталей. Владелец собирает ЕГО физически, поэтому
    /// в программе Thor — первый и выбранный по умолчанию робот, а «6-осевой (стандартный)»
    /// и SCARA становятся второстепенными.
    ///
    /// ОТКУДА ЧИСЛА (параметрический описатель — см. блок длин). Все размеры взяты из
    /// ОФИЦИАЛЬНЫХ исходников проекта Thor, лежащих в репозитории:
    ///   • `ModelSources/Thor/Thor-main/doc/ThorDimensions.png` — чертёж
    ///     «THOR — AXIS MEASURES» (автор ANGEL L.M., FreeCAD, 16.07.2016): 202 / 160 / 195 /
    ///     67,15 мм;
    ///   • `ModelSources/Thor/Thor-main/README.md` — «Height Stretched: 625mm»,
    ///     конфигурация yaw-roll-roll-yaw-roll-yaw, payload 750 г.
    ///   (Исходники лежат ВНЕ `Assets` — бюджет APK, см. `StepSourcesFolder`.)
    ///
    /// ЗАЧЕМ ОПИСАТЕЛЬ, А НЕ ЧИСЛА В КОДЕ СПАВНЕРА. Длины звеньев нужны в ЧЕТЫРЁХ местах:
    /// в префабе (геометрия), в осевой CCD (оси суставов), в тесте прямой кинематики (эталонные
    /// позы) и в подписи модели. Все они читают ОДИН источник — этот класс, поэтому замена
    /// меша на точный (или правка размера по факту сборки) делается в одном месте.
    ///
    /// ОТКУДА ГЕОМЕТРИЯ. Меш собран из ОФИЦИАЛЬНОЙ сборки FreeCAD (`freecad-src/Assembly.FCStd`)
    /// и выгружен в OBJ (`meshes/*.obj`, миллиметры): основание, колонна, плечо, предплечье,
    /// запястье, манипулятор. Путь к STEP-исходникам — `Thor-main/step/`, они лежат в проекте
    /// для будущей замены меша без переделки иерархии (см. `MeshTable`).
    ///
    /// КИНЕМАТИКА — тот же шестиосевой движок проекта (`SixAxisController` + осевая CCD из
    /// `RobotDH`): суставы `Axis1…Axis6`, оси вращения заданы в `JointAxesLocal` и применяются
    /// `KvRobotSpawner` при создании. Ничего нового в движок не добавлено.
    /// </summary>
    public static class KvThorModel
    {
        /// <summary>Идентификатор Thor в каталоге `KvRobotCatalog`.</summary>
        public const string Id = "thor";

        /// <summary>Название для меню и журнала (буквы — как у производителя).</summary>
        public const string Title = "THOR";

        /// <summary>Подпись модели: тип, число осей, вылет — для карточки в меню.</summary>
        public const string Description = "6 осей · открытый 3D-печатный · вылет ~0,62 м";

        // ========================================================================================
        // ПАРАМЕТРИЧЕСКИЙ ОПИСАТЕЛЬ: ДЛИНЫ ЗВЕНЬЕВ, МЕТРЫ.
        //
        // Ноль системы координат — ЦЕНТР НИЖНЕЙ ПЛОСКОСТИ ОСНОВАНИЯ (то, что стоит на столе).
        // Ось Y — вверх (конвенция Unity). Нулевая поза — робот ВЫТЯНУТ ВВЕРХ, как на чертеже
        // «Axis Measures»: тогда все позы теста FK считаются вручную без интерпретации.
        // ========================================================================================

        /// <summary>Основание → ось плеча, м (чертёж: 202 мм).</summary>
        public const float BaseToShoulderM = 0.202f;

        /// <summary>Плечо (ось плеча → ось локтя), м (чертёж: 160 мм).</summary>
        public const float UpperArmM = 0.160f;

        /// <summary>Предплечье (ось локтя → ось запястья), м (чертёж: 195 мм).</summary>
        public const float ForearmM = 0.195f;

        /// <summary>Запястье → фланец (TCP), м (чертёж: 67,15 мм).</summary>
        public const float WristToTcpM = 0.06715f;

        /// <summary>
        /// Локоть → ось четвёртой оси (yaw колонны запястья), м.
        /// Замер по официальной сборке FreeCAD: `AssemblyArt4` стоит в 444,5 мм при локте в
        /// 362 мм → 82,5 мм. Пятая и шестая оси совпадают (сферическое запястье), поэтому
        /// отдельная длина до них не нужна — это `ForearmM`.
        /// </summary>
        public const float ElbowToJ4M = 0.0825f;

        /// <summary>Высота вытянутого робота до фланца, м: 0,202 + 0,160 + 0,195 + 0,06715 = 0,62415.</summary>
        public static float StretchedHeightM
        {
            get { return BaseToShoulderM + UpperArmM + ForearmM + WristToTcpM; }
        }

        // ========================================================================================
        // ПОЗИЦИИ СУСТАВОВ В НУЛЕВОЙ ПОЗЕ (мировые, относительно основания).
        // ========================================================================================

        /// <summary>Ось плеча (J2).</summary>
        public static Vector3 ShoulderPos
        {
            get { return new Vector3(0f, BaseToShoulderM, 0f); }
        }

        /// <summary>Ось локтя (J3).</summary>
        public static Vector3 ElbowPos
        {
            get { return new Vector3(0f, BaseToShoulderM + UpperArmM, 0f); }
        }

        /// <summary>Ось J4 (yaw предплечья).</summary>
        public static Vector3 WristColumnPos
        {
            get { return new Vector3(0f, BaseToShoulderM + UpperArmM + ElbowToJ4M, 0f); }
        }

        /// <summary>Ось J5/J6 (сферическое запястье) — точка крепления манипулятора.</summary>
        public static Vector3 WristPos
        {
            get { return new Vector3(0f, BaseToShoulderM + UpperArmM + ForearmM, 0f); }
        }

        /// <summary>Точка TCP (фланец) в нулевой позе.</summary>
        public static Vector3 TcpPos
        {
            get { return new Vector3(0f, StretchedHeightM, 0f); }
        }

        // ========================================================================================
        // ОСИ ВРАЩЕНИЯ СУСТАВОВ В ИХ ЛОКАЛЬНЫХ ОСЯХ (то, что читает `SixAxisController`).
        //
        // Суставы стоят с ЛОКАЛЬНЫМ поворотом identity в нулевой позе, поэтому локальная ось =
        // мировая ось в нулевой позе:
        //   J1 — вертикаль (yaw базы), J2/J3 — поперёк плечо-локоть (маятник руки),
        //   J4 — ВДОЛЬ предплечья (yaw запястья), J5 — поперёк запястья, J6 — вдоль инструмента.
        // Конфигурация Thor (yaw-roll-roll-yaw-roll-yaw) из README: J4 ∥ J1 ∥ J6, J2 ∥ J3 ∥ J5.
        // ========================================================================================

        /// <summary>Оси вращения шести суставов в локальных координатах (`Axis1…Axis6`).</summary>
        public static readonly Vector3[] JointAxesLocal =
        {
            Vector3.up,       // J1 — yaw базы
            Vector3.forward,  // J2 — плечо
            Vector3.forward,  // J3 — локоть
            Vector3.up,       // J4 — yaw предплечья (вдоль оси)
            Vector3.forward,  // J5 — pitch запястья
            Vector3.up        // J6 — roll фланца (вдоль инструмента)
        };

        /// <summary>Имена суставов в порядке цепочки (те, что ищет `KvRobotSpawner`).</summary>
        public static readonly string[] JointNames =
        {
            "Axis1", "Axis2", "Axis3", "Axis4", "Axis5", "Axis6"
        };

        /// <summary>Имя объекта инструмента (TCP) в иерархии префаба.</summary>
        public const string EndEffectorName = "TCP";

        // ========================================================================================
        // ТАБЛИЦА МЕШЕЙ: КАКОЙ ФАЙЛ НА КАКОЕ ЗВЕНО ВЕШАЕТСЯ.
        //
        // Файлы лежат в `Assets/_Project/99_ModelSources/Thor/meshes/` (импорт Unity, OBJ).
        // Все меши выгружены из официальной сборки FreeCAD В МИРОВЫХ координатах (миллиметры,
        // пивот сборки в нуле), поэтому объект меша получает СДВИГ `−позиция сустава` и масштаб
        // 0,001 (мм → м), а поворот — identity. Заменить меш на более точный (или на другой
        // экспорт) можно, положив файл с тем же именем — иерархия и кинематика не меняются.
        // ========================================================================================

        /// <summary>Одна строка таблицы: файл меша + индекс сустава-родителя (−1 — корень).</summary>
        public struct MeshEntry
        {
            public string ResourceName;   // имя файла в папке meshes (без расширения)
            public int ParentJointIndex;  // 0..5 — AxisN, −1 — корень робота

            public MeshEntry(string resourceName, int parentJointIndex)
            {
                ResourceName = resourceName;
                ParentJointIndex = parentJointIndex;
            }
        }

        /// <summary>Шесть звеньев официальной сборки Thor.</summary>
        public static readonly MeshEntry[] MeshTable =
        {
            new MeshEntry("base",  -1),   // неподвижное основание
            new MeshEntry("art1",   0),   // вращающаяся колонна (Axis1)
            new MeshEntry("art2",   1),   // плечо (Axis2)
            new MeshEntry("art3",   2),   // предплечье (Axis3)
            new MeshEntry("art4",   3),   // колонна запястья (Axis4)
            new MeshEntry("art56",  5)    // запястье + манипулятор (Axis6)
        };

        /// <summary>Путь к папке с мешами (от корня Assets), для редактора и диагностики.</summary>
        public const string MeshFolder = "Assets/_Project/99_ModelSources/Thor/meshes";

        /// <summary>
        /// Путь к официальным исходникам Thor — STEP/STL/FreeCAD ИСТОЧНИКИ КОНВЕРТАЦИИ.
        ///
        /// Лежат ВНЕ `Assets` (корень проекта `ModelSources/Thor/`), потому что проект меряет
        /// бюджет `Assets` под APK шлема (`KvBuildBudgetTests`: меньше 400 МБ и без файлов
        /// крупнее 15 МБ), а одна только сборка FreeCAD весит около 200 МБ. В `Assets` остаются
        /// РОВНО те файлы, которыми пользуется рантайм: меши `meshes/*.obj`.
        /// </summary>
        public const string StepSourcesFolder = "ModelSources/Thor/Thor-main/step";
    }
}
