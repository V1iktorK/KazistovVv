using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>Режим темы интерфейса (ТЗ: Тёмная / Светлая / Системная).</summary>
    public enum KvThemeMode
    {
        Dark = 0,
        Light = 1,
        System = 2
    }

    /// <summary>
    /// Тема и фабрика элементов десктопного интерфейса KazistovVv в стиле FreeCAD:
    /// плотная инженерная гамма, тонкие рамки, мелкий шрифт, иконки-монохром.
    ///
    /// Цвета — СВОЙСТВА (а не readonly-поля): палитра меняется переключателем темы
    /// мгновенно, без перезагрузки. Все элементы интерфейса создаются этой фабрикой,
    /// поэтому новая тема применяется к ним при пересборке оболочки (`KvTheme.Changed`).
    /// Выбор режима хранится в PlayerPrefs (ключ <see cref="PrefsThemeKey"/>).
    /// </summary>
    public static class KvTheme
    {
        /// <summary>PlayerPrefs-ключ выбранного режима темы.</summary>
        public const string PrefsThemeKey = "KazistovVv.Theme.Mode";

        /// <summary>
        /// Размер основного шрифта (плотная компоновка). ЭТАП 11: значение УМНОЖАЕТСЯ на
        /// выбранный в настройках масштаб шрифта (маленький / средний / большой), поэтому
        /// отдельная настройка размера действует на весь интерфейс сразу.
        /// </summary>
        public static int FontSize { get { return ScaleFont(13); } }
        /// <summary>Размер мелкого шрифта (дерево, статус-бар, подписи).</summary>
        public static int FontSizeSmall { get { return ScaleFont(11); } }
        /// <summary>Размер шрифта заголовков панелей/окон.</summary>
        public static int FontSizeTitle { get { return ScaleFont(14); } }

        /// <summary>Базовые размеры (до применения настройки размера шрифта, ЭТАП 11).</summary>
        public const int BaseFontSize = 13;
        public const int BaseFontSizeSmall = 11;
        public const int BaseFontSizeTitle = 14;

        /// <summary>Размер шрифта с учётом настройки «Размер шрифта» (ЭТАП 11).</summary>
        public static int ScaleFont(int baseSize)
        {
            return Mathf.Max(8, Mathf.RoundToInt(baseSize * KvSettings.FontScale));
        }

        // ======================================================== ЗАДАЧА 6: ТЕКСТ МЕЛЬЧЕ

        /// <summary>
        /// КОЭФФИЦИЕНТ РАЗМЕРА ТЕКСТА. Задача 6 владельца дословно: «текст уменьшаем — очень большой
        /// (давай попробуем на два порядка, а там глянем)».
        ///
        /// ПОЧЕМУ НЕ БУКВАЛЬНО В 100 РАЗ («на два порядка» — это ×0,01). Шрифт 13 px стал бы
        /// 0,13 px. Unity округляет размер шрифта до целых пикселей, то есть от текста не осталось
        /// бы ВООБЩЕ НИЧЕГО: вместо «а там глянем» — пустые кнопки, и уменьшать было бы уже нечего.
        /// Поэтому взято уменьшение в 2,2 раза (коэффициент 0,45): текст заметно мельче, но ещё
        /// читается, и его видно на экране. Дальше — ровно как просил владелец, «там глянем»:
        /// коэффициент это НАСТРОЙКА — ОДНО число, а не переписанные размеры в десятке мест; его
        /// меняют здесь и смотрят результат. Новое значение применяется при следующей пересборке
        /// панели шлема (открыть меню, сменить экран, перезапустить приложение). Границы
        /// <see cref="MinTextScale"/>…<see cref="MaxTextScale"/> оставляют место и для более
        /// решительного шага, вплоть до «на два порядка» — но там текст уже не читается.
        ///
        /// ГДЕ ДЕЙСТВУЕТ. На интерфейсе ШЛЕМА: всё, что рисует `KvXrGroupPanel` — подписи строк,
        /// заголовки экранов и подписи иконок (`CreateLabel`, `CreateIcon`, верхняя подпись).
        /// Компьютерный интерфейс намеренно НЕ тронут: в задаче сказано «в интерфейсе шлема»,
        /// а вид компьютера владелец уже принял — менять его без просьбы нельзя.
        ///
        /// ПОЧЕМУ НЕ `metersPerCanvasUnit`. Он задаёт, сколько метров в одном пикселе канваса.
        /// Уменьшив его, мы уменьшили бы панель ЦЕЛИКОМ — вместе с иконками и строками, — а
        /// владелец просил мельче только ТЕКСТ: сами кнопки и строки его устраивают.
        /// </summary>
        public static float TextScale
        {
            get { return textScale; }
            set { textScale = ClampTextScale(value); }
        }

        /// <summary>Значение по умолчанию: 0,45 — уменьшение примерно в 2,2 раза.</summary>
        public const float DefaultTextScale = 0.45f;

        /// <summary>Нижняя граница коэффициента: 0,15 — уменьшение в 6,7 раза, дальше не читается.</summary>
        public const float MinTextScale = 0.15f;

        /// <summary>Верхняя граница коэффициента: 1 — размер как был до задачи 6.</summary>
        public const float MaxTextScale = 1f;

        /// <summary>
        /// ПРЕДЕЛ, НИЖЕ КОТОРОГО ШРИФТ НЕ РИСУЕТСЯ, пиксели. Нужен, потому что при коэффициенте
        /// 0,45 мелкие подписи (8–11 px) превращаются в 4–5 px, а при меньшем коэффициенте ушли бы
        /// в ноль. 4 px — последняя ступень, на которой глиф ещё различим.
        /// </summary>
        public const int MinTextPointSize = 4;

        private static float textScale = DefaultTextScale;

        /// <summary>Привести коэффициент к допустимому диапазону (и отсеять NaN с бесконечностью).</summary>
        public static float ClampTextScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return DefaultTextScale;
            return Mathf.Clamp(value, MinTextScale, MaxTextScale);
        }

        /// <summary>
        /// РАЗМЕР ШРИФТА С УЧЁТОМ КОЭФФИЦИЕНТА (задача 6). Через неё идёт весь текст шлема:
        /// явные размеры подписей (`CreateLabel`) и подписи кнопок (`CreateIcon`). Раньше в шлеме
        /// стоял ровно тот размер, который передал вызывающий код: заголовок экрана 18 px при
        /// 0,0025 м на пиксель — это 4,5 см буквы, отсюда и «очень большой».
        /// </summary>
        public static int ScaledText(int baseSize)
        {
            int size = Mathf.RoundToInt(Mathf.Max(1, baseSize) * ClampTextScale(textScale));
            return Mathf.Max(MinTextPointSize, size);
        }

        /// <summary>Тема изменилась (пересобрать оболочку/перекрасить оверлеи).</summary>
        public static event System.Action Changed;

        private static KvThemeMode mode = KvThemeMode.Dark;
        private static bool loaded;
        private static bool light;

        // ------------------------------------------------------------------ палитра

        private static bool Light => light;

        public static Color WindowBg { get { return Hi(Light ? C(0.855f, 0.860f, 0.870f) : C(0.125f, 0.128f, 0.138f), Light ? C(1f, 1f, 1f) : C(0f, 0f, 0f)); } }
        public static Color PanelBg { get { return Hi(Light ? C(0.815f, 0.820f, 0.835f) : C(0.160f, 0.163f, 0.175f), Light ? C(0.960f, 0.960f, 0.965f) : C(0.020f, 0.020f, 0.024f)); } }
        public static Color PanelHeader { get { return Hi(Light ? C(0.745f, 0.752f, 0.772f) : C(0.205f, 0.210f, 0.228f), Light ? C(0.900f, 0.905f, 0.915f) : C(0.060f, 0.062f, 0.070f)); } }
        public static Color PanelDark { get { return Hi(Light ? C(0.930f, 0.932f, 0.940f) : C(0.112f, 0.115f, 0.126f), Light ? C(1f, 1f, 1f) : C(0f, 0f, 0f)); } }
        public static Color ButtonBg { get { return Hi(Light ? C(0.735f, 0.740f, 0.755f) : C(0.250f, 0.255f, 0.275f), Light ? C(0.880f, 0.884f, 0.892f) : C(0.090f, 0.092f, 0.100f)); } }
        public static Color ButtonHover { get { return Hi(Light ? C(0.830f, 0.836f, 0.852f) : C(0.325f, 0.335f, 0.365f), Light ? C(0.960f, 0.960f, 0.965f) : C(0.220f, 0.225f, 0.240f)); } }
        public static Color ButtonPressed { get { return Hi(Light ? C(0.640f, 0.650f, 0.670f) : C(0.190f, 0.196f, 0.212f), Light ? C(0.740f, 0.745f, 0.760f) : C(0.030f, 0.032f, 0.038f)); } }
        public static Color ButtonChecked { get { return Hi(Light ? C(0.545f, 0.715f, 0.920f) : C(0.180f, 0.400f, 0.660f), Light ? C(0.250f, 0.560f, 0.980f) : C(0.100f, 0.430f, 0.950f)); } }
        public static Color ButtonActive { get { return ButtonChecked; } }
        public static Color Accent { get { return Hi(Light ? C(0.130f, 0.400f, 0.740f) : C(0.360f, 0.620f, 0.950f), Light ? C(0.000f, 0.220f, 0.700f) : C(0.400f, 0.760f, 1.000f)); } }
        public static Color TextMain { get { return Hi(Light ? C(0.090f, 0.095f, 0.105f) : C(0.900f, 0.910f, 0.930f), Light ? C(0f, 0f, 0f) : C(1f, 1f, 1f)); } }
        public static Color TextDim { get { return Hi(Light ? C(0.330f, 0.340f, 0.360f) : C(0.620f, 0.640f, 0.672f), Light ? C(0.070f, 0.075f, 0.085f) : C(0.860f, 0.870f, 0.890f)); } }
        public static Color TextDisabled { get { return Hi(Light ? C(0.560f, 0.570f, 0.585f) : C(0.400f, 0.410f, 0.430f), Light ? C(0.330f, 0.340f, 0.360f) : C(0.620f, 0.630f, 0.650f)); } }
        public static Color Border { get { return Hi(Light ? C(0.560f, 0.570f, 0.590f) : C(0.075f, 0.080f, 0.092f), Light ? C(0.300f, 0.310f, 0.330f) : C(0.850f, 0.860f, 0.880f)); } }
        public static Color Separator { get { return Hi(Light ? C(0.650f, 0.660f, 0.680f) : C(0.280f, 0.290f, 0.315f), Light ? C(0.420f, 0.430f, 0.450f) : C(0.700f, 0.710f, 0.730f)); } }
        public static Color SelectionBg { get { return Hi(Light ? C(0.560f, 0.720f, 0.930f) : C(0.170f, 0.380f, 0.640f), Light ? C(0.150f, 0.520f, 1.000f) : C(0.050f, 0.400f, 0.980f)); } }
        public static Color RowAlt { get { return Hi(Light ? C(0.870f, 0.874f, 0.884f) : C(0.148f, 0.152f, 0.165f), Light ? C(0.930f, 0.933f, 0.940f) : C(0.070f, 0.072f, 0.080f)); } }
        public static Color InputBg { get { return Hi(Light ? C(0.955f, 0.958f, 0.965f) : C(0.100f, 0.104f, 0.115f), Light ? C(1f, 1f, 1f) : C(0f, 0f, 0f)); } }
        public static Color IconTint { get { return Hi(Light ? C(0.150f, 0.160f, 0.180f) : C(0.860f, 0.870f, 0.890f), Light ? C(0f, 0f, 0f) : C(1f, 1f, 1f)); } }
        public static Color Ok { get { return Semantic(Hi(Light ? C(0.130f, 0.540f, 0.180f) : C(0.360f, 0.800f, 0.400f), Light ? C(0.000f, 0.420f, 0.100f) : C(0.350f, 1.000f, 0.450f)), SemanticSlot.Green); } }
        public static Color Warn { get { return Semantic(Hi(Light ? C(0.720f, 0.480f, 0.020f) : C(0.950f, 0.720f, 0.200f), Light ? C(0.700f, 0.420f, 0.000f) : C(1.000f, 0.800f, 0.100f)), SemanticSlot.Warn); } }
        public static Color Error { get { return Semantic(Hi(Light ? C(0.750f, 0.140f, 0.110f) : C(0.920f, 0.320f, 0.300f), Light ? C(0.700f, 0.000f, 0.000f) : C(1.000f, 0.250f, 0.200f)), SemanticSlot.Red); } }
        /// <summary>Красный лазер (выбор точки) — только как индикатор состояния кнопки.</summary>
        public static Color LaserRed { get { return Semantic(Hi(Light ? C(0.780f, 0.130f, 0.120f) : C(1.000f, 0.180f, 0.160f), Light ? C(0.800f, 0.000f, 0.000f) : C(1.000f, 0.100f, 0.050f)), SemanticSlot.Red); } }
        /// <summary>Зелёный лазер (выбор траектории).</summary>
        public static Color LaserGreen { get { return SematicGreen(); } }

        /// <summary>ЭТАП 11: высокий контраст включён (отдельный режим интерфейса).</summary>
        public static bool HighContrast
        {
            get { return KvSettings.HighContrast; }
        }

        /// <summary>ЭТАП 11: выбранная схема для дальтоников (0 — выключена).</summary>
        public static int ColorBlindMode
        {
            get { return KvSettings.ColorBlind; }
        }

        private static Color Hi(Color normal, Color high)
        {
            return HighContrast ? high : normal;
        }

        /// <summary>Семантические слоты, которые перекрашиваются схемой для дальтоников.</summary>
        private enum SemanticSlot
        {
            Red,
            Green,
            Warn,
            Info
        }

        private static Color SematicGreen()
        {
            Color normal = Hi(Light ? C(0.090f, 0.560f, 0.180f) : C(0.180f, 1.000f, 0.360f),
                Light ? C(0.000f, 0.480f, 0.120f) : C(0.100f, 1.000f, 0.300f));
            return Semantic(normal, SemanticSlot.Green);
        }

        /// <summary>
        /// Схема для дальтоников (ЭТАП 11). При выключенной настройке возвращается обычный
        /// цвет. При включённой красный/зелёный заменяются парой «оранжевый ↔ синий»
        /// (палитра Okabe–Ito): она различима при дейтеранопии, протанопии и тританопии.
        /// </summary>
        private static Color Semantic(Color normal, SemanticSlot slot)
        {
            int mode = ColorBlindMode;
            if (mode == 0) return normal;
            switch (slot)
            {
                case SemanticSlot.Red: return C(0.902f, 0.624f, 0.000f);       // оранжевый
                case SemanticSlot.Green: return C(0.337f, 0.706f, 0.914f);     // небесно-синий
                case SemanticSlot.Warn: return C(0.941f, 0.894f, 0.259f);      // жёлтый
                default: return C(0.800f, 0.475f, 0.655f);                     // розово-сиреневый
            }
        }

        private static Color C(float r, float g, float b)
        {
            return new Color(r, g, b, 1f);
        }

        // ------------------------------------------------------------------ шрифт/спрайт

        /// <summary>
        /// Шрифт интерфейса. С ЭТАПА 2 (мультиязычность) зависит от языка: для китайского и
        /// японского берётся системный шрифт с CJK-глифами (`KvLoc.Font`), потому что встроенный
        /// LegacyRuntime.ttf их не содержит и текст выглядел бы квадратами. Для остальных языков
        /// поведение прежнее — встроенный шрифт (ноль изменений в текущем виде интерфейса).
        /// </summary>
        public static Font Font
        {
            get { return KvLoc.UiFont(FontSize); }
        }

        // ------------------------------------------------------------------ режим темы

        /// <summary>Выбранный режим (может быть «системная»).</summary>
        public static KvThemeMode Mode
        {
            get { EnsureLoaded(); return mode; }
        }

        /// <summary>Фактически применённая палитра светлая?</summary>
        public static bool IsLight
        {
            get { EnsureLoaded(); return light; }
        }

        /// <summary>«Тёмная» / «Светлая» / «Системная».</summary>
        public static string ModeLabel
        {
            get
            {
                switch (Mode)
                {
                    case KvThemeMode.Light: return "Светлая";
                    case KvThemeMode.System: return "Системная";
                    default: return "Тёмная";
                }
            }
        }

        /// <summary>Подпись фактической палитры: «тёмная» / «светлая» (+ пометки доступности).</summary>
        public static string PaletteLabel
        {
            get
            {
                string label = IsLight ? "светлая" : "тёмная";
                if (HighContrast) label += " · высокий контраст";
                if (ColorBlindMode != 0) label += " · схема для дальтоников";
                return label;
            }
        }

        /// <summary>Идентификатор иконки для текущего режима (кнопка «Тема»).</summary>
        public static string ModeIcon
        {
            get
            {
                switch (Mode)
                {
                    case KvThemeMode.Light: return "theme-light";
                    case KvThemeMode.System: return "theme-system";
                    default: return "theme-dark";
                }
            }
        }

        /// <summary>Переключить режим (Тёмная → Светлая → Системная → Тёмная).</summary>
        public static KvThemeMode Cycle()
        {
            KvThemeMode next = Mode == KvThemeMode.Dark ? KvThemeMode.Light
                : Mode == KvThemeMode.Light ? KvThemeMode.System
                : KvThemeMode.Dark;
            SetMode(next);
            return next;
        }

        /// <summary>Задать режим темы (мгновенно, с сохранением в PlayerPrefs).</summary>
        public static void SetMode(KvThemeMode value, bool save = true)
        {
            EnsureLoaded();
            mode = value;
            if (save)
            {
                PlayerPrefs.SetInt(PrefsThemeKey, (int)value);
                PlayerPrefs.Save();
            }
            Apply();
            if (Changed != null) Changed();
        }

        /// <summary>Перечитать тему из PlayerPrefs (вызывается на старте).</summary>
        public static void Reload()
        {
            loaded = false;
            EnsureLoaded();
            Apply();
            if (Changed != null) Changed();
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            mode = (KvThemeMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefsThemeKey, (int)KvThemeMode.Dark), 0, 2);
            Apply();
        }

        private static void Apply()
        {
            light = mode == KvThemeMode.Light || (mode == KvThemeMode.System && SystemPrefersLight());
        }

        /// <summary>
        /// «Системная» тема: пробуем определить оформление ОС (Windows — AppsUseLightTheme).
        /// Сборка `Microsoft.Win32.Registry` в Unity-профиле .NET Standard недоступна напрямую,
        /// поэтому обращение идёт ЧЕРЕЗ РЕФЛЕКСИЮ: если тип/ключ недоступны (другая ОС, сборка
        /// без этой сборки, нет прав) — молча возвращаем «тёмную», как в FreeCAD по умолчанию.
        /// </summary>
        private static bool SystemPrefersLight()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                System.Type regType = System.Type.GetType(
                    "Microsoft.Win32.Registry, Microsoft.Win32.Registry", false);
                if (regType != null)
                {
                    System.Reflection.PropertyInfo prop = regType.GetProperty("CurrentUser");
                    object currentUser = prop != null ? prop.GetValue(null, null) : null;
                    if (currentUser != null)
                    {
                        System.Reflection.MethodInfo open = currentUser.GetType().GetMethod(
                            "OpenSubKey", new[] { typeof(string) });
                        object key = open != null ? open.Invoke(currentUser, new object[]
                        {
                            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
                        }) : null;
                        if (key != null)
                        {
                            System.Reflection.MethodInfo get = key.GetType().GetMethod(
                                "GetValue", new[] { typeof(string) });
                            object value = get != null ? get.Invoke(key, new object[]
                            {
                                "AppsUseLightTheme"
                            }) : null;
                            if (value is int) return (int)value != 0;
                        }
                    }
                }
            }
            catch
            {
                // реестр недоступен — падаем в тёмную тему
            }
#endif
            return false;
        }

        // ------------------------------------------------------------------ фабрика

        /// <summary>Белая текстура-спрайт (без файлов ассетов).</summary>
        public static Sprite WhiteSprite
        {
            get { return UIFactory.GetSprite(); }
        }

        public static Image CreatePanel(RectTransform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Панель с рамкой в стиле FreeCAD (тонкая линия по контуру).</summary>
        public static Image CreateFrame(RectTransform parent, string name, Color fill, Color border)
        {
            Image img = CreatePanel(parent, name, fill);
            Outline o = img.gameObject.AddComponent<Outline>();
            o.effectColor = border;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;
            return img;
        }

        public static Text CreateText(RectTransform parent, string name, string content, int size,
            TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.text = content;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>
        /// Плотная кнопка с подписью (диалоги/панели).
        ///
        /// ФИКС 6 (§23): размер задаётся ЯВНО — и `LayoutElement`, и `sizeDelta`.
        /// Раньше кнопка не имела ни того, ни другого, и в контейнере, который размерами
        /// детей НЕ управляет (`childControlHeight = false` — так устроены вертикальные
        /// списки панелей: настройки, функции, графика, автоматизация), она рисовалась
        /// квадратом 100×100: это «слишком большие иконки/кнопки» со скриншотов оператора.
        /// Высота = параметр <paramref name="height"/> (эталон 24 — KvWidgets.TextButtonHeight),
        /// ширина — по длине подписи (та же оценка 7.6 px/символ + 16 px отступа, что в §22).
        /// Вызывающий код по-прежнему может переопределить размер: он задаётся ДО возврата,
        /// поэтому любые последующие `sizeDelta`/`Fit`/`Stretch` имеют приоритет.
        /// </summary>
        public static Button CreateButton(RectTransform parent, string name, string label,
            System.Action onClick, int height = 24, bool small = true)
        {
            GameObject go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            Image img = go.GetComponent<Image>();
            img.color = ButtonBg;
            img.sprite = WhiteSprite;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Ratio(ButtonHover, ButtonBg);
            cb.pressedColor = Ratio(ButtonPressed, ButtonBg);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.55f);
            cb.fadeDuration = 0.05f;
            btn.colors = cb;

            // ФИКС 6 (§23): явный размер (см. комментарий выше).
            int h = Mathf.Max(14, height);
            float w = Mathf.Max(KvWidgets.MinTextButtonWidth, label.Length * KvWidgets.TextButtonCharWidth + 16f);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minWidth = w;
            le.preferredWidth = w;
            le.minHeight = h;
            le.preferredHeight = h;
            ((RectTransform)go.transform).sizeDelta = new Vector2(w, h);

            Text txt = CreateText((RectTransform)go.transform, "Label", label,
                small ? FontSizeSmall : FontSize, TextAnchor.MiddleCenter, TextMain);
            Stretch(txt.rectTransform, KvWidgets.ButtonPadding, 2);

            if (onClick != null) btn.onClick.AddListener(delegate { onClick(); });
            return btn;
        }

        public static Button CreateSmallButton(RectTransform parent, string name, string label,
            System.Action onClick)
        {
            return CreateButton(parent, name, label, onClick, 18, true);
        }

        /// <summary>Тонкая линия-разделитель.</summary>
        public static Image CreateDivider(RectTransform parent, bool vertical = false)
        {
            Image div = CreatePanel(parent, "Divider", Separator);
            if (vertical) div.rectTransform.sizeDelta = new Vector2(1f, 0f);
            else div.rectTransform.sizeDelta = new Vector2(0f, 1f);
            return div;
        }

        public static void Stretch(RectTransform rt, float padL = 0, float padR = 0,
            float padT = 0, float padB = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padL, padB);
            rt.offsetMax = new Vector2(-padR, -padT);
        }

        /// <summary>Множитель цвета для ColorBlock кнопки (hover/pressed от базового).</summary>
        public static Color Ratio(Color target, Color baseColor)
        {
            float r = baseColor.r > 0.001f ? Mathf.Clamp(target.r / baseColor.r, 0.2f, 3f) : 1f;
            float g = baseColor.g > 0.001f ? Mathf.Clamp(target.g / baseColor.g, 0.2f, 3f) : 1f;
            float b = baseColor.b > 0.001f ? Mathf.Clamp(target.b / baseColor.b, 0.2f, 3f) : 1f;
            return new Color(r, g, b, 1f);
        }

        /// <summary>Цвет текста для строки дерева/таблицы по состоянию.</summary>
        public static Color TextFor(bool selected, bool enabled)
        {
            if (!enabled) return TextDisabled;
            return selected && IsLight ? Color.white : TextMain;
        }
    }

    /// <summary>Разовые фабричные хелперы (белый спрайт).</summary>
    public static class UIFactory
    {
        private static Sprite _sprite;

        /// <summary>Белая текстура-спрайт для UI (без файлов ассетов).</summary>
        public static Sprite GetSprite()
        {
            if (_sprite != null) return _sprite;
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            Color[] px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            _sprite.hideFlags = HideFlags.HideAndDontSave;
            return _sprite;
        }
    }
}
