using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace KazistovVvXr
{
    /// <summary>
    /// ВИБРАЦИЯ КОНТРОЛЛЕРОВ ШЛЕМА (шаг 6 плана VR-управления, 05.11.2026).
    ///
    /// ПОЧЕМУ ОТДЕЛЬНЫЙ МОСТ, А НЕ `KvHaptics` ИЗ ПК-СЛОЯ. `KvHaptics` ищет компоненты XR
    /// Interaction Toolkit по имени типа, а в этом риге таких компонентов нет — вибрация
    /// «привязана в профиле, но не отправляется» (аудит 05.11.2026). Unity-API `InputDevice`
    /// работает с OpenXR-контроллерами напрямую, без пакетов и без рефлексии.
    ///
    /// ЧЕСТНО О НЕДОСТУПНОСТИ. Если устройство не сообщает `supportsImpulse`, импульс просто не
    /// отправляется и в журнал уходит ОДНА строка за сессию: молчать нельзя (выглядит как
    /// «вибрация сломалась»), спамить — тоже.
    /// </summary>
    public static class KvHapticsBridge
    {
        /// <summary>Сколько импульсов реально отправлено (диагностика/тесты).</summary>
        public static int PulseCount { get; private set; }

        private static bool loggedUnavailable;

        /// <summary>Короткий импульс: «легко» для нажатия, «средне» для смены режима/телепорта.</summary>
        public static bool Pulse(KvXrQuickHand hand, float amplitude, float duration)
        {
            XRNode node = hand == KvXrQuickHand.Right ? XRNode.RightHand : XRNode.LeftHand;
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(node, devices);

            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice device = devices[i];
                if (!device.isValid) continue;
                if (!device.TryGetHapticCapabilities(out HapticCapabilities capabilities)) continue;
                if (!capabilities.supportsImpulse) continue;

                device.SendHapticImpulse(0u,
                    Mathf.Clamp01(amplitude),
                    Mathf.Max(0.01f, duration));
                PulseCount++;
                return true;
            }

            if (!loggedUnavailable)
            {
                loggedUnavailable = true;
                Debug.Log("[KvXr] ВИБРАЦИЯ: контроллер не сообщает поддержку импульса — " +
                          "вибрация недоступна (остальное управление работает)");
            }
            return false;
        }

        /// <summary>Стандартные силы: true — сильнее (E-STOP), false — мягкий отклик.</summary>
        public static bool Pulse(KvXrQuickHand hand, bool strong)
        {
            return strong ? Pulse(hand, 0.85f, 0.12f) : Pulse(hand, 0.25f, 0.04f);
        }

        /// <summary>Строка для журнала/отчёта.</summary>
        public static string Describe()
        {
            return "вибрация: импульсов отправлено " + PulseCount +
                   (loggedUnavailable ? " · устройство не поддерживает" : " · канал проверен");
        }
    }
}
