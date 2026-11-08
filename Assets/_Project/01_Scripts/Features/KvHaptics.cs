using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>Сила вибрации (ЭТАП 17 ТЗ).</summary>
    public enum KvHapticStrength { Light = 0, Medium = 1, Hard = 2 }

    /// <summary>
    /// ВИБРАЦИЯ КОНТРОЛЛЕРОВ (ЭТАП 17 ТЗ) — ЗАГОТОВКА.
    ///
    /// По ТЗ: «пока что — заготовки, активация при VR/MR». В проекте нет пакета `com.meta.*`
    /// (Meta-контур подключён через OpenXR-слой), а PICO SDK не установлен, поэтому прямой
    /// зависимости от их API здесь нет — вместо неё РЕФЛЕКСИЯ:
    ///   • ищутся компоненты контроллеров (XR Interaction Toolkit: `XRBaseController`,
    ///     `ActionBasedController`, `XRController`, Meta/PICO-контроллеры) по имени типа;
    ///   • у найденного компонента вызывается первый подходящий метод
    ///     `SendHapticImpulse(float amplitude, float duration)`
    ///     (XRIT v3: `SendHapticImpulse(float, float)`; Meta: `SendHapticImpulse(float, float, float)`);
    ///   • если ничего не найдено — вибрация НЕ отправляется, в консоль пишется одна строка
    ///     за сессию (чтобы не спамить), и работа продолжается как обычно.
    ///
    /// Такой «мягкий» мост не ломает десктопный запуск и автоматически заработает, когда
    /// в сцену добавят VR/MR-контроллеры, — правки кода не потребуются.
    /// </summary>
    public class KvHaptics
    {
        /// <summary>Включена ли вибрация (по ТЗ — выключатель в настройках).</summary>
        public bool Enabled = true;
        /// <summary>Писать в консоль, когда вибрация недоступна.</summary>
        public bool logUnavailable = true;

        /// <summary>Длительности импульсов, с.</summary>
        public float lightDuration = 0.04f;
        public float mediumDuration = 0.09f;
        public float hardDuration = 0.16f;

        /// <summary>Амплитуды импульсов 0…1.</summary>
        public float lightAmplitude = 0.20f;
        public float mediumAmplitude = 0.45f;
        public float hardAmplitude = 0.85f;

        private readonly List<MonoBehaviour> controllers = new List<MonoBehaviour>();
        private readonly List<MethodInfo> hapticMethods = new List<MethodInfo>();
        private float controllerScanTimer = float.NegativeInfinity;
        private bool warned;

        /// <summary>
        /// УМЕНЬШЕНИЕ ОБХОДА СЦЕНЫ ДО ИЗВЕСТНЫХ ТИПОВ (P2-11, 11.11.2026).
        ///
        /// ЧТО БЫЛО. `Scan()` делал `FindObjectsByType&lt;MonoBehaviour&gt;` — обход ВСЕХ скриптов
        /// сцены (в MR их тысячи), затем для каждого — `GetType().Name`, четыре `IndexOf` по
        /// строке и `type.GetMethods()` (рефлексия!) чтобы понять, годится ли он. А `IsActive` —
        /// ГЕТТЕР — запускал этот скан раз в две секунды, то есть «прочитать свойство» означало
        /// обойти сцену с рефлексией прямо в кадре.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. Тактильная отдача — часть обратной связи оператора (стоп,
        /// столкновение). Обход сцены с рефлексией на главном потоке давал «столб» кадра ровно
        /// в этот момент: предохранитель сработал, а оператор увидел замирание вместо отклика.
        ///
        /// ТЕПЕРЬ ищутся ТОЛЬКО известные типы контроллеров (их немного), метод для вызова
        /// находится ОДИН РАЗ при сканировании и лежит рядом с компонентом, а геттер `IsActive`
        /// ничего не запускает — он читает готовое состояние.
        /// </summary>
        public static readonly string[] KnownControllerTypeNames =
        {
            "UnityEngine.XR.Interaction.Toolkit.XRBaseController",
            "UnityEngine.XR.Interaction.Toolkit.ActionBasedController",
            "UnityEngine.XR.Interaction.Toolkit.XRController",
            "UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor",
            "UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor",
            "Unity.XR.PXR.PXR_Controller",
            "Unity.XR.PXR.PXR_Interactor",
        };

        /// <summary>Есть ли найденный контроллер (для диагностики/панели функций).</summary>
        public bool Available { get { return controllers.Count > 0; } }
        public int ControllerCount { get { return controllers.Count; } }

        /// <summary>
        /// ЕСТЬ ЛИ КОНТРОЛЛЕРЫ XR В СЦЕНЕ — БЕЗ ОБХОДА СЦЕНЫ (P2-11). Геттер только читает
        /// результат последнего сканирования; пересборку списка делают `Pulse` и
        /// `RefreshControllers` (событие, а не чтение свойства).
        /// </summary>
        public bool IsActive { get { return controllers.Count > 0; } }

        /// <summary>
        /// ПОРА ЛИ ПЕРЕСКАНИРОВАТЬ СЦЕНУ — чистая функция (проверяется тестом P2-11): время ушло
        /// назад (новый прогон) — да, иначе только по истечении интервала.
        /// </summary>
        public static bool IsScanStale(float now, float lastScan, float intervalSeconds)
        {
            if (float.IsNaN(now) || float.IsNaN(lastScan)) return true;
            if (now < lastScan) return true;
            float interval = intervalSeconds > 0f ? intervalSeconds : 0f;
            return now - lastScan >= interval;
        }

        /// <summary>Пересобрать список контроллеров немедленно (событие: сцена/настройки изменились).</summary>
        public void RefreshControllers()
        {
            Scan();
        }

        /// <summary>Отправить импульс (light/medium/hard).</summary>
        public void Pulse(KvHapticStrength strength, string source = null)
        {
            if (!Enabled) return;

            float amplitude;
            float duration;
            switch (strength)
            {
                case KvHapticStrength.Hard: amplitude = hardAmplitude; duration = hardDuration; break;
                case KvHapticStrength.Medium: amplitude = mediumAmplitude; duration = mediumDuration; break;
                default: amplitude = lightAmplitude; duration = lightDuration; break;
            }
            Pulse(amplitude, duration, source);
        }

        public void Pulse(float amplitude, float duration, string source = null)
        {
            if (!Enabled) return;

            // Пересканирование — по интервалу, а не в геттере (P2-11).
            if (IsScanStale(Time.realtimeSinceStartup, controllerScanTimer, RescanIntervalSeconds))
                Scan();

            if (controllers.Count == 0)
            {
                if (logUnavailable && !warned)
                {
                    warned = true;
                    Debug.Log("[Haptics] контроллеры VR/MR не найдены — вибрация не отправляется" +
                              (string.IsNullOrEmpty(source) ? "" : " (событие: " + source + ")") +
                              ". Заготовка активна: при добавлении контроллеров XR импульсы уйдут автоматически.");
                }
                return;
            }

            bool sent = false;
            for (int i = 0; i < controllers.Count; i++)
            {
                MonoBehaviour controller = controllers[i];
                if (controller == null) continue;
                MethodInfo method = i < hapticMethods.Count ? hapticMethods[i] : null;
                if (SendTo(controller, method, amplitude, duration)) sent = true;
            }

            if (sent && !string.IsNullOrEmpty(source))
                Debug.Log("[Haptics] импульс " + amplitude.ToString("0.00") + " · " +
                          (duration * 1000f).ToString("0") + " мс · " + source);
        }

        // ---- события этапов (ТЗ): выбор точки — слабая, подтверждение — сильнее,
        //      столкновение/ошибка — резкая короткая, аварийный стоп — самая сильная.
        public void OnPointSelected() { Pulse(KvHapticStrength.Light, "выбор точки"); }
        public void OnTrajectoryConfirmed() { Pulse(KvHapticStrength.Medium, "подтверждение траектории"); }
        public void OnMotionStarted() { Pulse(0.35f, 0.12f, "старт движения"); }
        public void OnError() { Pulse(KvHapticStrength.Hard, "ошибка"); }
        public void OnEmergencyStop() { Pulse(1f, 0.30f, "аварийная остановка"); }

        /// <summary>Как часто пересканировать сцену на контроллеры, с (P2-11).</summary>
        public const float RescanIntervalSeconds = 2f;

        /// <summary>
        /// НАЙТИ КОНТРОЛЛЕРЫ — ТОЛЬКО ИЗВЕСТНЫХ ТИПОВ (P2-11). Прежний обход
        /// `FindObjectsByType&lt;MonoBehaviour&gt;` + `GetMethods()` на каждом скрипте сцены заменён
        /// на поиск по нескольким известным типам, а метод вибрации ищется один раз и
        /// кэшируется рядом с компонентом (`hapticMethods`), чтобы отправка импульса не
        /// занималась рефлексией заново.
        /// </summary>
        private void Scan()
        {
            controllers.Clear();
            hapticMethods.Clear();
            controllerScanTimer = Time.realtimeSinceStartup;

            for (int t = 0; t < KnownControllerTypeNames.Length; t++)
            {
                Type type = ResolveType(KnownControllerTypeNames[t]);
                if (type == null) continue;                    // пакета нет — законно, не ошибка

                MethodInfo method = FindHapticMethod(type);
                if (method == null) continue;                  // тип есть, но вибрации не умеет

                UnityEngine.Object[] found;
                try
                {
                    // Перегрузка БЕЗ `FindObjectsSortMode`: в Unity 6 она объявлена устаревшей
                    // (CS0618), а гейт проекта требует ноль предупреждений компилятора.
                    found = UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include);
                }
                catch (Exception)
                {
                    continue;   // тип не компонент/сцена занята — пропускаем, падать нечем
                }

                for (int i = 0; i < found.Length; i++)
                {
                    MonoBehaviour mb = found[i] as MonoBehaviour;
                    if (mb == null) continue;
                    controllers.Add(mb);
                    hapticMethods.Add(method);
                }
            }
        }

        private static readonly Dictionary<string, Type> resolvedTypes = new Dictionary<string, Type>();

        /// <summary>Найти тип по полному имени среди загруженных сборок (результат кэшируется).</summary>
        private static Type ResolveType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;

            Type cached;
            if (resolvedTypes.TryGetValue(fullName, out cached)) return cached;

            Type found = Type.GetType(fullName, false);
            if (found == null)
            {
                System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length && found == null; i++)
                {
                    try { found = assemblies[i].GetType(fullName, false); }
                    catch (Exception) { found = null; }
                }
            }

            resolvedTypes[fullName] = found;   // null тоже кэшируем: пакета нет — искать нечего
            return found;
        }

        private static MethodInfo FindHapticMethod(Type type)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo m = methods[i];
                if (m.Name != "SendHapticImpulse") continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length >= 2 &&
                    ps[0].ParameterType == typeof(float) && ps[1].ParameterType == typeof(float))
                    return m;
            }
            return null;
        }

        /// <summary>
        /// ОТПРАВИТЬ ИМПУЛЬС В КОНТРОЛЛЕР. Метод для вызова берётся ИЗ КЭША сканирования
        /// (`hapticMethods`), а не ищется рефлексией на каждое нажатие (P2-11): раньше каждый
        /// импульс заново вызывал `GetMethods()` — перечисление всех методов типа с аллокацией.
        /// </summary>
        private static bool SendTo(MonoBehaviour controller, MethodInfo method,
            float amplitude, float duration)
        {
            try
            {
                if (method == null) return false;
                ParameterInfo[] ps = method.GetParameters();
                object[] args = new object[ps.Length];
                args[0] = Mathf.Clamp01(amplitude);
                args[1] = Mathf.Max(0.001f, duration);
                for (int i = 2; i < ps.Length; i++)
                    args[i] = ps[i].ParameterType == typeof(float) ? (object)0f
                        : ps[i].ParameterType == typeof(uint) ? (object)0u
                        : null;
                method.Invoke(controller, args);
                return true;
            }
            catch (Exception)
            {
                return false;   // несовместимая подпись — молча пропускаем этот контроллер
            }
        }
    }
}
