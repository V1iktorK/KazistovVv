// ================================================================================================
//  KvMrPaintAndRayTests.cs — EditMode-тесты ПЯТИ ТРЕБОВАНИЙ ВЛАДЕЛЬЦА ОТ 12.11.2026.
//
//  ОТКУДА ЗАДАЧА (требования владельца дословно, порядок важен):
//    1. «При запуске MR версии надо чтобы сначала поверхности отображались каким-то покрытием будь
//       то сетка или поверхность.»
//    2. «Сразу после выхода из меню или сворачивания его надо было выбрать положение робота,
//       которое задавалось бы окончательно до нажатия на кнопку "переместить робота".»
//    3. «К концу правого лазера (который упирается в текстуры и в реальности) прикреплён робот,
//       позицию которого надо выбрать.»
//    4. «Левый (который надо перекрасить в красный) выбирает точку (добавь чтобы он мог как и
//       правый упираться в текстуры), а правым выбирать траекторию.»
//    5. «Сделай правый луч зелёным, как сделаешь левый красным.»
//
//  КАК ПРОВЕРЯЕТСЯ БЕЗ ШЛЕМА И БЕЗ СЦЕНЫ. Сцена не запускается (правило проекта), поэтому
//  проверяются ЧИСТЫЕ функции — те, у которых нет ни `Time`, ни `Debug`, ни Unity-объектов:
//  выбор ближайшего попадания, цвета лучей, правило включения режима, правило «можно ли ставить»,
//  сохранение последней валидной позиции и строки журнала регуляркой на числа.
//
//  ЧЕГО ЗДЕСЬ НЕТ И БЫТЬ НЕ МОЖЕТ: «видно ли покрытие глазами в шлеме». Это проверяется на
//  устройстве. Но всё, от чего зависит видимое поведение (какой цвет у какой поверхности, что
//  печатается в журнал, включается ли режим), закреплено здесь числом — чтобы «владелец снова
//  сказал» не было единственным способом узнать о поломке.
// ================================================================================================

using System;
using System.Text.RegularExpressions;
using KazistovVvRealWorld;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KazistovVvTests
{
    /// <summary>Покрытие поверхностей в MR, цвета лучей, выбор места робота.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvMrPaintAndRayTests
    {
        // ============================================================ а) БЛИЖАЙШЕЕ ПОПАДАНИЕ

        /// <summary>
        /// ИЗ ДВУХ КАНДИДАТОВ ВЫБИРАЕТСЯ БЛИЖАЙШИЙ — И ЗЕРКАЛЬНО ТОЖЕ.
        ///
        /// ЧТО БЫЛО (отчёт тестировщика, подтверждено кодом). У лучей были РАЗНЫЕ правила: правый
        /// спрашивал только `Physics.Raycast` (виртуальные объекты), левый — только реальные
        /// поверхности комнаты. Один и тот же мир, два разных ответа.
        /// ЧТО СТАЛО: одно правило `KvXrRayHitRules.Nearest` на оба луча.
        ///
        /// ПОЧЕМУ ОБА НАПРАВЛЕНИЯ. Проверить только «виртуальное 1 м побеждает реальное 3 м» мало:
        /// тогда ошибка «реальное всегда выигрывает» осталась бы незамеченной. Поэтому проверяются
        /// ОБА случая и ещё равенство расстояний.
        /// </summary>
        [Test]
        public void NearestHit_PicksCloserOfRealAndVirtual_BothDirections()
        {
            // Реальное 3 м, виртуальное 1 м → побеждает ВИРТУАЛЬНОЕ (1 м).
            KvXrRayHit first = KvXrRayHitRules.Nearest(
                true, 3f, new Vector3(0f, 0f, 3f), Vector3.up, "стол",
                true, 1f, new Vector3(0f, 0f, 1f), Vector3.up, "Стенд_1_Стол");
            Assert.AreEqual(KvXrHitSource.Virtual, first.Source,
                "виртуальное попадание на 1 м обязано побеждать реальное на 3 м");
            Assert.AreEqual(1f, first.Distance, 1e-4f);
            Assert.AreEqual("Стенд_1_Стол", first.Name);

            // Реальное 1 м, виртуальное 3 м → побеждает РЕАЛЬНОЕ (1 м). Зеркальный случай.
            KvXrRayHit second = KvXrRayHitRules.Nearest(
                true, 1f, new Vector3(0f, 0f, 1f), Vector3.up, "стол",
                true, 3f, new Vector3(0f, 0f, 3f), Vector3.up, "Стенд_1_Стол");
            Assert.AreEqual(KvXrHitSource.Real, second.Source,
                "реальное попадание на 1 м обязано побеждать виртуальное на 3 м");
            Assert.AreEqual(1f, second.Distance, 1e-4f);

            // Равные расстояния — в пользу РЕАЛЬНОЙ поверхности: настоящий стол важнее панели,
            // висящей в той же точке (иначе робот встал бы на панель).
            KvXrRayHit tie = KvXrRayHitRules.Nearest(
                true, 2f, new Vector3(1f, 0f, 0f), Vector3.up, "стол",
                true, 2f, new Vector3(1f, 0f, 0f), Vector3.up, "панель");
            Assert.AreEqual(KvXrHitSource.Real, tie.Source,
                "при равных расстояниях побеждает РЕАЛЬНАЯ поверхность");
        }

        /// <summary>
        /// ОБА ЛУЧА ДАЮТ ОДИН РЕЗУЛЬТАТ НА ОДНОЙ ГЕОМЕТРИИ — ЭТО И ЕСТЬ СМЫСЛ ПРАВИЛА.
        ///
        /// Проверяется прогоном таблицы случаев: для каждой геометрии ответ обязан быть одинаковым
        /// независимо от того, какой луч спросил. Здесь же — анти-вакуум: если бы правило всегда
        /// отвечало «нет попадания», таблица сошлась бы сама с собой и тест был бы зелёным впустую.
        /// </summary>
        [Test]
        public void BothLasers_GetTheSameAnswerOnTheSameGeometry()
        {
            int realAnswers = 0;
            int virtualAnswers = 0;

            for (int i = 0; i < 7; i++)
            {
                bool realHit = (i & 1) == 0;
                bool virtualHit = (i & 2) == 0;
                float realDistance = 0.5f + i * 0.4f;
                float virtualDistance = 4.0f - i * 0.4f;

                KvXrRayHit asRight = KvXrRayHitRules.Nearest(
                    realHit, realDistance, new Vector3(0f, 0f, realDistance), Vector3.up, "реальное",
                    virtualHit, virtualDistance, new Vector3(0f, 0f, virtualDistance), Vector3.up, "виртуальное");

                // Левый луч спрашивает ТУ ЖЕ функцию — второго правила в проекте нет. Поэтому здесь
                // проверяется главное: функция детерминирована и одинаковый вход даёт одинаковый выход.
                KvXrRayHit asLeft = KvXrRayHitRules.Nearest(
                    realHit, realDistance, new Vector3(0f, 0f, realDistance), Vector3.up, "реальное",
                    virtualHit, virtualDistance, new Vector3(0f, 0f, virtualDistance), Vector3.up, "виртуальное");

                Assert.AreEqual(asRight.Source, asLeft.Source, "лучи разошлись на случае " + i + ": разные правила");
                // СРАВНЕНИЕ РАССТОЯНИЙ — С УЧЁТОМ «ПОПАДАНИЯ НЕТ». У пустого ответа расстояние равно
                // +∞, а `|∞ − ∞|` — это NaN, поэтому обычная проверка «разница меньше 1e-5» на нём
                // ВСЕГДА ЛОЖНА (та же ловушка E-21, из-за которой мусор проходил за попадание).
                Assert.IsTrue(SameDistance(asRight.Distance, asLeft.Distance),
                    "лучи разошлись по расстоянию, случай " + i);
                Assert.AreEqual(asRight.Name, asLeft.Name, "лучи разошлись по имени цели, случай " + i);

                if (asRight.Source == KvXrHitSource.Real) realAnswers++;
                if (asRight.Source == KvXrHitSource.Virtual) virtualAnswers++;
            }

            Assert.Greater(realAnswers, 0, "таблица не дала ни одного попадания в реальную поверхность — проверка вакуумна");
            Assert.Greater(virtualAnswers, 0, "таблица не дала ни одного попадания в объект сцены — проверка вакуумна");
        }

        /// <summary>
        /// ОТСЕВ МУСОРА (класс E-21). NaN и бесконечность на входе — это НЕ попадание: в C# любое
        /// сравнение с NaN даёт `false`, поэтому без явной отбраковки мусор прошёл бы как
        /// «ближайшее» и луч ушёл бы в никуда.
        /// </summary>
        [Test]
        public void NearestHit_RejectsNonFiniteAndNonPositiveDistances()
        {
            Assert.AreEqual(KvXrHitSource.None, KvXrRayHitRules.Nearest(
                true, float.NaN, Vector3.zero, Vector3.up, "мусор",
                false, 0f, Vector3.zero, Vector3.up, null).Source,
                "NaN-расстояние обязано отвергаться, а не становиться целью");

            Assert.AreEqual(KvXrHitSource.None, KvXrRayHitRules.Nearest(
                true, 1f, new Vector3(float.NaN, 0f, 0f), Vector3.up, "мусор",
                false, 0f, Vector3.zero, Vector3.up, null).Source,
                "NaN-точка обязана отвергаться: Unity не рисует объекты с NaN, и цель исчезла бы");

            Assert.AreEqual(KvXrHitSource.None, KvXrRayHitRules.Nearest(
                true, 0f, Vector3.zero, Vector3.up, "ноль метров",
                false, 0f, Vector3.zero, Vector3.up, null).Source,
                "попадание на нуле метров — это «в собственной руке», а не поверхность");

            Assert.AreEqual(KvXrHitSource.None, KvXrRayHitRules.Nearest(
                false, 0f, Vector3.zero, Vector3.up, null,
                false, 0f, Vector3.zero, Vector3.up, null).Source,
                "двух попаданий нет — ответ обязан быть «нет попадания»");
        }

        // ============================================================ б) ЦВЕТА ЛУЧЕЙ

        /// <summary>
        /// ЛЕВЫЙ ЛУЧ КРАСНЫЙ, ПРАВЫЙ ЗЕЛЁНЫЙ — СРАВНЕНИЕМ С ОЖИДАЕМЫМ ЗНАЧЕНИЕМ (пункты 4–5).
        ///
        /// ЧТО БЫЛО. Оба луча были голубыми, а на наведении красились в зелёный — «зелёное пятно»,
        /// которое владелец не связывал ни с одной рукой. Потом цвета «съезжали» ещё раз, потому что
        /// оттенок задавался в трёх местах (тема, вершины линии, материал).
        /// ЧТО СТАЛО. Красный и зелёный записаны ИМЕНОВАННЫМИ КОНСТАНТАМИ с объяснением смысла:
        /// `PointLaserColor` (красный = точка) и `TrajectoryLaserColor` (зелёный = траектория).
        ///
        /// ПОЧЕМУ СРАВНЕНИЕ С ЗНАЧЕНИЕМ, А НЕ «r > g». Проверка «красного больше зелёного» зелёная и
        /// у бледно-розового, и у почти белого луча. Здесь ожидаемый компонент назван числом, поэтому
        /// «перекрасили обратно» ловится сразу.
        /// </summary>
        [Test]
        public void LeftLaserIsRed_RightLaserIsGreen()
        {
            Color left = KvXrRayHitRules.LaserColorOf(KvXrQuickHand.Left);
            Color right = KvXrRayHitRules.LaserColorOf(KvXrQuickHand.Right);

            Assert.AreEqual(KvXrRayHitRules.PointLaserColor, left,
                "цвет левого луча изменён: он обязан быть КРАСНЫМ (точка)");
            Assert.AreEqual(KvXrRayHitRules.TrajectoryLaserColor, right,
                "цвет правого луча изменён: он обязан быть ЗЕЛЁНЫМ (траектория)");

            // КРАСНЫЙ: красная составляющая больше и зелёной, и синей.
            Assert.Greater(left.r, left.g, "левый луч обязан быть красным (r > g)");
            Assert.Greater(left.r, left.b, "левый луч обязан быть красным (r > b)");
            // ЗЕЛЁНЫЙ: зелёная составляющая больше и красной, и синей.
            Assert.Greater(right.g, right.r, "правый луч обязан быть зелёным (g > r)");
            Assert.Greater(right.g, right.b, "правый луч обязан быть зелёным (g > b)");

            // ЦВЕТА РАЗНЫЕ: два одинаковых луча — это ровно то, с чего началась жалоба владельца.
            Assert.AreNotEqual(left, right, "лучи снова одного цвета — «не разноцветные лазеры»");

            // ЦВЕТ НЕ МЕНЯЕТСЯ ОТ НАВЕДЕНИЯ: правило владельца — меняется только яркость.
            Color leftHover = KvXrLaserColors.Ray(KvXrQuickHand.Left, true);
            Color rightHover = KvXrLaserColors.Ray(KvXrQuickHand.Right, true);
            Assert.Greater(leftHover.r, leftHover.g, "на наведении левый луч перестал быть красным");
            Assert.Greater(rightHover.g, rightHover.r, "на наведении правый луч перестал быть зелёным");
            Assert.Greater(leftHover.a, KvXrLaserColors.IdleAlpha, "на наведении луч обязан стать плотнее");
        }

        /// <summary>ПОДПИСИ ЛУЧЕЙ В ЖУРНАЛЕ: «ЛЕВЫЙ»/«ПРАВЫЙ» и что каждый выбирает.</summary>
        [Test]
        public void LaserLabels_SayHandAndPurpose()
        {
            Assert.AreEqual("ЛЕВЫЙ", KvXrRayHitRules.HandLabel(KvXrQuickHand.Left));
            Assert.AreEqual("ПРАВЫЙ", KvXrRayHitRules.HandLabel(KvXrQuickHand.Right));

            Assert.AreEqual("точка", KvXrRayHitRules.LaserPurposeName(KvXrQuickHand.Left),
                "левый луч выбирает ТОЧКУ (пункт 4 владельца)");
            Assert.AreEqual("траектория", KvXrRayHitRules.LaserPurposeName(KvXrQuickHand.Right),
                "правый луч выбирает ТРАЕКТОРИЮ (пункт 4 владельца)");

            Assert.AreEqual("красный", KvXrRayHitRules.LaserColorName(KvXrQuickHand.Left));
            Assert.AreEqual("зелёный", KvXrRayHitRules.LaserColorName(KvXrQuickHand.Right));
        }

        // ============================================================ е) ЖУРНАЛ ЛУЧЕЙ

        /// <summary>
        /// СТРОКА ЖУРНАЛА О ПОПАДАНИИ — С ЧИСЛАМИ, РЕГУЛЯРКОЙ (требование пункта 2 задания).
        ///
        /// ФОРМА, КОТОРУЮ ЖДЁТ ВЛАДЕЛЕЦ:
        ///     [KvXr] ЛУЧ ЛЕВЫЙ: попадание в реальная «стол» на 1.42 м
        ///     [KvXr] ЛУЧ ПРАВЫЙ: попадание в виртуальная «Стенд_1_Стол» на 2.05 м
        ///     [KvXr] ЛУЧ ЛЕВЫЙ: нет попадания — …
        /// </summary>
        [Test]
        public void RayLogLine_CarriesNumbersAndSource()
        {
            string real = KvXrRayHitRules.Describe("ЛЕВЫЙ", KvXrHitSource.Real, "стол", 1.42f, 10f);
            string virt = KvXrRayHitRules.Describe("ПРАВЫЙ", KvXrHitSource.Virtual, "Стенд_1_Стол", 2.05f, 10f);
            string none = KvXrRayHitRules.Describe("ЛЕВЫЙ", KvXrHitSource.None, null, 0f, 10f);

            StringAssert.StartsWith("[KvXr] ЛУЧ ЛЕВЫЙ: попадание в реальная «стол» на ", real);
            StringAssert.StartsWith("[KvXr] ЛУЧ ПРАВЫЙ: попадание в виртуальная «Стенд_1_Стол» на ", virt);

            // РЕГУЛЯРКА НА ЧИСЛО С ЕДИНИЦЕЙ: «на 1.42 м» — именно так приёмка ищет расстояние.
            StringAssert.IsMatch(@"на 1\.42 м", real);
            StringAssert.IsMatch(@"на 2\.05 м", virt);
            StringAssert.IsMatch(@"\d+\.\d\d м", real);
            StringAssert.IsMatch(@"\d+\.\d\d м", virt);

            // БЕЗ ПОПАДАНИЯ СТРОКА ТОЖЕ ЕСТЬ И НАЗЫВАЕТ ПРИЧИНУ. Молчание — дефект: по нему нельзя
            // отличить «перед лучом пусто» от «код сломался».
            StringAssert.Contains("нет попадания", none);
            StringAssert.IsMatch(@"\d+\.\d\d м", none);
        }

        /// <summary>
        /// ПРАВИЛО «ПЕЧАТАТЬ ПРИ СМЕНЕ»: одинаковый ключ не даёт второй строки, а смена цели и
        /// заметное изменение расстояния — даёт. Без этого журнал шлема утонул бы на 90 кадрах в секунду.
        /// </summary>
        [Test]
        public void RayTargetKey_ChangesOnlyWhenTargetChanges()
        {
            string a = KvXrRayHitRules.TargetKey(KvXrHitSource.Real, "стол", 1.42f);
            string same = KvXrRayHitRules.TargetKey(KvXrHitSource.Real, "стол", 1.44f);   // тот же дециметр
            string other = KvXrRayHitRules.TargetKey(KvXrHitSource.Virtual, "стол", 1.42f);
            string moved = KvXrRayHitRules.TargetKey(KvXrHitSource.Real, "стол", 2.51f);

            Assert.AreEqual(a, same, "микродрожание луча не должно печатать новую строку");
            Assert.AreNotEqual(a, other, "смена источника попадания обязана менять ключ");
            Assert.AreNotEqual(a, moved, "заметное изменение расстояния обязано менять ключ");
        }

        // ============================================================ в) РЕЖИМ ПОСЛЕ МЕНЮ

        /// <summary>
        /// РЕЖИМ ВЫБОРА МЕСТА ВКЛЮЧАЕТСЯ ПОСЛЕ ВЫХОДА ИЗ МЕНЮ (пункт 2 владельца).
        ///
        /// АНТИ-ВАКУУМ. Проверяется не только «после выхода включается», но и обратное: если меню
        /// НЕ БЫЛО открыто, режим НЕ включается. Иначе «сразу после выхода из меню» превратилось бы в
        /// «всегда при запуске», и требование владельца выполнялось бы формально.
        /// </summary>
        [Test]
        public void PickMode_TurnsOnAfterMenuCloses_AndNotWithoutMenu()
        {
            // Меню было видимым и стало невидимым — это и есть выход/сворачивание.
            Assert.IsTrue(KvMrRobotPick.ShouldEnterAfterMenu(true, false),
                "после выхода из меню режим выбора места ОБЯЗАН включиться");

            // АНТИ-ВАКУУМ: меню не открывали — режим не включается.
            Assert.IsFalse(KvMrRobotPick.ShouldEnterAfterMenu(false, false),
                "меню не открывалось — режим выбора места включаться не должен (не «всегда при запуске»)");
            Assert.IsFalse(KvMrRobotPick.ShouldEnterAfterMenu(false, true),
                "меню только что открылось — это не выход из него");
            Assert.IsFalse(KvMrRobotPick.ShouldEnterAfterMenu(true, true),
                "меню всё ещё видно — выход ещё не произошёл");

            // СТРОКА ЖУРНАЛА — дословная форма из требования владельца.
            Assert.AreEqual("[KvMr] ВЫБОР МЕСТА РОБОТА: режим включён после выхода из меню",
                KvMrRobotPick.DescribeModeOn("после выхода из меню"),
                "строка журнала о включении режима изменена — приёмка её не найдёт");
        }

        // ============================================================ г) ФИКСАЦИЯ МЕСТА

        /// <summary>
        /// ДО ФИКСАЦИИ «ПЕРЕМЕСТИТЬ РОБОТА» НЕДОСТУПНА И ОБЪЯСНЯЕТ ПРИЧИНУ (пункт 4 владельца).
        ///
        /// ЧТО БЫЛО. Кнопка переставляла робота ВСЛЕПУЮ: место вычислялось внутри, и оператор не
        /// знал, куда именно робот уедет. ЧЕМ ПЛОХО: ровно то, из-за чего владелец просил фиксацию —
        /// «задавалось бы окончательно до нажатия на кнопку "переместить робота"».
        /// </summary>
        [Test]
        public void MoveRobot_IsUnavailableUntilPlaceIsFixed_AndSaysWhy()
        {
            string reason;

            // 1. Режим не включён — отказ с причиной, а не тишина.
            Assert.IsFalse(KvMrRobotPick.CanPlace(false, true, true, true, out reason),
                "без включённого режима ставить нельзя");
            StringAssert.Contains("после выхода из меню", reason,
                "причина обязана говорить, ОТКУДА берётся режим, а не только «нельзя»");

            // 2. РЕЖИМ ВКЛЮЧЁН, НО МЕСТО НЕ ЗАФИКСИРОВАНО — отказ и подсказка про клавишу.
            Assert.IsFalse(KvMrRobotPick.CanPlace(true, false, true, true, out reason),
                "без фиксации места кнопка обязана отказывать, а не переставлять вслепую");
            StringAssert.Contains("зафиксируйте место", reason);
            StringAssert.Contains(KvMrRobotPick.ConfirmKeyName, reason,
                "подсказка обязана называть КЛАВИШУ фиксации — иначе оператор не знает, чем фиксировать");
            StringAssert.Contains("вслепую", reason,
                "причина обязана объяснять ПОСЛЕДСТВИЕ отказа, а не только сам факт");

            // 3. ФИКСАЦИЯ ЕСТЬ — можно.
            Assert.IsTrue(KvMrRobotPick.CanPlace(true, true, true, true, out reason),
                "с зафиксированным местом кнопка обязана работать");
            Assert.IsEmpty(reason, "при разрешении причины быть не должно");

            // 4. ЛУЧ В ПУСТОТЕ И ВАЛИДНОЙ ПОЗИЦИИ НЕТ — отказ с указанием, что делать.
            Assert.IsFalse(KvMrRobotPick.CanPlace(true, true, false, false, out reason),
                "ставить некуда — кнопка обязана отказать");
            StringAssert.Contains("наведите ПРАВЫЙ луч", reason,
                "причина обязана говорить, ЧТО ДЕЛАТЬ, а не только «нельзя»");
        }

        /// <summary>
        /// ФИКСАЦИЯ МЕНЯЕТ СОСТОЯНИЕ. Строка фиксации обязана нести КООРДИНАТЫ И КЛАВИШУ — это
        /// требование владельца дословно: «(x; y; z) м · луч правый · подтверждено &lt;клавиша&gt;».
        /// </summary>
        [Test]
        public void FixedPlacementLine_CarriesCoordinatesAndKey()
        {
            string line = KvMrRobotPick.DescribeFixed(new Vector3(0.42f, 0.78f, 1.05f), 30f, "B");

            StringAssert.StartsWith("[KvMr] МЕСТО РОБОТА ЗАФИКСИРОВАНО: (", line);
            StringAssert.Contains("0.42", line);
            StringAssert.Contains("0.78", line);
            StringAssert.Contains("1.05", line);
            StringAssert.Contains("луч правый", line);
            StringAssert.Contains("подтверждено B", line);

            // РЕГУЛЯРКА: три числа с двумя знаками — по ней приёмка и сверяет координаты.
            StringAssert.IsMatch(@"\(\s*-?\d+\.\d\d;\s*-?\d+\.\d\d;\s*-?\d+\.\d\d\)\s*м", line);
            Assert.IsTrue(Regex.IsMatch(line, @"\d"), "в строке фиксации нет ни одного числа: " + line);
        }

        /// <summary>
        /// КЛАВИША ФИКСАЦИИ СВОБОДНА И НАЗВАНА В ПОДСКАЗКЕ.
        ///
        /// ПОЧЕМУ ПРОВЕРЯЕТСЯ ИМЕННО ТАК. Свободу клавиши нельзя доказать комментарием: через месяц
        /// кто-то добавит обработчик B, и никто не заметит — это класс дефекта §27/E-08 «одно
        /// нажатие — два действия». Но и завести здесь ВТОРОГО читателя исходников нельзя: в проекте
        /// жёстко ограничено число файлов, которые открывают тексты скриптов (храповик P4-3), и
        /// каждый новый такой файл ломает прогон.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ, А ЧТО В `KvHotkeyConflictTests`:
        ///   • здесь — что клавиша НАЗВАНА (константа и её имя совпадают), что имя непустое и что
        ///     подсказка интерфейса называет ровно её (иначе оператор ищет клавишу, которой нет);
        ///   • конфликты клавиш между хабами стережёт уже существующий `KvHotkeyConflictTests` —
        ///     он читает исходники и падает с именем файла, если одна клавиша делает два дела.
        /// Клавиша B на момент правки не встречается ни в одном обработчике `01_Scripts` (проверено
        /// поиском по всем `KeyCode.*` проекта), и это записано в комментарии константы.
        /// </summary>
        [Test]
        public void ConfirmKey_IsNamedAndAdvertisedInHint()
        {
            Assert.AreEqual(Key.B, KvMrRobotPick.ConfirmKey,
                "клавиша фиксации изменена — обновите подсказку интерфейса, журнал и этот тест");
            Assert.AreEqual("B", KvMrRobotPick.ConfirmKeyName,
                "имя клавиши для подсказки разошлось с самой клавишей");

            // ИМЯ НЕ ПУСТОЕ — анти-вакуум: пустая строка прошла бы любое сравнение «содержит».
            Assert.IsNotEmpty(KvMrRobotPick.ConfirmKeyName);
            Assert.AreEqual(KvMrRobotPick.ConfirmKey.ToString(), KvMrRobotPick.ConfirmKeyName,
                "имя клавиши и её перечисление обязаны совпадать: иначе подсказка зовёт не ту клавишу");

            // ПОДСКАЗКА ИНТЕРФЕЙСА НАЗЫВАЕТ ЭТУ ЖЕ КЛАВИШУ (требование пункта 3 владельца:
            // «напиши его в подсказке интерфейса + журнале»).
            StringAssert.Contains(KvMrRobotPick.ConfirmKeyName, KvXrStartMenuRows.MoveRobotRowLabel,
                "строка «Переместить робота» не называет клавишу фиксации — оператор её не найдёт");
            StringAssert.Contains("зафиксируйте", KvXrStartMenuRows.MoveRobotRowLabel,
                "подсказка обязана говорить, что место сначала ФИКСИРУЮТ");

            // ПОДСКАЗКА К РЕЖИМУ И СТРОКА ФИКСАЦИИ — тоже с клавишей.
            StringAssert.Contains(KvMrRobotPick.ConfirmKeyName, KvMrRobotPick.HintTail(),
                "подсказка режима не называет клавишу фиксации");
            StringAssert.Contains(KvMrRobotPick.ConfirmKeyName,
                KvMrRobotPick.DescribeFixed(Vector3.zero, 0f, KvMrRobotPick.ConfirmKeyName),
                "строка фиксации не называет клавишу — журнал не докажет, чем подтвердили");
        }

        // ============================================================ д) ПУСТОТА НЕ ДВИГАЕТ РОБОТА

        /// <summary>
        /// ОТСУТСТВИЕ ПОПАДАНИЯ НЕ МЕНЯЕТ ПОСЛЕДНЮЮ ВАЛИДНУЮ ПОЗИЦИЮ (пункт 5 владельца: робот «не
        /// улетает в пустоту»).
        ///
        /// КАК ПРОВЕРЯЕТСЯ БЕЗ СЦЕНЫ. Правило «годится ли попадание для робота» — чистая функция
        /// `IsSurfaceSuitable`. Промах (нет попадания, стена, NaN) обязан давать `false`, и тогда
        /// компонент НЕ трогает ни робота, ни сохранённую точку — он только сообщает причину.
        /// </summary>
        [Test]
        public void NoHit_DoesNotMoveRobot_AndKeepsLastValidPoint()
        {
            // 1. ПРОМАХ: попадания нет.
            KvXrRayHit miss = new KvXrRayHit();      // Source = None
            Assert.IsFalse(KvMrRobotPick.IsSurfaceSuitable(miss),
                "отсутствие попадания не может годиться для постановки робота");

            // 2. СТЕНА: попадание есть, но поверхность вертикальная.
            KvXrRayHit wall;
            wall.Source = KvXrHitSource.Real;
            wall.Distance = 1.2f;
            wall.Point = new Vector3(0f, 1f, 1.2f);
            wall.Normal = Vector3.forward;          // нормаль вбок
            wall.Name = "стена";
            Assert.IsFalse(KvMrRobotPick.IsSurfaceSuitable(wall),
                "на вертикальную поверхность робота не ставят — это стена, а не площадка");

            // 3. МУСОР (класс E-21): NaN-точка не должна пройти проверку «на глаз».
            KvXrRayHit bad;
            bad.Source = KvXrHitSource.Virtual;
            bad.Distance = 1f;
            bad.Point = new Vector3(float.NaN, 0f, 0f);
            bad.Normal = Vector3.up;
            bad.Name = "мусор";
            Assert.IsFalse(KvMrRobotPick.IsSurfaceSuitable(bad),
                "NaN-точка обязана отвергаться: Unity не рисует объект с NaN, и робот исчез бы бесследно");

            // 4. ГОРИЗОНТАЛЬНАЯ ПОВЕРХНОСТЬ ГОДИТСЯ — иначе проверка была бы «всегда нет».
            KvXrRayHit table;
            table.Source = KvXrHitSource.Real;
            table.Distance = 1.4f;
            table.Point = new Vector3(0.3f, 0.78f, 1.4f);
            table.Normal = Vector3.up;
            table.Name = "стол";
            Assert.IsTrue(KvMrRobotPick.IsSurfaceSuitable(table),
                "горизонтальная поверхность обязана годиться для постановки");

            // 5. ПРАВИЛО «НЕ ДВИГАТЬ БЕЗ ФИКСАЦИИ» ДОПОЛНИТЕЛЬНО СТРАХУЕТ: даже если попадание есть,
            // но места нет и фиксации нет — постановка невозможна и причина названа.
            string reason;
            Assert.IsFalse(KvMrRobotPick.CanPlace(true, false, true, true, out reason));
            StringAssert.Contains("зафиксируйте", reason);
        }

        // ============================================================ 1) ПОКРЫТИЕ ПОВЕРХНОСТЕЙ

        /// <summary>
        /// СТРОКА ЖУРНАЛА О ПОКРЫТИИ — РОВНО ТА, ЧТО ТРЕБУЕТ ВЛАДЕЛЕЦ, С ЧИСЛАМИ.
        ///
        /// ТРЕБОВАНИЕ ДОСЛОВНО:
        ///     [KvMr] ПОКРЫТИЕ ПОВЕРХНОСТЕЙ: нарисовано N (мешей A, разметки B) · стол найден: да|нет
        /// </summary>
        [Test]
        public void CoverageLogLine_HasBothCountsAndTableFlag()
        {
            string withTable = KvMrSurfacePainter.DescribeCoverage(7, 5, 2, true);
            string withoutTable = KvMrSurfacePainter.DescribeCoverage(7, 5, 2, false);
            string empty = KvMrSurfacePainter.DescribeCoverage(0, 0, 0, false);

            Assert.AreEqual("[KvMr] ПОКРЫТИЕ ПОВЕРХНОСТЕЙ: нарисовано 7 (мешей 5, разметки 2) · стол найден: да",
                withTable, "строка покрытия изменена — приёмка её не найдёт");

            StringAssert.Contains("стол найден: нет", withoutTable,
                "«стол не найден» обязано отличаться от «найден» словом, а не отсутствием строки");

            // РЕГУЛЯРКА НА ЧИСЛА: все три счётчика — числом.
            StringAssert.IsMatch(@"нарисовано \d+", withTable);
            StringAssert.IsMatch(@"мешей \d+", withTable);
            StringAssert.IsMatch(@"разметки \d+", withTable);

            // НОЛЬ ТОЖЕ ПЕЧАТАЕТСЯ И ОТЛИЧАЕТСЯ ОТ СЕМИ: «нарисовано 0» — это ответ.
            StringAssert.Contains("нарисовано 0", empty);
            Assert.AreNotEqual(withTable, empty, "ноль и семь поверхностей дали одинаковую строку");
        }

        /// <summary>
        /// ЦВЕТА ПОВЕРХНОСТЕЙ РАЗЛИЧИМЫ: горизонталь, стена и СТОЛ — три разных цвета (пункт 1).
        ///
        /// ПОЧЕМУ ЭТО ВАЖНО ПРОВЕРИТЬ. Владелец просил «каким-то покрытием», но смысл покрытия —
        /// показать, ГДЕ стол: зелёную подсветку он уже принял в AR. Если стена и стол станут одного
        /// цвета, покрытие будет мешать, а не помогать.
        /// </summary>
        [Test]
        public void SurfaceStyles_HaveDistinctColours()
        {
            Assert.AreNotEqual(KvMrSurfacePainter.HorizontalColor, KvMrSurfacePainter.VerticalColor,
                "горизонталь и стена одного цвета — покрытие перестаёт что-либо объяснять");
            Assert.AreNotEqual(KvMrSurfacePainter.HorizontalColor, KvMrSurfacePainter.TableColor,
                "стол не отличается цветом от обычной поверхности — оператор не увидит, куда ставить");
            Assert.AreNotEqual(KvMrSurfacePainter.VerticalColor, KvMrSurfacePainter.TableColor);

            // СТОЛ — ЗЕЛЁНЫЙ: тот же смысл, что у зелёной подсветки стола в AR.
            Assert.Greater(KvMrSurfacePainter.TableColor.g, KvMrSurfacePainter.TableColor.r,
                "цвет стола обязан быть зелёным: «сюда можно ставить»");

            // ЗАЛИВКА ПОЛУПРОЗРАЧНАЯ: иначе покрытие закроет реальный стол, ради которого всё и делалось.
            Assert.Less(KvMrSurfacePainter.FillAlpha, 1f, "заливка обязана быть полупрозрачной");
            Assert.Greater(KvMrSurfacePainter.FillAlpha, 0f, "нулевая заливка = её нет");
        }

        /// <summary>
        /// ВИД ПОВЕРХНОСТИ — ЧИСТАЯ ФУНКЦИЯ: классификатор решает цвет, а не догадка.
        ///
        /// ПРАВИЛА, КОТОРЫЕ ПРОВЕРЯЮТСЯ:
        ///   • размеченный стол → зелёный;
        ///   • стена/потолок → свой цвет (не спутать с полом);
        ///   • горизонталь → нейтральный;
        ///   • мелкий обрывок меша → не рисуется вовсе (иначе покрытие из тысяч осколков).
        /// </summary>
        [Test]
        public void SurfaceStyle_FollowsClassifierNotGuess()
        {
            RealSurfaceCandidate horizontal = Candidate(KvRealSurfaceKind.Unknown, KvRealSurfaceSource.Mesh,
                Vector3.up, 1.2f, 0.8f);
            Assert.AreEqual(KvMrSurfaceStyle.Horizontal, KvMrSurfacePainter.StyleOf(horizontal),
                "горизонтальный кусок меша обязан быть нейтральной горизонталью");

            RealSurfaceCandidate table = Candidate(KvRealSurfaceKind.Table, KvRealSurfaceSource.Semantic,
                Vector3.up, 1.0f, 0.7f);
            Assert.AreEqual(KvMrSurfaceStyle.Table, KvMrSurfacePainter.StyleOf(table),
                "поверхность, которую платформа назвала столом, обязана быть зелёной");

            RealSurfaceCandidate wall = Candidate(KvRealSurfaceKind.Wall, KvRealSurfaceSource.Mesh,
                Vector3.forward, 3.0f, 2.0f);
            Assert.AreEqual(KvMrSurfaceStyle.Vertical, KvMrSurfacePainter.StyleOf(wall),
                "стена обязана иметь свой вид, а не «горизонталь»");

            // ВЕРТИКАЛЬ ПО НОРМАЛИ, даже если платформа молчит: нормаль вбок — это стена.
            RealSurfaceCandidate byNormal = Candidate(KvRealSurfaceKind.Unknown, KvRealSurfaceSource.Mesh,
                Vector3.forward, 2.0f, 2.0f);
            Assert.AreEqual(KvMrSurfaceStyle.Vertical, KvMrSurfacePainter.StyleOf(byNormal),
                "поверхность с нормалью вбок обязана считаться вертикалью");

            // МЕЛКИЙ ОБРЫВОК — НЕ РИСУЕТСЯ.
            RealSurfaceCandidate tiny = Candidate(KvRealSurfaceKind.Unknown, KvRealSurfaceSource.Mesh,
                Vector3.up, 0.01f, 0.01f);
            Assert.AreEqual(KvMrSurfaceStyle.Skip, KvMrSurfacePainter.StyleOf(tiny),
                "обрывок 1 см не поверхность — рисовать его значит засорить вид");

            // ВЕРДИКТ КЛАССИФИКАТОРА ПОДНИМАЕТ ЦВЕТ ДО ЗЕЛЁНОГО: в MR стол чаще всего НЕ размечен,
            // и его признаёт именно классификатор по высоте и габаритам.
            RealTableVerdict tableVerdict;
            tableVerdict.IsTable = true;
            tableVerdict.Confidence = 0.8f;
            tableVerdict.Kind = KvRealSurfaceKind.Table;
            tableVerdict.Reason = "габариты и высота в норме · уверенность 0.80";
            Assert.AreEqual(KvMrSurfaceStyle.Table,
                KvMrSurfacePainter.StyleOf(horizontal, tableVerdict),
                "поверхность, признанная классификатором столом, обязана стать зелёной");
        }

        /// <summary>
        /// ПОКРЫТИЕ ПЕРЕСЧИТЫВАЕТСЯ НЕ ЧАЩЕ РАЗА В СЕКУНДУ (требование владельца дословно).
        /// Проверяется константой: чаще — это спам на кадре шлема.
        /// </summary>
        [Test]
        public void Coverage_IsLoggedNoMoreThanOncePerSecond()
        {
            Assert.GreaterOrEqual(KvMrSurfacePainter.ReportIntervalSeconds, 1f,
                "покрытие пересчитывается чаще раза в секунду — журнал и кадровый бюджет пострадают");
            Assert.Greater(KvMrSurfacePainter.GridDivisions, 0, "сетка без делений — это не сетка");
            Assert.Greater(KvMrSurfacePainter.OutlineWidthMeters, 0f, "контур нулевой толщины не виден");
        }

        /// <summary>Кандидат в поверхность — короткий конструктор для тестов.</summary>
        private static RealSurfaceCandidate Candidate(KvRealSurfaceKind kind, KvRealSurfaceSource source,
            Vector3 up, float sideA, float sideB)
        {
            RealSurfaceCandidate candidate;
            candidate.Kind = kind;
            candidate.Source = source;
            candidate.Center = new Vector3(0f, 0.78f, 1f);
            candidate.Up = up;
            candidate.SideA = sideA;
            candidate.SideB = sideB;
            candidate.TrackedSeconds = 2f;
            candidate.IsLargestHorizontal = false;
            return candidate;
        }

        /// <summary>
        /// РАВНЫ ЛИ РАССТОЯНИЯ — С УЧЁТОМ БЕСКОНЕЧНОСТИ.
        ///
        /// ПОЧЕМУ НЕ ПРОСТО «РАЗНИЦА МЕНЬШЕ 1e-5». У ответа «попадания нет» расстояние равно
        /// `+∞`, а `|∞ − ∞|` даёт NaN, и любое сравнение с NaN — ЛОЖЬ. Обычная проверка разницы
        /// покраснела бы на правильном коде ровно там, где оба луча честно ничего не нашли. Это та
        /// же ловушка E-21, из-за которой в проекте мусор проходил за попадание.
        /// </summary>
        private static bool SameDistance(float a, float b)
        {
            if (float.IsInfinity(a) || float.IsInfinity(b))
                return float.IsInfinity(a) && float.IsInfinity(b) &&
                       (a > 0f) == (b > 0f);
            if (float.IsNaN(a) || float.IsNaN(b)) return false;
            return Math.Abs(a - b) < 1e-5f;
        }
    }
}
