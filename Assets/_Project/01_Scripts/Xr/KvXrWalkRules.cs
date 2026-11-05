using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ПРАВИЛА ХОДЬБЫ ПО АНГАРУ (шаг 3 плана VR-управления, 05.11.2026).
    ///
    /// ЧИСТАЯ ГЕОМЕТРИЯ, БЕЗ UNITY-ОБЪЕКТОВ, КРОМЕ ФИЗИЧЕСКОГО КАСТА. Вынесена из
    /// `KvXrLocomotion`, чтобы её можно было проверить тестами без шлема: раньше плавный ход
    /// просто прибавлял смещение к позиции рига, и оператор проходил сквозь столы и стены зала.
    ///
    /// ЧТО ЗДЕСЬ:
    ///   • границы зала — прямоугольник `KvHangarPlacement.BoundsAt` с отступом от стен;
    ///   • столкновения — капсула оператора против стендов/столов (не против слоя моделей
    ///     контроллеров и не против UI: иначе оператор «застревал» бы в собственных руках);
    ///   • режим — в VR правила включены, в MR выключены: зал там скрыт, комната 1:1, ходьба
    ///     свободная (человек ходит по своей комнате, а не по виртуальному залу).
    /// </summary>
    public static class KvXrWalkRules
    {
        /// <summary>Отступ от стен зала, метры: не «уткнуться носом» в стену.</summary>
        public const float WallMarginMeters = 0.6f;

        /// <summary>Слой UI (5): его поверхность не должна останавливать оператора.</summary>
        private const int UiLayer = 5;

        /// <summary>
        /// ПРИМЕНЯТЬ ЛИ ПРАВИЛА СЕЙЧАС. MR включается по факту пасс-тру (режим комнаты), AR сюда
        /// не попадает вовсе — там локомоция выключена. `Unknown` трактуем как VR (как и выбор
        /// режима в проекте: «спросить не удалось» — не повод ломать ходьбу).
        /// </summary>
        public static bool EnabledInCurrentMode
        {
            get { return KvXrCapabilities.Passthrough() != KvXrCapabilities.Answer.Yes; }
        }

        /// <summary>ГРАНИЦЫ ЗАЛА В МИРОВЫХ КООРДИНАТАХ: зал стоит вокруг места старта оператора.</summary>
        public static Bounds HangarBounds()
        {
            // Старт оператора — начало координат: `KvXrSceneContent` ставит стенды вокруг него,
            // а `KvXrHangarAnchor` переносит зал по тому же правилу.
            Vector3 root = KvHangarPlacement.RootPositionForOperator(Vector3.zero);
            return KvHangarPlacement.BoundsAt(root);
        }

        /// <summary>
        /// ОГРАНИЧИТЬ ПОЗИЦИЮ ЗАЛОМ. Высота не меняется: её задаёт трекинг, а не ходьба.
        /// </summary>
        public static Vector3 ClampToHangar(Vector3 position)
        {
            Bounds bounds = HangarBounds();
            float margin = Mathf.Max(0.05f, WallMarginMeters);
            float minX = bounds.min.x + margin;
            float maxX = bounds.max.x - margin;
            float minZ = bounds.min.z + margin;
            float maxZ = bounds.max.z - margin;
            if (minX > maxX) { minX = maxX = bounds.center.x; }
            if (minZ > maxZ) { minZ = maxZ = bounds.center.z; }
            return new Vector3(Mathf.Clamp(position.x, minX, maxX), position.y,
                               Mathf.Clamp(position.z, minZ, maxZ));
        }

        /// <summary>
        /// ПРОЙТИ ИЗ `current` В `desired` С УЧЁТОМ СТОЛКНОВЕНИЙ. Капсула оператора (радиус и рост
        /// передаёт вызывающий) кастуется вдоль шага; при попадании шаг скользит по нормали
        /// поверхности, а не останавливает оператора намертво.
        ///
        /// ПОЧЕМУ КАПСУЛА, А НЕ ЛУЧ. Луч толщиной в точку проскакивает мимо угла стола, и оператор
        /// «входит» в него боком. Капсула повторяет тело человека.
        /// </summary>
        public static Vector3 ResolveObstacles(Vector3 current, Vector3 desired,
                                               float radiusMeters, float heightMeters)
        {
            Vector3 delta = desired - current;
            delta.y = 0f;
            if (delta.sqrMagnitude < 1e-8f) return desired;

            float radius = Mathf.Max(0.1f, radiusMeters);
            float height = Mathf.Max(radius * 2f + 0.1f, heightMeters);

            Vector3 p1 = current + Vector3.up * (radius + 0.02f);
            Vector3 p2 = current + Vector3.up * (height - radius);

            int mask = ~(1 << KvXrControllerVisuals.IgnoreRaycastLayer) & ~(1 << UiLayer);

            Vector3 direction = delta.normalized;
            float distance = delta.magnitude;
            if (Physics.CapsuleCast(p1, p2, radius, direction, out RaycastHit hit,
                    distance + 0.02f, mask, QueryTriggerInteraction.Ignore))
            {
                // СКОЛЬЖЕНИЕ ПО СТЕНЕ: убираем из шага составляющую «в стену». На плоском полу
                // шаг по нормали сохраняет движение вдоль стены и не даёт «залипнуть».
                Vector3 slide = Vector3.ProjectOnPlane(delta, hit.normal);
                slide.y = 0f;
                return current + slide;
            }

            return desired;
        }

        /// <summary>СЛОВАМИ ДЛЯ ЖУРНАЛА: по каким правилам идёт оператор.</summary>
        public static string Describe()
        {
            Bounds bounds = HangarBounds();
            return string.Format(
                "ходьба: {0} · границы зала {1:0.0}×{2:0.0} м с отступом {3:0.0} м · капсула против стендов",
                EnabledInCurrentMode ? "VR (правила включены)" : "MR/свободная (зал скрыт)",
                bounds.size.x, bounds.size.z, WallMarginMeters);
        }
    }
}
