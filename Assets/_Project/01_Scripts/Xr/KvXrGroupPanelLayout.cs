using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>Уровень панели: обобщающие иконки групп или раскрытый веер одной группы.</summary>
    public enum KvXrPanelLevel
    {
        /// <summary>Уровень 0 — обобщающие иконки ВСЕХ групп, без названий.</summary>
        Groups = 0,

        /// <summary>Уровень 1 — веер «полумесяцем» из иконок ОДНОЙ группы, без названий.</summary>
        GroupFan = 1,

        /// <summary>
        /// ЭКРАН-ОКНО КОМПЬЮТЕРА (§42). Значение 16, а НЕ 11: значения 11–15 попадают в
        /// `IsScreen(value) => value >= StatusScreen`, и уровень строил бы экран панели у руки
        /// (радиус 0,11 м, бюджет 7 иконок) вместо окна 0,62 × 0,52 м. Окно должно быть ВЫШЕ уровня
        /// `StartMenu` (10), но НЕ считаться экраном панели — иначе ряд вкладок физически не влезает.
        /// Регистр «Desktops» из одного значения: задел на будущее без второй нумерации.
        /// </summary>
        DesktopWindow = 16,

        // ==================================================== ЭКРАНЫ (§40)
        //
        // «СРАЗУ ВСЁ ПЕРЕНЕСЁМ» — слова владельца. На компьютере четыре док-панели (дерево, свойства,
        // настройки, горячие клавиши); в шлеме они становятся ЭКРАНАМИ на той же панели у левой руки.

        /// <summary>Экран «Состояние»: что происходит прямо сейчас (только смотреть).</summary>
        StatusScreen = 2,

        /// <summary>Экран «Настройки»: значения со шагами «−/+» — пишут в те же настройки, что и компьютер.</summary>
        SettingsScreen = 3,

        /// <summary>Экран «Объекты»: список объектов сцены, выбор строки.</summary>
        ObjectsScreen = 4,

        /// <summary>Экран «Свойства»: свойства выбранного объекта (только просмотр).</summary>
        PropertiesScreen = 5,

        /// <summary>Экран «Справка»: что делают жесты, кнопки и второй луч.</summary>
        HelpScreen = 6,

        /// <summary>
        /// ЭКРАН «ВВОД» (§41): клавиатура или колесо символов и строка-кандидат. Открывается из
        /// «Настроек» (числа), «Свойств» (имя объекта) и «Поиска» (строка запроса).
        /// Новые значения добавляются В КОНЕЦ: прежние 0–6 не перенумеровываются, иначе поехали бы
        /// уже запускавшиеся тесты (`KvXrPanelTargetTests` перебирает экраны по именам, но номера
        /// участвуют в сравнениях и в журнале).
        /// </summary>
        InputScreen = 7,

        /// <summary>ЭКРАН «ПОИСК» (§41): ввод строки и выполнение найденной команды.</summary>
        SearchScreen = 8,

        /// <summary>
        /// ЭКРАН «ИНТЕРФЕЙС» (§41): числовые настройки КОМПЬЮТЕРА — масштаб, плотность, рядов панели,
        /// раскладка, шрифт, цветокоррекция. Каждую можно ввести руками теми же тремя способами.
        ///
        /// Почему отдельный экран, а не седьмая строка «Настроек»: состав строк «Настроек» закреплён
        /// уже запускавшимся тестом §40 (`Screen_SettingsRowOrderIsFixed` — «заголовок + шесть
        /// параметров»). Добавление строки сломало бы чужой тест, а править его нельзя.
        /// </summary>
        InterfaceScreen = 9,

        /// <summary>
        /// СТАРТОВОЕ МЕНЮ (§41, указание владельца 24.10.2026: «нет стартового меню — сначала
        /// сделаем его, похожим на настройки PICO»). Это не экран панели у руки, а отдельная
        /// панель перед оператором: слева разделы, справа строки — раскладка в `KvXrStartMenuLayout`.
        /// </summary>
        StartMenu = 10,

        // ==================================================== ЭКРАНЫ-ОКНА КОМПЬЮТЕРА (§42)
        //
        // Решение владельца 25.10.2026 (ответ на вопрос В1 плана §42): у каждого окна компьютера,
        // которое открывает иконка веера, — СВОЙ экран в шлеме. Пять новых экранов закрывают все
        // 50 команд-окон; остальные шесть экранов уже приняты (2–6, 8–10).
        //
        // Значения добавлены В КОНЕЦ по тому же правилу, что и раньше: прежние 0–10 не
        // перенумеровываются, иначе поехали бы уже запускавшиеся тесты и строки журнала.

        /// <summary>ЭКРАН «НАСТРОЙКИ ПК» (§42): 6 вкладок окна настроек компьютера.</summary>
        DesktopSettingsScreen = 11,

        /// <summary>ЭКРАН «ПАНЕЛЬ ФУНКЦИЙ» (§42): 10 вкладок панели функций компьютера.</summary>
        FeaturesScreen = 12,

        /// <summary>ЭКРАН «ВЕРСТАК» (§42): 23 вкладки верстака компьютера.</summary>
        WorkbenchScreen = 13,

        /// <summary>ЭКРАН «ГОРЯЧИЕ КЛАВИШИ» (§42): список клавиш из реестра компьютера.</summary>
        HotkeysScreen = 14,

        /// <summary>ЭКРАН «ВИРТУАЛЬНЫЙ ПУЛЬТ» (§42): режим, робот и шаг перемещения.</summary>
        PendantScreen = 15

        // ПРИМЕЧАНИЕ (§42). Уровень «окно компьютера» — это `DesktopWindow = 16` выше, рядом с
        // уровнями 0 и 1: окно строится как ОКНО (плита 0,62 × 0,52 м, ряд вкладок), а не как экран
        // панели у руки. Пять экранов 11–15 остались объявленными для таблицы соответствия
        // (`KvXrDesktopWindows`) — по ним видно, к какому экрану относится каждая команда-окно.
    }
    /// <summary>
    /// ГЕОМЕТРИЯ ПАНЕЛИ С ОБОБЩАЮЩИМИ ИКОНКАМИ ГРУПП (сессия §35, карточка Б19).
    ///
    /// РЕШЕНИЕ ВЛАДЕЛЬЦА (16.10.2026). Панель инструментов должна быть **у руки (у контроллера)**,
    /// а не висеть в середине экрана. Основные кнопки — ОБОБЩАЮЩИЕ ИКОНКИ ГРУПП, не текст.
    /// Текст появляется только после наведения и удержания фокуса **0,5 секунды**. После нажатия
    /// на кнопку группы **полумесяцем раскрываются ВСЕ иконки этой группы, без названий**.
    ///
    /// ПОЧЕМУ ОТДЕЛЬНЫЙ ТИП, А НЕ ПРАВКА `KvXrQuickPanelLayout`. У прежней панели предел —
    /// 12 кнопок (`KvXrQuickPanelLayout.MaxButtons`), и это закреплено уже запускавшимися тестами
    /// (`KvXrOverlayTests`: `IsValidCount(12)` верно, `IsValidCount(13)` неверно). Владелец же
    /// потребовал раскрывать ВСЕ иконки группы, а в самой большой группе («Визуализация») их 14.
    /// Поднимать предел у прежнего типа значило бы сломать чужие тесты — запрет [S3.8]. Поэтому
    /// у панели групп свой предел, а прежний тип и его тесты остаются как были.
    ///
    /// ЧТО ЗДЕСЬ ПРОВЕРЯЕМО. Углы, радиусы, попадание лучом и правило показа подписи — чистая
    /// арифметика без GameObject, кадра и состояния. Всё, что связано с построением кнопок,
    /// живёт в `KvXrQuickPanel`, и логики там как можно меньше.
    ///
    /// СИСТЕМА КООРДИНАТ: углы в градусах, 0° — вправо, 90° — вверх, отсчёт против часовой
    /// стрелки. 0° соответствует положительной оси X плоскости панели.
    /// </summary>
    public static class KvXrGroupPanelLayout
    {
        // ==================================================== ПРЕДЕЛЫ И РАЗМЕРЫ

        /// <summary>
        /// ПРЕДЕЛ ИКОНОК В ВЕЕРЕ. 14 — это размер самой большой группы проекта («Визуализация»),
        /// а не круглое число «на глаз»: веер обязан вмещать ЛЮБУЮ группу целиком, иначе часть
        /// команд пропадёт из шлема молча.
        /// </summary>
        public const int MaxFanIcons = 14;

        /// <summary>Меньше одной иконки — это не веер.</summary>
        public const int MinFanIcons = 1;

        /// <summary>Сколько групп показывается на уровне 0 (каталог `KvToolbarGroups`, 8 групп).</summary>
        public const int GroupCount = 8;

        /// <summary>Размер иконки, метры (4,2 см — уверенно читается с 40 см и попадается лучом).</summary>
        public const float IconSizeMeters = 0.042f;

        /// <summary>Наименьший просвет между иконками, метры: иконки не должны налезать друг на друга.</summary>
        public const float MinGapMeters = 0.012f;

        /// <summary>Наименьший радиус полумесяца, метры.</summary>
        public const float MinRadiusMeters = 0.14f;

        /// <summary>Наибольший радиус полумесяца, метры (дальше приходится тянуться).</summary>
        public const float MaxRadiusMeters = 0.42f;

        /// <summary>Дуга уровня 0 — 160°: восемь групп помещаются, не закрывая обзор.</summary>
        public const float GroupSpanDegrees = 160f;

        /// <summary>Дуга веера — 210°: столько нужно, чтобы 14 иконок не налезали друг на друга.</summary>
        public const float FanSpanDegrees = 210f;

        /// <summary>Направление «вверх» в градусах: середина дуги всегда сверху.</summary>
        public const float UpDegrees = 90f;

        /// <summary>
        /// ПОРОГ ПОДПИСИ — 0,5 СЕКУНДЫ. Решение владельца: «текст появляется только после
        /// наведения и удержания фокуса 0,5 секунды». Подпись у всех кнопок сразу превратила бы
        /// панель обратно в текст, который владелец запретил.
        /// </summary>
        public const float LabelDwellSeconds = 0.5f;

        /// <summary>Сколько иконок считается «наведённой» по расстоянию от луча, метры.</summary>
        public const float HitRadiusMeters = IconSizeMeters * 0.85f;

        // ==================================================== РАДИУС ПАНЕЛИ — НАСТРОЙКА (§37)
        //
        // ПОЧЕМУ ЭТО ПОЯВИЛОСЬ (задание владельца [S1.2]). До §37 радиус панели был НЕ настройкой,
        // а расчётом от константы: RadiusForCount(8, 160°) = 8 × (0.042 + 0.012) / 2.79 = 0.155 м.
        // Владелец сказал: «полукруг занимает много места перед лицом, иконки расположены шире,
        // чем мне нужно» — и уменьшить это было нечем.
        //
        // РЕШЕНИЕ ВЛАДЕЛЬЦА (опрос §37, до начала работ): вариант 3 — радиус 0.100 м,
        // иконка 27 мм, просвет 8 мм. Дальше — опросы «больше/меньше/норма» по журналу.
        //
        // ИКОНКИ УМЕНЬШАЮТСЯ ВМЕСТЕ С РАДИУСОМ, НО НЕ СЛИПАЮТСЯ: просвет считается долей
        // бюджета дуги, поэтому соседние иконки всегда расходятся на «иконка + просвет».

        /// <summary>
        /// Радиус панели по умолчанию, метры.
        ///
        /// ПЕРВЫЙ ВЫБОР ВЛАДЕЛЬЦА (опрос §37, до работ): 0,100 м. После первого взгляда в шлем
        /// (21.10.2026) владелец сказал: «панель стала меньше — отлично, давай увеличим иконки до
        /// 30 мм». 30 мм иконки и радиус связаны формулой: при 0,100 м просвет падает до 5 мм и
        /// иконки почти слипаются, поэтому радиус — 0,110 м (иконка 30 мм, просвет 8,4 мм).
        /// </summary>
        public const float DefaultPanelRadiusMeters = 0.110f;

        /// <summary>Нижняя граница радиуса: ближе — иконки уже не разложить без слияния.</summary>
        public const float MinPanelRadiusMeters = 0.080f;

        /// <summary>Верхняя граница радиуса: дальше приходится тянуться рукой.</summary>
        public const float MaxPanelRadiusMeters = 0.420f;

        /// <summary>Ниже этого иконка не читается вовсе.</summary>
        public const float MinIconSizeMeters = 0.016f;

        /// <summary>В веере иконка не мельче этого: 14 иконок обязаны остаться различимыми.</summary>
        public const float MinFanIconSizeMeters = 0.024f;

        /// <summary>Больше этого иконка не растёт: на большом радиусе хватает просвета.</summary>
        public const float MaxIconSizeMeters = 0.060f;

        /// <summary>Наименьший просвет при малом радиусе, метры (8 мм — просвет варианта 3).</summary>
        public const float MinGapForSmallIcons = 0.008f;

        /// <summary>Наибольший просвет: дальше иконки выглядят «разбежавшимися».</summary>
        public const float MaxGapMeters = 0.012f;

        /// <summary>Какая доля бюджета дуги уходит в просвет между соседними иконками.</summary>
        public const float GapRatio = 0.22f;

        /// <summary>Радиус панели, приведённый к допустимому диапазону.</summary>
        public static float ClampPanelRadius(float meters)
        {
            if (float.IsNaN(meters) || float.IsInfinity(meters)) return DefaultPanelRadiusMeters;
            return Mathf.Clamp(meters, MinPanelRadiusMeters, MaxPanelRadiusMeters);
        }

        /// <summary>Сколько метров дуги приходится на одну иконку при данном радиусе.</summary>
        public static float IconBudgetMeters(int count, float spanDegrees, float radiusMeters)
        {
            int n = Mathf.Max(1, count);
            float spanRad = Mathf.Max(0.0001f, spanDegrees) * Mathf.Deg2Rad;
            return spanRad * ClampPanelRadius(radiusMeters) / n;
        }

        /// <summary>Просвет между соседними иконками при данном радиусе, метры.</summary>
        public static float GapForCount(int count, float spanDegrees, float radiusMeters)
        {
            float budget = IconBudgetMeters(count, spanDegrees, radiusMeters);
            return Mathf.Clamp(budget * GapRatio, MinGapForSmallIcons, MaxGapMeters);
        }

        /// <summary>
        /// РАЗМЕР ИКОНКИ ПРИ ДАННОМ РАДИУСЕ, метры. Иконка занимает бюджет дуги за вычетом
        /// просвета — поэтому уменьшение радиуса уменьшает и иконки, но они не слипаются.
        /// </summary>
        public static float IconSizeForCount(int count, float spanDegrees, float radiusMeters)
        {
            float budget = IconBudgetMeters(count, spanDegrees, radiusMeters);
            float gap = GapForCount(count, spanDegrees, radiusMeters);
            return Mathf.Clamp(budget - gap, MinIconSizeMeters, MaxIconSizeMeters);
        }

        /// <summary>
        /// КАКОЙ РАДИУС НУЖЕН, ЧТОБЫ ИКОНКА БЫЛА НЕ МЕНЬШЕ ЗАДАННОЙ. Применяется к вееру:
        /// 14 иконок «Визуализации» на радиусе групп дали бы 18 мм — это уже не читается.
        /// </summary>
        public static float RadiusForIconSize(int count, float spanDegrees, float iconSizeMeters)
        {
            int n = Mathf.Max(1, count);
            float spanRad = Mathf.Max(0.0001f, spanDegrees) * Mathf.Deg2Rad;
            float size = Mathf.Clamp(iconSizeMeters, MinIconSizeMeters, MaxIconSizeMeters);
            return ClampPanelRadius(n * (size + MinGapForSmallIcons) / spanRad);
        }

        /// <summary>
        /// РАДИУС ВЕЕРА: не меньше радиуса групп и не меньше, чем нужно для читаемых иконок.
        /// Уровень групп при этом остаётся на выбранном владельцем радиусе.
        /// </summary>
        public static float FanRadiusMeters(int iconCount, float groupRadiusMeters)
        {
            float needed = RadiusForIconSize(iconCount, FanSpanDegrees, MinFanIconSizeMeters);
            return Mathf.Max(ClampPanelRadius(groupRadiusMeters), needed);
        }

        /// <summary>Расстояние между соседними иконками на дуге (хорда), метры.</summary>
        public static float NeighbourDistanceMeters(int count, float spanDegrees, float radiusMeters)
        {
            int n = Mathf.Max(1, count);
            if (n == 1) return float.MaxValue;
            float stepRad = Mathf.Max(0.0001f, spanDegrees) / (n - 1) * Mathf.Deg2Rad;
            return 2f * ClampPanelRadius(radiusMeters) * Mathf.Sin(stepRad * 0.5f);
        }

        /// <summary>
        /// НЕ СЛИПАЮТСЯ ЛИ ИКОНКИ: расстояние между соседями больше, чем иконка плюс просвет.
        /// Требование владельца дословно: «важно, чтобы они не сливались и оставались видимыми
        /// для луча».
        /// </summary>
        public static bool IconsDoNotOverlap(int count, float spanDegrees, float radiusMeters)
        {
            if (count < 2) return true;
            float icon = IconSizeForCount(count, spanDegrees, radiusMeters);
            float gap = GapForCount(count, spanDegrees, radiusMeters);
            float distance = NeighbourDistanceMeters(count, spanDegrees, radiusMeters);
            return distance + 1e-4f >= icon + gap;
        }

        /// <summary>
        /// РАСКЛАДКА УРОВНЯ 0 ОТ ЗАДАННОГО РАДИУСА. Прежняя сигнатура (`GroupOffsets(hand)`)
        /// остаётся как была — на ней запускались тесты §35–§36.
        /// </summary>
        public static Vector2[] GroupOffsetsForRadius(int groupCount, KvXrQuickHand hand, float radiusMeters)
        {
            int n = groupCount < 1 ? 1 : groupCount;
            float radius = ClampPanelRadius(radiusMeters);
            var result = new Vector2[n];
            for (int i = 0; i < n; i++)
                result[i] = OffsetMeters(i, n, GroupSpanDegrees, radius, hand);
            return result;
        }

        /// <summary>Раскладка веера от заданного радиуса (число иконок — как есть).</summary>
        public static Vector2[] FanOffsetsForRadius(int iconCount, KvXrQuickHand hand, float radiusMeters)
        {
            int n = ClampFanCount(iconCount);
            float radius = FanRadiusMeters(n, radiusMeters);
            var result = new Vector2[n];
            for (int i = 0; i < n; i++)
                result[i] = OffsetMeters(i, n, FanSpanDegrees, radius, hand);
            return result;
        }

        /// <summary>
        /// РАСКЛАДКА ВЕЕРА ВМЕСТЕ С ЦЕНТРАЛЬНОЙ ИКОНКОЙ (исправлено 21.10.2026).
        ///
        /// ПОЧЕМУ ОТДЕЛЬНЫЙ МЕТОД. Центральная иконка («выход» из веера) рисуется в центре панели,
        /// то есть со смещением (0,0). В списке попадания её не было — и луч физически не мог в неё
        /// попасть. Владелец сформулировал это как «не работает кнопка перехода на первый слой».
        /// Теперь центр входит в список попадания ПОСЛЕДНИМ — его номер совпадает с номером иконки
        /// в панели, поэтому нажатие вызывает именно выход.
        /// </summary>
        public static Vector2[] FanHitOffsets(int iconCount, KvXrQuickHand hand, float radiusMeters)
        {
            Vector2[] icons = FanOffsetsForRadius(iconCount, hand, radiusMeters);
            var result = new Vector2[icons.Length + 1];
            for (int i = 0; i < icons.Length; i++) result[i] = icons[i];
            result[icons.Length] = Vector2.zero;
            return result;
        }

        /// <summary>
        /// СТРОКА ЧИСЕЛ ДЛЯ ЖУРНАЛА: радиус, иконка и просвет. Без неё «стало меньше» — это слова,
        /// а не доказательство ([S3.7]).
        /// </summary>
        public static string DescribeSizes(int count, float spanDegrees, float radiusMeters)
        {
            float radius = ClampPanelRadius(radiusMeters);
            float icon = IconSizeForCount(count, spanDegrees, radius);
            float gap = GapForCount(count, spanDegrees, radius);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "радиус {0:0.000} м · иконка {1:0} мм · просвет {2:0.0} мм",
                radius, icon * 1000f, gap * 1000f);
        }

        // ==================================================== ПАНЕЛЬ У ПОЯСА (запасное положение)

        /// <summary>
        /// НАСКОЛЬКО ПАНЕЛЬ ОПУСКАЕТСЯ НИЖЕ ПОЯСА, метры. Запасное положение — «у пояса»
        /// (решение владельца), а НЕ перед глазами: показ перед глазами владелец запретил прямо.
        ///
        /// ИСПРАВЛЕНО 16.10.2026 ПО ЖИВОМУ ШЛЕМУ. Владелец: «Пояса нет». Прежние значения
        /// (пояс 1,00 м минус опускание 0,42 м) давали панель на высоте **0,58 м** и всего
        /// в 16 см перед телом — это 81° вниз от направления взгляда, то есть практически
        /// ПОД ПОДБОРОДКОМ, у самых ног. Такую панель не находит никто. Теперь панель встаёт
        /// на высоте ≈1,05 м и в 45 см впереди: около 51° вниз — видно, опустив глаза,
        /// и при этом вне центрального поля зрения, как владелец и требовал.
        /// </summary>
        public const float BeltDropMeters = 0.25f;

        /// <summary>
        /// Насколько панель выносится вперёд от тела в запасном положении, метры. Полметра —
        /// чтобы панель не оказалась вплотную к корпусу: при прежних 16 см она была практически
        /// внутри оператора и не читалась.
        /// </summary>
        public const float BeltForwardMeters = 0.45f;

        /// <summary>
        /// НИЖНЯЯ ГРАНИЦА УГЛА ОТ ВЗГЛЯДА для запасного положения, градусы. Панель обязана быть
        /// ВНЕ центрального поля зрения; 25° — заметно ниже направления взгляда, и при этом
        /// панель находится одним движением глаз вниз.
        /// </summary>
        public const float BeltMinAngleFromGazeDegrees = 25f;

        // ==================================================== РАСКЛАДКА

        /// <summary>Число иконок в веере, приведённое к допустимому.</summary>
        public static int ClampFanCount(int count)
        {
            if (count < MinFanIcons) return MinFanIcons;
            if (count > MaxFanIcons) return MaxFanIcons;
            return count;
        }

        /// <summary>Допустимое ли число иконок в веере.</summary>
        public static bool IsValidFanCount(int count)
        {
            return count >= MinFanIcons && count <= MaxFanIcons;
        }

        /// <summary>
        /// РАДИУС ПОЛУМЕСЯЦА ПО ЧИСЛУ ИКОНОК. Считается так, чтобы соседние иконки не налезали
        /// друг на друга: дуга обязана вместить `число × (иконка + просвет)`. Радиус «на глаз»
        /// дал бы при 14 иконках слипшийся комок — а именно 14 в самой большой группе.
        /// </summary>
        public static float RadiusForCount(int count, float spanDegrees)
        {
            int n = ClampFanCount(count);
            float spanRad = Mathf.Max(0.0001f, spanDegrees) * Mathf.Deg2Rad;
            float needed = n * (IconSizeMeters + MinGapMeters) / spanRad;
            return Mathf.Clamp(needed, MinRadiusMeters, MaxRadiusMeters);
        }

        /// <summary>
        /// УГОЛ ИКОНКИ. Середина дуги — сверху; иконки идут слева направо, поэтому первая
        /// оказывается слева, а последняя справа (порядок чтения совпадает с порядком групп).
        /// </summary>
        public static float AngleDegrees(int index, int count, float spanDegrees)
        {
            int n = ClampFanCount(count);
            int i = ((index % n) + n) % n;                  // индекс вне диапазона заворачивается
            if (n == 1) return UpDegrees;
            float t = (float)i / (n - 1);                   // 0 … 1
            return UpDegrees + spanDegrees * 0.5f - t * spanDegrees;
        }

        /// <summary>Смещение иконки от центра панели в её плоскости, метры.</summary>
        public static Vector2 OffsetMeters(int index, int count, float spanDegrees, float radiusMeters)
        {
            float a = AngleDegrees(index, count, spanDegrees) * Mathf.Deg2Rad;
            // §37: ограничение берётся из НОВОГО диапазона (0.080…0.420), иначе выбранный владельцем
            // радиус 0.100 м молча поднимался бы до прежнего предела 0.14 м и «уменьшение» не работало бы.
            float r = ClampPanelRadius(radiusMeters);
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        /// <summary>
        /// СМЕЩЕНИЕ С УЧЁТОМ РУКИ. У правой руки панель зеркалится по горизонтали: иначе
        /// полумесяц уходил бы в сторону от оператора, а не под ладонь.
        /// </summary>
        public static Vector2 OffsetMeters(int index, int count, float spanDegrees,
                                           float radiusMeters, KvXrQuickHand hand)
        {
            Vector2 local = OffsetMeters(index, count, spanDegrees, radiusMeters);
            if (hand != KvXrQuickHand.Right) return local;
            return new Vector2(-local.x, local.y);
        }

        /// <summary>
        /// РАСКЛАДКА УРОВНЯ 0: обобщающие иконки всех групп. Радиус считается по числу групп.
        /// </summary>
        public static Vector2[] GroupOffsets(KvXrQuickHand hand, int groupCount = GroupCount)
        {
            int n = groupCount < 1 ? 1 : groupCount;
            float radius = RadiusForCount(n, GroupSpanDegrees);
            var result = new Vector2[n];
            for (int i = 0; i < n; i++)
                result[i] = OffsetMeters(i, n, GroupSpanDegrees, radius, hand);
            return result;
        }

        /// <summary>
        /// РАСКЛАДКА УРОВНЯ 1: веер «полумесяцем» из ВСЕХ иконок одной группы. Радиус растёт
        /// с числом иконок, поэтому 14 иконок «Визуализации» не слипаются в комок.
        /// </summary>
        public static Vector2[] FanOffsets(int iconCount, KvXrQuickHand hand)
        {
            int n = ClampFanCount(iconCount);
            float radius = RadiusForCount(n, FanSpanDegrees);
            var result = new Vector2[n];
            for (int i = 0; i < n; i++)
                result[i] = OffsetMeters(i, n, FanSpanDegrees, radius, hand);
            return result;
        }

        /// <summary>
        /// ПОПАДАНИЕ ЛУЧОМ: номер ближайшей иконки или −1, если луч ни на одну не наведён.
        /// Возврат −1 (а не «нулевая иконка») важен: иначе нажатие «в пустоту» срабатывало бы
        /// по первой иконке, и владелец увидел бы, что панель нажимается сама.
        /// </summary>
        public static int ClosestIndex(Vector2 point, Vector2[] offsets, float maxDistanceMeters)
        {
            if (offsets == null || offsets.Length == 0) return -1;
            float limit = maxDistanceMeters <= 0f ? HitRadiusMeters : maxDistanceMeters;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < offsets.Length; i++)
            {
                float d = Vector2.Distance(point, offsets[i]);
                if (d <= limit && d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        // ==================================================== ПОПАДАНИЕ ЛУЧОМ (геометрия)

        /// <summary>
        /// ПЕРЕСЕЧЕНИЕ ЛУЧА С ПЛОСКОСТЬЮ ПАНЕЛИ — точка в ОСЯХ ПАНЕЛИ, метры.
        ///
        /// ЗАЧЕМ ЭТО ВООБЩЕ (сессия §36, дефект «подпись и веер в шлеме не проверить»).
        /// Прежний указатель отдавал мировую точку в `GraphicRaycaster`, а uGUI трактует её
        /// как ЭКРАННЫЕ ПИКСЕЛИ (`GraphicRaycaster.cs:184` → `ScreenPointToRay`), поэтому луч
        /// уходил в угол экрана вместо иконки. Здесь та же задача решена честной геометрией:
        /// плоскость панели известна, луч известен, точка пересечения считается в метрах и
        /// проверяется тестом без шлема.
        ///
        /// ЗНАК: `panelNormal` — нормаль ПЕРЕДНЕЙ стороны панели (та, что видит оператор).
        /// Луч, идущий от оператора К панели, даёт положительную дальность.
        /// </summary>
        public static bool TryRayToPanelPlane(Vector3 rayOrigin, Vector3 rayDirection,
                                              Vector3 panelOrigin, Vector3 panelRight, Vector3 panelUp,
                                              Vector3 panelNormal,
                                              out Vector2 pointMeters, out float distanceAlongRayMeters)
        {
            pointMeters = Vector2.zero;
            distanceAlongRayMeters = 0f;

            if (panelNormal.sqrMagnitude < 1e-8f) return false;
            if (rayDirection.sqrMagnitude < 1e-8f) return false;
            if (panelRight.sqrMagnitude < 1e-8f || panelUp.sqrMagnitude < 1e-8f) return false;

            Vector3 normal = panelNormal.normalized;
            Vector3 direction = rayDirection.normalized;

            float denominator = Vector3.Dot(direction, normal);
            // Луч параллелен плоскости: пересечения нет (или оно «в бесконечности»).
            if (Mathf.Abs(denominator) < 1e-3f) return false;

            float distance = Vector3.Dot(panelOrigin - rayOrigin, normal) / denominator;
            // Панель ЗА спиной луча — попадания нет. Возврат «попадания» здесь нажал бы
            // иконку, на которую оператор не смотрит.
            if (distance <= 0f) return false;

            Vector3 point = rayOrigin + direction * distance;
            Vector3 delta = point - panelOrigin;
            pointMeters = new Vector2(Vector3.Dot(delta, panelRight.normalized),
                                      Vector3.Dot(delta, panelUp.normalized));
            distanceAlongRayMeters = distance;
            return true;
        }

        /// <summary>
        /// ПОПАДАНИЕ В ИКОНКУ: номер ближайшей иконки (или −1). Дополнительно возвращает
        /// расстояние от центра иконки и дальность вдоль луча — обе в метрах: по ним пишется
        /// строка журнала, по которой видно, ЧЕМ наводились и НАСКОЛЬКО точно попали.
        /// </summary>
        public static bool TryHitIcon(Vector3 rayOrigin, Vector3 rayDirection,
                                      Vector3 panelOrigin, Vector3 panelRight, Vector3 panelUp,
                                      Vector3 panelNormal,
                                      Vector2[] iconOffsetsMeters,
                                      out int index, out float distanceFromCenterMeters,
                                      out float distanceAlongRayMeters,
                                      float hitRadiusMeters = HitRadiusMeters)
        {
            index = -1;
            distanceFromCenterMeters = 0f;

            Vector2 pointMeters;
            if (!TryRayToPanelPlane(rayOrigin, rayDirection, panelOrigin, panelRight, panelUp,
                                    panelNormal, out pointMeters, out distanceAlongRayMeters))
                return false;

            if (iconOffsetsMeters == null || iconOffsetsMeters.Length == 0) return false;

            // §37: РАДИУС ПОПАДАНИЯ СООТВЕТСТВУЕТ РАЗМЕРУ ИКОНКИ. При уменьшенных иконках (27 мм)
            // прежние 3.6 см «попадания» захватывали бы и промежутки между иконками, и нажатие
            // срабатывало бы по соседней — владелец увидел бы «панель нажимается не туда».
            index = ClosestIndex(pointMeters, iconOffsetsMeters, hitRadiusMeters);
            if (index < 0) return false;

            distanceFromCenterMeters = Vector2.Distance(pointMeters, iconOffsetsMeters[index]);
            return true;
        }

        /// <summary>
        /// ПОПАДАНИЕ ПО ВСЕЙ ПОКАЗАННОЙ КНОПКЕ, А НЕ ПО КРУГУ В ЕЁ ЦЕНТРЕ (дефект Д1, найден
        /// 24.10.2026 по журналу приёмки §41).
        ///
        /// ЧТО БЫЛО. Попадание считалось кругом радиусом 0,85 × размер иконки. У обычной иконки
        /// 30 мм это 25,5 мм — совпадает с картинкой, и всё работало. Но кнопки, нарисованные ШИРЕ
        /// (строка 100–360 мм), ловились тем же кругом 25 мм: владелец видел кнопку во всю панель,
        /// целился в её край и не попадал. В журнале это выглядело так: наведение «Ввести значение»
        /// зафиксировано на 2,4 см при радиусе 2,55 см, а строки шириной 360 мм не наведены ни разу.
        ///
        /// ЧТО ТЕПЕРЬ. У каждой цели есть СВОЙ прямоугольник (половина ширины и половина высоты),
        /// и попадание ищется по нему; старый круг остаётся запасным путём — поэтому чувствительность
        /// прежних иконок НЕ уменьшилась (это важно: владелец принимал именно её).
        /// Из нескольких прямоугольников выбирается ближайший к точке, иначе луч нажимал бы соседнюю
        /// строку.
        /// </summary>
        public static bool TryHitTarget(Vector3 rayOrigin, Vector3 rayDirection,
                                        Vector3 panelOrigin, Vector3 panelRight, Vector3 panelUp,
                                        Vector3 panelNormal,
                                        Vector2[] iconOffsetsMeters, Vector2[] iconHalfSizesMeters,
                                        out int index, out float distanceFromCenterMeters,
                                        out float distanceAlongRayMeters,
                                        float hitRadiusMeters = HitRadiusMeters)
        {
            index = -1;
            distanceFromCenterMeters = 0f;
            distanceAlongRayMeters = 0f;

            Vector2 pointMeters;
            if (!TryRayToPanelPlane(rayOrigin, rayDirection, panelOrigin, panelRight, panelUp,
                                    panelNormal, out pointMeters, out distanceAlongRayMeters))
                return false;

            if (iconOffsetsMeters == null || iconOffsetsMeters.Length == 0) return false;

            if (iconHalfSizesMeters != null && iconHalfSizesMeters.Length == iconOffsetsMeters.Length)
            {
                float best = float.MaxValue;
                for (int i = 0; i < iconOffsetsMeters.Length; i++)
                {
                    Vector2 delta = pointMeters - iconOffsetsMeters[i];
                    Vector2 half = iconHalfSizesMeters[i];
                    if (half.x <= 0f || half.y <= 0f) continue;
                    if (Mathf.Abs(delta.x) > half.x || Mathf.Abs(delta.y) > half.y) continue;

                    float distance = delta.sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        index = i;
                    }
                }

                if (index >= 0)
                {
                    distanceFromCenterMeters = Vector2.Distance(pointMeters, iconOffsetsMeters[index]);
                    return true;
                }
            }

            // ЗАПАСНОЙ ПУТЬ: круг, как было до §41 — прежние иконки ловятся ровно так же.
            index = ClosestIndex(pointMeters, iconOffsetsMeters, hitRadiusMeters);
            if (index < 0) return false;

            distanceFromCenterMeters = Vector2.Distance(pointMeters, iconOffsetsMeters[index]);
            return true;
        }

        // ==================================================== ПРАВИЛО ПОДПИСИ
        /// <summary>
        /// ПОКАЗЫВАТЬ ЛИ ПОДПИСЬ.
        ///
        /// ИЗМЕНЕНО 21.10.2026 ПО СЛОВАМ ВЛАДЕЛЬЦА. До этого подпись была ТОЛЬКО на уровне групп
        /// (решение §35 «в веере — одни иконки, без названий»). На живом шлеме выяснилось, чем это
        /// оборачивается: «при наведении на иконки второго слоя не появляется название выполняемой
        /// функции» — то есть человек не знает, что нажмёт. Владелец разрешил изменить это правило
        /// (и один существующий тест `FanLevel_NeverShowsLabels` заменён на обратный), поэтому
        /// подпись теперь появляется по удержанию 0,5 с на ОБОИХ уровнях.
        /// </summary>
        public static bool ShowsLabel(KvXrPanelLevel level, float dwellSeconds)
        {
            return KvGazeDwell.IsReady(dwellSeconds, LabelDwellSeconds);
        }

        /// <summary>Накопить время удержания на текущей иконке (переиспользует логику взгляда).</summary>
        public static float AdvanceLabelDwell(float progress, float deltaTime)
        {
            return KvGazeDwell.Advance(progress, deltaTime, LabelDwellSeconds);
        }

        // ==================================================== ПАНЕЛЬ У ПОЯСА

        /// <summary>
        /// ЗАПАСНОЕ ПОЛОЖЕНИЕ ПАНЕЛИ — У ПОЯСА. Применяется, когда позы контроллера нет:
        /// панель крепится к телу (низ живота), а НЕ встаёт перед глазами. Владелец запретил
        /// показ перед глазами прямо; при этом «панели нет вовсе» тоже не годится — тогда
        /// пункт приёмки «найти панель за 5 минут» не выполняется.
        /// </summary>
        public static Vector3 BeltPosition(Vector3 waistPosition, Vector3 bodyForward)
        {
            Vector3 forward = Flatten(bodyForward);
            return waistPosition + forward * BeltForwardMeters - Vector3.up * BeltDropMeters;
        }

        /// <summary>
        /// УГОЛ ПАНЕЛИ ОТ НАПРАВЛЕНИЯ ВЗГЛЯДА, градусы. Проверка «панель не висит в середине
        /// экрана» выражена числом, а не словами: запасное положение обязано быть не ближе
        /// <see cref="BeltMinAngleFromGazeDegrees"/> к взгляду.
        ///
        /// ВАЖНО ПРО ЧИСЛО: функция считает угол по ОПУСКАНИЮ ОТ ПОЯСА, а не от глаз. Полное
        /// опускание от глаз больше — оно складывается из перехода «глаза → пояс» и собственно
        /// <see cref="BeltDropMeters"/>. Для честной строки журнала есть перегрузка с явными
        /// метрами: её и надо применять, когда известно положение глаз и панели.
        /// </summary>
        public static float BeltAngleFromGazeDegrees()
        {
            return AngleFromGazeDegrees(BeltDropMeters, BeltForwardMeters);
        }

        /// <summary>Угол вниз от направления взгляда по фактическому опусканию и выносу, градусы.</summary>
        public static float AngleFromGazeDegrees(float dropMeters, float forwardMeters)
        {
            float forward = Mathf.Max(0.0001f, forwardMeters);
            return Mathf.Atan2(Mathf.Max(0f, dropMeters), forward) * Mathf.Rad2Deg;
        }

        /// <summary>Направление «вперёд» без наклона: панель у пояса не должна наклоняться за головой.</summary>
        public static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return Vector3.forward;
            return direction.normalized;
        }

        // ==================================================== ЖУРНАЛ

        /// <summary>
        /// СТРОКА ЖУРНАЛА панели. Без неё «стало лучше» не считается ([S3.7]): здесь видно
        /// число групп, число иконок в веере и порог подписи — то есть ровно то, что проверяет
        /// владелец в шлеме. Числа печатаются с инвариантной культурой: иначе на русской системе
        /// порог выглядел бы «0,5», и поиск по журналу давал бы разные результаты на разных машинах.
        /// </summary>
        public static string Describe(int groupCount, int fanGroupIconCount, string fanGroupTitle,
                                      KvXrQuickHand hand)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "панель групп: групп {0} · веер «{1}» — иконок {2} (предел {3}) · подпись через {4:0.0} с · " +
                "уровень 0 дуга {5:0}°, веер дуга {6:0}° · рука {7} · запасное положение у пояса {8:0}° от взгляда",
                groupCount, fanGroupTitle, fanGroupIconCount, MaxFanIcons, LabelDwellSeconds,
                GroupSpanDegrees, FanSpanDegrees,
                hand == KvXrQuickHand.Right ? "правая" : "левая",
                BeltAngleFromGazeDegrees());
        }
    }
}
