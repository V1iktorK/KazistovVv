using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ ПРАВОК §41 ПОСЛЕ ПРИЁМКИ 24.10.2026: стартовое меню, попадание по всей кнопке, метка в
    /// точке касания, цвета лазеров.
    ///
    /// ЗДЕСЬ ЖЕ ЗАКРЕПЛЕНЫ РЕШЕНИЯ, КОТОРЫЕ ИНАЧЕ ПРИШЛОСЬ БЫ СПРАШИВАТЬ СЛОВАМИ (указание владельца:
    /// «переспрашивай, но в виде теста»). Если какое-то из них неверно — падает конкретный тест, и
    /// видно, какое именно число или какое поведение надо поменять.
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ:
    ///   • Д1 — попадание луча по ВСЕЙ показанной кнопке, а не кругом 25 мм в её центре;
    ///   • Д2 — журнал называет новые экраны своими именами;
    ///   • П2 — метка стоит в точке касания луча, а не в центре иконки;
    ///   • П3 — правый лазер зелёный, левый красный (как на компьютере), наведение не меняет цвет руки;
    ///   • П1 — стартовое меню: шесть разделов, строки в панели не налезают, строки ловятся лучом.
    /// </summary>
    public class KvXrS41MenuTests
    {
        private const float ToleranceMeters = 0.0005f;

        // ============================================================ Д1: ПОПАДАНИЕ ПО ВСЕЙ КНОПКЕ

        /// <summary>Панель в памяти, открытая на нужном экране.</summary>
        private static KvXrGroupPanel NewPanel(KvXrPanelLevel level, out GameObject host)
        {
            host = new GameObject("KvXrS41MenuTestHost");
            var panel = host.AddComponent<KvXrGroupPanel>();
            panel.logOnBuild = false;
            panel.followHand = false;
            panel.Rebuild();
            if (level != KvXrPanelLevel.Groups) panel.OpenScreen(level);
            return panel;
        }

        /// <summary>Номер цели по её названию (−1 — нет такой).</summary>
        private static int IndexOf(KvXrGroupPanel panel, string title)
        {
            for (int i = 0; i < panel.RayTargetCount; i++)
                if (panel.TitleAt(i) == title) return i;
            return -1;
        }

        /// <summary>
        /// Д1. КНОПКА ЛОВИТСЯ ПО ВСЕЙ СВОЕЙ ПЛОЩАДИ, А НЕ КРУГОМ В ЦЕНТРЕ.
        ///
        /// Доказательство дефекта из журнала приёмки: наведение «Ввести значение» зафиксировано на
        /// 2,4 см от центра при радиусе попадания 2,55 см — то есть на самой границе, а строки
        /// шириной 360 мм не наведены ни разу. Владелец: «Всё потыкал. Ничего не работает».
        ///
        /// Проверка: луч идёт в точку на 4,5 см правее центра кнопки «Ввести значение» (кнопка
        /// нарисована шириной 100 мм, значит эта точка — внутри неё) — и обязан попасть в неё.
        /// Старое правило (круг 25,5 мм) эту точку не ловило.
        /// </summary>
        [Test]
        public void WideButton_HitAnywhereOnIt()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(KvXrPanelLevel.SettingsScreen, out host);
            try
            {
                int index = IndexOf(panel, "Ввести значение");
                Assert.GreaterOrEqual(index, 0, "на экране «Настройки» нет кнопки «Ввести значение»");

                Vector2 half = panel.RayTargetHalfSize(index);
                Assert.Greater(half.x, 0.03f,
                    "половина ширины цели " + half.x.ToString("0.000") +
                    " м — кнопка нарисована шириной 100 мм, значит цель обязана быть не уже");

                Vector3 center = panel.IconWorldPosition(index);

                // Точка внутри кнопки, но ЗА пределами старого круга (0,0255 м).
                Vector3 origin = center + Vector3.right * 0.045f + Vector3.forward * 0.40f;
                int hit;
                float fromCenter;
                float along;
                Vector3 point;
                bool aimed = panel.TryAim(origin, -Vector3.forward, out hit, out fromCenter, out along, out point);

                Assert.IsTrue(aimed, "луч прошёл мимо кнопки, хотя целился в её площадь (4,5 см от центра)");
                Assert.AreEqual(index, hit, "попал не в ту кнопку");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Д1, ВТОРАЯ ЧАСТЬ: чувствительность ПРЕЖНИХ иконок не уменьшилась. Круглое попадание
        /// осталось запасным путём — иначе владелец потерял бы то, что уже принял.
        /// </summary>
        [Test]
        public void SmallIcon_KeepsItsRoundingTolerance()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(KvXrPanelLevel.Groups, out host);
            try
            {
                int index = 0;
                Vector3 center = panel.IconWorldPosition(index);
                Vector2 half = panel.RayTargetHalfSize(index);

                // Точка чуть ЗА прямоугольником, но в пределах круга 0,85 × иконки.
                float outsideX = half.x + 0.004f;
                Vector3 origin = center + Vector3.right * outsideX + Vector3.forward * 0.40f;

                int hit;
                float fromCenter;
                float along;
                Vector3 point;
                bool aimed = panel.TryAim(origin, -Vector3.forward, out hit, out fromCenter, out along, out point);

                Assert.IsTrue(aimed,
                    "точка на " + outsideX.ToString("0.000") + " м в стороне перестала ловиться: " +
                    "запасной круг радиусом 0,85 × иконки должен остаться");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ============================================================ Д2: ЖУРНАЛ НЕ ВРЁТ

        /// <summary>
        /// Д2. ЖУРНАЛ НАЗЫВАЕТ НОВЫЕ ЭКРАНЫ СВОИМИ ИМЕНАМИ. В приёмке 24.10.2026 открытие
        /// «Интерфейса компьютера» записалось как «ЭКРАН: открыт «иконки групп»».
        /// </summary>
        [Test]
        public void ScreenTitle_NamesNewScreens()
        {
            Assert.AreEqual("Ввод", KvXrGroupPanel.ScreenTitle(KvXrPanelLevel.InputScreen), "экран ввода");
            Assert.AreEqual("Поиск", KvXrGroupPanel.ScreenTitle(KvXrPanelLevel.SearchScreen), "экран поиска");
            Assert.AreEqual("Интерфейс компьютера", KvXrGroupPanel.ScreenTitle(KvXrPanelLevel.InterfaceScreen),
                "экран настроек интерфейса компьютера");
            Assert.AreEqual("Стартовое меню", KvXrGroupPanel.ScreenTitle(KvXrPanelLevel.StartMenu),
                "стартовое меню");

            Assert.AreNotEqual("иконки групп", KvXrGroupPanel.ScreenTitle(KvXrPanelLevel.InputScreen),
                "название «иконки групп» для экрана — это и есть дефект Д2");
        }

        // ============================================================ П2: МЕТКА В ТОЧКЕ КАСАНИЯ

        /// <summary>
        /// П2. МЕТКА СТОИТ ТАМ, ГДЕ ЛУЧ КАСАЕТСЯ ПАНЕЛИ, А НЕ В ЦЕНТРЕ ИКОНКИ.
        ///
        /// Слова владельца: «зелёное пятно не в точке соприкосновения указателя правого контроллера».
        /// Правило §39 (метка в центре иконки) отменено: длина луча до цели равна расстоянию до
        /// плоскости панели, поэтому метка совпадает с точкой касания.
        /// </summary>
        [Test]
        public void Cursor_SitsAtRayContactPoint()
        {
            Assert.AreEqual(0.19f, KvXrCursor.RayLengthMeters(true, 0.19f, 10f), 1e-4f,
                "при попадании метка обязана стоять на расстоянии до плоскости панели, а не у центра иконки");

            Assert.AreEqual(10f, KvXrCursor.RayLengthMeters(false, 0.19f, 10f), 1e-4f,
                "при промахе луч идёт на полную длину — это осталось как было");
        }

        // ============================================================ П3: ЦВЕТА ЛАЗЕРОВ

        /// <summary>
        /// П3. ЦВЕТА ЛАЗЕРОВ КАК НА КОМПЬЮТЕРЕ: правая рука — зелёный, левая — красный.
        /// Источник: компьютерные инструменты «Выбор траектории (зелёный лазер)» — правая рука,
        /// «Выбор точки (красный лазер)» — левая (`KazistovVvUIManager.cs:838-870`).
        /// </summary>
        [Test]
        public void Laser_RightIsGreenLeftIsRed()
        {
            Color right = KvXrLaserColors.Base(KvXrQuickHand.Right);
            Color left = KvXrLaserColors.Base(KvXrQuickHand.Left);

            Assert.Greater(right.g, right.r, "правый лазер обязан быть зелёным (зелёного больше красного)");
            Assert.Greater(right.g, right.b, "правый лазер: зелёного больше синего");
            Assert.Greater(left.r, left.g, "левый лазер обязан быть красным (красного больше зелёного)");
            Assert.Greater(left.r, left.b, "левый лазер: красного больше синего");

            Assert.AreEqual("зелёный", KvXrLaserColors.Label(KvXrQuickHand.Right), "название цвета правой руки");
            Assert.AreEqual("красный", KvXrLaserColors.Label(KvXrQuickHand.Left), "название цвета левой руки");
        }

        /// <summary>
        /// НАВЕДЕНИЕ НЕ МЕНЯЕТ ЦВЕТ РУКИ: тот же оттенок, только плотнее.
        ///
        /// ПОПРАВЛЕНО 24.10.2026 (разрешение владельца: «переспрашивай, но в виде теста»). Прежняя
        /// редакция сравнивала ЯРКОСТЬ канала: ждала, что зелёный на цели станет «зелёнее» единицы.
        /// Это невозможно — у зелёного лазера канал уже равен 1,0 (`KvTheme.LaserGreen`), и поднять
        /// его нечем. Ощущение «ярче» в шлеме даёт плотность луча (альфа 0,55 → 0,95), её и проверяем,
        /// а цвет руки остаётся на своей стороне спектра.
        /// </summary>
        [Test]
        public void Laser_HoverKeepsHandColorButBrighter()
        {
            Color idle = KvXrLaserColors.Ray(KvXrQuickHand.Right, false);
            Color hover = KvXrLaserColors.Ray(KvXrQuickHand.Right, true);

            Assert.Greater(hover.a, idle.a, "на цели луч плотнее (альфа 0,55 → 0,95)");
            Assert.Greater(hover.g, hover.r, "и остаётся зелёным — в голубой не перекрашивается");
            Assert.Greater(hover.g, hover.b, "правый на цели: зелёного больше синего");

            Color leftIdle = KvXrLaserColors.Ray(KvXrQuickHand.Left, false);
            Color leftHover = KvXrLaserColors.Ray(KvXrQuickHand.Left, true);
            Assert.Greater(leftHover.a, leftIdle.a, "левый на цели тоже плотнее");
            Assert.Greater(leftHover.r, leftHover.g, "и остаётся красным");
        }

        /// <summary>ВТОРОЙ ЛУЧ — ЦВЕТА ПРОТИВОПОЛОЖНОЙ РУКИ (указатель другой руки).</summary>
        [Test]
        public void Laser_SecondaryRayUsesOppositeHand()
        {
            Assert.AreEqual(KvXrQuickHand.Left, KvXrLaserColors.Opposite(KvXrQuickHand.Right),
                "нажимает правая — второй луч левый");
            Assert.AreEqual(KvXrQuickHand.Right, KvXrLaserColors.Opposite(KvXrQuickHand.Left),
                "нажимает левая — второй луч правый");
        }

        // ============================================================ П1: СТАРТОВОЕ МЕНЮ

        /// <summary>
        /// П1. ШЕСТЬ РАЗДЕЛОВ В ЗАДАННОМ ПОРЯДКЕ: Стол, Робот, Проект, Обучение, Настройки, Выход.
        /// Слова владельца: «функционал берём из стартового меню в комп версии… где можно будет
        /// выбрать стол и робота», вид — как настройки PICO (разделы слева).
        /// </summary>
        [Test]
        public void StartMenu_HasSixSectionsInOrder()
        {
            string[] expected = { "Стол", "Робот", "Проект", "Обучение", "Настройки", "Выход" };

            Assert.AreEqual(expected.Length, KvXrStartMenuLayout.SectionCount, "разделов шесть");
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], KvXrStartMenuLayout.SectionTitle(i), "раздел №" + i);
        }

        /// <summary>
        /// П1. РАСКЛАДКА МЕНЮ: всё внутри панели 0,90 × 0,60 м, строки не налезают друг на друга,
        /// колонка разделов не залезает в колонку содержимого.
        /// </summary>
        [Test]
        public void StartMenu_FitsPanelAndDoesNotOverlap()
        {
            Assert.IsTrue(KvXrStartMenuLayout.FitsPanel(),
                "часть меню вылезает за панель " + KvXrStartMenuLayout.WidthMeters.ToString("0.00") + " × " +
                KvXrStartMenuLayout.HeightMeters.ToString("0.00") + " м");
            Assert.IsTrue(KvXrStartMenuLayout.ContentRowsDoNotOverlap(),
                "строки содержимого налезают друг на друга — луч нажмёт соседнюю");
            Assert.IsTrue(KvXrStartMenuLayout.SectionRowsDoNotOverlap(),
                "строки разделов налезают друг на друга");
            Assert.IsTrue(KvXrStartMenuLayout.ColumnsDoNotOverlap(),
                "колонка разделов и колонка содержимого пересекаются");

            Assert.Greater(KvXrStartMenuLayout.ContentHalfSize.x, KvXrStartMenuLayout.ContentHalfSize.y,
                "строка содержимого — широкая кнопка, значит и цель у неё широкая (дефект Д1)");
        }

        /// <summary>
        /// П1. СТРОКИ МЕНЮ ЛОВЯТСЯ ЛУЧОМ по всей площади: у цели тот же размер, что и у нарисованной
        /// строки, и «иконок = целей» — правило §K действует и в меню.
        /// </summary>
        [Test]
        public void StartMenu_RowsAreRayTargets()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(KvXrPanelLevel.Groups, out host);
            try
            {
                panel.OpenStartMenu();

                Assert.AreEqual(KvXrPanelLevel.StartMenu, panel.Level, "меню обязано открыться");
                Assert.AreEqual(panel.IconCount, panel.RayTargetCount,
                    "в меню целей луча обязано быть столько же, сколько иконок");

                int row = IndexOf(panel, "Запустить стенд (добавить стол и роботов)");
                Assert.GreaterOrEqual(row, 0, "в разделе «Стол» нет строки добавления стенда");

                Vector2 half = panel.RayTargetHalfSize(row);
                Assert.AreEqual(KvXrStartMenuLayout.ContentHalfSize.x, half.x, ToleranceMeters,
                    "половина ширины цели строки обязана совпадать с нарисованной строкой");
                Assert.AreEqual(KvXrStartMenuLayout.ContentHalfSize.y, half.y, ToleranceMeters,
                    "половина высоты цели строки обязана совпадать с нарисованной строкой");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ============================================================ ОВЕРЛЕЙ И СКРУГЛЕНИЕ

        /// <summary>
        /// ОВЕРЛЕЙ НЕ ПРОЗРАЧЕН ДЛЯ ЛУЧА. Указание владельца 24.10.2026: «вне кнопок лазер пролетает
        /// сквозь оверлей». Притяжение к кнопкам остаётся, но луч обязан упираться в саму плиту:
        /// точка внутри меню, но мимо строк — это попадание в плиту.
        /// </summary>
        [Test]
        public void Overlay_StopsRayOutsideButtons()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(KvXrPanelLevel.Groups, out host);
            try
            {
                panel.OpenStartMenu();

                Vector2 half = panel.PanelSurfaceHalfSize;
                Assert.AreEqual(KvXrStartMenuLayout.WidthMeters * 0.5f, half.x, ToleranceMeters,
                    "плита меню обязана быть той же ширины, что и нарисованная");
                Assert.AreEqual(KvXrStartMenuLayout.HeightMeters * 0.5f, half.y, ToleranceMeters,
                    "плита меню обязана быть той же высоты, что и нарисованная");

                // Точка ВНУТРИ плиты, но между строками содержимого (там кнопок нет).
                Vector3 inside = panel.transform.position + Vector3.right * 0.40f + Vector3.up * 0.28f;

                float along;
                bool hitSurface = panel.TryHitPanelSurface(inside + Vector3.forward * 0.5f, -Vector3.forward, out along);

                Assert.IsTrue(hitSurface, "луч в пустое место плиты не упирается в оверлей — пролетает сквозь");
                Assert.AreEqual(0.5f, along, 0.02f, "расстояние до плиты обязано быть расстоянием до плоскости меню");

                // А точка ВНЕ плиты — не попадание: мимо меню луч идёт как обычно.
                Vector3 outside = panel.transform.position + Vector3.right * 0.60f;
                float outsideAlong;
                Assert.IsFalse(panel.TryHitPanelSurface(outside + Vector3.forward * 0.5f, -Vector3.forward, out outsideAlong),
                    "мимо плиты луч не должен обрываться");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// УГЛЫ МЕНЮ СКРУГЛЕНЫ. Указание владельца: «углы надо скруглить». Скругление делается
        /// спрайтом с границами («девять частей»), иначе на широкой плите углы вытянулись бы в овалы.
        /// </summary>
        [Test]
        public void Overlay_RoundedCornersSprite()
        {
            Sprite rounded = KvXrRoundedSprite.Get();

            Assert.IsNotNull(rounded, "скруглённый спрайт не создан");
            Assert.Greater(rounded.border.x, 0f, "у спрайта нет границы — скругление растянется в овал");
            Assert.AreEqual(rounded.border.x, rounded.border.y, 1e-4f, "границы спрайта обязаны быть одинаковыми");
            Assert.AreSame(rounded, KvXrRoundedSprite.Get(), "спрайт создаётся один раз, а не каждый кадр");
        }

        // ============================================================ РЕЕСТР ПАНЕЛЕЙ (ОКНА)

        /// <summary>
        /// ЛУЧ ДОЛЖЕН ВИДЕТЬ ВСЕ ПАНЕЛИ: панель у руки, стартовое меню и окна. Указание владельца
        /// 24.10.2026: кнопки выдают ОКНА, которые не крепятся ко взгляду. Значит панелей становится
        /// несколько, и реестр обязан знать, какая из них «у руки».
        /// </summary>
        [Test]
        public void Panels_RegistryKnowsHandPanel()
        {
            KvXrPanels.Clear();

            GameObject hostA = new GameObject("KvXrPanelHand");
            GameObject hostB = new GameObject("KvXrPanelWindow");
            try
            {
                var hand = hostA.AddComponent<KvXrGroupPanel>();
                hand.followHand = true;
                hand.logOnBuild = false;

                var window = hostB.AddComponent<KvXrGroupPanel>();
                window.followHand = false;
                window.logOnBuild = false;

                KvXrPanels.Register(hand);
                KvXrPanels.Register(window);

                Assert.AreEqual(2, KvXrPanels.Count, "обе панели на учёте");
                Assert.AreSame(hand, KvXrPanels.HandPanel, "панелью у руки считается та, что следует за кистью");
                Assert.AreSame(hand, KvXrPanels.At(0), "порядок регистрации сохраняется");

                KvXrPanels.Unregister(hand);
                Assert.AreEqual(1, KvXrPanels.Count, "снятая панель выбывает из реестра");
                Assert.IsNull(KvXrPanels.HandPanel, "панели у руки больше нет");
            }
            finally
            {
                KvXrPanels.Clear();
                Object.DestroyImmediate(hostA);
                Object.DestroyImmediate(hostB);
            }
        }

        /// <summary>
        /// П1. РАЗДЕЛЫ ПЕРЕКЛЮЧАЮТСЯ, И У КАЖДОГО РАЗДЕЛА СВОЙ СПИСОК СТРОК. «Выход» — две строки
        /// (вернуться к работе / выйти), «Настройки» — две (шлем / интерфейс компьютера).
        /// </summary>
        [Test]
        public void StartMenu_SectionsSwitchAndKeepTheirRows()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(KvXrPanelLevel.Groups, out host);
            try
            {
                panel.OpenStartMenu();
                Assert.AreEqual(KvXrStartSection.Tables, panel.StartSection, "меню открывается на «Столе»");

                panel.SelectStartSection((int)KvXrStartSection.Settings);
                Assert.AreEqual(KvXrStartSection.Settings, panel.StartSection, "раздел «Настройки» выбран");
                Assert.GreaterOrEqual(IndexOf(panel, "Настройки шлема"), 0, "нет строки «Настройки шлема»");
                Assert.GreaterOrEqual(IndexOf(panel, "Интерфейс компьютера"), 0,
                    "нет строки «Интерфейс компьютера»");

                panel.SelectStartSection((int)KvXrStartSection.Exit);
                Assert.AreEqual(KvXrStartSection.Exit, panel.StartSection, "раздел «Выход» выбран");
                Assert.GreaterOrEqual(IndexOf(panel, "Вернуться к работе"), 0, "нет строки возврата");
                Assert.GreaterOrEqual(IndexOf(panel, "Выйти из приложения"), 0, "нет строки выхода");

                // шесть разделов + две строки + «Закрыть»
                Assert.AreEqual(KvXrStartMenuLayout.SectionCount + 2 + 1, panel.IconCount,
                    "в разделе «Выход» обязано быть шесть разделов, две строки и «Закрыть»");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
