// ================================================================================================
//  KvModeIndicatorTests.cs — EditMode-тесты ПОДПИСИ РЕЖИМА (сессия S44, пункт 5.19 плана).
//
//  ОТКУДА ЗАДАЧА. TAB переключает ДВА режима (§26): в режиме камеры системный курсор скрыт и
//  мышь вращает камеру, в режиме интерфейса — наоборот. Переключение НИЧЕМ не показывалось:
//  оператор нажимал TAB, курсор пропадал, и «почему не нажимаются кнопки» / «почему камера
//  крутится» можно было понять только по памяти.
//
//  ЧТО ПРОВЕРЯЕТСЯ (без сцены и без отрисовки):
//    1) подпись НАЗЫВАЕТ режим и РАЗЛИЧАЕТСЯ между режимами;
//    2) в подписи ЕСТЬ ПОДСКАЗКА, как выйти (без неё индикатор сообщает состояние, но не
//       способ его изменить — ровно та жалоба, из-за которой пункт и появился);
//    3) цвета режимов РАЗЛИЧАЮТСЯ (чтобы режим было видно боковым зрением, не читая текст);
//    4) строка журнала говорит словами, а не сокращениями.
// ================================================================================================

using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Подпись режима «камера / интерфейс».</summary>
    public class KvModeIndicatorTests
    {
        /// <summary>
        /// ПОДПИСЬ РАЗЛИЧАЕТ РЕЖИМЫ. Если оба режима дают одинаковый текст, индикатор бесполезен:
        /// оператор смотрит на него и не узнаёт, в каком режиме находится.
        /// </summary>
        [Test]
        public void Title_DiffersBetweenModes()
        {
            string camera = KvModeIndicator.Title(KvCursorMode.CameraMode);
            string ui = KvModeIndicator.Title(KvCursorMode.UiMode);

            Assert.IsFalse(string.IsNullOrEmpty(camera), "у режима камеры нет названия");
            Assert.IsFalse(string.IsNullOrEmpty(ui), "у режима интерфейса нет названия");
            Assert.AreNotEqual(camera, ui, "названия режимов совпали — индикатор не различает их");
        }

        /// <summary>
        /// ПОДСКАЗКА ОБЪЯСНЯЕТ, ЧТО ДЕЛАЕТ МЫШЬ. «КАМЕРА» без пояснения — это слово, а не подсказка:
        /// важно, что панели в этом режиме не нажимаются.
        /// </summary>
        [Test]
        public void Hint_ExplainsWhatMouseDoes()
        {
            string camera = KvModeIndicator.Hint(KvCursorMode.CameraMode);
            string ui = KvModeIndicator.Hint(KvCursorMode.UiMode);

            StringAssert.Contains("камер", camera.ToLowerInvariant(),
                "подсказка режима камеры обязана говорить про камеру: " + camera);
            StringAssert.Contains("панел", camera.ToLowerInvariant(),
                "подсказка режима камеры обязана предупреждать, что панели не нажимаются: " + camera);
            StringAssert.Contains("панел", ui.ToLowerInvariant(),
                "подсказка режима интерфейса обязана говорить про панели: " + ui);
            Assert.AreNotEqual(camera, ui);
        }

        /// <summary>
        /// В ПОЛНОЙ ПОДПИСИ ЕСТЬ СПОСОБ ВЫЙТИ ИЗ РЕЖИМА (пункт 5.19 — именно этого не хватало).
        /// Проверяется и ключевое слово переключения, и название самой клавиши: если клавишу
        /// сменят в `KvMouseCursor`, подпись обязана следовать за ней, а не отставать.
        /// </summary>
        [Test]
        public void Line_TellsHowToSwitchBack()
        {
            foreach (KvCursorMode mode in new[] { KvCursorMode.CameraMode, KvCursorMode.UiMode })
            {
                string line = KvModeIndicator.Line(mode);
                StringAssert.Contains("TAB", line, "в подписи нет подсказки про TAB: " + line);
                StringAssert.Contains(KvMouseCursor.ModeToggleKey.ToString(), line,
                    "в подписи нет самой клавиши переключения: " + line);
            }
        }

        /// <summary>
        /// ЦВЕТА РЕЖИМОВ РАЗЛИЧАЮТСЯ ЗАМЕТНО. Разница меньше 0,15 по любому каналу глазом не
        /// читается как «другой режим» — тогда цвет только создаёт видимость индикации.
        /// </summary>
        [Test]
        public void Tint_IsVisiblyDifferent()
        {
            Color camera = KvModeIndicator.Tint(KvCursorMode.CameraMode);
            Color ui = KvModeIndicator.Tint(KvCursorMode.UiMode);

            float delta = Mathf.Max(Mathf.Abs(camera.r - ui.r),
                Mathf.Max(Mathf.Abs(camera.g - ui.g), Mathf.Abs(camera.b - ui.b)));
            Assert.Greater(delta, 0.15f,
                "цвета режимов слишком близки (разница " + delta.ToString("0.00") + ") — " +
                "режим не различить, не прочитав текст");
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА ГОВОРИТ СЛОВАМИ. По журналу разбирают «почему не нажималось», поэтому
        /// там нужны полные слова, а не «КАМЕРА» и не цвет.
        /// </summary>
        [Test]
        public void LogLine_UsesWordsNotAbbreviations()
        {
            string camera = KvModeIndicator.LogLine(KvCursorMode.CameraMode);
            string ui = KvModeIndicator.LogLine(KvCursorMode.UiMode);

            StringAssert.Contains("режим:", camera.ToLowerInvariant(),
                "строка журнала обязана начинаться с «режим:»: " + camera);
            StringAssert.Contains("курсор", camera.ToLowerInvariant(),
                "в журнале режима камеры обязано быть сказано про скрытый курсор: " + camera);
            StringAssert.Contains("курсор", ui.ToLowerInvariant(),
                "в журнале режима интерфейса обязано быть сказано про видимый курсор: " + ui);
            StringAssert.Contains(KvMouseCursor.ModeToggleKey.ToString(), camera,
                "строка журнала обязана называть клавишу выхода: " + camera);
        }
    }
}
