using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// КАМЕРА ШЛЕМА (этап 1 плана XR, сессия §28).
    ///
    /// ЧТО ДЕЛАЕТ. Ставит камеру в правильное место и сообщает о состоянии XR одной строкой.
    /// Позу камеры в шлеме выставляет САМ РАНТАЙМ OpenXR (система трекинга пишет в
    /// `Camera.transform`) — здесь ничего не переписывается: попытка «двигать камеру руками»
    /// в VR даёт тряску и «плывущую» картинку.
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ ПРИ СТАРТЕ и попадает в журнал:
    ///   • активен ли OpenXR (без него в шлеме чёрный экран — и это первая причина, которую надо исключить);
    ///   • включено ли стерео SinglePassInstanced (иначе картинка для двух глаз считается дважды
    ///     и частота кадров падает почти вдвое);
    ///   • обнаружен ли шлем и контроллеры.
    ///
    /// ПОЧЕМУ НЕ XROrigin ИЗ XRI. `XROrigin` тянет за собой соглашение о префабе рига и
    /// компоненты XRI. Здесь риг собран из трёх собственных компонентов, и это осознанный
    /// минимум: меньше узлов — меньше того, что нельзя проверить без устройства.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrRig : MonoBehaviour
    {
        /// <summary>Камера шлема (обычно этот же объект).</summary>
        public Camera headCamera;

        /// <summary>Сообщать состояние в консоль при старте (в сборке — один раз, без спама).</summary>
        public bool logOnStart = true;

        /// <summary>Целевая частота кадров для шлема: PICO 4 Ultra работает на 72/90 Гц.</summary>
        public int targetFrameRate = 72;

        /// <summary>Резервный масштаб разрешения глаза при низком пресете (1 — без изменений).</summary>
        public float eyeTextureResolutionScale = 1f;

        /// <summary>
        /// СКОЛЬКО ЖДАТЬ ГОТОВНОСТИ XR ПЕРЕД ОПРЕДЕЛЕНИЕМ РЕЖИМА, секунды (S45).
        ///
        /// ЗАЧЕМ. На первом запуске PICO показывает системный диалог разрешений; пока он открыт,
        /// приложение на паузе и XR не активен. Если определить режим в этот момент, получится
        /// «сквозной вид НЕТ» при выданном расширении — и приложение навсегда останется в VR.
        /// Десяти секунд хватает и на диалог, и на подъём подсистемы; если XR так и не поднялся —
        /// режим VR и честная строка в журнале.
        /// </summary>
        public float modeDetectionTimeoutSeconds = 12f;

        /// <summary>OpenXR найден и активен.</summary>
        public bool OpenXrActive { get; private set; }

        /// <summary>Стерео включено в режиме SinglePassInstanced.</summary>
        public bool SinglePassInstanced { get; private set; }

        /// <summary>Строка состояния для журнала (та же, что попадает в отчёт).</summary>
        public string Status { get; private set; }

        void Awake()
        {
            if (headCamera == null) headCamera = GetComponent<Camera>();
            if (headCamera == null) headCamera = GetComponentInChildren<Camera>();

            // ИСПРАВЛЕНО 01.11.2026 (S44, живой шлем): НАЧАЛО ОТСЛЕЖИВАНИЯ — НЕ КАМЕРА.
            //
            // СИМПТОМ У ОПЕРАТОРА (дословно): «всё крутится», «картинка не стабильная».
            // ЖУРНАЛ ШЛЕМА:
            //     панель от глаз: 1.12 м → 4.49 м → 155.01 м → 394.11 м
            //     панель в (0.08, 0.88, 0.68) — НЕ МЕНЯЕТСЯ ни в одной строке
            //     курс шлема: 0.0° → 353.4° → 287.6° → 354.1°
            //
            // ПРИЧИНА. Этот компонент висит на объекте «XrCamera» — то есть НА САМОЙ КАМЕРЕ
            // (проверено по файлу сцены: `m_Name: XrCamera` содержит `KvXrRig`). А камера —
            // РЕБЁНОК объекта «XrRig».
            //
            // Прежняя строка `KvXrInputSource.TrackingOrigin = transform` означала:
            //     «начало отслеживания = КАМЕРА».
            // Дальше `ToWorld` делает `position = TrackingOrigin.TransformPoint(position)`, то есть
            // переводит позу трекинга в мир ОТНОСИТЕЛЬНО КАМЕРЫ. Но камеру же и ставят по этой позе
            // в `KvXrHeadTracking`. Получается круг:
            //     камера := поза + позиция_камеры  →  камера уезжает  →  следующая поза считается
            //     от уехавшей камеры  →  ещё дальше.
            // Ошибка накапливается каждый кадр — ровно то, что видит оператор.
            //
            // ПОЧЕМУ НЕ ПРОЯВЛЯЛОСЬ РАНЬШЕ. Пока камера стояла в нуле, `camera.TransformPoint(P)`
            // совпадало с `P`, и круг не замыкался. Стоило камере уехать хоть немного — пошла
            // раскрутка. А уезжает она сразу: камера висит на высоте глаз, а не в начале координат.
            //
            // ТЕПЕРЬ начало отслеживания — КОРЕНЬ РИГА (родитель камеры), как и задумано:
            // поза трекинга отсчитывается от пола под оператором, а не от его собственных глаз.
            KvXrInputSource.TrackingOrigin = ResolveTrackingOrigin();
            installedTrackingOrigin = true;          // 30.10.2026: чтобы OnDestroy снял именно свою ссылку

            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;

            DetectXr();
        }

        /// <summary>
        /// НАЙТИ НАСТОЯЩЕЕ НАЧАЛО ОТСЛЕЖИВАНИЯ (создано 01.11.2026).
        ///
        /// ПОЧЕМУ НЕ `transform`. Компонент может висеть на камере (так и есть в сцене `XrMain`:
        /// объект `XrCamera`), а камера — ребёнок рига. Если взять `transform`, началом отслеживания
        /// станет сама камера, и поза начнёт отсчитываться от глаз оператора — получится накопление
        /// и раскрутка мира.
        ///
        /// ПРАВИЛА ПОИСКА (по порядку):
        ///   1. если на этом объекте есть `Camera` — берём РОДИТЕЛЯ (риг, в котором лежит камера);
        ///   2. иначе — сам объект (компонент уже на риге, всё верно).
        ///
        /// Отдельным методом, а не строкой в `Awake`, чтобы это можно было проверить тестом:
        /// в EditMode `AddComponent` не вызывает `Awake`, и проверять поведение внутри него нельзя.
        /// </summary>
        public static Transform ResolveTrackingOrigin(Transform self, bool selfHasCamera)
        {
            if (self == null) return null;
            if (!selfHasCamera) return self;

            // На объекте камера — значит начало отслеживания выше: риг, внутри которого она лежит.
            return self.parent != null ? self.parent : self;
        }

        private Transform ResolveTrackingOrigin()
        {
            Camera ownCamera = GetComponent<Camera>();
            return ResolveTrackingOrigin(transform, ownCamera != null);
        }

        void Start()
        {
            // §37: ЧТО ЖИВЁТ ТОЛЬКО В РАНТАЙМЕ. Модели контроллеров и исполнитель жестов создаются
            // ЗДЕСЬ, а не лежат в сцене: файл XrMain.unity не правится вовсе, поэтому принятое в
            // §35–§36 не может сломаться от правки сцены. Оба вызова идемпотентны.
            EnsureRuntimeComponents();

            if (logOnStart)
            {
                Debug.Log("[KvXr] " + Status);
                Debug.Log("[KvXr] " + KvXrInputSource.Describe());
                Debug.Log("[KvXr] " + KvXrSettings.BuildRigConfig().Describe());
                Debug.Log("[KvXr] " + KvXrSettings.DescribePointer());
            }

            // 02.11.2026 (S45): РЕЖИМ ОПРЕДЕЛЯЕТСЯ ПОСЛЕ ГОТОВНОСТИ XR, А НЕ «НА ПЕРВОМ КАДРЕ».
            //
            // НАЙДЕНО НА ЖИВОМ ШЛЕМЕ. На первом запуске PICO показывает системный диалог
            // разрешений. Пока он открыт, приложение на паузе, а `XRSettings.isDeviceActive` —
            // ЛОЖЬ. Проба, выполненная в этот момент, честно отвечала «сквозной вид НЕТ», и режим
            // навсегда оставался VR: оператор нажимал «Разрешить», возвращался в приложение — и
            // видел виртуальную лабораторию, хотя шлем умеет MR (журнал 02.11.2026: строка
            // «РЕЖИМ: ВИРТУАЛЬНАЯ РЕАЛЬНОСТЬ … сквозной вид НЕТ» при выданном XR_FB_passthrough).
            //
            // Поэтому проба ждёт, пока XR реально поднимется (после диалога/паузы), и только потом
            // решает режим. Ожидание ограничено: если XR не поднялся — режим VR, честная строка.
            StartCoroutine(DetectModeWhenXrReady());
        }

        /// <summary>
        /// ОПРЕДЕЛИТЬ РЕЖИМ, КОГДА XR ГОТОВ. Ждёт `XrRunning` не дольше `modeDetectionTimeoutSeconds`:
        /// системный диалог разрешений ставит приложение на паузу, и проба до него бессмысленна.
        /// </summary>
        private System.Collections.IEnumerator DetectModeWhenXrReady()
        {
            float deadline = Time.unscaledTime + Mathf.Max(1f, modeDetectionTimeoutSeconds);
            while (!KvXrCapabilities.XrRunning && Time.unscaledTime < deadline)
                yield return null;

            KvXrPlatformModeSelector.EnsureApplied();

            // СТРОКА ПРОГРАММЫ (решение владельца 04.11.2026): в журнале сразу видно, какая
            // версия запущена и в каком режиме, — не нужно догадываться по косвенным строкам.
            Debug.Log("[KvXr] " + KvXrGroupPanel.ModeLine());

            if (!logOnStart) yield break;
            Debug.Log("[KvXr] режим определён после готовности XR: " +
                      (KvXrCapabilities.XrRunning ? "XR активен" : "XR НЕ активен за " +
                       modeDetectionTimeoutSeconds.ToString("0") + " с"));
        }

        /// <summary>
        /// СОЗДАТЬ ТО, ЧТО НЕ ЛЕЖИТ В СЦЕНЕ (§37): модели контроллеров, кисти и жесты. Вынесено
        /// отдельным методом, чтобы это было видно в одном месте: сцена не меняется, всё собирается кодом.
        /// </summary>
        private void EnsureRuntimeComponents()
        {
            Camera camera = headCamera != null ? headCamera : Camera.main;
            KvXrControllerVisuals.Ensure(transform, camera);
            KvXrHands.Ensure(transform, camera);
            KvXrGestures.Ensure(transform);

            // §44: подача взгляда в сервис отслеживания. Без этого компонента взгляд, фиксации и
            // выбор взглядом получали данные только из диагностики — то есть в сборке молчали.
            KvXrGazeFeed.Ensure(transform);
            KvXrSecondaryRay.Ensure(transform, camera);   // §39: второй луч — только указатель

            // §39: курсору на конце луча нужна камера, чтобы разворачиваться к оператору.
            KvXrUiPointer pointer = Object.FindAnyObjectByType<KvXrUiPointer>();
            if (pointer != null && pointer.headCamera == null) pointer.headCamera = camera;

            // §41: СТАРТОВОЕ МЕНЮ перед оператором (указание владельца: «нет стартового меню —
            // сначала сделаем его»). Создаётся в рантайме, сцена не меняется.
            KvXrStartMenu.Ensure(transform, camera);

            // §41: ОКНА В МИРЕ (указание владельца: кнопки должны выдавать окна, которые не крепятся
            // ко взгляду и остаются висеть, когда оператор отходит).
            KvXrWindowHost.Ensure(camera);

            // ВЛАДЕЛЕЦ 04.11.2026 (вечер): «убери сразу добавленные столы — они должны собираться
            // (стол + робот) САМИМ ПОЛЬЗОВАТЕЛЕМ, а не быть сначала готовыми». Поэтому старт снова
            // ПУСТОЙ, как в ПК: столы сцены вместе с их роботами гасятся `KvXrSceneEmpty`, а сборка
            // идёт из стартового меню («Стол», «Авто стол», «Добавить робота», «Запустить стенд»).
            // Сцена `XrMain.unity` не меняется: гашение только в рантайме.
            EnsureSceneEmpty();
            Debug.Log("[KvXr] СЦЕНА: старт ПУСТОЙ — стол и робота добавьте из меню " +
                      "(решение владельца 04.11.2026; раньше они стояли готовыми)");

            // §42: ПОТОК ЭТАПОВ — без него в шлеме не работала ни одна визуализация (пятна, границы,
            // зоны, сингулярности): службы привязываются к потоку, а его в XR-сцене не было вовсе.
            EnsureFlowBridge(camera);
        }

        /// <summary>
        /// СОЗДАТЬ «ПУСТО ПРИ ЗАПУСКЕ», если его нет (§42). Столы сцены и их роботы гасятся —
        /// пользователь собирает стенд сам из меню (решение владельца 04.11.2026, вечер).
        /// </summary>
        private static void EnsureSceneEmpty()
        {
            KvXrSceneEmpty existing = Object.FindAnyObjectByType<KvXrSceneEmpty>(FindObjectsInactive.Include);
            if (existing != null) return;

            var go = new GameObject("KvXrSceneEmpty");
            go.AddComponent<KvXrSceneEmpty>();
        }

        /// <summary>Создать мост потока этапов, если его нет (§42).</summary>
        private void EnsureFlowBridge(Camera camera)
        {
            KvXrFlowBridge existing = Object.FindAnyObjectByType<KvXrFlowBridge>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.headCamera == null) existing.headCamera = camera;
                return;
            }

            var go = new GameObject("KvXrFlowBridge");
            KvXrFlowBridge bridge = go.AddComponent<KvXrFlowBridge>();
            bridge.headCamera = camera;
        }

        private float lastRecoverTime = -100f;

        /// <summary>
        /// 30.10.2026: рига больше нет — начало отслеживания обязано быть снято.
        ///
        /// ПОЧЕМУ. `KvXrInputSource.TrackingOrigin` — статическое поле, и через него проходят
        /// ВСЕ позы трекинга (руки, контроллеры, лучи). Если риг пересоздан или выгружен, а поле
        /// осталось, трансформация идёт через уничтоженный объект — это `MissingReferenceException`
        /// каждый кадр, а не «позы не едут». Снимаем ссылку только если она наша: другой риг
        /// мог уже поставить свою.
        ///
        /// ПОЧЕМУ ФЛАГ, А НЕ СРАВНЕНИЕ. У уничтожаемого объекта Unity подменяет результат
        /// сравнения (`obj == null` — истина), поэтому сравнение с `transform` в `OnDestroy`
        /// может не сработать: прогон 30.10.2026 показал, что ссылка оставалась. Флаг ставится
        /// в `Awake` — там риг точно тот самый.
        /// </summary>
        private bool installedTrackingOrigin;

        void OnDestroy()
        {
            KvXrInputSource.ClearOriginIf(installedTrackingOrigin, transform);
            installedTrackingOrigin = false;
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) Recover();
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) Recover();
        }

        /// <summary>
        /// ВОССТАНОВЛЕНИЕ ПОСЛЕ ПРОБУЖДЕНИЯ ШЛЕМА (§37). Жалоба владельца: «после ухода в спящий
        /// режим указка на контроллерах не восстанавливается и веер тоже». Сон останавливает
        /// приложение и может пересоздать сеанс, устройства и подсистемы — всё, что было закэшировано
        /// (список устройств, закреплённые стороны, подсистема кистей, состояние указателя,
        /// положение панели), к этому моменту может быть негодным.
        ///
        /// ЧТО ЗДЕСЬ ДЕЛАЕТСЯ: пересканировать ввод, снять закрепление сторон (пусть определятся
        /// заново), поднять подсистему кистей, сбросить жесты и указатель, поставить панель заново —
        /// и напечатать ОДНУ строку с числами, по которой видно, что восстановление произошло.
        /// </summary>
        public void Recover()
        {
            if (Time.realtimeSinceStartup - lastRecoverTime < 1f) return;   // пауза и фокус приходят парой
            lastRecoverTime = Time.realtimeSinceStartup;

            KvXrInputSource.ForceScan();
            KvXrInputSource.UnlockSides();

            KvXrHands hands = Object.FindAnyObjectByType<KvXrHands>();
            if (hands != null) hands.Recover();

            KvXrGestures gestures = Object.FindAnyObjectByType<KvXrGestures>();
            if (gestures != null) gestures.ResetState();

            KvXrGroupPanel panel = Object.FindAnyObjectByType<KvXrGroupPanel>();
            if (panel != null) panel.Reanchor();

            KvXrUiPointer pointer = Object.FindAnyObjectByType<KvXrUiPointer>();
            if (pointer != null) pointer.ResetState();

            DetectXr();
            Debug.Log("[KvXr] ПРОБУЖДЕНИЕ: устройства пересмотрены · " + KvXrInputSource.Describe() +
                      " · " + KvXrInputSource.DescribeSides() +
                      " · кисти " + (KvXrHands.SubsystemRunning ? "подняты" : "НЕТ") +
                      " · панель и указатель поставлены заново");
        }

        /// <summary>
        /// ПОКА ИДУТ ПЕРВЫЕ СЕКУНДЫ РАБОТЫ — ПЕЧАТАТЬ СОСТАВ ВВОДА. Шлем надевают уже после
        /// запуска приложения, и контроллеры появляются в системе не сразу: одиночная строка
        /// в `Start()` не отвечает на вопрос «контроллеров нет — это дефект или устройство спит».
        /// Диагностика сама ограничена по времени и числу строк, спама в журнале не будет.
        /// </summary>
        void Update()
        {
            // §40: РЕЖИМ УПРАВЛЕНИЯ — контроллеры в руках или кисти. От него зависят луч (контроллер
            // или палец), крепление панели (контроллер или ладонь) и видимость второго луча.
            KvXrMode.Tick();

            // §37: ФАЙЛ ПОДСТРОЙКИ. Читается не чаще раза в 2 с; если значения изменились —
            // в журнал уходит строка с числами. Это механизм опросов радиуса панели без пересборки.
            string tuning = KvXrTuning.Tick();
            if (!string.IsNullOrEmpty(tuning)) Debug.Log(tuning);

            KvXrInputSource.TickDiagnostics();

            // СТАНДАРТ Quest/PICO (27.10.2026): кнопки A/B/X/Y — подтвердить/назад/меню/панель.
            KvXrActions.Tick();

            // СТРОКИ ОБ OPENXR И ВВОДЕ (сессия §36): выдано ли расширение контроллеров,
            // не нарушено ли правило привязок по рукам, сколько устройств видит XR-подсистема.
            // Без этих строк «контроллеров нет» остаётся догадкой — ровно как было в §35.
            KvXrOpenXrDiagnostics.Tick();
        }

        /// <summary>
        /// Определить состояние XR-подсистемы. Проверки сделаны ЧЕРЕЗ ОТРАЖЕНИЕ и
        /// `XRDisplaySubsystem`, без прямых ссылок на типы OpenXR: иначе сборка зависела бы
        /// от версии пакета, а проект этого избегает (тем же способом сделан `KvFoveatedRendering`).
        /// </summary>
        public void DetectXr()
        {
            bool stereo = false;
            string stereoMode = "нет XR";

            var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
            UnityEngine.SubsystemManager.GetSubsystems(displays);

            for (int i = 0; i < displays.Count; i++)
            {
                var d = displays[i];
                if (d == null || !d.running) continue;
                stereo = true;
                stereoMode = "XRDisplaySubsystem активна";
                break;
            }

            // Признак OpenXR — активный лоадер XR Management: подсистема отображения может
            // стартовать позже, а лоадер известен сразу. Проверка идёт по имени типа, без
            // прямой ссылки на сборку OpenXR (так же сделан `KvFoveatedRendering`).
            OpenXrActive = false;
            var settings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (settings != null && settings.Manager != null)
            {
                var loaders = settings.Manager.activeLoaders;
                if (loaders != null)
                {
                    for (int i = 0; i < loaders.Count; i++)
                    {
                        var loader = loaders[i];
                        if (loader == null) continue;
                        string type = loader.GetType().FullName ?? string.Empty;
                        if (type.IndexOf("OpenXR", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            OpenXrActive = true;
                            break;
                        }
                    }
                }
            }

            SinglePassInstanced = stereo;
            Status = string.Format(
                "Камера шлема: OpenXR {0} · стерео {1} ({2}) · цель {3} кадров/с · шлем {4}",
                OpenXrActive ? "активен" : "НЕ АКТИВЕН",
                SinglePassInstanced ? "включено" : "выключено",
                stereoMode,
                targetFrameRate,
                KvXrInputSource.HeadsetPresent ? "обнаружен" : "не обнаружен");
        }

        /// <summary>
        /// Резервный масштаб разрешения глаза. В HDRP на слабом GPU это главный рычаг
        /// производительности в шлеме; значение ограничено, чтобы картинка не превратилась
        /// в кашу (ниже 0,6 читать интерфейс уже нельзя).
        /// </summary>
        public void SetEyeResolutionScale(float scale)
        {
            eyeTextureResolutionScale = Mathf.Clamp(scale, 0.6f, 1.4f);

            var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
            UnityEngine.SubsystemManager.GetSubsystems(displays);
            for (int i = 0; i < displays.Count; i++)
            {
                var d = displays[i];
                if (d == null || !d.running) continue;
                d.scaleOfAllRenderTargets = eyeTextureResolutionScale;
            }
        }
    }
}
