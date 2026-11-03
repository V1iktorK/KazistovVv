using System.IO;
using KazistovVvAr;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ AR: ПОСТАНОВКА НА СТОЛ, МАСШТАБ 1:1, КАСАНИЯ И ОТКАЗЫ.
    ///
    /// ЗДЕСЬ ПРОВЕРЯЕТСЯ МАТЕМАТИКА ПРИЁМКИ, а не «нарисовано ли окно»:
    ///   Б1 — основание робота = точка попадания в стол;
    ///   Б2/Б3 — отклонение основания от плоскости считается и ограничено 5 мм;
    ///   Б3 — поза робота не зависит от камеры (в формуле позиции камеры нет);
    ///   В2 — пропорции виртуального стола = 10:7, как у реального 100×70;
    ///   Ж1/Ж2 — понятные сообщения для «нет ARCore» и «нет камеры»;
    ///   Г1/Г3/Г4/Г6 — путь нажатия идёт через те же функции меню/потока, что в шлеме.
    ///
    /// Всё это — EditMode: сцену и телефон не запускаем (иначе пункт проверялся бы только у
    /// владельца, а по правилам проекта он должен проверяться машинно).
    /// </summary>
    public class KvArPlacementTests
    {
        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        private static string ReadScript(string name)
        {
            string path = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", "Ar", name);
            Assert.IsTrue(File.Exists(path), "нет файла AR-скрипта " + path);
            return File.ReadAllText(path);
        }

        // ================================================================ Б1/Б2/Б3: ПОСТАНОВКА

        [Test]
        public void RobotBase_IsExactlySurfaceHitPoint()
        {
            Vector3 surface = new Vector3(0.37f, 0.78f, 1.42f);
            Assert.AreEqual(surface, KvArPlacementMath.RobotBasePosition(surface),
                "основание робота обязано совпадать с точкой попадания: иначе Б1 — «висит в воздухе»");
        }

        [Test]
        public void BaseHeightError_MeasuredInMeters()
        {
            Assert.AreEqual(0.003f, KvArPlacementMath.BaseHeightErrorMeters(
                new Vector3(0f, 0.783f, 0f), 0.78f), 0.0001f);
        }

        [Test]
        public void BaseIsOnPlane_ToleranceFiveMillimeters()
        {
            float plane = 0.78f;
            Assert.IsTrue(KvArPlacementMath.BaseIsOnPlane(new Vector3(0f, plane + 0.0049f, 0f), plane),
                "4,9 мм — в пределах допуска приёмки (5 мм)");
            Assert.IsFalse(KvArPlacementMath.BaseIsOnPlane(new Vector3(0f, plane + 0.0051f, 0f), plane),
                "5,1 мм — уже за пределами допуска Б3");
            Assert.AreEqual(0.005f, KvArPlacementMath.BaseHeightToleranceMeters, 0.0001f,
                "допуск обязан быть 5 мм по чек-листу Б3");
        }

        [Test]
        public void YawTowardsCamera_PointsRobotToOperator()
        {
            Vector3 surface = Vector3.zero;
            Assert.AreEqual(180f, KvArPlacementMath.YawTowardsCamera(surface, new Vector3(0f, 1f, -2f)), 0.5f,
                "камера позади робота — робот должен быть развёрнут на 180°");
            Assert.AreEqual(90f, KvArPlacementMath.YawTowardsCamera(surface, new Vector3(2f, 1f, 0f)), 0.5f,
                "камера справа — поворот 90°");
        }

        [Test]
        public void YawTowardsCamera_DoesNotAffectPosition_SoCameraCannotMoveRobot()
        {
            // Главный инвариант Б3: камера влияет ТОЛЬКО на поворот. Позиция считается от
            // плоскости и не содержит камеры — поэтому «робот уполз за камерой» невозможен
            // по построению, а не «потому что мы аккуратно написали».
            Vector3 surface = new Vector3(1f, 0.75f, -2f);
            Vector3 a = KvArPlacementMath.RobotBasePosition(surface);
            KvArPlacementMath.YawTowardsCamera(surface, new Vector3(10f, 1.5f, 10f));
            Vector3 b = KvArPlacementMath.RobotBasePosition(surface);
            Assert.AreEqual(a, b, "позиция робота не должна меняться от положения камеры");
        }

        [Test]
        public void ReticleRotation_LiesOnSurfaceNormal()
        {
            Quaternion rotation = KvArPlacementMath.ReticleRotation(Vector3.up);
            Vector3 mapped = rotation * Vector3.forward;
            Assert.AreEqual(Vector3.up.x, mapped.x, 0.001f);
            Assert.AreEqual(Vector3.up.y, mapped.y, 0.001f);
            Assert.AreEqual(Vector3.up.z, mapped.z, 0.001f);
        }

        [Test]
        public void Reticle_ShownOnlyUntilWorldPlaced()
        {
            Assert.IsTrue(KvArPlacementMath.ShouldShowReticle(false, true));
            Assert.IsFalse(KvArPlacementMath.ShouldShowReticle(false, false));
            Assert.IsFalse(KvArPlacementMath.ShouldShowReticle(true, true),
                "после постановки маркер обязан исчезнуть, иначе оператор думает, что «ещё ставится»");
        }

        [Test]
        public void PlacementHints_AreDifferentForEachState()
        {
            string waiting = KvArPlacementMath.Hint(KvArPlacementState.WaitingForPlanes, 0, 0f);
            string ready = KvArPlacementMath.Hint(KvArPlacementState.ReadyToPlace, 1, 1.5f);
            string placed = KvArPlacementMath.Hint(KvArPlacementState.Placed, 1, 1.5f);
            StringAssert.Contains("плоскостей: 0", waiting);
            StringAssert.Contains("Стол найден", ready);
            StringAssert.Contains("Робот стоит", placed);
            Assert.AreNotEqual(waiting, ready);
            Assert.AreNotEqual(ready, placed);
        }

        [Test]
        public void Placement_SourceUsesArAnchorAndNeverCameraParent()
        {
            string world = ReadScript("KvArWorld.cs");
            StringAssert.Contains("AttachAnchor", world, "основной путь якоря обязан быть AttachAnchor(ARPlane …)");
            StringAssert.Contains("TryAddAnchorAsync", world, "нужен запасной асинхронный путь якоря");
            StringAssert.Contains("SetParent(anchor.transform", world,
                "мир обязан быть ребёнком якоря — иначе он не привязан к реальному столу");
            StringAssert.DoesNotContain("SetParent(arCamera", world,
                "мир нельзя привязывать к камере (прямой запрет S3.1)");
            StringAssert.DoesNotContain("SetParent(camera", world,
                "мир нельзя привязывать к камере (прямой запрет S3.1)");
        }

        // ================================================================ В2: СТОЛ 10:7

        [Test]
        public void VirtualTable_ProportionsAre10to7_ForRealTable()
        {
            Assert.IsTrue(KvArPlacementMath.TableProportionsMatch(0.72f, 0.504f),
                "виртуальный стол проекта 0,72 × 0,504 м обязан иметь пропорцию 10:7");
            Assert.IsTrue(KvArPlacementMath.TableProportionsMatch(1.0f, 0.7f),
                "реальный стол 100 × 70 см — эталон пропорции");
            Assert.IsTrue(KvArPlacementMath.TableProportionsMatch(14.4f, 10.08f),
                "стол сцены проекта (14,4 × 10,08) тоже 10:7");
        }

        [Test]
        public void VirtualTable_RejectsWrongProportions()
        {
            Assert.IsFalse(KvArPlacementMath.TableProportionsMatch(0.72f, 0.48f),
                "старый стол 3:2 (0,72 × 0,48) НЕ соответствует реальному 10:7 — тест-сторож");
            Assert.AreEqual(float.MaxValue, KvArPlacementMath.TableProportionError(0f, 0.5f));
        }

        // ================================================================ В1: МАСШТАБ 1:1

        [Test]
        public void DefaultRobotHeight_IsPlausibleRealSize()
        {
            Assert.IsTrue(KvArContent.IsPlausibleRobotHeight(KvArContent.ExpectedDefaultRobotHeightMeters),
                "штатная высота робота 1,61 м обязана попадать в правдоподобный диапазон");
            Assert.IsTrue(KvArContent.IsPlausibleRobotHeight(0.961f), "COMAU e.DO 0,96 м — реальный робот");
            Assert.IsTrue(KvArContent.IsPlausibleRobotHeight(2.051f), "ABB IRB 6640 2,05 м — реальный робот");
        }

        [Test]
        public void ImplausibleHeights_Rejected()
        {
            Assert.IsFalse(KvArContent.IsPlausibleRobotHeight(0.02f), "робот-«игрушка» 2 см — это дефект масштаба");
            Assert.IsFalse(KvArContent.IsPlausibleRobotHeight(10f), "робот-«гигант» 10 м — это дефект масштаба");
        }

        [Test]
        public void Content_SpawnsRobotUnderAnchorRootAndKeepsScale()
        {
            string content = ReadScript("KvArContent.cs");
            StringAssert.Contains("SetParent(labRoot", content,
                "робот обязан становиться ребёнком корня мира (под якорем)");
            StringAssert.DoesNotContain("localScale = new Vector3(2", content,
                "никакого масштабирования робота: AR — 1:1");
            StringAssert.Contains("MeasureHeightMeters", content,
                "высота робота обязана измеряться и печататься в журнал (число для отчёта)");
        }

        // ================================================================ Ж1/Ж2: ОТКАЗЫ

        [Test]
        public void SessionMessages_AreHumanReadable()
        {
            string unsupported = KvArSessionController.MessageFor(ARSessionState.Unsupported);
            StringAssert.Contains("не поддерживает", unsupported);
            StringAssert.Contains("не падает", unsupported);
            Assert.Greater(unsupported.Length, 80);

            string needsInstall = KvArSessionController.MessageFor(ARSessionState.NeedsInstall);
            StringAssert.Contains("Google Play Services for AR", needsInstall);
            StringAssert.Contains("Установить", needsInstall);
        }

        [Test]
        public void SessionMessages_NoMessagesWhileWorking()
        {
            Assert.IsNull(KvArSessionController.MessageFor(ARSessionState.SessionTracking));
            Assert.IsNull(KvArSessionController.MessageFor(ARSessionState.SessionInitializing));
        }

        [Test]
        public void SessionState_Classification()
        {
            Assert.IsTrue(KvArSessionController.IsReadyState(ARSessionState.SessionTracking));
            Assert.IsTrue(KvArSessionController.IsReadyState(ARSessionState.Ready));
            Assert.IsFalse(KvArSessionController.IsReadyState(ARSessionState.Unsupported));
            Assert.IsTrue(KvArSessionController.IsFailureState(ARSessionState.Unsupported));
            Assert.IsFalse(KvArSessionController.IsFailureState(ARSessionState.SessionTracking));
        }

        [Test]
        public void SessionState_DescriptionsNameTheReason()
        {
            StringAssert.Contains("не поддерживает", KvArSessionController.DescribeState(ARSessionState.Unsupported));
            StringAssert.Contains("отслеживание", KvArSessionController.DescribeState(ARSessionState.SessionTracking));
            StringAssert.Contains("мало света", KvArSessionController.DescribeNotTracking(
                NotTrackingReason.InsufficientLight));
            StringAssert.Contains("камера", KvArSessionController.DescribeNotTracking(
                NotTrackingReason.CameraUnavailable));
        }

        [Test]
        public void SessionController_UsesArFoundationForInstallAndPermission()
        {
            string source = ReadScript("KvArSessionController.cs");
            StringAssert.Contains("ARSession.Install()", source,
                "установка Google Play Services for AR должна идти через ARSession.Install");
            StringAssert.Contains("permissionGranted", source,
                "разрешение камеры проверяется по ARCameraManager.permissionGranted (Ж2)");
            StringAssert.Contains("InvalidOperationException", source,
                "ARSession.Install бросает исключение в Unsupported — оно обязано ловиться");
        }

        // ================================================================ Г: КАСАНИЯ И МЕНЮ

        [Test]
        public void TouchInput_PressesMenuThroughSameGeometryAsHeadset()
        {
            string source = ReadScript("KvArTouchInput.cs");
            StringAssert.Contains("TryAim", source,
                "палец обязан наводить панель тем же TryAim, что луч контроллера");
            StringAssert.Contains("PressIndex", source,
                "нажатие обязано идти через PressIndex — второй копии выполнения команд быть не должно");
            StringAssert.Contains("KvXrPanels", source,
                "поиск панелей — через реестр KvXrPanels");
        }

        [Test]
        public void TouchInput_FeedsFlowSameAsController()
        {
            string source = ReadScript("KvArTouchInput.cs");
            StringAssert.Contains("UpdateAim(", source,
                "прицел и подтверждение точки обязаны подаваться в TrajectoryFlowController");
            StringAssert.Contains("TrajectoryFlowController", source);
            StringAssert.Contains("ZoomView", source, "щипок обязан менять масштаб ВИДА");
        }

        [Test]
        public void ViewZoom_ChangesFieldOfViewNotRobotScale()
        {
            string bridge = ReadScript("KvArRigBridge.cs");
            StringAssert.Contains("fieldOfView", bridge,
                "масштаб вида — это угол обзора камеры, а не масштаб робота (Г6)");
            StringAssert.Contains("ZoomView", bridge);
            float fov = KvArRigBridge.FieldOfViewFor(60f, 2f, 20f, 80f);
            Assert.AreEqual(30f, fov, 0.01f, "щипок «ближе» сужает угол обзора вдвое");
            Assert.AreEqual(60f, KvArRigBridge.FieldOfViewFor(60f, 1f, 20f, 80f), 0.01f);
        }

        [Test]
        public void TouchThresholds_AreFingerFriendly()
        {
            // Порог тапа/перетаскивания существует и не нулевой: иначе «тап» срабатывал бы на
            // любом микро-движении пальца и меню было бы не нажать.
            string source = ReadScript("KvArTouchInput.cs");
            StringAssert.Contains("tapMaxMovePixels", source);
            StringAssert.Contains("tapMaxSeconds", source);
            StringAssert.Contains("dragDegreesPerPixel", source);
        }

        [Test]
        public void Runtime_DeclaresArModeAndTouchesAreTheInput()
        {
            string source = ReadScript("KvArRuntime.cs");
            StringAssert.Contains("РЕЖИМ: ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ", source,
                "строка A3 обязана печататься из AR-сцены");
            StringAssert.Contains("KvArTouchInput", source);
            StringAssert.Contains("targetFrameRate", source,
                "цель по кадрам (Е1 — не ниже 30) обязана задаваться в AR");
        }

        [Test]
        public void RigBridge_CopiesArCameraPoseToLogicalHead()
        {
            string bridge = ReadScript("KvArRigBridge.cs");
            StringAssert.Contains("SetPositionAndRotation(source.position, source.rotation)", bridge,
                "мост обязан копировать позу AR-камеры как есть — без своих поправок");
            Assert.AreEqual(new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f)),
                KvArRigBridge.HeadPoseFrom(new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f))));
        }

        [Test]
        public void LooseObjects_AreAdoptedUnderAnchor()
        {
            string world = ReadScript("KvArWorld.cs");
            StringAssert.Contains("AdoptLooseObjects", world,
                "объекты из меню (столы/роботы) обязаны привязываться под якорь — иначе они «плывут»");
            StringAssert.Contains("SetParent(LabRoot, worldPositionStays: true)", world);
        }

        // ================================================================ ХУКИ ФАБРИКИ: VR НЕ МЕНЯЕТСЯ

        [Test]
        public void ObjectFactory_HooksAreNullByDefault_SoVrIsUnchanged()
        {
            Assert.IsNull(KvXrObjectFactory.SpotOverride,
                "хук AR обязан быть выключен по умолчанию: в шлеме и на компьютере поведение прежнее");
            Assert.IsNull(KvXrObjectFactory.TableFloorFixup);
            Assert.IsNull(KvXrObjectFactory.RobotPositionFixup);
        }

        [Test]
        public void ObjectFactory_SpotInFrontOf_UnchangedWithoutHooks()
        {
            GameObject go = new GameObject("KvArTestCamera");
            try
            {
                Camera camera = go.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 2f, -5f);
                camera.transform.rotation = Quaternion.identity;
                Vector3 spot = KvXrObjectFactory.SpotInFrontOf(camera, 1.2f);
                Assert.AreEqual(0f, spot.x, 0.001f);
                Assert.AreEqual(0f, spot.y, 0.001f, "в VR пол — мировой y = 0 (поведение не изменилось)");
                Assert.AreEqual(-3.8f, spot.z, 0.001f, "точка — 1,2 м перед камерой");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ObjectFactory_SpotOverride_IsUsedAndClearable()
        {
            GameObject go = new GameObject("KvArTestCamera");
            try
            {
                Camera camera = go.AddComponent<Camera>();
                KvXrObjectFactory.SpotOverride = (cam, distance) => new Vector3(3f, 0.78f, 4f);
                Vector3 spot = KvXrObjectFactory.SpotInFrontOf(camera, 1.2f);
                Assert.AreEqual(new Vector3(3f, 0.78f, 4f), spot,
                    "AR-хук обязан подменять точку постановки (плоскость стола вместо мирового нуля)");
            }
            finally
            {
                KvXrObjectFactory.SpotOverride = null;
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================ UI И СТАТУС

        [Test]
        public void StatusLine_ContainsFactsNotWords()
        {
            string line = KvArUi.BuildStatusLine(true, true, 3, true, true, 30f, 0.78f);
            StringAssert.Contains("камера есть", line);
            StringAssert.Contains("плоскостей: 3", line);
            StringAssert.Contains("робот поставлен", line);
            StringAssert.Contains("0.78 м", line);
            StringAssert.Contains("30 кадр/с", line);
        }

        [Test]
        public void StatusLine_WhenNothingFound_TellsUserWhatToDo()
        {
            string line = KvArUi.BuildStatusLine(false, false, 0, false, false, 0f, -9999f);
            StringAssert.Contains("камера НЕТ", line);
            StringAssert.Contains("наведите на стол", line);
        }

        [Test]
        public void Ui_UsesBigTouchFriendlyButtons()
        {
            string source = ReadScript("KvArUi.cs");
            StringAssert.Contains("buttonHeight = 96f", source,
                "кнопка 96 px при 1920×1080 — это ~13 мм на планшете, палец попадает без прицеливания");
            StringAssert.Contains("ScreenSpaceOverlay", source);
            StringAssert.Contains("EventSystem", source);
        }

        [Test]
        public void Ui_MessageScreenHasInstallButtonForNeedsInstall()
        {
            string source = ReadScript("KvArUi.cs");
            StringAssert.Contains("ShowMessage", source);
            StringAssert.Contains("messageButton", source, "у сообщения Ж1/Ж2 должна быть кнопка действия");
        }

        // ================================================================ СВЯЗЬ С ПРЕЖНИМ РЕЖИМОМ

        [Test]
        public void PlatformMode_ChooseAr_WhenNoPassthroughButAnchorsAndCamera()
        {
            Assert.AreEqual(KvXrPlatformMode.Ar,
                KvXrPlatformModeSelector.Choose(false, true, true),
                "телефон без сквозного вида, но с камерой и якорями — это AR, а не VR");
            Assert.AreEqual(KvXrPlatformMode.Vr, KvXrPlatformModeSelector.Choose(false, false, true),
                "камера без якорей — это VR: AR без опоры на мир бесполезен");
        }

        [Test]
        public void PlatformMode_ArDescriptionContainsArVerdict()
        {
            string line = KvXrPlatformModeSelector.Describe(KvXrPlatformMode.Ar, false, true, true, 1f);
            StringAssert.Contains("ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ", line);
        }

        [Test]
        public void Boot_WaitsForXrInitializationNotJustOneFrame()
        {
            string source = ReadScript("KvArBoot.cs");
            StringAssert.Contains("isInitializationComplete", source,
                "boot обязан ждать инициализацию XR (иначе первый запуск на шлеме с диалогом разрешений " +
                "навсегда остаётся в VR — этот дефект уже был в S45)");
            StringAssert.Contains("xrInitTimeoutSeconds", source);
        }

        [Test]
        public void Configuration_ArSceneAndBootPathsExistInConstants()
        {
            Assert.AreEqual("Assets/_Project/00_Scenes/ArMain.unity", KvArMode.ArScenePath);
            Assert.AreEqual("Assets/_Project/00_Scenes/BootAr.unity", KvArMode.BootScenePath);
            Assert.AreEqual("XrMain", KvArMode.VrSceneName);
            Assert.AreEqual("ArMain", KvArMode.ArSceneName);
        }
    }
}
