// ================================================================================================
//  KvBuildBudgetTests.cs — EditMode-тесты БЮДЖЕТА СБОРКИ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Приложение ставится на шлем PICO 4, где памяти меньше, чем на компьютере, а
//  APK качается по сети. Каждый лишний мегабайт в `Assets` — это мегабайт в сборке. Проверено
//  30.10.2026: `Assets` занимает 208 МБ, крупнейшие вложения — три текстуры стола по 16 МБ и
//  модель зала 17 МБ. Это НЕ значит «плохо»: текстуры уже сжаты и ограничены 2048 пикселями.
//
//  ЗАЧЕМ ТОГДА ТЕСТ. Чтобы «стало больше» не выяснилось уже после сборки. Историю проекта помнит
//  PLANS: APK вырос с 117 МБ (§32) до 191 МБ (§40) — то есть рост случался, и не раз. Тест ставит
//  ПОРОГ, после которого надо осознанно решить: сжимать, убирать или признать.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) `Assets` не превышает разумного бюджета (сейчас с запасом от 208 МБ);
//    2) ни один ОДИНОЧНЫЙ файл не превышает 20 МБ без объяснения в списке исключений;
//    3) настройки импорта крупных текстур ограничены (maxTextureSize не «без предела») и сжаты.
//
//  ГРАНИЦА ЧЕСТНОСТИ: тест не «оптимизирует» ассеты и не удаляет их — это решение владельца.
//  Он только сообщает, когда бюджет перестал держаться.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Бюджет сборки: вес ассетов и настройки импорта текстур.</summary>
    public class KvBuildBudgetTests
    {
        /// <summary>
        /// БЮДЖЕТ `Assets` В МЕГАБАЙТАХ. Текущее значение 208 МБ; порог взят с запасом ~2×, чтобы
        /// тест не краснел от одной новой модели зала. Превышение — сигнал: проверить, что вошло,
        /// и осознанно ли это.
        /// </summary>
        private const double AssetsBudgetMb = 400.0;

        /// <summary>
        /// ПРЕДЕЛ ДЛЯ ОДНОГО ФАЙЛА. 15 МБ — крупный ассет для мобильного шлема.
        ///
        /// ПОЧЕМУ ИМЕННО 15, А НЕ 20 (проверено прогоном 30.10.2026). Сначала стояло 20 МБ «на
        /// всякий случай», и тогда список обоснованных файлов оказался бессмысленным: крупнейший
        /// файл — 17,4 МБ, то есть НИ ОДИН файл предел не превышал, и все записи списка стали
        /// «устаревшими». Тест на устаревание справедливо это и показал.
        ///
        /// Мораль: порог должен стоять там, где он что-то отсекает. Порог, который никто не
        /// превышает, — это не защита, а украшение.
        /// </summary>
        private const double SingleFileLimitMb = 15.0;

        /// <summary>
        /// КРУПНЫЕ ФАЙЛЫ, КОТОРЫЕ ПРИЗНАНЫ ОСОЗНАННЫМИ — с причиной у каждого.
        ///
        /// Список нужен, чтобы отличие «так задумано» от «случайно притащили 100 МБ» было явным.
        /// Новый файл сверх предела обязан появиться здесь вместе с объяснением.
        /// </summary>
        private static readonly Dictionary<string, string> KnownLargeAssets = new Dictionary<string, string>
        {
            { "Level.glb", "модель уровня (зал), загружается как окружение" },
            { "desk_table0.tga", "текстура стола, сжата и ограничена 2048" },
            { "desk_table1.tga", "текстура стола, сжата и ограничена 2048" },
            { "desk_table2.tga", "текстура стола, сжата и ограничена 2048" },
        };

        private static string AssetsRoot
        {
            get { return Application.dataPath; }
        }

        /// <summary>
        /// ТЕКСТУРЫ ОГРАНИЧЕНЫ И СЖАТЫ. `maxTextureSize` без предела (или 8192+) на мобильном шлеме
        /// означает и размер сборки, и память видеокарты. Сжатие обязательно: несжатая RGBA
        /// 2048×2048 занимает 16 МБ в памяти, сжатая — около 2 МБ.
        ///
        /// Проверяются только КРУПНЫЕ исходники (больше 4 МБ): мелочь вроде иконок не интересна.
        /// </summary>
        [Test]
        public void LargeTextures_AreLimitedAndCompressed()
        {
            var problems = new List<string>();
            int checkedTextures = 0;

            foreach (string meta in Directory.GetFiles(AssetsRoot, "*.meta", SearchOption.AllDirectories))
            {
                string asset = meta.Substring(0, meta.Length - 5);
                if (!File.Exists(asset)) continue;
                if (new FileInfo(asset).Length <= 4 * 1024 * 1024) continue;

                string text;
                try { text = File.ReadAllText(meta); }
                catch { continue; }
                if (!text.Contains("TextureImporter")) continue;   // не текстура

                checkedTextures++;
                string name = Path.GetFileName(asset);

                Match maxSize = Regex.Match(text, @"maxTextureSize:\s*(\d+)");
                if (!maxSize.Success)
                {
                    problems.Add(name + ": нет maxTextureSize");
                }
                else
                {
                    int size = int.Parse(maxSize.Groups[1].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    if (size > 4096)
                        problems.Add(name + ": maxTextureSize " + size + " слишком велик для шлема");
                    if (size <= 0)
                        problems.Add(name + ": maxTextureSize без предела (" + size + ")");
                }

                Match compression = Regex.Match(text, @"textureCompression:\s*(\d+)");
                if (compression.Success && compression.Groups[1].Value == "0")
                    problems.Add(name + ": сжатие ВЫКЛЮЧЕНО (textureCompression = 0) — в памяти " +
                                 "такая текстура займёт в 8 раз больше");
            }

            // САМОПРОВЕРКА: если крупных текстур не нашлось, проверка ничего не проверила (E-16).
            Assert.Greater(checkedTextures, 3,
                "найдено всего " + checkedTextures + " крупных текстур — проверка бессмысленна");
            Assert.AreEqual(0, problems.Count,
                "настройки импорта крупных текстур не годятся для шлема: " +
                string.Join("; ", problems.ToArray()));
        }

        /// <summary>
        /// НИ ОДИН ФАЙЛ НЕ ПРЕВЫШАЕТ ПРЕДЕЛ БЕЗ ОБОСНОВАНИЯ. Новый файл на 50 МБ — это не «ну
        /// ладно», это +50 МБ к APK, который оператор будет скачивать и ставить в шлем.
        /// </summary>
        [Test]
        public void NoUnexplainedHugeAssets()
        {
            var unexplained = new List<string>();

            foreach (string file in Directory.GetFiles(AssetsRoot, "*", SearchOption.AllDirectories))
            {
                // Служебные файлы Unity не считаем: .meta весит килобайты, но проверить безопасно.
                double mb = new FileInfo(file).Length / (1024.0 * 1024.0);
                if (mb <= SingleFileLimitMb) continue;

                string name = Path.GetFileName(file);
                if (KnownLargeAssets.ContainsKey(name)) continue;
                unexplained.Add(name + " (" + mb.ToString("0.0") + " МБ)");
            }

            Assert.AreEqual(0, unexplained.Count,
                "эти файлы крупнее " + SingleFileLimitMb + " МБ и НЕ обоснованы — они увеличат APK: " +
                string.Join("; ", unexplained.ToArray()) +
                ". Сожмите/уменьшите либо внесите в KnownLargeAssets с причиной");
        }

        /// <summary>
        /// ОБЩИЙ ВЕС `ASSETS` В БЮДЖЕТЕ. Историю проекта помнит PLANS: APK вырос с 117 МБ до 191 МБ.
        /// Рост случался, и не раз; этот тест замечает его раньше, чем владелец.
        /// </summary>
        [Test]
        public void AssetsFitTheBudget()
        {
            double totalMb = 0;
            int files = 0;
            foreach (string file in Directory.GetFiles(AssetsRoot, "*", SearchOption.AllDirectories))
            {
                totalMb += new FileInfo(file).Length / (1024.0 * 1024.0);
                files++;
            }

            Assert.Greater(files, 100, "прочитано слишком мало файлов (" + files + ") — путь не тот");

            Assert.Less(totalMb, AssetsBudgetMb,
                "вес Assets " + totalMb.ToString("0") + " МБ превысил бюджет " +
                AssetsBudgetMb.ToString("0") + " МБ. Это напрямую увеличит APK для шлема — " +
                "проверьте, что нового добавилось, и решите осознанно");
        }

        /// <summary>
        /// СПИСОК ОБОСНОВАННЫХ КРУПНЫХ ФАЙЛОВ НЕ УСТАРЕЛ: каждый файл из него ДЕЙСТВИТЕЛЬНО
        /// существует и действительно крупный. Иначе список превратится в «разрешение на всё»:
        /// файл удалили, а имя осталось и прикрывает будущий тяжёлый ассет с тем же именем.
        /// </summary>
        [Test]
        public void KnownLargeAssetsListIsNotStale()
        {
            var stale = new List<string>();

            foreach (var pair in KnownLargeAssets)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length < 10)
                    stale.Add(pair.Key + " (нет причины)");

                string[] found = Directory.GetFiles(AssetsRoot, pair.Key, SearchOption.AllDirectories);
                if (found.Length == 0) { stale.Add(pair.Key + " (файла больше нет)"); continue; }

                bool anyLarge = false;
                foreach (string path in found)
                    if (new FileInfo(path).Length > SingleFileLimitMb * 1024 * 1024) anyLarge = true;
                if (!anyLarge) stale.Add(pair.Key + " (уже не крупный — уберите из списка)");
            }

            Assert.AreEqual(0, stale.Count,
                "список обоснованных крупных файлов устарел: " + string.Join("; ", stale.ToArray()));
        }
    }
}
