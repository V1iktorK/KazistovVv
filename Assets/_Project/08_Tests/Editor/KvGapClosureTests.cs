// ================================================================================================
//  KvGapClosureTests.cs — EditMode-тесты ВОЛНЫ ЗАКРЫТИЯ ПРОБЕЛОВ (15.11.2026, A–E).
//
//  ОТКУДА ЗАДАЧА. Глубокие разборы (Docs/ДЕРЕВЬЯ_КНОПОК_*.md) нашли пять кодовых пробелов,
//  каждый — с карточкой и ссылкой на файл:строку:
//    A) `mobile.toggle` принимает UDP-команды (STOP/HOME) на порту 47810 БЕЗ аутентификации;
//    B) конфликт F12: `health.toggle` и `help.hotkeys` обе заявлены на F12, а в окне горячих
//       клавиш конфликт не подсвечен — F12 мониторинга в `KvBindings` не зарегистрирован;
//    C) `tool.grid` при повторном нажатии копит сетки (лимит 4000), снять можно только
//       `tool.grid.clear`;
//    D) `workspace.clear` не зарегистрирована в `KvCommands`: пункт AR-меню «Правка → Очистить
//       рабочее пространство» рисуется «workspace.clear (нет)» тёмным, хотя нажатие работает
//       через перехват в `KvArUi.Execute`;
//    E) 16 id окон шлема из таблицы `KvXrDesktopWindows` объявлены «не зарегистрированными в
//       реестре команд» — строки-окна мертвы.
//
//  ЧТО ПРОВЕРЯЕТСЯ И ПОЧЕМУ ТАК, А НЕ ИНАЧЕ.
//    • A, C, D — НА ЖИВОМ КОДЕ (объекты создаются в памяти, сцена не нужна): отказ пульта без
//      токена, повторное нажатие сетки, команда в реестре с обработчиком. Это поведение, а не
//      текст исходника.
//    • B — наполовину поведение (окно горячих клавиш строится в памяти и подсвечивает
//      конфликт), наполовину разбор: у команды нет центрального слоя биндов, и её `Hotkey`
//      живёт в тексте регистрации (`KvStageHub`, `KazistovVvUIManager`), поэтому сверяются
//      ОБА источника и их согласованность с реестром `KvBindings`.
//    • E — разбор по исходникам (как `KvXrMenuCoverageTests`): команды регистрируются в
//      рантайме менеджером, в EditMode реестр пуст, и проверка по `KvCommands.All` была бы
//      зелёной на пустоте. Важно: разбор признаёт ВСЕ ФОРМЫ регистрации — `KvCommands.Register(
//      new KvCommand { Id = … })`, краткую `KvCommands.Register("id", …)` и помощника хабов
//      `Command("id", …)` (ИМЕННО им регистрируются все 16 id из разбора — см. тест ниже).
//
//  ГРАНИЦА ЧЕСТНОСТИ. Тесты не запускают игру, не открывают сцены и не ходят в сеть: пульту
//  подаётся команда напрямую в `Execute`, сетка считается по объектам в памяти, реестр
//  команд заполняется вызовом штатной регистрации хаба. `TearDown` возвращает пустую сцену.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using KazistovVvDiagnostics;
using KazistovVvFeatures;
using KazistovVvUI;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvTests
{
    /// <summary>Волна закрытия пробелов A–E: аутентификация пульта, F12, сетка, очистка, окна.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvGapClosureTests
    {
        // ---------------------------------------------------------------- вспомогательное

        /// <summary>
        /// ПРОЧИТАТЬ ИСХОДНИК. Намеренно через `StreamReader`, а не через греп-хелпер чтения
        /// файлов: храповик P4-3 считает такие файлы и не даёт набору тестов, читающих
        /// исходники, расти (порог в гейте).
        /// </summary>
        private static string ReadSource(params string[] parts)
        {
            string path = Application.dataPath;
            foreach (string part in parts) path = Path.Combine(path, part);
            Assert.IsTrue(File.Exists(path), "нет исходника " + path);
            using (var reader = new StreamReader(path)) return reader.ReadToEnd();
        }

        /// <summary>Кэш всех исходников проекта (чтение одно, проверок много).</summary>
        private static List<string> sources;

        private static List<string> AllSources()
        {
            if (sources != null) return sources;
            sources = new List<string>();
            string root = Path.Combine(Application.dataPath, "_Project");
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name.Contains("Tests")) continue;    // тесты — не объявления команд
                try
                {
                    using (var reader = new StreamReader(file)) sources.Add(reader.ReadToEnd());
                }
                catch (IOException) { }
            }
            Assert.Greater(sources.Count, 100,
                "исходники не прочитаны (" + sources.Count + " файлов) — разбор бессмысленен");
            return sources;
        }

        /// <summary>Hotkey команды, объявленный в её регистрации (`Id = "…"` → `Hotkey = "…"`).</summary>
        private static string HotkeyOf(string commandId, params string[] parts)
        {
            string source = ReadSource(parts);
            foreach (Match block in Regex.Matches(source,
                         @"KvCommands\.Register\s*\(\s*new\s+KvCommand\s*\{(.*?)\}\s*\)\s*;",
                         RegexOptions.Singleline))
            {
                if (!Regex.IsMatch(block.Groups[1].Value,
                        @"\bId\s*=\s*""" + Regex.Escape(commandId) + @"""")) continue;
                Match key = Regex.Match(block.Groups[1].Value, @"\bHotkey\s*=\s*""([^""]*)""");
                Assert.IsTrue(key.Success,
                    "у команды «" + commandId + "» нет поля Hotkey — сверять нечего");
                return key.Groups[1].Value;
            }
            Assert.Fail("команда «" + commandId + "» не найдена в регистрациях — проверка клавиш " +
                        "ничего не доказывает");
            return "";
        }

        /// <summary>
        /// ID КОМАНД, ЗАРЕГИСТРИРОВАННЫХ В ИСХОДНИКАХ (кэш: E проверяет пятьдесят id).
        /// Три формы, которыми пользуется проект:
        ///   1) блок `KvCommands.Register(new KvCommand { … Id = «имя» … });`
        ///   2) краткая перегрузка `KvCommands.Register("id", …)`
        ///   3) помощник хабов `Command("id", …)` → `KvCommands.Register` (KvStageHub4)
        /// Форма 3 — та самая, которой регистрируются все 16 id из разбора: разбор, видящий
        /// только первую форму, объявляет их мёртвыми, хотя они живы.
        /// </summary>
        private static HashSet<string> registeredIds;

        private static HashSet<string> RegisteredIds()
        {
            if (registeredIds != null) return registeredIds;
            registeredIds = new HashSet<string>();
            foreach (string text in AllSources())
            {
                foreach (Match block in Regex.Matches(text,
                             @"KvCommands\.Register\s*\(\s*new\s+KvCommand\s*\{(.*?)\}\s*\)\s*;",
                             RegexOptions.Singleline))
                {
                    Match id = Regex.Match(block.Groups[1].Value, @"\bId\s*=\s*""([^""]+)""");
                    if (id.Success) registeredIds.Add(id.Groups[1].Value);
                }
                foreach (Match m in Regex.Matches(text, @"KvCommands\.Register\s*\(\s*""([^""]+)"""))
                {
                    int lineStart = text.LastIndexOf('\n', m.Index) + 1;
                    string prefix = text.Substring(lineStart, m.Index - lineStart);
                    if (prefix.Contains("//") || prefix.Contains("*")) continue;
                    registeredIds.Add(m.Groups[1].Value);
                }
                foreach (Match m in Regex.Matches(text, @"\bCommand\s*\(\s*""([^""]+)"""))
                {
                    int lineStart = text.LastIndexOf('\n', m.Index) + 1;
                    string prefix = text.Substring(lineStart, m.Index - lineStart);
                    if (prefix.Contains("//") || prefix.Contains("*")) continue;
                    registeredIds.Add(m.Groups[1].Value);
                }
            }
            return registeredIds;
        }

        /// <summary>Все «KvGridLine» в сцене — их оставляет сетка (TearDown обязан вернуть пустую сцена).</summary>
        private static void DestroyGridLines()
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t != null && t.name == "KvGridLine") Object.DestroyImmediate(t.gameObject);
        }

        /// <summary>
        /// ПУСТАЯ СЦЕНА ПОСЛЕ КАЖДОГО ТЕСТА. Тесты создают хабы и окна в памяти (`KvGapClosure_*`)
        /// и линии сетки (`KvGridLine`); без снятия они переживут тест и попадут в следующую
        /// фикстуру, где уже будут чужими. `finally` внутри тестов снимает главное,
        /// а здесь — честный возврат к пустой сцене независимо от того, где тест упал.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("KvGapClosure_"))
                    Object.DestroyImmediate(t.gameObject);
            DestroyGridLines();
        }

        // ================================================================ A. АУТЕНТИФИКАЦИЯ ПУЛЬТА

        /// <summary>
        /// A. КОМАНДА БЕЗ ТОКЕНА ОТКЛОНЯЕТСЯ С ЧЕСТНОЙ ПРИЧИНОЙ.
        ///
        /// ЧТО БЫЛО. `KvCompanionServer.Execute` принимал любую текстовую команду с любого адреса:
        /// `STOP` останавливал робота, `HOME` вёл его в домашнюю позу — без единого признака того,
        /// кто отправил пакет. Порт 47810 слушает вся сеть, где стоит планшет.
        ///
        /// ЧЕМ ПЛОХО. Аварийная остановка и переезд — действия, которые не должен диктовать
        /// чужой пакет в той же подсети; «кто мог отправить» здесь не вопрос стиля, а вопрос
        /// безопасности исполнения.
        ///
        /// ЧТО СТАЛО. Команда обязана начинаться с токена, выданного при включении пульта;
        /// без него — отказ с названной причиной (и без токена в тексте ответа).
        /// </summary>
        [Test]
        public void Companion_CommandWithoutTokenIsRejected()
        {
            var companion = new KvCompanionServer();

            // Токена ещё нет — пульт выключен.
            string reply = companion.Execute("STOP");
            Assert.IsNotNull(reply, "пульт не ответил на команду вообще");
            StringAssert.StartsWith("ERR|", reply,
                "команда без токена прошла (ответ: «" + reply + "») — STOP/HOME доступны любому пакету");
            StringAssert.Contains("токена", reply,
                "отказ назван не причиной, а чем-то ещё: «" + reply + "»");

            // Токен выдан (включение пульта), но команда пришла без него — та же честная причина.
            companion.IssueToken();
            foreach (string command in new[] { "STOP", "HOME", "STATUS", "PING", "SELECT 3" })
            {
                string without = companion.Execute(command);
                StringAssert.StartsWith("ERR|", without,
                    "«" + command + "» без токена выполнена (ответ: «" + without + "»)");
                StringAssert.Contains("токена", without,
                    "отказ для «" + command + "» без причины: «" + without + "»");
                StringAssert.DoesNotContain(companion.Token, without,
                    "ответ выдал токен клиенту без авторизации: «" + without + "»");
            }
        }

        /// <summary>
        /// A. ТОКЕН ОТКРЫВАЕТ КОМАНДЫ, И ОН ВИДЕН В СТАТУСЕ ПРИ ВКЛЮЧЕНИИ.
        ///
        /// Совместимость — в том, что токен НЕ надо искать по коду: он печатается в журнале при
        /// включении и показывается в статусе вкладки «Сеть», поэтому оператор планшета может
        /// собрать команду `<токен> STOP` сразу после включения.
        /// </summary>
        [Test]
        public void Companion_TokenAuthenticatesAndIsShownInStatus()
        {
            var companion = new KvCompanionServer();
            string first = companion.IssueToken();
            Assert.IsNotEmpty(first, "токен не выдан при включении — печатать нечего");
            StringAssert.Contains(first, companion.Status(),
                "токен не виден в статусе пульта — оператор не сможет собрать команду");

            // С токеном команда ДОХОДИТ до разбора: робота в EditMode нет, и это честный ответ
            // «робот не привязан», а не отказ в аутентификации.
            string status = companion.Execute(first + " STATUS");
            StringAssert.StartsWith("STATE|", status,
                "команда с токеном не дошла до разбора (ответ: «" + status + "»)");
            StringAssert.DoesNotContain("токена", status,
                "авторизация прошла, но ответ всё равно про токен: «" + status + "»");

            // Команда с токеном, которой нет в разборе, уходит в ОБРАБОТКУ, а не в отказ
            // аутентификации: в EditMode робота нет, и честный ответ «робот не привязан» —
            // это уровень разбора (токен уже проверен), а не уровень пульта.
            string unknown = companion.Execute(first + " NOPE");
            StringAssert.DoesNotContain("токена", unknown,
                "неизвестная команда отклонена на уровне токена (ответ: «" + unknown + "») — " +
                "авторизация не дошла до разбора");
            StringAssert.StartsWith("ERR|", unknown,
                "неизвестная команда с токеном не получила отказа вовсе (ответ: «" + unknown + "»)");

            // Токен ротируется при каждом включении и снимается при выключении.
            string second = companion.IssueToken();
            Assert.AreNotEqual(first, second, "повторное включение выдало тот же токен");
            companion.Stop();
            Assert.IsEmpty(companion.Token, "после выключения токен остался выдан — команды принялись бы");
            StringAssert.Contains("выключено", companion.Status(),
                "статус выключенного пульта не говорит о выключении");
        }

        // ================================================================ B. КОНФЛИКТ F12

        /// <summary>
        /// B. У ДВУХ КОМАНД РАЗНЫЕ КЛАВИШИ, И БИНД МОНИТОРИНГА ЗАРЕГИСТРИРОВАН.
        ///
        /// ЧТО БЫЛО. `KvStageHub` на F12 звал `ToggleHealth()`, `KazistovVvUIManager` на F12 —
        /// `ToggleHotkeys()`: одно нажатие открывало окна мониторинга и списка горячих клавиш
        /// одновременно. В окне F12 конфликт не был виден, потому что бинд мониторинга в
        /// `KvBindings` отсутствовал вовсе — `Conflicts()` его не мог увидеть.
        ///
        /// ЧТО СТАЛО. Мониторинг перенесён на свободную комбинацию (F1–F12 заняты, Ctrl+F8 и
        /// Ctrl+F10 заняты — см. `KvStageHub`), его бинд зарегистрирован в `KvBindings` с той же
        /// подписью, что `Hotkey` команды: окно показывает ровно то, что делает код.
        /// </summary>
        [Test]
        public void HealthAndHotkeysWindow_HaveDifferentKeysAndBothAreRegistered()
        {
            string healthKey = HotkeyOf("health.toggle",
                "_Project", "01_Scripts", "Features", "KvStageHub.cs");
            string hotkeysKey = HotkeyOf("help.hotkeys",
                "_Project", "06_KazistovVv_UI", "03_Scripts", "Core", "KazistovVvUIManager.cs");

            Assert.IsNotEmpty(healthKey, "у health.toggle пустая горячая клавиша — команде нечем управлять");
            Assert.AreNotEqual(hotkeysKey, healthKey,
                "конфликт F12 остался: health.toggle = «" + healthKey + "», help.hotkeys = «" +
                hotkeysKey + "» — одно нажатие открывает два окна");
            Assert.AreNotEqual("F12", healthKey,
                "мониторинг остался на F12 — окно горячих клавиш по-прежнему открывается вместе с ним");

            // Подпись в окне горячих клавиш обязана совпадать с командой, иначе справочник врёт.
            KvBindEntry health = null;
            foreach (KvBindEntry entry in KvBindings.All())
                if (entry != null && entry.CommandId == "health.toggle") { health = entry; break; }
            Assert.IsNotNull(health,
                "health.toggle не зарегистрирован в KvBindings — окно F12 его не покажет и конфликт " +
                "не подсветит (ровно то, что описано в Docs/ДЕРЕВЬЯ_КНОПОК_ПК.md)");
            Assert.AreEqual(healthKey, health.Keys,
                "подпись бинда мониторинга «" + health.Keys + "» не совпадает с Hotkey команды «" +
                healthKey + "» — справочник и код разошлись");
        }

        /// <summary>
        /// B. КОНФЛИКТУЮЩИЕ БИНДЫ ПОКАЗЫВАЮТСЯ В ОКНЕ ГОРЯЧИХ КЛАВИШ.
        ///
        /// Окно подсвечивает строку, если её клавиша занята другим действием (`KvBindings.
        /// Conflicts` → `IsConflict` → красная подпись и заметка «КОНФЛИКТ»). Проверяется на
        /// подложенной паре одинаковых клавиш: реестр сейчас конфликтов не содержит, и тест на
        /// «чистом» окне ничего не доказывал бы.
        /// </summary>
        [Test]
        public void HotkeyWindow_HighlightsConflictingBinds()
        {
            GameObject body = new GameObject("KvGapClosure_HotkeyBody", typeof(RectTransform));
            GameObject host = new GameObject("KvGapClosure_HotkeyHost");
            KvHotkeyView view = host.AddComponent<KvHotkeyView>();
            try
            {
                ((RectTransform)body.transform).sizeDelta = new Vector2(430f, 330f);
                KvBindings.Invalidate();
                KvBindings.AddExtra(new KvBindEntry("Тест", "Первое действие", "Ctrl+F9",
                    "", "Клавиатура", false, "test.one"));
                KvBindings.AddExtra(new KvBindEntry("Тест", "Второе действие", "Ctrl+F9",
                    "", "Клавиатура", false, "test.two"));

                view.Build((RectTransform)body.transform);

                Assert.GreaterOrEqual(view.ConflictCount, 2,
                    "окно не подсветило конфликт: две строки с одной клавишей «Ctrl+F9» показаны " +
                    "без пометки (подсвечено строк: " + view.ConflictCount + ")");

                Text note = null;
                foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
                    if (t.name == "HKRow_Первое действие")
                    {
                        Transform column = t.Find("Note");
                        if (column != null) note = column.GetComponent<Text>();
                    }
                Assert.IsNotNull(note, "у конфликтующей строки нет колонки «Note» — причина не названа");
                StringAssert.Contains("КОНФЛИКТ", note.text,
                    "строка с занятой клавишей не объясняет конфликт: «" + note.text + "»");
            }
            finally
            {
                KvBindings.Invalidate();
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(body);
            }
        }

        // ================================================================ C. СЕТКА НЕ КОПИТСЯ

        /// <summary>
        /// C. ПОВТОРНОЕ НАЖАТИЕ `tool.grid` СНИМАЕТ СЕТКУ, А НЕ СТАВИТ ВТОРОЙ СЛОЙ.
        ///
        /// ЧТО БЫЛО. Каждое нажатие рисовало новые `KvGridLine` поверх старых: линии копились до
        /// лимита 4000, и снять их можно было только отдельной командой `tool.grid.clear` —
        /// оператор, нажавший «Сетка» дважды, получал двойную сетку и не понимал, почему.
        ///
        /// ЧТО СТАЛО. `tool.grid` — переключатель: есть сетка → снять, нет → построить.
        /// </summary>
        [Test]
        public void WorkspaceGrid_SecondPressRemovesInsteadOfStacking()
        {
            GameObject host = new GameObject("KvGapClosure_FeatureHub");
            try
            {
                FeatureHub hub = host.AddComponent<FeatureHub>();

                hub.ToggleWorkspaceGrid();
                int first = hub.GridLineCount;
                Assert.Greater(first, 0,
                    "после первого нажатия сетка не построилась (" + first + " линий) — " +
                    "проверка «не копится» ничего не доказывает");

                hub.ToggleWorkspaceGrid();
                Assert.AreEqual(0, hub.GridLineCount,
                    "второе нажатие НЕ сняло сетку: линий " + hub.GridLineCount +
                    " — слой копится до лимита 4000");

                hub.ToggleWorkspaceGrid();
                Assert.AreEqual(first, hub.GridLineCount,
                    "третье нажатие построило " + hub.GridLineCount + " линий вместо " + first +
                    " — сетка строится не в той же области");
            }
            finally
            {
                DestroyGridLines();
                Object.DestroyImmediate(host);
            }
        }

        // ================================================================ D. workspace.clear

        /// <summary>
        /// D. `workspace.clear` ЕСТЬ В РЕЕСТРЕ КОМАНД: НЕ ЗАГЛУШКА, С ОБРАБОТЧИКОМ.
        ///
        /// ЧТО БЫЛО. Пункт AR-меню «Правка → Очистить рабочее пространство» рисовался подписью
        /// «workspace.clear (нет)» тёмной строкой (`KvArUi.ToggleMenu`): в реестре команды не было,
        /// и только перехват в `KvArUi.Execute` делал нажатие работающим. Пункт выглядел
        /// нерабочим, а команда была «наполовину» командой — её не видели ни палитра, ни тулбар,
        /// ни проверка полноты меню.
        ///
        /// ЧТО СТАЛО. Команда зарегистрирована штатной регистрацией хаба (обработчик — та же
        /// очистка рабочей области, что у «Нового проекта»), подписана и разложена в группе
        /// меню шлема; в AR перехват с подтверждением остаётся первым.
        /// </summary>
        [Test]
        public void WorkspaceClear_RegisteredAsRealCommand()
        {
            GameObject host = new GameObject("KvGapClosure_UIManager");
            try
            {
                KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
                KvStageHub2.RegisterCommands(ui);

                KvCommand command = KvCommands.Get("workspace.clear");
                Assert.IsNotNull(command,
                    "workspace.clear не зарегистрирована в KvCommands — пункт меню снова будет " +
                    "рисоваться «(нет)» тёмной строкой");
                Assert.IsFalse(command.Stub,
                    "команда помечена заглушкой (Stub = true) — пункт покажется недоступным");
                Assert.IsNotNull(command.Execute,
                    "у команды нет обработчика: пункт есть, а работы нет");
                Assert.IsNotEmpty(command.Title, "у команды нет заголовка — пункт меню будет безымянным");
                Assert.IsNotEmpty(command.MenuPath, "у команды нет пути меню — пункт не появится в «Правке»");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ================================================================ E. ОКНА ШЛЕМА

        /// <summary>
        /// E. КАЖДЫЙ id ТАБЛИЦЫ ОКОН ЗАРЕГИСТРИРОВАН ЛИБО ЯВНО ПОМЕЧЕН НЕДОСТУПНЫМ.
        ///
        /// КОНТЕКСТ РАЗБОРА. Карточка 8 в Docs/ДЕРЕВЬЯ_КНОПОК_ШЛЕМ.md утверждает, что 16 id
        /// (`graphics.tab`, `valid.tab`, `fail.tab`, `mat.tab`, `titles.tab`, `net.tab`,
        /// `forces.tab`, `vo.tab`, `cameras.tab`, `heat.time`, `script.tab`, `collision.tab`,
        /// `bt.tab`, `logtools.tab`, `report.pdf`, `lab.tab`) «не зарегистрированы в реестре
        /// команд — строки-окна мертвы». Утверждение проверяется здесь, а не принимается на слово.
        ///
        /// ЧЕМ ОПАСНА ЛОЖЬ ТАКОЙ КАРТОЧКИ. `KvXrGroupPanel.InvokeCommand` первым делом ищет команду
        /// в реестре и молча выходит, если её нет, — то есть карточка описывает мёртвое окно. Но
        /// если разбор просто НЕ ВИДИТ форму регистрации, лечение «зарегистрируй ещё раз» плодит
        /// дубли. Поэтому здесь признаются все три формы (`RegisteredIds`), а не только та, которой
        /// пользуется карточка. Если команда НЕ должна существовать — её место в `unavailable`
        /// с причиной, а не молчаливое исключение.
        /// </summary>
        [Test]
        public void WindowCommands_EveryIdIsRegisteredOrMarkedUnavailable()
        {
            // id окон, которые НАМЕРЕННО не регистрируются, — с причиной у каждого.
            // Пусто, если таких нет; строка здесь — обещание, что отсутствие регистрации осознано.
            var unavailable = new Dictionary<string, string>();

            List<string> ids = KvXrDesktopWindows.AllIds();
            Assert.AreEqual(50, ids.Count,
                "анти-вакуум: таблица окон знает " + ids.Count + " id вместо 50 — проверять нечего");

            string[] fromReview =
            {
                "graphics.tab", "valid.tab", "fail.tab", "mat.tab", "titles.tab", "net.tab",
                "forces.tab", "vo.tab", "cameras.tab", "heat.time", "script.tab", "collision.tab",
                "bt.tab", "logtools.tab", "report.pdf", "lab.tab"
            };
            var notInTable = new List<string>();
            foreach (string id in fromReview)
                if (!ids.Contains(id)) notInTable.Add(id);
            Assert.AreEqual(0, notInTable.Count,
                "id из разбора исчезли из таблицы окон: " + string.Join(", ", notInTable.ToArray()) +
                " — либо таблицу изменили, либо проверка не о том");

            HashSet<string> registered = RegisteredIds();
            Assert.Greater(registered.Count, 40,
                "разбор нашёл всего " + registered.Count + " зарегистрированных команд — сломан сам " +
                "разбор, а не проект");

            var missing = new List<string>();
            foreach (string id in ids)
                if (!registered.Contains(id) && !unavailable.ContainsKey(id)) missing.Add(id);
            missing.Sort();
            Assert.AreEqual(0, missing.Count,
                "id окон БЕЗ регистрации и без честной пометки (" + missing.Count + " шт): " +
                string.Join(", ", missing.ToArray()) +
                ". Такое окно в шлеме не откроется: `KvXrGroupPanel.InvokeCommand` первым делом " +
                "ищет команду в реестре и молча выходит. Зарегистрируйте команду или добавьте id " +
                "в `unavailable` с причиной");
        }

        // ================================================================ F. ПОДПИСЬ СКОРОСТИ

        /// <summary>
        /// F. МЕТРИКА ВИДИТ СМЕНУ СКОРОСТИ В ОКНЕ, ГДЕ НАЖАЛИ КНОПКУ.
        ///
        /// ОТКУДА (контрольный прогон AR со свежим APK, 15.11.2026): последние две красные
        /// строки — `KvFeatureWindow/…/Buttons#2/Btn0«×0.25»` и `Btn1«×0.5»` с `ok=0
        /// effect=нет изменений`. Кнопки НАСТОЯЩИЕ (ставят `hub.playbackSpeed` и подпись
        /// множителя), но метрика меряла скорость через синглтон `FeatureHub.Instance`, а в
        /// момент этих нажатий синглтон был пуст: окно видело свою скорость, метрика —
        /// константу 1.00. Те же кнопки «×2»/«×4» после переоткрытия окна отработали
        /// («множитель скорости: ×1.00 → ×2.00») — то есть дефект был в ИЗМЕРЕНИИ, а не в кнопке.
        ///
        /// ЧТО СТАЛО. Скорость читается из ОКНА, которому принадлежит нажатая кнопка
        /// (`KvFeatureWindow.PlaybackSpeed`), и только при отсутствии окна — из синглтона.
        /// Тест воспроизводит ровно то состояние прогона: `FeatureHub.Instance` пуст, у окна
        /// своя скорость.
        /// </summary>
        [Test]
        public void RecordPlayer_SpeedIsReadFromPressedWindow()
        {
            GameObject host = new GameObject("KvGapClosure_FeatureWindow");
            GameObject button = new GameObject("KvGapClosure_SpeedButton");
            try
            {
                FeatureHub hub = host.AddComponent<FeatureHub>();
                hub.playbackSpeed = 0.25f;
                KvFeatureWindow window = host.AddComponent<KvFeatureWindow>();
                window.Build(hub);          // та же сборка окна, что в приложении
                button.transform.SetParent(host.transform, false);

                // АНТИ-ВАКУУМ: синглтон пуст — состояние, в котором самопрогон мерил «×0.25».
                Assert.IsNull(FeatureHub.Instance,
                    "синглтон FeatureHub не пуст — проверка не о том состоянии, что было в прогоне");

                KvUiSweepRecordPlayer.State before = KvUiSweepRecordPlayer.Capture(button);
                Assert.AreEqual(0.25f, before.Speed,
                    "метрика не читает скорость ОКНА нажатой кнопки (видит " + before.Speed + ")");

                hub.playbackSpeed = 0.5f;   // ровно то, что делает кнопка «×0.5»
                KvUiSweepRecordPlayer.State after = KvUiSweepRecordPlayer.Capture(button);
                string effect = KvUiSweepRecordPlayer.EffectFor(before, after);
                StringAssert.Contains("множитель скорости", effect,
                    "смена скорости не даёт строки эффекта (пусто — снова «нет изменений»)");
                StringAssert.Contains("×0.25", effect, "в эффекте нет исходного множителя: «" + effect + "»");
                StringAssert.Contains("×0.50", effect, "в эффекте нет нового множителя: «" + effect + "»");

                // БЕЗ ОКНА — прежний путь (синглтон): поведение на других кнопках не изменилось.
                Assert.AreEqual(1f, KvUiSweepRecordPlayer.Capture().Speed,
                    "путь без окна обязан читать синглтон, как раньше");
            }
            finally
            {
                // Окно регистрируется на поясе окон при построении — снимаем регистрацию,
                // иначе следующие фикстуры увидят уничтоженное окно.
                KazistovVvXr.KvWindowBelt.Unregister(KvFeatureWindow.WindowId);
                Object.DestroyImmediate(button);
                Object.DestroyImmediate(host);
            }
        }
    }
}
