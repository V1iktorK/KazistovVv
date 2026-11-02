using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ГДЕ ОБРЫВАЕТСЯ ЛАЗЕР — ОДНО ПРАВИЛО НА ВСЕ ЛУЧИ (пункт 2 владельца 02.11.2026:
    /// «лазеры не упираются в роботов и столы и пол»).
    ///
    /// ЧТО БЫЛО НЕ ТАК. В шлеме лазер рисовался так: «есть цель в интерфейсе — до неё, иначе
    /// `origin + direction * rayLength`». При `rayLength = 10` и расстоянии до руки около 1,1 м
    /// линия получалась «длиной 8,90 м» и проходила сквозь стол, робота и пол. Один и тот же
    /// дефект был в главном луче (`KvXrUiPointer.AimAlongPanel`) и в §42 его «исправили» в
    /// `KvXrUiPointer.Aiming` — ветке, которая при настройках по умолчанию не выполняется.
    ///
    /// ЧТО ДЕЛАЕТ ЭТО ПРАВИЛО. Известны три величины: попадание в интерфейс (кнопка или плита),
    /// попадание в геометрию сцены (робот, стол, пол, стена) и полная длина луча. Берётся
    /// БЛИЖАЙШЕЕ попадание; если попаданий нет — полная длина, как и было. Ничего «своего» у
    /// луча появляться не должно, поэтому правило живёт одним методом, а не копией в каждом луче.
    ///
    /// ПОЧЕМУ ОТДЕЛЬНЫЙ КЛАСС, А НЕ СТРОКИ ВНУТРИ КОМПОНЕНТА. Так это проверяется EditMode-тестом
    /// (`KvXrLaserBeamTests.RayEndsAtFirstHitOrAtFullLength`) без сцены и без шлема: иначе
    /// «упирается ли луч» можно было бы проверить только глазами в устройстве.
    /// </summary>
    public static class KvXrLaserRay
    {
        /// <summary>Минимальная длина луча, метры: нулевой длины луч не видно и он «мигает».</summary>
        public const float MinLengthMeters = 0.01f;

        /// <summary>
        /// ДЛИНА ЛУЧА В МЕТРАХ: ближайшее попадание (интерфейс или мир) либо полная длина.
        ///
        /// `interfaceAlong`/`worldAlong` — расстояния от начала луча до попадания. Признак
        /// попадания передаётся отдельно, поэтому «ноль метров» не путается с «попадания нет».
        /// </summary>
        public static float EndMeters(bool interfaceHit, float interfaceAlong,
                                      bool worldHit, float worldAlong, float fullLengthMeters)
        {
            float full = Mathf.Max(MinLengthMeters, fullLengthMeters);
            float end = full;

            if (interfaceHit && interfaceAlong > 0f && interfaceAlong <= end) end = interfaceAlong;
            if (worldHit && worldAlong > 0f && worldAlong < end) end = worldAlong;

            return Mathf.Max(MinLengthMeters, end);
        }

        /// <summary>ТОЧКА КОНЦА ЛУЧА по его длине (одна формула для обоих лучей).</summary>
        public static Vector3 EndPoint(Vector3 origin, Vector3 direction, float meters)
        {
            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            return origin + dir * Mathf.Max(MinLengthMeters, meters);
        }
    }
}
