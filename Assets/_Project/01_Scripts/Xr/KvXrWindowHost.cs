using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ОКНА В МИРЕ (указание владельца 24.10.2026: «кнопки не выдают окна, которые можно будет крепить
    /// на поясе, они тоже не должны крепиться ко взгляду, если персонаж будет отходить — окна останутся
    /// висеть в воздухе»).
    ///
    /// ЧТО БЫЛО. «Настройки», «Объекты», «Свойства», «Справка» были СТРАНИЦАМИ панели у руки: панель
    /// перерисовывалась в другой экран и ехала за кистью. Владелец хочет окон: нажал — окно появилось
    /// в мире, панель осталась панелью, окно живёт своей жизнью.
    ///
    /// ГДЕ СТОЯТ. В мире, а не на голове: окно ставится один раз перед оператором и остаётся на месте.
    /// Оператор отойдёт — окно останется висеть в воздухе (перемещение добавим отдельно, сейчас его
    /// нет). Поэтому хост и окна НЕ являются детьми рига: иначе перемещение рига тащило бы их за собой.
    ///
    /// ЧТО ВНУТРИ. Каждое окно — та же панель `KvXrGroupPanel` с `followHand = false`, открытая на
    /// нужном экране. Второй механизм кнопок не заводится: попадание, нажатие и подсветка уже приняты
    /// владельцем, а дефект «видно, а луч не ловит» (Б50) пришлось бы чинить дважды.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrWindowHost : MonoBehaviour
    {
        /// <summary>Живой хост окон (создаётся ригом).</summary>
        public static KvXrWindowHost Active { get; private set; }

        /// <summary>Камера шлема: по ней окна ставятся перед оператором.</summary>
        public Camera headCamera;

        /// <summary>
        /// Насколько далеко перед оператором встаёт окно, метры. БЛИЖЕ МЕНЮ (0,55 м): указание
        /// владельца 24.10.2026 — «окна должны открываться перед менюшной панелью, а не за ней».
        /// </summary>
        public float distanceMeters = 0.50f;

        /// <summary>
        /// Насколько ниже уровня глаз ставится центр окна, метры. НОЛЬ — строго на высоте головы
        /// (указание владельца 24.10.2026: «перенеси из нулевых координат на высоту головы»).
        /// </summary>
        public float belowEyeMeters = 0f;

        /// <summary>Ниже этой высоты глаз поза считается недостоверной, метры.</summary>
        public float minPlausibleEyeHeight = 0.5f;

        private Vector3 lastEye;
        private Vector3 lastForward = Vector3.forward;
        private bool hasEye;

        /// <summary>Шаг между окнами по горизонтали, метры (чтобы не садились друг на друга).</summary>
        public float stepMeters = 0.36f;

        /// <summary>Сколько окон держим одновременно.</summary>
        public int maxWindows = 4;

        /// <summary>Печатать строки в журнал.</summary>
        public bool logOnOpen = true;

        private readonly List<KvXrGroupPanel> windows = new List<KvXrGroupPanel>();
        private readonly List<KvXrPanelLevel> openedAs = new List<KvXrPanelLevel>();

        /// <summary>
        /// КЛЮЧ КАЖДОГО ОКНА (§42). Ключ — id команды компьютера. Дедупликация «по экрану» годилась,
        /// пока у экрана была одна страница; у «Верстака» двадцать три вкладки, и владелец вправе
        /// держать рядом «Верстак · Точки» и «Верстак · Экспорт». Поэтому окно ищется по ПАРЕ
        /// (экран, ключ), а прежний вызов без ключа работает как раньше.
        /// </summary>
        private readonly List<string> openedKeys = new List<string>();

        /// <summary>Сколько окон сейчас открыто.</summary>
        public int Count { get { return windows.Count; } }

        /// <summary>
        /// Последнее открытое окно (для «назад» по кнопке B): закрывается оно, а не первое попавшееся.
        /// </summary>
        public KvXrGroupPanel LastWindow
        {
            get { return windows.Count > 0 ? windows[windows.Count - 1] : null; }
        }

        void Awake()
        {
            Active = this;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        /// <summary>СОЗДАТЬ ХОСТ ОКОН, если его нет (зовёт риг).</summary>
        public static KvXrWindowHost Ensure(Camera camera)
        {
            KvXrWindowHost existing = Object.FindAnyObjectByType<KvXrWindowHost>();
            if (existing != null)
            {
                if (existing.headCamera == null) existing.headCamera = camera;
                return existing;
            }

            var go = new GameObject("KvXrWindows");
            KvXrWindowHost host = go.AddComponent<KvXrWindowHost>();
            host.headCamera = camera;
            return host;
        }

        /// <summary>
        /// ОТКРЫТЬ ОКНО. Если такой экран уже открыт — оно ПОДНИМАЕТСЯ на передний план и
        /// переставляется перед оператором (второе такое же окно не нужно: два «Настройки» на экране
        /// только путают).
        /// </summary>
        public KvXrGroupPanel Open(KvXrPanelLevel screen, string title)
        {
            return Open(screen, title, screen.ToString());
        }

        /// <summary>
        /// ОТКРЫТЬ ОКНО С КЛЮЧОМ (§42). Ключ отличает два окна ОДНОГО экрана друг от друга (вкладки
        /// «Верстака»); повторный вызов с тем же ключом поднимает уже открытое окно, а не создаёт второе.
        /// </summary>
        public KvXrGroupPanel Open(KvXrPanelLevel screen, string title, string key)
        {
            if (string.IsNullOrEmpty(key)) key = screen.ToString();
            KvXrGroupPanel window = FindOpen(screen, key);

            if (window == null)
            {
                window = Create();
                if (window == null) return null;

                windows.Add(window);
                openedAs.Add(screen);
                openedKeys.Add(key);
            }

            window.transform.SetParent(null, false);       // окно живёт в мире, а не в риге
            window.OpenScreen(screen);
            window.gameObject.SetActive(true);

            PlaceWindow(window, windows.IndexOf(window));

            if (logOnOpen)
                Debug.Log("[KvXr] ОКНО: открыто «" + (title ?? KvXrGroupPanel.ScreenTitle(screen)) +
                          "» · окон " + windows.Count + " · в мире на " +
                          window.transform.position.ToString("0.00") + " (не за взглядом)" +
                          " · ключ " + key);

            return window;
        }

        /// <summary>ЗАКРЫТЬ ВСЕ ОКНА (возврат к чистой панели).</summary>
        public void CloseAll()
        {
            for (int i = 0; i < windows.Count; i++)
                if (windows[i] != null) windows[i].gameObject.SetActive(false);

            int closed = windows.Count;
            windows.Clear();
            openedAs.Clear();
            openedKeys.Clear();

            if (logOnOpen && closed > 0) Debug.Log("[KvXr] ОКНО: закрыты все · было " + closed);
        }

        /// <summary>Уже открытое окно с таким экраном (или пусто).</summary>
        public KvXrGroupPanel FindOpen(KvXrPanelLevel screen)
        {
            for (int i = 0; i < windows.Count && i < openedAs.Count; i++)
                if (windows[i] != null && openedAs[i] == screen) return windows[i];
            return null;
        }

        /// <summary>Уже открытое окно с такой парой (экран, ключ) — или пусто (§42).</summary>
        public KvXrGroupPanel FindOpen(KvXrPanelLevel screen, string key)
        {
            if (string.IsNullOrEmpty(key)) return FindOpen(screen);
            for (int i = 0; i < windows.Count && i < openedAs.Count; i++)
            {
                if (windows[i] == null || openedAs[i] != screen) continue;
                string held = i < openedKeys.Count ? openedKeys[i] : null;
                if (held == key) return windows[i];
            }
            return null;
        }

        /// <summary>Ключ открытого окна (для журнала и проверок); пусто — окно без ключа.</summary>
        public string KeyOf(KvXrGroupPanel window)
        {
            int index = windows.IndexOf(window);
            if (index < 0 || index >= openedKeys.Count) return "";
            return openedKeys[index];
        }

        /// <summary>Создать пустое окно (панель, не следующая за рукой).</summary>
        private KvXrGroupPanel Create()
        {
            int index = windows.Count;

            // Больше `maxWindows` — самое старое окно уходит: иначе они заполонят комнату и закроют
            // друг друга, а луч будет попадать в случайное.
            if (maxWindows > 0 && index >= maxWindows)
            {
                KvXrGroupPanel oldest = windows[0];
                windows.RemoveAt(0);
                openedAs.RemoveAt(0);
                if (openedKeys.Count > 0) openedKeys.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
                index = windows.Count;
            }

            var go = new GameObject("KvXrWindow_" + index);
            go.transform.SetParent(null, false);

            KvXrGroupPanel panel = go.AddComponent<KvXrGroupPanel>();
            panel.headCamera = headCamera;
            panel.followHand = false;
            panel.isWindow = true;
            panel.logOnBuild = false;
            panel.sortingOrder = 80;       // ВЫШЕ меню (70): окно должно быть ПЕРЕД менюшной панелью
            return panel;
        }

        /// <summary>
        /// ЗАКРЫТЬ ОДНО ОКНО (крестик внизу окна). Окно уничтожается, а не превращается в иконки групп.
        /// </summary>
        public void Close(KvXrGroupPanel window)
        {
            if (window == null) return;

            int index = windows.IndexOf(window);
            if (index >= 0)
            {
                windows.RemoveAt(index);
                openedAs.RemoveAt(index);
                if (index < openedKeys.Count) openedKeys.RemoveAt(index);
            }

            if (logOnOpen) Debug.Log("[KvXr] ОКНО: закрыто · осталось окон " + windows.Count);
            Destroy(window.gameObject);
        }

        /// <summary>
        /// ПОСТАВИТЬ ОКНО ПЕРЕД ОПЕРАТОРОМ — ОДИН РАЗ. Окна расходятся по горизонтали, чтобы не
        /// накладываться. Разворот — лицом к оператору (то же правило, что у меню: знак проверен
        /// владельцем 24.10.2026).
        /// </summary>
        public void PlaceWindow(KvXrGroupPanel window, int slot)
        {
            if (window == null) return;

            TrackEye();

            Vector3 eye = hasEye ? lastEye : transform.position + Vector3.up * 1.6f;
            Vector3 forward = lastForward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.5f) right = Vector3.right;

            int side = slot % 2 == 0 ? 1 : -1;
            int step = (slot + 1) / 2;
            float lateral = side * step * Mathf.Max(0f, stepMeters);

            Vector3 position = eye + forward * Mathf.Clamp(distanceMeters, 0.40f, 1.60f) + right * lateral;

            // ВЫСОТА — ГОЛОВЫ, А НЕ НУЛЕВЫХ КООРДИНАТ (указание владельца 24.10.2026: «перенеси из
            // нулевых координат на высоту головы»). Раньше здесь вычиталось смещение от уровня глаз, а
            // при недостоверной позе окно вставало у пола.
            position.y = eye.y - Mathf.Clamp(belowEyeMeters, 0f, 0.40f);

            window.transform.position = position;
            window.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            if (logOnOpen)
                Debug.Log("[KvXr] ОКНО: поставлено на высоте головы · " + position.ToString("0.00") +
                          " м · глаз " + eye.y.ToString("0.00") + " м");
        }

        /// <summary>
        /// ЗАПОМНИТЬ ПОЛОЖЕНИЕ ГЛАЗ. Поза шлема спрашивается у `KvXrInputSource` — она проходит
        /// проверку правдоподобия; камера — запасной путь. Нулевая поза не принимается: иначе окна
        /// встали бы «из нулевых координат».
        /// </summary>
        private void TrackEye()
        {
            Vector3 headPosition;
            Quaternion headRotation;

            if (KvXrInputSource.TryHeadPose(out headPosition, out headRotation) &&
                headPosition.y >= minPlausibleEyeHeight)
            {
                lastEye = headPosition;
                lastForward = FlatForward(headRotation * Vector3.forward);
                hasEye = true;
                return;
            }

            Camera camera = headCamera != null ? headCamera : Camera.main;
            if (camera != null && camera.transform.position.y >= minPlausibleEyeHeight)
            {
                lastEye = camera.transform.position;
                lastForward = FlatForward(camera.transform.forward);
                hasEye = true;
            }
        }

        /// <summary>Горизонтальное направление взгляда (без наклона головы).</summary>
        private static Vector3 FlatForward(Vector3 direction)
        {
            Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up);
            return planar.sqrMagnitude > 0.0001f ? planar.normalized : Vector3.forward;
        }

        void Update()
        {
            TrackEye();
        }
    }
}
