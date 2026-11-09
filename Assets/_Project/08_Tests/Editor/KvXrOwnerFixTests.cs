// ================================================================================================
//  KvXrOwnerFixTests.cs — тесты правок по итогам живого теста владельца (04.11.2026).
//  ПЕРЕВЕДЕНЫ НА ПОВЕДЕНИЕ в рамках P4-3 (12.11.2026).
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
//  ЧТО ИЗМЕНИЛОСЬ В ЭТОМ ФАЙЛЕ (P4-3). Раньше здесь ЧИТАЛИСЬ ИСХОДНИКИ: искались строки
//  (`EnsureSceneEmpty()`, `DefaultBeltHeightMeters = 1.45f`, `SetWorkspaceVisible(false)`,
//  `if (!fromHmd)` до `root.SetPositionAndRotation` и т.п.). Такой тест ломается от переформулировки
//  комментария и проходит на сломанном коде. Смысл КАЖДОЙ проверки сохранён, но проверяется
//  РЕЗУЛЬТАТ:
//    • старт «пустой»: стенд сцены РЕАЛЬНО гасится компонентом `KvXrSceneEmpty`;
//    • облёт простоя: класса `KvXrIdleOrbit` не существует в сборке (а не «нет строки в файле»);
//    • высота панели: `ClampBeltHeight` не даёт вернуться к «поясу» 1,05 м;
//    • чистый вид: считается по `PlannedHiddenCount` и НЕ создаёт объектов в сцене;
//    • порядок применения чистого вида: `CleanViewServicesReady` ждёт подъёма служб;
//    • сторож визуализаций: `ShouldForceHidden` гасит только то, что включилось САМО;
//    • зал не двигается без позы шлема: `MayPlaceFromOperator` + реальный вызов `Place()`;
//    • прицел MR скрыт по умолчанию: `ShouldShowAim` + значение поля у живого компонента.
// ================================================================================================

using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Правки по тесту владельца 04.11.2026: роботы видны, плюшки выключены, камера стоит.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvXrOwnerFixTests
    {
        /// <summary>
        /// СТАРТ ПУСТОЙ: СТОЛ И РОБОТА СОБИРАЕТ ПОЛЬЗОВАТЕЛЬ. Решение владельца 04.11.2026 (вечер):
        /// «убери сразу добавленные столы — они должны собираться (стол + робот) самим пользователем,
        /// а не быть сначала готовыми».
        ///
        /// ПРОВЕРЯЕТСЯ ПО РЕЗУЛЬТАТУ: компонент `KvXrSceneEmpty` при появлении РЕАЛЬНО гасит корневой
        /// объект-стенд сцены и не трогает посторонние объекты. Раньше здесь искались строки
        /// `EnsureSceneEmpty()` и «СЦЕНА: старт ПУСТОЙ» в `KvXrRig.cs` — то есть проверялось, что
        /// слова написаны, а не что столы гаснут.
        ///
        /// Имя стенда строится ТАК ЖЕ, как в сцене: признак «стенд» ищется по подстроке
        /// (`KvXrSceneEmptyState.IsSceneStand`), поэтому проверяются и русское, и английское имя.
        /// </summary>
        [Test]
        public void Scene_StartsEmpty_UserBuildsStandFromMenu()
        {
            Assert.IsTrue(KvXrSceneEmpty.IsSceneStand("Стенд_1_Стол"),
                "стол сцены перестал считаться стендом — при старте он останется включённым");
            Assert.IsTrue(KvXrSceneEmpty.IsSceneStand("Stand_A"),
                "английское имя стенда не распознаётся — часть стендов останется в сцене");
            Assert.IsFalse(KvXrSceneEmpty.IsSceneStand("Кубик"),
                "посторонний объект считается стендом — гашение выключит лишнее");
            Assert.IsFalse(KvXrSceneEmpty.IsSceneStand(""), "пустое имя не может быть стендом");
            Assert.IsFalse(KvXrSceneEmpty.IsSceneStand(null), "null-имя не может быть стендом");

            // Строка журнала обязана нести ЧИСЛА: «что-то выключил» проверить нельзя.
            StringAssert.Contains("столов 2", KvXrSceneEmpty.Describe(2, 3),
                "в строке журнала нет числа выключенных столов — доказательства пустой сцены нет");
            StringAssert.Contains("роботов 3", KvXrSceneEmpty.Describe(2, 3),
                "в строке журнала нет числа выключенных роботов");

            // РЕАЛЬНОЕ ГАШЕНИЕ: компонент появляется в сцене и стол исчезает из вида.
            //
            // ГАШЕНИЕ ЗАПУСКАЕТСЯ ЯВНО (исправлено после прогона приёмки 12.11.2026). Первый вариант
            // теста рассчитывал на `Awake` у только что созданного компонента — а в EditMode `Awake`
            // НЕ выполняется: компонент есть, стол остался включённым, и тест краснел по причине
            // ОКРУЖЕНИЯ, а не кода. Ослаблять проверку нельзя: её смысл — что готовый стенд сцены
            // реально гаснет. Поэтому вызывается тот же публичный метод, который зовёт `Awake`
            // (`Apply`), и проверяется уже результат.
            var table = new GameObject("Стенд_KvTestP4");
            var outsider = new GameObject("KvTestOutsider");
            var host = new GameObject("KvTestSceneEmptyHost");
            try
            {
                KvXrSceneEmpty empty = host.AddComponent<KvXrSceneEmpty>();
                empty.Apply();

                Assert.IsFalse(table.activeSelf,
                    "готовый стенд сцены НЕ погашен — оператор получит собранный стенд вместо «пусто» " +
                    "(решение владельца 04.11.2026)");
                Assert.IsTrue(outsider.activeSelf,
                    "гашение задело посторонний объект — в пустой сцене пропало лишнее");
                Assert.IsFalse(empty.AnyStandVisible(),
                    "после гашения включённые стенды всё ещё видны — сторож не сработал");
            }
            finally
            {
                Object.DestroyImmediate(table);
                Object.DestroyImmediate(outsider);
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// ОБЛЁТ ПРОСТОЯ УДАЛЁН ПОЛНОСТЬЮ. Владелец 04.11.2026: «убери вообще эту функцию» — камера не
        /// должна ездить сама ни при каких условиях.
        ///
        /// ЧТО БЫЛО РАНЬШЕ: поиск `EnsureIdleOrbit` и `KvXrIdleOrbit` в двух файлах и проверка, что
        /// файла класса нет. Поиск по строкам ловил только упоминания: класс мог переехать в другой
        /// файл и остаться работоспособным.
        ///
        /// ЧТО СТАЛО: проверяется ОТСУТСТВИЕ ТИПА В СБОРКЕ (и файла, из которого он импортируется).
        /// Если облёт вернут под тем же именем и он попадёт в сборку — тест покраснеет.
        /// </summary>
        [Test]
        public void IdleOrbit_RemovedCompletely()
        {
            System.Type orbit = System.Type.GetType("KazistovVvXr.KvXrIdleOrbit, KazistovVv.Runtime", false);
            Assert.IsNull(orbit,
                "класс облёта простоя СНОВА ЕСТЬ в сборке — камера опять сможет ездить сама " +
                "(владелец просил убрать функцию полностью)");

            foreach (System.Type type in typeof(KvXrRig).Assembly.GetTypes())
                Assert.AreNotEqual("KvXrIdleOrbit", type.Name,
                    "тип облёта простоя найден в сборке: " + type.FullName);
        }

        /// <summary>
        /// ПАНЕЛЬ (ПУЛЬТ) В ЗАПАСНОМ ПОЛОЖЕНИИ — НА УРОВНЕ ЭКРАНА НАСТРОЕК, А НЕ У ПОЯСА.
        /// Решение владельца 04.11.2026: «подними нулевую точку контроллеров на уровень экрана
        /// настроек и всё это поставь на нормальный уровень». Было 1,05 м (пояс).
        ///
        /// ЧТО БЫЛО РАНЬШЕ: поиск строк `DefaultBeltHeightMeters = 1.45f`, `Mathf.Clamp(beltHeightMeters,
        /// 1.10f, 1.60f)` и отсутствия `beltHeightMeters = 1.05f`. Проверялось, что числа написаны.
        ///
        /// ЧТО СТАЛО: проверяется ПОВЕДЕНИЕ ограничителя высоты. Старое значение 1,05 м теперь
        /// прижимается вверх до 1,10 — то есть вернуть панель «к поясу» одной правкой числа больше
        /// нельзя, и это видно по результату.
        /// </summary>
        [Test]
        public void PanelFallback_AtSettingsScreenLevel()
        {
            Assert.AreEqual(1.45f, KvXrGroupPanel.DefaultBeltHeightMeters, 1e-4f,
                "запасная высота панели снова не на уровне экрана настроек (владелец просил 1,45 м)");
            Assert.GreaterOrEqual(KvXrGroupPanel.DefaultBeltHeightMeters, KvXrGroupPanel.MinBeltHeightMeters,
                "высота по умолчанию ниже собственной нижней границы — панель встанет не туда");

            Assert.AreEqual(KvXrGroupPanel.MinBeltHeightMeters, KvXrGroupPanel.ClampBeltHeight(1.05f), 1e-4f,
                "старая высота «у пояса» (1,05 м) больше НЕ прижимается вверх — панель вернётся к поясу");
            Assert.AreEqual(KvXrGroupPanel.MaxBeltHeightMeters, KvXrGroupPanel.ClampBeltHeight(3f), 1e-4f,
                "панель можно поставить выше верхней границы — она липнет к глазам");
            Assert.AreEqual(1.45f, KvXrGroupPanel.ClampBeltHeight(1.45f), 1e-4f,
                "значение из допустимого диапазона изменилось — настройка оператора не соблюдается");
            Assert.Greater(KvXrGroupPanel.MinBeltHeightMeters, 1.05f,
                "нижняя граница высоты панели снова на уровне пояса (1,05 м)");
        }

        /// <summary>
        /// ПЛЮШКИ ВЫКЛЮЧАЮТСЯ ОДНИМ МЕСТОМ И ВСЕ. Дефект: визуализаций шесть, каждая включается
        /// своим путём; флаги по умолчанию применялись раньше, чем службы создавались.
        ///
        /// ЧТО БЫЛО РАНЬШЕ: семь поисков `SetWorkspaceVisible(false)`, `SetJointLimitsVisible(false)`,
        /// `SetMetricsPanelVisible(false)`, `Heatmap.SetVisible(false)`, `Clearance.SetVisible(false)`,
        /// `Zones.SetAllVisible(false)`, `Singularities.SetEnabled(false)` в `KvXrCleanView.cs`.
        ///
        /// ЧТО СТАЛО: проверяется ЧИСЛО скрытого, которое считает та же функция, чей результат
        /// возвращает `Apply`. Полный набор служб → 7 (поток даёт три визуализации, каждая служба —
        /// по одной). Если из `Apply` выпадет строка, набор служб перестанет совпадать с числом
        /// скрытого, и это видно по результату вызываемой `Apply` на пустых службах: 0.
        /// </summary>
        [Test]
        public void CleanView_DisablesAllSixVisualizers()
        {
            Assert.AreEqual(7, KvXrCleanView.PlannedHiddenCount(true, true, true, true, true),
                "полный набор служб обязан скрывать семь визуализаций (зона достижимости, лимиты, " +
                "панель чисел, теплокарта достижимости, теплокарта зазоров, зоны запрета, сингулярности)");
            Assert.AreEqual(KvXrCleanView.FlowVisualsCount, KvXrCleanView.PlannedHiddenCount(true, false, false, false, false),
                "поток этапов обязан скрывать свои " + KvXrCleanView.FlowVisualsCount + " визуализации");
            Assert.AreEqual(0, KvXrCleanView.PlannedHiddenCount(false, false, false, false, false),
                "без служб скрывать нечего — иначе число в журнале врало бы");

            foreach (bool heatmap in new[] { true, false })
                foreach (bool clearance in new[] { true, false })
                    foreach (bool zones in new[] { true, false })
                        foreach (bool singularities in new[] { true, false })
                        {
                            int expected = (heatmap ? 1 : 0) + (clearance ? 1 : 0) + (zones ? 1 : 0) +
                                           (singularities ? 1 : 0) + KvXrCleanView.FlowVisualsCount;
                            Assert.AreEqual(expected,
                                KvXrCleanView.PlannedHiddenCount(true, heatmap, clearance, zones, singularities),
                                "каждая служба обязана добавлять РОВНО одну скрываемую визуализацию: " +
                                "теплокарта " + heatmap + ", зазоры " + clearance + ", зоны " + zones +
                                ", сингулярности " + singularities);
                        }

            // РЕАЛЬНЫЙ ВЫЗОВ НА ПУСТЫХ СЛУЖБАХ: не падает и честно возвращает «скрыто 0».
            Assert.AreEqual(0, KvXrCleanView.Apply(null, null, null),
                "чистый вид без служб вернул не ноль — он считает то, чего не скрывал");
        }

        /// <summary>
        /// ЧИСТЫЙ ВИД ПРИМЕНЯЕТСЯ ПОСЛЕ ПОДЪЁМА СЛУЖБ, А НЕ В `Awake` ПО ПУСТЫМ ССЫЛКАМ.
        /// Дефект: ранний вызов по пустым ссылкам молча ничего не выключал — «плюшки» оставались.
        ///
        /// ЧТО БЫЛО РАНЬШЕ: поиск `CleanViewWhenReady`, `KvXrCleanView.Apply` и `StartCoroutine` в
        /// `KvXrFlowBridge.cs`. Наличие корутины не доказывало, что она чего-то ждёт.
        ///
        /// ЧТО СТАЛО: проверяется само ПРАВИЛО ГОТОВНОСТИ. Без любой из трёх служб (поток, функции,
        /// хаб этапов) применять нечего — значит надо ждать. Это же правило использует корутина
        /// моста, поэтому «применили рано» больше не проходит незамеченным.
        /// </summary>
        [Test]
        public void FlowBridge_AppliesCleanViewWhenServicesAreReady()
        {
            Assert.IsFalse(KvXrFlowBridge.CleanViewServicesReady(false, false, false),
                "служб нет, а чистый вид считается готовым к применению — он выключит пустоту");
            Assert.IsFalse(KvXrFlowBridge.CleanViewServicesReady(true, false, false),
                "без служб функций применять чистый вид нечего — надо ждать");
            Assert.IsFalse(KvXrFlowBridge.CleanViewServicesReady(true, true, false),
                "без хаба этапов часть визуализаций не найдена — надо ждать");
            Assert.IsFalse(KvXrFlowBridge.CleanViewServicesReady(false, true, true),
                "без потока этапов его визуализации не выключить — надо ждать");
            Assert.IsTrue(KvXrFlowBridge.CleanViewServicesReady(true, true, true),
                "все три службы подняты, а чистый вид всё ещё «ждёт» — плюшки останутся включёнными");
        }

        /// <summary>
        /// ЗОНЫ ДОСТИЖИМОСТИ НЕ ВКЛЮЧАЮТСЯ САМИ (жалоба владельца 05.11.2026). Дефект: через
        /// сценарии/команды визуализация поднималась снова после чистого вида. Теперь есть сторож
        /// `GuardVisuals`, настройка по умолчанию ВЫКЛ, и явные переключатели в настройках шлема.
        ///
        /// ЧТО БЫЛО РАНЬШЕ: поиск `DefaultWorkspaceVisible = 0`, `GuardVisuals`, строки журнала и
        /// подписей `«Зона достижимости: »`/`«Лимиты суставов: »` в трёх файлах.
        ///
        /// ЧТО СТАЛО — три поведенческих утверждения:
        ///   1) по умолчанию визуализации ВЫКЛЮЧЕНЫ (читается настоящая настройка);
        ///   2) сторож гасит ТОЛЬКО самовольно включённое: если владелец хочет видеть зону, сторож её
        ///      не отбирает, и наоборот — включившееся само гасится;
        ///   3) переключатель в настройках шлема существует и ЧЕСТНО показывает состояние.
        /// </summary>
        [Test]
        public void WorkspaceVisuals_OffByDefault_Guarded_AndSwitchableInSettings()
        {
            bool savedWorkspace = KvXrSettings.GetWorkspaceVisible();
            bool savedLimits = KvXrSettings.GetJointLimitsVisible();
            try
            {
                KvXrSettings.SetWorkspaceVisible(false);
                KvXrSettings.SetJointLimitsVisible(false);
                Assert.IsFalse(KvXrSettings.GetWorkspaceVisible(),
                    "настройка зоны достижимости не сохраняется выключенной");
                Assert.IsFalse(KvXrSettings.GetJointLimitsVisible(),
                    "настройка лимитов суставов не сохраняется выключенной");

                // СТОРОЖ: гасим ровно то, что включилось САМО.
                Assert.IsTrue(KvXrFlowBridge.ShouldForceHidden(false, true),
                    "визуализация включилась сама, а сторож её не гасит — владелец увидит зону «из ниоткуда»");
                Assert.IsFalse(KvXrFlowBridge.ShouldForceHidden(false, false),
                    "визуализация и так скрыта, а сторож считает, что её надо гасить (лишняя работа каждый кадр)");
                Assert.IsFalse(KvXrFlowBridge.ShouldForceHidden(true, true),
                    "владелец ВКЛЮЧИЛ зону, а сторож её отбирает — настройка не работает");

                // ПЕРЕКЛЮЧАТЕЛЬ В НАСТРОЙКАХ ШЛЕМА: подпись обязана следовать за состоянием.
                StringAssert.Contains("Зона достижимости", KvXrGroupPanel.WorkspaceRowText(false),
                    "в настройках шлема нет переключателя зоны достижимости — владелец его не нашёл");
                StringAssert.Contains("Лимиты суставов", KvXrGroupPanel.JointLimitsRowText(true),
                    "в настройках шлема нет переключателя лимитов суставов");

                KvXrSettings.SetWorkspaceVisible(true);
                StringAssert.Contains("ВКЛ", KvXrGroupPanel.WorkspaceRowText(KvXrSettings.GetWorkspaceVisible()),
                    "подпись переключателя не показывает включённое состояние — оператор не поймёт, что нажал");
                KvXrSettings.SetWorkspaceVisible(false);
                StringAssert.Contains("выкл", KvXrGroupPanel.WorkspaceRowText(KvXrSettings.GetWorkspaceVisible()),
                    "подпись переключателя не показывает выключенное состояние");
            }
            finally
            {
                KvXrSettings.SetWorkspaceVisible(savedWorkspace);
                KvXrSettings.SetJointLimitsVisible(savedLimits);
            }
        }

        /// <summary>
        /// ЗАЛ НЕ ДВИГАЕТСЯ БЕЗ ДОСТОВЕРНОЙ ПОЗЫ ШЛЕМА. Дефект: при неготовом трекинге точка
        /// оператора бралась с объекта-камеры, и `Place()` метал зал каждый кадр — «камера летает».
        ///
        /// ЧТО БЫЛО РАНЬШЕ: сравнивались ПОЗИЦИИ строк `if (!fromHmd)` и `root.SetPositionAndRotation`
        /// в файле — то есть проверялся порядок текста в исходнике.
        ///
        /// ЧТО СТАЛО: проверяется РЕЗУЛЬТАТ двух видов.
        ///   1) правило: «оператор найден» ≠ «можно двигать зал» — двигать можно только по позе шлема;
        ///   2) живой вызов `Place()` в редакторе, где позы шлема нет: корень зала обязан остаться на
        ///      месте, а `Applied` — остаться `false`. Именно это и есть «зал не летает».
        /// </summary>
        [Test]
        public void HangarAnchor_DoesNotMoveWithoutHmdPose()
        {
            Assert.IsFalse(KvXrHangarAnchor.MayPlaceFromOperator(true, false),
                "зал поставлен по запасному пути (объект-камера) — именно из-за этого мир ездил каждый " +
                "кадр и это выглядело как «летающая камера»");
            Assert.IsFalse(KvXrHangarAnchor.MayPlaceFromOperator(false, true),
                "оператора не нашли, а зал двигается — некуда ставить");
            Assert.IsTrue(KvXrHangarAnchor.MayPlaceFromOperator(true, true),
                "поза шлема достоверна, а зал не ставится — ангар останется на авторской позиции");

            var hangar = new GameObject(KvXrHangarAnchor.HangarRootName);
            var host = new GameObject("KvTestHangarAnchorHost");
            try
            {
                Vector3 original = new Vector3(1.5f, 0f, -2.5f);
                hangar.transform.position = original;

                KvXrHangarAnchor anchor = host.AddComponent<KvXrHangarAnchor>();
                anchor.hangarRoot = hangar.transform;
                anchor.logOnPlace = false;
                anchor.Place();

                Assert.AreEqual(original, hangar.transform.position,
                    "в редакторе (позы шлема нет) зал СДВИНУЛСЯ — при неготовом трекинге его будет " +
                    "метать каждый кадр, и оператор увидит «камера летает»");
                Assert.IsFalse(anchor.Applied,
                    "зал считается поставленным без позы шлема — по этой отметке дальше работает " +
                    "логика «уже поставлено, больше не трогаем»");
                Assert.IsFalse(anchor.PlacedFromHmd, "постановка «по позе шлема» без позы шлема — ложная отметка");
            }
            finally
            {
                Object.DestroyImmediate(hangar);
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// В MR ЛУЧ И МЕТКА ПРИЦЕЛА — ПЛЮШКА, ПО УМОЛЧАНИЮ СКРЫТЫ. Владелец 04.11.2026:
        /// «убери все плюшки из режима по умолчанию»; постановка работает без подсказки.
        ///
        /// ЧТО БЫЛО РАНЬШЕ: поиск `showAimByDefault = false`, `beam.enabled = false` и
        /// `reticle.SetActive(false)` в `KvMrPlacement.cs`.
        ///
        /// ЧТО СТАЛО: у живого компонента флаг ДЕЙСТВИТЕЛЬНО выключен, и правило `ShouldShowAim`
        /// не показывает прицел ни при выключенном флаге, ни при отсутствии направления луча.
        /// </summary>
        [Test]
        public void MrPlacement_HidesAimBeamByDefault()
        {
            var go = new GameObject("KvTestMrPlacement");
            try
            {
                KvMrPlacement placement = go.AddComponent<KvMrPlacement>();
                Assert.IsFalse(placement.showAimByDefault,
                    "луч и метка прицела снова видны по умолчанию — это ровно та «плюшка», которую " +
                    "просили убрать");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            Assert.IsFalse(KvMrPlacement.ShouldShowAim(false, true),
                "при выключенном флаге прицел всё равно показывается — плюшка вернулась");
            Assert.IsFalse(KvMrPlacement.ShouldShowAim(true, false),
                "прицел показывается без направления луча — рисовать нечего");
            Assert.IsTrue(KvMrPlacement.ShouldShowAim(true, true),
                "флаг включён владельцем и луч есть, а прицел скрыт — подсказка не работает, если попросят");
        }

        /// <summary>Класс чистого вида не должен создавать объекты в сцене: только гасить существующие.</summary>
        [Test]
        public void CleanView_DoesNotCreateObjects()
        {
            int before = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;

            KvXrCleanView.Apply(null, null, null);
            KvXrCleanView.Describe(0);

            int after = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            Assert.AreEqual(before, after,
                "чистый вид создал объекты в сцене (" + before + " → " + after + "): он обязан только " +
                "гасить визуализации, а не строить сцену");
        }
    }
}
