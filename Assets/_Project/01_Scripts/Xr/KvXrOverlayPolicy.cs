using System;

namespace KazistovVvXr
{
    /// <summary>Что делать с объектом десктопного интерфейса, попавшим в XR-сцену.</summary>
    public enum KvXrOverlayDisposition
    {
        /// <summary>
        /// ПОГАСИТЬ РИСОВАНИЕ, ЛОГИКУ ОСТАВИТЬ. Так поступают с проектным канвасом: он рисует
        /// дерево, окна и тулбар — в шлеме их быть не должно, — но на том же объекте живёт
        /// менеджер интерфейса, который наполняет РЕЕСТР КОМАНД. Выключить объект целиком значит
        /// оставить панель шлема без команд.
        /// </summary>
        HideRendering = 0,

        /// <summary>УБРАТЬ СОВСЕМ. Так поступают с `XrPanelAnchor`: это он переводит десктопный
        /// канвас в мировое пространство и ставит его перед оператором. Без него канвас не
        /// показывается, и никакой логики в нём нет — терять нечего.</summary>
        Remove = 1,

        /// <summary>НЕ ТРОГАТЬ. Всё незнакомое: интерфейс шлема, риг, свет, начинка сцены.
        /// Дефолт выбран именно такой, чтобы ошибка в списке имён не гасила что-то нужное молча.</summary>
        Keep = 2
    }

    /// <summary>
    /// ПРАВИЛО «ЧЕГО В ШЛЕМЕ БЫТЬ НЕ ДОЛЖНО» (сессия §35, карточка Б20).
    ///
    /// ЗАДАНИЕ ВЛАДЕЛЬЦА. «Десктопный оверлей инструментов и окон (то, что похоже на интерфейс
    /// для ПК) в шлеме должен быть скрыт — его там быть не должно». Сейчас в `XrMain` живут
    /// `KazistovVv_UI` (проектный канвас: дерево, окна, тулбар) и `XrPanelAnchor` — тот самый
    /// компонент, который переводит этот канвас в мировое пространство и вешает перед оператором.
    ///
    /// ГЛАВНАЯ ЛОВУШКА, ИЗ-ЗА КОТОРОЙ ЭТОТ ТИП СУЩЕСТВУЕТ ОТДЕЛЬНО. Кажется очевидным:
    /// «десктопный интерфейс не нужен — выключить `KazistovVv_UI`». Это сломало бы панель шлема:
    /// реестр команд (`KvCommands`) наполняет менеджер интерфейса, живущий на ТОМ ЖЕ объекте.
    /// Команды перестали бы регистрироваться, панель с обобщающими иконками групп оказалась бы
    /// пустой, и в шлеме не нажималось бы вообще ничего — при том, что в журнале не было бы
    /// ни одной ошибки. Поэтому убирается РИСОВАНИЕ, а логика остаётся.
    ///
    /// ПОЧЕМУ ПРАВИЛО — ЧИСТАЯ ФУНКЦИЯ. Это решение о том, что делать с объектом по его имени;
    /// оно не требует сцены и проверяется тестом без шлема.
    /// </summary>
    public static class KvXrOverlayPolicy
    {
        /// <summary>Корень десктопного интерфейса в сцене (имя задаёт менеджер интерфейса).</summary>
        public const string DesktopUiRootName = "KazistovVv_UI";

        /// <summary>Канвас десктопного интерфейса — по этому имени его ищет `KvXrPanelAnchor`.</summary>
        public const string DesktopCanvasName = "KazistovVvCanvas";

        /// <summary>Компонент, который «везёт» десктопный канвас за головой оператора.</summary>
        public const string PanelAnchorName = "XrPanelAnchor";

        /// <summary>
        /// Панель шлема с обобщающими иконками групп — её гасить НЕЛЬЗЯ. Имя объекта сцены
        /// сохранено прежним (`XrQuickPanel`), чтобы не сломать то, что ищет панель по имени:
        /// меняется ПОВЕДЕНИЕ панели, а не имя её объекта.
        /// </summary>
        public const string VrPanelName = "XrQuickPanel";

        /// <summary>Канвас панели шлема прежней панели (создаётся кодом) — гасить НЕЛЬЗЯ.</summary>
        public const string VrPanelCanvasName = "KvXrQuickCanvas";

        /// <summary>Канвас панели шлема с обобщающими иконками групп (создаётся кодом) — гасить НЕЛЬЗЯ.</summary>
        public const string VrGroupCanvasName = "KvXrGroupCanvas";

        /// <summary>
        /// ЧТО ДЕЛАТЬ С ОБЪЕКТОМ ПО ЕГО ИМЕНИ. Незнакомое имя — «не трогать»: молчаливое
        /// гашение чего-то нужного хуже, чем лишний объект в шлеме.
        /// </summary>
        public static KvXrOverlayDisposition Classify(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return KvXrOverlayDisposition.Keep;

            // Интерфейс ШЛЕМА проверяется ПЕРВЫМ: он тоже канвас, и перепутать его
            // с десктопным значило бы погасить единственную панель управления в шлеме.
            if (IsVrInterface(objectName)) return KvXrOverlayDisposition.Keep;

            if (string.Equals(objectName, PanelAnchorName, StringComparison.Ordinal))
                return KvXrOverlayDisposition.Remove;

            if (string.Equals(objectName, DesktopCanvasName, StringComparison.Ordinal) ||
                string.Equals(objectName, DesktopUiRootName, StringComparison.Ordinal))
                return KvXrOverlayDisposition.HideRendering;

            return KvXrOverlayDisposition.Keep;
        }

        /// <summary>Это интерфейс ШЛЕМА (его гасить нельзя ни при каких условиях).</summary>
        public static bool IsVrInterface(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            return string.Equals(objectName, VrPanelName, StringComparison.Ordinal) ||
                   string.Equals(objectName, VrPanelCanvasName, StringComparison.Ordinal) ||
                   string.Equals(objectName, VrGroupCanvasName, StringComparison.Ordinal);
        }

        /// <summary>
        /// РЕЕСТР КОМАНД ОБЯЗАН ОСТАТЬСЯ ЖИВЫМ. Явная проверка того самого решения: десктопный
        /// интерфейс гасится как РИСОВАНИЕ, а не как объект, поэтому команды продолжают
        /// регистрироваться и панель шлема работает.
        /// </summary>
        public static bool KeepsCommandsAlive(KvXrOverlayDisposition disposition)
        {
            return disposition != KvXrOverlayDisposition.Remove;
        }

        /// <summary>
        /// УБИРАТЬ ЛИ ОБЪЕКТ ИЗ ЦЕЛЕЙ ЛУЧА. Гашение рисования обязано убирать его и из наведения:
        /// иначе НЕВИДИМЫЕ элементы десктопного интерфейса продолжают ловить луч и через задержку
        /// нажиматься. Найдено по журналу шлема 16.10.2026: `Указатель: взгляд на «Blocker» —
        /// держите 1,3 с для нажатия`. Владелец увидел бы «само переключается» без единого
        /// видимого элемента на экране.
        ///
        /// Интерфейс ШЛЕМА при этом обязан оставаться нажимаемым — иначе по панели не щёлкнуть.
        /// </summary>
        public static bool HidesFromPointer(KvXrOverlayDisposition disposition)
        {
            return disposition == KvXrOverlayDisposition.HideRendering;
        }

        /// <summary>
        /// Строка журнала сторожа оверлея — без неё «стало лучше» не считается ([S3.7]).
        /// Числа печатаются с инвариантной культурой: журнал — это доказательство, и его числа
        /// обязаны находиться поиском одинаково на любой системе.
        /// </summary>
        public static string Describe(int hiddenCanvases, int removedAnchors, int keptVrObjects,
                                      int commandsRegistered)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "десктопный оверлей в шлеме: погашено рисование канвасов {0} · убрано «{1}» {2} · " +
                "интерфейс шлема оставлен ({3}) · команд в реестре {4}",
                hiddenCanvases, PanelAnchorName, removedAnchors, keptVrObjects, commandsRegistered);
        }
    }
}
