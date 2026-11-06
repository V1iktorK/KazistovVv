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
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildPcChrome(canvasGo.transform);
            BuildHint(canvasGo.transform);
            BuildButtonRow(canvasGo.transform);
            BuildMessage(canvasGo.transform);
            HideMessage();

            if (logOnBuild)
                Debug.Log("[KvAr] ИНТЕРФЕЙС: ПК-вид построен · меню 6 · дерево · инструменты · " +
                          "опорное разрешение " + referenceResolution.x.ToString("0") + "×" +
                          referenceResolution.y.ToString("0"));
        }

        // ================================================================ ПК-КАРКАС

        private void BuildPcChrome(Transform parent)
        {
            Color bar = new Color(0.035f, 0.045f, 0.055f, 0.96f);
            Color line = new Color(0.78f, 0.82f, 0.86f, 0.95f);

            // ВЕРХНЕЕ МЕНЮ С РАБОЧИМИ ПУНКТАМИ
            GameObject menu = new GameObject("PcMenuBar", typeof(RectTransform), typeof(Image));
            menu.transform.SetParent(parent, false);
            RectTransform menuRect = (RectTransform)menu.transform;
            menuRect.anchorMin = new Vector2(0f, 1f);
            menuRect.anchorMax = new Vector2(1f, 1f);
            menuRect.pivot = new Vector2(0.5f, 1f);
            menuRect.anchoredPosition = Vector2.zero;
            menuRect.sizeDelta = new Vector2(0f, 50f);
            menu.GetComponent<Image>().color = bar;
            menu.GetComponent<Image>().raycastTarget = false;
            menuBar = menu.transform;

            string[] menus = { "Файл", "Правка", "Вид", "Робот", "Сервис", "Справка" };
            for (int i = 0; i < menus.Length; i++)
            {
                int index = i;
                CreateButton(menu.transform, "Menu_" + menus[i], menus[i],
                    delegate { ToggleMenu(menus[index]); },
                    new Vector2(132f, 46f), 24,
                    new Vector2(8f + i * 138f, 2f), TextAnchor.MiddleCenter);
            }

            // СТРОКА ИНСТРУМЕНТОВ (как панель инструментов ПК)
            GameObject tools = new GameObject("PcToolbar", typeof(RectTransform), typeof(Image));
            tools.transform.SetParent(parent, false);
            RectTransform toolsRect = (RectTransform)tools.transform;
            toolsRect.anchorMin = new Vector2(0f, 1f);
            toolsRect.anchorMax = new Vector2(1f, 1f);
            toolsRect.pivot = new Vector2(0.5f, 1f);
            toolsRect.anchoredPosition = new Vector2(0f, -50f);
            toolsRect.sizeDelta = new Vector2(0f, 52f);
            tools.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.04f, 0.92f);
            tools.GetComponent<Image>().raycastTarget = false;
            toolbar = tools.transform;

            string[][] quick =
            {
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
                    new Vector2(142f, 44f), 21,
                    new Vector2(8f + i * 150f, 4f), TextAnchor.MiddleCenter);
            }

            // ДЕРЕВО ПРОЕКТА СЛЕВА — КНОПКИ, КОТОРЫЕ ДЕЙСТВУЮТ
            GameObject tree = new GameObject("PcTreePanel", typeof(RectTransform), typeof(Image));
            tree.transform.SetParent(parent, false);
            RectTransform treeRect = (RectTransform)tree.transform;
            treeRect.anchorMin = new Vector2(0f, 0f);
            treeRect.anchorMax = new Vector2(0f, 1f);
            treeRect.pivot = new Vector2(0f, 0f);
            treeRect.anchoredPosition = new Vector2(0f, 40f);
            treeRect.sizeDelta = new Vector2(268f, -142f);
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
            for (int i = 0; i < treeRows.Length; i++)
            {
                string label = treeRows[i][0];
                string action = treeRows[i][1];
                CreateButton(treePanel, "Tree_" + label, "▸ " + label,
                    delegate { TreeAction(action); },
                    new Vector2(252f, 44f), 21,
                    new Vector2(8f, -8f - i * 48f), TextAnchor.MiddleLeft);
            }

            // НИЖНЯЯ СТРОКА СОСТОЯНИЯ (как в ПК; без верхней подсказки)
            GameObject bottom = new GameObject("PcStatusBar", typeof(RectTransform), typeof(Image));
            bottom.transform.SetParent(parent, false);
            RectTransform bottomRect = (RectTransform)bottom.transform;
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomRect.sizeDelta = new Vector2(0f, 40f);
            Image bottomBg = bottom.GetComponent<Image>();
            bottomBg.color = bar;
            bottomBg.raycastTarget = false;

            footerLeft = AddText(bottom.transform, "FooterLeft", "Готово", 20, line,
                TextAnchor.MiddleLeft, new Vector2(16f, 0f), new Vector2(0f, 0f));
            footerRight = AddText(bottom.transform, "FooterRight", "Камера: вкл", 20, line,
                TextAnchor.MiddleRight, new Vector2(-16f, 0f), new Vector2(-400f, 0f));

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
            rect.anchoredPosition = new Vector2(8f, -102f);
            rect.sizeDelta = new Vector2(560f, 500f);
            Image bg = menuPanel.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.06f, 0.98f);
            menuPanel.SetActive(false);
        }

        private void BuildCommandPanel(Transform parent)
        {
            commandPanel = new GameObject("PcCommandPanel", typeof(RectTransform), typeof(Image));
            commandPanel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)commandPanel.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(272f, 40f);
            rect.sizeDelta = new Vector2(900f, -142f);
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
            rowsRect.offsetMax = new Vector2(-10f, -96f);

            // ГРУППЫ КОМАНД (8) — вкладки с КОРОТКИМИ именами (как вкладки ПК-панели)
            KvToolbarGroup[] groups = KvToolbarGroups.All;
            string[] shortNames = { "Робот", "Траектория", "Постобр.", "Визуал.", "Экспорт", "Сеть", "Настройки", "Прочее" };
            for (int i = 0; i < groups.Length && i < 8; i++)
            {
                string id = groups[i].Id;
                string shortTitle = i < shortNames.Length ? shortNames[i] : ShortGroupTitle(groups[i].Title);
                CreateButton(commandPanel.transform, "GroupTab_" + id, shortTitle,
                    delegate { OpenCommands(id); },
                    new Vector2(92f, 38f), 14,
                    new Vector2(8f + i * 96f, -52f), TextAnchor.MiddleCenter);
            }

            CreateButton(commandPanel.transform, "CloseCommands", "Закрыть",
                delegate { ClosePanels(); },
                new Vector2(88f, 38f), 15, new Vector2(800f, -52f), TextAnchor.MiddleCenter);

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

            Text title = AddText(menuPanel.transform, "MenuTitle", menuTitle, 24,
                new Color(0.75f, 0.80f, 0.86f), TextAnchor.MiddleLeft,
                new Vector2(16f, 0f), new Vector2(0f, 0f));
            RectTransform titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -8f);
            titleRect.sizeDelta = new Vector2(0f, 36f);

            for (int r = 0; r < ids.Length; r++)
            {
                string commandId = ids[r];
                KvCommand cmd = KvCommands.Get(commandId);
                string caption = cmd != null ? cmd.LocalizedTitle : commandId + "  (нет)";
                Button row = CreateButton(menuPanel.transform, "MenuItem_" + commandId, caption,
                    delegate { Execute(commandId); },
                    new Vector2(540f, 44f), 20,
                    new Vector2(10f, -52f - r * 46f), TextAnchor.MiddleLeft);
                Image rowBg = row.GetComponent<Image>();
                if (rowBg != null)
                {
                    rowBg.color = cmd != null && cmd.Enabled
                        ? new Color(0.10f, 0.15f, 0.24f, 0.96f)
                        : new Color(0.10f, 0.10f, 0.12f, 0.90f);
                }
            }

            RectTransform rect = (RectTransform)menuPanel.transform;
            rect.sizeDelta = new Vector2(560f, 64f + ids.Length * 46f);
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

            const int pageSize = 12;
            int pages = Mathf.Max(1, Mathf.CeilToInt(commands.Length / (float)pageSize));
            commandPage = Mathf.Clamp(commandPage, 0, pages - 1);
            commandPageText.text = (commandPage + 1) + " / " + pages;

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
                    new Vector2(860f, 44f), 20,
                    new Vector2(6f, -4f - row * 48f), TextAnchor.MiddleLeft);
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
                        "edit.undo", "edit.redo", "edit.reset", "ui.resetlayout", "ui.toggle"
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
            rowRect.anchoredPosition = new Vector2(0f, 48f);
            rowRect.sizeDelta = new Vector2(1600f, 64f);

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
            rowRect.anchoredPosition = new Vector2(-16f, -112f);
            rowRect.sizeDelta = new Vector2(buttonWidth + 8f, 420f);

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
            rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.16f, 0.28f, 0.9f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = bg;
            if (onClick != null) button.onClick.AddListener(delegate { onClick(); });

            GameObject labelGo = new GameObject("Text", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            Text text = labelGo.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = 26;
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
            bool hasButton = !string.IsNullOrEmpty(buttonLabel) && action != null;
            if (messageButton != null) messageButton.gameObject.SetActive(hasButton);
            if (hasButton && messageButtonText != null) messageButtonText.text = buttonLabel;
            SetHint(string.Empty);
        }

        public void HideMessage()
        {
            if (messageRoot == null) return;
            messageRoot.SetActive(false);
            messageAction = null;
        }

        private void OnMessageButtonClicked()
        {
            Action action = messageAction;
            if (action != null && messageRoot != null) messageRoot.SetActive(false);
            messageAction = null;
            if (action != null) action();
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
