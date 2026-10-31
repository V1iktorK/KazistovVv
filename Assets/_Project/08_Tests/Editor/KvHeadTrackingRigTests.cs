// ================================================================================================
//  KvHeadTrackingRigTests.cs — EditMode-тесты ПРИМЕНЕНИЯ ПОЗЫ ГОЛОВЫ К КАМЕРЕ.
//
//  ИСТОРИЯ ЭТОГО ФАЙЛА (31.10.2026, живой шлем) — пример того, как НЕ НАДО ДЕЛАТЬ ВЫВОДЫ.
//
//  ЧТО УВИДЕЛ ОПЕРАТОР: «камера перевёрнута и не поворачивается вместе с головой», «всё колбасит»,
//  «меню далеко».
//
//  ЖУРНАЛ ШЛЕМА:
//      Камера следует за головой: поза (-0.41, 0.92, 0.32) · высота 0,92 м
//      проба ориентации: курс шлема 0.0° → 192.6° → 185.3° → 96.5° → 5.7° → 277.4°
//      панель от глаз: 1.15 м → 3.65 м → 48.73 м → 52.30 м
//
//  ЧТО Я РЕШИЛ (НЕВЕРНО): «поза уже переведена в мир через `ToWorld`, а камера лежит внутри рига —
//  значит риг применяется дважды». Написал «исправление» с `InverseTransformPoint` и тесты,
//  которые «доказывали» причину.
//
//  ЧТО ПОКАЗАЛИ ТЕСТЫ: `Transform.SetPositionAndRotation` для ДОЧЕРНЕГО объекта задаёт МИРОВЫЕ
//  координаты (движок сам пересчитывает в локальные). Двойного применения не было НИКОГДА, и
//  ошибка промаха в тесте была ровно 0,0 м вместо ожидаемых 26 м.
//
//  ЧТО СДЕЛАНО: «исправление» откачено коммитом `29f223a`. Причина дефекта ОСТАЁТСЯ НЕИЗВЕСТНОЙ —
//  и это записано честно, а не подменено красивой версией.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ ТЕПЕРЬ: только то, что доказано.
//    1) поза головы действительно применяется к камере каждый кадр;
//    2) `SetPositionAndRotation` задаёт мировую позу (поведение движка — проверяется числом);
//    3) `ToWorld` переводит в мир (от него зависят лучи, руки и панель).
//
//  ЧЕГО ЗДЕСЬ НЕТ: утверждения о причине дефекта. Пока причина не найдена, тест на неё был бы
//  «зелёным по построению» — ровно то, от чего предостерегает урок E-16.
// ================================================================================================

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Поза головы применяется к камере; проверяется только доказанное.</summary>
    public class KvHeadTrackingRigTests
    {
        private static string Find(string fileName)
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            string[] found = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }

        private static string BodyOf(string fileName, string signature)
        {
            string path = Find(fileName);
            Assert.IsNotNull(path, "не найден " + fileName);

            string text = File.ReadAllText(path);
            int start = text.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.Greater(start, 0, "в " + fileName + " нет " + signature);

            int open = text.IndexOf('{', start);
            int depth = 1, at = open + 1;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }
            return text.Substring(start, at - start);
        }

        /// <summary>
        /// ПОЗА ГОЛОВЫ ПРИМЕНЯЕТСЯ К КАМЕРЕ. Это единственное, что точно работает: без этого
        /// мир вообще не следовал бы за головой.
        /// </summary>
        [Test]
        public void HeadPoseIsAppliedToCamera()
        {
            string body = BodyOf("KvXrHeadTracking.cs", "void ApplyFromTracking");

            StringAssert.Contains("SetPositionAndRotation", body,
                "поза головы не применяется к камере — следование за головой сломано");
            StringAssert.Contains("TryHeadPose", body,
                "поза берётся не из системы ввода — появится второй источник истины");
        }

        /// <summary>
        /// ПРИ ОТСУТСТВИИ ТРЕКИНГА КАМЕРА НЕ ПЕРЕСТАВЛЯЕТСЯ. Иначе она прыгнет в ноль — а это ровно
        /// та точка на полу, из которой оператор и смотрел (так уже было, это записано в коде).
        /// </summary>
        [Test]
        public void CameraIsNotMovedWithoutTracking()
        {
            string body = BodyOf("KvXrHeadTracking.cs", "void ApplyFromTracking");

            // Отказ обязан быть ДО записи позы.
            int refuse = body.IndexOf("return;", System.StringComparison.Ordinal);
            int apply = body.IndexOf("SetPositionAndRotation", System.StringComparison.Ordinal);

            Assert.Greater(refuse, 0, "нет ветки отказа при отсутствии трекинга");
            Assert.Greater(apply, refuse,
                "поза применяется РАНЬШЕ отказа — при потере трекинга камера прыгнет в ноль");
        }

        // ---------------------------------------------------------------- ПОВЕДЕНИЕ ДВИЖКА

        /// <summary>
        /// `SETPOSITIONANDROTATION` ДЛЯ ДОЧЕРНЕГО ОБЪЕКТА ЗАДАЁТ МИРОВУЮ ПОЗУ.
        ///
        /// ЭТО ГЛАВНЫЙ ТЕСТ ФАЙЛА: он проверяет ФАКТ, на котором сломалось моё неверное объяснение.
        /// Я утверждал, что запись в дочерний объект «применяет риг второй раз». Движок делает
        /// иначе: сам пересчитывает мировую позу в локальную. Теперь это закреплено числом,
        /// чтобы никто (включая меня) не повторил ту же ошибку рассуждения.
        /// </summary>
        [Test]
        public void SetPositionAndRotationOnChildSetsWorldPose()
        {
            var rig = new GameObject("rig");
            rig.transform.position = new Vector3(0f, 0f, 26f);

            var cam = new GameObject("cam");
            cam.transform.SetParent(rig.transform, false);

            try
            {
                // Поза в мире — та, что должна получиться.
                Vector3 wanted = new Vector3(-0.4f, 1.6f, 26.3f);
                cam.transform.SetPositionAndRotation(wanted, Quaternion.identity);

                Vector3 got = cam.transform.position;
                Assert.AreEqual(wanted.x, got.x, 0.001f);
                Assert.AreEqual(wanted.y, got.y, 0.001f, "высота глаз обязана быть ровно та, что задали");
                Assert.AreEqual(wanted.z, got.z, 0.001f);

                // И локальная поза при этом ПЕРЕСЧИТАНА движком, а не равна мировой.
                Vector3 local = cam.transform.localPosition;
                Assert.AreEqual(26.3f - 26f, local.z, 0.001f,
                    "движок обязан пересчитать мировую позу в локальную — именно поэтому " +
                    "двойного применения рига не происходит");
            }
            finally
            {
                Object.DestroyImmediate(cam);
                Object.DestroyImmediate(rig);
            }
        }

        /// <summary>
        /// `TOWORLD` ПО-ПРЕЖНЕМУ ПЕРЕВОДИТ В МИР. От него зависят лучи, руки, панель и зазоры —
        /// трогать его ради камеры было бы грубой ошибкой.
        /// </summary>
        [Test]
        public void ToWorldStillConvertsToWorld()
        {
            string body = BodyOf("KvXrInputSource.cs", "static void ToWorld");

            StringAssert.Contains("TransformPoint", body,
                "ToWorld больше не переводит в мир — от него зависят лучи, руки и панель");
            StringAssert.Contains("TrackingOrigin", body,
                "ToWorld не использует начало отслеживания — позы перестанут совпадать с миром");
        }
    }
}
