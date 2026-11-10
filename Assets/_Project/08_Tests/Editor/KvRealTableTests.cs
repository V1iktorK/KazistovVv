// ================================================================================================
//  KvRealTableTests.cs — РАСПОЗНАВАНИЕ РЕАЛЬНОГО СТОЛА: ТЕСТЫ БЕЗ УСТРОЙСТВ (EditMode).
//
//  ОТКУДА ЗАДАЧА. На приёмке владелец сформулировал дефект одной фразой: «робот стоит на полу, а
//  приложение говорит, что на столе». Причина была в правиле выбора: и в AR, и в MR столом считалась
//  «самая большая горизонтальная плоскость». Пол горизонтален, огромен и стабилен — он и выигрывал.
//  В MR разметка `Table` запрашивалась, но её результат никто не проверял, поэтому строка
//  «получено столов N» ничего не доказывала.
//
//  ЧТО ЗДЕСЬ ПРОВЕРЯЕТСЯ. Классификатор `KvRealTableClassifier` — чистая функция над числами, и
//  ровно поэтому его можно проверить БЕЗ планшета, БЕЗ шлема и БЕЗ реального стола:
//    • пол (огромная горизонтальная плоскость на уровне пола) — НЕ стол, и в причине это написано;
//    • столешница на 0.75 м над полом, 1.2 × 0.8 м — стол;
//    • семантика платформы сильнее геометрии: `Table` — стол, `Floor` — отказ при любой площади;
//    • наклон, площадь, форма полосы, уровень глаз — отказы с числами в причине;
//    • свежая плоскость — «недостаточно данных», через 2 с — стол;
//    • гистерезис: выбор не мерцает между двумя столами;
//    • мусор (NaN, нули, отрицательные стороны) не бросает исключений;
//    • сторож: `KvArPlaneVisualizer` обязан спрашивать классификатор, а не «самую большую площадь».
//
//  ГРАНИЦА ЧЕСТНОСТИ. Тест доказывает правила по числам, а не то, что ARCore/PICO эти числа дают
//  правильно. Что осталось непроверяемым без физического стола перед камерой — перечислено в отчёте.
// ================================================================================================

using System.IO;
using System.Text.RegularExpressions;
using KazistovVvAr;
using KazistovVvRealWorld;
using KazistovVvXr;
using NUnit.Framework;
using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace KazistovVvTests
{
    /// <summary>Стол это или просто поверхность: правила, причина и гистерезис.</summary>
        [Category(KvTestCategories.Fast)]
    public class KvRealTableTests
    {
        /// <summary>Высота глаз оператора, м — обычный рост, камера на уровне лица.</summary>
        private const float EyeHeight = 1.40f;

        /// <summary>Уровень пола, м.</summary>
        private const float FloorY = 0f;

        /// <summary>
        /// ВЫСОТА СТОЛЕШНИЦЫ, м — обычный письменный стол. Отдельной константой, потому что от неё
        /// зависит почти каждый тест: «стол» в них отличается от «пола» именно высотой, и число
        /// должно быть одно, иначе тесты начнут спорить друг с другом.
        /// </summary>
        private const float DeskY = 0.75f;

        /// <summary>
        /// КАНДИДАТ В СТОЛ ДЛЯ ТЕСТА. Значения по умолчанию — «нормальная столешница»: 1.2 × 0.8 м,
        /// на 0.75 м над полом, нормаль строго вверх, отслеживается 3 секунды. Тест меняет ТОЛЬКО
        /// то, что проверяет, — поэтому падение теста однозначно указывает на порог.
        /// </summary>
        private static RealSurfaceCandidate Candidate(float sideA = 1.2f, float sideB = 0.8f,
            float centerY = DeskY, float trackedSeconds = 3f,
            KvRealSurfaceKind kind = KvRealSurfaceKind.Unknown,
            KvRealSurfaceSource source = KvRealSurfaceSource.Plane,
            Vector3? up = null, bool largestHorizontal = false)
        {
            RealSurfaceCandidate candidate;
            candidate.Kind = kind;
            candidate.Source = source;
            candidate.Center = new Vector3(0f, centerY, 1.5f);
            candidate.Up = up.HasValue ? up.Value : Vector3.up;
            candidate.SideA = sideA;
            candidate.SideB = sideB;
            candidate.TrackedSeconds = trackedSeconds;
            candidate.IsLargestHorizontal = largestHorizontal;
            return candidate;
        }

        private static RealTableVerdict ClassifyWithFloor(RealSurfaceCandidate candidate)
        {
            return KvRealTableClassifier.Classify(candidate, EyeHeight, FloorY, true);
        }

        // ================================================================ 1. ПОЛ — НЕ СТОЛ

        /// <summary>
        /// ПОЛ: огромная горизонтальная плоскость на уровне пола. Именно этот случай прежний код
        /// считал столом (пол всегда «самая большая горизонтальная»). Теперь отказ, и в причине
        /// написано, что это ПОЛ, с числом высоты над полом.
        /// </summary>
        [Test]
        public void FloorAtFloorLevel_IsNotATable()
        {
            RealSurfaceCandidate floor = Candidate(4f, 4f, FloorY, 5f, KvRealSurfaceKind.Unknown,
                KvRealSurfaceSource.Plane, Vector3.up, true);
            RealTableVerdict verdict = ClassifyWithFloor(floor);

            Assert.IsFalse(verdict.IsTable,
                "пол признан столом — это ровно дефект «робот на полу с подписью стол». Причина: " +
                verdict.Reason);
            Assert.AreEqual(KvRealSurfaceKind.Floor, verdict.Kind, "пол обязан быть назван полом");
            StringAssert.Contains("пол", verdict.Reason.ToLowerInvariant(),
                "в причине не сказано, что это пол: " + verdict.Reason);
            Assert.IsTrue(Regex.IsMatch(verdict.Reason, @"\d"),
                "в причине нет чисел — по журналу нельзя доказать результат: " + verdict.Reason);
            Assert.GreaterOrEqual(verdict.Confidence, 0.85f,
                "отказ «это пол» обязан быть уверенным, иначе он будет спорить с геометрией");
        }

        /// <summary>СТОЛ: 1.2 × 0.8 м на 0.75 м над полом при глазах на 1.4 м — обычный письменный стол.</summary>
        [Test]
        public void TableAboveFloor_IsRecognized()
        {
            RealTableVerdict verdict = ClassifyWithFloor(Candidate());
            Assert.IsTrue(verdict.IsTable, "настоящая столешница не признана столом: " + verdict.Reason);
            Assert.Greater(verdict.Confidence, 0.60f, "уверенность настоящего стола подозрительно низкая");
            Assert.Less(verdict.Confidence, KvRealTableClassifier.SemanticTableConfidence,
                "геометрия не должна равняться точной разметке платформы");
            StringAssert.Contains("0.96", verdict.Reason, "площадь 1.2×0.8 = 0.96 м² не названа");
            StringAssert.Contains("0.75", verdict.Reason, "высота над полом не названа");

            // Низкий столик 0.45 м (журнальный) — тоже стол: он выше пола на 0.45 м.
            Assert.IsTrue(ClassifyWithFloor(Candidate(1.0f, 0.6f, 0.45f)).IsTable,
                "журнальный столик 0.45 м над полом обязан быть столом");
        }

        /// <summary>СЕМАНТИКА СИЛЬНЕЕ ГЕОМЕТРИИ: если платформа размечтала объект как стол — это стол.</summary>
        [Test]
        public void SemanticTable_BeatsGeometricFloor()
        {
            // Геометрически это «пол»: 16 м², на уровне пола. Но разметка говорит «стол».
            RealSurfaceCandidate semantic = Candidate(4f, 4f, FloorY, 5f, KvRealSurfaceKind.Table,
                KvRealSurfaceSource.Semantic, Vector3.up, true);
            RealTableVerdict verdict = ClassifyWithFloor(semantic);

            Assert.IsTrue(verdict.IsTable,
                "разметка «стол» проиграла геометрии — правила перепутаны местами: " + verdict.Reason);
            Assert.AreEqual(KvRealSurfaceKind.Table, verdict.Kind);
            Assert.AreEqual(KvRealTableClassifier.SemanticTableConfidence, verdict.Confidence, 0.001f,
                "уверенность по разметке обязана быть максимальной (0.95)");
        }

        /// <summary>
        /// СЕМАНТИКА «ПОЛ» ОТКЛОНЯЕТСЯ ВСЕГДА: даже огромная площадь и идеальная горизонтальность не
        /// делают пол столом (это и был главный дефект прежнего правила «самая большая площадь»).
        /// </summary>
        [Test]
        public void SemanticFloor_IsRejectedAlways()
        {
            RealTableVerdict verdict = ClassifyWithFloor(Candidate(6f, 5f, FloorY, 30f,
                KvRealSurfaceKind.Floor, KvRealSurfaceSource.Plane, Vector3.up, true));

            Assert.IsFalse(verdict.IsTable, "разметка «пол» принята за стол: " + verdict.Reason);
            Assert.AreEqual(KvRealSurfaceKind.Floor, verdict.Kind);
            StringAssert.Contains("пол", verdict.Reason.ToLowerInvariant());

            // Потолок и стена — тоже не стол, и это тоже сказано словами.
            Assert.AreEqual(KvRealSurfaceKind.Ceiling,
                ClassifyWithFloor(Candidate(kind: KvRealSurfaceKind.Ceiling)).Kind);
            Assert.IsFalse(ClassifyWithFloor(Candidate(kind: KvRealSurfaceKind.Ceiling)).IsTable);
            Assert.IsFalse(ClassifyWithFloor(Candidate(kind: KvRealSurfaceKind.Wall)).IsTable,
                "стена не может быть столом");
        }

        // ================================================================ 2. ГЕОМЕТРИЧЕСКИЕ ОТКАЗЫ

        /// <summary>НАКЛОН: 30° и 60° — не стол, и в причине есть угол в градусах.</summary>
        [Test]
        public void TiltedSurface_IsNotATable()
        {
            float[] angles = { 30f, 60f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 up = Quaternion.Euler(angles[i], 0f, 0f) * Vector3.up;
                RealTableVerdict verdict = ClassifyWithFloor(Candidate(up: up));

                Assert.IsFalse(verdict.IsTable,
                    "плоскость с наклоном " + angles[i] + "° признана столом: " + verdict.Reason);
                StringAssert.Contains("наклон", verdict.Reason,
                    "в причине нет слова «наклон»: " + verdict.Reason);

                Match m = Regex.Match(verdict.Reason, @"наклон\s+(\d+[.,]\d+)");
                Assert.IsTrue(m.Success, "в причине нет числа градусов: " + verdict.Reason);
                float stated = float.Parse(m.Groups[1].Value.Replace(',', '.'),
                    System.Globalization.CultureInfo.InvariantCulture);
                Assert.AreEqual(angles[i], stated, 0.5f,
                    "названный угол " + stated + "° не совпадает с настоящим " + angles[i] + "°");
            }
        }

        /// <summary>МАЛЕНЬКАЯ ПЛОЩАДЬ: 0.5 × 0.2 = 0.1 м² — не стол, в причине есть площадь.</summary>
        [Test]
        public void TinySurface_IsNotATable()
        {
            RealTableVerdict verdict = ClassifyWithFloor(Candidate(0.5f, 0.2f));
            Assert.IsFalse(verdict.IsTable, "площадь 0.1 м² признана столом: " + verdict.Reason);
            StringAssert.Contains("0.10", verdict.Reason, "площадь не названа числом: " + verdict.Reason);
            StringAssert.Contains("0.25", verdict.Reason, "не назван порог площади: " + verdict.Reason);
        }

        /// <summary>СЛИШКОМ БОЛЬШАЯ ПЛОЩАДЬ: 2 × 3 = 6 м² — это не столешница, а пол/площадка.</summary>
        [Test]
        public void HugeSurface_IsNotATable()
        {
            RealTableVerdict verdict = ClassifyWithFloor(Candidate(2f, 3f, DeskY));
            Assert.IsFalse(verdict.IsTable, "площадь 6 м² признана столешницей: " + verdict.Reason);
            StringAssert.Contains("6.00", verdict.Reason, "площадь не названа: " + verdict.Reason);
            StringAssert.Contains("3.00", verdict.Reason, "порог 3 м² не назван: " + verdict.Reason);
        }

        /// <summary>ПОЛОСА: 0.3 × 5 м — отношение сторон 16.7, это не столешница.</summary>
        [Test]
        public void StripSurface_IsNotATable()
        {
            RealTableVerdict verdict = ClassifyWithFloor(Candidate(0.3f, 5f, DeskY));
            Assert.IsFalse(verdict.IsTable, "полоса 0.3×5 м признана столешницей: " + verdict.Reason);
            StringAssert.Contains("отношение сторон", verdict.Reason,
                "причина не называет форму: " + verdict.Reason);

            // Но и обратный случай: 5 × 0.3 — та же полоса, правило симметрично.
            Assert.IsFalse(ClassifyWithFloor(Candidate(5f, 0.3f, DeskY)).IsTable);
        }

        /// <summary>УРОВЕНЬ ГЛАЗ: поверхность почти на уровне камеры — не стол.</summary>
        [Test]
        public void SurfaceAtEyeLevel_IsNotATable()
        {
            RealTableVerdict verdict = ClassifyWithFloor(Candidate(1.2f, 0.8f, EyeHeight - 0.05f));
            Assert.IsFalse(verdict.IsTable,
                "поверхность на уровне глаз признана столом: " + verdict.Reason);
            StringAssert.Contains("уровне глаз", verdict.Reason,
                "причина не объясняет, что поверхность на уровне глаз: " + verdict.Reason);
        }

        /// <summary>
        /// СВЕЖАЯ ПЛОСКОСТЬ: 0.1 с — «недостаточно данных» (и причина это объясняет), а через 2 с тот
        /// же кандидат становится столом. Так выглядит ARCore: габариты уточняются за полсекунды.
        /// </summary>
        [Test]
        public void FreshSurface_WaitsThenBecomesTable()
        {
            RealTableVerdict fresh = ClassifyWithFloor(Candidate(trackedSeconds: 0.1f));
            Assert.IsFalse(fresh.IsTable, "свежая плоскость сразу принята столом: " + fresh.Reason);
            StringAssert.Contains("недостаточно данных", fresh.Reason,
                "причина не говорит, что данных мало: " + fresh.Reason);
            StringAssert.Contains("0.10", fresh.Reason, "в причине нет прошедшего времени: " + fresh.Reason);
            StringAssert.Contains("0.50", fresh.Reason, "в причине нет требуемого времени: " + fresh.Reason);

            RealTableVerdict aged = ClassifyWithFloor(Candidate(trackedSeconds: 2f));
            Assert.IsTrue(aged.IsTable,
                "тот же кандидат через 2 с обязан стать столом (иначе «ждём» превращается в «никогда»)");
        }

        // ================================================================ 3. ГИСТЕРЕЗИС

        /// <summary>
        /// ГИСТЕРЕЗИС: новый кандидат лучше на 5 % — остаётся прежний стол; лучше на 40 % — выбирается
        /// новый. Без этого правила подсветка мерцает между двумя столами, и оператор не понимает,
        /// куда встанет робот.
        /// </summary>
        [Test]
        public void Hysteresis_KeepsTableUntilClearlyBetter()
        {
            RealTableVerdict current = Verdict(0.60f);
            Assert.IsFalse(KvRealTableClassifier.ShouldReplace(current, Verdict(0.63f)),
                "кандидат лучше всего на 5 % — выбор обязан остаться прежним (иначе мерцание)");
            Assert.IsTrue(KvRealTableClassifier.ShouldReplace(current, Verdict(0.84f)),
                "кандидат лучше на 40 % — выбор обязан переключиться");
            Assert.IsTrue(KvRealTableClassifier.ShouldReplace(NotATable(), Verdict(0.10f)),
                "если прежнего стола нет, подходит любой стол");
            Assert.IsFalse(KvRealTableClassifier.ShouldReplace(current, NotATable()),
                "не-стол никогда не заменяет стол");

            // И то же самое на настоящих кандидатах: два стола, второй чуть лучше — остаётся первый.
            RealTableVerdict first = ClassifyWithFloor(Candidate(1.2f, 0.8f, DeskY, 2f));
            RealTableVerdict second = ClassifyWithFloor(Candidate(1.2f, 0.75f, DeskY, 3f));
            Assert.IsTrue(first.IsTable && second.IsTable, "оба кандидата обязаны быть столами");
            Assert.Greater(second.Confidence, first.Confidence, "второй стол обязан быть «лучше» для теста");

            var verdicts = new System.Collections.Generic.List<RealTableVerdict> { first, second };
            Assert.AreEqual(0, KvRealTableClassifier.SelectBestIndex(verdicts, 0),
                "незначительно лучший второй стол не должен переключать выбор с первого");
            Assert.AreEqual(1, KvRealTableClassifier.SelectBestIndex(verdicts, -1),
                "без прежнего выбора берётся действительно лучший");
        }

        /// <summary>
        /// ВЫБОР НЕ РАВЕН «САМОЙ БОЛЬШОЙ ПЛОЩАДИ»: пол в 16 м² проигрывает столешнице 0.96 м².
        /// Это поведенческий сторож главного дефекта.
        /// </summary>
        [Test]
        public void Selection_PicksVerifiedTableNotBiggestFloor()
        {
            RealSurfaceCandidate floor = Candidate(4f, 4f, FloorY, 5f, KvRealSurfaceKind.Unknown,
                KvRealSurfaceSource.Plane, Vector3.up, true);
            RealSurfaceCandidate table = Candidate();

            var verdicts = new System.Collections.Generic.List<RealTableVerdict>
            {
                ClassifyWithFloor(floor),
                ClassifyWithFloor(table)
            };

            int best = KvRealTableClassifier.SelectBestIndex(verdicts, -1);
            Assert.AreEqual(1, best,
                "выбран пол (индекс 0) вместо столешницы — правило «самая большая плоскость» вернулось");
            Assert.IsTrue(verdicts[best].IsTable);
            Assert.Greater(Mathf.Abs(floor.SideA * floor.SideB), Mathf.Abs(table.SideA * table.SideB),
                "пол в тесте обязан быть БОЛЬШЕ стола, иначе проверка ничего не доказывает");
        }

        // ================================================================ 4. ПРИЧИНА И УСТОЙЧИВОСТЬ

        /// <summary>
        /// ПРИЧИНА ВСЕГДА С ЧИСЛАМИ. Без чисел в `Reason` тестировщик не может доказать по logcat,
        /// что нашёлся именно стол, — это требование приёмки, а не украшение.
        /// </summary>
        [Test]
        public void ReasonAlwaysCarriesNumbers()
        {
            var candidates = new[]
            {
                Candidate(),                                                   // стол
                Candidate(4f, 4f, FloorY, 5f),                                  // пол по высоте
                Candidate(0.5f, 0.2f),                                          // мало
                Candidate(2f, 3f),                                              // много
                Candidate(0.3f, 5f),                                            // полоса
                Candidate(up: Quaternion.Euler(45f, 0f, 0f) * Vector3.up),       // наклон
                Candidate(1.2f, 0.8f, EyeHeight - 0.05f),                       // уровень глаз
                Candidate(trackedSeconds: 0.1f),                                // мало данных
                Candidate(kind: KvRealSurfaceKind.Floor),                       // разметка «пол»
                Candidate(kind: KvRealSurfaceKind.Table, source: KvRealSurfaceSource.Semantic)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                RealTableVerdict verdict = ClassifyWithFloor(candidates[i]);
                Assert.IsFalse(string.IsNullOrEmpty(verdict.Reason), "пустая причина у случая " + i);
                Assert.IsTrue(Regex.IsMatch(verdict.Reason, @"\d"), "нет цифр в причине, случай " + i +
                    ": " + verdict.Reason);
                Assert.IsTrue(Regex.IsMatch(verdict.Reason, @"\d+[.,]\d+"),
                    "нет числа с дробной частью в причине, случай " + i + ": " + verdict.Reason);
                Assert.IsTrue(Regex.IsMatch(verdict.Reason, @"уверенность\s+\d+[.,]\d+"),
                    "уверенность не названа числом, случай " + i + ": " + verdict.Reason);
            }
        }

        /// <summary>
        /// МУСОР НЕ РОЖДАЕТ ИСКЛЮЧЕНИЙ. Габариты приходят от ARCore/PICO, а при потере трекинга там
        /// штатно бывает NaN. Классификатор обязан вернуть отказ, а не уронить кадр устройства.
        /// </summary>
        [Test]
        public void GarbageInput_DoesNotThrow()
        {
            var garbage = new[]
            {
                Candidate(0f, 0f, DeskY),                                       // нулевые габариты
                Candidate(-1.2f, -0.8f, DeskY),                                 // отрицательные стороны
                Candidate(-0.3f, 5f, DeskY),                                    // отрицательная сторона
                Candidate(float.NaN, 0.8f, DeskY),                              // NaN в стороне
                Candidate(1.2f, float.PositiveInfinity, DeskY),                 // бесконечность
                Candidate(1.2f, 0.8f, float.NaN),                               // NaN в высоте
                Candidate(up: Vector3.zero),                                     // нулевая нормаль
                Candidate(up: new Vector3(float.NaN, float.NaN, float.NaN)),     // NaN в нормали
                Candidate(trackedSeconds: float.NaN)                            // NaN в возрасте
            };

            for (int i = 0; i < garbage.Length; i++)
            {
                RealTableVerdict verdict = default(RealTableVerdict);
                Assert.DoesNotThrow(() => verdict = ClassifyWithFloor(garbage[i]),
                    "классификатор бросил исключение на мусоре, случай " + i);
                Assert.IsFalse(verdict.Kind == KvRealSurfaceKind.Table && !verdict.IsTable,
                    "противоречивый результат на мусоре, случай " + i);
                Assert.IsFalse(string.IsNullOrEmpty(verdict.Reason),
                    "на мусоре не записана причина, случай " + i);
            }

            // Нулевые габариты и нулевая нормаль — это НЕ стол (нулевая площадь/наклона нет).
            Assert.IsFalse(ClassifyWithFloor(Candidate(0f, 0f, DeskY)).IsTable);
            Assert.IsFalse(ClassifyWithFloor(Candidate(up: Vector3.zero)).IsTable,
                "плоскость без нормали не может быть столом");

            // Отрицательные стороны — это те же габариты по модулю: 1.2 × 0.8 над полом = стол.
            Assert.IsTrue(ClassifyWithFloor(Candidate(-1.2f, -0.8f, DeskY)).IsTable,
                "отрицательные габариты обязаны браться по модулю, а не превращаться в отказ");

            // «Высота глаз неизвестна» (0) не должна валить разбор правила уровня глаз.
            Assert.DoesNotThrow(() => KvRealTableClassifier.Classify(Candidate(), 0f, 0f, false));
        }

        /// <summary>
        /// ВЫВОД УРОВНЯ ПОЛА ТРЕБУЕТ ДВУХ УРОВНЕЙ. Иначе единственная горизонтальная плоскость перед
        /// камерой (сам стол) была бы объявлена полом, и постановка на реальный стол стала бы
        /// невозможной — то есть «лечение» хуже болезни.
        /// </summary>
        [Test]
        public void FloorInference_RequiresTwoLevels()
        {
            var oneLevel = new System.Collections.Generic.List<RealSurfaceCandidate>
            {
                Candidate(1.2f, 0.8f, DeskY)
            };
            float floor;
            Assert.IsFalse(KvRealTableClassifier.TryInferFloorHeight(oneLevel, out floor),
                "уровень пола выведен по ОДНОЙ плоскости — так настоящий стол запретили бы навсегда");

            var twoLevels = new System.Collections.Generic.List<RealSurfaceCandidate>
            {
                Candidate(1.2f, 0.8f, DeskY),
                Candidate(4f, 4f, FloorY, 5f)
            };
            Assert.IsTrue(KvRealTableClassifier.TryInferFloorHeight(twoLevels, out floor),
                "пол не выведен, хотя в сцене два уровня поверхностей");
            Assert.AreEqual(FloorY, floor, 0.01f, "выведен не уровень пола");

            // Пол не выводится из вертикальных плоскостей: стена — не уровень пола.
            var walls = new System.Collections.Generic.List<RealSurfaceCandidate>
            {
                Candidate(4f, 4f, FloorY, 5f, KvRealSurfaceKind.Unknown, KvRealSurfaceSource.Mesh,
                    Vector3.forward),
                Candidate(1.2f, 0.8f, DeskY)
            };
            Assert.IsFalse(KvRealTableClassifier.TryInferFloorHeight(walls, out floor),
                "уровень пола выведен из вертикальной поверхности");
        }

        /// <summary>
        /// ЧЕСТНАЯ СТРОКА ДЛЯ ЭКРАНА И ЖУРНАЛА: три формы, требуемые приёмкой, и ни одного
        /// утверждения без числа.
        /// </summary>
        [Test]
        public void DescribeText_SaysTheTruthInThreeForms()
        {
            // 1. Стол: «стол найден · площадь X м² · высота над полом Y м».
            RealSurfaceCandidate table = Candidate();
            RealTableVerdict tableVerdict = ClassifyWithFloor(table);
            string tableText = KvRealTableClassifier.Describe(table, tableVerdict, FloorY, true);
            StringAssert.Contains("стол найден", tableText);
            StringAssert.Contains("площадь 0.96 м²", tableText);
            StringAssert.Contains("высота над полом 0.75 м", tableText);

            // 2. Пол: «пол (не стол) · площадь X м²».
            RealSurfaceCandidate floor = Candidate(4f, 4f, FloorY, 5f);
            RealTableVerdict floorVerdict = ClassifyWithFloor(floor);
            string floorText = KvRealTableClassifier.Describe(floor, floorVerdict, FloorY, true);
            StringAssert.Contains("пол (не стол)", floorText);
            StringAssert.Contains("площадь 16.00 м²", floorText);
            Assert.IsFalse(floorText.Contains("стол найден"),
                "строка про пол не должна содержать «стол найден»: " + floorText);

            // 3. Отказ: «стол не найден: <причина с числами>».
            RealSurfaceCandidate strip = Candidate(0.3f, 5f, DeskY);
            string stripText = KvRealTableClassifier.Describe(strip, ClassifyWithFloor(strip), FloorY, true);
            StringAssert.StartsWith("стол не найден:", stripText);
            Assert.IsTrue(Regex.IsMatch(stripText, @"\d"), "в отказе нет чисел: " + stripText);
        }

        // ================================================================ 5. ПОДКЛЮЧЕНИЕ К AR И MR

        // СТОРОЖ «ВИЗУАЛИЗАТОР СПРАШИВАЕТ КЛАССИФИКАТОР» ИСХОДНИКОМ ЗДЕСЬ НЕ ЖИВЁТ (12.11.2026).
        //
        // ЧТО БЫЛО. Проверка закрепляла, что `KvArPlaneVisualizer` вызывает `KvRealTableClassifier`,
        // поиском подстрок в тексте исходника. Это ровно тот приём, из-за которого в проекте вводился
        // храповик P4-3: тест зеленеет от переформулировки комментария и ПРОХОДИТ на сломанном коде
        // (строка осталась, а вызов недостижим), и при этом от него нельзя отличить «спросили
        // классификатор» от «спросили и проигнорировали ответ».
        //
        // ЧЕМ ЗАМЕНЕНО. Тем же, чем доказан сам результат: ЖУРНАЛОМ С УСТРОЙСТВА. Гейт требует, чтобы
        // на планшете в раскладке AR строка «[KvAr] ПОВЕРХНОСТИ: …» несла вердикт классификатора с
        // числами (площадь, высота над полом, уверенность, причина), а при виде одного пола — прямую
        // фразу «пол (не стол)». Это сильнее поиска строки: журнал печатает РЕЗУЛЬТАТ работы
        // классификатора на реальных плоскостях ARCore, а не факт наличия вызова.
        // Проверка: `Tools/acceptance-gate.ps1`, пункт `REAL_TABLE`.

        /// <summary>
        /// РАЗМЕТКА ARCORE ДОХОДИТ ДО КЛАССИФИКАТОРА: плоскость, помеченная как пол, приходит
        /// «полом», а не «неизвестно». Иначе правило «семантика сильнее геометрии» в AR мертво.
        /// </summary>
        [Test]
        public void ArSurfaceKind_MapsArCoreClassifications()
        {
            Assert.AreEqual(KvRealSurfaceKind.Table,
                KvArPlaneVisualizer.KindFromClassifications(PlaneClassifications.Table));
            Assert.AreEqual(KvRealSurfaceKind.Floor,
                KvArPlaneVisualizer.KindFromClassifications(PlaneClassifications.Floor));
            Assert.AreEqual(KvRealSurfaceKind.Ceiling,
                KvArPlaneVisualizer.KindFromClassifications(PlaneClassifications.Ceiling));
            Assert.AreEqual(KvRealSurfaceKind.Wall,
                KvArPlaneVisualizer.KindFromClassifications(PlaneClassifications.WallFace));
            Assert.AreEqual(KvRealSurfaceKind.Unknown,
                KvArPlaneVisualizer.KindFromClassifications(PlaneClassifications.None),
                "без разметки ARCore ничего не утверждает — решает геометрия");
            Assert.AreEqual(KvRealSurfaceKind.Other,
                KvArPlaneVisualizer.KindFromClassifications(PlaneClassifications.Couch));

            // Если ARCore пометил плоскость и столом, и сиденьем — верим «столу».
            Assert.AreEqual(KvRealSurfaceKind.Table,
                KvArPlaneVisualizer.KindFromClassifications(
                    PlaneClassifications.Table | PlaneClassifications.SeatOfAnyType));
        }

        /// <summary>
        /// РАЗМЕТКА PICO ДОХОДИТ ДО КЛАССИФИКАТОРА: `PxrSemanticLabel.Table` → «стол»,
        /// `Floor` → «пол», кресло → «другое», виртуальная стена → «стена».
        /// </summary>
        [Test]
        public void MrSurfaceKind_MapsPicoLabels()
        {
            Assert.AreEqual(KvRealSurfaceKind.Table,
                KvMrSurfaces.KindFromSemanticLabel(PxrSemanticLabel.Table));
            Assert.AreEqual(KvRealSurfaceKind.Floor,
                KvMrSurfaces.KindFromSemanticLabel(PxrSemanticLabel.Floor));
            Assert.AreEqual(KvRealSurfaceKind.Ceiling,
                KvMrSurfaces.KindFromSemanticLabel(PxrSemanticLabel.Ceiling));
            Assert.AreEqual(KvRealSurfaceKind.Wall,
                KvMrSurfaces.KindFromSemanticLabel(PxrSemanticLabel.Wall));
            Assert.AreEqual(KvRealSurfaceKind.Other,
                KvMrSurfaces.KindFromSemanticLabel(PxrSemanticLabel.Chair),
                "кресло — не стол, но и не «неизвестно»");
            Assert.AreEqual(KvRealSurfaceKind.Unknown,
                KvMrSurfaces.KindFromSemanticLabel(PxrSemanticLabel.Unknown));

            // Разметка «стол» из шлема проходит те же правила, что и на планшете.
            RealTableVerdict verdict = ClassifyWithFloor(Candidate(1.2f, 0.8f, DeskY,
                kind: KvMrSurfaces.KindFromSemanticLabel(PxrSemanticLabel.Table),
                source: KvRealSurfaceSource.Semantic));
            Assert.IsTrue(verdict.IsTable);
            Assert.AreEqual(KvRealTableClassifier.SemanticTableConfidence, verdict.Confidence, 0.001f);
        }

        // СТОРОЖ «ПОСТАНОВКА ПРОВЕРЯЕТ ПЛОЩАДКУ» — ТОЖЕ ПЕРЕЕХАЛ НА ЖУРНАЛ С УСТРОЙСТВА.
        //
        // ЧТО БЫЛО. Проверка искала в исходниках `KvArPlacement`, `KvMrPlacement` и `KvMrSurfaces`
        // подстроки вроде «ClassifySurface» и «стол НЕ подтверждён». Это тот же дефект метода, что и
        // у предыдущего сторожа: подпись можно оставить в недостижимой ветке, и тест этого не увидит.
        //
        // ЧЕМ ЗАМЕНЕНО. Гейт (`REAL_TABLE`) требует от устройств ФАКТИЧЕСКИХ строк журнала: на
        // планшете — «проверка площадки: СТОЛ подтверждён» либо «НЕ признана столом» при постановке;
        // в шлеме — «[KvMr] РЕАЛЬНЫЙ СТОЛ: …» с числами либо честное объяснение, что разметки
        // помещения нет. Это проверяет результат на живом пути, а не наличие слова в файле.

        // ================================================================ служебное

        private static RealTableVerdict Verdict(float confidence)
        {
            RealTableVerdict verdict;
            verdict.IsTable = true;
            verdict.Confidence = confidence;
            verdict.Kind = KvRealSurfaceKind.Table;
            verdict.Reason = "стол по геометрии · уверенность " + confidence.ToString("0.00");
            return verdict;
        }

        private static RealTableVerdict NotATable()
        {
            RealTableVerdict verdict;
            verdict.IsTable = false;
            verdict.Confidence = 0.9f;
            verdict.Kind = KvRealSurfaceKind.Floor;
            verdict.Reason = "это ПОЛ · площадь 16.00 м² · уверенность 0.90";
            return verdict;
        }

            }
}
