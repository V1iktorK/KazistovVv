using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ОПРЕДЕЛЕНИЕ СТОРОНЫ РУКИ ПО ПОЛОЖЕНИЮ ОТНОСИТЕЛЬНО ГОЛОВЫ (сессия §37, найдено на живом шлеме).
    ///
    /// ЧТО СЛУЧИЛОСЬ. Владелец надел шлем и сказал: «контроллеры перепутаны местами» и «руки
    /// перепутаны». Это подтвердилось ЧИСЛАМИ из журнала, а не на слово:
    ///
    ///     голова в (-0.75, 1.12, 0.09), поворот (46, 151, 4) → «вправо от взгляда» = (-0.85; 0.05; -0.53)
    ///     устройство, названное РАНТАЙМОМ «правым»:   (-0.66, 0.80, 0.01) → проекция -5 см (СЛЕВА)
    ///     устройство, названное РАНТАЙМОМ «левым»:    (-0.74, 0.79, -0.07) → проекция +6 см (СПРАВА)
    ///
    /// То есть метка руки, которую отдаёт система, противоречит тому, где рука находится. До §37
    /// проект верил метке (`handedness`), и модель правого контроллера появлялась в левой руке.
    ///
    /// ПОЧЕМУ ГЕОМЕТРИЯ, А НЕ ЕЩЁ ОДНА МЕТКА. Метка — это то же самое, что уже соврало. Независимый
    /// свидетель один: положение руки относительно ГОЛОВЫ. Он не зависит ни от профиля OpenXR, ни
    /// от версии рантайма, и его видно числами в журнале.
    ///
    /// ЧЕСТНОЕ ОГРАНИЧЕНИЕ: если человек скрестил руки, признак «правая = справа от взгляда»
    /// неверен. Поэтому решение принимается по НЕСКОЛЬКИМ кадрам подряд, только когда обе руки
    /// уверенно разведены (не ближе <see cref="MinSideMeters"/> к оси взгляда), и дальше
    /// ЗАПОМИНАЕТСЯ: одна ошибка в начале не переключает стороны туда-сюда посреди работы.
    /// </summary>
    public static class KvXrSideResolver
    {
        /// <summary>
        /// Как далеко рука должна быть в стороне от оси взгляда, чтобы по ней можно было судить, м.
        ///
        /// БЫЛО 3 СМ, СТАЛО 1,5 СМ (21.10.2026, второй вход в шлем). С порогом 3 см автоопределение
        /// не включилось НИ РАЗУ: руки были почти по центру (`правая (-0.10, 1.00, -0.10) ·
        /// левая (-0.06, 1.20, -0.20)`), и строка `СТОРОНЫ РУК` в журнал не попала. 1,5 см — это
        /// всё ещё заметно больше дрожания рук и шума трекинга.
        /// </summary>
        public const float MinSideMeters = 0.015f;

        /// <summary>Сколько кадров подряд решение должно совпасть, прежде чем ему поверить.</summary>
        public const int VotesToLock = 20;

        /// <summary>
        /// НАСКОЛЬКО ОЧЕВИДНЫМ ДОЛЖНО БЫТЬ ПРОТИВОРЕЧИЕ, чтобы пересмотреть уже принятое решение, м.
        /// Если стороны закреплены, а затем руки развели на 5+ см явно в разные стороны — значит
        /// прежнее решение было ошибкой, и его надо исправить.
        /// </summary>
        public const float RelockMeters = 0.05f;

        /// <summary>
        /// ГДЕ РУКА ОТНОСИТЕЛЬНО ВЗГЛЯДА: −1 слева, +1 справа, 0 — судить нельзя (рука на оси).
        /// </summary>
        public static int SideOf(Vector3 position, Vector3 headPosition, Vector3 headRight)
        {
            Vector3 right = headRight;
            right.y = 0f;                                  // наклон головы вниз не должен влиять на сторону
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            right.Normalize();

            Vector3 offset = position - headPosition;
            float projection = Vector3.Dot(offset, right);

            if (Mathf.Abs(projection) < MinSideMeters) return 0;
            return projection > 0f ? 1 : -1;
        }

        /// <summary>Проекция руки на «вправо от взгляда», метры (для строки журнала).</summary>
        public static float ProjectionMeters(Vector3 position, Vector3 headPosition, Vector3 headRight)
        {
            Vector3 right = headRight;
            right.y = 0f;
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            right.Normalize();
            return Vector3.Dot(position - headPosition, right);
        }

        /// <summary>
        /// МЕТКИ РАНТАЙМА ПРОТИВОРЕЧАТ ПОЛОЖЕНИЮ: «правый» физически слева, а «левый» справа.
        /// Если хотя бы одну сторону определить нельзя (0) или обе оказались с одной стороны
        /// (скрещённые руки) — противоречия не объявляем: лучше ничего не менять, чем менять наугад.
        /// </summary>
        public static bool LabelsAreSwapped(int sideOfClaimedRight, int sideOfClaimedLeft)
        {
            if (sideOfClaimedRight == 0 || sideOfClaimedLeft == 0) return false;
            if (sideOfClaimedRight == sideOfClaimedLeft) return false;
            return sideOfClaimedRight < 0 && sideOfClaimedLeft > 0;
        }

        /// <summary>Противоречия нет, и обе стороны известны — значит метки можно подтвердить.</summary>
        public static bool LabelsConfirmed(int sideOfClaimedRight, int sideOfClaimedLeft)
        {
            if (sideOfClaimedRight == 0 || sideOfClaimedLeft == 0) return false;
            if (sideOfClaimedRight == sideOfClaimedLeft) return false;
            return sideOfClaimedRight > 0 && sideOfClaimedLeft < 0;
        }
    }
}
