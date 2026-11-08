using System;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace KazistovVvXr
{
    /// <summary>
    /// БЭКЕНД СКВОЗНОГО ВИДА: ЧЕЙ РАНТАЙМ ВЫДАЛ MR (создано 02.11.2026, сессия S45).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ СЛОЙ. Включение сквозного вида — единственное место в переносе MR, которое
    /// нельзя написать «вообще»: у PICO и Meta разные API, и вызов «не того» API — это не ошибка
    /// компиляции, а чёрный экран на устройстве. Поэтому решение «кому звонить» принимается по
    /// ФАКТАМ рантайма (имя рантайма, модель устройства, PICO-специфичные расширения), а сам
    /// выбор — чистая функция <see cref="ChooseBackend(string,string,bool,bool)"/>, проверяемая
    /// тестом без шлема.
    ///
    /// ЧЕСТНО О ГРАНИЦЕ. Если сквозной вид не включился, приложение НЕ притворяется MR: оно пишет
    /// в журнал причину числом/строкой и остаётся работоспособным.
    /// </summary>
    public enum KvMrBackend
    {
        None = 0,
        Pico = 1,
        Meta = 2
    }

    /// <summary>
    /// ВКЛЮЧЕНИЕ И ВЫКЛЮЧЕНИЕ СКВОЗНОГО ВИДА (PICO 4 Ultra и Meta Quest 3).
    ///
    /// ПОЧЕМУ НЕ ОДНА СТРОКА. На PICO сквозной вид — это фича OpenXR `PassthroughFeature` из пакета
    /// PICO OpenXR: она создаёт полноэкранный пасс-слой и требует, чтобы фон камеры был чёрным с
    /// нулевой альфой (иначе пасс-слой перекрыт). На Meta тот же по смыслу вид даёт AR Foundation:
    /// камера-подсистема Meta OpenXR рисует реальный мир фоном (ARCameraBackground). Это ОДИН
    /// проект с двумя режимами, поэтому ветвление спрятано здесь, а не размазано по сцене.
    ///
    /// ЧТО НЕ ДЕЛАЕТСЯ ПОДМЕНОЙ ЦВЕТА. `Camera.backgroundColor = серый` здесь нет и не будет: это
    /// прямо запрещено ТЗ (S3.1). Ветка PICO включает настоящий пасс-слой, ветка Meta — камеру.
    /// </summary>
    public static class KvMrRuntime
    {
        /// <summary>Кем является рантайм — по фактам, а не по настройке.</summary>
        public static KvMrBackend Backend { get; private set; }

        /// <summary>Сквозной вид включён и подтверждён (а не «мы попытались»).</summary>
        public static bool PassthroughOn { get; private set; }

        /// <summary>Последняя причина отказа — для журнала и экрана состояния.</summary>
        public static string LastError { get; private set; }

        /// <summary>Строка состояния одной строкой (её печатает `KvXrMrRoom`).</summary>
        public static string Status
        {
            get
            {
                string backend;
                switch (Backend)
                {
                    case KvMrBackend.Pico: backend = "PICO"; break;
                    case KvMrBackend.Meta: backend = "Meta"; break;
                    default: backend = "не определён"; break;
                }

                return "MR-бэкенд: " + backend +
                       " · сквозной вид: " + (PassthroughOn ? "включён" : "НЕ включён") +
                       (string.IsNullOrEmpty(LastError) ? "" : " · причина: " + LastError);
            }
        }

        /// <summary>
        /// ВЫБОР БЭКЕНДА — ЧИСТАЯ ФУНКЦИЯ. Один вход — один выход, без чтения состояния Unity:
        /// поэтому её можно проверить таблицей без шлема.
        ///
        /// ПОРЯДОК:
        ///   1. PICO-специфичный признак (имя рантайма/устройства или расширение XR_BD_/XR_PICO_) —
        ///      PICO;
        ///   2. Meta-специфичный признак — Meta;
        ///   3. иначе unknown → `None` (сквозной вид не включаем, остаёмся в VR честно).
        /// </summary>
        public static KvMrBackend ChooseBackend(string runtimeName, string deviceModel,
            bool picoSpecificExtension, bool metaSpecificExtension)
        {
            if (LooksLike(runtimeName, "pico") || LooksLike(runtimeName, "bytedance") ||
                LooksLike(deviceModel, "pico") || LooksLike(deviceModel, "a9210") ||
                LooksLike(deviceModel, "a8110") || picoSpecificExtension)
                return KvMrBackend.Pico;

            if (LooksLike(runtimeName, "meta") || LooksLike(runtimeName, "oculus") ||
                LooksLike(runtimeName, "quest") || LooksLike(deviceModel, "quest") ||
                LooksLike(deviceModel, "oculus") || metaSpecificExtension)
                return KvMrBackend.Meta;

            return KvMrBackend.None;
        }

        /// <summary>
        /// ОПРЕДЕЛИТЬ БЭКЕНД ПО ЖИВОМУ РАНТАЙМУ. Вызывается один раз при включении MR.
        ///
        /// ПРИЗНАКИ PICO, которых нет у Meta: расширение `XR_BD_controller_interaction` (его выдача
        /// уже проверена журналом проекта §34) и PICO-расширения пространственных данных.
        /// </summary>
        public static KvMrBackend Detect()
        {
            string runtimeName = string.Empty;
            string deviceModel = string.Empty;
            try { runtimeName = OpenXRRuntime.name ?? string.Empty; }
            catch (Exception) { runtimeName = string.Empty; }     // имени нет — определим по устройству
            try { deviceModel = SystemInfo.deviceModel ?? string.Empty; }
            catch (Exception) { deviceModel = string.Empty; }     // модели нет — определим по расширениям

            bool picoExtension = ExtensionEnabled("XR_BD_controller_interaction") ||
                                 ExtensionEnabled("XR_BD_anchor") ||
                                 ExtensionEnabled("XR_PICO_spatial_mesh") ||
                                 ExtensionEnabled("XR_PICO_spatial_anchor") ||
                                 ExtensionEnabled("XR_PICO_scene_capture");

            bool metaExtension = ExtensionEnabled("XR_META_passthrough") ||
                                 ExtensionEnabled("XR_META_spatial_entity") ||
                                 ExtensionEnabled("XR_META_environment_depth");

            Backend = ChooseBackend(runtimeName, deviceModel, picoExtension, metaExtension);
            return Backend;
        }

        /// <summary>
        /// ВКЛЮЧИТЬ СКВОЗНОЙ ВИД ДЛЯ ТЕКУЩЕГО РЕЖИМА.
        ///
        /// Возвращает true ТОЛЬКО если включение подтверждено фактом (PICO — перечитыванием флага
        /// фичи, Meta — рабочим AR-Foundation-фоном). «Попытались» — не считается: иначе приёмка
        /// «сквозной вид включён» опиралась бы на намерение.
        /// </summary>
        public static bool EnablePassthrough(Camera headCamera)
        {
            if (PassthroughOn) return true;
            LastError = null;

            if (Application.isEditor)
            {
                LastError = "редактор: сквозного вида нет, это проверяется только в шлеме";
                Backend = KvMrBackend.None;
                return false;
            }

            KvXrCapabilities.Answer probe = KvXrCapabilities.Passthrough();
            if (probe != KvXrCapabilities.Answer.Yes)
            {
                LastError = "рантайм не выдал расширение сквозного вида (" +
                            KvXrCapabilities.Describe(probe, KvXrCapabilities.Answer.Unknown,
                                KvXrCapabilities.Answer.Unknown) + ")";
                Backend = KvMrBackend.None;
                return false;
            }

            if (Backend == KvMrBackend.None) Detect();

            bool ok = false;
            switch (Backend)
            {
                case KvMrBackend.Pico: ok = EnablePico(); break;
                case KvMrBackend.Meta: ok = EnableMeta(headCamera); break;
                default:
                    LastError = "неизвестный рантайм: не знаю, каким API включать сквозной вид";
                    break;
            }

            if (ok)
            {
                MakeCameraBackgroundTransparent(headCamera);
                PassthroughOn = true;
                Debug.Log("[KvMr] " + Status + " · сквозной вид ПОДТВЕРЖДЁН (расширение выдано рантаймом)");
            }
            else
            {
                Debug.LogWarning("[KvMr] " + Status);
            }
            return ok;
        }

        /// <summary>Выключить сквозной вид (возврат в VR без перезапуска).</summary>
        public static void DisablePassthrough(Camera headCamera)
        {
            if (!PassthroughOn) return;
            try
            {
                if (Backend == KvMrBackend.Pico)
                {
#if UNITY_ANDROID || UNITY_EDITOR
                    Unity.XR.OpenXR.Features.PICOSupport.PassthroughFeature.EnableVideoSeeThrough = false;
#endif
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KvMr] не удалось выключить сквозной вид: " + e.GetType().Name + ": " + e.Message);
            }
            PassthroughOn = false;
            Debug.Log("[KvMr] сквозной вид выключен");
        }

        // ------------------------------------------------------------ ветки платформ

        /// <summary>
        /// PICO: полноэкранный пасс-слой `PassthroughFeature` (расширение `XR_FB_passthrough`).
        ///
        /// ЧЕСТНОЕ ВКЛЮЧЕНИЕ — В ТРИ ПРОВЕРКИ, а не «поставил флаг и поверил»:
        ///   1. `isExtensionEnable` — фича инициализирована, то есть рантайм ВЫДАЛ расширение;
        ///   2. `IsPassthroughSupported()` — железо/прошивка действительно поддерживают сквозной вид;
        ///   3. после включения флаг перечитывается.
        ///
        /// ПОЧЕМУ НЕ ВЕРИТЬ ПРОСТО СВОЙСТВУ. Сеттер `EnableVideoSeeThrough` в пакете PICO
        /// «оптимистичный»: он выставляет внутренний флаг ДО проверки и игнорирует результат
        /// `EnableSeeThroughManual`. Поэтому одной проверки свойства недостаточно — нужны шаги 1–2.
        /// Свойство всё же ставится: только при `_enableVideoSeeThrough != -1` пакет сам
        /// восстанавливает пасс-слой после сворачивания/разворачивания шлема.
        ///
        /// Вызов в try/catch: `IsPassthroughSupported` — P/Invoke в `libopenxr_pico.so`, которой
        /// нет ни в редакторе, ни на ПК; исключение здесь означает «не наша ветка», а не поломку.
        /// </summary>
        private static bool EnablePico()
        {
#if UNITY_ANDROID || UNITY_EDITOR
            try
            {
                bool featureReady = Unity.XR.OpenXR.Features.PICOSupport.PassthroughFeature.isExtensionEnable;
                if (!featureReady)
                {
                    LastError = "фича сквозного вида не инициализирована: рантайм не выдал XR_FB_passthrough " +
                                "(фича включена не для Android или шлем отказал)";
                    return false;
                }

                bool supported =
                    Unity.XR.OpenXR.Features.PICOSupport.PassthroughFeature.IsPassthroughSupported();
                if (!supported)
                {
                    LastError = "рантайм PICO сообщил: сквозной вид не поддерживается устройством";
                    return false;
                }

                Unity.XR.OpenXR.Features.PICOSupport.PassthroughFeature.EnableVideoSeeThrough = true;

                // ПОДТВЕРЖДЕНИЕ ФАКТОМ: расширение выдано и флаг восстановления выставлен.
                bool fact = Unity.XR.OpenXR.Features.PICOSupport.PassthroughFeature.isExtensionEnable &&
                            Unity.XR.OpenXR.Features.PICOSupport.PassthroughFeature.EnableVideoSeeThrough;
                if (!fact) LastError = "PICO не подтвердил включение сквозного вида";
                else
                    Debug.Log("[KvMr] PICO: расширение выдано, рантайм сообщил поддержку, " +
                              "пасс-слой включён (EnableVideoSeeThrough = true)");
                return fact;
            }
            catch (Exception e)
            {
                LastError = "API PICO недоступно (" + e.GetType().Name + ": " + e.Message + ")";
                return false;
            }
#else
            LastError = "сборка не под Android — ветка PICO не собрана";
            return false;
#endif
        }

        /// <summary>
        /// Meta: сквозной вид включает `ARCameraManager` — он запускает камеру-подсистему Meta
        /// OpenXR, а та САМА создаёт полноэкранный composition-слой passthrough (`Passthrough`).
        ///
        /// ПОЧЕМУ НЕ ARCameraBackground И НЕ ARSession (проверено по исходникам пакета 2.4.1):
        ///   • `ARCameraBackground` на Quest не работает вовсе: провайдер Meta не отдаёт ни
        ///     `cameraMaterial`, ни `permissionGranted`, поэтому режим фона у него `None`; более
        ///     того, этот компонент перезаписывает проекционную матрицу камеры из кадра камеры —
        ///     на шлеме это могло бы перебить стерео-проекцию;
        ///   • `ARSession` для passthrough не нужен, а его включение тянет Meta-сессию на PICO
        ///     (фича Session у Meta не самогейтится) — поэтому он здесь не создаётся ВООБЩЕ.
        ///
        /// Честная проверка: подсистема камеры должна реально подняться. Если её нет (фича
        /// `Meta Quest: Camera (Passthrough)` не включена в сборке) — менеджер просто напишет
        /// предупреждение, и мы честно вернём false с причиной в журнал.
        /// </summary>
        private static bool EnableMeta(Camera headCamera)
        {
            try
            {
                if (headCamera == null)
                {
                    LastError = "нет камеры оператора — сквозной вид Meta включать некуда";
                    return false;
                }

                UnityEngine.XR.ARFoundation.ARCameraManager manager =
                    headCamera.GetComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();
                if (manager == null)
                    manager = headCamera.gameObject.AddComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();

                manager.enabled = true;

                // «ПРОВЕРКА ФАКТОМ» БЫЛА ФИКТИВНОЙ (исправлено 11.11.2026).
                //
                // ЧТО БЫЛО. Здесь стояло возвращение ЗНАЧЕНИЯ ТОГО ЖЕ ПОЛЯ, которому строкой выше
                // присвоено `true`. Результат НИКОГДА не мог быть false, и заявленный в шапке класса
                // принцип («true ТОЛЬКО если включение подтверждено фактом») не выполнялся: на шлеме
                // без фичи passthrough приложение объявляло себя MR, оператор видел чёрный экран, а
                // журнал утверждал, что сквозной вид включён.
                //
                // ЧТО СТАЛО. Подтверждение — НАЛИЧИЕ ЗАПУЩЕННОЙ ПОДСИСТЕМЫ КАМЕРЫ: композиционный
                // слой passthrough создаёт именно она. Нет подсистемы — нет и слоя, значит false
                // с причиной, а не «включено».
                bool cameraSubsystemRunning = HasRunningCameraSubsystem();
                if (!cameraSubsystemRunning)
                {
                    LastError = "сквозной вид Meta не подтверждён: подсистема камеры не запущена " +
                                "(фича «Meta Quest: Camera (Passthrough)» не включена в сборке?)";
                    Debug.LogWarning("[KvMr] " + LastError);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                LastError = "AR Foundation недоступен (" + e.GetType().Name + ": " + e.Message + ")";
                return false;
            }
        }

        /// <summary>
        /// ЕСТЬ ЛИ РЕАЛЬНО ЗАПУЩЕННАЯ ПОДСИСТЕМА КАМЕРЫ. Именно она создаёт композиционный слой
        /// сквозного вида, поэтому только она и может служить доказательством.
        ///
        /// Вынесено отдельным методом, чтобы проверялось тестом и чтобы «факт» был в одном месте:
        /// раньше доказательством считалось присваивание в поле рядом.
        /// </summary>
        public static bool HasRunningCameraSubsystem()
        {
            try
            {
                var subsystems = new System.Collections.Generic.List<UnityEngine.XR.ARSubsystems.XRCameraSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);
                for (int i = 0; i < subsystems.Count; i++)
                {
                    var subsystem = subsystems[i];
                    if (subsystem != null && subsystem.running) return true;
                }
            }
            catch (Exception)
            {
                // Отсутствие подсистемного API — это тоже «не подтверждено», а не повод падать.
            }
            return false;
        }

        /// <summary>
        /// ФОН КАМЕРЫ ДЛЯ ПАСС-СЛОЯ. Требование PICO: фон камеры — сплошной чёрный с нулевой
        /// альфой; иначе пасс-слой закрашивается фоном. Это НЕ «подмена цвета вместо passthrough»,
        /// а обязательное условие, чтобы пасс-слой был виден.
        /// </summary>
        private static void MakeCameraBackgroundTransparent(Camera headCamera)
        {
            if (headCamera == null) return;
            headCamera.clearFlags = CameraClearFlags.SolidColor;
            headCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        private static bool LooksLike(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ExtensionEnabled(string extensionName)
        {
            try { return OpenXRRuntime.IsExtensionEnabled(extensionName); }
            catch (Exception) { return false; }
        }
    }
}
