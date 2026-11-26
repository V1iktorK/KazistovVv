// ====================================================================================
//  KvBuildWindows.cs — СБОРКА ПК-ВЕРСИИ (StandaloneWindows64).
//
//  ЗАЧЕМ (26.11.2026). Владелец утвердил новый интерфейс (Blender-раскладка + стекло)
//  и попросил «полностью переносить функционал». Смотреть удобнее всего в ПК-версии:
//  мышью, с выездом доков у краёв и кнопками окон. Метода сборки ПК не было — только
//  BuildAndroidApk (KvXrTools). Собираем exe по тем же сценам Build Settings, что и APK.
//
//  ЗАПУСК: Unity -batchmode -quit -projectPath <проект> -executeMethod KazistovVvTools.KvBuildWindows.Build
// ====================================================================================
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KazistovVvTools
{
    public static class KvBuildWindows
    {
        public static void Build()
        {
            string outDir = @"C:\KazistovVv_Build\Builds";
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, "KazistovVv-PC.exe");

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[KvXr] ПК-сборка: в Build Settings нет включённых сцен");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[KvXr] ПК-сборка: сцен " + scenes.Length + " → " + string.Join(" → ", scenes));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log(string.Format("[KvXr] ПК-версия собрана: {0} · {1:0.0} МБ · ошибок {2}",
                    exePath, summary.totalSize / (1024f * 1024f), summary.totalErrors));
            }
            else
            {
                Debug.LogError("[KvXr] ПК-сборка НЕ прошла: " + summary.result);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }
    }
}
