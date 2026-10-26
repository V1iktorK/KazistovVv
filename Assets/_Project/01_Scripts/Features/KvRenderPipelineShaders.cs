using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KazistovVvFeatures
{
    /// <summary>Какой конвейер рендера сейчас в силе.</summary>
    public enum KvPipelineKind
    {
        /// <summary>Встроенный конвейер Unity (ни URP, ни HDRP).</summary>
        BuiltIn = 0,

        /// <summary>Universal Render Pipeline — конвейер шлема (решение [D9]).</summary>
        Urp = 1,

        /// <summary>High Definition Render Pipeline — конвейер компьютера (решение [D9]).</summary>
        Hdrp = 2,

        /// <summary>Конвейер не опознан (свой SRP) — судить о совместимости шейдеров нельзя.</summary>
        Unknown = 3
    }

    /// <summary>
    /// ОДИН ИСТОЧНИК ПРАВДЫ ПО ШЕЙДЕРАМ И МАТЕРИАЛАМ (сессия §29, карточка Б1).
    ///
    /// КАКУЮ ЗАДАЧУ РЕШАЕТ. В проекте ДВА конвейера сразу — это решение [D9]: шлем (Android)
    /// собирается на URP, компьютер остаётся на HDRP. Значит материал, созданный кодом, обязан
    /// совпадать с ТЕКУЩИМ конвейером: `Shader.Find("HDRP/Lit")` под URP даёт РОЗОВУЮ
    /// поверхность, а `Shader.Find("Universal Render Pipeline/Lit")` под HDRP — тоже.
    /// До этой сессии в 12 файлах проекта стояли прямые обращения к HDRP-шейдерам, часть —
    /// вовсе без запасного варианта: при сборке под шлем это «розовая» сцена.
    ///
    /// ПОЧЕМУ ИМЕНА ШЕЙДЕРОВ СТРОКАМИ, А НЕ ТИПАМИ ПАКЕТОВ. Если бы этот файл ссылался на
    /// `UniversalRenderPipelineAsset` или `HDRenderPipelineAsset` напрямую, он перестал бы
    /// компилироваться при временном отсутствии одного из пакетов — а проект обязан собираться
    /// и тогда, когда второй конвейер выключен. Плюс строки — это ЧИСТАЯ логика, которую можно
    /// проверить тестами без графики и без ассетов (что и сделано в `KvRenderPipelineTests`).
    ///
    /// ПОЧЕМУ ЦЕПОЧКИ ЗАПАСНЫХ ВАРИАНТОВ НЕ ПЕРЕСЕКАЮТ КОНВЕЙЕРЫ. Заманчиво поставить в запас
    /// «любой Lit» — но запас вида «URP → HDRP/Lit» вернул бы ровно ту же розовую поверхность,
    /// только другим путём. Поэтому цепочка URP заканчивается UNLIT-шейдером URP или встроенным
    /// `Unlit/Color`, который рисуется в любом конвейере, но никогда — шейдером чужого конвейера.
    /// </summary>
    public static class KvRenderPipelineShaders
    {
        // ---------------------------------------------------------------- имена шейдеров

        public const string UrpLitName = "Universal Render Pipeline/Lit";
        public const string UrpSimpleLitName = "Universal Render Pipeline/SimpleLit";
        public const string UrpUnlitName = "Universal Render Pipeline/Unlit";
        public const string HdrpLitName = "HDRP/Lit";
        public const string HdrpUnlitName = "HDRP/Unlit";
        public const string BuiltInLitName = "Standard";
        public const string BuiltInUnlitName = "Unlit/Color";
        public const string SpriteDefaultName = "Sprites/Default";

        /// <summary>Префикс шейдеров URP (в том числе Shader Graph, собранных под URP).</summary>
        public const string UrpPrefix = "Universal Render Pipeline/";

        /// <summary>Префикс шейдеров HDRP — он же признак «под URP это розовое».</summary>
        public const string HdrpPrefix = "HDRP/";

        // ---------------------------------------------------------------- свойства цвета

        /// <summary>
        /// Имена свойств цвета у «светящихся» (Lit) шейдеров: у URP/Lit и HDRP/Lit это
        /// `_BaseColor`, у встроенного Standard — `_Color`. Порядок = порядок проверки.
        /// </summary>
        public static readonly string[] LitColorCandidates = { "_BaseColor", "_Color" };

        /// <summary>
        /// Имена свойств цвета у Unlit-шейдеров: у HDRP/Unlit — `_UnlitColor`,
        /// у URP/Unlit — `_BaseColor`, у встроенных — `_Color`.
        /// </summary>
        public static readonly string[] UnlitColorCandidates = { "_UnlitColor", "_BaseColor", "_Color" };

        /// <summary>
        /// Имена свойств ОСНОВНОЙ ТЕКСТУРЫ. Различаются так же, как цвета: у HDRP/Lit это
        /// `_BaseColorMap`, у URP/Lit — `_BaseMap`, у встроенного Standard — `_MainTex`.
        /// Без этого списка текстура, привязанная кодом, под URP просто не появилась бы:
        /// запись в несуществующее свойство не падает, а молча ничего не делает.
        /// </summary>
        public static readonly string[] LitTextureCandidates = { "_BaseColorMap", "_BaseMap", "_MainTex" };

        /// <summary>Имена свойств основной текстуры у Unlit-шейдеров.</summary>
        public static readonly string[] UnlitTextureCandidates = { "_UnlitColorMap", "_BaseMap", "_MainTex" };

        // ---------------------------------------------------------------- определение конвейера

        /// <summary>Опознать конвейер по имени типа ассета (чистая функция, без ассетов).</summary>
        public static KvPipelineKind KindOfTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return KvPipelineKind.BuiltIn;

            if (typeName.IndexOf("UniversalRenderPipelineAsset", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return KvPipelineKind.Urp;
            if (typeName.IndexOf("HDRenderPipelineAsset", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return KvPipelineKind.Hdrp;

            return KvPipelineKind.Unknown;
        }

        /// <summary>Опознать конвейер по ассету (null — встроенный конвейер).</summary>
        public static KvPipelineKind KindOf(RenderPipelineAsset asset)
        {
            if (asset == null) return KvPipelineKind.BuiltIn;
            return KindOfTypeName(asset.GetType().Name);
        }

        /// <summary>
        /// КОНВЕЙЕР, КОТОРЫЙ СЕЙЧАС В СИЛЕ. `GraphicsSettings.currentRenderPipeline` — это
        /// конвейер текущего уровня качества, а если у уровня своего конвейера нет — глобальное
        /// умолчание проекта. Именно он определяет, какой шейдер обязан взять код.
        /// </summary>
        public static KvPipelineKind ActiveKind()
        {
            return KindOf(GraphicsSettings.currentRenderPipeline);
        }

        // ---------------------------------------------------------------- цепочки запасных вариантов

        /// <summary>Цепочка Lit-шейдеров для конвейера: ПЕРВЫЙ в списке — основной.</summary>
        public static string[] LitChain(KvPipelineKind kind)
        {
            switch (kind)
            {
                case KvPipelineKind.Urp:
                    // Запас — SimpleLit и Unlit того же конвейера: лучше плоская тень,
                    // чем розовая поверхность. Чужой конвейер в запас НЕ ставится.
                    return new[] { UrpLitName, UrpSimpleLitName, UrpUnlitName, BuiltInUnlitName };
                case KvPipelineKind.Hdrp:
                    return new[] { HdrpLitName, HdrpUnlitName, BuiltInUnlitName };
                case KvPipelineKind.BuiltIn:
                    return new[] { BuiltInLitName, BuiltInUnlitName };
                default:
                    // Конвейер не опознан: пробуем по порядку «свой для проекта» и только потом чужой.
                    return new[] { UrpLitName, HdrpLitName, BuiltInLitName, BuiltInUnlitName };
            }
        }

        /// <summary>Цепочка Unlit-шейдеров для конвейера: ПЕРВЫЙ в списке — основной.</summary>
        public static string[] UnlitChain(KvPipelineKind kind)
        {
            switch (kind)
            {
                case KvPipelineKind.Urp:
                    return new[] { UrpUnlitName, BuiltInUnlitName, SpriteDefaultName };
                case KvPipelineKind.Hdrp:
                    return new[] { HdrpUnlitName, BuiltInUnlitName, SpriteDefaultName };
                case KvPipelineKind.BuiltIn:
                    return new[] { BuiltInUnlitName, SpriteDefaultName };
                default:
                    return new[] { UrpUnlitName, HdrpUnlitName, BuiltInUnlitName, SpriteDefaultName };
            }
        }

        /// <summary>Имя ОСНОВНОГО Lit-шейдера для конвейера.</summary>
        public static string LitShaderName(KvPipelineKind kind) { return LitChain(kind)[0]; }

        /// <summary>Имя ОСНОВНОГО Unlit-шейдера для конвейера.</summary>
        public static string UnlitShaderName(KvPipelineKind kind) { return UnlitChain(kind)[0]; }

        // ---------------------------------------------------------------- поиск шейдеров

        private static Shader FindFirst(string[] chain)
        {
            if (chain == null) return null;
            for (int i = 0; i < chain.Length; i++)
            {
                Shader shader = Shader.Find(chain[i]);
                if (shader != null) return shader;
            }
            return null;
        }

        /// <summary>Lit-шейдер текущего конвейера (с запасными вариантами того же конвейера).</summary>
        public static Shader FindLit() { return FindFirst(LitChain(ActiveKind())); }

        /// <summary>Lit-шейдер заданного конвейера.</summary>
        public static Shader FindLit(KvPipelineKind kind) { return FindFirst(LitChain(kind)); }

        /// <summary>Unlit-шейдер текущего конвейера (для линий, лучей, меток).</summary>
        public static Shader FindUnlit() { return FindFirst(UnlitChain(ActiveKind())); }

        /// <summary>Unlit-шейдер заданного конвейера.</summary>
        public static Shader FindUnlit(KvPipelineKind kind) { return FindFirst(UnlitChain(kind)); }

        // ---------------------------------------------------------------- создание материалов

        /// <summary>Создать Lit-материал под текущий конвейер. null — если шейдера нет вовсе.</summary>
        public static Material CreateLit(string name, Color color)
        {
            return CreateLit(ActiveKind(), name, color);
        }

        /// <summary>Создать Lit-материал под заданный конвейер.</summary>
        public static Material CreateLit(KvPipelineKind kind, string name, Color color)
        {
            return Create(FindLit(kind), name, color, false);
        }

        /// <summary>Создать Unlit-материал под текущий конвейер.</summary>
        public static Material CreateUnlit(string name, Color color)
        {
            return CreateUnlit(ActiveKind(), name, color);
        }

        /// <summary>Создать Unlit-материал под заданный конвейер.</summary>
        public static Material CreateUnlit(KvPipelineKind kind, string name, Color color)
        {
            return Create(FindUnlit(kind), name, color, true);
        }

        private static Material Create(Shader shader, string name, Color color, bool unlit)
        {
            if (shader == null) return null;
            var material = new Material(shader);
            if (!string.IsNullOrEmpty(name)) material.name = name;
            ApplyColor(material, color, unlit);
            return material;
        }

        /// <summary>
        /// ЗАПИСАТЬ ЦВЕТ В МАТЕРИАЛ, НЕ ЗНАЯ ЗАРАНЕЕ ИМЕНИ СВОЙСТВА. Имя свойства зависит от
        /// шейдера (`_BaseColor` / `_UnlitColor` / `_Color`), и угадывать его нельзя: запись
        /// в несуществующее свойство молча ничего не делает, а поверхность остаётся серой.
        /// Поэтому кандидаты перебираются и берётся первый, который у материала ЕСТЬ.
        /// </summary>
        public static bool ApplyColor(Material material, Color color, bool unlit)
        {
            if (material == null) return false;
            string[] candidates = unlit ? UnlitColorCandidates : LitColorCandidates;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!material.HasProperty(candidates[i])) continue;
                material.SetColor(candidates[i], color);
                return true;
            }
            return false;
        }

        /// <summary>
        /// ПОЛОЖИТЬ ТЕКСТУРУ В МАТЕРИАЛ, НЕ ЗНАЯ ИМЕНИ СВОЙСТВА (та же логика, что у цвета):
        /// перебираются кандидаты, берётся первый, который у материала есть.
        /// </summary>
        public static bool ApplyTexture(Material material, Texture texture, bool unlit)
        {
            if (material == null || texture == null) return false;
            string[] candidates = unlit ? UnlitTextureCandidates : LitTextureCandidates;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!material.HasProperty(candidates[i])) continue;
                material.SetTexture(candidates[i], texture);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Имя свойства цвета ПО ИМЕНИ ШЕЙДЕРА (без создания материала). Нужно отчётам и тестам;
        /// для записи цвета пользуйтесь <see cref="ApplyColor"/> — она проверяет факт наличия.
        /// </summary>
        public static string ColorPropertyName(string shaderName, bool unlit)
        {
            if (string.IsNullOrEmpty(shaderName)) return null;

            if (unlit)
            {
                if (shaderName == HdrpUnlitName) return "_UnlitColor";
                if (shaderName == UrpUnlitName) return "_BaseColor";
                if (shaderName == BuiltInUnlitName || shaderName == SpriteDefaultName) return "_Color";
                return "_UnlitColor";
            }

            if (shaderName == BuiltInLitName) return "_Color";
            return "_BaseColor";
        }

        // ---------------------------------------------------------------- линии, лучи, метки

        /// <summary>
        /// ЦЕПОЧКА ШЕЙДЕРОВ ДЛЯ ЛИНИЙ, ЛУЧЕЙ, ТРУБ И МЕТОК (LineRenderer и тонкие меши).
        ///
        /// ПОЧЕМУ ПЕРВЫМ ИДЁТ ВСТРОЕННЫЙ `Sprites/Default`. Он рисуется в ЛЮБОМ конвейере
        /// (см. <see cref="IsKnownUnlitBuiltIn"/>), поэтому картинка на компьютере и в шлеме
        /// остаётся одинаковой. Ставить вперёд URP/HDRP нельзя: это изменило бы вид линий
        /// на рабочем столе, а он владельцем принят (решение [D9] — компьютер не трогаем).
        ///
        /// ПОЧЕМУ ЕСТЬ ЗАПАСНЫЕ ВАРИАНТЫ. `Shader.Find` во время работы находит только те шейдеры,
        /// что вошли в сборку. Встроенные шейдеры вырезаются стриппингом, если их не использует
        /// ни один материал сцены, — тогда линия остаётся БЕЗ материала и её не видно,
        /// а `new Material(null)` вообще бросает ArgumentNullException: ровно это и произошло
        /// на устройстве 08.10.2026 в `KvSelectionHighlight.Build`. Поэтому запас — всегда
        /// встроенный Unlit или шейдер ТОГО ЖЕ конвейера, но никогда шейдер чужого.
        /// </summary>
        public static string[] LineChain(KvPipelineKind kind)
        {
            switch (kind)
            {
                case KvPipelineKind.Urp:
                    return new[] { SpriteDefaultName, BuiltInUnlitName, UrpUnlitName };
                case KvPipelineKind.Hdrp:
                    return new[] { SpriteDefaultName, BuiltInUnlitName, HdrpUnlitName };
                case KvPipelineKind.BuiltIn:
                    return new[] { SpriteDefaultName, BuiltInUnlitName };
                default:
                    return new[] { SpriteDefaultName, BuiltInUnlitName, UrpUnlitName, HdrpUnlitName };
            }
        }

        /// <summary>Шейдер для линий/лучей ТЕКУЩЕГО конвейера (никогда — шейдер чужого).</summary>
        public static Shader FindLine() { return FindFirst(LineChain(ActiveKind())); }

        /// <summary>Шейдер для линий/лучей заданного конвейера.</summary>
        public static Shader FindLine(KvPipelineKind kind) { return FindFirst(LineChain(kind)); }

        /// <summary>
        /// Материал для линии/луча/метки. Возвращает null, если не нашёлся НИ ОДИН шейдер цепочки,
        /// — вызывающий код обязан это проверить и не создавать `new Material(null)`.
        /// </summary>
        public static Material CreateLine(string name, Color color)
        {
            Material material = Create(FindLine(), name, color, true);
            if (material != null) material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        /// <summary>
        /// ШЕЙДЕРЫ, КОТОРЫЕ ОБЯЗАНЫ ПОПАСТЬ В СБОРКУ. Материалы лучей, линий, меток, зон,
        /// теплокарт, призраков и подсветки создаются ВО ВРЕМЯ РАБОТЫ через `Shader.Find`,
        /// а он находит только те шейдеры, что вошли в сборку. Если их там нет, на устройстве
        /// материалы получаются пустыми (или падает `new Material(null)`) — и в шлеме пропадают
        /// лучи, метки и подсветка, хотя на компьютере всё видно.
        ///
        /// ПОЧЕМУ ЗДЕСЬ НЕТ HDRP-ШЕЙДЕРОВ. Материалы компьютера (HDRP) ссылаются из сцен и ассетов
        /// проекта (`Robot_HDRP.mat`, материалы стендов) — они в сборке и без этого списка.
        /// А вот попадание HDRP-шейдеров в «всегда включённые» утянуло бы их варианты
        /// в Android-сборку: проект уже заплатил за это карточкой Б2 (`URP/Lit` — 442 368
        /// вариантов и сборка на 46 часов). Поэтому список — только то, что нужно шлему,
        /// плюс встроенный спрайтовый шейдер, который рисуется в любом конвейере.
        ///
        /// Это единственный источник правды: по нему работает инструмент
        /// `KvRenderPipelineTools.EnsureRuntimeShadersInBuild` и тест-«сторож».
        /// </summary>
        public static string[] RuntimeShaderNamesForBuild()
        {
            return new[]
            {
                SpriteDefaultName,                            // линии, лучи, трубы, кольца (любой конвейер)
                UrpLitName, UrpSimpleLitName, UrpUnlitName    // материалы шлема (URP)
            };
        }

        // ---------------------------------------------------------------- совместимость (аудит)

        /// <summary>Встроенные UNLIT-шейдеры Unity: рисуются в любом конвейере.</summary>
        public static bool IsKnownUnlitBuiltIn(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;
            return shaderName.StartsWith("Unlit/") ||
                   shaderName.StartsWith("Sprites/") ||
                   shaderName.StartsWith("UI/") ||
                   shaderName.StartsWith("TextMeshPro/") ||
                   shaderName.StartsWith("Skybox/");
        }

        /// <summary>
        /// СОВМЕСТИМ ЛИ ШЕЙДЕР С КОНВЕЙЕРОМ — суждение аудита «что станет розовым».
        /// Правила намеренно строгие: «пропавший шейдер» считается несовместимым, потому что
        /// в этом случае Unity рисует материал magenta-ошибкой, а не «как-нибудь».
        /// </summary>
        public static bool IsCompatible(Shader shader, KvPipelineKind kind)
        {
            if (shader == null) return false;
            return IsCompatibleName(shader.name, kind);
        }

        /// <summary>То же по имени шейдера (чистая функция — проверяется тестами).</summary>
        public static bool IsCompatibleName(string shaderName, KvPipelineKind kind)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;
            if (kind == KvPipelineKind.Unknown) return true;      // о чужом конвейере не судим

            bool isUrp = shaderName.StartsWith(UrpPrefix);
            bool isHdrp = shaderName.StartsWith(HdrpPrefix) ||
                          shaderName.StartsWith("High Definition Render Pipeline/");

            switch (kind)
            {
                case KvPipelineKind.Urp:
                    if (isHdrp) return false;                     // вот это и есть «розовое»
                    if (isUrp) return true;
                    if (IsKnownUnlitBuiltIn(shaderName)) return true;
                    if (shaderName.StartsWith("Shader Graphs/")) return true;
                    return false;                                 // Standard, Legacy Shaders/… — не годятся

                case KvPipelineKind.Hdrp:
                    if (isUrp) return false;
                    if (isHdrp) return true;
                    if (IsKnownUnlitBuiltIn(shaderName)) return true;
                    if (shaderName.StartsWith("Shader Graphs/")) return true;
                    return false;

                case KvPipelineKind.BuiltIn:
                    if (isUrp || isHdrp) return false;
                    if (IsKnownUnlitBuiltIn(shaderName)) return true;
                    if (shaderName == BuiltInLitName) return true;
                    if (shaderName.StartsWith("Legacy Shaders/")) return true;
                    if (shaderName.StartsWith("Shader Graphs/")) return true;
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>Строка для журнала: какой конвейер в силе и какой шейдер возьмёт код.</summary>
        public static string Describe()
        {
            KvPipelineKind kind = ActiveKind();
            Shader lit = FindLit(kind);
            Shader unlit = FindUnlit(kind);
            return "конвейер " + kind +
                   " · Lit " + (lit != null ? lit.name : "НЕ НАЙДЕН") +
                   " · Unlit " + (unlit != null ? unlit.name : "НЕ НАЙДЕН") +
                   " · цвет " + ColorPropertyName(lit != null ? lit.name : null, false);
        }

        /// <summary>Полный список имён шейдеров, которые может вернуть фабрика (для отчётов).</summary>
        public static IEnumerable<string> AllShaderNames()
        {
            return new[]
            {
                UrpLitName, UrpSimpleLitName, UrpUnlitName,
                HdrpLitName, HdrpUnlitName,
                BuiltInLitName, BuiltInUnlitName, SpriteDefaultName
            };
        }
    }
}
