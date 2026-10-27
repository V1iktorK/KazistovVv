using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvUI;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ЦЕЛИ ЛУЧА НА ПАНЕЛИ: КАЖДАЯ КНОПКА ДОЛЖНА ЛОВИТЬСЯ ЛАЗЕРОМ (24.10.2026).
    ///
    /// ЖАЛОБА ВЛАДЕЛЬЦА ДОСЛОВНО: «Меня ДО ПЕРЕНОСА не устраивает что не приклеивается лазер к новым
    /// кнопкам внизу контроллера, что выстроены в ряд. информация, яркость и тд».
    ///
    /// ПРИЧИНА. Кнопка состоит из ДВУХ вещей: картинки на канвасе и смещения в списке целей, по
    /// которому считается попадание луча. Смещения раньше вписывались в каждый построитель уровня
    /// вручную (`currentOffsets = offsets`, `currentOffsets = hitOffsets`), и когда в §40 добавили
    /// ряд экранов («Состояние», «Настройки», «Объекты», «Свойства», «Справка») и кнопки внутри
    /// экранов («−», «+», «Дальше», «Закрыть», строки объектов), список НЕ расширили. Иконки были
    /// видны, но луч физически не мог на них навести — он проходил сквозь них.
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ (без шлема, настоящая панель в EditMode):
    ///   • целей ровно столько же, сколько интерактивных иконок — на каждом уровне и каждом экране;
    ///   • РЯД ЭКРАНОВ ВНИЗУ из жалобы владельца присутствует в списке целей своими пятью смещениями;
    ///   • кнопки «−», «+», «Дальше», «Закрыть» экрана «Настройки» тоже в списке целей;
    ///   • две цели не стоят в одном месте: иначе луч нажимал бы «соседнюю» кнопку.
    ///
    /// ТЕСТ НЕ КАСАЕТСЯ СЦЕНЫ И СБОРКИ: панель создаётся в памяти и уничтожается в конце.
    /// </summary>
    public class KvXrPanelTargetTests
    {
        /// <summary>Точность сравнения смещений на канвасе, метры (смещения задаются с 0,001 м).</summary>
        private const float ToleranceMeters = 0.0005f;

        private static KvXrGroupPanel NewPanel(out GameObject host)
        {
            host = new GameObject("KvXrPanelTargetTestHost");
            var panel = host.AddComponent<KvXrGroupPanel>();
            panel.logOnBuild = false;
            panel.Rebuild();
            return panel;
        }

        private static List<Vector2> Targets(KvXrGroupPanel panel)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < panel.RayTargetCount; i++) result.Add(panel.RayTargetOffset(i));
            return result;
        }

        private static bool HasTarget(IReadOnlyList<Vector2> targets, float x, float y)
        {
            for (int i = 0; i < targets.Count; i++)
                if (Mathf.Abs(targets[i].x - x) <= ToleranceMeters &&
                    Mathf.Abs(targets[i].y - y) <= ToleranceMeters) return true;
            return false;
        }

        /// <summary>
        /// ГЛАВНАЯ ПРОВЕРКА ЖАЛОБЫ: ряд экранов внизу панели ловится лучом.
        /// Числа взяты из построения ряда (`AddScreenEntries`): шаг 0,075 м, начало −0,150 м, высота −0,235 м.
        /// </summary>
        [Test]
        public void GroupLevel_ScreenRowIsHittable()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(out host);
            try
            {
                Assert.AreEqual(KvXrPanelLevel.Groups, panel.Level,
                    "панель обязана открыться на уровне групп");

                List<Vector2> targets = Targets(panel);

                Assert.AreEqual(panel.IconCount, panel.RayTargetCount,
                    "целей луча обязано быть столько же, сколько иконок: иначе часть кнопок луч не видит");

                // Восемь групп каталога + пять входов в экраны = тринадцать иконок.
                Assert.AreEqual(KvToolbarGroups.All.Length + 5, panel.IconCount,
                    "на уровне групп строится по иконке на группу плюс ряд из пяти экранов");

                for (int i = 0; i < 5; i++)
                {
                    float x = -0.150f + i * 0.075f;
                    Assert.IsTrue(HasTarget(targets, x, -0.235f),
                        "кнопка ряда экранов №" + (i + 1) + " (смещение " + x.ToString("0.000") +
                        "; −0,235 м) ОТСУТСТВУЕТ в списке целей луча — именно на такие кнопки " +
                        "владелец и жаловался: «не приклеивается лазер к новым кнопкам внизу»");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// КНОПКИ «−», «+», «ДАЛЬШЕ», «ЗАКРЫТЬ» ЭКРАНА «НАСТРОЙКИ» — тоже цели.
        /// Раньше они рисовались, но не были в списке попадания: экран открывался, а нажимать было нечего.
        /// </summary>
        [Test]
        public void SettingsScreen_StepperButtonsAreHittable()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(out host);
            try
            {
                panel.OpenScreen(KvXrPanelLevel.SettingsScreen);

                Assert.AreEqual(KvXrPanelLevel.SettingsScreen, panel.Level,
                    "экран настроек обязан открыться");
                Assert.AreEqual(panel.IconCount, panel.RayTargetCount,
                    "на экране настроек целей обязано быть столько же, сколько иконок");

                List<Vector2> targets = Targets(panel);

                Assert.IsTrue(HasTarget(targets, -0.18f, -0.02f),
                    "кнопка «−» на экране настроек не ловится лучом");
                Assert.IsTrue(HasTarget(targets, 0.18f, -0.02f),
                    "кнопка «+» на экране настроек не ловится лучом");
                Assert.IsTrue(HasTarget(targets, 0f, -0.16f),
                    "кнопка «Дальше» на экране настроек не ловится лучом");
                Assert.IsTrue(HasTarget(targets, 0f, -0.29f),
                    "кнопка «Закрыть» на экране настроек не ловится лучом");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>ВСЕ ПЯТЬ ЭКРАНОВ: у каждого иконки = цели, и «Закрыть» ловится лучом.</summary>
        [Test]
        public void EveryScreen_CloseButtonIsHittableAndCountsMatch()
        {
            KvXrPanelLevel[] screens =
            {
                KvXrPanelLevel.StatusScreen, KvXrPanelLevel.SettingsScreen,
                KvXrPanelLevel.ObjectsScreen, KvXrPanelLevel.PropertiesScreen,
                KvXrPanelLevel.HelpScreen
            };

            GameObject host;
            KvXrGroupPanel panel = NewPanel(out host);
            try
            {
                foreach (KvXrPanelLevel screen in screens)
                {
                    panel.OpenScreen(screen);

                    Assert.AreEqual(panel.IconCount, panel.RayTargetCount,
                        "экран «" + KvXrGroupPanel.ScreenTitle(screen) + "»: целей " +
                        panel.RayTargetCount + " против иконок " + panel.IconCount);

                    Assert.IsTrue(HasTarget(Targets(panel), 0f, -0.29f),
                        "экран «" + KvXrGroupPanel.ScreenTitle(screen) +
                        "»: кнопка «Закрыть» не ловится лучом — выйти из экрана было бы нечем");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// ВЕЕР: закрывающая иконка в центре тоже цель. Это уже случалось (21.10.2026): из веера
        /// нельзя было выйти нажатием, потому что смещения (0,0) не было в списке попадания.
        /// </summary>
        [Test]
        public void FanLevel_HasCenterExitTarget()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(out host);
            try
            {
                panel.OpenGroup(0);

                Assert.AreEqual(KvXrPanelLevel.GroupFan, panel.Level, "веер обязан раскрыться");
                Assert.AreEqual(panel.IconCount, panel.RayTargetCount,
                    "в веере целей обязано быть столько же, сколько иконок");
                Assert.IsTrue(HasTarget(Targets(panel), 0f, 0f),
                    "центральная иконка «Закрыть» в веере не ловится лучом");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// ДВЕ ЦЕЛИ НЕ В ОДНОМ МЕСТЕ. Совпадающие смещения означали бы, что луч нажимает соседнюю
        /// кнопку — это выглядит как «кнопка не работает» и ищется дольше всего.
        /// </summary>
        [Test]
        public void RayTargets_DoNotOverlap()
        {
            GameObject host;
            KvXrGroupPanel panel = NewPanel(out host);
            try
            {
                List<Vector2> targets = Targets(panel);
                for (int i = 0; i < targets.Count; i++)
                    for (int j = i + 1; j < targets.Count; j++)
                        Assert.IsFalse(
                            Mathf.Abs(targets[i].x - targets[j].x) <= ToleranceMeters &&
                            Mathf.Abs(targets[i].y - targets[j].y) <= ToleranceMeters,
                            "цели №" + i + " и №" + j + " стоят в одном месте (" +
                            targets[i].ToString("0.000") + "): луч нажмёт не ту кнопку");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
