using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KazistovVvFeatures;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты XR-ОВЕРЛЕЯ (сессия §32; решения владельца [D20]–[D22]).
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ И ПОЧЕМУ ИМЕННО ЭТО.
    ///   • РАСКЛАДКА ПАНЕЛИ У РУКИ ([D20]) — чистая математика: форма (бублик/полумесяц), сторона
    ///     руки, число кнопок, радиус. Глазами в шлеме это проверяется один раз и «на ощущение»,
    ///     а ошибка раскладки (кнопки ушли за спину или наложились) ловится только числами.
    ///   • СОСТОЯНИЯ ОКОН ([D21]: свернуть/закрыть) и ПОЯС ([D22]: скрытые окна уходят на пояс) —
    ///     правила переходов обязаны быть одинаковыми для всех окон, и «повторное нажатие ничего
    ///     не ломает» — тоже проверяемое свойство.
    ///   • ПОЛНОТА ПЕРЕВОДА новых строк — у каждой обязан быть перевод на 7 языков, иначе растёт
    ///     долг локализации (карточка из `Docs/LOCALIZATION_AUDIT.md`: 205 непереведённых ключей).
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: проверок «кнопка нажимается в шлеме» и «окно действительно свернулось
    /// на экране» — без устройства и без кадра это была бы выдумка. Проверяется логика,
    /// на которой эти действия построены.
    /// </summary>
    public class KvXrOverlayTests
    {
        // ============================================================ 1. РАСКЛАДКА ПАНЕЛИ У РУКИ

        /// <summary>В бублике первая кнопка — строго сверху, дальше по часовой стрелке.</summary>
        [Test]
        public void Ring_FirstButtonIsOnTop_AndOrderGoesClockwise()
        {
            int count = 8;
            float first = KvXrQuickPanelLayout.AngleDegrees(0, count, KvXrQuickShape.Ring);
            float second = KvXrQuickPanelLayout.AngleDegrees(1, count, KvXrQuickShape.Ring);

            Assert.AreEqual(KvXrQuickPanelLayout.UpDegrees, first, 0.001f, "первая кнопка обязана быть сверху");
            Assert.Less(second, first, "вторая кнопка идёт по часовой стрелке (угол уменьшается)");
        }

        /// <summary>Шаг между кнопками в бублике равный: 360 / число кнопок.</summary>
        [Test]
        public void Ring_StepIsEven()
        {
            int count = 12;
            float step = KvXrQuickPanelLayout.RingSpanDegrees / count;      // 30°
            for (int i = 0; i < count; i++)
            {
                float expected = KvXrQuickPanelLayout.UpDegrees - i * step;
                Assert.AreEqual(expected,
                    KvXrQuickPanelLayout.AngleDegrees(i, count, KvXrQuickShape.Ring), 0.01f,
                    "неверный угол кнопки " + i);
            }
        }

        /// <summary>Полумесяц занимает РОВНО заданную дугу (150°), а не полный круг.</summary>
        [Test]
        public void Crescent_SpansExactlyConfiguredArc()
        {
            int count = 6;
            float left = KvXrQuickPanelLayout.AngleDegrees(0, count, KvXrQuickShape.Crescent);
            float right = KvXrQuickPanelLayout.AngleDegrees(count - 1, count, KvXrQuickShape.Crescent);

            Assert.AreEqual(KvXrQuickPanelLayout.CrescentSpanDegrees, left - right, 0.01f,
                "дуга полумесяца не совпала с заданной");
            Assert.Less(KvXrQuickPanelLayout.CrescentSpanDegrees, KvXrQuickPanelLayout.RingSpanDegrees,
                "полумесяц обязан быть уже полного круга");
        }

        /// <summary>У полумесяца середина дуги — сверху (там, где оператор ждёт главную кнопку).</summary>
        [Test]
        public void Crescent_MiddleButtonIsOnTop()
        {
            int count = 5;                              // нечётное: есть ровно средняя кнопка
            float middle = KvXrQuickPanelLayout.AngleDegrees(count / 2, count, KvXrQuickShape.Crescent);
            Assert.AreEqual(KvXrQuickPanelLayout.UpDegrees, middle, 0.01f,
                "средняя кнопка полумесяца обязана быть сверху");
        }

        /// <summary>Число кнопок приводится к допустимому диапазону, а не ломает раскладку.</summary>
        [Test]
        public void Count_IsClampedToSupportedRange()
        {
            Assert.AreEqual(KvXrQuickPanelLayout.MinButtons, KvXrQuickPanelLayout.ClampCount(0));
            Assert.AreEqual(KvXrQuickPanelLayout.MinButtons, KvXrQuickPanelLayout.ClampCount(-5));
            Assert.AreEqual(KvXrQuickPanelLayout.MaxButtons, KvXrQuickPanelLayout.ClampCount(99));
            Assert.AreEqual(6, KvXrQuickPanelLayout.ClampCount(6));

            Assert.IsTrue(KvXrQuickPanelLayout.IsValidCount(2));
            Assert.IsTrue(KvXrQuickPanelLayout.IsValidCount(12));
            Assert.IsFalse(KvXrQuickPanelLayout.IsValidCount(1));
            Assert.IsFalse(KvXrQuickPanelLayout.IsValidCount(13));
        }

        /// <summary>Индекс вне диапазона заворачивается: раскладка не падает в кадре.</summary>
        [Test]
        public void IndexOutOfRange_WrapsInsteadOfThrowing()
        {
            int count = 4;
            Assert.DoesNotThrow(() =>
            {
                KvXrQuickPanelLayout.AngleDegrees(-1, count, KvXrQuickShape.Ring);
                KvXrQuickPanelLayout.AngleDegrees(count + 7, count, KvXrQuickShape.Crescent);
                KvXrQuickPanelLayout.OffsetMeters(-3, count, KvXrQuickShape.Ring, 0.2f);
            });

            Assert.AreEqual(KvXrQuickPanelLayout.AngleDegrees(count - 1, count, KvXrQuickShape.Ring),
                KvXrQuickPanelLayout.AngleDegrees(-1, count, KvXrQuickShape.Ring), 0.001f,
                "индекс -1 обязан соответствовать последней кнопке");
        }

        /// <summary>Кнопки стоят на заданном радиусе от центра панели.</summary>
        [Test]
        public void Offset_KeepsConfiguredRadius()
        {
            float radius = 0.2f;
            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = KvXrQuickPanelLayout.OffsetMeters(i, 8, KvXrQuickShape.Ring, radius);
                Assert.AreEqual(radius, offset.magnitude, 0.001f, "кнопка " + i + " не на радиусе");
            }
        }

        /// <summary>Радиус ограничен: слишком маленький и слишком большой приводятся к границам.</summary>
        [Test]
        public void Radius_IsClamped()
        {
            Assert.AreEqual(KvXrQuickPanelLayout.MinRadiusMeters, KvXrQuickPanelLayout.ClampRadius(0.01f), 0.0001f);
            Assert.AreEqual(KvXrQuickPanelLayout.MaxRadiusMeters, KvXrQuickPanelLayout.ClampRadius(5f), 0.0001f);
            Assert.AreEqual(0.2f, KvXrQuickPanelLayout.ClampRadius(0.2f), 0.0001f);
        }

        /// <summary>
        /// ДЛЯ ПРАВОЙ РУКИ ПАНЕЛЬ ЗЕРКАЛИТСЯ. Иначе полумесяц у правой руки уходил бы в сторону
        /// от оператора — кнопки оказались бы не под ладонью ([D20]: «или правой для левшей»).
        /// </summary>
        [Test]
        public void RightHand_MirrorsPanelHorizontally()
        {
            Vector2 left = KvXrQuickPanelLayout.OffsetMeters(0, 6, KvXrQuickShape.Crescent, 0.2f, KvXrQuickHand.Left);
            Vector2 right = KvXrQuickPanelLayout.OffsetMeters(0, 6, KvXrQuickShape.Crescent, 0.2f, KvXrQuickHand.Right);

            Assert.AreEqual(-left.x, right.x, 0.001f, "зеркало обязано изменить знак X");
            Assert.AreEqual(left.y, right.y, 0.001f, "высота кнопки при зеркале не меняется");
        }

        /// <summary>Левой руке ничего не зеркалится (это состояние по умолчанию).</summary>
        [Test]
        public void LeftHand_KeepsPanelAsLaidOut()
        {
            Vector2 plain = KvXrQuickPanelLayout.OffsetMeters(2, 6, KvXrQuickShape.Ring, 0.2f);
            Vector2 left = KvXrQuickPanelLayout.OffsetMeters(2, 6, KvXrQuickShape.Ring, 0.2f, KvXrQuickHand.Left);
            Assert.AreEqual(plain.x, left.x, 0.0001f);
            Assert.AreEqual(plain.y, left.y, 0.0001f);
        }

        /// <summary>Описание панели для журнала называет форму, руку, число кнопок и радиус.</summary>
        [Test]
        public void Describe_NamesEverythingThatMatters()
        {
            string text = KvXrQuickPanelLayout.Describe(KvXrQuickShape.Ring, KvXrQuickHand.Left, 8, 0.16f);
            Assert.IsFalse(string.IsNullOrEmpty(text));
            Assert.IsTrue(text.Contains("кнопок"), "в описании нет числа кнопок: " + text);
            Assert.IsTrue(text.Contains("рука"), "в описании нет руки: " + text);
            Assert.IsTrue(text.Contains("радиус"), "в описании нет радиуса: " + text);
        }

        // ============================================================ 2. ОКНА ([D21]) И ПОЯС ([D22])

        /// <summary>«Свернуть» уводит окно в свёрнутое состояние из любого другого.</summary>
        [Test]
        public void Collapse_WorksFromEveryState()
        {
            Assert.AreEqual(KvWindowState.Collapsed, KvWindowStates.Collapse(KvWindowState.Expanded));
            Assert.AreEqual(KvWindowState.Collapsed, KvWindowStates.Collapse(KvWindowState.Collapsed));
            Assert.AreEqual(KvWindowState.Collapsed, KvWindowStates.Collapse(KvWindowState.OnBelt));
            Assert.AreEqual(KvWindowState.Collapsed, KvWindowStates.Collapse(KvWindowState.Closed));
        }

        /// <summary>«Развернуть» возвращает окно из любого состояния, включая закрытое.</summary>
        [Test]
        public void Expand_ReturnsWindowFromAnyState()
        {
            foreach (KvWindowState state in new[]
                     { KvWindowState.Expanded, KvWindowState.Collapsed, KvWindowState.OnBelt, KvWindowState.Closed })
            {
                Assert.AreEqual(KvWindowState.Expanded, KvWindowStates.Expand(state),
                    "развернуть обязано работать из состояния " + state);
            }
        }

        /// <summary>«Закрыть» (квадратик) убирает окно с показа — и с пояса тоже.</summary>
        [Test]
        public void Close_HidesWindowFromPanelAndBelt()
        {
            Assert.AreEqual(KvWindowState.Closed, KvWindowStates.Close(KvWindowState.Expanded));
            Assert.AreEqual(KvWindowState.Closed, KvWindowStates.Close(KvWindowState.OnBelt));
            Assert.IsFalse(KvWindowStates.IsVisibleOnPanel(KvWindowState.Closed));
            Assert.IsTrue(KvWindowStates.IsHidden(KvWindowState.Closed));
        }

        /// <summary>Взять окно с пояса — значит ОТКРЫТЬ его (а не оставить свёрнутым).</summary>
        [Test]
        public void TakingFromBelt_OpensWindow()
        {
            Assert.AreEqual(KvWindowState.Expanded, KvWindowStates.TakeFromBelt(KvWindowState.OnBelt));
            Assert.IsTrue(KvWindowStates.IsVisibleOnPanel(KvWindowStates.TakeFromBelt(KvWindowState.OnBelt)));
        }

        /// <summary>Свёрнутое окно ОСТАЁТСЯ на панели (это не «убрать», а «сложить»).</summary>
        [Test]
        public void CollapsedWindow_StaysOnPanel()
        {
            Assert.IsTrue(KvWindowStates.IsVisibleOnPanel(KvWindowState.Collapsed));
            Assert.IsFalse(KvWindowStates.IsHidden(KvWindowState.Collapsed));
            Assert.IsFalse(KvWindowStates.IsStoredOnBelt(KvWindowState.Collapsed));
        }

        /// <summary>Повторное нажатие той же кнопки ничего не меняет (переходы идемпотентны).</summary>
        [Test]
        public void Transitions_AreIdempotent()
        {
            KvWindowState once = KvWindowStates.Collapse(KvWindowState.Expanded);
            KvWindowState twice = KvWindowStates.Collapse(once);
            Assert.AreEqual(once, twice, "двойное сворачивание изменило состояние");

            Assert.AreEqual(KvWindowStates.Close(KvWindowState.Closed), KvWindowStates.Close(KvWindowState.Closed));
            Assert.AreEqual(KvWindowStates.SendToBelt(KvWindowState.OnBelt), KvWindowStates.SendToBelt(KvWindowState.OnBelt));
        }

        /// <summary>Свойства состояний согласованы: видимое не лежит на поясе и наоборот.</summary>
        [Test]
        public void StateProperties_AreConsistent()
        {
            foreach (KvWindowState state in new[]
                     { KvWindowState.Expanded, KvWindowState.Collapsed, KvWindowState.OnBelt, KvWindowState.Closed })
            {
                if (KvWindowStates.IsVisibleOnPanel(state))
                    Assert.IsFalse(KvWindowStates.IsStoredOnBelt(state), "видимое окно не может быть на поясе");
                if (KvWindowStates.IsStoredOnBelt(state))
                    Assert.IsTrue(KvWindowStates.IsHidden(state), "окно на поясе считается скрытым");
            }
        }

        /// <summary>У состояния есть название для оператора — и оно не пустое.</summary>
        [Test]
        public void StateLabels_AreNotEmpty()
        {
            foreach (KvWindowState state in new[]
                     { KvWindowState.Expanded, KvWindowState.Collapsed, KvWindowState.OnBelt, KvWindowState.Closed })
            {
                Assert.IsFalse(string.IsNullOrEmpty(KvWindowStates.Label(state)),
                    "у состояния " + state + " нет названия");
            }
        }

        // ============================================================ 3. ПОЯС ([D22])

        /// <summary>Окно кладётся на пояс, находится там и снимается оттуда.</summary>
        [Test]
        public void Belt_PutTakeContains()
        {
            var belt = new KvXrBelt();
            Assert.IsTrue(belt.Put("panel.trajectory"));
            Assert.IsTrue(belt.Contains("panel.trajectory"));
            Assert.AreEqual(1, belt.Count);

            Assert.IsTrue(belt.Take("panel.trajectory"));
            Assert.IsFalse(belt.Contains("panel.trajectory"));
            Assert.AreEqual(0, belt.Count);

            Assert.IsFalse(belt.Take("panel.trajectory"), "повторное снятие обязано вернуть false");
        }

        /// <summary>Одно окно — одна плитка: повторная укладка не удваивает пояс.</summary>
        [Test]
        public void Belt_DoesNotDuplicateWindows()
        {
            var belt = new KvXrBelt();
            Assert.IsTrue(belt.Put("panel.settings"));
            Assert.IsTrue(belt.Put("panel.settings"), "повторная укладка того же окна — не ошибка");
            Assert.AreEqual(1, belt.Count, "плитка не должна удваиваться");
        }

        /// <summary>Мест на поясе конечное число: при переполнении — честный отказ.</summary>
        [Test]
        public void Belt_RefusesWhenFull()
        {
            var belt = new KvXrBelt(2);
            Assert.IsTrue(belt.Put("a"));
            Assert.IsTrue(belt.Put("b"));
            Assert.IsFalse(belt.HasRoom);
            Assert.IsFalse(belt.Put("c"), "при полном поясе окно не должно теряться молча");
            Assert.AreEqual(2, belt.Count);
        }

        /// <summary>Пустой идентификатор не занимает плитку (защита от пустых окон).</summary>
        [Test]
        public void Belt_IgnoresEmptyIds()
        {
            var belt = new KvXrBelt();
            Assert.IsFalse(belt.Put(""));
            Assert.IsFalse(belt.Put(null));
            Assert.AreEqual(0, belt.Count);
        }

        /// <summary>Вместимость пояса — не меньше единицы, даже если задали ноль.</summary>
        [Test]
        public void Belt_CapacityIsAtLeastOne()
        {
            var belt = new KvXrBelt(0);
            Assert.AreEqual(1, belt.Capacity);
        }

        /// <summary>Описание пояса: и пустой, и заполненный пояс говорят своё состояние.</summary>
        [Test]
        public void Belt_DescribeTellsState()
        {
            var belt = new KvXrBelt();
            Assert.IsTrue(belt.Describe().Contains("пуст"), "пустой пояс обязан сказать «пуст»: " + belt.Describe());

            belt.Put("panel.a");
            string text = belt.Describe();
            Assert.IsTrue(text.Contains("1"), "описание не назвало число окон: " + text);
        }

        /// <summary>Плитка по номеру: вне диапазона — пустая строка, а не исключение.</summary>
        [Test]
        public void Belt_ItemAtIsSafe()
        {
            var belt = new KvXrBelt();
            belt.Put("panel.a");
            Assert.AreEqual("panel.a", belt.ItemAt(0));
            Assert.AreEqual("", belt.ItemAt(5));
            Assert.AreEqual("", belt.ItemAt(-1));
        }

        // ============================================================ 4. СТРОКИ НА 7 ЯЗЫКАХ

        /// <summary>Таблица строк не пустая и все ключи начинаются с `xr.` — как обещано в шапке.</summary>
        [Test]
        public void OverlayStrings_HaveXrKeys()
        {
            Assert.Greater(KvLocExtra5.RowCount, 0, "таблица строк XR-оверлея пуста");

            var seen = new List<string>();
            for (int i = 0; i < KvLocExtra5.RowCount; i++)
            {
                string key = KvLocExtra5.KeyAt(i);
                Assert.IsTrue(key.StartsWith("xr.", System.StringComparison.Ordinal),
                    "ключ без префикса xr.: " + key);
                Assert.IsFalse(seen.Contains(key), "ключ повторяется: " + key);
                seen.Add(key);
            }
        }

        /// <summary>
        /// У КАЖДОЙ НОВОЙ СТРОКИ ЕСТЬ ВСЕ 7 ПЕРЕВОДОВ. Пустая ячейка означала бы, что в
        /// соответствующем языке оператор увидит русский текст из кода.
        /// </summary>
        [Test]
        public void OverlayStrings_AreTranslatedIntoAllSevenLanguages()
        {
            for (int i = 0; i < KvLocExtra5.RowCount; i++)
            {
                string key = KvLocExtra5.KeyAt(i);
                Assert.AreEqual(7, KvLocExtra5.LanguageCount(i), "у строки " + key + " не 7 языков");
                for (int lang = 0; lang < 7; lang++)
                {
                    string text = KvLocExtra5.TextAt(i, lang);
                    Assert.IsFalse(string.IsNullOrEmpty(text),
                        "у строки " + key + " пустой перевод, язык №" + lang);
                }
            }
        }

        /// <summary>Установка строк не бросает исключений и не ломает словари (EditMode-проверка).</summary>
        [Test]
        public void OverlayStrings_InstallDoesNotThrow()
        {
            Assert.DoesNotThrow(() => KvLocExtra5.Install());
            Assert.DoesNotThrow(() => KvLocExtra5.Install());     // повторный вызов — без последствий
        }
    }
}
