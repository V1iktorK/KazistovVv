// ================================================================================================
//  KvUiShellBehaviourTests.cs — ПОВЕДЕНЧЕСКИЕ тесты оболочки интерфейса (P4-8):
//  `06_KazistovVv_UI\03_Scripts\Core` и `...\Zones`.
//
//  ОТКУДА ЗАДАЧА. Хранилище настроек, раскладка окон, режим курсора и навигация с клавиатуры —
//  это то, чем оператор пользуется каждый день, и то, что ломается ТИХО: значение записалось, а не
//  прочиталось; окна «уехали» после перезапуска; мышь осталась захваченной. Проверялось это поиском
//  строк в исходнике, то есть никак.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ ПО РЕЗУЛЬТАТУ:
//    1) `KvSettings`: записал → прочитал; границы значений;
//    2) `KvLayoutStore`: сохранил раскладку → прочитал её же; сброс возвращает «сохранённого нет»;
//    3) `KvMouseCursor`: повторное применение режима не теряет состояние (правило «разошлось ли»);
//    4) `KvKeyboardNav`: ход по кругу и прижатие к границам списка;
//    5) `KvSegmented`/`KvWidgets`: индекс в границах вариантов, эталон размеров без провалов.
//
//  ЧЕСТНО О ПОБОЧНЫХ ЭФФЕКТАХ: тесты, трогающие `PlayerPrefs`, СОХРАНЯЮТ прежние значения и
//  возвращают их в `finally`. Иначе прогон оставил бы на машине тестировщика чужой масштаб
//  интерфейса — и следующая проверка «на глаз» показывала бы не то, что настроил человек.
// ================================================================================================

using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Поведение оболочки: настройки, раскладка, курсор, навигация, виджеты.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvUiShellBehaviourTests
    {
        // ---------------------------------------------------------------- KvSettings

        /// <summary>
        /// ЗАПИСАЛ → ПРОЧИТАЛ: значение настройки возвращается тем же.
        ///
        /// ЗАЧЕМ. `KvSettings` — единственное хранилище читаемости интерфейса (масштаб, плотность,
        /// кегль, контраст). Если запись и чтение разъедутся (разные ключи, разный тип, забытый
        /// `PlayerPrefs.Save`), наладчик настраивает читаемость, а на следующем запуске видит
        /// заводские значения — «настройки не работают».
        ///
        /// Проверяются ТРИ поля разного типа: `float` (масштаб), `int` (плотность), `bool` (контраст) —
        /// именно на типах и разъезжаются ключи.
        /// </summary>
        [Test]
        public void Settings_ValueSurvivesSetAndReload()
        {
            float oldScale = KvSettings.UiScale;
            int oldDensity = KvSettings.Density;
            bool oldContrast = KvSettings.HighContrast;
            try
            {
                KvSettings.UiScale = 1.15f;
                KvSettings.Density = 1;
                KvSettings.HighContrast = true;

                Assert.AreEqual(1.15f, KvSettings.UiScale, 1e-4f, "масштаб интерфейса не прочитался обратно");
                Assert.AreEqual(1, KvSettings.Density, "плотность строк не прочиталась обратно");
                Assert.IsTrue(KvSettings.HighContrast, "режим контраста не прочитался обратно");

                // ПЕРЕЗАПУСК ПРИЛОЖЕНИЯ = повторное чтение из хранилища.
                KvSettings.Reload();
                Assert.AreEqual(1.15f, KvSettings.UiScale, 1e-4f,
                    "после перечитывания настроек масштаб потерялся — значит он не сохранён");
                Assert.AreEqual(1, KvSettings.Density, "после перечитывания потеряна плотность строк");
                Assert.IsTrue(KvSettings.HighContrast, "после перечитывания потерян режим контраста");
            }
            finally
            {
                KvSettings.UiScale = oldScale;
                KvSettings.Density = oldDensity;
                KvSettings.HighContrast = oldContrast;
            }
        }

        /// <summary>
        /// ГРАНИЦЫ ЗНАЧЕНИЙ: мусор в настройке не должен попадать в интерфейс.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. Значение приходит из `PlayerPrefs` (его можно испортить руками) и из
        /// кода панелей. Масштаб 0 или 50 делает интерфейс нечитаемым, а плотность 7 — несуществующей
        /// веткой отрисовки. Отдельно проверяется, что границы ВКЛЮЧИТЕЛЬНЫ: «ровно 1,6» обязано
        /// остаться 1,6, иначе настройку нельзя выставить на максимум.
        /// </summary>
        [Test]
        public void Settings_OutOfRangeValuesAreClamped()
        {
            float oldScale = KvSettings.UiScale;
            int oldDensity = KvSettings.Density;
            int oldFont = KvSettings.FontSize;
            int oldLayout = KvSettings.ToolbarLayout;
            int oldColorBlind = KvSettings.ColorBlind;
            try
            {
                KvSettings.UiScale = 50f;
                Assert.LessOrEqual(KvSettings.UiScale, 1.6f + 1e-3f, "масштаб интерфейса выше верхней границы");
                KvSettings.UiScale = 0f;
                Assert.GreaterOrEqual(KvSettings.UiScale, 0.6f - 1e-3f, "масштаб интерфейса ниже нижней границы");

                KvSettings.Density = 7;
                Assert.AreEqual(1, KvSettings.Density, "плотность строк вне диапазона 0…1 не прижалась");
                KvSettings.FontSize = -3;
                Assert.AreEqual(0, KvSettings.FontSize, "кегль шрифта вне диапазона 0…2 не прижался");
                KvSettings.ToolbarLayout = 99;
                Assert.AreEqual(2, KvSettings.ToolbarLayout, "раскладка тулбара вне диапазона 0…2 не прижалась");
                KvSettings.ColorBlind = 42;
                Assert.AreEqual(3, KvSettings.ColorBlind,
                    "схема для дальтоников вне диапазона 0…3 не прижалась — панель покажет несуществующий тип");
            }
            finally
            {
                KvSettings.UiScale = oldScale;
                KvSettings.Density = oldDensity;
                KvSettings.FontSize = oldFont;
                KvSettings.ToolbarLayout = oldLayout;
                KvSettings.ColorBlind = oldColorBlind;
            }
        }

        // ---------------------------------------------------------------- KvLayoutStore

        /// <summary>
        /// РАСКЛАДКА ОКНА: сохранил → прочитал ту же самую → сбросил → «сохранённого нет».
        ///
        /// ЗАЧЕМ ПЕРВАЯ ЧАСТЬ. Смысл хранилища — «при следующем запуске окна стоят на своих местах».
        /// Пропущенное поле (например `Rail` — «полочка» или `Visible`) читается как значение по
        /// умолчанию, и панель, которую оператор свернул в полочку, при следующем запуске
        /// разворачивается на пол-экрана. Проверяются ВСЕ поля: «почти все» здесь равно «не работает».
        ///
        /// ЗАЧЕМ ВТОРАЯ. Кнопка «Сбросить раскладку» обещает заводское расположение окон. Если после
        /// сброса `Load` продолжает отдавать старые числа, нажатие выглядит как «кнопка не работает».
        ///
        /// Ключ — свой (`KvP4TestPanel...`): тест не трогает раскладку рабочего места и убирает за
        /// собой в `finally`.
        /// </summary>
        [Test]
        public void LayoutStore_SaveLoadAndResetRoundTrip()
        {
            const string key = "KvP4TestPanel";
            const string clearedKey = "KvP4TestPanelCleared";
            try
            {
                var layout = new KvPanelLayout
                {
                    Side = 2,
                    Thickness = 301f,
                    Collapsed = true,
                    Visible = false,
                    Rail = true,
                    X = 123f,
                    Y = 45f,
                    Width = 411f,
                    Height = 190f
                };

                KvLayoutStore.Save(key, layout);
                KvLayoutStore.Flush();

                KvPanelLayout loaded = KvLayoutStore.Load(key);
                Assert.IsNotNull(loaded, "сохранённая раскладка не прочиталась — окна «уедут» после перезапуска");
                Assert.AreEqual(2, loaded.Side, "край панели не сохранился");
                Assert.AreEqual(301f, loaded.Thickness, 1e-3f, "толщина панели не сохранилась");
                Assert.IsTrue(loaded.Collapsed, "состояние «свёрнута» не сохранилось");
                Assert.IsFalse(loaded.Visible, "состояние «скрыта» не сохранилось");
                Assert.IsTrue(loaded.Rail, "состояние «полочка» не сохранилось — панель развернётся сама");
                Assert.AreEqual(123f, loaded.X, 1e-3f, "позиция окна X не сохранилась");
                Assert.AreEqual(45f, loaded.Y, 1e-3f, "позиция окна Y не сохранилась");
                Assert.AreEqual(411f, loaded.Width, 1e-3f, "ширина окна не сохранилась");
                Assert.AreEqual(190f, loaded.Height, 1e-3f, "высота окна не сохранилась");

                // СБРОС: сохранённого быть не должно.
                KvLayoutStore.Save(clearedKey, new KvPanelLayout { Side = 1, Thickness = 350f });
                Assert.IsNotNull(KvLayoutStore.Load(clearedKey),
                    "подготовка: раскладка обязана читаться сразу после сохранения");

                KvLayoutStore.Clear(new[] { clearedKey });

                Assert.IsNull(KvLayoutStore.Load(clearedKey),
                    "после сброса раскладка всё ещё читается — окна останутся там, где их оставили");
            }
            finally
            {
                KvLayoutStore.Clear(new[] { key, clearedKey });
            }
        }

        // ---------------------------------------------------------------- KvMouseCursor

        /// <summary>
        /// ПОВТОРНОЕ ПРИМЕНЕНИЕ РЕЖИМА НЕ ТЕРЯЕТ СОСТОЯНИЕ. Главный тест курсора.
        ///
        /// ЧТО БЫЛО (ФИКС 1 §23). Редактор Unity сам снимает захват по Esc и при потере фокуса окна, а
        /// сторонний код меняет `Cursor.*` напрямую. Отсюда «после работы с роботом мышь не
        /// восстанавливается»: модель режима говорила одно, системный курсор — другое, и никто не
        /// замечал расхождения.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Правило «разошлось ли фактическое состояние с режимом» — то самое, по
        /// которому `Apply` возвращает курсор назад. Ошибка в любую сторону одинаково плоха: если
        /// признак не срабатывает, курсор остаётся скрытым (оператор «теряет мышь»); если срабатывает
        /// всегда — курсор дёргается каждый кадр и счётчик восстановлений превращается в шум.
        /// </summary>
        [Test]
        public void MouseCursor_ReapplyKeepsModeState()
        {
            // Режим камеры: мышь захвачена, системный курсор скрыт.
            Assert.AreEqual(CursorLockMode.Locked, KvMouseCursor.WantedLockState(true),
                "в режиме камеры мышь обязана быть захвачена");
            Assert.IsFalse(KvMouseCursor.WantedVisible(true),
                "в режиме камеры системный курсор обязан быть скрыт (§26)");

            // Режим интерфейса: мышь свободна, курсор виден.
            Assert.AreEqual(CursorLockMode.None, KvMouseCursor.WantedLockState(false),
                "в режиме интерфейса мышь обязана быть свободна");
            Assert.IsTrue(KvMouseCursor.WantedVisible(false),
                "в режиме интерфейса обязан показываться СИСТЕМНЫЙ курсор Unity");

            // Всё совпало — трогать нечего.
            Assert.IsFalse(KvMouseCursor.CursorStateDrifted(CursorLockMode.None, true, false),
                "в режиме интерфейса всё на месте, а признак дрейфа сработал: курсор будет дёргаться каждый кадр");
            Assert.IsFalse(KvMouseCursor.CursorStateDrifted(CursorLockMode.Locked, false, true),
                "в режиме камеры всё на месте, а признак дрейфа сработал");

            // Кто-то снял захват (Esc редактора, потеря фокуса) — расхождение ОБЯЗАНО быть замечено.
            Assert.IsTrue(KvMouseCursor.CursorStateDrifted(CursorLockMode.None, false, true),
                "захват снят извне, а расхождение не замечено — мышь останется свободной в телеоперации");
            Assert.IsTrue(KvMouseCursor.CursorStateDrifted(CursorLockMode.Locked, true, false),
                "курсор показан в режиме интерфейса, но мышь захвачена — расхождение не замечено");
        }

        // ---------------------------------------------------------------- KvKeyboardNav

        /// <summary>
        /// ХОД ПО КРУГУ, ПРИЖАТИЕ К ГРАНИЦАМ И КРУГ ОБЛАСТЕЙ — правила навигации с клавиатуры.
        ///
        /// ЗАЧЕМ. Кнопки тулбара ходят ПО КРУГУ (с последней стрелка вправо ведёт на первую), а
        /// пункты меню и узлы дерева — С ПРИЖАТИЕМ (на последнем пункте стрелка вниз ничего не
        /// делает). Если перепутать эти правила, выделение «прыгает» с конца списка в начало, и
        /// оператор, идущий стрелкой вниз по дереву, оказывается наверху, не заметив этого.
        /// Областей навигации ровно три (тулбар → меню → дерево), и F6 обязана обойти ВСЕ: пока
        /// переключение было записано числом по месту, ошибка «F6 ходит по двум областям»
        /// выяснялась только надетым шлемом.
        ///
        /// Отдельно проверяется `count = 0`: пустая панель не должна давать индекс 0, иначе
        /// `focusButtons[0]` — исключение прямо в кадре.
        /// </summary>
        [Test]
        public void KeyboardNav_WrapsToolbarClampsListsAndCyclesScopes()
        {
            Assert.AreEqual(0, KvKeyboardNav.WrapIndex(-1, 1, 5), "первая стрелка ставит фокус на первую кнопку");
            Assert.AreEqual(0, KvKeyboardNav.WrapIndex(4, 1, 5), "с последней кнопки вправо — на первую (по кругу)");
            Assert.AreEqual(4, KvKeyboardNav.WrapIndex(0, -1, 5), "с первой кнопки влево — на последнюю (по кругу)");
            Assert.AreEqual(-1, KvKeyboardNav.WrapIndex(0, 1, 0), "пустой панели не соответствует ни один индекс");

            Assert.AreEqual(0, KvKeyboardNav.StepInList(-1, 1, 4), "из «фокуса нет» шаг вниз встаёт на первый пункт");
            Assert.AreEqual(3, KvKeyboardNav.StepInList(3, 1, 4), "на последнем пункте шаг вниз остаётся на нём");
            Assert.AreEqual(0, KvKeyboardNav.StepInList(0, -1, 4), "на первом пункте шаг вверх остаётся на нём");
            Assert.AreEqual(2, KvKeyboardNav.StepInList(1, 1, 4), "обычный шаг вниз сдвигает на один пункт");
            Assert.AreEqual(-1, KvKeyboardNav.StepInList(0, 1, 0), "пустого списка не соответствует ни один индекс");

            KvKeyboardNav.NavScope first = KvKeyboardNav.NextScopeOf(KvKeyboardNav.NavScope.Toolbar);
            KvKeyboardNav.NavScope second = KvKeyboardNav.NextScopeOf(first);
            KvKeyboardNav.NavScope third = KvKeyboardNav.NextScopeOf(second);

            Assert.AreEqual(KvKeyboardNav.NavScope.Menu, first, "из тулбара F6 обязана вести в меню");
            Assert.AreEqual(KvKeyboardNav.NavScope.Tree, second, "из меню F6 обязана вести в дерево");
            Assert.AreEqual(KvKeyboardNav.NavScope.Toolbar, third,
                "из дерева F6 обязана возвращать в тулбар, иначе одна из областей недостижима");
        }

        // ---------------------------------------------------------------- KvSegmented / KvWidgets

        /// <summary>
        /// СЕГМЕНТНЫЙ ПЕРЕКЛЮЧАТЕЛЬ: ИНДЕКС В ГРАНИЦАХ + ЭТАЛОН РАЗМЕРОВ ВИДЖЕТОВ.
        ///
        /// ЧАСТЬ 1 — ИНДЕКС. Он приходит из `PlayerPrefs` и из кода панелей. Значение за пределами
        /// списка (сохранено при другом числе вариантов) не подсвечивает НИ ОДИН сегмент:
        /// переключатель «Масштаб: 1× | 1,15× | 1,3×» выглядит сломанным — значения записаны, а
        /// выбранного пункта не видно.
        ///
        /// ЧАСТЬ 2 — ЭТАЛОН. `KvWidgets.ValidateSizes()` — настоящая самопроверка (галка 16×16, глиф
        /// 0,78 от квадрата, кнопка-иконка из единственного источника `KvSettings.IconButtonSize`,
        /// вкладка 24…28). Раньше этот эталон проверялся ГЛАЗАМИ оператора по скриншотам («слишком
        /// большие квадратики»): самопроверка была написана, но её итог никто не читал. Здесь он
        /// обязателен.
        /// </summary>
        [Test]
        public void Segmented_IndexIsClampedAndWidgetStandardHolds()
        {
            Assert.AreEqual(-1, KvSegmented.ClampIndex(0, 0), "у переключателя без сегментов нет выбранного");
            Assert.AreEqual(0, KvSegmented.ClampIndex(-5, 3), "отрицательный индекс прижимается к первому сегменту");
            Assert.AreEqual(1, KvSegmented.ClampIndex(1, 3), "нормальный индекс не смещается");
            Assert.AreEqual(2, KvSegmented.ClampIndex(9, 3),
                "индекс за списком прижимается к последнему сегменту, а не остаётся невидимым");

            int failures = KvWidgets.ValidateSizesFailures();
            Assert.AreEqual(0, failures,
                "эталон размеров интерфейса нарушен (" + failures + " провалов). Отчёт: " +
                KvWidgets.ValidateSizes());

            // Живой размер кнопки-иконки обязан приходить ИЗ НАСТРОЕК ПЛОТНОСТИ, а не из константы:
            // иначе тулбар и дерево разъезжаются на 24 против 28 (P3-8).
            Assert.AreEqual(KvSettings.IconButtonSize, KvWidgets.IconButtonSize, 1e-4f,
                "сторона кнопки-иконки взята не из единственного источника (`KvSettings`)");
        }
    }
}
