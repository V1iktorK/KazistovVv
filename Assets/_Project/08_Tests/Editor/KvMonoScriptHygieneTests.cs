// ================================================================================================
//  KvMonoScriptHygieneTests.cs — СЕРИАЛИЗУЕМЫЕ КОМПОНЕНТЫ ОБЯЗАНЫ ИМЕТЬ СВОЙ MonoScript-ФАЙЛ.
//
//  ОТКУДА ЗАДАЧА (замер 12.11.2026). На устройствах (PICO, Redmi) журнал гудел:
//      The referenced script on this Behaviour (Game Object 'Стенд_1_Стол') is missing!
//  При этом редактор показывал сцены ЦЕЛЫМИ (KvSceneRepairTools.Report: «отсутствующих нет»), и
//  все тесты были зелёные. Причина: `RegisteredObject` объявлен ВНУТРИ другого файла
//  (`RuntimeRegistry.cs`). У такого класса Unity не создаёт MonoScript-ассет, и в сцену он
//  пишется битой ссылкой `m_Script: {fileID: <хеш>}` БЕЗ guid + `m_EditorClassIdentifier`.
//  Редактор разрешает класс по идентификатору (поэтому «missing» не видно в редакторе), а
//  IL2CPP-сборка на Android — НЕ разрешает: компонент мёртв, и вся его логика не работает.
//
//  ЧТО ПРОВЕРЯЕТСЯ. Для каждого MonoBehaviour КАЖДОЙ основной сцены: свойство `m_Script`
//  сериализованной ссылки ведёт на MonoScript, чей класс — ровно класс компонента.
//  Битая ссылка (null или чужой класс) — провал с именем объекта и класса.
//
//  ПОЧЕМУ ПРОВЕРЯЕТСЯ ТАК. Другие проверки не видят дефект ПО ПОСТРОЕНИЮ:
//    • `GetMonoBehavioursWithMissingScriptCount` в редакторе даёт 0 — класс разрешён фолбэком;
//    • разбор guid из текста сцены не ловит ссылку, в которой guid НЕТ вовсе;
//    • предупреждение появляется только на УСТРОЙСТВЕ, где дёргать редактор нельзя.
//  Проверка читает саму ССЫЛКУ сериализации — то, что реально уезжает в APK. Файлы текстом
//  не читаются (храповик P4-3), сцены открываются штатным EditorSceneManager.
//
//  ГРАНИЦА ЧЕСТНОСТИ: проверяются основные сцены проекта (те, что уезжают в сборку), а не демо-
//  сцены сторонних ассетов. Классы, которые добавляются в рантайме AddComponent-ом и в сцены не
//  сериализуются, здесь не проверяются: у них битой ссылки быть не может по построению.
// ================================================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Гигиена сериализации: `m_Script` каждой компоненты сцен ведёт на свой класс.</summary>
        [Category(KvTestCategories.Slow)]
    public class KvMonoScriptHygieneTests
    {
        /// <summary>
        /// ЧИСТАЯ СЦЕНА ПОСЛЕ ПРОГОНА. Фикстура открывает основные сцены (Single) и обязана
        /// вернуть редактор в пустую сцену: иначе следующие фикстуры (например P1-21,
        /// «стол найден в пустой сцене») находят объекты ЧУЖОЙ сцены и падают не по своей вине
        /// (найдено 12.11.2026 — кросс-загрязнение тестов).
        /// </summary>
        [TearDown]
        public void LeaveEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        /// <summary>Сцены, которые уезжают в сборку и в которых живут сериализованные компоненты.</summary>
        private static readonly string[] Scenes =
        {
            "Assets/_Project/00_Scenes/XrMain.unity",
            "Assets/_Project/00_Scenes/ArMain.unity",
            "Assets/_Project/00_Scenes/BootAr.unity",
            "Assets/_Project/00_Scenes/MainScene.unity"
        };

        [Test]
        public void SerializedComponents_ResolveToTheirOwnMonoScript()
        {
            var problems = new List<string>();

            foreach (string path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Walk(root.transform, path, problems);
                }
            }

            Assert.IsTrue(problems.Count == 0,
                "СЕРИАЛИЗАЦИЯ СЛОМАНА (на устройствах эти компоненты будут «missing script»):\n" +
                string.Join("\n", problems));
        }

        private static void Walk(Transform t, string scenePath, List<string> problems)
        {
            Check(t.gameObject, scenePath, problems);
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), scenePath, problems);
        }

        private static void Check(GameObject go, string scenePath, List<string> problems)
        {
            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null)
                {
                    problems.Add(scenePath + " · «" + PathOf(go.transform) + "» · компонент " + i +
                                 ": ссылка m_Script пуста (отсутствующий скрипт)");
                    continue;
                }
                if (!(c is MonoBehaviour mb)) continue;

                var so = new SerializedObject(mb);
                SerializedProperty scriptProp = so.FindProperty("m_Script");
                MonoScript ms = scriptProp != null ? scriptProp.objectReferenceValue as MonoScript : null;

                if (ms == null)
                {
                    problems.Add(scenePath + " · «" + PathOf(go.transform) + "» · класс " +
                                 mb.GetType().FullName +
                                 ": m_Script не ведёт на MonoScript (нет файла с этим классом)");
                }
                else if (ms.GetClass() != mb.GetType())
                {
                    problems.Add(scenePath + " · «" + PathOf(go.transform) + "» · класс " +
                                 mb.GetType().FullName + ": m_Script ведёт на чужой MonoScript «" +
                                 ms.name + "»");
                }
            }
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            Transform p = t.parent;
            while (p != null)
            {
                path = p.name + "/" + path;
                p = p.parent;
            }
            return path;
        }
    }
}
