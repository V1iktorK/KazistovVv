// ================================================================================================
//  KvXrOwnerFixTests.cs — тесты правок по итогам живого теста владельца (04.11.2026).
//
//  ОТКУДА. Владелец надел шлем и увидел: «камера летает», «зоны включены», «роботы пропали в VR
//  версии». Разбор журнала устройства показал три разные причины — и все три были не «настройкой»,
//  а кодом:
//    1) роботов и стенды гасил `KvXrSceneEmpty` («пусто при запуске» из §42) — в VR оставался пустой
//       ангар, хотя весь смысл приложения — работа с роботом;
//    2) «зоны включены» — визуализации поднимаются службами этапов и часть восстанавливается из
//       сохранённых флагов; флаги по умолчанию их уже не спасали, потому что применялись до создания
//       служб;
//    3) «камера летает» — `KvXrHangarAnchor` ставил зал по объекту-камере, пока трекинг головы не
//       готов, и МЕСТИЛ его каждый кадр.
//
//  СТИЛЬ — КАК В ПРОЕКТЕ: сцена в EditMode не запускается, поэтому проверяется ИСХОДНИК и порядок
//  проверок. Каждый тест ловит конкретную регрессию, а не «файл существует».
// ================================================================================================

using System;
using System.IO;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Правки по тесту владельца 04.11.2026: роботы видны, плюшки выключены, камера стоит.</summary>
    public class KvXrOwnerFixTests
    {
        private static string ScriptsDir
        {
            get
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                return Path.Combine(root, "Assets", "_Project", "01_Scripts", "Xr");
            }
        }

        private static string ReadScript(string fileName)
        {
            string path = Path.Combine(ScriptsDir, fileName);
            Assert.IsTrue(File.Exists(path), "не найден исходник: " + path);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// В VR-СЦЕНЕ РОБОТЫ И СТЕНДЫ ОСТАЮТСЯ ВИДИМЫМИ. Дефект: `KvXrRig` создавал
        /// `KvXrSceneEmpty`, который каждый кадр гасил столы и роботов — «как в ПК до добавления».
        /// Владелец 04.11.2026: «роботы пропали в VR версии»; по ТЗ нужно «только робот, пульт и ангар».
        /// </summary>
        [Test]
        public void Scene_NoLongerHidesRobotsInVr()
        {
            string rig = ReadScript("KvXrRig.cs");

            StringAssert.DoesNotContain("EnsureSceneEmpty()", rig,
                "риг снова гасит столы и роботов сцены: в VR будет пусто, владелец снова увидит пустой ангар");
            StringAssert.Contains("СЦЕНА: роботы и стенды видны", rig,
                "в журнале нет строки, что роботы видны при запуске — проверить «пусто» будет нечем");
        }

        /// <summary>
        /// ПЛЮШКИ ВЫКЛЮЧАЮТСЯ ОДНИМ МЕСТОМ И ВСЕ ШЕСТЬ. Дефект: визуализаций шесть, каждая
        /// включается своим путём; флаги по умолчанию применялись раньше, чем службы создавались.
        /// </summary>
        [Test]
        public void CleanView_DisablesAllSixVisualizers()
        {
            string clean = ReadScript("KvXrCleanView.cs");

            StringAssert.Contains("SetWorkspaceVisible(false)", clean, "зона достижимости не выключается");
            StringAssert.Contains("SetJointLimitsVisible(false)", clean, "лимиты суставов не выключаются");
            StringAssert.Contains("SetMetricsPanelVisible(false)", clean, "панель чисел не выключается");
            StringAssert.Contains("Heatmap.SetVisible(false)", clean, "тепловая карта (шар и жёлтые точки) не выключается");
            StringAssert.Contains("Clearance.SetVisible(false)", clean, "теплокарта зазоров не выключается");
            StringAssert.Contains("Zones.SetAllVisible(false)", clean, "зоны запрета не выключаются");
            StringAssert.Contains("Singularities.SetEnabled(false)", clean, "сингулярности не выключаются");
        }

        /// <summary>
        /// ЧИСТЫЙ ВИД ПРИМЕНЯЕТСЯ ПОСЛЕ ПОДЪЁМА СЛУЖБ, А НЕ В Awake ПО ПУСТЫМ ССЫЛКАМ.
        /// Дефект: ранний вызов по пустым ссылкам молча ничего не выключал — «плюшки» оставались.
        /// </summary>
        [Test]
        public void FlowBridge_AppliesCleanViewWhenServicesAreReady()
        {
            string bridge = ReadScript("KvXrFlowBridge.cs");

            StringAssert.Contains("CleanViewWhenReady", bridge, "нет корутины ожидания служб этапов");
            StringAssert.Contains("KvXrCleanView.Apply", bridge, "чистый вид не применяется вовсе");
            StringAssert.Contains("StartCoroutine", bridge, "корутина не запускается");
        }

        /// <summary>
        /// ЗАЛ НЕ ДВИГАЕТСЯ БЕЗ ДОСТОВЕРНОЙ ПОЗЫ ШЛЕМА. Дефект: при неготовом трекинге точка
        /// оператора бралась с объекта-камеры, и `Place()` метал зал каждый кадр — «камера летает».
        /// Проверяется ПОРЯДОК: проверка `fromHmd` обязана стоять ДО `SetPositionAndRotation`.
        /// </summary>
        [Test]
        public void HangarAnchor_DoesNotMoveWithoutHmdPose()
        {
            string anchor = ReadScript("KvXrHangarAnchor.cs");

            int guard = anchor.IndexOf("if (!fromHmd)", StringComparison.Ordinal);
            int move = anchor.IndexOf("root.SetPositionAndRotation", StringComparison.Ordinal);

            Assert.GreaterOrEqual(guard, 0, "в Place() нет проверки достоверности позы шлема (fromHmd)");
            Assert.GreaterOrEqual(move, 0, "в Place() не найдено перемещение корня зала");
            Assert.Less(guard, move,
                "проверка fromHmd стоит ПОСЛЕ перемещения зала: зал снова будет ездить по недостоверной позе");
            StringAssert.Contains("НЕ двигаю", anchor,
                "нет честной строки журнала «трекинг не готов — зал не двигаю»");
        }

        /// <summary>
        /// ОБЛЁТ ПРОСТОЯ: ТОЛЬКО ПОСЛЕ ДОЛГОГО ПОКОЯ И С МГНОВЕННЫМ ВОЗВРАТОМ УПРАВЛЕНИЯ.
        /// Просьба владельца 04.11.2026: «камеру летающую оставь, но после продолжительного простоя;
        /// после шевеления контроллерами — нормальная камера».
        /// </summary>
        [Test]
        public void IdleOrbit_LongIdleAndStopsOnControllerMovement()
        {
            string orbit = ReadScript("KvXrIdleOrbit.cs");

            StringAssert.Contains("idleSeconds = 180f", orbit,
                "порог простоя должен быть ПРОДОЛЖИТЕЛЬНЫМ (3 минуты), а не мгновенным");
            StringAssert.Contains("TryPose", orbit,
                "активность контроллеров не читается — облёт не остановится, когда владелец шевелит руками");
            StringAssert.Contains("TryHeadPose", orbit,
                "движение головы не считается активностью — облёт продолжится, когда оператор смотрит по сторонам");
            StringAssert.Contains("ОБЛЁТ ПРОСТОЯ: остановлен", orbit,
                "нет строки журнала об остановке облёта — проверить поведение будет нечем");
            StringAssert.Contains("MrActive", orbit,
                "в MR облёт обязан быть выключен: комната реальная, оператора двигать нельзя");
            StringAssert.Contains("GetPassthrough", orbit,
                "проверка MR идёт по запрошенному режиму комнаты — без неё облёт включится в MR-сессии");
        }

        /// <summary>
        /// В MR ЛУЧ И МЕТКА ПРИЦЕЛА — ПЛЮШКА, ПО УМОЛЧАНИЮ СКРЫТЫ. Владелец 04.11.2026:
        /// «убери все плюшки из режима по умолчанию»; постановка работает без подсказки.
        /// </summary>
        [Test]
        public void MrPlacement_HidesAimBeamByDefault()
        {
            string placement = ReadScript("KvMrPlacement.cs");

            StringAssert.Contains("showAimByDefault = false", placement,
                "луч и метка прицела снова видны по умолчанию — это ровно та «плюшка», которую просили убрать");
            StringAssert.Contains("beam.enabled = false", placement,
                "при выключенном флаге луч должен гаситься");
            StringAssert.Contains("reticle.SetActive(false)", placement,
                "при выключенном флаге метка должна гаситься");
        }

        /// <summary>Класс чистого вида не должен создавать объекты в сцене: только гасить существующие.</summary>
        [Test]
        public void CleanView_DoesNotCreateObjects()
        {
            string clean = ReadScript("KvXrCleanView.cs");
            StringAssert.DoesNotContain("new GameObject", clean,
                "чистый вид обязан только гасить визуализации, а не создавать сцену");
            StringAssert.DoesNotContain("CreatePrimitive", clean,
                "чистый вид не создаёт примитивы — это работа визуализаторов");
        }
    }
}
