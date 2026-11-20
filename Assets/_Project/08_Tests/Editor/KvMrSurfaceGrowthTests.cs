// ================================================================================================
//  KvMrSurfaceGrowthTests.cs — НАКОПЛЕНИЕ ПОВЕРХНОСТЕЙ И РИТМ ЖУРНАЛА (20.11.2026).
//
//  ОТКУДА ЗАДАЧА. Журнал реального прогона MR на PICO (logcat_MR_headset_20112026.log):
//  старт 86 FPS, через 20 с — 3 FPS, а число поверхностей росло КАЖДУЮ СЕКУНДУ:
//  420 → 1204 → 2799 → 9383 при пределе `maxMeshes = 400`. То есть каждый кусок комнаты
//  превращался в ~20 копий объекта физики и в ~20 «плиток покрытия» у рисовальщика.
//
//  ПРИЧИНА (найдена в коде): `MeshChangeState.Updated` для УЖЕ известного меша вызывал
//  `BuildMesh`, а тот создавал НОВЫЙ `GameObject` + `MeshCollider` и перезаписывал запись в
//  словаре — старый коллайдер оставался в `realWorldColliders` навсегда.
//
//  ЧТО КРЕПИТСЯ ЗДЕСЬ:
//    1) повторная генерация меша НЕ создаёт второго объекта (и не увеличивает число поверхностей);
//    2) перестройка известного меша идёт не чаще интервала (не каждый кадр);
//    3) покрытие перерисовывается и пишется в журнал по РАЗНЫМ интервалам, и все периодические
//       строки держат ритм «раз в 5 с», а не «раз в секунду»;
//    4) метрика кадров печатается строкой вида «[KvXr] MR: FPS N · цель M»;
//    5) масштаб отрисовки НИКОГДА не снижается автоматически (запрет владельца).
// ================================================================================================

using System.IO;
using KazistovVvFeatures;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR;

namespace KazistovVvTests
{
    /// <summary>Накопление поверхностей MR, интервалы журнала, метрика FPS и политика масштаба.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvMrSurfaceGrowthTests
    {
        // ==================================================== 1. НЕТ ВТОРОГО ОБЪЕКТА ОДНОГО МЕША

        /// <summary>
        /// ПОВТОРНАЯ ГЕНЕРАЦИЯ МЕША ПЕРЕИСПОЛЬЗУЕТ ТОТ ЖЕ КОЛЛАЙДЕР.
        ///
        /// Что ловит: возврат к старому `BuildMesh`, который на каждый `Updated` делал новый
        /// `GameObject` и перетирал словарь. Именно так журнал шлема дошёл до 9383 коллайдеров
        /// при пределе 400 мешей, а покрытие — до 9329 плиток и 3 FPS.
        /// </summary>
        [Test]
        public void MeshObject_IsReused_SoSurfaceCountNeverGrows()
        {
            var host = new GameObject("KvMrSurfaceGrowthTests_host");
            try
            {
                KvMrSurfaces surfaces = host.AddComponent<KvMrSurfaces>();
                MeshId id = default(MeshId);

                MeshCollider first = surfaces.EnsureMeshObject(id);
                MeshCollider second = surfaces.EnsureMeshObject(id);

                Assert.IsNotNull(first, "объект физики обязан создаться");
                Assert.AreSame(first, second,
                    "повторная генерация того же MeshId обязана вернуть ТОТ ЖЕ коллайдер — иначе " +
                    "старый останется в списке поверхностей навсегда (корень деградации MR 20.11.2026)");
                Assert.AreEqual(1, surfaces.MeshObjectCount,
                    "объектов физики обязано быть не больше числа уникальных мешей");

                Mesh mesh = surfaces.EnsureMeshData(id);
                Assert.AreSame(mesh, surfaces.EnsureMeshData(id), "mesh на один MeshId — тоже один");
                FillTriangle(mesh);

                Assert.IsTrue(surfaces.RecordGeneratedMesh(id, first, mesh),
                    "первая генерация — новая поверхность");
                Assert.IsFalse(surfaces.RecordGeneratedMesh(id, second, mesh),
                    "повторная генерация НЕ должна считаться новой поверхностью");
                Assert.AreEqual(1, surfaces.ColliderCount,
                    "число поверхностей обязано остаться 1: дубликаты и были причиной падения FPS");
                Assert.AreEqual(1, surfaces.MeshObjectCount,
                    "и объектов физики по-прежнему 1 — словарь перезаписи не порождает второго");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// ПЕРЕСТРОЙКА ИЗВЕСТНОГО МЕША РАЗРЕШЕНА НЕ ЧАЩЕ ИНТЕРВАЛА — чистое правило.
        ///
        /// Что ловит: возврат «собираем каждый раз, когда подсистема прислала Updated» — это и
        /// есть варка физики на каждом кадре (MeshCollider-cooking) плюс путь к дубликатам.
        /// </summary>
        [Test]
        public void MeshRebuild_IsThrottled_ButNewMeshIsBuiltAtOnce()
        {
            // Новый меш: строим сразу, иначе лучу не во что попадать.
            Assert.IsTrue(KvMrSurfaces.ShouldBuildMesh(known: false, pending: false,
                now: 100f, lastBuiltAt: float.NaN, refreshSeconds: 1f));

            // Уже генерируется — повтор не нужен.
            Assert.IsFalse(KvMrSurfaces.ShouldBuildMesh(known: true, pending: true,
                now: 100f, lastBuiltAt: 50f, refreshSeconds: 1f));

            // Известен, но интервал не прошёл — молчим (раньше здесь создавался дубликат).
            Assert.IsFalse(KvMrSurfaces.ShouldBuildMesh(known: true, pending: false,
                now: 100.5f, lastBuiltAt: 100f, refreshSeconds: 1f));

            // Известен и интервал прошёл — перестраиваем (обновление геометрии остаётся).
            Assert.IsTrue(KvMrSurfaces.ShouldBuildMesh(known: true, pending: false,
                now: 102f, lastBuiltAt: 100f, refreshSeconds: 1f));

            // Нулевой интервал — защита от «запретить обновления вовсе» по ошибке.
            Assert.IsTrue(KvMrSurfaces.ShouldBuildMesh(known: true, pending: false,
                now: 100.001f, lastBuiltAt: 100f, refreshSeconds: 0f));

            // Сам интервал обязан быть разумным: меньше 0,5 с — это снова каждый кадр.
            var surfaces = new GameObject("KvMrSurfaceGrowthTests_interval").AddComponent<KvMrSurfaces>();
            try
            {
                Assert.GreaterOrEqual(surfaces.meshRefreshSeconds, 0.5f,
                    "перестройка меша чаще раза в полсекунды не нужна ни оператору, ни качеству");
                Assert.GreaterOrEqual(surfaces.maxMeshes, 1,
                    "предел мешей обязан существовать — это защита памяти");
            }
            finally
            {
                Object.DestroyImmediate(surfaces.gameObject);
            }
        }

        // ==================================================== 2. РИТМ ЖУРНАЛА (СПАМ НЕ ВЕРНЁТСЯ)

        /// <summary>
        /// ПЕРИОДИЧЕСКИЕ СТРОКИ ДЕРЖАТ РИТМ «РАЗ В 5 С», А ПЕРЕРИСОВКА — «РАЗ В СЕКУНДУ».
        ///
        /// Что ловит: возврат интервала 1 с для строк, которые в журнале MR повторялись 137–140
        /// раз за прогон, и одновременное замедление самой отрисовки (эти две вещи раньше
        /// управлялись одной константой). Требование владельца «не чаще раза в секунду» — это
        /// порог, а частые строки — источник работы на главном потоке шлема.
        /// </summary>
        [Test]
        public void PeriodicLogs_AreThrottled_AndRepaintStaysAtOneSecond()
        {
            Assert.GreaterOrEqual(KvMrSurfacePainter.ReportIntervalSeconds, 5f,
                "строка о покрытии обязана уходить не чаще раза в 5 с (в журнале было 137 копий)");
            Assert.GreaterOrEqual(KvMrSurfacePainter.RepaintIntervalSeconds, 1f,
                "перерисовка обязана оставаться не чаще раза в секунду — это требование владельца");
            Assert.LessOrEqual(KvMrSurfacePainter.RepaintIntervalSeconds,
                KvMrSurfacePainter.ReportIntervalSeconds,
                "перерисовка не может быть реже журнала: покрытие обязано обновляться, а писаться — реже");

            Assert.GreaterOrEqual(KvXrFlowBridge.ZoneLogIntervalSeconds, 5f,
                "строка о зонах в журнале повторялась 139 раз — интервал обязан быть не меньше 5 с");
            Assert.GreaterOrEqual(KvXrHands.StateLogIntervalSeconds, 5f,
                "строка о кистях повторялась 74 раза — ритм не чаще 5 с");
            Assert.GreaterOrEqual(KvMrRobotPick.MissReportIntervalSeconds, 5f,
                "«робот НЕ переставлен» чередовалось по двум причинам и печаталось 40 раз");
            Assert.GreaterOrEqual(KvXrRig.FpsReportIntervalSeconds, 5f,
                "метрика кадров — служебная строка, ей хватит одного раза в 5 с");

            // «Стол найден / не найден» считается и печатается по своему интервалу — и он тоже
            // не чаще раза в 5 с (в журнале MR — 140 строк).
            string source = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Project", "01_Scripts", "Xr", "KvMrSurfaces.cs"));
            StringAssert.Contains("RealTableReportSeconds = 5f", source,
                "периодическая строка «РЕАЛЬНЫЙ СТОЛ» обязана быть ужата до 5 с");
        }

        // ==================================================== 3. МЕТРИКА КАДРОВ В ЖУРНАЛЕ

        /// <summary>
        /// СТРОКА О КАДРАХ НАЗЫВАЕТ РЕЖИМ И ДВА ЧИСЛА — ровно то, что просил владелец.
        ///
        /// Форма проверяется здесь, потому что на устройстве её читают глазами: «MR: FPS N» —
        /// это единственный способ увидеть, держится ли 50 FPS, не подключая профайлер.
        /// </summary>
        [Test]
        public void FpsLine_SaysModeAndNumbers()
        {
            Assert.AreEqual("[KvXr] MR: FPS 50 · цель 72", KvXrRig.DescribeFps("MR", 50, 72),
                "форма метрики: режим, измеренные кадры и цель");
            Assert.AreEqual("[KvXr] VR: FPS 86 · цель 72", KvXrRig.DescribeFps("VR", 86, 72));
            Assert.AreEqual("[KvXr] XR: FPS 0 · цель 0", KvXrRig.DescribeFps(null, -5, -1),
                "нечисла обязаны превращаться в нули, а не в минусы");
            Assert.AreEqual("VR", KvXrRig.FpsModeLabel(),
                "без включённой комнаты режим называется VR — метрика не должна врать");
        }

        // ==================================================== 4. КАЧЕСТВО НЕ СНИЖАЕТСЯ

        /// <summary>
        /// МАСШТАБ ОТРИСОВКИ НИКОГДА НЕ СНИЖАЕТСЯ АВТОМАТИЧЕСКИ (запрет владельца).
        ///
        /// Что ловит: возврат адаптивного шага 0,05 — в журнале 20.11.2026 он отработал восемь
        /// раз подряд (0,85 → 0,55) и не поднял ни одного кадра: FPS падал из-за накопления
        /// поверхностей. То есть картинка портилась впустую.
        /// </summary>
        [Test]
        public void RenderScale_MayNotDrop_Automatically()
        {
            Assert.IsFalse(KvGraphicsService.ScaleMayDrop(true, true),
                "масштаб нельзя снижать даже в пресете VR-Ready с включённым адаптивом");
            Assert.IsFalse(KvGraphicsService.ScaleMayDrop(true, false),
                "и вне пресета — тоже: политика не зависит от пресета");
            Assert.IsFalse(KvGraphicsService.ScaleMayDrop(false, false),
                "политика не обходится и выключением адаптивного качества");
            Assert.IsFalse(KvGraphicsService.ScaleDropAllowed,
                "флаг, по которому TickAdaptive решает, обязан быть закрыт");
        }

        // ==================================================== мелочи

        /// <summary>Треугольник для коллайдера: пустой меш Unity не любит (лог-ошибка валит тест).</summary>
        private static void FillTriangle(Mesh mesh)
        {
            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
        }
    }
}
