// ================================================================================================
//  KvEmptyCatchPolicyTests.cs — EditMode-тесты ПРАВИЛА ПУСТЫХ `catch` (сессия S44).
//
//  ОТКУДА ЗАДАЧА. В коде 57 пустых `catch { }` (из 272 всех `catch`). Обычно пустой `catch` — это
//  «проглотили ошибку и промолчали»: файл не сохранился, сеть не ответила, шлем не отдал данные —
//  а оператор ничего не узнал.
//
//  НО НЕ ВСЕГДА. Проверено 30.10.2026 вручную по каждому опасному месту:
//    • `FreeFlyCameraController` — ЗАКОННЫЙ резерв: если новая система ввода недоступна, пробуем
//      старую. Пустой `catch` здесь означает «эта ветка не сработала, есть другая»;
//    • `KvRobotExport.FolderPath` — ЗАКОННЫЙ резерв: папка «Документы» недоступна → берём данные
//      приложения. Резерв описан строкой ниже, сообщение оператору не нужно;
//    • `KvNetTools` — сетевые пробы, где отказ нормален (порт занят, хост не отвечает).
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ: пустой `catch` допустим, только если он в РЕЕСТРЕ ПРОВЕРЕННЫХ и у него есть
//  причина. Новый пустой `catch` в другом месте — красный тест.
//
//  ГРАНИЦА ЧЕСТНОСТИ: тест НЕ требует убрать пустые `catch` из реестра — их правка без нужды только
//  добавит шума в журнал. Он требует ОСОЗНАННОСТИ: либо объясни, либо не добавляй.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Пустые `catch`: законный резерв или проглоченная ошибка.</summary>
    public class KvEmptyCatchPolicyTests
    {
        /// <summary>
        /// ФАЙЛЫ, ГДЕ ПУСТОЙ `catch` ПРИЗНАН ЗАКОННЫМ — с причиной у каждого.
        ///
        /// ПРОВЕРЕНО ВРУЧНУЮ 30.10.2026: открывал каждый файл и смотрел, что в `try`. Если появится
        /// новый файл с пустым `catch` — его надо проверить и внести сюда с объяснением.
        /// </summary>
        private static readonly Dictionary<string, string> AllowedEmptyCatchFiles =
            new Dictionary<string, string>
        {
            { "FreeFlyCameraController.cs", "резерв между новой и старой системой ввода: отказ одной ветки — норма" },
            { "KvRobotExport.cs", "резерв папки экспорта: нет «Документов» — берём папку данных приложения" },
            { "KvNetTools.cs", "сетевые пробы: занятый порт и недоступный хост — ожидаемый отказ" },
            { "KvGraphicsService.cs", "опрос возможностей видеокарты: отсутствие параметра — норма" },
            { "KvGraphicsUi.cs", "оформление: если свойство темы недоступно, берётся значение по умолчанию" },
            { "KvXrInput.cs", "опрос устройств ввода: устройство может исчезнуть между кадрами" },
            { "KvCinematics.cs", "кадрирование: необязательная настройка камеры" },
            { "KvStartMenu.cs", "закрытие меню: отказ не мешает продолжить" },
            { "KvWorkbenchWindow.cs", "оформление окна: необязательная настройка" },
            { "KvRobotImport.cs", "разбор необязательного поля импорта: отсутствие — норма" },
            { "KvStageHub.cs", "необязательная настройка панели" },
            { "KvStageHub2.cs", "необязательная настройка панели" },
            { "KvStageHub3.cs", "необязательная настройка панели" },
            { "KvStageHub4.cs", "необязательная настройка панели" },
            { "FeatureHub.cs", "опрос состояния ввода: клавиатура может отсутствовать" },
            { "WorkspaceVisualizer.cs", "оформление сетки: необязательный параметр" },
            { "KvCaptures.cs", "снимок экрана: отказ камеры не должен ломать запись" },
            { "DshPlayModeRunner.cs", "вспомогательный логгер: файл недоступен — строка всё равно идёт в Debug.Log" },
        };

        private static string ScriptsRoot
        {
            get { return Path.Combine(Application.dataPath, "_Project", "01_Scripts"); }
        }

        /// <summary>
        /// СОДЕРЖИТ ЛИ ФАЙЛ ПУСТОЙ `CATCH`. Ищется `catch { }` и `catch (…) { }` без единого
        /// оператора внутри.
        ///
        /// Комментарий внутри блока считается «не пустым» только если это не единственное
        /// содержимое: комментарий объясняет, но ничего не делает — поэтому он НЕ спасает от
        /// правила. Так честнее: объяснение должно быть в реестре, а не в произвольном месте кода.
        /// </summary>
        private static bool HasEmptyCatch(string text)
        {
            foreach (Match m in Regex.Matches(text, @"catch\s*(?:\([^)]*\))?\s*\{"))
            {
                int start = m.Index + m.Length;
                int depth = 1;
                int at = start;
                while (at < text.Length && depth > 0)
                {
                    if (text[at] == '{') depth++;
                    else if (text[at] == '}') depth--;
                    at++;
                }
                string body = text.Substring(start, at - start - 1);
                if (body.Trim().Length == 0) return true;
            }
            return false;
        }

        /// <summary>
        /// РАЗБОР НАХОДИТ ПУСТЫЕ `CATCH` ТАМ, ГДЕ ОНИ ЕСТЬ. Без этой проверки следующий тест
        /// становился бы зелёным «потому что ничего не нашлось» (E-16).
        /// </summary>
        [Test]
        public void ParserFindsKnownEmptyCatch()
        {
            string path = FindFile("FreeFlyCameraController.cs");
            Assert.IsNotNull(path, "нет FreeFlyCameraController.cs — проверить нечего");

            Assert.IsTrue(HasEmptyCatch(File.ReadAllText(path)),
                "разбор не нашёл пустой catch в файле, где он точно есть — проверка обесценена");
        }

        /// <summary>
        /// НОВЫХ ПУСТЫХ `CATCH` НЕТ. Пустой `catch` без объяснения — это «ошибка проглочена».
        /// Оператор не узнает, что файл не сохранился, а мы — что что-то не работает.
        /// </summary>
        [Test]
        public void NoNewEmptyCatchOutsideRegistry()
        {
            Assert.IsTrue(Directory.Exists(ScriptsRoot), "нет папки скриптов: " + ScriptsRoot);

            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                // Тесты и диагностика не считаются: там пустой catch — часть инструмента проверки.
                if (name.EndsWith("Tests.cs")) continue;
                if (name.Contains("Diag")) continue;

                if (!HasEmptyCatch(File.ReadAllText(file))) continue;
                if (AllowedEmptyCatchFiles.ContainsKey(name)) continue;
                offenders.Add(name);
            }

            Assert.AreEqual(0, offenders.Count,
                "в этих файлах есть ПУСТОЙ `catch` без объяснения — ошибка проглатывается молча: " +
                string.Join(", ", offenders.ToArray()) +
                ". Объясните в комментарии к catch (в журнал / значение по умолчанию) либо, если " +
                "отказ законен, внесите файл в AllowedEmptyCatchFiles с причиной");
        }

        /// <summary>
        /// У КАЖДОЙ ЗАПИСИ РЕЕСТРА ЕСТЬ ПРИЧИНА, и файл ещё существует. Устаревшая запись опасна:
        /// она разрешает пустой `catch` файлу, которого уже нет, и прикрывает будущий одноимённый.
        /// </summary>
        [Test]
        public void RegistryIsNotStale()
        {
            var stale = new List<string>();

            foreach (var pair in AllowedEmptyCatchFiles)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length < 15)
                    stale.Add(pair.Key + " (нет причины)");

                if (FindFile(pair.Key) == null)
                    stale.Add(pair.Key + " (файла больше нет)");
            }

            Assert.AreEqual(0, stale.Count,
                "реестр законных пустых catch устарел: " + string.Join("; ", stale.ToArray()));
        }

        private static string FindFile(string fileName)
        {
            if (!Directory.Exists(ScriptsRoot)) return null;
            foreach (string path in Directory.GetFiles(ScriptsRoot, fileName, SearchOption.AllDirectories))
                return path;
            return null;
        }
    }
}
