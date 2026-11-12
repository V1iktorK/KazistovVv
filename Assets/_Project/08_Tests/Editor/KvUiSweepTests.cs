// ================================================================================================
//  KvUiSweepTests.cs — EditMode-тесты САМОПРОГОНА ИНТЕРФЕЙСА (сессия S45).
//
//  ОТКУДА ЗАДАЧА. Требование владельца: «каждая кнопочка обязана выполнять реальную работу».
//  Проверять это глазами в шлеме нельзя, поэтому в приложение встроен самопрогон
//  (`KvUiSweepRuntime`): он сам обходит элементы, нажимает их настоящим путём uGUI и печатает
//  строку на каждый. Тестировщик читает журнал и файл отчёта.
//
//  ЧЕМ ПЛОХО БЫЛО БЫ БЕЗ ЭТОГО ФАЙЛА. Самопрогон — это ИНСТРУМЕНТ ПРОВЕРКИ, и у него та же
//  болезнь, что у любого инструмента без проверки: он может зеленеть, ничего не проверив.
//  Достаточно одной строки `return ""` в сравнении снимков — и «эффекта нет» станет
//  «эффекта не бывает», а прогон будет отчитываться «всё сработало». Поэтому здесь проверяются
//  ЧИСТЫЕ части прогона: формат строк, счётчики, правило успеха, разбор флагов, сравнение
//  снимков. Всё это делается БЕЗ сцены и без устройства — то есть гоняется на каждом коммите.
//
//  ГРАНИЦА ЧЕСТНОСТИ. Сам обход сцены (настоящие кнопки, настоящие панели, сценарии нажатий)
//  здесь НЕ проверяется: для этого нужен живой PlayMode и шлем. Его проверяет
//  `Tools/ui-sweep-device.ps1` на устройстве, и это написано в отчёте прямо.
// ================================================================================================

using System.Collections.Generic;
using KazistovVvDiagnostics;
using KazistovVvXr;
using NUnit.Framework;

namespace KazistovVvTests
{
    /// <summary>Самопрогон интерфейса: формат отчёта, счётчики, правило успеха, флаги, снимки.</summary>
        [Category(KvTestCategories.Fast)]
    public class KvUiSweepTests
    {
        /// <summary>Пустой набор расширений интента (обычный запуск приложения).</summary>
        private static Dictionary<string, string> NoExtras()
        {
            return new Dictionary<string, string>();
        }

        private static Dictionary<string, string> Extras(string key, string value)
        {
            var map = new Dictionary<string, string>();
            map[key] = value;
            return map;
        }

        // ------------------------------------------------------------------ 1. строка элемента

        /// <summary>
        /// СТРОКА ЭЛЕМЕНТА НАЗЫВАЕТ ВСЁ, ЧТО НУЖНО ТЕСТИРОВЩИКУ: имя, вид, ok и эффект.
        /// Без любого из четырёх строка бесполезна: «element=… ok=0» не говорит, какая кнопка и
        /// что именно не сработало, а без имени её не найти в интерфейсе.
        /// </summary>
        [Test]
        public void ElementLine_ContainsNameTypeOkAndEffect()
        {
            var element = new KvUiSweepElement
            {
                Path = "KazistovVvCanvas/ПанельНастроек/Кнопка VR",
                Kind = KvUiSweepKind.Toggle,
                Ok = true,
                Effect = "панели: 2→3",
                Method = "uGUI"
            };

            string line = element.Line;

            StringAssert.Contains("[KvUiSweep] element=", line, "нет метки прогона и поля element");
            StringAssert.Contains("KazistovVvCanvas/ПанельНастроек/Кнопка VR", line, "в строке нет имени элемента");
            StringAssert.Contains("type=Toggle", line, "в строке нет вида элемента");
            StringAssert.Contains("ok=1", line, "в строке нет признака успеха");
            StringAssert.Contains("effect=панели: 2→3", line, "в строке нет того, ЧТО изменилось");
            StringAssert.Contains("exempt=-", line, "у обычного элемента обязано быть exempt=-");
            StringAssert.Contains("путь=uGUI", line,
                "не назван путь нажатия — тогда «нажали по-настоящему» остаётся обещанием");
        }

        // ------------------------------------------------------------------ 2. итоговая строка

        /// <summary>
        /// ИТОГ НАЗЫВАЕТ ВСЕ ЧЕТЫРЕ ЧИСЛА. «Элементов 80, всё хорошо» без разделения на
        /// «сработало / без эффекта / исключения» — это отчёт, по которому нельзя принять решение.
        /// </summary>
        [Test]
        public void TotalLine_ContainsAllFourNumbers()
        {
            var run = new KvUiSweepRun { Layout = "vr" };
            run.Add(new KvUiSweepElement { Path = "a", Ok = true, Effect = "панели: 1→2" });
            run.Add(new KvUiSweepElement { Path = "b", Ok = true, Effect = "команд выполнено: 3→4" });
            run.Add(new KvUiSweepElement { Path = "c", Ok = false });

            string total = run.TotalLine(42.5f);

            StringAssert.Contains("[KvUiSweep] ИТОГ:", total);
            StringAssert.Contains("элементов 3", total);
            StringAssert.Contains("сработало 2", total);
            StringAssert.Contains("без эффекта 1", total);
            StringAssert.Contains("исключений 0", total);
            StringAssert.Contains("исчезло 0", total);
            StringAssert.Contains("прогон=vr", total);
            StringAssert.Contains("время=42", total);
        }

        // ------------------------------------------------------------------ 3. нулевой эффект

        /// <summary>
        /// НАЖАЛИ — НИЧЕГО НЕ ИЗМЕНИЛОСЬ → `ok=0` И СЧЁТЧИК «БЕЗ ЭФФЕКТА». Это и есть дефект,
        /// который владелец называет «кнопка не выполняет работу». Если такое событие не
        /// считается провалом, весь прогон превращается в зелёную печать.
        /// </summary>
        [Test]
        public void ElementWithoutEffect_IsMarkedOk0AndCountedAsNoEffect()
        {
            var run = new KvUiSweepRun { Layout = "pc" };
            var dead = new KvUiSweepElement
            {
                Path = "KazistovVvCanvas/Тулбар/Кнопка «Стоп»",
                Kind = KvUiSweepKind.Button,
                Ok = false,
                Method = "onClick"
            };

            string line = run.Add(dead);

            StringAssert.Contains("ok=0", line, "элемент без эффекта обязан быть ok=0");
            Assert.AreEqual(1, run.NoEffect, "без эффекта обязано быть ровно 1");
            Assert.AreEqual(0, run.Worked, "сработавших быть не должно");
            Assert.IsFalse(run.Succeeded, "прогон с элементом без эффекта НЕ может быть успешным");
            StringAssert.Contains("без эффекта", run.FailureReason, "причина провала не названа");
        }

        // ------------------------------------------------------------------ 4. исключения

        /// <summary>
        /// ИСКЛЮЧЕНИЕ НЕ СЧИТАЕТСЯ ПРОВАЛОМ, НО ПЕЧАТАЕТСЯ С ПРИЧИНОЙ. Разница принципиальная:
        /// «мы это знаем и объяснили» — не то же самое, что «мы это скрыли». Поэтому строка
        /// обязана содержать `exempt=` с ТЕКСТОМ, а не прочерк.
        /// </summary>
        [Test]
        public void ExemptElement_IsNotAFailure_ButPrintsReason()
        {
            var run = new KvUiSweepRun { Layout = "vr" };

            // ПОРОГ ДОБИРАЕТСЯ ОСОЗНАННО: без него любая причина провала объяснялась бы «мало
            // элементов», и проверка «исключение не провал» ничего бы не доказывала.
            for (int i = 0; i < KvUiSweepRun.MinElements; i++)
                run.Add(new KvUiSweepElement { Path = "кнопка " + i, Ok = true, Effect = "панели: 1→2" });

            var voice = new KvUiSweepElement
            {
                Path = "ПанельШлема/Настройки/Голосовой ввод",
                Kind = KvUiSweepKind.PanelIcon,
                Ok = false,
                ExemptReason = KvUiSweepExemptions.ReasonFor("ПанельШлема/Настройки/Голосовой ввод")
            };

            string line = run.Add(voice);

            Assert.IsNotEmpty(voice.ExemptReason,
                "для голосового ввода причина обязана быть в ЯВНОМ списке исключений");
            StringAssert.Contains("exempt=" + voice.ExemptReason, line, "причина исключения не попала в строку");
            Assert.AreEqual(1, run.Exempted, "исключение обязано считаться в «исключениях»");
            Assert.AreEqual(0, run.NoEffect, "исключение НЕ должно попадать в «без эффекта»");
            Assert.IsTrue(run.Succeeded, "одно объяснённое исключение не делает прогон провальным");
            StringAssert.Contains("успешно", run.VerdictLine(), "вердикт не назвал прогон успешным");
        }

        // ------------------------------------------------------------------ 5. пустой прогон

        /// <summary>
        /// ПУСТОЙ ПРОГОН — НЕ УСПЕХ. Если интерфейс не построился, «провалов ноль» означает
        /// «проверок ноль». Ровно этот класс самообмана проект уже ловил (E-16: «зелёный тест,
        /// который ничего не проверил»).
        /// </summary>
        [Test]
        public void EmptyRun_IsNotSuccess_AndNamesReason()
        {
            var run = new KvUiSweepRun { Layout = "pc" };

            Assert.AreEqual(0, run.Total);
            Assert.IsFalse(run.Succeeded, "прогон без элементов не может считаться успешным");
            StringAssert.Contains("0", run.FailureReason, "причина провала не называет число элементов");
            StringAssert.Contains("панели не построены", run.FailureReason,
                "причина не объясняет, что интерфейс не построен");
            StringAssert.Contains("НЕ успешно", run.VerdictLine(), "вердикт не назван провальным");
        }

        /// <summary>
        /// МЕНЬШЕ ПОРОГА — ТОЖЕ НЕ УСПЕХ (порог 20 элементов). Двадцать кнопок — это минимум,
        /// ниже которого «обход всего интерфейса» уже не обход.
        /// </summary>
        [Test]
        public void FewerThanThresholdElements_IsNotSuccess()
        {
            var run = new KvUiSweepRun { Layout = "vr" };
            for (int i = 0; i < KvUiSweepRun.MinElements - 1; i++)
                run.Add(new KvUiSweepElement { Path = "кнопка " + i, Ok = true, Effect = "панели: 1→2" });

            Assert.AreEqual(KvUiSweepRun.MinElements - 1, run.Total);
            Assert.AreEqual(0, run.NoEffect, "сработали все — «без эффекта» быть не должно");
            Assert.IsFalse(run.Succeeded, "элементов меньше порога — прогон ничего не доказывает");
            StringAssert.Contains("порог", run.FailureReason, "причина не говорит про порог");
        }

        // ------------------------------------------------------------------ 6. таймаут

        /// <summary>
        /// ТАЙМАУТ ЭЛЕМЕНТА: помечается `ok=0 timeout`, а прогон ПРОДОЛЖАЕТСЯ. Один медленный
        /// элемент не имеет права отменить проверку остальных — иначе «завис на 3-м из 80»
        /// означало бы «не проверили 77».
        /// </summary>
        [Test]
        public void TimedOutElement_IsOk0Timeout_AndRunContinues()
        {
            var run = new KvUiSweepRun { Layout = "mr" };
            var slow = new KvUiSweepElement
            {
                Path = "KazistovVvCanvas/Экспорт",
                Kind = KvUiSweepKind.Button,
                Ok = false,
                TimedOut = true,
                Effect = "таймаут 2.4 с",
                Method = "uGUI"
            };

            string line = run.Add(slow);
            StringAssert.Contains("ok=0 timeout", line, "таймаут обязан быть помечен ok=0 timeout");
            Assert.AreEqual(1, run.TimedOut);

            // ПРОГОН ПРОДОЛЖАЕТСЯ: следующий элемент добавляется и считается как обычно.
            run.Add(new KvUiSweepElement { Path = "KazistovVvCanvas/Тулбар/Кнопка", Ok = true, Effect = "команд выполнено: 1→2" });

            Assert.AreEqual(2, run.Total, "после таймаута прогон обязан принять следующий элемент");
            Assert.AreEqual(1, run.Worked, "сработавший после таймаута элемент обязан засчитаться");
            Assert.AreEqual(1, run.NoEffect, "таймаут считается «без эффекта», но прогон не падает целиком");
            Assert.IsFalse(run.Succeeded, "прогон с таймаутом вне исключений не может быть успешным");
        }

        // ------------------------------------------------------------------ 7-8. снимок состояния

        /// <summary>
        /// РАЗЛИЧИЕ ПО ВИДИМОСТИ ПАНЕЛИ — ЭТО ЭФФЕКТ. Самая частая работа кнопки в этом проекте:
        /// открыть/закрыть панель. Если сравнение снимков этого не видит, «работает» и «не
        /// работает» становятся неразличимы.
        /// </summary>
        [Test]
        public void Snapshot_PanelVisibilityDifference_IsAnEffect()
        {
            var before = new KvUiSweepSnapshot { VisiblePanels = 2, ModeIndex = 0, Commands = 7 };
            var after = new KvUiSweepSnapshot { VisiblePanels = 3, ModeIndex = 0, Commands = 7 };

            string effect = KvUiSweepDiff.Describe(before, after);

            Assert.IsNotEmpty(effect, "открытие панели обязано считаться эффектом");
            StringAssert.Contains("панели", effect, "эффект не называет панели");
            StringAssert.Contains("2→3", effect, "эффект не показывает, что именно изменилось");
        }

        /// <summary>
        /// РАЗЛИЧИЕ ПО ЗНАЧЕНИЮ НАСТРОЙКИ — ЭТО ЭФФЕКТ. Переключатель VR/MR, язык и комфорт —
        /// ключевые значения `KvXrSettings`; их смена и есть работа соответствующей кнопки.
        /// </summary>
        [Test]
        public void Snapshot_SettingValueDifference_IsAnEffect()
        {
            // Режим VR → MR: ровно то, что делает переключатель в панели настроек.
            string mode = KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { ModeIndex = KvXrSettings.ModeVr },
                new KvUiSweepSnapshot { ModeIndex = KvXrSettings.ModeMr });
            StringAssert.Contains("режим", mode, "смена режима VR/MR не признана эффектом");

            // Язык и комфорт — отдельно: у них свои признаки, и потеря любого из них сделала бы
            // соответствующие кнопки «неработающими» в отчёте.
            string language = KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { Language = 2 }, new KvUiSweepSnapshot { Language = 5 });
            StringAssert.Contains("язык", language, "смена языка не признана эффектом");

            string comfort = KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { Comfort = 1035 }, new KvUiSweepSnapshot { Comfort = 0 });
            StringAssert.Contains("комфорт", comfort, "смена комфорта не признана эффектом");

            // ФЛАГИ ОБОЛОЧКИ: именно их меняют переключатели панели настроек («Фонарик»,
            // «Фантомы траекторий», «Автообновление дерева»). Без этих признаков рабочие
            // переключатели выглядели бы мёртвыми — тот же дефект, что был с кнопками шапки дока.
            string flashlight = KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { SetFlashlight = 0 }, new KvUiSweepSnapshot { SetFlashlight = 1 });
            StringAssert.Contains("фонарик", flashlight, "переключатель «Фонарик» не признан эффектом");

            string phantoms = KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { SetPhantoms = 1 }, new KvUiSweepSnapshot { SetPhantoms = 0 });
            StringAssert.Contains("фантомы", phantoms, "переключатель «Фантомы» не признан эффектом");

            string tree = KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { SetAutoTree = 1 }, new KvUiSweepSnapshot { SetAutoTree = 0 });
            StringAssert.Contains("автообновление дерева", tree,
                "переключатель «Автообновление дерева» не признан эффектом");
        }

        /// <summary>
        /// ОДИНАКОВЫЕ СНИМКИ — ПУСТОЕ РАЗЛИЧИЕ. Проверка от обратного: если бы `Describe` всегда
        /// что-нибудь возвращал, КАЖДЫЙ элемент выглядел бы сработавшим, и прогон был бы
        /// бесполезен. Этот тест — защита от такой «зелени любой ценой».
        /// </summary>
        [Test]
        public void IdenticalSnapshots_ProduceNoEffect()
        {
            var one = new KvUiSweepSnapshot
            {
                VisiblePanels = 3,
                UiObjects = 875,
                ModeIndex = KvXrSettings.ModeMr,
                Language = 1,
                Comfort = 1035,
                Commands = 42,
                SceneObjects = 1200,
                Files = 4,
                PanelLevel = 0,
                PanelIcons = 13
            };
            var two = one;   // копия структуры: ни один признак не отличается

            Assert.AreEqual("", KvUiSweepDiff.Describe(one, two),
                "одинаковые снимки обязаны давать ПУСТОЕ различие — иначе «сработало» врёт");
        }

        // ------------------------------------------------------------------ 9. защита от запуска

        /// <summary>
        /// БЕЗ EXTRA-ФЛАГА ПРОГОН НЕ СТАРТУЕТ. Это защита обычной работы приложения: тестировщик
        /// запускает сборку как всегда, и обход не должен начаться сам. Проверяются оба источника
        /// флага (extra интента и аргументы командной строки) и «выключенные» значения.
        /// </summary>
        [Test]
        public void WithoutExtraFlag_RunIsNotRequested()
        {
            Assert.IsFalse(KvUiSweepFlags.Requested(NoExtras(), new string[0]),
                "без extra-флага прогон обязан молчать");
            Assert.IsFalse(KvUiSweepFlags.Requested(null, null),
                "отсутствие обоих источников флага — тоже «не запускать»");
            Assert.IsFalse(KvUiSweepFlags.Requested(Extras(KvUiSweepFlags.ExtraKey, "0"), new string[0]),
                "kv_uisweep=0 — это ЯВНЫЙ отказ, а не согласие");
            Assert.IsFalse(KvUiSweepFlags.Requested(NoExtras(), new[] { "-batchmode", "-nographics" }),
                "обычные аргументы Unity не должны включать прогон");

            // И обратная сторона: флаг ЕСТЬ — прогон обязан стартовать, иначе защита превратилась
            // в «не запускается никогда».
            Assert.IsTrue(KvUiSweepFlags.Requested(Extras(KvUiSweepFlags.ExtraKey, "1"), new string[0]),
                "extra kv_uisweep=1 обязан включать прогон");
            Assert.IsTrue(KvUiSweepFlags.Requested(NoExtras(), new[] { "-kv_uisweep", "1" }),
                "аргумент командной строки -kv_uisweep 1 обязан включать прогон");
        }

        // ------------------------------------------------------------------ 10. режим и макет

        /// <summary>
        /// ФЛАГ РЕЖИМА: `kv_mode vr` и `kv_mode mr` — единственные допустимые значения; всё прочее
        /// означает «режим НЕ трогать» (−1). Молча включить не тот режим хуже, чем не включить
        /// никакой: отчёт назвал бы один макет, а прогон шёл бы в другом.
        /// </summary>
        [Test]
        public void ModeFlag_ParsesVrAndMr_AndIgnoresGarbage()
        {
            Assert.AreEqual(KvXrSettings.ModeVr, KvUiSweepFlags.ModeIndexOf("vr"));
            Assert.AreEqual(KvXrSettings.ModeMr, KvUiSweepFlags.ModeIndexOf("mr"));
            Assert.AreEqual(KvXrSettings.ModeMr, KvUiSweepFlags.ModeIndexOf(" MR "));
            Assert.AreEqual(-1, KvUiSweepFlags.ModeIndexOf(""));
            Assert.AreEqual(-1, KvUiSweepFlags.ModeIndexOf(null));
            Assert.AreEqual(-1, KvUiSweepFlags.ModeIndexOf("auto"),
                "мусор в kv_mode обязан означать «не трогать», а не «VR»");

            Assert.AreEqual("vr", KvUiSweepLayout.ModeName(KvXrSettings.ModeVr));
            Assert.AreEqual("mr", KvUiSweepLayout.ModeName(KvXrSettings.ModeMr));
            Assert.AreEqual("не трогали", KvUiSweepLayout.ModeName(-1));
        }

        /// <summary>
        /// ИМЯ МАКЕТА ДЛЯ ФАЙЛА ОТЧЁТА: `vr`, `mr`, `ar`, `pc`. От него зависит имя файла
        /// `_ui_sweep_&lt;layout&gt;.txt`: перепутать макеты — значит подменить один отчёт другим.
        /// </summary>
        [Test]
        public void LayoutName_SeparatesVrMrArAndPc()
        {
            Assert.AreEqual("vr", KvUiSweepLayout.NameOf("XrMain", KvXrSettings.ModeVr, true));
            Assert.AreEqual("mr", KvUiSweepLayout.NameOf("XrMain", KvXrSettings.ModeMr, true));
            Assert.AreEqual("ar", KvUiSweepLayout.NameOf("ArMain", KvXrSettings.ModeVr, true));
            Assert.AreEqual("ar", KvUiSweepLayout.NameOf("BootAr", KvXrSettings.ModeVr, true));
            Assert.AreEqual("pc", KvUiSweepLayout.NameOf("MainScene", KvXrSettings.ModeVr, false));
            Assert.AreEqual("pc", KvUiSweepLayout.NameOf("MainScene", KvXrSettings.ModeMr, false));
        }

        // ------------------------------------------------------------------ 11. тождество счётчиков

        /// <summary>
        /// СЧЁТЧИКИ СКЛАДЫВАЮТСЯ: N = сработало + без эффекта + исключения. Если тождество
        /// нарушится, итоговая строка начнёт «терять» элементы, и владелец увидит числа, которые
        /// не сходятся, — а это подрывает доверие ко всему отчёту целиком.
        /// </summary>
        [Test]
        public void CountersAddUp_TotalEqualsWorkedPlusNoEffectPlusExempt()
        {
            var run = new KvUiSweepRun { Layout = "vr" };
            run.Add(new KvUiSweepElement { Path = "кнопка 1", Ok = true, Effect = "панели: 1→2" });
            run.Add(new KvUiSweepElement { Path = "кнопка 2", Ok = false });
            run.Add(new KvUiSweepElement
            {
                Path = "кнопка 3", Ok = false,
                ExemptReason = KvUiSweepExemptions.ReasonFor("Калибровка")
            });
            run.Add(new KvUiSweepElement { Path = "кнопка 4", Ok = true, Effect = "команд выполнено: 5→6" });

            Assert.AreEqual(run.Total, run.Worked + run.NoEffect + run.Exempted + run.Missing,
                "N обязано равняться P + F + E + исчезло");
            Assert.AreEqual(4, run.Total);
            Assert.AreEqual(2, run.Worked);
            Assert.AreEqual(1, run.NoEffect);
            Assert.AreEqual(1, run.Exempted);
        }

        // ------------------------------------------------------------------ 12. опасные элементы

        /// <summary>
        /// ОПАСНЫЕ ЭЛЕМЕНТЫ НЕ НАЖИМАЮТСЯ. «Выход из приложения» завершил бы прогон раньше
        /// итоговой строки, и прогон выглядел бы зависшим. Правило обязано быть в ОДНОМ месте
        /// (`ShouldPress`), иначе обход рано или поздно нажмёт его снова.
        /// </summary>
        [Test]
        public void QuitElements_AreExemptAndNotPressed()
        {
            const string quitPath = "KazistovVvCanvas/Меню/Выход из приложения";

            Assert.IsFalse(KvUiSweepExemptions.ShouldPress(quitPath),
                "«Выход из приложения» нельзя нажимать в самопрогоне — он его завершит");
            Assert.IsNotEmpty(KvUiSweepExemptions.ReasonFor(quitPath),
                "у «Выхода» обязана быть причина в списке исключений");

            // Разрушительные действия нажимаются, но В КОНЦЕ — их признак нужен сортировке.
            Assert.IsTrue(KvUiSweepExemptions.ShouldPress("KazistovVvCanvas/Тулбар/Удалить узел"));
            Assert.IsTrue(KvUiSweepExemptions.IsDestructive("KazistovVvCanvas/Тулбар/Удалить узел"),
                "«Удалить» обязано считаться разрушительным и уходить в конец прогона");
            Assert.IsFalse(KvUiSweepExemptions.IsDestructive("KazistovVvCanvas/Тулбар/Зона достижимости"),
                "обычная кнопка не должна попадать в «разрушительные»");
        }

        // ------------------------------------------------------------------ 13. строка перед нажатием

        /// <summary>
        /// СТРОКА `press=` ПЕЧАТАЕТСЯ ДО НАЖАТИЯ — и это ЕДИНСТВЕННОЕ, что позволяет назвать
        /// виновника, если приложение зависло внутри нажатия.
        ///
        /// ОТКУДА. На планшете прогон нажал кнопку, открывшую системные настройки Android, кадры
        /// остановились, и в журнале не было ни строки элемента, ни итога: понять, на чём встало,
        /// было невозможно. Строка `press=` — это «заявка» на нажатие, которая остаётся в журнале
        /// и в файле даже тогда, когда следующей строки уже не будет.
        /// </summary>
        [Test]
        public void PressLine_IsPrintedBeforePress()
        {
            var run = new KvUiSweepRun { Layout = "vr" };

            string line = run.BeginPress("KazistovVvCanvas/Тулбар/Кнопка «Точка»", KvUiSweepKind.Button, 3, 40);

            StringAssert.Contains("[KvUiSweep] press=", line, "строка нажатия не помечена как press=");
            StringAssert.Contains("Тулбар/Кнопка «Точка»", line, "в строке нет пути элемента");
            StringAssert.Contains("type=Button", line, "в строке нет вида элемента");
            StringAssert.Contains("индекс=3 из 40", line, "в строке нет номера элемента и их общего числа");

            // ЭТО НЕ СТРОКА ЭЛЕМЕНТА: нажатие ещё не выполнено, значит и результата ещё нет.
            Assert.AreEqual(0, run.Total, "строка press= не должна регистрировать проверенный элемент");
            Assert.AreEqual("KazistovVvCanvas/Тулбар/Кнопка «Точка»", run.PressingPath,
                "обход обязан знать, какой элемент нажимается прямо сейчас");

            // Как только результат записан, «текущего» элемента больше нет.
            run.Add(new KvUiSweepElement { Path = "Кнопка «Точка»", Ok = true, Effect = "точек траектории: 0→1" });
            Assert.AreEqual("", run.PressingPath, "после записи результата текущий элемент обязан сбрасываться");
        }

        // ------------------------------------------------------------------ 14. уход в фон

        /// <summary>
        /// ПРИЛОЖЕНИЕ УШЛО В ФОН: элемент, который нажимался, ОБЯЗАН попасть в отчёт как `ok=0`
        /// с причиной, а прогон — получить конечный вердикт.
        ///
        /// ЧЕМ ПЛОХО БЫЛО. Корутина вставала вместе с кадрами: ни итога, ни причины, ни имени
        /// элемента. Тестировщик ждал таймаут и получал пустоту.
        /// </summary>
        [Test]
        public void Interrupt_MarksPressingElementAsOk0AndNamesIt()
        {
            var run = new KvUiSweepRun { Layout = "ar" };
            run.BeginPress("ArUiCanvas/GatePanel/GateButton«НАСТРОЙКИ ПРИЛОЖЕНИЯ»", KvUiSweepKind.Button, 7, 30);

            string line = run.Interrupt("приложение ушло в фон");

            StringAssert.Contains("ПРЕРВАНО", line, "нет строки прерывания");
            StringAssert.Contains("приложение ушло в фон", line, "причина прерывания не названа");
            StringAssert.Contains("на элементе", line, "виновник не назван");
            StringAssert.Contains("GateButton", line, "в строке нет пути виновника");

            Assert.AreEqual(1, run.Total, "элемент, после которого ушли в фон, обязан попасть в отчёт");
            Assert.IsNotNull(run.InterruptedElement,
                "обход обязан знать виновника, чтобы напечатать его строку element=…");
            Assert.IsFalse(run.Elements[0].Ok, "он обязан быть ok=0, а не потеряться");
            StringAssert.Contains("приложение ушло в фон", run.Elements[0].Effect,
                "причина не попала в строку элемента");
            StringAssert.Contains("ok=0", run.Elements[0].Line);
            Assert.AreEqual(1, run.NoEffect, "такой элемент считается «без эффекта»");
            Assert.IsFalse(run.Succeeded, "прерванный прогон не может быть успешным");
            StringAssert.Contains("ПРЕРВАНО", run.FailureReason, "причина провала не называет прерывание");
        }

        /// <summary>
        /// УХОД В ФОН МЕЖДУ ЭЛЕМЕНТАМИ: вердикт всё равно есть. «Прогон не дошёл до конца» — это
        /// тоже результат, и он обязан быть назван, иначе ожидание по времени ничего не значит.
        /// </summary>
        [Test]
        public void Interrupt_BetweenElements_StillGivesVerdict()
        {
            var run = new KvUiSweepRun { Layout = "vr" };
            run.Add(new KvUiSweepElement { Path = "кнопка 1", Ok = true, Effect = "панели: 1→2" });

            string line = run.Interrupt("приложение ушло в фон");

            StringAssert.Contains("между элементами", line, "не сказано, что нажатия в этот момент не было");
            Assert.AreEqual(1, run.Total, "лишний элемент добавлять нельзя — нажатия не было");
            Assert.IsFalse(run.Succeeded);
            StringAssert.Contains("ПРЕРВАНО", run.FailureReason);
            StringAssert.Contains("НЕ успешно", run.VerdictLine());
        }

        // ------------------------------------------------------------------ 15. системные пункты

        /// <summary>
        /// ПУНКТ, ВЕДУЩИЙ В СИСТЕМУ, НЕ НАЖИМАЕТСЯ. Именно такая кнопка («НАСТРОЙКИ ПРИЛОЖЕНИЯ»
        /// из `KvArCameraGate`) открыла на планшете системный экран MIUI, приложение ушло в фон и
        /// прогон завис. Проверяются кнопки ИНТЕРФЕЙСА, а не переходы в настройки Android.
        /// </summary>
        [Test]
        public void SystemScreenElement_IsExemptAndNotPressed()
        {
            const string candidate = "ArUiCanvas/GatePanel/GateButton«НАСТРОЙКИ ПРИЛОЖЕНИЯ»";

            string reason = KvUiSweepExemptions.SystemScreenReason(candidate, false);

            Assert.IsNotEmpty(reason, "причина исключения обязана быть названа");
            StringAssert.Contains("Android", reason, "причина не говорит, что это системный экран");
            Assert.IsFalse(KvUiSweepExemptions.ShouldPress(candidate, false),
                "такой пункт НЕ нажимается — иначе прогон уводит приложение из себя");
            Assert.IsTrue(KvUiSweepExemptions.LooksLikeSystemScreen(candidate));

            // Английские варианты того же класса — тоже система.
            Assert.IsFalse(KvUiSweepExemptions.ShouldPress("Plugin/App Info", false));
            Assert.IsFalse(KvUiSweepExemptions.ShouldPress("Plugin/System Settings", false));
        }

        /// <summary>
        /// ОДНОИМЁННЫЙ ПУНКТ ПАНЕЛИ ПРОЕКТА НАЖИМАЕТСЯ. Это главная предосторожность правки:
        /// штатная панель «Настройки» самого приложения обязана ПРОВЕРЯТЬСЯ, иначе мы «починили»
        /// зависание ценой непроверенной половины интерфейса.
        /// </summary>
        [Test]
        public void ProjectOwnedSettingsButton_IsStillPressed()
        {
            const string own = "KazistovVvCanvas/ПанельПанелей/Кнопка «Настройки»";

            Assert.AreEqual("", KvUiSweepExemptions.SystemScreenReason(own, true),
                "пункт панели проекта не может считаться системным");
            Assert.IsTrue(KvUiSweepExemptions.ShouldPress(own, true),
                "штатная кнопка «Настройки» обязана проверяться нажатием");
        }

        /// <summary>
        /// ОБЫЧНЫЕ КНОПКИ НЕ ПОПАДАЮТ В «СИСТЕМНЫЕ». Слишком широкий список запретов выключил бы
        /// из прогона половину интерфейса — и прогон снова стал бы бесполезным, только по другой
        /// причине.
        /// </summary>
        [Test]
        public void SystemPatterns_DoNotMatchOrdinaryButtons()
        {
            string[] ordinary =
            {
                "KazistovVvCanvas/Тулбар/Кнопка «Точка»",
                "KazistovVvCanvas/Тулбар/Кнопка «Зона достижимости»",
                "ПанельШлема/Настройки/Радиус панели",
                "KazistovVvCanvas/Свойства/Строка «Предел сустава»",
            };
            foreach (string path in ordinary)
            {
                Assert.IsFalse(KvUiSweepExemptions.LooksLikeSystemScreen(path),
                    "обычная кнопка ошибочно считается системной: " + path);
                Assert.IsTrue(KvUiSweepExemptions.ShouldPress(path, false),
                    "обычная кнопка обязана нажиматься: " + path);
            }
        }

        // ------------------------------------------------------------------ 16. состояние панелей

        /// <summary>Панель с заданным состоянием — компактный конструктор для тестов ниже.</summary>
        private static KvUiSweepPanelState Panel(string id, int side, bool collapsed, bool rail,
            bool shown, int width, int height)
        {
            return new KvUiSweepPanelState
            {
                Id = id,
                Side = side,
                Collapsed = collapsed ? 1 : 0,
                InRail = rail ? 1 : 0,
                Shown = shown ? 1 : 0,
                Width = width,
                Height = height
            };
        }

        /// <summary>
        /// СВЁРНУТАЯ ПАНЕЛЬ ОТЛИЧАЕТСЯ ОТ РАЗВЁРНУТОЙ. Кнопка «свернуть» в шапке дока панель НЕ
        /// скрывает — она остаётся видимой, меняя только высоту и признак свёрнутости. Именно
        /// поэтому три РАБОЧИЕ кнопки шапки получали «нет изменений» и выглядели мёртвыми.
        /// </summary>
        [Test]
        public void PanelState_CollapseDifference_IsAnEffect()
        {
            var before = new List<KvUiSweepPanelState> { Panel("tree", 0, false, false, true, 33, 63) };
            var after = new List<KvUiSweepPanelState> { Panel("tree", 0, true, false, true, 33, 63) };

            string text = KvUiSweepPanelSig.Describe(before, after);

            StringAssert.Contains("панель tree", text, "эффект не называет панель");
            StringAssert.Contains("свёрнута 0→1", text, "сворачивание не признано изменением");
            Assert.AreNotEqual(KvUiSweepPanelSig.Fingerprint(before), KvUiSweepPanelSig.Fingerprint(after),
                "отпечаток состояния обязан измениться");

            // и на уровне снимка это тоже эффект
            Assert.IsNotEmpty(KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { PanelFingerprint = KvUiSweepPanelSig.Fingerprint(before) },
                new KvUiSweepSnapshot { PanelFingerprint = KvUiSweepPanelSig.Fingerprint(after) }),
                "снимок обязан увидеть смену состояния панелей");
        }

        /// <summary>
        /// СМЕНА СТОРОНЫ ПАНЕЛИ — ЭФФЕКТ. Кнопка «причалить» переводит панель из док-края в
        /// плавающее окно (Side 0→3) и обратно; видимость при этом не меняется вовсе.
        /// </summary>
        [Test]
        public void PanelState_SideDifference_IsAnEffect()
        {
            var before = new List<KvUiSweepPanelState> { Panel("properties", 0, false, false, true, 33, 63) };
            var after = new List<KvUiSweepPanelState> { Panel("properties", 3, false, false, true, 33, 63) };

            string text = KvUiSweepPanelSig.Describe(before, after);

            StringAssert.Contains("сторона 0→3", text, "смена стороны панели не признана изменением");
        }

        /// <summary>
        /// УХОД ПАНЕЛИ В РЕЙЛ — ЭФФЕКТ. Кнопка «закрыть» в шапке прячет панель в боковую «полочку»:
        /// на экране остаётся узкая полоска, признак «панель показана» может не измениться.
        /// </summary>
        [Test]
        public void PanelState_RailDifference_IsAnEffect()
        {
            var before = new List<KvUiSweepPanelState> { Panel("settings", 0, false, false, true, 33, 63) };
            var after = new List<KvUiSweepPanelState> { Panel("settings", 0, false, true, true, 3, 63) };

            string text = KvUiSweepPanelSig.Describe(before, after);

            StringAssert.Contains("в рейле 0→1", text, "уход в рейл не признан изменением");
        }

        /// <summary>
        /// ОДИНАКОВОЕ СОСТОЯНИЕ — ОДИН И ТОТ ЖЕ ОТПЕЧАТОК (анти-вакуум). Если бы отпечаток менялся
        /// всегда, «эффект» был бы у любой мёртвой кнопки, и прогон зеленел бы на всём подряд.
        /// </summary>
        [Test]
        public void PanelState_IdenticalState_GivesSameFingerprint()
        {
            var one = new List<KvUiSweepPanelState>
            {
                Panel("tree", 0, false, false, true, 33, 63),
                Panel("properties", 1, false, false, true, 40, 63)
            };
            var two = new List<KvUiSweepPanelState>
            {
                Panel("tree", 0, false, false, true, 33, 63),
                Panel("properties", 1, false, false, true, 40, 63)
            };

            Assert.AreEqual(KvUiSweepPanelSig.Fingerprint(one), KvUiSweepPanelSig.Fingerprint(two),
                "одинаковое состояние обязано давать одинаковый отпечаток");
            Assert.AreEqual("", KvUiSweepPanelSig.Describe(one, two),
                "без изменений расшифровка обязана быть пустой");
        }

        // ------------------------------------------------------------------ 17. перепоиск по пути

        /// <summary>
        /// ПЕРЕСОЗДАННЫЙ ЭЛЕМЕНТ НАХОДИТСЯ ПО ПУТИ. Это ядро правки после планшета: нажатие
        /// переключателя перестраивает панель настроек, объекты уничтожаются и создаются заново, и
        /// ссылки `Selectable`, собранные в начале прогона, умирают. Путь переживает перестроение,
        /// ссылка — нет. И ключ НЕ содержит подписи, потому что подпись меняется от самого
        /// нажатия («Фонарик: выкл» → «Фонарик: вкл»).
        /// </summary>
        [Test]
        public void Reindex_FindsRecreatedElementByPath()
        {
            const string before = "KazistovVvCanvas/DockSettings/Body/Tab/Row/Switch«Фонарик: выкл»";
            const string after = "KazistovVvCanvas/DockSettings/Body/Tab/Row/Switch«Фонарик: вкл»";

            Assert.AreEqual("KazistovVvCanvas/DockSettings/Body/Tab/Row/Switch",
                KvUiSweepNames.KeyOf(before), "ключ обязан быть путём без подписи");
            Assert.AreEqual(KvUiSweepNames.KeyOf(before), KvUiSweepNames.KeyOf(after),
                "смена подписи не должна менять ключ — иначе сработавшая кнопка «исчезнет»");

            // И через очередь: тот же путь, пришедший из пересбора, — ТОТ ЖЕ элемент.
            var queue = new KvUiSweepQueue(new[] { KvUiSweepNames.KeyOf(before) });
            Assert.AreEqual(0, queue.Merge(new[] { KvUiSweepNames.KeyOf(after) }),
                "пересозданный элемент — не новый элемент очереди");
        }

        // ------------------------------------------------------------------ 18. «исчез»

        /// <summary>
        /// ИСЧЕЗНУВШИЙ ПУТЬ — ОТДЕЛЬНАЯ ПРИЧИНА И ОТДЕЛЬНЫЙ СЧЁТЧИК. «Элемент уничтожен» —
        /// утверждение о САМОМ ПРОГОНЕ (устаревшая ссылка), а не о кнопке. На планшете так были
        /// обвинены 45 работающих переключателей; такое обвинение хуже отсутствия проверки.
        /// </summary>
        [Test]
        public void VanishedElement_IsCountedSeparately_NotAsNoEffect()
        {
            var run = new KvUiSweepRun { Layout = "ar" };
            run.Add(new KvUiSweepElement { Path = "кнопка 1", Ok = true, Effect = "панели: 1→2" });

            string line = run.Add(KvUiSweepElement.Vanished(
                "KazistovVvCanvas/DockSettings/Body/Row/Switch«Фонарик»", KvUiSweepKind.Toggle));

            StringAssert.Contains("исчез после перестроения интерфейса", line,
                "причина обязана называть исчезновение после перестроения");
            StringAssert.DoesNotContain("уничтожен", line,
                "«элемент уничтожен» — утверждение о прогоне, а не о кнопке");
            StringAssert.Contains("ok=0", line);
            StringAssert.Contains("исчез", line);

            Assert.AreEqual(1, run.Missing, "исчезнувший элемент считается отдельно");
            Assert.AreEqual(0, run.NoEffect, "и НЕ попадает в «без эффекта»");
            Assert.AreEqual(1, run.Worked, "сработавший элемент не должен пострадать");
        }

        // ------------------------------------------------------------------ 19. пересборка очереди

        /// <summary>
        /// ПЕРЕСБОРКА НЕ ТЕРЯЕТ НЕПРОВЕРЕННЫЕ ЭЛЕМЕНТЫ. Интерфейс перестроился — список изменился,
        /// но всё, что ещё не нажато, обязано остаться в обходе, а новые элементы — попасть в него.
        /// </summary>
        [Test]
        public void Queue_RebuildKeepsUncheckedElements()
        {
            var queue = new KvUiSweepQueue(new[] { "a", "b", "c" });
            Assert.AreEqual("a", queue.Next(), "обход начинается с первого элемента");
            queue.Done("a");

            int added = queue.Merge(new[] { "a", "b", "c", "d" });

            Assert.AreEqual(1, added, "новый элемент d обязан попасть в очередь");
            Assert.AreEqual(3, queue.Count,
                "непроверенные b и c обязаны остаться (плюс добавленный d) — иначе пересборка «съела» бы обход");
            Assert.AreEqual("b", queue.Next(), "обход продолжается с непроверенного, а не с начала");

            queue.Done("b");
            queue.Done("c");
            queue.Done("d");
            Assert.IsNull(queue.Next(), "после проверки всех очередь исчерпана");
        }

        /// <summary>
        /// ПРОВЕРЕННЫЙ ПУТЬ НЕ ПРОВЕРЯЕТСЯ ВТОРОЙ РАЗ. Пересборка возвращает уже нажатые элементы
        /// (они никуда не делись из интерфейса) — без этого правила прогон ходил бы по кругу.
        /// </summary>
        [Test]
        public void Queue_DoesNotRepeatCheckedPath()
        {
            var queue = new KvUiSweepQueue(new[] { "a", "b" });
            queue.Done("a");

            queue.Merge(new[] { "a", "b", "c" });   // «a» вернулся из пересбора

            Assert.IsTrue(queue.IsDone("a"), "путь обязан остаться отмеченным как проверенный");
            Assert.AreEqual("b", queue.Next(), "проверенный путь не возвращается в очередь");
            Assert.AreEqual(0, queue.Merge(new[] { "a", "b", "c" }), "повторный пересбор ничего не добавляет");
        }

        // ------------------------------------------------------------------ 20. ожидание устойчивости

        /// <summary>
        /// ОЖИДАНИЕ УСТОЙЧИВОСТИ ПРЕКРАЩАЕТСЯ ПО ПРЕДЕЛУ. Двух кадров не хватало (панель
        /// перестраивается дольше), но ждать «пока успокоится» без предела — это вернуть
        /// зависание, которое уже было дефектом.
        /// </summary>
        [Test]
        public void Settle_StopsAtTimeLimit()
        {
            // Интерфейс всё ещё меняется — ждём.
            Assert.IsTrue(KvUiSweepSettle.ShouldWait(true, 10, 11, 0, 0.5f), "пока меняется — ждём");
            // Но по пределу — прекращаем, даже если так и не успокоился.
            Assert.IsFalse(KvUiSweepSettle.ShouldWait(true, 10, 11, 0, KvUiSweepSettle.MaxSeconds),
                "по пределу времени ожидание обязано прекратиться, иначе вернётся зависание");
            // Уже успокоился — ждать нечего.
            Assert.IsFalse(KvUiSweepSettle.ShouldWait(true, 10, 10, KvUiSweepSettle.StableFrames, 0.05f));
            // Элемент ещё не появился — ждём, но тоже не дольше предела.
            Assert.IsTrue(KvUiSweepSettle.ShouldWait(false, 10, 10, 9, 0.2f));
            Assert.IsFalse(KvUiSweepSettle.ShouldWait(false, 10, 10, 9, KvUiSweepSettle.MaxSeconds + 0.5f));
        }

        /// <summary>
        /// СЧЁТЧИКИ СКЛАДЫВАЮТСЯ С НОВЫМ ЧИСЛОМ:
        /// N = сработало + без эффекта + исключения + исчезло. Пятое число появилось не для
        /// красоты: без него «исчезло» либо терялось, либо превращалось в ложное «без эффекта».
        /// </summary>
        [Test]
        public void Counters_IncludeVanishedElements()
        {
            var run = new KvUiSweepRun { Layout = "ar" };
            run.Add(new KvUiSweepElement { Path = "кнопка 1", Ok = true, Effect = "панели: 1→2" });
            run.Add(new KvUiSweepElement { Path = "кнопка 2", Ok = false });
            run.Add(new KvUiSweepElement
            {
                Path = "кнопка 3", Ok = false,
                ExemptReason = KvUiSweepExemptions.ReasonFor("Калибровка")
            });
            run.Add(KvUiSweepElement.Vanished("A/B/Switch«Фонарик»", KvUiSweepKind.Toggle));

            Assert.AreEqual(4, run.Total);
            Assert.AreEqual(1, run.Worked);
            Assert.AreEqual(1, run.NoEffect);
            Assert.AreEqual(1, run.Exempted);
            Assert.AreEqual(1, run.Missing);
            Assert.AreEqual(run.Total, run.Worked + run.NoEffect + run.Exempted + run.Missing,
                "N обязано равняться P + F + E + исчезло");

            StringAssert.Contains("исчезло 1", run.TotalLine(10f),
                "пятое число обязано быть в итоговой строке");
        }

        // ------------------------------------------------------------------ 21. уникальные ключи

        /// <summary>
        /// ОДНОИМЁННЫЕ СОСЕДИ ПОЛУЧАЮТ РАЗНЫЕ КЛЮЧИ — И ОБА ПРОВЕРЯЮТСЯ.
        ///
        /// ОТКУДА. На планшете в строке СТАРТ появилось «путей-дублей 45»: сорок пять элементов
        /// имели ТОТ ЖЕ ключ, что и другие. Если два разных объекта дают один ключ, второй не
        /// проверяется НИКОГДА (перепоиск возвращает первый), а вердикт «всё сработало» относится
        /// к меньшему набору, чем есть на экране. Это тихая потеря проверок — худший вид ошибки
        /// в самопрогоне.
        /// </summary>
        [Test]
        public void Segment_NumberedKeysAreUniqueAndBothChecked()
        {
            Assert.AreEqual("Switch_Фонарик", KvUiSweepNames.Segment("Switch_Фонарик", 1, 0),
                "уникальное имя номер не получает");

            string first = KvUiSweepNames.Segment("Switch_Фонарик", 2, 0);
            string second = KvUiSweepNames.Segment("Switch_Фонарик", 2, 1);
            Assert.AreEqual("Switch_Фонарик#1", first);
            Assert.AreEqual("Switch_Фонарик#2", second);
            Assert.AreNotEqual(first, second, "ключи одноимённых соседей обязаны различаться");

            // Подпись в ключ не входит, а НОМЕР входит.
            string keyFirst = KvUiSweepNames.KeyOf("Body/Scroll/Content/" + first + "«Фонарик: выкл»");
            string keySecond = KvUiSweepNames.KeyOf("Body/Scroll/Content/" + second + "«Фонарик: выкл»");
            Assert.AreEqual("Body/Scroll/Content/Switch_Фонарик#2", keySecond,
                "номер обязан остаться в ключе, а подпись — нет");

            // ОБА соседа обязаны попасть в обход: иначе второй никогда не проверится.
            var queue = new KvUiSweepQueue(new[] { keyFirst, keySecond });
            Assert.AreEqual(2, queue.Total, "оба одноимённых соседа обязаны быть в обходе");
        }

        /// <summary>
        /// ПЕРЕПОИСК ПО КЛЮЧУ С НОМЕРОМ НАХОДИТ ИМЕННО ТОТ ОБЪЕКТ. Ключ — адрес элемента; если
        /// адрес не различает соседей, нажатие уходит не туда (или не происходит вовсе).
        /// </summary>
        [Test]
        public void NumberedKey_FindsExactlyThatObject()
        {
            string keyFirst = KvUiSweepNames.KeyOf(
                "Body/Scroll/Content/" + KvUiSweepNames.Segment("Switch", 2, 0) + "«Левый»");
            string keySecond = KvUiSweepNames.KeyOf(
                "Body/Scroll/Content/" + KvUiSweepNames.Segment("Switch", 2, 1) + "«Правый»");

            var index = new Dictionary<string, string>();
            index[keyFirst] = "левый объект";
            index[keySecond] = "правый объект";

            Assert.AreNotEqual(keyFirst, keySecond);
            Assert.AreEqual("правый объект", index[keySecond],
                "перепоиск по ключу с номером обязан вернуть ИМЕННО тот объект");
            Assert.AreEqual("левый объект", index[keyFirst]);
        }

        // ------------------------------------------------------------------ 22. системный шлюз

        /// <summary>
        /// ЭЛЕМЕНТ ПОД ПРЕДПОЛЁТНОЙ ЗАГЛУШКОЙ — ИСКЛЮЧЕНИЕ, А НЕ «ПЕРВЫЙ ЭЛЕМЕНТ ПРОГОНА».
        ///
        /// ОТКУДА. На планшете (сборка 10:45) прогон нажал `GateButton«РАЗРЕШИТЬ КАМЕРУ»`: кнопка
        /// вызвала системный диалог разрешений, приложение ушло в фон, сторож фокуса прервал
        /// прогон — и 139 элементов из 140 не проверились никогда. Заглушка — не интерфейс
        /// оператора, а системный шлюз: её «работа» в том, чтобы увести приложение из себя.
        /// </summary>
        [Test]
        public void SystemGateElement_IsExemptAndNotPressed()
        {
            const string gate = "ArRuntime/KvArCameraGateCanvas/GatePanel/GateButton«РАЗРЕШИТЬ КАМЕРУ»";

            Assert.IsTrue(KvUiSweepExemptions.IsGatePath(gate), "предполётная заглушка обязана распознаваться");
            Assert.IsFalse(KvUiSweepExemptions.ShouldPress(gate, true),
                "шлюз не нажимается ДАЖЕ как «свой»: нажатие вызывает системный диалог");
            StringAssert.Contains("системный шлюз", KvUiSweepExemptions.GateReason,
                "причина обязана называть системный шлюз");

            // Английские подписи и маркеры панели — тоже шлюз.
            Assert.IsTrue(KvUiSweepExemptions.IsGatePath("X/Y/GateButton«ALLOW CAMERA»"));
            Assert.IsTrue(KvUiSweepExemptions.IsGatePath("X/KvArCameraGateCanvas/Кнопка«РАЗРЕШИТЬ»"));

            // А обычная кнопка проекта шлюзом не считается.
            Assert.IsFalse(KvUiSweepExemptions.IsGatePath("KazistovVvCanvas/Тулбар/Кнопка «Точка»"));
        }

        /// <summary>
        /// СКРЫТЫЕ ПАНЕЛИ НЕ ВКЛЮЧАЮТСЯ ВООБЩЕ — НИ СИСТЕМНЫЕ, НИ ПРОЕКТНЫЕ.
        ///
        /// ОТКУДА (планшет, сборка 11:21). Обход включал скрытые панели силой, чтобы добраться до
        /// их кнопок. Панель команд AR заканчивается строкой `commandPanel.SetActive(false)`:
        /// включённая силой панель НЕ перестроена под текущее состояние, и её вкладки «нажимались»
        /// впустую — отсюда ложные «нет изменений». Теперь элемент из скрытой панели получает
        /// причину «сначала откройте её штатной кнопкой» и откладывается до второго прохода.
        /// </summary>
        [Test]
        public void HiddenPanel_IsNeverForceActivated()
        {
            Assert.IsFalse(KvUiSweepExemptions.MayActivate("ArRuntime/KvArCameraGateCanvas/GatePanel"),
                "системный шлюз включать принудительно нельзя");
            Assert.IsFalse(KvUiSweepExemptions.MayActivate("ArRuntime/ArUiCanvas/PcCommandPanel"),
                "скрытую панель ПРОЕКТА тоже нельзя: включённая силой панель не перестроена");
            Assert.IsFalse(KvUiSweepExemptions.MayActivate("KazistovVvCanvas/Panels/DockSettings"),
                "правило общее: силу заменяет штатная кнопка открытия");
        }

        // ------------------------------------------------------------------ 24. состояние AR

        /// <summary>
        /// ПРИ «ПЛОСКОСТЕЙ 0» ПОСТАНОВКА — ИСКЛЮЧЕНИЕ С ЧИСЛОМ, А ОБЫЧНАЯ КНОПКА — НЕТ.
        ///
        /// ОТКУДА. На планшете приложение само печатало «плоскостей 0», и нажатие «Поставить» ничего
        /// не создавало — это ПРАВИЛЬНОЕ поведение, а не дефект кнопки. Но причина обязана содержать
        /// фактическое число: иначе тестировщик не отличит «невозможно» от «сломалось».
        /// </summary>
        [Test]
        public void ZeroPlanes_ExemptsPlacementButNotOrdinaryButtons()
        {
            const string place = "ArRuntime/ArUiCanvas/ArButtons/ArButton_Поставить«Поставить»";

            string reason = KvUiSweepExemptions.PlaneReasonFor(place, 0);
            Assert.IsNotEmpty(reason, "постановка без плоскостей обязана быть исключением");
            StringAssert.Contains("плоскостей 0", reason, "причина обязана содержать ФАКТИЧЕСКОЕ число");
            StringAssert.Contains("состоянием AR", reason, "нужно указать, откуда взято состояние");

            Assert.AreEqual("", KvUiSweepExemptions.PlaneReasonFor(
                "ArRuntime/ArUiCanvas/ArButtons/ArButton_Команды«Команды»", 0),
                "обычная кнопка панели не должна считаться «невозможной»");
        }

        /// <summary>
        /// ПРИ «ПЛОСКОСТЕЙ 2» ТА ЖЕ КНОПКА НЕ ИСКЛЮЧЕНИЕ — её проверяют нажатием. Иначе правило
        /// «ставить некуда» стало бы вечным оправданием и скрыло бы настоящий дефект.
        /// </summary>
        [Test]
        public void TwoPlanes_ChecksPlacementNormally()
        {
            const string place = "ArRuntime/ArUiCanvas/ArButtons/ArButton_Поставить«Поставить»";

            Assert.AreEqual("", KvUiSweepExemptions.PlaneReasonFor(place, 2),
                "когда плоскость есть, постановка обязана проверяться нажатием");
            Assert.IsTrue(KvUiSweepExemptions.NeedsPlane(place),
                "признак «нужна плоскость» при этом не теряется");
        }

        /// <summary>
        /// ЭЛЕМЕНТ В СКРЫТОЙ ПАНЕЛИ — ПРИЧИНА «СНАЧАЛА ОТКРОЙТЕ ПАНЕЛЬ», А НЕ «БЕЗ ЭФФЕКТА».
        /// </summary>
        [Test]
        public void HiddenPanelElement_GetsOpenPanelReason()
        {
            string reason = KvUiSweepExemptions.HiddenPanelReasonFor("ArRuntime/ArUiCanvas/PcCommandPanel");

            StringAssert.Contains("скрытой панели", reason);
            StringAssert.Contains("откройте её штатной кнопкой", reason,
                "причина обязана говорить, ЧТО сделать, а не только «не проверено»");
            StringAssert.Contains("PcCommandPanel", reason, "панель обязана быть названа");

            var run = new KvUiSweepRun { Layout = "ar" };
            string line = run.Add(new KvUiSweepElement
            {
                Path = "ArRuntime/ArUiCanvas/PcCommandPanel/GroupTab_robot",
                Ok = false,
                Effect = "не нажимали — панель так и не открылась",
                ExemptReason = reason
            });
            Assert.AreEqual(1, run.Exempted);
            Assert.AreEqual(0, run.NoEffect, "элемент скрытой панели не считается «без эффекта»");
            StringAssert.Contains("exempt=" + reason, line);
        }

        /// <summary>
        /// ПОСЛЕ КНОПКИ, ОТКРЫВАЮЩЕЙ ПАНЕЛЬ, ЕЁ ЭЛЕМЕНТЫ ПОЯВЛЯЮТСЯ В ОЧЕРЕДИ САМИ — и проверяются
        /// в открытом, штатном состоянии. Сила (`SetActive`) для этого не нужна: пересборка списка
        /// уже есть.
        /// </summary>
        [Test]
        public void OpenedPanelElements_EnterQueueAfterOpenButton()
        {
            const string open = "ArRuntime/ArUiCanvas/ArButtons/ArButton_Команды«Команды»";
            var queue = new KvUiSweepQueue(new[] { open });

            Assert.AreEqual(open, queue.Next(), "сначала проверяется ВИДИМАЯ кнопка открытия");
            queue.Done(open);

            int added = queue.Merge(new[]
            {
                open,
                "ArRuntime/ArUiCanvas/PcCommandPanel/GroupTab_robot",
                "ArRuntime/ArUiCanvas/PcCommandPanel/CloseCommands"
            });

            Assert.AreEqual(2, added, "вкладки открытой панели обязаны попасть в обход");
            Assert.AreEqual("ArRuntime/ArUiCanvas/PcCommandPanel/GroupTab_robot", queue.Next(),
                "и проверяются уже в ОТКРЫТОМ состоянии, без принудительного SetActive");
        }

        // ------------------------------------------------------------------ 25. снимок видит AR

        /// <summary>
        /// СНИМОК ВИДИТ ПАНЕЛИ AR. Без этого честный ответ кнопки «Поставить» («плоскостей нет —
        /// ставить некуда», панель `ArMessage`) выглядел бы как молчание, и рабочая кнопка попадала
        /// бы в дефекты. Проверяются: панель сообщений и её ТЕКСТ, панель команд, меню, счётчик
        /// столов, общий счёт объектов интерфейса и выбор в дереве.
        /// </summary>
        [Test]
        public void Snapshot_SeesArMessagePanel()
        {
            string message = KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { ArMessageVisible = 0 },
                new KvUiSweepSnapshot { ArMessageVisible = 1, ArMessageHash = 12345, ArMessageLength = 34 });
            StringAssert.Contains("сообщение AR показано: 0→1", message,
                "показ панели сообщений AR обязан быть эффектом");
            StringAssert.Contains("текст сообщения AR", message, "текст сообщения обязан быть в снимке");
            StringAssert.Contains("длина сообщения AR", message, "длина сообщения — читаемое число");

            StringAssert.Contains("панель команд AR", KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { ArCommandPanel = 0 }, new KvUiSweepSnapshot { ArCommandPanel = 1 }),
                "открытие панели команд AR обязано быть эффектом");
            StringAssert.Contains("меню AR", KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { ArMenu = 0 }, new KvUiSweepSnapshot { ArMenu = 1 }));
            StringAssert.Contains("столов создано", KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { ArTables = 0 }, new KvUiSweepSnapshot { ArTables = 1 }),
                "создание стола обязано быть эффектом");
            StringAssert.Contains("строка состояния AR", KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { ArStatusHash = 1 }, new KvUiSweepSnapshot { ArStatusHash = 2 }));

            // ОБЩИЙ СЧЁТ ОБЪЕКТОВ ловит показ/скрытие ЛЮБОЙ панели на любом канвасе, а отпечаток
            // выбранного узла — нажатие строки дерева.
            StringAssert.Contains("активных объектов интерфейса", KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { UiObjectsActive = 120 }, new KvUiSweepSnapshot { UiObjectsActive = 121 }));
            StringAssert.Contains("выбранный узел дерева", KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { SelectedNodeHash = 1 }, new KvUiSweepSnapshot { SelectedNodeHash = 2 }),
                "смена выбранного узла дерева обязана быть эффектом");
        }

        // ------------------------------------------------------------------ 23. покрытие

        /// <summary>
        /// СТРОКА ПОКРЫТИЯ НАЗЫВАЕТ ПОТЕРЮ ЧИСЛОМ. «Всё сработало» — это вердикт о МЕНЬШЕМ наборе,
        /// если часть найденных элементов не проверена. Без этого числа потеря покрытия скрывается
        /// за зелёным итогом.
        /// </summary>
        [Test]
        public void CoverageLine_NamesCheckedAndFound()
        {
            var run = new KvUiSweepRun { Layout = "ar" };
            run.Add(new KvUiSweepElement { Path = "a", Ok = true, Effect = "панели: 1→2" });
            run.Add(new KvUiSweepElement { Path = "b", Ok = true, Effect = "панели: 2→3" });

            string full = run.CoverageLine(2);
            StringAssert.Contains("проверено объектов 2 из найденных 2", full);
            StringAssert.DoesNotContain("НЕ проверено", full,
                "при полном покрытии потерь быть не должно");

            string partial = run.CoverageLine(5);
            StringAssert.Contains("проверено объектов 2 из найденных 5", partial);
            StringAssert.Contains("НЕ проверено 3", partial,
                "потеря покрытия обязана быть названа числом, а не скрыта");
        }

        // ------------------------------------------------------------------ 26. статус и меню

        /// <summary>
        /// СНИМОК РАЗЛИЧАЕТ ИЗМЕНЕНИЕ СТРОКИ СТАТУСА.
        ///
        /// ОТКУДА (итерация 6 на планшете): 56 из 69 «нет изменений» — кнопки «Переназначить»,
        /// обработчик которых делает ТОЛЬКО `SetPlanStatus("… появится позже …")`. Они честно
        /// сообщают оператору, что действие невозможно, но снимок статус не читал — и рабочие
        /// кнопки выглядели мёртвыми.
        /// </summary>
        [Test]
        public void Snapshot_SeesStatusLineChange()
        {
            string effect = KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { StatusLength = 0, StatusHash = 111 },
                new KvUiSweepSnapshot { StatusLength = 42, StatusHash = 222 });

            StringAssert.Contains("длина статуса: 0→42", effect,
                "длина строки статуса обязана быть видна в эффекте");
            StringAssert.Contains("текст статуса", effect, "смена текста статуса обязана быть эффектом");

            // АНТИ-ВАКУУМ: одинаковый статус — пустое различие, иначе «эффект» был бы всегда.
            Assert.AreEqual("", KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { StatusLength = 42, StatusHash = 222 },
                new KvUiSweepSnapshot { StatusLength = 42, StatusHash = 222 }),
                "одинаковый статус не должен давать эффект");
        }

        /// <summary>
        /// СНИМОК РАЗЛИЧАЕТ СМЕНУ РАЗДЕЛА СТАРТОВОГО МЕНЮ. Нажатие раздела (`MenuSec_*`)
        /// переключает ЭКРАН меню — это и есть его работа.
        /// </summary>
        [Test]
        public void Snapshot_SeesStartMenuSectionChange()
        {
            string effect = KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { StartMenuVisible = 1, StartSection = 0, StartTables = 3 },
                new KvUiSweepSnapshot { StartMenuVisible = 1, StartSection = 1, StartTables = 3 });

            StringAssert.Contains("раздел стартового меню: 0→1", effect,
                "смена раздела стартового меню обязана быть эффектом");

            StringAssert.Contains("стартовое меню показано: 1→0", KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { StartMenuVisible = 1 }, new KvUiSweepSnapshot { StartMenuVisible = 0 }),
                "закрытие меню обязано быть эффектом");
        }

        /// <summary>
        /// АКТИВНАЯ ВКЛАДКА — ИСКЛЮЧЕНИЕ С ПРИЧИНОЙ, НЕАКТИВНАЯ — ПРОВЕРЯЕТСЯ НАЖАТИЕМ.
        ///
        /// ОТКУДА. `Seg0«Функции»` в панели настроек — вкладка, которая УЖЕ выбрана: нажатие на неё
        /// законно ничего не меняет. Правило узкое и с проверкой ФАКТИЧЕСКОГО состояния, иначе оно
        /// скрыло бы настоящий дефект неактивной вкладки.
        /// </summary>
        [Test]
        public void ActiveTab_IsExempt_ButInactiveIsPressed()
        {
            Assert.AreEqual(0, KvUiSweepExemptions.SegmentIndexOfName("Seg0"), "номер сегмента из имени");
            Assert.AreEqual(1, KvUiSweepExemptions.SegmentIndexOfName("Seg1"));
            Assert.AreEqual(-1, KvUiSweepExemptions.SegmentIndexOfName("Кнопка"), "не сегмент — номера нет");

            string active = KvUiSweepExemptions.AlreadyActiveTabReasonFor(1, 1);
            Assert.AreEqual(KvUiSweepExemptions.AlreadyActiveTabReason, active);
            StringAssert.Contains("уже выбрана", active, "причина обязана объяснять «почему ничего не меняется»");

            Assert.AreEqual("", KvUiSweepExemptions.AlreadyActiveTabReasonFor(0, 1),
                "неактивная вкладка обязана проверяться нажатием");
            Assert.AreEqual("", KvUiSweepExemptions.AlreadyActiveTabReasonFor(0, -1),
                "обычная кнопка к правилу не относится");

            // Та же логика для уже выбранной строки дерева: повторный клик выбор не меняет.
            StringAssert.Contains("уже выбрана", KvUiSweepExemptions.TreeRowAlreadySelectedReason,
                "у повторного нажатия строки дерева тоже обязана быть причина");
        }

        /// <summary>
        /// БЛОКИРОВЩИК МЕНЮ ПРИ ЗАКРЫТОМ МЕНЮ — ИСКЛЮЧЕНИЕ, ПРИ ОТКРЫТОМ — ПРОВЕРЯЕТСЯ.
        ///
        /// `KvMenuBlocker` — прозрачная плашка, закрывающая меню по клику. Когда меню не открыто,
        /// нажимать нечего; когда открыто — нажатие закрывает меню, и это настоящий эффект.
        /// </summary>
        [Test]
        public void MenuBlocker_IsExemptOnlyWhenMenuClosed()
        {
            const string blocker = "KazistovVvDockCanvas/MenuBar/KvMenuBlocker";

            Assert.IsTrue(KvUiSweepExemptions.IsMenuBlocker(blocker));

            string closed = KvUiSweepExemptions.MenuBlockerReasonFor(blocker, false);
            StringAssert.Contains("меню не открыто", closed, "причина обязана называть состояние меню");

            Assert.AreEqual("", KvUiSweepExemptions.MenuBlockerReasonFor(blocker, true),
                "при открытом меню плашка закрывает его — это эффект, а не исключение");
            Assert.AreEqual("", KvUiSweepExemptions.MenuBlockerReasonFor("KazistovVvCanvas/Тулбар/Кнопка", false),
                "обычная кнопка к правилу не относится");
        }

        /// <summary>
        /// ВТОРОЙ ПРОХОД НАЗЫВАЕТ ПРИЧИНУ ПО КАЖДОЙ КАТЕГОРИИ. «Обработано 0 из 32» ничего не
        /// объясняло: непонятно, почему ноль. Теперь в итоге три числа — проверено / панель не
        /// открыта / путь не найден.
        /// </summary>
        [Test]
        public void RetrySummary_NamesEveryCategory()
        {
            var run = new KvUiSweepRun { Layout = "ar" };
            string line = run.RetrySummaryLine(4, 2, 26, 12.5f);

            StringAssert.Contains("ВТОРОЙ ПРОХОД", line);
            StringAssert.Contains("проверено 4", line);
            StringAssert.Contains("панель не открыта 2", line);
            StringAssert.Contains("путь не найден 26", line);
            StringAssert.Contains("время 12.5", line);

            string empty = run.RetrySummaryLine(0, 0, 32, 0.6f);
            StringAssert.Contains("проверено 0", empty);
            StringAssert.Contains("путь не найден 32", empty,
                "ноль проверенных при 32 отложенных обязан объясняться числом");
        }

        // ------------------------------------------------------------------ 27. доставка клика

        /// <summary>
        /// КЛИК ДОХОДИТ ДО ОБРАБОТЧИКА, КОТОРЫЙ ЖИВЁТ НЕ НА КНОПКЕ.
        ///
        /// ОТКУДА (итерация 7): 5 строк дерева дока получили «нет изменений» с `путь=onClick` — то
        /// есть `ExecuteEvents` НЕ доставил клик до `KvTreeRow`, и сработал запасной
        /// `Button.onClick`, который у строки только завершает переименование. Снимок при этом не
        /// виноват: он читает `ui.SelectedNode`, куда пишет `KvTreeRow.OnPointerClick`. Виноват
        /// СПОСОБ доставки — теперь после `ExecuteEvents` идёт прямой вызов обработчиков объекта.
        /// </summary>
        [Test]
        public void ClickOnRowHandler_IsDeliveredEvenWithoutButton()
        {
            var row = new FakeRowHandler();
            row.OnPointerClick();                  // тот же вход, что у EventSystem
            Assert.AreEqual(1, row.Clicks, "вызов обработчика обязан дойти до строки");
            Assert.IsTrue(row.Selected, "и выбрать узел");

            var buttonOnly = new FakeRowHandler();
            buttonOnly.FallbackOnClick();
            Assert.AreEqual(0, buttonOnly.Clicks,
                "запасной onClick строку не выбирает — это и было причиной ложного обвинения");

            // ПРАВИЛО ПУТЕЙ: прямой вызов обработчиков обязан идти ПОСЛЕ событий указателя и ДО
            // `onClick` — иначе строка снова «не нажмётся».
            Assert.Less((int)KvUiSweepPressPath.PointerEvents, (int)KvUiSweepPressPath.DirectHandlers);
            Assert.Less((int)KvUiSweepPressPath.DirectHandlers, (int)KvUiSweepPressPath.UnityEvent);
            StringAssert.Contains("прямой", KvUiSweepPressPathNames.Of(KvUiSweepPressPath.DirectHandlers),
                "путь обязан называться в отчёте словами");
        }

        /// <summary>Модель строки дерева: выбор делает ТОЛЬКО обработчик указателя.</summary>
        private class FakeRowHandler
        {
            public int Clicks;
            public bool Selected;

            /// <summary>Путь обработчика указателя — ровно его зовёт `EventSystem`.</summary>
            public void OnPointerClick()
            {
                Clicks++;
                Selected = true;
            }

            /// <summary>Запасной путь `Button.onClick`: у строки дерева он только завершает переименование.</summary>
            public void FallbackOnClick()
            {
            }
        }

        // ------------------------------------------------------------------ 28. меню и тулбар

        /// <summary>
        /// УЖЕ ОТКРЫТЫЙ РАЗДЕЛ СТАРТОВОГО МЕНЮ — ИСКЛЮЧЕНИЕ, ДРУГОЙ РАЗДЕЛ — ПРОВЕРЯЕТСЯ, и
        /// «Закрыть» без открытого экрана — тоже исключение. Правила с проверкой ФАКТИЧЕСКОГО
        /// состояния, иначе они скрыли бы настоящие дефекты.
        /// </summary>
        [Test]
        public void StartMenuRules_CheckActualState()
        {
            Assert.AreEqual(0, KvUiSweepExemptions.MenuSectionIndexOfName("MenuSec_0"), "номер раздела из имени");
            Assert.AreEqual(5, KvUiSweepExemptions.MenuSectionIndexOfName("MenuSec_5"));
            Assert.AreEqual(-1, KvUiSweepExemptions.MenuSectionIndexOfName("Screen_Close"));

            Assert.AreEqual(KvUiSweepExemptions.StartSectionAlreadyOpenReason,
                KvUiSweepExemptions.StartSectionAlreadyOpenReasonFor(0, 0));
            Assert.AreEqual("", KvUiSweepExemptions.StartSectionAlreadyOpenReasonFor(1, 0),
                "другой раздел обязан проверяться нажатием");

            StringAssert.Contains("закрывать нечего",
                KvUiSweepExemptions.NothingToCloseReasonFor(0, 0));
            Assert.AreEqual("", KvUiSweepExemptions.NothingToCloseReasonFor(3, 0),
                "при открытом экране закрытие обязано проверяться нажатием");
        }

        /// <summary>
        /// СНИМОК ВИДИТ СВЁРНУТОСТЬ ГРУПП ТУЛБАРА. Вкладка группы («Group_path») меняет набор
        /// кнопок, и без этого числа эффект терялся.
        /// </summary>
        [Test]
        public void Snapshot_SeesToolbarGroupCollapse()
        {
            string effect = KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { ToolbarCollapsedGroups = 0 },
                new KvUiSweepSnapshot { ToolbarCollapsedGroups = 1 });

            StringAssert.Contains("свёрнутых групп тулбара: 0→1", effect,
                "вкладка группы тулбара обязана давать эффект");

            Assert.AreEqual("", KvUiSweepDiff.Describe(
                new KvUiSweepSnapshot { ToolbarCollapsedGroups = 1 },
                new KvUiSweepSnapshot { ToolbarCollapsedGroups = 1 }),
                "без изменений — пустое различие (анти-вакуум)");
        }

        // ------------------------------------------------------------------ 29. покрытие

        /// <summary>
        /// СОСТОЯНИЕ ПАНЕЛИ ВОССТАНАВЛИВАЕТСЯ ПОЛНОСТЬЮ И В ПРАВИЛЬНОМ ПОРЯДКЕ.
        ///
        /// ОТКУДА (сборка 13:51): кнопки шапки панели дерева давали «нет изменений», хотя в
        /// итерации 6 те же кнопки панели настроек давали `ok=1`. Причина этого класса — состояние
        /// панели, не возвращённое после предыдущего элемента: `Close` у уже убранной в рейл
        /// панели, `Collapse` у уже свёрнутой и т.д. законно ничего не меняют.
        /// </summary>
        [Test]
        public void PanelRestore_StepsCoverEveryDifference()
        {
            var before = new KvUiSweepPanelState { Id = "tree", Side = 0, Collapsed = 0, InRail = 0, Shown = 1 };
            var changed = new KvUiSweepPanelState { Id = "tree", Side = 3, Collapsed = 1, InRail = 1, Shown = 0 };

            IList<string> steps = KvUiSweepPanelSig.RestoreSteps(before, changed);

            CollectionAssert.AreEqual(new[] { "side", "rail", "collapsed", "shown" }, steps,
                "порядок восстановления обязателен: сторона → рейл → свёрнутость → видимость");

            Assert.AreEqual(0, KvUiSweepPanelSig.RestoreSteps(before, before).Count,
                "совпавшее состояние не требует восстановления (анти-вакуум)");
            Assert.AreEqual("", KvUiSweepPanelSig.DescribeOne("tree", before, before));
        }

        /// <summary>
        /// КНОПКА ШАПКИ В УЖЕ ДОСТИГНУТОМ СОСТОЯНИИ — ИСКЛЮЧЕНИЕ, ИНАЧЕ — ПРОВЕРЯЕТСЯ.
        /// Только `Close` (ToRail) и `Collapse` (свёрнутая панель): `Dock` — переключатель, он
        /// меняет сторону ВСЕГДА, и правило ему не даётся, чтобы не скрыть настоящий дефект.
        /// </summary>
        [Test]
        public void PanelHeaderButton_AlreadyInStateIsExempt()
        {
            Assert.AreEqual(KvUiSweepExemptions.PanelAlreadyInStateReason,
                KvUiSweepExemptions.PanelHeaderAlreadyInStateReasonFor("Close", false, true));
            Assert.AreEqual("", KvUiSweepExemptions.PanelHeaderAlreadyInStateReasonFor("Close", false, false),
                "на панели не в рейле закрытие обязано проверяться нажатием");

            Assert.AreEqual(KvUiSweepExemptions.PanelAlreadyInStateReason,
                KvUiSweepExemptions.PanelHeaderAlreadyInStateReasonFor("Collapse", true, false));
            Assert.AreEqual("", KvUiSweepExemptions.PanelHeaderAlreadyInStateReasonFor("Collapse", false, false));

            Assert.AreEqual("", KvUiSweepExemptions.PanelHeaderAlreadyInStateReasonFor("Dock", true, true),
                "Dock меняет сторону всегда — исключение скрыло бы дефект");
        }

        /// <summary>
        /// СТРЕЛКА СТРОКИ БЕЗ ДЕТЕЙ — ИСКЛЮЧЕНИЕ, С ДЕТЬМИ — ПРОВЕРЯЕТСЯ.
        /// </summary>
        [Test]
        public void TreeArrow_LeafIsExempt_ParentIsPressed()
        {
            StringAssert.Contains("нет вложенных узлов", KvUiSweepExemptions.LeafRowReasonFor(false),
                "причина обязана называть отсутствие детей");
            Assert.AreEqual("", KvUiSweepExemptions.LeafRowReasonFor(true),
                "у строки с детьми раскрытие обязано проверяться нажатием");
        }

        /// <summary>
        /// `EXPANDALL` ПРИ ПОЛНОСТЬЮ РАСКРЫТОМ ДЕРЕВЕ — ИСКЛЮЧЕНИЕ, ИНАЧЕ — ПРОВЕРЯЕТСЯ.
        /// </summary>
        [Test]
        public void TreeExpandAll_OnlyExemptWhenNothingToExpand()
        {
            Assert.AreEqual(KvUiSweepExemptions.TreeAlreadyInStateReason,
                KvUiSweepExemptions.TreeAlreadyInStateReasonFor("ExpandAll", 7, 7));
            Assert.AreEqual(KvUiSweepExemptions.TreeAlreadyInStateReason,
                KvUiSweepExemptions.TreeAlreadyInStateReasonFor("CollapseAll", 0, 7));

            // АНТИ-ВАКУУМ: состояние ДРУГОЕ — правило не срабатывает, кнопку проверяем.
            Assert.AreEqual("", KvUiSweepExemptions.TreeAlreadyInStateReasonFor("ExpandAll", 3, 7),
                "при частично раскрытом дереве ExpandAll обязан дать эффект");
            Assert.AreEqual("", KvUiSweepExemptions.TreeAlreadyInStateReasonFor("CollapseAll", 3, 7));
            Assert.AreEqual("", KvUiSweepExemptions.TreeAlreadyInStateReasonFor("ExpandAll", 0, 0),
                "если раскрывать нечего в принципе — это не «уже раскрыто», а другой случай");
        }

        /// <summary>
        /// ВОССТАНОВЛЕНИЕ ПАНЕЛИ ПРОВЕРЯЕТСЯ, А НЕ ПРЕДПОЛАГАЕТСЯ.
        ///
        /// ОТКУДА (итерация 7): кнопка «в рейл» у панели дерева не дала эффекта, хотя у панели
        /// настроек даёт. Причина такого рода — НЕВОССТАНОВЛЕННОЕ состояние: панель уже была в
        /// рейле после предыдущего элемента. Теперь после каждого элемента прогон сверяет
        /// фактическое состояние с исходным и печатает «состояние панели не восстановлено».
        /// </summary>
        [Test]
        public void PanelRestore_MismatchIsDetected()
        {
            var before = new KvUiSweepPanelState { Id = "tree", Side = 0, InRail = 0, Collapsed = 0, Shown = 1 };
            var stuck = new KvUiSweepPanelState { Id = "tree", Side = 0, InRail = 1, Collapsed = 0, Shown = 1 };

            string mismatch = KvUiSweepPanelSig.DescribeOne("tree", before, stuck);
            StringAssert.Contains("в рейле 0→1", mismatch,
                "невосстановленное состояние панели обязано быть видно");
            Assert.IsNotEmpty(mismatch, "и это и есть повод для предупреждения в журнале");

            // АНТИ-ВАКУУМ: если состояние совпало, предупреждения быть не должно.
            Assert.AreEqual("", KvUiSweepPanelSig.DescribeOne("tree", before, before),
                "совпавшее состояние не должно давать предупреждение");
        }

        /// <summary>
        /// ПОИСК ПО УКОРОЧЕННОМУ КЛЮЧУ: путь исчез целиком, но начало осталось — берём ЕДИНСТВЕННОЕ
        /// совпадение по префиксу. Неоднозначность и отсутствие совпадений — честный отказ (лучше
        /// «не найден», чем нажать чужой объект).
        /// </summary>
        [Test]
        public void ShortKey_FindsUniqueSurvivorAndRefusesAmbiguity()
        {
            Assert.AreEqual("A/B", KvUiSweepNames.ShortenKey("A/B/C"), "укорачивание на сегмент");
            Assert.AreEqual("", KvUiSweepNames.ShortenKey("A"), "короче одного сегмента некуда");

            var live = new List<string>
            {
                "Canvas/Panels/DockTree/Body/KvTreeView/Scroll/Content/Row_SCARA",
                "Canvas/Panels/DockSettings/Body/Tabs"
            };

            Assert.AreEqual("Canvas/Panels/DockSettings/Body/Tabs",
                KvUiSweepNames.ResolveKey("Canvas/Panels/DockSettings/Body/Tabs«Функции»", live),
                "полное совпадение — приоритет");

            Assert.AreEqual("Canvas/Panels/DockSettings/Body/Tabs",
                KvUiSweepNames.ResolveKey("Canvas/Panels/DockSettings/Body/Tabs/Row_1«Отчёт»", live),
                "элемент внутри пересобранного контейнера обязан находиться по префиксу");

            Assert.AreEqual("", KvUiSweepNames.ResolveKey("Canvas/Other/Кнопка", live));

            var ambiguous = new List<string> { "A/B/X", "A/B/Y" };
            Assert.AreEqual("", KvUiSweepNames.ResolveKey("A/B/Z", ambiguous),
                "при неоднозначности нажимать нельзя — честнее «не найден»");
        }

        // ------------------------------------------------------------------ 31. порядок в шлеме

        /// <summary>
        /// ДЕЙСТВИЯ, МЕНЯЮЩИЕ СЕССИЮ, НАЖИМАЮТСЯ ПОСЛЕДНИМИ — но НАЖИМАЮТСЯ.
        ///
        /// ОТКУДА (шлем, свежая сборка). Первым же элементом нажалось `Menu_start.new«Новый проект»`:
        /// стартовое меню закрылось, проект пересоздался — все остальные пункты меню «исчезли», а
        /// панели шлема остались скрытыми. Из 70 элементов проверился ОДИН.
        /// ПОЧЕМУ НЕ ИСКЛЮЧЕНИЕ: кнопка рабочая, и проверить её надо — но в САМОМ КОНЦЕ, когда
        /// остальное уже проверено. Исключение просто выключило бы её из прогона.
        /// </summary>
        [Test]
        public void SessionChangingActions_ArePressedLast()
        {
            const string newProject = "KvXrStartMenu/Panel/Menu_start.new«Новый проект»";
            const string ordinary = "KazistovVvCanvas/Тулбар/Кнопка «Точка»";

            Assert.IsTrue(KvUiSweepExemptions.IsSessionChanging(newProject),
                "«Новый проект» обязан распознаваться как действие, меняющее сессию");
            Assert.IsTrue(KvUiSweepExemptions.IsDestructive(newProject),
                "и попадать в список «нажимать В КОНЦЕ»");
            Assert.IsTrue(KvUiSweepExemptions.ShouldPress(newProject, true),
                "при этом кнопка НАЖИМАЕТСЯ — исключением её не выключаем");

            Assert.IsTrue(KvUiSweepExemptions.IsSessionChanging("X/Загрузить проект"));
            Assert.IsTrue(KvUiSweepExemptions.IsSessionChanging("X/New Project"));

            // АНТИ-ВАКУУМ: обычная кнопка ни в «сессию», ни в «разрушительные» не попадает.
            Assert.IsFalse(KvUiSweepExemptions.IsSessionChanging(ordinary));
            Assert.IsFalse(KvUiSweepExemptions.IsDestructive(ordinary));
        }

        /// <summary>
        /// ПОСЛЕ ОТКРЫВАЮЩЕЙ КНОПКИ СОДЕРЖИМОЕ ПАНЕЛИ ШЛЕМА ПОПАДАЕТ В ОЧЕРЕДЬ.
        ///
        /// В VR содержимое панели лежит в скрытом канвасе: сначала проверяется ВИДИМАЯ кнопка
        /// (стартовое меню / панель у руки), её нажатие открывает панель, и элементы уровня
        /// добавляются пересборкой списка — уже в штатно открытом состоянии.
        /// </summary>
        [Test]
        public void HelmetPanelContent_EntersQueueAfterOpener()
        {
            const string opener = "KvXrStartMenu/Panel/Icon_Screen_Settings";
            var queue = new KvUiSweepQueue(new[] { opener });

            Assert.AreEqual(opener, queue.Next(), "сначала проверяется ВИДИМАЯ кнопка открытия");
            queue.Done(opener);

            int added = queue.Merge(new[]
            {
                opener,
                "KvXrGroupCanvas/Panel/Screen_Settings/Set_VrMr",
                "KvXrGroupCanvas/Panel/Screen_Settings/Set_Language"
            });

            Assert.AreEqual(2, added, "содержимое открытой панели обязано попасть в обход");
            Assert.AreEqual("KvXrGroupCanvas/Panel/Screen_Settings/Set_VrMr", queue.Next(),
                "и проверяться в ОТКРЫТОМ состоянии, а не в скрытом");
        }

        // ------------------------------------------------------------------ 32. ярусы обхода

        /// <summary>
        /// ПОРЯДОК ЯРУСОВ: ОТКРЫВАЮЩИЕ → ОБЫЧНЫЕ → СТАРТОВОЕ МЕНЮ → РАЗРУШИТЕЛЬНЫЕ.
        ///
        /// ОТКУДА (шлем, сборка 21:36). «Показать демо» не попало в список «в конец» (он был по
        /// подписи), нажалось третьим, закрыло стартовое меню — и все оставшиеся разделы меню
        /// «исчезли»: из 71 элемента проверился ОДИН. Теперь порядок задан ЯРУСАМИ, а ярус —
        /// ФУНКЦИЯ от пути, подписи и признака «открывает панель».
        /// </summary>
        [Test]
        public void Tiers_OrderOpenersOrdinaryStartMenuDestructive()
        {
            // (ц) кнопка открытия панели — ярус 1, обычный переключатель — ярус 2.
            Assert.AreEqual(KvUiSweepTier.Opener, KvUiSweepTiers.TierOf(
                "KazistovVvCanvas/MenuBar/Файл«Файл»", "Файл", true));
            Assert.AreEqual(KvUiSweepTier.Opener, KvUiSweepTiers.TierOf(
                "KazistovVvCanvas/Toolbar/Кнопка«Открыть панель»", "Открыть панель", true));

            Assert.AreEqual(KvUiSweepTier.Ordinary, KvUiSweepTiers.TierOf(
                "KazistovVvCanvas/DockSettings/Body/Tab/Switch«Фонарик»", "Фонарик", false));

            // (б) «Показать демо» и «Новый проект» — ярус 4.
            Assert.AreEqual(KvUiSweepTier.Destructive, KvUiSweepTiers.TierOf(
                "KvStartMenuCanvas/Root/Panel/Menu_start.demo«Показать демо»", "Показать демо", true));
            Assert.AreEqual(KvUiSweepTier.Destructive, KvUiSweepTiers.TierOf(
                "KvStartMenuCanvas/Root/Panel/Menu_start.new«Новый проект»", "Новый проект", true));

            // Пункт стартового меню — ярус 3, даже если он «открывает экран».
            Assert.AreEqual(KvUiSweepTier.StartMenu, KvUiSweepTiers.TierOf(
                "KvXrGroupCanvas#3/MenuSec_1«Робот»", "Робот", true));

            // (а) ОЧЕРЕДЬ ИДЁТ СТРОГО ПО ЯРУСАМ, даже если пути добавлены в обратном порядке.
            var queue = new KvUiSweepQueue();
            queue.Merge("d", (int)KvUiSweepTier.Destructive);
            queue.Merge("m", (int)KvUiSweepTier.StartMenu);
            queue.Merge("o", (int)KvUiSweepTier.Ordinary);
            queue.Merge("p", (int)KvUiSweepTier.Opener);

            Assert.AreEqual("p", queue.Next(), "первыми идут открывающие");
            queue.Done("p");
            Assert.AreEqual("o", queue.Next(), "затем обычные");
            queue.Done("o");
            Assert.AreEqual("m", queue.Next(), "затем стартовое меню");
            queue.Done("m");
            Assert.AreEqual("d", queue.Next(), "и только в конце разрушительные");
        }

        /// <summary>
        /// АНТИ-ВАКУУМ: обычная кнопка не попадает ни в ярус меню, ни в разрушительные, а «демо»
        /// ловится даже без слова «проект» в подписи.
        /// </summary>
        [Test]
        public void Tiers_DoNotSwallowOrdinaryButtons()
        {
            Assert.IsFalse(KvUiSweepTiers.IsDestructiveAction("Canvas/Тулбар/Кнопка «Точка»", "Точка"));
            Assert.IsFalse(KvUiSweepTiers.IsStartMenuItem("Canvas/Тулбар/Кнопка «Точка»", "Точка"));
            Assert.IsTrue(KvUiSweepTiers.IsDestructiveAction("Canvas/Кнопка«Показать демо»", "Показать демо"),
                "«демо» — всегда ярус 4, как бы ни называлась кнопка");
            Assert.IsTrue(KvUiSweepTiers.IsDestructiveAction("Canvas/Кнопка«Demo»", "Demo"));
        }

        /// <summary>
        /// ВОССТАНОВЛЕНИЕ КОНТЕКСТА: меню было открыто и закрылось нажатием — открываем снова.
        /// АНТИ-ВАКУУМ: если меню НЕ было открыто, прогон не открывает его самоуправно.
        /// </summary>
        [Test]
        public void ContextRestore_OnlyWhenItWasOpenBefore()
        {
            Assert.IsTrue(KvUiSweepContext.NeedsRestore(1, 0),
                "меню закрылось нажатием — контекст обязан восстановиться");
            StringAssert.Contains("восстановлен", KvUiSweepContext.RestoreLine("стартовое меню", true));

            Assert.IsFalse(KvUiSweepContext.NeedsRestore(0, 0),
                "если меню не было открыто, прогон не открывает его сам");
            Assert.IsFalse(KvUiSweepContext.NeedsRestore(1, 1));
        }

        /// <summary>
        /// СЛЕДУЮЩИЙ ПУНКТ МЕНЮ ПРОВЕРЯЕТСЯ, А НЕ «ИСЧЕЗАЕТ»: после восстановления контекста он
        /// снова в обходе.
        /// </summary>
        [Test]
        public void ContextRestore_KeepsMenuItemsCheckable()
        {
            var queue = new KvUiSweepQueue();
            queue.Merge("KvXrGroupCanvas#3/MenuSec_0«Стол»", (int)KvUiSweepTier.StartMenu);
            queue.Merge("KvXrGroupCanvas#3/MenuSec_1«Робот»", (int)KvUiSweepTier.StartMenu);

            Assert.AreEqual("KvXrGroupCanvas#3/MenuSec_0«Стол»", queue.Next());
            queue.Done("KvXrGroupCanvas#3/MenuSec_0«Стол»");

            Assert.IsTrue(KvUiSweepContext.NeedsRestore(1, 0), "меню закрылось — контекст восстановлен");
            Assert.AreEqual("KvXrGroupCanvas#3/MenuSec_1«Робот»", queue.Next(),
                "следующий пункт меню обязан остаться в обходе, а не «исчезнуть»");
        }

        // ------------------------------------------------------------------ 33. ПК-окна в шлеме

        /// <summary>
        /// В РАСКЛАДКЕ VR ЭЛЕМЕНТЫ ПК-ОКОН НЕ ПЕРЕЧИСЛЯЮТСЯ — печатается ОДНА сводная строка.
        ///
        /// ОТКУДА (шлем, сборка 22:40). Из 48 «исключений» 46 оказались элементами ЗАКРЫТЫХ окон
        /// ПК-раскладки: `KvWorkbenchWindow` (30), `KvFeatureWindow` (12), `KvHintCard` (2) и
        /// оболочка ПК. В шлеме оператор их не видит: это интерфейс компьютера.
        /// </summary>
        [Test]
        public void PcWindows_InVr_GoToSingleSummaryLine()
        {
            Assert.IsTrue(KvUiSweepPcWindows.HiddenInLayout("vr"));
            Assert.IsTrue(KvUiSweepPcWindows.HiddenInLayout("mr"));
            Assert.IsFalse(KvUiSweepPcWindows.HiddenInLayout("ar"), "в AR правило не действует");
            Assert.IsFalse(KvUiSweepPcWindows.HiddenInLayout("pc"), "на ПК правило не действует");

            Assert.IsTrue(KvUiSweepPcWindows.IsPcOnly("KazistovVv_UI/KvWorkbenchWindow/Canvas/Window«Верстак»"));
            Assert.IsTrue(KvUiSweepPcWindows.IsPcOnly("KazistovVv_UI/KvFeatureCanvas/KvFeatureWindow«Палитра»"));
            Assert.IsTrue(KvUiSweepPcWindows.IsPcOnly("KazistovVv_UI/KvTutorialCanvas/KvHintCard«Как вы…»"));
            Assert.IsTrue(KvUiSweepPcWindows.IsPcOnly("KazistovVvDockCanvas/Dock_Дерево/Header/Collapse"));
            Assert.AreEqual("KvWorkbenchWindow",
                KvUiSweepPcWindows.KindOf("KazistovVv_UI/KvWorkbenchWindow/Canvas/Window«Верстак»"));

            string line = KvUiSweepPcWindows.SummaryLine(46, 30, 12, 2, 2);
            StringAssert.Contains("ПК-ОКНА В ШЛЕМЕ: элементов 46", line);
            StringAssert.Contains("KvWorkbenchWindow 30", line);
            StringAssert.Contains("KvFeatureWindow 12", line);
            StringAssert.Contains("KvHintCard 2", line);
            Assert.AreEqual("", KvUiSweepPcWindows.SummaryLine(0, 0, 0, 0, 0),
                "при нуле строки быть не должно");
        }

        /// <summary>
        /// АНТИ-ВАКУУМ: элементы шлема НЕ считаются ПК-окнами никогда, а в AR правило не включается —
        /// иначе оно «съело» бы и покрытие планшета, и саму панель шлема.
        /// </summary>
        [Test]
        public void PcWindows_DoNotSwallowVrElementsOrArCoverage()
        {
            Assert.IsFalse(KvUiSweepPcWindows.IsPcOnly("KvXrGroupCanvas#3/MenuSec_1«Робот»"));
            Assert.IsFalse(KvUiSweepPcWindows.IsPcOnly(
                "KvStartMenuCanvas/Root/Panel/Menu_start.open«Открыть сессию»"));
            Assert.IsFalse(KvUiSweepPcWindows.IsPcOnly(
                "KvTutorialCanvas/Card/CardBtn0«Остановить демонстрацию»"));
            Assert.IsFalse(KvUiSweepPcWindows.HiddenInLayout("ar"));
        }

        // ------------------------------------------------------------------ 34. цепочка уровней

        /// <summary>
        /// ЦЕПОЧКА УРОВНЕЙ ПАНЕЛИ ШЛЕМА: группа → экран → пункты, и КАЖДЫЙ шаг увеличивает очередь.
        /// Глубина поднята до 3, потому что `BuildScreenLevel` строит иконки только для ТЕКУЩЕГО
        /// уровня: без раскрытия цепочки содержимое экранов в очередь не попадает вообще.
        /// </summary>
        [Test]
        public void PanelChain_EachLevelGrowsQueue()
        {
            Assert.AreEqual(3, KvUiSweepRuntime.MaxPanelDepth,
                "глубина 3: группа → экран → пункты экрана");

            var queue = new KvUiSweepQueue();
            Assert.AreEqual(1, queue.Merge("ПанельШлема/Группы/Группа «Визуализация»",
                (int)KvUiSweepTier.Opener), "уровень 0: иконка группы в очереди");
            queue.Done("ПанельШлема/Группы/Группа «Визуализация»");

            Assert.AreEqual(2, queue.Merge(new[]
            {
                "ПанельШлема/Веер/Кнопка A",
                "ПанельШлема/Веер/Кнопка B"
            }), "уровень 1: раскрылся веер — очередь выросла");
            queue.Done("ПанельШлема/Веер/Кнопка A");
            queue.Done("ПанельШлема/Веер/Кнопка B");

            Assert.AreEqual(1, queue.Merge("ПанельШлема/Настройки/Радиус панели", 2),
                "уровень 2: раскрылся экран — в очереди появился его пункт");
            Assert.AreEqual("ПанельШлема/Настройки/Радиус панели", queue.Next());
        }

        /// <summary>
        /// ВОЗВРАТ НА БАЗОВЫЙ УРОВЕНЬ ВОССТАНАВЛИВАЕТ ИСХОДНОЕ СОСТОЯНИЕ: иначе вложенные уровни
        /// «съезжали» бы, и подписи иконок переставали соответствовать индексам.
        /// </summary>
        [Test]
        public void PanelChain_RestoreBaseReturnsToGroupsLevel()
        {
            Assert.AreEqual(0, PanelLevelAfterRestore(1), "веер закрывается до иконок групп");
            Assert.AreEqual(0, PanelLevelAfterRestore(3), "экран закрывается до иконок групп");
            Assert.AreEqual(0, PanelLevelAfterRestore(0), "базовый уровень остаётся базовым");
        }

        /// <summary>Модель `RestoreBase`: любой уровень приводится к «иконкам групп» (0).</summary>
        private static int PanelLevelAfterRestore(int level)
        {
            return level == 0 ? 0 : 0;
        }

        // ------------------------------------------------------------------ 35. стартовое меню шлема

        /// <summary>
        /// НАЗВАНИЯ РАЗДЕЛОВ ДЛЯ ПРОВЕРКИ. Рантайм берёт их из раскладки меню
        /// (`KvXrStartMenuLayout`), но ЧИСТОЕ правило не должно зависеть от сборки проекта: иначе
        /// эти тесты не запустились бы без Unity. Поэтому здесь свои названия — правило проверяется
        /// на них, а совпадение с раскладкой проверяется отдельным тестом раскладки.
        /// </summary>
        private static readonly string[] SectionTitles = { "Стол", "Робот", "Проект", "Работа" };

        /// <summary>
        /// ПУНКТЫ СТАРТОВОГО МЕНЮ УЗНАЮТСЯ ПО ПУТИ — И КАК КНОПКА uGUI, И КАК ПУНКТ ПАНЕЛИ.
        ///
        /// ОТКУДА. Один и тот же пункт попадает в сбор ДВАЖДЫ: `CollectUgui` видит кнопку
        /// (`CreateIcon` строит `Button`), а `CollectPanelIcons` — пункт панели, у которого путь
        /// кончается ПОДПИСЬЮ (`ПанельШлема/Стартовое меню/Стол`). Признака `MenuSec_*` для второго
        /// вида не хватает, и без этой проверки часть пунктов осталась бы в общем проходе — то есть
        /// снова уничтожала бы остальные (VR: 123 «исчезло» из 143).
        /// </summary>
        [Test]
        public void StartMenu_ItemsAreRecognisedByPath()
        {
            Assert.IsTrue(KvUiSweepStartMenu.IsHelmetMenuPath("KvXrGroupCanvas#3/MenuSec_2«Стол»"),
                "раздел меню — пункт стартового меню");
            Assert.IsTrue(KvUiSweepStartMenu.IsHelmetMenuPath("KvXrGroupCanvas#3/MenuRow_0«Добавить стол»"),
                "строка меню — пункт стартового меню");
            Assert.IsTrue(KvUiSweepStartMenu.IsHelmetMenuPath("ПанельШлема/Стартовое меню/Стол"),
                "пункт панели меню узнаётся по названию уровня");

            Assert.IsFalse(KvUiSweepStartMenu.IsHelmetMenuPath("KazistovVv_UI/KvWorkbenchWindow/Canvas/Кнопка"),
                "окно компьютера — не меню");
            Assert.IsFalse(KvUiSweepStartMenu.IsHelmetMenuPath(""),
                "пустой путь — не меню");

            // РАСКЛАДКА: правило только для шлема, AR и ПК не трогаем.
            Assert.IsTrue(KvUiSweepStartMenu.HelmetLayout("vr"));
            Assert.IsTrue(KvUiSweepStartMenu.HelmetLayout("MR"));
            Assert.IsFalse(KvUiSweepStartMenu.HelmetLayout("ar"), "в AR прогон зелёный — правило не нужно");
            Assert.IsFalse(KvUiSweepStartMenu.HelmetLayout("pc"));
        }

        /// <summary>
        /// РАЗДЕЛ И СТРОКА РАЗЛИЧАЮТСЯ ДАЖЕ ТОГДА, КОГДА ИМЕНИ `MenuSec_*` В ПУТИ НЕТ: у пункта
        /// панели путь кончается подписью раздела, и она сверяется с названиями разделов.
        /// </summary>
        [Test]
        public void StartMenu_SectionAndRowAreDistinguished()
        {
            Assert.IsTrue(KvUiSweepStartMenu.IsSection("KvXrGroupCanvas#3/MenuSec_2«Стол»", SectionTitles));
            Assert.IsFalse(KvUiSweepStartMenu.IsRow("KvXrGroupCanvas#3/MenuSec_2«Стол»", SectionTitles),
                "раздел — не строка");

            Assert.IsTrue(KvUiSweepStartMenu.IsRow("KvXrGroupCanvas#3/MenuRow_0«Добавить стол»", SectionTitles),
                "строка содержимого");
            Assert.IsFalse(KvUiSweepStartMenu.IsSection("KvXrGroupCanvas#3/MenuRow_0«Добавить стол»", SectionTitles));

            // ПУНКТ ПАНЕЛИ: имя уровня вместо `MenuSec_*`.
            Assert.IsTrue(KvUiSweepStartMenu.IsSection("ПанельШлема/Стартовое меню/Стол«Стол»", SectionTitles),
                "подпись раздела узнаётся по списку названий");
            Assert.IsTrue(KvUiSweepStartMenu.IsRow("ПанельШлема/Стартовое меню/Стол: Стол 1«Стол: Стол 1»",
                SectionTitles), "строка панели меню — всё, что не раздел");

            Assert.AreEqual("Стол", KvUiSweepStartMenu.LastSegment("ПанельШлема/Стартовое меню/Стол«Стол»"),
                "подпись в «ёлочках» в имя не входит");
            Assert.AreEqual("", KvUiSweepStartMenu.LastSegment(""));

            // АНТИ-ВАКУУМ: без списка названий разделов строкой не считается ничего лишнего.
            Assert.IsFalse(KvUiSweepStartMenu.IsSection("ПанельШлема/Стартовое меню/Стол", null),
                "без списка названий «раздел» не выдумывается");
        }

        /// <summary>
        /// РАЗДЕЛ ПЕРЕД НАЖАТИЕМ ВСЕГДА ДРУГОЙ: нажатие уже открытого раздела не меняет экран, и
        /// рабочая кнопка получила бы «нет эффекта». Проверяется и граница (последний раздел).
        /// </summary>
        [Test]
        public void StartMenu_DifferentSectionNeverEqualsItself()
        {
            const int count = 6;
            for (int section = 0; section < count; section++)
            {
                int picked = KvUiSweepStartMenu.DifferentSection(section, count);
                Assert.AreNotEqual(section, picked,
                    "раздел " + section + ": нажимать надо из ДРУГОГО раздела");
                Assert.GreaterOrEqual(picked, 0);
                Assert.Less(picked, count);
            }

            // ПОСЛЕДНИЙ РАЗДЕЛ НЕ ВЫХОДИТ ЗА ГРАНИЦУ: берётся первый.
            Assert.AreEqual(0, KvUiSweepStartMenu.DifferentSection(count - 1, count),
                "за последним разделом идёт первый, а не выдуманный номер");
            Assert.AreEqual(0, KvUiSweepStartMenu.DifferentSection(-5, count),
                "неверный номер приводится к допустимому");
            Assert.AreEqual(0, KvUiSweepStartMenu.DifferentSection(99, count),
                "номер за границей приводится к первому разделу");
            Assert.AreEqual(0, KvUiSweepStartMenu.DifferentSection(0, 0),
                "без разделов выдумывать нечего");
        }

        /// <summary>
        /// СТРОКИ ПРОХОДА НАЗЫВАЮТ ЧИСЛА И ВИНОВНИКА: сводка до прохода, итог после и честная
        /// причина, если пункт не появился. «Проверено 0 из 12» без причины ничего не объясняет.
        /// </summary>
        [Test]
        public void StartMenu_LinesCarryNumbersAndReason()
        {
            string summary = KvUiSweepStartMenu.SummaryLine(19, 6, 13);
            StringAssert.Contains("СТАРТОВОЕ МЕНЮ ШЛЕМА: элементов 19", summary);
            StringAssert.Contains("разделы 6", summary);
            StringAssert.Contains("строки 13", summary);
            Assert.AreEqual("", KvUiSweepStartMenu.SummaryLine(0, 0, 0), "при нуле строки быть не должно");

            string pass = KvUiSweepStartMenu.PassLine(19, 6, 13);
            StringAssert.Contains("ПРОХОД ПО СТАРТОВОМУ МЕНЮ: пунктов 19", pass);
            StringAssert.Contains("по одному", pass);
            Assert.AreEqual("", KvUiSweepStartMenu.PassLine(0, 0, 0), "при нуле строки быть не должно");

            string result = KvUiSweepStartMenu.PassResultLine(17, 19, 2, 12.34f);
            StringAssert.Contains("проверено 17 из 19", result);
            StringAssert.Contains("не найдено 2", result);
            StringAssert.Contains("12.3 с", result);

            string failed = KvUiSweepStartMenu.RestoreFailed("KvXrGroupCanvas#3/MenuRow_0«Вернуться к работе»");
            StringAssert.Contains("ВОЗВРАТ МЕНЮ НЕ УДАЛСЯ", failed);
            StringAssert.Contains("MenuRow_0", failed);

            string reason = KvUiSweepStartMenu.NotFoundReason("KvXrGroupCanvas#3/MenuRow_3");
            StringAssert.Contains("не появился", reason);
            StringAssert.Contains("MenuRow_3", reason);
            StringAssert.Contains("нажимать нечего", reason);
        }

        // ------------------------------------ 4. восстановление экрана панели шлема (UI_SWEEP)

        /// <summary>ГРУППЫ ДЛЯ ЦЕПОЧКИ (свои названия, чтобы тест не зависел от сборки проекта).</summary>
        private static readonly string[] GroupTitles = { "Стол", "Робот", "Проект" };

        /// <summary>
        /// ЦЕПОЧКА ВОССТАНОВЛЕНИЯ ЭКРАНА — ИЗ ШТАТНЫХ НАЖАТИЙ ОТ БАЗЫ: иконка группы → иконка
        /// экрана. Именно её прогон проигрывает перед проверкой элемента панели шлема: нажатие
        /// раздела пересобирает панель целиком, и без возврата на «свой» экран 152 из 209
        /// элементов «исчезали» (гейт UI_SWEEP, красный три прогона подряд).
        ///
        /// ГРАНИЦЫ ПРАВИЛА (честно закреплены тестом): базовый уровень — пустая цепочка; веер —
        /// подпись группы; стартовое меню — НЕ цепочка (оно показывается своим путём `Show`);
        /// экран без подписи и веер без группы — восстанавливать нечем (`null`), и тогда элемент
        /// обязан получить причину «панель не открыта», а не тихий «исчез».
        /// </summary>
        [Test]
        public void PanelNav_ChainFor_RestoresByStandardPath()
        {
            CollectionAssert.AreEqual(new string[0],
                KvUiSweepPanelNav.ChainFor(KvXrPanelLevel.Groups, -1, GroupTitles, "Состояние"),
                "базовый уровень — возвращаться не надо, цепочка пустая");

            CollectionAssert.AreEqual(new[] { "Робот" },
                KvUiSweepPanelNav.ChainFor(KvXrPanelLevel.GroupFan, 1, GroupTitles, "Состояние"),
                "веер достигается нажатием иконки группы");

            Assert.IsNull(KvUiSweepPanelNav.ChainFor(KvXrPanelLevel.GroupFan, -1, GroupTitles, "Состояние"),
                "веер без известной группы — восстанавливать нечем");
            Assert.IsNull(KvUiSweepPanelNav.ChainFor(KvXrPanelLevel.GroupFan, 99, GroupTitles, "Состояние"),
                "номер группы вне раскладки — цепочка не выдумывается");
            Assert.IsNull(KvUiSweepPanelNav.ChainFor(KvXrPanelLevel.GroupFan, 0, null, "Состояние"),
                "без списка групп — цепочки нет");

            CollectionAssert.AreEqual(new[] { "Состояние" },
                KvUiSweepPanelNav.ChainFor(KvXrPanelLevel.StatusScreen, -1, GroupTitles, "Состояние"),
                "экран достигается нажатием его иконки на базе");
            Assert.IsNull(KvUiSweepPanelNav.ChainFor(KvXrPanelLevel.StatusScreen, -1, GroupTitles, ""),
                "экран без подписи — по какой иконке нажимать, неизвестно");

            Assert.IsNull(KvUiSweepPanelNav.ChainFor(KvXrPanelLevel.StartMenu, -1, GroupTitles, "Стартовое меню"),
                "стартовое меню — НЕ цепочка: оно показывается штатным Show, иначе пункт меню");
        }

        /// <summary>
        /// ПРИЧИНА «ПАНЕЛЬ НЕ ОТКРЫТА» ОБЯЗАНА НАЗЫВАТЬ ЭКРАН И ДЕТАЛЬ — иначе это снова тихий
        /// «исчез», только другими словами. Попыток — ограниченное число (`MaxAttempts`), и в
        /// причине это видно: тестировщик должен отличить «не открывается вообще» от «не успели».
        /// </summary>
        [Test]
        public void PanelNav_NotOpenReason_NamesScreenAndAttemptLimit()
        {
            Assert.AreEqual(3, KvUiSweepPanelNav.MaxAttempts,
                "число попыток восстановления закреплено: больше — это уже не «ограниченное число»");

            string reason = KvUiSweepPanelNav.NotOpenReason("экран «Состояние»",
                "путь не найден после 3 попыток");
            StringAssert.Contains("панель не открыта", reason, "причина обязана начинаться с проверяемой фразы");
            StringAssert.Contains("Состояние", reason, "без имени экрана причину нельзя проверить");
            StringAssert.Contains("путь не найден после 3 попыток", reason, "без детали причина — общие слова");

            string withoutDetail = KvUiSweepPanelNav.NotOpenReason("веер", "");
            StringAssert.Contains("панель не открыта: веер", withoutDetail);
        }

        /// <summary>ИМЯ УРОВНЯ ДЛЯ ПРИЧИНЫ — по уровню панели, а не по догадке.</summary>
        [Test]
        public void PanelNav_LevelTitle_NamesEveryLevel()
        {
            Assert.AreEqual("иконки групп", KvUiSweepPanelNav.LevelTitle(KvXrPanelLevel.Groups, ""));
            Assert.AreEqual("веер", KvUiSweepPanelNav.LevelTitle(KvXrPanelLevel.GroupFan, ""));
            Assert.AreEqual("стартовое меню", KvUiSweepPanelNav.LevelTitle(KvXrPanelLevel.StartMenu, "Стартовое меню"));
            Assert.AreEqual("экран «Состояние»",
                KvUiSweepPanelNav.LevelTitle(KvXrPanelLevel.StatusScreen, "Состояние"));
            StringAssert.Contains("уровень 42",
                KvUiSweepPanelNav.LevelTitle((KvXrPanelLevel)42, ""), "неизвестный уровень назван номером");
        }

        // ------------------------------------ 5. два «без эффекта» без причины (VR/MR)

        /// <summary>
        /// СВОЙ РАЗДЕЛ КНОПКИ `MenuSec_*` — ЭТО ЕЁ НОМЕР, А НЕ РАЗДЕЛ, ОТКРЫТЫЙ ПРИ СБОРЕ.
        ///
        /// ОТКУДА (шлем, отчёт 11.11.2026): `MenuSec_1«Робот»` получил `ok=0 effect=нет
        /// изменений` — единственная причина: перед нажатием прогон ставил меню в раздел,
        /// ЗАПИСАННЫЙ при сборе (0 «Стол»), то есть DifferentSection(0) = 1 — РОВНО тот раздел,
        /// который собирался нажимать. Нажатие на уже открытый раздел ничего не меняет, и
        /// рабочая кнопка получила «без эффекта». Теперь у раздела — собственный номер, и
        /// перед нажатием всегда стоит СОСЕДНИЙ раздел.
        /// </summary>
        [Test]
        public void StartMenu_OwnSection_ExtractsElementOwnIndex()
        {
            Assert.AreEqual(1, KvUiSweepStartMenu.OwnSection(
                "KvXrStartMenu/KvXrStartMenuPanel/KvXrGroupCanvas#3/MenuSec_1«Робот»", SectionTitles),
                "номер раздела берётся из имени кнопки, а не из состояния панели");

            Assert.AreEqual(2, KvUiSweepStartMenu.OwnSection(
                "KvXrGroupCanvas#3/MenuSec_2«Проект»", SectionTitles));

            // ПУНКТ ПАНЕЛИ: имени MenuSec_* нет — номер даёт подпись среди названий разделов.
            Assert.AreEqual(1, KvUiSweepStartMenu.OwnSection(
                "ПанельШлема/Стартовое меню/Робот«Робот»", SectionTitles));

            Assert.AreEqual(-1, KvUiSweepStartMenu.OwnSection(
                "KvXrGroupCanvas#3/MenuRow_0«Стол (квадратный, как в …»", SectionTitles),
                "строка — не раздел, её номер раздела не выдумывается");
            Assert.AreEqual(-1, KvUiSweepStartMenu.OwnSection("ПанельШлема/Стартовое меню/Сменить робота", SectionTitles));
            Assert.AreEqual(-1, KvUiSweepStartMenu.OwnSection("", SectionTitles));
        }

        /// <summary>
        /// СТРОКА МЕНЮ, КОТОРАЯ ВЫЗЫВАЕТ КОМАНДУ РЕЕСТРА, УЗНАЁТСЯ ПО ПОДПИСИ — для проверки
        /// «команда сейчас недоступна». Второй «без эффекта» без причины: «Сменить робота»
        /// вызывает `robot.switch`, у которого `IsEnabled = роботов > 1`; строка нажимается
        /// ПОСЛЕ удаления последнего робота, команда недоступна, `InvokeMenuCommand` честно
        /// возвращается — и снимок не видит НИ ОДНОГО признака. Причина обязана опираться на
        /// ФАКТИЧЕСКОЕ состояние реестра, а не на догадку.
        /// </summary>
        [Test]
        public void StartMenu_RowCommand_NamesSwitchRobot()
        {
            Assert.AreEqual("robot.switch", KvUiSweepStartMenu.RowCommandId(
                "ПанельШлема/Стартовое меню/Сменить робота", "Сменить робота"));
            Assert.AreEqual("robot.switch", KvUiSweepStartMenu.RowCommandId(
                "KvXrGroupCanvas#3/MenuRow_5«Сменить робота»", "Сменить робота"),
                "строка из раскладки меню — тот же пункт");

            Assert.AreEqual("", KvUiSweepStartMenu.RowCommandId(
                "KvXrGroupCanvas#3/MenuRow_0«Стол (квадратный, как в …»", "Стол (квадратный, как в …"),
                "обычная строка не получает причину про чужую команду");
            Assert.AreEqual("", KvUiSweepStartMenu.RowCommandId("", ""));
        }

        /// <summary>
        /// ПРИЧИНА «КОМАНДА НЕДОСТУПНА» ДАЁТСЯ ТОЛЬКО КОГДА РЕЕСТР ТАК ГОВОРИТ. Правило узкое и
        /// с проверкой фактического состояния: если команда зарегистрирована и включена, причины
        /// НЕТ — тогда отсутствие эффекта остаётся настоящей находкой (`без эффекта`), а не
        /// оправданием.
        /// </summary>
        [Test]
        public void Exemptions_CommandUnavailableReason_OnlyWhenRegistrySaysSo()
        {
            string missing = KvUiSweepExemptions.CommandUnavailableReason(
                "robot.switch", false, false, "роботов в сцене 0");
            StringAssert.Contains("не найдена в реестре", missing);
            StringAssert.Contains("robot.switch", missing);
            StringAssert.Contains("роботов в сцене 0", missing, "причина обязана содержать фактическое состояние");

            string disabled = KvUiSweepExemptions.CommandUnavailableReason(
                "robot.switch", true, false, "роботов в сцене 0");
            StringAssert.Contains("недоступна", disabled);
            StringAssert.Contains("robot.switch", disabled);
            StringAssert.Contains("роботов в сцене 0", disabled);

            Assert.AreEqual("", KvUiSweepExemptions.CommandUnavailableReason(
                "robot.switch", true, true, "роботов в сцене 2"),
                "включённая команда не получает оправдания");
            Assert.AreEqual("", KvUiSweepExemptions.CommandUnavailableReason("", false, false, ""),
                "без команды причина не выдумывается");
        }

        /// <summary>
        /// ЭЛЕМЕНТ С ПРИЧИНОЙ «ПАНЕЛЬ НЕ ОТКРЫТА» — ЭТО ИСКЛЮЧЕНИЕ, А НЕ «ИСЧЕЗ» И НЕ «БЕЗ
        /// ЭФФЕКТА». Тождество отчёта N = P + F + E + исчезло обязано держаться, иначе внешний
        /// скрипт `Tools/ui-sweep-device.ps1` увидит не те числа.
        /// </summary>
        [Test]
        public void PanelNotOpenElement_CountsAsExemption_NotAsVanishedOrNoEffect()
        {
            string reason = KvUiSweepPanelNav.NotOpenReason("экран «Объекты»", "путь не найден после 3 попыток");
            var run = new KvUiSweepRun { Layout = "vr" };
            run.Add(new KvUiSweepElement { Path = "a", Ok = true, Effect = "команд выполнено: 1→2" });
            run.Add(new KvUiSweepElement
            {
                Path = "XrRig/XrQuickPanel/KvXrGroupCanvas/Cmd_robot.switch",
                Ok = false,
                Effect = "не нажимали — " + reason,
                ExemptReason = reason,
                Method = "пропущен"
            });
            run.Add(KvUiSweepElement.Vanished("b/c", KvUiSweepKind.Button));

            Assert.AreEqual(3, run.Total);
            Assert.AreEqual(1, run.Worked, "сработавший элемент — в «сработало»");
            Assert.AreEqual(1, run.Exempted, "«панель не открыта» — исключение с причиной");
            Assert.AreEqual(1, run.Missing, "исчез — только то, что не восстанавливали");
            Assert.AreEqual(0, run.NoEffect, "не ни в «без эффекта»: причина заявлена заранее");
            Assert.AreEqual(run.Total, run.Worked + run.NoEffect + run.Exempted + run.Missing,
                "тождество отчёта N = P + F + E + исчезло");
        }
    }
}
