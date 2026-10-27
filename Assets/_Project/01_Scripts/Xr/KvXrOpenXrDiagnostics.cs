using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

namespace KazistovVvXr
{
    /// <summary>
    /// СТРОКИ ОБ OPENXR И ВВОДЕ ДЛЯ ЖУРНАЛА ШЛЕМА (сессия §36).
    ///
    /// ЗАЧЕМ. Требование владельца [F5]: «сделано» без строки в журнале не принимается. По
    /// дефекту контроллеров (карточка Б18/Б24) в журнале раньше было видно только «правый нет ·
    /// левый нет · устройств 0», то есть итог, а не причину. Здесь печатается то, что раньше
    /// приходилось выяснять перебором логов рантайма:
    ///   • выдал ли рантайм расширение `XR_BD_controller_interaction` (без него профиль ByteDance
    ///     не действует вовсе);
    ///   • сколько расширений рантайм объявил всего и сколько включено;
    ///   • не нарушает ли наша карта привязок правило «путь кнопки руки → своя рука» — именно
    ///     это нарушение заставляло OpenXR отвечать `XR_ERROR_PATH_UNSUPPORTED` и отбрасывать
    ///     весь набор привязок;
    ///   • сколько устройств видит САМА XR-подсистема (`UnityEngine.XR.InputDevices`), отдельно
    ///     от `InputSystem`: это два независимых свидетеля, и расхождение между ними — улика.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: никакого лечения. Только чтение состояния и печать. Все вызовы пакета
    /// OpenXR обёрнуты в try/catch: на компьютере без OpenXR диагностика обязана молчать, а не
    /// ронять сцену исключением.
    /// </summary>
    public static class KvXrOpenXrDiagnostics
    {
        /// <summary>Вызывать каждый кадр: внутри не чаще раза в секунду и не дольше первых секунд.</summary>
        public static void Tick()
        {
            if (finished) return;

            if (startTime < 0f) startTime = Time.realtimeSinceStartup;

            float elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < 1.5f) return;
            if (Time.realtimeSinceStartup < nextReport) return;
            nextReport = Time.realtimeSinceStartup + 1f;

            if (!runtimeLogged && elapsed >= 1.5f)
            {
                runtimeLogged = true;
                Debug.Log("[KvXr] OpenXR: " + DescribeRuntime());
                Debug.Log("[KvXr] OpenXR: " + DescribeProfileBindings());
            }

            // ЧТО РАНТАЙМ НАЗЫВАЕТ ТЕКУЩИМ ПРОФИЛЕМ ДЛЯ РУК — ответ на вопрос «кто виноват»,
            // когда профиль привязан, а устройства ввода нет. Опрашивается каждые 2 с первые
            // 30 секунд: профиль может появиться ПОЗЖЕ старта (когда человек взял контроллер
            // и нажал кнопку), и одну пробу через 3 с легко «не поймать».
            if (elapsed >= nextProfilesTime && profilesLogged < MaxProfileProbes)
            {
                profilesLogged++;
                nextProfilesTime = elapsed + 2f;
                Debug.Log("[KvXr] OpenXR: " + KvPicoControllerProfile.DescribeRuntimeProfiles());
                if (profilesLogged == 1) Debug.Log("[KvXr] OpenXR: " + DescribeEnabledExtensions());
            }

            string inputDevices = DescribeInputSystemDevices();
            if (inputDevices != lastInputDevices)
            {
                lastInputDevices = inputDevices;
                Debug.Log("[KvXr] " + inputDevices);
            }

            string devices = DescribeXrDevices();
            if (devices != lastDevices)
            {
                lastDevices = devices;
                Debug.Log("[KvXr] " + devices);
                deviceReports++;
            }

            // Дольше 12 секунд не шумим: контроллеры появляются, когда шлем уже надет,
            // а слежение за их появлением/пропажей ведёт KvXrInputSource (оно без ограничения).
            if (elapsed > 34f && deviceReports > 0) finished = true;
        }

        private static bool finished;
        private static bool runtimeLogged;
        private static int profilesLogged;
        private static float nextProfilesTime = 3f;

        /// <summary>Сколько раз опрашивать профиль рук (каждые 2 с, первые 30 с).</summary>
        private const int MaxProfileProbes = 15;
        private static float startTime = -1f;
        private static float nextReport;
        private static string lastDevices;
        private static string lastInputDevices;
        private static int deviceReports;

        /// <summary>
        /// КАКИЕ РАСШИРЕНИЯ РАНТАЙМ ВКЛЮЧИЛ НАМ — перечень, а не только число. По нему видно,
        /// что именно получено (`XR_BD_controller_interaction`, `XR_EXT_hand_tracking` и т. д.).
        /// </summary>
        public static string DescribeEnabledExtensions()
        {
            try
            {
                string[] enabled = UnityEngine.XR.OpenXR.OpenXRRuntime.GetEnabledExtensions();
                if (enabled == null || enabled.Length == 0) return "включённых расширений нет";
                return "включённые расширения (" + enabled.Length + "): " + string.Join(", ", enabled);
            }
            catch (Exception e)
            {
                return "список расширений недоступен (" + e.GetType().Name + ")";
            }
        }

        /// <summary>
        /// УСТРОЙСТВА ГЛАЗАМИ INPUT SYSTEM — коротко и без длинных строк. Прежняя подробная
        /// распечатка обрезалась журналом на восьмой строке, и устройства с большими номерами
        /// в журнал не попадали вовсе (найдено 18.10.2026: «устройств 24», а видно восемь).
        /// Здесь печатаются счётчики и только те устройства, чей макет содержит «XR».
        /// </summary>
        public static string DescribeInputSystemDevices()
        {
            string result;
            try
            {
                var all = UnityEngine.InputSystem.InputSystem.devices;
                int controllers = 0;
                int hmds = 0;
                var sb = new StringBuilder();
                sb.Append("устройства InputSystem: всего ").Append(all.Count);

                var xr = new List<string>();
                for (int i = 0; i < all.Count; i++)
                {
                    UnityEngine.InputSystem.InputDevice d = all[i];
                    if (d == null) continue;
                    if (d is UnityEngine.InputSystem.XR.XRController) controllers++;
                    if (d is UnityEngine.InputSystem.XR.XRHMD) hmds++;

                    string layout = d.layout;
                    if (string.IsNullOrEmpty(layout)) continue;
                    if (layout.IndexOf("XR", StringComparison.OrdinalIgnoreCase) < 0 &&
                        layout.IndexOf("PICO", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    xr.Add("«" + d.displayName + "»/«" + layout + "»");
                }

                sb.Append(" · XRController ").Append(controllers).Append(" · XRHMD ").Append(hmds);
                sb.Append(" · XR-макетов ").Append(xr.Count);
                for (int i = 0; i < xr.Count && i < 6; i++) sb.Append(" · ").Append(xr[i]);
                result = sb.ToString();
            }
            catch (Exception e)
            {
                result = "устройства InputSystem: опрос не удался (" + e.GetType().Name + ")";
            }
            return result;
        }

        /// <summary>Имя расширения контроллеров ByteDance — то, без которого профиль не действует.</summary>
        public const string ByteDanceExtension = "XR_BD_controller_interaction";

        /// <summary>Строка о рантайме и расширениях: имя, версия, доступно/включено.</summary>
        public static string DescribeRuntime()
        {
            string name;
            string version;
            string[] available;
            string[] enabled;

            try
            {
                name = UnityEngine.XR.OpenXR.OpenXRRuntime.name;
                version = UnityEngine.XR.OpenXR.OpenXRRuntime.version;
                available = UnityEngine.XR.OpenXR.OpenXRRuntime.GetAvailableExtensions() ?? new string[0];
                enabled = UnityEngine.XR.OpenXR.OpenXRRuntime.GetEnabledExtensions() ?? new string[0];
            }
            catch (Exception e)
            {
                return "рантайм OpenXR недоступен (" + e.GetType().Name + ") — диагностика расширений пропущена";
            }

            bool availableExt = Contains(available, ByteDanceExtension);
            bool enabledExt = Contains(enabled, ByteDanceExtension);

            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "рантайм «{0}» версия {1} · расширений доступно {2} · включено {3} · " +
                "{4} — доступно: {5} · включено: {6}",
                name, version, available.Length, enabled.Length,
                ByteDanceExtension, availableExt ? "да" : "НЕТ", enabledExt ? "да" : "НЕТ");
        }

        /// <summary>
        /// Строка о карте привязок профиля PICO: сколько привязок, сколько ограничено по руке,
        /// сколько нарушений. Ноль нарушений — условие, при котором рантайм обязан принять набор.
        /// </summary>
        public static string DescribeProfileBindings()
        {
            string report;
            bool ok = KvPicoControllerProfile.AuditHandBindings(out report);
            return report + " · правило «путь кнопки руки → своя рука»: " + (ok ? "соблюдено" : "НАРУШЕНО");
        }

        /// <summary>
        /// УСТРОЙСТВА ГЛАЗАМИ XR-ПОДСИСТЕМЫ (не Input System). Отдельный свидетель: если здесь
        /// контроллеры есть, а `KvXrInputSource` их не видит — дело в макете устройства, если
        /// нет ни здесь, ни там — дело в привязке профиля.
        /// </summary>
        public static string DescribeXrDevices()
        {
            var devices = new List<InputDevice>();
            try { InputDevices.GetDevices(devices); }
            catch (Exception) { return "XR-подсистема: опрос устройств недоступен"; }

            int controllers = 0;
            int tracked = 0;
            var sb = new StringBuilder();
            sb.Append("XR-подсистема: устройств ").Append(devices.Count)
              .Append(" · шлемов ").Append(CountOf(devices, InputDeviceCharacteristics.HeadMounted));

            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice d = devices[i];
                if ((d.characteristics & InputDeviceCharacteristics.Controller) != 0) controllers++;
                bool isTracked = false;
                try { d.TryGetFeatureValue(CommonUsages.isTracked, out isTracked); }
                catch (Exception) { isTracked = false; }
                if (isTracked) tracked++;
            }

            sb.Append(" · контроллеров ").Append(controllers)
              .Append(" · с достоверной позой ").Append(tracked);

            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice d = devices[i];
                if ((d.characteristics & InputDeviceCharacteristics.Controller) == 0) continue;
                sb.Append("\n    контроллер «").Append(d.name).Append("» · ")
                  .Append((d.characteristics & InputDeviceCharacteristics.Left) != 0 ? "левый" : "правый");
                break;
            }

            return sb.ToString();
        }

        private static int CountOf(List<InputDevice> devices, InputDeviceCharacteristics flag)
        {
            int n = 0;
            for (int i = 0; i < devices.Count; i++)
                if ((devices[i].characteristics & flag) != 0) n++;
            return n;
        }

        private static bool Contains(string[] list, string value)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Length; i++)
                if (string.Equals(list[i], value, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
