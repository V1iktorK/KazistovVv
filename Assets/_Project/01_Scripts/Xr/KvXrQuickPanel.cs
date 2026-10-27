using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvXr
{
    /// <summary>
    /// ПАНЕЛЬ БЫСТРОГО ВЫБОРА У РУКИ (решение владельца [D20], сессия §32).
    ///
    /// ЧТО ДЕЛАЕТ. Строит небольшую панель кнопок у выбранной руки — «бубликом» (по кругу) или
    /// «полумесяцем» (по дуге сверху) — и держит её перед оператором: панель следует за рукой
    /// и разворачивается к лицу. Кнопки берутся из РЕЕСТРА КОМАНД проекта (`KvCommands`),
    /// поэтому нажатие делает ровно то же, что пункт меню или кнопка тулбара: второй реализации
    /// действий не появляется.
    ///
    /// ПОЧЕМУ КНОПКИ У РУКИ, А НЕ ЛИСТОМ СВЕРХУ. Прежняя формулировка [D8] («один лист кнопок
    /// сверху») заменена решением [D20]: лист закрывает обзор и требует уводить луч от работы,
    /// а панель у руки всегда под ладонью и не мешает целиться другой рукой.
    ///
    /// ГДЕ ЛОГИКА, А ГДЕ РИСОВАНИЕ. Вся раскладка (углы, смещения, сторона руки, границы значений)
    /// живёт в `KvXrQuickPanelLayout` и проверена тестами без шлема. Здесь — только построение
    /// объектов и слежение за рукой, то есть тот код, который нельзя проверить иначе как запуском.
    ///
    /// ЧЕСТНОЕ ОГРАНИЧЕНИЕ: панель строит свои объекты во время работы (как и остальной интерфейс
    /// проекта — он строится кодом, префабов нет). Поэтому её проверка — это журнал запуска
    /// (строка `[KvXr] панель у руки: …`) и глаза оператора в шлеме.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrQuickPanel : MonoBehaviour
    {
        /// <summary>Камера шлема: панель разворачивается к ней.</summary>
        public Camera headCamera;

        /// <summary>Порядок отрисовки: выше панели функций (45), ниже HUD режима точки (60).</summary>
        public int sortingOrder = 55;

        /// <summary>Насколько панель выносится от кисти к лицу, метры.</summary>
        public float forwardOffset = 0.14f;

        /// <summary>Насколько панель поднимается над кистью, метры (чтобы не закрывать ладонь).</summary>
        public float upOffset = 0.06f;

        /// <summary>Размер кнопки в пикселях канваса.</summary>
        public float buttonSize = 76f;

        /// <summary>
        /// ЗАПАСНОЕ ПОЛОЖЕНИЕ ПАНЕЛИ, КОГДА РУКИ НЕ ВИДНО (нет контроллеров). Панель встаёт
        /// перед глазами: 1,15 м вперёд и на 0,25 м ниже уровня глаз — так её видно, не задирая
        /// голову, и она не перекрывает вид на робота.
        /// </summary>
        public const float FallbackDistanceMeters = 1.15f;

        /// <summary>Насколько ниже уровня глаз ставится панель в запасном положении, метры.</summary>
        public const float FallbackDropMeters = 0.25f;

        /// <summary>Сказано ли в журнал про запасное положение (чтобы не повторять каждую секунду).</summary>
        private bool fallbackLogged;

        /// <summary>Размер канваса панели в пикселях (квадрат).</summary>
        public float canvasSize = 420f;

        /// <summary>
        /// КОМАНДЫ ПАНЕЛИ ПО УМОЛЧАНИЮ — те, что нужны в шлеме чаще всего. Идентификаторы взяты
        /// из реестра команд проекта; незнакомый идентификатор НЕ ломает панель, а пропускается
        /// с записью в журнал (реестр может измениться — панель обязана это пережить).
        /// </summary>
        public string[] commandIds =
        {
            "robot.stop", "robot.playpause", "edit.undo", "edit.redo",
            "edit.reset", "view.workspace", "view.limits", "panel.settings"
        };

        /// <summary>Сообщать состояние панели в журнал при построении.</summary>
        public bool logOnBuild = true;

        private Canvas canvas;
        private RectTransform canvasRect;
        private readonly List<GameObject> buttons = new List<GameObject>();
        private string builtSignature = "";
        private bool builtOnce;

        /// <summary>Сколько кнопок реально построено (после отсева неизвестных команд).</summary>
        public int ButtonCount { get { return buttons.Count; } }

        /// <summary>Строка состояния панели для журнала (та же, что печатается при построении).</summary>
        public string Status { get; private set; }

        void Start()
        {
            if (headCamera == null) headCamera = GetComponentInParent<Camera>();
            Rebuild(true);
        }

        void Update()
        {
            // Настройки могли измениться в панели настроек — пересобираем только при изменении.
            if (builtSignature != CurrentSignature()) Rebuild(false);

            if (canvas == null) return;

            bool visible = KvXrSettings.GetQuickPanelVisible() && KvXrInputSource.HeadsetPresent;
            if (canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
            if (!visible) return;

            FollowHand();
        }

        /// <summary>Подпись настроек: по ней видно, что панель надо пересобрать.</summary>
        private string CurrentSignature()
        {
            return ((int)KvXrSettings.GetQuickPanelShape()) + "|" +
                   ((int)KvXrSettings.GetQuickPanelHand()) + "|" +
                   KvXrSettings.GetQuickPanelButtonCount() + "|" +
                   KvXrSettings.GetQuickPanelRadius().ToString("0.000");
        }

        /// <summary>Собрать (или пересобрать) панель по текущим настройкам. Идемпотентно.</summary>
        public void Rebuild(bool firstTime)
        {
            builtSignature = CurrentSignature();
            BuildWith(KvXrSettings.GetQuickPanelShape(), KvXrSettings.GetQuickPanelHand(),
                KvXrSettings.GetQuickPanelButtonCount(), KvXrSettings.GetQuickPanelRadius(), firstTime);
        }

        /// <summary>
        /// ПОСТРОИТЬ ПАНЕЛЬ С ЯВНЫМИ ПАРАМЕТРАМИ (без чтения настроек). Нужно проверке без шлема:
        /// она собирает все четыре сочетания (бублик/полумесяц × левая/правая рука) и печатает
        /// результат в журнал. Так проверяются обе формы и обе стороны, а не только выбранная.
        /// </summary>
        public void BuildWith(KvXrQuickShape shape, KvXrQuickHand hand, int buttonsWanted,
                              float radius, bool log = false)
        {
            if (canvas == null) CreateCanvas();
            if (canvas == null) return;

            ClearButtons();

            var resolved = new List<string>();
            var skipped = new List<string>();
            for (int i = 0; i < commandIds.Length && resolved.Count < buttonsWanted; i++)
            {
                string id = commandIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (KazistovVvUI.KvCommands.Get(id) == null) { skipped.Add(id); continue; }
                resolved.Add(id);
            }

            int count = KvXrQuickPanelLayout.ClampCount(resolved.Count);
            float scale = (KvXrQuickPanelLayout.ClampRadius(radius) * 2.6f) / canvasSize;
            canvasRect.localScale = new Vector3(scale, scale, scale);

            for (int i = 0; i < resolved.Count; i++)
            {
                Vector2 offsetMeters = KvXrQuickPanelLayout.OffsetMeters(i, resolved.Count, shape,
                    KvXrQuickPanelLayout.ClampRadius(radius), hand);
                Vector2 px = offsetMeters / scale;                 // метры → пиксели канваса
                CreateButton(resolved[i], px, i);
            }

            Status = KvXrQuickPanelLayout.Describe(shape, hand, resolved.Count, radius).Replace("панель у руки: ", "") +
                     " · кнопок построено " + buttons.Count +
                     (skipped.Count > 0 ? " · не найдено в реестре команд: " + string.Join(", ", skipped.ToArray()) : "");

            if (log || logOnBuild)
                Debug.Log("[KvXr] панель у руки: " + Status);
        }

        private void CreateCanvas()
        {
            GameObject canvasGo = new GameObject("KvXrQuickCanvas", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.hideFlags = HideFlags.HideInHierarchy;

            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;

            canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(canvasSize, canvasSize);
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
        }

        private void CreateButton(string commandId, Vector2 positionPx, int index)
        {
            var go = new GameObject("Quick_" + commandId, typeof(Image), typeof(Button));
            go.transform.SetParent(canvasRect, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(buttonSize, buttonSize);
            rect.anchoredPosition = positionPx;

            Image image = go.GetComponent<Image>();
            image.sprite = KazistovVvUI.KvTheme.WhiteSprite;
            image.color = index == 0
                ? new Color(0.20f, 0.55f, 0.95f, 0.92f)     // первая кнопка (сверху) — «главная»
                : new Color(0.16f, 0.16f, 0.18f, 0.88f);

            Button button = go.GetComponent<Button>();
            string captured = commandId;
            button.onClick.AddListener(delegate { InvokeCommand(captured); });

            string title = TitleOf(commandId);
            Text label = KazistovVvUI.KvTheme.CreateText(rect, "Label", title,
                11, TextAnchor.MiddleCenter, KazistovVvUI.KvTheme.TextMain);
            KazistovVvUI.KvTheme.Stretch(label.rectTransform, 2f, 2f, 2f, 2f);

            buttons.Add(go);
        }

        /// <summary>Подпись кнопки: короткое название команды из реестра.</summary>
        private static string TitleOf(string commandId)
        {
            KazistovVvUI.KvCommand command = KazistovVvUI.KvCommands.Get(commandId);
            if (command == null) return commandId;
            string title = command.Title;
            if (string.IsNullOrEmpty(title)) return commandId;
            return title.Length > 14 ? title.Substring(0, 13) + "…" : title;
        }

        /// <summary>
        /// НАЖАТИЕ КНОПКИ: то же действие, что у пункта меню. Если команда отключена (заглушка),
        /// это видно в журнале — молча ничего не делать нельзя, иначе кнопка «не работает».
        /// </summary>
        private void InvokeCommand(string commandId)
        {
            KazistovVvUI.KvCommand command = KazistovVvUI.KvCommands.Get(commandId);
            if (command == null)
            {
                Debug.LogWarning("[KvXr] панель у руки: команда не найдена: " + commandId);
                return;
            }
            if (!command.Enabled)
            {
                Debug.Log("[KvXr] панель у руки: команда недоступна (заглушка): " + commandId);
                return;
            }
            if (!KazistovVvUI.KvCommands.Invoke(commandId))
                Debug.LogWarning("[KvXr] панель у руки: команда не выполнилась: " + commandId);
        }

        /// <summary>Панель следует за рукой и разворачивается к лицу оператора.</summary>
        private void FollowHand()
        {
            KvXrQuickHand hand = KvXrSettings.GetQuickPanelHand();
            Vector3 position;
            Quaternion rotation;

            bool hasPose = hand == KvXrQuickHand.Right
                ? KvXrInputSource.TryRightPose(out position, out rotation)
                : KvXrInputSource.TryLeftPose(out position, out rotation);

            if (!hasPose)
            {
                // РУКИ НЕ ВИДНО. Прежнее поведение — спрятать панель («не зависает в воздухе») —
                // оказалось плохим на живом устройстве 15.10.2026: контроллеры приложению не
                // отдаются вовсе, и владелец не видел панель НИКОГДА, хотя в журнале она «построена,
                // кнопок 7». Теперь панель встаёт ПЕРЕД ГЛАЗАМИ: пусть не у руки, зато её видно
                // и по ней можно понять, что интерфейс жив.
                if (headCamera == null)
                {
                    if (canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
                    return;
                }

                Vector3 gaze = headCamera.transform.forward;
                gaze.y = 0f;
                if (gaze.sqrMagnitude < 0.0001f) gaze = Vector3.forward;
                gaze.Normalize();

                Vector3 fallback = headCamera.transform.position
                                   + gaze * FallbackDistanceMeters
                                   - Vector3.up * FallbackDropMeters;

                transform.position = fallback;
                transform.rotation = Quaternion.LookRotation(gaze, Vector3.up);

                if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
                if (!fallbackLogged)
                {
                    fallbackLogged = true;
                    Debug.Log("[KvXr] Панель у руки: позы руки НЕТ (контроллеры не отдаются) — " +
                              "панель показана перед глазами на " + FallbackDistanceMeters + " м");
                }
                return;
            }

            fallbackLogged = false;

            Vector3 toHead = headCamera != null
                ? (headCamera.transform.position - position)
                : rotation * Vector3.forward;
            if (toHead.sqrMagnitude < 0.0001f) toHead = rotation * Vector3.forward;
            toHead.Normalize();

            transform.position = position + toHead * forwardOffset + Vector3.up * upOffset;
            transform.rotation = Quaternion.LookRotation(toHead, Vector3.up);
        }

        private void ClearButtons()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == null) continue;
                if (Application.isPlaying) Destroy(buttons[i]);
                else DestroyImmediate(buttons[i]);
            }
            buttons.Clear();
        }
    }
}
