// ================================================================================================
//  KvPlayAfterPauseTests.cs — EditMode-тесты ПУСКА ПОСЛЕ ПАУЗЫ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Найдено при разборе потока исполнения 30.10.2026.
//
//  ЧТО БЫЛО. `TrajectoryExecutor.Play` не сбрасывал `Paused`. Сценарий оператора:
//    1. Поставил паузу.
//    2. Выбрал другую траекторию и нажал ПУСК.
//    3. `Play` вернул `true`, статус показал метку новой траектории.
//    4. `Update` на первом кадре увидел `Paused == true` и вышел.
//    5. РОБОТ НЕ ДВИГАЛСЯ.
//
//  Оператор видел: «ПУСК нажат, статус правильный, а робот стоит». Ни ошибки, ни предупреждения —
//  просто ничего не происходит. Это хуже явного отказа: непонятно, ждать или жать ещё раз.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ (по исходнику — без сцены):
//    1) `Play` СНИМАЕТ паузу;
//    2) снятие происходит ПОСЛЕ проверок (иначе отказ оставлял бы «пауза снята, движения нет»);
//    3) `Stop` по-прежнему снимает паузу (иначе после аварии нельзя было бы запустить заново);
//    4) `Paused` не сбрасывается при входе в `Play` ДО отказа — см. проверку порядка.
// ================================================================================================

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Пуск после паузы: команда «начать» действительно начинает.</summary>
    public class KvPlayAfterPauseTests
    {
        private static string FindSource(string fileName)
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            string[] found = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// Тело метода `Play` из исходника.
        ///
        /// ЧЕСТНО О ПЕРВОЙ ПОПЫТКЕ (30.10.2026). Границы брались «до первой строки из одних `}`» —
        /// и это НЕВЕРНО для метода с вложенными блоками: внутри `Play` есть `if (...) { ... }`,
        /// его закрывающая скобка тоже стоит в отдельной строке. «Тело» обрывалось на ней, и тест
        /// не находил ни проверки предохранителя, ни снятия паузы — то есть объявлял дефект там,
        /// где всё правильно.
        ///
        /// Поэтому границы считаются ПО ГЛУБИНЕ СКОБОК: тело кончается там, где глубина возвращается
        /// к нулю. Так вложенность ни на что не влияет.
        /// </summary>
        private static string MethodBody(string methodSignature)
        {
            string path = FindSource("TrajectoryExecutor.cs");
            Assert.IsNotNull(path, "не найден TrajectoryExecutor.cs");

            string text = File.ReadAllText(path);
            int start = text.IndexOf(methodSignature, System.StringComparison.Ordinal);
            Assert.Greater(start, 0, "в TrajectoryExecutor.cs нет метода " + methodSignature);

            int open = text.IndexOf('{', start);
            Assert.Greater(open, 0, "не найдено тело метода " + methodSignature);

            int depth = 1;
            int at = open + 1;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }

            return text.Substring(start, at - start);
        }

        /// <summary>Тело `Play`.</summary>
        private static string PlayBody()
        {
            return MethodBody("public bool Play(");
        }

        /// <summary>
        /// ПУСК СНИМАЕТ ПАУЗУ. Главный тест: без этого пуск «срабатывал» и не начинал движение.
        /// </summary>
        [Test]
        public void PlayClearsPause()
        {
            string body = PlayBody();

            Assert.IsTrue(Regex.IsMatch(body, @"Paused\s*=\s*false"),
                "Play() НЕ снимает паузу — если оператор нажал ПУСК на паузе, робот не поедет, " +
                "хотя пуск вернул успех и статус показывает траекторию. Ни ошибки, ни предупреждения");
        }

        /// <summary>
        /// ПАУЗА СНИМАЕТСЯ ПОСЛЕ ПРОВЕРОК. Если снимать её в начале метода, то при ОТКАЗЕ
        /// предохранителя оператор увидит «пауза снята», но движения не будет — и решит, что
        /// проблема в роботе, а не в отказе.
        ///
        /// Проверяется порядок: `Paused = false` стоит ПОСЛЕ вызова `Approve`.
        /// </summary>
        [Test]
        public void PauseIsClearedAfterSafetyChecks()
        {
            string body = PlayBody();

            int approve = body.IndexOf("Approve(", System.StringComparison.Ordinal);
            int clearPause = body.IndexOf("Paused = false", System.StringComparison.Ordinal);

            Assert.Greater(approve, 0, "в Play нет вызова предохранителя — проверять нечего");
            Assert.Greater(clearPause, 0, "в Play нет снятия паузы");
            Assert.Greater(clearPause, approve,
                "пауза снимается ДО проверки предохранителя: при отказе оператор увидит «пауза " +
                "снята», но движения не будет — и решит, что проблема в роботе");
        }

        /// <summary>
        /// ОСТАНОВ ПО-ПРЕЖНЕМУ СНИМАЕТ ПАУЗУ. Иначе после аварийного останова (который САМ ставит
        /// паузу) запустить заново было бы невозможно: пуск снимает паузу, но останов её вернул бы.
        /// </summary>
        [Test]
        public void StopAlsoClearsPause()
        {
            string body = MethodBody("public void Stop(");

            Assert.IsTrue(Regex.IsMatch(body, @"Paused\s*=\s*false"),
                "Stop() не снимает паузу — после аварийного останова (он сам ставит паузу) " +
                "запустить движение заново будет невозможно");
        }

        /// <summary>
        /// ПУСК ВСЁ ЕЩЁ ПРОВЕРЯЕТ ТРАЕКТОРИЮ. Обратная ошибка — «пуск снимает паузу и запускает
        /// что угодно» — была бы опасна. Проверяется, что вызов предохранителя остался.
        /// </summary>
        [Test]
        public void PlayStillChecksSafety()
        {
            string body = PlayBody();

            StringAssert.Contains("Approve(", body,
                "Play больше не проверяет траекторию перед пуском — движение начнётся без проверки");
            Assert.IsTrue(Regex.IsMatch(body, @"return\s+false"),
                "в Play нет ни одного отказа — значит он всегда возвращает успех");
        }
    }
}
