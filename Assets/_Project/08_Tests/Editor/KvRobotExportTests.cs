// ================================================================================================
//  KvRobotExportTests.cs — EditMode-тесты (Unity Test Framework / NUnit) генерации программ
//  роботов классом KvRobotExporter.
//
//  Класс под тестом : Assets/_Project/01_Scripts/Features/KvRobotExport.cs
//                     (namespace KazistovVvFeatures, 577 строк — файл прочитан целиком)
//  Расположение     : Assets/_Project/08_Tests/Editor/  →  сборка Assembly-CSharp-Editor
//                     (в проекте нет ни одного .asmdef, сборку задаёт имя папки Editor)
//
//  ПОЧЕМУ РЕФЛЕКСИЯ. Текст программы формируют ТОЛЬКО private-методы BuildKrl / BuildKarel /
//  BuildRapid (стр. 252 / 324 / 395), а публичный Export (стр. 146) возвращает ПУТЬ к файлу,
//  безусловно пишет его на диск (File.WriteAllText, стр. 183; каталог создаёт FolderPath →
//  FeatureStorage.EnsureDir, стр. 111/115) и требует живой сцены: flow.Validator.Ready (стр. 155),
//  где flow — TrajectoryFlowController : MonoBehaviour. Чтобы проверить ИМЕННО тот код, который
//  работает в проде, — без правок продакшна, без сцены, без GameObject и без записи файлов —
//  тесты вызывают реальные private-члены через System.Reflection:
//    • private-вложенный тип KvExportPoint (стр. 216–222: q, tcp, flange, time) —
//      Activator.CreateInstance(type, nonPublic: true) + заполнение полей через FieldInfo;
//    • BuildKrl / BuildKarel / BuildRapid — private ЭКЗЕМПЛЯРНЫЕ методы (в постановке ожидались
//      static; по исходнику это НЕ так — см. стр. 252/324/395), фактическая сигнатура:
//      (List<KvExportPoint> pts, string robot, TrajectoryCandidate candidate, int index);
//      поэтому используется BindingFlags.NonPublic | BindingFlags.Instance + экземпляр класса;
//    • private static форматтеры: Joints (302), Cartesian (315), JointsKarel (374),
//      CartesianKarel (386), JointsRapid (440), CartesianRapid (454), Mm (467), Deg (472) —
//      вызываются через BindingFlags.NonPublic | BindingFlags.Static.
//  TrajectoryCandidate (поля label/timeS/lengthM читаются в комментариях) создаётся рефлексией
//  по типу третьего параметра — так тесты не зависят от using TrajectoryCore.
//
//  ЗАЩИТА ОТ ХРУПКОСТИ. [SetUp] разрешает внутренний API по именам и проверяет сигнатуры.
//  Если метод переименован, сигнатура изменилась или KvExportPoint не создаётся через Activator,
//  тест получает Assert.Fail с сообщением «внутренний API изменился» — КРАСНЫЙ тест.
//  НЕ NullReferenceException. То же для каждого private-форматтера: отсутствующий форматтер
//  игнорирует только свои тесты.
//
//  ДЕТЕРМИНИЗМ. Все данные задаются кодом, реальное время и сеть не используются. Метка времени
//  внутри генераторов (DateTime.Now, стр. 256 / 328 / 399) — не дефект: тесты детерминизма
//  сравнивают текст ПОСЛЕ удаления строки «создано:» (см. StripTimestamp).
//
//  ФАКТЫ, ЗАФИКСИРОВАННЫЕ ТЕСТАМИ (а не выдуманный «стандарт»):
//    • FANUC реализован как KAREL (KvRobotLanguage.Karel, стр. 17; расширение .kl, стр. 90);
//      эмиттера FANUC LS/TP и команды CIRC в коде нет вообще;
//    • русские комментарии есть во всех трёх языках (стр. 261 / 331 / 402) — тест
//      AllLanguages_CodeLinesAreAsciiOnly проверяет, что не-ASCII встречается ТОЛЬКО в комментариях;
//    • в RAPID строки «9E9» присутствуют по замыслу (стр. 412/416/421) — это НЕ дефект;
//    • скорости/зоны зашиты в код: v500/z50/z10/v200/z5, tool0 (стр. 426–434).
//
//  НАЙДЕННЫЙ ДЕФЕКТ (зафиксирован тестом Krl_HeaderComment_NumbersFollowCurrentCulture):
//  в строке-комментарии «траектория:» значения candidate.timeS / candidate.lengthM печатаются
//  БЕЗ CultureInfo.InvariantCulture (стр. 266–267, 336–337, 407–408), поэтому при культуре с
//  десятичной запятой (ru-RU — реальная культура этой машины) в файл попадает «время 12,500 с .
//  длина 7,250 м». Числа в СТРОКАХ КОДА при этом всегда инвариантны (там InvariantCulture есть).
//  Лечение — добавить CultureInfo.InvariantCulture в эти три места (правка продакшна запрещена ТЗ).
//
//  Продакшн-код не изменялся; .meta и .asmdef не создавались; Unity не запускалась.
// ================================================================================================

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using KazistovVvFeatures;

namespace KazistovVvTests
{
    /// <summary>
    /// Тесты генерации программ роботов KUKA KRL / FANUC KAREL / ABB RAPID (KvRobotExporter).
    /// Текстовые генераторы класса объявлены private, поэтому вызываются через рефлексию —
    /// проверяется ровно тот код, который работает в проде, без правок продакшна и без записи файлов.
    /// </summary>
    [TestFixture]
    public class KvRobotExportTests
    {
        private const string RobotName = "KR6_R900";
        private const string VariantLabel = "Вариант 1";

        // ---------------------------------------------------------------- состояние рефлексии

        private Type pointType;               // private-вложенный KvExportPoint (стр. 216)
        private Type candidateType;           // TrajectoryCore.TrajectoryCandidate (тип 3-го параметра)
        private FieldInfo fQ, fTcp, fFlange, fTime;
        private MethodInfo mBuildKrl, mBuildKarel, mBuildRapid;
        private object exporter;              // экземпляр KvRobotExporter (Build* — экземплярные методы)

        /// <summary>
        /// Разрешить внутренний API один раз перед каждым тестом. Любое расхождение с ожидаемой
        /// формой private-членов даёт Assert.Fail («внутренний API изменился») — прогон НЕ останется зелёным.
        /// </summary>
        [SetUp]
        public void ResolveInternalApi()
        {
            pointType = typeof(KvRobotExporter).GetNestedType("KvExportPoint", BindingFlags.NonPublic);
            if (pointType == null)
                Assert.Fail("Внутренний API изменился: у KvRobotExporter нет private-вложенного " +
                              "типа KvExportPoint (в исходнике — стр. 216).");

            const BindingFlags anyInstance =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            fQ = pointType.GetField("q", anyInstance);
            fTcp = pointType.GetField("tcp", anyInstance);
            fFlange = pointType.GetField("flange", anyInstance);
            fTime = pointType.GetField("time", anyInstance);
            if (fQ == null || fTcp == null || fFlange == null || fTime == null)
                Assert.Fail("Внутренний API изменился: у KvExportPoint нет ожидаемых полей " +
                              "q / tcp / flange / time (в исходнике — стр. 218–221).");

            mBuildKrl = FindBuilder("BuildKrl");
            mBuildKarel = FindBuilder("BuildKarel");
            mBuildRapid = FindBuilder("BuildRapid");
            ValidateBuilderSignature(mBuildKrl);
            ValidateBuilderSignature(mBuildKarel);
            ValidateBuilderSignature(mBuildRapid);

            try
            {
                exporter = Activator.CreateInstance(typeof(KvRobotExporter));
            }
            catch (Exception e)
            {
                Assert.Fail("KvRobotExporter больше не создаётся через Activator: " +
                              e.GetType().Name + ": " + e.Message);
            }
        }

        // ---------------------------------------------------------------- вспомогательные методы

        /// <summary>
        /// Найти private-генератор по имени. Build* — ЭКЗЕМПЛЯРНЫЕ методы, поэтому Bindings
        /// содержат Instance (в постановке предполагались static — сверено с исходником).
        /// </summary>
        private static MethodInfo FindBuilder(string name)
        {
            MethodInfo m = typeof(KvRobotExporter).GetMethod(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null)
                Assert.Fail("Внутренний API изменился: private-метод " + name +
                              " не найден у KvRobotExporter (в исходнике — стр. 252/324/395).");
            return m;
        }

        /// <summary>Проверить сигнатуру генератора: (List&lt;KvExportPoint&gt;, string, TrajectoryCandidate, int).</summary>
        private void ValidateBuilderSignature(MethodInfo builder)
        {
            ParameterInfo[] ps = builder.GetParameters();
            bool ok = ps.Length == 4
                      && ps[0].ParameterType.IsGenericType
                      && ps[0].ParameterType.GetGenericTypeDefinition() == typeof(List<>)
                      && ps[0].ParameterType.GetGenericArguments()[0] == pointType
                      && ps[1].ParameterType == typeof(string)
                      && ps[3].ParameterType == typeof(int);
            if (!ok)
                Assert.Fail("Внутренний API изменился: сигнатура " + builder.Name +
                              " отличается от (List<KvExportPoint>, string, TrajectoryCandidate, int).");
            candidateType = ps[2].ParameterType;
        }

        /// <summary>Private static форматтер по имени; отсутствие — Assert.Fail (красный тест, не пропуск).</summary>
        private static MethodInfo Formatter(string name)
        {
            MethodInfo m = typeof(KvRobotExporter).GetMethod(
                name, BindingFlags.NonPublic | BindingFlags.Static);
            if (m == null)
                Assert.Fail("Внутренний API изменился: private static метод " + name +
                              " не найден у KvRobotExporter.");
            return m;
        }

        /// <summary>Создать private KvExportPoint и заполнить его поля рефлексией.</summary>
        private object NewPoint(double[] q, Vector3 tcp, Quaternion flange, float time)
        {
            object point;
            try
            {
                point = Activator.CreateInstance(pointType, true);
            }
            catch (Exception e)
            {
                Assert.Fail("KvExportPoint больше не создаётся через Activator.CreateInstance: " +
                              e.GetType().Name + ": " + e.Message);
                return null;
            }
            fQ.SetValue(point, q);
            fTcp.SetValue(point, tcp);
            fFlange.SetValue(point, flange);
            fTime.SetValue(point, time);
            return point;
        }

        /// <summary>Список точек в том виде, в каком его принимает генератор (List&lt;KvExportPoint&gt;).</summary>
        private object MakePointList(params object[] points)
        {
            Type listType = typeof(List<>).MakeGenericType(pointType);
            object list = Activator.CreateInstance(listType);
            MethodInfo add = listType.GetMethod("Add");
            foreach (object p in points) add.Invoke(list, new[] { p });
            return list;
        }

        /// <summary>Детерминированные суставы: ось a → (a+1)·10° + 5°·i.</summary>
        private static double[] DefaultJoints(int i)
        {
            var q = new double[6];
            for (int a = 0; a < 6; a++) q[a] = (a + 1) * 10.0 + i * 5.0;
            return q;
        }

        /// <summary>Набор из count точек: позиция в метрах, ориентация через Quaternion.Euler.</summary>
        private object[] DefaultPoints(int count)
        {
            var pts = new object[count];
            for (int i = 0; i < count; i++)
                pts[i] = NewPoint(
                    DefaultJoints(i),
                    new Vector3(0.1f * i, 0.2f * i, 0.3f * i),
                    Quaternion.Euler(10f * i, 20f * i, 30f * i),
                    0.1f * i);
            return pts;
        }

        /// <summary>Экземпляр TrajectoryCandidate с заполненными полями, читаемыми в комментариях.</summary>
        private object NewCandidate(string label, float timeS, float lengthM)
        {
            object candidate;
            try
            {
                candidate = Activator.CreateInstance(candidateType);
            }
            catch (Exception e)
            {
                Assert.Fail("TrajectoryCandidate больше не создаётся через Activator: " +
                              e.GetType().Name + ": " + e.Message);
                return null;
            }
            SetCandidateField(candidate, "label", label);
            SetCandidateField(candidate, "timeS", timeS);
            SetCandidateField(candidate, "lengthM", lengthM);
            return candidate;
        }

        /// <summary>Записать публичное поле кандидата (label / timeS / lengthM) через рефлексию.</summary>
        private static void SetCandidateField(object candidate, string name, object value)
        {
            FieldInfo f = candidate.GetType().GetField(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null)
                Assert.Fail("Поле TrajectoryCandidate." + name + " не найдено — генератор читает " +
                              "его в строке-комментарии (стр. 265/335/406).");
            f.SetValue(candidate, value);
        }

        /// <summary>Вызвать генератор с готовым списком точек; исключение разворачивается из Invoke.</summary>
        private string Build(MethodInfo builder, object pointList, string label)
        {
            object candidate = NewCandidate(label, 12.5f, 7.25f);
            try
            {
                return (string)builder.Invoke(exporter,
                    new object[] { pointList, RobotName, candidate, 0 });
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                // Разворачиваем: иначе Assert.That(...) увидел бы только TargetInvocationException.
                throw tie.InnerException;
            }
        }

        /// <summary>Вызвать генератор с count автоматически построенными точками.</summary>
        private string Build(MethodInfo builder, int pointCount, string label)
        {
            return Build(builder, MakePointList(DefaultPoints(pointCount)), label);
        }

        /// <summary>Тексты всех трёх языков на ОДНИХ И ТЕХ ЖЕ данных.</summary>
        private string[] BuildAllThree(object pointList, string label)
        {
            return new[]
            {
                Build(mBuildKrl, pointList, label),
                Build(mBuildKarel, pointList, label),
                Build(mBuildRapid, pointList, label)
            };
        }

        private static int Count(string text, string pattern)
        {
            return Regex.Matches(text, pattern).Count;
        }

        /// <summary>
        /// Убрать строку с меткой времени: внутри генераторов используется DateTime.Now
        /// (стр. 256 / 328 / 399) — это не дефект, а единственный источник вариативности,
        /// поэтому детерминизм проверяется на тексте без этой строки.
        /// </summary>
        private static string StripTimestamp(string text)
        {
            return Regex.Replace(text, @"(?m)^.*создано:.*\n", "");
        }

        /// <summary>Строка кода (без комментария) не должна содержать не-ASCII символов.</summary>
        private static bool IsCodeLineAscii(string line)
        {
            string trimmed = line.TrimStart();
            if (trimmed.Length == 0) return true;
            if (trimmed[0] == ';' || trimmed[0] == '!') return true;              // комментарий KRL / RAPID
            if (trimmed.StartsWith("--", StringComparison.Ordinal)) return true;  // комментарий KAREL
            string code = trimmed;
            int cut = code.IndexOf("--", StringComparison.Ordinal);               // хвостовой комментарий KAREL
            if (cut >= 0) code = code.Substring(0, cut);
            cut = code.IndexOf(';');                                             // хвостовой комментарий KRL
            if (cut >= 0) code = code.Substring(0, cut);
            return !Regex.IsMatch(code, @"[^\x00-\x7F]");
        }

        /// <summary>
        /// Оставить только СТРОКИ КОДА (без комментариев). Нужно потому, что в комментарии-заголовке
        /// время и длина печатаются без InvariantCulture (стр. 266–267/336–337/407–408) и при
        /// культуре с десятичной запятой дают «12,500» — проверять разделитель дробной части
        /// корректно именно на коде программы.
        /// </summary>
        private static string CodeOnly(string text)
        {
            var kept = new List<string>();
            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.TrimStart();
                if (trimmed.Length > 0 &&
                    (trimmed[0] == ';' || trimmed[0] == '!' ||
                     trimmed.StartsWith("--", StringComparison.Ordinal)))
                    continue;                                    // строка-комментарий целиком
                string code = line;
                int cut = code.IndexOf("--", StringComparison.Ordinal);   // хвостовой комментарий KAREL
                if (cut >= 0) code = code.Substring(0, cut);
                cut = code.IndexOf(';');                                  // хвостовой комментарий KRL
                if (cut >= 0) code = code.Substring(0, cut);
                kept.Add(code);
            }
            return string.Join("\n", kept.ToArray());
        }

        // ============================================================ KUKA KRL (BuildKrl, 252–300)

        /// <summary>KRL: заголовок и объявления — точно тот формат, что печатает BuildKrl.</summary>
        [Test]
        public void Krl_Header_HasExpectedPreambleAndDeclarations()
        {
            string t = Build(mBuildKrl, 3, VariantLabel);

            Assert.That(t, Does.StartWith("&ACCESS RVP\n"), "KRL начинается с &ACCESS RVP (стр. 257)");
            Assert.That(t, Does.Contain("&REL 1\n"), "&REL 1 (стр. 258)");
            Assert.That(t, Does.Contain("DEF trajectory ( )\n"), "DEF trajectory ( ) (стр. 259)");
            Assert.That(t, Does.Contain("  DECL E6AXIS qStart\n"), "DECL E6AXIS qStart (стр. 271)");
            Assert.That(t, Does.Contain("  DECL E6POS  pStart\n"), "DECL E6POS  pStart (стр. 272, два пробела)");
            Assert.That(t, Does.Contain("  INI\n"), "INI (стр. 273)");
            Assert.That(t, Does.Contain("  $TOOL = TOOL_DATA[1]\n"), "$TOOL (стр. 274)");
            Assert.That(t, Does.Contain("  $BASE = BASE_DATA[1]\n"), "$BASE (стр. 275)");
            Assert.That(t, Does.Contain("  qStart = {A1 "), "присваивание qStart (стр. 279)");
            Assert.That(t, Does.Contain("  PTP qStart\n"), "PTP qStart (стр. 280)");
            Assert.That(t, Does.Contain("  pStart = {X "), "присваивание pStart (стр. 289)");
            Assert.That(t, Does.Contain("  PTP pStart\n"), "PTP pStart (стр. 290)");
        }

        /// <summary>KRL: файл завершается ровно одним END (стр. 298).</summary>
        [Test]
        public void Krl_Footer_EndsWithSingleEnd()
        {
            string t = Build(mBuildKrl, 2, VariantLabel);

            Assert.That(t, Does.EndWith("END\n"));
            Assert.That(Count(t, @"(?m)^END\s*$"), Is.EqualTo(1), "ключевое слово END — ровно одно (стр. 298)");
        }

        /// <summary>KRL: баланс DEF и END (открытие модуля и его закрытие).</summary>
        [Test]
        public void Krl_DefAndEnd_AreBalanced()
        {
            string t = Build(mBuildKrl, 4, VariantLabel);

            int def = Count(t, @"(?m)^DEF\b");
            int end = Count(t, @"(?m)^END\s*$");
            Assert.That(def, Is.EqualTo(1), "DEF один (стр. 259)");
            Assert.That(end, Is.EqualTo(1), "END один (стр. 298)");
            Assert.That(def, Is.EqualTo(end), "DEF/END должны быть сбалансированы");
        }

        /// <summary>KRL: счётчики инструкций движения — функции от N (PTP=N+1, LIN=N, C_PTP=C_DIS=N−1).</summary>
        [Test]
        public void Krl_MotionCounters_MatchFormulas_ForTwoThreeFivePoints()
        {
            foreach (int n in new[] { 2, 3, 5 })
            {
                string t = Build(mBuildKrl, n, VariantLabel);
                Assert.That(Count(t, @"(?m)^\s*PTP\b"), Is.EqualTo(n + 1),
                    "PTP = N+1 (стр. 280 + 283×(N−1) + 290), N=" + n);
                Assert.That(Count(t, @"(?m)^\s*LIN\b"), Is.EqualTo(n - 1),
                    // LIN = N−1: промежуточные с C_DIS (N−2) + финальная точная (1).
                    // ФИНАЛЬНАЯ ТОЧКА БОЛЬШЕ НЕ ДУБЛИРУЕТСЯ (исправлено 30.10.2026, аудит S43),
                    // поэтому раньше здесь стояло «LIN = N»; значение изменено осознанно.
                    "LIN = N−1 (промежуточные с C_DIS + финальная точная), N=" + n);
                Assert.That(Count(t, "C_PTP"), Is.EqualTo(n - 1),
                    "C_PTP = N−1 (стр. 283), N=" + n);
                // C_DIS = max(0, N−2): проскакивание ставится на все линейные участки КРОМЕ финального.
                // Раньше здесь было «C_DIS = N−1», потому что финальная точка дублировалась
                // (дефект исправлен 30.10.2026, аудит S43). При N=2 участок ровно один, и он же
                // финальный — поэтому C_DIS нет вовсе.
                Assert.That(Count(t, "C_DIS"), Is.EqualTo(System.Math.Max(0, n - 2)),
                    "C_DIS = max(0, N−2), N=" + n);
            }
        }

        /// <summary>KRL: последняя линейная инструкция идёт без C_DIS и стоит перед END (стр. 296–298).</summary>
        [Test]
        public void Krl_FinalLinearMove_HasNoCDis()
        {
            string t = Build(mBuildKrl, 5, VariantLabel);
            string[] lines = t.Split('\n');

            int linTotal = 0, linPlain = 0, lastLin = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!Regex.IsMatch(lines[i], @"^\s*LIN\b")) continue;
                linTotal++;
                lastLin = i;
                if (!lines[i].Contains("C_DIS")) linPlain++;
            }

            // LIN = N−1 = 4: было N, потому что финальная точка дублировалась (дефект исправлен
            // 30.10.2026, аудит S43). Значение изменено осознанно — это и есть доказательство правки.
            Assert.That(linTotal, Is.EqualTo(4), "LIN = N−1 = 4 (финальная точка не дублируется)");
            Assert.That(linPlain, Is.EqualTo(1), "без C_DIS — ровно одна LIN (финальная, стр. 296)");
            Assert.That(lastLin, Is.GreaterThan(0));
            Assert.That(lines[lastLin + 1], Is.EqualTo("END"),
                "финальный LIN стоит непосредственно перед END (стр. 296–298)");
        }

        /// <summary>KRL: одна точка — программа генерируется без исключения (PTP = N+1 = 2, LIN = N = 1).</summary>
        [Test]
        public void Krl_OnePoint_GeneratesProgramWithoutException()
        {
            string t = Build(mBuildKrl, 1, VariantLabel);

            Assert.That(string.IsNullOrEmpty(t), Is.False);
            Assert.That(Count(t, @"(?m)^\s*PTP\b"), Is.EqualTo(2), "PTP = N+1 = 2");
            Assert.That(Count(t, @"(?m)^\s*LIN\b"), Is.EqualTo(1), "LIN = N = 1");
            Assert.That(Count(t, "C_PTP"), Is.EqualTo(0), "C_PTP = N−1 = 0");
            Assert.That(Count(t, "C_DIS"), Is.EqualTo(0), "C_DIS = N−1 = 0");
            Assert.That(t, Does.EndWith("END\n"));
        }

        /// <summary>
        /// KRL: ноль точек — РЕАЛЬНОЕ исключение из генератора: pts[0] без проверки (стр. 279).
        /// Публичный Export такой вход отсекает раньше (стр. 148–169), поэтому это ограничение
        /// именно внутреннего API, а не пользовательский сценарий.
        /// </summary>
        [Test]
        public void Krl_ZeroPoints_ThrowsArgumentOutOfRange()
        {
            Assert.That(() => Build(mBuildKrl, MakePointList(DefaultPoints(0)), VariantLabel),
                Throws.TypeOf<ArgumentOutOfRangeException>(),
                "BuildKrl обращается к pts[0] (стр. 279) — с пустым списком падает; " +
                "публичный Export отсекает такой вход (стр. 164–169)");
        }

        /// <summary>KRL: только LF, никаких \r и прочих управляющих символов.</summary>
        [Test]
        public void Krl_Text_UsesOnlyLineFeedAndHasNoControlCharacters()
        {
            string t = Build(mBuildKrl, 3, VariantLabel);

            Assert.That(t.Contains("\r"), Is.False, "перевод строки только \\n (все Append заканчиваются \\n)");
            Assert.That(Regex.IsMatch(t, @"[\x00-\x09\x0B-\x1F\x7F]"), Is.False,
                "посторонних управляющих символов нет");
        }

        /// <summary>KRL: повторная генерация на тех же данных даёт идентичный текст (без строки времени).</summary>
        [Test]
        public void Krl_RepeatedGeneration_IsIdenticalAfterTimestampRemoval()
        {
            string first = StripTimestamp(Build(mBuildKrl, 3, VariantLabel));
            string second = StripTimestamp(Build(mBuildKrl, 3, VariantLabel));

            Assert.That(second, Is.EqualTo(first),
                "различие может дать только DateTime.Now в строке «; создано:» (стр. 256) — она удалена");
        }

        /// <summary>KRL: строка метки времени имеет ожидаемый формат yyyy-MM-dd HH:mm:ss.</summary>
        [Test]
        public void Krl_TimestampLine_HasExpectedFormat()
        {
            string t = Build(mBuildKrl, 2, VariantLabel);

            Assert.That(Regex.IsMatch(t, @"(?m)^; создано: \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$"), Is.True,
                "строка «; создано:» с форматом yyyy-MM-dd HH:mm:ss (стр. 256/264)");
        }

        /// <summary>
        /// KRL: В ШАПКЕ ВРЕМЯ И ДЛИНА — ВСЕГДА С ТОЧКОЙ (закрыто 30.10.2026, аудит S43).
        ///
        /// РАНЬШЕ ТЕСТ БЫЛ ХАРАКТЕРИЗАЦИОННЫМ. Он фиксировал дефект: время и длина печатались без
        /// `CultureInfo.InvariantCulture`, поэтому на машине с русской локалью в файл попадало
        /// «время 12,500 с». С 30.10.2026 дефект исправлен в трёх местах (KRL/KAREL/RAPID), и тест
        /// перевёрнут: теперь он ТРЕБУЕТ точку независимо от локали. Это надёжнее исходной задумки
        /// «сообщать об изменении»: ошибка ловится красным тестом, а не сообщением в отчёте.
        /// </summary>
        [Test]
        public void Krl_HeaderComment_NumbersAlwaysUseDecimalPoint()
        {
            string t = Build(mBuildKrl, 2, VariantLabel);

            Assert.That(t, Does.Contain("время 12.500 с"),
                "в шапке KRL время обязано быть с точкой независимо от локали машины");
            Assert.That(t, Does.Contain("длина 7.250 м"),
                "в шапке KRL длина обязана быть с точкой независимо от локали машины");
            Assert.That(t, Does.Not.Contain("12,500"), "десятичная запятая вернулась в шапку KRL");
            Assert.That(t, Does.Not.Contain("7,250"), "десятичная запятая вернулась в шапку KRL");
        }

        /// <summary>
        /// KRL: арифметика LastLines — Export считает code.Split('\n').Length (стр. 192), а текст
        /// заканчивается переводом строки, поэтому значение на 1 больше фактического числа строк.
        /// </summary>
        [Test]
        public void Krl_LastLinesFormula_CountsOneExtraLine()
        {
            string t = Build(mBuildKrl, 3, VariantLabel);

            int lastLines = t.Split('\n').Length;       // формула из Export (стр. 192)
            int newlines = Count(t, "\n");              // = число строк текста (текст кончается \n)
            Assert.That(lastLines, Is.EqualTo(newlines + 1),
                "Split('\\n') на тексте с завершающим \\n даёт число строк + 1 — так и считается LastLines");
        }

        // ======================================================= FANUC KAREL (BuildKarel, 324–371)

        /// <summary>KAREL: заголовок, секции VAR/BEGIN и объявления — фактический формат BuildKarel.</summary>
        [Test]
        public void Karel_Header_HasProgramVarBeginAndDeclarations()
        {
            string t = Build(mBuildKarel, 3, VariantLabel);

            Assert.That(t, Does.StartWith("PROGRAM trajectory\n"), "PROGRAM trajectory (стр. 329)");
            Assert.That(t, Does.Contain("\nVAR\n"), "секция VAR (стр. 340)");
            Assert.That(t, Does.Contain("  jStart : JOINT_POS\n"), "jStart : JOINT_POS (стр. 341)");
            Assert.That(t, Does.Contain("  pStart : XYZWPR\n"), "pStart : XYZWPR (стр. 342)");
            Assert.That(t, Does.Contain("  vSpeed : INTEGER\n"), "vSpeed : INTEGER (стр. 343)");
            Assert.That(t, Does.Contain("BEGIN\n"), "BEGIN (стр. 344)");
            Assert.That(t, Does.Contain("  jStart = JOINT_POS("), "присваивание JOINT_POS (стр. 350)");
            Assert.That(t, Does.Contain("  MOVE TO jStart\n"), "MOVE TO jStart (стр. 351)");
            Assert.That(t, Does.Contain("  pStart = POSITION("), "присваивание POSITION (стр. 363)");
            Assert.That(t, Does.Contain("-- "), "комментарии KAREL — «--» (стр. 330)");
            Assert.That(t, Does.Not.Contain("PROGRAM TP"), "это KAREL, а не FANUC TP/LS");
        }

        /// <summary>KAREL: завершение END trajectory (стр. 369).</summary>
        [Test]
        public void Karel_Footer_EndsWithEndTrajectory()
        {
            string t = Build(mBuildKarel, 2, VariantLabel);

            Assert.That(t, Does.EndWith("END trajectory\n"));
            Assert.That(Count(t, @"(?m)^END trajectory\s*$"), Is.EqualTo(1));
        }

        /// <summary>KAREL: баланс PROGRAM и END.</summary>
        [Test]
        public void Karel_ProgramAndEnd_AreBalanced()
        {
            string t = Build(mBuildKarel, 4, VariantLabel);

            int program = Count(t, @"(?m)^PROGRAM\b");
            int end = Count(t, @"(?m)^END\b");
            Assert.That(program, Is.EqualTo(1), "PROGRAM один (стр. 329)");
            Assert.That(end, Is.EqualTo(1), "END один (стр. 369)");
            Assert.That(program, Is.EqualTo(end), "PROGRAM/END должны быть сбалансированы");
        }

        /// <summary>KAREL: счётчики — MOVE TO = 2N, JOINT_POS( = N, POSITION( = N, метки и JMP по N−1.</summary>
        [Test]
        public void Karel_MotionCounters_MatchFormulas_ForTwoThreeFivePoints()
        {
            foreach (int n in new[] { 2, 3, 5 })
            {
                string t = Build(mBuildKarel, n, VariantLabel);
                Assert.That(Count(t, @"(?m)^\s*MOVE TO\b"), Is.EqualTo(2 * n),
                    "MOVE TO = 2N (стр. 351/358 + 364/367), N=" + n);
                Assert.That(Count(t, @"JOINT_POS\("), Is.EqualTo(n),
                    "JOINT_POS( = N (стр. 350/358), N=" + n);
                Assert.That(Count(t, @"POSITION\("), Is.EqualTo(n),
                    "POSITION( = N (стр. 363/367), N=" + n);
                Assert.That(Count(t, @"(?m)^\s*L\d+ :\s*$"), Is.EqualTo(n - 1),
                    "метки L<i> : = N−1 (стр. 357), N=" + n);
                Assert.That(Count(t, @"(?m)^\s*JMP\b"), Is.EqualTo(n - 1),
                    "JMP = N−1 (стр. 355), N=" + n);
            }
        }

        /// <summary>KAREL: одна точка — меток и JMP нет, но программа валидна (MOVE TO = 2).</summary>
        [Test]
        public void Karel_OnePoint_HasNoJumpLabelsAndRemainsValid()
        {
            string t = Build(mBuildKarel, 1, VariantLabel);

            Assert.That(Count(t, @"(?m)^\s*MOVE TO\b"), Is.EqualTo(2), "MOVE TO = 2N = 2");
            Assert.That(Count(t, @"JOINT_POS\("), Is.EqualTo(1));
            Assert.That(Count(t, @"POSITION\("), Is.EqualTo(1));
            Assert.That(Count(t, @"(?m)^\s*JMP\b"), Is.EqualTo(0), "JMP = N−1 = 0");
            Assert.That(Count(t, @"(?m)^\s*L\d+ :\s*$"), Is.EqualTo(0));
            Assert.That(t, Does.EndWith("END trajectory\n"));
        }

        /// <summary>KAREL: ноль точек — исключение из генератора (pts[0], стр. 350).</summary>
        [Test]
        public void Karel_ZeroPoints_ThrowsArgumentOutOfRange()
        {
            Assert.That(() => Build(mBuildKarel, MakePointList(DefaultPoints(0)), VariantLabel),
                Throws.TypeOf<ArgumentOutOfRangeException>(),
                "BuildKarel обращается к pts[0] (стр. 350) — с пустым списком падает; " +
                "публичный Export отсекает такой вход (стр. 164–169)");
        }

        /// <summary>KAREL: только LF и никаких посторонних управляющих символов.</summary>
        [Test]
        public void Karel_Text_UsesOnlyLineFeedAndHasNoControlCharacters()
        {
            string t = Build(mBuildKarel, 3, VariantLabel);

            Assert.That(t.Contains("\r"), Is.False, "перевод строки только \\n");
            Assert.That(Regex.IsMatch(t, @"[\x00-\x09\x0B-\x1F\x7F]"), Is.False);
        }

        /// <summary>KAREL: повторная генерация идентична (метка времени удалена).</summary>
        [Test]
        public void Karel_RepeatedGeneration_IsIdenticalAfterTimestampRemoval()
        {
            string first = StripTimestamp(Build(mBuildKarel, 3, VariantLabel));
            string second = StripTimestamp(Build(mBuildKarel, 3, VariantLabel));

            Assert.That(second, Is.EqualTo(first),
                "единственная вариативность — DateTime.Now в строке «-- создано:» (стр. 328/334)");
        }

        // ======================================================== ABB RAPID (BuildRapid, 395–438)

        /// <summary>RAPID: структура MODULE/PROC/ENDPROC/ENDMODULE и её баланс.</summary>
        [Test]
        public void Rapid_Structure_HasModuleAndTwoProcedures_WithBalance()
        {
            string t = Build(mBuildRapid, 3, VariantLabel);

            Assert.That(t, Does.StartWith("MODULE trajectory\n"), "MODULE trajectory (стр. 400)");
            Assert.That(t, Does.EndWith("ENDMODULE\n"), "ENDMODULE (стр. 436)");
            Assert.That(Count(t, @"(?m)^MODULE\b"), Is.EqualTo(1));
            Assert.That(Count(t, @"(?m)^ENDMODULE\b"), Is.EqualTo(1));
            Assert.That(Count(t, @"(?m)^\s*PROC\b"), Is.EqualTo(2), "PROC = 2 (стр. 424, 430)");
            Assert.That(Count(t, @"(?m)^\s*ENDPROC\b"), Is.EqualTo(2), "ENDPROC = 2 (стр. 429, 435)");
            Assert.That(t, Does.Contain("  PROC trajectory_ptp()\n"));
            Assert.That(t, Does.Contain("  PROC trajectory_lin()\n"));
        }

        /// <summary>RAPID: объявления jointtarget/robtarget — по одному на каждую точку.</summary>
        [Test]
        public void Rapid_Declarations_CountMatchesPointCount()
        {
            foreach (int n in new[] { 2, 3, 5 })
            {
                string t = Build(mBuildRapid, n, VariantLabel);
                Assert.That(Count(t, "CONST jointtarget"), Is.EqualTo(n),
                    "CONST jointtarget = N (стр. 411/415), N=" + n);
                Assert.That(Count(t, "CONST robtarget"), Is.EqualTo(n),
                    "CONST robtarget = N (стр. 420), N=" + n);
            }
        }

        /// <summary>RAPID: счётчики — MoveAbsJ = N, MoveJ = 1, MoveL = N−1.</summary>
        [Test]
        public void Rapid_MotionCounters_MatchFormulas_ForTwoThreeFivePoints()
        {
            foreach (int n in new[] { 2, 3, 5 })
            {
                string t = Build(mBuildRapid, n, VariantLabel);
                Assert.That(Count(t, @"(?m)^\s*MoveAbsJ\b"), Is.EqualTo(n),
                    "MoveAbsJ = N (стр. 426/428), N=" + n);
                Assert.That(Count(t, @"(?m)^\s*MoveJ\b"), Is.EqualTo(1),
                    "MoveJ = 1 (стр. 432), N=" + n);
                Assert.That(Count(t, @"(?m)^\s*MoveL\b"), Is.EqualTo(n - 1),
                    "MoveL = N−1 (стр. 434), N=" + n);
            }
        }

        /// <summary>RAPID: скорости, зоны и инструмент зашиты в код — фиксируем фактические значения.</summary>
        [Test]
        public void Rapid_HardcodedSpeedsZonesAndTool_ArePresent()
        {
            string t = Build(mBuildRapid, 4, VariantLabel);

            Assert.That(t, Does.Contain("MoveAbsJ jStart, v500, z50, tool0;"), "первая PTP-точка (стр. 426)");
            Assert.That(t, Does.Contain(", v500, z10, tool0;"), "остальные PTP-точки (стр. 428)");
            Assert.That(t, Does.Contain("MoveJ p0, v500, z50, tool0;"), "первая LIN-точка (стр. 432)");
            Assert.That(t, Does.Contain(", v200, z5, tool0;"), "остальные LIN-точки (стр. 434)");
            Assert.That(t, Does.Contain("tool0"), "инструмент tool0");
        }

        /// <summary>
        /// RAPID: маркеры 9E9 (по 6 на каждую jointtarget/robtarget) — ЗАМЫСЕЛ формата RAPID
        /// (внешние оси и конфигурация не заданы), а не дефект: 12·N вхождений.
        /// </summary>
        [Test]
        public void Rapid_NineE9Sentinels_AreIntentionalDesign()
        {
            string t = Build(mBuildRapid, 3, VariantLabel);

            Assert.That(Count(t, "9E9"), Is.EqualTo(12 * 3),
                "6 маркеров на каждую из N jointtarget и N robtarget = 12·N (стр. 412/416/421)");
            Assert.That(t, Does.Contain("[9E9,9E9,9E9,9E9,9E9,9E9]"));
        }

        /// <summary>RAPID: одна точка — программа валидна (MoveAbsJ = 1, MoveJ = 1, MoveL = 0).</summary>
        [Test]
        public void Rapid_OnePoint_GeneratesProgramWithoutException()
        {
            string t = Build(mBuildRapid, 1, VariantLabel);

            Assert.That(Count(t, @"(?m)^\s*MoveAbsJ\b"), Is.EqualTo(1));
            Assert.That(Count(t, @"(?m)^\s*MoveJ\b"), Is.EqualTo(1));
            Assert.That(Count(t, @"(?m)^\s*MoveL\b"), Is.EqualTo(0), "MoveL = N−1 = 0");
            Assert.That(Count(t, "CONST jointtarget"), Is.EqualTo(1));
            Assert.That(Count(t, "CONST robtarget"), Is.EqualTo(1));
            Assert.That(t, Does.EndWith("ENDMODULE\n"));
        }

        /// <summary>RAPID: ноль точек — исключение из генератора (pts[0], стр. 411).</summary>
        [Test]
        public void Rapid_ZeroPoints_ThrowsArgumentOutOfRange()
        {
            Assert.That(() => Build(mBuildRapid, MakePointList(DefaultPoints(0)), VariantLabel),
                Throws.TypeOf<ArgumentOutOfRangeException>(),
                "BuildRapid обращается к pts[0] (стр. 411) — с пустым списком падает; " +
                "публичный Export отсекает такой вход (стр. 164–169)");
        }

        /// <summary>RAPID: только LF и никаких посторонних управляющих символов.</summary>
        [Test]
        public void Rapid_Text_UsesOnlyLineFeedAndHasNoControlCharacters()
        {
            string t = Build(mBuildRapid, 3, VariantLabel);

            Assert.That(t.Contains("\r"), Is.False, "перевод строки только \\n");
            Assert.That(Regex.IsMatch(t, @"[\x00-\x09\x0B-\x1F\x7F]"), Is.False);
        }

        /// <summary>RAPID: повторная генерация идентична (метка времени удалена).</summary>
        [Test]
        public void Rapid_RepeatedGeneration_IsIdenticalAfterTimestampRemoval()
        {
            string first = StripTimestamp(Build(mBuildRapid, 3, VariantLabel));
            string second = StripTimestamp(Build(mBuildRapid, 3, VariantLabel));

            Assert.That(second, Is.EqualTo(first),
                "единственная вариативность — DateTime.Now в строке «! создано:» (стр. 399/405)");
        }

        // ============================================================== общие проверки (все языки)

        /// <summary>Три языка на одних данных: тексты непусты, различны и начинаются с корневых ключевых слов.</summary>
        [Test]
        public void AllLanguages_ProduceDistinctNonEmptyProgramsWithRootKeywords()
        {
            string[] texts = BuildAllThree(MakePointList(DefaultPoints(3)), VariantLabel);

            foreach (string t in texts)
                Assert.That(string.IsNullOrEmpty(t), Is.False, "текст программы непуст");

            Assert.That(texts[0], Does.StartWith("&ACCESS RVP"), "KRL");
            Assert.That(texts[1], Does.StartWith("PROGRAM trajectory"), "KAREL");
            Assert.That(texts[2], Does.StartWith("MODULE trajectory"), "RAPID");
            Assert.That(texts[1], Is.Not.EqualTo(texts[0]), "KRL и KAREL различаются");
            Assert.That(texts[2], Is.Not.EqualTo(texts[0]), "KRL и RAPID различаются");
            Assert.That(texts[2], Is.Not.EqualTo(texts[1]), "KAREL и RAPID различаются");
        }

        /// <summary>
        /// Во всех языках не-ASCII (кириллица) встречается ТОЛЬКО в комментариях: строки кода — ASCII.
        /// Это фактическое свойство кода (комментарии-заголовки на русском, стр. 261/331/402),
        /// а не требование ТЗ «нет русских букв».
        /// </summary>
        [Test]
        public void AllLanguages_CodeLinesAreAsciiOnly()
        {
            string[] texts = BuildAllThree(MakePointList(DefaultPoints(3)), VariantLabel);

            foreach (string text in texts)
                foreach (string line in text.Split('\n'))
                    Assert.That(IsCodeLineAscii(line), Is.True,
                        "строка кода должна быть ASCII-only: «" + line + "»");
        }

        /// <summary>
        /// Числа в СТРОКАХ КОДА всех трёх языков пишутся с точкой (InvariantCulture), а не с запятой.
        /// Комментарии-заголовки из проверки исключены: там время и длина печатаются без
        /// InvariantCulture (стр. 266/336/407) — см. Krl_HeaderComment_NumbersFollowCurrentCulture.
        /// </summary>
        [Test]
        public void AllLanguages_UseInvariantDecimalPoint()
        {
            object[] pts =
            {
                NewPoint(new double[] { 12.5, -3.25, 0.0, 0.0, 0.0, 0.0 },
                    new Vector3(1.5f, -2.25f, 0.001f), Quaternion.identity, 0f),
                NewPoint(new double[] { -7.75, 45.125, 90.5, 0.0, 0.0, 0.0 },
                    new Vector3(-1.5f, 2.25f, -0.001f), Quaternion.Euler(0f, 0f, 45f), 1f)
            };
            string[] texts = BuildAllThree(MakePointList(pts), VariantLabel);

            foreach (string text in texts)
            {
                string code = CodeOnly(text);
                Assert.That(code, Does.Contain("1500.000"), "1.5 м → 1500.000 мм (Mm, стр. 467)");
                Assert.That(code, Does.Contain("12.500"), "12.5° → 12.500 (Deg, стр. 472)");
                Assert.That(code, Does.Contain("-3.250"), "−3.25° → -3.250");
                Assert.That(code, Does.Not.Contain("1500,000"), "запятая как десятичный разделитель недопустима");
                Assert.That(code, Does.Not.Contain("12,500"), "запятая как десятичный разделитель недопустима");
                Assert.That(code, Does.Not.Contain("-3,250"), "запятая как десятичный разделитель недопустима");
            }
        }

        /// <summary>При обычных данных в текстах нет NaN и Infinity.</summary>
        [Test]
        public void AllLanguages_ContainNoNaNOrInfinityForNormalData()
        {
            string[] texts = BuildAllThree(MakePointList(DefaultPoints(4)), VariantLabel);

            foreach (string t in texts)
            {
                Assert.That(t, Does.Not.Contain("NaN"));
                Assert.That(t, Does.Not.Contain("Infinity"));
                Assert.That(t, Does.Not.Contain("∞"));
            }
        }

        /// <summary>Private static Mm: метры → миллиметры, «0.000» с точкой; Deg: «0.000».</summary>
        [Test]
        public void Mm_ConvertsMetresToMillimetres_AndDeg_KeepsInvariantFormat()
        {
            MethodInfo mm = Formatter("Mm");     // стр. 467
            MethodInfo deg = Formatter("Deg");   // стр. 472

            Assert.That(mm.Invoke(null, new object[] { 1.5f }), Is.EqualTo("1500.000"));
            Assert.That(mm.Invoke(null, new object[] { -2f }), Is.EqualTo("-2000.000"));
            Assert.That(mm.Invoke(null, new object[] { 0.001f }), Is.EqualTo("1.000"));
            Assert.That(deg.Invoke(null, new object[] { 12.5f }), Is.EqualTo("12.500"));
            Assert.That(deg.Invoke(null, new object[] { 0f }), Is.EqualTo("0.000"));
        }

        /// <summary>Private static Joints: пары «A&lt;i&gt; значение» через «, » в формате 0.000.</summary>
        [Test]
        public void Joints_FormatsKrlAxisPairs()
        {
            MethodInfo joints = Formatter("Joints");   // стр. 302

            Assert.That(joints.Invoke(null, new object[] { new double[] { 1.0, -2.5, 0.0 } }),
                Is.EqualTo("A1 1.000, A2 -2.500, A3 0.000"));
        }

        /// <summary>Private static JointsKarel: не более 9 осей (KAREL JOINT_POS).</summary>
        [Test]
        public void JointsKarel_LimitsToListOfNineAxes()
        {
            MethodInfo jointsKarel = Formatter("JointsKarel");   // стр. 374
            var ten = new double[10];
            for (int i = 0; i < ten.Length; i++) ten[i] = i + 1;

            Assert.That(jointsKarel.Invoke(null, new object[] { ten }),
                Is.EqualTo("1.000, 2.000, 3.000, 4.000, 5.000, 6.000, 7.000, 8.000, 9.000"),
                "десятая ось отбрасывается (i < 9, стр. 377)");
        }

        /// <summary>Private static JointsRapid: всегда ровно 6 значений, недостающие — «0».</summary>
        [Test]
        public void JointsRapid_AlwaysEmitsSixAxes_PaddingWithZero()
        {
            MethodInfo jointsRapid = Formatter("JointsRapid");   // стр. 440

            Assert.That(jointsRapid.Invoke(null, new object[] { new double[] { 1, 2, 3 } }),
                Is.EqualTo("1.000,2.000,3.000,0,0,0"), "недостающие оси дописываются как «0» (стр. 446–448)");
            Assert.That(jointsRapid.Invoke(null, new object[] { new double[] { 1, 2, 3, 4, 5, 6, 7 } }),
                Is.EqualTo("1.000,2.000,3.000,4.000,5.000,6.000"), "седьмая ось отбрасывается (i < 6, стр. 443)");
        }

        /// <summary>Private static CartesianRapid: позиция в мм и кватернион «0.00000» (w, x, y, z).</summary>
        [Test]
        public void CartesianRapid_FormatsPositionAndQuaternion()
        {
            MethodInfo cartesianRapid = Formatter("CartesianRapid");   // стр. 454
            object point = NewPoint(new double[] { 0, 0, 0, 0, 0, 0 },
                new Vector3(1f, 2f, 3f), Quaternion.identity, 0f);

            Assert.That(cartesianRapid.Invoke(null, new object[] { point }),
                Is.EqualTo("1000.000,2000.000,3000.000],[1.00000,0.00000,0.00000,0.00000"),
                "фактический формат: «x,y,z],[qw,qx,qy,qz» (стр. 454–462)");
        }

        /// <summary>
        /// Private static Cartesian: «X мм, Y мм, Z мм, A град, B град, C град».
        /// Углы берутся из Quaternion.eulerAngles, поэтому сверяется структура и перевод в мм,
        /// а не конкретные значения углов.
        /// </summary>
        [Test]
        public void Cartesian_FormatsMillimetresAndEulerAngles()
        {
            MethodInfo cartesian = Formatter("Cartesian");   // стр. 315
            string text = (string)cartesian.Invoke(null,
                new object[] { new Vector3(1f, 2f, 3f), Quaternion.identity });

            Assert.That(Regex.IsMatch(text,
                    @"^X 1000\.000, Y 2000\.000, Z 3000\.000, A -?[\d.]+, B -?[\d.]+, C -?[\d.]+$"),
                Is.True, "фактический текст: «" + text + "» (стр. 315–320)");
            Assert.That(text, Does.Not.Contain("NaN"));
        }

        /// <summary>
        /// Экстремальные координаты (1e4 м, 1e-6 м, отрицательные, нулевые углы) дают конечный текст
        /// без NaN/Infinity и без экспоненциальной формы: форматы «0.000»/«0.00000» фиксированные.
        /// В RAPID маркеры «9E9» — замысел формата, поэтому перед проверкой экспоненты они удаляются.
        /// </summary>
        [Test]
        public void ExtremeCoordinates_ProduceFiniteTextWithoutNaNOrExponent()
        {
            object[] pts =
            {
                NewPoint(new double[] { 0, 0, 0, 0, 0, 0 },
                    new Vector3(1e4f, -1e4f, 1e-6f), Quaternion.identity, 0f),
                NewPoint(new double[] { 0, 0, 0, 0, 0, 0 },
                    new Vector3(-1e-6f, 0f, -1e4f), Quaternion.Euler(0f, 0f, 0f), 1f)
            };
            string[] texts = BuildAllThree(MakePointList(pts), VariantLabel);

            foreach (string t in texts)
            {
                Assert.That(t, Does.Contain("10000000.000"), "1e4 м = 10 000 000 мм без экспоненты");
                Assert.That(t, Does.Contain("-10000000.000"), "отрицательные значения со знаком");
                Assert.That(t, Does.Not.Contain("NaN"));
                Assert.That(t, Does.Not.Contain("Infinity"));

                string withoutSentinel = t.Replace("9E9", "");   // RAPID: 9E9 — замысел (стр. 412/416/421)
                Assert.That(Regex.IsMatch(withoutSentinel, @"\d[Ee][+-]?\d"), Is.False,
                    "чисел в экспоненциальной форме быть не должно (кроме маркеров 9E9 в RAPID)");
            }
        }

        /* ==========================================================================================
           НЕ ПОКРЫТО (и почему)
           ==========================================================================================

           1) Публичный путь Export / ExportSelected (стр. 146–205) — НЕ ПОКРЫТ: он пишет файл
              (File.WriteAllText, стр. 183) в каталог FolderPath (создаётся через
              FeatureStorage.EnsureDir, стр. 111/115), требует живой сцены (flow.Validator.Ready,
              стр. 155; flow — TrajectoryFlowController : MonoBehaviour) и PlayerPrefs. Сцена,
              MonoBehaviour и запись на диск запрещены для этих тестов; текстовые генераторы при
              этом покрыты полностью — рефлексия вызывает ровно те методы, что вызывает Export
              (стр. 173–178).
           2) Отказные ветви Export (null-кандидат, plan.Path.Length < 2, points.Count < 2) —
              НЕ ПОКРЫТЫ: они возвращают "" и текста не создают (стр. 148–169), а наблюдаемы только
              через сцену и PlayerPrefs. Поведение на вырожденном входе проверено уровнем ниже —
              тесты *_ZeroPoints_ThrowsArgumentOutOfRange.
           3) Sample / MakePoint (прореживание до maxPoints = 160, стр. 225–248) — НЕ ПОКРЫТЫ:
              вызывают KvCalibrationService.FlangeFrame и PoseValidator.TcpAt, то есть прямую задачу
              кинематики по живой иерархии Transform. Поэтому формулы счётчиков проверены как функции
              от N — числа точек, реально дошедшего до генератора; после прореживания оно может быть
              меньше plan.Path.Length (step = ceil(n / max(2, maxPoints)), стр. 229, плюс условное
              добавление последней точки, стр. 232).
           4) Extension / LanguageLabel / Language / CycleLanguage (стр. 55–95, 133–138) — НЕ ПОКРЫТЫ:
              значение зависит от PlayerPrefs и языка интерфейса (стр. 123; KvLoc.T), а установка
              языка требует записи в PlayerPrefs — побочного эффекта и недетерминизма.
           5) LastFile / LastLines / LastPreview как свойства (стр. 97–99) — НЕ ПОКРЫТЫ: заполняются
              в Export ПОСЛЕ записи файла (стр. 191–193); покрыта только арифметика
              LastLines = code.Split('\n').Length (Krl_LastLinesFormula_CountsOneExtraLine).
           6) FolderPath (стр. 103) — НЕ ПОКРЫТ: создаёт каталог на диске.
           7) KvExportTab (стр. 489–576) — НЕ ПОКРЫТ: UI (IKvWorkbenchTab), по ТЗ не тестируется.
           8) Валидация некорректных чисел — НЕ ПОКРЫТА: NaN/Infinity в точках код не проверяет
              (Mm/Deg напечатают «NaN»/«Infinity»); тесты фиксируют отсутствие таких значений при
              КОРРЕКТНЫХ данных, а не наличие защиты.
           9) FANUC LS/TP и команда CIRC — НЕ ПОКРЫТЫ тестом: соответствующих эмиттеров в коде нет
              вообще (есть только Karel, стр. 17/90), проверять нечего; расхождение с формулировкой
              ТЗ зафиксировано здесь.
          10) Хрупкость приватного API: тесты вызывают private-члены, поэтому при рефакторинге
              KvRobotExport.cs они сообщат «внутренний API изменился» через Assert.Fail (красный прогон), а не
              упадут. Это осознанный компромисс варианта «тестировать без правок продакшна».
           ========================================================================================== */
    }
}
