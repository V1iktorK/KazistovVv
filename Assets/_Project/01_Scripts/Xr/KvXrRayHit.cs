using System.Globalization;
using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ОТКУДА ЛУЧ ПОПАЛ: РЕАЛЬНАЯ ПОВЕРХНОСТЬ КОМНАТЫ (MR) ИЛИ ВИРТУАЛЬНЫЙ ОБЪЕКТ СЦЕНЫ.
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ ТИП. Требование владельца № 4 дословно: «Левый (который надо перекрасить в
    /// красный) выбирает точку (добавь чтобы он мог как и правый упираться в текстуры), а правым
    /// выбирать траекторию». В отчёте тестировщика причина разницы была видна по коду: правый луч
    /// спрашивал `KvMrSurfaces.TryRaycast` (только реальные коллайдеры комнаты), а левый —
    /// обычный `Physics.Raycast` (только виртуальные). То есть ДВА РАЗНЫХ ПРАВИЛА на два луча,
    /// и каждый видел ПОЛОВИНУ мира.
    ///
    /// ЧТО СТАЛО. Источник попадания — одно перечисление, и оба луча получают его из ОДНОЙ функции
    /// (`KvXrRayHit.Nearest`). Разные правила для лучей — дефект, поэтому типа два состояния
    /// (реальное / виртуальное) достаточно: остальное различие не нужно ни журналу, ни тестам.
    /// </summary>
    public enum KvXrHitSource
    {
        /// <summary>Попадания нет: луч прошёл на полную длину.</summary>
        None = 0,

        /// <summary>Реальная поверхность комнаты (меш PICO или размеченный стол) — коллайдер `KvMrSurfaces`.</summary>
        Real = 1,

        /// <summary>Виртуальный объект сцены (робот, стенд, пол лаборатории, панель) — `Physics.Raycast`.</summary>
        Virtual = 2
    }

    /// <summary>
    /// ОДНО ПОПАДАНИЕ ЛУЧА (чистые данные, без Unity-объектов — проверяется тестом без сцены).
    ///
    /// ПОЧЕМУ НЕ `RaycastHit`. У `RaycastHit` нельзя спросить «а было ли попадание»: значение по
    /// умолчанию (`distance = 0`, `collider = null`) неотличимо от настоящего попадания в ноль
    /// метров. Здесь признак попадания явный (`Source != None`), поэтому «ноль метров» и
    /// «попадания нет» не путаются — та же ловушка [F2], что была с нулевой позой контроллера.
    /// </summary>
    public struct KvXrRayHit
    {
        /// <summary>Куда попал луч.</summary>
        public KvXrHitSource Source;

        /// <summary>Расстояние от начала луча до попадания, метры.</summary>
        public float Distance;

        /// <summary>Точка попадания в мировых координатах.</summary>
        public Vector3 Point;

        /// <summary>Нормаль поверхности в точке попадания.</summary>
        public Vector3 Normal;

        /// <summary>Имя объекта под лучом (для журнала) — «стол», «KvMrMesh_…», «Стенд_1_Стол».</summary>
        public string Name;

        /// <summary>Попадание есть.</summary>
        public bool Any { get { return Source != KvXrHitSource.None; } }

        /// <summary>Попадание в реальную поверхность комнаты.</summary>
        public bool IsReal { get { return Source == KvXrHitSource.Real; } }
    }

    /// <summary>
    /// ЕДИНЫЙ ИСТОЧНИК ПОПАДАНИЙ ДЛЯ ОБОИХ ЛУЧЕЙ + ЦВЕТА ЛУЧЕЙ (пункты 4 и 5 владельца).
    ///
    /// ЧТО БЫЛО (отчёт тестировщика, подтверждено чтением кода).
    ///   • Правый луч шёл через `KvXrUiPointer.AimAlongPanel` → `HitWorld` → `Physics.Raycast`:
    ///     видел робота и стенды, но НЕ видел настоящий стол оператора — у столешницы в MR нет
    ///     коллайдера в сцене, он появляется только из меша комнаты (`KvMrSurfaces`);
    ///   • левый луч (`KvXrSecondaryRay`) спрашивал только `Physics.Raycast`, то есть ровно то же
    ///     зеркальное правило: реальные поверхности комнаты он не видел вообще.
    ///   Итог: «один упирается в текстуры (реальность), другой — нет». Правило проекта — одно
    ///   действие не чинится в двух местах по-разному, — значит и попадание должно считаться ОДИН раз.
    ///
    /// ЧТО СТАЛО. `Nearest` берёт БЛИЖАЙШЕЕ из двух кандидатов и возвращает его ВМЕСТЕ с признаком
    /// источника. Оба луча зовут только её — разойтись они больше не могут.
    /// </summary>
    public static class KvXrRayHitRules
    {
        /// <summary>
        /// ЦВЕТ ЛЕВОГО ЛУЧА — КРАСНЫЙ. ЧТО ЗНАЧИТ: левый луч ВЫБИРАЕТ ТОЧКУ (пункт 4 владельца).
        ///
        /// ПОЧЕМУ ИМЕНОВАННАЯ КОНСТАНТА, А НЕ ЧИСЛО НА МЕСТЕ. Владелец дважды просил перекрасить
        /// лучи, и оба раза цвет «съезжал»: сначала оба были голубыми, потом цвет подменялся на
        /// наведении. Пока красный записан одним именем с этим объяснением, «перекрасить обратно»
        /// можно только осознанно, а тест `LeftLaserIsRed_RightLaserIsGreen` это заметит.
        /// </summary>
        public static readonly Color PointLaserColor = new Color(0.95f, 0.16f, 0.14f, 0.95f);

        /// <summary>
        /// ЦВЕТ ПРАВОГО ЛУЧА — ЗЕЛЁНЫЙ. ЧТО ЗНАЧИТ: правый луч ВЫБИРАЕТ ТРАЕКТОРИЮ (пункт 4
        /// владельца) и к его концу прикреплён робот (пункт 3).
        /// </summary>
        public static readonly Color TrajectoryLaserColor = new Color(0.18f, 0.92f, 0.34f, 0.95f);

        /// <summary>ЦВЕТ ЛУЧА ПО РУКЕ: левая — красный (точка), правая — зелёный (траектория).</summary>
        public static Color LaserColorOf(KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Left ? PointLaserColor : TrajectoryLaserColor;
        }

        /// <summary>Слово для журнала: какого цвета луч этой руки.</summary>
        public static string LaserColorName(KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Left ? "красный" : "зелёный";
        }

        /// <summary>Слово для журнала: что делает луч этой руки.</summary>
        public static string LaserPurposeName(KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Left ? "точка" : "траектория";
        }

        /// <summary>
        /// ПОДПИСЬ ЛУЧА В ЖУРНАЛЕ: «ЛЕВЫЙ» / «ПРАВЫЙ».
        ///
        /// СЛОЖЕНИЕ С ПРОЕКТНЫМ «РУКА ЛУЧА». В этом проекте луч нажатия идёт из НАСТРОЙКИ
        /// (`KvXrInputSource.RayHand`), а второй луч — из противоположной руки. Поэтому «левый»
        /// здесь — это именно ЛЕВАЯ РУКА, а не «второй по счёту»: иначе при переназначенной руке
        /// журнал называл бы левым зелёный луч, и разбор снова упирался бы в догадки.
        /// </summary>
        public static string HandLabel(KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Left ? "ЛЕВЫЙ" : "ПРАВЫЙ";
        }

        /// <summary>Слово для журнала: чем кончилось попадание.</summary>
        public static string SourceName(KvXrHitSource source)
        {
            switch (source)
            {
                case KvXrHitSource.Real: return "реальная";
                case KvXrHitSource.Virtual: return "виртуальная";
                default: return "нет";
            }
        }

        /// <summary>
        /// БЛИЖАЙШЕЕ ИЗ ДВУХ ПОПАДАНИЙ — ОДНО ПРАВИЛО НА ОБА ЛУЧА.
        ///
        /// ПОЧЕМУ ЧИСТАЯ ФУНКЦИЯ. На устройстве иначе не доказать, что лучи на одной геометрии дают
        /// ОДИН результат: журнал шлема читают глазами, а «правило у лучей разное» — это ровно та
        /// ошибка, которую нашёл тестировщик. Здесь нет ни `Physics`, ни `Debug`: только два
        /// кандидата и ответ.
        ///
        /// ПРАВИЛА, КОТОРЫЕ ВАЖНО СОХРАНИТЬ (каждое — из уже случавшегося дефекта):
        ///   • NaN/бесконечность на входе — это НЕ попадание (класс E-21): любое сравнение с NaN
        ///     даёт `false`, и без явной отбраковки мусор прошёл бы как «ближайшее»;
        ///   • отрицательное или нулевое расстояние — не попадание: попадание «за спиной» или «в
        ///     самой руке» нарисовало бы луч нулевой длины, то есть «лазер мигнул»;
        ///   • РАВНЫЕ расстояния — побеждает РЕАЛЬНАЯ поверхность: настоящий стол важнее
        ///     виртуальной панели, висящей в той же точке (иначе робот вставал бы на панель).
        /// </summary>
        public static KvXrRayHit Nearest(
            bool realHit, float realDistance, Vector3 realPoint, Vector3 realNormal, string realName,
            bool virtualHit, float virtualDistance, Vector3 virtualPoint, Vector3 virtualNormal,
            string virtualName)
        {
            KvXrRayHit hit;
            hit.Source = KvXrHitSource.None;
            hit.Distance = float.PositiveInfinity;
            hit.Point = Vector3.zero;
            hit.Normal = Vector3.up;
            hit.Name = null;

            bool hasReal = realHit && IsUsableDistance(realDistance) && IsFinite(realPoint);
            bool hasVirtual = virtualHit && IsUsableDistance(virtualDistance) && IsFinite(virtualPoint);

            if (!hasReal && !hasVirtual) return hit;

            // РАВЕНСТВО — В ПОЛЬЗУ РЕАЛЬНОЙ: сначала проверяем её, и виртуальная побеждает только
            // СТРОГО ближе. Так «та же точка» не превращается в «попадание в виртуальную панель».
            bool takeReal = hasReal && (!hasVirtual || realDistance <= virtualDistance);

            if (takeReal)
            {
                hit.Source = KvXrHitSource.Real;
                hit.Distance = realDistance;
                hit.Point = realPoint;
                hit.Normal = IsFinite(realNormal) && realNormal.sqrMagnitude > 0.0001f
                    ? realNormal.normalized
                    : Vector3.up;
                hit.Name = string.IsNullOrEmpty(realName) ? "поверхность комнаты" : realName;
                return hit;
            }

            hit.Source = KvXrHitSource.Virtual;
            hit.Distance = virtualDistance;
            hit.Point = virtualPoint;
            hit.Normal = IsFinite(virtualNormal) && virtualNormal.sqrMagnitude > 0.0001f
                ? virtualNormal.normalized
                : Vector3.up;
            hit.Name = string.IsNullOrEmpty(virtualName) ? "объект сцены" : virtualName;
            return hit;
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА О ПОПАДАНИИ — ЧИСТАЯ ФУНКЦИЯ (проверяется регуляркой на числа).
        ///
        /// ФОРМА (требование владельца, дословно из задания):
        ///     [KvXr] ЛУЧ ЛЕВЫЙ: попадание в реальная «стол» на 1.42 м
        ///     [KvXr] ЛУЧ ПРАВЫЙ: попадание в виртуальная «Стенд_1_Стол» на 2.05 м
        ///     [KvXr] ЛУЧ ЛЕВЫЙ: нет попадания — луч идёт на полную длину 10.00 м
        /// Без попадания строка ТОЖЕ печатается: молчание — дефект (по нему не отличить «нет
        /// поверхности» от «код сломался»), и это правило проекта уже записано в FIXES.md.
        /// </summary>
        public static string Describe(string handLabel, KvXrHitSource source, string name,
            float distanceMeters, float fullLengthMeters)
        {
            var c = CultureInfo.InvariantCulture;
            string head = "[KvXr] ЛУЧ " + (string.IsNullOrEmpty(handLabel) ? "?" : handLabel) + ": ";

            if (source == KvXrHitSource.None)
            {
                float full = float.IsNaN(fullLengthMeters) || float.IsInfinity(fullLengthMeters)
                    ? 0f
                    : Mathf.Max(0f, fullLengthMeters);
                return head + "нет попадания — ни реальной поверхности, ни объекта сцены; " +
                       "луч идёт на полную длину " + full.ToString("0.00", c) + " м";
            }

            float meters = float.IsNaN(distanceMeters) || float.IsInfinity(distanceMeters)
                ? 0f
                : Mathf.Max(0f, distanceMeters);
            string what = string.IsNullOrEmpty(name) ? "без имени" : name;
            return head + "попадание в " + SourceName(source) + " «" + what + "» на " +
                   meters.ToString("0.00", c) + " м";
        }

        /// <summary>
        /// КЛЮЧ ЦЕЛИ ДЛЯ ПРАВИЛА «ПЕЧАТАТЬ ПРИ СМЕНЕ». Расстояние в ключ ВХОДИТ с точностью до
        /// дециметра: без него строка не обновлялась бы, когда оператор ведёт луч по одной и той же
        /// столешнице (а число метров — единственное доказательство, что луч действительно едет).
        /// </summary>
        public static string TargetKey(KvXrHitSource source, string name, float distanceMeters)
        {
            if (source == KvXrHitSource.None) return "none";
            int decimeters = float.IsNaN(distanceMeters) || float.IsInfinity(distanceMeters)
                ? -1
                : Mathf.RoundToInt(Mathf.Max(0f, distanceMeters) * 10f);
            return source.ToString() + "|" + (name ?? "") + "|" + decimeters.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>РАССТОЯНИЕ ГОДИТСЯ: конечное и строго больше нуля (см. правило про «за спиной»).</summary>
        public static bool IsUsableDistance(float distanceMeters)
        {
            if (float.IsNaN(distanceMeters) || float.IsInfinity(distanceMeters)) return false;
            return distanceMeters > 0f;
        }

        /// <summary>ЧИСЛО ЛИ ЭТО (отсев NaN/бесконечности — класс E-21).</summary>
        public static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }

    /// <summary>
    /// ГДЕ ЛУЧ ВСТРЕТИЛ МИР — ОДИН ЗАПРОС ДЛЯ ОБОИХ ЛУЧЕЙ (обёртка над чистыми правилами).
    ///
    /// ЗДЕСЬ ЖИВЁТ ФИЗИКА, поэтому класс отдельный: чистая часть (`KvXrRayHitRules`) проверяется
    /// тестом без сцены, а эта — только вызывается. Маска лучей ОДНА: слой `IgnoreRaycast`
    /// (модели контроллеров, курсор, метки) исключён, иначе луч упирался бы в собственную руку на
    /// нуле метров — этот дефект уже был и лечился именно маской.
    /// </summary>
    public static class KvXrRayHitProbe
    {
        /// <summary>Маска попаданий: всё, кроме слоя моделей рук и служебных меток.</summary>
        public static int Mask
        {
            get { return ~(1 << KvXrControllerVisuals.IgnoreRaycastLayer); }
        }

        /// <summary>
        /// ЕДИНСТВЕННЫЙ ИСТОЧНИК ПОПАДАНИЙ ДЛЯ ОБОИХ ЛУЧЕЙ: ближайшее из (реальное, виртуальное).
        ///
        /// ПОРЯДОК ВАЖЕН: сначала спрашивается реальная поверхность (`KvMrSurfaces.TryRaycast`), и
        /// только потом — обычный `Physics.Raycast`. Обе проверки идут на ВСЮ длину луча, потому что
        /// решение «кто ближе» принимается после, а не по очереди: иначе ближнее попадание
        /// отбрасывалось бы первым же дальним (именно так правый луч «не видел» реальный стол).
        /// </summary>
        public static KvXrRayHit Nearest(Vector3 origin, Vector3 direction, float maxDistanceMeters)
        {
            if (!KvXrRayHitRules.IsFinite(origin) || !KvXrRayHitRules.IsFinite(direction))
                return KvXrRayHitRules.Nearest(false, 0f, Vector3.zero, Vector3.up, null,
                    false, 0f, Vector3.zero, Vector3.up, null);

            if (direction.sqrMagnitude < 0.000001f)
                return KvXrRayHitRules.Nearest(false, 0f, Vector3.zero, Vector3.up, null,
                    false, 0f, Vector3.zero, Vector3.up, null);

            float limit = KvXrRayHitRules.IsUsableDistance(maxDistanceMeters)
                ? maxDistanceMeters
                : KvXrLaserRay.MinLengthMeters;

            // 1. РЕАЛЬНЫЕ ПОВЕРХНОСТИ КОМНАТЫ (меш PICO и размеченный стол).
            bool realHit = false;
            float realDistance = 0f;
            Vector3 realPoint = Vector3.zero;
            Vector3 realNormal = Vector3.up;
            string realName = null;

            KvMrSurfaces surfaces = KvMrSurfaces.Instance;
            RaycastHit realCast;
            if (surfaces != null && surfaces.TryRaycast(origin, direction, limit, out realCast))
            {
                realHit = true;
                realDistance = realCast.distance;
                realPoint = realCast.point;
                realNormal = realCast.normal;
                realName = RealSurfaceName(realCast.collider);
            }

            // 2. ВИРТУАЛЬНЫЕ ОБЪЕКТЫ СЦЕНЫ (робот, стенды, пол лаборатории).
            bool virtualHit = false;
            float virtualDistance = 0f;
            Vector3 virtualPoint = Vector3.zero;
            Vector3 virtualNormal = Vector3.up;
            string virtualName = null;

            RaycastHit virtualCast;
            if (Physics.Raycast(origin, direction.normalized, out virtualCast, Mathf.Max(0.05f, limit),
                    Mask, QueryTriggerInteraction.Ignore))
            {
                virtualHit = true;
                virtualDistance = virtualCast.distance;
                virtualPoint = virtualCast.point;
                virtualNormal = virtualCast.normal;
                virtualName = virtualCast.collider != null ? virtualCast.collider.name : "без имени";
            }

            return KvXrRayHitRules.Nearest(realHit, realDistance, realPoint, realNormal, realName,
                virtualHit, virtualDistance, virtualPoint, virtualNormal, virtualName);
        }

        /// <summary>
        /// КАК НАЗВАТЬ РЕАЛЬНУЮ ПОВЕРХНОСТЬ В ЖУРНАЛЕ.
        ///
        /// ЧТО БЫЛО. Имя коллайдера реального мира — техническое (`KvMrMesh_17`,
        /// `KvMrRealTable_0`), и в журнале «попадание в KvMrMesh_17» оператору ничего не говорит.
        /// ЧТО СТАЛО: обломки меша называются «поверхность комнаты», размеченный стол — «стол».
        /// Техническое имя остаётся рядом, чтобы наладчик мог найти объект в сцене.
        /// </summary>
        private static string RealSurfaceName(Collider collider)
        {
            if (collider == null) return "поверхность комнаты";
            string raw = collider.name;
            if (!string.IsNullOrEmpty(raw) && raw.StartsWith("KvMrRealTable", System.StringComparison.Ordinal))
                return "стол (разметка комнаты) · " + raw;
            if (!string.IsNullOrEmpty(raw) && raw.StartsWith("KvMrMesh", System.StringComparison.Ordinal))
                return "поверхность комнаты · " + raw;
            return string.IsNullOrEmpty(raw) ? "поверхность комнаты" : raw;
        }
    }
}
