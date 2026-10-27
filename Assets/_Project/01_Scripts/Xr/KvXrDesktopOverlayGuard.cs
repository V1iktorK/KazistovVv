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

                if (!BelongsToDesktopUi(c.transform)) continue;

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
