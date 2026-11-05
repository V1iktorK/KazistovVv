using System.IO;
using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVv.Tests
{
    /// <summary>
    /// ВРАТА ПЛАНА VR-УПРАВЛЕНИЯ (05.11.2026, «все пункты»): перемещение по ангару, комфорт,
    /// наполнение кнопок. Тесты держат ЧИСТУЮ часть (без шлема) и контракт исходников: если
    /// кто-то удалит переключатель, коллизии, E-STOP, поиск или виньетку — тест скажет это
    /// раньше владельца.
    /// </summary>
    public class KvXrVrControlsTests
    {
        private static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        private static string Source(string relative)
        {
            string path = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", relative);
            Assert.IsTrue(File.Exists(path), "нет файла " + path);
            return File.ReadAllText(path);
        }

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

        /// <summary>Локомоция обязана использовать правила, а не «просто прибавлять смещение».</summary>
        [Test]
        public void Locomotion_UsesWalkRulesAndAimPose()
        {
            string source = Source("Xr\\KvXrLocomotion.cs");
            StringAssert.Contains("KvXrWalkRules.ClampToHangar", source,
                "плавный ход снова не ограничен залом — оператор пройдёт сквозь стену");
            StringAssert.Contains("KvXrWalkRules.ResolveObstacles", source,
                "в плавном ходе нет столкновений — столы/стенды не останавливают оператора");
            StringAssert.Contains("GetMoveAcceleration", source,
                "нет разгона/торможения — ход «дёргает»");
            StringAssert.Contains("TryRayAimPose", source,
                "телепорт снова целится позой кисти, а не указания");
        }

        // ================================================================ кнопки

        /// <summary>Удержания и Menu заполнены: E-STOP, поиск, смена хода, возврат к столу.</summary>
        [Test]
        public void Actions_HoldButtonsAreFilled()
        {
            string source = Source("Xr\\KvXrActions.cs");
            StringAssert.Contains("TriggerEStop", source, "удержание B не делает E-STOP");
            StringAssert.Contains("OpenCommandSearch", source, "Menu не открывает поиск команд");
            StringAssert.Contains("ToggleMoveMode", source, "удержание X не переключает способ хода");
            StringAssert.Contains("ReturnToWorkPosition", source, "удержание стика не возвращает к столу");
            StringAssert.Contains("RepositionMenu", source, "удержание Menu не переставляет меню");
            StringAssert.Contains("KvHapticsBridge.Pulse", source, "нажатия без тактильного отклика");
        }

        /// <summary>Кнопка Menu вообще перестала быть «мёртвой».</summary>
        [Test]
        public void Actions_MenuButtonIsUsed()
        {
            string source = Source("Xr\\KvXrActions.cs");
            StringAssert.Contains("KvXrInputSource.MenuButton", source,
                "Menu снова нигде не читается — кнопка мертва");
        }

        /// <summary>
        /// A/B/X/Y больше не дублируются как курок: `primaryButton` убран из списка курка,
        /// иначе на простом профиле одно нажатие давало два действия (класс E-08).
        /// </summary>
        [Test]
        public void Input_PrimaryButtonIsNotTrigger()
        {
            string source = Source("Xr\\KvXrInputSource.cs");
            StringAssert.DoesNotContain(
                "\"triggerPressed\", \"trigger\", \"triggerButton\", \"selectPressed\", \"select\", \"primaryButton\"",
                source, "primaryButton снова в списке курка — двойное действие на простом профиле");
            StringAssert.Contains("PrimaryButtonControlNames", source, "A/B/X/Y должны остаться");
        }

        /// <summary>Меню открывается левым стиком по отпусканию; правый занят режимом точки.</summary>
        [Test]
        public void StartMenu_LeftStickShortPressOnly()
        {
            string source = Source("Xr\\KvXrStartMenu.cs");
            StringAssert.DoesNotContain("RightStickClick", source,
                "правый стик снова открывает меню и конфликтует с режимом точки");
            StringAssert.Contains("коротким нажатием левого стика", source,
                "меню обязано открываться коротким нажатием левого стика");
        }

        // ================================================================ точка и виньетка

        /// <summary>Точку можно тащить грипом (режим точки), а не только стиком.</summary>
        [Test]
        public void Point_CanBeDraggedByGrip()
        {
            StringAssert.Contains("DragPointTo", Source("Trajectory\\TrajectoryFlowController.cs"),
                "в потоке нет переноса точки рукой");
            StringAssert.Contains("DragPointTo", Source("Xr\\KvXrFlowBridge.cs"),
                "мост шлема не передаёт перенос точки грипом");
        }

        /// <summary>Виньетка создаётся ригом и включена по умолчанию.</summary>
        [Test]
        public void Comfort_VignetteIsWired()
        {
            Assert.AreEqual(1, KvXrSettings.DefaultVignette, "виньетка комфорта обязана быть включена по умолчанию");
            Assert.Greater(KvXrSettings.DefaultVignetteStrength, 0f, "сила виньетки должна быть больше нуля");
            StringAssert.Contains("KvXrComfortVignette.Ensure", Source("Xr\\KvXrRig.cs"),
                "риг не создаёт виньетку — комфорт не работает");
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
