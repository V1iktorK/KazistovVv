using UnityEngine;
// CollisionGuard.cs — простейшая проверка перед движением
public class CollisionGuard : MonoBehaviour
{
    public RobotController robot;
    public LayerMask obstacleLayers;    // Стол, стены, другие роботы
    
    /// <summary>
    /// МОЖНО ЛИ ДВИГАТЬСЯ В ТОЧКУ.
    ///
    /// P3-11 (11.11.2026). ЧТО БЫЛО. Если цель совпадала с текущей позицией TCP, `direction`
    /// получался нулевым, а `Physics.Raycast` с нулевым направлением пишет ошибку в консоль и
    /// возвращает `false` — предохранитель МОЛЧА отвечал «можно» именно в вырожденном случае.
    /// ЧЕМ ЭТО ГРОЗИЛО: проверка «препятствие на пути» не выполнялась, и это читалось как
    /// «препятствий нет». ТЕПЕРЬ вырожденный случай назван явно, а луч не кастуется.
    /// </summary>
    public bool CanMoveTo(Vector3 targetPos)
    {
        if (robot == null || robot.tcp == null) return true; // нет данных — пропускаем

        Vector3 direction = targetPos - robot.tcp.position;
        float distance = direction.magnitude;

        // Цель совпала с TCP: пути нет, проверять нечего — кастовать луч нулевой длины нельзя.
        if (distance < 1e-4f) return true;

        if (Physics.Raycast(robot.tcp.position, direction / distance, distance, obstacleLayers))
        {
            Debug.LogWarning("⚠ Обнаружено препятствие! Движение отменено.");
            return false;
        }
        return true;
    }
}
