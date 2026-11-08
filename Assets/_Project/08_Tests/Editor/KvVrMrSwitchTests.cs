// ================================================================================================
//  KvVrMrSwitchTests.cs — EditMode-тесты ПЕРЕКЛЮЧАТЕЛЯ VR / MR (11.11.2026).
//
//  ОТКУДА ЗАДАЧА. Владелец: «надо добавить переключатель VR/MR в меню "настройки"».
//  До этой правки режим включался ГОЛОЙ ГАЛКОЙ «Режим комнаты (MR): ВКЛ/выкл», и только
//  в шлеме. У галки два состояния, но НИ ОДНО НЕ НАЗВАНО: оператор видел «выкл» и не мог
//  отличить «выбран VR» от «ничего не выбрано». На компьютере переключателя не было вовсе —
//  подготовить режим заранее было негде.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) ЧИСТЫЕ ФУНКЦИИ ПЕРЕКЛЮЧАТЕЛЯ: два положения, оба названы; мусор в значении приводится
//       к VR, а не падает; «следующее положение» действительно чередует, а не залипает.
//    2) ОДНО ХРАНИЛИЩЕ: переключатель пишет ТО ЖЕ значение, что читает вся ветка MR
//       (`GetPassthrough`). Второе хранилище означало бы два расходящихся режима — ровно этот
//       класс дефекта уже был в S44 (`SetPassthrough` не вызывался ниоткуда, ветка MR мертва).
//    3) ЗАПИСЬ ПЕРЕЖИВАЕТ ПЕРЕЗАПУСК: `SetModeIndex` обязан сохранять PlayerPrefs, иначе
//       подпись «применится после перезапуска» — ложь, и выбор теряется при выходе.
//    4) ОБА ИНТЕРФЕЙСА ПИШУТ ОДНУ НАСТРОЙКУ: ПК-панель `KvSettingsView` и экран шлема
//       `KvXrGroupPanel` пользуются `KvXrSettings`, а не своими полями.
//    5) ЧЕСТНАЯ СТРОКА СОСТОЯНИЯ: отдельно назван случай «шлем не умеет MR» — включённый
//       переключатель и серый экран не должны выглядеть одинаково.
//
//  СТИЛЬ — КАК В ПРОЕКТЕ: где сцену не запустить, проверяются ИСХОДНИК и ЧИСТЫЕ функции.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Переключатель VR/MR: положения, единое хранилище, два интерфейса, честная строка.</summary>
        [Category(KvTestCategories.Fast)]
    public class KvVrMrSwitchTests
    {
        private int savedPassthrough;
        private bool hadSavedPassthrough;

        // ---------------------------------------------------------------- сохранение окружения
        //
        // ТЕСТ НЕ ИМЕЕТ ПРАВА МЕНЯТЬ НАСТРОЙКИ МАШИНЫ. Переключатель живёт в PlayerPrefs — то есть
        // в общих настройках разработчика. Без сохранения/восстановления прогон тестов оставлял бы
        // владельцу включённый MR, и следующий запуск шлема стартовал бы не в VR.

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

        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        private static string ReadProjectFile(string relativePath)
        {
            string path = Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "не найден исходник: " + path);
            return File.ReadAllText(path);
        }

        private static string ReadXr(string fileName)
        {
            return ReadProjectFile("Assets/_Project/01_Scripts/Xr/" + fileName);
        }

        // ==================================================== 1. ЧИСТЫЕ ФУНКЦИИ

        /// <summary>
        /// У ПЕРЕКЛЮЧАТЕЛЯ РОВНО ДВА ПОЛОЖЕНИЯ, И ОБА НАЗВАНЫ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. Галка «ВКЛ/выкл» называла только одно состояние; «выкл»
        /// не значило «VR» ни для оператора, ни для того, кто читал журнал.
        /// </summary>
        [Test]
        public void Switch_HasTwoNamedPositions()
        {
            Assert.AreEqual(2, KvXrSettings.ModeCount, "у переключателя режима обязано быть два положения");
            Assert.AreEqual(0, KvXrSettings.ModeVr, "VR — первое положение (запуск по умолчанию)");
            Assert.AreEqual(1, KvXrSettings.ModeMr, "MR — второе положение");

            string vr = KvXrSettings.ModeLabel(KvXrSettings.ModeVr);
            string mr = KvXrSettings.ModeLabel(KvXrSettings.ModeMr);

            StringAssert.Contains("VR", vr, "положение VR обязано быть названо своей аббревиатурой");
            StringAssert.Contains("MR", mr, "положение MR обязано быть названо своей аббревиатурой");
            Assert.AreNotEqual(vr, mr, "положения не должны называться одинаково — иначе выбор неразличим");

            Assert.AreEqual("VR", KvXrSettings.ModeShortLabel(KvXrSettings.ModeVr));
            Assert.AreEqual("MR", KvXrSettings.ModeShortLabel(KvXrSettings.ModeMr));
        }

        /// <summary>
        /// МУСОР В ЗНАЧЕНИИ ПРИВОДИТСЯ К VR, А НЕ ПАДАЕТ И НЕ ОСТАВЛЯЕТ РЕЖИМ БЕЗЫМЯННЫМ.
        ///
        /// ПРАВИЛО ПРОЕКТА (§NaN/§storage): испорченное значение в хранилище не должно ронять
        /// приложение и не должно оставлять его в состоянии, которое нельзя назвать.
        /// </summary>
        [Test]
        public void Switch_ClampsGarbageToVr()
        {
            Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.ModeFromIndex(-5));
            Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.ModeFromIndex(2));
            Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.ModeFromIndex(999));
            Assert.AreEqual(KvXrSettings.ModeMr, KvXrSettings.ModeFromIndex(KvXrSettings.ModeMr));
        }

        /// <summary>
        /// «СЛЕДУЮЩЕЕ ПОЛОЖЕНИЕ» ЧЕРЕДУЕТ, А НЕ ЗАЛИПАЕТ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. Классическая ошибка переключателя — вернуть то же самое
        /// положение (сравнение с самим собой), и кнопка «щёлкает», ничего не меняя.
        /// </summary>
        [Test]
        public void Switch_ToggleAlternates()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);

            int first = KvXrSettings.ToggleMode();
            Assert.AreEqual(KvXrSettings.ModeMr, first, "из VR переключатель обязан уйти в MR");
            Assert.AreEqual(KvXrSettings.ModeMr, KvXrSettings.GetModeIndex(),
                "новое положение обязано СРАЗУ читаться обратно");

            int second = KvXrSettings.ToggleMode();
            Assert.AreEqual(KvXrSettings.ModeVr, second, "из MR переключатель обязан вернуться в VR");
            Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.GetModeIndex());

            int third = KvXrSettings.ToggleMode();
            Assert.AreEqual(KvXrSettings.ModeMr, third, "третье переключение снова даёт MR");
        }

        // ==================================================== 2. ОДНО ХРАНИЛИЩЕ

        /// <summary>
        /// ПО УМОЛЧАНИЮ — VR (решение владельца 04.11.2026), и это же положение переключателя.
        ///
        /// Проверяется в паре: константа «по умолчанию MR выключен» И чтение переключателя на
        /// чистом хранилище. Если однажды поменяют одно и забудут другое, тест покраснеет.
        /// </summary>
        [Test]
        public void Switch_DefaultsToVr()
        {
            Assert.AreEqual(0, KvXrSettings.DefaultPassthrough,
                "запуск по умолчанию — VR: MR экспериментальная ветка и включается осознанно");

            KvXrSettings.ResetMode();
            Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.GetModeIndex(),
                "на чистом хранилище переключатель обязан показывать VR");
        }

        /// <summary>
        /// ПЕРЕКЛЮЧАТЕЛЬ И ВЕТКА MR ЧИТАЮТ ОДНО И ТО ЖЕ ЗНАЧЕНИЕ.
        ///
        /// Это главный тест блока. Если переключатель заведёт СВОЙ ключ PlayerPrefs, оба будут
        /// правдоподобны и разойдутся: панель покажет «MR», а `MrActive` продолжит считать, что
        /// MR выключен. Ровно это и было в S44 в другой форме — настройка, которую никто не пишет.
        /// </summary>
        [Test]
        public void Switch_WritesTheSameSettingAsMrBranch()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            Assert.IsTrue(KvXrSettings.GetPassthrough(),
                "положение MR обязано быть видно ветке MR через GetPassthrough()");

            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
            Assert.IsFalse(KvXrSettings.GetPassthrough(),
                "положение VR обязано снимать настройку MR");

            // И обратно: запись старым путём обязана отражаться в переключателе — иначе две
            // панели разойдутся ровно в одну сторону (шлем пишет, ПК не видит).
            KvXrSettings.SetPassthrough(true);
            Assert.AreEqual(KvXrSettings.ModeMr, KvXrSettings.GetModeIndex(),
                "переключатель обязан видеть настройку, записанную через SetPassthrough");
        }

        /// <summary>
        /// ВЫБОР ХРАНИТСЯ В PLAYERPREFS: подпись «применится после перезапуска» обязана быть правдой.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. `SetPassthrough` пишет значение только в память PlayerPrefs;
        /// без `PlayerPrefs.Save()` выбор теряется, если приложение закрыли принудительно
        /// (а в шлеме это обычный способ выхода — снял шлем и снял приложение).
        /// </summary>
        [Test]
        public void Switch_PersistsToPlayerPrefs()
        {
            string source = ReadXr("KvXrSettings.cs");
            int index = source.IndexOf("public static void SetModeIndex", System.StringComparison.Ordinal);
            Assert.Greater(index, 0, "в KvXrSettings нет SetModeIndex");

            int end = source.IndexOf('}', index);
            string body = source.Substring(index, end - index);

            StringAssert.Contains("PlayerPrefs.Save", body,
                "выбор режима обязан сохраняться на диск, иначе теряется при принудительном закрытии");
            StringAssert.Contains("SetPassthrough", body,
                "переключатель обязан писать через SetPassthrough — одно хранилище, а не второе");
        }

        // ==================================================== 3. ДВА ИНТЕРФЕЙСА

        /// <summary>
        /// ЭКРАН ШЛЕМА ПОКАЗЫВАЕТ РЕЖИМ ПОСЛЕДНИМ ПАРАМЕТРОМ И ПЕРЕКЛЮЧАЕТ ЕГО ОБРАБОТЧИКОМ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. Панель привязывает обработчик по ИНДЕКСУ строки. Дефект
        /// 23.10.2026 был именно такой: переключатель стоял не у той строки, и «Второй луч»
        /// менял дальность телепорта. Поэтому проверяется и место в модели, и явный список
        /// индексов в обработчике.
        /// </summary>
        [Test]
        public void XrPanel_ShowsAndTogglesMode()
        {
            var data = new KvXrSettingsData { PanelRadius = 0.11f, MixedReality = true };
            List<string> rows = KvXrScreenModel.BuildSettings(data);

            int last = rows.Count - 1;
            StringAssert.Contains("Режим VR/MR", rows[last],
                "режим обязан стоять последним параметром — вставка в середину сдвинула бы шаги 0…3");
            StringAssert.Contains("MR", rows[last], "выбранный MR обязан быть виден в строке");

            data.MixedReality = false;
            List<string> vrRows = KvXrScreenModel.BuildSettings(data);
            StringAssert.Contains("VR", vrRows[vrRows.Count - 1], "выбранный VR обязан быть виден в строке");

            string panel = ReadXr("KvXrGroupPanel.cs");
            StringAssert.Contains("case 6: ToggleMixedReality()", panel,
                "переключатель в шлеме не привязан к строке 6 — кнопка будет менять не то");
            StringAssert.Contains("KvXrSettings.ToggleMode()", panel,
                "переключение обязано идти через единую логику, а не через !GetPassthrough() на месте");
            StringAssert.Contains("MixedReality =", panel,
                "экран шлема не заполняет выбранный режим — строка будет всегда показывать одно");
        }

        /// <summary>
        /// ПАНЕЛЬ КОМПЬЮТЕРА: переключатель есть, положения названы, запись идёт в ТО ЖЕ хранилище.
        ///
        /// Проверяется исходник панели и менеджера: колбэки `GetMode`/`SetMode` не должны
        /// остаться объявленными и неподключёнными — тогда переключатель на ПК был бы картинкой.
        /// </summary>
        [Test]
        public void DesktopPanel_HasModeSwitchWiredToSharedSetting()
        {
            string view = ReadProjectFile("Assets/_Project/06_KazistovVv_UI/03_Scripts/Zones/KvSettingsView.cs");
            StringAssert.Contains("GetMode", view, "в KvSettingsView нет чтения режима — переключателю нечего показать");
            StringAssert.Contains("SetMode", view, "в KvSettingsView нет записи режима — переключатель не работает");
            StringAssert.Contains("KvXrSettings.ModeLabel", view,
                "подписи положений обязаны браться из общего места, а не дублироваться строками");
            StringAssert.Contains("Режим приложения (VR / MR)", view,
                "в настройках компьютера нет раздела режима");

            string manager = ReadProjectFile(
                "Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs");
            StringAssert.Contains("c.SetMode =", manager,
                "колбэк смены режима объявлен, но не подключён в менеджере — кнопка на ПК мертва");
            StringAssert.Contains("c.GetMode =", manager,
                "колбэк чтения режима объявлен, но не подключён — панель покажет VR при включённом MR");
            StringAssert.Contains("KvXrSettings.SetModeIndex", manager,
                "менеджер обязан писать ОБЩЕЕ хранилище, а не своё значение");
        }

        // ==================================================== 4. ЧЕСТНАЯ СТРОКА

        /// <summary>
        /// СТРОКА СОСТОЯНИЯ НАЗЫВАЕТ ВЫБОР, ПОСЛЕДСТВИЕ И СРОК.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. «Включил MR, а ничего не изменилось» — два РАЗНЫХ случая:
        /// (а) выбор применится после перезапуска (так и задумано) и (б) шлем не умеет сквозной
        /// вид. Пока строка одна на оба случая, оператор не может отличить замысел от поломки.
        /// </summary>
        [Test]
        public void StatusLine_DistinguishesSupportedAndUnsupported()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            string supported = KvXrSettings.ModeStatusLine(true);
            string unsupported = KvXrSettings.ModeStatusLine(false);

            StringAssert.Contains("СМЕШАННАЯ", supported);
            StringAssert.Contains("перезапуск", supported,
                "строка обязана прямо говорить, что режим применится после перезапуска");
            StringAssert.Contains("VR", unsupported,
                "если шлем не умеет MR, строка обязана честно сказать, что приложение останется в VR");
            Assert.AreNotEqual(supported, unsupported,
                "случаи «применится» и «шлем не дал» обязаны различаться текстом");

            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
            StringAssert.Contains("ВИРТУАЛЬНАЯ", KvXrSettings.ModeStatusLine(true));
            StringAssert.Contains("ВИРТУАЛЬНАЯ", KvXrSettings.ModeStatusLine(false));
        }

        /// <summary>
        /// ЧТЕНИЕ РЕЖИМА НЕ ПИШЕТ В ХРАНИЛИЩЕ.
        ///
        /// ЗАЧЕМ. Геттеры зовутся из отрисовки панели каждый кадр и из EditMode-тестов. Если
        /// геттер начнёт «исправлять» значение записью, прогон тестов изменит настройки машины,
        /// а отрисовка получит побочный эффект (правило проекта: чтение не пишет).
        /// </summary>
        [Test]
        public void ReadingModeDoesNotWrite()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
            Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.GetModeIndex());
            Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.GetModeIndex());
            Assert.IsFalse(KvXrSettings.GetPassthrough(),
                "чтение режима не должно включать MR как побочный эффект");
        }
    }
}

