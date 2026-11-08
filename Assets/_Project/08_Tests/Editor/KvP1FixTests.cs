using System.IO;
using System.Text.RegularExpressions;
using KazistovVvFeatures;
using NUnit.Framework;
using TrajectoryCore;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ ИСПРАВЛЕНИЙ P1 (11.11.2026) — «предохранители и настройки сообщают не то, что есть».
    ///
    /// ПРАВИЛО: каждый тест обязан ПАДАТЬ на старом коде. Где поведение оторвать от сцены нельзя
    /// (например, `PlayerPrefs.Save()` в редакторе живёт в памяти и внешне не наблюдаем), тест
    /// проверяет ПРАВИЛО по исходнику — и обязательно с невакуумной страховкой (иначе пустой файл
    /// или переименование сделают проверку ложно зелёной, урок P4-2).
    /// </summary>
        [Category(KvTestCategories.Fast)]
    public class KvP1FixTests
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
        /// УБРАТЬ КОММЕНТАРИИ ИЗ ИСХОДНИКА. Нужно там, где проверяется ПРАВИЛО по тексту: иначе
        /// слово `renderer.material` или `Assert.Pass` в объяснении «что было» считается вызовом,
        /// и проверка ловит собственный комментарий (поймано на прогоне 11.11.2026).
        /// </summary>
        private static string StripComments(string source)
        {
            string withoutBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty,
                RegexOptions.Singleline);
            return Regex.Replace(withoutBlock, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }

        // ================================================================ P1-9: гистерезис IK

        /// <summary>
        /// P1-9: ПЕРЕКЛЮЧЕНИЕ ВЕТВИ IK ТОЛЬКО ПРИ СУЩЕСТВЕННОМ ВЫИГРЫШЕ.
        ///
        /// ЧТО БЫЛО. Условие стояло наоборот (`bestCost &gt; lockedCost * (1 - hysteresis)`), а
        /// `best` — глобальный минимум, поэтому ветвь переключалась именно на МАЛОМ выигрыше и
        /// удерживалась при явном улучшении: гистерезис работал против себя.
        ///
        /// На старом коде первый assert падал (переключение при выигрыше 1 %, хотя порог 20 %).
        /// </summary>
        [Test]
        public void P1_9_BranchSwitch_OnlyOnRealGain()
        {
            // Выигрыш 1 % при пороге 20 % — ветвь НЕ меняем (иначе дёрганье конфигурации).
            Assert.IsFalse(PostureSelector.ShouldSwitchBranch(99.0, 100.0, 0.20),
                "ветвь переключается на 1 % выигрыша при пороге 20 % — гистерезис инвертирован (P1-9)");

            // Выигрыш 50 % при пороге 20 % — меняем: прежняя ветвь стала заметно хуже.
            Assert.IsTrue(PostureSelector.ShouldSwitchBranch(50.0, 100.0, 0.20),
                "ветвь НЕ переключается при выигрыше 50 % — гистерезис заблокировал явное улучшение");

            // Равные стоимости — остаёмся на прежней ветви (устойчивость к шуму).
            Assert.IsFalse(PostureSelector.ShouldSwitchBranch(100.0, 100.0, 0.20),
                "при равной стоимости ветвь не должна меняться");

            // Нулевой гистерезис — переключаемся на любом выигрыше, но не на равенстве.
            Assert.IsTrue(PostureSelector.ShouldSwitchBranch(99.9, 100.0, 0.0),
                "при нулевом гистерезисе выигрыш обязан приниматься");
            Assert.IsFalse(PostureSelector.ShouldSwitchBranch(100.0, 100.0, 0.0),
                "равенство — не выигрыш");

            // Держаться не за что (нет кандидата прежней ветви) — берём лучший.
            Assert.IsTrue(PostureSelector.ShouldSwitchBranch(10.0, double.MaxValue, 0.2),
                "если прежней ветви нет в списке, обязан вернуться лучший кандидат");

            // Мусорные значения не должны приводить к переключению.
            Assert.IsFalse(PostureSelector.ShouldSwitchBranch(double.NaN, 100.0, 0.2),
                "NaN вместо стоимости — переключение запрещено");
        }

        // ================================================================ P1-1/P1-2: сохранение

        /// <summary>
        /// P1-1: НАСТРОЙКИ ИНТЕРФЕЙСА ПИШУТСЯ НА ДИСК.
        ///
        /// ЧТО БЫЛО. `KvSettings.Save` делал только `PlayerPrefs.Set*` без `PlayerPrefs.Save()`:
        /// при падении приложения настройки масштаба/кегля/контраста терялись.
        ///
        /// ПОЧЕМУ ПО ПРАВИЛУ, А НЕ ПО ЗНАЧЕНИЮ: в редакторе `PlayerPrefs` живёт в памяти процесса,
        /// и факт вызова `Save()` наблюдением не проверить. Поэтому проверяется правило: КАЖДАЯ
        /// запись настроек обязана сопровождаться сохранением, и страховка требует, чтобы записи
        /// в файле вообще были (иначе тест был бы вакуумным).
        /// </summary>
        [Test]
        public void P1_1_UiSettings_AreFlushedToDisk()
        {
            string source = ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KvSettings.cs");

            int writes = Regex.Matches(source, @"PlayerPrefs\.Set(Int|Float|String)\(").Count;
            int saves = Regex.Matches(source, @"PlayerPrefs\.Save\(\)").Count;

            Assert.Greater(writes, 0, "в KvSettings.cs нет ни одной записи в PlayerPrefs — проверка потеряла смысл");
            Assert.GreaterOrEqual(saves, writes,
                "записей в PlayerPrefs: " + writes + ", сохранений на диск: " + saves +
                " — настройки интерфейса не переживут перезапуск (P1-1)");
        }

        /// <summary>
        /// P1-2: ОДНО ПРАВИЛО ЗАПИСИ ДЛЯ НАСТРОЕК ШЛЕМА.
        ///
        /// ЧТО БЫЛО. `SetModeIndex`/`ToggleMoveMode` сохраняли на диск, а остальные ~25 сеттеров
        /// (панель у руки, зона достижимости, лимиты, пастхру, виньетка, дистанции, масштаб мира,
        /// рост оператора, луч, рука луча, жесты) — нет.
        ///
        /// На старом коде тест падал: прямых `PlayerPrefs.Set*` в файле было 27 при 2 сохранениях.
        /// </summary>
        [Test]
        public void P1_2_HeadsetSettings_HaveSingleSavePath()
        {
            string source = ReadScript("Assets/_Project/01_Scripts/Xr/KvXrSettings.cs");

            // Все записи обязаны идти через хелперы SetInt/SetFloat (они же сохраняют на диск).
            int directWrites = Regex.Matches(source, @"PlayerPrefs\.Set(Int|Float|String)\(").Count;
            Assert.AreEqual(2, directWrites,
                "прямых записей в PlayerPrefs: " + directWrites +
                " — их должно быть ровно 2 (внутри хелперов SetInt/SetFloat), иначе часть сеттеров " +
                "пишет без сохранения на диск (P1-2)");

            int helperSaves = Regex.Matches(source, @"private static void Set(Int|Float)\(string key")
                .Count;
            Assert.AreEqual(2, helperSaves, "нет двух хелперов записи настроек (P1-2)");

            int saves = Regex.Matches(source, @"PlayerPrefs\.Save\(\)").Count;
            Assert.GreaterOrEqual(saves, 2, "хелперы записи не сохраняют настройки на диск (P1-2)");

            // Геттеры не имеют права писать (правило [K5]).
            Assert.IsFalse(Regex.IsMatch(source, @"Get\w+\([^)]*\)\s*\{[^}]*PlayerPrefs\.Set"),
                "геттер настроек пишет в PlayerPrefs — чтение меняет состояние ([K5])");
        }

        // ================================================================ P1-4: геттер-предохранитель

        /// <summary>
        /// P1-4: ЧТЕНИЕ ФЛАГА ОПТИМИЗАЦИИ НЕ ОТКАТЫВАЕТ ГЛОБАЛЬНЫЙ РЕЕСТР ПРОКСИ.
        ///
        /// ЧТО БЫЛО. Геттер `Enabled` звал `Load()`, а `Load()` писал в `CollisionProxies.Enabled`.
        /// Одно чтение свойства выключало предохранитель зазоров, если в настройках стоял 0.
        ///
        /// На старом коде тест падал: после чтения `Enabled` реестр становился `false`.
        /// </summary>
        [Test]
        public void P1_4_ReadingEnabled_DoesNotResetCollisionProxies()
        {
            string key = KvCollisionOptimizer.EnabledPrefsKey;
            int savedPrefs = PlayerPrefs.GetInt(key, -1);
            bool savedRegistry = CollisionProxies.Enabled;
            try
            {
                // В настройках «выключено», а в реестре — «включено» (как после ручного включения).
                PlayerPrefs.SetInt(key, 0);
                PlayerPrefs.Save();
                CollisionProxies.Enabled = true;

                var optimizer = new KvCollisionOptimizer();
                bool reported = optimizer.Enabled;      // это ЧТЕНИЕ не должно ничего менять

                Assert.IsFalse(reported, "оптимизатор обязан читать значение из настроек");
                Assert.IsTrue(CollisionProxies.Enabled,
                    "чтение флага оптимизации откатило глобальный реестр прокси столкновений — " +
                    "предохранитель зазоров выключен побочным эффектом геттера (P1-4)");
            }
            finally
            {
                if (savedPrefs >= 0) PlayerPrefs.SetInt(key, savedPrefs);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                CollisionProxies.Enabled = savedRegistry;
            }
        }

        // ================================================================ P1-6: калибровка

        /// <summary>
        /// P1-6: КАЛИБРОВКА АБСОЛЮТНА И НЕ НАКАПЛИВАЕТ ПЕРЕКОС.
        ///
        /// ЧТО БЫЛО. Смещение сохранялось в `PlayerPrefs` и НИКОГДА не читалось (замер: записей 7,
        /// чтений 0), а поворот накладывался на текущий (`rotation = rotOffset * rotation`) —
        /// вторая калибровка подряд складывала перекос.
        /// </summary>
        [Test]
        public void P1_6_Calibration_IsAbsoluteNotAccumulated()
        {
            var real = new[]
            {
                new Vector3(1f, 0f, 2f), new Vector3(1f, 0f, 2.5f), new Vector3(1.3f, 0.8f, 2f)
            };
            var model = new[]
            {
                Vector3.zero, new Vector3(0f, 0.4f, 0f), new Vector3(0.3f, 0.8f, 0f)
            };

            Vector3 pos1, pos2;
            Quaternion rot1, rot2;
            CalibrationTool.ComputeOffsets(real, model, out pos1, out rot1);
            CalibrationTool.ComputeOffsets(real, model, out pos2, out rot2);

            Assert.AreEqual(pos1, pos2, "позиция калибровки зависит от истории — накопление смещения (P1-6)");
            Assert.AreEqual(0f, Quaternion.Angle(rot1, rot2), 0.01f,
                "поворот калибровки накапливается при повторном вызове — база «довернётся» ещё раз (P1-6)");

            // База встаёт так, чтобы модельная точка 0 совпала с реальной.
            Assert.AreEqual(real[0].x, (pos1 + rot1 * model[0]).x, 0.001f,
                "точка основания не совмещена с реальной по X");

            Vector3 posBad;
            Quaternion rotBad;
            CalibrationTool.ComputeOffsets(new[] { Vector3.zero }, new[] { Vector3.zero },
                out posBad, out rotBad);
            Assert.AreEqual(Vector3.zero, posBad, "при недостатке точек смещение обязано быть нулевым");
            Assert.AreEqual(Quaternion.identity, rotBad, "при недостатке точек поворот обязан быть единичным");
        }

        // ================================================================ P1-10: план без времён

        /// <summary>
        /// P1-10: ПЛАН БЕЗ `Times` НЕ ИСПОЛНЯЕТСЯ.
        ///
        /// ЧТО БЫЛО. `Play` и `SafetyGate.Approve` проверяли только `Path`; `Update` разыменовывал
        /// `active.Times` → `NullReferenceException` в цикле движения, мимо `Stop(reason)`.
        /// </summary>
        [Test]
        public void P1_10_TrajectoryWithoutTimes_IsRejected()
        {
            var plan = new PlannedTrajectory
            {
                Path = new[] { new double[] { 0, 0, 0 }, new double[] { 1, 1, 1 } },
                Times = null
            };
            Assert.IsFalse(TrajectoryExecutor.HasUsableTimeline(plan),
                "план без времён признан годным — падение в Update (P1-10)");

            plan.Times = new[] { 0f };
            Assert.IsFalse(TrajectoryExecutor.HasUsableTimeline(plan),
                "план с несовпадающей длиной Times/Path признан годным (P1-10)");

            plan.Times = new[] { 0f, 1f };
            Assert.IsTrue(TrajectoryExecutor.HasUsableTimeline(plan), "корректный план обязан быть годным");

            Assert.IsFalse(TrajectoryExecutor.HasUsableTimeline(null), "null-план не может быть годным");

            // Предохранитель обязан отклонять такой план ДО пуска.
            var gate = new SafetyGate();
            gate.NotifyState();
            SafetyReason reason;
            var badPlan = new PlannedTrajectory { Path = new[] { new double[] { 0, 0, 0 } }, Times = null };
            Assert.IsFalse(gate.Approve(badPlan, null, out reason),
                "SafetyGate пропустил план без времён — предохранитель пропускает падение (P1-10)");
            Assert.AreEqual(SafetyReason.NoData, reason, "причина отказа обязана быть NoData");
        }

        // ================================================================ P1-16: один порог зазора

        /// <summary>
        /// P1-16: ОДИН ИСТОЧНИК ПОРОГА ЗАЗОРА.
        ///
        /// ЧТО БЫЛО. Три числа для одного понятия: 0,015 в предохранителе, он же «× 0,5» без
        /// объяснения и отдельное 0,02 в планировщике.
        /// </summary>
        [Test]
        public void P1_16_ClearanceThreshold_HasSingleSource()
        {
            var gate = new SafetyGate();
            Assert.AreEqual(0.015f, SafetyGate.DefaultMinClearance, 0.0001f, "базовый порог зазора изменён");
            Assert.AreEqual(0.015f, gate.minClearance, 0.0001f, "действующий порог не совпадает с источником");
            Assert.AreEqual(SafetyGate.DefaultMinClearance * SafetyGate.PlanningClearanceFactor,
                SafetyGate.DefaultPlanningClearance, 0.0001f,
                "порог планировщика не выводится из базового порога (P1-16)");
            Assert.AreEqual(gate.minClearance * 0.5f, gate.StandingClearance, 0.0001f,
                "порог стоящей позы считается не от действующего порога");

            var planner = new Planner();
            Assert.AreEqual(SafetyGate.DefaultPlanningClearance, planner.clearance, 0.0001f,
                "у планировщика снова своё число зазора — «прошло проверку» значит разное (P1-16)");
            Assert.GreaterOrEqual(planner.clearance, SafetyGate.DefaultMinClearance,
                "планировщик обязан требовать запас не меньше предохранителя");
        }

        // ================================================================ P1-17/18: IK и откат

        /// <summary>
        /// P1-17: ДОПУСК IK ДЛЯ SCARA — ИМЕНОВАННОЕ ЧИСЛО, А НЕ 0,05 В ТЕЛЕ.
        /// P1-18: ОТКАТ САМОСТОЛКНОВЕНИЯ НЕ ВЫХОДИТ ЗА БУФЕР.
        /// </summary>
        [Test]
        public void P1_17_18_IkToleranceAndRollbackAreSafe()
        {
            var ik = new IkSolver();
            Assert.AreEqual(0.05f, ik.scaraFkTolerance, 0.0001f,
                "допуск SCARA не вынесен в именованное поле (P1-17)");
            Assert.AreEqual(0.004f, ik.positionTolerance, 0.0001f, "допуск 6-осевого изменён");
            Assert.Greater(ik.scaraFkTolerance, ik.positionTolerance,
                "допуск SCARA обязан быть шире, и это должно быть видно из полей");

            Assert.AreEqual(5, SixAxisController.SafeRollbackHigh(6, 6, 7),
                "откат выходит за буфер — исключение в механизме защиты от самостолкновения (P1-18)");
            Assert.AreEqual(3, SixAxisController.SafeRollbackHigh(3, 8, 6),
                "законная граница отката не должна сжиматься");
        }

        // ================================================================ P1-19/P3-14

        /// <summary>
        /// P1-19: СИГНАТУРА ЗОНЫ НЕ МОЖЕТ «СОВПАСТЬ» ПОСЛЕ ОТКАЗА.
        /// P3-14: ДОЛЯ ХОДА СУСТАВА СЧИТАЕТСЯ ОТ ХОДА, А НЕ ОТ 360°.
        /// </summary>
        [Test]
        public void P1_19_P3_14_InvalidSignatureAndTravelFraction()
        {
            string a = WorkspaceVisualizer.InvalidSignature("6-осевой");
            string b = WorkspaceVisualizer.InvalidSignature("6-осевой");
            Assert.AreNotEqual(a, b,
                "невалидные сигнатуры совпадают — зона навсегда останется «построенной верно» (P1-19)");
            StringAssert.StartsWith("invalid|", a, "невалидная сигнатура не помечена как невалидная");

            // Сустав с ходом 70°: остаток 36° — это половина хода, а не 10 %.
            Assert.AreEqual(0.514f, WorkspaceVisualizer.MarginFraction(36f, 70f, false), 0.002f,
                "доля хода считается от 360°, а не от хода сустава — индикатор загорается поздно (P3-14)");
            Assert.AreEqual(0.1f, WorkspaceVisualizer.MarginFraction(36f, 70f, true), 0.002f,
                "для призмы доля хода считается неверно");
            Assert.AreEqual(1f, WorkspaceVisualizer.MarginFraction(500f, 70f, false), 0.0001f,
                "доля хода обязана зажиматься в 0…1");
        }

        // ================================================================ P1-21: команды без стола

        /// <summary>
        /// P1-21: КОМАНДЫ МОДЕЛЕЙ РОБОТА НЕДОСТУПНЫ БЕЗ СТОЛА.
        ///
        /// ЧТО БЫЛО. `IsEnabled = delegate { return true; }` при комментарии «Доступно, когда в
        /// сцене есть стол»: меню разрешало постановку робота в воздух.
        /// </summary>
        [Test]
        public void P1_21_ModelCommands_RequireTable()
        {
            var saved = new System.Collections.Generic.List<GameObject>();
            try
            {
                KazistovVvUI.KvRobotCommands.InvalidateTableCache();
                Assert.IsFalse(KazistovVvUI.KvRobotCommands.SceneHasTable(),
                    "проверка «есть ли стол» нашла стол в пустой сцене (P1-21)");

                GameObject table = new GameObject("Стол_квадрат_120000");
                table.AddComponent<KazistovVvUI.RegisteredObject>().DisplayName = "Стол_квадрат_120000";
                saved.Add(table);

                KazistovVvUI.KvRobotCommands.InvalidateTableCache();
                Assert.IsTrue(KazistovVvUI.KvRobotCommands.SceneHasTable(),
                    "стол в сцене не найден — команды моделей будут заблокированы зря (P1-21)");
            }
            finally
            {
                foreach (GameObject go in saved) if (go != null) Object.DestroyImmediate(go);
                KazistovVvUI.KvRobotCommands.InvalidateTableCache();
            }
        }

        // ================================================================ P1-8: скорость робота

        /// <summary>
        /// P1-8: СКОРОСТЬ РОБОТА БЕРЁТСЯ ИЗ ОДНОГО МЕСТА И НЕ ПОДМЕНЯЕТСЯ МОЛЧА.
        ///
        /// ЧТО БЫЛО. Обе модели писали `Instance != null ? Instance.robotSpeed : 1f` — без ассета
        /// скорость тихо становилась 1.0 (оператор ставил 0.2 → робот ехал в 5 раз быстрее).
        /// </summary>
        [Test]
        public void P1_8_RobotSpeed_ComesFromSinglePlace()
        {
            SettingsData data = ScriptableObject.CreateInstance<SettingsData>();
            SettingsData savedInstance = SettingsData.Instance;
            try
            {
                data.robotSpeed = 0.2f;
                SettingsData.Instance = data;
                Assert.AreEqual(0.2f, SettingsData.RobotSpeedOrWarn(), 0.0001f,
                    "скорость из настроек не применяется — контур управления берёт своё (P1-8)");
            }
            finally
            {
                SettingsData.Instance = savedInstance;
                Object.DestroyImmediate(data);
            }

            string six = ReadScript("Assets/_Project/01_Scripts/Core/SixAxisController.cs");
            string scara = ReadScript("Assets/_Project/01_Scripts/Core/SCARAController.cs");
            StringAssert.Contains("SettingsData.RobotSpeedOrWarn()", six,
                "6-осевой берёт скорость мимо источника (P1-8)");
            StringAssert.Contains("SettingsData.RobotSpeedOrWarn()", scara,
                "SCARA берёт скорость мимо источника (P1-8)");
            StringAssert.DoesNotContain("Instance.robotSpeed : 1f", six,
                "в 6-осевом вернулась подмена скорости на 1.0 (P1-8)");
            StringAssert.DoesNotContain("Instance.robotSpeed : 1f", scara,
                "в SCARA вернулась подмена скорости на 1.0 (P1-8)");
        }

        // ================================================================ P1-13/14/15

        /// <summary>
        /// P1-13: ЛАЗЕРЫ Z/X НА ЗАЩЁЛКЕ ФРОНТА.
        /// P1-14: ВЫБОР КЛИКОМ ЗАПИСЫВАЕТСЯ В ПОЛЕ (публичное свойство не врёт).
        /// P1-15: ВТОРОЙ СПОСОБ ВЫБОРА РОБОТА БОЛЬШЕ НЕ ПИШЕТ `SetActive`.
        /// </summary>
        [Test]
        public void P1_13_14_15_InputEdgesAndSingleRobotSelection()
        {
            string freeFly = ReadScript("Assets/_Project/01_Scripts/Core/FreeFlyCameraController.cs");
            StringAssert.Contains("KeyToggleEdge(KeyCode.Z, ref zKeyWasDown)", freeFly,
                "лазер Z переключается на ненадёжном фронте — «включается и тут же гаснет» (P1-13)");
            StringAssert.Contains("KeyToggleEdge(KeyCode.X, ref xKeyWasDown)", freeFly,
                "лазер X переключается на ненадёжном фронте (P1-13)");
            StringAssert.Contains("private bool zKeyWasDown;", freeFly, "нет защёлки для Z (P1-13)");
            StringAssert.Contains("selectedRobot = candidate;", freeFly,
                "выбор кликом не попадает в поле класса — публичное свойство остаётся null (P1-14)");
            StringAssert.DoesNotContain("RobotController selectedRobot = FindActiveRobot();", freeFly,
                "вернулась локальная переменная, затеняющая поле (P1-14)");

            string selector = ReadScript("Assets/_Project/01_Scripts/Integration/RobotSelector.cs");
            StringAssert.Contains("[Obsolete(", selector,
                "второй способ выбора робота не помечен устаревшим (P1-15)");
            Assert.IsFalse(Regex.IsMatch(selector, @"robots\[activeIndex\]\.SetActive"),
                "RobotSelector снова пишет SetActive — два источника истины вернулись (P1-15)");
            StringAssert.Contains("if (Instance == this) Instance = null;", selector,
                "статическая ссылка не сбрасывается — «фальшиво-null» объект остаётся в Instance (P1-15)");
        }

        // ================================================================ P1-3

        /// <summary>
        /// P1-3: «ЧИСЛО РЯДОВ ТУЛБАРА» ИЗ НАСТРОЕК ДОХОДИТ ДО СБОРКИ ТУЛБАРА.
        ///
        /// ЧТО БЫЛО: значение сохранялось (clamp 1…6), а в `toolbar.Build` уходила константа 4.
        /// </summary>
        [Test]
        public void P1_3_ToolbarRows_AreApplied()
        {
            string ui = ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs");
            StringAssert.Contains("KvSettings.ToolbarRows", ui,
                "число рядов тулбара из настроек не используется (P1-3)");
            StringAssert.Contains("KvSettings.ToolbarLayout, toolbarRows, KvSettings.ToolbarCompact", ui,
                "в сборку тулбара уходит не сохранённое число рядов (P1-3)");
        }

        // ================================================================ P4-1 / P4-2: тесты не глушат себя

        /// <summary>
        /// P4-1/P4-2: В НАБОРЕ НЕТ САМОВЫКЛЮЧАЮЩИХСЯ ТЕСТОВ.
        ///
        /// ЧТО БЫЛО. Шесть `Assert.Pass` (NUnit считает их ПРОЙДЕННЫМИ) и один `Assert.Ignore` —
        /// то есть часть набора могла «зеленеть», ничего не проверяя: переименовали файл или
        /// метод, а прогон этого не видел (урок E-16).
        ///
        /// Проверяются ИСХОДНИКИ ТЕСТОВ (комментарии вырезаются, чтобы слово в объяснении не
        /// считалось вызовом), и есть страховка: файлов тестов должно быть больше полусотни —
        /// иначе обход ничего не проверил.
        /// </summary>
        [Test]
        public void P4_1_2_NoSelfDisablingTests()
        {
            string testsRoot = Path.Combine(ProjectRoot, "Assets", "_Project", "08_Tests");
            Assert.IsTrue(Directory.Exists(testsRoot), "нет каталога тестов — проверка потеряла смысл");

            string[] files = Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 50,
                "файлов тестов найдено " + files.Length + " — обход почти ничего не проверил (P4-2)");

            var offenders = new System.Collections.Generic.List<string>();
            foreach (string file in files)
            {
                // САМ ЭТОТ ФАЙЛ ИСКЛЮЧЁН: в нём лежит образец запрещённых вызовов внутри
                // регулярного выражения проверки — иначе политика ловила бы собственный шаблон.
                if (string.Equals(Path.GetFileName(file), "KvP1FixTests.cs",
                        System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string code = StripComments(File.ReadAllText(file));
                if (!Regex.IsMatch(code, @"Assert\.(Pass|Ignore)\s*\(|\[Ignore")) continue;
                offenders.Add(Path.GetFileName(file));
            }

            Assert.AreEqual(0, offenders.Count,
                "в наборе снова есть самовыключающиеся тесты (" + string.Join(", ", offenders.ToArray()) +
                "): Assert.Pass/Assert.Ignore/[Ignore] дают «зелёный» прогон, ничего не проверив (P4-1/P4-2)");
        }

        // ================================================================ P2-6/P2-7/P3-11/P3-12: правки поведения

        /// <summary>
        /// P2-6/P2-7/P3-11/P3-12 — ЧЕТЫРЕ ПРАВКИ, КОТОРЫЕ НЕЛЬЗЯ ПРОВЕРИТЬ БЕЗ СЦЕНЫ, НО МОЖНО
        /// ЗАФИКСИРОВАТЬ ПРАВИЛОМ (каждая проверка — на непустоту файла, чтобы не быть вакуумной):
        ///   P2-6  — луч AR не выделяет `List` на каждый кадр;
        ///   P2-7  — подсветка робота не создаёт копию материала и не пишет в ассет;
        ///   P3-11 — нулевой путь не кастует луч нулевой длины;
        ///   P3-12 — незаполненный сустав не роняет измерение.
        /// </summary>
        [Test]
        public void P2_6_P2_7_P3_11_P3_12_BehaviorRules()
        {
            string placement = StripComments(ReadScript("Assets/_Project/01_Scripts/Ar/KvArPlacement.cs"));
            Assert.Greater(placement.Length, 5000, "файл AR-постановки подозрительно мал");
            StringAssert.Contains("raycastHits", placement, "буфер лучей не переиспользуется каждый кадр (P2-6)");
            Assert.AreEqual(1, Regex.Matches(placement, @"new List<ARRaycastHit>\(\)").Count,
                "лучи AR снова выделяют список вне объявления поля — мусор каждый кадр (P2-6)");

            string robot = StripComments(ReadScript("Assets/_Project/01_Scripts/Core/RobotController.cs"));
            Assert.IsFalse(Regex.IsMatch(robot, @"renderer\.material"),
                "подсветка робота снова через `renderer.material` — копия материала на каждый рендерер " +
                "и запись цвета прямо в ассет (P2-7/P4-15)");
            StringAssert.Contains("SetPropertyBlock", robot, "подсветка не использует MaterialPropertyBlock (P2-7)");

            string guard = StripComments(ReadScript("Assets/_Project/01_Scripts/Integration/CollisionGuard.cs"));
            StringAssert.Contains("distance < 1e-4f", guard,
                "предохранитель не отсекает нулевой путь — луч нулевой длины пишет ошибку и «можно» (P3-11)");
            Assert.IsFalse(guard.Contains("direction.normalized"),
                "в предохранителе остался нормированный нулевой вектор (P3-11)");

            string validator = StripComments(ReadScript("Assets/_Project/01_Scripts/Trajectory/PoseValidator.cs"));
            StringAssert.Contains("joints[i] == null", validator,
                "измерение сустава снова падает на незаполненном суставе (P3-12)");
            string ui = StripComments(ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs"));
            StringAssert.Contains("f.Validator.Upper == null", ui,
                "панель свойств читает Upper без проверки длины — падение при отрисовке (P3-12)");
        }
    }
}

