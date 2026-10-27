using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ВСЕ ПАНЕЛИ, В КОТОРЫЕ МОЖЕТ ЦЕЛИТЬСЯ ЛУЧ (указание владельца 24.10.2026: «кнопки не выдают окна,
    /// которые можно будет крепить на поясе… если персонаж будет отходить, окна останутся висеть в
    /// воздухе»).
    ///
    /// ЗАЧЕМ РЕЕСТР. До этой волны луч умел ровно одну панель — ту, что у руки. Как только экраны
    /// становятся ОКНАМИ в мире, панелей сразу несколько: панель у руки, стартовое меню, окна
    /// «Настройки», «Объекты», «Ввод»… Указатель обязан выбирать ту, в которую он реально попал, и
    /// делать это по одному правилу, а не «если это не та панель, то вот эта».
    ///
    /// ПОЧЕМУ СТАТИКА. Панели создаются в рантайме (сцена не редактируется, §K), и окна появляются
    /// позже панели у руки. Список — единственное место, где они все видны; порядок регистрации
    /// сохраняется, поэтому при равном попадании приоритет у той панели, что создана раньше.
    /// </summary>
    public static class KvXrPanels
    {
        private static readonly List<KvXrGroupPanel> panels = new List<KvXrGroupPanel>();

        /// <summary>Сколько панелей зарегистрировано.</summary>
        public static int Count { get { return panels.Count; } }

        /// <summary>Панель по номеру (порядок регистрации).</summary>
        public static KvXrGroupPanel At(int index)
        {
            if (index < 0 || index >= panels.Count) return null;
            return panels[index];
        }

        /// <summary>Поставить панель на учёт (зовёт сама панель при появлении).</summary>
        public static void Register(KvXrGroupPanel panel)
        {
            if (panel == null || panels.Contains(panel)) return;
            panels.Add(panel);
        }

        /// <summary>Снять с учёта (зовёт сама панель при уничтожении).</summary>
        public static void Unregister(KvXrGroupPanel panel)
        {
            if (panel == null) return;
            panels.Remove(panel);
        }

        /// <summary>Забыть все панели (для тестов: сцена между тестами не сохраняется).</summary>
        public static void Clear()
        {
            panels.Clear();
        }

        /// <summary>
        /// ПАНЕЛЬ У РУКИ (та, что следует за кистью). Именно её скрывает меню и именно она — «родная»
        /// панель указателя, если луч никуда не попал.
        /// </summary>
        public static KvXrGroupPanel HandPanel
        {
            get
            {
                for (int i = 0; i < panels.Count; i++)
                {
                    KvXrGroupPanel panel = panels[i];
                    if (panel == null) continue;
                    if (panel.followHand) return panel;
                }
                return null;
            }
        }
    }
}
