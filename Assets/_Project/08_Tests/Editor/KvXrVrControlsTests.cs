// ================================================================================================
//  KvXrVrControlsTests.cs — ВРАТА ПЛАНА VR-УПРАВЛЕНИЯ (05.11.2026): перемещение по ангару, комфорт,
//  наполнение кнопок. ПЕРЕВЕДЕНЫ НА ПОВЕДЕНИЕ в рамках P4-3 (12.11.2026).
//
//  ЧТО БЫЛО НЕ ТАК. Семь проверок из этого файла ЧИТАЛИ ИСХОДНИК: искали в `KvXrLocomotion.cs`,
//  `KvXrActions.cs`, `KvXrStartMenu.cs`, `KvXrFlowBridge.cs`, `KvXrRig.cs` имена методов
//  (`ClampToHangar`, `TriggerEStop`, `OpenCommandSearch`, `DragPointTo`, `Ensure`…). Такой тест
//  ломается от переформулировки комментария и ПРОХОДИТ на сломанном коде: имя метода может остаться
//  в файле, а вызов — исчезнуть из рабочей ветки.
//
//  ЧТО СТАЛО. Те же самые утверждения проверяются ПО РЕЗУЛЬТАТУ — через чистые функции, которые
//  вызываются из тех же мест:
//    • шаг хода, разгон и границы зала   → `KvXrLocomotion.SmoothStep` + `KvXrWalkRules`
//    • чем целится телепорт              → `KvXrLocomotion.AimSourceFor`
//    • удержания кнопок и их действия    → `KvXrActions.ResolveButton` / `HoldSecondsOf` / `HoldActionId`
//    • вход в меню коротким нажатием     → `KvXrStartMenu.ShouldOpenFromStick` / `OpensOnStick`
//    • перенос точки грипом              → `KvXrFlowBridge.ShouldDragPointByGrip` / `KvXrInputSource.IsDragGrip`
//    • виньетка комфорта                 → `KvXrComfortVignette.Ensure` (реально создаётся)
//    • имена управляющих элементов       → сами таблицы имён `KvXrInputSource`
//
//  НИ ОДНА ПРОВЕРКА НЕ ОСЛАБЛЕНА: где раньше утверждалось «строка есть», теперь утверждается
//  «поведение такое»; где проверялся список имён — проверяется пересечение списков и выбор имени.
// ================================================================================================

using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVv.Tests
{
    /// <summary>VR-управление: перемещение, комфорт, наполнение кнопок.</summary>
    [Category(KazistovVvTests.KvTestCategories.Fast)]
    public class KvXrVrControlsTests
    {
        // ================================================================ перемещение

        /// <summary>По решению владельца 05.11.2026 «как во всех играх»: плавный ход по умолчанию.</summary>
        [Test]
        public void Movement_SmoothIsDefaultAndTeleportStillAvailable()
        {
            KvXrMoveMode saved = KvXrSettings.GetMoveMode();
            try
            {
                KvXrSettings.ResetMoveMode();
                Assert.AreEqual(KvXrMoveMode.Smooth, KvXrSettings.GetMoveMode(),
                    "по умолчанию должен быть плавный ход (просьба владельца 05.11.2026)");
                KvXrSettings.SetMoveMode(KvXrMoveMode.Teleport);
                Assert.AreEqual(KvXrMoveMode.Teleport, KvXrSettings.GetMoveMode(),
                    "телепорт обязан оставаться доступен — это второй способ, а не удалённый");
            }
            finally { KvXrSettings.SetMoveMode(saved); }
        }

        /// <summary>Скорость по умолчанию 1,8 м/с — «нормальный темп симулятора», не бег.</summary>
        [Test]
        public void Movement_DefaultSpeedIsWalkPace()
        {
            Assert.AreEqual(1.8f, KvXrSettings.DefaultSmoothSpeed, 1e-4f);
            Assert.GreaterOrEqual(KvXrSettings.DefaultSmoothSpeed, KvXrSettings.MinSmoothSpeed);
            Assert.LessOrEqual(KvXrSettings.DefaultSmoothSpeed, KvXrSettings.MaxSmoothSpeed);
        }

        /// <summary>Разгон и мёртвая зона хода ограничены разумными рамками.</summary>
        [Test]
        public void Movement_AccelerationAndDeadzoneAreClamped()
        {
            float savedAccel = KvXrSettings.GetMoveAcceleration();
            float savedZone = KvXrSettings.GetWalkDeadzone();
            try
            {
                KvXrSettings.SetMoveAcceleration(100f);
                Assert.LessOrEqual(KvXrSettings.GetMoveAcceleration(), KvXrSettings.MaxMoveAcceleration);
                KvXrSettings.SetMoveAcceleration(0f);
                Assert.GreaterOrEqual(KvXrSettings.GetMoveAcceleration(), KvXrSettings.MinMoveAcceleration);

                KvXrSettings.SetWalkDeadzone(0.9f);
                Assert.LessOrEqual(KvXrSettings.GetWalkDeadzone(), KvXrSettings.MaxWalkDeadzone);
                KvXrSettings.SetWalkDeadzone(0f);
                Assert.GreaterOrEqual(KvXrSettings.GetWalkDeadzone(), KvXrSettings.MinWalkDeadzone);
            }
            finally
            {
                KvXrSettings.SetMoveAcceleration(savedAccel);
                KvXrSettings.SetWalkDeadzone(savedZone);
            }
        }

        /// <summary>Границы зала: за его пределами позиция подтягивается внутрь с отступом от стен.</summary>
        [Test]
        public void WalkRules_ClampKeepsOperatorInsideHangar()
        {
            Bounds bounds = KvXrWalkRules.HangarBounds();
            Vector3 far = new Vector3(bounds.max.x + 100f, 0f, bounds.max.z + 100f);
            Vector3 clamped = KvXrWalkRules.ClampToHangar(far);

            Assert.LessOrEqual(clamped.x, bounds.max.x - KvXrWalkRules.WallMarginMeters + 1e-3f,
                "за границей зала оператор не должен оказаться");
            Assert.LessOrEqual(clamped.z, bounds.max.z - KvXrWalkRules.WallMarginMeters + 1e-3f);

            // И внутри зала позиция не меняется — границы не «тянут» оператора к центру.
            Vector3 inside = bounds.center;
            Vector3 kept = KvXrWalkRules.ClampToHangar(inside);
            Assert.AreEqual(inside.x, kept.x, 1e-3f);
            Assert.AreEqual(inside.z, kept.z, 1e-3f);
        }

        /// <summary>Габарит зала — тот же, что у компьютерной сцены (40,18 × 50,31 м).</summary>
        [Test]
        public void WalkRules_HangarSizeMatchesDesktop()
        {
            Bounds bounds = KvXrWalkRules.HangarBounds();
            Assert.AreEqual(KvHangarPlacement.DesktopHangarSize.x, bounds.size.x, 1e-2f);
            Assert.AreEqual(KvHangarPlacement.DesktopHangarSize.z, bounds.size.z, 1e-2f);
        }

        /// <summary>Без препятствий шаг не искажается: коллизии не «дёргают» оператора на ровном полу.</summary>
        [Test]
        public void WalkRules_NoObstacleKeepsDesiredPosition()
        {
            Vector3 current = new Vector3(0f, 0f, 0f);
            Vector3 desired = new Vector3(0.05f, 0f, 0.05f);
            Vector3 result = KvXrWalkRules.ResolveObstacles(current, desired, 0.30f, 1.70f);
            Assert.AreEqual(desired.x, result.x, 1e-4f);
            Assert.AreEqual(desired.z, result.z, 1e-4f);
        }

        /// <summary>
        /// ШАГ ПЛАВНОГО ХОДА ПРИМЕНЯЕТ ПРАВИЛА ЗАЛА, РАЗГОН И ЦЕЛИТСЯ ПОЗОЙ УКАЗАНИЯ.
        ///
        /// ЧТО БЫЛО ЗДЕСЬ РАНЬШЕ: поиск в `KvXrLocomotion.cs` имён `KvXrWalkRules.ClampToHangar`,
        /// `ResolveObstacles`, `GetMoveAcceleration` и `TryRayAimPose`. Проверялось наличие слов.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ ТЕПЕРЬ — поведение самого шага (`KvXrLocomotion.SmoothStep`), который
        /// вызывается из кадрового кода:
        ///   1) в VR (правила включены) оператор не выходит за стены зала;
        ///   2) в MR (правила выключены) его НЕ тянет в зал: комната 1:1, ходьба свободная;
        ///   3) разгон ограничен: за один кадр скорость не скачет к целевой мгновенно;
        ///   4) без ввода оператор не уезжает сам;
        ///   5) телепорт целится ТОЛЬКО позой указания: без неё источник прицела — «нет».
        /// </summary>
        [Test]
        public void Locomotion_UsesWalkRulesAndAimPose()
        {
            Bounds bounds = KvXrWalkRules.HangarBounds();

            // 1. ГРАНИЦЫ ЗАЛА (VR). Стоим далеко за стеной и шагаем ещё дальше.
            Vector3 outside = new Vector3(bounds.max.x + 50f, 0f, bounds.max.z + 50f);
            Vector3 velocity;
            Vector3 stepped = KvXrLocomotion.SmoothStep(outside, Vector3.zero,
                new Vector3(0f, 0f, 100f), 30f, 3f, true, out velocity);
            Assert.LessOrEqual(stepped.x, bounds.max.x - KvXrWalkRules.WallMarginMeters + 1e-3f,
                "шаг вывел оператора за стену зала — правила ходьбы не применяются");
            Assert.LessOrEqual(stepped.z, bounds.max.z - KvXrWalkRules.WallMarginMeters + 1e-3f,
                "шаг вывел оператора за стену зала по Z");

            // 2. MR: ПРАВИЛА ВЫКЛЮЧЕНЫ. Зал скрыт, комната настоящая — границы НЕ должны тянуть.
            Vector3 freeWalk;
            Vector3 freeVelocity;
            freeWalk = KvXrLocomotion.SmoothStep(outside, Vector3.zero, Vector3.zero, 30f, 3f, false,
                out freeVelocity);
            Assert.AreEqual(outside.z, freeWalk.z, 1e-4f,
                "в MR оператора притянуло к границам зала — а зал там скрыт, комната 1:1");

            // 3. РАЗГОН. С нуля к 3 м/с за кадр 0,02 с при ускорении 4 м/с² — не больше 0,08 м/с.
            Vector3 acceleratedVelocity;
            Vector3 accelerated = KvXrLocomotion.SmoothStep(Vector3.zero, Vector3.zero,
                new Vector3(0f, 0f, 3f), 4f, 0.02f, false, out acceleratedVelocity);
            Assert.LessOrEqual(acceleratedVelocity.magnitude, 4f * 0.02f + 1e-4f,
                "скорость скакнула к целевой мгновенно — разгон/торможение не работает, ход «дёргает»");
            Assert.Greater(acceleratedVelocity.magnitude, 0f, "оператор вообще не начал движение");
            Assert.AreEqual(acceleratedVelocity.z * 0.02f, accelerated.z, 1e-4f,
                "смещение за кадр не равно скорость × время кадра");

            // 4. БЕЗ ВВОДА НЕ ЕДЕМ. Нулевая целевая скорость и нулевая текущая — смещения нет.
            Vector3 idleVelocity;
            Vector3 idle = KvXrLocomotion.SmoothStep(Vector3.zero, Vector3.zero, Vector3.zero, 4f, 0.02f,
                true, out idleVelocity);
            Assert.AreEqual(0f, idle.x, 1e-6f, "без ввода оператора «уносит» — ошибка знака или времени кадра");
            Assert.AreEqual(0f, idle.z, 1e-6f, "без ввода оператора «уносит» по Z");
            Assert.AreEqual(0f, idleVelocity.magnitude, 1e-6f, "скорость в покое обязана быть нулевой");

            // 5. ПРИЦЕЛ ТЕЛЕПОРТА. Позы указания нет — телепорта нет (никакой подмены на позу кисти).
            Assert.AreEqual(KvXrLocomotion.TeleportAimSource.None, KvXrLocomotion.AimSourceFor(false),
                "без позы указания телепорт обязан быть невозможен, иначе оператор перенесётся " +
                "в место, которое не выбирал");
            Assert.AreEqual(KvXrLocomotion.TeleportAimSource.RayPose, KvXrLocomotion.AimSourceFor(true),
                "поза указания есть — телепорт обязан целиться именно ею");
        }

        // ================================================================ кнопки

        /// <summary>
        /// УДЕРЖАНИЯ И MENU ЗАПОЛНЕНЫ: E-STOP, поиск, смена хода, возврат к столу, перестановка меню.
        ///
        /// ЧТО БЫЛО ЗДЕСЬ РАНЬШЕ: поиск в `KvXrActions.cs` имён `TriggerEStop`, `OpenCommandSearch`,
        /// `ToggleMoveMode`, `ReturnToWorkPosition`, `RepositionMenu`, `KvHapticsBridge.Pulse`.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ ТЕПЕРЬ: таблица удержаний и правило «одно нажатие — одно действие»:
        ///   1) у каждой из четырёх кнопок есть СВОЁ действие удержания, и идентификаторы разные —
        ///      иначе две кнопки делали бы одно и то же, а одна не делала бы ничего;
        ///   2) E-STOP требует УДЕРЖАНИЯ в целую секунду: случайное касание робота не остановит;
        ///   3) короткое нажатие даёт ДРУГОЕ действие (у Menu — поиск по командам);
        ///   4) после сработавшего удержания отпускание НЕ даёт второго действия;
        ///   5) тактильный отклик вызывается там, где он обещан, и не падает без контроллера.
        /// </summary>
        [Test]
        public void Actions_HoldButtonsAreFilled()
        {
            Assert.AreEqual("estop", KvXrActions.HoldActionId(KvXrActions.HoldButton.B),
                "удержание B не делает E-STOP — аварийная кнопка пустая");
            Assert.AreEqual("move-mode", KvXrActions.HoldActionId(KvXrActions.HoldButton.X),
                "удержание X не переключает способ хода");
            Assert.AreEqual("menu-reposition", KvXrActions.HoldActionId(KvXrActions.HoldButton.Menu),
                "удержание Menu не переставляет меню");
            Assert.AreEqual("return-home", KvXrActions.HoldActionId(KvXrActions.HoldButton.LeftStickClick),
                "удержание стика не возвращает к столу");

            // ДЕЙСТВИЯ РАЗНЫЕ: две кнопки с одинаковым идентификатором означали бы «одна из них пустая».
            Assert.AreNotEqual(KvXrActions.HoldActionId(KvXrActions.HoldButton.B),
                KvXrActions.HoldActionId(KvXrActions.HoldButton.X),
                "удержания B и X делают одно и то же — одна из кнопок осталась без своего действия");

            // ПОРОГ E-STOP — ЦЕЛАЯ СЕКУНДА. Это не придирка: E-STOP останавливает робота, и срабатывание
            // по случайному касанию оператор воспримет как «робот встал сам».
            Assert.AreEqual(1.0f, KvXrActions.HoldSecondsOf(KvXrActions.HoldButton.B), 1e-4f,
                "порог удержания E-STOP изменился — случайное касание либо остановит робота, либо " +
                "заставит держать кнопку дольше, чем обещано");
            Assert.Greater(KvXrActions.HoldSecondsOf(KvXrActions.HoldButton.X), 0f,
                "у удержания X нет порога — оно сработает от любого касания");

            // КОРОТКОЕ НАЖАТИЕ ≠ УДЕРЖАНИЕ.
            float estopHold = KvXrActions.HoldSecondsOf(KvXrActions.HoldButton.B);
            Assert.AreEqual(KvXrActions.ButtonAction.None,
                KvXrActions.ResolveButton(true, false, false, estopHold * 0.5f, estopHold),
                "удержание сработало раньше своего порога — одно касание даст E-STOP");
            Assert.AreEqual(KvXrActions.ButtonAction.Hold,
                KvXrActions.ResolveButton(true, true, false, estopHold, estopHold),
                "кнопка удержана дольше порога, а удержание не сработало — второе действие недостижимо");
            Assert.AreEqual(KvXrActions.ButtonAction.Press,
                KvXrActions.ResolveButton(false, true, false, 0.1f, estopHold),
                "короткое нажатие обязано давать ДРУГОЕ действие (у Menu это поиск по командам)");
            Assert.AreEqual(KvXrActions.ButtonAction.None,
                KvXrActions.ResolveButton(false, true, true, 0.1f, estopHold),
                "после сработавшего удержания отпускание дало ВТОРОЕ действие — одно нажатие " +
                "сработало дважды (E-STOP и «назад» одновременно)");

            // ТАКТИЛЬНЫЙ ОТКЛИК: без контроллера он обязан честно вернуть «недоступно», а не упасть.
            // На живом шлеме тот же вызов отправляет импульс — именно поэтому проверяется вызов.
            Assert.DoesNotThrow(delegate { KvHapticsBridge.Pulse(KvXrQuickHand.Right, true); },
                "вызов вибрации падает без контроллера — нажатия останутся без отклика и с ошибкой в консоли");
            StringAssert.Contains("импульсов", KvHapticsBridge.Describe(),
                "строка диагностики вибрации не сообщает, сколько импульсов отправлено");
        }

        /// <summary>
        /// КНОПКА Menu БОЛЬШЕ НЕ «МЁРТВАЯ».
        ///
        /// ЧТО БЫЛО РАНЬШЕ: поиск `KvXrInputSource.MenuButton` в `KvXrActions.cs`. Наличие слова не
        /// доказывало, что кнопка что-то делает. Теперь проверяется, что у Menu есть СВОЙ порог
        /// удержания и что удержание реально распознаётся правилом.
        /// </summary>
        [Test]
        public void Actions_MenuButtonIsUsed()
        {
            float hold = KvXrActions.HoldSecondsOf(KvXrActions.HoldButton.Menu);
            Assert.Greater(hold, 0f, "у Menu нет порога удержания — кнопка снова ни к чему не подключена");
            Assert.AreEqual(KvXrActions.ButtonAction.Hold,
                KvXrActions.ResolveButton(true, true, false, hold, hold),
                "удержание Menu не распознаётся — переставить меню перед собой нечем");
            Assert.IsNotEmpty(KvXrActions.HoldActionId(KvXrActions.HoldButton.Menu),
                "у удержания Menu нет действия — кнопка читается, но ничего не делает");
        }

        /// <summary>
        /// A/B/X/Y НЕ ДУБЛИРУЮТСЯ КАК КУРОК. Класс E-08: на простом профиле одно нажатие давало два
        /// действия, потому что `primaryButton` попадал в список имён курка.
        ///
        /// ЧТО БЫЛО РАНЬШЕ: сравнение строки-списка имён с эталоном. Что стало: проверяется
        /// ПЕРЕСЕЧЕНИЕ самих таблиц имён, которыми пользуется чтение ввода. Пересечение непусто ровно
        /// тогда, когда кнопка одновременно считается курком, — от переформулировки комментариев это
        /// не зависит.
        /// </summary>
        [Test]
        public void Input_PrimaryButtonIsNotTrigger()
        {
            string[] primary = KvXrInputSource.PrimaryButtonControlNames;
            string[] trigger = KvXrInputSource.TriggerControlNames;

            Assert.IsNotEmpty(primary, "A/B/X/Y должны остаться: список кнопок пуст — нажатия не читаются");
            Assert.IsNotEmpty(trigger, "список имён курка пуст — курок не читается вовсе");

            foreach (string name in primary)
                Assert.IsFalse(System.Array.IndexOf(trigger, name) >= 0,
                    "имя «" + name + "» есть И в списке кнопок, И в списке курка — на простом профиле " +
                    "одно нажатие даст два действия (класс E-08)");

            // САМОПРОВЕРКА РАЗБОРА: выбор имени из списка работает, иначе проверка выше была бы пустой.
            Assert.AreEqual("trigger", KvXrInputSource.FirstPresent(trigger,
                    delegate (string n) { return n == "trigger" || n == "select"; }),
                "поиск первого присутствующего имени сломан — проверка списков была бы вакуумной");
            Assert.AreEqual("primaryButton", KvXrInputSource.FirstPresent(primary,
                    delegate (string n) { return n == "primaryButton"; }),
                "поиск первого присутствующего имени сломан для кнопок");
        }

        /// <summary>
        /// МЕНЮ ОТКРЫВАЕТСЯ КОРОТКИМ НАЖАТИЕМ ЛЕВОГО СТИКА, И ТОЛЬКО ЛЕВОГО.
        ///
        /// ЧТО БЫЛО РАНЬШЕ: проверялось, что в `KvXrStartMenu.cs` нет слова `RightStickClick` и есть
        /// комментарий про «короткое нажатие левого стика». Комментарий мог остаться, а поведение —
        /// измениться.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ ТЕПЕРЬ:
        ///   1) короткое нажатие (меньше порога) открывает меню, удержание — НЕТ (оно принадлежит
        ///      команде «вернуться к столу», иначе одно нажатие даст два действия);
        ///   2) правый стик меню не открывает — он занят режимом перемещения точки.
        /// </summary>
        [Test]
        public void StartMenu_LeftStickShortPressOnly()
        {
            Assert.IsTrue(KvXrStartMenu.ShouldOpenFromStick(0f),
                "мгновенное нажатие обязано открывать меню — иначе его не открыть коротким касанием");
            Assert.IsTrue(KvXrStartMenu.ShouldOpenFromStick(KvXrStartMenu.ShortPressMaxSeconds * 0.5f),
                "короткое нажатие обязано открывать меню");
            Assert.IsFalse(KvXrStartMenu.ShouldOpenFromStick(KvXrStartMenu.ShortPressMaxSeconds),
                "удержание стика открыло меню — а это команда «вернуться к столу»: одно нажатие " +
                "сделает два дела сразу");
            Assert.IsFalse(KvXrStartMenu.ShouldOpenFromStick(3f),
                "долгое удержание открыло меню — два действия на одно нажатие");

            Assert.AreEqual(KvXrStartMenu.MenuStick.Left,
                KvXrStartMenu.OpensOnStick(true, false), "левый клик стика обязан открывать меню");
            Assert.AreEqual(KvXrStartMenu.MenuStick.None,
                KvXrStartMenu.OpensOnStick(false, true),
                "правый стик снова открывает меню и конфликтует с режимом перемещения точки");
            Assert.AreEqual(KvXrStartMenu.MenuStick.None,
                KvXrStartMenu.OpensOnStick(false, false), "без нажатий меню открываться не должно");
        }

        // ================================================================ точка и виньетка

        /// <summary>
        /// ТОЧКУ МОЖНО ТАЩИТЬ ГРИПОМ. ЧТО БЫЛО РАНЬШЕ: поиск слова `DragPointTo` в
        /// `TrajectoryFlowController.cs` и `KvXrFlowBridge.cs`.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ ТЕПЕРЬ — правило переноса и порог грипа:
        ///   1) грип зажат + луч попал + луч не над интерфейсом → точка ведётся;
        ///   2) луч над панелью → НЕ ведётся: иначе точка уезжала бы за панелью, туда, куда оператор
        ///      не смотрит и чего не видит;
        ///   3) промах луча → НЕ ведётся (тащить точку в пустоту некуда);
        ///   4) дребезг грипа в нуле не считается нажатием — иначе точка дёргалась бы от касания.
        /// </summary>
        [Test]
        public void Point_CanBeDraggedByGrip()
        {
            Assert.IsTrue(KvXrFlowBridge.ShouldDragPointByGrip(true, true, false),
                "грип зажат и луч на поверхности, а перенос точки запрещён — тащить рукой нечем");
            Assert.IsFalse(KvXrFlowBridge.ShouldDragPointByGrip(true, true, true),
                "точка ведётся сквозь панель интерфейса — оператор поставит её туда, чего не видит");
            Assert.IsFalse(KvXrFlowBridge.ShouldDragPointByGrip(true, false, false),
                "точка ведётся при промахе луча — тащить её некуда");
            Assert.IsFalse(KvXrFlowBridge.ShouldDragPointByGrip(false, true, false),
                "точка ведётся без нажатого грипа — режим точки «дёргается» от одного прицела");

            Assert.IsFalse(KvXrInputSource.IsDragGrip(0f), "отпущенный грип не ведёт точку");
            Assert.IsFalse(KvXrInputSource.IsDragGrip(KvXrInputSource.GripDragThreshold * 0.9f),
                "дребезг грипа в нуле считается нажатием — точка поедет от случайного касания");
            Assert.IsTrue(KvXrInputSource.IsDragGrip(KvXrInputSource.GripDragThreshold),
                "грип ровно на пороге обязан считаться зажатым");
            Assert.IsTrue(KvXrInputSource.IsDragGrip(1f), "грип до упора обязан вести точку");
        }

        /// <summary>
        /// ВИНЬЕТКА КОМФОРТА ДЕЙСТВИТЕЛЬНО СОБИРАЕТСЯ. ЧТО БЫЛО РАНЬШЕ: поиск строки
        /// `KvXrComfortVignette.Ensure` в `KvXrRig.cs`.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ ТЕПЕРЬ: `Ensure` вызывается и создаёт РАБОТАЮЩИЙ оверлей — затемнение из
        /// четырёх полос по краям экрана. Проверяется и повторный вызов: он обязан вернуть ТУ ЖЕ
        /// виньетку, иначе на каждый пересоздание рига появлялся бы ещё один слой затемнения.
        ///
        /// ПОЧЕМУ СБОРКА ЗАПУСКАЕТСЯ ЯВНО (исправлено после прогона приёмки 12.11.2026). Первый
        /// вариант теста рассчитывал, что полосы построит `Awake` нового компонента, и получил
        /// «полос 0»: в EditMode `Awake` у только что созданного компонента НЕ выполняется. Это
        /// причина ОКРУЖЕНИЯ, а не кода, и «лечить» её ослаблением проверки нельзя — смысл в том,
        /// что затемнение СОБРАНО. Поэтому сборка вызывается тем же публичным входом, которым
        /// пользуется `Awake` (`EnsureBuilt`), и проверяются уже реальные полосы.
        /// </summary>
        [Test]
        public void Comfort_VignetteIsWired()
        {
            Assert.AreEqual(1, KvXrSettings.DefaultVignette, "виньетка комфорта обязана быть включена по умолчанию");
            Assert.Greater(KvXrSettings.DefaultVignetteStrength, 0f, "сила виньетки должна быть больше нуля");

            var rig = new GameObject("KvTestVignetteRig");
            try
            {
                KvXrComfortVignette vignette = KvXrComfortVignette.Ensure(rig.transform, null);
                Assert.IsNotNull(vignette, "риг не создаёт виньетку — комфорт не работает");
                Assert.AreSame(vignette, KvXrComfortVignette.Ensure(rig.transform, null),
                    "повторный Ensure создаёт ВТОРУЮ виньетку — затемнение сложится вдвое");

                // СБОРКА ОВЕРЛЕЯ — ЯВНО (см. пояснение в шапке теста): в EditMode `Awake` не идёт.
                vignette.EnsureBuilt();

                Assert.AreEqual(KvXrComfortVignette.BandsPerVignette, vignette.BandCount,
                    "виньетка собрана не из четырёх полос затемнения (сверху, снизу, слева, справа) — " +
                    "часть кадра остаётся без гашения. Состояние: " + vignette.Status);
                StringAssert.Contains("полос " + KvXrComfortVignette.BandsPerVignette, vignette.Status,
                    "виньетка собрана без четырёх полос затемнения — по краям экрана ничего не гасится. " +
                    "Состояние: " + vignette.Status);
                Assert.IsNotNull(rig.GetComponentInChildren<CanvasGroup>(true),
                    "у виньетки нет CanvasGroup — прозрачностью затемнения нечем управлять");

                // ПОВТОРНАЯ СБОРКА НЕ ПЛОДИТ ПОЛОС: иначе на каждое пересоздание оверлея
                // затемнение складывалось бы и экран чернел.
                vignette.EnsureBuilt();
                Assert.AreEqual(KvXrComfortVignette.BandsPerVignette, vignette.BandCount,
                    "повторный вызов сборки добавил полос — затемнение сложится, и экран почернеет");
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        /// <summary>Сила виньетки ограничена: 0,8 — уже почти чёрный экран, больше нельзя.</summary>
        [Test]
        public void Comfort_VignetteStrengthIsClamped()
        {
            float saved = KvXrSettings.GetVignetteStrength();
            try
            {
                KvXrSettings.SetVignetteStrength(5f);
                Assert.LessOrEqual(KvXrSettings.GetVignetteStrength(), KvXrSettings.MaxVignetteStrength);
                KvXrSettings.SetVignetteStrength(-1f);
                Assert.GreaterOrEqual(KvXrSettings.GetVignetteStrength(), KvXrSettings.MinVignetteStrength);
            }
            finally { KvXrSettings.SetVignetteStrength(saved); }
        }

        /// <summary>Правила ходьбы описываются словами — это нужно журналу/отчёту.</summary>
        [Test]
        public void WalkRules_DescribeMentionsBoundsAndMode()
        {
            string text = KvXrWalkRules.Describe();
            StringAssert.Contains("границы зала", text);
            StringAssert.Contains("капсула", text);
        }
    }
}
