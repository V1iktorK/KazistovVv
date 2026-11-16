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
//    • B, E — НА ЖИВОМ РЕЕСТРЕ КОМАНД. Раньше обе проверки ЧИТАЛИ ТЕКСТ ИСХОДНИКОВ (regex по
//      `KvCommands.Register`): такой тест ломается от переформулировки комментария и ПРОХОДИТ
//      на неправильном поведении — он проверяет «строка есть», а не «поведение верное» (P4-3).
//      Теперь тест сам выполняет ШТАТНУЮ регистрацию (`KvCommands` заполняется тем же вызовом,
//      что в приложении) и сверяет ПОЛЯ реестра: у пары клавиш — `Hotkey` обеих команд плюс
//      подпись бинда в `KvBindings`, у окон шлема — факт регистрации каждого id. Анти-вакуум
//      на месте: реестр обязан быть заполнен, иначе «всё зарегистрировано» ничего не доказывает.
//
//  ГРАНИЦА ЧЕСТНОСТИ. Тесты не запускают игру, не открывают сцены и не ходят в сеть: пульту
//  подаётся команда напрямую в `Execute`, сетка считается по объектам в памяти, реестр
//  команд заполняется вызовом штатной регистрации хаба. `TearDown` возвращает пустую сцену.
// ================================================================================================

using System.Collections.Generic;
using System.Reflection;
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
        /// СНИМОК РЕЕСТРА ДО штатной регистрации (снимается один раз, возвращается TearDown'ом).
        ///
        /// ЗАЧЕМ. `KvCommands` — общий на весь прогон статик: регистрация наполняет его сотней
        /// команд с обработчиками, которые считают состояние, в EditMode недоступное. Оставить
        /// такое следующим фикстурам нельзя — у них свои представления о реестре (см. `KvCommands.
        /// Clear` в шапке теста B и уборку в `KvRobotCatalogCoverageTests`). Снимок и есть
        /// «пустая сцена» для реестра: тесты работают на полном реестре, набор остаётся как был.
        /// </summary>
        private static List<KvCommand> registrySnapshot;

        /// <summary>
        /// ШТАТНАЯ РЕГИСТРАЦИЯ КОМАНД В `KvCommands` — ТОТ ЖЕ ВЫЗОВ, ЧТО В ПРИЛОЖЕНИИ.
        ///
        /// ЗАЧЕМ ОНА ЗДЕСЬ (P4-3). Раньше проверки B и E грепали ТЕКСТЫ ИСХОДНИКОВ: regex по
        /// `KvCommands.Register` не видел помощника хабов `Command("id", …)`, ломался от
        /// переформулировки комментария и ПРОХОДИЛ на неправильном поведении — он проверял
        /// «строка есть», а не «поведение верное». Храповик в гейте (Tools/acceptance-gate.ps1)
        /// считает такие файлы и не даёт набору расти.
        ///
        /// ЧТО ВЫПОЛНЯЕТСЯ. `KazistovVvUIManager.RegisterCommands()` — метод шелла: он очищает
        /// реестр, регистрирует команды менеджера (включая `help.hotkeys`) и в конце зовёт
        /// `FeatureHub.RegisterCommands`, `KvStageHub.RegisterCommands` и `KvStageHub2/3/4` —
        /// то есть все шесть источников регистрации проекта одним вызовом. Метод приватный, а
        /// в EditMode шелл не собирается, поэтому вызывается отражением; вся работа — постановка
        /// команд в реестр, объекты живут на `host` и снимаются TearDown'ом.
        /// </summary>

        private static void RegisterAllCommands(GameObject host)
        {
            if (registrySnapshot == null)
            {
                registrySnapshot = new List<KvCommand>();
                foreach (KvCommand command in KvCommands.All) registrySnapshot.Add(command);
            }

            KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
            MethodInfo register = typeof(KazistovVvUIManager).GetMethod("RegisterCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(register,
                "у KazistovVvUIManager больше нет RegisterCommands() — штатная регистрация " +
                "недоступна, проверка по реестру ничего не доказывает");
            try
            {
                register.Invoke(ui, null);
            }
            catch (TargetInvocationException error)
            {
                Assert.Fail("штатная регистрация команд упала: " + error.InnerException);
            }
        }

        /// <summary>ID всех команд, РЕАЛЬНО лежащих в реестре `KvCommands` (не в тексте исходников).</summary>
        private static HashSet<string> RegisteredIds()
        {
            var ids = new HashSet<string>();
            foreach (KvCommand command in KvCommands.All)
                if (command != null && !string.IsNullOrEmpty(command.Id)) ids.Add(command.Id);
            return ids;
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
            // Штатная регистрация команд строит `KvFeatureWindow`, который кладёт себя на пояс
            // окон: убитое окно не должно пережить тест, иначе следующая фикстура увидит ссылку
            // на уничтоженный объект.
            KvWindowBelt.Unregister(KvFeatureWindow.WindowId);
            // Реестр команд — общий статик прогона: возвращаем снимок, снятый ДО штатной
            // регистрации. Иначе следующие фикстуры увидят сотню команд с обработчиками, которые
            // считают состояние, в EditMode недоступное (правка B и E ничего об этом не знает).
            if (registrySnapshot != null)
            {
                KvCommands.Clear();
                foreach (KvCommand command in registrySnapshot) KvCommands.Register(command);
                registrySnapshot = null;
            }
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
        ///
        /// КАК ПРОВЕРЯЕТСЯ (P4-3). Раньше `Hotkey` вытаскивался regex'ом из ТЕКСТА регистрации —
        /// тест проверял «строка есть», а не поведение, и ломался от правки комментария. Теперь
        /// выполняется штатная регистрация (`RegisterAllCommands`) и сверяются ПОЛЯ реестра
        /// `KvCommands`: `Hotkey` обеих команд, их неравенство, отсутствие F12 у мониторинга и
        /// совпадение подписи бинда `KvBindings` с полем команды.
        /// </summary>
        [Test]
        public void HealthAndHotkeysWindow_HaveDifferentKeysAndBothAreRegistered()
        {
            GameObject host = new GameObject("KvGapClosure_CommandRegistry");
            try
            {
                RegisterAllCommands(host);

                KvCommand healthCommand = KvCommands.Get("health.toggle");
                Assert.IsNotNull(healthCommand,
                    "health.toggle не попала в реестр — команде нечем управлять");
                KvCommand hotkeysCommand = KvCommands.Get("help.hotkeys");
                Assert.IsNotNull(hotkeysCommand,
                    "help.hotkeys не попала в реестр — окно горячих клавиш не откроется");

                string healthKey = healthCommand.Hotkey;
                string hotkeysKey = hotkeysCommand.Hotkey;

                Assert.IsNotEmpty(healthKey, "у health.toggle пустая горячая клавиша — команде нечем управлять");
                Assert.AreNotEqual(hotkeysKey, healthKey,
                    "конфликт F12 остался: health.toggle = «" + healthKey + "», help.hotkeys = «" +
                    hotkeysKey + "» — одно нажатие открывает два окна");
                Assert.AreNotEqual("F12", healthKey,
                    "мониторинг остался на F12 — окно горячих клавиш по-прежнему открывается вместе с ним");

                // Подпись в окне горячих клавиш обязана совпадать с командой, иначе справочник врёт.
                KvBindEntry bind = null;
                foreach (KvBindEntry entry in KvBindings.All())
                    if (entry != null && entry.CommandId == "health.toggle") { bind = entry; break; }
                Assert.IsNotNull(bind,
                    "health.toggle не зарегистрирован в KvBindings — окно F12 его не покажет и конфликт " +
                    "не подсветит (ровно то, что описано в Docs/ДЕРЕВЬЯ_КНОПОК_ПК.md)");
                Assert.AreEqual(healthKey, bind.Keys,
                    "подпись бинда мониторинга «" + bind.Keys + "» не совпадает с Hotkey команды «" +
                    healthKey + "» — справочник и код разошлись");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
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
        /// если проверка НЕ ВИДИТ форму регистрации, лечение «зарегистрируй ещё раз» плодит дубли.
        ///
        /// КАК ПРОВЕРЯЕТСЯ (P4-3). Раньше id собирались грепом ТЕКСТОВ исходников (три формы
        /// `Register`/`Command`) — разбор мог не увидеть регистрацию, а тест жил от правки
        /// комментария. Теперь выполняется ШТАТНАЯ регистрация (`RegisterAllCommands`: менеджер +
        /// FeatureHub + KvStageHub1–4 — ровно то, что зовёт приложение), и каждый id таблицы
        /// ищется в РЕЕСТРЕ `KvCommands`: живая команда либо явная пометка `unavailable` с
        /// причиной. Анти-вакуум: реестр обязан быть заполнен, иначе проверка «всё
        /// зарегистрировано» проходила бы на пустоте.
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

            GameObject host = new GameObject("KvGapClosure_WindowRegistry");
            try
            {
                RegisterAllCommands(host);

                HashSet<string> registered = RegisteredIds();
                Assert.Greater(registered.Count, 40,
                    "в реестре всего " + registered.Count + " команд — штатная регистрация не " +
                    "выполнена, и проверка «всё зарегистрировано» ничего не доказывала бы");

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
            finally
            {
                Object.DestroyImmediate(host);
            }
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

        // ==================================== G. СКОРОСТЬ: МЕТРИКА ИЛИ ЧЕСТНАЯ ПРИЧИНА

        /// <summary>
        /// G. КНОПКА СКОРОСТИ («×0.25»/«×0.5»): ДВА СЦЕНАРИЯ ОСТАТКА САМОПРОГОНА AR 15.11.2026.
        ///
        /// ОТКУДА. Последние две красные строки прогона (474 элемента) —
        /// `KvFeatureWindow/…/Buttons#2/Btn0«×0.25»` и `Btn1«×0.5»` с `ok=0 effect=нет изменений`
        /// в прогоне БЕЗ `-CleanState` (с чистым состоянием они проходили). Множитель — настройка
        /// ВОСПРОИЗВЕДЕНИЯ: пока запись не играет, нажатие не даёт оператору ничего заметного.
        ///
        /// ЧТО ОБЯЗАНО БЫТЬ (принцип самопрогона: каждая строка либо даёт эффект, либо называет
        /// честную причину):
        ///   1) запись ИГРАЕТ — работает МЕТРИКА: смена скорости видна снимку, исключения нет;
        ///   2) ничего не играет — нажатие даёт ПРИЧИНУ-ИСКЛЮЧЕНИЕ (`ExemptReason`) с точной
        ///      формулировкой, а не «нет изменений» и не «исчез».
        ///
        /// ФАКТ «идёт ли воспроизведение» читается из ТОГО ЖЕ ОКНА (`KvFeatureWindow` → его хаб →
        /// `KvRecordingService`): в прогоне синглтон `FeatureHub.Instance` пуст (урок 5363a19),
        /// и правило, прочитанное из глобала, видело бы простой даже во время игры.
        /// </summary>
        [Test]
        public void RecordPlayer_SpeedWhilePlaying_MetricSeesIt_NoExemption()
        {
            const string slow =
                "KazistovVv_UI/KvFeatureCanvas/KvFeatureWindow/Scroll/Viewport/Content/Buttons#2/Btn0«×0.25»";
            GameObject host = new GameObject("KvGapClosure_PlayingWindow");
            GameObject button = new GameObject("KvGapClosure_PlayingSpeedButton");
            try
            {
                FeatureHub hub = host.AddComponent<FeatureHub>();
                hub.playbackSpeed = 1f;

                // Плеер ОКНА в состоянии «воспроизведение идёт». В EditMode `Awake` не вызывается
                // (Грабли 5), поэтому сервис и флаг ставятся отражением в ТЕ ЖЕ поля, что читает
                // приложение: тест подделывает состояние, а не обходит проверку.
                var service = new KvRecordingService();
                MethodInfo setter = typeof(FeatureHub).GetProperty("Recording").GetSetMethod(true);
                Assert.IsNotNull(setter,
                    "у FeatureHub нет сеттера Recording — плеер окна недоступен, правило не о чём");
                setter.Invoke(hub, new object[] { service });
                FieldInfo playing = typeof(KvRecordingService).GetField("playing",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(playing,
                    "у KvRecordingService нет поля playing — факт воспроизведения не подделать");
                playing.SetValue(service, new KvTrajectoryRecord { name = "Тест G1" });

                KvFeatureWindow window = host.AddComponent<KvFeatureWindow>();
                window.Build(hub);
                button.transform.SetParent(host.transform, false);

                // АНТИ-ВАКУУМ: синглтон пуст — состояние самого проблемного прогона.
                Assert.IsNull(FeatureHub.Instance,
                    "синглтон FeatureHub не пуст — факт взят бы не из окна");
                Assert.IsNotNull(window.RecordingService,
                    "окно не отдаёт свой плеер — правило не может читать факт из окна");

                KvUiSweepRecordPlayer.State before = KvUiSweepRecordPlayer.Capture(button);
                Assert.IsTrue(before.Playing,
                    "факт воспроизведения НЕ прочитан из окна нажатой кнопки (пустой синглтон его теряет)");

                hub.playbackSpeed = 0.25f;   // ровно то, что делает кнопка «×0.25»
                KvUiSweepRecordPlayer.State after = KvUiSweepRecordPlayer.Capture(button);
                string effect = KvUiSweepRecordPlayer.EffectFor(before, after);
                StringAssert.Contains("множитель скорости", effect,
                    "при играющей записи смена скорости обязана быть видна снимку: «" + effect + "»");

                // Работающая кнопка НЕ получает причину-исключение: это «сработало», а не «пропущено».
                Assert.AreEqual("", KvUiSweepRecordPlayer.HonestReasonFor(slow, before, "", false),
                    "идёт воспроизведение — работать должна метрика, а не причина-исключение");
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

        /// <summary>
        /// G. НИЧЕГО НЕ ИГРАЕТ — ПРИЧИНА-ИСКЛЮЧЕНИЕ С ТОЧНОЙ ФОРМУЛИРОВКОЙ, А НЕ «нет изменений».
        /// </summary>
        [Test]
        public void RecordPlayer_SpeedWhenNothingPlays_ExemptReasonNotNoChange()
        {
            const string slow =
                "KazistovVv_UI/KvFeatureCanvas/KvFeatureWindow/Scroll/Viewport/Content/Buttons#2/Btn0«×0.25»";
            const string half =
                "KazistovVv_UI/KvFeatureCanvas/KvFeatureWindow/Scroll/Viewport/Content/Buttons#2/Btn1«×0.5»";
            const string play =
                "KazistovVv_UI/KvFeatureCanvas/KvFeatureWindow/Scroll/Viewport/Content/Buttons#1/Btn1«▶ Проиграть / Пауза (P)»";
            GameObject host = new GameObject("KvGapClosure_IdleWindow");
            GameObject button = new GameObject("KvGapClosure_IdleSpeedButton");
            try
            {
                FeatureHub hub = host.AddComponent<FeatureHub>();  // Awake не зовётся: плеера нет
                hub.playbackSpeed = 1f;
                KvFeatureWindow window = host.AddComponent<KvFeatureWindow>();
                window.Build(hub);
                button.transform.SetParent(host.transform, false);

                Assert.IsNull(FeatureHub.Instance,
                    "синглтон FeatureHub не пуст — проверка не о состоянии прогона");
                KvUiSweepRecordPlayer.State before = KvUiSweepRecordPlayer.Capture(button);
                Assert.IsFalse(before.Playing, "ничего не играет — но состояние прочитано не так");

                // 1) ПРИЧИНА НАЗВАНА И РОВНО ТА, ЧТО ЗАКАЗАНА В ЗАДАЧЕ.
                string reason = KvUiSweepRecordPlayer.HonestReasonFor(slow, before, "", false);
                Assert.AreEqual(KvUiSweepRecordPlayer.SpeedIdleReason, reason,
                    "причина обязана быть ровно той, что названа в задаче: «" + reason + "»");
                StringAssert.Contains("только во время воспроизведения", reason);
                StringAssert.Contains("ничего не играет", reason);

                // 2) ПРАВИЛО УЗКОЕ: другие кнопки плеера свои причины считают сами, а отказ САМОГО
                //    сервиса (`Failed`) главнее — его слова не подменяются правилом скорости.
                Assert.AreEqual(KvUiSweepRecordPlayer.NoRecordsReason,
                    KvUiSweepRecordPlayer.HonestReasonFor(play, before, "", false),
                    "«Проиграть» при пустом списке обязан называть записи, а не скорость");
                StringAssert.Contains("робот потока не готов",
                    KvUiSweepRecordPlayer.HonestReasonFor(slow, before, "робот потока не готов", false),
                    "отказ сервиса главнее правила скорости");

                // 3) ПРИЧИНА — ИСКЛЮЧЕНИЕ ОТЧЁТА: не «нет изменений» и не «исчез».
                var element = new KvUiSweepElement
                {
                    Path = half,
                    Ok = false,
                    Effect = reason,
                    ExemptReason = reason,
                    Method = "uGUI"
                };
                Assert.IsTrue(element.IsExempt, "причина не помечена как исключение (ExemptReason)");
                string line = element.Line;
                StringAssert.Contains("exempt=" + reason, line, "причина не попала в строку отчёта");
                StringAssert.DoesNotContain("нет изменений", line,
                    "с причиной строка обязана быть объяснением, а не «нет изменений»");
                StringAssert.DoesNotContain("исчез", line,
                    "это не исчезновение: кнопка найдена и нажата, объяснение — про воспроизведение");
            }
            finally
            {
                KazistovVvXr.KvWindowBelt.Unregister(KvFeatureWindow.WindowId);
                Object.DestroyImmediate(button);
                Object.DestroyImmediate(host);
            }
        }
    }
}
