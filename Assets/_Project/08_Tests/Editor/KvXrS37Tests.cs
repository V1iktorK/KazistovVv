using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ СЕССИИ §37 — МОДЕЛИ КОНТРОЛЛЕРОВ, РАДИУС ПАНЕЛИ, РУКА ЛУЧА, КИСТИ, ЖЕСТЫ, СТОРОНЫ РУК.
    ///
    /// ЧТО ЗДЕСЬ ПРОВЕРЯЕТСЯ И ПОЧЕМУ ИМЕННО ЭТО. Всё, что можно проверить БЕЗ ШЛЕМА, проверяется
    /// здесь — до сборки, а не после. Предметы взяты из задания владельца [S1] и из того, что
    /// показал ЖИВОЙ шлем 21.10.2026:
    ///
    ///   1) РАДИУС ПАНЕЛИ. Владелец выбрал вариант 3 (0,100 м → иконка 27 мм), затем попросил
    ///      иконки 30 мм — это радиус 0,110 м при просвете 8,4 мм. Числа обязаны получаться ИЗ
    ///      ФОРМУЛЫ, а не «на глаз» ([S3.7]), и иконки не должны слипаться.
    ///
    ///   2) РУКА ЛУЧА. По умолчанию ПРАВАЯ, переключается настройкой, и рука панели от неё НЕ зависит.
    ///
    ///   3) ПОДСТРОЙКА БЕЗ ПЕРЕСБОРКИ (`KvXrTuning`). Разбор `ключ=значение` — чистая функция:
    ///      мусор, комментарии и значения вне диапазона не должны ни падать, ни применяться.
    ///
    ///   4) СТОРОНЫ РУК ПО ГЕОМЕТРИИ (`KvXrSideResolver`). На живом шлеме метки рантайма оказались
    ///      ПРОТИВОПОЛОЖНЫ положению рук (правая рука была на 5 см левее оси взгляда), поэтому
    ///      сторона теперь определяется по положению относительно головы — это и проверяется.
    ///
    ///   5) КИСТИ И ЖЕСТЫ. Ловушка [F2]: «нулевая поза» приходит как достоверная — сначала отказ от
    ///      неё. Дальше распознавание по СГИБАМ ПАЛЬЦЕВ (математика XR Hands): щипок, указательный
    ///      палец, раскрытая ладонь; кулак и расслабленная кисть не дают жестов; удержание, пауза,
    ///      требование нейтрального положения.
    ///
    ///   6) МОДЕЛИ КОНТРОЛЛЕРОВ. Отказ от нулевой позы и путь к мешу SDK.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ И БЫТЬ НЕ МОЖЕТ: проверки «шлем отдал кисти» или «модель видна глазами».
    /// Это подтверждается только строкой живого журнала и взглядом владельца в шлеме.
    /// </summary>
    public class KvXrS37Tests
    {
        // ============================================================ 1. РАДИУС ПАНЕЛИ (§37.2)

        /// <summary>
        /// ВЫБРАННЫЙ ВЛАДЕЛЬЦЕМ РАДИУС ДАЁТ ВЫБРАННЫЕ ЖЕ ИКОНКИ. Сначала это был вариант 3 (0,100 м
        /// → иконка 27 мм), после первого взгляда в шлем владелец попросил 30 мм — это 0,110 м.
        /// Если формула изменится, это увидит тест, а не владелец в шлеме.
        /// </summary>
        [Test]
        public void Radius_ChosenVariants_GiveExpectedIconSize()
        {
            float icon100 = KvXrGroupPanelLayout.IconSizeForCount(8, KvXrGroupPanelLayout.GroupSpanDegrees, 0.100f);
            float gap100 = KvXrGroupPanelLayout.GapForCount(8, KvXrGroupPanelLayout.GroupSpanDegrees, 0.100f);
            Assert.AreEqual(0.027f, icon100, 0.002f, "радиус 0.100 м → иконка ≈27 мм");
            Assert.AreEqual(0.008f, gap100, 0.0015f, "радиус 0.100 м → просвет ≈8 мм");

            float icon110 = KvXrGroupPanelLayout.IconSizeForCount(8, KvXrGroupPanelLayout.GroupSpanDegrees, 0.110f);
            float gap110 = KvXrGroupPanelLayout.GapForCount(8, KvXrGroupPanelLayout.GroupSpanDegrees, 0.110f);
            Assert.AreEqual(0.030f, icon110, 0.002f, "радиус 0.110 м → иконка ≈30 мм (просьба владельца)");
            Assert.GreaterOrEqual(gap110, KvXrGroupPanelLayout.MinGapForSmallIcons - 1e-4f,
                "просвет при иконке 30 мм не должен падать ниже 8 мм");
        }

        /// <summary>Радиус по умолчанию — 0,110 м (иконка 30 мм), и это записано константой.</summary>
        [Test]
        public void Radius_DefaultIsTheOwnerChoice()
        {
            Assert.AreEqual(0.110f, KvXrGroupPanelLayout.DefaultPanelRadiusMeters, 1e-4f,
                "радиус по умолчанию обязан совпадать с выбором владельца");
        }

        /// <summary>
        /// ДВА ПРЕЖНИХ ВАРИАНТА — ТОЖЕ ПО ФОРМУЛЕ: 0,125 м → 34 мм, 0,155 м → 42 мм (это ровно
        /// сегодняшняя иконка 42 мм при прежнем радиусе 0,155 м — значит формула согласована с тем,
        /// что владелец уже видел в шлеме).
        /// </summary>
        [Test]
        public void Radius_OtherVariants_MatchPromisedNumbers()
        {
            Assert.AreEqual(0.034f,
                KvXrGroupPanelLayout.IconSizeForCount(8, KvXrGroupPanelLayout.GroupSpanDegrees, 0.125f),
                0.002f, "радиус 0.125 м → иконка ≈34 мм");
            Assert.AreEqual(0.042f,
                KvXrGroupPanelLayout.IconSizeForCount(8, KvXrGroupPanelLayout.GroupSpanDegrees, 0.155f),
                0.002f, "радиус 0.155 м → иконка ≈42 мм (прежняя картинка)");
        }

        /// <summary>
        /// ИКОНКИ НЕ СЛИПАЮТСЯ НИ НА ОДНОМ РАДИУСЕ ИЗ ДОПУСТИМОГО ДИАПАЗОНА — требование владельца
        /// «важно, чтобы они не сливались и оставались видимыми для луча».
        /// </summary>
        [Test]
        public void Radius_ForEveryAllowedValue_IconsDoNotOverlap()
        {
            var radii = new[] { 0.080f, 0.090f, 0.100f, 0.110f, 0.125f, 0.155f, 0.200f, 0.420f };
            foreach (float r in radii)
            {
                Assert.IsTrue(KvXrGroupPanelLayout.IconsDoNotOverlap(8, KvXrGroupPanelLayout.GroupSpanDegrees, r),
                    "на радиусе " + r.ToString("0.000") + " м иконки групп слипаются");

                Assert.IsTrue(KvXrGroupPanelLayout.IconsDoNotOverlap(14, KvXrGroupPanelLayout.FanSpanDegrees,
                        KvXrGroupPanelLayout.FanRadiusMeters(14, r)),
                    "на радиусе " + r.ToString("0.000") + " м иконки веера (14) слипаются");
            }
        }

        /// <summary>РАДИУС ОГРАНИЧЕН ДОПУСТИМЫМ ДИАПАЗОНОМ — значение «на глаз» из файла не пройдёт.</summary>
        [Test]
        public void Radius_IsClampedToAllowedRange()
        {
            Assert.AreEqual(KvXrGroupPanelLayout.MinPanelRadiusMeters,
                KvXrGroupPanelLayout.ClampPanelRadius(0.001f), 1e-5f, "слишком малый радиус не ограничен");
            Assert.AreEqual(KvXrGroupPanelLayout.MaxPanelRadiusMeters,
                KvXrGroupPanelLayout.ClampPanelRadius(9f), 1e-5f, "слишком большой радиус не ограничен");
            Assert.AreEqual(0.110f, KvXrGroupPanelLayout.ClampPanelRadius(0.110f), 1e-5f,
                "допустимое значение не должно меняться");
        }

        /// <summary>
        /// ВЕЕР НЕ МЕЛЬЧЕ ЧИТАЕМОГО: 14 иконок на выбранном радиусе дали бы иконку мельче читаемой,
        /// поэтому радиус веера растёт — и это НЕ отменяет выбранный радиус для уровня групп.
        /// </summary>
        [Test]
        public void Fan_RadiusGrowsSoIconsStayReadable()
        {
            float fanRadius = KvXrGroupPanelLayout.FanRadiusMeters(14, 0.110f);
            float icon = KvXrGroupPanelLayout.IconSizeForCount(14, KvXrGroupPanelLayout.FanSpanDegrees, fanRadius);

            Assert.Greater(fanRadius, 0.110f, "для 14 иконок веера выбранного радиуса недостаточно");
            Assert.GreaterOrEqual(icon, KvXrGroupPanelLayout.MinFanIconSizeMeters - 1e-4f,
                "иконка веера мельче читаемого предела");
            Assert.AreEqual(0.110f, KvXrGroupPanelLayout.FanRadiusMeters(8, 0.110f), 1e-5f,
                "для 8 иконок веера выбранный радиус не должен расти");
        }

        /// <summary>
        /// ПРЕЖНЯЯ РАСКЛАДКА НЕ ИЗМЕНИЛАСЬ. Тесты §35–§36 запускались на `RadiusForCount(8, 160°)`
        /// и на `GroupOffsets(hand, 8)`; их поведение обязано остаться прежним ([S3.8]).
        /// </summary>
        [Test]
        public void Radius_OldLayoutApiIsUnchanged()
        {
            float radius = KvXrGroupPanelLayout.RadiusForCount(8, KvXrGroupPanelLayout.GroupSpanDegrees);
            Assert.AreEqual(0.155f, radius, 0.003f, "прежний расчёт радиуса изменился — это сломает тесты §35–§36");

            Vector2[] offsets = KvXrGroupPanelLayout.GroupOffsets(KvXrQuickHand.Left, 8);
            Assert.AreEqual(8, offsets.Length, "прежняя раскладка групп изменила число иконок");
            Assert.IsTrue(KvXrGroupPanelLayout.IconsDoNotOverlap(8, KvXrGroupPanelLayout.GroupSpanDegrees, radius),
                "прежняя раскладка начала слипаться");
        }

        /// <summary>РАСКЛАДКА ОТ РАДИУСА ДЕЙСТВИТЕЛЬНО СЖИМАЕТСЯ — иначе «уменьшил радиус» ничего не меняет.</summary>
        [Test]
        public void Radius_OffsetsFollowGivenRadius()
        {
            Vector2[] wide = KvXrGroupPanelLayout.GroupOffsetsForRadius(8, KvXrQuickHand.Left, 0.155f);
            Vector2[] narrow = KvXrGroupPanelLayout.GroupOffsetsForRadius(8, KvXrQuickHand.Left, 0.100f);

            Assert.AreEqual(8, wide.Length);
            Assert.AreEqual(8, narrow.Length);

            Assert.Greater(MaxAbs(wide), MaxAbs(narrow), "с уменьшением радиуса иконки не приблизились к центру");
            Assert.AreEqual(0.100f, MaxAbs(narrow), 0.002f, "самая дальняя иконка не стоит на заданном радиусе");
        }

        /// <summary>Строка чисел для журнала: радиус, иконка и просвет в мм.</summary>
        [Test]
        public void Radius_LogLineCarriesNumbers()
        {
            string text = KvXrGroupPanelLayout.DescribeSizes(8, KvXrGroupPanelLayout.GroupSpanDegrees, 0.110f);
            StringAssert.Contains("0.110", text, "в строке нет радиуса в метрах");
            StringAssert.Contains("мм", text, "в строке нет размеров иконки/просвета в миллиметрах");
            StringAssert.Contains("30", text, "в строке нет посчитанного размера иконки");
        }

        private static float MaxAbs(Vector2[] points)
        {
            float max = 0f;
            for (int i = 0; i < points.Length; i++) max = Mathf.Max(max, points[i].magnitude);
            return max;
        }

        // ============================================================ 2. РУКА ЛУЧА (§37.3)

        /// <summary>ПО УМОЛЧАНИЮ ЛУЧ ИЗ ПРАВОЙ РУКИ — требование владельца, а не «как вышло».</summary>
        [Test]
        public void RayHand_DefaultIsRight()
        {
            Assert.AreEqual((int)KvXrQuickHand.Right, KvXrSettings.DefaultRayHand,
                "по умолчанию луч обязан идти из ПРАВОЙ руки");
            Assert.AreEqual(KvXrQuickHand.Right, KvXrSettings.RayHandFromIndex(KvXrSettings.DefaultRayHand));
        }

        /// <summary>РУКА ЛУЧА ПЕРЕКЛЮЧАЕТСЯ, и мусор в индексе приводит к правой, а не к падению.</summary>
        [Test]
        public void RayHand_SwitchesAndRejectsGarbage()
        {
            Assert.AreEqual(KvXrQuickHand.Left, KvXrSettings.RayHandFromIndex((int)KvXrQuickHand.Left));
            Assert.AreEqual(KvXrQuickHand.Right, KvXrSettings.RayHandFromIndex(77));
            Assert.AreEqual(KvXrQuickHand.Right, KvXrSettings.RayHandFromIndex(-5));
        }

        /// <summary>РУКА ПАНЕЛИ И РУКА ЛУЧА — РАЗНЫЕ НАСТРОЙКИ (панель у левой, луч из правой).</summary>
        [Test]
        public void RayHand_PanelHandIsSeparateSetting()
        {
            Assert.AreEqual((int)KvXrQuickHand.Left, KvXrSettings.DefaultQuickPanelHand,
                "панель по умолчанию обязана быть у ЛЕВОЙ руки");
            Assert.AreNotEqual(KvXrSettings.KeyRayHand, KvXrSettings.KeyQuickPanelHand,
                "рука луча и рука панели обязаны храниться под разными ключами");
        }

        /// <summary>Сторона словами — то, что владелец видит в журнале и проверяет движением руки.</summary>
        [Test]
        public void RayHand_HasReadableLabel()
        {
            string right = KvXrSettings.RayHandLabel((int)KvXrQuickHand.Right);
            string left = KvXrSettings.RayHandLabel((int)KvXrQuickHand.Left);
            Assert.IsNotEmpty(right);
            Assert.IsNotEmpty(left);
            Assert.AreNotEqual(right, left, "стороны обязаны называться по-разному");
        }

        // ============================================================ 3. ПОДСТРОЙКА БЕЗ ПЕРЕСБОРКИ

        /// <summary>
        /// РАЗБОР ФАЙЛА ПОДСТРОЙКИ. Строки `ключ=значение`, комментарии `#`, пустые строки и мусор
        /// игнорируются; ключи приводятся к нижнему регистру (иначе `Panel.Radius` молча не сработал бы).
        /// </summary>
        [Test]
        public void Tuning_ParsesKeyValueText()
        {
            string text =
                "# комментарий\r\n" +
                "panel.radius = 0.110\r\n" +
                "\r\n" +
                "  RAY.HAND=left  \r\n" +
                "мусор без знака равно\r\n" +
                "gesture.enabled=0\r\n";

            Dictionary<string, string> map = KvXrTuning.Parse(text);

            Assert.AreEqual("0.110", map[KvXrTuning.KeyPanelRadius]);
            Assert.AreEqual("left", map[KvXrTuning.KeyRayHand], "ключ обязан приводиться к нижнему регистру");
            Assert.AreEqual("0", map[KvXrTuning.KeyGestures]);
            Assert.AreEqual(3, map.Count, "в карту попали строки, которых там быть не должно");
        }

        /// <summary>ПЛОХИЕ ЗНАЧЕНИЯ НЕ ПРИМЕНЯЮТСЯ И НЕ ПАДАЮТ.</summary>
        [Test]
        public void Tuning_BadValuesAreRejected()
        {
            float value;
            Assert.IsFalse(KvXrTuning.TryParseFloat("abc", out value));
            Assert.IsFalse(KvXrTuning.TryParseFloat("", out value));
            Assert.IsFalse(KvXrTuning.TryParseFloat("NaN", out value));
            Assert.IsFalse(KvXrTuning.TryParseFloat(null, out value));

            Assert.IsTrue(KvXrTuning.TryParseFloat("0.125", out value),
                "число с точкой обязано разбираться независимо от языка системы");
            Assert.AreEqual(0.125f, value, 1e-5f);
        }

        /// <summary>РАДИУС ИЗ ФАЙЛА ОГРАНИЧИВАЕТСЯ ТЕМ ЖЕ ДИАПАЗОНОМ, ЧТО И НАСТРОЙКА.</summary>
        [Test]
        public void Tuning_RadiusFromFileIsClamped()
        {
            var map = new Dictionary<string, string> { { KvXrTuning.KeyPanelRadius, "5.0" } };
            Assert.AreEqual(KvXrGroupPanelLayout.MaxPanelRadiusMeters,
                KvXrTuning.ResolvePanelRadius(map, 0.110f), 1e-5f);

            var bad = new Dictionary<string, string> { { KvXrTuning.KeyPanelRadius, "крупнее" } };
            Assert.AreEqual(0.110f, KvXrTuning.ResolvePanelRadius(bad, 0.110f), 1e-5f,
                "нечитаемое значение обязано оставить прежний радиус");
        }

        /// <summary>РУКА ИЗ ФАЙЛА: `right`/`left`/`правая`/`левая`, мусор — прежнее значение.</summary>
        [Test]
        public void Tuning_RayHandFromFile()
        {
            Assert.AreEqual(KvXrQuickHand.Left, KvXrTuning.ResolveRayHand(
                new Dictionary<string, string> { { KvXrTuning.KeyRayHand, "left" } }, KvXrQuickHand.Right));
            Assert.AreEqual(KvXrQuickHand.Left, KvXrTuning.ResolveRayHand(
                new Dictionary<string, string> { { KvXrTuning.KeyRayHand, "левая" } }, KvXrQuickHand.Right));
            Assert.AreEqual(KvXrQuickHand.Right, KvXrTuning.ResolveRayHand(
                new Dictionary<string, string> { { KvXrTuning.KeyRayHand, "правая" } }, KvXrQuickHand.Left));
            Assert.AreEqual(KvXrQuickHand.Right, KvXrTuning.ResolveRayHand(
                new Dictionary<string, string> { { KvXrTuning.KeyRayHand, "???" } }, KvXrQuickHand.Right));
        }

        /// <summary>ПУСТОЙ ФАЙЛ НИЧЕГО НЕ ЛОМАЕТ: все значения остаются прежними.</summary>
        [Test]
        public void Tuning_EmptyFileKeepsDefaults()
        {
            Dictionary<string, string> map = KvXrTuning.Parse(string.Empty);
            Assert.AreEqual(0, map.Count);
            Assert.AreEqual(0.110f, KvXrTuning.ResolvePanelRadius(map, 0.110f), 1e-5f);
            Assert.AreEqual(KvXrQuickHand.Right, KvXrTuning.ResolveRayHand(map, KvXrQuickHand.Right));
            Assert.IsTrue(KvXrTuning.ResolveGestures(map, true));
            Assert.AreEqual(KvXrControllerVisualMode.Mesh, KvXrTuning.ResolveVisuals(map, KvXrControllerVisualMode.Mesh));
        }

        /// <summary>РЕЖИМ ВИЗУАЛА И ЖЕСТЫ ИЗ ФАЙЛА: числа и слова, мусор — прежнее.</summary>
        [Test]
        public void Tuning_VisualsAndGesturesFromFile()
        {
            Assert.AreEqual(KvXrControllerVisualMode.Primitives, KvXrTuning.ResolveVisuals(
                new Dictionary<string, string> { { KvXrTuning.KeyVisuals, "primitives" } }, KvXrControllerVisualMode.Mesh));
            Assert.AreEqual(KvXrControllerVisualMode.Off, KvXrTuning.ResolveVisuals(
                new Dictionary<string, string> { { KvXrTuning.KeyVisuals, "off" } }, KvXrControllerVisualMode.Mesh));
            Assert.AreEqual(KvXrControllerVisualMode.Mesh, KvXrTuning.ResolveVisuals(
                new Dictionary<string, string> { { KvXrTuning.KeyVisuals, "как-нибудь" } }, KvXrControllerVisualMode.Mesh));

            Assert.IsFalse(KvXrTuning.ResolveGestures(
                new Dictionary<string, string> { { KvXrTuning.KeyGestures, "0" } }, true));
            Assert.IsTrue(KvXrTuning.ResolveGestures(
                new Dictionary<string, string> { { KvXrTuning.KeyGestures, "1" } }, false));
            Assert.IsTrue(KvXrTuning.ResolveGestures(
                new Dictionary<string, string> { { KvXrTuning.KeyGestures, "вкл" } }, false),
                "русское «вкл» обязано читаться как включено");
            Assert.IsFalse(KvXrTuning.ResolveGestures(
                    new Dictionary<string, string> { { KvXrTuning.KeyGestures, "как-нибудь" } }, false),
                "нечитаемое значение обязано оставить прежнее состояние");
        }

        // ============================================================ 4. СТОРОНЫ РУК ПО ГЕОМЕТРИИ

        /// <summary>
        /// ГДЕ РУКА ОТНОСИТЕЛЬНО ВЗГЛЯДА. Это тот самый независимый замер, который поймал ошибку
        /// 21.10.2026: «правый» контроллер стоял на 5 см ЛЕВЕЕ оси взгляда.
        /// </summary>
        [Test]
        public void Sides_LeftRightAndUndecidable()
        {
            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Vector3 headRight = Vector3.right;                         // голова смотрит вперёд

            Assert.AreEqual(1, KvXrSideResolver.SideOf(new Vector3(0.25f, 1.2f, 0.3f), head, headRight),
                "рука справа от взгляда не опознана");
            Assert.AreEqual(-1, KvXrSideResolver.SideOf(new Vector3(-0.25f, 1.2f, 0.3f), head, headRight),
                "рука слева от взгляда не опознана");
            Assert.AreEqual(0, KvXrSideResolver.SideOf(new Vector3(0.01f, 1.2f, 0.3f), head, headRight),
                "рука почти на оси взгляда — судить нельзя");
        }

        /// <summary>Наклон головы не должен менять сторону: «вправо» считается по горизонтали.</summary>
        [Test]
        public void Sides_HeadTiltDoesNotFlipSides()
        {
            Vector3 head = Vector3.zero;
            Quaternion tilted = Quaternion.Euler(40f, 0f, 25f);
            Vector3 headRight = tilted * Vector3.right;

            Assert.AreEqual(1, KvXrSideResolver.SideOf(new Vector3(0.3f, 0f, 0f), head, headRight),
                "при наклоне головы правая рука перестала быть правой");
        }

        /// <summary>
        /// МЕТКИ РАНТАЙМА ПРОТИВОРЕЧАТ ПОЛОЖЕНИЮ — и НЕ объявляются противоречием, когда судить
        /// нельзя (руки на оси) или когда они обе с одной стороны (скрещённые руки).
        /// </summary>
        [Test]
        public void Sides_SwappedOnlyWhenBothSidesKnown()
        {
            Assert.IsTrue(KvXrSideResolver.LabelsAreSwapped(-1, 1),
                "«правый» слева и «левый» справа — это и есть перепутанные стороны");
            Assert.IsFalse(KvXrSideResolver.LabelsAreSwapped(1, -1),
                "верные метки не должны объявляться перепутанными");
            Assert.IsFalse(KvXrSideResolver.LabelsAreSwapped(0, 1), "одну сторону определить нельзя");
            Assert.IsFalse(KvXrSideResolver.LabelsAreSwapped(-1, -1),
                "обе руки с одной стороны (скрещены) — менять нечего");
        }

        /// <summary>Подтверждение меток — отдельное правило: обе стороны известны и совпадают с метками.</summary>
        [Test]
        public void Sides_ConfirmedOnlyWhenBothSidesKnown()
        {
            Assert.IsTrue(KvXrSideResolver.LabelsConfirmed(1, -1));
            Assert.IsFalse(KvXrSideResolver.LabelsConfirmed(-1, 1));
            Assert.IsFalse(KvXrSideResolver.LabelsConfirmed(0, -1));
        }

        /// <summary>Проекция в метрах — именно её печатает журнал (по ней владелец сверяет стороны).</summary>
        [Test]
        public void Sides_ProjectionIsInMeters()
        {
            Vector3 head = new Vector3(1f, 1.6f, 1f);
            float projection = KvXrSideResolver.ProjectionMeters(
                new Vector3(1.12f, 1.2f, 1f), head, Vector3.right);
            Assert.AreEqual(0.12f, projection, 0.002f, "проекция посчитана не в метрах");
        }

        /// <summary>
        /// ПОРОГ ОПРЕДЕЛЕНИЯ СТОРОНЫ СНИЖЕН ДО 1,5 СМ — И ЭТО ПРОВЕРЯЕТСЯ ЧИСЛОМ.
        ///
        /// 21.10.2026 автоопределение не включилось ни разу при пороге 3 см: руки были почти по
        /// центру (`правая (-0.10, 1.00, -0.10) · левая (-0.06, 1.20, -0.20)`), и строка
        /// `СТОРОНЫ РУК` в журнал не попала. Теперь 2 см уже достаточно, а 1 см — ещё нет.
        /// </summary>
        [Test]
        public void Sides_ThresholdAcceptsTwoCentimetres()
        {
            Assert.AreEqual(0.015f, KvXrSideResolver.MinSideMeters, 1e-4f,
                "порог определения стороны должен быть 1,5 см");

            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Assert.AreEqual(1, KvXrSideResolver.SideOf(new Vector3(0.02f, 1.2f, 0.3f), head, Vector3.right),
                "2 см вправо — уже достаточное основание, чтобы понять сторону");
            Assert.AreEqual(0, KvXrSideResolver.SideOf(new Vector3(0.01f, 1.2f, 0.3f), head, Vector3.right),
                "1 см — всё ещё «нельзя судить»");
        }

        /// <summary>
        /// РУЧНОЙ ПЕРЕКЛЮЧАТЕЛЬ СТОРОН `sides.swap`: 1 — поменять, 0 — как есть, нет ключа — авто,
        /// мусор — авто (владелец не должен получить случайную перемену рук из-за опечатки).
        /// </summary>
        [Test]
        public void Tuning_SidesSwapFromFile()
        {
            Assert.AreEqual(true, KvXrTuning.ResolveSidesSwap(
                new Dictionary<string, string> { { KvXrTuning.KeySidesSwap, "1" } }));
            Assert.AreEqual(false, KvXrTuning.ResolveSidesSwap(
                new Dictionary<string, string> { { KvXrTuning.KeySidesSwap, "0" } }));
            Assert.AreEqual(true, KvXrTuning.ResolveSidesSwap(
                new Dictionary<string, string> { { KvXrTuning.KeySidesSwap, "поменять" } }));
            Assert.IsNull(KvXrTuning.ResolveSidesSwap(
                new Dictionary<string, string> { { KvXrTuning.KeySidesSwap, "как-нибудь" } }),
                "нечитаемое значение обязано оставить автоматику");
            Assert.IsNull(KvXrTuning.ResolveSidesSwap(new Dictionary<string, string>()));
        }

        /// <summary>
        /// ГЛУШЕНИЕ ЖЕСТОВ ПРИ КОНТРОЛЛЕРЕ В РУКЕ — ПО УМОЛЧАНИЮ ВЫКЛЮЧЕНО.
        /// 21.10.2026 из-за него владелец не смог показать ни один жест, держа контроллеры.
        /// </summary>
        [Test]
        public void Tuning_MuteWithControllerIsOffByDefault()
        {
            Assert.IsFalse(KvXrTuning.ResolveMuteWithController(new Dictionary<string, string>()));
            Assert.IsFalse(KvXrTuning.ResolveMuteWithController(
                new Dictionary<string, string> { { KvXrTuning.KeyMuteWithController, "0" } }));
            Assert.IsTrue(KvXrTuning.ResolveMuteWithController(
                new Dictionary<string, string> { { KvXrTuning.KeyMuteWithController, "1" } }));
        }

        /// <summary>
        /// ЦЕНТРАЛЬНАЯ ИКОНКА ВЕЕРА (ВЫХОД) ДОЛЖНА ПОПАДАТЬСЯ ЛУЧОМ.
        ///
        /// Дефект 21.10.2026: «не работает кнопка перехода на первый слой». Причина — смещения (0,0)
        /// центральной иконки не было в списке попадания, и луч физически не мог в неё попасть.
        /// </summary>
        [Test]
        public void Fan_CenterIconIsHittable()
        {
            Vector2[] hit = KvXrGroupPanelLayout.FanHitOffsets(10, KvXrQuickHand.Left, 0.110f);

            Assert.AreEqual(11, hit.Length, "в списке попадания обязана быть и центральная иконка");
            Assert.AreEqual(Vector2.zero, hit[hit.Length - 1], "центральная иконка обязана стоять в центре");

            int center = KvXrGroupPanelLayout.ClosestIndex(Vector2.zero, hit,
                KvXrGroupPanelLayout.IconSizeMeters * 0.85f);
            Assert.AreEqual(hit.Length - 1, center, "луч в центр панели обязан давать центральную иконку (выход)");

            for (int i = 0; i < hit.Length - 1; i++)
            {
                int found = KvXrGroupPanelLayout.ClosestIndex(hit[i], hit,
                    KvXrGroupPanelLayout.IconSizeMeters * 0.85f);
                Assert.AreEqual(i, found, "иконка " + i + " не выбирается наведением на неё");
            }
        }

        // ============================================================ 5. КИСТИ

        /// <summary>
        /// НУЛЕВАЯ ПОЗА — ОТКАЗ. Ловушка [F2]: поза может прийти «нулевой» и формально достоверной;
        /// жест по ней — ложное срабатывание, а кисть в начале координат — «рука на полу».
        /// </summary>
        [Test]
        public void Hands_ZeroPoseIsRejected()
        {
            var zero = new KvHandFrame { valid = true, hasShapes = true };
            Assert.IsFalse(KvGestureMath.IsUsable(zero), "нулевая поза принята за достоверную");
            Assert.IsFalse(KvGestureMath.IsPinch(zero));
            Assert.AreEqual(KvGestureKind.None, KvGestureRecognizer.Detect(zero));

            var untracked = GeometricPalm(hasShapes: false);
            untracked.valid = false;
            Assert.IsFalse(KvGestureMath.IsUsable(untracked), "кисть без isTracked принята за достоверную");
        }

        /// <summary>ДОСТОВЕРНАЯ КИСТЬ ПРИНИМАЕТСЯ, и её масштаб (ширина ладони) считается по суставам.</summary>
        [Test]
        public void Hands_UsableFrameHasPalmScale()
        {
            KvHandFrame frame = GeometricPalm(hasShapes: false);
            Assert.IsTrue(KvGestureMath.IsUsable(frame));
            Assert.AreEqual(0.06f, KvGestureMath.PalmScale(frame), 0.005f,
                "ширина ладони посчитана неверно — от неё зависит отсев нулевой позы");
        }

        /// <summary>
        /// БЕЗ СГИБОВ ОТ XR HANDS ЖЕСТОВ НЕТ ВООБЩЕ. Именно «угадывание по расстояниям» дало
        /// 63 ложных срабатывания 21.10.2026 — теперь при отсутствии сгибов распознавание молчит.
        /// </summary>
        [Test]
        public void Hands_WithoutShapesNoGestures()
        {
            KvHandFrame frame = GeometricPalm(hasShapes: false);
            Assert.IsTrue(KvGestureMath.IsUsable(frame), "поза достоверна");
            Assert.IsFalse(KvGestureMath.IsPinch(frame));
            Assert.IsFalse(KvGestureMath.IsOpenPalm(frame));
            Assert.IsFalse(KvGestureMath.IsIndexPoint(frame));
            Assert.AreEqual(KvGestureKind.None, KvGestureRecognizer.Detect(frame));
        }

        // ============================================================ 6. ЖЕСТЫ

        /// <summary>ЩИПОК РАСПОЗНАЁТСЯ по силе щипка и умеренному сгибу указательного.</summary>
        [Test]
        public void Gesture_PinchIsRecognised()
        {
            Assert.IsTrue(KvGestureMath.IsPinch(Pinch()), "щипок не распознан");
            Assert.IsFalse(KvGestureMath.IsPinch(OpenPalm()), "раскрытая ладонь принята за щипок");
            Assert.IsFalse(KvGestureMath.IsPinch(IndexPoint()), "указательный палец принят за щипок");
            Assert.IsFalse(KvGestureMath.IsPinch(Fist()), "КУЛАК принят за щипок — это и было на живом шлеме");
        }

        /// <summary>РАСКРЫТАЯ ЛАДОНЬ, УКАЗАТЕЛЬНЫЙ ПАЛЕЦ, КУЛАК И РАССЛАБЛЕННАЯ КИСТЬ — РАЗНОЕ.</summary>
        [Test]
        public void Gesture_OpenPalmIndexPointFistRelaxedAreDistinct()
        {
            Assert.IsTrue(KvGestureMath.IsOpenPalm(OpenPalm()));
            Assert.IsFalse(KvGestureMath.IsIndexPoint(OpenPalm()));

            Assert.IsTrue(KvGestureMath.IsIndexPoint(IndexPoint()));
            Assert.IsFalse(KvGestureMath.IsOpenPalm(IndexPoint()));

            Assert.IsTrue(KvGestureMath.IsFist(Fist()), "кулак не опознан как кулак");
            Assert.IsFalse(KvGestureMath.IsOpenPalm(Fist()), "кулак принят за раскрытую ладонь");
            Assert.AreEqual(KvGestureKind.None, KvGestureRecognizer.Detect(Fist()),
                "кулак не должен давать ни одного жеста");

            Assert.AreEqual(KvGestureKind.None, KvGestureRecognizer.Detect(Relaxed()),
                "расслабленная кисть не должна давать жестов (на живом шлеме давала «ладонь»)");
        }

        /// <summary>
        /// УДЕРЖАНИЕ: короткое касание пальцами НЕ срабатывает. Порог щипка 0,20 с — иначе жест
        /// срабатывал бы от случайного движения.
        /// </summary>
        [Test]
        public void Gesture_RequiresHold()
        {
            var recognizer = new KvGestureRecognizer();
            KvHandFrame pinch = Pinch();

            KvGestureEvent evt;
            Assert.IsFalse(recognizer.TryTick(pinch, default(KvHandFrame), 0.05f, out evt),
                "щипок сработал мгновенно, без удержания");
            Assert.IsFalse(recognizer.TryTick(pinch, default(KvHandFrame), 0.05f, out evt),
                "щипок сработал раньше порога удержания");
            Assert.IsTrue(recognizer.TryTick(pinch, default(KvHandFrame), 0.15f, out evt),
                "щипок не сработал после удержания 0,25 с");
            Assert.AreEqual(KvGestureKind.Pinch, evt.kind);
            Assert.AreEqual(KvXrQuickHand.Right, evt.hand);
            Assert.AreEqual(KvGestureAction.Press, evt.action);
        }

        /// <summary>
        /// ПАУЗА МЕЖДУ СРАБАТЫВАНИЯМИ И ТРЕБОВАНИЕ НЕЙТРАЛЬНОГО ПОЛОЖЕНИЯ: удержанный щипок не
        /// превращается в «пулемёт» нажатий.
        /// </summary>
        [Test]
        public void Gesture_DoesNotRepeatWhileHeld()
        {
            var recognizer = new KvGestureRecognizer();
            KvHandFrame pinch = Pinch();
            KvGestureEvent evt;

            Assert.IsTrue(recognizer.TryTick(pinch, default(KvHandFrame), 0.25f, out evt),
                "первое срабатывание потеряно");

            bool firedAgain = false;
            for (int i = 0; i < 40; i++)
            {
                if (recognizer.TryTick(pinch, default(KvHandFrame), 0.05f, out evt)) firedAgain = true;
            }
            Assert.IsFalse(firedAgain, "щипок срабатывает повторно, пока его держат");

            recognizer.TryTick(OpenPalm(), default(KvHandFrame), 0.30f, out evt);
            recognizer.TryTick(default(KvHandFrame), default(KvHandFrame), 0.30f, out evt);
            Assert.IsTrue(recognizer.TryTick(pinch, default(KvHandFrame), 0.30f, out evt),
                "после отпускания и паузы жест не сработал снова");
        }

        /// <summary>ЛЕВАЯ РУКА ДЕЛАЕТ ДРУГОЕ: щипок левой — выход из веера, а не нажатие.</summary>
        [Test]
        public void Gesture_LeftHandPinchClosesFan()
        {
            Assert.AreEqual(KvGestureAction.CloseFan,
                KvGestureRecognizer.ActionFor(KvGestureKind.Pinch, KvXrQuickHand.Left));
            Assert.AreEqual(KvGestureAction.Press,
                KvGestureRecognizer.ActionFor(KvGestureKind.Pinch, KvXrQuickHand.Right));
            Assert.AreEqual(KvGestureAction.TogglePanel,
                KvGestureRecognizer.ActionFor(KvGestureKind.OpenPalm, KvXrQuickHand.Left));
            Assert.AreEqual(KvGestureAction.NextGroup,
                KvGestureRecognizer.ActionFor(KvGestureKind.IndexPoint, KvXrQuickHand.Right));
            Assert.AreEqual(KvGestureAction.None,
                KvGestureRecognizer.ActionFor(KvGestureKind.IndexPoint, KvXrQuickHand.Left),
                "указательный палец левой руки не должен перелистывать группы сам по себе");
        }

        /// <summary>ЖЕСТ ЛЕВОЙ РУКИ ТОЖЕ СРАБАТЫВАЕТ и попадает в событие с правильной стороной.</summary>
        [Test]
        public void Gesture_LeftHandEventCarriesSide()
        {
            var recognizer = new KvGestureRecognizer();
            KvGestureEvent evt;
            Assert.IsTrue(recognizer.TryTick(default(KvHandFrame), Pinch(), 0.30f, out evt));
            Assert.AreEqual(KvXrQuickHand.Left, evt.hand);
            Assert.AreEqual(KvGestureAction.CloseFan, evt.action);
        }

        /// <summary>У КАЖДОГО ЖЕСТА ЕСТЬ НАЗВАНИЕ ДЛЯ ЖУРНАЛА — иначе строки-доказательства не будет.</summary>
        [Test]
        public void Gesture_HasNameForLog()
        {
            StringAssert.Contains("щипок", KvGestureRecognizer.GestureName(KvGestureKind.Pinch));
            StringAssert.Contains("ладонь", KvGestureRecognizer.GestureName(KvGestureKind.OpenPalm));
            StringAssert.Contains("палец", KvGestureRecognizer.GestureName(KvGestureKind.IndexPoint));
        }

        /// <summary>СБРОС СЧЁТЧИКОВ: после выключения жестов (или пробуждения шлема) ничего не «догоняет».</summary>
        [Test]
        public void Gesture_ResetClearsState()
        {
            var recognizer = new KvGestureRecognizer();
            KvGestureEvent evt;
            recognizer.TryTick(Pinch(), default(KvHandFrame), 0.30f, out evt);
            recognizer.Reset();
            Assert.IsTrue(recognizer.TryTick(Pinch(), default(KvHandFrame), 0.30f, out evt),
                "после сброса первое срабатывание обязано пройти сразу");
        }

        /// <summary>
        /// ПАНЕЛЬ БЛИЖЕ (21.10.2026): владелец попросил «сделай ближе на 30 см» и «чтобы смотрела в
        /// сторону поворота руки». Проверяется, что новые значения по умолчанию именно такие:
        /// вынос 0,12 м (было 0,30) и ближайшая дистанция от глаз 0,18 м (было 0,35 — именно этот
        /// кламп и держал панель далеко, сколько бы вынос ни уменьшали).
        /// </summary>
        [Test]
        public void Panel_CloserDefaultsAreAsRequested()
        {
            Assert.AreEqual(0.12f, KvXrGroupPanel.DefaultHandForwardMeters, 1e-4f,
                "вынос панели от контроллера обязан быть 0,12 м");
            Assert.AreEqual(0.18f, KvXrGroupPanel.DefaultHandMinDistanceFromEyes, 1e-4f,
                "ближайшая дистанция панели от глаз обязана быть 0,18 м");
            Assert.LessOrEqual(KvXrTuning.MinPanelForwardMeters, KvXrGroupPanel.DefaultHandForwardMeters,
                "минимальный вынос не должен поднимать значение по умолчанию вверх");
        }

        /// <summary>Ключ `panel.minDistanceEyes` читается из файла и ограничивается диапазоном.</summary>
        [Test]
        public void Tuning_MinDistanceEyesFromFile()
        {
            Assert.AreEqual(0.22f, KvXrTuning.ResolvePanelMinDistanceEyes(
                new Dictionary<string, string> { { KvXrTuning.KeyPanelMinDistanceEyes, "0.22" } }, 0.18f), 1e-4f);
            Assert.AreEqual(KvXrTuning.MinPanelDistanceEyesMeters, KvXrTuning.ResolvePanelMinDistanceEyes(
                new Dictionary<string, string> { { KvXrTuning.KeyPanelMinDistanceEyes, "0.001" } }, 0.18f), 1e-4f,
                "слишком близко к глазам — недопустимо");
            Assert.AreEqual(0.18f, KvXrTuning.ResolvePanelMinDistanceEyes(
                new Dictionary<string, string> { { KvXrTuning.KeyPanelMinDistanceEyes, "близко" } }, 0.18f), 1e-4f,
                "нечитаемое значение обязано оставить прежнее");
        }

        // ============================================================ 7. МОДЕЛИ КОНТРОЛЛЕРОВ

        /// <summary>НУЛЕВАЯ ПОЗА КОНТРОЛЛЕРА — МОДЕЛЬ НЕ СТАВИТСЯ (ловушка [F2]).</summary>
        [Test]
        public void Visuals_ZeroPoseRejected()
        {
            Assert.IsFalse(KvXrControllerVisuals.IsPoseUsable(false, new Vector3(0.2f, 1.1f, 0.3f)),
                "модель поставлена по позе без isTracked");
            Assert.IsFalse(KvXrControllerVisuals.IsPoseUsable(true, Vector3.zero),
                "модель поставлена в начало координат — это и есть «контроллер на полу»");
            Assert.IsFalse(KvXrControllerVisuals.IsPoseUsable(true, new Vector3(0f, 50f, 0f)),
                "модель поставлена по невозможной позе");
            Assert.IsTrue(KvXrControllerVisuals.IsPoseUsable(true, new Vector3(0.25f, 1.10f, 0.35f)));
        }

        /// <summary>ПУТИ К МЕШАМ SDK: правая и левая модели — разные файлы, оба в папке Resources.</summary>
        [Test]
        public void Visuals_SdkModelPathsAreDistinct()
        {
            string right = KvXrControllerVisuals.SdkModelPath(KvXrQuickHand.Right);
            string left = KvXrControllerVisuals.SdkModelPath(KvXrQuickHand.Left);
            Assert.AreNotEqual(right, left, "обе руки грузят одну и ту же модель");
            StringAssert.StartsWith("Controller/", right);
            StringAssert.Contains("PICO 4U", right);
        }

        /// <summary>РЕЖИМ ВИЗУАЛА: индекс настройки → режим, мусор → меш (а не «ничего не видно»).</summary>
        [Test]
        public void Visuals_ModeFromIndex()
        {
            Assert.AreEqual(KvXrControllerVisualMode.Mesh,
                KvXrControllerVisuals.ModeFromIndex((int)KvXrControllerVisualMode.Mesh));
            Assert.AreEqual(KvXrControllerVisualMode.Primitives,
                KvXrControllerVisuals.ModeFromIndex((int)KvXrControllerVisualMode.Primitives));
            Assert.AreEqual(KvXrControllerVisualMode.Mesh, KvXrControllerVisuals.ModeFromIndex(99));
            Assert.IsNotEmpty(KvXrControllerVisuals.ModeName(KvXrControllerVisualMode.Mesh));
        }

        // ============================================================ СИНТЕТИЧЕСКИЕ КИСТИ

        /// <summary>
        /// ГЕОМЕТРИЯ ЛАДОНИ: ширина 6 см, все точки на своих местах. Нужна только для отсева
        /// нулевой позы — жесты теперь решаются по сгибам, а не по этим координатам.
        /// </summary>
        private static KvHandFrame GeometricPalm(bool hasShapes)
        {
            return new KvHandFrame
            {
                valid = true,
                wrist = new Vector3(0f, 0f, 0f),
                palm = new Vector3(0f, 0.02f, 0f),
                indexProximal = new Vector3(0.03f, 0.06f, 0f),
                indexTip = new Vector3(0.03f, 0.12f, 0f),
                middleProximal = new Vector3(0.01f, 0.06f, 0f),
                middleTip = new Vector3(0.01f, 0.13f, 0f),
                ringProximal = new Vector3(-0.01f, 0.06f, 0f),
                ringTip = new Vector3(-0.01f, 0.12f, 0f),
                littleProximal = new Vector3(-0.03f, 0.06f, 0f),
                littleTip = new Vector3(-0.03f, 0.11f, 0f),
                thumbTip = new Vector3(-0.07f, 0.04f, 0f),
                palmRotation = Quaternion.identity,
                hasShapes = hasShapes
            };
        }

        /// <summary>КИСТЬ СО СГИБАМИ: единственный способ задать жест в тесте.</summary>
        private static KvHandFrame Shaped(float index, float middle, float ring, float little,
            float pinch, float spread)
        {
            KvHandFrame frame = GeometricPalm(hasShapes: true);
            frame.indexCurl = index;
            frame.middleCurl = middle;
            frame.ringCurl = ring;
            frame.littleCurl = little;
            frame.indexPinch = pinch;
            frame.middlePinch = pinch * 0.4f;
            frame.ringPinch = pinch * 0.3f;
            frame.littlePinch = pinch * 0.2f;
            frame.indexSpread = spread;
            frame.middleSpread = spread;
            frame.ringSpread = spread;
            return frame;
        }

        private static KvHandFrame Pinch() { return Shaped(0.55f, 0.45f, 0.45f, 0.45f, 0.92f, 0.15f); }
        private static KvHandFrame OpenPalm() { return Shaped(0.10f, 0.10f, 0.10f, 0.10f, 0.10f, 0.60f); }
        private static KvHandFrame IndexPoint() { return Shaped(0.10f, 0.85f, 0.85f, 0.85f, 0.20f, 0.20f); }
        private static KvHandFrame Fist() { return Shaped(0.95f, 0.95f, 0.95f, 0.95f, 0.60f, 0.05f); }

        /// <summary>РАССЛАБЛЕННАЯ КИСТЬ: пальцы чуть согнуты и сведены — это НЕ жест.</summary>
        private static KvHandFrame Relaxed() { return Shaped(0.38f, 0.38f, 0.38f, 0.38f, 0.20f, 0.15f); }
    }
}
