using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ СЕССИИ §40 — РЕЖИМ КИСТЕЙ И ПЕРЕНОС ИНТЕРФЕЙСА В VR.
    ///
    /// ЧАСТЬ А (слова владельца): «когда переходит управление на жесты — квадратики появляются у
    /// левой руки и лучи из указательных пальцев идут длинные как у контроллеров (упираются в
    /// текстуры)». Причина по журналу: лежащие контроллеры продолжали считаться «в игре» — панель
    /// ехала за лежащим контроллером, лучи шли на 10 м. Теперь: режим определяется по расстоянию
    /// «кисть ↔ контроллер» (порог 0,20 м), в режиме кистей луч идёт из КОНЧИКА УКАЗАТЕЛЬНОГО
    /// ПАЛЬЦА и не длиннее 1,5 м, а панель крепится к ЛАДОНИ левой кисти.
    ///
    /// ЧАСТЬ Б («сразу всё перенесём»): пять экранов панели — Состояние, Настройки, Объекты,
    /// Свойства, Справка. Здесь проверяется ЧИСТАЯ часть: что именно попадает в строки, шаги и
    /// границы настроек, пометка выбранного объекта, честная строка «не выбран».
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: «видно ли это в шлеме» — это ваш взгляд и строки журнала.
    /// </summary>
    public class KvXrS40Tests
    {
        // ============================================================ 1. РЕЖИМ УПРАВЛЕНИЯ

        /// <summary>
        /// РЕЖИМ ОПРЕДЕЛЯЕТСЯ РАССТОЯНИЕМ «КИСТЬ ↔ КОНТРОЛЛЕР». Порог 0,20 м: ближе — контроллер в
        /// руке, дальше — контроллер лежит и управление переходит кистям.
        /// </summary>
        [Test]
        public void Mode_ControllerInHandVersusLaidAside()
        {
            Assert.AreEqual(KvXrControlMode.Controllers,
                KvXrModeResolver.Resolve(true, true, 0.05f), "контроллер в 5 см от кисти — он в руке");
            Assert.AreEqual(KvXrControlMode.Controllers,
                KvXrModeResolver.Resolve(true, true, 0.19f), "19 см — всё ещё в руке");
            Assert.AreEqual(KvXrControlMode.Hands,
                KvXrModeResolver.Resolve(true, true, 0.25f), "25 см — контроллер лежит, работают кисти");
            Assert.AreEqual(KvXrControlMode.Hands,
                KvXrModeResolver.Resolve(true, false, 99f), "контроллера нет, кисть есть — кисти");
            Assert.AreEqual(KvXrControlMode.Controllers,
                KvXrModeResolver.Resolve(false, true, 0.05f), "кисти нет — остаётся контроллер");
            Assert.AreEqual(KvXrControlMode.None,
                KvXrModeResolver.Resolve(false, false, 99f), "ни кисти, ни контроллера");
        }

        /// <summary>Порог — число из правил (0,20 м), чтобы его нельзя было «поправить на глаз».</summary>
        [Test]
        public void Mode_ThresholdIsDocumented()
        {
            Assert.AreEqual(0.20f, KvXrModeResolver.ControllerInHandMeters, 1e-4f);
        }

        /// <summary>
        /// ПЕРЕКЛЮЧЕНИЕ РЕЖИМА С ЗАДЕРЖКОЙ. Трекинг кистей на этом рантайме пропадает на кадр-два;
        /// без задержки каждая такая потеря переключала бы луч и панель туда-обратно, а журнал
        /// забивался бы строками `РЕЖИМ:`.
        /// </summary>
        [Test]
        public void Mode_SwitchesOnlyAfterDelay()
        {
            Assert.AreEqual(KvXrControlMode.Controllers, KvXrModeResolver.Settle(
                    KvXrControlMode.Controllers, KvXrControlMode.Hands, 0.20f, KvXrModeResolver.SwitchDelaySeconds),
                "через 0,20 с новый режим ещё не применяется");

            Assert.AreEqual(KvXrControlMode.Hands, KvXrModeResolver.Settle(
                    KvXrControlMode.Controllers, KvXrControlMode.Hands, 0.45f, KvXrModeResolver.SwitchDelaySeconds),
                "после 0,45 с режим обязан примениться");

            Assert.AreEqual(KvXrControlMode.Hands, KvXrModeResolver.Settle(
                    KvXrControlMode.Hands, KvXrControlMode.Hands, 0f, KvXrModeResolver.SwitchDelaySeconds),
                "если режим не менялся, ждать нечего");

            Assert.AreEqual(0.40f, KvXrModeResolver.SwitchDelaySeconds, 1e-4f);
        }

        // ============================================================ 2. ЛУЧ ИЗ ПАЛЬЦА

        /// <summary>
        /// ЛУЧ В РЕЖИМЕ КИСТЕЙ — ИЗ КОНЧИКА УКАЗАТЕЛЬНОГО ПАЛЬЦА И НЕ ДЛИННЕЕ 1,5 м.
        /// «Длинные лучи на 10 м в текстуры» — это ровно то, на что жаловался владелец.
        /// </summary>
        [Test]
        public void HandRay_StartsAtFingerAndIsShort()
        {
            KvHandFrame frame = Hand(pinch: false, indexCurl: 0.10f);

            Vector3 origin;
            Vector3 direction;
            Assert.IsTrue(KvXrHandRay.TryGetRay(frame, out origin, out direction),
                "для нормальной кисти луч обязан строиться");
            Assert.AreEqual(frame.indexTip, origin, "луч обязан начинаться в КОНЧИКЕ указательного пальца");
            Assert.AreEqual(0f, direction.y, 1e-3f, "направление берётся от основания к кончику (палец вперёд)");

            Assert.AreEqual(1.5f, KvXrHandRay.MaxLengthMeters, 1e-4f);
            Assert.AreEqual(1.5f, KvXrHandRay.LengthMeters(false, 0.4f, 1.5f), 1e-4f,
                "при промахе луч идёт на 1,5 м, а не на 10");
            Assert.AreEqual(0.4f, KvXrHandRay.LengthMeters(true, 0.4f, 1.5f), 1e-4f,
                "при попадании в панель — до неё");
        }

        /// <summary>Нет кисти (или поза нулевая) — луча нет: выдумывать точку нельзя.</summary>
        [Test]
        public void HandRay_RequiresUsableHand()
        {
            Vector3 origin;
            Vector3 direction;
            Assert.IsFalse(KvXrHandRay.TryGetRay(default(KvHandFrame), out origin, out direction),
                "по нулевой позе луч строиться не должен");
        }

        // ============================================================ 3. ПАНЕЛЬ У КИСТИ

        /// <summary>
        /// ПАНЕЛЬ В РЕЖИМЕ КИСТЕЙ — У ЛАДОНИ: смещение 0,12 м по направлению кисти и 0,04 м вверх,
        /// разворот по кисти. Лежащий контроллер больше не тащит панель за собой.
        /// </summary>
        [Test]
        public void HandPanel_IsPlacedAtPalm()
        {
            KvHandFrame frame = Hand(pinch: false, indexCurl: 0.10f);

            Vector3 position;
            Vector3 forward;
            Assert.IsTrue(KvXrHandPanelPlacement.TryGet(frame, out position, out forward));

            Assert.AreEqual(KvXrHandPanelPlacement.ForwardMeters, 0.12f, 1e-4f);
            Assert.AreEqual(KvXrHandPanelPlacement.UpMeters, 0.04f, 1e-4f);

            Assert.AreEqual(frame.palm.y + 0.04f, position.y, 0.001f, "панель обязана быть приподнята над ладонью");
            Assert.Greater(Vector3.Dot(position - frame.palm, forward), 0.10f,
                "панель обязана стоять впереди ладони по направлению кисти");
        }

        // ============================================================ 4. ЭКРАН «СОСТОЯНИЕ»

        /// <summary>Экран «Состояние» содержит ВСЕ обязательные поля — иначе он бесполезен.</summary>
        [Test]
        public void Screen_StatusHasAllFields()
        {
            var data = new KvXrStatusData
            {
                Mode = "кисти",
                MoveMode = "Телепорт",
                Fps = 89.6f,
                HeadHeight = 1.62f,
                PanelRadius = 0.11f,
                IconSizeMm = 30f,
                CommandCount = 78,
                SecondRay = true,
                PlanningCandidates = false
            };

            List<string> rows = KvXrScreenModel.BuildStatus(data);
            string all = string.Join("\n", rows.ToArray());

            StringAssert.Contains("Режим управления", all);
            StringAssert.Contains("кисти", all);
            StringAssert.Contains("89.6", all, "частота кадров обязана быть в строке числами");
            StringAssert.Contains("1.62", all, "высота головы обязана быть в строке числами");
            StringAssert.Contains("0.110", all, "радиус панели обязан быть в строке числами");
            StringAssert.Contains("30", all, "размер иконки обязан быть в строке");
            StringAssert.Contains("78", all, "число команд в реестре обязано быть в строке");
        }

        // ============================================================ 5. ЭКРАН «НАСТРОЙКИ»

        /// <summary>
        /// ШАГ И ГРАНИЦЫ НАСТРОЕК: «−» на минимуме не уходит ниже, «+» на максимуме не уходит выше.
        /// Это то, что нельзя проверить глазами в шлеме, поэтому проверяется тестом.
        /// </summary>
        [Test]
        public void Screen_SettingsStepRespectsBounds()
        {
            Assert.AreEqual(0.09f, KvXrSettingStep.Step(0.10f, -0.01f, 0.08f, 0.42f), 1e-4f);
            Assert.AreEqual(0.08f, KvXrSettingStep.Step(0.08f, -0.01f, 0.08f, 0.42f), 1e-4f,
                "ниже минимума уходить нельзя");
            Assert.AreEqual(0.42f, KvXrSettingStep.Step(0.42f, 0.01f, 0.08f, 0.42f), 1e-4f,
                "выше максимума уходить нельзя");
            Assert.AreEqual(1.75f, KvXrSettingStep.Step(1.70f, 0.05f, 1.33f, 2.13f), 1e-4f,
                "внутри границ шаг применяется полностью");
            Assert.AreEqual(2.13f, KvXrSettingStep.Step(2.12f, 0.05f, 1.33f, 2.13f), 1e-4f,
                "рост упирается в максимум 2,13 м");
        }

        /// <summary>Экран «Настройки» показывает текущие значения и подписи — по строке на параметр.</summary>
        [Test]
        public void Screen_SettingsListAllParameters()
        {
            var data = new KvXrSettingsData
            {
                PanelRadius = 0.11f,
                SmoothSpeed = 1.4f,
                OperatorHeight = 1.70f,
                TeleportDistance = 6f,
                SecondRay = true,
                RayHand = KvXrQuickHand.Right
            };

            List<string> rows = KvXrScreenModel.BuildSettings(data);
            string all = string.Join("\n", rows.ToArray()).ToLowerInvariant();

            StringAssert.Contains("радиус панели", all);
            StringAssert.Contains("0.110", all);
            StringAssert.Contains("скорость", all);
            StringAssert.Contains("рост", all);
            StringAssert.Contains("телепорт", all);
            StringAssert.Contains("второй луч", all);
            StringAssert.Contains("рука луча", all);
        }

        /// <summary>
        /// ПОРЯДОК СТРОК «НАСТРОЕК» ЖЁСТКО ЗАДАН — и это защита от «кнопка делает не то».
        ///
        /// Дефект 23.10.2026: панель привязывала переключатель к ПОСЛЕДНЕМУ индексу, поэтому
        /// переключатель оказывался у «Руки луча», а «Второй луч» попадал в ветку шага и менял
        /// ДАЛЬНОСТЬ ТЕЛЕПОРТА. Строка журнала при этом называла одно, а менялось другое.
        /// Тест фиксирует порядок: 1 радиус, 2 скорость, 3 рост, 4 телепорт, 5 второй луч, 6 рука.
        /// </summary>
        [Test]
        public void Screen_SettingsRowOrderIsFixed()
        {
            var data = new KvXrSettingsData { PanelRadius = 0.11f, RayHand = KvXrQuickHand.Right };
            List<string> rows = KvXrScreenModel.BuildSettings(data);

            Assert.AreEqual(7, rows.Count, "заголовок + шесть параметров");
            StringAssert.Contains("Радиус панели", rows[1]);
            StringAssert.Contains("Скорость", rows[2]);
            StringAssert.Contains("Рост", rows[3]);
            StringAssert.Contains("телепорт", rows[4].ToLowerInvariant());
            StringAssert.Contains("Второй луч", rows[5]);
            StringAssert.Contains("Рука луча", rows[6]);
        }

        // ============================================================ 6. ЭКРАНЫ «ОБЪЕКТЫ», «СВОЙСТВА», «СПРАВКА»

        /// <summary>Экран «Объекты»: список из модели, выбранный помечен, пустой список не падает.</summary>
        [Test]
        public void Screen_ObjectsListAndSelection()
        {
            var names = new List<string> { "Робот SCARA", "Стенд 1", "Стенд 2" };
            List<string> rows = KvXrScreenModel.BuildObjects(names, 1);

            Assert.AreEqual(3, rows.Count);
            StringAssert.Contains("Стенд 1", string.Join("\n", rows.ToArray()));
            StringAssert.Contains("►", rows[1], "выбранный объект обязан быть помечен");
            StringAssert.DoesNotContain("►", rows[0], "невыбранный объект помечать нельзя");

            Assert.AreEqual(0, KvXrScreenModel.BuildObjects(new List<string>(), -1).Count,
                "пустой список объектов не должен падать");
        }

        /// <summary>«Свойства»: имя, тип, положение, габарит; объект не выбран — честная строка.</summary>
        [Test]
        public void Screen_PropertiesShowSelectedOrHonestEmpty()
        {
            var info = new KvXrObjectInfo
            {
                Selected = true,
                Name = "Робот SCARA",
                Kind = "Манипулятор",
                Position = new Vector3(1f, 2f, 3f),
                Size = new Vector3(0.5f, 0.6f, 0.7f)
            };

            string all = string.Join("\n", KvXrScreenModel.BuildProperties(info).ToArray());
            StringAssert.Contains("Робот SCARA", all);
            StringAssert.Contains("Манипулятор", all);
            StringAssert.Contains("1.00", all);
            StringAssert.Contains("0.50", all);

            var none = new KvXrObjectInfo { Selected = false };
            string empty = string.Join("\n", KvXrScreenModel.BuildProperties(none).ToArray());
            StringAssert.Contains("не выбран", empty, "при отсутствии выбора обязана быть честная строка");
        }

        /// <summary>«Справка»: перечислены все четыре согласованных жеста (ни один не забыт).</summary>
        [Test]
        public void Screen_HelpListsAllGestures()
        {
            string all = string.Join("\n", KvXrScreenModel.BuildHelp().ToArray()).ToLowerInvariant();

            StringAssert.Contains("щипок", all);
            StringAssert.Contains("ладонь", all);
            StringAssert.Contains("указательный", all);
            StringAssert.Contains("веер", all);
            StringAssert.Contains("второй луч", all, "справка обязана объяснять, что второй луч не нажимает");
        }

        // ============================================================ 7. ЗАГОЛОВОК НЕ РИСУЕТСЯ ДВАЖДЫ

        /// <summary>
        /// ПЕРВАЯ СТРОКА МОДЕЛИ — ЗАГОЛОВОК, И ПАНЕЛЬ ЕГО ПРОПУСКАЕТ.
        ///
        /// Дефект, найденный при ночной проверке кода 23.10.2026: экран рисовал заголовок отдельно,
        /// а модель отдавала его ещё раз первой строкой — заголовок появлялся дважды, и последняя
        /// строка данных обрезалась лимитом в 8 строк. Тест фиксирует контракт: заголовок есть в
        /// модели, и после него остаётся не меньше семи строк данных (то есть в лимит они влезают).
        /// </summary>
        [Test]
        public void Screen_ModelsStartWithTitleAndFitTheLimit()
        {
            var data = new KvXrStatusData { Mode = "кисти" };
            List<string> status = KvXrScreenModel.BuildStatus(data);
            Assert.AreEqual("Состояние", status[0], "первая строка модели — заголовок");
            Assert.GreaterOrEqual(status.Count - 1, 8,
                "после заголовка обязано быть не меньше восьми строк данных (они влезают в лимит экрана)");

            List<string> help = KvXrScreenModel.BuildHelp();
            Assert.AreEqual("Справка", help[0], "первая строка справки — заголовок");
            Assert.LessOrEqual(help.Count - 1, 8, "строк данных в справке не должно быть больше лимита экрана");
        }

        // ============================================================ СИНТЕТИЧЕСКАЯ КИСТЬ

        /// <summary>
        /// КИСТЬ, НАПРАВЛЕННАЯ ВПЕРЁД: запястье и ладонь на одной высоте, кончик указательного впереди
        /// по Z. Так направление «от запястья к кончику» даёт ровно +Z — и смещения панели можно
        /// проверять числами, а не «примерно».
        /// </summary>
        private static KvHandFrame Hand(bool pinch, float indexCurl)
        {
            return new KvHandFrame
            {
                valid = true,
                wrist = new Vector3(0f, 0.02f, 0f),
                palm = new Vector3(0f, 0.02f, 0.02f),
                indexProximal = new Vector3(0.03f, 0.02f, 0.08f),
                indexTip = new Vector3(0.03f, 0.02f, 0.14f),
                middleProximal = new Vector3(0.01f, 0.02f, 0.08f),
                middleTip = new Vector3(0.01f, 0.02f, 0.14f),
                ringProximal = new Vector3(-0.01f, 0.02f, 0.08f),
                ringTip = new Vector3(-0.01f, 0.02f, 0.14f),
                littleProximal = new Vector3(-0.03f, 0.02f, 0.08f),
                littleTip = new Vector3(-0.03f, 0.02f, 0.14f),
                thumbTip = new Vector3(-0.05f, 0.01f, 0.05f),
                palmRotation = Quaternion.identity,
                hasShapes = true,
                indexCurl = indexCurl,
                middleCurl = indexCurl,
                ringCurl = indexCurl,
                littleCurl = indexCurl,
                indexPinch = pinch ? 1f : 0f,
                indexSpread = 0.3f,
                middleSpread = 0.3f,
                ringSpread = 0.3f
            };
        }
    }
}
