using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;
using KazistovVvUI;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты ПАНЕЛИ С ОБОБЩАЮЩИМИ ИКОНКАМИ ГРУПП (сессия §35, карточка Б19).
    ///
    /// ЗАДАНИЕ ВЛАДЕЛЬЦА. «Панель инструментов должна быть у руки (у контроллера), а не висеть
    /// в середине экрана. Основные кнопки — ОБОБЩАЮЩИЕ ИКОНКИ групп (не текст). Текст появляется
    /// только после наведения и удержания фокуса 0,5 секунды. После нажатия на кнопку группы —
    /// полумесяцем раскрываются ВСЕ иконки этой группы, без названий, одними иконками.»
    ///
    /// ЧТО ЗДЕСЬ ПРОВЕРЯЕТСЯ (числа, без шлема и без сцены):
    ///   • веер вмещает ЛЮБУЮ группу проекта целиком — иначе часть команд пропала бы из шлема молча;
    ///   • подпись появляется не раньше 0,5 с и ТОЛЬКО на уровне групп: в веере подписей нет;
    ///   • иконки не налезают друг на друга даже при 14 иконках в веере;
    ///   • луч, не наведённый ни на одну иконку, не нажимает «первую попавшуюся»;
    ///   • запасное положение панели — У ПОЯСА, вне центрального поля зрения (владелец запретил
    ///     показ перед глазами прямо, но и «панели нет вовсе» не годится).
    /// </summary>
    public class KvXrGroupPanelTests
    {
        /// <summary>
        /// ВЕЕР ВМЕЩАЕТ ЛЮБУЮ ГРУППУ ЦЕЛИКОМ. Это главная проверка панели: владелец потребовал
        /// раскрывать ВСЕ иконки группы, а в самой большой группе проекта 14 команд.
        /// Если этот тест упадёт — часть команд исчезнет из шлема без единой ошибки в журнале.
        /// </summary>
        [Test]
        public void Fan_CoversEveryGroupInProject()
        {
            KvToolbarGroup[] groups = KvToolbarGroups.All;

            Assert.Greater(groups.Length, 0, "каталог групп проекта не должен быть пустым");
            Assert.AreEqual(KvXrGroupPanelLayout.GroupCount, groups.Length,
                "панель настроена на 8 групп — ровно столько в каталоге KvToolbarGroups");

            foreach (KvToolbarGroup group in groups)
            {
                int count = group.Commands != null ? group.Commands.Length : 0;
                Assert.LessOrEqual(count, KvXrGroupPanelLayout.MaxFanIcons,
                    "группа «" + group.Title + "» содержит " + count + " команд, а веер вмещает " +
                    KvXrGroupPanelLayout.MaxFanIcons + " — лишние команды пропали бы из шлема молча");
                Assert.IsTrue(KvXrGroupPanelLayout.IsValidFanCount(count),
                    "число иконок группы «" + group.Title + "» обязано быть допустимым для веера");
            }
        }

        /// <summary>У каждой группы обязана быть своя иконка — иначе кнопка группы будет пустой.</summary>
        [Test]
        public void EveryGroup_HasAnIcon()
        {
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
                Assert.IsFalse(string.IsNullOrEmpty(group.Icon),
                    "у группы «" + group.Title + "» нет иконки: кнопка группы окажется пустой, " +
                    "а владелец просил именно обобщающие ИКОНКИ групп");
        }

        /// <summary>
        /// ПОДПИСЬ — НЕ РАНЬШЕ 0,5 СЕКУНДЫ. Владелец: «текст появляется только после наведения
        /// и удержания фокуса 0,5 секунды». Подпись сразу вернула бы панель к тексту, который запрещён.
        /// </summary>
        [Test]
        public void Label_AppearsOnlyAfterHalfSecond()
        {
            Assert.AreEqual(0.5f, KvXrGroupPanelLayout.LabelDwellSeconds, 0.0001f,
                "порог подписи — ровно 0,5 с по решению владельца");

            Assert.IsFalse(KvXrGroupPanelLayout.ShowsLabel(KvXrPanelLevel.Groups, 0.49f),
                "за 0,01 с до порога подпись показывать рано");
            Assert.IsTrue(KvXrGroupPanelLayout.ShowsLabel(KvXrPanelLevel.Groups, 0.5f),
                "на пороге 0,5 с подпись обязана появиться");
        }

        /// <summary>
        /// ВЕЕР ПОКАЗЫВАЕТ ПОДПИСЬ ПО УДЕРЖАНИЮ — ИЗМЕНЕНО 21.10.2026 С РАЗРЕШЕНИЯ ВЛАДЕЛЬЦА.
        ///
        /// ЧТО БЫЛО И ПОЧЕМУ МЕНЯЕТСЯ. Прежняя проверка (`FanLevel_NeverShowsLabels`) требовала, чтобы
        /// в веере подписей не было НИКОГДА — так было решено в §35 («раскрываются все иконки, без
        /// названий»). На живом шлеме владелец увидел обратную сторону этого правила и сказал:
        /// «при наведении на иконки второго слоя не появляется название выполняемой функции» — то есть
        /// он не знает, что нажмёт. Разрешение на изменение ЭТОГО ОДНОГО теста получено 21.10.2026.
        ///
        /// ТЕПЕРЬ: подпись есть на обоих уровнях, и порог у неё тот же — 0,5 с удержания.
        /// </summary>
        [Test]
        public void FanLevel_ShowsLabelAfterDwell()
        {
            Assert.IsFalse(KvXrGroupPanelLayout.ShowsLabel(KvXrPanelLevel.GroupFan, 0.49f),
                "в веере до порога удержания подписи быть не должно");
            Assert.IsTrue(KvXrGroupPanelLayout.ShowsLabel(KvXrPanelLevel.GroupFan, 0.5f),
                "в веере на пороге 0,5 с подпись обязана появиться (просьба владельца)");
            Assert.IsTrue(KvXrGroupPanelLayout.ShowsLabel(KvXrPanelLevel.Groups, 0.5f),
                "на уровне групп поведение не изменилось");
        }

        /// <summary>Накопление времени удержания упирается в порог и не растёт бесконечно.</summary>
        [Test]
        public void LabelDwell_AccumulatesAndStops()
        {
            float progress = 0f;
            for (int i = 0; i < 200; i++)
                progress = KvXrGroupPanelLayout.AdvanceLabelDwell(progress, 1f / 90f);

            Assert.AreEqual(KvXrGroupPanelLayout.LabelDwellSeconds, progress, 0.0001f,
                "накопленное удержание обязано упираться в порог подписи");
        }

        /// <summary>
        /// ДУГА СИММЕТРИЧНА, середина — сверху. Иначе полумесяц «съедет» вбок от ладони
        /// и часть иконок окажется вне поля зрения.
        /// </summary>
        [Test]
        public void Crescent_IsSymmetricAroundTop()
        {
            for (int n = 2; n <= KvXrGroupPanelLayout.MaxFanIcons; n++)
            {
                float first = KvXrGroupPanelLayout.AngleDegrees(0, n, KvXrGroupPanelLayout.FanSpanDegrees);
                float last = KvXrGroupPanelLayout.AngleDegrees(n - 1, n, KvXrGroupPanelLayout.FanSpanDegrees);

                Assert.AreEqual(180f, first + last, 0.01f,
                    "дуга обязана быть симметричной относительно вертикали при " + n + " иконках");
                Assert.AreEqual(KvXrGroupPanelLayout.UpDegrees, (first + last) * 0.5f, 0.01f,
                    "середина дуги обязана быть направлена ВВЕРХ при " + n + " иконках");
            }
        }

        /// <summary>
        /// ИКОНКИ НЕ НАЛЕЗАЮТ ДРУГ НА ДРУГА. Радиус обязан расти с числом иконок; иначе
        /// 14 иконок «Визуализации» слиплись бы в неразличимый комок.
        /// </summary>
        [Test]
        public void Fan_IconsDoNotOverlap()
        {
            float spanRad = KvXrGroupPanelLayout.FanSpanDegrees * Mathf.Deg2Rad;
            float needed = KvXrGroupPanelLayout.IconSizeMeters + KvXrGroupPanelLayout.MinGapMeters;

            for (int n = 2; n <= KvXrGroupPanelLayout.MaxFanIcons; n++)
            {
                float radius = KvXrGroupPanelLayout.RadiusForCount(n, KvXrGroupPanelLayout.FanSpanDegrees);
                float spacing = radius * spanRad / (n - 1);      // шаг по дуге между соседними иконками

                Assert.GreaterOrEqual(spacing, needed - 0.0001f,
                    "при " + n + " иконках шаг по дуге " + spacing.ToString("0.000") +
                    " м меньше, чем иконка с просветом " + needed.ToString("0.000") + " м — иконки слипнутся");
            }
        }

        /// <summary>Радиус никогда не выходит за разумные границы: ни в точку, ни за пределы руки.</summary>
        [Test]
        public void Fan_RadiusStaysWithinBounds()
        {
            for (int n = 1; n <= KvXrGroupPanelLayout.MaxFanIcons; n++)
            {
                float radius = KvXrGroupPanelLayout.RadiusForCount(n, KvXrGroupPanelLayout.FanSpanDegrees);
                Assert.GreaterOrEqual(radius, KvXrGroupPanelLayout.MinRadiusMeters - 0.0001f,
                    "радиус не может быть меньше допустимого");
                Assert.LessOrEqual(radius, KvXrGroupPanelLayout.MaxRadiusMeters + 0.0001f,
                    "радиус не может быть больше допустимого — иначе за иконками придётся тянуться");
            }
        }

        /// <summary>Число иконок веера ограничено сверху и снизу, и границы защищены от нуля.</summary>
        [Test]
        public void FanCount_IsClamped()
        {
            Assert.AreEqual(KvXrGroupPanelLayout.MinFanIcons, KvXrGroupPanelLayout.ClampFanCount(0),
                "ноль иконок — это не веер; значение обязано подниматься до минимума");
            Assert.AreEqual(KvXrGroupPanelLayout.MinFanIcons, KvXrGroupPanelLayout.ClampFanCount(-7),
                "отрицательное число иконок обязано подниматься до минимума, а не падать");
            Assert.AreEqual(KvXrGroupPanelLayout.MaxFanIcons, KvXrGroupPanelLayout.ClampFanCount(999),
                "число иконок обязано упираться в предел");
            Assert.IsTrue(KvXrGroupPanelLayout.IsValidFanCount(KvXrGroupPanelLayout.MaxFanIcons),
                "предельное число иконок обязано считаться допустимым");
            Assert.IsFalse(KvXrGroupPanelLayout.IsValidFanCount(KvXrGroupPanelLayout.MaxFanIcons + 1),
                "число иконок выше предела допустимым не является");
        }

        /// <summary>Раскладка уровня 0 обязана содержать ровно столько иконок, сколько групп.</summary>
        [Test]
        public void GroupLevel_HasOneIconPerGroup()
        {
            Vector2[] offsets = KvXrGroupPanelLayout.GroupOffsets(KvXrQuickHand.Left);

            Assert.AreEqual(KvXrGroupPanelLayout.GroupCount, offsets.Length,
                "на уровне 0 обязана быть одна обобщающая иконка на каждую группу");
        }

        /// <summary>Веер содержит РОВНО столько иконок, сколько команд в группе.</summary>
        [Test]
        public void FanLevel_HasOneIconPerCommand()
        {
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
            {
                int commands = group.Commands != null ? group.Commands.Length : 0;
                Vector2[] offsets = KvXrGroupPanelLayout.FanOffsets(commands, KvXrQuickHand.Right);

                Assert.AreEqual(commands, offsets.Length,
                    "в веере группы «" + group.Title + "» обязана быть иконка на каждую команду");
            }
        }

        /// <summary>
        /// ЛУЧ НЕ НАВЁЛ НИ НА ЧТО — НЕ НАЖИМАТЬ НИЧЕГО. Возврат «нулевой иконки» вместо −1
        /// приводил бы к нажатию первой иконки «в пустоту», и владелец увидел бы, что панель
        /// срабатывает сама.
        /// </summary>
        [Test]
        public void ClosestIndex_ReturnsMinusOneWhenNothingAimed()
        {
            Vector2[] offsets = KvXrGroupPanelLayout.FanOffsets(8, KvXrQuickHand.Left);

            int index = KvXrGroupPanelLayout.ClosestIndex(new Vector2(9f, 9f), offsets,
                KvXrGroupPanelLayout.HitRadiusMeters);

            Assert.AreEqual(-1, index,
                "промах луча обязан давать −1, а не «первую попавшуюся» иконку");
        }

        /// <summary>Луч, наведённый на иконку, обязан выбирать ИМЕННО её, а не соседнюю.</summary>
        [Test]
        public void ClosestIndex_PicksTheAimedIcon()
        {
            Vector2[] offsets = KvXrGroupPanelLayout.FanOffsets(6, KvXrQuickHand.Left);

            for (int i = 0; i < offsets.Length; i++)
            {
                int found = KvXrGroupPanelLayout.ClosestIndex(offsets[i], offsets,
                    KvXrGroupPanelLayout.HitRadiusMeters);
                Assert.AreEqual(i, found,
                    "наведение точно на иконку " + i + " обязано выбирать её же");
            }
        }

        /// <summary>Пустой список иконок не должен ронять выбор: −1 и ничего больше.</summary>
        [Test]
        public void ClosestIndex_HandlesEmptyAndNull()
        {
            Assert.AreEqual(-1, KvXrGroupPanelLayout.ClosestIndex(Vector2.zero, null, 0.1f),
                "пустой список не должен приводить к исключению");
            Assert.AreEqual(-1, KvXrGroupPanelLayout.ClosestIndex(Vector2.zero, new Vector2[0], 0.1f),
                "список без иконок не должен выбирать ничего");
        }

        /// <summary>
        /// ПАНЕЛЬ У ПОЯСА, А НЕ ПЕРЕД ГЛАЗАМИ. Владелец запретил показ перед глазами прямо;
        /// но и «панели нет вовсе» не годится — тогда её не найти за 5 минут.
        /// </summary>
        [Test]
        public void BeltFallback_IsBelowAndInFrontOfBody()
        {
            Vector3 waist = new Vector3(0f, 1.00f, 0f);
            Vector3 position = KvXrGroupPanelLayout.BeltPosition(waist, Vector3.forward);

            Assert.Less(position.y, waist.y, "панель у пояса обязана быть НИЖЕ пояса");
            Assert.Greater(position.z, waist.z, "панель у пояса обязана быть ВПЕРЕДИ тела, иначе её не видно");
        }

        /// <summary>Панель у пояса обязана быть ВНЕ центрального поля зрения — это выражено числом.</summary>
        [Test]
        public void BeltFallback_IsOutsideCentralView()
        {
            float angle = KvXrGroupPanelLayout.BeltAngleFromGazeDegrees();

            Assert.GreaterOrEqual(angle, KvXrGroupPanelLayout.BeltMinAngleFromGazeDegrees,
                "угол " + angle.ToString("0.0") + "° от взгляда слишком мал: панель висела бы " +
                "в середине экрана, а это владелец запретил прямо");
            Assert.Less(angle, 90f,
                "панель обязана быть впереди тела, а не строго под ногами");
        }

        /// <summary>
        /// ПАНЕЛЬ ОБЯЗАНА БЫТЬ РАЗВЁРНУТА НА ОПЕРАТОРА, А НЕ ПО ЕГО ВЗГЛЯДУ.
        ///
        /// ЭТО И БЫЛА ПРИЧИНА «Пояса нет» (владелец, 16.10.2026). Панель у пояса разворачивалась
        /// вдоль направления взгляда, то есть ЛИЦОМ ОТ оператора: у мирового канваса видна
        /// передняя сторона, и владелец смотрел на «спину» панели.
        ///
        /// Проверка выражена знаком скалярного произведения: направление ВЗГЛЯДА уводит от панели,
        /// а направление НА оператора — к ней. Развернуть панель по взгляду значит отвернуть её.
        /// </summary>
        [Test]
        public void BeltPanel_MustFaceOperatorNotGazeDirection()
        {
            Vector3 eye = new Vector3(0f, 1.60f, 0f);
            Vector3 waist = new Vector3(0f, 1.30f, 0f);
            Vector3 gaze = Vector3.forward;

            Vector3 panel = KvXrGroupPanelLayout.BeltPosition(waist, gaze);

            Vector3 towardOperator = KvXrGroupPanelLayout.Flatten(eye - panel);   // КАК НАДО
            Vector3 alongGaze = KvXrGroupPanelLayout.Flatten(gaze);               // КАК БЫЛО (дефект)

            float dotCorrect = Vector3.Dot(towardOperator, KvXrGroupPanelLayout.Flatten(eye - panel));
            float dotWrong = Vector3.Dot(alongGaze, KvXrGroupPanelLayout.Flatten(eye - panel));

            Assert.Greater(dotCorrect, 0.9f,
                "панель обязана смотреть НА оператора: иначе он видит её обратную сторону");
            Assert.Less(dotWrong, -0.9f,
                "разворот панели ПО ВЗГЛЯДУ отворачивает её от оператора — это и был дефект «Пояса нет»");
        }

        /// <summary>
        /// ПАНЕЛЬ У ПОЯСА — НА ЧЕЛОВЕЧЕСКОЙ ВЫСОТЕ. Прежние значения давали 0,58 м (под подбородком,
        /// у самых ног, 81° вниз) — такую панель не находит никто. Проверяется число, а не слова.
        /// </summary>
        [Test]
        public void BeltPanel_SitsAtAReadableHeightAndDistance()
        {
            Vector3 eye = new Vector3(0f, 1.60f, 0f);
            Vector3 waist = new Vector3(0f, 1.30f, 0f);

            Vector3 panel = KvXrGroupPanelLayout.BeltPosition(waist, Vector3.forward);

            Assert.Greater(panel.y, 0.90f, "панель не должна висеть у самых ног");
            Assert.Less(panel.y, 1.30f, "панель не должна оказаться на уровне глаз");

            float ahead = KvXrGroupPanelLayout.Flatten(panel - eye).magnitude;
            Assert.Greater(ahead, 0.30f,
                "панель не должна быть вплотную к телу: при 16 см она была практически внутри оператора");

            float drop = eye.y - panel.y;
            float angle = KvXrGroupPanelLayout.AngleFromGazeDegrees(drop, ahead);
            Assert.GreaterOrEqual(angle, KvXrGroupPanelLayout.BeltMinAngleFromGazeDegrees,
                "панель обязана остаться вне центрального поля зрения — " + angle.ToString("0") + "° мало");
            Assert.Less(angle, 75f,
                "панель не должна уходить почти строго под ноги: " + angle.ToString("0") + "° — это " +
                "под подбородком, и её не найти (дефект «Пояса нет»)");
        }

        /// <summary>Панель у пояса не должна наклоняться вслед за поднятой головой.</summary>
        [Test]
        public void Flatten_RemovesTiltFromLookDirection()
        {
            Vector3 tilted = new Vector3(0.3f, 0.9f, 0.4f);       // взгляд вверх и вбок
            Vector3 flat = KvXrGroupPanelLayout.Flatten(tilted);

            Assert.AreEqual(0f, flat.y, 0.0001f, "направление «вперёд» обязано быть горизонтальным");
            Assert.AreEqual(1f, flat.magnitude, 0.0001f, "направление обязано остаться единичным");
        }

        /// <summary>Взгляд строго вниз не должен «схлопывать» направление в ноль.</summary>
        [Test]
        public void Flatten_SurvivesStraightDownGaze()
        {
            Vector3 flat = KvXrGroupPanelLayout.Flatten(new Vector3(0f, -1f, 0f));

            Assert.AreEqual(1f, flat.magnitude, 0.0001f,
                "при взгляде строго вниз направление обязано стать запасным «вперёд», а не нулём");
        }

        /// <summary>Правая рука зеркалит полумесяц: иначе он уходил бы в сторону от ладони.</summary>
        [Test]
        public void RightHand_MirrorsCrescentHorizontally()
        {
            Vector2[] left = KvXrGroupPanelLayout.FanOffsets(5, KvXrQuickHand.Left);
            Vector2[] right = KvXrGroupPanelLayout.FanOffsets(5, KvXrQuickHand.Right);

            Assert.AreEqual(left.Length, right.Length, "число иконок не зависит от руки");
            for (int i = 0; i < left.Length; i++)
            {
                Assert.AreEqual(-left[i].x, right[i].x, 0.0001f,
                    "правая рука обязана зеркалить панель по горизонтали");
                Assert.AreEqual(left[i].y, right[i].y, 0.0001f,
                    "вертикальное положение иконок не зависит от руки");
            }
        }

        /// <summary>Строка журнала обязана называть число групп, число иконок веера и порог подписи.</summary>
        [Test]
        public void Describe_ContainsGroupFanAndDwellNumbers()
        {
            string text = KvXrGroupPanelLayout.Describe(8, 14, "Визуализация", KvXrQuickHand.Right);

            StringAssert.Contains("групп 8", text, "в журнале обязано быть число групп");
            StringAssert.Contains("иконок 14", text, "в журнале обязано быть число иконок веера");
            StringAssert.Contains("подпись через 0.5 с", text, "в журнале обязан быть порог подписи");
            StringAssert.Contains("правая", text, "в журнале обязана быть рука");
        }

        /// <summary>
        /// СПИСКИ КОМАНД ПРИГОДНЫ ДЛЯ ВЕЕРА: непустые, без пустых строк и БЕЗ ПОВТОРОВ внутри
        /// группы. Повтор дал бы две одинаковые иконки, и владелец не смог бы понять, какая
        /// из них какая. Существование самих команд здесь не проверяется намеренно: реестр
        /// команд наполняется менеджером интерфейса ВО ВРЕМЯ РАБОТЫ, и в EditMode он пуст —
        /// такую проверку делает панель при построении и пишет пропуски в журнал.
        /// </summary>
        [Test]
        public void GroupCommandLists_AreClean()
        {
            int checkedIds = 0;
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
            {
                Assert.IsNotNull(group.Commands,
                    "у группы «" + group.Title + "» список команд не задан");
                Assert.Greater(group.Commands.Length, 0,
                    "группа «" + group.Title + "» пуста: её иконка в веере ничего бы не раскрывала");

                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (string id in group.Commands)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(id),
                        "в группе «" + group.Title + "» есть пустой идентификатор команды");
                    Assert.IsTrue(seen.Add(id),
                        "команда «" + id + "» повторяется в группе «" + group.Title + "»: " +
                        "в веере появятся две одинаковые иконки");
                    checkedIds++;
                }
            }
            Assert.Greater(checkedIds, 0, "проверка обязана была осмотреть хотя бы одну команду");
        }
    }
}
