using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>Строка панели свойств: либо заголовок секции, либо «подпись = значение».</summary>
    public struct KvProp
    {
        public string Label;
        public string Value;
        public Color ValueColor;
        public bool Header;

        public static KvProp Section(string title)
        {
            KvProp p = new KvProp();
            p.Label = title;
            p.Value = "";
            p.Header = true;
            p.ValueColor = Color.white;
            return p;
        }

        public static KvProp Row(string label, string value, Color color)
        {
            KvProp p = new KvProp();
            p.Label = label;
            p.Value = value;
            p.Header = false;
            p.ValueColor = color;
            return p;
        }

        /// <summary>Строка «подпись = значение» цветом темы по умолчанию (шорткат для новых модулей).</summary>
        public static KvProp Row(string label, string value)
        {
            return Row(label, value, KvTheme.TextMain);
        }
    }

    /// <summary>
    /// Правая панель — СВОЙСТВА выбранного объекта (в стиле FreeCAD: «View → Properties»).
    /// Поля ТОЛЬКО ДЛЯ ЧТЕНИЯ (ТЗ): значения обновляются из сцены/потока, но не правятся.
    ///
    /// Строки переиспользуются (пул): при обновлении 5 раз в секунду новые
    /// GameObject-ы не создаются, поэтому панель не влияет на FPS.
    /// </summary>
    public class KvPropertiesView : MonoBehaviour
    {
        private RectTransform root;
        private RectTransform content;
        private ScrollRect scroll;
        private Image bg;
        private Text titleText;
        private Image titleIcon;
        private Text kindText;
        private Image accentBar;
        private Text emptyText;
        private RectTransform skeletonGroup;
        private readonly List<Image> skeletons = new List<Image>();

        /// <summary>ЭТАП 8: показан «скелетон» (объект не выбран) — диагностика.</summary>
        public bool SkeletonVisible
        {
            get { return skeletonGroup != null && skeletonGroup.gameObject.activeSelf; }
        }

        private readonly List<GameObject> rowPool = new List<GameObject>();
        private readonly List<Text> rowLabels = new List<Text>();
        private readonly List<Text> rowValues = new List<Text>();
        private readonly List<Image> rowBars = new List<Image>();

        /// <summary>Сколько строк сейчас показано (диагностика).</summary>
        public int RowCount { get { return rowPool.Count; } }
        /// <summary>Текущий узел панели (для диагностики).</summary>
        public ProjectNode Node { get; private set; }

        public void Build(RectTransform canvas, Canvas owner)
        {
            GameObject go = new GameObject("KvPropertiesView", typeof(Image));
            go.transform.SetParent(canvas, false);
            root = (RectTransform)go.transform;
            bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelDark;
            bg.raycastTarget = true;

            // --- шапка панели: иконка + имя объекта + тип
            RectTransform head = KvWidgets.CreateRow(root, "Head", 30f, 4f);
            head.anchorMin = new Vector2(0f, 1f);
            head.anchorMax = new Vector2(1f, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.sizeDelta = new Vector2(0f, 30f);
            head.anchoredPosition = Vector2.zero;

            accentBar = KvTheme.CreatePanel(head, "Accent", KvTheme.Accent);
            KvWidgets.Fit(accentBar.gameObject, 3f, 26f);

            GameObject iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(head, false);
            titleIcon = iconGo.GetComponent<Image>();
            titleIcon.sprite = KvIcons.Get("properties", 15);
            titleIcon.color = KvTheme.TextDim;
            titleIcon.raycastTarget = false;
            KvWidgets.Fit(iconGo, 15f, 15f);

            RectTransform titles = KvWidgets.CreateColumn(head, "Titles", 26f, 0f);
            LayoutElement tle = titles.GetComponent<LayoutElement>();
            if (tle != null) tle.flexibleWidth = 1f;

            titleText = KvTheme.CreateText(titles, "Title", "Ничего не выбрано",
                KvTheme.FontSize, TextAnchor.LowerLeft, KvTheme.TextMain);
            KvWidgets.Fit(titleText.gameObject, 0f, 14f);
            kindText = KvTheme.CreateText(titles, "Kind", "выберите узел в дереве слева",
                KvTheme.FontSizeSmall - 1, TextAnchor.UpperLeft, KvTheme.TextDim);
            KvWidgets.Fit(kindText.gameObject, 0f, 12f);

            // --- прокручиваемый список свойств
            GameObject scrollGo = new GameObject("Scroll", typeof(ScrollRect), typeof(RectMask2D),
                typeof(Image));
            scrollGo.transform.SetParent(go.transform, false);
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.sprite = KvTheme.WhiteSprite;
            scrollBg.color = KvTheme.PanelDark;
            RectTransform viewport = (RectTransform)scrollGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(2f, 2f);
            viewport.offsetMax = new Vector2(-2f, -31f);

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
            vlg.padding = new RectOffset(2, 2, 2, 2);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.content = content;

            // --- ЭТАП 8: EMPTY STATE панели свойств — понятная подсказка вместо пустоты
            emptyText = KvTheme.CreateText(root, "Empty",
                KvLoc.T("properties.empty", "Выберите объект") + "\n" +
                KvLoc.T("properties.empty.hint",
                    "Клик по узлу в дереве слева (или выберите точку красным лазером — Z)"),
                KvTheme.FontSizeSmall, TextAnchor.UpperLeft, KvTheme.TextDisabled);
            emptyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            emptyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            emptyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            emptyText.rectTransform.pivot = new Vector2(0.5f, 1f);
            emptyText.rectTransform.sizeDelta = new Vector2(-12f, 40f);
            emptyText.rectTransform.anchoredPosition = new Vector2(0f, -40f);

            BuildSkeleton();
        }

        /// <summary>
        /// ЭТАП 8: «СКЕЛЕТОН» панели — серые полосы на месте будущих строк свойств.
        /// Показывается, когда объект не выбран (вместо пустой панели), и мягко пульсирует.
        /// </summary>
        private void BuildSkeleton()
        {
            GameObject group = new GameObject("Skeleton", typeof(RectTransform));
            group.transform.SetParent(root, false);
            skeletonGroup = (RectTransform)group.transform;
            skeletonGroup.anchorMin = new Vector2(0f, 1f);
            skeletonGroup.anchorMax = new Vector2(1f, 1f);
            skeletonGroup.pivot = new Vector2(0.5f, 1f);
            skeletonGroup.sizeDelta = new Vector2(-16f, 96f);
            skeletonGroup.anchoredPosition = new Vector2(0f, -86f);

            float y = 0f;
            for (int i = 0; i < 5; i++)
            {
                float width = i % 2 == 0 ? 0.42f : 0.30f;
                Image label = KvTheme.CreatePanel(skeletonGroup, "SkL" + i, SkeletonColor(0.55f));
                label.rectTransform.anchorMin = new Vector2(0f, 1f);
                label.rectTransform.anchorMax = new Vector2(0f, 1f);
                label.rectTransform.pivot = new Vector2(0f, 1f);
                label.rectTransform.sizeDelta = new Vector2(width * propertiesPanelHint, 10f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -y);

                Image value = KvTheme.CreatePanel(skeletonGroup, "SkV" + i, SkeletonColor(0.40f));
                value.rectTransform.anchorMin = new Vector2(1f, 1f);
                value.rectTransform.anchorMax = new Vector2(1f, 1f);
                value.rectTransform.pivot = new Vector2(1f, 1f);
                value.rectTransform.sizeDelta = new Vector2(0.38f * propertiesPanelHint, 10f);
                value.rectTransform.anchoredPosition = new Vector2(0f, -y);

                skeletons.Add(label);
                skeletons.Add(value);
                y += 18f;
            }
        }

        private const float propertiesPanelHint = 240f;

        private static Color SkeletonColor(float alpha)
        {
            Color baseColor = KvTheme.Separator;
            return new Color(baseColor.r, baseColor.g, baseColor.b, alpha * 0.55f);
        }

        /// <summary>
        /// ПУЛЬСАЦИЯ «СКЕЛЕТОНА» — НЕ В КАЖДОМ КАДРЕ (P2-10, 11.11.2026).
        ///
        /// ЧТО БЫЛО. Каждый кадр (72 раза в секунду) цвет каждого прямоугольника «скелетона»
        /// пересчитывался и присваивался заново. Смена цвета у `Graphic` помечает канвас
        /// грязным (`SetVerticesDirty`), поэтому ВЕСЬ канвас перестраивался 72 раза в секунду —
        /// на ПУСТОМ экране, где больше ничего не меняется.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. Перестройка канваса — самая дорогая операция интерфейса в Unity; на
        /// шлеме она съедала кадр из 13,9 мс, и именно на пустом экране (старт, ничего не
        /// выбрано), то есть тогда, когда оператор впервые смотрит на интерфейс.
        ///
        /// ТЕПЕРЬ пульсация обновляется 8 раз в секунду и только если цвет действительно
        /// изменился заметно: глаз разницы не видит, а перестроек канваса в девять раз меньше.
        /// </summary>
        private const float SkeletonUpdateIntervalSeconds = 0.125f;
        private const float SkeletonVisibleDelta = 0.02f;
        private float nextSkeletonUpdateTime;
        private float lastSkeletonAlpha = -1f;

        private void Update()
        {
            if (skeletons.Count == 0 || skeletonGroup == null) return;
            if (!skeletonGroup.gameObject.activeSelf) return;

            float now = Time.unscaledTime;
            if (now < nextSkeletonUpdateTime) return;
            nextSkeletonUpdateTime = now + SkeletonUpdateIntervalSeconds;

            // Мягкая пульсация «скелетона» — видно, что интерфейс работает, а не завис.
            float k = 0.45f + 0.25f * Mathf.Sin(now * 2.6f);
            if (lastSkeletonAlpha >= 0f && Mathf.Abs(k - lastSkeletonAlpha) < SkeletonVisibleDelta) return;
            lastSkeletonAlpha = k;

            for (int i = 0; i < skeletons.Count; i++)
            {
                if (skeletons[i] == null) continue;
                skeletons[i].color = SkeletonColor(i % 2 == 0 ? k : k * 0.75f);
            }
        }

        /// <summary>Корень панели (геометрию задаёт dock-панель).</summary>
        public RectTransform Root { get { return root; } }

        /// <summary>Сменить объект (обновляет шапку и состав строк).</summary>
        public void SetNode(ProjectNode node)
        {
            Node = node;
            if (titleText == null) return;
            if (node == null)
            {
                titleText.text = KvLoc.T("properties.none", "Ничего не выбрано");
                kindText.text = KvLoc.T("properties.none.hint", "выберите узел в дереве слева");
                titleIcon.sprite = KvIcons.Get("properties", 15);
                accentBar.color = KvTheme.TextDisabled;
                return;
            }
            titleText.text = node.DisplayName;
            kindText.text = KindLabel(node.Kind);
            titleIcon.sprite = KvIcons.Get(node.IconId, 15);
            titleIcon.gameObject.SetActive(titleIcon.sprite != null);
            accentBar.color = KindColor(node.Kind);
        }

        /// <summary>Показать набор строк (переиспользуя уже созданные).</summary>
        public void SetProperties(List<KvProp> props)
        {
            if (content == null) return;
            int count = props != null ? props.Count : 0;
            if (emptyText != null) emptyText.gameObject.SetActive(count == 0);
            if (skeletonGroup != null) skeletonGroup.gameObject.SetActive(count == 0);

            for (int i = 0; i < count; i++)
            {
                EnsureRow(i);
                KvProp p = props[i];
                if (p.Header)
                {
                    rowBars[i].gameObject.SetActive(true);
                    rowLabels[i].gameObject.SetActive(true);
                    rowValues[i].gameObject.SetActive(false);
                    rowLabels[i].text = p.Label.ToUpperInvariant();
                    rowLabels[i].color = KvTheme.TextDim;
                    rowLabels[i].fontSize = KvTheme.FontSizeSmall - 1;
                    rowBars[i].color = KvTheme.Separator;
                    rowPool[i].GetComponent<LayoutElement>().preferredHeight = 17f;
                }
                else
                {
                    rowBars[i].gameObject.SetActive(false);
                    rowLabels[i].gameObject.SetActive(true);
                    rowValues[i].gameObject.SetActive(true);
                    rowLabels[i].text = p.Label;
                    rowLabels[i].color = KvTheme.TextDim;
                    rowLabels[i].fontSize = KvTheme.FontSizeSmall;
                    rowValues[i].text = p.Value;
                    rowValues[i].color = p.ValueColor;
                    rowPool[i].GetComponent<LayoutElement>().preferredHeight = 16f;
                }
            }

            for (int i = count; i < rowPool.Count; i++)
                if (rowPool[i] != null) rowPool[i].SetActive(false);
        }

        private void EnsureRow(int index)
        {
            while (rowPool.Count <= index)
            {
                GameObject rowGo = new GameObject("Prop" + rowPool.Count, typeof(Image),
                    typeof(LayoutElement));
                rowGo.transform.SetParent(content, false);
                Image bgRow = rowGo.GetComponent<Image>();
                bgRow.sprite = KvTheme.WhiteSprite;
                bgRow.color = new Color(0f, 0f, 0f, 0f);
                bgRow.raycastTarget = false;
                LayoutElement le = rowGo.GetComponent<LayoutElement>();
                le.minHeight = 16f;
                le.preferredHeight = 16f;

                Image bar = KvTheme.CreatePanel((RectTransform)rowGo.transform, "Bar",
                    KvTheme.Separator);
                bar.rectTransform.anchorMin = new Vector2(0f, 0f);
                bar.rectTransform.anchorMax = new Vector2(1f, 0f);
                bar.rectTransform.pivot = new Vector2(0.5f, 0f);
                bar.rectTransform.sizeDelta = new Vector2(-8f, 1f);
                bar.rectTransform.anchoredPosition = Vector2.zero;

                Text label = KvTheme.CreateText((RectTransform)rowGo.transform, "Label", "",
                    KvTheme.FontSizeSmall, TextAnchor.MiddleLeft, KvTheme.TextDim);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                RectTransform lr = label.rectTransform;
                lr.anchorMin = new Vector2(0f, 0f);
                lr.anchorMax = new Vector2(0.52f, 1f);
                lr.offsetMin = new Vector2(6f, 0f);
                lr.offsetMax = Vector2.zero;

                Text value = KvTheme.CreateText((RectTransform)rowGo.transform, "Value", "",
                    KvTheme.FontSizeSmall, TextAnchor.MiddleRight, KvTheme.TextMain);
                value.horizontalOverflow = HorizontalWrapMode.Wrap;
                RectTransform vr = value.rectTransform;
                vr.anchorMin = new Vector2(0.52f, 0f);
                vr.anchorMax = new Vector2(1f, 1f);
                vr.offsetMin = Vector2.zero;
                vr.offsetMax = new Vector2(-6f, 0f);

                rowPool.Add(rowGo);
                rowLabels.Add(label);
                rowValues.Add(value);
                rowBars.Add(bar);
            }
            if (rowPool[index] != null) rowPool[index].SetActive(true);
        }

        private static string KindLabel(ProjectNodeKind kind)
        {
            switch (kind)
            {
                case ProjectNodeKind.Robot: return "робот";
                case ProjectNodeKind.Axis: return "ось/сустав";
                case ProjectNodeKind.Tcp: return "концевая точка (TCP)";
                case ProjectNodeKind.Table: return "стол/стенд";
                case ProjectNodeKind.Point: return "выбранная точка";
                case ProjectNodeKind.Trajectory: return "траектория";
                case ProjectNodeKind.Phantom: return "фантом";
                case ProjectNodeKind.Group: return "группа";
                default: return "объект";
            }
        }

        private static Color KindColor(ProjectNodeKind kind)
        {
            switch (kind)
            {
                case ProjectNodeKind.Robot: return KvTheme.Accent;
                case ProjectNodeKind.Point: return KvTheme.LaserRed;
                case ProjectNodeKind.Trajectory: return KvTheme.Warn;
                case ProjectNodeKind.Phantom: return KvTheme.Ok;
                default: return KvTheme.TextDim;
            }
        }

        /// <summary>Перекрасить панель под текущую тему.</summary>
        public void Repaint()
        {
            if (bg != null) bg.color = KvTheme.PanelDark;
            if (titleText != null) titleText.color = KvTheme.TextMain;
            if (kindText != null) kindText.color = KvTheme.TextDim;
            if (titleIcon != null) titleIcon.color = KvTheme.TextDim;
            if (emptyText != null) emptyText.color = KvTheme.TextDisabled;
        }
    }
}
