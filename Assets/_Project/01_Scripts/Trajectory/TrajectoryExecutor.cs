using TrajectoryCore;
using UnityEngine;

/// <summary>
/// Исполнитель траекторий (E4 плана): проигрывает q(t) по времени, каждый шаг
/// проверяется SafetyGate (лимиты/зазор/watchdog). При отказе — немедленный стоп.
/// </summary>
public class TrajectoryExecutor : MonoBehaviour
{
    public float timeScale = 1f;              // можно замедлить (0.5 = вдвое медленнее)

    private PoseValidator validator;
    private CollisionWorld world;
    private SafetyGate gate;
    private PlannedTrajectory active;
    private double playhead;
    private int index;
    private bool running;

    public bool IsRunning => running;
    public string StatusText { get; private set; } = "";

    // ------------------------------------------------------------------ прогресс (ЭТАП 12 ТЗ: панель ETA)
    // Добавлены ТОЛЬКО читающие свойства: сам проигрыватель, проверки SafetyGate и
    // порядок применения поз не менялись — панели ETA/прогресса нужен остаток времени.

    /// <summary>Активная траектория (null — ничего не играет).</summary>
    public PlannedTrajectory ActivePlan { get { return active; } }

    /// <summary>Пройденное время активной траектории, с.</summary>
    public float ElapsedSeconds { get { return (float)playhead; } }

    /// <summary>Полное время активной траектории, с.</summary>
    public float TotalSeconds
    {
        get
        {
            if (active == null || active.Times == null || active.Times.Length == 0) return 0f;
            return active.Times[active.Times.Length - 1];
        }
    }

    /// <summary>Прогресс 0…1 (0 — ничего не играет).</summary>
    public float Progress01
    {
        get
        {
            float total = TotalSeconds;
            if (total <= 0.0001f) return 0f;
            return Mathf.Clamp01((float)(playhead / total));
        }
    }

    /// <summary>Осталось секунд (с учётом паузы — время не идёт, остаток не растёт).</summary>
    public float RemainingSeconds
    {
        get
        {
            float total = TotalSeconds;
            return Mathf.Max(0f, total - (float)playhead);
        }
    }

    /// <summary>
    /// Пауза проигрывания: робот стоит, траектория НЕ считается завершённой
    /// (`IsRunning` остаётся true, поэтому State Machine не переключает состояние).
    /// Ручка интерфейса (кнопка «Запуск/пауза» на верхней панели).
    /// </summary>
    public bool Paused { get; private set; }

    /// <summary>Поставить/снять паузу.</summary>
    public void SetPaused(bool value)
    {
        Paused = value;
        if (!value) gate?.NotifyState();
    }

    public void Init(PoseValidator v, CollisionWorld w, SafetyGate g)
    {
        validator = v;
        world = w;
        gate = g;
    }

    /// <summary>
    /// ГОДЕН ЛИ ПЛАН К ИСПОЛНЕНИЮ: длины `Times` и `Path` обязаны совпадать (P1-10, 11.11.2026).
    ///
    /// ЧТО БЫЛО. `Play` и `SafetyGate.Approve` проверяли только `Path`. `MotionTiming.RescaleToSpeed`
    /// при `Times == null` просто выходил, оставляя поле пустым, и `Update` разыменовывал
    /// `active.Times` → `NullReferenceException` В САМОМ ЦИКЛЕ ДВИЖЕНИЯ. Для оператора это
    /// выглядело как «робот дёрнулся и остановился», причина — в консоли, которой у него нет.
    ///
    /// ЧЕМ ЭТО ГРОЗИЛО. Исключение посреди исполнения обходит `Stop(reason)`: статус остаётся
    /// «в движении», а `running` — `true`, при этом кадры уже не считаются. Робот стоит, а система
    /// считает, что едет.
    ///
    /// Теперь такой план отклоняется ДО пуска с понятным сообщением.
    /// </summary>
    public static bool HasUsableTimeline(PlannedTrajectory t)
    {
        if (t == null) return false;
        if (t.Path == null || t.Path.Length == 0) return false;
        return t.Times != null && t.Times.Length == t.Path.Length;
    }

    // ============================================================================================
    // ПАУЗА: ПРАВИЛА В ЧИСТЫХ ФУНКЦИЯХ (P4-3, 12.11.2026).
    //
    // ПОЧЕМУ ВЫНЕСЕНО. Всё поведение паузы жило внутри `Update`/`CheckStandingPose` — то есть
    // проверялось только чтением исходника (`KvPauseSafetyTests` брал подстроку 1500 символов от
    // `private void CheckStandingPose` и искал в ней слова). Такой тест ломается от переформулировки
    // комментария и, что хуже, ПРОХОДИТ на сломанном коде: строка «ApproveStep» может остаться,
    // а вызываться не будет. Правила ниже вызываются из тех же мест, но проверяются по результату.
    // ============================================================================================

    /// <summary>ЧТО ДЕЛАТЬ В КАДРЕ, КОГДА ДВИЖЕНИЕ СТОИТ (чистая функция — проверяется тестом).</summary>
    public enum PausedFrameAction
    {
        /// <summary>Ничего: проверять нечего (движение не запущено или траектории нет).</summary>
        Nothing = 0,
        /// <summary>Проверить стоящую позу: робот стоит, а обстановка вокруг могла измениться.</summary>
        VerifyStandingPose = 1
    }

    /// <summary>
    /// ПРОВЕРКА БЕЗОПАСНОСТИ НА ПАУЗЕ — НЕ «ОТДЫХ ОТ ПРОВЕРОК» (S44; чистая функция).
    ///
    /// До исправления 30.10.2026 `Update` на паузе выходил СРАЗУ после `NotifyState()`: робот стоял,
    /// но никто не следил, что он стоит в безопасном месте. После аварийного останова (он сам ставит
    /// паузу) контроль зазора и лимитов оказывался выключен, и первое, что происходило после снятия
    /// паузы, — движение в ставшую опасной зону.
    ///
    /// Возвращает <see cref="PausedFrameAction.VerifyStandingPose"/> ровно в одном случае: пауза при
    /// ЖИВОМ движении и непустой траектории. Во всех прочих проверять нечего.
    /// </summary>
    public static PausedFrameAction PausedFrameActionFor(bool paused, bool running, bool hasActivePath)
    {
        if (!running || !hasActivePath) return PausedFrameAction.Nothing;
        return paused ? PausedFrameAction.VerifyStandingPose : PausedFrameAction.Nothing;
    }

    /// <summary>
    /// ИНДЕКС СЭМПЛА ДЛЯ ПРОВЕРКИ СТОЯЩЕЙ ПОЗЫ (−1 — проверять нечего).
    /// Индекс прижимается к пути: `index` ведёт движение и может оказаться за последним сэмплом
    /// (или отрицательным на первом кадре), а обращение `active.Path[index]` за границей — это
    /// `IndexOutOfRangeException` посреди исполнения, то есть остановка БЕЗ причины в журнале.
    /// </summary>
    public static int StandingSampleIndex(int index, int pathLength)
    {
        if (pathLength <= 0) return -1;
        if (index < 0) return 0;
        if (index >= pathLength) return pathLength - 1;
        return index;
    }

    /// <summary>
    /// ВРЕМЯ ПРОИГРЫВАНИЯ ПОСЛЕ КАДРА (чистая функция). На паузе время НЕ идёт: пауза — это
    /// «стоять», а не «ехать медленно», и остаток пути после неё начинается с того же места.
    /// Нижняя граница `timeScale` та же, что была в `Update`.
    ///
    /// РАНЬШЕ ЗДЕСЬ БЫЛО НАПИСАНО: «иначе проверка «время не идёт вперёд» (`SafetyGate.ApproveStep`)
    /// сама остановила бы паузу». Это объяснение выдавало дефект за задумку: правило времени
    /// действительно срабатывало — и превращало паузу в аварийный останов «watchdog» (дефект A,
    /// 12.11.2026). Теперь проверка стоящей позы правила времени не знает вовсе (см.
    /// `SafetyGate.ApproveStandingPose`), и остановки на паузе нет.
    /// </summary>
    public static double PlayheadAfterFrame(double playhead, float deltaTime, float timeScale, bool paused)
    {
        if (paused) return playhead;
        return playhead + deltaTime * Mathf.Max(0.05f, timeScale);
    }

    /// <summary>
    /// НАДО ЛИ ОСТАНОВИТЬСЯ ПО ИТОГУ ПРОВЕРКИ СТОЯЩЕЙ ПОЗЫ (чистая функция).
    ///
    /// Смысл проверки не в том, чтобы «посчитать зазор», а в том, чтобы ОТКАЗ ПРЕДОХРАНИТЕЛЯ вёл к
    /// останову: без этого проверка обнаружит опасность и промолчит. Поэтому результат —
    /// «предохранитель отказал», а не «зазор такой-то».
    ///
    /// ВРЕМЯ ПРОИГРЫВАНИЯ ЗДЕСЬ БОЛЬШЕ НЕ ПЕРЕДАЁТСЯ (12.11.2026, дефект A). Пока проверка шла
    /// через `ApproveStep`, она обязана была передать `playhead`, а на паузе он заморожен — и
    /// правило «время вперёд» превращало паузу в аварийный останов «watchdog». Теперь проверка
    /// стоящей позы идёт своим путём (`SafetyGate.ApproveStandingPose`), где правила времени нет,
    /// а живость данных, лимиты и зазор проверяются как прежде.
    /// </summary>
    public static bool StandingPoseNeedsStop(SafetyGate gate, double[] q, float clearance,
        PoseValidator validator, out SafetyReason reason)
    {
        reason = SafetyReason.NotChecked;
        if (gate == null) return false;
        return !gate.ApproveStandingPose(q, clearance, validator, out reason);
    }

    public bool Play(PlannedTrajectory t)
    {
        if (validator == null || !validator.Ready || t == null) return false;

        // П1-10: план без времён (или с несовпадающей длиной) не запускаем — иначе NRE в Update.
        if (!HasUsableTimeline(t))
        {
            StatusText = "Отказ: в плане нет времён под длину пути (" +
                         (t.Path != null ? t.Path.Length : 0) + " точек, " +
                         (t.Times != null ? t.Times.Length : 0) + " времён)";
            Debug.LogWarning("[Safety] " + StatusText);
            return false;
        }

        gate?.NotifyState();
        if (gate != null && !gate.Approve(t, validator, out SafetyReason reason))
        {
            StatusText = "Отказ Safety: " + SafetyGate.Describe(reason);
            Debug.LogWarning("[Safety] Траектория отклонена: " + StatusText);
            return false;
        }
        active = t;
        playhead = 0;
        index = 0;
        running = true;

        // ПУСК СНИМАЕТ ПАУЗУ (исправлено 30.10.2026, S44).
        //
        // ЧТО БЫЛО. `Play` не трогал `Paused`. Если оператор поставил паузу и затем нажал ПУСК
        // (выбрав другую траекторию), происходило вот что:
        //   1. `Play` возвращал `true`, статус показывал метку новой траектории;
        //   2. `Update` на первом же кадре видел `Paused == true` и выходил;
        //   3. робот НЕ ДВИГАЛСЯ.
        //
        // Оператор видел: «ПУСК нажат, статус правильный, а робот стоит». Причина невидима: ни
        // ошибки, ни предупреждения — просто ничего не происходит. Это хуже явного отказа:
        // непонятно, ждать или жать ещё раз.
        //
        // ПОЧЕМУ ПУСК ДОЛЖЕН СНИМАТЬ ПАУЗУ. «Пуск» — это команда начать движение. Оставлять после
        // неё паузу значит противоречить самой команде. Если бы мы хотели «пуск остаётся на паузе»,
        // это называлось бы иначе — и оператор об этом должен был бы знать.
        //
        // ВАЖНО ПОРЯДОК: пауза снимается ПОСЛЕ всех проверок. Если `Approve` отказал, состояние
        // паузы не меняется — и оператор видит ту же картину, что была, а не «пауза снялась, но
        // движения нет».
        Paused = false;

        gate?.ResetPlayhead();
        StatusText = t.Label;
        return true;
    }

    public void Stop(SafetyReason reason = SafetyReason.OperatorStop)
    {
        if (!running) return;
        running = false;
        Paused = false;
        active = null;
        StatusText = reason == SafetyReason.OperatorStop ? "остановлено" : "СТОП: " + SafetyGate.Describe(reason);
        if (reason != SafetyReason.OperatorStop)
            Debug.LogWarning("[Safety] Аварийный стоп: " + SafetyGate.Describe(reason));
    }

    /// <summary>
    /// ПРОВЕРКА СТОЯЩЕЙ ПОЗЫ НА ПАУЗЕ (создано 30.10.2026, S44).
    ///
    /// Робот не двигается, но обстановка вокруг может измениться: оператор пододвинул стол,
    /// поставил манекен, включил ограничения. На паузе это надо замечать — иначе первое, что
    /// произойдёт после снятия паузы, будет движение в ставшую опасной зону.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: проверки скорости. Стоящий робот её не имеет.
    /// </summary>
    private void CheckStandingPose()
    {
        // ПРАВИЛО «КОГДА ПРОВЕРЯТЬ» — В ЧИСТОЙ ФУНКЦИИ (см. `PausedFrameActionFor`): проверка нужна
        // только на паузе при живом движении. Здесь это условие ничего не меняет (метод зовут ровно
        // из ветки паузы), но теперь правило проверяемо тестом, а не чтением этого файла.
        if (PausedFrameActionFor(Paused, running, active != null) != PausedFrameAction.VerifyStandingPose)
            return;
        if (gate == null || validator == null || !validator.Ready || active == null) return;

        int pathLength = active.Path != null ? active.Path.Length : 0;
        int sample = StandingSampleIndex(index, pathLength);
        if (sample < 0) return;
        double[] q = active.Path[sample];

        float clearance = validator.ClearanceAt(q, world, out _, out _);
        SafetyReason reason;
        // `playhead` НЕ ПЕРЕДАЁТСЯ (12.11.2026, дефект A): на паузе он заморожен осознанно, и
        // правило «время вперёд» в `ApproveStep` превращало нажатие «Пауза» в аварийный останов с
        // ложной причиной «watchdog: нет данных». Живость данных, лимиты и зазор проверяются
        // по-прежнему — исчезла только проверка, которая к стоящей позе не относится.
        if (StandingPoseNeedsStop(gate, q, clearance, validator, out reason))
        {
            Stop(reason);
        }
    }

    private void Update()
    {
        if (!running || active == null) return;

        // ПАУЗА — НЕ «ОТДЫХ ОТ ПРОВЕРОК» (исправлено 30.10.2026, S44).
        //
        // ЧТО БЫЛО. На паузе `Update` выходил СРАЗУ после `NotifyState()`, то есть до расчёта
        // зазора и до `ApproveStep`. Робот стоит — но никто не следит, что он стоит в безопасном
        // месте.
        //
        // ЧЕМ ЭТО ПЛОХО, и это не теория: аварийный останов В САМОМ ПРОЕКТЕ СТАВИТ ПАУЗУ
        // (`KvSafetyTools.StopMotion` → `features.EmergencyStop()` + `Motion.SetPaused(true)`).
        // То есть после аварии робот оказывается в состоянии, где контроль зазора и лимитов
        // выключен. Если в это время оператор поправит обстановку, пододвинет стол или манекен —
        // система об этом не узнает: проверок нет, пока не снимут паузу.
        //
        // ЧТО СТАЛО. Позиция НЕ меняется (playhead стоит), но проверка выполняется: зазор и лимиты
        // считаются для текущей позы. Отказ обрабатывается так же, как в движении — аварийным
        // стопом с ПРИЧИНОЙ. Если на паузе стало небезопасно, оператор увидит это сразу, а не
        // после снятия паузы, когда робот уже поедет.
        if (Paused)
        {
            gate?.NotifyState();
            CheckStandingPose();
            return;
        }

        gate?.NotifyState(); // живые данные для watchdog

        // ВРЕМЯ КАДРА — ЧЕРЕЗ ЧИСТУЮ ФУНКЦИЮ (`PlayheadAfterFrame`): на паузе оно не идёт, и это
        // правило теперь проверяется тестом, а не поиском «playhead» в исходнике.
        playhead = PlayheadAfterFrame(playhead, Time.deltaTime, timeScale, Paused);

        // Находим текущий сэмпл.
        float[] times = active.Times;
        while (index + 1 < times.Length && times[index + 1] <= playhead) index++;

        double[] q;
        if (index + 1 >= times.Length)
        {
            q = active.Path[active.Path.Length - 1];
        }
        else
        {
            float t0 = times[index], t1 = times[index + 1];
            float k = t1 > t0 ? Mathf.Clamp01((float)((playhead - t0) / (t1 - t0))) : 0f;
            double[] a = active.Path[index], b = active.Path[index + 1];
            q = new double[a.Length];
            for (int i = 0; i < a.Length; i++) q[i] = a[i] + (b[i] - a[i]) * k;
        }

        // Валидация шага и применение.
        float clearance = validator.ClearanceAt(q, world, out _, out _);
        bool ok = true;
        if (gate != null)
            ok = gate.ApproveStep(q, clearance, playhead, validator, out SafetyReason reason);

        if (!ok)
        {
            Stop(gate != null ? gate.LastReason : SafetyReason.NoData);
            return;
        }

        validator.Apply(q);

        if (playhead >= times[times.Length - 1])
        {
            running = false;
            active = null;
            StatusText = "выполнено";
        }
    }
}
