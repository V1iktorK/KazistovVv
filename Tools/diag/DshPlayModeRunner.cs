using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// ДИАГНОСТИКА АГЕНТА (§21). НЕ переносится в копию пользователя.
///
/// Запускает PLAY-MODE тесты проекта в ЖИВОМ (не batch) редакторе и складывает результат в файл.
///
/// ЗАЧЕМ ТАК. Batch-PlayMode на этой машине не завершается (§0.7.7: процесс уходит в бесконечный
/// цикл плеера), а нажать `Test Runner → Run All` руками агент не может. Поэтому используется
/// штатный программный вход Unity Test Framework — `TestRunnerApi.Execute`, — а редактор
/// открывается ОБЫЧНЫМ (не `-batchmode`), чтобы работал цикл плеера с графикой.
///
/// ФАЙЛ СОСТОЯНИЯ `<проект>/Logs/_dsh_playmode.state` (одно слово):
///   request  — просьба запустить прогон (создаёт тот, кто запускает редактор);
///   waiting  — редактор увидел просьбу и ждёт, пока он доимпортируется;
///   started  — тесты запущены (повторно НЕ запускаем);
///   файла нет — прогон не запрашивался.
///
/// ПОЧЕМУ ФАЙЛ, А НЕ SessionState. Вход в PlayMode перезагружает домен, и ЛЮБОЕ статическое
/// поле (в т.ч. подписка на `EditorApplication.update`) при этом теряется. Файл состояния
/// переживает перезагрузку домена, поэтому ожидание и запуск восстанавливаются сами —
/// без этого прогон молча «зависал» на первой же компиляции скриптов.
/// </summary>
public static class DshPlayModeRunner
{
    private const string StateName = "_dsh_playmode.state";
    private const string ResultName = "playmode_results.txt";

    /// <summary>Сколько секунд покоя без компиляции ждём перед запуском тестов.</summary>
    private const double SettleSeconds = 20.0;

    /// <summary>Сколько всего ждём импорта, после чего запускаем тесты всё равно.</summary>
    private const double MaxWaitSeconds = 15 * 60.0;

    private static bool ticking;
    private static double waitStarted;

    // ВАЖНО: и API, и объект колбэков держим в СТАТИЧЕСКИХ полях. TestRunnerApi — это
    // ScriptableObject; без живой ссылки его может собрать GC, и прогон «потеряет» результат.
    private static TestRunnerApi api;
    private static Callbacks callbacks;

    private static string StatePath
    {
        get { return Path.Combine(ProjectRoot, "Logs", StateName); }
    }

    private static string ResultPath
    {
        get { return Path.Combine(ProjectRoot, "Logs", ResultName); }
    }

    private static string ProjectRoot
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
    }

    // ------------------------------------------------------------------ вход

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        try
        {
            string state = ReadState();
            if (state == "request")
            {
                Log("=== DshPlayModeRunner · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
                Log("проект: " + ProjectRoot);
                Log("графика: " + SystemInfo.graphicsDeviceType + " · " + SystemInfo.graphicsDeviceName);
                Log("платформа: " + Application.platform + " (batch=" + Application.isBatchMode + ")");
                WriteState("waiting");
                state = "waiting";
            }

            if (state != "waiting") return;

            ticking = true;
            waitStarted = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Log("ожидание готовности редактора (компиляция/импорт)…");
        }
        catch (Exception e)
        {
            Log("ИСКЛЮЧЕНИЕ в Boot: " + e);
            WriteState("");
            EditorApplication.Exit(3);
        }
    }

    /// <summary>Ждём, пока редактор закончит компиляцию, и только потом запускаем тесты.</summary>
    private static void Tick()
    {
        if (!ticking) return;
        if (Application.isPlaying) return;                 // уже играем — ждать нечего
        if (EditorApplication.isCompiling)
        {
            waitStarted = EditorApplication.timeSinceStartup;
            return;
        }

        double waited = EditorApplication.timeSinceStartup - waitStarted;
        if (waited < SettleSeconds && waited < MaxWaitSeconds) return;

        ticking = false;
        EditorApplication.update -= Tick;
        if (waited >= MaxWaitSeconds)
            Log("внимание: импорт так и не успокоился за " + MaxWaitSeconds.ToString("0") +
                " с — запускаю тесты как есть");
        Start();
    }

    private static void Start()
    {
        try
        {
            WriteState("started");
            Log("запуск PlayMode-тестов через TestRunnerApi… (isPlaying=" + Application.isPlaying +
                ", isCompiling=" + EditorApplication.isCompiling + ")");
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new Callbacks();
            api.RegisterCallbacks(callbacks);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode }));
        }
        catch (Exception e)
        {
            Log("ИСКЛЮЧЕНИЕ при запуске тестов: " + e);
            WriteState("");
            EditorApplication.Exit(2);
        }
    }

    // ------------------------------------------------------------------ состояние

    private static string ReadState()
    {
        try
        {
            return File.Exists(StatePath) ? File.ReadAllText(StatePath).Trim() : "";
        }
        catch (Exception) { return ""; }
    }

    private static void WriteState(string value)
    {
        try
        {
            if (string.IsNullOrEmpty(value)) { if (File.Exists(StatePath)) File.Delete(StatePath); }
            else File.WriteAllText(StatePath, value, new UTF8Encoding(false));
        }
        catch (Exception e) { Debug.LogWarning("[DshPlayModeRunner] состояние не записано: " + e.Message); }
    }

    // ------------------------------------------------------------------ колбэки

    private class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
            Log("RunStarted: " + (testsToRun != null ? testsToRun.Name : "?") +
                " (дочерних узлов: " + CountChildren(testsToRun) + ")");
        }

        private static int CountChildren(ITestAdaptor node)
        {
            if (node == null || node.Children == null) return 0;
            int n = 0;
            foreach (ITestAdaptor child in node.Children) n++;
            return n;
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result == null || result.Test == null || result.Test.IsSuite) return;
            Log("  [" + result.ResultState + "] " + result.FullName + "  (" +
                result.Duration.ToString("0.00") + " с)");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            int exit = 0;
            try
            {
                Log("");
                Log("=== ИТОГ RUN ===");
                Log("result=" + (result != null ? result.ResultState : "null") +
                    " total=" + (result != null ? result.PassCount + result.FailCount +
                                                 result.SkipCount + result.InconclusiveCount : 0) +
                    " passed=" + (result != null ? result.PassCount : 0) +
                    " failed=" + (result != null ? result.FailCount : 0) +
                    " skipped=" + (result != null ? result.SkipCount : 0) +
                    " inconclusive=" + (result != null ? result.InconclusiveCount : 0) +
                    " duration=" + (result != null ? result.Duration.ToString("0.0") : "?") + " с");

                if (result != null && result.FailCount > 0)
                {
                    exit = 1;
                    Log("");
                    Log("=== ПАДЕНИЯ (полный текст) ===");
                    DumpFailures(result);
                }
                Log("");
                Log("=== ДЕРЕВО ===");
                if (result != null) DumpTree(result, 0);
            }
            catch (Exception e)
            {
                Log("ИСКЛЮЧЕНИЕ при разборе результата: " + e);
                exit = 4;
            }

            WriteState("");
            Log("=== КОНЕЦ, код выхода " + exit + " ===");
            EditorApplication.Exit(exit);
        }

        private static void DumpFailures(ITestResultAdaptor node)
        {
            if (node == null) return;
            if (node.Test != null && !node.Test.IsSuite &&
                (node.FailCount > 0 || node.ResultState == null || node.ResultState.Contains("Failed")))
            {
                Log("--- " + node.FullName);
                Log("    состояние: " + node.ResultState);
                Log("    сообщение: " + node.Message);
                Log("    стек: " + node.StackTrace);
            }
            if (node.Children == null) return;
            foreach (ITestResultAdaptor child in node.Children) DumpFailures(child);
        }

        private static void DumpTree(ITestResultAdaptor node, int depth)
        {
            if (node == null) return;
            string pad = new string(' ', depth * 2);
            Log(pad + (node.Test != null ? node.Test.Name : "?") + " → " + node.ResultState +
                " (" + node.Duration.ToString("0.00") + " с)");
            if (node.Children == null) return;
            foreach (ITestResultAdaptor child in node.Children) DumpTree(child, depth + 1);
        }
    }

    // ------------------------------------------------------------------ журнал

    private static void Log(string text)
    {
        try
        {
            File.AppendAllText(ResultPath, text + Environment.NewLine, new UTF8Encoding(false));
        }
        catch (Exception) { }
        Debug.Log("[DshPlayModeRunner] " + text);
    }
}
