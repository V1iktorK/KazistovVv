using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Язык робота для экспорта траектории (ЭТАП 11 ТЗ).</summary>
    public enum KvRobotLanguage
    {
        /// <summary>KUKA KRL — файл trajectory.src.</summary>
        Krl = 0,
        /// <summary>FANUC KAREL — файл trajectory.kl.</summary>
        Karel = 1,
        /// <summary>ABB RAPID — файл trajectory.mod.</summary>
        Rapid = 2
    }

    /// <summary>
    /// ЭТАП 11 ТЗ: ЭКСПОРТ ТРАЕКТОРИИ В ЯЗЫКИ РОБОТОВ (KUKA KRL / FANUC KAREL / ABB RAPID).
    ///
    /// Это НЕ языки интерфейса: пользователь выбирает язык робота в настройках, а платформа
    /// выгружает КОД ДВИЖЕНИЯ по выбранной траектории в файл нужного формата
    /// (`trajectory.src`, `trajectory.kl`, `trajectory.mod`).
    ///
    /// Что попадает в файл:
    ///   • заголовок с датой, именем робота, числом осей и метриками траектории;
    ///   • ДВЕ процедуры — PTP (движение по суставам) и LIN (линейные участки с ориентацией):
    ///     это и есть «шаблоны базовых команд MOVE / LIN / PTP» из ТЗ;
    ///   • точки берутся из ФАКТИЧЕСКОЙ кинематики: угол каждого сустава, положение TCP и
    ///     ориентация фланца (для RAPID — кватернион, для KRL — углы A/B/C, для KAREL — XYZWPR).
    ///
    /// Единицы — как принято у производителя: KRL/KAREL — миллиметры и градусы,
    /// RAPID — миллиметры и кватернион. Призматические оси выводятся в миллиметрах.
    /// </summary>
    public class KvRobotExporter
    {
        public const string LanguagePrefsKey = "KazistovVv.Export.Language";

        public event Action<string> Message;

        /// <summary>Сколько точек максимум выгружается (файл не должен быть нечитаемым).</summary>
        public int maxPoints = 160;

        private TrajectoryFlowController flow;
        private KvRobotLanguage language = KvRobotLanguage.Krl;
        private bool loaded;
        private string lastFile = "";
        private int lastLines;
        private string lastPreview = "";

        public KvRobotLanguage Language
        {
            get { Load(); return language; }
            set
            {
                Load();
                language = value;
                PlayerPrefs.SetInt(LanguagePrefsKey, (int)language);
                PlayerPrefs.Save();
            }
        }

        public string LanguageLabel
        {
            get
            {
                switch (Language)
                {
                    case KvRobotLanguage.Karel:
                        return KvLocExtra2.T("export.lang.karel", "FANUC KAREL");
                    case KvRobotLanguage.Rapid:
                        return KvLocExtra2.T("export.lang.rapid", "ABB RAPID");
                    default:
                        return KvLocExtra2.T("export.lang.krl", "KUKA KRL");
                }
            }
        }

        /// <summary>Расширение файла по ТЗ.</summary>
        public string Extension
        {
            get
            {
                switch (Language)
                {
                    case KvRobotLanguage.Karel: return ".kl";
                    case KvRobotLanguage.Rapid: return ".mod";
                    default: return ".src";
                }
            }
        }

        public string LastFile { get { return lastFile; } }
        public int LastLines { get { return lastLines; } }
        public string LastPreview { get { return lastPreview; } }
        public TrajectoryFlowController Flow() { return flow; }

        /// <summary>Папка экспорта: «Документы\KazistovVv\robot_export» (резерв — данные приложения).</summary>
        public string FolderPath
        {
            get
            {
                try
                {
                    string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (!string.IsNullOrEmpty(docs))
                        return FeatureStorage.EnsureDir(Path.Combine(
                            Path.Combine(docs, "KazistovVv"), "robot_export"));
                }
                catch (Exception) { }
                return FeatureStorage.EnsureDir(Path.Combine(FeatureStorage.Root, "Export"));
            }
        }

        private void Load()
        {
            if (loaded) return;
            loaded = true;
            language = (KvRobotLanguage)Mathf.Clamp(PlayerPrefs.GetInt(LanguagePrefsKey, 0), 0, 2);
        }

        public void Bind(TrajectoryFlowController controller)
        {
            flow = controller;
            Load();
        }

        /// <summary>Переключить язык робота (циклически) — команда/кнопка настроек.</summary>
        public KvRobotLanguage CycleLanguage()
        {
            Language = (KvRobotLanguage)(((int)Language + 1) % 3);
            Report("язык робота для экспорта: " + LanguageLabel + " (" + Extension + ")");
            return Language;
        }

        // ================================================================== экспорт

        /// <summary>
        /// Экспортировать выбранную траекторию. Возвращает путь к файлу или "" при отказе
        /// (причина всегда пишется в журнал).
        /// </summary>
        public string Export(TrajectoryCandidate candidate, int index)
        {
            if (candidate == null || candidate.plan == null || candidate.plan.Path == null ||
                candidate.plan.Path.Length < 2)
            {
                Report(KvLocExtra2.T("export.noplan",
                    "Нет траектории: выберите точку (Z + ЛКМ) и подтвердите вариант (X + ЛКМ)"));
                return "";
            }
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report(KvLocExtra2.T("export.fail", "экспорт не выполнен") +
                       ": робот не определён");
                return "";
            }

            PoseValidator v = flow.Validator;
            List<KvExportPoint> points = Sample(v, candidate.plan);
            if (points.Count < 2)
            {
                Report(KvLocExtra2.T("export.fail", "экспорт не выполнен") +
                       ": точек траектории недостаточно");
                return "";
            }

            string robot = flow.Robot != null ? flow.Robot.robotName : "robot";
            string code;
            switch (Language)
            {
                case KvRobotLanguage.Karel: code = BuildKarel(points, robot, candidate, index); break;
                case KvRobotLanguage.Rapid: code = BuildRapid(points, robot, candidate, index); break;
                default: code = BuildKrl(points, robot, candidate, index); break;
            }

            string path = Path.Combine(FolderPath, "trajectory" + Extension);
            try
            {
                File.WriteAllText(path, code, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Report(KvLocExtra2.T("export.fail", "экспорт не выполнен") + ": " + e.Message);
                return "";
            }

            lastFile = path;
            lastLines = code.Split('\n').Length;
            lastPreview = FirstLines(code, 8);
            Report(KvLocExtra2.T("export.done", "файл создан") + ": " + path + " · " +
                   LanguageLabel + " · точек " + points.Count + " · строк " + lastLines);
            return path;
        }

        /// <summary>Экспорт выбранного варианта (кнопка/команда).</summary>
        public string ExportSelected()
        {
            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            return Export(candidate, index);
        }

        // ================================================================== ПРОВЕРКА ВЫГРУЖЕННОГО КОДА

        /// <summary>
        /// ЧЕГО ЖДЁТ ПРОГРАММА РОБОТА ОТ ВЫГРУЖЕННОГО ФАЙЛА (30.10.2026, аудит S43).
        ///
        /// ЗАЧЕМ. До этой правки форматтеры были закрыты (private), а тесты добирались до них
        /// через отражение и при любом переименовании падали в `Assert.Ignore` — прогон оставался
        /// зелёным, ничего не проверив (13 таких мест). Владельцу это давало ложное «всё проверено».
        ///
        /// ЭТО НЕ ВТОРАЯ РЕАЛИЗАЦИЯ ФОРМАТА. Здесь описаны только ПРАВИЛА, которым обязан
        /// удовлетворять уже сгенерированный текст: строки-параметры с числами, переводы строк,
        /// кодировка. Ошибка в самом построителе (например, потерянная скобка) по-прежнему
        /// ловится прямым сравнением текста в тестах.
        /// </summary>
        public static class CodeCheck
        {
            /// <summary>
            /// Числа, которые записаны ЧЕРЕЗ ЗАПЯТУЮ вместо точки.
            ///
            /// ПОЧЕМУ ЭТО ВАЖНО. `ToString("0.000")` без `CultureInfo.InvariantCulture` на машине
            /// с русской локалью даёт запятую — и KRL/RAPID/KAREL получают «1,234» вместо «1.234».
            ///
            /// ЧЕМ ОТЛИЧАЕТСЯ ОТ РАЗДЕЛИТЕЛЯ СПИСКА (исправлено 30.10.2026 по первому прогону
            /// этого теста). В RAPID координаты разделены запятой: `450.000,300.000,125.000`.
            /// Наивное правило «цифра, запятая, три цифры» считало дефектом любую такую пару.
            /// Отличаем по точке СЛЕВА: у дефектного литерала «12,345» точки слева нет (это одно
            /// число), у списка «450.000,300.000» — есть (это два числа).
            /// </summary>
            /// <param name="code">текст программы целиком</param>
            /// <returns>список найденных дефектных литералов (обычно пустой)</returns>
            public static List<string> DecimalCommaLiterals(string code)
            {
                var bad = new List<string>();
                if (string.IsNullOrEmpty(code)) return bad;

                foreach (Match m in Regex.Matches(code, @"(?<=\d),(?=\d{3}(?!\d))"))
                {
                    int from = m.Index;
                    bool dotOnTheLeft = false;
                    while (from > 0 && (char.IsDigit(code[from - 1]) || code[from - 1] == '-' || code[from - 1] == '.'))
                    {
                        from--;
                        if (code[from] == '.') dotOnTheLeft = true;
                    }
                    // Слева уже была точка — это разделитель двух чисел списка, а не десятичная запятая.
                    if (dotOnTheLeft) continue;

                    int to = m.Index + 1;
                    while (to < code.Length && (char.IsDigit(code[to]) || code[to] == '.')) to++;
                    string literal = code.Substring(from, to - from);
                    if (!bad.Contains(literal)) bad.Add(literal);
                }
                return bad;
            }

            /// <summary>В файле нет символов возврата каретки и прочих управляющих знаков.</summary>
            public static bool HasCleanLineEndings(string code)
            {
                if (string.IsNullOrEmpty(code)) return false;
                if (code.IndexOf('\r') >= 0) return false;
                foreach (char c in code)
                {
                    if (c == '\n' || c == '\t') continue;
                    if (char.IsControl(c)) return false;
                }
                return true;
            }

            /// <summary>
            /// КОЛИЧЕСТВО ДВИЖЕНИЙ КАЖДОГО ВИДА. Считается по готовому тексту, а не по входному
            /// списку точек: так проверка видит потерянную или лишнюю строку движения.
            /// </summary>
            public static int CountLines(string code, string prefix)
            {
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(prefix)) return 0;
                int count = 0;
                foreach (string line in code.Split('\n'))
                {
                    // РАЗДЕЛИТЕЛЬ В КОНЦЕ ПРЕФИКСА = «слово целиком» (найдено прогоном 30.10.2026).
                    //
                    // `CountLines(code, "  MOVE TO jStart\n")` должен считать строки, которые
                    // содержат «MOVE TO jStart» И НА ЭТОМ ЗАКАНЧИВАЮТСЯ. Без этой ветки префикс
                    // со `\n` не совпал бы никогда: в строке перевода строки нет.
                    string body = line.TrimEnd('\r');
                    bool wholeLine = prefix.EndsWith("\n", StringComparison.Ordinal);
                    string core = wholeLine ? prefix.Substring(0, prefix.Length - 1) : prefix;

                    // ПОИСК ПО ВСЕЙ СТРОКЕ, А НЕ ТОЛЬКО С НАЧАЛА (исправлено прогоном 30.10.2026).
                    //
                    // ЖИВОЙ ПРИМЕР ДЕФЕКТА: `CountLines(code, "  MOVE TO JOINT_POS(")` возвращал 0,
                    // хотя строки `  MOVE TO JOINT_POS(...)` в тексте есть. Причина была не в
                    // шаблоне: проверка «строка начинается с префикса» требует, чтобы отступ в
                    // шаблоне совпал с отступом в тексте символ в символ, а это хрупко — любой
                    // лишний пробел в шаблоне молча превращал счётчик в ноль. Теперь подстрока
                    // ищется в строке целиком, а от ложных совпадений защищает проверка границы
                    // слова (ниже).
                    if (body.IndexOf(core, StringComparison.Ordinal) < 0) continue;

                    // «СЛОВО ЦЕЛИКОМ» — ТОЛЬКО КОГДА ПРЕФИКС И ЕСТЬ СЛОВО.
                    //
                    // Здесь же был найден собственный дефект проверки (прогон 30.10.2026): префикс
                    // «  MOVE TO JOINT_POS(» оканчивается на «(», и правило «за префиксом не идёт
                    // буква» отбрасывало КАЖДУЮ такую строку — вызов функции всегда продолжается
                    // буквой или цифрой. Теперь проверка «за префиксом не идёт буква» включается
                    // ТОЛЬКО если сам префикс оканчивается буквой/цифрой/подчёркиванием, то есть
                    // является словом. Для «…JOINT_POS(» и «LIN » она не нужна и не мешает.
                    char last = core.Length > 0 ? core[core.Length - 1] : ' ';
                    if (char.IsLetterOrDigit(last) || last == '_')
                    {
                        int at = body.IndexOf(core, StringComparison.Ordinal) + core.Length;
                        if (at < body.Length && (char.IsLetterOrDigit(body[at]) || body[at] == '_'))
                            continue;
                    }
                    count++;
                }
                return count;
            }

            /// <summary>Сбалансированы ли фигурные скобки (KRL: `{X 1.0, …}`, KAREL не использует).</summary>
            public static bool BracesBalanced(string code)
            {
                if (string.IsNullOrEmpty(code)) return false;
                int depth = 0;
                foreach (char c in code)
                {
                    if (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth < 0) return false;
                    }
                }
                return depth == 0;
            }

            /// <summary>Сбалансированы ли квадратные скобки (RAPID: `[[...],[...],[...],[...]]`).</summary>
            public static bool BracketsBalanced(string code)
            {
                if (string.IsNullOrEmpty(code)) return false;
                int depth = 0;
                foreach (char c in code)
                {
                    if (c == '[') depth++;
                    else if (c == ']')
                    {
                        depth--;
                        if (depth < 0) return false;
                    }
                }
                return depth == 0;
            }
        }

        /// <summary>
        /// ТЕКСТ ПРОГРАММЫ РОБОТА ДЛЯ ГОТОВЫХ ТОЧЕК (30.10.2026, аудит S43).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНО ОТ `Export`. `Export` пишет файл на диск и требует живой сцены
        /// (`flow.Validator.Ready`), поэтому весь формат проверялся тестами через отражение, а при
        /// любом переименовании молча выключался (`Assert.Ignore`). Этот метод делает ровно то, что
        /// и раньше делала ветка `switch` внутри `Export`, — но без файла и без сцены, поэтому
        /// формат проверяется напрямую, публичным API.
        ///
        /// ВОЗВРАЩАЕТСЯ ЗАКОНЧЕННЫЙ ТЕКСТ: у KRL последняя строка — `END`, у KAREL — `END trajectory`,
        /// у RAPID — `ENDMODULE`. Кириллица встречается ТОЛЬКО в комментариях.
        /// </summary>
        /// <param name="jointsDegrees">позы по суставам (градусы, призматические оси — миллиметры)</param>
        /// <param name="tcpMeters">положение TCP в метрах</param>
        /// <param name="flange">ориентация фланца</param>
        /// <param name="robotName">имя робота для шапки</param>
        /// <param name="language">язык программы</param>
        /// <param name="variantIndex">номер варианта (для шапки)</param>
        /// <param name="variantLabel">подпись варианта</param>
        /// <param name="timeSeconds">время траектории (для шапки)</param>
        /// <param name="lengthMeters">длина траектории (для шапки)</param>
        public string ProgramText(double[][] jointsDegrees, Vector3[] tcpMeters, Quaternion[] flange,
            string robotName, KvRobotLanguage language, int variantIndex, string variantLabel,
            double timeSeconds, double lengthMeters)
        {
            if (jointsDegrees == null) throw new ArgumentNullException("jointsDegrees");
            if (tcpMeters == null) throw new ArgumentNullException("tcpMeters");
            if (flange == null) throw new ArgumentNullException("flange");
            if (jointsDegrees.Length == 0 || jointsDegrees.Length != tcpMeters.Length ||
                jointsDegrees.Length != flange.Length)
            {
                throw new ArgumentException("Количество поз, точек TCP и ориентаций обязано совпадать и быть > 0");
            }

            var points = new List<KvExportPoint>(jointsDegrees.Length);
            for (int i = 0; i < jointsDegrees.Length; i++)
            {
                points.Add(new KvExportPoint
                {
                    q = jointsDegrees[i],
                    tcp = tcpMeters[i],
                    flange = flange[i],
                    time = 0.0f
                });
            }

            // Подпись варианта в шапке печатается как есть: это комментарий, парсер робота его
            // не читает, а оператору нужен ровно тот текст, который виден в интерфейсе.
            TrajectoryCandidate candidate = CandidateFor(variantLabel, timeSeconds, lengthMeters);
            string robot = string.IsNullOrEmpty(robotName) ? "robot" : robotName;

            switch (language)
            {
                case KvRobotLanguage.Karel: return BuildKarel(points, robot, candidate, variantIndex);
                case KvRobotLanguage.Rapid: return BuildRapid(points, robot, candidate, variantIndex);
                default: return BuildKrl(points, robot, candidate, variantIndex);
            }
        }

        /// <summary>
        /// Слепок варианта для шапки. `TrajectoryCandidate` объявлен в `TrajectoryCore` рядом с
        /// полем `plan`; здесь заполняются только три поля, которые читают генераторы (label,
        /// timeS, lengthM), — сам план не нужен вовсе.
        /// </summary>
        private static TrajectoryCandidate CandidateFor(string label, double timeSeconds, double lengthMeters)
        {
            return new TrajectoryCandidate
            {
                label = label,
                timeS = (float)timeSeconds,
                lengthM = (float)lengthMeters
            };
        }

        private static string FirstLines(string code, int count)
        {
            string[] lines = code.Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length && i < count; i++) sb.Append(lines[i]).Append('\n');
            return sb.ToString();
        }

        /// <summary>Одна точка выгрузки: суставы, TCP и ориентация фланца.</summary>
        private class KvExportPoint
        {
            public double[] q;
            public Vector3 tcp;
            public Quaternion flange;
            public float time;
        }

        /// <summary>Прореживание плана до `maxPoints` точек (равномерно по сэмплам).</summary>
        private List<KvExportPoint> Sample(PoseValidator v, PlannedTrajectory plan)
        {
            var list = new List<KvExportPoint>();
            int n = plan.Path.Length;
            int step = Mathf.Max(1, Mathf.CeilToInt(n / (float)Mathf.Max(2, maxPoints)));

            for (int i = 0; i < n; i += step) list.Add(MakePoint(v, plan, i));
            if ((n - 1) % step != 0) list.Add(MakePoint(v, plan, n - 1));
            return list;
        }

        private static KvExportPoint MakePoint(PoseValidator v, PlannedTrajectory plan, int i)
        {
            Vector3 pos;
            Quaternion rot;
            KvCalibrationService.FlangeFrame(v, plan.Path[i], out pos, out rot);
            return new KvExportPoint
            {
                q = plan.Path[i],
                tcp = v.TcpAt(plan.Path[i]),
                flange = rot,
                time = plan.Times != null && i < plan.Times.Length ? plan.Times[i] : 0f
            };
        }

        // ================================================================== KUKA KRL

        private string BuildKrl(List<KvExportPoint> pts, string robot, TrajectoryCandidate candidate,
            int index)
        {
            var sb = new StringBuilder();
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            sb.Append("&ACCESS RVP\n");
            sb.Append("&REL 1\n");
            sb.Append("DEF trajectory ( )\n");
            sb.Append("; ================================================================\n");
            sb.Append("; KazistovVv · экспорт траектории в KUKA KRL\n");
            sb.Append("; робот: ").Append(robot).Append(" · вариант №").Append(index + 1)
              .Append(" · точек: ").Append(pts.Count).Append('\n');
            sb.Append("; создано: ").Append(stamp).Append('\n');
            sb.Append("; траектория: ").Append(candidate.label).Append(" · время ")
              .Append(candidate.timeS.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(" с · длина ")
              .Append(candidate.lengthM.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(" м\n");
            sb.Append("; ВНИМАНИЕ: номера инструмента и базы (TOOL_DATA/BASE_DATA) задайте\n");
            sb.Append("; под свою ячейку — здесь используется 1.\n");
            sb.Append("; ================================================================\n");
            sb.Append("  DECL E6AXIS qStart\n");
            sb.Append("  DECL E6POS  pStart\n");
            sb.Append("  INI\n");
            sb.Append("  $TOOL = TOOL_DATA[1]\n");
            sb.Append("  $BASE = BASE_DATA[1]\n\n");

            // --- PTP: движение по суставам
            sb.Append("  ; --- PTP: движение по суставам (MOVE) ---\n");
            sb.Append("  qStart = {").Append(Joints(pts[0].q)).Append("}\n");
            sb.Append("  PTP qStart\n");
            for (int i = 1; i < pts.Count; i++)
            {
                sb.Append("  PTP {").Append(Joints(pts[i].q)).Append("} C_PTP\n");
            }
            sb.Append('\n');

            // --- LIN: линейные участки с ориентацией
            sb.Append("  ; --- LIN: линейное движение с ориентацией (LIN) ---\n");
            sb.Append("  pStart = {").Append(Cartesian(pts[0].tcp, pts[0].flange)).Append("}\n");
            sb.Append("  PTP pStart\n");
            // ПОСЛЕДНЯЯ ТОЧКА НЕ ПОВТОРЯЕТСЯ (исправлено 30.10.2026, аудит S43).
            //
            // РАНЬШЕ цикл печатал LIN с C_DIS для ВСЕХ точек со второй включительно, а после него
            // печатался ещё один LIN для последней точки — БЕЗ C_DIS. Последняя точка выгружалась
            // ДВА РАЗА подряд: робот получал приказ прийти в неё с проскакиванием (C_DIS) и сразу
            // же встать в неё точно. На стойке это лишний блок и лишнее движение, а на быстрых
            // участках — рывок в самой важной точке (финальной).
            //
            // Теперь: все точки КРОМЕ последней — с C_DIS, последняя — точно, без C_DIS.
            for (int i = 1; i < pts.Count - 1; i++)
            {
                sb.Append("  LIN {").Append(Cartesian(pts[i].tcp, pts[i].flange))
                  .Append("} C_DIS\n");
            }
            sb.Append("  LIN {").Append(Cartesian(pts[pts.Count - 1].tcp, pts[pts.Count - 1].flange))
              .Append("}\n");
            sb.Append("END\n");
            return sb.ToString();
        }

        private static string Joints(double[] q)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < q.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("A").Append(i + 1).Append(' ')
                  .Append(q[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>KRL: миллиметры + углы A/B/C (градусы) из ориентации фланца.</summary>
        private static string Cartesian(Vector3 tcp, Quaternion flange)
        {
            Vector3 euler = flange.eulerAngles;
            return "X " + Mm(tcp.x) + ", Y " + Mm(tcp.y) + ", Z " + Mm(tcp.z) +
                   ", A " + Deg(euler.x) + ", B " + Deg(euler.y) + ", C " + Deg(euler.z);
        }

        // ================================================================== FANUC KAREL

        private string BuildKarel(List<KvExportPoint> pts, string robot, TrajectoryCandidate candidate,
            int index)
        {
            var sb = new StringBuilder();
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            sb.Append("PROGRAM trajectory\n");
            sb.Append("-- ================================================================\n");
            sb.Append("-- KazistovVv · экспорт траектории в FANUC KAREL\n");
            sb.Append("-- робот: ").Append(robot).Append(" · вариант №").Append(index + 1)
              .Append(" · точек: ").Append(pts.Count).Append('\n');
            sb.Append("-- создано: ").Append(stamp).Append('\n');
            sb.Append("-- траектория: ").Append(candidate.label).Append(" · время ")
              .Append(candidate.timeS.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(" s · длина ")
              .Append(candidate.lengthM.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(" m\n");
            sb.Append("-- ВНИМАНИЕ: номера UTOOL/UTFRAME задайте под свою ячейку.\n");
            sb.Append("-- ================================================================\n");
            sb.Append("VAR\n");
            sb.Append("  jStart : JOINT_POS\n");
            sb.Append("  pStart : XYZWPR\n");
            sb.Append("  vSpeed : INTEGER\n");
            sb.Append("BEGIN\n");
            sb.Append("  -- PTP: движение по суставам (MOVE TO JOINT_POS)\n");
            for (int i = 0; i < pts.Count; i++)
            {
                if (i == 0)
                {
                    sb.Append("  jStart = JOINT_POS(").Append(JointsKarel(pts[i].q)).Append(")\n");
                    sb.Append("  MOVE TO jStart\n");
                }
                else
                {
                    sb.Append("  JMP L").Append(i).Append("  -- через точку ").Append(i)
                      .Append(" (суставы)\n");
                    sb.Append("  L").Append(i).Append(" :\n");
                    sb.Append("  MOVE TO JOINT_POS(").Append(JointsKarel(pts[i].q)).Append(")\n");
                }
            }
            sb.Append('\n');
            sb.Append("  -- LIN: линейное движение с ориентацией (MOVE TO XYZWPR)\n");
            sb.Append("  pStart = POSITION(").Append(CartesianKarel(pts[0])).Append(")\n");
            sb.Append("  MOVE TO pStart\n");
            // ПОСЛЕДНЯЯ ТОЧКА — ОДИН РАЗ (исправлено 30.10.2026, аудит S43).
            //
            // РАНЬШЕ цикл печатал `MOVE TO POSITION(...)` для всех точек со второй, а перед ним
            // для ПЕРВОЙ точки уже был `MOVE TO pStart` — первая точка выгружалась дважды
            // (жёстко прописанная позиция и та же позиция через вызов функции). Робот получал
            // лишний блок в начале программы. Теперь первая точка — только `pStart`.
            for (int i = 1; i < pts.Count; i++)
            {
                sb.Append("  MOVE TO POSITION(").Append(CartesianKarel(pts[i])).Append(")\n");
            }
            sb.Append("END trajectory\n");
            return sb.ToString();
        }

        /// <summary>KAREL: JOINT_POS принимает градусы; призматические оси — в миллиметрах.</summary>
        private static string JointsKarel(double[] q)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < q.Length && i < 9; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(q[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>KAREL: POSITION(x, y, z, w, p, r) — миллиметры и градусы.</summary>
        private static string CartesianKarel(KvExportPoint p)
        {
            Vector3 euler = p.flange.eulerAngles;
            return Mm(p.tcp.x) + ", " + Mm(p.tcp.y) + ", " + Mm(p.tcp.z) + ", " +
                   Deg(euler.z) + ", " + Deg(euler.y) + ", " + Deg(euler.x);
        }

        // ================================================================== ABB RAPID

        private string BuildRapid(List<KvExportPoint> pts, string robot, TrajectoryCandidate candidate,
            int index)
        {
            var sb = new StringBuilder();
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            sb.Append("MODULE trajectory\n");
            sb.Append("  ! ================================================================\n");
            sb.Append("  ! KazistovVv · экспорт траектории в ABB RAPID\n");
            sb.Append("  ! робот: ").Append(robot).Append(" · вариант №").Append(index + 1)
              .Append(" · точек: ").Append(pts.Count).Append('\n');
            sb.Append("  ! создано: ").Append(stamp).Append('\n');
            sb.Append("  ! траектория: ").Append(candidate.label).Append(" · время ")
              .Append(candidate.timeS.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(" s · длина ")
              .Append(candidate.lengthM.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(" m\n");
            sb.Append("  ! ВНИМАНИЕ: tool0/wobj0 задайте под свою ячейку.\n");
            sb.Append("  ! ================================================================\n");
            sb.Append("  CONST jointtarget jStart := [[").Append(JointsRapid(pts[0].q))
              .Append("],[9E9,9E9,9E9,9E9,9E9,9E9]];\n");
            for (int i = 1; i < pts.Count; i++)
            {
                sb.Append("  CONST jointtarget j").Append(i).Append(" := [[")
                  .Append(JointsRapid(pts[i].q)).Append("],[9E9,9E9,9E9,9E9,9E9,9E9]];\n");
            }
            for (int i = 0; i < pts.Count; i++)
            {
                sb.Append("  CONST robtarget p").Append(i).Append(" := [[")
                  .Append(CartesianRapid(pts[i])).Append("],[0,0,0,0],[0,0,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];\n");
            }
            sb.Append('\n');
            sb.Append("  PROC trajectory_ptp()\n");
            sb.Append("    ! PTP: движение по суставам\n");
            sb.Append("    MoveAbsJ jStart, v500, z50, tool0;\n");
            for (int i = 1; i < pts.Count; i++)
                sb.Append("    MoveAbsJ j").Append(i).Append(", v500, z10, tool0;\n");
            sb.Append("  ENDPROC\n\n");
            sb.Append("  PROC trajectory_lin()\n");
            sb.Append("    ! LIN: линейные участки с ориентацией\n");
            sb.Append("    MoveJ p0, v500, z50, tool0;\n");
            for (int i = 1; i < pts.Count; i++)
                sb.Append("    MoveL p").Append(i).Append(", v200, z5, tool0;\n");
            sb.Append("  ENDPROC\n");
            sb.Append("ENDMODULE\n");
            return sb.ToString();
        }

        private static string JointsRapid(double[] q)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(i < q.Length
                    ? q[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                    : "0");
            }
            return sb.ToString();
        }

        /// <summary>RAPID: robtarget = [[x,y,z],[q1,q2,q3,q4],[cfx,cfy,cfz,cf4],…], мм и кватернион.</summary>
        private static string CartesianRapid(KvExportPoint p)
        {
            Quaternion q = p.flange;
            // RAPID использует кватернион (q1..q4) в порядке (w, x, y, z).
            return Mm(p.tcp.x) + "," + Mm(p.tcp.y) + "," + Mm(p.tcp.z) + "],[" +
                   q.w.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   q.x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   q.y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   q.z.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ================================================================== мелочи

        private static string Mm(float meters)
        {
            return (meters * 1000f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Deg(float degrees)
        {
            return degrees.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[RobotExport] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «ЭКСПОРТ В ЯЗЫК РОБОТА» (ЭТАП 11 ТЗ): выбор языка, папка экспорта,
    /// кнопка выгрузки и предпросмотр начала файла.
    /// </summary>
    public class KvExportTab : IKvWorkbenchTab
    {
        private readonly KvRobotExporter exporter;
        private KvSegmented languageSegment;

        public KvExportTab(KvRobotExporter service)
        {
            exporter = service;
        }

        public string Key { get { return "export"; } }
        public string Title { get { return KvLocExtra2.T("export.title", "Экспорт траектории"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra2.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (exporter == null || kit == null) return;

            kit.Section(Title);
            string[] langs =
            {
                T("export.lang.krl", "KUKA KRL"),
                T("export.lang.karel", "FANUC KAREL"),
                T("export.lang.rapid", "ABB RAPID")
            };
            languageSegment = kit.Segmented(T("export.language", "Язык робота"), langs,
                (int)exporter.Language, delegate (int i)
                {
                    exporter.Language = (KvRobotLanguage)Mathf.Clamp(i, 0, 2);
                });

            kit.Info(delegate
            {
                return T("export.language", "Язык робота") + ": " + exporter.LanguageLabel +
                       " · trajectory" + exporter.Extension;
            }, KvTheme.Accent);
            kit.Info(delegate { return T("export.folder", "Папка экспорта") + ": " + exporter.FolderPath; },
                KvTheme.TextDim);
            kit.Info(delegate
            {
                TrajectoryFlowController f = exporter.Flow();
                if (f == null || f.State == null || f.State.candidates.Count == 0)
                    return T("export.noplan",
                        "Нет траектории: выберите точку (Z + ЛКМ) и подтвердите вариант (X + ЛКМ)");
                int index = f.State.selectedTrajectory >= 0 ? f.State.selectedTrajectory : 0;
                TrajectoryCandidate c = index < f.State.candidates.Count
                    ? f.State.candidates[index] : null;
                return c == null ? "—" : T("wb.variant", "Вариант") + " " + KvVariantKit.Label(c, index);
            }, KvTheme.TextMain);

            kit.Buttons(new[] { T("export.button", "Экспорт траектории") },
                new Action[] { delegate { exporter.ExportSelected(); } });

            kit.Divider();
            kit.Info(delegate
            {
                return string.IsNullOrEmpty(exporter.LastFile)
                    ? T("export.info", "Генерируется код движения по выбранной траектории")
                    : T("export.done", "файл создан") + ": " + exporter.LastFile + " · строк " +
                      exporter.LastLines;
            }, KvTheme.Ok);

            foreach (string line in (exporter.LastPreview ?? "").Split('\n'))
            {
                string captured = line;
                if (string.IsNullOrEmpty(captured)) continue;
                kit.Info(delegate { return captured; }, KvTheme.TextDim);
            }

            kit.Divider();
            kit.Note(T("export.info",
                "Генерируется код движения по выбранной траектории: PTP (по суставам) и LIN " +
                "(линейные участки с ориентацией из прямой задачи). Файлы: trajectory.src (KUKA), " +
                "trajectory.kl (FANUC), trajectory.mod (ABB)."), KvTheme.TextDim);
        }

        public void Tick() { }

        public void Refresh()
        {
            if (languageSegment != null && languageSegment.Index != (int)exporter.Language)
                languageSegment.Set((int)exporter.Language);
        }
    }
}
