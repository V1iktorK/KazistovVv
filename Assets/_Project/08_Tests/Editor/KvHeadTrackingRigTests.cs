// ================================================================================================
//  KvHeadTrackingRigTests.cs — EditMode-тесты ПРИМЕНЕНИЯ РИГА К КАМЕРЕ (сессия S44, живой шлем).
//
//  ОТКУДА ЗАДАЧА. 31.10.2026 на PICO 4 Ultra оператор увидел: «камера перевёрнута и не
//  поворачивается вместе с головой», «всё колбасит», «меню далеко».
//
//  ЖУРНАЛ ШЛЕМА:
//      Камера следует за головой: поза (-0.41, 0.92, 0.32) · высота 0,92 м
//      проба ориентации: курс шлема 0.0° → 192.6° → 185.3° → 96.5° → 5.7° → 277.4°
//      панель от глаз: 1.15 м → 3.65 м → 48.73 м → 52.30 м
//
//  ПРИЧИНА: ДВОЙНОЕ ПРИМЕНЕНИЕ РИГА.
//    1) `KvXrInputSource.TryPose` → `ToWorld(...)` → `TrackingOrigin.TransformPoint(position)`
//       — поза УЖЕ переведена в мировые координаты;
//    2) `KvXrHeadTracking` писал её в камеру через `SetPositionAndRotation`, а камера ЛЕЖИТ
//       ВНУТРИ рига — риг применялся второй раз.
//
//  ПОЧЕМУ НЕ ПРОЯВЛЯЛОСЬ РАНЬШЕ: пока риг в нуле с единичным поворотом, двойное применение равно
//  одинарному. Риг двигают и поворачивают (`KvXrLocomotion`) — и ошибка накапливается.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) перед записью позы камеры она переводится в ЛОКАЛЬНЫЕ координаты;
//    2) перевод делается через родителя камеры, а не через самого рига «на глаз»;
//    3) `ToWorld` по-прежнему переводит в мир (иначе сломается всё остальное);
//    4) математика верна: двойное применение даёт ошибку, одинарное — нет (проверяется числом).
// ================================================================================================

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Камера получает позу ровно с одним применением рига.</summary>
    public class KvHeadTrackingRigTests
    {
        private static string Find(string fileName)
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            string[] found = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// ПЕРЕД ЗАПИСЬЮ ПОЗА ПЕРЕВОДИТСЯ В ЛОКАЛЬНЫЕ КООРДИНАТЫ. Без этого риг применяется
        /// дважды, и оператор видит мир с высоты 0,92 м вместо 1,60.
        /// </summary>
        [Test]
        public void PoseIsConvertedToLocalBeforeApply()
        {
            string path = Find("KvXrHeadTracking.cs");
            Assert.IsNotNull(path, "не найден KvXrHeadTracking.cs");

            string text = File.ReadAllText(path);

            // Берём тело ApplyFromTracking по глубине скобок.
            int start = text.IndexOf("void ApplyFromTracking", System.StringComparison.Ordinal);
            Assert.Greater(start, 0, "нет метода ApplyFromTracking");

            int open = text.IndexOf('{', start);
            int depth = 1, at = open + 1;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }
            string body = text.Substring(start, at - start);

            StringAssert.Contains("InverseTransformPoint", body,
                "поза записывается в камеру БЕЗ перевода в локальные координаты — риг применится " +
                "дважды, и оператор увидит мир с 0,92 м вместо 1,60");

            StringAssert.Contains("SetPositionAndRotation", body,
                "поза камеры больше не применяется — следование за головой сломано");
        }

        /// <summary>
        /// ПЕРЕВОД ЧЕРЕЗ РОДИТЕЛЯ, А НЕ ЧЕРЕЗ РИГ НАПРЯМУЮ.
        ///
        /// `SetPositionAndRotation` пишет в систему координат РОДИТЕЛЯ. Значит и обратный перевод
        /// обязан быть от родителя: если камера когда-нибудь окажется вложенной глубже (промежуточный
        /// узел), перевод «от рига» дал бы неверный результат.
        /// </summary>
        [Test]
        public void ConversionUsesCameraParent()
        {
            string path = Find("KvXrHeadTracking.cs");
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            int start = text.IndexOf("void ApplyFromTracking", System.StringComparison.Ordinal);
            int open = text.IndexOf('{', start);
            int depth = 1, at = open + 1;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }
            string body = text.Substring(start, at - start);

            Assert.IsTrue(Regex.IsMatch(body, @"transform\.parent"),
                "перевод в локальные координаты не использует РОДИТЕЛЯ камеры — при вложенности " +
                "больше одного уровня результат будет неверным");
        }

        /// <summary>
        /// `ToWorld` ПО-ПРЕЖНЕМУ ПЕРЕВОДИТ В МИР.
        ///
        /// Опасность при исправлении: можно было «решить» проблему, убрав перевод в мир из
        /// `ToWorld`. Но от него зависят лучи, руки, панель и всё остальное — тогда сломается
        /// больше, чем починится.
        /// </summary>
        [Test]
        public void ToWorldStillConvertsToWorld()
        {
            string path = Find("KvXrInputSource.cs");
            Assert.IsNotNull(path, "не найден KvXrInputSource.cs");

            string text = File.ReadAllText(path);
            int start = text.IndexOf("static void ToWorld", System.StringComparison.Ordinal);
            Assert.Greater(start, 0, "нет метода ToWorld");

            int open = text.IndexOf('{', start);
            int depth = 1, at = open + 1;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }
            string body = text.Substring(start, at - start);

            StringAssert.Contains("TransformPoint", body,
                "ToWorld больше не переводит в мир — от него зависят лучи, руки и панель, " +
                "починить одну камеру ценой всего остального нельзя");
        }

        // ---------------------------------------------------------------- МАТЕМАТИКА

        /// <summary>
        /// ПРОВЕРКА ЧИСЛОМ: ДВОЙНОЕ ПРИМЕНЕНИЕ ДАЁТ ТУ САМУЮ ОШИБКУ.
        ///
        /// Воспроизводим то, что видел оператор: риг сдвинут на 26 м, поза головы в системе
        /// отслеживания — (–0,4; 1,6; 0,3). Считаем правильный результат и «двойной» — и убеждаемся,
        /// что они РАСХОДЯТСЯ, а после исправления совпадают.
        ///
        /// Это и есть доказательство, что причина именно в двойном применении, а не в трекинге.
        /// </summary>
        [Test]
        public void DoubleRigApplicationProducesTheObservedError()
        {
            // Риг сдвинут вперёд на 26 м (как в журнале: «сдвиг (0.00, 0.00, 26.00)»).
            var rig = new GameObject("rig");
            rig.transform.position = new Vector3(0f, 0f, 26f);
            rig.transform.rotation = Quaternion.identity;

            // Камера лежит ВНУТРИ рига.
            var cam = new GameObject("cam");
            cam.transform.SetParent(rig.transform, false);

            try
            {
                // Поза в системе отслеживания (то, что отдаёт OpenXR).
                Vector3 tracked = new Vector3(-0.4f, 1.6f, 0.3f);

                // ПРАВИЛЬНО: в мир = риг + поза.
                Vector3 world = rig.transform.TransformPoint(tracked);
                Assert.AreEqual(1.6f, world.y, 0.001f, "в мире глаза обязаны быть на 1,60 м");

                // ПРАВИЛЬНО: писать в камеру надо ЛОКАЛЬНУЮ позу — она равна исходной.
                Vector3 local = rig.transform.InverseTransformPoint(world);
                Assert.AreEqual(tracked.x, local.x, 0.001f);
                Assert.AreEqual(tracked.y, local.y, 0.001f, "локальная высота обязана остаться 1,60");
                Assert.AreEqual(tracked.z, local.z, 0.001f);

                // НЕПРАВИЛЬНО (как было): писать МИРОВУЮ позу в локальный трансформ.
                cam.transform.SetPositionAndRotation(world, Quaternion.identity);
                Vector3 resultingWorld = cam.transform.position;

                // Ошибка обязана быть равна смещению рига — иначе объяснение неверно.
                float error = Vector3.Distance(resultingWorld, world);
                Assert.AreEqual(26f, error, 0.5f,
                    "двойное применение обязано давать промах на смещение рига (26 м), " +
                    "а получилось " + error.ToString("0.0") + " м — значит причина ДРУГАЯ");
            }
            finally
            {
                Object.DestroyImmediate(cam);
                Object.DestroyImmediate(rig);
            }
        }

        /// <summary>
        /// ПОВОРОТ РИГА ТОЖЕ ДАЁТ ОШИБКУ. В журнале курс прыгал на сотни градусов — это вторая
        /// половина того же дефекта, и проверять её надо отдельно от смещения.
        /// </summary>
        [Test]
        public void DoubleRigApplicationAlsoBreaksRotation()
        {
            var rig = new GameObject("rig");
            rig.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var cam = new GameObject("cam");
            cam.transform.SetParent(rig.transform, false);

            try
            {
                Vector3 trackedPos = new Vector3(0f, 1.6f, 1.0f);
                Quaternion trackedRot = Quaternion.identity;

                // ПРАВИЛЬНО: мир = поворот рига × поза трекинга.
                Vector3 worldPos = rig.transform.TransformPoint(trackedPos);
                Quaternion worldRot = rig.transform.rotation * trackedRot;

                // Проверяем, что обратный перевод возвращает исходное — а без него риг применится дважды.
                Vector3 backToLocal = rig.transform.InverseTransformPoint(worldPos);
                Assert.AreEqual(trackedPos.z, backToLocal.z, 0.001f,
                    "обратный перевод обязан вернуть исходную позу трекинга");

                Quaternion backToLocalRot = Quaternion.Inverse(rig.transform.rotation) * worldRot;
                Assert.Less(Quaternion.Angle(trackedRot, backToLocalRot), 0.01f,
                    "обратный перевод поворота обязан вернуть исходный поворот");

                // НЕПРАВИЛЬНО: писать мировую позу в локальный трансформ при повёрнутом риге.
                cam.transform.SetPositionAndRotation(worldPos, worldRot);
                Vector3 wrong = cam.transform.position;
                Assert.Greater(Vector3.Distance(wrong, worldPos), 0.5f,
                    "поворот рига обязан давать заметную ошибку при двойном применении");
            }
            finally
            {
                Object.DestroyImmediate(cam);
                Object.DestroyImmediate(rig);
            }
        }
    }
}
