using System;
using UnityEngine;

/// <summary>
/// ВТОРОЙ (НЕЗАВИСИМЫЙ) СПОСОБ ВЫБОРА АКТИВНОГО РОБОТА — ИСТОЧНИК ПРОТИВОРЕЧИЙ (P1-15, 11.11.2026).
///
/// ЧТО БЫЛО. Активного робота выбирали ДВЕ системы: этот компонент (`activeIndex` + `SetActive`) и
/// `FreeFlyCameraController` (клавиша F, прицел, клик — тоже `SetActive`). Два источника истины
/// давали «оператор выбрал робота 2, а едет робот 1»: каждая система считала активным своего.
/// Плюс `Instance` — статическая ссылка без сброса в `OnDestroy` и без защиты от дубликата
/// (проверка `?.` на «фальшиво-null» Unity-объекте не спасает).
///
/// ЧТО СТАЛО (по требованию аудита «один путь выбора»): компонент помечен устаревшим, БОЛЬШЕ НЕ
/// ПИШЕТ `SetActive` (единственный путь выбора — `FreeFlyCameraController`/дерево), `Instance`
/// сбрасывается в `OnDestroy`. Класс НЕ УДАЛЁН: правило проекта — сначала `[Obsolete]` с причиной,
/// удаление — отдельным решением владельца (раздел 8 отчёта).
/// </summary>
[Obsolete("Второй независимый способ выбора робота (P1-15): единственный путь выбора — " +
          "FreeFlyCameraController/дерево. Компонент оставлен для совместимости сцен, SetActive не пишет.")]
public class RobotSelector : MonoBehaviour
{
    public static RobotSelector Instance;
    public RobotController[] robots;
    private int activeIndex = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RobotSelector] второй экземпляр на сцене — этот отключён (P1-15)");
            enabled = false;
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        // P1-15: без сброса статическая ссылка оставалась на уничтоженный объект.
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (robots == null || robots.Length == 0)
        {
            return;
        }

        // ВЫБОР БОЛЬШЕ НЕ ДЕЛАЕТСЯ ЗДЕСЬ: `SetActive` пишет только один путь (см. шапку класса).
        Debug.Log("[RobotSelector] выбор робота этим компонентом отключён (P1-15) — " +
                  "активного робота задаёт FreeFlyCameraController/дерево");
    }

    void Update()
    {
        if (robots == null || robots.Length == 0 || InputManager.Instance == null)
        {
            return;
        }

        if (InputManager.Instance.SwitchRobotDown)
        {
            // P1-15: `SetActive` здесь БОЛЬШЕ НЕ ВЫЗЫВАЕТСЯ. Именно эта запись спорила с
            // `FreeFlyCameraController`: обе системы объявляли активным своего робота, и «оператор
            // выбрал 2, а едет 1» было не редким случаем, а штатным поведением двух источников
            // истины. Переключение клавишей обслуживает единственный путь — `FreeFlyCameraController`/
            // дерево; здесь остаётся только предупреждение, чтобы старое место не «работало тихо».
            Debug.LogWarning("[RobotSelector] переключение робота клавишей отключено (P1-15): " +
                             "активного робота задаёт FreeFlyCameraController/дерево");
        }
    }

    public RobotController GetActiveRobot()
    {
        if (robots == null || robots.Length == 0)
        {
            return null;
        }

        int resolved = ResolveActiveIndex(activeIndex, robots.Length);
        if (resolved < 0) return null;
        RobotController robot = robots[resolved];
        return robot != null ? robot : null;
    }

    /// <summary>
    /// КАКОЙ ИНДЕКС СЧИТАЕТСЯ АКТИВНЫМ (−1 — активного нет). Чистая функция: вынесена из
    /// `GetActiveRobot`, чтобы правило «индекс живёт внутри массива роботов» проверялось тестом без
    /// сцены и без монобеха.
    ///
    /// ЧТО ЛОВИТСЯ. `robots[activeIndex]` без прижатия к границам — это `IndexOutOfRangeException`
    /// ровно в тот момент, когда робота пересоздали или список сократили: оператор получает
    /// исключение вместо «активного робота нет». Повторный выбор того же индекса обязан давать того
    /// же робота (правило идемпотентно): второй вызов не сдвигает выбор.
    /// </summary>
    public static int ResolveActiveIndex(int requested, int count)
    {
        if (count <= 0) return -1;
        if (requested < 0) return 0;
        if (requested >= count) return count - 1;
        return requested;
    }
}
