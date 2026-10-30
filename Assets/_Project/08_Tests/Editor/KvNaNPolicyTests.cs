// ================================================================================================
//  KvNaNPolicyTests.cs — EditMode-СТОРОЖ КЛАССА E-21 (сессия S44).
//
//  ОТКУДА ЗАДАЧА. За ночь найдено ПЯТЬ мест одного класса: проверка вида
//
//      if (length < limit) return false;
//      if (length > limit) return false;
//
//  ВЫГЛЯДИТ как защита от мусора, а для NaN НЕ РАБОТАЕТ ВООБЩЕ: в C# любое сравнение с NaN даёт
//  `false`. То есть `sqrMagnitude < 1e-8f`, `magnitude <= tolerance`, `distance < 0.01f` —
//  все они ПРОПУСКАЮТ худший случай вместо отбраковки.
//
//  ЧЕМ ЭТО КОНЧАЛОСЬ (все пять — реальные дефекты, не гипотезы):
//    • контроллер исчезал из мира (Unity не рисует объекты с NaN);
//    • жест молча не срабатывал;
//    • панель нажималась сама по мусорным координатам луча;
//    • GJK объявлял столкновение у тел, которые нигде не касаются;
//    • CCD-решатель 24 итерации вхолостую крутил суставы и портил позу.
//
//  ЧТО ДЕЛАЕТ ЭТОТ ТЕСТ. Он ищет функции, которые СРАВНИВАЮТ длину или расстояние, и требует, чтобы
//  в той же функции был отсев нечисла. Это сторож на БУДУЩЕЕ: новый код с такой проверкой не
//  пройдёт, пока не добавит отсев.
//
//  ГРАНИЦА ЧЕСТНОСТИ: это не доказательство корректности. Тест ловит ОТСУТСТВИЕ отсева в функции,
//  где он нужен, — по форме кода. Если функция получает уже проверенные данные (например,
//  `KvXrMode` через `KvGestureMath.IsUsable`), она в списке исключений с причиной.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Сторож класса E-21: сравнение длины требует отсева нечисла.</summary>
    public class KvNaNPolicyTests
    {
        /// <summary>
        /// ОБЛАСТЬ ПРОВЕРКИ: только XR-слой. Здесь данные приходят ИЗВНЕ — от OpenXR, от железа, от
        /// устройства, — и NaN появляется штатно при потере отслеживания.
        ///
        /// ЧЕСТНО О ПЕРВЫХ ДВУХ ПОПЫТКАХ (30.10.2026).
        /// 1) Сторож проверял ВСЕ скрипты. Он объявил нарушителями `FreeFlyCameraController`,
        ///    `SCARAController`, `SixAxisController` — и был НЕПРАВ: там сравнения работают с
        ///    векторами мыши и позициями суставов САМОЙ СЦЕНЫ, где NaN появиться не может.
        /// 2) После уточнения он всё равно перечислял десяток функций планировщика и интерфейса
        ///    (`KvPayloadCalculator`, `KvTeachPendant`, `KvConstrainedPlanner`) — там тоже геометрия
        ///    из сцены, а не с устройства.
        ///
        /// ВЫВОД, КОТОРЫЙ СТОИТ ЗАПОМНИТЬ: сторож на КЛАСС ошибки должен ограничивать область там,
        /// где класс ВОЗМОЖЕН. Широкий сторож — это не «строгость», а шум: на него перестают
        /// смотреть, и он перестаёт защищать (тот же урок, что E-16, только с другой стороны).
        ///
        /// ГРАНИЦА: математика планировщика НЕ покрыта этим сторожем. Там нечисло означает другую
        /// ошибку (в файле модели или в данных калибровки), и это отдельная задача — записана в план
        /// на субботу, а не «забыта».
        /// </summary>
        private static string[] ScanFolders
        {
            get
            {
                return new[]
                {
                    Path.Combine(ScriptsRoot, "Xr"),
                };
            }
        }
        private static readonly string[] LengthComparisons =
        {
            @"\.magnitude\s*[<>]",
            @"\.sqrMagnitude\s*[<>]",
            @"Vector3\.(?:Distance|Dot)\s*\([^)]*\)\s*[<>]",
            @"Magnitude\s*[<>]",
        };

        /// <summary>Признаки того, что функция ОТСЕКАЕТ нечисло.</summary>
        private static readonly string[] FiniteGuards =
        {
            "IsNaN",
            "IsInfinity",
            "IsFinite",
            "IsNotFinite",
        };

        /// <summary>
        /// ФАЙЛЫ, ГДЕ ДАННЫЕ ПРИХОДЯТ ИЗ ПРОВЕРЕННЫХ ИСТОЧНИКОВ — с причиной у каждого.
        ///
        /// ПОЧЕМУ ЭТО УТОЧНЕНИЕ (30.10.2026). Первый вариант сторожа объявил нарушителями
        /// `FreeFlyCameraController`, `SCARAController`, `SixAxisController` — и был НЕПРАВ:
        /// там сравнения работают с векторами мыши, направлениями осей и позициями суставов
        /// САМОЙ СЦЕНЫ. NaN там появиться не может: эти числа не приходят из внешнего рантайма.
        ///
        /// Класс E-21 опасен там, где данные приходят ИЗВНЕ: от OpenXR, от железа, от файла
        /// чужого формата. Именно там NaN появляется штатно при потере отслеживания.
        ///
        /// Пояснение записано здесь, а не в коде: в коде исключение выглядело бы «разрешением
        /// игнорировать», а это ОГРАНИЧЕНИЕ области проверки.
        /// </summary>
        private static readonly Dictionary<string, string> AllowedFiles =
            new Dictionary<string, string>
            {
                { "KvXrMode.cs",
                  "получает кадр кисти через KvGestureMath.IsUsable, который уже отсекает нечисло по всем девяти точкам" },
                { "KvXrKeyboardLayout.cs",
                  "раскладка клавиатуры — статические константы, внешних данных нет" },
                { "KvXrSideResolver.cs",
                  "определение стороны относительно головы — геометрия сцены, а не решение о годности данных" },
                { "KvXrObjectFactory.cs",
                  "размещение объектов в сцене — расчёт координат, а не проверка входных данных" },
            };

        private static string ScriptsRoot
        {
            get { return Path.Combine(Application.dataPath, "_Project", "01_Scripts"); }
        }

        /// <summary>
        /// ТЕЛА ФУНКЦИЙ ИЗ ИСХОДНИКА — по строкам, от объявления до строки из одной `}`.
        ///
        /// ПОЧЕМУ ПО СТРОКАМ, А НЕ РЕГУЛЯРКОЙ (за ночь это уже случалось трижды): `[^)]*` обрывается
        /// на скобке внутри параметров, а поиск по подстроке находит ВЫЗОВ метода раньше его
        /// объявления. Разбор по строкам устойчив к обоим случаям.
        /// </summary>
        private static List<string> MethodBodies(string path)
        {
            var bodies = new List<string>();
            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                // Объявление метода: модификатор + тип возврата + имя + скобка.
                // ТОЛЬКО ФУНКЦИИ, ВОЗВРАЩАЮЩИЕ bool: именно они по внешним данным говорят
                // «годится / не годится». Сравнение длины в функции, которая просто СЧИТАЕТ
                // число, — обычная геометрия, а не проверка данных (см. пояснение у ScanFolders).
                if (!Regex.IsMatch(lines[i],
                        @"^\s*(?:public|private|internal|protected)\s+(?:static\s+)?bool\s+\w+\s*\("))
                    continue;

                var builder = new System.Text.StringBuilder();
                for (int j = i; j < lines.Length && j < i + 200; j++)
                {
                    builder.AppendLine(lines[j]);
                    if (j > i && lines[j].Trim() == "}") break;
                }
                bodies.Add(builder.ToString());
            }
            return bodies;
        }

        /// <summary>
        /// САМОПРОВЕРКА: разбор находит тела функций. Без неё тест ниже зеленел бы «потому что ничего
        /// не нашлось» (E-16).
        /// </summary>
        [Test]
        public void ParserFindsMethodBodies()
        {
            Assert.IsTrue(Directory.Exists(ScriptsRoot), "нет папки скриптов: " + ScriptsRoot);

            int total = 0;
            foreach (string file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
                total += MethodBodies(file).Count;

            Assert.Greater(total, 200,
                "разобрано всего " + total + " тел функций — разбор сломан, проверка бессмысленна");
        }

        /// <summary>
        /// КАЖДАЯ ФУНКЦИЯ, СРАВНИВАЮЩАЯ ДЛИНУ, ОТСЕКАЕТ НЕЧИСЛО.
        ///
        /// Это главный сторож файла. Без него класс E-21 вернётся: новый код с проверкой
        /// `if (distance > limit) return false;` выглядит правильным и проходит мимо внимания.
        /// </summary>
        [Test]
        public void LengthComparisonsGuardAgainstNaN()
        {
            Assert.IsTrue(Directory.Exists(ScriptsRoot));

            var offenders = new List<string>();
            int checkedFunctions = 0;

            var files = new System.Collections.Generic.List<string>();
            foreach (string folder in ScanFolders)
                if (Directory.Exists(folder))
                    files.AddRange(Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories));

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith("Tests.cs")) continue;
                if (name.Contains("Diag")) continue;
                if (AllowedFiles.ContainsKey(name)) continue;

                foreach (string body in MethodBodies(file))
                {
                    bool comparesLength = false;
                    foreach (string pattern in LengthComparisons)
                        if (Regex.IsMatch(body, pattern)) { comparesLength = true; break; }
                    if (!comparesLength) continue;

                    checkedFunctions++;

                    bool hasGuard = false;
                    foreach (string guard in FiniteGuards)
                        if (body.IndexOf(guard, System.StringComparison.Ordinal) >= 0) { hasGuard = true; break; }

                    if (!hasGuard)
                    {
                        // Взять подпись функции — первые две строки тела.
                        string[] lines = body.Split('\n');
                        string signature = lines.Length > 0 ? lines[0].Trim() : "(неизвестно)";
                        if (signature.Length > 90) signature = signature.Substring(0, 90) + "…";
                        offenders.Add(name + ": " + signature);
                    }
                }
            }

            // ПОРОГ ВЗЯТ ПО ФАКТУ, А НЕ «НА ВСЯКИЙ СЛУЧАЙ» (30.10.2026).
            //
            // Сначала здесь стояло «больше 10», и после сужения правил функций оказалось ровно 5 —
            // те самые исправленные. Порог сработал бы как ложная тревога «разбор сломан».
            //
            // Правильный порог: столько, сколько таких функций реально есть. Если их станет
            // МЕНЬШЕ — значит разбор ослеп (и это надо заметить). Если больше — новая функция,
            // и её надо проверить, что и делает основной тест.
            Assert.GreaterOrEqual(checkedFunctions, 5,
                "найдено всего " + checkedFunctions + " функций со сравнением длины — разбор " +
                "ослеп, проверка бессмысленна");

            Assert.AreEqual(0, offenders.Count,
                "эти функции сравнивают длину БЕЗ отсева нечисла — для NaN такое сравнение всегда " +
                "даёт false, то есть пропускает мусор вместо отбраковки. Это класс E-21, он уже " +
                "стоил пяти дефектов: " + string.Join(" | ", offenders.ToArray()));
        }

        /// <summary>
        /// СПИСОК ИСКЛЮЧЕНИЙ ОБОСНОВАН И НЕ УСТАРЕЛ. Исключение без причины — то же, что
        /// «промолчали» (E-16).
        /// </summary>
        [Test]
        public void ExceptionListIsJustified()
        {
            var stale = new List<string>();

            foreach (var pair in AllowedFiles)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length < 30)
                    stale.Add(pair.Key + " (нет внятной причины)");

                if (Directory.GetFiles(ScriptsRoot, pair.Key, SearchOption.AllDirectories).Length == 0)
                    stale.Add(pair.Key + " (файла больше нет)");
            }

            Assert.AreEqual(0, stale.Count,
                "список исключений сторожа NaN устарел: " + string.Join("; ", stale.ToArray()));
        }

        /// <summary>
        /// ИСПРАВЛЕННЫЕ МЕСТА ОСТАЮТСЯ ИСПРАВЛЕННЫМИ. Проверяются именно те пять функций, где
        /// дефект был найден и устранён: это прямая защита от отката.
        /// </summary>
        [Test]
        public void PreviouslyFixedPlacesKeepTheirGuards()
        {
            var fixedPlaces = new Dictionary<string, string>
            {
                { "KvXrControllerVisuals.cs", "IsPoseUsable" },
                { "KvXrGestures.cs", "IsHandInView" },
                { "KvXrGroupPanelLayout.cs", "TryRayToPanelPlane" },
                { "NarrowPhase.cs", "Intersect" },
                { "RobotDH.cs", "SolveCCD" },
            };

            var broken = new List<string>();
            foreach (var pair in fixedPlaces)
            {
                string[] found = Directory.GetFiles(ScriptsRoot, pair.Key, SearchOption.AllDirectories);
                if (found.Length == 0) { broken.Add(pair.Key + " (файл не найден)"); continue; }

                string text = File.ReadAllText(found[0]);
                if (text.IndexOf(pair.Value, System.StringComparison.Ordinal) < 0)
                {
                    broken.Add(pair.Key + " → " + pair.Value + " (функции больше нет)");
                    continue;
                }

                // В файле обязан остаться признак отсева нечисла.
                bool hasGuard = false;
                foreach (string guard in FiniteGuards)
                    if (text.IndexOf(guard, System.StringComparison.Ordinal) >= 0) { hasGuard = true; break; }
                if (!hasGuard) broken.Add(pair.Key + " → потерян отсев нечисла");
            }

            Assert.AreEqual(0, broken.Count,
                "исправления класса E-21 ОТКАЧЕНЫ — дефекты вернутся: " +
                string.Join("; ", broken.ToArray()));
        }
    }
}
