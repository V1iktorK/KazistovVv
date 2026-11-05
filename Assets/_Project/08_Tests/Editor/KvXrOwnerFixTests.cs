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
        /// СТАРТ ПУСТОЙ: СТОЛ И РОБОТА СОБИРАЕТ ПОЛЬЗОВАТЕЛЬ. Решение владельца 04.11.2026 (вечер):
        /// «убери сразу добавленные столы — они должны собираться (стол + робот) самим пользователем,
        /// а не быть сначала готовыми». Поэтому риг снова создаёт `KvXrSceneEmpty`, а сборка идёт из
        /// стартового меню («Стол», «Авто стол», «Добавить робота», «Запустить стенд»).
        /// </summary>
        [Test]
        public void Scene_StartsEmpty_UserBuildsStandFromMenu()
        {
            string rig = ReadScript("KvXrRig.cs");

            StringAssert.Contains("EnsureSceneEmpty()", rig,
                "риг больше не гасит готовые столы сцены: пользователь снова получит готовый стенд вместо сборки");
            StringAssert.Contains("СЦЕНА: старт ПУСТОЙ", rig,
                "в журнале нет строки «старт ПУСТОЙ» — проверить пустую сцену будет нечем");
            StringAssert.Contains("из меню", rig,
                "в строке журнала нет подсказки, что стенд собирается из меню");
        }

        /// <summary>
        /// ОБЛЁТ ПРОСТОЯ УДАЛЁН ПОЛНОСТЬЮ. Владелец 04.11.2026: «убери вообще эту функцию» —
        /// камера не должна ездить сама ни при каких условиях.
        /// </summary>
        [Test]
        public void IdleOrbit_RemovedCompletely()
        {
            string rig = ReadScript("KvXrRig.cs");
            string panel = ReadScript("KvXrGroupPanel.cs");

            StringAssert.DoesNotContain("EnsureIdleOrbit", rig, "риг снова создаёт облёт простоя");
            StringAssert.DoesNotContain("KvXrIdleOrbit", panel, "в настройках снова есть переключатель облёта");
            string orbitFile = Path.Combine(ScriptsDir, "KvXrIdleOrbit.cs");
            Assert.IsFalse(File.Exists(orbitFile), "файл облёта простоя вернулся: " + orbitFile);
        }

        /// <summary>
        /// ПАНЕЛЬ (ПУЛЬТ) В ЗАПАСНОМ ПОЛОЖЕНИИ — НА УРОВНЕ ЭКРАНА НАСТРОЕК, А НЕ У ПОЯСА.
        /// Решение владельца 04.11.2026: «подними нулевую точку контроллеров на уровень экрана
        /// настроек и всё это поставь на нормальный уровень». Было 1,05 м (пояс).
        /// </summary>
        [Test]
        public void PanelFallback_AtSettingsScreenLevel()
        {
            string panel = ReadScript("KvXrGroupPanel.cs");

            StringAssert.Contains("DefaultBeltHeightMeters = 1.45f", panel,
                "запасная высота панели снова у пояса: владелец просил уровень экрана настроек");
            StringAssert.DoesNotContain("beltHeightMeters = 1.05f", panel,
                "старая высота пояса (1,05 м) вернулась");
            StringAssert.Contains("Mathf.Clamp(beltHeightMeters, 1.10f, 1.60f)", panel,
                "нижняя граница высоты панели снова ниже нормального уровня");
            StringAssert.Contains("УРОВНЕ ЭКРАНА НАСТРОЕК", panel,
                "в журнале нет честной строки, куда встала панель");
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
        /// ЗОНЫ ДОСТИЖИМОСТИ НЕ ВКЛЮЧАЮТСЯ САМИ (жалоба владельца 05.11.2026). Дефект: через
        /// сценарии/команды визуализация поднималась снова после чистого вида. Теперь есть сторож
        /// `GuardVisuals`, настройка по умолчанию ВЫКЛ, и явные переключатели в настройках шлема.
        /// </summary>
        [Test]
        public void WorkspaceVisuals_OffByDefault_Guarded_AndSwitchableInSettings()
        {
            string settings = ReadScript("KvXrSettings.cs");
            StringAssert.Contains("DefaultWorkspaceVisible = 0", settings,
                "зона достижимости снова включена по умолчанию — владелец увидит её сам по себе");
            StringAssert.Contains("DefaultJointLimitsVisible = 0", settings,
                "лимиты суставов снова включены по умолчанию");

            string bridge = ReadScript("KvXrFlowBridge.cs");
            StringAssert.Contains("GuardVisuals", bridge,
                "нет сторожа визуализаций: зона достижимости снова сможет включиться сама");
            StringAssert.Contains("ЗОНА ДОСТИЖИМОСТИ: включилась сама", bridge,
                "нет честной строки журнала о самовольном включении");

            string panel = ReadScript("KvXrGroupPanel.cs");
            StringAssert.Contains("Зона достижимости: ", panel,
                "в настройках шлема нет переключателя зоны достижимости — владелец его не нашёл");
            StringAssert.Contains("Лимиты суставов: ", panel,
                "в настройках шлема нет переключателя лимитов суставов");
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
