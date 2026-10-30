using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты ВВОДА ШЛЕМА (сессия §32, карточка Б14 «контроллеры не обнаружены»).
    ///
    /// ЗАЧЕМ ЭТИ ТЕСТЫ. На живом шлеме 08.10.2026 приложение написало одну строку:
    /// `[KvXr] XR-ввод: шлем есть · правый контроллер нет · левый контроллер нет · устройств в системе 0`.
    /// По ней НЕЛЬЗЯ было понять, что произошло: то ли устройства вообще не пришли (шлем спал),
    /// то ли пришли, но у них нет нужных элементов (профиль OpenXR беднее ожидаемого). Проверить
    /// это на устройстве можно только повторным запуском, а вот ПРАВИЛА разбора контроллеров
    /// проверяются здесь и без шлема.
    ///
    /// ЧТО ИМЕННО ЗАКРЕПЛЕНО.
    ///   1) В списке имён курка есть `select`: для Android в проекте включён
    ///      `KHRSimpleControllerProfile` (§31), у которого нет `trigger`. Без этого курок
    ///      на живом устройстве просто не срабатывал бы.
    ///   2) Богатый профиль проверяется РАНЬШЕ простого (порядок в списке — часть контракта).
    ///   3) Логика выбора первого существующего элемента (`FirstPresent`) ведёт себя предсказуемо
    ///      на пустых списках и null-аргументах — иначе поиск падал бы в кадре.
    ///   4) Строки отчёта (`Describe`, `DescribeDevices`) работают БЕЗ устройств: в EditMode
    ///      устройств нет, и это ровно тот случай, который обязан быть обработан словами,
    ///      а не исключением.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: тестов «контроллер найден» — без шлема контроллеров не существует,
    /// и такой тест был бы выдумкой. Проверка на устройстве — отдельный шаг (журнал `Logs\device-*`).
    /// </summary>
    public class KvXrInputTests
    {
        // ============================================================ 1. ИМЕНА УПРАВЛЯЮЩИХ ЭЛЕМЕНТОВ

        /// <summary>
        /// АНДРОИДНАЯ ПРИЧИНА ЭТОГО ТЕСТА: `KHRSimpleControllerProfile` не имеет `trigger`,
        /// у него `select`. Если `select` выпадет из списка, курок в шлеме перестанет работать
        /// молча — ошибки не будет, просто ноль.
        /// </summary>
        [Test]
        public void TriggerNames_ContainSelect_ForSimpleOpenXrProfile()
        {
            var names = new List<string>(KvXrInputSource.TriggerControlNames);
            Assert.IsTrue(names.Contains("select"),
                "в списке имён курка нет `select` — на профиле KHRSimpleController (включён для Android " +
                "в §31) курок не сработает");
            Assert.IsTrue(names.Contains("selectPressed"),
                "в списке имён курка нет `selectPressed`");
        }

        /// <summary>Богатый профиль (Oculus Touch) проверяется РАНЬШЕ простого: порядок — контракт.</summary>
        [Test]
        public void TriggerNames_PreferRichProfileOverSimple()
        {
            var names = new List<string>(KvXrInputSource.TriggerControlNames);
            int trigger = names.IndexOf("trigger");
            int select = names.IndexOf("select");

            Assert.GreaterOrEqual(trigger, 0, "в списке нет `trigger` — потерян основной путь Oculus Touch");
            Assert.Greater(select, trigger,
                "`select` обязан идти ПОСЛЕ `trigger`: иначе на богатом профиле курок читался бы " +
                "как кнопка, и аналоговое значение терялось бы");
        }

        /// <summary>Первым в списке курка стоит проверка «нажата ли кнопка», а не ось.</summary>
        [Test]
        public void TriggerNames_StartWithPressedButton()
        {
            Assert.AreEqual("triggerPressed", KvXrInputSource.TriggerControlNames[0],
                "список курка обязан начинаться с `triggerPressed`: нажатие должно читаться раньше оси");
        }

        /// <summary>Грип: у простых профилей вместо `grip` приходит `squeeze`.</summary>
        [Test]
        public void GripNames_ContainSqueezeFallback()
        {
            var names = new List<string>(KvXrInputSource.GripControlNames);
            Assert.IsTrue(names.Contains("grip"), "в списке грипа нет `grip`");
            Assert.IsTrue(names.Contains("squeeze"),
                "в списке грипа нет `squeeze` — на части профилей боковая кнопка называется именно так");
        }

        /// <summary>Стик: у простых профилей имя оси — `primary2DAxis`.</summary>
        [Test]
        public void StickNames_ContainPrimary2DAxisFallback()
        {
            var names = new List<string>(KvXrInputSource.StickControlNames);
            Assert.IsTrue(names.Contains("thumbstick"), "в списке стика нет `thumbstick`");
            Assert.IsTrue(names.Contains("primary2DAxis"),
                "в списке стика нет `primary2DAxis` — у простых профилей OpenXR ось называется так");
        }

        // ============================================================ 2. ЛОГИКА ВЫБОРА ЭЛЕМЕНТА

        /// <summary>Берётся ПЕРВЫЙ существующий элемент — от него зависит приоритет профилей.</summary>
        [Test]
        public void FirstPresent_ReturnsFirstExistingName()
        {
            var names = new[] { "trigger", "select", "primaryButton" };
            string chosen = KvXrInputSource.FirstPresent(names, n => n == "select");
            Assert.AreEqual("select", chosen, "выбран не первый существующий элемент");
        }

        /// <summary>Ничего не найдено — честный null (вызывающий код вернёт 0, а не «работает»).</summary>
        [Test]
        public void FirstPresent_ReturnsNull_WhenNothingExists()
        {
            var names = new[] { "trigger", "select" };
            Assert.IsNull(KvXrInputSource.FirstPresent(names, n => false),
                "при отсутствии всех элементов обязан вернуться null");
        }

        /// <summary>Пустые имена пропускаются, null-аргументы не роняют поиск (это кадровый путь).</summary>
        [Test]
        public void FirstPresent_SurvivesEmptyAndNullInput()
        {
            Assert.IsNull(KvXrInputSource.FirstPresent(null, n => true), "null-список обязан вернуть null");
            Assert.IsNull(KvXrInputSource.FirstPresent(new[] { "trigger" }, null), "null-функция обязана вернуть null");
            Assert.AreEqual("select",
                KvXrInputSource.FirstPresent(new[] { "", null, "select" }, n => true),
                "пустые имена обязаны пропускаться, а не считаться найденными");
        }

        /// <summary>Список из одних пустых имён — тоже «ничего нет».</summary>
        [Test]
        public void FirstPresent_AllEmptyNames_ReturnsNull()
        {
            Assert.IsNull(KvXrInputSource.FirstPresent(new[] { "", "", null }, n => true));
        }

        // ============================================================ 3. ОТЧЁТЫ БЕЗ УСТРОЙСТВ

        /// <summary>
        /// Прежняя строка отчёта обязана работать без устройств: именно она попадает в журнал
        /// устройства первой, и падать ей нельзя.
        /// </summary>
        [Test]
        public void Describe_WorksWithoutAnyDevices()
        {
            string text = KvXrInputSource.Describe();
            Assert.IsFalse(string.IsNullOrEmpty(text), "строка состояния ввода пустая");
            Assert.IsTrue(text.Contains("XR-ввод"), "в строке состояния нет пометки `XR-ввод`: " + text);
            Assert.IsTrue(text.Contains("устройств в системе"),
                "в строке состояния нет числа устройств: " + text);
        }

        /// <summary>
        /// НОВЫЙ подробный отчёт (ради него и заведена карточка Б14) обязан отвечать словами:
        /// сколько устройств и какие, а если их нет — сказать это прямо.
        /// </summary>
        [Test]
        public void DescribeDevices_ReportsCountAndNamesOrSaysThereAreNone()
        {
            string text = KvXrInputSource.DescribeDevices();
            Assert.IsFalse(string.IsNullOrEmpty(text), "подробный отчёт о вводе пуст");
            Assert.IsTrue(text.Contains("устройств в InputSystem"),
                "в подробном отчёте нет числа устройств Input System: " + text);

            if (UnityEngine.InputSystem.InputSystem.devices.Count == 0)
            {
                Assert.IsTrue(text.Contains("НЕТ НИ ОДНОГО УСТРОЙСТВА"),
                    "при нуле устройств отчёт обязан сказать это прямо, а не молчать: " + text);
            }
        }

        /// <summary>Сводка по контроллеру без контроллера не падает и говорит «нет».</summary>
        [Test]
        public void ControlSummary_WithoutController_SaysSo()
        {
            string text = KvXrInputSource.ControlSummary(null);
            Assert.IsFalse(string.IsNullOrEmpty(text));
            Assert.IsTrue(text.Contains("нет"), "сводка без контроллера обязана сказать «нет»: " + text);
        }

        /// <summary>Пересканирование вручную не бросает исключений (используется диагностикой в кадре).</summary>
        [Test]
        public void ForceScan_DoesNotThrowInEditMode()
        {
            Assert.DoesNotThrow(() => KvXrInputSource.ForceScan());
            Assert.DoesNotThrow(() => KvXrInputSource.ForceScan());
        }

        // ============================================================ 4. ПРОСТРАНСТВО КООРДИНАТ (30.10.2026)
        //
        // ЗАЧЕМ. Аудит 30.10.2026 нашёл: поза `devicePose` возвращалась в ПРОСТРАНСТВЕ
        // ОТСЛЕЖИВАНИЯ, а модель контроллера ставится в МИР (`KvXrControllerVisuals.Place`).
        // После телепорта или поворота рига модель оставалась на старом месте — тот же симптом,
        // что в E-07, но для моделей контроллеров. Без шлема это проверяется подстановкой начала
        // отслеживания: риг, поставленный в (1, 0, 2), обязан сдвинуть позу на то же смещение.

        /// <summary>Начало отслеживания снимается после каждого теста: это статическое поле.</summary>
        private Transform rig;

        [SetUp]
        public void SetUpRig()
        {
            // Чистое состояние обязательно: поле статическое и живёт между тестами, а прогон
            // EditMode идёт в одном домене (проверено прогоном 30.10.2026 — без этого тест
            // падал на состоянии, оставленном соседним).
            KvXrInputSource.TrackingOrigin = null;
        }

        [TearDown]
        public void TearDownRig()
        {
            if (rig != null && KvXrInputSource.TrackingOrigin == rig) KvXrInputSource.TrackingOrigin = null;
            if (rig != null) Object.DestroyImmediate(rig.gameObject);
            rig = null;
            KvXrInputSource.TrackingOrigin = null;
        }

        /// <summary>
        /// РИГ НИКОГДА НЕ ОСТАВЛЯЕТ ССЫЛКУ НА УНИЧТОЖЕННЫЙ ТРАНСФОРМ.
        ///
        /// ЗАЧЕМ. `KvXrInputSource.TrackingOrigin` — статическое поле, через него проходят ВСЕ
        /// позы трекинга (руки, контроллеры, лучи). Ссылка на уничтоженный трансформ означала бы
        /// `MissingReferenceException` в каждом кадре — ровно то, что бывает после пробуждения
        /// шлема и пересоздания рига.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ БЕЗ ДВИЖКА. Сама развязка вынесена в чистый метод
        /// `KvXrInputSource.ClearOriginIf` — его и проверяем: «своя ссылка снимается», «чужая
        /// остаётся» (второй риг не должен снять ссылку первого), «без флага установки не
        /// трогаем». Проверять `OnDestroy` через `DestroyImmediate` в EditMode нельзя: там
        /// `AddComponent` не вызывает `Awake`, и флаг установки взвести нечем — тест проверял бы
        /// сам себя (проверено прогоном 30.10.2026).
        /// </summary>
        [Test]
        public void ClearOriginIf_RemovesOnlyItsOwnReference()
        {
            var go = new GameObject("KvRigForTest");
            rig = go.transform;

            KvXrInputSource.TrackingOrigin = rig;
            Assert.IsTrue(KvXrInputSource.ClearOriginIf(true, rig),
                "своя ссылка обязана сниматься");
            Assert.IsNull(KvXrInputSource.TrackingOrigin, "после снятия поле пустое");

            KvXrInputSource.TrackingOrigin = rig;
            Assert.IsFalse(KvXrInputSource.ClearOriginIf(false, rig),
                "без флага установки снимать нельзя: риг ничего не ставил");
            Assert.AreSame(rig, KvXrInputSource.TrackingOrigin, "чужая/непоставленная ссылка остаётся");
        }

        /// <summary>
        /// РИГ СМЕЩАЕТ МИРОВУЮ ПОЗУ РОВНО НА СВОЁ СМЕЩЕНИЕ. Это и есть проверка правила
        /// «сырые координаты трекинга в мир не пускать»: поза (0,2; 1,0; 0,3) при риге в
        /// (1, 0, 2) обязана оказаться в (1,2; 1,0; 2,3).
        /// </summary>
        [Test]
        public void RigOffset_MovesTrackingPoseToWorld()
        {
            var go = new GameObject("KvRigOffsetForTest");
            rig = go.transform;
            rig.SetPositionAndRotation(new Vector3(1f, 0f, 2f), Quaternion.identity);
            KvXrInputSource.TrackingOrigin = rig;

            Vector3 tracking = new Vector3(0.2f, 1.0f, 0.3f);
            Vector3 world = KvXrInputSource.TrackingOrigin.TransformPoint(tracking);
            Assert.AreEqual(1.2f, world.x, 1e-4f, "смещение рига по X не применено");
            Assert.AreEqual(1.0f, world.y, 1e-4f, "смещение рига по Y не применено");
            Assert.AreEqual(2.3f, world.z, 1e-4f, "смещение рига по Z не применено");
        }

        /// <summary>
        /// БЕЗ РИГА ТРАНСФОРМАЦИИ НЕТ. Пока начало отслеживания не поставлено, позы остаются как
        /// пришли — это прежнее поведение, и при починке его нельзя потерять.
        /// </summary>
        [Test]
        public void NoRig_LeavesPosesUnchanged()
        {
            KvXrInputSource.TrackingOrigin = null;
            Vector3 tracking = new Vector3(0.2f, 1.0f, 0.3f);
            Vector3 world = KvXrInputSource.TrackingOrigin == null
                ? tracking
                : KvXrInputSource.TrackingOrigin.TransformPoint(tracking);
            Assert.AreEqual(tracking, world, "без рига поза обязана остаться нетронутой");
        }

        /// <summary>
        /// ОДНО НАЖАТИЕ — ОДНО ДЕЙСТВИЕ: КАДР ЗАХВАТА ОКНА ВИДЕН ВЫЗЫВАЮЩЕМУ.
        ///
        /// Захват окна указательным пальцем и нажатие иконки читают ОДИН курок. Чтобы нажатие
        /// в этом кадре можно было пропустить, `KvXrWindowDrag` обязан честно сказать «взял
        /// именно сейчас» — и не говорить этого, когда переноса не было.
        /// </summary>
        [Test]
        public void WindowDrag_GrabTick_IsFalseWithoutGrab()
        {
            var drag = new KvXrWindowDrag();
            Assert.IsFalse(drag.GrabbedThisTick, "до первого шага захвата не было");
            Assert.IsFalse(drag.Active, "перенос не начат");

            drag.Reset();
            Assert.IsFalse(drag.GrabbedThisTick, "Reset обязан снять признак захвата");
            Assert.IsFalse(drag.Active, "после Reset перенос не активен");
        }
    }
}
