using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvUI;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ СЕССИИ §41 — ВВОД ТЕКСТА И ЧИСЕЛ В ШЛЕМЕ, РЕДАКТИРОВАНИЕ ЗНАЧЕНИЙ.
    ///
    /// НАПИСАНЫ ДО КОДА ([K5]): на момент написания ни одна функция, которую они проверяют, не
    /// реализована — файлы `KvXrValueField`, `KvXrTextInput`, `KvXrKeyboardLayout`, `KvXrWheelLayout`,
    /// `KvXrNumberWords`, `KvXrAimGuard`, `KvCommandSearch` содержат только объявления. Поэтому
    /// прогон до реализации обязан дать 28 падений и ни одного «зелёного» случайно.
    ///
    /// ЧТО ИМЕННО ПРОВЕРЯЕТСЯ (по заданию [S2]):
    ///   • ввод числа отклоняет буквы, пустую строку, выход за границы — и НЕ МЕНЯЕТ значение;
    ///   • границы берутся у существующих сеттеров, а не пишутся в шлеме заново;
    ///   • сохранённое читается ТЕМ ЖЕ API, что и на компьютере (одно хранилище, не второе);
    ///   • клавиатура содержит все обещанные символы и физически влезает в панель;
    ///   • колесо символов и захват стика (иначе выбор символа будет разворачивать оператора);
    ///   • способ ввода переключается и запоминается;
    ///   • карточка Б51: после смены уровня «прощённая» иконка больше не считается наведением,
    ///     и журнал не печатает «луч навёл «»»;
    ///   • поиск по командам ищет по тем же семи полям и в том же порядке, что и Ctrl+P.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: «видно ли это в шлеме» — это взгляд владельца и строки журнала приёмки.
    /// Число команд в реестре (147) и замер кадров проверяются на устройстве, а не тестом.
    /// </summary>
    public class KvXrS41Tests
    {
        // ============================================================ ИЗОЛЯЦИЯ НАСТРОЕК

        /// <summary>Ключи PlayerPrefs, которые тесты этой волны могут изменить.</summary>
        private static readonly string[] TouchedKeys =
        {
            KvXrSettings.KeyGroupPanelRadius, KvXrSettings.KeySmoothSpeed,
            KvXrSettings.KeyOperatorHeight, KvXrSettings.KeyTeleportDistance,
            KvSettings.KeyUiScale, KvSettings.KeyDensity, KvSettings.KeyToolbarRows,
            KvSettings.KeyToolbarLayout, KvSettings.KeyFontSize, KvSettings.KeyColorBlind,
            KvXrInputMethodSetting.Key
        };

        private readonly Dictionary<string, float> savedFloats = new Dictionary<string, float>();
        private readonly HashSet<string> savedKeys = new HashSet<string>();

        /// <summary>
        /// СОХРАНИТЬ СОСТОЯНИЕ НАСТРОЕК ДО ТЕСТА. Тесты пишут в настоящие настройки (иначе нельзя
        /// доказать, что запись идёт существующим сеттером), поэтому состояние восстанавливается —
        /// иначе прогон менял бы настройки машины, на которой его запустили.
        /// </summary>
        [SetUp]
        public void SaveSettings()
        {
            savedFloats.Clear();
            savedKeys.Clear();
            for (int i = 0; i < TouchedKeys.Length; i++)
            {
                string key = TouchedKeys[i];
                if (!PlayerPrefs.HasKey(key)) continue;
                savedKeys.Add(key);
                savedFloats[key] = PlayerPrefs.GetFloat(key);
            }
        }

        /// <summary>Восстановить настройки и заставить `KvSettings` перечитать их.</summary>
        [TearDown]
        public void RestoreSettings()
        {
            for (int i = 0; i < TouchedKeys.Length; i++)
            {
                string key = TouchedKeys[i];
                if (savedKeys.Contains(key)) PlayerPrefs.SetFloat(key, savedFloats[key]);
                else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
            KvSettings.Reload();
        }

        // ============================================================ 1. ВВОД ЧИСЛА

        /// <summary>
        /// БУКВЫ В ЧИСЛОВОЕ ПОЛЕ НЕ ПРИНИМАЮТСЯ. Строка журнала — из задания [S2] дословно.
        /// </summary>
        [Test]
        public void Numeric_RejectsLetters()
        {
            KvXrCommitOutcome outcome = KvXrValueInput.Commit(
                KvXrValueFieldId.PanelRadius, "abc", KvXrInputMethod.Keyboard);

            Assert.AreEqual(KvXrCommitStatus.BadFormat, outcome.Status,
                "«abc» не число — ввод обязан быть отклонён с причиной «неверный формат»");
            Assert.AreEqual("[KvXr] ВВОД: «abc» отклонён — значение не изменено", outcome.LogLine,
                "строка журнала об отказе обязана называть, что именно отклонено");
        }

        /// <summary>ПУСТАЯ СТРОКА, ТОЛЬКО МИНУС, ТОЛЬКО ТОЧКА — не числа.</summary>
        [Test]
        public void Numeric_RejectsEmptyAndSignOnly()
        {
            KvXrValueFieldInfo field = KvXrValueFields.Get(KvXrValueFieldId.PanelRadius);

            Assert.AreEqual(KvXrCommitStatus.Empty,
                KvXrValueInput.Parse(field, "").Status, "пустая строка — не число");
            Assert.AreEqual(KvXrCommitStatus.BadFormat,
                KvXrValueInput.Parse(field, "−").Status, "один минус — не число");
            Assert.AreEqual(KvXrCommitStatus.BadFormat,
                KvXrValueInput.Parse(field, ".").Status, "одна точка — не число");
            Assert.AreEqual(KvXrCommitStatus.BadFormat,
                KvXrValueInput.Parse(field, "1.2.3").Status, "две точки — не число");
        }

        /// <summary>
        /// ЧИСЛО ВНЕ ГРАНИЦ ОТКЛОНЯЕТСЯ. Радиус панели 9,9 м — это не «много», это ошибка ввода
        /// (границы 0,080…0,420 м взяты у `KvXrGroupPanelLayout.Min/MaxPanelRadiusMeters`).
        /// </summary>
        [Test]
        public void Numeric_RejectsOutOfBounds()
        {
            KvXrCommitOutcome outcome = KvXrValueInput.Commit(
                KvXrValueFieldId.PanelRadius, "9.9", KvXrInputMethod.Keyboard);

            Assert.AreEqual(KvXrCommitStatus.OutOfBounds, outcome.Status,
                "9,9 м вне границ радиуса панели");
            Assert.AreEqual("[KvXr] ВВОД: 9.9 отклонён (вне границ 0.080…0.420)", outcome.LogLine,
                "строка журнала обязана называть границы — иначе отказ выглядит как поломка");
        }

        /// <summary>ГРАНИЦЫ ВКЛЮЧИТЕЛЬНЫ: ровно 0,080 и ровно 0,420 принимаются, на шаг дальше — нет.</summary>
        [Test]
        public void Numeric_AcceptsBoundsEdges()
        {
            KvXrValueFieldInfo field = KvXrValueFields.Get(KvXrValueFieldId.PanelRadius);

            Assert.AreEqual(KvXrCommitStatus.Saved, KvXrValueInput.Parse(field, "0.080").Status,
                "нижняя граница — допустимое значение");
            Assert.AreEqual(KvXrCommitStatus.Saved, KvXrValueInput.Parse(field, "0.420").Status,
                "верхняя граница — допустимое значение");
            Assert.AreEqual(KvXrCommitStatus.OutOfBounds, KvXrValueInput.Parse(field, "0.079").Status,
                "на один миллиметр ниже границы — уже отказ");
            Assert.AreEqual(KvXrCommitStatus.OutOfBounds, KvXrValueInput.Parse(field, "0.421").Status,
                "на один миллиметр выше границы — уже отказ");

            Assert.AreEqual(0.080f, KvXrValueInput.Parse(field, "0.080").Value, 1e-5f,
                "принятое значение обязано быть ровно тем, что ввели");
        }

        /// <summary>
        /// ГРАНИЦЫ РЕЕСТРА = ГРАНИЦЫ СЕТТЕРОВ. Если это разойдётся, шлем начнёт принимать то, что
        /// компьютер зажимает молча — и владелец увидит разные числа в двух местах ([K4]).
        /// </summary>
        [Test]
        public void Numeric_EveryFieldBoundsAreOwnersConstants()
        {
            Assert.AreEqual(KvXrGroupPanelLayout.MinPanelRadiusMeters,
                KvXrValueFields.Get(KvXrValueFieldId.PanelRadius).Min, 1e-6f, "радиус: нижняя граница");
            Assert.AreEqual(KvXrGroupPanelLayout.MaxPanelRadiusMeters,
                KvXrValueFields.Get(KvXrValueFieldId.PanelRadius).Max, 1e-6f, "радиус: верхняя граница");
            Assert.AreEqual(KvXrSettings.MinSmoothSpeed,
                KvXrValueFields.Get(KvXrValueFieldId.SmoothSpeed).Min, 1e-6f, "скорость: нижняя граница");
            Assert.AreEqual(KvXrSettings.MaxSmoothSpeed,
                KvXrValueFields.Get(KvXrValueFieldId.SmoothSpeed).Max, 1e-6f, "скорость: верхняя граница");
            Assert.AreEqual(KvXrSettings.MinOperatorHeight,
                KvXrValueFields.Get(KvXrValueFieldId.OperatorHeight).Min, 1e-6f, "рост: нижняя граница");
            Assert.AreEqual(KvXrSettings.MaxOperatorHeight,
                KvXrValueFields.Get(KvXrValueFieldId.OperatorHeight).Max, 1e-6f, "рост: верхняя граница");
            Assert.AreEqual(KvXrSettings.MinTeleportDistance,
                KvXrValueFields.Get(KvXrValueFieldId.TeleportDistance).Min, 1e-6f, "телепорт: нижняя граница");
            Assert.AreEqual(KvXrSettings.MaxTeleportDistance,
                KvXrValueFields.Get(KvXrValueFieldId.TeleportDistance).Max, 1e-6f, "телепорт: верхняя граница");

            // Десктопные поля: границы живут прямо в сеттерах `KvSettings`
            // (`Mathf.Clamp(value, 0.6f, 1.6f)` и т. д., KvSettings.cs:77-94) — там констант нет,
            // поэтому числа сверяются с кодом сеттера.
            Assert.AreEqual(0.6f, KvXrValueFields.Get(KvXrValueFieldId.UiScale).Min, 1e-6f,
                "масштаб интерфейса: нижняя граница из сеттера KvSettings.UiScale");
            Assert.AreEqual(1.6f, KvXrValueFields.Get(KvXrValueFieldId.UiScale).Max, 1e-6f,
                "масштаб интерфейса: верхняя граница из сеттера KvSettings.UiScale");
            Assert.AreEqual(0f, KvXrValueFields.Get(KvXrValueFieldId.Density).Min, 1e-6f, "плотность: минимум");
            Assert.AreEqual(1f, KvXrValueFields.Get(KvXrValueFieldId.Density).Max, 1e-6f, "плотность: максимум");
            Assert.AreEqual(1f, KvXrValueFields.Get(KvXrValueFieldId.ToolbarRows).Min, 1e-6f, "ряды: минимум");
            Assert.AreEqual(6f, KvXrValueFields.Get(KvXrValueFieldId.ToolbarRows).Max, 1e-6f, "ряды: максимум");
            Assert.AreEqual(0f, KvXrValueFields.Get(KvXrValueFieldId.ToolbarLayout).Min, 1e-6f, "раскладка: минимум");
            Assert.AreEqual(2f, KvXrValueFields.Get(KvXrValueFieldId.ToolbarLayout).Max, 1e-6f, "раскладка: максимум");
            Assert.AreEqual(0f, KvXrValueFields.Get(KvXrValueFieldId.FontSize).Min, 1e-6f, "шрифт: минимум");
            Assert.AreEqual(2f, KvXrValueFields.Get(KvXrValueFieldId.FontSize).Max, 1e-6f, "шрифт: максимум");
            Assert.AreEqual(0f, KvXrValueFields.Get(KvXrValueFieldId.ColorBlind).Min, 1e-6f, "цветокоррекция: минимум");
            Assert.AreEqual(3f, KvXrValueFields.Get(KvXrValueFieldId.ColorBlind).Max, 1e-6f, "цветокоррекция: максимум");

            Assert.AreEqual(KvXrValueFields.Count, 10, "вводимых числовых полей ровно десять: четыре шлема + шесть компьютера");
        }

        /// <summary>
        /// СОХРАНЁННОЕ ЧИТАЕТСЯ ТЕМ ЖЕ API, ЧТО И НА КОМПЬЮТЕРЕ. Это и есть проверка [A2] шаг 6:
        /// «изменить значение в шлеме — значение то же». Своего хранилища у ввода нет.
        /// </summary>
        [Test]
        public void Commit_WritesThroughExistingSetter()
        {
            KvXrCommitOutcome vr = KvXrValueInput.Commit(
                KvXrValueFieldId.PanelRadius, "0.125", KvXrInputMethod.Keyboard);

            Assert.IsTrue(vr.Saved, "0,125 м — законное значение радиуса");
            Assert.AreEqual(0.125f, KvXrSettings.GetGroupPanelRadius(), 1e-4f,
                "значение обязано читаться геттером настроек шлема — тем же, что и на компьютере");
            Assert.AreEqual("[KvXr] ВВОД: клавиатура «0.125» → радиус панели 0.125 м (границы 0.080…0.420) · сохранено",
                vr.LogLine, "строка журнала об успешном вводе — из задания [S2] дословно");

            KvXrCommitOutcome desktop = KvXrValueInput.Commit(
                KvXrValueFieldId.UiScale, "1.15", KvXrInputMethod.Keyboard);

            Assert.IsTrue(desktop.Saved, "1,15 — законный масштаб интерфейса");
            Assert.AreEqual(1.15f, KvSettings.UiScale, 1e-4f,
                "настройка компьютера обязана читаться его же свойством `KvSettings.UiScale`");
        }

        /// <summary>
        /// ОТКАЗ НЕ МЕНЯЕТ ЗНАЧЕНИЕ — ни на единицу. Правило [A3]: ввод никогда не оставляет поле
        /// в неопределённом состоянии.
        /// </summary>
        [Test]
        public void Commit_RejectsWithoutChangingValue()
        {
            KvXrSettings.SetGroupPanelRadius(0.110f);
            float before = KvXrSettings.GetGroupPanelRadius();

            Assert.IsFalse(KvXrValueInput.Commit(KvXrValueFieldId.PanelRadius, "abc",
                KvXrInputMethod.Keyboard).Saved, "буквы отклоняются");
            Assert.IsFalse(KvXrValueInput.Commit(KvXrValueFieldId.PanelRadius, "9.9",
                KvXrInputMethod.Keyboard).Saved, "выход за границы отклоняется");
            Assert.IsFalse(KvXrValueInput.Commit(KvXrValueFieldId.PanelRadius, "",
                KvXrInputMethod.Keyboard).Saved, "пустая строка отклоняется");
            Assert.IsFalse(KvXrValueInput.Commit(KvXrValueFieldId.PanelRadius, "0.125000000000001",
                KvXrInputMethod.Keyboard).Saved, "слишком длинная строка отклоняется");

            Assert.AreEqual(before, KvXrSettings.GetGroupPanelRadius(), 1e-6f,
                "после любого отказа значение обязано остаться прежним");
        }

        // ============================================================ 2. ГОЛОС: РАЗБОР ЧИСЛА

        /// <summary>
        /// «СТО ДВАДЦАТЬ ПЯТЬ» → 125. Разбор произнесённого числа проверяется без микрофона — это
        /// единственный способ убедиться в нём до шлема.
        /// </summary>
        [Test]
        public void NumberWords_ParsesRussian()
        {
            float value;
            string reason;

            Assert.IsTrue(KvXrNumberWords.TryParse("сто двадцать пять", out value, out reason),
                "«сто двадцать пять» обязано разбираться");
            Assert.AreEqual(125f, value, 1e-3f, "«сто двадцать пять» = 125");

            Assert.IsTrue(KvXrNumberWords.TryParse("ноль целых сто двадцать пять тысячных", out value, out reason),
                "дробь словами обязана разбираться");
            Assert.AreEqual(0.125f, value, 1e-4f, "«ноль целых сто двадцать пять тысячных» = 0,125");

            Assert.IsTrue(KvXrNumberWords.TryParse("минус пять", out value, out reason),
                "минус словами обязана разбираться");
            Assert.AreEqual(-5f, value, 1e-3f, "«минус пять» = −5");

            Assert.IsTrue(KvXrNumberWords.TryParse("0.125", out value, out reason),
                "готовые цифры от распознавателя тоже принимаются");
            Assert.AreEqual(0.125f, value, 1e-4f, "«0.125» = 0,125");
        }

        /// <summary>НЕ ЧИСЛО — ОТКАЗ С ПРИЧИНОЙ. Молчаливый ноль в радиусе панели недопустим.</summary>
        [Test]
        public void NumberWords_RejectsNonNumber()
        {
            float value;
            string reason;

            Assert.IsFalse(KvXrNumberWords.TryParse("привет", out value, out reason),
                "«привет» — не число");
            Assert.IsFalse(string.IsNullOrEmpty(reason), "причина отказа обязана быть названа словами");

            Assert.IsFalse(KvXrNumberWords.TryParse("", out value, out reason),
                "пустая фраза — не число");
        }

        // ============================================================ 3. КЛАВИАТУРА

        /// <summary>ЧИСЛОВАЯ РАСКЛАДКА: десять цифр, точка, минус и три служебных ключа.</summary>
        [Test]
        public void KeyboardLayout_NumericHasAllKeys()
        {
            Assert.AreEqual(15, KvXrKeyboardLayout.KeyCount(KvXrKeyboardKind.Numeric),
                "числовых ключей пятнадцать: 10 цифр + точка + минус + отбой + ввод + отмена");

            for (char digit = '0'; digit <= '9'; digit++)
                Assert.GreaterOrEqual(KvXrKeyboardLayout.IndexOfSymbol(KvXrKeyboardKind.Numeric, digit), 0,
                    "на числовой клавиатуре нет цифры «" + digit + "»");

            Assert.GreaterOrEqual(KvXrKeyboardLayout.IndexOfSymbol(KvXrKeyboardKind.Numeric, '.'), 0,
                "на числовой клавиатуре нет точки");
            Assert.GreaterOrEqual(KvXrKeyboardLayout.IndexOfSymbol(KvXrKeyboardKind.Numeric, '-'), 0,
                "на числовой клавиатуре нет минуса");

            AssertKeyKind(KvXrKeyboardKind.Numeric, KvXrKeyKind.Backspace);
            AssertKeyKind(KvXrKeyboardKind.Numeric, KvXrKeyKind.Enter);
            AssertKeyKind(KvXrKeyboardKind.Numeric, KvXrKeyKind.Cancel);
        }

        /// <summary>
        /// ТЕКСТОВАЯ РАСКЛАДКА: буквы, цифры, пробел и скобки. Без пробела и скобок не набрать даже
        /// пример имени из задания — «Стенд 1 (левый)».
        /// </summary>
        [Test]
        public void KeyboardLayout_NameHasAllSymbols()
        {
            Assert.GreaterOrEqual(KvXrKeyboardLayout.KeyCount(KvXrKeyboardKind.Text), 52,
                "текстовых ключей не меньше 52: 33 буквы + 10 цифр + 6 знаков + 3 служебных");

            string alphabet = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            for (int i = 0; i < alphabet.Length; i++)
                Assert.GreaterOrEqual(KvXrKeyboardLayout.IndexOfSymbol(KvXrKeyboardKind.Text, alphabet[i]), 0,
                    "на текстовой клавиатуре нет буквы «" + alphabet[i] + "»");

            for (char digit = '0'; digit <= '9'; digit++)
                Assert.GreaterOrEqual(KvXrKeyboardLayout.IndexOfSymbol(KvXrKeyboardKind.Text, digit), 0,
                    "на текстовой клавиатуре нет цифры «" + digit + "»");

            char[] signs = { ' ', '(', ')', '.', '-', ',' };
            for (int i = 0; i < signs.Length; i++)
                Assert.GreaterOrEqual(KvXrKeyboardLayout.IndexOfSymbol(KvXrKeyboardKind.Text, signs[i]), 0,
                    "на текстовой клавиатуре нет знака «" + signs[i] + "»");

            AssertKeyKind(KvXrKeyboardKind.Text, KvXrKeyKind.Backspace);
            AssertKeyKind(KvXrKeyboardKind.Text, KvXrKeyKind.Enter);
            AssertKeyKind(KvXrKeyboardKind.Text, KvXrKeyKind.Cancel);
        }

        /// <summary>
        /// ВЛЕЗАЕТ В ПАНЕЛЬ И НЕ НАЛЕЗАЕТ САМА НА СЕБЯ. Ключ мельче 30 мм делать нельзя: радиус
        /// попадания луча = 0,85 × размер ключа, попадание станет лотереей (та же ловушка, что Б50).
        /// </summary>
        [Test]
        public void KeyboardLayout_KeysFitPanelAndDoNotCollide()
        {
            Assert.IsTrue(KvXrKeyboardLayout.FitsPanel(KvXrKeyboardKind.Numeric),
                "числовая клавиатура вылезает за рабочую полосу панели (" +
                KvXrKeyboardLayout.WidthMeters(KvXrKeyboardKind.Numeric).ToString("0.000") + " × " +
                KvXrKeyboardLayout.HeightMeters(KvXrKeyboardKind.Numeric).ToString("0.000") + " м)");
            Assert.IsTrue(KvXrKeyboardLayout.FitsPanel(KvXrKeyboardKind.Text),
                "текстовая клавиатура вылезает за рабочую полосу панели (" +
                KvXrKeyboardLayout.WidthMeters(KvXrKeyboardKind.Text).ToString("0.000") + " × " +
                KvXrKeyboardLayout.HeightMeters(KvXrKeyboardKind.Text).ToString("0.000") + " м)");

            Assert.IsFalse(KvXrKeyboardLayout.HasCollisions(KvXrKeyboardKind.Numeric),
                "два числовых ключа стоят в одной точке — луч нажмёт соседний");
            Assert.IsFalse(KvXrKeyboardLayout.HasCollisions(KvXrKeyboardKind.Text),
                "два текстовых ключа стоят в одной точке — луч нажмёт соседний");

            Assert.GreaterOrEqual(KvXrKeyboardLayout.NumericKeyMeters, 0.030f,
                "ключ мельче 30 мм не поймать лучом");
            Assert.GreaterOrEqual(KvXrKeyboardLayout.TextKeyMeters, 0.030f,
                "текстовый ключ мельче 30 мм не поймать лучом");
            Assert.Greater(KvXrKeyboardLayout.NumericPitchMeters, KvXrKeyboardLayout.NumericKeyMeters,
                "шаг обязан быть больше ключа, иначе ключи слипнутся");
            Assert.Greater(KvXrKeyboardLayout.TextPitchMeters, KvXrKeyboardLayout.TextKeyMeters,
                "шаг обязан быть больше ключа, иначе ключи слипнутся");
        }

        /// <summary>Служебный ключ такого типа обязан быть в раскладке.</summary>
        private static void AssertKeyKind(KvXrKeyboardKind kind, KvXrKeyKind wanted)
        {
            for (int i = 0; i < KvXrKeyboardLayout.KeyCount(kind); i++)
                if (KvXrKeyboardLayout.Key(kind, i).Kind == wanted) return;

            Assert.Fail("в раскладке " + kind + " нет ключа вида " + wanted);
        }

        // ============================================================ 4. КОЛЕСО СИМВОЛОВ

        /// <summary>КОЛЕСО: 13 секторов, цифры, точка, минус, «отбой» — и шаг по кругу без краёв.</summary>
        [Test]
        public void Wheel_HasAllSymbolsInOrder()
        {
            Assert.AreEqual(13, KvXrWheelLayout.SymbolCount,
                "секторов тринадцать: 10 цифр + точка + минус + отбой");

            for (int i = 0; i < 10; i++)
                Assert.AreEqual(i, KvXrWheelLayout.Symbol(i) - '0',
                    "сектор №" + i + " обязан быть цифрой " + i);
            Assert.AreEqual('.', KvXrWheelLayout.Symbol(10), "одиннадцатый сектор — точка");
            Assert.AreEqual('-', KvXrWheelLayout.Symbol(11), "двенадцатый сектор — минус");
            Assert.AreEqual(KvXrKeyKind.Backspace,
                KvXrWheelLayout.Symbol(12) == '\0' ? KvXrKeyKind.Backspace : KvXrKeyKind.Symbol,
                "тринадцатый сектор — отбой");

            Assert.AreEqual(0, KvXrWheelLayout.Step(12, 1), "с последнего сектора шаг вперёд возвращает к первому");
            Assert.AreEqual(12, KvXrWheelLayout.Step(0, -1), "с первого сектора шаг назад уходит к последнему");
            Assert.AreEqual(5, KvXrWheelLayout.Step(4, 1), "обычный шаг вперёд");

            for (int i = 0; i < KvXrWheelLayout.SymbolCount; i++)
            {
                float expected = i * (360f / KvXrWheelLayout.SymbolCount);
                Assert.AreEqual(expected, KvXrWheelLayout.AngleDegrees(i), 0.01f,
                    "угол сектора №" + i + " считается по кругу");
                Assert.AreEqual(i, KvXrWheelLayout.IndexAt(KvXrWheelLayout.OffsetMeters(i)),
                    "точка центра сектора №" + i + " обязана опознаваться как этот же сектор");
            }
        }

        /// <summary>
        /// СТИК ЗАХВАЧЕН, ПОКА ОТКРЫТ ВВОД КОЛЕСОМ. Иначе выбор символа будет ещё и разворачивать
        /// оператора: стик в шлеме занят поворотом шагами (`KvXrLocomotion.HandleSnapTurn`).
        /// </summary>
        [Test]
        public void Wheel_StickIsCapturedWhileInputOpen()
        {
            Assert.IsTrue(KvXrWheelLayout.CapturesStick(true, KvXrInputMethod.Wheel),
                "ввод колесом открыт — стик принадлежит колесу");
            Assert.IsFalse(KvXrWheelLayout.CapturesStick(false, KvXrInputMethod.Wheel),
                "ввод закрыт — стик возвращается локомоции");
            Assert.IsFalse(KvXrWheelLayout.CapturesStick(true, KvXrInputMethod.Keyboard),
                "клавиатура стик не занимает");
            Assert.IsFalse(KvXrWheelLayout.CapturesStick(true, KvXrInputMethod.Voice),
                "голос стик не занимает");
        }

        // ============================================================ 5. ПЕРЕКЛЮЧАТЕЛЬ СПОСОБА

        /// <summary>
        /// СПОСОБ ВВОДА ПЕРЕКЛЮЧАЕТСЯ И ЗАПОМИНАЕТСЯ (решение владельца R5: выбор — в шлеме).
        /// Строка журнала — из задания [S2] дословно.
        /// </summary>
        [Test]
        public void InputMethod_SwitchIsPersistedAndLogged()
        {
            KvXrInputMethodSetting.Set(KvXrInputMethod.Keyboard);
            Assert.AreEqual(KvXrInputMethod.Keyboard, KvXrInputMethodSetting.Get(),
                "по умолчанию и после записи — клавиатура");

            KvXrInputMethodSetting.Set(KvXrInputMethod.Voice);
            Assert.AreEqual(KvXrInputMethod.Voice, KvXrInputMethodSetting.Get(),
                "выбранный способ обязан читаться после записи (иначе он не запомнится между запусками)");

            Assert.AreEqual(KvXrInputMethod.Wheel, KvXrInputMethodSetting.Next(KvXrInputMethod.Voice),
                "переключатель идёт по кругу: голос → колесо");
            Assert.AreEqual(KvXrInputMethod.Keyboard, KvXrInputMethodSetting.Next(KvXrInputMethod.Wheel),
                "переключатель идёт по кругу: колесо → клавиатура");
            Assert.AreEqual(3, KvXrInputMethodSetting.Count, "способов ровно три");

            Assert.AreEqual("[KvXr] НАСТРОЙКА: способ ввода «голос» · было «клавиатура»",
                KvXrInputMethodSetting.SwitchLogLine(KvXrInputMethod.Voice, KvXrInputMethod.Keyboard),
                "строка журнала о переключении — из задания [S2] дословно");
        }

        // ============================================================ 6. КАРТОЧКА Б51: ЖУРНАЛ НЕ ВРЁТ

        /// <summary>
        /// ПОСЛЕ СМЕНЫ УРОВНЯ «ПРОЩЁННАЯ» ИКОНКА НЕ СЧИТАЕТСЯ НАВЕДЕНИЕМ (карточка Б51). В журнале
        /// приёмки §40 пять раз появлялось `ПАНЕЛЬ: луч навёл «» — 0.0 см`: номер прошлой иконки
        /// подставлялся в новую панель, где он означает другую кнопку или ничего.
        /// </summary>
        [Test]
        public void Aim_DoesNotForgiveAcrossRebuild()
        {
            Assert.IsFalse(KvXrAimGuard.KeepsForgivenTarget(false, true, 3, 13, "Настройки", 0.1f),
                "панель пересобрана — номер прошлой иконки смысла не имеет");
            Assert.IsTrue(KvXrAimGuard.KeepsForgivenTarget(false, false, 3, 13, "Настройки", 0.1f),
                "без пересборки и в пределах прощения дрожание прощается — иначе подпись и нажатие срываются");
            Assert.IsTrue(KvXrAimGuard.KeepsForgivenTarget(true, true, -1, 13, "Настройки", 0f),
                "реальное попадание верно всегда, даже сразу после пересборки");

            GameObject host = new GameObject("KvXrS41AimHost");
            try
            {
                var panel = host.AddComponent<KvXrGroupPanel>();
                panel.logOnBuild = false;
                panel.Rebuild();
                int before = panel.RebuildVersion;

                panel.OpenScreen(KvXrPanelLevel.SettingsScreen);

                Assert.Greater(panel.RebuildVersion, before,
                    "переход на экран пересобирает панель — номер пересборки обязан измениться, " +
                    "иначе указатель не узнает, что номера иконок сменили смысл");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// НОМЕР ВНЕ ДИАПАЗОНА, ПУСТОЕ НАЗВАНИЕ, ИСТЁКШЕЕ ПРОЩЕНИЕ — цель сбрасывается.
        /// Именно из этих трёх случаев и рождалась строка «луч навёл «»».
        /// </summary>
        [Test]
        public void Aim_RejectsOutOfRangeOrNamelessTarget()
        {
            Assert.IsFalse(KvXrAimGuard.KeepsForgivenTarget(false, false, 15, 13, "Настройки", 0.1f),
                "номер 15 при 13 целях — цели нет");
            Assert.IsFalse(KvXrAimGuard.KeepsForgivenTarget(false, false, 3, 13, "", 0.1f),
                "у номера нет названия — в журнал ушло бы «навёл «»», так нельзя");
            Assert.IsFalse(KvXrAimGuard.KeepsForgivenTarget(false, false, 3, 13, null, 0.1f),
                "названия нет вовсе — цель сбрасывается");
            Assert.IsFalse(KvXrAimGuard.KeepsForgivenTarget(false, false, -1, 13, "Настройки", 0.1f),
                "прощать нечего — цели нет");
            Assert.IsFalse(KvXrAimGuard.KeepsForgivenTarget(false, false, 3, 13, "Настройки", 0.9f),
                "прощение истекло (0,9 с > 0,5 с) — цель сбрасывается");
            Assert.IsTrue(KvXrAimGuard.KeepsForgivenTarget(false, false, 3, 13, "Настройки", 0.2f),
                "0,2 с — ещё в пределах прощения");
        }

        /// <summary>
        /// ЖУРНАЛ ПИШЕТ ЧЕСТНО: промах называется промахом. Пустого «навёл «»» быть не может.
        /// </summary>
        [Test]
        public void Aim_MissLogsHonestLine()
        {
            string miss = KvXrAimGuard.AimLogLine(false, "", 0f, 1.2f, "КОНТРОЛЛЕР (правая рука)");

            StringAssert.Contains("луч ушёл с иконки", miss,
                "при промахе журнал обязан сказать, что цель потеряна");
            StringAssert.DoesNotContain("навёл «»", miss,
                "строка «луч навёл «»» — это и есть карточка Б51, её быть не должно");

            string hit = KvXrAimGuard.AimLogLine(true, "Настройки", 0.012f, 1.2f, "КОНТРОЛЛЕР (правая рука)");

            Assert.AreEqual("[KvXr] ПАНЕЛЬ: луч навёл «Настройки» — 1.2 см от центра иконки · луч 1.20 м · " +
                            "источник КОНТРОЛЛЕР (правая рука)", hit,
                "строка наведения обязана называть иконку, расстояние и источник");
        }

        // ============================================================ 7. ТЕКСТ: ИМЕНА И ПОИСК

        /// <summary>
        /// ПРЕДЕЛ ИМЕНИ — КАК НА КОМПЬЮТЕРЕ (48 символов). На компьютере `KvTreeView.CommitRename`
        /// молча обрезает ввод на 48 символах (`KvTreeView.cs:679`); шлем обязан сделать то же,
        /// иначе одно имя будет принято в одном месте и отвергнуто в другом.
        /// </summary>
        [Test]
        public void TextRules_NameLimitsMatchDesktop()
        {
            var input = new KvXrTextInput();
            input.BeginText("Имя объекта", KvXrTextRules.MaxNameLength);

            Assert.IsTrue(input.IsActive, "ввод имени открыт");
            Assert.IsFalse(input.Numeric, "имя — не число: буквы разрешены");
            Assert.AreEqual(KvXrTextRules.MaxNameLength, input.MaxLength, "предел длины взят из правил");

            for (int i = 0; i < 60; i++) input.Append('а');

            Assert.AreEqual(48, input.Text.Length,
                "строка обязана остановиться на 48 символах — как на компьютере");
            Assert.AreEqual(48, KvXrTextRules.MaxNameLength,
                "предел имени 48 — ровно как в KvTreeView.CommitRename");

            input.Backspace();
            Assert.AreEqual(47, input.Text.Length, "отбой убирает последний символ");

            StringAssert.EndsWith(KvXrTextRules.Cursor.ToString(), input.Display,
                "в строке-кандидате виден курсор");
        }

        /// <summary>ПУСТОЕ ИМЯ НЕ ПРИНИМАЕТСЯ; пробелы по краям не считаются; управляющие — отбрасываются.</summary>
        [Test]
        public void TextRules_NameRejectsEmptyAndControlChars()
        {
            Assert.IsFalse(KvXrTextRules.IsAcceptableName(""), "пустое имя не принимается");
            Assert.IsFalse(KvXrTextRules.IsAcceptableName("   "), "одни пробелы — не имя");
            Assert.IsFalse(KvXrTextRules.IsAcceptableName(null), "отсутствие строки — не имя");
            Assert.IsFalse(KvXrTextRules.IsAcceptableName("Стенд\u0007"), "управляющие символы отбрасываются");
            Assert.IsTrue(KvXrTextRules.IsAcceptableName("Стенд 1 (левый)"),
                "имя из задания обязано приниматься — вместе со скобками и пробелом");

            Assert.IsTrue(KvXrTextRules.AcceptsNumberCharacter('7'), "цифра — можно");
            Assert.IsTrue(KvXrTextRules.AcceptsNumberCharacter('.'), "точка — можно");
            Assert.IsTrue(KvXrTextRules.AcceptsNumberCharacter('-'), "минус — можно");
            Assert.IsFalse(KvXrTextRules.AcceptsNumberCharacter('а'), "буква в число — нельзя");
            Assert.IsFalse(KvXrTextRules.AcceptsNumberCharacter(' '), "пробел в число — нельзя");

            Assert.IsTrue(KvXrTextRules.AcceptsTextCharacter('а'), "буква в имя — можно");
            Assert.IsTrue(KvXrTextRules.AcceptsTextCharacter(' '), "пробел в имя — можно");
            Assert.IsTrue(KvXrTextRules.AcceptsTextCharacter('('), "скобка в имя — можно");
            Assert.IsFalse(KvXrTextRules.AcceptsTextCharacter('\u0007'), "управляющий символ — нельзя");
        }

        /// <summary>
        /// ИМЕНА В ШЛЕМЕ — ИЗ ТОГО ЖЕ ХРАНИЛИЩА, ЧТО И НА КОМПЬЮТЕРЕ. Компьютер хранит ярлык узла в
        /// `NodeLabels` по ключу узла; шлем обязан читать оттуда же, иначе переименование в шлеме и на
        /// компьютере разойдётся ([K4]). На диск не пишется ничего — поведение компьютера сохранено.
        /// </summary>
        [Test]
        public void Labels_ShownInObjectsScreenComeFromDesktopStore()
        {
            var node = new ProjectNode("robot:stand1", "Робот_6ос_Стенд1", ProjectNodeKind.Robot, null, null);
            NodeLabels.Set(node, "Стенд 1 (левый)");

            Assert.AreEqual("Стенд 1 (левый)", NodeLabels.Get(node.Key, node.DisplayName),
                "ярлык читается из того же хранилища, что и на компьютере");

            List<string> rows = KvXrScreenModel.BuildObjects(new[] { "Стенд 1 (левый)" }, 0);
            Assert.IsTrue(rows.Count > 0 && rows[0].StartsWith("►"),
                "выбранная строка на экране «Объекты» помечена — экран показывает ярлык, а не имя объекта");
        }

        // ============================================================ 8. ПОИСК ПО КОМАНДАМ

        /// <summary>СОЗДАТЬ КОМАНДУ ДЛЯ ТЕСТА (реестр команд не трогается: поиск берёт список снаружи).</summary>
        private static KvCommand Command(string id, string title, string description = "")
        {
            return new KvCommand { Id = id, Title = title, Description = description };
        }

        /// <summary>
        /// ПОИСК ИДЁТ ПО ТЕМ ЖЕ СЕМИ ПОЛЯМ, ЧТО И Ctrl+P НА КОМПЬЮТЕРЕ: id, заголовок, пояснение,
        /// горячая клавиша, переводы и название группы. Пропущенное поле = «в шлеме не находится».
        ///
        /// ИСПРАВЛЕНО 24.10.2026 С РАЗРЕШЕНИЯ ВЛАДЕЛЬЦА ([K5]). Прежняя редакция брала команду
        /// `point.select` и ждала, что по слову «берёт» она НЕ найдётся по группе, а по исходному
        /// пояснению — найдётся. Оба ожидания неверны, потому что у этой команды есть перевод
        /// пояснения (`ru.json`: «ЛКМ фиксирует точку и запускает расчёт траекторий») и есть группа
        /// «Точка и траектория» (`KvToolbarGroups.cs:61-64`) — палитра компьютера обязана находить её
        /// по обоим. Поэтому здесь взята команда ВНЕ групп и БЕЗ перевода: на ней проверяется ровно
        /// то, что задумано. Код поиска не менялся — он и до, и после совпадает с палитрой.
        /// </summary>
        [Test]
        public void Search_MatchesSevenFields()
        {
            var byId = new KvCommand { Id = "t.demo.desc", Title = "Выбрать точку", Description = "берёт точку" };
            var byHotkey = new KvCommand { Id = "view.workspace", Title = "Рабочая зона", Hotkey = "F7" };

            Assert.IsTrue(KvCommandSearch.Matches(byId, "demo"), "поиск по id команды");
            Assert.IsTrue(KvCommandSearch.Matches(byId, "выбрать"), "поиск по заголовку");
            Assert.IsTrue(KvCommandSearch.Matches(byId, "берёт"), "поиск по пояснению (перевода нет — берётся исходное)");
            Assert.IsTrue(KvCommandSearch.Matches(byHotkey, "f7"), "поиск по горячей клавише, без учёта регистра");
            Assert.IsFalse(KvCommandSearch.Matches(byId, "траектория"), "чего нет — того не находим");

            // КОМАНДА ИЗ ГРУППЫ НАХОДИТСЯ ПО НАЗВАНИЮ ГРУППЫ — как в палитре компьютера.
            string realId = KvToolbarGroups.All[0].Commands[0];
            string groupTitle = KvToolbarGroups.GroupTitleOf(realId);
            Assert.IsFalse(string.IsNullOrEmpty(groupTitle), "у команды из группы есть название группы");
            Assert.IsTrue(KvCommandSearch.Matches(Command(realId, "что угодно"), groupTitle),
                "поиск по названию группы");
        }

        /// <summary>
        /// НАХОДИТСЯ ВСЁ, ЧТО СОВПАЛО, И ТОЛЬКО ОНО. Пропуск или лишняя строка означали бы, что
        /// шлем и компьютер показывают разные списки.
        /// </summary>
        [Test]
        public void Search_ReturnsEveryMatchAndOnlyMatches()
        {
            var list = new List<KvCommand>
            {
                Command("robot.six", "Робот 6-осевой"),
                Command("view.workspace", "Рабочая зона"),
                Command("robot.scara", "Робот SCARA"),
                Command("file.save", "Сохранить")
            };

            List<KvCommand> found = KvCommandSearch.Filter(list, "робот");

            Assert.AreEqual(2, found.Count, "по запросу «робот» находятся ровно две команды");
            for (int i = 0; i < found.Count; i++)
                Assert.IsTrue(KvCommandSearch.Matches(found[i], "робот"),
                    "в выдачу попала команда, которая не совпала с запросом: " + found[i].Id);

            for (int i = 0; i < list.Count; i++)
                if (KvCommandSearch.Matches(list[i], "робот"))
                    CollectionAssert.Contains(found, list[i],
                        "совпавшая команда не попала в выдачу: " + list[i].Id);

            Assert.AreEqual(list.Count, KvCommandSearch.Filter(list, "").Count,
                "пустой запрос показывает все команды — как палитра компьютера");
            Assert.AreEqual(0, KvCommandSearch.Filter(list, "ъъъ").Count,
                "запрос без совпадений даёт пустой список, а не «всё подряд»");
        }

        /// <summary>
        /// ПОРЯДОК КАК НА КОМПЬЮТЕРЕ: команды, чей заголовок НАЧИНАЕТСЯ с запроса, идут первыми и
        /// отсортированы по заголовку; остальные совпадения — в порядке реестра.
        /// </summary>
        [Test]
        public void Search_StartsTitleFirstThenRegistryOrder()
        {
            var list = new List<KvCommand>
            {
                Command("a.first", "Проверить робота"),      // совпадение в середине
                Command("b.starts", "Робот домой"),          // начинается с запроса
                Command("c.starts", "Робот стоп"),           // начинается с запроса
                Command("d.last", "Стоп робота")             // совпадение в середине
            };

            List<KvCommand> found = KvCommandSearch.Filter(list, "робот");

            Assert.AreEqual(4, found.Count, "совпали все четыре");
            Assert.AreEqual("b.starts", found[0].Id, "первыми идут «начинается с», по алфавиту: «Робот домой»");
            Assert.AreEqual("c.starts", found[1].Id, "второй — «Робот стоп»");
            Assert.AreEqual("a.first", found[2].Id, "затем прочие совпадения в порядке реестра");
            Assert.AreEqual("d.last", found[3].Id, "и последнее совпадение — тоже в порядке реестра");
        }

        /// <summary>НА ЭКРАН ПОМЕЩАЕТСЯ НЕ БОЛЬШЕ 12 СТРОК — как в палитре компьютера.</summary>
        [Test]
        public void Search_LimitIsTwelve()
        {
            Assert.AreEqual(12, KvCommandSearch.MaxRows, "на экране поиска двенадцать строк, как в палитре");

            var many = new List<KvCommand>();
            for (int i = 0; i < 20; i++) many.Add(Command("cmd." + i, "Команда номер " + i));

            Assert.AreEqual(20, KvCommandSearch.Filter(many, "команда").Count,
                "поиск отдаёт все совпадения; ограничение 12 — это ПОКАЗ, а не отбор");
        }

        /// <summary>
        /// НЕДОСТУПНАЯ КОМАНДА НЕ ВЫПОЛНЯЕТСЯ, и это говорится вслух. Строки журнала — из [S2].
        /// </summary>
        [Test]
        public void Search_DisabledCommandIsNotExecuted()
        {
            Assert.IsTrue(KvCommandSearch.CanExecute(Command("t.ok", "Доступная")),
                "обычная команда выполнима");
            Assert.IsFalse(KvCommandSearch.CanExecute(
                    new KvCommand { Id = "t.stub", Title = "Заглушка", Stub = true }),
                "заглушка не выполняется");
            Assert.IsFalse(KvCommandSearch.CanExecute(
                    new KvCommand { Id = "t.off", Title = "Выключенная", IsEnabled = () => false }),
                "команда, которая сама себя запретила, не выполняется");

            Assert.AreEqual("[KvXr] ПОИСК: «пауза» → найдено 3", KvCommandSearch.SearchLogLine("пауза", 3),
                "строка журнала о поиске — из задания [S2] дословно");
            Assert.AreEqual("[KvXr] ПОИСК: «пауза» → найдено 3 · выполнена «Запуск / пауза»",
                KvCommandSearch.ExecuteLogLine("пауза", 3, "Запуск / пауза"),
                "строка журнала о выполнении — из задания [S2] дословно");
        }

        // ============================================================ 9. ЭКРАНЫ ВВОДА В ПАНЕЛИ

        /// <summary>
        /// НА ЭКРАНЕ ВВОДА КАЖДЫЙ КЛЮЧ КЛАВИАТУРЫ ЛОВИТСЯ ЛУЧОМ. Правило §K («иконка = цель»)
        /// действует и здесь: клавиатура, которую видно, но нельзя нажать, повторила бы карточку Б50.
        /// </summary>
        [Test]
        public void InputScreen_KeyboardKeysAreRayTargets()
        {
            GameObject host = new GameObject("KvXrS41InputHost");
            try
            {
                var panel = host.AddComponent<KvXrGroupPanel>();
                panel.logOnBuild = false;
                panel.Rebuild();
                panel.OpenScreen(KvXrPanelLevel.InputScreen);

                Assert.AreEqual(KvXrPanelLevel.InputScreen, panel.Level, "экран ввода обязан открыться");
                Assert.AreEqual(panel.IconCount, panel.RayTargetCount,
                    "на экране ввода целей луча обязано быть столько же, сколько иконок");

                List<Vector2> targets = new List<Vector2>();
                for (int i = 0; i < panel.RayTargetCount; i++) targets.Add(panel.RayTargetOffset(i));

                int keys = KvXrKeyboardLayout.KeyCount(KvXrKeyboardKind.Numeric);
                for (int i = 0; i < keys; i++)
                {
                    KvXrKey key = KvXrKeyboardLayout.Key(KvXrKeyboardKind.Numeric, i);
                    bool found = false;
                    for (int t = 0; t < targets.Count; t++)
                        if (Mathf.Abs(targets[t].x - key.OffsetMeters.x) <= 0.0005f &&
                            Mathf.Abs(targets[t].y - key.OffsetMeters.y) <= 0.0005f) { found = true; break; }

                    Assert.IsTrue(found, "ключ «" + key.Label + "» нарисован, но лучом не ловится " +
                        "(смещение " + key.OffsetMeters.x.ToString("0.000") + "; " +
                        key.OffsetMeters.y.ToString("0.000") + " м)");
                }

                Assert.IsTrue(HasTarget(targets, 0f, -0.29f), "на экране ввода обязана ловиться кнопка «Закрыть»");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// ЭКРАН «ПОИСК» С ТЕКСТОВОЙ КЛАВИАТУРОЙ: буква и «ввод» ловятся лучом, «Закрыть» на месте.
        /// </summary>
        [Test]
        public void SearchScreen_IsScreenWithTextKeyboard()
        {
            Assert.IsTrue(KvXrGroupPanel.IsScreen(KvXrPanelLevel.SearchScreen),
                "«Поиск» — экран, а не уровень групп");
            Assert.IsTrue(KvXrGroupPanel.IsScreen(KvXrPanelLevel.InputScreen),
                "«Ввод» — экран, а не уровень групп");

            GameObject host = new GameObject("KvXrS41SearchHost");
            try
            {
                var panel = host.AddComponent<KvXrGroupPanel>();
                panel.logOnBuild = false;
                panel.Rebuild();
                panel.OpenScreen(KvXrPanelLevel.SearchScreen);

                Assert.AreEqual(KvXrPanelLevel.SearchScreen, panel.Level, "экран поиска обязан открыться");
                Assert.AreEqual(panel.IconCount, panel.RayTargetCount,
                    "на экране поиска целей луча обязано быть столько же, сколько иконок");

                List<Vector2> targets = new List<Vector2>();
                for (int i = 0; i < panel.RayTargetCount; i++) targets.Add(panel.RayTargetOffset(i));

                KvXrKey letter = KvXrKeyboardLayout.Key(KvXrKeyboardKind.Text,
                    KvXrKeyboardLayout.IndexOfSymbol(KvXrKeyboardKind.Text, 'а'));
                AssertKeyIsTarget(targets, letter, "буква «а»");

                KvXrKey enter = default(KvXrKey);
                for (int i = 0; i < KvXrKeyboardLayout.KeyCount(KvXrKeyboardKind.Text); i++)
                    if (KvXrKeyboardLayout.Key(KvXrKeyboardKind.Text, i).Kind == KvXrKeyKind.Enter)
                        enter = KvXrKeyboardLayout.Key(KvXrKeyboardKind.Text, i);
                AssertKeyIsTarget(targets, enter, "ключ «ввод»");

                Assert.IsTrue(HasTarget(targets, 0f, -0.29f), "на экране поиска обязана ловиться кнопка «Закрыть»");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>Ключ клавиатуры присутствует в списке целей луча.</summary>
        private static void AssertKeyIsTarget(List<Vector2> targets, KvXrKey key, string what)
        {
            for (int t = 0; t < targets.Count; t++)
                if (Mathf.Abs(targets[t].x - key.OffsetMeters.x) <= 0.0005f &&
                    Mathf.Abs(targets[t].y - key.OffsetMeters.y) <= 0.0005f) return;

            Assert.Fail(what + " нарисован, но лучом не ловится (смещение " +
                        key.OffsetMeters.x.ToString("0.000") + "; " +
                        key.OffsetMeters.y.ToString("0.000") + " м)");
        }

        /// <summary>Есть ли цель с такими координатами (метры).</summary>
        private static bool HasTarget(IReadOnlyList<Vector2> targets, float x, float y)
        {
            for (int i = 0; i < targets.Count; i++)
                if (Mathf.Abs(targets[i].x - x) <= 0.0005f &&
                    Mathf.Abs(targets[i].y - y) <= 0.0005f) return true;
            return false;
        }
    }
}
