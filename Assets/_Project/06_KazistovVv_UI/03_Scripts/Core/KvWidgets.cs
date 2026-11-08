using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// Виджеты десктопного интерфейса в стиле FreeCAD: кнопки-иконки, разделители,
    /// заголовки секций, строки «свойство = значение», переключатели и сегментные
    /// переключатели (радио-строка). Всё создаётся кодом — файлов-ассетов нет.
    /// </summary>
    public static class KvWidgets
    {
        // ================================================================== ЭТАЛОН РАЗМЕРОВ
        // (ФИКС 6, §22; зафиксирован здесь по §0.6, ЭТАП 10 — «единый набор иконок».)
        //
        //   • СЕТКА ИКОНОК: KvIcons.GridSize = 24, штрих KvIcons.Stroke = 1.5 px
        //     (мелкие детали — KvIcons.StrokeThin = 1.2 px). Это эталон для ВСЕХ иконок.
        //   • Кнопка тулбара: квадрат 24 (плотность 0) / 28 (плотность 1), глиф 0.78 от стороны
        //     ≈ 19–22 px (см. IconButton и `IconButtonSize` — P3-8).
        //   • Строка-переключатель (галка): высота 20 px, КВАДРАТ галки 16×16,
        //     глиф галки 12×12 — та же пропорция 0.78, что и у кнопки тулбара.
        //   • Галки в меню (KvMenuBar / KvContextMenu): 10×10 при высоте строки 18 px.
        //
        // ПОЧЕМУ ЭТО ВЫНЕСЕНО В КОНСТАНТЫ (корень ФИКСА 6): строка переключателя —
        // это HorizontalLayoutGroup, и раньше у неё было childControlWidth = false.
        // В таком режиме Unity берёт РАЗМЕР ребёнка из его `sizeDelta` (у нового
        // RectTransform это 100×100), а НЕ из LayoutElement. Поэтому квадрат галки
        // получался 100×100, а иконка «✓» (она растянута по квадрату) — во весь квадрат:
        // ровно то, что оператор описал как «слишком большие квадратики».
        // Размеры задаются ЯВНО и проверяются методом <see cref="ValidateSizes"/>.
        public const float SwitchRowHeight = 20f;
        public const float SwitchBoxSize = 16f;
        public const int SwitchCheckIcon = 12;
        /// <summary>Доля квадрата, которую занимает глиф (эталон 0.78 — как у кнопки тулбара).</summary>
        public const float GlyphRatio = 0.78f;

        // ---- эталон §23 (ФИКС 6, дополнение к §22) --------------------------------------
        //   • Кнопка-иконка: квадрат 24 (плотность 0) или 28 (плотность 1) — значение берётся
        //     из `KvSettings.IconButtonSize`, см. пояснение к `IconButtonSize` ниже (P3-8);
        //     глиф 0.78 от стороны ≈ 19–22 (см. IconButton);
        //   • Кнопка с текстом: высота 24, внутренний отступ 6 px (KvTheme.CreateButton);
        //   • ВКЛАДКА (панель настроек, верстак): высота 26 — внутри требуемого диапазона
        //     24…28, шрифт мелкий (FontSizeSmall).
        //
        // ПОЧЕМУ ЭТО ВАЖНО: `RectTransform` нового объекта по умолчанию имеет sizeDelta
        // 100×100. Если контейнер НЕ управляет размерами детей (`childControlWidth/Height =
        // false` — так устроены строки `CreateRow`), Unity берёт размер ребёнка ИМЕННО из
        // sizeDelta, а `LayoutElement` игнорирует. Поэтому у каждой кнопки размер задаётся
        // ЯВНО И ДВАЖДЫ (LayoutElement + sizeDelta) — это и есть лечение «огромных
        // квадратиков» и «непонятных +/−» (ФИКС 8: кнопки «развернуть/свернуть дерево»).
        // ---- P3-8 (11.11.2026): ЭТАЛОН СТОРОНЫ КНОПКИ-ИКОНКИ — ОДИН, В `KvSettings` ----------
        //
        // ЧТО БЫЛО. Здесь стояло `public const float IconButtonSize = 24f;`, а рядом жило второе
        // значение того же понятия — `KvSettings.IconButtonSize => Density == 0 ? 24f : 28f`.
        // Тулбар берёт размер из `KvSettings` (то есть при плотности 1 у него 28), а самопроверка
        // `ValidateSizes()` сверяла КОНСТАНТУ 24 и рапортовала «провалов 0» при реальных 28 px.
        //
        // ЧЕМ ЭТО ГРОЗИЛО. Самопроверка подтверждала то, чего нет: оператор включал плотность 1,
        // кнопки становились 28, а отчёт «эталон соблюдён» говорил, что всё в порядке. Хуже —
        // дерево (`KvTreeView`) строилось по 24 и рядом с тулбаром на 28 разъезжалось.
        //
        // ТЕПЕРЬ: не константа, а ССЫЛКА на единственный источник (`KvSettings.IconButtonSize`).
        // Имя оставлено, чтобы не переписывать вызовы; значение приходит из настроек плотности.
        public static float IconButtonSize { get { return KvSettings.IconButtonSize; } }

        public const float TextButtonHeight = 24f;
        public const float TabHeight = 26f;
        /// <summary>Внутренний отступ кнопки с текстом (эталон).</summary>
        public const float ButtonPadding = 6f;
        /// <summary>Минимальная ширина кнопки с текстом (до подгонки по длине подписи).</summary>
        public const float MinTextButtonWidth = 60f;
        /// <summary>Ширина одного символа подписи в пикселях (та же оценка, что в §22).</summary>
        public const float TextButtonCharWidth = 7.6f;

        // ------------------------------------------------------------------ контейнеры

        public static RectTransform CreateRow(RectTransform parent, string name, float height,
            float spacing = 4f, TextAnchor align = TextAnchor.MiddleLeft, bool expandHeight = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            HorizontalLayoutGroup h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = expandHeight;
            h.childControlWidth = false;
            h.childControlHeight = false;
            if (height > 0f)
            {
                LayoutElement le = go.AddComponent<LayoutElement>();
                le.minHeight = height;
                le.preferredHeight = height;
                // ФИКС 6 (§22): высота строки задаётся ЕЩЁ И ЯВНО. LayoutElement работает
                // только когда родитель управляет размером детей (childControlHeight = true),
                // а контейнеры вкладок верстака и панели функций это управление ВЫКЛЮЧАЮТ —
                // тогда строка брала дефолтный sizeDelta (100 px) и все «квадратики»
                // выглядели огромными. Ширина НЕ трогается: её задаёт родитель.
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            }
            return rt;
        }

        public static RectTransform CreateColumn(RectTransform parent, string name, float height,
            float spacing = 2f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            VerticalLayoutGroup v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            v.childControlHeight = false;
            if (height > 0f)
            {
                LayoutElement le = go.AddComponent<LayoutElement>();
                le.minHeight = height;
                le.preferredHeight = height;
            }
            return rt;
        }

        /// <summary>
        /// Задать размер элементу — И через `LayoutElement`, И через `sizeDelta` (ФИКС 6, §23).
        ///
        /// ПОЧЕМУ ОБА: `LayoutElement` работает ТОЛЬКО когда родитель управляет размерами детей
        /// (`childControlWidth/Height = true`). Панели настроек, функций, графики и верстака
        /// это управление выключают (`childControlHeight = false`), и тогда Unity берёт размер
        /// ребёнка из его `sizeDelta` — у нового `RectTransform` это 100×100. Отсюда «слишком
        /// большие» строки, галки, подписи и кнопки на скриншотах оператора: сам элемент был
        /// 16×16, а СТРОКА вокруг него — 100 px. Дублирование значения в `sizeDelta` лечит это
        /// у всех, кто уже вызывает `Fit`, без правки каждого места по отдельности.
        /// </summary>
        public static LayoutElement Fit(GameObject go, float width, float height)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (width > 0f) { le.minWidth = width; le.preferredWidth = width; }
            if (height > 0f) { le.minHeight = height; le.preferredHeight = height; }

            RectTransform rt = go.transform as RectTransform;
            if (rt != null && (width > 0f || height > 0f))
            {
                float w = width > 0f ? width : rt.sizeDelta.x;
                float h = height > 0f ? height : rt.sizeDelta.y;
                rt.sizeDelta = new Vector2(w, h);
            }
            return le;
        }

        // ------------------------------------------------------------------ текст

        public static Text Label(RectTransform parent, string name, string text, int size,
            Color color, TextAnchor anchor = TextAnchor.MiddleLeft, float width = 0f)
        {
            Text t = KvTheme.CreateText(parent, name, text, size, anchor, color);
            if (width > 0f) Fit(t.gameObject, width, 0f);
            return t;
        }

        /// <summary>Заголовок секции панели (как «Tasks»/«Properties» в FreeCAD).</summary>
        public static RectTransform SectionHeader(RectTransform parent, string title, float height = 20f)
        {
            RectTransform row = CreateRow(parent, "Header_" + title, height, 6f);
            Image bg = KvTheme.CreatePanel(row, "Bg", KvTheme.PanelHeader);
            KvTheme.Stretch(bg.rectTransform);
            bg.transform.SetAsFirstSibling();
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;

            Text t = KvTheme.CreateText(row, "Title", title.ToUpperInvariant(), KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextDim);
            Fit(t.gameObject, 0f, height);
            t.rectTransform.offsetMin = new Vector2(6f, 0f);
            return row;
        }

        // ------------------------------------------------------------------ кнопка-иконка

        /// <summary>
        /// Кнопка ТОЛЬКО с иконкой (без подписи) — основной элемент тулбара FreeCAD.
        /// Размер задаётся ЯВНО и дважды (ФИКС 6/8 §23): LayoutElement И sizeDelta.
        /// </summary>
        public static KvIconButton IconButton(RectTransform parent, string name, string iconId,
            float size, Action onClick)
        {
            GameObject go = new GameObject(name, typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.ButtonBg;
            bg.raycastTarget = true;

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.minWidth = size;
            le.minHeight = size;
            le.preferredWidth = size;
            le.preferredHeight = size;

            // ФИКС 6/8 (§23): без этой строки кнопка в строке `CreateRow` (там
            // childControlWidth/Height = false) рисовалась квадратом 100×100 — так и
            // выглядели «непонятные + и −» в панели дерева: это кнопки «раскрыть/свернуть
            // всё дерево», у которых LayoutElement игнорировался, а глиф «+»/«−» был виден
            // в углу огромного квадрата. Явный sizeDelta работает при ЛЮБОМ родителе.
            RectTransform selfRect = (RectTransform)go.transform;
            selfRect.sizeDelta = new Vector2(size, size);

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.ButtonBg);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.ButtonBg);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            cb.fadeDuration = 0.05f;
            btn.colors = cb;

            GameObject iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            Image icon = iconGo.GetComponent<Image>();
            icon.sprite = KvIcons.Get(iconId, Mathf.RoundToInt(size * GlyphRatio));
            icon.color = KvTheme.IconTint;
            icon.raycastTarget = false;
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            float iconSize = size * GlyphRatio;
            ir.sizeDelta = new Vector2(iconSize, iconSize);
            ir.anchoredPosition = Vector2.zero;
            if (icon.sprite == null) iconGo.SetActive(false);

            KvIconButton view = go.AddComponent<KvIconButton>();
            view.Bind(btn, bg, icon, iconId);
            if (onClick != null) btn.onClick.AddListener(delegate { onClick(); });
            return view;
        }

        /// <summary>Вертикальный разделитель тулбара.</summary>
        public static Image ToolbarSeparator(RectTransform parent)
        {
            Image sep = KvTheme.CreatePanel(parent, "Sep", KvTheme.Separator);
            LayoutElement le = sep.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 1f;
            le.preferredWidth = 1f;
            return sep;
        }

        // ------------------------------------------------------------------ строка «свойство»

        /// <summary>Строка свойств: слева подпись, справа ЗНАЧЕНИЕ (только чтение).</summary>
        public static Text PropertyRow(RectTransform parent, string label, string value,
            float height = 17f, float labelWidth = 116f)
        {
            RectTransform row = CreateRow(parent, "Prop_" + label, height, 4f);
            Text l = KvTheme.CreateText(row, "Label", label, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextDim);
            Fit(l.gameObject, labelWidth, height);
            l.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text v = KvTheme.CreateText(row, "Value", value, KvTheme.FontSizeSmall,
                TextAnchor.MiddleRight, KvTheme.TextMain);
            v.horizontalOverflow = HorizontalWrapMode.Wrap;
            LayoutElement le = v.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
            return v;
        }

        // ------------------------------------------------------------------ переключатель

        /// <summary>
        /// Галка-переключатель в стиле FreeCAD (квадрат с «✓» + подпись).
        /// Кликабельна ВСЯ строка: Button живёт на корне строки, возвращаемый
        /// <see cref="KvSwitch"/> — на том же объекте (удобно навешивать колбэки).
        /// </summary>
        public static KvSwitch Switch(RectTransform parent, string label, bool initial, Action<bool> onChanged)
        {
            GameObject rowGo = new GameObject("Switch_" + label, typeof(Image), typeof(Button),
                typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);
            RectTransform row = (RectTransform)rowGo.transform;

            Image bg = rowGo.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(0f, 0f, 0f, 0.16f);
            bg.raycastTarget = true;

            HorizontalLayoutGroup h = rowGo.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            // ФИКС 6 (§22): управление размером ВКЛЮЧЕНО — теперь LayoutElement (min/preferred)
            // реально применяется, а не игнорируется в пользу дефолтного sizeDelta 100×100.
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.padding = new RectOffset(3, 3, 1, 1);

            LayoutElement rle = rowGo.GetComponent<LayoutElement>();
            rle.minHeight = SwitchRowHeight;
            rle.preferredHeight = SwitchRowHeight;
            // ФИКС 6 (§23): высота строки задаётся ЕЩЁ И ЯВНО — панель настроек выключает
            // управление высотой детей (`childControlHeight = false`), и тогда `LayoutElement`
            // не действует: строка-галка («Зона достижимости» и остальные пункты «Функций»)
            // получала дефолтные 100 px. Теперь строка ровно 20 px, а квадрат галки — 16×16.
            row.sizeDelta = new Vector2(row.sizeDelta.x, SwitchRowHeight);

            Button rowButton = rowGo.GetComponent<Button>();
            rowButton.targetGraphic = bg;
            ColorBlock cb = rowButton.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.PanelDark);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.PanelDark);
            cb.disabledColor = Color.white;
            cb.fadeDuration = 0.05f;
            rowButton.colors = cb;

            GameObject boxGo = new GameObject("Box", typeof(Image), typeof(LayoutElement));
            boxGo.transform.SetParent(rowGo.transform, false);
            Image box = boxGo.GetComponent<Image>();
            box.sprite = KvTheme.WhiteSprite;
            box.color = initial ? KvTheme.ButtonChecked : KvTheme.ButtonBg;
            box.raycastTarget = false;
            // Размер квадрата — ИЗ ЭТАЛОНА и ЯВНО (LayoutElement + sizeDelta):
            // до первого пересчёта раскладки HorizontalLayoutGroup ещё не применил
            // preferred-размер, и без sizeDelta квадрат на один кадр был бы 100×100.
            Fit(boxGo, SwitchBoxSize, SwitchBoxSize);
            RectTransform boxRect = (RectTransform)boxGo.transform;
            boxRect.sizeDelta = new Vector2(SwitchBoxSize, SwitchBoxSize);

            GameObject checkGo = new GameObject("Check", typeof(Image));
            checkGo.transform.SetParent(boxGo.transform, false);
            Image check = checkGo.GetComponent<Image>();
            check.sprite = KvIcons.Get("check", SwitchCheckIcon);
            check.color = KvTheme.IsLight ? Color.white : KvTheme.TextMain;
            check.raycastTarget = false;
            // Глиф вписан в квадрат с полем 2 px (16 − 2·2 = 12 = SwitchCheckIcon):
            // иконка берётся из ЕДИНОГО набора KvIcons, а не растягивается по квадрату.
            KvTheme.Stretch(check.rectTransform, 2f, 2f, 2f, 2f);
            checkGo.SetActive(initial);

            Text labelText = KvTheme.CreateText(row, "Label", label, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            LayoutElement le = labelText.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minHeight = SwitchBoxSize;
            le.preferredHeight = SwitchBoxSize;

            KvSwitch view = rowGo.AddComponent<KvSwitch>();
            view.Bind(box, checkGo, initial);
            rowButton.onClick.AddListener(delegate
            {
                if (view.Locked) return;
                view.Set(!view.Value);
                onChanged?.Invoke(view.Value);
            });
            return view;
        }

        // ------------------------------------------------------------------ сегментный выбор

        /// <summary>Сегментный переключатель (Тёмная | Светлая | Системная).</summary>
        public static KvSegmented Segmented(RectTransform parent, string name, string[] options,
            int initial, Action<int> onChanged, float height = 20f)
        {
            RectTransform row = CreateRow(parent, "Seg_" + name, height, 2f);
            // ФИКС 6 (§22): сегменты делят строку ПОРОВНУ, поэтому здесь управление размером
            // ВКЛЮЧЕНО. Раньше (childControlWidth = false) LayoutElement.flexibleWidth не
            // действовал, и каждая кнопка сегмента рисовалась квадратом 100×100 — это и были
            // «слишком большие квадратики» в переключателях темы/масштаба/вкладок верстака.
            HorizontalLayoutGroup segRow = row.GetComponent<HorizontalLayoutGroup>();
            if (segRow != null)
            {
                segRow.childControlWidth = true;
                segRow.childControlHeight = true;
            }
            Image bg = KvTheme.CreatePanel(row, "Bg", KvTheme.ButtonPressed);
            KvTheme.Stretch(bg.rectTransform);
            bg.transform.SetAsFirstSibling();
            // Фон растянут на всю строку и в раскладке не участвует: иначе при включённом
            // управлении размером он получил бы нулевую ширину и пропал (ФИКС 6, §22).
            LayoutElement bgLe = bg.gameObject.AddComponent<LayoutElement>();
            bgLe.ignoreLayout = true;

            KvSegmented view = row.gameObject.AddComponent<KvSegmented>();
            List<Button> buttons = new List<Button>();
            List<Image> images = new List<Image>();

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                Button b = KvTheme.CreateButton(row, "Seg" + i, options[i], null,
                    Mathf.RoundToInt(height), true);
                Fit(b.gameObject, 60f, height);
                // ФИКС 6 (§23): размер сегмента задаётся ещё и явно — на случай контейнера,
                // который размерами детей не управляет (тогда LayoutElement игнорируется).
                ((RectTransform)b.transform).sizeDelta = new Vector2(60f, height);
                LayoutElement le = b.GetComponent<LayoutElement>();
                le.flexibleWidth = 1f;
                images.Add(b.GetComponent<Image>());
                buttons.Add(b);
                b.onClick.AddListener(delegate
                {
                    view.Set(index);
                    onChanged?.Invoke(index);
                });
            }

            view.Bind(buttons, images, initial);
            return view;
        }

        /// <summary>
        /// Информационная строка (подсказка/пометка) в панели настроек.
        ///
        /// ФИКС 6 (§23): текст переносится по ширине, поэтому высота считается ПО СОДЕРЖИМОМУ
        /// (`ContentSizeFitter`), а «пол» — одна строка. Раньше подсказка без явной высоты
        /// получала дефолтные 100 px и раздувала панель: именно это давало ощущение
        /// «всё слишком большое и растянутое» в настройках и на вкладках верстака.
        /// Если высота задана вызывающим — она и применяется (ContentSizeFitter не добавляется).
        /// </summary>
        public static Text Note(RectTransform parent, string text, Color color, float height = 0f)
        {
            Text t = KvTheme.CreateText(parent, "Note", text, KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, color);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (height > 0f)
            {
                Fit(t.gameObject, 0f, height);
                return t;
            }

            float line = KvTheme.FontSizeSmall + 5f;
            Fit(t.gameObject, 0f, line);
            ContentSizeFitter fitter = t.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return t;
        }

        // ------------------------------------------------------------------ проверка эталона

        /// <summary>
        /// ПРОВЕРКА ЭТАЛОНА РАЗМЕРОВ (ФИКС 6 §22, дополнено ФИКСАМИ 6/8 §23). Возвращает
        /// отчёт «проверка — результат» и одну строку итога; ничего не меняет и ничего
        /// не требует из сцены. Запускается диагностикой/тестами, чтобы «квадратики»,
        /// «огромные иконки» и «непонятные +/−» не разъехались снова.
        /// </summary>
        public static string ValidateSizes()
        {
            StringBuilder sb = new StringBuilder();
            int failures = 0;

            failures += Check(sb, "сетка иконок KvIcons.GridSize = 24",
                KvIcons.GridSize == 24, "GridSize = " + KvIcons.GridSize);
            failures += Check(sb, "штрих иконок KvIcons.Stroke = 1.5 px",
                Mathf.Abs(KvIcons.Stroke - 1.5f) < 0.001f, "Stroke = " + KvIcons.Stroke);
            failures += Check(sb, "квадрат галки 16×16",
                Mathf.Abs(SwitchBoxSize - 16f) < 0.01f, "SwitchBoxSize = " + SwitchBoxSize);
            failures += Check(sb, "глиф галки = SwitchBoxSize · GlyphRatio",
                Mathf.Abs(SwitchBoxSize * GlyphRatio - SwitchCheckIcon) < 0.6f,
                SwitchBoxSize + " · " + GlyphRatio + " = " + (SwitchBoxSize * GlyphRatio).ToString("0.0") +
                " , иконка " + SwitchCheckIcon);
            failures += Check(sb, "квадрат галки меньше строки переключателя",
                SwitchBoxSize < SwitchRowHeight, SwitchBoxSize + " < " + SwitchRowHeight);
            failures += Check(sb, "иконка галки из единого набора KvIcons",
                KvIcons.Get("check", SwitchCheckIcon) != null, "KvIcons.Get(\"check\") != null");

            // ---- ФИКС 6/8 (§23): эталон кнопок и вкладок ----
            // P3-8 (11.11.2026). ЧТО БЫЛО. Проверка сравнивала КОНСТАНТУ `IconButtonSize` с 24,
            // хотя тулбар берёт сторону из `KvSettings.IconButtonSize` (28 при плотности 1):
            // самопроверка рапортовала «провалов 0» при реальных 28 px.
            // ЧЕМ ЭТО ГРОЗИЛО. Отчёт подтверждал неверный эталон, и расхождение тулбара с деревом
            // (24 против 28) никто не видел.
            // ТЕПЕРЬ сверяется ФАКТИЧЕСКОЕ значение с единственным источником и с допуском 24…28.
            failures += Check(sb, "кнопка-иконка = KvSettings.IconButtonSize, в диапазоне 24…28",
                Mathf.Abs(IconButtonSize - KvSettings.IconButtonSize) < 0.01f &&
                IconButtonSize >= 24f - 0.01f && IconButtonSize <= 28f + 0.01f,
                IconButtonSize + " (KvSettings = " + KvSettings.IconButtonSize +
                ", плотность " + KvSettings.Density + ")");
            failures += Check(sb, "глиф кнопки-иконки = сторона · 0,78 и не мельче 18 px",
                Mathf.Abs(GlyphRatio - 0.78f) < 0.001f && IconButtonSize * GlyphRatio >= 18f,
                IconButtonSize + " · " + GlyphRatio + " = " + (IconButtonSize * GlyphRatio).ToString("0.0"));
            failures += Check(sb, "кнопка с текстом высотой 24",
                Mathf.Abs(TextButtonHeight - 24f) < 0.01f, "TextButtonHeight = " + TextButtonHeight);
            failures += Check(sb, "отступ кнопки с текстом 6 px",
                Mathf.Abs(ButtonPadding - 6f) < 0.01f, "ButtonPadding = " + ButtonPadding);
            failures += Check(sb, "высота вкладки в диапазоне 24…28",
                TabHeight >= 24f && TabHeight <= 28f, "TabHeight = " + TabHeight);
            failures += Check(sb, "вкладка выше кнопки-иконки",
                TabHeight > IconButtonSize, TabHeight + " > " + IconButtonSize);
            failures += Check(sb, "единая ширина текстовой кнопки считается из Length · 7.6 + 16",
                Mathf.Abs(TextButtonCharWidth - 7.6f) < 0.01f && Mathf.Abs(MinTextButtonWidth - 60f) < 0.01f,
                "char = " + TextButtonCharWidth + " , минимум = " + MinTextButtonWidth);
            // ФИКС 8 (§23): кнопки «раскрыть/свернуть дерево» — иконки "expand"/"collapse"
            // («+» и «−»). Проверяем, что обе иконки есть в едином наборе (иначе кнопка
            // осталась бы пустым квадратом, а оператор снова увидел бы «непонятные кнопки»).
            failures += Check(sb, "иконки «+»/«−» дерева из единого набора KvIcons",
                KvIcons.Get("expand", 18) != null && KvIcons.Get("collapse", 18) != null,
                "KvIcons.Get(\"expand\") и Get(\"collapse\") != null");

            // P3-8 (11.11.2026). ЧТО БЫЛО. В подписи итога стояло твёрдое «Проверок: 13», хотя
            // здесь выполняется 14 проверок. ЧЕМ ЭТО ГРОЗИЛО. Отчёт самопроверки занижал
            // собственный объём: «13 проверок, провалов 0» при 14 реальных — и по этой цифре
            // судили, что эталон проверен полностью. ТЕПЕРЬ число СЧИТАЕТСЯ по самому отчёту.
            string report = sb.ToString();
            int total = 0;
            foreach (string line in report.Split('\n'))
                if (line.StartsWith("  [OK]") || line.StartsWith("  [FAIL]")) total++;

            return report + "Проверок: " + total + " · провалов: " + failures;
        }

        private static int Check(StringBuilder sb, string what, bool ok, string detail)
        {
            sb.Append(ok ? "  [OK]   " : "  [FAIL] ").Append(what).Append(" — ").Append(detail).Append('\n');
            return ok ? 0 : 1;
        }

        /// <summary>
        /// Сколько проверок эталона ПРОВАЛЕНО — короткий ответ для диагностики, тестов и
        /// автопрогона (сам отчёт целиком отдаёт <see cref="ValidateSizes"/>).
        /// ФИКС 6/8 (§23): в §22 метод был написан, но НЕ ВЫЗЫВАЛСЯ НИ ОТКУДА (§22.14 п.2) —
        /// теперь он вызывается диагностикой и при старте интерфейса, поэтому эталон
        /// подтверждается при каждом прогоне.
        /// </summary>
        public static int ValidateSizesFailures()
        {
            int failures = 0;
            string[] lines = ValidateSizes().Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].StartsWith("  [FAIL]")) failures++;
            return failures;
        }
    }

    /// <summary>Кнопка-иконка: состояние (обычная/включена/недоступна) и подсказка.</summary>
    public class KvIconButton : MonoBehaviour
    {
        public Button Button { get; private set; }
        public Image Background { get; private set; }
        public Image Icon { get; private set; }
        public string IconId { get; private set; }
        public KvTooltipTarget Tooltip { get; private set; }

        private bool enabledState = true;

        internal void Bind(Button button, Image background, Image icon, string iconId)
        {
            Button = button;
            Background = background;
            Icon = icon;
            IconId = iconId;
            Tooltip = gameObject.GetComponent<KvTooltipTarget>();
            if (Tooltip == null) Tooltip = gameObject.AddComponent<KvTooltipTarget>();
        }

        /// <summary>Сменить иконку (например, play ⟷ pause, тема).</summary>
        public void SetIcon(string iconId)
        {
            if (IconId == iconId) return;
            IconId = iconId;
            if (Icon == null) return;
            Sprite sp = KvIcons.Get(iconId, Mathf.RoundToInt(Icon.rectTransform.sizeDelta.x));
            Icon.sprite = sp;
            Icon.gameObject.SetActive(sp != null);
        }

        /// <summary>Подсветить как «включено» (тумблер) и задать оттенок иконки.</summary>
        public void SetChecked(bool value, Color? iconTint = null)
        {
            if (Background != null)
                Background.color = value ? KvTheme.ButtonChecked : KvTheme.ButtonBg;
            if (Icon != null)
                Icon.color = iconTint.HasValue ? iconTint.Value
                    : (value && !KvTheme.IsLight ? Color.white : KvTheme.IconTint);
        }

        /// <summary>Доступна ли кнопка (недоступные — «заглушки»).</summary>
        public void SetEnabled(bool value)
        {
            enabledState = value;
            if (Button != null) Button.interactable = value;
            if (Icon != null)
                Icon.color = value ? KvTheme.IconTint : KvTheme.TextDisabled;
            if (Background != null && !value) Background.color = KvTheme.PanelDark;
        }

        /// <summary>Пометка «в разработке» для подсказки.</summary>
        public void SetStub(bool stub)
        {
            if (Tooltip != null) Tooltip.stub = stub;
        }

        public bool IsEnabled { get { return enabledState; } }
    }

    /// <summary>Переключатель (галка) панели настроек.</summary>
    public class KvSwitch : MonoBehaviour
    {
        public bool Value { get; private set; }

        /// <summary>Заблокирован (нет обработчика — пункт «в разработке»).</summary>
        public bool Locked { get; set; }

        private Image box;
        private GameObject check;

        internal void Bind(Image boxImage, GameObject checkMark, bool initial)
        {
            box = boxImage;
            check = checkMark;
            Value = initial;
            Paint();
        }

        public void Set(bool value)
        {
            Value = value;
            Paint();
        }

        private void Paint()
        {
            if (box != null) box.color = Value ? KvTheme.ButtonChecked : KvTheme.ButtonBg;
            if (check != null) check.SetActive(Value);
        }
    }

    /// <summary>Сегментный переключатель (используется для темы и масштаба).</summary>
    public class KvSegmented : MonoBehaviour
    {
        public int Index { get; private set; }

        private List<Button> buttons;
        private List<Image> images;

        internal void Bind(List<Button> btns, List<Image> imgs, int initial)
        {
            buttons = btns;
            images = imgs;
            Index = initial;
            Paint();
        }

        public void Set(int index)
        {
            Index = index;
            Paint();
        }

        private void Paint()
        {
            if (images == null) return;
            for (int i = 0; i < images.Count; i++)
            {
                if (images[i] == null) continue;
                images[i].color = i == Index ? KvTheme.ButtonChecked : KvTheme.ButtonBg;
                Text t = images[i].GetComponentInChildren<Text>();
                if (t != null)
                    t.color = i == Index && !KvTheme.IsLight ? Color.white : KvTheme.TextMain;
            }
        }
    }
}
