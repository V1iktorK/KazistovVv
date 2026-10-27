using NUnit.Framework;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты ПРАВИЛА «ЧЕГО В ШЛЕМЕ БЫТЬ НЕ ДОЛЖНО» (сессия §35, карточка Б20).
    ///
    /// ЗАДАНИЕ ВЛАДЕЛЬЦА. «Десктопный оверлей инструментов и окон (то, что похоже на интерфейс
    /// для ПК) в шлеме должен быть скрыт — его там быть не должно». При этом отдельно запрещено
    /// ломать десктопный режим и требуется, чтобы панель в шлеме работала.
    ///
    /// ГЛАВНАЯ ПРОВЕРКА ЗДЕСЬ — НЕ ОЧЕВИДНАЯ. Кажется правильным: «десктопный интерфейс в шлеме
    /// не нужен — выключить объект `KazistovVv_UI`». Это сломало бы панель шлема: РЕЕСТР КОМАНД
    /// наполняет менеджер интерфейса, живущий на ТОМ ЖЕ объекте. Команды перестали бы
    /// регистрироваться, панель с иконками групп оказалась бы пустой, и в шлеме не нажималось бы
    /// ничего — при полном отсутствии ошибок в журнале. Поэтому тесты требуют: десктопный
    /// интерфейс гасится КАК РИСОВАНИЕ, а не как объект, а «убрать совсем» разрешено ТОЛЬКО
    /// для компонента-якоря, в котором логики нет.
    /// </summary>
    public class KvXrOverlayPolicyTests
    {
        /// <summary>
        /// ДЕСКТОПНЫЙ ИНТЕРФЕЙС ГАСИТСЯ КАК РИСОВАНИЕ. Если этот тест когда-нибудь упадёт
        /// со значением `Remove`, панель шлема останется без команд.
        /// </summary>
        [Test]
        public void DesktopUiRoot_IsHiddenNotRemoved()
        {
            KvXrOverlayDisposition disposition =
                KvXrOverlayPolicy.Classify(KvXrOverlayPolicy.DesktopUiRootName);

            Assert.AreEqual(KvXrOverlayDisposition.HideRendering, disposition,
                "`KazistovVv_UI` несёт менеджер интерфейса, который наполняет реестр команд: " +
                "выключить объект целиком значит оставить панель шлема без команд");
            Assert.AreNotEqual(KvXrOverlayDisposition.Remove, disposition,
                "объект с менеджером интерфейса убирать нельзя ни при каких условиях");
        }

        /// <summary>Канвас десктопного интерфейса — тоже только «погасить рисование».</summary>
        [Test]
        public void DesktopCanvas_IsHiddenNotRemoved()
        {
            Assert.AreEqual(KvXrOverlayDisposition.HideRendering,
                KvXrOverlayPolicy.Classify(KvXrOverlayPolicy.DesktopCanvasName),
                "канвас десктопного интерфейса гасится рисованием, а не удалением");
        }

        /// <summary>
        /// ЯКОРЬ ПАНЕЛИ УБИРАЕТСЯ СОВСЕМ. Именно он переводит десктопный канвас в мировое
        /// пространство и вешает его перед оператором — то есть создаёт тот самый «интерфейс
        /// для ПК» в шлеме, которого быть не должно. Логики в нём нет, терять нечего.
        /// </summary>
        [Test]
        public void PanelAnchor_IsRemovedEntirely()
        {
            Assert.AreEqual(KvXrOverlayDisposition.Remove,
                KvXrOverlayPolicy.Classify(KvXrOverlayPolicy.PanelAnchorName),
                "`XrPanelAnchor` обязан убираться совсем: он и есть причина десктопного оверлея в шлеме");
        }

        /// <summary>
        /// ИНТЕРФЕЙС ШЛЕМА НЕ ГАСИТСЯ НИКОГДА. Панель с обобщающими иконками групп — это
        /// единственное управление в шлеме; погасить её значило бы оставить владельца без панели.
        /// </summary>
        [Test]
        public void VrInterface_IsNeverTouched()
        {
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.Classify(KvXrOverlayPolicy.VrPanelName),
                "панель шлема гасить нельзя — это единственное управление в шлеме");
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.Classify(KvXrOverlayPolicy.VrPanelCanvasName),
                "канвас панели шлема гасить нельзя");
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.Classify(KvXrOverlayPolicy.VrGroupCanvasName),
                "канвас панели групп гасить нельзя");
        }

        /// <summary>
        /// Интерфейс шлема узнаётся РАНЬШЕ десктопного: он тоже канвас, и перепутать их
        /// значило бы погасить единственную панель управления.
        /// </summary>
        [Test]
        public void VrInterface_IsRecognisedAsVr()
        {
            Assert.IsTrue(KvXrOverlayPolicy.IsVrInterface(KvXrOverlayPolicy.VrPanelName),
                "панель шлема обязана опознаваться как интерфейс шлема");
            Assert.IsTrue(KvXrOverlayPolicy.IsVrInterface(KvXrOverlayPolicy.VrGroupCanvasName),
                "канвас панели групп обязан опознаваться как интерфейс шлема");
            Assert.IsFalse(KvXrOverlayPolicy.IsVrInterface(KvXrOverlayPolicy.DesktopUiRootName),
                "десктопный интерфейс НЕ является интерфейсом шлема");
        }

        /// <summary>
        /// НЕЗНАКОМОЕ ИМЯ — НЕ ТРОГАТЬ. Дефолт выбран именно такой: ошибка в списке имён
        /// не должна молча гасить что-то нужное (свет, стенды, роботов, риг).
        /// </summary>
        [Test]
        public void UnknownObjects_AreKept()
        {
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.Classify("XrKeyLight"),
                "свет гасить нельзя — без него в шлеме темно");
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.Classify("Стенд_1_Стол"),
                "стенды гасить нельзя — владелец требует видеть столы с роботами");
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.Classify("XrRig"),
                "риг гасить нельзя — без него не работает положение в пространстве");
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.Classify(""),
                "пустое имя обязано трактоваться как «не трогать», а не как «убрать»");
            Assert.AreEqual(KvXrOverlayDisposition.Keep,
                KvXrOverlayPolicy.Classify(null),
                "отсутствие имени обязано трактоваться как «не трогать»");
        }

        /// <summary>
        /// РЕЕСТР КОМАНД ОСТАЁТСЯ ЖИВЫМ. Прямая проверка того самого решения: при гашении
        /// рисованием команды продолжают регистрироваться и панель шлема работает.
        /// </summary>
        [Test]
        public void CommandsStayAlive_ForEveryDispositionExceptRemoval()
        {
            Assert.IsTrue(KvXrOverlayPolicy.KeepsCommandsAlive(KvXrOverlayDisposition.HideRendering),
                "гашение рисования обязано сохранять реестр команд");
            Assert.IsTrue(KvXrOverlayPolicy.KeepsCommandsAlive(KvXrOverlayDisposition.Keep),
                "нетронутый объект тем более сохраняет команды");
            Assert.IsFalse(KvXrOverlayPolicy.KeepsCommandsAlive(KvXrOverlayDisposition.Remove),
                "удаление объекта с менеджером интерфейса убило бы реестр команд — " +
                "именно поэтому десктопный интерфейс НЕ удаляется");
        }

        /// <summary>
        /// РЕШЕНИЕ ПО КАЖДОМУ ОБЪЕКТУ СЦЕНЫ ШЛЕМА. Это перечень того, что реально есть в `XrMain`;
        /// тест фиксирует, что ни один нужный объект не попал под гашение.
        /// </summary>
        [Test]
        public void XrMainObjects_GetTheIntendedDisposition()
        {
            string[] hideExpected = { "KazistovVv_UI", "KazistovVvCanvas" };
            string[] keepExpected =
            {
                "XrKeyLight", "XrFillLight", "XrRig", "XrCamera", "XrSceneContent",
                "XrMrRoom", "XrQuickPanel", "XrFloor", "Стенд_1_Стол", "Стенд_2_Стол"
            };

            foreach (string name in hideExpected)
                Assert.AreEqual(KvXrOverlayDisposition.HideRendering, KvXrOverlayPolicy.Classify(name),
                    "«" + name + "» обязан быть погашен как рисование");

            foreach (string name in keepExpected)
                Assert.AreEqual(KvXrOverlayDisposition.Keep, KvXrOverlayPolicy.Classify(name),
                    "«" + name + "» трогать нельзя");

            Assert.AreEqual(KvXrOverlayDisposition.Remove,
                KvXrOverlayPolicy.Classify("XrPanelAnchor"),
                "якорь десктопной панели обязан убираться совсем");
        }

        /// <summary>
        /// НЕВИДИМЫЙ ДЕСКТОПНЫЙ ИНТЕРФЕЙС НЕ ДОЛЖЕН БЫТЬ ЦЕЛЬЮ ЛУЧА.
        ///
        /// ЭТО НАЙДЕНО ПО ЖУРНАЛУ ШЛЕМА 16.10.2026: `Указатель: взгляд на «Blocker» — держите
        /// 1,3 с для нажатия`. То есть луч наводился на НЕВИДИМЫЙ объект десктопного интерфейса
        /// и через 1,3 с по нему происходило нажатие. Владелец видел бы, что в шлеме «само
        /// переключается», и причины не нашёл бы: на экране ничего нет, а нажатие есть.
        ///
        /// Поэтому гашение рисования обязано сопровождаться гашением райкастера: политика
        /// возвращает «погасить рисование» — и это же решение означает «убрать из целей луча».
        /// </summary>
        [Test]
        public void HiddenDesktopUi_MustNotStayClickable()
        {
            KvXrOverlayDisposition desktop =
                KvXrOverlayPolicy.Classify(KvXrOverlayPolicy.DesktopCanvasName);

            Assert.AreEqual(KvXrOverlayDisposition.HideRendering, desktop,
                "десктопный канвас обязан быть погашен как рисование (логика остаётся живой)");
            Assert.IsTrue(KvXrOverlayPolicy.HidesFromPointer(desktop),
                "погашенный десктопный канвас обязан ПЕРЕСТАТЬ быть целью луча: иначе по невидимым " +
                "кнопкам будут происходить нажатия, и владелец увидит «само переключается»");

            Assert.IsFalse(KvXrOverlayPolicy.HidesFromPointer(KvXrOverlayDisposition.Keep),
                "нетронутый объект остаётся целью луча — панель шлема обязана нажиматься");
            Assert.IsFalse(KvXrOverlayPolicy.HidesFromPointer(KvXrOverlayDisposition.Remove),
                "у удаляемого объекта целей луча уже нет по определению");
        }

        /// <summary>Строка журнала обязана содержать числа: без неё «стало лучше» не считается.</summary>
        [Test]
        public void Describe_ContainsCounts()
        {
            string text = KvXrOverlayPolicy.Describe(2, 1, 3, 85);

            StringAssert.Contains("канвасов 2", text, "в журнале обязано быть число погашенных канвасов");
            StringAssert.Contains("XrPanelAnchor", text, "в журнале обязан быть назван убранный якорь");
            StringAssert.Contains("85", text, "в журнале обязано быть число команд в реестре — " +
                "именно оно доказывает, что панель шлема не осталась пустой");
        }
    }
}
