using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KazistovVvFeatures;

namespace KazistovVvXr
{
    /// <summary>
    /// ЛУЧ КОНТРОЛЛЕРА ПО ИНТЕРФЕЙСУ (решение владельца [D1], этап 2–3 плана XR).
    ///
    /// ЗАЧЕМ. В шлеме нет мыши, и весь проект, построенный вокруг системного курсора,
    /// перестаёт управляться. Здесь контроллер превращается в «мышь»: из руки идёт луч,
    /// он попадает в панель интерфейса, курок нажимает кнопку. Это и есть минимальный
    /// рабочий ввод, после которого шлем перестаёт быть кинозалом.
    ///
    /// КАК ЭТО РАБОТАЕТ БЕЗ XRI. Используется штатный `GraphicRaycaster` канваса: он умеет
    /// пускать луч в мировом пространстве (для Canvas в режиме World Space). Положение
    /// указателя передаётся в мировых координатах — этого достаточно, чтобы кнопки
    /// подсвечивались и нажимались. Ни одного нового пакета для этого не нужно.
    ///
    /// ПОЧЕМУ УЧАСТИЕ В ТЕЛЕПОРТЕ. Курок в VR — единственная удобная кнопка. Если он будет
    /// одновременно нажимать кнопку интерфейса И телепортировать оператора, получится ровно
    /// тот дефект «одно нажатие — два действия», который в §27 лечили на десктопе.
    /// Поэтому при наведении на интерфейс телепорт молчит (<see cref="AimingAtUi"/>).
    ///
    /// ЧЕСТНОЕ ОГРАНИЧЕНИЕ: без живого шлема проверено только на стенде — расчёт луча,
    /// наведение и нажатие. Что кнопки в шлеме попадают в цель «с первого раза»,
    /// проверяется на устройстве по чек-листу приёмки.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrUiPointer : MonoBehaviour
    {
        /// <summary>Длина луча по умолчанию, метры (в помещении достаточно 10).</summary>
        public float rayLength = 10f;

        /// <summary>Толщина луча, метры.</summary>
        public float rayWidth = 0.008f;

        /// <summary>Цвет луча, когда он ни во что не попал.</summary>
        public Color idleColor = new Color(0.55f, 0.75f, 1f, 0.55f);

        /// <summary>Цвет луча при наведении на элемент интерфейса.</summary>
        public Color hoverColor = new Color(0.30f, 0.95f, 0.55f, 0.95f);

        /// <summary>Порог курка, при котором считается «нажато».</summary>
        public float triggerThreshold = 0.5f;

        private LineRenderer line;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private PointerEventData pointerData;
        private GameObject currentHover;
        private bool triggerWasDown;
        private bool aimingAtUi;

        /// <summary>Луч сейчас наведён на элемент интерфейса (нужно локомоции: не телепортировать).</summary>
        public bool AimingAtUi { get { return aimingAtUi; } }

        /// <summary>Имя элемента под лучом (для журнала и ручной проверки в шлеме).</summary>
        public string HoverName { get { return currentHover != null ? currentHover.name : "—"; } }

        /// <summary>Сколько нажатий по интерфейсу сделано — метрика проверки ввода.</summary>
        public int ClickCount { get; private set; }

        void Awake()
        {
            EnsureLine();
            EnsurePointerData();
        }

        void OnDisable()
        {
            if (line != null) line.enabled = false;
            if (currentHover != null) ClearHover();
        }

        void Update()
        {
            if (KvXrSettings.GetShowPanel() == false)
            {
                if (line != null) line.enabled = false;
                aimingAtUi = false;
                return;
            }

            if (!KvXrInputSource.TryRightPose(out Vector3 handPosition, out Quaternion handRotation))
            {
                if (line != null) line.enabled = false;
                aimingAtUi = false;
                ClearHover();
                return;
            }

            Vector3 direction = handRotation * Vector3.forward;
            Aiming(handPosition, direction);
        }

        /// <summary>
        /// Один шаг луча: найти цель, подсветить, нажать при курке.
        /// Вынесено отдельным методом, чтобы это можно было проверить без шлема
        /// (подставив произвольные позицию и направление).
        /// </summary>
        public void Aiming(Vector3 handPosition, Vector3 direction)
        {
            EnsureLine();
            EnsurePointerData();

            float length = rayLength;
            GameObject target = null;
            Vector3 endPoint = handPosition + direction.normalized * rayLength;

            if (EventSystem.current != null && DirectionalUi(handPosition, direction))
            {
                target = pointerData.pointerCurrentRaycast.gameObject;
                endPoint = pointerData.pointerCurrentRaycast.worldPosition;
                length = Vector3.Distance(handPosition, endPoint);
            }

            aimingAtUi = target != null;

            if (line != null)
            {
                line.enabled = true;
                line.SetPosition(0, handPosition);
                line.SetPosition(1, endPoint);
                line.startColor = aimingAtUi ? hoverColor : idleColor;
                line.endColor = line.startColor;
            }

            SetHover(target);
            HandleClick(target);
        }

        /// <summary>
        /// Пройти лучом по канвасу и заполнить данные указателя.
        ///
        /// Ключевая тонкость: в мировом пространстве `GraphicRaycaster` ожидает координаты
        /// указателя В МИРОВЫХ единицах, а не в пикселях экрана. Поэтому в `position`
        /// кладётся точка попадания, а не позиция мыши.
        /// </summary>
        private bool DirectionalUi(Vector3 origin, Vector3 direction)
        {
            pointerData.position = origin;
            pointerData.delta = Vector2.zero;
            pointerData.scrollDelta = Vector2.zero;
            pointerData.pressPosition = origin;
            pointerData.pointerCurrentRaycast = default(RaycastResult);
            pointerData.pointerEnter = null;

            hits.Clear();
            EventSystem.current.RaycastAll(pointerData, hits);

            for (int i = 0; i < hits.Count; i++)
            {
                GameObject go = hits[i].gameObject;
                if (go == null) continue;
                if (go.GetComponent<Selectable>() == null) continue;   // интересуют только органы управления
                pointerData.pointerCurrentRaycast = hits[i];
                pointerData.pointerEnter = go;
                pointerData.position = hits[i].worldPosition;
                return true;
            }
            return false;
        }

        private void SetHover(GameObject target)
        {
            if (target == currentHover) return;
            ClearHover();
            currentHover = target;
            if (currentHover == null) return;

            pointerData.pointerEnter = currentHover;
            ExecuteEvents.ExecuteHierarchy(currentHover, pointerData, ExecuteEvents.pointerEnterHandler);
        }

        private void ClearHover()
        {
            if (currentHover != null && pointerData != null)
                ExecuteEvents.ExecuteHierarchy(currentHover, pointerData, ExecuteEvents.pointerExitHandler);
            currentHover = null;
            if (pointerData != null) pointerData.pointerEnter = null;
        }

        /// <summary>
        /// Нажатие курком. Событие уходит ИЕРАРХИИ: кнопка часто лежит внутри строки,
        /// и обработчик живёт выше по дереву — как `ExecuteEvents.ExecuteHierarchy` и делают.
        /// </summary>
        private void HandleClick(GameObject target)
        {
            bool down = KvXrInputSource.RightTrigger >= triggerThreshold;

            if (down && !triggerWasDown && target != null)
            {
                pointerData.pointerPress = target;
                pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;
                pointerData.eligibleForClick = true;
                ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerDownHandler);
            }
            else if (!down && triggerWasDown)
            {
                GameObject press = pointerData.pointerPress;
                pointerData.pointerPress = null;
                pointerData.eligibleForClick = false;
                if (press != null)
                {
                    ExecuteEvents.ExecuteHierarchy(press, pointerData, ExecuteEvents.pointerUpHandler);
                    if (target == press)
                    {
                        ExecuteEvents.ExecuteHierarchy(press, pointerData, ExecuteEvents.pointerClickHandler);
                        ClickCount++;
                    }
                }
            }

            triggerWasDown = down;
        }

        private void EnsureLine()
        {
            if (line != null) return;
            line = GetComponent<LineRenderer>();
            if (line == null) line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = rayWidth;
            line.endWidth = rayWidth * 0.4f;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            // Материал луча — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP для шлема,
            // HDRP для компьютера). Жёсткий `Shader.Find("HDRP/Unlit")` под URP дал бы
            // розовый луч — то есть «сломанный указатель» в шлеме.
            Material material = KvRenderPipelineShaders.CreateUnlit("Материал_луча_XR", idleColor);
            if (material != null) line.material = material;
        }

        private void EnsurePointerData()
        {
            if (pointerData == null) pointerData = new PointerEventData(EventSystem.current);
        }

        /// <summary>Строка состояния: куда смотрит луч и сколько нажатий сделано.</summary>
        public string Describe()
        {
            return string.Format("XR-указатель: цель «{0}» · нажатий {1} · на интерфейсе: {2}",
                HoverName, ClickCount, aimingAtUi ? "да" : "нет");
        }
    }
}
