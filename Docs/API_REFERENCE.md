# API_REFERENCE.md — справочник публичного API проекта KazistovVv

**Сгенерировано автоматически** скриптом `_tools\kv_api.ps1` из исходного кода (XML-комментарии `/// <summary>`).
Дата генерации: **15.09.2026**. Проект: **KazistovVv** (Unity 

| Показатель | Значение |
|---|---:|
| Пространств имён | 7 |
| Публичных типов | 396 |
| Публичных членов | 4257 |
| Проанализировано файлов `*.cs` | 178 |

> **Как читать.** Описание берётся из XML-комментария, стоящего непосредственно перед объявлением.
> Если описание пустое — значит в коде у этого члена нет `<summary>` (это не ошибка генератора).
> Сигнатуры приведены в том виде, в каком объявлены, с сокращением длинных строк.

## Оглавление

- **`(глобальное пространство имён)`** — 68 типов
  - `class AimIndicator` — 21 членов
  - `class ARInputProvider` — 13 членов
  - `class CalibrationTool` — 4 членов
  - `class CollisionGuard` — 3 членов
  - `class ConvertRobotMaterialsToHDRP` — 6 членов
  - `class DshDesktopUiDiag` — 1 членов
  - `class DshFeaturesDiag` — 1 членов
  - `class DshFullVerifyDiag` — 1 членов
  - `class DshScaraDiag` — 1 членов
  - `class DshScaraEightDiag` — 1 членов
  - `class DshScreenshotsDiag` — 6 членов
  - `class DshStage2Diag` — 1 членов
  - `class DshStage3Diag` — 1 членов
  - `class DshStage4Diag` — 1 членов
  - `class DshStageDiag` — 1 членов
  - `class DshUiStagesDiag` — 1 членов
  - `class FreeFlyCameraController` — 61 членов
  - `class GamepadInputProvider` — 13 членов
  - `class GhostView` — 5 членов
  - `class HDRPAutoLighting` — 15 членов
  - `class HierarchyAutoRefresh` — 2 членов
  - `class HierarchyPhantomCleaner` — 2 членов
  - `enum InputDevice` — 13 членов
  - `class InputDeviceExtensions` — 2 членов
  - `enum InputDeviceType` — 0 членов
  - `class InputManager` — 1 членов
  - `class InputProvider` — 12 членов
  - `class InverseKinematics` — 10 членов
  - `class KeyboardController` — 2 членов
  - `class KeyboardMouseInputProvider` — 14 членов
  - `class MainMenu` — 3 членов
  - `enum MetricSort` — 33 членов
  - `class MotionExecutor` — 9 членов
  - `class MRInputProvider` — 10 членов
  - `class Palette` — 4 членов
  - `class PlacementController` — 3 членов
  - `class PlanMetrics` — 7 членов
  - `class PointMoveHud` — 20 членов
  - `class PoseSelector` — 4 членов
  - `class Readme` — 4 членов
  - `class ReadmeEditor` — 1 членов
  - `class RobotController` — 22 членов
  - `class RobotInventory` — 2 членов
  - `class RobotSelector` — 3 членов
  - `class RobotSelfCollision` — 4 членов
  - `class SCARAController` — 10 членов
  - `class Section` — 0 членов
  - `class SettingsData` — 43 членов
  - `class SettingsMenu` — 5 членов
  - `class SettingsSaver` — 3 членов
  - `class SixAxisAutoSetup` — 4 членов
  - `class SixAxisController` — 24 членов
  - `class SpatialAnchorManager` — 5 членов
  - `class StandsMenu` — 2 членов
  - `class TargetMarker` — 5 членов
  - `enum ToolOffsetMode` — 55 членов
  - `class TrajectoryDataWrapper` — 1 членов
  - `class TrajectoryExecutor` — 13 членов
  - `class TrajectoryFlowController` — 24 членов
  - `class TrajectoryMetricsPanel` — 0 членов
  - `class TrajectoryPlannerController` — 7 членов
  - `class TrajectoryPlayer` — 7 членов
  - `class TrajectoryPoint` — 4 членов
  - `class TrajectoryRecorder` — 4 членов
  - `class VRHandTracker` — 2 членов
  - `class VRInputManager` — 1 членов
  - `class VRInputProvider` — 10 членов
  - `class WorkspaceVisualizer` — 58 членов
- **`KazistovVvKinematics`** — 3 типов
  - `class DHForward` — 2 членов
  - `class DHInverse` — 1 членов
  - `class DHLink` — 7 членов
- **`KazistovVvFeatures`** — 211 типов
  - `enum CameraMode` — 14 членов
  - `class Draft` — 28 членов
  - `class Draft` — 23 членов
  - `class FeatureHub` — 51 членов
  - `class FeatureStorage` — 17 членов
  - `interface IKvSpeechSource` — 0 членов
  - `interface IKvUndoAction` — 0 членов
  - `interface IKvWorkbenchTab` — 0 членов
  - `class KvActionLog` — 20 членов
  - `class KvAnnotation` — 3 членов
  - `class KvBehaviorTab` — 5 членов
  - `class KvBehaviorTree` — 10 членов
  - `class KvBehaviorTreeWindow` — 7 членов
  - `class KvBenchResult` — 11 членов
  - `class KvBenchTask` — 9 членов
  - `class KvBtNode` — 7 членов
  - `enum KvBtNodeType` — 0 членов
  - `class KvBtNodeView` — 6 членов
  - `class KvBtRunner` — 10 членов
  - `enum KvBtStatus` — 0 членов
  - `class KvCalibrationData` — 31 членов
  - `class KvCalibrationService` — 37 членов
  - `class KvCalibrationTab` — 5 членов
  - `class KvCameraService` — 13 членов
  - `class KvCameraTab` — 5 членов
  - `class KvCaptureService` — 25 членов
  - `class KvCinemaTab` — 5 членов
  - `class KvCinematicService` — 0 членов
  - `class KvClearanceOverlay` — 17 членов
  - `class KvCollaborationService` — 29 членов
  - `class KvCollisionBenchmark` — 11 членов
  - `class KvCollisionOptimizer` — 13 членов
  - `class KvCollisionProxyInfo` — 12 членов
  - `class KvCollisionTab` — 5 членов
  - `class KvCompanionServer` — 12 членов
  - `class KvComparison` — 13 членов
  - `class KvConstrainedPlanner` — 30 членов
  - `class KvConstrainedTab` — 5 членов
  - `class KvDelegateAction` — 6 членов
  - `struct KvDwell` — 3 членов
  - `class KvDynamicObstacleService` — 33 членов
  - `class KvEnergyModel` — 7 членов
  - `class KvEnergyOptimal` — 2 членов
  - `class KvEnergyTab` — 5 членов
  - `class KvEnvironmentPreset` — 14 членов
  - `class KvEnvironmentStudio` — 12 членов
  - `class KvEnvironmentTab` — 5 членов
  - `class KvExportTab` — 5 членов
  - `class KvExpr` — 6 членов
  - `class KvEyesTab` — 5 членов
  - `class KvEyeTrackingService` — 19 членов
  - `enum KvFailureKind` — 0 членов
  - `class KvFailureSimulator` — 26 членов
  - `class KvFailureTab` — 5 членов
  - `class KvFeatureDrag` — 3 членов
  - `class KvFeatureWindow` — 19 членов
  - `class KvFinding` — 6 членов
  - `class KvForceHeatTab` — 5 членов
  - `class KvForceVisualizer` — 12 членов
  - `class KvFoveatedRendering` — 5 членов
  - `class KvGraphics` — 3 членов
  - `class KvGripper` — 24 членов
  - `class KvHandsTab` — 5 членов
  - `class KvHandTrackingService` — 19 членов
  - `class KvHaptics` — 18 членов
  - `enum KvHapticStrength` — 0 членов
  - `class KvHealthMonitor` — 39 членов
  - `class KvHealthPanel` — 12 членов
  - `struct KvHealthSample` — 2 членов
  - `class KvHighlightFrame` — 8 членов
  - `class KvHintCard` — 10 членов
  - `class KvHoverHint` — 5 членов
  - `class KvImportedRobot` — 14 членов
  - `enum KvImportPlacement` — 0 членов
  - `class KvImportTab` — 5 членов
  - `class KvInputKit` — 3 членов
  - `enum KvJointFailureMode` — 0 членов
  - `class KvJointGraph` — 11 членов
  - `class KvLightingPreset` — 18 членов
  - `class KvLightingStudio` — 21 членов
  - `class KvLightingTab` — 5 членов
  - `class KvLocExtra` — 5 членов
  - `class KvLocExtra2` — 4 членов
  - `class KvLocExtra3` — 4 членов
  - `class KvLogEntry` — 5 членов
  - `enum KvLogKind` — 0 членов
  - `enum KvLogLevel` — 0 членов
  - `class KvLogTools` — 17 членов
  - `class KvLogToolsTab` — 5 членов
  - `class KvMaterialEdit` — 6 членов
  - `class KvMaterialEditsFile` — 2 членов
  - `class KvMaterialStudio` — 32 членов
  - `class KvMaterialTab` — 5 членов
  - `class KvMiniPlot` — 3 членов
  - `class KvMiniTip` — 5 членов
  - `class KvMotionLimits` — 7 членов
  - `enum KvNetRole` — 0 членов
  - `class KvNetTab` — 5 членов
  - `enum KvObstacleMode` — 0 членов
  - `class KvOrientConstraint` — 9 членов
  - `enum KvOrientMode` — 0 членов
  - `class KvOverlayKit` — 3 членов
  - `class KvPathSmoothing` — 24 членов
  - `class KvPayloadCalculator` — 13 членов
  - `class KvPayloadModel` — 6 членов
  - `class KvPayloadResult` — 12 членов
  - `class KvPayloadTab` — 5 членов
  - `class KvPdfWriter` — 12 членов
  - `enum KvPendantAttach` — 0 членов
  - `enum KvPendantJog` — 0 членов
  - `class KvPendantStick` — 4 членов
  - `enum KvPendantVariant` — 0 членов
  - `class KvPickAndPlace` — 0 членов
  - `class KvPiPWindow` — 9 членов
  - `class KvPlanKit` — 2 членов
  - `class KvPlannerLab` — 19 членов
  - `class KvPlannerLabTab` — 5 членов
  - `class KvPlannerPerformance` — 11 членов
  - `enum KvPlanRisk` — 0 членов
  - `class KvPlanRun` — 11 членов
  - `class KvPlanRunList` — 2 членов
  - `enum KvPlanStrategy` — 0 членов
  - `class KvPoseLibrary` — 13 членов
  - `class KvPosePreset` — 13 членов
  - `class KvPoseStore` — 5 членов
  - `class KvPreRunValidator` — 19 членов
  - `class KvPresentationMode` — 19 членов
  - `class KvQuickStart` — 20 членов
  - `class KvReachabilityHeatmap` — 19 членов
  - `class KvRecordingService` — 31 членов
  - `class KvRecordSample` — 6 членов
  - `class KvRecordStore` — 6 членов
  - `class KvReportGenerator` — 9 членов
  - `class KvReportTab` — 5 членов
  - `struct KvRiskResult` — 5 членов
  - `class KvRobotExporter` — 19 членов
  - `class KvRobotImportService` — 16 членов
  - `enum KvRobotLanguage` — 0 членов
  - `class KvScenario` — 4 членов
  - `class KvScenarioManager` — 17 членов
  - `class KvScenarioStep` — 5 членов
  - `class KvScriptEngine` — 3 членов
  - `class KvScriptError` — 3 членов
  - `class KvScriptMacro` — 3 членов
  - `class KvScriptParser` — 1 членов
  - `class KvScriptTab` — 5 членов
  - `class KvSession` — 17 членов
  - `class KvSessionFlags` — 7 членов
  - `class KvSessionManager` — 11 членов
  - `class KvSessionRobot` — 5 членов
  - `class KvSessionStore` — 5 членов
  - `enum KvSeverity` — 0 членов
  - `enum KvSingularityKind` — 0 членов
  - `class KvSingularityVisualizer` — 27 членов
  - `class KvSmoothEntry` — 13 членов
  - `enum KvSmoothMethod` — 0 членов
  - `class KvSmoothTab` — 5 членов
  - `enum KvSound` — 0 членов
  - `class KvSpatialAudio` — 12 членов
  - `struct KvSpeedCap` — 3 членов
  - `class KvStageHub` — 29 членов
  - `class KvStageHub2` — 34 членов
  - `class KvStageHub3` — 29 членов
  - `class KvStageHub4` — 55 членов
  - `class KvStartMenu` — 32 членов
  - `struct KvStepAxis` — 8 членов
  - `class KvStmt` — 8 членов
  - `enum KvStmtKind` — 0 членов
  - `class KvTabKit` — 10 членов
  - `class KvTeachPendant` — 39 членов
  - `class KvTimeHeatmap` — 13 членов
  - `class KvTimeOptimal` — 4 членов
  - `class KvTimeOptimalTab` — 5 членов
  - `class KvTitleCue` — 4 членов
  - `class KvTitlesService` — 23 членов
  - `class KvTitlesTab` — 5 членов
  - `class KvToolKinematics` — 5 членов
  - `class KvTrajectoryRecord` — 21 членов
  - `class KvTrajMath` — 20 членов
  - `struct KvTrajMetrics` — 12 членов
  - `struct KvTrajStats` — 15 членов
  - `class KvTutorial` — 28 членов
  - `class KvUndoStack` — 16 членов
  - `class KvUrdfJoint` — 10 членов
  - `class KvUrdfLink` — 6 членов
  - `class KvValidateTab` — 5 членов
  - `struct KvValue` — 31 членов
  - `class KvVariantKit` — 9 членов
  - `class KvVec3` — 1 членов
  - `class KvVoiceCommand` — 6 членов
  - `class KvVoiceOverService` — 21 членов
  - `class KvVoiceOverTab` — 5 членов
  - `class KvVoiceService` — 21 членов
  - `class KvVoiceTab` — 5 членов
  - `class KvWaypoint` — 12 членов
  - `class KvWaypointLimits` — 14 членов
  - `class KvWaypointManager` — 42 членов
  - `class KvWaypointRouteKit` — 4 членов
  - `class KvWaypointTab` — 5 членов
  - `class KvWebDashboard` — 12 членов
  - `class KvWindowDrag` — 4 членов
  - `class KvWorkbenchWindow` — 18 членов
  - `class KvZone` — 9 членов
  - `class KvZoneData` — 21 членов
  - `struct KvZoneHit` — 7 членов
  - `class KvZoneMarks` — 8 членов
  - `class KvZoneService` — 27 членов
  - `enum KvZoneShape` — 0 членов
  - `class KvZoneStore` — 3 членов
  - `struct PlanCheck` — 12 членов
  - `enum Step` — 18 членов
- **`TrajectoryCore`** — 37 типов
  - `class CollisionProxies` — 11 членов
  - `class CollisionWorld` — 12 членов
  - `enum FlowState` — 0 членов
  - `class GhostMaterial` — 3 членов
  - `struct GoalConfig` — 14 членов
  - `class HierarchyOrder` — 2 членов
  - `struct IkBranchTag` — 4 членов
  - `struct IkSolution` — 5 членов
  - `class IkSolver` — 9 членов
  - `class KinematicsJacobian` — 4 членов
  - `class LaserManager` — 12 членов
  - `class MotionTiming` — 1 членов
  - `class NarrowPhase` — 3 членов
  - `struct ObstacleBox` — 5 членов
  - `struct ObstacleCapsule` — 5 членов
  - `struct Outcome` — 7 членов
  - `struct PhantomConfig` — 9 членов
  - `class PhantomManager` — 52 членов
  - `class PhantomMath` — 2 членов
  - `class PlannedTrajectory` — 15 членов
  - `class Planner` — 15 членов
  - `class PoseValidator` — 35 членов
  - `class PostureControl` — 10 членов
  - `class PostureSelector` — 14 членов
  - `struct Prim` — 9 членов
  - `enum PrimKind` — 0 членов
  - `class ReachabilityOracle` — 10 членов
  - `struct ReachResult` — 5 членов
  - `enum ReachVerdict` — 0 членов
  - `class SafetyGate` — 11 членов
  - `enum SafetyReason` — 0 членов
  - `class SelectionState` — 18 членов
  - `class StandBuilder` — 16 членов
  - `class ToolAlign` — 0 членов
  - `class TrajectoryCandidate` — 13 членов
  - `class TrajectoryTube` — 13 членов
  - `class TubeMath` — 3 членов
- **`KazistovVvUI`** — 70 типов
  - `class BoostedObjectMarker` — 0 членов
  - `class CenterWindow` — 13 членов
  - `class IdleCameraBrain` — 7 членов
  - `class KazistovVvUIManager` — 91 членов
  - `class KvBindEntry` — 7 членов
  - `class KvBindingEntry` — 4 членов
  - `class KvBindings` — 8 членов
  - `class KvCommand` — 17 членов
  - `class KvCommandPalette` — 14 членов
  - `class KvCommands` — 8 членов
  - `class KvContextMenu` — 6 членов
  - `class KvContextMenuItem` — 8 членов
  - `class KvCursors` — 1 членов
  - `class KvDockDrag` — 5 членов
  - `class KvDockIndicator` — 3 членов
  - `class KvDockPanel` — 50 членов
  - `enum KvDockSide` — 0 членов
  - `class KvGamepadBridge` — 8 членов
  - `class KvGamepadHud` — 5 членов
  - `class KvGamepadRouter` — 15 членов
  - `class KvHotkeyView` — 7 членов
  - `class KvIconButton` — 10 членов
  - `class KvIconCanvas` — 8 членов
  - `class KvIcons` — 7 членов
  - `class KvKeyboardNav` — 11 членов
  - `class KvLangFile` — 5 членов
  - `class KvLangInfo` — 7 членов
  - `class KvLangString` — 2 членов
  - `class KvLayoutStore` — 6 членов
  - `class KvLoc` — 28 членов
  - `class KvMenuBar` — 9 членов
  - `class KvMenuCheck` — 1 членов
  - `class KvPanelLayout` — 11 членов
  - `struct KvProp` — 7 членов
  - `class KvPropertiesView` — 8 членов
  - `enum KvResizeEdge` — 0 членов
  - `class KvResizeHandle` — 9 членов
  - `class KvSegmented` — 1 членов
  - `class KvSelectionHighlight` — 6 членов
  - `class KvSettingEntry` — 7 членов
  - `class KvSettings` — 58 членов
  - `class KvSettingsCallbacks` — 15 членов
  - `class KvSettingsFile` — 2 членов
  - `class KvSettingsSchema` — 5 членов
  - `class KvSettingsView` — 6 членов
  - `class KvSplitter` — 6 членов
  - `class KvStatusBar` — 16 членов
  - `class KvSwitch` — 2 членов
  - `class KvTheme` — 54 членов
  - `enum KvThemeMode` — 0 членов
  - `class KvToolbar` — 41 членов
  - `class KvToolbarGroup` — 4 членов
  - `class KvToolbarGroupMenu` — 5 членов
  - `class KvToolbarGroups` — 4 членов
  - `class KvToolbarMoreMenu` — 5 членов
  - `class KvTooltip` — 10 членов
  - `class KvTooltipTarget` — 6 членов
  - `class KvTreeRow` — 7 членов
  - `class KvTreeView` — 29 членов
  - `class KvUiStates` — 18 членов
  - `class KvWidgets` — 10 членов
  - `class NodeLabels` — 1 членов
  - `class ObjectSpawner` — 5 членов
  - `class ProjectNode` — 14 членов
  - `enum ProjectNodeKind` — 0 членов
  - `class RegisteredObject` — 2 членов
  - `class RuntimeRegistry` — 8 членов
  - `class ScaraCableFollow` — 7 членов
  - `enum SpawnKind` — 0 членов
  - `class UIFactory` — 1 членов
- **`KazistovVvUI.EditorTools`** — 1 типов
  - `class KazistovVvMenu` — 3 членов
- **`KazistovVvTests`** — 6 типов
  - `class KvCalibrationTests` — 32 членов
  - `class KvEnergyOptimalTests` — 37 членов
  - `class KvPayloadCalculatorTests` — 31 членов
  - `class KvRobotExportTests` — 41 членов
  - `class KvTimeOptimalTests` — 52 членов
  - `class KvTrajMathTests` — 44 членов

---

## Пространство имён `(глобальное пространство имён)`

### `class AimIndicator`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\AimIndicator.cs` (строка 11)
- **Назначение:** Индикатор прицела (E5 плана, упрощённый онлайн-вариант): каждый кадр спрашивает Reachability Oracle про точку прицела активного робота и красит маркер: зелёный — достижимо с запасом, жёлтый — предельно/лимит, красный — столкновение или недостижимо. Мир столкновений пересобирается периодически (ст…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `allowSeedSelection` | `public bool allowSeedSelection = false;` |  |
| поле | `clearance` | `public float clearance = 0.02f;` |  |
| поле | `freeEmissionScale` | `public float freeEmissionScale = 0.55f;` |  |
| поле | `linkRadius` | `public float linkRadius = 0.06f;` |  |
| поле | `markerEmission` | `public float markerEmission = 3.2f; // яркость шарика (на уровне лазера или выше)` |  |
| поле | `markerSize` | `public float markerSize = 0.045f; // «шарик» на конце лазера` |  |
| поле | `offsetAlongNormal` | `public bool offsetAlongNormal = true;` |  |
| поле | `surfaceFlatten` | `public float surfaceFlatten = 0.55f;` |  |
| поле | `toolOffset` | `public float toolOffset = 0f;` |  |
| поле | `worldRebuildInterval` | `public float worldRebuildInterval = 0.5f;` |  |
| свойство | `HasResult` | `public bool HasResult { get; private set; }` |  |
| свойство | `Last` | `public ReachResult Last { get; private set; }` |  |
| свойство | `LastBranch` | `public string LastBranch { get; private set; } = "";` |  |
| метод | `BallOnSurface` | `public bool BallOnSurface => onSurfaceNow;` | Шарик «прилип» к поверхности (визуал: сплющен по нормали) — для проверок. |
| метод | `BallSurfaceNormal` | `public Vector3 BallSurfaceNormal => normalNow;` | Мировая нормаль поверхности под шариком (для проверок/визуала). |
| метод | `MarkerScale` | `public Vector3 MarkerScale => marker != null ? marker.transform.localScale : Vector3.zero;` | Текущий масштаб маркера (для автопроверки «сплющен / ровный шар»). |
| метод | `MarkerVisible` | `public bool MarkerVisible => marker != null && marker.activeSelf;` | Виден ли шарик прицела (для проверок). |
| метод | `SetSuspended` | `public void SetSuspended(bool value)` | Приостановить индикатор прицела (используется в режиме перемещения точки: там точку показывает PointMoveHud, а наведение мышью не действует). Оракул в это время не опрашивается. |
| метод | `UpdateAim` | `public void UpdateAim(Vector3 aimPoint, bool hitSurface)` | Вызывается контроллером камеры: точка прицела и попадание в поверхность. |
| метод | `UpdateAim` | `public void UpdateAim(Vector3 aimPoint, bool hitSurface, Vector3 surfaceNormal, bool onSurface)` | То же, но с нормалью поверхности под прицелом: оракул проверяет ТУ ЖЕ точку, что зафиксирует поток этапов (точка поверхности + toolOffset вдоль нормали), а шарик остаётся на поверхности — оператор видит, куда наводит. |
| метод | `UpdateAim` | `public void UpdateAim(Vector3 aimPoint, bool ballExists, bool hitSurface, Vector3 surfaceNormal, bool onSurface)` | Полное обновление прицела (после появления управления глубиной колесом мыши): ballExists — шарик существует и его надо показывать (у него ВСЕГДА есть глубина, даже если луч ушёл в пустоту); hitSurface — луч встретил поверхность (реальную геометрию или рабочую плоскость y = 0); surfaceNormal — нор… |

### `class ARInputProvider`

- **Файл:** `Assets\_Project\01_Scripts\VR\ARInputProvider.cs` (строка 4)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `ARRaycastFromTouch` | `public bool ARRaycastFromTouch(Vector2 touchPos, out Vector3 hitPoint)` |  |
| метод | `GetGrabDown` | `public override bool GetGrabDown() => false;` |  |
| метод | `GetGrabHeld` | `public override bool GetGrabHeld() => false;` |  |
| метод | `GetMovement` | `public override Vector2 GetMovement() => Vector2.zero;` |  |
| метод | `GetPlayDown` | `public override bool GetPlayDown() => false;` |  |
| метод | `GetPointerDirection` | `public override Vector3 GetPointerDirection() => Camera.main?.transform.forward ?? Vector3.forward;` |  |
| метод | `GetPointerPosition` | `public override Vector3 GetPointerPosition() => Camera.main?.transform.position ?? Vector3.zero;` |  |
| метод | `GetRecordDown` | `public override bool GetRecordDown() => false;` |  |
| метод | `GetRotation` | `public override Vector2 GetRotation() => Vector2.zero;` |  |
| метод | `GetSelectDown` | `public override bool GetSelectDown() => Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;` |  |
| метод | `GetSelectHeld` | `public override bool GetSelectHeld() => Input.touchCount > 0;` |  |
| метод | `GetSwitchRobotDown` | `public override bool GetSwitchRobotDown() => false;` |  |
| метод | `IsAvailable` | `public override bool IsAvailable() => Application.platform == RuntimePlatform.Android \|\| Application.platform == RuntimePlatform.IPhonePlayer;` |  |

### `class CalibrationTool`

- **Файл:** `Assets\_Project\01_Scripts\Spatial\CalibrationTool.cs` (строка 3)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `calibrationPointPrefab` | `public GameObject calibrationPointPrefab; // Красная сфера` |  |
| поле | `calibrationUI` | `public GameObject calibrationUI;` |  |
| поле | `virtualRobotBase` | `public Transform virtualRobotBase;` |  |
| метод | `StartCalibration` | `public void StartCalibration()` |  |

### `class CollisionGuard`

- **Файл:** `Assets\_Project\01_Scripts\Integration\CollisionGuard.cs` (строка 3)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `obstacleLayers` | `public LayerMask obstacleLayers; // Стол, стены, другие роботы` |  |
| поле | `robot` | `public RobotController robot;` |  |
| метод | `CanMoveTo` | `public bool CanMoveTo(Vector3 targetPos)` |  |

### `class ConvertRobotMaterialsToHDRP`

- **Файл:** `Assets\_Project\01_Scripts\Editor\ConvertRobotMaterialsToHDRP.cs` (строка 12)
- **Назначение:** Конвертирует встроенные Standard-материалы в Robot.fbx и ScaraRobot.fbx в отдельные HDRP/Lit материалы. НЕ трогает материалы ангара (ROOMS/HQ Hangar Free).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Convert` | `public static void Convert()` |  |
| метод | `ConvertAllInScene` | `public static void ConvertAllInScene()` |  |
| метод | `ConvertSelected` | `public static void ConvertSelected()` |  |
| метод | `ShowWindow` | `public static void ShowWindow()` |  |
| метод | `ValidateConvert` | `public static bool ValidateConvert()` |  |
| метод | `ValidateConvertSelected` | `public static bool ValidateConvertSelected()` |  |

### `class DshDesktopUiDiag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshDesktopUiDiag.cs` (строка 22)
- **Назначение:** Батч-прогон ДЕСКТОПНОГО интерфейса KazistovVv (FreeCAD-стиль): проверяет сборку оболочки, 15 кнопок тулбара в 3 ряда, подсказки, дерево моделей (выбор/глазик/ переименование), свойства, статус-бар, переключение темы с PlayerPrefs, dock-панели, вкладки настроек, реестр команд, производительность и…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Run` | `public static void Run()` |  |

### `class DshFeaturesDiag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshFeaturesDiag.cs` (строка 24)
- **Назначение:** Диагностический прогон этапов 1–20 (запись, позы, суставы, аварийный стоп, зоны, сравнение, графики, тепловые карты, гриппер, pick-and-place, ETA, журнал, замер, Undo/Redo, звук, вибрация, сценарии, презентация, сессии). ЗАПУСК: Unity.exe -batchmode -nographics -projectPath "" -executeMethod DshF…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Run` | `public static void Run()` |  |

### `class DshFullVerifyDiag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshFullVerifyDiag.cs` (строка 25)
- **Назначение:** ПОЛНАЯ ПРОВЕРКА ПРОЕКТА (этапы 1–8 ТЗ): оба робота, интерфейс, взаимодействия, производительность, стабильность, консоль и журнал. ЗАПУСК: Unity.exe -batchmode -nographics -projectPath "" -executeMethod DshFullVerifyDiag.Run -logFile _dsh_full.log (БЕЗ -quit). Отчёт: `_dsh_full_verify.txt` в корн…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Run` | `public static void Run()` |  |

### `class DshScaraDiag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshScaraDiag.cs` (строка 33)
- **Назначение:** ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится). ФИКС 7 сессии 18.09.2026: СКВОЗНОЙ ПРОГОН ВСЕХ 36 ЭТАПОВ НА SCARA (раньше SCARA проверялась только частично — этапы 1–12 и в пакетном режиме). Прогон идёт ОДНИМ проходом по областям из ТЗ: лазеры · State Machine · 8 траекторий · фантомы · …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Run` | `public static void Run()` |  |

### `class DshScaraEightDiag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshScaraEightDiag.cs` (строка 34)
- **Назначение:** ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится). СЕССИЯ «8 ТРАЕКТОРИЙ ДЛЯ SCARA» (15.09.2026): проверяется РОВНО то, что просило ТЗ — сколько УНИКАЛЬНЫХ траекторий (форма пути отличается, критерий `IsSamePath`) даёт SCARA после правки бюджета попыток (`distinctAttemptCap`), вариаций обхо…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Run` | `public static void Run()` |  |

### `class DshScreenshotsDiag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshScreenshotsDiag.cs` (строка 36)
- **Назначение:** ЭТАП 7 (аддитивная диагностика агента): снимки сцен и ключевых объектов. ЗАПУСК (БЕЗ -nographics — нужен настоящий графический контекст): Unity.exe -batchmode -projectPath "" -executeMethod DshScreenshotsDiag.Run -logFile _dsh_shots.log ЧТО ДЕЛАЕТ: 1) открывает КАЖДУЮ сцену проекта ПРИСОЕДИНЁННО …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `File` | `public string File;` |  |
| поле | `Note` | `public string Note;` |  |
| поле | `Scene` | `public string Scene;` |  |
| поле | `What` | `public string What;` |  |
| метод | `Run` | `public static void Run()` | Основной вход: снимки ВСЕХ сцен (без PlayMode). Именно так снимался отчёт 15.09.2026 — 16 PNG в Docs/Screenshots. |
| метод | `RunWithPlayMode` | `public static void RunWithPlayMode()` | Дополнительный вход: дополнительно пытается снять рантайм-интерфейс в PlayMode. ВНИМАНИЕ (проверено 15.09.2026): в batch-режиме БЕЗ -nographics вход в PlayMode на этой машине не завершается — процесс Unity уходит в бесконечный цикл плеера и не возвращается в EditorApplication.update, поэтому сним… |

### `class DshStage2Diag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshStage2Diag.cs` (строка 32)
- **Назначение:** ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится) для этапов 1–6 текущей сессии: главное меню со стартовым облётом, туториал, демонстрация, сглаживание траекторий, время-оптимальная траектория и эко-профиль. Прогон идёт в PlayMode полным циклом сначала на роботе, затем на SCARA. ОСОБЕННОСТ…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Run` | `public static void Run()` |  |

### `class DshStage3Diag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshStage3Diag.cs` (строка 25)
- **Назначение:** ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится) для этапов 7–12: ограничения промежуточных точек, планирование с ограничениями, калибровочный мастер, калькулятор нагрузки, экспорт в языки роботов (KRL / KAREL / RAPID), импорт моделей URDF / STEP с проверкой кинематики. Отчёт читается из …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Run` | `public static void Run()` |  |

### `class DshStage4Diag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshStage4Diag.cs` (строка 29)
- **Назначение:** ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится) для этапов 13–36 прошлой сессии: коллизионные прокси, стенд планировщиков, дерево RRT, камеры и PiP, силы и моменты, тепловая карта времени, PDF-отчёт, совместная работа, веб-дашборд, мобильный пульт, голос, руки, взгляд и фовеальное зрение…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Run` | `public static void Run()` |  |

### `class DshStageDiag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshStageDiag.cs` (строка 32)
- **Назначение:** ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится): сквозная проверка ЭТАПОВ 1–8 сессии 15.09.2026 в PlayMode. Запуск (по §9 PROJECT_CONTEXT.md): Unity.exe -batchmode -nographics -projectPath "" -executeMethod DshStageDiag.Run -logFile _dsh_stages.log (БЕЗ -quit), отчёт — `KavistovVv/_dsh_s…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Run` | `public static void Run()` |  |

### `class DshUiStagesDiag`

- **Файл:** `Assets\_Project\01_Scripts\Editor\DshUiStagesDiag.cs` (строка 31)
- **Назначение:** БАТЧ-ПРОГОН ЭТАПОВ 1–12 сессии «UX/UI + геймпад» (диагностика агента, в копию пользователя НЕ переносится). Проверяет: 1) группировку тулбара (группы, разделители, меню группы, сворачивание, подсказки); 2) КАЖДУЮ команду реестра (кнопку тулбара и пункт меню) — на исключения; 3) dockable/undockabl…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Run` | `public static void Run()` |  |

### `class FreeFlyCameraController`

- **Файл:** `Assets\_Project\01_Scripts\Core\FreeFlyCameraController.cs` (строка 31)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `bodyHeight` | `public float bodyHeight = 1.6f;` |  |
| поле | `bodyRadius` | `public float bodyRadius = 0.25f;` |  |
| поле | `bodySkin` | `public float bodySkin = 0.02f;` |  |
| поле | `boostMultiplier` | `public float boostMultiplier = 2f;` |  |
| поле | `enableCameraCollision` | `public bool enableCameraCollision = true;` |  |
| поле | `enableFlashlight` | `public bool enableFlashlight = true;` |  |
| поле | `enableLaserPointer` | `public bool enableLaserPointer = true; // мастер-выключатель обеих указок` |  |
| поле | `flashlightAngle` | `public float flashlightAngle = 60f;` |  |
| поле | `flashlightColorTemperature` | `public float flashlightColorTemperature = 4500f;` |  |
| поле | `flashlightEnabled` | `public bool flashlightEnabled = false;` |  |
| поле | `flashlightFadeTime` | `public float flashlightFadeTime = 0f;` |  |
| поле | `flashlightInnerAngle` | `public float flashlightInnerAngle = 30f;` |  |
| поле | `flashlightIntensity` | `public float flashlightIntensity = 1500f;` |  |
| поле | `flashlightRadius` | `public float flashlightRadius = 0.025f;` |  |
| поле | `flashlightRange` | `public float flashlightRange = 14f;` |  |
| поле | `flashlightSoftShadows` | `public bool flashlightSoftShadows = true;` |  |
| поле | `gamepadLookSensitivity` | `public float gamepadLookSensitivity = 1.5f;` |  |
| поле | `invertY` | `public bool invertY = false;` |  |
| поле | `laserDownOffset` | `public float laserDownOffset = 0.15f; // опускание рук относительно центра` |  |
| поле | `laserHandOffset` | `public float laserHandOffset = 0.35f; // латеральное смещение рук от центра` |  |
| поле | `laserLayers` | `public LayerMask laserLayers = Physics.DefaultRaycastLayers;` |  |
| поле | `laserLength` | `public float laserLength = 100f;` |  |
| поле | `leftColor` | `public Color leftColor = new Color(1f, 0.1f, 0.1f);` |  |
| поле | `leftHandEnabled` | `public bool leftHandEnabled = true;` |  |
| поле | `leftHandOffset` | `public float leftHandOffset = 0.35f;` |  |
| поле | `lockCursorOnStart` | `public bool lockCursorOnStart = false;` |  |
| поле | `lookSensitivity` | `public float lookSensitivity = 2f;` |  |
| поле | `maxDistance` | `public float maxDistance = 20f;` |  |
| поле | `minDistance` | `public float minDistance = 0.25f;` |  |
| поле | `moveSpeed` | `public float moveSpeed = 5f;` |  |
| поле | `rightColor` | `public Color rightColor = new Color(0.1f, 1f, 0.3f);` |  |
| поле | `rightHandEnabled` | `public bool rightHandEnabled = false;` |  |
| поле | `rightHandOffset` | `public float rightHandOffset = 0.35f;` |  |
| поле | `scrollSmoothSpeed` | `public float scrollSmoothSpeed = 16f;` |  |
| поле | `scrollStep` | `public float scrollStep = 0.08f;` |  |
| поле | `sprintSpeed` | `public float sprintSpeed = 10f;` |  |
| поле | `startupLookAt` | `public Vector3 startupLookAt = new Vector3(12f, -8f, 0f);` |  |
| поле | `startupPosition` | `public Vector3 startupPosition = new Vector3(0f, 2f, -28f);` |  |
| поле | `stickyToSurface` | `public bool stickyToSurface = true;` |  |
| поле | `suppressDirectTeleop` | `public bool suppressDirectTeleop = true;` | Подавить старую прямую телеоперацию кликом (движение — только через поток выбора). |
| поле | `surfaceLayer` | `public LayerMask surfaceLayer = Physics.DefaultRaycastLayers;` |  |
| поле | `surfaceStickTolerance` | `public float surfaceStickTolerance = 0.005f;` |  |
| поле | `verticalSpeed` | `public float verticalSpeed = 4f;` |  |
| метод | `AddScrollInput` | `public void AddScrollInput(float notches)` | Прокрутка колеса «извне» (VR-контроллеры, автотесты), в щелчках: + = вперёд (от оператора). Складывается с мышью в том же кадре; в режиме перемещения точки игнорируется. |
| метод | `AimBallDepth` | `public float AimBallDepth => ballDepth;` | Глубина шарика вдоль луча (от оператора), юниты — «колесо мыши». |
| метод | `AimBallDetached` | `public bool AimBallDetached => ballHasBase && !ballOnSurface;` | Шарик отведён колесом от поверхности (идёт «в воздухе» к оператору или за поверхность). |
| метод | `AimBallHasSurface` | `public bool AimBallHasSurface => ballHasBase;` | У шарика есть опорная поверхность под лучом (реальная геометрия или рабочая плоскость y = 0). |
| метод | `AimBallOnSurface` | `public bool AimBallOnSurface => ballOnSurface;` | Шарик «прилип»: стоит РОВНО на поверхности (нормаль поверхности настоящая). |
| метод | `AimHitPublic` | `public bool AimHitPublic => aimHitSurface;` |  |
| метод | `AimNormalPublic` | `public Vector3 AimNormalPublic => aimNormal;` | Нормаль поверхности в точке прицела (для смещения TCP и выравнивания «пятака»). |
| метод | `AimOnSurfacePublic` | `public bool AimOnSurfacePublic => aimOnRealSurface;` | Прицел попал в РЕАЛЬНУЮ геометрию (RaycastHit): нормаль поверхности настоящая. |
| метод | `AimPointPublic` | `public Vector3 AimPointPublic => aimPoint;` |  |
| метод | `AimPosition` | `public Vector3 AimPosition => aimPoint;` | Точка прицела (шарик лазера) в мире. |
| метод | `ApplyFlashlightSettings` | `public void ApplyFlashlightSettings()` | Применить сериализуемые параметры фонарика к компоненту Light. Вызывается при создании и КАЖДЫЙ раз, когда значение в инспекторе изменилось (`UpdateFlashlightLive`) — поэтому параметры крутятся в PlayMode без перекомпиляции и сразу видны в сцене. Инварианты (защита от «произвольных» чисел в инспе… |
| метод | `FlashlightLight` | `public Light FlashlightLight => flashlight;` | Компонент Light фонарика (диагностика/VR-контур). null, если фонарика нет. |
| метод | `FlashlightLit` | `public bool FlashlightLit => flashlight != null && flashlight.enabled && flashlight.intensity > 0.01f;` | Фонарик реально светит в этом кадре (Light.enabled и ненулевая яркость). |
| метод | `FlashlightOn` | `public bool FlashlightOn => flashlight != null && enableFlashlight && flashlightEnabled;` | Фонарик ВКЛЮЧЁН по состоянию (мастер-флаг + флаг состояния). При `flashlightFadeTime > 0` свет в первые миллисекунды ещё разгорается, но логическое состояние уже «включён» — поэтому проверки «нажатие включает фонарик» смотрят именно сюда. |
| метод | `FocusOn` | `public void FocusOn(Vector3 worldPoint, float distance = 2.4f)` | НАВЕСТИ КАМЕРУ НА ОБЪЕКТ (ЭТАП 6, пункт «Фокус камеры на объекте» контекстного меню дерева). Меняются ТОЛЬКО позиция оператора и его yaw/pitch — те же две переменные, которыми управляет мышь, поэтому после фокуса обзор продолжает работать как обычно. Логика роботов, лазеров, потока и планировщика… |
| метод | `SelectedRobot` | `public RobotController SelectedRobot => selectedRobot;` | Текущий выбранный робот (для потока траекторий). |
| метод | `SetFlashlight` | `public void SetFlashlight(bool on)` | Включить/выключить фонарик программно (VR-контроллер, автотесты, UI). G и это метод — один и тот же путь: состояние пишется в сериализуемое поле и в Light. Повторный вызов с тем же значением ничего не меняет и не пишет в лог (идемпотентно). |
| метод | `SnapAimBallToSurface` | `public bool SnapAimBallToSurface()` | Средняя кнопка мыши: шарик МГНОВЕННО возвращается к ближайшей поверхности по текущему лучу (первое попадание Raycast). Если луч ни во что не попал (и рабочей плоскости под ним нет) — шарик ОСТАЁТСЯ НА МЕСТЕ: решение зафиксировано в PROJECT_CONTEXT (никаких «телепортов» вслепую). |

### `class GamepadInputProvider`

- **Файл:** `Assets\_Project\01_Scripts\Input\GamepadInputProvider.cs` (строка 4)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `playerCamera` | `public Camera playerCamera;` |  |
| метод | `GetGrabDown` | `public override bool GetGrabDown()` |  |
| метод | `GetGrabHeld` | `public override bool GetGrabHeld()` |  |
| метод | `GetMovement` | `public override Vector2 GetMovement()` |  |
| метод | `GetPlayDown` | `public override bool GetPlayDown()` |  |
| метод | `GetPointerDirection` | `public override Vector3 GetPointerDirection()` |  |
| метод | `GetPointerPosition` | `public override Vector3 GetPointerPosition()` |  |
| метод | `GetRecordDown` | `public override bool GetRecordDown()` |  |
| метод | `GetRotation` | `public override Vector2 GetRotation()` |  |
| метод | `GetSelectDown` | `public override bool GetSelectDown()` |  |
| метод | `GetSelectHeld` | `public override bool GetSelectHeld()` |  |
| метод | `GetSwitchRobotDown` | `public override bool GetSwitchRobotDown()` |  |
| метод | `IsAvailable` | `public override bool IsAvailable()` |  |

### `class GhostView`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\GhostView.cs` (строка 10)
- **Назначение:** Визуализация фантомных траекторий (E5 плана): полилинии TCP для каждого кандидата (первый — зелёный, остальные — синие), маркер самого узкого места и «бегунок», проигрывающий выбранный вариант. Лёгкая реализация без копий моделей.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `maxSamplesPerLine` | `public int maxSamplesPerLine = 90;` |  |
| поле | `runnerSpeed` | `public float runnerSpeed = 0.35f; // доля пути в секунду` |  |
| метод | `CandidateCount` | `public int CandidateCount => candidates.Count;` |  |
| метод | `Clear` | `public void Clear()` |  |
| метод | `Show` | `public void Show(List<PlannedTrajectory> list, PoseValidator v)` | Показать кандидатов (первый считается оптимальным). |

### `class HDRPAutoLighting`

- **Файл:** `Assets\_Project\01_Scripts\Core\HDRPAutoLighting.cs` (строка 18)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `ambientBoost` | `public float ambientBoost = 0f;` |  |
| поле | `ambientOcclusionStrength` | `public float ambientOcclusionStrength = 0.1f;` |  |
| поле | `cameraExposureCompensation` | `public float cameraExposureCompensation = 0f;` |  |
| поле | `enableBloom` | `public bool enableBloom = false;` |  |
| поле | `enableKeyLight` | `public bool enableKeyLight = true;` |  |
| поле | `fallbackCenter` | `public Vector3 fallbackCenter = new Vector3(12f, 0f, -8f);` |  |
| поле | `fillColor` | `public Color fillColor = new Color(0.96f, 0.98f, 1f); // холодно-нейтральный белый` |  |
| поле | `fillLightIntensity` | `public float fillLightIntensity = 8000f; // люкс` |  |
| поле | `keyLightColor` | `public Color keyLightColor = new Color(1f, 0.98f, 0.94f); // тёплый белый` |  |
| поле | `lightDistance` | `public float lightDistance = 4f;` |  |
| поле | `lightHeight` | `public float lightHeight = 4f;` |  |
| поле | `lightTargets` | `public Transform[] lightTargets; // роботы/стол; если пусто — найдём роботов сами` |  |
| поле | `mainLightIntensity` | `public float mainLightIntensity = 20000f; // люкс` |  |
| поле | `rimLightIntensity` | `public float rimLightIntensity = 6000f; // люкс` |  |
| метод | `Apply` | `public void Apply()` | Применяет студийный сет и настройки Volume. Идемпотентно. Исправлен пересвет: направленные источники HDRP в люксах складываются, поэтому держим один аккуратный ключ + лёгкий fill (без «выжигающего» rim, и без +0.5 EV компенсации, которая выбеливала белую графику UI). Функция идемпотентна: повторн… |

### `class HierarchyAutoRefresh`

- **Файл:** `Assets\_Project\01_Scripts\Editor\HierarchyAutoRefresh.cs` (строка 21)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `IntervalSeconds` | `public const double IntervalSeconds = 2.0;` | Интервал обновления, секунды (по ТЗ — 2 с). |
| метод | `Enabled` | `public static bool Enabled` | Включено ли автообновление (по умолчанию — да). |

### `class HierarchyPhantomCleaner`

- **Файл:** `Assets\_Project\01_Scripts\Editor\HierarchyPhantomCleaner.cs` (строка 31)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Purge` | `public static int Purge(bool verbose)` | Удалить «протёкшие» и служебные объекты фантомов. Возвращает число удалённых. |
| метод | `ReportRobotInventory` | `public static void ReportRobotInventory()` | Отчёт: сколько роботов реально в открытой сцене (ожидается 2). |

### `enum InputDevice`

- **Файл:** `Assets\_Project\01_Scripts\Input\InputManager.cs` (строка 16)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `ActiveProvider` | `public InputProvider ActiveProvider` |  |
| метод | `GrabDown` | `public bool GrabDown` |  |
| метод | `GrabHeld` | `public bool GrabHeld` |  |
| метод | `Movement` | `public Vector2 Movement` |  |
| метод | `PlayDown` | `public bool PlayDown` |  |
| метод | `PointerDirection` | `public Vector3 PointerDirection` |  |
| метод | `PointerPosition` | `public Vector3 PointerPosition` |  |
| метод | `RecordDown` | `public bool RecordDown` |  |
| метод | `Rotation` | `public Vector2 Rotation` |  |
| метод | `SelectDown` | `public bool SelectDown` |  |
| метод | `SelectHeld` | `public bool SelectHeld` |  |
| метод | `SetInputDevice` | `public void SetInputDevice(InputDevice device)` |  |
| метод | `SwitchRobotDown` | `public bool SwitchRobotDown` |  |

### `class InputDeviceExtensions`

- **Файл:** `Assets\_Project\01_Scripts\Input\InputDeviceType.cs` (строка 19)
- **Назначение:** Конвертация между InputDeviceType и InputManager.InputDevice.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `ToInputDeviceType` | `public static InputDeviceType ToInputDeviceType(InputManager.InputDevice type)` |  |
| метод | `ToInputManagerDevice` | `public static InputManager.InputDevice ToInputManagerDevice(InputDeviceType type)` |  |

### `enum InputDeviceType`

- **Файл:** `Assets\_Project\01_Scripts\Input\InputDeviceType.cs` (строка 7)
- **Назначение:** Отдельный enum для устройства ввода (не зависит от InputManager MonoBehaviour). Используется SettingsData и InputManager.

_Публичных членов нет (или тип объявлен без них)._

### `class InputManager`

- **Файл:** `Assets\_Project\01_Scripts\Input\InputManager.cs` (строка 4)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Instance` | `public static InputManager Instance;` |  |

### `class InputProvider`

- **Файл:** `Assets\_Project\01_Scripts\Input\InputProvider.cs` (строка 3)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `GetGrabDown` | `public abstract bool GetGrabDown();` |  |
| метод | `GetGrabHeld` | `public virtual bool GetGrabHeld() => false;` |  |
| метод | `GetMovement` | `public abstract Vector2 GetMovement();` |  |
| метод | `GetPlayDown` | `public abstract bool GetPlayDown();` |  |
| метод | `GetPointerDirection` | `public abstract Vector3 GetPointerDirection();` |  |
| метод | `GetPointerPosition` | `public abstract Vector3 GetPointerPosition();` |  |
| метод | `GetRecordDown` | `public abstract bool GetRecordDown();` |  |
| метод | `GetRotation` | `public abstract Vector2 GetRotation();` |  |
| метод | `GetSelectDown` | `public abstract bool GetSelectDown();` |  |
| метод | `GetSelectHeld` | `public virtual bool GetSelectHeld() => false;` |  |
| метод | `GetSwitchRobotDown` | `public abstract bool GetSwitchRobotDown();` |  |
| метод | `IsAvailable` | `public abstract bool IsAvailable();` |  |

### `class InverseKinematics`

- **Файл:** `Assets\_Project\01_Scripts\Core\InverseKinematics.cs` (строка 8)
- **Назначение:** Упрощённый CCD-солвер инверсной кинематики для 6-осевого манипулятора. Сначала приводит TCP к целевой позиции, затем доворачивает конечные суставы до целевой ориентации фланца.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `baseTransform` | `public Transform baseTransform;` |  |
| поле | `endEffector` | `public Transform endEffector;` |  |
| поле | `joints` | `public Transform[] joints; // 6 суставов от основания к фланцу` |  |
| поле | `maxIterations` | `public int maxIterations = 10;` |  |
| поле | `orientationThreshold` | `public float orientationThreshold = 2f; // градусы` |  |
| поле | `positionThreshold` | `public float positionThreshold = 0.01f;` |  |
| поле | `target` | `public Transform target; // Куда должна прийти TCP (Tool Center Point)` |  |
| метод | `SetTarget` | `public void SetTarget(Vector3 position)` |  |
| метод | `SetTarget` | `public void SetTarget(Vector3 position, Quaternion rotation)` |  |
| метод | `Solve` | `public void Solve(float deltaTime)` |  |

### `class KeyboardController`

- **Файл:** `Assets\_Project\01_Scripts\Input\KeyboardController.cs` (строка 3)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `moveSpeed` | `public float moveSpeed = 2f;` |  |
| поле | `rotateSpeed` | `public float rotateSpeed = 90f;` |  |

### `class KeyboardMouseInputProvider`

- **Файл:** `Assets\_Project\01_Scripts\Input\KeyboardMouseInputProvider.cs` (строка 4)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `mouseSensitivity` | `public float mouseSensitivity = 1f;` |  |
| поле | `playerCamera` | `public Camera playerCamera;` |  |
| метод | `GetGrabDown` | `public override bool GetGrabDown()` |  |
| метод | `GetGrabHeld` | `public override bool GetGrabHeld()` |  |
| метод | `GetMovement` | `public override Vector2 GetMovement()` |  |
| метод | `GetPlayDown` | `public override bool GetPlayDown()` |  |
| метод | `GetPointerDirection` | `public override Vector3 GetPointerDirection()` |  |
| метод | `GetPointerPosition` | `public override Vector3 GetPointerPosition()` |  |
| метод | `GetRecordDown` | `public override bool GetRecordDown()` |  |
| метод | `GetRotation` | `public override Vector2 GetRotation()` |  |
| метод | `GetSelectDown` | `public override bool GetSelectDown()` |  |
| метод | `GetSelectHeld` | `public override bool GetSelectHeld()` |  |
| метод | `GetSwitchRobotDown` | `public override bool GetSwitchRobotDown()` | Смена робота. Клавиша — только F: TAB занят переключением РЕЖИМА UI (`FreeFlyCameraController.ToggleUiMode`, бинд перенесён с CAPS LOCK), а F — та же клавиша, которой робот выбирается лучом в `FreeFlyCameraController`. Так одна клавиша не может делать два разных действия. |
| метод | `IsAvailable` | `public override bool IsAvailable() => true;` |  |

### `class MainMenu`

- **Файл:** `Assets\_Project\01_Scripts\UI\MainMenu.cs` (строка 7)
- **Назначение:** Главное меню: запуск симуляции, настройки и выход.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `OnExit` | `public void OnExit()` |  |
| метод | `OnOpenSettings` | `public void OnOpenSettings()` |  |
| метод | `OnStartSimulation` | `public void OnStartSimulation()` |  |

### `enum MetricSort`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\TrajectoryMetricsPanel.cs` (строка 37)
- **Назначение:** По какому критерию сортируются строки панели.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `background` | `public Image background;` |  |
| поле | `fontSize` | `public int fontSize = 12;` |  |
| поле | `maxRows` | `public int maxRows = 0;` |  |
| поле | `panelRightOffset` | `public float panelRightOffset = 20f;` |  |
| поле | `panelTopOffset` | `public float panelTopOffset = 178f;` |  |
| поле | `panelWidth` | `public float panelWidth = 700f;` |  |
| поле | `phantomSpeed` | `public float phantomSpeed = 0.2f;` |  |
| поле | `refreshInterval` | `public float refreshInterval = 0.5f;` |  |
| поле | `robotSpeed` | `public float robotSpeed = 1f / 15f;` |  |
| поле | `root` | `public GameObject root;` |  |
| поле | `rowHeight` | `public float rowHeight = 19f;` |  |
| поле | `showPanel` | `public bool showPanel = true;` |  |
| поле | `sortBy` | `public MetricSort sortBy = MetricSort.Score;` |  |
| поле | `sortingOrder` | `public int sortingOrder = 50; // ниже панели лимитов (55) и HUD перемещения точки (60)` |  |
| поле | `swatch` | `public Image swatch;` |  |
| поле | `text` | `public Text text;` |  |
| свойство | `SelectedDisplayIndex` | `public int SelectedDisplayIndex { get; private set; } = -1;` | Индекс выбранной траектории (строка подсвечена) — для проверок. |
| метод | `BestValueColorHex` | `public static string BestValueColorHex => ColorUtility.ToHtmlStringRGB(ColorBest);` | HEX цвета «лучшее значение» (подсветка в строке) — для диагностики/проверок. |
| метод | `Clear` | `public void Clear()` | Убрать все строки (сброс, Esc, движение робота). |
| метод | `CycleSort` | `public void CycleSort()` | Следующий критерий сортировки (для UI-кнопки или внешнего вызова). |
| метод | `PlainValueColorHex` | `public static string PlainValueColorHex => ColorUtility.ToHtmlStringRGB(ColorDim);` | HEX цвета обычного значения — для диагностики/проверок. |
| метод | `RowBackground` | `public Color RowBackground(int i)` | Фон строки i (подсвечен у выбранной траектории) — для проверок. |
| метод | `RowColor` | `public Color RowColor(int i)` | Цвет строки i — тот же, что у «колбаски» траектории (диагностика/проверки). |
| метод | `RowCount` | `public int RowCount => items.Count;` | Сколько строк сейчас в панели (диагностика/проверки). |
| метод | `RowText` | `public string RowText(int i) => i >= 0 && i < rows.Count && rows[i].text != null ? rows[i].text.text : "";` | Текст строки i (диагностика/проверки). |
| метод | `SetSelected` | `public void SetSelected(int selectedIndex)` | Отметить выбранную траекторию (шаг 3/4 ТЗ) — подсветка строки. |
| метод | `SetSort` | `public void SetSort(MetricSort sort)` | Задать критерий сортировки строк. |
| метод | `SetTrajectories` | `public void SetTrajectories(List<TrajectoryCandidate> candidates, int selectedIndex)` | Показать метрики набора траекторий (вызывается при генерации вариантов) и отметить выбранную (индекс в списке `SelectionState.candidates`; -1 — не выбрана). |
| метод | `SetVisible` | `public void SetVisible(bool on)` | Включить/выключить панель (публичный метод — бинды не используются). |
| метод | `SortCriterion` | `public MetricSort SortCriterion => sortBy;` | Текущий критерий сортировки (для проверок). |
| метод | `Tick` | `public void Tick()` | Живое обновление: время выполнения зависит от текущей скорости робота. |
| метод | `ToggleVisible` | `public void ToggleVisible() { SetVisible(!showPanel); }` | Переключить панель. |
| метод | `Visible` | `public bool Visible => showPanel;` | Панель показывается (публичный флаг). |

### `class MotionExecutor`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\MotionExecutor.cs` (строка 9)
- **Назначение:** Исполнитель движения с настраиваемой скоростью (MVP: 1 м за 20 с). Оборачивает TrajectoryExecutor: пересчитывает времена траектории под заданную скорость TCP (м/с) и при необходимости доводит робота до выбранной ветви IK.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `maxSpeedMps` | `public float maxSpeedMps = 0.5f;` |  |
| поле | `speedMps` | `public float speedMps = 0.05f; // 0.05 м/с = 1 метр за 20 секунд` |  |
| метод | `Bind` | `public void Bind(TrajectoryExecutor exec, PoseValidator v, CollisionWorld w, SafetyGate g)` |  |
| метод | `IsRunning` | `public bool IsRunning => executor != null && executor.IsRunning;` |  |
| метод | `Paused` | `public bool Paused => executor != null && executor.Paused;` | Движение на паузе (робот стоит, траектория НЕ завершается) — кнопка UI. |
| метод | `Play` | `public bool Play(PlannedTrajectory plan, double[] branchQ)` | Запустить движение по траектории; branchQ — выбранная конфигурация (ветвь IK). |
| метод | `SetPaused` | `public void SetPaused(bool value)` | Поставить/снять паузу движения (State Machine это не затрагивает). |
| метод | `SetSpeed` | `public void SetSpeed(float mps) => speedMps = Mathf.Clamp(mps, 0.005f, maxSpeedMps);` |  |
| метод | `Stop` | `public void Stop() => executor?.Stop(SafetyReason.OperatorStop);` |  |

### `class MRInputProvider`

- **Файл:** `Assets\_Project\01_Scripts\Input\MRInputProvider.cs` (строка 4)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `GetGrabDown` | `public override bool GetGrabDown() => false;` |  |
| метод | `GetMovement` | `public override Vector2 GetMovement() => Vector2.zero;` |  |
| метод | `GetPlayDown` | `public override bool GetPlayDown() => false;` |  |
| метод | `GetPointerDirection` | `public override Vector3 GetPointerDirection() =>` |  |
| метод | `GetPointerPosition` | `public override Vector3 GetPointerPosition() =>` |  |
| метод | `GetRecordDown` | `public override bool GetRecordDown() => false;` |  |
| метод | `GetRotation` | `public override Vector2 GetRotation() => Vector2.zero;` |  |
| метод | `GetSelectDown` | `public override bool GetSelectDown() => false;` |  |
| метод | `GetSwitchRobotDown` | `public override bool GetSwitchRobotDown() => false;` |  |
| метод | `IsAvailable` | `public override bool IsAvailable() => false;` |  |

### `class Palette`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\TrajectoryFlowController.cs` (строка 2260)
- **Назначение:** Палитра подсказок и шарика (ядовитые неоновые цвета).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Bad` | `public static readonly Color Bad = new Color(1f, 0.05f, 0.45f); // неоновый маджента-красный` |  |
| поле | `Info` | `public static readonly Color Info = new Color(0.35f, 1f, 0.75f); // неоновый циан` |  |
| поле | `Ok` | `public static readonly Color Ok = new Color(0.55f, 1f, 0.05f); // кислотно-зелёный` |  |
| поле | `Warn` | `public static readonly Color Warn = new Color(1f, 0.72f, 0f); // ядовито-оранжевый` |  |

### `class PlacementController`

- **Файл:** `Assets\_Project\01_Scripts\VR\PlacementController.cs` (строка 3)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `anchorManager` | `public SpatialAnchorManager anchorManager;` |  |
| поле | `floorLayer` | `public LayerMask floorLayer;` |  |
| поле | `indicator` | `public GameObject indicator;` |  |

### `class PlanMetrics`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\PlanMetrics.cs` (строка 12)
- **Назначение:** Метрики и автотест (E5 плана): замер латентностей оракула/планировщика, success rate, длина пути, время, минимальный зазор, запас до лимитов, манипулируемость, σ_min, число отклонений Safety. Отчёт — в консоль и в CSV.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `logEveryTarget` | `public bool logEveryTarget = false;` |  |
| поле | `selfTestTargets` | `public int selfTestTargets = 60;` |  |
| метод | `PrintSummary` | `public void PrintSummary(string title = "Итоги планирования")` |  |
| метод | `RecordOracle` | `public void RecordOracle(double ms)` |  |
| метод | `RecordPlan` | `public void RecordPlan(double ms, bool success, PlannedTrajectory best)` |  |
| метод | `SaveCsv` | `public void SaveCsv()` |  |
| метод | `SelfTest` | `public string SelfTest(RobotController robot, PoseValidator validator, CollisionWorld world,` | Автотест: N случайных точек в рабочей зоне вокруг робота → оракул → планировщик. Возвращает строку-сводку (для UI/лога), исполнение не запускается. |

### `class PointMoveHud`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\PointMoveHud.cs` (строка 18)
- **Назначение:** Индикатор РЕЖИМА ПЕРЕМЕЩЕНИЯ ТОЧКИ (ТЗ: этапы 2, 4, 6). Состоит из двух частей: 1) HUD (screen-space overlay, uGUI в стиле KazistovVv): крупная панель с координатами точки ОТНОСИТЕЛЬНО НУЛЯ РОБОТА (его Base) и статусом достижимости — зелёный/жёлтый/красный. Панель стоит сверху по центру (ниже вер…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `markerAlpha` | `public float markerAlpha = 0.85f;` |  |
| поле | `markerPulse` | `public float markerPulse = 0.15f;` |  |
| поле | `markerSize` | `public float markerSize = 0.085f;` |  |
| поле | `panelHeight` | `public float panelHeight = 156f;` |  |
| поле | `panelWidth` | `public float panelWidth = 700f;` |  |
| поле | `sortingOrder` | `public int sortingOrder = 60; // выше канваса KazistovVv (0)` |  |
| поле | `topOffset` | `public float topOffset = 92f;` |  |
| метод | `CoordsText` | `public string CoordsText => coordText != null ? coordText.text : "";` | Текущие координаты в панели (диагностика/проверки). |
| метод | `DeltaText` | `public string DeltaText => deltaText != null ? deltaText.text : "";` | Строка смещения от исходной позиции (диагностика/проверки). |
| метод | `IsMarkerVisible` | `public bool IsMarkerVisible => markerVisible;` | Виден ли маркер точки (точка видна всегда, когда она зафиксирована). |
| метод | `IsVisible` | `public bool IsVisible => visible;` | Виден ли крупный индикатор режима (панель). |
| метод | `SetHint` | `public void SetHint(string text)` | Дополнительная строка вместо подсказки (например, «нет робота»). |
| метод | `SetHudVisible` | `public void SetHudVisible(bool value)` | Крупный индикатор РЕЖИМА (панель). Виден только в `PointMoveMode` (ТЗ: «Индикатор режима … виден только в PointMoveMode»). |
| метод | `SetMarkerVisible` | `public void SetMarkerVisible(bool value)` | Маркер зафиксированной точки. Точка «видна всегда» (ТЗ): маркер остаётся и после выхода из режима, скрывается только при сбросе/завершении сценария. |
| метод | `SetOrigin` | `public void SetOrigin(Vector3 originInRobotFrame)` | Исходная позиция точки (в системе робота) — для строки «Δ от исходной». |
| метод | `SetPoint` | `public void SetPoint(Vector3 worldPoint, Vector3 pointInRobotFrame)` | Позиция точки: маркер ставится в мире, координаты — в системе робота. |
| метод | `SetVerdict` | `public void SetVerdict(ReachVerdict verdict, string reason)` | Статус достижимости (этап 4): цвет полосы и шарика + текст причины. |
| метод | `SetVisible` | `public void SetVisible(bool value)` | Показывать/скрывать индикатор целиком (HUD режима + маркер точки). |
| метод | `Show` | `public void Show(Vector3 worldPoint, Vector3 pointInRobotFrame)` | Показать индикатор: маркер в мировой точке, координаты — в системе робота. |
| метод | `StatusText` | `public string StatusText => statusText != null ? statusText.text : "";` | Строка статуса достижимости (диагностика/проверки). |

### `class PoseSelector`

- **Файл:** `Assets\_Project\01_Scripts\VR\PoseSelector.cs` (строка 3)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `laserPointer` | `public LineRenderer laserPointer;` |  |
| поле | `maxDistance` | `public float maxDistance = 15f;` |  |
| поле | `selectableLayers` | `public LayerMask selectableLayers;` |  |
| поле | `targetMarker` | `public TargetMarker targetMarker;` |  |

### `class Readme`

- **Файл:** `Assets\TutorialInfo\Scripts\Readme.cs` (строка 4)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `icon` | `public Texture2D icon;` |  |
| поле | `loadedLayout` | `public bool loadedLayout;` |  |
| поле | `sections` | `public Section[] sections;` |  |
| поле | `title` | `public string title;` |  |

### `class ReadmeEditor`

- **Файл:** `Assets\TutorialInfo\Scripts\Editor\ReadmeEditor.cs` (строка 11)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `OnInspectorGUI` | `public override void OnInspectorGUI()` |  |

### `class RobotController`

- **Файл:** `Assets\_Project\01_Scripts\Core\RobotController.cs` (строка 8)
- **Назначение:** Абстрактная основа всех контроллеров роботов. Реализует позиционный CCD (Cyclic Coordinate Descent) для суставов, разрешение ссылок на геометрию по именам и базовую телеметрию.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `alternativeEndEffectorName` | `public string alternativeEndEffectorName = "LS10-B702S_z_5";` |  |
| поле | `endEffector` | `public Transform endEffector;` |  |
| поле | `endEffectorName` | `public string endEffectorName = "Axis6_2";` |  |
| поле | `fixedBase` | `public Transform fixedBase;` |  |
| поле | `fixedBaseName` | `public string fixedBaseName = "LS10-B702S_base_1";` |  |
| поле | `ikIterations` | `[Range(1, 32)] public int ikIterations = 12;` |  |
| поле | `ikTolerance` | `[Min(0.001f)] public float ikTolerance = 0.01f;` |  |
| поле | `isActive` | `[HideInInspector] public bool isActive = false;` |  |
| поле | `jointTemperature` | `public float jointTemperature = 25f;` |  |
| поле | `maxSpeed` | `[Range(0.1f, 5f)] public float maxSpeed = 1f;` |  |
| поле | `movementSpeedScale` | `public float movementSpeedScale = 0.34f;` |  |
| поле | `operatingHours` | `public float operatingHours = 0f;` |  |
| поле | `robotName` | `public string robotName = "Robot";` |  |
| поле | `tcp` | `public Transform tcp;` |  |
| поле | `telemetryEnabled` | `public bool telemetryEnabled = true;` |  |
| метод | `ClearTarget` | `public virtual void ClearTarget()` | Сбросить цель: пока нет ПОДТВЕРЖДЁННОЙ позиции — робот не двигается (критично для логики «два лазера»). |
| метод | `GetJointAngles` | `public virtual float[] GetJointAngles()` |  |
| метод | `MoveToTarget` | `public virtual void MoveToTarget(float deltaTime)` |  |
| метод | `SetActive` | `public virtual void SetActive(bool active)` |  |
| метод | `SetTarget` | `public virtual void SetTarget(Vector3 position)` | Задать цель только по позиции (ориентация не меняется). |
| метод | `SetTarget` | `public virtual void SetTarget(Vector3 position, Quaternion rotation)` | Задать цель по позиции и ориентации. |
| метод | `UpdateTelemetry` | `public virtual void UpdateTelemetry(float deltaTime)` |  |

### `class RobotInventory`

- **Файл:** `Assets\_Project\01_Scripts\Core\RobotController.cs` (строка 273)
- **Назначение:** Учёт роботов сцены: в MainScene их ровно два — 6-осевой на стенде 1 и SCARA на стенде 2. Рантайм роботов не создаёт (создание возможно только явной командой оператора «Добавить робота» на верхней панели интерфейса → ObjectSpawner); этот класс только проверяет инвентарь и пишет предупреждение, есл…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Suppress` | `public static bool Suppress;` | Пауза проверки на время создания служебных копий (фантомы). Копия создаётся «тихим» Instantiate: её `Awake` отрабатывает ДО того, как копии выставят HideInHierarchy, поэтому проверка инвентаря видела «лишнего» робота и писала ложное предупреждение «Роботов в сцене: 3» на каждое создание фантомов. |
| метод | `Guard` | `public static void Guard(RobotController self)` | Проверка инвентаря при появлении робота (вызывается из RobotController.Awake). |

### `class RobotSelector`

- **Файл:** `Assets\_Project\01_Scripts\Integration\RobotSelector.cs` (строка 3)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Instance` | `public static RobotSelector Instance;` |  |
| поле | `robots` | `public RobotController[] robots;` |  |
| метод | `GetActiveRobot` | `public RobotController GetActiveRobot()` |  |

### `class RobotSelfCollision`

- **Файл:** `Assets\_Project\01_Scripts\Core\RobotSelfCollision.cs` (строка 19)
- **Назначение:** Добавляет коллайдеры к каждому звену 6-осного манипулятора и отключает столкновения между соседними звеньями (чтобы робот не сталкивался сам с собой, но при этом взаимодействовал с окружающей средой: стол, стены, другие объекты). 6-осный манипулятор приводит к перекрытию соседних звеньев при боль…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `disableSelfCollisions` | `public bool disableSelfCollisions = true;` |  |
| поле | `jointColliderRadius` | `public float jointColliderRadius = 0.08f;` |  |
| поле | `jointTransforms` | `public Transform[] jointTransforms;` |  |
| поле | `robotColliderLayer` | `public int robotColliderLayer = 15; // "Robot" по умолчанию` |  |

### `class SCARAController`

- **Файл:** `Assets\_Project\01_Scripts\Core\SCARAController.cs` (строка 18)
- **Назначение:** Контроллер SCARA-робота LS10-B702S на базе честной DH-кинематики. Кинематическая схема SCARA (DH: две вращательные оси Z1||Z2 — вертикали, затем призматическая ось Z3 по вертикали): * Прямая задача — параметры звеньев (a1,a2) измеряются автоматически из геометрии (горизонтальные проекции отрезков…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `baseTransform` | `public Transform baseTransform;` |  |
| поле | `joint1` | `public Transform joint1; // LS10-B702S_J1_3` |  |
| поле | `joint2` | `public Transform joint2; // LS10-B702S_J2_4` |  |
| поле | `joint3` | `public Transform joint3; // LS10-B702S_z_5 — призматическая ось (вертикальный ход)` |  |
| поле | `ZMax` | `public float ZMax = 0.18f;` |  |
| поле | `ZMin` | `public float ZMin = -0.06f;` |  |
| метод | `GetJointAngles` | `public override float[] GetJointAngles()` |  |
| метод | `MoveToTarget` | `public override void MoveToTarget(float deltaTime)` |  |
| метод | `SetTarget` | `public override void SetTarget(Vector3 position)` |  |
| метод | `SetTarget` | `public override void SetTarget(Vector3 position, Quaternion rotation)` |  |

### `class Section`

- **Файл:** `Assets\TutorialInfo\Scripts\Readme.cs` (строка 12)

_Публичных членов нет (или тип объявлен без них)._

### `class SettingsData`

- **Файл:** `Assets\_Project\01_Scripts\UI\SettingsData.cs` (строка 4)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `adaptiveUI` | `public bool adaptiveUI = true;` |  |
| поле | `aiTrajectoryAssist` | `public bool aiTrajectoryAssist = false;` |  |
| поле | `autoCalibrate` | `public bool autoCalibrate = false;` |  |
| поле | `autoSaveTrajectory` | `public bool autoSaveTrajectory = false;` |  |
| поле | `calibrationProfile` | `public string calibrationProfile = "";` |  |
| поле | `cloudSync` | `public bool cloudSync = false;` |  |
| поле | `collisionDetection` | `public bool collisionDetection = true;` |  |
| поле | `ecoMode` | `public bool ecoMode = false;` |  |
| поле | `enableBiometry` | `public bool enableBiometry = false;` |  |
| поле | `enableEyeTracking` | `public bool enableEyeTracking = false;` |  |
| поле | `enableSwarm` | `public bool enableSwarm = false;` |  |
| поле | `enableVoiceCommands` | `public bool enableVoiceCommands = false;` |  |
| поле | `fatigueThreshold` | `public float fatigueThreshold = 300f;` |  |
| поле | `gamepadSensitivity` | `[Range(0.1f, 5f)] public float gamepadSensitivity = 1f;` |  |
| поле | `hapticFeedback` | `public bool hapticFeedback = true;` |  |
| поле | `Instance` | `public static SettingsData Instance;` |  |
| поле | `invertY` | `public bool invertY = false;` |  |
| поле | `language` | `public string language = "ru";` |  |
| поле | `masterVolume` | `[Range(0f, 1f)] public float masterVolume = 1f;` |  |
| поле | `mouseSensitivity` | `[Range(0.1f, 5f)] public float mouseSensitivity = 1f;` |  |
| поле | `musicVolume` | `[Range(0f, 1f)] public float musicVolume = 0.5f;` |  |
| поле | `passthroughOpacity` | `[Range(0f, 1f)] public float passthroughOpacity = 0.8f;` |  |
| поле | `preferredDevice` | `public InputDeviceType preferredDevice = InputDeviceType.KeyboardMouse;` |  |
| поле | `qualityLevel` | `public int qualityLevel = 2;` |  |
| поле | `robotIP` | `public string robotIP = "192.168.1.100";` |  |
| поле | `robotPort` | `public int robotPort = 5007;` |  |
| поле | `robotSpeed` | `[Range(0.1f, 5f)] public float robotSpeed = 1f;` |  |
| поле | `rosBridgeIP` | `public string rosBridgeIP = "localhost";` |  |
| поле | `rosBridgePort` | `public int rosBridgePort = 9090;` |  |
| поле | `safetyZoneRadius` | `public float safetyZoneRadius = 0.5f;` |  |
| поле | `sfxVolume` | `[Range(0f, 1f)] public float sfxVolume = 1f;` |  |
| поле | `showFPS` | `public bool showFPS = false;` |  |
| поле | `showRealRobotOverlay` | `public bool showRealRobotOverlay = true;` |  |
| поле | `showTrajectoryPreview` | `public bool showTrajectoryPreview = true;` |  |
| поле | `showTutorialOnStart` | `public bool showTutorialOnStart = true;` |  |
| поле | `swarmSize` | `public int swarmSize = 3;` |  |
| поле | `targetFPS` | `[Range(30, 144)] public int targetFPS = 60;` |  |
| поле | `trajectorySavePath` | `public string trajectorySavePath = "";` |  |
| поле | `useHandTracking` | `public bool useHandTracking = true;` |  |
| поле | `useROS` | `public bool useROS = false;` |  |
| поле | `useSpatialAnchors` | `public bool useSpatialAnchors = true;` |  |
| поле | `vrFOV` | `[Range(60, 120)] public int vrFOV = 90;` |  |
| поле | `vSync` | `public bool vSync = true;` |  |

### `class SettingsMenu`

- **Файл:** `Assets\_Project\01_Scripts\UI\SettingsMenu.cs` (строка 7)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `LoadSettings` | `public void LoadSettings()` |  |
| метод | `OnBackButton` | `public void OnBackButton()` |  |
| метод | `ResetToDefaults` | `public void ResetToDefaults()` |  |
| метод | `SaveSettings` | `public void SaveSettings()` |  |
| метод | `ShowTab` | `public void ShowTab(int index)` |  |

### `class SettingsSaver`

- **Файл:** `Assets\_Project\01_Scripts\UI\SettingsSaver.cs` (строка 4)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Delete` | `public static void Delete()` |  |
| метод | `Load` | `public static void Load(SettingsData data)` |  |
| метод | `Save` | `public static void Save(SettingsData data)` |  |

### `class SixAxisAutoSetup`

- **Файл:** `Assets\_Project\01_Scripts\Core\SixAxisAutoSetup.cs` (строка 12)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `applyTestTargetOnStartup` | `public bool applyTestTargetOnStartup = false;` |  |
| поле | `autoSetupOnStart` | `public bool autoSetupOnStart = true;` |  |
| поле | `removeExtraComponents` | `public bool removeExtraComponents = true;` |  |
| метод | `Setup` | `public void Setup()` |  |

### `class SixAxisController`

- **Файл:** `Assets\_Project\01_Scripts\Core\SixAxisController.cs` (строка 20)
- **Назначение:** Контроллер 6-осного манипулятора на базе честной кинематики по Денавиту–Хартенбергу (DH): * Прямая задача — иерархия трансформов (Unity-цепочка) + DHForward (RobotDH); * Обратная задача — ОСЕВОЙ CCD (RobotDH.DHInverse): каждый сустав поворачивается строго вокруг СВОЕЙ оси вращения (jointAxesLocal…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `autoDetectAxesFromLinks` | `public bool autoDetectAxesFromLinks = false;` |  |
| поле | `baseName` | `public string baseName = "LS10-B702S_base_1";` |  |
| поле | `baseTransform` | `public Transform baseTransform;` |  |
| поле | `enableSelfCollisionGuard` | `public bool enableSelfCollisionGuard = true;` |  |
| поле | `fixLegacyMeshParents` | `public bool fixLegacyMeshParents = true;` |  |
| поле | `ik` | `public InverseKinematics ik;` |  |
| поле | `jointAxesLocal` | `public Vector3[] jointAxesLocal = new Vector3[]` |  |
| поле | `jointLimits` | `public Vector2[] jointLimits = new Vector2[6];` |  |
| поле | `jointTransforms` | `public Transform[] jointTransforms;` |  |
| поле | `linkRadius` | `public float linkRadius = 0.07f;` |  |
| поле | `positionSmoothing` | `[Range(0f, 0.99f)] public float positionSmoothing = 0.85f;` |  |
| поле | `rotationSmoothing` | `[Range(0f, 0.99f)] public float rotationSmoothing = 0.9f;` |  |
| поле | `selectiveRollback` | `public bool selectiveRollback = true;` |  |
| поле | `selfCollisionStopTime` | `public float selfCollisionStopTime = 0.25f;` |  |
| свойство | `ReachedTarget` | `public bool ReachedTarget { get { return _lastReached; } }` |  |
| свойство | `SelfCollisionBlocked` | `public bool SelfCollisionBlocked { get; private set; }` |  |
| метод | `ClearTarget` | `public override void ClearTarget()` | Сброс цели: робот стоит, пока позиция не подтверждена (логика двух лазеров). |
| метод | `ClosestDistanceBetweenSegments` | `public static float ClosestDistanceBetweenSegments(` | Минимальное расстояние между двумя отрезками (3D). |
| метод | `GetJointAngles` | `public override float[] GetJointAngles()` |  |
| метод | `HasPendingSeed` | `public bool HasPendingSeed => seedPending;` | True, если задан непотреблённый сид (для диагностики). |
| метод | `MoveToTarget` | `public override void MoveToTarget(float deltaTime)` |  |
| метод | `SetPreferredSeed` | `public void SetPreferredSeed(double[] q)` | Задать предпочтительную конфигурацию (ветвь IK) для следующей цели. |
| метод | `SetTarget` | `public override void SetTarget(Vector3 position)` |  |
| метод | `SetTarget` | `public override void SetTarget(Vector3 position, Quaternion rotation)` |  |

### `class SpatialAnchorManager`

- **Файл:** `Assets\_Project\01_Scripts\Spatial\SpatialAnchorManager.cs` (строка 8)
- **Назначение:** Управляет якорем размещения робота: сохраняет и восстанавливает и позицию, и поворот (PlayerPrefs), чтобы после перезапуска виртуальный робот совпадал с реальным.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Instance` | `public static SpatialAnchorManager Instance;` |  |
| поле | `placementIndicator` | `public GameObject placementIndicator;` |  |
| поле | `robotRoot` | `public Transform robotRoot;` |  |
| метод | `PlaceRobot` | `public void PlaceRobot(Vector3 position, Quaternion rotation)` |  |
| метод | `Recalibrate` | `public void Recalibrate()` |  |

### `class StandsMenu`

- **Файл:** `Assets\_Project\01_Scripts\Editor\StandsMenu.cs` (строка 15)
- **Назначение:** Инструменты сборки стендов (столы + роботы) в MainScene. ВАЖНО: пункты меню Tools/KazistovVv отсюда УДАЛЕНЫ — столы и роботы теперь стоят в MainScene как обычные объекты сцены (см. Assets/_Project/00_Scenes/ MainScene.unity) и не должны создаваться ни кнопкой, ни в рантайме. Класс оставлен как ут…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `PlaceStands` | `public static void PlaceStands()` | Разместить два стенда, не удаляя существующие объекты. |
| метод | `RebuildScene` | `public static void RebuildScene()` | Пересобрать сцену: удалить старые столы/роботов и создать два стенда. |

### `class TargetMarker`

- **Файл:** `Assets\_Project\01_Scripts\Integration\TargetMarker.cs` (строка 3)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `baseScale` | `public float baseScale = 0.05f;` |  |
| поле | `invalidColor` | `public Color invalidColor = Color.red;` |  |
| поле | `pulseSpeed` | `public float pulseSpeed = 3f;` |  |
| поле | `validColor` | `public Color validColor = Color.green;` |  |
| метод | `SetValid` | `public void SetValid(bool valid)` |  |

### `enum ToolOffsetMode`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\TrajectoryFlowController.cs` (строка 150)
- **Назначение:** Как смещается выбранная точка: вдоль нормали поверхности (основное) или вверх по Y (хак).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `detour` | `public bool detour; // строить ОБХОД через промежуточную позу (реально другой маршрут)` |  |
| поле | `goalQ` | `public double[] goalQ;` |  |
| поле | `hasGoal` | `public bool hasGoal;` |  |
| поле | `looseGoal` | `public bool looseGoal; // цель прошла лимиты, но БЕЗ запаса 3° (краевая точка SCARA)` |  |
| поле | `seed` | `public int seed;` |  |
| поле | `tag` | `public string tag;` |  |
| поле | `variant` | `public int variant; // номер варианта обхода (глубина/направление — Planner.PlanViaWaypoint)` |  |
| свойство | `DuplicatesFiltered` | `public int DuplicatesFiltered { get { return duplicatesFiltered; } }` | Сколько вариантов отброшено как дубликаты (этап 14). |
| свойство | `ExternalMotionRunning` | `public bool ExternalMotionRunning { get { return externalMotion; } }` | Идёт движение, запущенное снаружи потока (поза/запись/сценарий). |
| свойство | `Generating` | `public bool Generating { get { return generating; } }` | Идёт генерация вариантов для текущей точки. |
| свойство | `IkBranchesFound` | `public int IkBranchesFound { get { return ikBranchesFound; } }` | Сколько конфигураций IK нашёл решатель (этап 14). |
| свойство | `IkBranchesUsable` | `public int IkBranchesUsable { get { return ikBranchesUsable; } }` | Сколько из них прошли лимиты и годны как цель (этап 14). |
| свойство | `PaddedVariantCount` | `public int PaddedVariantCount { get { return paddedVariants; } }` | Сколько вариантов добито ПОХОЖИМИ по форме путями (последнее средство, когда бюджет поиска уникальных исчерпан, а отказы RRT не дали добрать честно). 0 — все уникальны. |
| свойство | `PlanAttempts` | `public int PlanAttempts { get { return planAttempts; } }` | Сколько попыток планирования сделано для текущей точки (этап 14). |
| свойство | `PlanFailures` | `public int PlanFailures { get { return planFailures; } }` | Сколько попыток не дали пути (этап 14). |
| свойство | `PlannedCount` | `public int PlannedCount { get { return plannedSoFar.Count; } }` | Сколько вариантов уже готово. |
| свойство | `Planner` | `public Planner Planner { get { return planner; } }` | Штатный планировщик (ТОЛЬКО ЧТЕНИЕ) — ЭТАП 5 ТЗ: waypoint-редактор использует ЕГО ЖЕ (`PlanToGoal`/`PlanViaWaypoint`/`Plan` — тот же BiRRT с теми же зазорами, лимитами и самозазором), а не отдельный планировщик. Ни один метод и ни одно поле `Planner` не менялись — добавлено только читающее свойство. |
| свойство | `PlannerBranchInfo` | `public string PlannerBranchInfo { get { return planner.LastBranchInfo; } }` | Строка о ветвях IK последнего запуска (этап 14). |
| свойство | `PlannerDebug` | `public string PlannerDebug { get { return planner.LastDebug; } }` | Отладочная строка планировщика последнего запуска (этап 14). |
| свойство | `PlannerIterations` | `public int PlannerIterations { get { return planner.LastIterations; } }` | Итераций последнего запуска планировщика BiRRT (этап 14). |
| свойство | `PlanQueueLength` | `public int PlanQueueLength { get { return planQueue.Count; } }` | Сколько запросов планирования ещё в очереди. |
| свойство | `PlanRequestCount` | `public int PlanRequestCount { get { return planRequests.Count; } }` | Сколько запросов планирования построено для точки. |
| свойство | `PlanStartTime` | `public float PlanStartTime { get { return planStartTime; } }` | Время старта генерации (Time.realtimeSinceStartup). |
| свойство | `PlanTargetCount` | `public int PlanTargetCount { get { return trajectoryCount; } }` | Сколько вариантов запрошено (trajectoryCount). |
| свойство | `UniqueVariantCount` | `public int UniqueVariantCount { get { return Mathf.Max(0, plannedSoFar.Count - paddedVariants); } }` | Сколько из найденных вариантов УНИКАЛЬНЫ (форма пути отличается — `IsSamePath`). Это и есть «N из 8» из честного отчёта оператору (ТЗ сессии 15.09.2026). |
| метод | `ConfirmSelectedTrajectory` | `public bool ConfirmSelectedTrajectory()` | ПОДТВЕРДИТЬ ВЫБРАННУЮ ТРАЕКТОРИЮ БЕЗ ЛАЗЕРА — пуск робота «как кнопкой ЛКМ по фантому». Добавлено для интерфейсов этапов 13–36 (кнопка «ПУСК с проверкой», голосовая команда «пуск», мобильный пульт, макросы и деревья поведения): им нужен пуск без наведения зелёного лазера. Метод делает РОВНО ТО ЖЕ… |
| метод | `EndExternalMotion` | `public void EndExternalMotion()` | Завершить внешнее движение (вызывает владелец — хаб новых функций). |
| метод | `Executor` | `public TrajectoryExecutor Executor => executor;` | Низкоуровневый исполнитель траектории (пауза/статус). |
| метод | `IsPointMoveMode` | `public bool IsPointMoveMode => state.phase == FlowState.PointMoveMode;` | Активен ли режим перемещения точки (камера по этому флагу отдаёт QWEASD точке). |
| метод | `Lasers` | `public LaserManager Lasers => lasers;` | Менеджер лазеров (красная/зелёная указка). |
| метод | `LockPointFromUi` | `public bool LockPointFromUi(Vector3 target, Vector3 surfacePoint, Vector3 surfaceNormal, bool onSurface)` | Зафиксировать точку из интерфейса (Undo/Redo, повтор точки): ровно та же операция, что делает красный луч с ЛКМ — включая запуск расчёта вариантов. |
| метод | `Metrics` | `public TrajectoryMetricsPanel Metrics => metricsPanel;` | Панель метрик траекторий (ЗАДАЧА 5). |
| метод | `Motion` | `public MotionExecutor Motion => motion;` | Исполнитель движения с настраиваемой скоростью (шаг 5). |
| метод | `MotionGoalQ` | `public double[] MotionGoalQ` | Поза цели, к которой едет робот на этапе 4 (null — не едет). |
| метод | `Phantoms` | `public PhantomManager Phantoms => phantoms;` | Менеджер фантомов (диагностика/визуализация в дереве). |
| метод | `PlayExternalPlan` | `public bool PlayExternalPlan(PlannedTrajectory plan, double[] goalQ, string why)` | Запустить ВНЕШНИЙ план штатным исполнителем проекта (тот же `MotionExecutor` + `TrajectoryExecutor` + `SafetyGate`, что и на этапе 4): так «перейти в позу», «проиграть запись» и шаги сценариев едут ровно теми же механизмами, что и обычная траектория. Исполнитель создаётся лениво — как в `SelectPh… |
| метод | `PointHud` | `public PointMoveHud PointHud => pointHud;` | HUD режима перемещения точки. |
| метод | `ResetFlow` | `public void ResetFlow(string why = "Сброшено (кнопка интерфейса)")` | Сброс сценария из интерфейса (та же операция, что Esc): точка, траектории, фантомы и движение очищаются, состояние возвращается в `Idle`. |
| метод | `Robot` | `public RobotController Robot => robot;` | Робот, к которому привязан поток (может быть null до первого прицела). |
| метод | `SelectCandidateByIndex` | `public bool SelectCandidateByIndex(int index)` | Выбрать вариант траектории по индексу ТАК ЖЕ, как это делает зелёный луч с ЛКМ (`SelectTrajectory`): появляется фантом, состояние переходит в `PhantomsMoving`. Нужно интерфейсу сравнения траекторий и Undo/Redo — логика выбора не дублируется. |
| метод | `SetJointLimitsVisible` | `public void SetJointLimitsVisible(bool on) { viz?.SetJointLimitsVisible(on); }` | Включить/выключить индикаторы лимитов суставов (ЗАДАЧА 4). |
| метод | `SetMetricsPanelVisible` | `public void SetMetricsPanelVisible(bool on) { metricsPanel?.SetVisible(on); }` | Включить/выключить панель метрик траекторий (ЗАДАЧА 5). |
| метод | `SetWorkspaceVisible` | `public void SetWorkspaceVisible(bool on) { viz?.SetWorkspaceVisible(on); }` | Включить/выключить зону достижимости (ЗАДАЧА 3) — биндов не требует. |
| метод | `State` | `public SelectionState State => state;` |  |
| метод | `StopExternalMotion` | `public void StopExternalMotion(string why = "остановлено оператором")` | Остановить внешнее движение штатным стопом исполнителя. |
| метод | `ToggleJointLimits` | `public void ToggleJointLimits() { viz?.ToggleJointLimits(); }` | Переключить индикаторы лимитов суставов. |
| метод | `ToggleMetricsPanel` | `public void ToggleMetricsPanel() { metricsPanel?.ToggleVisible(); }` | Переключить панель метрик траекторий. |
| метод | `ToggleWorkspace` | `public void ToggleWorkspace() { viz?.ToggleWorkspace(); }` | Переключить зону достижимости. |
| метод | `TryGetPointMoveTarget` | `public bool TryGetPointMoveTarget(out Vector3 point, out Color color)` | Цель лазеров в режиме перемещения: сама точка и цвет по вердикту оракула (ТЗ: в режиме лазеры — только визуальная индикация перемещаемой точки и её цвета). |
| метод | `UpdateAim` | `public void UpdateAim(Vector3 aimPoint, bool aimHit, bool confirmRed, bool confirmGreen,` | Совместимость: старое разделение «красная/зелёная» кнопка → одна ЛКМ. |
| метод | `UpdateAim` | `public void UpdateAim(Vector3 aimPoint, bool aimHit, bool confirm, bool cancel,` | Кадровый вход без нормали поверхности (VR/старый поток): поведение прежнее — точка берётся как есть, без смещения и без выравнивания «пятака». |
| метод | `UpdateAim` | `public void UpdateAim(Vector3 aimPoint, bool aimHit, bool confirm, bool cancel,` | Полный кадровый вход (добавлен режим перемещения точки): moveToggle — Enter (вход в режим из PointSelected/TrajectoriesShown и выход из него), moveAxis — МИРОВОЕ направление движения точки по QWEASD (считает контроллер камеры), moveFast — Shift (ускорение перемещения точки). |
| метод | `UpdateAim` | `public void UpdateAim(Vector3 aimPoint, bool aimHit, bool confirm, bool cancel,` | Кадровый вход: прицел, ЛКМ (одна кнопка действия), отмена, состояние лазеров, нормаль поверхности под прицелом (hit.normal) и признак «попали в реальную геометрию». redLaserOn — красный (Z), greenLaserOn — зелёный (X). |
| метод | `Validator` | `public PoseValidator Validator => validator;` | Валидатор позы привязанного робота (ТОЛЬКО чтение: лимиты/оси/TCP для панели свойств). |
| метод | `Workspace` | `public WorkspaceVisualizer Workspace => viz;` | Зона достижимости и индикаторы лимитов суставов (ЗАДАЧИ 3–4) — публичный доступ для UI/VR-контура: SetWorkspaceVisible/ToggleWorkspace/SetJointLimitsVisible/ToggleJointLimits. |

### `class TrajectoryDataWrapper`

- **Файл:** `Assets\_Project\01_Scripts\Recording\TrajectoryRecorder.cs` (строка 91)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `points` | `public TrajectoryPoint[] points;` |  |

### `class TrajectoryExecutor`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\TrajectoryExecutor.cs` (строка 8)
- **Назначение:** Исполнитель траекторий (E4 плана): проигрывает q(t) по времени, каждый шаг проверяется SafetyGate (лимиты/зазор/watchdog). При отказе — немедленный стоп.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `timeScale` | `public float timeScale = 1f; // можно замедлить (0.5 = вдвое медленнее)` |  |
| свойство | `ActivePlan` | `public PlannedTrajectory ActivePlan { get { return active; } }` | Активная траектория (null — ничего не играет). |
| свойство | `ElapsedSeconds` | `public float ElapsedSeconds { get { return (float)playhead; } }` | Пройденное время активной траектории, с. |
| свойство | `Paused` | `public bool Paused { get; private set; }` | Пауза проигрывания: робот стоит, траектория НЕ считается завершённой (`IsRunning` остаётся true, поэтому State Machine не переключает состояние). Ручка интерфейса (кнопка «Запуск/пауза» на верхней панели). |
| свойство | `StatusText` | `public string StatusText { get; private set; } = "";` |  |
| метод | `Init` | `public void Init(PoseValidator v, CollisionWorld w, SafetyGate g)` |  |
| метод | `IsRunning` | `public bool IsRunning => running;` |  |
| метод | `Play` | `public bool Play(PlannedTrajectory t)` |  |
| метод | `Progress01` | `public float Progress01` | Прогресс 0…1 (0 — ничего не играет). |
| метод | `RemainingSeconds` | `public float RemainingSeconds` | Осталось секунд (с учётом паузы — время не идёт, остаток не растёт). |
| метод | `SetPaused` | `public void SetPaused(bool value)` | Поставить/снять паузу. |
| метод | `Stop` | `public void Stop(SafetyReason reason = SafetyReason.OperatorStop)` |  |
| метод | `TotalSeconds` | `public float TotalSeconds` | Полное время активной траектории, с. |

### `class TrajectoryFlowController`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\TrajectoryFlowController.cs` (строка 40)
- **Назначение:** Полный сценарий «два лазера». Единственная кнопка действия — ЛКМ; её смысл определяется тем, какой лазер включён (Z — красный, X — зелёный). НОВЫЙ АЛГОРИТМ ПОДТВЕРЖДЕНИЯ ТРАЕКТОРИИ (ТЗ): Шаг 1. Красный лазер наводится на поверхность, ЛКМ — точка фиксируется. Траектории сразу НЕ показываются: идёт…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `alignAngleToleranceDeg` | `public float alignAngleToleranceDeg = 6f;` |  |
| поле | `alignToolToSurface` | `public bool alignToolToSurface = false;` |  |
| поле | `candidatesPerSlice` | `public int candidatesPerSlice = 1;` |  |
| поле | `detourVariants` | `public int detourVariants = 12;` |  |
| поле | `distinctAttemptCap` | `public int distinctAttemptCap = 320;` |  |
| поле | `distinctBudgetSeconds` | `public float distinctBudgetSeconds = 30f;` |  |
| поле | `logOffsetDiagnostics` | `public bool logOffsetDiagnostics = true;` |  |
| поле | `logTcpFrame` | `public bool logTcpFrame = true;` |  |
| поле | `MaxTrajectories` | `public const int MaxTrajectories = 8;` | Сколько траекторий генерируется и показывается для каждой точки (ТЗ: ровно 8). |
| поле | `offsetMode` | `public ToolOffsetMode offsetMode = ToolOffsetMode.SurfaceNormal;` |  |
| поле | `phantomCount` | `[Range(1, MaxTrajectories)] public int phantomCount = MaxTrajectories;` |  |
| поле | `phantomSpeedMultiplier` | `[SerializeField] public float phantomSpeedMultiplier = 3f;` |  |
| поле | `planningSliceMs` | `public float planningSliceMs = 12f;` |  |
| поле | `pointMoveFastMultiplier` | `public float pointMoveFastMultiplier = 3f;` |  |
| поле | `pointMoveHudEnabled` | `public bool pointMoveHudEnabled = true;` |  |
| поле | `pointMoveOracleInterval` | `public float pointMoveOracleInterval = 0.08f;` |  |
| поле | `pointMoveSpeed` | `public float pointMoveSpeed = 0.5f;` |  |
| поле | `robotMoveSpeed` | `[SerializeField] public float robotMoveSpeed = 1f / 15f;` |  |
| поле | `slowMotionEnabled` | `public bool slowMotionEnabled = true;` |  |
| поле | `toolOffset` | `public float toolOffset = 0f;` |  |
| поле | `trajectoryCount` | `[Range(1, MaxTrajectories)] public int trajectoryCount = MaxTrajectories;` |  |
| поле | `tubeRadius` | `public float tubeRadius = 0.05f;` |  |
| поле | `tubeSpreadStep` | `public float tubeSpreadStep = 0.035f;` |  |
| метод | `phantomMoveSpeed` | `public float phantomMoveSpeed` | СКОРОСТЬ ФАНТОМОВ, юнитов/с — ВЫЧИСЛЯЕТСЯ, а не настраивается: robotMoveSpeed × phantomSpeedMultiplier (ТЗ: «если робот движется со скоростью X, фантомы — 3X»). Свойство оставлено для совместимости (диагностика/UI читают его как раньше); ПРИСВАИВАНИЕ пересчитывает множитель (value / robotMoveSpee… |

### `class TrajectoryMetricsPanel`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\TrajectoryMetricsPanel.cs` (строка 34)
- **Назначение:** ПАНЕЛЬ МЕТРИК ТРАЕКТОРИЙ (ТЗ сессии 14.09.2026, ЗАДАЧА 5) — временное решение до полноценного UI: простая, но читаемая панель, по которой оператор ОСОЗНАННО выбирает из 8 вариантов. Для каждой сгенерированной траектории показывается строка ЕЁ цветом (тем же, что и «колбаска» в сцене) и метрики: *…

_Публичных членов нет (или тип объявлен без них)._

### `class TrajectoryPlannerController`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\TrajectoryPlannerController.cs` (строка 16)
- **Назначение:** Оркестратор планирования (E2–E5 плана) на камере оператора: P — построить фантомные траектории к точке прицела активного робота; 1 / 2 / 3 — исполнить выбранный вариант (через SafetyGate); Esc — отменить (очистить фантомы, остановить исполнение); F9 — автотест: N случайных целей, сводка метрик в …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `clearance` | `public float clearance = 0.02f;` |  |
| поле | `linkRadius` | `public float linkRadius = 0.06f;` |  |
| поле | `logEachTarget` | `public bool logEachTarget = false;` |  |
| поле | `maxCandidates` | `public int maxCandidates = 3;` |  |
| поле | `selfTestTargets` | `public int selfTestTargets = 20;` |  |
| метод | `HasCandidates` | `public bool HasCandidates => candidates.Count > 0;` |  |
| метод | `UpdateAim` | `public void UpdateAim(Vector3 point, bool hitSurface)` | Точка прицела от камеры (каждый кадр). |

### `class TrajectoryPlayer`

- **Файл:** `Assets\_Project\01_Scripts\Recording\TrajectoryPlayer.cs` (строка 5)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `loop` | `public bool loop = false;` |  |
| поле | `recorder` | `public TrajectoryRecorder recorder;` |  |
| поле | `robot` | `public RobotController robot;` |  |
| поле | `timeScale` | `public float timeScale = 1f;` |  |
| метод | `IsPlaying` | `public bool IsPlaying => isPlaying;` |  |
| метод | `Play` | `public void Play()` |  |
| метод | `Stop` | `public void Stop()` |  |

### `class TrajectoryPoint`

- **Файл:** `Assets\_Project\01_Scripts\Recording\TrajectoryRecorder.cs` (строка 6)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `jointAngles` | `public float[] jointAngles;` |  |
| поле | `position` | `public Vector3 position;` |  |
| поле | `rotation` | `public Quaternion rotation;` |  |
| поле | `time` | `public float time;` |  |

### `class TrajectoryRecorder`

- **Файл:** `Assets\_Project\01_Scripts\Recording\TrajectoryRecorder.cs` (строка 14)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `robot` | `public RobotController robot;` |  |
| метод | `GetTrajectory` | `public List<TrajectoryPoint> GetTrajectory() => trajectory;` |  |
| метод | `StartRecording` | `public void StartRecording()` |  |
| метод | `StopRecording` | `public void StopRecording()` |  |

### `class VRHandTracker`

- **Файл:** `Assets\_Project\01_Scripts\VR\VRHandTracker.cs` (строка 3)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `handProxy` | `public Transform handProxy;` |  |
| поле | `laserLine` | `public LineRenderer laserLine;` |  |

### `class VRInputManager`

- **Файл:** `Assets\_Project\01_Scripts\VR\VRInputManager.cs` (строка 3)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `useFoveatedRendering` | `public bool useFoveatedRendering = true;` |  |

### `class VRInputProvider`

- **Файл:** `Assets\_Project\01_Scripts\Input\VRInputProvider.cs` (строка 4)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `GetGrabDown` | `public override bool GetGrabDown() => false;` |  |
| метод | `GetMovement` | `public override Vector2 GetMovement() => Vector2.zero;` |  |
| метод | `GetPlayDown` | `public override bool GetPlayDown() => false;` |  |
| метод | `GetPointerDirection` | `public override Vector3 GetPointerDirection() =>` |  |
| метод | `GetPointerPosition` | `public override Vector3 GetPointerPosition() =>` |  |
| метод | `GetRecordDown` | `public override bool GetRecordDown() => false;` |  |
| метод | `GetRotation` | `public override Vector2 GetRotation() => Vector2.zero;` |  |
| метод | `GetSelectDown` | `public override bool GetSelectDown() => false;` |  |
| метод | `GetSwitchRobotDown` | `public override bool GetSwitchRobotDown() => false;` |  |
| метод | `IsAvailable` | `public override bool IsAvailable() => false;` |  |

### `class WorkspaceVisualizer`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\WorkspaceVisualizer.cs` (строка 40)
- **Назначение:** Визуализация ВОЗМОЖНОСТЕЙ робота (ТЗ сессии 14.09.2026, ЗАДАЧИ 3 и 4). ЗАДАЧА 3 — ЗОНА ДОСТИЖИМОСТИ (рабочее пространство), «куда робот может дотянуться»: * РОБОТ — полупрозрачный КУПОЛ (сферическая оболочка) вокруг оси 1: радиус = сумма длин звеньев (аналитическая оценка по пивотам суставов, БЕЗ…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `center` | `public Vector3 center; // центр купола (ось 1) / база SCARA` |  |
| поле | `criticalMarginDeg` | `public float criticalMarginDeg = 3f;` |  |
| поле | `criticalMarginFraction` | `[Range(0f, 0.5f)] public float criticalMarginFraction = 0.015f;` |  |
| поле | `edge` | `public LineRenderer edge;` |  |
| поле | `edgeMat` | `public Material edgeMat;` |  |
| поле | `go` | `public GameObject go;` |  |
| поле | `innerRadius` | `public float innerRadius; // внутренний радиус (робот — «мёртвая зона», SCARA — \|L1−L2\|)` |  |
| поле | `jointRingAlpha` | `[Range(0f, 1f)] public float jointRingAlpha = 0.30f;` |  |
| поле | `jointRingRadius` | `public float jointRingRadius = 0.11f;` |  |
| поле | `lineMats` | `public readonly List<Material> lineMats = new List<Material>();` |  |
| поле | `mat` | `public Material mat;` |  |
| поле | `oracleZoneRadius` | `public float oracleZoneRadius = 1.6f;` |  |
| поле | `panelLeftOffset` | `public float panelLeftOffset = 20f;` |  |
| поле | `panelSortingOrder` | `public int panelSortingOrder = 55; // ниже HUD режима перемещения точки (60)` |  |
| поле | `panelTopOffset` | `public float panelTopOffset = 180f;` |  |
| поле | `panelWidth` | `public float panelWidth = 352f;` |  |
| поле | `planeY` | `public float planeY; // плоскость опоры (верх стола внизу робота)` |  |
| поле | `radius` | `public float radius = 1f; // внешний радиус (робот — вылет, SCARA — L1+L2)` |  |
| поле | `refreshInterval` | `public float refreshInterval = 0.1f;` |  |
| поле | `renderer` | `public Renderer renderer;` |  |
| поле | `robot` | `public RobotController robot;` |  |
| поле | `root` | `public GameObject root;` |  |
| поле | `scara` | `public bool scara;` |  |
| поле | `showJointLimits` | `public bool showJointLimits = true;` |  |
| поле | `showJointPanel` | `public bool showJointPanel = true;` |  |
| поле | `showOracleZone` | `public bool showOracleZone = true;` |  |
| поле | `showWorkspace` | `public bool showWorkspace = true;` |  |
| поле | `signature` | `public string signature = "";` |  |
| поле | `surfaceMat` | `public Material surfaceMat;` |  |
| поле | `up` | `public Vector3 up = Vector3.up;` |  |
| поле | `validator` | `public readonly PoseValidator validator = new PoseValidator();` |  |
| поле | `warningMarginDeg` | `public float warningMarginDeg = 15f;` |  |
| поле | `warningMarginFraction` | `[Range(0f, 0.5f)] public float warningMarginFraction = 0.08f;` |  |
| поле | `workspaceAllRobots` | `public bool workspaceAllRobots = true;` |  |
| поле | `workspaceAlpha` | `[Range(0f, 0.5f)] public float workspaceAlpha = 0.09f;` |  |
| поле | `workspaceColor` | `public Color workspaceColor = new Color(0.30f, 0.55f, 0.98f, 1f);` |  |
| поле | `workspaceEdgeAlpha` | `[Range(0f, 1f)] public float workspaceEdgeAlpha = 0.55f;` |  |
| поле | `workspaceGridSteps` | `[Range(1, 8)] public int workspaceGridSteps = 4;` |  |
| поле | `workspaceLineWidth` | `public float workspaceLineWidth = 0.008f;` |  |
| поле | `workspaceSegments` | `[Range(16, 128)] public int workspaceSegments = 64;` |  |
| поле | `workspaceSurface` | `public bool workspaceSurface = true;` |  |
| метод | `Bind` | `public void Bind(RobotController robot)` | Активный робот потока: по нему строятся индикаторы лимитов (зоны — по обоим). |
| метод | `JointLimitsVisible` | `public bool JointLimitsVisible => showJointLimits;` | Индикаторы лимитов показываются (публичный флаг). |
| метод | `JointPanelColor` | `public Color JointPanelColor(int i)` | Цвет индикатора сустава i — вердикт «норма / близко / предел» (диагностика). |
| метод | `JointPanelLine` | `public string JointPanelLine(int i)` | Текст строки панели лимитов для сустава i (диагностика/проверки). |
| метод | `JointRingCount` | `public int JointRingCount => rings.Count;` | Сколько колец-индикаторов суставов построено — для диагностики. |
| метод | `RebuildJointLimits` | `public void RebuildJointLimits()` | Пересобрать индикаторы лимитов (после смены активного робота). |
| метод | `RebuildWorkspace` | `public void RebuildWorkspace()` | Пересобрать зону достижимости вручную — например, после смены конфигурации робота (переехал, заменён, изменились лимиты/звенья). Автоматически вызывается и сама, когда смена конфигурации замечена (см. ConfigurationChanged). |
| метод | `SetJointLimitsVisible` | `public void SetJointLimitsVisible(bool on)` | Включить/выключить индикаторы лимитов суставов (кольца + панель). |
| метод | `SetWorkspaceVisible` | `public void SetWorkspaceVisible(bool on)` | Включить/выключить зону достижимости (публичный метод — бинды не используются). |
| метод | `Tick` | `public void Tick()` | Кадровое обслуживание (зовёт поток этапов): раз в refreshInterval обновляет индикаторы лимитов и панель, раз в 0.5 с проверяет смену конфигурации робота (тогда зона пересобирается). Работа стоит доли миллисекунды — только цвета материалов и текст панели. |
| метод | `ToggleJointLimits` | `public void ToggleJointLimits() { SetJointLimitsVisible(!showJointLimits); }` | Переключить индикаторы лимитов суставов. |
| метод | `ToggleWorkspace` | `public void ToggleWorkspace() { SetWorkspaceVisible(!showWorkspace); }` | Переключить зону достижимости. |
| метод | `WorkspaceVisible` | `public bool WorkspaceVisible => showWorkspace;` | Зона достижимости показывается (публичный флаг). |
| метод | `ZoneCount` | `public int ZoneCount => zones.Count;` | Сколько зон построено (робот + SCARA) — для диагностики/проверок. |
| метод | `ZoneInnerRadius` | `public float ZoneInnerRadius(int index) => index >= 0 && index < zones.Count ? zones[index].innerRadius : 0f;` | Внутренний радиус k-й зоны (мёртвая зона / кольцо SCARA). |
| метод | `ZoneRadius` | `public float ZoneRadius(int index) => index >= 0 && index < zones.Count ? zones[index].radius : 0f;` | Внешний радиус k-й зоны (юниты) — для диагностики. |
| метод | `ZoneRobotName` | `public string ZoneRobotName(int index)` | Имя робота k-й зоны. |

## Пространство имён `KazistovVvKinematics`

### `class DHForward`

- **Файл:** `Assets\_Project\01_Scripts\Core\RobotDH.cs` (строка 43)
- **Назначение:** Прямая кинематика.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Position` | `public static Vector3 Position(IList<DHLink> links, IList<float> thetas)` |  |
| метод | `Solve` | `public static Matrix4x4 Solve(IList<DHLink> links, IList<float> thetas)` |  |

### `class DHInverse`

- **Файл:** `Assets\_Project\01_Scripts\Core\RobotDH.cs` (строка 67)
- **Назначение:** Обратная кинематика — осевой CCD (Cyclic Coordinate Descent): каждый сустав поворачивается ТОЛЬКО вокруг своей оси вращения, угол считается из проекций векторов «сустав→TCP» и «сустав→цель».

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `SolveCCD` | `public static bool SolveCCD(Transform[] joints, Vector3[] axes,` | Пытается свести TCP (endEffector, потомок последнего сустава) к target. Суставы от базы к фланцу. Оси вращения в локальных координатах соответствующих суставов. Точка TCP (может совпадать с последним суставом). Целевая мировая точка. Число итераций. Допуск, м. |

### `class DHLink`

- **Файл:** `Assets\_Project\01_Scripts\Core\RobotDH.cs` (строка 19)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `a` | `public float a; // длина звена (смещение по X в системе i-1)` |  |
| поле | `alphaRad` | `public float alphaRad; // скрутка между осями Z` |  |
| поле | `axisLocal` | `public Vector3 axisLocal = Vector3.up; // ось вращения в локальных осях сустава` |  |
| поле | `d` | `public float d; // офсет по оси Z` |  |
| поле | `theta0Rad` | `public float theta0Rad;// начальный угол (нулевая поза модели)` |  |
| поле | `thetaRad` | `public float thetaRad; // текущий угол` |  |
| метод | `Matrix` | `public Matrix4x4 Matrix(float theta)` |  |

## Пространство имён `KazistovVvFeatures`

### `enum CameraMode`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCinematics.cs` (строка 32)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `BarHeight` | `public float BarHeight { get { return barHeight; } }` |  |
| свойство | `Enabled` | `public bool Enabled { get { return enabled; } }` |  |
| свойство | `Mode` | `public CameraMode Mode { get { return mode; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, Camera mainCamera, Transform canvasParent)` |  |
| метод | `NextMode` | `public void NextMode()` |  |
| метод | `SetBarHeight` | `public void SetBarHeight(float value)` |  |
| метод | `SetBars` | `public void SetBars(bool value)` |  |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` |  |
| метод | `SetMode` | `public void SetMode(CameraMode value)` |  |
| метод | `SetOrbit` | `public void SetOrbit(float radius, float height, float speed)` |  |
| метод | `Status` | `public string Status()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое движение камеры. Вызывать из LateUpdate, после управления оператором. |
| метод | `Toggle` | `public void Toggle()` |  |

### `class Draft`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvEnergyOptimal.cs` (строка 31)
- **Назначение:** Черновик эко-профиля: план, метрики и выигрыш по энергии.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `accelScale` | `public float accelScale;` |  |
| поле | `applied` | `public bool applied;` |  |
| поле | `energy` | `public float energy; // Дж` |  |
| поле | `energyPerMeter` | `public float energyPerMeter; // Дж/м` |  |
| поле | `key` | `public string key = "";` |  |
| поле | `originalEnergy` | `public float originalEnergy; // Дж (исходный профиль)` |  |
| поле | `originalPeakPower` | `public float originalPeakPower;` |  |
| поле | `originalPerMeter` | `public float originalPerMeter;` |  |
| поле | `originalTime` | `public float originalTime;` |  |
| поле | `peakPower` | `public float peakPower; // Вт` |  |
| поле | `plan` | `public PlannedTrajectory plan;` |  |
| поле | `savings` | `public float savings; // % экономии энергии` |  |
| поле | `time` | `public float time; // с` |  |
| поле | `trials` | `public int trials;` |  |
| поле | `velScale` | `public float velScale;` |  |
| свойство | `DraftCount` | `public int DraftCount { get { return drafts.Count; } }` |  |
| свойство | `Last` | `public Draft Last { get { return last; } }` |  |
| свойство | `Model` | `public KvEnergyModel Model { get { Load(); return model; } }` |  |
| метод | `ApplySelected` | `public bool ApplySelected()` | Переключиться на эко-профиль (подменить время варианта). |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, KvMotionLimits motionLimits)` |  |
| метод | `Compute` | `public Draft Compute(TrajectoryCandidate candidate, bool quiet = false)` | Рассчитать эко-профиль варианта (кэшируется по лимитам, грузу и варианту). |
| метод | `CurrentEnergy` | `public float CurrentEnergy(TrajectoryCandidate candidate, out float peakPower)` | Энергия текущего (уже применённого) плана варианта — для свойств и дерева. |
| метод | `Flow` | `public TrajectoryFlowController Flow() { return flow; }` |  |
| метод | `Invalidate` | `public void Invalidate()` |  |
| метод | `MetricLine` | `public string MetricLine(TrajectoryCandidate candidate)` | Строка метрики «энергоэффективность» для статуса/свойств. |
| метод | `PayloadKg` | `public float PayloadKg` | Масса груза, кг (влияет и на энергию, и на поиск эко-профиля). |
| метод | `ResetCache` | `public void ResetCache()` |  |
| метод | `ResetSelected` | `public bool ResetSelected()` | Вернуть исходный профиль времени. |

### `class Draft`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTimeOptimal.cs` (строка 33)
- **Назначение:** Ограничения, при которых считается время-оптимальная траектория.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `applied` | `public bool applied;` |  |
| поле | `jerkOk` | `public bool jerkOk = true;` | Уложился ли фактический рывок в предел. |
| поле | `jerkReport` | `public string jerkReport = "";` | Отчёт о фактическом рывке (ФИКС 1) — честная проверка по сэмплам. |
| поле | `key` | `public string key = "";` |  |
| поле | `original` | `public KvTrajStats original;` |  |
| поле | `plan` | `public PlannedTrajectory plan;` |  |
| поле | `stats` | `public KvTrajStats stats;` |  |
| поле | `timeGain` | `public float timeGain; // % выигрыша по времени` |  |
| свойство | `DraftCount` | `public int DraftCount { get { return drafts.Count; } }` |  |
| свойство | `Last` | `public Draft Last { get { return last; } }` |  |
| свойство | `Limits` | `public KvMotionLimits Limits { get { Load(); return limits; } }` |  |
| свойство | `MaxAcc` | `public float MaxAcc { get { Load(); return limits.maxAccDeg; } }` | Максимальное ускорение суставов, °/с². |
| свойство | `MaxJerk` | `public float MaxJerk { get { Load(); return limits.maxJerkDeg; } }` | Максимальный jerk, °/с³. |
| свойство | `MaxVel` | `public float MaxVel { get { Load(); return limits.maxVelDeg; } }` | Максимальная скорость суставов, °/с. |
| метод | `ApplySelected` | `public bool ApplySelected()` | Переключиться на время-оптимальную траекторию (подменить план варианта). |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,` |  |
| метод | `Compute` | `public Draft Compute(TrajectoryCandidate candidate, bool quiet = false)` | Рассчитать время-оптимальный профиль для варианта (кэшируется по лимитам). |
| метод | `Flow` | `public TrajectoryFlowController Flow() { return flow; }` |  |
| метод | `Invalidate` | `public void Invalidate()` | Лимиты изменились — прежние расчёты больше не действительны. |
| метод | `MarkInComparison` | `public bool MarkInComparison()` | Отметить вариант в сравнении траекторий (A/B) — «переключиться на неё» в сравнении. |
| метод | `ResetCache` | `public void ResetCache()` | Сбросить при смене робота. |
| метод | `ResetSelected` | `public bool ResetSelected()` | Вернуть исходное время (отменить время-оптимальный профиль). |
| метод | `SetLimits` | `public void SetLimits(float vel, float acc, float jerk, bool save = true)` | Изменить лимиты (вызывается ползунками вкладки), с сохранением в PlayerPrefs. |

### `class FeatureHub`

- **Файл:** `Assets\_Project\01_Scripts\Features\FeatureHub.cs` (строка 26)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `discardDangerousTrajectories` | `public bool discardDangerousTrajectories = false;` |  |
| поле | `heatmapOnByDefault` | `public bool heatmapOnByDefault = true;` |  |
| поле | `heatmapSamplesPerFrame` | `public int heatmapSamplesPerFrame = 64;` |  |
| поле | `hotkeysEnabled` | `public bool hotkeysEnabled = true;` |  |
| поле | `logEvents` | `public bool logEvents = true;` |  |
| поле | `playbackSpeed` | `public float playbackSpeed = 1f;` |  |
| поле | `recordRate` | `public float recordRate = 20f;` |  |
| свойство | `Audio` | `public KvSpatialAudio Audio { get; private set; }` |  |
| свойство | `CameraRig` | `public FreeFlyCameraController CameraRig { get { return rig; } }` | Контроллер оператора (камера). |
| свойство | `Clearance` | `public KvClearanceOverlay Clearance { get; private set; }` |  |
| свойство | `Comparison` | `public KvComparison Comparison { get; private set; }` |  |
| свойство | `Current` | `public static FeatureHub Current { get { return Instance; } }` | Хаб, если он уже создан (для команд/дерева). |
| свойство | `EtaLabel` | `public string EtaLabel { get; private set; }` |  |
| свойство | `EtaProgress` | `public float EtaProgress { get; private set; }` |  |
| свойство | `EtaRemaining` | `public float EtaRemaining { get; private set; }` |  |
| свойство | `EtaTotal` | `public float EtaTotal { get; private set; }` |  |
| свойство | `EtaVisible` | `public bool EtaVisible { get; private set; }` | Показывать ли панель ETA (идёт движение). |
| свойство | `Gripper` | `public KvGripper Gripper { get; private set; }` |  |
| свойство | `Haptics` | `public KvHaptics Haptics { get; private set; }` |  |
| свойство | `Heatmap` | `public KvReachabilityHeatmap Heatmap { get; private set; }` |  |
| свойство | `Instance` | `public static FeatureHub Instance { get; private set; }` |  |
| свойство | `Log` | `public KvActionLog Log { get { return KvActionLog.Instance; } }` |  |
| свойство | `Performance` | `public KvPlannerPerformance Performance { get; private set; }` |  |
| свойство | `PickAndPlace` | `public KvPickAndPlace PickAndPlace { get; private set; }` |  |
| свойство | `PlaybackSpeed` | `public float PlaybackSpeed { get { return playbackSpeed; } }` | Текущий множитель скорости (для панели). |
| свойство | `Poses` | `public KvPoseLibrary Poses { get; private set; }` |  |
| свойство | `Presentation` | `public KvPresentationMode Presentation { get; private set; }` |  |
| свойство | `Recording` | `public KvRecordingService Recording { get; private set; }` |  |
| свойство | `Scenarios` | `public KvScenarioManager Scenarios { get; private set; }` |  |
| свойство | `Sessions` | `public KvSessionManager Sessions { get; private set; }` |  |
| свойство | `Undo` | `public KvUndoStack Undo { get; private set; }` |  |
| свойство | `Window` | `public KvFeatureWindow Window { get; private set; }` |  |
| свойство | `World` | `public CollisionWorld World { get; private set; }` | Свой мир столкновений для новых функций (поток свой не отдаёт, а трогать его нельзя). |
| свойство | `Zones` | `public KvZoneService Zones { get; private set; }` |  |
| метод | `BuildExtraProperties` | `public static void BuildExtraProperties(ProjectNode node, List<KvProp> list) {` | Дополнительные строки свойств для узлов новых функций (вызывает UI-менеджер). |
| метод | `BuildTreeNodes` | `public static void BuildTreeNodes(KazistovVvUIManager manager, List<ProjectNode> roots,` | Добавить ветки новых функций в дерево моделей (вызывает UI-менеджер). |
| метод | `CreateZone` | `public void CreateZone(KvZoneShape shape)` | Создать зону запрета в точке прицела. |
| метод | `CyclePlaybackSpeed` | `public void CyclePlaybackSpeed()` | Циклический перебор множителя скорости воспроизведения. |
| метод | `Dump` | `public string Dump()` | Данные для свойств/диагностики: список сервисов одной строкой. |
| метод | `EmergencyStop` | `public void EmergencyStop()` | АВАРИЙНАЯ ОСТАНОВКА (этап 4): стоп всего, сброс траектории, Idle, журнал. |
| метод | `Ensure` | `public static FeatureHub Ensure(KazistovVvUIManager manager)` | Создать (или найти) хаб и панель функций. Вызывается UI-менеджером. |
| метод | `Flow` | `public TrajectoryFlowController Flow() { return flow; }` | Поток этапов (для панели функций; null — ещё не найден). |
| метод | `LocalizeRefresh` | `public static void LocalizeRefresh()` | ЭТАП 2 (мультиязычность): после смены языка обновить подписи панели функций. Мгновенно, без перезагрузки и без пересоздания окна. |
| метод | `MarkForComparison` | `public void MarkForComparison()` | Отметить выбранную в дереве траекторию для сравнения (этап 6). |
| метод | `RedoLast` | `public void RedoLast()` |  |
| метод | `RegisterCommands` | `public static void RegisterCommands(KazistovVvUIManager manager)` | Зарегистрировать команды новых функций. Вызывает UI-менеджер. |
| метод | `RunPickAndPlace` | `public void RunPickAndPlace()` | Запустить pick-and-place демо (этап 11). |
| метод | `TogglePlayback` | `public void TogglePlayback()` | Воспроизведение последней/активной записи (этап 1). |
| метод | `ToggleRecord` | `public void ToggleRecord()` | Запись вкл/выкл (этап 1). |
| метод | `TreeSignaturePart` | `public static string TreeSignaturePart()` | Компактная «подпись» состояния новых функций для подписи дерева моделей (менеджер пересобирает дерево только при изменении подписи — экономия кадров). |
| метод | `UndoLast` | `public void UndoLast()` |  |

### `class FeatureStorage`

- **Файл:** `Assets\_Project\01_Scripts\Features\FeatureStorage.cs` (строка 27)
- **Назначение:** Единое хранилище файлов новых функций (этапы 1–20 ТЗ): записи траекторий, позы, зоны запрета, журнал действий, сценарии, сессии. ФОРМАТ — JSON (текстовый), сериализация — JsonUtility. Обоснование выбора (ТЗ этапа 1 требует обосновать): 1) читается и правится человеком, диффится в git — важно для …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `ConfigDir` | `public static string ConfigDir { get { return EnsureDir(Path.Combine(Root, "Config")); } }` |  |
| свойство | `LogsDir` | `public static string LogsDir { get { return EnsureDir(Path.Combine(Root, "Logs")); } }` |  |
| свойство | `PosesDir` | `public static string PosesDir { get { return EnsureDir(Path.Combine(Root, "Poses")); } }` |  |
| свойство | `RecordingsDir` | `public static string RecordingsDir { get { return EnsureDir(Path.Combine(Root, "Recordings")); } }` |  |
| свойство | `ScenariosDir` | `public static string ScenariosDir { get { return EnsureDir(Path.Combine(Root, "Scenarios")); } }` |  |
| свойство | `SessionsDir` | `public static string SessionsDir { get { return EnsureDir(Path.Combine(Root, "Sessions")); } }` |  |
| свойство | `ZonesDir` | `public static string ZonesDir { get { return EnsureDir(Path.Combine(Root, "Zones")); } }` |  |
| метод | `AppendLine` | `public static bool AppendLine(string path, string line)` | Дописать строку в файл (журнал действий, телеметрия прогонов). |
| метод | `DeleteFile` | `public static bool DeleteFile(string path)` | Удалить файл (false — файла не было или он занят). |
| метод | `EnsureDir` | `public static string EnsureDir(string path)` |  |
| метод | `FileStem` | `public static string FileStem(string path)` | Имя файла без расширения (для подписи в дереве/списке). |
| метод | `IsoNow` | `public static string IsoNow()` | ISO-время для полей «created». |
| метод | `LegacyRecordingFolder` | `public static string LegacyRecordingFolder` | Каталог существующей заготовки записи (ТЗ этапа 1: «используй существующую»). |
| метод | `ListFiles` | `public static List<string> ListFiles(string dir, string pattern)` | Список файлов каталога по маске (без временных). |
| метод | `Root` | `public static string Root` | Корневой каталог данных функций. |
| метод | `SafeName` | `public static string SafeName(string name, string fallback = "item")` | Безопасное имя файла из произвольной пользовательской строки. |
| метод | `TimeStamp` | `public static string TimeStamp()` | Метка времени для имён файлов: 20260101_120000. |

### `interface IKvSpeechSource`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvXrInput.cs` (строка 28)
- **Назначение:** ЭТАП 23 ТЗ: ГОЛОСОВЫЕ КОМАНДЫ. ЧТО СДЕЛАНО ЧЕСТНО И РАБОТАЕТ БЕЗ ДОПОЛНИТЕЛЬНЫХ ПАКЕТОВ: • РЕЧЕВОЙ ДЕТЕКТОР (VAD) — реальный захват микрофона (`Microphone`), RMS-уровень, автоматическое определение начала и конца фразы по громкости; • ГРАММАТИКА КОМАНД — разбор текста фразы на русском и английско…

_Публичных членов нет (или тип объявлен без них)._

### `interface IKvUndoAction`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvUndoRedo.cs` (строка 8)
- **Назначение:** Одно отменяемое действие (ЭТАП 15 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `interface IKvWorkbenchTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvWorkbenchWindow.cs` (строка 14)
- **Назначение:** ВКЛАДКА ВЕРСТАКА ПОСТОБРАБОТКИ. Модуль этапов 4–6 (а затем и последующих) добавляет свою вкладку регистрацией, поэтому окно не нужно править при появлении новых функций: как реестр команд у тулбара — здесь реестр вкладок.

_Публичных членов нет (или тип объявлен без них)._

### `class KvActionLog`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvActionLog.cs` (строка 60)
- **Назначение:** ЖУРНАЛ ДЕЙСТВИЙ (ЭТАП 13 ТЗ). Прокручиваемый список событий: «точка выбрана», «траектория №3 подтверждена», «робот начал движение», «аварийная остановка» и т.п. Хранится в кольцевом буфере (последние `capacity` записей) И на диске — каждая строка дописывается в файл сразу (JSON Lines + читаемый т…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `capacity` | `public int capacity = 600;` | Сколько записей держать в памяти. |
| поле | `echoToConsole` | `public bool echoToConsole;` | Дублировать ли записи в консоль Unity (по умолчанию — только важные). |
| свойство | `Count` | `public int Count { get { return entries.Count; } }` |  |
| свойство | `Entries` | `public IReadOnlyList<KvLogEntry> Entries { get { return entries; } }` | Все записи (новые в конце). |
| событие | `Changed` | `public event Action Changed;` |  |
| метод | `Add` | `public void Add(KvLogKind kind, string text)` | Добавить событие. |
| метод | `Clear` | `public void Clear()` |  |
| метод | `Error` | `public void Error(string text) { Add(KvLogKind.Error, text); }` |  |
| метод | `Export` | `public string Export()` | Выгрузить весь буфер в отдельный файл (кнопка «Экспорт журнала»). |
| метод | `FilePath` | `public string FilePath` | Файл журнала (текстовый, дописывается построчно). |
| метод | `Filter` | `public KvLogKind? Filter` | Активный фильтр (null — показывать все типы). |
| метод | `Filtered` | `public List<KvLogEntry> Filtered()` | Записи с учётом фильтра (для панели журнала). |
| метод | `Info` | `public void Info(string text) { Add(KvLogKind.System, text); }` |  |
| метод | `Instance` | `public static KvActionLog Instance` | Общий журнал приложения (создаётся при первом обращении). |
| метод | `KindColor` | `public static Color KindColor(KvLogKind kind)` |  |
| метод | `KindLabel` | `public static string KindLabel(KvLogKind kind)` |  |
| метод | `ShortKind` | `public static string ShortKind(KvLogKind kind)` | Короткое имя типа для колонки фильтра. |
| метод | `Tail` | `public List<KvLogEntry> Tail(int count)` | Последние N записей с учётом фильтра (для прокрутки «в конец»). |
| метод | `TryParseKind` | `public static bool TryParseKind(string name, out KvLogKind kind)` | Тип события по строке настроек (для внешней схемы). |
| метод | `Warning` | `public void Warning(string text) { Add(KvLogKind.Warning, text); }` | Предупреждение (этап 36 ТЗ): не ошибка, но требует внимания оператора. |

### `class KvAnnotation`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCinematics.cs` (строка 325)
- **Назначение:** Пояснение, привязанное к точке сцены (этап 32 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `color` | `public Color color = Color.white;` |  |
| поле | `point` | `public Vector3 point;` |  |
| поле | `text` | `public string text = "";` |  |

### `class KvBehaviorTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 2173)
- **Назначение:** ВКЛАДКА «ДЕРЕВО ПОВЕДЕНИЯ» (ЭТАП 27 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "behavior"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("bt.title", "Дерево поведения"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvBehaviorTree`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 1368)
- **Назначение:** Дерево поведения: набор узлов, корень и запуск.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `name` | `public string name = "Дерево 1";` |  |
| поле | `nodes` | `public readonly List<KvBtNode> nodes = new List<KvBtNode>();` |  |
| поле | `rootId` | `public int rootId = -1;` |  |
| метод | `Add` | `public KvBtNode Add(KvBtNodeType type, float x, float y)` |  |
| метод | `ExportScript` | `public string ExportScript()` | Выгрузка дерева в текст скрипта (этап 26) — для повторного запуска макросом. |
| метод | `Find` | `public KvBtNode Find(int id)` |  |
| метод | `Flatten` | `public List<int> Flatten()` | Плоский список узлов сверху вниз — для обхода в рантайме. |
| метод | `Link` | `public bool Link(int parentId, int childId)` | Сделать `child` потомком `parent` (с проверкой на цикл). |
| метод | `Remove` | `public void Remove(int id)` |  |
| метод | `Unlink` | `public void Unlink(int childId)` |  |

### `class KvBehaviorTreeWindow`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 1715)
- **Назначение:** Редактор дерева поведения на отдельном канвасе (этап 27 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `sortingOrder` | `public int sortingOrder = 47;` |  |
| свойство | `Instance` | `public static KvBehaviorTreeWindow Instance { get { return instance; } }` |  |
| свойство | `Tree` | `public KvBehaviorTree Tree { get { return tree; } }` |  |
| свойство | `Visible` | `public bool Visible { get { return visible; } }` |  |
| метод | `Install` | `public static KvBehaviorTreeWindow Install(Transform parent, KvBehaviorTree behaviorTree,` |  |
| метод | `SetVisible` | `public void SetVisible(bool value)` |  |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvBenchResult`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPlannerLab.cs` (строка 37)
- **Назначение:** Итог бенчмарка по одной стратегии.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `avgLengthM` | `public float avgLengthM;` |  |
| поле | `avgMarginDeg` | `public float avgMarginDeg;` |  |
| поле | `avgTimeMs` | `public float avgTimeMs;` |  |
| поле | `maxTimeMs` | `public float maxTimeMs;` |  |
| поле | `minTimeMs` | `public float minTimeMs = float.MaxValue;` |  |
| поле | `strategy` | `public KvPlanStrategy strategy;` |  |
| поле | `success` | `public int success;` |  |
| поле | `tasks` | `public int tasks;` |  |
| свойство | `SuccessRate` | `public float SuccessRate { get { return tasks > 0 ? success / (float)tasks : 0f; } }` |  |
| метод | `Line` | `public string Line()` |  |
| метод | `StrategyLabel` | `public string StrategyLabel` |  |

### `class KvBenchTask`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPlannerLab.cs` (строка 23)
- **Назначение:** Результат одной задачи бенчмарка (этап 14 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `index` | `public int index;` |  |
| поле | `iterations` | `public int iterations;` |  |
| поле | `lengthM` | `public float lengthM;` |  |
| поле | `limitMarginDeg` | `public float limitMarginDeg;` |  |
| поле | `note` | `public string note = "";` |  |
| поле | `samples` | `public int samples;` |  |
| поле | `strategy` | `public KvPlanStrategy strategy;` |  |
| поле | `success` | `public bool success;` |  |
| поле | `timeMs` | `public float timeMs;` |  |

### `class KvBtNode`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 1341)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `children` | `public readonly List<int> children = new List<int>();` |  |
| поле | `id` | `public int id;` |  |
| поле | `parent` | `public int parent = -1;` |  |
| поле | `payload` | `public string payload = ""; // команда для Action, выражение для Condition, секунды для Wait` |  |
| поле | `title` | `public string title = "";` |  |
| поле | `type` | `public KvBtNodeType type = KvBtNodeType.Action;` |  |
| метод | `TypeLabel` | `public string TypeLabel` |  |

### `enum KvBtNodeType`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 1336)

_Публичных членов нет (или тип объявлен без них)._

### `class KvBtNodeView`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 2105)
- **Назначение:** Перетаскивание узла дерева мышью (drag-and-drop редактора этапа 27).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `node` | `public KvBtNode node;` |  |
| поле | `rect` | `public RectTransform rect;` |  |
| поле | `window` | `public KvBehaviorTreeWindow window;` |  |
| метод | `OnDrag` | `public void OnDrag(PointerEventData eventData)` |  |
| метод | `OnPointerDown` | `public void OnPointerDown(PointerEventData eventData)` |  |
| метод | `OnPointerUp` | `public void OnPointerUp(PointerEventData eventData)` |  |

### `class KvBtRunner`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 1531)
- **Назначение:** ИСПОЛНИТЕЛЬ ДЕРЕВА ПОВЕДЕНИЯ: обходит дерево в главном потоке. Последовательность выполняется, пока все дети успешны; выбор — до первого успеха; действие запускает мини-скрипт (движок этапа 26) и ждёт его завершения; условие проверяет выражение тем же разборщиком; пауза ждёт указанное время.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `ActiveId` | `public int ActiveId { get { return activeId; } }` |  |
| свойство | `Path` | `public IList<int> Path { get { return path; } }` |  |
| свойство | `Result` | `public string Result { get { return result; } }` |  |
| свойство | `Running` | `public bool Running { get { return running; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `SetTree` | `public void SetTree(KvBehaviorTree value) { tree = value; }` |  |
| метод | `Start` | `public bool Start()` |  |
| метод | `StatusOf` | `public KvBtStatus StatusOf(int id)` |  |
| метод | `Stop` | `public void Stop()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` |  |

### `enum KvBtStatus`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 1337)

_Публичных членов нет (или тип объявлен без них)._

### `class KvCalibrationData`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCalibration.cs` (строка 12)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `baseEulerWorld` | `public float[] baseEulerWorld = new float[3]; // предлагаемый доворот базы, °` |  |
| поле | `baseForwardPose` | `public float[] baseForwardPose = new float[6];` |  |
| поле | `baseForwardSet` | `public bool baseForwardSet;` |  |
| поле | `baseForwardWorld` | `public float[] baseForwardWorld = new float[3];` |  |
| поле | `baseHeightMm` | `public float baseHeightMm; // высота базы над опорной плоскостью` |  |
| поле | `baseOffsetWorld` | `public float[] baseOffsetWorld = new float[3]; // предлагаемое смещение базы в мире, м` |  |
| поле | `basePlaneNormal` | `public float[] basePlaneNormal = new float[3];` |  |
| поле | `basePoints` | `public int basePoints;` |  |
| поле | `baseSolved` | `public bool baseSolved;` |  |
| поле | `baseTiltDeg` | `public float baseTiltDeg; // наклон оси базы к нормали плоскости` |  |
| поле | `baseYawDeg` | `public float baseYawDeg; // доворот базы вокруг вертикали, °` |  |
| поле | `cameraCalibrated` | `public bool cameraCalibrated;` |  |
| поле | `cameraNote` | `public string cameraNote = "заглушка: ждём модель крепления камеры";` |  |
| поле | `created` | `public string created = "";` |  |
| поле | `notes` | `public string notes = "";` |  |
| поле | `robot` | `public string robot = "";` |  |
| поле | `tcpLength` | `public float tcpLength; // \|смещение\|, м` |  |
| поле | `tcpMaxResidualMm` | `public float tcpMaxResidualMm; // худшая точка, мм` |  |
| поле | `tcpMethod` | `public string tcpMethod = "4point"; // "4point" (робот) \| "plane" (SCARA)` |  |
| поле | `tcpOffsetFlange` | `public float[] tcpOffsetFlange = new float[3]; // смещение инструмента в системе фланца, м` |  |
| поле | `tcpOffsetPlane` | `public float[] tcpOffsetPlane = new float[3]; // смещение TCP в мире сцены, м (для отчёта)` |  |
| поле | `tcpPlaneHeight` | `public float tcpPlaneHeight; // высота инструмента над плоскостью, м` |  |
| поле | `tcpPlaneNormal` | `public float[] tcpPlaneNormal = new float[3]; // нормаль опорной плоскости` |  |
| поле | `tcpPlanePoints` | `public int tcpPlanePoints;` |  |
| поле | `tcpPlaneResidualMm` | `public float tcpPlaneResidualMm; // остаток приведения к плоскости, мм` |  |
| поле | `tcpPlaneSolved` | `public bool tcpPlaneSolved;` |  |
| поле | `tcpPoints` | `public int tcpPoints;` |  |
| поле | `tcpPose` | `public float[] tcpPose = new float[6]; // поза при последней записи (диагностика)` |  |
| поле | `tcpResidualMm` | `public float tcpResidualMm; // СКО остатка, мм` |  |
| поле | `tcpSolved` | `public bool tcpSolved;` |  |
| поле | `version` | `public int version = 1;` |  |

### `class KvCalibrationService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCalibration.cs` (строка 82)
- **Назначение:** ЭТАП 9 ТЗ: КАЛИБРОВОЧНЫЙ МАСТЕР. 1. КАЛИБРОВКА TCP ПО 4 ТОЧКАМ. Оператор наводит инструмент в ОДНУ И ТУ ЖЕ точку пространства четырьмя разными ориентациями. Для каждой записи берётся поза суставов, из неё считается СИСТЕМА ФЛАНЦА (положение — пивот последнего сустава, оси — ось последнего сустава…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `referencePlaneHeightM` | `public float referencePlaneHeightM = TrajectoryCore.StandBuilder.TopHeight;` | ВЫСОТА ОПОРНОЙ ПЛОСКОСТИ НАД ПОЛОМ, м (ФИКС 3): по умолчанию — столешница стенда (`StandBuilder.TopHeight`). Оператор касается ИМЕННО этой плоскости, поэтому её положение нужно знать: из одних только касаний высота инструмента НЕ определяется — при любой длине инструмента точки касания всё равно … |
| поле | `TcpPlanePointsNeeded` | `public const int TcpPlanePointsNeeded = 3;` | Сколько точек нужно методу «по нормали» (минимум 3 точки плоскости). |
| поле | `TcpPointsNeeded` | `public const int TcpPointsNeeded = 4;` |  |
| свойство | `BaseForwardSet` | `public bool BaseForwardSet { get { return baseForwardSet; } }` | Задано ли направление «вперёд» (для интерфейса). |
| свойство | `BasePointCount` | `public int BasePointCount { get { return basePoints.Count; } }` |  |
| свойство | `BaseSolved` | `public bool BaseSolved { get { return data.baseSolved; } }` |  |
| свойство | `Data` | `public KvCalibrationData Data { get { return data; } }` |  |
| свойство | `TcpPlanePointCount` | `public int TcpPlanePointCount { get { return planeFlange.Count; } }` | Сколько точек плоскости записано для метода «по нормали» (ФИКС 3). |
| свойство | `TcpPointCount` | `public int TcpPointCount { get { return tcpPoses.Count; } }` |  |
| свойство | `TcpSolved` | `public bool TcpSolved { get { return data.tcpSolved; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `ApplyBaseToScene` | `public bool ApplyBaseToScene()` | Применить найденную коррекцию базы к роботу в сцене (runtime, не в файл сцены). |
| метод | `ApplyToolOffsetToFlow` | `public bool ApplyToolOffsetToFlow()` | Применить найденное смещение инструмента к потоку: проект ведёт TCP смещением вдоль направления (`toolOffset`), поэтому применяется ДЛИНА инструмента — направление задано моделью робота. Полный вектор остаётся в файле калибровки. |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)` |  |
| метод | `FilePath` | `public string FilePath` |  |
| метод | `FlangeFrame` | `public static bool FlangeFrame(PoseValidator v, double[] q, out Vector3 position,` | Система фланца для конфигурации q: положение — пивот последнего сустава, ориентация — ось последнего сустава (z) и перпендикуляр к оси предыдущего (x). Считается по ФАКТИЧЕСКОЙ кинематике валидатора, без изменений в ядре. |
| метод | `Load` | `public bool Load()` | Загрузить калибровку из файла (если он есть). |
| метод | `MarkCameraStub` | `public void MarkCameraStub()` | Отметить калибровку камеры как «заглушку» (ТЗ: заглушка на будущее). |
| метод | `PlaneMethodOverride` | `public bool PlaneMethodOverride` | Ручное переопределение метода (true — всегда «по нормали»). |
| метод | `RecordBaseDirection` | `public bool RecordBaseDirection()` | ЗАПИСАТЬ НАПРАВЛЕНИЕ «ВПЕРЁД» БАЗЫ (ФИКС 4): оператор ставит инструмент в точку на оси X базы робота (направление «вперёд» робота) и нажимает кнопку. Без этой точки поворот базы вокруг вертикали не определяется — плоскость его не задаёт. |
| метод | `RecordBasePoint` | `public bool RecordBasePoint()` | Записать точку касания опорной плоскости (столешницы) текущим TCP. |
| метод | `RecordTcpPlanePoint` | `public bool RecordTcpPlanePoint()` | ЗАПИСАТЬ ТОЧКУ ПЛОСКОСТИ (ФИКС 3): берётся текущая поза, из неё — положение ФЛАНЦА (пивот последней оси). Оператор ставит инструмент в N ≥ 3 точки одной плоскости (например, столешницы) в разных местах рабочей зоны. |
| метод | `RecordTcpPoint` | `public bool RecordTcpPoint()` | Записать точку касания: берётся ТЕКУЩАЯ поза суставов (оператор только что приложил инструмент к эталонной точке новой ориентацией). |
| метод | `ResetBaseDirection` | `public void ResetBaseDirection()` | Сбросить направление «вперёд» (вторая точка отсчёта). |
| метод | `ResetBasePoints` | `public void ResetBasePoints()` |  |
| метод | `ResetTcpPlanePoints` | `public void ResetTcpPlanePoints()` | Сбросить точки метода «по нормали». |
| метод | `ResetTcpPoints` | `public void ResetTcpPoints()` |  |
| метод | `Save` | `public bool Save()` | Сохранить калибровку в файл JSON (ТЗ: «Сохранение калибровки в файл»). |
| метод | `SelfTest` | `public static string SelfTest()` | САМОПРОВЕРКА МЕТОДА на синтетических данных: по известному смещению инструмента строятся четыре системы фланца, касающиеся ОДНОЙ точки, и метод обязан вернуть исходное смещение. Возвращает текст отчёта для диагностики. |
| метод | `SelfTestPlane` | `public static string SelfTestPlane()` | САМОПРОВЕРКА МЕТОДА «ПО НОРМАЛИ» на синтетических данных: задаётся плоскость и известная высота инструмента, точки фланцев строятся как «касание + ось·h». |
| метод | `SolveBase` | `public bool SolveBase()` | РЕШЕНИЕ КАЛИБРОВКИ БАЗЫ: по точкам касания плоскости считается её нормаль, высота базы над плоскостью и наклон оси базы к нормали, а также предлагаемое смещение/доворот базы в мире. |
| метод | `SolvePlaneOffset` | `public static bool SolvePlaneOffset(List<Vector3> points, Vector3 toolAxis,` | ЯДРО МЕТОДА «ПО НОРМАЛИ» (ФИКС 3): по точкам ФЛАНЦЕВ и оси инструмента считается нормаль опорной плоскости, высота инструмента (смещение вдоль оси) и остаток в мм. • нормаль — собственный вектор наименьшей дисперсии ковариационной матрицы точек (устойчивее «векторных произведений соседних троек»:… |
| метод | `SolveTcp` | `public bool SolveTcp()` | РЕШЕНИЕ СМЕЩЕНИЯ TCP по записанным точкам: метод наименьших квадратов для системы (R_i − R_1)·t = p_1 − p_i. Возвращает false с понятной причиной, если точек мало. |
| метод | `SolveTcpPlane` | `public bool SolveTcpPlane()` | РЕШЕНИЕ СМЕЩЕНИЯ TCP ПО НОРМАЛИ К ПЛОСКОСТИ (ФИКС 3). У SCARA ориентация инструмента при движении не меняется (кисти нет), поэтому `TCP_i = Фланец_i + R·t`. Все точки касания лежат в ОДНОЙ плоскости, значит фланцы тоже лежат в плоскости, параллельной опорной, сдвинутой на вектор инструмента. Отсю… |
| метод | `SolveToolOffset` | `public static bool SolveToolOffset(Vector3[] positions, Quaternion[] rotations,` | РЕШЕНИЕ 4-ТОЧЕЧНОЙ КАЛИБРОВКИ по системам фланца: метод наименьших квадратов для системы «(R_i − R_1)·t = p_1 − p_i» (неизвестная точка касания исключается). Вынесено отдельной функцией, чтобы метод можно было проверить на синтетических данных (см. SelfTest). |
| метод | `TcpMethodLabel` | `public string TcpMethodLabel` | Подпись действующего метода для интерфейса и журнала. |
| метод | `UsePlaneMethod` | `public bool UsePlaneMethod` | ВЫБОР МЕТОДА КАЛИБРОВКИ TCP АВТОМАТИЧЕСКИ ПО ТИПУ РОБОТА (ФИКС 3): у SCARA кисти нет (ориентация инструмента не меняется), поэтому 4-точечный метод для неё вырожден — применяется метод «по нормали к плоскости». Оператор может переопределить выбор вручную (`PlaneMethodOverride`). |

### `class KvCalibrationTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCalibration.cs` (строка 1082)
- **Назначение:** ВКЛАДКА «КАЛИБРОВОЧНЫЙ МАСТЕР» (ЭТАП 9 ТЗ): пошаговые шаги TCP (4 точки), базы (3+ точки) и камеры (заглушка) с сохранением результата в файл.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "calibration"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra2.T("calib.title", "Калибровочный мастер"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvCameraService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCameras.cs` (строка 39)
- **Назначение:** ЭТАП 16 ТЗ: НЕСКОЛЬКО КАМЕР / PiP (КАРТИНКА В КАРТИНКЕ). Что делает: • создаёт ДОПОЛНИТЕЛЬНЫЕ камеры: вид сверху, вид сбоку и вид «от первого лица» (камера у инструмента робота, смотрит его же осью) — основная камера оператора при этом работает как обычно; • каждая камера рисуется в свою текстуру…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Fullscreen` | `public string Fullscreen { get { return fullscreenId; } }` |  |
| свойство | `Windows` | `public IReadOnlyList<KvPiPWindow> Windows { get { return windows; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `AnyVisible` | `public bool AnyVisible` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, Camera mainCamera)` |  |
| метод | `Build` | `public void Build(Transform parent)` | Собрать окна PiP (вызывается один раз при привязке). |
| метод | `Dispose` | `public void Dispose()` |  |
| метод | `SetVisible` | `public void SetVisible(string id, bool visible)` | Показать/скрыть окно по id ("" — переключить все). |
| метод | `Status` | `public string Status()` | Строка состояния для дерева моделей и свойств (этап 16 ТЗ). |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое сопровождение: камеры ставятся в нужные точки. |
| метод | `Toggle` | `public void Toggle(string id)` |  |
| метод | `ToggleAll` | `public void ToggleAll()` | Переключить весь набор окон (кнопка/клавиша). |
| метод | `ToggleFullscreen` | `public void ToggleFullscreen(string id)` | Показать одно окно на весь экран (повторный вызов возвращает PiP). |

### `class KvCameraTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCameras.cs` (строка 323)
- **Назначение:** ВКЛАДКА «КАМЕРЫ И PiP» (ЭТАП 16 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "cameras"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("cam.title", "Камеры и PiP"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvCaptureService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCaptures.cs` (строка 35)
- **Назначение:** ЭТАП 3 ТЗ — ЭКСПОРТ ДЕМОНСТРАЦИЙ: скриншоты (PNG) и видеозапись (MP4). КУДА СОХРАНЯЕТСЯ (ТЗ): стандартная папка Windows «Видео» — берётся системно (`Environment.SpecialFolder.MyVideos`, на этой машине `C:\Users\Ольга\Videos`), а если система папку не отдала — `%USERPROFILE%\Videos`. Имена файлов …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `annotate` | `public bool annotate = true;` |  |
| поле | `logEvents` | `public bool logEvents = true;` |  |
| поле | `screenshotKey` | `public KeyCode screenshotKey = KeyCode.F8;` |  |
| поле | `videoBitrateKbps` | `public int videoBitrateKbps = 12000;` |  |
| поле | `videoFps` | `public int videoFps = 30;` |  |
| поле | `videoHeight` | `public int videoHeight = 1080;` |  |
| поле | `videoKey` | `public KeyCode videoKey = KeyCode.F10;` |  |
| поле | `videoWidth` | `public int videoWidth = 1920;` |  |
| свойство | `IsRecording` | `public bool IsRecording { get { return recording; } }` |  |
| свойство | `LastRecordingFolder` | `public string LastRecordingFolder { get { return recordPath; } }` |  |
| свойство | `LastScreenshotPath` | `public string LastScreenshotPath { get { return lastScreenshot; } }` |  |
| свойство | `LastVideoPath` | `public string LastVideoPath { get { return lastVideo; } }` |  |
| свойство | `RecordedFrames` | `public int RecordedFrames { get { return videoFrames; } }` |  |
| свойство | `RecordStartedAt` | `public float RecordStartedAt { get { return recording ? recordStartedAt : 0f; } }` | Момент начала текущей видеозаписи (`Time.realtimeSinceStartup`), 0 — запись не идёт. Добавлено на этапе 33 ТЗ: запись голоса диктора сохраняет СМЕЩЕНИЕ относительно начала видео, чтобы на монтаже звук выставлялся одним движением. |
| событие | `Message` | `public event Action<string> Message;` | Сообщение для журнала/статуса (подписка — хабом). |
| метод | `Create` | `public static KvCaptureService Create(Transform parent)` | Создать сервис на скрытом служебном объекте (сцена не меняется). |
| метод | `Folder` | `public string Folder` | Папка экспорта (стандартная «Видео» Windows). |
| метод | `RecordingName` | `public static string RecordingName(DateTime time)` | Имя файла записи по ТЗ: KazistovVv_recording_YYYY-MM-DD_HH-MM-SS |
| метод | `ScreenshotName` | `public static string ScreenshotName(DateTime time)` | Имя файла по ТЗ: KazistovVv_screenshot_YYYY-MM-DD_HH-MM-SS.png |
| метод | `StartRecording` | `public bool StartRecording()` | Начать запись вида оператора. Возвращает true, если запись действительно началась (через Unity Recorder либо через резервный режим кадров). |
| метод | `Status` | `public string Status` | Строка состояния для панели/подсказки. |
| метод | `StopRecording` | `public void StopRecording()` | Остановить запись. |
| метод | `TakeScreenshot` | `public bool TakeScreenshot(string stateLabel, string robotName)` | Сделать скриншот. `stateLabel` и `robotName` попадают в подпись (ТЗ: «опционально: аннотации (дата, состояние, выбранный робот)»). Возвращает false, если снимок уже делается или экран недоступен. |
| метод | `Tick` | `public void Tick()` | Кадровое обслуживание РЕЗЕРВНОГО режима (без Unity Recorder): кадры снимаются с частотой `videoFps`. При работающем рекордере здесь делать нечего — он сам пишет MP4 из вида игры. |
| метод | `ToggleRecording` | `public bool ToggleRecording()` | Старт/стоп записи (кнопка тулбара и клавиша). |

### `class KvCinemaTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCinematics.cs` (строка 944)
- **Назначение:** ВКЛАДКА «КИНОРЕЖИМ» (ЭТАП 31 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "cine"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("cine.title", "Кинематографический режим"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvCinematicService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCinematics.cs` (строка 30)
- **Назначение:** ЭТАП 31 ТЗ: КИНЕМАТОГРАФИЧЕСКИЙ РЕЖИМ (CINEMATIC MODE). Что делает: • «широкий экран» — чёрные полосы сверху и снизу (настраиваемая высота) и мягкое затемнение кадра; • автоматическое движение камеры: облёт робота по кругу, слежение за инструментом, неподвижный «штатив»; движение плавное (демпфир…

_Публичных членов нет (или тип объявлен без них)._

### `class KvClearanceOverlay`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvHeatmaps.cs` (строка 351)
- **Назначение:** ТЕПЛОВАЯ КАРТА ЗАЗОРОВ (ЭТАП 9 ТЗ). Показывает, где траектория проходит близко к препятствиям: цвет участка — от зелёного (большой запас) до красного (опасно близко). Рисуется ПОВЕРХ траектории: для каждого варианта считаются зазоры по сэмплам (`PoseValidator.ClearanceAt` на том же мире столкнове…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `ColorGreen` | `public static readonly Color ColorGreen = new Color(0.25f, 0.95f, 0.30f);` |  |
| поле | `ColorOrange` | `public static readonly Color ColorOrange = new Color(1f, 0.52f, 0.10f);` |  |
| поле | `ColorRed` | `public static readonly Color ColorRed = new Color(1f, 0.12f, 0.10f);` |  |
| поле | `ColorYellow` | `public static readonly Color ColorYellow = new Color(1f, 0.90f, 0.18f);` |  |
| поле | `orangeThreshold` | `public float orangeThreshold = 0.05f;` |  |
| поле | `redThreshold` | `public float redThreshold = 0.02f;` | Пороги зазора (м) для уровней: красный ≤ safety, оранжевый, жёлтый, зелёный. |
| поле | `width` | `public float width = 0.022f;` | Толщина линии, м. |
| поле | `yellowThreshold` | `public float yellowThreshold = 0.10f;` |  |
| свойство | `LastSummary` | `public string LastSummary { get; private set; }` | Последний отчёт для панели (мин. зазор, число опасных участков). |
| свойство | `Visible` | `public bool Visible { get; private set; }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)` |  |
| метод | `Dispose` | `public void Dispose()` |  |
| метод | `Rebuild` | `public void Rebuild(bool force)` | Полная пересборка раскраски. |
| метод | `RebuildIfNeeded` | `public void RebuildIfNeeded()` | Пересобрать при смене набора траекторий (вызывает хаб). |
| метод | `SetVisible` | `public void SetVisible(bool value)` |  |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvCollaborationService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvNetTools.cs` (строка 48)
- **Назначение:** ЭТАП 20 ТЗ: МУЛЬТИПЛЕЕР (COLLABORATION MODE) — ДВА ПОЛЬЗОВАТЕЛЯ В ОДНОЙ СЦЕНЕ. Реализация без внешних пакетов (Netcode/Mirror в проект не добавлялись — это решение зафиксировано в контексте): обмен идёт по UDP компактными текстовыми пакетами. • ОПЕРАТОР рассылает состояние 10 раз в секунду: имя р…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `DefaultPort` | `public const int DefaultPort = 47777;` |  |
| поле | `heartbeatInterval` | `public float heartbeatInterval = 0.5f;` |  |
| поле | `PeerPrefsKey` | `public const string PeerPrefsKey = "KazistovVv.Net.Peer";` |  |
| поле | `PortPrefsKey` | `public const string PortPrefsKey = "KazistovVv.Net.Port";` |  |
| поле | `RolePrefsKey` | `public const string RolePrefsKey = "KazistovVv.Net.Role";` |  |
| поле | `timeoutSeconds` | `public float timeoutSeconds = 2f;` |  |
| свойство | `HeartbeatInterval` | `public float HeartbeatInterval { get { return heartbeatInterval; } }` |  |
| свойство | `HeartbeatsReceived` | `public int HeartbeatsReceived { get { return heartbeatsReceived; } }` |  |
| свойство | `HeartbeatsSent` | `public int HeartbeatsSent { get { return heartbeatsSent; } }` |  |
| свойство | `LinkAlive` | `public bool LinkAlive { get { return link == KvLinkState.Up; } }` |  |
| свойство | `Log` | `public string Log { get { return log; } }` |  |
| свойство | `PacketsReceived` | `public int PacketsReceived { get { return packetsReceived; } }` |  |
| свойство | `PacketsSent` | `public int PacketsSent { get { return packetsSent; } }` |  |
| свойство | `Peer` | `public string Peer { get { return peer; } }` |  |
| свойство | `Port` | `public int Port { get { return port; } }` |  |
| свойство | `RemotePosesIgnored` | `public int RemotePosesIgnored { get { return remotePosesIgnored; } }` |  |
| свойство | `Role` | `public KvNetRole Role { get { return role; } }` |  |
| свойство | `RoundTripMs` | `public float RoundTripMs { get { return roundTripMs; } }` |  |
| свойство | `TimeoutSeconds` | `public float TimeoutSeconds { get { return timeoutSeconds; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller)` |  |
| метод | `LinkStatus` | `public string LinkStatus()` | Строка состояния связи для вкладки (ФИКС 11.В): «связь есть, задержка N мс» либо «связь потеряна». Задержка измеряется по heartbeat и является задержкой «туда-обратно». |
| метод | `SendHello` | `public void SendHello()` | Текстовое приветствие (наблюдатель может «представиться»). |
| метод | `SetPeer` | `public void SetPeer(string value)` |  |
| метод | `SetPort` | `public void SetPort(int value)` |  |
| метод | `SetRole` | `public void SetRole(KvNetRole value)` | Установить роль (Off/Operator/Observer) — с перезапуском сокетов. |
| метод | `Status` | `public string Status()` |  |
| метод | `Stop` | `public void Stop()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание: отправка состояния и heartbeat или применение принятого. |

### `class KvCollisionBenchmark`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCollisionOptimizer.cs` (строка 39)
- **Назначение:** Результат замера производительности (этап 13 ТЗ: «ускорение планирования»).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `clearanceWithMs` | `public float clearanceWithMs;` |  |
| поле | `clearanceWithoutMs` | `public float clearanceWithoutMs;` |  |
| поле | `obstaclesWith` | `public int obstaclesWith;` |  |
| поле | `obstaclesWithout` | `public int obstaclesWithout;` |  |
| поле | `planCompared` | `public bool planCompared;` |  |
| поле | `planWithMs` | `public float planWithMs;` |  |
| поле | `planWithoutMs` | `public float planWithoutMs;` |  |
| поле | `rebuildWithMs` | `public float rebuildWithMs;` |  |
| поле | `rebuildWithoutMs` | `public float rebuildWithoutMs;` |  |
| поле | `samples` | `public int samples;` |  |
| метод | `Line` | `public string Line()` |  |

### `class KvCollisionOptimizer`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCollisionOptimizer.cs` (строка 86)
- **Назначение:** ЭТАП 13 ТЗ: РЕДАКТОР КОЛЛИЗИОННЫХ МЕШЕЙ. Что делает: • читает меши сцены и строит по ним НИЗКОПОЛИГОНАЛЬНЫЙ прокси столкновений: ориентированный бокс (OBB) по главным осям облака вершин (метод главных компонент, собственный решатель 3×3) либо капсулу для вытянутых тел; • прокси кладёт в реестр `C…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `EnabledPrefsKey` | `public const string EnabledPrefsKey = "KazistovVv.Collision.Proxies";` |  |
| свойство | `Benchmark` | `public KvCollisionBenchmark Benchmark { get { return benchmark; } }` |  |
| свойство | `Items` | `public IReadOnlyList<KvCollisionProxyInfo> Items { get { return items; } }` |  |
| свойство | `PreviewVisible` | `public bool PreviewVisible { get { return preview; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,` |  |
| метод | `Enabled` | `public bool Enabled` | Оптимизация включена (тумблер в настройках). |
| метод | `Measure` | `public KvCollisionBenchmark Measure()` | Замер производительности (ТЗ: «ускорение планирования»): считается число препятствий, время пересборки мира и время проверок зазора БЕЗ прокси и С ними. |
| метод | `Rebuild` | `public int Rebuild(bool force = false)` | Пересобрать прокси по текущей сцене. |
| метод | `ResetCache` | `public void ResetCache()` |  |
| метод | `SetPreview` | `public void SetPreview(bool value)` | Показать/скрыть каркас прокси в сцене. |
| метод | `Status` | `public string Status()` | Строка состояния для вкладки и свойств. |
| метод | `TogglePreview` | `public void TogglePreview()` |  |

### `class KvCollisionProxyInfo`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCollisionOptimizer.cs` (строка 10)
- **Назначение:** Сведения о прокси одного меша (этап 13 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `kind` | `public string kind = "—"; // бокс / капсула / пропущен / нет прокси` |  |
| поле | `looseSize` | `public Vector3 looseSize; // габарит (AABB), м` |  |
| поле | `looseVolume` | `public float looseVolume;` |  |
| поле | `name` | `public string name = "";` |  |
| поле | `note` | `public string note = "";` |  |
| поле | `readableMesh` | `public bool readableMesh; // удалось ли прочитать вершины меша` |  |
| поле | `renderer` | `public Renderer renderer;` |  |
| поле | `tightSize` | `public Vector3 tightSize; // размеры прокси, м` |  |
| поле | `tightVolume` | `public float tightVolume;` |  |
| поле | `triangles` | `public int triangles;` |  |
| метод | `Line` | `public string Line()` |  |
| метод | `SavedVolume01` | `public float SavedVolume01` |  |

### `class KvCollisionTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCollisionOptimizer.cs` (строка 610)
- **Назначение:** ВКЛАДКА «КОЛЛИЗИИ» (ЭТАП 13 ТЗ): включение оптимизации, таблица прокси и замер.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "collision"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("coll.title", "Коллизионные меши"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvCompanionServer`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvNetTools.cs` (строка 873)
- **Назначение:** ЭТАП 22 ТЗ: МОБИЛЬНОЕ ПРИЛОЖЕНИЕ-КОМПАНЬОН (ПЛАНШЕТ/ТЕЛЕФОН КАК ПУЛЬТ). Заготовка с рабочим каналом: по UDP принимаются текстовые команды `PING` · `STOP` · `HOME` · `SELECT 3` · `STATUS` · `RUN` · `PAUSE`, они ставятся в очередь и выполняются в главном потоке ТЕМИ ЖЕ обработчиками, что кнопки инт…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `DefaultPort` | `public const int DefaultPort = 47810;` |  |
| свойство | `Commands` | `public int Commands { get { return commands; } }` |  |
| свойство | `LastCommand` | `public string LastCommand { get { return lastCommand; } }` |  |
| свойство | `Port` | `public int Port { get { return port; } }` |  |
| свойство | `Running` | `public bool Running { get { return running; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvStageHub3 hub3)` |  |
| метод | `SetPort` | `public void SetPort(int value)` |  |
| метод | `Start` | `public bool Start()` |  |
| метод | `Stop` | `public void Stop()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание: выполнение команд в главном потоке. |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvComparison`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvComparison.cs` (строка 38)
- **Назначение:** СРАВНЕНИЕ ТРАЕКТОРИЙ (ЭТАП 6 ТЗ). Из восьми вариантов выбираются ДВЕ траектории (слоты A и B) — по ТЗ «например, чекбоксами в дереве»: дерево проекта не поддерживает чекбоксы и мультивыбор, поэтому отметка выполняется командой (кнопка в панели сравнения и пункт меню) на ВЫБРАННОМ в дереве узле тр…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `SlotA` | `public int SlotA = -1;` | Индекс траектории в слоте A (-1 — пусто). |
| поле | `SlotB` | `public int SlotB = -1;` | Индекс траектории в слоте B (-1 — пусто). |
| свойство | `CandidateCount` | `public int CandidateCount { get { return Candidates != null ? Candidates.Count : 0; } }` |  |
| свойство | `Ready` | `public bool Ready { get { return SlotA >= 0 && SlotB >= 0 && SlotA != SlotB; } }` |  |
| событие | `Changed` | `public event Action Changed;` |  |
| метод | `Activate` | `public bool Activate(int index)` | Переключиться на одну из сравниваемых траекторий (как выбор зелёным лучом). |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller)` |  |
| метод | `Mark` | `public string Mark(int index)` | Отметить траекторию в свободный слот (A, затем B, затем замена A). |
| метод | `MarkOf` | `public string MarkOf(int index)` | Буква метки для узла дерева ("" — не отмечена). |
| метод | `Metrics` | `public KvTrajMetrics Metrics(int index)` | Метрики варианта по индексу. |
| метод | `Reset` | `public void Reset()` | Сбросить отметки (новая точка — новые траектории). |
| метод | `SideBySide` | `public List<string[]> SideBySide()` | Строки «метрика слева · значение A · значение B» для панели. |
| метод | `Verdict` | `public string Verdict()` | Короткий вывод «чем отличаются» (для журнала и подсказки). |

### `class KvConstrainedPlanner`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvConstrainedPlanner.cs` (строка 30)
- **Назначение:** ЭТАП 8 ТЗ: ПЛАНИРОВАНИЕ С ОГРАНИЧЕНИЯМИ (CONSTRAINED PLANNING). Задачи из ТЗ: «двигайся, держа инструмент вертикально», «не наклоняйся больше 15°», «TCP всегда смотрит на объект X». Реализовано через IK С ДОПОЛНИТЕЛЬНЫМИ УСЛОВИЯМИ (KvToolKinematics), без переписывания ядра: 1. ПРОВЕРКА: каждая по…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `EnabledPrefsKey` | `public const string EnabledPrefsKey = "KazistovVv.Constr.Enabled";` |  |
| поле | `FilterPrefsKey` | `public const string FilterPrefsKey = "KazistovVv.Constr.Filter";` |  |
| поле | `ModePrefsKey` | `public const string ModePrefsKey = "KazistovVv.Constr.Mode";` |  |
| поле | `TiltPrefsKey` | `public const string TiltPrefsKey = "KazistovVv.Constr.Tilt";` |  |
| поле | `TolPrefsKey` | `public const string TolPrefsKey = "KazistovVv.Constr.Tolerance";` |  |
| свойство | `EvaluatedCount` | `public int EvaluatedCount { get { return evaluated; } }` |  |
| свойство | `LastAttempts` | `public int LastAttempts { get; private set; }` | Сколько проб IK выполнено в последней проекции (диагностика). |
| свойство | `LastFailed` | `public int LastFailed { get; private set; }` | Сколько сэмплов последней проекции привести не удалось (ФИКС 2). |
| свойство | `LastProjected` | `public int LastProjected { get; private set; }` | Последний результат проекции (для интерфейса и журнала). |
| свойство | `LastProjectedIndex` | `public int LastProjectedIndex { get; private set; } = -1;` | Какой вариант проектировался последним. |
| свойство | `LastResidual` | `public float LastResidual { get; private set; }` | Худший остаток после проекции, °. |
| свойство | `Profile` | `public KvOrientConstraint Profile { get { Load(); return profile; } }` | Активное ограничение (тип, направление, допуск, цель). |
| свойство | `ViolatingCount` | `public int ViolatingCount { get { return violating; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Active` | `public KvOrientConstraint Active` | Ограничение, действующее прямо сейчас (null — выключено). |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,` |  |
| метод | `Check` | `public KvToolKinematics.PlanCheck Check(TrajectoryCandidate candidate)` | Проверка ограничения для варианта (кэшируется по подписи сцены). |
| метод | `DiscardViolating` | `public bool DiscardViolating` | Отбрасывать варианты, нарушающие ограничение. |
| метод | `Enabled` | `public bool Enabled` | Ограничение включено (тумблер в настройках/вкладке). |
| метод | `Flow` | `public TrajectoryFlowController Flow() { return flow; }` |  |
| метод | `Invalidate` | `public void Invalidate()` |  |
| метод | `Project` | `public int Project(TrajectoryCandidate candidate, int index)` | ПРИВЕСТИ ТРАЕКТОРИЮ К ОГРАНИЧЕНИЮ (проекция): нарушающие сэмплы пересчитываются решателем IK с дополнительным условием, лимиты и зазоры проверяются на каждом шаге. ФИКС 2 (сходимость): для КАЖДОГО нарушающего сэмпла пробуются РАЗНЫЕ стартовые приближения — исправленный сосед, сам сэмпл, зеркально… |
| метод | `ProjectSelected` | `public int ProjectSelected()` | Привести выбранный вариант (кнопка/команда). |
| метод | `Replan` | `public int Replan(TrajectoryCandidate candidate, int index)` | ПЕРЕПЛАНИРОВАТЬ С ОГРАНИЧЕНИЕМ (ФИКС 2): сегмент строится С НУЛЯ под ограничение, а не проектируется существующий путь. Схема (ядро не меняется — только публичные методы планировщика): 1. берутся ветви целевой точки (`Planner.SolveGoalConfigs`), среди них выбирается та, что соблюдает ограничение … |
| метод | `ReplanSelected` | `public int ReplanSelected()` | Перепланировать выбранный вариант (кнопка/команда). |
| метод | `Save` | `public void Save()` |  |
| метод | `SetLookAtFromAim` | `public void SetLookAtFromAim(Vector3 point)` | Задать цель «смотреть на объект» из точки прицела. |
| метод | `StatusLine` | `public string StatusLine()` | Подпись состояния для дерева/свойств ("" — ограничение выключено). |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание: при появлении новых вариантов они проверяются один раз, в журнал идёт понятная строка. Ничего не пересчитывается каждый кадр. |
| метод | `Violates` | `public bool Violates(TrajectoryCandidate candidate)` | Вариант нарушает ограничение (для дерева, свойств и фильтра). |

### `class KvConstrainedTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvConstrainedPlanner.cs` (строка 574)
- **Назначение:** ВКЛАДКА «ПЛАНИРОВАНИЕ С ОГРАНИЧЕНИЯМИ» (ЭТАП 8 ТЗ): выбор типа ограничения, предела наклона, допуска, цели «смотреть на объект», кнопки проверки и приведения.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "constraints"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra2.T("constr.title", "Планирование с ограничениями"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvDelegateAction`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvUndoRedo.cs` (строка 19)
- **Назначение:** Готовое действие из двух делегатов (для мелких операций).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `redo` | `public Action redo;` |  |
| поле | `title` | `public string title = "действие";` |  |
| поле | `undo` | `public Action undo;` |  |
| свойство | `Title` | `public string Title { get { return title; } }` |  |
| метод | `Redo` | `public void Redo() { if (redo != null) redo(); }` |  |
| метод | `Undo` | `public void Undo() { if (undo != null) undo(); }` |  |

### `struct KvDwell`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvWaypointConstraints.cs` (строка 110)
- **Назначение:** Пауза в конкретном сэмпле маршрута (вставляется как выдержка).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `AfterSample` | `public int AfterSample;` |  |
| поле | `Label` | `public string Label;` |  |
| поле | `Seconds` | `public float Seconds;` |  |

### `class KvDynamicObstacleService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvDynamicObstacles.cs` (строка 63)
- **Назначение:** ЭТАП 7 ТЗ — ДИНАМИЧЕСКИЕ ПРЕПЯТСТВИЯ. ЧТО ДЕЛАЕТ: * создаёт ДВИЖУЩИЙСЯ объект (тележка: платформа + мачта + маркерный огонь), который едет по настраиваемому маршруту (`mode`: туда-обратно / по кругу / один проход, `speed` м/с, `pauseSeconds` на концах); * тележка — НАСТОЯЩЕЕ препятствие проекта: …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `autoStop` | `public bool autoStop = true;` |  |
| поле | `cartSize` | `public Vector3 cartSize = new Vector3(0.80f, 0.26f, 0.60f);` |  |
| поле | `checkInterval` | `public float checkInterval = 0.4f; // как часто проверять варианты траекторий, с` |  |
| поле | `circleRadius` | `public float circleRadius = 2.5f;` |  |
| поле | `discardRisky` | `public bool discardRisky = false; // true — рискованные отбрасываются, false — помечаются` |  |
| поле | `logEvents` | `public bool logEvents = true;` |  |
| поле | `mastHeight` | `public float mastHeight = 0.42f;` |  |
| поле | `mode` | `public KvObstacleMode mode = KvObstacleMode.BackAndForth;` |  |
| поле | `pathA` | `public Vector3 pathA = new Vector3(-3.0f, 0f, -21.6f);` |  |
| поле | `pathB` | `public Vector3 pathB = new Vector3(3.0f, 0f, -21.6f);` |  |
| поле | `pauseSeconds` | `public float pauseSeconds = 1.0f; // пауза на концах маршрута` |  |
| поле | `speed` | `public float speed = 0.55f; // м/с` |  |
| поле | `startPhase` | `public float startPhase = 0f; // 0..1 — где начинать` |  |
| поле | `stopDistance` | `public float stopDistance = 0.10f; // ближе — аварийная остановка, м` |  |
| поле | `stopRequest` | `public Func<bool> stopRequest;` | Аварийная остановка (передаётся хабом — это та же кнопка, что в тулбаре). |
| поле | `warningDistance` | `public float warningDistance = 0.30f; // ближе — «рискованная», м` |  |
| свойство | `Enabled` | `public bool Enabled { get { return enabled; } }` |  |
| свойство | `LastRisk` | `public string LastRisk { get; private set; }` |  |
| свойство | `Position` | `public Vector3 Position { get { return cart != null ? cart.position : pathA; } }` |  |
| свойство | `Traveled` | `public float Traveled { get { return traveled; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)` |  |
| метод | `CartPositionAt` | `public Vector3 CartPositionAt(float u)` | Позиция препятствия при фазе 0..1 (используется и прогнозом движения). |
| метод | `CartPositionIn` | `public Vector3 CartPositionIn(float seconds)` | Где препятствие будет через `seconds` секунд (прогноз для риска). |
| метод | `Dispose` | `public void Dispose()` |  |
| метод | `DistanceFromChainToCart` | `public float DistanceFromChainToCart(PoseValidator v, double[] q)` | Минимальное расстояние от цепочки звеньев робота (в позе `q` или в текущей) до габарита препятствия в его позиции `cartPos` (по умолчанию — текущей). |
| метод | `Evaluate` | `public KvRiskResult Evaluate(PlannedTrajectory plan)` | Оценить траекторию с учётом ДВИЖЕНИЯ препятствия: для каждого сэмпла берётся время `plan.Times[i]`, считается прогноз позиции препятствия и расстояние до звеньев робота в этой позе. Возвращает уровень риска и минимальное расстояние. |
| метод | `EvaluateVariants` | `public void EvaluateVariants()` | Оценить все текущие варианты траекторий потока и записать результат в журнал. Помеченные «рискованными» видны в статусе; при `discardRisky` сообщается об отбраковке. |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` |  |
| метод | `SetRoute` | `public void SetRoute(Vector3 a, Vector3 b, float newSpeed)` | Сменить маршрут (публичные параметры меняются кодом/инспектором). |
| метод | `Status` | `public string Status` | Строка состояния (статус-бар, панель, отчёт). |
| метод | `Tick` | `public void Tick(float dt)` |  |
| метод | `Toggle` | `public bool Toggle()` |  |

### `class KvEnergyModel`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTrajMath.cs` (строка 93)
- **Назначение:** УПРОЩЁННАЯ ДИНАМИКА ДЛЯ МЕТРИКИ ЭНЕРГИИ (ЭТАП 6 ТЗ). Момент сустава = инерция × ускорение + вязкое трение × скорость + удержание (масса distal-части и груза на плече от оси до TCP). Модель учебная и служит для СРАВНЕНИЯ вариантов; в интерфейсе это написано прямо в подсказке.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `distalMass` | `public float[] distalMass = { 7.0f, 5.0f, 3.0f, 1.6f, 0.8f, 0.4f };` |  |
| поле | `friction` | `public float[] friction = { 0.90f, 0.70f, 0.50f, 0.15f, 0.08f, 0.04f };` |  |
| поле | `inertia` | `public float[] inertia = { 1.40f, 1.05f, 0.70f, 0.22f, 0.10f, 0.05f };` |  |
| поле | `payloadKg` | `public float payloadKg = 1.0f;` |  |
| поле | `payloadShare` | `public float[] payloadShare = { 0.0f, 0.85f, 1.0f, 0.25f, 1.0f, 0.4f };` |  |
| метод | `Clone` | `public KvEnergyModel Clone()` |  |
| метод | `Distal` | `public float Distal(int joint)` |  |

### `class KvEnergyOptimal`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvEnergyOptimal.cs` (строка 24)
- **Назначение:** ЭТАП 6 ТЗ: ОПТИМИЗАЦИЯ ПО ЭНЕРГИИ. Симуляция ровно по ТЗ: энергия ∝ Σ |момент × угловая скорость| × время (момент — упрощённая динамика: инерция + вязкое трение + удержание груза, см. KvEnergyModel). Метрика «Энергоэффективность» — Дж и Дж/м плюс пиковая мощность: по ней видно, какой вариант бере…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `PayloadPrefsKey` | `public const string PayloadPrefsKey = "KazistovVv.Post.PayloadKg";` |  |
| событие | `Message` | `public event Action<string> Message;` |  |

### `class KvEnergyTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvEnergyOptimal.cs` (строка 287)
- **Назначение:** ВКЛАДКА «ЭНЕРГИЯ» окна-верстака (этап 6): масса груза, расчёт эко-профиля и таблица «исходная / эко» по энергии, удельной энергии, мощности и времени.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "energy"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra.T("energy.title", "Оптимизация по энергии"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvEnvironmentPreset`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs` (строка 13)
- **Назначение:** Описание окружения (этап 28 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `accent` | `public Color accent = Color.grey; // цвет разметки/технических полос` |  |
| поле | `ceiling` | `public Color ceiling = Color.grey;` |  |
| поле | `floor` | `public Color floor = Color.grey;` |  |
| поле | `hasCeiling` | `public bool hasCeiling = true;` |  |
| поле | `height` | `public float height = 4.5f;` |  |
| поле | `id` | `public string id = "";` |  |
| поле | `metallic` | `public float metallic;` |  |
| поле | `note` | `public string note = "";` |  |
| поле | `props` | `public int props; // сколько объектов обстановки добавить` |  |
| поле | `sizeX` | `public float sizeX = 14f;` |  |
| поле | `sizeZ` | `public float sizeZ = 14f;` |  |
| поле | `smoothness` | `public float smoothness = 0.25f;` |  |
| поле | `title` | `public string title = "";` |  |
| поле | `wall` | `public Color wall = Color.grey;` |  |

### `class KvEnvironmentStudio`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs` (строка 41)
- **Назначение:** ЭТАП 28 ТЗ: ПРЕСЕТЫ ОКРУЖЕНИЯ (АНГАР, ЛАБОРАТОРИЯ, ЦЕХ, ЧИСТОЕ ПОМЕЩЕНИЕ). Помещение строится из простых плоскостей (пол, четыре стены, потолок), к нему добавляются элементы обстановки: ящики, стеллаж, ограждение, разметка на полу. Всё это — ОФОРМЛЕНИЕ: коллайдеров нет, объекты служебные (`HideFl…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Index` | `public int Index { get { return index; } }` |  |
| свойство | `ObjectCount` | `public int ObjectCount { get { return root != null ? root.transform.childCount : 0; } }` | Сколько объектов построено в помещении (для проверок и свойств). |
| свойство | `Presets` | `public IList<KvEnvironmentPreset> Presets { get { return presets; } }` |  |
| свойство | `Visible` | `public bool Visible { get { return visible; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller)` |  |
| метод | `CurrentTitle` | `public string CurrentTitle` |  |
| метод | `Next` | `public void Next()` |  |
| метод | `Rebuild` | `public void Rebuild()` | Пересобрать помещение (вызывается при смене пресета/робота). |
| метод | `SetPreset` | `public void SetPreset(int value)` |  |
| метод | `SetVisible` | `public void SetVisible(bool value)` |  |
| метод | `Status` | `public string Status()` |  |

### `class KvEnvironmentTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs` (строка 1246)
- **Назначение:** ВКЛАДКА «ОКРУЖЕНИЕ» (ЭТАП 28 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "env"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("env.title", "Окружение"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvExportTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRobotExport.cs` (строка 489)
- **Назначение:** ВКЛАДКА «ЭКСПОРТ В ЯЗЫК РОБОТА» (ЭТАП 11 ТЗ): выбор языка, папка экспорта, кнопка выгрузки и предпросмотр начала файла.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "export"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra2.T("export.title", "Экспорт траектории"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvExpr`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 21)
- **Назначение:** Узел выражения (число, строка, переменная, вызов, арифметика, сравнение).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `args` | `public List<KvExpr> args;` |  |
| поле | `kind` | `public string kind = ""; // num, str, var, call, bin, not` |  |
| поле | `line` | `public int line;` |  |
| поле | `number` | `public double number;` |  |
| поле | `op` | `public string op = "";` |  |
| поле | `text` | `public string text = "";` |  |

### `class KvEyesTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvXrInput.cs` (строка 1328)
- **Назначение:** ВКЛАДКА «ВЗГЛЯД» (ЭТАП 25 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "eyes"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("eyes.title", "Взгляд и фовеальное зрение"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvEyeTrackingService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvXrInput.cs` (строка 780)
- **Назначение:** ЭТАП 25 ТЗ: ОТСЛЕЖИВАНИЕ ВЗГЛЯДА (PICO 4 Ultra) И ЦЕНТРАЛЬНОЕ (ФОВЕАЛЬНОЕ) РЕНДЕРИРОВАНИЕ. Платформенная часть — поза глаз — приходит от SDK и передаётся в `PushGaze(...)`. Здесь сделано то, что действительно можно проверить: • ФИКСАЦИИ: взгляд, удержанный в конусе 2.5° дольше 0.22 с, считается ф…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Blinks` | `public int Blinks { get { return blinks; } }` |  |
| свойство | `DwellActions` | `public int DwellActions { get { return dwellActions; } }` |  |
| свойство | `Enabled` | `public bool Enabled { get { return enabled; } }` |  |
| свойство | `FixationPoint` | `public Vector3 FixationPoint { get { return fixationPoint; } }` |  |
| свойство | `FixationPoints` | `public IList<Vector3> FixationPoints { get { return fixationPoints; } }` |  |
| свойство | `Fixations` | `public int Fixations { get { return fixations; } }` |  |
| свойство | `GazeValid` | `public bool GazeValid { get { return gazeValid; } }` |  |
| свойство | `MaskEnabled` | `public bool MaskEnabled { get { return maskEnabled; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `AverageFixation` | `public float AverageFixation` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvWaypointManager route)` |  |
| метод | `DwellProgress` | `public float DwellProgress` |  |
| метод | `PushGaze` | `public void PushGaze(Vector3 origin, Vector3 direction, float leftOpenness = 1f, float rightOpenness = 1f)` | Поза глаз от SDK шлема (или от имитатора на ПК). |
| метод | `SetDwell` | `public void SetDwell(bool value) { dwellEnabled = value; }` |  |
| метод | `SetDwellSeconds` | `public void SetDwellSeconds(float value) { dwellSeconds = Mathf.Clamp(value, 0.3f, 3f); }` |  |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` |  |
| метод | `SetMask` | `public void SetMask(bool value)` |  |
| метод | `Status` | `public string Status()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` |  |

### `enum KvFailureKind`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 14)
- **Назначение:** Вид имитируемого отказа (этап 34 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `class KvFailureSimulator`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 67)
- **Назначение:** ЭТАП 34 ТЗ: ИМИТАЦИЯ ОТКАЗОВ И ПОВЕДЕНИЕ СИСТЕМЫ БЕЗОПАСНОСТИ. ЧТО ЗДЕСЬ ЕСТЬ (и это работает): • ОТКАЗ СУСТАВА — выбранная ось теряет момент. Момент силы тяжести считается ШТАТНОЙ моделью нагрузки (`KvPayloadCalculator.JointTorques`: вес звеньев в своих центрах плюс вес груза в точке инструмента…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `inertiaKgM2` | `public float inertiaKgM2 = 20f;` |  |
| поле | `viscousFriction` | `public float viscousFriction = 140f;` |  |
| свойство | `Active` | `public bool Active { get { return kind != KvFailureKind.None; } }` |  |
| свойство | `Banner` | `public string Banner { get { return banner; } }` |  |
| свойство | `DroopRate` | `public float DroopRate { get { return droopRateDeg; } }` |  |
| свойство | `DroopSpeedDeg` | `public float DroopSpeedDeg { get { return (float)(droopOmega * Mathf.Rad2Deg); } }` | Текущая скорость провисания, °/с (для подписи в интерфейсе). |
| свойство | `DroopStopped` | `public bool DroopStopped { get { return droopStopped; } }` | Остановилось ли провисание (равновесие груза под осью или упор сустава). |
| свойство | `Events` | `public int Events { get { return events; } }` |  |
| свойство | `Holding` | `public bool Holding { get { return holding; } }` | Держится ли угол оси (потеря управления или отключение с фиксацией). |
| свойство | `Joint` | `public int Joint { get { return joint; } }` |  |
| свойство | `JointMode` | `public KvJointFailureMode JointMode { get { return jointMode; } }` |  |
| свойство | `Kind` | `public KvFailureKind Kind { get { return kind; } }` |  |
| свойство | `LastReport` | `public string LastReport { get { return lastReport; } }` |  |
| свойство | `PayloadFactor` | `public float PayloadFactor { get { return payloadFactor; } }` |  |
| свойство | `RequiresReset` | `public bool RequiresReset { get { return requiresReset; } }` |  |
| свойство | `Since` | `public float Since { get { return since; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvStageHub3 hub3)` |  |
| метод | `Clear` | `public void Clear(bool announce = true)` | Сброс аварии: снимает запрет пуска, возвращает модель нагрузки и все состояния отказа сустава. |
| метод | `JointModeLabel` | `public static string JointModeLabel(KvJointFailureMode value)` | Название состояния отказа сустава (для подписей в интерфейсе и журнала). |
| метод | `MotionAllowed` | `public bool MotionAllowed(out string reason)` | Разрешать ли запуск движения (используется проверкой перед пуском, этап 35). |
| метод | `SetDroopRate` | `public void SetDroopRate(float value)` |  |
| метод | `SetJointMode` | `public void SetJointMode(KvJointFailureMode value)` | Выбрать, что произойдёт с осью при следующем «отказе сустава» (ФИКС 11.А): провисание под весом, потеря управления или отключение с фиксацией. |
| метод | `Start` | `public void Start(KvFailureKind value, int jointNumber = 1)` | Запустить отказ. Для отказа сустава указывается номер сустава (1…N); что именно произойдёт с осью — определяет выбранный вид отказа (`SetJointMode`, ФИКС 11.А). |
| метод | `Status` | `public string Status()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` |  |

### `class KvFailureTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 1246)
- **Назначение:** ВКЛАДКА «ОТКАЗЫ» (ЭТАП 34 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "failures"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("fail.title", "Имитация отказов"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvFeatureDrag`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvFeatureWindow.cs` (строка 13)
- **Назначение:** Перетаскивание окна за заголовок.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Target` | `public RectTransform Target;` |  |
| метод | `OnBeginDrag` | `public void OnBeginDrag(PointerEventData eventData)` |  |
| метод | `OnDrag` | `public void OnDrag(PointerEventData eventData)` |  |

### `class KvFeatureWindow`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvFeatureWindow.cs` (строка 50)
- **Назначение:** ПАНЕЛЬ ФУНКЦИЙ (этапы 1–20 ТЗ) — одно плавающее окно с вкладками. Строится КОДОМ на собственном канвасе (`sortingOrder = 45`: ниже панели метрик (50) и HUD режима точки (60), выше основной оболочки), поэтому существующий интерфейс в стиле FreeCAD не перестраивается и не ломается: окно можно перет…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `EtaPanelEnabled` | `public bool EtaPanelEnabled = true;` |  |
| поле | `RecordSourcePhantom` | `public bool RecordSourcePhantom;` |  |
| поле | `refreshInterval` | `public float refreshInterval = 0.2f;` |  |
| поле | `sortingOrder` | `public int sortingOrder = 45;` |  |
| поле | `windowHeight` | `public float windowHeight = 520f;` |  |
| поле | `windowWidth` | `public float windowWidth = 760f;` |  |
| свойство | `Visible` | `public bool Visible { get { return visible; } }` |  |
| свойство | `VisibleTab` | `public int VisibleTab { get { return visible ? tab : -1; } }` |  |
| метод | `Build` | `public void Build(FeatureHub featureHub)` | Собрать окно (идемпотентно: повторный вызов ничего не ломает). |
| метод | `Hide` | `public void Hide()` |  |
| метод | `Refresh` | `public void Refresh()` | Обновление динамических подписей (частота — `refreshInterval`). |
| метод | `RefreshLanguageLabels` | `public void RefreshLanguageLabels()` | ЭТАП 2 (мультиязычность): обновить подписи окна после смены языка — заголовок и полосу вкладок. Окно не пересоздаётся, состояние и выбранная вкладка сохраняются. |
| метод | `RequestRefresh` | `public void RequestRefresh()` |  |
| метод | `RunSelectedScenario` | `public void RunSelectedScenario()` | Запустить выбранный в панели сценарий. |
| метод | `SelectedRecord` | `public KvTrajectoryRecord SelectedRecord` |  |
| метод | `Show` | `public void Show(int tabIndex)` |  |
| метод | `TabTitlesLocalized` | `public static string[] TabTitlesLocalized()` | Подписи вкладок с учётом языка интерфейса. |
| метод | `Toggle` | `public void Toggle(int tabIndex)` |  |
| метод | `ToggleEta` | `public void ToggleEta()` |  |

### `class KvFinding`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 597)
- **Назначение:** Одно замечание проверки перед пуском.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `blocks` | `public bool blocks;` |  |
| поле | `details` | `public string details = "";` |  |
| поле | `requiresConfirmation` | `public bool requiresConfirmation;` |  |
| поле | `severity` | `public KvSeverity severity;` |  |
| поле | `title` | `public string title = "";` |  |
| метод | `Describe` | `public string Describe()` |  |

### `class KvForceHeatTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvForceHeat.cs` (строка 528)
- **Назначение:** ВКЛАДКА «СИЛЫ И ВРЕМЯ» (ЭТАПЫ 17–18 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "forces"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("force.title", "Силы и моменты"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvForceVisualizer`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvForceHeat.cs` (строка 19)
- **Назначение:** ЭТАП 17 ТЗ: ВИЗУАЛИЗАЦИЯ СИЛ И МОМЕНТОВ. Момент каждого сустава считается ТОЙ ЖЕ моделью, что и калькулятор нагрузки (этап 10) — вес звеньев и груза на фактических плечах, спроецированный на ось сустава, — и рисуется стрелкой у оси: длина пропорциональна моменту, цвет — доля от номинала (зелёный …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Enabled` | `public bool Enabled { get { return enabled; } }` |  |
| свойство | `Loads` | `public IReadOnlyList<float> Loads { get { return lastLoad; } }` |  |
| свойство | `PayloadKg` | `public float PayloadKg { get { return payloadKg; } set { payloadKg = Mathf.Clamp(value, 0f, 50f); } }` |  |
| свойство | `ToolForce` | `public Vector3 ToolForce { get { return lastToolForce; } }` |  |
| свойство | `Torques` | `public IReadOnlyList<float> Torques { get { return lastTorque; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub)` |  |
| метод | `Dispose` | `public void Dispose()` |  |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` |  |
| метод | `StatusLine` | `public string StatusLine()` | Строка состояния для вкладки (моменты по осям). |
| метод | `Tick` | `public void Tick(float deltaTime)` |  |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvFoveatedRendering`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvXrInput.cs` (строка 1098)
- **Назначение:** ФОВЕАЛЬНОЕ (ЦЕНТРАЛЬНОЕ) РЕНДЕРИРОВАНИЕ ПО ВЗГЛЯДУ. Unity рисует всю картинку одинаково; экономию даёт VRS/foveated rendering, которым управляет платформа (PICO/OpenXR) через свои API. Чтобы проект не зависел от версии SDK, поиск API идёт ОТРАЖЕНИЕМ: если в сборке есть тип `FoveatedRendering` с м…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Details` | `public string Details { get { return details; } }` |  |
| свойство | `Level` | `public int Level { get { return level; } }` |  |
| свойство | `Supported` | `public bool Supported { get { return supported; } }` |  |
| метод | `Probe` | `public bool Probe()` |  |
| метод | `SetLevel` | `public bool SetLevel(int value)` | Уровень детализации: 0 — максимум, 1 — средний, 2 — экономия. |

### `class KvGraphics`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvGraphics.cs` (строка 21)
- **Назначение:** ПРОВЕРКА НАЛИЧИЯ ГРАФИКИ (для сервисов этапов 13–36). Зачем это нужно. Все новые сервисы разделены на ЛОГИКУ и ПОКАЗ: расчёт моментов, времени достижимости, распознавание жестов, макросы, дерево поведения, имитация отказов и проверка перед пуском работают без графики, а вот стрелки, меши, окна «к…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Reason` | `public static string Reason { get; private set; }` | Короткое объяснение для журнала и интерфейса. |
| метод | `Available` | `public static bool Available` | Есть ли рабочая графика (не «Null Device» и не нулевой уровень шейдеров). |
| метод | `Reset` | `public static void Reset()` | Сбросить кэш проверки (нужно, если устройство сменилось — например при старте XR). |

### `class KvGripper`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvGripper.cs` (строка 21)
- **Назначение:** ПРОСТОЙ ГРИППЕР (ЭТАП 10 ТЗ). Двухпалый захват на конце робота: два «пальца», которые раздвигаются/сдвигаются по кнопке. Собирается КОДОМ (никаких префабов и ассетов) и вешается на `tcp` робота — поэтому работает и у робота, и у SCARA (см. отчёт: у SCARA инструмент вертикальный, пальцы идут вдоль…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `closedWidth` | `public float closedWidth = 0.012f;` | Раскрытие в закрытом состоянии, м. |
| поле | `extendAxisLocal` | `public Vector3 extendAxisLocal = Vector3.down;` | Ось «вдоль инструмента» в локальных координатах TCP. По соглашению проекта (§5.8: нормаль концевой плоскости — локальная Y фланца, а инструмент вытянут вдоль локальной −Y) пальцы идут ВНИЗ по локальной −Y, поэтому значение по умолчанию `Vector3.down`. Если в вашей сборке инструмент смотрит иначе … |
| поле | `fingerLength` | `public float fingerLength = 0.055f;` | Длина пальца, м. |
| поле | `fingerThickness` | `public float fingerThickness = 0.010f;` | Толщина/ширина пальца, м. |
| поле | `mountOffset` | `public float mountOffset = 0.010f;` | Смещение основания пальцев вдоль инструмента, м. |
| поле | `openAxisLocal` | `public Vector3 openAxisLocal = Vector3.right;` | Ось раскрытия пальцев в локальных координатах TCP. |
| поле | `openWidth` | `public float openWidth = 0.075f;` | Раскрытие пальцев, м (расстояние между внутренними плоскостями). |
| поле | `speed` | `public float speed = 3.5f;` | Скорость смыкания/размыкания, 1/с (доля хода в секунду). |
| свойство | `Attached` | `public bool Attached { get { return root != null; } }` |  |
| свойство | `GraspDrop` | `public float GraspDrop { get { return mountOffset + fingerLength; } }` | Насколько точка захвата отстоит от TCP вдоль инструмента, м. |
| свойство | `Held` | `public Transform Held { get; private set; }` | Захваченный объект (null — пусто). |
| свойство | `IsOpen` | `public bool IsOpen { get { return Open01 > 0.5f; } }` |  |
| свойство | `Open01` | `public float Open01 { get; private set; }` | Раскрытие 0 (закрыт) … 1 (открыт). |
| свойство | `Width` | `public float Width { get { return Mathf.Lerp(closedWidth, openWidth, Open01); } }` | Расстояние между кончиками пальцев (для проверки «можно ли взять»). |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Attach` | `public bool Attach(RobotController robot)` | Собрать/пересобрать захват на роботе (вызывается при инициализации и смене робота). |
| метод | `Detach` | `public void Detach()` |  |
| метод | `ExtendWorld` | `public Vector3 ExtendWorld` | Направление «вдоль инструмента» в МИРОВЫХ координатах. |
| метод | `Grab` | `public bool Grab(Transform target)` | Взять объект (родителем становится точка захвата, физика выключается). |
| метод | `GraspPoint` | `public Vector3 GraspPoint` | Точка между пальцами в мире (куда «смотрит» захват). |
| метод | `Release` | `public bool Release()` | Отпустить объект (снова свободен и с физикой). |
| метод | `SetOpen` | `public void SetOpen(bool open)` | Открыть/закрыть захват (плавно). |
| метод | `Tick` | `public void Tick(float deltaTime)` | Анимация пальцев (вызывается из кадрового обновления хаба). |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvHandsTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvXrInput.cs` (строка 1280)
- **Назначение:** ВКЛАДКА «РУКИ» (ЭТАП 24 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "hands"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("hands.title", "Отслеживание рук"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvHandTrackingService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvXrInput.cs` (строка 413)
- **Назначение:** ЭТАП 24 ТЗ: ОТСЛЕЖИВАНИЕ РУК (ЖЕСТЫ: PINCH, SWIPE, GRAB). Платформенная часть (получение позы суставов) приходит от SDK шлема — SDK пишет точки в `PushJoint(...)`. Вся обработка жестов сделана здесь и работает по-настоящему: • КАСАНИЕ (pinch): сближение большого и указательного пальцев ближе 2.5 …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `JointCount` | `public const int JointCount = 6;` |  |
| поле | `Wrist` | `public const int Wrist = 0, ThumbTip = 1, IndexTip = 2, MiddleTip = 3, RingTip = 4, LittleTip = 5;` |  |
| свойство | `Enabled` | `public bool Enabled { get { return enabled; } }` |  |
| свойство | `GrabCount` | `public int GrabCount { get { return grabCount; } }` |  |
| свойство | `LastGesture` | `public string LastGesture { get { return lastGesture; } }` |  |
| свойство | `PinchCount` | `public int PinchCount { get { return pinchCount; } }` |  |
| свойство | `SwipeCount` | `public int SwipeCount { get { return swipeCount; } }` |  |
| свойство | `Visible` | `public bool Visible { get { return visible; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub)` |  |
| метод | `ClearPose` | `public void ClearPose()` |  |
| метод | `IsTracked` | `public bool IsTracked(int hand)` |  |
| метод | `LastGestureAge` | `public float LastGestureAge` |  |
| метод | `Position` | `public Vector3 Position(int hand, int joint)` |  |
| метод | `PushJoint` | `public void PushJoint(int hand, int joint, Vector3 position, float gripValue = 0f)` | Точка сустава от SDK шлема (или от имитатора на ПК). |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` |  |
| метод | `SetVisible` | `public void SetVisible(bool value)` |  |
| метод | `Status` | `public string Status()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` |  |

### `class KvHaptics`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvHaptics.cs` (строка 28)
- **Назначение:** ВИБРАЦИЯ КОНТРОЛЛЕРОВ (ЭТАП 17 ТЗ) — ЗАГОТОВКА. По ТЗ: «пока что — заготовки, активация при VR/MR». В проекте нет пакета `com.meta.*` (Meta-контур подключён через OpenXR-слой), а PICO SDK не установлен, поэтому прямой зависимости от их API здесь нет — вместо неё РЕФЛЕКСИЯ: • ищутся компоненты кон…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Enabled` | `public bool Enabled = true;` | Включена ли вибрация (по ТЗ — выключатель в настройках). |
| поле | `hardAmplitude` | `public float hardAmplitude = 0.85f;` |  |
| поле | `hardDuration` | `public float hardDuration = 0.16f;` |  |
| поле | `lightAmplitude` | `public float lightAmplitude = 0.20f;` | Амплитуды импульсов 0…1. |
| поле | `lightDuration` | `public float lightDuration = 0.04f;` | Длительности импульсов, с. |
| поле | `logUnavailable` | `public bool logUnavailable = true;` | Писать в консоль, когда вибрация недоступна. |
| поле | `mediumAmplitude` | `public float mediumAmplitude = 0.45f;` |  |
| поле | `mediumDuration` | `public float mediumDuration = 0.09f;` |  |
| свойство | `Available` | `public bool Available { get { return controllers.Count > 0; } }` | Есть ли найденный контроллер (для диагностики/панели функций). |
| свойство | `ControllerCount` | `public int ControllerCount { get { return controllers.Count; } }` |  |
| метод | `IsActive` | `public bool IsActive` | Есть ли контроллеры XR в сцене (пересканируется раз в 2 с). |
| метод | `OnEmergencyStop` | `public void OnEmergencyStop() { Pulse(1f, 0.30f, "аварийная остановка"); }` |  |
| метод | `OnError` | `public void OnError() { Pulse(KvHapticStrength.Hard, "ошибка"); }` |  |
| метод | `OnMotionStarted` | `public void OnMotionStarted() { Pulse(0.35f, 0.12f, "старт движения"); }` |  |
| метод | `OnPointSelected` | `public void OnPointSelected() { Pulse(KvHapticStrength.Light, "выбор точки"); }` |  |
| метод | `OnTrajectoryConfirmed` | `public void OnTrajectoryConfirmed() { Pulse(KvHapticStrength.Medium, "подтверждение траектории"); }` |  |
| метод | `Pulse` | `public void Pulse(KvHapticStrength strength, string source = null)` | Отправить импульс (light/medium/hard). |
| метод | `Pulse` | `public void Pulse(float amplitude, float duration, string source = null)` |  |

### `enum KvHapticStrength`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvHaptics.cs` (строка 9)
- **Назначение:** Сила вибрации (ЭТАП 17 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `class KvHealthMonitor`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvHealthMonitor.cs` (строка 46)
- **Назначение:** ЭТАП 6 ТЗ — HEALTH MONITOR (мониторинг состояния суставов). ЧТО СЧИТАЕТСЯ (симуляция поверх РЕАЛЬНЫХ углов робота, кинематика не меняется): * СКОРОСТЬ сустава — по фактическому изменению угла за кадр (у призмы SCARA — м/с); * ТОК — «пропорционально нагрузке»: холостой ток + вклад скорости + вклад…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `ambientTemperature` | `public float ambientTemperature = 28f; // температура среды, °C` |  |
| поле | `coolingPerDegree` | `public float coolingPerDegree = 0.030f; // охлаждение ∝ (T−Tсреды), 1/с` |  |
| поле | `currentPerAccel` | `public float currentPerAccel = 0.12f; // А на единицу ускорения` |  |
| поле | `currentPerSpeed` | `public float currentPerSpeed = 0.85f; // А на единицу скорости (рад/с или м/с)` |  |
| поле | `heatPerCurrent2` | `public float heatPerCurrent2 = 0.020f; // нагрев ∝ I², °C/с на А²` |  |
| поле | `historyLength` | `public int historyLength = 300; // сколько точек держим (кольцевой буфер)` |  |
| поле | `idleCurrent` | `public float idleCurrent = 0.45f; // холостой ток, А` |  |
| поле | `loadCurrent` | `public float loadCurrent = 1.10f; // добавка за удержание «вытянутой» руки, А` |  |
| поле | `logEvents` | `public bool logEvents = true;` |  |
| поле | `logInterval` | `public float logInterval = 1f; // как часто пишем строку в файл, с` |  |
| поле | `maxTemperature` | `public float maxTemperature = 90f; // предел, °C` |  |
| поле | `redrawInterval` | `public float redrawInterval = 0.25f; // как часто перерисовываем графики, с` |  |
| поле | `sampleInterval` | `public float sampleInterval = 0.05f; // как часто считаем телеметрию, с` |  |
| поле | `wearCriticalLimit` | `public float wearCriticalLimit = 1.0f; // «критический износ»` |  |
| поле | `wearPerAmpSecond` | `public float wearPerAmpSecond = 1.1e-5f; // износ за 1 А·с` |  |
| поле | `wearServiceLimit` | `public float wearServiceLimit = 0.60f; // порог «пора на обслуживание»` |  |
| поле | `windowSeconds` | `public float windowSeconds = 60f; // окно графика по времени, с` |  |
| свойство | `Current` | `public float[] Current { get { return current; } }` |  |
| свойство | `Dof` | `public int Dof { get { return dof; } }` |  |
| свойство | `Elapsed` | `public float Elapsed { get { return elapsed; } }` |  |
| свойство | `Enabled` | `public bool Enabled { get; private set; }` |  |
| свойство | `LogPath` | `public string LogPath { get { return logPath; } }` |  |
| свойство | `LogRows` | `public int LogRows { get { return logRows; } }` |  |
| свойство | `RobotName` | `public string RobotName { get; private set; }` |  |
| свойство | `Speed` | `public float[] Speed { get { return speed; } }` |  |
| свойство | `Temperature` | `public float[] Temperature { get { return temperature; } }` |  |
| свойство | `Wear` | `public float[] Wear { get { return wear; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller)` |  |
| метод | `Dispose` | `public void Dispose()` |  |
| метод | `ForecastHours` | `public float ForecastHours(bool critical, out int worstJoint)` | Прогноз ресурса: часы до порога обслуживания и до критического износа. |
| метод | `Histories` | `public List<KvHealthSample>[] Histories(int metric)` |  |
| метод | `HottestJoint` | `public int HottestJoint(out float value)` | Сустав с наибольшей температурой и его значение. |
| метод | `Rebind` | `public void Rebind(RobotController robot)` | Сменить робота: пересоздать массивы под его число осей (их 6 или 3). |
| метод | `Sample` | `public void Sample(float dt)` | Один шаг телеметрии: скорость → ток → температура → износ → история. |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` |  |
| метод | `Status` | `public string Status` | Строка состояния (статус-бар, панель, отчёт). |
| метод | `Tick` | `public void Tick(float dt)` | Кадровое обслуживание. Считается всегда, когда включено (и во время движения). |
| метод | `Toggle` | `public bool Toggle()` |  |

### `class KvHealthPanel`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvHealthMonitor.cs` (строка 402)
- **Назначение:** Панель мониторинга: четыре графика в реальном времени (температура, ток, скорость, износ) + строка прогноза ресурса. Собственный Canvas (как у окна функций), скрыт из иерархии, кликов не перехватывает — обычный интерфейс не меняется.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `panelHeight` | `public float panelHeight = 420f;` |  |
| поле | `panelWidth` | `public float panelWidth = 470f;` |  |
| поле | `refreshInterval` | `public float refreshInterval = 0.25f;` |  |
| поле | `sortingOrder` | `public int sortingOrder = 44;` |  |
| поле | `textureHeight` | `public int textureHeight = 74;` |  |
| поле | `textureWidth` | `public int textureWidth = 400;` |  |
| свойство | `Visible` | `public bool Visible { get { return visible; } }` |  |
| метод | `Build` | `public void Build(KvHealthMonitor source)` |  |
| метод | `Redraw` | `public void Redraw()` |  |
| метод | `Refresh` | `public void Refresh()` | Обновить графики (не чаще refreshInterval). |
| метод | `SetVisible` | `public void SetVisible(bool value)` |  |
| метод | `Toggle` | `public bool Toggle()` |  |

### `struct KvHealthSample`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvHealthMonitor.cs` (строка 14)
- **Назначение:** Одна точка истории телеметрии (для графиков).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `t` | `public float t; // время от начала мониторинга, с` |  |
| поле | `v` | `public float v; // значение величины` |  |

### `class KvHighlightFrame`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvOverlayKit.cs` (строка 224)
- **Назначение:** ПУЛЬСИРУЮЩАЯ РАМКА ПОДСВЕТКИ (этап 2 ТЗ: «подсветка нужных элементов»). Ставится поверх любого элемента интерфейса (в том числе на другом канвасе): положение берётся из его экранного прямоугольника.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `color` | `public Color color = new Color(1f, 0.78f, 0.2f, 1f);` |  |
| поле | `padding` | `public float padding = 3f;` |  |
| поле | `pulseSpeed` | `public float pulseSpeed = 3.4f;` |  |
| поле | `thickness` | `public float thickness = 2f;` |  |
| свойство | `IsVisible` | `public bool IsVisible { get { return visible && root != null && root.gameObject.activeSelf; } }` |  |
| метод | `Create` | `public static KvHighlightFrame Create(RectTransform canvasRect)` |  |
| метод | `SetTarget` | `public void SetTarget(RectTransform value)` |  |
| метод | `SetVisible` | `public void SetVisible(bool value)` |  |

### `class KvHintCard`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvOverlayKit.cs` (строка 322)
- **Назначение:** КАРТОЧКА-ПОДСКАЗКА (туториал и демонстрация): заголовок, счётчик шагов, полоса прогресса, текст и ряд кнопок. Живёт на своём канвасе, поэтому видна и при скрытой оболочке интерфейса.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `width` | `public float width = 520f;` |  |
| свойство | `Visible` | `public bool Visible { get { return root != null && root.gameObject.activeSelf; } }` |  |
| метод | `Create` | `public static KvHintCard Create(RectTransform canvasRect)` |  |
| метод | `Hide` | `public void Hide()` |  |
| метод | `SetBody` | `public void SetBody(string body)` |  |
| метод | `SetButtons` | `public void SetButtons(string[] labels, Action[] actions)` | Кнопки карточки. `labels` и `actions` — одинаковой длины. |
| метод | `SetFooter` | `public void SetFooter(string footer)` |  |
| метод | `SetStep` | `public void SetStep(int index, int total)` |  |
| метод | `SetTitle` | `public void SetTitle(string title)` |  |
| метод | `Show` | `public void Show()` |  |

### `class KvHoverHint`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvOverlayKit.cs` (строка 71)
- **Назначение:** Наведение на элемент: показывает всплывающую подсказку и (необязательно) отдаёт текст наружу — для строки-подсказки в стартовом меню.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `body` | `public string body = "";` |  |
| поле | `tip` | `public KvMiniTip tip;` | Общая всплывающая подсказка слоя (ставится при создании канваса). |
| поле | `title` | `public string title = "";` |  |
| метод | `OnPointerEnter` | `public void OnPointerEnter(PointerEventData eventData)` |  |
| метод | `OnPointerExit` | `public void OnPointerExit(PointerEventData eventData)` |  |

### `class KvImportedRobot`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRobotImport.cs` (строка 14)
- **Назначение:** Метка импортированной модели (этап 12): источник, состав, время импорта.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `format` | `public string format = "";` |  |
| поле | `importedAt` | `public string importedAt = "";` |  |
| поле | `jointCount` | `public int jointCount;` |  |
| поле | `linkCount` | `public int linkCount;` |  |
| поле | `note` | `public string note = "";` |  |
| поле | `sourceFile` | `public string sourceFile = "";` |  |
| поле | `standName` | `public string standName = "";` | Имя стенда, на который поставлен робот ("" — отдельный объект). |
| поле | `standRef` | `public GameObject standRef;` | Ссылка на объект стенда (сохраняется в компоненте робота по ТЗ ФИКС 6). |
| поле | `stepAxes` | `public string[] stepAxes = new string[0];` | Подписи найденных осей (для свойств и отчёта). |
| поле | `stepAxisCount` | `public int stepAxisCount;` | Сколько осей-кандидатов найдено в STEP (цилиндрические поверхности). |
| поле | `stepKinematicsNote` | `public string stepKinematicsNote = "";` | Честная строка о том, что дала кинематическая разведка STEP. |
| поле | `stepPairCount` | `public int stepPairCount;` | Сколько кинематических пар описано в файле (AP214 KINEMATIC_PAIR). |
| поле | `stepPlaneCount` | `public int stepPlaneCount;` | Сколько плоскостей-кандидатов скольжения найдено в STEP. |
| поле | `visualOnly` | `public bool visualOnly;` | Модель создана как визуальная (STEP): кинематики нет. |

### `enum KvImportPlacement`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRobotImport.cs` (строка 45)
- **Назначение:** Куда поставить импортированную модель (ФИКС 6).

_Публичных членов нет (или тип объявлен без них)._

### `class KvImportTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRobotImport.cs` (строка 1207)
- **Назначение:** ВКЛАДКА «ИМПОРТ МОДЕЛИ РОБОТА» (ЭТАП 12 ТЗ): список найденных файлов, импорт, проверка кинематики, удаление модели и подсказки по папкам.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "import"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra2.T("import.title", "Импорт модели робота"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvInputKit`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvInputKit.cs` (строка 17)
- **Назначение:** ПОЛЯ ВВОДА ТЕКСТА для новых вкладок (этапы 23, 26, 27, 32, 36 ТЗ). Во встроенном наборе виджетов (`KvWidgets`) поля ввода не было — до этих этапов интерфейс был чисто «кнопочным». Чтобы не менять общий стиль, поле рисуется теми же материалами и шрифтом `KvTheme`: фон `InputBg`, рамка отсутствует,…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Filter` | `public static KvSegmented Filter(RectTransform parent, string caption, string[] options,` | Переключатель-«радио» для набора вариантов одной строкой (для фильтров). |
| метод | `Multi` | `public static InputField Multi(RectTransform parent, string caption, string value,` | Многострочное поле (скрипты, титры, списки команд). |
| метод | `Single` | `public static InputField Single(RectTransform parent, string caption, string value,` | Однострочное поле. `onSubmit` вызывается при нажатии Enter/уходе фокуса. |

### `enum KvJointFailureMode`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 30)
- **Назначение:** Уточнение отказа сустава (ФИКС 11.А): что именно происходит с отказавшей осью. Это не отдельные отказы, а три состояния одного и того же отказа «отказ сустава», поэтому вид выбирается заранее, а запускается всё той же кнопкой «Отказ сустава».

_Публичных членов нет (или тип объявлен без них)._

### `class KvJointGraph`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvJointGraph.cs` (строка 22)
- **Назначение:** ГРАФИКИ УГЛОВ СУСТАВОВ (ЭТАП 7 ТЗ). Рисуется в отдельной панели (собственный `RawImage` + `Texture2D`, без шейдеров и ассетов): по выбранной траектории строится график изменения КАЖДОГО угла во времени, у каждого сустава своя линия своего цвета, по горизонтали — время, по вертикали — угол. Резкие…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `fontSize` | `public int fontSize = 11;` |  |
| поле | `JointColors` | `public static readonly Color[] JointColors =` | Цвета линий суставов (J1…J6). |
| поле | `textureHeight` | `public int textureHeight = 240;` |  |
| поле | `textureWidth` | `public int textureWidth = 620;` |  |
| свойство | `HasData` | `public bool HasData { get { return seriesA.Count > 0; } }` |  |
| свойство | `Root` | `public RectTransform Root { get { return root; } }` |  |
| метод | `Build` | `public void Build(RectTransform parent, float width, float height)` | Собрать панель (создаётся кодом, ассетов нет). |
| метод | `Clear` | `public void Clear()` |  |
| метод | `Redraw` | `public void Redraw()` | Полная перерисовка текстуры графика. |
| метод | `SetPlan` | `public void SetPlan(PlannedTrajectory plan, string title)` | Показать график одной траектории. |
| метод | `SetPlanPair` | `public void SetPlanPair(PlannedTrajectory a, string titleFirst, PlannedTrajectory b, string titleSecond)` | Показать два графика наложенно (опция ТЗ). |

### `class KvLightingPreset`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs` (строка 302)
- **Назначение:** Описание светового пресета (этап 29 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `bloom` | `public float bloom;` |  |
| поле | `exposureEv` | `public float exposureEv = 0f;` |  |
| поле | `fillLux` | `public float fillLux = 8000f;` |  |
| поле | `fog` | `public bool fog;` |  |
| поле | `fogColor` | `public Color fogColor = Color.grey;` |  |
| поле | `fogDistance` | `public float fogDistance = 400f;` |  |
| поле | `id` | `public string id = "";` |  |
| поле | `keyLux` | `public float keyLux = 20000f;` |  |
| поле | `note` | `public string note = "";` |  |
| поле | `rimLux` | `public float rimLux = 6000f;` |  |
| поле | `skyBottom` | `public Color skyBottom = Color.white;` |  |
| поле | `skyMiddle` | `public Color skyMiddle = new Color(0.3f, 0.7f, 1f);` |  |
| поле | `skyTop` | `public Color skyTop = Color.blue;` |  |
| поле | `sunAzimuth` | `public float sunAzimuth = 40f;` |  |
| поле | `sunColor` | `public Color sunColor = Color.white;` |  |
| поле | `sunElevation` | `public float sunElevation = 45f;` |  |
| поле | `sunLux` | `public float sunLux = 20000f;` |  |
| поле | `title` | `public string title = "";` |  |

### `class KvLightingStudio`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs` (строка 336)
- **Назначение:** ЭТАП 29 ТЗ: ПРЕСЕТЫ ОСВЕЩЕНИЯ (ДЕНЬ, НОЧЬ, СТУДИЯ, ДРАМАТИЧНЫЙ СВЕТ). Пресет меняет РЕАЛЬНЫЕ параметры сцены HDRP: • солнце (направленный источник) — цвет, яркость в люксах, положение (высота/азимут); • студийный свет проекта (`HDRPAutoLighting`: ключевой/заливающий/контровой) — яркости; • глобал…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `bloom` | `public float bloom;` |  |
| поле | `exposure` | `public float exposure;` |  |
| поле | `fog` | `public bool fog;` |  |
| поле | `fogColor` | `public Color fogColor = Color.grey;` |  |
| поле | `fogDistance` | `public float fogDistance = 400f;` |  |
| поле | `skyTop` | `public Color skyTop = Color.blue, skyMiddle = Color.cyan, skyBottom = Color.white;` |  |
| поле | `sunColor` | `public Color sunColor = Color.white;` |  |
| поле | `sunLux` | `public float sunLux;` |  |
| поле | `SunName` | `public const string SunName = "KvPresetSun";` |  |
| свойство | `Applied` | `public bool Applied { get { return applied; } }` |  |
| свойство | `Index` | `public int Index { get { return index; } }` |  |
| свойство | `Presets` | `public IList<KvLightingPreset> Presets { get { return presets; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Apply` | `public void Apply()` |  |
| метод | `Apply` | `public void Apply(float duration, float delay)` | Применить пресет: `duration` — время плавного перехода в секундах. |
| метод | `CurrentTitle` | `public string CurrentTitle` |  |
| метод | `Next` | `public void Next()` |  |
| метод | `SetPreset` | `public void SetPreset(int value, bool smooth = true)` |  |
| метод | `Status` | `public string Status()` |  |
| метод | `Sun` | `public Light Sun` | Направленный источник, которым управляет пресет (для свойств и проверок). |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание: плавный переход между пресетами. |

### `class KvLightingTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs` (строка 1290)
- **Назначение:** ВКЛАДКА «ОСВЕЩЕНИЕ» (ЭТАП 29 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "light"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("light.title", "Освещение"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvLocExtra`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvLocExtra.cs` (строка 23)
- **Назначение:** ЛОКАЛИЗАЦИЯ НОВЫХ ФУНКЦИЙ (этапы 1–6 этой сессии: стартовое меню, туториал, демонстрация, сглаживание, время-оптимальные и эко-траектории). ПОЧЕМУ СТРОКИ ЗДЕСЬ, А НЕ В ФАЙЛАХ-СЛОВАРЯХ: словари в `StreamingAssets/kazistovvv_i18n/*.json` — это данные интерфейса, которые правят без Unity; новые моду…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Codes` | `public static readonly string[] Codes = { "ru", "en", "zh", "es", "de", "fr", "ja" };` | Порядок языков в таблице (совпадает с кодами каталогов). |
| свойство | `RegisteredCount` | `public static int RegisteredCount { get { return registered; } }` | Сколько строк зарегистрировано модулем (диагностика). |
| метод | `F` | `public static string F(string key, string fallback, params object[] args)` | Перевод с подстановкой: {0}, {1} — как в string.Format. |
| метод | `Install` | `public static void Install()` | Зарегистрировать строки модуля (вызывается хабом этапов один раз). |
| метод | `T` | `public static string T(string key, string fallback)` | Перевод по ключу (fallback — русский текст из кода). |

### `class KvLocExtra2`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvLocExtra2.cs` (строка 15)
- **Назначение:** ЛОКАЛИЗАЦИЯ ЭТАПОВ 7–12 (ограничения на waypoints, планирование с ограничениями, калибровочный мастер, калькулятор нагрузки, экспорт в языки роботов, импорт URDF/STEP). Формат и правила те же, что у KvLocExtra (см. его комментарий): таблица «ключ + 7 языков» (RU / EN / ZH / ES / DE / FR / JA), ре…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `RegisteredCount` | `public static int RegisteredCount { get { return registered; } }` | Сколько строк добавлено модулем (диагностика). |
| метод | `F` | `public static string F(string key, string fallback, params object[] args)` | Перевод с подстановкой: {0}, {1} — как в string.Format. |
| метод | `Install` | `public static void Install()` | Зарегистрировать строки (вызывает хаб этапов 7–12 один раз). |
| метод | `T` | `public static string T(string key, string fallback)` | Перевод по ключу (fallback — русский текст из кода). |

### `class KvLocExtra3`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvLocExtra3.cs` (строка 15)
- **Назначение:** ЛОКАЛИЗАЦИЯ ЭТАПОВ 13–36 (коллизионные меши, бенчмарк, дерево RRT, камеры, силы, тепловая карта времени, PDF-отчёт, сеть, веб-дашборд, мобильный пульт, голос, hand/eye tracking, скрипты, деревья поведения, окружения, свет, материалы, плёночный режим, титры, голосовой комментарий, отказы, валидаци…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `RegisteredCount` | `public static int RegisteredCount { get { return registered; } }` |  |
| метод | `F` | `public static string F(string key, string fallback, params object[] args)` |  |
| метод | `Install` | `public static void Install()` |  |
| метод | `T` | `public static string T(string key, string fallback)` |  |

### `class KvLogEntry`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvActionLog.cs` (строка 33)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `kind` | `public KvLogKind kind = KvLogKind.System;` | Тип события (для фильтра). |
| поле | `stamp` | `public string stamp = "";` | Время суток чч:мм:сс. |
| поле | `text` | `public string text = "";` |  |
| поле | `time` | `public float time;` | Секунды от старта приложения (для сортировки). |
| метод | `Line` | `public string Line` |  |

### `enum KvLogKind`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvActionLog.cs` (строка 10)
- **Назначение:** Тип события журнала действий (ЭТАП 13 ТЗ) — по нему работает фильтр.

_Публичных членов нет (или тип объявлен без них)._

### `enum KvLogLevel`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 1100)
- **Назначение:** Уровень записи журнала (этап 36 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `class KvLogTools`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 1111)
- **Назначение:** ЭТАП 36 ТЗ: УРОВНИ ЖУРНАЛА, ФИЛЬТРАЦИЯ, ПОИСК И ЦВЕТА. Журнал проекта (`KvActionLog`) хранит событие с типом. Здесь типы сводятся к УРОВНЯМ «информация / предупреждение / ошибка», добавляется ПОИСК по тексту и фильтр по уровню, цвет строки берётся из типа события. Есть счётчики по уровням, автома…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `AutoScroll` | `public bool AutoScroll { get { return autoScroll; } set { autoScroll = value; } }` |  |
| свойство | `ErrorCount` | `public int ErrorCount { get { return errors; } }` |  |
| свойство | `InfoCount` | `public int InfoCount { get { return info; } }` |  |
| свойство | `MinLevel` | `public KvLogLevel MinLevel { get { return minLevel; } }` |  |
| свойство | `Query` | `public string Query { get { return query; } }` |  |
| свойство | `Shown` | `public int Shown { get { return shown; } }` |  |
| свойство | `WarningCount` | `public int WarningCount { get { return warnings; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `ExportVisible` | `public string ExportVisible()` | Выгрузить то, что видно на экране (с учётом уровня и поиска). |
| метод | `LastLine` | `public string LastLine()` |  |
| метод | `LevelLabel` | `public static string LevelLabel(KvLogLevel level)` |  |
| метод | `LevelOf` | `public static KvLogLevel LevelOf(KvLogKind kind)` | Уровень записи по типу события. |
| метод | `SetLevel` | `public void SetLevel(KvLogLevel value)` |  |
| метод | `SetQuery` | `public void SetQuery(string value)` |  |
| метод | `Status` | `public string Status()` |  |
| метод | `Tail` | `public List<KvLogEntry> Tail(int count)` | Последние `count` записей отфильтрованного списка (для панели). |
| метод | `View` | `public List<KvLogEntry> View()` | Отфильтрованный список (новые в конце). Считает счётчики по уровням. |

### `class KvLogToolsTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 1421)
- **Назначение:** ВКЛАДКА «ЖУРНАЛ: УРОВНИ И ПОИСК» (ЭТАП 36 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "logtools"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("logtools.title", "Журнал: уровни и поиск"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvMaterialEdit`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs` (строка 691)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `baseColor` | `public Color baseColor = Color.white;` |  |
| поле | `emission` | `public Color emission = Color.black;` |  |
| поле | `emissionIntensity` | `public float emissionIntensity;` |  |
| поле | `metallic` | `public float metallic;` |  |
| поле | `smoothness` | `public float smoothness = 0.5f;` |  |
| поле | `target` | `public string target = "";` |  |

### `class KvMaterialEditsFile`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs` (строка 709)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `edits` | `public List<KvMaterialEdit> edits = new List<KvMaterialEdit>();` | Правки: «имя объекта # слот» + цвет, металличность, гладкость, свечение. |
| поле | `savedAt` | `public string savedAt = "";` | Когда файл записан (ISO-время) — для человека, читающего JSON. |

### `class KvMaterialStudio`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs` (строка 737)
- **Назначение:** ЭТАП 30 ТЗ: РЕДАКТОР МАТЕРИАЛОВ В РЕЖИМЕ РЕАЛЬНОГО ВРЕМЕНИ. Выбирается объект (пипеткой из центра экрана или перебором списка видимых частей), показываются его материалы и правятся их свойства — цвет, металличность, гладкость, свечение. Правка делается на КОПИИ материала (`Renderer.materials`, а …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `EditsFileName` | `public const string EditsFileName = "materials_edits.json";` | Имя файла правок в каталоге ConfigDir. |
| свойство | `Dirty` | `public bool Dirty { get { return dirty; } }` | Есть ли несохранённые изменения (по ним и работает автосохранение при выходе). |
| свойство | `EditsPath` | `public string EditsPath { get { return Path.Combine(FeatureStorage.ConfigDir, EditsFileName); } }` | Полный путь файла правок (для подписи в интерфейсе и проверок). |
| свойство | `Index` | `public int Index { get { return index; } }` |  |
| свойство | `LastError` | `public string LastError { get { return lastError; } }` |  |
| свойство | `MaterialIndex` | `public int MaterialIndex { get { return materialIndex; } }` |  |
| свойство | `Saved` | `public IList<KvMaterialEdit> Saved { get { return saved; } }` |  |
| свойство | `SavedCount` | `public int SavedCount { get { return saved.Count; } }` | Сколько правок записано в списке (сколько уйдёт в файл при сохранении). |
| свойство | `Targets` | `public IList<Renderer> Targets { get { return targets; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Current` | `public Renderer Current` |  |
| метод | `CurrentLabel` | `public string CurrentLabel` |  |
| метод | `CurrentValues` | `public KvMaterialEdit CurrentValues()` |  |
| метод | `EditsStatus` | `public string EditsStatus()` | Строка состояния файла правок для вкладки. |
| метод | `ForgetEdits` | `public bool ForgetEdits(bool announce = true)` | Забыть сохранённые правки: файл удаляется, а материалы возвращаются к исходному виду. Без этой кнопки отказаться от сохранённых правок было бы невозможно (ФИКС 11.Б). |
| метод | `HasSavedFile` | `public bool HasSavedFile` | Есть ли на диске сохранённый файл правок. |
| метод | `LoadEdits` | `public int LoadEdits(bool announce = true)` | Вернуть сохранённые правки на место (вызывается хабом при инициализации сервиса, то есть при следующем запуске). Для каждой правки ищется объект с тем же именем и слотом материала; возвращается число применённых правок. |
| метод | `Next` | `public void Next(int delta) { Select(index + delta); }` |  |
| метод | `PickFromCenter` | `public bool PickFromCenter(Camera camera)` | Выбрать объект лучом из центра экрана (пипетка). |
| метод | `PresetHighlight` | `public void PresetHighlight()` | Быстрые готовые варианты: подсветить, сделать матовым, вернуть исходное. |
| метод | `PresetMatte` | `public void PresetMatte()` |  |
| метод | `Refresh` | `public void Refresh(Transform root)` | Обновить список доступных объектов (части роботов и стенда). |
| метод | `ResetAll` | `public void ResetAll()` |  |
| метод | `ResetCurrent` | `public void ResetCurrent()` |  |
| метод | `SaveEdits` | `public bool SaveEdits(bool announce = true)` | Сохранить правки в `materials_edits.json` (каталог `FeatureStorage.ConfigDir`). Вызывается кнопкой «Сохранить правки» и автоматически при выходе из PlayMode. |
| метод | `Select` | `public void Select(int value)` |  |
| метод | `SelectMaterial` | `public void SelectMaterial(int value)` |  |
| метод | `SetBaseColor` | `public bool SetBaseColor(Color color)` |  |
| метод | `SetEmission` | `public bool SetEmission(Color color, float intensity)` |  |
| метод | `SetMetallic` | `public bool SetMetallic(float value)` |  |
| метод | `SetSmoothness` | `public bool SetSmoothness(float value)` |  |
| метод | `Status` | `public string Status()` |  |

### `class KvMaterialTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs` (строка 1340)
- **Назначение:** ВКЛАДКА «МАТЕРИАЛЫ» (ЭТАП 30 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "material"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("mat.title", "Материалы"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvMiniPlot`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPayloadCalculator.cs` (строка 544)
- **Назначение:** МИНИ-ГРАФИК (процедурная текстура): оси, подписи, ломаная по точкам. Используется калькулятором нагрузки (этап 10) — файлов-ассетов не требуется.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `AxisTitles` | `public string AxisTitles { get { return xLabel + " / " + yLabel; } }` | Подписи осей (текстом поверх графика — отдельными Text-элементами). |
| метод | `Create` | `public static KvMiniPlot Create(RectTransform parent, string name, float width, float height)` |  |
| метод | `SetData` | `public void SetData(float[] distances, float[] payloads, string xTitle, string yTitle)` |  |

### `class KvMiniTip`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvOverlayKit.cs` (строка 102)
- **Назначение:** ВСПЛЫВАЮЩАЯ ПОДСКАЗКА СВОЕГО СЛОЯ (в стиле FreeCAD): заголовок + пояснение, появляется рядом с элементом, целиком в пределах канваса.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `maxWidth` | `public float maxWidth = 320f;` |  |
| свойство | `Visible` | `public bool Visible { get { return root != null && root.gameObject.activeSelf; } }` |  |
| метод | `Create` | `public static KvMiniTip Create(RectTransform canvasRect)` |  |
| метод | `Hide` | `public void Hide()` |  |
| метод | `Show` | `public void Show(string title, string body, RectTransform anchor)` |  |

### `class KvMotionLimits`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTrajMath.cs` (строка 25)
- **Назначение:** ОГРАНИЧЕНИЯ ДВИЖЕНИЯ (ЭТАП 5 ТЗ): максимальная скорость суставов, максимальное ускорение и максимальный jerk. Единый набор для сглаживания, время-оптимальной траектории и эко-профиля, чтобы метрики были сравнимы между собой. Значения по умолчанию — типовые для учебного 6-осевого робота и SCARA.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `maxAccDeg` | `public float maxAccDeg = 180f;` |  |
| поле | `maxAccMps2` | `public float maxAccMps2 = 0.8f;` |  |
| поле | `maxJerkDeg` | `public float maxJerkDeg = 1200f;` |  |
| поле | `maxJerkMps3` | `public float maxJerkMps3 = 6f;` |  |
| поле | `maxVelDeg` | `public float maxVelDeg = 90f;` |  |
| поле | `maxVelMps` | `public float maxVelMps = 0.35f;` |  |
| метод | `Clone` | `public KvMotionLimits Clone()` |  |

### `enum KvNetRole`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvNetTools.cs` (строка 15)
- **Назначение:** Роль узла в режиме совместной работы (этап 20 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `class KvNetTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvNetTools.cs` (строка 1087)
- **Назначение:** ВКЛАДКА «СЕТЬ И МОНИТОРИНГ» (ЭТАПЫ 20–22 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "net"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("net.title", "Сеть: совместная работа"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `enum KvObstacleMode`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvDynamicObstacles.cs` (строка 29)
- **Назначение:** Режим движения препятствия по маршруту.

_Публичных членов нет (или тип объявлен без них)._

### `class KvOrientConstraint`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvToolKinematics.cs` (строка 27)
- **Назначение:** ОПИСАНИЕ ОГРАНИЧЕНИЯ ОРИЕНТАЦИИ: используется и промежуточными точками (этап 7), и планированием с ограничениями (этап 8), и калибровкой (этап 9 — там важна ориентация при обучении точек).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `direction` | `public Vector3 direction = Vector3.down;` | Требуемое направление оси инструмента в мире (ToolDirection). |
| поле | `mode` | `public KvOrientMode mode = KvOrientMode.None;` |  |
| поле | `reference` | `public Vector3 reference = Vector3.down;` | Опорное направление (ToolVertical/MaxTilt): вертикаль мира. |
| поле | `targetPoint` | `public Vector3 targetPoint = Vector3.zero;` | Точка, на которую смотрит инструмент (LookAt). |
| поле | `tiltLimitDeg` | `public float tiltLimitDeg = 15f;` | Предел наклона, ° (MaxTilt). |
| поле | `toleranceDeg` | `public float toleranceDeg = 6f;` | Допуск соблюдения, °. |
| свойство | `Active` | `public bool Active { get { return mode != KvOrientMode.None; } }` |  |
| метод | `Clone` | `public KvOrientConstraint Clone()` |  |
| метод | `Describe` | `public string Describe()` | Текстовое описание для интерфейса, журнала и свойств. |

### `enum KvOrientMode`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvToolKinematics.cs` (строка 8)
- **Назначение:** ТИП ОГРАНИЧЕНИЯ ОРИЕНТАЦИИ ИНСТРУМЕНТА (этапы 7 и 8 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `class KvOverlayKit`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvOverlayKit.cs` (строка 22)
- **Назначение:** ИНСТРУМЕНТЫ ЭКРАННЫХ СЛОЁВ НОВЫХ ФУНКЦИЙ (этапы 1–3 ТЗ): собственный канвас, всплывающие подсказки, пульсирующая рамка подсветки и карточка-подсказка (туториал / демонстрация). ПОЧЕМУ СВОИ СЛОИ, А НЕ ОБЩИЕ: стартовое меню показывается ДО/ВМЕСТО рабочей оболочки (она на это время скрыта), а подска…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `CreateCanvas` | `public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)` | Создать отдельный экранный канвас (Screen Space Overlay) поверх оболочки. |
| метод | `ScreenRect` | `public static bool ScreenRect(RectTransform target, out Vector2 center, out Vector2 size)` | Прямоугольник элемента на экране (все канвасы проекта — Screen Space Overlay). |
| метод | `ToCanvas` | `public static Vector2 ToCanvas(RectTransform canvasRect, Vector2 screenPoint)` | Перевести точку экрана в локальные координаты канваса. |

### `class KvPathSmoothing`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPathSmoothing.cs` (строка 78)
- **Назначение:** ЭТАП 4 ТЗ: СГЛАЖИВАНИЕ ТРАЕКТОРИЙ (POST-PROCESSING ПОСЛЕ ПЛАНИРОВАНИЯ). Что делает: • после того как планировщик построил варианты, путь каждого варианта сглаживается выбранным методом (B-сплайн / Безье / фильтр Гаусса) — ползунок «Уровень сглаживания» 0…100 % задаёт силу обработки; • время перес…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `AutoPrefsKey` | `public const string AutoPrefsKey = "KazistovVv.Post.SmoothAuto";` |  |
| поле | `LevelPrefsKey` | `public const string LevelPrefsKey = "KazistovVv.Post.SmoothLevel";` |  |
| поле | `MethodPrefsKey` | `public const string MethodPrefsKey = "KazistovVv.Post.SmoothMethod";` |  |
| свойство | `AppliedCount` | `public int AppliedCount { get { return appliedCount; } }` |  |
| свойство | `ComputedCount` | `public int ComputedCount { get { return entries.Count; } }` |  |
| свойство | `LastJerkOk` | `public bool LastJerkOk { get; private set; } = true;` | Уложился ли фактический рывок в предел. |
| свойство | `LastJerkReport` | `public string LastJerkReport { get; private set; } = "";` | Отчёт о фактическом рывке последнего сглаженного пути (ФИКС 1). |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `ApplyAll` | `public int ApplyAll(bool quiet = true)` | Применить сглаживание ко ВСЕМ построенным вариантам (авторежим). |
| метод | `ApplySelected` | `public bool ApplySelected(bool quiet = false)` | Применить сглаживание к выбранному варианту (или к лучшему). |
| метод | `Auto` | `public bool Auto` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,` |  |
| метод | `CurrentEntry` | `public KvSmoothEntry CurrentEntry(out int index)` | Метрики выбранного варианта для интерфейса: опорный вариант («тот же путь, время по лимитам») считается здесь и только для ВЫБРАННОГО варианта — в авторежиме он не нужен, а стоит он лишних расчётов на каждый из 8 вариантов. |
| метод | `EnsureBaseline` | `public void EnsureBaseline(TrajectoryCandidate candidate, KvSmoothEntry entry = null)` | Посчитать ОПОРНЫЙ вариант («тот же путь, время пересчитано по лимитам»), с которым сравнивается сглаживание. Считается один раз на вариант и только по запросу интерфейса/свойств — в авторежиме лишней работы не делается. |
| метод | `EntryOf` | `public KvSmoothEntry EntryOf(TrajectoryCandidate candidate)` | Метрики варианта (кэшируются; исходные считаются один раз). |
| метод | `Flow` | `public TrajectoryFlowController Flow() { return flow; }` | Поток этапов, к которому привязан сервис (для вкладок верстака). |
| метод | `Level` | `public float Level` | Уровень сглаживания, % (0 — выключено, 100 — максимальная обработка). |
| метод | `Limits` | `public KvMotionLimits Limits` |  |
| метод | `Method` | `public KvSmoothMethod Method` |  |
| метод | `MethodLabel` | `public string MethodLabel` |  |
| метод | `Preview` | `public PlannedTrajectory Preview(TrajectoryCandidate candidate)` | Рассчитать сглаженный план варианта БЕЗ применения (для предпросмотра метрик) и заполнить «после». |
| метод | `ResetCache` | `public void ResetCache()` | Сбросить кэш метрик (после смены робота). |
| метод | `ResetSelected` | `public bool ResetSelected()` | Вернуть исходный (несглаженный) план выбранного варианта. |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание: если включено «применять автоматически», сразу после планирования (появление новых вариантов) все они сглаживаются один раз. |

### `class KvPayloadCalculator`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPayloadCalculator.cs` (строка 78)
- **Назначение:** ЭТАП 10 ТЗ: КАЛЬКУЛЯТОР НАГРУЗКИ (PAYLOAD CALCULATOR). Что считает: • МАКСИМАЛЬНУЮ НАГРУЗКУ в ТЕКУЩЕЙ ПОЗЕ: для каждого сустава берётся момент от веса груза (масса × g × плечо до TCP, спроецированный на ось сустава) плюс вклад веса звеньев, и сравнивается с номинальным моментом с коэффициентом за…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `SafetyPrefsKey` | `public const string SafetyPrefsKey = "KazistovVv.Payload.Safety";` |  |
| поле | `ToolMassPrefsKey` | `public const string ToolMassPrefsKey = "KazistovVv.Payload.ToolMass";` |  |
| свойство | `CurveSamples` | `public int CurveSamples { get { return curveSamples; } set { curveSamples = Mathf.Clamp(value, 4, 40); } }` |  |
| свойство | `Last` | `public KvPayloadResult Last { get { return last; } }` |  |
| свойство | `Model` | `public KvPayloadModel Model { get { Load(); return model; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)` |  |
| метод | `Evaluate` | `public KvPayloadResult Evaluate(double[] q)` | Пересчитать нагрузку для конкретной позы (используется и вкладкой, и свойствами). |
| метод | `Flow` | `public TrajectoryFlowController Flow() { return flow; }` |  |
| метод | `JointTorques` | `public bool JointTorques(double[] q, float payloadKg, out float[] torque,` | МОМЕНТЫ НА ОСЯХ В ТЕКУЩЕЙ ПОЗЕ (используется визуализацией сил, этап 17 ТЗ): для вращательных суставов — момент от веса звеньев и груза, спроецированный на ось сустава (со знаком, Н·м); для призматических — усилие вдоль оси (Н). Дополнительно выдаётся сила на инструменте (вес груза и захвата, Н) … |
| метод | `Save` | `public void Save()` |  |
| метод | `StatusLine` | `public string StatusLine()` | Строка метрики для свойств робота и статус-бара. |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание: пересчёт при заметном изменении позы (не каждый кадр). |

### `class KvPayloadModel`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPayloadCalculator.cs` (строка 37)
- **Назначение:** Модель привода: номинальные моменты (Н·м) и массы звеньев (кг).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `linkMass` | `public float[] linkMass = { 9.0f, 6.5f, 3.6f, 1.5f, 0.8f, 0.4f };` | Масса, приводимая к суставу (звенья, кг). |
| поле | `rating` | `public float[] rating = { 95f, 95f, 55f, 16f, 11f, 7f };` | Номинальный момент сустава (Н·м) либо усилие призматической оси (Н). |
| поле | `ratingPrismaticN` | `public float ratingPrismaticN = 600f;` | Номинальное усилие призматической оси (Н): у SCARA призма держит вес руки. |
| поле | `safety` | `public float safety = 1.5f;` | Коэффициент запаса (ТЗ: инженерная оценка). |
| поле | `toolMassKg` | `public float toolMassKg = 0.4f;` | Масса самого захвата/инструмента, кг (считается как часть нагрузки). |
| метод | `CopyFrom` | `public void CopyFrom(KvPayloadModel other)` |  |

### `class KvPayloadResult`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPayloadCalculator.cs` (строка 11)
- **Назначение:** Результат расчёта нагрузки (ЭТАП 10 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `distances` | `public float[] distances; // кривая: расстояние от базы, м` |  |
| поле | `jointLoad` | `public float[] jointLoad; // доля использования каждого сустава при 1 кг, 0..∞` |  |
| поле | `leverM` | `public float leverM; // плечо до TCP у ограничивающего сустава, м` |  |
| поле | `limitingJoint` | `public int limitingJoint = -1; // какой сустав ограничивает` |  |
| поле | `limitingRating` | `public float limitingRating; // номинальное значение этого сустава` |  |
| поле | `limitingValue` | `public float limitingValue; // момент (Н·м) или сила (Н) в limitingJoint` |  |
| поле | `maxKg` | `public float maxKg; // максимальная нагрузка в текущей позе, кг` |  |
| поле | `payloads` | `public float[] payloads; // кривая: нагрузка, кг` |  |
| поле | `tcpDistance` | `public float tcpDistance; // расстояние TCP от базы, м` |  |
| поле | `valid` | `public bool valid;` |  |
| поле | `why` | `public string why = "";` |  |
| метод | `Line` | `public string Line()` |  |

### `class KvPayloadTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPayloadCalculator.cs` (строка 434)
- **Назначение:** ВКЛАДКА «КАЛЬКУЛЯТОР НАГРУЗКИ» (ЭТАП 10 ТЗ): максимальная нагрузка в текущей позе, график зависимости от расстояния до базы и настройка модели приводов.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "payload"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra2.T("payload.title", "Калькулятор нагрузки"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { UpdatePlot(false); }` |  |
| метод | `Tick` | `public void Tick() { UpdatePlot(false); }` |  |

### `class KvPdfWriter`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvReportPdf.cs` (строка 24)
- **Назначение:** МИНИ-ГЕНЕРАТОР PDF (ЭТАП 19 ТЗ): пишет корректный PDF 1.4 вручную — без внешних библиотек. Поддерживает страницы с текстом (встроенный шрифт Helvetica) и страницы с изображением JPEG (кадры сцены и отрендеренные страницы отчёта). ПОЧЕМУ ТЕКСТ ЛАТИНИЦЕЙ: у встроенных шрифтов PDF (base-14) нет кири…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `caption` | `public string caption = "";` |  |
| поле | `imageHeight` | `public int imageHeight;` |  |
| поле | `imageWidth` | `public int imageWidth;` |  |
| поле | `jpeg` | `public byte[] jpeg;` |  |
| поле | `lines` | `public List<string> lines = new List<string>();` |  |
| поле | `title` | `public string title = "";` |  |
| свойство | `HasImage` | `public bool HasImage { get { return jpeg != null && jpeg.Length > 0; } }` |  |
| свойство | `PageCount` | `public int PageCount { get { return pages.Count; } }` |  |
| метод | `AddImagePage` | `public void AddImagePage(byte[] jpeg, int width, int height, string caption = "")` | Страница с изображением JPEG (кадр сцены или отрендеренная страница). |
| метод | `AddTextPage` | `public void AddTextPage(string title, IList<string> lines, float fontSize = 11f)` | Страница с текстом (строки — латиницей: см. Latin()). |
| метод | `Finish` | `public byte[] Finish(string title)` | Собрать файл PDF. |
| метод | `Latin` | `public static string Latin(string text)` | Транслитерация кириллицы (у встроенных шрифтов PDF её нет). |

### `enum KvPendantAttach`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTeachPendant.cs` (строка 19)
- **Назначение:** Как пульт закреплён в сцене.

_Публичных членов нет (или тип объявлен без них)._

### `enum KvPendantJog`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTeachPendant.cs` (строка 27)
- **Назначение:** Режим ручного управления джойстиком.

_Публичных членов нет (или тип объявлен без них)._

### `class KvPendantStick`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTeachPendant.cs` (строка 823)
- **Назначение:** Обработчик захвата/перетаскивания джойстика (uGUI, работает и в World Space).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Init` | `public void Init(KvTeachPendant pendant, RectTransform rect)` |  |
| метод | `OnDrag` | `public void OnDrag(PointerEventData eventData)` |  |
| метод | `OnPointerDown` | `public void OnPointerDown(PointerEventData eventData)` |  |
| метод | `OnPointerUp` | `public void OnPointerUp(PointerEventData eventData)` |  |

### `enum KvPendantVariant`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTeachPendant.cs` (строка 12)
- **Назначение:** Вариант оформления пульта (ТЗ: минимум два).

_Публичных членов нет (или тип объявлен без них)._

### `class KvPickAndPlace`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvGripper.cs` (строка 240)
- **Назначение:** PICK-AND-PLACE ДЕМО (ЭТАП 11 ТЗ). На столешнице появляется куб; робот берёт его и перекладывает в другую точку. Последовательность: подъезд → опускание → захват → подъём → перенос → опускание → отпускание → отход. Каждый шаг — обычная траектория, построенная общими средствами (`KvPlanKit` + штатн…

_Публичных членов нет (или тип объявлен без них)._

### `class KvPiPWindow`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCameras.cs` (строка 11)
- **Назначение:** Одно окно «картинки в картинке» (этап 16 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `camera` | `public Camera camera;` |  |
| поле | `height` | `public float height = 150f;` |  |
| поле | `id` | `public string id = "";` |  |
| поле | `image` | `public RawImage image;` |  |
| поле | `root` | `public RectTransform root;` |  |
| поле | `texture` | `public RenderTexture texture;` |  |
| поле | `title` | `public string title = "";` |  |
| поле | `visible` | `public bool visible;` |  |
| поле | `width` | `public float width = 260f;` |  |

### `class KvPlanKit`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPoseLibrary.cs` (строка 325)
- **Назначение:** Общий «конструктор планов» для новых функций (позы, pick-and-place, сценарии): • MakeJointPlan — плавный путь в пространстве суставов с честными зазором и запасом лимитов (его проверяет штатный `SafetyGate`); • SolvePoseForPoint — поза для точки TCP с выравниванием инструмента через публичный `To…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `MakeJointPlan` | `public static TrajectoryCore.PlannedTrajectory MakeJointPlan(TrajectoryCore.PoseValidator v,` | Плавный план «из текущей позы в целевую» (smoothstep по суставам). |
| метод | `SolvePoseForPoint` | `public static bool SolvePoseForPoint(TrajectoryCore.PoseValidator v,` | Поза, при которой TCP стоит в точке , а инструмент — вдоль (для 6-осевого — через `ToolAlign`, у SCARA ось инструмента вертикальна сама). Возвращает false, если IK не сошлась. |

### `class KvPlannerLab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPlannerLab.cs` (строка 89)
- **Назначение:** ЭТАП 14 ТЗ: БЕНЧМАРК ПЛАНИРОВЩИКА и ЭТАП 15 ТЗ: ВИЗУАЛИЗАЦИЯ ДЕРЕВА RRT. БЕНЧМАРК: прогоняет N задач (по умолчанию 100) — каждая задача это случайная достижимая цель вокруг базы робота; для каждой цели запускаются три стратегии: • BiRRT — штатный `Planner.PlanToGoal` (как в 8 траекториях); • RRT*…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `StrategiesPrefsKey` | `public const string StrategiesPrefsKey = "KazistovVv.Bench.Strategies";` |  |
| поле | `TasksPrefsKey` | `public const string TasksPrefsKey = "KazistovVv.Bench.Tasks";` |  |
| свойство | `LastReport` | `public string LastReport { get; private set; } = "";` |  |
| свойство | `Progress` | `public int Progress { get { return cursor; } }` |  |
| свойство | `Results` | `public IReadOnlyList<KvBenchResult> Results { get { return results; } }` |  |
| свойство | `Running` | `public bool Running { get { return running; } }` |  |
| свойство | `Total` | `public int Total { get { return totalTasks; } }` |  |
| свойство | `TreeVisible` | `public bool TreeVisible { get { return treeVisible; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,` |  |
| метод | `Export` | `public string Export()` | Выгрузить таблицу результатов в CSV и JSON (для защиты/презентации). |
| метод | `LastStrategyLabel` | `public string LastStrategyLabel` | Подпись стратегии последнего прогона (для свойств варианта траектории и дерева моделей). Если прогонов ещё не было — возвращается «—». |
| метод | `SetTreeVisible` | `public void SetTreeVisible(bool value)` | Показать/скрыть дерево RRT (ТЗ этапа 15: включается кнопкой). |
| метод | `Start` | `public bool Start()` | Начать прогон бенчмарка (N задач × 3 стратегии). |
| метод | `Status` | `public string Status()` | Строка состояния для вкладки. |
| метод | `Stop` | `public void Stop()` |  |
| метод | `TasksPerRun` | `public int TasksPerRun` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание: порция задач за кадр (прогон не морозит кадр). |
| метод | `ToggleTree` | `public void ToggleTree()` |  |

### `class KvPlannerLabTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPlannerLab.cs` (строка 528)
- **Назначение:** ВКЛАДКА «БЕНЧМАРК И ДЕРЕВО RRT» (ЭТАПЫ 14–15 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "planner"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("bench.title", "Бенчмарк планировщика"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvPlannerPerformance`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPlannerPerformance.cs` (строка 40)
- **Назначение:** ЗАМЕР ПРОИЗВОДИТЕЛЬНОСТИ ПЛАНИРОВЩИКА (ЭТАП 14 ТЗ). Считает и показывает по КАЖДОМУ прогону генерации: • сколько времени ушло на генерацию вариантов траекторий; • сколько было попыток планирования и сколько итераций у планировщика (IK/RRT); • сколько конфигураций IK нашлось и сколько из них годны…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `historySize` | `public int historySize = 24;` | Сколько прогонов держать в истории. |
| свойство | `Generating` | `public bool Generating { get { return flow != null && flow.Generating; } }` |  |
| свойство | `History` | `public IReadOnlyList<KvPlanRun> History { get { return history; } }` |  |
| свойство | `Last` | `public KvPlanRun Last { get { return history.Count > 0 ? history[history.Count - 1] : null; } }` |  |
| событие | `RunFinished` | `public event Action<KvPlanRun> RunFinished;` |  |
| метод | `AverageSeconds` | `public float AverageSeconds()` | Среднее время генерации по истории (0 — данных нет). |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller)` |  |
| метод | `CurrentSeconds` | `public float CurrentSeconds` | Сколько секунд уже идёт генерация (0 — не идёт). |
| метод | `Lines` | `public List<string> Lines()` | Строки для панели метрик/производительности. |
| метод | `Summary` | `public static string Summary(KvPlanRun run)` | Строка отчёта по прогону (для панели и журнала). |
| метод | `Tick` | `public void Tick()` | Кадровое наблюдение за генерацией (вызывает хаб). |

### `enum KvPlanRisk`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvDynamicObstacles.cs` (строка 10)
- **Назначение:** Оценка риска траектории из-за движущегося препятствия (ЭТАП 7 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `class KvPlanRun`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPlannerPerformance.cs` (строка 10)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `attempts` | `public int attempts; // попыток планирования` |  |
| поле | `created` | `public string created = "";` |  |
| поле | `duplicates` | `public int duplicates; // отфильтровано дубликатов` |  |
| поле | `failures` | `public int failures; // попыток без пути` |  |
| поле | `ikFound` | `public int ikFound; // конфигураций IK найдено` |  |
| поле | `ikUsable` | `public int ikUsable; // из них прошли лимиты` |  |
| поле | `plannerIterations` | `public int plannerIterations; // итераций последней попытки BiRRT` |  |
| поле | `produced` | `public int produced; // сколько получилось на самом деле` |  |
| поле | `requested` | `public int requested; // сколько вариантов запрошено (trajectoryCount)` |  |
| поле | `robot` | `public string robot = "";` |  |
| поле | `seconds` | `public float seconds; // сколько заняла генерация` |  |

### `class KvPlanRunList`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPlannerPerformance.cs` (строка 193)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `runs` | `public List<KvPlanRun> runs = new List<KvPlanRun>();` |  |
| поле | `version` | `public int version = 1;` |  |

### `enum KvPlanStrategy`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPlannerLab.cs` (строка 12)
- **Назначение:** Стратегия планирования для бенчмарка (этап 14 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `class KvPoseLibrary`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPoseLibrary.cs` (строка 125)
- **Назначение:** ПРЕДУСТАНОВЛЕННЫЕ ПОЗЫ (ЭТАП 2 ТЗ). «Сохранить текущую позу как…» — снимает углы суставов активного робота и пишет JSON. «Перейти в позу X» — строит траекторию в пространстве суставов до этой позы (планировщиком, если он нашёл путь, иначе плавной интерполяцией с проверкой лимитов) и отдаёт её шта…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `fallbackSpeedMps` | `public float fallbackSpeedMps = 0.08f;` | Скорость «плавного» переезда в позу, если планировщик не нашёл путь. |
| свойство | `All` | `public IReadOnlyList<KvPosePreset> All { get { return poses; } }` |  |
| свойство | `Count` | `public int Count { get { return poses.Count; } }` |  |
| событие | `Changed` | `public event Action Changed;` |  |
| событие | `Failed` | `public event Action<string> Failed;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller)` |  |
| метод | `BindWorld` | `public void BindWorld(CollisionWorld collisionWorld)` |  |
| метод | `BuildJointPlan` | `public TrajectoryCore.PlannedTrajectory BuildJointPlan(double[] start, double[] goal,` | Плавный путь в пространстве суставов (страховка, когда планировщик не нашёл путь): линейная интерполяция с профилем разгона/торможения, проверка лимитов на каждом сэмпле. |
| метод | `Delete` | `public bool Delete(KvPosePreset pose)` |  |
| метод | `ForCurrentRobot` | `public List<KvPosePreset> ForCurrentRobot()` | Позы текущего робота потока (для дерева моделей). |
| метод | `MoveTo` | `public string MoveTo(KvPosePreset pose)` | «Перейти в позу X»: построить траекторию до позы и запустить движение. Возвращает описание результата (для журнала/статуса). |
| метод | `Reload` | `public void Reload()` |  |
| метод | `SaveCurrent` | `public KvPosePreset SaveCurrent(string name, string notes = "")` | Сохранить текущую позу робота под именем . |

### `class KvPosePreset`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPoseLibrary.cs` (строка 11)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `created` | `public string created = "";` |  |
| поле | `filePath` | `[NonSerialized] public string filePath = "";` |  |
| поле | `id` | `public string id = "";` |  |
| поле | `name` | `public string name = "Поза";` |  |
| поле | `notes` | `public string notes = "";` |  |
| поле | `q` | `public float[] q = new float[0];` | Углы суставов, град (для SCARA ось Z — призматическая, как у робота). |
| поле | `robot` | `public string robot = "";` | Имя робота, для которого сохранена поза (у SCARA и робота свои наборы). |
| поле | `tcp` | `public float[] tcp = new float[0];` | TCP в момент сохранения (справочно, для свойств и подсказки). |
| поле | `version` | `public int version = 1;` |  |
| метод | `Normalize` | `public void Normalize()` |  |
| метод | `TcpVector` | `public Vector3 TcpVector` |  |
| метод | `ToDoubles` | `public double[] ToDoubles()` |  |
| метод | `Tooltip` | `public string Tooltip` |  |

### `class KvPoseStore`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPoseLibrary.cs` (строка 64)
- **Назначение:** Хранилище поз (по одному файлу на позу — копируются и правятся руками).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Extension` | `public const string Extension = ".json";` |  |
| поле | `SuggestedNames` | `public static readonly string[] SuggestedNames =` | Имена, предложенные ТЗ (быстрый выбор при сохранении). |
| метод | `Delete` | `public static bool Delete(KvPosePreset pose)` |  |
| метод | `LoadAll` | `public static List<KvPosePreset> LoadAll()` |  |
| метод | `Save` | `public static string Save(KvPosePreset pose)` |  |

### `class KvPreRunValidator`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 632)
- **Назначение:** ЭТАП 35 ТЗ: ПРОВЕРКА ПЕРЕД ПУСКОМ И ПОДТВЕРЖДЕНИЕ ОПЕРАТОРОМ. Перед запуском движения проверяется выбранная траектория целиком (по сэмплам плана): • ЗАЗОР до препятствий по всей траектории; • БЛИЗОСТЬ К ЧЕЛОВЕКУ — самый важный пункт ТЗ: считается расстояние от звеньев робота на каждом сэмпле до ч…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `ConfirmSeconds` | `public const float ConfirmSeconds = 20f;` |  |
| поле | `PersonCriticalDistance` | `public const float PersonCriticalDistance = 0.7f; // м: ближе — критично` |  |
| поле | `PersonWarningDistance` | `public const float PersonWarningDistance = 1.2f; // м: ближе — предупреждение` |  |
| свойство | `BlockedRuns` | `public int BlockedRuns { get { return blockedRuns; } }` |  |
| свойство | `Cancellations` | `public int Cancellations { get { return cancellations; } }` |  |
| свойство | `Confirmations` | `public int Confirmations { get { return confirmations; } }` |  |
| свойство | `Countdown` | `public float Countdown { get { return Mathf.Max(0f, confirmTimer); } }` |  |
| свойство | `Findings` | `public IList<KvFinding> Findings { get { return findings; } }` |  |
| свойство | `LastSummary` | `public string LastSummary { get { return lastSummary; } }` |  |
| свойство | `Pending` | `public bool Pending { get { return pending; } }` |  |
| свойство | `ValidationRuns` | `public int ValidationRuns { get { return validationRuns; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvStageHub3 hub3,` |  |
| метод | `Cancel` | `public void Cancel()` |  |
| метод | `ClosestPersonDistance` | `public float ClosestPersonDistance` |  |
| метод | `Confirm` | `public void Confirm()` |  |
| метод | `RequestRun` | `public bool RequestRun(Action start)` | Запуск движения «через проверку»: либо выполняет немедленно, либо показывает окно подтверждения (возвращает false — движение ещё не начато). |
| метод | `Tick` | `public void Tick(float deltaTime)` | Обратный отсчёт окна подтверждения (вызывать каждый кадр). |
| метод | `Validate` | `public List<KvFinding> Validate()` |  |

### `class KvPresentationMode`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPresentationMode.cs` (строка 20)
- **Назначение:** ПРЕЗЕНТАЦИОННЫЙ РЕЖИМ (ЭТАП 19 ТЗ). Камера сама облетает робота по кругу, на экране идёт текст-рассказ, весь интерфейс скрыт. Выход — Esc или кнопка в углу (управление в режиме: ПРОБЕЛ — следующий кадр, ←/→ — предыдущий/следующий, Esc — выход). ВАЖНО про совместимость: `FreeFlyCameraController` Н…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `height` | `public float height = 1.9f;` | Высота камеры, м (над базой робота). |
| поле | `KeyNext` | `public const string KeyNext = "SPACE — следующий кадр · ← / → — перелистывание · Esc — выход";` |  |
| поле | `lineSeconds` | `public float lineSeconds = 6f;` | Длительность одного «кадра» рассказа, с. |
| поле | `orbitSpeed` | `public float orbitSpeed = 12f;` | Скорость облёта, градусов в секунду. |
| поле | `radius` | `public float radius = 3.2f;` | Радиус облёта, м. |
| свойство | `Active` | `public bool Active { get { return active; } }` |  |
| свойство | `LineCount` | `public int LineCount { get { return narration.Count; } }` |  |
| свойство | `LineIndex` | `public int LineIndex { get { return line; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(Camera mainCamera, TrajectoryFlowController controller,` |  |
| метод | `CurrentLine` | `public string CurrentLine` |  |
| метод | `DefaultNarration` | `public static List<string> DefaultNarration()` | Текст по умолчанию: если хаб не задал рассказ, показ всё равно осмысленный. |
| метод | `Enter` | `public bool Enter()` | Включить презентационный режим. |
| метод | `Exit` | `public void Exit()` | Выйти из режима (Esc или кнопка). |
| метод | `Next` | `public void Next()` | Следующий кадр рассказа. |
| метод | `Previous` | `public void Previous()` |  |
| метод | `SetNarration` | `public void SetNarration(List<string> lines)` | Текст рассказа (по одному пункту на «кадр»). |
| метод | `Tick` | `public void Tick(float deltaTime, bool nextPressed, bool prevPressed)` | Ведение камеры (вызывать из LateUpdate хаба). |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvQuickStart`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvQuickStart.cs` (строка 28)
- **Назначение:** ЭТАП 3 ТЗ: QUICK START («Показать демо» в главном меню). Автоматический сценарий показывает полный цикл работы платформы, пока оператор просто смотрит: точка над столешницей → планировщик строит 8 вариантов → по каждой траектории идут фантомы → выбирается лучший вариант → робот едет по траектории…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Done` | `public Func<bool> Done;` |  |
| поле | `Enter` | `public Action Enter;` |  |
| поле | `finalPause` | `public float finalPause = 4f; // пауза на «готово», с` |  |
| поле | `MaxTime` | `public float MaxTime;` |  |
| поле | `MinTime` | `public float MinTime;` |  |
| поле | `pointHeight` | `public float pointHeight = 0.10f; // точка над столешницей, м` |  |
| поле | `TextKey` | `public string TextKey;` |  |
| поле | `waitForMotion` | `public float waitForMotion = 240f; // сколько ждать проезда робота, с` |  |
| поле | `waitForPlanning` | `public float waitForPlanning = 75f; // сколько ждать 8 вариантов, с` |  |
| поле | `waitForRobot` | `public float waitForRobot = 25f; // сколько ждать привязки робота, с` |  |
| свойство | `Running` | `public bool Running { get { return running; } }` |  |
| свойство | `StepCount` | `public int StepCount { get { return steps.Count; } }` |  |
| свойство | `StepIndex` | `public int StepIndex { get { return index; } }` |  |
| свойство | `TotalTime` | `public float TotalTime { get { return totalTime; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Begin` | `public bool Begin()` | Запустить демонстрацию (кнопка «Показать демо»). ВАЖНО: метод назван `Begin`, а НЕ `Start`: у MonoBehaviour `Start` — служебное сообщение Unity, и движок вызвал бы его сам при первом кадре (без привязки к потоку этапов, то есть на пустом сценарии). |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, KvTutorial tutorialOverlay)` |  |
| метод | `StopDemo` | `public void StopDemo(string why)` | Остановить демонстрацию (кнопка или Esc). |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание сценария. |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvReachabilityHeatmap`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvHeatmaps.cs` (строка 24)
- **Назначение:** ТЕПЛОВАЯ КАРТА ДОСТИЖИМОСТИ (ЭТАП 8 ТЗ). Вокруг робота строится поверхность (сфера/эллипсоид для робота, КОЛЬЦО для SCARA), окрашенная по «стоимости» достижения точки: у центра — зелёный (легко), у края — красный (на пределе). Стоимость берётся у НАСТОЯЩЕГО оракула достижимости (`ReachabilityOrac…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `alpha` | `public float alpha = 0.30f;` | Прозрачность карты. |
| поле | `radialWeight` | `public float radialWeight = 0.3f;` | Вес радиального градиента (0 — только вердикт оракула, 1 — только радиус). |
| поле | `samplesPerFrame` | `public int samplesPerFrame = 64;` | Сколько запросов к оракулу делать за кадр при построении. |
| поле | `textureHeight` | `public int textureHeight = 48;` |  |
| поле | `textureWidth` | `public int textureWidth = 96;` | Разрешение карты (текселей по горизонтали/вертикали). |
| свойство | `Building` | `public bool Building { get; private set; }` | Готово ли построение (false — карта ещё считается по кадрам). |
| свойство | `BuiltCount` | `public int BuiltCount { get; private set; }` | Сколько текстур посчитано (диагностика). |
| свойство | `BuiltRadius` | `public float BuiltRadius { get; private set; }` | Радиус построенной поверхности (м). |
| свойство | `IsVisible` | `public bool IsVisible { get { return Visible; } }` |  |
| свойство | `LastSummary` | `public string LastSummary { get; private set; }` | Точка поверхности в мире по индексу тексела (для диагностики/подсказки). |
| свойство | `Visible` | `public bool Visible { get; private set; }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)` |  |
| метод | `CostColor` | `public Color CostColor(float cost)` | Палитра «легко → на пределе»: зелёный → жёлтый → красный. |
| метод | `Dispose` | `public void Dispose()` |  |
| метод | `RequestRebuild` | `public void RequestRebuild(bool force)` | Конфигурация робота изменилась — пересчитать карту. |
| метод | `SetVisible` | `public void SetVisible(bool value)` | Включить/выключить карту (кнопка тулбара). |
| метод | `Tick` | `public void Tick()` | Один шаг построения (вызывается из кадрового обновления хаба). |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvRecordingService`

- **Файл:** `Assets\_Project\01_Scripts\Recording\KvRecordingService.cs` (строка 25)
- **Назначение:** ЗАПИСЬ И ВОСПРОИЗВЕДЕНИЕ ТРАЕКТОРИИ (ЭТАП 1 ТЗ). Запись: с заданной частотой (`rate`, по умолчанию 20 Гц) снимаются углы суставов робота потока, время от начала записи и TCP-координаты. Источник — реальный робот (позы применяет поток/исполнитель) ИЛИ фантом (его текущая поза в полёте). Воспроизве…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `checkClearance` | `public bool checkClearance = true;` | Проверять зазор до сцены (дороже; раз в `clearanceInterval` секунд). |
| поле | `checkLimits` | `public bool checkLimits = true;` | Проверять лимиты суставов на каждом шаге воспроизведения. |
| поле | `clearanceInterval` | `public float clearanceInterval = 0.25f;` |  |
| поле | `minClearance` | `public float minClearance = 0.01f;` |  |
| поле | `rate` | `public float rate = 20f;` | Частота сэмплирования при записи, Гц. |
| свойство | `ElapsedSeconds` | `public float ElapsedSeconds { get { return (float)playhead; } }` | Истекло секунд от начала воспроизведения. |
| свойство | `IsPlaying` | `public bool IsPlaying { get { return playing != null; } }` | Идёт воспроизведение. |
| свойство | `IsRecording` | `public bool IsRecording { get { return recording != null; } }` | Идёт запись. |
| свойство | `Paused` | `public bool Paused { get { return playPaused; } }` |  |
| свойство | `Playing` | `public KvTrajectoryRecord Playing { get { return playing; } }` | Воспроизводимая запись. |
| свойство | `Recording` | `public KvTrajectoryRecord Recording { get { return recording; } }` | Записываемая (ещё не сохранённая) запись. |
| событие | `Failed` | `public event Action<string> Failed; // причина отказа (для журнала)` |  |
| событие | `PlaybackFinished` | `public event Action<KvTrajectoryRecord> PlaybackFinished; // по концу ИЛИ остановке` |  |
| событие | `PlaybackStarted` | `public event Action<KvTrajectoryRecord> PlaybackStarted;` |  |
| событие | `RecordingFinished` | `public event Action<KvTrajectoryRecord> RecordingFinished; // запись сохранена (или null)` |  |
| событие | `RecordingStarted` | `public event Action<KvTrajectoryRecord> RecordingStarted;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller)` |  |
| метод | `BindWorld` | `public void BindWorld(CollisionWorld collisionWorld)` | Мир столкновений (для проверки зазора; владелец — хаб функций). |
| метод | `ListRecords` | `public static List<KvTrajectoryRecord> ListRecords()` | Записи, доступные на диске (для дерева моделей и списков). |
| метод | `Play` | `public bool Play(KvTrajectoryRecord rec, float speedMultiplier = -1f)` | Проиграть запись (с множителем скорости). |
| метод | `Progress01` | `public float Progress01` | Прогресс воспроизведения 0…1. |
| метод | `RemainingSeconds` | `public float RemainingSeconds` | Осталось секунд (по множителю скорости) — для панели ETA (этап 12). |
| метод | `SampleNow` | `public bool SampleNow(bool force = false)` | Записать один сэмпл вручную (используется также при ручном ведении суставов). |
| метод | `SetPaused` | `public void SetPaused(bool value)` |  |
| метод | `SpeedMultiplier` | `public float SpeedMultiplier` | Множитель скорости воспроизведения (0.1…8; 1 — как записано). |
| метод | `StartRecording` | `public bool StartRecording(string name, string source = "live")` | Начать запись. Источник: live (робот) / phantom / manual. |
| метод | `StopAll` | `public void StopAll(string why = "сброшено")` | Остановить всё (Esc/аварийный стоп/сброс). |
| метод | `StopPlayback` | `public void StopPlayback(string why = "остановлено оператором")` | Остановить воспроизведение (штатная остановка оператора). |
| метод | `StopRecording` | `public KvTrajectoryRecord StopRecording(bool save = true)` | Остановить запись; сохранить в файл (save=true) и вернуть запись. |
| метод | `Tick` | `public void Tick(float deltaTime)` | Покадровое ведение воспроизведения (вызывает хаб функций). |
| метод | `TogglePause` | `public bool TogglePause()` | Пауза/продолжение воспроизведения. |

### `class KvRecordSample`

- **Файл:** `Assets\_Project\01_Scripts\Recording\KvTrajectoryRecord.cs` (строка 15)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `q` | `public float[] q;` | Углы суставов, град (у SCARA ось Z — призматическая, в тех же единицах, что робот). |
| поле | `rot` | `public float[] rot;` | TCP-поворот (кватернион xyzw) — для полноты записи; в расчётах не участвует. |
| поле | `t` | `public float t;` | Время от начала записи, с. |
| поле | `tcp` | `public float[] tcp;` | TCP в МИРОВЫХ координатах: x, y, z. |
| метод | `CloneQ` | `public double[] CloneQ()` |  |
| метод | `TcpVector` | `public Vector3 TcpVector` |  |

### `class KvRecordStore`

- **Файл:** `Assets\_Project\01_Scripts\Recording\KvTrajectoryRecord.cs` (строка 195)
- **Назначение:** Хранилище записей на диске (папка `Recordings` внутри данных функций). Записи читаются лениво (по требованию дерева/списка), файлы можно копировать руками.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Extension` | `public const string Extension = ".json";` |  |
| метод | `Delete` | `public static bool Delete(KvTrajectoryRecord rec)` |  |
| метод | `Load` | `public static KvTrajectoryRecord Load(string path)` |  |
| метод | `LoadAll` | `public static List<KvTrajectoryRecord> LoadAll()` | Прочитать все записи каталога (битые пропускаются с предупреждением). |
| метод | `Rename` | `public static bool Rename(KvTrajectoryRecord rec, string newName)` | Переименовать запись (файл тоже переименовывается — иначе имя «уедет»). |
| метод | `Save` | `public static string Save(KvTrajectoryRecord rec)` | Сохранить запись; возвращает путь файла или null. |

### `class KvReportGenerator`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvReportPdf.cs` (строка 249)
- **Назначение:** ЭТАП 19 ТЗ: ГЕНЕРАТОР ОТЧЁТОВ (PDF). Что попадает в отчёт: заголовок (проект, дата, робот, число осей), выбранная траектория и её метрики (время, длина, кривизна, зазор, запас лимитов, энергия), таблица суставов (текущий угол, лимиты, запас), кинематика (TCP, база), состояние постобработки (сглаж…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `AutoReportPrefsKey` | `public const string AutoReportPrefsKey = "KazistovVv.Report.Auto";` |  |
| свойство | `ImagesIncluded` | `public bool ImagesIncluded { get { return imagesIncluded; } }` |  |
| свойство | `LastFile` | `public string LastFile { get { return lastFile; } }` |  |
| свойство | `LastPages` | `public int LastPages { get { return lastPages; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvStageHub3 hub3)` |  |
| метод | `FolderPath` | `public string FolderPath` | Папка отчётов (ТЗ): «Документы\KazistovVv\reports». |
| метод | `Generate` | `public string Generate()` | Собрать и сохранить PDF. Возвращает путь ("" — отказ). |
| метод | `Status` | `public string Status()` | Краткое состояние генератора (дерево моделей, свойства, вкладка). |

### `class KvReportTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvReportPdf.cs` (строка 686)
- **Назначение:** ВКЛАДКА «ОТЧЁТ (PDF)» (ЭТАП 19 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "report"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("report.title", "Отчёт (PDF)"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `struct KvRiskResult`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvDynamicObstacles.cs` (строка 19)
- **Назначение:** Результат проверки одной траектории.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `cartAtClosest` | `public Vector3 cartAtClosest; // где в этот момент было препятствие` |  |
| поле | `level` | `public KvPlanRisk level;` |  |
| поле | `minDistance` | `public float minDistance; // минимальное расстояние «звено ↔ препятствие», м` |  |
| поле | `reason` | `public string reason;` |  |
| поле | `timeOfClosest` | `public float timeOfClosest; // когда это произошло, с (от начала траектории)` |  |

### `class KvRobotExporter`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRobotExport.cs` (строка 39)
- **Назначение:** ЭТАП 11 ТЗ: ЭКСПОРТ ТРАЕКТОРИИ В ЯЗЫКИ РОБОТОВ (KUKA KRL / FANUC KAREL / ABB RAPID). Это НЕ языки интерфейса: пользователь выбирает язык робота в настройках, а платформа выгружает КОД ДВИЖЕНИЯ по выбранной траектории в файл нужного формата (`trajectory.src`, `trajectory.kl`, `trajectory.mod`). Чт…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `flange` | `public Quaternion flange;` |  |
| поле | `LanguagePrefsKey` | `public const string LanguagePrefsKey = "KazistovVv.Export.Language";` |  |
| поле | `maxPoints` | `public int maxPoints = 160;` | Сколько точек максимум выгружается (файл не должен быть нечитаемым). |
| поле | `q` | `public double[] q;` |  |
| поле | `tcp` | `public Vector3 tcp;` |  |
| поле | `time` | `public float time;` |  |
| свойство | `LastFile` | `public string LastFile { get { return lastFile; } }` |  |
| свойство | `LastLines` | `public int LastLines { get { return lastLines; } }` |  |
| свойство | `LastPreview` | `public string LastPreview { get { return lastPreview; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller)` |  |
| метод | `CycleLanguage` | `public KvRobotLanguage CycleLanguage()` | Переключить язык робота (циклически) — команда/кнопка настроек. |
| метод | `Export` | `public string Export(TrajectoryCandidate candidate, int index)` | Экспортировать выбранную траекторию. Возвращает путь к файлу или "" при отказе (причина всегда пишется в журнал). |
| метод | `ExportSelected` | `public string ExportSelected()` | Экспорт выбранного варианта (кнопка/команда). |
| метод | `Extension` | `public string Extension` | Расширение файла по ТЗ. |
| метод | `Flow` | `public TrajectoryFlowController Flow() { return flow; }` |  |
| метод | `FolderPath` | `public string FolderPath` | Папка экспорта: «Документы\KazistovVv\robot_export» (резерв — данные приложения). |
| метод | `Language` | `public KvRobotLanguage Language` |  |
| метод | `LanguageLabel` | `public string LanguageLabel` |  |

### `class KvRobotImportService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRobotImport.cs` (строка 100)
- **Назначение:** ЭТАП 12 ТЗ: ИМПОРТ МОДЕЛЕЙ РОБОТОВ (URDF / STEP). URDF (полная поддержка): файл читается с диска, из него берутся звенья (`link`), суставы (`joint`: revolute / continuous / prismatic / fixed), их оси, смещения (`origin xyz rpy`) и ЛИМИТЫ. По этим данным КОДОМ строится иерархия сцены: база → звень…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Files` | `public IReadOnlyList<string> Files { get { return files; } }` |  |
| свойство | `Imported` | `public GameObject Imported { get { return imported; } }` |  |
| свойство | `LastCheck` | `public string LastCheck { get; private set; } = "";` |  |
| свойство | `LastReport` | `public string LastReport { get; private set; } = "";` |  |
| свойство | `LastVisualOnly` | `public bool LastVisualOnly { get; private set; }` |  |
| свойство | `Placement` | `public KvImportPlacement Placement { get; set; }` | Куда ставить модель при импорте (ФИКС 6). |
| свойство | `StandName` | `public string StandName { get; set; }` | Стенд, выбранный для вариантов «заменить» / «добавить» (ФИКС 6). |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)` |  |
| метод | `Import` | `public bool Import(string path)` | Импортировать файл: URDF (с кинематикой) или STEP (визуально). |
| метод | `ImportStep` | `public bool ImportStep(string path)` | Импорт STEP: базовая поддержка — габарит и визуальный корпус. |
| метод | `ImportUrdf` | `public bool ImportUrdf(string path)` | Импорт URDF: структура «база → звенья → суставы → TCP». |
| метод | `RefreshFiles` | `public int RefreshFiles()` | Найти файлы моделей (.urdf / .xml / .step / .stp) во всех папках поиска. |
| метод | `RobotOnStand` | `public RobotController RobotOnStand(string standName)` | Робот, стоящий на стенде (или null). |
| метод | `SearchFolders` | `public string[] SearchFolders()` | Папки поиска моделей: StreamingAssets/robots и «Документы\KazistovVv\robot_models». |
| метод | `StandNames` | `public string[] StandNames()` | Имена стендов сцены (для интерфейса). |

### `enum KvRobotLanguage`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRobotExport.cs` (строка 12)
- **Назначение:** Язык робота для экспорта траектории (ЭТАП 11 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `class KvScenario`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvScenarioManager.cs` (строка 22)
- **Назначение:** Готовый сценарий: список шагов, выполняемых последовательно.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Description` | `public string Description = "";` |  |
| поле | `Id` | `public string Id = "";` |  |
| поле | `Steps` | `public readonly List<KvScenarioStep> Steps = new List<KvScenarioStep>();` |  |
| поле | `Title` | `public string Title = "";` |  |

### `class KvScenarioManager`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvScenarioManager.cs` (строка 43)
- **Назначение:** МЕНЕДЖЕР СЦЕНАРИЕВ (ЭТАП 18 ТЗ). Список готовых сценариев («Показать workspace», «Продемонстрировать pick-and-place», «Показать лимиты», «Демонстрация 8 траекторий»); каждый — набор шагов, выполняемых ПОСЛЕДОВАТЕЛЬНО. Кнопка «Запустить сценарий», пауза и отмена. Шаги оперируют только публичными в…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `All` | `public IReadOnlyList<KvScenario> All { get { return scenarios; } }` |  |
| свойство | `Paused` | `public bool Paused { get { return paused; } }` |  |
| свойство | `Running` | `public bool Running { get { return running != null; } }` |  |
| свойство | `RunningTitle` | `public string RunningTitle { get { return running != null ? running.Title : ""; } }` |  |
| свойство | `StepCount` | `public int StepCount { get { return running != null ? running.Steps.Count : 0; } }` |  |
| свойство | `StepIndex` | `public int StepIndex { get { return stepIndex; } }` |  |
| событие | `Changed` | `public event Action Changed;` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Add` | `public void Add(KvScenario scenario)` |  |
| метод | `CurrentStepText` | `public string CurrentStepText` |  |
| метод | `Find` | `public KvScenario Find(string id)` |  |
| метод | `SetPaused` | `public void SetPaused(bool value)` |  |
| метод | `Start` | `public bool Start(string id)` | Запустить сценарий по id (или по индексу в списке). |
| метод | `Start` | `public bool Start(KvScenario scenario)` |  |
| метод | `Stop` | `public void Stop(string why = "отменено оператором")` | Отменить сценарий. |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое ведение (вызывает хаб). |
| метод | `TogglePause` | `public bool TogglePause()` | Пауза/продолжение сценария. |

### `class KvScenarioStep`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvScenarioManager.cs` (строка 8)
- **Назначение:** Один шаг сценария: что сделать и когда считать шаг завершённым.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Done` | `public Func<bool> Done;` | Признак завершения (null — ждём только MinTime). |
| поле | `Enter` | `public Action Enter;` | Выполнить шаг (может быть null — тогда шаг только «ждёт»). |
| поле | `MaxTime` | `public float MaxTime = 60f;` | Максимальное время шага, с (страховка от «зависания»). |
| поле | `MinTime` | `public float MinTime = 1.5f;` | Минимальное время шага, с. |
| поле | `Text` | `public string Text = "";` |  |

### `class KvScriptEngine`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 464)
- **Назначение:** ЭТАП 26 ТЗ: ДВИЖОК МАКРОСОВ. Макрос — это последовательность РАЗРЕШЁННЫХ команд робота (движение, смена варианта, постобработка, маршрут, журнал) с переменными, циклами `repeat/while` и условиями `if`. Выполнение идёт ПО ЧАСТЯМ в главном потоке (по 3 мс на кадр), поэтому интерфейс не замирает, а …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `MaxRunSeconds` | `public const float MaxRunSeconds = 600f;` |  |
| поле | `MaxStepsPerRun` | `public const int MaxStepsPerRun = 200000;` |  |
| событие | `Message` | `public event Action<string> Message;` |  |

### `class KvScriptError`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 46)
- **Назначение:** Ошибка разбора или выполнения макроса (с номером строки).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `line` | `public int line;` |  |
| поле | `message` | `public string message = "";` |  |
| метод | `ToString` | `public override string ToString()` |  |

### `class KvScriptMacro`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 445)
- **Назначение:** Сохранённый макрос.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `name` | `public string name = "";` |  |
| поле | `saved` | `public string saved = "";` |  |
| поле | `source` | `public string source = "";` |  |

### `class KvScriptParser`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 72)
- **Назначение:** РАЗБОР МАКРОСА. Язык намеренно маленький и «питоно-подобный»: строки, отступы не важны, блоки в фигурных скобках: скорость = 0.4 repeat 3 { в_точку(0.4, 0.2, 0.7) ждать(0.5) } if высота_инструмента() > 0.5 { печать("высоко") } Это НЕ Python и НЕ Lua: отдельного интерпретатора с доступом к файлам …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Parse` | `public static List<KvStmt> Parse(string source, out string error)` | Разобрать текст. Возвращает программу или null (причина — в `out error`). |

### `class KvScriptTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 1250)
- **Назначение:** ВКЛАДКА «МАКРОСЫ» (ЭТАП 26 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "script"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("script.title", "Макросы (скрипты)"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvSession`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSessionManager.cs` (строка 61)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `created` | `public string created = "";` |  |
| поле | `currentPoint` | `public KvVec3 currentPoint = new KvVec3();` |  |
| поле | `filePath` | `[NonSerialized] public string filePath = "";` |  |
| поле | `flags` | `public KvSessionFlags flags = new KvSessionFlags();` |  |
| поле | `hasPoint` | `public bool hasPoint;` |  |
| поле | `id` | `public string id = "";` |  |
| поле | `name` | `public string name = "Сессия";` |  |
| поле | `notes` | `public string notes = "";` |  |
| поле | `points` | `public List<KvVec3> points = new List<KvVec3>();` |  |
| поле | `poseFiles` | `public List<string> poseFiles = new List<string>();` | Файлы сохранённых поз. |
| поле | `recordFiles` | `public List<string> recordFiles = new List<string>();` | Файлы записанных траекторий (пути), которые были в работе. |
| поле | `robots` | `public List<KvSessionRobot> robots = new List<KvSessionRobot>();` |  |
| поле | `sceneName` | `public string sceneName = "";` |  |
| поле | `version` | `public int version = 1;` |  |
| поле | `zones` | `public List<KvZoneData> zones = new List<KvZoneData>();` | Зоны запрета целиком (их можно восстановить в сцене). |
| метод | `Normalize` | `public void Normalize()` |  |
| метод | `Summary` | `public string Summary` |  |

### `class KvSessionFlags`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSessionManager.cs` (строка 40)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `autoRecordPose` | `public bool autoRecordPose = true;` |  |
| поле | `heatmapClearance` | `public bool heatmapClearance;` |  |
| поле | `heatmapReachability` | `public bool heatmapReachability = true;` |  |
| поле | `jointsPanel` | `public bool jointsPanel;` |  |
| поле | `logPanel` | `public bool logPanel;` |  |
| поле | `scenariosPanel` | `public bool scenariosPanel;` |  |
| поле | `zonesVisible` | `public bool zonesVisible = true;` |  |

### `class KvSessionManager`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSessionManager.cs` (строка 170)
- **Назначение:** СОХРАНЕНИЕ / ЗАГРУЗКА СЕССИИ (ЭТАП 20 ТЗ). Сборка сессии — из СЦЕНЫ (позы роботов, активный робот, точки) и из сервисов новых функций (зоны, записи, позы, флаги). Загрузка — обратная операция: зоны создаются заново, позы роботов применяются напрямую через валидатор (с проверкой лимитов), точка во…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `All` | `public IReadOnlyList<KvSession> All { get { return sessions; } }` |  |
| свойство | `Count` | `public int Count { get { return sessions.Count; } }` |  |
| событие | `Changed` | `public event Action Changed;` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, KvZoneService zoneService,` |  |
| метод | `Capture` | `public KvSession Capture(string name)` | Собрать сессию из текущего состояния (без записи на диск). |
| метод | `Delete` | `public bool Delete(KvSession session)` |  |
| метод | `Load` | `public bool Load(KvSession session)` | Загрузить сессию (роботы, точка, зоны, флаги). |
| метод | `Newest` | `public KvSession Newest` | Свежайшая сессия (для «Загрузить последнюю»). |
| метод | `Reload` | `public void Reload()` |  |
| метод | `Save` | `public KvSession Save(string name)` | Сохранить текущее состояние как сессию. |

### `class KvSessionRobot`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSessionManager.cs` (строка 28)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `active` | `public bool active;` |  |
| поле | `euler` | `public KvVec3 euler = new KvVec3();` |  |
| поле | `name` | `public string name = "";` |  |
| поле | `position` | `public KvVec3 position = new KvVec3();` |  |
| поле | `q` | `public float[] q;` | Углы суставов, град (у SCARA ось Z — призматическая). |

### `class KvSessionStore`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSessionManager.cs` (строка 111)
- **Назначение:** Хранилище сессий (файл на сессию).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Extension` | `public const string Extension = ".session.json";` |  |
| метод | `Delete` | `public static bool Delete(KvSession session)` |  |
| метод | `Load` | `public static KvSession Load(string path)` |  |
| метод | `LoadAll` | `public static List<KvSession> LoadAll()` |  |
| метод | `Save` | `public static string Save(KvSession session)` |  |

### `enum KvSeverity`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 594)
- **Назначение:** Важность замечания проверки перед пуском (этап 35 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `enum KvSingularityKind`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSingularityZones.cs` (строка 10)
- **Назначение:** Тип сингулярности (для подписи и подсветки).

_Публичных членов нет (или тип объявлен без них)._

### `class KvSingularityVisualizer`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSingularityZones.cs` (строка 49)
- **Назначение:** ЭТАП 4 ТЗ — ВИЗУАЛИЗАЦИЯ СИНГУЛЯРНОСТЕЙ. ЧТО СЧИТАЕТСЯ (по фактической геометрии проекта, без правок кинематики): * ЗАПЯСТЬЕ (`wrist`) — ось 5 близка к нулю (|q5| < 8°), оси 4 и 6 становятся коллинеарны: робот теряет возможность поворота вокруг оси инструмента. Тот же критерий использует оракул д…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Enabled` | `public bool Enabled;` |  |
| поле | `extendDanger` | `public float extendDanger = 0.985f;` |  |
| поле | `extendWarn` | `public float extendWarn = 0.94f;` |  |
| поле | `interval` | `public float interval = 0.1f;` |  |
| поле | `logEvents` | `public bool logEvents = true;` |  |
| поле | `shoulderDanger` | `public float shoulderDanger = 0.14f;` |  |
| поле | `shoulderWarn` | `public float shoulderWarn = 0.28f;` |  |
| поле | `wristDangerDeg` | `public float wristDangerDeg = 6f;` |  |
| поле | `wristWarnDeg` | `public float wristWarnDeg = 14f;` |  |
| поле | `zoneRadius` | `public float zoneRadius = 0.075f;` |  |
| свойство | `Danger` | `public bool Danger { get; private set; }` |  |
| свойство | `Joint` | `public int Joint { get; private set; } // вовлечённый сустав (-1 — нет)` |  |
| свойство | `Kind` | `public KvSingularityKind Kind { get; private set; }` |  |
| свойство | `Mobility` | `public float Mobility { get; private set; } // σmin / эталон (1 — отлично)` |  |
| свойство | `Reason` | `public string Reason { get; private set; }` |  |
| свойство | `Value` | `public float Value { get; private set; } // метрика (град / доля / σ)` |  |
| свойство | `VisibleZones` | `public int VisibleZones { get; private set; }` |  |
| свойство | `Warn` | `public bool Warn { get; private set; }` |  |
| событие | `Message` | `public event Action<string> Message;` | Сообщение (в журнал/статус). |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller)` |  |
| метод | `Dispose` | `public void Dispose()` |  |
| метод | `Evaluate` | `public void Evaluate()` | Пересчёт метрики и обновление зон (можно звать вручную — диагностика). |
| метод | `Rebind` | `public void Rebind(RobotController robot)` | Сменить робота: пересчитать длины звеньев и эталон σmin. |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` |  |
| метод | `Status` | `public string Status` | Строка состояния (статус-бар, панель, отчёт). |
| метод | `Tick` | `public void Tick(float dt)` |  |
| метод | `Toggle` | `public bool Toggle()` |  |

### `class KvSmoothEntry`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPathSmoothing.cs` (строка 10)
- **Назначение:** Метрики одного варианта: исходная траектория и результат постобработки.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `after` | `public KvTrajStats after;` |  |
| поле | `applied` | `public bool applied;` |  |
| поле | `baseline` | `public KvTrajStats baseline; // тот же путь, но перепараметризованный по лимитам` |  |
| поле | `before` | `public KvTrajStats before; // метрики плана как его построил планировщик` |  |
| поле | `level` | `public float level;` |  |
| поле | `method` | `public KvSmoothMethod method;` |  |
| поле | `original` | `public PlannedTrajectory original; // копия плана ДО обработки` |  |
| поле | `smoothed` | `public PlannedTrajectory smoothed; // план ПОСЛЕ обработки` |  |
| свойство | `Reference` | `public KvTrajStats Reference { get { return baseline.valid ? baseline : before; } }` | Метрики, с которыми сравнивается результат: перепараметризованный по лимитам вариант того же пути. Так сравнение честное — планировщик отдаёт путь с ПОСТОЯННОЙ скоростью внутри сегментов (нулевые ускорение и рывок, но мгновенные скачки скорости в узлах), поэтому «до» по сырому плану всегда выгляд… |
| свойство | `TimeDelta` | `public float TimeDelta { get { return after.time - Reference.time; } }` |  |
| метод | `AccGain` | `public float AccGain` |  |
| метод | `CurvatureGain` | `public float CurvatureGain` |  |
| метод | `JerkGain` | `public float JerkGain` |  |

### `enum KvSmoothMethod`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTrajMath.cs` (строка 9)
- **Назначение:** Метод постобработки пути (ЭТАП 4 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `class KvSmoothTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvPathSmoothing.cs` (строка 475)
- **Назначение:** ВКЛАДКА «СГЛАЖИВАНИЕ» окна-верстака (этап 4): ползунок уровня, выбор метода, тумблер авторежима и таблица метрик «До / После».

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "smooth"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra.T("smooth.title", "Сглаживание траектории"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `enum KvSound`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSpatialAudio.cs` (строка 8)
- **Назначение:** Виды звуковых событий (ЭТАП 16 ТЗ).

_Публичных членов нет (или тип объявлен без них)._

### `class KvSpatialAudio`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSpatialAudio.cs` (строка 29)
- **Назначение:** ПРОСТРАНСТВЕННЫЙ ЗВУК (ЭТАП 16 ТЗ). Аудиофайлов в проекте нет, поэтому сигналы СИНТЕЗИРУЮТСЯ кодом (`AudioClip.Create` + `SetData`) — пять коротких сигналов: щелчок, подтверждение, старт движения, ошибка, аварийная остановка. Никаких ассетов и .meta. Пространственное позиционирование: один общий …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `maxDistance` | `public float maxDistance = 25f;` | Максимальная слышимость, м. |
| поле | `SampleRate` | `public const int SampleRate = 44100;` |  |
| поле | `spatial` | `public bool spatial = true;` | Играть ли звук из позиции робота (false — 2D, «в голове оператора»). |
| поле | `volume` | `public float volume = 0.55f;` | Общая громкость. |
| метод | `Bind` | `public void Bind(Transform host, Transform listenerTransform = null)` | Создать источник звука (вешается на объект потока/камеры). |
| метод | `Buzz` | `public static AudioClip Buzz(string name, float frequency, float duration, float amplitude)` | Низкий «неприятный» сигнал ошибки. |
| метод | `Click` | `public static AudioClip Click(string name, float duration, float toneHz, float amplitude)` | Короткий щелчок: шумовая посылка с быстрым спадом + тон. |
| метод | `Dispose` | `public void Dispose()` | Освободить созданные клипы (вызывается при уничтожении хаба). |
| метод | `Play` | `public void Play(KvSound sound, Vector3 worldPosition, float gain = 1f)` | Проиграть сигнал в мировой точке (по умолчанию — позиция источника). |
| метод | `PlayAtRobot` | `public void PlayAtRobot(KvSound sound, TrajectoryFlowController flow, float gain = 1f)` | Сигнал «от робота»: точка события — TCP робота потока (или его корень). |
| метод | `Sweep` | `public static AudioClip Sweep(string name, float fromHz, float toHz, float duration, float amplitude)` | Плавный свип по частоте (старт/аварийный стоп). |
| метод | `TwoTone` | `public static AudioClip TwoTone(string name, float f0, float f1, float duration, float amplitude)` | Два тона подряд (подтверждение). |

### `struct KvSpeedCap`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvWaypointConstraints.cs` (строка 102)
- **Назначение:** Ограничение скорости в конкретном сэмпле маршрута.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Label` | `public string Label;` |  |
| поле | `MaxMps` | `public float MaxMps;` |  |
| поле | `Sample` | `public int Sample;` |  |

### `class KvStageHub`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvStageHub.cs` (строка 26)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `captureOnHotkeys` | `public bool captureOnHotkeys = true;` |  |
| поле | `healthOnByDefault` | `public bool healthOnByDefault = false;` |  |
| поле | `logEvents` | `public bool logEvents = true;` |  |
| поле | `obstaclesOnByDefault` | `public bool obstaclesOnByDefault = false;` |  |
| поле | `pendantOnByDefault` | `public bool pendantOnByDefault = false;` |  |
| поле | `singularitiesOnByDefault` | `public bool singularitiesOnByDefault = true;` |  |
| свойство | `Capture` | `public KvCaptureService Capture { get; private set; }` |  |
| свойство | `Current` | `public static KvStageHub Current { get { return Instance; } }` |  |
| свойство | `Health` | `public KvHealthMonitor Health { get; private set; }` |  |
| свойство | `HealthPanel` | `public KvHealthPanel HealthPanel { get; private set; }` |  |
| свойство | `Instance` | `public static KvStageHub Instance { get; private set; }` |  |
| свойство | `Obstacles` | `public KvDynamicObstacleService Obstacles { get; private set; }` |  |
| свойство | `Pendant` | `public KvTeachPendant Pendant { get; private set; }` |  |
| свойство | `Singularities` | `public KvSingularityVisualizer Singularities { get; private set; }` |  |
| свойство | `Waypoints` | `public KvWaypointManager Waypoints { get; private set; }` |  |
| метод | `BuildExtraProperties` | `public static void BuildExtraProperties(ProjectNode node, List<KvProp> list)` | Строки свойств для узлов этапов 1–8 (waypoints, препятствие, сингулярности). |
| метод | `BuildTreeNodes` | `public static void BuildTreeNodes(KazistovVvUIManager manager, List<ProjectNode> roots,` | Ветки дерева моделей от этапов 1–8: «Промежуточные точки» (waypoints) и «Динамические препятствия» + «Сингулярности» (как узлы-состояния). |
| метод | `GetFeature` | `public static bool GetFeature(string id, out bool handled)` | Текущее значение переключателя (handled = false — id не наш). |
| метод | `HasFeature` | `public static bool HasFeature(string id)` | Какие id функций обслуживает хаб этапов 1–8 (для панели настроек). |
| метод | `Install` | `public static KvStageHub Install(KazistovVvUIManager manager)` | Создать (или найти) хаб этапов. Вызывается UI-менеджером при регистрации команд. |
| метод | `OnNodeSelected` | `public static void OnNodeSelected(ProjectNode node)` | Выбор узла дерева: узел waypoint становится «выбранным» в редакторе. |
| метод | `RegisterCommands` | `public static void RegisterCommands(KazistovVvUIManager manager)` | Регистрация команд этапов 1–8 (кнопки тулбара и пункты меню). |
| метод | `SetFeature` | `public static bool SetFeature(string id, bool value)` | Применить переключатель. true — id обработан хабом. |
| метод | `TakeScreenshot` | `public bool TakeScreenshot()` | Скриншот: подпись берётся из фактического состояния (ТЗ этап 3.1). |
| метод | `ToggleHealth` | `public void ToggleHealth()` |  |
| метод | `ToggleObstacles` | `public void ToggleObstacles() { Obstacles.Toggle(); }` |  |
| метод | `TogglePendant` | `public void TogglePendant() { Pendant.Toggle(); }` |  |
| метод | `ToggleSingularities` | `public void ToggleSingularities() { Singularities.Toggle(); }` |  |
| метод | `ToggleVideo` | `public void ToggleVideo()` |  |

### `class KvStageHub2`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvStageHub2.cs` (строка 27)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `logEvents` | `public bool logEvents = true;` |  |
| поле | `menuOrbitRadius` | `public float menuOrbitRadius = 3.6f;` |  |
| поле | `showStartMenuOnLaunch` | `public bool showStartMenuOnLaunch = true;` |  |
| поле | `tutorialOnLaunch` | `public bool tutorialOnLaunch = false;` |  |
| свойство | `Current` | `public static KvStageHub2 Current { get { return Instance; } }` |  |
| свойство | `Energy` | `public KvEnergyOptimal Energy { get; private set; }` |  |
| свойство | `Instance` | `public static KvStageHub2 Instance { get; private set; }` |  |
| свойство | `Limits` | `public KvMotionLimits Limits { get; private set; }` | Общие ограничения движения (этапы 4–6: сглаживание, время, энергия). |
| свойство | `QuickStart` | `public KvQuickStart QuickStart { get; private set; }` |  |
| свойство | `Smoothing` | `public KvPathSmoothing Smoothing { get; private set; }` |  |
| свойство | `StartMenu` | `public KvStartMenu StartMenu { get; private set; }` |  |
| свойство | `TimeOptimal` | `public KvTimeOptimal TimeOptimal { get; private set; }` |  |
| свойство | `Tutorial` | `public KvTutorial Tutorial { get; private set; }` |  |
| свойство | `Workbench` | `public KvWorkbenchWindow Workbench { get; private set; }` |  |
| метод | `ApplyEcoProfile` | `public void ApplyEcoProfile()` | Переключиться на эко-профиль (этап 6). |
| метод | `ApplyTimeOptimal` | `public void ApplyTimeOptimal()` | Переключиться на время-оптимальную траекторию (этап 5). |
| метод | `BuildExtraProperties` | `public static void BuildExtraProperties(ProjectNode node, List<KvProp> list)` | Строки свойств: постобработка выбранного варианта + состояние узлов группы «Постобработка траекторий». |
| метод | `BuildTreeNodes` | `public static void BuildTreeNodes(KazistovVvUIManager manager, List<ProjectNode> roots,` | Ветки дерева моделей этапов 1–6: группа «Постобработка траекторий». |
| метод | `ClearWorkspace` | `public void ClearWorkspace()` | Дополнительная очистка рабочей области для «Нового проекта» (этап 1). |
| метод | `GetFeature` | `public static bool GetFeature(string id, out bool handled)` |  |
| метод | `HasFeature` | `public static bool HasFeature(string id)` | Какие id функций обслуживает этот хаб (для панели настроек). |
| метод | `Install` | `public static KvStageHub2 Install(KazistovVvUIManager manager)` | Установить хаб (вызывается UI-менеджером при регистрации команд). |
| метод | `OnNodeSelected` | `public static void OnNodeSelected(ProjectNode node)` | Выбор узла дерева: узлы постобработки открывают свою вкладку верстака. |
| метод | `OpenWorkbench` | `public void OpenWorkbench(string tabKey)` |  |
| метод | `PlayDemo` | `public void PlayDemo()` | Показать демонстрацию (этап 3). |
| метод | `RegisterCommands` | `public static void RegisterCommands(KazistovVvUIManager manager)` | Регистрация команд этапов 1–6 (кнопки тулбара и пункты меню). |
| метод | `SetFeature` | `public static bool SetFeature(string id, bool value)` |  |
| метод | `SmoothSelected` | `public void SmoothSelected()` | Постобработка выбранного варианта: сглаживание (этап 4). |
| метод | `StartTutorial` | `public void StartTutorial()` | Показать обучение (продолжить с сохранённого шага, если оно не закончено). |
| метод | `ToggleDemo` | `public void ToggleDemo()` |  |
| метод | `ToggleStartMenu` | `public void ToggleStartMenu()` |  |
| метод | `ToggleTutorial` | `public void ToggleTutorial()` |  |
| метод | `ToggleWorkbench` | `public void ToggleWorkbench()` |  |
| метод | `TreeSignaturePart` | `public static string TreeSignaturePart()` | Подпись состояния постобработки для пересборки дерева UI-менеджером. |

### `class KvStageHub3`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvStageHub3.cs` (строка 30)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `logEvents` | `public bool logEvents = true;` |  |
| поле | `scanModelsOnStart` | `public bool scanModelsOnStart = true;` |  |
| поле | `waypointConstraints` | `public bool waypointConstraints = true;` |  |
| свойство | `Calibration` | `public KvCalibrationService Calibration { get; private set; }` |  |
| свойство | `Constrained` | `public KvConstrainedPlanner Constrained { get; private set; }` |  |
| свойство | `Current` | `public static KvStageHub3 Current { get { return Instance; } }` |  |
| свойство | `Exporter` | `public KvRobotExporter Exporter { get; private set; }` |  |
| свойство | `Import` | `public KvRobotImportService Import { get; private set; }` |  |
| свойство | `Instance` | `public static KvStageHub3 Instance { get; private set; }` |  |
| свойство | `Limits` | `public KvMotionLimits Limits { get; private set; }` | Единые лимиты движения (те же, что у этапов 4–6). |
| свойство | `Payload` | `public KvPayloadCalculator Payload { get; private set; }` |  |
| метод | `BuildExtraProperties` | `public static void BuildExtraProperties(ProjectNode node, List<KvProp> list)` | Строки свойств этапов 7–12 (узлы группы, робот — нагрузка, варианты — ограничение). |
| метод | `BuildTreeNodes` | `public static void BuildTreeNodes(KazistovVvUIManager manager, List<ProjectNode> roots,` | Ветки дерева моделей этапов 7–12. |
| метод | `CycleExportLanguage` | `public void CycleExportLanguage()` | Переключить язык робота для экспорта (этап 11). |
| метод | `ExportSelected` | `public void ExportSelected()` | Экспорт выбранной траектории в язык робота (этап 11). |
| метод | `GetFeature` | `public static bool GetFeature(string id, out bool handled)` |  |
| метод | `HasFeature` | `public static bool HasFeature(string id)` |  |
| метод | `Install` | `public static KvStageHub3 Install(KazistovVvUIManager manager)` | Установить хаб (вызывается UI-менеджером при регистрации команд). |
| метод | `OnNodeSelected` | `public static void OnNodeSelected(ProjectNode node)` | Выбор узла дерева: узлы этапов 7–12 открывают свою вкладку верстака. |
| метод | `OpenTab` | `public void OpenTab(string tabKey)` |  |
| метод | `RegisterCommands` | `public static void RegisterCommands(KazistovVvUIManager manager)` | Регистрация команд этапов 7–12. |
| метод | `SetFeature` | `public static bool SetFeature(string id, bool value)` |  |
| метод | `ToggleConstrained` | `public void ToggleConstrained()` | Включить/выключить планирование с ограничениями (этап 8). |
| метод | `TreeSignaturePart` | `public static string TreeSignaturePart()` | Подпись состояния этапов 7–12 для пересборки дерева. |
| метод | `WaypointClearLimits` | `public void WaypointClearLimits()` | Снять все ограничения выбранной точки (этап 7). |
| метод | `WaypointDetour` | `public void WaypointDetour()` | Обязательный обход препятствия у выбранной точки (этап 7). |
| метод | `WaypointOrientationFromCurrent` | `public void WaypointOrientationFromCurrent()` | Ограничение ориентации выбранной промежуточной точки «как сейчас» (этап 7). |
| метод | `WaypointPause` | `public void WaypointPause()` | Пауза в выбранной точке (этап 7). |
| метод | `WaypointSpeedLimit` | `public void WaypointSpeedLimit()` | Ограничить скорость в выбранной точке (этап 7). |

### `class KvStageHub4`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvStageHub4.cs` (строка 48)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Behavior` | `public KvBehaviorTree Behavior { get; private set; }` |  |
| свойство | `BehaviorRunner` | `public KvBtRunner BehaviorRunner { get; private set; }` |  |
| свойство | `Cameras` | `public KvCameraService Cameras { get; private set; }` |  |
| свойство | `Cinema` | `public KvCinematicService Cinema { get; private set; }` |  |
| свойство | `Collab` | `public KvCollaborationService Collab { get; private set; }` |  |
| свойство | `Companion` | `public KvCompanionServer Companion { get; private set; }` |  |
| свойство | `Current` | `public static KvStageHub4 Current { get { return Instance; } }` |  |
| свойство | `Environment` | `public KvEnvironmentStudio Environment { get; private set; }` |  |
| свойство | `Eyes` | `public KvEyeTrackingService Eyes { get; private set; }` |  |
| свойство | `Failures` | `public KvFailureSimulator Failures { get; private set; }` |  |
| свойство | `Forces` | `public KvForceVisualizer Forces { get; private set; }` |  |
| свойство | `Foveated` | `public KvFoveatedRendering Foveated { get; private set; }` |  |
| свойство | `Hands` | `public KvHandTrackingService Hands { get; private set; }` |  |
| свойство | `Heatmap` | `public KvTimeHeatmap Heatmap { get; private set; }` |  |
| свойство | `Instance` | `public static KvStageHub4 Instance { get; private set; }` |  |
| свойство | `Lab` | `public KvPlannerLab Lab { get; private set; }` |  |
| свойство | `Lighting` | `public KvLightingStudio Lighting { get; private set; }` |  |
| свойство | `LogTools` | `public KvLogTools LogTools { get; private set; }` |  |
| свойство | `Materials` | `public KvMaterialStudio Materials { get; private set; }` |  |
| свойство | `PreRun` | `public KvPreRunValidator PreRun { get; private set; }` |  |
| свойство | `Proxies` | `public KvCollisionOptimizer Proxies { get; private set; }` |  |
| свойство | `Report` | `public KvReportGenerator Report { get; private set; }` |  |
| свойство | `Script` | `public KvScriptEngine Script { get; private set; }` |  |
| свойство | `Titles` | `public KvTitlesService Titles { get; private set; }` |  |
| свойство | `Voice` | `public KvVoiceService Voice { get; private set; }` |  |
| свойство | `VoiceOver` | `public KvVoiceOverService VoiceOver { get; private set; }` |  |
| свойство | `Web` | `public KvWebDashboard Web { get; private set; }` |  |
| метод | `BuildExtraProperties` | `public static void BuildExtraProperties(ProjectNode node, List<KvProp> list)` | Строки свойств этапов 13–36 (робот: прокси и безопасность; варианты: время достижимости). |
| метод | `BuildTreeNodes` | `public static void BuildTreeNodes(KazistovVvUIManager manager, List<ProjectNode> roots,` | Ветки дерева моделей этапов 13–36. |
| метод | `ClearFault` | `public void ClearFault() { Failures.Clear(); }` |  |
| метод | `CycleEnvironment` | `public void CycleEnvironment()` |  |
| метод | `CycleLighting` | `public void CycleLighting() { Lighting.SetPreset((Lighting.Index + 1) % Lighting.Presets.Count); }` |  |
| метод | `ExportLogView` | `public void ExportLogView() { LogTools.ExportVisible(); }` |  |
| метод | `GetFeature` | `public static bool GetFeature(string id, out bool handled)` |  |
| метод | `HasFeature` | `public static bool HasFeature(string id)` |  |
| метод | `Install` | `public static KvStageHub4 Install(KazistovVvUIManager manager)` |  |
| метод | `MakeReport` | `public void MakeReport() { Report.Generate(); }` |  |
| метод | `OnNodeSelected` | `public static void OnNodeSelected(ProjectNode node)` | Выбор узла дерева: узлы этапов 13–36 открывают свою вкладку верстака. |
| метод | `OpenTab` | `public void OpenTab(string tabKey)` |  |
| метод | `RegisterCommands` | `public static void RegisterCommands(KazistovVvUIManager manager)` | Регистрация команд этапов 13–36. |
| метод | `RequestRun` | `public bool RequestRun()` | Пуск с проверкой перед пуском (этап 35 ТЗ). Возвращает false, если нужно подтверждение. |
| метод | `SetFeature` | `public static bool SetFeature(string id, bool value)` |  |
| метод | `SimulateCommsLoss` | `public void SimulateCommsLoss() { Failures.Start(KvFailureKind.CommsLoss); }` |  |
| метод | `SimulateJointLoss` | `public void SimulateJointLoss() { Failures.Start(KvFailureKind.JointLoss, 2); }` |  |
| метод | `StartBenchmark` | `public void StartBenchmark() { Lab.Start(); }` |  |
| метод | `StartSelected` | `public bool StartSelected()` | ПУСК выбранной траектории (используется проверкой перед пуском, кнопкой «ПУСК с проверкой», голосом, мобильным пультом, макросами). Подтверждение делается тем же путём, что нажатие ЛКМ по фантому — через штатный `ConfirmSelectedTrajectory` потока, без дублирования логики. |
| метод | `ToggleCinema` | `public void ToggleCinema() { Cinema.Toggle(); }` |  |
| метод | `ToggleCompanion` | `public void ToggleCompanion() { Companion.Toggle(); }` |  |
| метод | `ToggleEyes` | `public void ToggleEyes() { Eyes.SetEnabled(!Eyes.Enabled); }` |  |
| метод | `ToggleHands` | `public void ToggleHands() { Hands.SetEnabled(!Hands.Enabled); Hands.SetVisible(Hands.Enabled); }` |  |
| метод | `ToggleProxies` | `public void ToggleProxies() { Proxies.Enabled = !Proxies.Enabled; }` |  |
| метод | `ToggleTree` | `public void ToggleTree() { Lab.ToggleTree(); }` |  |
| метод | `ToggleVoice` | `public void ToggleVoice() { Voice.SetEnabled(!Voice.Enabled); }` |  |
| метод | `ToggleWeb` | `public void ToggleWeb() { Web.Toggle(); }` |  |
| метод | `TreeSignaturePart` | `public static string TreeSignaturePart()` | Подпись состояния этапов 13–36 для пересборки дерева. |

### `class KvStartMenu`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvStartMenu.cs` (строка 31)
- **Назначение:** ЭТАП 1 ТЗ: ГЛАВНОЕ МЕНЮ ПРИ ЗАПУСКЕ. Что делает: • показывает экран запуска поверх сцены: «Новый проект» · «Открыть сессию» · «Показать демо» (этап 3) · «Обучение» (этап 2) · «Настройки» · «Выход»; • фон — МЕДЛЕННЫЙ КИНЕМАТОГРАФИЧЕСКИЙ ОБЛЁТ стенда: камера сама идёт по кругу, плавно меняет высоту…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `cinematic` | `public bool cinematic = true;` |  |
| поле | `demoAction` | `public Action demoAction;` | Действие кнопки «Показать демо» (этап 3) — ставит хаб. |
| поле | `heightWave` | `public float heightWave = 0.35f; // амплитуда плавного подъёма/спуска, м` |  |
| поле | `heightWavePeriod` | `public float heightWavePeriod = 26f; // секунд на полный цикл` |  |
| поле | `lookSmoothing` | `public float lookSmoothing = 3.2f;` |  |
| поле | `newProjectAction` | `public Action newProjectAction;` | Действие «Новый проект» (дополнительная очистка у хаба). |
| поле | `orbitHeight` | `public float orbitHeight = 1.75f;` |  |
| поле | `orbitRadius` | `public float orbitRadius = 3.6f;` |  |
| поле | `orbitSpeed` | `public float orbitSpeed = 6.5f; // градусов в секунду` |  |
| поле | `panelWidth` | `public float panelWidth = 330f;` |  |
| поле | `PrefsKey` | `public const string PrefsKey = "KazistovVv.StartMenu.Enabled";` | Ключ PlayerPrefs: показывать меню при запуске. |
| поле | `sortingOrder` | `public int sortingOrder = 200;` |  |
| поле | `tutorialAction` | `public Action tutorialAction;` | Действие кнопки «Обучение» (этап 2) — ставит хаб. |
| свойство | `ButtonCount` | `public int ButtonCount { get { return buttons.Count; } }` | Сколько пунктов в меню (диагностика). |
| свойство | `IconIds` | `public IReadOnlyList<string> IconIds { get { return buttonIcons; } }` | Иконки пунктов меню по порядку (диагностика). |
| свойство | `TimeInMenu` | `public float TimeInMenu { get { return timeInMenu; } }` |  |
| свойство | `Visible` | `public bool Visible { get { return visible; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Build` | `public void Build(TrajectoryFlowController controller, FreeFlyCameraController cameraRig)` |  |
| метод | `ExitApplication` | `public void ExitApplication()` | ВЫХОД: закрыть приложение (в редакторе — остановить Play). |
| метод | `Hide` | `public void Hide()` |  |
| метод | `NewProject` | `public void NewProject()` | НОВЫЙ ПРОЕКТ: чистая рабочая область (точка, траектории, зоны, маршрут). |
| метод | `OpenSession` | `public void OpenSession()` | ОТКРЫТЬ СЕССИЮ: последняя сохранённая сессия (точка, зоны, позы). |
| метод | `OpenSettings` | `public void OpenSettings()` | НАСТРОЙКИ: вернуть оболочку и открыть панель настроек. |
| метод | `PlayDemo` | `public void PlayDemo()` | ПОКАЗАТЬ ДЕМО (этап 3). |
| метод | `RefreshLanguage` | `public void RefreshLanguage()` | Обновить подписи после смены языка (вызывает хаб). |
| метод | `SelectedLabel` | `public string SelectedLabel` |  |
| метод | `Show` | `public void Show()` |  |
| метод | `ShowOnStart` | `public static bool ShowOnStart` | Показывать ли меню при следующем запуске (настройка переживает перезапуск). |
| метод | `StartTutorial` | `public void StartTutorial()` | ОБУЧЕНИЕ (этап 2). |
| метод | `Tick` | `public void Tick(float deltaTime)` | Вести камеру — вызывать из LateUpdate хаба (после логики оператора). |
| метод | `Toggle` | `public void Toggle()` |  |

### `struct KvStepAxis`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRobotImport.cs` (строка 823)
- **Назначение:** Найденная в STEP ось-кандидат (вращения — цилиндрическая поверхность).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `direction` | `public Vector3 direction;` |  |
| поле | `kind` | `public string kind; // "вращение (CYLINDRICAL_SURFACE)" и т. п.` |  |
| поле | `name` | `public string name;` |  |
| поле | `origin` | `public Vector3 origin;` |  |
| поле | `radius` | `public float radius;` |  |
| метод | `CheckKinematics` | `public bool CheckKinematics()` | Проверить кинематику импортированной модели (ТЗ: «Проверка кинематики после импорта»). |
| метод | `ExtractStepKinematics` | `public static void ExtractStepKinematics(string text, out KvStepAxis[] axes,` | ИЗВЛЕЧЬ КИНЕМАТИЧЕСКУЮ СТРУКТУРУ ИЗ ТЕКСТОВОГО STEP (ФИКС 5). Что ищется (только то, что РЕАЛЬНО есть в файле — ничего не выдумывается): • `CYLINDRICAL_SURFACE` → её `AXIS2_PLACEMENT_3D` → точка + направление оси: это ось-кандидат ВРАЩЕНИЯ (так в STEP описаны шарниры); • `PLANE` → нормаль её разм… |
| метод | `RemoveImported` | `public bool RemoveImported()` | Удалить импортированного робота (модель — самостоятельный объект сцены). |

### `class KvStmt`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 33)
- **Назначение:** Оператор программы макроса.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `args` | `public List<KvExpr> args = new List<KvExpr>();` |  |
| поле | `body` | `public List<KvStmt> body = new List<KvStmt>();` |  |
| поле | `condition` | `public KvExpr condition; // для Repeat/If/While` |  |
| поле | `elseBody` | `public List<KvStmt> elseBody = new List<KvStmt>();` |  |
| поле | `kind` | `public KvStmtKind kind;` |  |
| поле | `line` | `public int line;` |  |
| поле | `name` | `public string name = ""; // имя переменной/функции` |  |
| поле | `value` | `public KvExpr value; // правая часть (для Assign — выражение, для Call — null)` |  |

### `enum KvStmtKind`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 18)
- **Назначение:** Вид оператора во внутреннем языке макросов.

_Публичных членов нет (или тип объявлен без них)._

### `class KvTabKit`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvWorkbenchWindow.cs` (строка 34)
- **Назначение:** НАБОР СТРОИТЕЛЕЙ ДЛЯ ВКЛАДКИ: секции, строки «подпись = значение», переключатели, ползунки, таблицы и кнопки — в том же стиле uGUI/FreeCAD, что и остальные панели проекта. Строки с динамическим текстом обновляются окном (провайдеры `Func`), поэтому в кадре нет сборки мусора.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Content` | `public RectTransform Content { get { return content; } }` |  |
| метод | `Buttons` | `public void Buttons(string[] labels, Action[] actions)` | Ряд кнопок. `labels` и `actions` — одинаковой длины. |
| метод | `Divider` | `public void Divider()` | Разделитель между блоками. |
| метод | `Info` | `public Text Info(Func<string> provider, Color color)` | Строка с динамическим текстом: «подпись: значение». |
| метод | `Note` | `public Text Note(string text, Color color)` |  |
| метод | `Section` | `public void Section(string title)` |  |
| метод | `Segmented` | `public KvSegmented Segmented(string caption, string[] options, int index, Action<int> onChanged)` | Сегментный выбор (радио-строка) — например метод сглаживания. |
| метод | `Slider` | `public Slider Slider(string caption, float min, float max, float value, string format,` | Ползунок с подписью и текущим значением (для «уровня сглаживания» и лимитов). |
| метод | `Table` | `public void Table(string caption, Func<string> left, Func<string> right)` | Строка таблицы «подпись \| значение \| значение» (для метрик). |
| метод | `Toggle` | `public void Toggle(string text, bool value, Action<bool> onChanged)` |  |

### `class KvTeachPendant`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTeachPendant.cs` (строка 61)
- **Назначение:** ЭТАП 8 ТЗ — ВИРТУАЛЬНЫЙ ПУЛЬТ (TEACH PENDANT). ВНЕШНИЙ ВИД (ТЗ): прямоугольная панель на World Space Canvas — корпус, ЭКРАН, ДЖОЙСТИК и крупные кнопки, плюс индикаторы состояния (LED). Два варианта: * ВАРИАНТ A (`Industrial`) — в стиле промышленных пультов: широкий корпус, тёмный экран сверху, тр…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `attach` | `public KvPendantAttach attach = KvPendantAttach.Floating;` |  |
| поле | `faceCamera` | `public bool faceCamera = true;` |  |
| поле | `floatingPosition` | `public Vector3 floatingPosition = new Vector3(1.05f, 1.32f, -24.6f);` |  |
| поле | `followDistance` | `public float followDistance = 0.62f;` |  |
| поле | `followDrop` | `public float followDrop = -0.16f;` |  |
| поле | `homeRequest` | `public Action homeRequest;` |  |
| поле | `jogAxis` | `public int jogAxis = 0; // выбранный сустав (0 — первый)` |  |
| поле | `jogEnabled` | `public bool jogEnabled = true;` |  |
| поле | `jogMode` | `public KvPendantJog jogMode = KvPendantJog.Joint;` |  |
| поле | `jogSpeedDeg` | `public float jogSpeedDeg = 22f; // град/с в режиме Joint` |  |
| поле | `jogSpeedMps` | `public float jogSpeedMps = 0.18f; // м/с в режиме World` |  |
| поле | `logEvents` | `public bool logEvents = true;` |  |
| поле | `recordPoseRequest` | `public Action recordPoseRequest;` |  |
| поле | `startRequest` | `public Func<bool> startRequest;` |  |
| поле | `stopRequest` | `public Action stopRequest;` |  |
| поле | `variant` | `public KvPendantVariant variant = KvPendantVariant.Industrial;` |  |
| поле | `worldScale` | `public float worldScale = 0.0016f; // пиксель UI → метр` |  |
| свойство | `Attach` | `public KvPendantAttach Attach { get { return attach; } }` |  |
| свойство | `JogAxis` | `public int JogAxis { get { return jogAxis; } }` |  |
| свойство | `JogMode` | `public KvPendantJog JogMode { get { return jogMode; } }` |  |
| свойство | `JoystickVector` | `public Vector2 JoystickVector { get { return stick; } }` |  |
| свойство | `ScreenStateText` | `public string ScreenStateText { get { return screenState != null ? screenState.text : ""; } }` |  |
| свойство | `ScreenTcpText` | `public string ScreenTcpText { get { return screenTcp != null ? screenTcp.text : ""; } }` |  |
| свойство | `Variant` | `public KvPendantVariant Variant { get { return variant; } }` |  |
| свойство | `Visible` | `public bool Visible { get { return visible; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Build` | `public void Build(TrajectoryFlowController controller)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `SetAttach` | `public void SetAttach(KvPendantAttach value)` |  |
| метод | `SetJogAxis` | `public void SetJogAxis(int index)` |  |
| метод | `SetJogMode` | `public void SetJogMode(KvPendantJog value)` |  |
| метод | `SetStick` | `public void SetStick(Vector2 value)` | Внешний ввод стика (VR-контроллер, автотест, геймпад). |
| метод | `SetVariant` | `public void SetVariant(KvPendantVariant value)` |  |
| метод | `SetVisible` | `public void SetVisible(bool value)` |  |
| метод | `Status` | `public string Status` |  |
| метод | `Tick` | `public void Tick(float dt)` |  |
| метод | `Toggle` | `public bool Toggle()` |  |
| метод | `ToggleJogMode` | `public void ToggleJogMode()` |  |
| метод | `ToggleVariant` | `public void ToggleVariant()` | Переключить вариант оформления ON-THE-FLY (ТЗ: минимум два варианта). |

### `class KvTimeHeatmap`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvForceHeat.cs` (строка 215)
- **Назначение:** ЭТАП 18 ТЗ: ТЕПЛОВАЯ КАРТА ВРЕМЕНИ ДОСТИЖЕНИЯ. Отличие от обычной тепловой карты достижимости: цвет точки показывает НЕ абстрактную «стоимость», а ВРЕМЯ В СЕКУНДАХ — сколько займёт добраться до неё из текущей позы при текущих лимитах скорости и ускорения. Для каждой точки выборки решается IK, зат…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Building` | `public bool Building { get { return building; } }` |  |
| свойство | `Enabled` | `public bool Enabled { get { return enabled; } }` |  |
| свойство | `PointCount` | `public int PointCount { get { return times != null ? times.Length : 0; } }` |  |
| свойство | `ScaleMax` | `public float ScaleMax { get { return scaleMax; } set { scaleMax = Mathf.Clamp(value, 0.5f, 60f); } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub)` |  |
| метод | `Dispose` | `public void Dispose()` |  |
| метод | `RequestRebuild` | `public void RequestRebuild(bool immediate = false)` |  |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` |  |
| метод | `Status` | `public string Status()` | Строка состояния для вкладки. |
| метод | `Tick` | `public void Tick(float deltaTime)` |  |
| метод | `TimeAt` | `public float TimeAt(TrajectoryCandidate candidate)` | Время достижимости цели варианта траектории: берётся ближайший посчитанный узел карты к конечной точке «колбаски» варианта. −1 — карта не рассчитана или цель далеко от узлов. |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvTimeOptimal`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTimeOptimal.cs` (строка 24)
- **Назначение:** ЭТАП 5 ТЗ: ВРЕМЯ-ОПТИМАЛЬНЫЕ ТРАЕКТОРИИ. Задача: при заданных ограничениях (максимальная скорость суставов, максимальное ускорение, максимальный jerk) найти САМУЮ БЫСТРУЮ траекторию к той же точке. Форма пути сохраняется — меняется распределение времени вдоль пути: • скорость вдоль пути ограничен…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `AccPrefsKey` | `public const string AccPrefsKey = "KazistovVv.Post.MaxAcc";` |  |
| поле | `JerkPrefsKey` | `public const string JerkPrefsKey = "KazistovVv.Post.MaxJerk";` |  |
| поле | `VelPrefsKey` | `public const string VelPrefsKey = "KazistovVv.Post.MaxVel";` |  |
| событие | `Message` | `public event Action<string> Message;` |  |

### `class KvTimeOptimalTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTimeOptimal.cs` (строка 272)
- **Назначение:** ВКЛАДКА «ВРЕМЯ-ОПТИМАЛЬНАЯ ТРАЕКТОРИЯ» окна-верстака (этап 5): ползунки лимитов, кнопки расчёта/переключения и таблица метрик.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "topt"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra.T("topt.title", "Время-оптимальная траектория"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvTitleCue`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCinematics.cs` (строка 308)
- **Назначение:** Одна надпись на экране: титр, подзаголовок или пояснение (этап 32 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `end` | `public float end = 2f;` |  |
| поле | `kind` | `public int kind; // 0 — титр, 1 — подзаголовок, 2 — нижняя треть` |  |
| поле | `start` | `public float start;` |  |
| поле | `text` | `public string text = "";` |  |

### `class KvTitlesService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCinematics.cs` (строка 343)
- **Назначение:** ЭТАП 32 ТЗ: ТИТРЫ, ПОДПИСИ И ПОЯСНЕНИЯ (плюс редактор субтитров). Три вида надписей: крупный титр (по центру сверху), подзаголовок (внизу по центру) и «нижняя треть» (плашка слева внизу, как в новостях). Каждая надпись живёт свой отрезок времени; список отрезков редактируется во вкладке и выгружа…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Annotations` | `public IList<KvAnnotation> Annotations { get { return annotations; } }` |  |
| свойство | `Cues` | `public IList<KvTitleCue> Cues { get { return cues; } }` |  |
| свойство | `Duration` | `public float Duration { get { return duration; } set { duration = Mathf.Clamp(value, 1f, 3600f); } }` |  |
| свойство | `Playing` | `public bool Playing { get { return playing; } }` |  |
| свойство | `Visible` | `public bool Visible { get { return visible; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `AddAnnotation` | `public KvAnnotation AddAnnotation(Vector3 point, string text, Color color)` |  |
| метод | `AddCue` | `public KvTitleCue AddCue(float start, float end, string text, int kind)` |  |
| метод | `Bind` | `public void Bind(Camera mainCamera, Transform canvasParent)` |  |
| метод | `ClearAnnotations` | `public void ClearAnnotations()` |  |
| метод | `ClearCues` | `public void ClearCues()` |  |
| метод | `CurrentText` | `public string CurrentText` |  |
| метод | `ExportSrt` | `public string ExportSrt(string folder, string baseName)` | Выгрузка субтитров в файл `.srt` (понимают видеоредакторы и плееры). |
| метод | `Play` | `public void Play(float from = 0f)` |  |
| метод | `RemoveCue` | `public bool RemoveCue(int index)` |  |
| метод | `Reset` | `public void Reset()` |  |
| метод | `SetVisible` | `public void SetVisible(bool value)` |  |
| метод | `SrtStamp` | `public static string SrtStamp(float seconds)` | Формат субтитров SRT: часы:минуты:секунды,запятая,миллисекунды. |
| метод | `Stamp` | `public static string Stamp(float seconds)` |  |
| метод | `Status` | `public string Status()` |  |
| метод | `Stop` | `public void Stop()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` |  |
| метод | `Time` | `public float Time` |  |

### `class KvTitlesTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCinematics.cs` (строка 987)
- **Назначение:** ВКЛАДКА «ТИТРЫ И СУБТИТРЫ» (ЭТАП 32 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "titles"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("titles.title", "Титры и подписи"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvToolKinematics`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvToolKinematics.cs` (строка 97)
- **Назначение:** КИНЕМАТИКА ИНСТРУМЕНТА И IK С ДОПОЛНИТЕЛЬНЫМИ ОГРАНИЧЕНИЯМИ (этапы 7–8 ТЗ). ЧТО ЗДЕСЬ: • ось инструмента считается по ФАКТИЧЕСКОЙ кинематике — это ось вращения последнего сустава (`PoseValidator.AxisWorld`), ровно тот же критерий, что и у `ToolAlign` и у оракула достижимости. Ничего в ядре не мен…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `DesiredAxis` | `public static Vector3 DesiredAxis(PoseValidator v, double[] q, KvOrientConstraint c)` | Куда должна смотреть ось инструмента в данной конфигурации (для LookAt направление зависит от фактического TCP, поэтому считается каждый раз). |
| метод | `Error` | `public static float Error(PoseValidator v, double[] q, KvOrientConstraint c)` | НАРУШЕНИЕ ОГРАНИЧЕНИЯ В ГРАДУСАХ: 0 — соблюдено. Для MaxTilt превышение предела, для остальных — угол между фактической осью и требуемой. |
| метод | `Satisfies` | `public static bool Satisfies(PoseValidator v, double[] q, KvOrientConstraint c)` | Соблюдено ли ограничение с учётом допуска. |
| метод | `TiltDeg` | `public static float TiltDeg(PoseValidator v, double[] q, Vector3 reference)` | Фактический наклон инструмента от опорного направления, °. |
| метод | `ToolAxis` | `public static Vector3 ToolAxis(PoseValidator v, double[] q)` | Ось инструмента: ось вращения последнего сустава (нормаль концевой плоскости). |

### `class KvTrajectoryRecord`

- **Файл:** `Assets\_Project\01_Scripts\Recording\KvTrajectoryRecord.cs` (строка 51)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `created` | `public string created = "";` | Когда создана (yyyy-MM-dd HH:mm:ss). |
| поле | `duration` | `public float duration;` | Длительность записи, с. |
| поле | `filePath` | `[NonSerialized] public string filePath = "";` | Файл, из которого запись прочитана (пусто для только что созданной). |
| поле | `id` | `public string id = "";` |  |
| поле | `length` | `public float length;` | Длина пути TCP, м. |
| поле | `name` | `public string name = "Запись";` |  |
| поле | `notes` | `public string notes = "";` | Свободная заметка оператора. |
| поле | `rate` | `public float rate = 20f;` | Частота сэмплирования при записи, Гц. |
| поле | `robot` | `public string robot = "";` | Имя робота, на котором писали (чтобы не проигрывать чужую кинематику). |
| поле | `samples` | `public List<KvRecordSample> samples = new List<KvRecordSample>();` |  |
| поле | `source` | `public string source = "live";` | Источник: live (реальный робот), phantom (фантом), plan (вариант траектории). |
| поле | `speed` | `public float speed = 1f;` | Множитель скорости воспроизведения, сохранённый вместе с записью. |
| поле | `version` | `public int version = 1;` | Версия схемы файла (растёт при несовместимых изменениях). |
| свойство | `SampleCount` | `public int SampleCount { get { return samples != null ? samples.Count : 0; } }` |  |
| метод | `ComputeLength` | `public float ComputeLength()` | Длина TCP-пути по сэмплам, м (пересчитывается, если не сохранена). |
| метод | `Dof` | `public int Dof` | Число суставов в записи (по первому сэмплу). |
| метод | `Normalize` | `public void Normalize()` | Починить запись после чтения: поля, списки, монотонное время, длины. |
| метод | `ShortLabel` | `public string ShortLabel` | Подпись для дерева моделей и списков. |
| метод | `SourceLabel` | `public string SourceLabel` |  |
| метод | `Tooltip` | `public string Tooltip` | Полное описание для подсказки. |
| метод | `ToPlannedTrajectory` | `public TrajectoryCore.PlannedTrajectory ToPlannedTrajectory(float speedMultiplier = 1f)` | Превратить запись в траекторию ядра (`TrajectoryCore.PlannedTrajectory`) — так запись можно отдать фантому и планировщику «как обычную траекторию» (ТЗ этап 1). Времена масштабируются множителем скорости (1 — как записано). |

### `class KvTrajMath`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTrajMath.cs` (строка 138)
- **Назначение:** ОБЩАЯ МАТЕМАТИКА ПОСТОБРАБОТКИ ТРАЕКТОРИЙ (этапы 4–6 ТЗ). Единицы: углы суставов — ГРАДУСЫ (как во всём проекте), призматические оси — МЕТРЫ (`PoseValidator.IsPrismatic`), время — секунды, длины TCP — метры. Всё считается в ПРОСТРАНСТВЕ СУСТАВОВ: это гарантирует, что сглаженный путь остаётся дост…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `LastProfileApplicable` | `public static bool LastProfileApplicable { get; private set; }` | Профиль последнего расчёта — аналитический (true) или пересчёт времён (false). |
| свойство | `LastProfileClamped` | `public static int LastProfileClamped { get; private set; }` | Сколько сэмплов последнего профиля пришлось срезать по пределу (0 — чисто). |
| свойство | `LastProfileJerk` | `public static double LastProfileJerk { get; private set; }` | Рывок, которым строился последний профиль, ед/с³. |
| свойство | `LastProfileNote` | `public static string LastProfileNote { get; private set; } = "";` | Строка о последнем построенном профиле (для журнала и интерфейса). |
| метод | `Analyze` | `public static KvTrajStats Analyze(PoseValidator v, PlannedTrajectory plan,` | ПОЛНЫЙ РАЗБОР ТРАЕКТОРИИ: время, длина, максимумы скорости/ускорения/jerk, кривизна, зазор, запас лимитов и энергия (если дана модель). |
| метод | `Clone` | `public static PlannedTrajectory Clone(PlannedTrajectory plan, string label = null)` | Глубокая копия плана (варианты «исходная / сглаженная / оптимальная» независимы). |
| метод | `Copy` | `public static double[] Copy(double[] q)` |  |
| метод | `CopyPath` | `public static double[][] CopyPath(double[][] path)` |  |
| метод | `Curvature` | `public static void Curvature(Vector3[] poly, out float max, out float average)` | Кривизна полилинии TCP: k = 2\|a×b\| / (\|a\|\|b\|\|a+b\|). |
| метод | `Energy` | `public static float Energy(PoseValidator v, PlannedTrajectory plan, KvEnergyModel model,` | То же, но с заранее посчитанными плечами удержания (см. LeverArms). |
| метод | `Energy` | `public static float Energy(PoseValidator v, PlannedTrajectory plan, KvEnergyModel model,` | ЭНЕРГИЯ ПО ТЗ (ЭТАП 6): E = Σ \|момент × угловая скорость\| × dt. Момент — по упрощённой динамике: инерция × ускорение + вязкое трение × скорость + удержание (масса distal-части и груза на плече от базы до TCP). Призматическая ось считается в СИ (Н × м/с = Вт), вращательные — в °/с, переведённых в … |
| метод | `EnsureTimes` | `public static bool EnsureTimes(PlannedTrajectory plan)` | Гарантировать корректный массив времён (монотонный, той же длины, что путь). |
| метод | `LeverArms` | `public static float[] LeverArms(PoseValidator v, double[][] path, Vector3 basePosition)` | Плечи удержания по сэмплам пути: горизонтальное расстояние от базы робота до TCP. Зависит ТОЛЬКО от геометрии, поэтому считается один раз и переиспользуется для всех профилей времени (поиск эко-профиля на этапе 6 делает десятки расчётов). |
| метод | `MeasureJerk` | `public static double MeasureJerk(double[][] path, float[] times, double[] scale)` | ФАКТИЧЕСКИЙ максимум рывка траектории (ФИКС 1) — теми же КОНЕЧНЫМИ РАЗНОСТЯМИ по времени, что и метрика Analyze, в единицах пути (нормировка ), поэтому величина сравнима с пределом рывка. |
| метод | `PathLength` | `public static float PathLength(PoseValidator v, double[][] path)` | Длина пути TCP по сэмплам суставов, м. |
| метод | `Retime` | `public static PlannedTrajectory Retime(PoseValidator v, PlannedTrajectory source,` | ВРЕМЯ-ОПТИМАЛЬНАЯ ПАРАМЕТРИЗАЦИЯ (ЭТАП 5 ТЗ). Путь не меняется — меняется только распределение времени. Схема: 1. путь переводится в нормированное пространство суставов (1 единица ≈ 90° или 10 см), считается длина пути s и производные q'=dq/ds, q''=d²q/ds²; 2. предел скорости вдоль пути: v ≤ min_… |
| метод | `Smooth` | `public static double[][] Smooth(double[][] path, KvSmoothMethod method, float level)` | СГЛАЖИВАНИЕ ПУТИ (ЭТАП 4 ТЗ). Уровень 0…1 — доля смешивания с гладким путём (0 % — исходная траектория, 100 % — полностью гладкая). Концы НЕ смещаются: траектория обязана начинаться и заканчиваться там, где её построил планировщик. Методы: • B-сплайн — кубический равномерный B-сплайн (свёртка мас… |
| метод | `SProfileBuild` | `public static void SProfileBuild(PlannedTrajectory plan, double[] scale, double[] s,` | АНАЛИТИЧЕСКИЙ S-ПРОФИЛЬ (ФИКС 1): разгон → крейсер → торможение. 1. ОСНОВА — время-оптимальный ТРАПЕЦЕИДАЛЬНЫЙ профиль по лимитам скорости и ускорения (классическое интегрирование вперёд/назад по длине пути); 2. поверх него ускорение ограничивается ПО РЫВКУ во ВРЕМЕННОЙ области: \|a[i+1] − a[i]\| ≤… |
| метод | `TcpPolyline` | `public static Vector3[] TcpPolyline(PoseValidator v, double[][] path)` | Полилиния TCP по сэмплам конфигураций (для визуала и кривизны). |
| метод | `VerifyJerk` | `public static bool VerifyJerk(PoseValidator v, PlannedTrajectory plan, KvMotionLimits limits,` | ЧЕСТНАЯ ПРОВЕРКА РЫВКА ПО ФАКТИЧЕСКИМ СЭМПЛАМ (ФИКС 1). Аналитический S-профиль задаёт рывок ВДОЛЬ пути. У сглаженного пути (Безье, Гаусс, B-сплайн) к нему добавляется вклад формы пути (q''·v²), которого в одномерном профиле нет. Поэтому после расчёта метрика проверяется отдельно, и если предел п… |

### `struct KvTrajMetrics`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvComparison.cs` (строка 10)
- **Назначение:** Метрики одной траектории для сравнения (ЭТАП 6 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `clearanceMm` | `public float clearanceMm;` |  |
| поле | `curvatureDeg` | `public float curvatureDeg;` |  |
| поле | `dangerous` | `public bool dangerous; // пересекает зону запрета` |  |
| поле | `index` | `public int index;` |  |
| поле | `label` | `public string label;` |  |
| поле | `lengthM` | `public float lengthM;` |  |
| поле | `limitMarginDeg` | `public float limitMarginDeg;` |  |
| поле | `safe` | `public bool safe;` |  |
| поле | `samples` | `public int samples;` |  |
| поле | `score` | `public double score;` |  |
| поле | `timeS` | `public float timeS;` |  |
| поле | `why` | `public string why;` |  |

### `struct KvTrajStats`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTrajMath.cs` (строка 60)
- **Назначение:** МЕТРИКИ ТРАЕКТОРИИ «ДО / ПОСЛЕ» (ЭТАП 4 ТЗ: jerk, ускорения, кривизна) плюс энергетика (ЭТАП 6 ТЗ: энергия, удельная энергия, пиковая мощность). Все величины считаются по ФАКТИЧЕСКИМ сэмплам пути (конечные разности по времени), поэтому метрика честно отражает то, что поедет исполнитель.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `clearance` | `public float clearance; // минимальный зазор, м` |  |
| поле | `curvature` | `public float curvature; // макс. кривизна пути TCP, 1/м` |  |
| поле | `curvatureAvg` | `public float curvatureAvg; // средняя кривизна, 1/м` |  |
| поле | `energy` | `public float energy; // энергия, Дж (Σ \|момент × скорость\| × dt)` |  |
| поле | `length` | `public float length; // длина пути TCP, м` |  |
| поле | `limitMargin` | `public float limitMargin; // минимальный запас до лимитов, °` |  |
| поле | `maxAcc` | `public float maxAcc; // макс. ускорение сустава, °/с²` |  |
| поле | `maxJerk` | `public float maxJerk; // макс. jerk сустава, °/с³` |  |
| поле | `maxVel` | `public float maxVel; // макс. скорость сустава, °/с` |  |
| поле | `peakPower` | `public float peakPower; // пиковая мощность, Вт` |  |
| поле | `samples` | `public int samples;` |  |
| поле | `time` | `public float time; // полное время, с` |  |
| поле | `valid` | `public bool valid;` |  |
| свойство | `EnergyPerMeter` | `public float EnergyPerMeter { get { return length > 1e-4f ? energy / length : 0f; } }` |  |
| метод | `Line` | `public string Line()` |  |

### `class KvTutorial`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvTutorial.cs` (строка 33)
- **Назначение:** ЭТАП 2 ТЗ: ТУТОРИАЛ / ONBOARDING. Четыре шага ровно по ТЗ: 1. как выбрать точку (красный лазер); 2. как подтвердить траекторию (зелёный лазер); 3. как двигать точку (режим перемещения точки); 4. как выбрать фантом (переключение варианта). Каждый шаг завершается САМ, когда оператор действительно в…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `ActivePrefsKey` | `public const string ActivePrefsKey = "KazistovVv.Tutorial.Active";` | Ключ PlayerPrefs: обучение было начато (для «продолжить»). |
| поле | `Done` | `public Func<bool> Done; // условие автозавершения` |  |
| поле | `DonePrefsKey` | `public const string DonePrefsKey = "KazistovVv.Tutorial.Done";` | Ключ PlayerPrefs: обучение пройдено до конца. |
| поле | `HighlightCommand` | `public string HighlightCommand; // id команды тулбара ("" — не подсвечивать)` |  |
| поле | `Hotkey` | `public string Hotkey; // «Z», «X», «Enter», «ЛКМ»` |  |
| поле | `sortingOrder` | `public int sortingOrder = 210;` |  |
| поле | `StepPrefsKey` | `public const string StepPrefsKey = "KazistovVv.Tutorial.Step";` | Ключ PlayerPrefs: номер текущего шага. |
| поле | `TextKey` | `public string TextKey;` |  |
| поле | `TitleKey` | `public string TitleKey;` |  |
| свойство | `Active` | `public bool Active { get { return active; } }` |  |
| свойство | `CaptionVisible` | `public bool CaptionVisible { get { return card != null && card.Visible; } }` |  |
| свойство | `Card` | `public KvHintCard Card { get { return card; } }` |  |
| свойство | `Completed` | `public static bool Completed { get { return PlayerPrefs.GetInt(DonePrefsKey, 0) != 0; } }` | Обучение пройдено до конца (сохраняется между запусками). |
| свойство | `SavedStep` | `public static int SavedStep { get { return PlayerPrefs.GetInt(StepPrefsKey, 0); } }` | Сохранённый шаг (для «продолжить позже»). |
| свойство | `StepCount` | `public int StepCount { get { return steps.Count; } }` |  |
| свойство | `StepIndex` | `public int StepIndex { get { return stepIndex; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Build` | `public void Build(TrajectoryFlowController controller)` |  |
| метод | `HideCaption` | `public void HideCaption()` | Скрыть карточку (демонстрация завершена, обучение выключено). |
| метод | `HideCardOnly` | `public void HideCardOnly()` | Просто скрыть карточку, не меняя режим (используется при выходе из демо). |
| метод | `MarkStepDone` | `public void MarkStepDone()` | Отметить текущий шаг выполненным (кнопка карточки «Отметить выполненным», а также внешние вызовы — например диагностический прогон). |
| метод | `RestartTutorial` | `public void RestartTutorial()` | Начать обучение заново (кнопка после завершения). |
| метод | `ShowCaption` | `public void ShowCaption(string title, string body, string footer, string[] buttons,` | Показать карточку с произвольным текстом (режим демонстрации). |
| метод | `Skip` | `public void Skip(string why)` | «Пропустить туториал»: подсказки выключаются, прогресс сохраняется. |
| метод | `StartedNotFinished` | `public static bool StartedNotFinished` | Обучение было начато, но не закончено. |
| метод | `StartTutorial` | `public void StartTutorial(bool resume)` | Начать (или продолжить) обучение. |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание: проверка условия шага. |
| метод | `Toggle` | `public void Toggle()` | Тумблер по клавише F3: включить/выключить обучение. |

### `class KvUndoStack`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvUndoRedo.cs` (строка 41)
- **Назначение:** UNDO / REDO (ЭТАП 15 ТЗ). Стек отмен глубиной 20 (по ТЗ 10–20): смена точки, выбор траектории, запуск движения, а также операции новых функций (зоны, позы, записи). Отмена/повтор НЕ переписывают State Machine: каждая операция выполняется теми же публичными входами, что и действие оператора (`Lock…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `depth` | `public int depth = 20;` | Глубина истории (ТЗ: 10–20 шагов). |
| свойство | `Applying` | `public bool Applying { get { return applying; } }` | Идёт применение отмены/повтора (чтобы наблюдатель не записал это как новое действие). |
| свойство | `CanRedo` | `public bool CanRedo { get { return redo.Count > 0; } }` |  |
| свойство | `CanUndo` | `public bool CanUndo { get { return undo.Count > 0; } }` |  |
| свойство | `NextRedoTitle` | `public string NextRedoTitle { get { return CanRedo ? redo[redo.Count - 1].Title : ""; } }` |  |
| свойство | `NextUndoTitle` | `public string NextUndoTitle { get { return CanUndo ? undo[undo.Count - 1].Title : ""; } }` | Заголовок следующей отмены/повтора (для подсказок кнопок). |
| свойство | `RedoCount` | `public int RedoCount { get { return redo.Count; } }` |  |
| свойство | `UndoCount` | `public int UndoCount { get { return undo.Count; } }` |  |
| событие | `Changed` | `public event Action Changed;` |  |
| событие | `Performed` | `public event Action<string> Performed; // «Отменено: …» / «Повторено: …»` |  |
| метод | `Clear` | `public void Clear()` |  |
| метод | `History` | `public List<string> History(int max = 10)` | Список последних действий (для панели журнала/диагностики). |
| метод | `Record` | `public void Record(IKvUndoAction action)` | Записать выполненное действие в историю. |
| метод | `Record` | `public void Record(string title, Action undoAction, Action redoAction)` | Записать действие делегатами. |
| метод | `Redo` | `public bool Redo()` |  |
| метод | `Undo` | `public bool Undo()` |  |

### `class KvUrdfJoint`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRobotImport.cs` (строка 56)
- **Назначение:** Описание сустава из URDF.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `axis` | `public Vector3 axis = Vector3.up;` |  |
| поле | `child` | `public string child = "";` |  |
| поле | `childLink` | `public Transform childLink;` |  |
| поле | `hasLimits` | `public bool hasLimits;` |  |
| поле | `name` | `public string name = "joint";` |  |
| поле | `origin` | `public Vector3 origin = Vector3.zero;` |  |
| поле | `parent` | `public string parent = "";` |  |
| поле | `rpy` | `public Vector3 rpy = Vector3.zero; // градусы` |  |
| поле | `transform` | `public Transform transform; // созданный узел сустава` |  |
| поле | `type` | `public string type = "revolute";` |  |

### `class KvUrdfLink`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRobotImport.cs` (строка 72)
- **Назначение:** Описание звена из URDF.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `geometry` | `public string geometry = ""; // box / cylinder / sphere / mesh / нет` |  |
| поле | `name` | `public string name = "link";` |  |
| поле | `size` | `public Vector3 size = Vector3.one; // box: размеры, cylinder: (r, length, r)` |  |
| поле | `transform` | `public Transform transform;` |  |
| поле | `visualOrigin` | `public Vector3 visualOrigin = Vector3.zero;` |  |
| поле | `visualRpy` | `public Vector3 visualRpy = Vector3.zero;` |  |

### `class KvValidateTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` (строка 1334)
- **Назначение:** ВКЛАДКА «ПРОВЕРКА ПЕРЕД ПУСКОМ» (ЭТАП 35 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "validate"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("valid.title", "Проверка перед пуском"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `struct KvValue`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 496)
- **Назначение:** Значение переменной макроса: число или строка.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `body` | `public List<KvStmt> body;` |  |
| поле | `condition` | `public KvExpr condition;` |  |
| поле | `index` | `public int index;` |  |
| поле | `isRepeat` | `public bool isRepeat;` |  |
| поле | `isText` | `public bool isText;` |  |
| поле | `isWhile` | `public bool isWhile;` |  |
| поле | `line` | `public int line;` |  |
| поле | `number` | `public double number;` |  |
| поле | `repeatLeft` | `public int repeatLeft;` |  |
| поле | `Sample` | `public const string Sample =` | Пример макроса — открывается кнопкой «Пример» (этап 26 ТЗ). |
| поле | `text` | `public string text;` |  |
| свойство | `CurrentLine` | `public int CurrentLine { get { return currentLine; } }` |  |
| свойство | `LastError` | `public KvScriptError LastError { get { return error; } }` |  |
| свойство | `Macros` | `public IList<KvScriptMacro> Macros { get { return macros; } }` |  |
| свойство | `Name` | `public string Name { get { return name; } }` |  |
| свойство | `Output` | `public IList<string> Output { get { return output; } }` |  |
| свойство | `Running` | `public bool Running { get { return running; } }` |  |
| свойство | `Source` | `public string Source { get { return source; } }` |  |
| свойство | `StepCount` | `public int StepCount { get { return steps; } }` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvWaypointManager route,` |  |
| метод | `DeleteMacro` | `public bool DeleteMacro(string macroName)` |  |
| метод | `EvaluateExpression` | `public bool EvaluateExpression(string expression, out double value)` | Публичная оценка выражения (для условий дерева поведения, этап 27): принимает как «tcp_z() > 0.5», так и «предел()». |
| метод | `LoadMacros` | `public void LoadMacros()` |  |
| метод | `LoadMacroSource` | `public string LoadMacroSource(string macroName)` |  |
| метод | `MacrosFolder` | `public string MacrosFolder` |  |
| метод | `Run` | `public bool Run(string text, string macroName)` |  |
| метод | `SaveMacro` | `public void SaveMacro(string macroName, string text)` |  |
| метод | `Status` | `public string Status` |  |
| метод | `Stop` | `public void Stop()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое выполнение: не дольше 3 мс за кадр. |
| метод | `ToString` | `public override string ToString()` |  |

### `class KvVariantKit`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvVariantKit.cs` (строка 22)
- **Назначение:** ОБЩИЕ ОПЕРАЦИИ НАД ВАРИАНТАМИ ТРАЕКТОРИЙ (нужны этапам 4–6: сглаживание, время-оптимальная траектория, эко-профиль). Задача модуля — аккуратно подменить геометрию/время УЖЕ ПОСТРОЕННОГО варианта, не ломая ни «колбаску», ни летящие фантомы: • путь копируется В ТЕ ЖЕ массивы (`double[][]`), если чи…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `ApplyPlan` | `public static bool ApplyPlan(TrajectoryFlowController flow, TrajectoryCandidate candidate,` | Подменить план варианта: геометрия копируется в существующие массивы (если число сэмплов совпадает — иначе ссылка заменяется), время берётся из нового плана, «колбаска» и метрики варианта обновляются. |
| метод | `At` | `public static TrajectoryCandidate At(TrajectoryFlowController flow, int index)` | Вариант по индексу (с проверкой диапазона). |
| метод | `Count` | `public static int Count(TrajectoryFlowController flow)` | Сколько вариантов сейчас построено. |
| метод | `Label` | `public static string Label(TrajectoryCandidate candidate, int index)` | Краткая подпись варианта для интерфейса: «№3 · название». |
| метод | `Ready` | `public static bool Ready(TrajectoryFlowController flow)` | Готова ли сцена к постобработке (есть варианты и валидатор). |
| метод | `RobotBase` | `public static Vector3 RobotBase(TrajectoryFlowController flow)` | Позиция базы робота (для энергетической модели этапа 6). |
| метод | `SceneStatus` | `public static string SceneStatus(TrajectoryFlowController flow)` | Состояние сцены одной строкой (для вкладок верстака). |
| метод | `Selected` | `public static TrajectoryCandidate Selected(TrajectoryFlowController flow, out int index)` | Вариант, с которым работает оператор (или null, если траекторий нет). |
| метод | `SelectedIndex` | `public static int SelectedIndex(TrajectoryFlowController flow)` | Индекс варианта, с которым работает оператор (выбранный, иначе лучший). |

### `class KvVec3`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvSessionManager.cs` (строка 10)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Vector` | `public Vector3 Vector { get { return new Vector3(x, y, z); } }` |  |

### `class KvVoiceCommand`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvXrInput.cs` (строка 39)
- **Назначение:** Разобранная голосовая команда.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `executed` | `public bool executed;` |  |
| поле | `id` | `public string id; // «stop», «home», «run», «pause», «select», «undo», «pose», «shot»` |  |
| поле | `number` | `public int number = -1; // номер для «вариант три»` |  |
| поле | `phrase` | `public string phrase; // исходная фраза` |  |
| поле | `result` | `public string result;` |  |
| поле | `time` | `public float time;` |  |

### `class KvVoiceOverService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCinematics.cs` (строка 659)
- **Назначение:** Запись голоса диктора в WAV (этап 33 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `MaxSeconds` | `public const int MaxSeconds = 300;` |  |
| свойство | `Duration` | `public float Duration { get { return savedClip != null ? (float)recordedSamples / frequency : 0f; } }` |  |
| свойство | `HasRecording` | `public bool HasRecording { get { return savedClip != null && recordedSamples > 0; } }` |  |
| свойство | `LastPath` | `public string LastPath { get { return lastPath; } }` |  |
| свойство | `Peak` | `public float Peak { get { return peak; } }` |  |
| свойство | `PlaybackTime` | `public float PlaybackTime { get { return source != null ? source.time : 0f; } }` |  |
| свойство | `Playing` | `public bool Playing { get { return source != null && source.isPlaying; } }` |  |
| свойство | `Recording` | `public bool Recording { get { return recording; } }` |  |
| свойство | `VideoOffset` | `public float VideoOffset { get { return videoOffset; } }` |  |
| свойство | `Waveform` | `public float[] Waveform { get { return waveform; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(Transform parent)` |  |
| метод | `Folder` | `public string Folder` |  |
| метод | `Play` | `public void Play(float from = 0f)` |  |
| метод | `PlaySyncedToVideo` | `public void PlaySyncedToVideo(float videoSeconds)` | Воспроизведение, синхронизированное с видеорядом: время видео → время голоса. |
| метод | `RecordingTime` | `public float RecordingTime` |  |
| метод | `SaveMarkers` | `public void SaveMarkers()` | Рядом с записью — текстовый список меток (титры и время) для монтажа. |
| метод | `Start` | `public bool Start()` |  |
| метод | `Status` | `public string Status()` |  |
| метод | `Stop` | `public string Stop()` | Остановить запись и сохранить файл WAV. |
| метод | `StopPlayback` | `public void StopPlayback()` |  |

### `class KvVoiceOverTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvCinematics.cs` (строка 1113)
- **Назначение:** ВКЛАДКА «ГОЛОС ДИКТОРА» (ЭТАП 33 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "voiceover"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("vo.title", "Голос диктора"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh()` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvVoiceService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvXrInput.cs` (строка 49)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `EnabledPrefsKey` | `public const string EnabledPrefsKey = "KazistovVv.Voice.Enabled";` |  |
| поле | `ThresholdPrefsKey` | `public const string ThresholdPrefsKey = "KazistovVv.Voice.Threshold";` |  |
| свойство | `DeviceName` | `public string DeviceName { get { return string.IsNullOrEmpty(device) ? "—" : device; } }` |  |
| свойство | `Enabled` | `public bool Enabled { get { return enabled; } }` |  |
| свойство | `History` | `public IList<KvVoiceCommand> History { get { return history; } }` |  |
| свойство | `Level` | `public float Level { get { return level; } }` |  |
| свойство | `MicActive` | `public bool MicActive { get { return clip != null && Microphone.IsRecording(device); } }` |  |
| свойство | `MicCount` | `public int MicCount { get { return Microphone.devices != null ? Microphone.devices.Length : 0; } }` |  |
| свойство | `Peak` | `public float Peak { get { return peak; } }` |  |
| свойство | `SourceCount` | `public int SourceCount { get { return sources.Count; } }` |  |
| свойство | `Speaking` | `public bool Speaking { get { return speaking; } }` |  |
| свойство | `Threshold` | `public float Threshold { get { return threshold; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub, KazistovVvUIManager manager)` |  |
| метод | `Parse` | `public static KvVoiceCommand Parse(string phrase)` | Грамматика: русские и английские формулировки → идентификатор команды. |
| метод | `PushRecognized` | `public KvVoiceCommand PushRecognized(string text)` | Принять текст от распознавателя речи (или из поля ручного ввода). |
| метод | `RegisterSource` | `public void RegisterSource(IKvSpeechSource source)` |  |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` |  |
| метод | `SetThreshold` | `public void SetThreshold(float value)` |  |
| метод | `Status` | `public string Status()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание: измерение громкости и определение границ фразы. |

### `class KvVoiceTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvXrInput.cs` (строка 1207)
- **Назначение:** ВКЛАДКА «ГОЛОС» (ЭТАП 23 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "voice"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra3.T("voice.title", "Голосовые команды"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvWaypoint`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvWaypoints.cs` (строка 10)
- **Назначение:** Одна промежуточная точка маршрута (ЭТАП 5 ТЗ).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Clearance` | `public float Clearance; // минимальный зазор в позе, м` |  |
| поле | `Index` | `public int Index; // позиция в маршруте (0 — первая)` |  |
| поле | `Limits` | `public KvWaypointLimits Limits = new KvWaypointLimits();` | ОГРАНИЧЕНИЯ ТОЧКИ (ЭТАП 7 ТЗ): ориентация TCP, скорость в точке, обязательный обход препятствия и пауза. Живут вместе с точкой, переживают перемещение, смену порядка и пересчёт маршрута. |
| поле | `Marker` | `public GameObject Marker;` |  |
| поле | `MarkerRenderer` | `public Renderer MarkerRenderer;` |  |
| поле | `Note` | `public string Note = ""; // причина недостижимости` |  |
| поле | `OrientationError` | `public float OrientationError;` | Фактическое нарушение ориентации в позе прохода, ° (0 — соблюдено). |
| поле | `Pose` | `public double[] Pose; // поза прохода (для планировщика)` |  |
| поле | `Position` | `public Vector3 Position; // точка в мире (цель TCP)` |  |
| поле | `Reachable` | `public bool Reachable; // решена ли IK и есть ли зазор` |  |
| свойство | `HasLimits` | `public bool HasLimits { get { return Limits != null && Limits.Any; } }` |  |
| метод | `Short` | `public string Short` |  |

### `class KvWaypointLimits`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvWaypointConstraints.cs` (строка 19)
- **Назначение:** ОГРАНИЧЕНИЯ ПРОМЕЖУТОЧНОЙ ТОЧКИ (ЭТАП 7 ТЗ). Пользователь может задать для waypoint: • требуемую ОРИЕНТАЦИЮ TCP (направление оси инструмента в мире + допуск); • максимальную СКОРОСТЬ TCP в этой точке; • обязательный ОБХОД препятствия в окрестности (центр + радиус + запас); • ПАУЗУ (остановиться и…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Detour` | `public bool Detour;` | Обязательный обход препятствия в окрестности точки. |
| поле | `DetourCenter` | `public Vector3 DetourCenter = Vector3.zero;` | Центр препятствия (откуда берётся — из точки прицела или задаётся вручную). |
| поле | `DetourMargin` | `public float DetourMargin = 0.04f;` | Дополнительный запас, м (насколько ближе радиуса подходить нельзя). |
| поле | `DetourRadius` | `public float DetourRadius = 0.12f;` | Радиус окрестности препятствия, м. |
| поле | `LimitSpeed` | `public bool LimitSpeed;` | Ограничивать скорость TCP в точке. |
| поле | `MaxSpeedMps` | `public float MaxSpeedMps = 0.10f;` | Максимальная скорость TCP в точке, м/с. |
| поле | `Orientation` | `public KvOrientConstraint Orientation = new KvOrientConstraint();` | Ограничение ориентации инструмента (этап 7, тот же тип, что у этапа 8). |
| поле | `Pause` | `public bool Pause;` | Пауза в точке: остановиться и подождать. |
| поле | `PauseSeconds` | `public float PauseSeconds = 2f;` | Длительность паузы, с. |
| метод | `Any` | `public bool Any` | Есть ли хотя бы одно ограничение. |
| метод | `Clear` | `public void Clear()` |  |
| метод | `Clone` | `public KvWaypointLimits Clone()` |  |
| метод | `Describe` | `public string Describe()` | Короткое описание для дерева, свойств и журнала. |
| метод | `Short` | `public string Short()` | Короткая метка для маркера/строки списка. |

### `class KvWaypointManager`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvWaypoints.cs` (строка 69)
- **Назначение:** ЭТАП 5 ТЗ — WAYPOINT EDITOR (промежуточные точки маршрута). ЧТО ДЕЛАЕТ: * ставит промежуточные точки: кнопка «Добавить waypoint» берёт ТЕКУЩУЮ точку (зафиксированную красным лазером) или точку прицела — как и требует ТЗ; * каждая точка проверяется на достижимость (IK + зазор до мира тем же `PoseV…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `colorBad` | `public Color colorBad = new Color(1f, 0.20f, 0.18f);` |  |
| поле | `colorLimits` | `public Color colorLimits = new Color(0.75f, 0.45f, 1f); // точка с ограничениями (этап 7)` |  |
| поле | `colorOk` | `public Color colorOk = new Color(0.20f, 0.95f, 0.40f);` |  |
| поле | `colorSelected` | `public Color colorSelected = new Color(0.30f, 0.85f, 1f);` |  |
| поле | `limits` | `public KvMotionLimits limits;` | Лимиты движения (скорость/ускорение/jerk) — те же, что у этапов 4–6; нужны для пересчёта профиля времени маршрута при ограничении скорости в точках (этап 7). |
| поле | `logEvents` | `public bool logEvents = true;` |  |
| поле | `markerRadius` | `public float markerRadius = 0.042f;` |  |
| поле | `maxWaypoints` | `public int maxWaypoints = 8;` |  |
| поле | `routeColor` | `public Color routeColor = new Color(0.95f, 0.55f, 0.10f); // оранжевый, как «колбаски»` |  |
| поле | `routeVariants` | `public int routeVariants = 3; // сколько маршрутов-вариантов показывать` |  |
| поле | `tubeRadius` | `public float tubeRadius = 0.035f;` |  |
| поле | `tubeSpread` | `public float tubeSpread = 0.045f; // боковое разведение маршрутов, м` |  |
| поле | `useWaypointLimits` | `public bool useWaypointLimits = true;` | Учитывать ли ограничения точек (этап 7) при проверке точки и построении маршрута. Выключается переключателем «Ограничения waypoint» в настройках. |
| свойство | `Count` | `public int Count { get { return items.Count; } }` |  |
| свойство | `HasRoute` | `public bool HasRoute { get { return route != null; } }` |  |
| свойство | `Items` | `public IReadOnlyList<KvWaypoint> Items { get { return items; } }` |  |
| свойство | `Route` | `public PlannedTrajectory Route { get { return route; } }` |  |
| свойство | `RouteNote` | `public string RouteNote { get { return routeNote; } }` |  |
| свойство | `SelectedIndex` | `public int SelectedIndex { get { return selected; } }` |  |
| событие | `Changed` | `public event Action Changed;` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Add` | `public bool Add(Vector3 point, string source)` | Добавить waypoint в конкретной точке мира. |
| метод | `AddFromAim` | `public bool AddFromAim()` | Добавить waypoint в ТЕКУЩЕЙ точке: если красным лазером зафиксирована точка — она, иначе — точка прицела (шарик лазера). Это и есть «выбрать точку + Добавить waypoint». |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,` |  |
| метод | `Clear` | `public void Clear(string why = "очищено оператором")` | Очистить все точки (маршрут снимается). |
| метод | `Dispose` | `public void Dispose()` |  |
| метод | `EvaluateAll` | `public void EvaluateAll()` | Проверить все точки: IK (тем же способом, что pick-and-place и позы), лимиты и зазор до мира. Недостижимая точка помечается и в маршрут не пускается. ВАЖНО (найдено прогоном 15.09.2026): одиночная IK часто даёт позу, стоящую РОВНО на пределе лимита сустава (запас 0.0°), а планировщик требует ≥3° … |
| метод | `MoveDown` | `public bool MoveDown(int index)` | Изменить порядок: сдвинуть точку позже. |
| метод | `MoveSelectedToAim` | `public bool MoveSelectedToAim()` | Переместить выбранную точку в точку прицела (ТЗ: «переместить»). |
| метод | `MoveUp` | `public bool MoveUp(int index)` | Изменить порядок: сдвинуть точку раньше (ТЗ: «изменить порядок»). |
| метод | `NudgeSelected` | `public bool NudgeSelected(Vector3 delta)` | Сдвинуть точку на вектор (кнопки/клавиши интерфейса). |
| метод | `PlayRoute` | `public bool PlayRoute()` | Проиграть маршрут штатным исполнителем (этап 4 потока, внешний план). |
| метод | `RebuildRoute` | `public bool RebuildRoute()` | Построить маршрут через waypoints существующим планировщиком. Возвращает false, если маршрут не построен (нет точки, есть недостижимая точка, планировщик не нашёл путь) — по ТЗ траектория в этом случае НЕ строится. |
| метод | `Remove` | `public bool Remove(int index)` | Удалить точку по индексу. |
| метод | `RemoveLast` | `public bool RemoveLast() { return items.Count > 0 && Remove(items.Count - 1); }` | Удалить последнюю точку. |
| метод | `RemoveSelected` | `public bool RemoveSelected() { return Remove(selected); }` |  |
| метод | `Select` | `public void Select(int index)` | Выбрать точку (из дерева моделей). |
| метод | `SetMotionLimits` | `public void SetMotionLimits(KvMotionLimits value)` | Задать лимиты движения (вызывает хаб этапов 7–12). |
| метод | `SetPosition` | `public bool SetPosition(int index, Vector3 position)` | Задать новую позицию точки. |
| метод | `Status` | `public string Status` | Строка состояния для панели/статуса. |
| метод | `Tick` | `public void Tick(float dt)` | Кадровое обслуживание: пульсация выбранного маркера. |
| метод | `UnreachableCount` | `public int UnreachableCount` |  |

### `class KvWaypointRouteKit`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvWaypointConstraints.cs` (строка 122)
- **Назначение:** ИНСТРУМЕНТЫ МАРШРУТА С ОГРАНИЧЕНИЯМИ (этап 7): измерение расстояния до препятствия, ограничение скорости в точках и вставка пауз. Все расчёты — на готовом `PlannedTrajectory` штатного планировщика, ядро не меняется.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `EnforceSpeedCaps` | `public static bool EnforceSpeedCaps(PoseValidator v, PlannedTrajectory plan,` | ОГРАНИЧЕНИЕ СКОРОСТИ В ТОЧКАХ: если фактическая скорость TCP в точке больше заданной, весь профиль времени пересчитывается по лимитам с подходящим масштабом (до 3 попыток). Профиль считается тем же `KvTrajMath.Retime`, что и время-оптимальные траектории (этап 5), поэтому ограничения суставов собл… |
| метод | `InsertDwells` | `public static PlannedTrajectory InsertDwells(PlannedTrajectory plan, List<KvDwell> dwells,` | ВСТАВИТЬ ПАУЗЫ В МАРШРУТ: в позицию сэмпла добавляется столько же сэмплов той же позы, сколько нужно на выдержку (шаг 0.05 с). Исполнитель ведёт робота по времени, поэтому дубли позы = реальная остановка и ожидание (ТЗ этапа 7). |
| метод | `MinDistanceToPoint` | `public static float MinDistanceToPoint(PoseValidator v, PlannedTrajectory plan,` | Минимальное расстояние цепочки TCP до точки (препятствия), м. |
| метод | `TcpSpeedAt` | `public static float TcpSpeedAt(PoseValidator v, PlannedTrajectory plan, int index, int window = 1)` | Скорость TCP (м/с) в сэмпле плана: по полилинии TCP и временам плана. |

### `class KvWaypointTab`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvWaypointConstraints.cs` (строка 255)
- **Назначение:** ВКЛАДКА «ОГРАНИЧЕНИЯ WAYPOINT» (ЭТАП 7 ТЗ): для выбранной промежуточной точки задаются ориентация TCP, скорость, обход препятствия и пауза; маршрут перестраивается штатным планировщиком с учётом этих ограничений.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Key` | `public string Key { get { return "waypoints"; } }` |  |
| свойство | `Title` | `public string Title { get { return KvLocExtra2.T("wp.limits", "Ограничения точки"); } }` |  |
| метод | `Build` | `public void Build(KvTabKit kit)` |  |
| метод | `Refresh` | `public void Refresh() { }` |  |
| метод | `Tick` | `public void Tick() { }` |  |

### `class KvWebDashboard`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvNetTools.cs` (строка 562)
- **Назначение:** ЭТАП 21 ТЗ: ВЕБ-ДАШБОРД — мониторинг состояния робота через браузер. Поднимается МАЛЕНЬКИЙ HTTP-СЕРВЕР на «сыром» сокете (`TcpListener`): так не нужны права администратора на резервирование URL (чего требует `HttpListener`). Отдаёт страницу мониторинга (`/`) и состояние в JSON (`/api/state`): фаз…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `DefaultPort` | `public const int DefaultPort = 47800;` |  |
| свойство | `Port` | `public int Port { get { return port; } }` |  |
| свойство | `Requests` | `public int Requests { get { return requests; } }` |  |
| свойство | `Running` | `public bool Running { get { return running; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Address` | `public string Address` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvStageHub3 hub3)` |  |
| метод | `SetPort` | `public void SetPort(int value)` |  |
| метод | `Start` | `public bool Start()` |  |
| метод | `Stop` | `public void Stop()` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обслуживание: главный поток готовит JSON состояния. |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvWindowDrag`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvAutomation.cs` (строка 2145)
- **Назначение:** Перетаскивание окна за заголовок (единый приём для новых окон этапов 13–36).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `canvasRect` | `public RectTransform canvasRect;` |  |
| поле | `target` | `public RectTransform target;` |  |
| метод | `OnDrag` | `public void OnDrag(PointerEventData eventData)` |  |
| метод | `OnPointerDown` | `public void OnPointerDown(PointerEventData eventData)` |  |

### `class KvWorkbenchWindow`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvWorkbenchWindow.cs` (строка 250)
- **Назначение:** ОКНО-ВЕРСТАК ПОСТОБРАБОТКИ ТРАЕКТОРИЙ (этапы 4–6 ТЗ: сглаживание, время-оптимальная траектория, энергия). Собственный канвас, перетаскивание за заголовок, вкладки из реестра — ничего в существующей оболочке не меняется.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `refreshInterval` | `public float refreshInterval = 0.2f;` |  |
| поле | `sortingOrder` | `public int sortingOrder = 46;` |  |
| поле | `windowHeight` | `public float windowHeight = 430f;` |  |
| поле | `windowWidth` | `public float windowWidth = 560f;` |  |
| свойство | `ActiveTab` | `public int ActiveTab { get { return activeTab; } }` |  |
| свойство | `Instance` | `public static KvWorkbenchWindow Instance { get { return instance; } }` |  |
| свойство | `TabCount` | `public static int TabCount { get { return tabs.Count; } }` |  |
| свойство | `Visible` | `public bool Visible { get { return visible; } }` |  |
| метод | `ActiveKey` | `public string ActiveKey` |  |
| метод | `Create` | `public static KvWorkbenchWindow Create(Transform parent)` |  |
| метод | `FindTab` | `public static IKvWorkbenchTab FindTab(string key)` |  |
| метод | `Hide` | `public void Hide()` |  |
| метод | `RebuildContent` | `public void RebuildContent()` | Перерисовать содержимое активной вкладки (после внешних изменений). |
| метод | `RebuildTabs` | `public void RebuildTabs()` | Пересобрать полосу вкладок (после регистрации новой вкладки). |
| метод | `RegisterTab` | `public static void RegisterTab(IKvWorkbenchTab tab)` | Зарегистрировать вкладку (повторная регистрация с тем же ключом заменяет её). |
| метод | `Show` | `public void Show(int index)` |  |
| метод | `Show` | `public void Show(string key)` |  |
| метод | `Toggle` | `public void Toggle()` |  |

### `class KvZone`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvKeepOutZones.cs` (строка 115)
- **Назначение:** Зона запрета в сцене: полупрозрачный красный объём БЕЗ коллайдера. Коллайдер удаляется намеренно: `CollisionWorld.Rebuild` собирает препятствия из коллайдеров сцены, и живой коллайдер зоны молча изменил бы и планирование, и вердикты оракула во всём проекте (правило «не ломать существующее поведен…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Data` | `public KvZoneData Data { get; private set; }` |  |
| свойство | `ZoneName` | `public string ZoneName { get { return Data != null ? Data.name : name; } }` |  |
| метод | `ApplyTransform` | `public void ApplyTransform()` | Обновить позицию/поворот/масштаб без пересоздания объекта. |
| метод | `ContainsPoint` | `public bool ContainsPoint(Vector3 worldPoint)` | Точка внутри зоны (true) — с учётом формы и поворота. |
| метод | `Initialize` | `public void Initialize(KvZoneData data)` |  |
| метод | `Rebuild` | `public void Rebuild()` | Пересобрать визуал под текущие данные (форма/размер/поворот). |
| метод | `SetColor` | `public void SetColor(Color color)` |  |
| метод | `SetVisible` | `public void SetVisible(bool visible)` |  |
| метод | `SignedDistance` | `public float SignedDistance(Vector3 worldPoint)` | Расстояние до зоны со знаком: 0 снаружи (по габариту, для куба/цилиндра — «шахматная» метрика, достаточная для пометки опасных участков). |

### `class KvZoneData`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvKeepOutZones.cs` (строка 14)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `center` | `public float[] center = new float[3];` | Центр в МИРОВЫХ координатах: x, y, z. |
| поле | `created` | `public string created = "";` |  |
| поле | `euler` | `public float[] euler = new float[3];` | Поворот (эйлеры, град) — для куба и цилиндра. |
| поле | `filePath` | `[NonSerialized] public string filePath = "";` |  |
| поле | `id` | `public string id = "";` |  |
| поле | `name` | `public string name = "Зона запрета";` |  |
| поле | `notes` | `public string notes = "";` |  |
| поле | `shape` | `public KvZoneShape shape = KvZoneShape.Box;` |  |
| поле | `size` | `public float[] size = new float[] { 0.4f, 0.4f, 0.4f };` | Габарит: куб — (ширина, высота, глубина); сфера — (радиус); цилиндр — (радиус, высота). |
| поле | `version` | `public int version = 1;` |  |
| поле | `visible` | `public bool visible = true;` | Показывать ли зону в сцене. |
| свойство | `Center` | `public Vector3 Center { get { return ToVector(center, Vector3.zero); } }` |  |
| свойство | `Euler` | `public Vector3 Euler { get { return ToVector(euler, Vector3.zero); } }` |  |
| свойство | `Size` | `public Vector3 Size { get { return ToVector(size, Vector3.one * 0.4f); } }` |  |
| метод | `Normalize` | `public void Normalize()` |  |
| метод | `SetCenter` | `public void SetCenter(Vector3 v) { center = new[] { v.x, v.y, v.z }; }` |  |
| метод | `SetEuler` | `public void SetEuler(Vector3 v) { euler = new[] { v.x, v.y, v.z }; }` |  |
| метод | `SetSize` | `public void SetSize(Vector3 v) { size = new[] { v.x, v.y, v.z }; }` |  |
| метод | `ShapeLabel` | `public string ShapeLabel` |  |
| метод | `SizeText` | `public string SizeText` | Габарит одной строкой. |
| метод | `Tooltip` | `public string Tooltip` |  |

### `struct KvZoneHit`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvKeepOutZones.cs` (строка 271)
- **Назначение:** Результат проверки траектории на пересечение с зонами запрета.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `hit` | `public bool hit;` |  |
| поле | `linkIndex` | `public int linkIndex;` | Индекс звена робота (0..Dof-1), которое вошло в зону. |
| поле | `penetration` | `public float penetration;` | Насколько глубоко внутрь зашла (м): 0 — коснулась границы. |
| поле | `sampleIndex` | `public int sampleIndex;` | Индекс сэмпла траектории, где пересечение самое глубокое. |
| поле | `zoneId` | `public string zoneId;` |  |
| поле | `zoneName` | `public string zoneName;` |  |
| метод | `Describe` | `public string Describe()` |  |

### `class KvZoneMarks`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvComparison.cs` (строка 219)
- **Назначение:** Отметка «траектория пересекает зону запрета» (ЭТАП 5 ТЗ). Живёт в `ConditionalWeakTable`-подобном виде: отметка привязана к САМОМУ объекту кандидата, поэтому не путается между пересчётами и не требует правок `TrajectoryCandidate` (файл ядра не меняется).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `dangerous` | `public bool dangerous;` |  |
| поле | `penetration` | `public float penetration;` |  |
| поле | `reason` | `public string reason = "";` |  |
| метод | `Clear` | `public static void Clear()` |  |
| метод | `IsMarked` | `public static bool IsMarked(TrajectoryCandidate candidate)` |  |
| метод | `PenetrationOf` | `public static float PenetrationOf(TrajectoryCandidate candidate)` |  |
| метод | `Prune` | `public static void Prune(List<TrajectoryCandidate> alive)` | Убрать отметки, которых уже нет в текущем списке кандидатов (без утечки словаря). |
| метод | `ReasonOf` | `public static string ReasonOf(TrajectoryCandidate candidate)` |  |

### `class KvZoneService`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvKeepOutZones.cs` (строка 302)
- **Назначение:** ЗОНЫ ЗАПРЕТА (ЭТАП 5 ТЗ). Создание куба/сферы/цилиндра, полупрозрачная красная подсветка, список в дереве моделей, удаление, изменение размера и перемещение. Планировщик проверяет траектории на пересечение: каждый сэмпл плана раскладывается в цепочку звеньев робота (`PoseValidator.JointFrames`), …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `discardDangerous` | `public bool discardDangerous = false;` | Отбрасывать опасные траектории (true) или только помечать (false). |
| поле | `linkSampleStep` | `public float linkSampleStep = 0.05f;` | Шаг сэмплирования звеньев при проверке, м. |
| поле | `skipEdgeSamples` | `public int skipEdgeSamples;` | Не проверять первые/последние сэмплы (робот стоит у цели) — по ТЗ не требуется. |
| свойство | `Count` | `public int Count { get { return zones.Count; } }` |  |
| свойство | `Zones` | `public IReadOnlyList<KvZone> Zones { get { return zones; } }` |  |
| событие | `Changed` | `public event Action Changed;` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `CheckPlan` | `public KvZoneHit CheckPlan(PlannedTrajectory plan, PoseValidator validator, int dof)` | Проверить план на пересечение с зонами запрета (по звеньям робота, не только по TCP). |
| метод | `Clear` | `public void Clear()` |  |
| метод | `Create` | `public KvZone Create(KvZoneShape shape, Vector3 center, Vector3? size = null, string name = null)` | Создать зону заданной формы с центром в точке и габаритом по умолчанию. |
| метод | `Create` | `public KvZone Create(KvZoneData data)` |  |
| метод | `DefaultName` | `public static string DefaultName(KvZoneShape shape)` |  |
| метод | `DefaultSize` | `public static Vector3 DefaultSize(KvZoneShape shape)` |  |
| метод | `Delete` | `public bool Delete(KvZone zone)` |  |
| метод | `DestroyRoot` | `public void DestroyRoot()` | Убрать сам контейнер при выходе (как фантомы — служебные объекты не «протекают»). |
| метод | `Find` | `public KvZone Find(string id)` |  |
| метод | `LoadAll` | `public int LoadAll()` | Загрузить зоны с диска (при старте и по кнопке «Загрузить зоны»). |
| метод | `Move` | `public void Move(KvZone zone, Vector3 center)` | Переместить зону (и сохранить). |
| метод | `Rename` | `public void Rename(KvZone zone, string newName)` |  |
| метод | `Resize` | `public void Resize(KvZone zone, Vector3 size)` | Изменить габарит зоны (и сохранить). |
| метод | `Root` | `public Transform Root` | Контейнер зон в сцене (виден в Hierarchy — оператор должен их находить). |
| метод | `Rotate` | `public void Rotate(KvZone zone, Vector3 euler)` | Повернуть зону (и сохранить). |
| метод | `Save` | `public void Save(KvZone zone)` |  |
| метод | `SaveAll` | `public void SaveAll()` |  |
| метод | `SetAllVisible` | `public void SetAllVisible(bool visible)` | Показать/скрыть все зоны разом (кнопка «Зоны запрета» в тулбаре). |
| метод | `SetVisible` | `public void SetVisible(KvZone zone, bool visible)` |  |
| метод | `ZoneAt` | `public KvZone ZoneAt(Vector3 worldPoint)` | Точка внутри какой-нибудь зоны (для предупреждения при выборе точки). |

### `enum KvZoneShape`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvKeepOutZones.cs` (строка 10)
- **Назначение:** Форма зоны запрета (ЭТАП 5 ТЗ: куб, сфера, цилиндр).

_Публичных членов нет (или тип объявлен без них)._

### `class KvZoneStore`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvKeepOutZones.cs` (строка 594)
- **Назначение:** Хранилище зон запрета на диске.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Extension` | `public const string Extension = ".zone.json";` |  |
| метод | `LoadAll` | `public static List<KvZoneData> LoadAll()` |  |
| метод | `Save` | `public static string Save(KvZoneData data)` |  |

### `struct PlanCheck`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvToolKinematics.cs` (строка 175)
- **Назначение:** Результат проверки ограничения на плане.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `compliance` | `public float compliance; // доля соблюдения 0..1` |  |
| поле | `firstViolation` | `public int firstViolation; // индекс первого нарушающего сэмпла` |  |
| поле | `samples` | `public int samples;` |  |
| поле | `violations` | `public int violations;` |  |
| поле | `worstAngle` | `public float worstAngle; // худшее нарушение, °` |  |
| поле | `worstLimit` | `public float worstLimit; // наибольший наклон фактически, °` |  |
| свойство | `Ok` | `public bool Ok { get { return violations == 0; } }` |  |
| метод | `AnalyzePlan` | `public static PlanCheck AnalyzePlan(PoseValidator v, PlannedTrajectory plan,` | Проверить ограничение на всех сэмплах готового плана. |
| метод | `Describe` | `public string Describe(string what)` |  |
| метод | `Seeds` | `public static List<double[]> Seeds(PoseValidator v, Planner planner, Vector3 point,` | СТАРТОВЫЕ ПРИБЛИЖЕНИЯ ДЛЯ IK С ОГРАНИЧЕНИЕМ (ФИКС 2). Спуск по градиенту — локальный метод: у ориентации «на 180°» (инструмент должен смотреть вниз, а смотрит вверх) градиент в окрестности почти нулевой, и из «обычной» позы решение не находится, хотя оно существует. Поэтому для нарушающих сэмплов… |
| метод | `Solve` | `public static bool Solve(PoseValidator v, CollisionWorld world, Vector3 point,` | РЕШИТЬ IK С ДОПОЛНИТЕЛЬНЫМ ОГРАНИЧЕНИЕМ: TCP остаётся в точке `point`, нарушение ориентации уменьшается до допуска. Возвращает false с причиной, если поза не найдена (лимиты, зазор, самозазор, сближение звеньев). |
| метод | `SolveWaypointPose` | `public static double[] SolveWaypointPose(PoseValidator v, CollisionWorld world, Planner planner,` | Поза прохода через точку с учётом ограничения: сначала пробуются ветви IK планировщика (как в waypoint-редакторе), среди них выбирается САМАЯ «свободная» по ориентации и запасу лимитов; если ни одна ветвь не подошла — численный решатель с ограничением из текущей позы. |

### `enum Step`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvGripper.cs` (строка 242)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `approachHeight` | `public float approachHeight = 0.16f;` | Высота подхода над кубом, м. |
| поле | `cubeSize` | `public float cubeSize = 0.07f;` | Размер куба, м. |
| поле | `liftHeight` | `public float liftHeight = 0.20f;` | Высота подъёма после захвата, м. |
| поле | `maxStepAttempts` | `public int maxStepAttempts = 3;` |  |
| поле | `tableHeight` | `public float tableHeight = TrajectoryCore.StandBuilder.TopHeight;` | Высота стола, если не удалось определить (мир Y). |
| поле | `toolExtendsAlongNegativeY` | `public bool toolExtendsAlongNegativeY = true;` | Какое направление инструмента просить у `ToolAlign`. По соглашению проекта инструмент вытянут вдоль локальной −Y, поэтому «пальцы вниз» = нормаль −up (`true`). ИЗМЕРЕНО прогонами 14.09.2026: при `true` фактическое `ExtendWorld · up` = **+0.90**, при `false` = **+1.00** — то есть выравнивание на `… |
| поле | `transferOffset` | `public Vector3 transferOffset = new Vector3(0.35f, 0f, 0.25f);` | Смещение точки переноса от исходной позиции куба, м. |
| поле | `transferOffsetScara` | `public Vector3 transferOffsetScara = new Vector3(0.15f, 0f, 0.10f);` |  |
| свойство | `Cube` | `public GameObject Cube { get { return cube; } }` |  |
| свойство | `Current` | `public Step Current { get; private set; }` |  |
| свойство | `Running` | `public bool Running { get { return Current != Step.Idle && Current != Step.Done && Current != Step.Failed; } }` |  |
| событие | `Message` | `public event Action<string> Message;` |  |
| метод | `Bind` | `public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld, KvGripper grip)` |  |
| метод | `Cancel` | `public void Cancel(string why = "отменено")` |  |
| метод | `DestroyCube` | `public void DestroyCube()` |  |
| метод | `Run` | `public bool Run()` | Запустить демонстрацию (робот должен стоять, гриппер обязателен). |
| метод | `SpawnCube` | `public bool SpawnCube(bool replace = true)` | Положить куб на столешницу рядом с роботом. |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое ведение последовательности (вызывает хаб). |

## Пространство имён `TrajectoryCore`

### `class CollisionProxies`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\CollisionWorld.cs` (строка 45)
- **Назначение:** РЕЕСТР ПРОКСИ СТОЛКНОВЕНИЙ (ЭТАП 13 ТЗ: «low-poly collision mesh»). Оптимизатор (`KvCollisionOptimizer` в модуле функций) считает по мешам сцены упрощённые прокси — ориентированные боксы и капсулы — и кладёт их сюда. Мир столкновений спрашивает реестр при пересборке и, если прокси есть, используе…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Enabled` | `public static bool Enabled;` | Использовать прокси при пересборке мира (флаг ставит оптимизатор). |
| свойство | `BoxCount` | `public static int BoxCount { get { return boxes.Count; } }` |  |
| свойство | `CapsuleCount` | `public static int CapsuleCount { get { return capsules.Count; } }` |  |
| свойство | `Count` | `public static int Count { get { return boxes.Count + capsules.Count + skipped.Count; } }` |  |
| свойство | `SkippedCount` | `public static int SkippedCount { get { return skipped.Count; } }` |  |
| метод | `Clear` | `public static void Clear()` |  |
| метод | `SetBox` | `public static void SetBox(Renderer r, ObstacleBox box)` |  |
| метод | `SetCapsule` | `public static void SetCapsule(Renderer r, ObstacleCapsule capsule)` |  |
| метод | `SetSkipped` | `public static void SetSkipped(Renderer r)` | Пометить объект как «не препятствие» (мелкая деталь внутри другого объёма). |
| метод | `Status` | `public static string Status()` | Сколько прокси сейчас в реестре (диагностика). |
| метод | `TryGet` | `public static bool TryGet(Renderer r, out ObstacleBox box, out ObstacleCapsule capsule,` | Что делать с этим рендерером: бокс / капсула / пропустить / нет прокси. |

### `class CollisionWorld`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\CollisionWorld.cs` (строка 126)
- **Назначение:** Мир столкновений (E0/E1 плана): упрощённая капсульная модель окружения. * статика сцены (столы, детали) — боксы по габаритам мешей (вытянутые — капсулы); * пол — горизонтальная плоскость (по самой крупной плоской поверхности); * другие роботы — капсульные цепочки по их суставам; * очень крупные о…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Boxes` | `public readonly List<ObstacleBox> Boxes = new List<ObstacleBox>();` |  |
| поле | `Capsules` | `public readonly List<ObstacleCapsule> Capsules = new List<ObstacleCapsule>();` |  |
| поле | `FloorY` | `public float FloorY = float.NegativeInfinity;` |  |
| поле | `PickAndPlaceCubeName` | `public const string PickAndPlaceCubeName = "Куб_PickAndPlace";` | Имя куба pick-and-place. Куб — ЦЕЛЬ захвата, а не препятствие: если считать его препятствием, подход к нему отбраковывается SafetyGate по зазору («малый зазор») и демонстрация не выполняется вовсе. |
| свойство | `Version` | `public uint Version { get; private set; }` |  |
| метод | `DistancePointSegment` | `public static float DistancePointSegment(Vector3 p, Vector3 a, Vector3 b)` |  |
| метод | `DistanceToPoint` | `public float DistanceToPoint(Vector3 p)` | Минимальное расстояние от точки до мира (пол + капсулы + боксы). |
| метод | `MinDistanceChain` | `public float MinDistanceChain(IList<Vector3> nodes, float linkRadius)` | Минимальный зазор между капсульной цепочкой робота и миром. |
| метод | `PointBoxDistance` | `public static float PointBoxDistance(Vector3 p, ObstacleBox b)` | Точное расстояние точка–бокс (с учётом поворота прокси-бокса, этап 13). |
| метод | `Rebuild` | `public void Rebuild(RobotController skipRobot, float linkRadius = 0.06f)` | Пересобрать мир. basePos — позиция базы робота (для определения опоры). |
| метод | `SegmentBoxDistance` | `public static float SegmentBoxDistance(Vector3 a, Vector3 b, ObstacleBox box)` | Расстояние отрезок–бокс (сэмплирование отрезка). |
| метод | `SegmentSegmentDistance` | `public static float SegmentSegmentDistance(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)` | Минимальное расстояние между двумя отрезками (3D). |

### `enum FlowState`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\SelectionTypes.cs` (строка 9)
- **Назначение:** Состояния сценария (новый алгоритм подтверждения траектории) + режим перемещения точки.

_Публичных членов нет (или тип объявлен без них)._

### `class GhostMaterial`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\LaserAndPhantomManagers.cs` (строка 13)
- **Назначение:** Материалы-«призраки» (бирюзовые, полупрозрачные) и неоновые материалы (HDRP). Прозрачность включается ровно так, как это делает сам HDRP (см. BaseUnlitAPI): _SurfaceType = 1, ключ _SURFACE_TYPE_TRANSPARENT, _BlendMode = Alpha, _SrcBlend = One, _DstBlend = OneMinusSrcAlpha, _ZWrite = 0, очередь 30…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `MakeGhost` | `public static void MakeGhost(Material m, Color tint, float alpha)` |  |
| метод | `MakeNeon` | `public static void MakeNeon(Material m, Color color, float emission)` | «Ядовитый» неоновый материал (для шарика прицела и линий). |
| метод | `SetGhostGlow` | `public static void SetGhostGlow(Material m, Color tint, float alpha, float glow)` | Подсветка/затемнение фантома без пересоздания материала. |

### `struct GoalConfig`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\Planner.cs` (строка 144)
- **Назначение:** Конфигурация цели для планирования: вектор q + метка источника («IK S-,E+» / «CCD»).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `q` | `public double[] q;` |  |
| поле | `RecordTree` | `public static bool RecordTree;` | Включить запись дерева (ставит визуализатор этапа 15). |
| поле | `tag` | `public string tag;` |  |
| поле | `TreeNodesA` | `public readonly List<double[]> TreeNodesA = new List<double[]>();` | Узлы дерева A (от старта) — конфигурации суставов. |
| поле | `TreeNodesB` | `public readonly List<double[]> TreeNodesB = new List<double[]>();` | Узлы дерева B (от цели). |
| поле | `TreeParentsA` | `public readonly List<int> TreeParentsA = new List<int>();` | Индексы родителей узлов дерева A (−1 у корня). |
| поле | `TreeParentsB` | `public readonly List<int> TreeParentsB = new List<int>();` | Индексы родителей узлов дерева B (−1 у корня). |
| свойство | `LastBranchInfo` | `public string LastBranchInfo { get; private set; } = "";` |  |
| свойство | `TreeIterations` | `public int TreeIterations { get; private set; }` | Итераций в последней записи. |
| свойство | `TreeSolved` | `public bool TreeSolved { get; private set; }` | Последняя запись закончилась найденным путём. |
| свойство | `TreeVersion` | `public int TreeVersion { get; private set; }` | Счётчик записанных версий дерева (для интерфейса: «дерево обновилось»). |
| метод | `PlanToGoal` | `public PlannedTrajectory PlanToGoal(double[] start, double[] goalQ, int seed, string tag = "")` | Планирование К КОНКРЕТНОЙ КОНФИГУРАЦИИ ЦЕЛИ (ветви IK) — ЗАДАЧА 1 ТЗ «8 траекторий»: одна ветвь (плечо/локоть/запястье) → ровно ОДИН путь BiRRT с заданным seed'ом. Так 8 ветвей дают 8 РАЗНЫХ траекторий (робот приходит в точку разными позами), в отличие от `Plan`, который сам решает, сколько ветве… |
| метод | `PlanViaWaypoint` | `public PlannedTrajectory PlanViaWaypoint(double[] start, double[] goalQ, int seed, string tag,` | Путь ЧЕРЕЗ ПРОМЕЖУТОЧНУЮ КОНФИГУРАЦИЮ («обход») — вариация маршрута для набора «8 траекторий» (ТЗ: «разные seed'ы планировщика — если IK-конфигураций не хватает»). Зачем: в свободном пространстве BiRRT + short-cut всегда выпрямляет путь в прямую в пространстве суставов, поэтому 8 разных seed'ов д… |
| метод | `SolveGoalConfigs` | `public List<GoalConfig> SolveGoalConfigs(double[] start, Vector3 goalPoint, int maxCount, int seed)` | ВСЕ конфигурации цели, в которые планировщик готов вести робота (единый источник правды для `Plan` и для потока «8 траекторий»): 1) аналитические ветви IK — 6-осевой: до 8 (плечо влево/вправо × локоть вверх/вниз × запястье с переворотом/без), SCARA: до 4 (вылет вперёд/назад × локоть вверх/вниз); … |

### `class HierarchyOrder`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\SelectionTypes.cs` (строка 216)
- **Назначение:** Детерминированный порядок объектов: путь в иерархии сцены (как в окне Hierarchy). Нужен, чтобы выбор робота/узлов дерева не «мигал» от кадра к кадру (FindObjectsByType не гарантирует порядок).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Compare` | `public static int Compare(Transform a, Transform b)` |  |
| метод | `PathOf` | `public static string PathOf(Transform t)` |  |

### `struct IkBranchTag`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\IkSolver.cs` (строка 7)
- **Назначение:** Метки ветви решения IK (для «posture locking» и гистерезиса).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `elbowDown` | `public bool elbowDown; // локоть «вниз» (вторая ветвь)` |  |
| поле | `shoulderFar` | `public bool shoulderFar; // база повёрнута на 180°` |  |
| поле | `wristFlip` | `public bool wristFlip; // запястье перевёрнуто` |  |
| метод | `ToString` | `public override string ToString()` |  |

### `struct IkSolution`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\IkSolver.cs` (строка 19)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `fkError` | `public double fkError; // ошибка прямой задачи по TCP, м` |  |
| поле | `nearSingularity` | `public bool nearSingularity; // близко к вырождению (вытянута/сложена — SCARA; wrist-singular — 6R)` |  |
| поле | `q` | `public double[] q;` |  |
| поле | `tag` | `public IkBranchTag tag;` |  |
| поле | `withinLimits` | `public bool withinLimits;` |  |

### `class IkSolver`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\IkSolver.cs` (строка 39)
- **Назначение:** Аналитическая IK (MVP, Проблема 1): для 6-осевого робота с «сферическим» запястьем (оси 4-5-6 пересекаются) даём ВСЕ ветви: 2 (плечо) × 2 (локоть) × 2 (переворот запястья) = до 8 решений, плюс резервный многостартовый CCD. Вывод формул: позиционная часть — 2R в плоскости руки (теорема косинусов),…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `ccdIterations` | `public int ccdIterations = 120;` |  |
| поле | `ccdSeeds` | `public int ccdSeeds = 6;` |  |
| поле | `positionTolerance` | `public float positionTolerance = 0.004f; // 4 мм` |  |
| свойство | `LastAnalyticCount` | `public int LastAnalyticCount { get; private set; }` | Сколько решений последнего `SolveAllSeeded` дала АНАЛИТИКА (всё, что дальше, — добор CCD-сидами). Нужно вызывающему, чтобы честно подписать конфигурацию цели («IK S-,E+» или «CCD»), а не выдавать CCD-доводку за ветвь аналитики. |
| свойство | `LastScaraDebug` | `public string LastScaraDebug { get; private set; } = "";` |  |
| свойство | `Ready` | `public bool Ready { get; private set; }` |  |
| метод | `Init` | `public void Init(PoseValidator validator)` |  |
| метод | `SolveAll` | `public List<IkSolution> SolveAll(Vector3 target, double[] seed)` | Все ветви аналитической IK; при неудаче — многостартовый CCD. |
| метод | `SolveAllSeeded` | `public List<IkSolution> SolveAllSeeded(Vector3 target, double[] seed, int maxSolutions = 6)` | Расширенный набор решений для «фантомов» (MVP-3): аналитические ветви + детерминированный мультисид-CCD (текущая поза, «дом», зеркала локтя/плеча, переворот запястья). Дубликаты по конфигурации отбрасываются. |

### `class KinematicsJacobian`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\KinematicsJacobian.cs` (строка 10)
- **Назначение:** Численный анализ якобиана (E2 плана): позиционный якобиан 3×N конечными разностями, манипулируемость sqrt(det(J·Jᵀ)) и оценка σ_min (степенной метод по JᵀJ). Нужен для критерия «близко к сингулярности» и для скоринга траекторий.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Allocate` | `public static double[][] Allocate(int dof)` |  |
| метод | `Compute` | `public static void Compute(PoseValidator v, double[] q, double[][] jac, out Vector3 tcp)` |  |
| метод | `Manipulability` | `public static double Manipulability(double[][] j, int dof)` | Манипулируемость: sqrt(det(J·Jᵀ)) — мера близости к сингулярности. |
| метод | `SigmaMin` | `public static double SigmaMin(double[][] j, int dof)` | Оценка σ_min через степенной метод по JᵀJ (10 итераций, детерминированно). |

### `class LaserManager`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\LaserAndPhantomManagers.cs` (строка 95)
- **Назначение:** Менеджер лазеров (два луча): красный — выбор точки на поверхности, зелёный — подтверждение траектории и фантома. Лучи берутся из позы камеры и латеральных смещений рук — ровно там же, где рисованы сами лучи, поэтому «куда смотрю, туда и выбираю».

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `downOffset` | `public float downOffset = 0.15f;` |  |
| поле | `handOffset` | `public float handOffset = 0.35f;` |  |
| поле | `maxDistance` | `public float maxDistance = 50f;` |  |
| поле | `phantomPickRadius` | `public float phantomPickRadius = 0.18f;` |  |
| поле | `tubePickRadius` | `public float tubePickRadius = 0.05f; // радиус «колбаски» (1/20 юнита)` |  |
| свойство | `GreenAimValid` | `public bool GreenAimValid { get; private set; }` |  |
| свойство | `GreenHitPoint` | `public Vector3 GreenHitPoint { get; private set; }` |  |
| свойство | `GreenRay` | `public Ray GreenRay { get; private set; }` |  |
| свойство | `RedAimValid` | `public bool RedAimValid { get; private set; }` |  |
| свойство | `RedHitPoint` | `public Vector3 RedHitPoint { get; private set; }` |  |
| свойство | `RedRay` | `public Ray RedRay { get; private set; }` |  |
| метод | `UpdateRays` | `public void UpdateRays(Vector3 aimPoint, bool aimHit)` | Обновить оба луча по текущей позе камеры (вызывается каждый кадр). |

### `class MotionTiming`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\SelectionTypes.cs` (строка 235)
- **Назначение:** Таймирование движения под заданную скорость инструмента (м/с).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `RescaleToSpeed` | `public static void RescaleToSpeed(PlannedTrajectory t, float lengthM, float speedMps)` | Пересчитывает времена траектории так, чтобы средняя скорость TCP была speedMps (например 0.05 м/с = 1 м за 20 с), сохраняя форму профиля. |

### `class NarrowPhase`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\NarrowPhase.cs` (строка 66)
- **Назначение:** Узкая фаза (Проблема 2): GJK (пересечение выпуклых тел через разность Минковского) и EPA (глубина проникновения и нормаль контакта). Работает с любыми выпуклыми примитивами (капсулы, боксы, оболочки звеньев).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `CapsuleCapsuleDistance` | `public static float CapsuleCapsuleDistance(in Prim A, in Prim B)` | Быстрая проверка капсула-капсула (замкнутая формула) — для ссылок робота. |
| метод | `Intersect` | `public static bool Intersect(in Prim A, in Prim B)` | Пересекаются ли тела (GJK). |
| метод | `Penetration` | `public static bool Penetration(in Prim A, in Prim B, out float depth, out Vector3 normal)` | Глубина проникновения и нормаль. Реализация: минимизация функции перекрытия f(n) = h_A(n) + h_B(−n) по направлениям (грубая сетка + уточнение спуском), где h — опорные функции. Для выпуклых тел min f(n) = глубина проникновения. |

### `struct ObstacleBox`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\CollisionWorld.cs` (строка 17)
- **Назначение:** Осевой бокс препятствия (столешницы, плиты, ящики).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `center` | `public Vector3 center;` |  |
| поле | `half` | `public Vector3 half;` |  |
| поле | `name` | `public string name;` |  |
| поле | `rotation` | `public Quaternion rotation;` | ЭТАП 13 ТЗ: поворот бокса (низкополигональный прокси-меш). По умолчанию — единичный кватернион, то есть прежнее поведение «осевой бокс»: поле добавлено так, чтобы уже существующая геометрия сцены считалась ровно как раньше. |
| поле | `support` | `public bool support;` |  |

### `struct ObstacleCapsule`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\CollisionWorld.cs` (строка 7)
- **Назначение:** Капсула препятствия: отрезок A–B + радиус.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `a` | `public Vector3 a;` |  |
| поле | `b` | `public Vector3 b;` |  |
| поле | `name` | `public string name;` |  |
| поле | `r` | `public float r;` |  |
| поле | `support` | `public bool support; // опора робота (стол, на котором он стоит) — не считается столкновением` |  |

### `struct Outcome`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\ToolAlign.cs` (строка 21)
- **Назначение:** Итог выравнивания. q — выровненная поза, либо исходная при ok == false.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `angleDeg` | `public float angleDeg; // остаточный угол между нормалью «пятака» и нормалью поверхности` |  |
| поле | `intoSurface` | `public bool intoSurface; // true — инструмент развёрнут «в поверхность» (−нормаль)` |  |
| поле | `ok` | `public bool ok;` |  |
| поле | `posErrM` | `public float posErrM; // ошибка TCP по позиции, м` |  |
| поле | `q` | `public double[] q;` |  |
| поле | `why` | `public string why; // почему не удалось (для лога)` |  |
| метод | `AlignGoal` | `public static Outcome AlignGoal(PoseValidator v, CollisionWorld world, double[] qGoal,` | Подгоняет позу цели так, чтобы TCP остался в target, а «пятак» встал параллельно плоскости с нормалью surfaceNormal. Сначала пробуется направление «в поверхность» (−нормаль), затем разворот оси (+нормаль): «пятак» параллелен в обоих случаях. |

### `struct PhantomConfig`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\SelectionTypes.cs` (строка 38)
- **Назначение:** Вариант конфигурации робота (решение IK) для фантома.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `cost` | `public double cost; // стоимость позы (PostureSelector)` |  |
| поле | `fkError` | `public float fkError; // ошибка прямой задачи, м` |  |
| поле | `ghost` | `public GameObject ghost; // визуал` |  |
| поле | `index` | `public int index;` |  |
| поле | `q` | `public double[] q;` |  |
| поле | `sigmaMin` | `public float sigmaMin; // м/град` |  |
| поле | `tag` | `public IkBranchTag tag;` |  |
| поле | `tint` | `public Color tint; // оттенок бирюзового (различимость конфигураций)` |  |
| метод | `Label` | `public string Label => "Фантом " + (index + 1) + " [" + tag + "]";` |  |

### `class PhantomManager`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\LaserAndPhantomManagers.cs` (строка 168)
- **Назначение:** Менеджер фантомов: полупрозрачные БИРЮЗОВЫЕ копии модели. * ShowAllAlongPaths — НОВЫЙ АЛГОРИТМ (шаг 2 ТЗ): по ОДНОМУ фантому на КАЖДУЮ сгенерированную траекторию; все стартуют одновременно из текущей позы робота и едут каждый по своему пути со скоростью phantomMoveSpeed; * KeepOnly(index) — шаг 3…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `appearAtTargetPose` | `public bool appearAtTargetPose = false;` |  |
| поле | `arrived` | `public bool arrived;` |  |
| поле | `castShadowsLikeRobot` | `public bool castShadowsLikeRobot = true;` |  |
| поле | `cfg` | `public PhantomConfig cfg;` |  |
| поле | `cursor` | `public int cursor;` |  |
| поле | `ghostAlpha` | `public float ghostAlpha = 0.55f; // полупрозрачность старого «рентген»-призрака` |  |
| поле | `ghostGlow` | `public float ghostGlow = 0.45f; // свечение старого «призрака»` |  |
| поле | `GhostHideFlags` | `public const HideFlags GhostHideFlags = HideFlags.HideInHierarchy;` | Флаги служебных объектов: скрыто из Hierarchy, но НЕ DontSave. |
| поле | `ghostTint` | `public Color ghostTint = new Color(0.10f, 0.92f, 0.95f, 1f); // бирюзовый (старый)` |  |
| поле | `joints` | `public Transform[] joints;` |  |
| поле | `matchRealAppearance` | `public bool matchRealAppearance = true;` |  |
| поле | `maxTravelTime` | `public float maxTravelTime = 240f;` |  |
| поле | `minTravelTime` | `public float minTravelTime = 0.25f;` |  |
| поле | `moveSpeed` | `public float moveSpeed = 1f / 30f; // 1 юнит за 30 секунд (переезд в Show)` |  |
| поле | `outlineOnHover` | `public bool outlineOnHover = true;` |  |
| поле | `path` | `public double[][] path;` |  |
| поле | `pathSamples` | `public int pathSamples = 24; // сэмплов для оценки длины переезда/пути` |  |
| поле | `pathSpeed` | `public float pathSpeed = 0.5f;` |  |
| поле | `pickRadius` | `public float pickRadius = 0.20f; // радиус захвата лучом` |  |
| поле | `pose` | `public double[] pose;` |  |
| поле | `pulseHz` | `public float pulseHz = 1.6f; // «дыхание» фантома в полёте` |  |
| поле | `pz` | `public Vector3 pz;` |  |
| поле | `renderers` | `public Renderer[] renderers;` |  |
| поле | `scara` | `public ScaraBaseline scara;` |  |
| поле | `times` | `public float[] times;` |  |
| метод | `AllArrived` | `public bool AllArrived` | Все фантомы стоят в своих конечных позах. |
| метод | `ArrivedOf` | `public bool ArrivedOf(int index)` | Дошёл ли i-й фантом до конечной позы. |
| метод | `ClearPhantoms` | `public void ClearPhantoms() { DestroyGhosts(true); }` | Тот же полный сброс, но по смыслу «перед созданием нового фантома» (ТЗ: если используется пул — очищай его перед созданием нового). После неё в сцене не остаётся ни одной копии — фантом всегда ОДИН. |
| метод | `Configs` | `public IReadOnlyList<PhantomConfig> Configs => configs;` |  |
| метод | `Count` | `public int Count => configs.Count;` |  |
| метод | `CyanShade` | `public static Color CyanShade(int index, int total)` | Оттенок бирюзового для фантома k из total: cyan → turquoise → aqua. Разные оттенки — фантомы визуально различимы между собой. При 8 фантомах (ТЗ «8 траекторий») одного тона мало, поэтому соседние номера различаются ещё и светлотой — иначе 8 бирюзовых копий читались бы как одно пятно. |
| метод | `Destroy` | `public void Destroy()` |  |
| метод | `DurationOf` | `public float DurationOf(int index)` | Длительность прохода i-го фантома по своей траектории, с (диагностика/HUD). |
| метод | `ExpectedTravelTime` | `public float ExpectedTravelTime(float pathLength, float speedUnitsPerSec)` | Сколько времени займёт проход пути при заданной скорости (без клампов) — нужно проверкам, что фантом действительно быстрее робота ровно в multiplier раз. |
| метод | `Hide` | `public void Hide() { DestroyGhosts(true); }` | Полная уборка фантомов (Esc, выбор, новая точка красным лазером). Делает то же, что ClearPhantoms: уничтожаются и зарегистрированные копии, и всё, что осталось в контейнере, и сам контейнер, и «осиротевшие» объекты Phantom_*/Phantoms верхнего уровня сцены, и материалы-инстансы. |
| метод | `Hide` | `public void Hide()` |  |
| метод | `HoverIndex` | `public int HoverIndex(Ray ray, Vector3 aimPoint)` | Ближайший фантом к лучу. Учитывается и луч (объём модели), и точка прицела (шарик лазера) — выбор работает и в движении, и после остановки. |
| метод | `HoverIndex` | `public int HoverIndex(Ray ray) => HoverIndex(ray, ray.origin + ray.direction * 10f);` |  |
| метод | `Init` | `public void Init(RobotController robotTemplate, PoseValidator poseValidator)` |  |
| метод | `KeepOnly` | `public bool KeepOnly(int index)` | ОСТАВИТЬ ТОЛЬКО ОДИН ФАНТОМ (шаг 3 ТЗ: «остальные траектории и фантомы скрываются/удаляются»). Остальные копии удаляются, а у выбранного СОХРАНЯЕТСЯ прогресс — он продолжает движение (или уже доехал). |
| метод | `MovingCount` | `public int MovingCount` | Какие фантомы ещё едут (для подсказок и проверок). |
| метод | `PathLengthOf` | `public float PathLengthOf(int index)` | Длина TCP-пути i-го фантома, м (диагностика/HUD). |
| метод | `PoseOf` | `public double[] PoseOf(int index)` | Текущая КОНФИГУРАЦИЯ i-го фантома (последняя применённая поза) — диагностика и HUD: по ней проверяется, что фантом идёт РОВНО по своей траектории и не «телепортируется» (см. `validator.TcpAt(PoseOf(i))` — точка инструмента фантома). Массив отдаётся по ссылке и вызывающим кодом не изменяется. |
| метод | `ProgressOf` | `public float ProgressOf(int index)` | Доля переезда i-го фантома (0..1). |
| метод | `Pulse` | `public void Pulse(float k)` |  |
| метод | `SetHighlight` | `public void SetHighlight(int index)` | Подсветка фантома, на который наведён зелёный луч (ЭТАП 1): вокруг копии включается ТОНКИЙ КОНТУР (12 прутьев, HDRP/Unlit), а материалы модели не изменяются ни на йоту — поэтому без наведения фантом неотличим от реального робота. |
| метод | `Show` | `public void Show(List<IkSolution> solutions, List<int> indices, double[] startQ, float speedUnitsPerSec)` | Показать фантомы по списку конфигураций IK (задел на несколько фантомов). Плавный переезд из стартовой позы в конечную; при appearAtTargetPose = true копия ставится сразу в конечную позу. Рабочий путь одного фантома по траектории — ShowAlongPath (ниже). |
| метод | `Show` | `public void Show(Transform value)` |  |
| метод | `ShowAllAlongPaths` | `public bool ShowAllAlongPaths(List<PlannedTrajectory> plans, double[] startQ, float speedUnitsPerSec)` | ФАНТОМЫ ПО КАЖДОЙ ТРАЕКТОРИИ (новый алгоритм, шаг 2 ТЗ): по одному фантому на каждую сгенерированную траекторию. Все стартуют ОДНОВРЕМЕННО из текущей позы робота и едут каждый по своему пути с одной и той же скоростью (phantomMoveSpeed). ПЕРЕД созданием — полная уборка: в сцене живут ровно эти ко… |
| метод | `ShowAlongPath` | `public bool ShowAlongPath(PlannedTrajectory plan, double[] startQ, float speedUnitsPerSec)` | ОДИН фантом по ВЫБРАННОЙ траектории (используется при ПЕРЕКЛЮЧЕНИИ траектории — шаг 4 ТЗ: «фантом перезапускается, либо создаётся заново»). Полная уборка + один новый фантом, который начинает анимацию с текущей позы робота. |
| метод | `Tick` | `public void Tick(float dt)` |  |
| метод | `Tick` | `public void Tick(float deltaTime)` | Кадровое обновление фантомов. Ровно ОДИН шаг на кадр (Tick зовут и поток, и Update): * фантом с ПУТЁМ (ShowAlongPath) идёт по сэмплам подтверждённой траектории с её профилем времени — движение видимое и не по прямой; флаг appearAtTargetPose на него НЕ влияет (он только для многофантомного задела)… |

### `class PhantomMath`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\SelectionTypes.cs` (строка 159)
- **Назначение:** Немного геометрии для фантомов: выбор «максимально разных» ветвей.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `ConfigDistance` | `public static double ConfigDistance(double[] a, double[] b, double[] ranges)` |  |
| метод | `PickDistinct` | `public static List<int> PickDistinct(List<IkSolution> solutions, double[] ranges, int count)` | Жадный выбор МАКСИМАЛЬНО РАЗНЕСЁННЫХ по конфигурации ветвей (farthest-point sampling): первая — лучшая по стоимости, далее — та, что дальше всех от уже выбранных. Даёт визуально различимые фантомы (elbow up/down, shoulder left/right, wrist flip). |

### `class PlannedTrajectory`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\Planner.cs` (строка 7)
- **Назначение:** Готовая траектория: сэмплы q(t) + метрики.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `BranchTag` | `public string BranchTag = "";` | Метка источника траектории — ветвь IK (`S-,E+,W-`) или вариация seed'а планировщика (`seed 1000+…`). Нужна оператору (подпись «колбаски») и отчёту: по ней видно, чем именно 8 вариантов отличаются друг от друга. |
| поле | `Curvature` | `public double Curvature;` | Средняя «кривизна» пути в нормированном пространстве суставов — средний угол поворота направления движения между сэмплами (град). 0 — путь прямой; больше — путь «петляет». Один из критериев сортировки вариантов (ТЗ: длина, время, кривизна). |
| поле | `CurvatureMax` | `public double CurvatureMax;` | МАКСИМАЛЬНАЯ кривизна (град) — самый резкий перелом направления на пути. |
| поле | `CurvatureSamples` | `public int CurvatureSamples;` | Сколько сэмплов участвовало в оценке кривизны (для интерпретации суммы). |
| поле | `CurvatureTotal` | `public double CurvatureTotal;` | СУММАРНАЯ кривизна пути (град) = сумма углов поворота направления между сэмплами. Это «сколько всего путь петляет»: у прямого пути ≈0, у зигзага — сотни градусов. Метрика панели траекторий (ЗАДАЧА 5 ТЗ). |
| поле | `Label` | `public string Label = "";` |  |
| поле | `Length` | `public double Length; // нормированная длина пути` |  |
| поле | `LimitMargin` | `public float LimitMargin; // минимальный запас до лимитов, град` |  |
| поле | `MinClearance` | `public float MinClearance; // минимальный зазор, м` |  |
| поле | `Path` | `public double[][] Path; // сэмплы конфигураций` |  |
| поле | `Score` | `public double Score; // итоговый (меньше — лучше)` |  |
| поле | `SigmaMin` | `public double SigmaMin; // оценка σ_min в цели` |  |
| поле | `Time` | `public double Time; // полное время, с` |  |
| поле | `Times` | `public float[] Times; // время каждого сэмпла, с` |  |
| метод | `GoalQ` | `public double[] GoalQ => Path != null && Path.Length > 0 ? Path[Path.Length - 1] : null;` |  |

### `class Planner`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\Planner.cs` (строка 55)
- **Назначение:** Планировщик (E2–E3 плана): BiRRT-Connect в пространстве обобщённых координат с проверкой столкновений и лимитов, сглаживание short-cut, параметризация времени (трапеция по лимитам скоростей) и скоринг нескольких кандидатов. Детерминирован: один seed → один результат.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `accelShare` | `public float accelShare = 0.25f; // доля времени на разгон/торможение` |  |
| поле | `clearance` | `public float clearance = 0.02f;` |  |
| поле | `maxIterations` | `public int maxIterations = 2500;` |  |
| поле | `minLimitMarginDeg` | `public float minLimitMarginDeg = 3f; // как SafetyGate.minLimitMarginDeg` |  |
| поле | `selfClearance` | `public float selfClearance = 0.015f; // запас между своими звеньями` |  |
| поле | `stepSizeDeg` | `public double stepSizeDeg = 12.0;` |  |
| поле | `timeStep` | `public float timeStep = 0.02f; // шаг сэмплирования траектории` |  |
| поле | `wCurvature` | `public double wCurvature = 0.35; // вклад кривизны пути (ТЗ: сортировка по длине/времени/кривизне)` |  |
| поле | `wPosture` | `public double wPosture = 0.05; // вклад стоимости позы (PostureSelector)` |  |
| поле | `wTime` | `public double wTime = 1.0, wLength = 0.6, wClearance = 2.0, wLimit = 0.5, wSigma = 0.5;` |  |
| свойство | `LastDebug` | `public string LastDebug { get; private set; } = "";` |  |
| свойство | `LastIterations` | `public int LastIterations { get; private set; }` |  |
| свойство | `Ready` | `public bool Ready { get; private set; }` |  |
| метод | `Init` | `public void Init(PoseValidator validator, CollisionWorld collisionWorld)` |  |
| метод | `Plan` | `public List<PlannedTrajectory> Plan(double[] start, Vector3 goalPoint, int maxCandidates, int seed)` | Планирование до точки; возвращает до maxCandidates вариантов (отсортированы). |

### `class PoseValidator`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\PoseValidator.cs` (строка 13)
- **Назначение:** Валидатор позы: FK по вектору обобщённых координат q → капсульная цепочка звеньев → зазор до мира. Для 6-осевого применяет углы к реальным трансформам и возвращает позу назад (дешёвый «виртуальный» FK); для SCARA считает аналитически, не трогая сцену. q для 6-осевого: углы вокруг осей суставов от…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `linkRadius` | `public float linkRadius = 0.06f;` |  |
| поле | `pos` | `public Vector3[] pos;` |  |
| поле | `rot` | `public Quaternion[] rot;` |  |
| свойство | `Dof` | `public int Dof { get; private set; }` |  |
| свойство | `LastIkError` | `public float LastIkError { get; private set; }` | Ошибка последнего решения IK (м) — для диагностики. |
| свойство | `Lower` | `public float[] Lower { get; private set; }` |  |
| свойство | `Ready` | `public bool Ready { get; private set; }` |  |
| свойство | `RobotName` | `public string RobotName { get; private set; }` |  |
| свойство | `Upper` | `public float[] Upper { get; private set; }` |  |
| свойство | `VelMax` | `public float[] VelMax { get; private set; }` |  |
| метод | `Apply` | `public void Apply(double[] q)` | Применить конфигурацию (реальные трансформы). Для SCARA — тоже (yaw + z). |
| метод | `ApplyScaraToCopy` | `public void ApplyScaraToCopy(Transform copyRoot, double[] q)` | Применить конфигурацию SCARA к КОПИИ робота (фантом): θ1/θ2 — доворот звеньев копии относительно их эталонных направлений, z — вертикальный сдвиг z_5. Работает на копии, реального робота не трогает. |
| метод | `ApplyToCopy` | `public void ApplyToCopy(Transform[] copyJoints, double[] q)` | Применить конфигурацию к КОПИИ робота (фантом): суставы копии берутся по именам, соглашение то же (AngleAxis(q, q0·e) · q0), т.к. копия идентична в покое. |
| метод | `AxisWorld` | `public Vector3 AxisWorld(int i, double[] q)` | Мировое направление оси сустава i в конфигурации q (для z SCARA — вертикаль). |
| метод | `BasePosition` | `public Vector3 BasePosition => basePos;` | Мировая позиция базы робота (для отсчёта хода z_5 и фантомов). |
| метод | `ClearanceAt` | `public float ClearanceAt(double[] q, CollisionWorld world, out Vector3 tcp, out Vector3[] nodes)` | Зазор до мира в конфигурации q (поза восстанавливается). |
| метод | `ContinueFrom` | `public double[] ContinueFrom(double[] previous, double[] q)` | Приводит конфигурацию к НЕПРЕРЫВНОМУ виду относительно предыдущей: каждый вращательный сустав берёт кратчайший доворот (без скачков ±360°). Нужно исполнителю движения, иначе последний шаг «проворачивает» сустав на 360°. |
| метод | `CopyCurrent` | `public double[] CopyCurrent()` |  |
| метод | `CopyCurrentInto` | `public void CopyCurrentInto(double[] q)` | Текущая конфигурация робота в ГОТОВЫЙ массив (без аллокаций) — для визуализации, которой поза нужна каждый кадр (индикаторы лимитов суставов). Семантика та же, что у CopyCurrent. |
| метод | `FindCopyJoints` | `public Transform[] FindCopyJoints(Transform copyRoot)` | Найти суставы в иерархии копии (для фантомов). |
| метод | `FoldAngle` | `public double FoldAngle(int i, double angle)` | Эквивалентный угол в пределах хода сустава (ближайший по модулю 360°). |
| метод | `Init` | `public void Init(RobotController robot)` |  |
| метод | `IsPrismatic` | `public bool IsPrismatic(int i)` | Углы вращения заданы по модулю 360°, поэтому «в лимитах» проверяем эквивалентный угол (для призматической оси SCARA — само значение). |
| метод | `Jacobian` | `public void Jacobian(double[] q, double[][] jac, out Vector3 tcp)` | Аналитический (численный) позиционный Якобиан 3×N в конфигурации q. J_k = ∂p_tcp/∂q_k (конечные разности, шаг 0.25°; для призмы SCARA — метры). |
| метод | `JointFrames` | `public void JointFrames(double[] q, Vector3[] pivots, Vector3[] axesOut)` | Пивоты и МИРОВЫЕ оси ВСЕХ суставов за ОДНО применение позы (вместо Dof вызовов PivotAt/AxisWorld, каждый из которых «примеряет» позу): нужна визуализации лимитов. Поза восстанавливается, реальный робот не двигается. |
| метод | `LimitMargin` | `public float LimitMargin(double[] q)` | Запас до лимитов в сопоставимых единицах (градусы). ВАЖНО: для призматической оси SCARA (z_5, ход ~0,2 м) метры приводятся к «градусам» (полный ход = 360°). Иначе порог SafetyGate (3°) отбраковывал ЛЮБУЮ позу SCARA: запас 0,07 м численно меньше 3 и трактовался как «на пределе». |
| метод | `LimitMargins` | `public void LimitMargins(double[] q, float[] result)` | Запас до лимитов КАЖДОГО сустава в «градусах» (у призмы SCARA полный ход = 360° — та же нормировка, что в LimitMargin). Нужна визуализации лимитов: по ней красится каждый сустав отдельно (зелёный/жёлтый/красный). |
| метод | `MaxJacobianNorm` | `public float MaxJacobianNorm(double[] q)` | Максимальная норма строки Якобиана ‖∂p/∂q_k‖∞ — для адаптивного шага рёбер. |
| метод | `PivotAt` | `public Vector3 PivotAt(int i, double[] q)` | Мировая позиция пивота сустава i в конфигурации q (поза восстанавливается). |
| метод | `ReadChain` | `public Vector3[] ReadChain()` | Прочитать капсульную цепочку (после Apply). |
| метод | `SelfClearance` | `public float SelfClearance(double[] q, out int linkA, out int linkB)` | Самоколлизия звеньев (Проблема 2): минимальный зазор между несоседними звеньями робота. Возвращает зазор (м, минус — пересечение) и индексы звеньев. |
| метод | `SolveIk` | `public bool SolveIk(Vector3 goal, double[] seed, out double[] result,` | CCD-IK в пространстве обобщённых координат (работает и для SCARA, и для 6-осевого): вращает каждый сустав вокруг своей мировой оси; для SCARA дополнительно доводит z. |
| метод | `SolveIkRange` | `public bool SolveIkRange(Vector3 goal, double[] q, int first, int last,` | CCD только по подмножеству суставов [first..last] (например, доводка запястья 3..5, когда позиционная часть уже решена аналитически). q меняется на месте. |
| метод | `TcpAt` | `public Vector3 TcpAt(double[] q)` | Позиция TCP в конфигурации q (поза восстанавливается). |
| метод | `WithinLimits` | `public bool WithinLimits(double[] q, float marginDeg = 0f)` |  |

### `class PostureControl`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\PostureControl.cs` (строка 17)
- **Назначение:** Постур-контроль (Проблема 1, п.1.4): движение к цели с проекцией вторичной цели в нулевое пространство Якобиана — робот доезжает до точки, одновременно «уходя» от лимитов и сингулярностей, не сбиваясь с траектории. J# = Jᵀ (J Jᵀ + λ²I)⁻¹ (демпфированный псевдообратный) λ² = λ0²(1 − σ_min/σ_th)² п…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `alpha` | `public float alpha = 0.6f; // вклад нулевого пространства` |  |
| поле | `gain` | `public float gain = 0.5f; // k для градиента вторичной цели` |  |
| поле | `lambda0` | `public float lambda0 = 0.05f; // базовая демпфирующая добавка` |  |
| поле | `maxStepDeg` | `public float maxStepDeg = 3f; // ограничение шага на кадр (град)` |  |
| поле | `sigmaThreshold` | `public float sigmaThreshold = 0.05f; // порог σ_min (м/град)` |  |
| свойство | `LastNullSpaceGain` | `public float LastNullSpaceGain { get; private set; }` |  |
| свойство | `LastSigmaMin` | `public float LastSigmaMin { get; private set; }` |  |
| свойство | `Ready` | `public bool Ready { get; private set; }` |  |
| метод | `Init` | `public void Init(PoseValidator validator, PostureSelector postureSelector)` |  |
| метод | `Step` | `public double[] Step(double[] q, Vector3 targetPos, double dt)` | Один шаг: сместить TCP к targetPos (мировые координаты), сохранив позу удобной. Возвращает новую конфигурацию (не меняя сцену). |

### `class PostureSelector`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\PostureSelector.cs` (строка 18)
- **Назначение:** Селектор позы (Проблема 1): выбирает «удобную» конфигурацию из всех ветвей IK по функции стоимости + гистерезис (posture locking). S(q) = w_home·||q−q_home||²_W + w_lim·Σφ_lim(q_i) + w_sing·φ_sing(q) + w_jump·||q−q_prev||²_W + w_fk·(ошибка FK)² φ_lim = ((qmax−qmin)²)/((qmax−q)(q−qmin)) (барьер, C…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `hysteresis` | `public float hysteresis = 0.15f; // 15 % — порог смены ветви` |  |
| поле | `sigmaThreshold` | `public float sigmaThreshold = 0.05f; // м/град (после нормировки Якобиана)` |  |
| поле | `wFk` | `public float wFk = 500f;` |  |
| поле | `wHome` | `public float wHome = 0.35f;` |  |
| поле | `wJump` | `public float wJump = 1.0f;` |  |
| поле | `wLimit` | `public float wLimit = 1.0f;` |  |
| поле | `wSing` | `public float wSing = 0.6f;` |  |
| свойство | `LastManipulability` | `public float LastManipulability { get; private set; }` |  |
| свойство | `LastSigmaMin` | `public float LastSigmaMin { get; private set; }` |  |
| свойство | `Ready` | `public bool Ready { get; private set; }` |  |
| метод | `Cost` | `public double Cost(double[] q, double[] qPrev)` | Стоимость конфигурации; меньше — лучше. |
| метод | `Init` | `public void Init(PoseValidator validator, double[] homePose = null)` |  |
| метод | `ResetLock` | `public void ResetLock() { hasLock = false; }` |  |
| метод | `Select` | `public IkSolution Select(System.Collections.Generic.List<IkSolution> candidates, double[] qPrev)` | Выбор решения с гистерезисом: держимся за ветвь, пока она адекватна. |

### `struct Prim`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\NarrowPhase.cs` (строка 8)
- **Назначение:** Выпуклый примитив с опорной функцией h_K(d) = max_{x∈K} dᵀx.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `half` | `public Vector3 half; // box` |  |
| поле | `hull` | `public Vector3[] hull; // hull` |  |
| поле | `kind` | `public PrimKind kind;` |  |
| поле | `r` | `public float r; // sphere/capsule` |  |
| метод | `Box` | `public static Prim Box(Vector3 c, Vector3 half)` |  |
| метод | `Capsule` | `public static Prim Capsule(Vector3 a, Vector3 b, float r)` |  |
| метод | `Hull` | `public static Prim Hull(Vector3[] verts)` |  |
| метод | `Sphere` | `public static Prim Sphere(Vector3 c, float r)` |  |
| метод | `Support` | `public Vector3 Support(Vector3 dir)` |  |

### `enum PrimKind`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\NarrowPhase.cs` (строка 5)

_Публичных членов нет (или тип объявлен без них)._

### `class ReachabilityOracle`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\ReachabilityOracle.cs` (строка 30)
- **Назначение:** Reachability Oracle (E1 плана): онлайн-вердикт по точке прицела. * SCARA — аналитически: кольцо рабочей зоны + ход z + капсулы корпуса (2 ветви локтя); * 6-осевой — численно: seeded CCD в текущей позе, затем проверка капсульной цепочки. Результат кэшируется по вокселю 1 см и сбрасывается при смен…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `bodyZoneRadius` | `public float bodyZoneRadius = 0.15f;` |  |
| поле | `ccdIterations` | `public int ccdIterations = 24;` |  |
| поле | `ccdTolerance` | `public float ccdTolerance = 0.01f;` |  |
| поле | `clearance` | `public float clearance = 0.02f; // требуемый запас, м` |  |
| поле | `linkRadius` | `public float linkRadius = 0.06f; // радиус звеньев, м` |  |
| поле | `workZoneRadius` | `public float workZoneRadius = 1.6f;` |  |
| свойство | `Ready` | `public bool Ready { get; private set; }` |  |
| свойство | `RobotName` | `public string RobotName { get; private set; }` |  |
| метод | `Init` | `public void Init(RobotController robot, CollisionWorld collisionWorld)` |  |
| метод | `Query` | `public ReachResult Query(Vector3 point)` | Вердикт по точке прицела (мир). |

### `struct ReachResult`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\ReachabilityOracle.cs` (строка 15)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `clearance` | `public float clearance; // минимальный зазор, м (минус = пересечение)` |  |
| поле | `reason` | `public string reason; // человекочитаемая причина` |  |
| поле | `stampMs` | `public long stampMs;` |  |
| поле | `tcp` | `public Vector3 tcp; // где окажется инструмент` |  |
| поле | `verdict` | `public ReachVerdict verdict;` |  |

### `enum ReachVerdict`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\ReachabilityOracle.cs` (строка 7)

_Публичных членов нет (или тип объявлен без них)._

### `class SafetyGate`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\SafetyGate.cs` (строка 12)
- **Назначение:** Safety Layer (E4 плана): независимая проверка траектории и «сторожевой таймер». Проверки: лимиты суставов (с запасом), минимальный зазор, предельные скорости, актуальность данных. При отказе — движение не разрешается (fail-safe).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Enabled` | `public bool Enabled = true;` |  |
| поле | `minClearance` | `public float minClearance = 0.015f;` |  |
| поле | `minLimitMarginDeg` | `public float minLimitMarginDeg = 3f;` |  |
| поле | `watchdogTimeoutMs` | `public int watchdogTimeoutMs = 300;` |  |
| свойство | `LastReason` | `public SafetyReason LastReason { get; private set; } = SafetyReason.NotChecked;` |  |
| метод | `Approve` | `public bool Approve(PlannedTrajectory t, PoseValidator validator, out SafetyReason reason)` | Проверка траектории перед исполнением. |
| метод | `ApproveStep` | `public bool ApproveStep(double[] q, float clearance, double playheadTime, PoseValidator validator,` | Проверка на шаге исполнения: не вышли ли за пределы/не застряли ли. |
| метод | `Describe` | `public static string Describe(SafetyReason r)` |  |
| метод | `NotifyState` | `public void NotifyState() { lastStateMs = (long)(Time.realtimeSinceStartup * 1000f); }` |  |
| метод | `ResetPlayhead` | `public void ResetPlayhead() { lastPlayheadTime = -1; }` |  |
| метод | `WatchdogAlive` | `public bool WatchdogAlive()` |  |

### `enum SafetyReason`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\SafetyGate.cs` (строка 5)

_Публичных членов нет (или тип объявлен без них)._

### `class SelectionState`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\SelectionTypes.cs` (строка 52)
- **Назначение:** Текущее состояние выбора (единая точка правды для UI и логики).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `aimAtLock` | `public Vector3 aimAtLock; // прицел в момент фиксации точки` |  |
| поле | `candidates` | `public readonly List<TrajectoryCandidate> candidates = new List<TrajectoryCandidate>();` |  |
| поле | `hasPoint` | `public bool hasPoint;` |  |
| поле | `hoveredPhantom` | `public int hoveredPhantom = -1;` |  |
| поле | `hoveredTrajectory` | `public int hoveredTrajectory = -1;` |  |
| поле | `moveOrigin` | `public Vector3 moveOrigin; // позиция на входе в режим (для отмены по Esc)` |  |
| поле | `movePoint` | `public Vector3 movePoint; // текущая позиция перемещаемой точки (цель TCP)` |  |
| поле | `moveReason` | `public string moveReason = ""; // причина вердикта (для HUD/подсказки)` |  |
| поле | `moveVerdict` | `public ReachVerdict moveVerdict = ReachVerdict.Safe; // онлайн-вердикт оракула` |  |
| поле | `moveVerdictValid` | `public bool moveVerdictValid; // был ли уже получен вердикт для текущей позиции` |  |
| поле | `phantoms` | `public readonly List<PhantomConfig> phantoms = new List<PhantomConfig>();` |  |
| поле | `phase` | `public FlowState phase = FlowState.Idle;` |  |
| поле | `point` | `public Vector3 point; // зафиксированная точка (цель TCP)` |  |
| поле | `selectedPhantom` | `public int selectedPhantom = -1;` |  |
| поле | `selectedTrajectory` | `public int selectedTrajectory = -1;` |  |
| метод | `ClearAll` | `public void ClearAll()` |  |
| метод | `InMoveMode` | `public bool InMoveMode => phase == FlowState.PointMoveMode;` |  |
| метод | `ResetTrajectorySelection` | `public void ResetTrajectorySelection()` |  |

### `class StandBuilder`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\StandBuilder.cs` (строка 26)
- **Назначение:** Стенды (две «сцены» с оборудованием): Стенд 1: стол в (0,0,−20.7) → 6-осевой робот на нём; Стенд 2: стол в (0,0,−31.3) → SCARA на нём. ГЕОМЕТРИЯ (ТЗ сессии 13.09.2026 — «увеличить столы в 3 раза»): столешница 14.4 × 9.6 юнита (было 4.8 × 3.2, то есть ×3 по ширине и глубине), высота НЕ менялась (в…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `GapBetweenTables` | `public const float GapBetweenTables = 1f; // зазор между ближними краями (не менялся)` |  |
| поле | `LegInset` | `public const float LegInset = 0.1f; // отступ ножки от края столешницы` |  |
| поле | `Robot1CopyName` | `public const string Robot1CopyName = "Робот_6ос_Стенд1";` |  |
| поле | `Robot2CopyName` | `public const string Robot2CopyName = "SCARA_Стенд2";` |  |
| поле | `RobotInsetFromInnerEdge` | `public const float RobotInsetFromInnerEdge = 1.6f; // робот у внутренней кромки (как в сцене)` |  |
| поле | `Stand1Name` | `public const string Stand1Name = "Стенд_1_Стол";` |  |
| поле | `Stand1Pos` | `public static readonly Vector3 Stand1Pos = new Vector3(0f, 0f, -20.7f);` |  |
| поле | `Stand2Name` | `public const string Stand2Name = "Стенд_2_Стол";` |  |
| поле | `Stand2Pos` | `public static readonly Vector3 Stand2Pos = new Vector3(0f, 0f, -31.3f);` |  |
| поле | `TableDepth` | `public const float TableDepth = 9.6f; // было 3.2 → ×3 (исходно 0.8 → ×12)` |  |
| поле | `TableWidth` | `public const float TableWidth = 14.4f; // было 4.8 → ×3 (исходно 1.2 → ×12)` |  |
| поле | `TopHeight` | `public const float TopHeight = 0.98f; // верхняя плоскость (роботы стоят на ней)` |  |
| поле | `TopThickness` | `public const float TopThickness = 0.05f; // толщина столешницы` |  |
| метод | `EnsureStands` | `public static void EnsureStands(float tableTopHeight = 0.98f)` | Создать оба стенда, если их ещё нет (идемпотентно). |
| метод | `RebuildStandaloneScene` | `public static void RebuildStandaloneScene(float tableTopHeight = 0.98f)` | Полное пересоздание сцены (для меню редактора): удалить старые столы/роботов и создать только два стенда. Копии роботов делаются ДО удаления шаблонов. |
| метод | `TemplateFor` | `public static RobotController TemplateFor(System.Type robotType) => FindTemplate(robotType);` | Шаблон робота (для редакторского инструмента). |

### `class ToolAlign`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\ToolAlign.cs` (строка 18)
- **Назначение:** Выравнивание концевой плоскости робота («пятака») по плоскости поверхности. Нормаль «пятака» — ось вращения последнего сустава: если она совпала с нормалью поверхности, концевая плоскость ПАРАЛЛЕЛЬНА столу/полу/стене (и наклонной поверхности). Решается численно и НЕ трогает кинематику, IK и плани…

_Публичных членов нет (или тип объявлен без них)._

### `class TrajectoryCandidate`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\SelectionTypes.cs` (строка 20)
- **Назначение:** Кандидат-траектория: геометрия для «колбаски», метрики и стоимость.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `color` | `public Color color; // СВОЙ оттенок варианта (оранжево-жёлтая гамма, ТЗ 8 траекторий)` |  |
| поле | `id` | `public int id;` |  |
| поле | `label` | `public string label = "";` |  |
| поле | `lengthM` | `public float lengthM; // длина пути TCP, м` |  |
| поле | `limitMarginDeg` | `public float limitMarginDeg; // запас до лимитов, град` |  |
| поле | `minClearance` | `public float minClearance; // минимальный зазор, м` |  |
| поле | `plan` | `public PlannedTrajectory plan; // q(t) + времена` |  |
| поле | `safe` | `public bool safe; // прошла SafetyGate` |  |
| поле | `score` | `public double score; // S — меньше лучше` |  |
| поле | `timeS` | `public float timeS; // время по параметризации, с` |  |
| поле | `tube` | `public Vector3[] tube; // полилиния TCP для визуала и выбора` |  |
| поле | `view` | `public TrajectoryTube view; // визуал (создаётся TrajectoryTube)` |  |
| поле | `why` | `public string why = ""; // причина отказа/предупреждения` |  |

### `class TrajectoryTube`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\TrajectoryTube.cs` (строка 12)
- **Назначение:** «Колбаска» вокруг траектории (MVP-1/2): тонкая линия + процедурный tube-меш радиусом ~0.05 (1/20 юнита) + штрих-пунктирный слой для подсветки. Выбор — геометрический (расстояние луч↔полилиния), коллайдеры и слои не нужны: это устойчивее и работает с любым источником луча (мышь, контроллер, геймпад).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `baseColor` | `public Color baseColor = new Color(1f, 0.45f, 0.03f, 0.9f); // ярко-оранжевый (HDRP)` |  |
| поле | `hoverColor` | `public Color hoverColor = new Color(1f, 0.68f, 0.15f, 1f); // подсветка наведения` |  |
| поле | `radius` | `public float radius = 0.05f; // радиус «колбаски» (захват лучом с ~1 юнита)` |  |
| поле | `selectedColor` | `public Color selectedColor = new Color(1f, 0.85f, 0.3f, 1f); // выбранная` |  |
| поле | `tubeAlpha` | `public float tubeAlpha = 0.07f; // «колбаска» почти невидима (только для захвата)` |  |
| метод | `Build` | `public void Build(Vector3[] path, Color color, bool dashed = true)` |  |
| метод | `Contains` | `public bool Contains(Ray ray) => Contains(ray, radius);` |  |
| метод | `Contains` | `public bool Contains(Ray ray, float pickRadius)` | Попадание луча в «колбаску» (расстояние до полилинии ≤ радиуса). |
| метод | `Dimmed` | `public bool Dimmed => dimmed;` |  |
| метод | `DistanceToRay` | `public float DistanceToRay(Ray ray)` |  |
| метод | `Points` | `public Vector3[] Points => points;` |  |
| метод | `SetDimmed` | `public void SetDimmed(bool on)` | Приглушение траектории. Применяется в НОВОМ алгоритме подтверждения (шаг 3): когда одна траектория выбрана, остальные остаются в сцене, но «гаснут» — на них всё ещё можно навести зелёный лазер и переключиться (шаг 4 ТЗ), при этом выбранная видна ярко. Полностью траектории исчезают только на шаге 5. |
| метод | `SetHighlight` | `public void SetHighlight(bool hover, bool isSelected)` |  |

### `class TubeMath`

- **Файл:** `Assets\_Project\01_Scripts\Trajectory\SelectionTypes.cs` (строка 97)
- **Назначение:** Геометрия выбора: расстояние «луч ↔ трубка траектории» (без коллайдеров).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `DistanceRayPolyline` | `public static float DistanceRayPolyline(Ray ray, Vector3[] poly, out float rayParam, out int segIndex)` | Минимальное расстояние от луча до полилинии; возвращает также параметр вдоль луча (для проверки перекрытия по глубине) и индекс ближайшего сегмента. |
| метод | `PolylineLength` | `public static float PolylineLength(Vector3[] poly)` | Длина полилинии, м. |
| метод | `SegmentSegmentDistance` | `public static float SegmentSegmentDistance(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)` |  |

## Пространство имён `KazistovVvUI`

### `class BoostedObjectMarker`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KazistovVvUIManager.cs` (строка 3491)
- **Назначение:** Маркер «объект уже получил усиление светоотражения» (как было в UI-менеджере).

_Публичных членов нет (или тип объявлен без них)._

### `class CenterWindow`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\CenterWindow.cs` (строка 12)
- **Назначение:** Центральное окно (главная сцена): подсказки режимов и предпросмотр размещения. Для робота поддерживается выбор направления в горизонтали: ←/→ или A/D (мышь/клавиатура), левый стик X (геймпад) — вращение; при приближении к углу, кратному 90°, направление «примагничивается».

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `RobotYaw` | `public float RobotYaw { get; private set; }` |  |
| метод | `ActiveKind` | `public SpawnKind ActiveKind => activeKind;` |  |
| метод | `Build` | `public void Build(RectTransform parent)` |  |
| метод | `DestroyPreview` | `public void DestroyPreview()` |  |
| метод | `HideRobotChooser` | `public void HideRobotChooser()` |  |
| метод | `RobotChooserOpen` | `public bool RobotChooserOpen => chooserRoot != null && chooserRoot.gameObject.activeSelf;` |  |
| метод | `RotateRobot` | `public void RotateRobot(float deltaDegrees)` | Вращение робота в горизонтали (вызывается UIManager'ом из ввода). |
| метод | `SetHint` | `public void SetHint(string text)` |  |
| метод | `SetPhantomTemplate` | `public void SetPhantomTemplate(RobotController template)` | Реальная модель-«фантом» для предпросмотра (вместо стилизованной). |
| метод | `SetRobotYaw` | `public void SetRobotYaw(float degrees)` | Задать направление напрямую (уже кратно 90°). |
| метод | `ShowRobotChooser` | `public void ShowRobotChooser(System.Action<int> onPick)` |  |
| метод | `StartPlacement` | `public void StartPlacement(SpawnKind kind)` |  |
| метод | `UpdatePreview` | `public void UpdatePreview(Vector3 position, bool valid)` |  |

### `class IdleCameraBrain`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Camera\IdleCameraBrain.cs` (строка 11)
- **Назначение:** Режим простоя камеры («как в Skyrim»): если пользователь ничего не делает (нет движения мыши, клавиш, стиков) — камера медленно облетает активного робота на небольшой высоте. Любое действие мгновенно возвращает управление.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `blendSpeed` | `public float blendSpeed = 1.2f;` |  |
| поле | `idleDelay` | `public float idleDelay = 600f;` |  |
| поле | `orbitHeight` | `public float orbitHeight = 2.2f;` |  |
| поле | `orbitRadius` | `public float orbitRadius = 6f;` |  |
| поле | `orbitSpeed` | `public float orbitSpeed = 12f;` |  |
| свойство | `OrbitTarget` | `public Transform OrbitTarget { get; set; }` | Целевой робот для облёта (ставится UIManager'ом). |
| метод | `PingActivity` | `public void PingActivity()` |  |

### `class KazistovVvUIManager`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KazistovVvUIManager.cs` (строка 35)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `autoRebuildTree` | `public bool autoRebuildTree = true;` |  |
| поле | `Collapsed` | `public bool Collapsed;` |  |
| поле | `enableLightBoost` | `public bool enableLightBoost = true;` |  |
| поле | `enableWorkLamp` | `public bool enableWorkLamp = true;` |  |
| поле | `FloatPos` | `public Vector2 FloatPos = new Vector2(80f, 140f);` |  |
| поле | `lampHeight` | `public float lampHeight = 1.25f;` |  |
| поле | `lampIntensity` | `public float lampIntensity = 900f;` |  |
| поле | `lampRange` | `public float lampRange = 7f;` |  |
| поле | `lampTemperature` | `public float lampTemperature = 2700f;` |  |
| поле | `lightBoostAlbedo` | `public float lightBoostAlbedo = 1.35f;` |  |
| поле | `lightBoostEmissive` | `public float lightBoostEmissive = 0.35f;` |  |
| поле | `lightBoostSmoothness` | `public float lightBoostSmoothness = 0.12f;` |  |
| поле | `logUiEvents` | `public bool logUiEvents = true;` |  |
| поле | `propertiesInterval` | `public float propertiesInterval = 0.2f;` |  |
| поле | `propertiesPanelWidth` | `public float propertiesPanelWidth = 296f;` |  |
| поле | `settingsPanelHeight` | `public float settingsPanelHeight = 224f;` |  |
| поле | `Side` | `public KvDockSide Side;` |  |
| поле | `statusInterval` | `public float statusInterval = 0.1f;` |  |
| поле | `Thickness` | `public float Thickness;` |  |
| поле | `treeInterval` | `public float treeInterval = 0.4f;` |  |
| поле | `treePanelWidth` | `public float treePanelWidth = 272f;` |  |
| поле | `uiReferenceHeight` | `public float uiReferenceHeight = 1080f;` |  |
| поле | `uiReferenceWidth` | `public float uiReferenceWidth = 1920f;` |  |
| поле | `UiVersion` | `public const string UiVersion = "1.0 · FreeCAD-style desktop";` | Версия оболочки (показывается на вкладке «О программе»). |
| поле | `uiVisible` | `public bool uiVisible = true;` |  |
| поле | `Visible` | `public bool Visible;` |  |
| свойство | `GamepadHudView` | `public KvGamepadHud GamepadHudView { get { return gamepadHud; } }` | Виртуальный геймпад (ЭТАП 9) — для диагностики. |
| свойство | `GamepadRouter` | `public KvGamepadRouter GamepadRouter { get { return gamepadRouter; } }` | Роутер геймпада (ЭТАП 9) — для диагностики. |
| свойство | `Highlight` | `public KvSelectionHighlight Highlight { get { return highlight; } }` | Подсветка выбранного объекта в сцене. |
| свойство | `HotkeyDock` | `public KvDockPanel HotkeyDock { get { return hotkeyDock; } }` | Dock-панель окна горячих клавиш (для диагностики/раскладки). |
| свойство | `Hotkeys` | `public KvHotkeyView Hotkeys { get { return hotkeys; } }` | Окно горячих клавиш (для диагностики). |
| свойство | `Instance` | `public static KazistovVvUIManager Instance { get; private set; }` | Активный (не-дубликат) менеджер UI. |
| свойство | `KeyboardNav` | `public KvKeyboardNav KeyboardNav { get { return keyboardNav; } }` | Навигация по интерфейсу с клавиатуры (ЭТАП 11) — для диагностики. |
| свойство | `Menu` | `public KvMenuBar Menu { get { return menuBar; } }` | Меню (для диагностики/расширения). |
| свойство | `Palette` | `public KvCommandPalette Palette { get { return palette; } }` | Палитра команд (Ctrl+P) — только чтение для диагностики. |
| свойство | `PointCount` | `public int PointCount { get { return pointHistory.Count; } }` | Число известных точек (ветка «Точки»). |
| свойство | `Properties` | `public KvPropertiesView Properties { get { return properties; } }` | Панель свойств. |
| свойство | `PropertiesDock` | `public KvDockPanel PropertiesDock { get { return propertiesDock; } }` | Dock-панель свойств. |
| свойство | `PropertyRowCount` | `public int PropertyRowCount { get { return properties != null ? properties.RowCount : 0; } }` | Сколько строк в свойствах. |
| свойство | `SelectedNode` | `public ProjectNode SelectedNode { get { return selectedNode; } }` | Текущий выбранный узел дерева. |
| свойство | `SettingsDock` | `public KvDockPanel SettingsDock { get { return settingsDock; } }` | Dock-панель настроек. |
| свойство | `SettingsTab` | `public int SettingsTab { get { return settings != null ? settings.ActiveTab : -1; } }` | Текущая вкладка панели настроек (диагностика). |
| свойство | `SettingsView` | `public KvSettingsView SettingsView { get { return settings; } }` | Панель настроек и справки. |
| свойство | `States` | `public KvUiStates States { get { return uiStates; } }` | Состояния интерфейса (ошибка/загрузка/пустое состояние) — для диагностики. |
| свойство | `StatusBar` | `public KvStatusBar StatusBar { get { return status; } }` | Статус-бар. |
| свойство | `StatusCursor` | `public string StatusCursor { get { return status != null ? status.CursorText : ""; } }` | Текст координат в статус-баре. |
| свойство | `StatusMessage` | `public string StatusMessage { get { return status != null ? status.MessageText : ""; } }` | Текст сообщения в статус-баре. |
| свойство | `StatusState` | `public string StatusState { get { return status != null ? status.StateText : ""; } }` | Текст состояния в статус-баре. |
| свойство | `StatusTheme` | `public string StatusTheme { get { return status != null ? status.ThemeText : ""; } }` | Индикатор темы в статус-баре. |
| свойство | `Toolbar` | `public KvToolbar Toolbar { get { return toolbar; } }` | Верхняя панель инструментов. |
| свойство | `ToolbarButtonCount` | `public int ToolbarButtonCount { get { return toolbar != null ? toolbar.ButtonCount : 0; } }` | Сколько кнопок в верхней панели (ТЗ: 15). |
| свойство | `Tree` | `public KvTreeView Tree { get { return tree; } }` | Дерево моделей. |
| свойство | `TreeDock` | `public KvDockPanel TreeDock { get { return treeDock; } }` | Dock-панель дерева. |
| свойство | `TreeModel` | `public IReadOnlyList<ProjectNode> TreeModel { get { return treeModel; } }` | Текущая модель дерева (для диагностики/расширений). |
| свойство | `TreeRowCount` | `public int TreeRowCount { get { return tree != null ? tree.RowCount : 0; } }` | Сколько строк в дереве. |
| свойство | `UiObjectCount` | `public int UiObjectCount { get { return canvasRect != null ? CountChildren(canvasRect) : 0; } }` | Сколько объектов в иерархии интерфейса (диагностика производительности). |
| метод | `ApplySettingsFromDiagnostics` | `public void ApplySettingsFromDiagnostics()` | Заново применить сохранённые настройки визуализаций к сцене (диагностика). |
| метод | `BuildProperties` | `public List<KvProp> BuildProperties(ProjectNode node)` | Собрать строки свойств для узла (ТОЛЬКО ЧТЕНИЕ). |
| метод | `CameraRig` | `public FreeFlyCameraController CameraRig` | Контроллер оператора (WASD, лазеры, фонарик, шарик прицела). |
| метод | `CloseCommandPalette` | `public void CloseCommandPalette()` | Закрыть палитру команд. |
| метод | `CommandButtonRect` | `public RectTransform CommandButtonRect(string commandId)` | Прямоугольник кнопки тулбара по id команды (ТОЛЬКО ЧТЕНИЕ). Нужен подсветке туториала (этап 2): рамка обводит САМУ кнопку, интерфейс при этом не перестраивается и не дублируется. null — кнопки с таким id в тулбаре нет или оболочка ещё не собрана. |
| метод | `CopyNodeName` | `public void CopyNodeName(ProjectNode node)` | ЭТАП 6: скопировать имя узла в буфер обмена. |
| метод | `CycleTheme` | `public void CycleTheme()` | Переключить тему (кнопка/меню/настройки). |
| метод | `DeleteNodes` | `public void DeleteNodes(List<ProjectNode> nodes)` | ЭТАП 6: удалить узлы (точки — из истории, waypoints — из маршрута). |
| метод | `DuplicateNodes` | `public void DuplicateNodes(List<ProjectNode> nodes)` | ЭТАП 6: дублировать узлы (точки — со смещением, waypoints — со смещением). |
| метод | `Flow` | `public TrajectoryFlowController Flow` | Поток этапов (владелец лазеров, фантомов, визуализаций). |
| метод | `FocusCameraOn` | `public void FocusCameraOn(ProjectNode node)` | ЭТАП 6: навести камеру на объект узла (фокус). |
| метод | `IsPanelVisible` | `public bool IsPanelVisible(string id)` | Фактические состояния панелей (для панели настроек и диагностики). |
| метод | `LayoutDock` | `public void LayoutDock(float statusHeight)` | Разложить dock-панели (единая точка правды по геометрии). |
| метод | `OnTreeContextMenu` | `public void OnTreeContextMenu(List<ProjectNode> nodes, Vector2 screenPosition)` | ПКМ по узлу дерева: меню строится ПОД ТИП УЗЛА и применяется ко ВСЕМУ набору выделенных узлов (Ctrl+клик — мультивыбор). Меню показывает только то, что реально поддержано: «Удалить»/«Дублировать» — для точек и waypoints, «Скрыть/Показать» — для узлов с объектом сцены, «Фокус камеры» — для объектов. |
| метод | `OpenCommandPalette` | `public void OpenCommandPalette()` | Открыть палитру команд (Ctrl+P, Start на геймпаде, кнопка тулбара). |
| метод | `RebuildShell` | `public void RebuildShell()` | Пересобрать оболочку (смена темы/масштаба/плотности) — без перезагрузки. |
| метод | `RebuildTree` | `public void RebuildTree(bool force)` | Пересборка дерева. При =false пересборка идёт только если «подпись» содержимого изменилась — это и есть защита от лишней работы в кадре. |
| метод | `Refresh` | `public void Refresh()` | Публичное обновление интерфейса (для диагностики/тестов/меню редактора). |
| метод | `RefreshPanels` | `public void RefreshPanels()` | Обновление, которое реально идёт КАЖДЫЙ тик (статус-бар, свойства, кнопки) — без пересборки дерева и реестра. Именно этот путь определяет влияние UI на FPS. |
| метод | `ReleaseFonts` | `public static void ReleaseFonts()` | Пересоздать шрифты всех текстов после смены языка (для CJK Unity обязан пересобрать атлас глифов, иначе останутся «квадраты» от прежнего шрифта). |
| метод | `RenameNode` | `public void RenameNode(ProjectNode node)` | ЭТАП 6: переименовать узел «на месте» (как двойным кликом). |
| метод | `ResetLayout` | `public void ResetLayout()` | СБРОСИТЬ РАСКЛАДКУ ОКОН (ЭТАП 3): сохранённые в PlayerPrefs позиции/размеры/края удаляются, группы тулбара разворачиваются, оболочка пересобирается — все панели возвращаются на места по умолчанию. |
| метод | `SelectNode` | `public void SelectNode(ProjectNode node)` | Выбрать узел дерева: подсветка в сцене + панель свойств. |
| метод | `SetPanel` | `public void SetPanel(string id, bool visible)` | Показать/скрыть dock-панель по id. |
| метод | `SetPlanStatus` | `public static void SetPlanStatus(string text, Color color)` | Сообщение от планировщика/метрик (одна строка в статус-баре). |
| метод | `SetTooltip` | `public static void SetTooltip(string text, Vector3 worldPos)` | Всплывающая подсказка у мировой точки (метрики траектории/фантома). |
| метод | `SetUiVisible` | `public static void SetUiVisible(bool visible)` | Показать/скрыть весь интерфейс (TAB). |
| метод | `ShowHotkeys` | `public void ShowHotkeys(bool show)` | Показать окно горячих клавиш (без переключения). |
| метод | `ShowSettings` | `public void ShowSettings(int tab)` | Открыть панель настроек на вкладке (0 функции … 4 справка). |
| метод | `StartPlacement` | `public void StartPlacement(SpawnKind kind)` |  |
| метод | `SwitchRobotFromGamepad` | `public void SwitchRobotFromGamepad()` | Переключить активного робота (D-Pad ← / → на геймпаде). |
| метод | `ToggleHotkeys` | `public void ToggleHotkeys()` | Показать/скрыть окно горячих клавиш (F12, меню «Справка», Select геймпада). |
| метод | `ToggleNodesVisibility` | `public void ToggleNodesVisibility(List<ProjectNode> nodes)` | ЭТАП 6: скрыть/показать объекты выбранных узлов. |
| метод | `TogglePanel` | `public void TogglePanel(string id)` | Переключить панель. |
| метод | `ToggleSettings` | `public void ToggleSettings()` | Показать/скрыть панель настроек (диагностика/меню). |

### `class KvBindEntry`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvBindings.cs` (строка 9)
- **Назначение:** Одна строка реестра биндов (клавиатура, мышь, геймпад).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Action` | `public string Action = "";` | Что делает. |
| поле | `CommandId` | `public string CommandId = "";` | Id команды, если бинд можно выполнить из палитры команд. |
| поле | `Contextual` | `public bool Contextual;` | Контекстный бинд: одна и та же клавиша в РАЗНЫХ состояниях (Esc — отмена/сброс). Такие строки не считаются конфликтом (иначе окно горячих клавиш «краснело» бы зря). |
| поле | `Device` | `public string Device = "Клавиатура";` | Устройство: «Клавиатура», «Геймпад» … |
| поле | `Group` | `public string Group = "";` | Раздел: «Интерфейс», «Робот», «Траектории», «Экспорт», «Геймпад» … |
| поле | `Keys` | `public string Keys = "";` | Клавиша/кнопка («Z», «Ctrl+P», «LT»). |
| поле | `Note` | `public string Note = "";` | Пояснение (необязательно). |

### `class KvBindingEntry`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvSettingsView.cs` (строка 24)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `action` | `public string action = "";` |  |
| поле | `group` | `public string group = "";` |  |
| поле | `keys` | `public string keys = "";` |  |
| поле | `note` | `public string note = "";` |  |

### `class KvBindings`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvBindings.cs` (строка 49)
- **Назначение:** ЕДИНЫЙ РЕЕСТР БИНДОВ (ЭТАП 7): окно «Горячие клавиши» (F12), палитра команд и проверка конфликтов читают ЭТОТ список. Значения соответствуют фактическому коду: клавиатура/мышь — как было (бинды не менялись), геймпад — ЭТАП 9 этой сессии.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `All` | `public static List<KvBindEntry> All()` | Все бинды (сначала клавиатура, затем геймпад, затем VR-заглушки). |
| метод | `Conflicts` | `public static HashSet<string> Conflicts()` | КОНФЛИКТЫ БИНДОВ (ЭТАП 7): одна и та же клавиша у двух РАЗНЫХ действий внутри одного устройства. Контекстные бинды (Esc в разных режимах) конфликтом не считаются. Возвращает множество «устройство\|клавиша», по которым есть конфликт. |
| метод | `Dump` | `public static string Dump()` | Текстовый дамп (диагностика). |
| метод | `Filter` | `public static List<KvBindEntry> Filter(string query)` | Бинды, подходящие под строку поиска (по действию, клавише, пояснению). |
| метод | `Groups` | `public static List<string> Groups()` | Разделы в порядке появления (для группировки в окне). |
| метод | `Invalidate` | `public static void Invalidate()` | Сбросить кэш (например, после переназначения бинда — задел). |
| метод | `IsConflict` | `public static bool IsConflict(KvBindEntry entry, HashSet<string> conflicts)` | Конфликтует ли конкретный бинд (по ключу «устройство\|клавиша»). |
| метод | `Split` | `public static List<string> Split(string keys)` | Клавиши строки («Q/E, W/S, A/D» → q/e, w/s, a/d). |

### `class KvCommand`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvCommands.cs` (строка 14)
- **Назначение:** Одна команда интерфейса: иконка, подсказка, горячая клавиша, действие и функции состояния. Тулбар, меню и «горячие» кнопки рисуются ИЗ РЕЕСТРА команд, поэтому новая кнопка добавляется регистрацией команды (плюс её id в раскладке тулбара) — без правок самих панелей.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `CheckedTint` | `public Func<Color?> CheckedTint;` | Оттенок иконки в состоянии «включено» (красный/зелёный лазер и т.п.). |
| поле | `Description` | `public string Description = "";` | Пояснение (вторая строка подсказки). |
| поле | `Execute` | `public Action Execute;` | Действие команды (null — заглушка). |
| поле | `Hotkey` | `public string Hotkey = "";` | Горячая клавиша/кнопка («Z», «ЛКМ», «TAB»). |
| поле | `Icon` | `public string Icon = "";` | Id иконки из KvIcons. |
| поле | `IconChecked` | `public string IconChecked = "";` | Идентификатор иконки в состоянии «включено» (тумблеры). |
| поле | `Id` | `public string Id;` | Уникальный id («point.select», «view.workspace»). |
| поле | `IsChecked` | `public Func<bool> IsChecked;` | Состояние тумблера (null — обычная кнопка). |
| поле | `IsEnabled` | `public Func<bool> IsEnabled;` | Доступность (null — всегда доступна). |
| поле | `MenuPath` | `public string MenuPath = "";` | Путь в строке меню («Вид/Зона достижимости»), пусто — только тулбар. |
| поле | `Stub` | `public bool Stub;` | Функционала нет — кнопка показывается недоступной, в подсказке «в разработке». |
| поле | `Title` | `public string Title;` | Заголовок для подсказки и меню. |
| свойство | `Checked` | `public bool Checked { get { return IsChecked != null && IsChecked(); } }` |  |
| свойство | `Enabled` | `public bool Enabled { get { return !Stub && (IsEnabled == null \|\| IsEnabled()); } }` |  |
| свойство | `LocalizedDescription` | `public string LocalizedDescription { get { return KvLoc.CmdDesc(Id, Description); } }` | Пояснение с учётом языка интерфейса (ключ «cmd..desc»). |
| свойство | `LocalizedTitle` | `public string LocalizedTitle { get { return KvLoc.Cmd(Id, Title); } }` | Заголовок с учётом языка интерфейса (ЭТАП 2): ключ словаря «cmd.», а если перевода нет — исходный русский текст этой команды. |
| метод | `TooltipBody` | `public string TooltipBody` |  |

### `class KvCommandPalette`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvCommandPalette.cs` (строка 22)
- **Назначение:** COMMAND PALETTE (ЭТАП 5): быстрый доступ ко ВСЕМ командам через поиск. Открывается Ctrl+P / Ctrl+Shift+P (и кнопкой Start геймпада, ЭТАП 9), закрывается Esc; Enter — выполнить, стрелки — навигация. Поиск идёт по: • подписи на ТЕКУЩЕМ языке и на ВСЕХ остальных (включая английский) — через AllLangu…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Highlight` | `public int Highlight { get { return highlight; } }` | Индекс подсвеченной строки (диагностика). |
| свойство | `IsOpen` | `public bool IsOpen { get { return root != null && root.gameObject.activeSelf; } }` | Палитра открыта (диагностика). |
| свойство | `Query` | `public string Query { get { return field != null ? field.text : ""; } }` | Строка поиска (диагностика). |
| свойство | `Recent` | `public IReadOnlyList<string> Recent { get { return recent; } }` | Недавние команды (id), самые свежие — первыми. |
| свойство | `ResultCount` | `public int ResultCount { get { return filtered.Count; } }` | Сколько команд в отфильтрованном списке (диагностика). |
| метод | `Close` | `public void Close()` | Закрыть палитру. |
| метод | `Create` | `public static KvCommandPalette Create(RectTransform canvas)` | Создать палитру на канвасе (один раз на оболочку). |
| метод | `Execute` | `public bool Execute()` | Выполнить подсвеченную команду. |
| метод | `ExecuteId` | `public bool ExecuteId(string id)` | Выполнить команду по id (диагностика/тесты). |
| метод | `HighlightLabel` | `public string HighlightLabel` | Подпись подсвеченной команды (диагностика). |
| метод | `Move` | `public void Move(int delta)` | Передвинуть подсветку по списку. |
| метод | `Open` | `public void Open()` | Открыть палитру с пустой строкой поиска. |
| метод | `SetQuery` | `public void SetQuery(string query)` | Ввести строку поиска программно (диагностика/тесты). |
| метод | `Toggle` | `public void Toggle()` | Открыть/закрыть. |

### `class KvCommands`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvCommands.cs` (строка 66)
- **Назначение:** Реестр команд интерфейса (расширяемость без правок панелей).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `All` | `public static IReadOnlyList<KvCommand> All { get { return ordered; } }` | Все команды в порядке регистрации. |
| метод | `Clear` | `public static void Clear()` | Очистить реестр (например, перед повторной регистрацией на смене сцены). |
| метод | `Dump` | `public static string Dump()` | Текстовый дамп реестра — для диагностики и отчёта. |
| метод | `Invoke` | `public static bool Invoke(string id)` | Выполнить команду (недоступные и заглушки не выполняются). |
| метод | `IsChecked` | `public static bool IsChecked(string id)` |  |
| метод | `IsEnabled` | `public static bool IsEnabled(string id)` |  |
| метод | `Register` | `public static KvCommand Register(KvCommand command)` | Зарегистрировать команду (повторная регистрация с тем же id заменяет её). |
| метод | `Register` | `public static KvCommand Register(string id, string title, string description, string hotkey,` | Зарегистрировать команду по короткому набору полей. |

### `class KvContextMenu`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvContextMenu.cs` (строка 53)
- **Назначение:** КОНТЕКСТНОЕ МЕНЮ (ЭТАП 6): всплывающий список действий по правому клику. Показывается в точке курсора, закрывается по клику вне, по Esc и после выбора. Готового решения в проекте нет, поэтому меню сделано локально — по тому же принципу, что выпадающие списки меню-бара и групп тулбара.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Current` | `public static KvContextMenu Current { get { return instance; } }` | Текущее (последнее открытое) меню. |
| свойство | `IsOpen` | `public bool IsOpen { get { return panel != null && panel.gameObject.activeSelf; } }` | Меню сейчас открыто. |
| свойство | `ItemCount` | `public int ItemCount { get { return rows.Count; } }` | Сколько строк в меню (диагностика). |
| метод | `Close` | `public void Close()` | Закрыть меню. |
| метод | `CloseCurrent` | `public static void CloseCurrent()` | Закрыть текущее меню (если открыто). |
| метод | `Show` | `public static KvContextMenu Show(RectTransform canvas, Vector2 screenPosition, string title,` | Показать меню в точке экрана. — канвас интерфейса, — позиция курсора (обычно `PointerEventData.position`). |

### `class KvContextMenuItem`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvContextMenu.cs` (строка 10)
- **Назначение:** Строка контекстного меню (ЭТАП 6).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Action` | `public Action Action;` | Действие пункта. |
| поле | `Checked` | `public bool Checked;` | Пункт отмечен галочкой (для «Скрыть/Показать»). |
| поле | `Enabled` | `public bool Enabled = true;` | Пункт доступен. |
| поле | `Hotkey` | `public string Hotkey = "";` | Горячая клавиша справа (необязательно). |
| поле | `Icon` | `public string Icon = "";` | Id иконки (может быть пустым). |
| поле | `Label` | `public string Label = "";` | Подпись пункта. |
| поле | `Separator` | `public bool Separator;` | Пункт-разделитель (остальные поля не используются). |
| метод | `Sep` | `public static KvContextMenuItem Sep()` | Разделитель. |

### `class KvCursors`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvCursors.cs` (строка 26)
- **Назначение:** Курсоры изменения размера окна (ЭТАП 4): «↔», «↕» и две диагонали. Текстуры рисуются КОДОМ (как иконки), файлов-ассетов нет; курсом управляет Set при наведении на границу и Reset при уходе. Если курсор захвачен (телеоперация, `Cursor.lockState == Locked`) — не трогаем: в FPS-режиме системный курс…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Reset` | `public static void Reset()` | Вернуть обычный курсор. |

### `class KvDockDrag`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvDockPanel.cs` (строка 779)
- **Назначение:** Перетаскивание заголовка dock-панели (ЭТАП 3): панель следует за курсором, при поднесении к краю показывается ЗОНА прикрепления, при отпускании — прилипание к краю или плавающее окно в центре.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Bind` | `public void Bind(KvDockPanel owner, RectTransform canvas)` |  |
| метод | `OnBeginDrag` | `public void OnBeginDrag(PointerEventData e)` |  |
| метод | `OnDrag` | `public void OnDrag(PointerEventData e)` |  |
| метод | `OnEndDrag` | `public void OnEndDrag(PointerEventData e)` |  |
| метод | `OnPointerClick` | `public void OnPointerClick(PointerEventData e)` |  |

### `class KvDockIndicator`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvDockPanel.cs` (строка 843)
- **Назначение:** ИНДИКАТОР ЗОНЫ ПРИКРЕПЛЕНИЯ (ЭТАП 3): при перетаскивании окна к краю подсвечивается область, куда оно прикрепится (левая/правая/нижняя полоса) либо центр (плавающее окно).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Ensure` | `public static KvDockIndicator Ensure(RectTransform canvas)` | Создать (один раз на канвас) слой индикатора. |
| метод | `Hide` | `public void Hide()` | Скрыть индикатор. |
| метод | `Show` | `public void Show(KvDockSide side)` | Показать зону, куда прикрепится окно. |

### `class KvDockPanel`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvDockPanel.cs` (строка 33)
- **Назначение:** Dockable-панель в стиле FreeCAD (ЭТАПЫ 3–4): • перетаскивание за ЗАГОЛОВОК: панель отклеивается, следует за курсором, при поднесении к краю подсвечивается ЗОНА прикрепления, при отпускании панель прилипает к краю (left / right / bottom), а в центре становится ПЛАВАЮЩИМ окном; • кнопка ОТКРЕПЛЕНИЯ…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `CollapsedHeight` | `public const float CollapsedHeight = HeaderHeight + 2f;` | Высота свёрнутой панели. |
| поле | `CornerGrip` | `public const float CornerGrip = 9f;` | Сторона углового маркера (px). |
| поле | `EdgeGrip` | `public const float EdgeGrip = 5f;` | Толщина зоны захвата границы (px). |
| поле | `HeaderHeight` | `public const float HeaderHeight = 19f;` | Высота заголовка. |
| поле | `maxThickness` | `public float maxThickness = 720f;` |  |
| поле | `MinFloatHeight` | `public const float MinFloatHeight = 100f;` |  |
| поле | `MinFloatWidth` | `public const float MinFloatWidth = 150f;` | Минимальный размер плавающего окна (ТЗ ЭТАПА 4: 150×100). |
| поле | `minThickness` | `public float minThickness = 120f;` | Минимальная/максимальная толщина пристыкованной панели. |
| поле | `RailThickness` | `public const float RailThickness = 20f;` | Ширина «полочки» (панель, свёрнутая кнопкой ✕). |
| свойство | `Body` | `public RectTransform Body { get { return body; } }` | Контейнер содержимого панели. |
| свойство | `Collapsed` | `public bool Collapsed { get; private set; }` | Свёрнута (остаётся только заголовок). |
| свойство | `FloatHeight` | `public float FloatHeight { get { return floatH; } }` |  |
| свойство | `Floating` | `public bool Floating { get { return Side == KvDockSide.Float; } }` | Плавающее окно (не пристыковано). |
| свойство | `FloatWidth` | `public float FloatWidth { get { return floatW; } }` |  |
| свойство | `HeaderRect` | `public RectTransform HeaderRect { get { return header; } }` | Заголовок (для подписи/иконки). |
| свойство | `InRail` | `public bool InRail { get; private set; }` | Свёрнута в боковую «полочку» (кнопка ✕). |
| свойство | `LayoutKey` | `public string LayoutKey { get; private set; }` | Ключ раскладки (для PlayerPrefs). |
| свойство | `Root` | `public RectTransform Root { get { return root; } }` | Корень панели. |
| свойство | `Shown` | `public bool Shown { get { return root != null && root.gameObject.activeSelf; } }` | Панель показана. |
| свойство | `Side` | `public KvDockSide Side { get; private set; }` | Текущая пристыковка. |
| свойство | `Thickness` | `public float Thickness { get; private set; }` | Толщина (ширина для левой/правой, высота для нижней), px канваса. |
| свойство | `Title` | `public string Title { get; private set; }` | Заголовок панели. |
| событие | `Closed` | `public event Action Closed;` | Пользователь закрыл панель (менеджер снимает галочку в меню «Вид»). |
| событие | `LayoutChanged` | `public event Action<string> LayoutChanged;` | Раскладка панели изменилась (для сохранения в PlayerPrefs). |
| событие | `LayoutDirty` | `public event Action LayoutDirty;` | Геометрия панели изменилась — пересобрать раскладку. |
| метод | `ApplyLayout` | `public void ApplyLayout(float topInset, float bottomInset, float leftInset, float rightInset)` | Разложить панель. Вызывается менеджером UI: он передаёт актуальные отступы (высота тулбара сверху, высота статус-бара снизу, занятые края). |
| метод | `ApplySavedLayout` | `public void ApplySavedLayout(KvPanelLayout l)` | Применить сохранённую раскладку (ЭТАП 3: «окна на своих местах»). |
| метод | `BeginDockedResize` | `public void BeginDockedResize(Vector2 canvasPoint)` | Начать изменение толщины ПРИСТЫКОВАННОЙ панели (внутренняя кромка). |
| метод | `Build` | `public void Build(RectTransform parent, Canvas owner, string title, string iconId,` | Собрать панель внутри канваса. |
| метод | `CaptureLayout` | `public KvPanelLayout CaptureLayout()` | Текущая раскладка панели (для сохранения в PlayerPrefs). |
| метод | `CycleDock` | `public void CycleDock()` | Сменить пристыковку (Left → Right → Bottom → Float → Left). |
| метод | `DockZoneAt` | `public KvDockSide DockZoneAt(Vector2 canvasPoint)` | Какая зона прикрепления под точкой канваса (для индикатора и отпускания). |
| метод | `DragTo` | `public void DragTo(Vector2 canvasPoint)` | Перетаскивание заголовка: панель «отклеивается» и следует за курсором. |
| метод | `DropAt` | `public void DropAt(Vector2 canvasPoint)` | Отпускание заголовка: прилипание к ближнему краю или свободное окно. Зона прилипания — та же, что подсвечивал индикатор (DockZoneAt). |
| метод | `EndDockedResize` | `public void EndDockedResize()` | Закончить изменение толщины + сохранить раскладку (ЭТАП 3). |
| метод | `FromRail` | `public void FromRail()` | Развернуть из «полочки». |
| метод | `Hide` | `public void Hide()` | Скрыть панель полностью (меню «Вид»/настройки). |
| метод | `NotifyLayoutChanged` | `public void NotifyLayoutChanged()` | Сообщить владельцу, что раскладку пора сохранить (после перетаскивания). |
| метод | `RailClick` | `public void RailClick()` | Клик по полочке возвращает окно. |
| метод | `Repaint` | `public void Repaint()` | Перекрасить панель под текущую тему (после смены темы). |
| метод | `ResizeDockedTo` | `public void ResizeDockedTo(Vector2 canvasPoint)` | Изменить толщину ПРИСТЫКОВАННОЙ панели: смещение считается ОТ НАЧАЛА перетаскивания (внутренняя кромка идёт за курсором ровно, без накопления погрешности на пределах). |
| метод | `ResizeFloatTo` | `public void ResizeFloatTo(Vector2 canvasPoint, KvResizeEdge edge)` | Изменить размер ПЛАВАЮЩЕГО окна перетаскиванием грани/угла (ЭТАП 4). Позиция/размер считаются от точки курсора — так окно не «уезжает» при упоре в предел. |
| метод | `SetCollapsed` | `public void SetCollapsed(bool value)` |  |
| метод | `SetFloatRect` | `public void SetFloatRect(Vector2 position, Vector2 size)` | Позиция/размер в режиме «плавающей» панели (px канваса, от левого-нижнего угла). |
| метод | `SetSide` | `public void SetSide(KvDockSide value)` |  |
| метод | `SetThickness` | `public void SetThickness(float value)` | Толщина задаётся ползунком или менеджером. |
| метод | `SetVisible` | `public void SetVisible(bool value)` | Показать/скрыть панель. |
| метод | `ToggleCollapsed` | `public void ToggleCollapsed()` | Свернуть/развернуть (остаётся один заголовок). |
| метод | `ToggleFloat` | `public void ToggleFloat()` | Открепить/прикрепить: плавающее окно ⟷ пристыкованное (ТЗ ЭТАПА 3). |
| метод | `ToRail` | `public void ToRail()` | Свернуть окно в боковую «полочку» (кнопка ✕, ТЗ ЭТАПА 3). |

### `enum KvDockSide`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvDockPanel.cs` (строка 9)
- **Назначение:** Куда пристыкована панель.

_Публичных членов нет (или тип объявлен без них)._

### `class KvGamepadBridge`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvGamepadBridge.cs` (строка 12)
- **Назначение:** МОСТ между геймпадом и существующим вводом (ЭТАП 9). Почему мост, а не правка State Machine: проект уже читает подтверждение/отмену/ вход в режим точки в `FreeFlyCameraController` (ЛКМ, Esc, Enter). Чтобы кнопки геймпада означали ТЕ ЖЕ действия, роутер геймпада (KvGamepadRouter) выставляет эти фл…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Active` | `public static bool Active;` | Геймпад подключён и роутер работает. |
| поле | `Cancel` | `public static bool Cancel;` | Отмена (B) — аналог Esc. |
| поле | `Confirm` | `public static bool Confirm;` | Подтверждение (A / RT) — аналог ЛКМ. |
| поле | `Enter` | `public static bool Enter;` | Вход/подтверждение режима перемещения точки (LT) — аналог Enter. |
| свойство | `SticksAlwaysActive` | `public static bool SticksAlwaysActive { get { return Active; } }` | Стики работают БЕЗ ручного тумблера R3 (ТЗ ЭТАПА 9: левая ось — ходьба, правая — обзор, «как в FPS»). Тумблер R3 оставлен как совместимость. |
| свойство | `SuppressLegacyGamepad` | `public static bool SuppressLegacyGamepad { get { return Active; } }` | Старые «геймпадные» пути в контроллере камеры (LB — выбор робота по прицелу, левый триггер — подтверждение) отключаются, пока работает роутер: иначе одно нажатие срабатывало бы дважды (LB = аварийный стоп по ТЗ и выбор робота по старому коду). |
| метод | `ClearFrame` | `public static void ClearFrame()` | Сбросить флаги (вызывает роутер в LateUpdate — флаги живут ровно один кадр). |
| метод | `Reset` | `public static void Reset()` | Полный сброс (геймпад отключён). |

### `class KvGamepadHud`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvGamepadHud.cs` (строка 17)
- **Назначение:** ВИРТУАЛЬНЫЙ ГЕЙМПАД (ЭТАП 9): мини-индикатор на экране, который показывает ТЕКУЩИЕ НАЖАТИЯ — какой стик отклонён, какие кнопки нажаты, насколько выжаты триггеры. Нужен потому, что оператор взял геймпад впервые: видно, что система действительно видит нажатие, и не нужно угадывать раскладку. Панель…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `PillCount` | `public int PillCount { get { return pills.Count; } }` | Сколько «кнопок-таблеток» на индикаторе (диагностика). |
| свойство | `StatusText` | `public string StatusText { get { return title != null ? title.text : ""; } }` | Подпись состояния геймпада (диагностика). |
| свойство | `Visible` | `public bool Visible { get { return root != null && root.gameObject.activeSelf; } }` | Панель видна (диагностика). |
| метод | `Create` | `public static KvGamepadHud Create(RectTransform canvas)` | Собрать индикатор на канвасе. |
| метод | `Repaint` | `public void Repaint()` | Перекрасить под текущую тему. |

### `class KvGamepadRouter`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvGamepadRouter.cs` (строка 37)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `enableGamepad` | `public bool enableGamepad = true;` |  |
| поле | `logDeviceChanges` | `public bool logDeviceChanges = true;` |  |
| поле | `Pressed` | `public readonly HashSet<string> Pressed = new HashSet<string>();` | Текущее состояние кнопок (для виртуального геймпада на экране). |
| поле | `stickDeadzone` | `public float stickDeadzone = 0.18f;` |  |
| свойство | `ActionCount` | `public int ActionCount { get; private set; }` | Сколько действий геймпада выполнено за сессию (диагностика). |
| свойство | `ConnectCount` | `public int ConnectCount { get; private set; }` | Сколько раз геймпад подключался (диагностика автоопределения). |
| свойство | `DeviceName` | `public string DeviceName { get; private set; }` | Имя устройства («Xbox Controller», «DualSense»…). |
| свойство | `Instance` | `public static KvGamepadRouter Instance { get; private set; }` | Активный роутер (для виртуального геймпада и диагностики). |
| свойство | `LastAction` | `public string LastAction { get; private set; }` | Последнее действие (диагностика). |
| свойство | `LeftStick` | `public Vector2 LeftStick { get; private set; }` | Положение левого стика (для индикатора). |
| свойство | `LeftTrigger` | `public float LeftTrigger { get; private set; }` | Значения триггеров 0..1 (для индикатора). |
| свойство | `Present` | `public bool Present { get; private set; }` | Геймпад подключён (управление активно). |
| свойство | `RightStick` | `public Vector2 RightStick { get; private set; }` | Положение правого стика (для индикатора). |
| свойство | `RightTrigger` | `public float RightTrigger { get; private set; }` | Правый триггер 0..1. |
| метод | `Detect` | `public bool Detect()` | Пересчитать признак «геймпад есть» (автоопределение, ТЗ ЭТАПА 9). |

### `class KvHotkeyView`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvHotkeyView.cs` (строка 17)
- **Назначение:** ОКНО СПИСКА ГОРЯЧИХ КЛАВИШ (ЭТАП 7). Открывается по F12 и через меню «Справка → Горячие клавиши»; живёт в ПЛАВАЮЩЕЙ dock-панели, поэтому его можно тащить за заголовок, пристыковать к краю, растянуть за границы/углы (этапы 3–4) — раскладка сохраняется в PlayerPrefs. Содержимое: поле поиска (фильтр…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `ConflictCount` | `public int ConflictCount { get; private set; }` | Сколько конфликтов подсвечено в текущем списке (диагностика). |
| свойство | `Query` | `public string Query { get { return field != null ? field.text : ""; } }` | Текущая строка поиска (диагностика). |
| свойство | `RowCount` | `public int RowCount { get { return rows.Count; } }` | Сколько строк построено (диагностика). |
| метод | `Build` | `public void Build(RectTransform parent)` |  |
| метод | `Rebuild` | `public void Rebuild()` | Перестроить список биндов с учётом фильтра и конфликтов. |
| метод | `Repaint` | `public void Repaint()` | Перекрасить окно под текущую тему. |
| метод | `SetQuery` | `public void SetQuery(string query)` | Задать строку поиска программно (диагностика/тесты). |

### `class KvIconButton`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvWidgets.cs` (строка 307)
- **Назначение:** Кнопка-иконка: состояние (обычная/включена/недоступна) и подсказка.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Background` | `public Image Background { get; private set; }` |  |
| свойство | `Button` | `public Button Button { get; private set; }` |  |
| свойство | `Icon` | `public Image Icon { get; private set; }` |  |
| свойство | `IconId` | `public string IconId { get; private set; }` |  |
| свойство | `IsEnabled` | `public bool IsEnabled { get { return enabledState; } }` |  |
| свойство | `Tooltip` | `public KvTooltipTarget Tooltip { get; private set; }` |  |
| метод | `SetChecked` | `public void SetChecked(bool value, Color? iconTint = null)` | Подсветить как «включено» (тумблер) и задать оттенок иконки. |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` | Доступна ли кнопка (недоступные — «заглушки»). |
| метод | `SetIcon` | `public void SetIcon(string iconId)` | Сменить иконку (например, play ⟷ pause, тема). |
| метод | `SetStub` | `public void SetStub(bool stub)` | Пометка «в разработке» для подсказки. |

### `class KvIconCanvas`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvIcons.cs` (строка 695)
- **Назначение:** Мини-растеризатор иконок: белые штрихи на прозрачном фоне. Координаты — в пикселях, начало в левом НИЖНЕМ углу (иконки рисуются «вверх»).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Size` | `public int Size { get { return w; } }` |  |
| метод | `Arc` | `public void Arc(float cx, float cy, float r, float a0, float a1, float t)` | Дуга от угла a0 до a1 (градусы, против часовой). |
| метод | `Circle` | `public void Circle(float cx, float cy, float r, float t, bool filled)` | Окружность (контур или заливка). |
| метод | `Dot` | `public void Dot(float cx, float cy, float r)` | Залитая точка (узел). |
| метод | `Line` | `public void Line(float x0, float y0, float x1, float y1, float t = KvIcons.Stroke)` | Отрезок с полутолщиной t/2 и сглаживанием. |
| метод | `Poly` | `public void Poly(bool filled, params float[] xy)` | Многоугольник: контур или заливка (алгоритм «точка внутри»). |
| метод | `Rect` | `public void Rect(float x, float y, float width, float height, float t, bool filled)` | Прямоугольник (контур или заливка). |
| метод | `ToSprite` | `public Sprite ToSprite()` |  |

### `class KvIcons`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvIcons.cs` (строка 18)
- **Назначение:** Библиотека МОНОХРОМНЫХ иконок в стиле FreeCAD: иконки рисуются кодом (белые штрихи на прозрачном фоне) и тонируются цветом `Image.color`. Ни одного файла-ассета не нужно, поэтому нет проблем с импортом/meta. Спрайты кэшируются по (id, размер) — построение идёт один раз на иконку. ЭТАП 10 (единый …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `AllIds` | `public static readonly string[] AllIds =` | Все id иконок библиотеки (для диагностики единого набора). |
| поле | `GridSize` | `public const int GridSize = 24;` | ЭТАП 10: базовый размер сетки иконки (спрайт всегда рисуется в ней). |
| поле | `RowSize` | `public const int RowSize = 14;` | Размер иконки строки дерева. |
| поле | `Stroke` | `public const float Stroke = 1.5f;` | ЭТАП 10: единая толщина линии иконки (px сетки 24×24). |
| поле | `StrokeThin` | `public const float StrokeThin = 1.2f;` | ЭТАП 10: толщина мелких деталей (штриховка, второстепенные линии). |
| поле | `ToolbarSize` | `public const int ToolbarSize = 24;` | Штатный размер иконки тулбара (пикселей, в координатах канваса). |
| метод | `Has` | `public static bool Has(string id)` | Есть ли такая иконка в библиотеке. |

### `class KvKeyboardNav`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvKeyboardNav.cs` (строка 23)
- **Назначение:** НАВИГАЦИЯ ПО ИНТЕРФЕЙСУ С КЛАВИАТУРЫ (ЭТАП 11): Tab / Shift+Tab — вперёд-назад, стрелки — по соседним кнопкам (геометрически, как в панелях инструментов), Enter или пробел — нажать, Esc — снять фокус. Фокус обводится рамкой. Работает ТОЛЬКО в режиме интерфейса (курсор свободен, панели видны) и то…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `FocusableCount` | `public int FocusableCount { get { return focusables.Count; } }` | Сколько элементов доступно навигации (диагностика). |
| свойство | `FocusIndex` | `public int FocusIndex { get { return index; } }` | Индекс кнопки в фокусе (−1 — фокуса нет). |
| свойство | `HasFocus` | `public bool HasFocus { get { return index >= 0 && index < focusables.Count; } }` | Фокус есть. |
| метод | `Activate` | `public bool Activate()` | Нажать кнопку в фокусе. |
| метод | `ClearFocus` | `public void ClearFocus()` | Снять фокус. |
| метод | `Create` | `public static KvKeyboardNav Create(RectTransform canvas, KazistovVvUIManager manager,` | Создать навигацию (один раз на оболочку). |
| метод | `FocusCommand` | `public bool FocusCommand(string commandId)` | Поставить фокус на кнопку по id команды (диагностика/тесты). |
| метод | `FocusLabel` | `public string FocusLabel` | Подпись кнопки в фокусе (диагностика). |
| метод | `Repaint` | `public void Repaint()` | Перекрасить рамку под текущую тему. |
| метод | `SetEnabled` | `public void SetEnabled(bool value)` | Включить/выключить навигацию (настройка «Навигация с клавиатуры»). |
| метод | `TabHandledByNavigation` | `public static bool TabHandledByNavigation` | Навигация забирает Tab себе. Пока true, контроллер камеры НЕ переключает режим интерфейса по Tab (иначе одно нажатие делало бы два дела). |

### `class KvLangFile`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvLocalization.cs` (строка 603)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `cjk` | `public bool cjk;` |  |
| поле | `code` | `public string code = "";` |  |
| поле | `english` | `public string english = "";` |  |
| поле | `name` | `public string name = "";` |  |
| поле | `strings` | `public KvLangString[] strings = new KvLangString[0];` |  |

### `class KvLangInfo`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvLocalization.cs` (строка 10)
- **Назначение:** Описание языка в списке доступных (для панели настроек).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `code` | `public string code = ""; // «ru», «en», «zh», «it» …` |  |
| поле | `englishName` | `public string englishName = ""; // английское название (для fallback-подписи)` |  |
| поле | `fromFile` | `public bool fromFile; // загружен из StreamingAssets (а не встроенный)` |  |
| поле | `name` | `public string name = ""; // название на самом языке («Русский», «中文»)` |  |
| поле | `needsCjk` | `public bool needsCjk; // требует шрифт с CJK-глифами` |  |
| поле | `strings` | `public int strings; // сколько строк загружено (диагностика)` |  |
| метод | `Label` | `public string Label` |  |

### `class KvLangString`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvLocalization.cs` (строка 614)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `key` | `public string key = "";` |  |
| поле | `text` | `public string text = "";` |  |

### `class KvLayoutStore`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvLayoutStore.cs` (строка 54)
- **Назначение:** ХРАНИЛИЩЕ РАСКЛАДКИ ОКОН (ЭТАП 3): сохраняет край/толщину/размер/позицию каждой панели в PlayerPrefs, чтобы при следующем запуске окна стояли на своих местах. Кнопка «Сбросить раскладку» возвращает значения по умолчанию (Clear + пересборка оболочки).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Prefix` | `public const string Prefix = "KazistovVv.Layout.";` | Префикс ключей PlayerPrefs. |
| метод | `Clear` | `public static void Clear(string[] keys)` | Сбросить сохранённую раскладку (кнопка «Сбросить раскладку»). |
| метод | `Flush` | `public static void Flush()` | Записать изменения на диск немедленно (после перетаскивания окна). |
| метод | `HasAny` | `public static bool HasAny` | Раскладка сохранена хотя бы для одной панели. |
| метод | `Load` | `public static KvPanelLayout Load(string key)` | Прочитать раскладку панели (null — сохранённой раскладки нет). |
| метод | `Save` | `public static void Save(string key, KvPanelLayout layout)` | Сохранить раскладку панели. |

### `class KvLoc`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvLocalization.cs` (строка 50)
- **Назначение:** СИСТЕМА ЛОКАЛИЗАЦИИ (ЭТАП 2 ТЗ): 7 языков (RU / EN / ZH / ES / DE / FR / JA) плюс любые дополнительные, добавленные файлом. ПОЧЕМУ JSON, А НЕ ScriptableObject (обоснование по ТЗ): * текст правится без Unity и без перекомпиляции (у ScriptableObject нужен редактор и .meta-ассет на каждый язык, а на…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `FolderName` | `public const string FolderName = "kazistovvv_i18n";` | Каталог словарей в StreamingAssets. |
| поле | `PrefsKey` | `public const string PrefsKey = "KazistovVv.Language";` | Ключ PlayerPrefs с выбранным языком («system» — автоматически). |
| поле | `SystemCode` | `public const string SystemCode = "system";` | Значение «язык как в системе». |
| свойство | `CurrentCode` | `public static string CurrentCode { get { Ensure(); return current; } }` | Фактически применённый язык (никогда не «system»). |
| свойство | `Languages` | `public static IReadOnlyList<KvLangInfo> Languages { get { Ensure(); return langs; } }` | Доступные языки (встроенные + найденные в StreamingAssets). |
| свойство | `PreferenceCode` | `public static string PreferenceCode { get { Ensure(); return preference; } }` | Выбор оператора: код языка или «system». |
| событие | `Changed` | `public static event Action Changed;` | Смена языка (интерфейс пересобирается по этому событию). |
| метод | `AddRuntimeStrings` | `public static void AddRuntimeStrings(string code, params string[] keyValuePairs)` | Добавить строки в каталог языка ВО ВРЕМЯ РАБОТЫ (модули новых функций: стартовое меню, туториал, постобработка траекторий). Формат — пары «ключ, текст»; файлы-словари при этом не трогаются, а ключ ведёт себя как обычный: цепочка «текущий язык → английский → русский исходный текст» сохраняется пол… |
| метод | `AllLanguages` | `public static string AllLanguages(string key)` | ЭТАП 5: ВСЕ переводы ключа на всех языках одной строкой. Нужен палитре команд: ТЗ требует, чтобы команда находилась и по текущему языку, И по английскому, поэтому поиск идёт по этому тексту, а не только по подписи на экране. |
| метод | `Cmd` | `public static string Cmd(string id, string fallbackTitle)` | Перевод заголовка команды интерфейса (ключ «cmd.»). |
| метод | `CmdDesc` | `public static string CmdDesc(string id, string fallbackDescription)` | Перевод пояснения команды интерфейса (ключ «cmd..desc»). |
| метод | `CountOf` | `public static int CountOf(string code)` | Сколько строк в каталоге языка (диагностика). |
| метод | `CurrentName` | `public static string CurrentName` | Название текущего языка на нём самом. |
| метод | `Cycle` | `public static string Cycle()` | Следующий язык в списке (кнопка «Язык» в тулбаре/меню). |
| метод | `FolderPath` | `public static string FolderPath` | Каталог словарей на диске (может не существовать — работают встроенные). |
| метод | `FontName` | `public static string FontName` | Имя фактического шрифта (диагностика). |
| метод | `Has` | `public static bool Has(string key)` | Есть ли ключ в текущем каталоге (диагностика покрытия). |
| метод | `KeysOf` | `public static List<string> KeysOf(string code)` | Все ключи языка (диагностика и проверка ПОЛНОТЫ покрытия: любой ключ русского каталога обязан присутствовать в английском — это и есть гарантия «fallback на английский» из ТЗ). |
| метод | `Menu` | `public static string Menu(string ruName)` | Перевод пункта СТРОКИ МЕНЮ по его русскому имени: «Файл» → menu.file → «File». Пути команд (`MenuPath`) остаются русскими (они — часть кода), а подписи переводятся здесь, поэтому меню и раскладка не расходятся. |
| метод | `MenuGroup` | `public static string MenuGroup(string ruName)` | Перевод подписи-группы внутри выпадающего меню (ключ «menu.group.»). |
| метод | `NeedsCjk` | `public static bool NeedsCjk` | Нужен ли шрифт с CJK-глифами (китайский/японский/корейский). |
| метод | `ReleaseFont` | `public static void ReleaseFont()` | Сбросить кэш шрифта (после смены языка). |
| метод | `Reload` | `public static void Reload()` | Перечитать словари с диска (правка JSON без перезапуска). |
| метод | `SetLanguage` | `public static void SetLanguage(string code, bool save = true)` | Установить язык (код или «system»). Мгновенно применяется: перекрашивается тема (шрифт), пересобирается оболочка интерфейса и обновляются тексты панелей. |
| метод | `Status` | `public static string Status` | Диагностика загрузки (для отчётов и панели настроек). |
| метод | `T` | `public static string T(string key, string fallback)` | Перевод по ключу. `fallback` — русский исходный текст из кода: он используется, если ключа нет ни в текущем языке, ни в английском. |
| метод | `TotalStrings` | `public static int TotalStrings` | Сколько строк во всех каталогах (диагностика). |
| метод | `UiFont` | `public static Font UiFont(int size)` | Шрифт интерфейса с учётом языка: для китайского/японского берётся системный шрифт с CJK-глифами (встроенный LegacyRuntime.ttf их не содержит — были бы «квадратики»). |

### `class KvMenuBar`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvMenuBar.cs` (строка 13)
- **Назначение:** Строка меню в стиле FreeCAD (сверху, тонкая): пункты строятся ИЗ РЕЕСТРА команд по их MenuPath («Вид/Зона достижимости»). Поэтому новое меню/пункт добавляется регистрацией команды — код этой панели трогать не нужно.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Height` | `public const float Height = 20f;` | Высота строки меню. |
| свойство | `DropdownRowCount` | `public int DropdownRowCount { get { return dropdownRows.Count; } }` | Сколько строк в открытом выпадающем списке (диагностика). |
| свойство | `OpenMenu` | `public string OpenMenu { get { return openMenu; } }` | Открытое меню (пусто — закрыто). |
| метод | `Build` | `public float Build(RectTransform canvas, Canvas owner)` | Собрать строку меню. Возвращает её высоту. |
| метод | `CloseMenu` | `public void CloseMenu()` | Закрыть открытое меню. |
| метод | `Refresh` | `public void Refresh()` | Перерисовать галки и доступность пунктов меню. |
| метод | `Repaint` | `public void Repaint()` | Перекрасить под текущую тему. |
| метод | `SetInfo` | `public void SetInfo(string text)` | Текст справа в строке меню (например «KazistovVv · робот: 6-осевой»). |
| метод | `ToggleMenu` | `public void ToggleMenu(string menu)` | Открыть/закрыть меню по имени. |

### `class KvMenuCheck`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvMenuBar.cs` (строка 380)
- **Назначение:** Галка состояния пункта меню.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Bind` | `public void Bind(GameObject checkGo)` |  |

### `class KvPanelLayout`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvLayoutStore.cs` (строка 9)
- **Назначение:** Раскладка ОДНОЙ панели (ЭТАП 3): край, толщина/размер, свёрнутость, видимость, «полочка» (свёрнута в боковую панель) и позиция плавающего окна.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Collapsed` | `public bool Collapsed;` |  |
| поле | `Height` | `public float Height = 300f;` |  |
| поле | `Rail` | `public bool Rail;` | Панель свёрнута в боковую «полочку» (кнопка ✕ по ТЗ ЭТАПА 3). |
| поле | `Side` | `public int Side;` |  |
| поле | `Thickness` | `public float Thickness = 272f;` |  |
| поле | `Visible` | `public bool Visible = true;` |  |
| поле | `Width` | `public float Width = 320f;` |  |
| поле | `X` | `public float X = 80f;` |  |
| поле | `Y` | `public float Y = 140f;` |  |
| метод | `Clone` | `public KvPanelLayout Clone()` |  |
| метод | `Describe` | `public string Describe()` |  |

### `struct KvProp`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvPropertiesView.cs` (строка 9)
- **Назначение:** Строка панели свойств: либо заголовок секции, либо «подпись = значение».

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Header` | `public bool Header;` |  |
| поле | `Label` | `public string Label;` |  |
| поле | `Value` | `public string Value;` |  |
| поле | `ValueColor` | `public Color ValueColor;` |  |
| метод | `Row` | `public static KvProp Row(string label, string value, Color color)` |  |
| метод | `Row` | `public static KvProp Row(string label, string value)` | Строка «подпись = значение» цветом темы по умолчанию (шорткат для новых модулей). |
| метод | `Section` | `public static KvProp Section(string title)` |  |

### `class KvPropertiesView`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvPropertiesView.cs` (строка 50)
- **Назначение:** Правая панель — СВОЙСТВА выбранного объекта (в стиле FreeCAD: «View → Properties»). Поля ТОЛЬКО ДЛЯ ЧТЕНИЯ (ТЗ): значения обновляются из сцены/потока, но не правятся. Строки переиспользуются (пул): при обновлении 5 раз в секунду новые GameObject-ы не создаются, поэтому панель не влияет на FPS.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Node` | `public ProjectNode Node { get; private set; }` | Текущий узел панели (для диагностики). |
| свойство | `Root` | `public RectTransform Root { get { return root; } }` | Корень панели (геометрию задаёт dock-панель). |
| свойство | `RowCount` | `public int RowCount { get { return rowPool.Count; } }` | Сколько строк сейчас показано (диагностика). |
| метод | `Build` | `public void Build(RectTransform canvas, Canvas owner)` |  |
| метод | `Repaint` | `public void Repaint()` | Перекрасить панель под текущую тему. |
| метод | `SetNode` | `public void SetNode(ProjectNode node)` | Сменить объект (обновляет шапку и состав строк). |
| метод | `SetProperties` | `public void SetProperties(List<KvProp> props)` | Показать набор строк (переиспользуя уже созданные). |
| метод | `SkeletonVisible` | `public bool SkeletonVisible` | ЭТАП 8: показан «скелетон» (объект не выбран) — диагностика. |

### `enum KvResizeEdge`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvCursors.cs` (строка 6)
- **Назначение:** Направление, в котором тянется граница окна (ЭТАП 4).

_Публичных членов нет (или тип объявлен без них)._

### `class KvResizeHandle`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvDockPanel.cs` (строка 1022)
- **Назначение:** ПОЛЗУНОК ГРАНИЦЫ ОКНА (ЭТАП 4): невидимая полоса по краю/углу панели. При наведении меняет системный курсор на «↔», «↕» или диагональ, при перетаскивании меняет размер окна (или толщину пристыкованной панели).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Edge` | `public KvResizeEdge Edge { get; private set; }` | Какой это маркер. |
| свойство | `Rect` | `public RectTransform Rect { get { return (RectTransform)transform; } }` | Прямоугольник маркера. |
| метод | `Bind` | `public void Bind(KvDockPanel owner, RectTransform canvas, KvResizeEdge edge)` |  |
| метод | `IsInnerFor` | `public bool IsInnerFor(KvDockSide side)` | Внутренняя ли это кромка для данного края пристыковки. |
| метод | `OnDrag` | `public void OnDrag(PointerEventData e)` |  |
| метод | `OnEndDrag` | `public void OnEndDrag(PointerEventData e)` |  |
| метод | `OnPointerDown` | `public void OnPointerDown(PointerEventData e)` |  |
| метод | `OnPointerEnter` | `public void OnPointerEnter(PointerEventData e)` |  |
| метод | `OnPointerExit` | `public void OnPointerExit(PointerEventData e)` |  |

### `class KvSegmented`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvWidgets.cs` (строка 400)
- **Назначение:** Сегментный переключатель (используется для темы и масштаба).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Index` | `public int Index { get; private set; }` |  |

### `class KvSelectionHighlight`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvSelectionHighlight.cs` (строка 12)
- **Назначение:** Подсветка выбранного в дереве объекта в СЦЕНЕ: вокруг объекта рисуется «инженерная» рамка-габарит (12 тонких цилиндров) цветом акцента темы. Служебный объект рантайма — `HideFlags.HideInHierarchy`, без коллайдеров, поэтому в `CollisionWorld` он не попадает (правило проекта). Обновление габаритов …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `barRadius` | `public float barRadius = 0.006f;` | Толщина прутьев рамки, м. |
| поле | `refreshInterval` | `public float refreshInterval = 0.2f;` | Интервал обновления габаритов, с. |
| свойство | `Target` | `public Transform Target { get { return target; } }` | Текущая цель (диагностика). |
| метод | `Clear` | `public void Clear()` | Скрыть подсветку. |
| метод | `Repaint` | `public void Repaint()` | Перекрасить рамку под текущую тему. |
| метод | `SetTarget` | `public void SetTarget(Transform value)` | Показать рамку вокруг объекта (null — скрыть). |

### `class KvSettingEntry`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvSettingsView.cs` (строка 11)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `id` | `public string id = "";` |  |
| поле | `note` | `public string note = "";` |  |
| поле | `options` | `public string[] options = new string[0];` |  |
| поле | `tab` | `public string tab = "Функции";` |  |
| поле | `title` | `public string title = "";` |  |
| поле | `type` | `public string type = "toggle"; // toggle \| choice \| info \| group` |  |
| поле | `value` | `public string value = "";` |  |

### `class KvSettings`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvSettings.cs` (строка 14)
- **Назначение:** Хранилище настроек интерфейса (PlayerPrefs): включение/отключение функций, масштаб и плотность интерфейса. Значения переживают перезапуск и доступны панели «Настройки» (вкладка «Функции») без правок кода. Это ЧИСТОЕ хранилище: оно ничего не применяет само — применение делает KazistovVvUIManager (…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `DefaultAutoRefreshTree` | `public const bool DefaultAutoRefreshTree = true;` |  |
| поле | `DefaultColorBlind` | `public const int DefaultColorBlind = 0;` |  |
| поле | `DefaultDensity` | `public const int DefaultDensity = 0; // 0 — компактная, 1 — обычная` |  |
| поле | `DefaultErrorPlate` | `public const bool DefaultErrorPlate = true;` |  |
| поле | `DefaultFlashlight` | `public const bool DefaultFlashlight = false;` |  |
| поле | `DefaultFontSize` | `public const int DefaultFontSize = 1; // 1 — средний (как было)` |  |
| поле | `DefaultGamepadHud` | `public const bool DefaultGamepadHud = true;` |  |
| поле | `DefaultHighContrast` | `public const bool DefaultHighContrast = false;` |  |
| поле | `DefaultJointLimits` | `public const bool DefaultJointLimits = true;` |  |
| поле | `DefaultKeyboardNav` | `public const bool DefaultKeyboardNav = true;` |  |
| поле | `DefaultMetrics` | `public const bool DefaultMetrics = true;` |  |
| поле | `DefaultPhantoms` | `public const bool DefaultPhantoms = true;` |  |
| поле | `DefaultToolbarCompact` | `public const bool DefaultToolbarCompact = false;` |  |
| поле | `DefaultToolbarLayout` | `public const int DefaultToolbarLayout = 0; // 0 — авто (по ширине канваса)` |  |
| поле | `DefaultUiScale` | `public const float DefaultUiScale = 1f;` |  |
| поле | `DefaultWorkspace` | `public const bool DefaultWorkspace = true;` |  |
| поле | `KeyAutoRefreshTree` | `public const string KeyAutoRefreshTree = "KazistovVv.Show.AutoRefreshTree";` |  |
| поле | `KeyColorBlind` | `public const string KeyColorBlind = "KazistovVv.UI.ColorBlind";` | ЭТАП 11: схема для дальтоников (0 — нет, 1 — дейтеранопия, 2 — протанопия, 3 — тританопия). |
| поле | `KeyDensity` | `public const string KeyDensity = "KazistovVv.UI.Density";` |  |
| поле | `KeyErrorPlate` | `public const string KeyErrorPlate = "KazistovVv.UI.ErrorPlate";` | ЭТАП 8: показывать красную плашку ошибок в углу (0/1). |
| поле | `KeyFlashlight` | `public const string KeyFlashlight = "KazistovVv.Show.Flashlight";` |  |
| поле | `KeyFontSize` | `public const string KeyFontSize = "KazistovVv.UI.FontSize";` | ЭТАП 11: размер шрифта интерфейса (0 — маленький, 1 — средний, 2 — большой). |
| поле | `KeyGamepadHud` | `public const string KeyGamepadHud = "KazistovVv.UI.GamepadHud";` | ЭТАП 9: виртуальный геймпад (мини-индикатор) включён (0/1). |
| поле | `KeyGroupPrefix` | `public const string KeyGroupPrefix = "KazistovVv.UI.ToolbarGroup.";` | Префикс ключа «группа тулбара свёрнута» (ЭТАП 1). |
| поле | `KeyHighContrast` | `public const string KeyHighContrast = "KazistovVv.UI.HighContrast";` | ЭТАП 11: режим высокого контраста (0/1). |
| поле | `KeyJointLimits` | `public const string KeyJointLimits = "KazistovVv.Show.JointLimits";` |  |
| поле | `KeyKeyboardNav` | `public const string KeyKeyboardNav = "KazistovVv.UI.KeyboardNav";` | ЭТАП 11: навигация с клавиатуры (Tab/стрелки/Enter) включена (0/1). |
| поле | `KeyMetrics` | `public const string KeyMetrics = "KazistovVv.Show.Metrics";` |  |
| поле | `KeyPhantoms` | `public const string KeyPhantoms = "KazistovVv.Show.Phantoms";` |  |
| поле | `KeyToolbarCompact` | `public const string KeyToolbarCompact = "KazistovVv.UI.ToolbarCompact";` | ФИКС 9: компактный тулбар (0/1). |
| поле | `KeyToolbarLayout` | `public const string KeyToolbarLayout = "KazistovVv.UI.ToolbarLayout";` | ФИКС 8: раскладка тулбара (0 — авто, 1 — широко, 2 — 5 в ряд). |
| поле | `KeyToolbarRows` | `public const string KeyToolbarRows = "KazistovVv.UI.ToolbarRows";` |  |
| поле | `KeyUiScale` | `public const string KeyUiScale = "KazistovVv.UI.Scale";` |  |
| поле | `KeyWorkspace` | `public const string KeyWorkspace = "KazistovVv.Show.Workspace";` |  |
| свойство | `AutoRefreshTree` | `public static bool AutoRefreshTree { get { Ensure(); return autoTree; } set { autoTree = value; Save(KeyAutoRefreshTree, value ? 1 : 0); } }` | Автообновление дерева моделей. |
| свойство | `ColorBlind` | `public static int ColorBlind { get { Ensure(); return colorBlind; } set { colorBlind = Mathf.Clamp(value, 0, 3); Save(KeyColorBlind, colorBlind); } }` | ЭТАП 11: схема для дальтоников (0 — выключена, 1…3 — тип). |
| свойство | `Density` | `public static int Density { get { Ensure(); return density; } set { density = Mathf.Clamp(value, 0, 1); Save(KeyDensity, density); } }` | Плотность строк: 0 — компактная, 1 — обычная. |
| свойство | `ErrorPlate` | `public static bool ErrorPlate { get { Ensure(); return errorPlate; } set { errorPlate = value; Save(KeyErrorPlate, value ? 1 : 0); } }` | ЭТАП 8: красная плашка ошибок в углу экрана. |
| свойство | `Flashlight` | `public static bool Flashlight { get { Ensure(); return flashlight; } set { flashlight = value; Save(KeyFlashlight, value ? 1 : 0); } }` | Фонарик включён. |
| свойство | `FontSize` | `public static int FontSize { get { Ensure(); return fontSize; } set { fontSize = Mathf.Clamp(value, 0, 2); Save(KeyFontSize, fontSize); } }` | ЭТАП 11: размер шрифта (0 маленький … 2 большой). |
| свойство | `GamepadHud` | `public static bool GamepadHud { get { Ensure(); return gamepadHud; } set { gamepadHud = value; Save(KeyGamepadHud, value ? 1 : 0); } }` | ЭТАП 9: мини-индикатор геймпада на экране. |
| свойство | `HighContrast` | `public static bool HighContrast { get { Ensure(); return highContrast; } set { highContrast = value; Save(KeyHighContrast, value ? 1 : 0); } }` | ЭТАП 11: высокий контраст. |
| свойство | `IconButtonSize` | `public static float IconButtonSize { get { return Density == 0 ? 24f : 28f; } }` | Сторона квадратной кнопки-иконки тулбара. |
| свойство | `KeyboardNav` | `public static bool KeyboardNav { get { Ensure(); return keyboardNav; } set { keyboardNav = value; Save(KeyKeyboardNav, value ? 1 : 0); } }` | ЭТАП 11: навигация по интерфейсу с клавиатуры. |
| свойство | `RowHeight` | `public static float RowHeight { get { return Density == 0 ? 18f : 22f; } }` | Высота строки дерева с учётом плотности. |
| свойство | `ShowJointLimits` | `public static bool ShowJointLimits { get { Ensure(); return jointLimits; } set { jointLimits = value; Save(KeyJointLimits, value ? 1 : 0); } }` | Показывать индикаторы лимитов суставов. |
| свойство | `ShowMetrics` | `public static bool ShowMetrics { get { Ensure(); return metrics; } set { metrics = value; Save(KeyMetrics, value ? 1 : 0); } }` | Показывать панель метрик траекторий. |
| свойство | `ShowPhantoms` | `public static bool ShowPhantoms { get { Ensure(); return phantoms; } set { phantoms = value; Save(KeyPhantoms, value ? 1 : 0); } }` | Показывать фантомы (задел: пока только флаг, см. PROJECT_CONTEXT). |
| свойство | `ShowWorkspace` | `public static bool ShowWorkspace { get { Ensure(); return workspace; } set { workspace = value; Save(KeyWorkspace, value ? 1 : 0); } }` | Показывать зону достижимости. |
| свойство | `ToolbarCompact` | `public static bool ToolbarCompact { get { Ensure(); return toolbarCompact; } set { toolbarCompact = value; Save(KeyToolbarCompact, value ? 1 : 0); } }` | ФИКС 9: компактный тулбар — мелкие иконки и скрытие редко используемых кнопок. |
| свойство | `ToolbarLayout` | `public static int ToolbarLayout { get { Ensure(); return toolbarLayout; } set { toolbarLayout = Mathf.Clamp(value, 0, 2); Save(KeyToolbarLayout, toolbarLayout); } }` | ФИКС 8: раскладка тулбара — 0 «авто» (колонки считаются от ширины канваса), 1 «широко» (занять всю доступную ширину), 2 «5 в ряд» (исторический вид). |
| свойство | `ToolbarRows` | `public static int ToolbarRows { get { Ensure(); return toolbarRows; } set { toolbarRows = Mathf.Clamp(value, 1, 6); Save(KeyToolbarRows, toolbarRows); } }` | Сколько рядов кнопок в тулбаре (3 — базовый интерфейс, 4 — с кнопками этапов 1–20). |
| свойство | `UiScale` | `public static float UiScale { get { Ensure(); return uiScale; } set { uiScale = Mathf.Clamp(value, 0.6f, 1.6f); Save(KeyUiScale, uiScale); } }` | Масштаб интерфейса (0.85 / 1 / 1.15 / 1.3). |
| метод | `FontScale` | `public static float FontScale` | Множитель размера шрифта для текущей настройки (ЭТАП 11). |
| метод | `IsToolbarGroupCollapsed` | `public static bool IsToolbarGroupCollapsed(string groupId)` | ЭТАП 1: свёрнута ли группа тулбара в одну иконку. Состояние каждой группы — отдельный ключ PlayerPrefs, поэтому оно переживает перезапуск и пересборку. |
| метод | `Reload` | `public static void Reload()` | Перечитать настройки из PlayerPrefs. |
| метод | `ResetToDefaults` | `public static void ResetToDefaults()` | Сбросить настройки интерфейса к значениям по умолчанию. |
| метод | `SetToolbarGroupCollapsed` | `public static void SetToolbarGroupCollapsed(string groupId, bool collapsed)` | ЭТАП 1: свернуть/развернуть группу тулбара. |

### `class KvSettingsCallbacks`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvSettingsView.cs` (строка 231)
- **Назначение:** Колбэки панели настроек (реализует менеджер UI).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `OpenHotkeys` | `public Action OpenHotkeys;` | Открыть окно горячих клавиш (F12). |
| поле | `OpenPalette` | `public Action OpenPalette;` | Открыть палитру команд (Ctrl+P). |
| поле | `ResetAll` | `public Action ResetAll;` | Сброс настроек интерфейса. |
| поле | `ResetLayout` | `public Action ResetLayout;` | Сбросить раскладку окон и групп тулбара (ЭТАП 3). |
| поле | `SetColorBlind` | `public Action<int> SetColorBlind;` | Схема для дальтоников (0 — выключена). |
| поле | `SetDensity` | `public Action<int> SetDensity;` | Сменить плотность (0 — компактная). |
| поле | `SetFontSize` | `public Action<int> SetFontSize;` | Сменить размер шрифта (0 маленький … 2 большой). |
| поле | `SetGamepadHud` | `public Action<bool> SetGamepadHud;` | Виртуальный геймпад на экране (ЭТАП 9). |
| поле | `SetHighContrast` | `public Action<bool> SetHighContrast;` | Высокий контраст. |
| поле | `SetKeyboardNav` | `public Action<bool> SetKeyboardNav;` | Навигация по интерфейсу с клавиатуры (Tab/стрелки/Enter). |
| поле | `SetScale` | `public Action<float> SetScale;` | Сменить масштаб интерфейса. |
| поле | `SetTheme` | `public Action<KvThemeMode> SetTheme;` | Сменить тему. |
| поле | `SetToolbarCompact` | `public Action<bool> SetToolbarCompact;` | ФИКС 9: включить/выключить компактный тулбар. |
| поле | `SetToolbarLayout` | `public Action<int> SetToolbarLayout;` | ФИКС 8: сменить раскладку тулбара (0 — авто, 1 — широко, 2 — «5 в ряд»). |
| поле | `SetToolbarRows` | `public Action<int> SetToolbarRows;` | Сменить число рядов тулбара. |

### `class KvSettingsFile`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvSettingsView.cs` (строка 34)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `bindings` | `public KvBindingEntry[] bindings = new KvBindingEntry[0];` |  |
| поле | `items` | `public KvSettingEntry[] items = new KvSettingEntry[0];` |  |

### `class KvSettingsSchema`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvSettingsView.cs` (строка 46)
- **Назначение:** Схема панели настроек: встроенный набор пунктов + внешние пункты из `StreamingAssets/kazistovvv_settings.json` (ТЗ: «новые пункты добавляются без перекомпиляции»). Внешние пункты с неизвестным `id` показываются, но помечаются как «нет обработчика» — их можно подключить кодом позже.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `ExternalFileName` | `public const string ExternalFileName = "kazistovvv_settings.json";` | Имя файла расширения в StreamingAssets. |
| метод | `Bindings` | `public static List<KvBindingEntry> Bindings()` | Таблица биндов — ТОЛЬКО ДЛЯ ОТОБРАЖЕНИЯ (ТЗ: «сами бинды не трогать»). ЭТАП 7: единственный источник — реестр KvBindings (клавиатура, мышь, геймпад), поэтому вкладка «Управление» и окно «Горячие клавиши» (F12) показывают ОДНО И ТО ЖЕ и не расходятся между собой. |
| метод | `External` | `public static KvSettingsFile External` | Загруженный внешний файл (может быть пустым). |
| метод | `ExternalStatus` | `public static string ExternalStatus` | Путь внешнего файла и результат загрузки (диагностика). |
| метод | `Features` | `public static List<KvSettingEntry> Features()` | Встроенные пункты вкладки «Функции» (id → подпись). |

### `class KvSettingsView`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvSettingsView.cs` (строка 285)
- **Назначение:** Содержимое панели «Настройки» (dock-панель): вкладки «Функции», «Управление», «Интерфейс», «О программе». Состав вкладок — из KvSettingsSchema, поэтому список расширяется данными (StreamingAssets), а не правкой этого файла.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `ActiveTab` | `public int ActiveTab { get; private set; }` | Активная вкладка (0…3). |
| свойство | `RowCount` | `public int RowCount { get { return rows.Count; } }` | Сколько строк построено на вкладке (диагностика). |
| метод | `Build` | `public void Build(RectTransform parent, KvSettingsCallbacks cbs)` |  |
| метод | `RefreshValues` | `public void RefreshValues()` | Обновить отрисовку при внешнем изменении (например, темы). |
| метод | `Repaint` | `public void Repaint()` | Перекрасить панель под текущую тему. |
| метод | `SetTab` | `public void SetTab(int index)` | Показать вкладку (0…3). |

### `class KvSplitter`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvDockPanel.cs` (строка 967)
- **Назначение:** Изменение толщины пристыкованной панели перетаскиванием внутренней кромки.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Bind` | `public void Bind(KvDockPanel owner, RectTransform canvas)` |  |
| метод | `OnDrag` | `public void OnDrag(PointerEventData e)` |  |
| метод | `OnEndDrag` | `public void OnEndDrag(PointerEventData e)` |  |
| метод | `OnPointerDown` | `public void OnPointerDown(PointerEventData e)` |  |
| метод | `OnPointerEnter` | `public void OnPointerEnter(PointerEventData e)` |  |
| метод | `OnPointerExit` | `public void OnPointerExit(PointerEventData e)` |  |

### `class KvStatusBar`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvStatusBar.cs` (строка 13)
- **Назначение:** Нижняя панель — СТАТУС-БАР в стиле FreeCAD: слева состояние State Machine, выбранный робот и координаты луча; в центре — статусные сообщения (те же, что идут в лог/подсказки потока); справа — индикатор темы и FPS. Обновление текста — по вызову менеджера (10 раз в секунду), поэтому панель не влияе…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Height` | `public const float Height = 22f;` | Высота статус-бара. |
| свойство | `CursorText` | `public string CursorText { get { return cursorText != null ? cursorText.text : ""; } }` | Текущий текст координат (диагностика). |
| свойство | `MessageText` | `public string MessageText { get { return messageText != null ? messageText.text : ""; } }` | Текущий текст сообщения (диагностика). |
| свойство | `RobotText` | `public string RobotText { get { return robotText != null ? robotText.text : ""; } }` | Текущий текст робота (диагностика). |
| свойство | `Root` | `public RectTransform Root { get { return root; } }` | Корень панели. |
| свойство | `StateText` | `public string StateText { get { return stateText != null ? stateText.text : ""; } }` | Текущий текст состояния (диагностика). |
| свойство | `ThemeText` | `public string ThemeText { get { return themeText != null ? themeText.text : ""; } }` | Текущий текст темы (диагностика). |
| метод | `Build` | `public float Build(RectTransform canvas, Canvas owner)` | Собрать статус-бар. Возвращает высоту. |
| метод | `Repaint` | `public void Repaint()` | Перекрасить статус-бар под текущую тему. |
| метод | `SetCursor` | `public void SetCursor(string text)` | Координаты курсора/луча. |
| метод | `SetFps` | `public void SetFps(float fps)` | FPS-счётчик (проверка «UI не влияет на FPS»). |
| метод | `SetHint` | `public void SetHint(string text)` | Служебная подсказка справа (TAB, режим UI и т.п.). |
| метод | `SetMessage` | `public void SetMessage(string text, Color color)` | Статусное сообщение (то же, что в логе потока). |
| метод | `SetRobot` | `public void SetRobot(string text)` | Выбранный/активный робот. |
| метод | `SetState` | `public void SetState(string text, Color color)` | Состояние State Machine (Idle / PointSelected / …). |
| метод | `SetTheme` | `public void SetTheme(string text)` | Индикатор темы («Тёмная», «Светлая», «Системная»). |

### `class KvSwitch`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvWidgets.cs` (строка 368)
- **Назначение:** Переключатель (галка) панели настроек.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Locked` | `public bool Locked { get; set; }` | Заблокирован (нет обработчика — пункт «в разработке»). |
| свойство | `Value` | `public bool Value { get; private set; }` |  |

### `class KvTheme`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvTheme.cs` (строка 23)
- **Назначение:** Тема и фабрика элементов десктопного интерфейса KazistovVv в стиле FreeCAD: плотная инженерная гамма, тонкие рамки, мелкий шрифт, иконки-монохром. Цвета — СВОЙСТВА (а не readonly-поля): палитра меняется переключателем темы мгновенно, без перезагрузки. Все элементы интерфейса создаются этой фабрик…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `BaseFontSize` | `public const int BaseFontSize = 13;` | Базовые размеры (до применения настройки размера шрифта, ЭТАП 11). |
| поле | `BaseFontSizeSmall` | `public const int BaseFontSizeSmall = 11;` |  |
| поле | `BaseFontSizeTitle` | `public const int BaseFontSizeTitle = 14;` |  |
| поле | `PrefsThemeKey` | `public const string PrefsThemeKey = "KazistovVv.Theme.Mode";` | PlayerPrefs-ключ выбранного режима темы. |
| свойство | `Accent` | `public static Color Accent { get { return Hi(Light ? C(0.130f, 0.400f, 0.740f) : C(0.360f, 0.620f, 0.950f), Light ? C(0.000f, 0.220f, 0.700f) : C(0.400f, 0.760f, 1.000f)); } }` |  |
| свойство | `Border` | `public static Color Border { get { return Hi(Light ? C(0.560f, 0.570f, 0.590f) : C(0.075f, 0.080f, 0.092f), Light ? C(0.300f, 0.310f, 0.330f) : C(0.850f, 0.860f, 0.880f)); } }` |  |
| свойство | `ButtonActive` | `public static Color ButtonActive { get { return ButtonChecked; } }` |  |
| свойство | `ButtonBg` | `public static Color ButtonBg { get { return Hi(Light ? C(0.735f, 0.740f, 0.755f) : C(0.250f, 0.255f, 0.275f), Light ? C(0.880f, 0.884f, 0.892f) : C(0.090f, 0.092f, 0.100f)); } }` |  |
| свойство | `ButtonChecked` | `public static Color ButtonChecked { get { return Hi(Light ? C(0.545f, 0.715f, 0.920f) : C(0.180f, 0.400f, 0.660f), Light ? C(0.250f, 0.560f, 0.980f) : C(0.100f, 0.430f, 0.950f))…` |  |
| свойство | `ButtonHover` | `public static Color ButtonHover { get { return Hi(Light ? C(0.830f, 0.836f, 0.852f) : C(0.325f, 0.335f, 0.365f), Light ? C(0.960f, 0.960f, 0.965f) : C(0.220f, 0.225f, 0.240f)); } }` |  |
| свойство | `ButtonPressed` | `public static Color ButtonPressed { get { return Hi(Light ? C(0.640f, 0.650f, 0.670f) : C(0.190f, 0.196f, 0.212f), Light ? C(0.740f, 0.745f, 0.760f) : C(0.030f, 0.032f, 0.038f))…` |  |
| свойство | `Error` | `public static Color Error { get { return Semantic(Hi(Light ? C(0.750f, 0.140f, 0.110f) : C(0.920f, 0.320f, 0.300f), Light ? C(0.700f, 0.000f, 0.000f) : C(1.000f, 0.250f, 0.200f)…` |  |
| свойство | `FontSize` | `public static int FontSize { get { return ScaleFont(13); } }` | Размер основного шрифта (плотная компоновка). ЭТАП 11: значение УМНОЖАЕТСЯ на выбранный в настройках масштаб шрифта (маленький / средний / большой), поэтому отдельная настройка размера действует на весь интерфейс сразу. |
| свойство | `FontSizeSmall` | `public static int FontSizeSmall { get { return ScaleFont(11); } }` | Размер мелкого шрифта (дерево, статус-бар, подписи). |
| свойство | `FontSizeTitle` | `public static int FontSizeTitle { get { return ScaleFont(14); } }` | Размер шрифта заголовков панелей/окон. |
| свойство | `IconTint` | `public static Color IconTint { get { return Hi(Light ? C(0.150f, 0.160f, 0.180f) : C(0.860f, 0.870f, 0.890f), Light ? C(0f, 0f, 0f) : C(1f, 1f, 1f)); } }` |  |
| свойство | `InputBg` | `public static Color InputBg { get { return Hi(Light ? C(0.955f, 0.958f, 0.965f) : C(0.100f, 0.104f, 0.115f), Light ? C(1f, 1f, 1f) : C(0f, 0f, 0f)); } }` |  |
| свойство | `LaserGreen` | `public static Color LaserGreen { get { return SematicGreen(); } }` | Зелёный лазер (выбор траектории). |
| свойство | `LaserRed` | `public static Color LaserRed { get { return Semantic(Hi(Light ? C(0.780f, 0.130f, 0.120f) : C(1.000f, 0.180f, 0.160f), Light ? C(0.800f, 0.000f, 0.000f) : C(1.000f, 0.100f, 0.05…` | Красный лазер (выбор точки) — только как индикатор состояния кнопки. |
| свойство | `Ok` | `public static Color Ok { get { return Semantic(Hi(Light ? C(0.130f, 0.540f, 0.180f) : C(0.360f, 0.800f, 0.400f), Light ? C(0.000f, 0.420f, 0.100f) : C(0.350f, 1.000f, 0.450f)), …` |  |
| свойство | `PanelBg` | `public static Color PanelBg { get { return Hi(Light ? C(0.815f, 0.820f, 0.835f) : C(0.160f, 0.163f, 0.175f), Light ? C(0.960f, 0.960f, 0.965f) : C(0.020f, 0.020f, 0.024f)); } }` |  |
| свойство | `PanelDark` | `public static Color PanelDark { get { return Hi(Light ? C(0.930f, 0.932f, 0.940f) : C(0.112f, 0.115f, 0.126f), Light ? C(1f, 1f, 1f) : C(0f, 0f, 0f)); } }` |  |
| свойство | `PanelHeader` | `public static Color PanelHeader { get { return Hi(Light ? C(0.745f, 0.752f, 0.772f) : C(0.205f, 0.210f, 0.228f), Light ? C(0.900f, 0.905f, 0.915f) : C(0.060f, 0.062f, 0.070f)); } }` |  |
| свойство | `RowAlt` | `public static Color RowAlt { get { return Hi(Light ? C(0.870f, 0.874f, 0.884f) : C(0.148f, 0.152f, 0.165f), Light ? C(0.930f, 0.933f, 0.940f) : C(0.070f, 0.072f, 0.080f)); } }` |  |
| свойство | `SelectionBg` | `public static Color SelectionBg { get { return Hi(Light ? C(0.560f, 0.720f, 0.930f) : C(0.170f, 0.380f, 0.640f), Light ? C(0.150f, 0.520f, 1.000f) : C(0.050f, 0.400f, 0.980f)); } }` |  |
| свойство | `Separator` | `public static Color Separator { get { return Hi(Light ? C(0.650f, 0.660f, 0.680f) : C(0.280f, 0.290f, 0.315f), Light ? C(0.420f, 0.430f, 0.450f) : C(0.700f, 0.710f, 0.730f)); } }` |  |
| свойство | `TextDim` | `public static Color TextDim { get { return Hi(Light ? C(0.330f, 0.340f, 0.360f) : C(0.620f, 0.640f, 0.672f), Light ? C(0.070f, 0.075f, 0.085f) : C(0.860f, 0.870f, 0.890f)); } }` |  |
| свойство | `TextDisabled` | `public static Color TextDisabled { get { return Hi(Light ? C(0.560f, 0.570f, 0.585f) : C(0.400f, 0.410f, 0.430f), Light ? C(0.330f, 0.340f, 0.360f) : C(0.620f, 0.630f, 0.650f));…` |  |
| свойство | `TextMain` | `public static Color TextMain { get { return Hi(Light ? C(0.090f, 0.095f, 0.105f) : C(0.900f, 0.910f, 0.930f), Light ? C(0f, 0f, 0f) : C(1f, 1f, 1f)); } }` |  |
| свойство | `Warn` | `public static Color Warn { get { return Semantic(Hi(Light ? C(0.720f, 0.480f, 0.020f) : C(0.950f, 0.720f, 0.200f), Light ? C(0.700f, 0.420f, 0.000f) : C(1.000f, 0.800f, 0.100f))…` |  |
| свойство | `WindowBg` | `public static Color WindowBg { get { return Hi(Light ? C(0.855f, 0.860f, 0.870f) : C(0.125f, 0.128f, 0.138f), Light ? C(1f, 1f, 1f) : C(0f, 0f, 0f)); } }` |  |
| событие | `Changed` | `public static event System.Action Changed;` | Тема изменилась (пересобрать оболочку/перекрасить оверлеи). |
| метод | `ColorBlindMode` | `public static int ColorBlindMode` | ЭТАП 11: выбранная схема для дальтоников (0 — выключена). |
| метод | `CreateButton` | `public static Button CreateButton(RectTransform parent, string name, string label,` | Плотная кнопка с подписью (диалоги/панели). |
| метод | `CreateDivider` | `public static Image CreateDivider(RectTransform parent, bool vertical = false)` | Тонкая линия-разделитель. |
| метод | `CreateFrame` | `public static Image CreateFrame(RectTransform parent, string name, Color fill, Color border)` | Панель с рамкой в стиле FreeCAD (тонкая линия по контуру). |
| метод | `CreatePanel` | `public static Image CreatePanel(RectTransform parent, string name, Color color)` |  |
| метод | `CreateSmallButton` | `public static Button CreateSmallButton(RectTransform parent, string name, string label,` |  |
| метод | `CreateText` | `public static Text CreateText(RectTransform parent, string name, string content, int size,` |  |
| метод | `Cycle` | `public static KvThemeMode Cycle()` | Переключить режим (Тёмная → Светлая → Системная → Тёмная). |
| метод | `Font` | `public static Font Font` | Шрифт интерфейса. С ЭТАПА 2 (мультиязычность) зависит от языка: для китайского и японского берётся системный шрифт с CJK-глифами (`KvLoc.Font`), потому что встроенный LegacyRuntime.ttf их не содержит и текст выглядел бы квадратами. Для остальных языков поведение прежнее — встроенный шрифт (ноль и… |
| метод | `HighContrast` | `public static bool HighContrast` | ЭТАП 11: высокий контраст включён (отдельный режим интерфейса). |
| метод | `IsLight` | `public static bool IsLight` | Фактически применённая палитра светлая? |
| метод | `Mode` | `public static KvThemeMode Mode` | Выбранный режим (может быть «системная»). |
| метод | `ModeIcon` | `public static string ModeIcon` | Идентификатор иконки для текущего режима (кнопка «Тема»). |
| метод | `ModeLabel` | `public static string ModeLabel` | «Тёмная» / «Светлая» / «Системная». |
| метод | `PaletteLabel` | `public static string PaletteLabel` | Подпись фактической палитры: «тёмная» / «светлая» (+ пометки доступности). |
| метод | `Ratio` | `public static Color Ratio(Color target, Color baseColor)` | Множитель цвета для ColorBlock кнопки (hover/pressed от базового). |
| метод | `Reload` | `public static void Reload()` | Перечитать тему из PlayerPrefs (вызывается на старте). |
| метод | `ScaleFont` | `public static int ScaleFont(int baseSize)` | Размер шрифта с учётом настройки «Размер шрифта» (ЭТАП 11). |
| метод | `SetMode` | `public static void SetMode(KvThemeMode value, bool save = true)` | Задать режим темы (мгновенно, с сохранением в PlayerPrefs). |
| метод | `Stretch` | `public static void Stretch(RectTransform rt, float padL = 0, float padR = 0,` |  |
| метод | `TextFor` | `public static Color TextFor(bool selected, bool enabled)` | Цвет текста для строки дерева/таблицы по состоянию. |
| метод | `WhiteSprite` | `public static Sprite WhiteSprite` | Белая текстура-спрайт (без файлов ассетов). |

### `enum KvThemeMode`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvTheme.cs` (строка 7)
- **Назначение:** Режим темы интерфейса (ТЗ: Тёмная / Светлая / Системная).

_Публичных членов нет (или тип объявлен без них)._

### `class KvToolbar`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvToolbar.cs` (строка 28)
- **Назначение:** Верхняя панель инструментов в стиле FreeCAD: ПЛОТНЫЙ блок кнопок-иконок БЕЗ подписей, разложенный ПО ЛОГИЧЕСКИМ ГРУППАМ (ЭТАП 1). Что изменилось относительно «плоской сетки»: • кнопки идут не одним потоком, а группами («Робот», «Точка и траектория», «Постобработка», «Визуализация», «Запись и эксп…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `BlockH` | `public float BlockH;` |  |
| поле | `BlockW` | `public float BlockW;` |  |
| поле | `Columns` | `public const int Columns = 5;` | Минимум колонок в одной группе (историческое «5 в ряд» — как минимум для панели). |
| поле | `CompactIconSize` | `public const float CompactIconSize = 20f;` | Сторона кнопки-иконки в компактном режиме (ФИКС 9: 20 px вместо 24/28). |
| поле | `Group` | `public KvToolbarGroup Group;` |  |
| поле | `GroupGap` | `public const float GroupGap = 8f;` | Расстояние между группами; линия-разделитель стоит по его центру. |
| поле | `GroupHandleWidth` | `public const float GroupHandleWidth = 15f;` | Ширина «ручки» группы (кнопка-иконка группы) — ЭТАП 1. |
| поле | `HasSeparator` | `public bool HasSeparator;` |  |
| поле | `Ids` | `public List<string> Ids;` |  |
| поле | `Line` | `public int Line;` |  |
| поле | `LineGap` | `public const float LineGap = 3f;` | Зазор между полосами групп при переносе (px). |
| поле | `LineHeight` | `public float LineHeight;` |  |
| поле | `RareCommandIds` | `public static readonly string[] RareCommandIds =` | ФИКС 9: редко используемые команды. Они ДУБЛИРУЮТСЯ пунктами меню, поэтому в компактном режиме не занимают место (в KvCommands/меню ничего не удаляется). |
| поле | `RightBlockWidth` | `public const float RightBlockWidth = 336f;` | Ширина правого блока «Верстак» — по ней считается место под кнопки. |
| поле | `RightGap` | `public const float RightGap = 16f;` | Зазор между сеткой кнопок и правым блоком (px канваса). |
| поле | `Rows` | `public int Rows;` |  |
| поле | `Separator` | `public bool Separator;` |  |
| поле | `X` | `public float X;` |  |
| поле | `Y` | `public float Y;` |  |
| свойство | `ButtonCount` | `public int ButtonCount { get { return buttons.Count; } }` | Сколько кнопок в тулбаре (диагностика). |
| свойство | `ButtonIds` | `public IReadOnlyList<string> ButtonIds { get { return ids; } }` | Список id кнопок (диагностика). |
| свойство | `ColumnsCount` | `public int ColumnsCount { get { return columns; } }` | Сколько кнопок в самой широкой группе (диагностика раскладки). |
| свойство | `GroupCount` | `public int GroupCount { get { return groupHandles.Count; } }` | Сколько «ручек» групп на панели (диагностика). |
| свойство | `GroupMenuOpen` | `public bool GroupMenuOpen { get { return groupMenu != null && groupMenu.IsOpen; } }` | Список группы открыт (диагностика). |
| свойство | `GroupMenuRows` | `public int GroupMenuRows { get { return groupMenu != null ? groupMenu.RowCount : 0; } }` | Сколько строк в открытом списке группы (диагностика). |
| свойство | `Height` | `public float Height { get; private set; }` | Высота панели (px канваса) — считается от числа полос групп. |
| свойство | `MoreCount` | `public int MoreCount { get { return 0; } }` | Сколько команд не попало в панель (в групповой раскладке — 0). |
| свойство | `Root` | `public RectTransform Root { get { return root; } }` | Прямоугольник панели (для раскладки). |
| метод | `Build` | `public float Build(RectTransform canvas, Canvas owner, string[] commandIds,` | Собрать тулбар. Возвращает его высоту. — режим раскладки (0 авто, 1 широко, 2 «5 в ряд»), — максимум рядов ВНУТРИ одной группы (по умолчанию 4), — компактный режим (мелкие иконки, без «редких» кнопок). |
| метод | `ButtonAt` | `public KvIconButton ButtonAt(int index)` | Кнопка по индексу (диагностика/расширение). |
| метод | `CloseGroupMenu` | `public void CloseGroupMenu()` | Закрыть список группы. |
| метод | `CollapsedGroupCount` | `public int CollapsedGroupCount` | Сколько групп свёрнуто (диагностика). |
| метод | `ExpandAllGroups` | `public void ExpandAllGroups()` | Развернуть все группы (кнопка «Сбросить раскладку»). |
| метод | `GroupOfButton` | `public string GroupOfButton(int index)` | Группа, которой принадлежит кнопка с индексом (диагностика/тесты). |
| метод | `IsGroupCollapsed` | `public bool IsGroupCollapsed(string groupId)` | Свёрнута ли группа в одну иконку (состояние — в PlayerPrefs). |
| метод | `OpenGroupMenu` | `public bool OpenGroupMenu(string groupId)` | Открыть список группы по её id (диагностика/тесты). |
| метод | `Refresh` | `public void Refresh()` | Обновить состояние кнопок (галки/доступность/иконки/подсказки). |
| метод | `Repaint` | `public void Repaint()` | Перекрасить под текущую тему. |
| метод | `SetGroupCollapsed` | `public void SetGroupCollapsed(string groupId, bool collapsed)` | Свернуть/развернуть группу и перестроить панель. |
| метод | `SetLayout` | `public void SetLayout(string[] commandIds)` | Заменить раскладку кнопок (для расширения/профилей). |
| метод | `SetWorkbench` | `public void SetWorkbench(int index)` | Активный «верстак» (0 — робот, 1 — SCARA). |

### `class KvToolbarGroup`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvToolbarGroups.cs` (строка 16)
- **Назначение:** ЛОГИЧЕСКАЯ ГРУППА тулбара (ЭТАП 1): «Робот», «Точка и траектория», «Постобработка», «Визуализация», «Запись и экспорт», «Сеть и автоматизация», «Настройки». Кнопки внутри группы идут ПОДРЯД и отделяются от соседней группы тонкой вертикальной линией; у каждой группы есть кнопка-иконка группы, по к…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Commands` | `public string[] Commands;` | Id команд группы в порядке показа. |
| поле | `Icon` | `public string Icon;` | Иконка группы (id из KvIcons). |
| поле | `Id` | `public string Id;` | Id группы («robot», «path», …) — он же ключ в PlayerPrefs. |
| поле | `Title` | `public string Title;` | Название группы для подсказки и меню. |

### `class KvToolbarGroupMenu`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvToolbarGroups.cs` (строка 176)
- **Назначение:** Выпадающий список КОМАНД ГРУППЫ (ЭТАП 1): открывается кнопкой-иконкой группы. Показывает все команды группы с названием и горячей клавишей, а также действие «свернуть/развернуть группу» (группа в одну иконку). Сделан локально по образцу `KvMenuBar`/`KvToolbarMoreMenu`: нет готового выпадающего сп…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `IsOpen` | `public bool IsOpen { get { return panel != null && panel.gameObject.activeSelf; } }` | Меню открыто (диагностика). |
| свойство | `RowCount` | `public int RowCount { get { return rows.Count; } }` | Сколько строк построено (диагностика). |
| метод | `Close` | `public void Close()` |  |
| метод | `Ensure` | `public void Ensure(RectTransform canvas, KvToolbar toolbar)` |  |
| метод | `Toggle` | `public void Toggle(RectTransform anchor, KvToolbarGroup group, IReadOnlyList<string> ids)` | Открыть/закрыть список группы под её кнопкой. |

### `class KvToolbarGroups`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvToolbarGroups.cs` (строка 37)
- **Назначение:** Каталог групп тулбара (ЭТАП 1). Группы — ДАННЫЕ: правка панели не нужна.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `All` | `public static KvToolbarGroup[] All` | Все группы в порядке показа. |
| метод | `Find` | `public static KvToolbarGroup Find(string id)` | Группа по id (null — нет такой). |
| метод | `GroupOf` | `public static int GroupOf(string commandId)` | Индекс группы, которой принадлежит команда (-1 — не разложена). |
| метод | `GroupTitleOf` | `public static string GroupTitleOf(string commandId)` | Название группы, которой принадлежит команда (для палитры/подсказок). |

### `class KvToolbarMoreMenu`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvToolbar.cs` (строка 626)
- **Назначение:** Выпадающий список «Ещё» (ФИКС 8) — оставлен для совместимости: в групповой раскладке (ЭТАП 1) ничего не скрывается, поэтому список не используется.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `IsOpen` | `public bool IsOpen { get { return false; } }` | Список сейчас открыт. |
| свойство | `RowCount` | `public int RowCount { get { return 0; } }` | Сколько команд в списке (диагностика). |
| метод | `Close` | `public void Close() { }` |  |
| метод | `Ensure` | `public void Ensure(RectTransform canvas, KvToolbar toolbar) { }` |  |
| метод | `Toggle` | `public void Toggle(RectTransform anchor, IReadOnlyList<string> commandIds) { }` |  |

### `class KvTooltip`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvTooltip.cs` (строка 13)
- **Назначение:** Всплывающая подсказка в стиле FreeCAD: появляется у курсора через Delay секунд после наведения, состоит из заголовка, необязательного описания и «бейджа» горячей клавиши. Один слой на весь интерфейс (последний sibling канваса — всегда сверху).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `cursorOffset` | `public Vector2 cursorOffset = new Vector2(16f, -18f);` | Смещение от курсора. |
| поле | `delay` | `public float delay = 0.35f;` | Задержка появления, с. |
| поле | `maxWidth` | `public float maxWidth = 330f;` | Максимальная ширина текста подсказки. |
| свойство | `Current` | `public static KvTooltip Current { get { return instance; } }` | Текущий слой подсказок (может быть null). |
| свойство | `CurrentBody` | `public string CurrentBody { get { return bodyText != null ? bodyText.text : ""; } }` | Текст описания показанной подсказки (диагностика). |
| свойство | `CurrentTitle` | `public string CurrentTitle { get { return titleText != null ? titleText.text : ""; } }` | Заголовок показанной подсказки (диагностика). |
| свойство | `IsShown` | `public bool IsShown { get { return visible; } }` | Подсказка сейчас на экране (диагностика). |
| метод | `Ensure` | `public static KvTooltip Ensure(RectTransform canvasTransform, Canvas ownerCanvas)` | Создать слой подсказок (вызывается один раз менеджером UI). |
| метод | `Release` | `public static void Release()` | Снять наведение. |
| метод | `Request` | `public static void Request(string title, string body, string hotkey)` | Подсказка (заголовок/описание/клавиша) для текущего наведения. |

### `class KvTooltipTarget`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvTooltip.cs` (строка 222)
- **Назначение:** Навешивается на любой элемент, у которого должна быть подсказка (кнопки-иконки тулбара, строки дерева, узлы статус-бара). Требует, чтобы у элемента был Graphic с raycastTarget = true.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `body` | `public string body = "";` | Описание (может быть пустым). |
| поле | `hotkey` | `public string hotkey = "";` | Горячая клавиша («Z», «ЛКМ», «Enter»). |
| поле | `stub` | `public bool stub;` | Пометка «в разработке» (заглушка). |
| поле | `title` | `public string title = "";` | Заголовок подсказки. |
| метод | `OnPointerEnter` | `public void OnPointerEnter(PointerEventData eventData)` |  |
| метод | `OnPointerExit` | `public void OnPointerExit(PointerEventData eventData)` |  |

### `class KvTreeRow`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvTreeView.cs` (строка 704)
- **Назначение:** Строка дерева: выбор, мультивыбор (Ctrl), ПКМ (контекстное меню), двойной клик.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Label` | `public Text Label;` |  |
| поле | `onSelect` | `public Action<ProjectNode> onSelect;` |  |
| свойство | `Node` | `public ProjectNode Node { get; private set; }` |  |
| метод | `Bind` | `public void Bind(ProjectNode node, KvTreeView view, Image bg, bool isVisible)` |  |
| метод | `OnPointerClick` | `public void OnPointerClick(PointerEventData e)` |  |
| метод | `SetSelected` | `public void SetSelected(bool value) { SetSelected(value, false); }` |  |
| метод | `SetSelected` | `public void SetSelected(bool primary, bool inMulti)` |  |

### `class KvTreeView`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvTreeView.cs` (строка 19)
- **Назначение:** Левая панель — ДЕРЕВО МОДЕЛЕЙ в стиле FreeCAD (combo view): иерархия с раскрывающимися узлами, «глазик» (скрыть/показать объект), переименование по двойному клику, выбор узла (подсветка объекта в сцене). Строки строятся по списку ProjectNode; пересборка — только когда список реально изменился (си…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `maxDepth` | `public int maxDepth = 6;` | Максимальная глубина отрисовки (защита от циклов). |
| свойство | `EditBuffer` | `public string EditBuffer { get { return editBuffer; } }` | Текст, который сейчас набран при переименовании (диагностика). |
| свойство | `IsRenaming` | `public bool IsRenaming { get { return editing != null; } }` | Идёт ли переименование узла. |
| свойство | `MultiSelection` | `public IReadOnlyList<ProjectNode> MultiSelection { get { return multiSelection; } }` | ЭТАП 6: узлы мультивыбора (Ctrl+клик). Первый — основной выбор. |
| свойство | `RebuildVersion` | `public int RebuildVersion { get; private set; }` | Счётчик пересборок дерева (по нему видно, пересобиралось ли дерево). |
| свойство | `Root` | `public RectTransform Root { get { return root; } }` | Внешняя геометрия панели (задаёт менеджер через KvDockPanel). |
| свойство | `RowCount` | `public int RowCount { get { return rows.Count; } }` | Сколько строк сейчас в дереве (диагностика). |
| свойство | `Selected` | `public ProjectNode Selected { get { return selected; } }` | Выбранный узел. |
| метод | `AppendToEdit` | `public void AppendToEdit(string text)` | Дописать текст в поле переименования (внешний источник: геймпад, IME, тест). |
| метод | `BeginRename` | `public void BeginRename(ProjectNode node, Text label)` | Начать переименование узла (двойной клик). |
| метод | `BeginRenameSelected` | `public bool BeginRenameSelected()` | Начать переименование ВЫБРАННОГО узла (внешний вызов/диагностика). |
| метод | `Build` | `public void Build(RectTransform canvas, Canvas owner, Action<ProjectNode> select,` |  |
| метод | `CancelRenameNow` | `public void CancelRenameNow()` | Отменить переименование (внешний вызов). |
| метод | `ClearMultiSelection` | `public void ClearMultiSelection()` | ЭТАП 6: очистить мультивыбор. |
| метод | `CollapseAll` | `public void CollapseAll()` |  |
| метод | `CommitRenameNow` | `public void CommitRenameNow()` | Подтвердить переименование (внешний вызов). |
| метод | `Expand` | `public void Expand(string key)` | Раскрыть узел по ключу. |
| метод | `ExpandAll` | `public void ExpandAll()` |  |
| метод | `FindByKey` | `public ProjectNode FindByKey(string key, int depth)` | Найти узел текущего дерева по ключу (для восстановления мультивыбора). |
| метод | `HandleRowClick` | `public void HandleRowClick(ProjectNode node, bool ctrl)` | ЭТАП 6: обработать клик по узлу с учётом модификаторов: Ctrl+клик — добавить/убрать узел из мультивыбора, обычный клик — единственный выбор. |
| метод | `HandleRowRightClick` | `public void HandleRowRightClick(ProjectNode node, Vector2 screenPosition)` | ЭТАП 6: правый клик — выбрать узел (если он не в наборе) и запросить меню. |
| метод | `IsExpanded` | `public bool IsExpanded(string key)` |  |
| метод | `IsObjectVisible` | `public static bool IsObjectVisible(ProjectNode node)` | Виден ли объект узла (по рендерерам, без смены hideFlags). |
| метод | `Rebuild` | `public void Rebuild(List<ProjectNode> newRoots, ProjectNode newSelected)` | Пересобрать дерево по списку корневых узлов. |
| метод | `Repaint` | `public void Repaint()` | Перекрасить дерево под текущую тему (без пересборки строк). |
| метод | `Select` | `public void Select(ProjectNode node)` | Выбрать узел (подсветить объект в сцене). |
| метод | `SelectionSet` | `public List<ProjectNode> SelectionSet()` | ЭТАП 6: выделенные узлы: основной + мультивыбор (без повторов). |
| метод | `SetObjectVisible` | `public void SetObjectVisible(ProjectNode node, bool visible)` | Скрыть/показать объект узла: выключаются ТОЛЬКО рендереры (визуальная видимость, как «глазик» во FreeCAD). Коллайдеры, скрипты, State Machine, планировщик и `CollisionWorld` не затрагиваются — объект остаётся препятствием. |
| метод | `SetSelected` | `public void SetSelected(ProjectNode node)` | Только выделение (без полной пересборки). |

### `class KvUiStates`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvUiStates.cs` (строка 21)
- **Назначение:** ЭЛЕМЕНТЫ СОСТОЯНИЙ (ЭТАП 8): интерфейс реагирует на события, а не молчит. • ERROR STATE — красная плашка в углу экрана с текстом ошибки/исключения и кнопкой «Подробнее» (разворачивает стек). Ошибки приходят из `Application.logMessageReceived`, то есть НЕ только в консоль. • LOADING STATE — полоса…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Current` | `public static KvUiStates Current { get { return instance; } }` | Активный модуль состояний. |
| свойство | `DetailsOpen` | `public bool DetailsOpen { get { return detailsOpen; } }` | Подробности ошибки развёрнуты. |
| свойство | `EmptyHint` | `public string EmptyHint { get { return emptyHintText; } }` | Текст подсказки пустого состояния (диагностика). |
| свойство | `ErrorCount` | `public static int ErrorCount { get; private set; }` | Сколько ошибок перехвачено за сессию (диагностика). |
| свойство | `ErrorVisible` | `public bool ErrorVisible { get { return errorPlate != null && errorPlate.gameObject.activeSelf; } }` | Плашка ошибки видна. |
| свойство | `LastError` | `public static string LastError { get; private set; }` | Последняя ошибка (диагностика). |
| свойство | `Loading` | `public bool Loading { get { return loading; } }` | Идёт загрузка/выполнение длительной операции. |
| свойство | `LoadingText` | `public string LoadingText { get { return loadingText; } }` | Подпись текущей загрузки (диагностика). |
| свойство | `Progress` | `public float Progress { get { return loadingProgress; } }` | Текущий прогресс (0..1; −1 — неопределённый). |
| метод | `Begin` | `public static void Begin(string label, float progress = -1f)` | Начать длительную операцию (планирование, запись, экспорт). |
| метод | `Create` | `public static KvUiStates Create(RectTransform canvas)` | Создать слой состояний на канвасе интерфейса. |
| метод | `EmptyHintAge` | `public float EmptyHintAge` | Секунд показана подсказка пустого состояния (диагностика). |
| метод | `End` | `public static void End()` | Завершить длительную операцию. |
| метод | `HideError` | `public void HideError()` | Скрыть плашку ошибки. |
| метод | `Repaint` | `public void Repaint()` | Перекрасить под текущую тему. |
| метод | `ReportError` | `public static void ReportError(string message, string details)` | Показать ошибку (из лога Unity или из кода). |
| метод | `SetEmptyHint` | `public static void SetEmptyHint(string text)` | ЭТАП 8: подсказка пустого состояния («Выберите точку красным лазером»). Пустая строка скрывает подсказку. |
| метод | `SetProgress` | `public static void SetProgress(float progress, string label = null)` | Обновить прогресс (0..1; −1 — «неопределённый», полоса бежит). |

### `class KvWidgets`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvWidgets.cs` (строка 13)
- **Назначение:** Виджеты десктопного интерфейса в стиле FreeCAD: кнопки-иконки, разделители, заголовки секций, строки «свойство = значение», переключатели и сегментные переключатели (радио-строка). Всё создаётся кодом — файлов-ассетов нет.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `CreateColumn` | `public static RectTransform CreateColumn(RectTransform parent, string name, float height,` |  |
| метод | `CreateRow` | `public static RectTransform CreateRow(RectTransform parent, string name, float height,` |  |
| метод | `Fit` | `public static LayoutElement Fit(GameObject go, float width, float height)` |  |
| метод | `IconButton` | `public static KvIconButton IconButton(RectTransform parent, string name, string iconId,` | Кнопка ТОЛЬКО с иконкой (без подписи) — основной элемент тулбара FreeCAD. |
| метод | `Label` | `public static Text Label(RectTransform parent, string name, string text, int size,` |  |
| метод | `Note` | `public static Text Note(RectTransform parent, string text, Color color, float height = 0f)` | Информационная строка (подсказка/пометка) в панели настроек. |
| метод | `PropertyRow` | `public static Text PropertyRow(RectTransform parent, string label, string value,` | Строка свойств: слева подпись, справа ЗНАЧЕНИЕ (только чтение). |
| метод | `SectionHeader` | `public static RectTransform SectionHeader(RectTransform parent, string title, float height = 20f)` | Заголовок секции панели (как «Tasks»/«Properties» в FreeCAD). |
| метод | `Segmented` | `public static KvSegmented Segmented(RectTransform parent, string name, string[] options,` | Сегментный переключатель (Тёмная \| Светлая \| Системная). |
| метод | `ToolbarSeparator` | `public static Image ToolbarSeparator(RectTransform parent)` | Вертикальный разделитель тулбара. |

### `class NodeLabels`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KazistovVvUIManager.cs` (строка 3465)
- **Назначение:** Пользовательские подписи узлов дерева: переименование во FreeCAD меняет ЯРЛЫК узла, а не имя объекта сцены (безопасно: имена объектов используют планировщик, столы и сборочные утилиты). Ключ — стабильный ключ узла.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Clear` | `public static void Clear()` |  |

### `class ObjectSpawner`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Data\ObjectSpawner.cs` (строка 17)
- **Назначение:** Спавн объектов «как кубик в Unity»: выбор типа → указание места лучом → создание. Созданные объекты регистрируются в RuntimeRegistry и попадают в дерево.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `robotTemplates` | `public RobotController[] robotTemplates;` |  |
| метод | `RefreshRobotTemplates` | `public RobotController[] RefreshRobotTemplates()` | Находит все шаблоны роботов в сцене (оригиналы, не RegisteredObject-копии). |
| метод | `SpawnRobot` | `public RegisteredObject SpawnRobot(Vector3 position, float yawDegrees)` | Спавнит копию робота-шаблона с поворотом вокруг вертикали (yaw, градусы). |
| метод | `SpawnRobot` | `public RegisteredObject SpawnRobot(Vector3 position, float yawDegrees,` | Спавнит копию конкретного шаблона (SCARA/6-осевой). |
| метод | `SpawnTable` | `public RegisteredObject SpawnTable(Vector3 surfacePoint, Vector3? upNormal = null)` | Спавнит стол. point — точка ПОВЕРХНОСТИ (пол/стол): низ стола «приклеивается» к ней (без утопленного по центру размещения). |

### `class ProjectNode`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Data\ProjectNode.cs` (строка 24)
- **Назначение:** Узел дерева моделей: любая сущность, которую пользователь видит слева и свойства которой показывает правая панель.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Children` | `public List<ProjectNode> Children;` |  |
| поле | `Details` | `public string Details = "";` | Короткая дополнительная строка справа в дереве (например «2.65 ю · 5.3 с»). |
| поле | `DisplayName` | `public string DisplayName;` |  |
| поле | `Id` | `public string Id;` |  |
| поле | `Key` | `public string Key;` | Стабильный ключ (переживает пересборку дерева) — по нему хранится состояние «раскрыт/свёрнут» и пользовательское имя узла. |
| поле | `Kind` | `public ProjectNodeKind Kind;` |  |
| поле | `Robot` | `public RobotController Robot; // если узел — робот` |  |
| поле | `Tag` | `public object Tag;` | Произвольные данные узла (например индекс траектории или Vector3 точки). |
| поле | `Tooltip` | `public string Tooltip = "";` | Полное описание для подсказки. |
| поле | `WorldTransform` | `public Transform WorldTransform; // объект в сцене (null для виртуальных узлов)` |  |
| свойство | `HasObject` | `public bool HasObject { get { return WorldTransform != null; } }` | Есть ли у узла связанный объект сцены. |
| метод | `CanHide` | `public bool CanHide` | Можно ли скрывать/показывать объект узла («глазик»). Результат кэшируется: обход рендереров делается один раз на узел, а не при каждой пересборке дерева. |
| метод | `IconId` | `public string IconId` | Идентификатор иконки узла. |
| метод | `ResetVisibilityCache` | `public void ResetVisibilityCache()` | Сбросить кэш «глазика» (например, после смены объекта узла). |

### `enum ProjectNodeKind`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Data\ProjectNode.cs` (строка 7)
- **Назначение:** Тип узла дерева моделей.

_Публичных членов нет (или тип объявлен без них)._

### `class RegisteredObject`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Data\RuntimeRegistry.cs` (строка 122)
- **Назначение:** Маркер на произвольных объектах сцены (столы и т.п.), чтобы реестр мог их найти и показать в дереве. Вешается автоматически при спавне.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `DisplayName` | `public string DisplayName = "Объект";` |  |
| поле | `Node` | `[HideInInspector] public ProjectNode Node;` |  |

### `class RuntimeRegistry`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Data\RuntimeRegistry.cs` (строка 11)
- **Назначение:** Единый реестр «проекта»: все роботы и объекты, которые показывает дерево слева. Наполняется автоматически из сцены и при спавне.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `Roots` | `public static readonly List<ProjectNode> Roots = new List<ProjectNode>();` |  |
| событие | `Changed` | `public static event Action Changed;` | Событие изменения реестра (после добавления/удаления). |
| метод | `Clear` | `public static void Clear()` |  |
| метод | `CreateNodeFor` | `public static ProjectNode CreateNodeFor(Transform world, RobotController robot)` |  |
| метод | `FindRobotNode` | `public static ProjectNode FindRobotNode(RobotController robot)` |  |
| метод | `IsHidden` | `public static bool IsHidden(GameObject go)` | Объект скрыт из иерархии (HideFlags.HideInHierarchy) — в дерево не попадает. |
| метод | `NotifyChanged` | `public static void NotifyChanged()` |  |
| метод | `RebuildFromScene` | `public static void RebuildFromScene()` | Полное перестроение реестра из сцены. |

### `class ScaraCableFollow`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\ScaraCableFollow.cs` (строка 11)
- **Назначение:** «Физика» кабеля SCARA (LS10-B702S_cable_2): нижняя точка кабеля неподвижно держится за base_1, а сама петля кабеля разворачивается так, чтобы её «дальний конец» (направление меша от пивота к центру) смотрел на точку LS10-B702S_J2_4. Кабель следует за рукой, оставаясь закреплённым у базы.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `baseAnchor` | `public Transform baseAnchor;` |  |
| поле | `baseAttachLocal` | `public Vector3 baseAttachLocal;` |  |
| поле | `cable` | `public Transform cable;` |  |
| поле | `followTarget` | `public Transform followTarget;` |  |
| поле | `rotationSpeed` | `public float rotationSpeed = 8f;` |  |
| поле | `targetAttachLocal` | `public Vector3 targetAttachLocal = Vector3.zero;` |  |
| метод | `Initialize` | `public void Initialize()` |  |

### `enum SpawnKind`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Data\ObjectSpawner.cs` (строка 6)
- **Назначение:** Какой объект пользователь хочет разместить в центре сцены.

_Публичных членов нет (или тип объявлен без них)._

### `class UIFactory`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvTheme.cs` (строка 421)
- **Назначение:** Разовые фабричные хелперы (белый спрайт).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `GetSprite` | `public static Sprite GetSprite()` | Белая текстура-спрайт для UI (без файлов ассетов). |

## Пространство имён `KazistovVvUI.EditorTools`

### `class KazistovVvMenu`

- **Файл:** `Assets\_Project\06_KazistovVv_UI\03_Scripts\Editor\KazistovVvMenu.cs` (строка 9)
- **Назначение:** Меню для быстрого создания KazistovVv-интерфейса в сцене.

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `CreateInScene` | `public static void CreateInScene()` |  |
| метод | `RebuildTree` | `public static void RebuildTree()` |  |
| метод | `RemoveFromScene` | `public static void RemoveFromScene()` |  |

## Пространство имён `KazistovVvTests`

### `class KvCalibrationTests`

- **Файл:** `Assets\_Project\08_Tests\Editor\KvCalibrationTests.cs` (строка 46)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `FlangeFrame_WithoutReadyValidator_ReturnsFalseAndIdentityOutputs` | `public void FlangeFrame_WithoutReadyValidator_ReturnsFalseAndIdentityOutputs()` |  |
| метод | `NextSymmetric` | `public float NextSymmetric()` | Число в диапазоне [-1, 1) с шагом 2^-23 (ровно представимо во float). |
| метод | `NextVector` | `public Vector3 NextVector(float sigma)` | Вектор шума с покомпонентным разбросом ±sigma. |
| метод | `Service_MarkCameraStub_ReportsStubAndKeepsCameraNotCalibrated` | `public void Service_MarkCameraStub_ReportsStubAndKeepsCameraNotCalibrated()` |  |
| метод | `Service_NewInstanceAndDataDefaults_AreClean` | `public void Service_NewInstanceAndDataDefaults_AreClean()` |  |
| метод | `Service_PlaneMethodSelection_FollowsOverrideAndRobotType` | `public void Service_PlaneMethodSelection_FollowsOverrideAndRobotType()` |  |
| метод | `Service_ResetMethods_KeepCountsZeroAndReportToMessage` | `public void Service_ResetMethods_KeepCountsZeroAndReportToMessage()` |  |
| метод | `Service_WithoutBoundRobot_AllOperationsRefuseAndReport` | `public void Service_WithoutBoundRobot_AllOperationsRefuseAndReport()` |  |
| метод | `SolvePlaneOffset_CollinearOrNearlyCollinearPoints_ReturnFalse` | `public void SolvePlaneOffset_CollinearOrNearlyCollinearPoints_ReturnFalse()` |  |
| метод | `SolvePlaneOffset_DuplicatePointIsToleratedAndHeightStaysCorrect` | `public void SolvePlaneOffset_DuplicatePointIsToleratedAndHeightStaysCorrect()` |  |
| метод | `SolvePlaneOffset_ExtremeScales_StayFiniteAndAccurate` | `public void SolvePlaneOffset_ExtremeScales_StayFiniteAndAccurate()` |  |
| метод | `SolvePlaneOffset_IdealSyntheticData_RecoversHeightNormalAndZeroResidual` | `public void SolvePlaneOffset_IdealSyntheticData_RecoversHeightNormalAndZeroResidual()` |  |
| метод | `SolvePlaneOffset_InvalidOrDegenerateInputSizes_ReturnFalse` | `public void SolvePlaneOffset_InvalidOrDegenerateInputSizes_ReturnFalse()` |  |
| метод | `SolvePlaneOffset_NoisyData_StaysAccurate` | `public void SolvePlaneOffset_NoisyData_StaysAccurate()` |  |
| метод | `SolvePlaneOffset_PermutationAndRepeatedCalls_AreStable` | `public void SolvePlaneOffset_PermutationAndRepeatedCalls_AreStable()` |  |
| метод | `SolvePlaneOffset_SelfTestPlaneSyntheticConstruction_ResidualIsAroundTwoHeights` | `public void SolvePlaneOffset_SelfTestPlaneSyntheticConstruction_ResidualIsAroundTwoHeights()` |  |
| метод | `SolvePlaneOffset_ToolAxisEdgeCases_AreHandled` | `public void SolvePlaneOffset_ToolAxisEdgeCases_AreHandled()` |  |
| метод | `SolvePlaneOffset_WrongReferenceHeight_ShiftsHeightExactlyByError` | `public void SolvePlaneOffset_WrongReferenceHeight_ShiftsHeightExactlyByError()` |  |
| метод | `SolveToolOffset_DegenerateRotationsOrPositions_BehaveAsDocumented` | `public void SolveToolOffset_DegenerateRotationsOrPositions_BehaveAsDocumented()` |  |
| метод | `SolveToolOffset_ExtremeCoordinateScales_StayFiniteAndBounded` | `public void SolveToolOffset_ExtremeCoordinateScales_StayFiniteAndBounded()` |  |
| метод | `SolveToolOffset_ExtremeRotationAngles_AreSolvedCorrectly` | `public void SolveToolOffset_ExtremeRotationAngles_AreSolvedCorrectly()` |  |
| метод | `SolveToolOffset_FarTouchPoint_RecoversOffsetLikeSelfTest` | `public void SolveToolOffset_FarTouchPoint_RecoversOffsetLikeSelfTest()` |  |
| метод | `SolveToolOffset_IdealData_ResidualStaysNearZeroForFourSixAndEightPoses` | `public void SolveToolOffset_IdealData_ResidualStaysNearZeroForFourSixAndEightPoses()` |  |
| метод | `SolveToolOffset_IdealSyntheticData_RecoversAllThreeComponents` | `public void SolveToolOffset_IdealSyntheticData_RecoversAllThreeComponents()` |  |
| метод | `SolveToolOffset_IdealSyntheticData_RecoversOffsetMagnitude` | `public void SolveToolOffset_IdealSyntheticData_RecoversOffsetMagnitude()` |  |
| метод | `SolveToolOffset_InvalidInputShapes_ReturnFalseWithZeroOutputs` | `public void SolveToolOffset_InvalidInputShapes_ReturnFalseWithZeroOutputs()` |  |
| метод | `SolveToolOffset_NoisyData_ErrorGrowsButOffsetStaysCloseToTruth` | `public void SolveToolOffset_NoisyData_ErrorGrowsButOffsetStaysCloseToTruth()` |  |
| метод | `SolveToolOffset_NoisyData_ResidualTracksNoiseLevel` | `public void SolveToolOffset_NoisyData_ResidualTracksNoiseLevel()` |  |
| метод | `SolveToolOffset_PointOrderInvariance_HoldsForIdealAndStaysSmallForNoisy` | `public void SolveToolOffset_PointOrderInvariance_HoldsForIdealAndStaysSmallForNoisy()` |  |
| метод | `SolveToolOffset_RepeatedCalls_ProduceIdenticalResults` | `public void SolveToolOffset_RepeatedCalls_ProduceIdenticalResults()` |  |
| метод | `SolveToolOffset_TwoDistinctRotationsOnly_ResidualIsZeroButOffsetIsWrong` | `public void SolveToolOffset_TwoDistinctRotationsOnly_ResidualIsZeroButOffsetIsWrong()` |  |
| метод | `StaticSelfTests_ReportSuccessWithoutFailureMarkers` | `public void StaticSelfTests_ReportSuccessWithoutFailureMarkers()` |  |

### `class KvEnergyOptimalTests`

- **Файл:** `Assets\_Project\08_Tests\Editor\KvEnergyOptimalTests.cs` (строка 75)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `ApplyAndResetSelected_WithoutBoundFlow_ReturnFalse` | `public void ApplyAndResetSelected_WithoutBoundFlow_ReturnFalse()` |  |
| метод | `Bind_WithNullController_KeepsFlowNull` | `public void Bind_WithNullController_KeepsFlowNull()` |  |
| метод | `Compute_WithoutBoundFlow_ReportsReasonThroughMessageEvent` | `public void Compute_WithoutBoundFlow_ReportsReasonThroughMessageEvent()` |  |
| метод | `Compute_WithoutScene_ReturnsNullAndCachesNothing` | `public void Compute_WithoutScene_ReturnsNullAndCachesNothing()` |  |
| метод | `CurrentEnergy_WithoutBoundFlow_ReturnsZero` | `public void CurrentEnergy_WithoutBoundFlow_ReturnsZero()` |  |
| метод | `Draft_DefaultsAreEmptyAndFieldsRoundTrip` | `public void Draft_DefaultsAreEmptyAndFieldsRoundTrip()` |  |
| метод | `EcoProfileFamily_SofterProfileIsNotMoreExpensive` | `public void EcoProfileFamily_SofterProfileIsNotMoreExpensive()` |  |
| метод | `Energy_ExtremeMassAndLongPath_StayFiniteAndGrow` | `public void Energy_ExtremeMassAndLongPath_StayFiniteAndGrow()` |  |
| метод | `Energy_GravityAssistedDownwardMotion_IsCheaperThanUpward` | `public void Energy_GravityAssistedDownwardMotion_IsCheaperThanUpward()` |  |
| метод | `Energy_GrowsWithPayloadMass_LinearlyInHoldingJoint` | `public void Energy_GrowsWithPayloadMass_LinearlyInHoldingJoint()` |  |
| метод | `Energy_GrowsWithTravelDistance` | `public void Energy_GrowsWithTravelDistance()` |  |
| метод | `Energy_Guards_ReturnZeroWithoutReadyValidatorOrUsablePlan` | `public void Energy_Guards_ReturnZeroWithoutReadyValidatorOrUsablePlan()` |  |
| метод | `Energy_IsAdditiveOverJoints` | `public void Energy_IsAdditiveOverJoints()` |  |
| метод | `Energy_LinearMotionWithFriction_MatchesAnalyticValue` | `public void Energy_LinearMotionWithFriction_MatchesAnalyticValue()` |  |
| метод | `Energy_NegativeMotion_IsNonNegative_AndRegenerationIsNotSubtracted` | `public void Energy_NegativeMotion_IsNonNegative_AndRegenerationIsNotSubtracted()` |  |
| метод | `Energy_NullModel_FallsBackToDefaults` | `public void Energy_NullModel_FallsBackToDefaults()` |  |
| метод | `Energy_PeakPower_IsNotBelowAveragePower` | `public void Energy_PeakPower_IsNotBelowAveragePower()` |  |
| метод | `Energy_ScalesInverselyWithTime_ForPureFriction` | `public void Energy_ScalesInverselyWithTime_ForPureFriction()` |  |
| метод | `Energy_ScalesQuadraticallyWithAcceleration_ForPureInertia` | `public void Energy_ScalesQuadraticallyWithAcceleration_ForPureInertia()` |  |
| метод | `Energy_VerySmallAndVeryFastMotion_StayFinite` | `public void Energy_VerySmallAndVeryFastMotion_StayFinite()` |  |
| метод | `Energy_ZeroInputs_ProduceZeroWithoutNaN` | `public void Energy_ZeroInputs_ProduceZeroWithoutNaN()` |  |
| метод | `Energy_ZeroTimes_AreRebuiltWithoutDivisionByZero` | `public void Energy_ZeroTimes_AreRebuiltWithoutDivisionByZero()` |  |
| метод | `EnergyModel_CloneIsDeepCopy` | `public void EnergyModel_CloneIsDeepCopy()` |  |
| метод | `EnergyModel_DefaultsAndDistalMass_FollowDocumentedShares` | `public void EnergyModel_DefaultsAndDistalMass_FollowDocumentedShares()` |  |
| метод | `EnergyModel_Distal_IgnoresNegativePayloadAndFallsBackOutOfRange` | `public void EnergyModel_Distal_IgnoresNegativePayloadAndFallsBackOutOfRange()` |  |
| метод | `Invalidate_And_ResetCache_AreIdempotentOnEmptyService` | `public void Invalidate_And_ResetCache_AreIdempotentOnEmptyService()` |  |
| метод | `MetricLine_IsDashForEmptyCandidate_AndZeroWithoutFlow` | `public void MetricLine_IsDashForEmptyCandidate_AndZeroWithoutFlow()` |  |
| метод | `Model_ReturnsSameInstanceAndKeepsPhysicalParameters` | `public void Model_ReturnsSameInstanceAndKeepsPhysicalParameters()` |  |
| метод | `MotionLimits_DefaultsAndCloneAreIndependent` | `public void MotionLimits_DefaultsAndCloneAreIndependent()` |  |
| метод | `NewService_HasNoDraftsAndNoFlow` | `public void NewService_HasNoDraftsAndNoFlow()` |  |
| метод | `PayloadKg_IsClampedToDocumentedRange` | `public void PayloadKg_IsClampedToDocumentedRange()` |  |
| метод | `PayloadKg_IsPersistedUnderDocumentedPrefsKey` | `public void PayloadKg_IsPersistedUnderDocumentedPrefsKey()` |  |
| метод | `PayloadKg_WithNaN_DoesNotThrowAndStaysBounded` | `public void PayloadKg_WithNaN_DoesNotThrowAndStaysBounded()` |  |
| метод | `Retime_NullLimitsUseDefaults_AndNotReadyValidatorGivesNull` | `public void Retime_NullLimitsUseDefaults_AndNotReadyValidatorGivesNull()` |  |
| метод | `Retime_SofterScales_ProduceLongerTime` | `public void Retime_SofterScales_ProduceLongerTime()` |  |
| метод | `SetUp` | `public void SetUp()` |  |
| метод | `TearDown` | `public void TearDown()` |  |

### `class KvPayloadCalculatorTests`

- **Файл:** `Assets\_Project\08_Tests\Editor\KvPayloadCalculatorTests.cs` (строка 116)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Api_IsNotTestableWithoutRefactoring` | `public void Api_IsNotTestableWithoutRefactoring()` |  |
| метод | `Calculator_CurveSamples_DefaultsToFourteenAndSetterIsIdempotent` | `public void Calculator_CurveSamples_DefaultsToFourteenAndSetterIsIdempotent()` |  |
| метод | `Calculator_CurveSamples_IsClampedToFourForty` | `public void Calculator_CurveSamples_IsClampedToFourForty()` |  |
| метод | `Calculator_NewInstance_ExposesEmptyResultAndNoBoundRobot` | `public void Calculator_NewInstance_ExposesEmptyResultAndNoBoundRobot()` |  |
| метод | `Calculator_PrefsKeys_AreStable` | `public void Calculator_PrefsKeys_AreStable()` |  |
| метод | `CopyFrom_ClonesArrays_SoSourceAndTargetStayIndependent` | `public void CopyFrom_ClonesArrays_SoSourceAndTargetStayIndependent()` |  |
| метод | `CopyFrom_CopiesSafetyPrismaticRatingAndToolMass` | `public void CopyFrom_CopiesSafetyPrismaticRatingAndToolMass()` |  |
| метод | `CopyFrom_Null_IsNoOp` | `public void CopyFrom_Null_IsNoOp()` |  |
| метод | `CopyFrom_SourceWithNullArray_ThrowsNullReference_DocumentedBehaviour` | `public void CopyFrom_SourceWithNullArray_ThrowsNullReference_DocumentedBehaviour()` |  |
| метод | `Evaluate_ExtremeAndNaNInputs_ProduceNoNaNOrInfinityInResult` | `public void Evaluate_ExtremeAndNaNInputs_ProduceNoNaNOrInfinityInResult()` |  |
| метод | `Evaluate_InvalidResult_LeavesJointAndCurveArraysNull` | `public void Evaluate_InvalidResult_LeavesJointAndCurveArraysNull()` |  |
| метод | `Evaluate_NullPose_ReportsRobotNotDefined` | `public void Evaluate_NullPose_ReportsRobotNotDefined()` |  |
| метод | `Evaluate_PublishesTheResultIntoLast` | `public void Evaluate_PublishesTheResultIntoLast()` |  |
| метод | `Evaluate_RepeatedAndInterleavedCalls_AreDeterministic` | `public void Evaluate_RepeatedAndInterleavedCalls_AreDeterministic()` |  |
| метод | `Evaluate_WithoutBoundRobot_RejectsEveryPose` | `public void Evaluate_WithoutBoundRobot_RejectsEveryPose()` |  |
| метод | `JointTorques_OutputParameters_AreNeverNullOnFailure` | `public void JointTorques_OutputParameters_AreNeverNullOnFailure()` |  |
| метод | `JointTorques_RejectsAnyPayloadAndPose` | `public void JointTorques_RejectsAnyPayloadAndPose()` |  |
| метод | `JointTorques_WithoutBoundRobot_ReturnsFalseWithEmptyOutputs` | `public void JointTorques_WithoutBoundRobot_ReturnsFalseWithEmptyOutputs()` |  |
| метод | `Message_Event_IsSilentOnTheGuardedPaths` | `public void Message_Event_IsSilentOnTheGuardedPaths()` |  |
| метод | `Model_DefaultData_YieldsAvailableTorqueAsRatingOverSafety` | `public void Model_DefaultData_YieldsAvailableTorqueAsRatingOverSafety()` |  |
| метод | `Model_DefaultLinkMass_HasSixExpectedMasses` | `public void Model_DefaultLinkMass_HasSixExpectedMasses()` |  |
| метод | `Model_DefaultRating_HasSixAxesWithExpectedTorques` | `public void Model_DefaultRating_HasSixAxesWithExpectedTorques()` |  |
| метод | `Model_DefaultRatings_FitWorkbenchSliderRange` | `public void Model_DefaultRatings_FitWorkbenchSliderRange()` |  |
| метод | `Model_DefaultScalars_MatchDocumentedDefaults` | `public void Model_DefaultScalars_MatchDocumentedDefaults()` |  |
| метод | `Result_Defaults_DescribeAnEmptyUnavailableEstimate` | `public void Result_Defaults_DescribeAnEmptyUnavailableEstimate()` |  |
| метод | `Result_Line_WhenInvalid_ReturnsWhyVerbatim` | `public void Result_Line_WhenInvalid_ReturnsWhyVerbatim()` |  |
| метод | `Result_Line_WhenValid_ConvertsJointIndexToHumanNumber` | `public void Result_Line_WhenValid_ConvertsJointIndexToHumanNumber()` |  |
| метод | `Result_Line_WhenValid_FormatsAllNumbersIntoSingleLine` | `public void Result_Line_WhenValid_FormatsAllNumbersIntoSingleLine()` |  |
| метод | `StatusLine_AfterFailedEvaluation_StillShowsDashPlaceholder` | `public void StatusLine_AfterFailedEvaluation_StillShowsDashPlaceholder()` |  |
| метод | `StatusLine_WithoutValidResult_ShowsDashPlaceholder` | `public void StatusLine_WithoutValidResult_ShowsDashPlaceholder()` |  |
| метод | `Tick_FirstCallEvaluates_ThenHonoursFourTenthsInterval` | `public void Tick_FirstCallEvaluates_ThenHonoursFourTenthsInterval()` |  |

### `class KvRobotExportTests`

- **Файл:** `Assets\_Project\08_Tests\Editor\KvRobotExportTests.cs` (строка 73)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `AllLanguages_CodeLinesAreAsciiOnly` | `public void AllLanguages_CodeLinesAreAsciiOnly()` |  |
| метод | `AllLanguages_ContainNoNaNOrInfinityForNormalData` | `public void AllLanguages_ContainNoNaNOrInfinityForNormalData()` |  |
| метод | `AllLanguages_ProduceDistinctNonEmptyProgramsWithRootKeywords` | `public void AllLanguages_ProduceDistinctNonEmptyProgramsWithRootKeywords()` |  |
| метод | `AllLanguages_UseInvariantDecimalPoint` | `public void AllLanguages_UseInvariantDecimalPoint()` |  |
| метод | `Cartesian_FormatsMillimetresAndEulerAngles` | `public void Cartesian_FormatsMillimetresAndEulerAngles()` |  |
| метод | `CartesianRapid_FormatsPositionAndQuaternion` | `public void CartesianRapid_FormatsPositionAndQuaternion()` |  |
| метод | `ExtremeCoordinates_ProduceFiniteTextWithoutNaNOrExponent` | `public void ExtremeCoordinates_ProduceFiniteTextWithoutNaNOrExponent()` |  |
| метод | `Joints_FormatsKrlAxisPairs` | `public void Joints_FormatsKrlAxisPairs()` |  |
| метод | `JointsKarel_LimitsToListOfNineAxes` | `public void JointsKarel_LimitsToListOfNineAxes()` |  |
| метод | `JointsRapid_AlwaysEmitsSixAxes_PaddingWithZero` | `public void JointsRapid_AlwaysEmitsSixAxes_PaddingWithZero()` |  |
| метод | `Karel_Footer_EndsWithEndTrajectory` | `public void Karel_Footer_EndsWithEndTrajectory()` |  |
| метод | `Karel_Header_HasProgramVarBeginAndDeclarations` | `public void Karel_Header_HasProgramVarBeginAndDeclarations()` |  |
| метод | `Karel_MotionCounters_MatchFormulas_ForTwoThreeFivePoints` | `public void Karel_MotionCounters_MatchFormulas_ForTwoThreeFivePoints()` |  |
| метод | `Karel_OnePoint_HasNoJumpLabelsAndRemainsValid` | `public void Karel_OnePoint_HasNoJumpLabelsAndRemainsValid()` |  |
| метод | `Karel_ProgramAndEnd_AreBalanced` | `public void Karel_ProgramAndEnd_AreBalanced()` |  |
| метод | `Karel_RepeatedGeneration_IsIdenticalAfterTimestampRemoval` | `public void Karel_RepeatedGeneration_IsIdenticalAfterTimestampRemoval()` |  |
| метод | `Karel_Text_UsesOnlyLineFeedAndHasNoControlCharacters` | `public void Karel_Text_UsesOnlyLineFeedAndHasNoControlCharacters()` |  |
| метод | `Karel_ZeroPoints_ThrowsArgumentOutOfRange` | `public void Karel_ZeroPoints_ThrowsArgumentOutOfRange()` |  |
| метод | `Krl_DefAndEnd_AreBalanced` | `public void Krl_DefAndEnd_AreBalanced()` |  |
| метод | `Krl_FinalLinearMove_HasNoCDis` | `public void Krl_FinalLinearMove_HasNoCDis()` |  |
| метод | `Krl_Footer_EndsWithSingleEnd` | `public void Krl_Footer_EndsWithSingleEnd()` |  |
| метод | `Krl_Header_HasExpectedPreambleAndDeclarations` | `public void Krl_Header_HasExpectedPreambleAndDeclarations()` |  |
| метод | `Krl_HeaderComment_NumbersFollowCurrentCulture` | `public void Krl_HeaderComment_NumbersFollowCurrentCulture()` |  |
| метод | `Krl_LastLinesFormula_CountsOneExtraLine` | `public void Krl_LastLinesFormula_CountsOneExtraLine()` |  |
| метод | `Krl_MotionCounters_MatchFormulas_ForTwoThreeFivePoints` | `public void Krl_MotionCounters_MatchFormulas_ForTwoThreeFivePoints()` |  |
| метод | `Krl_OnePoint_GeneratesProgramWithoutException` | `public void Krl_OnePoint_GeneratesProgramWithoutException()` |  |
| метод | `Krl_RepeatedGeneration_IsIdenticalAfterTimestampRemoval` | `public void Krl_RepeatedGeneration_IsIdenticalAfterTimestampRemoval()` |  |
| метод | `Krl_Text_UsesOnlyLineFeedAndHasNoControlCharacters` | `public void Krl_Text_UsesOnlyLineFeedAndHasNoControlCharacters()` |  |
| метод | `Krl_TimestampLine_HasExpectedFormat` | `public void Krl_TimestampLine_HasExpectedFormat()` |  |
| метод | `Krl_ZeroPoints_ThrowsArgumentOutOfRange` | `public void Krl_ZeroPoints_ThrowsArgumentOutOfRange()` |  |
| метод | `Mm_ConvertsMetresToMillimetres_AndDeg_KeepsInvariantFormat` | `public void Mm_ConvertsMetresToMillimetres_AndDeg_KeepsInvariantFormat()` |  |
| метод | `Rapid_Declarations_CountMatchesPointCount` | `public void Rapid_Declarations_CountMatchesPointCount()` |  |
| метод | `Rapid_HardcodedSpeedsZonesAndTool_ArePresent` | `public void Rapid_HardcodedSpeedsZonesAndTool_ArePresent()` |  |
| метод | `Rapid_MotionCounters_MatchFormulas_ForTwoThreeFivePoints` | `public void Rapid_MotionCounters_MatchFormulas_ForTwoThreeFivePoints()` |  |
| метод | `Rapid_NineE9Sentinels_AreIntentionalDesign` | `public void Rapid_NineE9Sentinels_AreIntentionalDesign()` |  |
| метод | `Rapid_OnePoint_GeneratesProgramWithoutException` | `public void Rapid_OnePoint_GeneratesProgramWithoutException()` |  |
| метод | `Rapid_RepeatedGeneration_IsIdenticalAfterTimestampRemoval` | `public void Rapid_RepeatedGeneration_IsIdenticalAfterTimestampRemoval()` |  |
| метод | `Rapid_Structure_HasModuleAndTwoProcedures_WithBalance` | `public void Rapid_Structure_HasModuleAndTwoProcedures_WithBalance()` |  |
| метод | `Rapid_Text_UsesOnlyLineFeedAndHasNoControlCharacters` | `public void Rapid_Text_UsesOnlyLineFeedAndHasNoControlCharacters()` |  |
| метод | `Rapid_ZeroPoints_ThrowsArgumentOutOfRange` | `public void Rapid_ZeroPoints_ThrowsArgumentOutOfRange()` |  |
| метод | `ResolveInternalApi` | `public void ResolveInternalApi()` |  |

### `class KvTimeOptimalTests`

- **Файл:** `Assets\_Project\08_Tests\Editor\KvTimeOptimalTests.cs` (строка 62)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `AccelPeak` | `public double AccelPeak; // ВЫХОД: максимум внутреннего профиля ускорений` |  |
| поле | `AccLimitDeg` | `public double[] AccLimitDeg; // °/с²` |  |
| поле | `Amax` | `public double[] Amax; // касательный предел ускорения (единицы/с²)` |  |
| поле | `Clamped` | `public int Clamped; // ВЫХОД: сколько сэмплов срезано по пределу скорости` |  |
| поле | `Dof` | `public int Dof;` |  |
| поле | `JerkCap` | `public double[] JerkCap; // предел рывка вдоль пути (единицы/с³)` |  |
| поле | `JerkLimitDeg` | `public double[] JerkLimitDeg; // °/с³` |  |
| поле | `JerkPeak` | `public double JerkPeak; // ВЫХОД: расчётный рывок профиля` |  |
| поле | `JerkRef` | `public double JerkRef; // эталонный предел рывка (единицы/с³)` |  |
| поле | `N` | `public int N;` |  |
| поле | `Plan` | `public PlannedTrajectory Plan;` |  |
| поле | `S` | `public double[] S; // длина пути вдоль сэмплов (единицы пути)` |  |
| поле | `Scale` | `public double[] Scale; // нормировка суставов (1/90 для вращательных)` |  |
| поле | `Times` | `public float[] Times; // ВЫХОД: времена сэмплов` |  |
| поле | `Vel` | `public double[] Vel; // ВЫХОД: скорости вдоль пути` |  |
| поле | `VelLimitDeg` | `public double[] VelLimitDeg; // физические лимиты суставов, °/с` |  |
| поле | `Vmax` | `public double[] Vmax; // предел скорости вдоль пути (единицы/с), = velCapRaw` |  |
| метод | `AnalyzeAndVerifyJerk_WithoutReadyValidator_ReturnNeutralDefaults` | `public void AnalyzeAndVerifyJerk_WithoutReadyValidator_ReturnNeutralDefaults()` |  |
| метод | `Clone_DeepCopiesPathAndTimes` | `public void Clone_DeepCopiesPathAndTimes()` |  |
| метод | `Compute_GuardBranchesReturnNullAndReportReasonOnlyWhenNotQuiet` | `public void Compute_GuardBranchesReturnNullAndReportReasonOnlyWhenNotQuiet()` |  |
| метод | `Constructor_StartsUnboundAndCacheOperationsAreSafe` | `public void Constructor_StartsUnboundAndCacheOperationsAreSafe()` |  |
| метод | `Draft_DefaultsAreJerkSafe` | `public void Draft_DefaultsAreJerkSafe()` |  |
| метод | `EnsureTimes_InvalidPlansReturnFalseAndValidOnesAreFilledOrKept` | `public void EnsureTimes_InvalidPlansReturnFalseAndValidOnesAreFilledOrKept()` |  |
| метод | `GeometryAndEnergyHelpers_WithoutReadyValidator_ReturnEmptyValues` | `public void GeometryAndEnergyHelpers_WithoutReadyValidator_ReturnEmptyValues()` |  |
| метод | `Limits_ConversionLiveInstanceAndPrefsKeyContract` | `public void Limits_ConversionLiveInstanceAndPrefsKeyContract()` |  |
| метод | `MeasureJerk_GuardsReturnZeroForUnusableInput` | `public void MeasureJerk_GuardsReturnZeroForUnusableInput()` |  |
| метод | `MeasureJerk_LinearPathIsNearZeroAndScalesWithJointNormalisation` | `public void MeasureJerk_LinearPathIsNearZeroAndScalesWithJointNormalisation()` |  |
| метод | `MotionLimits_DefaultsAndCloneAreIndependent` | `public void MotionLimits_DefaultsAndCloneAreIndependent()` |  |
| метод | `ResetSelected_ReturnsFalse_WithoutFlow` | `public void ResetSelected_ReturnsFalse_WithoutFlow()` |  |
| метод | `Retime_RealEntryPoint_BuildsSProfileAndRetimesPlan` | `public void Retime_RealEntryPoint_BuildsSProfileAndRetimesPlan()` |  |
| метод | `Retime_WithoutReadyValidator_ReturnsNull` | `public void Retime_WithoutReadyValidator_ReturnsNull()` |  |
| метод | `Retime_ZeroLengthPath_ReturnsPlanWithoutRetiming` | `public void Retime_ZeroLengthPath_ReturnsPlanWithoutRetiming()` |  |
| метод | `SetLimits_ClampsExtremeLimits` | `public void SetLimits_ClampsExtremeLimits()` |  |
| метод | `SetLimits_ClampsZeroAndNegativeLimits` | `public void SetLimits_ClampsZeroAndNegativeLimits()` |  |
| метод | `SetLimits_InvalidatesCachedDrafts` | `public void SetLimits_InvalidatesCachedDrafts()` |  |
| метод | `SProfile_ExtremeLimitsRespectLimitsAndStayFinite` | `public void SProfile_ExtremeLimitsRespectLimitsAndStayFinite()` |  |
| метод | `SProfile_HigherLimitsTakeLessTime` | `public void SProfile_HigherLimitsTakeLessTime()` |  |
| метод | `SProfile_InteriorVelocityNeverExceedsPathLimit` | `public void SProfile_InteriorVelocityNeverExceedsPathLimit()` |  |
| метод | `SProfile_LongerPathTakesLongerTime` | `public void SProfile_LongerPathTakesLongerTime()` |  |
| метод | `SProfile_LongMoveUsesMostOfSpeedLimit` | `public void SProfile_LongMoveUsesMostOfSpeedLimit()` |  |
| метод | `SProfile_MeasuredJerkNeverExceedsMaxJerk` | `public void SProfile_MeasuredJerkNeverExceedsMaxJerk()` |  |
| метод | `SProfile_MeasuredJointAccelerationNeverExceedsMaxAcceleration` | `public void SProfile_MeasuredJointAccelerationNeverExceedsMaxAcceleration()` |  |
| метод | `SProfile_MeasuredJointSpeedNeverExceedsMaxSpeed` | `public void SProfile_MeasuredJointSpeedNeverExceedsMaxSpeed()` |  |
| метод | `SProfile_MultiJointPathRespectsPerJointLimits` | `public void SProfile_MultiJointPathRespectsPerJointLimits()` |  |
| метод | `SProfile_OutputVelocitiesStartAndEndAtRest` | `public void SProfile_OutputVelocitiesStartAndEndAtRest()` |  |
| метод | `SProfile_RetimingDoesNotChangePathGeometry` | `public void SProfile_RetimingDoesNotChangePathGeometry()` |  |
| метод | `SProfile_ShortMoveKeepsPeakSpeedBelowLimit` | `public void SProfile_ShortMoveKeepsPeakSpeedBelowLimit()` |  |
| метод | `SProfile_SymmetryIsNotHeld_Characterized` | `public void SProfile_SymmetryIsNotHeld_Characterized()` |  |
| метод | `SProfile_TimesStartAtZeroAndIncreaseStrictly` | `public void SProfile_TimesStartAtZeroAndIncreaseStrictly()` |  |
| метод | `SProfile_ZeroAndNegativeLimits_StayFiniteWithoutExceptions` | `public void SProfile_ZeroAndNegativeLimits_StayFiniteWithoutExceptions()` |  |
| метод | `SProfile_ZeroLengthMoveStaysFiniteAndMinimal` | `public void SProfile_ZeroLengthMoveStaysFiniteAndMinimal()` |  |
| метод | `TrajStats_EnergyPerMeter_GuardsZeroLength` | `public void TrajStats_EnergyPerMeter_GuardsZeroLength()` |  |

### `class KvTrajMathTests`

- **Файл:** `Assets\_Project\08_Tests\Editor\KvTrajMathTests.cs` (строка 22)

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Analyze_UnavailableValidator_ReturnsDefaultStats` | `public void Analyze_UnavailableValidator_ReturnsDefaultStats()` |  |
| метод | `CopyCopyPathAndClone_HandleNullAndCopyDeeply` | `public void CopyCopyPathAndClone_HandleNullAndCopyDeeply()` |  |
| метод | `Curvature_DuplicateZeroLengthAndSubMicrometerSegments_AreSkippedWithoutNaN` | `public void Curvature_DuplicateZeroLengthAndSubMicrometerSegments_AreSkippedWithoutNaN()` |  |
| метод | `Curvature_NullOrTooShortPolyline_ReturnsZero` | `public void Curvature_NullOrTooShortPolyline_ReturnsZero()` |  |
| метод | `Curvature_RightAngleTurn_ReturnsRootTwoMaxAndHalfOfItAsAverage` | `public void Curvature_RightAngleTurn_ReturnsRootTwoMaxAndHalfOfItAsAverage()` |  |
| метод | `Curvature_SmallerCircleRadius_ReturnsProportionallyLargerCurvature` | `public void Curvature_SmallerCircleRadius_ReturnsProportionallyLargerCurvature()` |  |
| метод | `Curvature_StraightPolyline_ReturnsZeroMaxAndAverage` | `public void Curvature_StraightPolyline_ReturnsZeroMaxAndAverage()` |  |
| метод | `Curvature_ThreePointsOnCircleRadiusTwo_ReturnsHalf` | `public void Curvature_ThreePointsOnCircleRadiusTwo_ReturnsHalf()` |  |
| метод | `Energy_UnavailableValidator_ReturnsZeroEnergyAndPeak` | `public void Energy_UnavailableValidator_ReturnsZeroEnergyAndPeak()` |  |
| метод | `EnergyModel_Clone_IsIndependent` | `public void EnergyModel_Clone_IsIndependent()` |  |
| метод | `EnergyModel_Distal_AddsPayloadShareClampsNegativeAndFallsBack` | `public void EnergyModel_Distal_AddsPayloadShareClampsNegativeAndFallsBack()` |  |
| метод | `EnsureTimes_MissingOrDegenerateTimes_AreReplacedByUniformRamp` | `public void EnsureTimes_MissingOrDegenerateTimes_AreReplacedByUniformRamp()` |  |
| метод | `EnsureTimes_NullOrTooShortPlan_ReturnsFalse` | `public void EnsureTimes_NullOrTooShortPlan_ReturnsFalse()` |  |
| метод | `EnsureTimes_ValidMonotonicTimes_AreKeptUntouched` | `public void EnsureTimes_ValidMonotonicTimes_AreKeptUntouched()` |  |
| метод | `Extreme_HugeCoordinates_StayFiniteAndNegligible` | `public void Extreme_HugeCoordinates_StayFiniteAndNegligible()` |  |
| метод | `Extreme_LongPathWithMicroSteps_SmoothingKeepsEndsAndStaysFinite` | `public void Extreme_LongPathWithMicroSteps_SmoothingKeepsEndsAndStaysFinite()` |  |
| метод | `Extreme_SProfileBuild_ZeroAndHugeLimits_StayFinite` | `public void Extreme_SProfileBuild_ZeroAndHugeLimits_StayFinite()` |  |
| метод | `Extreme_TenThousandPointStraightPolyline_CurvatureIsZero` | `public void Extreme_TenThousandPointStraightPolyline_CurvatureIsZero()` |  |
| метод | `MeasureJerk_ConstantVelocity_ReturnsZero` | `public void MeasureJerk_ConstantVelocity_ReturnsZero()` |  |
| метод | `MeasureJerk_InvalidAndDegenerateInputs_AreFinite` | `public void MeasureJerk_InvalidAndDegenerateInputs_AreFinite()` |  |
| метод | `MeasureJerk_KnownSamples_ReturnsExactValueAndAppliesScale` | `public void MeasureJerk_KnownSamples_ReturnsExactValueAndAppliesScale()` |  |
| метод | `MotionLimits_DefaultsAndClone_AreAsSpecified` | `public void MotionLimits_DefaultsAndClone_AreAsSpecified()` |  |
| метод | `PathLength_UnavailableValidatorOrPath_ReturnsZero` | `public void PathLength_UnavailableValidatorOrPath_ReturnsZero()` |  |
| метод | `RetimeAndVerifyJerk_UnavailableValidator_ReturnSafeDefaults` | `public void RetimeAndVerifyJerk_UnavailableValidator_ReturnSafeDefaults()` |  |
| метод | `Smooth_AllMethods_PreserveEndsAndProduceFiniteValues` | `public void Smooth_AllMethods_PreserveEndsAndProduceFiniteValues()` |  |
| метод | `Smooth_BSpline_KeepsMonotonicRampMonotonic` | `public void Smooth_BSpline_KeepsMonotonicRampMonotonic()` |  |
| метод | `Smooth_BSplineAndGauss_KeepValuesWithinInputRangeAndDof` | `public void Smooth_BSplineAndGauss_KeepValuesWithinInputRangeAndDof()` |  |
| метод | `Smooth_BSplineAndGauss_ReduceRoughnessOfZigzag` | `public void Smooth_BSplineAndGauss_ReduceRoughnessOfZigzag()` |  |
| метод | `Smooth_ConstantPath_StaysConstantForAllMethods` | `public void Smooth_ConstantPath_StaysConstantForAllMethods()` |  |
| метод | `Smooth_LessThanFourPoints_ReturnsUnchangedCopy` | `public void Smooth_LessThanFourPoints_ReturnsUnchangedCopy()` |  |
| метод | `Smooth_LevelIsClampedToUnitRange` | `public void Smooth_LevelIsClampedToUnitRange()` |  |
| метод | `Smooth_NullPath_ReturnsNull` | `public void Smooth_NullPath_ReturnsNull()` |  |
| метод | `Smooth_OutputIsNotAliasedToInput` | `public void Smooth_OutputIsNotAliasedToInput()` |  |
| метод | `SProfileBuild_DegenerateInputs_ReturnZeroMetrics` | `public void SProfileBuild_DegenerateInputs_ReturnZeroMetrics()` |  |
| метод | `SProfileBuild_HigherVelocityCap_TakesLessTime` | `public void SProfileBuild_HigherVelocityCap_TakesLessTime()` |  |
| метод | `SProfileBuild_LongerPath_TakesMoreTime` | `public void SProfileBuild_LongerPath_TakesMoreTime()` |  |
| метод | `SProfileBuild_LongPath_ProducesFiniteMonotonicProfile` | `public void SProfileBuild_LongPath_ProducesFiniteMonotonicProfile()` |  |
| метод | `SProfileBuild_SameInputTwice_ProducesIdenticalProfile` | `public void SProfileBuild_SameInputTwice_ProducesIdenticalProfile()` |  |
| метод | `SProfileBuild_StraightPath_ProducesIncreasingTimesFromZeroAndRestAtEnds` | `public void SProfileBuild_StraightPath_ProducesIncreasingTimesFromZeroAndRestAtEnds()` |  |
| метод | `SProfileBuild_StraightPath_RespectsVelocityCapWithoutClamping` | `public void SProfileBuild_StraightPath_RespectsVelocityCapWithoutClamping()` |  |
| метод | `TcpPolylineAndLeverArms_UnavailableValidator_ReturnEmptyArrays` | `public void TcpPolylineAndLeverArms_UnavailableValidator_ReturnEmptyArrays()` |  |
| метод | `TrajStats_DefaultInstance_IsInvalidAndZeroed` | `public void TrajStats_DefaultInstance_IsInvalidAndZeroed()` |  |
| метод | `TrajStats_EnergyPerMeter_ComputesRatioAndGuardsZeroLength` | `public void TrajStats_EnergyPerMeter_ComputesRatioAndGuardsZeroLength()` |  |
| метод | `TrajStats_Line_ContainsAllMetricUnitsAndNoNaN` | `public void TrajStats_Line_ContainsAllMetricUnitsAndNoNaN()` |  |

---


---

## Дополнение 08.10.2026: XR-слой и конвейер рендера (§28–§31)

_Этот раздел добавлен к справочнику, потому что снимок от 15.09.2026 не содержит
ни одного типа XR-слоя (§28) и ни одного инструмента конвейера рендера (§29–§31).
Собран из исходников скриптом `Tools\api-reference-add-xr.ps1`, формат — как у остальных
разделов: файл, назначение, таблица публичных членов._

## Пространство имён `KazistovVvFeatures`

### `enum KvPipelineKind`

- **Файл:** `Assets\_Project\01_Scripts\Features\KvRenderPipelineShaders.cs` (строка 8)
- **Назначение:** —

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| значение | `BuiltIn` | `BuiltIn = 0` |  |
| значение | `Urp` | `Urp = 1` |  |
| значение | `Hdrp` | `Hdrp = 2` |  |
| значение | `Unknown` | `Unknown = 3` |  |

## Пространство имён `KazistovVvXr`

### `class KvXrCameraGuard`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrCameraGuard.cs` (строка 24)
- **Назначение:** СТОРОЖ XR-КАМЕРЫ (сессия §28, этап 1–3 плана XR). ЗАЧЕМ ОТДЕЛЬНЫЙ КОМПОНЕНТ. Проект написан для компьютера, и в нём есть сервисы, которые считают, что камерой управляет МЫШЬ. В шлеме камерой управляет трекинг головы, и любой такой сервис становится дефектом. Самый опасный из них — `IdleCameraBrain` («режим простоя, как в Skyrim»): через десять минут бездействия он НАЧИНАЕТ ОБЛЕТАТЬ РОБОТА и переписывает `transform` камеры. В VR это не «красиво», а мгновенная дурнота у оператора, потому что картинка едет сама. ЧТО ДЕЛАЕТ СТОРОЖ: 1) убирает облёт простоя с камеры шлема (в VR он недопустим в принципе); 2) снимает захват системного курсора — в шлеме его нет, а захваченный курсор на компьютере ме…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `freeSystemCursor` | `public bool freeSystemCursor = true;` |  |
| поле | `disableIdleOrbit` | `public bool disableIdleOrbit = true;` |  |
| свойство | `ActionsTaken` | `public string ActionsTaken { get; private set; } = "ничего (всё уже было выключено)";` |  |

### `class KvXrInputSource`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrInputSource.cs` (строка 28)
- **Назначение:** СОСТОЯНИЕ КОНТРОЛЛЕРОВ И ГОЛОВЫ (сессия §28, этап 2 плана XR). ЗАЧЕМ. Позы, курок и стик надо читать из ОДНОГО места: и ввод для робота (<c>VRInputProvider</c>), и перемещение (<c>KvXrLocomotion</c>), и луч по интерфейсу (<c>KvXrUiPointer</c>) должны видеть одну и ту же картину кадра. Иначе получается классический дефект VR: «курок нажал два действия» — ровно та болезнь, которую в §27 лечили на десктопе арбитражем клавиш. ПОЧЕМУ НЕ XRI. Проект обходится без компонентов XR Interaction Toolkit: они требуют настроенного префаба рига, а собрать и проверить его без шлема нечем. Здесь читается сам <see cref="InputSystem"/> — он уже подключён (пакет 1.20.0, только new Input System), и устройство PI…

### `class KvXrLocomotion`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrLocomotion.cs` (строка 28)
- **Назначение:** ПЕРЕМЕЩЕНИЕ ОПЕРАТОРА В ВИРТУАЛЬНОМ ПРОСТРАНСТВЕ (решение владельца [D2], этап 2 плана XR). ДВА СПОСОБА, ОБА — ПО РЕШЕНИЮ ВЛАДЕЛЬЦА: • ТЕЛЕПОРТ (по умолчанию) — прицел из контроллера, подтверждение курком, мгновенный перенос на выбранное место. Не укачивает: это главная причина, почему телепорт выбран основным для человека, который впервые надел шлем. • ПЛАВНЫЙ ХОД (включается в настройках) — непрерывное движение по стику. ЧТО ЗДЕСЬ ГЛАВНЫЙ РИСК. Плавный ход в VR — источник «укачивания»: движения не совпадают с тем, что чувствует вестибулярный аппарат. Поэтому скорость жёстко ограничена (0,5…3 м/с), а режим по умолчанию — телепорт. Это не перестраховка, а прямое следствие решения [D2]. ПОВОР…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `snapTurnDegrees` | `public float snapTurnDegrees = 30f;` |  |
| поле | `snapTurnThreshold` | `public float snapTurnThreshold = 0.7f;` |  |
| поле | `snapTurnCooldown` | `public float snapTurnCooldown = 0.25f;` |  |
| поле | `useRightStickForMove` | `public bool useRightStickForMove = false;` |  |
| поле | `triggerThreshold` | `public float triggerThreshold = 0.5f;` |  |
| поле | `groundMask` | `public LayerMask groundMask = ~0;` |  |
| поле | `maxSlopeDegrees` | `public float maxSlopeDegrees = 45f;` |  |
| поле | `validMarkerColor` | `public Color validMarkerColor = new Color(0.20f, 0.85f, 0.35f, 0.75f);` |  |
| поле | `invalidMarkerColor` | `public Color invalidMarkerColor = new Color(0.90f, 0.25f, 0.20f, 0.60f);` |  |
| свойство | `TeleportTargetValid` | `public bool TeleportTargetValid { get { return teleportValid; } }` |  |
| свойство | `TeleportPoint` | `public Vector3 TeleportPoint { get { return teleportPoint; } }` |  |
| свойство | `TeleportCount` | `public int TeleportCount { get; private set; }` |  |
| свойство | `SnapTurnCount` | `public int SnapTurnCount { get; private set; }` |  |
| свойство | `SmoothMovingSeconds` | `public float SmoothMovingSeconds { get; private set; }` |  |

### `enum KvXrMoveMode`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrSettings.cs` (строка 6)
- **Назначение:** —

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| значение | `Teleport` | `Teleport = 0` |  |
| значение | `Smooth` | `Smooth = 1` |  |

### `class KvXrMrRoom`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrMrRoom.cs` (строка 33)
- **Назначение:** MR-КОМНАТА: РЕАЛЬНЫЙ СТОЛ, РОБОТ 1:1 И ВЫКЛЮЧАЕМЫЕ СЛОИ ВИРТУАЛЬНОГО (решения владельца [D5], [D7], [D8]; этап 4 плана XR, сессия §28). ЧТО ЗДЕСЬ ПРОИСХОДИТ. В MR оператор видит СВОЮ комнату, а виртуальный робот должен стоять на ЕГО столе — 100 см поперёк, 70 см в длину, столешница на 78 см от пола (решение [D7]). Этот компонент: 1) гасит виртуальную лабораторию (столы-стенды), чтобы она не перекрывала комнату; 2) ставит робота на реальный стол в масштабе 1:1; 3) включает и выключает слои виртуального ПО ТУМБЛЕРАМ ([D8]): панель интерфейса, сетка рабочей зоны, достижимость руки, виртуальные стенды. По умолчанию включена только панель — комната оператора не зарастает слоями, которые он не про…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `headCamera` | `public Camera headCamera;` |  |
| поле | `table` | `public KvXrTableSize table = new KvXrTableSize(1.00f, 0.70f, 0.78f);` |  |
| поле | `room` | `public KvXrRoomSize room = new KvXrRoomSize(3.0f, 2.0f);` |  |
| поле | `robotOffset` | `public Vector2 robotOffset = Vector2.zero;` |  |
| поле | `robotYawDegrees` | `public float robotYawDegrees = 0f;` |  |
| поле | `worldScale` | `public float worldScale = 1f;` |  |
| поле | `hideVirtualStands` | `public bool hideVirtualStands = true;` |  |
| поле | `workZoneRadiusMeters` | `public float workZoneRadiusMeters = 1.0f;` |  |
| поле | `tableCenter` | `public Vector3 tableCenter = Vector3.zero;` |  |
| поле | `robotOnTable` | `public Transform robotOnTable;` |  |
| свойство | `RobotSize` | `public Vector3 RobotSize { get; private set; }` |  |
| свойство | `MrActive` | `public bool MrActive { get { return KvXrSettings.GetPassthrough(); } }` |  |

### `enum KvXrOriginMode`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrRigConfig.cs` (строка 22)
- **Назначение:** КОНФИГУРАЦИЯ XR-РИГА (сессия §28, этап 1 плана XR). ЗАЧЕМ ОТДЕЛЬНЫЙ ТИП. Все решения по ригу — уровень глаз, масштаб мира, способ стерео, отсчёт от пола или от устройства — должны быть ОДНИМ объектом, который можно посчитать, проверить тестом и показать в отчёте. Разбросанные по сцене числа проверить нельзя: в шлеме они выглядят «нормально» ровно до момента, когда оператор ростом 1,95 видит панель на уровне колен. ЧТО ЗДЕСЬ ЧИСТОЕ, А ЧТО НЕТ. Здесь только арифметика (никаких GameObject, сцен и устройств) — поэтому тип полностью покрыт EditMode-тестами, а сборку рига делает <c>KvXrRigBuilder</c>. Решения, на которые опирается тип: [R1] PICO 4 Ultra, [R2] HDRP низкий пресет, [R3] только OpenXR…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| значение | `Floor` | `Floor = 0` |  |
| значение | `Device` | `Device = 1` |  |
| значение | `Scene` | `Scene = 2` |  |

### `class KvXrPanelAnchor`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrPanelAnchor.cs` (строка 31)
- **Назначение:** ПЛОСКАЯ ПАНЕЛЬ ИНТЕРФЕЙСА В МИРОВОМ ПРОСТРАНСТВЕ (решение владельца [D1], этап 3). ПРОБЛЕМА, КОТОРУЮ ЭТО РЕШАЕТ. Интерфейс проекта — Canvas в режиме Screen Space Overlay: он рисуется ПОВЕРХ кадра, минуя камеры. В стерео это не работает: каждый глаз должен получить свою картинку, а overlay рисуется один раз на экран. В шлеме такой интерфейс либо не виден, либо виден плоско и «мимо глаз». РЕШЕНИЕ (вариант А, выбранный владельцем): канвас переводится в World Space и вешается перед оператором как накладной экран. Все панели, дерево, свойства, тулбар и настройки остаются ровно теми же объектами — меняется только способ их отображения. Это и есть «плоская панель» из решения [D1]. ПОЧЕМУ ГЕОМЕТРИЯ …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `targetCamera` | `public Camera targetCamera;` |  |
| поле | `followHead` | `public bool followHead = true;` |  |
| поле | `followSmoothSeconds` | `public float followSmoothSeconds = 0.35f;` |  |
| поле | `referenceWidth` | `public float referenceWidth = 1920f;` |  |
| поле | `referenceHeight` | `public float referenceHeight = 1080f;` |  |
| свойство | `Applied` | `public bool Applied { get { return applied; } }` |  |
| свойство | `WidthMeters` | `public float WidthMeters { get; private set; }` |  |
| свойство | `LastSettings` | `public KvXrUiPanelSettings LastSettings { get; private set; }` |  |

### `class KvXrRig`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrRig.cs` (строка 24)
- **Назначение:** КАМЕРА ШЛЕМА (этап 1 плана XR, сессия §28). ЧТО ДЕЛАЕТ. Ставит камеру в правильное место и сообщает о состоянии XR одной строкой. Позу камеры в шлеме выставляет САМ РАНТАЙМ OpenXR (система трекинга пишет в `Camera.transform`) — здесь ничего не переписывается: попытка «двигать камеру руками» в VR даёт тряску и «плывущую» картинку. ЧТО ПРОВЕРЯЕТСЯ ПРИ СТАРТЕ и попадает в журнал: • активен ли OpenXR (без него в шлеме чёрный экран — и это первая причина, которую надо исключить); • включено ли стерео SinglePassInstanced (иначе картинка для двух глаз считается дважды и частота кадров падает почти вдвое); • обнаружен ли шлем и контроллеры. ПОЧЕМУ НЕ XROrigin ИЗ XRI. `XROrigin` тянет за собой соглаш…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `headCamera` | `public Camera headCamera;` |  |
| поле | `logOnStart` | `public bool logOnStart = true;` |  |
| поле | `targetFrameRate` | `public int targetFrameRate = 72;` |  |
| поле | `eyeTextureResolutionScale` | `public float eyeTextureResolutionScale = 1f;` |  |
| свойство | `OpenXrActive` | `public bool OpenXrActive { get; private set; }` |  |
| свойство | `SinglePassInstanced` | `public bool SinglePassInstanced { get; private set; }` |  |
| свойство | `Status` | `public string Status { get; private set; }` |  |

### `class KvXrSceneContent`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrSceneContent.cs` (строка 28)
- **Назначение:** НАЧИНКА XR-СЦЕНЫ: СТОЛЫ И РОБОТЫ (решение владельца: «добавить роботов и столы», сессия §28). ЗАЧЕМ ОТДЕЛЬНЫЙ КОМПОНЕНТ. По решению [D6] XR-сцена сделана ОТДЕЛЬНОЙ и сначала была пустой (риг, интерфейс, MR-комната) — начинку предполагалось подгружать. Проверка показала, что в `XrMain.unity` нет ни одного робота, то есть в шлеме было нечего показывать. Этот компонент создаёт начинку ТЕМ ЖЕ способом, что и десктопный проект — через <see cref="StandBuilder"/>, а не копированием объектов из `MainScene`. ПОЧЕМУ ЧЕРЕЗ StandBuilder. Три причины: 1) это ровно тот код, которым построены стенды в `MainScene` — значит геометрия, высоты и расположение роботов совпадают с проверенными; 2) он идемпотентен…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `createStands` | `public bool createStands = true;` |  |
| поле | `tableTopHeight` | `public float tableTopHeight = StandBuilder.TopHeight;` |  |
| поле | `buildOnStart` | `public bool buildOnStart = true;` |  |
| свойство | `RobotRoot` | `public Transform RobotRoot { get; private set; }` |  |
| свойство | `RobotCount` | `public int RobotCount { get; private set; }` |  |

### `class KvXrSettingsChanged`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrSettingsChanged.cs` (строка 17)
- **Назначение:** ОПОВЕЩЕНИЕ О СМЕНЕ XR-НАСТРОЕК (сессия §28). ЗАЧЕМ. Настройки XR — статическое хранилище (PlayerPrefs): у него нет ни событий, ни владельца. А применять их надо немедленно: переключил «плавный ход» — метка телепорта обязана исчезнуть в тот же кадр, а не после перезапуска. Ровно на такой несогласованности в проекте уже были дефекты (§23: «настройки не открывались», «курсор не восстанавливался»), поэтому здесь одна точка оповещения на весь XR-слой. Кто подписывается: локомоция, панель интерфейса, слои MR. Кто вызывает: панель настроек (и диагностика).

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| метод | `Notify` | `public static void Notify()` |  |

### `struct KvXrTableSize`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrMrPlacement.cs` (строка 6)
- **Назначение:** —

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `WidthMeters` | `public float WidthMeters;` |  |
| поле | `DepthMeters` | `public float DepthMeters;` |  |
| поле | `HeightMeters` | `public float HeightMeters;` |  |

### `struct KvXrUiPanelSettings`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrUiPanel.cs` (строка 23)
- **Назначение:** ПЛОСКАЯ ПАНЕЛЬ ИНТЕРФЕЙСА В VR (решение владельца [D1], сессия §28, этап 3). ЧТО ЭТО. Весь существующий интерфейс проекта (меню, тулбар, дерево, свойства, настройки, статус-строка) висит в воздухе перед оператором как один большой накладной экран и управляется лучом контроллера. Пространственный интерфейс (панели на руке, кнопки-плитки) — ОТДЕЛЬНЫЙ этап после того, как владелец поживёт в шлеме: строить его «по представлению» значит гарантированно переделать. ГДЕ ЗДЕСЬ ГЛАВНАЯ ОПАСНОСТЬ. Интерфейс проекта — Screen Space Overlay: он рисуется поверх кадра и в стерео НЕ РАБОТАЕТ (каждый глаз отрисовал бы свой). Поэтому канвас переводится в World Space и вешается перед камерой. Размеры при этом п…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `position` | `public Vector3 position;` |  |
| поле | `rotation` | `public Quaternion rotation;` |  |
| поле | `worldScale` | `public float worldScale;` |  |
| поле | `widthMeters` | `public float widthMeters;` |  |
| свойство | `eulerAngles` | `public Vector3 eulerAngles { get { return rotation.eulerAngles; } }` |  |

### `class KvXrUiPointer`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrUiPointer.cs` (строка 32)
- **Назначение:** ЛУЧ КОНТРОЛЛЕРА ПО ИНТЕРФЕЙСУ (решение владельца [D1], этап 2–3 плана XR). ЗАЧЕМ. В шлеме нет мыши, и весь проект, построенный вокруг системного курсора, перестаёт управляться. Здесь контроллер превращается в «мышь»: из руки идёт луч, он попадает в панель интерфейса, курок нажимает кнопку. Это и есть минимальный рабочий ввод, после которого шлем перестаёт быть кинозалом. КАК ЭТО РАБОТАЕТ БЕЗ XRI. Используется штатный `GraphicRaycaster` канваса: он умеет пускать луч в мировом пространстве (для Canvas в режиме World Space). Положение указателя передаётся в мировых координатах — этого достаточно, чтобы кнопки подсвечивались и нажимались. Ни одного нового пакета для этого не нужно. ПОЧЕМУ УЧАСТИ…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `rayLength` | `public float rayLength = 10f;` |  |
| поле | `rayWidth` | `public float rayWidth = 0.008f;` |  |
| поле | `idleColor` | `public Color idleColor = new Color(0.55f, 0.75f, 1f, 0.55f);` |  |
| поле | `hoverColor` | `public Color hoverColor = new Color(0.30f, 0.95f, 0.55f, 0.95f);` |  |
| поле | `triggerThreshold` | `public float triggerThreshold = 0.5f;` |  |
| свойство | `AimingAtUi` | `public bool AimingAtUi { get { return aimingAtUi; } }` |  |
| свойство | `HoverName` | `public string HoverName { get { return currentHover != null ? currentHover.name : "—"; } }` |  |
| свойство | `ClickCount` | `public int ClickCount { get; private set; }` |  |

## Пространство имён `KazistovVvXrEditor`

### `class KvPostProcessVariantStripper`

- **Файл:** `Assets\_Project\01_Scripts\Editor\KvShaderVariantStripper.cs` (строка 41)
- **Назначение:** ОТСЕЧЕНИЕ ВАРИАНТОВ ПОСТОБРАБОТКИ ПРИ СБОРКЕ — карточка Б2 (Этап А). ЧТО БЫЛО ИЗМЕРЕНО (сборка §30, лог `Logs\xr-build-apk-s30.log`): шейдер `Hidden/Universal Render Pipeline/UberPost` компилировался как **92 160 вариантов на каждый из двух проходов**, `FinalPost` — 2 560 × 4 прохода. Из ~122 000 вариантов всей сборки на постобработку пришлось ~105 000, и сборка шла 46 часов, не дойдя до конца. ПОЧЕМУ ШТАТНОЕ ОТСЕЧЕНИЕ URP НЕ ПОМОГЛО. В URP отсечение постобработки включено (`UniversalRenderPipelineGlobalSettings` → `m_StripUnusedPostProcessingVariants: 1`), но работает оно так (`URP/Editor/ShaderBuildPreprocessor.cs`, `GetSupportedFeaturesFromVolumes`): берутся ВСЕ профили объёма в `Assets/`…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `callbackOrder` | `public int callbackOrder { get { return 0; } }` |  |
| константа | `UberPostShader` | `public const string UberPostShader = "Hidden/Universal Render Pipeline/UberPost";` |  |
| константа | `FinalPostShader` | `public const string FinalPostShader = "Hidden/Universal Render Pipeline/FinalPost";` |  |
| поле (только чтение) | `EffectKeywords` | `public static readonly string[] EffectKeywords =` |  |

### `class KvRenderPipelineTools`

- **Файл:** `Assets\_Project\01_Scripts\Editor\KvRenderPipelineTools.cs` (строка 37)
- **Назначение:** ИНСТРУМЕНТЫ ПАЙПЛАЙНА РЕНДЕРА (сессия §29, карточка Б1: HDRP мешает сборке под шлем). ЗАЧЕМ ОТДЕЛЬНЫЙ ФАЙЛ. Решение [D9] — «шлем на URP, компьютер на HDRP» — требует ПЕРЕНЕСТИ глобальное умолчание графики с HDRP на URP. Это правка проектной настройки (`ProjectSettings/GraphicsSettings.asset`), а не сцены, поэтому: • делается кодом (воспроизводимо и проверяемо), а не «руками в окне»; • печатает состояние ДО и ПОСЛЕ — иначе нечем доказать, что десктоп не поехал; • ничего не сохраняет, пока не попросят (у инструмента чтения нет побочных эффектов). ЧТО ЗДЕСЬ ЕЩЁ И ПОЧЕМУ. «Готовые текстуры без ошибок» — это не только настройка пайплайна: материал, сделанный на шейдере HDRP, под URP рисуется РОЗО…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| константа | `UrpAssetPath` | `public const string UrpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";` |  |
| константа | `HdrpAssetPath` | `public const string HdrpAssetPath = "Assets/ROOMS/HQ Hangar Free/Scene/HDRPHighQuality.asset";` |  |
| метод | `Recon` | `public static void Recon()` |  |

### `class KvUrpKeywordPolicy`

- **Файл:** `Assets\_Project\01_Scripts\Editor\KvUrpKeywordPolicy.cs` (строка 25)
- **Назначение:** ПОЛИТИКА ОТСЕЧЕНИЯ КЛЮЧЕВЫХ СЛОВ URP — ЧТО ПРОЕКТ НЕ ИСПОЛЬЗУЕТ, С ПРИЧИНОЙ. ЗАЧЕМ ЭТО ПОЯВИЛОСЬ. В сборке `Universal Render Pipeline/Lit` (проход `ForwardLit`) даёт **442 368 вариантов**, и URP их не срезает, хотя обязан: в его собственном коде (`Editor/ShaderBuildPreprocessor.cs`, `CreatePrefilteringSettings`) режимы отсечения вычисляются из настроек URP-ассета. Замерено 08.10.2026: в ассете `Assets/Settings/Mobile_RPAsset.asset` уже лежат ЗНАЧЕНИЯ ПО УМОЛЧАНИЮ (`mainLightShadows = SelectAll`, `additionalLights = SelectAll`, остальные `Select`) — то есть URP считает, что «оставить нужно всё»: мягкие тени выключены, тени дополнительного света выключены, дополнительный свет повершинный, каск…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| константа | `UrpAssetPath` | `public const string UrpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";` |  |
| константа | `UrpRendererPath` | `public const string UrpRendererPath = "Assets/Settings/Mobile_Renderer.asset";` |  |
| метод | `Describe` | `public static string Describe()` |  |

### `class KvXrDeviceTools`

- **Файл:** `Assets\_Project\01_Scripts\Editor\KvXrDeviceTools.cs` (строка 29)
- **Назначение:** НАСТРОЙКИ ПРИЛОЖЕНИЯ ДЛЯ ЖИВОГО УСТРОЙСТВА (сессия §31: шлем PICO 4 Ultra приехал). ЗАЧЕМ ОТДЕЛЬНЫЙ ИНСТРУМЕНТ. Эти настройки нельзя «посмотреть глазами» в редакторе так, чтобы понять, что попадёт в APK: идентификатор приложения лежит в проектном файле, а состояние OpenXR-фич зависит от ПЛАТФОРМЫ (включённое для Standalone в сборку под Android не попадает). Ошибка здесь выглядит так: приложение поставилось, запустилось, но в шлеме не работают контроллеры — и непонятно, почему. ПОДТВЕРЖДЕНИЕ ФАКТА, А НЕ НАМЕРЕНИЯ. Оба метода печатают состояние ДО и ПОСЛЕ и перечитывают значение — как в §29 с глобальным умолчанием графики: правка считается сделанной только если её видно в сохранённом файле. ВН…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| константа | `OwnIdentifier` | `public const string OwnIdentifier = "com.KazistovVv.app";` |  |
| константа | `SimpleControllerProfile` | `public const string SimpleControllerProfile = "KHRSimpleControllerProfile";` |  |
| метод | `ApplyOwnIdentifier` | `public static void ApplyOwnIdentifier()` |  |

### `class KvXrTools`

- **Файл:** `Assets\_Project\01_Scripts\Editor\KvXrTools.cs` (строка 33)
- **Назначение:** ИНСТРУМЕНТЫ XR-ЭТАПА (сессия §28, этапы 0–4 плана XR). ПОЧЕМУ ВСЁ ДЕЛАЕТСЯ КОДОМ, А НЕ РУКАМИ В РЕДАКТОРЕ. Шлема нет, а собрать XR-сцену надо так, чтобы её можно было ПОВТОРИТЬ и ПРОВЕРИТЬ: сцена из кода проверяется повторным запуском и сравнением, сцена из ручных действий — только памятью. Плюс: проект ведётся в VCS, и воспроизводимая сборка сцены не создаёт конфликтов «а у меня сцена другая». ЧТО ЗДЕСЬ ГЛАВНОЕ (<see cref="ConfigureXrLoaders"/>) — НАЙДЕННЫЙ ДЕФЕКТ: в проекте активными лоадерами XR стояли **Mock HMD** и аркадный ARCore, а OpenXR — просто лежал ассетом. С таким набором собранное приложение в шлеме НЕ РАБОТАЛО БЫ вообще: Mock HMD — это заглушка для отладки без устройства. Имен…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| константа | `XrSceneName` | `public const string XrSceneName = "XrMain";` |  |
| константа | `ScenesFolder` | `public const string ScenesFolder = "Assets/_Project/00_Scenes";` |  |
| метод | `ConfigureXrLoaders` | `public static void ConfigureXrLoaders()` |  |


## Дополнение 15.10.2026: профиль контроллеров PICO и нажатие взглядом (§33–§34)

_Собрано тем же инструментом (`Tools\api-reference-add-xr.ps1`, черновик `Tools\_stage_a\API_S33_34.md`), что и дополнение §28–§31: только то, что написано в коде._

**Новые типы §§33–§34 — два.** Остальные файлы XR-слоя в этой сессии ПРАВИЛИСЬ, а не создавались, и их описания из дополнения §28–§31 остаются в силе; изменения перечислены в конце раздела.

### Пространство имён `KazistovVvXr`
### `class KvGazeDwell`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvGazeDwell.cs` (строка 21)
- **Назначение:** НАЖАТИЕ ВЗГЛЯДОМ ПО ЗАДЕРЖКЕ (сессия §33, дефект с живого шлема 15.10.2026). ЗАЧЕМ. Владелец в шлеме: «не могу навестись на оверлей». Причина не в интерфейсе: указатель проекта брал луч у ПРАВОГО КОНТРОЛЛЕРА (`TryRightPose`), а контроллеры приложению не отдаются вовсе — значит луча не было и нажать было нечем. Пока контроллеры не заведены, единственный доступный «указатель» — ВЗГЛЯД: человек смотрит на кнопку, и через некоторое время она нажимается сама. Такой способ (dwell) — стандарт для VR-интерфейсов без контроллеров, и он не мешает: когда контроллер появится, работает обычный луч с курком. ПОЧЕМУ ВЫНЕСЕНО В ОТДЕЛЬНЫЙ ТИП. Решение «пора нажимать» — это ЧИСЛО, а не картинка: накопили врем…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| константа | `DefaultSeconds` | `public const float DefaultSeconds = 1.3f;` |  |
| константа | `MinSeconds` | `public const float MinSeconds = 0.2f;` |  |
| метод | `Advance` | `public static float Advance(float progress, float deltaTime, float threshold)` |  |


### `class KvPicoControllerProfile`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvPicoControllerProfile.cs` (строка 75)
- **Назначение:** ПРОФИЛЬ КОНТРОЛЛЕРОВ PICO 4 ULTRA ДЛЯ OPENXR (сессия §34, дефект с живого шлема 15.10.2026). ЗАЧЕМ СВОЙ ПРОФИЛЬ. Контроллеры в руках владельца работали: система шлема их видела и печатала позы (`PvrTrackingService: logDebugControllerPose,left confidence 2`). А приложение — нет: `[KvXr] XR-ввод: шлем есть · правый нет · левый нет`. ПОЧЕМУ. OpenXR создаёт устройство ввода только для ТОГО профиля взаимодействия, который рантайм назвал текущим для руки. Журнал шлема называет его прямо: `xrGetCurrentInteractionProfile ret = 0 Path /interaction_profiles/bytedance/pico4s_controller` В пакете Unity `com.unity.xr.openxr` профилей PICO НЕТ ВООБЩЕ: там Khronos Simple, Oculus Touch, Valve Index, Microso…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| константа | `featureId` | `public const string featureId = "com.kazistovvv.openxr.feature.input.pico4s";` |  |
| константа | `profile` | `public const string profile = "/interaction_profiles/bytedance/pico4s_controller";` |  |
| константа | `extensionString` | `public const string extensionString = "XR_BD_controller_interaction";` |  |
| константа | `trigger` | `public const string trigger = "/input/trigger/value";` |  |
| константа | `triggerClick` | `public const string triggerClick = "/input/trigger/click";` |  |
| константа | `triggerTouch` | `public const string triggerTouch = "/input/trigger/touch";` |  |
| константа | `squeeze` | `public const string squeeze = "/input/squeeze/value";` |  |
| константа | `squeezeClick` | `public const string squeezeClick = "/input/squeeze/click";` |  |
| константа | `thumbstick` | `public const string thumbstick = "/input/thumbstick";` |  |
| константа | `thumbstickClick` | `public const string thumbstickClick = "/input/thumbstick/click";` |  |
| константа | `thumbstickTouch` | `public const string thumbstickTouch = "/input/thumbstick/touch";` |  |
| константа | `buttonA` | `public const string buttonA = "/input/a/click";` |  |
| константа | `buttonATouch` | `public const string buttonATouch = "/input/a/touch";` |  |
| константа | `buttonB` | `public const string buttonB = "/input/b/click";` |  |
| константа | `buttonBTouch` | `public const string buttonBTouch = "/input/b/touch";` |  |
| константа | `buttonX` | `public const string buttonX = "/input/x/click";` |  |
| константа | `buttonXTouch` | `public const string buttonXTouch = "/input/x/touch";` |  |
| константа | `buttonY` | `public const string buttonY = "/input/y/click";` |  |
| константа | `buttonYTouch` | `public const string buttonYTouch = "/input/y/touch";` |  |
| константа | `menu` | `public const string menu = "/input/menu/click";` |  |
| константа | `system` | `public const string system = "/input/system/click";` |  |
| константа | `grip` | `public const string grip = "/input/grip/pose";` |  |
| константа | `aim` | `public const string aim = "/input/aim/pose";` |  |
| константа | `haptic` | `public const string haptic = "/output/haptic";` |  |
| тип | `KvPicoController` | `public class KvPicoController : XRControllerWithRumble` |  |
| свойство | `thumbstick` | `public ThumbstickControl thumbstick { get; private set; }` |  |
| свойство | `grip` | `public AxisControl grip { get; private set; }` |  |
| свойство | `gripPressed` | `public ButtonControl gripPressed { get; private set; }` |  |
| свойство | `menu` | `public ButtonControl menu { get; private set; }` |  |
| свойство | `primaryButton` | `public ButtonControl primaryButton { get; private set; }` |  |
| свойство | `primaryTouched` | `public ButtonControl primaryTouched { get; private set; }` |  |
| свойство | `secondaryButton` | `public ButtonControl secondaryButton { get; private set; }` |  |
| свойство | `secondaryTouched` | `public ButtonControl secondaryTouched { get; private set; }` |  |
| свойство | `trigger` | `public AxisControl trigger { get; private set; }` |  |
| свойство | `triggerPressed` | `public ButtonControl triggerPressed { get; private set; }` |  |
| свойство | `triggerTouched` | `public ButtonControl triggerTouched { get; private set; }` |  |
| свойство | `thumbstickClicked` | `public ButtonControl thumbstickClicked { get; private set; }` |  |
| свойство | `thumbstickTouched` | `public ButtonControl thumbstickTouched { get; private set; }` |  |
| свойство | `devicePose` | `public PoseControl devicePose { get; private set; }` |  |
| свойство | `pointer` | `public PoseControl pointer { get; private set; }` |  |
| свойство | `haptic` | `public HapticControl haptic { get; private set; }` |  |


### Что изменилось в уже описанных типах (§33–§34)

| Тип | Что изменилось | Зачем |
|---|---|---|
| `KvXrSceneContent` | поля `bringStandsNearPlayer`, `standDistance`, `standSpacing`, `createFloor`, `floorSize`; методы `PlaceStandsAroundPlayer`, `EnsureFloor`; константа `FloorName` | стенды переносятся к оператору (3,2 м), под ногами создаётся пол 40×40 м — иначе в шлеме «только горизонт» |
| `KvXrQuickPanel` | поле `fallbackLogged`; поведение при отсутствии позы руки — панель встаёт перед глазами (1,15 м, на 0,25 м ниже уровня глаз) | без контроллеров панель раньше пряталась, и её не видели вообще |
| `KvXrUiPointer` | поля `useGazeWhenNoController`, `gazeDwellSeconds`, `gazeOriginForward`, `gazeOriginDown`; метод `HandleGazeClick`; `Aiming` учитывает режим взгляда | наведение и нажатие взглядом, когда контроллеров нет |
| `KvXrInputSource` | новых публичных членов нет; события «контроллеры появились/пропали» больше не ограничены по времени | короткое окно диагностики скрывало появление контроллеров |
| `KvXrDeviceTools` | константа `PicoControllerProfile`; `ApplyControllerProfile` включает оба профиля и подтверждает по факту; обновление списка фич OpenXR | фича, не включённая для Android, в APK не попадает |

---

## Дополнение 18.10.2026: панель групп у руки, ангар, камера за головой, контроллеры (§35–§36)

_Раздел собран так же, как дополнения §28–§31 и §33–§34: описания типов и таблицы членов взяты
из исходников дословно (XML-комментарии `/// <summary>`), черновик — `Tools\_stage_a\API_S36_draft.md`.
Таблицы «что изменилось» и два типа, которые генератор не взял (в их файлах первым объявлен
другой тип), написаны вручную по сделанным правкам._

**Появилось в §35:** панель с ОБОБЩАЮЩИМИ ИКОНКАМИ ГРУПП вместо плоской панели с подписями;
ангар вокруг оператора; камера, которая следует за головой (карточка Б23); сторож десктопного
оверлея; перевод материалов ангара на URP для шлема.
**Появилось в §36:** строки диагностики OpenXR и ввода в журнал; ГЕОМЕТРИЧЕСКОЕ наведение луча
на панель (луч × плоскость панели, метры) вместо экранных пикселей uGUI.

### Пространство имён `KazistovVvXr`


### `class KvHangarPlacement`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvHangarPlacement.cs` (строка 34)
- **Назначение:** ГДЕ СТАВИТЬ АНГАР В XR-СЦЕНЕ (сессия §35, карточка Б17). ЗАЧЕМ ОТДЕЛЬНЫЙ ТИП. «Поставить ангар рядом со стендами» — это ЧИСЛО, а не картинка, и ошибиться в нём легко на десятки метров: ангар проекта — зал 37,8 × 50,3 м при потолке 9,4 м, и промах означает либо «ангар в стороне от оператора», либо «оператор внутри стены». Здесь лежит только арифметика размещения; объекты сцены создаёт редакторный инструмент `KvXrHangarTools`, а правило проверяется тестом без шлема и без сцены. ОТКУДА ЧИСЛА. Измерено, а не подобрано: редакторный метод `KvXrHangarTools.MeasureDesktopEnvironment` (журнал `Logs\s35-hangar-measure3.log`, 16.10.2026) вернул габарит ангара (40,18 × 9,38 × 50,31), его центр (10,91; 4…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле (только чтение) | `DesktopHangarSize` | `public static readonly Vector3 DesktopHangarSize = new Vector3(40.18f, 9.38f, 50.31f);` |  |
| поле (только чтение) | `DesktopHangarCenter` | `public static readonly Vector3 DesktopHangarCenter = new Vector3(10.91f, 4.69f, -15.00f);` |  |
| поле (только чтение) | `DesktopStandsCenter` | `public static readonly Vector3 DesktopStandsCenter = new Vector3(0.00f, 1.30f, -26.00f);` |  |
| поле (только чтение) | `HangarLocalCenter` | `public static readonly Vector3 HangarLocalCenter = DesktopHangarCenter;` |  |
| константа | `RootScale` | `public const float RootScale = 1f;` |  |
| константа | `MinCeilingMeters` | `public const float MinCeilingMeters = 2.2f;` |  |

### `class KvHdrpToUrpMaterial`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvHdrpToUrpMaterial.cs` (строка 29)
- **Назначение:** ПЕРЕВОД МАТЕРИАЛОВ АНГАРА С HDRP НА URP (сессия §35, карточка Б17). ЗАЧЕМ. Материалы ангара в проекте — `HDRP/Lit` (проверено: шейдер `…render-pipelines.high-definition…/Runtime/Material/Lit/Lit.shader`). На компьютере это правильно: уровень качества `Standalone = 1` — это HDRP. Но для шлема собирается уровень качества `Mobile` (в `QualitySettings.asset` записано `Android: 0`), а он на `Assets\Settings\Mobile_RPAsset.asset` — это **URP**. HDRP-материал под URP даёт РОЗОВУЮ поверхность, а приёмка владельца требует «ни одной розовой поверхности». ПОЧЕМУ НЕЛЬЗЯ ПРОСТО ПОПРАВИТЬ МАТЕРИАЛЫ АНГАРА. Общие `.mat` в `Assets\ROOMS\HQ Hangar Free\Materials\` используются десктопной сценой, которая рабо…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| константа | `HdrpBaseColorMap` | `public const string HdrpBaseColorMap = "_BaseColorMap";` |  |
| константа | `UrpBaseMap` | `public const string UrpBaseMap = "_BaseMap";` |  |
| константа | `BaseColor` | `public const string BaseColor = "_BaseColor";` |  |
| константа | `HdrpNormalMap` | `public const string HdrpNormalMap = "_NormalMap";` |  |
| константа | `UrpBumpMap` | `public const string UrpBumpMap = "_BumpMap";` |  |
| константа | `HdrpMaskMap` | `public const string HdrpMaskMap = "_MaskMap";` |  |
| константа | `UrpMetallicGlossMap` | `public const string UrpMetallicGlossMap = "_MetallicGlossMap";` |  |
| константа | `UrpOcclusionMap` | `public const string UrpOcclusionMap = "_OcclusionMap";` |  |
| константа | `Metallic` | `public const string Metallic = "_Metallic";` |  |
| константа | `Smoothness` | `public const string Smoothness = "_Smoothness";` |  |
| константа | `UrpLitShader` | `public const string UrpLitShader = "Universal Render Pipeline/Lit";` |  |
| константа | `HdrpMetallicChannel` | `public const int HdrpMetallicChannel = 0;      // R` |  |
| константа | `HdrpOcclusionChannel` | `public const int HdrpOcclusionChannel = 1;     // G` |  |
| константа | `HdrpDetailChannel` | `public const int HdrpDetailChannel = 2;        // B` |  |
| константа | `HdrpSmoothnessChannel` | `public const int HdrpSmoothnessChannel = 3;    // A` |  |
| метод | `ToUrpMetallicGloss` | `public static Color ToUrpMetallicGloss(Color hdrpMask)` |  |

### `class KvXrDesktopOverlayGuard`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrDesktopOverlayGuard.cs` (строка 34)
- **Назначение:** ДЕСКТОПНОГО ИНТЕРФЕЙСА В ШЛЕМЕ БЫТЬ НЕ ДОЛЖНО (сессия §35, карточка Б20). ЗАДАНИЕ ВЛАДЕЛЬЦА. «Десктопный оверлей инструментов и окон (то, что похоже на интерфейс для ПК) в шлеме должен быть скрыт — его там быть не должно». ЧТО ИМЕННО МЕШАЛО. В `XrMain` живёт проектный канвас (`KazistovVv_UI` / `KazistovVvCanvas`) с деревом, окнами и тулбаром, а компонент `KvXrPanelAnchor` переводил его в мировое пространство и вешал перед оператором. Это и есть «интерфейс для ПК» в шлеме. ГЛАВНАЯ ЛОВУШКА, РАДИ КОТОРОЙ ЭТОТ КОМПОНЕНТ УСТРОЕН ИМЕННО ТАК. Кажется очевидным: «десктопный интерфейс не нужен — выключить `KazistovVv_UI`». Это сломало бы панель шлема: РЕЕСТР КОМАНД (`KvCommands`) наполняет менеджер и…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `checkIntervalSeconds` | `public float checkIntervalSeconds = 1f;` |  |
| поле | `neutralizePanelAnchor` | `public bool neutralizePanelAnchor = true;` |  |
| поле | `logOnGuard` | `public bool logOnGuard = true;` |  |

### `class KvXrGroupPanel`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrGroupPanel.cs` (строка 39)
- **Назначение:** ПАНЕЛЬ С ОБОБЩАЮЩИМИ ИКОНКАМИ ГРУПП У РУКИ (сессия §35, карточка Б19). РЕШЕНИЕ ВЛАДЕЛЬЦА (16.10.2026), дословно: • «Панель инструментов должна быть у руки (у контроллера), а не висеть в середине экрана»; • «Основные кнопки — ОБОБЩАЮЩИЕ ИКОНКИ групп (не текст)»; • «Текст появляется только после наведения и удержания фокуса 0,5 секунды»; • «После нажатия на кнопку группы — полумесяцем раскрываются ВСЕ иконки этой группы, без названий, одними иконками». ЧТО ЗАМЕНЕНО. Прежняя панель (`KvXrQuickPanel`) показывала 8 ПЛОСКИХ кнопок с постоянными ПОДПИСЯМИ, а когда позы руки не было — вставала ПЕРЕД ГЛАЗАМИ на 1,15 м. Владелец запретил и то, и другое: подписи превращали панель в текст, а показ перед…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `headCamera` | `public Camera headCamera;` |  |
| поле | `sortingOrder` | `public int sortingOrder = 55;` |  |
| поле | `forwardOffset` | `public float forwardOffset = 0.14f;` |  |
| поле | `upOffset` | `public float upOffset = 0.06f;` |  |
| поле | `beltHeightMeters` | `public float beltHeightMeters = 1.05f;` |  |
| поле | `canvasSize` | `public float canvasSize = 420f;` |  |
| поле | `metersPerCanvasUnit` | `public float metersPerCanvasUnit = 0.0025f;` |  |
| поле | `handForwardMeters` | `public float handForwardMeters = 0.30f;` |  |
| поле | `handUpMeters` | `public float handUpMeters = 0.04f;` |  |
| поле | `handMinDistanceFromEyes` | `public float handMinDistanceFromEyes = 0.35f;` |  |
| поле | `handMaxDistanceFromEyes` | `public float handMaxDistanceFromEyes = 1.20f;` |  |
| поле | `logOnBuild` | `public bool logOnBuild = true;` |  |
| свойство | `BuiltOnce` | `public bool BuiltOnce { get { return builtOnce; } }` |  |
| свойство | `IconCount` | `public int IconCount { get { return icons.Count; } }` |  |
| свойство | `Level` | `public KvXrPanelLevel Level { get { return level; } }` |  |
| свойство | `OpenGroupIndex` | `public int OpenGroupIndex { get { return openGroupIndex; } }` |  |
| свойство | `Status` | `public string Status { get; private set; }` |  |
| свойство | `PlacedAtHand` | `public bool PlacedAtHand { get; private set; }` |  |

### `class KvXrHangarAnchor`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrHangarAnchor.cs` (строка 27)
- **Назначение:** СТАВИТ АНГАР ТАМ, ГДЕ ОКАЗАЛИСЬ СТЕНДЫ (сессия §35, карточка Б17). ЗАЧЕМ ЭТОТ КОМПОНЕНТ, ЕСЛИ АНГАР УЖЕ ЛЕЖИТ В СЦЕНЕ. Геометрия ангара впекается в `XrMain` редакторным инструментом — так её видно и в сборке, и в файле уровня. Но ПОЛОЖЕНИЕ ангара нельзя впечь окончательно: `KvXrSceneContent` переносит стенды вокруг оператора ВО ВРЕМЯ РАБОТЫ, отталкиваясь от того, где оператор стоит. Если оператор окажется не в начале координат, стенды уедут — и впечённый ангар разошёлся бы с ними: столы вылезли бы из стены или оказались бы снаружи зала. ЧТО ДЕЛАЕТ. После того как стенды встали, находит их общий габарит и переносит корень ангара по тому же правилу, что и на компьютере (см. <see cref="KvHangar…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `hangarRoot` | `public Transform hangarRoot;` |  |
| поле | `forceFloorAtZero` | `public bool forceFloorAtZero = true;` |  |
| поле | `logOnPlace` | `public bool logOnPlace = true;` |  |
| свойство | `StandsCenter` | `public Vector3 StandsCenter { get; private set; }` |  |
| свойство | `AppliedRootPosition` | `public Vector3 AppliedRootPosition { get; private set; }` |  |
| свойство | `Applied` | `public bool Applied { get; private set; }` |  |

### `class KvXrHeadTracking`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrHeadTracking.cs` (строка 35)
- **Назначение:** КАМЕРА ШЛЕМА СЛЕДУЕТ ЗА ГОЛОВОЙ ОПЕРАТОРА (сессия §35, найдено 18.10.2026 на живом шлеме). СИМПТОМЫ ВЛАДЕЛЬЦА, которые это лечит: • «камера где-то в непонятном месте»; • «я нахожусь в 0 по высоте где-то за ангаром»; • «камера плохо поворачивается и всегда крутится». ПРИЧИНА (доказана строкой журнала, а не рассуждением). В журнале шлема: [KvHangar] камеры: «XrCamera» (0.00, 0.00, 0.00) вкл (main) · поза шлема (-0.73, 1.54, -0.22) То есть объект-камера стоял в НАЧАЛЕ КООРДИНАТ НА УРОВНЕ ПОЛА, а настоящая голова была на высоте 1,54 м — и в другом месте. Проба ориентации восемь раз подряд показала «курс шлема 0.0°», хотя оператор в это время крутил головой. ПОЧЕМУ ТАК ВЫШЛО. В XR-сцене НЕТ ни `T…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `headCamera` | `public Camera headCamera;` |  |
| поле | `logOnFirstApply` | `public bool logOnFirstApply = true;` |  |
| свойство | `AppliedFrames` | `public int AppliedFrames { get; private set; }` |  |
| свойство | `LastHeadPosition` | `public Vector3 LastHeadPosition { get; private set; }` |  |
| свойство | `Tracking` | `public bool Tracking { get; private set; }` |  |

### `class KvXrIconHover`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrIconHover.cs` (строка 24)
- **Назначение:** НАВЕДЕНИЕ НА ИКОНКУ ПАНЕЛИ (сессия §35, карточка Б19). ЗАЧЕМ. Владелец требует: «Текст появляется только после наведения и удержания фокуса 0,5 секунды». Чтобы отсчитать эти полсекунды, панель должна знать, когда луч навёл её иконку, а когда ушёл с неё. ПОЧЕМУ ИМЕННО ТАК, А НЕ «СВОИМ ЛУЧОМ». Указатель проекта (`KvXrUiPointer`) уже подсвечивает цель через `ExecuteEvents.ExecuteHierarchy(..., pointerEnterHandler)` и `pointerExitHandler`. Значит иконке достаточно реализовать эти два обработчика — и наведение придёт ТЕМ ЖЕ путём, что и нажатие. Второго способа ввода не появляется, и разойтись им негде: если указатель перенастроят, наведение и нажатие сместятся вместе. ЭТО КАСАЕТСЯ ТОЛЬКО УРОВНЯ …

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| свойство | `Title` | `public string Title { get; private set; }` |  |
| свойство | `Hovered` | `public bool Hovered { get; private set; }` |  |
| метод | `Bind` | `public void Bind(KvXrGroupPanel owner, string title)` |  |

### `class KvXrOpenXrDiagnostics`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrOpenXrDiagnostics.cs` (строка 29)
- **Назначение:** СТРОКИ ОБ OPENXR И ВВОДЕ ДЛЯ ЖУРНАЛА ШЛЕМА (сессия §36). ЗАЧЕМ. Требование владельца [F5]: «сделано» без строки в журнале не принимается. По дефекту контроллеров (карточка Б18/Б24) в журнале раньше было видно только «правый нет · левый нет · устройств 0», то есть итог, а не причину. Здесь печатается то, что раньше приходилось выяснять перебором логов рантайма: • выдал ли рантайм расширение `XR_BD_controller_interaction` (без него профиль ByteDance не действует вовсе); • сколько расширений рантайм объявил всего и сколько включено; • не нарушает ли наша карта привязок правило «путь кнопки руки → своя рука» — именно это нарушение заставляло OpenXR отвечать `XR_ERROR_PATH_UNSUPPORTED` и отбрасыв…

### `enum KvXrOverlayDisposition`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrOverlayPolicy.cs` (строка 6)
- **Назначение:** —

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| значение | `HideRendering` | `HideRendering = 0` |  |
| значение | `Remove` | `Remove = 1` |  |
| значение | `Keep` | `Keep = 2` |  |

### `enum KvXrPanelLevel`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrGroupPanelLayout.cs` (строка 6)
- **Назначение:** —

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| значение | `Groups` | `Groups = 0` |  |
| значение | `GroupFan` | `GroupFan = 1` |  |

### `class KvXrGroupPanelLayout` (генератор не взял: в файле первым объявлен `enum KvXrPanelLevel`)

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrGroupPanelLayout.cs`
- **Назначение:** ГЕОМЕТРИЯ ПАНЕЛИ С ОБОБЩАЮЩИМИ ИКОНКАМИ ГРУПП (§35) и ПОПАДАНИЕ ЛУЧОМ ПО
  ИКОНКЕ (§36). Чистая арифметика без `GameObject`, кадра и состояния: углы, радиусы, порог
  подписи, проверка попадания луча в плоскость панели. Всё это проверяется тестами без шлема —
  и только поэтому подпись 0,5 с и веер 210° можно считать сделанными, а не «на глаз».

| Тип | Имя | Смысл |
|---|---|---|
| константа | `MaxFanIcons` = 14 | предел веера — размер самой большой группы проекта («Визуализация»); у прежней панели он 12 и закреплён её тестами |
| константа | `MinFanIcons` = 1 | меньше одной иконки — это не веер |
| константа | `GroupCount` = 8 | сколько групп показывает уровень 0 |
| константа | `IconSizeMeters` = 0,042 | размер иконки: читается с 40 см и уверенно ловится лучом |
| константа | `MinGapMeters` = 0,012 | просвет между соседними иконками |
| константа | `MinRadiusMeters` = 0,14 · `MaxRadiusMeters` = 0,42 | границы радиуса полумесяца |
| константа | `GroupSpanDegrees` = 160 · `FanSpanDegrees` = 210 | дуга уровня групп и дуга веера |
| константа | `UpDegrees` = 90 | середина дуги всегда сверху |
| константа | `LabelDwellSeconds` = 0,5 | **порог подписи — решение владельца** |
| константа | `HitRadiusMeters` = 0,85 × размера иконки | радиус попадания луча |
| константа | `BeltDropMeters` · `BeltForwardMeters` · `BeltMinAngleFromGazeDegrees` | запасное положение у пояса и его угол от взгляда |
| метод | `ClampFanCount` · `IsValidFanCount` | приведение и проверка числа иконок веера |
| метод | `RadiusForCount` · `AngleDegrees` · `OffsetMeters` (две перегрузки) | раскладка полумесяца |
| метод | `GroupOffsets` · `FanOffsets` | смещения иконок уровня 0 и веера, метры |
| метод | `ClosestIndex` | номер ближайшей иконки или −1 (возврат −1 — не «нулевая иконка», иначе нажатие «в пустоту» срабатывало бы) |
| метод | `TryRayToPanelPlane` **(§36)** | пересечение луча с плоскостью панели: точка в осях панели и дальность вдоль луча, МЕТРЫ |
| метод | `TryHitIcon` **(§36)** | попадание в иконку по геометрии: номер, расстояние от центра, дальность вдоль луча |
| метод | `ShowsLabel` · `AdvanceLabelDwell` | правило показа подписи (только уровень групп, только после 0,5 с) |
| метод | `BeltPosition` · `BeltAngleFromGazeDegrees` · `AngleFromGazeDegrees` · `Flatten` | запасное положение у пояса и угол от направления взгляда |
| метод | `Describe` | строка журнала панели (числа с инвариантной культурой) |

### `class KvXrOverlayPolicy` (тоже не попал в генератор: в файле первым объявлен `enum KvXrOverlayDisposition`)

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrOverlayPolicy.cs`
- **Назначение:** ПРАВИЛО «что в шлеме видно, а что нет» — чистые данные и решения, которые сторож
  (`KvXrDesktopOverlayGuard`) только применяет. Отдельный тип ради тестов: «десктопный интерфейс в
  шлеме погашен» — это утверждение, и оно проверяется списком, а не глазами.
  Ключевой запрет, записанный в коде: **гасить рисование ≠ гасить кликабельность** — невидимый
  интерфейс продолжал ловить луч и нажиматься (найдено 16.10.2026), поэтому отключаются и
  `Canvas.enabled`, и `GraphicRaycaster`.

### Что изменилось в уже описанных типах (§35–§36)

| Тип | Что изменилось | Зачем |
|---|---|---|
| `KvPicoControllerProfile` | у каждой кнопки руки свой `userPaths` (`x/y/menu → leftHand`, `a/b → rightHand`); карта действий вынесена в `BuildActionMap()`; правило `HandBindingViolation`, `ComponentOf`, аудит `AuditHandBindings` | **главный дефект Б18**: без `userPaths` Unity подставляет путь ОБЕИМ рукам (`OpenXRInput.cs:296`), рантайм отвечает `XR_ERROR_PATH_UNSUPPORTED` и отбрасывает весь набор привязок — контроллеры не появляются при исправном железе |
| `KvXrInputSource` | `TryRightAimPose`, `TryLeftAimPose`, `TryAimPose`, `HasRightAim`, `HasLeftAim` | поза УКАЗАНИЯ (`aim`) — вдоль неё идёт луч; поза кисти (`grip`) остаётся для панели |
| `KvXrUiPointer` | поля `groupPanel`, `useEventSystemFallback`, `MaxGazeDownMeters`; методы `AimAlongPanel`, `HandleTriggerPress`, `HandleGazePress`, `ResetHoverAfterPress` | наведение на панель ГЕОМЕТРИЕЙ; смещение «луча взгляда» вниз ограничено 3 см (10 см смещали линию взгляда на 14°, и луч проходил ниже иконки) |
| `KvXrGroupPanel` | поля `handForwardMeters`, `handUpMeters`, `handMinDistanceFromEyes`, `handMaxDistanceFromEyes`, `poseRejectedLogged`; методы `TryAim`, `SetHoverIndex`, `TitleAt`, `PressIndex`, `IconsOnLevel`; списки `hovers`, `iconActions`, `iconTitles`, `currentOffsets` | панель ставится У КОНТРОЛЛЕРА (вперёд по указанию, разворот НА оператора), нажатие и подпись идут через геометрию, каждая строка журнала — доказательство |
| `KvXrRig` | вызов `KvXrOpenXrDiagnostics.Tick()` | строки о рантайме, расширении контроллеров и XR-устройствах |
| `KvHangarPlacement` | в описании прямо сказано, что `ApprovedOperatorLocalPosition` (0, 0, −29,20) **больше не используется**: зал ставится сдвигом всей лаборатории | старая пометка в журнале вводила в заблуждение |
| `KvXrHangarTools`, `KvXrHangarBuilder`, `KvXrInterfaceSetupTools`, `KvXrLightingTools` (Editor) | методы постановки зала, замера окружающей среды, постановки панели групп и сторожа, света шлема | правки сцены только редакторными инструментами ([S3.4]) |


---

## Источники

Все файлы `*.cs` из `Assets\` (кроме `Assets\_Recovery\`), сгруппированные по пространствам имён:

**`(глобальное пространство имён)`** (59 файлов):

- `Assets\_Project\01_Scripts\Core\FreeFlyCameraController.cs`
- `Assets\_Project\01_Scripts\Core\HDRPAutoLighting.cs`
- `Assets\_Project\01_Scripts\Core\InverseKinematics.cs`
- `Assets\_Project\01_Scripts\Core\RobotController.cs`
- `Assets\_Project\01_Scripts\Core\RobotSelfCollision.cs`
- `Assets\_Project\01_Scripts\Core\SCARAController.cs`
- `Assets\_Project\01_Scripts\Core\SixAxisAutoSetup.cs`
- `Assets\_Project\01_Scripts\Core\SixAxisController.cs`
- `Assets\_Project\01_Scripts\Editor\ConvertRobotMaterialsToHDRP.cs`
- `Assets\_Project\01_Scripts\Editor\DshDesktopUiDiag.cs`
- `Assets\_Project\01_Scripts\Editor\DshFeaturesDiag.cs`
- `Assets\_Project\01_Scripts\Editor\DshFullVerifyDiag.cs`
- `Assets\_Project\01_Scripts\Editor\DshScaraDiag.cs`
- `Assets\_Project\01_Scripts\Editor\DshScaraEightDiag.cs`
- `Assets\_Project\01_Scripts\Editor\DshScreenshotsDiag.cs`
- `Assets\_Project\01_Scripts\Editor\DshStage2Diag.cs`
- `Assets\_Project\01_Scripts\Editor\DshStage3Diag.cs`
- `Assets\_Project\01_Scripts\Editor\DshStage4Diag.cs`
- `Assets\_Project\01_Scripts\Editor\DshStageDiag.cs`
- `Assets\_Project\01_Scripts\Editor\DshUiStagesDiag.cs`
- `Assets\_Project\01_Scripts\Editor\HierarchyAutoRefresh.cs`
- `Assets\_Project\01_Scripts\Editor\HierarchyPhantomCleaner.cs`
- `Assets\_Project\01_Scripts\Editor\StandsMenu.cs`
- `Assets\_Project\01_Scripts\Input\GamepadInputProvider.cs`
- `Assets\_Project\01_Scripts\Input\InputDeviceType.cs`
- `Assets\_Project\01_Scripts\Input\InputManager.cs`
- `Assets\_Project\01_Scripts\Input\InputProvider.cs`
- `Assets\_Project\01_Scripts\Input\KeyboardController.cs`
- `Assets\_Project\01_Scripts\Input\KeyboardMouseInputProvider.cs`
- `Assets\_Project\01_Scripts\Input\MRInputProvider.cs`
- `Assets\_Project\01_Scripts\Input\VRInputProvider.cs`
- `Assets\_Project\01_Scripts\Integration\CollisionGuard.cs`
- `Assets\_Project\01_Scripts\Integration\RobotSelector.cs`
- `Assets\_Project\01_Scripts\Integration\TargetMarker.cs`
- `Assets\_Project\01_Scripts\Recording\TrajectoryPlayer.cs`
- `Assets\_Project\01_Scripts\Recording\TrajectoryRecorder.cs`
- `Assets\_Project\01_Scripts\Spatial\CalibrationTool.cs`
- `Assets\_Project\01_Scripts\Spatial\SpatialAnchorManager.cs`
- `Assets\_Project\01_Scripts\Trajectory\AimIndicator.cs`
- `Assets\_Project\01_Scripts\Trajectory\GhostView.cs`
- `Assets\_Project\01_Scripts\Trajectory\MotionExecutor.cs`
- `Assets\_Project\01_Scripts\Trajectory\PlanMetrics.cs`
- `Assets\_Project\01_Scripts\Trajectory\PointMoveHud.cs`
- `Assets\_Project\01_Scripts\Trajectory\TrajectoryExecutor.cs`
- `Assets\_Project\01_Scripts\Trajectory\TrajectoryFlowController.cs`
- `Assets\_Project\01_Scripts\Trajectory\TrajectoryMetricsPanel.cs`
- `Assets\_Project\01_Scripts\Trajectory\TrajectoryPlannerController.cs`
- `Assets\_Project\01_Scripts\Trajectory\WorkspaceVisualizer.cs`
- `Assets\_Project\01_Scripts\UI\MainMenu.cs`
- `Assets\_Project\01_Scripts\UI\SettingsData.cs`
- `Assets\_Project\01_Scripts\UI\SettingsMenu.cs`
- `Assets\_Project\01_Scripts\UI\SettingsSaver.cs`
- `Assets\_Project\01_Scripts\VR\ARInputProvider.cs`
- `Assets\_Project\01_Scripts\VR\PlacementController.cs`
- `Assets\_Project\01_Scripts\VR\PoseSelector.cs`
- `Assets\_Project\01_Scripts\VR\VRHandTracker.cs`
- `Assets\_Project\01_Scripts\VR\VRInputManager.cs`
- `Assets\TutorialInfo\Scripts\Editor\ReadmeEditor.cs`
- `Assets\TutorialInfo\Scripts\Readme.cs`

**`KazistovVvKinematics`** (1 файлов):

- `Assets\_Project\01_Scripts\Core\RobotDH.cs`

**`KazistovVvFeatures`** (62 файлов):

- `Assets\_Project\01_Scripts\Features\FeatureHub.cs`
- `Assets\_Project\01_Scripts\Features\FeatureStorage.cs`
- `Assets\_Project\01_Scripts\Features\KvActionLog.cs`
- `Assets\_Project\01_Scripts\Features\KvAutomation.cs`
- `Assets\_Project\01_Scripts\Features\KvCalibration.cs`
- `Assets\_Project\01_Scripts\Features\KvCameras.cs`
- `Assets\_Project\01_Scripts\Features\KvCaptures.cs`
- `Assets\_Project\01_Scripts\Features\KvCinematics.cs`
- `Assets\_Project\01_Scripts\Features\KvCollisionOptimizer.cs`
- `Assets\_Project\01_Scripts\Features\KvComparison.cs`

- `Assets\_Project\01_Scripts\Features\KvConstrainedPlanner.cs`
- `Assets\_Project\01_Scripts\Features\KvDynamicObstacles.cs`
- `Assets\_Project\01_Scripts\Features\KvEnergyOptimal.cs`
- `Assets\_Project\01_Scripts\Features\KvFeatureWindow.cs`
- `Assets\_Project\01_Scripts\Features\KvForceHeat.cs`
- `Assets\_Project\01_Scripts\Features\KvGraphics.cs`
- `Assets\_Project\01_Scripts\Features\KvGripper.cs`
- `Assets\_Project\01_Scripts\Features\KvHaptics.cs`
- `Assets\_Project\01_Scripts\Features\KvHealthMonitor.cs`
- `Assets\_Project\01_Scripts\Features\KvHeatmaps.cs`
- `Assets\_Project\01_Scripts\Features\KvInputKit.cs`
- `Assets\_Project\01_Scripts\Features\KvJointGraph.cs`
- `Assets\_Project\01_Scripts\Features\KvKeepOutZones.cs`
- `Assets\_Project\01_Scripts\Features\KvLocExtra.cs`
- `Assets\_Project\01_Scripts\Features\KvLocExtra2.cs`
- `Assets\_Project\01_Scripts\Features\KvLocExtra3.cs`
- `Assets\_Project\01_Scripts\Features\KvNetTools.cs`
- `Assets\_Project\01_Scripts\Features\KvOverlayKit.cs`
- `Assets\_Project\01_Scripts\Features\KvPathSmoothing.cs`
- `Assets\_Project\01_Scripts\Features\KvPayloadCalculator.cs`
- `Assets\_Project\01_Scripts\Features\KvPlannerLab.cs`
- `Assets\_Project\01_Scripts\Features\KvPlannerPerformance.cs`
- `Assets\_Project\01_Scripts\Features\KvPoseLibrary.cs`
- `Assets\_Project\01_Scripts\Features\KvPresentationMode.cs`
- `Assets\_Project\01_Scripts\Features\KvQuickStart.cs`
- `Assets\_Project\01_Scripts\Features\KvReportPdf.cs`
- `Assets\_Project\01_Scripts\Features\KvRobotExport.cs`
- `Assets\_Project\01_Scripts\Features\KvRobotImport.cs`
- `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs`
- `Assets\_Project\01_Scripts\Features\KvScenarioManager.cs`
- `Assets\_Project\01_Scripts\Features\KvSceneStudio.cs`
- `Assets\_Project\01_Scripts\Features\KvSessionManager.cs`
- `Assets\_Project\01_Scripts\Features\KvSingularityZones.cs`
- `Assets\_Project\01_Scripts\Features\KvSpatialAudio.cs`
- `Assets\_Project\01_Scripts\Features\KvStageHub.cs`
- `Assets\_Project\01_Scripts\Features\KvStageHub2.cs`
- `Assets\_Project\01_Scripts\Features\KvStageHub3.cs`
- `Assets\_Project\01_Scripts\Features\KvStageHub4.cs`
- `Assets\_Project\01_Scripts\Features\KvStartMenu.cs`
- `Assets\_Project\01_Scripts\Features\KvTeachPendant.cs`
- `Assets\_Project\01_Scripts\Features\KvTimeOptimal.cs`
- `Assets\_Project\01_Scripts\Features\KvToolKinematics.cs`
- `Assets\_Project\01_Scripts\Features\KvTrajMath.cs`
- `Assets\_Project\01_Scripts\Features\KvTutorial.cs`
- `Assets\_Project\01_Scripts\Features\KvUndoRedo.cs`
- `Assets\_Project\01_Scripts\Features\KvVariantKit.cs`
- `Assets\_Project\01_Scripts\Features\KvWaypointConstraints.cs`
- `Assets\_Project\01_Scripts\Features\KvWaypoints.cs`
- `Assets\_Project\01_Scripts\Features\KvWorkbenchWindow.cs`
- `Assets\_Project\01_Scripts\Features\KvXrInput.cs`
- `Assets\_Project\01_Scripts\Recording\KvRecordingService.cs`
- `Assets\_Project\01_Scripts\Recording\KvTrajectoryRecord.cs`

**`TrajectoryCore`** (15 файлов):

- `Assets\_Project\01_Scripts\Trajectory\CollisionWorld.cs`
- `Assets\_Project\01_Scripts\Trajectory\IkSolver.cs`
- `Assets\_Project\01_Scripts\Trajectory\KinematicsJacobian.cs`
- `Assets\_Project\01_Scripts\Trajectory\LaserAndPhantomManagers.cs`
- `Assets\_Project\01_Scripts\Trajectory\NarrowPhase.cs`
- `Assets\_Project\01_Scripts\Trajectory\Planner.cs`
- `Assets\_Project\01_Scripts\Trajectory\PoseValidator.cs`
- `Assets\_Project\01_Scripts\Trajectory\PostureControl.cs`
- `Assets\_Project\01_Scripts\Trajectory\PostureSelector.cs`
- `Assets\_Project\01_Scripts\Trajectory\ReachabilityOracle.cs`
- `Assets\_Project\01_Scripts\Trajectory\SafetyGate.cs`
- `Assets\_Project\01_Scripts\Trajectory\SelectionTypes.cs`
- `Assets\_Project\01_Scripts\Trajectory\StandBuilder.cs`
- `Assets\_Project\01_Scripts\Trajectory\ToolAlign.cs`
- `Assets\_Project\01_Scripts\Trajectory\TrajectoryTube.cs`

**`KazistovVvUI`** (34 файлов):

- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Camera\IdleCameraBrain.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KazistovVvUIManager.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvBindings.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvCommands.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvCursors.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvGamepadBridge.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvGamepadRouter.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvIcons.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvKeyboardNav.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvLayoutStore.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvLocalization.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvSettings.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvTheme.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvTooltip.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvUiStates.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Core\KvWidgets.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Data\ObjectSpawner.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Data\ProjectNode.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Data\RuntimeRegistry.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\CenterWindow.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvCommandPalette.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvContextMenu.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvDockPanel.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvGamepadHud.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvHotkeyView.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvMenuBar.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvPropertiesView.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvSelectionHighlight.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvSettingsView.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvStatusBar.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvToolbar.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvToolbarGroups.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvTreeView.cs`
- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\ScaraCableFollow.cs`

**`KazistovVvUI.EditorTools`** (1 файлов):

- `Assets\_Project\06_KazistovVv_UI\03_Scripts\Editor\KazistovVvMenu.cs`

**`KazistovVvTests`** (6 файлов):

- `Assets\_Project\08_Tests\Editor\KvCalibrationTests.cs`
- `Assets\_Project\08_Tests\Editor\KvEnergyOptimalTests.cs`
- `Assets\_Project\08_Tests\Editor\KvPayloadCalculatorTests.cs`
- `Assets\_Project\08_Tests\Editor\KvRobotExportTests.cs`
- `Assets\_Project\08_Tests\Editor\KvTimeOptimalTests.cs`
- `Assets\_Project\08_Tests\Editor\KvTrajMathTests.cs`

---

# ДОПОЛНЕНИЕ 21.10.2026 (§37): МОДЕЛИ КОНТРОЛЛЕРОВ, КИСТИ, ЖЕСТЫ, РУКА ЛУЧА, ПОДСТРОЙКА

## A. Новые типы

### A.1 `KazistovVvXr.KvXrControllerVisualMode` (перечисление)

| Значение | Число | Что значит |
|---|---|---|
| `Mesh` | 0 | готовый меш PICO 4 Ultra из SDK (`Resources/Controller/PICO 4U L|R`) + наши материалы (выбор владельца §37) |
| `Primitives` | 1 | модель из примитивов (цилиндр-рукоять, сфера-торец, диск-кольцо) |
| `Billboard` | 2 | плоский значок у кисти, всегда развёрнут к оператору |
| `Off` | 3 | не показывать ничего |

### A.2 `KazistovVvXr.KvXrControllerVisuals` (`MonoBehaviour`)

Создаётся в рантайме из `KvXrRig` (`Ensure(Transform parent, Camera camera)`) — в сцене его нет.

| Член | Тип | Назначение |
|---|---|---|
| `Ensure(parent, camera)` | `static KvXrControllerVisuals` | найти или создать (идемпотентно: второй модели на руку не появляется) |
| `ModeFromIndex(int)` | `static KvXrControllerVisualMode` | индекс настройки → режим; мусор → `Mesh` |
| `ModeName(KvXrControllerVisualMode)` | `static string` | название режима для журнала |
| `SdkModelPath(KvXrQuickHand)` | `static string` | `Controller/PICO 4U R` или `… L` |
| `IsPoseUsable(bool isTracked, Vector3 position)` | `static bool` | отказ: нет `isTracked`, поза нулевая, поза дальше 3 м |
| `Build(KvXrControllerVisualMode)` | `void` | перестроить модели по режиму (с откатом на примитивы) |
| `ShownCount`, `ModelDescription`, `Mode` | свойства | состояние для журнала и отчёта |
| Константы | `IgnoreRaycastLayer = 2`, `SdkModelRight`, `SdkModelLeft`, `MinPlausibleSizeMeters = 0.04`, `MaxPlausibleSizeMeters = 0.60`, `HideGraceSeconds = 0.30` | пороги, по которым модель принимается/скрывается |

### A.3 `KazistovVvXr.KvHandFrame` (структура)

Положение кисти, как его видит распознавание: `valid`, `wrist`, `palm`, `palmRotation`, `thumbTip`,
`indexProximal`, `indexTip`, `middleProximal`, `middleTip`, `ringProximal`, `ringTip`,
`littleProximal`, `littleTip`. Ни `GameObject`, ни подсистемы — поэтому собирается руками в тесте.

### A.4 `KazistovVvXr.KvGestureMath` (статический)

| Член | Что считает |
|---|---|
| `PalmScale(frame)` | ширина ладони (основания указательного ↔ мизинца), иначе «ладонь ↔ запястье»; 0 = позы нет |
| `IsUsable(frame)` | `valid` + ладонь ≥ 10 мм + все точки конечные числа |
| `IsFingerExtended(frame, proximal, tip)` | кончик дальше от ладони, чем основание, в 1,15 раза |
| `IsPinch(frame, maxRatio = 0.30)` | «большой ↔ указательный» ≤ 30 % ширины ладони |
| `IsOpenPalm(frame)` | все 4 пальца выпрямлены, большой отведён, не щипок |
| `IsIndexPoint(frame)` | выпрямлен только указательный, остальные согнуты, не щипок |
| Константы | `PinchRatio = 0.30`, `ExtendedRatio = 1.15`, `MinPalmScaleMeters = 0.010`, `ThumbAwayRatio = 0.80` |

### A.5 `KazistovVvXr.KvGestureRecognizer` (класс)

| Член | Что делает |
|---|---|
| `TryTick(KvHandFrame right, KvHandFrame left, float deltaTime, out KvGestureEvent result)` | один шаг; `true` — жест сработал (не чаще одного за кадр; правая рука первой) |
| `Reset()` | сбросить удержания, паузы и состояние нейтрали |
| `Detect(frame)` | `Pinch` → `OpenPalm` → `IndexPoint` → `None` (порядок важен: в начале щипка пальцы ещё выпрямлены) |
| `ActionFor(kind, hand)` | `Pinch+Right → Press`, `Pinch+Left → CloseFan`, `OpenPalm → TogglePanel`, `IndexPoint+Right → NextGroup`, `IndexPoint+Left → None` |
| `GestureName(kind)` | «щипок» / «раскрытая ладонь» / «указательный палец» |
| `HoldSecondsFor(kind)` | 0,15 с (щипок), 0,40 с (ладонь, палец) |
| Константы | `CooldownSeconds = 0.80`, `NeutralSeconds = 0.10` |

### A.6 `KazistovVvXr.KvGestureKind`, `KvGestureAction`, `KvGestureEvent`

`KvGestureKind`: `None`, `Pinch`, `OpenPalm`, `IndexPoint`.
`KvGestureAction`: `None`, `Press`, `TogglePanel`, `NextGroup`, `CloseFan`.
`KvGestureEvent`: `kind`, `hand`, `action`, `Name`, `HandName`.

### A.7 `KazistovVvXr.KvXrHandSource` (статический)

| Член | Назначение |
|---|---|
| `Provider` | `Func<KvXrQuickHand, KvHandFrame?>` — поставщик поз (ставит `KvXrHands`) |
| `TryGet(hand, out frame)` | поза кисти, если она есть и достоверна (иначе `false`, без исключений) |

### A.8 `KazistovVvXr.KvXrHands` (`MonoBehaviour`)

Создаётся в рантайме (`Ensure(parent, camera)`). Поднимает подсистему XR Hands
(`HandTracking.EnsureSubsystemInitialized`), читает 26 суставов, рисует их ОДНИМ мешем на руку.

| Член | Назначение |
|---|---|
| `TryReadFrame(hand, out KvHandFrame)` | прочитать позу кисти (false, если кисть не отслеживается или суставов нет) |
| `Describe()` | строка журнала `ОТСЛЕЖИВАНИЕ РУК: левая … · правая … · поза достоверна` |
| `RightTracked`, `LeftTracked`, `RightJointCount`, `LeftJointCount`, `SubsystemRunning` | состояние для журнала и отчёта |
| Поля настройки | `jointSizeMeters` (0,010), `fingertipSizeMeters` (0,016), `drawJoints`, `logOnChange` |

### A.9 `KazistovVvXr.KvXrGestures` (`MonoBehaviour`)

Создаётся в рантайме (`Ensure(parent)`). Один шаг распознавания за кадр; действие выполняет через
`KvXrUiPointer.HoveredIndex` и `KvXrGroupPanel` (нажатие иконки, закрытие веера, показ/скрытие
панели, следующая группа). Каждое срабатывание — строка `[KvXr] ЖЕСТ: «…» сработал · <рука> · <что сделано>`.

### A.10 `KazistovVvXr.KvXrTuning` (статический)

Подстройка чисел файлом `Application.persistentDataPath/kv_xr_tuning.txt` (на шлеме —
`/storage/emulated/0/Android/data/com.KazistovVv.app/files/kv_xr_tuning.txt`), перечитывается раз в
2 с. Формат: `ключ=значение` построчно, `#` — комментарий. Файл ПОВЕРХ настроек: он не заменяет
`PlayerPrefs` и ничего не пишет на диск.

| Ключ | Границы | Действие |
|---|---|---|
| `panel.radius` | 0,080…0,420 м | радиус панели групп |
| `panel.forward` | 0,050…0,900 м | вынос панели вперёд от контроллера (по умолчанию 0,12 м) |
| `panel.up` | −0,050…0,250 м | подъём панели над лучом |
| `panel.minDistanceEyes` | 0,100…1,200 м | ближайшая дистанция панели от глаз (по умолчанию 0,18 м); именно этот кламп держал панель в 0,35 м |
| `ray.hand` | `right`/`left`/`правая`/`левая` | рука луча |
| `visuals.mode` | `mesh`/`primitives`/`billboard`/`off` | чем показывать контроллеры |
| `gesture.enabled` | `1`/`0`/`вкл`/`выкл` | распознавание жестов |
| `sides.swap` | `1`/`0`/`поменять`/`как есть` (нет ключа — автоматика) | поменять стороны рук местами (§37, после «контроллеры всё ещё перепутаны») |

| Член | Назначение |
|---|---|
| `Parse(string)` | чистая функция разбора текста в `Dictionary<string,string>` |
| `TryParseFloat(raw, out value)` | разбор числа независимо от языка системы; `NaN`/∞ отвергаются |
| `ResolvePanelRadius/Forward/Up(map, fallback)`, `ResolveRayHand`, `ResolveVisuals`, `ResolveGestures` | чистые функции «значение из файла либо прежнее», с ограничением диапазона |
| `ResolveSidesSwap(map)` | `bool?`: `true` — поменять стороны, `false` — как есть, `null` — автоматика |
| `Tick()` | перечитать файл, если прошло 2 с; возвращает строку для журнала при изменении |
| `Reload()` | перечитать немедленно |
| `PanelRadiusMeters`, `PanelForwardMeters`, `PanelUpMeters`, `RayHand`, `VisualsMode`, `GesturesEnabled`, `FilePath`, `FileFound`, `KeyCount` | действующие значения и состояние |

## B. Изменения описанных ранее типов

### B.1 `KvXrGroupPanelLayout` — новые члены (прежние НЕ менялись)

| Член | Что считает |
|---|---|
| `DefaultPanelRadiusMeters = 0.100f` | радиус по умолчанию (выбор владельца §37) |
| `MinPanelRadiusMeters = 0.080f`, `MaxPanelRadiusMeters = 0.420f` | границы радиуса |
| `MinIconSizeMeters = 0.016f`, `MaxIconSizeMeters = 0.060f`, `MinFanIconSizeMeters = 0.024f` | границы размера иконки |
| `MinGapForSmallIcons = 0.008f`, `MaxGapMeters = 0.012f`, `GapRatio = 0.22f` | просвет между иконками |
| `ClampPanelRadius(float)` | ограничить радиус |
| `IconBudgetMeters(count, span, radius)` | сколько метров дуги на иконку |
| `GapForCount(count, span, radius)` / `IconSizeForCount(count, span, radius)` | просвет и размер иконки ОТ радиуса |
| `RadiusForIconSize(count, span, iconSize)` | какой радиус нужен, чтобы иконка была не мельче заданной |
| `FanRadiusMeters(iconCount, groupRadius)` | радиус веера (не даёт иконкам стать мельче 24 мм) |
| `NeighbourDistanceMeters(...)`, `IconsDoNotOverlap(...)` | проверка «иконки не слипаются» |
| `GroupOffsetsForRadius(groupCount, hand, radius)`, `FanOffsetsForRadius(iconCount, hand, radius)` | раскладка от заданного радиуса |
| `DescribeSizes(count, span, radius)` | строка `радиус 0.100 м · иконка 27 мм · просвет 8.0 мм` |
| `ShowsLabel(level, dwellSeconds)` | **изменено 21.10.2026**: подпись по 0,5 с удержания на ОБОИХ уровнях (раньше — только на уровне групп); разрешение владельца получено, тест `FanLevel_NeverShowsLabels` заменён на `FanLevel_ShowsLabelAfterDwell` |
| `TryHitIcon(..., float hitRadiusMeters = HitRadiusMeters)` | радиус попадания теперь параметр (панель передаёт 0,85 от фактического размера иконки) |

### B.2 `KvXrInputSource` — новые члены

| Член | Назначение |
|---|---|
| `RayHand` | рука луча (файл подстройки → настройка; по умолчанию правая) |
| `RayHandName` | «правая»/«левая» для журнала |
| `Controller(KvXrQuickHand)`, `HasHand(hand)` | контроллер конкретной руки |
| `TryRayAimPose`, `TryRayPose`, `RayTrigger`, `RayGrip`, `RayStick` | то же, что раньше, но для руки луча |
| `TryAimPose(hand, …)`, `TryPose(hand, …)` | позы конкретной руки |
| `TryTrackedPose(hand, …)` | поза с ОБЯЗАТЕЛЬНОЙ проверкой `isTracked` (для моделей: нулевая поза отвергается) |
| `ResolveSides()`, `UnlockSides()`, `DescribeSides()`, `DescribeHandedness()` | определение сторон рук: ручной `sides.swap` → метки `LeftHand`/`RightHand` → `handedness` → положение относительно головы; решение по 20 кадрам, с пересмотром при явном (≥ 5 см) разведении рук |
| `SidesLocked`, `SidesWereSwapped`, `ManualSidesActive` | состояние сторон для журнала и отчёта |

### B.3 `KvXrSettings` — новые члены

| Ключ / член | По умолчанию | Назначение |
|---|---|---|
| `KeyRayHand` = `KazistovVv.XR.Ray.Hand` · `GetRayHand/SetRayHand`, `RayHandFromIndex`, `RayHandLabel` | правая | рука луча |
| `KeyGroupPanelRadius` = `KazistovVv.XR.Panel.Radius` · `GetGroupPanelRadius/SetGroupPanelRadius` | 0,100 м | радиус панели групп |
| `KeyControllerVisuals` · `GetControllerVisualMode/SetControllerVisualMode` | `Mesh` | чем показывать контроллеры |
| `KeyGestures` · `GetGesturesEnabled/SetGesturesEnabled` | включены | распознавание жестов |
| `DescribePointer()` | — | строка `луч из ПРАВОЙ руки · панель у ЛЕВОЙ руки · радиус панели 0.100 м` |

### B.4 `KvXrGroupPanel` — новые члены

| Член | Назначение |
|---|---|
| `RadiusMeters` | действующий радиус (настройка → файл подстройки) |
| `DefaultHandForwardMeters = 0.30f`, `DefaultHandUpMeters = 0.04f` | значения по умолчанию для выноса/подъёма |
| перестройка на ходу | при изменении радиуса панель перестраивается и встаёт заново, печатая `радиус изменён на …` |

### B.5 `KvXrUiPointer` — новые члены

| Член | Назначение |
|---|---|
| `HoveredIndex` | номер иконки под лучом (−1 — ни одной); нужен жестам |
| строка `Указатель: луч из ПРАВОЙ руки …` | печатается один раз при старте и при смене руки |
| `источник КОНТРОЛЛЕР (правая рука)` | сторона в строке наведения |

### B.6 `KvXrLocomotion`

Телепорт читает позу и курок РУКИ ЛУЧА (`TryRayPose`, `RayTrigger`), а не жёстко правой: иначе при
смене руки курок нажимал бы иконку и одновременно телепортировал.

### B.7 `KvXrRig`

`Start()` вызывает `EnsureRuntimeComponents()`: создаёт `KvXrControllerVisuals`, `KvXrHands`,
`KvXrGestures` (все — идемпотентно, сцена не меняется) и печатает `DescribePointer()`.
`Update()` раз в 2 с читает файл подстройки (`KvXrTuning.Tick()`).

## C. Редакторные инструменты §37 (`KazistovVvXrEditor.KvXrS37Tools`)

| Метод | Что делает |
|---|---|
| `AddHandTrackingPackage()` | ставит `com.unity.xr.hands` 1.9.0 через менеджер пакетов и печатает зависимости |
| `ApplyHandTrackingForAndroid()` | включает фичу `HandTracking` для Android, выключает `MetaHandTrackingAim`, ставит `PICOProjectSetting.isHandTracking = true` (режим «контроллеры и кисти») |
| `VerifySdkControllerModels()` | проверяет БЕЗ ШЛЕМА: загружается ли меш SDK, габарит, коллайдеры, аниматоры, шейдеры материалов |
| `ReportS37State()` | сводка: версия пакета, фича кистей, `DescribePointer()`, размеры панели, путь файла подстройки |

## D. Тесты §37 (`Assets/_Project/08_Tests/Editor/KvXrS37Tests.cs`)

31 тест: радиус и размеры иконок (включая «иконки не слипаются» и неизменность прежнего API),
рука луча (по умолчанию правая, переключение, отдельная настройка панели), разбор файла подстройки и
ограничение значений, отказ от нулевой позы кисти, распознавание щипка/ладони/указательного пальца,
удержание, пауза, требование нейтрального положения, сторона руки в событии жеста, отказ от нулевой
позы контроллера и пути к мешам SDK.

---

# ДОПОЛНЕНИЕ 21.10.2026 (§38): СТРОГИЕ ЖЕСТЫ, «ЖЕСТ БЕЗ ДЕЛА», ДИАГНОСТИКА КИСТЕЙ

## E. Новое и изменённое в §38

### E.1 `KvGestureMath` — строгие пороги и условия места

| Член | Значение / смысл |
|---|---|
| `PinchTouching` | **0,85** (было 0,75) — «почти сведённые» пальцы больше не щипок |
| `PinchMinIndexCurl` / `PinchMaxIndexCurl` | **0,30 / 0,85** — указательный в этом диапазоне (кулак исключён) |
| `OpenCurl` | **0,15** (было 0,22) — палец считается прямым только при таком сгибе |
| `OpenSpread` | **0,10** — пальцы должны быть разведены |
| `OpenThumbCurl` | **0,45** — большой палец выпрямлен (иначе это хват) |
| `OpenMaxPinch` | **0,30** — ладонь не может быть одновременно щипком |
| `PointIndexCurl` / `PointOtherCurl` / `PointMaxPinch` | **0,20 / 0,75 / 0,40** |
| `MinForwardMeters` = **0,15**, `MaxDistanceFromHeadMeters` = **0,90** | «рука в поле зрения» |
| `PalmUpMinVertical` = **0,50** | насколько «вверх» смотрят пальцы для позы «ладонь поднята» |
| `IsHandInView(hand, headPosition, headRotation)` | рука впереди ≥ 0,15 м и не дальше 0,90 м от головы |
| `IsPalmUpPosture(frame)` | пальцы смотрят вверх (жест «стоп») — опущенная рука этому не удовлетворяет |

### E.2 `KvGestureActions` (новый статический класс)

| Член | Смысл |
|---|---|
| `IsUseful(action, hasTargetIcon, fanOpen, panelVisible)` | «жест без дела не считается»: нажатие — только при иконке под лучом; выход из веера — только при раскрытом веере; следующая группа — только при видимой панели; показ/скрытие панели — всегда |

### E.3 `KvGestureRecognizer` — новое

| Член | Смысл |
|---|---|
| `Detect(frame, headPosition, headRotation)` | распознавание с учётом МЕСТА руки и позы «ладонь вверх» |
| `TryTick(right, left, headPosition, headRotation, deltaTime, out result)` | то же, но по кадру целиком (так зовёт исполнитель жестов) |
| `PalmHoldSeconds` | **0,80** (было 0,60) |
| `PointHoldSeconds` | **0,60** (было 0,50) |
| `PinchHoldSeconds` = 0,20 · `CooldownSeconds` = 1,0 · `NeutralSeconds` = 0,15 | **не менялись** — закреплены тестами §37 |

### E.4 `KvHandDiagnostics` (новый статический класс)

| Член | Смысл |
|---|---|
| `MaxPlausibleDistanceMeters` = **1,20** | дальше этого кисть «нарисована не там, где рука» |
| `DistanceToCamera(hand, camera)` | расстояние в метрах |
| `IsPlausible(hand, camera)` | правдоподобно ли расстояние |
| `Describe(hand, camera)` | строка `запястье (…) · камера (…) · до камеры 0.49 м`, при превышении — `ВНИМАНИЕ: это не рука, а ошибка пространства` |

### E.5 Изменения в `KvXrHands`

* в строку `ОТСЛЕЖИВАНИЕ РУК` добавлена диагностика места кисти (запястье, камера, расстояние);
* частота печати ограничена: смена состояния — сразу, «просто числа» — не чаще раза в 2 с
  (до §38 за 25 минут набиралось **13 238** строк).

### E.6 Изменения в `KvXrGestures`

* вызывает распознавание с головой (место руки) и отбрасывает «жесты без дела»;
* каждое срабатывание печатает СВОИ числа: `ЖЕСТ: «щипок» сработал · правая рука · нажата иконка «…» · всего жестов N · сгибы 0.55/0.45/0.45/0.45 · щипок 0.92 · разведение 0.15`;
* ключ файла подстройки `gesture.muteWithController` (по умолчанию выключен) глушит жесты руки с контроллером.

## F. Тесты §38 (`Assets/_Project/08_Tests/Editor/KvXrS38Tests.cs`)

14 тестов: место руки (впереди/далеко/за спиной), пороги места числами, поза «ладонь вверх»,
расслабленная кисть не раскрытая ладонь, строгие пороги щипка и указательного пальца, «жест без
дела не считается» для всех действий, распознавание с учётом места, удлинённое удержание ладони,
диагностика кистей (расстояние, правдоподобие, строка журнала).

---

# ДОПОЛНЕНИЕ 23.10.2026 (§39): КУРСОРЫ, ВТОРОЙ ЛАЗЕР, СКЕЛЕТ КИСТЕЙ

## G. Новые типы §39 (`Assets/_Project/01_Scripts/Xr/KvXrCursor.cs`)

### G.1 `KvCursorState` (перечисление)

`Hidden` (луча нет) · `Idle` (луч есть, иконки нет) · `OverIcon` (луч навёл иконку).

### G.2 `KvXrCursor` (статический)

| Член | Значение / смысл |
|---|---|
| `RayMarkerMeters` = **0,014** | размер метки на конце луча |
| `FingerMarkerMeters` = **0,022** | размер метки у кончика указательного пальца |
| `State(rayActive, overIcon)` | `Hidden` / `Idle` / `OverIcon` |
| `RayLengthMeters(overIcon, distanceToHit, fullLength)` | попадание → до иконки; промах → **полная длина** (исправление «луч упирается в невидимую плоскость панели») |

### G.3 `KvXrSecondaryRayLogic` (статический)

| Член | Значение / смысл |
|---|---|
| `WidthMeters` = **0,005** | второй луч тоньше основного (8 мм) |
| `MarkerMeters` = **0,010** | метка на конце второго луча |
| `CanPress()` | **всегда false** — решение владельца «второй луч только указатель» |
| `SourceHand(rayHand)` | всегда ПРОТИВОПОЛОЖНАЯ рука руки луча |
| `LengthMeters(...)` | те же правила, что у основного луча |

### G.4 `KvHandBone` / `KvHandSkeleton` (структура и статический класс)

| Член | Значение / смысл |
|---|---|
| `BoneWidthMeters` = **0,004** | толщина «кости» |
| `JointSizeMeters` = **0,012** | размер точки сустава (для сравнения толщин) |
| `FingerPointerMeters` = **0,060** | «указательный» отрезок от кончика пальца |
| `JointCount` = 26, `MinJointIndex`/`MaxJointIndex` | границы суставов кисти без зависимости от пакета в тестах |
| `Bones` / `BoneCount` = **25** | запястье–ладонь (1) + большой палец (4) + по 5 на четыре пальца (20) |
| `IsValidJoint(int)`, `ToLocalIndex(int)` | проверка и перевод индекса сустава в локальный |
| `CountDrawableBones(bool[] jointPresent)` | сколько костей рисуемо: «нет сустава — нет кости» |

### G.5 `KvXrSecondaryRay` (`MonoBehaviour`)

Создаётся в рантайме (`Ensure(parent, camera)` из `KvXrRig`). Тонкий бледный луч из руки, которая не
является рукой луча; на конце метка 10 мм; **нажатия нет**. Выключается ключом файла подстройки
`ray.secondary=0`. Состояние: `CurrentLength`, `SourceHand`.

### G.6 Изменения в существующих типах (§39)

| Тип | Что добавлено |
|---|---|
| `KvXrUiPointer` | поле `headCamera`, `cursorSizeMeters` (14 мм), метка-курсор на конце луча (цвет: спокойный → зелёный над иконкой), `HideCursor()`; луч при промахе идёт на полную длину, при попадании — **в центр иконки** |
| `KvXrGroupPanel` | `IconWorldPosition(int index)` — центр иконки в мировых координатах (для курсора) |
| `KvXrHands` | отрисовка **скелета**: 26 точек + до 25 костей + указательный отрезок 6 см — **в одном меше** (по-прежнему 1 вызов отрисовки на руку) |
| `KvXrTuning` | ключ `ray.secondary` (`1`/`0`) и `ResolveSecondaryRay(map)`, свойство `SecondaryRayEnabled` |
| `KvXrRig` | создаёт `KvXrSecondaryRay`, передаёт камеру указателю |

## H. Тесты §39 (`Assets/_Project/08_Tests/Editor/KvXrS39Tests.cs`)

12 тестов: длина луча при промахе и попадании, состояние метки, размеры меток (14/22 мм),
второй луч не нажимает, источник второго луча — противоположная рука, длина/толщина второго луча,
выключение файлом подстройки, 25 костей скелета, корректность индексов суставов, отсутствие дублей,
«нет сустава — нет кости», толщины кости и точки, длина указательного отрезка.

---

# ДОПОЛНЕНИЕ 23.10.2026 (§40): РЕЖИМ КИСТЕЙ И ЭКРАНЫ ИНТЕРФЕЙСА

## I. Новые типы (`Assets/_Project/01_Scripts/Xr/KvXrMode.cs`)

### I.1 `KvXrControlMode` (перечисление)

`None` (управлять нечем) · `Controllers` (контроллеры в руках) · `Hands` (контроллеры отложены).

### I.2 `KvXrModeResolver` (статический)

| Член | Значение / смысл |
|---|---|
| `ControllerInHandMeters` = **0,20** | ближе этого «кисть ↔ контроллер» — контроллер в руке |
| `Resolve(handTracked, controllerTracked, distance)` | чистое правило режима |
| `ModeName(mode)` | «контроллеры» / «кисти» / «нет управления» |

### I.3 `KvXrMode` (статический, состояние кадра)

| Член | Смысл |
|---|---|
| `Right`, `Left`, `For(hand)`, `Current` | режим каждой руки и руки луча |
| `Description`, `LastDistance` | строка для журнала и экрана «Состояние»; последнее расстояние «кисть ↔ контроллер» |
| `Tick()` | пересчёт раз в кадр из `KvXrRig`; каждая смена режима — строка `[KvXr] РЕЖИМ: …` |

### I.4 `KvXrHandRay` (статический)

| Член | Значение / смысл |
|---|---|
| `MaxLengthMeters` = **1,5** | предел длины луча из пальца |
| `TryGetRay(frame, out origin, out direction)` | начало — кончик указательного, направление — от основания к кончику; нет кисти — нет луча |
| `LengthMeters(overIcon, distanceToHit, maxLength)` | попадание → до иконки, промах → предел |

### I.5 `KvXrHandPanelPlacement` (статический)

| Член | Значение / смысл |
|---|---|
| `ForwardMeters` = **0,12**, `UpMeters` = **0,04** | смещение панели от ладони |
| `TryGet(frame, out position, out forward)` | положение и направление панели по позе кисти |

### I.6 `KvXrSettingStep`, `KvXrStatusData`, `KvXrSettingsData`, `KvXrObjectInfo`

`KvXrSettingStep.Step/StepInt` — шаг с границами; структуры данных — входы экранов.

### I.7 `KvXrScreenModel` (статический)

`BuildStatus` · `BuildSettings` · `BuildObjects(names, selected)` · `BuildProperties(info)` ·
`BuildHelp` — чистые функции, формирующие строки экранов (проверяются тестами без шлема).

### I.8 Изменения в существующих типах (§40)

| Тип | Что добавлено |
|---|---|
| `KvXrPanelLevel` | уровни экранов: `StatusScreen`, `SettingsScreen`, `ObjectsScreen`, `PropertiesScreen`, `HelpScreen` |
| `KvXrGroupPanel` | `OpenScreen(level)`, `CloseScreen()`, `ScreenTitle(level)`, `IsScreen(level)`, `SelectObject(index)`; ряд входов в экраны внизу панели; режим кистей — панель у ладони; строки экранов как надписи без попадания лучом |
| `KvXrUiPointer` | `handRayLength` (1,5 м), ветка «луч из пальца» в режиме кистей, `AimAlongPanel(..., maxLength)` |
| `KvXrSecondaryRay` | в режиме кистей не рисуется |
| `KvXrSettings` | `KeySecondaryRay`, `GetSecondaryRay/SetSecondaryRay` (настройка, которую перекрывает файл `ray.secondary`) |
| `KvXrRig` | `KvXrMode.Tick()` каждый кадр |

## J. Тесты §40 (`Assets/_Project/08_Tests/Editor/KvXrS40Tests.cs`)

11 тестов: режим по расстоянию (5/19/25 см, отсутствие кисти/контроллера, порог числом), луч из
пальца (начало, длина 1,5 м, «нет кисти — нет луча»), панель у ладони (0,12/0,04, разворот по кисти),
содержимое экранов «Состояние» и «Настройки», шаги и границы настроек, список объектов с пометкой
выбранного и пустой список, свойства выбранного и «не выбран», справка со всеми четырьмя жестами.




## K. Правка после приёмки §40 (24.10.2026): цели луча на панели

ЗАЧЕМ. Владелец: «не приклеивается лазер к новым кнопкам внизу контроллера, что выстроены в ряд.
информация, яркость и тд». Кнопка на панели — это картинка на канвасе ПЛЮС смещение в списке целей
луча. Смещения раньше вписывались вручную в каждый построитель уровня, и для ряда экранов §40 их
забыли, поэтому луч проходил сквозь видимые кнопки.

### K.1 Новые члены `KvXrGroupPanel`

| Член | Смысл |
|---|---|
| `RayTargetCount` | сколько целей у луча на текущем уровне; обязано совпадать с `IconCount` |
| `RayTargetOffset(int index)` | смещение цели на канвасе, метры — позволяет проверить без шлема, какая кнопка под лучом |
| `pendingOffsets` (закрытое) | накопитель смещений, который заполняет `CreateIcon`; в конце `Rebuild()` становится `currentOffsets` |
| `ClearIcons()` | очищает в том числе `pendingOffsets` (иначе смещения копились бы через пересборки) |

### K.2 Правило, которое теперь держится кодом, а не памятью

1. Каждая интерактивная иконка создаётся ТОЛЬКО через `CreateIcon` — и он же добавляет её смещение
   в список целей. Нет иконки без цели, потому что это один и тот же вызов.
2. `Rebuild()` собирает `currentOffsets` из накопителя ЦЕЛИКОМ, после построения уровня
   (группы + ряд экранов; веер + центр; экран + его кнопки).
3. Порядок списка совпадает с порядком `icons`/`hovers`/`iconActions`/`iconTitles`, поэтому
   `PressIndex(index)` нажимает именно наведённую кнопку (`TryAim` → `SetHoverIndex` → `PressIndex`).
4. Если длины всё же разошлись — `Debug.LogError` «ЦЕЛИ ЛУЧА РАСХОДЯТСЯ С ИКОНКАМИ: целей N · иконок M».
5. В веере команды раскладываются до `hitOffsets.Length - 1`: последнее смещение в `FanHitOffsets` —
   центр (0,0), это иконка «Закрыть», а не команда.

### K.3 Тесты (`Assets/_Project/08_Tests/Editor/KvXrPanelTargetTests.cs`), 5 штук

| Тест | Что доказывает |
|---|---|
| `GroupLevel_ScreenRowIsHittable` | ряд экранов внизу панели присутствует в целях своими пятью смещениями (−0,150 / −0,075 / 0 / +0,075 / +0,150 м; −0,235 м) — 13 иконок, 13 целей |
| `SettingsScreen_StepperButtonsAreHittable` | «−», «+», «Дальше», «Закрыть» экрана «Настройки» ловятся лучом |
| `EveryScreen_CloseButtonIsHittableAndCountsMatch` | на всех пяти экранах иконок столько же, сколько целей, и «Закрыть» ловится |
| `FanLevel_HasCenterExitTarget` | центральная иконка веера (0,0) — цель (повторение дефекта 21.10.2026 исключено) |
| `RayTargets_DoNotOverlap` | две цели не стоят в одном месте: иначе луч нажимает соседнюю кнопку |

---

## M. §42 (25.10.2026): окна компьютера в шлеме, столы, поток этапов

Приложение к справочнику. Описывает то, что добавлено волной §42 — по одному разделу на файл.

### M.1 `KvXrDesktopWindows` — таблица «id команды → экран шлема»

Соответствие хранится ДАННЫМИ (`KvXrDesktopWindowInfo[]`), а не ветками `switch`: новая команда
компьютера не может молча открыть не тот экран.

| Член | Что делает |
|---|---|
| `int Count` | сколько команд-окон знает таблица (**50**) |
| `bool TryGet(string id, out KvXrDesktopWindowInfo info)` | экран, вкладка и заголовок окна для команды; `false` — команда окна не открывает, её надо выполнить |
| `bool IsWindowCommand(string id)` | то же без структуры |
| `List<string> AllIds()` | все id команд-окон (тест полноты) |
| `int ScreenCount()` | сколько разных экранов задействовано (**11**) |
| `string Describe()` | строка журнала о таблице |

`KvXrDesktopWindowInfo`: `CommandId`, `Screen` (какой экран), `Tab` (номер вкладки), `Title` (заголовок).

### M.2 `KvXrDesktopTabs` — вкладки новых экранов

| Член | Что делает |
|---|---|
| `int CountOf(screen)` | вкладок у экрана (0 — без вкладок) |
| `string TabTitle(screen, i)` | короткая подпись кнопки |
| `string KeyOf(screen, i)` | ключ вкладки — **всегда латиницей** (`record`, `waypoints`, `interface`…): по нему ищутся кнопки вкладки |
| `string TitleOf(screen, i)` | полный заголовок: «Верстак · Точки» |
| `int PageCount(tabs)`, `PageOf(tab)`, `FirstOnPage(page)` | листание по 4 вкладки |
| `bool Fits(screen, halfWidth)` | ряд вкладок влезает в окно (0,58 м ≤ 0,62 м) |

Числа: `VisibleSlots = 4`, `StepMeters = 0,13`. Всего вкладок в новых экранах — **41**.

### M.3 `KvXrScreenRows` + `KvXrScreenData` — содержимое вкладок

`List<string> Build(screen, tab, data)` — **чистая функция**: что попадёт в строки, решает она, а рисует
панель. Правило: **пустого списка не бывает** — если данных нет, первая строка честно называет причину
(`KvXrScreenRows.NoData(reason)`).

`KvXrScreenData` — одна структура на все экраны: робот, версия, язык, масштаб, пресет графики, кадры,
углы суставов, журнал, списки (позы/сценарии/сессии/записи/зоны), верстак (груз, плоскость, точки,
ограничения, узлы RRT, прокси, силы), пульт, клавиши.

### M.4 `KvXrTableKinds` + `KvReachMath` — числа столов

| Константа / метод | Значение |
|---|---|
| `KvXrTableKinds.SquareWidth` / `SquareDepth` / `TopHeight` | **0,72** / **0,48** / **0,98** м (решение владельца В2) |
| `KvXrTableKinds.SquareDiagonal` | **0,8653** м («диагональ 0,87 м») |
| `KvXrTableKinds.Radius(kind, reach)` | квадратный — половина диагонали; авто — `KvReachMath.AutoRadius` |
| `KvReachMath.ChainLength(pivots, tcp)` | вылет = сумма расстояний между пивотами + отрезок до TCP (не меньше 0,05 м) |
| `KvReachMath.AutoRadius(chain)` | **0,95 × вылет + 0,10 м** → 2,3496 м (6-осевой) и 0,7774 м (SCARA) |
| `KvXrTableKinds.DescribeCreate(kind, reach)` | строка журнала с числами |

Одна формула вылета на всю программу: её же используют сингулярностные зоны — второй формулы в проекте нет.

### M.5 `KvXrStartMenuRows` + `KvXrStartRowAction` — строки меню «Стол»/«Робот»

`Tables(existing)` / `Robots(existing)` возвращают состав строк, `ActionFor(section, row, tables, robots)` —
действие строки. Состав и действие считаются **одной функцией** (раньше это были два независимых
`switch`, и они могли разойтись). Потолок панели — `KvXrStartMenuLayout.MaxContentRows` (7).

`KvXrStartRowAction`: `SelectObject`, `CreateSquareTable`, `CreateAutoTable`, `CreateSixAxisRobot`,
`CreateScaraRobot`, `RunStand`, `SwitchRobot`, `None`.

### M.6 `KvXrSceneEmpty` (компонент) + `KvXrSceneEmptyState` — пусто при запуске

`[DefaultExecutionOrder(500)]` — после начинки (−400) и после MR-слоя. Гасит корни со «стенд» в имени
вместе с их роботами, повторяет гашение раз в секунду, печатает один раз:
`[KvXr] СЦЕНА: выключено роботов N · столов M — как в ПК до добавления`.

Сцена `XrMain.unity` при этом **не меняется**; правило `HidesVirtualStands`, закреплённое тестом, не тронуто.

### M.7 `KvXrFlowBridge` (компонент) + `KvXrFlowRules` — поток этапов в шлеме

Создаёт на камере шлема **тот же** `TrajectoryFlowController`, что на компьютере, и кормит его публичным
`UpdateAim(...)`. Правила: лазеры потока выключены (в шлеме свои), подтверждение — только вне интерфейса,
камеру поток не двигает. Переключатель без пересборки — `KazistovVv.XR.FlowAim`.

| Член | Что делает |
|---|---|
| `bool ShouldFeedConfirm(overInterface, triggerDown)` | курок подтверждает точку только вне интерфейса |
| `bool ShouldFeedAim(overInterface)` | прицел кормится, когда луч не над панелью/окном/меню |
| `string Describe(flowCreated, robot, visible)` | `[KvXr] ПОТОК: создан на камере шлема · робот «…» · визуализаций видимо N` |

### M.8 `KvXrVisuals` — почему визуализаций не видно

`Reason(flowCreated, robotBound, toggledOn, hasGeometry, onDesktopCanvas)` возвращает одну из причин:
`поток этапов не создан` / `робот не привязан` / `выключено тумблером` / `в этой позе показывать нечего` /
`панель на десктопном канвасе` / `видно`.
`Describe(visible, reason)` — `[KvXr] ЗОНЫ: видимо N · причина скрытия «…»`.

### M.9 `KvXrObjectFactory` — создание столов и роботов

| Член | Что делает |
|---|---|
| `float ReachOf(RobotController)` | вылет руки по пивотам сцены (через `KvReachMath`) |
| `RobotController ActiveRobot()` | первый активный робот |
| `Vector3 SpotInFrontOf(camera, distance)` | точка на полу впереди оператора (по умолчанию 1,20 м) |
| `float YawTowards(from, target)` | разворот стола на оператора |
| `GameObject CreateTable(kind, point, yaw, reach)` | стол нужного вида (через `StandBuilder`) |
| `RobotController CreateRobot(scara, position, yaw)` | копия сценного эталона через `ObjectSpawner.SpawnRobot` |

### M.10 `KvXrVoiceStatus` — честная строка про голос

`Available(hasAppId, hasMic)` и `Line(hasAppId, hasMic)`: без appID — «голос недоступен: нет appID PICO
(клавиатура и колесо работают)»; без разрешения — своя строка про `RECORD_AUDIO`.

### M.11 Изменения в `KvXrGroupPanel` и `KvXrWindowHost`

| Член | Что делает |
|---|---|
| `bool OpenDesktopWindow(string commandId)` | открыть окно компьютера по команде; `false` — команда окна не открывает |
| `void OpenDesktopTab(int index)` | переключить вкладку окна (рядом с прежним `OpenScreen`) |
| `KvXrPanelLevel WindowScreen` / `OpenedWindowScreen` | какой экран-окно показан |
| `string WindowCommandId` | какой командой окно открыто (журнал и дедупликация) |
| `KvXrPanelLevel.DesktopWindow = 16` | уровень «окно компьютера» (не экран панели у руки: своя плита 0,62 × 0,52 м) |
| `KvXrWindowHost.Open(screen, title, key)` | окно с ключом: два окна «Верстака» живут рядом |
| `KvXrWindowHost.FindOpen(screen, key)` / `KeyOf(window)` | поиск и ключ открытого окна |

### M.12 Тесты этой волны (`KvXrS42Tests.cs`, 20 штук)

`Windows_EveryWindowCommandHasScreen`, `Windows_OpenCreatesWindowNotCommand`, `Windows_KeyIsUniquePerCommand`,
`Windows_EveryTabHasRows`, `Windows_TabStripIsHittable`, `Windows_ScreensDoNotCollideWithFixedRow`,
`Tables_ThereAreTwoKinds`, `AutoTable_RadiusEqualsRobotReach`, `SquareTable_IsMuchSmallerThanAuto`,
`SquareTable_MatchesPcNumbers`, `Reach_ChainLengthIsSumOfSegments`, `Scene_StandsAreRecognisedByName`,
`Scene_EmptyLineHasNumbers`, `Menu_StandAndRobotRowsMatchActions`, `Zones_HiddenVisualizerAlwaysNamesReason`,
`Zones_DiagnosticLineHasNumbers`, `Flow_ConfirmIsNotFedOverInterface`, `Flow_DescribeNamesRobotAndCount`,
`Voice_WithoutAppIdSaysUnavailable`, `Scenes_UntouchedByThisWave`.
