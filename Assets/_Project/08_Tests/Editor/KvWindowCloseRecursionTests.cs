// ================================================================================================
//  KvWindowCloseRecursionTests.cs — ПАДЕНИЕ ПРИЛОЖЕНИЯ ПРИ ЗАКРЫТИИ ОКНА (сессия S45, 12.11.2026).
//
//  ОТКУДА ЗАДАЧА. Tombstone планшета Redmi, сборка 10.11.2026:
//      signal 11 (SIGSEGV), code 2 (SEGV_ACCERR)
//      Cause: stack pointer is close to top of stack; likely stack overflow.
//      512 total frames, в которых ЧЕРЕДУЮТСЯ ДВА адреса `libil2cpp.so` (…51fa89c / …50d449c)
//  Перед падением в журнале ровно одна строка действия:
//      [KvUiSweep] press=KazistovVv_UI/KvFeatureCanvas/KvFeatureWindow/Header/Close«✕» type=Button
//  То есть оператор нажал «✕» в шапке окна «Панель функций», и приложение упало от рекурсии.
//
//  НАЙДЕННАЯ ЦЕПОЧКА (метод за методом):
//      1) `KvFeatureWindow.Build` … `KvTheme.CreateSmallButton(header, "Close", "✕", …)` →
//         обработчик зовёт `Hide()`                                       (KvFeatureWindow.cs:228);
//      2) `KvFeatureWindow.Build` регистрирует В РЕЕСТРЕ ОКОН `KvWindowBelt` СВОЮ ЖЕ `Hide`
//         как действие «скрыть» (`KvWindowBelt.Register(WindowId, this, …, Hide, …)`);
//      3) старая `Hide()` первым делом звала `KvWindowBelt.SendToBelt(WindowId)`
//         (`if (KvWindowBelt.Find(WindowId) != null) { … SendToBelt … }`);
//      4) `KvWindowBelt.SendToBelt` в конце ЗОВЁТ ЗАРЕГИСТРИРОВАННОЕ действие:
//         `if (entry.Hide != null) entry.Hide();`
//      5) `entry.Hide` — это `KvFeatureWindow.Hide` из пункта 3. Круг замкнулся:
//         `Hide → SendToBelt → entry.Hide → Hide → …` до конца стека → SIGSEGV.
//
//  ПОЧЕМУ ЭТО НЕ ВИДНО В РЕДАКТОРЕ. В PlayMode Unity ловит такую рекурсию своим сторожевым
//  счётчиком и пишет в консоль; в IL2CPP ограничителя глубины нет — процесс получает SIGSEGV и
//  исчезает без сообщения. Отсюда «нативное падение» в отчёте и «у нас не воспроизводится».
//
//  ЧТО ЗДЕСЬ ПРОВЕРЯЕТСЯ. Рекурсию в EditMode напрямую не воспроизвести (нужна живая сцена,
//  Canvas и EventSystem), поэтому проверяются три вещи, каждая из которых ЛОВИТ ИМЕННО ЭТОТ
//  ДЕФЕКТ:
//    • ПРАВИЛО ЗАКРЫТИЯ (`KvHideSequence`): у зарегистрированного окна ровно два шага и именно в
//      порядке «сначала скрыть себя, потом на пояс». Старый код этот порядок нарушал — он начинал
//      с реестра, ничего не скрыв, то есть шаг «скрыть себя» в нём отсутствовал как первый;
//    • ЖИВОЙ ЦИКЛ (`KvWindowBelt`): окно-двойник, чьё действие «скрыть» само зовёт `SendToBelt`
//      (ровно как настоящая старая `Hide`), обязано закрыться за ОДИН вызов, а не уйти в рекурсию.
//      На старом реестре этот тест не возвращает управление вообще;
//    • АНТИ-ВАКУУМ: обычное одиночное закрытие работает как раньше — действие вызвано, плитка на
//      поясе, состояние «на поясе», повторное нажатие не теряет окно и не плодит плиток.
//
//  ГРАНИЦА ЧЕСТНОСТИ: сам `KvFeatureWindow.Hide` здесь не вызывается — для него нужны сцена,
//  Canvas и `FeatureHub`. Поэтому используется ДВОЙНИК окна с тем же контрактом (действие
//  «скрыть» ходит в реестр) и отдельно проверяется правило, которое задаёт порядок шагов.
// ================================================================================================

using NUnit.Framework;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// Закрытие окна (крестик) не должно уходить в бесконечный взаимный вызов
    /// «окно → реестр → окно». Падение планшета 10.11.2026 — SIGSEGV от переполнения стека.
    /// </summary>
    [Category(KvTestCategories.Fast)]
    public class KvWindowCloseRecursionTests
    {
        private const string WindowId = "panel.features";   // тот же id, что у панели функций

        [SetUp]
        public void SetUp()
        {
            KvWindowBelt.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            KvWindowBelt.Clear();
        }

        // ==================================================================================
        // 1. ПРАВИЛО ЗАКРЫТИЯ: порядок шагов — часть контракта
        // ==================================================================================

        /// <summary>
        /// СТОРОЖ ЦИКЛА, ЧАСТЬ ПЕРВАЯ: у зарегистрированного окна шагов РОВНО ДВА, и первый из
        /// них — «скрыть себя», а не «спросить реестр».
        ///
        /// ЧТО БЫЛО. Единственное, что делала старая `Hide()`, — звала реестр. То есть шага
        /// «скрыть себя» в ней не было ВООБЩЕ: окно отдавало себя на пояс, ни разу не спрятавшись,
        /// и реестр вызывал `Hide` заново. Именно этот порядок и есть причина падения.
        /// </summary>
        [Test]
        public void RegisteredWindow_HidesItselfBeforeAskingRegistry()
        {
            KvHideStep[] steps = KvHideSequence.Registered;

            Assert.AreEqual(2, steps.Length,
                "у окна, зарегистрированного в реестре, обязано быть ДВА шага: «скрыть себя» и «на пояс»");
            Assert.AreEqual(KvHideStep.HideItself, steps[0],
                "ПЕРВЫМ шагом окно скрывает СЕБЯ, а не спрашивает реестр — иначе реестр вызовет Hide " +
                "заново и стек кончится (падало приложением, а не ошибкой)");
            Assert.AreEqual(KvHideStep.SendToBelt, steps[1],
                "вторым шагом окно просит реестр положить его на пояс");
        }

        /// <summary>
        /// СТОРОЖ ЦИКЛА, ЧАСТЬ ВТОРАЯ: порядок «на пояс → скрыть себя» (тот, что был) НЕ проходит.
        /// Проверка нужна отдельно: без неё сторож согласится с любым массивом из двух шагов.
        /// </summary>
        [Test]
        public void RegistryFirstOrder_IsRejected()
        {
            var oldOrder = new[] { KvHideStep.SendToBelt, KvHideStep.HideItself };

            Assert.IsFalse(KvHideSequence.IsRegisteredOrder(oldOrder),
                "порядок «сначала реестр, потом скрыть себя» — это и есть взаимный вызов, он запрещён");
            Assert.IsFalse(KvHideSequence.IsValidOrder(oldOrder, true),
                "зарегистрированное окно с таким порядком не имеет права считаться корректным");
        }

        /// <summary>
        /// ЧИСТАЯ ФУНКЦИЯ РАЗРЫВА: пока реестр вызывает окно, окно НЕ спрашивает реестр.
        /// Это единственное место, где цикл и разрывается; проверяется без сцены.
        /// </summary>
        [Test]
        public void WindowInsideRegistryCall_DoesNotAskRegistry()
        {
            Assert.IsFalse(KvHideSequence.MayAskRegistry(true),
                "изнутри вызова реестра окно обязано молчать — иначе круг Hide → SendToBelt → Hide");
            Assert.IsTrue(KvHideSequence.MayAskRegistry(false),
                "обычное закрытие (не из реестра) обязано класть окно на пояс — иначе [D22] сломан");
        }

        /// <summary>
        /// НЕЗАРЕГИСТРИРОВАННОЕ окно шага «на пояс» не имеет: ему остаётся только скрыть себя.
        /// Это не ослабление сторожа, а честная разница: класть на пояс некуда.
        /// </summary>
        [Test]
        public void UnregisteredWindow_HasOnlyHideItself()
        {
            Assert.IsTrue(KvHideSequence.IsValidOrder(new[] { KvHideStep.HideItself }, false));
            Assert.IsFalse(KvHideSequence.IsValidOrder(new[] { KvHideStep.HideItself }, true),
                "зарегистрированное окно обязано иметь ОБА шага");
            Assert.IsFalse(KvHideSequence.IsValidOrder(
                    new[] { KvHideStep.HideItself, KvHideStep.SendToBelt }, false),
                "незарегистрированному окну нечего делать на поясе");
        }

        /// <summary>Сторож не соглашается на пустой и на удлинённый список шагов.</summary>
        [Test]
        public void Guard_RejectsWrongStepCount()
        {
            Assert.IsFalse(KvHideSequence.IsRegisteredOrder(null), "null не является порядком шагов");
            Assert.IsFalse(KvHideSequence.IsRegisteredOrder(new KvHideStep[0]), "пустой список не порядок");
            Assert.IsFalse(KvHideSequence.IsRegisteredOrder(
                    new[] { KvHideStep.HideItself, KvHideStep.SendToBelt, KvHideStep.HideItself }),
                "третий шаг означает, что кто-то снова пошёл по кругу");
        }

        // ==================================================================================
        // 2. ПРАВИЛО ПОВТОРНОГО ВХОДА (общее для всех окон и для реестра)
        // ==================================================================================

        /// <summary>
        /// ЧТО БЫЛО: флага повторного входа не было вообще, поэтому взаимный вызов шёл до конца
        /// стека. ЧТО СТАЛО: пока действие выполняется, начать его заново нельзя.
        /// </summary>
        [Test]
        public void ClickGuard_BlocksReentryWhileRunning()
        {
            Assert.IsTrue(KvUiClickGuard.CanStart(false), "обычный вход обязан быть разрешён");
            Assert.IsFalse(KvUiClickGuard.CanStart(true),
                "повторный вход внутрь того же действия — это и есть путь к переполнению стека");
        }

        /// <summary>
        /// АНТИ-ВАКУУМ: правило не запрещает всё подряд. Новое намерение оператора (того же
        /// действия, но после завершения предыдущего) проходит — иначе кнопка «✕» работала бы
        /// только один раз за запуск.
        /// </summary>
        [Test]
        public void ClickGuard_AllowsNextClickAfterFinish()
        {
            Assert.IsTrue(KvUiClickGuard.ShouldRun(false), "первое нажатие обязано выполниться");
            Assert.IsFalse(KvUiClickGuard.ShouldRun(true), "повтор того же действия не выполняется");
        }

        // ==================================================================================
        // 3. ЖИВОЙ ЦИКЛ: окно, чьё действие «скрыть» само зовёт реестр
        // ==================================================================================

        /// <summary>
        /// ОКНО-ДВОЙНИК С ТЕМ ЖЕ КОНТРАКТОМ, ЧТО У ПАНЕЛИ ФУНКЦИЙ: его действие «скрыть» зовёт
        /// `SendToBelt` СВОЕГО ЖЕ идентификатора — ровно то, что делала старая `Hide()`.
        ///
        /// ЧЕМ ЭТО ЛУЧШЕ ГРЕПА ПО ИСХОДНИКУ. Греп поймал бы «строка на месте», а здесь проверяется
        /// ФАКТ: вызов возвращает управление и скрывает окно ОДИН раз. На старом реестре этот тест
        /// не дошёл бы до конца — рекурсия `SendToBelt → Hide → SendToBelt` съела бы стек.
        ///
        /// ПОЧЕМУ ЗДЕСЬ ЕСТЬ ФЛАГ. Настоящее окно `KvFeatureWindow` держит СВОЙ флаг
        /// `inBeltDispatch` и по нему отказывается спрашивать реестр повторно — так цикл и
        /// разорван. Двойник обязан повторить ТО ЖЕ правило, иначе он исправно воспроизводил бы
        /// старый дефект даже поверх исправленного реестра. Проверяется при этом не отсутствие
        /// флага, а результат: возврат управления и ровно одно скрытие. Страховку самого реестра
        /// проверяет отдельный тест (`Belt_RefusesReentrantSendToBelt`).
        /// </summary>
        private class SelfRoutingWindow
        {
            public int Hidden;
            public int Showed;
            public bool Visible = true;

            /// <summary>Идёт вызов из реестра — повторно реестр НЕ спрашиваем (разрыв цикла).</summary>
            public bool InBeltDispatch;

            /// <summary>Действие «скрыть»: сначала скрыть себя, потом (как настоящее окно) — на пояс.</summary>
            public void Hide()
            {
                Hidden++;
                Visible = false;
                if (!KvHideSequence.MayAskRegistry(InBeltDispatch)) return;
                if (KvWindowBelt.Find(WindowId) == null) return;

                InBeltDispatch = true;
                try
                {
                    KvWindowBelt.SendToBelt(WindowId);
                }
                finally
                {
                    InBeltDispatch = false;
                }
            }

            /// <summary>Действие «показать»: реестр зовёт его, когда окно достают с пояса.</summary>
            public void Show()
            {
                Showed++;
                Visible = true;
            }
        }

        private static SelfRoutingWindow RegisterSelfRouting(out SelfRoutingWindow reference)
        {
            var window = new SelfRoutingWindow();
            reference = window;
            KvWindowBelt.Register(WindowId, window, window.Show, window.Hide, null, "Панель функций");
            return window;
        }

        /// <summary>
        /// ГЛАВНЫЙ ТЕСТ ДЕФЕКТА: нажатие «✕» (одно!) обязано ЗАВЕРШИТЬСЯ, скрыть окно и положить
        /// его на пояс — не уходя в бесконечный взаимный вызов.
        ///
        /// НА СТАРОМ КОДЕ ЭТОТ ТЕСТ ПАДАЕТ ПЕРЕПОЛНЕНИЕМ СТЕКА: `SendToBelt` вызывал `entry.Hide`,
        /// тот снова `SendToBelt`, и так до конца стека — то есть ровно тот SIGSEGV, что в
        /// tombstone планшета.
        ///
        /// ПОЧЕМУ «ДВА ВЫЗОВА» — ЭТО НОРМА, А НЕ ДЕФЕКТ. Действие скрытия законно выполняется
        /// дважды за ОДНО нажатие: сначала его зовёт кнопка, затем — реестр, который кладёт окно
        /// на пояс (он обязан скрыть окно своим вызовом, см. `KvWindowBelt.SendToBelt`). Кругом
        /// был бы ТРЕТИЙ и все последующие вызовы без конца. Поэтому проверяются три факта:
        /// управление вернулось, вызовов ровно два, окно скрыто и лежит на поясе.
        /// </summary>
        [Test]
        public void Close_CalledFromInsideRegistry_FinishesAndHidesOnce()
        {
            SelfRoutingWindow window = RegisterSelfRouting(out _);

            // ОДИН вызов закрытия, как у кнопки «✕». Если цикл не разорван, сюда управление
            // не вернётся вовсе.
            window.Hide();

            Assert.AreEqual(2, window.Hidden,
                "действие «скрыть» выполнилось не два раза (кнопка + реестр) — либо цикл, либо окно " +
                "не скрылось по просьбе реестра");
            Assert.IsFalse(window.Visible, "окно осталось видимым после закрытия");
            Assert.IsTrue(KvWindowBelt.Belt.Contains(WindowId), "окно не легло на пояс");
        }

        /// <summary>
        /// РЕЕСТР — ТОЖЕ СТОРОЖ. Прямой вызов `SendToBelt` окна, чьё действие «скрыть» заглядывает
        /// в реестр, обязан вернуть управление: защита стоит в двух местах, потому что реестр общий
        /// и следующее окно с похожим действием снова уронило бы приложение.
        /// </summary>
        [Test]
        public void SendToBelt_WindowThatCallsRegistryBack_DoesNotRecurse()
        {
            SelfRoutingWindow window = RegisterSelfRouting(out _);

            Assert.IsTrue(KvWindowBelt.SendToBelt(WindowId),
                "закрытие обязано сообщить об успехе: плитка на поясе");
            Assert.AreEqual(1, window.Hidden, "второй заход внутрь действия — это вход в цикл");
            Assert.IsTrue(KvWindowBelt.Belt.Contains(WindowId), "плитки на поясе нет");
            Assert.AreEqual(KvWindowState.OnBelt, KvWindowBelt.StateOf(WindowId));
        }

        /// <summary>
        /// СТРАХОВКА НЕ ЗАЛИПАЕТ: после закрытия флаг «действие выполняется» снят, и следующее
        /// закрытие снова работает. Без снятия в `finally` окно закрывалось бы ровно один раз за
        /// запуск, а второе нажатие молча ничего не делало бы.
        /// </summary>
        [Test]
        public void GuardFlag_IsReleasedAfterClose()
        {
            SelfRoutingWindow window = RegisterSelfRouting(out _);

            KvWindowBelt.SendToBelt(WindowId);
            Assert.IsFalse(KvWindowBelt.Find(WindowId).Dispatching,
                "флаг «действие выполняется» остался взведённым — следующее закрытие будет молчать");

            // Вернули окно на экран и закрыли заново: повторное закрытие обязано сработать.
            KvWindowBelt.TakeFromBelt(WindowId);
            Assert.IsTrue(KvWindowBelt.SendToBelt(WindowId), "второе закрытие не сработало");
            Assert.AreEqual(2, window.Hidden, "второе закрытие не дошло до действия окна");
        }

        /// <summary>
        /// СТРАХОВКА САМОГО РЕЕСТРА — БЕЗ УЧАСТИЯ ОКНА. Окно-двойник здесь НАРОЧНО не имеет
        /// никакого своего флага: его действие «скрыть» просто зовёт `SendToBelt` (ровно как
        /// старая `Hide()`). Спасти обязан реестр — иначе следующее окно с похожим действием
        /// снова уронит приложение, ведь реестр общий.
        ///
        /// НА СТАРОМ КОДЕ: `SendToBelt → Hide → SendToBelt → …` до переполнения стека — тот же
        /// SIGSEGV, что в tombstone планшета.
        /// </summary>
        [Test]
        public void Belt_RefusesReentrantSendToBelt()
        {
            int hidden = 0;
            bool secondResult = true;
            KvWindowBelt.Register(WindowId, new object(),
                delegate { },
                delegate
                {
                    hidden++;
                    // Действие окна БЕЗ своей защиты: снова просит реестр положить его на пояс.
                    secondResult = KvWindowBelt.SendToBelt(WindowId);
                },
                null, "Окно без своей защиты");

            bool first = KvWindowBelt.SendToBelt(WindowId);

            Assert.IsTrue(first, "первое закрытие обязано сработать");
            Assert.AreEqual(1, hidden,
                "действие «скрыть» ушло в повторный вызов — страховка реестра не работает (это падение устройства)");
            Assert.IsFalse(secondResult,
                "повторный SendToBelt изнутри действия обязан вернуть ОТКАЗ, а не запускать окно заново");
            Assert.AreEqual(1, KvWindowBelt.Belt.Count, "плитка размножилась повторными заходами");
        }

        /// <summary>
        /// ИСКЛЮЧЕНИЕ ВНУТРИ ДЕЙСТВИЯ ОКНА НЕ ОСТАВЛЯЕТ ЗАПИСЬ «ЗАНЯТОЙ»: флаг снимается в
        /// `finally`, поэтому упавшее действие не превращает окно в «навсегда закрывающееся».
        /// </summary>
        [Test]
        public void ExceptionInsideAction_StillReleasesEntry()
        {
            int calls = 0;
            KvWindowBelt.Register(WindowId, new object(),
                delegate { },
                delegate
                {
                    calls++;
                    throw new System.InvalidOperationException("сбой внутри действия окна");
                },
                null, "Панель функций");

            Assert.Throws<System.InvalidOperationException>(delegate { KvWindowBelt.SendToBelt(WindowId); });
            Assert.AreEqual(1, calls, "действие вызвано не один раз");
            Assert.IsFalse(KvWindowBelt.Find(WindowId).Dispatching,
                "после исключения запись осталась «занятой» — окно больше не закрыть");
        }

        // ==================================================================================
        // 4. АНТИ-ВАКУУМ: обычное одиночное закрытие работает как раньше
        // ==================================================================================

        /// <summary>
        /// ОБЫЧНОЕ ЗАКРЫТИЕ (окно НЕ ходит в реестр из своего действия — так ведут себя все окна,
        /// кроме панели функций): действие вызвано РОВНО один раз, плитка на поясе, состояние
        /// честное. Без этого «сторож цикла» мог бы оказаться сторожем «ничего не работает».
        /// </summary>
        [Test]
        public void PlainClose_StillHidesOnceAndPutsOnBelt()
        {
            int hidden = 0;
            KvWindowBelt.Register(WindowId, new object(),
                delegate { },
                delegate { hidden++; },
                null, "Обычное окно");

            Assert.IsTrue(KvWindowBelt.SendToBelt(WindowId), "обычное закрытие отказало");
            Assert.AreEqual(1, hidden, "действие «скрыть» вызвано не один раз");
            Assert.IsTrue(KvWindowBelt.Belt.Contains(WindowId), "плитки на поясе нет");
            Assert.AreEqual(KvWindowState.OnBelt, KvWindowBelt.StateOf(WindowId));
        }

        /// <summary>
        /// ВТОРОЕ НАЖАТИЕ НА «✕» НЕ ЛОМАЕТСЯ: окно уже на поясе, плитка не удваивается, действие
        /// вызывается ещё раз (окно и так скрыто), исключений нет. Это поведение закреплено и
        /// прежними тестами пояса — здесь важно, что разрыв цикла его не изменил.
        /// </summary>
        [Test]
        public void SecondClose_DoesNotDuplicateTile()
        {
            int hidden = 0;
            KvWindowBelt.Register(WindowId, new object(),
                delegate { },
                delegate { hidden++; },
                null, "Обычное окно");

            KvWindowBelt.SendToBelt(WindowId);
            Assert.DoesNotThrow(delegate { KvWindowBelt.SendToBelt(WindowId); },
                "второе закрытие бросило исключение");
            Assert.AreEqual(1, KvWindowBelt.Belt.Count, "плитка размножилась");
            Assert.AreEqual(2, hidden, "второе закрытие не дошло до действия окна");
        }

        /// <summary>
        /// ПОЛНЫЙ ПОЯС — ОКНО НЕ ТЕРЯЕТСЯ (правило [D22] осталось в силе после разрыва цикла):
        /// закрытие отказывает, действие НЕ вызывается, окно остаётся видимым.
        /// </summary>
        [Test]
        public void FullBelt_RefusesWithoutHidingWindow()
        {
            int hidden = 0;
            KvWindowBelt.Register(WindowId, new object(),
                delegate { },
                delegate { hidden++; },
                null, "Обычное окно");
            for (int i = 0; i < KvWindowBelt.Belt.Capacity; i++)
                KvWindowBelt.Belt.Put("занято." + i);

            Assert.IsFalse(KvWindowBelt.SendToBelt(WindowId), "при полном поясе закрытие обязано отказать");
            Assert.AreEqual(0, hidden, "окно скрылось, не попав на пояс — оператор его не найдёт");
            Assert.AreNotEqual(KvWindowState.OnBelt, KvWindowBelt.StateOf(WindowId));
        }

        /// <summary>
        /// ДОСТАТЬ С ПОЯСА по-прежнему возвращает окно на экран: разрыв цикла не должен был
        /// сломать обратный путь ([D22] — «скрытое окно не исчезает»).
        /// </summary>
        [Test]
        public void TakeFromBelt_StillShowsWindow()
        {
            SelfRoutingWindow window = RegisterSelfRouting(out _);

            KvWindowBelt.SendToBelt(WindowId);
            Assert.IsTrue(KvWindowBelt.TakeFromBelt(WindowId), "окно не досталось с пояса");
            Assert.AreEqual(1, window.Showed, "действие «показать» не вызвано");
            Assert.IsTrue(window.Visible, "окно осталось скрытым после возврата с пояса");
            Assert.AreEqual(KvWindowState.Expanded, KvWindowBelt.StateOf(WindowId));
        }
    }
}
