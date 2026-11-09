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
        /// <summary>Порог удержания X — смена способа хода, секунды.</summary>
        public const float MoveModeHoldSeconds = 0.50f;
        /// <summary>Порог удержания B — аварийный стоп, секунды.</summary>
        public const float EStopHoldSeconds = 1.00f;
        /// <summary>Порог удержания Menu — переставить меню, секунды.</summary>
        public const float MenuRepositionHoldSeconds = 0.80f;
        /// <summary>Порог удержания клика левого стика — возврат к столу, секунды.</summary>
        public const float StickHoldSeconds = 0.80f;

        /// <summary>
        /// ЧТО ПРОИСХОДИТ С КНОПКОЙ В ЭТОМ КАДРЕ (чистая функция — проверяется тестом без шлема).
        ///
        /// ПОЧЕМУ ВЫНЕСЕНО (P4-3). Правило «одно нажатие — одно действие» повторялось в `Tick`
        /// ЧЕТЫРЕ раза (B, X, Menu, стик) одними и теми же руками — и проверялось только чтением
        /// исходника на наличие имён `TriggerEStop`/`OpenCommandSearch` и т.п. Такой тест зеленел,
        /// даже если удержание срабатывало на случайном касании. Здесь правило одно на все кнопки:
        ///   • <see cref="ButtonAction.Hold"/> — кнопка держится дольше своего порога и удержание ещё
        ///     не срабатывало;
        ///   • <see cref="ButtonAction.Press"/> — кнопку ОТПУСТИЛИ раньше порога (короткое нажатие);
        ///   • <see cref="ButtonAction.None"/> — ни то, ни другое.
        /// ПОСЛЕ СРАБОТАВШЕГО УДЕРЖАНИЯ отпускание НЕ даёт второго действия: иначе одно долгое
        /// нажатие сработало бы дважды (E-STOP и «назад» одновременно).
        /// </summary>
        public enum ButtonAction
        {
            /// <summary>Ничего не произошло.</summary>
            None = 0,
            /// <summary>Короткое нажатие: действие выполняется по отпусканию.</summary>
            Press = 1,
            /// <summary>Удержание перешло порог: второе действие без новой кнопки.</summary>
            Hold = 2
        }

        /// <summary>Разобрать состояние кнопки (см. <see cref="ButtonAction"/>).</summary>
        public static ButtonAction ResolveButton(bool heldNow, bool wasHeld, bool holdFired,
            float heldSeconds, float holdSeconds)
        {
            if (heldNow && !holdFired && heldSeconds >= holdSeconds) return ButtonAction.Hold;
            if (!heldNow && wasHeld && !holdFired) return ButtonAction.Press;
            return ButtonAction.None;
        }

        /// <summary>Кнопки, у которых есть ВТОРОЕ действие по удержанию (таблица одна на весь файл).</summary>
        public enum HoldButton
        {
            /// <summary>B — аварийный стоп (E-STOP).</summary>
            B = 0,
            /// <summary>X — смена способа перемещения.</summary>
            X = 1,
            /// <summary>Menu — переставить меню перед оператором.</summary>
            Menu = 2,
            /// <summary>Клик левого стика — вернуться на рабочую позицию.</summary>
            LeftStickClick = 3
        }

        /// <summary>Сколько держать кнопку, чтобы сработало удержание, секунды (0 — удержания нет).</summary>
        public static float HoldSecondsOf(HoldButton button)
        {
            switch (button)
            {
                case HoldButton.B: return EStopHoldSeconds;
                case HoldButton.X: return MoveModeHoldSeconds;
                case HoldButton.Menu: return MenuRepositionHoldSeconds;
                case HoldButton.LeftStickClick: return StickHoldSeconds;
                default: return 0f;
            }
        }

        /// <summary>
        /// ИДЕНТИФИКАТОР ДЕЙСТВИЯ УДЕРЖАНИЯ — тот же, что попадает в `LastAction`, поэтому по журналу
        /// видно, что именно сработало. Пустая строка означала бы «удержание есть, а действия нет» —
        /// то есть мёртвую кнопку, которую владелец просил заполнить.
        /// </summary>
        public static string HoldActionId(HoldButton button)
        {
            switch (button)
            {
                case HoldButton.B: return "estop";
                case HoldButton.X: return "move-mode";
                case HoldButton.Menu: return "menu-reposition";
                case HoldButton.LeftStickClick: return "return-home";
                default: return "";
            }
        }

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
            ButtonAction bAction = ResolveButton(b, bWasDown, bHoldFired,
                bDownAt >= 0f ? now - bDownAt : 0f, HoldSecondsOf(HoldButton.B));
            if (bAction == ButtonAction.Hold)
            {
                bHoldFired = true;
                TriggerEStop();
            }
            else if (bAction == ButtonAction.Press)
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
            ButtonAction xAction = ResolveButton(x, xWasDown, xHoldFired,
                xDownAt >= 0f ? now - xDownAt : 0f, HoldSecondsOf(HoldButton.X));
            if (xAction == ButtonAction.Hold)
            {
                xHoldFired = true;
                ToggleMoveMode();
            }
            else if (xAction == ButtonAction.Press)
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
            ButtonAction menuAction = ResolveButton(menu, menuWasDown, menuHoldFired,
                menuDownAt >= 0f ? now - menuDownAt : 0f, HoldSecondsOf(HoldButton.Menu));
            if (menuAction == ButtonAction.Hold)
            {
                menuHoldFired = true;
                RepositionMenu();
            }
            else if (menuAction == ButtonAction.Press)
            {
                OpenCommandSearch();
            }
            menuWasDown = menu;

            // ---------------------------------------------------------------- Левый стик (клик)
            // У стика НЕТ короткого действия: отпускание без удержания ничего не делает (вход в
            // меню по короткому нажатию обрабатывает `KvXrStartMenu`).
            if (stick && !stickWasDown)
            {
                stickDownAt = now;
                stickHoldFired = false;
            }
            ButtonAction stickAction = ResolveButton(stick, stickWasDown, stickHoldFired,
                stickDownAt >= 0f ? now - stickDownAt : 0f, HoldSecondsOf(HoldButton.LeftStickClick));
            if (stickAction == ButtonAction.Hold)
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
