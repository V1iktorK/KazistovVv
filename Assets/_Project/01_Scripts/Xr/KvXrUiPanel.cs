using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ПЛОСКАЯ ПАНЕЛЬ ИНТЕРФЕЙСА В VR (решение владельца [D1], сессия §28, этап 3).
    ///
    /// ЧТО ЭТО. Весь существующий интерфейс проекта (меню, тулбар, дерево, свойства,
    /// настройки, статус-строка) висит в воздухе перед оператором как один большой
    /// накладной экран и управляется лучом контроллера. Пространственный интерфейс
    /// (панели на руке, кнопки-плитки) — ОТДЕЛЬНЫЙ этап после того, как владелец
    /// поживёт в шлеме: строить его «по представлению» значит гарантированно переделать.
    ///
    /// ГДЕ ЗДЕСЬ ГЛАВНАЯ ОПАСНОСТЬ. Интерфейс проекта — Screen Space Overlay: он рисуется
    /// поверх кадра и в стерео НЕ РАБОТАЕТ (каждый глаз отрисовал бы свой). Поэтому
    /// канвас переводится в World Space и вешается перед камерой. Размеры при этом
    /// перестают быть пикселями и становятся метрами — и ошибиться тут легко на порядок:
    /// панель шириной «1920» окажется стеной в 1,9 км. Поэтому вся геометрия считается
    /// ЗДЕСЬ, числами, и проверяется тестами; сборка только применяет результат.
    ///
    /// Что этот тип НЕ делает: не создаёт объектов, не ищет камеру, не трогает канвас.
    /// </summary>
    public struct KvXrUiPanelSettings
    {
        /// <summary>Мировая позиция центра панели.</summary>
        public Vector3 position;
        /// <summary>Мировой поворот панели.</summary>
        public Quaternion rotation;
        /// <summary>Мировой масштаб канваса (перевод пикселей канваса в метры).</summary>
        public float worldScale;
        /// <summary>Ширина панели в метрах.</summary>
        public float widthMeters;

        /// <summary>Углы поворота в привычном виде (для журнала и ручной проверки).</summary>
        public Vector3 eulerAngles { get { return rotation.eulerAngles; } }
    }

    /// <summary>Геометрия плоской панели: расстояния, углы, перевод пикселей канваса в метры.</summary>
    public static class KvXrUiPanel
    {
        /// <summary>
        /// Расстояние панели от глаз по умолчанию, метры.
        ///
        /// Почему 2,0: ближе 0,4 м глаза не сводятся (дискомфорт и «двоение»), дальше
        /// 4 м текст канваса проекта (13–16 px) уже не читается. 2 м — компромисс, при
        /// котором панель шириной 2 м занимает 90° поля зрения, а строка кода в тулбаре
        /// остаётся различимой.
        /// </summary>
        public const float DefaultDistanceMeters = 2.0f;

        /// <summary>Наклон панели к лицу, градусы. Небольшой: верх панели чуть дальше низа.</summary>
        public const float TiltDegrees = 6f;

        /// <summary>Угол обзора, который занимает панель, по умолчанию — 90°.</summary>
        public const float DefaultFieldOfViewDegrees = 90f;

        /// <summary>Нижняя граница углового размера панели, градусы.</summary>
        public const float MinFieldOfViewDegrees = 45f;

        /// <summary>Верхняя граница углового размера панели, градусы (шире — уже надо крутить шеей).</summary>
        public const float MaxFieldOfViewDegrees = 120f;

        /// <summary>
        /// Ширина панели в метрах по расстоянию и угловому размеру: W = 2·d·tg(φ/2).
        ///
        /// Это хорда, которую панель занимает в поле зрения: при 2 м и 90° получается
        /// ровно 2 м — панель такой ширины оператор видит целиком, не поворачивая головы
        /// (половина угла в каждую сторону). Ошибка «в два раза» здесь была бы незаметна
        /// в шлеме, но превратила бы панель в стену — поэтому число проверено тестом.
        /// </summary>
        public static float WidthMeters(float distanceMeters, float fieldOfViewDegrees)
        {
            float d = Mathf.Max(0f, distanceMeters);
            float fov = SanitizeFieldOfView(fieldOfViewDegrees);
            // Дефект, найденный тестом до шлема (02.10.2026): здесь стоял множитель 2,
            // и панель на 2 м выходила 4 м шириной — стена вместо экрана. Ошибка была бы
            // незаметна в коде, но видна в шлеме; тест поймал её числом.
            return d * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>Угол обзора панели, приведённый к разумным границам 45…120°.</summary>
        public static float SanitizeFieldOfView(float fieldOfViewDegrees)
        {
            if (float.IsNaN(fieldOfViewDegrees) || float.IsInfinity(fieldOfViewDegrees))
                return DefaultFieldOfViewDegrees;
            return Mathf.Clamp(fieldOfViewDegrees, MinFieldOfViewDegrees, MaxFieldOfViewDegrees);
        }

        /// <summary>Высота панели в метрах из ширины и пропорции канваса (по умолчанию 1920×1080 = 16:9).</summary>
        public static float HeightMeters(float widthMeters, float referenceWidth, float referenceHeight)
        {
            if (referenceWidth <= 0f || referenceHeight <= 0f) return 0f;
            // Для канваса 1920×1080 это ровно 9/16 ширины. Дефект «в два раза выше» был
            // найден тестом до шлема: ширина 2 м давала панель высотой 2,25 м, то есть
            // почти квадрат вместо широкого экрана.
            return Mathf.Max(0f, widthMeters) * (referenceHeight / referenceWidth);
        }

        /// <summary>
        /// Мировой масштаб канваса: сколько метров приходится на одну «пиксель-единицу»
        /// канваса. Именно это число превращает канвас 1920 px в панель 1,92 м.
        /// Возвращает 0 при бессмысленных входах — вызывающий обязан это заметить
        /// (в тестах проверено: ноль, а не NaN и не бесконечность).
        /// </summary>
        public static float WorldScaleFromReference(float referenceWidth, float widthMeters)
        {
            if (referenceWidth <= 0f || widthMeters <= 0f) return 0f;
            return widthMeters / referenceWidth;
        }

        /// <summary>
        /// Полный расчёт панели для камеры: позиция перед глазами, поворот к лицу,
        /// масштаб канваса и ширина в метрах.
        ///
        /// Панель ставится НЕ в мировых осях, а в осях камеры (вперёд/вверх/вправо) —
        /// поэтому она корректно работает при любом положении и повороте оператора.
        /// </summary>
        public static KvXrUiPanelSettings SettingsFor(
            Vector3 headPosition,
            Quaternion headRotation,
            float eyeHeightMeters,
            float distanceMeters = DefaultDistanceMeters,
            float fieldOfViewDegrees = DefaultFieldOfViewDegrees)
        {
            KvXrUiPanelSettings s;
            float d = Mathf.Max(0f, distanceMeters);
            float width = WidthMeters(d, fieldOfViewDegrees);

            // Панель ставится туда, КУДА СМОТРИТ ОПЕРАТОР: направление взгляда берётся
            // у камеры, но без вертикальной составляющей — иначе при взгляде в пол панель
            // уезжала бы в пол. Дефект, найденный тестом до шлема (02.10.2026): здесь
            // использовалось полное `headRotation * Vector3.forward`, и при взгляде вниз
            // панель оказывалась «на уровне глаз», то есть фактически в ногах.
            Vector3 forward = headRotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;   // смотрим строго в пол
            forward.Normalize();

            s.position = headPosition + forward * d;

            // Поворот: канвас смотрит своей осью −Z (как Canvas в Unity), а камера — +Z.
            // Разворачиваем канвас к лицу и наклоняем верхнюю кромку чуть дальше нижней.
            // Наклон в осях ОПЕРАТОРА (yaw → pitch), а не в мировых: иначе при повороте
            // головы панель наклонялась бы вбок («заваливалась на ухо»).
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            // Знак наклона ПРОВЕРЕН ЗАМЕРОМ (02.10.2026): при развороте панели на 180°
            // (yaw + 180) положительный угол Эйлера даёт ФАКТИЧЕСКИЙ наклон от оператора
            // (ось «вперёд» панели уходит вверх, pitch = −6°). Чтобы верх панели был ближе
            // к лицу, чем низ, угол задаётся отрицательным — это и есть «наклон к лицу».
            s.rotation = Quaternion.Euler(-TiltDegrees, yaw + 180f, 0f);
            s.worldScale = WorldScaleFromReference(1920f, width);
            s.widthMeters = width;
            return s;
        }
    }
}
