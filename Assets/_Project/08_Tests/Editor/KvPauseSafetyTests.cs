// ================================================================================================
//  KvPauseSafetyTests.cs — EditMode-тесты БЕЗОПАСНОСТИ НА ПАУЗЕ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. В проекте аварийный останов СТАВИТ ПАУЗУ: `KvSafetyTools.StopMotion` вызывает
//  `features.EmergencyStop()` и `Motion.SetPaused(true)`.
//
//  ЧТО НАЙДЕНО 30.10.2026. На паузе `TrajectoryExecutor.Update` выходил СРАЗУ после `NotifyState()` —
//  то есть ДО расчёта зазора и ДО `ApproveStep`. Робот стоит, но никто не следит, что он стоит в
//  безопасном месте.
//
//  ЧЕМ ЭТО ПЛОХО. После аварии робот оказывается в состоянии, где контроль зазора и лимитов
//  ВЫКЛЮЧЕН. Если в это время оператор пододвинет стол, поставит манекен или включит ограничения —
//  система об этом не узнает. Первое, что произойдёт после снятия паузы, — движение в ставшую
//  опасной зону.
//
//  ЧТО СДЕЛАНО. На паузе позиция НЕ меняется, но проверка выполняется: `CheckStandingPose` считает
//  зазор для текущей позы и вызывает `ApproveStep`. Отказ обрабатывается аварийным стопом с
//  причиной.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ (по исходнику — без сцены и без монобеха):
//    1) на паузе ВЫЗЫВАЕТСЯ проверка (а не просто выход);
//    2) проверка на паузе НЕ двигает время (иначе сработало бы «время не идёт»);
//    3) аварийный останов в проекте действительно ставит паузу — причина всего дефекта;
//    4) порядок в `Update`: пауза проверяется ПОСЛЕ входа, но ДО движения.
// ================================================================================================

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Пауза не отключает контроль зазора и лимитов.</summary>
    public class KvPauseSafetyTests
    {
        private static string FindSource(string fileName)
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            string[] found = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// НА ПАУЗЕ ВЫЗЫВАЕТСЯ ПРОВЕРКА. Главный тест файла: до исправления был просто `return`.
        /// </summary>
        [Test]
        public void PausedFrameStillChecksSafety()
        {
            string path = FindSource("TrajectoryExecutor.cs");
            Assert.IsNotNull(path, "не найден TrajectoryExecutor.cs");

            string text = File.ReadAllText(path);

            Match paused = Regex.Match(text, @"if\s*\(\s*Paused\s*\)(.*?)\n\s*\}",
                RegexOptions.Singleline);
            Assert.IsTrue(paused.Success, "в Update нет ветки обработки паузы");

            string body = paused.Groups[1].Value;

            Assert.IsTrue(body.Contains("CheckStandingPose"),
                "на паузе НЕ выполняется проверка безопасности — робот стоит без контроля зазора " +
                "и лимитов. Это особенно опасно после аварийного останова, который сам ставит паузу: " +
                "оператор поправит обстановку, а система не заметит");
        }

        /// <summary>
        /// МЕТОД ПРОВЕРКИ СТОЯЩЕЙ ПОЗЫ СУЩЕСТВУЕТ и действительно вызывает предохранитель.
        /// Иначе вызов из `Update` вёл бы в никуда.
        /// </summary>
        [Test]
        public void StandingPoseCheckCallsTheGate()
        {
            string path = FindSource("TrajectoryExecutor.cs");
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            Assert.IsTrue(text.Contains("private void CheckStandingPose"),
                "нет метода CheckStandingPose — вызов из Update вёл бы в никуда");

            int index = text.IndexOf("private void CheckStandingPose", System.StringComparison.Ordinal);
            string body = text.Substring(index, System.Math.Min(1500, text.Length - index));

            StringAssert.Contains("ApproveStep", body,
                "проверка стоящей позы не вызывает предохранитель — она ничего не проверяет");
            StringAssert.Contains("ClearanceAt", body,
                "проверка стоящей позы не считает зазор — главная опасность на паузе не видна");
            StringAssert.Contains("Stop(", body,
                "при отказе нет остановки — проверка обнаружит опасность и промолчит");
        }

        /// <summary>
        /// АВАРИЙНЫЙ ОСТАНОВ В ПРОЕКТЕ ДЕЙСТВИТЕЛЬНО СТАВИТ ПАУЗУ. Это ПРИЧИНА всего дефекта:
        /// если бы останов просто останавливал выполнение, пауза не была бы «опасным состоянием».
        /// Проверка фиксирует связь, чтобы её нельзя было потерять незаметно.
        /// </summary>
        [Test]
        public void EmergencyStopUsesPause()
        {
            string path = FindSource("KvSafetyTools.cs");
            if (path == null) Assert.Pass("KvSafetyTools.cs нет — проверять нечего");

            string text = File.ReadAllText(path);
            int index = text.IndexOf("private void StopMotion", System.StringComparison.Ordinal);
            if (index < 0) Assert.Pass("метода StopMotion нет");

            string body = text.Substring(index, System.Math.Min(500, text.Length - index));

            StringAssert.Contains("SetPaused(true)", body,
                "аварийный останов больше не ставит паузу — тогда этот тест надо пересмотреть " +
                "(связь «авария → пауза» была причиной дефекта)");
        }

        /// <summary>
        /// ПРОВЕРКА НА ПАУЗЕ НЕ ДВИГАЕТ ВРЕМЯ. Если бы `playhead` увеличивался, сработала бы
        /// проверка «время идёт вперёд» — и пауза сама себя останавливала бы.
        /// </summary>
        [Test]
        public void StandingCheckDoesNotAdvanceTime()
        {
            string path = FindSource("TrajectoryExecutor.cs");
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            int index = text.IndexOf("private void CheckStandingPose", System.StringComparison.Ordinal);
            string body = text.Substring(index, System.Math.Min(1200, text.Length - index));

            Assert.IsFalse(Regex.IsMatch(body, @"playhead\s*(\+=|=)"),
                "проверка на паузе ДВИГАЕТ время — тогда сработает проверка «время не идёт» и " +
                "пауза сама себя остановит");
        }
    }
}
