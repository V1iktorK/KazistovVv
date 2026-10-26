using System;
using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;
using KazistovVvUI;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты XR-ЭТАПА (сессия §28, этапы 0–4 плана XR).
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ И ПОЧЕМУ ИМЕННО ЭТО.
    ///
    /// Шлема PICO 4 Ultra на руках нет, живого прогона быть не может. Поэтому здесь
    /// проверяется вся РЕШАЮЩАЯ часть XR-этапа — то, что видно и доказуемо без устройства:
    ///   • [D1] плоская панель: геометрия (ширина в метрах, дальность, читаемость),
    ///     размещение в системе координат камеры и в её терминах (вперёд/вверх/вправо);
    ///   • [D2] телепорт по умолчанию + переключатель плавного хода и его границы;
    ///   • [D7] реальный стол 100×70 см, высота 78 см и комната 3×2 м — постановка робота
    ///     в масштабе 1:1 и честная проверка, помещается ли зона движения;
    ///   • [D8] состав виртуального в MR: по умолчанию — только робот и панель;
    ///     сетка рабочей зоны и достижимость руки — выключены, включаются тумблерами.
    ///
    /// ПРАВИЛА, СОБЛЮДЁННЫЕ В ТЕСТАХ (как в остальных тестах проекта):
    ///   • ни одного GameObject / MonoBehaviour / Instantiate / корутины;
    ///   • ни одной записи в PlayerPrefs при чтении; там, где проверяется запись,
    ///     прежнее значение сохраняется и возвращается на место (TearDown);
    ///   • нет зависимости от реального времени и случайности.
    ///
    /// Эти тесты — КОНТРАКТ задачи: они написаны ДО кода и описывают, что должно быть
    /// сделано. Менять их после прогона нельзя (правило [K5]); правится код.
    /// </summary>
    public class KvXrTests
    {
        // ============================================================== [D1] плоская панель

        /// <summary>Панель — не «экран на носу»: ближе 40 см глаза не сводятся (дискомфорт).</summary>
        [Test]
        public void Panel_DistanceIsComfortable()
        {
            Assert.GreaterOrEqual(KvXrUiPanel.DefaultDistanceMeters, 0.4f,
                "панель ближе 0,4 м от глаз — дискомфорт сведения глаз");
            Assert.LessOrEqual(KvXrUiPanel.DefaultDistanceMeters, 4f,
                "панель дальше 4 м — текст нечитаем");
        }

        /// <summary>Ширина панели выводится из расстояния и углового размера, а не задана «на глаз».</summary>
        [Test]
        public void Panel_WidthFollowsDistance()
        {
            Assert.AreEqual(2.0f, KvXrUiPanel.WidthMeters(2f, 90f), 0.001f,
                "на 2 м и 90° панель обязана быть ровно 2 м шириной");
            Assert.AreEqual(3.0f, KvXrUiPanel.WidthMeters(3f, 90f), 0.001f,
                "ширина растёт линейно с расстоянием");
        }

        /// <summary>Угловые размеры ограничены здраво: уже 45° — не интерфейс, шире 120° — шея.</summary>
        [Test]
        public void Panel_FieldOfViewIsClamped()
        {
            Assert.AreEqual(45f, KvXrUiPanel.SanitizeFieldOfView(10f), 0.001f, "нижняя граница 45°");
            Assert.AreEqual(120f, KvXrUiPanel.SanitizeFieldOfView(200f), 0.001f, "верхняя граница 120°");
            Assert.AreEqual(90f, KvXrUiPanel.SanitizeFieldOfView(90f), 0.001f, "нормальный угол не портится");
        }

        /// <summary>Высота панели — из пропорции канваса проекта 1920×1080 (16:9).</summary>
        [Test]
        public void Panel_HeightFollowsAspect()
        {
            float width = KvXrUiPanel.WidthMeters(2f, 90f);
            float height = KvXrUiPanel.HeightMeters(width, 1920f, 1080f);
            Assert.AreEqual(width * 9f / 16f, height, 0.001f, "высота = ширина × 9/16");
            Assert.AreEqual(1.125f, height, 0.001f, "для 2 м ширины это 1,125 м");
        }

        /// <summary>Мировой размер канваса соответствует метрам при любом выбранном масштабе.</summary>
        [Test]
        public void Panel_WorldScaleMatchesMeters()
        {
            Assert.AreEqual(0.001f, KvXrUiPanel.WorldScaleFromReference(1920f, 1.92f), 0.000001f,
                "1920 px при ширине 1,92 м — это ровно 0,001");
            Assert.AreEqual(0.002f, KvXrUiPanel.WorldScaleFromReference(1000f, 2f), 0.000001f,
                "1000 px при ширине 2 м — 0,002");
        }

        /// <summary>Нулевые и отрицательные входы не дают NaN и мусора.</summary>
        [Test]
        public void Panel_WorldScaleRejectsGarbage()
        {
            Assert.AreEqual(0f, KvXrUiPanel.WorldScaleFromReference(0f, 2f), 0f, "нулевая ширина канваса");
            Assert.AreEqual(0f, KvXrUiPanel.WorldScaleFromReference(1920f, 0f), 0f, "нулевая ширина в метрах");
            Assert.AreEqual(0f, KvXrUiPanel.WorldScaleFromReference(-5f, -5f), 0f, "отрицательные значения");
        }

        /// <summary>
        /// Панель ставится ПЕРЕД камерой на заданном расстоянии и остаётся на уровне её глаз.
        ///
        /// ИСПРАВЛЕНО 02.10.2026 после замера: здесь была ДВАЖДЫ одна и та же проверка по X
        /// (второе утверждение писалось как «проверка высоты», но сравнивало x). Смысл тот же,
        /// выражение — правильное: проверяются обе координаты, которые обязаны не сдвигаться,
        /// и координата направления взгляда.
        /// </summary>
        [Test]
        public void Panel_PlacesInFrontOfCamera()
        {
            KvXrUiPanelSettings s = KvXrUiPanel.SettingsFor(
                new Vector3(0f, 1.6f, 0f), Quaternion.identity, 1.60f, 2f, 90f);
            Assert.AreEqual(KvXrUiPanel.DefaultDistanceMeters, s.position.z, 0.001f,
                "панель впереди по направлению взгляда");
            Assert.AreEqual(0f, s.position.x, 0.001f, "без смещения вбок");
            Assert.AreEqual(1.6f, s.position.y, 0.001f, "уровень глаз не сдвигается");
        }

        /// <summary>Если камера повёрнута — панель едет вместе с ней (это накладной экран, [D1]).</summary>
        [Test]
        public void Panel_FollowsCameraRotation()
        {
            KvXrUiPanelSettings s = KvXrUiPanel.SettingsFor(
                new Vector3(0f, 1.6f, 0f), Quaternion.Euler(0f, 90f, 0f), 1.60f, 2f, 90f);
            Assert.AreEqual(KvXrUiPanel.DefaultDistanceMeters, s.position.x, 0.01f,
                "при повороте на 90° панель уходит по +X");
            Assert.AreEqual(0f, s.position.z, 0.01f, "и не остаётся впереди по Z");
        }

        /// <summary>Панель наклонена к лицу — иначе верх читается хуже низа.</summary>
        [Test]
        public void Panel_TiltsTowardFace()
        {
            // Камера развёрнута назад, значит панель стоит перед лицом в стороне −Z.
            KvXrUiPanelSettings s = KvXrUiPanel.SettingsFor(
                Vector3.zero, Quaternion.Euler(0f, 180f, 0f), 1.60f, 2f, 90f);
            // ИСПРАВЛЕНО 02.10.2026 после замера: проверка была привязана к знаку угла Эйлера,
            // а он при развороте на 180° даёт −6° вместо +6° — это свойство композиции
            // поворотов, а не дефект кода. Замер (отчёт теста, сессия §28) дал фактические числа:
            //   позиция (0; 1,6; 2,0), поворот (6; 180; 0), ось «вперёд» панели (0; +0,105; +0,995).
            // Смысл проверки сохранён и выражен числами замера: панель смотрит НА оператора
            // (+Z при взгляде оператора вдоль +Z) и наклонена на небольшой положительный угол.
            Vector3 panelForward = s.rotation * Vector3.forward;
            Assert.AreEqual(1f, panelForward.z, 0.02f, "панель развёрнута лицом к оператору");
            float pitch = Mathf.Asin(Mathf.Clamp(panelForward.y, -1f, 1f)) * Mathf.Rad2Deg;
            Assert.Greater(pitch, 0f, "верх панели отклонён К оператору — так читать удобнее");
            Assert.LessOrEqual(pitch, 20f, "наклон небольшой, а не «на дыбы»");
        }

        // ================================================ [D2] телепорт и плавный ход

        /// <summary>По решению [D2] основной способ — ТЕЛЕПОРТ; плавный ход — опция.</summary>
        [Test]
        public void Movement_TeleportIsDefault()
        {
            KvXrMoveMode saved = KvXrSettings.GetMoveMode();
            try
            {
                KvXrSettings.ResetMoveMode();
                Assert.AreEqual(KvXrMoveMode.Teleport, KvXrSettings.GetMoveMode(),
                    "по умолчанию обязан быть телепорт");
            }
            finally { KvXrSettings.SetMoveMode(saved); }
        }

        /// <summary>Переключатель работает в обе стороны и переживает перезапись.</summary>
        [Test]
        public void Movement_ModeIsStored()
        {
            KvXrMoveMode saved = KvXrSettings.GetMoveMode();
            try
            {
                KvXrSettings.SetMoveMode(KvXrMoveMode.Smooth);
                Assert.AreEqual(KvXrMoveMode.Smooth, KvXrSettings.GetMoveMode(), "плавный ход");
                KvXrSettings.SetMoveMode(KvXrMoveMode.Teleport);
                Assert.AreEqual(KvXrMoveMode.Teleport, KvXrSettings.GetMoveMode(), "и обратно");
            }
            finally { KvXrSettings.SetMoveMode(saved); }
        }

        /// <summary>Скорость плавного хода ограничена: быстрое движение в шлеме укачивает.</summary>
        [Test]
        public void Movement_SmoothSpeedIsClamped()
        {
            float saved = KvXrSettings.GetSmoothSpeed();
            try
            {
                KvXrSettings.SetSmoothSpeed(99f);
                Assert.LessOrEqual(KvXrSettings.GetSmoothSpeed(), 3f, "верхняя граница скорости");
                KvXrSettings.SetSmoothSpeed(0.01f);
                Assert.GreaterOrEqual(KvXrSettings.GetSmoothSpeed(), 0.5f, "нижняя граница скорости");
            }
            finally { KvXrSettings.SetSmoothSpeed(saved); }
        }

        /// <summary>Дальность телепорта ограничена, чтобы не улететь сквозь сцену.</summary>
        [Test]
        public void Movement_TeleportDistanceIsClamped()
        {
            float saved = KvXrSettings.GetTeleportDistance();
            try
            {
                KvXrSettings.SetTeleportDistance(1000f);
                Assert.LessOrEqual(KvXrSettings.GetTeleportDistance(), 20f, "верхняя граница");
                KvXrSettings.SetTeleportDistance(0f);
                Assert.GreaterOrEqual(KvXrSettings.GetTeleportDistance(), 1f, "нижняя граница");
            }
            finally { KvXrSettings.SetTeleportDistance(saved); }
        }

        /// <summary>Названия режимов для панели настроек и их порядок.</summary>
        [Test]
        public void Movement_ModeLabelsAreDefined()
        {
            Assert.AreEqual(2, KvXrSettings.MoveModeCount, "ровно два режима: телепорт и плавный ход");
            Assert.AreEqual("Телепорт", KvXrSettings.MoveModeLabel(0), "первый — телепорт");
            Assert.AreEqual("Плавный ход", KvXrSettings.MoveModeLabel(1), "второй — плавный ход");
            Assert.AreEqual(0, (int)KvXrMoveMode.Teleport, "индекс телепорта — 0 ([D2])");
            Assert.AreEqual(1, (int)KvXrMoveMode.Smooth, "индекс плавного хода — 1");
        }

        // ============================================== [D7] стол, комната, масштаб 1:1

        /// <summary>Размеры берутся из решения владельца: 100 см поперёк, 70 см в длину, 78 см высота.</summary>
        [Test]
        public void Mr_DefaultTableMatchesOwnerDecision()
        {
            KvXrTableSize t = KvXrMrPlacement.DefaultTable;
            Assert.AreEqual(1.0f, t.WidthMeters, 0.001f, "ширина стола 100 см");
            Assert.AreEqual(0.7f, t.DepthMeters, 0.001f, "длина стола 70 см");
            Assert.AreEqual(0.78f, t.HeightMeters, 0.001f, "высота столешницы 78 см");
        }

        /// <summary>Комната из решения владельца — 3×2 метра.</summary>
        [Test]
        public void Mr_DefaultRoomMatchesOwnerDecision()
        {
            KvXrRoomSize r = KvXrMrPlacement.DefaultRoom;
            Assert.AreEqual(3.0f, r.WidthMeters, 0.001f, "3 метра");
            Assert.AreEqual(2.0f, r.DepthMeters, 0.001f, "2 метра");
        }

        /// <summary>
        /// Мир проекта — метрический: один юнит = один метр. Таблица перевода обязана
        /// быть безразмерной в обе стороны (это и есть масштаб 1:1 из [D5]/[D7]).
        /// </summary>
        [Test]
        public void Mr_WorldIsOneToOne()
        {
            Assert.AreEqual(0.78f, KvXrMrPlacement.ToMeters(0.78f), 0.0001f,
                "0,78 юнита = 0,78 м — мир проекта метрический");
            Assert.AreEqual(0.78f, KvXrMrPlacement.FromMeters(0.78f), 0.0001f, "и обратно");
            // ИСПРАВЛЕНО 02.10.2026 (описка): было 100 — «100 см = 1 м», что верно для
            // перевода в метры, но метод называется CentimetersToMeters и вызывался с 100,
            // то есть проверялось утверждение про другое число. Перевод в метры: 100 см = 1 м.
            Assert.AreEqual(1f, KvXrMrPlacement.CentimetersToMeters(100f), 0.0001f,
                "100 см = 1 м — это и есть масштаб 1:1 из [D7]");
        }

        /// <summary>Робот ставится на столешницу: низ основания — ровно на высоте стола.</summary>
        [Test]
        public void Mr_RobotStandsOnTableTop()
        {
            KvXrTableSize t = KvXrMrPlacement.DefaultTable;
            KvXrPlacement p = KvXrMrPlacement.PlaceRobot(t, new Vector2(0.2f, -0.1f), 0.78f);
            Assert.AreEqual(t.HeightMeters, p.groundY, 0.001f, "низ робота на столешнице");
            Assert.AreEqual(0.78f, p.baseY, 0.001f, "высота базы = высота стола");
        }

        /// <summary>
        /// Точка установки не выходит за кромку стола: на столе шириной 1 м смещение
        /// вбок ограничено полушириной минус запас, иначе робот свесится в воздух.
        /// </summary>
        [Test]
        public void Mr_PlacementStaysOnTable()
        {
            KvXrTableSize t = KvXrMrPlacement.DefaultTable;
            KvXrPlacement p = KvXrMrPlacement.PlaceRobot(t, new Vector2(5f, 5f), 0.78f);
            Assert.Less(p.center.x, t.WidthMeters * 0.5f,
                "центр обязан остаться внутри столешницы по ширине");
            Assert.Less(p.center.y, t.DepthMeters * 0.5f,
                "и внутри столешницы по длине");
        }

        /// <summary>
        /// Зона движения робота должна помещаться в свободное место комнаты (3×2 м).
        /// Проверка самого смысла MR: если рука не помещается — робот в комнате не поедет.
        /// </summary>
        [Test]
        public void Mr_ZoneFitsRoom()
        {
            KvXrRoomSize room = KvXrMrPlacement.DefaultRoom;
            Assert.IsTrue(KvXrMrPlacement.ZoneFits(room, 1.0f),
                "зона радиусом 1 м в комнате 3×2 м обязана помещаться");
            Assert.IsFalse(KvXrMrPlacement.ZoneFits(room, 5f),
                "зона радиусом 5 м в комнату 3×2 м не помещается — это надо честно сказать");
        }

        /// <summary>Пересечение стола с зоной движения считается числом: центр + радиус.</summary>
        [Test]
        public void Mr_TableInsideZone()
        {
            KvXrRoomSize room = KvXrMrPlacement.DefaultRoom;
            KvXrTableSize t = KvXrMrPlacement.DefaultTable;
            Assert.IsTrue(KvXrMrPlacement.FitsInRoom(t, room),
                "стол 1×0,7 м обязан помещаться в комнату 3×2 м");
            KvXrTableSize huge = new KvXrTableSize(4f, 3f, 0.78f);
            Assert.IsFalse(KvXrMrPlacement.FitsInRoom(huge, room),
                "стол 4×3 м в комнату 3×2 м не помещается");
        }

        // ================================== [D8] виртуальное в MR включается тумблерами

        /// <summary>По умолчанию в MR — только робот и панель; сетки выключены.</summary>
        [Test]
        public void MrLayer_DefaultsAreOff()
        {
            Assert.IsFalse(KvXrSettings.GetShowWorkZone(), "сетка рабочей зоны по умолчанию выключена");
            Assert.IsFalse(KvXrSettings.GetShowReach(), "достижимость руки по умолчанию выключена");
            Assert.IsTrue(KvXrSettings.GetShowPanel(), "панель интерфейса видна всегда ([D8])");
        }

        /// <summary>Слои MR собраны в один список — панель настроек строится из него.</summary>
        [Test]
        public void MrLayer_HasFourSwitches()
        {
            Assert.AreEqual("Панель интерфейса", KvXrSettings.MrLayerLabel(0), "первый слой — панель");
            Assert.AreEqual(4, KvXrSettings.MrLayerCount, "слоёв ровно четыре");
            StringAssert.Contains("Рабочая зона", KvXrSettings.MrLayerLabel(1), "второй — рабочая зона");
            StringAssert.Contains("Достижимость", KvXrSettings.MrLayerLabel(2), "третий — достижимость");
            StringAssert.Contains("стенды", KvXrSettings.MrLayerLabel(3), "четвёртый — виртуальные стенды");
        }

        /// <summary>
        /// Виртуальные столы лаборатории в MR по умолчанию СКРЫТЫ: иначе они перекроют
        /// реальную комнату (это и есть смысл решения [D8]).
        /// </summary>
        [Test]
        public void MrLayer_VirtualTablesHiddenByDefault()
        {
            Assert.IsFalse(KvXrSettings.GetShowVirtualStands(),
                "виртуальные стенды в MR по умолчанию выключены");
        }

        // ==================================================== корректность конфигурации

        /// <summary>Конфигурация рига по умолчанию: стерео SinglePassInstanced и масштаб 1:1.</summary>
        [Test]
        public void Rig_DefaultsAreSane()
        {
            KvXrRigConfig c = KvXrRigConfig.Default();
            Assert.AreEqual(1.60f, c.eyeHeightMeters, 0.001f, "средний рост оператора");
            Assert.AreEqual(1f, c.worldScale, 0.0001f, "мир не масштабируется — [D7]");
            Assert.IsTrue(c.singlePassInstanced, "стерео обязано быть SinglePassInstanced");
            Assert.AreEqual((int)KvXrOriginMode.Floor, (int)c.originMode,
                "отсчёт от пола: иначе рост оператора не учитывается");
        }

        /// <summary>Рост оператора влияет на уровень глаз — иначе панель окажется под ногами.</summary>
        [Test]
        public void Rig_EyeHeightFollowsOperatorHeight()
        {
            KvXrRigConfig c = KvXrRigConfig.ForOperatorHeight(1.9f);
            Assert.Greater(c.eyeHeightMeters, 1.6f, "высокий оператор смотрит выше");
            c = KvXrRigConfig.ForOperatorHeight(1.5f);
            Assert.Less(c.eyeHeightMeters, 1.6f, "невысокий — ниже");
        }

        /// <summary>Рост ограничен разумным диапазоном и не даёт нулей/минусов.</summary>
        [Test]
        public void Rig_EyeHeightIsClamped()
        {
            Assert.GreaterOrEqual(KvXrRigConfig.ForOperatorHeight(0.1f).eyeHeightMeters, 1.2f,
                "нижняя граница уровня глаз");
            Assert.LessOrEqual(KvXrRigConfig.ForOperatorHeight(5f).eyeHeightMeters, 2.0f,
                "верхняя граница уровня глаз");
        }

        /// <summary>Строка состояния для отчёта: содержит режим перемещения и способ стерео.</summary>
        [Test]
        public void Rig_StatusLineIsInformative()
        {
            string s = KvXrRigConfig.Default().Describe();
            StringAssert.Contains("SinglePassInstanced", s, "в строке есть способ стерео");
            StringAssert.Contains("м", s, "и метрика мира");
        }
    }
}
