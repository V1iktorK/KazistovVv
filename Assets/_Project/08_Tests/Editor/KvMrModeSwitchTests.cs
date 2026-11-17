// ================================================================================================
//  KvMrModeSwitchTests.cs — EditMode-тесты ПЕРЕКЛЮЧАТЕЛЯ VR / MR В ГРАФЕ «НАСТРОЙКИ» (17.11.2026).
//
//  ОТКУДА ЗАДАЧА. Владелец (просил НЕСКОЛЬКО РАЗ): «под двумя подпунктами в графе настройки —
//  переключатель из VR на MR». Два места, где владелец ищет этот переключатель:
//    1) панель «Настройки и справка» → вкладка «Управление» (компьютер/планшет/окно в шлеме);
//    2) колонка «Настройки» СТАРТОВОГО МЕНЮ шлема — там ровно ДВА подпункта
//       («Настройки шлема», «Интерфейс компьютера»), и переключателя между ними не было.
//
//  ЧТО ПРОВЕРЯЕТСЯ (поведением, не грепом исходников — храповик P4-3 запрещает новые файлы,
//  читающие тексты исходников):
//    а) строка-переключатель ЕСТЬ и стоит ПОД содержимым вкладки (после строк биндов);
//    б) подпись/положение отражает ФАКТ: сменили режим — сегмент показал новый режим;
//    в) нажатие реально меняет режим — через ОБЩЕЕ хранилище `KvXrSettings` (не своё поле);
//    г) в шлеме третья строка колонки «Настройки» строится под двумя прежними и ведёт на тот же
//       механизм (`ToggleMixedReality` → `KvXrSettings.ToggleMode`).
//
//  ОКРУЖЕНИЕ. Режим живёт в PlayerPrefs (общие настройки машины) — тест обязан вернуть его
//  как был, иначе прогон оставит владельцу включённый MR.
// ================================================================================================

using KazistovVvUI;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvTests
{
    /// <summary>Переключатель VR/MR в графе «Настройки»: панель настроек и стартовое меню шлема.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvMrModeSwitchTests
    {
        private int savedPassthrough;
        private bool hadSavedPassthrough;

        [SetUp]
        public void SaveEnvironment()
        {
            hadSavedPassthrough = PlayerPrefs.HasKey(KvXrSettings.KeyPassthrough);
            savedPassthrough = PlayerPrefs.GetInt(KvXrSettings.KeyPassthrough, 0);
        }

        [TearDown]
        public void RestoreEnvironment()
        {
            if (hadSavedPassthrough) PlayerPrefs.SetInt(KvXrSettings.KeyPassthrough, savedPassthrough);
            else PlayerPrefs.DeleteKey(KvXrSettings.KeyPassthrough);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ вспомогательное

        /// <summary>Имя сегмента-переключателя вкладки «Управление» (строка носит имя `Seg_…`).</summary>
        public const string SegmentName = "VrMrMenu";

        private static Transform FindByName(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == name) return t;
            return null;
        }

        private static int IndexInParent(Transform child)
        {
            if (child == null || child.parent == null) return -1;
            return child.GetSiblingIndex();
        }

        private static KvSegmented FindSwitch(Transform root)
        {
            Transform row = FindByName(root, "Seg_" + SegmentName);
            return row != null ? row.GetComponent<KvSegmented>() : null;
        }

        /// <summary>Панель настроек в памяти, собранная как в оболочке (канвас → Body → Content).</summary>
        private static KvSettingsView BuildSettingsView(out GameObject host)
        {
            host = new GameObject("KvMrModeSwitchHost");
            GameObject canvasGo = new GameObject("Canvas", typeof(Canvas));
            canvasGo.transform.SetParent(host.transform, false);
            GameObject bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(canvasGo.transform, false);

            KvSettingsView view = bodyGo.AddComponent<KvSettingsView>();
            var callbacks = new KvSettingsCallbacks
            {
                HasFeature = delegate { return true; },
                GetFeature = delegate { return false; },
                GetMode = delegate { return KvXrSettings.GetModeIndex(); },
                SetMode = delegate (int i) { KvXrSettings.SetModeIndex(i); }
            };
            view.Build((RectTransform)bodyGo.transform, callbacks);
            return view;
        }

        // ==================================================== А. ПАНЕЛЬ «НАСТРОЙКИ И СПРАВКА»

        /// <summary>
        /// ВКЛАДКА «УПРАВЛЕНИЕ» НЕСЁТ СТРОКУ-ПЕРЕКЛЮЧАТЕЛЬ ПОД СУЩЕСТВУЮЩИМИ ПУНКТАМИ.
        ///
        /// Что ловит: переключателя на вкладке не было вовсе — владелец искал его там, где
        /// читает управление, и не находил. Проверяется и место (ПОСЛЕ строк биндов, под
        /// заголовком «Настройки»), и то, что это НАСТОЯЩИЙ сегмент на два положения.
        /// </summary>
        [Test]
        public void SettingsPanel_ControlsTab_HasModeSwitchBelowExistingRows()
        {
            GameObject host;
            KvSettingsView view = BuildSettingsView(out host);
            try
            {
                view.SetTab(KvSettingsView.IndexOfTab("Управление"));

                Transform content = null;
                foreach (Transform t in view.GetComponentsInChildren<Transform>(true))
                    if (t != null && t.name == "Content") { content = t; break; }
                Assert.IsNotNull(content, "у панели настроек нет контейнера Content");

                KvSegmented seg = FindSwitch(content);
                Assert.IsNotNull(seg,
                    "на вкладке «Управление» нет строки-переключателя «Режим: VR / MR» — " +
                    "владелец просил его именно здесь");
                Assert.AreEqual(KvXrSettings.ModeCount, 2,
                    "переключатель обязан иметь два положения (VR и MR)");

                // ПОД СУЩЕСТВУЮЩИМИ ПУНКТАМИ: заголовок графы «Настройки» идёт ПОСЛЕ строк биндов.
                Transform header = FindByName(content, "H_" +
                    KvLoc.T("settings.mode.section", "Настройки"));
                Assert.IsNotNull(header, "нет заголовка графы «Настройки» на вкладке «Управление»");
                Assert.Less(IndexInParent(header), IndexInParent(seg.transform),
                    "заголовок обязан стоять над переключателем (иначе графа не читается сверху вниз)");

                int lastBind = -1;
                int headerIndex = IndexInParent(header);
                foreach (Transform child in content)
                {
                    if (child != null && child.name.StartsWith("Bind_", System.StringComparison.Ordinal))
                        lastBind = Mathf.Max(lastBind, child.GetSiblingIndex());
                }
                Assert.GreaterOrEqual(lastBind, 0,
                    "на вкладке «Управление» не построилась ни одна строка биндов — проверка вакуумна");
                Assert.Greater(headerIndex, lastBind,
                    "графа «Настройки» обязана стоять ПОД существующими подпунктами вкладки, " +
                    "а не перед ними (заголовок " + headerIndex + " ≤ последний бинд " + lastBind + ")");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// ПОДПИСЬ ОТРАЖАЕТ ФАКТ, А НЕ ВСЕГДА ОДНО И ТО ЖЕ.
        ///
        /// Что ловит: переключатель, нарисованный «как картинка» — сегмент всегда на VR при
        /// включённом MR. Положение обязано читаться из того же хранилища, что и вся ветка MR.
        /// </summary>
        [Test]
        public void SettingsPanel_SegmentReflectsActualMode()
        {
            GameObject host;
            KvSettingsView view = BuildSettingsView(out host);
            try
            {
                KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
                view.SetTab(KvSettingsView.IndexOfTab("Управление"));
                KvSegmented seg = FindSwitch(view.transform);
                Assert.IsNotNull(seg, "переключатель не построился после смены режима");
                Assert.AreEqual(KvXrSettings.ModeMr, seg.Index,
                    "при выбранном MR сегмент обязан показывать MR, а не VR");

                KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
                view.SetTab(KvSettingsView.IndexOfTab("Управление"));
                seg = FindSwitch(view.transform);
                Assert.IsNotNull(seg, "переключатель не построился после возврата в VR");
                Assert.AreEqual(KvXrSettings.ModeVr, seg.Index,
                    "при выбранном VR сегмент обязан показывать VR");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// НАЖАТИЕ РЕАЛЬНО МЕНЯЕТ РЕЖИМ — ТЕМ ЖЕ МЕХАНИЗМОМ, ЧТО И ОСТАЛЬНЫЕ ПАНЕЛИ.
        ///
        /// Что ловит: сегмент есть, но его обработчик никуда не подключён (кнопка-картинка).
        /// Здесь жмётся настоящая кнопка сегмента и проверяется общее хранилище `KvXrSettings`.
        /// </summary>
        [Test]
        public void SettingsPanel_PressingSegmentWritesSharedModeSetting()
        {
            GameObject host;
            KvSettingsView view = BuildSettingsView(out host);
            try
            {
                KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
                view.SetTab(KvSettingsView.IndexOfTab("Управление"));
                KvSegmented seg = FindSwitch(view.transform);
                Assert.IsNotNull(seg, "переключатель не построился");

                Transform mrSegment = FindByName(seg.transform, "Seg1");
                Assert.IsNotNull(mrSegment, "у переключателя нет второго сегмента (MR)");
                Button button = mrSegment.GetComponent<Button>();
                Assert.IsNotNull(button, "сегмент MR — не кнопка: по нему нечем нажать");
                button.onClick.Invoke();

                Assert.AreEqual(KvXrSettings.ModeMr, KvXrSettings.GetModeIndex(),
                    "нажатие «MR» обязано записать MR в ОБЩЕЕ хранилище (KvXrSettings), " +
                    "иначе панель и ветка MR разойдутся");

                // ОБРАТНО: VR тоже пишется туда же. Нажатие пересобирает панель (Rebuild), поэтому
                // старый сегмент УНИЧТОЖЕН — искать кнопку нужно заново, а не по мёртвой ссылке.
                seg = FindSwitch(view.transform);
                Assert.IsNotNull(seg, "после пересборки панели переключателя больше нет");
                Transform vrSegment = FindByName(seg.transform, "Seg0");
                Assert.IsNotNull(vrSegment, "у переключателя нет первого сегмента (VR)");
                vrSegment.GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(KvXrSettings.ModeVr, KvXrSettings.GetModeIndex(),
                    "нажатие «VR» обязано вернуть VR в то же хранилище");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ==================================================== Б. СТАРТОВОЕ МЕНЮ ШЛЕМА

        /// <summary>Панель шлема в памяти со стартовым меню.</summary>
        private static KvXrGroupPanel NewStartMenuPanel(out GameObject host)
        {
            host = new GameObject("KvMrModeSwitchMenuHost");
            var panel = host.AddComponent<KvXrGroupPanel>();
            panel.logOnBuild = false;
            panel.followHand = false;
            panel.Rebuild();
            panel.OpenStartMenu();
            return panel;
        }

        /// <summary>Номер ЦЕЛИ ЛУЧА по точному названию (−1 — нет).</summary>
        private static int IndexOfTarget(KvXrGroupPanel panel, string title)
        {
            for (int i = 0; i < panel.RayTargetCount; i++)
                if (panel.TitleAt(i) == title) return i;
            return -1;
        }

        /// <summary>
        /// КОЛОНКА «НАСТРОЙКИ» СТАРТОВОГО МЕНЮ: ТРЕТЬЯ СТРОКА — ПЕРЕКЛЮЧАТЕЛЬ, ПОД ДВУМЯ ПРЕЖНИМИ.
        ///
        /// ОТКУДА. Владелец: «под двумя подпунктами в графе настройки — переключатель из VR на MR».
        /// В графе стартового меню у раздела «Настройки» ровно два подпункта («Настройки шлема»,
        /// «Интерфейс компьютера») — переключателя там не было, и режим из меню шлема менялся
        /// только через экран «Настройки N из 7».
        ///
        /// Проверяется и место (строка идёт ПОСЛЕ двух прежних), и то, что нажатие ведёт на тот же
        /// механизм, что и кнопка `Set_Mode` экрана настроек.
        /// </summary>
        [Test]
        public void StartMenu_SettingsColumn_GetsThirdRowSwitchingMode()
        {
            GameObject host;
            KvXrGroupPanel panel = NewStartMenuPanel(out host);
            try
            {
                panel.SelectStartSection((int)KvXrStartSection.Settings);

                int first = IndexOfTarget(panel, "Настройки шлема");
                int second = IndexOfTarget(panel, "Интерфейс компьютера");
                Assert.GreaterOrEqual(first, 0, "прежний подпункт «Настройки шлема» исчез");
                Assert.GreaterOrEqual(second, 0, "прежний подпункт «Интерфейс компьютера» исчез");

                string modeRow = KvXrGroupPanel.ModeRowText(KvXrSettings.GetModeIndex());
                int third = IndexOfTarget(panel, modeRow);
                Assert.GreaterOrEqual(third, 0,
                    "в колонке «Настройки» стартового меню нет строки-переключателя «" +
                    modeRow + "» — под двумя подпунктами должна стоять третья строка");
                Assert.Greater(third, first, "переключатель обязан стоять ПОД первым подпунктом");
                Assert.Greater(third, second, "переключатель обязан стоять ПОД вторым подпунктом");

                // НАЖАТИЕ: номер строки = номер цели минус число иконок разделов (как в меню).
                KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
                panel.Rebuild();
                panel.OpenStartMenu();
                panel.SelectStartSection((int)KvXrStartSection.Settings);
                int row = IndexOfTarget(panel, KvXrGroupPanel.ModeRowText(KvXrSettings.GetModeIndex())) -
                          KvXrStartMenuLayout.SectionCount;
                Assert.GreaterOrEqual(row, 0, "строка переключателя обязана идти после иконок разделов");

                panel.PressStartRow(row);
                Assert.AreEqual(KvXrSettings.ModeMr, KvXrSettings.GetModeIndex(),
                    "нажатие строки в колонке «Настройки» обязано переключить режим VR → MR " +
                    "тем же механизмом, что и кнопка «Режим VR/MR» экрана настроек");

                // Подпись строки показывает ФАКТ после переключения.
                panel.Rebuild();
                panel.OpenStartMenu();
                panel.SelectStartSection((int)KvXrStartSection.Settings);
                StringAssert.Contains("MR", KvXrGroupPanel.ModeRowText(KvXrSettings.GetModeIndex()),
                    "строка меню обязана называть выбранный режим после переключения");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
