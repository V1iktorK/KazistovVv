using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KazistovVvUI;
using KazistovVvXr;

namespace KazistovVvAr
{
    /// <summary>
    /// ЭКРАННЫЙ ИНТЕРФЕЙС ПЛАНШЕТА — В ВИДЕ ПК-ПРИЛОЖЕНИЯ (06.11.2026).
    ///
    /// ВЛАДЕЛЕЦ: «убери подсказки в оверлее “AR. камера есть...”, снизу подсказки можешь оставить.
    /// Переделай оверлей под ПК версию, чтобы ничего не напоминало VR/MR и наполни оверлей
    /// функциями максимально».
    ///
    /// ЧТО ЭТО ЗНАЧИТ НА ПРАКТИКЕ:
    ///   • верхней строки «AR · камера есть · сессия…» больше нет;
    ///   • сверху — меню как в ПК-версии: Файл, Правка, Вид, Робот, Сервис, Справка. Пункты
    ///     меню НАСТОЯЩИЕ: выполняют команды проекта через `KvCommands.Invoke` (так же, как кнопки
    ///     на компьютере), а не показывают текст;
    ///   • под меню — строка инструментов (Скриншот, Запись, Стоп, Сетка, Лимиты, Метрики,
    ///     Настройки, Объекты, Экспорт, Сохранить, Демо) — те же команды;
    ///   • слева — дерево проекта: робот, столы, траектория, постобработка, ограничения,
    ///     калибровка, этапы. Нажатие ДЕЛАЕТ действие или открывает список команд группы;
    ///   • справа — быстрые действия планшета (Поставить, Переставить, Команды, Сброс вида);
    ///   • внизу — подсказка (владелец разрешил оставить снизу) и строка состояния как в ПК;
    ///   • окно «Команды» — все 8 групп и все команды проекта с листанием (222 команды реестра
    ///     доступны без VR-жестов и без луча).
    ///
    /// ЧЕГО НЕТ НАМЕРЕННО: слов «VR», «MR», «шлем», «виртуальная реальность» в интерфейсе
    /// планшета, а также верхней статус-подсказки про камеру/сессию.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArUi : MonoBehaviour
    {
        /// <summary>Опорное разрешение экранного слоя (как у ПК-версии).</summary>
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);

        /// <summary>Высота кнопки в пикселях опорного разрешения (значение по умолчанию — ПК-размер).</summary>
        public float buttonHeight = 96f;

        /// <summary>Ширина кнопки в пикселях опорного разрешения.</summary>
        public float buttonWidth = 300f;

        /// <summary>Строить интерфейс сразу при старте.</summary>
        public bool buildOnStart = true;

        /// <summary>
        /// ВЫСОТА СТРОКИ «ПОД ПАЛЕЦ» — ДОЛЯ ВЫСОТЫ ЭКРАНА (07.11.2026). Замер по фото планшета
        /// 2560×1600: строка тулбара была ≈5,3 мм, дерево проекта ≈5,3 мм, правые кнопки ≈7,7 мм —
        /// это меньше трети пальца, попасть трудно. 6,5 % высоты экрана ≈ 10,4 мм.
        /// </summary>
        public float touchRowScreenFraction = 0.065f;

        /// <summary>Писать в журнал о построении (обязательная строка-доказательство).</summary>
        public bool logOnBuild = true;

        private Canvas canvas;
        private Text footerLeft;
        private Text footerRight;
        private Text hintText;
        private GameObject hintsRow;
        private GameObject messageRoot;
        private Text messageText;
        private Button messageButton;
        private Text messageButtonText;
        private GameObject messageCancelRoot;
        private Action messageCancelAction;
        private Action messageAction;
        private readonly List<Button> buttons = new List<Button>();

        private GameObject menuPanel;
        private GameObject commandPanel;
        private GameObject commandRowsRoot;
        private Text commandTitle;
        private Text commandPageText;
        private string commandGroupId = "robot";
        private int commandPage;

        private Transform menuBar;
        private Transform toolbar;
        private Transform treePanel;

        /// <summary>Интерфейс построен (для проверок и журнала).</summary>
        public bool Built { get { return canvas != null; } }

        /// <summary>Текст строки состояния — как он выглядит на экране (для теста).</summary>
        public string StatusText { get { return footerLeft != null ? footerLeft.text : string.Empty; } }

        /// <summary>Текст подсказки (для теста).</summary>
        public string HintText { get { return hintText != null ? hintText.text : string.Empty; } }

        /// <summary>Сообщение об отказе показано (для проверок).</summary>
        public bool MessageVisible { get { return messageRoot != null && messageRoot.activeSelf; } }

        /// <summary>Сколько кнопок создано (для проверок).</summary>
        public int ButtonCount { get { return buttons.Count; } }

        /// <summary>Сколько столов создано из интерфейса планшета (для нижней строки).</summary>
        public static int CreatedTableCount { get; private set; }

        /// <summary>Открыта ли панель команд (для проверок).</summary>
        public bool CommandPanelVisible { get { return commandPanel != null && commandPanel.activeSelf; } }

        /// <summary>Открыто ли меню (для проверок).</summary>
        public bool MenuVisible { get { return menuPanel != null && menuPanel.activeSelf; } }

        void Start()
        {
            if (buildOnStart) EnsureBuilt();
        }

        /// <summary>ВСТРОЕННЫЙ ШРИФТ UNITY: единственный, который есть в любом билде.</summary>
        public static Font BuiltinFont
        {
            get
            {
                Font font = null;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch (Exception) { font = null; }
                if (font != null) return font;
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch (Exception) { font = null; }
                return font;
            }
        }

        /// <summary>
        /// ВЫСОТА СТРОКИ ПОД ПАЛЕЦ В ЕДИНИЦАХ КАНВАСА. Считается от фактической высоты экрана и
        /// масштаба канваса, поэтому одинаково читается на 2560×1600 и на другом планшете.
        /// </summary>
        public float TouchRowHeightUi()
        {
            float fraction = Mathf.Clamp(touchRowScreenFraction, 0.03f, 0.12f);
            return Mathf.Round(Mathf.Clamp(CanvasHeightUi() * fraction, 48f, 220f));
        }

        /// <summary>ВЫСОТА ЭКРАНА В ЕДИНИЦАХ КАНВАСА (для раскладки без обрезки снизу).</summary>
        private float CanvasHeightUi()
        {
            return Screen.height > 0 ? Screen.height / UiScaleFactor() : referenceResolution.y;
        }

        /// <summary>ШИРИНА ЭКРАНА В ЕДИНИЦАХ КАНВАСА (строка инструментов обязана влезть целиком).</summary>
        private float CanvasWidthUi()
        {
            return Screen.width > 0 ? Screen.width / UiScaleFactor() : referenceResolution.x;
        }

        /// <summary>
        /// МАСШТАБ КАНВАСА. `canvas.scaleFactor` в момент построения интерфейса ещё равен 1 —
        /// `CanvasScaler` применяет масштаб на своём первом обновлении, то есть ПОСЛЕ постройки.
        /// Из-за этого размеры считались в «сырых» пикселях: на планшете 2560×1600 строка вышла
        /// 143 px вместо 104, а 13 кнопок инструментов заняли 2544 ед. при доступных 1821 —
        /// правые кнопки («Экспорт», «Сохранить», «Демо») уехали за экран (факт по
        /// `Logs/ar-fix2-screen.png`). Поэтому масштаб считаем ТОЙ ЖЕ формулой, что у
        /// `CanvasScaler.ScaleWithScreenSize` с `matchWidthOrHeight = 0.5`.
        /// </summary>
        private float UiScaleFactor()
        {
            // ВАЖНО: `canvas.scaleFactor` в момент постройки равен 1 — это НЕ применённый масштаб,
            // а значение по умолчанию (CanvasScaler выставит своё на первом обновлении). Поэтому
            // проверять его бессмысленно: масштаб всегда считаем формулой CanvasScaler.
            float refW = Mathf.Max(1f, referenceResolution.x);
            float refH = Mathf.Max(1f, referenceResolution.y);
            float logWidth = Mathf.Log(Mathf.Max(1f, Screen.width) / refW, 2f);
            float logHeight = Mathf.Log(Mathf.Max(1f, Screen.height) / refH, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, UiScaleMatch));
            return scale > 0.01f ? scale : 1f;
        }

        /// <summary>Как в `CanvasScaler`: 0 — по ширине, 1 — по высоте, 0,5 — поровну.</summary>
        private const float UiScaleMatch = 0.5f;

        /// <summary>ПОСТРОИТЬ ИНТЕРФЕЙС (идемпотентно).</summary>
        public void EnsureBuilt()
        {
            if (canvas != null) return;

            EnsureEventSystem();

            GameObject canvasGo = new GameObject("ArUiCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = UiScaleMatch;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildPcChrome(canvasGo.transform);
            BuildHint(canvasGo.transform);
            BuildButtonRow(canvasGo.transform);
            BuildMessage(canvasGo.transform);
            HideMessage();

            if (logOnBuild)
            {
                float rowUi = TouchRowHeightUi();
                Debug.Log("[KvAr] ИНТЕРФЕЙС: ПК-вид построен · меню 6 · дерево · инструменты · " +
                          "опорное разрешение " + referenceResolution.x.ToString("0") + "×" +
                          referenceResolution.y.ToString("0") +
                          " · канвас " + CanvasWidthUi().ToString("0") + "×" +
                          CanvasHeightUi().ToString("0") + " ед. · масштаб " +
                          UiScaleFactor().ToString("0.000") +
                          " · строка под палец " + rowUi.ToString("0") + " ед. (≈" +
                          (rowUi * UiScaleFactor()).ToString("0") + " px)");
            }
        }

        // ================================================================ ПК-КАРКАС

        private void BuildPcChrome(Transform parent)
        {
            Color bar = new Color(0.035f, 0.045f, 0.055f, 0.96f);
            Color line = new Color(0.78f, 0.82f, 0.86f, 0.95f);

            // РАЗМЕРЫ ПОД ПАЛЕЦ: высота строки считается от экрана (≈10 мм), а не берётся
            // «как на ПК». Раскладка ПК-вида сохраняется — меняются только высоты строк.
            float touch = TouchRowHeightUi();
            float menuHeight = touch;
            float toolbarHeight = touch;
            float statusHeight = Mathf.Round(Mathf.Max(48f, touch * 0.62f));
            float buttonHeightUi = touch - 8f;
            // СТРОКА ИНСТРУМЕНТОВ ОБЯЗАНА ВЛЕЗТЬ ЦЕЛИКОМ: ширина кнопок считается от ширины
            // экрана. Раньше 13 кнопок по 142 ед. требовали 1950 ед. при экране 1859 ед. —
            // последняя («Демо») не попадала на экран вовсе (факт по Logs/ar-fix-screen.png).
            float toolbarPitch = Mathf.Max(96f, (CanvasWidthUi() - 16f) / 13f);
            float toolbarButtonWidth = Mathf.Max(88f, toolbarPitch - 8f);
            int fontSizeMenu = Mathf.RoundToInt(Mathf.Clamp(touch * 0.30f, 22f, 34f));
            int fontSizeTools = Mathf.RoundToInt(Mathf.Clamp(touch * 0.27f, 20f, 30f));
            int fontSizeFooter = Mathf.RoundToInt(Mathf.Clamp(touch * 0.30f, 22f, 34f));

            // ВЕРХНЕЕ МЕНЮ С РАБОЧИМИ ПУНКТАМИ
            GameObject menu = new GameObject("PcMenuBar", typeof(RectTransform), typeof(Image));
            menu.transform.SetParent(parent, false);
            RectTransform menuRect = (RectTransform)menu.transform;
            menuRect.anchorMin = new Vector2(0f, 1f);
            menuRect.anchorMax = new Vector2(1f, 1f);
            menuRect.pivot = new Vector2(0.5f, 1f);
            menuRect.anchoredPosition = Vector2.zero;
            menuRect.sizeDelta = new Vector2(0f, menuHeight);
            menu.GetComponent<Image>().color = bar;
            menu.GetComponent<Image>().raycastTarget = false;
            menuBar = menu.transform;

            string[] menus = { "Файл", "Правка", "Вид", "Робот", "Сервис", "Справка" };
            for (int i = 0; i < menus.Length; i++)
            {
                int index = i;
                CreateButton(menu.transform, "Menu_" + menus[i], menus[i],
                    delegate { ToggleMenu(menus[index]); },
                    new Vector2(152f, buttonHeightUi), fontSizeMenu,
                    new Vector2(8f + i * 158f, 4f), TextAnchor.MiddleCenter);
            }

            // СТРОКА ИНСТРУМЕНТОВ (как панель инструментов ПК)
            GameObject tools = new GameObject("PcToolbar", typeof(RectTransform), typeof(Image));
            tools.transform.SetParent(parent, false);
            RectTransform toolsRect = (RectTransform)tools.transform;
            toolsRect.anchorMin = new Vector2(0f, 1f);
            toolsRect.anchorMax = new Vector2(1f, 1f);
            toolsRect.pivot = new Vector2(0.5f, 1f);
            toolsRect.anchoredPosition = new Vector2(0f, -menuHeight);
            toolsRect.sizeDelta = new Vector2(0f, toolbarHeight);
            tools.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.04f, 0.92f);
            tools.GetComponent<Image>().raycastTarget = false;
            toolbar = tools.transform;

            string[][] quick =
            {
                new[] { "Очистить", "workspace.clear" },
                new[] { "Скриншот", "shot.screenshot" },
                new[] { "Запись", "record.toggle" },
                new[] { "Стоп", "estop" },
                new[] { "Сетка", "tool.grid" },
                new[] { "Лимиты", "view.limits" },
                new[] { "Метрики", "view.metrics" },
                new[] { "Раб.зона", "view.workspace" },
                new[] { "Настройки", "ui.settings" },
                new[] { "Объекты", "view.panel.tree" },
                new[] { "Экспорт", "file.export" },
                new[] { "Сохранить", "session.save" },
                new[] { "Демо", "demo.quick" }
            };
            for (int i = 0; i < quick.Length; i++)
            {
                string label = quick[i][0];
                string id = quick[i][1];
                CreateButton(toolbar, "Tool_" + id, label, delegate { Execute(id); },
                    new Vector2(toolbarButtonWidth, buttonHeightUi), fontSizeTools,
                    new Vector2(8f + i * toolbarPitch, 4f), TextAnchor.MiddleCenter);
            }

            // ДЕРЕВО ПРОЕКТА СЛЕВА — КНОПКИ, КОТОРЫЕ ДЕЙСТВУЮТ
            GameObject tree = new GameObject("PcTreePanel", typeof(RectTransform), typeof(Image));
            tree.transform.SetParent(parent, false);
            RectTransform treeRect = (RectTransform)tree.transform;
            treeRect.anchorMin = new Vector2(0f, 0f);
            treeRect.anchorMax = new Vector2(0f, 1f);
            treeRect.pivot = new Vector2(0f, 0f);
            treeRect.anchoredPosition = new Vector2(0f, statusHeight);
            // ВЫСОТА = экран − строка состояния − меню − инструменты. Без вычитания строки
            // состояния панель дерева заезжала на строку инструментов и закрывала первые
            // кнопки «Очистить» и «Скриншот» (видно на снимке Logs/ar-fix-screen.png).
            treeRect.sizeDelta = new Vector2(300f, -(statusHeight + menuHeight + toolbarHeight));
            Image treeBg = tree.GetComponent<Image>();
            treeBg.color = new Color(0.03f, 0.04f, 0.05f, 0.9f);
            treeBg.raycastTarget = false;
            treePanel = tree.transform;

            string[][] treeRows =
            {
                new[] { "6-осевой робот", "robot.model.stand_6axis" },
                new[] { "SCARA LS10-B702S", "robot.model.scara_ls10b702s" },
                new[] { "Квадратный стол", "table.square" },
                new[] { "Авто стол", "table.auto" },
                new[] { "Траектория", "group:path" },
                new[] { "Постобработка", "group:post" },
                new[] { "Ограничения", "view.limits" },
                new[] { "Калибровка", "calib.tab" },
                new[] { "Этапы 13–36", "features.open" },
                new[] { "Все команды", "group:robot" }
            };
            float treePitch = touch + 8f;
            float treeRowHeight = touch;
            // ДЕРЕВО ОБЯЗАНО ВЛЕЗТЬ ЦЕЛИКОМ: если строки «под палец» не помещаются по высоте,
            // сжимаем ИХ, а не обрезаем последние пункты (иначе «Все команды» недоступны).
            float treeSpace = CanvasHeightUi() - menuHeight - toolbarHeight - statusHeight - 16f;
            float fitPitch = treeSpace / Mathf.Max(1, treeRows.Length);
            if (fitPitch < treePitch)
            {
                treePitch = Mathf.Max(32f, fitPitch);
                treeRowHeight = Mathf.Max(28f, treePitch - 6f);
            }
            for (int i = 0; i < treeRows.Length; i++)
            {
                string label = treeRows[i][0];
                string action = treeRows[i][1];
                CreateButton(treePanel, "Tree_" + label, "▸ " + label,
                    delegate { TreeAction(action); },
                    new Vector2(284f, treeRowHeight), fontSizeTools,
                    new Vector2(8f, -8f - i * treePitch), TextAnchor.MiddleLeft);
            }

            // НИЖНЯЯ СТРОКА СОСТОЯНИЯ (как в ПК; без верхней подсказки)
            GameObject bottom = new GameObject("PcStatusBar", typeof(RectTransform), typeof(Image));
            bottom.transform.SetParent(parent, false);
            RectTransform bottomRect = (RectTransform)bottom.transform;
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomRect.sizeDelta = new Vector2(0f, statusHeight);
            Image bottomBg = bottom.GetComponent<Image>();
            bottomBg.color = bar;
            bottomBg.raycastTarget = false;

            // СТРОКА СОСТОЯНИЯ: левая и правая части не должны наезжать друг на друга.
            // Раньше оба текста растягивались на всю ширину, и «Стол: нет» сходился с
            // «Камера: вкл» (видно на снимке Logs/ar-fix-screen.png).
            float footerSplit = CanvasWidthUi() * 0.56f;
            footerLeft = AddText(bottom.transform, "FooterLeft", "Готово", fontSizeFooter,
                line, TextAnchor.MiddleLeft, new Vector2(16f, 0f),
                new Vector2(-(CanvasWidthUi() - footerSplit), 0f));
            footerRight = AddText(bottom.transform, "FooterRight", "Камера: вкл",
                fontSizeFooter, line,
                TextAnchor.MiddleRight, new Vector2(footerSplit, 0f), new Vector2(-16f, 0f));

            // ПАНЕЛЬ МЕНЮ (выпадающая) И ПАНЕЛЬ КОМАНД — создаются один раз
            BuildMenuPanel(parent);
            BuildCommandPanel(parent);
        }

        private void BuildMenuPanel(Transform parent)
        {
            menuPanel = new GameObject("PcMenuDropdown", typeof(RectTransform), typeof(Image));
            menuPanel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)menuPanel.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            // Под меню и тулбаром (их высота теперь «под палец», а не 50 + 52 пикселя).
            rect.anchoredPosition = new Vector2(8f, -(TopChromeHeightUi() + 8f));
            rect.sizeDelta = new Vector2(620f, 600f);
            Image bg = menuPanel.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.06f, 0.98f);
            menuPanel.SetActive(false);
        }

        /// <summary>ВЫСОТА ВЕРХНЕГО КАРКАСА (меню + строка инструментов), единицы канваса.</summary>
        private float TopChromeHeightUi()
        {
            return TouchRowHeightUi() * 2f;
        }

        /// <summary>ВЫСОТА НИЖНЕЙ СТРОКИ СОСТОЯНИЯ, единицы канваса.</summary>
        private float StatusBarHeightUi()
        {
            return Mathf.Round(Mathf.Max(48f, TouchRowHeightUi() * 0.62f));
        }

        /// <summary>ВЫСОТА ВКЛАДОК ГРУПП В ПАНЕЛИ КОМАНД, единицы канваса.</summary>
        private float TabHeightUi()
        {
            return Mathf.Round(Mathf.Max(38f, TouchRowHeightUi() * 0.72f));
        }

        private void BuildCommandPanel(Transform parent)
        {
            commandPanel = new GameObject("PcCommandPanel", typeof(RectTransform), typeof(Image));
            commandPanel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)commandPanel.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(308f, StatusBarHeightUi());
            rect.sizeDelta = new Vector2(900f, -TopChromeHeightUi());
            Image bg = commandPanel.GetComponent<Image>();
            bg.color = new Color(0.035f, 0.045f, 0.055f, 0.97f);

            commandTitle = AddText(commandPanel.transform, "CommandTitle", "Команды", 24,
                new Color(0.9f, 0.93f, 0.96f), TextAnchor.MiddleLeft,
                new Vector2(14f, 0f), new Vector2(-300f, 0f));
            RectTransform titleRect = (RectTransform)commandTitle.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -6f);
            titleRect.sizeDelta = new Vector2(0f, 38f);

            commandPageText = AddText(commandPanel.transform, "CommandPage", "", 20,
                new Color(0.7f, 0.75f, 0.8f), TextAnchor.MiddleRight,
                new Vector2(0f, 0f), new Vector2(-14f, 0f));
            RectTransform pageRect = (RectTransform)commandPageText.transform;
            pageRect.anchorMin = new Vector2(0f, 1f);
            pageRect.anchorMax = new Vector2(1f, 1f);
            pageRect.pivot = new Vector2(1f, 1f);
            pageRect.anchoredPosition = new Vector2(0f, -6f);
            pageRect.sizeDelta = new Vector2(-400f, 38f);

            commandRowsRoot = new GameObject("CommandRows", typeof(RectTransform));
            commandRowsRoot.transform.SetParent(commandPanel.transform, false);
            RectTransform rowsRect = (RectTransform)commandRowsRoot.transform;
            rowsRect.anchorMin = Vector2.zero;
            rowsRect.anchorMax = Vector2.one;
            rowsRect.offsetMin = new Vector2(10f, 10f);
            // Строки начинаются ПОД вкладками групп (их высота теперь «под палец»).
            rowsRect.offsetMax = new Vector2(-10f, -(52f + TabHeightUi() + 10f));

            // ГРУППЫ КОМАНД (8) — вкладки с КОРОТКИМИ именами (как вкладки ПК-панели)
            KvToolbarGroup[] groups = KvToolbarGroups.All;
            string[] shortNames = { "Робот", "Траектория", "Постобр.", "Визуал.", "Экспорт", "Сеть", "Настройки", "Прочее" };
            float tabHeight = TabHeightUi();
            for (int i = 0; i < groups.Length && i < 8; i++)
            {
                string id = groups[i].Id;
                string shortTitle = i < shortNames.Length ? shortNames[i] : ShortGroupTitle(groups[i].Title);
                CreateButton(commandPanel.transform, "GroupTab_" + id, shortTitle,
                    delegate { OpenCommands(id); },
                    new Vector2(100f, tabHeight), Mathf.RoundToInt(Mathf.Clamp(tabHeight * 0.34f, 16f, 26f)),
                    new Vector2(8f + i * 104f, -52f), TextAnchor.MiddleCenter);
            }

            CreateButton(commandPanel.transform, "CloseCommands", "Закрыть",
                delegate { ClosePanels(); },
                new Vector2(100f, tabHeight), Mathf.RoundToInt(Mathf.Clamp(tabHeight * 0.34f, 16f, 26f)),
                new Vector2(800f, -52f), TextAnchor.MiddleCenter);

            commandPanel.SetActive(false);
        }

        /// <summary>КНОПКА В ПАНЕЛИ (единый стиль интерфейса планшета).</summary>
        private Button CreateButton(Transform parent, string name, string label, Action onClick,
                                    Vector2 size, int fontSize, Vector2 anchoredPosition,
                                    TextAnchor align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.15f, 0.24f, 0.94f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = bg;
            if (onClick != null) button.onClick.AddListener(delegate { onClick(); });

            GameObject labelGo = new GameObject("Text", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 2f);
            labelRect.offsetMax = new Vector2(-6f, -2f);
            Text text = labelGo.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = fontSize;
            text.color = new Color(0.95f, 0.96f, 0.98f);
            text.alignment = align;
            text.text = label;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            buttons.Add(button);
            return button;
        }

        private static Text AddText(Transform parent, string name, string text, int size,
                                    Color color, TextAnchor anchor, Vector2 offsetMin,
                                    Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Text label = go.AddComponent<Text>();
            label.font = BuiltinFont;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        // ================================================================ ДЕЙСТВИЯ ИНТЕРФЕЙСА

        /// <summary>ВЫПОЛНИТЬ КОМАНДУ ПРОЕКТА — тот же реестр, что на компьютере.</summary>
        public void Execute(string commandId)
        {
            if (string.IsNullOrEmpty(commandId)) return;

            if (commandId == "workspace.clear" || commandId == "workspace.clear.confirmed")
            {
                ShowConfirm(
                    "Очистить рабочее пространство?\nБудут удалены все столы, роботы, точки и траектории.\nМир вернётся к камере.",
                    "ОЧИСТИТЬ", RequestClearWorkspace);
                return;
            }

            if (commandId.StartsWith("group:", StringComparison.Ordinal))
            {
                OpenCommands(commandId.Substring(6));
                return;
            }

            // Столы создаются фабрикой объектов (в меню компьютера это тоже отдельное действие).
            if (commandId == "table.square") { CreateTable(false); return; }
            if (commandId == "table.auto") { CreateTable(true); return; }

            KvCommand command = KvCommands.Get(commandId);
            if (command == null)
            {
                Debug.LogWarning("[KvAr] ИНТЕРФЕЙС: команда «" + commandId + "» не зарегистрирована");
                return;
            }
            if (!command.Enabled)
            {
                Debug.Log("[KvAr] ИНТЕРФЕЙС: команда «" + commandId + "» сейчас недоступна");
                return;
            }

            ClosePanels();
            Debug.Log("[KvAr] ИНТЕРФЕЙС: выбрана «" + command.LocalizedTitle + "» (" + commandId + ")");
            KvCommands.Invoke(commandId);
        }

        private void TreeAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return;
            Execute(action);
        }

        /// <summary>СОЗДАТЬ СТОЛ ПЕРЕД КАМЕРОЙ (в AR — на найденной плоскости, если она есть).</summary>
        private static void CreateTable(bool auto)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[KvAr] ИНТЕРФЕЙС: нет камеры — стол не создан");
                return;
            }

            Vector3 spot = KvXrObjectFactory.SpotInFrontOf(camera, KvXrObjectFactory.SpawnDistanceMeters);
            float yaw = KvXrObjectFactory.YawTowards(spot, camera.transform.position);
            float reach = auto ? KvXrObjectFactory.ReachForAutoTable(null) : 1f;
            GameObject table = KvXrObjectFactory.CreateTable(
                auto ? KvXrTableKind.Auto : KvXrTableKind.Square, spot, yaw, reach);
            if (table == null)
            {
                Debug.LogWarning("[KvAr] ИНТЕРФЕЙС: стол не создан");
                return;
            }
            CreatedTableCount++;
            Debug.Log("[KvAr] ИНТЕРФЕЙС: создан стол «" + table.name + "» (ПК-меню) · всего столов " + CreatedTableCount);
            if (KvArWorld.Active != null) KvArWorld.Active.AdoptLooseObjects();
        }

        private string menuOpenTitle = "";

        /// <summary>ОТКРЫТЬ/ЗАКРЫТЬ МЕНЮ (Файл/Правка/…). Пункты настоящие.</summary>
        public void ToggleMenu(string menuTitle)
        {
            EnsureBuilt();
            if (menuPanel == null) return;

            if (menuPanel.activeSelf && menuOpenTitle == menuTitle)
            {
                menuPanel.SetActive(false);
                return;
            }

            string[] ids = MenuCommands(menuTitle);
            if (ids == null)
            {
                Debug.LogWarning("[KvAr] ИНТЕРФЕЙС: неизвестное меню «" + menuTitle + "»");
                return;
            }

            for (int i = menuPanel.transform.childCount - 1; i >= 0; i--)
                Destroy(menuPanel.transform.GetChild(i).gameObject);

            menuOpenTitle = menuTitle;
            menuPanel.SetActive(true);

            Text title = AddText(menuPanel.transform, "MenuTitle", menuTitle,
                Mathf.RoundToInt(Mathf.Clamp(TouchRowHeightUi() * 0.26f, 22f, 30f)),
                new Color(0.75f, 0.80f, 0.86f), TextAnchor.MiddleLeft,
                new Vector2(16f, 0f), new Vector2(0f, 0f));
            RectTransform titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -8f);
            titleRect.sizeDelta = new Vector2(0f, 36f);

            float rowHeight = TouchRowHeightUi() - 8f;
            float rowPitch = TouchRowHeightUi();
            // ВЫПАДАЮЩЕЕ МЕНЮ ОБЯЗАНО ВЛЕЗТЬ НА ЭКРАН: длинный раздел не должен уходить за низ.
            float menuSpace = CanvasHeightUi() - TopChromeHeightUi() - StatusBarHeightUi() - 80f;
            float fitPitch = menuSpace / Mathf.Max(1, ids.Length);
            if (fitPitch < rowPitch)
            {
                rowPitch = Mathf.Max(30f, fitPitch);
                rowHeight = Mathf.Max(26f, rowPitch - 6f);
            }
            for (int r = 0; r < ids.Length; r++)
            {
                string commandId = ids[r];
                KvCommand cmd = KvCommands.Get(commandId);
                string caption = cmd != null ? cmd.LocalizedTitle : commandId + "  (нет)";
                Button row = CreateButton(menuPanel.transform, "MenuItem_" + commandId, caption,
                    delegate { Execute(commandId); },
                    new Vector2(600f, rowHeight),
                    Mathf.RoundToInt(Mathf.Clamp(TouchRowHeightUi() * 0.25f, 20f, 28f)),
                    new Vector2(10f, -52f - r * rowPitch), TextAnchor.MiddleLeft);
                Image rowBg = row.GetComponent<Image>();
                if (rowBg != null)
                {
                    rowBg.color = cmd != null && cmd.Enabled
                        ? new Color(0.10f, 0.15f, 0.24f, 0.96f)
                        : new Color(0.10f, 0.10f, 0.12f, 0.90f);
                }
            }

            RectTransform rect = (RectTransform)menuPanel.transform;
            rect.sizeDelta = new Vector2(620f, 64f + ids.Length * rowPitch);
        }
        /// <summary>ОТКРЫТЬ ПАНЕЛЬ КОМАНД ГРУППЫ (все команды проекта с листанием).</summary>
        public void OpenCommands(string groupId = "robot")
        {
            EnsureBuilt();
            if (commandPanel == null) return;
            if (string.IsNullOrEmpty(groupId)) groupId = "robot";
            if (commandPanel.activeSelf && commandGroupId == groupId)
            {
                ClosePanels();
                return;
            }
            commandGroupId = groupId;
            commandPage = 0;
            commandPanel.SetActive(true);
            FillCommandRows();
            Debug.Log("[KvAr] ИНТЕРФЕЙС: открыта панель команд группы «" + groupId + "»");
        }

        private void FillCommandRows()
        {
            if (commandRowsRoot == null) return;
            for (int i = commandRowsRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(commandRowsRoot.transform.GetChild(i).gameObject);

            KvToolbarGroup group = KvToolbarGroups.Find(commandGroupId);
            string[] commands = group != null ? group.Commands : new string[0];
            commandTitle.text = "Команды · " + (group != null ? group.Title : commandGroupId) +
                                " (" + commands.Length + ")";

            const int pageSize = 8;
            int pages = Mathf.Max(1, Mathf.CeilToInt(commands.Length / (float)pageSize));
            commandPage = Mathf.Clamp(commandPage, 0, pages - 1);
            commandPageText.text = (commandPage + 1) + " / " + pages;

            // СТРОКИ ПОД ПАЛЕЦ, НО БЕЗ ОБРЕЗКИ: шаг сжимается, если страница не влезает целиком.
            float rowsSpace = CanvasHeightUi() - TopChromeHeightUi() - StatusBarHeightUi() -
                              10f - (52f + TabHeightUi() + 10f) - 12f;
            float rowPitch = Mathf.Max(30f, Mathf.Min(TouchRowHeightUi(), rowsSpace / pageSize));
            float rowHeight = Mathf.Max(26f, rowPitch - 6f);
            int start = commandPage * pageSize;
            int end = Mathf.Min(commands.Length, start + pageSize);
            for (int i = start; i < end; i++)
            {
                string id = commands[i];
                KvCommand cmd = KvCommands.Get(id);
                string label = cmd != null ? CleanLabel(cmd.LocalizedTitle) : id + "  (нет)";
                int row = i - start;
                CreateButton(commandRowsRoot.transform, "Cmd_" + id, label,
                    delegate { Execute(id); },
                    new Vector2(860f, rowHeight),
                    Mathf.RoundToInt(Mathf.Clamp(rowPitch * 0.25f, 20f, 28f)),
                    new Vector2(6f, -4f - row * rowPitch), TextAnchor.MiddleLeft);
            }

            if (pages > 1)
            {
                CreateButton(commandRowsRoot.transform, "CmdPrev", "◀ ",
                    delegate { commandPage--; FillCommandRows(); },
                    new Vector2(120f, 44f), 20, new Vector2(6f, -4f - 12 * 48f), TextAnchor.MiddleCenter);
                CreateButton(commandRowsRoot.transform, "CmdNext", " ▶",
                    delegate { commandPage++; FillCommandRows(); },
                    new Vector2(120f, 44f), 20, new Vector2(136f, -4f - 12 * 48f), TextAnchor.MiddleCenter);
            }
        }

        /// <summary>НЕЙТРАЛЬНЫЕ ПОДПИСИ: «Виртуальный пульт» на планшете читается как намёк на VR — убираем слово.</summary>
        private static string CleanLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("Виртуальный ", string.Empty)
                       .Replace("виртуальный ", string.Empty)
                       .Replace("Виртуальная ", "Режим ")
                       .Replace("Виртуальную ", string.Empty);
        }

        private static string ShortGroupTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "?";
            if (title.Length <= 14) return title;
            return title.Substring(0, 13) + "…";
        }

        /// <summary>СПИСОК КОМАНД МЕНЮ (идентификаторы реестра — те же, что на ПК).</summary>
        private static string[] MenuCommands(string menu)
        {
            switch (menu)
            {
                case "Файл":
                    return new[]
                    {
                        "session.save", "session.load", "export.robot", "file.export",
                        "file.import", "log.export", "shot.screenshot", "record.toggle",
                        "video.toggle", "export.lang", "record.speed", "import.scan"
                    };
                case "Правка":
                    return new[]
                    {
                        "workspace.clear", "edit.undo", "edit.redo", "edit.reset",
                        "ui.resetlayout", "ui.toggle"
                    };
                case "Вид":
                    return new[]
                    {
                        "view.workspace", "view.limits", "view.metrics", "view.singularity",
                        "view.heatmap", "view.clearance", "health.toggle", "obstacle.toggle",
                        "tool.grid", "tool.grid.clear", "tool.measure", "tool.measure.reset",
                        "view.panel.tree", "view.panel.properties", "view.panel.settings"
                    };
                case "Робот":
                    return new[]
                    {
                        "robot.switch", "robot.playpause", "robot.stop", "estop",
                        "joints.panel", "pose.save", "pose.goto", "gripper.toggle",
                        "robot.model.stand_6axis", "robot.model.scara_ls10b702s",
                        "robot.model.ur5", "robot.model.kuka_iiwa7", "robot.model.kuka_iiwa14",
                        "robot.model.abb_irb6640", "robot.model.fanuc_m20ib",
                        "robot.model.franka_fr3", "robot.model.franka_fr3_v2",
                        "robot.model.kinova_gen3", "robot.model.comau_edo", "robot.model.openarm"
                    };
                case "Сервис":
                    return new[]
                    {
                        "ui.settings", "view.theme", "lang.cycle", "ui.palette", "help.hotkeys",
                        "features.open", "perf.open", "log.open", "demo.quick", "demo.pickplace",
                        "calib.tab", "payload.tab", "scenarios.open", "scenarios.run",
                        "pendant.toggle", "gamepad.hud", "eta.toggle", "presentation.toggle"
                    };
                case "Справка":
                    return new[]
                    {
                        "help.open", "help.about", "tut.toggle", "tut.restart"
                    };
                default:
                    return null;
            }
        }

        /// <summary>ЗАКРЫТЬ МЕНЮ И ПАНЕЛЬ КОМАНД.</summary>
        public void ClosePanels()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (commandPanel != null) commandPanel.SetActive(false);
        }

        // ================================================================ НИЗ/КНОПКИ

        private void BuildHint(Transform parent)
        {
            GameObject row = new GameObject("ArHint", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            hintsRow = row;
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0.5f, 0f);
            rowRect.anchorMax = new Vector2(0.5f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.anchoredPosition = new Vector2(0f, StatusBarHeightUi() + 8f);
            rowRect.sizeDelta = new Vector2(1600f, Mathf.Round(Mathf.Max(64f, TouchRowHeightUi() * 0.8f)));

            Image bg = row.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.5f);
            bg.raycastTarget = false;

            GameObject textGo = new GameObject("ArHintText", typeof(RectTransform));
            textGo.transform.SetParent(row.transform, false);
            hintText = textGo.AddComponent<Text>();
            hintText.font = BuiltinFont;
            hintText.fontSize = 28;
            hintText.color = Color.white;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);
            hintText.text = "Поднимите планшет и наведите камеру на стол";
        }

        private void BuildButtonRow(Transform parent)
        {
            GameObject row = new GameObject("ArButtons", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(1f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(1f, 1f);
            rowRect.anchoredPosition = new Vector2(-16f, -(TopChromeHeightUi() + 16f));
            rowRect.sizeDelta = new Vector2(buttonWidth + 8f, 4f * (TouchRowHeightUi() + 10f) + 20f);

            VerticalLayoutGroup layout = row.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            buttons.Clear();
        }

        private void BuildMessage(Transform parent)
        {
            GameObject root = new GameObject("ArMessage", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            messageRoot = root;
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.82f);

            GameObject textGo = new GameObject("ArMessageText", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            messageText = textGo.AddComponent<Text>();
            messageText.font = BuiltinFont;
            messageText.fontSize = 40;
            messageText.color = Color.white;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            messageText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, 60f);
            textRect.sizeDelta = new Vector2(1500f, 600f);

            GameObject buttonGo = new GameObject("ArMessageButton", typeof(RectTransform));
            buttonGo.transform.SetParent(root.transform, false);
            RectTransform buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -320f);
            buttonRect.sizeDelta = new Vector2(560f, 110f);
            Image buttonBg = buttonGo.AddComponent<Image>();
            buttonBg.color = new Color(0.15f, 0.45f, 0.9f, 0.95f);
            messageButton = buttonGo.AddComponent<Button>();
            messageButton.targetGraphic = buttonBg;
            messageButton.onClick.AddListener(OnMessageButtonClicked);

            GameObject labelGo = new GameObject("ArMessageButtonText", typeof(RectTransform));
            labelGo.transform.SetParent(buttonGo.transform, false);
            messageButtonText = labelGo.AddComponent<Text>();
            messageButtonText.font = BuiltinFont;
            messageButtonText.fontSize = 34;
            messageButtonText.color = Color.white;
            messageButtonText.alignment = TextAnchor.MiddleCenter;
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // ВТОРАЯ КНОПКА — «ОТМЕНА» ДЛЯ ПОДТВЕРЖДЕНИЯ ОЧИСТКИ (иначе окно не закрыть без действия).
            GameObject cancelGo = new GameObject("ArMessageCancel", typeof(RectTransform));
            cancelGo.transform.SetParent(root.transform, false);
            messageCancelRoot = cancelGo;
            RectTransform cancelRect = (RectTransform)cancelGo.transform;
            cancelRect.anchorMin = new Vector2(0.5f, 0.5f);
            cancelRect.anchorMax = new Vector2(0.5f, 0.5f);
            cancelRect.pivot = new Vector2(0.5f, 0.5f);
            cancelRect.anchoredPosition = new Vector2(0f, -450f);
            cancelRect.sizeDelta = new Vector2(560f, 100f);
            Image cancelBg = cancelGo.AddComponent<Image>();
            cancelBg.color = new Color(0.18f, 0.20f, 0.24f, 0.95f);
            Button cancelButton = cancelGo.AddComponent<Button>();
            cancelButton.targetGraphic = cancelBg;
            cancelButton.onClick.AddListener(OnMessageCancelClicked);
            GameObject cancelLabelGo = new GameObject("Text", typeof(RectTransform));
            cancelLabelGo.transform.SetParent(cancelGo.transform, false);
            Text cancelLabel = cancelLabelGo.AddComponent<Text>();
            cancelLabel.font = BuiltinFont;
            cancelLabel.fontSize = 30;
            cancelLabel.color = Color.white;
            cancelLabel.alignment = TextAnchor.MiddleCenter;
            cancelLabel.text = "ОТМЕНА";
            RectTransform cancelLabelRect = (RectTransform)cancelLabelGo.transform;
            cancelLabelRect.anchorMin = Vector2.zero;
            cancelLabelRect.anchorMax = Vector2.one;
            cancelLabelRect.offsetMin = Vector2.zero;
            cancelLabelRect.offsetMax = Vector2.zero;
            cancelGo.SetActive(false);
        }

        /// <summary>ДОБАВИТЬ КНОПКУ В ПРАВЫЙ РЯД (быстрые действия планшета).</summary>
        public Button AddButton(string label, Action onClick)
        {
            EnsureBuilt();
            Transform row = canvas.transform.Find("ArButtons");
            if (row == null) return null;

            GameObject go = new GameObject("ArButton_" + label, typeof(RectTransform));
            go.transform.SetParent(row, false);
            RectTransform rect = (RectTransform)go.transform;
            // ВЫСОТА ПОД ПАЛЕЦ: поле `buttonHeight` в сцене может быть меньше — поднимаем до
            // минимальной (≈10 мм), иначе по кнопке трудно попасть (замер по фото 06.11.2026).
            rect.sizeDelta = new Vector2(Mathf.Max(180f, buttonWidth),
                Mathf.Max(TouchRowHeightUi(), buttonHeight));

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.16f, 0.28f, 0.9f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = bg;
            if (onClick != null) button.onClick.AddListener(delegate { onClick(); });

            GameObject labelGo = new GameObject("Text", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            Text text = labelGo.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = Mathf.RoundToInt(Mathf.Clamp(TouchRowHeightUi() * 0.28f, 22f, 34f));
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);

            buttons.Add(button);
            return button;
        }

        /// <summary>ОБНОВИТЬ СТРОКУ СОСТОЯНИЯ (нижняя ПК-строка).</summary>
        public void SetStatus(string text)
        {
            SetFooter(text, footerRight != null ? footerRight.text : string.Empty);
        }

        /// <summary>НИЖНЯЯ СТРОКА СОСТОЯНИЯ: слева состояние, справа камера/кадры.</summary>
        public void SetFooter(string left, string right)
        {
            if (footerLeft != null) footerLeft.text = string.IsNullOrEmpty(left) ? "Готово" : left;
            if (footerRight != null) footerRight.text = string.IsNullOrEmpty(right) ? string.Empty : right;
        }

        /// <summary>ОБНОВИТЬ ПОДСКАЗКУ (короткая строка внизу экрана).</summary>
        public void SetHint(string text)
        {
            if (hintText == null) return;
            hintText.text = text ?? string.Empty;
            if (hintsRow != null) hintsRow.SetActive(!string.IsNullOrEmpty(text));
        }

        public void HideHint() { SetHint(string.Empty); }

        public void ShowMessage(string text, string buttonLabel = null, Action action = null)
        {
            EnsureBuilt();
            if (messageRoot == null) return;
            messageRoot.SetActive(true);
            if (messageText != null) messageText.text = text ?? string.Empty;
            messageAction = action;
            messageCancelAction = null;
            if (messageCancelRoot != null) messageCancelRoot.SetActive(false);
            bool hasButton = !string.IsNullOrEmpty(buttonLabel) && action != null;
            if (messageButton != null) messageButton.gameObject.SetActive(hasButton);
            if (hasButton && messageButtonText != null) messageButtonText.text = buttonLabel;
            SetHint(string.Empty);
        }

        /// <summary>
        /// ПОДТВЕРЖДЕНИЕ ОПАСНОГО ДЕЙСТВИЯ (очистка рабочего пространства): две кнопки —
        /// «ОЧИСТИТЬ» и «ОТМЕНА». Одна кнопка без отмены — риск случайно стереть сцену.
        /// </summary>
        public void ShowConfirm(string text, string confirmLabel, Action onConfirm)
        {
            EnsureBuilt();
            if (messageRoot == null) return;
            messageRoot.SetActive(true);
            if (messageText != null) messageText.text = text ?? string.Empty;
            messageAction = onConfirm;
            messageCancelAction = delegate { HideMessage(); };
            if (messageButton != null) messageButton.gameObject.SetActive(true);
            if (messageButtonText != null) messageButtonText.text = confirmLabel;
            if (messageCancelRoot != null) messageCancelRoot.SetActive(true);
            SetHint(string.Empty);
            Debug.Log("[KvAr] ИНТЕРФЕЙС: подтверждение — " + (text ?? string.Empty).Replace("\n", " "));
        }

        /// <summary>ВЫПОЛНИТЬ ОЧИСТКУ РАБОЧЕГО ПРОСТРАНСТВА (после подтверждения).</summary>
        public void RequestClearWorkspace()
        {
            KvArRuntime runtime = UnityEngine.Object.FindAnyObjectByType<KvArRuntime>();
            if (runtime == null)
            {
                Debug.LogWarning("[KvAr] ОЧИСТКА: нет KvArRuntime — очистить некому");
                HideMessage();
                return;
            }
            runtime.ClearWorkspace();
        }

        /// <summary>СБРОС СЧЁТЧИКА СТОЛОВ (вызывается очисткой рабочего пространства).</summary>
        public static void ResetCreatedTableCounter()
        {
            CreatedTableCount = 0;
        }

        public void HideMessage()
        {
            if (messageRoot == null) return;
            messageRoot.SetActive(false);
            messageAction = null;
            messageCancelAction = null;
            if (messageCancelRoot != null) messageCancelRoot.SetActive(false);
        }

        private void OnMessageButtonClicked()
        {
            Action action = messageAction;
            if (action != null && messageRoot != null) messageRoot.SetActive(false);
            messageAction = null;
            messageCancelAction = null;
            if (action != null) action();
        }

        private void OnMessageCancelClicked()
        {
            HideMessage();
            Debug.Log("[KvAr] ИНТЕРФЕЙС: действие отменено");
        }

        public void SetButtonsVisible(bool visible)
        {
            if (canvas == null) return;
            Transform row = canvas.transform.Find("ArButtons");
            if (row != null) row.gameObject.SetActive(visible);
        }

        /// <summary>EVENT SYSTEM ДЛЯ ПАЛЬЦА (только если его нет в сцене).</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            EventSystem[] all = UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (all != null && all.Length > 0) return;

            GameObject go = new GameObject("ArEventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Debug.Log("[KvAr] ИНТЕРФЕЙС: создан EventSystem с модулем Input System");
        }

        /// <summary>
        /// СТРОКА СОСТОЯНИЯ ИЗ ФАКТОВ (чистая функция — историческая; на экране больше не
        /// показывается, оставлена для тестов).
        /// </summary>
        public static string BuildStatusLine(bool arSessionReady, bool cameraGranted, int planesFound,
                                             bool worldPlaced, bool robotPlaced, float fps,
                                             float anchorHeightMeters)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("AR · ");
            sb.Append(cameraGranted ? "камера есть" : "камера НЕТ");
            sb.Append(" · сессия: ").Append(arSessionReady ? "готова" : "не готова");
            sb.Append(" · плоскостей: ").Append(planesFound);
            sb.Append(" · ");
            sb.Append(worldPlaced ? (robotPlaced ? "робот поставлен" : "якорь есть") : "наведите на стол");
            if (worldPlaced && anchorHeightMeters > -1000f)
                sb.Append(" · высота якоря ")
                  .Append(anchorHeightMeters.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(" м");
            if (fps > 0f)
                sb.Append(" · ").Append(fps.ToString("0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(" кадр/с");
            return sb.ToString();
        }
    }
}
