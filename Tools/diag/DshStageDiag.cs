using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KazistovVvUI;
using KazistovVvFeatures;
using TrajectoryCore;

/// <summary>
/// ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится): сквозная проверка
/// ЭТАПОВ 1–8 сессии 15.09.2026 в PlayMode.
///
/// Запуск (по §9 PROJECT_CONTEXT.md):
///   Unity.exe -batchmode -nographics -projectPath "&lt;копия&gt;" -executeMethod DshStageDiag.Run -logFile _dsh_stages.log
/// (БЕЗ -quit), отчёт — `KavistovVv/_dsh_stage_verify.txt`.
///
/// Проверяются ИМЕННО требования ТЗ:
///   * этап 1 — фантомы используют ТЕ ЖЕ материалы, что робот (без копий/прозрачности/цвета);
///   * этап 2 — 7 языков, переключение, fallback на английский, CJK-шрифт, PlayerPrefs;
///   * этап 3 — имена файлов по шаблону, папка «Видео», скриншот и запись не падают;
///   * этап 4 — сингулярность запястья распознаётся, зоны включаются, манёвренность в 0..1;
///   * этап 5 — waypoint добавляется, достижимость проверяется, маршрут строится/не строится,
///     точки удаляются и меняют порядок;
///   * этап 6 — температура растёт при движении и падает в покое, файл данных пишется;
///   * этап 7 — тележка существует, попадает в мир столкновений и исчезает при выключении,
///     риск траектории считается;
///   * этап 8 — пульт собирается в двух вариантах, экран показывает TCP, джойстик двигает сустав.
/// </summary>
public static class DshStageDiag
{
    private const string Key = "DshStageDiag.Running";
    private static string report;

    public static void Run()
    {
        report = FeatureStorage.ReportPath("_dsh_stage_verify.txt");
        try { File.WriteAllText(report, "=== DshStageDiag · этапы 1–8 ===\n"); }
        catch (Exception e) { Debug.LogError("[DshStageDiag] отчёт не создан: " + e.Message); }

        EditorSceneManager.OpenScene("Assets/_Project/00_Scenes/MainScene.unity",
            OpenSceneMode.Single);

        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    // ------------------------------------------------------------------ состояние прогона
    private static int step;
    private static int waited;
    private static bool playStarted;
    private static int ok, fail, info;

    private static KazistovVvUIManager ui;
    private static FeatureHub hub;
    private static KvStageHub stages;
    private static TrajectoryFlowController flow;
    private static RobotController robot;
    private static PoseValidator v;

    private static string originalLanguage;
    private static Vector3 targetPoint;
    private static double[] savedPose;

    private static void Line(string text)
    {
        try { File.AppendAllText(report, text + "\n"); } catch { }
        Debug.Log("[DshStageDiag] " + text);
    }

    private static void Check(bool condition, string what, string detail = "")
    {
        if (condition) { ok++; Line("[OK]   " + what + (detail.Length > 0 ? " — " + detail : "")); }
        else { fail++; Line("[FAIL] " + what + (detail.Length > 0 ? " — " + detail : "")); }
    }

    private static void Note(string what) { info++; Line("[info] " + what); }

    /// <summary>Пропустить N игровых кадров (в batch EditorApplication.update зовётся чаще).</summary>
    private static bool WaitGame(int frames)
    {
        if (waited >= frames) { waited = 0; return true; }
        waited++;
        return false;
    }

    // ------------------------------------------------------------------ основной цикл
    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (playStarted)
            {
                Line("PlayMode завершился неожиданно");
                Finish();
            }
            return;
        }
        playStarted = true;

        try
        {
            switch (step)
            {
                case 0: StepBoot(); break;
                case 1: StepLocalization(); break;
                case 2: StepLockPoint(); break;
                case 3: StepWaitCandidates(); break;
                case 4: StepPhantoms(); break;
                case 5: StepSingularities(); break;
                case 6: StepWaypoints(); break;
                case 7: StepHealth(); break;
                case 8: StepObstacles(); break;
                case 9: StepPendant(); break;
                case 10: StepCapture(); break;
                case 11: StepFinishReport(); break;
            }
        }
        catch (Exception e)
        {
            fail++;
            Line("[FAIL] ИСКЛЮЧЕНИЕ на шаге " + step + ": " + e.GetType().Name + " " + e.Message +
                 "\n" + e.StackTrace);
            step++;
        }
    }

    // ------------------------------------------------------------------ шаги

    private static void StepBoot()
    {
        ui = KazistovVvUIManager.Instance;
        if (ui == null || ui.Flow == null) return;
        flow = ui.Flow;
        if (flow.Validator == null || !flow.Validator.Ready) return;

        hub = FeatureHub.Current;
        stages = KvStageHub.Current;

        Check(hub != null, "хаб этапов 1–20 поднят");
        Check(stages != null, "хаб этапов 1–8 поднят");
        if (stages == null) { step = 11; return; }

        robot = flow.Robot;
        v = flow.Validator;
        savedPose = v.CopyCurrent();
        originalLanguage = KvLoc.PreferenceCode;

        // Точка для проверок: смещение от базы робота (гарантированно в рабочей зоне).
        targetPoint = robot.transform.position + new Vector3(0.10f, 0.10f, -0.60f);

        Note("робот: " + robot.robotName + " · осей " + v.Dof +
             " · база " + Flow0(robot.transform.position));
        Note("язык на входе: " + KvLoc.PreferenceCode + " (" + KvLoc.CurrentCode + ")");

        step = 1;
        waited = 0;
    }

    private static void StepLocalization()
    {
        if (!WaitGame(2)) return;

        // --- этап 2: состав языков и каталоги
        IReadOnlyList<KvLangInfo> langs = KvLoc.Languages;
        Check(langs.Count >= 7, "языков в словаре ≥ 7", "найдено " + langs.Count);
        string[] need = { "ru", "en", "zh", "es", "de", "fr", "ja" };
        string found = "";
        foreach (string code in need)
        {
            bool has = false;
            for (int i = 0; i < langs.Count; i++)
                if (string.Equals(langs[i].code, code, StringComparison.OrdinalIgnoreCase)) has = true;
            if (!has) found += code + " ";
        }
        Check(found.Length == 0, "все 7 требуемых языков на месте", found.Length > 0 ? "нет: " + found : "");

        int ruCount = KvLoc.CountOf("ru");
        int enCount = KvLoc.CountOf("en");
        Check(ruCount > 100 && enCount > 100, "строк в словарях ru/en > 100",
            "ru=" + ruCount + " en=" + enCount);

        // --- переключение и мгновенное применение
        KvLoc.SetLanguage("en");
        string enToolbar = KvLoc.Cmd("point.select", "Выбор точки (красный лазер)");
        bool enApplied = KvLoc.CurrentCode == "en" && enToolbar.IndexOf("point", StringComparison.OrdinalIgnoreCase) >= 0;
        Check(enApplied, "язык EN применён мгновенно", "заголовок команды: " + enToolbar);

        KvLoc.SetLanguage("ru");
        string ruToolbar = KvLoc.Cmd("point.select", "Выбор точки (красный лазер)");
        Check(ruToolbar != enToolbar, "тексты РАЗНЫХ языков различаются", "RU: " + ruToolbar);

        KvLoc.SetLanguage("de");
        string deToolbar = KvLoc.Cmd("point.select", "Выбор точки (красный лазер)");
        Check(deToolbar != ruToolbar && KvLoc.CurrentCode == "de", "язык DE применён", deToolbar);

        // --- CJK-шрифт
        KvLoc.SetLanguage("zh");
        Check(KvLoc.NeedsCjk, "для ZH включается CJK-режим шрифта");
        string zhText = KvLoc.T("cmd.point.select", "");
        Check(!string.IsNullOrEmpty(zhText) && zhText != ruToolbar, "ZH-перевод отдаётся", zhText);
        Note("шрифт для ZH: " + KvLoc.FontName);
        UnityEngine.Font zhFont = KvLoc.UiFont(13);
        Check(zhFont != null, "шрифт интерфейса получен", zhFont != null ? zhFont.name : "null");

        KvLoc.SetLanguage("ja");
        Check(KvLoc.NeedsCjk, "для JA включается CJK-режим шрифта");
        Note("шрифт для JA: " + KvLoc.FontName);

        // --- fallback: английский каталог обязан покрывать ВСЕ ключи русского
        List<string> ruKeys = KvLoc.KeysOf("ru");
        List<string> enKeys = KvLoc.KeysOf("en");
        var enSet = new HashSet<string>(enKeys);
        int missing = 0;
        string missingSample = "";
        foreach (string key in ruKeys)
        {
            if (enSet.Contains(key)) continue;
            missing++;
            if (missingSample.Length == 0) missingSample = key;
        }
        Check(ruKeys.Count > 100 && missing == 0,
            "английский каталог покрывает ВСЕ ключи (fallback на английский работает)",
            "ru=" + ruKeys.Count + " en=" + enKeys.Count +
            (missing > 0 ? " · нет: " + missing + " (например " + missingSample + ")" : ""));

        // Исходный текст возвращается, когда перевода нет нигде.
        KvLoc.SetLanguage("es");
        string kept1 = KvLoc.T("совсем.нет.такого.ключа", "исходный русский текст");
        Check(kept1 == "исходный русский текст",
            "при отсутствии ключа возвращается исходный текст (fallback последнего уровня)", kept1);

        // --- PlayerPrefs
        KvLoc.SetLanguage("fr");
        string kept = PlayerPrefs.GetString(KvLoc.PrefsKey, "");
        Check(kept == "fr", "выбор языка сохранён в PlayerPrefs", "Prefs=" + kept);

        KvLoc.SetLanguage("system");
        Check(KvLoc.PreferenceCode == "system", "режим «системный» сохраняется",
            "фактически: " + KvLoc.CurrentCode);

        // --- интерфейс пересобрался и жив (тулбар/меню/дерево)
        Check(KvCommands.All.Count > 20, "интерфейс пережил смену языка",
            "команд в реестре: " + KvCommands.All.Count);

        step = 2;
        waited = 0;
    }

    private static void StepLockPoint()
    {
        bool locked = flow.LockPointFromUi(targetPoint, targetPoint, Vector3.up, false);
        Check(locked, "точка зафиксирована для проверок", Flow0(targetPoint));
        step = 3;
        waited = 0;
    }

    private static void StepWaitCandidates()
    {
        if (flow.State.candidates.Count > 0 && flow.Phantoms.Count > 0) { step = 4; waited = 0; return; }
        if (waited > 900)   // ~15 с игрового времени
        {
            Check(false, "траектории и фантомы построены",
                "вариантов " + flow.State.candidates.Count + " · фантомов " + flow.Phantoms.Count);
            step = 4;
            waited = 0;
            return;
        }
        waited++;
    }

    private static void StepPhantoms()
    {
        // ---------- ЭТАП 1: фантомы без визуальной разницы
        int phantoms = flow.Phantoms.Count;
        Check(phantoms > 0, "фантомы созданы", "штук: " + phantoms);
        if (phantoms == 0) { step = 5; return; }

        // Материалы реального робота (ссылки).
        var robotMats = new HashSet<Material>();
        foreach (Renderer r in robot.GetComponentsInChildren<Renderer>(true))
            if (r != null && r.sharedMaterial != null) robotMats.Add(r.sharedMaterial);

        var phantomRoots = new List<GameObject>();
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include))
        {
            if (t == null || t.parent != null) continue;
            if (t.name == "Phantoms")
                foreach (Transform child in t) phantomRoots.Add(child.gameObject);
        }

        Check(phantomRoots.Count > 0, "контейнер фантомов найден в сцене",
            "копий: " + phantomRoots.Count);

        int renderers = 0, sameMaterial = 0, transparent = 0, instanceMats = 0;
        foreach (GameObject root in phantomRoots)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                renderers++;
                Material m = r.sharedMaterial;
                if (m == null) continue;
                if (robotMats.Contains(m)) sameMaterial++;
                if (m.HasProperty("_SurfaceType"))
                {
                    if (m.GetFloat("_SurfaceType") > 0.5f) transparent++;
                }
                else if (m.renderQueue >= 3000) transparent++;
                // ВАЖНО: имя материала может само содержать «(Instance)» (материалы сцены
                // получены из HDRP-конвертера), поэтому «инстанс» определяется НЕ по имени,
                // а по тому, что материал отсутствует в наборе материалов робота.
                if (!robotMats.Contains(m)) instanceMats++;
            }
        }

        Check(renderers > 0, "рендереры фантомов найдены", "всего: " + renderers);
        Check(sameMaterial == renderers && renderers > 0,
            "ВСЕ материалы фантомов — ТЕ ЖЕ, что у робота (sharedMaterial, без копий)",
            "совпало " + sameMaterial + " из " + renderers);
        Check(transparent == 0, "прозрачных материалов у фантомов нет", "прозрачных: " + transparent);
        Check(instanceMats == 0, "материалов-инстансов у фантомов нет", "инстансов: " + instanceMats);

        bool hidden = true;
        foreach (GameObject root in phantomRoots)
            if ((root.hideFlags & HideFlags.HideInHierarchy) == 0) hidden = false;
        Check(hidden, "фантомы скрыты из Hierarchy (HideInHierarchy)");

        Note("фантомов " + phantomRoots.Count + " · рендереров " + renderers);
        step = 5;
        waited = 0;
    }

    private static void StepSingularities()
    {
        // ---------- ЭТАП 4: сингулярности
        stages.Singularities.SetEnabled(true);
        stages.Singularities.Evaluate();

        bool mobilityOk = stages.Singularities.Mobility >= 0f && stages.Singularities.Mobility <= 1f;
        Check(mobilityOk, "манёвренность (σmin / эталон) в диапазоне 0..1",
            "σ " + (stages.Singularities.Mobility * 100f).ToString("0") + " %");
        Check(!string.IsNullOrEmpty(stages.Singularities.Status), "статус сингулярностей формируется",
            stages.Singularities.Status);

        if (v.Dof >= 6)
        {
            // Искусственно ставим запястье в сингулярность (|q5| → 0) и проверяем распознавание.
            double[] q = new double[v.Dof];
            v.CopyCurrentInto(q);
            q[4] = 0.0;
            q = v.ContinueFrom(v.CopyCurrent(), q);
            if (v.WithinLimits(q))
            {
                v.Apply(q);
                stages.Singularities.Evaluate();
                Check(stages.Singularities.Kind == KvSingularityKind.Wrist,
                    "сингулярность ЗАПЯСТЬЯ распознана", "тип " + stages.Singularities.Kind);
                Check(stages.Singularities.Danger, "состояние помечено ОПАСНЫМ (красный)");
                Note("зоны вокруг суставов: " + stages.Singularities.VisibleZones);
            }
            else Note("поза с q5 = 0 вне лимитов — проверка запястья пропущена");

            // Уводим запястье из сингулярности (q5 = 45°) → предупреждение обязано сняться.
            double[] safe = new double[v.Dof];
            v.CopyCurrentInto(safe);
            safe[4] = 45.0;
            safe = v.ContinueFrom(v.CopyCurrent(), safe);
            if (v.WithinLimits(safe)) v.Apply(safe);
            stages.Singularities.Evaluate();
            Check(stages.Singularities.Kind == KvSingularityKind.None ||
                  !stages.Singularities.Danger,
                "при выходе из сингулярности зона гаснет",
                "тип " + stages.Singularities.Kind + " · опасность " + stages.Singularities.Danger);
        }
        else
        {
            // SCARA: проверяем вытягивание в плоскости.
            Note("SCARA (осей " + v.Dof + "): проверка вытягивания/складывания");
            Check(!string.IsNullOrEmpty(stages.Singularities.Status), "статус SCARA-сингулярностей формируется");
        }

        step = 6;
        waited = 0;
    }

    private static void StepWaypoints()
    {
        // ---------- ЭТАП 5: waypoint-редактор
        // Ставим робота в «удобную» позу: проверяем ЛОГИКУ промежуточных точек, а не поведение
        // планировщика в сингулярной конфигурации (её отдельно проверяет этап 4).
        double[] neutral = new double[v.Dof];
        v.CopyCurrentInto(neutral);
        if (v.Dof >= 6)
        {
            neutral[0] = 0.0; neutral[1] = 35.0; neutral[2] = -55.0;
            neutral[3] = 0.0; neutral[4] = 45.0; neutral[5] = 0.0;
        }
        neutral = v.ContinueFrom(v.CopyCurrent(), neutral);
        if (v.WithinLimits(neutral)) v.Apply(neutral);

        KvWaypointManager wp = stages.Waypoints;

        Vector3 near = targetPoint + new Vector3(0f, 0.14f, 0f);   // выше стола — у руки есть запас
        bool added = wp.Add(near, "диагностика");
        Check(added && wp.Count == 1, "waypoint добавлен", "точек: " + wp.Count);

        KvWaypoint first = wp.Count > 0 ? wp.Items[0] : null;
        Check(first != null && first.Reachable && first.Pose != null, "waypoint проверен на достижимость",
            first != null ? first.Note : "нет");

        bool route = wp.RebuildRoute();
        Check(route, "маршрут через waypoint построен штатным планировщиком",
            wp.RouteNote);
        if (wp.HasRoute)
        {
            PlannedTrajectory plan = wp.Route;
            Check(plan.Path != null && plan.Path.Length > 2, "в маршруте есть сэмплы",
                "сэмплов " + (plan.Path != null ? plan.Path.Length : 0));
            double[] cur = v.CopyCurrent();
            Vector3 start = v.TcpAt(plan.Path[0]);
            Vector3 now = v.TcpAt(cur);
            Check(Vector3.Distance(start, now) < 0.35f, "маршрут начинается в текущей позе робота",
                "смещение " + (Vector3.Distance(start, now) * 1000f).ToString("0") + " мм");
            Check(plan.Length > 0.01f, "длина маршрута посчитана",
                plan.Length.ToString("0.000") + " м · запас " + plan.LimitMargin.ToString("0.0") + "°");
        }

        // Порядок и перемещение
        Vector3 pos0 = wp.Items[0].Position;
        wp.SetPosition(0, pos0 + new Vector3(0.05f, 0f, 0f));
        Check(Vector3.Distance(wp.Items[0].Position, pos0) > 0.01f, "waypoint перемещён");

        // Недостижимый waypoint: далеко за пределами руки → красный, маршрут НЕ строится
        Vector3 far = robot.transform.position + new Vector3(4.0f, 1.2f, -4.0f);
        wp.Add(far, "диагностика (недостижимая)");
        bool unreachableMarked = wp.UnreachableCount > 0;
        Check(unreachableMarked, "недостижимый waypoint помечен", "недостижимых: " + wp.UnreachableCount);
        bool routeBlocked = !wp.RebuildRoute();
        Check(routeBlocked, "маршрут с недостижимым waypoint НЕ строится", wp.RouteNote);

        wp.RemoveLast();
        Check(wp.Count == 1, "недостижимый waypoint удалён", "точек: " + wp.Count);

        bool rebuilt = wp.RebuildRoute();
        Check(rebuilt, "после удаления маршрут снова строится", wp.RouteNote);

        wp.Remove(0);
        Check(wp.Count == 0 && !wp.HasRoute, "удаление точки снимает маршрут",
            "точек: " + wp.Count);

        step = 7;
        waited = 0;
    }

    private static void StepHealth()
    {
        // ---------- ЭТАП 6: мониторинг состояния
        KvHealthMonitor h = stages.Health;
        h.SetEnabled(true);
        h.Rebind(robot);
        Check(h.Dof == v.Dof, "число суставов в мониторинге совпало с роботом",
            "dof " + h.Dof);

        float before = h.Temperature != null ? h.Temperature[0] : 0f;

        // Имитируем движение: качаем первый сустав и снимаем телеметрию.
        double[] q = v.CopyCurrent();
        for (int i = 0; i < 60; i++)
        {
            q[0] += (i % 2 == 0 ? 3.0 : -3.0);
            q = v.ContinueFrom(v.CopyCurrent(), q);
            if (v.WithinLimits(q)) v.Apply(q);
            h.Sample(0.05f);
        }
        float afterMove = h.Temperature[0];
        Check(afterMove > before, "температура РАСТЁТ при движении",
            before.ToString("0.00") + " → " + afterMove.ToString("0.00") + " °C");

        float currentPeak = 0f;
        for (int i = 0; i < h.Current.Length; i++) currentPeak = Mathf.Max(currentPeak, h.Current[i]);
        Check(currentPeak > 0f, "ток считается", "макс " + currentPeak.ToString("0.00") + " А");

        float speedPeak = 0f;
        for (int i = 0; i < h.Speed.Length; i++) speedPeak = Mathf.Max(speedPeak, Mathf.Abs(h.Speed[i]));
        Check(speedPeak > 0f, "скорость суставов считается",
            "макс " + speedPeak.ToString("0.000") + " (рад/с или м/с)");

        // Покой: температура обязана падать к температуре среды.
        v.Apply(savedPose);
        for (int i = 0; i < 200; i++) h.Sample(0.05f);
        float afterRest = h.Temperature[0];
        Check(afterRest < afterMove, "температура ПАДАЕТ в покое",
            afterMove.ToString("0.00") + " → " + afterRest.ToString("0.00") + " °C");

        bool wearOk = true;
        for (int i = 0; i < h.Wear.Length; i++)
            if (h.Wear[i] < 0f || h.Wear[i] > 1f) wearOk = false;
        Check(wearOk, "износ в диапазоне 0..1");

        int wj; float hours = h.ForecastHours(false, out wj);
        Note("прогноз ресурса: " + (hours < 0f ? "—" : hours.ToString("0.0") + " ч") +
             " (сустав " + (wj + 1) + ")");

        Check(!string.IsNullOrEmpty(h.LogPath) && File.Exists(h.LogPath),
            "данные мониторинга пишутся в файл", h.LogPath);
        for (int i = 0; i < 5; i++) h.Tick(1.1f);      // строка в файл раз в logInterval
        Check(h.LogRows > 0, "строки телеметрии записаны в файл", "строк: " + h.LogRows);

        stages.HealthPanel.SetVisible(true);
        stages.HealthPanel.Redraw();
        Check(stages.HealthPanel.Visible, "панель графиков показана");

        step = 8;
        waited = 0;
    }

    private static void StepObstacles()
    {
        // ---------- ЭТАП 7: динамические препятствия
        KvDynamicObstacleService o = stages.Obstacles;
        o.SetEnabled(true);
        Check(o.Enabled, "динамическое препятствие включено", o.Status);

        GameObject cart = GameObject.Find("ДинамическоеПрепятствие_Тележка");
        Check(cart != null, "тележка создана в сцене");

        if (cart != null)
        {
            bool hidden = (cart.hideFlags & HideFlags.HideInHierarchy) != 0;
            Check(!hidden, "тележка НЕ считается служебным объектом (иначе выпала бы из CollisionWorld)");
        }

        // Мир столкновений строится по рендерерам; проверяем, что тележка в нём есть.
        hub.World.Rebuild(robot, v.linkRadius);
        int found = 0;
        foreach (ObstacleBox b in hub.World.Boxes)
            if (b.name != null && b.name.StartsWith("ДинамическоеПрепятствие", StringComparison.Ordinal)) found++;
        foreach (ObstacleCapsule c in hub.World.Capsules)
            if (c.name != null && c.name.StartsWith("ДинамическоеПрепятствие", StringComparison.Ordinal)) found++;
        Check(found > 0, "тележка попала в мир столкновений (планировщик её видит)",
            "объектов в мире: " + found);

        // Движение: фаза меняется, позиция меняется.
        Vector3 p0 = o.Position;
        for (int i = 0; i < 30; i++) o.Tick(0.05f);
        Check(Vector3.Distance(o.Position, p0) > 0.05f, "тележка едет по маршруту",
            Flow0(o.Position) + " · пройдено " + o.Traveled.ToString("0.00") + " м");

        // Прогноз риска для настоящей траектории.
        double[] cur = v.CopyCurrent();
        double[] probeGoal = (double[])cur.Clone();
        probeGoal[0] += 12.0;
        probeGoal = v.ContinueFrom(cur, probeGoal);
        if (!v.WithinLimits(probeGoal)) probeGoal = (double[])cur.Clone();
        PlannedTrajectory probe = KvPlanKit.MakeJointPlan(v, hub.World, cur,
            probeGoal, "проба риска", 0.2f, 24);
        if (probe != null)
        {
            KvRiskResult risk = o.Evaluate(probe);
            Check(risk.level != KvPlanRisk.Unknown, "риск траектории посчитан",
                risk.level + " · мин. интервал " + (risk.minDistance * 100f).ToString("0") + " см · " +
                risk.reason);
        }
        else Note("пробный план не построен — оценка риска пропущена");

        // Реакция на приближение: проверяем обработчик аварийной остановки.
        Check(o.stopRequest != null, "обработчик реакции (аварийная остановка) привязан");

        // Выключение убирает тележку из мира расчётов.
        o.SetEnabled(false);
        hub.World.Rebuild(robot, v.linkRadius);
        int after = 0;
        foreach (ObstacleBox b in hub.World.Boxes)
            if (b.name != null && b.name.StartsWith("ДинамическоеПрепятствие", StringComparison.Ordinal)) after++;
        foreach (ObstacleCapsule c in hub.World.Capsules)
            if (c.name != null && c.name.StartsWith("ДинамическоеПрепятствие", StringComparison.Ordinal)) after++;
        Check(after == 0, "при выключении тележка исчезает из мира столкновений",
            "осталось объектов: " + after);

        step = 9;
        waited = 0;
    }

    private static void StepPendant()
    {
        // ---------- ЭТАП 8: виртуальный пульт
        KvTeachPendant p = stages.Pendant;
        p.SetVisible(true);
        Check(p.Visible, "пульт показан");

        p.Refresh();
        Check(!string.IsNullOrEmpty(p.ScreenTcpText), "экран показывает координаты TCP",
            p.ScreenTcpText != null ? p.ScreenTcpText.Replace("\n", " | ") : "");

        p.SetVariant(KvPendantVariant.Addon);
        p.Refresh();
        Check(p.Variant == KvPendantVariant.Addon, "вариант B (АДДОН) применён", p.Status);
        p.SetVariant(KvPendantVariant.Industrial);
        p.Refresh();
        Check(p.Variant == KvPendantVariant.Industrial, "вариант A (промышленный) применён", p.Status);

        p.SetAttach(KvPendantAttach.FollowCamera);
        p.Tick(0.05f);
        Check(p.Attach == KvPendantAttach.FollowCamera, "пульт можно закрепить у оператора");
        p.SetAttach(KvPendantAttach.Floating);

        Check(p.stopRequest != null && p.homeRequest != null && p.recordPoseRequest != null &&
              p.startRequest != null, "кнопки пульта привязаны к действиям (СТОП/ДОМОЙ/ПОЗА/ПУСК)");

        // Джойстик: выдаёт сигнал и двигает сустав.
        p.SetJogMode(KvPendantJog.Joint);
        p.SetJogAxis(0);
        double[] before = v.CopyCurrent();
        p.SetStick(new Vector2(0f, 1f));
        Check(p.JoystickVector.y > 0.9f, "джойстик выдаёт вектор",
            "(" + p.JoystickVector.x.ToString("0.00") + ", " + p.JoystickVector.y.ToString("0.00") + ")");

        for (int i = 0; i < 12; i++) p.Tick(0.05f);
        double[] after = v.CopyCurrent();
        double delta = Math.Abs(after[0] - before[0]);
        Check(delta > 0.5, "джойстик ДВИГАЕТ сустав",
            "изменение J1: " + delta.ToString("0.00") + "°");
        p.SetStick(Vector2.zero);

        // Режим TCP
        p.SetJogMode(KvPendantJog.World);
        Check(p.JogMode == KvPendantJog.World, "режим джойстика переключается (JOINT / TCP)");
        p.SetJogMode(KvPendantJog.Joint);

        v.Apply(savedPose);

        step = 10;
        waited = 0;
    }

    private static void StepCapture()
    {
        // ---------- ЭТАП 3: экспорт демонстраций
        KvCaptureService c = stages.Capture;

        string shot = KvCaptureService.ScreenshotName(new DateTime(2026, 9, 15, 14, 3, 7));
        string rec = KvCaptureService.RecordingName(new DateTime(2026, 9, 15, 14, 3, 7));
        Check(Regex.IsMatch(shot, @"^KazistovVv_screenshot_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.png$"),
            "имя скриншота по шаблону ТЗ", shot);
        Check(Regex.IsMatch(rec, @"^KazistovVv_recording_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}$"),
            "имя записи по шаблону ТЗ", rec + ".mp4");
        Check(Directory.Exists(c.Folder), "папка экспорта существует (системная «Видео»)", c.Folder);
        Check(c.Folder.IndexOf("Videos", StringComparison.OrdinalIgnoreCase) >= 0 ||
              c.Folder.IndexOf("Видео", StringComparison.OrdinalIgnoreCase) >= 0,
            "папка экспорта — стандартная папка «Видео»", c.Folder);

        Check(c.annotate, "подпись на скриншоте включена по умолчанию");

        // В -nographics графического устройства нет: проверяем, что вызов не падает,
        // а сервис сообщает об этом понятной строкой.
        bool started = c.TakeScreenshot("Idle · ожидание", robot.robotName);
        Check(started, "запрос скриншота принят");

        bool video = c.StartRecording();
        Check(video, "запись видео стартовала", c.LastRecordingFolder);
        c.Tick();
        c.Tick();
        c.StopRecording();
        Check(!c.IsRecording, "запись видео остановлена",
            "кадров/файл: " + c.LastVideoPath);
        Note("последний снимок: " + (string.IsNullOrEmpty(c.LastScreenshotPath)
            ? "(в -nographics снимок недоступен)" : c.LastScreenshotPath));

        step = 11;
        waited = 0;
    }

    private static void StepFinishReport()
    {
        // Возврат языка и позы — прогон не должен менять настройки оператора.
        KvLoc.SetLanguage(originalLanguage);
        if (v != null && v.Ready && savedPose != null) v.Apply(savedPose);
        if (stages != null && stages.Waypoints != null) stages.Waypoints.Clear("конец диагностики");
        if (stages != null && stages.Obstacles != null) stages.Obstacles.SetEnabled(false);

        Line("");
        Line("ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · [info] " + info);
        Line(fail == 0 ? "ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ИТОГ: ЕСТЬ ОТКАЗЫ (" + fail + ")");
        Finish();
    }

    private static void Finish()
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(fail == 0 ? 0 : 1);
    }

    private static string Flow0(Vector3 p)
    {
        return "(" + p.x.ToString("0.000") + ", " + p.y.ToString("0.000") + ", " +
               p.z.ToString("0.000") + ")";
    }
}
