using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты ОСВЕЩЕНИЯ И ФОНА XR-СЦЕНЫ (сессия §33, дефект с живого шлема 15.10.2026).
    ///
    /// ЧТО БЫЛО. Владелец надел шлем и увидел: «всё чёрное, только оверлей программы с квадратиками».
    /// Интерфейс виден, потому что он unlit; вся остальная геометрия — нет. Причина оказалась
    /// двойной и обе части лежали в `XrMain.unity`:
    ///   1) **в сцене ноль источников света** — роботы и столы стоят, но освещать их нечем
    ///      (в десктопной `MainScene` источников три);
    ///   2) **камера чистит фон в чёрный цвет** (`m_ClearFlags: 2`, цвет `{0,0,0,1}`) — за объектами
    ///      не видно ни неба, ни окружения.
    ///
    /// ПОЧЕМУ ЭТО НЕ ЛОВИЛОСЬ. Сцена собиралась кодом, свет в неё никто не добавлял, а прежние
    /// тесты проверяли СОСТАВ компонентов и МАТЕРИАЛЫ — но не освещённость. «Всё чёрное» —
    /// это не про материал и не про конвейер, а про отсутствие света: класс дефекта, которого
    /// в тестах не было вовсе.
    ///
    /// Тесты написаны ДО правки сцены и на текущем состоянии обязаны падать (правило [K5]).
    /// </summary>
        [Category(KvTestCategories.Fast)]
    public class KvXrLightingTests
    {
        private const string XrScenePath = "Assets/_Project/00_Scenes/XrMain.unity";

        /// <summary>
        /// В XR-СЦЕНЕ ОБЯЗАН БЫТЬ СВЕТ. Без источника света геометрия под URP рисуется чёрной,
        /// как бы верны ни были материалы — именно это и увидел владелец в шлеме.
        /// </summary>
        [Test]
        public void XrScene_HasAtLeastOneEnabledLight()
        {
            LightStats stats = StatsInScene();

            Assert.Greater(stats.Total, 0,
                "в XR-сцене нет ни одного источника света: роботы и столы будут чёрными " +
                "(интерфейс виден только потому, что он unlit)");

            Assert.Greater(stats.Enabled, 0, "все источники света в XR-сцене выключены");
            Assert.Greater(stats.Directional, 0,
                "в XR-сцене нет направленного света: она останется без общего освещения и объёма");
        }

        /// <summary>
        /// СВЕТ ДОЛЖЕН БЫТЬ ДОСТАТОЧНЫМ, А НЕ «ДЛЯ ГАЛОЧКИ». Слишком слабый источник формально
        /// есть, но сцена всё равно тёмная — поэтому проверяется суммарная интенсивность.
        /// </summary>
        [Test]
        public void XrScene_LightingIsBrightEnough()
        {
            LightStats stats = StatsInScene();

            Assert.Greater(stats.TotalIntensity, 0.8f,
                "суммарная интенсивность света в XR-сцене " + stats.TotalIntensity.ToString("0.00") +
                " — этого мало, сцена останется тёмной");
        }

        /// <summary>
        /// ФОН КАМЕРЫ НЕ ДОЛЖЕН БЫТЬ ЧЁРНОЙ ПУСТОТОЙ. Чёрный фон — это «ничего вокруг»:
        /// в VR он читается как выключенный экран, а не как лаборатория.
        /// </summary>
        [Test]
        public void XrScene_CameraBackgroundIsNotPitchBlack()
        {
            Scene scene = OpenXrScene();
            try
            {
                Camera camera = null;
                foreach (Camera candidate in UnityEngine.Object.FindObjectsByType<Camera>(
                             FindObjectsInactive.Include))
                {
                    if (candidate == null || candidate.gameObject.scene != scene) continue;
                    camera = candidate;
                    break;
                }

                Assert.IsNotNull(camera, "в XR-сцене нет камеры");

                bool skybox = camera.clearFlags == CameraClearFlags.Skybox;
                bool blackSolid = camera.clearFlags == CameraClearFlags.SolidColor &&
                                  camera.backgroundColor.r < 0.02f &&
                                  camera.backgroundColor.g < 0.02f &&
                                  camera.backgroundColor.b < 0.02f;

                Assert.IsFalse(blackSolid,
                    "камера XR-сцены чистит фон в ЧЁРНЫЙ цвет: вокруг объектов пустота, " +
                    "в шлеме это выглядит как неработающее приложение");
                Assert.IsTrue(skybox || camera.backgroundColor.maxColorComponent >= 0.02f,
                    "фон камеры не задан: ожидается небо либо осмысленный цвет окружения");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ---------------------------------------------------------------- вспомогательное

        private static Scene OpenXrScene()
        {
            Assert.IsTrue(File.Exists(XrScenePath), "нет XR-сцены " + XrScenePath);
            return EditorSceneManager.OpenScene(XrScenePath, OpenSceneMode.Additive);
        }

        /// <summary>Числа по источникам света — считаются ПОКА СЦЕНА ОТКРЫТА.</summary>
        private struct LightStats
        {
            public int Total;
            public int Enabled;
            public int Directional;
            public float TotalIntensity;
        }

        /// <summary>
        /// СЧИТАТЬ СВЕТ ВНУТРИ ОТКРЫТОЙ СЦЕНЫ И ВЕРНУТЬ ЧИСЛА, А НЕ ССЫЛКИ.
        ///
        /// ПЕРВАЯ ВЕРСИЯ ЭТОГО ТЕСТА БЫЛА НАПИСАНА С ОШИБКОЙ и это зафиксировано в отчёте:
        /// она собирала `List&lt;Light&gt;` и обращалась к компонентам уже ПОСЛЕ `CloseScene`,
        /// когда объекты сцены уничтожены — падало `MissingReferenceException`, и падение
        /// выглядело как «в сцене нет света», хотя свет в ней есть. Ссылки на объекты сцены
        /// нельзя выносить за пределы её жизни; числа — можно.
        /// </summary>
        private static LightStats StatsInScene()
        {
            var stats = new LightStats();
            Scene scene = OpenXrScene();
            try
            {
                foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(
                             FindObjectsInactive.Include))
                {
                    if (light == null || light.gameObject.scene != scene) continue;
                    stats.Total++;
                    if (!light.enabled || !light.gameObject.activeInHierarchy) continue;
                    stats.Enabled++;
                    if (light.type == LightType.Directional) stats.Directional++;
                    stats.TotalIntensity += light.intensity;
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            return stats;
        }

        // ====================================== ПУСТОЙ СПИСОК ПРЕСЕТОВ СВЕТА (команда light.next)

        /// <summary>
        /// ПУСТОЙ СПИСОК ПРЕСЕТОВ — ЧЕСТНАЯ ПРИЧИНА В ЖУРНАЛЕ, А НЕ ДЕЛЕНИЕ НА НОЛЬ.
        ///
        /// ОТКУДА (дефект B, 13.11.2026). Обработчик `light.next` считал следующий индекс как
        /// `(Index + 1) % Presets.Count`: при пустом списке это `DivideByZeroException`, который
        /// `KvCommands.Invoke` НЕ перехватывает — команда падает исключением, оператор не видит
        /// ни результата, ни объяснения. Пустой список — это СОСТОЯНИЕ, и оно обязано быть
        /// названо (журнал/статус), а не исключением.
        /// </summary>
        [Test]
        public void LightingNext_WithEmptyPresetList_ReportsReasonInsteadOfDividingByZero()
        {
            var studio = new KazistovVvFeatures.KvLightingStudio();
            studio.Presets.Clear();

            string reported = null;
            studio.Message += delegate (string text) { reported = text; };

            Assert.DoesNotThrow(delegate { studio.Next(); },
                "light.next при пустом списке пресетов не должен бросать DivideByZeroException");
            Assert.DoesNotThrow(delegate { studio.SetPreset(0); },
                "SetPreset при пустом списке тоже обязан обойтись без исключения");

            Assert.IsNotNull(reported, "пустой список — честная причина, и она обязана попасть в журнал");
            StringAssert.Contains("пресет", reported.ToLowerInvariant(),
                "причина обязана называть пустой список пресетов: " + reported);
            Assert.AreEqual("пресетов нет", studio.Status(),
                "статус обязан честно говорить, что переключать нечего");
        }
    }
}

