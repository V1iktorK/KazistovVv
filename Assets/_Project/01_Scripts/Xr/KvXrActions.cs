using UnityEngine;
using KazistovVvUI;

namespace KazistovVvXr
{
    /// <summary>
    /// СТАНДАРТНОЕ УПРАВЛЕНИЕ VR: кнопки A/B/X/Y + Menu (сессия 27.10.2026, дополнено 05.11.2026).
    ///
    /// ЗАДАЧЕЙ СКАЗАНО: «управление среднестатистическим для VR пользователей, чтобы им не надо
    /// было переучиваться». Значит — ровно те привязки, что во всех Quest/PICO-приложениях:
    ///   • <b>A (правый primary)</b> — подтвердить/выбрать (то же, что курок);
    ///   • <b>B (правый secondary)</b> — назад/отмена/закрыть окно (эквивалент Esc);
    ///     <b>удержание B 1 с</b> — E-STOP (аварийный стоп робота, команда `estop`);
    ///   • <b>X (левый primary)</b> — меню (открыть/закрыть стартовое меню);
    ///     <b>удержание X 0,5 с</b> — переключить способ перемещения «Телепорт ↔ Плавный ход»;
    ///   • <b>Y (левый secondary)</b> — панель у руки;
    ///   • <b>Menu (системная)</b> — поиск по 110 командам; удержание — переставить меню перед собой.
    ///   • <b>удержание клика левого стика 0,8 с</b> — вернуться на рабочую позицию (центр ангара).
    ///
    /// ПОЧЕМУ УДЕРЖАНИЯ. Владелец: «наполнить функционалом каждую кнопочку». У одиночных нажатий
    /// уже есть действия, а удержание — это ВТОРОЕ действие без новой кнопки и без конфликта:
    /// короткое нажатие остаётся прежним, длинное — новое. Правило «одно нажатие — одно действие»
    /// сохраняется: решение принимается по ОТПУСКАНИЮ, ровно один раз.
    ///
    /// ФРОНТ НАЖАТИЯ. Кнопки читаются как состояние, действия — по фронту (одно нажатие = одно
    /// действие), как §27 лечил двойное действие Enter.
    ///
    /// ПОРЯДОК ОБРАБОТКИ (не нарушать): сначала B («назад» имеет приоритет — он аварийный выход
    /// из любого состояния), затем X (меню), затем Y (панель), затем Menu (поиск), затем A.
    /// </summary>
    public static class KvXrActions
    {
        private const float MoveModeHoldSeconds = 0.50f;       // X удержан — смена способа хода
        private const float EStopHoldSeconds = 1.00f;          // B удержан — аварийный стоп
        private const float MenuRepositionHoldSeconds = 0.80f; // Menu удержан — переставить меню
        private const float StickHoldSeconds = 0.80f;          // стик удержан — возврат к столу

        private static bool aWasDown, bWasDown, xWasDown, yWasDown, menuWasDown, stickWasDown;

        private static float xDownAt = -1f, bDownAt = -1f, menuDownAt = -1f, stickDownAt = -1f;
        private static bool xHoldFired, bHoldFired, menuHoldFired, stickHoldFired;

        /// <summary>Сколько действий выполнено (диагностика/тесты).</summary>
        public static int ActionCount { get; private set; }

        /// <summary>Какое действие выполнено последним (диагностика/тесты).</summary>
        public static string LastAction { get; private set; } = "";

        /// <summary>Сколько раз сработал E-STOP (диагностика/тесты).</summary>
        public static int EStopCount { get; private set; }

        /// <summary>
        /// Опрос кнопок и выполнение действий. Вызывается каждый кадр из <c>KvXrRig.Update</c>.
        /// </summary>
        public static void Tick()
        {
            float now = Time.unscaledTime;

            bool a = KvXrInputSource.RightPrimary;
            bool b = KvXrInputSource.RightSecondary;
            bool x = KvXrInputSource.LeftPrimary;
            bool y = KvXrInputSource.LeftSecondary;
            bool menu = KvXrInputSource.MenuButton;
            bool stick = KvXrInputSource.LeftStickClick;

            // ---------------------------------------------------------------- B
            if (b && !bWasDown)
            {
                bDownAt = now;
                bHoldFired = false;
            }
            if (b && bDownAt >= 0f && !bHoldFired && now - bDownAt >= EStopHoldSeconds)
            {
                bHoldFired = true;
                TriggerEStop();
            }
            if (!b && bWasDown && bDownAt >= 0f && !bHoldFired)
            {
                Back();
            }
            bWasDown = b;

            // ---------------------------------------------------------------- X
            if (x && !xWasDown)
            {
                xDownAt = now;
                xHoldFired = false;
            }
            if (x && xDownAt >= 0f && !xHoldFired && now - xDownAt >= MoveModeHoldSeconds)
            {
                xHoldFired = true;
                ToggleMoveMode();
            }
            if (!x && xWasDown && xDownAt >= 0f && !xHoldFired)
            {
                ToggleMenu();
            }
            xWasDown = x;

            // ---------------------------------------------------------------- Y
            if (y && !yWasDown)
            {
                ToggleQuickPanel();
            }
            yWasDown = y;

            // ---------------------------------------------------------------- Menu
            if (menu && !menuWasDown)
            {
                menuDownAt = now;
                menuHoldFired = false;
            }
            if (menu && menuDownAt >= 0f && !menuHoldFired && now - menuDownAt >= MenuRepositionHoldSeconds)
            {
                menuHoldFired = true;
                RepositionMenu();
            }
            if (!menu && menuWasDown && menuDownAt >= 0f && !menuHoldFired)
            {
                OpenCommandSearch();
            }
            menuWasDown = menu;

            // ---------------------------------------------------------------- Левый стик (клик)
            if (stick && !stickWasDown)
            {
                stickDownAt = now;
                stickHoldFired = false;
            }
            if (stick && stickDownAt >= 0f && !stickHoldFired && now - stickDownAt >= StickHoldSeconds)
            {
                stickHoldFired = true;
                ReturnToWorkPosition();
            }
            stickWasDown = stick;

            // ---------------------------------------------------------------- A
            if (a && !aWasDown)
            {
                Confirm();
            }
            aWasDown = a;
        }

        // ------------------------------------------------------------------ действия

        /// <summary>B — назад/отмена/закрыть (эквивалент Esc на десктопе).</summary>
        private static void Back()
        {
            ActionCount++;
            LastAction = "back";

            if (KvXrStartMenu.Active != null && KvXrStartMenu.Active.Visible)
            {
                KvXrStartMenu.Active.Hide();
                Debug.Log("[KvXr] B: стартовое меню закрыто");
                return;
            }

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

        /// <summary>
        /// УДЕРЖАНИЕ B — АВАРИЙНЫЙ СТОП. Команда та же, что красная кнопка на компьютере (`estop`),
        /// второй реализации нет. Отпускание до порога отменяет: случайное касание не остановит робота.
        /// </summary>
        private static void TriggerEStop()
        {
            ActionCount++;
            LastAction = "estop";
            EStopCount++;
            KvHapticsBridge.Pulse(KvXrQuickHand.Right, 1f, 0.15f);
            if (KvCommands.Get("estop") != null)
            {
                KvCommands.Invoke("estop");
                Debug.Log("[KvXr] B (удержание): E-STOP — аварийный стоп робота");
            }
            else
            {
                Debug.LogWarning("[KvXr] B (удержание): команда estop не зарегистрирована");
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

        /// <summary>
        /// УДЕРЖАНИЕ X — СМЕНА СПОСОБА ПЕРЕМЕЩЕНИЯ. Владелец: «добавить перемещение по ангару»;
        /// в VR не было вообще никакого переключателя (аудит 05.11.2026). Настройка сохраняется
        /// в PlayerPrefs, поэтому переживает перезапуск.
        /// </summary>
        private static void ToggleMoveMode()
        {
            ActionCount++;
            KvXrMoveMode mode = KvXrSettings.ToggleMoveMode();
            LastAction = mode == KvXrMoveMode.Smooth ? "move-smooth" : "move-teleport";
            KvHapticsBridge.Pulse(KvXrQuickHand.Right, 0.45f, 0.08f);
            Debug.Log("[KvXr] X (удержание): способ перемещения — " +
                      KvXrSettings.MoveModeLabel((int)mode) +
                      (mode == KvXrMoveMode.Smooth
                          ? " · левый стик — идти"
                          : " · левый стик вперёд — телепорт"));
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

        /// <summary>Menu — поиск по командам (аналог Ctrl+P на компьютере).</summary>
        private static void OpenCommandSearch()
        {
            ActionCount++;
            LastAction = "search";
            KvXrGroupPanel panel = KvXrGroupPanel.Active;
            if (panel != null)
            {
                panel.OpenSearch();
                Debug.Log("[KvXr] MENU: открыт поиск по 110 командам");
            }
            else
            {
                Debug.Log("[KvXr] MENU: панели ещё нет — поиск недоступен");
            }
        }

        /// <summary>УДЕРЖАНИЕ Menu — переставить меню перед собой (после ходьбы оно остаётся позади).</summary>
        private static void RepositionMenu()
        {
            ActionCount++;
            LastAction = "menu-reposition";
            if (KvXrStartMenu.Active != null)
            {
                KvXrStartMenu.Active.Reposition();
                Debug.Log("[KvXr] MENU (удержание): меню переставлено перед оператором");
            }
        }

        /// <summary>УДЕРЖАНИЕ левого стика — вернуться на рабочую позицию (центр ангара).</summary>
        private static void ReturnToWorkPosition()
        {
            ActionCount++;
            LastAction = "return-home";
            KvXrLocomotion locomotion = KvXrLocomotion.Active;
            if (locomotion != null)
            {
                locomotion.ReturnToStart();
            }
            else
            {
                Debug.Log("[KvXr] стик (удержание): локомоции нет — возврат невозможен");
            }
        }

        /// <summary>A — подтвердить/выбрать (то же, что курок вне интерфейса).</summary>
        private static void Confirm()
        {
            // MR «ПОСТАВИТЬ СЮДА» ИМЕЕТ ПРИОРИТЕТ (добавлено 02.11.2026, S45).
            if (KvMrPlacement.TryConsumeConfirm()) return;

            ActionCount++;
            LastAction = "confirm";
            KvXrFlowBridge.RequestConfirm();
            Debug.Log("[KvXr] A: подтвердить (как курок, с арбитражем «не над интерфейсом»)");
        }
    }
}
