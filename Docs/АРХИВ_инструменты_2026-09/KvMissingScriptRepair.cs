#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using KazistovVvUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// §30 (ЗАДАЧА 4). ПОЧИНКА ОТСУТСТВУЮЩИХ СКРИПТОВ НА ОБЪЕКТАХ СЦЕНЫ.
///
/// ЧТО БЫЛО НАЙДЕНО (доказано разбором `MainScene.unity`).
/// На объектах столов «Стенд_1_Стол» и «Стенд_2_Стол» Unity в консоли писала
/// `The referenced script on this Behaviour (Game Object 'Стенд_1_Стол') is missing!`
///
/// Причина — НЕ удалённый скрипт, а «оторвавшаяся» ссылка. В самой сцене лежал
/// ВСТРОЕННЫЙ (inline) документ MonoScript:
///
///     --- !u!115 &413221061
///     MonoScript:
///       m_ClassName: RegisteredObject
///       m_Namespace: KompasUI              ← ЭТОГО ПРОСТРАНСТВА ИМЁН БОЛЬШЕ НЕТ
///       m_AssemblyName: Assembly-CSharp
///
/// а компоненты столов ссылались на него так: `m_Script: {fileID: 413221061}` —
/// БЕЗ `guid`, то есть на локальный fileID внутри файла сцены, а не на файл скрипта.
/// Пространство имён `KompasUI` переименовано ещё в §0.4 (`KompasUI` → `KavistovVvUI`,
/// затем проект стал `KazistovVv`), поэтому класс `KompasUI.RegisteredObject` перестал
/// существовать, и Unity не смогла связать компонент ни с чем.
///
/// ВАЖНО: сам скрипт НИКУДА НЕ ПРОПАДАЛ — `RegisteredObject` живёт в
/// `06_KazistovVv_UI/03_Scripts/Data/RuntimeRegistry.cs` (namespace `KazistovVvUI`,
/// guid `2f4b8774f5704b7458e6e6a3af28a110`). Поэтому по ТЗ ссылка ВОССТАНАВЛИВАЕТСЯ,
/// а не удаляется.
///
/// ПОЧЕМУ ПОЧИНКА ИДЁТ ИЗ РЕДАКТОРА, А НЕ ПРАВКОЙ YAML.
/// `MainScene.unity` открыта в работающем редакторе Unity. Правка файла сцены «снаружи»
/// при живом редакторе — верный способ потерять правку: Unity держит сцену в памяти и
/// при следующем сохранении перезапишет файл своим состоянием. Здесь починка идёт
/// ШТАТНЫМИ средствами Unity (`GameObjectUtility` + `Undo`), поэтому она попадает и в
/// память редактора, и на диск, и её можно отменить (`Ctrl+Z`).
///
/// Когда работает: сразу после перезагрузки домена (то есть после компиляции скриптов),
/// один раз за сессию; плюс вручную из меню `Tools/KazistovVv/Сцена/…`.
/// </summary>
[InitializeOnLoad]
public static class KvMissingScriptRepair
{
    private const string LogTag = "[MissingScriptFix]";
    private const string MenuRoot = "Tools/KazistovVv/Сцена/";

    /// <summary>Чтобы автопочинка не крутилась по кругу при каждой перезагрузке домена.</summary>
    private static bool autoRunDone;

    /// <summary>
    /// §32 (ЗАДАЧА A3). ПОЧЕМУ ПОЧИНКА НЕ СРАБАТЫВАЛА — НАЙДЕНА НАСТОЯЩАЯ ПРИЧИНА.
    ///
    /// В §30 автопочинка была привязана ТОЛЬКО к `EditorApplication.delayCall` из статического
    /// конструктора `[InitializeOnLoad]`. При СТАРТЕ РЕДАКТОРА порядок такой: перезагрузка
    /// домена (и, значит, наш `delayCall`) происходит РАНЬШЕ, чем Unity открывает последнюю
    /// сцену. В момент вызова не было ни одной загруженной сцены, цикл по
    /// `SceneManager.sceneCount` ничего не находил, `repaired == 0`, и функция молча
    /// выходила — в консоли не было НИ ОДНОЙ строки `[MissingScriptFix]`. Это подтверждено
    /// по `Logs/Editor.log` этой сессии: скрипт импортирован (строка 796), но не напечатал
    /// ничего, а ошибки «The referenced script … is missing!» появились ПОЗЖЕ импорта —
    /// вместе с загрузкой сцены. `autoRunDone = true` к тому моменту уже стоял, поэтому
    /// второй попытки не было.
    ///
    /// ЧТО СДЕЛАНО: попытка теперь ЖДЁТ, пока сцена действительно загрузится
    /// (<see cref="Pump"/>), и повторяется при КАЖДОМ открытии сцены
    /// (<see cref="OnSceneOpened"/>) — то есть и при старте редактора, и при `Ctrl+R`
    /// (после компиляции сцена уже загружена, и починка выполняется сразу).
    /// Починка идемпотентна: если отсутствующих скриптов нет, она ничего не меняет.
    /// </summary>
    private static string pendingReason = "";
    private static int pendingFrames;

    /// <summary>
    /// Известные объекты с известным «потерянным» компонентом: имя объекта → тип компонента.
    /// Список намеренно УЗКИЙ: починка касается только тех объектов, где состав компонентов
    /// доказан разбором сцены. Всё остальное только ПОКАЗЫВАЕТСЯ (см. <see cref="Report"/>),
    /// но не правится — чтобы не «починить» лишнего.
    /// </summary>
    private static readonly Dictionary<string, System.Type> KnownRepairs =
        new Dictionary<string, System.Type>
        {
            { "Стенд_1_Стол", typeof(RegisteredObject) },
            { "Стенд_2_Стол", typeof(RegisteredObject) },
            { "Стенд_3_Стол", typeof(RegisteredObject) },
        };

    // ------------------------------------------------------------------ §50 (задача 3): второй критерий
    /// <summary>
    /// Сколько всего БИТЫХ компонентов найдено последним проходом (диагностика и тесты).
    /// </summary>
    public static int LastBrokenCount { get; private set; }
    /// <summary>Отчёт последнего прохода «сколько и на каких объектах» (диагностика и тесты).</summary>
    public static string LastBrokenReport { get; private set; } = "";
    /// <summary>Объекты, которым нужна правка сцены при закрытом Unity (штатный API их не берёт).</summary>
    public static readonly List<string> LastNeedsYamlFix = new List<string>();

    /// <summary>
    /// §50 (задача 3). СКОЛЬКО КОМПОНЕНТОВ UNITY ОТДАЁТ КАК NULL — ВТОРОЙ КРИТЕРИЙ
    /// потерянного скрипта.
    ///
    /// ПОЧЕМУ ОН НУЖЕН. `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount` возвращает
    /// 0, когда `m_Script` компонента — ЛОКАЛЬНАЯ ссылка на ВСТРОЕННЫЙ в сцену документ
    /// `MonoScript` с устаревшим `m_Namespace`. Такой документ в сцене СУЩЕСТВУЕТ, поэтому
    /// Unity считает ссылку назначенной, но класс не резолвится — и `GetComponents<Component>()`
    /// отдаёт на этом месте null, а в консоли висит «The referenced script on this Behaviour
    /// (Game Object 'Стенд_1_Стол') is missing!». Именно так 21.09.2026 остались битыми два
    /// компонента столов стендов: разбор сцены показал `--- !u!115 &413221061 MonoScript`
    /// c `m_Namespace: KompasUI` и ссылку `m_Script: {fileID: 413221061}` без guid, а починка
    /// при этом печатала «отсутствующих скриптов нет — сцена в порядке» (ложное «всё хорошо»).
    /// </summary>
    public static int CountBrokenComponents(GameObject go)
    {
        if (go == null) return 0;
        Component[] comps = go.GetComponents<Component>();
        int broken = 0;
        for (int i = 0; i < comps.Length; i++)
            if (comps[i] == null) broken++;
        return broken;
    }

    static KvMissingScriptRepair()
    {
        // 1) Перезагрузка домена (компиляция скриптов). Если сцена уже открыта — починим сразу;
        //    если нет — ждём её загрузки (см. Pump).
        EditorApplication.delayCall += delegate { ScheduleRepair("перезагрузка домена/компиляция"); };
        // 2) СЦЕНА ОТКРЫЛАСЬ — это и есть путь старта редактора (сцена грузится после домена).
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        ScheduleRepair("открыта сцена «" + scene.name + "»");
    }

    private static void ScheduleRepair(string reason)
    {
        pendingReason = reason;
        pendingFrames = 0;
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
    }

    /// <summary>
    /// §32 (A3). Ждём, пока в редакторе появится ЗАГРУЖЕННАЯ сцена, и только тогда чиним.
    /// Ожидание ограничено (~5 с): если сцены так и нет, громко сообщаем об этом —
    /// молчаливый выход и был дефектом §30.
    /// </summary>
    private static void Pump()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.update -= Pump;
            return;
        }
        if (!AnySceneLoaded())
        {
            pendingFrames++;
            if (pendingFrames < 300) return;
            EditorApplication.update -= Pump;
            Debug.LogWarning(LogTag + " автопочинка не выполнена: загруженных сцен нет " +
                             "(причина: " + pendingReason + "). Откройте сцену и вызовите " +
                             "«Tools/KazistovVv/Сцена/Починить отсутствующие скрипты (§30)».");
            return;
        }
        EditorApplication.update -= Pump;
        RepairAll(false);
    }

    private static bool AnySceneLoaded()
    {
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (scene.IsValid() && scene.isLoaded) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ починка

    /// <summary>Починить известные отсутствующие скрипты во всех загруженных сценах.</summary>
    [MenuItem(MenuRoot + "Починить отсутствующие скрипты (§30)")]
    public static void RepairFromMenu()
    {
        RepairAll(true);
    }

    private static void RepairAll(bool verbose)
    {
        int repaired = 0;
        int unknown = 0;
        StringBuilder log = new StringBuilder();
        LastNeedsYamlFix.Clear();          // §50: список «нужна правка сцены» — на каждый проход свой

        // Сцены, которые ПОСЛЕ починки нужно закрепить на диске. Разделены намеренно:
        //   * cleanBefore — до починки не имели несохранённых правок, сохраняем сразу;
        //   * dirtyBefore — несохранённые правки БЫЛИ (возможно, чужие и нужные),
        //     поэтому сохранять их автоматически нельзя — только предупредить.
        List<Scene> cleanBefore = new List<Scene>();
        List<Scene> dirtyBefore = new List<Scene>();

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded) continue;

            bool wasDirty = scene.isDirty;
            int repairedBefore = repaired;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Scan(roots[r].transform, scene, log, ref repaired, ref unknown);
            }

            if (repaired > repairedBefore)
            {
                if (wasDirty) dirtyBefore.Add(scene);
                else cleanBefore.Add(scene);
            }
        }

        if (repaired == 0)
        {
            if (unknown > 0)
            {
                Debug.LogWarning(LogTag + " объекты с НЕИЗВЕСТНЫМИ отсутствующими скриптами " +
                                 "(не тронуты, нужен ручной разбор):\n" + log);
            }
            else
            {
                // §32 (A3): строка печатается ВСЕГДА (раньше — только при ручном вызове).
                // Именно её отсутствие в `Editor.log` и показало, что автопочинка вообще
                // не выполнялась: молчаливый выход невозможно было отличить от «всё в порядке».
                if (LastNeedsYamlFix.Count > 0)
                {
                    // §50 (задача 3): молчать здесь больше нельзя — раньше именно эта ветка
                    // печатала «сцена в порядке», когда в ней были битые компоненты.
                    Debug.LogWarning(LogTag + " штатная починка не справилась: битые компоненты " +
                                     "со ссылкой на ВСТРОЕННЫЙ MonoScript есть у объектов (" +
                                     string.Join(", ", LastNeedsYamlFix.ToArray()) +
                                     "), но API Unity их не снимает. Нужна правка сцены при " +
                                     "закрытом Unity (§50, задача 3):\n" + log);
                }
                else
                {
                    Debug.Log(LogTag + " проверка выполнена: отсутствующих скриптов нет — сцена в порядке.");
                }
            }
            return;
        }

        for (int i = 0; i < cleanBefore.Count; i++)
        {
            Scene scene = cleanBefore[i];
            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                EditorSceneManager.SaveScene(scene);
        }

        Debug.Log(LogTag + " восстановлено компонентов: " + repaired +
                  " · сцен сохранено: " + cleanBefore.Count +
                  (unknown > 0 ? " · НЕ тронуто неизвестных объектов: " + unknown : "") +
                  "\n" + log);

        if (LastNeedsYamlFix.Count > 0)
        {
            // §50 (задача 3): часть битых компонентов штатным API не снимается — говорим прямо.
            Debug.LogWarning(LogTag + " остались битые компоненты (ссылка на встроенный MonoScript): " +
                             string.Join(", ", LastNeedsYamlFix.ToArray()) +
                             " — нужна правка сцены при закрытом Unity (§50, задача 3)");
        }

        if (dirtyBefore.Count > 0)
        {
            StringBuilder names = new StringBuilder();
            for (int i = 0; i < dirtyBefore.Count; i++)
            {
                if (i > 0) names.Append(", ");
                names.Append(dirtyBefore[i].name);
            }
            Debug.LogWarning(LogTag + " в сценах (" + names + ") БЫЛИ несохранённые правки — " +
                             "починка сделана в памяти редактора, но НЕ сохранена автоматически, " +
                             "чтобы не затереть их. Сохраните сцену вручную (Ctrl+S).");
        }

        if (unknown > 0)
        {
            Debug.LogWarning(LogTag + " объекты с НЕИЗВЕСТНЫМИ отсутствующими скриптами " +
                             "(не тронуты, нужен ручной разбор):\n" + log);
        }
    }

    private static void Scan(Transform t, Scene scene, StringBuilder log,
        ref int repaired, ref int unknown)
    {
        GameObject go = t.gameObject;
        // §50 (задача 3): ДВА критерия, а не один. Первый — штатный счётчик Unity
        // (`m_Script` вообще без ссылки). Второй — компонент, который Unity отдаёт как null
        // (встроенный MonoScript с устаревшим namespace: счётчик его НЕ видит, см.
        // `CountBrokenComponents`). Раньше проверялся только первый, и битые компоненты
        // столов стендов для починки были невидимы.
        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        int broken = CountBrokenComponents(go);
        if (missing > 0 || broken > 0)
        {
            System.Type known;
            if (KnownRepairs.TryGetValue(go.name, out known) && known != null)
            {
                // Компонент-«пустышку» убираем и вешаем настоящий — штатными средствами Unity,
                // с регистрацией в Undo (Ctrl+Z отменяет).
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                int left = CountBrokenComponents(go);

                // §50 (задача 3): ДОБАВЛЯЕМ настоящий компонент ТОЛЬКО ЕСЛИ битый снят.
                // Раньше добавление шло безусловно — на объекте с неубранным битым компонентом
                // это дало бы ДВА маркера (битый + рабочий) и ту же ошибку в консоли.
                if (left > 0)
                {
                    LastNeedsYamlFix.Add(go.name);
                    log.AppendLine("  • «" + go.name + "» (" + scene.name + "): битых компонентов " +
                                   left + " — штатным API не снимаются (ссылка на ВСТРОЕННЫЙ " +
                                   "MonoScript с устаревшим namespace). Компонент НЕ добавлен " +
                                   "(иначе на объекте оказалось бы два маркера). Нужна правка сцены " +
                                   "при закрытом Unity: m_Script → guid настоящего скрипта, " +
                                   "m_EditorClassIdentifier → актуальный класс (§50, задача 3)");
                    unknown++;
                    return;
                }

                Component added = Undo.AddComponent(go, known);
                if (added is RegisteredObject ro) ro.DisplayName = go.name;

                log.AppendLine("  • «" + go.name + "» (" + scene.name + "): " +
                               "снято пустышек " + removed + ", добавлен " + known.FullName +
                               " (DisplayName = \"" + go.name + "\")");
                repaired++;
            }
            else
            {
                log.AppendLine("  • «" + go.name + "» (" + scene.name + "): отсутствующих " +
                               missing + ", битых (null) " + broken +
                               " — соответствие неизвестно, оставлено как есть");
                unknown++;
            }
        }

        for (int i = 0; i < t.childCount; i++)
            Scan(t.GetChild(i), scene, log, ref repaired, ref unknown);
    }

    // ------------------------------------------------------------------ отчёт

    /// <summary>
    /// Показать ВСЕ отсутствующие скрипты в загруженных сценах, ничего не меняя.
    /// §50 (задача 3): счёт идёт по ДВУМ критериям (штатный счётчик + null-компоненты),
    /// результат публикуется в <see cref="LastBrokenCount"/>/<see cref="LastBrokenReport"/>
    /// для тестов и для отчёта перф-набора.
    /// </summary>
    [MenuItem(MenuRoot + "Показать отсутствующие скрипты (только отчёт)")]
    public static void Report()
    {
        int total = 0;
        StringBuilder sb = new StringBuilder();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded) continue;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
                ReportWalk(roots[r].transform, scene, sb, ref total);
        }
        LastBrokenCount = total;
        LastBrokenReport = sb.ToString();
        Debug.Log(total == 0
            ? LogTag + " отсутствующих скриптов не найдено (проверены оба критерия)."
            : LogTag + " всего объектов с отсутствующими скриптами: " + total + "\n" + sb);
    }

    private static void ReportWalk(Transform t, Scene scene, StringBuilder sb, ref int total)
    {
        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        int broken = CountBrokenComponents(t.gameObject);
        if (missing > 0 || broken > 0)
        {
            sb.AppendLine("  • «" + t.gameObject.name + "» (" + scene.name + "): отсутствующих " +
                          missing + ", битых (null) " + broken);
            total++;
        }
        for (int i = 0; i < t.childCount; i++)
            ReportWalk(t.GetChild(i), scene, sb, ref total);
    }
}
#endif
