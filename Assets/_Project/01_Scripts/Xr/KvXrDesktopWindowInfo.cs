using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ОДНА КОМАНДА-ОКНО (§42): какая команда компьютера каким экраном шлема открывается.
    ///
    /// ЗАЧЕМ ОТДЕЛЬНАЯ СТРУКТУРА. Команд, открывающих на компьютере окно, — 50, а экранов в шлеме
    /// одиннадцать (пять новых и шесть уже принятых). Соответствие хранится ДАННЫМИ — см.
    /// `KvXrDesktopWindows`, — а не пятьюдесятью ветками `switch`.
    /// </summary>
    public struct KvXrDesktopWindowInfo
    {
        /// <summary>Id команды компьютера («ui.settings», «joints.panel»…).</summary>
        public string CommandId;

        /// <summary>Экран шлема, которым эта команда открывается.</summary>
        public KvXrPanelLevel Screen;

        /// <summary>Номер вкладки внутри экрана (0 — первая).</summary>
        public int Tab;

        /// <summary>Заголовок окна так, как его увидит владелец.</summary>
        public string Title;
    }
}
