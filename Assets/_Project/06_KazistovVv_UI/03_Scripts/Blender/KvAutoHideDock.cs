using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>Край экрана, у которого живёт док.</summary>
    public enum KvDockEdge
    {
        Left = 0,
        Right = 1,
        Bottom = 2
    }

    /// <summary>
    /// БОКОВОЙ ДОК С АВТОСКРЫТИЕМ (утверждённый дизайн: Blender-раскладка).
    ///
    /// Как в Blender: у края экрана живёт ТОНКАЯ ПОЛОСА; док выезжает при наведении на край
    /// и уезжает, когда курсор уходит. Проверка «курсор в полосе/доке» — через
    /// `RectTransformUtility.RectangleContainsScreenPoint` (та же геометрия, что видит клик),
    /// а не по расстоянию до края: полоса и док имеют реальные прямоугольники.
    ///
    /// ТЕСТИРУЕМОСТЬ. `EvaluatePointer` — публичная логика наведения: её дёргает и `Update`
    /// с `Input.mousePosition`, и тест с произвольной точкой. Закрытие по уходу курсора
    /// мгновенное (без таймера): поведение детерминированное, «уехал — закрылся».
    /// </summary>
    public class KvAutoHideDock : MonoBehaviour
    {
        /// <summary>Ширина полосы автоскрытия (px).</summary>
        public const float StripSize = 8f;

        public KvDockEdge Edge { get; private set; }

        /// <summary>Док выдвинут.</summary>
        public bool IsOpen { get; private set; }

        public RectTransform Root { get; private set; }
        /// <summary>Тонкая полоса у края (всегда видна).</summary>
        public RectTransform Strip { get; private set; }
        /// <summary>Область с окнами панелей (видна, когда док открыт).</summary>
        public RectTransform Panel { get; private set; }

        private readonly List<KvGlassWindow> windows = new List<KvGlassWindow>();
        public IReadOnlyList<KvGlassWindow> Windows
        {
            get { return windows; }
        }

        /// <summary>Док открылся/закрылся (для панелей и диагностики).</summary>
        public event Action<bool> OpenChanged;

        /// <summary>Собрать док у заданного края рабочего района.</summary>
        public void Build(RectTransform workArea, KvDockEdge edge, float panelSize)
        {
            Edge = edge;

            GameObject rootGo = new GameObject("Dock_" + edge, typeof(RectTransform));
            rootGo.transform.SetParent(workArea, false);
            Root = (RectTransform)rootGo.transform;
            AnchorToEdge(Root, edge, StripSize);

            // --- полоса автоскрытия (тонкая, всегда видна)
            Image stripImg = KvGlassStyle.CreateGlassRect(Root, "Strip", KvGlassRole.Border);
            Strip = stripImg.rectTransform;
            AnchorToEdge(Strip, edge, StripSize);
            stripImg.raycastTarget = true;
            EventTrigger trigger = stripImg.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry enter = new EventTrigger.Entry();
            enter.eventID = EventTriggerType.PointerEnter;
            enter.callback.AddListener(delegate { Open(); });
            trigger.triggers.Add(enter);

            // --- выезжающая область с окнами
            Image panelImg = KvGlassStyle.CreateGlassRect(Root, "Panel", KvGlassRole.Fill);
            Panel = panelImg.rectTransform;
            AnchorToEdge(Panel, edge, panelSize);
            panelImg.raycastTarget = true;

            IsOpen = false;
            Panel.gameObject.SetActive(false);
        }

        /// <summary>Добавить стеклянное окно в док (выстраивается столбиком от края).</summary>
        public void AddWindow(KvGlassWindow window)
        {
            if (window == null || Panel == null) return;
            windows.Add(window);
            window.Root.SetParent(Panel, false);
            StackWindows();
        }

        /// <summary>Переставить окна столбиком (после добавления/закрытия).</summary>
        public void StackWindows()
        {
            float offset = 0f;
            for (int i = 0; i < windows.Count; i++)
            {
                KvGlassWindow w = windows[i];
                if (w == null || !w.Visible) continue;
                RectTransform rt = w.Root;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.offsetMin = new Vector2(2f, 0f);
                rt.offsetMax = new Vector2(-2f, 0f);
                rt.anchoredPosition = new Vector2(2f, -offset);
                offset += rt.sizeDelta.y + 4f;
            }
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            if (Panel != null) Panel.gameObject.SetActive(true);
            StackWindows();
            if (OpenChanged != null) OpenChanged(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (Panel != null) Panel.gameObject.SetActive(false);
            if (OpenChanged != null) OpenChanged(false);
        }

        /// <summary>Тумблер (команды/клавиши).</summary>
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>
        /// НАВЕДЕНИЕ КУРСОРА: курсор в полосе или в открытом доке → выдвинуть; вне их → убрать.
        /// Та же точка входа у `Update` (живой курсор) и у тестов (произвольная точка).
        /// </summary>
        public void EvaluatePointer(Vector2 screenPoint)
        {
            bool over = PointerOverDock(screenPoint);
            if (over) Open();
            else Close();
        }

        /// <summary>Курсор над полосой или над выдвинутым доком?</summary>
        public bool PointerOverDock(Vector2 screenPoint)
        {
            if (Strip != null && Strip.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(Strip, screenPoint, null))
                return true;
            if (IsOpen && Panel != null && Panel.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(Panel, screenPoint, null))
                return true;
            return false;
        }

        private void Update()
        {
            EvaluatePointer(Input.mousePosition);
        }

        private static void AnchorToEdge(RectTransform rt, KvDockEdge edge, float size)
        {
            switch (edge)
            {
                case KvDockEdge.Left:
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(size, 0f);
                    rt.anchoredPosition = Vector2.zero;
                    break;
                case KvDockEdge.Right:
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.sizeDelta = new Vector2(size, 0f);
                    rt.anchoredPosition = Vector2.zero;
                    break;
                default:
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.sizeDelta = new Vector2(0f, size);
                    rt.anchoredPosition = Vector2.zero;
                    break;
            }
        }
    }
}
