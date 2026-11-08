using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ЕДИНЫЙ КЭШ СПИСКА РОБОТОВ СЦЕНЫ (P2-1 / P2-2, 11.11.2026).
///
/// ЧТО БЫЛО. Сцену полным обходом `FindObjectsByType&lt;RobotController&gt;` опрашивали прямо из
/// кадра три места: `TrajectoryFlowController.UpdateAim → ResolveSelectedRobot`,
/// `TrajectoryFlowController.HoldAllRobots → RefreshRobotList` и
/// `KazistovVvUIManager.Update → TrackActiveRobotForProperties`. Каждый обход — это массив
/// (аллокация) и проход по ВСЕМ объектам сцены, а не по роботам.
///
/// ЧЕМ ЭТО ГРОЗИЛО. Бюджет кадра шлема на 72 Гц — 13,9 мс. Два-три полных обхода сцены в потоке
/// траекторий выбирали этот бюджет целиком и давали «столб» ровно в момент наведения, то есть
/// тогда, когда оператор целится и ждёт отклика; в MR, где объектов тысячи, — тем вернее.
///
/// ТЕПЕРЬ. Список собирается не чаще, чем раз в <see cref="MaxAgeSeconds"/>, и сбрасывается по
/// СОБЫТИЮ: `RuntimeRegistry.NotifyChanged()` (робот появился/исчез/пересобрано дерево),
/// загрузка сцены и старт PlayMode. Кэш без инвалидации не годится: оператор импортировал
/// робота — интерфейс обязан увидеть его сразу, а не «когда-нибудь».
/// </summary>
public static class RobotSceneCache
{
    /// <summary>
    /// МАКСИМАЛЬНЫЙ ВОЗРАСТ КЭША, с. Даже если событие об инвалидации не пришло (робота создал
    /// сторонний скрипт), список не может быть старше этого срока: цена — один обход сцены в
    /// секунду вместо одного в кадр.
    /// </summary>
    public const float MaxAgeSeconds = 1.0f;

    private static readonly List<RobotController> allRobots = new List<RobotController>();
    private static readonly List<RobotController> activeRobots = new List<RobotController>();
    private static readonly List<RobotController> visibleRobots = new List<RobotController>();

    private static float builtAt = float.NegativeInfinity;
    private static bool cacheValid;
    private static int version;

    /// <summary>Растёт на каждой пересборке — по нему видно, что кэш действительно обновился.</summary>
    public static int Version { get { return version; } }

    /// <summary>Собран ли список и не просрочен ли он (без побочных эффектов).</summary>
    public static bool IsFresh
    {
        get { return cacheValid && !ShouldRebuild(Time.realtimeSinceStartup, builtAt, cacheValid, MaxAgeSeconds); }
    }

    /// <summary>Все роботы сцены, ВКЛЮЧАЯ выключенные (нужно потоку траекторий: сценные роботы стартуют выключенными).</summary>
    public static List<RobotController> All
    {
        get { EnsureFresh(); return allRobots; }
    }

    /// <summary>Только активные и включённые роботы (прежний `FindObjectsInactive.Exclude`).</summary>
    public static List<RobotController> Active
    {
        get { EnsureFresh(); return activeRobots; }
    }

    /// <summary>Роботы без служебных копий (фантомы помечены `HideFlags.HideInHierarchy`).</summary>
    public static List<RobotController> Visible
    {
        get { EnsureFresh(); return visibleRobots; }
    }

    /// <summary>
    /// ПОРА ЛИ ПЕРЕСОБИРАТЬ СПИСОК — чистая функция без обращения к сцене (проверяется тестом
    /// P2-1 `P2_1_RobotCache_RebuildDecision`).
    ///
    /// Правила: нет кэша — собираем; время ушло назад (старт PlayMode/перезагрузка домена) —
    /// собираем; возраст дошёл до предела — собираем; иначе — нет.
    /// </summary>
    public static bool ShouldRebuild(float now, float lastBuild, bool valid, float maxAgeSeconds)
    {
        if (!valid) return true;
        if (float.IsNaN(now) || float.IsNaN(lastBuild)) return true;
        if (float.IsInfinity(lastBuild) && lastBuild < 0f) return true; // ещё ни разу не собирали
        if (now < lastBuild) return true;                              // время перескочило назад
        float maxAge = maxAgeSeconds > 0f ? maxAgeSeconds : 0f;
        return now - lastBuild >= maxAge;
    }

    /// <summary>Сбросить кэш: следующий запрос пересоберёт список из сцены.</summary>
    public static void Invalidate()
    {
        cacheValid = false;
    }

    /// <summary>Пересобрать сейчас, если кэш просрочен или сброшен.</summary>
    public static void EnsureFresh()
    {
        if (ShouldRebuild(Time.realtimeSinceStartup, builtAt, cacheValid, MaxAgeSeconds)) Rebuild();
    }

    /// <summary>Пересобрать список немедленно (один обход сцены).</summary>
    public static void Rebuild()
    {
        allRobots.Clear();
        activeRobots.Clear();
        visibleRobots.Clear();

        RobotController[] found = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
        foreach (RobotController rc in found)
        {
            if (rc == null) continue;
            allRobots.Add(rc);
            if (rc.isActiveAndEnabled) activeRobots.Add(rc);
            // Фантомы живут с HideInHierarchy: в дереве и в выборе прицелом они не участвуют.
            if ((rc.gameObject.hideFlags & HideFlags.HideInHierarchy) == 0) visibleRobots.Add(rc);
        }

        builtAt = Time.realtimeSinceStartup;
        cacheValid = true;
        version++;
    }

    /// <summary>
    /// СТАТИКА НЕ ДОЛЖНА ПЕРЕЖИВАТЬ ПРОГОН. Между запусками PlayMode (и при отключённой
    /// перезагрузке домена) статические поля сохраняются: список держал бы ссылки на уже
    /// уничтоженных роботов и «фальшиво-нулевые» Unity-объекты. `SubsystemRegistration` —
    /// самая ранняя точка сброса, до первого `Awake` сцены.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        allRobots.Clear();
        activeRobots.Clear();
        visibleRobots.Clear();
        builtAt = float.NegativeInfinity;
        cacheValid = false;
        version = 0;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Invalidate();
    }
}
