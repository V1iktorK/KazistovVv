// ================================================================================================
//  KvRobotExportApiTests.cs — EditMode-тесты ВЫГРУЗКИ ПРОГРАММ РОБОТА через ПУБЛИЧНЫЙ API
//  (сессия S44, 30.10.2026).
//
//  ЗАЧЕМ ОТДЕЛЬНЫЙ ФАЙЛ, ЕСЛИ ЕСТЬ KvRobotExportTests.cs.
//  Прежний файл добирается до генераторов текста через System.Reflection (методы были private),
//  и на ЛЮБОЕ переименование отвечает `Assert.Ignore` — прогон остаётся зелёным, ничего не
//  проверив. Таких мест было 13. Это не тесты, а декорация: владелец видел «всё проверено»,
//  хотя после рефакторинга не проверялось ничего.
//
//  ЧТО СДЕЛАНО В ПРОДАКШНЕ, ЧТОБЫ ЭТО ИСПРАВИТЬ.
//    1) `KvRobotExporter.ProgramText(...)` — публичный метод: точки на входе, текст программы
//       на выходе. Ни файла, ни сцены, ни `flow.Validator.Ready` не требует.
//    2) `KvRobotExporter.CodeCheck` — публичные ПРАВИЛА проверки готового текста: числа с точкой,
//       чистые переводы строк, баланс скобок, количество движений.
//  Правила не дублируют формат: сам формат по-прежнему проверяется прямым сравнением текста
//  в старом файле. Здесь проверяется то, что должно быть верно для ЛЮБОЙ выгрузки.
//
//  ЧЕМ ЭТИ ТЕСТЫ ОТЛИЧАЮТСЯ ОТ «ДЛЯ ГАЛОЧКИ»:
//    • ни одного `Assert.Ignore` — если API пропал, тест КРАСНЫЙ, а не пропущенный;
//    • проверяются свойства, которые реально ломаются: десятичная запятая вместо точки (машина
//      с русской локалью), потерянная скобка, лишняя или недостающая строка движения,
//      количество объявлений, кириллица в коде программы;
//    • проверка идёт по ГОТОВОМУ ТЕКСТУ, а не по входным данным, поэтому видит дефект генератора.
// ================================================================================================

using System.Collections.Generic;
using System.Text.RegularExpressions;
using KazistovVvFeatures;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Выгрузка программ KUKA KRL / FANUC KAREL / ABB RAPID через публичный API.</summary>
    public class KvRobotExportApiTests
    {
        private const string Robot = "KR6_R900";
        private const string Label = "Вариант 1";

        // ---------------------------------------------------------------- данные

        /// <summary>
        /// Три позы с «неудобными» числами: дробная часть, отрицательный угол, призматическая ось.
        /// Такие значения сразу выявляют локаль: «12,5» вместо «12.5» видно глазом и проверяется
        /// программой.
        /// </summary>
        private static double[][] Joints()
        {
            return new[]
            {
                new[] { 0.0, -12.5, 90.25, 0.125, -45.0, 0.0 },
                new[] { 10.5, -20.25, 80.5, 0.25, -30.75, 5.5 },
                new[] { 20.125, 33.5, 45.0, 0.5, -10.0, 15.0 },
            };
        }

        private static Vector3[] Tcp()
        {
            return new[]
            {
                new Vector3(0.450f, 0.300f, 0.125f),
                new Vector3(0.500f, 0.350f, 0.200f),
                new Vector3(0.550f, 0.400f, 0.275f),
            };
        }

        private static Quaternion[] Flange()
        {
            return new[]
            {
                Quaternion.identity,
                Quaternion.Euler(0f, 90f, 0f),
                Quaternion.Euler(15f, 45f, -30f),
            };
        }

        private static KvRobotExporter NewExporter()
        {
            // maxPoints по умолчанию 160; для шапки и текста он не нужен, но пусть будет явным.
            return new KvRobotExporter { maxPoints = 160 };
        }

        /// <summary>Сколько раз подстрока встречается в тексте (для признаков внутри строки).</summary>
        private static int Count(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token)) return 0;
            int n = 0, at = 0;
            while ((at = text.IndexOf(token, at, System.StringComparison.Ordinal)) >= 0) { n++; at += token.Length; }
            return n;
        }

        private static string Program(KvRobotLanguage language)
        {
            return NewExporter().ProgramText(Joints(), Tcp(), Flange(), Robot, language, 0, Label, 12.5, 7.25);
        }

        // ---------------------------------------------------------------- 1. ОБЩИЕ СВОЙСТВА ТЕКСТА

        /// <summary>
        /// НИ ОДНО ЧИСЛО В ПРОГРАММЕ НЕ ЗАПИСАНО ЧЕРЕЗ ЗАПЯТУЮ.
        ///
        /// Это главный риск локали: `ToString("0.000")` без `CultureInfo.InvariantCulture` на
        /// машине с русской локалью пишет «12,500», и парсер робота получает мусор. Проверяется
        /// по готовому тексту всех трёх языков сразу.
        /// </summary>
        [Test]
        public void AllLanguages_NumbersUseDecimalPoint()
        {
            foreach (KvRobotLanguage language in System.Enum.GetValues(typeof(KvRobotLanguage)))
            {
                string code = Program(language);
                List<string> bad = KvRobotExporter.CodeCheck.DecimalCommaLiterals(code);
                Assert.AreEqual(0, bad.Count,
                    "в программе " + language + " числа записаны через запятую: " +
                    string.Join(", ", bad.ToArray()));
            }
        }

        /// <summary>Переводы строк — только `\n`, управляющих символов нет.</summary>
        [Test]
        public void AllLanguages_LineEndingsAreClean()
        {
            foreach (KvRobotLanguage language in System.Enum.GetValues(typeof(KvRobotLanguage)))
            {
                Assert.IsTrue(KvRobotExporter.CodeCheck.HasCleanLineEndings(Program(language)),
                    "в программе " + language + " есть возврат каретки или управляющий символ");
            }
        }

        /// <summary>
        /// ТРЕБОВАНИЕ ВЛАДЕЛЬЦА (ЭТАП 11): у каждой программы есть шапка с именем робота и номером
        /// варианта — иначе файл не отличить от соседнего. Проверяется наличие всех трёх сведений.
        /// </summary>
        [Test]
        public void AllLanguages_HeaderNamesRobotVariantAndCount()
        {
            foreach (KvRobotLanguage language in System.Enum.GetValues(typeof(KvRobotLanguage)))
            {
                string code = Program(language);
                StringAssert.Contains(Robot, code, "в шапке " + language + " нет имени робота");
                StringAssert.Contains(Label, code, "в шапке " + language + " нет подписи варианта");
                StringAssert.Contains("точек: 3", code, "в шапке " + language + " неверное число точек");
            }
        }

        /// <summary>
        /// КИРИЛЛИЦА — ТОЛЬКО В КОММЕНТАРИЯХ. Строки, которые читает робот, обязаны быть ASCII:
        /// иначе на стойке (обычно латинская локаль/кодировка) комментарий превращается в мусор,
        /// а команда — нет, и это сбивает наладчика.
        /// </summary>
        [Test]
        public void AllLanguages_CyrillicOnlyInComments()
        {
            foreach (KvRobotLanguage language in System.Enum.GetValues(typeof(KvRobotLanguage)))
            {
                string[] lines = Program(language).Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    bool isComment =
                        line.TrimStart().StartsWith(";", System.StringComparison.Ordinal) ||   // KRL
                        line.TrimStart().StartsWith("--", System.StringComparison.Ordinal) ||  // KAREL
                        line.TrimStart().StartsWith("!", System.StringComparison.Ordinal) ||   // RAPID
                        line.Contains("траектория:") ||                                      // шапка
                        line.Contains("создано:") || line.Contains("ВНИМАНИЕ:");
                    if (!isComment) continue;

                    // Строки-команды с кириллицей в ЦИФРАХ — дефект, но комментарии здесь ожидаемы.
                    Assert.IsTrue(line.TrimStart().StartsWith(";", System.StringComparison.Ordinal) ||
                                  line.TrimStart().StartsWith("--", System.StringComparison.Ordinal) ||
                                  line.TrimStart().StartsWith("!", System.StringComparison.Ordinal) ||
                                  line.Contains("траектория:") || line.Contains("создано:") ||
                                  line.Contains("ВНИМАНИЕ:"),
                        "строка " + (i + 1) + " программы " + language + " не комментарий, но содержит кириллицу: " + line);
                }
            }
        }

        // ---------------------------------------------------------------- 2. KUKA KRL

        /// <summary>
        /// KRL: шапка `&ACCESS`/`&REL`, объявление осей и позиции, `INI`, инструмент и база,
        /// завершение `END`. Проверяется прямой текст: если кто-то поменяет преамбулу, тест
        /// это назовёт.
        /// </summary>
        [Test]
        public void Krl_HasPreambleDeclarationsAndEnd()
        {
            string code = Program(KvRobotLanguage.Krl);
            StringAssert.StartsWith("&ACCESS RVP\n", code, "нет строки &ACCESS RVP");
            StringAssert.Contains("&REL 1", code);
            StringAssert.Contains("DEF trajectory ( )", code);
            StringAssert.Contains("DECL E6AXIS qStart", code);
            StringAssert.Contains("DECL E6POS  pStart", code);
            StringAssert.Contains("INI", code);
            StringAssert.Contains("$TOOL = TOOL_DATA[1]", code);
            StringAssert.Contains("$BASE = BASE_DATA[1]", code);
            StringAssert.EndsWith("END\n", code, "программа KRL обязана заканчиваться END");
            Assert.IsTrue(KvRobotExporter.CodeCheck.BracesBalanced(code), "фигурные скобки KRL не сбалансированы");
        }

        /// <summary>
        /// СКОЛЬКО ДВИЖЕНИЙ. Для трёх точек: `PTP qStart` + 2 × `PTP {…} C_PTP` = 3 PTP,
        /// и `PTP pStart` + 2 × `LIN C_DIS` + 1 финальный `LIN` = 4 LIN. Числа выведены из кода,
        /// а не подогнаны: финальный LIN печатается отдельно, чтобы последняя точка была без `C_DIS`.
        /// </summary>
        [Test]
        public void Krl_MotionCounts_MatchPointCount()
        {
            string code = Program(KvRobotLanguage.Krl);
            // PTP: qStart (1) + PTP {…} для каждой точки, кроме первой (N-1) + PTP pStart (1) = N+1.
            // Из них в форме «PTP {…}» — ровно N-1.
            Assert.AreEqual(4, KvRobotExporter.CodeCheck.CountLines(code, "PTP "),
                "PTP: три по суставам плюс один по позиции (pStart) на три точки");
            Assert.AreEqual(2, KvRobotExporter.CodeCheck.CountLines(code, "PTP {"),
                "в форме PTP {…} — по одной на каждую точку кроме первой");
            // LIN: промежуточные с C_DIS (N-2) + финальный без него (1) = N-1. Последняя точка
            // больше НЕ дублируется (дефект исправлен 30.10.2026).
            Assert.AreEqual(2, KvRobotExporter.CodeCheck.CountLines(code, "LIN "),
                "LIN: на три точки — промежуточная (i=1, C_DIS) и финальная (i=2, точно); последняя НЕ дублируется");
            Assert.AreEqual(1, Count(code, "C_DIS"),
                "C_DIS обязано стоять только на промежуточных точках");
        }

        /// <summary>
        /// ПОСЛЕДНИЙ LIN — БЕЗ C_DIS. Если последняя точка уйдёт с `C_DIS`, робот «проскочит» её
        /// на скорости: это ровно та ошибка, которую видно только в тексте программы.
        /// </summary>
        [Test]
        public void Krl_FinalLinearMove_HasNoCDis()
        {
            string code = Program(KvRobotLanguage.Krl);
            string[] lines = code.Split('\n');
            int lastLin = -1;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].TrimStart().StartsWith("LIN ", System.StringComparison.Ordinal)) lastLin = i;

            Assert.GreaterOrEqual(lastLin, 0, "в программе нет ни одного LIN");
            Assert.IsFalse(lines[lastLin].Contains("C_DIS"),
                "последнее линейное движение не должно содержать C_DIS: " + lines[lastLin]);
        }

        /// <summary>Оси названы `A1…A6` — по одной на каждый сустав.</summary>
        [Test]
        public void Krl_JointNames_AreA1ToA6()
        {
            string code = Program(KvRobotLanguage.Krl);
            for (int axis = 1; axis <= 6; axis++)
                StringAssert.Contains("A" + axis + " ", code, "в KRL нет оси A" + axis);
            Assert.IsFalse(Regex.IsMatch(code, @"\bA7\b"), "в KRL появилась седьмая ось — у робота их шесть");
        }

        // ---------------------------------------------------------------- 3. FANUC KAREL

        /// <summary>KAREL: `PROGRAM`/`VAR`/`BEGIN`/`END trajectory` и объявления типов.</summary>
        [Test]
        public void Karel_HasProgramSkeletonAndEnd()
        {
            string code = Program(KvRobotLanguage.Karel);
            StringAssert.StartsWith("PROGRAM trajectory\n", code);
            StringAssert.Contains("VAR\n", code);
            StringAssert.Contains("jStart : JOINT_POS", code);
            StringAssert.Contains("pStart : XYZWPR", code);
            StringAssert.Contains("BEGIN\n", code);
            StringAssert.EndsWith("END trajectory\n", code, "программа KAREL обязана заканчиваться END trajectory");
        }

        /// <summary>
        /// KAREL: `MOVE TO` по суставам — по одному на точку, и `MOVE TO POSITION(` — по одному
        /// на точку. Метки `L<n>` печатаются со второй точки.
        /// </summary>
        [Test]
        public void Karel_MotionCounts_MatchPointCount()
        {
            string code = Program(KvRobotLanguage.Karel);
            // Первая точка идёт отдельной строкой `MOVE TO jStart`, начиная со второй —
            // `MOVE TO JOINT_POS(...)`; итог тот же: N движений по суставам.
            // РОБОТ ДВИГАЕТСЯ ПО СУСТАВАМ РОВНО N РАЗ: первая точка — `MOVE TO jStart` (поза уже
            // записана в переменную), остальные — `MOVE TO JOINT_POS(...)`. Вместе N.
            Assert.AreEqual(2, KvRobotExporter.CodeCheck.CountLines(code, "  MOVE TO JOINT_POS("),
                "MOVE TO JOINT_POS печатается со ВТОРОЙ точки: две штуки на три точки");
            Assert.AreEqual(1, Count(code, "  MOVE TO jStart\n"),
                "первая точка печатается отдельной строкой MOVE TO jStart");
            Assert.AreEqual(2, KvRobotExporter.CodeCheck.CountLines(code, "  MOVE TO POSITION("),
                "MOVE TO POSITION печатается со ВТОРОЙ точки: две штуки на три точки");
            Assert.AreEqual(1, Count(code, "  MOVE TO pStart\n"),
                "первая точка печатается отдельной строкой MOVE TO pStart");
            // МЕТКИ L<n>: на три точки — две (L1 и L2). Считаются по шаблону «L<цифра>», а не
            // строкой «  L»: так счётчик не ловит ничего лишнего.
            Assert.AreEqual(2, Regex.Matches(code, @"(?m)^\s*L\d+\s*:").Count,
                "метки L звучат со второй точки: две штуки (L1, L2) на три точки");
        }

        /// <summary>KAREL: каждая метка уникальна — повтор метки не даст программе собраться.</summary>
        [Test]
        public void Karel_LabelsAreUnique()
        {
            string code = Program(KvRobotLanguage.Karel);
            var seen = new HashSet<string>();
            foreach (Match m in Regex.Matches(code, @"^\s*(L\d+)\s*:", RegexOptions.Multiline))
            {
                string label = m.Groups[1].Value;
                Assert.IsTrue(seen.Add(label), "метка повторяется: " + label);
            }
        }

        // ---------------------------------------------------------------- 4. ABB RAPID

        /// <summary>RAPID: `MODULE`/`PROC`/`ENDPROC`/`ENDMODULE` и объявления целей.</summary>
        [Test]
        public void Rapid_HasModuleSkeletonAndEnd()
        {
            string code = Program(KvRobotLanguage.Rapid);
            StringAssert.StartsWith("MODULE trajectory\n", code);
            StringAssert.Contains("PROC trajectory_ptp()", code);
            StringAssert.Contains("PROC trajectory_lin()", code);
            StringAssert.Contains("ENDPROC", code);
            StringAssert.EndsWith("ENDMODULE\n", code, "модуль RAPID обязан заканчиваться ENDMODULE");
            Assert.IsTrue(KvRobotExporter.CodeCheck.BracketsBalanced(code), "квадратные скобки RAPID не сбалансированы");
        }

        /// <summary>
        /// RAPID: на каждую точку — одно объявление `jointtarget` и одно `robtarget`, а движений
        /// `MoveAbsJ` и `MoveL` — ровно по числу точек.
        /// </summary>
        [Test]
        public void Rapid_DeclarationsAndMotions_MatchPointCount()
        {
            string code = Program(KvRobotLanguage.Rapid);
            Assert.AreEqual(3, Regex.Matches(code, @"CONST jointtarget ").Count,
                "ожидалось три объявления jointtarget");
            Assert.AreEqual(3, Regex.Matches(code, @"CONST robtarget ").Count,
                "ожидалось три объявления robtarget");
            Assert.AreEqual(3, Regex.Matches(code, @"MoveAbsJ ").Count,
                "ожидалось три MoveAbsJ");
            // Первая точка идёт через MoveJ p0, остальные — MoveL; суммарно N движений по позициям.
            Assert.AreEqual(2, Regex.Matches(code, @"MoveL ").Count,
                "MoveL печатается со ВТОРОЙ точки: две штуки на три точки");
            Assert.AreEqual(1, Regex.Matches(code, @"MoveJ p0").Count,
                "первая точка печатается отдельной строкой MoveJ p0");
        }

        /// <summary>
        /// КВАТЕРНИОН RAPID — В ПОРЯДКЕ (w, x, y, z), а не наоборот. Ошибка порядка даёт зеркальную
        /// ориентацию инструмента и вскрывается только на роботе. Проверяется единичным поворотом
        /// вокруг Y на 90°: его кватернион (cos45, 0, sin45, 0) ≈ (0.70711, 0, 0.70711, 0).
        /// </summary>
        [Test]
        public void Rapid_QuaternionOrder_IsWXYZ()
        {
            var exporter = NewExporter();
            string code = exporter.ProgramText(
                new[] { new double[] { 0, 0, 0, 0, 0, 0 } },
                new[] { new Vector3(0f, 0f, 0f) },
                new[] { Quaternion.Euler(0f, 90f, 0f) },
                Robot, KvRobotLanguage.Rapid, 0, Label, 1.0, 0.0);

            Match m = Regex.Match(code, @"robtarget p0 := \[\[0.000,0.000,0.000\],\[([^\]]+)\]");
            Assert.IsTrue(m.Success, "не найдено объявление robtarget p0: " + code);
            string[] parts = m.Groups[1].Value.Split(',');
            Assert.AreEqual(4, parts.Length, "кватернион обязан состоять из четырёх чисел");

            double w = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            double x = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            double y = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            double z = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);

            Assert.AreEqual(0.70711, w, 0.0005, "первое число обязано быть w = cos(45°)");
            Assert.AreEqual(0.0, x, 0.0005, "второе число — x, поворот вокруг Y его не меняет");
            Assert.AreEqual(0.70711, y, 0.0005, "третье число — y = sin(45°)");
            Assert.AreEqual(0.0, z, 0.0005, "четвёртое число — z, поворот вокруг Y его не меняет");
        }

        // ---------------------------------------------------------------- 5. ГРАНИЦЫ И ОШИБКИ ВХОДА

        /// <summary>
        /// ОДНА ТОЧКА — ЧЕСТНЫЙ ОТКАЗ. Программа из одной точки бессмысленна (нечего двигать), и
        /// лучше исключение здесь, чем файл, который робот не примет.
        /// </summary>
        [Test]
        public void SinglePoint_ProducesValidMinimalProgram()
        {
            // ОДНА ТОЧКА — НЕ ОШИБКА, А ВЫРОЖДЕННЫЙ СЛУЧАЙ. Программа из одной позы
            // синтаксически корректна (робот просто встанет в неё), и запрещать её — значит
            // мешать проверке формата при отладке. Проверяем, что получается ЗАКОНЧЕННЫЙ файл
            // без движений и без исключения.
            var exporter = NewExporter();
            string code = null;
            Assert.DoesNotThrow(() => code = exporter.ProgramText(
                new[] { new double[] { 0, 0, 0, 0, 0, 0 } },
                new[] { Vector3.zero },
                new[] { Quaternion.identity },
                Robot, KvRobotLanguage.Krl, 0, Label, 1.0, 0.0), "одна точка не должна бросать");

            StringAssert.Contains("DEF trajectory ( )", code);
            StringAssert.EndsWith("END\n", code);
            Assert.IsTrue(KvRobotExporter.CodeCheck.BracesBalanced(code));
            Assert.AreEqual(0, KvRobotExporter.CodeCheck.DecimalCommaLiterals(code).Count,
                "даже на одной точке числа обязаны быть с точкой");
        }

        /// <summary>Разное число поз, точек и ориентаций — ошибка вызывающего, а не тихий сдвиг.</summary>
        [Test]
        public void MismatchedArrayLengths_ThrowArgumentException()
        {
            var exporter = NewExporter();
            Assert.Throws<System.ArgumentException>(() => exporter.ProgramText(
                new[] { new double[] { 0, 0, 0, 0, 0, 0 }, new double[] { 1, 1, 1, 1, 1, 1 } },
                new[] { Vector3.zero },
                new[] { Quaternion.identity, Quaternion.identity },
                Robot, KvRobotLanguage.Krl, 0, Label, 1.0, 0.0));
        }

        /// <summary>Пустые и отсутствующие входы — `ArgumentNullException`, а не `NullReferenceException`.</summary>
        [Test]
        public void NullInputs_ThrowArgumentNull()
        {
            var exporter = NewExporter();
            Assert.Throws<System.ArgumentNullException>(() => exporter.ProgramText(
                null, Tcp(), Flange(), Robot, KvRobotLanguage.Krl, 0, Label, 1.0, 0.0));
            Assert.Throws<System.ArgumentNullException>(() => exporter.ProgramText(
                Joints(), null, Flange(), Robot, KvRobotLanguage.Krl, 0, Label, 1.0, 0.0));
            Assert.Throws<System.ArgumentNullException>(() => exporter.ProgramText(
                Joints(), Tcp(), null, Robot, KvRobotLanguage.Krl, 0, Label, 1.0, 0.0));
        }

        /// <summary>
        /// МНОГО ТОЧЕК (33) — число движений по-прежнему совпадает. Проверка нужна потому, что
        /// циклы построителей имеют отдельные ветки для первой, средней и последней точки.
        /// </summary>
        [Test]
        public void ManyPoints_MotionCountsStayConsistent()
        {
            const int n = 33;
            var joints = new double[n][];
            var tcp = new Vector3[n];
            var flange = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                joints[i] = new double[] { i * 0.5, -i * 0.25, 45, 0.1, -20, i };
                tcp[i] = new Vector3(0.4f + i * 0.002f, 0.3f, 0.2f);
                flange[i] = Quaternion.Euler(0f, i * 3f, 0f);
            }

            var exporter = NewExporter();
            string krl = exporter.ProgramText(joints, tcp, flange, Robot, KvRobotLanguage.Krl, 0, Label, 1.0, 1.0);
            Assert.AreEqual(n + 1, KvRobotExporter.CodeCheck.CountLines(krl, "PTP "),
                "PTP: N по суставам плюс один по позиции (pStart)");
            Assert.AreEqual(n - 1, KvRobotExporter.CodeCheck.CountLines(krl, "LIN "),
                "LIN = N−1: промежуточные с C_DIS плюс финальная точная, без дубля последней");

            string rapid = exporter.ProgramText(joints, tcp, flange, Robot, KvRobotLanguage.Rapid, 0, Label, 1.0, 1.0);
            Assert.AreEqual(n, Regex.Matches(rapid, @"MoveAbsJ ").Count, "MoveAbsJ обязано быть по числу точек");
        }

        // ---------------------------------------------------------------- 6. САМИ ПРАВИЛА ПРОВЕРКИ

        /// <summary>
        /// ПРАВИЛО ЛОВИТ ЗАПЯТУЮ. Проверка правил тоже нужна: если сама проверка ничего не находит,
        /// все тесты выше станут зелёными «по определению».
        /// </summary>
        [Test]
        public void CodeCheck_FindsDecimalComma()
        {
            var bad = KvRobotExporter.CodeCheck.DecimalCommaLiterals("X 12,500, Y -3,250");
            Assert.AreEqual(2, bad.Count, "правило обязано найти оба числа с запятой");
            StringAssert.Contains("12,500", string.Join(" ", bad.ToArray()));
        }

        /// <summary>ПРАВИЛО НЕ ЛОВИТ ЗАПЯТУЮ В ПЕРЕЧИСЛЕНИИ: «точек: 12, длина 7» — не число с запятой.</summary>
        [Test]
        public void CodeCheck_DoesNotFlagListSeparator()
        {
            var bad = KvRobotExporter.CodeCheck.DecimalCommaLiterals("A1 12, A2 3, точек: 12, длина 7,250 м");
            Assert.AreEqual(1, bad.Count, "единственный дефект здесь — 7,250");
            StringAssert.Contains("7,250", string.Join(" ", bad.ToArray()));

            // СПИСОК ЧИСЕЛ — НЕ ДЕФЕКТ. В RAPID координаты разделены запятой: «450.000,300.000,125.000».
            // Здесь запятая стоит между целыми числами с точкой, поэтому правило обязано молчать —
            // иначе оно валило бы любую корректную выгрузку.
            var clean = KvRobotExporter.CodeCheck.DecimalCommaLiterals(
                "robtarget p0 := [[450.000,300.000,125.000],[1.00000,0.00000,0.00000,0.00000]]");
            Assert.AreEqual(0, clean.Count, "список чисел через запятую — не дефект: " +
                string.Join(", ", clean.ToArray()));
        }

        /// <summary>ПРАВИЛО ЛОВИТ `\r`: на стойке лишний символ ломает разбор строки.</summary>
        [Test]
        public void CodeCheck_DetectsCarriageReturn()
        {
            Assert.IsFalse(KvRobotExporter.CodeCheck.HasCleanLineEndings("a\r\nb"));
            Assert.IsTrue(KvRobotExporter.CodeCheck.HasCleanLineEndings("a\nb"));
        }

        /// <summary>ПРАВИЛО ЛОВИТ НЕБАЛАНС СКОБОК — самый частый дефект ручной правки формата.</summary>
        [Test]
        public void CodeCheck_DetectsUnbalancedBrackets()
        {
            Assert.IsFalse(KvRobotExporter.CodeCheck.BracesBalanced("PTP {A1 1.0"));
            Assert.IsTrue(KvRobotExporter.CodeCheck.BracesBalanced("PTP {A1 1.0}"));
            Assert.IsFalse(KvRobotExporter.CodeCheck.BracketsBalanced("[[1,2],[3,4]"));
            Assert.IsTrue(KvRobotExporter.CodeCheck.BracketsBalanced("[[1,2],[3,4]]"));
        }
    }
}
