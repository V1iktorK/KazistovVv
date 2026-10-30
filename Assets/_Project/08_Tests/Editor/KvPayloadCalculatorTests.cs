// ============================================================================================
//  EditMode-тесты (Unity Test Framework / NUnit) для KvPayloadCalculator — «нагрузка, моменты».
//
//  Тестируемый файл: Assets/_Project/01_Scripts/Features/KvPayloadCalculator.cs
//  Сборка: Assembly-CSharp-Editor (папка Editor, без asmdef). Изоляция: НЕ создаются GameObject,
//  MonoBehaviour, компоненты и сцены; нет PlayerPrefs (кроме косвенного чтения в StatusLine),
//  нет файлового ввода-вывода, сети и реального времени — все проверки детерминированы.
//
//  МОДЕЛЬ И ФОРМУЛЫ, КОТОРЫЕ ПРИМЕНЯЕТ КАЛЬКУЛЯТОР (сверено по коду, строки указаны по
//  KvPayloadCalculator.cs) — нужны, чтобы понимать, что именно осталось непокрытым:
//    * g — ЛОКАЛЬНАЯ КОНСТАНТА 9.81 (строки 166, 313, 367); Physics.gravity в файле НЕ используется;
//    * вращательный сустав, момент от груза на 1 кг:
//          M_груза(1 кг) = |dot(cross(tcp - pivot_j, up * g), axis_j)|                     (211)
//    * вклад веса звеньев (каждое звено — в своём центре, середина пивот-следующий пивот):
//          M_звеньев_j = Σ(k=j..Dof-1) m_k * |dot(cross(center_k - pivot_j, up * g), axis_j)| (217-223)
//    * допустимая масса груза у сустава:
//          kg_j = (rating_j / safety - M_звеньев_j) / M_груза(1 кг)                         (225-226)
//    * итог: maxKg = max(0, min_j kg_j - toolMassKg)                                        (240)
//    * призматическая ось (SCARA): F = m_дист * g; kg = (ratingPrismaticN / safety - F) / g  (190-196)
//    * доля использования сустава «на 1 кг груза»: jointLoad[j] = M_груза(1 кг) / (rating_j/safety) (227)
//    * запас в JointTorques: load01[j] = |tau_j| / (rating_j / safety), 1.0 ровно на лимите  (403)
//    * доступный момент = rating_j / safety, где safety = max(1, model.safety)              (174, 225)
//  ИНЕРЦИЙ В МОДЕЛИ НЕТ ВООБЩЕ: KvPayloadModel хранит только rating, linkMass,
//  ratingPrismaticN, safety, toolMassKg (строки 37-59) — тестировать инерции нечего.
//
//  ЧТО ПОКРЫТО (30 работающих тестов + 1 явно пропущенный):
//    * KvPayloadModel: дефолтные номиналы/массы/скаляры, пригодность данных для формулы
//      rating/safety, попадание номиналов в диапазон слайдеров вкладки, CopyFrom (null-источник,
//      клонирование массивов, перенос скаляров, фактическое поведение при null-массиве);
//    * KvPayloadResult: значения по умолчанию, Line() для невалидного и валидного результата,
//      1-базовая нумерация сустава;
//    * KvPayloadCalculator: состояние нового экземпляра, ключи PlayerPrefs, CurveSamples
//      (значение по умолчанию, клампинг 4..40, идемпотентность), ВСЕ охраняемые выходы
//      Evaluate (null-поза, пустая поза, экстремальные и нечисловые позы), «нулевые/экстремальные
//      входы не дают NaN/Infinity», детерминизм повторных и чередующихся вызовов, охрана
//      JointTorques (пустые out-массивы, нулевая сила, отказ при любой массе), интервал Tick
//      0.4 с без реального времени, молчание события Message, StatusLine без валидного результата.
//
//  НЕ ПОКРЫТО (причины):
//
//   1) ФИЗИЧЕСКОЕ ЯДРО РАСЧЁТА МОМЕНТОВ — недостижимо в EditMode без сцены.
//      Точки входа: Evaluate (153) и JointTorques (356) после проверки flow.Validator.Ready.
//      Цепочка зависимостей: flow — это TrajectoryFlowController : MonoBehaviour
//      (Trajectory/TrajectoryFlowController.cs:40) с приватным readonly полем
//      `PoseValidator validator` (там же, 167) и публичным только для чтения свойством
//      Validator (313); PoseValidator.Ready становится true ТОЛЬКО после
//      PoseValidator.Init(RobotController) (Trajectory/PoseValidator.cs:41-168), где нужны живые
//      Transform-ы: RobotController : MonoBehaviour (Core/RobotController.cs:8), у 6-осевого —
//      six.jointTransforms (95-143), у SCARA — scara.joint1/joint2/joint3 (144-168).
//      Ни `new GameObject`, ни `AddComponent`, ни `Object.Instantiate` в этих тестах
//      использовать нельзя (жёсткое ограничение задачи), поэтому обе ветки расчёта всегда
//      упираются в охрану `flow == null` (156-161 и 362-363) и возвращают
//      why = "робот не определён". Косвенно это подтверждает и то, что при Dof = 0 массив
//      jointLoad пуст, а limitingJoint остаётся -1 — «момент» физически не с чем считать.
//      Вместе с ядром НЕ проверены пункты ТЗ:
//        п.1 (M = m*g*r) — формула момента от груза, строка 211, g = 9.81f (константа, НЕ Physics.gravity);
//        п.2 (монотонность по массе груза, вылету и массе звена) — kg_j зависит от живых
//            пивотов/осей/TCP (211-226), проверить нечем;
//        п.3-4 (нулевые и отрицательные входы) — в физических ветках payloadKg и q вообще не
//            валидируются: отрицательная масса молча обнуляется через Mathf.Max(0f, payloadKg)
//            (369), знак q определяется кинематикой; фактическое поведение без сцены недостижимо;
//        п.5 (лимит по моменту и граница ±1e-4) — freeTorque = rating/safety - linkLoad (225)
//            и флаг result.valid (239) требуют живых пивотов;
//        п.6 (запас в %) — ВАЖНО: jointLoad[j] (227) это НЕ запас, а доля использования сустава
//            «на 1 кг груза» (её вкладка печатает как проценты, 469), поэтому «0 кг груза → 0 %»
//            по коду не выполняется вовсе; настоящий запас считается только в
//            JointTorques.load01 (403) и там «0 нагрузки → 0 %» тоже неверно, потому что вес
//            звеньев и захвата присутствует всегда: при нулевом грузе load01 уже больше нуля;
//        п.7 (экстремальные массы 1e-6…1e4 кг, вылеты 1e-6…1e3 м, большие инерции) — проверено
//            только на уровне охраны входа (нет NaN/Infinity в результате); инерций в модели нет;
//        п.8 (согласованность/детерминизм полного расчёта) — проверен на уровне состояния
//            сервиса и охраняемых выходов, а не на числах моментов;
//        кривая BuildCurve (275) / MaxPayloadForPose (311) — требуют SolveIk по живым трансформам;
//        призматическая ветка (186-206) — существует только для SCARA со сценой.
//
//   2) KvPayloadCalculator.Model (96), Save (110), Bind (118) — не тестируются: Model и Save
//      читают/пишут PlayerPrefs (105-106, 113-115) — это файловый ввод-вывод, запрещённый
//      правилами задачи, а сохранённое значение делает результат недетерминированным между
//      прогонами; Bind требует TrajectoryFlowController и CollisionWorld (то есть сцену).
//
//   3) KvPayloadCalculator.Tick (126) — покрыт только ранний выход «робот не определён» (134)
//      и интервал 0.4 с; ветка Signature/Evaluate (137-141) требует flow.Validator.Ready.
//
//   4) Приватные члены: Load (101), Signature (144), LinkCenter (253), DistalMass (261),
//      RatingOf (268), BuildCurve (275), MaxPayloadForPose (311), Report (422). Отдельно
//      отмечу: Report (422) не вызывается НИОТКУДА, поэтому событие Message (83) на практике
//      не срабатывает — это зафиксировано тестом (отказ виден только в поле why).
//
//   5) KvPayloadTab (434) — вкладка верстака (IKvWorkbenchTab, KvTabKit, слайдеры, локализация,
//      Time.unscaledTime в UpdatePlot) — UI, вне области unit-тестов.
//
//   6) KvMiniPlot (544) — MonoBehaviour: создаёт GameObject, Texture2D, RawImage, LayoutElement.
//
//   7) KvPayloadResult.Line() для ветки valid = true проверен на результате, собранном вручную:
//      получить валидный KvPayloadResult из калькулятора без сцены невозможно.
//
//   8) StatusLine() проверен только для НЕвалидного результата (411-412); формат валидной
//      строки (413-419) недостижим. Сам вызов проходит через KvLocExtra2.T → KvLoc.T →
//      KvLoc.Ensure(), который читает PlayerPrefs и StreamingAssets, поэтому ожидаемое значение
//      в тесте вычисляется тем же публичным вызовом — проверка не зависит от языка интерфейса.
// ============================================================================================

using System;
using NUnit.Framework;
using UnityEngine;
using KazistovVvFeatures;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты калькулятора нагрузки (этап 10 ТЗ): модель приводов, результат расчёта,
    /// состояние сервиса и все пути, достижимые без привязанного робота. Тесты не создают
    /// GameObject/MonoBehaviour и не используют реальное время, поэтому полностью детерминированы.
    /// </summary>
    [TestFixture]
    public class KvPayloadCalculatorTests
    {
        /// <summary>Текст отказа, когда робот не привязан (KvPayloadCalculator.cs:134 и 158).</summary>
        private const string RobotNotDefined = "робот не определён";

        /// <summary>Поза «нулей» 6-осевого робота — детерминированный вход для охраняемых веток.</summary>
        private static double[] SixAxisPose()
        {
            return new double[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
        }

        /// <summary>Проверка, что значение конечно (не NaN и не бесконечность).</summary>
        private static void AssertFinite(float value, string name)
        {
            Assert.That(float.IsNaN(value), Is.False, name + " не должен быть NaN");
            Assert.That(float.IsInfinity(value), Is.False, name + " не должен быть бесконечностью");
        }

        /// <summary>
        /// Ожидаемый «отказной» результат: робот не привязан, поэтому оценка недействительна,
        /// ограничивающий сустав не найден (-1), а все числа остались нулевыми.
        /// </summary>
        private static void AssertInvalidEstimate(KvPayloadResult r, string context = null)
        {
            string where = string.IsNullOrEmpty(context) ? "" : " [" + context + "]";

            Assert.That(r, Is.Not.Null, "Evaluate обязан вернуть объект результата" + where);
            Assert.That(r.valid, Is.False, "без робота оценка недействительна" + where);
            Assert.That(r.why, Is.EqualTo(RobotNotDefined), "причина отказа" + where);
            Assert.That(r.maxKg, Is.EqualTo(0f).Within(1e-6f), "maxKg" + where);
            Assert.That(r.limitingJoint, Is.EqualTo(-1), "limitingJoint" + where);
            Assert.That(r.limitingValue, Is.EqualTo(0f).Within(1e-6f), "limitingValue" + where);
            Assert.That(r.limitingRating, Is.EqualTo(0f).Within(1e-6f), "limitingRating" + where);
            Assert.That(r.leverM, Is.EqualTo(0f).Within(1e-6f), "leverM" + where);
            Assert.That(r.tcpDistance, Is.EqualTo(0f).Within(1e-6f), "tcpDistance" + where);
        }

        /// <summary>Побайтовое сравнение значимых полей двух результатов (проверка детерминизма).</summary>
        private static void AssertSameOutcome(KvPayloadResult expected, KvPayloadResult actual)
        {
            Assert.That(expected, Is.Not.Null);
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.valid, Is.EqualTo(expected.valid), "valid");
            Assert.That(actual.why, Is.EqualTo(expected.why), "why");
            Assert.That(actual.maxKg, Is.EqualTo(expected.maxKg).Within(1e-6f), "maxKg");
            Assert.That(actual.limitingJoint, Is.EqualTo(expected.limitingJoint), "limitingJoint");
            Assert.That(actual.limitingValue, Is.EqualTo(expected.limitingValue).Within(1e-6f), "limitingValue");
            Assert.That(actual.limitingRating, Is.EqualTo(expected.limitingRating).Within(1e-6f), "limitingRating");
            Assert.That(actual.leverM, Is.EqualTo(expected.leverM).Within(1e-6f), "leverM");
            Assert.That(actual.tcpDistance, Is.EqualTo(expected.tcpDistance).Within(1e-6f), "tcpDistance");
            Assert.That(actual.jointLoad == null, Is.EqualTo(expected.jointLoad == null), "jointLoad");
            Assert.That(actual.distances == null, Is.EqualTo(expected.distances == null), "distances");
            Assert.That(actual.payloads == null, Is.EqualTo(expected.payloads == null), "payloads");
        }

        // ==========================================================================================
        //  KvPayloadModel — данные модели приводов (вход всех формул момента)
        // ==========================================================================================

        /// <summary>
        /// Номинальные моменты по умолчанию: 6 осей со значениями 95 / 95 / 55 / 16 / 11 / 7 Н·м
        /// (KvPayloadCalculator.cs:40). Это числитель формулы «доступный момент = rating / safety» (225),
        /// а также вход слайдеров J1..J6 вкладки верстака (481-486).
        /// </summary>
        [Test]
        public void Model_DefaultRating_HasSixAxesWithExpectedTorques()
        {
            var model = new KvPayloadModel();

            Assert.That(model.rating, Is.Not.Null);
            Assert.That(model.rating.Length, Is.EqualTo(6), "вкладка рисует ровно 6 слайдеров J1..J6");

            float[] expected = { 95f, 95f, 55f, 16f, 11f, 7f };
            for (int i = 0; i < expected.Length; i++)
                Assert.That(model.rating[i], Is.EqualTo(expected[i]).Within(1e-4f), "ось J" + (i + 1));
        }

        /// <summary>
        /// Массы звеньев по умолчанию (KvPayloadCalculator.cs:42): 9.0 / 6.5 / 3.6 / 1.5 / 0.8 / 0.4 кг.
        /// Они складываются в M_звеньев_j (217-223) и в DistalMass для призмы (261-267) и напрямую
        /// уменьшают допустимую нагрузку — рост любой массы обязан уменьшать maxKg.
        /// </summary>
        [Test]
        public void Model_DefaultLinkMass_HasSixExpectedMasses()
        {
            var model = new KvPayloadModel();

            Assert.That(model.linkMass, Is.Not.Null);
            Assert.That(model.linkMass.Length, Is.EqualTo(6), "по одному звену на сустав");

            float[] expected = { 9.0f, 6.5f, 3.6f, 1.5f, 0.8f, 0.4f };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(model.linkMass[i], Is.EqualTo(expected[i]).Within(1e-4f), "звено " + (i + 1));
                Assert.That(model.linkMass[i], Is.GreaterThan(0f), "масса звена положительна");
            }
        }

        /// <summary>
        /// Скалярные настройки модели: номинальное усилие призмы 600 Н (44), коэффициент запаса 1.5 (46),
        /// масса захвата 0.4 кг (48). Запас участвует как делитель: доступный момент = rating / safety (225),
        /// масса захвата вычитается из итога: maxKg = max(0, best - toolMassKg) (240).
        /// </summary>
        [Test]
        public void Model_DefaultScalars_MatchDocumentedDefaults()
        {
            var model = new KvPayloadModel();

            Assert.That(model.ratingPrismaticN, Is.EqualTo(600f).Within(1e-4f), "усилие призмы, Н");
            Assert.That(model.safety, Is.EqualTo(1.5f).Within(1e-4f), "коэффициент запаса");
            Assert.That(model.toolMassKg, Is.EqualTo(0.4f).Within(1e-4f), "масса захвата, кг");
            Assert.That(model.toolMassKg, Is.GreaterThanOrEqualTo(0f), "отрицательная масса захвата недопустима");
        }

        /// <summary>
        /// Дефолтные данные обязаны быть пригодны для формулы доступного момента (KvPayloadCalculator.cs:225):
        ///     M_доступно_j = rating_j / safety,   safety = max(1, model.safety)
        /// ожидаемые значения: J1/J2 = 95 / 1.5 = 63.3333 Н·м, J3 = 55 / 1.5 = 36.6667, J4 = 16 / 1.5 = 10.6667,
        /// J5 = 11 / 1.5 = 7.3333, J6 = 7 / 1.5 = 4.6667 Н·м, призма = 600 / 1.5 = 400 Н.
        /// Запас больше единицы обязан СТРОГО снижать номинал, иначе он не запас.
        /// </summary>
        [Test]
        public void Model_DefaultData_YieldsAvailableTorqueAsRatingOverSafety()
        {
            var model = new KvPayloadModel();

            Assert.That(model.safety, Is.GreaterThan(1f), "иначе деление rating / safety не ограничивает момент");
            Assert.That(model.rating[0] / model.safety, Is.EqualTo(63.3333f).Within(1e-3f), "J1: 95 / 1.5");
            Assert.That(model.rating[1] / model.safety, Is.EqualTo(63.3333f).Within(1e-3f), "J2: 95 / 1.5");
            Assert.That(model.rating[2] / model.safety, Is.EqualTo(36.6667f).Within(1e-3f), "J3: 55 / 1.5");
            Assert.That(model.rating[3] / model.safety, Is.EqualTo(10.6667f).Within(1e-3f), "J4: 16 / 1.5");
            Assert.That(model.rating[4] / model.safety, Is.EqualTo(7.3333f).Within(1e-3f), "J5: 11 / 1.5");
            Assert.That(model.rating[5] / model.safety, Is.EqualTo(4.6667f).Within(1e-3f), "J6: 7 / 1.5");
            Assert.That(model.ratingPrismaticN / model.safety, Is.EqualTo(400f).Within(1e-3f), "призма: 600 / 1.5, Н");

            for (int i = 0; i < model.rating.Length; i++)
            {
                float available = model.rating[i] / model.safety;
                AssertFinite(available, "M_доступно сустава " + (i + 1));
                Assert.That(available, Is.GreaterThan(0f), "доступный момент положителен");
                Assert.That(available, Is.LessThan(model.rating[i]), "запас обязан снижать номинал");
            }
        }

        /// <summary>
        /// Инвариант интерфейса: вкладка рисует слайдеры J1..J6 в диапазоне 1…300 Н·м
        /// (KvPayloadCalculator.cs:484 — kit.Slider("J" + (index + 1), 1f, 300f, ...)), поэтому дефолтные
        /// номиналы обязаны попадать в этот диапазон: иначе слайдер показывал бы не то значение,
        /// что реально лежит в модели (правка ползунком молча «съедала» бы номинал).
        /// </summary>
        [Test]
        public void Model_DefaultRatings_FitWorkbenchSliderRange()
        {
            var model = new KvPayloadModel();

            for (int i = 0; i < model.rating.Length; i++)
            {
                Assert.That(model.rating[i], Is.GreaterThanOrEqualTo(1f), "нижняя граница слайдера: J" + (i + 1));
                Assert.That(model.rating[i], Is.LessThanOrEqualTo(300f), "верхняя граница слайдера: J" + (i + 1));
            }
        }

        /// <summary>
        /// CopyFrom(null) — безопасный no-op (KvPayloadCalculator.cs:50-52): копирование из
        /// неназначенного источника не должно портить уже настроенную модель.
        /// </summary>
        [Test]
        public void CopyFrom_Null_IsNoOp()
        {
            var model = new KvPayloadModel();
            model.rating[0] = 123f;
            model.linkMass[0] = 4.5f;
            model.safety = 2.25f;
            model.toolMassKg = 1.75f;
            model.ratingPrismaticN = 700f;

            Assert.DoesNotThrow(delegate { model.CopyFrom(null); });

            Assert.That(model.rating[0], Is.EqualTo(123f).Within(1e-4f));
            Assert.That(model.linkMass[0], Is.EqualTo(4.5f).Within(1e-4f));
            Assert.That(model.safety, Is.EqualTo(2.25f).Within(1e-4f));
            Assert.That(model.toolMassKg, Is.EqualTo(1.75f).Within(1e-4f));
            Assert.That(model.ratingPrismaticN, Is.EqualTo(700f).Within(1e-4f));
        }

        /// <summary>
        /// CopyFrom обязан КЛОНИРОВАТЬ массивы (KvPayloadCalculator.cs:53 и 55), а не делить ссылки:
        /// иначе правка номинала ползунком в одном месте меняла бы «эталон» в другом, и расчёт
        /// момента (rating / safety) стал бы зависеть от порядка обращений.
        /// </summary>
        [Test]
        public void CopyFrom_ClonesArrays_SoSourceAndTargetStayIndependent()
        {
            var source = new KvPayloadModel();
            var target = new KvPayloadModel();

            target.CopyFrom(source);

            Assert.That(target.rating, Is.Not.SameAs(source.rating), "массив номиналов склонирован");
            Assert.That(target.linkMass, Is.Not.SameAs(source.linkMass), "массив масс склонирован");

            source.rating[0] = 1000f;
            source.linkMass[0] = 100f;
            Assert.That(target.rating[0], Is.EqualTo(95f).Within(1e-4f), "копия не следует за источником");
            Assert.That(target.linkMass[0], Is.EqualTo(9f).Within(1e-4f), "копия не следует за источником");

            target.rating[1] = 1f;
            Assert.That(source.rating[1], Is.EqualTo(95f).Within(1e-4f), "источник не следует за копией");
        }

        /// <summary>
        /// CopyFrom переносит все скаляры (KvPayloadCalculator.cs:54, 56-57): усилие призмы,
        /// коэффициент запаса и массу захвата — ровно они меняют результат расчёта нагрузки
        /// (rating / safety и вычитание toolMassKg).
        /// </summary>
        [Test]
        public void CopyFrom_CopiesSafetyPrismaticRatingAndToolMass()
        {
            var source = new KvPayloadModel
            {
                safety = 3.5f,
                toolMassKg = 2.25f,
                ratingPrismaticN = 850f,
                rating = new float[] { 10f, 20f, 30f, 40f, 50f, 60f },
                linkMass = new float[] { 1f, 2f, 3f, 4f, 5f, 6f }
            };
            var target = new KvPayloadModel();

            target.CopyFrom(source);

            Assert.That(target.safety, Is.EqualTo(3.5f).Within(1e-4f), "коэффициент запаса");
            Assert.That(target.toolMassKg, Is.EqualTo(2.25f).Within(1e-4f), "масса захвата");
            Assert.That(target.ratingPrismaticN, Is.EqualTo(850f).Within(1e-4f), "усилие призмы");
            for (int i = 0; i < source.rating.Length; i++)
            {
                Assert.That(target.rating[i], Is.EqualTo(source.rating[i]).Within(1e-4f), "номинал J" + (i + 1));
                Assert.That(target.linkMass[i], Is.EqualTo(source.linkMass[i]).Within(1e-4f), "масса " + (i + 1));
            }
        }

        /// <summary>
        /// ФАКТИЧЕСКОЕ поведение при null-массиве в источнике: CopyFrom падает с NullReferenceException
        /// (KvPayloadCalculator.cs:53 и 55 вызывают Clone() без проверки). Тест фиксирует именно
        /// существующее поведение, а не желаемое: он же документирует, что копирование НЕ атомарно —
        /// при null в linkMass поле rating у цели уже заменено. Это точка возможного укрепления кода,
        /// менять который задача запрещает.
        /// </summary>
        [Test]
        public void CopyFrom_SourceWithNullArray_ThrowsNullReference_DocumentedBehaviour()
        {
            var target1 = new KvPayloadModel();
            Assert.Throws<NullReferenceException>(delegate { target1.CopyFrom(new KvPayloadModel { rating = null }); });
            Assert.That(target1.rating[0], Is.EqualTo(95f).Within(1e-4f), "до первого поля дело не дошло — модель цела");

            var source2 = new KvPayloadModel { linkMass = null };
            source2.rating[0] = 777f;
            var target2 = new KvPayloadModel();
            Assert.Throws<NullReferenceException>(delegate { target2.CopyFrom(source2); });
            Assert.That(target2.rating[0], Is.EqualTo(777f).Within(1e-4f), "rating уже перенесён: копирование не атомарно");
            Assert.That(target2.linkMass[0], Is.EqualTo(9f).Within(1e-4f), "linkMass остался прежним");
        }

        // ==========================================================================================
        //  KvPayloadResult — результат расчёта и его строка для интерфейса
        // ==========================================================================================

        /// <summary>
        /// «Пустой» результат: расчёт ещё не выполнялся (KvPayloadCalculator.cs:11-24).
        /// limitingJoint = -1 — маркер «ограничивающий сустав не найден» (он же попадает в Line()
        /// как «ось 0», поэтому UI обязан сначала проверять valid).
        /// </summary>
        [Test]
        public void Result_Defaults_DescribeAnEmptyUnavailableEstimate()
        {
            var result = new KvPayloadResult();

            Assert.IsFalse(result.valid, "по умолчанию результат недействителен");
            Assert.That(result.maxKg, Is.EqualTo(0f).Within(1e-6f), "максимальная нагрузка, кг");
            Assert.That(result.limitingJoint, Is.EqualTo(-1), "ограничивающий сустав не найден");
            Assert.That(result.limitingValue, Is.EqualTo(0f).Within(1e-6f), "момент/сила в суставе");
            Assert.That(result.limitingRating, Is.EqualTo(0f).Within(1e-6f), "номинал сустава");
            Assert.That(result.leverM, Is.EqualTo(0f).Within(1e-6f), "плечо, м");
            Assert.That(result.tcpDistance, Is.EqualTo(0f).Within(1e-6f), "расстояние TCP от базы, м");
            Assert.That(result.why, Is.Empty, "причина отказа ещё не заполнена");
            Assert.That(result.jointLoad, Is.Null, "доли по суставам появляются только после расчёта");
            Assert.That(result.distances, Is.Null, "точки кривой появляются только после расчёта");
            Assert.That(result.payloads, Is.Null);
        }

        /// <summary>
        /// Line() при valid = false возвращает поле why БЕЗ изменений (KvPayloadCalculator.cs:28) —
        /// интерфейс показывает причину отказа ровно так, как её сформулировал расчёт.
        /// Проверяются обе реально достижимые формулировки из кода (134/158 и 245).
        /// </summary>
        [Test]
        public void Result_Line_WhenInvalid_ReturnsWhyVerbatim()
        {
            var result = new KvPayloadResult
            {
                valid = false,
                why = "не удалось оценить нагрузку (нет данных о суставах)"
            };
            Assert.That(result.Line(), Is.EqualTo("не удалось оценить нагрузку (нет данных о суставах)"));

            result.why = RobotNotDefined;
            Assert.That(result.Line(), Is.EqualTo(RobotNotDefined));

            result.why = "";
            Assert.That(result.Line(), Is.Empty, "пустая причина — пустая строка, без исключений");
        }

        /// <summary>
        /// Line() при valid = true собирает одну строку из пяти чисел (KvPayloadCalculator.cs:29-32):
        ///     maxKg("0.00") + " кг · ограничивает ось " + (limitingJoint + 1) +
        ///     " (" + limitingValue("0.0") + " из " + limitingRating("0.0") +
        ///     ") · плечо " + leverM("0.000") + " м · TCP " + tcpDistance("0.000") + " м от базы"
        /// Ожидание собирается теми же форматами — так проверяется шаблон, порядок полей и точность,
        /// но проверка не зависит от разделителя дробной части в текущей культуре.
        /// </summary>
        [Test]
        public void Result_Line_WhenValid_FormatsAllNumbersIntoSingleLine()
        {
            var result = new KvPayloadResult
            {
                valid = true,
                maxKg = 12.34f,
                limitingJoint = 2,
                limitingValue = 95f,
                limitingRating = 95f,
                leverM = 0.25f,
                tcpDistance = 0.5f,
                why = "эта причина не должна попасть в строку"
            };

            string expected = 12.34f.ToString("0.00") + " кг · ограничивает ось " + (2 + 1) + " (" +
                              95f.ToString("0.0") + " из " + 95f.ToString("0.0") + ") · плечо " +
                              0.25f.ToString("0.000") + " м · TCP " + 0.5f.ToString("0.000") + " м от базы";

            Assert.That(result.Line(), Is.EqualTo(expected));
            StringAssert.DoesNotContain("эта причина не должна попасть в строку", result.Line(),
                "при валидном результате поле why в строку не попадает");
        }

        /// <summary>
        /// Нумерация сустава в интерфейсе 1-базовая: zero-based limitingJoint = 0 превращается в «ось 1»,
        /// а 5 — в «ось 6» (KvPayloadCalculator.cs:29 прибавляет единицу). Проверяются оба конца
        /// диапазона — типичное место ошибки на единицу.
        /// </summary>
        [Test]
        public void Result_Line_WhenValid_ConvertsJointIndexToHumanNumber()
        {
            var first = new KvPayloadResult { valid = true, limitingJoint = 0, maxKg = 1f, limitingRating = 1f };
            StringAssert.Contains("ось 1", first.Line(), "нулевой индекс показывается как ось 1");
            StringAssert.DoesNotContain("ось 0", first.Line(), "оси с номером 0 не существует");

            var last = new KvPayloadResult { valid = true, limitingJoint = 5, maxKg = 1f, limitingRating = 1f };
            StringAssert.Contains("ось 6", last.Line(), "индекс 5 показывается как ось 6");
        }

        // ==========================================================================================
        //  KvPayloadCalculator — состояние сервиса до привязки робота
        // ==========================================================================================

        /// <summary>
        /// Новый калькулятор: Last уже существует (вкладка читает его без проверки на null),
        /// но это «пустой» результат с ПУСТОЙ причиной, а Flow() ещё null — робот не привязан
        /// (KvPayloadCalculator.cs:85-97). Пустая причина отличается от отказа «робот не определён»:
        /// так интерфейс отличает «ещё не считали» от «считать не удалось».
        /// </summary>
        [Test]
        public void Calculator_NewInstance_ExposesEmptyResultAndNoBoundRobot()
        {
            var calculator = new KvPayloadCalculator();

            Assert.That(calculator.Last, Is.Not.Null, "Last никогда не null — вкладка читает его напрямую");
            Assert.IsFalse(calculator.Last.valid, "расчёт ещё не выполнялся");
            Assert.That(calculator.Last.limitingJoint, Is.EqualTo(-1), "сустав не найден");
            Assert.That(calculator.Last.why, Is.Empty, "«ещё не считали» — пустая причина");
            Assert.IsNull(calculator.Flow(), "Bind не вызывался — робота нет");
        }

        /// <summary>
        /// Ключи сохранения настроек — часть внешнего контракта (KvPayloadCalculator.cs:80-81):
        /// их переименование молча сбрасывает у оператора коэффициент запаса и массу захвата,
        /// поэтому ключи зафиксированы тестом.
        /// </summary>
        [Test]
        public void Calculator_PrefsKeys_AreStable()
        {
            Assert.That(KvPayloadCalculator.SafetyPrefsKey, Is.EqualTo("KazistovVv.Payload.Safety"));
            Assert.That(KvPayloadCalculator.ToolMassPrefsKey, Is.EqualTo("KazistovVv.Payload.ToolMass"));
        }

        /// <summary>
        /// Число точек кривой по умолчанию — 14 (KvPayloadCalculator.cs:93), а присваивание
        /// идемпотентно: повторное чтение возвращает то же значение, состояние между вызовами не «плывёт».
        /// </summary>
        [Test]
        public void Calculator_CurveSamples_DefaultsToFourteenAndSetterIsIdempotent()
        {
            var calculator = new KvPayloadCalculator();

            Assert.That(calculator.CurveSamples, Is.EqualTo(14), "значение по умолчанию");

            calculator.CurveSamples = 20;
            Assert.That(calculator.CurveSamples, Is.EqualTo(20), "присвоенное значение читается обратно");
            Assert.That(calculator.CurveSamples, Is.EqualTo(20), "повторное чтение ничего не меняет");
        }

        /// <summary>
        /// CurveSamples клампится в 4…40 (KvPayloadCalculator.cs:99). Обе границы включительны.
        /// Клампинг защищает BuildCurve (293): там делитель (samples - 1), то есть при samples = 1
        /// было бы деление на ноль, а при 0 — пустая кривая и NaN в Mathf.Lerp.
        /// </summary>
        [Test]
        public void Calculator_CurveSamples_IsClampedToFourForty()
        {
            var calculator = new KvPayloadCalculator();

            calculator.CurveSamples = int.MinValue;
            Assert.That(calculator.CurveSamples, Is.EqualTo(4), "переполнение снизу");
            calculator.CurveSamples = -1;
            Assert.That(calculator.CurveSamples, Is.EqualTo(4), "отрицательное значение");
            calculator.CurveSamples = 3;
            Assert.That(calculator.CurveSamples, Is.EqualTo(4), "чуть ниже границы");
            calculator.CurveSamples = 4;
            Assert.That(calculator.CurveSamples, Is.EqualTo(4), "нижняя граница включительна");
            calculator.CurveSamples = 5;
            Assert.That(calculator.CurveSamples, Is.EqualTo(5), "чуть выше границы");
            calculator.CurveSamples = 39;
            Assert.That(calculator.CurveSamples, Is.EqualTo(39), "чуть ниже верхней границы");
            calculator.CurveSamples = 40;
            Assert.That(calculator.CurveSamples, Is.EqualTo(40), "верхняя граница включительна");
            calculator.CurveSamples = 41;
            Assert.That(calculator.CurveSamples, Is.EqualTo(40), "чуть выше верхней границы");
            calculator.CurveSamples = int.MaxValue;
            Assert.That(calculator.CurveSamples, Is.EqualTo(40), "переполнение сверху");
        }

        // ==========================================================================================
        //  Evaluate — охраняемые выходы (единственный достижимый без сцены путь расчёта)
        // ==========================================================================================

        /// <summary>
        /// Робот не привязан (flow = null) — Evaluate обязан вернуть недействительный результат
        /// с причиной «робот не определён» (KvPayloadCalculator.cs:156-161), а не падать и не
        /// возвращать NaN. Это ровно тот путь, по которому идёт вкладка до выбора робота.
        /// </summary>
        [Test]
        public void Evaluate_NullPose_ReportsRobotNotDefined()
        {
            var calculator = new KvPayloadCalculator();

            KvPayloadResult result = calculator.Evaluate(null);

            AssertInvalidEstimate(result);
            Assert.That(calculator.Last, Is.SameAs(result), "отказ тоже публикуется в Last");
        }

        /// <summary>
        /// Контракт «Last — последний посчитанный результат» соблюдается и на отказе:
        /// Evaluate возвращает тот же объект, который доступен через Last (KvPayloadCalculator.cs:159),
        /// поэтому вкладка и панель свойств показывают одно и то же.
        /// </summary>
        [Test]
        public void Evaluate_PublishesTheResultIntoLast()
        {
            var calculator = new KvPayloadCalculator();

            KvPayloadResult first = calculator.Evaluate(null);
            Assert.That(calculator.Last, Is.SameAs(first), "после первого вызова");

            KvPayloadResult second = calculator.Evaluate(SixAxisPose());
            Assert.That(calculator.Last, Is.SameAs(second), "после второго вызова");
            Assert.That(second, Is.Not.SameAs(first), "каждый расчёт создаёт новый объект результата");
        }

        /// <summary>
        /// На отказе массивы результата остаются null (KvPayloadCalculator.cs:155-161 их не создаёт):
        /// jointLoad/distances/payloads заполняются только в успешной ветке (169, 306-307).
        /// Вкладка это учитывает — читает jointLoad лишь при valid == true (464-466).
        /// </summary>
        [Test]
        public void Evaluate_InvalidResult_LeavesJointAndCurveArraysNull()
        {
            var calculator = new KvPayloadCalculator();

            KvPayloadResult result = calculator.Evaluate(null);

            Assert.That(result.jointLoad, Is.Null, "доли по суставам не считались");
            Assert.That(result.distances, Is.Null, "кривая не строилась");
            Assert.That(result.payloads, Is.Null, "кривая не строилась");
        }

        /// <summary>
        /// Без привязанного робота отклоняется ЛЮБАЯ поза: null, пустой массив, нули, рабочие углы,
        /// экстремумы 1e300, NaN и бесконечности. Причина одна и та же (156-158) — вход q на этом
        /// уровне не анализируется, потому что проверка flow стоит первой в условии.
        /// </summary>
        [Test]
        public void Evaluate_WithoutBoundRobot_RejectsEveryPose()
        {
            var calculator = new KvPayloadCalculator();

            double[][] poses =
            {
                null,
                new double[0],
                new double[] { 0, 0, 0, 0, 0, 0 },
                new double[] { -180, -90, 45.5, 12, 0, -170 },
                new double[] { 1e300, -1e300, 1e-300, 0, 0, 0 },
                new double[] { double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN },
                new double[] { double.PositiveInfinity, double.NegativeInfinity, 0, 0, 0, 0 },
                new double[] { double.MaxValue, double.MinValue, 0, 0, 0, 0 }
            };

            for (int i = 0; i < poses.Length; i++)
                AssertInvalidEstimate(calculator.Evaluate(poses[i]), "поза №" + i);
        }

        /// <summary>
        /// Нулевые и экстремальные входы не должны давать NaN/Infinity в результате: ни в нагрузке,
        /// ни в расстоянии TCP, ни в моменте, ни в номинале, ни в плече. Без сцены это гарантирует
        /// только охрана входа (156-161) — самих формул момента (M = m*g*r и kg = M_доступно / M_груза)
        /// тест не касается, см. блок «НЕ ПОКРЫТО» в начале файла.
        /// </summary>
        [Test]
        public void Evaluate_ExtremeAndNaNInputs_ProduceNoNaNOrInfinityInResult()
        {
            var calculator = new KvPayloadCalculator();
            double[] garbage = { double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, -1e300, 1e-6 };

            foreach (double[] q in new[] { null, garbage })
            {
                KvPayloadResult result = calculator.Evaluate(q);

                AssertFinite(result.maxKg, "maxKg");
                AssertFinite(result.tcpDistance, "tcpDistance");
                AssertFinite(result.limitingValue, "limitingValue");
                AssertFinite(result.limitingRating, "limitingRating");
                AssertFinite(result.leverM, "leverM");
                Assert.That(result.limitingJoint, Is.EqualTo(-1), "ограничивающий сустав не найден");
            }
        }

        /// <summary>
        /// Детерминизм: повторный расчёт той же позы даёт те же значения, а промежуточный расчёт
        /// другой позы ничего не «портит» (нет накопления состояния между вызовами). Внутри Evaluate
        /// используются только входные аргументы и модель (155-250) — результат не зависит от порядка
        /// вызовов и от реального времени.
        /// </summary>
        [Test]
        public void Evaluate_RepeatedAndInterleavedCalls_AreDeterministic()
        {
            var calculator = new KvPayloadCalculator();
            double[] first = { 10, 20, 30, 0, 0, 0 };
            double[] second = { -45, 90, 0, 5, 5, 5 };

            KvPayloadResult a = calculator.Evaluate(first);
            KvPayloadResult b = calculator.Evaluate(second);
            KvPayloadResult c = calculator.Evaluate(first);

            Assert.That(c, Is.Not.SameAs(a), "каждый вызов возвращает новый объект результата");
            AssertSameOutcome(a, c);
            AssertSameOutcome(a, b);
            Assert.That(calculator.Last, Is.SameAs(c), "Last указывает на последний результат");
        }

        // ==========================================================================================
        //  JointTorques — охраняемые выходы (моменты/усилия по осям)
        // ==========================================================================================

        /// <summary>
        /// Без привязанного робота JointTorques возвращает false и НЕ считает моменты
        /// (KvPayloadCalculator.cs:362-363): out-массивы пустые, сила на инструменте нулевая.
        /// Это тот же путь, по которому идёт визуализация сил до выбора робота.
        /// </summary>
        [Test]
        public void JointTorques_WithoutBoundRobot_ReturnsFalseWithEmptyOutputs()
        {
            var calculator = new KvPayloadCalculator();
            float[] torque;
            float[] load01;
            Vector3 toolForce;

            bool ok = calculator.JointTorques(SixAxisPose(), 5f, out torque, out load01, out toolForce);

            Assert.IsFalse(ok, "робот не привязан — моменты не определены");
            Assert.That(torque.Length, Is.EqualTo(0), "моментов нет");
            Assert.That(load01.Length, Is.EqualTo(0), "запасов нет");
            Assert.That(toolForce.x, Is.EqualTo(0f).Within(1e-6f), "сила на инструменте: X");
            Assert.That(toolForce.y, Is.EqualTo(0f).Within(1e-6f), "сила на инструменте: Y");
            Assert.That(toolForce.z, Is.EqualTo(0f).Within(1e-6f), "сила на инструменте: Z");
        }

        /// <summary>
        /// Out-параметры инициализируются ПЕРВЫМ делом (KvPayloadCalculator.cs:359-361), поэтому
        /// вызывающий всегда получает пригодные для перебора значения, даже если внутри лежал
        /// «мусор» от предыдущего вызова: старые данные затираются, а не остаются висеть.
        /// </summary>
        [Test]
        public void JointTorques_OutputParameters_AreNeverNullOnFailure()
        {
            var calculator = new KvPayloadCalculator();
            float[] torque = { 123f };
            float[] load01 = { 0.5f };
            Vector3 toolForce = Vector3.one;

            Assert.IsFalse(calculator.JointTorques(null, 1f, out torque, out load01, out toolForce));

            Assert.That(torque, Is.Not.Null, "массив моментов всегда пригоден для чтения");
            Assert.That(load01, Is.Not.Null, "массив запасов всегда пригоден для чтения");
            Assert.That(torque.Length, Is.EqualTo(0), "старое содержимое затёрто");
            Assert.That(load01.Length, Is.EqualTo(0), "старое содержимое затёрто");
            Assert.That(toolForce.x, Is.EqualTo(0f).Within(1e-6f), "старая сила затёрта: X");
            Assert.That(toolForce.y, Is.EqualTo(0f).Within(1e-6f), "старая сила затёрта: Y");
            Assert.That(toolForce.z, Is.EqualTo(0f).Within(1e-6f), "старая сила затёрта: Z");
        }

        /// <summary>
        /// Масса груза проверяется ПОСЛЕ проверки робота (KvPayloadCalculator.cs:362-369), поэтому
        /// без робота отклоняется любая масса: отрицательная, нулевая, микроскопическая, огромная,
        /// NaN и бесконечность. Клампы Mathf.Max(0f, payloadKg) и Mathf.Max(0f, toolMassKg) (369)
        /// относятся к недостижимой ветке — отрицательные массы там молча превратились бы в ноль,
        /// а не в исключение.
        /// </summary>
        [Test]
        public void JointTorques_RejectsAnyPayloadAndPose()
        {
            var calculator = new KvPayloadCalculator();
            float[] payloads = { -1000f, -1f, 0f, 1e-6f, 1f, 1e4f, float.MaxValue, float.NaN, float.PositiveInfinity };

            for (int i = 0; i < payloads.Length; i++)
            {
                float[] torque;
                float[] load01;
                Vector3 toolForce;
                bool ok = calculator.JointTorques(SixAxisPose(), payloads[i], out torque, out load01, out toolForce);

                Assert.IsFalse(ok, "payloadKg = " + payloads[i]);
                Assert.That(torque.Length, Is.EqualTo(0), "payloadKg = " + payloads[i]);
                Assert.That(load01.Length, Is.EqualTo(0), "payloadKg = " + payloads[i]);
            }
        }

        // ==========================================================================================
        //  Tick, событие Message и строка метрики StatusLine
        // ==========================================================================================

        /// <summary>
        /// Кадровое обслуживание считает не чаще, чем раз в 0.4 с (KvPayloadCalculator.cs:126-131:
        /// timer -= deltaTime; if (timer больше 0) return; timer = 0.4). Реальное время тест не читает:
        /// интервал отсчитывается только от переданного deltaTime, поэтому проверка детерминирована.
        /// На первом кадре timer равен нулю, то есть окно открыто.
        /// </summary>
        [Test]
        public void Tick_FirstCallEvaluates_ThenHonoursFourTenthsInterval()
        {
            var calculator = new KvPayloadCalculator();
            KvPayloadResult initial = calculator.Last;

            calculator.Tick(0f);
            KvPayloadResult first = calculator.Last;
            Assert.That(first, Is.Not.SameAs(initial), "нулевой deltaTime при открытом окне — пересчёт есть");
            AssertInvalidEstimate(first);

            calculator.Tick(0.25f);
            Assert.That(calculator.Last, Is.SameAs(first), "0.25 с меньше 0.4 с — пересчёта нет");
            calculator.Tick(0.125f);
            Assert.That(calculator.Last, Is.SameAs(first), "суммарно 0.375 с меньше 0.4 с — пересчёта нет");

            calculator.Tick(0.25f);
            KvPayloadResult second = calculator.Last;
            Assert.That(second, Is.Not.SameAs(first), "суммарно 0.625 с больше 0.4 с — пересчёт");
            AssertInvalidEstimate(second);
        }

        /// <summary>
        /// Событие Message при отказах НЕ срабатывает: единственный источник сообщений — приватный
        /// Report (KvPayloadCalculator.cs:422-427), который не вызывается ниоткуда. Фактическое
        /// поведение: причина отказа видна только в поле why, а вкладка показывает прочерк (466-472).
        /// Тест фиксирует это, чтобы «тихий отказ» не превратился незаметно в поток сообщений.
        /// </summary>
        [Test]
        public void Message_Event_IsSilentOnTheGuardedPaths()
        {
            var calculator = new KvPayloadCalculator();
            int raised = 0;
            Action<string> handler = delegate (string text) { raised++; };
            calculator.Message += handler;

            float[] torque;
            float[] load01;
            Vector3 toolForce;
            calculator.Evaluate(null);
            calculator.Tick(0.25f);
            calculator.JointTorques(SixAxisPose(), 3f, out torque, out load01, out toolForce);

            calculator.Message -= handler;

            Assert.That(raised, Is.EqualTo(0), "отказ сообщается через KvPayloadResult.why, а не через Message");
        }

        /// <summary>
        /// Строка метрики без валидного результата — подпись и прочерк (KvPayloadCalculator.cs:411-412).
        /// Подпись берётся через локализацию (KvLocExtra2.T: текущий язык, затем en, затем русский
        /// текст из кода), поэтому ожидание вычисляется тем же публичным вызовом — проверка не зависит
        /// от выбранного языка интерфейса.
        /// </summary>
        [Test]
        public void StatusLine_WithoutValidResult_ShowsDashPlaceholder()
        {
            var calculator = new KvPayloadCalculator();
            string caption = KvLocExtra2.T("payload.now", "Максимальная нагрузка в текущей позе, кг");

            Assert.That(calculator.StatusLine(), Is.EqualTo(caption + ": —"));
        }

        /// <summary>
        /// После неудачного расчёта (робот не привязан) и после кадрового Tick строка метрики
        /// остаётся той же самой: результат не «портится» повторами, а невалидная ветка StatusLine
        /// устойчива (KvPayloadCalculator.cs:409-412).
        /// </summary>
        [Test]
        public void StatusLine_AfterFailedEvaluation_StillShowsDashPlaceholder()
        {
            var calculator = new KvPayloadCalculator();
            calculator.Evaluate(SixAxisPose());
            calculator.Tick(0.25f);
            string caption = KvLocExtra2.T("payload.now", "Максимальная нагрузка в текущей позе, кг");

            Assert.That(calculator.StatusLine(), Is.EqualTo(caption + ": —"));
            Assert.That(calculator.StatusLine(), Is.EqualTo(caption + ": —"), "повторный вызов даёт ту же строку");
        }

        // ==========================================================================================
        //  ФИЗИЧЕСКОЕ ЯДРО — БЕЗ СЦЕНЫ (закрыто 30.10.2026, аудит S43)
        //
        //  Раньше здесь стоял ОДИН тест с `Assert.Ignore` и длинным объяснением, почему формулы
        //  нагрузки «невозможно» проверить: `Evaluate` и `JointTorques` требуют живого
        //  `flow.Validator.Ready`. Это было честно, но означало, что главная физика проекта
        //  (момент от груза, допустимая масса, призматическая ось) НЕ ПРОВЕРЯЛАСЬ НИ РАЗУ.
        //
        //  Формулы вынесены в чистые функции `KvPayloadCalculator` (TorquePerKgOnAxis,
        //  LeverOnAxis, AllowedKg) и проверяются здесь арифметикой, а не сценой.
        // ==========================================================================================

        /// <summary>
        /// МОМЕНТ ОТ ГРУЗА — ЭТО `m·g·плечо`. Проверка на числе, которое можно посчитать в уме:
        /// сустав в начале координат, ось — горизонтальная X, груз в метре ВНИЗ по Z.
        /// Плечо 1 м, вес 9.81 Н на килограмм → момент 9.81 Н·м на кг.
        /// </summary>
        [Test]
        public void TorquePerKg_IsGravityTimesLever()
        {
            // Ось вращения — X. Груз висит в (0, 0, 1): r = (0,0,1), F = (0,-9.81,0).
            // r × F = (0*0 - 1*(-9.81), 1*0 - 0*0, 0*(-9.81) - 0*0) = (9.81, 0, 0); проекция на X = 9.81.
            float torque = KvPayloadCalculator.TorquePerKgOnAxis(
                Vector3.zero, new Vector3(0f, 0f, 1f), Vector3.right);

            Assert.AreEqual(9.81f, torque, 1e-3f, "момент на 1 кг обязан быть 9.81 Н·м (плечо 1 м)");
            Assert.AreEqual(9.81f * 5f,
                KvPayloadCalculator.TorquePerKgOnAxis(Vector3.zero, new Vector3(0f, 0f, 5f), Vector3.right),
                1e-2f, "момент обязан расти линейно с плечом");
        }

        /// <summary>
        /// ГРУЗ НАД ОСЬЮ МОМЕНТА НЕ ДАЁТ. Если TCP стоит ровно над суставом, плечо нулевое и вес
        /// груза эту ось не нагружает — это не «0 из-за ошибки», а физика. Тест отличает одно от
        /// другого: рядом проверяется, что при сдвиге на 1 см момент уже не нулевой.
        /// </summary>
        [Test]
        public void TorquePerKg_IsZeroWhenPayloadIsAboveTheAxis()
        {
            Vector3 pivot = new Vector3(0f, 1f, 0f);
            Assert.AreEqual(0f, KvPayloadCalculator.TorquePerKgOnAxis(
                pivot, new Vector3(0f, 2f, 0f), Vector3.right), 1e-5f,
                "груз ровно над суставом не нагружает горизонтальную ось");

            // ОСЬ ПРАВИЛЬНО ВЫБРАНА? Груз, сдвинутый по X над суставом, горизонтальная ось X НЕ
            // нагружает: r = (0.01, 1, 0), F = (0, −9.81, 0), r × F = (0, 0, −0.0981), проекция на X
            // = 0. Момент появится только у оси Z. Проверяем именно её — иначе тест проверял бы
            // не физику, а мою ошибку в выборе оси (прогон 30.10.2026 поймал ровно это).
            Assert.AreEqual(0f, KvPayloadCalculator.TorquePerKgOnAxis(
                pivot, new Vector3(0.01f, 2f, 0f), Vector3.right), 1e-5f,
                "сдвиг по X не нагружает ось X — плечо в плоскости YZ нулевое");

            Assert.AreEqual(9.81f * 0.01f, KvPayloadCalculator.TorquePerKgOnAxis(
                pivot, new Vector3(0.01f, 2f, 0f), Vector3.forward), 1e-4f,
                "сдвиг на 1 см по X относительно оси Z даёт 9.81·0.01 = 0.0981 Н·м");
        }

        /// <summary>
        /// СИЛА ВДОЛЬ ОСИ МОМЕНТА НЕ ДАЁТ — та же ловушка, что «груз над осью», но для
        /// вертикальной оси: груз, висящий над вертикальным суставом, не создаёт момента, сколько
        /// бы он ни весил. Если это сломается, расчёт начнёт занижать допустимую нагрузку.
        /// </summary>
        [Test]
        public void TorquePerKg_IsZeroForVerticalAxis()
        {
            Assert.AreEqual(0f, KvPayloadCalculator.TorquePerKgOnAxis(
                Vector3.zero, new Vector3(0.5f, -1f, 0.3f), Vector3.up), 1e-5f,
                "вес направлен вдоль вертикальной оси — момента нет");
        }

        /// <summary>
        /// ВЫРОЖДЕННАЯ ОСЬ — НЕ ИСКЛЮЧЕНИЕ. Нулевая ось означает «данных о суставе нет»;
        /// функция обязана вернуть 0, а не бросать или выдавать NaN, иначе один битый сустав
        /// обнулит весь расчёт нагрузки.
        /// </summary>
        [Test]
        public void TorquePerKg_HandlesDegenerateAxisAndLever()
        {
            Assert.AreEqual(0f, KvPayloadCalculator.TorquePerKgOnAxis(
                Vector3.zero, Vector3.one, Vector3.zero), 1e-6f, "нулевая ось — 0, без исключения");
            Assert.AreEqual(0f, KvPayloadCalculator.LeverOnAxis(
                Vector3.zero, Vector3.one, Vector3.zero), 1e-6f, "нулевая ось — плечо 0");
            Assert.IsFalse(float.IsNaN(KvPayloadCalculator.TorquePerKgOnAxis(
                Vector3.zero, Vector3.one, Vector3.zero)), "NaN недопустим");
        }

        /// <summary>
        /// ПЛЕЧО — РАССТОЯНИЕ ДО ОСИ, а не длина `r`. Груз, висящий вдоль оси (высоко над ней),
        /// имеет большое `r`, но нулевое плечо. Тест фиксирует именно это различие: иначе
        /// «плечо» в подсказке оператору врало бы при вертикальной руке.
        /// </summary>
        [Test]
        public void Lever_IsDistanceToAxis_NotLengthOfRadius()
        {
            Vector3 pivot = Vector3.zero;
            Vector3 axis = Vector3.up;
            Vector3 high = new Vector3(0f, 3f, 0f);       // строго на оси, r = 3 м
            Assert.AreEqual(0f, KvPayloadCalculator.LeverOnAxis(pivot, high, axis), 1e-5f,
                "груз на самой оси имеет нулевое плечо, хотя r = 3 м");

            Vector3 sideways = new Vector3(2f, 3f, 0f);   // 2 м от оси
            Assert.AreEqual(2f, KvPayloadCalculator.LeverOnAxis(pivot, sideways, axis), 1e-5f,
                "плечо — проекция на плоскость, перпендикулярную оси");
        }

        /// <summary>
        /// ДОПУСТИМЫЙ ГРУЗ = (номинал / запас − момент звеньев) / момент на килограмм.
        /// Числа взяты такие, чтобы ответ считался в уме: (100 / 2 − 20) / 9.81 = 3.058 кг.
        /// </summary>
        [Test]
        public void AllowedKg_FollowsRatedTorqueOverSafety()
        {
            float kg = KvPayloadCalculator.AllowedKg(100f, 2f, 20f, 9.81f);
            Assert.AreEqual((100f / 2f - 20f) / 9.81f, kg, 1e-3f,
                "допустимая масса = (номинал/запас − момент звеньев) / момент на кг");
        }

        /// <summary>
        /// ЗАПАС НЕ МОЖЕТ БЫТЬ МЕНЬШЕ ЕДИНИЦЫ: `safety = 0.5` — это ошибка настройки, и она не
        /// должна РАЗРЕШАТЬ вдвое больше, чем номинал сустава. Проверяется, что при запасе ниже
        /// единицы результат равен результату при единице.
        /// </summary>
        [Test]
        public void AllowedKg_ClampsSafetyToOne()
        {
            float atHalf = KvPayloadCalculator.AllowedKg(100f, 0.5f, 0f, 10f);
            float atOne = KvPayloadCalculator.AllowedKg(100f, 1f, 0f, 10f);
            Assert.AreEqual(atOne, atHalf, 1e-4f,
                "запас меньше единицы обязан читаться как единица — иначе номинал сустава превышается вдвое");
        }

        /// <summary>
        /// НЕТ ЗАПАСА — НОЛЬ, А НЕ ОТРИЦАТЕЛЬНАЯ МАССА. Если вес звеньев уже съел весь доступный
        /// момент, допустимый груз — 0 кг. Отрицательное число в интерфейсе читалось бы как
        /// «можно антивещество».
        /// </summary>
        [Test]
        public void AllowedKg_IsZeroWhenLinksConsumeAllTorque()
        {
            Assert.AreEqual(0f, KvPayloadCalculator.AllowedKg(100f, 2f, 60f, 9.81f), 1e-4f,
                "доступный момент 50 Н·м, момент звеньев 60 — грузить нечего");
            Assert.AreEqual(0f, KvPayloadCalculator.AllowedKg(0f, 2f, 0f, 9.81f), 1e-4f,
                "сустав без номинала не держит ничего");
        }

        /// <summary>
        /// ОСЬ, НА КОТОРУЮ ГРУЗ НЕ ДЕЙСТВУЕТ, НЕ ОГРАНИЧИВАЕТ НИЧЕГО. Это важно: `Evaluate` берёт
        /// минимум по всем осям, и если такая ось вернёт 0 вместо «очень много», весь результат
        /// станет нулевым.
        /// </summary>
        [Test]
        public void AllowedKg_UnlimitedWhenPayloadCannotLoadTheAxis()
        {
            Assert.AreEqual(float.MaxValue,
                KvPayloadCalculator.AllowedKg(100f, 2f, 0f, 0f), 1f,
                "нулевой момент на килограмм означает «ограничения по этой оси нет»");
        }
    }
}
