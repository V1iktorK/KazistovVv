namespace KazistovVvXr
{
    /// <summary>ЧТО ДЕЛАЕТ СТРОКА СТАРТОВОГО МЕНЮ (§42).</summary>
    public enum KvXrStartRowAction
    {
        /// <summary>Ничего (строка ещё не размечена).</summary>
        None = 0,

        /// <summary>Выбрать существующий объект сцены (стол или робота).</summary>
        SelectObject = 1,

        /// <summary>Создать квадратный стол 0,72 × 0,48 м.</summary>
        CreateSquareTable = 2,

        /// <summary>Создать круглый «авто» стол.</summary>
        CreateAutoTable = 3,

        /// <summary>Создать 6-осевого робота.</summary>
        CreateSixAxisRobot = 4,

        /// <summary>Создать SCARA.</summary>
        CreateScaraRobot = 5,

        /// <summary>Существующая строка «Запустить стенд (добавить стол и роботов)».</summary>
        RunStand = 6,

        /// <summary>Существующая строка «Сменить робота».</summary>
        SwitchRobot = 7
    }
}
