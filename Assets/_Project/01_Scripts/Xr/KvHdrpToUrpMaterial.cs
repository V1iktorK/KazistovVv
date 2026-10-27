using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ПЕРЕВОД МАТЕРИАЛОВ АНГАРА С HDRP НА URP (сессия §35, карточка Б17).
    ///
    /// ЗАЧЕМ. Материалы ангара в проекте — `HDRP/Lit` (проверено: шейдер
    /// `…render-pipelines.high-definition…/Runtime/Material/Lit/Lit.shader`). На компьютере это
    /// правильно: уровень качества `Standalone = 1` — это HDRP. Но для шлема собирается уровень
    /// качества `Mobile` (в `QualitySettings.asset` записано `Android: 0`), а он на
    /// `Assets\Settings\Mobile_RPAsset.asset` — это **URP**. HDRP-материал под URP даёт
    /// РОЗОВУЮ поверхность, а приёмка владельца требует «ни одной розовой поверхности».
    ///
    /// ПОЧЕМУ НЕЛЬЗЯ ПРОСТО ПОПРАВИТЬ МАТЕРИАЛЫ АНГАРА. Общие `.mat` в
    /// `Assets\ROOMS\HQ Hangar Free\Materials\` используются десктопной сценой, которая работает
    /// на HDRP. Их правка сломала бы десктоп — прямой запрет [S3.2]. Поэтому для шлема создаются
    /// КОПИИ, и этот тип описывает, что именно в них перекладывается.
    ///
    /// ЧТО ЗДЕСЬ ПРОВЕРЯЕМО. Соответствие КАНАЛОВ карты маски — чистая арифметика над цветом,
    /// и она вынесена сюда, чтобы её можно было проверить тестом без текстур и без сцены.
    /// У HDRP и URP каналы упакованы ПО-РАЗНОМУ, и перепутать их — значит получить «металл,
    /// который выглядит как пластик», то есть тихую потерю вида без единой ошибки в журнале.
    ///
    /// УПАКОВКА HDRP (`_MaskMap`):   R — металличность, G — затенение (AO), B — маска деталей, A — гладкость.
    /// УПАКОВКА URP  (`_MetallicGlossMap`): R — металличность, A — гладкость.
    /// УПАКОВКА URP  (`_OcclusionMap`):     G — затенение.
    /// </summary>
    public static class KvHdrpToUrpMaterial
    {
        // ==================================================== ИМЕНА СВОЙСТВ

        /// <summary>Основная текстура: у HDRP — `_BaseColorMap`, у URP — `_BaseMap`.</summary>
        public const string HdrpBaseColorMap = "_BaseColorMap";
        public const string UrpBaseMap = "_BaseMap";

        /// <summary>Цвет: имя одинаковое, но перекладывать всё равно надо явно.</summary>
        public const string BaseColor = "_BaseColor";

        /// <summary>Нормали: у HDRP — `_NormalMap`, у URP — `_BumpMap`.</summary>
        public const string HdrpNormalMap = "_NormalMap";
        public const string UrpBumpMap = "_BumpMap";

        /// <summary>Карта маски HDRP и её адреса в URP.</summary>
        public const string HdrpMaskMap = "_MaskMap";
        public const string UrpMetallicGlossMap = "_MetallicGlossMap";
        public const string UrpOcclusionMap = "_OcclusionMap";

        /// <summary>Скалярные значения — на случай материала без карты.</summary>
        public const string Metallic = "_Metallic";
        public const string Smoothness = "_Smoothness";

        /// <summary>Имя URP-шейдера, которым заменяется `HDRP/Lit`.</summary>
        public const string UrpLitShader = "Universal Render Pipeline/Lit";

        // ==================================================== КАНАЛЫ

        /// <summary>Канал металличности в карте маски HDRP.</summary>
        public const int HdrpMetallicChannel = 0;      // R

        /// <summary>Канал затенения (AO) в карте маски HDRP.</summary>
        public const int HdrpOcclusionChannel = 1;     // G

        /// <summary>Канал маски деталей в карте маски HDRP (в URP аналога нет).</summary>
        public const int HdrpDetailChannel = 2;        // B

        /// <summary>Канал гладкости в карте маски HDRP.</summary>
        public const int HdrpSmoothnessChannel = 3;    // A

        /// <summary>
        /// КАРТА «МЕТАЛЛИЧНОСТЬ + ГЛАДКОСТЬ» ДЛЯ URP. Металличность едет из красного канала,
        /// гладкость — из альфы. Синий и зелёный не используются и обнуляются, чтобы не тащить
        /// в URP смысл, которого он не знает.
        /// </summary>
        public static Color ToUrpMetallicGloss(Color hdrpMask)
        {
            return new Color(hdrpMask.r, 0f, 0f, hdrpMask.a);
        }

        /// <summary>
        /// КАРТА ЗАТЕНЕНИЯ ДЛЯ URP. Затенение у URP живёт в ЗЕЛЁНОМ канале: положить его
        /// в красный — распространённая ошибка, при которой затенение молча пропадает.
        /// </summary>
        public static Color ToUrpOcclusion(Color hdrpMask)
        {
            return new Color(1f, hdrpMask.g, 1f, 1f);
        }

        /// <summary>Металличность из карты маски HDRP (для скалярного запасного значения).</summary>
        public static float MetallicOf(Color hdrpMask)
        {
            return Mathf.Clamp01(hdrpMask.r);
        }

        /// <summary>Гладкость из карты маски HDRP (для скалярного запасного значения).</summary>
        public static float SmoothnessOf(Color hdrpMask)
        {
            return Mathf.Clamp01(hdrpMask.a);
        }

        /// <summary>
        /// ГОДИТСЯ ЛИ КАРТА МАСКИ. Полностью однотонная карта (все пиксели одинаковы) не несёт
        /// сведений: у неё металличность и гладкость надо брать скалярами, а не текстурой.
        /// Проверка нужна, чтобы не создать пустую текстуру и не потерять вид молча.
        /// </summary>
        public static bool HasUsableMask(Color hdrpMask)
        {
            return Mathf.Abs(hdrpMask.r - hdrpMask.g) > 0.001f
                || Mathf.Abs(hdrpMask.g - hdrpMask.b) > 0.001f
                || Mathf.Abs(hdrpMask.b - hdrpMask.a) > 0.001f;
        }

        /// <summary>Строка журнала: чем заменён материал и что перенесено (числа — инвариантно).</summary>
        public static string Describe(string materialName, bool hasBaseMap, bool hasNormalMap,
                                      bool hasMaskMap, float metallic, float smoothness)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} → {1} · цвет {2} · нормали {3} · маска {4} · металличность {5:0.00} · гладкость {6:0.00}",
                materialName, UrpLitShader,
                hasBaseMap ? "перенесён" : "нет",
                hasNormalMap ? "перенесены" : "нет",
                hasMaskMap ? "переложена (R→R, A→A, G→G затенение)" : "нет (скаляры)",
                metallic, smoothness);
        }
    }
}
