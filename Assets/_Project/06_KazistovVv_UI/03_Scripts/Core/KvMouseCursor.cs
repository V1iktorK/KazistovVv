using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>Режим курсора: РЕЖИМ КАМЕРЫ (телеоперация) либо РЕЖИМ ИНТЕРФЕЙСА (системный курсор Unity).</summary>
    public enum KvCursorMode
    {
        /// <summary>РЕЖИМ КАМЕРЫ: `Cursor.lockState = Locked`, системный курсор скрыт, панели скрыты, мышь вращает камеру, WASD двигает камеру.</summary>
        CameraMode = 0,
        /// <summary>РЕЖИМ ИНТЕРФЕЙСА: `Cursor.visible = true`, `lockState = None`, панели показаны, камера стоит (ни мышь, ни WASD её не двигают).</summary>
        UiMode = 1,

        /// <summary>СОВМЕСТИМОСТЬ (§22–§25): прежнее имя режима КАМЕРЫ — то же значение.</summary>
        Captured = CameraMode,
        /// <summary>СОВМЕСТИМОСТЬ (§22–§25): прежнее имя режима ИНТЕРФЕЙСА — то же значение.</summary>
        Free = UiMode
    }

    /// <summary>
    /// РЕЖИМ КУРСОРА — ЕДИНСТВЕННЫЙ источник правды для всего проекта (ФИКСЫ 1, 3, 4 §22/§23; §26).
    ///
    /// ДВА РЕЖИМА (и ровно два набора записей в `Cursor.*` во ВСЁМ проекте — здесь):
    ///   • <see cref="KvCursorMode.Captured"/> — РЕЖИМ КАМЕРЫ (телеоперация):
    ///     `Cursor.lockState = Locked`, `Cursor.visible = false`, панели скрыты,
    ///     мышь вращает камеру, интерфейс недоступен;
    ///   • <see cref="KvCursorMode.Free"/> — РЕЖИМ ИНТЕРФЕЙСА:
    ///     `Cursor.visible = true`, `Cursor.lockState = None`, панели показаны,
    ///     камера на мышь НЕ реагирует, мышь работает только с UI (меню, дерево, свойства).
    ///
    /// ПЕРЕКЛЮЧЕНИЕ — клавиша <see cref="ModeToggleKey"/> (**TAB**, §26): читается ФРОНТ нажатия,
    /// как и положено обычному переключателю. Tab показывает системный курсор Unity и панели,
    /// повторный Tab возвращает управление камере.
    ///
    /// CAPS LOCK НЕ ДЕЛАЕТ НИЧЕГО (§26). Удержание убрано ПОЛНОСТЬЮ: клавиша не переключает
    /// режим, не влияет на курсор и не влияет на панели — этот класс её даже не читает.
    /// Прежняя логика удержания (§23.3: `HoldKey`, `HoldPressed`, `modeBeforeHold`,
    /// `HoldSuspended`) удалена целиком.
    ///
    /// ПОЧЕМУ САМОВОССТАНОВЛЕНИЕ: в редакторе Unity сама снимает захват по Esc и при потере
    /// фокуса окна, поэтому «кто последний выставил флаг» и давало «мёртвую» мышь. Теперь
    /// <see cref="Tick"/> вызывается каждый кадр и возвращает `Cursor.*` к выбранному режиму,
    /// если кто-то (движок или сторонний код) их изменил — счётчик <see cref="ReapplyCount"/>
    /// показывает, сколько таких правок уже отбито.
    /// </summary>
    public static class KvMouseCursor
    {
        /// <summary>
        /// КЛАВИША ПЕРЕКЛЮЧЕНИЯ РЕЖИМА — **TAB** (§26): режим камеры ⟷ режим интерфейса.
        /// Это единственное место, где Tab что-то переключает: фокус по кнопкам тулбара
        /// переключают стрелки и F6 (<see cref="KvKeyboardNav"/>).
        /// </summary>
        public const KeyCode ModeToggleKey = KeyCode.Tab;

        /// <summary>Совместимость с §22/§23: та же клавиша переключения (<see cref="ModeToggleKey"/>).</summary>
        public const KeyCode ToggleKey = ModeToggleKey;

        /// <summary>
        /// Показывать ли СОБСТВЕННУЮ иконку курсора (<see cref="KvVirtualCursor"/>) в режиме камеры.
        ///
        /// ПО УМОЛЧАНИЮ **false** (§26): в режиме камеры курсор скрыт полностью (как в обычном
        /// FPS-управлении), в режиме интерфейса показывается СИСТЕМНЫЙ курсор Unity.
        /// Флаг оставлен единственной точкой, через которую внутриигровую иконку можно включить,
        /// если она понадобится для особого случая — основной режим от неё не зависит.
        /// </summary>
        public static bool InGameIconInCameraMode = false;

        /// <summary>Режим на момент старта: системный курсор (меню и панели кликабельны).</summary>
        private static KvCursorMode mode = KvCursorMode.Free;

        /// <summary>Смена режима (подписчики — диагностика/оболочка).</summary>
        public static event Action<KvCursorMode> ModeChanged;

        /// <summary>Текущий режим (совместимое имя; для индикатора и других систем — <see cref="CurrentMode"/>).</summary>
        public static KvCursorMode Mode { get { return mode; } }

        /// <summary>
        /// ТЕКУЩИЙ РЕЖИМ КУРСОРА — публичное свойство для индикатора (§26) и любых других систем.
        /// Читать его можно всегда; менять режим — только через <see cref="SetMode"/>.
        /// </summary>
        public static KvCursorMode CurrentMode { get { return mode; } }

        /// <summary>Режим камеры активен (телеоперация).</summary>
        public static bool Captured { get { return mode == KvCursorMode.Captured; } }

        /// <summary>Последняя причина смены режима (диагностика и отчёт).</summary>
        public static string LastReason { get; private set; }

        /// <summary>Сколько раз режим переключался (диагностика).</summary>
        public static int SwitchCount { get; private set; }

        /// <summary>Сколько раз режим пришлось восстановить после чужой правки `Cursor.*`.</summary>
        public static int ReapplyCount { get; private set; }

        /// <summary>Кадр, в котором режим последний раз СМЕНИЛСЯ (−1 — ещё не менялся).</summary>
        private static int switchFrame = -1;

        /// <summary>
        /// Режим сменился ИМЕННО В ЭТОМ кадре. Нужно контроллеру камеры: при переключении
        /// Tab (или кликом) мышь в том же кадре ещё «свободна», и её дельта могла бы разом
        /// повернуть камеру на десятки градусов — однокадровый «рывок». С этим признаком
        /// камера пропускает поворот ровно один кадр, а со следующего ведёт себя как обычно.
        /// </summary>
        public static bool SwitchedThisFrame { get { return switchFrame == Time.frameCount; } }

        /// <summary>
        /// Пока true, клавиша переключения (Tab) игнорируется: модальный экран (стартовое меню,
        /// презентационный режим) владеет курсором сам. Иначе Tab отобрал бы мышь у меню,
        /// и его пункты стало бы невозможно нажать.
        /// </summary>
        public static bool ModeToggleSuspended { get; set; }

        // ------------------------------------------------------------------ смена режима

        /// <summary>Включить РЕЖИМ КАМЕРЫ (телеоперация) и скрыть панели.</summary>
        public static void Capture(string reason)
        {
            SetMode(KvCursorMode.Captured, reason, true);
        }

        /// <summary>
        /// Показать СИСТЕМНЫЙ курсор Unity (режим интерфейса). <paramref name="showPanels"/>=false —
        /// панели не трогать (нужно стартовому меню: оно само гасит оболочку и рисует свой экран).
        /// </summary>
        public static void Release(string reason, bool showPanels = true)
        {
            SetMode(KvCursorMode.Free, reason, showPanels);
        }

        /// <summary>
        /// Переключить режим (клавиша Tab, команда «Показать/скрыть интерфейс», диагностика).
        /// Режим камеры ⟷ режим интерфейса; панели переключаются вместе с режимом.
        /// </summary>
        public static KvCursorMode Toggle(string reason)
        {
            SetMode(Captured ? KvCursorMode.Free : KvCursorMode.Captured, reason, true);
            return mode;
        }

        /// <summary>
        /// ЕДИНСТВЕННАЯ функция смены режима (§26, пункт «единый источник правды»):
        /// `SetMode(KvCursorMode.CameraMode)` или `SetMode(KvCursorMode.UiMode)`.
        /// Все прочие входы (<see cref="Capture"/>, <see cref="Release"/>, <see cref="Toggle"/>,
        /// Esc, клик по индикатору режима) сводятся СЮДА, поэтому панели, системный курсор,
        /// иконка и строка состояния не могут разъехаться.
        /// </summary>
        public static void SetMode(KvCursorMode value, string reason = "смена режима курсора",
            bool showPanels = true)
        {
            bool changed = mode != value;
            mode = value;
            if (changed)
            {
                SwitchCount++;
                LastReason = reason;
                switchFrame = Time.frameCount;         // см. SwitchedThisFrame (защита камеры от рывка)
            }

            if (showPanels) KazistovVvUIManager.SetUiVisible(value == KvCursorMode.Free);
            Apply(true);

            if (changed && ModeChanged != null) ModeChanged(mode);
        }

        // ------------------------------------------------------------------ кадровое обслуживание

        /// <summary>
        /// Вызывается КАЖДЫЙ КАДР (из <see cref="KvVirtualCursor"/>).
        ///
        /// 1) КЛАВИША ПЕРЕКЛЮЧЕНИЯ (**TAB**, §26): фронт нажатия переключает режим
        ///    «камера ⟷ интерфейс». Раньше здесь читалось УДЕРЖАНИЕ CapsLock — эта логика
        ///    удалена целиком, CapsLock не делает ничего.
        /// 2) САМОВОССТАНОВЛЕНИЕ (ФИКС 1 §23): если `Cursor.*` изменил кто-то ещё (движок
        ///    редактора по Esc, потеря фокуса окна, сторонний скрипт) — режим возвращается
        ///    к выбранному. Именно это убирает «после работы с роботом мышь не восстанавливается».
        /// </summary>
        public static void Tick()
        {
            if (ModeTogglePressedThisFrame())
                Toggle("TAB: переключение режима курсора (камера ⟷ интерфейс)");

            Apply(false);                              // «догнать» чужую правку Cursor.*
        }

        /// <summary>
        /// ФРОНТ нажатия клавиши переключения (TAB). Не срабатывает:
        ///   • пока модальный экран владеет курсором — <see cref="ModeToggleSuspended"/>
        ///     (стартовое меню, презентационный режим);
        ///   • пока текст вводится в поле (палитра команд Ctrl+P, поиск в окне F12, переименование
        ///     узла) — там Tab принадлежит вводу/списку, и отбирать его режимом курсора нельзя.
        /// </summary>
        public static bool ModeTogglePressedThisFrame()
        {
            if (ModeToggleSuspended) return false;
            if (IsTypingInField()) return false;

            try
            {
                UnityEngine.InputSystem.Keyboard k = UnityEngine.InputSystem.Keyboard.current;
                if (k != null) return k.tabKey.wasPressedThisFrame;
            }
            catch (Exception e)
            {
                // P3-3 (11.11.2026). ЧТО БЫЛО: перехват был ПУСТЫМ (тело из одних скобок). Если
                // Input System бросал, состояние курсора расходилось с моделью, счётчик
                // переприменений не рос, и в журнале НЕ БЫЛО НИ СТРОЧКИ: шапка класса описывает
                // историю «мышь не восстанавливается», а её повторение становилось
                // недиагностируемым.
                // ЧЕМ ЭТО ГРОЗИЛО: оператор застревал с захваченной мышью без единой зацепки.
                ReapplyCount++;
                Debug.LogWarning("[KvMouseCursor] переключение режима курсора через Input System не " +
                                 "удалось (" + e.GetType().Name + ": " + e.Message +
                                 ") — пробую legacy Input; переприменений: " + ReapplyCount);
            }
            try { return Input.GetKeyDown(ModeToggleKey); }
            catch (Exception e)
            {
                ReapplyCount++;
                Debug.LogWarning("[KvMouseCursor] legacy Input тоже недоступен (" + e.GetType().Name +
                                 ": " + e.Message + ") — переключение режима курсора не работает; " +
                                 "переприменений: " + ReapplyCount);
                return false;
            }
        }

        /// <summary>Идёт ли ввод текста в поле (тот же признак, что у <see cref="KvKeyboardNav"/>).</summary>
        private static bool IsTypingInField()
        {
            try
            {
                EventSystem es = EventSystem.current;
                if (es == null) return false;
                GameObject selected = es.currentSelectedGameObject;
                if (selected == null) return false;
                InputField field = selected.GetComponent<InputField>();
                return field != null && field.isFocused;
            }
            catch (Exception) { return false; }
        }

        // ------------------------------------------------------------------ применение

        /// <summary>
        /// Привести системный курсор и собственную иконку в соответствие с режимом.
        /// <paramref name="force"/>=false — тихая проверка дрейфа: если всё уже так, ничего
        /// не трогаем; если что-то разъехалось — исправляем и считаем <see cref="ReapplyCount"/>.
        /// </summary>
        public static void Apply(bool force = true)
        {
            KvVirtualCursor cursor = KvVirtualCursor.Ensure();

            // ОКНО НЕ В ФОКУСЕ: захват невозможен (мышь принадлежит другому окну), поэтому
            // показываем системный курсор, но САМ РЕЖИМ НЕ МЕНЯЕМ — при возврате фокуса
            // он восстановится сам. Иначе Alt+Tab оставлял бы скрытый курсор.
            if (!Application.isFocused)
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    // P3-3 (11.11.2026): см. пояснение ниже у основного блока — отказ установки
                    // курсора больше не глотается молча, иначе «мышь не восстанавливается»
                    // повторяется без единой зацепки в журнале.
                    try { Cursor.lockState = CursorLockMode.None; }
                    catch (Exception e)
                    {
                        ReapplyCount++;
                        Debug.LogWarning("[KvMouseCursor] окно без фокуса: не удалось снять захват (" +
                                         e.GetType().Name + ": " + e.Message + "); переприменений: " +
                                         ReapplyCount);
                    }
                }
                if (!Cursor.visible)
                {
                    try { Cursor.visible = true; }
                    catch (Exception e)
                    {
                        ReapplyCount++;
                        Debug.LogWarning("[KvMouseCursor] окно без фокуса: не удалось показать курсор (" +
                                         e.GetType().Name + ": " + e.Message + "); переприменений: " +
                                         ReapplyCount);
                    }
                }
                if (cursor != null) cursor.SetVisible(false);
                return;
            }

            bool captured = Captured;
            CursorLockMode wantLock = captured ? CursorLockMode.Locked : CursorLockMode.None;
            // ФИКС 4 (§23): в режиме интерфейса показывается ИМЕННО СИСТЕМНЫЙ курсор Unity.
            bool wantVisible = !captured;

            bool drift = Cursor.lockState != wantLock || Cursor.visible != wantVisible;
            if (force || drift)
            {
                // P3-3 (11.11.2026). ЧТО БЫЛО. Главная запись состояния курсора была обёрнута в
                // `try` с ПУСТЫМ перехватом. Если платформа бросала
                // исключение, состояние курсора расходилось с моделью, но `ReapplyCount` не рос,
                // и в журнале не появлялось НИ СТРОЧКИ: шапка класса описывает историю «мышь не
                // восстанавливается», а её повторение становилось недиагностируемым.
                // ЧЕМ ЭТО ГРОЗИЛО. Оператор застревал с захваченной или скрытой мышью, и понять,
                // что дело в отказе `Cursor.*`, было нечем: счётчик восстановлений молчал.
                // ТЕПЕРЬ отказ виден: `ReapplyCount++` и предупреждение с типом исключения.
                try
                {
                    Cursor.lockState = wantLock;
                    Cursor.visible = wantVisible;
                }
                catch (Exception e)
                {
                    ReapplyCount++;
                    Debug.LogWarning("[KvMouseCursor] не удалось поставить курсор (" +
                                     e.GetType().Name + ": " + e.Message + "): хотели " + wantLock +
                                     " / видимость " + wantVisible + ", сейчас " + Cursor.lockState +
                                     " / " + Cursor.visible + "; переприменений: " + ReapplyCount);
                }
                if (!force && drift) ReapplyCount++;
            }

            // §26: своя иконка в основном режиме НЕ используется (по умолчанию выключена) —
            // в режиме камеры курсор скрыт полностью, в режиме интерфейса работает системный.
            if (cursor != null) cursor.SetVisible(captured && InGameIconInCameraMode);
            if (captured) KvCursors.ResetShapeOnly();
        }

        // ------------------------------------------------------------------ форма системного курсора
        // §26, пункт «единый источник правды»: ЛЮБАЯ запись в `Cursor.*` живёт только здесь.
        // Форма курсора (курсоры изменения размера окна, KvCursors) — тоже запись в `Cursor.*`,
        // поэтому она проходит через эти два метода, а не напрямую из KvCursors.

        /// <summary>
        /// Поставить СИСТЕМНОМУ курсору форму (курсоры изменения размера окна).
        /// В режиме камеры вызов игнорируется: системный курсор скрыт и подмена бессмысленна.
        /// </summary>
        public static void SetSystemCursorShape(Texture2D texture, Vector2 hotspot)
        {
            if (Captured) return;
            // P3-4 (11.11.2026). ЧТО БЫЛО. ПУСТОЙ перехват вокруг `Cursor.SetCursor`: если форма
            // курсора не ставилась, об этом не узнавал никто — курсор «залипал» в форме изменения
            // размера окна, и причину искали в UI, а не в отказе платформы.
            // ЧЕМ ЭТО ГРОЗИЛО. Неверный курсор при перетаскивании границ окна без единой зацепки
            // в журнале. ТЕПЕРЬ причина называется — ОДИН раз: форма курсора вызывается часто
            // (при наведении на границу), и предупреждение в каждом кадре обесценило бы себя.
            try { Cursor.SetCursor(texture, hotspot, CursorMode.ForceSoftware); }
            catch (Exception e)
            {
                if (systemCursorShapeWarned) return;
                systemCursorShapeWarned = true;
                Debug.LogWarning("[KvMouseCursor] не удалось поставить ФОРМУ системного курсора (" +
                                 e.GetType().Name + ": " + e.Message + ") — курсор останется " +
                                 "обычным; сообщение повторяться не будет");
            }
        }

        /// <summary>Сообщение об отказе формы курсора уже выдано (см. `SetSystemCursorShape`).</summary>
        private static bool systemCursorShapeWarned;

        /// <summary>Вернуть системному курсору обычную форму (см. <see cref="SetSystemCursorShape"/>).</summary>
        public static void ResetSystemCursorShape()
        {
            if (Captured) return;
            // P3-4 (11.11.2026): см. пояснение в `SetSystemCursorShape` — отказ формы курсора
            // больше не глотается молча (сообщение одно на компонент, чтобы не залить журнал).
            try { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }
            catch (Exception e)
            {
                if (systemCursorShapeWarned) return;
                systemCursorShapeWarned = true;
                Debug.LogWarning("[KvMouseCursor] не удалось вернуть ОБЫЧНУЮ форму курсора (" +
                                 e.GetType().Name + ": " + e.Message + ") — курсор может остаться " +
                                 "с прежней формой; сообщение повторяться не будет");
            }
        }

        /// <summary>Захват по умолчанию для старта (используется камерой).</summary>
        public static void CaptureOnStart(string reason)
        {
            Capture(reason);
        }

        /// <summary>Строка состояния для консоли/диагностики (проверка занимает секунды).</summary>
        public static string Describe()
        {
            return "режим: " + (Captured ? "КАМЕРА (мышь вращает камеру)" : "ИНТЕРФЕЙС (системный курсор Unity)") +
                   " · переключение " + ModeToggleKey +
                   " · Cursor.lockState = " + Cursor.lockState +
                   " · Cursor.visible = " + Cursor.visible +
                   " · иконка " + (KvVirtualCursor.Instance != null && KvVirtualCursor.Instance.IconVisible
                       ? "показана" : "скрыта") +
                   " · переключений " + SwitchCount +
                   " · восстановлений " + ReapplyCount +
                   (string.IsNullOrEmpty(LastReason) ? "" : " · причина: " + LastReason);
        }
    }
}
