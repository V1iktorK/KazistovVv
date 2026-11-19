// =================================================================================================
//  KvMarkerAnchorTests.cs — EditMode-тесты НЕПРЕРЫВНОГО ЯКОРЯ ПО QR-МАРКЕРУ (19.11.2026).
//
//  ОТКУДА ЗАДАЧА. Владелец (стиль «Pokémon GO»): в AR роботы и стол обязаны стоять В ПРОСТРАНСТВЕ,
//  пока планшет двигают/кладут/наводят на QR, — а не быть приклеенными к экрану. Планшет-инициатор
//  (Redmi Pad 2 Pro) не ARCore-сертифицирован: позы устройства трекинг не даёт, поэтому единственный
//  источник «где мир» — QR-маркер в кадре. `KvArPlacement` уже ставит стол ОДНОКРАТНО по маркеру;
//  этот файл проверяет якорь, который ПОДДЕРЖИВАЕТ позу каждый кадр.
//
//  ЧТО ПРОВЕРЯЕТСЯ (математика и поведение на СИНТЕТИКЕ — железо и AR-сессии не нужны):
//    • поза корня из позы камеры и позы маркера: при СОГЛАСОВАННЫХ измерениях корень НЕ ДВИГАЕТСЯ
//      (мир стоит), при «камера не отчиталась о движении» корень встаёт ровно туда, где маркер
//      измерен (виртуальный стол накладывается на реальный — и только это видит оператор);
//    • сглаживание: экспоненциальная формула (dt = tau → 63,2 % пути), tau = 0 — мгновенно,
//      нулевой шаг — без изменений, повторные шаги сходятся к цели;
//    • удержание при потере маркера: корень НЕ ПРЫГАЕТ, статусы честно идут
//      «найден → потерян — поза удерживается → долго нет — сцена может плыть»;
//    • dead reckoning от гироскопа: пока маркера нет, непротрекированное вращение камеры
//      поворачивает мир вокруг позиции камеры, сохраняя расстояние до неё;
//    • переключатель «якорь: маркер вкл/выкл» — выкл возвращает ПРЕЖНЕЕ поведение (корень не трогается);
//    • перевод статусов: все семь словарей отдают строки (ключи ar.anchor.* и cmd.ar.anchor.marker);
//    • команда `ar.anchor.marker` — настоящая в реестре, с обработчиком и признаком «проверяемость»;
//    • буферы детектора: путь с переиспользуемым буфером яркости даёт ТЕ ЖЕ углы, что и путь
//      с новыми буферами (покадровый скан не должен менять геометрию).
//
//  ГРАНИЦА ЧЕСТНОСТИ (P4-3). Файл НЕ читает тексты исходников: всё проверяется по живым API
//  (`KvMarkerAnchorMath`, `KvArMarkerAnchor`, `KvCommands`, `KvLoc`). `SetUp`/`TearDown`
//  возвращают пустую сцену, реестр команд — к снимку до регистрации, язык — как был.
// =================================================================================================

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
    /// <summary>Якорь по QR-маркеру: поза, сглаживание, удержание, статусы, переводы, команда.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvMarkerAnchorTests
    {
        /// <summary>СНИМОК РЕЕСТРА ДО штатной регистрации (возвращается в TearDown).</summary>
        private static List<KvCommand> registrySnapshot;

        /// <summary>Язык до теста (возвращается в TearDown).</summary>
        private string originalLanguage;

        /// <summary>Шаг времени в тестах: 15 Гц — частота, на которой работает якорь.</summary>
        private const float Dt = 1f / 15f;

        [SetUp]
        public void SetUp()
        {
            if (registrySnapshot == null)
            {
                registrySnapshot = new List<KvCommand>();
                foreach (KvCommand command in KvCommands.All) registrySnapshot.Add(command);
            }
            originalLanguage = PlayerPrefs.HasKey(KvLoc.PrefsKey)
                ? PlayerPrefs.GetString(KvLoc.PrefsKey)
                : KvLoc.SystemCode;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            KvWindowBelt.Unregister(KvFeatureWindow.WindowId);
            if (registrySnapshot != null)
            {
                KvCommands.Clear();
                foreach (KvCommand command in registrySnapshot) KvCommands.Register(command);
                registrySnapshot = null;
            }
            KvLoc.SetLanguage(originalLanguage);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // ================================================================ вспомогательное

        /// <summary>Поза измеренная: позиция/ориентация маркера В СИСТЕМЕ КАМЕРЫ, пригодна к якорю.</summary>
        private static KvMarkerPoseResult Measured(Vector3 positionInCamera, Quaternion rotationInCamera,
            float distanceMeters)
        {
            KvMarkerPoseResult pose = default(KvMarkerPoseResult);
            pose.status = KvMarkerPoseStatus.Ok;
            pose.usable = true;
            pose.position = positionInCamera;
            pose.rotation = rotationInCamera;
            pose.distance = distanceMeters;
            pose.tiltDegrees = 0f;
            return pose;
        }

        /// <summary>Обратное применение позы камеры: мировая поза → поза в камере.</summary>
        private static Pose InCamera(Pose cameraWorld, Pose worldPose)
        {
            Quaternion inverse = Quaternion.Inverse(cameraWorld.rotation);
            return new Pose(inverse * (worldPose.position - cameraWorld.position),
                inverse * worldPose.rotation);
        }

        /// <summary>Постановка мира БЕЗ AR-якоря (штатный путь маркерной постановки, EditMode).</summary>
        private static KvArWorld PlaceWorld(GameObject host, Vector3 point, Vector3 normal)
        {
            KvArWorld world = host.AddComponent<KvArWorld>();
            KvArPlacement placement = host.AddComponent<KvArPlacement>();
            placement.world = world;

            KvArSurfaceHit hit = default(KvArSurfaceHit);
            hit.valid = true;
            hit.point = point;
            hit.normal = normal;
            hit.worldPose = new Pose(point, KvArPlacementMath.ReticleRotation(normal));
            hit.plane = null;
            hit.distance = 0.8f;

            string reason;
            Assert.IsTrue(world.TryPlace(hit, Vector3.zero, out reason, allowWithoutAnchor: true),
                "подготовка мира не удалась: " + reason);
            Assert.IsNotNull(world.LabRoot, "корень мира не создан — якорю нечего двигать");
            return world;
        }

        /// <summary>Хост со всеми компонентами якоря (якорь — на том же объекте, что и мир).</summary>
        private static KvArMarkerAnchor BuildAnchor(out GameObject host, out KvArWorld world,
            out KvArPlacement placement)
        {
            host = new GameObject("KvAnchor_Host");
            world = PlaceWorld(host, new Vector3(0f, 0.7f, 0f), Vector3.up);
            placement = host.GetComponent<KvArPlacement>();

            KvArMarkerAnchor anchor = host.AddComponent<KvArMarkerAnchor>();
            anchor.placement = placement;
            // В тестах нужна точность, а не сглаживание: фильтр проверяется отдельно.
            anchor.positionSmoothTime = 0f;
            anchor.rotationSmoothTime = 0f;
            return anchor;
        }

        /// <summary>Рисует одно «ушко» QR 7×7 модулей в Color32-кадре.</summary>
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

        /// <summary>СИНТЕТИЧЕСКИЙ QR: три ушка по углам кода; ожидаемые углы — через out.</summary>
        private static Color32[] DrawQr(int w, int h, int codeX, int codeY, int modulePx, int modules,
            out Vector2 tl, out Vector2 tr, out Vector2 br, out Vector2 bl)
        {
            Color32[] px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);

            int side = modules * modulePx;
            int finderSide = 7 * modulePx;
            DrawFinder(px, w, codeX, codeY, modulePx);
            DrawFinder(px, w, codeX + side - finderSide, codeY, modulePx);
            DrawFinder(px, w, codeX, codeY + side - finderSide, modulePx);

            tl = new Vector2(codeX, codeY);
            tr = new Vector2(codeX + side, codeY);
            br = new Vector2(codeX + side, codeY + side);
            bl = new Vector2(codeX, codeY + side);
            return px;
        }

        // ================================================================ математика позы

        /// <summary>
        /// ЯКОРЬ ДЕРЖИТ МИР: при согласованных измерениях (камера отчиталась о движении ровно так,
        /// как видит маркер) корень возвращается в ИСХОДНУЮ позу — мир не дрейфует и не дрожит.
        /// </summary>
        [Test]
        public void RootPose_ConsistentMeasurements_KeepRootExactlyInPlace()
        {
            Pose root0 = new Pose(new Vector3(0f, 0.7f, 0f),
                Quaternion.FromToRotation(Vector3.forward, Vector3.up));
            Pose camera0 = new Pose(new Vector3(0f, 1.5f, 0f), Quaternion.identity);
            Pose markerInCamera0 = InCamera(camera0, root0);

            Pose markerWorld = KvMarkerAnchorMath.MarkerWorld(camera0, markerInCamera0);
            Pose relative = KvMarkerAnchorMath.MarkerRelativeTo(root0, markerWorld);
            Assert.That(Vector3.Distance(markerWorld.position, root0.position), Is.LessThan(1e-4f),
                "маркер в мире — это ровно та точка, что измерена из камеры");

            Pose rootAtStart = KvMarkerAnchorMath.RootPoseFor(camera0, markerInCamera0, relative);
            Assert.That(Vector3.Distance(rootAtStart.position, root0.position), Is.LessThan(1e-4f),
                "первый кадр обязан оставить корень там, где он был (прыжок при старте = мигание сцены)");
            Assert.That(Quaternion.Angle(rootAtStart.rotation, root0.rotation), Is.LessThan(0.01f));

            // Камера двигалась и отчиталась об этом; маркер в мире неподвижен.
            for (int step = 1; step <= 8; step++)
            {
                Pose camera = new Pose(new Vector3(0.12f * step, 1.35f, -0.3f * step),
                    Quaternion.AngleAxis(7f * step, Vector3.up));
                Pose markerInCamera = InCamera(camera, markerWorld);
                Pose root = KvMarkerAnchorMath.RootPoseFor(camera, markerInCamera, relative);

                Assert.That(Vector3.Distance(root.position, root0.position), Is.LessThan(1e-3f),
                    "кадр " + step + ": корень уехал с места — мир «плывёт» при согласованном трекинге");
                Assert.That(Quaternion.Angle(root.rotation, root0.rotation), Is.LessThan(0.05f),
                    "кадр " + step + ": корень повернулся — согласованные измерения не должны его двигать");
            }
        }

        /// <summary>
        /// КАМЕРА НЕ ОТЧИТАЛАСЬ О ДВИЖЕНИИ (планшет без ARCore-трекинга): корень обязан встать
        /// ровно туда, где маркер измерен — виртуальный стол накладывается на реальный QR.
        /// </summary>
        [Test]
        public void RootPose_UntrackedCamera_RootFollowsMeasuredMarker()
        {
            Pose root0 = new Pose(new Vector3(0f, 0.7f, 0f),
                Quaternion.FromToRotation(Vector3.forward, Vector3.up));
            Pose camera0 = new Pose(Vector3.zero, Quaternion.identity);
            Pose relative = KvMarkerAnchorMath.MarkerRelativeTo(
                root0, KvMarkerAnchorMath.MarkerWorld(camera0, InCamera(camera0, root0)));

            // Поза «в отчёте» не изменилась (AR не двигался), маркер в кадре сместился.
            Pose cameraStuck = new Pose(Vector3.zero, Quaternion.identity);
            Pose measured = new Pose(new Vector3(0.1f, 0.75f, 0.45f), Quaternion.AngleAxis(12f, Vector3.up));

            Pose root = KvMarkerAnchorMath.RootPoseFor(cameraStuck, measured, relative);
            Assert.That(Vector3.Distance(root.position, measured.position), Is.LessThan(1e-4f),
                "при зафиксированной камере корень обязан совпасть с измеренной позой маркера " +
                "(иначе стол останется приклеен к экрану): " + root.position);
            Assert.That(Quaternion.Angle(root.rotation, measured.rotation), Is.LessThan(0.05f),
                "ориентация корня тоже берётся у маркера");
            Assert.That(Vector3.Distance(root.position, root0.position), Is.GreaterThan(0.05f),
                "подготовка сломана: корень не двигался вовсе — проверка ничего не доказывает");
        }

        /// <summary>СГЛАЖИВАНИЕ: экспоненциальный фильтр, мгновенный при tau = 0, без сдвигов при dt = 0.</summary>
        [Test]
        public void Smooth_IsExponentialAndSnapsWhenTimeConstantZero()
        {
            Assert.That(KvMarkerAnchorMath.Alpha(0.1f, 0.1f),
                Is.EqualTo(1f - Mathf.Exp(-1f)).Within(1e-5f),
                "dt = tau обязан давать 63,2 % пути — это и есть эталон экспоненты");
            Assert.That(KvMarkerAnchorMath.Alpha(0f, 0.1f), Is.EqualTo(0f).Within(1e-6f),
                "нулевой шаг — фильтр не двигается");
            Assert.That(KvMarkerAnchorMath.Alpha(1f, 0f), Is.EqualTo(1f).Within(1e-6f),
                "tau = 0 — фильтр выключен, движение мгновенное");
            Assert.That(KvMarkerAnchorMath.Alpha(1f, -5f), Is.EqualTo(1f).Within(1e-6f),
                "отрицательный tau не должен давать отрицательную долю");

            Pose current = new Pose(Vector3.zero, Quaternion.identity);
            Pose target = new Pose(new Vector3(1f, 0f, 0f), Quaternion.AngleAxis(90f, Vector3.up));

            Pose step = KvMarkerAnchorMath.Smooth(current, target, 0.1f, 0.1f, 0.1f);
            Assert.That(step.position.x, Is.EqualTo(1f - Mathf.Exp(-1f)).Within(1e-4f),
                "позиция обязана пройти ровно долю alpha");
            Assert.That(Quaternion.Angle(step.rotation, target.rotation), Is.GreaterThan(1f),
                "за один шаг поворот НЕ должен долетать до цели — иначе дрожание останется");

            Pose snap = KvMarkerAnchorMath.Smooth(current, target, 0.1f, 0f, 0f);
            Assert.That(Vector3.Distance(snap.position, target.position), Is.LessThan(1e-5f),
                "tau = 0 — фильтр обязан вернуть цель целиком");
            Assert.That(Quaternion.Angle(snap.rotation, target.rotation), Is.LessThan(0.01f));

            Pose idle = KvMarkerAnchorMath.Smooth(current, current, 0.1f, 0.1f, 0.1f);
            Assert.That(Vector3.Distance(idle.position, current.position), Is.LessThan(1e-6f),
                "цель == текущее — сглаживание не должно никуда уводить позу");

            // СХОДИМОСТЬ: повторные шаги приближаются к цели монотонно и упираются в неё.
            Pose running = current;
            float previousError = float.MaxValue;
            for (int i = 0; i < 60; i++)
            {
                running = KvMarkerAnchorMath.Smooth(running, target, 0.066f, 0.1f, 0.1f);
                float error = Vector3.Distance(running.position, target.position);
                Assert.LessOrEqual(error, previousError + 1e-6f, "шаг " + i + ": фильтр разошёлся");
                previousError = error;
            }
            Assert.That(previousError, Is.LessThan(1e-3f), "за 60 кадров фильтр не дошёл до цели");
        }

        /// <summary>ДЕД-РЕККЕНИНГ: вращение, которое не отчитался AR, поворачивает мир вокруг камеры.</summary>
        [Test]
        public void DeadReckon_RotatesWorldAboutCameraKeepingDistance()
        {
            Pose camera = new Pose(new Vector3(0.2f, 1.5f, -0.4f), Quaternion.AngleAxis(35f, Vector3.up));
            Pose root = new Pose(new Vector3(0f, 0.7f, 1.2f), Quaternion.AngleAxis(30f, Vector3.left));
            float distanceBefore = Vector3.Distance(root.position, camera.position);

            Quaternion untracked = Quaternion.AngleAxis(10f, Vector3.up);
            Pose moved = KvMarkerAnchorMath.DeadReckon(root, camera, untracked);

            Assert.That(Vector3.Distance(moved.position, camera.position),
                Is.EqualTo(distanceBefore).Within(1e-4f),
                "мир крутится ВОКРУГ камеры — расстояние до камеры обязано сохраниться");
            Quaternion worldFrame = camera.rotation * untracked * Quaternion.Inverse(camera.rotation);
            Assert.That(Quaternion.Angle(moved.rotation, worldFrame * root.rotation), Is.LessThan(0.05f),
                "поворот мира — это поворот камеры, перенесённый в мировые координаты");

            Pose untouched = KvMarkerAnchorMath.DeadReckon(root, camera, Quaternion.identity);
            Assert.That(Vector3.Distance(untouched.position, root.position), Is.LessThan(1e-5f),
                "без непротрекированного вращения мир не должен сдвигаться вовсе");
            Assert.That(Quaternion.Angle(untouched.rotation, root.rotation), Is.LessThan(0.01f));
        }

        /// <summary>НЕПРОТРЕКИРОВАННАЯ ЧАСТЬ = гироскоп минус то, что уже дал AR (оба в кадре камеры).</summary>
        [Test]
        public void UntrackedRotation_PerfectTrackingIsIdentity_DeadArKeepsGyro()
        {
            Quaternion gyro = Quaternion.AngleAxis(6f, Vector3.right);

            Assert.That(Quaternion.Angle(KvMarkerAnchorMath.UntrackedRotation(gyro, gyro),
                    Quaternion.identity), Is.LessThan(0.01f),
                "AR отчитался ровно то, что видит гироскоп — компенсировать нечего");
            Assert.That(Quaternion.Angle(KvMarkerAnchorMath.UntrackedRotation(gyro, Quaternion.identity),
                    gyro), Is.LessThan(0.01f),
                "AR не двигался вообще — вся компенсация от гироскопа");
            Quaternion partial = Quaternion.AngleAxis(2f, Vector3.right);
            Assert.That(Quaternion.Angle(
                    KvMarkerAnchorMath.UntrackedRotation(gyro, partial),
                    Quaternion.AngleAxis(4f, Vector3.right)), Is.LessThan(0.05f),
                "AR отчитался о части поворота — компенсируется только остаток");
        }

        /// <summary>
        /// КОНВЕНЦИЯ ГИРОСКОПА: дельта между двумя сырыми позами приводится к кадру Unity
        /// (формула `Euler(90,0,0) · (−x, −y, z, w)`), и это сопряжение — гомоморфизм:
        /// дельта через промежуточную позу равна произведению дельт.
        /// </summary>
        [Test]
        public void GyroDelta_ConvertsRawAttitudeAndComposes()
        {
            Quaternion raw0 = Quaternion.identity;
            Quaternion raw1 = Quaternion.AngleAxis(10f, Vector3.forward);
            Quaternion delta = KvMarkerAnchorMath.GyroDelta(raw0, raw1);
            Assert.That(Quaternion.Angle(delta, Quaternion.AngleAxis(10f, Vector3.forward)),
                Is.LessThan(0.01f), "ось Z сопряжением не меняется — дельта должна сохранить поворот");

            Quaternion rawX = Quaternion.AngleAxis(10f, Vector3.right);
            Assert.That(Quaternion.Angle(KvMarkerAnchorMath.GyroDelta(raw0, rawX),
                    Quaternion.AngleAxis(-10f, Vector3.right)), Is.LessThan(0.01f),
                "ось X меняется знаком (сопряжение на 180° вокруг Z)");

            // Мультипликативность: d(a→c) = d(a→b) · d(b→c) — накопление дельт не должно врать.
            Quaternion raw2 = Quaternion.AngleAxis(25f, Vector3.up);
            Quaternion direct = KvMarkerAnchorMath.GyroDelta(raw0, raw2);
            Quaternion ab = KvMarkerAnchorMath.GyroDelta(raw0, raw1);
            Quaternion bc = KvMarkerAnchorMath.GyroDelta(raw1, raw2);
            Assert.That(Quaternion.Angle(direct, ab * bc), Is.LessThan(0.1f),
                "дельта обязано перемножаться через промежуточную позу — иначе накопление врёт");

            // Обратный путь — обратная дельта.
            Assert.That(Quaternion.Angle(KvMarkerAnchorMath.GyroDelta(raw2, raw0),
                    Quaternion.Inverse(direct)), Is.LessThan(0.1f));
        }

        // ================================================================ статусы

        /// <summary>ПОРОГИ СОСТОЯНИЙ: вкл/выкл, мира нет, маркера нет, найден, потерян, давно потерян.</summary>
        [Test]
        public void StateFor_TurnsOffFirstThenWalksThroughThresholds()
        {
            const float lostHold = 0.5f;
            const float lostLong = 3f;

            Assert.AreEqual(KvArAnchorState.Disabled,
                KvMarkerAnchorMath.StateFor(false, true, true, 0f, lostHold, lostLong),
                "выключенный якорь обязан быть Disabled независимо от прочего");
            Assert.AreEqual(KvArAnchorState.Disabled,
                KvMarkerAnchorMath.StateFor(false, false, false, 0f, lostHold, lostLong));
            Assert.AreEqual(KvArAnchorState.NoWorld,
                KvMarkerAnchorMath.StateFor(true, false, false, 0f, lostHold, lostLong),
                "мира нет — якорю нечего держать");
            Assert.AreEqual(KvArAnchorState.Waiting,
                KvMarkerAnchorMath.StateFor(true, true, false, 0f, lostHold, lostLong),
                "мир поставлен, маркер ещё не видели — ждём QR");
            Assert.AreEqual(KvArAnchorState.Found,
                KvMarkerAnchorMath.StateFor(true, true, true, 0f, lostHold, lostLong));
            Assert.AreEqual(KvArAnchorState.Found,
                KvMarkerAnchorMath.StateFor(true, true, true, lostHold, lostHold, lostLong),
                "ровно на пороге удержания маркер ещё «найден»");
            Assert.AreEqual(KvArAnchorState.Lost,
                KvMarkerAnchorMath.StateFor(true, true, true, lostHold + 0.01f, lostHold, lostLong),
                "за порогом удержания — честно «потерян»");
            Assert.AreEqual(KvArAnchorState.Lost,
                KvMarkerAnchorMath.StateFor(true, true, true, lostLong, lostHold, lostLong),
                "ровно на пороге долгой потери статус ещё «потерян»");
            Assert.AreEqual(KvArAnchorState.LostLong,
                KvMarkerAnchorMath.StateFor(true, true, true, lostLong + 0.01f, lostHold, lostLong));
        }

        /// <summary>КЛЮЧИ СТАТУСОВ: каждому состоянию — свой ключ словаря (иначе перевод не найти).</summary>
        [Test]
        public void StatusKey_MapsEveryStateToItsOwnDictionaryKey()
        {
            Assert.AreEqual("ar.anchor.disabled", KvMarkerAnchorMath.StatusKey(KvArAnchorState.Disabled));
            Assert.AreEqual("ar.anchor.noworld", KvMarkerAnchorMath.StatusKey(KvArAnchorState.NoWorld));
            Assert.AreEqual("ar.anchor.waiting", KvMarkerAnchorMath.StatusKey(KvArAnchorState.Waiting));
            Assert.AreEqual("ar.anchor.found", KvMarkerAnchorMath.StatusKey(KvArAnchorState.Found));
            Assert.AreEqual("ar.anchor.lost", KvMarkerAnchorMath.StatusKey(KvArAnchorState.Lost));
            Assert.AreEqual("ar.anchor.drift", KvMarkerAnchorMath.StatusKey(KvArAnchorState.LostLong));

            foreach (KvArAnchorState state in System.Enum.GetValues(typeof(KvArAnchorState)))
            {
                Assert.IsNotEmpty(KvMarkerAnchorMath.StatusPattern(state),
                    "у состояния " + state + " нет русского шаблона — нечему переводить");
                Assert.IsNotEmpty(KvMarkerAnchorMath.StatusText(state, 0.85f),
                    "у состояния " + state + " пустая строка статуса");
            }
        }

        /// <summary>РУССКИЕ СТАТУСЫ: те самые слова, что читает оператор, и дистанция с точкой.</summary>
        [Test]
        public void StatusText_Russian_SaysWhatOperatorMustRead()
        {
            KvLoc.SetLanguage("ru", false);

            string found = KvMarkerAnchorMath.StatusText(KvArAnchorState.Found, 0.85f);
            StringAssert.Contains("маркер найден", found, found);
            StringAssert.Contains("дистанция", found, found);
            StringAssert.Contains("0.85", found,
                "дистанция обязана быть в метрах с точкой (инвариантная локаль): " + found);
            Assert.IsFalse(found.Contains("0,85"),
                "число ушло в локаль с запятой — сверка по журналу разойдётся: " + found);

            StringAssert.Contains("поза удерживается",
                KvMarkerAnchorMath.StatusText(KvArAnchorState.Lost, 0.85f));
            StringAssert.Contains("сцена может плыть",
                KvMarkerAnchorMath.StatusText(KvArAnchorState.LostLong, 0.85f));
            StringAssert.Contains("выключен",
                KvMarkerAnchorMath.StatusText(KvArAnchorState.Disabled, 0f));
            StringAssert.Contains("наведите камеру",
                KvMarkerAnchorMath.StatusText(KvArAnchorState.Waiting, 0f));
            StringAssert.Contains("постановки",
                KvMarkerAnchorMath.StatusText(KvArAnchorState.NoWorld, 0f));
        }

        /// <summary>ВСЕ СЕМЬ СЛОВАРЕЙ отдают строки статусов и переключателя (не ключ и не пусто).</summary>
        [Test]
        public void AnchorStatuses_AreLocalizedInAllSevenLanguages()
        {
            string[] keys =
            {
                "ar.anchor.disabled", "ar.anchor.noworld", "ar.anchor.waiting",
                "ar.anchor.found", "ar.anchor.lost", "ar.anchor.drift",
                "cmd.ar.anchor.marker", "cmd.ar.anchor.marker.desc"
            };

            var problems = new List<string>();
            foreach (string code in KvLoc.KnownFileCodes)
            {
                KvLoc.SetLanguage(code, false);
                foreach (string key in keys)
                {
                    string text = KvLoc.T(key, null);
                    if (string.IsNullOrEmpty(text) || string.Equals(text, key,
                            System.StringComparison.Ordinal))
                        problems.Add(code + ":" + key);
                }
            }
            Assert.AreEqual(0, problems.Count,
                "перевода нет (оператор увидит ключ или пустое место): " +
                string.Join(", ", problems.ToArray()));

            // Шаблон дистанции обязан форматироваться, а не выдавать скобки.
            KvLoc.SetLanguage("ru", false);
            string line = KvMarkerAnchorMath.StatusText(KvArAnchorState.Found, 1.25f);
            Assert.IsFalse(line.Contains("{0}"),
                "подстановка дистанции осталась на экране: " + line);
            StringAssert.Contains("1.25", line, line);
        }

        // ================================================================ поведение якоря (имитация кадров)

        /// <summary>
        /// СОГЛАСОВАННЫЕ КАДРЫ: камера отчиталась о движении, маркер измерен согласованно —
        /// корень НЕ ДВИГАЕТСЯ ни на миллиметр (мир стоит, планшет «летает» вокруг).
        /// </summary>
        [Test]
        public void Anchor_ConsistentFrames_RootNeverMoves()
        {
            GameObject host;
            KvArWorld world;
            KvArPlacement placement;
            KvArMarkerAnchor anchor = BuildAnchor(out host, out world, out placement);
            try
            {
                Pose root0 = new Pose(world.LabRoot.position, world.LabRoot.rotation);
                Pose markerWorld = root0;
                Pose camera0 = new Pose(Vector3.zero, Quaternion.identity);

                Assert.IsTrue(anchor.FeedMarker(
                        Measured(InCamera(camera0, markerWorld).position,
                            InCamera(camera0, markerWorld).rotation, 0.8f), camera0, 0f),
                    "первый кадр обязан приняться");
                anchor.Tick(Dt, 0f, camera0, Quaternion.identity);
                Assert.AreEqual(KvArAnchorState.Found, anchor.State, anchor.Describe());
                Assert.IsTrue(anchor.AppliesPose, "якорь зацепился — позу мира ведёт он");

                for (int step = 1; step <= 12; step++)
                {
                    Pose camera = new Pose(new Vector3(0.1f * step, 1.45f, -0.25f * step),
                        Quaternion.AngleAxis(6f * step, Vector3.up));
                    Pose inCam = InCamera(camera, markerWorld);
                    Assert.IsTrue(anchor.FeedMarker(
                        Measured(inCam.position, inCam.rotation, 0.8f), camera, step * Dt),
                        "кадр " + step + " отвергнут: " + anchor.Describe());
                    anchor.Tick(Dt, step * Dt, camera, Quaternion.identity);
                }

                Assert.That(Vector3.Distance(world.LabRoot.position, root0.position), Is.LessThan(1e-4f),
                    "мир уехал при согласованных измерениях: " + world.LabRoot.position);
                Assert.That(Quaternion.Angle(world.LabRoot.rotation, root0.rotation), Is.LessThan(0.02f),
                    "мир повернулся при согласованных измерениях");
                Assert.AreEqual(KvArAnchorState.Found, anchor.State, anchor.Describe());
                Assert.AreEqual(13, anchor.AcceptedFrames, "каждый кадр обязан попасть в счётчик");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// КАМЕРА НЕ ОТЧИТАЛАСЬ О ДВИЖЕНИИ: корень встаёт туда, где маркер измерен, —
        /// виртуальный стол накладывается на QR, а не остаётся приклеенным к экрану.
        /// </summary>
        [Test]
        public void Anchor_UntrackedCamera_RootFollowsMarkerMeasurement()
        {
            GameObject host;
            KvArWorld world;
            KvArPlacement placement;
            KvArMarkerAnchor anchor = BuildAnchor(out host, out world, out placement);
            try
            {
                Pose root0 = new Pose(world.LabRoot.position, world.LabRoot.rotation);
                Pose camera0 = new Pose(Vector3.zero, Quaternion.identity);
                anchor.FeedMarker(Measured(root0.position, root0.rotation, 0.8f), camera0, 0f);
                anchor.Tick(Dt, 0f, camera0, Quaternion.identity);

                // Поза «в отчёте» зафиксирована (ARCore нет), маркер в кадре сместился.
                Pose measured = new Pose(new Vector3(0.15f, 0.8f, 0.4f),
                    Quaternion.AngleAxis(18f, Vector3.up));
                Assert.IsTrue(anchor.FeedMarker(
                    Measured(measured.position, measured.rotation, 0.7f), camera0, Dt));
                anchor.Tick(Dt, Dt, camera0, Quaternion.identity);

                Assert.That(Vector3.Distance(world.LabRoot.position, measured.position), Is.LessThan(0.001f),
                    "корень обязан встать на измеренную позу маркера (иначе стол плывёт за экраном): " +
                    world.LabRoot.position);
                Assert.That(Quaternion.Angle(world.LabRoot.rotation, measured.rotation), Is.LessThan(0.1f));
                Assert.That(Vector3.Distance(world.LabRoot.position, root0.position), Is.GreaterThan(0.05f),
                    "подготовка: корень не двигался вовсе — проверка ничего не доказывает");
                Assert.AreEqual(KvArAnchorState.Found, anchor.State, anchor.Describe());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// ПОТЕРЯ МАРКЕРА: корень НЕ ПРЫГАЕТ (поза держится), статусы идут
        /// «найден → потерян — поза удерживается → долго нет — сцена может плыть».
        /// </summary>
        [Test]
        public void Anchor_MarkerLost_HoldsPoseAndReportsHonestStatuses()
        {
            GameObject host;
            KvArWorld world;
            KvArPlacement placement;
            KvArMarkerAnchor anchor = BuildAnchor(out host, out world, out placement);
            try
            {
                anchor.lostHoldSeconds = 0.5f;
                anchor.lostLongSeconds = 3f;
                Pose camera = new Pose(Vector3.zero, Quaternion.identity);
                anchor.FeedMarker(Measured(world.LabRoot.position, world.LabRoot.rotation, 0.8f),
                    camera, 0f);
                anchor.Tick(Dt, 0f, camera, Quaternion.identity);
                Pose held = new Pose(world.LabRoot.position, world.LabRoot.rotation);

                // Кадры без маркера (углов нет / контраст низкий / маркер ушёл из кадра).
                anchor.Tick(Dt, 0.3f, camera, Quaternion.identity);
                Assert.AreEqual(KvArAnchorState.Found, anchor.State,
                    "в пределах удержания статус остаётся «найден»: " + anchor.Describe());
                Assert.That(Vector3.Distance(world.LabRoot.position, held.position), Is.LessThan(1e-5f));

                anchor.Tick(Dt, 1f, camera, Quaternion.identity);
                Assert.AreEqual(KvArAnchorState.Lost, anchor.State, anchor.Describe());
                StringAssert.Contains("поза удерживается", anchor.StatusLine, anchor.StatusLine);
                Assert.That(Vector3.Distance(world.LabRoot.position, held.position), Is.LessThan(1e-5f),
                    "при потере маркера корень обязан стоять (удержание), а не дёргаться");

                anchor.Tick(Dt, 4f, camera, Quaternion.identity);
                Assert.AreEqual(KvArAnchorState.LostLong, anchor.State, anchor.Describe());
                StringAssert.Contains("сцена может плыть", anchor.StatusLine, anchor.StatusLine);
                Assert.That(Quaternion.Angle(world.LabRoot.rotation, held.rotation), Is.LessThan(0.01f),
                    "вращение тоже удерживается, пока гироскоп не компенсирует поворот");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// DEAD RECKONING В ЖИВОМ ЦИКЛЕ: маркер потерян, гироскоп видит поворот, который AR не
        /// отчитал, — мир поворачивается вокруг камеры, расстояние до камеры сохраняется.
        /// </summary>
        [Test]
        public void Anchor_MarkerLost_GyroDeadReckonTurnsWorldAboutCamera()
        {
            GameObject host;
            KvArWorld world;
            KvArPlacement placement;
            KvArMarkerAnchor anchor = BuildAnchor(out host, out world, out placement);
            try
            {
                anchor.gyroDeadReckoning = true;
                Pose camera = new Pose(new Vector3(0.3f, 1.5f, -0.2f), Quaternion.identity);
                anchor.FeedMarker(Measured(world.LabRoot.position, world.LabRoot.rotation, 0.8f),
                    camera, 0f);
                anchor.Tick(Dt, 0f, camera, Quaternion.identity);

                float distanceBefore = Vector3.Distance(world.LabRoot.position, camera.position);
                Quaternion before = world.LabRoot.rotation;
                Vector3 positionBefore = world.LabRoot.position;

                anchor.Tick(Dt, 1f, camera, Quaternion.AngleAxis(12f, Vector3.up));

                Assert.AreEqual(KvArAnchorState.Lost, anchor.State, anchor.Describe());
                Assert.That(Vector3.Distance(world.LabRoot.position, camera.position),
                    Is.EqualTo(distanceBefore).Within(1e-3f),
                    "мертвое плавание крутит мир ВОКРУГ камеры, а не уводит его в сторону");
                Assert.That(Quaternion.Angle(world.LabRoot.rotation,
                        Quaternion.AngleAxis(12f, Vector3.up) * before), Is.LessThan(0.2f),
                    "поворот мира обязан равняться непротрекированному повороту камеры");
                Assert.That(Vector3.Distance(world.LabRoot.position, positionBefore), Is.GreaterThan(0.01f),
                    "подготовка: мир не сдвинулся — проверка ничего не доказывает");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>ЯКОРЬ ВЫКЛЮЧЕН — ПРЕЖНЕЕ ПОВЕДЕНИЕ: маркер не читается, корень не трогается.</summary>
        [Test]
        public void Anchor_Disabled_RestoresPreviousBehaviorAndSaysSo()
        {
            GameObject host;
            KvArWorld world;
            KvArPlacement placement;
            KvArMarkerAnchor anchor = BuildAnchor(out host, out world, out placement);
            try
            {
                Pose camera = new Pose(Vector3.zero, Quaternion.identity);
                anchor.FeedMarker(Measured(world.LabRoot.position, world.LabRoot.rotation, 0.8f),
                    camera, 0f);
                anchor.Tick(Dt, 0f, camera, Quaternion.identity);
                Pose before = new Pose(world.LabRoot.position, world.LabRoot.rotation);

                anchor.SetEnabled(false);
                Assert.AreEqual(KvArAnchorState.Disabled, anchor.State, anchor.Describe());
                StringAssert.Contains("выключен", anchor.StatusLine, anchor.StatusLine);
                Assert.IsFalse(anchor.AppliesPose,
                    "выключенный якорь не ведёт позу — иначе «прежнее поведение» невозможно");

                Assert.IsFalse(anchor.FeedMarker(
                        Measured(new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, 0.4f), camera, Dt),
                    "выключенный якорь обязан отвергать кадры, а не копить их");
                anchor.Tick(Dt, Dt, camera, Quaternion.AngleAxis(30f, Vector3.up));
                anchor.Tick(Dt, 4f, camera, Quaternion.identity);

                Assert.AreEqual(KvArAnchorState.Disabled, anchor.State, anchor.Describe());
                Assert.That(Vector3.Distance(world.LabRoot.position, before.position), Is.LessThan(1e-5f),
                    "при выключенном якоре корень обязан стоять как при постановке");
                Assert.That(Quaternion.Angle(world.LabRoot.rotation, before.rotation), Is.LessThan(0.01f),
                    "при выключенном якоре корень не должен поворачиваться от мёртвого плавания");

                anchor.SetEnabled(true);
                anchor.Tick(Dt, 4f, camera, Quaternion.identity);
                Assert.AreNotEqual(KvArAnchorState.Disabled, anchor.State,
                    "после включения якорь снова считает состояние");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>ЯКОРЬ НЕ ЗАЦЕПИЛСЯ (маркера не было) — позу ведёт прежний код, статус честно ждёт QR.</summary>
        [Test]
        public void Anchor_WithoutMarker_DoesNotTouchPoseAndWaitsForQr()
        {
            GameObject host;
            KvArWorld world;
            KvArPlacement placement;
            KvArMarkerAnchor anchor = BuildAnchor(out host, out world, out placement);
            try
            {
                Pose before = new Pose(world.LabRoot.position, world.LabRoot.rotation);
                anchor.Tick(Dt, 0f, new Pose(Vector3.zero, Quaternion.identity),
                    Quaternion.AngleAxis(45f, Vector3.up));

                Assert.AreEqual(KvArAnchorState.Waiting, anchor.State, anchor.Describe());
                Assert.IsFalse(anchor.AppliesPose,
                    "без маркера якорь не имеет права двигать мир (это была бы самодеятельность)");
                Assert.That(Vector3.Distance(world.LabRoot.position, before.position), Is.LessThan(1e-5f),
                    "корень сдвинут без единого измерения маркера");
                StringAssert.Contains("наведите камеру", anchor.StatusLine, anchor.StatusLine);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>СГЛАЖИВАНИЕ В ЖИВОМ ЦИКЛЕ: цель прыгнула — корень идёт к ней плавно.</summary>
        [Test]
        public void Anchor_Smoothing_MovesTowardJumpedTargetGradually()
        {
            GameObject host;
            KvArWorld world;
            KvArPlacement placement;
            KvArMarkerAnchor anchor = BuildAnchor(out host, out world, out placement);
            try
            {
                anchor.positionSmoothTime = 0.3f;
                anchor.rotationSmoothTime = 0.3f;
                Pose camera = new Pose(Vector3.zero, Quaternion.identity);
                anchor.FeedMarker(Measured(world.LabRoot.position, world.LabRoot.rotation, 0.8f),
                    camera, 0f);
                anchor.Tick(Dt, 0f, camera, Quaternion.identity);
                Vector3 start = world.LabRoot.position;

                Pose target = new Pose(start + new Vector3(0.4f, 0f, 0f), world.LabRoot.rotation);
                anchor.FeedMarker(Measured(target.position, target.rotation, 0.6f), camera, Dt);
                anchor.Tick(Dt, Dt, camera, Quaternion.identity);

                float afterOne = Vector3.Distance(world.LabRoot.position, start);
                Assert.That(afterOne, Is.GreaterThan(0.001f), "после прыжка цели корень обязан тронуться");
                Assert.That(afterOne, Is.LessThan(0.4f * 0.8f),
                    "за один кадр корень обязан пройти МЕНЬШЕ пути — иначе сглаживания нет");

                for (int i = 2; i <= 90; i++) anchor.Tick(Dt, i * Dt, camera, Quaternion.identity);
                Assert.That(Vector3.Distance(world.LabRoot.position, target.position), Is.LessThan(0.005f),
                    "за ~6 с фильтр обязан дойти до цели: " + world.LabRoot.position);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ================================================================ гироскоп и команда

        /// <summary>ГИРО-ФОЛБЭК УСТУПАЕТ ЯКОРЮ: два ведущих мира дали бы двойное вращение.</summary>
        [Test]
        public void GyroFallback_YieldsToTrackingAndToMarkerAnchor()
        {
            Assert.IsTrue(KvArGyroFallback.ShouldRotate(false, false),
                "ни трекинга, ни якоря — гироскоп и есть резерв обзора");
            Assert.IsFalse(KvArGyroFallback.ShouldRotate(true, false),
                "AR-трекинг ведёт позу сам — фолбэк обязан молчать");
            Assert.IsFalse(KvArGyroFallback.ShouldRotate(false, true),
                "якорь маркера ведёт мир каждый кадр — второй поворот сломал бы сцену");
            Assert.IsFalse(KvArGyroFallback.ShouldRotate(true, true));
        }

        /// <summary>`ar.anchor.marker` — НАСТОЯЩАЯ команда: обработчик, путь меню, признак «проверяемость».</summary>
        [Test]
        public void AnchorToggleCommand_RegisteredAsRealCommand()
        {
            GameObject host = new GameObject("KvAnchor_UIManager");
            try
            {
                KazistovVvUIManager ui = host.AddComponent<KazistovVvUIManager>();
                KvStageHub2.RegisterCommands(ui);

                KvCommand command = KvCommands.Get("ar.anchor.marker");
                Assert.IsNotNull(command,
                    "ar.anchor.marker не зарегистрирована — переключателя якоря не существует");
                Assert.IsFalse(command.Stub, "команда помечена заглушкой — кнопка будет погашена");
                Assert.IsNotNull(command.Execute, "у команды нет обработчика: пункт есть, а работы нет");
                Assert.IsNotNull(command.IsChecked, "нет признака «проверяемость» — пункт не покажет состояние");
                Assert.IsNotEmpty(command.Title, "без заголовка пункт меню безымянный");
                Assert.IsNotEmpty(command.MenuPath, "без пути меню пункт не появится");
                Assert.IsTrue(KvCommands.IsEnabled("ar.anchor.marker"), "команда обязана быть доступной");

                // Выполняется без AR-сцены: компонента в пустой сцене нет — обработчик обязан
                // честно отработать, а не упасть исключением.
                Assert.DoesNotThrow(() => KvCommands.Invoke("ar.anchor.marker"),
                    "переключатель упал без AR-сцены");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>ПЕРЕКЛЮЧАТЕЛЬ МЕНЯЕТ СОСТОЯНИЕ (иначе пункт в меню — «кнопка без эффекта»).</summary>
        [Test]
        public void AnchorToggle_FlipsStateAndStatusLine()
        {
            GameObject host;
            KvArWorld world;
            KvArPlacement placement;
            KvArMarkerAnchor anchor = BuildAnchor(out host, out world, out placement);
            try
            {
                Assert.IsTrue(anchor.anchorEnabled, "якорь включён по умолчанию — это и есть новое поведение");
                anchor.Tick(Dt, 0f, new Pose(Vector3.zero, Quaternion.identity), Quaternion.identity);
                string on = anchor.StatusLine;
                Assert.IsNotEmpty(on);

                anchor.SetEnabled(false);
                string off = anchor.StatusLine;
                Assert.AreNotEqual(on, off,
                    "переключатель не изменил строку состояния — оператор не увидит эффекта нажатия");
                StringAssert.Contains("выключен", off, off);

                anchor.SetEnabled(true);
                anchor.Tick(Dt, Dt, new Pose(Vector3.zero, Quaternion.identity), Quaternion.identity);
                Assert.AreNotEqual(off, anchor.StatusLine, "после включения статус обязан вернуться");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ================================================================ детектор с общими буферами

        /// <summary>
        /// ПУТЬ С ПЕРЕИСПОЛЬЗУЕМЫМ БУФЕРОМ ЯРКОСТИ даёт ТЕ ЖЕ УГЛЫ, что и путь с новыми буферами:
        /// покадровый скан не имеет права менять геометрию (иначе якорь дрожал бы иначе, чем постановка).
        /// </summary>
        [Test]
        public void QrDetector_WithSharedBuffer_FindsSameCorners()
        {
            Vector2 tl, tr, br, bl;
            Color32[] image = DrawQr(320, 240, 100, 60, 6, 21, out tl, out tr, out br, out bl);
            byte[] sharedLuminance = new byte[320 * 240];

            Vector2[] fresh;
            string freshReason;
            Assert.IsTrue(KvQrMarkerDetector.TryFindCorners(image, 320, 240, out fresh, out freshReason),
                freshReason);

            Vector2[] reused;
            string reusedReason;
            Assert.IsTrue(KvQrMarkerDetector.TryFindCorners(image, 320, 240, sharedLuminance,
                out reused, out reusedReason), reusedReason);

            Assert.AreEqual(fresh.Length, reused.Length, "число углов разное");
            for (int i = 0; i < fresh.Length; i++)
            {
                Assert.That(Vector2.Distance(fresh[i], reused[i]), Is.LessThan(0.01f),
                    "угол " + i + " разошёлся на пути с общим буфером: " + fresh[i] + " vs " + reused[i]);
            }

            // Повторный вызов с тем же буфером даёт тот же результат (буфер не «пачкается»).
            Vector2[] again;
            string againReason;
            Assert.IsTrue(KvQrMarkerDetector.TryFindCorners(image, 320, 240, sharedLuminance,
                out again, out againReason), againReason);
            for (int i = 0; i < fresh.Length; i++)
                Assert.That(Vector2.Distance(fresh[i], again[i]), Is.LessThan(0.01f));

            // Буфер слишком мал — детектор обязан взять свой, а не читать мимо.
            Vector2[] small;
            string smallReason;
            Assert.IsTrue(KvQrMarkerDetector.TryFindCorners(image, 320, 240, new byte[10],
                out small, out smallReason), smallReason);
            for (int i = 0; i < fresh.Length; i++)
                Assert.That(Vector3.Distance(fresh[i], small[i]), Is.LessThan(0.01f));
        }
    }
}
