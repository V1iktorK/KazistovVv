using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>Роль элемента «стекла» — от неё берётся цвет при смене темы.</summary>
    public enum KvGlassRole
    {
        Fill,
        Header,
        Border
    }

    /// <summary>
    /// СТИЛЬ «СТЕКЛЯННЫХ ОКОН» (утверждённый дизайн: Blender-раскладка + стекло).
    ///
    /// Стекло = полупрозрачная заливка (`KvTheme.PanelBg` с альфой) + скруглённая рамка.
    /// Скругление — процедурный 9-slice спрайт (белый скруглённый прямоугольник с альфой):
    /// без файлов ассетов, как и весь UI проекта. Рамка рисуется вторым слоем: сзади лежит
    /// скруглённый прямоугольник цвета `KvTheme.Border`, впереди — заливка с отступом 1.5 px.
    ///
    /// ТЕМА. Все созданные стёкла регистрируются в `Track`, поэтому смена темы
    /// (`KvTheme.Changed` → `ApplyTheme`) мгновенно перекрашивает их без пересборки.
    /// </summary>
    public static class KvGlassStyle
    {
        private static Sprite rounded;

        /// <summary>Скруглённый прямоугольник (9-slice, кэш).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded == null) rounded = BuildRounded();
                return rounded;
            }
        }

        /// <summary>Радиус скругления (px канваса).</summary>
        public const float CornerRadius = 6f;

        /// <summary>Альфа заливки «стекла»: тёмная тема чуть прозрачнее светлой.</summary>
        public static float FillAlpha
        {
            get { return KvTheme.IsLight ? 0.92f : 0.86f; }
        }

        public static Color FillColor
        {
            get { return WithAlpha(KvTheme.PanelBg, FillAlpha); }
        }

        public static Color HeaderColor
        {
            get { return WithAlpha(KvTheme.PanelHeader, 0.95f); }
        }

        public static Color BorderColor
        {
            get { return KvTheme.Border; }
        }

        private static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        // ------------------------------------------------------------------ реестр стёкол

        private struct Tracked
        {
            public Image Image;
            public KvGlassRole Role;
        }

        private static readonly List<Tracked> tracked = new List<Tracked>();

        /// <summary>Зарегистрировать элемент для перекраски при смене темы.</summary>
        public static void Track(Image image, KvGlassRole role)
        {
            if (image == null) return;
            tracked.Add(new Tracked { Image = image, Role = role });
        }

        /// <summary>Сколько элементов зарегистрировано (тесты: перекраска реально действует).</summary>
        public static int TrackedCount
        {
            get { return tracked.Count; }
        }

        /// <summary>Применить текущую тему ко всем зарегистрированным элементам.</summary>
        public static void ApplyTheme()
        {
            for (int i = 0; i < tracked.Count; i++)
            {
                Tracked t = tracked[i];
                if (t.Image == null) continue;
                t.Image.color = t.Role == KvGlassRole.Fill ? FillColor
                    : t.Role == KvGlassRole.Header ? HeaderColor
                    : BorderColor;
            }
        }

        /// <summary>Сбросить реестр (тесты: уничтоженные изображения не копятся).</summary>
        public static void ClearTracking()
        {
            tracked.Clear();
        }

        /// <summary>
        /// Уничтожить объект БЕЗОПАСНО и в рантайме, и в EditMode: `Destroy` в редакторе
        /// пишет ошибку (а ошибка в логе валит EditMode-тест), поэтому вне PlayMode
        /// используется немедленное уничтожение.
        /// </summary>
        public static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }

        // ------------------------------------------------------------------ фабрика

        /// <summary>
        /// Скруглённая плашка нужной роли. Для `Border` — фоновая плашка рамки; для
        /// `Fill`/`Header` — содержимое поверх рамки (создаётся вызывающим с отступом).
        /// </summary>
        public static Image CreateGlassRect(RectTransform parent, string name, KvGlassRole role)
        {
            GameObject go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = role == KvGlassRole.Fill ? FillColor
                : role == KvGlassRole.Header ? HeaderColor
                : BorderColor;
            img.raycastTarget = true;
            Track(img, role);
            return img;
        }

        /// <summary>Стеклянная рамка: рамка (Border) + заливка (Fill) с внутренним отступом.</summary>
        public static Image CreateGlassFrame(RectTransform parent, string name, out Image fill)
        {
            Image border = CreateGlassRect(parent, name, KvGlassRole.Border);
            KvTheme.Stretch(border.rectTransform);
            fill = CreateGlassRect(border.rectTransform, name + "_Fill", KvGlassRole.Fill);
            KvTheme.Stretch(fill.rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);
            return border;
        }

        // ------------------------------------------------------------------ спрайт

        private static Sprite BuildRounded()
        {
            const int size = 64;
            const float radius = 14f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - x, x - (size - 1 - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - y, y - (size - 1 - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = d <= radius - 1f ? 1f : (d >= radius + 1f ? 0f : (radius + 1f - d) / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }
    }
}
