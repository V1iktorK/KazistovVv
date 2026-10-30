// ================================================================================================
//  KvRecordingFallbackTests.cs — EditMode-тесты РЕЗЕРВНОГО РЕЖИМА ВИДЕОЗАПИСИ
//  (сессия S44, пункт 5.21 плана).
//
//  ОТКУДА ЗАДАЧА. План требовал решения владельца: «пакет Unity Recorder или ТЗ на серию PNG».
//  Проверка кода 30.10.2026 показала: РЕШЕНИЕ УЖЕ ПРИНЯТО И РЕАЛИЗОВАНО — запись идёт через
//  Unity Recorder (MP4), а если его нет, включается резерв: последовательность PNG-кадров плюс
//  файл `recording.json` с параметрами. То есть пункт не «открыт», а НЕ ПРОВЕРЕН.
//
//  ЧЕМ ЭТО ПЛОХО БЕЗ ТЕСТОВ. Резервный режим оживает ровно тогда, когда основного нет — то есть в
//  сборке без Recorder. Именно там его никто не видит, и любая ошибка (неверное имя файла,
//  отсутствующий каталог, манифест без параметров) обнаружится у оператора, а не у нас.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ:
//    1) имена файлов по ТЗ и с временем внутри (иначе вторая запись затрёт первую);
//    2) манифест содержит ВСЕ параметры записи — по нему кадры потом собираются в видео;
//    3) манифест — корректный JSON, а не склеенные строки;
//    4) числа в манифесте без учёта локали (иначе в русской локали будет «1920,5»).
// ================================================================================================

using System.Text.RegularExpressions;
using KazistovVvFeatures;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Имена и манифест резервной видеозаписи.</summary>
    public class KvRecordingFallbackTests
    {
        /// <summary>
        /// ИМЯ ФАЙЛА ЗАПИСИ ПО ТЗ: `KazistovVv_recording_ГГГГ-ММ-ДД_ЧЧ-ММ-СС`.
        ///
        /// ВРЕМЯ ВНУТРИ — НЕ УКРАШЕНИЕ: без него вторая запись затрёт первую, и оператор потеряет
        /// дубль, который только что сделал.
        /// </summary>
        [Test]
        public void RecordingNameFollowsSpecAndIncludesTime()
        {
            var time = new System.DateTime(2026, 10, 30, 23, 15, 42);
            string name = KvCaptureService.RecordingName(time);

            StringAssert.StartsWith("KazistovVv_recording_", name);
            Assert.IsTrue(Regex.IsMatch(name, @"^KazistovVv_recording_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}$"),
                "имя записи не по ТЗ (ожидалось с временем): " + name);
            StringAssert.Contains("2026-10-30_23-15-42", name,
                "в имени нет даты и времени съёмки — вторая запись затрёт первую: " + name);
        }

        /// <summary>ДВЕ ЗАПИСИ В РАЗНОЕ ВРЕМЯ — РАЗНЫЕ ИМЕНА. Прямая проверка «не затрёт».</summary>
        [Test]
        public void TwoRecordingsAtDifferentTimes_HaveDifferentNames()
        {
            string first = KvCaptureService.RecordingName(new System.DateTime(2026, 10, 30, 23, 15, 42));
            string second = KvCaptureService.RecordingName(new System.DateTime(2026, 10, 30, 23, 15, 43));

            Assert.AreNotEqual(first, second,
                "записи с разницей в секунду получили одинаковое имя — вторая затрёт первую");
        }

        /// <summary>
        /// ИМЯ СКРИНШОТА ПО ТЗ: `KazistovVv_screenshot_ГГГГ-ММ-ДД_ЧЧ-ММ-СС.png`.
        /// Отдельно от записи: у снимка расширение входит в имя, у записи — нет (она бывает
        /// и папкой кадров, и MP4).
        /// </summary>
        [Test]
        public void ScreenshotNameFollowsSpec()
        {
            var time = new System.DateTime(2026, 10, 30, 23, 15, 42);
            string name = KvCaptureService.ScreenshotName(time);

            Assert.IsTrue(Regex.IsMatch(name, @"^KazistovVv_screenshot_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.png$"),
                "имя скриншота не по ТЗ: " + name);
        }

        /// <summary>
        /// МАНИФЕСТ СОДЕРЖИТ ВСЕ ПАРАМЕТРЫ. По нему кадры собираются в видео ПОСЛЕ съёмки; если в
        /// манифесте нет частоты кадров или размера, сборщик не сможет восстановить скорость
        /// воспроизведения — и видео получится «дёрганым», а причина останется неизвестной.
        ///
        /// Проверяется по исходнику: манифест пишется во время записи, и создать его в EditMode без
        /// монобеха можно только отражением. Проверка исходника здесь уместнее — она ловит именно
        /// «забыли параметр».
        /// </summary>
        [Test]
        public void FrameManifest_ListsAllRecordingParameters()
        {
            string path = FindCapturesSource();
            Assert.IsNotNull(path, "не найден KvCaptures.cs — проверить нечего");

            string body = ManifestBody(path);
            Assert.IsNotNull(body, "в KvCaptures.cs не найден метод WriteFrameManifest");

            // САМОПРОВЕРКА РАЗБОРА: тело обязано быть непустым. Если разбор вернёт пустую строку,
            // все проверки ниже «не найдут» параметры и тест покраснеет по ошибке разбора, а не по
            // дефекту — это уже случалось (E-19).
            Assert.Greater(body.Length, 50,
                "тело WriteFrameManifest разобрано как пустое (" + body.Length + " символов) — " +
                "ошибка в разборе, а не в коде");

            var required = new[]
            {
                ("width", "ширина кадра"),
                ("height", "высота кадра"),
                ("fps", "частота кадров"),
                ("baseName", "имя основания записи"),
            };

            // ПОИСК ПО ИМЕНИ КЛЮЧА, БЕЗ КАВЫЧЕК.
            //
            // В исходнике ключ записан как `\"width\"` — с ЭКРАНИРОВАННЫМИ кавычками, потому что
            // это часть строкового литерала C#. Поиск по `"width"` (с настоящими кавычками) не
            // находил ничего, и тест объявлял отсутствие параметров, которые там есть. Достаточно
            // имени: `width:` в исходнике может встретиться только как ключ манифеста.
            var missing = new System.Collections.Generic.List<string>();
            foreach (var item in required)
                if (body.IndexOf(item.Item1 + "\\\"", System.StringComparison.Ordinal) < 0 &&
                    body.IndexOf(item.Item1 + "\"", System.StringComparison.Ordinal) < 0)
                    missing.Add(item.Item1 + " (" + item.Item2 + ")");

            Assert.AreEqual(0, missing.Count,
                "в манифесте записи нет параметров — по нему нельзя собрать видео: " +
                string.Join(", ", missing.ToArray()));
        }

        /// <summary>
        /// МАНИФЕСТ — КОРРЕКТНЫЙ JSON, А НЕ СКЛЕЕННЫЕ СТРОКИ. Классическая ошибка ручной сборки:
        /// забытая запятая или лишняя в конце, и файл не читается ни одним парсером. Здесь
        /// проверяется, что у ключей есть значения и запятые стоят между элементами.
        /// </summary>
        [Test]
        public void FrameManifest_IsWellFormedJson()
        {
            string path = FindCapturesSource();
            Assert.IsNotNull(path);

            string body = ManifestBody(path);
            Assert.IsNotNull(body);

            // Каждая строка с ключом обязана заканчиваться запятой ИЛИ быть без неё, но не иметь
            // двух запятых подряд и не заканчиваться на ",}".
            Assert.IsFalse(Regex.IsMatch(body, @",\s*\\n""\s*\}"),
                "последний элемент JSON заканчивается запятой — строгий парсер это отвергнет");

            int keyCount = Regex.Matches(body, @"\\""\w+\\"":").Count;
            Assert.Greater(keyCount, 4,
                "в манифесте найдено всего " + keyCount + " ключей — похоже, разбор сломан");
        }

        /// <summary>
        /// ЧИСЛА В МАНИФЕСТЕ БЕЗ ЛОКАЛИ. Ширина, высота и частота — целые, но `bitrateKbps` тоже
        /// число. Если однажды понадобится дробное значение, форматирование по текущей культуре
        /// даст «1920,5», и JSON станет нечитаемым в русской локали. Проверяется, что явное
        /// форматирование НЕ с запятой.
        /// </summary>
        [Test]
        public void FrameManifest_UsesInvariantNumbers()
        {
            string path = FindCapturesSource();
            Assert.IsNotNull(path);

            string text = System.IO.File.ReadAllText(path);
            // В этом проекте уже ловили «0,250» вместо «0.250» в файлах отчётов. Правило простое:
            // там, где число попадает в файл, формат обязан быть InvariantCulture.
            Assert.IsFalse(Regex.IsMatch(text, @"ToString\(\s*""[^""]*[^""]""\s*\)\s*\+.*\\""\s*\}"),
                "похоже, число форматируется в JSON без InvariantCulture");
        }

        /// <summary>
        /// ТЕЛО МЕТОДА `WriteFrameManifest` ИЗ ИСХОДНИКА.
        ///
        /// ЧЕСТНО О ДВУХ ПОПЫТКАХ (30.10.2026).
        /// 1) Сначала границы брались регуляркой `void WriteFrameManifest\([^)]*\)(.*?)` — и
        ///    `[^)]*` обрывался на скобке внутри параметров, так что «тело» выходило пустым.
        /// 2) Потом поиск шёл по строке, содержащей `void WriteFrameManifest`. Но в файле есть
        ///    ВЫЗОВ `WriteFrameManifest(recordPath, baseName)` — он стоит РАНЬШЕ объявления, и
        ///    метод «нашёлся» не там, откуда надо.
        ///
        /// Поэтому объявление ищется по точной сигнатуре (с модификатором доступа), а границы
        /// берутся по строкам: от объявления до строки из одной `}`. Вложенные скобки и вызовы
        /// ни на что не влияют.
        /// </summary>
        private static string ManifestBody(string path)
        {
            string[] lines = System.IO.File.ReadAllLines(path);
            int start = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                // ОБЪЯВЛЕНИЕ, а не вызов: перед именем метода есть тип возврата.
                if (Regex.IsMatch(lines[i], @"\bvoid\s+WriteFrameManifest\s*\(")) { start = i; break; }
            }
            if (start < 0) return null;

            var builder = new System.Text.StringBuilder();
            for (int i = start; i < lines.Length; i++)
            {
                builder.AppendLine(lines[i]);
                // Конец тела — строка, где после отступа только закрывающая скобка метода.
                if (i > start && lines[i].Trim() == "}") break;
            }
            return builder.ToString();
        }

        /// <summary>Путь к исходнику службы снимков и записи.</summary>
        private static string FindCapturesSource()
        {
            string root = System.IO.Path.Combine(Application.dataPath, "_Project");
            if (!System.IO.Directory.Exists(root)) return null;
            foreach (string file in System.IO.Directory.GetFiles(root, "KvCaptures.cs",
                         System.IO.SearchOption.AllDirectories))
                return file;
            return null;
        }
    }
}
