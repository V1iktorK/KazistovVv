using NUnit.Framework;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты РЕЕСТРА ОКОН И ПОЯСА (сессия §32, решения владельца [D21] и [D22]).
    ///
    /// ЗАЧЕМ. Владелец решил: окно можно **закрыть** (квадратик) и **свернуть** (чёрточка),
    /// а **скрытые окна убираются на пояс** и достаются оттуда. Эти правила обязаны быть
    /// одинаковыми для всех окон и не зависеть от того, какое окно закрыли. Проверить это
    /// глазами в каждом окне нельзя, поэтому проверяются сами правила — на «окнах-двойниках»
    /// с действиями-счётчиками (ни одного GameObject: правила проекта запрещают объекты в тестах).
    ///
    /// ЧТО ЗДЕСЬ ВАЖНЕЕ ВСЕГО: «закрыть при полном поясе» НЕ должно терять окно. Если места нет,
    /// окно остаётся видимым, а интерфейс обязан сказать «на поясе нет места» — иначе оператор
    /// решит, что закрыл окно, и не найдёт его ни на экране, ни на поясе.
    /// </summary>
    public class KvWindowBeltTests
    {
        /// <summary>Окно-двойник: считает, сколько раз его показали, скрыли и свернули.</summary>
        private class FakeWindow
        {
            public int Shown;
            public int Hidden;
            public int Collapsed;
        }

        private FakeWindow window;

        [SetUp]
        public void SetUp()
        {
            KvWindowBelt.Clear();
            window = new FakeWindow();
        }

        [TearDown]
        public void TearDown()
        {
            KvWindowBelt.Clear();
        }

        private void RegisterDefault()
        {
            KvWindowBelt.Register("panel.test", window,
                delegate { window.Shown++; },
                delegate { window.Hidden++; },
                delegate { window.Collapsed++; },
                "Тестовое окно");
        }

        // ============================================================ 1. РЕГИСТРАЦИЯ

        /// <summary>Окно регистрируется, находится по идентификатору и снимается с учёта.</summary>
        [Test]
        public void Register_Find_Unregister()
        {
            RegisterDefault();

            Assert.AreEqual(1, KvWindowBelt.Count, "окно не зарегистрировалось");
            Assert.IsNotNull(KvWindowBelt.Find("panel.test"));
            Assert.AreEqual("Тестовое окно", KvWindowBelt.Find("panel.test").Title);

            Assert.IsTrue(KvWindowBelt.Unregister("panel.test"));
            Assert.AreEqual(0, KvWindowBelt.Count, "окно осталось в реестре");
            Assert.IsNull(KvWindowBelt.Find("panel.test"));
        }

        /// <summary>Повторная регистрация обновляет окно, а не плодит записи (окна пересобираются).</summary>
        [Test]
        public void RegisterTwice_UpdatesInsteadOfDuplicating()
        {
            RegisterDefault();
            var second = new FakeWindow();
            KvWindowBelt.Register("panel.test", second, delegate { second.Shown++; },
                delegate { second.Hidden++; }, delegate { second.Collapsed++; });

            Assert.AreEqual(1, KvWindowBelt.Count, "появилась вторая запись того же окна");
            Assert.AreSame(second, KvWindowBelt.Find("panel.test").Window, "действия не обновились");
        }

        /// <summary>Пустой идентификатор не регистрируется и не падает.</summary>
        [Test]
        public void Register_WithEmptyId_IsRejected()
        {
            Assert.IsFalse(KvWindowBelt.Register("", window, null, null, null));
            Assert.IsFalse(KvWindowBelt.Register(null, window, null, null, null));
            Assert.AreEqual(0, KvWindowBelt.Count);
        }

        /// <summary>Незнакомое окно считается развёрнутым (никаких исключений на «чужой» id).</summary>
        [Test]
        public void StateOf_UnknownWindow_IsExpanded()
        {
            Assert.AreEqual(KvWindowState.Expanded, KvWindowBelt.StateOf("нет.такого"));
            Assert.IsNull(KvWindowBelt.Find("нет.такого"));
        }

        // ============================================================ 2. ЗАКРЫТИЕ → НА ПОЯС ([D22])

        /// <summary>Закрытие скрывает окно И кладёт его на пояс — окно не теряется.</summary>
        [Test]
        public void SendToBelt_HidesWindowAndPutsItOnBelt()
        {
            RegisterDefault();

            Assert.IsTrue(KvWindowBelt.SendToBelt("panel.test"), "закрытие не сработало");
            Assert.AreEqual(1, window.Hidden, "окно не скрылось");
            Assert.AreEqual(KvWindowState.OnBelt, KvWindowBelt.StateOf("panel.test"), "состояние не «на поясе»");
            Assert.IsTrue(KvWindowBelt.Belt.Contains("panel.test"), "плитки на поясе нет");
            Assert.AreEqual(1, KvWindowBelt.OnBelt().Length);
        }

        /// <summary>Достать окно с пояса — значит показать его РАЗВЁРНУТЫМ и снять плитку.</summary>
        [Test]
        public void TakeFromBelt_ShowsWindowAndClearsTile()
        {
            RegisterDefault();
            KvWindowBelt.SendToBelt("panel.test");

            Assert.IsTrue(KvWindowBelt.TakeFromBelt("panel.test"));
            Assert.AreEqual(1, window.Shown, "окно не показалось");
            Assert.AreEqual(KvWindowState.Expanded, KvWindowBelt.StateOf("panel.test"));
            Assert.IsFalse(KvWindowBelt.Belt.Contains("panel.test"), "плитка осталась на поясе");
        }

        /// <summary>Повторное закрытие одного окна не удваивает плитку на поясе.</summary>
        [Test]
        public void SendToBelt_Twice_DoesNotDuplicateTile()
        {
            RegisterDefault();
            KvWindowBelt.SendToBelt("panel.test");
            KvWindowBelt.SendToBelt("panel.test");

            Assert.AreEqual(1, KvWindowBelt.Belt.Count, "плитка размножилась");
        }

        /// <summary>
        /// ГЛАВНОЕ ПРАВИЛО: при полном поясе окно НЕ скрывается. Иначе оно исчезло бы с экрана,
        /// не попав на пояс, и оператор не смог бы его вернуть.
        /// </summary>
        [Test]
        public void SendToBelt_WhenBeltIsFull_KeepsWindowVisible()
        {
            // Занимаем все места пояса «чужими» окнами.
            for (int i = 0; i < KvWindowBelt.Belt.Capacity; i++)
                KvWindowBelt.Belt.Put("занято." + i);

            RegisterDefault();

            Assert.IsFalse(KvWindowBelt.SendToBelt("panel.test"), "при полном поясе закрытие обязано отказать");
            Assert.AreEqual(0, window.Hidden, "окно всё-таки скрылось — его потеряли бы");
            Assert.AreNotEqual(KvWindowState.OnBelt, KvWindowBelt.StateOf("panel.test"),
                "окно записали на пояс, где нет места");
        }

        /// <summary>Закрытие незарегистрированного окна — просто «нечего делать», без исключений.</summary>
        [Test]
        public void SendToBelt_UnknownWindow_ReturnsFalse()
        {
            Assert.IsFalse(KvWindowBelt.SendToBelt("нет.такого"));
            Assert.IsFalse(KvWindowBelt.TakeFromBelt("нет.такого"));
        }

        /// <summary>Снятие окна с учёта убирает и его плитку с пояса.</summary>
        [Test]
        public void Unregister_RemovesTileFromBelt()
        {
            RegisterDefault();
            KvWindowBelt.SendToBelt("panel.test");
            Assert.IsTrue(KvWindowBelt.Belt.Contains("panel.test"));

            KvWindowBelt.Unregister("panel.test");
            Assert.IsFalse(KvWindowBelt.Belt.Contains("panel.test"), "плитка осталась после снятия с учёта");
        }

        // ============================================================ 3. СВОРАЧИВАНИЕ ([D21])

        /// <summary>Свёрнутое окно остаётся на панели — это «сложить», а не «убрать».</summary>
        [Test]
        public void CollapsedWindow_StaysOnPanel()
        {
            RegisterDefault();
            KvWindowBelt.NotifyCollapsed("panel.test", true);

            Assert.AreEqual(KvWindowState.Collapsed, KvWindowBelt.StateOf("panel.test"));
            Assert.IsTrue(KvWindowStates.IsVisibleOnPanel(KvWindowBelt.StateOf("panel.test")));
            Assert.IsFalse(KvWindowBelt.Belt.Contains("panel.test"), "свёрнутое окно не должно уходить на пояс");
        }

        /// <summary>Разворачивание возвращает окно в развёрнутое состояние.</summary>
        [Test]
        public void NotifyCollapsed_False_ExpandsWindow()
        {
            RegisterDefault();
            KvWindowBelt.NotifyCollapsed("panel.test", true);
            KvWindowBelt.NotifyCollapsed("panel.test", false);

            Assert.AreEqual(KvWindowState.Expanded, KvWindowBelt.StateOf("panel.test"));
        }

        /// <summary>Показ окна «снаружи» (тулбар, клавиша) снимает его с пояса.</summary>
        [Test]
        public void NotifyShown_TakesWindowOffBelt()
        {
            RegisterDefault();
            KvWindowBelt.SendToBelt("panel.test");
            KvWindowBelt.NotifyShown("panel.test");

            Assert.AreEqual(KvWindowState.Expanded, KvWindowBelt.StateOf("panel.test"));
            Assert.IsFalse(KvWindowBelt.Belt.Contains("panel.test"));
        }

        // ============================================================ 4. ОТЧЁТЫ И ОЧИСТКА

        /// <summary>Описание для журнала называет и число окон, и состояние пояса.</summary>
        [Test]
        public void Describe_NamesWindowsAndBelt()
        {
            Assert.IsTrue(KvWindowBelt.Describe().Contains("окон зарегистрировано"),
                "в описании нет числа окон: " + KvWindowBelt.Describe());

            RegisterDefault();
            KvWindowBelt.SendToBelt("panel.test");
            string text = KvWindowBelt.Describe();
            Assert.IsTrue(text.Contains("на поясе"), "в описании нет состояния пояса: " + text);
        }

        /// <summary>Очистка снимает и окна, и пояс (перезагрузка сцены).</summary>
        [Test]
        public void Clear_ResetsRegistryAndBelt()
        {
            RegisterDefault();
            KvWindowBelt.SendToBelt("panel.test");

            KvWindowBelt.Clear();

            Assert.AreEqual(0, KvWindowBelt.Count);
            Assert.AreEqual(0, KvWindowBelt.Belt.Count);
            Assert.IsNull(KvWindowBelt.Find("panel.test"));
        }
    }
}
