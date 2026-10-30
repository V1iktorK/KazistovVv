using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    // ==========================================================================================
    // ЭТАП 26 ТЗ: ИНТЕРФЕЙС СКРИПТОВ (МАКРОСЫ)
    // ==========================================================================================

    /// <summary>Вид оператора во внутреннем языке макросов.</summary>
    public enum KvStmtKind { Assign, Call, Repeat, If, While }

    /// <summary>Узел выражения (число, строка, переменная, вызов, арифметика, сравнение).</summary>
    public class KvExpr
    {
        public string kind = "";     // num, str, var, call, bin, not
        public double number;
        public string text = "";
        public string op = "";
        public KvExpr a, b;
        public List<KvExpr> args;
        public int line;
    }

    /// <summary>Оператор программы макроса.</summary>
    public class KvStmt
    {
        public KvStmtKind kind;
        public string name = "";          // имя переменной/функции
        public KvExpr value;              // правая часть (для Assign — выражение, для Call — null)
        public List<KvExpr> args = new List<KvExpr>();
        public KvExpr condition;          // для Repeat/If/While
        public List<KvStmt> body = new List<KvStmt>();
        public List<KvStmt> elseBody = new List<KvStmt>();
        public int line;
    }

    /// <summary>Ошибка разбора или выполнения макроса (с номером строки).</summary>
    public class KvScriptError
    {
        public int line;
        public string message = "";
        public KvScriptError(int line, string message) { this.line = line; this.message = message; }
        public override string ToString()
        {
            return "строка " + line + ": " + message;
        }
    }

    /// <summary>
    /// РАЗБОР МАКРОСА. Язык намеренно маленький и «питоно-подобный»: строки, отступы не важны,
    /// блоки в фигурных скобках:
    /// <code>
    /// скорость = 0.4
    /// repeat 3 {
    ///   в_точку(0.4, 0.2, 0.7)
    ///   ждать(0.5)
    /// }
    /// if высота_инструмента() &gt; 0.5 { печать("высоко") }
    /// </code>
    /// Это НЕ Python и НЕ Lua: отдельного интерпретатора с доступом к файлам и системе здесь нет,
    /// поэтому макрос физически не может ничего сломать вне списка разрешённых команд (см.
    /// <see cref="KvScriptEngine.Commands"/>). Такой «белый список» и есть песочница.
    /// </summary>
    public class KvScriptParser
    {
        private readonly string[] lines;
        private int index;
        private string error;

        private KvScriptParser(string source)
        {
            lines = (source ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        /// <summary>Разобрать текст. Возвращает программу или null (причина — в `out error`).</summary>
        public static List<KvStmt> Parse(string source, out string error)
        {
            KvScriptParser p = new KvScriptParser(source);
            List<KvStmt> program = p.Block(false);
            error = p.error;
            return error == null ? program : null;
        }

        private List<KvStmt> Block(bool insideBraces)
        {
            List<KvStmt> result = new List<KvStmt>();
            while (index < lines.Length)
            {
                string raw = Strip(lines[index]);
                int lineNo = index + 1;
                index++;
                if (raw.Length == 0) continue;
                if (raw == "}") { if (insideBraces) return result; Fail(lineNo, "лишняя закрывающая скобка"); return result; }

                KvStmt stmt = Statement(raw, lineNo);
                if (stmt == null) return result;

                if (stmt.kind == KvStmtKind.Repeat || stmt.kind == KvStmtKind.If ||
                    stmt.kind == KvStmtKind.While)
                {
                    // Тело блока: либо в той же строке после «{», либо в следующих строках до «}».
                    string tail = AfterBrace(raw);
                    if (tail != null && tail.Length > 0)
                    {
                        stmt.body = ParseInline(tail, lineNo);
                        if (error != null) return result;
                    }
                    else
                    {
                        stmt.body = Block(true);
                        if (error != null) return result;
                    }
                    if (stmt.kind == KvStmtKind.If)
                    {
                        while (index < lines.Length)
                        {
                            string next = Strip(lines[index]);
                            if (next.Length == 0) { index++; continue; }
                            if (!next.StartsWith("else") && !next.StartsWith("иначе")) break;
                            index++;
                            string elseTail = AfterBrace(next);
                            stmt.elseBody = elseTail != null && elseTail.Length > 0
                                ? ParseInline(elseTail, lineNo)
                                : Block(true);
                            if (error != null) return result;
                            break;
                        }
                    }
                }
                result.Add(stmt);
            }
            if (insideBraces) Fail(lines.Length, "не закрыт блок «{»");
            return result;
        }

        private List<KvStmt> ParseInline(string text, int lineNo)
        {
            List<KvStmt> body = new List<KvStmt>();
            string inner = text.Trim();
            if (inner.EndsWith("}")) inner = inner.Substring(0, inner.Length - 1).Trim();
            string[] parts = inner.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = Strip(parts[i]);
                if (part.Length == 0) continue;
                KvStmt s = Statement(part, lineNo);
                if (s == null) return body;
                body.Add(s);
            }
            return body;
        }

        private KvStmt Statement(string text, int lineNo)
        {
            string lower = text.ToLowerInvariant();
            if (lower.StartsWith("repeat ") || lower.StartsWith("повторить ") || lower.StartsWith("повтор "))
            {
                int brace = text.IndexOf('{');
                string exprText = brace >= 0 ? text.Substring(text.IndexOf(' ') + 1, brace - text.IndexOf(' ') - 1) : text.Substring(text.IndexOf(' ') + 1);
                return new KvStmt
                {
                    kind = KvStmtKind.Repeat,
                    line = lineNo,
                    condition = Expr(exprText, lineNo, text)
                };
            }
            if (lower.StartsWith("while ") || lower.StartsWith("пока "))
            {
                int brace = text.IndexOf('{');
                string exprText = brace >= 0 ? text.Substring(text.IndexOf(' ') + 1, brace - text.IndexOf(' ') - 1) : text.Substring(text.IndexOf(' ') + 1);
                return new KvStmt
                {
                    kind = KvStmtKind.While,
                    line = lineNo,
                    condition = Expr(exprText, lineNo, text)
                };
            }
            if (lower.StartsWith("if ") || lower.StartsWith("если "))
            {
                int brace = text.IndexOf('{');
                string exprText = brace >= 0 ? text.Substring(text.IndexOf(' ') + 1, brace - text.IndexOf(' ') - 1) : text.Substring(text.IndexOf(' ') + 1);
                return new KvStmt
                {
                    kind = KvStmtKind.If,
                    line = lineNo,
                    condition = Expr(exprText, lineNo, text)
                };
            }

            int eq = FindAssign(text);
            if (eq > 0)
            {
                return new KvStmt
                {
                    kind = KvStmtKind.Assign,
                    line = lineNo,
                    name = text.Substring(0, eq).Trim(),
                    value = Expr(text.Substring(eq + 1), lineNo, text)
                };
            }

            // Вызов команды: имя(аргументы)
            string head = text;
            string argsText = "";
            int open = text.IndexOf('(');
            if (open >= 0)
            {
                int close = text.LastIndexOf(')');
                if (close < open) { Fail(lineNo, "не закрыта скобка вызова"); return null; }
                head = text.Substring(0, open);
                argsText = text.Substring(open + 1, close - open - 1);
            }
            KvStmt call = new KvStmt
            {
                kind = KvStmtKind.Call,
                line = lineNo,
                name = head.Trim().ToLowerInvariant()
            };
            if (call.name.Length == 0) { Fail(lineNo, "пустое имя команды"); return null; }

            string[] parts = SplitArgs(argsText);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Trim().Length == 0) continue;
                KvExpr e = Expr(parts[i], lineNo, text);
                if (e == null) return null;
                call.args.Add(e);
            }
            return call;
        }

        private static int FindAssign(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '=' && (i == 0 || text[i - 1] != '=') &&
                    (i + 1 >= text.Length || text[i + 1] != '=')) return i;
                if (text[i] == '(') return -1;      // внутри вызова присваивания нет
            }
            return -1;
        }

        private KvExpr Expr(string text, int lineNo, string full)
        {
            KvExpr e = ParseOr(text.Trim(), lineNo);
            if (e == null) Fail(lineNo, "не разобрано выражение: «" + full.Trim() + "»");
            return e;
        }

        private KvExpr ParseOr(string s, int lineNo)
        {
            KvExpr left = ParseAnd(s, lineNo);
            return left;
        }

        private KvExpr ParseAnd(string s, int lineNo)
        {
            int idx = TopLevelIndex(s, "==");
            if (idx < 0) idx = TopLevelIndex(s, "!=");
            if (idx < 0) idx = TopLevelIndex(s, ">=");
            if (idx < 0) idx = TopLevelIndex(s, "<=");
            if (idx < 0) idx = TopLevelIndex(s, ">");
            if (idx < 0) idx = TopLevelIndex(s, "<");
            if (idx >= 0)
            {
                string op = s.Substring(idx, s.Length > idx + 1 && (s[idx + 1] == '=') ? 2 : 1);
                return new KvExpr
                {
                    kind = "bin",
                    op = op,
                    line = lineNo,
                    a = ParseAnd(s.Substring(0, idx), lineNo),
                    b = ParseAnd(s.Substring(idx + op.Length), lineNo)
                };
            }
            return ParseSum(s, lineNo);
        }

        private KvExpr ParseSum(string s, int lineNo)
        {
            int idx = TopLevelIndex(s, "+");
            if (idx < 0) idx = TopLevelIndex(s, "-", 1);
            if (idx >= 0)
            {
                return new KvExpr
                {
                    kind = "bin",
                    op = s[idx].ToString(),
                    line = lineNo,
                    a = ParseSum(s.Substring(0, idx), lineNo),
                    b = ParseProduct(s.Substring(idx + 1), lineNo)
                };
            }
            return ParseProduct(s, lineNo);
        }

        private KvExpr ParseProduct(string s, int lineNo)
        {
            int idx = TopLevelIndex(s, "*");
            if (idx < 0) idx = TopLevelIndex(s, "/");
            if (idx >= 0)
            {
                return new KvExpr
                {
                    kind = "bin",
                    op = s[idx].ToString(),
                    line = lineNo,
                    a = ParseProduct(s.Substring(0, idx), lineNo),
                    b = ParseAtom(s.Substring(idx + 1), lineNo)
                };
            }
            return ParseAtom(s, lineNo);
        }

        private KvExpr ParseAtom(string s, int lineNo)
        {
            string t = s.Trim();
            if (t.Length == 0) return new KvExpr { kind = "num", number = 0, line = lineNo };
            if (t.StartsWith("(") && t.EndsWith(")")) return ParseAnd(t.Substring(1, t.Length - 2), lineNo);
            if (t.StartsWith("\"") && t.EndsWith("\"") && t.Length >= 2)
                return new KvExpr { kind = "str", text = t.Substring(1, t.Length - 2), line = lineNo };
            if (t.StartsWith("'") && t.EndsWith("'") && t.Length >= 2)
                return new KvExpr { kind = "str", text = t.Substring(1, t.Length - 2), line = lineNo };

            int open = t.IndexOf('(');
            if (open > 0 && t.EndsWith(")"))
            {
                KvExpr call = new KvExpr
                {
                    kind = "call",
                    text = t.Substring(0, open).Trim().ToLowerInvariant(),
                    line = lineNo,
                    args = new List<KvExpr>()
                };
                string[] parts = SplitArgs(t.Substring(open + 1, t.Length - open - 2));
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Trim().Length == 0) continue;
                    call.args.Add(ParseAnd(parts[i], lineNo));
                }
                return call;
            }

            double value;
            if (double.TryParse(t.Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value))
                return new KvExpr { kind = "num", number = value, line = lineNo };

            if (t == "истина" || t == "true") return new KvExpr { kind = "num", number = 1, line = lineNo };
            if (t == "ложь" || t == "false") return new KvExpr { kind = "num", number = 0, line = lineNo };
            return new KvExpr { kind = "var", text = t.ToLowerInvariant(), line = lineNo };
        }

        /// <summary>Позиция оператора на верхнем уровне (не внутри скобок/кавычек).</summary>
        private static int TopLevelIndex(string s, string op, int from = 0)
        {
            int depth = 0;
            bool inString = false;
            char quote = ' ';
            for (int i = from; i <= s.Length - op.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (c == quote) inString = false;
                    continue;
                }
                if (c == '"' || c == '\'') { inString = true; quote = c; continue; }
                if (c == '(') { depth++; continue; }
                if (c == ')') { depth--; continue; }
                if (depth != 0) continue;
                if (string.CompareOrdinal(s, i, op, 0, op.Length) != 0) continue;
                if ((op == "+" || op == "-") && i == 0) continue;
                if (op == "+" || op == "-")
                {
                    // Не считаем унарный знак (после оператора или открывающей скобки).
                    char prev = s[i - 1];
                    if (prev == '(' || prev == ',' || prev == '+' || prev == '-' || prev == '*' || prev == '/')
                        continue;
                }
                if (op == ">" || op == "<")
                {
                    if (i + 1 < s.Length && s[i + 1] == '=') continue;   // >= или <=
                }
                return i;
            }
            return -1;
        }

        private static string[] SplitArgs(string text)
        {
            List<string> parts = new List<string>();
            int depth = 0;
            bool inString = false;
            char quote = ' ';
            StringBuilder current = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    current.Append(c);
                    if (c == quote) inString = false;
                    continue;
                }
                if (c == '"' || c == '\'') { inString = true; quote = c; current.Append(c); continue; }
                if (c == '(') depth++;
                if (c == ')') depth--;
                if (c == ',' && depth == 0) { parts.Add(current.ToString()); current.Length = 0; continue; }
                current.Append(c);
            }
            if (current.Length > 0) parts.Add(current.ToString());
            return parts.ToArray();
        }

        private static string Strip(string line)
        {
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash);
            return line.Trim();
        }

        private static string AfterBrace(string line)
        {
            int brace = line.IndexOf('{');
            if (brace < 0) return null;
            return line.Substring(brace + 1).Trim();
        }

        private void Fail(int line, string message)
        {
            if (error == null) error = "строка " + line + ": " + message;
        }
    }

    /// <summary>Сохранённый макрос.</summary>
    public class KvScriptMacro
    {
        public string name = "";
        public string source = "";
        public string saved = "";
    }

    /// <summary>
    /// ЭТАП 26 ТЗ: ДВИЖОК МАКРОСОВ.
    ///
    /// Макрос — это последовательность РАЗРЕШЁННЫХ команд робота (движение, смена варианта,
    /// постобработка, маршрут, журнал) с переменными, циклами `repeat/while` и условиями `if`.
    /// Выполнение идёт ПО ЧАСТЯМ в главном потоке (по 3 мс на кадр), поэтому интерфейс не
    /// замирает, а долгие команды (`в_точку`, `ждать`, `ждать_движение`) приостанавливают
    /// скрипт до завершения, не блокируя кадр целиком.
    ///
    /// Песочница: список команд закрыт, шагов и времени ограничено, доступа к файлам,
    /// сети и системным вызовам из макроса нет.
    /// </summary>
    public class KvScriptEngine
    {
        public const int MaxStepsPerRun = 200000;
        public const float MaxRunSeconds = 600f;

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;
        private KvWaypointManager waypoints;
        private KvStageHub3 stage3;

        private List<KvStmt> program;
        private readonly List<KvScriptMacro> macros = new List<KvScriptMacro>();
        private readonly Dictionary<string, KvValue> vars = new Dictionary<string, KvValue>();
        private readonly List<string> output = new List<string>();
        private readonly List<Frame> frames = new List<Frame>();

        private string name = "";
        private string source = "";
        private bool running;
        private int steps;
        private float runStart;
        private float waitUntil;
        private bool waitMotion;
        private KvScriptError error;
        private int currentLine;
        private int loopGuard;
        private int pauseDepth;                 // вложенность незавершённых тел
        private int pauseIndex;

        /// <summary>Значение переменной макроса: число или строка.</summary>
        public struct KvValue
        {
            public double number;
            public string text;
            public bool isText;
            public KvValue(double n) { number = n; text = null; isText = false; }
            public KvValue(string s) { number = 0; text = s; isText = true; }
            public override string ToString()
            {
                return isText ? (text ?? "") : number.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }

        private class Frame
        {
            public List<KvStmt> body;
            public int index;
            public int repeatLeft;
            public bool isRepeat;
            public bool isWhile;
            public KvExpr condition;
            public int line;
        }

        public bool Running { get { return running; } }
        public string Status
        {
            get
            {
                if (error != null) return "ошибка · " + error;
                if (!running) return "не выполняется";
                return "выполняется" + (string.IsNullOrEmpty(name) ? "" : " «" + name + "»") +
                       " · строка " + currentLine + " · шагов " + steps;
            }
        }
        public KvScriptError LastError { get { return error; } }
        public int CurrentLine { get { return currentLine; } }
        public IList<string> Output { get { return output; } }
        /// <summary>
        /// СПИСОК МАКРОСОВ ИЗМЕНИЛСЯ (реализовано 30.10.2026, S44, пункт 5.25).
        ///
        /// ЧТО БЫЛО. `RefreshMacroList()` был ПУСТЫМ методом (`private void RefreshMacroList() { }`),
        /// и его вызывали после сохранения и после удаления макроса. То есть код «делал вид», что
        /// обновляет список, а на деле не делал ничего. Последствие для оператора: сохранил макрос —
        /// а в списке его нет; удалил — а он там остался. Приходилось перезапускать окно.
        ///
        /// ПОЧЕМУ СОБЫТИЕ, А НЕ ПРЯМОЙ ВЫЗОВ ОКНА. Движок макросов НЕ ДОЛЖЕН знать про окна
        /// интерфейса: иначе появляется обратная зависимость «движок зависит от UI». Событие —
        /// единственный способ уведомить всех, кто показывает список, не зная, кто это.
        ///
        /// ВАЖНО ПРО ОТПИСКУ. Подписчик обязан отписаться в `OnDestroy`: иначе закрытое окно
        /// останется в списке подписчиков и после переоткрытия будет обновляться дважды — та же
        /// утечка, что закрывали в §27 (43 подписки без отписки).
        /// </summary>
        public event Action MacroListChanged;

        /// <summary>Есть ли подписчики на изменение списка (для диагностики и тестов).</summary>
        public bool HasMacroListSubscribers { get { return MacroListChanged != null; } }

        public IList<KvScriptMacro> Macros { get { return macros; } }
        public string Name { get { return name; } }
        public string Source { get { return source; } }
        public int StepCount { get { return steps; } }
        public IDictionary<string, KvValue> Variables { get { return vars; } }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvWaypointManager route,
            KvStageHub3 hub3)
        {
            flow = controller;
            features = hub;
            waypoints = route;
            stage3 = hub3;
            LoadMacros();
        }

        // ---------------------------------------------------------------- выполнение

        public bool Run(string text, string macroName)
        {
            if (running) { Report("макрос уже выполняется"); return false; }
            string parseError;
            List<KvStmt> parsed = KvScriptParser.Parse(text, out parseError);
            if (parsed == null)
            {
                error = new KvScriptError(0, parseError);
                Report("макрос не запущен — " + parseError);
                return false;
            }
            program = parsed;
            source = text;
            name = macroName ?? "";
            vars.Clear();
            output.Clear();
            frames.Clear();
            error = null;
            steps = 0;
            loopGuard = 0;
            waitUntil = 0f;
            waitMotion = false;
            currentLine = 0;
            runStart = Time.realtimeSinceStartup;
            frames.Add(new Frame { body = program, index = 0, line = 0 });
            running = true;
            Report("макрос запущен" + (string.IsNullOrEmpty(name) ? "" : " «" + name + "»") +
                   " · операторов " + program.Count);
            return true;
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            frames.Clear();
            Report("макрос остановлен оператором");
        }

        /// <summary>Кадровое выполнение: не дольше 3 мс за кадр.</summary>
        public void Tick(float deltaTime)
        {
            if (!running) return;

            if (Time.realtimeSinceStartup - runStart > MaxRunSeconds)
            {
                Fail(currentLine, "макрос выполняется дольше " + MaxRunSeconds + " с — остановлен");
                return;
            }
            if (waitMotion)
            {
                if (IsRobotBusy()) return;
                waitMotion = false;
            }
            if (Time.realtimeSinceStartup < waitUntil) return;

            float budgetEnd = Time.realtimeSinceStartup + 0.003f;
            while (running && Time.realtimeSinceStartup < budgetEnd)
            {
                if (!Step()) return;
                if (waitMotion || Time.realtimeSinceStartup < waitUntil) return;
            }
        }

        private bool Step()
        {
            if (frames.Count == 0) { Finish("макрос выполнен"); return false; }
            Frame frame = frames[frames.Count - 1];
            if (frame.index >= frame.body.Count)
            {
                frames.RemoveAt(frames.Count - 1);
                OnFrameEnd(frame);
                return true;
            }

            KvStmt stmt = frame.body[frame.index];
            currentLine = stmt.line;
            steps++;
            if (steps > MaxStepsPerRun) { Fail(stmt.line, "слишком много шагов — остановлено"); return false; }

            switch (stmt.kind)
            {
                case KvStmtKind.Assign:
                {
                    KvValue value;
                    if (!Eval(stmt.value, out value)) return false;
                    vars[stmt.name.ToLowerInvariant()] = value;
                    frame.index++;
                    return true;
                }
                case KvStmtKind.Call:
                    frame.index++;
                    return Execute(stmt);

                case KvStmtKind.Repeat:
                {
                    KvValue count;
                    if (!Eval(stmt.condition, out count)) return false;
                    int times = Mathf.Clamp(Mathf.RoundToInt((float)count.number), 0, 10000);
                    frame.index++;
                    if (times > 0 && stmt.body.Count > 0)
                        frames.Add(new Frame
                        {
                            body = stmt.body,
                            index = 0,
                            repeatLeft = times,
                            isRepeat = true,
                            line = stmt.line
                        });
                    return true;
                }
                case KvStmtKind.While:
                {
                    if (loopGuard++ > 100000) { Fail(stmt.line, "цикл «пока» не завершается"); return false; }
                    KvValue cond;
                    if (!Eval(stmt.condition, out cond)) return false;
                    if (cond.number == 0 || cond.isText)
                    {
                        frame.index++;
                        return true;
                    }
                    if (stmt.body.Count > 0)
                        frames.Add(new Frame
                        {
                            body = stmt.body,
                            index = 0,
                            isWhile = true,
                            condition = stmt.condition,
                            line = stmt.line
                        });
                    return true;
                }
                case KvStmtKind.If:
                {
                    KvValue cond;
                    if (!Eval(stmt.condition, out cond)) return false;
                    bool truth = !cond.isText && cond.number != 0;
                    frame.index++;
                    List<KvStmt> branch = truth ? stmt.body : stmt.elseBody;
                    if (branch != null && branch.Count > 0)
                        frames.Add(new Frame { body = branch, index = 0, line = stmt.line });
                    return true;
                }
            }
            frame.index++;
            return true;
        }

        /// <summary>Возврат из вложенного тела: повтор цикла или выход.</summary>
        private void OnFrameEnd(Frame frame)
        {
            if (frame.isRepeat && frame.repeatLeft > 1)
            {
                frame.repeatLeft--;
                frame.index = 0;
                frames.Add(frame);
                return;
            }
            if (frame.isWhile)
            {
                KvValue cond;
                if (Eval(frame.condition, out cond) && !cond.isText && cond.number != 0 &&
                    frame.body.Count > 0)
                {
                    frame.index = 0;
                    frames.Add(frame);
                }
            }
        }

        /// <summary>
        /// Публичная оценка выражения (для условий дерева поведения, этап 27):
        /// принимает как «tcp_z() > 0.5», так и «предел()».
        /// </summary>
        public bool EvaluateExpression(string expression, out double value)
        {
            value = 0;
            if (string.IsNullOrEmpty(expression)) return false;
            string error;
            List<KvStmt> parsed = KvScriptParser.Parse("результат_выражения = " + expression, out error);
            if (parsed == null || parsed.Count == 0) return false;

            KvValue result;
            if (!Eval(parsed[0].value, out result)) return false;
            value = result.isText ? 1 : result.number;
            return true;
        }

        private bool Execute(KvStmt stmt)
        {
            string command = Normalize(stmt.name);
            List<KvScriptEngine.KvValue> args = new List<KvValue>();
            for (int i = 0; i < stmt.args.Count; i++)
            {
                KvValue v;
                if (!Eval(stmt.args[i], out v)) return false;
                args.Add(v);
            }

            float n0 = args.Count > 0 ? (float)args[0].number : 0f;
            float n1 = args.Count > 1 ? (float)args[1].number : 0f;
            float n2 = args.Count > 2 ? (float)args[2].number : 0f;

            switch (command)
            {
                case "home": return RunHome(stmt.line);
                case "move_to": return RunMoveTo(new Vector3(n0, n1, n2), stmt.line);
                case "wait":
                    waitUntil = Time.realtimeSinceStartup + Mathf.Clamp(n0, 0f, 120f);
                    return true;
                case "wait_motion": waitMotion = true; return true;
                case "stop":
                    if (features != null) features.EmergencyStop();
                    return true;
                case "run": return RunSelected(stmt.line);
                case "pause":
                    if (flow.Motion != null) flow.Motion.SetPaused(!flow.Motion.Paused);
                    return true;
                case "select": return SelectVariant((int)n0, stmt.line);
                case "next": return StepVariant(1);
                case "prev": return StepVariant(-1);
                case "smooth":
                {
                    KvStageHub2 hub2 = KvStageHub2.Current;
                    if (hub2 != null && hub2.Smoothing != null)
                    {
                        hub2.Smoothing.Level = n0;
                        if (n0 > 0f) hub2.Smoothing.ApplySelected(true);
                    }
                    return true;
                }
                case "constrain":
                {
                    KvStageHub3 hub3 = KvStageHub3.Current;
                    if (hub3 != null && hub3.Constrained != null) hub3.Constrained.Enabled = n0 > 0f;
                    return true;
                }
                case "payload":
                {
                    KvStageHub3 hub3 = KvStageHub3.Current;
                    if (hub3 != null && hub3.Payload != null)
                        hub3.Payload.Model.toolMassKg = Mathf.Clamp(n0, 0f, 50f);
                    return true;
                }
                case "waypoint":
                    if (waypoints != null) waypoints.Add(new Vector3(n0, n1, n2), "макрос (этап 26)");
                    return true;
                case "clear_route": if (waypoints != null) waypoints.Clear("макрос"); return true;
                case "route_run":
                    if (waypoints != null && !waypoints.PlayRoute()) Report("маршрут не запущен");
                    return true;
                case "pose": return SavePose(args, stmt.line);
                case "screenshot": return Screenshot(stmt.line);
                case "print": Print(args, false); return true;
                case "log": Print(args, true); return true;
                case "clear":
                    if (features != null && features.Log != null) features.Log.Clear();
                    return true;
                default:
                    Fail(stmt.line, "неизвестная команда «" + stmt.name + "» (см. список разрешённых)");
                    return false;
            }
        }

        /// <summary>Приведение имён команд: русские и английские написания равнозначны.</summary>
        private static string Normalize(string name)
        {
            switch (name)
            {
                case "домой": return "home";
                case "в_точку": case "в_точке": case "перейти": return "move_to";
                case "ждать": return "wait";
                case "ждать_движение": return "wait_motion";
                case "стоп": return "stop";
                case "пуск": return "run";
                case "пауза": return "pause";
                case "вариант": return "select";
                case "дальше": return "next";
                case "назад": return "prev";
                case "сгладить": return "smooth";
                case "ограничить": return "constrain";
                case "нагрузка": return "payload";
                case "точка_маршрута": case "маршрут_точка": return "waypoint";
                case "очистить_маршрут": return "clear_route";
                case "маршрут_пуск": return "route_run";
                case "поза": case "записать_позу": return "pose";
                case "снимок": return "screenshot";
                case "печать": case "печатать": return "print";
                case "журнал": case "запись": return "log";
                case "очистить_журнал": return "clear";
                // функции-запросы (используются в выражениях условий)
                case "предел": case "запас": case "запас_лимитов": case "лимиты": return "limit_margin";
                case "зазор": case "клиренс": return "clearance";
                case "выбран": case "вариант_выбран": return "selected";
                case "вариантов": case "количество_вариантов": return "variant_count";
                case "едет": case "движется": return "moving";
                case "высота": case "высота_инструмента": return "tcp_z";
                case "время": return "seconds";
                case "x_инструмента": return "tcp_x";
                case "y_инструмента": return "tcp_y";
                default: return name;
            }
        }

        private bool IsRobotBusy()
        {
            if (flow == null) return false;
            return flow.ExternalMotionRunning || (flow.Motion != null && flow.Motion.IsRunning) ||
                   flow.State.phase == FlowState.RobotMoving;
        }

        private bool RunHome(int line)
        {
            if (flow == null || !flow.Validator.Ready) { Fail(line, "робот не определён"); return false; }
            if (IsRobotBusy()) { Fail(line, "робот занят — команда «домой» отклонена"); return false; }
            PoseValidator v = flow.Validator;
            double[] start = v.CopyCurrent();
            double[] home = new double[v.Dof];
            for (int i = 0; i < home.Length; i++)
                home[i] = v.IsPrismatic(i) ? v.Lower[i] + (v.Upper[i] - v.Lower[i]) * 0.5 : 0.0;
            PlannedTrajectory plan = KvPlanKit.MakeJointPlan(v,
                features != null ? features.World : null, start, v.ContinueFrom(start, home),
                "макрос: домой", 0.08f, 60);
            if (plan == null) { Fail(line, "план до домашней позы не построен"); return false; }
            if (!flow.PlayExternalPlan(plan, plan.GoalQ, "макрос (этап 26)"))
            {
                Fail(line, "робот занят");
                return false;
            }
            waitMotion = true;
            Print(new List<KvValue> { new KvValue("домой: движение начато") }, true);
            return true;
        }

        private bool RunMoveTo(Vector3 target, int line)
        {
            if (flow == null || !flow.Validator.Ready) { Fail(line, "робот не определён"); return false; }
            if (IsRobotBusy()) { Fail(line, "робот занят — команда «в_точку» отклонена"); return false; }
            PoseValidator v = flow.Validator;
            double[] seed = v.CopyCurrent();
            double[] goal;
            if (!v.SolveIk(target, seed, out goal, 90, 0.008f))
            {
                Fail(line, "точка недостижима (" + target.x.ToString("0.00") + ", " +
                           target.y.ToString("0.00") + ", " + target.z.ToString("0.00") + ")");
                return false;
            }
            if (!v.WithinLimits(goal, 0.5f)) { Fail(line, "решение выходит за лимиты суставов"); return false; }
            goal = v.ContinueFrom(seed, goal);
            PlannedTrajectory plan = KvPlanKit.MakeJointPlan(v,
                features != null ? features.World : null, seed, goal, "макрос: в точку", 0.04f, 60);
            if (plan == null)
            {
                Fail(line, "путь не построен (мешают препятствия или лимиты)");
                return false;
            }
            if (!flow.PlayExternalPlan(plan, plan.GoalQ, "макрос (этап 26)"))
            {
                Fail(line, "робот занят");
                return false;
            }
            waitMotion = true;
            return true;
        }

        private bool RunSelected(int line)
        {
            if (flow == null) { Fail(line, "робот не определён"); return false; }
            if (flow.State.phase != FlowState.PhantomsMoving)
            {
                Fail(line, "пуск невозможен: сначала выберите вариант («вариант(n)»), " +
                           "текущее состояние " + flow.State.phase);
                return false;
            }
            if (!flow.ConfirmSelectedTrajectory()) { Fail(line, "пуск отклонён (Safety)"); return false; }
            waitMotion = true;
            return true;
        }

        private bool SelectVariant(int number, int line)
        {
            if (flow == null) { Fail(line, "робот не определён"); return false; }
            if (number <= 0)
            {
                Fail(line, "номер варианта должен быть больше нуля (получено " + number + ")");
                return false;
            }
            if (!flow.SelectCandidateByIndex(number - 1))
            {
                Fail(line, "варианта №" + number + " нет (доступно " + flow.State.candidates.Count + ")");
                return false;
            }
            return true;
        }

        private bool StepVariant(int delta)
        {
            if (flow == null || flow.State.candidates.Count == 0) return true;
            int count = flow.State.candidates.Count;
            int index = flow.State.selectedTrajectory < 0 ? 0 : flow.State.selectedTrajectory;
            index = ((index + delta) % count + count) % count;
            flow.SelectCandidateByIndex(index);
            return true;
        }

        private bool SavePose(List<KvValue> args, int line)
        {
            if (features == null || features.Poses == null) { Fail(line, "библиотека поз недоступна"); return false; }
            string poseName = args.Count > 0 ? args[0].ToString() : "Поза (макрос)";
            features.Poses.SaveCurrent(poseName);
            return true;
        }

        private bool Screenshot(int line)
        {
            KvStageHub hub = KvStageHub.Current;
            if (hub == null || !hub.TakeScreenshot()) { Fail(line, "снимок не сделан"); return false; }
            return true;
        }

        private void Print(List<KvValue> args, bool toLog)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < args.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(args[i].ToString());
            }
            string text = sb.ToString();
            output.Add(text);
            if (output.Count > 200) output.RemoveAt(0);
            if (toLog && features != null && features.Log != null) features.Log.Info("макрос: " + text);
            Report(text);
        }

        // ---------------------------------------------------------------- выражения

        private bool Eval(KvExpr e, out KvValue value)
        {
            value = new KvValue(0);
            if (e == null) return true;
            switch (e.kind)
            {
                case "num": value = new KvValue(e.number); return true;
                case "str": value = new KvValue(e.text); return true;
                case "var":
                {
                    KvValue stored;
                    if (!vars.TryGetValue(e.text, out stored))
                    {
                        Fail(e.line, "переменная «" + e.text + "» не задана");
                        return false;
                    }
                    value = stored;
                    return true;
                }
                case "call": return EvalCall(e, out value);
                case "bin": return EvalBinary(e, out value);
            }
            Fail(e.line, "неизвестное выражение");
            return false;
        }

        private bool EvalBinary(KvExpr e, out KvValue value)
        {
            value = new KvValue(0);
            KvValue a, b;
            if (!Eval(e.a, out a) || !Eval(e.b, out b)) return false;

            if (e.op == "+" && (a.isText || b.isText))
            {
                value = new KvValue(a.ToString() + b.ToString());
                return true;
            }
            double x = a.number, y = b.number;
            switch (e.op)
            {
                case "+": value = new KvValue(x + y); return true;
                case "-": value = new KvValue(x - y); return true;
                case "*": value = new KvValue(x * y); return true;
                case "/":
                    if (Math.Abs(y) < 1e-9) { Fail(e.line, "деление на ноль"); return false; }
                    value = new KvValue(x / y);
                    return true;
                case ">": value = new KvValue(x > y ? 1 : 0); return true;
                case "<": value = new KvValue(x < y ? 1 : 0); return true;
                case ">=": value = new KvValue(x >= y ? 1 : 0); return true;
                case "<=": value = new KvValue(x <= y ? 1 : 0); return true;
                case "==": value = new KvValue(Math.Abs(x - y) < 1e-9 ? 1 : 0); return true;
                case "!=": value = new KvValue(Math.Abs(x - y) >= 1e-9 ? 1 : 0); return true;
            }
            Fail(e.line, "неизвестная операция «" + e.op + "»");
            return false;
        }

        /// <summary>Функции-запросы: их можно использовать в условиях и арифметике.</summary>
        private bool EvalCall(KvExpr e, out KvValue value)
        {
            value = new KvValue(0);
            string fn = Normalize(e.text);
            if (fn == "высота" || fn == "height") fn = "tcp_z";
            if (fn == "время" || fn == "time") fn = "seconds";
            if (fn == "вариантов" || fn == "variants") fn = "variant_count";

            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                if (fn == "seconds") { value = new KvValue(Time.realtimeSinceStartup); return true; }
                Fail(e.line, "робот не определён — функция недоступна");
                return false;
            }

            Vector3 tcp = flow.Validator.TcpAt(flow.Validator.CopyCurrent());
            switch (fn)
            {
                case "tcp_x": value = new KvValue(tcp.x); return true;
                case "tcp_y": value = new KvValue(tcp.y); return true;
                case "tcp_z": value = new KvValue(tcp.z); return true;
                case "seconds": value = new KvValue(Time.realtimeSinceStartup); return true;
                case "variant_count": value = new KvValue(flow.State.candidates.Count); return true;
                case "selected": value = new KvValue(flow.State.selectedTrajectory + 1); return true;
                case "limit_margin": value = new KvValue(flow.Validator.LimitMargin(flow.Validator.CopyCurrent())); return true;
                case "clearance":
                {
                    Vector3 tcpPoint;
                    Vector3[] nodes;
                    value = new KvValue(flow.Validator.ClearanceAt(flow.Validator.CopyCurrent(),
                        features != null ? features.World : null, out tcpPoint, out nodes));
                    return true;
                }
                case "moving":
                    value = new KvValue(IsRobotBusy() ? 1 : 0);
                    return true;
                case "payload":
                {
                    KvStageHub3 hub3 = KvStageHub3.Current;
                    value = new KvValue(hub3 != null && hub3.Payload != null
                        ? hub3.Payload.Model.toolMassKg : 0f);
                    return true;
                }
                case "constrain":
                    value = new KvValue(stage3 != null && stage3.Constrained != null &&
                                        stage3.Constrained.Enabled ? 1 : 0);
                    return true;
                case "smooth":
                {
                    KvStageHub2 hub2 = KvStageHub2.Current;
                    value = new KvValue(hub2 != null && hub2.Smoothing != null ? hub2.Smoothing.Level : 0f);
                    return true;
                }
            }
            Fail(e.line, "неизвестная функция «" + e.text + "»");
            return false;
        }

        private void Fail(int line, string message)
        {
            error = new KvScriptError(line, message);
            running = false;
            frames.Clear();
            Report("макрос остановлен — строка " + line + ": " + message);
        }

        private void Finish(string text)
        {
            running = false;
            Report(text + " · шагов " + steps + " · за " +
                   (Time.realtimeSinceStartup - runStart).ToString("0.00") + " с");
        }

        // ---------------------------------------------------------------- макросы на диске

        public string MacrosFolder
        {
            get { return System.IO.Path.Combine(FeatureStorage.Root, "Macros"); }
        }

        public void SaveMacro(string macroName, string text)
        {
            if (string.IsNullOrEmpty(macroName)) return;
            try
            {
                System.IO.Directory.CreateDirectory(MacrosFolder);
                string path = System.IO.Path.Combine(MacrosFolder, Safe(macroName) + ".kvs");
                System.IO.File.WriteAllText(path, text ?? "", Encoding.UTF8);
                KvScriptMacro found = Find(macroName);
                if (found == null) { found = new KvScriptMacro { name = macroName }; macros.Add(found); }
                found.source = text ?? "";
                found.saved = FeatureStorage.TimeStamp();
                Report("макрос сохранён: " + path);
                RefreshMacroList();
            }
            catch (Exception e)
            {
                Report("макрос не сохранён: " + e.Message);
            }
        }

        public bool DeleteMacro(string macroName)
        {
            try
            {
                string path = System.IO.Path.Combine(MacrosFolder, Safe(macroName) + ".kvs");
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                KvScriptMacro found = Find(macroName);
                if (found != null) macros.Remove(found);
                RefreshMacroList();
                return true;
            }
            catch (Exception e)
            {
                Report("макрос не удалён: " + e.Message);
                return false;
            }
        }

        public string LoadMacroSource(string macroName)
        {
            try
            {
                string path = System.IO.Path.Combine(MacrosFolder, Safe(macroName) + ".kvs");
                return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path, Encoding.UTF8) : "";
            }
            catch (Exception) { return ""; }
        }

        public void LoadMacros()
        {
            macros.Clear();
            try
            {
                if (!System.IO.Directory.Exists(MacrosFolder)) return;
                string[] files = System.IO.Directory.GetFiles(MacrosFolder, "*.kvs");
                for (int i = 0; i < files.Length; i++)
                {
                    macros.Add(new KvScriptMacro
                    {
                        name = System.IO.Path.GetFileNameWithoutExtension(files[i]),
                        source = System.IO.File.ReadAllText(files[i], Encoding.UTF8),
                        saved = System.IO.File.GetLastWriteTime(files[i]).ToString("dd.MM.yyyy HH:mm")
                    });
                }
                Report("загружено макросов: " + macros.Count);
            }
            catch (Exception e)
            {
                Report("список макросов не прочитан: " + e.Message);
            }
        }

        /// <summary>
        /// СООБЩИТЬ, ЧТО СПИСОК МАКРОСОВ ИЗМЕНИЛСЯ (было пустым методом — см. `MacroListChanged`).
        ///
        /// ВЫЗЫВАЕТСЯ ПОСЛЕ СОХРАНЕНИЯ И УДАЛЕНИЯ. Раньше это был пустой метод: список в
        /// интерфейсе не обновлялся, и оператор не видел ни нового макроса, ни удалённого.
        ///
        /// ИСКЛЮЧЕНИЕ В ПОДПИСЧИКЕ НЕ ДОЛЖНО ЛОМАТЬ СОХРАНЕНИЕ. Если окно интерфейса упадёт,
        /// макрос всё равно записан на диск — сообщение о падении идёт в журнал, а работа движка
        /// продолжается. Это важно: сохранение макроса не должно зависеть от состояния UI.
        /// </summary>
        private void RefreshMacroList()
        {
            Action handler = MacroListChanged;
            if (handler == null) return;
            try
            {
                handler();
            }
            catch (Exception e)
            {
                Report("список макросов не обновлён в интерфейсе: " + e.Message +
                       " (сам макрос при этом сохранён на диск)");
            }
        }

        private KvScriptMacro Find(string macroName)
        {
            for (int i = 0; i < macros.Count; i++)
                if (string.Equals(macros[i].name, macroName, StringComparison.OrdinalIgnoreCase))
                    return macros[i];
            return null;
        }

        private static string Safe(string text)
        {
            char[] bad = System.IO.Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                bool ok = true;
                for (int b = 0; b < bad.Length; b++) if (text[i] == bad[b]) { ok = false; break; }
                sb.Append(ok ? text[i] : '_');
            }
            return sb.ToString();
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Script] " + text);
            if (Message != null) Message(text);
        }

        /// <summary>Пример макроса — открывается кнопкой «Пример» (этап 26 ТЗ).</summary>
        public const string Sample =
            "# Пример макроса KazistovVv (этап 26)\n" +
            "# Переезд по точкам с проверкой запаса до лимитов суставов\n" +
            "домой()\n" +
            "ждать_движение()\n" +
            "repeat 3 {\n" +
            "  в_точку(0.45, 0.10, 0.55)\n" +
            "  ждать_движение()\n" +
            "  запас = предел()\n" +
            "  if запас < 5 { журнал(\"маленький запас до лимитов\") }\n" +
            "  ждать(0.4)\n" +
            "}\n" +
            "вариант(1)\n" +
            "снимок()\n" +
            "печать(\"макрос завершён\")\n";
    }

    /// <summary>ВКЛАДКА «МАКРОСЫ» (ЭТАП 26 ТЗ).</summary>
    public class KvScriptTab : IKvWorkbenchTab
    {
        private readonly KvScriptEngine engine;
        private InputField editor;
        private InputField macroName;

        public KvScriptTab(KvScriptEngine scriptEngine) { engine = scriptEngine; }

        public string Key { get { return "script"; } }
        public string Title { get { return KvLocExtra3.T("script.title", "Макросы (скрипты)"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("script.title", "Макросы (скрипты)"));
            editor = KvInputKit.Multi(kit.Content, T("script.text", "Текст макроса"),
                string.IsNullOrEmpty(editor != null ? editor.text : null) ? KvScriptEngine.Sample : editor.text,
                "домой()\nждать_движение()", 150f, null);
            macroName = KvInputKit.Single(kit.Content, T("script.name", "Имя макроса"), "Макрос1", "",
                null);

            kit.Buttons(new[]
            {
                T("script.run", "Выполнить"),
                T("script.stop", "Остановить"),
                T("script.save", "Сохранить"),
                T("script.load", "Загрузить"),
                T("script.sample", "Пример")
            }, new Action[]
            {
                delegate { if (editor != null) engine.Run(editor.text, macroName != null ? macroName.text : ""); },
                delegate { engine.Stop(); },
                delegate
                {
                    if (editor != null && macroName != null) engine.SaveMacro(macroName.text, editor.text);
                },
                delegate
                {
                    if (editor != null && macroName != null)
                    {
                        string text = engine.LoadMacroSource(macroName.text);
                        if (!string.IsNullOrEmpty(text)) editor.text = text;
                    }
                },
                delegate { if (editor != null) editor.text = KvScriptEngine.Sample; }
            });

            kit.Info(delegate { return engine.Status; },
                engine.LastError != null ? KvTheme.Error : (engine.Running ? KvTheme.Ok : KvTheme.TextMain));
            kit.Info(delegate
            {
                if (engine.Output.Count == 0) return T("script.noout", "вывод пуст");
                return engine.Output[engine.Output.Count - 1];
            }, KvTheme.TextMain);
            kit.Info(delegate
            {
                return T("script.macros", "Макросов на диске") + ": " + engine.Macros.Count +
                       " · " + T("script.folder", "папка") + ": " + engine.MacrosFolder;
            }, KvTheme.TextDim);

            kit.Divider();
            kit.Section(T("script.commands", "Разрешённые команды (песочница)"));
            kit.Note(T("script.commands.list",
                "домой() · в_точку(x, y, z) · ждать(с) · ждать_движение() · стоп() · пуск() · пауза() · " +
                "вариант(n) · дальше() · назад() · сгладить(0/1) · ограничить(0/1) · нагрузка(кг) · " +
                "точка_маршрута(x, y, z) · очистить_маршрут() · маршрут_пуск() · поза(имя) · снимок() · " +
                "печать(…) · журнал(…) · очистить_журнал()"), KvTheme.TextDim);
            kit.Note(T("script.funcs",
                "Функции-запросы: tcp_x() · tcp_y() · tcp_z() · предел() → limit_margin() · " +
                "зазор() → clearance() · вариант_выбран() → selected() · вариантов() → variant_count() · " +
                "едет() → moving() · нагрузка() → payload() · время() → seconds()"), KvTheme.TextDim);
            kit.Note(T("script.sandbox",
                "Песочница: список команд закрыт, шагов не больше 200 000, время не больше 600 с, " +
                "доступа к файлам, сети и системе у макроса нет. Ошибки показываются с номером строки."),
                KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh() { }
    }

    // ==========================================================================================
    // ЭТАП 27 ТЗ: ВИЗУАЛЬНЫЙ РЕДАКТОР ДЕРЕВА ПОВЕДЕНИЯ
    // ==========================================================================================

    public enum KvBtNodeType { Sequence, Selector, Action, Condition, Wait }
    public enum KvBtStatus { Idle, Running, Success, Failure }

    /// <summary>Узел дерева поведения (с координатами для редактора).</summary>
    [Serializable]
    public class KvBtNode
    {
        public int id;
        public KvBtNodeType type = KvBtNodeType.Action;
        public string title = "";
        public string payload = "";       // команда для Action, выражение для Condition, секунды для Wait
        public float x, y;                // положение в редакторе
        public int parent = -1;
        public readonly List<int> children = new List<int>();

        public string TypeLabel
        {
            get
            {
                switch (type)
                {
                    case KvBtNodeType.Sequence: return "последовательность";
                    case KvBtNodeType.Selector: return "выбор (или)";
                    case KvBtNodeType.Condition: return "условие";
                    case KvBtNodeType.Wait: return "пауза";
                    default: return "действие";
                }
            }
        }
    }

    /// <summary>Дерево поведения: набор узлов, корень и запуск.</summary>
    public class KvBehaviorTree
    {
        public string name = "Дерево 1";
        public readonly List<KvBtNode> nodes = new List<KvBtNode>();
        public int rootId = -1;
        private int nextId = 1;

        public KvBtNode Add(KvBtNodeType type, float x, float y)
        {
            KvBtNode node = new KvBtNode
            {
                id = nextId++,
                type = type,
                x = x,
                y = y,
                title = type == KvBtNodeType.Action ? "действие" :
                        type == KvBtNodeType.Condition ? "условие" :
                        type == KvBtNodeType.Wait ? "пауза 1 с" : "группа"
            };
            nodes.Add(node);
            if (rootId < 0) rootId = node.id;
            return node;
        }

        public KvBtNode Find(int id)
        {
            for (int i = 0; i < nodes.Count; i++) if (nodes[i].id == id) return nodes[i];
            return null;
        }

        /// <summary>Сделать `child` потомком `parent` (с проверкой на цикл).</summary>
        public bool Link(int parentId, int childId)
        {
            if (parentId == childId) return false;
            KvBtNode parent = Find(parentId);
            KvBtNode child = Find(childId);
            if (parent == null || child == null) return false;
            if (parent.type == KvBtNodeType.Action || parent.type == KvBtNodeType.Condition ||
                parent.type == KvBtNodeType.Wait) return false;         // лист не может иметь детей
            if (IsDescendant(childId, parentId)) return false;         // не допускаем цикл

            Unlink(childId);
            child.parent = parentId;
            parent.children.Add(childId);
            return true;
        }

        private bool IsDescendant(int candidateId, int ancestorId)
        {
            KvBtNode node = Find(candidateId);
            int guard = 0;
            while (node != null && node.parent >= 0 && guard++ < 500)
            {
                if (node.parent == ancestorId) return true;
                node = Find(node.parent);
            }
            return false;
        }

        public void Unlink(int childId)
        {
            KvBtNode child = Find(childId);
            if (child == null || child.parent < 0) return;
            KvBtNode parent = Find(child.parent);
            if (parent != null) parent.children.Remove(childId);
            child.parent = -1;
        }

        public void Remove(int id)
        {
            KvBtNode node = Find(id);
            if (node == null) return;
            if (node.children.Count > 0)
            {
                // Дети «поднимаются» к родителю удаляемого узла — дерево не теряет ветки.
                List<int> kids = new List<int>(node.children);
                for (int i = 0; i < kids.Count; i++)
                {
                    KvBtNode child = Find(kids[i]);
                    if (child == null) continue;
                    child.parent = node.parent;
                    if (node.parent >= 0)
                    {
                        KvBtNode parent = Find(node.parent);
                        if (parent != null && !parent.children.Contains(child.id))
                            parent.children.Add(child.id);
                    }
                }
            }
            Unlink(id);
            nodes.Remove(node);
            if (rootId == id) rootId = nodes.Count > 0 ? nodes[0].id : -1;
        }

        /// <summary>Выгрузка дерева в текст скрипта (этап 26) — для повторного запуска макросом.</summary>
        public string ExportScript()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Дерево поведения «" + name + "» → скрипт (этап 27 → 26)");
            if (rootId < 0) { sb.AppendLine("# Дерево пусто"); return sb.ToString(); }
            WriteNode(sb, Find(rootId), 0, null);
            return sb.ToString();
        }

        private void WriteNode(StringBuilder sb, KvBtNode node, int depth, string branch)
        {
            if (node == null) return;
            string indent = new string(' ', depth * 2);
            switch (node.type)
            {
                case KvBtNodeType.Sequence:
                    sb.AppendLine(indent + "# последовательность: все шаги по порядку" +
                                  (branch != null ? " (" + branch + ")" : ""));
                    break;
                case KvBtNodeType.Selector:
                    sb.AppendLine(indent + "# выбор: выполняется первая успешная ветка" +
                                  (branch != null ? " (" + branch + ")" : ""));
                    break;
                case KvBtNodeType.Condition:
                    sb.AppendLine(indent + "if " + (string.IsNullOrEmpty(node.payload) ? "1" : node.payload) + " {");
                    WriteChildren(sb, node, depth + 1);
                    sb.AppendLine(indent + "}");
                    return;
                case KvBtNodeType.Wait:
                    sb.AppendLine(indent + "ждать(" + (string.IsNullOrEmpty(node.payload) ? "1" : node.payload) + ")");
                    return;
                default:
                    sb.AppendLine(indent + (string.IsNullOrEmpty(node.payload) ? "# действие без команды" : node.payload));
                    return;
            }
            WriteChildren(sb, node, depth + 1);
        }

        private void WriteChildren(StringBuilder sb, KvBtNode node, int depth)
        {
            for (int i = 0; i < node.children.Count; i++)
                WriteNode(sb, Find(node.children[i]), depth, "ветка " + (i + 1));
        }

        /// <summary>Плоский список узлов сверху вниз — для обхода в рантайме.</summary>
        public List<int> Flatten()
        {
            List<int> order = new List<int>();
            Collect(rootId, order, 0);
            return order;
        }

        private void Collect(int id, List<int> order, int depth)
        {
            if (depth > 200) return;
            KvBtNode node = Find(id);
            if (node == null) return;
            order.Add(id);
            for (int i = 0; i < node.children.Count; i++) Collect(node.children[i], order, depth + 1);
        }
    }

    /// <summary>
    /// ИСПОЛНИТЕЛЬ ДЕРЕВА ПОВЕДЕНИЯ: обходит дерево в главном потоке.
    /// Последовательность выполняется, пока все дети успешны; выбор — до первого успеха;
    /// действие запускает мини-скрипт (движок этапа 26) и ждёт его завершения;
    /// условие проверяет выражение тем же разборщиком; пауза ждёт указанное время.
    /// </summary>
    public class KvBtRunner
    {
        public event Action<string> Message;

        private readonly KvScriptEngine engine;
        private KvBehaviorTree tree;

        private readonly Dictionary<int, KvBtStatus> statuses = new Dictionary<int, KvBtStatus>();
        private readonly List<int> path = new List<int>();
        private readonly HashSet<int> started = new HashSet<int>();     // узлы, начавшие работу
        private readonly HashSet<int> completed = new HashSet<int>();   // узлы, уже отработавшие
        private int activeId = -1;
        private float waitUntil;
        private bool running;
        private string result = "—";
        private float cycleStart;

        public KvBtRunner(KvScriptEngine scriptEngine) { engine = scriptEngine; }

        public bool Running { get { return running; } }
        public int ActiveId { get { return activeId; } }
        public string Result { get { return result; } }
        public IList<int> Path { get { return path; } }

        public KvBtStatus StatusOf(int id)
        {
            KvBtStatus s;
            return statuses.TryGetValue(id, out s) ? s : KvBtStatus.Idle;
        }

        public void SetTree(KvBehaviorTree value) { tree = value; }

        public bool Start()
        {
            if (tree == null || tree.rootId < 0) { Report("дерево пусто"); return false; }
            if (engine.Running) { Report("сейчас выполняется макрос — дерево не запущено"); return false; }
            statuses.Clear();
            started.Clear();
            completed.Clear();
            path.Clear();
            activeId = -1;
            waitUntil = 0f;
            cycleStart = Time.realtimeSinceStartup;
            running = true;
            result = "выполняется";
            Report("дерево «" + tree.name + "» запущено");
            return true;
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            engine.Stop();
            result = "остановлено оператором";
            Report("дерево остановлено");
        }

        public void Tick(float deltaTime)
        {
            if (!running) return;
            if (Time.realtimeSinceStartup < waitUntil) return;
            if (engine.Running) return;         // ждём завершения действия

            KvBtStatus status = TickNode(tree.rootId, 0);
            if (status == KvBtStatus.Running) return;

            running = false;
            result = status == KvBtStatus.Success ? "дерево выполнено успешно" : "дерево завершилось неудачей";
            Report(result + " · за " + (Time.realtimeSinceStartup - cycleStart).ToString("0.00") + " с");
        }

        private KvBtStatus TickNode(int id, int depth)
        {
            if (depth > 100) return KvBtStatus.Failure;
            KvBtNode node = tree.Find(id);
            if (node == null) return KvBtStatus.Failure;
            activeId = id;
            path.Add(id);
            if (path.Count > 4000) path.RemoveAt(0);

            KvBtStatus status;
            switch (node.type)
            {
                case KvBtNodeType.Sequence:
                {
                    status = KvBtStatus.Success;
                    for (int i = 0; i < node.children.Count; i++)
                    {
                        KvBtStatus child = TickNode(node.children[i], depth + 1);
                        if (child == KvBtStatus.Failure) { status = KvBtStatus.Failure; break; }
                        if (child == KvBtStatus.Running) { status = KvBtStatus.Running; break; }
                    }
                    break;
                }
                case KvBtNodeType.Selector:
                {
                    status = KvBtStatus.Failure;
                    for (int i = 0; i < node.children.Count; i++)
                    {
                        KvBtStatus child = TickNode(node.children[i], depth + 1);
                        if (child == KvBtStatus.Success) { status = KvBtStatus.Success; break; }
                        if (child == KvBtStatus.Running) { status = KvBtStatus.Running; break; }
                    }
                    break;
                }
                case KvBtNodeType.Condition:
                {
                    float value = Evaluate(node.payload);
                    status = value != 0f ? KvBtStatus.Success : KvBtStatus.Failure;
                    // Условие может иметь ветку «тогда».
                    if (status == KvBtStatus.Success && node.children.Count > 0)
                        status = TickNode(node.children[0], depth + 1);
                    break;
                }
                case KvBtNodeType.Wait:
                {
                    // Пауза помнит, что уже отработала: иначе при следующем заходе в узел
                    // (например, в последовательности) она запускалась бы заново бесконечно.
                    if (completed.Contains(id)) { status = KvBtStatus.Success; break; }
                    if (!started.Contains(id))
                    {
                        started.Add(id);
                        waitUntil = Time.realtimeSinceStartup +
                                    Mathf.Clamp(Evaluate(node.payload), 0f, 120f);
                        status = KvBtStatus.Running;
                        break;
                    }
                    if (Time.realtimeSinceStartup < waitUntil) { status = KvBtStatus.Running; break; }
                    completed.Add(id);
                    waitUntil = 0f;
                    status = KvBtStatus.Success;
                    break;
                }
                default:
                {
                    // Действие: мини-скрипт выполняется движком макросов (этап 26).
                    if (string.IsNullOrEmpty(node.payload)) { status = KvBtStatus.Success; break; }
                    if (completed.Contains(id)) { status = KvBtStatus.Success; break; }
                    if (started.Contains(id))
                    {
                        if (engine.Running) { status = KvBtStatus.Running; break; }
                        started.Remove(id);
                        completed.Add(id);
                        status = engine.LastError == null ? KvBtStatus.Success : KvBtStatus.Failure;
                        break;
                    }
                    if (engine.Running) { status = KvBtStatus.Running; break; }
                    if (!engine.Run(node.payload, "дерево: " + node.title))
                    {
                        status = KvBtStatus.Failure;
                        break;
                    }
                    started.Add(id);
                    status = KvBtStatus.Running;
                    break;
                }
            }

            statuses[id] = status;
            return status;
        }

        private float Evaluate(string expression)
        {
            if (string.IsNullOrEmpty(expression)) return 1f;
            double value;
            if (!engine.EvaluateExpression(expression, out value))
            {
                Report("условие «" + expression + "» не разобрано — считаем ложным");
                return 0f;
            }
            return (float)value;
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Behavior] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>Редактор дерева поведения на отдельном канвасе (этап 27 ТЗ).</summary>
    public class KvBehaviorTreeWindow : MonoBehaviour
    {
        public int sortingOrder = 47;

        private static KvBehaviorTreeWindow instance;
        private KvBehaviorTree tree;
        private KvBtRunner runner;

        private Canvas canvas;
        private RectTransform root;
        private RectTransform nodeArea;
        private Text statusText;
        private Text detailsText;
        private bool visible;
        private int selectedId = -1;
        private readonly Dictionary<int, RectTransform> views = new Dictionary<int, RectTransform>();
        private readonly List<Image> links = new List<Image>();
        private float timer;

        public static KvBehaviorTreeWindow Instance { get { return instance; } }
        public bool Visible { get { return visible; } }
        public KvBehaviorTree Tree { get { return tree; } }

        public static KvBehaviorTreeWindow Install(Transform parent, KvBehaviorTree behaviorTree,
            KvBtRunner behaviorRunner)
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("KvBehaviorTreeWindow", typeof(KvBehaviorTreeWindow));
            go.transform.SetParent(parent, false);
            instance = go.GetComponent<KvBehaviorTreeWindow>();
            instance.tree = behaviorTree;
            instance.runner = behaviorRunner;
            instance.Build(parent);
            instance.SetVisible(false);
            return instance;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (root != null) root.gameObject.SetActive(value);
            if (value) Rebuild();
        }

        public void Toggle()
        {
            SetVisible(!visible);
        }

        private void Build(Transform parent)
        {
            canvas = KvOverlayKit.CreateCanvas(parent, "KvBehaviorCanvas", sortingOrder);
            root = (RectTransform)canvas.transform;

            GameObject win = new GameObject("Window", typeof(Image));
            win.transform.SetParent(root, false);
            RectTransform winRect = (RectTransform)win.transform;
            winRect.anchorMin = new Vector2(0.5f, 0.5f);
            winRect.anchorMax = new Vector2(0.5f, 0.5f);
            winRect.pivot = new Vector2(0.5f, 0.5f);
            winRect.sizeDelta = new Vector2(880f, 560f);
            winRect.anchoredPosition = Vector2.zero;
            Image bg = win.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(KvTheme.WindowBg.r, KvTheme.WindowBg.g, KvTheme.WindowBg.b, 0.97f);
            this.windowRect = winRect;

            Text title = KvTheme.CreateText(winRect, "Title", KvLocExtra3.T("bt.title",
                "Дерево поведения (этап 27)"), KvTheme.FontSize, TextAnchor.MiddleLeft, KvTheme.TextMain);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-90f, 26f);
            title.rectTransform.anchoredPosition = new Vector2(-14f, -3f);

            Button close = KvTheme.CreateButton(winRect, "Close", "×", delegate { SetVisible(false); }, 22);
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(26f, 22f);
            closeRect.anchoredPosition = new Vector2(-8f, -5f);

            RectTransform tools = KvWidgets.CreateRow(winRect, "Tools", 26f, 6f);
            tools.anchorMin = new Vector2(0f, 1f);
            tools.anchorMax = new Vector2(1f, 1f);
            tools.pivot = new Vector2(0.5f, 1f);
            tools.sizeDelta = new Vector2(-16f, 26f);
            tools.anchoredPosition = new Vector2(0f, -32f);

            AddTool(tools, "＋ последовательность", delegate { AddNode(KvBtNodeType.Sequence); });
            AddTool(tools, "＋ выбор", delegate { AddNode(KvBtNodeType.Selector); });
            AddTool(tools, "＋ действие", delegate { AddNode(KvBtNodeType.Action); });
            AddTool(tools, "＋ условие", delegate { AddNode(KvBtNodeType.Condition); });
            AddTool(tools, "＋ пауза", delegate { AddNode(KvBtNodeType.Wait); });
            AddTool(tools, "связать с выбранным", LinkToSelected);
            AddTool(tools, "удалить", DeleteSelected);
            AddTool(tools, "корень", MakeRoot);
            AddTool(tools, "пуск", delegate { if (runner != null) runner.Start(); });
            AddTool(tools, "стоп", delegate { if (runner != null) runner.Stop(); });
            AddTool(tools, "в скрипт", ExportToScript);

            GameObject area = new GameObject("NodeArea", typeof(Image));
            area.transform.SetParent(winRect, false);
            nodeArea = (RectTransform)area.transform;
            nodeArea.anchorMin = new Vector2(0f, 0f);
            nodeArea.anchorMax = new Vector2(1f, 1f);
            nodeArea.pivot = new Vector2(0.5f, 0.5f);
            nodeArea.offsetMin = new Vector2(8f, 62f);
            nodeArea.offsetMax = new Vector2(-8f, -62f);
            Image areaBg = area.GetComponent<Image>();
            areaBg.sprite = KvTheme.WhiteSprite;
            areaBg.color = new Color(KvTheme.PanelDark.r, KvTheme.PanelDark.g, KvTheme.PanelDark.b, 0.6f);
            areaBg.raycastTarget = true;

            statusText = KvTheme.CreateText(winRect, "Status", "", KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
            statusText.rectTransform.sizeDelta = new Vector2(-16f, 18f);
            statusText.rectTransform.anchoredPosition = new Vector2(0f, 26f);

            detailsText = KvTheme.CreateText(winRect, "Details", "", KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextDim);
            detailsText.rectTransform.anchorMin = new Vector2(0f, 0f);
            detailsText.rectTransform.anchorMax = new Vector2(1f, 0f);
            detailsText.rectTransform.pivot = new Vector2(0.5f, 0f);
            detailsText.rectTransform.sizeDelta = new Vector2(-16f, 18f);
            detailsText.rectTransform.anchoredPosition = new Vector2(0f, 6f);

            // Перетаскивание окна за заголовок.
            KvWindowDrag drag = win.AddComponent<KvWindowDrag>();
            drag.target = winRect;
            drag.canvasRect = root;
        }

        private RectTransform windowRect;

        private void AddTool(RectTransform parent, string label, Action action)
        {
            Button b = KvTheme.CreateSmallButton(parent, "B" + label, label, action);
            // ФИКС 5/6 (§22): размеры кнопок панели инструментов редактора дерева задаются ЯВНО.
            // Строка KvWidgets.CreateRow не управляет размером детей, поэтому LayoutElement
            // игнорировался, и каждая кнопка рисовалась квадратом 100×100 — отсюда
            // «очень большие иконки» в окне «Дерево поведения».
            float width = Mathf.Max(60f, label.Length * 8f + 14f);
            LayoutElement le = b.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 24f;
            le.preferredHeight = 24f;
            le.minWidth = width;
            le.preferredWidth = width;
            ((RectTransform)b.transform).sizeDelta = new Vector2(width, 24f);
        }

        private void Update()
        {
            if (!visible) return;
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                timer = 0.15f;
                Rebuild();
            }
        }

        private void Rebuild()
        {
            if (tree == null || nodeArea == null) return;

            // Убираем виды узлов, которых больше нет в дереве.
            List<int> gone = new List<int>();
            foreach (KeyValuePair<int, RectTransform> pair in views)
                if (tree.Find(pair.Key) == null) gone.Add(pair.Key);
            for (int i = 0; i < gone.Count; i++)
            {
                if (views[gone[i]] != null) UnityEngine.Object.Destroy(views[gone[i]].gameObject);
                views.Remove(gone[i]);
            }

            for (int i = 0; i < tree.nodes.Count; i++)
            {
                KvBtNode node = tree.nodes[i];
                RectTransform rt;
                if (!views.TryGetValue(node.id, out rt) || rt == null)
                {
                    rt = CreateNodeView(node);
                    views[node.id] = rt;
                }
                rt.anchoredPosition = new Vector2(node.x, node.y);
                PaintNode(node, rt);
            }
            DrawLinks();

            if (runner != null)
            {
                statusText.text = KvLocExtra3.T("bt.status", "Состояние") + ": " + runner.Result +
                                  " · " + KvLocExtra3.T("bt.active", "активный узел") + ": " +
                                  (runner.ActiveId < 0 ? "—" : NodeTitle(runner.ActiveId));
                detailsText.text = KvLocExtra3.T("bt.hint",
                    "Перетаскивайте узлы мышью. «Связать с выбранным» делает выбранный узел потомком " +
                    "ранее выбранного. «В скрипт» выгружает дерево в текст макроса.");
            }
        }

        private string NodeTitle(int id)
        {
            KvBtNode node = tree.Find(id);
            return node == null ? "—" : node.title + " (" + node.TypeLabel + ")";
        }

        private RectTransform CreateNodeView(KvBtNode node)
        {
            GameObject go = new GameObject("Node" + node.id, typeof(Image));
            go.transform.SetParent(nodeArea, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(170f, 54f);
            Image img = go.GetComponent<Image>();
            img.sprite = KvTheme.WhiteSprite;
            img.color = KvTheme.ButtonBg;
            img.raycastTarget = true;

            Text label = KvTheme.CreateText(rt, "Label", "", KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, KvTheme.TextMain);
            KvTheme.Stretch(label.rectTransform, 6f, 6f, 4f, 4f);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            KvBtNodeView view = go.AddComponent<KvBtNodeView>();
            view.node = node;
            view.window = this;
            view.rect = rt;
            return rt;
        }

        private void PaintNode(KvBtNode node, RectTransform rt)
        {
            Image img = rt.GetComponent<Image>();
            Text label = rt.GetComponentInChildren<Text>();
            if (img != null)
            {
                KvBtStatus status = runner != null ? runner.StatusOf(node.id) : KvBtStatus.Idle;
                Color color = KvTheme.ButtonBg;
                if (node.id == selectedId) color = KvTheme.ButtonChecked;
                else if (status == KvBtStatus.Success) color = KvTheme.Ok;
                else if (status == KvBtStatus.Failure) color = new Color(0.55f, 0.20f, 0.20f);
                else if (status == KvBtStatus.Running) color = KvTheme.Accent;
                if (runner != null && runner.ActiveId == node.id)
                    color = Color.Lerp(color, Color.white, 0.25f);
                img.color = color;
            }
            if (label != null)
            {
                string payload = string.IsNullOrEmpty(node.payload) ? "—" : node.payload;
                if (payload.Length > 60) payload = payload.Substring(0, 57) + "…";
                label.text = node.title + "\n" + node.TypeLabel + "\n" + payload;
            }
        }

        private void DrawLinks()
        {
            int index = 0;
            for (int i = 0; i < tree.nodes.Count; i++)
            {
                KvBtNode parent = tree.nodes[i];
                RectTransform parentView;
                if (!views.TryGetValue(parent.id, out parentView) || parentView == null) continue;
                for (int c = 0; c < parent.children.Count; c++)
                {
                    RectTransform childView;
                    if (!views.TryGetValue(parent.children[c], out childView) || childView == null) continue;

                    while (links.Count <= index)
                    {
                        GameObject line = new GameObject("Link" + links.Count, typeof(Image));
                        line.transform.SetParent(nodeArea, false);
                        Image img = line.GetComponent<Image>();
                        img.sprite = KvTheme.WhiteSprite;
                        img.color = KvTheme.Separator;
                        img.raycastTarget = false;
                        links.Add(img);
                    }
                    Image link = links[index++];
                    link.gameObject.SetActive(true);

                    Vector2 a = parentView.anchoredPosition + new Vector2(85f, 0f);
                    Vector2 b = childView.anchoredPosition + new Vector2(85f, 54f);
                    Vector2 delta = b - a;
                    RectTransform lr = link.rectTransform;
                    lr.anchorMin = lr.anchorMax = new Vector2(0f, 1f);
                    lr.pivot = new Vector2(0.5f, 0.5f);
                    lr.sizeDelta = new Vector2(delta.magnitude, 2f);
                    lr.anchoredPosition = a + delta * 0.5f;
                    lr.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
            }
            for (int i = index; i < links.Count; i++) links[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ действия

        private void AddNode(KvBtNodeType type)
        {
            if (tree == null) return;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(nodeArea,
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), null, out local);
            KvBtNode node = tree.Add(type, local.x - 85f, local.y + 27f);
            selectedId = node.id;
            Rebuild();
            Report("узел добавлен: " + node.title);
        }

        private void LinkToSelected()
        {
            if (tree == null || selectedId < 0) return;
            KvBtNode child = tree.Find(selectedId);
            if (child == null) return;
            if (child.parent >= 0) { Report("узел уже связан — сначала отвяжите"); return; }

            KvBtNode parent = PickParent(child);
            if (parent == null) { Report("нет подходящего родителя — добавьте группу"); return; }
            bool ok = tree.Link(parent.id, child.id);
            Report(ok
                ? "узел «" + child.title + "» стал потомком «" + parent.title + "»"
                : "связь не создана (цикл или лист)");
            Rebuild();
        }

        /// <summary>Родитель — ближайшая группа выше узла, ещё не имеющая этого потомка.</summary>
        private KvBtNode PickParent(KvBtNode child)
        {
            KvBtNode best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < tree.nodes.Count; i++)
            {
                KvBtNode candidate = tree.nodes[i];
                if (candidate.id == child.id) continue;
                if (candidate.type != KvBtNodeType.Sequence && candidate.type != KvBtNodeType.Selector &&
                    candidate.type != KvBtNodeType.Condition) continue;
                if (candidate.y <= child.y) continue;
                float distance = Mathf.Abs(candidate.x - child.x) + Mathf.Abs(candidate.y - child.y);
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }
            return best;
        }

        private void DeleteSelected()
        {
            if (tree == null || selectedId < 0) return;
            tree.Remove(selectedId);
            selectedId = -1;
            Rebuild();
            Report("узел удалён");
        }

        private void MakeRoot()
        {
            if (tree == null || selectedId < 0) return;
            tree.rootId = selectedId;
            Rebuild();
            Report("корень дерева: " + NodeTitle(selectedId));
        }

        private void ExportToScript()
        {
            if (tree == null) return;
            string script = tree.ExportScript();
            KvStageHub4 hub = KvStageHub4.Current;
            if (hub != null && hub.Script != null) hub.Script.Run(script, "из дерева поведения");
            Report("дерево выгружено в скрипт (" + script.Split('\n').Length + " строк) и запущено");
        }

        internal void Select(int id)
        {
            selectedId = id;
            Rebuild();
        }

        internal void MoveNode(KvBtNode node, Vector2 position)
        {
            node.x = position.x;
            node.y = position.y;
            DrawLinks();
        }

        private void Report(string text)
        {
            Debug.Log("[Behavior] " + text);
        }
    }

    /// <summary>Перетаскивание узла дерева мышью (drag-and-drop редактора этапа 27).</summary>
    public class KvBtNodeView : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public KvBtNode node;
        public KvBehaviorTreeWindow window;
        public RectTransform rect;
        private Vector2 startPosition;
        private Vector2 startPointer;

        public void OnPointerDown(PointerEventData eventData)
        {
            startPosition = rect.anchoredPosition;
            RectTransform parent = rect.parent as RectTransform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position,
                eventData.pressEventCamera, out local);
            startPointer = local;
            if (window != null) window.Select(node.id);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rect == null) return;
            RectTransform parent = rect.parent as RectTransform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position,
                eventData.pressEventCamera, out local);
            Vector2 target = startPosition + (local - startPointer);
            target.x = Mathf.Clamp(target.x, 0f, parent.rect.width - rect.sizeDelta.x);
            target.y = Mathf.Clamp(target.y, rect.sizeDelta.y, parent.rect.height);
            rect.anchoredPosition = target;
            if (window != null) window.MoveNode(node, target);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (window != null) window.Select(node.id);
        }
    }

    /// <summary>Перетаскивание окна за заголовок (единый приём для новых окон этапов 13–36).</summary>
    public class KvWindowDrag : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public RectTransform target;
        public RectTransform canvasRect;
        private Vector2 startPosition;
        private Vector2 startPointer;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (target == null) return;
            startPosition = target.anchoredPosition;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position,
                eventData.pressEventCamera, out local);
            startPointer = local;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position,
                eventData.pressEventCamera, out local);
            target.anchoredPosition = startPosition + (local - startPointer);
        }
    }

    /// <summary>ВКЛАДКА «ДЕРЕВО ПОВЕДЕНИЯ» (ЭТАП 27 ТЗ).</summary>
    public class KvBehaviorTab : IKvWorkbenchTab
    {
        private readonly KvBehaviorTree tree;
        private readonly KvBtRunner runner;
        private readonly KvBehaviorTreeWindow window;

        public KvBehaviorTab(KvBehaviorTree behaviorTree, KvBtRunner behaviorRunner,
            KvBehaviorTreeWindow editor)
        {
            tree = behaviorTree;
            runner = behaviorRunner;
            window = editor;
        }

        public string Key { get { return "behavior"; } }
        public string Title { get { return KvLocExtra3.T("bt.title", "Дерево поведения"); } }
        private static string T(string k, string f) { return KvLocExtra3.T(k, f); }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;
            kit.Section(T("bt.title", "Дерево поведения"));
            kit.Buttons(new[]
            {
                T("bt.open", "Открыть редактор"),
                T("bt.run", "Пуск"),
                T("bt.stop", "Стоп"),
                T("bt.export", "В скрипт")
            }, new Action[]
            {
                delegate { if (window != null) window.Toggle(); },
                delegate { if (runner != null) runner.Start(); },
                delegate { if (runner != null) runner.Stop(); },
                delegate
                {
                    if (tree == null) return;
                    string script = tree.ExportScript();
                    KvStageHub4 hub = KvStageHub4.Current;
                    if (hub != null && hub.Script != null) hub.Script.Run(script, "из дерева поведения");
                }
            });
            kit.Info(delegate
            {
                return T("bt.nodes", "Узлов") + ": " + (tree != null ? tree.nodes.Count : 0) +
                       " · " + T("bt.status", "состояние") + ": " +
                       (runner != null ? runner.Result : "—");
            }, KvTheme.Accent);
            kit.Info(delegate
            {
                if (runner == null || runner.Path.Count == 0) return T("bt.nopath", "путь выполнения пуст");
                StringBuilder sb = new StringBuilder(T("bt.path", "Путь") + ": ");
                int from = Mathf.Max(0, runner.Path.Count - 8);
                for (int i = from; i < runner.Path.Count; i++)
                {
                    if (i > from) sb.Append(" → ");
                    KvBtNode node = tree != null ? tree.Find(runner.Path[i]) : null;
                    sb.Append(node != null ? node.title : "?");
                }
                return sb.ToString();
            }, KvTheme.TextMain);
            kit.Note(T("bt.info",
                "Виды узлов: последовательность (все шаги по порядку), выбор (первая успешная ветка), " +
                "действие (команда или мини-скрипт), условие (выражение), пауза (секунды). " +
                "Узлы перетаскиваются мышью в редакторе, дерево выгружается в текст макроса этапа 26."),
                KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
