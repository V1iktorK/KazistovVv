using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace KazistovVvAr
{
    /// <summary>
    /// ГИРОСКОП КАК РЕЗЕРВ ОБЗОРА (05.11.2026).
    ///
    /// ЗАЧЕМ. Владелец: «вижу всё жёлтым и не могу двигать экраном». Причина не в разрешениях:
    /// Android не требует разрешения на гироскоп, а камера уже была разрешена. ARCore не начинал
    /// трекинг, потому что камера смотрела в однотонную поверхность (VIO не находит признаков) —
    /// и поза устройства не менялась, значит вид «замирал».
    ///
    /// ЧТО ДЕЛАЕТ. Пока ARCore НЕ в состоянии `SessionTracking`, поворот устройства вокруг
    /// вертикали вращает ВИРТУАЛЬНЫЙ МИР (а не камеру): смотреть по сторонам можно даже без
    /// трекинга. Как только трекинг пошёл — фолбэк выключается, и работает штатная AR-камера.
    /// Это ровно тот «доступ к гироскопу», который просил владелец, без второй системы трекинга.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArGyroFallback : MonoBehaviour
    {
        /// <summary>Сколько поворотов сделано гироскопом (для журнала/отчёта).</summary>
        public int RotationCount { get; private set; }

        /// <summary>Писать в журнал первый поворот (одна строка, без спама).</summary>
        public bool logOnFirstRotation = true;

        private Quaternion lastAttitude;
        private bool hasLast;
        private bool logged;
        private KvArSessionController controller;

        void OnEnable()
        {
            Input.gyro.enabled = true;   // Android не требует разрешения, но без этого датчик молчит
            hasLast = false;
            // ПОИСК ОДИН РАЗ: покадровый FindAnyObjectByType запрещён тестом бюджета кадра.
            controller = FindAnyObjectByType<KvArSessionController>();
        }

        void Update()
        {
            bool tracking = controller != null &&
                            controller.State == ARSessionState.SessionTracking;
            if (tracking)
            {
                hasLast = false;
                return;
            }

            Quaternion attitude = Input.gyro.attitude;
            if (!hasLast)
            {
                lastAttitude = attitude;
                hasLast = true;
                return;
            }

            float deltaYaw = Mathf.DeltaAngle(lastAttitude.eulerAngles.y, attitude.eulerAngles.y);
            lastAttitude = attitude;
            if (Mathf.Abs(deltaYaw) < 0.05f) return;

            KvArWorld world = KvArWorld.Active;
            if (world == null) return;
            world.RotateView(-deltaYaw);
            RotationCount++;
            if (logOnFirstRotation && !logged)
            {
                logged = true;
                Debug.Log("[KvAr] ГИРОСКОП: ARCore ещё не отслеживает — обзор поворачивается " +
                          "гироскопом (поворот устройства вокруг вертикали)");
            }
        }

        /// <summary>Строка для журнала.</summary>
        public string Describe()
        {
            return "гиро-обзор: поворотов " + RotationCount +
                   (controller != null && controller.State == ARSessionState.SessionTracking
                       ? " · трекинг есть (фолбэк выключен)"
                       : " · фолбэк активен");
        }
    }
}
