using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>Внешний пункт настроек (читается из StreamingAssets без перекомпиляции).</summary>
    [Serializable]
    public class KvSettingEntry
    {
        public string id = "";
        public string tab = "Функции";
        public string title = "";
        public string type = "toggle";     // toggle | choice | info | group
        public string value = "";
        public string note = "";
        public string[] options = new string[0];
    }

    /// <summary>Внешняя строка таблицы биндов.</summary>
    [Serializable]
    public class KvBindingEntry
    {
        public string group = "";
        public string action = "";
        public string keys = "";
        public string note = "";
    }

    /// <summary>Файл расширения настроек (StreamingAssets/kazistovvv_settings.json).</summary>
    [Serializable]
    public class KvSettingsFile
    {
        public KvSettingEntry[] items = new KvSettingEntry[0];
        public KvBindingEntry[] bindings = new KvBindingEntry[0];
    }

    /// <summary>
    /// Схема панели настроек: встроенный набор пунктов + внешние пункты из
    /// `StreamingAssets/kazistovvv_settings.json` (ТЗ: «новые пункты добавляются
    /// без перекомпиляции»). Внешние пункты с неизвестным `id` показываются, но
    /// помечаются как «нет обработчика» — их можно подключить кодом позже.
    /// </summary>
    public static class KvSettingsSchema
    {
        /// <summary>Имя файла расширения в StreamingAssets.</summary>
        public const string ExternalFileName = "kazistovvv_settings.json";

        private static KvSettingsFile external;
        private static bool externalLoaded;
        private static string externalPath = "";

        /// <summary>Загруженный внешний файл (может быть пустым).</summary>
        public static KvSettingsFile External
        {
            get
            {
                if (!externalLoaded)
                {
                    externalLoaded = true;
                    external = LoadExternal();
                }
                return external;
            }
        }

        /// <summary>Путь внешнего файла и результат загрузки (диагностика).</summary>
        public static string ExternalStatus
        {
            get
            {
                KvSettingsFile f = External;
                if (f == null || f.items == null) return "внешний файл не найден";
                return "внешний файл: " + externalPath + " · пунктов " + f.items.Length;
            }
        }

        private static KvSettingsFile LoadExternal()
        {
            try
            {
                externalPath = Path.Combine(Application.streamingAssetsPath, ExternalFileName);
                if (!File.Exists(externalPath)) return new KvSettingsFile();
                string json = File.ReadAllText(externalPath);
                KvSettingsFile file = JsonUtility.FromJson<KvSettingsFile>(json);
                if (file == null) return new KvSettingsFile();
                if (file.items == null) file.items = new KvSettingEntry[0];
                if (file.bindings == null) file.bindings = new KvBindingEntry[0];
                return file;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KazistovVv] Внешние настройки не прочитаны: " + e.Message);
                return new KvSettingsFile();
            }
        }

        /// <summary>Встроенные пункты вкладки «Функции» (id → подпись).</summary>
        public static List<KvSettingEntry> Features()
        {
            List<KvSettingEntry> list = new List<KvSettingEntry>();
            list.Add(Entry("vis.workspace", "Функции", "Зона достижимости (купол/кольцо)"));
            list.Add(Entry("vis.limits", "Функции", "Индикаторы лимитов суставов"));
            list.Add(Entry("vis.metrics", "Функции", "Панель метрик траекторий"));
            list.Add(Entry("tool.flashlight", "Функции", "Фонарик (G)"));
            list.Add(Entry("tree.autorefresh", "Функции", "Автообновление дерева моделей"));
            list.Add(Entry("scene.phantoms", "Функции", "Фантомы траекторий"));
            // --- ЭТАП 9 текущей сессии (обработчик — в менеджере UI)
            list.Add(Entry("ui.gamepadhud", "Функции",
                "Виртуальный геймпад (индикатор нажатий кнопок и стиков)"));

            // --- ЭТАПЫ 1–8 сессии 15.09.2026 (обработчики — в `KvStageHub`)
            list.Add(Entry("scene.singularities", "Функции",
                KvLoc.T("singularity.title", "Сингулярности") +
                " — подсветка зон потери манёвренности у суставов"));
            list.Add(Entry("scene.obstacles", "Функции",
                KvLoc.T("obstacle.title", "Динамические препятствия") +
                " — движущаяся тележка, учитываемая планировщиком"));
            list.Add(Entry("tool.health", "Функции",
                KvLoc.T("health.title", "Мониторинг состояния") +
                " — температура, ток, скорость, износ суставов"));
            list.Add(Entry("tool.pendant", "Функции",
                KvLoc.T("pendant.title", "Виртуальный пульт") +
                " — teach pendant с экраном TCP, джойстиком и кнопками"));
            list.Add(Entry("tool.waypoints", "Функции",
                KvLoc.T("waypoint.title", "Промежуточные точки") +
                " — ход маршрута через waypoints (только просмотр, добавление — кнопкой тулбара)"));
            list.Add(Entry("capture.annotate", "Функции",
                KvLoc.T("capture.annotate", "Подпись на скриншоте (дата, состояние, робот)")));

            // --- ЭТАПЫ 1–6 сессии 16.09.2026 (обработчики — в `KvStageHub2`)
            list.Add(Entry("ui.startmenu", "Функции",
                "Главное меню при запуске (F1) — экран выбора проекта со стартовым облётом робота"));
            list.Add(Entry("post.auto", "Функции",
                "Автосглаживание траекторий после планирования (этап 4)"));
            list.Add(Entry("tool.workbench", "Функции",
                "Верстак постобработки (F2): сглаживание, время-оптимальная траектория, энергия"));
            list.Add(Entry("tool.tutorial", "Функции",
                "Обучение с подсказками (F3) — шаги по выбору точки, траектории и фантома"));

            // --- ЭТАПЫ 7–12 сессии 17.09.2026 (обработчики — в `KvStageHub3`)
            list.Add(Entry("wp.constraints", "Функции",
                "Ограничения промежуточных точек (этап 7): ориентация TCP, скорость, обход, пауза"));
            list.Add(Entry("plan.constrained", "Функции",
                "Планирование с ограничениями (этап 8): вертикальный инструмент, предел наклона, " +
                "взгляд на объект"));
            list.Add(Entry("plan.filter", "Функции",
                "Отбрасывать варианты траекторий, нарушающие ограничение (этап 8)"));
            list.Add(Entry("import.scan", "Функции",
                "Импорт моделей роботов (этап 12): сканировать папки моделей при запуске"));

            // --- ЭТАПЫ 13–36 сессии 17.09.2026 (обработчики — в `KvStageHub4`)
            list.Add(Entry("collision.proxies", "Функции",
                "Коллизионные прокси (этап 13): упрощённые оболочки столкновений ускоряют планирование"));
            list.Add(Entry("lab.tree", "Функции",
                "Живое дерево RRT на экране (этап 15): видно, как планировщик ищет путь"));
            list.Add(Entry("web.server", "Функции",
                "Веб-дашборд (этап 21): страница состояния робота в браузере на 127.0.0.1"));
            list.Add(Entry("voice.input", "Функции",
                "Голосовые команды (этап 23): детектор речи и грамматика команд «стоп», «домой», «пуск»"));
            list.Add(Entry("hands.input", "Функции",
                "Отслеживание рук (этап 24): жесты «касание», «свайп», «горсть»"));
            list.Add(Entry("eyes.input", "Функции",
                "Отслеживание взгляда (этап 25): фиксации, выбор взглядом, зона центрального зрения"));
            list.Add(Entry("cinema.mode", "Функции",
                "Кинематографический режим (этап 31): широкий экран и плавное движение камеры"));
            list.Add(Entry("env.show", "Функции",
                "Окружение (этап 28): помещение вокруг робота — ангар, лаборатория, цех, чистое"));
            list.Add(Entry("safety.confirm", "Функции",
                "Проверка перед пуском (этап 35): замечания по траектории и подтверждение оператора"));

            if (External.items != null)
            {
                foreach (KvSettingEntry e in External.items)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    if (string.IsNullOrEmpty(e.tab)) e.tab = "Функции";
                    list.Add(e);
                }
            }
            return list;
        }

        private static KvSettingEntry Entry(string id, string tab, string title)
        {
            KvSettingEntry e = new KvSettingEntry();
            e.id = id;
            e.tab = tab;
            e.title = title;
            e.type = "toggle";
            return e;
        }

        /// <summary>
        /// Таблица биндов — ТОЛЬКО ДЛЯ ОТОБРАЖЕНИЯ (ТЗ: «сами бинды не трогать»).
        /// ЭТАП 7: единственный источник — реестр <see cref="KvBindings"/> (клавиатура,
        /// мышь, геймпад), поэтому вкладка «Управление» и окно «Горячие клавиши» (F12)
        /// показывают ОДНО И ТО ЖЕ и не расходятся между собой.
        /// </summary>
        public static List<KvBindingEntry> Bindings()
        {
            List<KvBindingEntry> list = new List<KvBindingEntry>();

            foreach (KvBindEntry b in KvBindings.All())
            {
                if (b == null) continue;
                KvBindingEntry e = new KvBindingEntry();
                e.group = b.Device + " · " + b.Group;
                e.action = b.Action;
                e.keys = b.Keys;
                e.note = b.Note;
                list.Add(e);
            }

            if (External.bindings != null)
            {
                foreach (KvBindingEntry b in External.bindings)
                {
                    if (b == null || string.IsNullOrEmpty(b.action)) continue;
                    if (string.IsNullOrEmpty(b.group)) b.group = "Из внешнего файла";
                    list.Add(b);
                }
            }
            return list;
        }
    }

    /// <summary>Колбэки панели настроек (реализует менеджер UI).</summary>
    public class KvSettingsCallbacks
    {
        /// <summary>Включить/выключить функцию по id («vis.workspace», …).</summary>
        public Action<string, bool> SetFeature;
        /// <summary>Текущее значение функции (для отрисовки галки).</summary>
        public Func<string, bool> GetFeature;
        /// <summary>Есть ли обработчик у функции (иначе — «нет обработчика»).</summary>
        public Func<string, bool> HasFeature;
        /// <summary>Сменить тему.</summary>
        public Action<KvThemeMode> SetTheme;
        /// <summary>Сменить масштаб интерфейса.</summary>
        public Action<float> SetScale;
        /// <summary>Сменить плотность (0 — компактная).</summary>
        public Action<int> SetDensity;
        /// <summary>Сменить число рядов тулбара.</summary>
        public Action<int> SetToolbarRows;
        /// <summary>ФИКС 8: сменить раскладку тулбара (0 — авто, 1 — широко, 2 — «5 в ряд»).</summary>
        public Action<int> SetToolbarLayout;
        /// <summary>ФИКС 9: включить/выключить компактный тулбар.</summary>
        public Action<bool> SetToolbarCompact;
        /// <summary>Показать/скрыть dock-панель по id («tree», «properties», «settings»).</summary>
        public Action<string, bool> SetPanelVisible;
        /// <summary>Видима ли панель.</summary>
        public Func<string, bool> GetPanelVisible;
        /// <summary>Сброс настроек интерфейса.</summary>
        public Action ResetAll;

        // --- ПЕРЕКЛЮЧАТЕЛЬ VR / MR (11.11.2026)
        //
        // ЗАЧЕМ ОТДЕЛЬНЫЕ КОЛБЭКИ, А НЕ ЕЩЁ ОДИН ПУНКТ ВКЛАДКИ «ФУНКЦИИ». Вкладка «Функции»
        // состоит из галок «включено/выключено» и не умеет показывать ВЫБОР ИЗ ПОЛОЖЕНИЙ:
        // у режима два РАВНОПРАВНЫХ названных состояния, а не «включено» и «не включено».
        // Плюс режим — не украшение сцены, а корневое решение, и жить он должен рядом с темой
        // и языком, а не в списке из тридцати галок.
        //
        // ХРАНИЛИЩЕ — ТО ЖЕ, ЧТО У ШЛЕМА (`KazistovVvXr.KvXrSettings`): одна настройка, два
        // входа. Готовое значение читается и пишется индексом 0 (VR) / 1 (MR).
        /// <summary>Текущее положение переключателя режима: 0 — VR, 1 — MR.</summary>
        public Func<int> GetMode;
        /// <summary>Сменить положение переключателя режима.</summary>
        public Action<int> SetMode;

        // --- ЭТАП 11: ДОСТУПНОСТЬ
        /// <summary>Сменить размер шрифта (0 маленький … 2 большой).</summary>
        public Action<int> SetFontSize;
        /// <summary>Высокий контраст.</summary>
        public Action<bool> SetHighContrast;
        /// <summary>Схема для дальтоников (0 — выключена).</summary>
        public Action<int> SetColorBlind;
        /// <summary>Навигация по интерфейсу с клавиатуры (Tab/стрелки/Enter).</summary>
        public Action<bool> SetKeyboardNav;
        /// <summary>Виртуальный геймпад на экране (ЭТАП 9).</summary>
        public Action<bool> SetGamepadHud;

        // --- ЭТАПЫ 3/5/7: сервисные действия из вкладки «Интерфейс»
        /// <summary>Открыть палитру команд (Ctrl+P).</summary>
        public Action OpenPalette;
        /// <summary>Открыть окно горячих клавиш (F12).</summary>
        public Action OpenHotkeys;
        /// <summary>Сбросить раскладку окон и групп тулбара (ЭТАП 3).</summary>
        public Action ResetLayout;
    }

    /// <summary>
    /// Содержимое панели «Настройки» (dock-панель): вкладки «Функции»,
    /// «Управление», «Интерфейс», «Графика», «О программе», «Справка». Состав вкладок — из
    /// <see cref="KvSettingsSchema"/>, поэтому список расширяется данными
    /// (StreamingAssets), а не правкой этого файла.
    ///
    /// ВКЛАДКА «ГРАФИКА» (сессия 18.09.2026) добавлена ЧЕТВЁРТОЙ — рядом с «Интерфейс»,
    /// как требует ТЗ. Её содержимое строит отдельный модуль
    /// `KazistovVvFeatures.KvGraphicsUi` — панель остаётся такой же, как была.
    /// </summary>
    public class KvSettingsView : MonoBehaviour
    {
        private static readonly string[] Tabs =
            { "Функции", "Управление", "Интерфейс", "Графика", "О программе", "Справка" };

        /// <summary>Сколько вкладок в панели настроек.</summary>
        public const int TabCount = 6;

        /// <summary>Номер вкладки «Графика» (ТЗ: рядом с «Интерфейс»).</summary>
        public const int GraphicsTabIndex = 3;

        /// <summary>Номер вкладки «Справка».</summary>
        public const int HelpTabIndex = 5;

        /// <summary>
        /// Номер вкладки по её русскому имени (внутреннему ключу). Нужен там, где номер
        /// вкладки задавать числом опасно (диагностика, команды меню) — при добавлении
        /// новых вкладок такие вызовы не ломаются.
        /// </summary>
        public static int IndexOfTab(string ruKey)
        {
            if (string.IsNullOrEmpty(ruKey)) return -1;
            for (int i = 0; i < Tabs.Length; i++)
                if (string.Equals(Tabs[i], ruKey, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private RectTransform root;
        private RectTransform content;
        private ScrollRect scroll;
        private Image bg;
        private KvSettingsCallbacks callbacks;
        private KvSegmented tabStrip;
        private readonly List<GameObject> rows = new List<GameObject>();
        private readonly List<KvSwitch> switches = new List<KvSwitch>();
        private readonly List<string> switchIds = new List<string>();

        /// <summary>Активная вкладка (0…3).</summary>
        public int ActiveTab { get; private set; }
        /// <summary>Сколько строк построено на вкладке (диагностика).</summary>
        public int RowCount { get { return rows.Count; } }

        public void Build(RectTransform parent, KvSettingsCallbacks cbs)
        {
            callbacks = cbs;
            root = parent;
            bg = parent.GetComponent<Image>();
            if (bg == null)
            {
                bg = parent.gameObject.AddComponent<Image>();
                bg.sprite = KvTheme.WhiteSprite;
                bg.color = KvTheme.PanelDark;
            }

            // ФИКС 6 (§23): полоса вкладок приведена к эталону KvWidgets.TabHeight (26 px —
            // внутри требуемого диапазона 24…28), шрифт — мелкий (FontSizeSmall, задаётся
            // внутри KvTheme.CreateButton при small = true). До этого высота была 19…21 px,
            // но кнопки вкладок всё равно могли «разъехаться»: размер теперь задаётся явно
            // и в LayoutElement, и в sizeDelta (см. KvWidgets.Segmented).
            RectTransform strip = KvWidgets.CreateRow(root, "Tabs", KvWidgets.TabHeight, 2f);
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.sizeDelta = new Vector2(-6f, KvWidgets.TabHeight - 1f);
            strip.anchoredPosition = new Vector2(0f, -2f);
            tabStrip = KvWidgets.Segmented(strip, "Tabs", TabLabels(), 0, SetTab, KvWidgets.TabHeight - 2f);

            GameObject scrollGo = new GameObject("Scroll", typeof(ScrollRect), typeof(RectMask2D),
                typeof(Image));
            scrollGo.transform.SetParent(root, false);
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.sprite = KvTheme.WhiteSprite;
            scrollBg.color = KvTheme.PanelDark;
            RectTransform viewport = (RectTransform)scrollGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(2f, 2f);
            viewport.offsetMax = new Vector2(-2f, -(KvWidgets.TabHeight + 2f));

            scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 26f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1f;
            vlg.padding = new RectOffset(4, 4, 3, 3);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.content = content;

            SetTab(0);
        }

        /// <summary>Показать вкладку (0…3).</summary>
        public void SetTab(int index)
        {
            ActiveTab = Mathf.Clamp(index, 0, Tabs.Length - 1);
            if (tabStrip != null) tabStrip.Set(ActiveTab);
            Rebuild();
        }

        private void Rebuild()
        {
            Clear();
            switch (ActiveTab)
            {
                case 0: BuildFeatures(); break;
                case 1: BuildBindings(); break;
                case 2: BuildInterface(); break;
                case 3: BuildGraphics(); break;
                case 4: BuildAbout(); break;
                default: BuildHelp(); break;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void Clear()
        {
            foreach (GameObject go in rows)
                if (go != null) Destroy(go);
            rows.Clear();
            switches.Clear();
            switchIds.Clear();
        }

        private Text Header(string title)
        {
            Text t = KvWidgets.Label(content, "H_" + title, title.ToUpperInvariant(),
                KvTheme.FontSizeSmall - 1, KvTheme.TextDim, TextAnchor.MiddleLeft);
            KvWidgets.Fit(t.gameObject, 0f, 16f);
            rows.Add(t.gameObject);
            return t;
        }

        private void Divider()
        {
            Image img = KvTheme.CreatePanel(content, "Div", KvTheme.Separator);
            KvWidgets.Fit(img.gameObject, 0f, 1f);
            rows.Add(img.gameObject);
        }

        // ------------------------------------------------------------------ вкладка 1

        private void BuildFeatures()
        {
            Header("Отключение функций");
            List<KvSettingEntry> entries = KvSettingsSchema.Features();
            foreach (KvSettingEntry e in entries)
            {
                bool mapped = callbacks != null && callbacks.HasFeature != null && callbacks.HasFeature(e.id);
                bool canToggle = mapped && e.type != "info" && e.type != "group";
                bool initial = canToggle && callbacks.GetFeature != null && callbacks.GetFeature(e.id);

                KvSwitch sw = KvWidgets.Switch(content, e.title, initial, null);
                rows.Add(sw.gameObject);
                switches.Add(sw);
                switchIds.Add(e.id);

                string id = e.id;
                if (canToggle)
                {
                    // Пересобираем колбэк «поверх» созданного переключателя.
                    UnityEngine.UI.Button btn = sw.GetComponent<UnityEngine.UI.Button>();
                    if (btn != null)
                    {
                        btn.onClick.AddListener(delegate
                        {
                            if (callbacks.SetFeature != null) callbacks.SetFeature(id, sw.Value);
                        });
                    }
                }
                else
                {
                    sw.Locked = true;
                    UnityEngine.UI.Button box = sw.GetComponent<UnityEngine.UI.Button>();
                    if (box != null) box.interactable = false;
                    Text note = KvWidgets.Note(content,
                        string.IsNullOrEmpty(e.note)
                            ? "нет обработчика — будет добавлено позже"
                            : e.note,
                        KvTheme.TextDisabled);
                    rows.Add(note.gameObject);
                }
            }

            Divider();
            Header("Служебное");
            Button reset = KvTheme.CreateButton(content, "Reset", "Сбросить настройки интерфейса",
                delegate
                {
                    if (callbacks != null && callbacks.ResetAll != null) callbacks.ResetAll();
                    Rebuild();
                }, 20, true);
            rows.Add(reset.gameObject);

            Text note2 = KvWidgets.Note(content,
                "Пункты вкладки читаются из встроенной схемы и (если есть) из " +
                "StreamingAssets/" + KvSettingsSchema.ExternalFileName + " — новые пункты " +
                "добавляются без перекомпиляции.\n" + KvSettingsSchema.ExternalStatus,
                KvTheme.TextDisabled);
            rows.Add(note2.gameObject);
        }

        // ------------------------------------------------------------------ вкладка 2

        private void BuildBindings()
        {
            Header("Управление (только просмотр — бинды не редактируются)");
            List<KvBindingEntry> binds = KvSettingsSchema.Bindings();
            string lastGroup = null;
            foreach (KvBindingEntry b in binds)
            {
                if (b.group != lastGroup)
                {
                    Divider();
                    Header(b.group);
                    lastGroup = b.group;
                }
                RectTransform row = KvWidgets.CreateRow(content, "Bind_" + b.action, 17f, 6f);
                Text action = KvTheme.CreateText(row, "Action", b.action, KvTheme.FontSizeSmall,
                    TextAnchor.MiddleLeft, KvTheme.TextMain);
                LayoutElement le = action.gameObject.AddComponent<LayoutElement>();
                le.minWidth = 190f;
                le.preferredWidth = 190f;
                le.minHeight = 16f;

                Text keys = KvTheme.CreateText(row, "Keys", b.keys, KvTheme.FontSizeSmall,
                    TextAnchor.MiddleLeft, KvTheme.Accent);
                LayoutElement le2 = keys.gameObject.AddComponent<LayoutElement>();
                le2.minWidth = 120f;
                le2.preferredWidth = 120f;
                le2.minHeight = 16f;

                if (!string.IsNullOrEmpty(b.note))
                {
                    Text note = KvTheme.CreateText(row, "Note", b.note, KvTheme.FontSizeSmall - 1,
                        TextAnchor.MiddleLeft, KvTheme.TextDisabled);
                    LayoutElement le3 = note.gameObject.AddComponent<LayoutElement>();
                    le3.flexibleWidth = 1f;
                    le3.minHeight = 16f;
                }
                rows.Add(row.gameObject);
            }

            Divider();
            Text tail = KvWidgets.Note(content,
                "VR/MR: строки-заглушки, реальные бинды появятся вместе с VR-интерфейсом " +
                "(он делается отдельно, «приклеенным к рукам»).",
                KvTheme.TextDisabled);
            rows.Add(tail.gameObject);
        }

        // ------------------------------------------------------------------ вкладка 3

        private void BuildInterface()
        {
            // ЭТАП 2 (мультиязычность): выбор языка стоит ПЕРВЫМ пунктом вкладки
            // «Интерфейс», потому что от него зависят все подписи остальных вкладок.
            Header(KvLoc.T("settings.header.language", "Язык интерфейса"));
            BuildLanguageList();
            Text langNote = KvWidgets.Note(content,
                KvLoc.T("settings.language.note",
                    "Переключение мгновенное, выбор хранится в PlayerPrefs.") + "  " +
                KvLoc.T("status.language", "Язык") + ": " + KvLoc.CurrentName +
                " · " + KvLoc.Status,
                KvTheme.TextDisabled);
            rows.Add(langNote.gameObject);

            // ---------------------------------------------------------------- РЕЖИМ VR / MR
            // СТОИТ ВЫСОКО, СРАЗУ ПОСЛЕ ЯЗЫКА: это корневое решение (какой мир строить —
            // виртуальную лабораторию или комнату оператора), а не оформление. Положения
            // НАЗВАНЫ оба — «выключено» больше не значит «непонятно что».
            Divider();
            Header("Режим приложения (VR / MR)");
            int mode = callbacks != null && callbacks.GetMode != null ? callbacks.GetMode() : 0;
            KvSegmented modeSeg = KvWidgets.Segmented(content, "VrMr",
                new[]
                {
                    KazistovVvXr.KvXrSettings.ModeLabel(KazistovVvXr.KvXrSettings.ModeVr),
                    KazistovVvXr.KvXrSettings.ModeLabel(KazistovVvXr.KvXrSettings.ModeMr)
                }, Mathf.Clamp(mode, 0, KazistovVvXr.KvXrSettings.ModeCount - 1),
                delegate (int i)
                {
                    if (callbacks != null && callbacks.SetMode != null) callbacks.SetMode(i);
                    // Пересобираем панель: подпись состояния ниже обязана смениться в том же
                    // кадре, иначе переключатель молча врёт про текущий режим.
                    Rebuild();
                }, 22f);
            rows.Add(modeSeg.gameObject);

            Text modeNote = KvWidgets.Note(content,
                "MR — оператор видит свою комнату, робот ставится в неё (масштаб 1:1). " +
                "VR — виртуальная лаборатория. Настройка ОДНА для компьютера и шлема и " +
                "применяется после перезапуска приложения (сквозной вид пересоздаёт XR-сцену). " +
                KazistovVvXr.KvXrSettings.ModeStatusLine(),
                KvTheme.TextDim);
            rows.Add(modeNote.gameObject);

            Divider();
            Header(KvLoc.T("settings.header.theme", "Тема"));
            KvSegmented theme = KvWidgets.Segmented(content, "Theme",
                new[] { "Тёмная", "Светлая", "Системная" }, (int)KvTheme.Mode,
                delegate (int i)
                {
                    if (callbacks != null && callbacks.SetTheme != null)
                        callbacks.SetTheme((KvThemeMode)i);
                }, 20f);
            rows.Add(theme.gameObject);
            Text themeNote = KvWidgets.Note(content,
                "Переключение мгновенное, выбор хранится в PlayerPrefs. " +
                "Сейчас активна: " + KvTheme.ModeLabel + " (" + KvTheme.PaletteLabel + " палитра).",
                KvTheme.TextDim);
            rows.Add(themeNote.gameObject);

            Divider();
            Header(KvLoc.T("settings.header.scale", "Масштаб и плотность"));
            KvSegmented scale = KvWidgets.Segmented(content, "Scale",
                new[] { "85 %", "100 %", "115 %", "130 %" }, ScaleIndex(KvSettings.UiScale),
                delegate (int i)
                {
                    if (callbacks != null && callbacks.SetScale != null)
                        callbacks.SetScale(ScaleValue(i));
                }, 20f);
            rows.Add(scale.gameObject);

            KvSegmented density = KvWidgets.Segmented(content, "Density",
                new[] { "Компактная", "Обычная" }, KvSettings.Density,
                delegate (int i)
                {
                    if (callbacks != null && callbacks.SetDensity != null)
                        callbacks.SetDensity(i);
                }, 20f);
            rows.Add(density.gameObject);

            KvSegmented rowsSeg = KvWidgets.Segmented(content, "ToolbarRows",
                new[] { "1 ряд", "2 ряда", "3 ряда" }, Mathf.Clamp(KvSettings.ToolbarRows - 1, 0, 2),
                delegate (int i)
                {
                    if (callbacks != null && callbacks.SetToolbarRows != null)
                        callbacks.SetToolbarRows(i + 1);
                }, 20f);
            rows.Add(rowsSeg.gameObject);

            // ФИКС 8: раскладка тулбара — колонки считаются от ширины канваса, поэтому
            // на 1366×768 панель остаётся низкой, а не растёт в 8–9 рядов.
            KvSegmented barLayout = KvWidgets.Segmented(content, "ToolbarLayout",
                new[] { "Авто", "Широко", "5 в ряд" }, Mathf.Clamp(KvSettings.ToolbarLayout, 0, 2),
                delegate (int i)
                {
                    if (callbacks != null && callbacks.SetToolbarLayout != null)
                        callbacks.SetToolbarLayout(i);
                }, 20f);
            rows.Add(barLayout.gameObject);

            // ФИКС 9: компактный тулбар — иконки 20 px и без редко используемых кнопок
            // (эти команды дублируются меню, сами пункты меню продолжают работать).
            KvSwitch compact = KvWidgets.Switch(content, "Компактный тулбар",
                KvSettings.ToolbarCompact,
                delegate (bool v)
                {
                    if (callbacks != null && callbacks.SetToolbarCompact != null)
                        callbacks.SetToolbarCompact(v);
                });
            rows.Add(compact.gameObject);

            Text barNote = KvWidgets.Note(content,
                "Раскладка тулбара: «Авто» подбирает число колонок по ширине канваса так, " +
                "чтобы кнопки укладывались в 4 ряда; «Широко» занимает всю доступную ширину; " +
                "«5 в ряд» — прежний вид панели. Не поместившиеся команды уходят в меню «Ещё». " +
                "Выбор хранится в PlayerPrefs.",
                KvTheme.TextDisabled);
            rows.Add(barNote.gameObject);

            Divider();
            Header("Панели");
            AddPanelSwitch("tree", "Дерево моделей (слева)");
            AddPanelSwitch("properties", "Свойства (справа)");
            AddPanelSwitch("settings", "Настройки (низ)");
            AddPanelSwitch("metrics", "Панель метрик траекторий");
            AddPanelSwitch("limits", "Панель лимитов суставов");
            AddPanelSwitch("hotkeys", "Горячие клавиши (F12)");

            Divider();
            // --- ЭТАП 11: ДОСТУПНОСТЬ
            Header(KvLoc.T("settings.header.access", "Доступность"));
            Text fontLabel = KvWidgets.Note(content,
                KvLoc.T("settings.font", "Размер шрифта интерфейса"), KvTheme.TextDim);
            rows.Add(fontLabel.gameObject);
            KvSegmented font = KvWidgets.Segmented(content, "FontSize",
                new[]
                {
                    KvLoc.T("settings.font.small", "Маленький"),
                    KvLoc.T("settings.font.normal", "Средний"),
                    KvLoc.T("settings.font.large", "Большой")
                }, Mathf.Clamp(KvSettings.FontSize, 0, 2),
                delegate (int i)
                {
                    if (callbacks != null && callbacks.SetFontSize != null) callbacks.SetFontSize(i);
                }, 20f);
            rows.Add(font.gameObject);

            KvSwitch contrast = KvWidgets.Switch(content,
                KvLoc.T("settings.contrast", "Высокий контраст"),
                KvSettings.HighContrast,
                delegate (bool v)
                {
                    if (callbacks != null && callbacks.SetHighContrast != null)
                        callbacks.SetHighContrast(v);
                });
            rows.Add(contrast.gameObject);

            KvSegmented colorBlind = KvWidgets.Segmented(content, "ColorBlind",
                new[]
                {
                    KvLoc.T("settings.cb.off", "Обычные цвета"),
                    KvLoc.T("settings.cb.deuter", "Дейтеранопия"),
                    KvLoc.T("settings.cb.prot", "Протанопия"),
                    KvLoc.T("settings.cb.trit", "Тританопия")
                }, Mathf.Clamp(KvSettings.ColorBlind, 0, 3),
                delegate (int i)
                {
                    if (callbacks != null && callbacks.SetColorBlind != null)
                        callbacks.SetColorBlind(i);
                }, 20f);
            rows.Add(colorBlind.gameObject);
            Text cbNote = KvWidgets.Note(content,
                KvLoc.T("settings.cb.note",
                    "Схема заменяет красный/зелёный на пару «оранжевый ↔ синий» (палитра Okabe–Ito): " +
                    "она различима при всех трёх типах дальтонизма. Тип выбирается одним списком — " +
                    "отдельные наборы цветов для каждого типа появятся позже."),
                KvTheme.TextDisabled);
            rows.Add(cbNote.gameObject);

            KvSwitch keyboardNav = KvWidgets.Switch(content,
                KvLoc.T("settings.keyboardnav", "Навигация с клавиатуры (Tab, стрелки, Enter)"),
                KvSettings.KeyboardNav,
                delegate (bool v)
                {
                    if (callbacks != null && callbacks.SetKeyboardNav != null)
                        callbacks.SetKeyboardNav(v);
                });
            rows.Add(keyboardNav.gameObject);

            KvSwitch padHud = KvWidgets.Switch(content,
                KvLoc.T("settings.gamepadhud", "Виртуальный геймпад на экране"),
                KvSettings.GamepadHud,
                delegate (bool v)
                {
                    if (callbacks != null && callbacks.SetGamepadHud != null)
                        callbacks.SetGamepadHud(v);
                });
            rows.Add(padHud.gameObject);

            Divider();
            Header(KvLoc.T("settings.header.service", "Быстрые действия"));
            Button paletteBtn = KvTheme.CreateButton(content, "OpenPalette",
                KvLoc.T("settings.open.palette", "Палитра команд (Ctrl+P)"),
                delegate
                {
                    if (callbacks != null && callbacks.OpenPalette != null) callbacks.OpenPalette();
                }, 20, true);
            rows.Add(paletteBtn.gameObject);

            Button hotkeysBtn = KvTheme.CreateButton(content, "OpenHotkeys",
                KvLoc.T("settings.open.hotkeys", "Окно горячих клавиш (F12)"),
                delegate
                {
                    if (callbacks != null && callbacks.OpenHotkeys != null) callbacks.OpenHotkeys();
                }, 20, true);
            rows.Add(hotkeysBtn.gameObject);

            Button layoutBtn = KvTheme.CreateButton(content, "ResetLayout",
                KvLoc.T("settings.reset.layout", "Сбросить раскладку окон"),
                delegate
                {
                    if (callbacks != null && callbacks.ResetLayout != null) callbacks.ResetLayout();
                }, 20, true);
            rows.Add(layoutBtn.gameObject);

            Divider();
            Text hint = KvWidgets.Note(content,
                "Панели можно тащить за заголовок (прилипают к левому/правому/нижнему краю, " +
                "в центре — плавающее окно), тянуть за любой край или угол (курсор меняется), " +
                "сворачивать (▾) и убирать в боковую полочку (✕). Раскладка сохраняется в PlayerPrefs.",
                KvTheme.TextDisabled);
            rows.Add(hint.gameObject);
        }

        /// <summary>
        /// Список языков (ЭТАП 2): «Системный» + все языки словаря. Список берётся из
        /// `KvLoc.Languages`, поэтому НОВЫЙ ЯЗЫК добавляется простым файлом
        /// `StreamingAssets/kazistovvv_i18n/&lt;код&gt;.json` — он появится здесь сам.
        /// </summary>
        private void BuildLanguageList()
        {
            List<string> codes = new List<string>();
            List<string> labels = new List<string>();

            codes.Add(KvLoc.SystemCode);
            labels.Add(KvLoc.T("settings.language.system", "Системный (как в ОС)"));

            System.Collections.Generic.IReadOnlyList<KvLangInfo> all = KvLoc.Languages;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                codes.Add(all[i].code);
                labels.Add(all[i].Label + "  ·  " + all[i].strings);
            }

            string current = KvLoc.PreferenceCode;
            for (int i = 0; i < codes.Count; i++)
            {
                string code = codes[i];
                bool active = string.Equals(code, current, System.StringComparison.OrdinalIgnoreCase);
                string label = (active ? "●  " : "○  ") + labels[i];
                Button b = KvTheme.CreateSmallButton(content, "Lang_" + code, label, delegate
                {
                    KvLoc.SetLanguage(code);
                });
                if (b != null)
                {
                    Image img = b.targetGraphic as Image;
                    if (img != null) img.color = active ? KvTheme.ButtonChecked : KvTheme.ButtonBg;
                    Text t = b.GetComponentInChildren<Text>();
                    if (t != null) t.color = KvTheme.TextFor(active, true);
                    rows.Add(b.gameObject);
                }
            }
        }

        /// <summary>
        /// Подписи вкладок с учётом языка (ЭТАП 2). Массив <see cref="Tabs"/> остаётся
        /// РУССКИМ — это внутренние ключи: по ним внешний `kazistovvv_settings.json`
        /// указывает `tab`, поэтому переводится только подпись на экране.
        /// </summary>
        private static string[] TabLabels()
        {
            return new[]
            {
                KvLoc.T("settings.tab.functions", Tabs[0]),
                KvLoc.T("settings.tab.controls", Tabs[1]),
                KvLoc.T("settings.tab.interface", Tabs[2]),
                KvLoc.T("graphics.tab.title", Tabs[3]),
                KvLoc.T("settings.tab.about", Tabs[4]),
                KvLoc.T("settings.tab.help", Tabs[5])
            };
        }

        // ------------------------------------------------------------------ вкладка 4 «Графика»

        /// <summary>
        /// ЭТАП 3 ТЗ: раздел «Графика». Содержимое строит отдельный модуль
        /// `KazistovVvFeatures.KvGraphicsUi`: он складывает созданные строки в тот же
        /// список `rows`, поэтому штатная очистка вкладки работает без изменений.
        /// Вкладка построена так, что при недоступной службе (например, до её запуска)
        /// показывает честное сообщение вместо пустоты.
        /// </summary>
        private void BuildGraphics()
        {
            try
            {
                KazistovVvFeatures.KvGraphicsUi.BuildPanel(content, rows, Rebuild);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KazistovVv] вкладка «Графика» не собрана: " + e.Message);
                Text error = KvWidgets.Note(content,
                    "Раздел «Графика» недоступен: " + e.Message, KvTheme.Warn);
                rows.Add(error.gameObject);
            }
        }

        private void AddPanelSwitch(string id, string title)
        {
            bool visible = callbacks != null && callbacks.GetPanelVisible != null &&
                           callbacks.GetPanelVisible(id);
            KvSwitch sw = KvWidgets.Switch(content, title, visible, null);
            rows.Add(sw.gameObject);
            switches.Add(sw);
            switchIds.Add("panel:" + id);
            UnityEngine.UI.Button btn = sw.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(delegate
                {
                    if (callbacks != null && callbacks.SetPanelVisible != null)
                        callbacks.SetPanelVisible(id, sw.Value);
                });
            }
        }

        private static int ScaleIndex(float value)
        {
            if (value < 0.92f) return 0;
            if (value < 1.07f) return 1;
            if (value < 1.22f) return 2;
            return 3;
        }

        private static float ScaleValue(int index)
        {
            switch (index)
            {
                case 0: return 0.85f;
                case 2: return 1.15f;
                case 3: return 1.3f;
                default: return 1f;
            }
        }

        // ------------------------------------------------------------------ вкладка 4

        private void BuildAbout()
        {
            Header("Проект");
            AddInfo("Название", "KazistovVv");
            AddInfo("Назначение", "ПК-платформа управления роботами (SCARA и робот)");
            AddInfo("Интерфейс", "Десктопный, в стиле FreeCAD (тулбары, dock-панели, combo view)");
            AddInfo("UI-система", "uGUI (Unity UI) + кодовая фабрика элементов");
            AddInfo("Версия UI", KazistovVvUIManager.UiVersion);
            AddInfo("Иконки", "процедурные монохромные (без файлов-ассетов)");

            Divider();
            Header("Среда");
            AddInfo("Unity", Application.unityVersion);
            AddInfo("Платформа", Application.platform.ToString());
            AddInfo("Разрешение", Screen.width + " × " + Screen.height);
            AddInfo("Режим игры", Application.isPlaying ? "PlayMode" : "редактор");

            Divider();
            Header("Модули интерфейса");
            AddInfo("Команд в реестре", KvCommands.All.Count.ToString());
            AddInfo("Внешние настройки", KvSettingsSchema.ExternalStatus);
            // Сессия 18.09.2026: раздел «Графика» — подбор под железо и пресеты качества.
            if (KazistovVvFeatures.KvGraphicsService.Current != null)
            {
                KazistovVvFeatures.KvGraphicsService g = KazistovVvFeatures.KvGraphicsService.Current;
                AddInfo("Графика", KazistovVvFeatures.KvGraphicsModel.Label(g.EffectivePreset()) +
                    " · " + (g.Available ? "доступна" : "недоступна"));
                AddInfo("Пресеты графики", g.PresetFileStatus);
            }
            else
            {
                AddInfo("Графика", "служба ещё не запущена");
            }
            Text tail = KvWidgets.Note(content,
                "Состав кнопок/меню задаётся реестром команд: новая команда — новая кнопка " +
                "или пункт меню без правок панелей.", KvTheme.TextDisabled);
            rows.Add(tail.gameObject);
        }

        private void AddInfo(string label, string value)
        {
            Text v = KvWidgets.PropertyRow(content, label, value, 16f, 150f);
            rows.Add(v.transform.parent.gameObject);
        }

        // ------------------------------------------------------------------ вкладка 5 (справка)

        private void BuildHelp()
        {
            Header("Как работать (шаги алгоритма)");
            AddHelpRow("Шаг 0", "Включите красный лазер (Z или кнопка «Выбор точки») и наведите на поверхность");
            AddHelpRow("Шаг 1", "ЛКМ — точка фиксируется, идёт расчёт 8 траекторий (траектории сразу не показываются)");
            AddHelpRow("Шаг 2", "Появляются 8 «колбасок» и по каждому варианту едет свой фантом");
            AddHelpRow("Шаг 3", "Зелёный лазер (X) на «колбаску» + ЛКМ — вариант выбран, остальные фантомы убраны");
            AddHelpRow("Шаг 4", "Зелёный лазер на ДРУГУЮ «колбаску» + ЛКМ — переключение (фантом создаётся заново)");
            AddHelpRow("Шаг 5", "Зелёный лазер на фантом + ЛКМ — робот едет по траектории; траектории исчезают");
            AddHelpRow("Enter", "Режим перемещения точки (Q/E, W/S, A/D; Enter — подтвердить, Esc — отмена)");
            AddHelpRow("Esc", "Сброс сценария (вне режима перемещения точки)");

            Divider();
            Header("Интерфейс");
            AddHelpRow("TAB", "Показать/скрыть интерфейс и освободить курсор (телеоперация ⟷ работа с панелями)");
            AddHelpRow("Ctrl+P", "Палитра команд: поиск по ВСЕМ командам (тулбар, меню, настройки), Enter — выполнить");
            AddHelpRow("F12", "Окно горячих клавиш: все бинды, поиск, подсветка конфликтов");
            AddHelpRow("Тулбар", "Кнопки сгруппированы по областям; у каждой группы слева — своя иконка: " +
                "клик открывает список команд группы и позволяет свернуть её в одну иконку");
            AddHelpRow("Панели", "Заголовок можно тащить: у края подсвечивается зона прикрепления, " +
                "в центре панель становится плавающим окном");
            AddHelpRow("Размер", "Любое окно тянется за края и углы (курсор меняется); минимум 150×100, " +
                "максимум — размер канваса. Раскладка сохраняется в PlayerPrefs");
            AddHelpRow("Закрыть (✕)", "Сворачивает окно в боковую полочку; клик по полочке возвращает окно");
            AddHelpRow("Сброс", "«Вид → Панели → Сбросить раскладку» возвращает окна и группы тулбара на места");
            AddHelpRow("Дерево", "Двойной клик — переименовать, «глазик» — скрыть/показать объект, " +
                "ПКМ — контекстное меню (переименовать, дублировать, удалить, скрыть, фокус камеры, свойства, копировать имя)");
            AddHelpRow("Дерево", "Ctrl + клик — мультивыбор: действие контекстного меню применяется ко всем выбранным узлам");
            AddHelpRow("Меню", "Все команды дублируются в строке меню сверху (Файл/Правка/Вид/Робот/Сервис/Справка)");

            Divider();
            Header("Геймпад (подключается автоматически)");
            AddHelpRow("Левый стик", "Движение камеры (вперёд/назад/влево/вправо, как ходьба)");
            AddHelpRow("Правый стик", "Поворот камеры (обзор)");
            AddHelpRow("D-Pad ↑ / ↓", "Глубина шарика прицела (аналог колеса мыши)");
            AddHelpRow("D-Pad ← / →", "Переключение робот ⟷ SCARA");
            AddHelpRow("A / RT", "Подтверждение (аналог ЛКМ): точка, траектория, запуск");
            AddHelpRow("B", "Отмена / Esc");
            AddHelpRow("X / Y", "Красный лазер / зелёный лазер");
            AddHelpRow("LB / RB", "Аварийная остановка / домой (preset-поза)");
            AddHelpRow("LT", "Вход и подтверждение режима перемещения точки (аналог Enter)");
            AddHelpRow("Start / Select", "Палитра команд / список горячих клавиш");
            AddHelpRow("Индикатор", "Мини-геймпад в левом нижнем углу показывает текущие нажатия " +
                "(отключается в «Интерфейс → Виртуальный геймпад»)");

            Divider();
            Header("Что пока не реализовано (заглушки)");
            AddHelpRow("Экспорт/Импорт", "пункты меню «Файл» (сценарий) — задел");
            AddHelpRow("Измерение/Сетка", "пункты меню «Сервис → Инструменты» — задел");
            AddHelpRow("Переназначение", "кнопка «Переназначить» в окне горячих клавиш — заглушка (бинды не меняются)");
            AddHelpRow("Дальтоники", "наборы цветов по типам дальтонизма — общая схема уже работает, отдельные наборы позже");
            AddHelpRow("VR / MR", "бинды и панели для VR появятся вместе с VR-интерфейсом");

            Divider();
            Text tail = KvWidgets.Note(content,
                "Кнопки и пункты меню строятся из реестра команд (KvCommands), пункты настроек — " +
                "из схемы (встроенной + StreamingAssets/" + KvSettingsSchema.ExternalFileName + "). " +
                "Интерфейс — uGUI, создаётся кодом: сцена не меняется.", KvTheme.TextDisabled);
            rows.Add(tail.gameObject);
        }

        private void AddHelpRow(string left, string right)
        {
            RectTransform row = KvWidgets.CreateRow(content, "Help_" + left, 17f, 6f);
            Text l = KvTheme.CreateText(row, "Key", left, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.Accent);
            LayoutElement le = l.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 92f;
            le.preferredWidth = 92f;
            le.minHeight = 16f;

            Text r = KvTheme.CreateText(row, "Text", right, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            LayoutElement le2 = r.gameObject.AddComponent<LayoutElement>();
            le2.flexibleWidth = 1f;
            le2.minHeight = 16f;
            rows.Add(row.gameObject);
        }

        /// <summary>Обновить отрисовку при внешнем изменении (например, темы).</summary>
        public void RefreshValues()
        {
            // Вкладки 0 и 1 — списки (функции, бинды) — пересобираются по своим событиям;
            // остальные («Интерфейс», «Графика», «О программе», «Справка») показывают
            // текущие значения, поэтому пересобираются здесь.
            if (ActiveTab >= 2) Rebuild();
        }

        /// <summary>Перекрасить панель под текущую тему.</summary>
        public void Repaint()
        {
            if (bg != null) bg.color = KvTheme.PanelDark;
            Rebuild();
        }
    }
}
