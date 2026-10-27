using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>РАЗДЕЛЫ СТАРТОВОГО МЕНЮ (указание владельца 24.10.2026).</summary>
    public enum KvXrStartSection
    {
        /// <summary>Столы: выбор стола, на котором стоит робот.</summary>
        Tables = 0,

        /// <summary>Роботы: выбор робота.</summary>
        Robots = 1,

        /// <summary>Проект: то же, что в стартовом меню компьютера (сессия, демо, сброс).</summary>
        Project = 2,

        /// <summary>Обучение.</summary>
        Tutorial = 3,

        /// <summary>Настройки: шлема и интерфейса компьютера.</summary>
        Settings = 4,

        /// <summary>Выход и возврат к работе.</summary>
        Exit = 5
    }

    /// <summary>
    /// ГЕОМЕТРИЯ СТАРТОВОГО МЕНЮ — ЧИСТАЯ АРИФМЕТИКА В МЕТРАХ, БЕЗ UNITY-ОБЪЕКТОВ.
    ///
    /// ВИД (слова владельца): «похожим на оверлей SteamOS последней версии или как сами настройки
    /// PICO, скорее второе» — то есть плоская панель перед лицом, СЛЕВА список разделов, СПРАВА
    /// содержимое выбранного раздела строками. Никакой дуги и никаких иконок-кружочков: это меню,
    /// а не панель инструментов у руки.
    ///
    /// РАЗМЕРЫ. Панель 0,90 × 0,60 м; строка раздела 0,24 × 0,056 м; строка содержимого
    /// 0,57 × 0,052 м. Числа выбраны так, чтобы строка ловилась лучом по ВСЕЙ площади (дефект Д1:
    /// раньше попадание считалось кругом 25 мм в центре, и широкие кнопки «не нажимались»).
    ///
    /// Почему всё это отдельным классом: раскладку можно проверить тестом без шлема — «помещается
    /// ли», «не налезают ли строки», «на том ли месте раздел» ([A2] шаг 3).
    /// </summary>
    public static class KvXrStartMenuLayout
    {
        /// <summary>Ширина панели меню, метры.</summary>
        public const float WidthMeters = 0.90f;

        /// <summary>Высота панели меню, метры.</summary>
        public const float HeightMeters = 0.60f;

        /// <summary>Сколько разделов в левой колонке.</summary>
        public const int SectionCount = 6;

        /// <summary>Сколько строк содержимого помещается справа.</summary>
        public const int MaxContentRows = 7;

        /// <summary>Половина размера строки раздела, метры.</summary>
        public static readonly Vector2 SectionHalfSize = new Vector2(0.115f, 0.026f);

        /// <summary>Половина размера строки содержимого, метры.</summary>
        public static readonly Vector2 ContentHalfSize = new Vector2(0.285f, 0.026f);

        /// <summary>Где стоит колонка разделов по X, метры от центра панели.</summary>
        public const float SectionColumnX = -0.30f;

        /// <summary>Где стоят строки содержимого по X, метры от центра панели.</summary>
        public const float ContentColumnX = 0.135f;

        /// <summary>Верхняя строка разделов, метры от центра панели.</summary>
        public const float SectionTopY = 0.215f;

        /// <summary>Шаг строк разделов, метры.</summary>
        public const float SectionStepY = 0.068f;

        /// <summary>Верхняя строка содержимого, метры от центра панели.</summary>
        public const float ContentTopY = 0.215f;

        /// <summary>Шаг строк содержимого, метры.</summary>
        public const float ContentStepY = 0.070f;

        /// <summary>Название раздела — как оно написано в меню.</summary>
        public static string SectionTitle(int index)
        {
            switch (Normalize(index))
            {
                case KvXrStartSection.Tables: return "Стол";
                case KvXrStartSection.Robots: return "Робот";
                case KvXrStartSection.Project: return "Проект";
                case KvXrStartSection.Tutorial: return "Обучение";
                case KvXrStartSection.Settings: return "Настройки";
                default: return "Выход";
            }
        }

        /// <summary>Иконка раздела — из уже существующего набора `KvIcons` (новых картинок не заводим).</summary>
        public static string SectionIcon(int index)
        {
            switch (Normalize(index))
            {
                case KvXrStartSection.Tables: return "table";
                case KvXrStartSection.Robots: return "robot";
                case KvXrStartSection.Project: return "session";
                case KvXrStartSection.Tutorial: return "help";
                case KvXrStartSection.Settings: return "settings";
                default: return "close";
            }
        }

        /// <summary>Центр строки раздела на панели, метры.</summary>
        public static Vector2 SectionCenter(int index)
        {
            return new Vector2(SectionColumnX, SectionTopY - (int)Normalize(index) * SectionStepY);
        }

        /// <summary>Центр строки содержимого на панели, метры.</summary>
        public static Vector2 ContentCenter(int row)
        {
            return new Vector2(ContentColumnX, ContentTopY - Mathf.Clamp(row, 0, MaxContentRows - 1) * ContentStepY);
        }

        /// <summary>Раздел по номеру (с зажимом в допустимый диапазон).</summary>
        public static KvXrStartSection Normalize(int index)
        {
            return (KvXrStartSection)Mathf.Clamp(index, 0, SectionCount - 1);
        }

        /// <summary>
        /// ВЕСЬ ЛИ РАЗДЕЛ В ПАНЕЛИ: ни одна строка не вылезает за её край. Кнопка, вылезшая за панель, —
        /// это кнопка, до которой не дотянуться лучом.
        /// </summary>
        public static bool FitsPanel()
        {
            for (int i = 0; i < SectionCount; i++)
                if (!InsidePanel(SectionCenter(i), SectionHalfSize)) return false;

            for (int i = 0; i < MaxContentRows; i++)
                if (!InsidePanel(ContentCenter(i), ContentHalfSize)) return false;

            return true;
        }

        /// <summary>Строка с таким центром и половиной размера помещается в панель.</summary>
        public static bool InsidePanel(Vector2 center, Vector2 half)
        {
            if (Mathf.Abs(center.x) + half.x > WidthMeters * 0.5f + 0.0001f) return false;
            if (Mathf.Abs(center.y) + half.y > HeightMeters * 0.5f + 0.0001f) return false;
            return true;
        }

        /// <summary>Две строки содержимого не налезают друг на друга (иначе луч нажимал бы соседнюю).</summary>
        public static bool ContentRowsDoNotOverlap()
        {
            return ContentStepY >= ContentHalfSize.y * 2f - 0.0001f;
        }

        /// <summary>Строки разделов не налезают друг на друга.</summary>
        public static bool SectionRowsDoNotOverlap()
        {
            return SectionStepY >= SectionHalfSize.y * 2f - 0.0001f;
        }

        /// <summary>Строка содержимого не залезает в колонку разделов.</summary>
        public static bool ColumnsDoNotOverlap()
        {
            float sectionRight = SectionColumnX + SectionHalfSize.x;
            float contentLeft = ContentColumnX - ContentHalfSize.x;
            return contentLeft >= sectionRight;
        }
    }
}
