// ================================================================================================
//  KvSettingsPersistenceTests.cs — EditMode-тесты ПЕРЕЖИВАНИЯ НАСТРОЕК (сессия S46).
//
//  ОТКУДА ЗАДАЧА. Настройки шлема (`KazistovVvXr.KvXrSettings`) и настройки интерфейса
//  (`KazistovVvUI.KvSettings`) живут в PlayerPrefs — единственном месте, которое переживает
//  перезапуск приложения. Оператор подгоняет под себя режим VR/MR, язык, силу виньетки, масштаб
//  мира и позу панели, и любая потеря этих значений выглядит как «программа забыла мои настройки».
//
//  ЧТО БЫЛО. Сеттеры `KvSettings` писали в хранилище БЕЗ сохранения на диск (P1-1): Unity пишет
//  PlayerPrefs при штатном выходе, поэтому после падения или принудительной остановки часть
//  настроек откатывалась к заводским. Тем же страдал шлем (P1-2): сохранение звали два сеттера
//  из двадцати, и «какая настройка слетела» зависело от того, какой сеттер её записал.
//
//  ЧЕМ ПЛОХО БЫЛО. Наладчик подгонял читаемость интерфейса и позу панели, приложение падало —
//  и после перезапуска половина значений оказывалась прежней, а половина заводской. Хуже:
//  значение, попавшее в хранилище в обход сеттера (или оставшееся от старой версии), читалось
//  БЕЗ зажима — виньетка −1 или масштаб мира 7 доезжали до интерфейса «как есть».
//
//  ЧТО СТАЛО И ЧТО ЗДЕСЬ ЗАКРЕПЛЕНО:
//    1) запись идёт ОДНОЙ точкой (`SetInt`/`SetFloat` + сохранение), чтение только читает;
//    2) чтение шлема ЗАЖИМАЕТ значение в задокументированный коридор, поэтому мусор в хранилище
//       не превращается в «мир увеличен в семь раз»;
//    3) сброс возвращает к умолчанию ВСЕ параметры сразу, а не один;
//    4) сброс не трогает посторонние ключи хранилища.
//
//  ГРАНИЦА ЧЕСТНОСТИ. «Перезапуск» здесь моделируется чтением того же хранилища и
//  `KvSettings.Reload()` — настоящий перезапуск требует живого редактора или шлема и проверяется
//  на устройстве. `KvLoc.Reload()` в тестах сознательно НЕ зовётся: он пересобирает общие словари,
//  на которых стоят другие фикстуры, и тест настроек не имеет права их ронять.
// ================================================================================================

using System.Collections.Generic;
using KazistovVvUI;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Настройки переживают перезапуск, зажимаются при чтении, сбрасываются целиком.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvSettingsPersistenceTests
    {
        // ------------------------------------------------------------------ сохранение окружения
        //
        // ТЕСТ НЕ ИМЕЕТ ПРАВА МЕНЯТЬ НАСТРОЙКИ МАШИНЫ. Хранилище настроек — общее: это те же
        // PlayerPrefs, из которых стартует само приложение. Без сохранения/восстановления прогон
        // оставил бы владельцу включённый MR, чужой язык интерфейса и увеличенный масштаб мира.

        private sealed class PrefsSnapshot
        {
            private readonly Dictionary<string, object> values = new Dictionary<string, object>();

            public void TakeInt(string key)
            {
                values[key] = PlayerPrefs.HasKey(key) ? (object)PlayerPrefs.GetInt(key) : null;
            }

            public void TakeFloat(string key)
            {
                values[key] = PlayerPrefs.HasKey(key) ? (object)PlayerPrefs.GetFloat(key) : null;
            }

            public void TakeString(string key)
            {
                values[key] = PlayerPrefs.HasKey(key) ? (object)PlayerPrefs.GetString(key) : null;
            }

            public void Restore()
            {
                foreach (KeyValuePair<string, object> pair in values)
                {
                    if (pair.Value == null) { PlayerPrefs.DeleteKey(pair.Key); continue; }
                    if (pair.Value is int) PlayerPrefs.SetInt(pair.Key, (int)pair.Value);
                    else if (pair.Value is float) PlayerPrefs.SetFloat(pair.Key, (float)pair.Value);
                    else PlayerPrefs.SetString(pair.Key, (string)pair.Value);
                }
                PlayerPrefs.Save();
            }
        }

        /// <summary>Целочисленные ключи настроек шлема, которые трогают тесты этого файла.</summary>
        private static readonly string[] XrIntKeys =
        {
            KvXrSettings.KeyPassthrough, KvXrSettings.KeyMoveMode, KvXrSettings.KeyVignette,
            KvXrSettings.KeyShowPanel, KvXrSettings.KeyShowWorkZone, KvXrSettings.KeyShowReach,
            KvXrSettings.KeyShowVirtualStands, KvXrSettings.KeyWorkspaceVisible,
            KvXrSettings.KeyJointLimitsVisible, KvXrSettings.KeyQuickPanelVisible,
            KvXrSettings.KeyQuickPanelShape, KvXrSettings.KeyQuickPanelHand,
            KvXrSettings.KeyQuickPanelButtonCount, KvXrSettings.KeyRayHand,
            KvXrSettings.KeyControllerVisuals, KvXrSettings.KeyGestures, KvXrSettings.KeySecondaryRay
        };

        /// <summary>Дробные ключи настроек шлема.</summary>
        private static readonly string[] XrFloatKeys =
        {
            KvXrSettings.KeyVignetteStrength, KvXrSettings.KeySmoothSpeed,
            KvXrSettings.KeyMoveAcceleration, KvXrSettings.KeyWalkDeadzone,
            KvXrSettings.KeyTeleportDistance, KvXrSettings.KeyPanelDistance,
            KvXrSettings.KeyWorldScale, KvXrSettings.KeyOperatorHeight,
            KvXrSettings.KeyQuickPanelRadius, KvXrSettings.KeyGroupPanelRadius
        };

        /// <summary>Ключи настроек интерфейса (галки хранятся целыми 0/1).</summary>
        private static readonly string[] UiIntKeys =
        {
            KvSettings.KeyWorkspace, KvSettings.KeyJointLimits, KvSettings.KeyMetrics,
            KvSettings.KeyFlashlight, KvSettings.KeyAutoRefreshTree, KvSettings.KeyPhantoms,
            KvSettings.KeyDensity, KvSettings.KeyToolbarRows, KvSettings.KeyToolbarLayout,
            KvSettings.KeyToolbarCompact, KvSettings.KeyFontSize, KvSettings.KeyHighContrast,
            KvSettings.KeyColorBlind, KvSettings.KeyKeyboardNav, KvSettings.KeyGamepadHud,
            KvSettings.KeyErrorPlate
        };

        private PrefsSnapshot snapshot;

        /// <summary>
        /// ВЫБРАННЫЙ ЯЗЫК ДО ТЕСТА. Хранилище восстановить мало: `KvLoc` держит применённый язык
        /// в статическом поле, и без обратного переключения следующие фикстуры увидели бы «ja» —
        /// хотя оператор ничего не выбирал.
        /// </summary>
        private string originalLanguagePreference;

        [SetUp]
        public void SaveEnvironment()
        {
            snapshot = new PrefsSnapshot();
            foreach (string key in XrIntKeys) snapshot.TakeInt(key);
            foreach (string key in XrFloatKeys) snapshot.TakeFloat(key);
            foreach (string key in UiIntKeys) snapshot.TakeInt(key);
            snapshot.TakeFloat(KvSettings.KeyUiScale);
            snapshot.TakeString(KvLoc.PrefsKey);

            originalLanguagePreference = PlayerPrefs.HasKey(KvLoc.PrefsKey)
                ? PlayerPrefs.GetString(KvLoc.PrefsKey)
                : KvLoc.SystemCode;
        }

        [TearDown]
        public void RestoreEnvironment()
        {
            snapshot.Restore();
            // Кэш `KvSettings` живёт в статических полях: без перечитывания следующие тесты
            // увидели бы значения ЭТОГО теста, хотя хранилище уже восстановлено.
            KvSettings.Reload();
            // Применённый язык — тоже общее состояние: возвращаем то, что было до теста.
            KvLoc.SetLanguage(originalLanguagePreference);
        }

        // ==================================================== 1. РЕЖИМ VR / MR ПЕРЕЖИВАЕТ

        /// <summary>
        /// РЕЖИМ VR/MR: ЗАПИСАЛИ — ПРОЧИТАЛИ — СОВПАЛО, и запись легла в ХРАНИЛИЩЕ (а не в поле объекта).
        ///
        /// ЧТО БЫЛО. У режима два входа: переключатель «VR/MR» и прежняя галка «режим комнаты (MR)».
        /// Они обязаны писать ОДНО значение — `KeyPassthrough`. Пока это было не так, выбор «MR»
        /// в панели не включал ветку сквозного вида, и приложение оставалось в VR.
        ///
        /// ЧЕМ ПЛОХО. Оператор выбирает MR, видит «включено», перезапускает приложение — и снова VR.
        /// Понять, что именно произошло, нельзя: оба входа выглядят рабочими.
        ///
        /// ЧТО СТАЛО. Переключатель и галка — два имени одного значения; тест проверяет и связку
        /// (`SetModeIndex` → `GetPassthrough`), и фактическую запись в хранилище.
        /// </summary>
        [Test]
        public void XrMode_RoundTripsThroughStorage()
        {
            Assert.AreEqual(0, KvXrSettings.ModeVr, "VR — первое положение переключателя режима");
            Assert.AreEqual(1, KvXrSettings.ModeMr, "MR — второе положение переключателя режима");

            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);

            Assert.AreEqual(KvXrSettings.ModeMr, KvXrSettings.GetModeIndex(),
                "записали MR — прочитали не MR: выбор режима не переживает даже одного чтения");
            Assert.IsTrue(KvXrSettings.GetPassthrough(),
                "переключатель MR не включил ветку сквозного вида (`GetPassthrough`): у режима два " +
                "входа в ОДНО хранилище, и они разошлись");
            Assert.AreEqual(KvXrSettings.ModeMr, PlayerPrefs.GetInt(KvXrSettings.KeyPassthrough, -1),
                "выбор режима не лёг в хранилище (`" + KvXrSettings.KeyPassthrough + "`) — после " +
                "перезапуска приложения он потеряется");

            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);

            Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.GetModeIndex(),
                "обратная запись VR не прочиталась — переключатель работает в одну сторону");
            Assert.IsFalse(KvXrSettings.GetPassthrough(),
                "выбрали VR, а сквозной вид остался включённым: режим не переключается обратно");

            // АНТИ-ВАКУУМ: значение в хранилище ДЕЙСТВИТЕЛЬНО меняется (иначе обе проверки выше
            // прошли бы и на сеттере, который ничего не пишет, но совпадает с умолчанием).
            KvXrSettings.ToggleMode();
            Assert.AreEqual(KvXrSettings.ModeMr, KvXrSettings.GetModeIndex(),
                "переключение режима одной кнопкой не сдвинуло состояние");
        }

        // ==================================================== 2. ЯЗЫК ПЕРЕЖИВАЕТ

        /// <summary>
        /// ЯЗЫК: КАЖДЫЙ ИЗ СЕМИ КОДОВ ЗАПИСАЛИ — ПРОЧИТАЛИ — СОВПАЛ, и код лёг в хранилище.
        ///
        /// ЧТО БЫЛО. Язык применялся мгновенно (`KvLoc.Changed` пересобирает оболочку), но выбор
        /// обязан ещё и пережить перезапуск — он хранится в `KvLoc.PrefsKey`. Сеттер мог применить
        /// язык и НЕ записать его: интерфейс переводился, а следующий запуск начинался с системного.
        ///
        /// ЧЕМ ПЛОХО. Владелец переключает язык, перезапускает шлем — и снова видит чужой язык.
        ///
        /// ЧТО СТАЛО. Проверяются оба факта сразу: применённый язык и значение в хранилище.
        /// </summary>
        [Test]
        public void Language_RoundTripsThroughStorage()
        {
            Assert.AreEqual(7, KvLoc.KnownFileCodes.Length,
                "в проекте семь языков словарей — если это число изменилось, тест надо пересмотреть");
            Assert.Greater(KvLoc.CountOf("ru"), 100,
                "русский словарь пуст — проверять переключение языка не на чем (вакуумный тест)");

            foreach (string code in KvLoc.KnownFileCodes)
            {
                KvLoc.SetLanguage(code);

                Assert.AreEqual(code, KvLoc.CurrentCode,
                    "записали язык «" + code + "», а применён «" + KvLoc.CurrentCode + "»");
                Assert.AreEqual(code, KvLoc.PreferenceCode,
                    "выбор оператора не запомнился: ожидался «" + code + "», получен «" +
                    KvLoc.PreferenceCode + "»");
                Assert.AreEqual(code, PlayerPrefs.GetString(KvLoc.PrefsKey, ""),
                    "выбор языка не лёг в хранилище (`" + KvLoc.PrefsKey + "`) — после перезапуска " +
                    "приложение начнёт с системного языка");

                // АНТИ-ВАКУУМ: словарь языка, на который переключились, обязан быть непустым —
                // иначе «переключение» вело бы на пустой каталог, и это выглядело бы как успех.
                Assert.Greater(KvLoc.CountOf(code), 100,
                    "у языка «" + code + "» нет строк: оператор увидит пустой интерфейс");
            }
        }

        // ==================================================== 3. ЗАЖИМ ПРИ ЧТЕНИИ

        /// <summary>
        /// СИЛА ВИНЬЕТКИ ЗАЖИМАЕТСЯ ПРИ ЧТЕНИИ, А НЕ ТОЛЬКО ПРИ ЗАПИСИ.
        ///
        /// ЧТО БЫЛО. Зажим стоял в сеттере: `SetVignetteStrength(5)` записывал 0,8. Но в хранилище
        /// значение попадает не только сеттером — остались файлы прошлых версий, ручные правки,
        /// испорченные данные. Геттер отдавал их как есть.
        ///
        /// ЧЕМ ПЛОХО. Виньетка −1 или 5 доезжает до шейдера/оверлея: края экрана либо полностью
        /// чёрные, либо «наоборот» — оператор в шлеме получает дискомфорт, а причина не в сцене,
        /// а в одном числе из хранилища.
        ///
        /// ЧТО СТАЛО. Чтение шлема зажимает значение в задокументированный коридор 0…0,8, поэтому
        /// мусор в хранилище не меняет поведение приложения.
        /// </summary>
        [Test]
        public void VignetteStrength_ClampsOnReadNotOnlyOnWrite()
        {
            // (1) ЧТЕНИЕ: значение вне диапазона записано В ОБХОД сеттера.
            PlayerPrefs.SetFloat(KvXrSettings.KeyVignetteStrength, 5f);
            Assert.AreEqual(KvXrSettings.MaxVignetteStrength, KvXrSettings.GetVignetteStrength(), 1e-6f,
                "сила виньетки 5 из хранилища прочитана без зажима — края экрана станут сплошными");

            PlayerPrefs.SetFloat(KvXrSettings.KeyVignetteStrength, -1f);
            Assert.AreEqual(KvXrSettings.MinVignetteStrength, KvXrSettings.GetVignetteStrength(), 1e-6f,
                "отрицательная сила виньетки прочитана без зажима — затемнение «наоборот»");

            // (2) ЗАПИСЬ тоже зажимает — иначе значение 5 осталось бы ждать своего читателя.
            KvXrSettings.SetVignetteStrength(5f);
            Assert.AreEqual(KvXrSettings.MaxVignetteStrength, KvXrSettings.GetVignetteStrength(), 1e-6f,
                "сеттер записал силу виньетки вне коридора");
            KvXrSettings.SetVignetteStrength(-1f);
            Assert.AreEqual(KvXrSettings.MinVignetteStrength, KvXrSettings.GetVignetteStrength(), 1e-6f,
                "сеттер записал отрицательную силу виньетки");

            // АНТИ-ВАКУУМ: коридор не «схлопнут» — значение ВНУТРИ диапазона проходит без изменений,
            // иначе зажим можно было бы сломать до константы, и тест этого не заметил бы.
            Assert.Greater(KvXrSettings.MaxVignetteStrength, KvXrSettings.MinVignetteStrength,
                "коридор силы виньетки вырожден — зажим проверять не на чем");
            float middle = (KvXrSettings.MinVignetteStrength + KvXrSettings.MaxVignetteStrength) * 0.5f;
            KvXrSettings.SetVignetteStrength(middle);
            Assert.AreEqual(middle, KvXrSettings.GetVignetteStrength(), 1e-4f,
                "значение ВНУТРИ коридора искажено зажимом — тогда «зажим» превратился в «константу»");
        }

        // ==================================================== 4. СБРОС ВСЕХ ПАРАМЕТРОВ

        /// <summary>
        /// ЗАДОКУМЕНТИРОВАННОЕ ЗНАЧЕНИЕ ПО УМОЛЧАНИЮ ДЛЯ ЧИСЛА РЯДОВ ТУЛБАРА.
        ///
        /// ЗАЧЕМ ОТДЕЛЬНАЯ КОНСТАНТА В ТЕСТЕ. В коде у `ToolbarRows` НЕТ константы умолчания:
        /// тройка записана литералом в `KvSettings.Reload` и в `KvSettings.ResetToDefaults`.
        /// Пока её нет, тест обязан назвать это число осознанно, а не «подсмотреть текущее» —
        /// иначе проверка умолчания стала бы вакуумной (сравнение значения с самим собой).
        /// Это мелкий долг проекта: у остальных параметров константа есть, у этого — нет.
        /// </summary>
        private const int DocumentedDefaultToolbarRows = 3;

        /// <summary>
        /// «СБРОС НАСТРОЕК» ВОЗВРАЩАЕТ УМОЛЧАНИЯ ПО ВСЕМ ПАРАМЕТРАМ СРАЗУ.
        ///
        /// ЧТО БЫЛО. Сброс восстанавливал значения и записывал их в хранилище, но проверить это
        /// можно было только глазами: ни один тест не перечислял параметры.
        ///
        /// ЧЕМ ПЛОХО. Забыть один параметр в `ResetToDefaults` ничего не стоит — код длинный,
        /// поля и записи идут парами. Оператор нажимает «Сброс», большая часть настроек меняется,
        /// а одна остаётся «моей» — и он уверен, что сбросил всё.
        ///
        /// ЧТО СТАЛО. Тест выставляет КАЖДЫЙ параметр не по умолчанию, сбрасывает и сверяет
        /// каждый с его собственной константой умолчания.
        /// </summary>
        [Test]
        public void ResetToDefaults_RestoresEveryParameter()
        {
            // (1) ВЫСТАВИТЬ ВСЁ НЕ ПО УМОЛЧАНИЮ.
            KvSettings.ShowWorkspace = false;
            KvSettings.ShowJointLimits = false;
            KvSettings.ShowMetrics = false;
            KvSettings.Flashlight = true;
            KvSettings.AutoRefreshTree = false;
            KvSettings.ShowPhantoms = false;
            KvSettings.UiScale = 1.3f;
            KvSettings.Density = 1;
            KvSettings.ToolbarRows = 5;
            KvSettings.ToolbarLayout = 2;
            KvSettings.ToolbarCompact = true;
            KvSettings.FontSize = 2;
            KvSettings.HighContrast = true;
            KvSettings.ColorBlind = 2;
            KvSettings.KeyboardNav = false;
            KvSettings.GamepadHud = false;
            KvSettings.ErrorPlate = false;

            // (2) АНТИ-ВАКУУМ: подготовка обязана была сработать, иначе сверка ниже пройдёт
            // на значениях, которые и не менялись.
            Assert.IsFalse(KvSettings.ShowWorkspace, "подготовка сорвана: галка не выключилась");
            Assert.AreEqual(1.3f, KvSettings.UiScale, 1e-6f, "подготовка сорвана: масштаб не записался");
            Assert.AreEqual(2, KvSettings.FontSize, "подготовка сорвана: кегль не записался");
            Assert.AreEqual(5, KvSettings.ToolbarRows, "подготовка сорвана: число рядов не записалось");

            // (3) СБРОС.
            KvSettings.ResetToDefaults();

            // (4) СВЕРКА КАЖДОГО ПАРАМЕТРА С ЕГО КОНСТАНТОЙ.
            var wrong = new List<string>();
            if (KvSettings.ShowWorkspace != KvSettings.DefaultWorkspace)
                wrong.Add("ShowWorkspace = " + KvSettings.ShowWorkspace);
            if (KvSettings.ShowJointLimits != KvSettings.DefaultJointLimits)
                wrong.Add("ShowJointLimits = " + KvSettings.ShowJointLimits);
            if (KvSettings.ShowMetrics != KvSettings.DefaultMetrics)
                wrong.Add("ShowMetrics = " + KvSettings.ShowMetrics);
            if (KvSettings.Flashlight != KvSettings.DefaultFlashlight)
                wrong.Add("Flashlight = " + KvSettings.Flashlight);
            if (KvSettings.AutoRefreshTree != KvSettings.DefaultAutoRefreshTree)
                wrong.Add("AutoRefreshTree = " + KvSettings.AutoRefreshTree);
            if (KvSettings.ShowPhantoms != KvSettings.DefaultPhantoms)
                wrong.Add("ShowPhantoms = " + KvSettings.ShowPhantoms);
            if (Mathf.Abs(KvSettings.UiScale - KvSettings.DefaultUiScale) > 1e-6f)
                wrong.Add("UiScale = " + KvSettings.UiScale);
            if (KvSettings.Density != KvSettings.DefaultDensity)
                wrong.Add("Density = " + KvSettings.Density);
            if (KvSettings.ToolbarRows != DocumentedDefaultToolbarRows)
                wrong.Add("ToolbarRows = " + KvSettings.ToolbarRows);
            if (KvSettings.ToolbarLayout != KvSettings.DefaultToolbarLayout)
                wrong.Add("ToolbarLayout = " + KvSettings.ToolbarLayout);
            if (KvSettings.ToolbarCompact != KvSettings.DefaultToolbarCompact)
                wrong.Add("ToolbarCompact = " + KvSettings.ToolbarCompact);
            if (KvSettings.FontSize != KvSettings.DefaultFontSize)
                wrong.Add("FontSize = " + KvSettings.FontSize);
            if (KvSettings.HighContrast != KvSettings.DefaultHighContrast)
                wrong.Add("HighContrast = " + KvSettings.HighContrast);
            if (KvSettings.ColorBlind != KvSettings.DefaultColorBlind)
                wrong.Add("ColorBlind = " + KvSettings.ColorBlind);
            if (KvSettings.KeyboardNav != KvSettings.DefaultKeyboardNav)
                wrong.Add("KeyboardNav = " + KvSettings.KeyboardNav);
            if (KvSettings.GamepadHud != KvSettings.DefaultGamepadHud)
                wrong.Add("GamepadHud = " + KvSettings.GamepadHud);
            if (KvSettings.ErrorPlate != KvSettings.DefaultErrorPlate)
                wrong.Add("ErrorPlate = " + KvSettings.ErrorPlate);

            Assert.AreEqual(0, wrong.Count,
                "«Сброс настроек» вернул к умолчанию не всё — оператор уверен, что сбросил, а " +
                "эти параметры остались его: " + string.Join("; ", wrong.ToArray()));

            // (5) Сброс обязан быть записан в хранилище, иначе он «сработал» только в памяти
            // и откатится при перезапуске.
            Assert.AreEqual(KvSettings.DefaultUiScale, PlayerPrefs.GetFloat(KvSettings.KeyUiScale, -1f), 1e-6f,
                "сброшенный масштаб интерфейса не лёг в хранилище — после перезапуска вернётся прежний");
        }

        /// <summary>
        /// СБРОС НЕ ТРОГАЕТ ПОСТОРОННИЕ КЛЮЧИ ХРАНИЛИЩА.
        ///
        /// ЧТО БЫЛО. «Простой» способ сбросить настройки — стереть всё хранилище целиком. Соблазн
        /// реальный: одна строка вместо семнадцати пар «поле + запись».
        ///
        /// ЧЕМ ПЛОХО. В том же PlayerPrefs лежат выбор языка, режим VR/MR, поза панели, луч руки,
        /// состояние групп тулбара и настройки других подсистем. Сброс «всего» уносит их молча:
        /// оператор просил вернуть фабричные галки, а получил другую раскладку и другой язык.
        ///
        /// ЧТО СТАЛО. Сброс пишет умолчания ТОЛЬКО своих ключей; посторонний ключ обязан выжить.
        /// </summary>
        [Test]
        public void ResetToDefaults_LeavesForeignKeysAlone()
        {
            const string foreignInt = "KvSettingsTests.ForeignInt";
            const string foreignText = "KvSettingsTests.ForeignText";

            // Подчистка на случай прошлого прерванного прогона.
            PlayerPrefs.DeleteKey(foreignInt);
            PlayerPrefs.DeleteKey(foreignText);

            try
            {
                PlayerPrefs.SetInt(foreignInt, 4242);
                PlayerPrefs.SetString(foreignText, "не трогать");
                KvSettings.UiScale = 1.3f;      // «своя» настройка тоже изменена — сброс должен её тронуть

                KvSettings.ResetToDefaults();

                Assert.AreEqual(4242, PlayerPrefs.GetInt(foreignInt, -1),
                    "сброс настроек интерфейса стёр ПОСТОРОННИЙ целочисленный ключ хранилища: " +
                    "вместе с галками оператор потерял выбор языка/режима/раскладки");
                Assert.AreEqual("не трогать", PlayerPrefs.GetString(foreignText, ""),
                    "сброс настроек стёр ПОСТОРОННЮЮ строку хранилища");

                // АНТИ-ВАКУУМ: сброс действительно сработал — иначе «чужой ключ цел» верно и для
                // сброса, который вообще ничего не делает.
                Assert.AreEqual(KvSettings.DefaultUiScale, KvSettings.UiScale, 1e-6f,
                    "сброс не тронул даже собственный параметр — тогда проверка чужого ключа вакуумна");
            }
            finally
            {
                PlayerPrefs.DeleteKey(foreignInt);
                PlayerPrefs.DeleteKey(foreignText);
                PlayerPrefs.Save();
            }
        }

        // ==================================================== 5. ИСПОРЧЕННОЕ ЗНАЧЕНИЕ

        /// <summary>
        /// «МУСОР» ВМЕСТО ЧИСЛА НЕ БРОСАЕТ ИСКЛЮЧЕНИЕ И НЕ ВЫХОДИТ ЗА КОРИДОР.
        ///
        /// ЧТО БЫЛО. Правило проекта (§NaN/§storage): испорченное значение в хранилище не должно
        /// ронять приложение и не должно оставлять его в состоянии, которое нельзя назвать.
        ///
        /// ЧЕМ ПЛОХО. Исключение в геттере настроек вылетает из середины построения интерфейса:
        /// оператор видит пустую панель и не понимает, что дело в одном ключе хранилища.
        ///
        /// ЧТО СТАЛО. Чтение проходит через зажим, поэтому любой мусор остаётся в коридоре.
        /// ВАЖНО ПРО ЧЕСТНОСТЬ ПРОВЕРКИ: Unity сопоставляет нечисловую строку с нулём, поэтому
        /// сверять «с умолчанием» здесь НЕЛЬЗЯ — проверяется отсутствие исключения, отсутствие
        /// NaN и попадание в задокументированный коридор (умолчание для отсутствующего ключа
        /// проверяется отдельным тестом ниже).
        /// </summary>
        [Test]
        public void GarbageInStorage_DoesNotThrowAndStaysInRange()
        {
            PlayerPrefs.SetString(KvXrSettings.KeyVignetteStrength, "мусор");
            PlayerPrefs.SetString(KvXrSettings.KeyQuickPanelButtonCount, "мусор");
            PlayerPrefs.SetString(KvXrSettings.KeySmoothSpeed, "не число");

            float strength = float.NaN;
            Assert.DoesNotThrow(() => strength = KvXrSettings.GetVignetteStrength(),
                "чтение силы виньетки из испорченного хранилища бросило исключение — интерфейс " +
                "построить нельзя, и причина не названа");
            Assert.IsFalse(float.IsNaN(strength) || float.IsInfinity(strength),
                "сила виньетки из испорченного хранилища = " + strength + " (NaN/∞), а не число");
            Assert.GreaterOrEqual(strength, KvXrSettings.MinVignetteStrength,
                "испорченное значение вышло ниже коридора: " + strength);
            Assert.LessOrEqual(strength, KvXrSettings.MaxVignetteStrength,
                "испорченное значение вышло выше коридора: " + strength);

            int buttons = -1;
            Assert.DoesNotThrow(() => buttons = KvXrSettings.GetQuickPanelButtonCount(),
                "чтение числа кнопок панели из испорченного хранилища бросило исключение");
            Assert.GreaterOrEqual(buttons, KvXrQuickPanelLayout.MinButtons,
                "число кнопок панели ниже допустимого: " + buttons);
            Assert.LessOrEqual(buttons, KvXrQuickPanelLayout.MaxButtons,
                "число кнопок панели выше допустимого: " + buttons);

            float speed = float.NaN;
            Assert.DoesNotThrow(() => speed = KvXrSettings.GetSmoothSpeed(),
                "чтение скорости хода из испорченного хранилища бросило исключение");
            Assert.GreaterOrEqual(speed, KvXrSettings.MinSmoothSpeed,
                "скорость хода ниже коридора: " + speed);
            Assert.LessOrEqual(speed, KvXrSettings.MaxSmoothSpeed,
                "скорость хода выше коридора: " + speed);

            // АНТИ-ВАКУУМ: коридоры не вырождены — иначе предыдущие проверки выполнялись бы
            // автоматически при любом значении.
            Assert.Greater(KvXrSettings.MaxSmoothSpeed, KvXrSettings.MinSmoothSpeed,
                "коридор скорости хода вырожден — проверка «в коридоре» ничего не значит");
            Assert.Greater(KvXrQuickPanelLayout.MaxButtons, KvXrQuickPanelLayout.MinButtons,
                "коридор числа кнопок вырожден — проверка «в коридоре» ничего не значит");
        }

        // ==================================================== 6. ЗНАЧЕНИЕ ВНЕ ДИАПАЗОНА

        /// <summary>
        /// ЗНАЧЕНИЕ ВНЕ ДИАПАЗОНА, ЗАПИСАННОЕ ПРЯМО В ХРАНИЛИЩЕ, ЧИТАЕТСЯ ЗАЖАТЫМ.
        ///
        /// ЧТО БЫЛО. Зажим в сеттерах не спасает от данных, попавших в хранилище иначе: старая
        /// версия приложения, ручная правка, восстановление настроек с другого шлема.
        ///
        /// ЧЕМ ПЛОХО. Масштаб мира 7 — это мир, в котором оператор случайно оказался великаном:
        /// панель улетела из поля зрения, робот вышел из комнаты, и «приложение сломалось».
        /// Поза панели 1 см от глаз — то же самое, только ещё и с резью в глазах.
        ///
        /// ЧТО СТАЛО. Каждый геттер шлема зажимает значение в свой задокументированный коридор.
        /// </summary>
        [Test]
        public void OutOfRangeInStorage_IsClampedOnRead()
        {
            PlayerPrefs.SetFloat(KvXrSettings.KeySmoothSpeed, 99f);
            Assert.AreEqual(KvXrSettings.MaxSmoothSpeed, KvXrSettings.GetSmoothSpeed(), 1e-4f,
                "скорость хода 99 м/с прочитана без зажима — оператор улетит сквозь стену");

            PlayerPrefs.SetFloat(KvXrSettings.KeyPanelDistance, 0.01f);
            Assert.AreEqual(KvXrSettings.MinPanelDistance, KvXrSettings.GetPanelDistance(), 1e-4f,
                "панель в 1 см от глаз прочитана без зажима — её нельзя прочитать глазами");

            PlayerPrefs.SetFloat(KvXrSettings.KeyWorldScale, 7f);
            Assert.AreEqual(KvXrSettings.MaxWorldScale, KvXrSettings.GetWorldScale(), 1e-4f,
                "масштаб мира 7 прочитан без зажима — лаборатория перестанет помещаться вокруг оператора");

            PlayerPrefs.SetFloat(KvXrSettings.KeyOperatorHeight, 5f);
            Assert.AreEqual(KvXrSettings.MaxOperatorHeight, KvXrSettings.GetOperatorHeight(), 1e-4f,
                "рост оператора 5 м прочитан без зажима — уровень глаз окажется выше лаборатории");

            PlayerPrefs.SetFloat(KvXrSettings.KeyTeleportDistance, 500f);
            Assert.AreEqual(KvXrSettings.MaxTeleportDistance, KvXrSettings.GetTeleportDistance(), 1e-4f,
                "дальность телепорта 500 м прочитана без зажима — прыжок за пределы сцены");

            PlayerPrefs.SetInt(KvXrSettings.KeyQuickPanelButtonCount, 500);
            Assert.AreEqual(KvXrQuickPanelLayout.MaxButtons, KvXrSettings.GetQuickPanelButtonCount(),
                "число кнопок панели 500 прочитано без зажима — панель у руки не построить");

            PlayerPrefs.SetFloat(KvXrSettings.KeyQuickPanelRadius, 9f);
            Assert.AreEqual(KvXrQuickPanelLayout.MaxRadiusMeters, KvXrSettings.GetQuickPanelRadius(), 1e-4f,
                "радиус панели у руки 9 м прочитан без зажима — панель закроет весь обзор");

            // АНТИ-ВАКУУМ: коридоры не вырождены, и зажим не «прибивает» любое значение к границе.
            Assert.Greater(KvXrSettings.MaxPanelDistance, KvXrSettings.MinPanelDistance,
                "коридор расстояния панели вырожден");
            PlayerPrefs.SetFloat(KvXrSettings.KeyWorldScale, 0.5f);
            Assert.AreEqual(0.5f, KvXrSettings.GetWorldScale(), 1e-4f,
                "значение ВНУТРИ коридора искажено — «зажим» превратился в «константу»");
        }

        // ==================================================== 7. ОТСУТСТВУЮЩИЙ КЛЮЧ

        /// <summary>
        /// ОТСУТСТВУЮЩИЙ КЛЮЧ ДАЁТ ЗАДОКУМЕНТИРОВАННОЕ УМОЛЧАНИЕ — сверка с КОНСТАНТОЙ, а не
        /// с «каким-то числом».
        ///
        /// ЧТО БЫЛО. Первый запуск приложения — это пустое хранилище. Значения по умолчанию
        /// объявлены константами (`DefaultSmoothSpeed`, `DefaultPanelDistance`, …), и именно они
        /// определяют, каким оператор увидит приложение ДО того, как что-то настроит.
        ///
        /// ЧЕМ ПЛОХО. Если геттер вернёт 0 вместо умолчания, первый запуск покажет виньетку 0,
        /// масштаб мира 0 и панель на нулевом расстоянии — «приложение сломано при первом запуске»,
        /// и никакая настройка этого не объясняет.
        ///
        /// ЧТО СТАЛО. Тест стирает ключи и сверяет значения с константами: изменение умолчания
        /// в коде обязано быть осознанным, а не случайным.
        /// </summary>
        [Test]
        public void MissingKey_GivesDocumentedDefaultConstants()
        {
            PlayerPrefs.DeleteKey(KvXrSettings.KeyVignetteStrength);
            PlayerPrefs.DeleteKey(KvXrSettings.KeySmoothSpeed);
            PlayerPrefs.DeleteKey(KvXrSettings.KeyPanelDistance);
            PlayerPrefs.DeleteKey(KvXrSettings.KeyWorldScale);
            PlayerPrefs.DeleteKey(KvXrSettings.KeyOperatorHeight);
            PlayerPrefs.DeleteKey(KvXrSettings.KeyWorkspaceVisible);
            PlayerPrefs.DeleteKey(KvXrSettings.KeyQuickPanelButtonCount);
            PlayerPrefs.DeleteKey(KvXrSettings.KeyMoveMode);

            Assert.AreEqual(KvXrSettings.DefaultVignetteStrength, KvXrSettings.GetVignetteStrength(), 1e-6f,
                "без ключа сила виньетки не равна документированному умолчанию");
            Assert.AreEqual(KvXrSettings.DefaultSmoothSpeed, KvXrSettings.GetSmoothSpeed(), 1e-6f,
                "без ключа скорость хода не равна документированному умолчанию");
            Assert.AreEqual(KvXrSettings.DefaultPanelDistance, KvXrSettings.GetPanelDistance(), 1e-6f,
                "без ключа расстояние панели не равно документированному умолчанию");
            Assert.AreEqual(KvXrSettings.DefaultWorldScale, KvXrSettings.GetWorldScale(), 1e-6f,
                "без ключа масштаб мира не равен документированному умолчанию");
            Assert.AreEqual(KvXrSettings.DefaultOperatorHeight, KvXrSettings.GetOperatorHeight(), 1e-6f,
                "без ключа рост оператора не равен документированному умолчанию");
            Assert.AreEqual(KvXrSettings.DefaultWorkspaceVisible != 0, KvXrSettings.GetWorkspaceVisible(),
                "без ключа «зона достижимости» не равна документированному умолчанию: визуализация " +
                "обязана быть ВЫКЛЮЧЕНА (жалоба владельца «появились сами по себе»)");
            Assert.AreEqual(KvXrSettings.DefaultQuickPanelButtons, KvXrSettings.GetQuickPanelButtonCount(),
                "без ключа число кнопок панели у руки не равно документированному умолчанию");
            Assert.AreEqual(KvXrSettings.DefaultMoveMode, KvXrSettings.GetMoveModeIndex(),
                "без ключа способ перемещения не равен документированному умолчанию");

            // Интерфейс: кэш перечитывается, иначе проверялись бы значения из памяти теста.
            PlayerPrefs.DeleteKey(KvSettings.KeyUiScale);
            PlayerPrefs.DeleteKey(KvSettings.KeyFontSize);
            PlayerPrefs.DeleteKey(KvSettings.KeyDensity);
            PlayerPrefs.DeleteKey(KvSettings.KeyErrorPlate);
            KvSettings.Reload();

            Assert.AreEqual(KvSettings.DefaultUiScale, KvSettings.UiScale, 1e-6f,
                "без ключа масштаб интерфейса не равен документированному умолчанию");
            Assert.AreEqual(KvSettings.DefaultFontSize, KvSettings.FontSize,
                "без ключа кегль интерфейса не равен документированному умолчанию");
            Assert.AreEqual(KvSettings.DefaultDensity, KvSettings.Density,
                "без ключа плотность строк не равна документированному умолчанию");
            Assert.AreEqual(KvSettings.DefaultErrorPlate, KvSettings.ErrorPlate,
                "без ключа плашка ошибок не равна документированному умолчанию");

            // АНТИ-ВАКУУМ: умолчания РАЗЛИЧНЫ (иначе сверка с константой ничего не различала бы).
            Assert.AreNotEqual(KvXrSettings.DefaultSmoothSpeed, KvXrSettings.MaxSmoothSpeed,
                "умолчание скорости хода совпало с верхней границей — «умолчание» и «зажим» " +
                "невозможно различить");
        }

        // ==================================================== 8. СБРОС В ШЛЕМЕ

        /// <summary>
        /// СБРОСЫ ШЛЕМА ВОЗВРАЩАЮТ УМОЛЧАНИЯ (`ResetMoveMode`, `ResetMode`).
        ///
        /// ЧТО БЫЛО. У настроек шлема нет одного «сброса всего»: есть адресные `ResetMoveMode`
        /// и `ResetMode`, и они обязаны не просто очистить хранилище, но и ОПОВЕСТИТЬ слушателей
        /// (`KvXrSettingsChanged`), иначе кэши (правила ходьбы, луч, панель) останутся со старым
        /// значением — «сбросил, а ходьба осталась плавной».
        ///
        /// ЧЕМ ПЛОХО. Молчаливая очистка выглядит как рабочий сброс и не работает до перезапуска.
        ///
        /// ЧТО СТАЛО. После сброса чтение даёт умолчание, а событие смены настроек действительно
        /// приходит подписчику.
        /// </summary>
        [Test]
        public void XrResets_RestoreDefaultsAndNotify()
        {
            int notified = 0;
            System.Action handler = delegate { notified++; };
            KvXrSettingsChanged.SettingsChanged += handler;
            try
            {
                KvXrSettings.SetMoveMode(KvXrMoveMode.Teleport);
                Assert.AreNotEqual(KvXrSettings.DefaultMoveMode, KvXrSettings.GetMoveModeIndex(),
                    "подготовка сорвана: способ перемещения не изменился — сбрасывать нечего");

                KvXrSettings.ResetMoveMode();
                Assert.AreEqual(KvXrSettings.DefaultMoveMode, KvXrSettings.GetMoveModeIndex(),
                    "`ResetMoveMode` не вернул документированное умолчание способа перемещения");

                KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
                Assert.AreNotEqual(KvXrSettings.ModeVr, KvXrSettings.GetModeIndex(),
                    "подготовка сорвана: режим MR не включился");

                KvXrSettings.ResetMode();
                Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.GetModeIndex(),
                    "`ResetMode` не вернул VR — решение владельца «запуск всегда в VR» нарушено");
                Assert.IsFalse(PlayerPrefs.HasKey(KvXrSettings.KeyPassthrough),
                    "`ResetMode` оставил ключ в хранилище: сброс должен возвращать к «ключа нет»");

                // АНТИ-ВАКУУМ: без оповещения кэши остались бы со старым значением, и «сброс»
                // был бы виден только до первого чтения из кэша.
                Assert.GreaterOrEqual(notified, 2,
                    "сбросы не оповестили подписчиков `KvXrSettingsChanged` (оповещений " + notified +
                    "): кэши ходьбы/луча/панели останутся со старым значением до перезапуска");
            }
            finally
            {
                KvXrSettingsChanged.SettingsChanged -= handler;
            }
        }

        // ==================================================== 9. ПЕРЕЖИВАНИЕ «ПЕРЕЗАПУСКА»

        /// <summary>
        /// ЗАПИСАННЫЕ НАСТРОЙКИ ИНТЕРФЕЙСА ПЕРЕЖИВАЮТ ПЕРЕЧТЕНИЕ ХРАНИЛИЩА (модель перезапуска).
        ///
        /// ЧТО БЫЛО. У `KvSettings` есть кэш в статических полях: читать хранилище каждый кадр
        /// нельзя (бюджет 72 Гц). Пока сеттер обновлял и кэш, и хранилище, всё сходилось; стоило
        /// забыть запись — значение жило до перезапуска и исчезало после него.
        ///
        /// ЧЕМ ПЛОХО. Ровно так выглядит «настройки не сохраняются»: в текущей сессии всё работает,
        /// после перезапуска — заводские значения, и воспроизвести это в редакторе трудно.
        ///
        /// ЧТО СТАЛО. `Reload()` — это и есть путь «приложение только что запустилось»: он читает
        /// хранилище заново. Все записанные значения обязаны вернуться теми же.
        /// </summary>
        [Test]
        public void WrittenUiSettings_SurviveReload()
        {
            KvSettings.UiScale = 1.15f;
            KvSettings.FontSize = 2;
            KvSettings.HighContrast = true;
            KvSettings.ToolbarCompact = true;
            KvSettings.ColorBlind = 3;
            KvSettings.Density = 1;

            KvSettings.Reload();    // «перезапуск»: кэш выброшен, значения читаются из хранилища

            var lost = new List<string>();
            if (Mathf.Abs(KvSettings.UiScale - 1.15f) > 1e-6f) lost.Add("UiScale = " + KvSettings.UiScale);
            if (KvSettings.FontSize != 2) lost.Add("FontSize = " + KvSettings.FontSize);
            if (!KvSettings.HighContrast) lost.Add("HighContrast = " + KvSettings.HighContrast);
            if (!KvSettings.ToolbarCompact) lost.Add("ToolbarCompact = " + KvSettings.ToolbarCompact);
            if (KvSettings.ColorBlind != 3) lost.Add("ColorBlind = " + KvSettings.ColorBlind);
            if (KvSettings.Density != 1) lost.Add("Density = " + KvSettings.Density);

            Assert.AreEqual(0, lost.Count,
                "записанные настройки интерфейса НЕ пережили перечтение хранилища — после " +
                "перезапуска приложения они вернутся к заводским: " + string.Join("; ", lost.ToArray()));

            // АНТИ-ВАКУУМ: кэш действительно перечитывался. Если бы `Reload` ничего не делал,
            // предыдущая проверка прошла бы на значениях, оставшихся в памяти теста.
            KvSettings.UiScale = 0.85f;
            KvSettings.Reload();
            Assert.AreEqual(0.85f, KvSettings.UiScale, 1e-6f,
                "`Reload` не перечитывает хранилище: значения остались из памяти, и «перезапуск» " +
                "в этом тесте ничего не проверял");
        }

        // ==================================================== 10. ЗАЖИМ ПРИ ЧТЕНИИ (KvSettings)

        /// <summary>
        /// НАСТРОЙКИ ИНТЕРФЕЙСА ЗАЖИМАЮТСЯ ПРИ ЧТЕНИИ, А НЕ ТОЛЬКО ПРИ ЗАПИСИ.
        ///
        /// ЧТО БЫЛО (дефект Д2, найден при написании этого файла и исправлен в рантайме). Сеттеры
        /// `KvSettings` зажимали значения (`Mathf.Clamp(value, 0.6f, 1.6f)` и т.д.), а `Reload()`
        /// читал хранилище БЕЗ зажима: `uiScale = PlayerPrefs.GetFloat(KeyUiScale, DefaultUiScale)`.
        /// В хранилище значение попадает не только сеттером — остались настройки прошлых версий,
        /// ручные правки, данные с другого компьютера.
        ///
        /// ЧЕМ ПЛОХО. Масштаб интерфейса 99 растягивает оболочку так, что кнопок не найти, −5
        /// схлопывает её в точку. У настроек ШЛЕМА (`KvXrSettings`) зажим при чтении был всегда,
        /// поэтому два хранилища одного проекта вели себя по-разному — и «слетело / не слетело»
        /// зависело от того, какое читают.
        ///
        /// ЧТО СТАЛО. Коридоры названы константами (`MinUiScale`…`MaxColorBlind`) и применяются
        /// и при записи, и при чтении: проверяются оба конца коридора для дробной настройки и
        /// границы для целочисленных.
        /// </summary>
        [Test]
        public void UiSettings_ClampOutOfRangeValuesOnRead()
        {
            // (1) МАСШТАБ ИНТЕРФЕЙСА — дробная настройка, оба конца коридора.
            PlayerPrefs.SetFloat(KvSettings.KeyUiScale, 99f);
            KvSettings.Reload();
            Assert.AreEqual(KvSettings.MaxUiScale, KvSettings.UiScale, 1e-4f,
                "масштаб интерфейса 99 из хранилища прочитан без зажима — оболочку невозможно " +
                "рассмотреть, и причину искать негде");
            Assert.LessOrEqual(KvSettings.UiScale, KvSettings.MaxUiScale,
                "масштаб интерфейса вышел за верхнюю границу коридора");

            PlayerPrefs.SetFloat(KvSettings.KeyUiScale, -5f);
            KvSettings.Reload();
            Assert.AreEqual(KvSettings.MinUiScale, KvSettings.UiScale, 1e-4f,
                "отрицательный масштаб интерфейса прочитан без зажима — оболочка схлопывается в точку");

            // (2) ЦЕЛОЧИСЛЕННЫЕ НАСТРОЙКИ — выше и ниже коридора.
            PlayerPrefs.SetInt(KvSettings.KeyFontSize, 99);
            PlayerPrefs.SetInt(KvSettings.KeyColorBlind, 99);
            PlayerPrefs.SetInt(KvSettings.KeyToolbarLayout, 99);
            PlayerPrefs.SetInt(KvSettings.KeyToolbarRows, 99);
            PlayerPrefs.SetInt(KvSettings.KeyDensity, -7);
            KvSettings.Reload();

            var wrong = new List<string>();
            if (KvSettings.FontSize != KvSettings.MaxFontSize) wrong.Add("FontSize = " + KvSettings.FontSize);
            if (KvSettings.ColorBlind != KvSettings.MaxColorBlind) wrong.Add("ColorBlind = " + KvSettings.ColorBlind);
            if (KvSettings.ToolbarLayout != KvSettings.MaxToolbarLayout) wrong.Add("ToolbarLayout = " + KvSettings.ToolbarLayout);
            if (KvSettings.ToolbarRows != KvSettings.MaxToolbarRows) wrong.Add("ToolbarRows = " + KvSettings.ToolbarRows);
            if (KvSettings.Density != KvSettings.MinDensity) wrong.Add("Density = " + KvSettings.Density);

            Assert.AreEqual(0, wrong.Count,
                "эти настройки интерфейса прочитаны ВНЕ коридора (ожидались границы): " +
                string.Join("; ", wrong.ToArray()));

            // (3) АНТИ-ВАКУУМ: зажим не «прибивает» значение к границе — значение внутри коридора
            // проходит без изменений, иначе проверка выше выполнялась бы при любом зажиме.
            PlayerPrefs.SetFloat(KvSettings.KeyUiScale, 1.15f);
            PlayerPrefs.SetInt(KvSettings.KeyFontSize, 0);
            KvSettings.Reload();
            Assert.AreEqual(1.15f, KvSettings.UiScale, 1e-6f,
                "значение ВНУТРИ коридора искажено зажимом при чтении — «зажим» превратился в «константу»");
            Assert.AreEqual(0, KvSettings.FontSize,
                "кегль 0 — законное значение внутри коридора, а прочитан как " + KvSettings.FontSize);
        }

        /// <summary>
        /// «МУСОР» ВМЕСТО ЧИСЛА В НАСТРОЙКАХ ИНТЕРФЕЙСА ДАЁТ УМОЛЧАНИЕ, А НЕ НОЛЬ.
        ///
        /// ЧТО БЫЛО. Чтение было прямым (`PlayerPrefs.GetFloat`), поэтому испорченное значение
        /// доходило до интерфейса как есть. Unity читает нечисловую строку как НОЛЬ, а ноль для
        /// масштаба интерфейса — это «оболочки не видно».
        ///
        /// ЧЕМ ПЛОХО. Оператор видит пустой экран вместо интерфейса и не может даже открыть
        /// настройки, чтобы это исправить.
        ///
        /// ЧТО СТАЛО. Ноль ВНЕ коридора (а также NaN и бесконечность) трактуется как «значение не
        /// прочиталось» и заменяется документированным умолчанием. ЗАМЕЧАНИЕ ЧЕСТНОСТИ: у целочисленных
        /// настроек ноль — законное значение (`Density = 0`), поэтому «мусор» там читается как ноль;
        /// PlayerPrefs не отличает испорченную строку от честного нуля. Здесь проверяются те настройки,
        /// где отличить можно — масштаб (коридор не содержит нуля) и число рядов тулбара (от 1).
        /// </summary>
        [Test]
        public void UiSettings_GarbageInStorageFallsBackToDefaults()
        {
            PlayerPrefs.SetString(KvSettings.KeyUiScale, "мусор");
            PlayerPrefs.SetString(KvSettings.KeyToolbarRows, "мусор");

            Assert.DoesNotThrow(() => KvSettings.Reload(),
                "чтение испорченного хранилища бросило исключение — интерфейс не построить, и причина не названа");

            Assert.AreEqual(KvSettings.DefaultUiScale, KvSettings.UiScale, 1e-6f,
                "испорченный масштаб интерфейса дал " + KvSettings.UiScale + " вместо умолчания " +
                KvSettings.DefaultUiScale + ": Unity читает «мусор» как 0, и оболочка исчезнет");
            Assert.AreEqual(KvSettings.DefaultToolbarRows, KvSettings.ToolbarRows,
                "испорченное число рядов тулбара дало " + KvSettings.ToolbarRows + " вместо умолчания " +
                KvSettings.DefaultToolbarRows);

            Assert.IsFalse(float.IsNaN(KvSettings.UiScale) || float.IsInfinity(KvSettings.UiScale),
                "масштаб интерфейса из испорченного хранилища = " + KvSettings.UiScale);

            // (АНТИ-ВАКУУМ) Правильное число читается нормально: значит проверка выше сработала на
            // «мусоре», а не на том, что чтение сломано целиком.
            KvSettings.UiScale = 1.15f;     // сеттер зажимает и пишет в хранилище
            KvSettings.Reload();
            Assert.AreEqual(1.15f, KvSettings.UiScale, 1e-6f,
                "правильно записанный масштаб не прочитался — тогда вывод «мусор → умолчание» " +
                "ничего не значит: проверка ловила бы сломанное чтение, а не мусор");
        }
    }
}
