using System.Collections.Generic;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.Scripting;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Input;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if USE_INPUT_SYSTEM_POSE_CONTROL
using PoseControl = UnityEngine.InputSystem.XR.PoseControl;
#else
using PoseControl = UnityEngine.XR.OpenXR.Input.PoseControl;
#endif

#if USE_STICK_CONTROL_THUMBSTICKS
using ThumbstickControl = UnityEngine.InputSystem.Controls.StickControl;
#else
using ThumbstickControl = UnityEngine.InputSystem.Controls.Vector2Control;
#endif

namespace KazistovVvXr
{
    /// <summary>
    /// ПРОФИЛЬ КОНТРОЛЛЕРОВ PICO 4 ULTRA ДЛЯ OPENXR (сессия §34, дефект с живого шлема 15.10.2026).
    ///
    /// ЗАЧЕМ СВОЙ ПРОФИЛЬ. Контроллеры в руках владельца работали: система шлема их видела
    /// и печатала позы (`PvrTrackingService: logDebugControllerPose,left confidence 2`).
    /// А приложение — нет: `[KvXr] XR-ввод: шлем есть · правый нет · левый нет`.
    ///
    /// ПОЧЕМУ. OpenXR создаёт устройство ввода только для ТОГО профиля взаимодействия, который
    /// рантайм назвал текущим для руки. Журнал шлема называет его прямо:
    ///     `xrGetCurrentInteractionProfile ret = 0 Path /interaction_profiles/bytedance/pico4s_controller`
    /// В пакете Unity `com.unity.xr.openxr` профилей PICO НЕТ ВООБЩЕ: там Khronos Simple,
    /// Oculus Touch, Valve Index, Microsoft Motion, HP Reverb G2, Meta Quest Touch Plus/Pro,
    /// ручное взаимодействие и взгляд — четырнадцать профилей, и ни одного ByteDance.
    /// Поэтому включённый в §31 `KHRSimpleControllerProfile` привязку получал, а устройства
    /// не создавал: Unity регистрирует устройство под конкретный путь профиля
    /// (`Internal_RegisterDeviceDefinition(userPath, desiredInteractionProfile, …)`),
    /// и «похожий» профиль эту роль не выполняет.
    ///
    /// ПОЧЕМУ ЭТО НЕ НАРУШАЕТ РЕШЕНИЕ [R3] («только OpenXR, PICO SDK не ставим»).
    /// Здесь нет ни одной строки вендорского SDK: это описание контроллера средствами самого
    /// OpenXR — тот же способ, которым в пакете Unity описаны профили Oculus и Valve.
    /// Используется расширение рантайма `XR_BD_controller_interaction`, которое шлем
    /// объявляет сам (`XR_BD_controller_interaction: Version=2`).
    ///
    /// ОТКУДА ВЗЯТЫ ПУТИ. Путь профиля и пути ввода сверены с официальным профилем
    /// PICO 4 Ultra (`PICO4UltraControllerProfile.cs` из PICO-Unity-OpenXR-SDK) и с
    /// журналом рантайма шлема. Ничего не угадано: ошибка в одном символе стоит цикла
    /// сборки и поездки к шлему, а выглядит как «контроллеры снова не найдены».
    ///
    /// ПОЧЕМУ У КНОПОК РАЗНЫЕ РУКИ (найдено 18.10.2026, сессия §36). Профиль был объявлен
    /// правильно, расширение рантайм выдавал (`instance_info … "BD_controller_interaction":true`),
    /// а привязки он ВСЁ РАВНО отвергал:
    ///     `xrSuggestInteractionProfileBindings: XR_ERROR_PATH_UNSUPPORTED`
    /// Причина в способе, которым Unity раскладывает привязку по рукам —
    /// `com.unity.xr.openxr/Runtime/input/OpenXRInput.cs:296`:
    ///     `foreach (var userPath in binding.userPaths ?? deviceUserPaths)`
    /// то есть привязка БЕЗ списка `userPaths` уходит СРАЗУ НА ОБЕ РУКИ. Кнопок «a» на левом
    /// контроллере нет, кнопок «x» нет на правом — такие сочетания «путь × рука» в профиле
    /// ByteDance не существуют, и по спецификации OpenXR весь набор привязок отклоняется
    /// целиком. Поэтому у каждой кнопки руки теперь СВОЙ `userPaths`, как в официальном
    /// профиле: `x/y/menu → leftHand`, `a/b → rightHand`. Проверка этого правила —
    /// <see cref="HandBindingViolation"/> и <see cref="AuditHandBindings"/>.
    ///
    /// ЧЕСТНОЕ ОГРАНИЧЕНИЕ. Объявлен ОДИН профиль — тот, который назвал наш шлем.
    /// Для других устройств PICO (например, Neo3) нужен свой путь профиля; в SDK их три:
    /// `/interaction_profiles/bytedance/pico4s_controller` (4 Ultra), `…/pico4_controller`,
    /// `…/pico_neo3_controller`. Добавлять их «на всякий случай» нельзя: каждое объявление —
    /// это ещё одно устройство ввода в системе, и разбираться в нём придётся уже в шлеме.
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.XR.OpenXR.Features.OpenXRFeature(
        UiName = "PICO 4 Ultra (ByteDance) Controller Profile",
        BuildTargetGroups = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone },
        Company = "KazistovVv",
        Desc = "Контроллеры PICO 4 Ultra по профилю взаимодействия /interaction_profiles/bytedance/pico4s_controller. " +
               "Без него OpenXR не создаёт устройств ввода: контроллеры видны системе шлема, но не приложению.",
        OpenxrExtensionStrings = extensionString,
        Version = "1.0.0",
        Category = UnityEditor.XR.OpenXR.Features.FeatureCategory.Interaction,
        FeatureId = featureId)]
#endif
    public class KvPicoControllerProfile : OpenXRInteractionFeature
    {
        /// <summary>
        /// Идентификатор фичи. Свой (не `com.unity.openxr.*`): по нему фича отличается
        /// в настройках OpenXR и в отчёте о том, что попало в APK.
        /// </summary>
        public const string featureId = "com.kazistovvv.openxr.feature.input.pico4s";

        /// <summary>
        /// ПУТЬ ПРОФИЛЯ — ровно тот, который рантайм шлема назвал текущим.
        /// Проверяется тестом посимвольно: это самая дорогая ошибка в файле.
        /// </summary>
        public const string profile = "/interaction_profiles/bytedance/pico4s_controller";

        /// <summary>Расширение рантайма, без которого профиль не действует.</summary>
        public const string extensionString = "XR_BD_controller_interaction";

        // ------------------------------------------------ пути ввода профиля PICO

        /// <summary>Курок: значение 0…1 (ось).</summary>
        public const string trigger = "/input/trigger/value";

        /// <summary>Курок: нажатие кнопкой.</summary>
        public const string triggerClick = "/input/trigger/click";

        /// <summary>Курок: касание.</summary>
        public const string triggerTouch = "/input/trigger/touch";

        /// <summary>Боковой хват: значение 0…1.</summary>
        public const string squeeze = "/input/squeeze/value";

        /// <summary>Боковой хват: нажатие.</summary>
        public const string squeezeClick = "/input/squeeze/click";

        /// <summary>Стик: две оси.</summary>
        public const string thumbstick = "/input/thumbstick";

        /// <summary>Стик: нажатие.</summary>
        public const string thumbstickClick = "/input/thumbstick/click";

        /// <summary>Стик: касание.</summary>
        public const string thumbstickTouch = "/input/thumbstick/touch";

        /// <summary>Кнопка A (правый контроллер).</summary>
        public const string buttonA = "/input/a/click";

        /// <summary>Кнопка A: касание.</summary>
        public const string buttonATouch = "/input/a/touch";

        /// <summary>Кнопка B (правый контроллер).</summary>
        public const string buttonB = "/input/b/click";

        /// <summary>Кнопка B: касание.</summary>
        public const string buttonBTouch = "/input/b/touch";

        /// <summary>Кнопка X (левый контроллер).</summary>
        public const string buttonX = "/input/x/click";

        /// <summary>Кнопка X: касание.</summary>
        public const string buttonXTouch = "/input/x/touch";

        /// <summary>Кнопка Y (левый контроллер).</summary>
        public const string buttonY = "/input/y/click";

        /// <summary>Кнопка Y: касание.</summary>
        public const string buttonYTouch = "/input/y/touch";

        /// <summary>Кнопка меню (она же «домой»).</summary>
        public const string menu = "/input/menu/click";

        /// <summary>Системная кнопка: та же кнопка меню, отдельный путь профиля.</summary>
        public const string system = "/input/system/click";

        /// <summary>Поза кисти (по ней ставится панель у руки).</summary>
        public const string grip = "/input/grip/pose";

        /// <summary>Поза указания (по ней идёт луч в интерфейс).</summary>
        public const string aim = "/input/aim/pose";

        /// <summary>Вибрация.</summary>
        public const string haptic = "/output/haptic";

        /// <summary>
        /// СПИСОК РУК ДЛЯ ПРИВЯЗКИ — «только правая». Один экземпляр на все правые привязки:
        /// Unity читает эти списки, а не изменяет их.
        ///
        /// ЗАЧЕМ ЭТО ВООБЩЕ НУЖНО (кратко, подробно — в описании класса). Без `userPaths`
        /// Unity подставляет путь ОБЕИМ рукам (`OpenXRInput.cs:296`), и сочетание
        /// «/input/a/click на левой руке» рантайм отвергает — вместе со всем набором привязок.
        /// </summary>
        public static readonly List<string> RightHand = new List<string>() { UserPaths.rightHand };

        /// <summary>Список рук для привязки — «только левая».</summary>
        public static readonly List<string> LeftHand = new List<string>() { UserPaths.leftHand };

        private const string kDeviceLocalizedName = "PICO 4 Ultra Controller OpenXR";

        /// <summary>
        /// УСТРОЙСТВО ВВОДА: то, что увидит `InputSystem` и, значит, `KvXrInputSource`.
        ///
        /// СОСТАВ ЭЛЕМЕНТОВ ВЗЯТ НЕ «НА ГЛАЗ». `KvXrInputSource` ищет курок по списку имён
        /// (`triggerPressed`, `trigger`, …), хват — по (`gripPressed`, `grip`, `squeeze`),
        /// стик — по (`thumbstick`, `primary2DAxis`), позу — по `devicePose`. Все эти имена
        /// здесь есть, и совпадение проверяется тестом. Имена — общие для OpenXR-профилей
        /// (как у Oculus Touch), а не выдуманные: на них рассчитывает и Input System.
        /// </summary>
        [Preserve, InputControlLayout(displayName = "PICO 4 Ultra Controller (OpenXR)",
            commonUsages = new[] { "LeftHand", "RightHand" })]
        public class KvPicoController : XRControllerWithRumble
        {
            /// <summary>Стик (две оси).</summary>
            [Preserve, InputControl(aliases = new[] { "Primary2DAxis", "Joystick" }, usage = "Primary2DAxis")]
            public ThumbstickControl thumbstick { get; private set; }

            /// <summary>Боковой хват как ось 0…1.</summary>
            [Preserve, InputControl(aliases = new[] { "GripAxis", "squeeze" }, usage = "Grip")]
            public AxisControl grip { get; private set; }

            /// <summary>Нажатие бокового хвата.</summary>
            [Preserve, InputControl(aliases = new[] { "GripButton", "squeezeClicked" }, usage = "GripButton")]
            public ButtonControl gripPressed { get; private set; }

            /// <summary>Кнопка меню.</summary>
            [Preserve, InputControl(aliases = new[] { "Primary", "menubutton", "systemButton" }, usage = "MenuButton")]
            public ButtonControl menu { get; private set; }

            /// <summary>Главная кнопка: A справа, X слева.</summary>
            [Preserve, InputControl(aliases = new[] { "A", "X", "buttonA", "buttonX" }, usage = "PrimaryButton")]
            public ButtonControl primaryButton { get; private set; }

            /// <summary>Касание главной кнопки.</summary>
            [Preserve, InputControl(aliases = new[] { "ATouched", "XTouched" }, usage = "PrimaryTouch")]
            public ButtonControl primaryTouched { get; private set; }

            /// <summary>Вторая кнопка: B справа, Y слева.</summary>
            [Preserve, InputControl(aliases = new[] { "B", "Y", "buttonB", "buttonY" }, usage = "SecondaryButton")]
            public ButtonControl secondaryButton { get; private set; }

            /// <summary>Касание второй кнопки.</summary>
            [Preserve, InputControl(aliases = new[] { "BTouched", "YTouched" }, usage = "SecondaryTouch")]
            public ButtonControl secondaryTouched { get; private set; }

            /// <summary>Курок как ось 0…1.</summary>
            [Preserve, InputControl(usage = "Trigger")]
            public AxisControl trigger { get; private set; }

            /// <summary>Нажатие курка.</summary>
            [Preserve, InputControl(aliases = new[] { "indexButton", "indexTouched", "triggerbutton" }, usage = "TriggerButton")]
            public ButtonControl triggerPressed { get; private set; }

            /// <summary>Касание курка.</summary>
            [Preserve, InputControl(aliases = new[] { "indexTouch", "indexNearTouched" }, usage = "TriggerTouch")]
            public ButtonControl triggerTouched { get; private set; }

            /// <summary>Нажатие стика.</summary>
            [Preserve, InputControl(aliases = new[] { "JoystickOrPadPressed", "thumbstickClick" }, usage = "Primary2DAxisClick")]
            public ButtonControl thumbstickClicked { get; private set; }

            /// <summary>Касание стика.</summary>
            [Preserve, InputControl(aliases = new[] { "JoystickOrPadTouched", "thumbstickTouch" }, usage = "Primary2DAxisTouch")]
            public ButtonControl thumbstickTouched { get; private set; }

            /// <summary>
            /// ПОЗА КИСТИ. Смещение 0 — так же, как в профилях самого Unity: это составная поза,
            /// из неё читаются положение, поворот и признак отслеживания (`KvXrInputSource.TryPose`).
            /// </summary>
            [Preserve, InputControl(offset = 0, aliases = new[] { "device", "gripPose" }, usage = "Device")]
            public PoseControl devicePose { get; private set; }

            /// <summary>ПОЗА УКАЗАНИЯ — из неё идёт луч указателя интерфейса.</summary>
            [Preserve, InputControl(offset = 0, alias = "aimPose", usage = "Pointer")]
            public PoseControl pointer { get; private set; }

            /// <summary>Вибрация.</summary>
            [Preserve, InputControl(usage = "Haptic")]
            public HapticControl haptic { get; private set; }

            /// <inheritdoc/>
            protected override void FinishSetup()
            {
                base.FinishSetup();

                thumbstick = GetChildControl<ThumbstickControl>("thumbstick");
                grip = GetChildControl<AxisControl>("grip");
                gripPressed = GetChildControl<ButtonControl>("gripPressed");
                menu = GetChildControl<ButtonControl>("menu");
                primaryButton = GetChildControl<ButtonControl>("primaryButton");
                primaryTouched = GetChildControl<ButtonControl>("primaryTouched");
                secondaryButton = GetChildControl<ButtonControl>("secondaryButton");
                secondaryTouched = GetChildControl<ButtonControl>("secondaryTouched");
                trigger = GetChildControl<AxisControl>("trigger");
                triggerPressed = GetChildControl<ButtonControl>("triggerPressed");
                triggerTouched = GetChildControl<ButtonControl>("triggerTouched");
                thumbstickClicked = GetChildControl<ButtonControl>("thumbstickClicked");
                thumbstickTouched = GetChildControl<ButtonControl>("thumbstickTouched");
                devicePose = GetChildControl<PoseControl>("devicePose");
                pointer = GetChildControl<PoseControl>("pointer");
                haptic = GetChildControl<HapticControl>("haptic");
            }
        }

        /// <summary>
        /// Регистрация устройства в Input System — по имени, которое отдаёт рантайм.
        ///
        /// ПОЧЕМУ БЕЗ ПРОВЕРКИ «ЛОАДЕР ВКЛЮЧЁН ДЛЯ ПЛАТФОРМЫ». В профилях самого Unity перед
        /// регистрацией стоит проверка `OpenXRLoaderEnabledForSelectedBuildTarget`, но она
        /// объявлена `internal` — из сборки проекта недоступна (компилятор отказал: `error CS0103`
        /// на обеих строках, 15.10.2026). Регистрация макета без этой проверки безопасна: макет —
        /// это описание устройства для Input System, и устройств по нему не появится, пока рантайм
        /// не отдаст контроллер. В сборке нужную проверку делает сам пакет OpenXR при инициализации.
        /// </summary>
        protected override void RegisterDeviceLayout()
        {
            UnityEngine.InputSystem.InputSystem.RegisterLayout(typeof(KvPicoController),
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct(kDeviceLocalizedName));
        }

        /// <summary>Снятие регистрации при выключении фичи.</summary>
        protected override void UnregisterDeviceLayout()
        {
            UnityEngine.InputSystem.InputSystem.RemoveLayout(typeof(KvPicoController).Name);
        }

        /// <inheritdoc/>
        protected override string GetDeviceLayoutName()
        {
            return nameof(KvPicoController);
        }

        /// <summary>
        /// КАРТА ДЕЙСТВИЙ: что именно привязывается к каким путям профиля.
        ///
        /// ПОЧЕМУ КНОПКИ ПРИВЯЗАНЫ ПАРАМИ. Кнопки правого контроллера — A и B, левого — X и Y,
        /// при этом действие одно (`primaryButton`). Обе привязки идут в ОДНО действие: так же
        /// сделано в профиле Oculus Touch внутри пакета Unity. Иначе пришлось бы гадать, в какой
        /// руке контроллер, и интерфейс работал бы «через раз».
        ///
        /// ПУТИ МЕНЮ. У профиля PICO их два (`menu` и `system`) — обе ведут на кнопку меню;
        /// привязываем оба, чтобы не зависеть от того, какой из них рантайм считает основным.
        /// </summary>
        protected override void RegisterActionMapsWithRuntime()
        {
            AddActionMap(BuildActionMap());
        }

        /// <summary>
        /// КАРТА ДЕЙСТВИЙ КАК ДАННЫЕ — без регистрации в рантайме. Нужна ради проверки:
        /// правило «путь кнопки руки обязан быть ограничен своей рукой» (<see cref="AuditHandBindings"/>)
        /// можно прогнать только по настоящей карте, а не по её пересказу в отчёте.
        /// </summary>
        private static ActionMapConfig BuildActionMap()
        {
            ActionMapConfig actionMap = new ActionMapConfig()
            {
                name = "picocontroller4s",
                localizedName = kDeviceLocalizedName,
                desiredInteractionProfile = profile,
                manufacturer = "ByteDance",
                serialNumber = "",
                deviceInfos = new List<DeviceConfig>()
                {
                    new DeviceConfig()
                    {
                        characteristics = (InputDeviceCharacteristics)(
                            InputDeviceCharacteristics.HeldInHand |
                            InputDeviceCharacteristics.TrackedDevice |
                            InputDeviceCharacteristics.Controller |
                            InputDeviceCharacteristics.Left),
                        userPath = UserPaths.leftHand
                    },
                    new DeviceConfig()
                    {
                        characteristics = (InputDeviceCharacteristics)(
                            InputDeviceCharacteristics.HeldInHand |
                            InputDeviceCharacteristics.TrackedDevice |
                            InputDeviceCharacteristics.Controller |
                            InputDeviceCharacteristics.Right),
                        userPath = UserPaths.rightHand
                    }
                },
                actions = new List<ActionConfig>()
                {
                    // Стик
                    new ActionConfig()
                    {
                        name = "thumbstick",
                        localizedName = "Thumbstick",
                        type = ActionType.Axis2D,
                        usages = new List<string>() { "Primary2DAxis" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = thumbstick, interactionProfileName = profile }
                        }
                    },
                    // Нажатие стика
                    new ActionConfig()
                    {
                        name = "thumbstickClicked",
                        localizedName = "Thumbstick Click",
                        type = ActionType.Binary,
                        usages = new List<string>() { "Primary2DAxisClick" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = thumbstickClick, interactionProfileName = profile }
                        }
                    },
                    // Касание стика
                    new ActionConfig()
                    {
                        name = "thumbstickTouched",
                        localizedName = "Thumbstick Touch",
                        type = ActionType.Binary,
                        usages = new List<string>() { "Primary2DAxisTouch" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = thumbstickTouch, interactionProfileName = profile }
                        }
                    },
                    // Боковой хват — ось
                    new ActionConfig()
                    {
                        name = "grip",
                        localizedName = "Grip",
                        type = ActionType.Axis1D,
                        usages = new List<string>() { "Grip" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = squeeze, interactionProfileName = profile }
                        }
                    },
                    // Боковой хват — нажатие
                    new ActionConfig()
                    {
                        name = "gripPressed",
                        localizedName = "Grip Pressed",
                        type = ActionType.Binary,
                        usages = new List<string>() { "GripButton" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = squeezeClick, interactionProfileName = profile }
                        }
                    },
                    // Кнопка меню
                    new ActionConfig()
                    {
                        name = "menu",
                        localizedName = "Menu",
                        type = ActionType.Binary,
                        usages = new List<string>() { "MenuButton" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = menu, interactionProfileName = profile, userPaths = LeftHand },
                            new ActionBinding() { interactionPath = system, interactionProfileName = profile }
                        }
                    },
                    // A (правая) / X (левая)
                    new ActionConfig()
                    {
                        name = "primaryButton",
                        localizedName = "Primary Button",
                        type = ActionType.Binary,
                        usages = new List<string>() { "PrimaryButton" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = buttonA, interactionProfileName = profile, userPaths = RightHand },
                            new ActionBinding() { interactionPath = buttonX, interactionProfileName = profile, userPaths = LeftHand }
                        }
                    },
                    // Касание A / X
                    new ActionConfig()
                    {
                        name = "primaryTouched",
                        localizedName = "Primary Touched",
                        type = ActionType.Binary,
                        usages = new List<string>() { "PrimaryTouch" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = buttonATouch, interactionProfileName = profile, userPaths = RightHand },
                            new ActionBinding() { interactionPath = buttonXTouch, interactionProfileName = profile, userPaths = LeftHand }
                        }
                    },
                    // B (правая) / Y (левая)
                    new ActionConfig()
                    {
                        name = "secondaryButton",
                        localizedName = "Secondary Button",
                        type = ActionType.Binary,
                        usages = new List<string>() { "SecondaryButton" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = buttonB, interactionProfileName = profile, userPaths = RightHand },
                            new ActionBinding() { interactionPath = buttonY, interactionProfileName = profile, userPaths = LeftHand }
                        }
                    },
                    // Касание B / Y
                    new ActionConfig()
                    {
                        name = "secondaryTouched",
                        localizedName = "Secondary Touched",
                        type = ActionType.Binary,
                        usages = new List<string>() { "SecondaryTouch" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = buttonBTouch, interactionProfileName = profile, userPaths = RightHand },
                            new ActionBinding() { interactionPath = buttonYTouch, interactionProfileName = profile, userPaths = LeftHand }
                        }
                    },
                    // Курок — ось
                    new ActionConfig()
                    {
                        name = "trigger",
                        localizedName = "Trigger",
                        type = ActionType.Axis1D,
                        usages = new List<string>() { "Trigger" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = trigger, interactionProfileName = profile }
                        }
                    },
                    // Курок — нажатие
                    new ActionConfig()
                    {
                        name = "triggerPressed",
                        localizedName = "Trigger Pressed",
                        type = ActionType.Binary,
                        usages = new List<string>() { "TriggerButton" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = triggerClick, interactionProfileName = profile }
                        }
                    },
                    // Курок — касание
                    new ActionConfig()
                    {
                        name = "triggerTouched",
                        localizedName = "Trigger Touched",
                        type = ActionType.Binary,
                        usages = new List<string>() { "TriggerTouch" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = triggerTouch, interactionProfileName = profile }
                        }
                    },
                    // Поза кисти
                    new ActionConfig()
                    {
                        name = "devicePose",
                        localizedName = "Device Pose",
                        type = ActionType.Pose,
                        usages = new List<string>() { "Device" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = grip, interactionProfileName = profile }
                        }
                    },
                    // Поза указания
                    new ActionConfig()
                    {
                        name = "pointer",
                        localizedName = "Pointer Pose",
                        type = ActionType.Pose,
                        usages = new List<string>() { "Pointer" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = aim, interactionProfileName = profile }
                        }
                    },
                    // Вибрация
                    new ActionConfig()
                    {
                        name = "haptic",
                        localizedName = "Haptic Output",
                        type = ActionType.Vibrate,
                        usages = new List<string>() { "Haptic" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding() { interactionPath = haptic, interactionProfileName = profile }
                        }
                    }
                }
            };

            return actionMap;
        }

        // ==================================================== ЧТО ГОВОРИТ РАНТАЙМ

        /// <summary>
        /// ТЕКУЩИЙ ПРОФИЛЬ ВЗАИМОДЕЙСТВИЯ ДЛЯ РУКИ — спрашиваем САМ РАНТАЙМ из нашего сеанса.
        ///
        /// ЗАЧЕМ (сессия §36). После правки `userPaths` рантайм впервые привязал руку к НАШЕМУ
        /// профилю: `oxr_session_attach_action_sets … profiles.left path str =
        /// /interaction_profiles/bytedance/pico4s_controller`. Но `UnityEngine.XR` по-прежнему
        /// показывает ноль контроллеров. Дальше спорить «кто виноват» бессмысленно: нужен ответ
        /// самого рантайма на вопрос «какой профиль сейчас у этой руки В МОЁМ СЕАНСЕ».
        /// Метод берёт его через `xrGetCurrentInteractionProfile` (обёртка пакета OpenXR).
        /// </summary>
        public static string RuntimeProfileOf(string userPath)
        {
            try
            {
                // ПЕРВОЕ, ЧТО МОЖЕТ СЛОМАТЬСЯ, — ПРЕВРАЩЕНИЕ ПУТИ В ИДЕНТИФИКАТОР. Если оно не
                // удалось, «путь 0» ниже означал бы не ответ рантайма, а нашу же ошибку. Поэтому
                // эти два случая различаются в журнале явно.
                ulong userPathId = StringToPath(userPath);
                if (userPathId == 0)
                    return "путь «" + userPath + "» не превратился в идентификатор (xrStringToPath не сработал)";

                ulong profile = GetCurrentInteractionProfile(userPathId);
                if (profile == 0)
                    return "рантайм вернул ПУСТО (XR_NULL_PATH) для «" + userPath + "»";

                string name = PathToString(profile);
                return string.IsNullOrEmpty(name) ? "путь " + profile + " (без имени)" : name;
            }
            catch (System.Exception e)
            {
                return "ошибка опроса (" + e.GetType().Name + ")";
            }
        }

        /// <summary>Строка журнала: что рантайм называет текущим профилем для левой и правой руки.</summary>
        public static string DescribeRuntimeProfiles()
        {
            return "профиль рук по мнению рантайма: левая — " + RuntimeProfileOf(UserPaths.leftHand) +
                   " · правая — " + RuntimeProfileOf(UserPaths.rightHand);
        }

        // ==================================================== ПРАВИЛО ПРИВЯЗОК ПО РУКАМ

        /// <summary>
        /// КНОПКИ, КОТОРЫЕ ЕСТЬ ТОЛЬКО НА ПРАВОМ КОНТРОЛЛЕРЕ (a, b).
        /// Числа и буквы — из профиля PICO 4 Ultra, как у Oculus Touch: справа A/B, слева X/Y.
        /// </summary>
        public static readonly string[] RightHandOnlyComponents = { "a", "b" };

        /// <summary>
        /// КНОПКИ ТОЛЬКО ЛЕВОГО КОНТРОЛЛЕРА (x, y) и кнопка меню. Меню в официальном профиле
        /// PICO привязано к левой руке — повторяем, чтобы не спорить с рантаймом.
        /// </summary>
        public static readonly string[] LeftHandOnlyComponents = { "x", "y", "menu" };

        /// <summary>
        /// ИМЯ УПРАВЛЯЮЩЕГО ЭЛЕМЕНТА ИЗ ПУТИ: `/input/thumbstick/click` → `thumbstick`.
        /// Пусто, если путь не вида `/input/<элемент>/…`.
        /// </summary>
        public static string ComponentOf(string interactionPath)
        {
            if (string.IsNullOrEmpty(interactionPath)) return string.Empty;
            const string marker = "/input/";
            int start = interactionPath.IndexOf(marker, System.StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            start += marker.Length;
            int end = interactionPath.IndexOf('/', start);
            if (end < 0) end = interactionPath.Length;
            return interactionPath.Substring(start, end - start);
        }

        /// <summary>
        /// НАРУШЕНИЕ ПРАВИЛА «ПУТЬ КНОПКИ РУКИ → СВОЯ РУКА» или null, если всё верно.
        ///
        /// ПОЧЕМУ БЕЗ ЭТОГО ПРАВИЛА КОНТРОЛЛЕРЫ НЕ РАБОТАЮТ. Без `userPaths` Unity подставляет
        /// путь обеим рукам, и сочетание «/input/a/click на левой руке» профиль ByteDance не
        /// содержит. OpenXR на такое отвечает `XR_ERROR_PATH_UNSUPPORTED` и отклоняет ВЕСЬ
        /// набор привязок: пропадают и поза, и курок, и стик — контроллеры «не находятся»
        /// при исправном железе. Проверка чистая (только строки), поэтому живёт в тестах.
        /// </summary>
        public static string HandBindingViolation(string interactionPath, IList<string> userPaths)
        {
            string component = ComponentOf(interactionPath);
            if (component.Length == 0) return null;

            bool rightOnly = System.Array.IndexOf(RightHandOnlyComponents, component) >= 0;
            bool leftOnly = System.Array.IndexOf(LeftHandOnlyComponents, component) >= 0;
            if (!rightOnly && !leftOnly) return null;

            bool hasRight = Contains(userPaths, UserPaths.rightHand);
            bool hasLeft = Contains(userPaths, UserPaths.leftHand);
            bool noRestriction = !hasRight && !hasLeft;

            if (rightOnly && hasLeft)
                return "путь правой руки «" + interactionPath + "» привязан и к ЛЕВОЙ руке — " +
                       "такого сочетания в профиле нет, OpenXR отклонит весь набор привязок";
            if (leftOnly && hasRight)
                return "путь левой руки «" + interactionPath + "» привязан и к ПРАВОЙ руке — " +
                       "такого сочетания в профиле нет, OpenXR отклонит весь набор привязок";
            if (noRestriction)
                return "у пути «" + interactionPath + "» не указана рука: Unity подставит ОБЕ руки " +
                       "(OpenXRInput.cs:296), рантайм ответит XR_ERROR_PATH_UNSUPPORTED";
            return null;
        }

        private static bool Contains(IList<string> list, string value)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// СПЛОШНАЯ ПРОВЕРКА НАСТОЯЩЕЙ КАРТЫ ДЕЙСТВИЙ. Возвращает true, если ни одна привязка
        /// не нарушает правило; в <paramref name="report"/> — числа и перечень нарушений
        /// (числа с инвариантной культурой, чтобы строка журнала искалась одинаково везде).
        /// </summary>
        public static bool AuditHandBindings(out string report)
        {
            int bindings;
            int restricted;
            return AuditHandBindings(out report, out bindings, out restricted);
        }

        /// <summary>
        /// То же, но с числами в отдельных аргументах: тесту нужны не слова, а количество
        /// привязок, ограниченных по руке (иначе проверка «правило применено» ничего не значит).
        /// </summary>
        public static bool AuditHandBindings(out string report, out int bindingCount, out int handRestrictedCount)
        {
            return AuditHandBindings(BuildActionMap(), out report, out bindingCount, out handRestrictedCount);
        }

        /// <summary>
        /// Проверка произвольной карты действий. Отдельная перегрузка, потому что тип карты
        /// (`ActionMapConfig`) виден только внутри пакета OpenXR и наследникам — тест подать
        /// свою карту не может, поэтому правило проверяется отдельно и на строках
        /// (<see cref="HandBindingViolation"/>), а карта — этой перегрузкой.
        /// </summary>
        private static bool AuditHandBindings(ActionMapConfig actionMap, out string report,
                                              out int bindingCount, out int handRestrictedCount)
        {
            int bindings = 0;
            int handRestricted = 0;
            var violations = new List<string>();

            if (actionMap.actions != null)
            {
                for (int a = 0; a < actionMap.actions.Count; a++)
                {
                    ActionConfig action = actionMap.actions[a];
                    if (action == null || action.bindings == null) continue;

                    for (int b = 0; b < action.bindings.Count; b++)
                    {
                        ActionBinding binding = action.bindings[b];
                        if (binding == null || string.IsNullOrEmpty(binding.interactionPath)) continue;

                        bindings++;
                        if (binding.userPaths != null && binding.userPaths.Count > 0) handRestricted++;

                        string violation = HandBindingViolation(binding.interactionPath, binding.userPaths);
                        if (violation != null) violations.Add(violation);
                    }
                }
            }

            bindingCount = bindings;
            handRestrictedCount = handRestricted;

            report = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "привязки профиля: всего {0} · с ограничением по руке {1} · нарушений {2}" +
                (violations.Count > 0 ? " · " + string.Join("; ", violations.ToArray()) : string.Empty),
                bindings, handRestricted, violations.Count);

            return violations.Count == 0;
        }

        // ==================================================== ПРОБА ДЛЯ ТЕСТА

        /// <summary>
        /// Нарушение в НАСТОЯЩЕЙ карте действий или null. Публичная обёртка над аудитом:
        /// удобна и в журнале, и в тесте — возвращает первую причину, а не только «ложь».
        /// </summary>
        public static string FirstHandBindingViolation()
        {
            string report;
            return AuditHandBindings(out report) ? null : report;
        }
    }
}
