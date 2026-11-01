// ================================================================================================
//  KvTrackingOriginTests.cs — EditMode-тесты НАЧАЛА ОТСЛЕЖИВАНИЯ (сессия S44, живой шлем).
//
//  ОТКУДА ЗАДАЧА. 01.11.2026 оператор: «всё крутится», «картинка не стабильная».
//  Журнал шлема: панель от глаз 1.12 м → 4.49 м → 155 м → 394 м, при том что САМА ПАНЕЛЬ
//  не двигалась (`панель в (0.08, 0.88, 0.68)` во всех строках). То есть уезжала КАМЕРА.
//
//  ПРИЧИНА. Компонент `KvXrRig` висит на объекте `XrCamera` — НА САМОЙ КАМЕРЕ (проверено по
//  файлу сцены). А камера — ребёнок объекта `XrRig`. Прежняя строка:
//      KvXrInputSource.TrackingOrigin = transform;   // ← это КАМЕРА
//  означала, что `ToWorld` переводит позу трекинга в мир ОТНОСИТЕЛЬНО КАМЕРЫ. Но камеру же и
//  ставят по этой позе. Получался круг: камера := поза + позиция_камеры → камера уезжает →
//  следующая поза считается от уехавшей камеры → ещё дальше. Накопление каждый кадр.
//
//  ЧТО ПРОВЕРЯЕТСЯ (чистая функция — без сцены):
//    1) компонент на камере → начало отслеживания = РОДИТЕЛЬ (риг), а не камера;
//    2) компонент не на камере → начало отслеживания = сам объект (всё верно);
//    3) камера без родителя → откат на себя (не падаем с null);
//    4) null → null;
//    5) и главное — ПРОВЕРКА ЧИСЛОМ: если начало отслеживания на камере, поза накапливается,
//       а если на риге — нет. Это прямое доказательство причины.
// ================================================================================================

using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Начало отслеживания — риг, а не камера.</summary>
    public class KvTrackingOriginTests
    {
        /// <summary>
        /// КОМПОНЕНТ НА КАМЕРЕ → НАЧАЛО ОТСЛЕЖИВАНИЯ = РОДИТЕЛЬ.
        ///
        /// Это и есть исправление: в сцене `XrMain` компонент лежит на `XrCamera`, а риг — его
        /// родитель. Если вернуть `self`, поза начнёт отсчитываться от глаз оператора.
        /// </summary>
        [Test]
        public void CameraObjectResolvesToParentRig()
        {
            var rig = new GameObject("XrRig");
            var camera = new GameObject("XrCamera");
            camera.transform.SetParent(rig.transform, false);

            try
            {
                Transform origin = KvXrRig.ResolveTrackingOrigin(camera.transform, selfHasCamera: true);

                Assert.AreEqual(rig.transform, origin,
                    "компонент на камере обязан взять РИГ (родителя), иначе поза отсчитывается от глаз " +
                    "оператора и мир раскручивается");
                Assert.AreNotEqual(camera.transform, origin,
                    "началом отслеживания НЕ ДОЛЖНА быть сама камера — это и был дефект");
            }
            finally
            {
                Object.DestroyImmediate(camera);
                Object.DestroyImmediate(rig);
            }
        }

        /// <summary>
        /// КОМПОНЕНТ НЕ НА КАМЕРЕ → НАЧАЛО ОТСЛЕЖИВАНИЯ = САМ ОБЪЕКТ. Если компонент уже стоит
        /// на риге, подниматься выше некуда и не нужно.
        /// </summary>
        [Test]
        public void RigObjectResolvesToItself()
        {
            var rig = new GameObject("XrRig");
            var child = new GameObject("SomethingElse");
            child.transform.SetParent(rig.transform, false);

            try
            {
                Assert.AreEqual(rig.transform,
                    KvXrRig.ResolveTrackingOrigin(rig.transform, selfHasCamera: false),
                    "объект без камеры обязан остаться началом отслеживания");

                // И даже если у него есть родитель — подниматься не надо: камеры на нём нет.
                Assert.AreEqual(child.transform,
                    KvXrRig.ResolveTrackingOrigin(child.transform, selfHasCamera: false),
                    "объект без камеры не должен подменяться родителем");
            }
            finally
            {
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(rig);
            }
        }

        /// <summary>
        /// КАМЕРА БЕЗ РОДИТЕЛЯ. Откат на себя: лучше отсчитывать от камеры, чем получить `null`
        /// и потерять все позы трекинга (лучи, руки, панель — всё встанет).
        /// </summary>
        [Test]
        public void CameraWithoutParentFallsBackToItself()
        {
            var camera = new GameObject("LonelyCamera");

            try
            {
                Assert.AreEqual(camera.transform,
                    KvXrRig.ResolveTrackingOrigin(camera.transform, selfHasCamera: true),
                    "камера без родителя обязана вернуть себя, а не null");
            }
            finally
            {
                Object.DestroyImmediate(camera);
            }
        }

        /// <summary>
        /// NULL НЕ ПАДАЕТ. Метод публичный и может быть вызван с пустым аргументом.
        /// </summary>
        [Test]
        public void NullIsHandled()
        {
            Assert.IsNull(KvXrRig.ResolveTrackingOrigin(null, selfHasCamera: true));
            Assert.IsNull(KvXrRig.ResolveTrackingOrigin(null, selfHasCamera: false));
        }

        // ---------------------------------------------------------------- ДОКАЗАТЕЛЬСТВО ЧИСЛОМ

        /// <summary>
        /// ГЛАВНЫЙ ТЕСТ ФАЙЛА: НАЧАЛО ОТСЛЕЖИВАНИЯ НА КАМЕРЕ ДАЁТ НАКОПЛЕНИЕ, НА РИГЕ — НЕТ.
        ///
        /// Воспроизводим цикл «поставить камеру по позе из ToWorld» много раз подряд и смотрим,
        /// уезжает ли камера. Это прямое доказательство причины, а не рассуждение о ней
        /// (урок E-23: гипотезу надо проверять числом).
        /// </summary>
        [Test]
        public void CameraAsOriginAccumulatesError()
        {
            var rig = new GameObject("XrRig");
            rig.transform.position = new Vector3(0f, 0f, 0f);

            var camera = new GameObject("XrCamera");
            camera.transform.SetParent(rig.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);   // глаза на 1.6 м

            try
            {
                // Поза от устройства — постоянная, оператор не двигается.
                Vector3 tracked = new Vector3(0.1f, 1.6f, 0.2f);

                // --- ПРАВИЛЬНО: начало отслеживания = РИГ ---
                for (int i = 0; i < 10; i++)
                {
                    Vector3 world = rig.transform.TransformPoint(tracked);
                    camera.transform.SetPositionAndRotation(world, Quaternion.identity);
                }
                Vector3 correct = camera.transform.position;

                // --- НЕПРАВИЛЬНО: начало отслеживания = КАМЕРА (как было) ---
                camera.transform.SetParent(rig.transform, false);
                camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);

                for (int i = 0; i < 10; i++)
                {
                    Vector3 world = camera.transform.TransformPoint(tracked);   // ← отсчёт от камеры
                    camera.transform.SetPositionAndRotation(world, Quaternion.identity);
                }
                Vector3 wrong = camera.transform.position;

                // Правильный вариант обязан остаться на месте.
                Assert.AreEqual(1.6f, correct.y, 0.001f,
                    "при начале отслеживания на риге высота глаз обязана остаться 1,60 м");

                // Неправильный — уехать. Если НЕ уехал, значит причина дефекта ДРУГАЯ.
                float drift = Vector3.Distance(correct, wrong);
                Assert.Greater(drift, 1f,
                    "начало отслеживания на камере обязано давать заметный уход (получилось " +
                    drift.ToString("0.00") + " м) — иначе объяснение причины неверно");
            }
            finally
            {
                Object.DestroyImmediate(camera);
                Object.DestroyImmediate(rig);
            }
        }
    }
}
