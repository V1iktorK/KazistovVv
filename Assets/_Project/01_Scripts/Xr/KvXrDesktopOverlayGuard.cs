using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvXr
{
    /// <summary>
    /// ДЕСКТОПНОГО ИНТЕРФЕЙСА В ШЛЕМЕ БЫТЬ НЕ ДОЛЖНО (сессия §35, карточка Б20).
    ///
    /// ЗАДАНИЕ ВЛАДЕЛЬЦА. «Десктопный оверлей инструментов и окон (то, что похоже на интерфейс
    /// для ПК) в шлеме должен быть скрыт — его там быть не должно».
    ///
    /// ЧТО ИМЕННО МЕШАЛО. В `XrMain` живёт проектный канвас (`KazistovVv_UI` / `KazistovVvCanvas`)
    /// с деревом, окнами и тулбаром, а компонент `KvXrPanelAnchor` переводил его в мировое
    /// пространство и вешал перед оператором. Это и есть «интерфейс для ПК» в шлеме.
    ///
    /// ГЛАВНАЯ ЛОВУШКА, РАДИ КОТОРОЙ ЭТОТ КОМПОНЕНТ УСТРОЕН ИМЕННО ТАК. Кажется очевидным:
    /// «десктопный интерфейс не нужен — выключить `KazistovVv_UI`». Это сломало бы панель шлема:
    /// РЕЕСТР КОМАНД (`KvCommands`) наполняет менеджер интерфейса, живущий на ТОМ ЖЕ объекте.
    /// Выключив объект, мы получили бы панель с иконками групп, за которыми нет ни одной команды:
    /// в шлеме не нажималось бы ничего, и при этом в журнале не было бы ни одной ошибки.
    ///
    /// ПОЭТОМУ ГАСИТСЯ РИСОВАНИЕ, А НЕ ОБЪЕКТ: у канваса выключается компонент `Canvas`
    /// (`canvas.enabled = false`). Компоненты на объекте продолжают работать, команды —
    /// регистрироваться, а на экран шлема не выводится ничего.
    ///
    /// ПОЧЕМУ ПРОВЕРКА ИДЁТ ПОВТОРНО. Окна интерфейса создаются кодом во время работы, и канвасы
    /// могут появляться позже первого кадра. Однократного гашения не хватило бы: окно, открытое
    /// на десятой секунде, снова оказалось бы перед глазами.
    ///
    /// ЭТОТ КОМПОНЕНТ ЖИВЁТ ТОЛЬКО В XR-СЦЕНЕ. В десктопную сцену он не добавляется, поэтому
    /// десктопный режим им не затрагивается вовсе ([S3.2]).
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrDesktopOverlayGuard : MonoBehaviour
    {
        /// <summary>Как часто перепроверять появление новых канвасов, секунды.</summary>
        public float checkIntervalSeconds = 1f;

        /// <summary>Выключать компонент `KvXrPanelAnchor`, если он всё же оказался в сцене.</summary>
        public bool neutralizePanelAnchor = true;

        /// <summary>Сообщать итог в журнал (обязательная строка доказательства).</summary>
        public bool logOnGuard = true;

        private float nextCheck;
        private int hiddenCanvases;
        private int hiddenRaycasters;
        private int removedAnchors;
        private int keptVrObjects;
        private bool firstReportDone;
        private bool operatorPanelReportDone;

        void Start()
        {
            Guard();
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + Mathf.Max(0.25f, checkIntervalSeconds);
            Guard();
        }

        /// <summary>
        /// ПОГАСИТЬ РИСОВАНИЕ ДЕСКТОПНОГО ИНТЕРФЕЙСА. Решение по каждому объекту принимает
        /// проверенный тестами <see cref="KvXrOverlayPolicy"/> — здесь только применение.
        /// </summary>
        public void Guard()
        {
            hiddenCanvases = 0;
            hiddenRaycasters = 0;
            removedAnchors = 0;
            keptVrObjects = 0;

            // --- 1. Канвасы десктопного интерфейса: гасим РИСОВАНИЕ, объект оставляем живым
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas c = canvases[i];
                if (c == null) continue;

                // Интерфейс ШЛЕМА проверяется первым: он тоже канвас, и погасить его значило бы
                // оставить владельца без единственной панели управления.
                if (KvXrOverlayPolicy.IsVrInterface(c.gameObject.name))
                {
                    keptVrObjects++;
                    continue;
                }

                // КАНВАС ПАНЕЛЕЙ ОПЕРАТОРА (R6-1, третий круг приёмки 12.11.2026). У него своё
                // правило: он гасится как десктопный интерфейс, НО только пока в нём нет панели,
                // которую оператор открыл командой. Иначе на планшете в AR команда «Настройки»
                // выполнялась, а панели не было: канвас гасил именно этот сторож, и гасил бы
                // снова каждый тик, сколько бы панель ни включали из команды.
                //
                // Вложенные канвасы панелей отдельно не трогаем вовсе: их судьбу решает корневой
                // канвас панелей — под выключенным корнем не рисуется ничего, а «погашенный»
                // вложенный канвас помешал бы панели, когда корень снова включат.
                if (KvXrOperatorPanelCanvas.IsOperatorPanelCanvas(c))
                {
                    if (!KvXrOperatorPanelCanvas.IsDockRootCanvas(c)) continue;
                    if (Decide(c.gameObject.name, true, HostsRequestedPanel(c),
                               KvXrInputSource.HeadsetPresent) == KvXrOverlayDisposition.Keep)
                    {
                        ReportOperatorPanelsKept(c);
                        continue;   // рисование и райкастер остаются: панель открыта по команде
                    }
                }
                else if (Decide(c.gameObject.name, BelongsToDesktopUi(c.transform), false) ==
                         KvXrOverlayDisposition.Keep) continue;

                if (c.enabled) c.enabled = false;         // рисование выключено, логика продолжает работать
                hiddenCanvases++;

                // И РАЙКАСТЕР ТОЖЕ. Погасить только рисование НЕДОСТАТОЧНО: невидимые элементы
                // десктопного интерфейса остаются целями для луча и продолжают нажиматься.
                // Журнал шлема 16.10.2026 показал это прямо: `Указатель: взгляд на «Blocker» —
                // держите 1,3 с для нажатия` — то есть луч наводился на НЕВИДИМЫЙ объект интерфейса,
                // и через 1,3 с по нему происходило нажатие. Владелец при этом видел бы, что
                // в шлеме «что-то само переключается», и причину найти было бы нечем.
                var raycaster = c.GetComponent<GraphicRaycaster>();
                if (raycaster != null && raycaster.enabled)
                {
                    raycaster.enabled = false;
                    hiddenRaycasters++;
                }
            }

            // --- 2. Якорь панели: он и вешает десктопный канвас перед оператором
            if (neutralizePanelAnchor)
            {
                KvXrPanelAnchor[] anchors = Object.FindObjectsByType<KvXrPanelAnchor>(FindObjectsInactive.Include);
                for (int i = 0; i < anchors.Length; i++)
                {
                    KvXrPanelAnchor a = anchors[i];
                    if (a == null) continue;
                    if (a.enabled) a.enabled = false;
                    removedAnchors++;
                }
            }

            if (logOnGuard && !firstReportDone)
            {
                firstReportDone = true;
                Debug.Log("[KvXr] " + KvXrOverlayPolicy.Describe(hiddenCanvases, removedAnchors,
                    keptVrObjects, CountResolvableCommands()) +
                    " · невидимых целей луча отключено " + hiddenRaycasters +
                    " (иначе луч нажимал бы невидимые кнопки десктопного интерфейса)" +
                    " · команд в реестре достаточно для панели: " + (CountResolvableCommands() > 0 ? "да" : "НЕТ"));
            }
        }

        /// <summary>
        /// РЕШЕНИЕ ПО ОДНОМУ КАНВАСУ — ЧИСТАЯ ФУНКЦИЯ (R6-1, третий круг приёмки 12.11.2026).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНО ОТ `Guard()`. `Guard()` обходит сцену — в тесте без сцены его не
        /// проверить. А решение «гасить или оставить» обязано быть проверяемым: именно здесь
        /// ошиблись две предыдущие версии правки R6-1 (гасили канвас, в котором лежит открытая
        /// оператором панель), и именно здесь теперь стоит правило, а не порядок строк в цикле.
        ///
        /// ЧТО ЗНАЧИТ `belongsToDesktopUi`. Канвас принадлежит десктопному интерфейсу — по своему
        /// имени или по имени предка (`BelongsToDesktopUi`). Канвас панелей оператора проверяется
        /// РАНЬШЕ этого признака: он тоже «десктопный», но у него отдельное правило.
        /// </summary>
        public static KvXrOverlayDisposition Decide(string canvasName, bool belongsToDesktopUi,
                                                       bool hostsRequestedPanel,
                                                       bool headsetPresent = false)
        {
            // Интерфейс шлема не трогаем никогда: это единственная панель управления в шлеме.
            if (KvXrOverlayPolicy.IsVrInterface(canvasName)) return KvXrOverlayDisposition.Keep;

            // Панели оператора: гасим, только пока оператор ничего не открывал, и всегда — в шлеме
            // (там настройки открываются мировым окном, а экранная панель была бы десктопным
            // оверлеем, который владелец запретил).
            if (KvXrOverlayPolicy.IsDockCanvas(canvasName))
                return KvXrOverlayPolicy.ClassifyDockCanvas(hostsRequestedPanel, headsetPresent);

            return belongsToDesktopUi
                ? KvXrOverlayDisposition.HideRendering
                : KvXrOverlayDisposition.Keep;
        }

        /// <summary>
        /// В КАНВАСЕ ЕСТЬ ПАНЕЛЬ, КОТОРУЮ ОПЕРАТОР ОТКРЫЛ КОМАНДОЙ.
        ///
        /// Условие двойное и оба слагаемых обязательны: `OperatorRequested` (панель запросили
        /// ИМЕННО СЕЙЧАС, а не она пришла из сохранённой раскладки) и `Shown` (панель на экране).
        /// Одного `Shown` мало: раскладка из `PlayerPrefs` включает панель дерева на старте, и по
        /// такому признаку сторож оставил бы десктопный оверлей в шлеме — ровно то, что владелец
        /// запретил.
        /// </summary>
        private static bool HostsRequestedPanel(Canvas canvas)
        {
            if (canvas == null) return false;
            KazistovVvUI.KvDockPanel[] panels =
                canvas.GetComponentsInChildren<KazistovVvUI.KvDockPanel>(true);
            for (int i = 0; i < panels.Length; i++)
            {
                KazistovVvUI.KvDockPanel panel = panels[i];
                if (panel == null) continue;
                if (panel.OperatorRequested && panel.Shown) return true;
            }
            return false;
        }

        /// <summary>
        /// СТРОКА В ЖУРНАЛ О ПАНЕЛЯХ ОПЕРАТОРА. Печатается один раз, по появлению: иначе она
        /// повторялась бы каждый тик сторожа. По этой строке на устройстве видно, что путь
        /// «команда → панель» дошёл до рендерера, а не остановился на флаге (находка R6-1).
        /// </summary>
        private void ReportOperatorPanelsKept(Canvas canvas)
        {
            if (!logOnGuard || operatorPanelReportDone) return;
            operatorPanelReportDone = true;
            Debug.Log("[KvXr] ПАНЕЛИ: канвас «" + canvas.name + "» оставлен включённым — " +
                      "в нём открытая по команде панель оператора (R6-1)");
        }

        /// <summary>
        /// ЖИВ ЛИ РЕЕСТР КОМАНД. Это и есть доказательство, что гашение рисования не убило логику:
        /// если бы менеджер интерфейса выключился вместе с канвасом, здесь был бы ноль, и панель
        /// с обобщающими иконками групп оказалась бы пустой.
        /// </summary>
        public static int CountResolvableCommands()
        {
            int found = 0;
            KazistovVvUI.KvToolbarGroup[] groups = KazistovVvUI.KvToolbarGroups.All;
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g].Commands == null) continue;
                for (int i = 0; i < groups[g].Commands.Length; i++)
                {
                    string id = groups[g].Commands[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    if (KazistovVvUI.KvCommands.Get(id) != null) found++;
                }
            }
            return found;
        }

        /// <summary>
        /// ЭТОТ КАНВАС ПРИНАДЛЕЖИТ ДЕСКТОПНОМУ ИНТЕРФЕЙСУ? Признак — сам канвас назван
        /// `KazistovVvCanvas` либо находится внутри корня `KazistovVv_UI`. Окна проекта живут
        /// на разных канвасах, поэтому проверка идёт по РОДИТЕЛЯМ, а не только по имени.
        /// </summary>
        private static bool BelongsToDesktopUi(Transform t)
        {
            if (t == null) return false;
            if (KvXrOverlayPolicy.Classify(t.name) == KvXrOverlayDisposition.HideRendering) return true;

            Transform current = t.parent;
            int guard = 0;
            while (current != null && guard++ < 64)
            {
                if (KvXrOverlayPolicy.Classify(current.name) == KvXrOverlayDisposition.HideRendering)
                    return true;
                current = current.parent;
            }
            return false;
        }
    }
}
