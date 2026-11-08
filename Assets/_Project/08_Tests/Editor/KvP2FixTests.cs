using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using KazistovVvFeatures;
using KazistovVvUI;
using KazistovVvXr;
using NUnit.Framework;
using TrajectoryCore;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ ИСПРАВЛЕНИЙ P2 (11.11.2026) — «КАДРОВЫЙ БЮДЖЕТ VR (72 Гц)».
    ///
    /// ОБЩЕЕ ПРАВИЛО ЭТОГО ФАЙЛА то же, что у `KvP1FixTests`: каждый тест обязан ПАДАТЬ на старом
    /// коде. Там, где логику можно оторвать от сцены, она вынесена в ЧИСТУЮ функцию и
    /// проверяется по значению (`RobotSceneCache.ShouldRebuild`, `KvXrWalkRules.EnabledForAnswer`,
    /// `KvXrTuning.FileChangedSince`, `KvHaptics.IsScanStale`, `KvMenuBar.MenuFingerprint`).
    /// Там, где предмет дефекта — САМ ФАКТ обращения к сцене/`PlayerPrefs` в кадре (его значение
    /// наблюдаемо, а не результат), проверяется правило по исходнику — и обязательно с
    /// невакуумной страховкой: файл прочитан, текст достаточно длинный, комментарии вырезаны
    /// (иначе слово `FindObjectsByType` в объяснении «что было» считалось бы вызовом).
    ///
    /// ПОЧЕМУ БЮДЖЕТ КАДРА ВООБЩЕ ПРОВЕРЯЕТСЯ ТЕСТОМ. 72 Гц — это 13,9 мс на кадр. Каждый обход
    /// сцены, каждое чтение `PlayerPrefs` и каждая пересборка канваса в кадре отбирают у рендера
    /// часть этого бюджета; на шлеме это не «медленно», а «укачивает». Поэтому «в кадре нет
    /// обхода сцены» — это требование безопасности оператора, а не стилистика.
    /// </summary>
        [Category(KvTestCategories.Fast)]
    public class KvP2FixTests
    {
        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        private static string ReadScript(string relativePath)
        {
            string path = Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "нет файла " + path);
            string text = File.ReadAllText(path);
            Assert.Greater(text.Length, 200, "файл " + relativePath + " подозрительно пуст — проверка потеряла смысл");
            return text;
        }

        /// <summary>
        /// ВЫРЕЗАТЬ КОММЕНТАРИИ. Без этого шапка «ЧТО БЫЛО» с упоминанием `FindObjectsByType`
        /// или `PlayerPrefs` сама попадает под проверку и тест ловит собственное объяснение
        /// (это уже случалось на прогоне 11.11.2026, см. `KvP1FixTests.StripComments`).
        /// </summary>
        private static string StripComments(string source)
        {
            string withoutBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(withoutBlock, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }

        /// <summary>
        /// ТЕЛО МЕТОДА по его подписи: от подписи до ПАРНОЙ закрывающей скобки (считаем вложенность).
        /// Нужно там, где важно, ЧТО именно внутри метода, а не во всём файле: рядом могут быть
        /// десятки других обращений к сцене, которые к дефекту не относятся.
        ///
        /// ПОЧЕМУ СЧЁТ СКОБОК, А НЕ «ДО ПЕРВОГО ОТСТУПА 8»: файлы проекта правились разными
        /// инструментами и местами потеряли форматирование, поэтому поиск по отступу может
        /// «уехать» в следующий метод — и проверка станет либо вакуумной, либо ложной.
        /// </summary>
        private static string MethodBody(string source, string signature)
        {
            return MethodBody(source, signature, null);
        }

        /// <summary>
        /// То же, но с ЯКОРЕМ ВНУТРИ ТЕЛА (`mustContain`): если в файле несколько методов с
        /// одинаковой подписью (например, `void Update()` в разных классах одного файла), берётся
        /// тот, внутри которого есть ожидаемый вызов. Без якоря проверка могла бы достать ЧУЖОЕ
        /// тело и пройти вакуумно.
        /// </summary>
        private static string MethodBody(string source, string signature, string mustContain)
        {
            int index = 0;
            while (true)
            {
                int start = source.IndexOf(signature, index, System.StringComparison.Ordinal);
                if (start < 0) break;

                int open = source.IndexOf('{', start);
                int depth = 0;
                int end = -1;
                for (int i = open >= 0 ? open : start; i < source.Length && open >= 0; i++)
                {
                    if (source[i] == '{') depth++;
                    else if (source[i] == '}')
                    {
                        depth--;
                        if (depth == 0) { end = i; break; }
                    }
                }

                if (end > start)
                {
                    string candidate = source.Substring(start, end - start + 1);
                    if (mustContain == null)
                    {
                        Assert.Greater(candidate.Length, signature.Length + 20,
                            "тело метода «" + signature + "» подозрительно короткое — разбор сломался");
                        return candidate;
                    }
                    if (candidate.IndexOf(mustContain, System.StringComparison.Ordinal) >= 0) return candidate;
                }

                index = start + signature.Length;
            }

            Assert.Fail("не найдено тело метода «" + signature + "»" +
                        (mustContain == null ? "" : " с вызовом «" + mustContain + "»") + " — тест ослеп");
            return null;
        }

        // ================================================================= СТРАХОВКА ПОМОЩНИКОВ

        /// <summary>
        /// ПРОВЕРКА САМИХ ПОМОЩНИКОВ — ОТ ТОЙ ЖЕ БОЛЕЗНИ, ЧТО ПОЙМАЛ ПРОГОН 11.11.2026.
        ///
        /// ЧТО СЛУЧИЛОСЬ. Одиннадцать тестов этого файла покраснели на ЗАВЕДОМО ИСПРАВЛЕННОМ коде:
        /// в части вызовов `StringAssert` аргументы стояли наоборот — первым шёл ИСХОДНИК, вторым
        /// ожидаемый фрагмент. `StringAssert.Contains(expected, actual)` в этом случае спрашивает
        /// «содержит ли идентификатор весь файл целиком» и падает ВСЕГДА. Хуже того: та же путаница
        /// в `DoesNotContain` давала бы ЛОЖНО ЗЕЛЁНЫЙ результат — то есть тихо выключила бы
        /// предохранитель, ради которого тест написан.
        ///
        /// ЗАЧЕМ ЭТОТ ТЕСТ. Он проверяет, что помощники возвращают РЕАЛЬНЫЙ текст: файл читается,
        /// комментарии вырезаются (и только они!), тело метода вырезается ровно по своей паре
        /// скобок. Если помощник однажды начнёт отдавать пустоту, падение будет здесь и с понятной
        /// причиной, а не в виде одиннадцати «дефектов», которых нет.
        ///
        /// ЭТО НЕ ВЫКЛЮЧЕНИЕ ПРОВЕРОК: тесты задач остаются как были и по-прежнему краснеют на
        /// старом коде — здесь проверяется инструмент, которым они пользуются.
        /// </summary>
        [Test]
        public void P2_0_TestHelpers_ReadRealSources()
        {
            string raw = ReadScript("Assets/_Project/01_Scripts/Trajectory/TrajectoryTube.cs");
            StringAssert.Contains("class TrajectoryTube", raw,
                "ReadScript вернул не тот файл — все проверки по исходникам ослепли");

            const string sample = "код(); // комментарий с FindObjectsByType\nещё();\n";
            string stripped = StripComments(sample);
            StringAssert.Contains("код();", stripped, "StripComments съел код первой строки");
            StringAssert.Contains("ещё();", stripped, "StripComments съел вторую строку целиком");
            StringAssert.DoesNotContain("FindObjectsByType", stripped,
                "StripComments не вырезал комментарий — тогда проверка «не содержит» ложно красная " +
                "на собственном объяснении, а «содержит» — ложно зелёная");

            // Тело метода берётся ровно своё: соседний метод попасть в него не должен.
            const string twoMethods = "void Foo(int a)\n{\n    Bar(a);\n    Baz(a);\n}\nvoid Next() { }\n";
            string body = MethodBody(twoMethods, "void Foo(int a)");
            StringAssert.Contains("Bar(a);", body, "MethodBody не вернул тело метода — проверки по телам ослепли");
            StringAssert.DoesNotContain("Next", body,
                "MethodBody захватил соседний метод: проверка «в кадровом методе нет обхода сцены» " +
                "смотрела бы на чужой код");
        }

        // ================================================================= P2-1 / P2-2: кэш роботов

        /// <summary>
        /// P2-1: РЕШЕНИЕ «ПЕРЕСОБИРАТЬ ЛИ СПИСОК» — ЧИСТАЯ ФУНКЦИЯ.
        ///
        /// ЧТО БЫЛО. Список роботов добывался двумя полными обходами сцены в КАЖДОМ кадре
        /// (`UpdateAim → ResolveSelectedRobot` и `HoldAllRobots → RefreshRobotList`), кэша не было
        /// вовсе — значит и решения «пора обновить» не требовалось.
        ///
        /// Правила, которые обязаны держаться: нет кэша — собираем; возраст дошёл до предела —
        /// собираем; время ушло назад (новый прогон) — собираем; иначе — НЕ собираем.
        /// </summary>
        [Test]
        public void P2_1_RobotCache_RebuildDecision()
        {
            // ВНИМАНИЕ К АРГУМЕНТАМ (поймано прогоном 11.11.2026): здесь были ОДИН И ТЕ ЖЕ числа
            // (10; 9,5) и для «просрочен», и для «свежий» — возраст 0,5 с при пределе 1 с, то есть
            // утверждение «кэш старше предела» стояло на свежем кэше и падало ВСЕГДА. Случаи
            // обязаны различаться ровно возрастом записи: 2 с против 0,5 с при пределе 1 с.
            Assert.IsTrue(RobotSceneCache.ShouldRebuild(10f, 0f, false, 1f),
                "без кэша список обязан собираться");
            Assert.IsTrue(RobotSceneCache.ShouldRebuild(10f, 8f, true, 1f),
                "кэш возрастом 2 с при пределе 1 с обязан пересобираться");
            Assert.IsTrue(RobotSceneCache.ShouldRebuild(0.2f, 9.5f, true, 1f),
                "время ушло назад (новый прогон) — кэш обязан пересобираться");
            Assert.IsFalse(RobotSceneCache.ShouldRebuild(10f, 9.5f, true, 1f),
                "свежий кэш (возраст 0,5 с при пределе 1 с) пересобирать нельзя: это и есть обход сцены в кадре");
            Assert.IsFalse(RobotSceneCache.ShouldRebuild(10.1f, 10f, true, 1f),
                "0,1 с после сборки — пересборки быть не должно");
            Assert.IsTrue(RobotSceneCache.ShouldRebuild(float.NaN, 10f, true, 1f),
                "мусорное время обязано приводить к пересборке, а не к вечно «свежему» кэшу");
        }

        /// <summary>
        /// P2-1/P2-2: ИНВАЛИДАЦИЯ ЕСТЬ И РАБОТАЕТ. Кэш без инвалидации — не решение: оператор
        /// импортировал робота, а интерфейс обязан увидеть его сразу.
        /// </summary>
        [Test]
        public void P2_1_RobotCache_InvalidateForcesRebuild()
        {
            RobotSceneCache.Rebuild();
            Assert.IsTrue(RobotSceneCache.IsFresh, "только что собранный кэш обязан считаться свежим");
            int versionAfterRebuild = RobotSceneCache.Version;

            RobotSceneCache.Invalidate();
            Assert.IsFalse(RobotSceneCache.IsFresh, "после Invalidate кэш обязан считаться устаревшим");

            RobotSceneCache.Rebuild();
            Assert.IsTrue(RobotSceneCache.IsFresh);
            Assert.Greater(RobotSceneCache.Version, versionAfterRebuild,
                "пересборка обязана поднимать номер версии — иначе по нему нельзя понять, что кэш обновился");

            // Список отдаётся и в трёх видах: все / только активные / без служебных копий.
            Assert.GreaterOrEqual(RobotSceneCache.All.Count, RobotSceneCache.Visible.Count,
                "список «без фантомов» не может быть больше полного");
            Assert.GreaterOrEqual(RobotSceneCache.All.Count, RobotSceneCache.Active.Count,
                "список активных не может быть больше полного");
        }

        /// <summary>
        /// P2-1: В ПОТОКЕ ТРАЕКТОРИЙ БОЛЬШЕ НЕТ ОБХОДА СЦЕНЫ ЗА КАДР.
        ///
        /// На старом коде проверка падала: в файле было два `FindObjectsByType<RobotController>`.
        /// </summary>
        [Test]
        public void P2_1_TrajectoryFlow_UsesRobotCache()
        {
            string source = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Trajectory/TrajectoryFlowController.cs"));

            int scans = Regex.Matches(source, @"FindObjectsByType\s*<\s*RobotController\s*>").Count;
            Assert.AreEqual(0, scans,
                "обходов сцены за роботами: " + scans + " — список обязан браться из RobotSceneCache (P2-1)");

            int cacheUses = Regex.Matches(source, @"RobotSceneCache\.").Count;
            Assert.GreaterOrEqual(cacheUses, 2,
                "обращений к кэшу роботов: " + cacheUses + " — ожидались минимум два (P2-1): " +
                "список для «у всех нет цели» и выбор робота прицелом");
        }

        /// <summary>
        /// P2-2: ОБОЛОЧКА НЕ ИЩЕТ РОБОТОВ В СЦЕНЕ КАЖДЫЙ КАДР.
        ///
        /// На старом коде в теле `TrackActiveRobotForProperties` стоял
        /// `FindObjectsByType<RobotController>(FindObjectsInactive.Include)` — обход сцены из `Update`.
        /// </summary>
        [Test]
        public void P2_2_UiManager_TrackActiveRobot_UsesCache()
        {
            string source = StripComments(
                ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs"));

            string body = MethodBody(source, "private void TrackActiveRobotForProperties()");
            StringAssert.DoesNotContain("FindObjectsByType", body,
                "в кадровом методе оболочки остался обход сцены — панель свойств обходит сцену каждый кадр (P2-2)");
            StringAssert.Contains("RobotSceneCache", body,
                "кадровый метод обязан брать роботов из кэша (P2-2)");
        }

        // ================================================================= P2-3: локомоция

        /// <summary>P2-3: ПРАВИЛО «НУЖНЫ ЛИ ПРАВИЛА» — чистая функция (без обращения к OpenXR).</summary>
        [Test]
        public void P2_3_WalkRules_EnabledForAnswer()
        {
            // MR (сквозной вид есть) — правила ходьбы выключены: комната 1:1, зал скрыт.
            Assert.IsFalse(KvXrWalkRules.EnabledForAnswer(KvXrCapabilities.Answer.Yes),
                "при работающем сквозном виде правила зала обязаны быть выключены");

            // «Нет» и «спросить не удалось» — правила включены: сломать ходьбу хуже, чем ошибиться в MR.
            Assert.IsTrue(KvXrWalkRules.EnabledForAnswer(KvXrCapabilities.Answer.No));
            Assert.IsTrue(KvXrWalkRules.EnabledForAnswer(KvXrCapabilities.Answer.Unknown),
                "«не удалось спросить» не повод отключать правила: оператор остался бы без границ зала");
        }

        /// <summary>
        /// P2-3: РЕЖИМ СПРАШИВАЕТСЯ У ПЛАТФОРМЫ ОДИН РАЗ, А НЕ В КАЖДОМ КАДРЕ ХОДА.
        ///
        /// На старом коде геттер `EnabledInCurrentMode` на КАЖДОЕ обращение вызывал
        /// `KvXrCapabilities.Passthrough()` → `OpenXRRuntime.GetEnabledExtensions()` с новым
        /// массивом строк; счётчика обращений не было вовсе.
        /// </summary>
        [Test]
        public void P2_3_WalkRules_ProbeIsCached()
        {
            KvXrWalkRules.Invalidate();
            int before = KvXrWalkRules.ProbeCount;

            bool first = KvXrWalkRules.EnabledInCurrentMode;
            bool second = KvXrWalkRules.EnabledInCurrentMode;
            bool third = KvXrWalkRules.EnabledInCurrentMode;

            Assert.AreEqual(first, second, "ответ пробы не может меняться от чтения к чтению");
            Assert.AreEqual(first, third);
            Assert.AreEqual(before + 1, KvXrWalkRules.ProbeCount,
                "режим спрошен у платформы " + (KvXrWalkRules.ProbeCount - before) +
                " раз(а) на три чтения геттера — в кадре 72 Гц это нативный вызов и новый string[] (P2-3)");

            KvXrWalkRules.Invalidate();
            bool afterInvalidate = KvXrWalkRules.EnabledInCurrentMode;
            Assert.AreEqual(before + 2, KvXrWalkRules.ProbeCount,
                "после Invalidate режим обязан спроситься заново — иначе кэш переживёт смену VR/MR");
            Assert.AreEqual(first, afterInvalidate, "в редакторе ответ пробы стабилен (XR не поднят)");
        }

        /// <summary>
        /// P2-3: В ЛОКОМОЦИИ НЕТ ЧТЕНИЯ НАСТРОЕК В КАДРЕ. На старом коде `HandleSmooth` трижды за
        /// кадр читал `PlayerPrefs` (`GetWalkDeadzone`, `GetSmoothSpeed`, `GetMoveAcceleration`),
        /// а `HandleTeleport` — `GetTeleportDistance`.
        /// </summary>
        [Test]
        public void P2_3_Locomotion_HasNoSettingsReadsInFrame()
        {
            string source = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Xr/KvXrLocomotion.cs"));

            string smooth = MethodBody(source, "private void HandleSmooth(");
            StringAssert.DoesNotContain("KvXrSettings.", smooth,
                "в кадровом методе хода осталось чтение настроек (PlayerPrefs) — это P2-3");
            StringAssert.Contains("cached", smooth,
                "кадровый метод обязан брать настройки из кэша (P2-3)");

            int prefsReads = Regex.Matches(source, @"PlayerPrefs\.Get(Float|Int|String)\(").Count;
            Assert.AreEqual(0, prefsReads,
                "чтений PlayerPrefs в файле локомоции: " + prefsReads +
                " — настройки обязаны сниматься по событию (`ReloadMovementSettings`)");
            // ПОРЯДОК АРГУМЕНТОВ `StringAssert.Contains(expected, actual)` — ПЕРВЫЙ ОЖИДАЕМЫЙ
            // ФРАГМЕНТ, ВТОРОЙ ИСХОДНИК (поймано прогоном 11.11.2026: здесь стояли они наоборот,
            // и проверка спрашивала «содержит ли идентификатор весь файл целиком» — то есть
            // падала всегда, независимо от кода).
            StringAssert.Contains("ReloadMovementSettings", source,
                "кэш настроек перемещения обязан иметь точку перечитывания (P2-3, правило 6 задания)");
        }

        // ================================================================= P2-4 / P2-5: панели

        /// <summary>
        /// P2-4: УКАЗАТЕЛЬ НЕ ИЩЕТ ПАНЕЛЬ ПО ВСЕЙ СЦЕНЕ В `Update`. На старом коде там стояло
        /// `if (groupPanel == null) groupPanel = FindAnyObjectByType&lt;KvXrGroupPanel&gt;();`.
        /// </summary>
        [Test]
        public void P2_4_Pointer_UsesPanelRegistry()
        {
            string source = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Xr/KvXrUiPointer.cs"));

            int sceneSearches = Regex.Matches(source, @"FindAnyObjectByType\s*<\s*KvXrGroupPanel\s*>").Count;
            Assert.AreEqual(0, sceneSearches,
                "поисков панели по сцене: " + sceneSearches + " — панель берётся из реестра (P2-4)");

            StringAssert.Contains("KvXrPanels.HandPanel", source,
                "панель обязана браться из реестра `KvXrPanels` (P2-4)");
        }

        /// <summary>
        /// P2-5: ПАНЕЛЬ ГРУПП НЕ ЧИТАЕТ `PlayerPrefs` В КАДРЕ. На старом коде `Update` звал
        /// `KvXrSettings.GetQuickPanelVisible()` и `GetQuickPanelHand()` каждый кадр.
        /// </summary>
        [Test]
        public void P2_5_GroupPanel_CachesSettings()
        {
            string source = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Xr/KvXrGroupPanel.cs"));

            int visibleReads = Regex.Matches(source, @"GetQuickPanelVisible\(\)").Count;
            Assert.AreEqual(1, visibleReads,
                "чтений «панель видна» в файле: " + visibleReads +
                " — ожидалось ровно одно (внутри ReloadPanelSettings), иначе панель снова читает " +
                "PlayerPrefs в кадре (P2-5)");

            string body = MethodBody(source, "void Update()", "ReloadPanelSettings");
            StringAssert.DoesNotContain("KvXrSettings.", body,
                "в кадровом методе панели осталось чтение настроек (P2-5)");
            StringAssert.Contains("cachedQuickPanelVisible", body,
                "кадровый метод обязан брать видимость панели из кэша (P2-5)");
        }

        // ================================================================= P2-8: PostureControl

        /// <summary>
        /// P2-8: `PostureControl` ПОМЕЧЕН `[Obsolete]` С ПРИЧИНОЙ. ЧТО БЫЛО: класс создавался, но
        /// `Step()` не вызывался ниоткуда — «уход от лимитов» был обещан описанием и не работал,
        /// при этом `Step()` аллоцирует ~10 массивов на вызов, то есть «просто подключить» нельзя.
        /// </summary>
        [Test]
        public void P2_8_PostureControl_IsMarkedObsolete()
        {
            string source = ReadScript("Assets/_Project/01_Scripts/Trajectory/PostureControl.cs");

            int mark = source.IndexOf("[System.Obsolete(", System.StringComparison.Ordinal);
            int cls = source.IndexOf("public class PostureControl", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(mark, 0, "нет метки [System.Obsolete] — класс снова выглядит живым (P2-8)");
            Assert.GreaterOrEqual(cls, 0, "не найден сам класс PostureControl — тест ослеп");
            Assert.Less(mark, cls, "метка обязана стоять ПЕРЕД объявлением класса (P2-8)");

            Match reason = Regex.Match(source, @"\[System\.Obsolete\(""([^""]*)""");
            Assert.IsTrue(reason.Success, "у метки нет текста причины — через месяц её снимут (P2-8)");
            Assert.Greater(reason.Groups[1].Value.Trim().Length, 20,
                "причина короче 20 символов: «" + reason.Groups[1].Value + "»");
            StringAssert.Contains("P2-8", reason.Groups[1].Value,
                "в причине обязан быть номер задачи, иначе не найти, почему класс помечен");

            // Класс НЕ удалён: правило проекта — сначала метка, удаление отдельным решением.
            StringAssert.Contains("public double[] Step(", source,
                "метод Step() удалён — правило «не удаляй молча» нарушено (P2-8)");
        }

        // ================================================================= P2-9: утечки

        /// <summary>
        /// P2-9: НАТИВНЫЕ РЕСУРСЫ ОСВОБОЖДАЮТСЯ. ЧТО БЫЛО: у каждого из перечисленных файлов
        /// нативный объект (меш, материал, словарь со ссылками на сцену, `WebCamTexture`) жил
        /// вечно — сборщик мусора C# такие объекты не считает.
        ///
        /// Проверка по исходнику: у каждого файла обязана быть ТОЧКА ОСВОБОЖДЕНИЯ или СБРОСА.
        /// </summary>
        [Test]
        public void P2_9_NativeResources_AreReleased()
        {
            // 1. «Колбаска»: меш переиспользуется (а не `new Mesh` на каждую генерацию), созданные
            //    материалы учтены и уничтожаются в OnDestroy.
            string tube = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Trajectory/TrajectoryTube.cs"));
            StringAssert.Contains("private void OnDestroy()", tube,
                "у «колбаски» нет OnDestroy — меш и материалы не освобождаются (P2-9)");
            StringAssert.Contains("tubeMesh.Clear()", tube,
                "меш «колбаски» не переиспользуется — значит создаётся новый на каждую генерацию (P2-9)");
            StringAssert.Contains("ownedMaterials", tube,
                "созданные материалы не учтены — их нечем уничтожить (P2-9)");
            Assert.IsFalse(Regex.IsMatch(tube, @"tubeRenderer\.material\s*="),
                "`tubeRenderer.material = …` создаёт копию материала на каждый рендерер (P2-9)");

            // 2. Реестр прокси столкновений: статические словари сбрасываются на старте прогона.
            string collision = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Trajectory/CollisionWorld.cs"));
            StringAssert.Contains("RuntimeInitializeOnLoadMethod", collision,
                "статический реестр прокси не сбрасывается между прогонами (P2-9)");
            StringAssert.Contains("SubsystemRegistration", collision,
                "сброс статики обязан происходить до первого Awake сцены (SubsystemRegistration)");

            // 3. Материал столов: статический кэш сбрасывается, наш материал — уничтожается.
            string stands = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Trajectory/StandBuilder.cs"));
            StringAssert.Contains("tableMaterialIsOurs", stands,
                "не различается «материал сцены» и «наш созданный» — уничтожать их одинаково нельзя (P2-9)");

            // 4. Камера AR: WebCamTexture не только останавливается, но и уничтожается.
            string gate = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Ar/KvArCameraGate.cs"));
            Assert.IsTrue(Regex.IsMatch(gate, @"Destroy(Immediate)?\(webCam\)"),
                "WebCamTexture только останавливается, но не уничтожается — камера остаётся занятой (P2-9)");

            // 5. Плоскости AR: статический меш заливки освобождается.
            string planes = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Ar/KvArPlaneVisualizer.cs"));
            StringAssert.Contains("DestroyQuadMesh()", planes,
                "статический меш заливки не уничтожается (P2-9)");

            // 6. Запись траектории: предел точек, создание каталога и защита записи файла.
            string recorder = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Recording/TrajectoryRecorder.cs"));
            StringAssert.Contains("MaxPoints", recorder,
                "список точек записи не ограничен — длинная запись съест память шлема (P2-9)");
            StringAssert.Contains("Directory.CreateDirectory", recorder,
                "каталог записи не создаётся — запись упадёт на исключении (P2-9)");
            StringAssert.Contains("catch (System.Exception e)", recorder,
                "запись файла без обработки отказа: траектория теряется молча (P2-9)");

            // 7. Реестр узлов UI: ссылки на объекты сцены не переживают смену сцены/прогон.
            string registry = StripComments(
                ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Data/RuntimeRegistry.cs"));
            StringAssert.Contains("SubsystemRegistration", registry,
                "статический список узлов не сбрасывается на старте прогона (P2-9)");
            StringAssert.Contains("sceneUnloaded", registry,
                "список узлов не сбрасывается при выгрузке сцены — остаются ссылки на мёртвые объекты (P2-9)");
        }

        // ================================================================= P2-10: пересборка UI

        /// <summary>
        /// P2-10: ПЕРЕСБОРКА UI ТОЛЬКО ПРИ ИЗМЕНЕНИИ. ЧТО БЫЛО: оболочка зовёт `Refresh` десять раз
        /// в секунду, и каждый вызов пересобирал выпадающее меню, склеивал тексты подсказок,
        /// красил «скелетон» (перестройка канваса 72 раза в секунду) и строил список видимых узлов.
        ///
        /// Проверяется наличие РЕШЕНИЯ (сравнение отпечатка/кэш), а не отсутствие вызова: вызов
        /// остаётся, исчезает работа при неизменном состоянии.
        /// </summary>
        [Test]
        public void P2_10_UiRebuild_OnlyOnChange()
        {
            string menu = StripComments(
                ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Zones/KvMenuBar.cs"));
            StringAssert.Contains("MenuFingerprint", menu,
                "меню пересобирается без сравнения состояния — Destroy+Instantiate десять раз в секунду (P2-10)");
            StringAssert.Contains("lastMenuFingerprint", menu,
                "отпечаток меню не запоминается — сравнивать не с чем (P2-10)");

            string toolbar = StripComments(
                ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Zones/KvToolbar.cs"));
            StringAssert.Contains("tooltipBodies", toolbar,
                "тексты подсказок тулбара считаются заново в каждом `Refresh` (P2-10)");

            string properties = StripComments(
                ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Zones/KvPropertiesView.cs"));
            StringAssert.Contains("SkeletonUpdateIntervalSeconds", properties,
                "пульсация «скелетона» идёт в каждом кадре — канвас перестраивается 72 раза в секунду (P2-10)");

            string tree = StripComments(
                ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Zones/KvTreeView.cs"));
            StringAssert.Contains("visibleNodesDirty", tree,
                "список видимых узлов строится с нуля в каждом кадре навигации (P2-10)");

            string states = StripComments(
                ReadScript("Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KvUiStates.cs"));
            StringAssert.Contains("instance.loading &&", states,
                "повторный Begin ничего не проверяет — `SetAsLastSibling` в кадре перестраивает канвас (P2-10)");
        }

        /// <summary>
        /// P2-10: ОТПЕЧАТОК МЕНЮ — ЧИСТАЯ ФУНКЦИЯ: стабилен и различает меню. Если бы отпечаток
        /// зависел от случайности/времени, меню пересобиралось бы на каждом вызове.
        /// </summary>
        [Test]
        public void P2_10_MenuFingerprint_IsStableAndSpecific()
        {
            string view = KvMenuBar.MenuFingerprint("Вид");
            string viewAgain = KvMenuBar.MenuFingerprint("Вид");
            string file = KvMenuBar.MenuFingerprint("Файл");

            Assert.IsNotNull(view, "отпечаток не может быть null");
            Assert.AreEqual(view, viewAgain,
                "отпечаток одного и того же меню обязан совпадать — иначе пересборка не прекратится никогда");
            Assert.AreNotEqual(view, file,
                "отпечатки разных меню обязаны различаться — иначе меню не перестроится при переключении");
        }

        // ================================================================= P2-11: вибрация

        /// <summary>P2-11: РЕШЕНИЕ «ПОРА ПЕРЕСКАНИРОВАТЬ» — чистая функция.</summary>
        [Test]
        public void P2_11_Haptics_ScanStaleRule()
        {
            Assert.IsTrue(KvHaptics.IsScanStale(10f, float.NegativeInfinity, 2f),
                "первый запуск: сканирования ещё не было — обязано быть «пора»");
            Assert.IsTrue(KvHaptics.IsScanStale(10f, 7f, 2f),
                "прошло 3 с при интервале 2 с — обязано быть «пора»");
            Assert.IsFalse(KvHaptics.IsScanStale(10f, 9f, 2f),
                "прошло 1 с при интервале 2 с — сканировать нельзя, это обход сцены");
            Assert.IsTrue(KvHaptics.IsScanStale(0.5f, 9f, 2f),
                "время ушло назад (новый прогон) — обязано быть «пора»");
        }

        /// <summary>
        /// P2-11: ГЕТТЕР `IsActive` НЕ ОБХОДИТ СЦЕНУ. На старом коде первое же чтение свойства
        /// запускало `Scan()` (обход ВСЕХ `MonoBehaviour` + `GetMethods()` на каждом) —
        /// наблюдаемо это по времени последнего сканирования.
        /// </summary>
        [Test]
        public void P2_11_Haptics_GetterDoesNotScan()
        {
            FieldInfo scanTime = typeof(KvHaptics).GetField("controllerScanTimer",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(scanTime, "поле времени сканирования исчезло — тест ослеп (P2-11)");

            var haptics = new KvHaptics();
            object before = scanTime.GetValue(haptics);

            bool first = haptics.IsActive;
            bool second = haptics.IsActive;

            Assert.AreEqual(first, second);
            Assert.IsFalse(first, "в тестовой сцене XR-контроллеров нет — IsActive обязан быть false");
            Assert.AreEqual(before, scanTime.GetValue(haptics),
                "чтение свойства перезапустило сканирование сцены — геттер не имеет права обходить сцену (P2-11)");

            string source = StripComments(ReadScript("Assets/_Project/01_Scripts/Features/KvHaptics.cs"));
            int allMonoBehaviours = Regex.Matches(source, @"FindObjectsByType\s*<\s*MonoBehaviour\s*>").Count;
            Assert.AreEqual(0, allMonoBehaviours,
                "обход ВСЕХ MonoBehaviour остался: " + allMonoBehaviours +
                " — искать надо известные типы контроллеров (P2-11)");
            StringAssert.Contains("KnownControllerTypeNames", source,
                "список известных типов контроллеров не заведён — поиск снова станет всеядным (P2-11)");
        }

        // ================================================================= P2-12: TCP-прокси

        /// <summary>
        /// P2-12: «ПРОКСИ НЕ НУЖЕН» ЗАПОМИНАЕТСЯ ФЛАГОМ. На старом коде `CreateTcpProxy` штатно
        /// возвращал `null`, и на следующем кадре (а `ResolveRobotReferences` зовётся из `Update`)
        /// всё повторялось: две `GetComponent` в кадр, которые никогда не завершатся успехом.
        /// </summary>
        [Test]
        public void P2_12_SixAxis_TcpProxyDecisionIsRemembered()
        {
            string source = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Core/SixAxisController.cs"));

            StringAssert.Contains("tcpProxyResolved", source,
                "нет флага «решение о прокси принято» — расчёт повторяется каждый кадр (P2-12)");
            StringAssert.Contains("ResetTcpProxyDecision", source,
                "у решения нет точки инвалидации — после подмены фланца прокси не пересчитается (P2-12)");
            Assert.IsFalse(
                Regex.IsMatch(source, @"if\s*\(\s*tcpProxy\s*==\s*null\s*&&\s*endEffector\s*!=\s*null\s*\)"),
                "осталось прежнее условие `tcpProxy == null && endEffector != null` — это и есть дефект (P2-12)");
        }

        // ================================================================= P2-13: файл подстройки

        /// <summary>
        /// P2-13: ЧТЕНИЕ ФАЙЛА С ДИСКА НЕ КАЖДЫЕ 2 СЕКУНДЫ. ЧТО БЫЛО: `Tick()` раз в 2 с делал
        /// `File.Exists` + `File.ReadAllText` синхронно на главном потоке из `KvXrRig.Update`;
        /// на Android `persistentDataPath` — не быстрая ФС, это «столб» кадра.
        /// </summary>
        [Test]
        public void P2_13_Tuning_ReloadIsRareAndChangeAware()
        {
            Assert.GreaterOrEqual(KvXrTuning.ReloadIntervalSeconds, 5f,
                "интервал перечитывания файла подстройки " + KvXrTuning.ReloadIntervalSeconds +
                " с — слишком частый для главного потока (P2-13: нужно 5–10 с)");
            Assert.LessOrEqual(KvXrTuning.ReloadIntervalSeconds, 10f,
                "интервал больше 10 с — подстройка перестанет быть «на ходу»");

            // Не изменился — не читаем.
            Assert.IsFalse(KvXrTuning.FileChangedSince(1234L, 1234L),
                "тот же момент изменения — файл читать нельзя (P2-13)");
            Assert.IsTrue(KvXrTuning.FileChangedSince(1235L, 1234L),
                "файл изменился — читать обязательно, иначе подстройка не применится");
            Assert.IsTrue(KvXrTuning.FileChangedSince(1234L, -1L),
                "прошлого чтения не было — читаем");
            Assert.IsTrue(KvXrTuning.FileChangedSince(-1L, 1234L),
                "время файла не получено — читаем: лучше прочитать лишний раз, чем пропустить подстройку");

            // Таймер: до срока — не читаем, после — читаем.
            Assert.IsFalse(KvXrTuning.IsTimeToReload(5f, 10f), "до срока читать файл нельзя");
            Assert.IsTrue(KvXrTuning.IsTimeToReload(10f, 10f), "срок наступил — читаем");

            string source = StripComments(ReadScript("Assets/_Project/01_Scripts/Xr/KvXrTuning.cs"));
            StringAssert.Contains("File.GetLastWriteTimeUtc", source,
                "нет проверки «файл не менялся» — читать будет каждый тик (P2-13)");
        }

        // ================================================================= P2-14: один якобиан

        /// <summary>
        /// P2-14: ШАГ ЯКОБИАНА ЗАДАН В ОДНОМ МЕСТЕ. ЧТО БЫЛО: `KinematicsJacobian` считал с шагом
        /// 0,5°, `PoseValidator.Jacobian` — с 0,25°: σ_min и манипулируемость для ОДНОЙ позы
        /// получались разными, а по ним решается «близко к сингулярности».
        /// </summary>
        [Test]
        public void P2_14_Jacobian_HasSingleStep()
        {
            Assert.Greater(KinematicsJacobian.StepDegrees, 0.0,
                "шаг конечных разностей обязан быть положительным");
            Assert.AreEqual(KinematicsJacobian.StepDegrees * Mathf.Deg2Rad, KinematicsJacobian.StepRadians, 1e-9,
                "шаг в радианах обязан вычисляться из StepDegrees, а не задаваться вторым числом (P2-14)");

            string pose = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Trajectory/PoseValidator.cs"));
            string body = MethodBody(pose, "public void Jacobian(double[] q, double[][] jac, out Vector3 tcp)");
            StringAssert.Contains("KinematicsJacobian.Compute", body,
                "`PoseValidator.Jacobian` не использует единственную реализацию — вернулся второй расчёт (P2-14)");
            Assert.IsFalse(Regex.IsMatch(body, @"double\s+h\s*="),
                "в теле `PoseValidator.Jacobian` снова считается свой шаг — дубль расчёта (P2-14)");
            Assert.IsFalse(Regex.IsMatch(pose, @"0\.25\s*\*\s*Mathf\.Deg2Rad"),
                "в файле остался шаг 0,25° — второй источник истины для якобиана (P2-14)");
        }

        /// <summary>
        /// P2-14: У `TubeMath.SegmentSegmentDistance` НЕТ СВОЕЙ РЕАЛИЗАЦИИ. ЧТО БЫЛО: построчная
        /// копия `CollisionWorld.SegmentSegmentDistance`; правка в одной не переносилась в другую,
        /// и выбор траектории лучом расходился с проверкой зазоров.
        /// </summary>
        [Test]
        public void P2_14_TubeMath_DelegatesSegmentDistance()
        {
            string selection = StripComments(
                ReadScript("Assets/_Project/01_Scripts/Trajectory/SelectionTypes.cs"));
            string body = MethodBody(selection,
                "public static float SegmentSegmentDistance(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)");
            StringAssert.Contains("CollisionWorld.SegmentSegmentDistance", body,
                "`TubeMath` не вызывает общую реализацию расстояния между отрезками (P2-14)");

            // ПОВЕДЕНЧЕСКАЯ СТРАХОВКА: обёртка обязана давать ровно те же числа, что реализация.
            Vector3 a1 = new Vector3(0f, 0f, 0f), a2 = new Vector3(1f, 0f, 0f);
            Vector3 b1 = new Vector3(1f, 1f, 0f), b2 = new Vector3(2f, 1f, 0f);
            Assert.AreEqual(CollisionWorld.SegmentSegmentDistance(a1, a2, b1, b2),
                TubeMath.SegmentSegmentDistance(a1, a2, b1, b2), 1e-5f,
                "обёртка и общая реализация дают разные числа — дубль расчёта жив (P2-14)");

            // Пересекающиеся отрезки — ноль; разведённые на 1 м — единица.
            Assert.AreEqual(0f, TubeMath.SegmentSegmentDistance(
                new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, -1f, 0f), new Vector3(0f, 1f, 0f)), 1e-4f,
                "пересекающиеся отрезки обязаны давать нулевое расстояние");
            Assert.AreEqual(1f, TubeMath.SegmentSegmentDistance(
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f)), 1e-4f,
                "параллельные отрезки в 1 м друг от друга обязаны давать 1 м");
        }
    }
}

