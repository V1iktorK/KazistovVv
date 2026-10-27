using UnityEngine;
using UnityEngine.EventSystems;

namespace KazistovVvXr
{
    /// <summary>
    /// НАВЕДЕНИЕ НА ИКОНКУ ПАНЕЛИ (сессия §35, карточка Б19).
    ///
    /// ЗАЧЕМ. Владелец требует: «Текст появляется только после наведения и удержания фокуса
    /// 0,5 секунды». Чтобы отсчитать эти полсекунды, панель должна знать, когда луч навёл её
    /// иконку, а когда ушёл с неё.
    ///
    /// ПОЧЕМУ ИМЕННО ТАК, А НЕ «СВОИМ ЛУЧОМ». Указатель проекта (`KvXrUiPointer`) уже
    /// подсвечивает цель через `ExecuteEvents.ExecuteHierarchy(..., pointerEnterHandler)`
    /// и `pointerExitHandler`. Значит иконке достаточно реализовать эти два обработчика — и
    /// наведение придёт ТЕМ ЖЕ путём, что и нажатие. Второго способа ввода не появляется,
    /// и разойтись им негде: если указатель перенастроят, наведение и нажатие сместятся вместе.
    ///
    /// ЭТО КАСАЕТСЯ ТОЛЬКО УРОВНЯ ГРУПП. В веере подписей нет вовсе (решение владельца), поэтому
    /// там наведение ни на что не влияет — панель сама решает это через
    /// `KvXrGroupPanelLayout.ShowsLabel`.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private KvXrGroupPanel panel;

        /// <summary>Название иконки — то, что показывается по удержанию.</summary>
        public string Title { get; private set; }

        /// <summary>Наведена ли иконка прямо сейчас (для журнала и проверок).</summary>
        public bool Hovered { get; private set; }

        /// <summary>Привязать иконку к панели и запомнить её название.</summary>
        public void Bind(KvXrGroupPanel owner, string title)
        {
            panel = owner;
            Title = title;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered = true;
            if (panel != null) panel.NotifyHover(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hovered = false;
            if (panel != null) panel.NotifyHover(this, false);
        }
    }
}
