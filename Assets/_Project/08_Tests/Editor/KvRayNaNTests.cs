// ================================================================================================
//  KvRayNaNTests.cs — EditMode-тесты НЕЧИСЛОВЫХ ДАННЫХ В ЛУЧЕ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Продолжение находки из предыдущего раунда: в C# любое сравнение с NaN даёт `false`,
//  поэтому проверки вида `sqrMagnitude < 1e-8f` для вектора с NaN НЕ СРАБАТЫВАЮТ — вектор проходит
//  как нормальный.
//
//  ЧТО НАЙДЕНО 30.10.2026 в `TryRayToPanelPlane` (наведение луча на панель в шлеме):
//    • `rayOrigin` не проверялся ВООБЩЕ — он не участвует в `sqrMagnitude`, а попадает прямо
//      в `panelOrigin - rayOrigin`;
//    • проверки нормали и направления при NaN проходили;
//    • дальше `distance <= 0f` для NaN тоже давало `false`, и функция возвращала «ПОПАДАНИЕ»
//      с мусорными координатами.
//
//  ЧЕМ ЭТО ПЛОХО ДЛЯ ОПЕРАТОРА. Луч «нажимал» иконку, на которую он не смотрел: попадание
//  вычислялось по нечисловым данным, а проверка «за спиной ли панель» не работала. В шлеме это
//  выглядело бы как «панель сама нажимается».
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ:
//    1) нечисловое НАЧАЛО луча — отказ (главная находка: не проверялось вообще);
//    2) нечисловое НАПРАВЛЕНИЕ — отказ;
//    3) нечисловая ПЛОСКОСТЬ (нормаль, право, верх, начало) — отказ;
//    4) НОРМАЛЬНЫЙ луч попадает (обратная ошибка тоже дефект);
//    5) луч ЗА спиной панели не попадает (базовая проверка направления).
// ================================================================================================

using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Луч и панель: нечисловые данные дают отказ, а не «попадание».</summary>
    public class KvRayNaNTests
    {
        // Панель стоит в метре перед оператором, лицом к нему.
        private static readonly Vector3 PanelOrigin = new Vector3(0f, 1.5f, 1f);
        private static readonly Vector3 PanelRight = Vector3.right;
        private static readonly Vector3 PanelUp = Vector3.up;
        private static readonly Vector3 PanelNormal = Vector3.back;   // лицом к оператору (в −Z)

        private static bool TryHit(Vector3 origin, Vector3 direction, out Vector2 point, out float distance)
        {
            return KvXrGroupPanelLayout.TryRayToPanelPlane(
                origin, direction, PanelOrigin, PanelRight, PanelUp, PanelNormal,
                out point, out distance);
        }

        // ---------------------------------------------------------------- 1. НАЧАЛО ЛУЧА

        /// <summary>
        /// НЕЧИСЛОВОЕ НАЧАЛО ЛУЧА — ОТКАЗ. ЭТО ГЛАВНАЯ НАХОДКА: `rayOrigin` не проверялся вообще.
        ///
        /// Он не участвует в `sqrMagnitude`, а сразу попадает в `panelOrigin - rayOrigin` — то есть
        /// заражает ВСЁ вычисление, обходя все проверки.
        /// </summary>
        [Test]
        public void NaNOrigin_IsRejected()
        {
            Vector2 point;
            float distance;

            var nanOrigin = new Vector3(float.NaN, 1.5f, 0f);
            Assert.IsFalse(TryHit(nanOrigin, Vector3.forward, out point, out distance),
                "луч с NaN в НАЧАЛЕ признан попавшим в панель — координаты попадания мусорные");

            var infOrigin = new Vector3(float.PositiveInfinity, 1.5f, 0f);
            Assert.IsFalse(TryHit(infOrigin, Vector3.forward, out point, out distance),
                "луч с бесконечностью в начале признан попавшим");
        }

        // ---------------------------------------------------------------- 2. НАПРАВЛЕНИЕ

        /// <summary>
        /// НЕЧИСЛОВОЕ НАПРАВЛЕНИЕ — ОТКАЗ. Проверка `rayDirection.sqrMagnitude < 1e-8f` для NaN даёт
        /// `false`, то есть направление проходит как нормальное.
        /// </summary>
        [Test]
        public void NaNDirection_IsRejected()
        {
            Vector2 point;
            float distance;

            var nanDirection = new Vector3(0f, 0f, float.NaN);
            Assert.IsFalse(TryHit(Vector3.zero, nanDirection, out point, out distance),
                "луч с NaN в НАПРАВЛЕНИИ признан попавшим — норма прицела не работает");
        }

        // ---------------------------------------------------------------- 3. ПЛОСКОСТЬ

        /// <summary>
        /// НЕЧИСЛОВАЯ ПЛОСКОСТЬ ПАНЕЛИ — ОТКАЗ. Если нормаль или базис панели повреждены, попадание
        /// посчитать нельзя: получились бы координаты в метрах «от неизвестного начала».
        /// </summary>
        [Test]
        public void NaNPanel_IsRejected()
        {
            Vector2 point;
            float distance;
            var origin = new Vector3(0f, 1.5f, 0f);
            var direction = Vector3.forward;

            Assert.IsFalse(KvXrGroupPanelLayout.TryRayToPanelPlane(
                    origin, direction, new Vector3(float.NaN, 1.5f, 1f), PanelRight, PanelUp, PanelNormal,
                    out point, out distance),
                "панель с NaN в НАЧАЛЕ признана попадаемой");

            Assert.IsFalse(KvXrGroupPanelLayout.TryRayToPanelPlane(
                    origin, direction, PanelOrigin, PanelRight, PanelUp, new Vector3(0f, 0f, float.NaN),
                    out point, out distance),
                "панель с NaN в НОРМАЛИ признана попадаемой");

            Assert.IsFalse(KvXrGroupPanelLayout.TryRayToPanelPlane(
                    origin, direction, PanelOrigin, new Vector3(float.NaN, 0f, 0f), PanelUp, PanelNormal,
                    out point, out distance),
                "панель с NaN в векторе ВПРАВО признана попадаемой");
        }

        // ---------------------------------------------------------------- 4. НОРМА

        /// <summary>
        /// НОРМАЛЬНЫЙ ЛУЧ ПОПАДАЕТ В ПАНЕЛЬ. Обратная ошибка («отсекаем всё») тоже дефект: тогда
        /// интерфейс в шлеме перестал бы нажиматься вообще.
        /// </summary>
        [Test]
        public void NormalRayHitsPanel()
        {
            Vector2 point;
            float distance;

            // Смотрим прямо вперёд из начала координат на высоте панели.
            var origin = new Vector3(0f, 1.5f, 0f);
            var direction = Vector3.forward;

            Assert.IsTrue(TryHit(origin, direction, out point, out distance),
                "обычный луч в панель не попал — интерфейс в шлеме не будет нажиматься");

            Assert.AreEqual(1f, distance, 0.01f,
                "дальность попадания обязана быть 1 м (панель в метре), получено " +
                distance.ToString("0.000"));

            Assert.AreEqual(0f, point.x, 0.01f, "попадание обязано быть в центр панели по горизонтали");
            Assert.AreEqual(0f, point.y, 0.01f, "попадание обязано быть в центр панели по вертикали");
        }

        /// <summary>
        /// ПАНЕЛЬ ЗА СПИНОЙ — НЕ ПОПАДАНИЕ. Проверка `distance <= 0f` защищает от нажатия иконки, на
        /// которую оператор не смотрит. Без неё интерфейс реагировал бы на объекты позади.
        /// </summary>
        [Test]
        public void PanelBehindRay_IsNotHit()
        {
            Vector2 point;
            float distance;

            // Оператор смотрит ВПЕРЁД (+Z), панель стоит ПОЗАДИ (нормаль к нему, то есть в +Z).
            var origin = new Vector3(0f, 1.5f, 0f);
            var direction = Vector3.forward;
            var panelBehind = new Vector3(0f, 1.5f, -1f);

            Assert.IsFalse(KvXrGroupPanelLayout.TryRayToPanelPlane(
                    origin, direction, panelBehind, Vector3.right, Vector3.up, Vector3.forward,
                    out point, out distance),
                "панель ЗА спиной признана попадаемой — интерфейс отреагирует на то, чего оператор не видит");
        }
    }
}
