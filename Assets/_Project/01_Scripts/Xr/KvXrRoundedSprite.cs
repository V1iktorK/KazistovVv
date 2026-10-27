using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// СКРУГЛЁННЫЙ ПРЯМОУГОЛЬНИК ДЛЯ ПЛИТ И СТРОК МЕНЮ (указание владельца 24.10.2026: «углы надо
    /// скруглить»).
    ///
    /// ПОЧЕМУ КАРТИНКА СОЗДАЁТСЯ В КОДЕ, А НЕ ВЗЯТА ГОТОВАЯ. Правило волны: новых ассетов без
    /// разрешения владельца не добавлять ([S3]), а скругление — это вопрос формы, а не картинки.
    /// Поэтому текстура 64 × 64 с прозрачными углами делается один раз в памяти и живёт столько же,
    /// сколько приложение: ни файлов, ни импорта, ни новых метаданных.
    ///
    /// ПОЧЕМУ «ДЕВЯТЬ ЧАСТЕЙ» (sliced). Плита меню — 0,90 × 0,60 м, строка — 0,57 × 0,052 м. Если
    /// растянуть одну и ту же картинку, на широкой плите уголки вытянулись бы в овалы. С границами
    /// 16 px углы остаются углами при любом размере: растягивается только середина.
    /// </summary>
    public static class KvXrRoundedSprite
    {
        /// <summary>Сторона текстуры, пиксели.</summary>
        public const int SizePixels = 64;

        /// <summary>Радиус скругления, пиксели (он же border спрайта).</summary>
        public const int RadiusPixels = 16;

        private static Sprite cached;

        /// <summary>Скруглённый спрайт (создаётся один раз).</summary>
        public static Sprite Get()
        {
            if (cached != null) return cached;

            var texture = new Texture2D(SizePixels, SizePixels, TextureFormat.RGBA32, false)
            {
                name = "KvXrRoundedRect",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float radius = RadiusPixels;

            for (int y = 0; y < SizePixels; y++)
            {
                for (int x = 0; x < SizePixels; x++)
                {
                    // Расстояние до ближайшего «углового» центра: внутри средних полос — 0.
                    float dx = Mathf.Max(0f, radius - x, x - (SizePixels - 1 - radius));
                    float dy = Mathf.Max(0f, radius - y, y - (SizePixels - 1 - radius));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Мягкий край в один пиксель — иначе на скруглении видны ступеньки.
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);

            cached = Sprite.Create(
                texture,
                new Rect(0f, 0f, SizePixels, SizePixels),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(RadiusPixels, RadiusPixels, RadiusPixels, RadiusPixels));

            cached.name = "KvXrRoundedRectSprite";
            cached.hideFlags = HideFlags.HideAndDontSave;
            return cached;
        }
    }
}
