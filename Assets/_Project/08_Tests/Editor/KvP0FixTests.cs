// ================================================================================================
//  KvP0FixTests.cs — EditMode-тесты БЛОКЕРОВ, исправленных по итогам тестирования 11.11.2026.
//
//  ОТКУДА ЗАДАЧА. Промпт `Docs/ПРОМТ_DSH_ИСПРАВЛЕНИЕ_ПОСЛЕ_ТЕСТИРОВАНИЯ_2026-11-11.md` перечисляет
//  89 подтверждённых дефектов. Тесты здесь закрывают БЛОКЕРЫ (P0) — те, из-за которых приложение
//  врёт о безопасности, теряет управление или не собирается.
//
//  ПРАВИЛО ЭТОГО ФАЙЛА: каждый тест обязан ПАДАТЬ на старом коде. Там, где проверяемую логику
//  можно отделить от сцены, она вынесена в ЧИСТУЮ функцию (`NeedsFactoryJointLimits`,
//  `VRInputProvider.Edge`, `KvArCameraGate.FrameHasContent`) — иначе проверка сводится к грепу
//  исходника, а такой тест проходит и на сломанном коде.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using KazistovVvAr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Блокеры P0: лимиты суставов, фронт нажатия, содержимое кадра, подпись сборки.</summary>
        [Category(KvTestCategories.Fast)]
    public class KvP0FixTests
    {
        private static string Root
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        private static string Read(string relative)
        {
            string path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "не найден исходник: " + path);
            return File.ReadAllText(path);
        }

        // ============================================================ P0-2: ЛИМИТЫ СУСТАВОВ

        /// <summary>
        /// ЗАДАННЫЕ ОПЕРАТОРОМ ЛИМИТЫ СУСТАВОВ НЕ ЗАМЕНЯЮТСЯ ПАСПОРТНЫМИ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. В `InitializeJointLimits()` присваивания паспортных значений
        /// стояли ПОСЛЕ `if` без `else` и выполнялись ВСЕГДА: любые лимиты из инспектора
        /// уничтожались на первом кадре. По лимитам считают `ApplyJointLimits()` и
        /// `ReachabilityOracle`, то есть это ограничитель безопасности — и он молча работал по
        /// чужим пределам.
        /// </summary>
        [Test]
        public void SixAxis_KeepsOperatorJointLimits()
        {
            var custom = new[]
            {
                new Vector2(-10f, 10f), new Vector2(-20f, 20f), new Vector2(-30f, 30f),
                new Vector2(-40f, 40f), new Vector2(-50f, 50f), new Vector2(-60f, 60f)
            };

            Assert.IsFalse(SixAxisController.NeedsFactoryJointLimits(custom),
                "заданные лимиты нельзя объявлять «не заданными» — они будут затёрты паспортными");
            Assert.AreEqual(new Vector2(-10f, 10f), custom[0], "проверка не должна менять сами лимиты");
        }

        /// <summary>ПАСПОРТНЫЕ ПРЕДЕЛЫ НУЖНЫ ТОЛЬКО КОГДА СВОИХ НЕТ: null, короткий массив, все нули.</summary>
        [Test]
        public void SixAxis_FactoryLimitsOnlyWhenNothingSet()
        {
            Assert.IsTrue(SixAxisController.NeedsFactoryJointLimits(null), "null — нужны паспортные");
            Assert.IsTrue(SixAxisController.NeedsFactoryJointLimits(new Vector2[3]),
                "массив короче шести — нужны паспортные");
            Assert.IsTrue(SixAxisController.NeedsFactoryJointLimits(new Vector2[6]),
                "все нули — это «не настроено», нужны паспортные");

            // Один ненулевой предел — уже настройка: остальные трогать нельзя.
            var oneSet = new Vector2[6];
            oneSet[3] = new Vector2(-5f, 5f);
            Assert.IsFalse(SixAxisController.NeedsFactoryJointLimits(oneSet),
                "достаточно одного заданного предела, чтобы не подменять всю таблицу");
        }

        /// <summary>Паспортные пределы — копия: правка результата не меняет эталон следующего робота.</summary>
        [Test]
        public void SixAxis_FactoryLimitsAreCopied()
        {
            Vector2[] first = SixAxisController.GetFactoryJointLimits();
            Assert.AreEqual(6, first.Length, "у 6-осевого робота шесть пределов");
            first[0] = new Vector2(999f, 999f);

            Vector2[] second = SixAxisController.GetFactoryJointLimits();
            Assert.AreNotEqual(999f, second[0].x,
                "вызывающий получил ссылку на эталон и испортил его для следующих роботов");
        }

        /// <summary>Строка журнала называет ФАКТИЧЕСКИЕ пределы — по ней наладчик сверяет настройку.</summary>
        [Test]
        public void SixAxis_DescribesActualLimits()
        {
            var limits = new[]
            {
                new Vector2(-170f, 170f), new Vector2(-90f, 150f), new Vector2(-70f, 225f),
                new Vector2(-180f, 180f), new Vector2(-120f, 120f), new Vector2(-180f, 180f)
            };
            string text = SixAxisController.DescribeJointLimits(limits);

            StringAssert.Contains("J1", text);
            StringAssert.Contains("-170", text);
            StringAssert.Contains("225", text);
            StringAssert.Contains("°", text, "пределы задаются в градусах — единица обязана быть видна");
            StringAssert.Contains("не заданы", SixAxisController.DescribeJointLimits(null));
        }

        // ============================================================ P0-5: ФРОНТ НАЖАТИЯ VR

        /// <summary>
        /// ФРОНТ НАЖАТИЯ КУРКА — ЭТО ПЕРЕХОД, А НЕ УРОВЕНЬ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. `VRInputProvider.GetSelectDown()` возвращал
        /// `RightTrigger >= порог`, то есть был равен `GetSelectHeld()`. Контракт `InputProvider`:
        /// «Down» — нажато ИМЕННО В ЭТОМ КАДРЕ. Цена: одно длинное нажатие курка заполняло все три
        /// точки калибровки (`CalibrationTool`) и применяло случайную калибровку робота.
        /// </summary>
        [Test]
        public void VrInput_SelectDownIsEdgeNotLevel()
        {
            // Первый кадр нажатия — фронт есть.
            Assert.IsTrue(VRInputProvider.Edge(true, false), "переход «не нажато → нажато» — это Down");
            // Удержание — фронта НЕТ (именно это и было сломано).
            Assert.IsFalse(VRInputProvider.Edge(true, true),
                "удержание курка не должно каждый кадр считаться новым нажатием");
            // Отпускание — тоже не нажатие.
            Assert.IsFalse(VRInputProvider.Edge(false, true), "отпускание — не нажатие");
            Assert.IsFalse(VRInputProvider.Edge(false, false), "покой — не нажатие");
        }

        /// <summary>
        /// ПОСЛЕДОВАТЕЛЬНОСТЬ КАДРОВ ДАЁТ РОВНО ОДНО НАЖАТИЕ. Проверяется модель, которой живёт
        /// провайдер: два кадра удержания подряд — это ОДИН Down, а не два.
        /// </summary>
        [Test]
        public void VrInput_HoldingProducesExactlyOnePress()
        {
            bool wasHeld = false;
            int presses = 0;
            // Курок держат пять кадров подряд.
            for (int frame = 0; frame < 5; frame++)
            {
                bool held = true;
                if (VRInputProvider.Edge(held, wasHeld)) presses++;
                wasHeld = held;
            }

            Assert.AreEqual(1, presses,
                "пять кадров удержания обязаны дать ОДНО нажатие, иначе курсор/калибровка «строчат»");
        }

        /// <summary>Провайдер действительно хранит прошлое состояние, а не считает уровень.</summary>
        [Test]
        public void VrInput_SourceUsesStoredPreviousState()
        {
            string source = Read("Assets/_Project/01_Scripts/Input/VRInputProvider.cs");

            StringAssert.Contains("selectWasHeld", source,
                "провайдер не помнит прошлое состояние курка — фронт посчитать нечем");
            StringAssert.Contains("public override bool GetSelectDown() { return selectDownThisFrame; }", source,
                "GetSelectDown обязан отдавать ФРОНТ, а не уровень курка");
            StringAssert.Contains("Edge(selectHeldNow, selectWasHeld)", source,
                "фронт обязан считаться чистой функцией Edge");
        }

        // ============================================================ P0-4: ВВОД ГЕЙМПАДА

        /// <summary>
        /// ВВОД С ГЕЙМПАДА НЕ ОБРАЩАЕТСЯ К ИМЕНАМ, КОТОРЫХ НЕТ В `InputManager.asset`.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. `Input.GetButtonDown("LB"/"X"/"Y")` и
        /// `Input.GetAxis("RightStickX"/"RightStickY")` — таких имён в проекте НЕТ, Unity бросает
        /// `ArgumentException` КАЖДЫЙ КАДР. Причём `IsAvailable()` подтверждала провайдер по
        /// legacy-джойстику, и `InputManager` выбирал его на старте — оператор оставался без
        /// управления.
        /// </summary>
        [Test]
        public void Gamepad_UsesOnlyDeclaredInputNames()
        {
            string source = Read("Assets/_Project/01_Scripts/Input/GamepadInputProvider.cs");

            foreach (string missing in new[] { "\"LB\"", "\"RightStickX\"", "\"RightStickY\"" })
                StringAssert.DoesNotContain("GetButtonDown(" + missing, source,
                    "имени " + missing + " нет в InputManager.asset — вызов бросает ArgumentException");
            StringAssert.DoesNotContain("GetAxis(\"RightStickX\")", source,
                "оси RightStickX в InputManager.asset нет — вызов бросает ArgumentException");

            // Объявленные в проекте имена использовать можно.
            StringAssert.Contains("Input.GetAxis(\"Horizontal\")", source,
                "движение обязано читаться с объявленных осей");
        }

        /// <summary>
        /// ДОСТУПНОСТЬ ПРОВАЙДЕРА НЕ ПОДТВЕРЖДАЕТСЯ LEGACY-ДЖОЙСТИКОМ.
        ///
        /// Кнопки читаются через `Gamepad.current`; если новый Input System устройства не видит,
        /// провайдер не умеет ничего. Раньше `Input.GetJoystickNames()` давало «доступен», и
        /// `InputManager` выбирал мёртвый провайдер.
        /// </summary>
        [Test]
        public void Gamepad_AvailabilityRequiresNewInputSystem()
        {
            string source = Read("Assets/_Project/01_Scripts/Input/GamepadInputProvider.cs");
            StringAssert.DoesNotContain("GetJoystickNames", source,
                "доступность по legacy-джойстику выбирает провайдер, который не может читать кнопки");
        }

        // ============================================================ P0-9: СОДЕРЖИМОЕ КАДРА

        /// <summary>
        /// ЧЁРНЫЙ КАДР — ЭТО НЕ «КАМЕРА РАБОТАЕТ».
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ (найден прогоном на планшете 11.11.2026). Приложение получило
        /// 31 кадр, написало «КАМЕРА: РАБОТАЕТ — программа действительно использует камеру», а
        /// сохранённый им же кадр оказался полностью чёрным. Оператор видел чёрный экран с
        /// интерфейсом, журнал утверждал успех.
        /// </summary>
        [Test]
        public void CameraGate_BlackFrameIsNotContent()
        {
            var black = new Color32[64 * 64];
            for (int i = 0; i < black.Length; i++) black[i] = new Color32(0, 0, 0, 255);

            Assert.IsFalse(KvArCameraGate.FrameHasContent(black),
                "полностью чёрный кадр обязан считаться ПУСТЫМ — иначе предохранитель врёт");
            Assert.AreEqual(0f, KvArCameraGate.AverageLuminance(black), 1e-4f);
        }

        /// <summary>Настоящий кадр содержимое имеет — проверка не должна блокировать рабочую камеру.</summary>
        [Test]
        public void CameraGate_NormalFrameHasContent()
        {
            var lit = new Color32[64 * 64];
            for (int i = 0; i < lit.Length; i++) lit[i] = new Color32(120, 130, 140, 255);

            Assert.IsTrue(KvArCameraGate.FrameHasContent(lit),
                "обычный серый кадр — это содержимое; иначе рабочая камера считалась бы сломанной");
            Assert.Greater(KvArCameraGate.AverageLuminance(lit), 0.4f);
        }

        /// <summary>Границы: null и пустой массив не должны ни падать, ни «проходить».</summary>
        [Test]
        public void CameraGate_EmptyInputIsNotContent()
        {
            Assert.IsFalse(KvArCameraGate.FrameHasContent(null));
            Assert.IsFalse(KvArCameraGate.FrameHasContent(new Color32[0]));
            Assert.AreEqual(0f, KvArCameraGate.AverageLuminance(null), 1e-4f);
        }

        /// <summary>
        /// ПОДТВЕРЖДЕНИЕ КАМЕРЫ ТРЕБУЕТ ПРОВЕРКИ СОДЕРЖИМОГО. Одного счётчика кадров недостаточно:
        /// именно он и врал на устройстве.
        /// </summary>
        [Test]
        public void CameraGate_ConfirmationRequiresImageCheck()
        {
            string source = Read("Assets/_Project/01_Scripts/Ar/KvArCameraGate.cs");

            StringAssert.Contains("imageChecked", source,
                "подтверждение камеры не зависит от проверки содержимого кадра");
            StringAssert.Contains("VerifyCameraImageOnce", source,
                "нет проверки содержимого кадра — снова останется только счётчик");
            StringAssert.Contains("FrameHasContent", source,
                "решение «камера работает» обязано опираться на содержимое, а не на число кадров");
        }

        // ============================================================ P0-7: ПРАВДА О MR

        /// <summary>
        /// ВКЛЮЧЕНИЕ СКВОЗНОГО ВИДА ПОДТВЕРЖДАЕТСЯ ФАКТОМ, А НЕ ПРИСВАИВАНИЕМ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. `EnableMeta()` заканчивался `return manager.enabled;` — то есть
        /// возвращал значение, присвоенное строкой выше, и НИКОГДА не мог вернуть false. Приложение
        /// объявляло себя MR на устройстве без сквозного вида, оператор видел чёрный экран.
        /// </summary>
        [Test]
        public void MrRuntime_ConfirmsPassthroughBySubsystem()
        {
            string source = Read("Assets/_Project/01_Scripts/Xr/KvMrRuntime.cs");

            StringAssert.DoesNotContain("return manager.enabled;", source,
                "возврат только что присвоенного значения — это не проверка, а её имитация");
            StringAssert.Contains("HasRunningCameraSubsystem", source,
                "подтверждение обязано опираться на запущенную подсистему камеры");
        }

        /// <summary>
        /// БЕЗ XR ПОДСИСТЕМЫ ПРОВЕРКА ЧЕСТНО ОТВЕЧАЕТ «НЕТ» И НЕ ПАДАЕТ (в редакторе это норма).
        /// </summary>
        [Test]
        public void MrRuntime_SubsystemProbeIsSafeWithoutXr()
        {
            Assert.DoesNotThrow(delegate { KazistovVvXr.KvMrRuntime.HasRunningCameraSubsystem(); },
                "проба подсистемы не должна бросать без XR — иначе редактор и тесты падают");
        }

        // ============================================================ P0-1: ПАРОЛЬ ПОДПИСИ

        /// <summary>
        /// ПАРОЛЯ ДЕЙСТВУЮЩЕГО КЛЮЧА ПОДПИСИ НЕТ В ИСХОДНИКАХ.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. В `KvAndroidSigning` лежала константа с паролем ключа, а сам
        /// keystore — в истории git. Вместе это позволяло выпускать обновления от имени владельца.
        ///
        /// ЗДЕСЬ ПАРОЛЯ ТОЖЕ НЕТ — И ЭТО ИСПРАВЛЕНО 12.11.2026. В прошлой версии тест проверял
        /// `DoesNotContain("&lt;пароль&gt;")`, то есть САМ ХРАНИЛ ПАРОЛЬ ЛИТЕРАЛОМ в отслеживаемом
        /// файле: проверка «секрета нет в исходниках» светила секретом в репозиторий (найдено
        /// приёмкой: `git grep` по паролю находил не код, а этот тест). Теперь проверяется
        /// ПРИЗНАК, а не значение: если `ResolvePassword()` начнёт возвращать строковый литерал,
        /// тест покраснеет — а самого пароля в файле нет.
        /// </summary>
        [Test]
        public void Signing_NoHardcodedPassword()
        {
            string source = Read("Assets/_Project/01_Scripts/Editor/KvAndroidSigning.cs");

            // Невакуумные страховки: без них проверка «литерала нет» прошла бы на пустом файле.
            StringAssert.Contains("ResolvePassword", source,
                "в файле нет ResolvePassword — проверка была бы вакуумной (урок P4-2)");
            StringAssert.Contains("KAZISTOVVV_KEYPASS", source,
                "пароль обязан читаться из окружения");

            // ГЛАВНАЯ ПРОВЕРКА: внутри ResolvePassword не должно быть возврата литерала.
            int start = source.IndexOf("public static string ResolvePassword", System.StringComparison.Ordinal);
            Assert.Greater(start, 0, "не найден метод ResolvePassword — проверка потеряла смысл");
            int end = source.IndexOf('}', source.IndexOf("return null;", start, System.StringComparison.Ordinal));
            Assert.Greater(end, start, "не найден конец ResolvePassword — проверка потеряла смысл");
            string body = source.Substring(start, end - start);

            Assert.IsFalse(Regex.IsMatch(body, @"return\s+""[^""]{3,}""\s*;"),
                "ResolvePassword возвращает СТРОКОВЫЙ ЛИТЕРАЛ — это и есть вшитый в код пароль ключа " +
                "подписи: кто получил репозиторий, тот может выпускать обновления от имени владельца");
            StringAssert.DoesNotContain("LegacyPassword =", source,
                "встроенный пароль обязан быть УДАЛЁН, а не помечен устаревшим");
        }

        /// <summary>
        /// НЕТ ПАРОЛЯ — СБОРКА ОСТАНАВЛИВАЕТСЯ С ПОНЯТНОЙ ПРИЧИНОЙ, А НЕ ПАДАЕТ В GRADLE.
        ///
        /// Раньше исключение настройки подписи глоталось как предупреждение, и сборка продолжалась
        /// с `useCustomKeystore = 1` без паролей, падая сообщением «No keystore passwords were
        /// found» — про пароли, хотя причиной был отсутствующий ФАЙЛ ключа.
        /// </summary>
        [Test]
        public void Signing_FailsEarlyAndClearlyWhenUnconfigured()
        {
            string signing = Read("Assets/_Project/01_Scripts/Editor/KvAndroidSigning.cs");
            StringAssert.Contains("ConfigureOrThrow", signing,
                "нет пути «остановить сборку с понятной причиной»");
            StringAssert.Contains("BuildFailedException", signing,
                "сборка должна падать ДО Gradle, иначе причина теряется");

            string tools = Read("Assets/_Project/01_Scripts/Editor/KvXrTools.cs");
            StringAssert.Contains("ConfigureOrThrow()", tools,
                "сборщик обязан вызывать строгий вариант настройки подписи");
            StringAssert.DoesNotContain("настройка подписи не удалась", tools,
                "ошибка подписи больше не должна превращаться в предупреждение");
        }

        // ============================================================ P0-3: ДВА UI-МЕНЕДЖЕРА

        /// <summary>
        /// ДУБЛИКАТ МЕНЕДЖЕРА ИНТЕРФЕЙСА УДАЛЯЕТ СЕБЯ, А НЕ ОБА.
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ. Проверка была по числу найденных менеджеров, а `Destroy`
        /// отложен до конца кадра: при двух менеджерах КАЖДЫЙ видел «нас двое» и уничтожал себя.
        /// Интерфейс не собирался вовсе, без единой ошибки. Второй менеджер создаётся штатно —
        /// `FreeFlyCameraController.EnsureKazistovVvUi()`.
        /// </summary>
        [Test]
        public void UiManager_DuplicateCheckIsByInstance()
        {
            string source = Read("Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs");

            StringAssert.DoesNotContain("FindObjectsByType<KazistovVvUIManager>", source,
                "подсчёт менеджеров в сцене — именно тот способ, из-за которого удалялись оба");
            StringAssert.Contains("Instance != null && Instance != this", source,
                "дубликат обязан определяться по Instance, как в KvVirtualCursor");
        }
    }
}

