using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KazistovVvUI;

namespace KazistovVvXr
{


/// <summary>
/// ПАНЕЛЬ С ОБОБЩАЮЩИМИ ИКОНКАМИ ГРУПП У РУКИ (сессия §35, карточка Б19).
///    /
// РЕШЕНИЕ ВЛАДЕЛЬЦА (16.10.2026), дословно:
///   • «Панель инструментов должна быть у руки (у контроллера), а не висеть в середине экрана»;

///   • «Основные кнопки — ОБОБЩАЮЩИЕ ИКОНКИ групп (не текст)»;

///   • «Текст появляется только после наведения и удержания фокуса 0,5 секунды»;

///   • «После нажатия на кнопку группы — полумесяцем раскрываются ВСЕ иконки этой группы,
///     без названий, одними иконками».
///    /
// ЧТО ЗАМЕНЕНО. Прежняя панель (`KvXrQuickPanel`) показывала 8 ПЛОСКИХ кнопок с постоянными
/// ПОДПИСЯМИ, а когда позы руки не было — вставала ПЕРЕД ГЛАЗАМИ на 1,15 м. Владелец запретил
/// и то, и другое: подписи превращали панель в текст, а показ перед глазами — это ровно то
/// «висит в середине экрана», от которого он уходит.
///    /
// ОТКУДА ГРУППЫ. Из каталога проекта `KvToolbarGroups` — там уже есть 8 групп со своими
/// иконками и списками команд. Поэтому кнопка в шлеме делает РОВНО то же, что пункт меню
/// (`KvCommands.Invoke`): второй реализации действий не появляется, и разойтись им негде.
///    /
// ГДЕ ЛОГИКА, А ГДЕ РИСОВАНИЕ. Вся раскладка — углы, радиусы, попадание лучом, правило
/// показа подписи — живёт в `KvXrGroupPanelLayout` и проверена тестами без шлема. Здесь только
/// построение объектов, слежение за рукой и вызов команд, то есть то, что иначе как запуском
/// не проверить.
///    /
// ПОЧЕМУ ПАНЕЛЬ У ПОЯСА, А НЕ ПЕРЕД ГЛАЗАМИ, КОГДА РУКИ НЕ ВИДНО. Показ перед глазами
/// запрещён владельцем прямо, но «панели нет вовсе» тоже не годится: по журналу шлема
/// контроллеры могут не отдаваться приложению, и тогда панель не нашлась бы никогда.
/// У пояса она привязана к телу, всегда на одном месте и находится движением глаз вниз.
/// </summary>    [DisallowMultipleComponent]
public class KvXrGroupPanel : MonoBehaviour    {


/// <summary>Камера шлема: к ней разворачивается панель, по ней считается пояс.</summary>
public Camera headCamera;

/// <summary>Порядок отрисовки: выше панели функций (45), ниже HUD режима точки (60).</summary>
public int sortingOrder = 55;

/// <summary>
/// ЛЕГАСИ-ПОЛЯ ПРЕЖНЕГО ПОЛОЖЕНИЯ У РУКИ (сессия §35): «вынос от кисти к лицу» и
/// «подъём над кистью». В основном положении больше НЕ ИСПОЛЬЗУЮТСЯ — вместо них
/// <see cref="handForwardMeters"/> и <see cref="handUpMeters"/>, потому что вынос «к лицу»
/// ставил панель вплотную к глазам, а луч из того же контроллера шёл мимо неё.
/// Оставлены, чтобы сцена не потеряла значения и было видно, что именно заменено.
/// </summary>
public float forwardOffset = 0.14f;

/// <inheritdoc cref="forwardOffset"/>
public float upOffset = 0.06f;

/// <summary>
/// ВЫСОТА ПАНЕЛИ НАД ПОЛОМ в запасном положении, метры. Панель ставится ОТ ПОЛА, а не
/// «от головы минус рост до пояса»: по журналу 16.10.2026 такой расчёт увёл её на −0,85 м,
/// то есть ПОД ПОЛ, и владелец сказал «Пояса нет». 1,05 м — примерно пояс: видно,
/// опустив глаза, и вне центрального поля зрения.
/// </summary>
public float beltHeightMeters = 1.05f;

/// <summary>Размер канваса панели в пикселях (квадрат).</summary>
public float canvasSize = 420f;

/// <summary>Сколько метров приходится на одну единицу канваса.</summary>
public float metersPerCanvasUnit = 0.0025f;

/// <summary>
/// НАСКОЛЬКО ПАНЕЛЬ ВЫНОСИТСЯ ВПЕРЁД ОТ КОНТРОЛЛЕРА, метры (основное положение — У РУКИ).
/// 
/// ЧИСЛО НЕ «НА ГЛАЗ»: панель уровня групп — это дуга радиусом ≈0,15 м, то есть иконки
/// расходятся на ±0,18 м. На 0,30 м от контроллера это ±31° от направления указания —
/// наводиться wrist-ом удобно, а панель не липнет к лицу. Обновлённые `forwardOffset`
/// (0,14 м) для этого малы: иконка ушла бы почти на 50° в сторону.
/// </summary>
public float handForwardMeters = DefaultHandForwardMeters;

/// <summary>Насколько панель приподнята над лучом контроллера, метры (чтобы не резать кисть).</summary>
public float handUpMeters = DefaultHandUpMeters;

/// <summary>Вынос панели вперёд от контроллера по умолчанию, метры (§37 — тоже значение файла подстройки).</summary>
/// 
/// БЫЛО 0,30 — СТАЛО 0,12 (21.10.2026, слова владельца: «сделай ближе на 30 см панельку на
/// левой руке»). Панель висела далеко от руки, и до неё приходилось тянуться лучом.
public const float DefaultHandForwardMeters = 0.12f;

/// <summary>Подъём панели над лучом контроллера по умолчанию, метры.</summary>
public const float DefaultHandUpMeters = 0.04f;

/// <summary>
/// БЛИЖАЙШАЯ ДОПУСТИМАЯ ДИСТАНЦИЯ ОТ ГЛАЗ ДЛЯ ПАНЕЛИ У РУКИ, метры.
/// 
/// БЫЛО 0,35 — СТАЛО 0,18 (21.10.2026). Прежнее значение было защитой «панель в лицо», но
/// именно оно и держало панель далеко: в журнале стояло `от глаз 0.35 м` — то есть кламп
/// срабатывал и отодвигал панель, сколько бы её ни приближали настройкой выноса.
/// 0,18 м — это уже «на руке», но ещё читаемо (иконка 30 мм занимает ≈9,5° поля зрения).
/// </summary>
public float handMinDistanceFromEyes = DefaultHandMinDistanceFromEyes;

/// <summary>Значение по умолчанию для ближайшей дистанции от глаз, метры.</summary>
public const float DefaultHandMinDistanceFromEyes = 0.18f;

/// <summary>
/// ДАЛЬШЕ ЭТОГО ПОЗА РУКИ СЧИТАЕТСЯ НЕДОСТОВЕРНОЙ, метры. Известная ловушка проекта
/// ([F2]): поза устройства может прийти «нулевой», но формально достоверной. Панель,
/// поставленная по такой позе, оказалась бы на полу в начале координат — и это выглядело
/// бы как «панель сломалась». Поэтому такая поза отвергается с честной строкой журнала,
/// а панель встаёт в запасное положение у пояса.
/// </summary>
public float handMaxDistanceFromEyes = 1.20f;

/// <summary>Сообщать состояние панели в журнал при построении.</summary>
public bool logOnBuild = true;

/// <summary>
/// СЛЕДОВАТЬ ЗА РУКОЙ (по умолчанию — да: это панель у левой руки). У СТАРТОВОГО МЕНЮ (§41)
/// значение ЛОЖЬ: меню стоит перед оператором на своём месте и не должно ездить за кистью,
/// иначе это снова «панель», а не меню.
/// </summary>
public bool followHand = true;

/// <summary>
/// СКОЛЬКО СЕКУНД ПАНЕЛЬ СТОИТ ПОСЛЕ УХОДА ЛУЧА, не переставляясь. Даёт закончить
/// движение к иконке и не потерять её из-за одного кадра без попадания.
/// </summary>
public float holdAfterHoverSeconds = 0.5f;

/// <summary>
/// ПОРОГ УХОДА ВЗГЛЯДА, градусы: панель переставляется, только если оператор отвернулся
/// от неё дальше этого угла. У пояса панель и так на 29–33° ниже взгляда — это не уход.
/// </summary>
public float reanchorAngleDegrees = 55f;
private Canvas canvas;
private RectTransform canvasRect;
private readonly List<GameObject> icons = new List<GameObject>();
private readonly List<KvXrIconHover> hovers = new List<KvXrIconHover>();
private readonly List<UnityEngine.Events.UnityAction> iconActions = new List<UnityEngine.Events.UnityAction>();
private readonly List<string> iconTitles = new List<string>();
private Vector2[] currentOffsets = new Vector2[0];
private Text label;
private KvXrPanelLevel level = KvXrPanelLevel.Groups;
private int openGroupIndex = -1;
private float labelDwell;
private KvXrIconHover hovered;
private bool builtOnce;
private bool labelLogged;

/// <summary>Панель уже строилась хотя бы раз — для диагностики и проверок.</summary>
public bool BuiltOnce {
 get {
 return builtOnce; } }
private bool beltLogged;
private bool handLogged;
private bool poseRejectedLogged;
private bool placed;
private float holdUntil;

/// <summary>Сколько иконок построено на текущем уровне (для журнала и проверок).</summary>
public int IconCount {
 get {
 return icons.Count; } }

/// <summary>
/// СКОЛЬКО ЦЕЛЕЙ У ЛУЧА на текущем уровне. Обязано совпадать с <see cref="IconCount"/>:
/// смещение каждой интерактивной иконки попадает в список целей в момент её создания
/// (`CreateIcon` → `pendingOffsets` → `Rebuild`). Расхождение означало бы, что часть кнопок
/// луч не видит — именно так 24.10.2026 вышло с рядом экранов внизу панели, к которому
/// «не приклеивался лазер».
/// </summary>
public int RayTargetCount {
 get {
 return currentOffsets != null ? currentOffsets.Length : 0; } }

/// <summary>
/// НОМЕР ПЕРЕСБОРКИ ПАНЕЛИ (карточка Б51). Увеличивается в `Rebuild()`, когда меняется уровень
/// («группы» → экран → веер): с этого момента НОМЕРА ИКОНОК ОЗНАЧАЮТ ДРУГОЕ.
/// 
/// ЗАЧЕМ. Указатель «прощает дрожание» и до 0,5 с держит номер прошлой иконки. После смены
/// уровня этот номер указывает на чужую кнопку (или ни на какую), и в журнал уходила строка
/// `ПАНЕЛЬ: луч навёл «» — 0.0 см`, а телепорт до 0,5 с оставался запрещён. Указатель
/// сравнивает этот номер со своим и при расхождении сбрасывает наведение — забыть сброс
/// нельзя, потому что решение принимает факт пересборки, а не вызывающий код.
/// </summary>
public int RebuildVersion {
 get; private set; }

/// <summary>Смещение цели луча на канвасе, метры (для проверок: какая кнопка под лучом).</summary>
public Vector2 RayTargetOffset(int index)
{
            if (currentOffsets == null || index < 0 || index >= currentOffsets.Length) return Vector2.zero;
return currentOffsets[index];
}

/// <summary>
/// ПОЛОВИНА РАЗМЕРА ЦЕЛИ, метры (§41): по ней считается попадание луча. У иконки 30 мм это
/// (0,015; 0,015), у строки во всю панель — (0,18; 0,025). Нужна тестам и отладке.
/// </summary>
public Vector2 RayTargetHalfSize(int index)
{
            if (currentHalfSizes == null || index < 0 || index >= currentHalfSizes.Length) return Vector2.zero;
return currentHalfSizes[index];
}

/// <summary>Текущий уровень панели.</summary>
public KvXrPanelLevel Level {
 get {
 return level; } }

/// <summary>Какая группа раскрыта веером (−1 — ни одна).</summary>
public int OpenGroupIndex {
 get {
 return openGroupIndex; } }

/// <summary>Строка состояния панели для журнала.</summary>
public string Status {
 get; private set; }

/// <summary>Действующий радиус панели групп, метры (настройка → файл подстройки).</summary>
public float RadiusMeters {
 get; private set; }
private float builtRadius = -1f;

/// <summary>
/// РАЗМЕР ИКОНКИ, С КОТОРЫМ ПАНЕЛЬ ПОСТРОЕНА, метры (§37). Иконка уменьшается вместе с
/// радиусом, иначе на малом радиусе они налезали бы друг на друга — владелец просил ровно
/// обратного: «иконки можно уменьшить вместе с ним — важно, чтобы они не сливались».
/// </summary>
private float builtIconSize = KvXrGroupPanelLayout.IconSizeMeters;

/// <summary>Откуда взято положение панели: у контроллера или у пояса.</summary>
public bool PlacedAtHand {
 get; private set; }

/// <summary>
/// ЖИВАЯ ПАНЕЛЬ (последняя созданная). Нужна локомоции: она обязана знать, не забрал ли
/// ввод стик (§41), и не искать панель каждую секунду через `FindAnyObjectByType`.
/// </summary>
public static KvXrGroupPanel Active {
 get; private set; }        void Awake()
{
            Active = this;
            KvXrPanels.Register(this);
}        void OnDestroy()
{
            if (Active == this) Active = null;
            KvXrPanels.Unregister(this);
}        void Start()
{
            if (headCamera == null) headCamera = GetComponentInParent<Camera>();
            Rebuild(true);
}        void Update()
{
            if (canvas == null) return;

// §37: РАДИУС МОГ ИЗМЕНИТЬСЯ НА ХОДУ. Это и есть механизм опросов владельца: значение
// приходит из файла подстройки (или из настроек), панель перестраивается за один кадр, и
// в журнал уходит строка с новыми числами. Пересборки APK для этого не нужно.
float radius = KvXrTuning.PanelRadiusMeters;
if (builtOnce && Mathf.Abs(radius - builtRadius) > 0.0005f)
{
                Rebuild();
placed = false;

// панель встанет заново уже с новым радиусом
Debug.Log("[KvXr] панель групп: радиус изменён на " +                          radius.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) +                          " м — панель перестроена · " +                          KvXrGroupPanelLayout.DescribeSizes(icons.Count,                              level == KvXrPanelLevel.Groups                                  ? KvXrGroupPanelLayout.GroupSpanDegrees                                  : KvXrGroupPanelLayout.FanSpanDegrees, radius));
}
bool visible = KvXrSettings.GetQuickPanelVisible() && KvXrInputSource.HeadsetPresent;
if (followHand)
{
                if (canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
if (!visible) return;
}            UpdateWheelFromStick();
UpdateLabelDwell();

// ПАНЕЛЬ ЖЁСТКО НА ЛЕВОМ КОНТРОЛЛЕРЕ (требование владельца 21.10.2026: «осталось
// закрепить меню на левом контроллере»). Пока панель стоит у руки, она переставляется
// КАЖДЫЙ КАДР вместе с контроллером — это и есть «жёстко на руке».
//            // ИСПРАВЛЕНО 23.10.2026. Владелец: «иконки перестали быть прикреплены к левому
// контроллеру». Причина: панель один раз встала У ПОЯСА (в тот момент поза головы пришла
// нулевой, см. `KvXrInputSource.MinPlausibleHeadHeight`), а `ShouldHoldStill()` возвращало
// «держать на месте», потому что панель у пояса попадала в поле зрения. В итоге панель
// оставалась у пояса НАВСЕГДА. Теперь правило простое: есть поза руки — панель идёт к руке,
// независимо от того, что было раньше.
bool handPoseAvailable = KvXrSettings.GetQuickPanelHand() == KvXrQuickHand.Right                ? KvXrInputSource.TryRightAimPose(out _, out _)                : KvXrInputSource.TryLeftAimPose(out _, out _);
if (followHand && (PlacedAtHand || handPoseAvailable || !ShouldHoldStill())) PlacePanel();
            LogOrientationProbe();
}

/// <summary>
/// РАЗБОР ЖАЛОБЫ «КАМЕРА ПЛОХО ПОВОРАЧИВАЕТСЯ И ВСЕГДА КРУТИТСЯ» (16.10.2026).
/// Владелец в шлеме. Проверено, что дело НЕ в частоте кадров: метрики устройства
/// показывают ровные 72/72 кадра в секунду. Значит либо что-то ВРАЩАЕТ риг, либо
/// оператор видит вращение самой панели.
/// 
/// Здесь несколько секунд подряд печатаются УГЛЫ: курс шлема, курс корня рига и угол
/// панели. Если курс рига сам по себе ползёт — вращает код; если стоит, а «крутится»
/// картинка — дело в чём-то другом, и это будет видно по числам, а не по догадке.
/// Строк немного (ограничено), спама в журнале не будет.
/// </summary>
private void LogOrientationProbe()
{
            if (orientationProbeLeft <= 0) return;
if (Time.unscaledTime < nextOrientationProbe) return;
            nextOrientationProbe = Time.unscaledTime + 1.5f;
            orientationProbeLeft--;
float headYaw = headCamera != null ? headCamera.transform.eulerAngles.y : -1f;
float rigYaw = -1f;
if (headCamera != null)
{
                Transform root = headCamera.transform.root;
if (root != null) rigYaw = root.eulerAngles.y;
}
float panelYaw = transform.eulerAngles.y;
int frames = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime));
Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,                "[KvXr] проба ориентации: курс шлема {0:0.0}° · курс корня рига {1:0.0}° · курс панели {2:0.0}° · " +                "кадров/с ≈ {3} · панель в {4} · от глаз {5:0.00} м",                headYaw, rigYaw, panelYaw, frames, transform.position.ToString("0.00"),                headCamera != null ? Vector3.Distance(transform.position, headCamera.transform.position) : -1f));
}
private int orientationProbeLeft = 8;
private float nextOrientationProbe;

// ==================================================== ПОСТРОЕНИЕ
/// <summary>Собрать панель заново по текущему уровню (идемпотентно).</summary>
/// <summary>
/// ПЕРЕСОБРАТЬ ПАНЕЛЬ. `firstTime = true` — полная пересборка (§42): холст создаётся заново.
/// 
/// ЗАЧЕМ ЭТО НУЖНО. Уровень экрана держит бюджет примерно в семь иконок (панель у руки радиусом
/// ~0,11 м). Экран-окно компьютера — это ряд вкладок, строки и кнопки, и в один проход бюджет
/// не влезает: часть кнопок просто не появляется. Полная пересборка (новый холст + новый
/// уровень) даёт полный набор и ровный список целей луча. Поведение прежних экранов не меняется:
/// они вызывают `Rebuild()` без параметра, как и раньше.
/// </summary>
public void Rebuild(bool firstTime = false)
{
            if (canvas == null || firstTime) CreateCanvas();
if (canvas == null) return;

// НОМЕР ПЕРЕСБОРКИ — ДО построения (карточка Б51): с этого мгновения прежние номера
// иконок недействительны, и указатель обязан это увидеть, даже если смотрит на панель
// посреди кадра.
RebuildVersion++;
            ClearIcons();
if (level == KvXrPanelLevel.Groups) BuildGroupLevel();
            else if (IsDesktopWindowLevel(level))
{
                // §42: ОКНО КОМПЬЮТЕРА. Базовый уровень панели нужен и окну — размер иконки и подпись
// (общий подготовительный код), — но рисует он ОКНО, а не экран панели у руки.
PrepareScreenLevel();
BuildWindowPlate();
BuildDesktopWindowScreen(WindowScreen);
}            else if (IsScreen(level)) BuildScreenLevel();
            else BuildFanLevel();

// ЦЕЛИ ЛУЧА — ИЗ ОДНОГО МЕСТА (исправлено 24.10.2026).
//            // Владелец: «не приклеивается лазер к новым кнопкам внизу контроллера, что выстроены в
// ряд — информация, яркость и т.д.». Причина: список целей (`currentOffsets`) заполнялся
// вручную в каждом построителе уровня, и его ЗАБЫЛИ расширить при добавлении ряда экранов
// и кнопок внутри экранов. Луч физически не мог навести ни одну из них.
//            // Теперь смещение КАЖДОЙ интерактивной иконки попадает в список в момент её создания
// (`CreateIcon` → `pendingOffsets`), а здесь список берётся целиком. Забыть отдельную
// кнопку больше нельзя: нет иконки без цели, потому что это один и тот же вызов.
currentOffsets = pendingOffsets.ToArray();
            currentHalfSizes = pendingHalfSizes.ToArray();

// ГРОМКАЯ ОШИБКА, ЕСЛИ СПИСКИ РАСХОДЯТСЯ. Молчаливое расхождение — это ровно то, из-за
// чего владелец час искал «не приклеивается лазер»: кнопки есть, а луч их не видит.
if (currentOffsets.Length != icons.Count)
Debug.LogError("[KvXr] ЦЕЛИ ЛУЧА РАСХОДЯТСЯ С ИКОНКАМИ: целей " + currentOffsets.Length +                               " · иконок " + icons.Count + " · уровень " + level +                               " — часть кнопок луч не увидит");

// ТО ЖЕ ДЛЯ РАЗМЕРОВ ЦЕЛЕЙ (дефект Д1): разошлись длины — попадание молча вернётся
// к кругу в центре, и широкие кнопки снова «не будут нажиматься».
if (currentHalfSizes.Length != currentOffsets.Length)
Debug.LogError("[KvXr] РАЗМЕРЫ ЦЕЛЕЙ РАСХОДЯТСЯ С ЦЕЛЯМИ: размеров " + currentHalfSizes.Length +                               " · целей " + currentOffsets.Length + " · уровень " + level +                               " — попадание по площади кнопки работать не будет");
            builtOnce = true;

// ВЕРХНЯЯ НАДПИСЬ — ПОД ФАКТИЧЕСКИЙ ВЕРХНИЙ РЯД (указание владельца 24.10.2026:
// «верхняя панель должна быть ниже — упираться в верхние кнопки, а не заезжать на них»).
PlacePanelLabel();
if (logOnBuild || firstTime) Debug.Log("[KvXr] " + Status);
}

/// <summary>Уровень 0: по одной обобщающей иконке на каждую группу проекта.</summary>
private void BuildGroupLevel()
{
            KvToolbarGroup[] groups = KvToolbarGroups.All;
            KvXrQuickHand hand = KvXrSettings.GetQuickPanelHand();

// РАДИУС — ИЗ НАСТРОЙКИ (§37, опрос владельца: 0.100 м). До §37 здесь стоял расчёт от
// константы, и уменьшить панель было нечем: жалоба владельца «полукруг занимает много
// места перед лицом».
float radius = KvXrTuning.PanelRadiusMeters;
            RadiusMeters = radius;
            builtRadius = radius;
builtIconSize = KvXrGroupPanelLayout.IconSizeForCount(groups.Length,                KvXrGroupPanelLayout.GroupSpanDegrees, radius);
            Vector2[] offsets = KvXrGroupPanelLayout.GroupOffsetsForRadius(groups.Length, hand, radius);
int built = 0;
for (int i = 0; i < groups.Length && i < offsets.Length; i++)
{
                KvToolbarGroup group = groups[i];
int index = i;
CreateIcon("Group_" + group.Id, group.Icon, group.Title, offsets[i],                    delegate { OpenGroup(index); });
                built++;
}

// СПИСОК ЦЕЛЕЙ ЗДЕСЬ НЕ ЗАПОЛНЯЕТСЯ (§24.10.2026): он собирается в `Rebuild()` из
// `pendingOffsets` по факту создания иконок — и для групп, и для ряда экранов ниже.
labelDwell = 0f;
hovered = null;
labelLogged = false;
Status = KvXrGroupPanelLayout.Describe(groups.Length, 0, "не раскрыт", hand) +                     " · построено иконок групп " + built +                     " · " + KvXrGroupPanelLayout.DescribeSizes(groups.Length,                         KvXrGroupPanelLayout.GroupSpanDegrees, radius);

// ЭКРАНЫ (§40): пять входов внизу панели — «СОСТОЯНИЕ», «НАСТРОЙКИ», «ОБЪЕКТЫ»,
// «СВОЙСТВА», «СПРАВКА». Они не трогают реестр команд, поэтому десктопный интерфейс не
// меняется вовсе.
AddScreenEntries();
}

/// <summary>Пять входов в экраны — отдельным рядом внизу панели, чтобы не занимать дугу групп.</summary>
private void AddScreenEntries()
{
            KvXrPanelLevel[] screens =            {
                KvXrPanelLevel.StatusScreen, KvXrPanelLevel.SettingsScreen, KvXrPanelLevel.ObjectsScreen,                KvXrPanelLevel.PropertiesScreen, KvXrPanelLevel.HelpScreen
};
            string[] icons = { "graph", "settings", "tree", "info", "more" };
            string[] titles = { "Состояние", "Настройки", "Объекты", "Свойства", "Справка" };
float spacing = 0.075f;
float startX = -0.15f;
float y = -0.235f;
for (int i = 0; i < screens.Length; i++)
{
                KvXrPanelLevel screen = screens[i];
CreateIcon("Screen_" + screen, icons[i % icons.Length], titles[i % titles.Length],                    new Vector2(startX + i * spacing, y), delegate { OpenWindow(screen); });
}
}

/// <summary>
/// ЭТО ОКНО В МИРЕ (а не панель у руки и не меню). У окна есть плита, за верх которой берут
/// указательным пальцем, и «крестик» внизу закрывает его целиком.
/// </summary>
public bool isWindow;

/// <summary>Полуразмер плиты окна, метры (для попадания и для переноса).</summary>
public static readonly Vector2 WindowPlateHalfSize = new Vector2(0.31f, 0.26f);

/// <summary>
/// ПЛИТА ОКНА: рисуется тот же скруглённый прямоугольник, что и у меню, и плита ГАСИТ ЛУЧ —
/// по ней же определяются «любая точка окна» (перенос средним пальцем) и «верх окна»
/// (перенос указательным).
/// </summary>
private void BuildWindowPlate()
{
            CreatePlate("WindowPlate", Vector2.zero,                new Vector2(WindowPlateHalfSize.x * 2f, WindowPlateHalfSize.y * 2f),                new Color(0.10f, 0.11f, 0.13f, 0.97f));
PanelSurfaceHalfSize = WindowPlateHalfSize;
}

/// <summary>
/// ЛУЧ ПОПАЛ В ВЕРХНЮЮ ПОЛОСУ ОКНА («заголовок»). За неё окно берут УКАЗАТЕЛЬНЫМ пальцем
/// (указание владельца 24.10.2026).
/// </summary>
public bool IsTitlePoint(Vector3 rayOrigin, Vector3 rayDirection)
{
            if (canvasRect == null) return false;
Vector2 pointMeters;
float alongMeters;
if (!KvXrGroupPanelLayout.TryRayToPanelPlane(rayOrigin, rayDirection,                    canvasRect.position, canvasRect.right, canvasRect.up, canvasRect.forward,                    out pointMeters, out alongMeters)) return false;
float top = PanelSurfaceHalfSize.y > 0f ? PanelSurfaceHalfSize.y : WindowPlateHalfSize.y;
float halfWidth = PanelSurfaceHalfSize.x > 0f ? PanelSurfaceHalfSize.x : WindowPlateHalfSize.x;

// ПОЛОСА ЗАХВАТА — 24 % высоты (было 30 %): указание владельца 24.10.2026 «сделай на 1/5
// часть меньше верхнюю панель, за которую можно переносить окна».
float band = Mathf.Max(0.05f, top * 0.24f);
return pointMeters.y >= top - band && Mathf.Abs(pointMeters.x) <= halfWidth;
}

/// <summary>
/// ОТКРЫТЬ ОКНО В МИРЕ ВМЕСТО СТРАНИЦЫ ПАНЕЛИ (указание владельца 24.10.2026): «кнопки не
/// выдают окна… они тоже не должны крепиться ко взгляду, если персонаж будет отходить — окна
/// останутся висеть в воздухе». Панель у руки при этом остаётся панелью с иконками групп.
/// 
/// Если хоста окон ещё нет (сцена без рига), работает прежний путь — экран на самой панели:
/// так поведение не пропадает совсем.
/// </summary>
public void OpenWindow(KvXrPanelLevel screen)
{
            KvXrWindowHost host = KvXrWindowHost.Active;
if (host == null)
{
                OpenScreen(screen);
                return;
}            host.Open(screen, ScreenTitle(screen));
}

/// <summary>
/// ID КОМАНДЫ КОМПЬЮТЕРА, КОТОРОЙ ОТКРЫТО ЭТО ОКНО (§42). Пусто у панели у руки и у меню;

/// у окна — «ui.settings», «joints.panel» и т. п. Нужен, чтобы дедупликация шла ПО КОМАНДЕ
/// (два разных окна одного экрана могут висеть рядом) и чтобы журнал называл команду.
/// </summary>
public string WindowCommandId {
 get; private set; }

/// <summary>Номер вкладки, открытой в окне (§42): у «Верстака» их 23.</summary>
public int WindowTab {
 get; private set; }

/// <summary>
/// ОТКРЫТЬ ОКНО КОМПЬЮТЕРА В МИРЕ (§42). Возвращает `false`, если команда окна не открывает
/// (тогда её надо выполнить как команду). Само открытие делает `KvXrWindowHost` с ключом
/// команды; второй механизм окон не заводится.
/// </summary>
/// <summary>Какой экран-окно сейчас показан (для журнала и проверок, §42).</summary>
public KvXrPanelLevel WindowScreen {
 get; private set; }

/// <summary>Какой экран-окно сейчас показан — для тестов (уровень при этом `DesktopWindow`).</summary>
public KvXrPanelLevel OpenedWindowScreen {
 get {
 return WindowScreen; } }
public bool OpenDesktopWindow(string commandId)
{
            KvXrDesktopWindowInfo info;
if (!KvXrDesktopWindows.TryGet(commandId, out info)) return false;
            WindowCommandId = commandId;
            WindowTab = info.Tab;
            WindowScreen = info.Screen;
            KvXrWindowHost host = KvXrWindowHost.Active;
if (host == null)
{
                // Сцены без рига (проверки и отладка): окно показывается на самой панели.
OpenScreen(info.Screen);
Debug.Log("[KvXr] ОКНО: открыто «" + info.Title + "» на панели (хоста окон нет) · по команде " + commandId);
return true;
}            KvXrGroupPanel window = host.Open(info.Screen, info.Title, commandId);
if (window != null && window != this)
{
                window.WindowCommandId = commandId;
                window.WindowTab = info.Tab;
                window.OpenDesktopTab(info.Tab);
}            return true;
}

/// <summary>ПЕРЕКЛЮЧИТЬ ВКЛАДКУ ОКНА (§42): ряд вкладок внутри окна, а не иконки панели.</summary>
public void OpenDesktopTab(int tabIndex)
{
int count = KvXrDesktopTabs.CountOf(WindowScreen);
if (count <= 0) return;
            WindowTab = Mathf.Clamp(tabIndex, 0, count - 1);
            Rebuild();
Debug.Log("[KvXr] ОКНО: вкладка «" + KvXrDesktopTabs.TabTitle(WindowScreen, WindowTab) +                      "» (номер " + (WindowTab + 1) + " из " + count + ") · страница " +                      (KvXrDesktopTabs.PageOf(WindowTab) + 1) + " из " +                      KvXrDesktopTabs.PageCount(count));
}

/// <summary>
/// ОТКРЫТЬ ЭКРАН (§40). Строка в журнале обязательна: по ней видно, что открылось.
/// 
/// §42: ЭКРАНЫ-ОКНА КОМПЬЮТЕРА — ЭТО ОКНА, А НЕ ЭКРАНЫ ПАНЕЛИ. Панель у руки рассчитана на
/// радиус ~0,11 м и строит около семи иконок; ряд вкладок окна в этот бюджет не влезает
/// физически. Поэтому экран-окно переводит панель на уровень `DesktopWindow` — там своя плита
/// 0,62 × 0,52 м и полный набор кнопок. Какой именно экран показан, помнит `WindowScreen`.
/// </summary>
public void OpenScreen(KvXrPanelLevel screen)
{
            openGroupIndex = -1;
if (IsDesktopWindowScreen(screen))
{
                WindowScreen = screen;
level = KvXrPanelLevel.DesktopWindow;
}            else            {
                level = screen;
}            Rebuild(true);
Debug.Log("[KvXr] ЭКРАН: открыт «" + ScreenTitle(screen) + "» · строк " + icons.Count +                      " · " + (KvXrMode.Description));
}

/// <summary>Экран-окно компьютера (§42)? — по таблице соответствия, а не по списку в коде.</summary>
public static bool IsDesktopWindowScreen(KvXrPanelLevel screen)
{
            return screen == KvXrPanelLevel.DesktopSettingsScreen                || screen == KvXrPanelLevel.FeaturesScreen                || screen == KvXrPanelLevel.WorkbenchScreen                || screen == KvXrPanelLevel.HotkeysScreen                || screen == KvXrPanelLevel.PendantScreen;
}

/// <summary>Уровень «окно компьютера» (§42).</summary>
public static bool IsDesktopWindowLevel(KvXrPanelLevel level)
{
            return level == KvXrPanelLevel.DesktopWindow;
}

/// <summary>Название экрана для журнала.</summary>
public static string ScreenTitle(KvXrPanelLevel screen)
{
            switch (screen)
{
case KvXrPanelLevel.StatusScreen: return "Состояние";
case KvXrPanelLevel.SettingsScreen: return "Настройки";
case KvXrPanelLevel.ObjectsScreen: return "Объекты";
case KvXrPanelLevel.PropertiesScreen: return "Свойства";
case KvXrPanelLevel.HelpScreen: return "Справка";

// §41: БЕЗ ЭТИХ ТРЁХ СТРОК ЖУРНАЛ ВРАЛ (дефект Д2). Открытие «Интерфейса компьютера»
// записывалось как «ЭКРАН: открыт «иконки групп»», и по журналу нельзя было понять,
// что вообще открылось.
case KvXrPanelLevel.InputScreen: return "Ввод";
case KvXrPanelLevel.SearchScreen: return "Поиск";
case KvXrPanelLevel.InterfaceScreen: return "Интерфейс компьютера";
case KvXrPanelLevel.StartMenu: return "Стартовое меню";

// §42: экраны-окна компьютера. Без этих строк журнал снова стал бы врать — ровно тем
// же дефектом Д2, что чинился в §41.
case KvXrPanelLevel.DesktopSettingsScreen: return "Настройки ПК";
case KvXrPanelLevel.FeaturesScreen: return "Панель функций";
case KvXrPanelLevel.WorkbenchScreen: return "Верстак";
case KvXrPanelLevel.HotkeysScreen: return "Горячие клавиши";
case KvXrPanelLevel.PendantScreen: return "Виртуальный пульт";

// Сам уровень «окно компьютера» подписи не имеет: подпись берётся у конкретного экрана
// (`WindowScreen`), иначе журнал снова стал бы врать, как в дефекте Д2.
case KvXrPanelLevel.DesktopWindow: return "окно компьютера";
default: return "иконки групп";
}
}

/// <summary>Закрыть экран и вернуться к иконкам групп.</summary>
public void CloseScreen()
{
            // «ЗАКРЫТЬ» НА ЭКРАНЕ ВВОДА — ЭТО ОТМЕНА (значение не меняется и это сказано в журнале),
// а не прыжок к иконкам групп: иначе владелец терял бы место, из которого пришёл.
if (level == KvXrPanelLevel.InputScreen)
{
                CancelInput();
                return;
}

// У ПАНЕЛИ МЕНЮ «ЗАКРЫТЬ» ЗАКРЫВАЕТ МЕНЮ ЦЕЛИКОМ (§41): превращать меню в иконки групп
// нельзя — это другой интерфейс, и владелец не поймёт, куда попал.
if (isWindow && KvXrWindowHost.Active != null)
{
                // КРЕСТИК ВНИЗУ ОКНА ЗАКРЫВАЕТ ОКНО (дефект приёмки 24.10.2026: «закрытие на крестик
// внизу не работает»). Раньше он переводил окно на уровень иконок групп.
KvXrWindowHost.Active.Close(this);
                return;
}            if (!followHand && KvXrStartMenu.Active != null)
{
                KvXrStartMenu.Active.Hide();
                return;
}            level = KvXrPanelLevel.Groups;
            Rebuild();
Debug.Log("[KvXr] ЭКРАН: закрыт — снова иконки групп, иконок " + IconsOnLevel);
}

/// <summary>Это экран (а не группы или веер).</summary>
public static bool IsScreen(KvXrPanelLevel value)
{
            return value >= KvXrPanelLevel.StatusScreen;
}

/// <summary>
/// УРОВЕНЬ 1: веер «полумесяцем» из ВСЕХ иконок выбранной группы, БЕЗ НАЗВАНИЙ.
/// В центре — иконка самой группы: нажатие на неё закрывает веер, чтобы из него
/// был выход, а не только «выбери что-нибудь».
/// </summary>
private void BuildFanLevel()
{
            KvToolbarGroup[] groups = KvToolbarGroups.All;
if (openGroupIndex < 0 || openGroupIndex >= groups.Length)
{
                level = KvXrPanelLevel.Groups;
BuildGroupLevel();
                return;
}            KvToolbarGroup group = groups[openGroupIndex];
            KvXrQuickHand hand = KvXrSettings.GetQuickPanelHand();

// Отбираются ТОЛЬКО реально существующие команды: список группы может отстать
// от реестра, и пустая иконка в веере выглядела бы как «дырка».
var resolved = new List<string>();
var skipped = new List<string>();
if (group.Commands != null)
{
                foreach (string id in group.Commands)
{
                    if (string.IsNullOrEmpty(id)) continue;
if (KvCommands.Get(id) == null) {
 skipped.Add(id); continue; }                    resolved.Add(id);
}
}            FanCommandCount = resolved.Count;

// РАДИУС ВЕЕРА СЧИТАЕТСЯ ОТ РАДИУСА ГРУПП, но не даёт иконкам стать мельче читаемых:
// 14 иконок «Визуализации» на 0.110 м дали бы 18 мм — это уже не попадается лучом.
float radius = KvXrTuning.PanelRadiusMeters;
float fanRadius = KvXrGroupPanelLayout.FanRadiusMeters(resolved.Count, radius);
            RadiusMeters = radius;
            builtRadius = radius;
builtIconSize = KvXrGroupPanelLayout.IconSizeForCount(resolved.Count,                KvXrGroupPanelLayout.FanSpanDegrees, fanRadius);

// В СПИСОК ПОПАДАНИЯ ВХОДИТ И ЦЕНТРАЛЬНАЯ ИКОНКА (исправлено 21.10.2026).
// Владелец: «не работает кнопка перехода на первый слой». Причина: центральная иконка
// («выход») рисуется в центре панели, но её смещения (0,0) НЕ было в списке попадания —
// луч физически не мог в неё попасть, и выйти из веера нажатием было невозможно.
// Раньше это маскировалось тем, что веер закрывался сам после любой команды.
Vector2[] hitOffsets = KvXrGroupPanelLayout.FanHitOffsets(resolved.Count, hand, radius);

// ПОСЛЕДНЕЕ СМЕЩЕНИЕ В СПИСКЕ — ЦЕНТР (0,0), это иконка «Закрыть», а не команда.
// Поэтому команды раскладываются строго до последнего элемента: иначе при группе
// длиннее MaxFanIcons одна команда встала бы в центр поверх «Закрыть».
for (int i = 0; i < resolved.Count && i < hitOffsets.Length - 1; i++)
{
string id = resolved[i];
                KvCommand command = KvCommands.Get(id);
string iconId = command != null && !string.IsNullOrEmpty(command.Icon) ? command.Icon : "info";
string title = command != null ? command.LocalizedTitle : id;
CreateIcon("Cmd_" + id, iconId, title, hitOffsets[i], delegate { InvokeCommand(id); });
}

// Центр веера — выход. Рисуется поверх, чтобы было видно, что он «главный».
CreateIcon("Fan_Close", group.Icon, "Закрыть «" + group.Title + "»", Vector2.zero,                delegate { CloseFan(); }, true);

// СПИСОК ЦЕЛЕЙ — ИЗ `Rebuild()` (`pendingOffsets`): там и команды веера, и центральная
// иконка «Закрыть», которой раньше не было в попадании (дефект 21.10.2026).
labelDwell = 0f;
hovered = null;
labelLogged = false;
Status = KvXrGroupPanelLayout.Describe(groups.Length, resolved.Count, group.Title, hand) +                     " · иконок в веере " + resolved.Count +                     " · " + KvXrGroupPanelLayout.DescribeSizes(resolved.Count,                         KvXrGroupPanelLayout.FanSpanDegrees, fanRadius) +                     " · радиус групп " + radius.ToString("0.000") + " м" +                     (skipped.Count > 0 ? " · не найдено в реестре команд: " + string.Join(", ", skipped.ToArray()) : "");
}

// ==================================================== ЭКРАНЫ (§40)
private int settingsIndex;
private int selectedObjectIndex = -1;
private readonly List<GameObject> rows = new List<GameObject>();

// ==================================================== ВВОД (§41)
/// <summary>Состояние ввода: строка-кандидат, её предел и поле, к которому она относится.</summary>
private readonly KvXrTextInput input = new KvXrTextInput();

/// <summary>Куда вернуться после «ввода»: в «Настройки», «Свойства» или «Поиск».</summary>
private KvXrPanelLevel returnLevel = KvXrPanelLevel.SettingsScreen;

/// <summary>В какое поле сейчас вводится число.</summary>
private KvXrValueFieldId pendingField = KvXrValueFieldId.PanelRadius;

/// <summary>Выбранный сектор колеса символов.</summary>
private int wheelIndex;

/// <summary>Строка поиска и найденные команды (порядок — из `KvCommandSearch`).</summary>
private string searchQuery = "";
private readonly List<KvCommand> searchResults = new List<KvCommand>();
private int searchPage;

/// <summary>Скрытая надпись-проба: контракт переименования на компьютере требует ярлык.</summary>
private Text renameProbe;

/// <summary>Строка-кандидат и её состояние (для указателя и для проверок).</summary>
public KvXrTextInput Input {
 get {
 return input; } }

/// <summary>Выбранный сектор колеса символов.</summary>
public int WheelIndex {
 get {
 return wheelIndex; } }

/// <summary>Сколько команд нашёл поиск.</summary>
public int SearchResultCount {
 get {
 return searchResults.Count; } }

/// <summary>Поле, в которое идёт ввод числа.</summary>
public KvXrValueFieldId PendingField {
 get {
 return pendingField; } }

/// <summary>
/// СТИК ЗАНЯТ ВВОДОМ (колесо символов). Локомоция обязана в это время молчать, иначе выбор
/// символа будет ещё и разворачивать оператора: стик в шлеме — это поворот шагами и ход.
/// </summary>
public bool StickCaptured        {
            get            {
                return level == KvXrPanelLevel.InputScreen &&                       KvXrWheelLayout.CapturesStick(true, KvXrInputMethodSetting.Get());
}
}

/// <summary>
/// ПОСТРОИТЬ ЭКРАН. Значения пересобираются при каждом входе и при изменении настройки;

/// отдельного обновления «каждый кадр» нет намеренно — пересборка создаёт объекты.
/// </summary>
/// <summary>
/// ОБЩАЯ ЧАСТЬ ЛЮБОГО УРОВНЯ-ЭКРАНА (§42): радиус, размер иконки, сброс ссылок ввода.
/// Вынесено из `BuildScreenLevel`, чтобы окно компьютера получало те же базовые величины,
/// но рисовало своё (у него своя плита 0,62 × 0,52 м, а не радиус панели 0,110 м).
/// </summary>
private void PrepareScreenLevel()
{
float radius = KvXrTuning.PanelRadiusMeters;
            RadiusMeters = radius;
            builtRadius = radius;
builtIconSize = KvXrGroupPanelLayout.IconSizeForCount(8,                KvXrGroupPanelLayout.GroupSpanDegrees, radius);

// ССЫЛКИ НА СТРОКИ ВВОДА СБРАСЫВАЮТСЯ КАЖДУЮ ПЕРЕСБОРКУ: они указывают на объекты,
// которые сейчас будут уничтожены вместе с прежним экраном.
inputCandidate = null;
            inputHint = null;
}
private void BuildScreenLevel()
{
            PrepareScreenLevel();

// ПЛИТА, ГАСЯЩАЯ ЛУЧ, — только у меню (§41). У остальных экранов её нет: там принятый
// вид панели у руки, и менять его поведение без просьбы нельзя.
PanelSurfaceHalfSize = Vector2.zero;

// ПЛИТА ОКНА (указание владельца 24.10.2026): у окна есть подложка, по ней считаются
// «любая точка окна» (перенос средним пальцем) и «верх окна» (перенос указательным).
//            // §42: У ОКНА КОМПЬЮТЕРА (уровень DesktopWindow) ПЛИТА НУЖНА ВСЕГДА — даже если объект
// создан без `isWindow` (так делают проверки в памяти).
if (isWindow) BuildWindowPlate();

// ОКНО КОМПЬЮТЕРА (§42) — ЗДЕСЬ ЖЕ, ПОСЛЕ РЕШЕНИЯ О ПЛИТЕ. Иначе кнопки вкладок некуда
// ставить: плиты нет, `PanelSurfaceHalfSize` нулевой, и луч не может попасть ни в одну
// (правило §K «иконка = цель» требует, чтобы у окна была своя поверхность).
// Второй проход по уровню окна после этого не нужен: ветка исчерпывающая, возврат внизу.
switch (level)
{
case KvXrPanelLevel.StatusScreen: BuildStatusScreen(); break;
case KvXrPanelLevel.SettingsScreen: BuildSettingsScreen(); break;
case KvXrPanelLevel.ObjectsScreen: BuildObjectsScreen(); break;
case KvXrPanelLevel.PropertiesScreen: BuildPropertiesScreen(); break;
case KvXrPanelLevel.InputScreen: BuildInputScreen(); break;
case KvXrPanelLevel.SearchScreen: BuildSearchScreen(); break;
case KvXrPanelLevel.InterfaceScreen: BuildInterfaceScreen(); break;
case KvXrPanelLevel.StartMenu: BuildStartMenuLevel(); break;

// §42: ЭКРАНЫ-ОКНА КОМПЬЮТЕРА строятся отдельной веткой Rebuild (уровень DesktopWindow),
// потому что это ОКНО, а не экран панели у руки.
default: BuildHelpScreen(); break;
}

// ВЫХОД — внизу по центру, всегда в одном месте.
CreateIcon("Screen_Close", "close", "Закрыть", new Vector2(0f, -0.29f),                delegate { CloseScreen(); }, true);
labelDwell = 0f;
hovered = null;
labelLogged = false;
Status = "экран «" + ScreenTitle(level) + "» · иконок " + icons.Count +                     " · " + KvXrGroupPanelLayout.DescribeSizes(8,                         KvXrGroupPanelLayout.GroupSpanDegrees, RadiusMeters);
}
private void BuildStatusScreen()
{
var data = new KvXrStatusData            {
                Mode = KvXrModeResolver.ModeName(KvXrMode.Current),
                MoveMode = KvXrSettings.MoveModeLabel(KvXrSettings.GetMoveModeIndex()),
                Fps = 1f / Mathf.Max(0.0001f, Time.smoothDeltaTime),
                HeadHeight = headCamera != null ? headCamera.transform.position.y : 0f,
                PanelRadius = RadiusMeters,
                IconSizeMm = builtIconSize * 1000f,
                CommandCount = KazistovVvUI.KvCommands.All.Count,
                SecondRay = KvXrTuning.SecondaryRayEnabled,
                PlanningCandidates = PlanningHasCandidates()
};
CreateLabel("ScreenTitle", "Состояние", new Vector2(0f, 0.30f), 18);
ShowRows(KvXrScreenModel.BuildStatus(data), true);
}
private static bool PlanningHasCandidates()
{
#pragma warning disable 0618 // TrajectoryPlannerController помечен [Obsolete] — мёртвый код (аудит §5.7)
            TrajectoryPlannerController planner =                UnityEngine.Object.FindAnyObjectByType<TrajectoryPlannerController>();
#pragma warning restore 0618
return planner != null && planner.HasCandidates;
}

/// <summary>
/// ЭКРАН «НАСТРОЙКИ»: ОДИН ПАРАМЕТР КРУПНО, с кнопками «−» и «+». Так сделано специально:
/// строка «Радиус панели: 0.110 м» вместе с двумя кнопками не помещается на панель и
/// слипается — в шлеме это нечитаемо. Один параметр на экран читается без напряжения.
/// </summary>
private void BuildSettingsScreen()
{
            KvXrSettingsData data = CurrentSettings();
            List<string> settings = KvXrScreenModel.BuildSettings(data);

// ПОРЯДОК ПАРАМЕТРОВ ЖЁСТКО ЗАДАН и совпадает с моделью `KvXrScreenModel.BuildSettings`:
// 0 — радиус панели, 1 — скорость, 2 — рост, 3 — дальность телепорта (у этих четырёх
// есть шаг «−/+»), 4 — второй луч, 5 — рука луча (у этих двух — переключатель).
//            // ИСПРАВЛЕНО 23.10.2026: раньше «переключатель» был привязан к ПОСЛЕДНЕМУ индексу, поэтому
// он оказывался у «Руки луча», а «Второй луч» уходил в ветку шага и менял дальность
// телепорта. Дефект беззвучный: строка журнала называла одно, менялось другое.
const int stepperCount = 4;
            const int totalCount = 6;
settingsIndex = Mathf.Clamp(settingsIndex, 0, totalCount - 1);
CreateLabel("ScreenTitle", "Настройки " + (settingsIndex + 1) + " из " + totalCount,                new Vector2(0f, 0.30f), 18);
string value = settingsIndex + 1 < settings.Count ? settings[settingsIndex + 1] : "—";
CreateLabel("ScreenValue", value, new Vector2(0f, 0.15f), 26);
if (settingsIndex >= stepperCount)
{
CreateIcon("Set_Toggle", "check", "Переключить", new Vector2(0f, -0.02f),                    delegate { ToggleCurrentSetting(); });
}            else            {
CreateIcon("Set_Minus", "minus", "Меньше", new Vector2(-0.18f, -0.02f),                    delegate { StepSetting(-1); });
CreateIcon("Set_Plus", "plus", "Больше", new Vector2(0.18f, -0.02f),                    delegate { StepSetting(+1); });

// ВВОД ВРУЧНУЮ (§41): «−/+» — это шаг, а нужен ещё и ввод значения строкой.
// Кнопка стоит между шагом и «Дальше», прежние кнопки не сдвинуты — их смещения
// закреплены уже запускавшимся тестом §40.
CreateIcon("Set_Enter", "keyboard", "Ввести значение", new Vector2(0f, -0.09f),                    delegate { OpenInput(KvXrValueFields.Get(settingsIndex).Id); }, false, "123", 0.10f, 0.05f);
}
CreateIcon("Set_Next", "next", "Следующий параметр", new Vector2(0f, -0.16f),                delegate { NextSetting(totalCount); });

// СПОСОБ ВВОДА (§41, решение владельца R5): переключается здесь, в шлеме — чтобы владелец
// сравнивал способы руками. Отдельной кнопкой, а не седьмою строкой списка: состав строк
// закреплён тестом §40 (см. `CycleInputMethod`).
CreateIcon("Set_Input", "keyboard",                "Способ ввода: " + KvXrInputMethodSetting.Label(KvXrInputMethodSetting.Get()),                new Vector2(-0.18f, -0.16f), delegate { CycleInputMethod(); }, false,                KvXrInputMethodSetting.Label(KvXrInputMethodSetting.Get()), 0.12f, 0.05f);

// ЧИСЛОВЫЕ НАСТРОЙКИ КОМПЬЮТЕРА (§41): на компьютере их шесть и они есть, значит и в
// шлеме путь к ним должен быть. Отдельный экран, чтобы не трогать закреплённый список.
CreateIcon("Set_Interface", "settings", "Настройки интерфейса компьютера",                new Vector2(0.18f, -0.16f), delegate { OpenScreen(KvXrPanelLevel.InterfaceScreen); });

// СТАРТОВОЕ МЕНЮ (§41, указание владельца): вызвать снова, если закрыл. Появляется само
// при запуске, но путь назад к нему должен быть — иначе «меню есть, а вернуться некуда».
CreateIcon("Set_StartMenu", "session", "Стартовое меню", new Vector2(0.18f, -0.09f),                delegate { ShowStartMenu(); }, false, "меню", 0.10f, 0.05f);
}

/// <summary>ПОКАЗАТЬ СТАРТОВОЕ МЕНЮ (кнопка на экране «Настройки» и первое открытие).</summary>
private void ShowStartMenu()
{
            if (KvXrStartMenu.Active != null)
{
                KvXrStartMenu.Active.Show();
                return;
}
Debug.Log("[KvXr] МЕНЮ: показать не удалось — стартовое меню не создано (нет компонента KvXrStartMenu)");
}

/// <summary>
/// ЭКРАН «ИНТЕРФЕЙС»: шесть числовых настроек КОМПЬЮТЕРА (`KvSettings`) строками. Нажатие
/// открывает ввод значения — тем же путём, что и у настроек шлема (одна точка сохранения).
/// </summary>
private void BuildInterfaceScreen()
{
            builtIconSize = KvXrGroupPanelLayout.IconSizeMeters;
CreateLabel("ScreenTitle", "Интерфейс компьютера", new Vector2(0f, 0.30f), 18);
CreateLabel("ScreenRange", "нажмите строку, чтобы ввести значение", new Vector2(0f, 0.24f), 13);
float spacing = 0.070f;
float top = 0.145f;
int drawn = 0;
for (int i = KvXrValueFields.Count - 6; i < KvXrValueFields.Count; i++, drawn++)
{
                KvXrValueFieldInfo field = KvXrValueFields.Get(i);
                KvXrValueFieldId id = field.Id;
string caption = field.Title + ": " +                                 KvXrValueFieldInfo.Format(field.Read(), field.Decimals);
CreateIcon("Interface_" + drawn, "settings", field.Title,                    new Vector2(0f, top - drawn * spacing),                    delegate { OpenInput(id); }, false, caption, 0.36f, 0.050f);
}
}

/// <summary>Переключатель для параметров без шага (второй луч, рука луча).</summary>
private void ToggleCurrentSetting()
{
            if (settingsIndex == 4) ToggleSecondaryRay();
            else if (settingsIndex == 5) ToggleRayHand();
}
private void ToggleRayHand()
{
            KvXrQuickHand now = KvXrSettings.GetRayHand();
            KvXrQuickHand next = now == KvXrQuickHand.Right ? KvXrQuickHand.Left : KvXrQuickHand.Right;
            KvXrSettings.SetRayHand(next);
            KvXrSettingsChanged.Notify();
            Rebuild();
Debug.Log("[KvXr] НАСТРОЙКА: рука луча — " + (next == KvXrQuickHand.Right ? "правая" : "левая"));
}
private KvXrSettingsData CurrentSettings()
{
            return new KvXrSettingsData            {
                PanelRadius = KvXrSettings.GetGroupPanelRadius(),
                SmoothSpeed = KvXrSettings.GetSmoothSpeed(),
                OperatorHeight = KvXrSettings.GetOperatorHeight(),
                TeleportDistance = KvXrSettings.GetTeleportDistance(),
                SecondRay = KvXrSettings.GetSecondaryRay(),
                RayHand = KvXrSettings.GetRayHand()
};
}
private void NextSetting(int parameterCount)
{
            settingsIndex = (settingsIndex + 1) % Mathf.Max(1, parameterCount);
            Rebuild();
}

/// <summary>
/// ШАГ ПАРАМЕТРА. Границы — те же, что в настройках: «−» на минимуме не уходит ниже, «+» на
/// максимуме — выше (проверено тестом `Screen_SettingsStepRespectsBounds`).
/// </summary>
private void StepSetting(int direction)
{
            // Только четыре параметра с шагом. Всё остальное — переключатели (`ToggleCurrentSetting`),
// и «проваливаться» в чужое действие здесь нельзя: именно так «Второй луч» менял
// дальность телепорта (дефект найден 23.10.2026).
if (settingsIndex > 3) return;
            switch (settingsIndex)
{
case 0:                    KvXrSettings.SetGroupPanelRadius(KvXrSettingStep.Step(                        KvXrSettings.GetGroupPanelRadius(), direction * 0.01f,                        KvXrGroupPanelLayout.MinPanelRadiusMeters, KvXrGroupPanelLayout.MaxPanelRadiusMeters));
                    break;
case 1:                    KvXrSettings.SetSmoothSpeed(KvXrSettingStep.Step(                        KvXrSettings.GetSmoothSpeed(), direction * 0.1f,                        KvXrSettings.MinSmoothSpeed, KvXrSettings.MaxSmoothSpeed));
                    break;
case 2:                    KvXrSettings.SetOperatorHeight(KvXrSettingStep.Step(                        KvXrSettings.GetOperatorHeight(), direction * 0.01f,                        KvXrSettings.MinOperatorHeight, KvXrSettings.MaxOperatorHeight));
                    break;
default:                    KvXrSettings.SetTeleportDistance(KvXrSettingStep.Step(                        KvXrSettings.GetTeleportDistance(), direction * 0.5f,                        KvXrSettings.MinTeleportDistance, KvXrSettings.MaxTeleportDistance));
                    break;
}            KvXrSettingsChanged.Notify();
            Rebuild();
            KvXrSettingsData data = CurrentSettings();
            List<string> settings = KvXrScreenModel.BuildSettings(data);
string value = settingsIndex + 1 < settings.Count ? settings[settingsIndex + 1] : "—";
Debug.Log("[KvXr] НАСТРОЙКА: " + value + " · шаг " + (direction > 0 ? "+" : "−"));
}
private void ToggleSecondaryRay()
{
bool now = !KvXrSettings.GetSecondaryRay();
            KvXrSettings.SetSecondaryRay(now);
            Rebuild();
Debug.Log("[KvXr] НАСТРОЙКА: второй луч " + (now ? "включён" : "выключен"));
}
private void BuildObjectsScreen()
{
            List<Transform> objects = CollectObjects();
            KazistovVvUIManager manager = KazistovVvUIManager.Instance;

// ИМЯ — ИЗ ТОГО ЖЕ ХРАНИЛИЩА, ЧТО И НА КОМПЬЮТЕРЕ (§41). На компьютере строку дерева
// подписывает ярлык узла (`ProjectNode.DisplayName` → `NodeLabels`), а не `Transform.name`.
// Показывать в шлеме другое имя значило бы «шлем и компьютер разошлись» ([K4]).
var names = new List<string>();
for (int i = 0; i < objects.Count; i++) names.Add(LabelOf(manager, objects[i]));
            List<string> list = KvXrScreenModel.BuildObjects(names, selectedObjectIndex);
CreateLabel("ScreenTitle", "Объекты", new Vector2(0f, 0.30f), 18);
float spacing = 0.062f;
float top = 0.20f;
for (int i = 0; i < list.Count && i < 7; i++)
{
int index = i;
CreateIcon("Obj_" + i, "robot", list[i], new Vector2(0f, top - i * spacing),                    delegate { SelectObject(index); });
}
}

/// <summary>Подпись объекта: ярлык узла дерева компьютера, иначе имя объекта сцены.</summary>
private static string LabelOf(KazistovVvUIManager manager, Transform target)
{
            if (target == null) return "(нет)";
            ProjectNode node = NodeFor(manager, target);
if (node != null && !string.IsNullOrEmpty(node.DisplayName)) return node.DisplayName;
return target.name;
}
private void BuildPropertiesScreen()
{
            List<Transform> objects = CollectObjects();
            KvXrObjectInfo info = new KvXrObjectInfo();
if (selectedObjectIndex >= 0 && selectedObjectIndex < objects.Count && objects[selectedObjectIndex] != null)
{
                Transform target = objects[selectedObjectIndex];
                Renderer renderer = target.GetComponentInChildren<Renderer>();
                Component[] components = target.GetComponents<Component>();
                info.Selected = true;
                info.Name = target.name;
                info.Kind = components.Length > 1 ? components[1].GetType().Name : "Transform";
                info.Position = target.position;
                info.Size = renderer != null ? renderer.bounds.size : Vector3.zero;
}
CreateLabel("ScreenTitle", "Свойства", new Vector2(0f, 0.30f), 18);
ShowRows(KvXrScreenModel.BuildProperties(info));

// ИМЯ МЕНЯЕТСЯ В ШЛЕМЕ (§41): путь тот же, что на компьютере — ярлык узла дерева.
CreateIcon("Prop_Rename", "rename", "Изменить имя объекта", new Vector2(-0.15f, -0.29f),                delegate { OpenNameInput(); }, false, "имя", 0.10f, 0.055f);
}
private void BuildHelpScreen()
{
CreateLabel("ScreenTitle", "Справка", new Vector2(0f, 0.30f), 18);
ShowRows(KvXrScreenModel.BuildHelp(), true);

// ПОИСК ПО КОМАНДАМ (§41): на компьютере это Ctrl+P, в шлеме — этот вход. Отдельной
// иконкой в нижнем ряду его не сделать: ряд из пяти входов закреплён тестом §40
// (`GroupLevel_ScreenRowIsHittable`: 8 групп + 5 экранов = 13 иконок).
CreateIcon("Help_Search", "palette", "Поиск по командам (как Ctrl+P)",                new Vector2(-0.15f, -0.29f), delegate { OpenSearch(); }, false, "поиск", 0.10f, 0.055f);
}

// ==================================================== ЭКРАН ВВОДА (§41)
/// <summary>Ссылка на строку-кандидата: её текст обновляется БЕЗ пересборки панели.</summary>
private Text inputCandidate;

/// <summary>Ссылка на подсказку (границы поля, причина отказа).</summary>
private Text inputHint;

/// <summary>Колесо символов опущено от строки значения, чтобы не наехать на надписи.</summary>
private static readonly Vector2 WheelCenterOffsetMeters = new Vector2(0f, -0.03f);

/// <summary>
/// ОТКРЫТЬ ВВОД ЧИСЛА (из «Настроек»). Строка в журнале называет поле, способ и границы —
/// по ней видно, что именно открылось и в каких пределах можно вводить ([A3]).
/// </summary>
public void OpenInput(KvXrValueFieldId field)
{
            KvXrValueFieldInfo info = KvXrValueFields.Get(field);
            pendingField = field;

// Куда вернуться: настройки шлема живут в «Настройках», настройки интерфейса компьютера —
// на экране «Интерфейс». Возврат не «примерный», а тот экран, откуда пришли.
returnLevel = field <= KvXrValueFieldId.TeleportDistance                ? KvXrPanelLevel.SettingsScreen                : KvXrPanelLevel.InterfaceScreen;
            input.BeginField(field);
Debug.Log("[KvXr] ВВОД: поле «" + info.LogTitle + "» · способ «" +                      KvXrInputMethodSetting.Label(KvXrInputMethodSetting.Get()) +                      "» · границы " + info.RangeText() + KvXrValueFields.UnitPart(info));
OpenScreen(KvXrPanelLevel.InputScreen);
}

/// <summary>ОТКРЫТЬ ВВОД ИМЕНИ выбранного объекта (из «Свойств»).</summary>
public void OpenNameInput()
{
            Transform target = SelectedObject();
if (target == null)
{
Debug.Log("[KvXr] ОБЪЕКТ: имя не изменено — объект не выбран (откройте «Объекты» и выберите строку)");
                return;
}            returnLevel = KvXrPanelLevel.PropertiesScreen;
            input.BeginText("Имя объекта", KvXrTextRules.MaxNameLength);
Debug.Log("[KvXr] ВВОД: поле «имя объекта» · способ «" +                      KvXrInputMethodSetting.Label(KvXrInputMethodSetting.Get()) +                      "» · не длиннее " + KvXrTextRules.MaxNameLength + " символов");
OpenScreen(KvXrPanelLevel.InputScreen);
}

/// <summary>ОТКРЫТЬ ПОИСК ПО КОМАНДАМ: экран сам показывает клавиатуру, затем найденное.</summary>
public void OpenSearch()
{
            returnLevel = KvXrPanelLevel.SearchScreen;

// «ИЗМЕНИТЬ ЗАПРОС» ДОЛЖНО ВЕРНУТЬ КЛАВИАТУРУ, а не прежнюю выдачу: экран показывает
// клавиатуру именно тогда, когда найденного нет. Поэтому результаты сбрасываются здесь.
searchResults.Clear();
            searchPage = 0;
            input.BeginText("Поиск команд", KvXrTextRules.MaxQueryLength);
OpenScreen(KvXrPanelLevel.SearchScreen);
}

/// <summary>
/// ЭКРАН «ВВОД»: заголовок поля, строка-кандидат, подсказка о границах и ввод — клавиатура
/// или колесо символов, смотря какой способ владелец выбрал в «Настройках».
/// </summary>
        /// <summary>
        /// ЕСТЬ ЛИ appID ДЛЯ PICO ASR (§42/Д20). Читается из `PXR_PlatformSetting` — того поля, которое
        /// владелец заполняет из консоли разработчика PICO. Пусто — голос недоступен, и строка скажет это.
        /// </summary>
        public static bool HasVoiceAppId()
        {
            Unity.XR.PXR.PXR_PlatformSetting settings = Unity.XR.PXR.PXR_PlatformSetting.Instance;
            if (settings == null) return false;
            return !string.IsNullOrEmpty(settings.appID);
        }

        /// <summary>ЕСТЬ ЛИ РАЗРЕШЕНИЕ НА МИКРОФОН (§42/Д20): без него PICO ASR не поднимется.</summary>
        public static bool HasMicrophonePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Microphone);
#else
            return UnityEngine.Microphone.devices != null && UnityEngine.Microphone.devices.Length > 0;
#endif
        }
private void BuildInputScreen()
{
            // Экран обязан быть работоспособным и без предварительного `OpenInput` (так его открывают
// тесты и так он ведёт себя после перезапуска): по умолчанию — ввод первого поля.
if (!input.IsActive) input.BeginField(pendingField);
            KvXrKeyboardKind kind = input.Numeric ? KvXrKeyboardKind.Numeric : KvXrKeyboardKind.Text;
builtIconSize = KvXrKeyboardLayout.KeySizeMeters(kind);
CreateLabel("ScreenTitle", "Ввод: " + input.Title, new Vector2(0f, 0.30f), 18);
            inputCandidate = CreateLabel("ScreenValue", input.Display, new Vector2(0f, 0.215f), 22);
string method = KvXrInputMethodSetting.Label(KvXrInputMethodSetting.Get());
if (KvXrInputMethodSetting.Get() == KvXrInputMethod.Voice)
{
                // ЧЕСТНО И СРАЗУ: голос выбран, но движка нет — набирайте клавиатурой.
// Службы распознавания Android в шлеме нет (проверено `pm query-services … RECOGNIZE_SPEECH`
// → «No services found»), а у PICO ASR пуст appID. Молчать об этом нельзя ([F8]).
                // §42/Д20: ПРИЧИНА БЕРЁТСЯ ИЗ ОДНОГО МЕСТА (`KvXrVoiceStatus`). Раньше здесь стоял свой
                // текст «нет движка распознавания», и владелец не узнавал, ЧТО нужно, чтобы голос
                // заработал. Теперь строка называет либо appID PICO, либо разрешение RECORD_AUDIO.
                method = KvXrVoiceStatus.Line(HasVoiceAppId(), HasMicrophonePermission());
                Debug.Log("[KvXr] ВВОД: " + method);
}            if (input.Numeric)
{
                KvXrValueFieldInfo field = KvXrValueFields.Get(input.FieldIndex);
                inputHint = CreateLabel("ScreenRange",                    "границы " + field.RangeText() + KvXrValueFields.UnitPart(field) + " · способ: " + method,                    new Vector2(0f, 0.16f), 14);
}            else            {
                inputHint = CreateLabel("ScreenRange",                    "способ: " + method + " · не длиннее " + input.MaxLength + " символов",                    new Vector2(0f, 0.16f), 14);
}            if (input.Numeric && KvXrInputMethodSetting.Get() == KvXrInputMethod.Wheel) BuildWheelKeys();
            else BuildKeyGrid(kind);
}

/// <summary>
/// КЛАВИАТУРА: каждый ключ — кнопка С НАДПИСЬЮ. Кнопки создаются тем же `CreateIcon`, поэтому
/// каждая попадает в список целей луча автоматически (правило §K «иконка = цель», карточка Б50).
/// </summary>
private void BuildKeyGrid(KvXrKeyboardKind kind)
{
int count = KvXrKeyboardLayout.KeyCount(kind);
for (int i = 0; i < count; i++)
{
                KvXrKey key = KvXrKeyboardLayout.Key(kind, i);
                KvXrKey local = key;
CreateIcon("Key_" + i, "keyboard", local.Label, local.OffsetMeters,                    delegate { PressKey(local); }, false, local.Label);
}
}

/// <summary>КОЛЕСО СИМВОЛОВ: 13 секторов по кругу, выбранный подсвечен, «ввод» — в центре.</summary>
private void BuildWheelKeys()
{
            builtIconSize = KvXrGroupPanelLayout.IconSizeMeters;
for (int i = 0; i < KvXrWheelLayout.SymbolCount; i++)
{
int index = i;
CreateIcon("Wheel_" + i, "more", KvXrWheelLayout.SymbolLabel(index),                    KvXrWheelLayout.OffsetMeters(index) + WheelCenterOffsetMeters,                    delegate { wheelIndex = index; AppendWheelSymbol(); }, index == wheelIndex);
}
CreateIcon("Wheel_Enter", "check", "Ввод (принять)", Vector2.zero,                delegate { CommitInput(); }, true, "ввод");
}

/// <summary>НАЖАТИЕ КЛЮЧА КЛАВИАТУРЫ: символ, отбой, ввод или отмена.</summary>
private void PressKey(KvXrKey key)
{
            switch (key.Kind)
{
case KvXrKeyKind.Backspace: BackspaceInput(); break;
case KvXrKeyKind.Enter: CommitInput(); break;
case KvXrKeyKind.Cancel: CancelInput(); break;
default: AppendChar(key.Symbol); break;
}
}

/// <summary>Дописать символ (один путь и для клавиатуры, и для колеса).</summary>
public void AppendChar(char symbol)
{
            if (!input.Append(symbol)) return;
if (inputCandidate != null) inputCandidate.text = input.Display;
}

/// <summary>Отбой: убрать последний символ.</summary>
public void BackspaceInput()
{
            input.Backspace();
if (inputCandidate != null) inputCandidate.text = input.Display;
}

/// <summary>Вставить выбранный сектор колеса (курок или щипок).</summary>
public void AppendWheelSymbol()
{
            char symbol = KvXrWheelLayout.Symbol(wheelIndex);
if (symbol == '\0') BackspaceInput();
            else AppendChar(symbol);
}

/// <summary>Сдвинуть выбор колеса стиком: +1 — следующий сектор, −1 — предыдущий.</summary>
public void StepWheel(int direction)
{
            wheelIndex = KvXrWheelLayout.Step(wheelIndex, direction);
            Rebuild();
}

/// <summary>Отсечка повтора стика: без неё одно движение перескочило бы несколько символов.</summary>
private const float WheelStepCooldownSeconds = 0.25f;
private float nextWheelStep;

/// <summary>
/// СТИК ЛИСТАЕТ СЕКТОРЫ КОЛЕСА (задание [A3]). Порог — тот же, что у поворота шагами (0,7),
/// чтобы «стик до упора» означал одно и то же в вводе и в перемещении.
/// </summary>
private void UpdateWheelFromStick()
{
            if (!StickCaptured) return;
if (Time.unscaledTime < nextWheelStep) return;
Vector2 stick = KvXrInputSource.RayStick;
float horizontal = Mathf.Abs(stick.x) >= Mathf.Abs(stick.y) ? stick.x : stick.y;
if (Mathf.Abs(horizontal) < KvXrWheelLayout.StickThreshold) return;
            StepWheel(horizontal > 0f ? 1 : -1);
            nextWheelStep = Time.unscaledTime + WheelStepCooldownSeconds;
}

/// <summary>
/// ОТМЕНА ВВОДА. Значение не меняется — и это сказано в журнале, а не оставлено на догадку.
/// </summary>
public void CancelInput()
{
Debug.Log(input.Numeric                ? "[KvXr] ВВОД: отменён — значение не изменено"                : "[KvXr] ВВОД: отменён · поле «" + input.Title + "»");
            input.End();
level = returnLevel;
            Rebuild();
}

/// <summary>
/// «ВВОД» — передать строку-кандидата в ЕДИНСТВЕННУЮ точку сохранения ([A3]).
/// Число проверяется и пишется `KvXrValueInput.Commit`, имя — контрактом дерева компьютера,
/// строка запроса — поиском. Экран при отказе остаётся открытым, значение не меняется.
/// </summary>
public void CommitInput()
{
            if (!input.IsActive) return;
if (input.Numeric)
{
                KvXrCommitOutcome outcome = KvXrValueInput.Commit(                    input.FieldIndex, input.Text, KvXrInputMethodSetting.Get());
Debug.Log(outcome.LogLine);
if (!outcome.Saved)
{
                    if (inputHint != null) inputHint.text = "отказ: " + outcome.Reason;
                    return;
}                input.End();
level = returnLevel;
                Rebuild();
                return;
}            CommitText();
}

/// <summary>Сохранение НЕ числа: строка поиска или имя объекта.</summary>
private void CommitText()
{
string text = input.Text.Trim();
if (returnLevel == KvXrPanelLevel.SearchScreen)
{
                searchQuery = text;
                searchResults.Clear();
                searchResults.AddRange(KvCommandSearch.Filter(KvCommands.All, text));
                searchPage = 0;
Debug.Log(KvCommandSearch.SearchLogLine(searchQuery, searchResults.Count));
                input.End();
level = KvXrPanelLevel.SearchScreen;
                Rebuild();
                return;
}            Transform target = SelectedObject();
if (target == null)
{
Debug.Log("[KvXr] ОБЪЕКТ: имя не изменено — объект не выбран");
                return;
}            if (!KvXrTextRules.IsAcceptableName(text))
{
Debug.Log("[KvXr] ОБЪЕКТ: имя «" + text + "» отклонено — пустое, длиннее " +                          KvXrTextRules.MaxNameLength + " символов или с управляющими символами");
                return;
}            RenameObject(target, text);
}

/// <summary>
/// ПЕРЕИМЕНОВАНИЕ ЧЕРЕЗ КОНТРАКТ КОМПЬЮТЕРА: `KvTreeView.BeginRename` → `SetEditText` →
/// `CommitRenameNow`. Своего переименования в шлеме нет: меняется тот же ярлык узла
/// (`ProjectNode.DisplayName` → `NodeLabels`), который читает компьютер, и на диск, как и на
/// компьютере, ничего не пишется — ярлык живёт в сеансе.
/// </summary>
private void RenameObject(Transform target, string name)
{
            KazistovVvUIManager manager = KazistovVvUIManager.Instance;
            KvTreeView tree = manager != null ? manager.Tree : null;
            ProjectNode node = NodeFor(manager, target);
if (tree == null || node == null)
{
Debug.Log("[KvXr] ОБЪЕКТ: имя не изменено — дерево моделей компьютера недоступно " +                          "или объект в нём не найден");
                return;
}
string before = node.DisplayName;
            tree.BeginRename(node, RenameProbe());
            tree.SetEditText(name);
            tree.CommitRenameNow();
Debug.Log("[KvXr] ОБЪЕКТ: имя «" + before + "» → «" + node.DisplayName +                      "» · сохранено в сеансе (ярлык узла)");
            input.End();
level = KvXrPanelLevel.PropertiesScreen;
            Rebuild();
}

/// <summary>Узел дерева компьютера, соответствующий объекту сцены.</summary>
private static ProjectNode NodeFor(KazistovVvUIManager manager, Transform target)
{
            if (manager == null || target == null) return null;
            IReadOnlyList<ProjectNode> model = manager.TreeModel;
if (model == null) return null;
for (int i = 0; i < model.Count; i++)
{
                ProjectNode node = model[i];
if (node == null) continue;
if (node.WorldTransform == target) return node;
if (node.Robot != null && node.Robot.transform == target) return node;
}            return null;
}

/// <summary>
/// СКРЫТАЯ НАДПИСЬ-ПРОБА. Контракт переименования на компьютере требует ярлык строки
/// (`BeginRename(node, label)`), чтобы рисовать в нём курсор. В шлеме строки нет, поэтому
/// передаётся невидимая надпись — сама логика имени от ярлыка не зависит.
/// </summary>
private Text RenameProbe()
{
            if (renameProbe == null && canvasRect != null)
{
                renameProbe = KvTheme.CreateText(canvasRect, "RenameProbe", "", 10,                    TextAnchor.MiddleCenter, KvTheme.TextMain);
                renameProbe.gameObject.SetActive(false);
}            return renameProbe;
}

/// <summary>Выбранный объект сцены (список тот же, что на экране «Объекты»).</summary>
private Transform SelectedObject()
{
            List<Transform> objects = CollectObjects();
if (selectedObjectIndex < 0 || selectedObjectIndex >= objects.Count) return null;
return objects[selectedObjectIndex];
}

/// <summary>
/// ЭКРАН «ПОИСК КОМАНД»: запрос, число найденного и строки результата. Строки — кнопки с
/// надписью во всю ширину панели: попадание считается от центра, но читается надпись целиком.
/// </summary>
private void BuildSearchScreen()
{
            // Экран самодостаточен: пока запрос не набран — он показывает КЛАВИАТУРУ, после «ввода» —
// НАЙДЕННОЕ. Так владельцу не нужно искать, где набирать, а найденное не спорит за место
// с клавишами.
if (!input.IsActive) input.BeginText("Поиск команд", KvXrTextRules.MaxQueryLength);
CreateLabel("ScreenTitle", "Поиск команд", new Vector2(0f, 0.30f), 18);
bool hasResults = searchResults.Count > 0;
if (!hasResults)
{
                builtIconSize = KvXrKeyboardLayout.TextKeyMeters;
                inputCandidate = CreateLabel("ScreenQuery", input.Display, new Vector2(0f, 0.235f), 20);
                inputHint = CreateLabel("ScreenHint",                    "наберите запрос и нажмите «ввод» · способ: " +                    KvXrInputMethodSetting.Label(KvXrInputMethodSetting.Get()),                    new Vector2(0f, 0.185f), 13);
BuildKeyGrid(KvXrKeyboardKind.Text);
                return;
}            builtIconSize = KvXrGroupPanelLayout.IconSizeMeters;
CreateLabel("ScreenQuery", "«" + searchQuery + "»", new Vector2(0f, 0.235f), 16);
CreateLabel("ScreenCount", "найдено " + searchResults.Count, new Vector2(0f, 0.185f), 14);
            const int perPage = 6;
int first = searchPage * perPage;
for (int i = 0; i < perPage; i++)
{
int index = first + i;
if (index >= searchResults.Count) break;
                KvCommand command = searchResults[index];
if (command == null) continue;
int captured = index;
string title = command.LocalizedTitle;
CreateIcon("SearchRow_" + i, "more", title,                    new Vector2(0f, 0.115f - i * 0.062f),                    delegate { ExecuteSearchResult(captured); }, false, title, 0.36f, 0.050f);
}
CreateIcon("Search_Edit", "keyboard", "Изменить запрос", new Vector2(-0.15f, -0.255f),                delegate { OpenSearch(); }, false, "ABC", 0.10f, 0.055f);
if (searchResults.Count > perPage)
{
CreateIcon("Search_Next", "next", "Следующие", new Vector2(0.15f, -0.255f),                    delegate { NextSearchPage(); }, false, "ещё", 0.10f, 0.055f);
}
}

/// <summary>Следующая страница результатов (по кругу).</summary>
private void NextSearchPage()
{
            const int perPage = 6;
int pages = Mathf.Max(1, Mathf.CeilToInt(searchResults.Count / (float)perPage));
            searchPage = (searchPage + 1) % pages;
            Rebuild();
}

/// <summary>
/// ВЫПОЛНИТЬ НАЙДЕННУЮ КОМАНДУ. Недоступная команда не выполняется — причина называется
/// вслух, как на компьютере. Выполнение — тот же `KvCommands.Invoke`, что и на компьютере.
/// </summary>
public void ExecuteSearchResult(int index)
{
            if (index < 0 || index >= searchResults.Count) return;
            KvCommand command = searchResults[index];
if (command == null) return;
if (!KvCommandSearch.CanExecute(command))
{
Debug.Log("[KvXr] ПОИСК: команда «" + command.LocalizedTitle + "» недоступна — не выполнена");
                return;
}
Debug.Log(KvCommandSearch.ExecuteLogLine(searchQuery, searchResults.Count, command.LocalizedTitle));
            input.End();
level = KvXrPanelLevel.Groups;
            Rebuild();
            KvCommands.Invoke(command.Id);
}

/// <summary>
/// ПЕРЕКЛЮЧИТЬ СПОСОБ ВВОДА (кнопка на экране «Настройки»). Выбор владельца сохраняется,
/// строка журнала называет новый способ и прежний — из задания [S2].
/// 
/// ПОЧЕМУ ОТДЕЛЬНОЙ КНОПКОЙ, А НЕ СЕДЬМЫМ ПУНКТОМ СПИСКА. Список «Настроек» — это модель
/// `KvXrScreenModel.BuildSettings`, и её состав закреплён уже запускавшимся тестом §40
/// (`Screen_SettingsRowOrderIsFixed`: «заголовок + шесть параметров»). Седьмая строка сломала
/// бы чужой тест, поэтому способ ввода живёт кнопкой на том же экране.
/// </summary>
public void CycleInputMethod()
{
            KvXrInputMethod was = KvXrInputMethodSetting.Get();
            KvXrInputMethod now = KvXrInputMethodSetting.Next(was);
            KvXrInputMethodSetting.Set(now);
Debug.Log(KvXrInputMethodSetting.SwitchLogLine(now, was));
            Rebuild();
}

// ==================================================== СТАРТОВОЕ МЕНЮ (§41)
/// <summary>Выбранный раздел меню.</summary>
private KvXrStartSection startSection = KvXrStartSection.Tables;
private readonly List<Transform> startTables = new List<Transform>();
private readonly List<Transform> startRobots = new List<Transform>();

/// <summary>Какой раздел открыт в меню (для проверок и журнала).</summary>
public KvXrStartSection StartSection {
 get {
 return startSection; } }

/// <summary>Сколько столов найдено в сцене на момент построения меню.</summary>
public int StartTableCount {
 get {
 return startTables.Count; } }

/// <summary>Сколько роботов найдено в сцене на момент построения меню.</summary>
public int StartRobotCount {
 get {
 return startRobots.Count; } }

/// <summary>ОТКРЫТЬ СТАРТОВОЕ МЕНЮ (вызывается при запуске и командой панели).</summary>
public void OpenStartMenu()
{
            level = KvXrPanelLevel.StartMenu;
            Rebuild();
Debug.Log("[KvXr] МЕНЮ: открыто · раздел «" + KvXrStartMenuLayout.SectionTitle((int)startSection) +                      "» · столов " + startTables.Count + " · роботов " + startRobots.Count +                      " · иконок " + icons.Count);
}

/// <summary>
/// ПОСТРОИТЬ СТАРТОВОЕ МЕНЮ: подложка во всю панель, слева разделы, справа строки
/// выбранного раздела. Вид — как у настроек PICO (указание владельца), поэтому это плита
/// с колонками, а не дуга иконок у руки.
/// </summary>
private void BuildStartMenuLevel()
{
            builtIconSize = KvXrGroupPanelLayout.IconSizeMeters;

// ЛУЧ УПИРАЕТСЯ В ПЛИТУ (указание владельца): вне кнопок он больше не пролетает сквозь
// оверлей. Размеры — из раскладки меню, те же, что видит владелец.
PanelSurfaceHalfSize = new Vector2(KvXrStartMenuLayout.WidthMeters * 0.5f,                                               KvXrStartMenuLayout.HeightMeters * 0.5f);
RefreshStartLists();
            CreatePlate("MenuPlate", Vector2.zero,                new Vector2(KvXrStartMenuLayout.WidthMeters, KvXrStartMenuLayout.HeightMeters),                new Color(0.09f, 0.10f, 0.12f, 0.97f));
CreateLabel("MenuTitle", "KazistovVv", new Vector2(0f, 0.335f), 20);
for (int i = 0; i < KvXrStartMenuLayout.SectionCount; i++)
{
int index = i;
bool active = (int)startSection == i;
CreateIcon("MenuSec_" + i, KvXrStartMenuLayout.SectionIcon(i),                    KvXrStartMenuLayout.SectionTitle(i), KvXrStartMenuLayout.SectionCenter(i),                    delegate { SelectStartSection(index); }, active,                    KvXrStartMenuLayout.SectionTitle(i),                    KvXrStartMenuLayout.SectionHalfSize.x * 2f,                    KvXrStartMenuLayout.SectionHalfSize.y * 2f, true);
}            List<string> rows = StartContentLabels();
for (int i = 0; i < rows.Count && i < KvXrStartMenuLayout.MaxContentRows; i++)
{
int index = i;
CreateIcon("MenuRow_" + i, "chevron-right", rows[i], KvXrStartMenuLayout.ContentCenter(i),                    delegate { PressStartRow(index); }, false, rows[i],                    KvXrStartMenuLayout.ContentHalfSize.x * 2f,                    KvXrStartMenuLayout.ContentHalfSize.y * 2f, true);
}
Status = "меню · раздел «" + KvXrStartMenuLayout.SectionTitle((int)startSection) +                     "» · строк " + rows.Count + " · столов " + startTables.Count +                     " · роботов " + startRobots.Count;
}

/// <summary>Строки содержимого выбранного раздела — в порядке показа сверху вниз.</summary>
private List<string> StartContentLabels()
{
var labels = new List<string>();
            switch (startSection)
{
case KvXrStartSection.Tables:                {
int shown = Mathf.Min(startTables.Count, KvXrStartMenuRows.MaxExistingRows);
for (int i = 0; i < shown; i++) labels.Add("Стол: " + NameOf(startTables[i]));
                    labels.Add(KvXrTableKinds.Label(KvXrTableKind.Square));
                    labels.Add(KvXrTableKinds.Label(KvXrTableKind.Auto));
                    labels.Add("Запустить стенд (добавить стол и роботов)");
                    break;
}
case KvXrStartSection.Robots:                {
int shown = Mathf.Min(startRobots.Count, KvXrStartMenuRows.MaxExistingRows);
for (int i = 0; i < shown; i++) labels.Add("Робот: " + NameOf(startRobots[i]));
                    labels.Add("Добавить 6-осевой робот");
                    labels.Add("Добавить SCARA");
                    labels.Add("Сменить робота");
                    break;
}
case KvXrStartSection.Project:                    labels.Add("Сброс сцены (новый проект)");
                    labels.Add("Открыть сессию");
                    labels.Add("Сохранить сессию");
                    labels.Add("Показать демо");
                    break;
case KvXrStartSection.Tutorial:                    labels.Add("Начать обучение");
                    break;
case KvXrStartSection.Settings:                    labels.Add("Настройки шлема");
                    labels.Add("Интерфейс компьютера");
                    break;
default:                    labels.Add("Вернуться к работе");
                    labels.Add("Выйти из приложения");
                    break;
}            return labels;
}

/// <summary>Имя объекта для строки меню: имя объекта сцены (не ярлык — ярлык только у роботов).</summary>
private static string NameOf(Transform target)
{
            if (target == null) return "(нет)";
            RegisteredObject registered = target.GetComponent<RegisteredObject>();
if (registered != null && !string.IsNullOrEmpty(registered.DisplayName)) return registered.DisplayName;
return target.name;
}

/// <summary>ПЕРЕКЛЮЧИТЬ РАЗДЕЛ МЕНЮ; список справа перестраивается.</summary>
public void SelectStartSection(int index)
{
            startSection = KvXrStartMenuLayout.Normalize(index);
            Rebuild();
Debug.Log("[KvXr] МЕНЮ: раздел «" + KvXrStartMenuLayout.SectionTitle((int)startSection) + "»");
}

/// <summary>
/// НАЖАТИЕ СТРОКИ МЕНЮ. Действия — существующие команды компьютера (`KvCommands.Invoke`),
/// своего «второго» действия у шлема нет.
/// </summary>
public void PressStartRow(int row)
{
            switch (startSection)
{
case KvXrStartSection.Tables:
case KvXrStartSection.Robots:                {
                    // §42: состав строк и действие считаются ОДНОЙ функцией — рассинхронизировать нельзя.
KvXrStartRowAction action = KvXrStartMenuRows.ActionFor(startSection, row,                        startTables.Count, startRobots.Count);
                    switch (action)
{
case KvXrStartRowAction.SelectObject:                            if (startSection == KvXrStartSection.Tables && row < startTables.Count)
SelectStartObject(startTables[row], "стол");
                            else if (row < startRobots.Count) SelectStartObject(startRobots[row], "робот");
                            return;
case KvXrStartRowAction.CreateSquareTable:
case KvXrStartRowAction.CreateAutoTable:                            CreateStartTable(action);
                            return;
case KvXrStartRowAction.CreateSixAxisRobot:                            CreateStartRobot(false);
                            return;
case KvXrStartRowAction.CreateScaraRobot:                            CreateStartRobot(true);
                            return;
case KvXrStartRowAction.RunStand:                            CreateStartTable(KvXrStartRowAction.CreateSquareTable);
                            CreateStartRobot(false);
                            return;
case KvXrStartRowAction.SwitchRobot:                            InvokeMenuCommand("robot.switch", "Сменить робота");
                            return;
}                    return;
}
case KvXrStartSection.Project:                    if (row == 0) InvokeMenuCommand("edit.reset", "Сброс сцены (новый проект)");
                    else if (row == 1) InvokeMenuCommand("session.load", "Открыть сессию");
                    else if (row == 2) InvokeMenuCommand("session.save", "Сохранить сессию");
                    else InvokeMenuCommand("demo.quick", "Показать демо");
                    return;
case KvXrStartSection.Tutorial:                    InvokeMenuCommand("tut.toggle", "Начать обучение");
                    return;
case KvXrStartSection.Settings:                    OpenWindow(row == 0 ? KvXrPanelLevel.SettingsScreen : KvXrPanelLevel.InterfaceScreen);
                    return;
default:                    if (row == 0)
{
                        // «Вернуться к работе» — это ЗАКРЫТЬ МЕНЮ, а не «показать иконки групп»:
// панель меню не должна превращаться в панель у руки.
if (KvXrStartMenu.Active != null)
{
                            KvXrStartMenu.Active.Hide();
                            return;
}
Debug.Log("[KvXr] МЕНЮ: возврат к работе");
CloseScreen();
}                    else                    {
Debug.Log("[KvXr] МЕНЮ: выход из приложения");
                        Application.Quit();
}                    return;
}
}

/// <summary>ВЫБРАТЬ СТОЛ ИЛИ РОБОТА В МЕНЮ: выбор уходит и в дерево компьютера (один источник).</summary>
private void SelectStartObject(Transform target, string kind)
{
            if (target == null) return;
            KazistovVvUIManager manager = KazistovVvUIManager.Instance;
if (manager != null && manager.Tree != null)
{
                ProjectNode node = NodeFor(manager, target);
if (node != null) manager.Tree.Select(node);
}            if (kind == "робот")
{
                List<Transform> objects = CollectObjects();
int found = objects.IndexOf(target);
if (found >= 0) selectedObjectIndex = found;
}
Debug.Log("[KvXr] МЕНЮ: выбран " + kind + " «" + NameOf(target) + "» · столов " +                      startTables.Count + " · роботов " + startRobots.Count);
}

/// <summary>ВЫПОЛНИТЬ КОМАНДУ КОМПЬЮТЕРА из меню; недоступная — не выполняется и это сказано.</summary>
/// <summary>СОЗДАТЬ СТОЛ ИЗ МЕНЮ (§42): числа в журнал, объект в мир перед оператором.</summary>
private void CreateStartTable(KvXrStartRowAction action)
{
            KvXrTableKind kind = action == KvXrStartRowAction.CreateAutoTable                ? KvXrTableKind.Auto : KvXrTableKind.Square;
float reach = KvXrObjectFactory.ReachOf(KvXrObjectFactory.ActiveRobot());
Vector3 spot = KvXrObjectFactory.SpotInFrontOf(headCamera, KvXrObjectFactory.SpawnDistanceMeters);
Vector3 eye = headCamera != null ? headCamera.transform.position : Vector3.zero;
float yaw = KvXrObjectFactory.YawTowards(spot, eye);
            GameObject table = KvXrObjectFactory.CreateTable(kind, spot, yaw, reach);
if (table == null)
{
Debug.LogWarning("[KvXr] СТОЛ: не создан — построитель вернул пусто");
                return;
}
Debug.Log(KvXrTableKinds.DescribeCreate(kind, reach));
RefreshStartLists();
            Rebuild();
Debug.Log("[KvXr] МЕНЮ: создан стол «" + table.name + "» · столов " + startTables.Count +                      " · роботов " + startRobots.Count);
}

/// <summary>СОЗДАТЬ РОБОТА ИЗ МЕНЮ (§42): копия сценного эталона на столешнице перед оператором.</summary>
private void CreateStartRobot(bool scara)
{
            Vector3 spot = KvXrObjectFactory.SpotInFrontOf(headCamera, KvXrObjectFactory.SpawnDistanceMeters);
            spot.y = KvXrTableKinds.TopHeight;
            RobotController robot = KvXrObjectFactory.CreateRobot(scara, spot, 0f);
if (robot == null) return;
RefreshStartLists();
            Rebuild();
Debug.Log("[KvXr] МЕНЮ: создан робот «" + robot.robotName + "» · столов " + startTables.Count +                      " · роботов " + startRobots.Count);
}
private void InvokeMenuCommand(string id, string title)
{
            KvCommand command = KvCommands.Get(id);
if (command == null)
{
Debug.Log("[KvXr] МЕНЮ: команда «" + title + "» (" + id + ") не найдена — не выполнена");
                return;
}            if (!command.Enabled)
{
Debug.Log("[KvXr] МЕНЮ: команда «" + title + "» недоступна — не выполнена");
                return;
}
Debug.Log("[KvXr] МЕНЮ: выбрано «" + title + "» · команда " + id + " выполнена");
            KvCommands.Invoke(id);
}

/// <summary>Перечитать столы и роботов сцены (меню показывает то, что есть сейчас).</summary>
private void RefreshStartLists()
{
            startTables.Clear();
            startRobots.Clear();
            // §42, дефект Д14: В СПИСОК СТОЛОВ ПОПАДАЛИ РОБОТЫ. Копия робота тоже получает
            // `RegisteredObject` (так его видит дерево проекта), поэтому раздел «Стол» показывал
            // строку «Стол: 6-осевой робот_101404», и владелец нажимал её как стол. Найдено в шлеме
            // 25.10.2026 по журналу: `ПАНЕЛЬ: НАЖАТИЕ «Стол: 6-осевой робот_101404»`.
            // Теперь стол — объект с `RegisteredObject`, но БЕЗ `RobotController`.
            RegisteredObject[] registered = Object.FindObjectsByType<RegisteredObject>(FindObjectsInactive.Exclude);
            for (int i = 0; i < registered.Length; i++)
            {
                if (registered[i] == null) continue;
                if (registered[i].GetComponent<RobotController>() != null) continue;   // робот — не стол
                startTables.Add(registered[i].transform);
            }
            startRobots.AddRange(CollectObjects());
}

/// <summary>
/// ПОДЛОЖКА (плита) — не цель луча: `raycastTarget = false`, в список целей НЕ попадает.
/// Нужна для вида «как в настройках PICO»: строки лежат на плите, а не в пустоте.
/// </summary>
private void CreatePlate(string name, Vector2 offsetMeters, Vector2 sizeMeters, Color color)
{
            if (canvasRect == null) return;
var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(canvasRect, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
float unit = Mathf.Max(0.0001f, metersPerCanvasUnit);
            rect.sizeDelta = new Vector2(sizeMeters.x / unit, sizeMeters.y / unit);
            rect.anchoredPosition = offsetMeters / unit;
var image = go.GetComponent<Image>();
ApplyRounded(image, true);
            image.color = color;
            image.raycastTarget = false;
            rows.Add(go);
}

/// <summary>
/// ФОРМА ПОДЛОЖКИ: скруглённая или прямоугольная. Скругление включается у меню (указание
/// владельца 24.10.2026: «углы надо скруглить»), у иконок панели остаётся как было — их вид
/// уже принят и менять его без просьбы нельзя.
/// </summary>
private static void ApplyRounded(Image image, bool rounded)
{
            if (image == null) return;
if (!rounded)
{
                image.sprite = KvTheme.WhiteSprite;
                image.type = Image.Type.Simple;
                return;
}            image.sprite = KvXrRoundedSprite.Get();
            image.type = Image.Type.Sliced;
}

/// <summary>
/// ГОЛОВКА ПЛИТЫ, ПО КОТОРОЙ ЛУЧ УПИРАЕТСЯ, метры (полуразмеры). Ноль — плиты нет, луч сквозь
/// интерфейс проходит как раньше.
/// 
/// Указание владельца 24.10.2026: «Меня не устраивает, что вне кнопок лазер пролетает сквозь
/// оверлей (классно, что есть небольшое притяжение ко кнопкам)». То есть притяжение к кнопкам
/// оставляем, а сквозь плиту луч пускать нельзя: он должен останавливаться на ней.
/// </summary>
public Vector2 PanelSurfaceHalfSize {
 get; private set; }

/// <summary>
/// ЛУЧ ПОПАЛ В САМУ ПЛИТУ (не в кнопку). Возвращает расстояние до плоскости и признак, что
/// точка внутри плиты. Используется указателем, чтобы оборвать луч на оверлее.
/// </summary>
public bool TryHitPanelSurface(Vector3 rayOrigin, Vector3 rayDirection, out float alongMeters)
{
            alongMeters = 0f;
if (canvasRect == null || !canvasRect.gameObject.activeInHierarchy) return false;
if (PanelSurfaceHalfSize.x <= 0f || PanelSurfaceHalfSize.y <= 0f) return false;
Vector2 pointMeters;
if (!KvXrGroupPanelLayout.TryRayToPanelPlane(rayOrigin, rayDirection,                    canvasRect.position, canvasRect.right, canvasRect.up, canvasRect.forward,                    out pointMeters, out alongMeters)) return false;
if (Mathf.Abs(pointMeters.x) > PanelSurfaceHalfSize.x) return false;
if (Mathf.Abs(pointMeters.y) > PanelSurfaceHalfSize.y) return false;
return true;
}

/// <summary>
/// СТРОКИ ЭКРАНА — ТОЛЬКО ТЕКСТ, без попадания лучом (кроме кнопок).
/// 
/// `skipTitle` обязателен: у моделей «Состояние» и «Справка» ПЕРВАЯ строка — это заголовок, а
/// заголовок уже нарисован отдельно. Без пропуска заголовок рисовался дважды, а последняя
/// строка данных обрезалась (исправлено 23.10.2026 при ночной проверке кода).
/// </summary>
private void ShowRows(List<string> list, bool skipTitle = false)
{
float spacing = 0.062f;
float top = 0.20f;
int start = skipTitle ? 1 : 0;
int drawn = 0;
for (int i = start; i < list.Count && drawn < 8; i++, drawn++)
CreateLabel("ScreenRow_" + drawn, list[i], new Vector2(0f, top - drawn * spacing), 17);
}

/// <summary>
/// НАДПИСЬ БЕЗ ПОПАДАНИЯ ЛУЧОМ. Такие надписи НЕ регистрируются в списках иконок — иначе
/// номера иконок разъехались бы с номерами попаданий луча.
/// </summary>
private Text CreateLabel(string name, string text, Vector2 offsetMeters, int fontSize)
{
            if (canvasRect == null) return null;
var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            Text text2 = KvTheme.CreateText((RectTransform)go.transform, "Text", text, fontSize,                TextAnchor.MiddleCenter, KvTheme.TextMain);
            RectTransform rect = text2.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(canvasSize * 0.92f, fontSize * 1.6f);
float unit = Mathf.Max(0.0001f, metersPerCanvasUnit);
            rect.anchoredPosition = offsetMeters / unit;
            rows.Add(go);

// ССЫЛКА НУЖНА ВВОДУ (§41): строка-кандидат обновляется БЕЗ пересборки панели, иначе
// каждый набранный символ пересоздавал бы всю клавиатуру (52 кнопки) — это и кадры, и мусор.
return text2;
}

/// <summary>Объекты для экранов «Объекты» и «Свойства»: роботы сцены.</summary>
private static List<Transform> CollectObjects()
{
var list = new List<Transform>();
            RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(                FindObjectsInactive.Exclude);
for (int i = 0; i < robots.Length; i++)
if (robots[i] != null) list.Add(robots[i].transform);
return list;
}

/// <summary>Выбрать объект по номеру строки (экран «Объекты») и перейти к свойствам.</summary>
public void SelectObject(int index)
{
            selectedObjectIndex = index;
            List<Transform> objects = CollectObjects();
string name = index >= 0 && index < objects.Count && objects[index] != null                ? objects[index].name : "(нет)";
Debug.Log("[KvXr] ОБЪЕКТ: выбран «" + name + "» (номер " + index + ")");
OpenScreen(KvXrPanelLevel.PropertiesScreen);
}

/// <summary>Раскрыть веер группы.</summary>
public void OpenGroup(int index)
{
            // ИМЯ ГРУППЫ СНИМАЕТСЯ ДО ПЕРЕСБОРКИ — и это исправление дефекта, найденного
// в живом журнале 19.10.2026. Прежняя строка читала название ПОСЛЕ `Rebuild()`:
// список иконок к тому моменту уже перестроен под веер, и в журнал уходило имя
// первой иконки веера («ВЕЕР: группа «Сингулярности» раскрыта»), хотя нажимали
// группу «Визуализация». Строка-доказательство обязана называть то, что нажали.
string groupTitle = TitleAt(index);
openGroupIndex = index;
level = KvXrPanelLevel.GroupFan;
            Rebuild();

// СТРОКА-ДОКАЗАТЕЛЬСТВО ВЕЕРА (требование [S1.3]). Здесь видно всё, что владелец
// проверяет в шлеме: какая группа, сколько иконок команд, какая дуга и что подписей нет.
Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,                "[KvXr] ВЕЕР: группа «{0}» раскрыта — иконок команд {1} · в центре выход · дуга {2:0.0}° · " +                "название по удержанию {3:0.0} с (и в веере тоже) · выход центральной иконкой",                groupTitle, FanCommandCount, KvXrGroupPanelLayout.FanSpanDegrees,                KvXrGroupPanelLayout.LabelDwellSeconds));
}

/// <summary>Свернуть веер обратно к обобщающим иконкам групп.</summary>
public void CloseFan()
{
            openGroupIndex = -1;
level = KvXrPanelLevel.Groups;
            Rebuild();
Debug.Log("[KvXr] ВЕЕР: закрыт — снова иконки групп, иконок " + IconsOnLevel);
}

/// <summary>
/// ВЫПОЛНИТЬ КОМАНДУ — то же самое, что пункт меню или кнопка тулбара. Если команда
/// недоступна (заглушка), это видно в журнале: молча ничего не делать нельзя, иначе
/// кнопка в шлеме выглядит «неработающей» и причину не найти.
/// </summary>
private void InvokeCommand(string commandId)
{
            KvCommand command = KvCommands.Get(commandId);
if (command == null)
{
Debug.LogWarning("[KvXr] панель групп: команда не найдена: " + commandId);
                return;
}            if (!command.Enabled)
{
Debug.Log("[KvXr] панель групп: команда недоступна (заглушка): " + commandId);
                return;
}

// §42: ЕСЛИ КОМАНДА ОТКРЫВАЕТ ОКНО — ОТКРЫВАЕМ ОКНО В МИРЕ, а не выполняем её вслепую.
// Раньше окно рисовалось на погашенном десктопном канвасе, и владелец видел «нажалось и ничего».
if (KvXrDesktopWindows.IsWindowCommand(commandId))
{
                OpenDesktopWindow(commandId);
Debug.Log("[KvXr] ОКНО: команда " + commandId + " («" + command.LocalizedTitle +                          "») открывает окно в мире — выполнением не дублируется");
                return;
}            if (!KvCommands.Invoke(commandId))
Debug.LogWarning("[KvXr] панель групп: команда не выполнилась: " + commandId);
            else
Debug.Log("[KvXr] панель групп: выполнена команда " + commandId + " («" +                          command.LocalizedTitle + "») · веер ОСТАЁТСЯ открытым — выход по центральной иконке");

// ВЕЕР ПОСЛЕ КОМАНДЫ НЕ ЗАКРЫВАЕТСЯ (исправлено 21.10.2026 по словам владельца:
// «при нажатии на второй появляется первый, а должна выполняться иконка со второго слоя»).
// Раньше здесь стоял `CloseFan()`: команда выполнялась, но панель тут же возвращалась к
// иконкам групп, и выглядело это как «нажатие не сработало». Выход — центральная иконка
// (она нарисована с иконкой выбранной группы) либо щипок левой рукой.
}

// ==================================================== ЭКРАНЫ-ОКНА КОМПЬЮТЕРА (§42)
/// <summary>Центр ряда вкладок на окне, метры от центра панели.</summary>
private static readonly float TabRowY = 0.235f;

/// <summary>Половина размера кнопки вкладки, метры.</summary>
private static readonly Vector2 TabHalfSize = new Vector2(0.062f, 0.016f);

/// <summary>
/// ПОСТРОИТЬ ЭКРАН-ОКНО КОМПЬЮТЕРА (§42). Состав: заголовок окна, ряд вкладок с листанием,
/// живые строки вкладки и кнопки-команды. ВСЕ кнопки создаются ТОЛЬКО через `CreateIcon` —
/// правило «иконка = цель» (§K); иначе луч не будет их видеть (карточка Б50).
/// </summary>
private void BuildDesktopWindowScreen(KvXrPanelLevel screen)
{
            // §42: сюда можно попасть только с экраном-окном; иначе берём «Настройки ПК» (без молчаливой
// подмены на «Справку», как было бы с default в switch).
if (!IsDesktopWindowScreen(screen)) screen = KvXrPanelLevel.DesktopSettingsScreen;
            WindowScreen = screen;
builtIconSize = KvXrGroupPanelLayout.IconSizeMeters;

// СЧЁТЧИКИ СЧИТАЮТСЯ ЗДЕСЬ ЖЕ (исправление 25.10.2026). У `KvXrPanelLevel.DesktopWindow`
// значение 16, поэтому ветка `IsScreen` его не берёт, и счётчики `tabCount`/`pages`
// оставались от прошлого уровня — ряд вкладок не строился вовсе (в журнале «иконок 6»).
int windowTabCount = KvXrDesktopTabs.CountOf(screen);
if (WindowTab >= windowTabCount) WindowTab = 0;
int tabCount = windowTabCount;
int page = KvXrDesktopTabs.PageOf(WindowTab);
int first = KvXrDesktopTabs.FirstOnPage(page);
int pages = KvXrDesktopTabs.PageCount(tabCount);

// ВЫСОТА СТРОК — ПО ВЫСОТЕ ПЛИТЫ ОКНА, а не панели у руки (исправление 25.10.2026).
// Уровень окна строит `BuildScreenLevel`, а он сбрасывает `RadiusMeters` в радиус ПАНЕЛИ
// (0,110 м) и считает шаг строк 0,062 м — на плите окна 0,52 м это вылезает за край и
// наезжает на ряд вкладок. Ниже — те же числа, что у принятого меню: шаг 0,036 м.
float rowStep = WindowPlateHalfSize.y * 0.14f;

// 0,036 м при полувысоте 0,26 м
CreateLabel("WindowTitle", KvXrDesktopTabs.TitleOf(screen, WindowTab), new Vector2(0f, 0.30f), 17);
CreateLabel("WindowPage", "вкладка " + (WindowTab + 1) + " из " + tabCount +                                      " · страница " + (page + 1) + " из " + pages,                        new Vector2(0f, 0.278f), 11);

// --- ряд вкладок: 4 слота + листание
for (int slot = 0; slot < KvXrDesktopTabs.VisibleSlots; slot++)
{
int index = first + slot;
if (index >= tabCount) break;
int captured = index;
bool active = index == WindowTab;
float x = (slot - (KvXrDesktopTabs.VisibleSlots - 1) * 0.5f) * KvXrDesktopTabs.StepMeters;
string title = KvXrDesktopTabs.TabTitle(screen, index);
CreateIcon("Tab_" + slot, "info", title, new Vector2(x, TabRowY),                    delegate { OpenDesktopTab(captured); }, active, title,                    TabHalfSize.x * 2f, TabHalfSize.y * 2f, true);
}            if (pages > 1)
{
int prevTab = Mathf.Max(0, first - 1);
int nextTab = Mathf.Min(tabCount - 1, first + KvXrDesktopTabs.VisibleSlots);
CreateIcon("TabPrev", "chevron-down", "Вкладки: назад", new Vector2(-0.29f, TabRowY),                    delegate { OpenDesktopTab(prevTab); }, false, "вкладки назад", 0.06f, 0.032f, true);
CreateIcon("TabNext", "chevron-right", "Вкладки: вперёд", new Vector2(0.29f, TabRowY),                    delegate { OpenDesktopTab(nextTab); }, false, "вкладки вперёд", 0.06f, 0.032f, true);
}

// --- живые строки вкладки (своими строками окна: шаг 0,036 м, от y = +0,19 вниз
ShowWindowRows(KvXrScreenRows.Build(screen, WindowTab, GatherScreenData()), rowStep);

// --- кнопки-команды этой вкладки (те же команды, что на компьютере
AddDesktopWindowActions(screen, WindowTab);

            // --- ВЫХОД: внизу по центру, как у любого экрана (§40). Без него окно компьютера
            // нельзя закрыть крестиком, и владелец остался бы в окне без выхода.
            CreateIcon("Window_Close", "close", "Закрыть", new Vector2(0f, -0.29f),
                delegate { CloseScreen(); }, false, null, 0.06f, 0.032f, true);

// (строку журнала ставит ветка уровня окна — здесь только построение)
}

/// <summary>
/// СТРОКИ ОКНА КОМПЬЮТЕРА (§42) — с шагом окна, а не панели у руки.
/// Сверху вниз от y = +0,19 м до нижнего края плиты (0,26 м); не больше восьми строк.
/// Надписи — БЕЗ попадания лучом (`CreateLabel`), как и на прежних экранах: строки не должны
/// перехватывать нажатие у кнопок.
/// </summary>
private void ShowWindowRows(List<string> list, float rowStep)
{
            if (list == null) return;
float step = Mathf.Max(0.02f, rowStep);
float top = 0.19f;
int start = list.Count > 1 ? 1 : 0;

// первая строка — заголовок вкладки, он уже нарисован
int drawn = 0;
for (int i = start; i < list.Count && drawn < 8; i++, drawn++)
CreateLabel("WindowRow_" + drawn, list[i], new Vector2(0f, top - drawn * step), 15);
}

/// <summary>СОБРАТЬ ДАННЫЕ для строк из живых компонентов (строки остаются чистой функцией).</summary>
private KvXrScreenData GatherScreenData()
{
var data = new KvXrScreenData();
            data.RobotName = SelectedRobotName();
            data.Fps = CurrentFps();
            data.FrameMs = data.Fps > 0.01f ? 1000f / data.Fps : 0f;
            data.FrameCount = Time.frameCount;
            data.UptimeSeconds = Time.realtimeSinceStartup;
            data.UiScale = KazistovVvUI.KvSettings.UiScale;
            data.Language = KazistovVvUI.KvLoc.CurrentCode;
            data.Version = Application.version;
            data.Bindings = BindingLines();
            data.BindingsPerPage = 8;
            data.PendantMode = "Jog";
            data.ReferencePlaneHeight = TrajectoryCore.StandBuilder.TopHeight;
return data;
}

/// <summary>КНОПКИ-КОМАНДЫ ВКЛАДКИ: только существующие команды, никаких «вторых» действий.</summary>
private void AddDesktopWindowActions(KvXrPanelLevel screen, int tab)
{
var actions = new List<string>();
string key = KvXrDesktopTabs.KeyOf(screen, tab);
            // §42: ключи вкладок — АНГЛИЙСКИЕ ("waypoints", "topt", …), а не русские подписи. Раньше
            // сравнение шло по подписям, ни одно условие не совпадало, и кнопок у вкладки не было вовсе
            // (в журнале «иконок 6» при 23 вкладках — ровно ряд вкладок и листание).
if (screen == KvXrPanelLevel.FeaturesScreen)
{
                if (key == "record") actions.Add("record.toggle");
if (key == "poses") {
 actions.Add("pose.save"); actions.Add("pose.goto"); }                if (key == "compare") actions.Add("compare.mark");
if (key == "scenarios") actions.Add("scenarios.run");
if (key == "sessions") {
 actions.Add("session.save"); actions.Add("session.load"); }
}            else if (screen == KvXrPanelLevel.WorkbenchScreen)
{
                if (key == "smooth") actions.Add("post.apply.smooth");
if (key == "topt") actions.Add("post.apply.topt");
if (key == "energy") actions.Add("post.apply.energy");
if (key == "constraints") actions.Add("constr.toggle");
if (key == "collision") actions.Add("collision.toggle");
if (key == "planner") actions.Add("lab.run");
if (key == "report") actions.Add("report.make");
if (key == "validate") actions.Add("valid.run");
if (key == "export") actions.Add("export.robot");
if (key == "import") actions.Add("import.scan");
}            else if (screen == KvXrPanelLevel.DesktopSettingsScreen)
{
                if (key == "interface") actions.Add("ui.resetlayout");
}            else if (screen == KvXrPanelLevel.PendantScreen)
{
                actions.Add("pendant.jogmode");
                actions.Add("pendant.attach");
}
            for (int i = 0; i < actions.Count && i < 6; i++)
{
string id = actions[i];
                KvCommand command = KvCommands.Get(id);
                if (command == null) continue;
float x = (i % 3 - 1) * 0.19f;
float y = i < 3 ? -0.19f : -0.24f;
string title = command.LocalizedTitle;
CreateIcon("WinAct_" + id, "play", title, new Vector2(x, y),                    delegate { InvokeCommand(id); }, false, title, 0.18f, 0.032f, true);
}
}

/// <summary>Имя робота (активного или первого в сцене) — для строк экранов.</summary>
private static string SelectedRobotName()
{
            RobotController robot = KvXrObjectFactory.ActiveRobot();
return robot != null ? robot.robotName : "";
}

/// <summary>Кадры в секунду для экранов «Замер» и «Настройки ПК · Графика».</summary>
private static float CurrentFps()
{
float dt = Time.unscaledDeltaTime;
return dt > 0.0001f ? 1f / dt : 0f;
}

/// <summary>Строки реестра клавиш — того же, что на компьютере (не выдуманный список).</summary>
private static string[] BindingLines()
{
string dump = KazistovVvUI.KvBindings.Dump();
if (string.IsNullOrEmpty(dump)) return new string[0];
return dump.Split('\n');
}

// ==================================================== ПОДПИСЬ ПО УДЕРЖАНИЮ
/// <summary>
/// ПОДПИСЬ ПОЯВЛЯЕТСЯ ТОЛЬКО ПОСЛЕ 0,5 С УДЕРЖАНИЯ и ТОЛЬКО на уровне групп.
/// Решение «показывать или нет» принимает проверенный тестами
/// <see cref="KvXrGroupPanelLayout.ShowsLabel"/> — здесь только накопление времени.
/// </summary>
private void UpdateLabelDwell()
{
            if (label == null) return;

// НАКОПЛЕНИЕ ИДЁТ ТОЛЬКО ПОКА ЛУЧ НАВЕДЁН. Отвёл взгляд — счёт начинается заново:
// иначе подпись «залипала» бы и появлялась уже после ухода с иконки.
if (hovered != null) labelDwell = KvXrGroupPanelLayout.AdvanceLabelDwell(labelDwell, Time.deltaTime);
            else labelDwell = 0f;
bool allowed = KvXrGroupPanelLayout.ShowsLabel(level, labelDwell);
if (allowed) label.text = hovered != null ? hovered.Title : "";
if (label.gameObject.activeSelf != allowed) label.gameObject.SetActive(allowed);

// СТРОКА-ДОКАЗАТЕЛЬСТВО ПОДПИСИ (требование [S1.3]: «подтверждать строкой журнала»).
// Печатается один раз на появление подписи, а не каждый кадр: иначе журнал утонет.
if (allowed && !labelLogged)
{
labelLogged = true;
Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,                    "[KvXr] ПОДПИСЬ: «{0}» через {1:0.0} с удержания (уровень групп, луч наведён)",                    label.text, KvXrGroupPanelLayout.LabelDwellSeconds));
}            else if (!allowed)
{
labelLogged = false;
}
}

/// <summary>Панель сообщает о наведении на иконку (зовёт сама иконка).</summary>
internal void NotifyHover(KvXrIconHover icon, bool entered)
{
            if (entered)
{
                if (hovered != icon) labelDwell = 0f;

// новая цель — счёт заново
hovered = icon;
}            else if (hovered == icon)
{
hovered = null;
labelDwell = 0f;
}
}

// ==================================================== НАВЕДЕНИЕ ЛУЧОМ (геометрия, §36)
/// <summary>
/// ПОПАДАНИЕ ЛУЧА В ИКОНКУ ПАНЕЛИ. Считается геометрией в метрах: луч × плоскость панели.
/// 
/// ПОЧЕМУ НЕ uGUI. Прежний путь отдавал мировую точку в `GraphicRaycaster`, а он трактует
/// её как ЭКРАННЫЕ ПИКСЕЛИ (`GraphicRaycaster.cs:184` → `ScreenPointToRay`). Мировая точка
/// (0.4; 1.2; 0.5) превращалась в луч через левый нижний угол экрана — попасть в панель
/// было почти нельзя, и подпись с веером в шлеме оставались непроверенными. Геометрия
/// панели известна точно, поэтому здесь нет ни пикселей, ни камеры.
/// </summary>
public bool TryAim(Vector3 rayOrigin, Vector3 rayDirection,                           out int index, out float fromCenterMeters,                           out float alongRayMeters, out Vector3 hitPoint)
{
            index = -1;
            fromCenterMeters = 0f;
            alongRayMeters = 0f;
            hitPoint = rayOrigin;
if (canvasRect == null || !canvasRect.gameObject.activeInHierarchy) return false;
if (currentOffsets == null || currentOffsets.Length == 0) return false;
bool hit = KvXrGroupPanelLayout.TryHitTarget(                rayOrigin, rayDirection,                canvasRect.position, canvasRect.right, canvasRect.up, canvasRect.forward,                currentOffsets, currentHalfSizes, out index, out fromCenterMeters, out alongRayMeters,                Mathf.Max(0.008f, builtIconSize * 0.85f));
if (alongRayMeters > 0f)
hitPoint = rayOrigin + rayDirection.normalized * alongRayMeters;
return hit;
}

/// <summary>
/// СКАЗАТЬ ПАНЕЛИ, КАКАЯ ИКОНКА ПОД ЛУЧОМ (−1 — ни одна). Подпись и её удержание
/// считаются ровно там же, где и раньше: здесь только выбор цели.
/// </summary>
public void SetHoverIndex(int index)
{
            KvXrIconHover target = index >= 0 && index < hovers.Count ? hovers[index] : null;
if (hovered != null && hovered != target) NotifyHover(hovered, false);
if (target != null && hovered != target) NotifyHover(target, true);
if (target == null && hovered == null) labelDwell = 0f;
}

/// <summary>НАЗВАНИЕ ИКОНКИ ПО НОМЕРУ (пустая строка, если номера нет).</summary>
public string TitleAt(int index)
{
            return index >= 0 && index < iconTitles.Count ? iconTitles[index] : string.Empty;
}

/// <summary>
/// ЦЕНТР ИКОНКИ В МИРОВЫХ КООРДИНАТАХ (§39). Нужен курсору: луч заканчивается не на
/// невидимой плоскости канваса, а ровно в центре иконки, на которую наведён.
/// </summary>
public Vector3 IconWorldPosition(int index)
{
            if (canvasRect == null) return transform.position;
if (currentOffsets == null || index < 0 || index >= currentOffsets.Length)
return canvasRect.position;
Vector2 offset = currentOffsets[index];
return canvasRect.position + canvasRect.right * offset.x + canvasRect.up * offset.y;
}

/// <summary>
/// НАЖАТИЕ НА ИКОНКУ. Действие берётся то же, что висело на кнопке: второй реализации
/// команд не появляется. Строка журнала обязательна — по ней владелец видит, что
/// нажатие случилось и по какой иконке.
/// </summary>
public void PressIndex(int index)
{
            if (index < 0 || index >= iconActions.Count)
{
Debug.LogWarning("[KvXr] ПАНЕЛЬ: нажатие мимо иконки проигнорировано (номер " + index + ")");
                return;
}
string title = TitleAt(index);
Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,                "[KvXr] ПАНЕЛЬ: НАЖАТИЕ «{0}» (уровень {1})", title,                level == KvXrPanelLevel.Groups ? "групп" : "веера"));
            UnityEngine.Events.UnityAction action = iconActions[index];
if (action != null) action.Invoke();
}

/// <summary>Сколько иконок на текущем уровне (для журнала и проверок).</summary>
public int IconsOnLevel {
 get {
 return icons.Count; } }

/// <summary>
/// ПОСТАВИТЬ ПАНЕЛЬ ЗАНОВО (§37). Нужно после пробуждения шлема: сон мог пересоздать риг и
/// камеру, и «панель стоит там, где была до сна» превращается в «панель не там» или «панели
/// нет». Сбрасываем только отметки положения — иконки и уровень остаются как были.
/// </summary>
public void Reanchor()
{
            placed = false;
            handLogged = false;
            beltLogged = false;
            poseRejectedLogged = false;
            holdUntil = 0f;
}

/// <summary>
/// Сколько КОМАНД в раскрытом веере (без иконки-выхода в центре). Отдельное число, потому
/// что «иконок 14» в журнале путало: 13 команд группы плюс выход — это 14 иконок.
/// </summary>
public int FanCommandCount {
 get; private set; }

/// <summary>
/// ДЕРЖАТЬ ПАНЕЛЬ НА МЕСТЕ. Возвращает true, когда переставлять её НЕЛЬЗЯ.
/// 
/// ПРИЧИНА (найдено на живом шлеме 19.10.2026, слова владельца): «веер поворачивается за
/// камерой и из-за этого нет возможности выбирать иконку».
/// 
/// ПОЧЕМУ ТАК БЫЛО. Панель у пояса считается ОТ ГОЛОВЫ: направление и место берутся из
/// позы шлема. Пока оператор ведёт лучом к иконке, голова поворачивается — и панель
/// вместе с ней: цель уходит из-под луча быстрее, чем на неё наводятся, а нажатие требует
/// удержания. В веере (14 иконок на дуге 210°) это делало выбор практически невозможным.
/// 
/// ЧТО СЧИТАЕТСЯ «ДЕРЖАТЬ»:
///   1) на панели есть цель (луч навёл иконку) — и ещё полсекунды после того, как отвёл;

///   2) раскрыт веер — он обязан стоять, пока из него выбирают;

///   3) панель ещё в поле зрения (угол от направления взгляда меньше порога).
/// Во всех остальных случаях панель ставится заново — это и есть «запасное положение у
/// пояса»: опустил глаза — она там.
/// </summary>
        /// <summary>Есть ли в векторе NaN или бесконечность (отсев нечисловых данных, E-21).</summary>
        private static bool IsNotFinite(Vector3 v)
        {
            return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                   float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);
        }

private bool ShouldHoldStill()
{
            if (!placed) return false;
if (hovered != null)
{
                holdUntil = Time.unscaledTime + Mathf.Max(0f, holdAfterHoverSeconds);
return true;
}            if (level == KvXrPanelLevel.GroupFan) return true;
if (Time.unscaledTime < holdUntil) return true;

// Панель на виду — не дёргаем. Порог 55° выбран с запасом: у пояса панель и так
// висит на 29–33° ниже направления взгляда, и этот наклон не должен считаться
// «оператор ушёл».
Vector3 headPosition;
            Quaternion headRotation;
if (!KvXrInputSource.TryHeadPose(out headPosition, out headRotation)) return true;
            // ОТСЕВ НЕЧИСЕЛ (добавлено 30.10.2026, S44 — урок E-21).
            // Поза головы приходит от рантайма: при потере отслеживания там появляется NaN.
            // Тогда `toPanel.sqrMagnitude < 0.0001f` даёт false (проверка проходит),
            // `Vector3.Angle` даёт NaN, и `NaN <= предел` — тоже false: панель решила бы,
            // что оператор УШЁЛ. При нечисловых данных правильнее считать, что он на месте.
            if (IsNotFinite(headPosition)) return true;
Vector3 toPanel = transform.position - headPosition;
if (toPanel.sqrMagnitude < 0.0001f) return true;
float angle = Vector3.Angle(headRotation * Vector3.forward, toPanel);
return angle <= Mathf.Clamp(reanchorAngleDegrees, 20f, 120f);
}

// ==================================================== ПОСТРОЕНИЕ ОБЪЕКТОВ
private void CreateCanvas()
{
var canvasGo = new GameObject(KvXrOverlayPolicy.VrGroupCanvasName, typeof(Canvas),                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;
            canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(canvasSize, canvasSize);
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
float scale = Mathf.Max(0.0001f, metersPerCanvasUnit);
            canvasRect.localScale = new Vector3(scale, scale, scale);

// Подпись — одна на всю панель, показывается по удержанию.
label = KvTheme.CreateText(canvasRect, "PanelLabel", "", 30,                TextAnchor.MiddleCenter, KvTheme.TextMain);
            RectTransform lr = label.rectTransform;
            lr.anchorMin = new Vector2(0.5f, 0.5f);
            lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(canvasSize, 44f);

// Выше дуги: середина дуги — сверху, значит подпись уходит ещё выше иконок.
lr.anchoredPosition = new Vector2(0f, canvasSize * 0.5f - 30f);
            label.gameObject.SetActive(false);
}

/// <summary>
/// СОЗДАТЬ КНОПКУ. Подпись — либо иконка, либо ТЕКСТ (§41: цифры и буквы значками не нарисовать).
/// 
/// ЗДЕСЬ ЖЕ КНОПКА ПОПАДАЕТ В СПИСОК ЦЕЛЕЙ ЛУЧА. Это единственное место, где он пополняется:
/// поэтому «кнопка есть, а луч её не видит» (карточка Б50) повториться не может — нет способа
/// создать кнопку и не зарегистрировать её цель.
/// </summary>
private GameObject CreateIcon(string name, string iconId, string title, Vector2 offsetMeters,                                      UnityEngine.Events.UnityAction onClick, bool center = false,
string text = null, float widthMeters = 0f, float heightMeters = 0f,
bool rounded = false)
{
var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(canvasRect, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
float unit = Mathf.Max(0.0001f, metersPerCanvasUnit);
float width = widthMeters > 0f ? widthMeters : builtIconSize;
float height = heightMeters > 0f ? heightMeters : builtIconSize;
float widthPx = Mathf.Max(0.0001f, width) / unit;
float heightPx = Mathf.Max(0.0001f, height) / unit;
            rect.sizeDelta = new Vector2(widthPx, heightPx);
            rect.anchoredPosition = offsetMeters / unit;
var bg = go.GetComponent<Image>();
ApplyRounded(bg, rounded);
            bg.color = center                ? new Color(0.20f, 0.55f, 0.95f, 0.92f)                : new Color(0.16f, 0.16f, 0.18f, 0.88f);
            bg.raycastTarget = true;
if (!string.IsNullOrEmpty(text))
{
                Text keyLabel = KvTheme.CreateText(rect, "Key", text,                    Mathf.Clamp(Mathf.RoundToInt(heightPx * 0.55f), 8, 22),                    TextAnchor.MiddleCenter, KvTheme.TextMain);
                keyLabel.raycastTarget = false;
                keyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                keyLabel.verticalOverflow = VerticalWrapMode.Truncate;
                RectTransform kr = keyLabel.rectTransform;
                kr.anchorMin = Vector2.zero;
                kr.anchorMax = Vector2.one;
                kr.offsetMin = new Vector2(1f, 1f);
                kr.offsetMax = new Vector2(-1f, -1f);
}            else            {
                // Иконка поверх подложки: сама картинка, без текста.
var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(rect, false);
var icon = iconGo.GetComponent<Image>();
                icon.sprite = KvIcons.Get(string.IsNullOrEmpty(iconId) ? "info" : iconId, 32);
                icon.color = KvTheme.IconTint;
                icon.raycastTarget = false;
                RectTransform ir = icon.rectTransform;
                ir.anchorMin = Vector2.zero;
                ir.anchorMax = Vector2.one;
                ir.offsetMin = new Vector2(3f, 3f);
                ir.offsetMax = new Vector2(-3f, -3f);
if (icon.sprite == null) iconGo.SetActive(false);
}
var button = go.GetComponent<Button>();
if (onClick != null) button.onClick.AddListener(onClick);

// Наведение: луч указателя вызывает pointerEnter/pointerExit, иконка сообщает панели.
var hover = go.AddComponent<KvXrIconHover>();
            hover.Bind(this, title);
icons.Add(go);
hovers.Add(hover);
            iconActions.Add(onClick);
            iconTitles.Add(title);

// СРАЗУ В СПИСОК ЦЕЛЕЙ: иконка без цели — это кнопка, к которой «не приклеивается лазер»
// (именно так вышло с рядом экранов и с кнопками внутри экранов, 24.10.2026).
pendingOffsets.Add(offsetMeters);

// И РАЗМЕР ЦЕЛИ — ТОЖЕ СРАЗУ (§41, дефект Д1): попадание считается по нарисованному
// прямоугольнику, поэтому у широкой строки оно не 25 мм в центре, а во всю строку.
pendingHalfSizes.Add(new Vector2(width * 0.5f, height * 0.5f));
return go;
}

/// <summary>
/// ПОСТАВИТЬ ВЕРХНЮЮ НАДПИСЬ НАД САМЫМ ВЕРХНИМ РЯДОМ КНОПОК (указание владельца 24.10.2026:
/// «верхняя панель должна быть ниже — упираться в верхние кнопки, а не заезжать на них»).
/// Высота считается по фактическим целям (смещение + половина размера), поэтому на меню, на
/// экранах и на дуге надпись всегда стоит над верхним рядом, а не по постоянному числу.
/// </summary>
private void PlacePanelLabel()
{
            if (label == null) return;
float top = 0f;
for (int i = 0; i < currentOffsets.Length; i++)
{
float halfHeight = i < currentHalfSizes.Length ? currentHalfSizes[i].y : 0.02f;
float edge = currentOffsets[i].y + halfHeight;
if (edge > top) top = edge;
}
float unit = Mathf.Max(0.0001f, metersPerCanvasUnit);
            label.rectTransform.anchoredPosition = new Vector2(0f, (top + 0.035f) / unit);
}

/// <summary>
/// СМЕЩЕНИЯ ИНТЕРАКТИВНЫХ ИКОНОК, накопленные при построении текущего уровня. Порядок
/// совпадает с порядком `icons`/`hovers`/`iconActions`/`iconTitles`, поэтому `PressIndex`
/// по номеру попадания нажимает именно ту иконку, на которую навели луч.
/// </summary>
private readonly List<Vector2> pendingOffsets = new List<Vector2>();

/// <summary>
/// ПОЛОВИНЫ РАЗМЕРОВ ЦЕЛЕЙ, метры (§41). Пополняется там же, где смещения — в `CreateIcon`,
/// поэтому «кнопка без размера цели» так же невозможна, как «кнопка без цели» (дефект Д1).
/// </summary>
private readonly List<Vector2> pendingHalfSizes = new List<Vector2>();

/// <summary>Действующие половины размеров целей текущего уровня (для проверок и попадания).</summary>
private Vector2[] currentHalfSizes = new Vector2[0];
private void ClearIcons()
{
            for (int i = 0; i < icons.Count; i++)
{
                if (icons[i] == null) continue;
if (Application.isPlaying) Destroy(icons[i]);
                else DestroyImmediate(icons[i]);
}            icons.Clear();
hovers.Clear();
            iconActions.Clear();
            iconTitles.Clear();
pendingOffsets.Clear();

// РАЗМЕРЫ ЦЕЛЕЙ ЧИСТЯТСЯ ЗДЕСЬ ЖЕ (§41). Это не формальность: пока список не чистился,
// после смены уровня в нём оставались размеры прошлых иконок, длины списков расходились,
// и попадание молча возвращалось к старому кругу — то есть дефект Д1 «чинился» только на
// бумаге. Поймано тестом `WideButton_HitAnywhereOnIt`.
pendingHalfSizes.Clear();

// НАДПИСИ ЭКРАНОВ (§40) — отдельный список: они не участвуют в попадании лучом,
// но убирать их при пересборке обязательно, иначе текст копился бы слоями.
for (int i = 0; i < rows.Count; i++)
{
                if (rows[i] == null) continue;
if (Application.isPlaying) Destroy(rows[i]);
                else DestroyImmediate(rows[i]);
}            rows.Clear();
currentOffsets = new Vector2[0];
            currentHalfSizes = new Vector2[0];
hovered = null;
labelDwell = 0f;
labelLogged = false;
}

// ==================================================== ПОЛОЖЕНИЕ ПАНЕЛИ
/// <summary>
/// ПАНЕЛЬ У КОНТРОЛЛЕРА — ОСНОВНОЕ ПОЛОЖЕНИЕ (требование владельца [S1.2], «с первого дня»).
/// 
/// ОТКУДА БЕРЁТСЯ ПОЗА. Поза кисти (`devicePose`) и — если профиль её отдал — поза
/// указания (`pointer`), см. `KvXrInputSource.TryRightAimPose`. Панель ставится ВПЕРЁД
/// по направлению указания и разворачивается НА ОПЕРАТОРА (мировой канвас виден с
/// передней стороны; разворот «по взгляду» показал бы спину панели — этот дефект уже
/// стоил сессии 16.10.2026).
/// 
/// ПОЧЕМУ ВПЕРЁД, А НЕ «К ЛИЦУ». Прежняя строка выносила панель на 14 см ОТ кисти К лицу,
/// и панель оказывалась вплотную к глазам, а луч из того же контроллера шёл мимо неё.
/// Теперь панель висит перед контроллером (0,30 м) — её видно, подняв руку, и в неё
/// можно целиться тем же контроллером.
/// 
/// Позы руки нет — панель у ПОЯСА (запасное положение, прямо названо в журнале), а не
/// перед глазами: показ перед глазами владелец запретил прямо.
/// </summary>
private void PlacePanel()
{
            KvXrQuickHand hand = KvXrSettings.GetQuickPanelHand();
Vector3 handPosition;
            Quaternion handRotation;

// РЕЖИМ КИСТЕЙ (§40): контроллер отложен — панель крепится К ЛАДОНИ левой кисти.
// Раньше в этом режиме панель ехала за лежащим на столе контроллером, и владелец видел
// «квадратики появляются у левой руки» вместо панели у руки.
if (KvXrMode.For(hand) == KvXrControlMode.Hands)
{
                KvHandFrame frame;
Vector3 palmPosition;
Vector3 palmForward;
if (KvXrHandSource.TryGet(hand, out frame) &&                    KvXrHandPanelPlacement.TryGet(frame, out palmPosition, out palmForward))
{
                    transform.position = palmPosition;
Vector3 up = Vector3.up;
if (Mathf.Abs(Vector3.Dot(palmForward.normalized, up)) > 0.95f) up = Vector3.forward;
                    transform.rotation = Quaternion.LookRotation(palmForward.normalized, up);
PlacedAtHand = true;
placed = true;
                    lastHandPoseTime = Time.unscaledTime;
if (!handLogged)
{
                        handLogged = true;
                        beltLogged = false;
Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,                            "[KvXr] панель групп: встала У КИСТИ ({0} рука, контроллер отложен) · панель в {1} · " +                            "впереди ладони {2:0.00} м · иконок {3} · развёрнута ПО КИСТИ · {4}",                            hand == KvXrQuickHand.Right ? "правая" : "левая",                            palmPosition.ToString("0.00"), KvXrHandPanelPlacement.ForwardMeters, icons.Count,                            KvXrGroupPanelLayout.DescribeSizes(icons.Count,                                level == KvXrPanelLevel.Groups                                    ? KvXrGroupPanelLayout.GroupSpanDegrees                                    : KvXrGroupPanelLayout.FanSpanDegrees, RadiusMeters)));
}                    return;
}
}
bool right = hand == KvXrQuickHand.Right;
bool hasPose = right                ? KvXrInputSource.TryRightAimPose(out handPosition, out handRotation)                : KvXrInputSource.TryLeftAimPose(out handPosition, out handRotation);
if (hasPose && headCamera != null)
{
                lastHandPoseTime = Time.unscaledTime;
Vector3 headPosition;
                Quaternion headRotation;
if (!KvXrInputSource.TryHeadPose(out headPosition, out headRotation))
headPosition = headCamera.transform.position;
Vector3 forward = handRotation * Vector3.forward;
if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();

// ВЫНОС И ПОДЪЁМ — из файла подстройки (§37): числа, подобранные опросом, должны
// применяться на ходу, а не только через пересборку.
float forwardMeters = KvXrTuning.PanelForwardMeters;
float upMeters = KvXrTuning.PanelUpMeters;
Vector3 position = handPosition + forward * forwardMeters + Vector3.up * upMeters;

// ПОЗА, КОТОРОЙ НЕ ВЕРЯТ: слишком далеко от глаз. Так выглядит «нулевая» поза,
// которую ловушка [F2] пропускает как достоверную. Панель на полу в начале
// координат — это не «панель у руки», поэтому такая поза отвергается честно.
float roughDistance = Vector3.Distance(position, headPosition);
if (roughDistance > Mathf.Max(0.5f, handMaxDistanceFromEyes))
{
                    if (!poseRejectedLogged)
{
                        poseRejectedLogged = true;
Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,                            "[KvXr] панель групп: поза {0} руки НЕДОСТОВЕРНА (панель оказалась бы " +                            "в {1:0.00} м от глаз) — панель встанем у ПОЯСА, а не на полу",                            right ? "правой" : "левой", roughDistance));
}                    PlaceAtBelt(hand);
                    return;
}

// ЗАЩИТА ОТ «ПАНЕЛЬ В ЛИЦО»: при контроллере у лица панель иначе оказалась бы
// в 10–15 см от глаз. Отодвигаем её вдоль луча от головы, но НЕ поворачиваем
// и не меняем сторону — иначе панель уйдёт за спину контроллера.
Vector3 fromHead = position - headPosition;
float distanceFromEyes = fromHead.magnitude;
float minDistance = Mathf.Clamp(                    KvXrTuning.PanelMinDistanceEyesMeters, 0.10f, 1.20f);
if (distanceFromEyes < minDistance)
{
                    Vector3 push = distanceFromEyes > 0.0001f ? fromHead / distanceFromEyes : forward;
                    position = headPosition + push * minDistance;
                    distanceFromEyes = minDistance;
}                transform.position = position;
placed = true;

// ПАНЕЛЬ РАЗВОРАЧИВАЕТСЯ ПО РУКЕ, А НЕ ПО ЛИЦУ (требование владельца 21.10.2026:
// «надо, чтобы она смотрела всегда в сторону поворота руки, а не лица»).
//                // ЧТО БЫЛО: панель разворачивалась вектором «от глаз к панели» — то есть всегда
// косилась на оператора, и, поворачивая кисть, человек не мог повернуть меню.
//                // ЧТО СТАЛО: берётся ПОВОРОТ КОНТРОЛЛЕРА ЦЕЛИКОМ — и направление (вперёд по руке),
// и «верх» руки, поэтому крен тоже передаётся («как повернул руку — так и стоит»).
// Читаемая сторона при этом сохраняется: forward канваса по-прежнему смотрит ОТ
// оператора (иначе текст был бы зеркальным — дефект §35–§36).
Vector3 panelForward = forward;
if (panelForward.sqrMagnitude < 0.0001f)
{
                    Vector3 awayFromHead = position - headPosition;
                    panelForward = awayFromHead.sqrMagnitude > 0.0001f ? awayFromHead.normalized : Vector3.forward;
}                Vector3 panelUp = handRotation * Vector3.up;
if (Mathf.Abs(Vector3.Dot(panelForward.normalized, panelUp.normalized)) > 0.95f)
panelUp = Vector3.up;

// луч вверх/вниз: «верх» руки вырожден
transform.rotation = Quaternion.LookRotation(panelForward.normalized, panelUp.normalized);
PlacedAtHand = true;
if (!handLogged)
{
                    handLogged = true;
                    beltLogged = false;
float drop = headPosition.y - position.y;

// «ВПЕРЕДИ НА … М» СЧИТАЛОСЬ НЕВЕРНО (исправлено 21.10.2026): `Flatten` возвращает
// НОРМИРОВАННЫЙ вектор, поэтому его длина всегда 1,00 м, и в журнале стояло
// «впереди на 1.00 м» при реальных 0,30–0,35 м. Теперь берётся длина ДО нормировки.
Vector3 flat = position - headPosition;
                    flat.y = 0f;
float ahead = flat.magnitude;
float angle = KvXrGroupPanelLayout.AngleFromGazeDegrees(drop, ahead);
Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,                        "[KvXr] панель групп: встала У КОНТРОЛЛЕРА ({0} рука) · панель в {1} · " +                        "высота над полом {2:0.00} м · от глаз {3:0.00} м · впереди на {4:0.00} м · " +                        "{5:0}° от направления взгляда · иконок {6} · поза {7} · развёрнута ПО РУКЕ · " +                        "{8}",                        right ? "правая" : "левая",                        position.ToString("0.00"),                        position.y, distanceFromEyes, ahead, angle, icons.Count,                        KvXrInputSource.HasRightAim || KvXrInputSource.HasLeftAim ? "УКАЗАНИЯ" : "кисти",                        KvXrGroupPanelLayout.DescribeSizes(icons.Count,                            level == KvXrPanelLevel.Groups                                ? KvXrGroupPanelLayout.GroupSpanDegrees                                : KvXrGroupPanelLayout.FanSpanDegrees,                            RadiusMeters)));
}                return;
}

// ПОТЕРЯ ПОЗЫ РУКИ «ПРОЩАЕТСЯ» НА КОРОТКОЕ ВРЕМЯ. Иначе одно потерянное окно трекинга
// уводило бы панель к поясу и тут же возвращало обратно — рывок на ровном месте.
if (PlacedAtHand && Time.unscaledTime - lastHandPoseTime < handPoseGraceSeconds) return;
PlaceAtBelt(hand);
}

/// <summary>Сколько секунд панель держится на месте, если поза руки пропала (защита от рывка).</summary>
public float handPoseGraceSeconds = 0.60f;
private float lastHandPoseTime = -10f;

/// <summary>
/// ЗАПАСНОЕ ПОЛОЖЕНИЕ — У ПОЯСА. Панель крепится к телу: опускается ниже пояса и выносится
/// вперёд. Взгляд вперёд её не задевает (проверено числом в тестах), а чтобы найти —
/// достаточно опустить глаза.
/// </summary>
private void PlaceAtBelt(KvXrQuickHand hand)
{
            if (headCamera == null)
{
                if (canvas != null && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
                return;
}

// ГОЛОВА БЕРЁТСЯ У САМОГО ШЛЕМА, А НЕ У ОБЪЕКТА-КАМЕРЫ. Это исправление найдено
// по журналу 16.10.2026: проба ориентации восемь раз подряд показала «курс шлема 0.0°»,
// то есть у объекта, который считался камерой шлема, направление НЕ МЕНЯЛОСЬ. Значит
// панель ставилась относительно неподвижного объекта, а не относительно оператора:
// она оказалась в точке (0,00; −0,85; 0,45) — НИЖЕ ПОЛА, и владелец сказал «Пояса нет».
// Поза HMD из системы ввода — это и есть настоящая голова.
Vector3 head;
            Quaternion headRotation;
bool fromHmd = KvXrInputSource.TryHeadPose(out head, out headRotation);
if (!fromHmd)
{
                head = headCamera.transform.position;
                headRotation = headCamera.transform.rotation;
}

// ПАНЕЛЬ СТАВИТСЯ ОТ ПОЛА, А НЕ ОТ ВЫСОТЫ ГОЛОВЫ: тогда увести её под пол нечем,
// даже если высота головы придёт неверной.
float height = Mathf.Clamp(beltHeightMeters, 0.60f, 1.60f);
Vector3 flatForward = KvXrGroupPanelLayout.Flatten(headRotation * Vector3.forward);
Vector3 position = new Vector3(head.x, height, head.z)                             + flatForward * KvXrGroupPanelLayout.BeltForwardMeters;
            transform.position = position;

// ПАНЕЛЬ ОБЯЗАНА БЫТЬ ПОВЁРНУТА К ОПЕРАТОРУ, А НЕ ПО ЕГО ВЗГЛЯДУ.
// Прежняя строка разворачивала канвас ВДОЛЬ взгляда (`LookRotation(forward)`), то есть
// ЛИЦОМ ОТ оператора: у мирового канваса видна передняя сторона, и владелец смотрел
// на «спину» панели — отсюда «Пояса нет». Теперь панель разворачивается НА оператора.
// РАЗВОРОТ: forward канваса смотрит ОТ оператора. Это и есть читаемая сторона —
// у мирового канваса текст читается со стороны, куда forward НЕ направлен, а
// UI/Default рисует и обратную сторону (Cull Off), поэтому при развороте
// «на оператора» видны те же иконки, но НАДПИСЬ ЗЕРКАЛЬНАЯ. Именно это владелец
// и увидел 19.10.2026: «веер развёрнут ко мне спиной и текст тоже».
Vector3 look = KvXrGroupPanelLayout.Flatten(position - head);
if (look.sqrMagnitude < 0.0001f) look = flatForward;
            transform.rotation = Quaternion.LookRotation(look, Vector3.up);
PlacedAtHand = false;
placed = true;
if (!beltLogged)
{
                beltLogged = true;
                handLogged = false;
float drop = head.y - position.y;

// см. пояснение в PlacePanel: `Flatten` нормирует вектор, его длина всегда 1,00 м
Vector3 flatOffset = position - head;
                flatOffset.y = 0f;
float ahead = flatOffset.magnitude;
float realAngle = KvXrGroupPanelLayout.AngleFromGazeDegrees(drop, ahead);
Debug.Log("[KvXr] панель групп: позы контроллера НЕТ — панель у ПОЯСА (" +                          realAngle.ToString("0") +                          "° ниже направления взгляда, не перед глазами) · панель в " +                          position.ToString("0.00") + " · высота над полом " + position.y.ToString("0.00") +                          " м · впереди на " + ahead.ToString("0.00") + " м · от глаз " +                          (head - position).magnitude.ToString("0.00") +                          " м · голова из " + (fromHmd ? "ПОЗЫ ШЛЕМА" : "объекта-камеры") +                          " на высоте " + head.y.ToString("0.00") +                          " м · канвас " + (canvas != null && canvas.enabled ? "включён" : "ВЫКЛЮЧЕН") +                          " · иконок " + icons.Count + " · развёрнута НА оператора");
}
}
}}
