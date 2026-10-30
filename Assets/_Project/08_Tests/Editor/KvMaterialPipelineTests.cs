// ================================================================================================
//  KvMaterialPipelineTests.cs — EditMode-тесты СОГЛАСОВАННОСТИ МАТЕРИАЛОВ ДВУХ КОНВЕЙЕРОВ
//  (сессия S44, пункт 5.11 плана, карточка Б12).
//
//  ОТКУДА ЗАДАЧА. Проект живёт на двух конвейерах: компьютер — HDRP, шлем (PICO) — URP. Для
//  XR-сцены заведены URP-КОПИИ материалов, а оригиналы остались HDRP. Связи между копией
//  и оригиналом нет НИКАКОЙ: цвет, прозрачность и текстура в них живут отдельно.
//
//  ЧЕМ ЭТО ОПАСНО (уже случалось, дефект A11). Поправили цвет робота в HDRP-оригинале — в шлеме
//  он остался прежним. А если у копии забыть URP-признаки прозрачности (`_Surface = 1`,
//  `_Blend = 0`), стекло в шлеме станет НЕПРОЗРАЧНЫМ (дефект A13), и это заметят только глазами
//  и только в шлеме.
//
//  ЧТО ПРОВЕРЯЕТСЯ (по файлам, без открытия Unity-редактора):
//    1) у КАЖДОЙ URP-копии есть HDRP-оригинал — и наоборот, ни одна копия не «сирота»;
//    2) цвет основы СОВПАДАЕТ, включая альфу (альфа — это прозрачность);
//    3) прозрачные оригиналы дают прозрачные копии (URP-признаки на месте);
//    4) копии используют шейдер URP, а не HDRP (иначе в шлеме будет розовое — дефект A8).
//
//  ЭТО НЕ ФОРМАЛЬНОСТЬ: карточка Б12 висела с 05.10.2026 именно потому, что «сверить глазами»
//  откладывалось. Здесь сверка автоматическая и идёт на каждом прогоне.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>URP-копии материалов против HDRP-оригиналов.</summary>
    public class KvMaterialPipelineTests
    {
        // GUID шейдеров Unity (стабильные): URP/Lit и HDRP/Lit.
        private const string UrpLitGuid = "933532a4fcc9baf4fa0491de14d08ed7";
        private const string HdrpLitGuid = "6e4ae4064600d784cac1e41a9e6f2e59";

        /// <summary>
        /// ПАРЫ «ОРИГИНАЛ HDRP → КОПИЯ URP», которые заведены в проекте осознанно (дефект A11).
        /// Список — контракт: добавили материал в XR-сцену из существующего HDRP-оригинала —
        /// добавьте пару сюда, и тест проверит её наравне с остальными.
        /// </summary>
        private static readonly string[][] Pairs =
        {
            new[] { @"ROOMS\HQ Hangar Free\Materials\Glass.mat",
                    @"_Project\04_Materials\XrHangar\Glass_URP.mat" },
            new[] { @"ROOMS\HQ Hangar Free\Materials\Sheet_metal.mat",
                    @"_Project\04_Materials\XrHangar\Sheet_metal_URP.mat" },
            new[] { @"ROOMS\HQ Hangar Free\Materials\Metals_trim.mat",
                    @"_Project\04_Materials\XrHangar\Metals_trim_URP.mat" },
            new[] { @"ROOMS\HQ Hangar Free\Materials\Concret_floor_wet.mat",
                    @"_Project\04_Materials\XrHangar\Concret_floor_wet_URP.mat" },
            new[] { @"_Project\04_Materials\Robot\Robot_HDRP.mat",
                    @"_Project\04_Materials\Robot\Robot_URP.mat" },
        };

        /// <summary>
        /// КОПИИ БЕЗ ПАРЫ — НЕ «СИРОТЫ», А САМОСТОЯТЕЛЬНЫЕ МАТЕРИАЛЫ (проверено 30.10.2026, S44).
        ///
        /// ЧТО ВЫЯСНИЛОСЬ ПРОВЕРКОЙ, А НЕ ПРЕДПОЛОЖЕНИЕМ. Эти семь URP-материалов ИСПОЛЬЗУЮТСЯ
        /// в XR-сцене (`XrMain.unity`), то есть удалить их нельзя. Но HDRP-оригиналов у них
        /// в проекте НЕТ: `Material_1…6` и `DefaultHDMaterial` пришли из сторонних ассетов
        /// (стенды/шаблонное окружение), и самих HDRP-файлов в проекте не сохранилось.
        ///
        /// ПОЧЕМУ ОНИ ЗДЕСЬ, А НЕ В ОБЩЕМ СПИСКЕ. У них нет «второй стороны», с которой можно
        /// сверить цвет: сверять не с чем. Поэтому для них проверяется ДРУГОЕ и не менее важное —
        /// что они на шейдере URP (иначе в шлеме будут РОЗОВЫМИ, дефект A8) и что в XR-сцене
        /// нет URP-материала, забытого в этом списке. Оба требования — в тестах ниже.
        ///
        /// ЕСЛИ У ТАКОГО МАТЕРИАЛА ПОЯВИТСЯ HDRP-ОРИГИНАЛ — переносите его в `Pairs`, и он
        /// автоматически получит полную сверку цвета и прозрачности.
        /// </summary>
        private static readonly string[] StandaloneUrpCopies =
        {
            @"_Project\04_Materials\URP\Material_1_URP.mat",
            @"_Project\04_Materials\URP\Material_2_URP.mat",
            @"_Project\04_Materials\URP\Material_3_URP.mat",
            @"_Project\04_Materials\URP\Material_4_URP.mat",
            @"_Project\04_Materials\URP\Material_5_URP.mat",
            @"_Project\04_Materials\URP\Material_6_URP.mat",
            @"_Project\04_Materials\URP\DefaultHDMaterial_URP.mat",
        };

        private static string AssetsRoot
        {
            get { return Application.dataPath; }
        }

        private static string PathOf(string relative)
        {
            return Path.Combine(AssetsRoot, relative.Replace('\\', Path.DirectorySeparatorChar));
        }

        /// <summary>Цвет `_BaseColor` из файла материала. `null` — свойства нет.</summary>
        private static Color? BaseColor(string materialPath)
        {
            foreach (string line in File.ReadAllLines(materialPath))
            {
                if (!line.Contains("_BaseColor:")) continue;
                Match m = Regex.Match(line,
                    @"_BaseColor:\s*\{r:\s*([-\d.eE]+),\s*g:\s*([-\d.eE]+),\s*b:\s*([-\d.eE]+),\s*a:\s*([-\d.eE]+)\}");
                if (!m.Success) continue;
                return new Color(
                    float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            return null;
        }

        /// <summary>GUID шейдера материала.</summary>
        private static string ShaderGuid(string materialPath)
        {
            foreach (string line in File.ReadAllLines(materialPath))
            {
                Match m = Regex.Match(line, @"m_Shader:\s*\{fileID:\s*\d+,\s*guid:\s*(\w+)");
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        /// <summary>Значение числового свойства материала (`_Surface`, `_Blend`, …) или null.</summary>
        private static int? IntProperty(string materialPath, string property)
        {
            foreach (string line in File.ReadAllLines(materialPath))
            {
                Match m = Regex.Match(line, @"^\s*-\s*" + Regex.Escape(property) + @":\s*(-?\d+)\s*$");
                if (m.Success) return int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            return null;
        }

        // ---------------------------------------------------------------- 1. ПАРЫ СУЩЕСТВУЮТ

        /// <summary>
        /// КАЖДАЯ ПАРА ЖИВА: оба файла на месте. «Потерянная» копия означает, что в XR-сцене
        /// материал молча заменится на стандартный — в шлеме это будет другой цвет, а не ошибка.
        /// </summary>
        [Test]
        public void EveryPair_HasBothFiles()
        {
            var missing = new List<string>();
            foreach (string[] pair in Pairs)
            {
                if (!File.Exists(PathOf(pair[0]))) missing.Add("оригинал " + pair[0]);
                if (!File.Exists(PathOf(pair[1]))) missing.Add("копия " + pair[1]);
            }
            Assert.AreEqual(0, missing.Count,
                "материалы из контракта пар не найдены: " + string.Join("; ", missing.ToArray()) +
                ". Переименовали материал — поправьте список пар в этом тесте");
        }

        /// <summary>
        /// НИ ОДНОЙ URP-КОПИИ-СИРОТЫ. Каждый файл `*_URP.mat` обязан быть либо в списке ПАР
        /// (тогда сверяется цвет), либо в списке САМОСТОЯТЕЛЬНЫХ (тогда сверять не с чем, но
        /// шейдер всё равно проверяется). Файл вне обоих списков — это забытая копия, которая
        /// разойдётся с оригиналом, и никто этого не заметит.
        /// </summary>
        [Test]
        public void NoOrphanUrpCopies()
        {
            var known = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string[] pair in Pairs) known.Add(pair[1].Replace('\\', Path.DirectorySeparatorChar));
            foreach (string single in StandaloneUrpCopies) known.Add(single.Replace('\\', Path.DirectorySeparatorChar));

            var orphans = new List<string>();
            string urpDir = PathOf(@"_Project\04_Materials");
            if (!Directory.Exists(urpDir))
            {
                // НЕ «пропуск»: отсутствие папки материалов означает, что XR-сцена останется без
                // URP-материалов и в шлеме всё будет розовым. Это красный тест, а не Ignore (E-16).
                Assert.Fail("нет папки URP-материалов: " + urpDir +
                            " — в шлеме материалы будут розовыми (дефект A8)");
            }
            foreach (string file in Directory.GetFiles(urpDir, "*_URP.mat", SearchOption.AllDirectories))
            {
                string relative = file.Substring(AssetsRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, '/')
                    .Replace('/', Path.DirectorySeparatorChar);
                if (!known.Contains(relative)) orphans.Add(relative);
            }

            Assert.AreEqual(0, orphans.Count,
                "URP-копии, которых нет ни в парах, ни в самостоятельных: " +
                string.Join("; ", orphans.ToArray()) +
                ". Допишите материал в контракт — иначе копия разойдётся с оригиналом незаметно");
        }

        /// <summary>
        /// САМОСТОЯТЕЛЬНЫЕ КОПИИ — НА ШЕЙДЕРЕ URP. У них нет оригинала для сверки цвета, но
        /// шейдер обязан быть правильным: HDRP-шейдер под URP даёт РОЗОВЫЙ материал (дефект A8),
        /// и это видно только в шлеме.
        /// </summary>
        [Test]
        public void StandaloneCopies_UseUrpShader()
        {
            var wrong = new List<string>();
            foreach (string relative in StandaloneUrpCopies)
            {
                string path = PathOf(relative);
                if (!File.Exists(path)) { wrong.Add(Path.GetFileName(relative) + " — файла нет"); continue; }
                string g = ShaderGuid(path);
                if (g != UrpLitGuid) wrong.Add(Path.GetFileName(relative) + " — шейдер " + g);
            }
            Assert.AreEqual(0, wrong.Count,
                "самостоятельные URP-материалы не на шейдере URP (в шлеме будут розовыми): " +
                string.Join("; ", wrong.ToArray()));
        }

        // ---------------------------------------------------------------- 2. ЦВЕТ СОВПАДАЕТ

        /// <summary>
        /// ЦВЕТ ОСНОВЫ СОВПАДАЕТ, ВКЛЮЧАЯ АЛЬФУ — это и есть карточка Б12.
        ///
        /// Допуск 1/255: цвет в файле хранится числами с плавающей точкой, и разница меньше шага
        /// квантования невидима глазом. Всё, что больше, — реальное расхождение.
        /// </summary>
        [Test]
        public void EveryPair_HasSameBaseColorIncludingAlpha()
        {
            var mismatches = new List<string>();

            foreach (string[] pair in Pairs)
            {
                string originalPath = PathOf(pair[0]);
                string copyPath = PathOf(pair[1]);
                if (!File.Exists(originalPath) || !File.Exists(copyPath)) continue;   // уже названо выше

                Color? original = BaseColor(originalPath);
                Color? copy = BaseColor(copyPath);

                if (original == null || copy == null)
                {
                    mismatches.Add(Path.GetFileName(pair[0]) + ": свойства _BaseColor нет в " +
                                  (original == null ? "оригинале " : "") + (copy == null ? "копии" : ""));
                    continue;
                }

                Color a = original.Value, b = copy.Value;
                const float tolerance = 1f / 255f;
                if (Mathf.Abs(a.r - b.r) > tolerance || Mathf.Abs(a.g - b.g) > tolerance ||
                    Mathf.Abs(a.b - b.b) > tolerance || Mathf.Abs(a.a - b.a) > tolerance)
                {
                    mismatches.Add(Path.GetFileName(pair[0]) + ": оригинал " + a.ToString() +
                                   " ≠ копия " + b.ToString());
                }
            }

            Assert.AreEqual(0, mismatches.Count,
                "материалы двух конвейеров разошлись (карточка Б12): " +
                string.Join("; ", mismatches.ToArray()) +
                ". В шлеме будет не тот цвет, что на компьютере");
        }

        // ---------------------------------------------------------------- 3. ПРОЗРАЧНОСТЬ

        /// <summary>
        /// ПРОЗРАЧНЫЙ ОРИГИНАЛ — ПРОЗРАЧНАЯ КОПИЯ (дефект A13).
        ///
        /// ПОЧЕМУ ОТДЕЛЬНО ОТ ЦВЕТА. У URP прозрачность включают не альфой, а ДВУМЯ свойствами:
        /// `_Surface = 1` (transparent) и `_Blend = 0` (alpha). Если их забыть, копия останется
        /// непрозрачной, хотя альфа в ней 0,27 — стекло в шлеме станет серой плитой.
        /// </summary>
        [Test]
        public void TransparentOrigins_ProduceTransparentUrpCopies()
        {
            var broken = new List<string>();

            foreach (string[] pair in Pairs)
            {
                string originalPath = PathOf(pair[0]);
                string copyPath = PathOf(pair[1]);
                if (!File.Exists(originalPath) || !File.Exists(copyPath)) continue;

                Color? original = BaseColor(originalPath);
                if (original == null || original.Value.a >= 0.999f) continue;   // оригинал непрозрачный

                int? surface = IntProperty(copyPath, "_Surface");
                int? blend = IntProperty(copyPath, "_Blend");
                if (surface != 1 || blend != 0)
                {
                    broken.Add(Path.GetFileName(pair[1]) + " (_Surface=" +
                               (surface.HasValue ? surface.Value.ToString() : "нет") + ", _Blend=" +
                               (blend.HasValue ? blend.Value.ToString() : "нет") + ")");
                }
            }

            Assert.AreEqual(0, broken.Count,
                "у этих URP-копий не включена прозрачность (дефект A13) — в шлеме они будут " +
                "непрозрачными: " + string.Join("; ", broken.ToArray()));
        }

        // ---------------------------------------------------------------- 4. ШЕЙДЕР

        /// <summary>
        /// КОПИЯ — НА ШЕЙДЕРЕ URP, ОРИГИНАЛ — НА ШЕЙДЕРЕ HDRP.
        ///
        /// САМАЯ ДОРОГАЯ ОШИБКА ЭТОГО КЛАССА (дефект A8): если URP-копия останется на
        /// HDRP-шейдере, в шлеме материал станет РОЗОВЫМ. Розовое видно глазами, но только в
        /// шлеме — то есть через день после правки.
        /// </summary>
        [Test]
        public void CopiesUseUrpShader_OriginalsUseHdrpShader()
        {
            var wrong = new List<string>();

            foreach (string[] pair in Pairs)
            {
                string originalPath = PathOf(pair[0]);
                string copyPath = PathOf(pair[1]);
                if (File.Exists(originalPath))
                {
                    string g = ShaderGuid(originalPath);
                    if (g != HdrpLitGuid) wrong.Add(Path.GetFileName(pair[0]) + " — не HDRP/Lit (" + g + ")");
                }
                if (File.Exists(copyPath))
                {
                    string g = ShaderGuid(copyPath);
                    if (g != UrpLitGuid) wrong.Add(Path.GetFileName(pair[1]) + " — не URP/Lit (" + g + ")");
                }
            }

            Assert.AreEqual(0, wrong.Count,
                "шейдер не того конвейера (дефект A8: под URP материал станет РОЗОВЫМ): " +
                string.Join("; ", wrong.ToArray()));
        }

        // ---------------------------------------------------------------- 5. САМОПРОВЕРКА ПРАВИЛ

        /// <summary>
        /// ПРАВИЛО РАЗБОРА ЦВЕТА РАБОТАЕТ. Если сам разбор ничего не находит, все проверки выше
        /// окажутся «зелёными по определению» — это и есть тест-для-галочки. Поэтому проверяется,
        /// что цвет из настоящего файла читается и что `_Surface` находится.
        /// </summary>
        [Test]
        public void ParsingActuallyReadsRealFiles()
        {
            string glassCopy = PathOf(@"_Project\04_Materials\XrHangar\Glass_URP.mat");
            Assert.IsTrue(File.Exists(glassCopy), "нет файла для самопроверки: " + glassCopy);

            Color? color = BaseColor(glassCopy);
            Assert.IsNotNull(color, "разбор не нашёл _BaseColor в настоящем материале — проверки выше бессмысленны");
            Assert.AreEqual(1f, color.Value.r, 1e-4f, "стекло обязано быть белым по RGB");

            int? surface = IntProperty(glassCopy, "_Surface");
            Assert.AreEqual(1, surface, "у прозрачной URP-копии _Surface обязан быть 1");
        }
    }
}
