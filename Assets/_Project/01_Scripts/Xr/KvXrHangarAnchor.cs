using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// СТАВИТ АНГАР ТАМ, ГДЕ ОКАЗАЛИСЬ СТЕНДЫ (сессия §35, карточка Б17).
    ///
    /// ЗАЧЕМ ЭТОТ КОМПОНЕНТ, ЕСЛИ АНГАР УЖЕ ЛЕЖИТ В СЦЕНЕ. Геометрия ангара впекается в `XrMain`
    /// редакторным инструментом — так её видно и в сборке, и в файле уровня. Но ПОЛОЖЕНИЕ ангара
    /// нельзя впечь окончательно: `KvXrSceneContent` переносит стенды вокруг оператора ВО ВРЕМЯ
    /// РАБОТЫ, отталкиваясь от того, где оператор стоит. Если оператор окажется не в начале
    /// координат, стенды уедут — и впечённый ангар разошёлся бы с ними: столы вылезли бы из стены
    /// или оказались бы снаружи зала.
    ///
    /// ЧТО ДЕЛАЕТ. После того как стенды встали, находит их общий габарит и переносит корень
    /// ангара по тому же правилу, что и на компьютере (см. <see cref="KvHangarPlacement"/>):
    /// стенды занимают внутри зала ровно то место, что и в десктопной сцене. Правило — чистая
    /// арифметика, проверенная тестами, здесь только применение и строка журнала.
    ///
    /// ПОРЯДОК РАБОТЫ. `[DefaultExecutionOrder]` ставит компонент ПОСЛЕ `KvXrSceneContent` (−400):
    /// сначала стенды появляются и переносятся, потом по ним ставится зал. Иначе зал считался бы
    /// по старым координатам (стенды стояли на 20–31 м от оператора — именно из-за этого в шлеме
    /// и было «только горизонт»).
    /// </summary>
    [DefaultExecutionOrder(-350)]
    [DisallowMultipleComponent]
    public class KvXrHangarAnchor : MonoBehaviour
    {
        /// <summary>Корень ангара. Пусто — найдётся по имени среди детей этого объекта.</summary>
        public Transform hangarRoot;

        /// <summary>Ставить пол зала ровно на уровень пола сцены (защита от «зала в земле»).</summary>
        public bool forceFloorAtZero = true;

        /// <summary>Сообщать в журнал при постановке (обязательная строка доказательства).</summary>
        public bool logOnPlace = true;

        /// <summary>Последний применённый центр стендов, метры — для журнала и проверок.</summary>
        public Vector3 StandsCenter { get; private set; }

        /// <summary>Последнее применённое положение корня ангара, метры.</summary>
        public Vector3 AppliedRootPosition { get; private set; }

        /// <summary>Применено ли размещение (для диагностики).</summary>
        public bool Applied { get; private set; }

        void Start()
        {
            Place();
        }

        private float retryUntil = -1f;

        /// <summary>
        /// ДОПОСТАВИТЬ ЗАЛ, КОГДА ПОЯВИТСЯ ПОЗА ШЛЕМА. В `Start` трекинг может быть ещё не готов:
        /// тогда точка оператора берётся с запасного пути (объект-камера), а он, как выяснилось
        /// 16.10.2026, может указывать не туда. Поэтому несколько секунд пробуем снова — и как
        /// только поза шлема появилась, ставим зал уже по ней.
        /// </summary>
        void Update()
        {
            if (PlacedFromHmd) return;
            if (retryUntil < 0f) retryUntil = Time.time + 12f;
            if (Time.time < retryUntil) Place();
        }

        /// <summary>Поставлено ли по позе шлема (а не по запасному пути).</summary>
        public bool PlacedFromHmd { get; private set; }

        /// <summary>
        /// ПОСТАВИТЬ ЗАЛ ПО СТЕНДАМ. Вынесено публичным методом, чтобы это можно было вызвать
        /// повторно (например, после переноса стендов) и проверить без ожидания кадра.
        /// </summary>
        public void Place()
        {
            Transform root = hangarRoot != null ? hangarRoot : FindHangarRoot();
            if (root == null)
            {
                Debug.LogWarning("[KvXr] Ангар: корень зала не найден в XR-сцене — " +
                                 "в шлеме будет видно «только горизонт». Ожидается объект " + HangarRootName + ".");
                return;
            }

            if (!TryOperatorBase(out Vector3 operatorBase, out bool fromHmd))
            {
                Debug.LogWarning("[KvXr] Ангар: оператор не найден (ни позы шлема, ни камеры) — " +
                                 "зал оставлен на месте из сцены.");
                return;
            }
            PlacedFromHmd = fromHmd;

            // ЗАЛ СТАВИТСЯ ПО ОПЕРАТОРУ, А НЕ ПО СТЕНДАМ. Прежняя привязка к стендам приводила
            // к тому, что стоило отодвинуть столы — и зал уезжал вместе с ними, унося оператора
            // к торцевой стене («камера где-то в непонятном месте»). Столы и зал — независимые вещи.
            Vector3 position = KvHangarPlacement.RootPositionForOperator(operatorBase);

            if (forceFloorAtZero && Mathf.Abs(position.y) > 0.001f) position.y = 0f;

            root.SetPositionAndRotation(position, Quaternion.Euler(0f, root.eulerAngles.y, 0f));
            AppliedRootPosition = position;
            Applied = true;

            Bounds hall = KvHangarPlacement.BoundsAt(position);
            Vector3 operatorLocal = operatorBase - position;
            bool operatorInside = KvHangarPlacement.Contains(position, operatorBase);

            // ГДЕ СТОЯТ СТЕНДЫ — для честной строки журнала: они больше НЕ двигают зал.
            bool standsFound = TryStandsBounds(out Bounds stands);
            StandsCenter = standsFound ? stands.center : Vector3.zero;
            bool standsInside = standsFound && KvHangarPlacement.Contains(position, stands.center);

            if (logOnPlace)
                Debug.Log("[KvXr] " + KvHangarPlacement.Describe(StandsCenter, position) +
                          " · оператор в зале на " + operatorLocal.ToString("0.00") +
                          " · точка оператора из " + (fromHmd ? "ПОЗЫ ШЛЕМА" : "объекта-камеры (трекинг ещё не готов)") +
                          " · оператор внутри зала: " + (operatorInside ? "да" : "НЕТ") +
                          " · стенды внутри зала: " + (standsInside ? "да" : "НЕТ") +
                          " · объектов ангара " + root.childCount);
            if (logOnPlace) Debug.Log(DescribeCameras());

            if (!operatorInside || !standsInside)
                Debug.LogWarning("[KvXr] Ангар: окружение поставлено неверно — оператор или стенды " +
                                 "оказались вне зала. Габарит зала " + hall.size.ToString("0.00") +
                                 " при положении корня " + position.ToString("0.00") + ".");
        }

        /// <summary>
        /// ГДЕ СТОИТ ОПЕРАТОР. Сначала спрашиваем ПОЗУ ШЛЕМА из системы ввода, и только если её
        /// нет — берём объект-камеру.
        ///
        /// ПОЧЕМУ ТАК (найдено 16.10.2026 по живой жалобе «камера не в той позиции… я нахожусь
        /// в 0 по высоте где-то за ангаром»). Зал привязывался к `Camera.main`, то есть к объекту
        /// `XrCamera`. Проба ориентации показала, что у этого объекта курс НЕ МЕНЯЕТСЯ (0,0° восемь
        /// замеров подряд), хотя оператор в это время крутил головой — значит это НЕ та камера,
        /// которой он смотрит. Зал, поставленный относительно неподвижного объекта, оказывался
        /// в стороне от настоящего оператора: он видел лабораторию «за ангаром», а не вокруг себя.
        /// Поза HMD — это и есть настоящая голова.
        /// </summary>
        private static bool TryOperatorBase(out Vector3 basePosition, out bool fromHmd)
        {
            Vector3 head;
            Quaternion headRotation;
            if (KvXrInputSource.TryHeadPose(out head, out headRotation))
            {
                basePosition = new Vector3(head.x, 0f, head.z);
                fromHmd = true;
                return true;
            }

            fromHmd = false;
            basePosition = Vector3.zero;

            Camera cam = Camera.main;
            if (cam == null)
            {
                Camera[] all = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != null && all[i].enabled) { cam = all[i]; break; }
            }
            if (cam == null) return false;

            Vector3 p = cam.transform.position;
            basePosition = new Vector3(p.x, 0f, p.z);
            return true;
        }

        /// <summary>
        /// ГДЕ НА САМОМ ДЕЛЕ КАМЕРА. Печатает все активные камеры с их положением и позу шлема,
        /// чтобы в журнале было ВИДНО, совпадают ли они. Нужно, потому что жалоба «камера не в той
        /// позиции» иначе проверяется только догадкой.
        /// </summary>
        public static string DescribeCameras()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[KvHangar] камеры: ");
            Camera[] all = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            int shown = 0;
            for (int i = 0; i < all.Length && shown < 6; i++)
            {
                Camera c = all[i];
                if (c == null) continue;
                sb.Append("«").Append(c.name).Append("» ")
                  .Append(c.transform.position.ToString("0.00"))
                  .Append(c.enabled ? " вкл" : " выкл")
                  .Append(c == Camera.main ? " (main)" : "")
                  .Append(" · ");
                shown++;
            }
            Vector3 head;
            Quaternion rot;
            sb.Append(KvXrInputSource.TryHeadPose(out head, out rot)
                ? "поза шлема " + head.ToString("0.00")
                : "позы шлема НЕТ");
            return sb.ToString();
        }

        /// <summary>Имя корня зала в XR-сцене — по нему он и находится.</summary>
        public const string HangarRootName = "XrHangar";

        private Transform FindHangarRoot()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == HangarRootName) return t;
            }
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t != null && t.name == HangarRootName) return t;
            }
            return null;
        }

        /// <summary>Общий габарит обоих стендов — по нему ставится зал.</summary>
        private static bool TryStandsBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null) continue;
                if (t.name != TrajectoryCore.StandBuilder.Stand1Name &&
                    t.name != TrajectoryCore.StandBuilder.Stand2Name) continue;

                foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (!found) { bounds = r.bounds; found = true; }
                    else bounds.Encapsulate(r.bounds);
                }
            }
            return found;
        }
    }
}
