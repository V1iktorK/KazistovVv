using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// АРБИТРАЖ «КУРКУ В ПОТОК ИЛИ В ИНТЕРФЕЙС» и строка журнала потока (§42, решение владельца
    /// «максимально глубоко»). Правило одно на все случаи: над панелью, меню или окном нажатие
    /// остаётся нажатием иконки, вне интерфейса — подтверждением точки.
    /// </summary>
    public static class KvXrFlowRules
    {
        /// <summary>Курок подтверждает точку только вне интерфейса.</summary>
        public static bool ShouldFeedConfirm(bool overInterface, bool triggerDown)
        {
            return triggerDown && !overInterface;
        }

        /// <summary>Прицел кормится, когда луч не над интерфейсом.</summary>
        public static bool ShouldFeedAim(bool overInterface)
        {
            return !overInterface;
        }

        /// <summary>
        /// Строка журнала: создан ли поток, какой робот, сколько визуализаций видно, что с лазерами.
        ///
        /// ПРО ЛАЗЕРЫ (изменено 02.11.2026, пункт 2 владельца). Раньше здесь стояло «лазеры потока
        /// выключены (в шлеме свои)» — и владелец справедливо считал, что лазеры выключены вообще.
        /// Теперь строка называет оба лазера и главное правило: луч обрывается на первом попадании.
        /// По ней в журнале шлема видно, что требование выполнено, а не «должно быть выполнено».
        /// </summary>
        public static string Describe(bool flowCreated, string robotName, int visualizersVisible)
        {
            string robot = string.IsNullOrEmpty(robotName) ? "не привязан" : "«" + robotName + "»";
            return "[KvXr] ПОТОК: " + (flowCreated ? "создан на камере шлема" : "НЕ создан") +
                   " · робот " + robot + " · визуализаций видимо " + Mathf.Max(0, visualizersVisible) +
                   " · лазеры: красный (новая точка) и зелёный (выбор) — лучи рук, обрыв по первому попаданию" +
                   " · камеру поток не двигает";
        }
    }
}
