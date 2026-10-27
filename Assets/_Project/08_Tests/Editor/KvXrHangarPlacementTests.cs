using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты ПОСТАНОВКИ АНГАРА В XR-СЦЕНУ (сессия §35, карточка Б17).
    ///
    /// ЗАЧЕМ. Владелец надел шлем и увидел «только горизонт, никаких ангаров»: в APK одна сцена
    /// (`XrMain`), и в ней нет ни одного объекта ангара, хотя в десктопной сцене лаборатория стоит
    /// ВНУТРИ ангара. Постановка окружения — это ЧИСЛА (положение, габарит, масштаб), и ошибиться
    /// в них легко на десятки метров: зал проекта — 37,8 × 50,3 м при потолке 9,4 м.
    ///
    /// ЧТО ЗДЕСЬ ПРОВЕРЯЕТСЯ (всё — арифметика, без шлема и без сцены):
    ///   • масштаб ангара РОВНО 1,0 — он измерен, а не подобран «на глаз» ([S3.6]);
    ///   • оператор и ОБА стенда оказываются ВНУТРИ зала, а не в стене и не снаружи;
    ///   • смещение центра зала от центра стендов сохраняется таким же, как на компьютере —
    ///     это и есть «тем же расположением относительно стендов» из порядка работ владельца;
    ///   • пол зала встаёт на уровень пола сцены (иначе оператор «провалится» или «всплывёт»);
    ///   • потолок остаётся человеческим, а не игрушечным и не километровым;
    ///   • КАНАЛЫ КАРТЫ МАСКИ при переводе материалов с HDRP на URP не путаются — иначе
    ///     металл становится похож на пластик молча, без единой ошибки в журнале.
    /// </summary>
    public class KvXrHangarPlacementTests
    {
        /// <summary>Центр стендов так, как их ставит `KvXrSceneContent`: впереди оператора на 3,2 м.</summary>
        private static readonly Vector3 StandsCenterXr = new Vector3(0f, 1.30f, 3.20f);

        /// <summary>МАСШТАБ АНГАРА РОВНО ЕДИНИЦА. Это измеренное значение, а не догадка.</summary>
        [Test]
        public void Scale_IsExactlyOne()
        {
            Assert.AreEqual(1f, KvHangarPlacement.RootScale, 0.0001f,
                "масштаб ангара обязан быть ровно 1,0: он измерен (масштаб корней 1.000), " +
                "а подобранный «на глаз» дал бы либо игрушечную комнату, либо зал в километр");
        }

        /// <summary>
        /// СМЕЩЕНИЕ ЦЕНТРА ЗАЛА ОТ ЦЕНТРА СТЕНДОВ — переме́ренное (10,91; 3,39; 11,00).
        /// Первый замер давал по X «9,71» и был неверен: измеритель видел 1 плиту пола из 16.
        /// </summary>
        [Test]
        public void DesktopOffset_MatchesMeasurement()
        {
            Vector3 offset = KvHangarPlacement.DesktopOffset;

            Assert.AreEqual(10.91f, offset.x, 0.01f, "смещение по X взято из измерения, а не назначено");
            Assert.AreEqual(3.39f, offset.y, 0.01f, "смещение по Y взято из измерения");
            Assert.AreEqual(11.00f, offset.z, 0.01f, "смещение по Z взято из измерения");
        }

        /// <summary>
        /// ГАБАРИТ ЗАЛА — ИЗМЕРЕННЫЙ, а не удобный. 40,18 м по X, а не 37,77: плиты пола зала
        /// идут от x = −9 до x = 31, и прежний измеритель их не видел. Это не придирка к цифре:
        /// габарит печатается в журнал как доказательство и участвует в проверке «внутри зала».
        /// </summary>
        [Test]
        public void HallSize_MatchesMeasurement()
        {
            Vector3 size = KvHangarPlacement.DesktopHangarSize;

            Assert.AreEqual(40.18f, size.x, 0.01f, "габарит зала по X — переме́ренное значение");
            Assert.AreEqual(9.38f, size.y, 0.01f, "высота зала — переме́ренное значение");
            Assert.AreEqual(50.31f, size.z, 0.01f, "габарит зала по Z — переме́ренное значение");
        }

        /// <summary>
        /// ГЛАВНОЕ СВОЙСТВО: после переноса центр зала отстоит от центра стендов РОВНО на то же
        /// смещение, что и на компьютере. Если это сломается, ангар уедет в сторону.
        /// </summary>
        [Test]
        public void Placement_KeepsDesktopOffsetFromStands()
        {
            Vector3 root = KvHangarPlacement.RootPosition(StandsCenterXr);
            Vector3 actual = KvHangarPlacement.BoundsAt(root).center - StandsCenterXr;
            Vector3 expected = KvHangarPlacement.DesktopOffset;

            Assert.AreEqual(expected.x, actual.x, 0.01f, "смещение по X обязано сохраниться");
            Assert.AreEqual(expected.y, actual.y, 0.01f, "смещение по Y обязано сохраниться");
            Assert.AreEqual(expected.z, actual.z, 0.01f, "смещение по Z обязано сохраниться");
        }

        /// <summary>ОПЕРАТОР ВНУТРИ ЗАЛА. Иначе он увидит стену или пустоту, а не помещение.</summary>
        [Test]
        public void Operator_IsInsideHall()
        {
            Vector3 root = KvHangarPlacement.RootPosition(StandsCenterXr);

            Assert.IsTrue(KvHangarPlacement.Contains(root, Vector3.zero),
                "оператор стоит в начале координат и обязан оказаться ВНУТРИ ангара — " +
                "ради этого вся задача и делается");
        }

        /// <summary>ОБА СТЕНДА ВНУТРИ ЗАЛА: владелец требует видеть столы с роботами в помещении.</summary>
        [Test]
        public void BothStands_AreInsideHall()
        {
            Vector3 root = KvHangarPlacement.RootPosition(StandsCenterXr);

            // Так их ставит KvXrSceneContent: впереди оператора, слева и справа от него.
            Vector3 standLeft = new Vector3(-1.50f, 1.30f, 3.20f);
            Vector3 standRight = new Vector3(1.50f, 1.30f, 3.20f);

            Assert.IsTrue(KvHangarPlacement.Contains(root, standLeft),
                "левый стенд обязан стоять внутри зала");
            Assert.IsTrue(KvHangarPlacement.Contains(root, standRight),
                "правый стенд обязан стоять внутри зала");
        }

        /// <summary>Пол зала — на уровне пола сцены: иначе оператор провалится или повиснет.</summary>
        [Test]
        public void Floor_SitsAtSceneFloorLevel()
        {
            Vector3 root = KvHangarPlacement.RootPosition(StandsCenterXr);

            Assert.AreEqual(0f, KvHangarPlacement.FloorHeight(root), 0.01f,
                "пол ангара обязан лечь на пол сцены (стенды стоят на y = 0), " +
                "иначе оператор окажется под полом или над ним");
        }

        /// <summary>Потолок — человеческий: высокий зал, но не игрушечная коробка и не километр.</summary>
        [Test]
        public void Ceiling_IsHumanScale()
        {
            Vector3 root = KvHangarPlacement.RootPosition(StandsCenterXr);
            float ceiling = KvHangarPlacement.CeilingMeters(root);

            Assert.Greater(ceiling, KvHangarPlacement.MinCeilingMeters,
                "потолок ниже человеческого роста означал бы «я не в помещении»");
            Assert.Less(ceiling, 30f,
                "потолок в десятки метров означал бы, что масштаб потерян");
            Assert.AreEqual(9.38f, ceiling, 0.05f,
                "измеренный потолок ангара — 9,38 м; другое значение значит, что габарит пересчитан неверно");
        }

        /// <summary>
        /// СТОЛЕШНИЦА ПО ПОЯС. Владелец: «столешница примерно по пояс (0,98 м)». Это высота
        /// стендов проекта, и постановка ангара не имеет права её менять.
        /// </summary>
        [Test]
        public void TableTop_StaysAtWaistHeight()
        {
            Assert.AreEqual(0.98f, TrajectoryCore.StandBuilder.TopHeight, 0.001f,
                "высота столешницы — 0,98 м по проекту; ангар ставится ВОКРУГ стендов, а не вместо них");
        }

        /// <summary>Зал обязан быть заметно больше стендов, иначе это не помещение, а витрина.</summary>
        [Test]
        public void Hall_IsLargerThanStands()
        {
            Bounds hall = KvHangarPlacement.BoundsForStands(StandsCenterXr);

            Assert.Greater(hall.size.x, 14.40f * 2f, "зал по X обязан быть заметно шире стендов (14,4 м)");
            Assert.Greater(hall.size.z, 20.20f * 2f, "зал по Z обязан быть заметно глубже стендов (20,2 м)");
        }

        /// <summary>
        /// ЗАЛ СТАВИТСЯ ПО ОПЕРАТОРУ, А НЕ ПО СТЕНДАМ. Исправление 16.10.2026: привязка к стендам
        /// уносила оператора к торцевой стене, как только столы отодвигали.
        ///
        /// ОПЕРАТОР ВСТАЁТ В ЦЕНТР СТЕНДОВ — это ЗАЗОР между двумя столами (десктопная точка
        /// (0; 1,30; −26,00)). Так вся лаборатория сохраняет РОВНО десктопные координаты,
        /// как и просил владелец: «сверь координаты робота в desktop версии».
        /// </summary>
        [Test]
        public void Hall_IsAnchoredToOperatorAndLabKeepsDesktopCoordinates()
        {
            Vector3 operatorBase = Vector3.zero;
            Vector3 root = KvHangarPlacement.RootPositionForOperator(operatorBase);
            Vector3 operatorLocal = operatorBase - root;

            // Оператор оказывается ровно в десктопной точке центра стендов (0; 1,30; −26,00),
            // то есть в ЗАЗОРЕ между двумя столами. Знак: центр стендов на компьютере имеет
            // отрицательный z, и в шлеме оператор тоже оказывается в отрицательном z относительно
            // корня зала — потому что зал сдвинут на +26, а оператор стоит в начале координат.
            Assert.AreEqual(KvHangarPlacement.DesktopStandsCenter.z, operatorLocal.z, 0.01f,
                "оператор обязан оказаться ровно в центре стендов — в зазоре между двумя столами");
            Assert.IsTrue(KvHangarPlacement.Contains(root, operatorBase),
                "при такой привязке оператор обязан быть внутри зала");

            // Лаборатория сохраняет десктопные координаты: стол 1 встаёт туда, где он на компьютере,
            // плюс единый сдвиг — тот же самый, что у зала.
            Vector3 stand1 = KvHangarPlacement.ShiftToOperator(new Vector3(0f, 0f, -20.70f));
            Assert.AreEqual(0f, stand1.x, 0.01f, "стол не должен уезжать вбок: на компьютере он по центру");
            Assert.AreEqual(-20.70f - KvHangarPlacement.DesktopStandsCenter.z, stand1.z, 0.01f,
                "стол обязан встать в свою ДЕСКТОПНУЮ координату плюс единый сдвиг лаборатории");
            Assert.AreEqual(root.z, KvHangarPlacement.LabShiftToOperator.z, 0.01f,
                "зал и столы сдвигаются ОДНИМ И ТЕМ ЖЕ смещением, иначе они разъедутся");
        }

        /// <summary>
        /// ОПЕРАТОР НЕ У ТОРЦЕВОЙ СТЕНЫ. Именно это увидел владелец, когда зал поехал за столами:
        /// «камера где-то в непонятном месте». Расстояние до ближнего торца зала обязано быть
        /// заметно больше, чем пары метров.
        /// </summary>
        [Test]
        public void Operator_IsNotJammedAgainstTheEndWall()
        {
            Vector3 root = KvHangarPlacement.RootPositionForOperator(Vector3.zero);
            Bounds hall = KvHangarPlacement.BoundsAt(root);

            float toFarEnd = Mathf.Abs(hall.min.z - 0f);
            float toNearEnd = Mathf.Abs(hall.max.z - 0f);

            Assert.Greater(Mathf.Min(toFarEnd, toNearEnd), 5f,
                "оператор обязан стоять не у самой стены: до ближайшего торца " +
                Mathf.Min(toFarEnd, toNearEnd).ToString("0.0") + " м — этого мало");
        }

        /// <summary>Строка журнала обязана существовать и содержать числа, а не слова «стало лучше».</summary>
        [Test]
        public void Describe_ContainsMeasuredNumbers()
        {
            Vector3 root = KvHangarPlacement.RootPosition(StandsCenterXr);
            string text = KvHangarPlacement.Describe(StandsCenterXr, root);

            StringAssert.Contains("40.18", text, "в журнале обязан быть измеренный габарит зала по X");
            StringAssert.Contains("50.31", text, "в журнале обязан быть измеренный габарит зала по Z");
            StringAssert.Contains("масштаб 1.000", text, "в журнале обязан быть масштаб");
            StringAssert.Contains("потолок 9.38", text, "в журнале обязан быть потолок");
        }

        /// <summary>
        /// КАНАЛЫ МАСКИ HDRP → URP. У HDRP металличность в R, затенение в G, гладкость в A;
        /// у URP металличность в R и гладкость в A, а затенение — В ЗЕЛЁНОМ канале отдельной карты.
        /// Перепутать каналы — значит потерять вид металла молча.
        /// </summary>
        [Test]
        public void MaterialMask_ChannelsAreMappedCorrectly()
        {
            // Карта маски: R — металличность 0,9 · G — затенение 0,6 · B — детали 0,2 · A — гладкость 0,35
            Color mask = new Color(0.90f, 0.60f, 0.20f, 0.35f);

            Color metallicGloss = KvHdrpToUrpMaterial.ToUrpMetallicGloss(mask);
            Assert.AreEqual(0.90f, metallicGloss.r, 0.001f, "металличность обязана остаться в КРАСНОМ канале");
            Assert.AreEqual(0.35f, metallicGloss.a, 0.001f, "гладкость обязана уехать в АЛЬФУ");

            Color occlusion = KvHdrpToUrpMaterial.ToUrpOcclusion(mask);
            Assert.AreEqual(0.60f, occlusion.g, 0.001f,
                "затенение у URP живёт в ЗЕЛЁНОМ канале; положить его в красный — тихая потеря затенения");

            Assert.AreEqual(0.90f, KvHdrpToUrpMaterial.MetallicOf(mask), 0.001f, "скаляр металличности — из R");
            Assert.AreEqual(0.35f, KvHdrpToUrpMaterial.SmoothnessOf(mask), 0.001f, "скаляр гладкости — из A");
        }

        /// <summary>Однотонная карта маски бесполезна: металличность и гладкость надо брать скалярами.</summary>
        [Test]
        public void MaterialMask_FlatMapIsRejected()
        {
            Color flat = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Color useful = new Color(0.9f, 0.5f, 0.5f, 0.2f);

            Assert.IsFalse(KvHdrpToUrpMaterial.HasUsableMask(flat),
                "однотонная карта маски не несёт сведений — её нельзя выдавать за перенесённую");
            Assert.IsTrue(KvHdrpToUrpMaterial.HasUsableMask(useful),
                "карта с разными каналами обязана считаться пригодной");
        }

        /// <summary>Замена шейдера обязана называть URP, иначе розовые поверхности вернутся.</summary>
        [Test]
        public void MaterialReplacement_TargetsUrpLit()
        {
            Assert.AreEqual("Universal Render Pipeline/Lit", KvHdrpToUrpMaterial.UrpLitShader,
                "ангар в шлеме обязан использовать URP-шейдер: уровень качества Android — Mobile (URP)");

            StringAssert.Contains("Universal Render Pipeline",
                KvHdrpToUrpMaterial.Describe("Concret_floor_wet", true, true, true, 0.1f, 0.4f),
                "строка журнала обязана называть конвейер, под который переведён материал");
        }
    }
}
