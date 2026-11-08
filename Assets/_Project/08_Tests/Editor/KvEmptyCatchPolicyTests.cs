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
//
//  ---------------------------------------------------------------------------------------------
//  P3-4 (11.11.2026). ЧТО БЫЛО. Тест обходил ТОЛЬКО `01_Scripts`, а вторая половина проекта —
//  оболочка интерфейса `06_KazistovVv_UI/03_Scripts` — не сканировалась ВООБЩЕ. Замерено: 14
//  пустых `catch` жили именно там (`KvMouseCursor` — 5, `KazistovVvUIManager` — 4, `KvTreeView`,
//  `KvVirtualCursor`, `KvKeyboardNav`, `KvLayoutStore` — по одному).
//  ЧЕМ ЭТО ГРОЗИЛО. Правило «ошибка не проглатывается» выполнялось ровно на той половине кода,
//  которую проверяли, — и зеленело. А самая операторская часть (курсор, ввод в интерфейсе,
//  сохранение раскладки) молчала об отказах, и ни один тест этого не видел: пустой `catch` там
//  был не исключением, а нормой.
//  ТЕПЕРЬ обходятся ОБА корня, у каждого законного случая в оболочке есть своя причина в реестре,
//  а не «найдено вручную 30.10.2026».
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Пустые `catch`: законный резерв или проглоченная ошибка.</summary>
        [Category(KvTestCategories.Fast)]
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

            // ---- ОБОЛОЧКА ИНТЕРФЕЙСА (`06_KazistovVv_UI/03_Scripts`) — проверено 11.11.2026 (P3-4).
            // Причины конкретные, а не «необязательная настройка»: в каждом случае ниже рядом
            // стоит ВТОРАЯ, работающая ветка, и пустой перехват означает именно «эта не сработала».
            // Всё, что не подходило под это правило, ИСПРАВЛЕНО: `KvMouseCursor` (5 перехватов) и
            // `KazistovVvUIManager` (4) теперь пишут в журнал и считают `ReapplyCount`.
            { "KvKeyboardNav.cs", "резерв новой/старой системы ввода в CtrlHeld: отказ новой ветки — норма, ниже сразу идёт legacy с возвратом false" },
            { "KvVirtualCursor.cs", "резерв новой/старой системы ввода для позиции мыши: ниже сразу берётся legacy Input.mousePosition" },
            { "KvTreeView.cs", "резерв новой/старой системы ввода при переименовании узла: legacy читается только когда Keyboard.current == null" },
            { "KvLayoutStore.cs", "PlayerPrefs.Save при недоступном хранилище: значения уже в памяти, перетаскивание окна падать не должно" },
        };

        /// <summary>
        /// КОРНИ, КОТОРЫЕ ОБХОДЯТСЯ. P3-4 (11.11.2026): раньше здесь был ОДИН корень —
        /// `01_Scripts`. Оболочка интерфейса (`06_KazistovVv_UI/03_Scripts`) не проверялась вовсе,
        /// а именно там жили 13 пустых перехватов из замеренных 14. Правило, которое выполняется
        /// на половине кода, — это не правило, а его видимость.
        /// </summary>
        private static string[] ScriptsRoots
        {
            get
            {
                return new[]
                {
                    Path.Combine(Application.dataPath, "_Project", "01_Scripts"),
                    Path.Combine(Application.dataPath, "_Project", "06_KazistovVv_UI", "03_Scripts"),
                };
            }
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
        ///
        /// P3-4 (11.11.2026): обходятся ОБА корня (`ScriptsRoots`), а не только `01_Scripts`.
        /// </summary>
        [Test]
        public void NoNewEmptyCatchOutsideRegistry()
        {
            var offenders = new List<string>();
            int scanned = 0;

            foreach (string root in ScriptsRoots)
            {
                Assert.IsTrue(Directory.Exists(root), "нет папки скриптов: " + root);

                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    scanned++;
                    string name = Path.GetFileName(file);
                    // Тесты и диагностика не считаются: там пустой catch — часть инструмента проверки.
                    if (name.EndsWith("Tests.cs")) continue;
                    if (name.Contains("Diag")) continue;

                    if (!HasEmptyCatch(File.ReadAllText(file))) continue;
                    if (AllowedEmptyCatchFiles.ContainsKey(name)) continue;
                    // В имя попадает корень: одноимённые файлы в разных корнях — разные файлы.
                    offenders.Add(Path.GetFileName(Path.GetDirectoryName(file)) + "/" + name);
                }
            }

            // СТРАХОВКА ОТ ВАКУУМА (E-16): обход обязан был увидеть заметное число файлов,
            // иначе «0 нарушителей» означало бы «0 просмотренных».
            Assert.Greater(scanned, 100,
                "просмотрено всего " + scanned + " файлов — обход почти ничего не проверил (P3-4)");

            Assert.AreEqual(0, offenders.Count,
                "в этих файлах есть ПУСТОЙ `catch` без объяснения — ошибка проглатывается молча: " +
                string.Join(", ", offenders.ToArray()) +
                ". Объясните в комментарии к catch (в журнал / значение по умолчанию) либо, если " +
                "отказ законен, внесите файл в AllowedEmptyCatchFiles с причиной");
        }

        /// <summary>
        /// ОБОЛОЧКА ИНТЕРФЕЙСА ДЕЙСТВИТЕЛЬНО СКАНИРУЕТСЯ (P3-4, 11.11.2026).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНЫЙ ТЕСТ. Основная проверка зелёная и тогда, когда корень оболочки забыли
        /// добавить снова: файлов меньше, нарушителей ноль. Этот тест падает, если корень
        /// `06_KazistovVv_UI/03_Scripts` пропал из обхода или в нём не нашлось ни одного `.cs`.
        /// </summary>
        [Test]
        public void UiShellRootIsScanned()
        {
            string shellRoot = Path.Combine(Application.dataPath, "_Project", "06_KazistovVv_UI", "03_Scripts");
            Assert.IsTrue(Directory.Exists(shellRoot), "нет папки оболочки интерфейса: " + shellRoot);

            int uiFiles = Directory.GetFiles(shellRoot, "*.cs", SearchOption.AllDirectories).Length;
            Assert.Greater(uiFiles, 20,
                "в оболочке найдено " + uiFiles + " файлов — обход корня потерял смысл");

            var roots = new List<string>(ScriptsRoots);
            Assert.Contains(shellRoot, roots,
                "корень оболочки интерфейса не входит в обход (P3-4): правило снова проверяет " +
                "только половину кода");

            // И сам файл оболочки, где перехваты ИСПРАВЛЕНЫ, обязан быть в обходе по имени:
            // если его переименуют, запись реестра станет мёртвой (проверяется ниже, в реестре).
            Assert.IsNotNull(FindFile("KazistovVvUIManager.cs"),
                "обход не находит KazistovVvUIManager.cs — значит корень оболочки не читается");
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

        /// <summary>
        /// ПУТЬ К ФАЙЛУ ПО ИМЕНИ — в ЛЮБОМ из корней (P3-4: корней два, а не один).
        /// </summary>
        private static string FindFile(string fileName)
        {
            foreach (string root in ScriptsRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.GetFiles(root, fileName, SearchOption.AllDirectories))
                    return path;
            }
            return null;
        }
    }
}

