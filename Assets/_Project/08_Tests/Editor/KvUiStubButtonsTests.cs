// ================================================================================================
//  KvUiStubButtonsTests.cs — EditMode-тесты «КНОПОК-ЗАГЛУШЕК» В ИНТЕРФЕЙСЕ (12.11.2026).
//
//  ОТКУДА ЗАДАЧА. Самопрогон интерфейса на планшете нашёл в окне «Горячие клавиши» (F12) кнопку
//  «Переназначить» — она стояла в КАЖДОЙ из 56 строк. Обработчик только писал в статус
//  «Переназначение клавиш появится позже — бинды не меняются», подсказка была помечена
//  «в разработке» (`KvTooltipTarget.stub = true`). То есть 56 кнопок обещали функцию, которой нет:
//  в проекте нет центрального слоя биндов, `KeyCode` зашиты литералами в `FeatureHub`,
//  `KvStageHub`…`KvStageHub4`, `KvKeyboardNav`, `FreeFlyCameraController`.
//
//  ТРЕБОВАНИЕ ВЛАДЕЛЬЦА (дословно): «каждая кнопочка обязана выполнять реальную работу; если
//  функционала нет — убрать из интерфейса». Кнопка удалена, окно осталось справочником:
//  действие, клавиша, пояснение, подсветка конфликтов.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ — на РЕАЛЬНО ПОСТРОЕННОМ окне (не по тексту исходника!):
//    1) в окне НЕТ ни одной кнопки с именем/подписью «Rebind» / «Переназнач…»;
//    2) строк ровно столько, сколько записей в реестре `KvBindings`, и колонка «Keys» заполнена
//       там, где реестр дал клавишу (окно по-прежнему справочник, а не пустой список);
//    3) в окне НЕТ подсказок, помеченных `stub` — сторож на будущее: если кто-то снова добавит
//       элемент «в разработке», прогон покраснеет с именем этого элемента;
//    4) в окне НЕТ текстов «в разработке» / «появится позже» / «будет добавлено»;
//    5) АНТИ-ВАКУУМ: тесты выше зеленели бы и на ПУСТОМ окне, поэтому отдельно проверяется, что
//       окно действительно построилось (строк > 10, есть заголовки разделов, есть поле поиска,
//       реестр биндов непустой).
//
//  ГРАНИЦА ЧЕСТНОСТИ (что этим тестом НЕ проверяется):
//    • тест НЕ запускает игру и не нажимает кнопки: проверяется СОСТАВ построенного окна;
//    • «нажатие ничего не делает» у будущих кнопок этим тестом не ловится — ловится сам факт
//      элемента-обещания (подсказка `stub`) и текст «в разработке»;
//    • настоящее переназначение клавиш не проверяется: его в проекте нет, и тест это закрепляет —
//      пока в окне не появится рабочая кнопка, «переназначить» нечем.
// ================================================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;

namespace KazistovVvTests
{
    /// <summary>
    /// ОКНО ГОРЯЧИХ КЛАВИШ БЕЗ КНОПОК-ЗАГЛУШЕК: справочник, а не обещание редактора биндов.
    ///
    /// Окно строится В ПАМЯТИ (как в `KazistovVvUIManager.BuildShell`: компонент висит на объекте-
    /// владельце, а `Build` получает тело dock-панели) и уничтожается в `TearDown`. Сцена, канвас
    /// и менеджер интерфейса не нужны — поэтому тест идёт в EditMode.
    /// </summary>
    [Category(KvTestCategories.Fast)]
    public class KvUiStubButtonsTests
    {
        /// <summary>Объект-владелец компонента (в игре — объект менеджера интерфейса).</summary>
        private GameObject hostGo;

        /// <summary>«Тело» панели: сюда окно строит свои элементы.</summary>
        private GameObject bodyGo;

        private KvHotkeyView view;

        /// <summary>Имя строки бинда — префикс, который ставит `KvHotkeyView.BindRow`.</summary>
        private const string BindRowPrefix = "HKRow_";

        /// <summary>Имя подзаголовка раздела — префикс из `KvHotkeyView.SubHeader`.</summary>
        private const string GroupHeaderPrefix = "HKG_";

        [SetUp]
        public void BuildHotkeyWindow()
        {
            bodyGo = new GameObject("KvUiStubButtons_Body", typeof(RectTransform));
            ((RectTransform)bodyGo.transform).sizeDelta = new Vector2(430f, 330f);

            hostGo = new GameObject("KvUiStubButtons_Host");
            view = hostGo.AddComponent<KvHotkeyView>();
            view.Build((RectTransform)bodyGo.transform);

            // АНТИ-ВАКУУМ ПОДГОТОВКИ: если окно не построилось, проверки ниже были бы вакуумными.
            Assert.IsNotNull(view, "компонент окна горячих клавиш не создался — проверять нечего");
            Assert.Greater(view.RowCount, 10,
                "окно горячих клавиш построило " + view.RowCount + " строк — это не список биндов " +
                "(подготовка теста сорвана, дальше проверять нечего)");
        }

        [TearDown]
        public void DestroyHotkeyWindow()
        {
            // DestroyImmediate, а НЕ Destroy: в EditMode отложенное уничтожение не выполняется,
            // и объект остался бы в сцене редактора до следующего кадра.
            if (hostGo != null) Object.DestroyImmediate(hostGo);
            if (bodyGo != null) Object.DestroyImmediate(bodyGo);
            hostGo = null;
            bodyGo = null;
            view = null;
        }

        // ---------------------------------------------------------------- вспомогательное

        /// <summary>Все кнопки окна, включая выключенные объекты (как их увидел бы оператор).</summary>
        private List<Button> Buttons()
        {
            return new List<Button>(bodyGo.GetComponentsInChildren<Button>(true));
        }

        /// <summary>Строки биндов в окне: имя обязано начинаться с «HKRow_» (заголовки — «HK_»/«HKG_»).</summary>
        private List<Transform> BindRows()
        {
            var rows = new List<Transform>();
            foreach (Transform t in bodyGo.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(BindRowPrefix)) rows.Add(t);
            return rows;
        }

        /// <summary>Сколько строк реестра содержат НЕПУСТУЮ клавишу (то, что оператор обязан видеть).</summary>
        private static int RegistryRowsWithKeys()
        {
            int count = 0;
            foreach (KvBindEntry e in KvBindings.All())
                if (e != null && !string.IsNullOrEmpty(e.Keys)) count++;
            return count;
        }

        /// <summary>
        /// КНОПКА-ЗАГЛУШКА «ПЕРЕНАЗНАЧИТЬ» (та самая, что убрана): имя `Rebind` или подпись
        /// «Переназначить». Логика вынесена в отдельный метод НАМЕРЕННО — её проверяет
        /// `RebindDetector_ReallyCatchesAPlantedStubButton` на подложенной кнопке, иначе тест
        /// «кнопки нет» ничего не доказывал бы (мог просто не уметь её находить).
        /// </summary>
        private static bool IsRebindButton(Button button, List<string> found)
        {
            bool hit = false;
            string name = button.gameObject.name;
            if (name.ToLowerInvariant().Contains("rebind"))
            {
                found.Add("кнопка «" + name + "» (имя содержит Rebind)");
                hit = true;
            }
            if (name.Contains("Переназнач"))
            {
                found.Add("кнопка «" + name + "» (имя содержит «Переназнач»)");
                hit = true;
            }

            foreach (Text label in button.GetComponentsInChildren<Text>(true))
            {
                string text = label.text ?? "";
                if (!text.Contains("Переназнач")) continue;
                found.Add("кнопка «" + name + "» с подписью «" + text + "»");
                hit = true;
            }
            return hit;
        }

        /// <summary>Подсказки окна, помеченные «в разработке» (`stub = true`), — с описанием каждой.</summary>
        private List<string> CollectStubTooltips()
        {
            var stubs = new List<string>();
            foreach (KvTooltipTarget tip in bodyGo.GetComponentsInChildren<KvTooltipTarget>(true))
            {
                if (!tip.stub) continue;
                stubs.Add("«" + tip.gameObject.name + "» (заголовок «" + tip.title +
                          "», описание «" + tip.body + "»)");
            }
            return stubs;
        }

        // ---------------------------------------------------------------- 1. КНОПОК «ПЕРЕНАЗНАЧИТЬ» НЕТ

        /// <summary>
        /// В ОКНЕ ГОРЯЧИХ КЛАВИШ НЕТ НИ ОДНОЙ КНОПКИ «ПЕРЕНАЗНАЧИТЬ».
        ///
        /// ЧТО БЫЛО. В каждой из 56 строк стояла кнопка `Rebind` с подписью «Переназначить»:
        /// нажатие писало в статус «появится позже», подсказка говорила «в разработке».
        ///
        /// ЧЕМ ПЛОХО. Кнопка обещала редактор биндов, которого нет: оператор нажимал и упирался в
        /// «позже», а окно из справочника превращалось в «здесь скоро будет работа».
        ///
        /// ЧТО СТАЛО. Кнопка, её `LayoutElement`, подсказка и обработчик удалены; клавиши видны
        /// текстом в колонке «Keys». Этот тест закрепляет удаление: вернут кнопку — прогон
        /// покраснеет с её именем.
        /// </summary>
        [Test]
        public void HotkeyWindow_HasNoRebindButtons()
        {
            List<Button> buttons = Buttons();
            var found = new List<string>();
            foreach (Button button in buttons) IsRebindButton(button, found);

            Assert.AreEqual(0, found.Count,
                "в окне горячих клавиш снова есть кнопка «Переназначить», хотя переназначения в " +
                "проекте НЕТ (нет слоя биндов: `KeyCode` зашиты литералами в `FeatureHub`, " +
                "`KvStageHub`…`KvStageHub4`, `KvKeyboardNav`, `FreeFlyCameraController`). " +
                "Найдено: " + string.Join("; ", found.ToArray()) +
                ". Кнопка-обещание без функционала запрещена владельцем — либо сделайте сквозной " +
                "слой биндов и работающий редактор, либо не показывайте кнопку. Всего кнопок в окне: " +
                buttons.Count);
        }

        // ---------------------------------------------------------------- 2. СТРОКИ = РЕЕСТР

        /// <summary>
        /// СТРОК В ОКНЕ СТОЛЬКО ЖЕ, СКОЛЬКО ЗАПИСЕЙ В РЕЕСТРЕ, И КЛАВИШИ ПОКАЗАНЫ.
        ///
        /// ЗАЧЕМ. Удаление кнопки не должно было «унести» вместе с ней данные строки. Проверяется
        /// ДВЕ вещи сразу: (а) окно не потеряло и не задвоило ни одной записи реестра `KvBindings`;
        /// (б) колонка «Keys» заполнена ровно там, где реестр дал клавишу, — то есть окно осталось
        /// справочником клавиш, ради которого его и открывают по F12.
        /// </summary>
        [Test]
        public void HotkeyWindow_RowsMatchBindingsRegistry()
        {
            List<KvBindEntry> registry = KvBindings.All();
            List<Transform> rows = BindRows();

            Assert.Greater(registry.Count, 10,
                "в реестре биндов " + registry.Count + " записей — сверять окно не с чем");
            Assert.AreEqual(registry.Count, rows.Count,
                "строк в окне " + rows.Count + ", а записей в реестре `KvBindings` " + registry.Count +
                " — окно и реестр разошлись: часть биндов оператор не увидит (или увидит лишнее)");

            int withKeys = 0;
            foreach (Transform row in rows)
            {
                Transform keyColumn = row.Find("Keys");
                Assert.IsNotNull(keyColumn,
                    "в строке «" + row.name + "» нет колонки «Keys» — клавиша не показывается, и " +
                    "окно перестало быть справочником");

                Text keyText = keyColumn.GetComponent<Text>();
                Assert.IsNotNull(keyText,
                    "в колонке «Keys» строки «" + row.name + "» нет текста — клавиша невидима");
                if (!string.IsNullOrEmpty(keyText.text)) withKeys++;
            }

            int expected = RegistryRowsWithKeys();
            Assert.AreEqual(expected, withKeys,
                "клавиши заполнены у " + withKeys + " строк, а реестр даёт клавишу у " + expected +
                " — часть клавиш потерялась по дороге в окно");
            Assert.Greater(withKeys, rows.Count / 2,
                "клавиши показаны меньше чем у половины строк (" + withKeys + " из " + rows.Count +
                ") — окно перестало быть справочником горячих клавиш");
        }

        // ---------------------------------------------------------------- 3. СТОРОЖ НА БУДУЩЕЕ

        /// <summary>
        /// В ОКНЕ НЕТ ПОДСКАЗОК, ПОМЕЧЕННЫХ `stub` (то есть «в разработке»).
        ///
        /// ЗАЧЕМ НА БУДУЩЕЕ. Кнопка «Переназначить» была не одна такая: механизм подсказки
        /// `KvTooltipTarget.stub = true` добавляет к тексту «(в разработке)». Если кто-то снова
        /// добавит в это окно элемент-обещание и пометит его `stub`, тест обязан покраснеть —
        /// иначе дефект вернётся молча, как это уже случилось один раз.
        /// </summary>
        [Test]
        public void HotkeyWindow_HasNoStubTooltips()
        {
            List<string> stubs = CollectStubTooltips();

            Assert.AreEqual(0, stubs.Count,
                "в окне горячих клавиш есть элементы с подсказкой «в разработке» " +
                "(`KvTooltipTarget.stub = true`): " + string.Join("; ", stubs.ToArray()) +
                ". Элемент, который обещает несделанную работу, владелец запретил: либо " +
                "реализуйте функционал и снимите пометку, либо уберите элемент из интерфейса");
        }

        // ---------------------------------------------------------------- 4. НЕТ ОБЕЩАНИЙ В ТЕКСТЕ

        /// <summary>
        /// В ОКНЕ НЕТ ТЕКСТОВ «в разработке» / «появится позже» / «будет добавлено».
        ///
        /// ПОЧЕМУ ОТДЕЛЬНО ОТ ПОДСКАЗОК. Обещание можно написать и прямо в строке (как было в
        /// статусной строке обработчика «Переназначить»: «Переназначение клавиш появится позже —
        /// бинды не меняются»). Такой текст оператор читает сразу, без наведения мыши, и он
        /// действует на него сильнее подсказки.
        /// </summary>
        [Test]
        public void HotkeyWindow_ShowsNoInDevelopmentLabels()
        {
            string[] forbidden = { "в разработке", "появится позже", "будет добавлено" };
            var found = new List<string>();

            foreach (Text text in bodyGo.GetComponentsInChildren<Text>(true))
            {
                string value = text.text ?? "";
                for (int i = 0; i < forbidden.Length; i++)
                {
                    if (!value.Contains(forbidden[i])) continue;
                    found.Add("«" + text.gameObject.name + "»: " + value);
                    break;
                }
            }

            Assert.AreEqual(0, found.Count,
                "в окне горячих клавиш есть строки с обещанием будущей работы: " +
                string.Join("; ", found.ToArray()) +
                ". Обещание «появится позже» — то же самое, что кнопка-заглушка: оператор " +
                "считает, что функция будет, и не ищет рабочий путь");
        }

        // ---------------------------------------------------------------- 5. АНТИ-ВАКУУМ

        /// <summary>
        /// ОКНО ДЕЙСТВИТЕЛЬНО ПОСТРОЕНО: строк много, есть заголовки разделов и поле поиска.
        ///
        /// ЗАЧЕМ. Все проверки «чего-то НЕТ» зеленеют на пустом окне — классическая вакуумная
        /// зелень. Здесь закрепляется, что окно живое: биндов больше десяти, они сгруппированы по
        /// разделам, поиск на месте, а `RowCount` (диагностика окна) больше числа строк биндов,
        /// потому что в него входят заголовки.
        /// </summary>
        [Test]
        public void HotkeyWindow_IsReallyBuilt_AntiVacuum()
        {
            List<Transform> rows = BindRows();
            Assert.Greater(rows.Count, 10,
                "строк биндов " + rows.Count + " — на таком окне проверки «нет кнопок/нет заглушек» " +
                "зеленеют даром");

            int groupHeaders = 0;
            foreach (Transform t in bodyGo.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(GroupHeaderPrefix)) groupHeaders++;
            Assert.Greater(groupHeaders, 0,
                "в окне нет ни одного заголовка раздела («" + GroupHeaderPrefix + "…») — группировка " +
                "сломана, и список перестал быть читаемым");

            Assert.Greater(view.RowCount, rows.Count,
                "RowCount (" + view.RowCount + ") не больше числа строк биндов (" + rows.Count +
                ") — окно не добавляет заголовки разделов, то есть построилось не целиком");

            InputField search = bodyGo.GetComponentInChildren<InputField>(true);
            Assert.IsNotNull(search,
                "в окне нет поля поиска — оно было всегда, и его пропажа означает, что построилось " +
                "не то окно (или окно строится другим кодом)");

            Assert.Greater(KvBindings.All().Count, 10,
                "реестр биндов пуст (" + KvBindings.All().Count + " записей) — окну нечего показывать");
            Assert.AreEqual("", view.Query,
                "поле поиска окна при постройке не пустое («" + view.Query + "») — список уже " +
                "отфильтрован, и проверки шли бы по части биндов");
        }

        // ---------------------------------------------------------------- 6. САМОПРОВЕРКА СТОРОЖЕЙ

        /// <summary>
        /// СТОРОЖА ДЕЙСТВИТЕЛЬНО ЛОВЯТ ТУ КНОПКУ, КОТОРУЮ УБРАЛИ (анти-вакуум проверки, а не окна).
        ///
        /// ЗАЧЕМ. «Кнопки Rebind нет» и «подсказок stub нет» — утверждения о ПУСТОТЕ, и они были бы
        /// зелёными даже на сломанном детекторе. Здесь ровно та кнопка-заглушка ПОДКЛАДЫВАЕТСЯ в
        /// построенное окно: имя `Rebind`, подпись «Переназначить», подсказка `stub = true`. Обе
        /// проверки обязаны её УВИДЕТЬ — а после удаления снова не видеть. Так тест доказывает, что
        /// вернувшаяся заглушка будет поймана, а не «пропущена молча».
        /// </summary>
        [Test]
        public void StubDetectors_CatchAPlantedRebindButton()
        {
            // 1. ПОДКЛАДЫВАЕМ ту самую кнопку: имя Rebind, подпись «Переназначить», подсказка stub.
            GameObject probe = new GameObject("Rebind", typeof(RectTransform), typeof(Image), typeof(Button));
            probe.transform.SetParent(bodyGo.transform, false);
            Button probeButton = probe.GetComponent<Button>();
            Assert.IsNotNull(probeButton, "подложенная кнопка не создалась — проверять детектор нечем");

            Text label = KvTheme.CreateText((RectTransform)probe.transform, "Label", "Переназначить",
                KvTheme.FontSizeSmall, TextAnchor.MiddleCenter, KvTheme.TextMain);
            Assert.IsNotNull(label, "подпись подложенной кнопки не создалась");

            KvTooltipTarget probeTip = probe.AddComponent<KvTooltipTarget>();
            probeTip.Set("Переназначить", "в разработке", "");
            probeTip.stub = true;

            // 2. ОБЕ ПРОВЕРКИ ОБЯЗАНЫ СРАБОТАТЬ.
            var rebindFound = new List<string>();
            Assert.IsTrue(IsRebindButton(probeButton, rebindFound),
                "детектор НЕ увидел подложенную кнопку «Rebind» — значит проверка " +
                "`HotkeyWindow_HasNoRebindButtons` вакуумная и вернувшуюся заглушку пропустит");

            List<string> stubs = CollectStubTooltips();
            Assert.Greater(stubs.Count, 0,
                "детектор НЕ увидел подложенную подсказку «в разработке» — значит проверка " +
                "`HotkeyWindow_HasNoStubTooltips` вакуумная");

            // 3. УБИРАЕМ ПОДКЛАДКУ — детекторы обязаны замолчать (проверяют ТЕКУЩЕЕ дерево, а не историю).
            Object.DestroyImmediate(probe);
            var after = new List<string>();
            foreach (Button button in Buttons()) IsRebindButton(button, after);
            Assert.AreEqual(0, after.Count,
                "после удаления подложенной кнопки детектор продолжает её видеть — он смотрит не на " +
                "то дерево: " + string.Join("; ", after.ToArray()));
            Assert.AreEqual(0, CollectStubTooltips().Count,
                "после удаления подложки детектор подсказок продолжает видеть «в разработке»");
        }
    }
}
