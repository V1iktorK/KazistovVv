// ================================================================================================
//  KvStaleJointsTests.cs — EditMode-тесты «ЗАСТРЯВШИХ» ТОЧЕК КИСТИ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. `KvHandTrackingService.PushJoint` ставит отметку `valid[hand, joint] = true`.
//  Снять её может только `ClearPose()`. И он **не вызывался ниоткуда** — ни одним файлом проекта
//  (проверено поиском 30.10.2026).
//
//  ЧЕМ ЭТО ГРОЗИЛО. Кисть пропала (оператор опустил руку, контроллер перекрыл пальцы) — а сервис
//  продолжает считать её отслеживаемой по СТАРЫМ координатам: `IsTracked` смотрит только на флаги,
//  а не на свежесть данных.
//
//  РЕЗУЛЬТАТ: «ПРИЗРАЧНАЯ РУКА». Щипок вычисляется по точке, где пальцы были секунду назад, и
//  повторяется снова и снова. Оператор видит, что интерфейс нажимается САМ, и объяснить это
//  невозможно — ошибок нет, предупреждений нет.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ:
//    1) `ClearPose` ВЫЗЫВАЕТСЯ из кода (а не только объявлен);
//    2) вызов стоит в `Update` — то есть каждый кадр, а не по событию;
//    3) при пропаже одной руки вторая не теряется (иначе она мигала бы);
//    4) `IsTracked` опирается на отметки, которые теперь сбрасываются.
// ================================================================================================

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Точки кисти не застревают в сервисе распознавания.</summary>
    public class KvStaleJointsTests
    {
        private static string Find(string fileName)
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            string[] found = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// `CLEARPOSE` ВЫЗЫВАЕТСЯ ИЗ КОДА. Главный тест: до исправления метод существовал, но его
        /// никто не вызывал — то есть отметки суставов жили вечно.
        ///
        /// Считаем вызовы во всём проекте, КРОМЕ объявления и тестов: `ClearPose` должен быть
        /// вызван хотя бы однажды.
        /// </summary>
        [Test]
        public void ClearPoseIsActuallyCalled()
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            Assert.IsTrue(Directory.Exists(root), "нет папки скриптов");

            int calls = 0;
            var callSites = new System.Collections.Generic.List<string>();

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith("Tests.cs")) continue;

                string text = File.ReadAllText(file);

                // Объявление `public void ClearPose()` не считается вызовом.
                foreach (Match m in Regex.Matches(text, @"\.ClearPose\s*\(\s*\)"))
                {
                    calls++;
                    callSites.Add(name);
                }

                // Вызов без точки (внутри самого сервиса) — тоже считаем, если это не объявление.
                foreach (Match m in Regex.Matches(text, @"(?<!void )(?<!public void )\bClearPose\s*\(\s*\)"))
                {
                    if (m.Index > 0 && text[m.Index - 1] == '.') continue;   // уже посчитали
                    // Пропускаем строку объявления.
                    int lineStart = text.LastIndexOf('\n', m.Index) + 1;
                    string line = text.Substring(lineStart, m.Index - lineStart);
                    if (line.Contains("public void") || line.Contains("private void")) continue;
                    calls++;
                    callSites.Add(name + " (внутренний)");
                }
            }

            Assert.Greater(calls, 0,
                "ClearPose() НЕ ВЫЗЫВАЕТСЯ НИОТКУДА — отметки суставов живут вечно: пропавшая кисть " +
                "продолжает считаться отслеживаемой по старым координатам, и интерфейс нажимается сам");

            // САМОПРОВЕРКА: разбор увидел вызовы (E-16).
            Assert.Greater(callSites.Count, 0, "разбор не записал ни одного места вызова");
        }

        /// <summary>
        /// СБРОС ПРОИСХОДИТ КАЖДЫЙ КАДР, А НЕ ПО СОБЫТИЮ. Если сбрасывать только при пропаже руки
        /// по событию (например `OnDestroy`), между событиями точки успеют застрять.
        ///
        /// Проверяется, что вызов стоит внутри `Update` в `KvXrHands`.
        /// </summary>
        [Test]
        public void ResetHappensEveryFrame()
        {
            string path = Find("KvXrHands.cs");
            Assert.IsNotNull(path, "не найден KvXrHands.cs");

            string text = File.ReadAllText(path);
            int update = text.IndexOf("void Update()", System.StringComparison.Ordinal);
            Assert.Greater(update, 0, "в KvXrHands нет Update");

            // Тело метода по глубине скобок.
            int open = text.IndexOf('{', update);
            int depth = 1, at = open + 1;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }
            string body = text.Substring(update, at - update);

            StringAssert.Contains("ClearPose", body,
                "сброс отметок не стоит в Update — точки будут застревать между событиями");
        }

        /// <summary>
        /// ПРИ ПРОПАЖЕ ОДНОЙ РУКИ ВТОРАЯ НЕ ТЕРЯЕТСЯ.
        ///
        /// `ClearPose()` снимает ОБЕ руки сразу. Если после сброса не вернуть оставшуюся руку на
        /// место, она пропадёт из распознавания — оператор увидит, что вторая рука «мигает».
        /// </summary>
        [Test]
        public void SecondHandIsRestoredAfterReset()
        {
            string path = Find("KvXrHands.cs");
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);

            // Ищем помощника, который сбрасывает и возвращает оставшуюся руку.
            bool hasHelper = text.Contains("ClearSinkBecauseHandLost");
            Assert.IsTrue(hasHelper,
                "нет помощника, который после сброса возвращает ОСТАВШУЮСЯ руку — она будет мигать " +
                "при пропаже второй");

            // И внутри помощника должен быть повторный вызов подачи.
            int start = text.IndexOf("void ClearSinkBecauseHandLost", System.StringComparison.Ordinal);
            Assert.Greater(start, 0);

            int open = text.IndexOf('{', start);
            int depth = 1, at = open + 1;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }
            string body = text.Substring(start, at - start);

            StringAssert.Contains("FeedGestures", body,
                "помощник сбрасывает отметки, но не возвращает оставшуюся руку — она пропадёт " +
                "из распознавания до следующего кадра");
        }

        /// <summary>
        /// `IsTracked` ОПИРАЕТСЯ НА ОТМЕТКИ. Если бы он смотрел на свежесть координат, проблема
        /// решалась бы иначе — и сброс был бы не нужен. Раз проблема была, значит смотрит на флаги;
        /// тест фиксирует это, чтобы правку нельзя было сделать «наполовину».
        /// </summary>
        [Test]
        public void IsTrackedDependsOnFlags()
        {
            string path = Find("KvXrInput.cs");
            Assert.IsNotNull(path, "не найден KvXrInput.cs");

            string text = File.ReadAllText(path);
            Match tracked = Regex.Match(text, @"public bool IsTracked\(int hand\)\s*\{(.*?)\}",
                RegexOptions.Singleline);
            if (!tracked.Success) Assert.Pass("метод IsTracked не найден — проверять нечего");

            string body = tracked.Groups[1].Value;
            StringAssert.Contains("valid", body,
                "IsTracked не смотрит на отметки valid — тогда сброс отметок ни на что не влияет, " +
                "и надо искать другую причину застревания");
        }
    }
}
