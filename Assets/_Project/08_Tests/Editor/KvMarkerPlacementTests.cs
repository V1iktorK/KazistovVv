// ================================================================================================
//  KvMarkerPlacementTests.cs — EditMode-тесты МАРКЕРНОЙ ПОСТАНОВКИ (16.11.2026, волна A–D).
//
//  ОТКУДА ЗАДАЧА. Устройство-инициатор волны (Redmi Pad 2 Pro) НЕ ARCore-совместимо:
//  плоскостей 0, трекаблов 0, но кадр живой и повороты камеры отдаются. Поэтому стол ставится
//  по QR-маркеру: четыре угла кода + физический размер маркера → гомография (ДЛТ 8×8) →
//  поза → верх стола в плоскости маркера; робот — штатным путём `KvArWorld`.
//
//  ЧТО ПРОВЕРЯЕТСЯ (математика и статусы на СИНТЕТИКЕ — железо и сцены проекта не нужны):
//    • поза по синтетическим углам: фронтально, со смещением в кадре (косизна = atan(0.25) =
//      14.04°), с наклоном 35° и фолбэк-интринсиками (fx ≈ ширина кадра) — эталонные числа
//      взяты из standalone-харнесса `scratch/marker_harness` (31/31);
//    • ЧЕСТНЫЕ ОТКАЗЫ: маркер не виден / слишком мал / слишком под углом / вырожден;
//    • `KvArPlacement.LastMarkerStatus` называет причину отказа, а успех — строка
//      `KvMarkerPose.SuccessStatus` («стол поставлен по маркеру: дистанция X м, наклон Y°»);
//    • `BuildMarkerSurface` строит площадку из позы маркера БЕЗ плоскости ARCore;
//    • команда `ar.place.marker` лежит в живом реестре: не заглушка, с обработчиком;
//    • детектор QR: синтетический код находится с углами в допуске, пустой кадр отвечает
//      отказом, а связка «детектор → поза» даёт дистанцию 0.80 м и косизну 11.8°.
//
//  ГРАНИЦА ЧЕСТНОСТИ (P4-3). Файл НЕ читает тексты исходников: всё проверяется по поведению
//  живого кода. `SetUp`/`TearDown` возвращают пустую сцену, реестр команд — к снимку, снятому
//  до штатной регистрации, иначе следующая фикстура увидит чужие обработчики.
// ================================================================================================

using System.Collections.Generic;
using KazistovVvAr;
using KazistovVvFeatures;
using KazistovVvUI;
using KazistovVvXr;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>
    /// Маркерная постановка: поза QR по четырём углам, честные отказы, статусы, реестр команд.
    /// </summary>
    [Category(KvTestCategories.Fast)]
    public class KvMarkerPlacementTests
    {
        /// <summary>
        /// СНИМОК РЕЕСТРА ДО штатной регистрации (возвращается в TearDown).
        ///
        /// ЗАЧЕМ. `KvCommands` — общий на весь прогон статик: регистрация хаба кладёт в него
        /// команды с обработчиками, считающими состояние, недоступное в EditMode. Оставить такое
        /// следующим фикстурам нельзя — снимок и есть «пустая сцена» для реестра
        /// (тот же приём, что в `KvGapClosureTests`).
        /// </summary>
        private static List<KvCommand> registrySnapshot;

        /// <summary>
        /// ПУСТАЯ СЦЕНА ПЕРЕД КАЖДЫМ ТЕСТОМ. Фикстуры до нас оставляют в редакторе свои объекты;
        /// без сброса проверка «камеры AR в сцене нет» проходила бы на чужой сцене и врала.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            if (registrySnapshot == null)
            {
                registrySnapshot = new List<KvCommand>();
                foreach (KvCommand command in KvCommands.All) registrySnapshot.Add(command);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>
        /// ПУСТАЯ СЦЕНА И СНИМОК РЕЕСТРА ПОСЛЕ КАЖДОГО ТЕСТА: честный возврат к состоянию,
        /// в котором фикстуру застала, — независимо от того, где тест упал.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            // Штатная регистрация команд строит окно хаба: убитое окно не должно пережить тест.
            KvWindowBelt.Unregister(KvFeatureWindow.WindowId);
            if (registrySnapshot != null)
            {
                KvCommands.Clear();
                foreach (KvCommand command in registrySnapshot) KvCommands.Register(command);
                registrySnapshot = null;
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // ================================================================ вспомогательное

        /// <summary>Настоящие интринсики кадра 640×360 (эталон харнесса).</summary>
        private static KvMarkerIntrinsics ExactIntrinsics()
        {
            return new KvMarkerIntrinsics
            {
                fx = 800f, fy = 800f, cx = 320f, cy = 180f, approximate = false
            };
        }

        /// <summary>
        /// ОБРАТНАЯ ПРОЕКЦИЯ: четыре угла маркера в пиксели кадра.
        /// `u = fx·x/z + cx`, `v = cy − fy·y/z` — та же система, в которой считает `KvMarkerPose`.
        /// </summary>
        private static Vector2[] Project(Vector3 pos, Quaternion rot, float size,
            KvMarkerIntrinsics k, Vector3 camPos, Quaternion camRot)
        {
            float a = size * 0.5f;
            Vector3[] obj =
            {
                new Vector3(-a, a, 0f), new Vector3(a, a, 0f),
                new Vector3(a, -a, 0f), new Vector3(-a, -a, 0f)
            };
            Quaternion inv = Quaternion.Inverse(camRot);
            Vector2[] img = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 world = pos + rot * obj[i];
                Vector3 cam = inv * (world - camPos);
                img[i] = new Vector2(k.fx * cam.x / cam.z + k.cx, k.cy - k.fy * cam.y / cam.z);
            }
            return img;
        }

        /// <summary>Квадрат в пикселях кадра (углы по порядку TL,TR,BR,BL).</summary>
        private static Vector2[] Square(float cx, float cy, float side)
        {
            float h = side * 0.5f;
            return new[]
            {
                new Vector2(cx - h, cy - h), new Vector2(cx + h, cy - h),
                new Vector2(cx + h, cy + h), new Vector2(cx - h, cy + h)
            };
        }

        /// <summary>Рисует один «ушко» QR 7×7 модулей в Color32-кадре.</summary>
        private static void DrawFinder(Color32[] px, int w, int topLeftX, int topLeftY, int modulePx)
        {
            for (int dy = 0; dy <= 6; dy++)
            for (int dx = 0; dx <= 6; dx++)
            {
                int dist = Mathf.Max(Mathf.Abs(dx - 3), Mathf.Abs(dy - 3));
                bool dark = dist >= 3 || dist <= 1;
                if (!dark) continue;
                int x0 = topLeftX + dx * modulePx;
                int y0 = topLeftY + dy * modulePx;
                for (int y = y0; y < y0 + modulePx; y++)
                for (int x = x0; x < x0 + modulePx; x++)
                    px[y * w + x] = new Color32(0, 0, 0, 255);
            }
        }

        /// <summary>
        /// СИНТЕТИЧЕСКИЙ QR: три ушка 1:3:1 по углам кода. Ожидаемые углы кода — через out
        /// (TL, TR, BR, BL — в системе кадра, той же, что у интринсик).
        /// </summary>
        private static Color32[] DrawQr(int w, int h, int codeX, int codeY, int modulePx, int modules,
            out Vector2 tl, out Vector2 tr, out Vector2 br, out Vector2 bl)
        {
            Color32[] px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);

            int side = modules * modulePx;
            int finderSide = 7 * modulePx;
            DrawFinder(px, w, codeX, codeY, modulePx);                       // TL
            DrawFinder(px, w, codeX + side - finderSide, codeY, modulePx);    // TR
            DrawFinder(px, w, codeX, codeY + side - finderSide, modulePx);    // BL

            tl = new Vector2(codeX, codeY);
            tr = new Vector2(codeX + side, codeY);
            br = new Vector2(codeX + side, codeY + side);
            bl = new Vector2(codeX, codeY + side);
            return px;
        }

        // ================================================================ поза по синтетическим углам

        /// <summary>
        /// ФРОНТАЛЬНЫЙ МАРКЕР: поза восстанавливается точно, нормаль смотрит на камеру.
        /// Маркер перед камерой на 1 м, развёрнут лицевой стороной к ней (`R_y(180)`).
        /// </summary>
        [Test]
        public void FrontalMarker_RecoversPositionTiltAndNormal()
        {
            Vector3 mpos = new Vector3(0f, 0f, 1f);
            Quaternion mrot = Quaternion.AngleAxis(180f, Vector3.up);
            Vector2[] corners = Project(mpos, mrot, 0.1f, ExactIntrinsics(), Vector3.zero,
                Quaternion.identity);

            KvMarkerPoseResult r = KvMarkerPose.Compute(corners, 0.1f, ExactIntrinsics());

            Assert.AreEqual(KvMarkerPoseStatus.Ok, r.status,
                "фронтальный маркер обязан считаться без предупреждений: " + r.reason);
            Assert.IsTrue(r.usable, "поза пригодна к постановке");
            Assert.That(r.position.x, Is.EqualTo(mpos.x).Within(0.005f), "X восстановлен неточно");
            Assert.That(r.position.y, Is.EqualTo(mpos.y).Within(0.005f), "Y восстановлен неточно");
            Assert.That(r.position.z, Is.EqualTo(mpos.z).Within(0.01f), "Z восстановлен неточно");
            Assert.That(r.tiltDegrees, Is.LessThan(1f),
                "фронтальный маркер не может иметь косизну: " + r.tiltDegrees.ToString("F2"));
            Vector3 normal = r.rotation * Vector3.forward;
            Assert.Greater(Vector3.Dot(normal, Vector3.back), 0.999f,
                "нормаль обязана смотреть на камеру (иначе развёртка зеркальная): " + normal);
            Assert.That(r.distance, Is.EqualTo(mpos.magnitude).Within(0.01f),
                "дистанция — это длина вектора до маркера");
        }

        /// <summary>
        /// КОСИЗНА = УГОЛ МЕЖДУ НОРМАЛЬЮ И ЛУЧОМ: фронтально-параллельный маркер, смещённый
        /// в кадре, даёт ровно `atan(0.25/1) = 14.04°` — а не «маркер под углом».
        /// </summary>
        [Test]
        public void OffCenterMarker_TiltIsAngleToLineOfSight()
        {
            Vector3 mpos = new Vector3(0f, -0.25f, 1f);
            Quaternion mrot = Quaternion.AngleAxis(180f, Vector3.up);
            Vector2[] corners = Project(mpos, mrot, 0.1f, ExactIntrinsics(), Vector3.zero,
                Quaternion.identity);

            KvMarkerPoseResult r = KvMarkerPose.Compute(corners, 0.1f, ExactIntrinsics());

            Assert.AreEqual(KvMarkerPoseStatus.Ok, r.status, r.reason);
            Assert.That(r.tiltDegrees, Is.EqualTo(14.036f).Within(0.5f),
                "косизна обязана быть atan(0.25): " + r.tiltDegrees.ToString("F2"));
            Assert.That(r.position.z, Is.EqualTo(1f).Within(0.01f), "глубина восстановлена");
            Assert.That(r.position.y, Is.EqualTo(-0.25f).Within(0.01f), "высота восстановлена");
        }

        /// <summary>НАКЛОН 35°: угол косизны восстанавливается, поза остаётся пригодной.</summary>
        [Test]
        public void TiltedMarker35_RecoversObliqueness()
        {
            Vector3 mpos = new Vector3(0f, 0f, 1f);
            Quaternion mrot = Quaternion.AngleAxis(35f, Vector3.right) *
                              Quaternion.AngleAxis(180f, Vector3.up);
            Vector2[] corners = Project(mpos, mrot, 0.1f, ExactIntrinsics(), Vector3.zero,
                Quaternion.identity);

            KvMarkerPoseResult r = KvMarkerPose.Compute(corners, 0.1f, ExactIntrinsics());

            Assert.AreEqual(KvMarkerPoseStatus.Ok, r.status, r.reason);
            Assert.That(r.tiltDegrees, Is.EqualTo(35f).Within(1.5f),
                "наклон 35° восстановлен неточно: " + r.tiltDegrees.ToString("F2"));
            Assert.That(r.position.z, Is.EqualTo(1f).Within(0.02f), "глубина восстановлена");
            Assert.IsTrue(r.usable, "35° — в допуске (60°), позой можно пользоваться");
        }

        /// <summary>
        /// ФОЛБЭК-ИНТРИНСИКИ: поза есть, но статус ЧЕСТНО называет её приблизительной,
        /// а `requireExactIntrinsics` превращает её в отказ. Грубость фолбэка — в ДИСТАНЦИИ
        /// (fx 1280 вместо 900 → z ≈ 1.42 вместо 1.00): именно это и есть «приблизительность».
        /// </summary>
        [Test]
        public void FallbackIntrinsics_PoseIsApproximateAndStrictModeRefuses()
        {
            KvMarkerIntrinsics fb = KvMarkerIntrinsics.FromFrame(1280f, 720f);
            KvMarkerIntrinsics trueK = new KvMarkerIntrinsics
            {
                fx = 900f, fy = 900f, cx = 640f, cy = 360f, approximate = false
            };
            Vector3 mpos = new Vector3(0f, 0f, 1f);
            Quaternion mrot = Quaternion.AngleAxis(180f, Vector3.up);
            Vector2[] corners = Project(mpos, mrot, 0.1f, trueK, Vector3.zero, Quaternion.identity);

            KvMarkerPoseResult r = KvMarkerPose.Compute(corners, 0.1f, fb);

            Assert.AreEqual(KvMarkerPoseStatus.IntrinsicsApproximate, r.status,
                "фолбэк обязан называться приблизительным, а не Ok: " + r.reason);
            Assert.IsTrue(r.usable, "приблизительная поза по умолчанию пригодна (без запрета)");
            Assert.IsTrue(r.intrinsicsApproximate, "признак точности поднят");
            StringAssert.Contains("интринсики приблизительные", r.reason,
                "причина обязана назвать неточность: " + r.reason);
            Assert.That(r.tiltDegrees, Is.LessThan(3f), "наклон фолбэка всё же грубый, но приемлемый");
            Assert.That(r.position.z, Is.GreaterThan(1.2f).And.LessThan(1.7f),
                "дистанция фолбэка грубая (fx 1280 вместо 900), но вменяемая: " +
                r.position.z.ToString("F3"));

            KvMarkerPoseResult strict = KvMarkerPose.Compute(corners, 0.1f, fb,
                requireExactIntrinsics: true);
            Assert.IsFalse(strict.usable, "при запрете точности приблизительная поза не применяется");
            Assert.AreEqual(KvMarkerPoseStatus.IntrinsicsApproximate, strict.status, strict.reason);
        }

        // ================================================================ честные отказы

        /// <summary>УГЛОВ НЕТ (или их меньше четырёх) — «маркер не виден», а не нулевая поза.</summary>
        [Test]
        public void MissingCorners_RefuseAsMarkerNotVisible()
        {
            KvMarkerPoseResult r = KvMarkerPose.Compute(null, 0.1f, ExactIntrinsics());
            Assert.AreEqual(KvMarkerPoseStatus.MarkerNotVisible, r.status, r.reason);
            Assert.IsFalse(r.usable, "без углов постановки быть не может");
            StringAssert.Contains("маркер не виден", r.reason, r.reason);

            KvMarkerPoseResult shortArray = KvMarkerPose.Compute(new Vector2[2], 0.1f,
                ExactIntrinsics());
            Assert.AreEqual(KvMarkerPoseStatus.MarkerNotVisible, shortArray.status,
                "два угла — это не маркер");
        }

        /// <summary>МАРКЕР МЕНЬШЕ ПОРОГА В ПИКСЕЛЯХ — отказ до всякой алгебры.</summary>
        [Test]
        public void TinyMarker_RefusesBelowPixelThreshold()
        {
            KvMarkerPoseResult r = KvMarkerPose.Compute(Square(320f, 180f, 20f), 0.1f,
                ExactIntrinsics());
            Assert.AreEqual(KvMarkerPoseStatus.MarkerTooSmall, r.status, r.reason);
            Assert.IsFalse(r.usable, "из 20 пикселей поза не считается");
            StringAssert.Contains("слишком мал", r.reason, r.reason);
            Assert.That(r.markerPixels, Is.EqualTo(20f).Within(0.01f),
                "диагностика обязана показать размер, на котором отказалось");
        }

        /// <summary>СЛИШКОМ КОСО (70° при допуске 60°) — честный отказ с числом.</summary>
        [Test]
        public void ObliqueMarker70_RefusesAboveTiltLimit()
        {
            Vector3 mpos = new Vector3(0f, 0f, 1f);
            Quaternion mrot = Quaternion.AngleAxis(70f, Vector3.right) *
                              Quaternion.AngleAxis(180f, Vector3.up);
            Vector2[] corners = Project(mpos, mrot, 0.1f, ExactIntrinsics(), Vector3.zero,
                Quaternion.identity);

            KvMarkerPoseResult r = KvMarkerPose.Compute(corners, 0.1f, ExactIntrinsics());

            Assert.AreEqual(KvMarkerPoseStatus.MarkerTooOblique, r.status, r.reason);
            Assert.IsFalse(r.usable, "косая поза ненадёжна — стол встал бы криво");
            StringAssert.Contains("слишком под углом", r.reason, r.reason);
        }

        /// <summary>КОЛЛИНЕАРНЫЕ УГЛЫ: площадь нулевая, гомография не строится — отказ.</summary>
        [Test]
        public void CollinearCorners_RefuseAsDegenerate()
        {
            KvMarkerPoseResult r = KvMarkerPose.Compute(new[]
            {
                new Vector2(10f, 10f), new Vector2(50f, 10f),
                new Vector2(90f, 10f), new Vector2(130f, 10f)
            }, 0.1f, ExactIntrinsics());

            Assert.AreEqual(KvMarkerPoseStatus.Degenerate, r.status, r.reason);
            Assert.IsFalse(r.usable, "из прямой линии позы нет");
            Assert.IsFalse(string.IsNullOrEmpty(r.reason), "отказ обязан иметь причину");
        }

        // ================================================================ статусы

        /// <summary>
        /// СТРОКА УСПЕХА ФИКСИРУЕТСЯ ТЕСТОМ: оператор читает именно «дистанция X м, наклон Y°».
        /// Это та же строка, которую `TryPlaceByMarker` кладёт в `LastMarkerStatus`.
        /// </summary>
        [Test]
        public void SuccessStatus_FormatIsFixedForOperator()
        {
            string line = KvMarkerPose.SuccessStatus(0.85f, 12f);
            StringAssert.Contains("стол поставлен по маркеру", line, line);
            StringAssert.Contains("0.85", line, "дистанция в метрах обязана быть в строке: " + line);
            StringAssert.Contains("12", line, "наклон в градусах обязан быть в строке: " + line);

            string approx = KvMarkerPose.SuccessStatus(0.85f, 12f, true);
            StringAssert.Contains("интринсики приблизительные", approx,
                "приблизительная поза обязана предупредить оператора: " + approx);
        }

        /// <summary>
        /// ПЛОЩАДКА ИЗ ПОЗЫ МАРКЕРА: точка — центр маркера в мире, нормаль — нормаль маркера
        /// (маркер на столе → нормаль вверх), и `plane == null`: постановка НЕ ждёт плоскости
        /// ARCore — в этом весь смысл волны.
        /// </summary>
        [Test]
        public void MarkerSurface_BuildedFromPoseWithoutArPlane()
        {
            GameObject camGo = new GameObject("KvMarker_Camera");
            try
            {
                camGo.transform.position = new Vector3(0f, 1.5f, 0f);
                camGo.transform.rotation = Quaternion.AngleAxis(90f, Vector3.right); // взгляд в пол

                // Маркер лежит на столешнице: центр (0, 0.7, 0), нормаль — вверх.
                KvMarkerPoseResult pose = default(KvMarkerPoseResult);
                pose.position = new Vector3(0f, 0f, 0.8f);   // в системе камеры: взгляд в пол → 0.8 м «вперёд» = вниз в мире
                pose.rotation = Quaternion.identity;          // нормаль маркера = +Z камеры (вниз) — функция обязана развернуть её вверх
                pose.distance = 0.8f;
                pose.tiltDegrees = 0f;

                KvArSurfaceHit hit = KvArPlacement.BuildMarkerSurface(pose, camGo.transform);

                Assert.IsTrue(hit.valid, "площадка из позы валидна по построению");
                Assert.That(hit.point.y, Is.EqualTo(0.7f).Within(0.001f),
                    "центр маркера перенесён в мир точь-в-точь");
                Assert.That(hit.normal.y, Is.GreaterThan(0.999f),
                    "нормаль площадки обязана смотреть вверх: " + hit.normal);
                Assert.IsNull(hit.plane,
                    "плоскости ARCore нет — маркерная постановка от неё не зависит");
                Assert.That(hit.distance, Is.EqualTo(0.8f).Within(0.001f),
                    "дистанция до площадки — от камеры до маркера");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        /// <summary>
        /// КАЖДЫЙ ОТКАЗ ДОХОДИТ ДО `LastMarkerStatus` С ПРИЧИНОЙ: без маркера / слишком мал /
        /// слишком под углом / точная поза запрещена. Молчание или общие слова — дефект:
        /// оператор не поймёт, почему стол не встал.
        /// </summary>
        [Test]
        public void PlaceByMarker_RefusalsAreNamedInLastMarkerStatus()
        {
            GameObject host = new GameObject("KvMarker_Placement");
            try
            {
                KvArPlacement placement = host.AddComponent<KvArPlacement>();

                Assert.IsFalse(placement.TryPlaceByMarker(null, ExactIntrinsics()),
                    "без маркера постановка не может пройти");
                StringAssert.Contains("маркер не виден", placement.LastMarkerStatus,
                    placement.LastMarkerStatus);

                Assert.IsFalse(placement.TryPlaceByMarker(Square(320f, 180f, 20f),
                    ExactIntrinsics()), "20 пикселей — не маркер");
                StringAssert.Contains("слишком мал", placement.LastMarkerStatus,
                    placement.LastMarkerStatus);

                Vector3 mpos = new Vector3(0f, 0f, 1f);
                Quaternion mrot = Quaternion.AngleAxis(70f, Vector3.right) *
                                  Quaternion.AngleAxis(180f, Vector3.up);
                Vector2[] oblique = Project(mpos, mrot, 0.1f, ExactIntrinsics(), Vector3.zero,
                    Quaternion.identity);
                Assert.IsFalse(placement.TryPlaceByMarker(oblique, ExactIntrinsics()),
                    "70° косизны — отказ");
                StringAssert.Contains("слишком под углом", placement.LastMarkerStatus,
                    placement.LastMarkerStatus);

                // Фолбэк под запретом точности: постановки нет, но причина названа.
                KvMarkerIntrinsics fb = KvMarkerIntrinsics.FromFrame(1280f, 720f);
                KvMarkerIntrinsics trueK = new KvMarkerIntrinsics
                {
                    fx = 900f, fy = 900f, cx = 640f, cy = 360f, approximate = false
                };
                Vector2[] frontal = Project(mpos, Quaternion.AngleAxis(180f, Vector3.up), 0.1f,
                    trueK, Vector3.zero, Quaternion.identity);
                Assert.IsFalse(placement.TryPlaceByMarker(frontal, fb,
                    requireExactIntrinsics: true), "точная постановка приблизительной позой запрещена");
                StringAssert.Contains("интринсики приблизительные", placement.LastMarkerStatus,
                    placement.LastMarkerStatus);

                Assert.Greater(placement.PlaceAttempts, 3,
                    "каждая попытка обязана попасть в счётчик попыток");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// УСПЕХ: стол поставлен, `LastMarkerStatus` — строка успеха с дистанцией и наклоном.
        /// Путь штатный (`BuildMarkerSurface` → `KvArWorld.TryPlace` без якоря), только без
        /// AR-сессии: мира в EditMode нет, поэтому `anchorManager == null` и срабатывает
        /// легитимный путь «без якоря», придуманный для устройств без ARCore.
        /// </summary>
        [Test]
        public void PlaceByMarker_SuccessWritesDistanceAndTiltStatus()
        {
            GameObject camGo = new GameObject("KvMarker_Camera");
            GameObject host = new GameObject("KvMarker_Placement");
            try
            {
                camGo.tag = "MainCamera";
                Camera cam = camGo.AddComponent<Camera>();
                camGo.transform.position = new Vector3(0f, 1.5f, 0f);
                camGo.transform.rotation = Quaternion.AngleAxis(90f, Vector3.right);

                KvArPlacement placement = host.AddComponent<KvArPlacement>();
                placement.arCamera = cam;
                placement.world = host.AddComponent<KvArWorld>();

                // Маркер 0.1 м на столешнице в 0.8 м под камерой, смотрит вверх.
                Vector3 mpos = new Vector3(0f, 0.7f, 0f);
                Quaternion mrot = Quaternion.AngleAxis(90f, Vector3.right);
                Vector2[] corners = Project(mpos, mrot, 0.1f, ExactIntrinsics(),
                    camGo.transform.position, camGo.transform.rotation);

                bool placed = placement.TryPlaceByMarker(corners, ExactIntrinsics());

                Assert.IsTrue(placed,
                    "маркер в допуске — постановка обязана пройти: " + placement.LastMarkerStatus);
                Assert.AreEqual(KvMarkerPoseStatus.Ok, placement.LastMarkerPose.status,
                    "поза посчитана настоящими интринсиками");
                StringAssert.Contains("стол поставлен по маркеру", placement.LastMarkerStatus,
                    placement.LastMarkerStatus);
                StringAssert.Contains("0.80", placement.LastMarkerStatus,
                    "дистанция до маркера в строке успеха: " + placement.LastMarkerStatus);
                StringAssert.Contains("наклон", placement.LastMarkerStatus,
                    "наклон в строке успеха: " + placement.LastMarkerStatus);
                Assert.IsTrue(placement.IsPlaced, "мир поставлен");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        /// <summary>
        /// ВХОД ЧЕРЕЗ СКАНЕР: без AR-камеры в сцене отказ честный («камера AR не найдена»),
        /// а не тихий false. Железо не нужно — в пустой сцене менеджера камеры нет.
        /// </summary>
        [Test]
        public void PlaceByMarkerFromCamera_WithoutArCamera_RefusesHonestly()
        {
            GameObject host = new GameObject("KvMarker_Placement");
            try
            {
                KvArPlacement placement = host.AddComponent<KvArPlacement>();

                Assert.IsFalse(placement.TryPlaceByMarkerFromCamera(),
                    "без AR-камеры сканировать нечего — постановки быть не может");
                StringAssert.Contains("камера AR", placement.LastMarkerStatus,
                    placement.LastMarkerStatus);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ================================================================ команда в реестре

        /// <summary>
        /// `ar.place.marker` — НАСТОЯЩАЯ КОМАНДА: зарегистрирована штатной регистрацией хаба,
        /// не заглушка, с обработчиком и путём меню. Проверяется по живому реестру `KvCommands`,
        /// а не по тексту исходника (P4-3): иначе тест жил бы от переформулировки комментария.
        /// </summary>
        [Test]
        public void PlaceMarkerCommand_RegisteredAsRealCommand()
        {
            GameObject host = new GameObject("KvMarker_UIManager");
            try
            {
                KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
                KvStageHub2.RegisterCommands(ui);

                KvCommand command = KvCommands.Get("ar.place.marker");
                Assert.IsNotNull(command,
                    "ar.place.marker не зарегистрирована — пункт меню нарисуется «(нет)» тёмной строкой");
                Assert.IsFalse(command.Stub,
                    "команда помечена заглушкой (Stub = true) — кнопка будет недоступной");
                Assert.IsNotNull(command.Execute, "у команды нет обработчика: пункт есть, а работы нет");
                Assert.IsNotEmpty(command.Title, "без заголовка пункт меню будет безымянным");
                Assert.IsNotEmpty(command.MenuPath, "без пути меню пункт не появится в «Сервис»");
                Assert.IsTrue(KvCommands.IsEnabled("ar.place.marker"),
                    "команда обязана быть доступной, а не погашенной");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ================================================================ детектор QR

        /// <summary>
        /// СИНТЕТИЧЕСКИЙ QR НАХОДИТСЯ: три ушка 1:3:1 дают углы в допуске (суммарная ошибка
        /// меньше 16 px) — иначе поза считается не по тому коду, что в кадре.
        /// </summary>
        [Test]
        public void QrDetector_FindsSyntheticCodeCorners()
        {
            Vector2 tl, tr, br, bl;
            Color32[] img = DrawQr(320, 240, 100, 60, 6, 21, out tl, out tr, out br, out bl);

            Vector2[] found;
            string reason;
            bool ok = KvQrMarkerDetector.TryFindCorners(img, 320, 240, out found, out reason);

            Assert.IsTrue(ok, "детектор не нашёл синтетический код: " + reason);
            Assert.AreEqual(4, found.Length, "найдены ровно четыре угла");

            float err = Mathf.Abs(found[0].x - tl.x) + Mathf.Abs(found[0].y - tl.y) +
                        Mathf.Abs(found[1].x - tr.x) + Mathf.Abs(found[1].y - tr.y) +
                        Mathf.Abs(found[2].x - br.x) + Mathf.Abs(found[2].y - br.y) +
                        Mathf.Abs(found[3].x - bl.x) + Mathf.Abs(found[3].y - bl.y);
            Assert.That(err, Is.LessThan(16f),
                "углы найдены мимо кода (суммарная ошибка " + err.ToString("F2") + " px): " +
                string.Join(" ", System.Array.ConvertAll(found, p => p.ToString("F1"))));
        }

        /// <summary>ПУСТОЙ КАДР — честный отказ: `false`, углов нет, причина названа.</summary>
        [Test]
        public void QrDetector_BlankFrame_RefusesHonestly()
        {
            Color32[] blank = new Color32[320 * 240];
            for (int i = 0; i < blank.Length; i++) blank[i] = new Color32(255, 255, 255, 255);

            Vector2[] found;
            string reason;
            bool ok = KvQrMarkerDetector.TryFindCorners(blank, 320, 240, out found, out reason);

            Assert.IsFalse(ok, "в пустом кадре кода быть не может");
            Assert.IsNull(found, "углы не выдуманы из шума");
            Assert.IsFalse(string.IsNullOrEmpty(reason), "отказ обязан назвать причину: " + reason);
        }

        /// <summary>
        /// КОНЕЦ КОНЦОВ: детектор → поза. Код 0.126 м в кадре 320×240 при fx 800 даёт
        /// дистанцию 0.80 м и косизну 11.8° (углы смещены от центра кадра) — эталон харнесса.
        /// </summary>
        [Test]
        public void QrDetectorToPose_EndToEndDistanceAndIncidence()
        {
            Vector2 tl, tr, br, bl;
            Color32[] img = DrawQr(320, 240, 100, 60, 6, 21, out tl, out tr, out br, out bl);

            Vector2[] found;
            string reason;
            Assert.IsTrue(KvQrMarkerDetector.TryFindCorners(img, 320, 240, out found, out reason),
                reason);

            KvMarkerPoseResult r = KvMarkerPose.Compute(found, 0.126f, ExactIntrinsics());

            Assert.IsTrue(r.usable, "поза из найденных углов пригодна: " + r.reason);
            Assert.AreEqual(KvMarkerPoseStatus.Ok, r.status, r.reason);
            Assert.That(r.distance, Is.EqualTo(0.8f).Within(0.03f),
                "дистанция из размера кода и фокуса: " + r.distance.ToString("F3"));
            Assert.That(r.tiltDegrees, Is.EqualTo(11.8f).Within(1.5f),
                "косизна смещённого от центра кода: " + r.tiltDegrees.ToString("F2"));
        }
    }
}
