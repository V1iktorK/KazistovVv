// ================================================================================================
//  KvVrMrPanelSwitchTests.cs — EditMode-тесты ПУНКТА «РЕЖИМ VR/MR» В ПАНЕЛИ ШЛЕМА (12.11.2026).
//
//  ОТКУДА ЗАДАЧА. Владелец, дословно: «Меня не устраивает, что в пункте "настройки" в MR/VR
//  версии нет переключателя VR/MR — добавь.»
//
//  ПОЧЕМУ ОДНИХ ПРОВЕРОК ИСХОДНИКА МАЛО. Прежний набор (`KvVrMrSwitchTests`) проверял строку
//  `case 6: ToggleMixedReality()` в тексте панели. Этого хватает, чтобы поймать «обработчик
//  привязан к чужой строке», но НЕ хватает, чтобы поймать «пункт нигде не нажимается»: текст в
//  исходнике есть, а по факту луч упирается в надпись, а не в кнопку. Здесь панель
//  СОБИРАЕТСЯ В ПАМЯТИ и по ней ищется настоящая цель луча с настоящим названием.
//
//  ЧТО ПРОВЕРЯЕТСЯ (по пунктам задания владельца):
//    а) пункт ЕСТЬ в модели настроек шлема (`KvXrScreenModel.BuildSettings`) и его заголовок
//       содержит и VR, и MR — иначе его снова никто не найдёт;
//    б) нажатие меняет `KvXrSettings.GetModeIndex` туда и обратно;
//    в) подпись пункта отражает ФАКТИЧЕСКИЙ режим: сменили режим — сменилась подпись;
//    г) значение, установленное на ПК, читается в шлеме (одно хранилище, а не два режима);
//    д) АНТИ-ВАКУУМ: в модели настроек есть и другие пункты (проверка не проходит на пустом
//       списке), и найденная цель луча — не «Закрыть» и не «Дальше».
//
//  ЧЕГО ЗДЕСЬ СОЗНАТЕЛЬНО НЕТ: ЧТЕНИЯ ИСХОДНИКОВ С ДИСКА. Храповик P4-3 в
//  `Tools/acceptance-gate.ps1` считает файлы тестов, которые читают исходники, и его порог
//  (56) выбран по текущему составу набора: новый такой файл поднял бы счётчик и запрещён.
//  Поэтому проверки поведения идут через СОБРАННУЮ В ПАМЯТИ панель и через чистые функции —
//  это строже грепа: ловится не «текст на месте», а «кнопка действительно нажимается».
//
//  СТИЛЬ — КАК В ПРОЕКТЕ: где сцену не запустить, проверяются ЧИСТЫЕ функции и панель В ПАМЯТИ.
// ================================================================================================

using System.Collections.Generic;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Пункт «Режим VR/MR» в настройках шлема: есть, называется, нажимается, честен.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvVrMrPanelSwitchTests
    {
        // --------------------------------------------------------------- сохранение окружения
        //
        // ТЕСТ НЕ ИМЕЕТ ПРАВА МЕНЯТЬ НАСТРОЙКИ МАШИНЫ. Режим живёт в PlayerPrefs — то есть в общих
        // настройках разработчика. Без сохранения/восстановления прогон оставил бы владельцу
        // включённый MR, и следующий запуск шлема стартовал бы не в VR.

        private int savedPassthrough;
        private bool hadSavedPassthrough;

        [SetUp]
        public void SaveEnvironment()
        {
            hadSavedPassthrough = PlayerPrefs.HasKey(KvXrSettings.KeyPassthrough);
            savedPassthrough = PlayerPrefs.GetInt(KvXrSettings.KeyPassthrough, 0);
        }

        [TearDown]
        public void RestoreEnvironment()
        {
            if (hadSavedPassthrough) PlayerPrefs.SetInt(KvXrSettings.KeyPassthrough, savedPassthrough);
            else PlayerPrefs.DeleteKey(KvXrSettings.KeyPassthrough);
            PlayerPrefs.Save();
        }

        /// <summary>Панель в памяти, открытая на экране «Настройки».</summary>
        private static KvXrGroupPanel NewSettingsPanel(out GameObject host)
        {
            host = new GameObject("KvVrMrPanelSwitchTestHost");
            var panel = host.AddComponent<KvXrGroupPanel>();
            panel.logOnBuild = false;
            panel.followHand = false;
            panel.Rebuild();
            panel.OpenScreen(KvXrPanelLevel.SettingsScreen);
            return panel;
        }

        /// <summary>Номер ЦЕЛИ ЛУЧА по началу её названия (−1 — нет такой).</summary>
        private static int IndexOfTarget(KvXrGroupPanel panel, string titlePrefix)
        {
            for (int i = 0; i < panel.RayTargetCount; i++)
            {
                string title = panel.TitleAt(i);
                if (title != null && title.StartsWith(titlePrefix, System.StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// ОТКРЫТЬ ПУНКТ «РЕЖИМ VR/MR» ТАК ЖЕ, КАК ЭТО ДЕЛАЕТ ОПЕРАТОР: кнопкой «Дальше».
        ///
        /// ПОЧЕМУ НЕ «ПРОСТО ВЗЯТЬ ИНДЕКС». Экран «Настройки» показывает ОДИН параметр за раз
        /// (`settingsIndex`), и на кнопке режима светится только седьмой. Если бы тест лез во
        /// внутреннее поле, он проверял бы несуществующий путь: оператор идёт кнопкой «Дальше».
        /// Здесь нажимается ровно она — столько раз, сколько нужно, чтобы дойти до режима.
        /// </summary>
        private static int OpenModeItemLikeOperator(KvXrGroupPanel panel, int maxPresses = 12)
        {
            for (int press = 0; press <= maxPresses; press++)
            {
                int mode = IndexOfTarget(panel, "Режим VR/MR");
                if (mode >= 0) return mode;

                int next = IndexOfTarget(panel, "Следующий параметр");
                if (next < 0) break;
                panel.PressIndex(next);
            }
            return IndexOfTarget(panel, "Режим VR/MR");
        }

        // ==================================================== а) ПУНКТ ЕСТЬ И НАЗВАН

        /// <summary>
        /// ПУНКТ «РЕЖИМ VR/MR» ЕСТЬ В МОДЕЛИ НАСТРОЕК, И В ЗАГОЛОВКЕ ВИДНЫ ОБА НАЗВАНИЯ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. Владелец искал в «Настройках» переключатель VR/MR и не
        /// находил: строка называлась «Режим комнаты (MR)», и про VR в ней не было ни слова.
        /// Пункт, в котором не видно ОБОИХ названий режимов, переключателем не читается — он
        /// выглядит галкой про комнату.
        /// </summary>
        [Test]
        public void SettingsModel_HasModeRowNamingBothModes()
        {
            var data = new KvXrSettingsData { PanelRadius = 0.11f, MixedReality = false };
            List<string> rows = KvXrScreenModel.BuildSettings(data);

            Assert.Greater(rows.Count, 7,
                "в модели настроек меньше семи параметров — пункт режима потерялся");

            string all = string.Join("\n", rows.ToArray());
            StringAssert.Contains("Режим VR/MR", all,
                "в настройках шлема НЕТ пункта «Режим VR/MR» — ровно на это жаловался владелец");

            // Оба названия режимов обязаны быть видны в строке режима: иначе выбор неразличим.
            int modeRow = -1;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null && rows[i].Contains("Режим VR/MR")) modeRow = i;
            Assert.GreaterOrEqual(modeRow, 0, "строка «Режим VR/MR» не найдена");

            string vrRow = rows[modeRow];
            StringAssert.Contains("VR", vrRow, "в строке режима не назван VR");
            StringAssert.Contains("Виртуальная реальность", vrRow,
                "выбранный VR обязан быть назван полным словом, а не только аббревиатурой");

            data.MixedReality = true;
            List<string> mrRows = KvXrScreenModel.BuildSettings(data);
            StringAssert.Contains("MR", mrRows[modeRow], "в строке режима не назван MR");
            StringAssert.Contains("Смешанная реальность", mrRows[modeRow],
                "выбранный MR обязан быть назван полным словом");
            Assert.AreNotEqual(vrRow, mrRows[modeRow],
                "строки для VR и MR не должны совпадать — иначе выбор не виден оператору");
        }

        /// <summary>
        /// СТРОКА РЕЖИМА ГОВОРИТ, КОГДА ВЫБОР ВСТУПИТ В СИЛУ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. Владелец переключал режим в шлеме и видел, что «ничего не
        /// изменилось». Причина не поломка: сквозной вид и реальные поверхности поднимаются
        /// только при СТАРТЕ приложения (проверено на PICO 4 Ultra). Если строка об этом молчит,
        /// честное поведение выглядит как дефект.
        /// </summary>
        [Test]
        public void SettingsModel_ModeRowNamesRestartTerm()
        {
            var data = new KvXrSettingsData { PanelRadius = 0.11f, MixedReality = true };
            List<string> rows = KvXrScreenModel.BuildSettings(data);

            string modeRow = null;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null && rows[i].Contains("Режим VR/MR")) modeRow = rows[i];

            Assert.IsNotNull(modeRow, "строка «Режим VR/MR» не найдена");
            StringAssert.Contains("запуск", modeRow,
                "строка режима обязана честно сказать, что выбор применится при следующем запуске");
        }

        // ==================================================== б) НАЖАТИЕ МЕНЯЕТ РЕЖИМ

        /// <summary>
        /// НАЖАТИЕ ПУНКТА МЕНЯЕТ РЕЖИМ ТУДА И ОБРАТНО — ЧЕРЕЗ НАСТОЯЩУЮ ЦЕЛЬ ЛУЧА.
        ///
        /// ПОЧЕМУ ЧЕРЕЗ ПАНЕЛЬ, А НЕ ЧЕРЕЗ `KvXrSettings.ToggleMode()` НАПРЯМУЮ. Прямой вызов
        /// проверил бы только хранилище и прошёл бы даже в том случае, когда кнопка есть, а
        /// обработчик к ней не привязан (именно так «Второй луч» когда-то менял дальность
        /// телепорта). Здесь вызывается `PressIndex` — тот же путь, которым нажимает луч.
        /// </summary>
        [Test]
        public void SettingsModeItem_PressTogglesModeBothWays()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);

            GameObject host;
            KvXrGroupPanel panel = NewSettingsPanel(out host);
            try
            {
                int target = OpenModeItemLikeOperator(panel);
                Assert.GreaterOrEqual(target, 0,
                    "на экране «Настройки» нет цели луча «Режим VR/MR» — пункт есть только на бумаге, " +
                    "нажать его в шлеме нечем");

                int before = KvXrSettings.GetModeIndex();
                Assert.AreEqual(KvXrSettings.ModeVr, before, "исходное положение обязано быть VR");

                panel.PressIndex(target);
                Assert.AreEqual(KvXrSettings.ModeMr, KvXrSettings.GetModeIndex(),
                    "нажатие пункта обязано перевести режим в MR");

                int back = OpenModeItemLikeOperator(panel);
                Assert.GreaterOrEqual(back, 0,
                    "после пересборки панели пункт режима пропал — вернуться в VR было бы нечем");

                panel.PressIndex(back);
                Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.GetModeIndex(),
                    "повторное нажатие обязано вернуть режим в VR (переключатель чередует, а не залипает)");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ==================================================== в) ПОДПИСЬ ОТРАЖАЕТ ФАКТ

        /// <summary>
        /// ПОДПИСЬ ПУНКТА ОТРАЖАЕТ ФАКТИЧЕСКИЙ РЕЖИМ: СМЕНИЛИ РЕЖИМ — СМЕНИЛАСЬ ПОДПИСЬ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. Классика переключателя: подпись нарисована один раз при
        /// сборке панели и больше не обновляется. Оператор жмёт, режим в хранилище меняется, а на
        /// кнопке продолжает гореть прежнее положение — и поверить в смену некому.
        /// </summary>
        [Test]
        public void SettingsModeItem_LabelReflectsActualMode()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);

            string vrLabel = KvXrGroupPanel.ModeRowText(KvXrSettings.GetModeIndex());
            StringAssert.Contains("Виртуальная реальность", vrLabel,
                "при выбранном VR подпись обязана называть виртуальную реальность");
            StringAssert.Contains("VR", vrLabel, "подпись обязана содержать аббревиатуру VR");

            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            string mrLabel = KvXrGroupPanel.ModeRowText(KvXrSettings.GetModeIndex());
            StringAssert.Contains("Смешанная реальность", mrLabel,
                "при выбранном MR подпись обязана называть смешанную реальность");
            StringAssert.Contains("MR", mrLabel, "подпись обязана содержать аббревиатуру MR");

            Assert.AreNotEqual(vrLabel, mrLabel,
                "подпись не сменилась вместе с режимом — переключатель врёт про текущее положение");

            // И в собранной панели: после нажатия кнопка обязана называть НОВЫЙ режим, а не прежний.
            GameObject host;
            KvXrGroupPanel panel = NewSettingsPanel(out host);
            try
            {
                int target = OpenModeItemLikeOperator(panel);
                Assert.GreaterOrEqual(target, 0, "на экране «Настройки» нет цели луча «Режим VR/MR»");

                KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
                panel.Rebuild();
                int vrTarget = OpenModeItemLikeOperator(panel);
                Assert.GreaterOrEqual(vrTarget, 0, "пункт режима не найден после пересборки (VR)");
                StringAssert.Contains("Виртуальная реальность", panel.TitleAt(vrTarget),
                    "панель нарисовала подпись режима не по фактическому значению (VR)");

                KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
                panel.Rebuild();
                int mrTarget = OpenModeItemLikeOperator(panel);
                Assert.GreaterOrEqual(mrTarget, 0, "пункт режима не найден после пересборки (MR)");
                StringAssert.Contains("Смешанная реальность", panel.TitleAt(mrTarget),
                    "после смены режима на MR подпись на кнопке осталась прежней — " +
                    "оператор видит не то, что выбрано");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ==================================================== г) ОДНО ХРАНИЛИЩЕ

        /// <summary>
        /// ЗНАЧЕНИЕ, УСТАНОВЛЕННОЕ НА ПК, ЧИТАЕТСЯ В ШЛЕМЕ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. Два интерфейса с двумя хранилищами: на компьютере выбрали
        /// MR, надели шлем — там по-прежнему VR, и оба «правы». Поэтому запись делается ровно тем
        /// путём, которым пишет панель компьютера (`KvXrSettings.SetModeIndex`), и проверяется,
        /// что ШЛЕМ читает именно это значение — и в подписи пункта, и в модели настроек.
        /// </summary>
        [Test]
        public void ModeSetOnDesktop_IsReadInHeadset()
        {
            // Так пишет компьютер: колбэк `c.SetMode` в `KazistovVvUIManager` → `SetModeIndex`.
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);

            Assert.AreEqual(KvXrSettings.ModeMr, KvXrSettings.GetModeIndex(),
                "шлем обязан видеть режим, записанный с компьютера (одно хранилище)");

            StringAssert.Contains("Смешанная реальность",
                KvXrGroupPanel.ModeRowText(KvXrSettings.GetModeIndex()),
                "подпись пункта в шлеме не увидела режим, установленный на компьютере");

            var data = new KvXrSettingsData { MixedReality = KvXrSettings.GetModeIndex() == KvXrSettings.ModeMr };
            string all = string.Join("\n", KvXrScreenModel.BuildSettings(data).ToArray());
            StringAssert.Contains("Смешанная реальность", all,
                "модель настроек шлема не увидела режим, установленный на компьютере");

            // И обратно: выбор в шлеме обязан быть виден ветке MR — тому же значению, что читает ПК.
            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
            Assert.IsFalse(KvXrSettings.GetPassthrough(),
                "выбор VR в шлеме обязан снимать общую настройку MR");
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            Assert.IsTrue(KvXrSettings.GetPassthrough(),
                "выбор MR в шлеме обязан включать ту же настройку, которую читает ветка MR");
        }

        // ==================================================== д) АНТИ-ВАКУУМ

        /// <summary>
        /// ПРОВЕРКИ НЕ ПРОХОДЯТ НА ПУСТОМ СПИСКЕ И НА КНОПКЕ «ЗАКРЫТЬ».
        ///
        /// ЗАЧЕМ. Проверка «в модели есть пункт режима» и «цель луча найдена» может оказаться
        /// зелёной впустую: если модель начнёт отдавать пустой список, а поиск цели будет
        /// находить первую попавшуюся кнопку, дефект «пункта нет» снова станет невидимым.
        /// </summary>
        [Test]
        public void SettingsModeChecks_AreNotVacuous()
        {
            var data = new KvXrSettingsData { PanelRadius = 0.11f };
            List<string> rows = KvXrScreenModel.BuildSettings(data);

            // Модель не пуста и в ней ЕСТЬ ДРУГИЕ пункты, кроме режима: иначе «нашлась строка
            // режима» ничего не доказывает — она была бы единственной.
            Assert.Greater(rows.Count, 5, "модель настроек подозрительно коротка — проверка vacuous");
            string all = string.Join("\n", rows.ToArray());
            StringAssert.Contains("Радиус панели", all);
            StringAssert.Contains("Второй луч", all);
            StringAssert.Contains("Рука луча", all);

            GameObject host;
            KvXrGroupPanel panel = NewSettingsPanel(out host);
            try
            {
                Assert.Greater(panel.RayTargetCount, 3,
                    "на экране «Настройки» слишком мало целей луча — поиск пункта режима vacuous");

                int mode = OpenModeItemLikeOperator(panel);
                int close = IndexOfTarget(panel, "Закрыть");
                int next = IndexOfTarget(panel, "Следующий параметр");

                Assert.GreaterOrEqual(mode, 0, "пункт режима не найден среди целей луча");
                Assert.GreaterOrEqual(close, 0, "кнопка «Закрыть» не найдена — поиск по названию сломан");
                Assert.GreaterOrEqual(next, 0, "кнопка «Дальше» не найдена — поиск по названию сломан");

                Assert.AreNotEqual(close, mode, "пункт режима нашёлся как кнопка «Закрыть» — поиск врёт");
                Assert.AreNotEqual(next, mode, "пункт режима нашёлся как кнопка «Дальше» — поиск врёт");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ==================================================== честность журнала

        /// <summary>
        /// СТРОКА ЖУРНАЛА НАЗЫВАЕТ ОБА ПОЛОЖЕНИЯ ЧИСЛАМИ И ПРИЗНАК «ПРИМЕНЕНО».
        ///
        /// ЗАЧЕМ. Жалоба «переключателя нет / не работает» разбирается по журналу. Строка без
        /// чисел не отличает «нажатие не дошло» от «запись не применилась» — оба случая выглядят
        /// одинаково. Формат закреплён здесь, чтобы он не «поплыл» при следующей правке текста.
        /// </summary>
        [Test]
        public void ModeSwitchLogLine_NamesBothPositionsAndApplied()
        {
            string line = KvXrGroupPanel.ModeSwitchLogLine(KvXrSettings.ModeVr, KvXrSettings.ModeMr, true);

            StringAssert.Contains("РЕЖИМ VR/MR", line, "строка журнала обязана называть пункт");
            StringAssert.Contains("было 0", line, "прежнее положение обязано быть названо числом");
            StringAssert.Contains("стало 1", line, "новое положение обязано быть названо числом");
            StringAssert.Contains("применено да", line, "признак применения обязателен");

            string failed = KvXrGroupPanel.ModeSwitchLogLine(KvXrSettings.ModeMr, KvXrSettings.ModeMr, false);
            StringAssert.Contains("применено нет", failed,
                "неудавшаяся запись обязана быть названа честно, а не как успех");
            Assert.AreNotEqual(line, failed, "успех и неудача не должны выглядеть одинаково");
        }

        /// <summary>
        /// ЧЕСТНАЯ ПОДПИСЬ ПРО ПЕРЕЗАПУСК ГОВОРИТ О ПЕРЕЗАПУСКЕ И НАЗЫВАЕТ ТЕКУЩИЙ РЕЖИМ.
        ///
        /// ЗАЧЕМ. Переключение режима НА ЛЕТУ не поднимает MR-конвейер: сквозной вид и реальные
        /// поверхности включаются только при старте (проверено на PICO 4 Ultra). Значит после
        /// переключения оператор обязан прочитать это в интерфейсе, а не узнать от разработчика.
        /// </summary>
        [Test]
        public void ModeRestartNote_IsHonestAndNamesCurrentMode()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            string note = KvXrGroupPanel.ModeRestartNote();

            StringAssert.Contains("перезапуск", note,
                "подпись обязана прямо сказать про перезапуск — иначе «нажал и ничего» выглядит поломкой");
            StringAssert.Contains("MR", note, "подпись обязана назвать текущий режим (MR)");
        }
    }
}
