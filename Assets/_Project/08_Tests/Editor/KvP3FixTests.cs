using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ ИСПРАВЛЕНИЙ P3 (11.11.2026) — «ЛОЖНЫЕ ОБЕЩАНИЯ В ИНТЕРФЕЙСЕ».
    ///
    /// ПРАВИЛО ФАЙЛА (то же, что в `KvP1FixTests`): каждый тест обязан ПАДАТЬ на старом коде.
    /// Где поведение оторвать от сцены можно — проверяется ПОВЕДЕНИЕ (конвертер устройств, размер
    /// кнопки, доступность провайдеров). Где нельзя (приватные методы менеджера интерфейса, луч по
    /// плоскостям AR) — проверяется ПРАВИЛО по исходнику, и ОБЯЗАТЕЛЬНО с невакуумной страховкой:
    /// длина файла, число найденных перехватов, найденный фрагмент метода. Иначе переименование или
    /// пустой файл сделали бы проверку ложно зелёной (урок P4-2).
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: `Assert.Pass`, `Assert.Ignore`, `[Ignore]` — такой тест «зеленеет», ничего не
    /// проверив, и гейт по `skipped` его не видит (урок E-16).
    /// </summary>
        [Category(KvTestCategories.Fast)]
    public class KvP3FixTests
    {
        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        private static string ReadScript(string relativePath)
        {
            string path = Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "нет файла " + path);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// УБРАТЬ КОММЕНТАРИИ: там, где проверяется правило по тексту, объяснение «что было» с
        /// упоминанием старого кода не должно считаться самим кодом (поймано на прогоне 11.11.2026).
        /// </summary>
        private static string StripComments(string source)
        {
            string withoutBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty,
                RegexOptions.Singleline);
            return Regex.Replace(withoutBlock, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }

        private static string UiManagerPath
        {
            get { return "Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs"; }
        }

        /// <summary>
        /// ВЫРЕЗАТЬ ТЕЛО МЕТОДА по сигнатуре: от строки объявления до закрывающей скобки на том же
        /// уровне вложенности. Нужно, чтобы проверять порядок вызовов ВНУТРИ метода, а не по файлу.
        /// Возвращает null, если метод не найден (тогда тест обязан упасть, а не «пройти пусто»).
        /// </summary>
        private static string ExtractMethod(string code, string signature)
        {
            int at = code.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return null;

            int open = code.IndexOf('{', at + signature.Length);
            if (open < 0) return null;

            int depth = 1;
            int i = open + 1;
            while (i < code.Length && depth > 0)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}') depth--;
                i++;
            }
            return code.Substring(at, i - at);
        }

        /// <summary>
        /// ЧИСЛО ПУСТЫХ ПЕРЕХВАТОВ (`catch (…)` без единого оператора в теле).
        /// Тот же разбор, что в `KvEmptyCatchPolicyTests`, но здесь он нужен как проверка
        /// конкретных файлов оболочки (P3-4).
        /// </summary>
        private static int CountEmptyCatch(string text)
        {
            int empty = 0;
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
                if (body.Trim().Length == 0) empty++;
            }
            return empty;
        }

        // ============================================================ P3-1

        /// <summary>
        /// P3-1: НЕИЗВЕСТНЫЙ id БОЛЬШЕ НЕ ОТДАЁТ СОСТОЯНИЕ «ФАНТОМЫ», а объявленные мёртвыми id
        /// убраны из `HasFeature`.
        ///
        /// ЧТО БЫЛО. `default: return KvSettings.ShowPhantoms;` обслуживал ЛЮБОЙ неизвестный id, а
        /// `HasFeature` объявлял существующими `ui.toolbar.layout` и `ui.toolbar.compact`, которых
        /// нет ни в `GetFeature`, ни в `ApplyVisualization`. Тумблер показывал чужое состояние, а
        /// нажатие уходило в «нет обработчика».
        ///
        /// На старом коде падали все три утверждения: `default` был с фантомами, оба мёртвых id
        /// были в списке, и своего `case` для «Фантомов» не существовало (их обслуживал default).
        /// </summary>
        [Test]
        public void P3_1_GetFeature_DoesNotBorrowPhantomsState()
        {
            string code = StripComments(ReadScript(UiManagerPath));
            Assert.Greater(code.Length, 50000, "файл менеджера подозрительно мал — проверка потеряла смысл");

            StringAssert.DoesNotContain("default: return KvSettings.ShowPhantoms", code,
                "неизвестный id снова получает состояние «Фантомы» (P3-1): тумблер показывает чужое состояние");

            StringAssert.DoesNotContain("case \"ui.toolbar.layout\"", code,
                "мёртвый id «ui.toolbar.layout» вернулся в HasFeature — обработчика у него нет (P3-1)");
            StringAssert.DoesNotContain("case \"ui.toolbar.compact\"", code,
                "мёртвый id «ui.toolbar.compact» вернулся в HasFeature — обработчика у него нет (P3-1)");

            // ОБРАТНАЯ СТОРОНА ТОЙ ЖЕ ПРАВКИ, без неё исправление стало бы новой ошибкой:
            // «Фантомы» обязаны иметь СВОЙ case, иначе после смены default тумблер залипнет выключенным.
            StringAssert.Contains("case \"scene.phantoms\": return KvSettings.ShowPhantoms;", code,
                "у «Фантомов» нет собственного case — после смены default тумблер залипнет выключенным");

            // И обработчик gamepadhud обязан ПИСАТЬ значение, а не стоять пустым break (P3-1).
            Assert.Greater(Regex.Matches(code, @"KvSettings\.GamepadHud\s*=\s*value;").Count, 0,
                "переключатель «Виртуальный геймпад» не записывает значение — «нажал и ничего» (P3-1)");
        }

        // ============================================================ P3-2

        /// <summary>
        /// P3-2: ПОДТВЕРЖДЕНИЕ КОПИРОВАНИЯ ТОЛЬКО ПО ФАКТУ.
        ///
        /// ЧТО БЫЛО. Запись в буфер в однострочном `try` с пустым перехватом, а строкой ниже —
        /// безусловное «Имя скопировано в буфер».
        /// На старом коде падало первое же утверждение: метод возвращал `void`, признака успеха
        /// не существовало, и буфер не читался обратно.
        /// </summary>
        [Test]
        public void P3_2_CopyNodeName_ConfirmsOnlyRealSuccess()
        {
            string code = StripComments(ReadScript(UiManagerPath));
            Assert.Greater(code.Length, 50000, "файл менеджера подозрительно мал — проверка потеряла смысл");

            StringAssert.Contains("public bool CopyNodeName(", code,
                "копирование имени не возвращает признак успеха — подтверждение снова безусловно (P3-2)");

            // ЧТЕНИЕ ОБРАТНО: именно оно отличает «исключения не было» от «значение записалось».
            Assert.Greater(Regex.Matches(code, @"GUIUtility\.systemCopyBuffer").Count, 1,
                "буфер записывается, но не читается обратно — успех не проверяется (P3-2)");

            StringAssert.Contains("ctx.copyfailed", code,
                "нет ветки сообщения об ОШИБКЕ копирования — остаётся только зелёное подтверждение (P3-2)");

            // Ошибка показывается предупреждающим цветом, а не зелёным «всё хорошо».
            StringAssert.Contains("KvTheme.Warn", code,
                "сообщение об отказе копирования не отличается от успеха по цвету (P3-2)");
        }

        // ============================================================ P3-3 / P3-4

        /// <summary>
        /// P3-3: ОТКАЗ УСТАНОВКИ КУРСОРА БОЛЬШЕ НЕ ГЛОТАЕТСЯ.
        ///
        /// ЧТО БЫЛО. `try { Cursor.lockState = …; Cursor.visible = …; } catch (Exception) { }`:
        /// при исключении состояние курсора расходилось с моделью, `ReapplyCount` не считался и в
        /// журнал не попадало ничего.
        /// На старом коде в файле было 5 пустых перехватов и НИ ОДНОГО упоминания причины отказа.
        /// </summary>
        [Test]
        public void P3_3_MouseCursor_ReportsFailedCursorWrite()
        {
            string raw = ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KvMouseCursor.cs");
            Assert.Greater(raw.Length, 15000, "файл курсора подозрительно мал — проверка потеряла смысл");

            string code = StripComments(raw);
            int catches = Regex.Matches(code, @"catch").Count;
            Assert.Greater(catches, 3,
                "в файле найдено " + catches + " перехватов — разбор ничего не проверил (страховка от вакуума)");

            Assert.AreEqual(0, CountEmptyCatch(code),
                "в KvMouseCursor снова есть ПУСТОЙ перехват — отказ установки курсора не диагностируется (P3-3)");

            Assert.GreaterOrEqual(Regex.Matches(code, @"ReapplyCount\+\+").Count, 4,
                "счётчик переприменений не растёт при отказе — «мышь не восстанавливается» снова невидима (P3-3)");

            StringAssert.Contains("Debug.LogWarning", code,
                "отказ установки курсора не пишется в журнал (P3-3)");
        }

        /// <summary>
        /// P3-4: В ОБОЛОЧКЕ НЕ ОСТАЛОСЬ НЕОБЪЯСНЁННЫХ ПУСТЫХ ПЕРЕХВАТОВ.
        ///
        /// ЧТО БЫЛО. В `KazistovVvUIManager` четыре места перехватывали отказ ПУСТЫМ блоком, и
        /// каждое работает в кадре: горячие клавиши, выбор типа робота, поворот при постановке,
        /// копирование имени.
        /// На старом коде их было 4, и предупреждения о недоступном legacy-вводе не существовало.
        /// </summary>
        [Test]
        public void P3_4_UiManager_HasNoSilentCatches()
        {
            string code = StripComments(ReadScript(UiManagerPath));
            StringAssert.Contains("WarnLegacyInputOnce", code,
                "нет единого места предупреждения об отказе старой системы ввода (P3-4)");

            Assert.AreEqual(0, CountEmptyCatch(code),
                "в KazistovVvUIManager снова есть ПУСТОЙ перехват — отказ ввода проглатывается молча (P3-4)");
        }

        // ============================================================ P3-5

        /// <summary>
        /// P3-5: CSV-ОТЧЁТ МЕТРИК БОЛЬШЕ НЕ ОБЕЩАН.
        ///
        /// ЧТО БЫЛО. Поле `rows` не пополнялось ни одним `rows.Add(...)`, `SaveCsv()` всегда выходил
        /// на `if (rows.Count == 0) return;`, а `TrajectoryPlannerController.RunSelfTest` вызывал его
        /// как будто отчёт записан.
        /// На старом коде падали оба утверждения: метка устаревания отсутствовала, а вызов
        /// `SaveCsv()` из автотеста существовал.
        /// </summary>
        [Test]
        public void P3_5_PlanMetrics_DoesNotPromiseCsv()
        {
            string metrics = StripComments(ReadScript("Assets/_Project/01_Scripts/Trajectory/PlanMetrics.cs"));
            Assert.Greater(metrics.Length, 2000, "файл метрик подозрительно мал — проверка потеряла смысл");

            StringAssert.Contains("[System.Obsolete(", metrics,
                "SaveCsv не помечен устаревшим — он снова обещает файл, которого не будет (P3-5)");
            Assert.AreEqual(0, Regex.Matches(metrics, @"rows\.Add\(").Count,
                "поле rows начало пополняться — тогда метку устаревания пора снимать, а не держать (P3-5)");

            string planner = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Trajectory/TrajectoryPlannerController.cs"));
            Assert.Greater(planner.Length, 2000, "файл планировщика подозрительно мал");
            StringAssert.DoesNotContain("SaveCsv()", planner,
                "автотест снова рапортует «метрики сохранены» вызовом SaveCsv, который ничего не пишет (P3-5)");
        }

        // ============================================================ P3-6

        /// <summary>
        /// P3-6: `TopUpVariants` ЧЕСТНО ПОМЕЧЕН, А НЕ ВЫГЛЯДИТ РАБОТАЮЩИМ.
        ///
        /// ЧТО БЫЛО. Метод не вызывался ниоткуда (внутри — настоящая IK-доводка), и «добивка
        /// фантомов до 8» оставалась заявленной, но не работающей.
        /// На старом коде метки устаревания не было.
        /// </summary>
        [Test]
        public void P3_6_TopUpVariants_IsMarkedObsolete()
        {
            string code = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Trajectory/TrajectoryFlowController.cs"));
            Assert.Greater(code.Length, 50000, "файл потока подозрительно мал — проверка потеряла смысл");

            Assert.AreEqual(1, Regex.Matches(code, @"TopUpVariants\s*\(").Count,
                "TopUpVariants либо исчез, либо у него появились вызовы — тогда решение надо пересмотреть (P3-6)");

            // Между атрибутом и объявлением: именно это и означает «помечен, а не удалён молча».
            Match m = Regex.Match(code, @"\[System\.Obsolete\(\s*""([^""]{40,})""[^\]]*\)\]\s*private\s+List<IkSolution>\s+TopUpVariants");
            Assert.IsTrue(m.Success,
                "TopUpVariants не помечен [Obsolete] с внятной причиной — мёртвая добивка снова выглядит рабочей (P3-6)");
        }

        // ============================================================ P3-7

        /// <summary>
        /// P3-7: МЁРТВОЕ ПОЛЕ, МЁРТВАЯ ВЕТКА И ОБХОД СЦЕНЫ РАДИ НЕСУЩЕСТВУЮЩЕГО ТИПА УБРАНЫ.
        ///
        /// ЧТО БЫЛО. `FreeFlyCameraController` содержал никогда не присваиваемое поле
        /// `plannerController` и недостижимую ветку под `#pragma warning disable 0618`;
        /// `KvXrGroupPanel.PlanningHasCandidates()` делал `FindAnyObjectByType` этого типа (полный
        /// обход сцены) и всегда получал null.
        /// На старом коде падали все пять утверждений.
        /// </summary>
        [Test]
        public void P3_7_DeadPlannerWiringIsGone()
        {
            string freeFly = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Core/FreeFlyCameraController.cs"));
            Assert.Greater(freeFly.Length, 40000, "файл камеры подозрительно мал — проверка потеряла смысл");

            StringAssert.DoesNotContain("plannerController", freeFly,
                "мёртвое поле plannerController вернулось в камеру (P3-7)");
            StringAssert.DoesNotContain("#pragma warning disable 0618", freeFly,
                "вернулась директива, глушащая предупреждение о мёртвом типе (P3-7)");

            string xrPanel = StripComments(ReadScript("Assets/_Project/01_Scripts/Xr/KvXrGroupPanel.cs"));
            Assert.Greater(xrPanel.Length, 40000, "файл панели шлема подозрительно мал");
            StringAssert.DoesNotContain("PlanningHasCandidates", xrPanel,
                "обход сцены ради несуществующего планировщика вернулся (P3-7)");
            StringAssert.DoesNotContain("FindAnyObjectByType<TrajectoryPlannerController>", xrPanel,
                "полный обход сцены ради мёртвого типа вернулся (P3-7)");

            // САМ КЛАСС ОСТАВЛЕН И ПОМЕЧЕН: удаление — отдельное решение владельца.
            string planner = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Trajectory/TrajectoryPlannerController.cs"));
            StringAssert.Contains("class TrajectoryPlannerController", planner,
                "класс удалён без решения владельца — правило проекта: сначала метка, удаление отдельно (P3-7)");
            StringAssert.Contains("[System.Obsolete(", planner,
                "класс снова выглядит живым, хотя его роль выполняет TrajectoryFlowController (P3-7)");
        }

        // ============================================================ P3-8

        /// <summary>
        /// P3-8: ЭТАЛОН РАЗМЕРА КНОПКИ-ИКОНКИ ОДИН, И КОММЕНТАРИЙ О ТЕКСТЕ — ПО ФАКТУ.
        ///
        /// ЧТО БЫЛО. `KvWidgets.IconButtonSize = 24f` против `KvSettings.IconButtonSize` (24/28 по
        /// плотности): самопроверка сверяла константу 24 и рапортовала «провалов 0» при реальных
        /// 28 px; в `KvTheme` стояло «0,45 / в 2,2 раза» при фактическом `DefaultTextScale = 0.55f`
        /// (это 1,8 раза).
        /// Поведенческая часть (равенство значений) на старом коде падала при плотности 1.
        /// </summary>
        [Test]
        public void P3_8_IconSizeHasSingleSource()
        {
            string widgets = StripComments(
                ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KvWidgets.cs"));
            Assert.Greater(widgets.Length, 20000, "файл виджетов подозрительно мал");

            StringAssert.DoesNotContain("const float IconButtonSize", widgets,
                "в KvWidgets снова СВОЯ константа размера кнопки — второй эталон вернулся (P3-8)");
            StringAssert.Contains("KvSettings.IconButtonSize", widgets,
                "KvWidgets больше не берёт размер из единственного источника (P3-8)");

            // ПОВЕДЕНИЕ: значение виджета обязано совпадать с настройкой при любой плотности.
            Assert.AreEqual(KvSettings.IconButtonSize, KvWidgets.IconButtonSize, 1e-4f,
                "размер кнопки-иконки у виджетов расходится с KvSettings — тулбар и дерево разъедутся (P3-8)");
            Assert.That(KvSettings.IconButtonSize, Is.InRange(24f, 28f),
                "размер кнопки-иконки вышел из эталона 24…28 (P3-8)");

            // ВНИМАНИЕ: здесь комментарии НЕ вырезаются — исправлялся именно ТЕКСТ комментария
            // (P3-8), и вырезав его, проверка стала бы вакуумной. Поэтому сверяется исходник как есть.
            string theme = ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KvTheme.cs");
            Assert.Greater(theme.Length, 20000, "файл темы подозрительно мал");
            StringAssert.DoesNotContain("2,2 раза", theme,
                "комментарий снова обещает уменьшение «в 2,2 раза» при коэффициенте 0.55 (P3-8)");
            StringAssert.Contains("1,8 раза", theme,
                "в комментарии не назван фактический коэффициент уменьшения текста (P3-8)");
            StringAssert.Contains("DefaultTextScale = 0.55f", theme,
                "значение коэффициента текста изменилось — комментарий про 1,8 раза надо пересчитать (P3-8)");
        }

        // ============================================================ P3-9

        /// <summary>
        /// P3-9: ОДИН enum И ЖИВЫЕ КОНВЕРТЕРЫ.
        ///
        /// ЧТО БЫЛО. `InputDeviceType` и `InputManager.InputDevice` держались на совпадении чисел,
        /// конвертеры не вызывались ни разу, а `SettingsMenu` кастовал значение списка напрямую.
        ///
        /// Проверяется ПОВЕДЕНИЕ: туда-обратно через конвертер обязан вернуться тот же элемент —
        /// на любом значении перечисления. На старом коде это тоже проходило (каст работал, пока
        /// числа совпадали), поэтому рядом стоят проверки ПРАВИЛА по исходнику: значения обязаны
        /// браться из `InputManager.InputDevice`, соответствие — через switch, а сырого каста в
        /// меню настроек быть не должно.
        /// </summary>
        [Test]
        public void P3_9_InputDeviceConversionIsExplicit()
        {
            int checkedValues = 0;
            foreach (InputDeviceType type in Enum.GetValues(typeof(InputDeviceType)))
            {
                // Конвертеры вызываются ЯВНО через класс расширений: проверяется именно их
                // соответствие, а не то, нашёл ли компилятор метод расширения.
                InputManager.InputDevice device = InputDeviceExtensions.ToInputManagerDevice(type);
                Assert.AreEqual(type, InputDeviceExtensions.ToInputDeviceType(device),
                    "конвертер устройств ввода не возвращает исходное значение для " + type + " (P3-9)");
                checkedValues++;
            }
            Assert.Greater(checkedValues, 4,
                "проверено " + checkedValues + " значений — перечисление усохло, проверка потеряла смысл");

            string deviceType = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Input/InputDeviceType.cs"));
            StringAssert.Contains("(int)InputManager.InputDevice.", deviceType,
                "значения InputDeviceType снова заданы числами вручную, а не взяты из единственного enum (P3-9)");
            StringAssert.Contains("switch (type)", deviceType,
                "прямое приведение вернулось вместо явного соответствия по именам (P3-9)");

            string menu = StripComments(ReadScript("Assets/_Project/01_Scripts/UI/SettingsMenu.cs"));
            StringAssert.DoesNotContain("(InputManager.InputDevice)v", menu,
                "в меню настроек вернулся сырой каст значения списка в устройство ввода (P3-9)");
            StringAssert.Contains("ToInputManagerDevice()", menu,
                "меню настроек не пользуется конвертером — соответствие снова держится на числах (P3-9)");
        }

        // ============================================================ P3-10

        /// <summary>
        /// P3-10: НЕРАБОЧИЙ ПРОВАЙДЕР НЕ ВЫБИРАЕТСЯ, ССЫЛКА СБРАСЫВАЕТСЯ, AR ПРОВЕРЯЕТ ПОДСИСТЕМУ.
        ///
        /// ЧТО БЫЛО. `SetInputDevice(MR)` выставлял провайдер, у которого `IsAvailable() == false`;
        /// `Instance` не обнулялся в `OnDestroy`; `ARInputProvider.IsAvailable()` = «платформа
        /// Android» (то есть true и на шлеме PICO), а `ARRaycastFromTouch` — заглушка, на которой
        /// целиком построен `PoseSelector.HandleARInput`.
        ///
        /// Поведенческая часть: в редакторе AR-сессии нет, поэтому AR- и MR-провайдеры обязаны
        /// честно сообщать «недоступен» — на старом коде AR-провайдер отвечал `true` на Windows?
        /// Нет: старый ответ был «платформа Android или iOS», то есть в редакторе `false`. Поэтому
        /// решающими здесь являются проверки ПРАВИЛА (подсистема AR, делегирование луча, отказ
        /// выбора, сброс ссылки) — и они падали на старом коде все четыре.
        /// </summary>
        [Test]
        public void P3_10_ProvidersReportRealAvailability()
        {
            GameObject arGo = new GameObject("ARInputProvider_P3_10");
            GameObject mrGo = new GameObject("MRInputProvider_P3_10");
            try
            {
                ARInputProvider ar = arGo.AddComponent<ARInputProvider>();
                MRInputProvider mr = mrGo.AddComponent<MRInputProvider>();

                Assert.IsFalse(mr.IsAvailable(),
                    "MR-провайдер объявил себя доступным, хотя не умеет ни одного действия (P3-10)");
                Assert.IsFalse(ar.IsAvailable(),
                    "AR-провайдер объявил себя доступным без AR-подсистемы (P3-10)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(arGo);
                UnityEngine.Object.DestroyImmediate(mrGo);
            }

            string arSource = StripComments(ReadScript("Assets/_Project/01_Scripts/VR/ARInputProvider.cs"));
            Assert.Greater(arSource.Length, 1500, "файл AR-провайдера подозрительно мал");
            StringAssert.Contains("ARSession.state", arSource,
                "AR-провайдер снова судит о доступности по платформе, а не по AR-подсистеме (P3-10)");

            string raycast = ExtractMethod(arSource, "public bool ARRaycastFromTouch(Vector2 touchPos, out Vector3 hitPoint)");
            Assert.IsNotNull(raycast, "не найден ARRaycastFromTouch — проверка потеряла смысл");
            Assert.Greater(raycast.Length, 100, "тело ARRaycastFromTouch подозрительно короткое");
            StringAssert.Contains("TryRaycast", raycast,
                "ARRaycastFromTouch снова заглушка: по нему не работает ни прицел, ни постановка (P3-10)");
            StringAssert.Contains("return true;", raycast,
                "ARRaycastFromTouch не умеет вернуть попадание — попадание всегда «мимо» (P3-10)");

            string manager = StripComments(ReadScript("Assets/_Project/01_Scripts/Input/InputManager.cs"));
            Assert.Greater(manager.Length, 4000, "файл менеджера ввода подозрительно мал");
            StringAssert.Contains("!wanted.IsAvailable()", manager,
                "нерабочий провайдер снова выбирается без проверки доступности (P3-10)");
            StringAssert.Contains("if (Instance == this) Instance = null;", manager,
                "статическая ссылка не сбрасывается в OnDestroy — «фальшиво-null» объект остаётся (P3-10)");
        }

        // ============================================================ P3-13

        /// <summary>
        /// P3-13: ПОРЯДОК «НОВЫЙ ВВОД → LEGACY» ВЫРОВНЕН.
        ///
        /// ЧТО БЫЛО. `IsMouseButtonPressed` и `ReadMouseDelta` начинались с legacy `Input.*` и при
        /// успехе возвращались, тогда как все клавиатурные хелперы класса — наоборот. При
        /// `activeInputHandler = Both` ветка `Mouse.current` не исполнялась никогда.
        /// На старом коде порядок внутри обоих методов был обратным — тест падал.
        /// </summary>
        [Test]
        public void P3_13_MouseHelpers_TryNewInputFirst()
        {
            string freeFly = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Core/FreeFlyCameraController.cs"));
            Assert.Greater(freeFly.Length, 40000, "файл камеры подозрительно мал — проверка потеряла смысл");

            string pressed = ExtractMethod(freeFly, "private bool IsMouseButtonPressed(int button)");
            Assert.IsNotNull(pressed, "не найден метод IsMouseButtonPressed — проверка потеряла смысл");
            Assert.Greater(pressed.Length, 100, "тело IsMouseButtonPressed подозрительно короткое");

            int newApi = pressed.IndexOf("Mouse.current", StringComparison.Ordinal);
            int legacy = pressed.IndexOf("Input.GetMouseButton", StringComparison.Ordinal);
            Assert.Greater(newApi, -1, "в IsMouseButtonPressed нет ветки новой системы ввода (P3-13)");
            Assert.Greater(legacy, -1, "в IsMouseButtonPressed нет legacy-отката (P3-13)");
            Assert.Less(newApi, legacy,
                "IsMouseButtonPressed снова начинает с legacy: при «Both» новая ветка не исполняется (P3-13)");

            string delta = ExtractMethod(freeFly, "private Vector2 ReadMouseDelta()");
            Assert.IsNotNull(delta, "не найден метод ReadMouseDelta — проверка потеряла смысл");
            Assert.Greater(delta.Length, 100, "тело ReadMouseDelta подозрительно короткое");

            int newDelta = delta.IndexOf("Mouse.current", StringComparison.Ordinal);
            int legacyDelta = delta.IndexOf("GetAxisRaw", StringComparison.Ordinal);
            Assert.Greater(newDelta, -1, "в ReadMouseDelta нет ветки новой системы ввода (P3-13)");
            Assert.Greater(legacyDelta, -1, "в ReadMouseDelta нет legacy-отката (P3-13)");
            Assert.Less(newDelta, legacyDelta,
                "ReadMouseDelta снова начинает с legacy-осей: обзор на устройстве без них не работает (P3-13)");
        }
    }
}

