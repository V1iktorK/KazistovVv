// ================================================================================================
//  KvSceneRepairTools.cs — ИНСТРУМЕНТ РЕМОНТА СЦЕН: ПОИСК И СНЯТИЕ «ОТСУТСТВУЮЩИХ СКРИПТОВ».
//
//  ОТКУДА ЗАДАЧА. Прогон на шлеме PICO 4 Ultra (12.11.2026) записал в журнал:
//      The referenced script on this Behaviour (Game Object 'Стенд_2_Стол') is missing!
//      The referenced script on this Behaviour (Game Object 'Стенд_1_Стол') is missing!
//  То есть на двух объектах сцены висят компоненты, чей скрипт из проекта исчез: Unity не может их
//  создать и молча пропускает. Чем это плохо: объект выглядит «пустым» для кода, который ищет этот
//  компонент, и настоящее поведение зависит от того, что успело выполниться до ошибки. Такое не
//  ловится тестами: сцена считается корректной, пока кто-то не прочитает журнал.
//
//  ЧТО ДЕЛАЕТ ИНСТРУМЕНТ. Находит такие компоненты в указанных сценах и (а) печатает список,
//  (б) по флагу `apply` — снимает их и сохраняет сцену. По умолчанию НИЧЕГО НЕ МЕНЯЕТ: сначала
//  смотрим, что именно висит, потом решаем.
//
//  ПОЧЕМУ ОТДЕЛЬНЫЙ ИНСТРУМЕНТ, А НЕ РУЧНАЯ ПРАВКА. `.unity` — текстовый файл с идентификаторами;
//  править его руками значит однажды получить сцену, которая «открылась», но потеряла связи. Через
//  Unity API это безопасно: правка идёт тем же путём, что и в редакторе.
//
//  ЗАПУСК (batchmode):
//      Unity.exe -batchmode -quit -projectPath <проект> \
//        -executeMethod KazistovVvEditor.KvSceneRepairTools.Report -logFile <лог>
//      Unity.exe -batchmode -quit -projectPath <проект> \
//        -executeMethod KazistovVvEditor.KvSceneRepairTools.Remove -logFile <лог>
// ================================================================================================

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KazistovVvEditor
{
    /// <summary>Поиск и снятие компонентов с отсутствующими скриптами в сценах проекта.</summary>
    public static class KvSceneRepairTools
    {
        /// <summary>Сцены, которые проверяются. Только те, что реально уезжают в сборку.</summary>
        private static readonly string[] Scenes =
        {
            "Assets/_Project/00_Scenes/XrMain.unity",
            "Assets/_Project/00_Scenes/ArMain.unity",
            "Assets/_Project/00_Scenes/BootAr.unity"
        };

        /// <summary>
        /// ЧТО НАЙДЕНО — БЕЗ ИЗМЕНЕНИЙ. Печатает по каждому объекту число отсутствующих скриптов и
        /// полный путь объекта в сцене. Возврат: 0 — отсутствующих нет; 1 — есть (так удобнее для CI).
        /// </summary>
        public static void Report()
        {
            int total = Scan(false);
            Debug.Log(total == 0
                ? "[KvSceneRepair] ОТСУТСТВУЮЩИХ СКРИПТОВ НЕТ: сцены чистые"
                : "[KvSceneRepair] НАЙДЕНО отсутствующих скриптов: " + total + " (снятие: -executeMethod …Remove)");
            if (Application.isBatchMode) EditorApplication.Exit(total == 0 ? 0 : 1);
        }

        /// <summary>
        /// СНЯТЬ ОТСУТСТВУЮЩИЕ СКРИПТЫ И СОХРАНИТЬ СЦЕНЫ. Сначала печатает, что снимает, — чтобы в
        /// журнале осталось доказательство, а не только «готово».
        /// </summary>
        public static void Remove()
        {
            int total = Scan(true);
            Debug.Log("[KvSceneRepair] снято отсутствующих скриптов: " + total);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// ОБХОД СЦЕН. `apply == false` — только посчитать и напечатать; `true` — снять и сохранить.
        /// </summary>
        private static int Scan(bool apply)
        {
            int total = 0;
            foreach (string path in Scenes)
            {
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[KvSceneRepair] сцены нет: " + path);
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var found = new List<string>();
                GameObject[] roots = scene.GetRootGameObjects();

                // СЧИТАЕМ ПО ОБЪЕКТАМ, А НЕ ПО КОМПОНЕНТАМ. У объекта с отсутствующим скриптом
                // Unity не создаёт компонент вовсе — в списке компонентов его нет, а предупреждение
                // в журнале есть. Поэтому спрашиваем у Unity счётчик отсутствующих скриптов.
                foreach (GameObject root in roots) CollectMissing(root.transform, found);

                if (found.Count == 0)
                {
                    Debug.Log("[KvSceneRepair] " + path + ": отсутствующих скриптов нет");
                    continue;
                }

                total += found.Count;
                Debug.Log("[KvSceneRepair] " + path + ": объектов с отсутствующими скриптами — " + found.Count);
                foreach (string line in found) Debug.Log("    · " + line);

                if (!apply) continue;

                bool dirty = false;
                foreach (GameObject go in EnumerateAll(roots))
                {
                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    if (removed <= 0) continue;
                    Debug.Log("[KvSceneRepair] снято " + removed + " отсутствующих скриптов у «" + go.name + "»");
                    dirty = true;
                }

                if (dirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[KvSceneRepair] сцена сохранена: " + path);
                }
            }
            return total;
        }

        /// <summary>Собрать пути объектов, на которых есть отсутствующие компоненты.</summary>
        private static void CollectMissing(Transform t, List<string> into)
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (missing > 0) into.Add(PathOf(t) + " · отсутствующих скриптов " + missing);
            for (int i = 0; i < t.childCount; i++) CollectMissing(t.GetChild(i), into);
        }

        /// <summary>Все объекты сцены, включая выключенные.</summary>
        private static IEnumerable<GameObject> EnumerateAll(GameObject[] roots)
        {
            var stack = new Stack<Transform>();
            foreach (GameObject root in roots) stack.Push(root.transform);
            while (stack.Count > 0)
            {
                Transform t = stack.Pop();
                yield return t.gameObject;
                for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
            }
        }

        /// <summary>Полный путь объекта в сцене — по нему в журнале видно, ГДЕ проблема.</summary>
        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            Transform p = t.parent;
            while (p != null)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }
    }
}
