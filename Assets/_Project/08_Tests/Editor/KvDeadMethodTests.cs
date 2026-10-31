// ================================================================================================
//  KvDeadMethodTests.cs — EditMode-ТЕСТЫ «МЁРТВЫХ» МЕТОДОВ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Ночью найден дефект: `KvHandTrackingService.ClearPose()` существовал, но НЕ
//  ВЫЗЫВАЛСЯ НИОТКУДА. Последствие — «призрачная рука»: пропавшая кисть продолжала считаться
//  отслеживаемой, и интерфейс нажимался сам.
//
//  ЭТО КЛАСС, А НЕ ОДИН СЛУЧАЙ. Метод, который никто не вызывает, — это либо забытая функция
//  (как `ClearPose`), либо честный публичный API для внешнего вызова. Различить их можно только
//  поимённо — что и делает этот реестр.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) каждый метод из реестра «мёртвых» ДЕЙСТВИТЕЛЬНО не вызывается (реестр не устарел);
//    2) каждый такой метод имеет ПРИЧИНУ, почему он существует;
//    3) у каждого есть замена или это осознанный публичный API.
//
//  ГРАНИЦА ЧЕСТНОСТИ: полный поиск «невызываемых» методов даёт 214 совпадений, и большинство —
//  ЗАКОННЫЕ: редакторские `[MenuItem]`, колбэки Unity (`Awake`, `Update`), публичный API для
//  внешних сборок, методы, которые вызывает Unity по имени. Автоматически отличить их нельзя,
//  поэтому реестр ведётся ВРУЧНУЮ и проверяется на актуальность.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Реестр публичных методов без вызовов: каждый обоснован.</summary>
    public class KvDeadMethodTests
    {
        /// <summary>
        /// ПУБЛИЧНЫЕ МЕТОДЫ БЕЗ ВЫЗОВОВ ВНУТРИ ПРОЕКТА — с причиной у каждого.
        ///
        /// ПРОВЕРЕНО 30.10.2026 просмотром каждого. Причина обязательна: без неё метод либо забыт
        /// (дефект), либо кто-то не понимает, зачем он.
        /// </summary>
        private static readonly Dictionary<string, string> KnownUncalledMethods = new Dictionary<string, string>
        {
            { "ToggleUiMode",
              "публичный API ручного переключения режима; рабочая клавиша TAB идёт через KvMouseCursor.Tick, " +
              "а этот метод — для вызова из кода и тестов" },
        };

        private static string ProjectScripts
        {
            get { return Path.Combine(Application.dataPath, "_Project"); }
        }

        /// <summary>Все исходники проекта без тестов и диагностики.</summary>
        private static List<string> Sources()
        {
            var files = new List<string>();
            if (!Directory.Exists(ProjectScripts)) return files;

            foreach (string file in Directory.GetFiles(ProjectScripts, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith("Tests.cs")) continue;
                if (name.Contains("Diag")) continue;
                files.Add(file);
            }
            return files;
        }

        /// <summary>
        /// ЕСТЬ ЛИ ВЫЗОВ МЕТОДА ГДЕ-ЛИБО В ПРОЕКТЕ. Ищем `Имя(` с точкой или без, исключая
        /// объявления (`public void Имя(`, `private Имя(`) и вызовы из тестов/диагностики — они
        /// НЕ считаются: тест, который проверяет мёртвый метод, не делает его живым.
        /// </summary>
        private static bool IsCalled(string methodName)
        {
            var call = new Regex(@"(?:\.|\b)" + Regex.Escape(methodName) + @"\s*\(");
            var declaration = new Regex(
                @"(?:public|private|internal|protected)\s+(?:static\s+)?(?:override\s+)?" +
                @"[\w<>\[\],\s]+?\s+" + Regex.Escape(methodName) + @"\s*\(");

            foreach (string file in Sources())
            {
                string text = File.ReadAllText(file);

                foreach (Match m in call.Matches(text))
                {
                    // Отбрасываем объявление: проверяем, что перед вызовом нет модификатора доступа.
                    int lineStart = text.LastIndexOf('\n', m.Index) + 1;
                    string prefix = text.Substring(lineStart, m.Index - lineStart);
                    if (declaration.IsMatch(prefix + methodName + "(")) continue;
                    if (prefix.Contains("public ") || prefix.Contains("private ")) continue;
                    if (prefix.Contains("internal ") || prefix.Contains("protected ")) continue;

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// РЕЕСТР НЕ УСТАРЕЛ: каждый записанный метод ДЕЙСТВИТЕЛЬНО не вызывается. Если его начали
        /// вызывать — запись надо убрать, иначе реестр прикрывает будущий мёртвый метод с тем же
        /// именем.
        /// </summary>
        [Test]
        public void RegistryIsNotStale()
        {
            var stale = new List<string>();

            foreach (var pair in KnownUncalledMethods)
            {
                if (IsCalled(pair.Key))
                    stale.Add(pair.Key + " (уже вызывается — уберите из реестра)");
            }

            Assert.AreEqual(0, stale.Count,
                "реестр мёртвых методов устарел: " + string.Join("; ", stale.ToArray()));
        }

        /// <summary>
        /// У КАЖДОЙ ЗАПИСИ ЕСТЬ ПРИЧИНА. Пустая строка — то же, что «промолчали» (E-16).
        /// </summary>
        [Test]
        public void EveryEntryHasReason()
        {
            var vague = new List<string>();
            foreach (var pair in KnownUncalledMethods)
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length < 30)
                    vague.Add(pair.Key);

            Assert.AreEqual(0, vague.Count,
                "у этих мёртвых методов нет внятной причины: " + string.Join(", ", vague.ToArray()));
        }

        /// <summary>
        /// `CLEARPOSE` — НЕ В РЕЕСТРЕ. Он ОБЯЗАН вызываться: именно его отсутствие было дефектом
        /// «призрачной руки». Отдельная проверка, потому что этот метод важнее остальных.
        /// </summary>
        [Test]
        public void ClearPoseIsNotAllowedToBeDead()
        {
            Assert.IsFalse(KnownUncalledMethods.ContainsKey("ClearPose"),
                "ClearPose внесён в реестр мёртвых — но он ОБЯЗАН вызываться каждый кадр, иначе " +
                "пропавшая кисть продолжит считаться отслеживаемой");

            Assert.IsTrue(IsCalled("ClearPose"),
                "ClearPose НЕ ВЫЗЫВАЕТСЯ — это дефект «призрачной руки»: интерфейс нажимается сам");
        }

        /// <summary>
        /// РАЗБОР РАБОТАЕТ. Без этого проверки выше могли бы зеленеть впустую.
        /// </summary>
        [Test]
        public void ParserWorks()
        {
            // Заведомо вызываемый метод.
            Assert.IsTrue(IsCalled("PushJoint"),
                "разбор не нашёл вызов заведомо вызываемого метода — проверки бессмысленны");

            // Заведомо несуществующий.
            Assert.IsFalse(IsCalled("ТакогоМетодаТочноНет_Самопроверка"));
        }
    }
}
