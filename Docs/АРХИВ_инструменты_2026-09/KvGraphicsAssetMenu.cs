#if UNITY_EDITOR
using KazistovVvFeatures;
using UnityEditor;
using UnityEngine;

/// <summary>
/// §30 (ЗАДАЧА 2). Пункт меню для ОТЛОЖЕННОГО применения настроек конвейера HDRP.
///
/// Зачем он нужен. До §30 служба графики (`KvGraphicsService.ApplyAsset`) правила
/// ПРОЕКТНЫЙ HDRP-ассет прямо из PlayMode: присвоение
/// `HDRenderPipelineAsset.currentPlatformRenderPipelineSettings` вызывает в HDRP
/// `OnValidate()` (см. `HDRenderPipelineAsset.cs`, строка 140), тот пересобирает конвейер
/// целиком и помечает ассет «грязным». Грязный ассет Unity сохраняет на диск при ближайшем
/// `AssetDatabase.Refresh` — отсюда строки `Asset Pipeline Refresh` в `Logs\Editor.log`.
/// Плюс на выходе из PlayMode ассет правился ВТОРОЙ раз
/// (`KvGraphicsRuntime.OnApplicationQuit` → `RestoreAsset`).
///
/// Что теперь. В PlayMode файл проекта не меняется вообще: настройки конвейера считаются
/// и кэшируются. Применить их к проекту можно ТОЛЬКО вне воспроизведения — этим пунктом
/// меню (или программно: `KvGraphicsService.ApplyAssetOutsidePlayMode()`).
///
/// ВАЖНО (честно): фильтры конвейера (MSAA, трассировка лучей, размер атласа отражений,
/// дистанция декалей, поддержка SSAO/SSR/объёмного тумана) — это НАСТРОЙКИ ПРОЕКТА,
/// а не рантайма; их смена всегда пересобирает конвейер. Поэтому порядок такой:
/// сначала применить здесь, потом входить в PlayMode. Всё остальное (QualitySettings,
/// сглаживание камеры, глобальный том, рельеф) по-прежнему работает из PlayMode сразу.
/// </summary>
public static class KvGraphicsAssetMenu
{
    private const string MenuRoot = "Tools/KazistovVv/Графика/";

    /// <summary>Применить рассчитанные настройки конвейера к проектному HDRP-ассету.</summary>
    [MenuItem(MenuRoot + "Применить настройки конвейера к проекту (вне PlayMode)")]
    public static void ApplyAsset()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Графика · настройки конвейера",
                "Идёт воспроизведение (PlayMode).\n\n" +
                "Файл HDRP-ассета в рантайме НЕ меняется — иначе каждое сохранение\n" +
                "грязного ассета даёт «Asset Pipeline Refresh» (§30).\n\n" +
                "Остановите воспроизведение и повторите команду.",
                "Понятно");
            return;
        }

        KvGraphicsService service = KvGraphicsService.EnsureStarted();
        if (service == null)
        {
            Debug.LogWarning("[Graphics] §30: служба графики не запущена — применять нечего");
            return;
        }

        string report = service.ApplyAssetOutsidePlayMode();
        if (string.IsNullOrEmpty(report))
        {
            Debug.Log("[Graphics] §30: настройки конвейера уже соответствуют профилю — " +
                      "HDRP-ассет не изменялся");
            return;
        }

        // Ассет помечен грязным в памяти — сохраняем осознанно и ОДИН раз, вне PlayMode.
        AssetDatabase.SaveAssets();
        Debug.Log("[Graphics] §30: настройки конвейера применены к проекту:\n" + report);
    }

    /// <summary>Вернуть HDRP-ассету настройки, снятые перед последним применением.</summary>
    [MenuItem(MenuRoot + "Вернуть исходные настройки конвейера")]
    public static void RestoreAsset()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Графика · настройки конвейера",
                "Идёт воспроизведение (PlayMode) — файл проекта не меняется (§30).",
                "Понятно");
            return;
        }

        KvGraphicsService service = KvGraphicsService.EnsureStarted();
        bool restored = service != null && service.RestoreAsset();
        if (restored) AssetDatabase.SaveAssets();
        Debug.Log(restored
            ? "[Graphics] §30: исходные настройки HDRP-ассета восстановлены"
            : "[Graphics] §30: восстанавливать нечего — ассет не правился этой сессией");
    }

    /// <summary>Проверка состояния: есть ли рассчитанные, но не применённые настройки.</summary>
    [MenuItem(MenuRoot + "Состояние отложенных настроек")]
    public static void ReportState()
    {
        KvGraphicsService service = KvGraphicsService.EnsureStarted();
        bool pending = service != null && service.HasPendingAssetApply;
        Debug.Log("[Graphics] §30: отложенные настройки конвейера — " +
                  (pending ? "ЕСТЬ (применить вне PlayMode)" : "нет") +
                  " · сейчас " + (EditorApplication.isPlaying ? "PlayMode" : "режим редактирования"));
    }

    // ================================================================== §51 (ЗАДАЧА B)

    /// <summary>
    /// §51 (ЗАДАЧА B). ОБЛЕГЧЁННЫЙ ПРЕСЕТ ДЛЯ СЛАБОГО ЖЕЛЕЗА — вручную.
    ///
    /// ЗАЧЕМ ПУНКТ МЕНЮ, ЕСЛИ ЕСТЬ АВТОВКЛЮЧЕНИЕ. Автовключение срабатывает по данным железа
    /// (встроенная графика или VRAM &lt; 4 ГБ) при входе в PlayMode. Но у оператора должны быть
    /// обе возможности: (1) включить облегчённый режим ДО PlayMode — чтобы тяжёлый вход в
    /// воспроизведение не начинался с полной нагрузки; (2) включить его на машине, которую
    /// автопроверка «сильной» не считает, но которая фактически не тянет (или наоборот —
    /// снять, если после TdrDelay всё работает и хочется картинку получше).
    ///
    /// ЧТО ПРОИСХОДИТ В КАЖДОМ РЕЖИМЕ (честно, без «магии»):
    ///   * PlayMode — применяется рантаймовый профиль LowGPU целиком: QualitySettings,
    ///     камера, глобальный том, дальность камер 100 и тени мелких объектов;
    ///   * режим редактирования — применяются ТОЛЬКО настройки конвейера HDRP (штатный
    ///     путь §30, файл ассета); QualitySettings и камера — рантаймовые, они применятся
    ///     при входе в воспроизведение. Об этом сообщается в консоль.
    /// </summary>
    [MenuItem(MenuRoot + "Применить LowGPU-пресет (слабое железо)")]
    public static void ApplyLowGpuMenu()
    {
        KvGraphicsService service = KvGraphicsService.EnsureStarted();
        if (service == null)
        {
            Debug.LogWarning("[Graphics] §51: служба графики не запущена — применять нечего");
            return;
        }

        string auto;
        bool needed = service.LowGpuNeeded(out auto);
        string report = service.ApplyLowGpu(auto, announce: false);
        if (!Application.isPlaying) AssetDatabase.SaveAssets();

        Debug.Log("[Graphics] §51: LowGPU применён вручную" +
                  (needed ? " (автопроверка тоже говорит «нужен»)" : " (автопроверка считает железо достаточным — " + auto + ")") +
                  "\n" + report);
    }

    /// <summary>§51 (ЗАДАЧА B). Снять облегчённый пресет и вернуть обычный.</summary>
    [MenuItem(MenuRoot + "Снять LowGPU-пресет (вернуть обычный)")]
    public static void ReleaseLowGpuMenu()
    {
        KvGraphicsService service = KvGraphicsService.EnsureStarted();
        if (service == null)
        {
            Debug.LogWarning("[Graphics] §51: служба графики не запущена — снимать нечего");
            return;
        }

        string report = service.ReleaseLowGpu(announce: false);
        if (!Application.isPlaying) AssetDatabase.SaveAssets();
        Debug.Log("[Graphics] §51: " + report);
    }

    // ================================================================== §33 (ЗАДАЧА D1)

    /// <summary>
    /// §33 (ЗАДАЧА D1). ОДНА КНОПКА — ВСЯ ГРАФИКА: «Применить все настройки графики (HDRP)».
    ///
    /// Последовательность (ровно та, что требует ТЗ, каждый шаг пишется в консоль):
    ///   1. Convert Materials to HDRP — материалы роботов (`ConvertRobotMaterialsToHDRP.Convert`);
    ///   2. Convert Scene Materials to HDRP — материалы сцены (`ConvertAllInScene`);
    ///   3. Apply Studio Lighting (HDRP) — студийный свет (`HDRPAutoLighting.SetMode(Studio)`);
    ///   4. `KvGraphicsService.ApplyAssetOutsidePlayMode()` — настройки конвейера в проект
    ///      (§30, A2: только вне PlayMode, иначе файл проекта не трогаем).
    ///
    /// В PlayMode команда честно отказывает: шаг 4 там невозможен по построению (§30), и
    /// «наполовину применённая» графика хуже, чем понятный отказ. Прогресс показывается
    /// `EditorUtility.DisplayCancelableProgressBar` — операция долгая (импорт материалов).
    /// </summary>
    [MenuItem(MenuRoot + "Применить все настройки графики (HDRP)")]
    public static void ApplyAllGraphics()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Графика · применить всё",
                "Идёт воспроизведение (PlayMode).\n\n" +
                "Настройки конвейера HDRP применяются к проекту ТОЛЬКО вне PlayMode (§30, A2):\n" +
                "в рантайме файл проекта не меняется, иначе каждое сохранение грязного ассета\n" +
                "даёт «Asset Pipeline Refresh».\n\n" +
                "Остановите воспроизведение и повторите команду.",
                "Понятно");
            return;
        }

        const int steps = 4;
        int ok = 0;
        try
        {
            // --- шаг 1: материалы роботов
            EditorUtility.DisplayCancelableProgressBar("Графика · применить всё",
                "Шаг 1/4: материалы роботов (Convert Materials to HDRP)", 0.05f);
            try
            {
                ConvertRobotMaterialsToHDRP.Convert();
                ok++;
                Debug.Log("[Graphics] §33 (D1): шаг 1/4 — материалы роботов сконвертированы " +
                          "(Convert Materials to HDRP)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Graphics] §33 (D1): шаг 1/4 не выполнен: " + e.Message);
            }

            // --- шаг 2: материалы сцены
            EditorUtility.DisplayCancelableProgressBar("Графика · применить всё",
                "Шаг 2/4: материалы сцены (Convert Scene Materials to HDRP)", 0.35f);
            try
            {
                ConvertRobotMaterialsToHDRP.ConvertAllInScene();
                ok++;
                Debug.Log("[Graphics] §33 (D1): шаг 2/4 — материалы сцены сконвертированы " +
                          "(Convert Scene Materials to HDRP)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Graphics] §33 (D1): шаг 2/4 не выполнен: " + e.Message);
            }

            // --- шаг 3: студийный свет
            EditorUtility.DisplayCancelableProgressBar("Графика · применить всё",
                "Шаг 3/4: студийное освещение (Apply Studio Lighting)", 0.65f);
            try
            {
                HDRPAutoLighting.SetMode(HDRPAutoLightingMode.Studio);
                ok++;
                Debug.Log("[Graphics] §33 (D1): шаг 3/4 — применено студийное освещение " +
                          "(режим «Студийный», он же сохранён для следующих запусков)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Graphics] §33 (D1): шаг 3/4 не выполнен: " + e.Message);
            }

            // --- шаг 4: настройки конвейера в проект (только вне PlayMode)
            EditorUtility.DisplayCancelableProgressBar("Графика · применить всё",
                "Шаг 4/4: настройки конвейера HDRP в проектный ассет", 0.85f);
            try
            {
                KvGraphicsService service = KvGraphicsService.EnsureStarted();
                string report = service != null ? service.ApplyAssetOutsidePlayMode() : null;
                if (service == null)
                {
                    Debug.LogWarning("[Graphics] §33 (D1): шаг 4/4 — служба графики не запущена");
                }
                else if (string.IsNullOrEmpty(report))
                {
                    ok++;
                    Debug.Log("[Graphics] §33 (D1): шаг 4/4 — настройки конвейера уже " +
                              "соответствуют профилю, ассет не менялся");
                }
                else
                {
                    AssetDatabase.SaveAssets();     // осознанно и ОДИН раз, вне PlayMode
                    ok++;
                    Debug.Log("[Graphics] §33 (D1): шаг 4/4 — настройки конвейера применены " +
                              "к проекту:\n" + report);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Graphics] §33 (D1): шаг 4/4 не выполнен: " + e.Message);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log("[Graphics] §33 (D1): «Применить все настройки графики (HDRP)» завершено — " +
                  "шагов выполнено " + ok + " из " + steps +
                  (ok == steps ? " (звёздочка «не применено» с вкладки «Графика» снята)"
                               : " — смотрите предупреждения выше"));
    }
}
#endif
