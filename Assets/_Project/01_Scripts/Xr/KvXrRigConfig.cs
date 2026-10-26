using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// КОНФИГУРАЦИЯ XR-РИГА (сессия §28, этап 1 плана XR).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ ТИП. Все решения по ригу — уровень глаз, масштаб мира, способ
    /// стерео, отсчёт от пола или от устройства — должны быть ОДНИМ объектом, который
    /// можно посчитать, проверить тестом и показать в отчёте. Разбросанные по сцене
    /// числа проверить нельзя: в шлеме они выглядят «нормально» ровно до момента,
    /// когда оператор ростом 1,95 видит панель на уровне колен.
    ///
    /// ЧТО ЗДЕСЬ ЧИСТОЕ, А ЧТО НЕТ. Здесь только арифметика (никаких GameObject,
    /// сцен и устройств) — поэтому тип полностью покрыт EditMode-тестами, а сборку
    /// рига делает <c>KvXrRigBuilder</c>.
    ///
    /// Решения, на которые опирается тип: [R1] PICO 4 Ultra, [R2] HDRP низкий пресет,
    /// [R3] только OpenXR, [D6] отдельная сцена для VR/MR, [D7] стол 100×70 см,
    /// высота 78 см, комната 3×2 м.
    /// </summary>
    public enum KvXrOriginMode
    {
        /// <summary>Отсчёт от ПОЛА: ноль мира — пол комнаты, рост оператора учитывается сам.</summary>
        Floor = 0,

        /// <summary>Отсчёт от УСТРОЙСТВА: ноль — сам шлем. Для сидячего режима.</summary>
        Device = 1,

        /// <summary>Отсчёт от СЦЕНЫ: положение задаётся сценой, трекинг только накладывается.</summary>
        Scene = 2
    }

    /// <summary>
    /// Параметры XR-рига. Неизменяемое значение: правится копированием, а не «на месте»
    /// (иначе одну и ту же конфигурацию нельзя сравнить «до/после» в отчёте).
    /// </summary>
    public struct KvXrRigConfig
    {
        /// <summary>Уровень глаз оператора, метры. Средний рост 1,70 → глаза ≈ 1,60.</summary>
        public float eyeHeightMeters;

        /// <summary>Масштаб мира: 1 — метрический мир ([D7]). Мир проекта не метрический (см. план).</summary>
        public float worldScale;

        /// <summary>Стерео SinglePassInstanced: обязательное условие для мобильного шлема.</summary>
        public bool singlePassInstanced;

        /// <summary>От чего отсчитывается мир (пол/устройство/сцена).</summary>
        public KvXrOriginMode originMode;

        /// <summary>Показывать реальную комнату (passthrough) вместо виртуальной лаборатории.</summary>
        public bool passthrough;

        /// <summary>Конфигурация по умолчанию — она же «рекомендуемая» для MVP.</summary>
        public static KvXrRigConfig Default()
        {
            return new KvXrRigConfig
            {
                eyeHeightMeters = 1.60f,
                worldScale = 1f,
                singlePassInstanced = true,
                originMode = KvXrOriginMode.Floor,
                passthrough = false
            };
        }

        /// <summary>
        /// Уровень глаз по росту оператора.
        ///
        /// Почему не «просто рост»: глаза находятся примерно на 0,94 роста, и это
        /// единственная величина, от которой зависит, куда встанет панель интерфейса.
        /// Диапазон ограничен (рост 1,33…2,13 м) — защита от опечатки в сантиметрах
        /// вместо метров: рост «170» не должен дать глаза на высоте 160 метров.
        /// </summary>
        public static KvXrRigConfig ForOperatorHeight(float heightMeters)
        {
            KvXrRigConfig c = Default();
            float h = Mathf.Clamp(heightMeters, 1.33f, 2.13f);
            c.eyeHeightMeters = Mathf.Clamp(h * 0.94f, 1.25f, 2.00f);
            return c;
        }

        /// <summary>Строка состояния для журнала и отчёта (одной строкой, без домыслов).</summary>
        public string Describe()
        {
            return string.Format(
                "XR-риг: глаза {0:0.00} м · мир {1:0.00}× (≈{2} м на юнит) · стерео {3} · отсчёт: {4} · {5}",
                eyeHeightMeters,
                worldScale,
                worldScale > 0f ? (1f / worldScale).ToString("0.00") : "—",
                singlePassInstanced ? "SinglePassInstanced" : "MultiPass",
                OriginLabel(originMode),
                passthrough ? "реальная комната (MR)" : "виртуальная лаборатория (VR)");
        }

        /// <summary>Читаемое имя способа отсчёта.</summary>
        public static string OriginLabel(KvXrOriginMode mode)
        {
            switch (mode)
            {
                case KvXrOriginMode.Floor: return "от пола";
                case KvXrOriginMode.Device: return "от устройства";
                default: return "от сцены";
            }
        }
    }
}
