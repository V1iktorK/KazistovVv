using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// СТАНДАРТНОЕ УПРАВЛЕНИЕ VR: кнопки A/B/X/Y (сессия 27.10.2026).
    ///
    /// ЗАДАЧЕЙ СКАЗАНО: «управление среднестатистическим для VR пользователей, чтобы им не надо
    /// было переучиваться». Значит — ровно те привязки, что во всех Quest/PICO-приложениях:
    ///   • <b>A (правый primary)</b> — подтвердить/выбрать (то же, что курок; в интерфейсах
    ///     Quest A — стандартное «подтвердить»);
    ///   • <b>B (правый secondary)</b> — назад/отмена/закрыть окно (эквивалент Esc);
    ///   • <b>X (левый primary)</b> — меню (открыть/закрыть стартовое меню);
    ///   • <b>Y (левый secondary)</b> — вторичное меню: панель у руки.
    ///
    /// ПОЧЕМУ A ДУБЛИРУЕТ КУРОК, А НЕ ЗАМЕНЯЕТ. Курок в проекте уже занят двумя делами
    /// (иконка панели / подтверждение точки — арбитраж §42), и операторы к нему привыкли.
    /// В стандартных Quest-приложениях A = «подтвердить», поэтому A здесь делает то же самое,
    /// что курок ВНЕ интерфейса — нажавший на A не переучивается.
    ///
    /// ФРОНТ НАЖАТИЯ. Кнопки читаются как состояние, но действия выполняются по фронту
    /// (одно нажатие = одно действие) — ровно как §27 лечил двойное действие Enter.
    ///
    /// ПОРЯДОК ОБРАБОТКИ (не нарушать): сначала B («назад» имеет приоритет — он аварийный выход
    /// из любого состояния), затем X (меню), затем Y (панель), затем A (действие).
    /// </summary>
    public static class KvXrActions
    {
        private static bool aWasDown, bWasDown, xWasDown, yWasDown;

        /// <summary>Сколько действий выполнено (диагностика/тесты).</summary>
        public static int ActionCount { get; private set; }

        /// <summary>Какое действие выполнено последним (диагностика/тесты).</summary>
        public static string LastAction { get; private set; } = "";

        /// <summary>
        /// Опрос кнопок A/B/X/Y и выполнение действий по фронту нажатия.
        /// Вызывается каждый кадр из <c>KvXrRig.Update</c>.
        /// </summary>
        public static void Tick()
        {
            bool a = KvXrInputSource.RightPrimary;
            bool b = KvXrInputSource.RightSecondary;
            bool x = KvXrInputSource.LeftPrimary;
            bool y = KvXrInputSource.LeftSecondary;

            // B — «назад/отмена» (приоритет: аварийный выход из любого состояния).
            if (b && !bWasDown)
            {
                Back();
            }

            // X — «меню» (открыть/закрыть стартовое меню).
            if (x && !xWasDown)
            {
                ToggleMenu();
            }

            // Y — «панель у руки» (вторичное меню, как во всех Quest-приложениях).
            if (y && !yWasDown)
            {
                ToggleQuickPanel();
            }

            // A — «подтвердить/выбрать» (то же, что курок вне интерфейса).
            if (a && !aWasDown)
            {
                Confirm();
            }

            aWasDown = a;
            bWasDown = b;
            xWasDown = x;
            yWasDown = y;
        }

        /// <summary>B — назад/отмена/закрыть (эквивалент Esc на десктопе).</summary>
        private static void Back()
        {
            ActionCount++;
            LastAction = "back";

            // 1) Открыто стартовое меню — закрыть его.
            if (KvXrStartMenu.Active != null && KvXrStartMenu.Active.Visible)
            {
                KvXrStartMenu.Active.Hide();
                Debug.Log("[KvXr] B: стартовое меню закрыто");
                return;
            }

            // 2) Есть открытые окна — закрыть последнее (как Esc закрывает контекст).
            if (KvXrWindowHost.Active != null)
            {
                KvXrGroupPanel last = KvXrWindowHost.Active.LastWindow;
                if (last != null)
                {
                    KvXrWindowHost.Active.Close(last);
                    Debug.Log("[KvXr] B: окно закрыто («назад»)");
                    return;
                }
            }

            // 3) Иначе — сброс потока сценария (то же, что Esc → ResetAll на десктопе).
            if (KvXrFlowBridge.Active != null && KvXrFlowBridge.Active.Flow != null)
            {
                KvXrFlowBridge.Active.Flow.ResetFlow("сброшено кнопкой B (эквивалент Esc)");
                Debug.Log("[KvXr] B: сценарий сброшен (эквивалент Esc)");
            }
            else
            {
                Debug.Log("[KvXr] B: контекста нет — сбросать нечего");
            }
        }

        /// <summary>X — стартовое меню (открыть/закрыть).</summary>
        private static void ToggleMenu()
        {
            ActionCount++;
            LastAction = "menu";
            if (KvXrStartMenu.Active == null)
            {
                Debug.Log("[KvXr] X: стартовое меню ещё не создано");
                return;
            }
            KvXrStartMenu.Active.Toggle();
            Debug.Log("[KvXr] X: меню " + (KvXrStartMenu.Active.Visible ? "открыто" : "закрыто"));
        }

        /// <summary>Y — панель у руки (вторичное меню): показать/скрыть.</summary>
        private static void ToggleQuickPanel()
        {
            ActionCount++;
            LastAction = "quickpanel";
            bool next = !KvXrSettings.GetQuickPanelVisible();
            KvXrSettings.SetQuickPanelVisible(next);
            Debug.Log("[KvXr] Y: панель у руки " + (next ? "показана" : "скрыта"));
        }

        /// <summary>A — подтвердить/выбрать (то же, что курок вне интерфейса).</summary>
        private static void Confirm()
        {
            ActionCount++;
            LastAction = "confirm";
            KvXrFlowBridge.RequestConfirm();
            Debug.Log("[KvXr] A: подтвердить (как курок, с арбитражем «не над интерфейсом»)");
        }
    }
}
