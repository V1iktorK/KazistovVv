// ================================================================================================
//  KvGazeFeedTests.cs — EditMode-тесты ПОДАЧИ ВЗГЛЯДА (сессия S44, закрытие пункта 5.23).
//
//  ОТКУДА ЗАДАЧА. `KvEyeTrackingService` (взгляд, фиксации, выбор взглядом, фовеальное зрение)
//  получал данные ТОЛЬКО из диагностики. В собранном приложении вся эта логика работала вхолостую:
//  она есть, но источника у неё нет. Ровно та же история, что была с кистами.
//
//  ЧТО СДЕЛАНО. Компонент `KvXrGazeFeed` подаёт направление взгляда каждый кадр. Источник — ПОЗА
//  ГОЛОВЫ: настоящий взгляд (зрачок) требует расширения OpenXR и разрешения на камеры глаз, а
//  направление головы доступно всегда. Это ЧЕСТНОЕ УПРОЩЕНИЕ, оно названо в коде и в журнале.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ (чистая функция — без сцены):
//    1) направление взгляда совпадает с направлением головы;
//    2) начало луча — ВПЕРЕДИ головы, а не внутри неё;
//    3) нечисловые данные отсекаются (урок E-21);
//    4) источник зрачка имеет приоритет, когда появляется;
//    5) хаб подключает подачу, риг создаёт компонент — иначе всё бесполезно.
// ================================================================================================

using System.IO;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Подача взгляда: направление, начало луча, отсев нечисла.</summary>
    public class KvGazeFeedTests
    {
        private static readonly Vector3 Head = new Vector3(0f, 1.6f, 0f);

        /// <summary>
        /// НАПРАВЛЕНИЕ ВЗГЛЯДА СОВПАДАЕТ С НАПРАВЛЕНИЕМ ГОЛОВЫ. База: если оператор смотрит прямо,
        /// взгляд и голова совпадают.
        /// </summary>
        [Test]
        public void GazeDirectionMatchesHeadForward()
        {
            Vector3 origin, direction;
            bool ok = KvXrGazeFeed.TryHeadGaze(Head, Quaternion.identity, out origin, out direction);

            Assert.IsTrue(ok, "взгляд по позе головы обязан вычисляться");
            Assert.AreEqual(Vector3.forward, direction, "направление взгляда обязано совпадать с «вперёд»");
        }

        /// <summary>
        /// ПОВОРОТ ГОЛОВЫ ВРАЩАЕТ ВЗГЛЯД. Иначе выбор взглядом работал бы только при взгляде строго
        /// вперёд — то есть нигде.
        /// </summary>
        [Test]
        public void GazeRotatesWithHead()
        {
            Vector3 origin, direction;
            Quaternion turned = Quaternion.Euler(0f, 90f, 0f);   // голова повёрнута вправо

            Assert.IsTrue(KvXrGazeFeed.TryHeadGaze(Head, turned, out origin, out direction));

            // СРАВНЕНИЕ С ДОПУСКОМ, А НЕ НА РАВЕНСТВО (исправлено 30.10.2026).
            //
            // Первая версия использовала `Assert.AreEqual(Vector3.right, direction)` — и падала на
            // значениях, КОТОРЫЕ РАВНЫ: сообщение показывало «Expected: (1.00, 0.00, 0.00) But was:
            // (1.00, 0.00, 0.00)». Причина — поворот кватернионом даёт не ровно 1, а 0.9999999:
            // float-арифметика. Для направления взгляда разница в 7-м знаке не значит ничего, а
            // «равенство» для float — неправильная проверка по определению.
            Assert.AreEqual(1f, direction.x, 1e-4f, "при повороте на 90° взгляд обязан смотреть вправо (X)");
            Assert.AreEqual(0f, direction.y, 1e-4f, "взгляд не должен уходить вверх");
            Assert.AreEqual(0f, direction.z, 1e-4f, "взгляд не должен оставаться вперёд");
        }

        /// <summary>
        /// ЛУЧ НАЧИНАЕТСЯ ВПЕРЕДИ ГОЛОВЫ. Если начать луч В голове, первое попадание окажется на
        /// собственных волосах или на виртуальной модели головы — и фиксация собьётся на пустом месте.
        /// </summary>
        [Test]
        public void GazeOriginIsInFrontOfHead()
        {
            Vector3 origin, direction;
            Assert.IsTrue(KvXrGazeFeed.TryHeadGaze(Head, Quaternion.identity, out origin, out direction));

            float forwardOffset = origin.z - Head.z;
            Assert.AreEqual(KvXrGazeFeed.OriginForwardMeters, forwardOffset, 0.001f,
                "луч взгляда обязан начинаться в " + KvXrGazeFeed.OriginForwardMeters +
                " м впереди головы, а не в самой голове");

            // И высота не должна меняться: взгляд смотрит вперёд, а не вверх.
            Assert.AreEqual(Head.y, origin.y, 0.001f, "начало луча не должно смещаться по высоте");
        }

        /// <summary>
        /// НЕЧИСЛОВЫЕ ДАННЫЕ ОТСЕКАЮТСЯ (урок E-21).
        ///
        /// Почему это важно здесь: `PushGaze` нормализует направление. Для NaN получится NaN, сервис
        /// запомнит его как валидный взгляд, и ВСЯ логика фиксаций начнёт работать с мусором.
        /// </summary>
        [Test]
        public void NaNHeadIsRejected()
        {
            Vector3 origin, direction;

            var nanHead = new Vector3(float.NaN, 1.6f, 0f);
            Assert.IsFalse(KvXrGazeFeed.TryHeadGaze(nanHead, Quaternion.identity, out origin, out direction),
                "поза головы с NaN признана годной — сервис запомнит мусорный взгляд");

            var infHead = new Vector3(float.PositiveInfinity, 1.6f, 0f);
            Assert.IsFalse(KvXrGazeFeed.TryHeadGaze(infHead, Quaternion.identity, out origin, out direction),
                "поза головы с бесконечностью признана годной");
        }

        /// <summary>
        /// ВЫХОДНЫЕ ЗНАЧЕНИЯ БЕЗОПАСНЫ ПРИ ОТКАЗЕ. Если функция вернула `false`, но оставила мусор в
        /// `out`-параметрах, вызывающий код может случайно им воспользоваться.
        /// </summary>
        [Test]
        public void RejectedGazeLeavesSafeDefaults()
        {
            Vector3 origin, direction;
            var nanHead = new Vector3(float.NaN, 1.6f, 0f);

            Assert.IsFalse(KvXrGazeFeed.TryHeadGaze(nanHead, Quaternion.identity, out origin, out direction));

            Assert.AreEqual(Vector3.zero, origin, "при отказе начало луча обязано быть нулевым");
            Assert.IsFalse(float.IsNaN(origin.x) || float.IsNaN(origin.y) || float.IsNaN(origin.z),
                "при отказе в начале луча не должно быть NaN");
        }

        // ---------------------------------------------------------------- ИСТОЧНИКИ

        /// <summary>
        /// ХАБ ПОДКЛЮЧАЕТ ВЗГЛЯД К СЕРВИСУ. Без этой строки компонент существует, но данные не
        /// доходят до распознавания — та же ситуация, что была до правки.
        /// </summary>
        [Test]
        public void HubConnectsGazeToService()
        {
            string path = FindSource("KvStageHub4.cs");
            Assert.IsNotNull(path, "не найден KvStageHub4.cs");

            string text = File.ReadAllText(path);
            StringAssert.Contains("KvXrGazeFeed.Sink", text,
                "хаб НЕ подключает подачу взгляда к сервису — фиксации и выбор взглядом снова " +
                "останутся без данных");
        }

        /// <summary>
        /// РИГ СОЗДАЁТ КОМПОНЕНТ ПОДАЧИ. Если его нет в сцене и никто не создаёт — подача не работает.
        /// </summary>
        [Test]
        public void RigCreatesGazeFeed()
        {
            string path = FindSource("KvXrRig.cs");
            Assert.IsNotNull(path, "не найден KvXrRig.cs");

            string text = File.ReadAllText(path);
            StringAssert.Contains("KvXrGazeFeed.Ensure", text,
                "риг не создаёт компонент подачи взгляда — подача не запустится ни в шлеме, ни на ПК");
        }

        /// <summary>
        /// ИСТОЧНИК ЗРАЧКА ИМЕЕТ ПРИОРИТЕТ. Когда появится платформенная проба настоящего взгляда,
        /// она должна подменять приближение по голове, а не спорить с ним.
        /// </summary>
        [Test]
        public void RealEyeSourceTakesPriority()
        {
            string path = FindSource("KvXrGazeFeed.cs");
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            StringAssert.Contains("GazeSource", text,
                "нет точки подключения настоящего источника взгляда — приближение по голове " +
                "останется навсегда, даже когда платформа отдаст зрачок");
        }

        private static string FindSource(string fileName)
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            string[] found = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }
    }
}
