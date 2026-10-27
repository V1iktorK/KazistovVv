using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ СЕССИИ §38 — СТРОГИЕ ЖЕСТЫ, «ЖЕСТ БЕЗ ДЕЛА НЕ СЧИТАЕТСЯ», ДИАГНОСТИКА КИСТЕЙ.
    ///
    /// ОТКУДА ВЗЯТЫ ПРЕДМЕТЫ. Из живого прогона 21.10.2026 (`Logs\device-s37h.log`): жесты
    /// СРАБАТЫВАЛИ (50 событий), но 20 из них — «раскрытая ладонь» на расслабленную кисть
    /// (панель переключалась 20 раз), 12 — «щипок левой» при уже закрытом веере, 14 — «щипок
    /// правой» без цели под лучом. Владелец выбрал СТРОГИЙ режим: «лучше не сработает, чем
    /// сработает лишний раз».
    ///
    /// ЧТО ЗДЕСЬ ПРОВЕРЯЕТСЯ:
    ///   1) ПОРОГИ стали строгими: расслабленная кисть не «раскрытая ладонь», кулак и «почти
    ///      щипок» не щипок, согнутый средний палец не «указательный палец»;
    ///   2) МЕСТО РУКИ: жест считается только когда рука впереди и не дальше 0,9 м от глаз;
    ///   3) ПОЗА ЛАДОНИ: «раскрытая ладонь» — пальцами ВВЕРХ (как «стоп»); опущенная или
    ///      указывающая рука жест не даёт;
    ///   4) «ЖЕСТ БЕЗ ДЕЛА»: нажатие без иконки под лучом, выход из веера при закрытом веере,
    ///      «следующая группа» при невидимой панели — НЕ считаются;
    ///   5) ДИАГНОСТИКА КИСТЕЙ: строка с положением запястья, камеры и расстоянием, и
    ///      предупреждение, когда кисть нарисована «не там».
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: проверки «шлем увидел руку» — это строка живого журнала и взгляд владельца.
    /// </summary>
    public class KvXrS38Tests
    {
        private static readonly Vector3 Head = new Vector3(0f, 1.6f, 0f);
        private static readonly Quaternion HeadForward = Quaternion.identity;   // смотрим в +Z

        // ============================================================ 1. МЕСТО РУКИ

        /// <summary>РУКА ВПЕРЕДИ И НЕДАЛЕКО — жест считается. Опущена или за спиной — нет.</summary>
        [Test]
        public void View_HandMustBeInFrontAndNear()
        {
            Assert.IsTrue(KvGestureMath.IsHandInView(new Vector3(0f, 1.4f, 0.45f), Head, HeadForward),
                "рука впереди на 0,45 м — в поле зрения");
            Assert.IsTrue(KvGestureMath.IsHandInView(new Vector3(0.3f, 1.3f, 0.7f), Head, HeadForward),
                "рука сбоку, но впереди и близко — в поле зрения");

            Assert.IsFalse(KvGestureMath.IsHandInView(new Vector3(0f, 0.8f, 0.05f), Head, HeadForward),
                "рука почти на одной вертикали с головой — жест не считается");
            Assert.IsFalse(KvGestureMath.IsHandInView(new Vector3(0f, 1.2f, -0.4f), Head, HeadForward),
                "рука ЗА спиной — жест не считается");
            Assert.IsFalse(KvGestureMath.IsHandInView(new Vector3(0f, 1.4f, 1.4f), Head, HeadForward),
                "рука дальше 1,0 м — слишком далеко для жеста");
        }

        /// <summary>
        /// Пороги места — те самые числа, что в правилах. ПЕРЕКАЛИБРОВАНЫ 23.10.2026 по живым
        /// числам (разрешение владельца получено): 0,15/0,90 → 0,10/1,00, потому что проверка места
        /// отсекала настоящие жесты.
        /// </summary>
        [Test]
        public void View_ThresholdsAreTheDocumentedNumbers()
        {
            Assert.AreEqual(0.10f, KvGestureMath.MinForwardMeters, 1e-4f);
            Assert.AreEqual(1.00f, KvGestureMath.MaxDistanceFromHeadMeters, 1e-4f);
        }

        // ============================================================ 2. ПОЗА ЛАДОНИ

        /// <summary>
        /// «РАСКРЫТАЯ ЛАДОНЬ» — ПАЛЬЦАМИ ВВЕРХ, как жест «стоп». Опущенная рука этому не
        /// удовлетворяет (именно она давала ложные переключения панели); а указывающая рука
        /// распознаётся как «указательный палец», а НЕ как раскрытая ладонь.
        /// </summary>
        [Test]
        public void Palm_PostureRequiresFingersUp()
        {
            KvHandFrame up = Placed(OpenPalmFingersUp());
            Assert.IsTrue(KvGestureMath.IsPalmUpPosture(up), "ладонь пальцами вверх обязана опознаваться");

            KvHandFrame hanging = HangingHand();
            Assert.IsFalse(KvGestureMath.IsPalmUpPosture(hanging), "опущенная рука не должна считаться позой «ладонь вверх»");
            Assert.AreEqual(KvGestureKind.None, KvGestureRecognizer.Detect(hanging, Head, HeadForward),
                "опущенная рука не должна давать жест «раскрытая ладонь»");

            // Указывающая рука: поза формально «пальцы вверх», но это указание, а не ладонь.
            Assert.AreEqual(KvGestureKind.IndexPoint,
                KvGestureRecognizer.Detect(Placed(IndexPoint()), Head, HeadForward),
                "указывающая рука обязана опознаваться как указательный палец, а не как ладонь");
        }

        /// <summary>
        /// РАССЛАБЛЕННАЯ КИСТЬ БОЛЬШЕ НЕ «РАСКРЫТАЯ ЛАДОНЬ»: пороги строгие (сгибы ≤ 0,15,
        /// разведение ≥ 0,10, большой выпрямлен, щипок ≤ 0,30).
        /// </summary>
        [Test]
        public void Palm_RelaxedHandIsNotOpenPalm()
        {
            Assert.IsTrue(KvGestureMath.IsOpenPalm(OpenPalmFingersUp()),
                "раскрытая ладонь пальцами вверх должна опознаваться");
            Assert.IsFalse(KvGestureMath.IsOpenPalm(Relaxed()),
                "расслабленная кисть (сгибы ≈0,38) не должна считаться раскрытой ладонью");
        }

        /// <summary>
        /// Строгие пороги — те самые числа, что записаны в правилах. ПЕРЕКАЛИБРОВАНЫ 23.10.2026 по
        /// живым числам владельца (раскрытая ладонь даёт сгибы 0,15–0,30 при пороге 0,15 — жесты
        /// «пропали все»). Разрешение на правку этих утверждений получено.
        /// </summary>
        [Test]
        public void Palm_ThresholdsAreStrict()
        {
            Assert.AreEqual(0.30f, KvGestureMath.OpenCurl, 1e-4f, "сгиб для раскрытой ладони обязан быть ≤ 0,30");
            Assert.AreEqual(0.05f, KvGestureMath.OpenSpread, 1e-4f, "разведение для раскрытой ладони ≥ 0,05");
            Assert.AreEqual(0.40f, KvGestureMath.OpenMaxPinch, 1e-4f, "ладонь не должна быть одновременно щипком");
        }

        // ============================================================ 3. СТРОГИЕ ПОРОГИ ОСТАЛЬНЫХ ЖЕСТОВ

        /// <summary>ЩИПОК: сила ≥ 0,80 и умеренный сгиб указательного. «Почти» и кулак — не щипок.</summary>
        [Test]
        public void Pinch_StrictThresholds()
        {
            Assert.IsTrue(KvGestureMath.IsPinch(Pinch()), "настоящий щипок не опознан");
            Assert.IsFalse(KvGestureMath.IsPinch(NearlyPinch()), "сведённые, но не сомкнутые пальцы — ещё не щипок");
            Assert.IsFalse(KvGestureMath.IsPinch(Fist()), "кулак не должен считаться щипком");
            Assert.AreEqual(0.80f, KvGestureMath.PinchTouching, 1e-4f);
        }

        /// <summary>УКАЗАТЕЛЬНЫЙ ПАЛЕЦ: остальные три обязаны быть согнуты сильнее 0,75.</summary>
        [Test]
        public void IndexPoint_RequiresOtherFingersCurled()
        {
            Assert.IsTrue(KvGestureMath.IsIndexPoint(IndexPoint()), "указательный палец не опознан");
            Assert.IsFalse(KvGestureMath.IsIndexPoint(SemiPointing()),
                "полусогнутые остальные пальцы (0,60) не должны считаться указательным жестом");
        }

        // ============================================================ 4. «ЖЕСТ БЕЗ ДЕЛА НЕ СЧИТАЕТСЯ»

        /// <summary>
        /// ЖЕСТ СЧИТАЕТСЯ ТОЛЬКО КОГДА ЕСТЬ ЧТО СДЕЛАТЬ. Именно этого не хватало: 14 «щипков» без
        /// цели под лучом и 12 «выходов из веера» при уже закрытом веере попадали в журнал и жгли паузу.
        /// </summary>
        [Test]
        public void Useful_OnlyWhenThereIsSomethingToDo()
        {
            Assert.IsTrue(KvGestureActions.IsUseful(KvGestureAction.Press, true, false, true),
                "щипок при иконке под лучом — полезен");
            Assert.IsFalse(KvGestureActions.IsUseful(KvGestureAction.Press, false, false, true),
                "щипок без цели под лучом — НЕ полезен");

            Assert.IsTrue(KvGestureActions.IsUseful(KvGestureAction.CloseFan, false, true, true),
                "выход из веера при раскрытом веере — полезен");
            Assert.IsFalse(KvGestureActions.IsUseful(KvGestureAction.CloseFan, false, false, true),
                "выход из веера при закрытом веере — НЕ полезен");

            Assert.IsTrue(KvGestureActions.IsUseful(KvGestureAction.NextGroup, false, false, true),
                "следующая группа при видимой панели — полезна");
            Assert.IsFalse(KvGestureActions.IsUseful(KvGestureAction.NextGroup, false, false, false),
                "следующая группа при СКРЫТОЙ панели — не полезна");

            Assert.IsTrue(KvGestureActions.IsUseful(KvGestureAction.TogglePanel, false, false, false),
                "показ/скрытие панели полезно всегда");
            Assert.IsFalse(KvGestureActions.IsUseful(KvGestureAction.None, true, true, true),
                "у действия «нет» полезности нет");
        }

        // ============================================================ 5. ПОЛНЫЙ ПУТЬ РАСПОЗНАВАНИЯ

        /// <summary>
        /// РАСПОЗНАВАНИЕ С УЧЁТОМ МЕСТА: та же кисть «пальцами вверх», но опущенная вниз —
        /// жеста нет. Это и есть главное правило §38.
        /// </summary>
        [Test]
        public void Detect_RequiresHandInView()
        {
            KvHandFrame up = Placed(OpenPalmFingersUp());
            Assert.AreEqual(KvGestureKind.OpenPalm,
                KvGestureRecognizer.Detect(up, Head, HeadForward),
                "ладонь пальцами вверх перед глазами обязана опознаваться");

            // Та же кисть, но далеко внизу и за спиной — жеста быть не должно.
            Vector3 shift = new Vector3(0f, -1.0f, -0.6f);
            KvHandFrame far = OpenPalmFingersUp();
            far.wrist += shift;
            far.palm += shift;
            far.indexTip += shift;
            far.indexProximal += shift;
            Assert.AreEqual(KvGestureKind.None, KvGestureRecognizer.Detect(far, Head, HeadForward),
                "жест рукой вне поля зрения не должен распознаваться");
        }

        /// <summary>
        /// РАССЛАБЛЕННАЯ РУКА НЕ ДАЁТ НИ ОДНОГО ЖЕСТА — ни по форме, ни в полном пути с местом.
        /// </summary>
        [Test]
        public void Detect_RelaxedHandGivesNothing()
        {
            KvHandFrame relaxed = Relaxed();
            relaxed.wrist = new Vector3(0f, 1.35f, 0.45f);
            relaxed.palm = new Vector3(0f, 1.37f, 0.45f);

            Assert.AreEqual(KvGestureKind.None, KvGestureRecognizer.Detect(relaxed));
            Assert.AreEqual(KvGestureKind.None, KvGestureRecognizer.Detect(relaxed, Head, HeadForward));
        }

        /// <summary>Удержание ладони удлинено (0,80 с) — случайное «раскрыл и убрал» не срабатывает.</summary>
        [Test]
        public void Hold_OpenPalmIsLongerThanBefore()
        {
            Assert.AreEqual(0.80f, KvGestureRecognizer.PalmHoldSeconds, 1e-4f);
            Assert.AreEqual(0.60f, KvGestureRecognizer.PointHoldSeconds, 1e-4f);

            var recognizer = new KvGestureRecognizer();
            KvGestureEvent evt;
            KvHandFrame palm = OpenPalmFingersUp();

            // 0,60 с — ещё не порог ладони (было бы достаточно при прежних 0,40 с).
            Assert.IsFalse(recognizer.TryTick(default(KvHandFrame), palm, 0.60f, out evt),
                "ладонь сработала раньше нового порога 0,80 с");
            Assert.IsTrue(recognizer.TryTick(default(KvHandFrame), palm, 0.25f, out evt),
                "ладонь не сработала после удержания 0,85 с");
        }

        // ============================================================ 6. ДИАГНОСТИКА КИСТЕЙ

        /// <summary>
        /// ЧИСЛА ДЛЯ ПРОВЕРКИ ВИДИМОСТИ КИСТЕЙ: расстояние от запястья до камеры и признак
        /// «это не рука, а ошибка пространства».
        /// </summary>
        [Test]
        public void Diagnostics_DistanceAndPlausibility()
        {
            Vector3 camera = new Vector3(0f, 1.6f, 0f);

            // Расстояние считается по трём осям: рука ниже глаз на 0,20 м и впереди на 0,45 м,
            // значит √(0,20² + 0,45²) ≈ 0,49 м — именно это число должно попасть в журнал.
            Assert.AreEqual(0.49f, KvHandDiagnostics.DistanceToCamera(new Vector3(0f, 1.4f, 0.45f), camera), 0.02f,
                "расстояние до камеры посчитано неверно");
            Assert.IsTrue(KvHandDiagnostics.IsPlausible(new Vector3(0f, 1.4f, 0.45f), camera),
                "рука в 0,45 м от камеры — правдоподобно");
            Assert.IsFalse(KvHandDiagnostics.IsPlausible(new Vector3(0f, 1.4f, 2.4f), camera),
                "рука в 2,4 м от камеры — это не рука, а ошибка пространства");

            string text = KvHandDiagnostics.Describe(new Vector3(0.1f, 1.3f, 0.5f), camera);
            StringAssert.Contains("запястье", text);
            StringAssert.Contains("камера", text);
            StringAssert.Contains("м", text, "в строке нет расстояния в метрах");
        }

        // ============================================================ 7. НУЛЕВАЯ ПОЗА ГОЛОВЫ

        /// <summary>
        /// НУЛЕВАЯ ПОЗА ГОЛОВЫ ОТВЕРГАЕТСЯ (найдено 23.10.2026). В журнале было
        /// `Камера следует за головой: поза (0.00, 0.00, 0.00) · высота 0,00 м` — и это выглядело
        /// сразу тремя дефектами: камера на полу, панель у пояса навсегда, все жесты отвергнуты
        /// проверкой «рука в поле зрения». Человек не стоит на высоте 0,30 м — такая поза не поза.
        /// </summary>
        [Test]
        public void Head_ZeroPoseIsRejected()
        {
            Assert.IsFalse(KvXrInputSource.IsPlausibleHeadPosition(Vector3.zero),
                "нулевая поза головы принята как достоверная — это и была причина трёх дефектов");
            Assert.IsFalse(KvXrInputSource.IsPlausibleHeadPosition(new Vector3(0f, 0.1f, 0f)));
            Assert.IsTrue(KvXrInputSource.IsPlausibleHeadPosition(new Vector3(0f, 1.62f, 0f)));
            Assert.AreEqual(0.30f, KvXrInputSource.MinPlausibleHeadHeight, 1e-4f);
        }

        // ============================================================ СИНТЕТИЧЕСКИЕ КИСТИ

        /// <summary>Геометрия ладони (нужна для отсева нулевой позы) + заданные сгибы.</summary>
        private static KvHandFrame Shaped(float index, float middle, float ring, float little,
            float pinch, float spread, float thumbCurl = 0.2f)
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
                hasShapes = true,
                indexCurl = index,
                middleCurl = middle,
                ringCurl = ring,
                littleCurl = little,
                thumbCurl = thumbCurl,
                indexPinch = pinch,
                middlePinch = pinch * 0.4f,
                ringPinch = pinch * 0.3f,
                littlePinch = pinch * 0.2f,
                indexSpread = spread,
                middleSpread = spread,
                ringSpread = spread
            };
        }

        /// <summary>Раскрытая ладонь пальцами ВВЕРХ: прямые пальцы, разведены, большой выпрямлен.</summary>
        private static KvHandFrame OpenPalmFingersUp() { return Shaped(0.10f, 0.10f, 0.10f, 0.10f, 0.10f, 0.60f, 0.20f); }

        /// <summary>
        /// ПЕРЕНЕСТИ КИСТЬ К ГРУДИ: чтобы она попала в поле зрения (впереди 0,45 м, чуть ниже глаз).
        /// Без этого проверка места отсекает все синтетические кисти — и тест ничего не проверяет.
        /// </summary>
        private static KvHandFrame Placed(KvHandFrame frame)
        {
            Vector3 target = new Vector3(0f, 1.35f, 0.45f);
            Vector3 shift = target - frame.wrist;
            frame.wrist += shift;
            frame.palm += shift;
            frame.indexTip += shift;
            frame.indexProximal += shift;
            frame.littleProximal += shift;
            frame.thumbTip += shift;
            return frame;
        }

        /// <summary>Расслабленная кисть: пальцы чуть согнуты (0,38) — это НЕ жест.</summary>
        private static KvHandFrame Relaxed() { return Shaped(0.38f, 0.38f, 0.38f, 0.38f, 0.20f, 0.15f, 0.55f); }

        /// <summary>Опущенная рука: пальцы смотрят ВНИЗ (геометрия перевёрнута).</summary>
        private static KvHandFrame HangingHand()
        {
            KvHandFrame frame = Shaped(0.15f, 0.15f, 0.15f, 0.15f, 0.15f, 0.20f, 0.30f);
            frame.wrist = new Vector3(0f, 1.10f, 0.40f);
            frame.palm = new Vector3(0f, 1.05f, 0.40f);
            frame.indexProximal = new Vector3(0.03f, 1.02f, 0.40f);
            frame.indexTip = new Vector3(0.03f, 0.94f, 0.40f);
            return frame;
        }

        /// <summary>Настоящий щипок: сила 0,92, указательный согнут в допустимом диапазоне.</summary>
        private static KvHandFrame Pinch() { return Shaped(0.55f, 0.45f, 0.45f, 0.45f, 0.92f, 0.15f, 0.60f); }

        /// <summary>«Почти щипок»: 0,70 — при СТРОГОМ пороге 0,85 этого мало.</summary>
        private static KvHandFrame NearlyPinch() { return Shaped(0.55f, 0.45f, 0.45f, 0.45f, 0.70f, 0.15f, 0.55f); }

        /// <summary>Кулак: все пальцы сжаты, сила щипка случайно высокая — но это не щипок.</summary>
        private static KvHandFrame Fist() { return Shaped(0.95f, 0.95f, 0.95f, 0.95f, 0.60f, 0.05f, 0.90f); }

        /// <summary>Указательный палец: сам прямой, три остальных согнуты сильнее 0,75.</summary>
        private static KvHandFrame IndexPoint() { return Shaped(0.10f, 0.85f, 0.85f, 0.85f, 0.20f, 0.20f, 0.40f); }

        /// <summary>«Полууказание»: остальные согнуты только до 0,60 — при строгом пороге 0,75 не годится.</summary>
        private static KvHandFrame SemiPointing() { return Shaped(0.10f, 0.60f, 0.60f, 0.60f, 0.20f, 0.20f, 0.40f); }
    }
}
