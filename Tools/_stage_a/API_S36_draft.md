
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

### `class KvGazeDwell`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvGazeDwell.cs` (строка 21)
- **Назначение:** НАЖАТИЕ ВЗГЛЯДОМ ПО ЗАДЕРЖКЕ (сессия §33, дефект с живого шлема 15.10.2026). ЗАЧЕМ. Владелец в шлеме: «не могу навестись на оверлей». Причина не в интерфейсе: указатель проекта брал луч у ПРАВОГО КОНТРОЛЛЕРА (`TryRightPose`), а контроллеры приложению не отдаются вовсе — значит луча не было и нажать было нечем. Пока контроллеры не заведены, единственный доступный «указатель» — ВЗГЛЯД: человек смотрит на кнопку, и через некоторое время она нажимается сама. Такой способ (dwell) — стандарт для VR-интерфейсов без контроллеров, и он не мешает: когда контроллер появится, работает обычный луч с курком. ПОЧЕМУ ВЫНЕСЕНО В ОТДЕЛЬНЫЙ ТИП. Решение «пора нажимать» — это ЧИСЛО, а не картинка: накопили врем…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| константа | `DefaultSeconds` | `public const float DefaultSeconds = 1.3f;` |  |
| константа | `MinSeconds` | `public const float MinSeconds = 0.2f;` |  |
| метод | `Advance` | `public static float Advance(float progress, float deltaTime, float threshold)` |  |

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

### `class KvPicoControllerProfile`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvPicoControllerProfile.cs` (строка 89)
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
| поле (только чтение) | `RightHand` | `public static readonly List<string> RightHand = new List<string>() { UserPaths.rightHand };` |  |
| поле (только чтение) | `LeftHand` | `public static readonly List<string> LeftHand = new List<string>() { UserPaths.leftHand };` |  |
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

### `class KvWindowBelt`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvWindowBelt.cs` (строка 26)
- **Назначение:** РЕЕСТР ОКОН И ПОЯС (решения владельца [D21] «свернуть/закрыть» и [D22] «скрытые окна — на пояс»). ЧТО ЭТО. Единое место, где известно про ВСЕ окна интерфейса: как их показать, скрыть и свернуть, в каком они состоянии и какие окна сейчас лежат на поясе. Без такого реестра «пояс» пришлось бы собирать по сцене поиском компонентов (а окна проекта живут на разных канвасах и создаются кодом), и любое новое окно пришлось бы подключать в трёх местах. ЧЕГО ЗДЕСЬ НЕТ: ни GameObject, ни Canvas, ни кадра. Только идентификаторы, состояния и ДЕЙСТВИЯ, которые передаёт само окно при регистрации. Поэтому правила пояса проверяются тестами без сцены (`KvWindowBeltTests`), а построение кнопок остаётся в интерф…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| тип | `Entry` | `public class Entry` |  |
| поле | `Id` | `public string Id;` |  |
| поле | `Window` | `public object Window;` |  |
| поле | `Show` | `public WindowAction Show;` |  |
| поле | `Hide` | `public WindowAction Hide;` |  |
| поле | `Collapse` | `public WindowAction Collapse;` |  |
| поле | `State` | `public KvWindowState State = KvWindowState.Expanded;` |  |
| поле | `Title` | `public string Title;` |  |

### `enum KvWindowState`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrWindowState.cs` (строка 6)
- **Назначение:** —

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| значение | `Expanded` | `Expanded = 0` |  |
| значение | `Collapsed` | `Collapsed = 1` |  |
| значение | `OnBelt` | `OnBelt = 2` |  |
| значение | `Closed` | `Closed = 3` |  |

### `class KvXrCameraGuard`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrCameraGuard.cs` (строка 24)
- **Назначение:** СТОРОЖ XR-КАМЕРЫ (сессия §28, этап 1–3 плана XR). ЗАЧЕМ ОТДЕЛЬНЫЙ КОМПОНЕНТ. Проект написан для компьютера, и в нём есть сервисы, которые считают, что камерой управляет МЫШЬ. В шлеме камерой управляет трекинг головы, и любой такой сервис становится дефектом. Самый опасный из них — `IdleCameraBrain` («режим простоя, как в Skyrim»): через десять минут бездействия он НАЧИНАЕТ ОБЛЕТАТЬ РОБОТА и переписывает `transform` камеры. В VR это не «красиво», а мгновенная дурнота у оператора, потому что картинка едет сама. ЧТО ДЕЛАЕТ СТОРОЖ: 1) убирает облёт простоя с камеры шлема (в VR он недопустим в принципе); 2) снимает захват системного курсора — в шлеме его нет, а захваченный курсор на компьютере ме…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `freeSystemCursor` | `public bool freeSystemCursor = true;` |  |
| поле | `disableIdleOrbit` | `public bool disableIdleOrbit = true;` |  |
| свойство | `ActionsTaken` | `public string ActionsTaken { get; private set; } = "ничего (всё уже было выключено)";` |  |

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
| метод | `HidesVirtualStands` | `public static bool HidesVirtualStands(bool mrActive, bool hideField, bool showToggle)` |  |

### `class KvXrOpenXrDiagnostics`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrOpenXrDiagnostics.cs` (строка 29)
- **Назначение:** СТРОКИ ОБ OPENXR И ВВОДЕ ДЛЯ ЖУРНАЛА ШЛЕМА (сессия §36). ЗАЧЕМ. Требование владельца [F5]: «сделано» без строки в журнале не принимается. По дефекту контроллеров (карточка Б18/Б24) в журнале раньше было видно только «правый нет · левый нет · устройств 0», то есть итог, а не причину. Здесь печатается то, что раньше приходилось выяснять перебором логов рантайма: • выдал ли рантайм расширение `XR_BD_controller_interaction` (без него профиль ByteDance не действует вовсе); • сколько расширений рантайм объявил всего и сколько включено; • не нарушает ли наша карта привязок правило «путь кнопки руки → своя рука» — именно это нарушение заставляло OpenXR отвечать `XR_ERROR_PATH_UNSUPPORTED` и отбрасыв…

### `enum KvXrOriginMode`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrRigConfig.cs` (строка 22)
- **Назначение:** КОНФИГУРАЦИЯ XR-РИГА (сессия §28, этап 1 плана XR). ЗАЧЕМ ОТДЕЛЬНЫЙ ТИП. Все решения по ригу — уровень глаз, масштаб мира, способ стерео, отсчёт от пола или от устройства — должны быть ОДНИМ объектом, который можно посчитать, проверить тестом и показать в отчёте. Разбросанные по сцене числа проверить нельзя: в шлеме они выглядят «нормально» ровно до момента, когда оператор ростом 1,95 видит панель на уровне колен. ЧТО ЗДЕСЬ ЧИСТОЕ, А ЧТО НЕТ. Здесь только арифметика (никаких GameObject, сцен и устройств) — поэтому тип полностью покрыт EditMode-тестами, а сборку рига делает <c>KvXrRigBuilder</c>. Решения, на которые опирается тип: [R1] PICO 4 Ultra, [R2] HDRP низкий пресет, [R3] только OpenXR…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| значение | `Floor` | `Floor = 0` |  |
| значение | `Device` | `Device = 1` |  |
| значение | `Scene` | `Scene = 2` |  |

### `enum KvXrOverlayDisposition`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrOverlayPolicy.cs` (строка 6)
- **Назначение:** —

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| значение | `HideRendering` | `HideRendering = 0` |  |
| значение | `Remove` | `Remove = 1` |  |
| значение | `Keep` | `Keep = 2` |  |

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

### `enum KvXrPanelLevel`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrGroupPanelLayout.cs` (строка 6)
- **Назначение:** —

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| значение | `Groups` | `Groups = 0` |  |
| значение | `GroupFan` | `GroupFan = 1` |  |

### `class KvXrQuickPanel`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrQuickPanel.cs` (строка 29)
- **Назначение:** ПАНЕЛЬ БЫСТРОГО ВЫБОРА У РУКИ (решение владельца [D20], сессия §32). ЧТО ДЕЛАЕТ. Строит небольшую панель кнопок у выбранной руки — «бубликом» (по кругу) или «полумесяцем» (по дуге сверху) — и держит её перед оператором: панель следует за рукой и разворачивается к лицу. Кнопки берутся из РЕЕСТРА КОМАНД проекта (`KvCommands`), поэтому нажатие делает ровно то же, что пункт меню или кнопка тулбара: второй реализации действий не появляется. ПОЧЕМУ КНОПКИ У РУКИ, А НЕ ЛИСТОМ СВЕРХУ. Прежняя формулировка [D8] («один лист кнопок сверху») заменена решением [D20]: лист закрывает обзор и требует уводить луч от работы, а панель у руки всегда под ладонью и не мешает целиться другой рукой. ГДЕ ЛОГИКА, А Г…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `headCamera` | `public Camera headCamera;` |  |
| поле | `sortingOrder` | `public int sortingOrder = 55;` |  |
| поле | `forwardOffset` | `public float forwardOffset = 0.14f;` |  |
| поле | `upOffset` | `public float upOffset = 0.06f;` |  |
| поле | `buttonSize` | `public float buttonSize = 76f;` |  |
| константа | `FallbackDistanceMeters` | `public const float FallbackDistanceMeters = 1.15f;` |  |
| константа | `FallbackDropMeters` | `public const float FallbackDropMeters = 0.25f;` |  |
| поле | `canvasSize` | `public float canvasSize = 420f;` |  |
| поле | `commandIds` | `public string[] commandIds =` |  |

### `enum KvXrQuickShape`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrQuickPanelLayout.cs` (строка 6)
- **Назначение:** —

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| значение | `Ring` | `Ring = 0` |  |
| значение | `Crescent` | `Crescent = 1` |  |

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

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrSceneContent.cs` (строка 29)
- **Назначение:** НАЧИНКА XR-СЦЕНЫ: СТОЛЫ И РОБОТЫ (решение владельца: «добавить роботов и столы», сессия §28). ЗАЧЕМ ОТДЕЛЬНЫЙ КОМПОНЕНТ. По решению [D6] XR-сцена сделана ОТДЕЛЬНОЙ и сначала была пустой (риг, интерфейс, MR-комната) — начинку предполагалось подгружать. Проверка показала, что в `XrMain.unity` нет ни одного робота, то есть в шлеме было нечего показывать. Этот компонент создаёт начинку ТЕМ ЖЕ способом, что и десктопный проект — через <see cref="StandBuilder"/>, а не копированием объектов из `MainScene`. ПОЧЕМУ ЧЕРЕЗ StandBuilder. Три причины: 1) это ровно тот код, которым построены стенды в `MainScene` — значит геометрия, высоты и расположение роботов совпадают с проверенными; 2) он идемпотентен…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| поле | `createStands` | `public bool createStands = true;` |  |
| поле | `tableTopHeight` | `public float tableTopHeight = StandBuilder.TopHeight;` |  |
| поле | `buildOnStart` | `public bool buildOnStart = true;` |  |
| свойство | `RobotRoot` | `public Transform RobotRoot { get; private set; }` |  |
| свойство | `RobotCount` | `public int RobotCount { get; private set; }` |  |
| поле | `bringStandsNearPlayer` | `public bool bringStandsNearPlayer = true;` |  |
| поле | `standNearEdgeMeters` | `public float standNearEdgeMeters = 0.5f;` |  |
| поле | `standDistance` | `public float standDistance = 3.2f;` |  |
| поле | `standSpacing` | `public float standSpacing = 3.0f;` |  |
| поле | `createFloor` | `public bool createFloor = true;` |  |
| поле | `floorSize` | `public float floorSize = 40f;` |  |
| поле | `floorDropMeters` | `public float floorDropMeters = 0.03f;` |  |

### `class KvXrSettingsChanged`

- **Файл:** `Assets\_Project\01_Scripts\Xr\KvXrSettingsChanged.cs` (строка 17)
- **Назначение:** ОПОВЕЩЕНИЕ О СМЕНЕ XR-НАСТРОЕК (сессия §28). ЗАЧЕМ. Настройки XR — статическое хранилище (PlayerPrefs): у него нет ни событий, ни владельца. А применять их надо немедленно: переключил «плавный ход» — метка телепорта обязана исчезнуть в тот же кадр, а не после перезапуска. Ровно на такой несогласованности в проекте уже были дефекты (§23: «настройки не открывались», «курсор не восстанавливался»), поэтому здесь одна точка оповещения на весь XR-слой. Кто подписывается: локомоция, панель интерфейса, слои MR. Кто вызывает: панель настроек (и диагностика).

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
| поле | `useGazeWhenNoController` | `public bool useGazeWhenNoController = true;` |  |
| поле | `gazeDwellSeconds` | `public float gazeDwellSeconds = KvGazeDwell.DefaultSeconds;` |  |
| поле | `gazeOriginForward` | `public float gazeOriginForward = 0.10f;` |  |
| поле | `gazeOriginDown` | `public float gazeOriginDown = 0.10f;` |  |
| константа | `MaxGazeDownMeters` | `public const float MaxGazeDownMeters = 0.03f;` |  |
| поле | `useEventSystemFallback` | `public bool useEventSystemFallback = false;` |  |
| поле | `groupPanel` | `public KvXrGroupPanel groupPanel;` |  |
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

- **Файл:** `Assets\_Project\01_Scripts\Editor\KvXrDeviceTools.cs` (строка 31)
- **Назначение:** НАСТРОЙКИ ПРИЛОЖЕНИЯ ДЛЯ ЖИВОГО УСТРОЙСТВА (сессия §31: шлем PICO 4 Ultra приехал). ЗАЧЕМ ОТДЕЛЬНЫЙ ИНСТРУМЕНТ. Эти настройки нельзя «посмотреть глазами» в редакторе так, чтобы понять, что попадёт в APK: идентификатор приложения лежит в проектном файле, а состояние OpenXR-фич зависит от ПЛАТФОРМЫ (включённое для Standalone в сборку под Android не попадает). Ошибка здесь выглядит так: приложение поставилось, запустилось, но в шлеме не работают контроллеры — и непонятно, почему. ПОДТВЕРЖДЕНИЕ ФАКТА, А НЕ НАМЕРЕНИЯ. Оба метода печатают состояние ДО и ПОСЛЕ и перечитывают значение — как в §29 с глобальным умолчанием графики: правка считается сделанной только если её видно в сохранённом файле. ВН…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| константа | `OwnIdentifier` | `public const string OwnIdentifier = "com.KazistovVv.app";` |  |
| константа | `SimpleControllerProfile` | `public const string SimpleControllerProfile = "KHRSimpleControllerProfile";` |  |
| константа | `PicoControllerProfile` | `public const string PicoControllerProfile = "KvPicoControllerProfile";` |  |
| метод | `ApplyOwnIdentifier` | `public static void ApplyOwnIdentifier()` |  |

### `class KvXrTools`

- **Файл:** `Assets\_Project\01_Scripts\Editor\KvXrTools.cs` (строка 33)
- **Назначение:** ИНСТРУМЕНТЫ XR-ЭТАПА (сессия §28, этапы 0–4 плана XR). ПОЧЕМУ ВСЁ ДЕЛАЕТСЯ КОДОМ, А НЕ РУКАМИ В РЕДАКТОРЕ. Шлема нет, а собрать XR-сцену надо так, чтобы её можно было ПОВТОРИТЬ и ПРОВЕРИТЬ: сцена из кода проверяется повторным запуском и сравнением, сцена из ручных действий — только памятью. Плюс: проект ведётся в VCS, и воспроизводимая сборка сцены не создаёт конфликтов «а у меня сцена другая». ЧТО ЗДЕСЬ ГЛАВНОЕ (<see cref="ConfigureXrLoaders"/>) — НАЙДЕННЫЙ ДЕФЕКТ: в проекте активными лоадерами XR стояли **Mock HMD** и аркадный ARCore, а OpenXR — просто лежал ассетом. С таким набором собранное приложение в шлеме НЕ РАБОТАЛО БЫ вообще: Mock HMD — это заглушка для отладки без устройства. Имен…

| Тип | Имя | Объявление | Описание |
|---|---|---|---|
| константа | `XrSceneName` | `public const string XrSceneName = "XrMain";` |  |
| константа | `ScenesFolder` | `public const string ScenesFolder = "Assets/_Project/00_Scenes";` |  |
| метод | `ConfigureXrLoaders` | `public static void ConfigureXrLoaders()` |  |


