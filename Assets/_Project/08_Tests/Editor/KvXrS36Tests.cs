using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;
using UnityEngine.XR.OpenXR.Features;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ СЕССИИ §36 — ТО, ЧТО СЛОМАЛОСЬ НА САМОМ ДЕЛЕ.
    ///
    /// ЗДЕСЬ ДВА ПРЕДМЕТА, и оба взяты не из головы, а из журнала живого шлема:
    ///
    /// 1) ПРОФИЛЬ КОНТРОЛЛЕРОВ. Рантайм отвечал `xrSuggestInteractionProfileBindings:
    ///    XR_ERROR_PATH_UNSUPPORTED` и отбрасывал ВЕСЬ набор привязок, потому что привязка без
    ///    `userPaths` уходит обеим рукам (`OpenXRInput.cs:296`), а кнопки «a» на левом
    ///    контроллере не существует. Правило проверяется на строках, карта действий — целиком.
    ///
    /// 2) ПОПАДАНИЕ ЛУЧА В ИКОНКУ ПАНЕЛИ. Прежний путь (uGUI) ждал экранные пиксели и вёл луч
    ///    в угол экрана; теперь попадание считается геометрией в МЕТРАХ. Это чистая арифметика,
    ///    поэтому проверяется без шлема: ровно то, что иначе стоило бы ещё одного цикла сборки.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: проверки «шлем отдал контроллер». Это видно только в журнале устройства
    /// строкой `[KvXr] КОНТРОЛЛЕРЫ ПОЯВИЛИСЬ` — тестами без шлема такое не проверяется.
    /// </summary>
    public class KvXrS36Tests
    {
        // ==================================================== 1. ПРОФИЛЬ КОНТРОЛЛЕРОВ

        /// <summary>
        /// НАСТОЯЩАЯ КАРТА ДЕЙСТВИЙ НЕ НАРУШАЕТ ПРАВИЛО ПО РУКАМ. Это тот самый дефект, из-за
        /// которого контроллеры «не находились»: нарушение здесь = отказ рантайма на всё.
        /// </summary>
        [Test]
        public void Profile_AuditFindsNoHandBindingViolations()
        {
            string report;
            int bindings;
            int restricted;
            bool ok = KvPicoControllerProfile.AuditHandBindings(out report, out bindings, out restricted);

            Assert.IsTrue(ok, "карта привязок нарушает правило «путь кнопки руки → своя рука»: " + report);
            StringAssert.Contains("нарушений 0", report, "в отчёте обязано быть видно число нарушений");
            Assert.Greater(bindings, 10, "привязок подозрительно мало — карта собралась не полностью");
        }

        /// <summary>
        /// ОГРАНИЧЕНИЙ ПО РУКЕ НЕ МЕНЬШЕ ВОСЬМИ: a, a-touch, b, b-touch (правая) и x, x-touch,
        /// y, y-touch, menu (левая). Если их станет меньше, часть путей снова уйдёт обеим рукам.
        /// </summary>
        [Test]
        public void Profile_HandRestrictedBindingsArePresent()
        {
            string report;
            int bindings;
            int restricted;
            KvPicoControllerProfile.AuditHandBindings(out report, out bindings, out restricted);

            Assert.GreaterOrEqual(restricted, 8,
                "привязок с ограничением по руке меньше восьми: " + report);
        }

        /// <summary>
        /// ПУТЬ ПРАВОЙ РУКИ БЕЗ ОГРАНИЧЕНИЯ — НАРУШЕНИЕ. Проверка «правило не пустое»:
        /// если бы функция всегда возвращала null, дефект §35 не был бы пойман.
        /// </summary>
        [Test]
        public void Profile_RuleRejectsRightPathWithoutHand()
        {
            Assert.IsNotNull(KvPicoControllerProfile.HandBindingViolation("/input/a/click", null),
                "путь правой руки без userPaths обязан считаться нарушением");
            Assert.IsNotNull(KvPicoControllerProfile.HandBindingViolation("/input/b/touch", new List<string>()),
                "пустой список рук — то же самое, что его отсутствие");
        }

        /// <summary>
        /// ПУТЬ ПРАВОЙ РУКИ, ПРИВЯЗАННЫЙ И К ЛЕВОЙ, — НАРУШЕНИЕ. Именно это отправляло
        /// `/user/hand/left/input/a/click` в рантайм и роняло весь набор.
        /// </summary>
        [Test]
        public void Profile_RuleRejectsRightPathOnBothHands()
        {
            var both = new List<string>() { OpenXRInteractionFeature.UserPaths.leftHand, OpenXRInteractionFeature.UserPaths.rightHand };
            Assert.IsNotNull(KvPicoControllerProfile.HandBindingViolation("/input/a/click", both));
        }

        /// <summary>ПУТЬ ЛЕВОЙ РУКИ (x) НА ПРАВОЙ — НАРУШЕНИЕ.</summary>
        [Test]
        public void Profile_RuleRejectsLeftPathOnRightHand()
        {
            var right = new List<string>() { OpenXRInteractionFeature.UserPaths.rightHand };
            Assert.IsNotNull(KvPicoControllerProfile.HandBindingViolation("/input/x/click", right));
            Assert.IsNotNull(KvPicoControllerProfile.HandBindingViolation("/input/menu/click", right));
        }

        /// <summary>ПРАВИЛЬНЫЕ СОЧЕТАНИЯ ПРИНИМАЮТСЯ — и общие пути (курок, поза) тоже.</summary>
        [Test]
        public void Profile_RuleAcceptsCorrectHandsAndCommonPaths()
        {
            Assert.IsNull(KvPicoControllerProfile.HandBindingViolation("/input/a/click",
                new List<string>() { OpenXRInteractionFeature.UserPaths.rightHand }), "a на правой руке — верно");
            Assert.IsNull(KvPicoControllerProfile.HandBindingViolation("/input/y/touch",
                new List<string>() { OpenXRInteractionFeature.UserPaths.leftHand }), "y на левой руке — верно");
            Assert.IsNull(KvPicoControllerProfile.HandBindingViolation("/input/trigger/value", null),
                "курок есть на обеих руках — ограничение не требуется");
            Assert.IsNull(KvPicoControllerProfile.HandBindingViolation("/input/grip/pose", null),
                "поза кисти есть на обеих руках");
            Assert.IsNull(KvPicoControllerProfile.HandBindingViolation("/output/haptic", null),
                "вибрация — выход, руки не важны");
        }

        /// <summary>ИМЯ ЭЛЕМЕНТА ИЗ ПУТИ — по нему и работает правило.</summary>
        [Test]
        public void Profile_ComponentOfParsesInputPath()
        {
            Assert.AreEqual("thumbstick", KvPicoControllerProfile.ComponentOf("/input/thumbstick/click"));
            Assert.AreEqual("menu", KvPicoControllerProfile.ComponentOf("/input/menu/click"));
            Assert.AreEqual("a", KvPicoControllerProfile.ComponentOf("/input/a/click"));
            Assert.AreEqual(string.Empty, KvPicoControllerProfile.ComponentOf("/output/haptic"));
            Assert.AreEqual(string.Empty, KvPicoControllerProfile.ComponentOf(null));
        }

        /// <summary>
        /// ПУТИ ОФИЦИАЛЬНОГО ПРОФИЛЯ НА МЕСТЕ. Список короткий, но каждая строка проверена
        /// сравнением с `Tools/_pico_ref/PICO4UltraControllerProfile.cs`.
        /// </summary>
        [Test]
        public void Profile_HandOnlyPathsMatchOfficialProfile()
        {
            Assert.AreEqual("/interaction_profiles/bytedance/pico4s_controller", KvPicoControllerProfile.profile);
            Assert.AreEqual("/input/a/click", KvPicoControllerProfile.buttonA);
            Assert.AreEqual("/input/b/click", KvPicoControllerProfile.buttonB);
            Assert.AreEqual("/input/x/click", KvPicoControllerProfile.buttonX);
            Assert.AreEqual("/input/y/click", KvPicoControllerProfile.buttonY);
            Assert.AreEqual("/input/menu/click", KvPicoControllerProfile.menu);
            Assert.AreEqual("/input/aim/pose", KvPicoControllerProfile.aim);
        }

        // ==================================================== 2. ГЕОМЕТРИЯ ЛУЧА ПО ПАНЕЛИ

        private static readonly Vector3 PanelOrigin = new Vector3(0f, 1.4f, 0f);
        private static readonly Vector3 PanelRight = Vector3.right;
        private static readonly Vector3 PanelUp = Vector3.up;
        // Оси панели: вправо (+X), вверх (+Y), нормаль передней стороны (−Z). Оператор стоит
        // на +Z и смотрит в −Z, то есть В ПАНЕЛЬ. Расчёт не зависит от знака нормали (числитель
        // и знаменатель меняют знак вместе) — это отдельно проверено тестом «панель за спиной».
        private static readonly Vector3 PanelNormal = new Vector3(0f, 0f, -1f);

        /// <summary>ЛУЧ В ЦЕНТР ИКОНКИ ПОПАДАЕТ В НЕЁ, и расстояние от центра считается в метрах.</summary>
        [Test]
        public void Geometry_RayAtIconCenterHitsThatIcon()
        {
            var offsets = new[] { new Vector2(0f, 0.155f), new Vector2(0.1f, 0f) };

            int index;
            float fromCenter;
            float along;
            bool hit = KvXrGroupPanelLayout.TryHitIcon(
                new Vector3(0.1f, 1.4f, 0.5f),           // глаз оператора: в 0,5 м перед панелью
                new Vector3(0f, 0f, -1f),                // смотрит на панель
                PanelOrigin, PanelRight, PanelUp, PanelNormal, offsets,
                out index, out fromCenter, out along);

            Assert.IsTrue(hit, "луч в центр иконки обязан давать попадание");
            Assert.AreEqual(1, index, "попали не в ту иконку");
            Assert.AreEqual(0f, fromCenter, 0.0001f, "расстояние от центра обязано быть нулевым");
            Assert.AreEqual(0.5f, along, 0.001f, "дальность вдоль луча — 0,5 м (в метрах, не в пикселях)");
        }

        /// <summary>ЛУЧ МИМО ДАЛЬШЕ РАДИУСА ПОПАДАНИЯ — ПРОМАХ, а не «ближайшая иконка».</summary>
        [Test]
        public void Geometry_RayPastHitRadiusMisses()
        {
            var offsets = new[] { new Vector2(0f, 0f) };
            float beyond = KvXrGroupPanelLayout.HitRadiusMeters + 0.05f;

            int index;
            float fromCenter;
            float along;
            bool hit = KvXrGroupPanelLayout.TryHitIcon(
                new Vector3(beyond, 1.4f, 0.5f), new Vector3(0f, 0f, -1f),
                PanelOrigin, PanelRight, PanelUp, PanelNormal, offsets,
                out index, out fromCenter, out along);

            Assert.IsFalse(hit, "промах дальше радиуса попадания не должен нажимать иконку");
            Assert.AreEqual(-1, index, "номер иконки при промахе — −1, а не 0");
        }

        /// <summary>ЛУЧ ПАРАЛЛЕЛЕН ПЛОСКОСТИ ПАНЕЛИ — ПЕРЕСЕЧЕНИЯ НЕТ.</summary>
        [Test]
        public void Geometry_ParallelRayMisses()
        {
            Vector2 point;
            float along;
            bool hit = KvXrGroupPanelLayout.TryRayToPanelPlane(
                new Vector3(0f, 1.4f, 0.5f), Vector3.right,
                PanelOrigin, PanelRight, PanelUp, PanelNormal,
                out point, out along);

            Assert.IsFalse(hit, "параллельный луч не пересекает плоскость панели");
        }

        /// <summary>ПАНЕЛЬ ЗА СПИНОЙ ЛУЧА — ПРОМАХ (иначе нажималась бы иконка за оператором).</summary>
        [Test]
        public void Geometry_PanelBehindRayMisses()
        {
            Vector2 point;
            float along;
            bool hit = KvXrGroupPanelLayout.TryRayToPanelPlane(
                new Vector3(0f, 1.4f, -0.5f), new Vector3(0f, 0f, -1f),
                PanelOrigin, PanelRight, PanelUp, PanelNormal,
                out point, out along);

            Assert.IsFalse(hit, "панель позади луча не должна ловить наведение");
        }

        /// <summary>
        /// ПЕРЕСЕЧЕНИЕ ПЛОСКОСТИ: точка в осях панели и дальность вдоль луча в метрах.
        /// Сверка с ручным расчётом: луч из (0,15; 1,4; 0,75) по −Z при нормали (0,0,−1)
        /// проходит 0,75 м и попадает в (0,15; 1,4; 0).
        /// </summary>
        [Test]
        public void Geometry_PlaneIntersectionIsInMeters()
        {
            Vector2 point;
            float along;
            bool ok = KvXrGroupPanelLayout.TryRayToPanelPlane(
                new Vector3(0.15f, 1.4f, 0.75f), new Vector3(0f, 0f, -1f),
                PanelOrigin, PanelRight, PanelUp, PanelNormal,
                out point, out along);

            Assert.IsTrue(ok);
            Assert.AreEqual(0.75f, along, 0.001f, "дальность вдоль луча — в метрах");
            Assert.AreEqual(0.15f, point.x, 0.001f, "смещение вправо от центра панели, метры");
            Assert.AreEqual(0f, point.y, 0.001f, "по вертикали смещения нет");
        }

        /// <summary>НАКЛОНёнНАЯ ПАНЕЛЬ: оси берутся из её собственного поворота, а не из мировых.</summary>
        [Test]
        public void Geometry_RotatedPanelUsesItsOwnAxes()
        {
            Quaternion rotation = Quaternion.Euler(0f, 30f, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 normal = rotation * Vector3.forward;   // передняя сторона смотрит по +Z

            Vector3 origin = PanelOrigin - normal * 0.6f;  // оператор перед панелью
            var offsets = new[] { new Vector2(0.2f, 0f) }; // иконка в 20 см вправо В ОСЯХ ПАНЕЛИ
            Vector3 target = PanelOrigin + right * 0.2f;

            int index;
            float fromCenter;
            float along;
            bool hit = KvXrGroupPanelLayout.TryHitIcon(
                origin, (target - origin).normalized,
                PanelOrigin, right, up, normal, offsets,
                out index, out fromCenter, out along);

            Assert.IsTrue(hit, "луч в иконку повёрнутой панели обязан попадать");
            Assert.AreEqual(0, index);
            Assert.AreEqual(0f, fromCenter, 0.0005f, "точное попадание в центр иконки");
        }

        /// <summary>ПУСТОЙ СПИСОК ИКОНОК И НУЛЕВЫЕ ВЕКТОРЫ — не исключение, а честный промах.</summary>
        [Test]
        public void Geometry_DegenerateInputsDoNotThrow()
        {
            int index;
            float fromCenter;
            float along;

            Assert.IsFalse(KvXrGroupPanelLayout.TryHitIcon(
                Vector3.zero, Vector3.forward, PanelOrigin, PanelRight, PanelUp, PanelNormal,
                null, out index, out fromCenter, out along), "без иконок попадания быть не может");

            Assert.IsFalse(KvXrGroupPanelLayout.TryHitIcon(
                Vector3.zero, Vector3.zero, PanelOrigin, PanelRight, PanelUp, PanelNormal,
                new[] { Vector2.zero }, out index, out fromCenter, out along),
                "нулевое направление луча — промах, а не NaN");

            Assert.IsFalse(KvXrGroupPanelLayout.TryRayToPanelPlane(
                Vector3.zero, Vector3.forward, PanelOrigin, PanelRight, PanelUp, Vector3.zero,
                out var point, out along), "нулевая нормаль панели — промах");
            Assert.AreEqual(0f, point.x, 0.0001f);
        }
    }
}
