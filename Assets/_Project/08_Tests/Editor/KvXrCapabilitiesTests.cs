// ================================================================================================
//  KvXrCapabilitiesTests.cs — EditMode-тесты ПРОБ ВОЗМОЖНОСТЕЙ MR/AR (сессия S44).
//
//  ОТКУДА ЗАДАЧА. В `KvXrRig` три детектора возвращали «нет» с пояснением «пробы пока нет».
//  Это честно, но приложение ВСЕГДА оказывалось в VR — даже на шлеме, который умеет сквозной
//  вид. Суббота ушла бы на «почему не MR» вместо настройки MR.
//
//  ЧТО СДЕЛАНО. `KvXrCapabilities` спрашивает OpenXR БЕЗ вендорского SDK ([R3] не нарушается) и
//  возвращает ТРИСТАТЕЗНЫЙ ответ: `Yes` / `No` / `Unknown`.
//
//  ЗАЧЕМ ИМЕННО ТРИ СОСТОЯНИЯ, А НЕ `bool`. «Не умеет» и «не смогли спросить» — РАЗНЫЕ вещи:
//  первое нормально, второе требует внимания наладчика. Если свести их к `false`, оба случая
//  дают «режим VR», и в журнале не отличить норму от дефекта.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) `Unknown` НЕ читается как «да» — иначе выдали бы MR без картинки с камеры (серый экран);
//    2) список расширений заполнен и не содержит пустых строк (по нему идёт поиск);
//    3) строка журнала РАЗЛИЧАЕТ «нет» и «неизвестно» — это её главная работа;
//    4) без запущенного XR пробы отвечают `No`, а не бросают — в редакторе это норма;
//    5) имена расширений покрывают хотя бы одну известную платформу (Meta и PICO).
// ================================================================================================

using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Пробы возможностей MR/AR: три состояния, честный выбор, читаемая строка.</summary>
    public class KvXrCapabilitiesTests
    {
        // ---------------------------------------------------------------- 1. ТРИСТАТЕЗНАЯ ЛОГИКА

        /// <summary>
        /// `Unknown` НЕ РАВЕН `Yes`. Это главный инвариант файла: если проба не смогла ответить,
        /// приложение обязано остаться в VR. Иначе оператор увидит серый экран вместо комнаты —
        /// и решит, что сломано железо.
        /// </summary>
        [Test]
        public void UnknownIsNotYes()
        {
            Assert.IsFalse(KvXrCapabilities.IsYes(KvXrCapabilities.Answer.Unknown),
                "«не удалось спросить» обязано читаться как «нет», а не «да»");
            Assert.IsFalse(KvXrCapabilities.IsYes(KvXrCapabilities.Answer.No));
            Assert.IsTrue(KvXrCapabilities.IsYes(KvXrCapabilities.Answer.Yes));
        }

        /// <summary>
        /// ВЫБОР РЕЖИМА С УЧЁТОМ ТРЁХ СОСТОЯНИЙ: даже если бы `Unknown` просочился как `true`,
        /// выбор режима не должен давать MR без сквозного вида. Проверяем итоговое правило через
        /// селектор: MR требует И сквозного вида, И камеры.
        /// </summary>
        [Test]
        public void ModeSelection_RequiresBothPassthroughAndCamera()
        {
            Assert.AreEqual(KvXrPlatformMode.Vr,
                KvXrPlatformModeSelector.Choose(false, false, false),
                "ни одной возможности — VR, и это не ошибка");
            Assert.AreEqual(KvXrPlatformMode.Vr,
                KvXrPlatformModeSelector.Choose(false, true, false),
                "якоря без камеры — VR: показать нечего");
            Assert.AreEqual(KvXrPlatformMode.Mr,
                KvXrPlatformModeSelector.Choose(true, false, true),
                "сквозной вид и камера — MR (якоря для него не обязательны)");
        }

        // ---------------------------------------------------------------- 2. СПИСКИ РАСШИРЕНИЙ

        /// <summary>
        /// СПИСОК РАСШИРЕНИЙ СКВОЗНОГО ВИДА ГОДЕН ДЛЯ ПОИСКА: непустой, без пустых строк.
        ///
        /// По нему идёт сверка с рантаймом. Пустая строка в списке — это «искать пустоту», то есть
        /// молчаливое «нет» на любой платформе.
        /// </summary>
        [Test]
        public void PassthroughExtensionList_IsUsable()
        {
            string[] names = KvXrCapabilities.PassthroughExtensionNames;
            Assert.Greater(names.Length, 0, "список расширений сквозного вида пуст — проба всегда скажет «нет»");

            foreach (string name in names)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(name),
                    "в списке расширений есть пустая строка — это молчаливое «нет» на любой платформе");
                StringAssert.StartsWith("XR_", name,
                    "имя расширения OpenXR начинается с XR_, получено: «" + name + "»");
            }
        }

        /// <summary>
        /// ПОКРЫТЫ ОБЕ ПЛАТФОРМЫ, НА КОТОРЫЕ ОРИЕНТИРУЕТСЯ ПРОЕКТ: Meta и PICO (Bytekdance).
        /// Если одна из них выпадет, на соответствующем шлеме проба скажет «нет» — и MR там не
        /// включится, хотя железо умеет.
        /// </summary>
        [Test]
        public void PassthroughList_CoversBothPlatforms()
        {
            var joined = string.Join(" ", KvXrCapabilities.PassthroughExtensionNames).ToUpperInvariant();
            StringAssert.Contains("_FB_", joined,
                "в списке нет расширения Meta (XR_FB_…) — на Meta-шлемах проба скажет «нет»");
            StringAssert.Contains("_BD_", joined,
                "в списке нет расширения PICO/Bytekdance (XR_BD_…) — на PICO проба скажет «нет»");
        }

        /// <summary>Список якорей тоже годен: без него AR не включится ни при каких условиях.</summary>
        [Test]
        public void SpatialAnchorList_IsUsable()
        {
            string[] names = KvXrCapabilities.SpatialAnchorExtensionNames;
            Assert.Greater(names.Length, 0);
            foreach (string name in names)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(name));
                StringAssert.StartsWith("XR_", name);
            }
        }

        // ---------------------------------------------------------------- 3. СТРОКА ЖУРНАЛА

        /// <summary>
        /// СТРОКА РАЗЛИЧАЕТ «НЕТ» И «НЕИЗВЕСТНО». Это её главная работа: наладчик должен видеть,
        /// устройство не умеет MR или программа не смогла спросить. Иначе «режим VR» выглядит
        /// одинаково в обоих случаях, и искать причину придётся наугад.
        /// </summary>
        [Test]
        public void Describe_DistinguishesNoFromUnknown()
        {
            string noLine = KvXrCapabilities.Describe(
                KvXrCapabilities.Answer.No, KvXrCapabilities.Answer.No, KvXrCapabilities.Answer.No);
            string unknownLine = KvXrCapabilities.Describe(
                KvXrCapabilities.Answer.Unknown, KvXrCapabilities.Answer.Unknown, KvXrCapabilities.Answer.Unknown);

            StringAssert.Contains("НЕТ", noLine, "строка при «нет» обязана говорить «НЕТ»: " + noLine);
            StringAssert.Contains("неизвестно", unknownLine,
                "строка при «неизвестно» обязана говорить «неизвестно»: " + unknownLine);
            Assert.AreNotEqual(noLine, unknownLine,
                "строки для «нет» и «неизвестно» совпали — наладчик не отличит норму от дефекта");
        }

        /// <summary>Строка называет ВСЕ ТРИ возможности — иначе пропавшая не будет видна.</summary>
        [Test]
        public void Describe_NamesAllThreeCapabilities()
        {
            string line = KvXrCapabilities.Describe(
                KvXrCapabilities.Answer.Yes, KvXrCapabilities.Answer.No, KvXrCapabilities.Answer.Yes);

            StringAssert.Contains("сквозной вид", line, line);
            StringAssert.Contains("якоря", line, line);
            StringAssert.Contains("камера", line, line);
            StringAssert.Contains("есть", line, "строка обязана называть и положительный ответ: " + line);
        }

        /// <summary>
        /// ПРИ НЕИЗВЕСТНОСТИ СТРОКА ПРЕДУПРЕЖДАЕТ, ЧТО РЕЖИМ VR ВЫБРАН ЧЕСТНО. Без этого пояснения
        /// «VR» прочитали бы как «устройство не умеет», хотя дело в отсутствии ответа.
        /// </summary>
        [Test]
        public void Describe_WarnsWhenAnswersAreUnknown()
        {
            string line = KvXrCapabilities.Describe(
                KvXrCapabilities.Answer.Unknown, KvXrCapabilities.Answer.No, KvXrCapabilities.Answer.No);

            StringAssert.Contains("не удалось спросить", line,
                "строка обязана честно сказать, что часть ответов не получена: " + line);
            StringAssert.Contains("честно", line, "и что режим VR выбран именно поэтому: " + line);
        }

        /// <summary>
        /// БЕЗ ЗАПУЩЕННОГО XR ПРОБЫ ОТВЕЧАЮТ `No` И НЕ БРОСАЮТ. В редакторе (и в тестах) XR не
        /// поднят — это норма, и пробы обязаны это пережить, а не падать.
        /// </summary>
        [Test]
        public void WithoutXr_ProbesAnswerNoWithoutThrowing()
        {
            KvXrCapabilities.Answer passthrough = KvXrCapabilities.Answer.Unknown;
            Assert.DoesNotThrow(() => passthrough = KvXrCapabilities.Passthrough(),
                "проба сквозного вида не должна бросать без XR");

            if (!KvXrCapabilities.XrRunning)
            {
                Assert.AreEqual(KvXrCapabilities.Answer.No, passthrough,
                    "без активного XR сквозного вида быть не может — ответ обязан быть «нет», а не «неизвестно»");
            }

            Assert.DoesNotThrow(() => KvXrCapabilities.SpatialAnchors());
            Assert.DoesNotThrow(() => KvXrCapabilities.Camera());
            Assert.DoesNotThrow(() => KvXrCapabilities.Describe(
                KvXrCapabilities.Answer.No, KvXrCapabilities.Answer.No, KvXrCapabilities.Answer.No));
        }

        /// <summary>
        /// ПУСТОЙ СПИСОК РАСШИРЕНИЙ — `Unknown`, А НЕ «ДА». Проверка границы: если кто-то передаст
        /// пустой список, проба не должна решить, что «расширение найдено среди ничего».
        /// </summary>
        [Test]
        public void EmptyExtensionList_IsUnknownNotYes()
        {
            Assert.AreEqual(KvXrCapabilities.Answer.Unknown, KvXrCapabilities.CheckExtension(null));
            Assert.AreEqual(KvXrCapabilities.Answer.Unknown, KvXrCapabilities.CheckExtension(new string[0]));
            Assert.AreNotEqual(KvXrCapabilities.Answer.Yes, KvXrCapabilities.CheckExtension(new string[0]));
        }
    }
}
