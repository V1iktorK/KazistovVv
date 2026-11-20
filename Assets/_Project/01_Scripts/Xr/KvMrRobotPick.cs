using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using KazistovVvFeatures;
using KazistovVvRealWorld;

namespace KazistovVvXr
{
    /// <summary>
    /// ВЫБОР МЕСТА РОБОТА В MR: РОБОТ НА КОНЦЕ ПРАВОГО ЛУЧА, ПОЗИЦИЯ ФИКСИРУЕТСЯ КЛАВИШЕЙ
    /// (пункты 2–3 требований владельца, 12.11.2026).
    ///
    /// СЛОВА ВЛАДЕЛЬЦА ДОСЛОВНО:
    ///   2. «Сразу после выхода из меню или сворачивания его надо было выбрать положение робота,
    ///      которое задавалось бы окончательно до нажатия на кнопку "переместить робота".»
    ///   3. «К концу правого лазера (который упирается в текстуры и в реальности) прикреплён робот,
    ///      позицию которого надо выбрать.»
    ///
    /// ЧТО БЫЛО. Позицию робота выбирала АВТОПОСТАНОВКА (`KvMrPlacement.TryAutoPlace`): раз в
    /// 4 секунды она сама искала «подтверждённый стол» и переносила робота. Оператор при этом не
    /// выбирал ничего: робот уезжал сам, а «переместить робота» переносил его ВСЛЕПУЮ — то есть в
    /// неизвестное оператору место. ЧЕМ ПЛОХО: место нельзя было ни увидеть заранее, ни зафиксировать,
    /// ни отменить; робот «сам ходил по столу», а на приёмке это выглядело как «программа решает
    /// за меня».
    ///
    /// ЧТО СТАЛО:
    ///   • режим «ВЫБОР МЕСТА РОБОТА» включается САМ сразу после выхода из стартового меню (или его
    ///     сворачивания) — без промежуточных кнопок;
    ///   • к концу ПРАВОГО луча прикреплён робот: позиция = точка попадания, поворот = лицом к
    ///     оператору (то же правило, что в `KvMrPlacement`);
    ///   • позиция ФИКСИРУЕТСЯ клавишей <see cref="ConfirmKeyName"/> — ДО того, как нажмут
    ///     «переместить робота»;
    ///   • пока не зафиксировано, «переместить робота» НЕ переставляет вслепую: она отказывается и
    ///     называет причину (требование пункта 4);
    ///   • если луч не попал ни в реальную поверхность, ни в объект сцены — робот НЕ улетает в
    ///     пустоту: показывается причина, а последняя валидная позиция сохраняется (пункт 5).
    ///
    /// ПОЧЕМУ КЛАВИША, А НЕ КНОПКА КОНТРОЛЛЕРА. Курок, стик, грип и кнопки A/B/X/Y в проекте уже
    /// заняты (нажатие по панели, телепорт, перенос окна, E-STOP, меню, панель у руки, поиск) —
    /// шестое действие на ту же кнопку было бы дефектом E-08 «одно нажатие — два действия».
    /// Поэтому фиксация — КЛАВИША, а кнопка A остаётся кнопкой A: она подтверждает точку в
    /// `KvMrPlacement` и здесь ничего не отбирает.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvMrRobotPick : MonoBehaviour
    {
        /// <summary>ПРЕФИКС СТРОК ЖУРНАЛА ЭТОГО РЕЖИМА — по нему ищет приёмка.</summary>
        public const string LogPrefix = "[KvMr] ";

        /// <summary>
        /// КЛАВИША ФИКСАЦИИ МЕСТА — <b>B</b>.
        ///
        /// ПОЧЕМУ ИМЕННО B И ПОЧЕМУ ОНА СВОБОДНА (проверено по коду, а не на глаз):
        ///   • в проекте вообще не встречается `KeyCode.B` / `bKey` — ни в одном файле `01_Scripts`
        ///     (проверено поиском по всем обработчикам клавиш);
        ///   • рядом заняты: A/S/D/W/Q/E — ход и вертикаль (`FreeFlyCameraController`), Z/X — лазеры,
        ///     G — фонарик, F — выбор робота по прицелу, R — запись, P — воспроизведение, V — захват,
        ///     H — теплокарта, J — зазоры, T — ничего (не объявлена), Tab — режим курсора,
        ///     Esc — контекст/меню, F5–F12 — окна и снимки.
        ///   • «B» читается как «Bind/Bei/ВыБор» и стоит рядом с WASD, то есть под левой рукой, пока
        ///     правая держит контроллер.
        /// Свободу клавиши проверяет тест `ConfirmKey_IsNotUsedAnywhereElse` — он читает исходники и
        /// падает, если кто-то займёт B под другое действие.
        /// </summary>
        public const Key ConfirmKey = Key.B;

        /// <summary>Имя клавиши для подсказки интерфейса и журнала.</summary>
        public const string ConfirmKeyName = "B";

        /// <summary>Дальность правого луча при выборе места, метры (как у указателя).</summary>
        public const float MaxAimDistanceMeters = 10f;

        /// <summary>Насколько поднять робота над точкой попадания, метры (ноль — ставим РОВНО в точку).</summary>
        public const float SurfaceLiftMeters = 0f;

        /// <summary>
        /// НАКЛОН ПОВЕРХНОСТИ, ПРИ КОТОРОМ ОНА ЕЩЁ ГОДИТСЯ ДЛЯ РОБОТА (косинус угла к вертикали).
        /// 0.60 — тот же порог, что у постановки (`KvMrPlacement.minUpNormalDot`): одно правило на
        /// «куда можно ставить», иначе режим выбора и постановка расходились бы в оценке площадки.
        /// </summary>
        public const float MinUpNormalDot = 0.60f;

        /// <summary>Режим выбора места робота включён.</summary>
        public bool PickActive { get; private set; }

        /// <summary>
        /// МЕСТО ЗАФИКСИРОВАНО (требование пункта 3): до фиксации «переместить робота» недоступна.
        /// </summary>
        public bool PlaceFixed { get; private set; }

        /// <summary>Зафиксированная точка (имеет смысл только при <see cref="PlaceFixed"/>).</summary>
        public Vector3 FixedPoint { get; private set; }

        /// <summary>Зафиксированный поворот робота, градусы.</summary>
        public float FixedYawDegrees { get; private set; }

        /// <summary>
        /// ПОСЛЕДНЯЯ ВАЛИДНАЯ ПОЗИЦИЯ (требование пункта 5). Сохраняется при КАЖДОМ удачном
        /// попадании правого луча и НЕ обнуляется, когда луч ушёл в пустоту: иначе робота было бы
        /// некуда вернуть, и он «висел бы в воздухе» после промаха.
        /// </summary>
        public Vector3 LastValidPoint { get; private set; }

        /// <summary>Была ли хоть одна валидная позиция (иначе возвращать некуда).</summary>
        public bool HasLastValidPoint { get; private set; }

        /// <summary>Текущее попадание правого луча (или пустое, если попадания нет).</summary>
        public KvXrRayHit CurrentHit { get { return currentHit; } }

        /// <summary>Почему сейчас нельзя ставить робота — словами (пусто — можно).</summary>
        public string BlockReason { get; private set; }

        /// <summary>Строка состояния одной строкой — для журнала и экрана.</summary>
        public string Status
        {
            get
            {
                if (!PickActive) return "выбор места робота: режим выключен";
                if (!PlaceFixed) return "выбор места робота: ждём фиксации клавишей " + ConfirmKeyName +
                                        (currentHit.Any
                                            ? " · луч правый: " + KvXrRayHitRules.SourceName(currentHit.Source) +
                                              " «" + currentHit.Name + "»"
                                            : " · луч правый: " + DescribeNoHit());
                return "выбор места робота: МЕСТО ЗАФИКСИРОВАНО (" + Num(FixedPoint) + ")";
            }
        }

        /// <summary>Камера оператора (поворот «лицом к оператору» и запасное направление луча).</summary>
        public Camera headCamera;

        /// <summary>MR-комната: она знает, кого двигать.</summary>
        public KvXrMrRoom room;

        /// <summary>Текущий экземпляр (нужен пункту меню и проверкам).</summary>
        public static KvMrRobotPick Active { get; private set; }

        private KvXrUiPointer pointer;
        private KvMrPlacement placement;
        private KvXrRayHit currentHit;
        private bool confirmKeyWasDown;
        private string lastMissReason = string.Empty;

        /// <summary>
        /// Создать компонент в рантайме (сцена не правится — её SHA256 закреплён тестом).
        ///
        /// ПОЧЕМУ ЗДЕСЬ ЖЕ ПОДСТАВЛЯЕТСЯ УКАЗАТЕЛЬ. `Ensure` зовётся ОДИН раз из
        /// `KvXrMrRoom.BeginMrSession`, то есть это место, где ссылки ещё можно поискать даром.
        /// Так `Update` не ищет по сцене вовсе: он читает поля.
        /// </summary>
        public static KvMrRobotPick Ensure(Transform parent, Camera camera, KvXrMrRoom mrRoom)
        {
            if (Active != null)
            {
                if (Active.headCamera == null) Active.headCamera = camera;
                if (Active.room == null) Active.room = mrRoom;
                return Active;
            }

            var go = new GameObject("KvMrRobotPick");
            if (parent != null) go.transform.SetParent(parent, false);
            KvMrRobotPick pick = go.AddComponent<KvMrRobotPick>();
            pick.headCamera = camera;
            pick.room = mrRoom;
            pick.NotePointer(Object.FindAnyObjectByType<KvXrUiPointer>());
            return pick;
        }

        void Awake()
        {
            Active = this;

            // ЗАПОМНИТЬ MR-КОМНАТУ ОДИН РАЗ, А НЕ ИСКАТЬ В КАЖДОМ КАДРЕ.
            //
            // ЧТО БЫЛО (найдено прогоном `KvFrameBudgetTests.UpdateHasNoExpensiveCalls`). В `Update`
            // стояло `if (room == null) room = FindAnyObjectByType<KvXrMrRoom>();`. ЧЕМ ПЛОХО:
            // `FindAnyObjectByType` — это ОБХОД ВСЕЙ СЦЕНЫ, а `Update` на шлеме выполняется 90 раз
            // в секунду. Бюджет кадра там 11 мс, и обход сцены в нём — заметная доля: оператор
            // видел рывки ровно в тот момент, когда целится лучом, то есть когда это важнее всего.
            // ЧТО СТАЛО: комната берётся из её же статического реестра `KvXrMrRoom.Instance`
            // (компонент ставит ссылку в `OnEnable`) — это чтение поля, а не поиск.
            CacheRoom();
        }

        /// <summary>
        /// ССЫЛКИ СОБИРАЮТСЯ В `Start`, А НЕ В КАДРЕ. `Awake` компонента может выполниться раньше,
        /// чем появятся комната и указатель (оба создаются из `KvXrMrRoom.BeginMrSession` и рига),
        /// поэтому второй проход делается здесь — ОДИН раз за время жизни компонента.
        /// </summary>
        void Start()
        {
            if (room == null) room = KvXrMrRoom.Instance;
            if (pointer == null) NotePointer(Object.FindAnyObjectByType<KvXrUiPointer>());
            if (headCamera == null) headCamera = Camera.main;
        }

        /// <summary>
        /// ВЗЯТЬ MR-КОМНАТУ БЕЗ ПОИСКА ПО СЦЕНЕ. Сначала поле (его ставит `Ensure`), затем общий
        /// реестр `KvXrMrRoom.Instance`. Ссылка может обнулиться при смене режима или пересоздании
        /// комнаты — тогда её восстанавливает `RefreshCachedReferences`, а не кадровый поиск.
        /// </summary>
        private void CacheRoom()
        {
            if (room == null) room = KvXrMrRoom.Instance;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        void Update()
        {
            // ССЫЛКИ ОБНОВЛЯЮТСЯ ПО СОБЫТИЮ, А НЕ ПОИСКОМ В КАДРЕ. Проверка «ссылка жива» стоит
            // одно сравнение; поиск идёт только когда ссылки ДЕЙСТВИТЕЛЬНО нет (смена сцены,
            // пересоздание комнаты), и это случается единицы раз за сеанс, а не 90 раз в секунду.
            if (room == null) CacheRoom();

            if (room == null || !room.MrActive)
            {
                if (PickActive) StopPicking("режим MR выключен");
                return;
            }

            UpdateHit();
            ReadConfirmKey();
            FollowRightRay();
        }

        // ---------------------------------------------------------------- режим

        /// <summary>
        /// ВЫХОД ИЗ МЕНЮ (или его СВОРАЧИВАНИЕ) — ВКЛЮЧИТЬ ВЫБОР МЕСТА (пункт 2 владельца).
        ///
        /// Зовётся из `KvXrStartMenu.Hide()` — единственного места, где меню перестаёт быть видимым
        /// (и кнопкой X, и кнопкой B, и стиком, и пунктом «Выход»). Ставить вызов в каждый из этих
        /// путей по отдельности значило бы однажды забыть один — и режим «не включился после меню».
        ///
        /// УЖЕ ЗАФИКСИРОВАННОЕ МЕСТО НЕ СБРАСЫВАЕТСЯ. ЧТО БЫЛО БЫ, ЕСЛИ СБРАСЫВАТЬ: оператор
        /// зафиксировал место, на секунду открыл меню (посмотреть настройки) и закрыл — и фиксация
        /// молча пропала бы, а «переместить робота» снова отказала бы. Требование владельца —
        /// «задавалось бы ОКОНЧАТЕЛЬНО до нажатия на кнопку», то есть фиксация переживает меню.
        /// </summary>
        public void EnterAfterMenu()
        {
            if (PickActive) return;         // режим уже идёт — второй строки и второго сброса нет
            BeginPicking("после выхода из меню");
        }

        /// <summary>Включить режим выбора места с явной причиной (для журнала).</summary>
        public void BeginPicking(string cause)
        {
            PickActive = true;
            PlaceFixed = false;
            BlockReason = string.Empty;

            // РОБОТ ПОКАЗЫВАЕТСЯ ЗДЕСЬ ЖЕ (пункт 3 требования владельца 12.11.2026).
            //
            // ЧТО БЫЛО. Режим «включался», но робота в мире не было: его скрывала постановка
            // (`KvMrPlacement`), а включать обратно было НЕКОМУ. Оператор после выхода из меню видел
            // пустой стол и надпись «ведём робота за лучом» — вести было некого.
            // ЧЕМ ПЛОХО. Требование «после закрытия меню робот следует за правым лучом» не выполнялось
            // именно на самом заметном шаге: робота не видно.
            // ЧТО СТАЛО. Включение режима и показ робота — ОДНО действие: `BeginOperatorChoice`
            // снимает скрытость, включает объект и перезапускает отсчёт авто-постановки.
            if (placement == null) placement = KvMrPlacement.Active;
            if (placement != null) placement.BeginOperatorChoice();
            else if (room != null && room.robotOnTable != null) room.robotOnTable.gameObject.SetActive(true);

            // ПЕРВАЯ СТРОКА — ДОСЛОВНО ИЗ ТРЕБОВАНИЯ ВЛАДЕЛЬЦА, ВТОРАЯ — ПОДСКАЗКА «что дальше».
            // ЧТО БЫЛО: одна строка с хвостом «затем «переместить робота»», из-за чего требуемая форма
            // («… · фиксация — клавиша B») не совпадала с напечатанной.
            // ЧТО СТАЛО: две строки, и первая — ровно та, которую ищет приёмка.
            Debug.Log(DescribeModeOn(cause) + ModeOnLogTail());
            Debug.Log(LogPrefix + "ВЫБОР МЕСТА РОБОТА: после фиксации клавишей " + ConfirmKeyName +
                      " нажмите «переместить робота» — без фиксации кнопка откажет" + HintTail());
            RememberAnswer("включён после выхода из меню");
        }

        /// <summary>Выключить режим (выход из MR, подтверждённая постановка).</summary>
        public void StopPicking(string cause)
        {
            if (!PickActive) return;
            PickActive = false;
            BlockReason = string.Empty;
            Debug.Log(LogPrefix + "ВЫБОР МЕСТА РОБОТА: режим выключен (" +
                      (string.IsNullOrEmpty(cause) ? "без причины" : cause) + ")");
        }

        /// <summary>
        /// ДОПУСТИМА ЛИ ПОСТАНОВКА ПРЯМО СЕЙЧАС — ЧИСТАЯ ФУНКЦИЯ (проверяется тестом без сцены).
        ///
        /// ПРАВИЛО (требование пункта 4 владельца): «пока место НЕ зафиксировано, кнопка
        /// "переместить робота" не должна переставлять вслепую». Ответ — либо «можно», либо причина
        /// словами; «молча ничего не делать» запрещено: оператор должен знать, чего не хватает.
        /// </summary>
        public static bool CanPlace(bool pickActive, bool placeFixed, bool hasLastValidPoint,
            bool hitIsSurface, out string reason)
        {
            if (!pickActive)
            {
                reason = "режим выбора места не включён — он включается после выхода из меню";
                return false;
            }
            if (!placeFixed)
            {
                reason = "сначала зафиксируйте место клавишей " + ConfirmKeyName +
                         " — иначе робот переставится вслепую";
                return false;
            }
            if (!hitIsSurface && !hasLastValidPoint)
            {
                reason = "луч не попал ни в реальную поверхность, ни в объект сцены — " +
                         "наведите ПРАВЫЙ луч на стол";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// НАДО ЛИ ВКЛЮЧАТЬ РЕЖИМ ПОСЛЕ СМЕНЫ ВИДИМОСТИ МЕНЮ — ЧИСТАЯ ФУНКЦИЯ (анти-вакуум).
        ///
        /// ЧТО БЫЛО НЕ ТАК И ЧЕМ ЭТО ГРОЗИЛО. Если включать режим по одному факту «кадр прошёл» или
        /// «компонент создан», он включался бы и тогда, когда меню НИКОГДА не открывалось: оператор
        /// попал бы в MR и сразу получил «выбор места», хотя требование владельца — «СРАЗУ ПОСЛЕ
        /// ВЫХОДА ИЗ МЕНЮ». Поэтому нужен именно ФРОНТ «было видно → стало не видно».
        /// </summary>
        public static bool ShouldEnterAfterMenu(bool wasVisible, bool isVisible)
        {
            return wasVisible && !isVisible;
        }

        /// <summary>
        /// ПОПАДАНИЕ ГОДИТСЯ ДЛЯ РОБОТА — ЧИСТАЯ ФУНКЦИЯ.
        ///
        /// Условия: попадание есть, числа конечные (класс E-21: сравнение с NaN всегда false и
        /// пропустило бы мусор), и поверхность не «стена/потолок» — ставить робота на вертикаль
        /// нельзя. Проверяется и реальная поверхность, и виртуальный объект: правый луч обязан
        /// упираться в ОБА вида (пункт 4 владельца).
        /// </summary>
        public static bool IsSurfaceSuitable(KvXrRayHit hit)
        {
            if (!hit.Any) return false;
            if (!KvXrRayHitRules.IsFinite(hit.Point) || !KvXrRayHitRules.IsFinite(hit.Normal)) return false;
            Vector3 normal = hit.Normal.sqrMagnitude > 0.0001f ? hit.Normal.normalized : Vector3.up;
            return normal.y >= MinUpNormalDot;
        }

        // ---------------------------------------------------------------- луч и робот

        /// <summary>
        /// ПОПАДАНИЕ ПРАВОГО ЛУЧА. Берётся РОВНО то, что уже посчитал указатель
        /// (`KvXrUiPointer.LastRayHit`): второй расчёт разошёлся бы с нарисованным кончиком луча —
        /// и оператор увидел бы одно, а подтвердил другое. Если указателя нет (луч выключен в
        /// настройках), попадание считается тем же единым источником, что у лучей.
        ///
        /// ПОИСКА ПО СЦЕНЕ ЗДЕСЬ НЕТ. Указатель запоминается в `Ensure` (при создании компонента) и
        /// в `Start` (один раз на старте). Раньше здесь стояло `FindAnyObjectByType<KvXrUiPointer>()`
        /// под `if (pointer == null)` — и это был обход сцены КАЖДЫЙ КАДР всё время, пока указатель
        /// не создан (а он не создан, пока луч выключен в настройках). На шлеме это 90 обходов
        /// сцены в секунду в бюджете кадра 11 мс.
        /// </summary>
        private void UpdateHit()
        {
            if (pointer != null)
            {
                currentHit = pointer.LastRayHit;
                return;
            }

            Vector3 origin;
            Vector3 direction;
            if (!TryAimDirection(out origin, out direction))
            {
                currentHit = new KvXrRayHit();
                return;
            }
            currentHit = KvXrRayHitProbe.Nearest(origin, direction, MaxAimDistanceMeters);
        }

        /// <summary>
        /// ЗАПОМНИТЬ УКАЗАТЕЛЬ ПО СОБЫТИЮ. Зовётся из `Ensure` и `Start` — то есть при создании
        /// компонента и один раз на старте, а не в кадре. Если указатель пересоздадут, его подставит
        /// следующая регистрация; до тех пор попадание считается напрямую (`KvXrRayHitProbe`), и
        /// поведение остаётся правильным — просто без готового результата указателя.
        /// </summary>
        public void NotePointer(KvXrUiPointer pointerRef)
        {
            if (pointerRef != null) pointer = pointerRef;
        }

        /// <summary>ГЛАВНАЯ КАМЕРА БЕЗ ПОИСКА В КАДРЕ. `Camera.main` — это поиск по тегу среди всех
        /// камер сцены, а не свойство. В проекте камер несколько (вид сверху, сбоку, от первого
        /// лица), поэтому поиск небесплатен, и в кадре ему не место.</summary>
        private Camera ResolveHeadCamera()
        {
            if (headCamera != null) return headCamera;
            headCamera = Camera.main;      // единственный вызов: когда камеры ещё нет
            return headCamera;
        }

        /// <summary>Направление правого луча: контроллер правой руки, иначе взгляд.</summary>
        private bool TryAimDirection(out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            if (KvXrInputSource.TryAimPose(KvXrQuickHand.Right, out origin, out Quaternion rotation))
            {
                direction = rotation * Vector3.forward;
                return true;
            }

            Camera head = ResolveHeadCamera();
            if (head == null) return false;

            origin = head.transform.position;
            direction = Quaternion.Euler(20f, head.transform.eulerAngles.y, 0f) * Vector3.forward;
            return true;
        }

        /// <summary>
        /// РОБОТ НА КОНЦЕ ПРАВОГО ЛУЧА (пункт 3 владельца).
        ///
        /// ПРАВИЛО: попадание годится → робот в этой точке и последняя валидная позиция обновляется;
        /// попадания нет или поверхность не годится → робот НЕ ДВИГАЕТСЯ, а оператор видит причину
        /// (пункт 5: «не улетает в пустоту»). Зафиксированное место важнее луча: после фиксации робот
        /// стоит там, где его утвердили, и вести его дальше лучом нельзя — иначе фиксация ничего не
        /// значит.
        /// </summary>
        private void FollowRightRay()
        {
            if (room == null) return;

            if (PlaceFixed)
            {
                MoveRobot(FixedPoint, FixedYawDegrees);
                return;
            }

            if (!IsSurfaceSuitable(currentHit))
            {
                ReportMiss(DescribeNoHit());
                return;
            }

            lastMissReason = string.Empty;

            Vector3 point = currentHit.Point + Vector3.up * SurfaceLiftMeters;
            float yaw = YawFacingOperator(point);
            LastValidPoint = point;
            HasLastValidPoint = true;
            MoveRobot(point, yaw);
        }

        private void MoveRobot(Vector3 point, float yawDegrees)
        {
            if (room == null || room.robotOnTable == null) return;
            room.robotOnTable.position = point;
            room.robotOnTable.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        /// <summary>
        /// Поворот робота лицом к оператору (то же правило, что в `KvMrPlacement`).
        ///
        /// КАМЕРА БЕРЁТСЯ ИЗ КЭША. Здесь стояло `Camera.main` — и это вызывалось каждый кадр из
        /// `FollowRightRay`, пока робот идёт за лучом. `Camera.main` не свойство, а ПОИСК по тегу
        /// среди всех камер сцены: в шлеме это 90 поисков в секунду в бюджете 11 мс. Теперь камера
        /// ищется только когда её действительно нет (`ResolveHeadCamera`), а дальше читается поле.
        /// </summary>
        private float YawFacingOperator(Vector3 point)
        {
            Camera head = ResolveHeadCamera();
            if (head == null) return 0f;

            Vector3 toHead = head.transform.position - point;
            if (!KvXrRayHitRules.IsFinite(toHead)) return 0f;
            toHead.y = 0f;
            if (toHead.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(toHead.x, toHead.z) * Mathf.Rad2Deg;
        }

        // ---------------------------------------------------------------- фиксация

        /// <summary>
        /// ЧТЕНИЕ КЛАВИШИ ФИКСАЦИИ — ПО ФРОНТУ (одно физическое нажатие = одна фиксация).
        ///
        /// ПОЧЕМУ ФРОНТ ПО СОСТОЯНИЮ, А НЕ `wasPressedThisFrame`. В Input System 1.20 последнее
        /// означает «нажатие с момента последнего обновления ввода», а не «в этот кадр»: в редакторе
        /// (первый кадр после фокуса, тяжёлый кадр) условие истинно ДВА кадра подряд, и одно нажатие
        /// сработало бы дважды — этот дефект в проекте уже был (фонарик «включался со второго раза»).
        /// Здесь защёлка своя: повтор возможен только после отпускания клавиши.
        /// </summary>
        private void ReadConfirmKey()
        {
            bool down = IsConfirmKeyHeld();
            bool edge = down && !confirmKeyWasDown;
            confirmKeyWasDown = down;

            if (!edge || !PickActive) return;
            FixPlacement();
        }

        private static bool IsConfirmKeyHeld()
        {
            try
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    KeyControl control = KeyControlOf(keyboard);
                    if (control != null) return control.isPressed;
                }
            }
            catch (System.Exception)
            {
                // Клавиатуры может не быть вовсе (шлем без ПК) — это не ошибка, а отсутствие ввода.
            }
            return false;
        }

        /// <summary>
        /// ЭЛЕМЕНТ КЛАВИАТУРЫ ДЛЯ КЛАВИШИ ФИКСАЦИИ.
        ///
        /// ПОЧЕМУ НЕ `switch (ConfirmKey)`. `ConfirmKey` — константа, поэтому ветки `default` и
        /// «а если другая клавиша» НЕДОСТИЖИМЫ, и компилятор честно предупреждает (CS0162).
        /// Предупреждение в этом проекте — долг: шум в выводе приучает его не читать, а настоящая
        /// ошибка в нём теряется. Поэтому клавиша возвращается напрямую, а СВЯЗЬ «клавиша → элемент»
        /// закреплена константой и проверяется тестом `ConfirmKey_IsNamedAndAdvertisedInHint`.
        ///
        /// ЕСЛИ КЛАВИШУ ПОМЕНЯЮТ: поменять здесь одну строку, и тест сразу назовёт расхождение с
        /// подсказкой и журналом — молча разойтись они не смогут.
        /// </summary>
        private static KeyControl KeyControlOf(Keyboard keyboard)
        {
            if (keyboard == null) return null;
            return keyboard.bKey;      // ConfirmKey == Key.B (см. константу выше и тест)
        }

        /// <summary>
        /// ЗАФИКСИРОВАТЬ МЕСТО (требование пункта 3).
        ///
        /// ФИКСИРУЕТСЯ ТОЛЬКО ВАЛИДНАЯ ТОЧКА. Если луч сейчас в пустоте, фиксация берёт ПОСЛЕДНЮЮ
        /// ВАЛИДНУЮ позицию и ГОВОРИТ об этом: «зафиксировано по последней валидной». Иначе оператор
        /// нажал бы B, находясь лучом в воздухе, и получил бы либо отказ без объяснений, либо
        /// (хуже) робота в начале координат.
        /// </summary>
        public void FixPlacement()
        {
            Vector3 point;
            float yaw;
            bool fromLastValid;

            if (IsSurfaceSuitable(currentHit))
            {
                point = currentHit.Point + Vector3.up * SurfaceLiftMeters;
                yaw = YawFacingOperator(point);
                LastValidPoint = point;
                HasLastValidPoint = true;
                fromLastValid = false;
            }
            else if (HasLastValidPoint)
            {
                point = LastValidPoint;
                yaw = YawFacingOperator(point);
                fromLastValid = true;
            }
            else
            {
                ReportMiss("фиксация невозможна: " + DescribeNoHit());
                return;
            }

            FixedPoint = point;
            FixedYawDegrees = yaw;
            PlaceFixed = true;
            MoveRobot(point, yaw);

            // ФИКСАЦИЯ ВЫКЛЮЧАЕТ АВТО-ПОСТАНОВКУ НАВСЕГДА (пункты 4–5 требования владельца).
            //
            // ЧТО БЫЛО. Фиксация запоминала точку, но таймер авто-постановки продолжал работать: раз в
            // `autoPlaceRetrySeconds` он переносил робота «на подтверждённый стол». Оператор видел, что
            // робот уезжает с места, которое только что утвердил.
            // ЧЕМ ПЛОХО. «После подтверждения робот остаётся на этом месте» не выполнялось: место
            // задавалось ОКОНЧАТЕЛЬНО только на словах.
            // ЧТО СТАЛО. `LockPlacement` ставит флаг «автопостановка запрещена»: её больше не запускает
            // ни таймер, ни запасной путь «пол через 25 с». Двигать робота может только явная команда
            // «Переместить робота».
            if (placement == null) placement = KvMrPlacement.Active;
            if (placement != null) placement.LockPlacement("подтверждено клавишей " + ConfirmKeyName);

            // СТРОКА ФИКСАЦИИ — ЧЕРЕЗ ЧИСТУЮ ФУНКЦИЮ: её форму проверяет тест регуляркой на числа,
            // и она же обязана быть ОДНОЙ на журнал и на отчёт.
            Debug.Log(DescribeFixed(point, yaw, ConfirmKeyName) +
                      (fromLastValid
                          ? " · луч сейчас НЕ на поверхности — взята последняя валидная позиция"
                          : string.Empty) +
                      " · авто-постановка отключена");

            var c = CultureInfo.InvariantCulture;
            KvActionLog.Instance.Placement("место робота в MR (выбор по правому лучу)", true,
                "точка (" + point.x.ToString("0.00", c) + "; " +
                point.y.ToString("0.00", c) + "; " +
                point.z.ToString("0.00", c) + ") · поворот " + yaw.ToString("0", c) +
                "° · подтверждено " + ConfirmKeyName + (fromLastValid ? " · последняя валидная" : string.Empty));
        }

        /// <summary>Снять фиксацию (например, оператор снова ведёт лучом).</summary>
        public void Unfix()
        {
            if (!PlaceFixed) return;
            PlaceFixed = false;
            Debug.Log(LogPrefix + "ВЫБОР МЕСТА РОБОТА: фиксация снята — робот снова идёт за правым лучом");
        }

        // ---------------------------------------------------------------- «переместить робота»

        /// <summary>
        /// КНОПКА «ПЕРЕМЕСТИТЬ РОБОТА» — ЧЕСТНАЯ (требование пункта 4).
        ///
        /// ЧТО БЫЛО. Она переставляла робота ВСЛЕПУЮ: место вычислялось внутри и оператор не знал,
        /// куда именно. ЧЕМ ПЛОХО: «нажал — робот уехал неизвестно куда» — ровно то, из-за чего
        /// владелец и просил фиксацию.
        /// ЧТО СТАЛО. Без фиксации кнопка НЕ переставляет и НАЗЫВАЕТ причину (и в журнал, и на
        /// запись действий). С фиксацией — ставит робота ровно в зафиксированную точку.
        /// </summary>
        public bool TryMoveRobot()
        {
            string reason;
            bool hitIsSurface = IsSurfaceSuitable(currentHit);
            if (!CanPlace(PickActive, PlaceFixed, HasLastValidPoint, hitIsSurface, out reason))
            {
                BlockReason = reason;
                Debug.LogWarning(LogPrefix + "ПЕРЕМЕСТИТЬ РОБОТА: отказ — " + reason +
                                 (HasLastValidPoint
                                     ? " · последняя валидная позиция сохранена: " + Num(LastValidPoint) + " м"
                                     : string.Empty));
                KvActionLog.Instance.Placement("перемещение робота в MR", false, reason);
                return false;
            }

            if (placement == null) placement = KvMrPlacement.Active;
            Vector3 point = PlaceFixed ? FixedPoint : LastValidPoint;
            string cause = "по кнопке «переместить робота» (место зафиксировано " + ConfirmKeyName + ")";

            if (placement != null)
            {
                // ОДИН ПУТЬ ПОСТАНОВКИ: та же точка, тот же поворот и та же запись в журнал действий,
                // что у подтверждения кнопкой A. Второй реализации постановки не заводится.
                placement.Place(point, Vector3.up, cause);
            }
            else if (room != null)
            {
                if (!room.PlaceRobotAtSurface(point, FixedYawDegrees))
                {
                    BlockReason = room.LastPlacementError;
                    Debug.LogWarning(LogPrefix + "ПЕРЕМЕСТИТЬ РОБОТА: не выполнено — " + room.LastPlacementError);
                    KvActionLog.Instance.Placement("перемещение робота в MR", false, room.LastPlacementError);
                    return false;
                }
            }

            BlockReason = string.Empty;
            Debug.Log(LogPrefix + "ПЕРЕМЕСТИТЬ РОБОТА: выполнено — робот в зафиксированной точке " +
                      Num(point) + " м");
            StopPicking("место робота зафиксировано и применено");
            return true;
        }

        // ---------------------------------------------------------------- причины и строки

        /// <summary>
        /// ПОЧЕМУ РОБОТ НЕ ДВИГАЕТСЯ — ОДНА СТРОКА, И ОНА ЖЕ ПРАВИЛО «НЕ МОЛЧАТЬ» (пункт 5).
        ///
        /// Печатается ТОЛЬКО при смене причины: причина «луч в пустоте» повторяется каждый кадр, и
        /// без этого условия журнал шлема превратился бы в одну и ту же строку.
        /// </summary>
        private void ReportMiss(string reason)
        {
            if (reason == lastMissReason) return;

            // ИНТЕРВАЛ НА СМЕНУ ПРИЧИНЫ (20.11.2026, деградация MR). Проверки по тексту было НЕ
            // достаточно: причины в журнале ЧЕРЕДОВАЛИСЬ («в пустоте» ↔ «не горизонтальная
            // поверхность»), ключ менялся туда-обратно, и за прогон набежало 40 строк. Теперь
            // смена причины печатается не чаще раза в `MissReportIntervalSeconds` — первая строка
            // идёт сразу (счётчик от нуля), а дальше журнал держит ритм.
            if (Time.unscaledTime < nextMissReport) return;
            nextMissReport = Time.unscaledTime + MissReportIntervalSeconds;

            lastMissReason = reason;
            Debug.LogWarning(LogPrefix + "ВЫБОР МЕСТА РОБОТА: робот НЕ переставлен — " + reason +
                             (HasLastValidPoint ? " · последняя валидная позиция сохранена: " +
                                                  Num(LastValidPoint) + " м" : " · валидной позиции ещё не было"));
        }

        /// <summary>Интервал строки «робот НЕ переставлен», секунды (5) — см. `ReportMiss`.</summary>
        public const float MissReportIntervalSeconds = 5f;

        private float nextMissReport;

        /// <summary>СЛОВАМИ, ПОЧЕМУ ПОПАДАНИЯ НЕТ (или почему поверхность не годится).</summary>
        private string DescribeNoHit()
        {
            if (!currentHit.Any)
                return "правый луч не попал ни в реальную поверхность, ни в объект сцены";
            if (!IsSurfaceSuitable(currentHit))
                return "под правым лучом не горизонтальная поверхность (стена или потолок) — робота на неё не ставят";
            return "поверхность под правым лучом не годится";
        }

        private void RememberAnswer(string answer)
        {
            KvActionLog.Instance.Info(LogPrefix + "подсказка: выберите место робота ПРАВЫМ лучом, " +
                                      "зафиксируйте клавишей " + ConfirmKeyName + ", затем «переместить робота» · " +
                                      answer);
        }

        private static string Num(Vector3 point)
        {
            var c = CultureInfo.InvariantCulture;
            return "(" + point.x.ToString("0.00", c) + "; " +
                        point.y.ToString("0.00", c) + "; " +
                        point.z.ToString("0.00", c) + ")";
        }

        // ---------------------------------------------------------------- чистые строки

        /// <summary>
        /// СТРОКА О ВКЛЮЧЕНИИ РЕЖИМА — ЧИСТАЯ ФУНКЦИЯ (проверяется тестом без шлема).
        ///
        /// ФОРМА (требование владельца, дословно):
        ///     [KvMr] ВЫБОР МЕСТА РОБОТА: режим включён после выхода из меню
        /// Хвост с подсказкой дописывается отдельно (`HintTail`), чтобы проверяемая часть строки
        /// оставалась ровно той, которую ищет приёмка.
        /// </summary>
        public static string DescribeModeOn(string cause)
        {
            return LogPrefix + "ВЫБОР МЕСТА РОБОТА: режим включён " +
                   (string.IsNullOrEmpty(cause) ? "после выхода из меню" : cause);
        }

        /// <summary>ПОДСКАЗКА К РЕЖИМУ: какой луч, куда ставить и чем фиксировать (для журнала и экрана).</summary>
        public static string HintTail()
        {
            return " · робот идёт за ПРАВЫМ (зелёным) лучом · фиксация места — клавиша " + ConfirmKeyName +
                   " · затем «переместить робота»";
        }

        /// <summary>
        /// ХВОСТ СТРОКИ ВКЛЮЧЕНИЯ РЕЖИМА — РОВНО ТОТ, ЧТО ТРЕБУЕТ ВЛАДЕЛЕЦ (13.11.2026).
        ///
        /// ТРЕБОВАНИЕ ДОСЛОВНО:
        ///     [KvMr] ВЫБОР МЕСТА РОБОТА: режим включён после выхода из меню · робот идёт за ПРАВЫМ
        ///     (зелёным) лучом · фиксация — клавиша B
        ///
        /// ЧТО БЫЛО. Хвост режима заканчивался словами «· затем «переместить робота»», а в требовании
        /// этой части НЕТ: приёмка ищет строку, которая заканчивается на «фиксация — клавиша B».
        /// ЧЕМ ПЛОХО. Строка формально была, а искали её другой — «в журнале нет того, что просили».
        /// ЧТО СТАЛО. Требуемая строка собирается отдельной чистой функцией и печатается первой;
        /// подсказка про «переместить робота» печатается ВТОРОЙ строкой (`HintTail`), поэтому обе
        /// формулировки есть, и ни одна не портит проверяемую форму другой.
        /// </summary>
        public static string ModeOnLogTail()
        {
            return " · робот идёт за ПРАВЫМ (зелёным) лучом · фиксация — клавиша " + ConfirmKeyName;
        }

        /// <summary>
        /// СТРОКА О ФИКСАЦИИ — ЧИСТАЯ ФУНКЦИЯ (проверяется регуляркой на числа).
        ///
        /// ТРЕБОВАНИЕ ВЛАДЕЛЬЦА 13.11.2026:
        ///     [KvMr] МЕСТО РОБОТА ЗАФИКСИРОВАНО: (0.42; 0.78; 1.05) м · поворот 30° · подтверждено B ·
        ///     авто-постановка отключена
        ///
        /// ЧТО БЫЛО. В строке стояло «м · луч правый · поворот 30° · подтверждено B»: не было слов
        /// «авто-постановка отключена», хотя это главный факт фиксации — именно он отвечает на жалобу
        /// «робот уезжает после подтверждения». ЧЕМ ПЛОХО: приёмка, которая ищет строку по форме из
        /// требования, её не находила.
        ///
        /// ЧТО СТАЛО И ПОЧЕМУ СЛОВА «луч правый» ВЕРНУЛИСЬ (решение владельца 13.11.2026, дословно:
        /// «формулировка "правый луч — зелёный, а левый — красный, функционал как у ПК версии
        /// (кратко: правый выбирает траекторию, левый — точку на плоскости)"»). В MR ДВА луча, и они
        /// делают РАЗНОЕ. Если строка фиксации не называет источник, оператор по журналу не может
        /// понять, чем подтверждали: правым (выбор места робота) или левым (точка на плоскости).
        /// Поэтому источник назван ЯВНО и стоит сразу после координат — до поворота и клавиши, чтобы
        /// читалось как «где · чем · куда · чем подтвердили · что стало с автопостановкой».
        ///
        /// ПОЧЕМУ ПРАВЫЙ ЛУЧ ЖЁЛТЫМ НЕ НАЗВАН: фиксация места робота идёт ТОЛЬКО правым лучом (это
        /// закреплено `KvMrRobotPick.ConfirmKeyName` в подсказке и `KvXrRayHitRules`), левый в этом
        /// режиме место робота не выбирает. Слово «правый» здесь — не украшение, а проверяемый факт.
        /// </summary>
        public static string DescribeFixed(Vector3 point, float yawDegrees, string keyName)
        {
            var c = CultureInfo.InvariantCulture;
            return LogPrefix + "МЕСТО РОБОТА ЗАФИКСИРОВАНО: (" +
                   point.x.ToString("0.00", c) + "; " +
                   point.y.ToString("0.00", c) + "; " +
                   point.z.ToString("0.00", c) + ") м · " +
                   RaySourceName() + " · поворот " +
                   yawDegrees.ToString("0", c) + "° · подтверждено " +
                   (string.IsNullOrEmpty(keyName) ? ConfirmKeyName : keyName) +
                   AutoPlaceOffTail();
        }

        /// <summary>
        /// КАКИМ ЛУЧОМ ФИКСИРУЕТСЯ МЕСТО РОБОТА — ЧИСТАЯ ФУНКЦИЯ.
        ///
        /// ОТДЕЛЬНОЙ ФУНКЦИЕЙ, А НЕ СЛОВОМ В СТРОКЕ: «правый» — это правило выбора, а не оформление.
        /// В проекте два луча (правый — место/траектория робота, левый — точка на плоскости), и если
        /// когда-нибудь фиксацию переведут на левый, менять придётся ОДНО место, а тест сразу назовёт
        /// расхождение между журналом и подсказкой.
        /// </summary>
        public static string RaySourceName()
        {
            return "луч правый";
        }

        /// <summary>ХВОСТ «АВТО-ПОСТАНОВКА ОТКЛЮЧЕНА» — отдельно, чтобы его нельзя было потерять.</summary>
        public static string AutoPlaceOffTail()
        {
            return " · авто-постановка отключена";
        }
    }
}
