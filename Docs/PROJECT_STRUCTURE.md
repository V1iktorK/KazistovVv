# PROJECT_STRUCTURE.md — структура папок и подсистем

_Обновлено: 03.10.2026 (сессия §28, после переноса проекта в `C:\KazistovVv_Build\`)._
_Назначение: быстро найти, где что лежит, и понять, за что отвечает каждая подсистема._
_Это тот документ, которого в проекте не хватало: карта по факту, а не архивный снимок._

**Текущий путь проекта: `C:\KazistovVv_Build\`** (латинский путь обязателен для сборки под Android —
см. `Docs/PROBLEMS.md` Б6/А6). Старая папка `…\новое пространство\KazistovVv` пока сохранена.

---

## 1. Корень проекта

| Папка / файл | Что это | Трогать? |
|---|---|---|
| `Assets/` | всё содержимое проекта: сцены, скрипты, модели, материалы, тесты | да, это рабочая зона |
| `Packages/manifest.json` | список зависимостей Unity (HDRP, URP, XRI, OpenXR, Input System, Test Framework) | да, но осторожно |
| `Packages/packages-lock.json` | зафиксированные версии зависимостей | генерируется Unity |
| `ProjectSettings/` | настройки проекта: качество, графика, физика, Android, XR | да, но каждая правка — с проверкой |
| `Docs/` | документация проекта (см. §7) | да |
| `Tools/` | служебные скрипты PowerShell: гейт «чистой консоли», сборка, прогоны | да |
| `Builds/` | выход сборки (APK/EXE) | генерируется |
| `PROJECT_CONTEXT.md` | **главный журнал проекта**: все сессии §0–§28 | да, всегда дополняется |
| `KazistovVv.sln`, `KazistovVv.slnx`, `*.csproj` | файлы решений для IDE | генерируются Unity |
| `.gitignore`, `.gitattributes`, `ignore.conf` | правила version control (git и Plastic) | да, осторожно |
| `.git/`, `.plastic/` | история версий (git-репозиторий и Unity VCS) | не трогать вручную |
| `.vscode/`, `.claude/`, `.codebuddy/`, `.gigacode_vsc/`, `.opencode/` | настройки редакторов и ИИ-инструментов | по желанию |
| `Library/`, `Temp/`, `obj/`, `Logs/`, `UserSettings/` | **производные** каталоги: кэш импорта, временные файлы, логи, настройки редактора | **никогда не коммитить**; удаление = долгая переимпортация |

---

## 2. `Assets/` — что где

| Папка | Содержимое | Откуда |
|---|---|---|
| **`_Project/`** | **ВСЁ СВОЁ**: сцены, скрипты, интерфейс, тесты | создано в этом проекте |
| `Premises/`, `ROOMS/` | модель помещения (ангар HQ Hangar) | сторонний пакет ассетов |
| `TABLES/` | модели столов | сторонний пакет |
| `Robots/` | модели роботов | сторонний пакет |
| `Oculus/`, `XR/`, `XRI/`, `CompositionLayers/` | конфигурация XR: лоадеры, настройки OpenXR, композиционные слои | Unity XR |
| `Samples/` | примеры из пакетов (XR Interaction Toolkit) | Unity |
| `Settings/` | ассеты пайплайнов рендера: `PC_RPAsset` (HDRP), `Mobile_RPAsset` (URP) | HDRP/URP-шаблон |
| `HDRPDefaultResources/` | ресурсы HDRP по умолчанию | HDRP-шаблон |
| `Plugins/` | нативные библиотеки (Android, Roslyn) | пакеты |
| `StreamingAssets/` | **данные, читаемые без перекомпиляции**: словари локализации, настройки, модели роботов | проект |
| `Resources/` | ассеты, загружаемые по имени во время работы | проект |
| `TutorialInfo/` | обучающий пример Unity | шаблон (можно удалить) |

---

## 3. `Assets/_Project/` — ядро проекта

| Папка | Файлов | Назначение |
|---|---|---|
| `00_Scenes/` | 12 | **сцены**: `MainScene` (десктоп, рабочая) и `XrMain` (VR/MR для шлема) |
| `01_Scripts/` | 329 | **весь код проекта** — см. §4 |
| `06_KazistovVv_UI/` | 80 | **интерфейс** (оболочка, панели, темы, локализация) — см. §5 |
| `08_Tests/` | 21 | **тесты**: `Editor/` (EditMode, NUnit) и `PlayMode/` (сцена). В §29 добавлен **`KvRenderPipelineTests.cs`** — 17 тестов конвейера рендера и «текстур без ошибок» |
| `02_Prefabs/` | 4 | префабы |
| `03_Models/`, `04_Materials/`, `05_Textures/`, `06_Animations/`, `07_Audio/` | по 2–6 | ресурсы сцены. В §29 в `04_Materials/URP/` добавлены **7 URP-копий материалов XR-сцены** (шлем); оригиналы HDRP остались нетронутыми |
| `Docs/` | 2 | план алгоритма траекторий |
| `02_Editor/`, `06_Lighting/`, `08_Resources/`, `08_Shaders/`, `09_Data/`, `09_Shaders/`, `10_StreamingAssets/` | **0** | пустые каталоги — наследие структуры, назначение не реализовано |

---

## 4. `Assets/_Project/01_Scripts/` — код по подсистемам

### 4.1 `Core/` (9 файлов) — роботы и камера

| Файл | За что отвечает |
|---|---|
| `RobotController.cs` | базовый контроллер робота: суставы, скорость, сборка |
| `SixAxisController.cs` | **6-осевой манипулятор**: прямая/обратная кинематика, самоколлизии, границы суставов (самый крупный файл ядра, 40 КБ) |
| `SCARAController.cs` | **SCARA**: кинематика, ход Z, работа у столешницы |
| `SixAxisAutoSetup.cs` | автоопределение суставов робота в сцене |
| `RobotDH.cs` | параметры Денавита–Хартенберга, прямые вычисления |
| `InverseKinematics.cs` | решатель обратной задачи (CCD) |
| `RobotSelfCollision.cs` | столкновения робота с самим собой |
| `FreeFlyCameraController.cs` | **свободная камера десктопа**: полёт, прицел, лассо, колесо, телеоперация (110 КБ — крупнейший файл проекта) |
| `HDRPAutoLighting.cs` | автоматический свет HDRP |

### 4.2 `Trajectory/` (25 файлов) — планирование и движение

| Файл | За что отвечает |
|---|---|
| `Planner.cs` | **планировщик траекторий** (RRT/BiRRT, время-оптимальная перепараметризация) |
| `CollisionWorld.cs` | **мир коллизий**: препятствия, капсульные цепочки, минимальные расстояния |
| `NarrowPhase.cs` | точные проверки пересечений |
| `SafetyGate.cs` | **допуск движения**: лимиты суставов, скорости, зоны |
| `MotionExecutor.cs` | **единственная точка входа любого движения** (импульс, S-профиль, плотность сэмплов) |
| `TrajectoryExecutor.cs` | исполнение плана во времени |
| `TrajectoryFlowController.cs` | **точка правды о роботе**: поток состояний, выбранный робот, варианты |
| `PoseValidator.cs` | проверка поз (лимиты, сингулярности) |
| `IkSolver.cs`, `KinematicsJacobian.cs`, `PostureControl.cs`, `PostureSelector.cs` | кинематика: обратная задача, якобиан, выбор позы |
| `ReachabilityOracle.cs` | зона достижимости (для визуализации) |
| `WorkspaceVisualizer.cs` | **отрисовка зоны достижимости** роботов |
| `CollisionWorld` / `GhostView.cs` | фантомы (копии робота для предпросмотра) |
| `LaserAndPhantomManagers.cs` | лазеры и фантомы |
| `AimIndicator.cs`, `PointMoveHud.cs`, `TrajectoryTube.cs` | визуальные подсказки: прицел, HUD перемещения точки, трубка траектории |
| `PlanMetrics.cs`, `TrajectoryMetricsPanel.cs` | метрики траектории (время, длина, энергия) |
| `StandBuilder.cs` | **построение стендов**: столы 14,4 × 9,6 и роботы на них |
| `ToolAlign.cs` | выравнивание инструмента к поверхности |
| `TrajectoryPlannerController.cs` | мёртвый код (в сцене не используется) |
| `SelectionTypes.cs` | типы выделения |

### 4.3 `Features/` (66 файлов) — прикладные возможности

Сгруппированы по назначению (полный список — в конце раздела):

| Группа | Файлы |
|---|---|
| **Каркас** | `FeatureHub.cs` (единый узел служб), `FeatureStorage.cs` (сохранение), `KvStageHub.cs`…`KvStageHub4.cs` (этапы ТЗ), `KvWorkbenchWindow.cs`, `KvFeatureWindow.cs` |
| **Планирование и траектории** | `KvConstrainedPlanner`, `KvPathSmoothing`, `KvTimeOptimal`, `KvEnergyOptimal`, `KvComparison` (стенд сравнения), `KvPlannerLab`, `KvPlannerPerformance`, `KvTrajMath`, `KvWaypoints`, `KvWaypointConstraints`, `KvSingularityZones`, `KvKeepOutZones`, `KvDynamicObstacles`, `KvCollisionOptimizer` |
| **Робот и инструмент** | `KvGripper` (гриппер), `KvToolKinematics`, `KvPayloadCalculator` (нагрузка), `KvCalibration` (мастер калибровки TCP), `KvRobotExport` (KRL/KAREL/RAPID), `KvRobotImport` (URDF/STEP) |
| **Безопасность** | `KvSafetyTools` (имитация отказов, проверка перед пуском), `KvMannequins` (манекены-люди), `KvHealthMonitor` |
| **Интерфейс и подача** | `KvGraphicsUi`/`KvGraphicsModel`/`KvGraphicsService` (раздел «Графика»), `KvOverlayKit`, `KvInputKit`, `KvVariantKit` (варианты траекторий), `KvTeachPendant` (виртуальный пульт), `KvPoseLibrary` (позы), `KvQuickStart`, `KvStartMenu`, `KvTutorial`, `KvPresentationMode`, `KvScenarioManager`, `KvSessionManager`, `KvUndoRedo`, `KvActionLog` (журнал) |
| **Показ и кино** | `KvCameras` (несколько камер, PiP), `KvCinematics`, `KvCaptures` (скриншоты, видео), `KvSceneStudio` (окружение/свет/материалы), `KvReportPdf`, `KvHeatmaps`, `KvForceHeat`, `KvJointGraph` |
| **Сеть** | `KvNetTools` (совместная работа, веб-дашборд, UDP-пульт) |
| **XR-сервисы** | `KvXrInput.cs` (1255 строк: голос, руки, взгляд, фовеальное), `KvHaptics` (вибрация), `KvSpatialAudio` (3D-звук) |
| `Features/` | 67 | прикладные возможности — см. §4.3 (в §29 добавлена фабрика `KvRenderPipelineShaders.cs`) |
| **Фабрика рендера (§29)** | `KvRenderPipelineShaders.cs` — **один источник правды по шейдерам**: определяет активный конвейер (URP для шлема / HDRP для компьютера), выбирает Lit/Unlit-шейдер и запасные варианты ТОГО ЖЕ конвейера, кладёт цвет и текстуру в правильные для конвейера свойства. Прямое `Shader.Find("HDRP/…")` в коде запрещено — на это есть тест-«сторож» |
| **Локализация** | `KvLocExtra.cs`…`KvLocExtra4.cs` (строки на 7 языков) |
| **Автоматизация** | `KvAutomation` (макросы, деревья поведения) |

### 4.4 `Input/` (8 файлов) — ввод

| Файл | За что отвечает |
|---|---|
| `InputManager.cs` | **диспетчер устройств**: выбирает активного поставщика (клавиатура/VR/геймпад/MR/AR), отдаёт проекту единые действия |
| `InputProvider.cs` | абстрактный поставщик ввода (базовый класс) |
| `KeyboardMouseInputProvider.cs` | клавиатура и мышь |
| `GamepadInputProvider.cs` | геймпад |
| **`VRInputProvider.cs`** | **ввод от шлема** (луч, курок, грип, стики) — в §28 переписан из заглушки |
| `MRInputProvider.cs` | MR-ввод — **заглушка** (не нужна, пока одна XR-сцена) |
| `KeyboardController.cs`, `InputDeviceType.cs` | вспомогательное |

### 4.5 Остальные подсистемы

| Папка | Файлов | Назначение |
|---|---|---|
| `Xr/` | **13** | **XR-слой §28**: риг, ввод, перемещение, панель, MR-комната, настройки (см. §4.6) |
| `Editor/` | 21 | редакторские инструменты и **диагностика `Dsh*`** (прогоны в batch-mode). В §29 добавлен **`KvRenderPipelineTools.cs`** — отчёт о конвейерах, аудит материалов, переключение глобального умолчания графики, начинка XR-сцены, перевод материалов на URP (меню `Tools/KazistovVv Рендер/`) |
| `VR/` | 5 | старые VR-заготовки: `VRInputManager` (под Oculus SDK), `VRHandTracker`, `PoseSelector`, `PlacementController`, `ARInputProvider` |
| `Recording/` | 4 | запись и проигрывание траекторий, запись видео |
| `Integration/` | 3 | связка: `RobotSelector` (выбор робота), `CollisionGuard`, `TargetMarker` |
| `Spatial/` | 2 | `SpatialAnchorManager` (пространственные якоря), `CalibrationTool` |
| `UI/` | 4 | старые настройки: `SettingsData` (ScriptableObject), `SettingsMenu`, `SettingsSaver`, `MainMenu` |

### 4.6 `Xr/` — XR-слой (появился в §28)

| Файл | За что отвечает |
|---|---|
| `KvXrRigConfig.cs` | конфигурация рига: уровень глаз по росту, масштаб мира, стерео, отсчёт от пола |
| `KvXrUiPanel.cs` | геометрия плоской панели: расстояние, углы, перевод пикселей канваса в метры |
| `KvXrMrPlacement.cs` | стол и комната владельца, ограничение смещения, проверки «помещается» |
| `KvXrSettings.cs` | настройки XR (PlayerPrefs): режим перемещения, скорости, слои MR |
| `KvXrSettingsChanged.cs` | шина оповещения о смене настроек |
| `KvXrInputSource.cs` | чтение головы и контроллеров через Input System |
| `KvXrLocomotion.cs` | **телепорт + плавный ход + поворот шагами** |
| `KvXrUiPointer.cs` | **луч по интерфейсу**: наведение, нажатие, приоритет над телепортом |
| `KvXrPanelAnchor.cs` | перевод канваса проекта в мировое пространство |
| `KvXrRig.cs` | камера шлема: проверка OpenXR и стерео, целевые кадры, масштаб глаза |
| `KvXrCameraGuard.cs` | снимает с камеры шлема десктопные сервисы (облёт простоя) |
| `KvXrMrRoom.cs` | MR: робот на реальный стол, сетки зон по тумблерам |
| `KvXrSceneContent.cs` | начинка XR-сцены: столы и роботы через `StandBuilder`. **В §29 компонент наконец добавлен в `XrMain.unity`** (до этого класс существовал, а сцена была пустой) |

---

## 5. `Assets/_Project/06_KazistovVv_UI/` — интерфейс (80 файлов)

Оболочка в стиле FreeCAD строится **кодом во время работы** (никаких префабов).

| Папка | Файлов | Назначение |
|---|---|---|
| `03_Scripts/Core/` | 17 | **ядро интерфейса**: `KazistovVvUIManager` (176 КБ — сборка всей оболочки), `KvWidgets` (эталонные виджеты), `KvTheme`, `KvIcons`, `KvMouseCursor` (единый источник правды курсора), `KvVirtualCursor`, `KvBindings` (реестр горячих клавиш), `KvKeyboardNav`, `KvCommands`, `KvSettings`, `KvLocalization`, `KvLayoutStore`, `RuntimeRegistry` |
| `03_Scripts/Zones/` | 15 | **области экрана**: `KvMenuBar` (меню), `KvToolbar` + `KvToolbarGroups` (тулбар, 8 рядов), `KvTreeView` (дерево моделей), `KvPropertiesView` (свойства), `KvStatusBar` (статус), `KvSettingsView` (настройки), `KvDockPanel` (панели-доки), `KvContextMenu`, `KvTooltip`, `KvHotkeyView`, `KvCommandPalette`, `KvUiStates`, `KvGamepadHud`, `ScaraCableFollow` |
| `03_Scripts/Data/` | 3 | `ObjectSpawner` (размещение объектов), `ProjectNode`, `KvSelectionHighlight` |
| `03_Scripts/Camera/` | 1 | `IdleCameraBrain` (облёт камеры в простое — в VR отключается сторожем) |
| `03_Scripts/Editor/` | 1 | меню «Создать интерфейс в сцене» |

---

## 6. Данные и конфигурация

| Путь | Что содержит | Меняется без перекомпиляции? |
|---|---|---|
| `Assets/StreamingAssets/kazistovvv_settings.json` | настройки приложения | да |
| `Assets/StreamingAssets/kazistovvv_graphics.json` | пресеты графики под железо | да |
| `Assets/StreamingAssets/kazistovvv_i18n/*.json` | **7 языков** (ru, en, zh, es, de, fr, ja) | да |
| `Assets/StreamingAssets/robots/*` | образцы URDF и STEP для импорта | да |
| `ProjectSettings/QualitySettings.asset` | **уровни качества**: «Mobile» (URP, для шлема) и «PC» (HDRP, для компьютера) | только с проверкой |
| `ProjectSettings/GraphicsSettings.asset` | **глобальное умолчание рендера** (сейчас HDRP) — именно оно блокирует сборку под Android, см. `Docs/PROBLEMS.md` Б1 | только осознанно |
| `ProjectSettings/EditorBuildSettings.asset` | список сцен сборки: сначала `XrMain`, затем `MainScene` | да |
| `Assets/XR/**` | лоадеры XR и настройки OpenXR (активен OpenXR) | через редактор |
| `PlayerPrefs` (реестр Windows) | пользовательские настройки: курсор, графика, XR-режим | во время работы |

**Отчёты во время работы** пишутся в `%USERPROFILE%\AppData\LocalLow\KazistovVv\KazistovVv\Reports`
(единый каталог: `plan_metrics.csv`, PDF, субтитры `.srt`).

---

## 7. Документация (`Docs/`) и корневые `.md`

| Файл | Что внутри |
|---|---|
| `PROJECT_CONTEXT.md` (корень) | журнал всех сессий §0–§28 |
| `Docs/PROJECT_STRUCTURE.md` | **этот документ** |
| `Docs/PROBLEMS.md` | карточка проблем: закрытые и открытые |
| `Docs/DEVELOPER_README.md` | среда, сборка, запуск, тесты, типовые проблемы |
| `Docs/API_REFERENCE.md` | полный справочник API проекта |
| `Docs/ARCHITECTURE.md` | архитектурный снимок (§0.7) |
| `Docs/CHANGELOG.md` | что ломалось и как починили |
| `Docs/CODE_AUDIT.md`, `LOCALIZATION_AUDIT.md`, `PERFORMANCE_AUDIT.md`, `GIT_AUDIT.md`, `AUDIT_S27_SMOOTHNESS_COLLISION_UI.md` | аудиты |
| `Docs/PLAN_XR_STAGES.md` | план XR-этапов 0–8 |
| `Docs/REPORT_XR_S28.md` | подробный разбор XR-этапа |
| `Docs/PROMPT_SKELETON_MR_VR_AR.md` | каркас промта и алгоритм работы |
| `Docs/Screenshots/` | снимки сцены с подписями (`CAPTIONS.md`) |

---

## 8. Инструменты (`Tools/`)

| Файл | Что делает |
|---|---|
| `ci-console-check.ps1` | **батчмод-гейт «чистой консоли»**: компилирует проект, разбирает лог (ошибки, предупреждения, служебные сообщения), возвращает 1 при проблемах |
| `xr-checks.ps1` | последовательный прогон гейта и EditMode-тестов (§28) |
| `xr-build-apk-ascii.ps1` | сборка APK через латинский путь (§28; нужна из-за не-ASCII пути старой папки) |

Редакторские инструменты живут в `Assets/_Project/01_Scripts/Editor/` (меню `Tools/`) —
в том числе `KvXrTools.cs` (§28: лоадеры XR, сборка XR-сцены, сборка APK).

---

## 9. Что НЕ коммитить и не переносить

| Путь | Почему |
|---|---|
| `Library/`, `Temp/`, `obj/`, `Logs/`, `UserSettings/` | производные: пересоздаются Unity |
| `Builds/` | артефакты сборки |
| `*.csproj`, `*.sln`, `*.slnx` | генерируются Unity (в проекте исторически лежат в git) |
| `Assets/Resources/PerformanceTestRun*.json` | служебные файлы прогонов тестов |
| Корневые `_diag_*`, `_dsh_*`, `_*_compile.log` | стенограммы диагностики прошлых сессий (в новой папке их нет — остались в старой) |
| Папки-резервы `_backup_*`, `_trash_*`, `_rollback_*` | снимки проекта, не часть сборки |

---

## 10. Быстрый указатель «где что искать»

| Задача | Где смотреть |
|---|---|
| кинематика роботов | `01_Scripts/Core/SixAxisController.cs`, `SCARAController.cs`, `RobotDH.cs` |
| планирование пути | `01_Scripts/Trajectory/Planner.cs`, `CollisionWorld.cs`, `SafetyGate.cs` |
| движение робота | `01_Scripts/Trajectory/MotionExecutor.cs`, `TrajectoryFlowController.cs` |
| все движения из кнопок/голоса/жестов | `01_Scripts/Features/KvPlanKit`-вызовы → `MotionExecutor.Play` |
| зона достижимости | `01_Scripts/Trajectory/WorkspaceVisualizer.cs`, `ReachabilityOracle.cs` |
| интерфейс (оболочка) | `06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs` |
| тулбар / меню / дерево | `06_KazistovVv_UI/03_Scripts/Zones/` |
| горячие клавиши | `06_KazistovVv_UI/03_Scripts/Core/KvBindings.cs` (реестр) |
| настройки пользователя | `06_KazistovVv_UI/03_Scripts/Core/KvSettings.cs` |
| ввод (мышь/шлем/геймпад) | `01_Scripts/Input/` |
| XR: риг, ввод, панель, MR | `01_Scripts/Xr/` |
| голос, руки, взгляд, вибрация | `01_Scripts/Features/KvXrInput.cs`, `KvHaptics.cs` |
| сборка сцены со стендами | `01_Scripts/Trajectory/StandBuilder.cs` |
| тесты | `Assets/_Project/08_Tests/{Editor,PlayMode}/` |
| прогоны и проверки | `Tools/`, `Logs/` |
