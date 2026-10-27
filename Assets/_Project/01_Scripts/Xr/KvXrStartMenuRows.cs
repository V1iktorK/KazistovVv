using System.Collections.Generic;

namespace KazistovVvXr
{
    /// <summary>
    /// СТРОКИ РАЗДЕЛОВ «СТОЛ» И «РОБОТ» (§42). Состав строк и действие по нажатию считаются ОДНОЙ
    /// функцией: раньше это были два независимых `switch` по одному разделу, и их легко было
    /// рассинхронизировать (строка «Авто стол» создавала бы не то).
    ///
    /// ПОРЯДОК: сначала существующие объекты (не больше четырёх), затем строки СОЗДАНИЯ, последней —
    /// служебная. Потолок панели — `KvXrStartMenuLayout.MaxContentRows` (7).
    /// </summary>
    public static class KvXrStartMenuRows
    {
        /// <summary>Сколько существующих объектов показываем максимум (остальные — через «Объекты»).</summary>
        public const int MaxExistingRows = 4;

        /// <summary>Строки раздела «Стол».</summary>
        public static List<string> Tables(int existingTables)
        {
            var rows = new List<string>();
            int shown = existingTables < MaxExistingRows ? existingTables : MaxExistingRows;
            for (int i = 0; i < shown; i++) rows.Add("Стол: " + (i + 1));

            rows.Add(KvXrTableKinds.Label(KvXrTableKind.Square));
            rows.Add(KvXrTableKinds.Label(KvXrTableKind.Auto));
            rows.Add("Запустить стенд (добавить стол и роботов)");
            return rows;
        }

        /// <summary>Строки раздела «Робот».</summary>
        public static List<string> Robots(int existingRobots)
        {
            var rows = new List<string>();
            int shown = existingRobots < MaxExistingRows ? existingRobots : MaxExistingRows;
            for (int i = 0; i < shown; i++) rows.Add("Робот: " + (i + 1));

            rows.Add("Добавить 6-осевой робот");
            rows.Add("Добавить SCARA");
            rows.Add("Сменить робота");
            return rows;
        }

        /// <summary>Действие строки `row` раздела `section`.</summary>
        public static KvXrStartRowAction ActionFor(KvXrStartSection section, int row,
                                                  int existingTables, int existingRobots)
        {
            if (row < 0) return KvXrStartRowAction.None;

            if (section == KvXrStartSection.Tables)
            {
                int existing = existingTables < MaxExistingRows ? existingTables : MaxExistingRows;
                if (row < existing) return KvXrStartRowAction.SelectObject;
                if (row == existing) return KvXrStartRowAction.CreateSquareTable;
                if (row == existing + 1) return KvXrStartRowAction.CreateAutoTable;
                if (row == existing + 2) return KvXrStartRowAction.RunStand;
                return KvXrStartRowAction.None;
            }

            if (section == KvXrStartSection.Robots)
            {
                int existing = existingRobots < MaxExistingRows ? existingRobots : MaxExistingRows;
                if (row < existing) return KvXrStartRowAction.SelectObject;
                if (row == existing) return KvXrStartRowAction.CreateSixAxisRobot;
                if (row == existing + 1) return KvXrStartRowAction.CreateScaraRobot;
                if (row == existing + 2) return KvXrStartRowAction.SwitchRobot;
                return KvXrStartRowAction.None;
            }

            return KvXrStartRowAction.None;
        }
    }
}
