using UnityEngine;
using KazistovVvUI;

namespace KazistovVvXr
{
    /// <summary>
    /// СТОРОЖ XR-КАМЕРЫ (сессия §28, этап 1–3 плана XR).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ КОМПОНЕНТ. Проект написан для компьютера, и в нём есть сервисы,
    /// которые считают, что камерой управляет МЫШЬ. В шлеме камерой управляет трекинг
    /// головы, и любой такой сервис становится дефектом. Самый опасный из них —
    /// `IdleCameraBrain` («режим простоя, как в Skyrim»): через десять минут бездействия
    /// он НАЧИНАЕТ ОБЛЕТАТЬ РОБОТА и переписывает `transform` камеры. В VR это не «красиво»,
    /// а мгновенная дурнота у оператора, потому что картинка едет сама.
    ///
    /// ЧТО ДЕЛАЕТ СТОРОЖ:
    ///   1) убирает облёт простоя с камеры шлема (в VR он недопустим в принципе);
    ///   2) снимает захват системного курсора — в шлеме его нет, а захваченный курсор
    ///      на компьютере мешает смотреть, что происходит;
    ///   3) сообщает одной строкой в журнал, что именно отключено: молчаливое отключение
    ///      чужого кода — это тот же дефект, отложенный на потом.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class KvXrCameraGuard : MonoBehaviour
    {
        /// <summary>Снимать захват системного курсора (на компьютере-зеркале он только мешает).</summary>
        public bool freeSystemCursor = true;

        /// <summary>Убирать режим простоя камеры (облёт робота).</summary>
        public bool disableIdleOrbit = true;

        /// <summary>Что именно отключено — для журнала и отчёта.</summary>
        public string ActionsTaken { get; private set; } = "ничего (всё уже было выключено)";

        void Awake()
        {
            var actions = new System.Collections.Generic.List<string>();

            if (disableIdleOrbit)
            {
                var brains = GetComponents<IdleCameraBrain>();
                for (int i = 0; i < brains.Length; i++)
                {
                    if (brains[i] == null) continue;
                    brains[i].enabled = false;
                    actions.Add("облёт простоя IdleCameraBrain выключен");
                }
            }

            if (freeSystemCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                actions.Add("системный курсор освобождён");
            }

            if (actions.Count > 0) ActionsTaken = string.Join(", ", actions);
        }

        void Start()
        {
            Debug.Log("[KvXr] Сторож камеры: " + ActionsTaken);
        }
    }
}
