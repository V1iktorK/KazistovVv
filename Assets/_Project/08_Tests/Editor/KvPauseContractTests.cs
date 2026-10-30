// ================================================================================================
//  KvPauseContractTests.cs — EditMode-тесты ЕДИНОГО КОНТРАКТА ПАУЗЫ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. За два предыдущих раунда найдено ДВА дефекта паузы в исполнителе траекторий:
//    1) на паузе не проверялся зазор (опасно после аварийного останова);
//    2) ПУСК не снимал паузу (робот стоял, хотя пуск вернул успех).
//
//  Оба — про ОДИН контракт, который нигде не был записан словами:
//
//      «ПАУЗА — это состояние МЕЖДУ пусками. Пуск начинает движение всегда.
//       Останов и завершение сбрасывают паузу в исходное состояние.»
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ. Не поведение одного класса, а СОГЛАСОВАННОСТЬ двух исполнителей:
//  траекторий (`TrajectoryExecutor`) и записи (`KvRecordingService`). Они делают похожее —
//  проигрывают движение по времени с паузой, — и разойтись в правилах им нельзя.
//
//  ГРАНИЦА ЧЕСТНОСТИ: это проверка по исходнику. Полное поведение требует сцены и монобеха, но
//  нарушение контракта видно и так: именно оно и было причиной обоих дефектов.
// ================================================================================================

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Единый контракт паузы у исполнителей движения.</summary>
    public class KvPauseContractTests
    {
        private static string FindSource(string fileName)
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            string[] found = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>Тело метода по глубине скобок (за ночь это правило пригодилось пять раз).</summary>
        private static string MethodBody(string fileName, string signature)
        {
            string path = FindSource(fileName);
            Assert.IsNotNull(path, "не найден " + fileName);

            string text = File.ReadAllText(path);
            int start = text.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.Greater(start, 0, "в " + fileName + " нет метода " + signature);

            int open = text.IndexOf('{', start);
            Assert.Greater(open, 0, "не найдено тело " + signature);

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

        // ---------------------------------------------------------------- 1. ПУСК НАЧИНАЕТ

        /// <summary>
        /// ПУСК ИСПОЛНИТЕЛЯ ТРАЕКТОРИЙ СНИМАЕТ ПАУЗУ. Прямая защита от дефекта прошлого раунда.
        /// </summary>
        [Test]
        public void TrajectoryPlayClearsPause()
        {
            string body = MethodBody("TrajectoryExecutor.cs", "public bool Play(");

            Assert.IsTrue(Regex.IsMatch(body, @"Paused\s*=\s*false"),
                "Play() исполнителя траекторий не снимает паузу — ПУСК после паузы не начнёт движение");
        }

        /// <summary>
        /// ПУСК ВОСПРОИЗВЕДЕНИЯ ЗАПИСИ ТОЖЕ СНИМАЕТ ПАУЗУ. Второй исполнитель делает то же самое,
        /// и расхождение здесь значило бы, что «пуск» в одном месте работает, а в другом — нет.
        /// </summary>
        [Test]
        public void RecordingPlayClearsPause()
        {
            string body = MethodBody("KvRecordingService.cs", "public bool Play(");

            Assert.IsTrue(Regex.IsMatch(body, @"playPaused\s*=\s*false"),
                "воспроизведение записи не снимает паузу — ПУСК после паузы не начнёт проигрывание");
        }

        // ---------------------------------------------------------------- 2. ОСТАНОВ СБРАСЫВАЕТ

        /// <summary>
        /// ОСТАНОВ СНИМАЕТ ПАУЗУ у исполнителя траекторий. Иначе после аварийного останова (который
        /// САМ ставит паузу) состояние осталось бы «на паузе», и следующий пуск... впрочем, теперь
        /// пуск снимает паузу сам. Но останавливаться в состоянии «на паузе» всё равно неправильно:
        /// оператор должен видеть остановленное движение, а не замершее.
        /// </summary>
        [Test]
        public void TrajectoryStopClearsPause()
        {
            string body = MethodBody("TrajectoryExecutor.cs", "public void Stop(");

            Assert.IsTrue(Regex.IsMatch(body, @"Paused\s*=\s*false"),
                "Stop() не снимает паузу — после останова состояние останется «на паузе»");
        }

        /// <summary>
        /// ЗАВЕРШЕНИЕ ВОСПРОИЗВЕДЕНИЯ СБРАСЫВАЕТ ПАУЗУ. Иначе следующая запись начнётся «на паузе»
        /// от предыдущей — тот же класс, что дефект ПУСКА.
        /// </summary>
        [Test]
        public void RecordingCompletionClearsPause()
        {
            string path = FindSource("KvRecordingService.cs");
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            // Блок завершения: после `playhead >= playing.duration`.
            int index = text.IndexOf("playhead >= playing.duration", System.StringComparison.Ordinal);
            Assert.Greater(index, 0, "не найден блок завершения воспроизведения");

            string tail = text.Substring(index, System.Math.Min(400, text.Length - index));
            Assert.IsTrue(Regex.IsMatch(tail, @"playPaused\s*=\s*false"),
                "при завершении воспроизведения пауза не сбрасывается — следующая запись " +
                "начнётся «на паузе» от предыдущей");
        }

        // ---------------------------------------------------------------- 3. ПАУЗА ПРОВЕРЯЕТСЯ

        /// <summary>
        /// НА ПАУЗЕ ВСЁ РАВНО ИДЁТ ПРОВЕРКА БЕЗОПАСНОСТИ. Защита от дефекта двух раундов назад:
        /// робот стоит, но обстановка вокруг может измениться.
        /// </summary>
        [Test]
        public void PausedTrajectoryStillChecksSafety()
        {
            string body = MethodBody("TrajectoryExecutor.cs", "private void Update()");

            Match paused = Regex.Match(body, @"if\s*\(\s*Paused\s*\)(.*?)\n",
                RegexOptions.Singleline);
            Assert.IsTrue(paused.Success, "в Update нет ветки паузы");

            // Берём несколько строк после условия — там должен быть вызов проверки.
            string around = body.Substring(System.Math.Max(0, paused.Index), 
                System.Math.Min(400, body.Length - paused.Index));

            StringAssert.Contains("CheckStandingPose", around,
                "на паузе не выполняется проверка безопасности — робот стоит без контроля зазора");
        }

        // ---------------------------------------------------------------- 4. САМОПРОВЕРКА

        /// <summary>
        /// РАЗБОР НАХОДИТ ТЕЛА МЕТОДОВ. Без этого тесты выше зеленели бы на пустых строках (E-16), а
        /// за ночь разбор по строкам уже подводил дважды.
        /// </summary>
        [Test]
        public void MethodBodyParsingWorks()
        {
            string body = MethodBody("TrajectoryExecutor.cs", "public bool Play(");

            Assert.Greater(body.Length, 100,
                "тело Play разобрано как пустое (" + body.Length + " символов) — разбор сломан");
            StringAssert.Contains("validator", body,
                "в разобранном теле нет ожидаемого содержимого — разбор берёт не тот метод");
        }
    }
}
