using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// НАЖАТИЕ ВЗГЛЯДОМ ПО ЗАДЕРЖКЕ (сессия §33, дефект с живого шлема 15.10.2026).
    ///
    /// ЗАЧЕМ. Владелец в шлеме: «не могу навестись на оверлей». Причина не в интерфейсе:
    /// указатель проекта брал луч у ПРАВОГО КОНТРОЛЛЕРА (`TryRightPose`), а контроллеры
    /// приложению не отдаются вовсе — значит луча не было и нажать было нечем.
    ///
    /// Пока контроллеры не заведены, единственный доступный «указатель» — ВЗГЛЯД: человек
    /// смотрит на кнопку, и через некоторое время она нажимается сама. Такой способ (dwell)
    /// — стандарт для VR-интерфейсов без контроллеров, и он не мешает: когда контроллер
    /// появится, работает обычный луч с курком.
    ///
    /// ПОЧЕМУ ВЫНЕСЕНО В ОТДЕЛЬНЫЙ ТИП. Решение «пора нажимать» — это ЧИСЛО, а не картинка:
    /// накопили время на одной цели — нажали; отвели взгляд — счётчик обнулился. Такое
    /// проверяется тестом без шлема, что и сделано в `KvGazeDwellTests`.
    /// </summary>
    public static class KvGazeDwell
    {
        /// <summary>Сколько смотреть на кнопку, чтобы она нажалась. Меньше секунды — «случайные» нажатия.</summary>
        public const float DefaultSeconds = 1.3f;

        /// <summary>Нижняя граница порога: нулевой порог нажимал бы всё подряд мгновенно.</summary>
        public const float MinSeconds = 0.2f;

        /// <summary>
        /// ПРИБАВИТЬ ВРЕМЯ К НАКОПЛЕННОМУ. Возвращает новое значение в границах
        /// `0 … порог`: переполнение не нужно, а отрицательный шаг (сломанное время кадра)
        /// игнорируется, чтобы счётчик не «отматывался» назад.
        /// </summary>
        public static float Advance(float progress, float deltaTime, float threshold)
        {
            float limit = Limit(threshold);
            float step = deltaTime > 0f ? deltaTime : 0f;
            return Mathf.Clamp(progress + step, 0f, limit);
        }

        /// <summary>НАКОПИЛОСЬ ЛИ ДОСТАТОЧНО для нажатия.</summary>
        public static bool IsReady(float progress, float threshold)
        {
            return progress >= Limit(threshold) - 0.0001f;
        }

        /// <summary>Доля накопленного 0…1 — для подсветки луча, чтобы человек видел «сколько ещё смотреть».</summary>
        public static float Progress01(float progress, float threshold)
        {
            return Mathf.Clamp01(progress / Limit(threshold));
        }

        /// <summary>Порог с защитой от нуля и отрицательных значений.</summary>
        public static float Limit(float threshold)
        {
            return threshold < MinSeconds ? MinSeconds : threshold;
        }
    }
}
