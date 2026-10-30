// ================================================================================================
//  KvLaserPointerFlagTests.cs — EditMode-тесты РАЗДЕЛЕНИЯ «РИСОВАТЬ ЛУЧ» и «РАБОТАТЬ ПРИЦЕЛОМ»
//  (сессия S44, пункт 5.18 плана).
//
//  ОТКУДА ЗАДАЧА. Флаг `enableLaserPointer` назывался «мастер-выключателем УКАЗОК», но ранним
//  `return` в начале `UpdateLaserPointers` глушил ВЕСЬ поток: расчёт прицела, вердикт маркера,
//  обновление планировщика и обработку ЛКМ. Выключив «лучи», оператор терял всю работу — и по
//  названию флага догадаться об этом было нельзя.
//
//  ЧТО СДЕЛАНО. Возврата из метода больше нет: флаг гасит только линии.
//
//  КАК ЭТО ПРОВЕРИТЬ БЕЗ СЦЕНЫ И БЕЗ КАМЕРЫ. Поведение `UpdateLaserPointers` целиком не
//  воспроизвести (нужны камера, планировщик и ввод), но ПРИЧИНУ дефекта видно в исходнике:
//  ранний `return` стоял до всей остальной работы. Поэтому тест читает исходник и проверяет
//  ИНВАРИАНТ: внутри ветки «лучи выключены» НЕ ДОЛЖНО быть выхода из метода.
//
//  ЭТО НЕ «ТЕСТ НА ТЕКСТ РАДИ ТЕСТА»: именно эта строчка была причиной того, что один флаг
//  выключал пять подсистем. Сторож на неё дешевле, чем ещё один разбор «почему ничего не
//  работает» в шлеме.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Флаг лучей гасит только рисование, а не работу прицела.</summary>
    public class KvLaserPointerFlagTests
    {
        /// <summary>Файл, в котором живёт `UpdateLaserPointers`.</summary>
        private static string ControllerPath
        {
            get
            {
                string root = Path.Combine(Application.dataPath, "_Project");
                if (!Directory.Exists(root)) return null;
                foreach (string file in Directory.GetFiles(root, "FreeFlyCameraController.cs", SearchOption.AllDirectories))
                    return file;
                return null;
            }
        }

        /// <summary>
        /// ВЕТКА «ЛУЧИ ВЫКЛЮЧЕНЫ» НЕ ВЫХОДИТ ИЗ МЕТОДА.
        ///
        /// Проверяется тело `if (!enableLaserPointer) { … }`: в нём разрешено только гасить
        /// `leftLaser`/`rightLaser`. Любой `return` внутри возвращает дефект 5.18 — тогда
        /// выключение лучей снова выключит прицел, маркер, планировщик и ЛКМ.
        /// </summary>
        [Test]
        public void DisabledLasers_DoNotReturnFromAimingFlow()
        {
            string path = ControllerPath;
            Assert.IsNotNull(path, "не найден FreeFlyCameraController.cs — проверить нечего");
            string text = File.ReadAllText(path);

            // Найти `if (!enableLaserPointer)` и его блок в фигурных скобках.
            Match head = Regex.Match(text, @"if\s*\(\s*!\s*enableLaserPointer\s*\)\s*\{");
            Assert.IsTrue(head.Success,
                "в контроллере нет ветки `if (!enableLaserPointer) { … }` — код переписан, " +
                "проверьте вручную, что флаг по-прежнему гасит ТОЛЬКО линии");

            int start = head.Index + head.Length;
            int depth = 1;
            int at = start;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }
            string body = text.Substring(start, at - start - 1);

            Assert.IsFalse(Regex.IsMatch(body, @"\breturn\b"),
                "внутри `if (!enableLaserPointer)` есть `return` — флаг снова глушит весь поток, " +
                "а не только рисование лучей (дефект 5.18). Тело должно лишь гасить линии: " +
                body.Trim());
        }

        /// <summary>
        /// ФЛАГ ВСЁ-ТАКИ ГАСИТ ЛИНИИ. Обратная ошибка — «флаг не делает ничего» — тоже дефект:
        /// оператор выключил лучи, а они продолжают мешать.
        /// </summary>
        [Test]
        public void DisabledLasers_StillHideTheLines()
        {
            string path = ControllerPath;
            Assert.IsNotNull(path);
            string text = File.ReadAllText(path);

            Match head = Regex.Match(text, @"if\s*\(\s*!\s*enableLaserPointer\s*\)\s*\{");
            Assert.IsTrue(head.Success);
            int start = head.Index + head.Length;
            int depth = 1, at = start;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }
            string body = text.Substring(start, at - start - 1);

            Assert.IsTrue(body.Contains("leftLaser") && body.Contains("rightLaser"),
                "ветка «лучи выключены» обязана гасить ОБА луча (левый и правый), иначе половина " +
                "остаётся на экране: " + body.Trim());
            Assert.IsTrue(body.Contains("enabled = false"),
                "ветка «лучи выключены» обязана выставлять `enabled = false`: " + body.Trim());
        }

        /// <summary>
        /// ПОСЛЕ ВЕТКИ РАБОТА ПРОДОЛЖАЕТСЯ. Проверяется, что сразу за блоком идёт расчёт прицела
        /// (`ComputeAimPoint`), а не выход: именно порядок «сначала вердикт, потом рисование»
        /// и составляет суть правки.
        /// </summary>
        [Test]
        public void AimingWorkContinuesAfterTheFlagBranch()
        {
            string path = ControllerPath;
            Assert.IsNotNull(path);
            string text = File.ReadAllText(path);

            Match head = Regex.Match(text, @"if\s*\(\s*!\s*enableLaserPointer\s*\)\s*\{");
            Assert.IsTrue(head.Success);
            int start = head.Index + head.Length;
            int depth = 1, at = start;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }

            // Смотрим 600 символов после закрытия ветки: там должен идти расчёт прицела.
            int tail = System.Math.Min(600, text.Length - at);
            string after = text.Substring(at, tail);

            Assert.IsTrue(after.Contains("ComputeAimPoint"),
                "сразу за веткой лучей обязан идти расчёт прицела (`ComputeAimPoint`): иначе " +
                "выключение лучей снова отключает прицеливание. После ветки: " +
                after.Substring(0, System.Math.Min(200, after.Length)).Trim());
        }

        /// <summary>
        /// ФЛАГ ПО УМОЛЧАНИЮ ВКЛЮЧЁН. Если он окажется выключенным по умолчанию, оператор при
        /// первом запуске не увидит ни лучей, ни — до правки 5.18 — вообще ничего.
        /// </summary>
        [Test]
        public void FlagIsEnabledByDefault()
        {
            string path = ControllerPath;
            Assert.IsNotNull(path);
            string text = File.ReadAllText(path);

            Match m = Regex.Match(text, @"public\s+bool\s+enableLaserPointer\s*=\s*(\w+)");
            Assert.IsTrue(m.Success, "поле `enableLaserPointer` не найдено или переписано");
            Assert.AreEqual("true", m.Groups[1].Value,
                "флаг лучей обязан быть включён по умолчанию: иначе первый запуск даёт невидимый указатель");
        }
    }
}
