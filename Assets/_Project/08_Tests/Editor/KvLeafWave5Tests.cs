// ================================================================================================
//  KvLeafWave5Tests.cs — EditMode-тесты ВОЛНЫ 5 КАМПАНИИ «каждый листочек путеводителя закрыт».
//
//  ОТКУДА ЗАДАЧА. `Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv` — 213 листьев со статусом «не проверено»
//  (после волн 1–4, 6a). Эта волна закрывает ПК-поверхность: строка меню (Файл/Правка/Вид/
//  Робот/Сервис/Справка, слайды 7–31), кнопки и ручки тулбара (33–59), списки групп (59),
//  дерево (61), статус-бар (64), палитра команд (66) и служебные кнопки (76). Остальные
//  листья веера/шлема и планшета — в `KvLeafWave5bTests`.
//
//  ТАКТИКА (та же, что в волнах 3/4): массовая триажа → группы по типу доказательства → батчи.
//  Для каждого листа — ТЕСТ, закрепляющий его обещание: пункт существует на СВОЕЙ поверхности,
//  доступность равна условию команды, нажатие его же `onClick` доходит до команды (строка
//  `нажатие: <id>`, которую пишет сам `KvCommands.Invoke`), а там, где лист обещает состояние,
//  — и само состояние (галка, панель, флаг, текст подсказки).
//
//  ЧЕМ ПРОВЕРЯЕТСЯ (P4-3). ТОЛЬКО ПОВЕДЕНИЕМ живого кода: оболочка собирается штатным
//  приватным `BuildShell()`, пункты нажимаются ИХ ЖЕ `onClick`, состояние читается геттерами
//  самого интерфейса, журнал — из `KvActionLog.Entries`. Тексты исходников файлы тестов НЕ
//  читают. Ни один тест не открывает сцену через `OpenSceneMode.Single`, не запускает игру,
//  не собирает APK и не ходит в сеть: `Awake` хабов в EditMode не вызывается, поэтому службы
//  (веб-дашборд, запись видео, микрофон) в тестах отсутствуют — команды обязаны честно
//  отказывать с причиной в журнале, что и проверяется там, где на это опирается лист.
//
//  ЧИСКА. `TearDown` возвращает пустую сцену: хосты, чужие корневые объекты, открытые
//  окна/палитра и глобальные настройки (тема, язык, геймпад, режим курсора) снимаются всегда,
//  даже когда тест упал.
// ================================================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using KazistovVvFeatures;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KazistovVvTests
{
    /// <summary>Волна 5: ПК-поверхность из Docs/ЛИСТЬЯ_НЕЗАКРЫТЫЕ.csv (213 листьев «не проверено»).</summary>
    [Category(KvTestCategories.Fast)]
    public class KvLeafWave5Tests
    {
        // ------------------------------------------------------------------ инфраструктура

        /// <summary>Хосты, созданные тестом: снимаются в TearDown независимо от исхода.</summary>
        private readonly List<GameObject> hosts = new List<GameObject>();

        /// <summary>Корни сцены ДО теста: команды создают объекты, они обязаны исчезнуть в TearDown.</summary>
        private readonly List<GameObject> rootsBefore = new List<GameObject>();

        /// <summary>Было ли отсечение повторов до теста (возвращается в TearDown).</summary>
        private bool repeatsWereOn;

        /// <summary>Было ли игнорирование ошибок лога до теста (возвращается в TearDown).</summary>
        private bool ignoreLogsWas;

        /// <summary>СНИМОК РЕЕСТРА КОМАНД ДО `BuildShell` (общий статик — вернуть как был).</summary>
        private static List<KvCommand> registrySnapshot;

        /// <summary>Язык/тема/геймпад/режим курсора до теста — возвращаются в TearDown.</summary>
        private string localeWas;
        private KvThemeMode themeWas;
        private bool gamepadWasOn;
        private KvCursorMode cursorWas;

        [SetUp]
        public void SetUp()
        {
            // ОТСЕЧЕНИЕ ПОВТОРОВ МЕШАЕТ ПРОВЕРКЕ СОСТОЯНИЯ: ключ журнала вырезает числа и
            // одинаковые события — проверяемая правда была бы правилом журнала, а не кодом.
            repeatsWereOn = KvActionLog.Instance.suppressRepeats;
            KvActionLog.Instance.suppressRepeats = false;
            // ЖУРНАЛ КОЛЬЦЕВОЙ: чистим на входе — каждый тест волны пишет в свой буфер.
            KvActionLog.Instance.Clear();

            ignoreLogsWas = LogAssert.ignoreFailingMessages;

            if (registrySnapshot == null)
            {
                registrySnapshot = new List<KvCommand>();
                foreach (KvCommand command in KvCommands.All) registrySnapshot.Add(command);
            }

            localeWas = KvLoc.CurrentCode;
            if (KvLoc.CurrentCode != "ru") KvLoc.SetLanguage("ru");
            themeWas = KvTheme.Mode;
            gamepadWasOn = KvSettings.GamepadHud;
            cursorWas = KvMouseCursor.CurrentMode;

            rootsBefore.Clear();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                rootsBefore.Add(root);
        }

        [TearDown]
        public void TearDown()
        {
            // ГЛОБАЛЬНЫЕ НАСТРОЙКИ — ДО СНЯТИЯ ХОСТОВ: менеджер интерфейса ещё жив и слышит
            // их (KvMouseCursor.SetMode зовёт SetUiVisible у Instance — после уничтожения
            // хоста это MissingReferenceException в TearDown). Подавление — потому что смена
            // темы/режима зовёт `RebuildShell`, а тот зовёт `Destroy` детей: в EditMode это
            // ошибка лога, не про поведение листа.
            Quietly(delegate
            {
                if (KvLoc.CurrentCode != localeWas) KvLoc.SetLanguage(localeWas);
                if (KvTheme.Mode != themeWas) KvTheme.SetMode(themeWas);
                if (KvSettings.GamepadHud != gamepadWasOn) KvSettings.GamepadHud = gamepadWasOn;
                if (KvMouseCursor.CurrentMode != cursorWas)
                    KvMouseCursor.SetMode(cursorWas, "TearDown волны 5");
                KvMouseCursor.Release("TearDown волны 5: возврат курсора");
                if (KvUiStates.Current != null) KvUiStates.Current.HideError();
            });

            // СНЯТИЕ ХОСТОВ ПОД ПОДАВЛЕНИЕМ ОШИБОК ЛОГА: `OnDestroy` оболочки зовёт
            // `Destroy(...)` детей, а в EditMode это ОШИБКА («Destroy may not be called from
            // edit mode») — она не про поведение листа, но NUnit читает её как провал теста.
            Quietly(delegate
            {
                foreach (GameObject host in hosts)
                    if (host != null) UnityEngine.Object.DestroyImmediate(host);
                hosts.Clear();

                // Объекты, созданные нажатыми командами (сетки, зоны, демо, роботы): чужой
                // сцене их оставлять нельзя.
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (rootsBefore.Contains(root)) continue;
                    if (root == null) continue;
                    if (root.name.StartsWith("[", StringComparison.Ordinal)) continue;
                    UnityEngine.Object.DestroyImmediate(root);
                }

                // Окна, которые открывают нажатые кнопки и которые живут вне хостов.
                foreach (string name in new[] { "ArUiCanvas", "ArEventSystem" })
                {
                    GameObject leftover = GameObject.Find(name);
                    if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);
                }
            });

            // ПОДПИСКИ СОБЫТИЙ — ПОСЛЕ УНИЧТОЖЕНИЯ ХОСТОВ: подписчик мёртв, остаток события
            // будит бы удал следующего теста.
            ClearStaticEvent(typeof(KvTheme), "Changed");

            KvActionLog.Instance.suppressRepeats = repeatsWereOn;
            LogAssert.ignoreFailingMessages = ignoreLogsWas;

            // Реестр команд возвращаем к снимку ДО сборки оболочки (OnDestroy менеджера чистит
            // общий статик — без восстановления следующие фикстуры увидели бы пустой реестр).
            if (registrySnapshot != null)
            {
                KvCommands.Clear();
                foreach (KvCommand command in registrySnapshot) KvCommands.Register(command);
                registrySnapshot = null;
            }
        }

        /// <summary>ХОСТ С КОМПОНЕНТОМ (имя — с префиксом, чтобы TearDown находил его).</summary>
        private GameObject NewHost(string name)
        {
            var go = new GameObject("KvLeafWave5_" + name);
            hosts.Add(go);
            return go;
        }

        /// <summary>
        /// СОБРАТЬ ОБОЛОЧКУ В РЕДАКТОРЕ: `Awake` (сам singleton, настройки, тема) и приватный
        /// `BuildShell` — теми же отражениями, которыми волна 4 поднимала оболочку. `Start` НЕ
        /// зовём: он делает то же `BuildShell`, но добавляет корутины и работу со сценой.
        /// </summary>
        private KazistovVvUIManager BuildShell()
        {
            ClearStaticEvent(typeof(KvTheme), "Changed");

            GameObject host = NewHost("shell");
            var ui = host.AddComponent<KazistovVvUIManager>();

            // `Awake`/`BuildShell` в EditMode дёргают `Destroy(LayoutElement)` (KvDockPanel,
            // «HeaderButton»): Unity пишет ОШИБКУ лога, которую NUnit читает как провал теста.
            // Подавляем ТОЛЬКО на время сборки — сами проверки идут уже без подавления.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                if (KazistovVvUIManager.Instance != ui)
                    InvokeNonPublic(ui, "Awake", "менеджер интерфейса");
                InvokeNonPublic(ui, "BuildShell", "оболочка интерфейса");

                // СЛУЖБЫ ЖИВУТ В `Awake`, А В EDITMODE ЕГО НЕ БЫВАЕТ (приём волны 3):
                // команды, чьи обработчики зовут сервисы хабов (`hub.Sessions`, `hub.Undo`,
                // пульт, сценарии, журнал), падали бы на NullReference вместо работы.
                // `KvStageHub4` НЕ поднимаем: его Awake заводит веб-дашборд и службу
                // компаньона (сеть запрещена границами прогона) — команды хаба обязаны
                // честно отказывать с причиной, что и проверяется.
                WakeUp<FeatureHub>(host, "FeatureHub");
                WakeUp<KvStageHub>(host, "KvStageHub");
                WakeUp<KvStageHub3>(host, "KvStageHub3");
                // `KvStageHub2` поднимаем ТОЖЕ: его Awake строит окно «Верстак», без которого
                // 19 листьев-вкладок («калибровка», «материалы», «силы», «титры»…) отвечают
                // «окно „Верстак“ в сцене нет». Ошибка `Destroy` при сборке окна в EditMode
                // гасится подавлением этого блока — волна 3 поэтому хаб и не поднимала.
                WakeUp<KvStageHub2>(host, "KvStageHub2");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoreLogsWas;
            }

            Assert.IsNotNull(ui.Tree, "после BuildShell в дереве нет компонента KvTreeView");
            Assert.IsNotNull(ui.Menu, "после BuildShell нет строки меню KvMenuBar");
            Assert.IsNotNull(ui.Toolbar, "после BuildShell нет тулбара KvToolbar");
            Assert.IsNotNull(ui.StatusBar, "после BuildShell нет статус-бара KvStatusBar");
            Assert.IsNotNull(ui.Palette, "после BuildShell нет палитры команд");
            return ui;
        }

        /// <summary>ВЫЗЫВАТЬ ПРИВАТНЫЙ МЕТОД ОТРАЖЕНИЕМ (в EditMode штатные Unity-события не идут).</summary>
        private static void InvokeNonPublic(object target, string method, string label)
        {
            MethodInfo info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(info, "у " + label + " больше нет метода " + method + " — проверка потеряла смысл");
            try { info.Invoke(target, null); }
            catch (TargetInvocationException error)
            {
                Assert.Fail(method + " (" + label + ") упал: " + error.InnerException);
            }
        }

        /// <summary>ВЫЗЫВАТЬ `Awake` ХАБА ЯВНО (в EditMode Unity его не вызывает — приём волны 3).</summary>
        private static void WakeUp<T>(GameObject host, string label) where T : Component
        {
            T hub = host.GetComponent<T>();
            if (hub == null) return;    // хаба нет — его команды зарегистрированы не были
            MethodInfo awake = typeof(T).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (awake == null) return;
            try { awake.Invoke(hub, null); }
            catch (TargetInvocationException error)
            {
                Assert.Fail("Awake хаба " + label + " упал: " + error.InnerException);
            }
        }

        /// <summary>
        /// СБРОСИТЬ СТАТИЧЕСКОЕ СОБЫТИЕ. Нужен для `KvTheme.Changed`: двойная подписка из-за
        /// повторного `Awake` переживает уничтожение одного подписчика и будит мёртвый объект.
        /// </summary>
        private static void ClearStaticEvent(Type type, string name)
        {
            FieldInfo field = type.GetField(name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return;
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) return;
            field.SetValue(null, null);
        }

        /// <summary>ПОТОМКОВЫЙ ЭЛЕМЕНТ ПО ИМЕНИ (любая глубина — элементы оболочки вложены).</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>ПРЯМОЙ ПОТОМКОВ ПО ИМЕНИ (без разбора имени как пути — заголовки содержат «/»).</summary>
        private static Transform FindChild(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        /// <summary>
        /// ПОСЛЕДНИЙ ПОТОМОК ПО ИМЕНИ. Перестройка в EditMode зовёт отложенный `Destroy`:
        /// старые строки ещё лежат в списке, и ПЕРВОЕ совпадение было бы строкой ПРОШЛОГО
        /// списка (её `onClick` замыкает чужую группу). Свежая строка всегда позже.
        /// </summary>
        private static Transform FindLastChild(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform found = null;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) found = parent.GetChild(i);
            return found;
        }

        /// <summary>ВЫПОЛНИТЬ ДЕЙСТВИЕ ПОД УЗКИМ ПОДАВЛЕНИЕМ ОШИБОК ЛОГА (перестройки зовут Destroy).</summary>
        private static void Quietly(Action action)
        {
            bool was = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try { action(); }
            finally { LogAssert.ignoreFailingMessages = was; }
        }

        /// <summary>НОВЫЕ ЗАПИСИ ЖУРНАЛА ПОСЛЕ `from` (нумерация от начала журнала).</summary>
        private static List<string> LogSince(int from)
        {
            var lines = new List<string>();
            IReadOnlyList<KvLogEntry> entries = KvActionLog.Instance.Entries;
            for (int i = from; i < entries.Count; i++) lines.Add(entries[i].text);
            return lines;
        }

        private static int LogCount() { return KvActionLog.Instance.Entries.Count; }

        private static bool AnyContains(List<string> lines, string needle)
        {
            foreach (string line in lines)
                if (line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>ПОЛЕ КОМПОНЕНТА (в т.ч. private) — для проверки внутреннего состояния.</summary>
        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(target);
        }

        /// <summary>ПРОВЕРИТЬ ПОЛЕ-ФЛАГ (bool) у объекта.</summary>
        private static bool GetBool(object target, string name)
        {
            object value = GetField(target, name);
            Assert.IsNotNull(value, "у " + target.GetType().Name + " нет поля " + name);
            return (bool)value;
        }

        // ==================== 1. СТРОКА МЕНЮ: 90 ЛИСТЬЕВ (СЛАЙДЫ 7–31)

        /// <summary>Листья «меню»: путь = `MenuPath` команды в реестре (см. KvMenuBar.BuildDropdown).</summary>
        private static readonly string[] MenuLeaves =
        {
            "Файл/Главное меню",
            "Файл/Голос диктора",
            "Файл/Импорт модели робота",
            "Файл/Импорт модели робота/Обновить список",
            "Файл/Импорт сценария",
            "Файл/Отчёт PDF",
            "Файл/Отчёт PDF/Сформировать",
            "Файл/Папка данных",
            "Файл/Сессия/Загрузить последнюю сессию",
            "Файл/Сессия/Сохранить сессию",
            "Файл/Сессия/Список сессий",
            "Файл/Экспорт в язык робота",
            "Файл/Экспорт в язык робота (вкладка)",
            "Файл/Экспорт демонстрации/Запись видео",
            "Файл/Экспорт демонстрации/Подпись на скриншоте",
            "Файл/Экспорт демонстрации/Скриншот",
            "Файл/Экспорт демонстрации/Язык робота",
            "Файл/Экспорт журнала",
            "Правка/Вернуть",
            "Правка/Отменить",
            "Правка/Сброс сценария",
            "Вид/Графика",
            "Вид/Графика/Бенчмарк 5 секунд",
            "Вид/Графика/Метрики",
            "Вид/Графика/Определить по железу",
            "Вид/Графика/Сбросить по умолчанию",
            "Вид/Динамические препятствия/Проверить варианты",
            "Вид/Журнал: уровни и поиск",
            "Вид/Журнал: уровни и поиск/Выгрузить",
            "Вид/Зоны запрета/Список зон",
            "Вид/Камеры и картинка в картинке",
            "Вид/Кинорежим",
            "Вид/Материалы",
            "Вид/Окружение/Следующий пресет",
            "Вид/Освещение/Следующий пресет",
            "Вид/Палитра команд",
            "Вид/Панели/Панель ETA",
            "Вид/Панели/Сбросить раскладку",
            "Вид/Панель функций",
            "Вид/Презентационный режим",
            "Вид/Силы и моменты",
            "Вид/Тема",
            "Вид/Тепловая карта времени",
            "Вид/Титры и подписи",
            "Робот/Модель робота/6-осевой робот (stand)",
            "Робот/Модель робота/SCARA LS10-B702S",
            "Робот/ПУСК с проверкой",
            "Робот/Пересчитать кинематику SCARA",
            "Робот/Проверка перед пуском",
            "Робот/Проверка перед пуском/Учитывать манекены",
            "Робот/Производительность/Дерево RRT",
            "Робот/Производительность/Коллизионные прокси",
            "Робот/Производительность/Прокси (переключатель)",
            "Робот/Производительность/Стенд планировщиков",
            "Робот/Производительность/Стенд планировщиков/Запустить",
            "Робот/Промежуточные точки/Удалить",
            "Сервис/Веб-дашборд",
            "Сервис/Виртуальный пульт",
            "Сервис/Виртуальный пульт/Вариант A / B",
            "Сервис/Виртуальный пульт/Закрепление",
            "Сервис/Виртуальный пульт/Режим джойстика",
            "Сервис/Голосовые команды",
            "Сервис/Демонстрации/Pick-and-place",
            "Сервис/Дерево поведения",
            "Сервис/Журнал действий",
            "Сервис/Имитация отказов",
            "Сервис/Имитация отказов/Отказ сустава",
            "Сервис/Имитация отказов/Потеря связи",
            "Сервис/Имитация отказов/Сброс аварии",
            "Сервис/Инструменты/Измерение: сбросить",
            "Сервис/Калибровочный мастер",
            "Сервис/Калькулятор нагрузки",
            "Сервис/Макросы",
            "Сервис/Мобильный пульт",
            "Сервис/Настройки",
            "Сервис/Настройки/Виртуальный геймпад",
            "Сервис/Настройки/Доступность",
            "Сервис/Настройки/Язык интерфейса",
            "Сервис/Отслеживание взгляда",
            "Сервис/Отслеживание рук",
            "Сервис/Показать демо",
            "Сервис/Производительность планировщика",
            "Сервис/Сеть и мониторинг",
            "Сервис/Сценарии",
            "Сервис/Сценарии/Запустить",
            "Справка/Горячие клавиши",
            "Справка/О программе",
            "Справка/Обучение",
            "Справка/Обучение заново",
            "Справка/Помощь"
        };

        /// <summary>
        /// ЛИСТ-ПУТЬ, КОТОРЫЙ В РЕЕСТРЕ ЗАПИСАН ИНАЧЕ: модели роботов регистрируются с
        /// `MenuPath = "Робот/Модель робота/" + <заголовок каталога>`, а лист называет модель
        /// так, как она записана в путеводителе («6-осевой робот (stand)»). Сопоставление —
        /// по id команды: пункт в меню всё равно строится из реестра, проверяется он по заголовку.
        /// </summary>
        private static readonly Dictionary<string, string> MenuPathByCommandId =
            new Dictionary<string, string>
            {
                { "Робот/Модель робота/6-осевой робот (stand)", "robot.model.stand_6axis" },
                { "Робот/Модель робота/SCARA LS10-B702S", "robot.model.scara_ls10b702s" }
            };

        /// <summary>НАЙТИ КОМАНДУ ПО ЕЁ `MenuPath` (путь листа — это и есть путь в меню).</summary>
        private static KvCommand FindByMenuPath(string path)
        {
            foreach (KvCommand command in KvCommands.All)
                if (command.MenuPath == path) return command;
            string id;
            if (MenuPathByCommandId.TryGetValue(path, out id)) return KvCommands.Get(id);
            return null;
        }

        /// <summary>
        /// ЛИСТЫ 7–31 «Файл/…, Правка/…, Вид/…, Робот/…, Сервис/…, Справка/…» (90 листьев):
        /// пункт строки меню СУЩЕСТВУЕТ, его доступность равна условию команды, а нажатие его же
        /// `onClick` доходит до команды — судится по строке `нажатие: <id>`, которую пишет сам
        /// `KvCommands.Invoke` после выполнения; недоступный пункт выключен и НЕ доходит до
        /// обработчика (иначе «серая» кнопка делала бы работу).
        ///
        /// ОТКУДА СМЫСЛ. `KvMenuBar.BuildDropdown` строит список ИЗ реестра по `MenuPath`, то
        /// есть маршрут «лист путеводителя → пункт меню → команда» — это ровно то, что лист и
        /// обещает. Сломанная маршрутизация выглядит для оператора как «нажал — тишина».
        /// </summary>
        [Test]
        public void W5_MenuBar_EveryLeafItem_RoutesToItsOwnCommand()
        {
            KazistovVvUIManager ui = BuildShell();
            KvMenuBar bar = ui.Menu;

            var failures = new List<string>();
            int pressed = 0;
            int disabled = 0;

            foreach (string path in MenuLeaves)
            {
                KvCommand command = FindByMenuPath(path);
                if (command == null)
                {
                    failures.Add(path + ": в реестре нет команды с таким MenuPath — пункта в меню нет");
                    continue;
                }

                string menu = path.Split('/')[0];
                Quietly(() => bar.ToggleMenu(menu));
                Assert.IsTrue(bar.AnyOpen, "меню «" + menu + "» не открылось для листа " + path);

                Transform dropdown = FindDeep(ui.transform, "KvMenuDropdown");
                if (dropdown == null)
                {
                    failures.Add(path + ": выпадающий список не построился");
                    continue;
                }

                Transform row = FindChild(dropdown, "Item_" + command.LocalizedTitle);
                if (row == null)
                {
                    failures.Add(path + ": пункта «" + command.LocalizedTitle + "» нет в меню «" + menu + "»");
                    continue;
                }

                Button button = row.GetComponent<Button>();
                if (button == null)
                {
                    failures.Add(path + ": пункт «" + command.LocalizedTitle + "» не кнопка");
                    continue;
                }

                bool enabled = command.Enabled;
                if (button.interactable != enabled)
                {
                    failures.Add(path + ": кнопка " + (button.interactable ? "включена" : "выключена") +
                                 ", а условие команды " + (enabled ? "выполнено" : "не выполнено"));
                    continue;
                }

                int before = LogCount();
                try
                {
                    Quietly(() => button.onClick.Invoke());
                }
                catch (Exception error)
                {
                    failures.Add(path + " (" + command.Id + "): нажатие упало: " + error.Message);
                    continue;
                }

                List<string> lines = LogSince(before);
                string trace = lines.Count > 0 ? string.Join(" | ", lines.ToArray()) : "журнал пуст";
                if (!AnyContains(lines, "нажатие: " + command.Id))
                {
                    failures.Add(path + " (" + command.Id + "): нажатие не дошло до команды: " + trace);
                    continue;
                }
                if (enabled)
                {
                    pressed++;
                    if (AnyContains(lines, "результат: без обработчика"))
                        failures.Add(path + " (" + command.Id + "): команда без обработчика — пункт меню " +
                                     "не делает ничего: " + trace);
                }
                else
                {
                    disabled++;
                    if (AnyContains(lines, "результат: выполнена"))
                        failures.Add(path + " (" + command.Id + "): недоступный пункт всё-таки выполнился: " + trace);
                    else if (!AnyContains(lines, "недоступна"))
                        failures.Add(path + " (" + command.Id + "): отказ без причины в журнале: " + trace);
                }

                // ЛИСТ «Сервис/Показать демо» (ДЕФЕКТ ВОЛНЫ 5): нажатие обязано НЕ ПАДАТЬ и
                // назвать демонстрацию в журнале — раньше, до готовности планировщика, оно
                // падало IndexOutOfRange на пустом списке шагов (KvQuickStart.Begin).
                if (command.Id == "demo.quick" && !AnyContains(lines, "демонстрация"))
                    failures.Add("demo.quick: нажатие не назвало демонстрацию в журнале: " + trace);

                // СЛЕД КАЖДОГО ЛИСТА В ЛОГ ПРОГОНА: по нему разбирают, что именно сделало
                // нажатие (выполнено / недоступно / какой отказ), — факты для статуса в CSV.
                Debug.Log("[W5OUT] " + path + " | " + command.Id + " | " + trace);

                Quietly(bar.CloseMenu);
            }

            Assert.IsTrue(failures.Count == 0,
                "пункты строки меню не все ведут к своим командам (" + failures.Count + " шт):\n" +
                string.Join("\n", failures.ToArray()));
            Assert.AreEqual(MenuLeaves.Length, pressed + disabled,
                "проверено не все листья меню — остальные не нашли пункта");
            Assert.Greater(pressed, 60,
                "доступных пунктов всего " + pressed + " — проверка не про 90 листьев меню");
        }

        // ==================== 2. ТУЛБАР: 51 ЛИСТ «KvToolbar / <группа> / Cmd_<id>» (СЛАЙДЫ 33–56)

        /// <summary>Листья тулбара: id команды из пути `KvToolbar / <группа> / Cmd_<id>`.</summary>
        private static readonly string[] ToolbarCmdLeaves =
        {
            "edit.reset", "robot.model.stand_6axis", "robot.model.scara_ls10b702s",
            "waypoint.delete", "tool.measure.reset", "shot.screenshot", "video.toggle",
            "export.robot", "import.tab", "session.save", "session.load", "log.export",
            "session.panel", "file.export", "shot.annotate", "export.lang", "export.tab",
            "import.scan", "file.import", "scenarios.open", "scenarios.run", "pendant.toggle",
            "pendant.variant", "pendant.attach", "pendant.jogmode", "gamepad.hud", "view.theme",
            "lang.cycle", "ui.settings", "ui.palette", "help.hotkeys", "ui.resetlayout",
            "features.open", "startmenu.show", "tut.toggle", "help.open", "tut.restart",
            "help.about", "ui.access", "eta.toggle", "presentation.toggle", "log.open",
            "demo.quick", "obstacle.evaluate", "zone.panel", "perf.open", "demo.pickplace",
            "edit.undo", "edit.redo", "calib.tab", "payload.tab"
        };

        /// <summary>
        /// ЛИСТЫ 33–56 «KvToolbar / <группа> / Cmd_&lt;id&gt;» (51 лист): кнопка собранного
        /// тулбара существует, её доступность равна условию команды, нажатие её же `onClick`
        /// оставляет в журнале `нажатие: <id>` (или честный отказ «недоступна» с причиной).
        /// </summary>
        [Test]
        public void W5_Toolbar_EveryLeafButton_RoutesToItsOwnCommand()
        {
            KazistovVvUIManager ui = BuildShell();
            Quietly(ui.Toolbar.ExpandAllGroups);

            Transform toolbar = FindDeep(ui.transform, "KvToolbar");
            Assert.IsNotNull(toolbar, "в оболочке нет строки KvToolbar");
            Transform groups = FindChild(toolbar, "Groups");
            Assert.IsNotNull(groups, "в тулбаре нет блока Groups с кнопками Cmd_*");

            var failures = new List<string>();
            int pressed = 0;
            int disabled = 0;

            foreach (string id in ToolbarCmdLeaves)
            {
                KvCommand command = KvCommands.Get(id);
                if (command == null)
                {
                    failures.Add(id + ": команды нет в реестре — кнопки в тулбаре не будет");
                    continue;
                }

                Transform buttonTransform = FindChild(groups, "Cmd_" + id);
                if (buttonTransform == null)
                {
                    failures.Add(id + ": кнопки Cmd_" + id + " нет в тулбаре — лист без кнопки");
                    continue;
                }

                Button button = buttonTransform.GetComponent<Button>();
                if (button == null)
                {
                    failures.Add(id + ": у Cmd_" + id + " нет onClick");
                    continue;
                }

                bool enabled = command.Enabled;
                if (button.interactable != enabled)
                {
                    failures.Add(id + ": кнопка " + (button.interactable ? "включена" : "выключена") +
                                 ", а условие " + (enabled ? "выполнено" : "не выполнено"));
                    continue;
                }

                int before = LogCount();
                try { Quietly(() => button.onClick.Invoke()); }
                catch (Exception error)
                {
                    failures.Add(id + ": нажатие упало: " + error.Message);
                    continue;
                }

                List<string> lines = LogSince(before);
                string trace = lines.Count > 0 ? string.Join(" | ", lines.ToArray()) : "журнал пуст";
                if (!AnyContains(lines, "нажатие: " + id))
                {
                    failures.Add(id + ": нажатие не дошло до команды: " + trace);
                    continue;
                }
                if (enabled)
                {
                    pressed++;
                    if (AnyContains(lines, "результат: без обработчика"))
                        failures.Add(id + ": команда без обработчика — кнопка не делает ничего: " + trace);
                }
                else
                {
                    disabled++;
                    if (AnyContains(lines, "результат: выполнена"))
                        failures.Add(id + ": недоступная кнопка всё-таки выполнилась: " + trace);
                    else if (!AnyContains(lines, "недоступна"))
                        failures.Add(id + ": отказ без причины в журнале: " + trace);
                }
            }

            Assert.IsTrue(failures.Count == 0,
                "кнопки тулбара не все ведут к своим командам (" + failures.Count + " шт):\n" +
                string.Join("\n", failures.ToArray()));
            Assert.AreEqual(ToolbarCmdLeaves.Length, pressed + disabled,
                "проверено не все листья тулбара");
        }

        // ==================== 3. РУЧКИ И СПИСКИ ГРУПП: 12 ЛИСТЬЕВ (СЛАЙДЫ 58–59)

        /// <summary>Ручки групп (слайд 8,4 листа): id группы из пути `KvToolbar / Group_<id> — …`.</summary>
        private static readonly string[] HandleLeaves = { "export", "net", "ui", "misc" };

        /// <summary>Списки групп (слайд 59, 8 листьев): id группы из пути `KvGroupMenu / … (Group_<id>)`.</summary>
        private static readonly string[] GroupMenuLeaves =
            { "robot", "path", "post", "view", "export", "net", "ui", "misc" };

        /// <summary>
        /// ЛИСТЫ 58/59. Ручка `Group_&lt;id&gt;` открывает СВОЙ список (`RowsGroupId`), подсказка
        /// называет группу и число команд, в списке — строка на каждую зарегистрированную команду
        /// (`GM_&lt;id&gt;`) плюс действие «Свернуть группу в иконку», а его нажатие переключает
        /// `IsGroupCollapsed` (флаг в `KvSettings`) — ровно то, что обещают листья.
        ///
        /// ЧИСЛО КОМАНД. Листья называют число на момент написания (17/7/13/12 и 21/13/17/22);
        /// с тех пор каталог вырос (THOR в «Роботе», `ar.place.marker` в «Прочем»), поэтому
        /// проверяется НЕ МЕНЬШЕ обещанного и — главное — строка на КАЖДУЮ команду реестра.
        /// </summary>
        [Test]
        public void W5_GroupHandlesAndMenus_EveryCommandGetsItsRow()
        {
            KazistovVvUIManager ui = BuildShell();
            KvToolbar toolbar = ui.Toolbar;
            Quietly(toolbar.ExpandAllGroups);

            Transform toolbarTransform = FindDeep(ui.transform, "KvToolbar");
            Assert.IsNotNull(toolbarTransform, "в оболочке нет строки KvToolbar");

            var failures = new List<string>();

            // --- РУЧКИ (слайд 58): подсказка + открытие списка под кнопкой.
            var promised = new Dictionary<string, int>
            {
                { "export", 17 }, { "net", 7 }, { "ui", 13 }, { "misc", 12 }
            };
            foreach (string id in HandleLeaves)
            {
                KvToolbarGroup group = KvToolbarGroups.Find(id);
                Assert.IsNotNull(group, "группы «" + id + "» нет в каталоге");

                Transform handle = FindDeep(toolbarTransform, "Group_" + id);
                if (handle == null)
                {
                    failures.Add("Group_" + id + ": ручки нет в тулбаре — лист 58 без кнопки");
                    continue;
                }
                KvIconButton icon = handle.GetComponent<KvIconButton>();
                if (icon == null || icon.Button == null)
                {
                    failures.Add("Group_" + id + ": у ручки нет кнопки/подсказки");
                    continue;
                }
                Assert.IsNotNull(icon.Tooltip, "Group_" + id + ": у ручки нет подсказки");
                StringAssert.Contains("Группа «" + group.Title + "»", icon.Tooltip.title,
                    "Group_" + id + ": подсказка не называет группу: «" + icon.Tooltip.title + "»");
                StringAssert.Contains("команд: ", icon.Tooltip.body,
                    "Group_" + id + ": подсказка не считает команды: «" + icon.Tooltip.body + "»");
                int promisedCount = promised[id];
                KvToolbarGroup declared = KvToolbarGroups.Find(id);
                Assert.GreaterOrEqual(declared.Commands.Length, promisedCount,
                    "группа «" + id + "» стала меньше, чем обещает лист (" + promisedCount + ")");

                int before = LogCount();
                Quietly(() => icon.Button.onClick.Invoke());
                Assert.IsTrue(toolbar.GroupMenuOpen,
                    "Group_" + id + ": ручка не открыла список группы");
                Assert.AreEqual(id, toolbar.RowsGroupId,
                    "Group_" + id + ": открыт список чужой группы «" + toolbar.RowsGroupId + "»");

                Transform menuPanel = FindDeep(ui.transform, "KvToolbarGroupLayer");
                Assert.IsNotNull(menuPanel, "панели списка группы нет в канвасе");
                menuPanel = menuPanel.Find("KvGroupMenu");
                Assert.IsNotNull(menuPanel, "панели KvGroupMenu нет в слое списка");
                // СТРОКА НА КАЖДУЮ КОМАНДУ: строку считаем по точным именам (`GM_<id>`),
                // а не по префиксу — иначе в счёт попадает и `GM_Action` (действие сворачивания),
                // и отложенно уничтоженные строки прошлого списка (Destroy в EditMode отложен).
                int registered = 0;
                foreach (string commandId in group.Commands)
                {
                    if (KvCommands.Get(commandId) == null) continue;
                    registered++;
                    Assert.IsNotNull(FindChild(menuPanel, "GM_" + commandId),
                        "Group_" + id + ": в списке нет строки команды " + commandId);
                }
                Assert.Greater(registered, 0,
                    "Group_" + id + ": в группе нет ни одной зарегистрированной команды");
                Assert.IsNotNull(FindChild(menuPanel, "GM_Action"),
                    "Group_" + id + ": в списке нет действия «Свернуть группу в иконку»");
                Quietly(toolbar.CloseGroupMenu);
            }
            Assert.IsTrue(failures.Count == 0,
                "ручки групп не все открывают свои списки (" + failures.Count + " шт):\n" +
                string.Join("\n", failures.ToArray()));

            // --- СПИСКИ ГРУПП (слайд 59): сворачивание через GM_Action.
            foreach (string id in GroupMenuLeaves)
            {
                KvToolbarGroup group = KvToolbarGroups.Find(id);
                Assert.IsNotNull(group, "группы «" + id + "» нет в каталоге");

                bool wasCollapsed = toolbar.IsGroupCollapsed(id);
                try
                {
                    Assert.IsTrue(QuietlyOpen(toolbar, id),
                        "GroupMenu_" + id + ": список группы не открылся — лист 59 без пункта");

                    Transform menuPanel = FindDeep(ui.transform, "KvToolbarGroupLayer");
                    Assert.IsNotNull(menuPanel, "панели списка группы нет в канвасе");
                    menuPanel = menuPanel.Find("KvGroupMenu");
                    Assert.IsNotNull(menuPanel, "панели KvGroupMenu нет в слое списка");

                    foreach (string commandId in group.Commands)
                    {
                        if (KvCommands.Get(commandId) == null) continue;
                        Assert.IsNotNull(FindChild(menuPanel, "GM_" + commandId),
                            "GroupMenu_" + id + ": в списке нет строки команды " + commandId);
                    }
                    Assert.IsNotNull(FindChild(menuPanel, "GM_Action"),
                        "GroupMenu_" + id + ": нет действия сворачивания");
                    Assert.IsNotNull(FindChild(menuPanel, "GroupTitle"),
                        "GroupMenu_" + id + ": нет заголовка списка");

                    Transform action = FindLastChild(menuPanel, "GM_Action");
                    Assert.IsNotNull(action, "GroupMenu_" + id + ": нет действия сворачивания (свежего)");
                    Button actionButton = action.GetComponent<Button>();
                    Assert.IsNotNull(actionButton, "GroupMenu_" + id + ": «Свернуть» без onClick");

                    int before = LogCount();
                    Quietly(() => actionButton.onClick.Invoke());
                    Assert.AreNotEqual(wasCollapsed, toolbar.IsGroupCollapsed(id),
                        "GroupMenu_" + id + ": нажатие «Свернуть группу в иконку» не изменило флаг " +
                        "(KvSettings/IsGroupCollapsed)");
                    Assert.IsFalse(toolbar.GroupMenuOpen,
                        "GroupMenu_" + id + ": список не закрылся после сворачивания");
                }
                finally
                {
                    QuietlySetCollapsed(toolbar, id, wasCollapsed);
                    Quietly(toolbar.CloseGroupMenu);
                }
            }
        }

        /// <summary>ОТКРЫТЬ СПИСОК ГРУППЫ без ошибок лога (перестройка строк зовёт `Destroy`).</summary>
        private static bool QuietlyOpen(KvToolbar toolbar, string id)
        {
            bool result = false;
            Quietly(() => result = toolbar.EnsureGroupMenuOpen(id));
            return result;
        }

        /// <summary>ВЕРНУТЬ СВЁРНУТОСТЬ ГРУППЫ (значение из `KvSettings`) без ошибок лога.</summary>
        private static void QuietlySetCollapsed(KvToolbar toolbar, string id, bool collapsed)
        {
            Quietly(() => toolbar.SetGroupCollapsed(id, collapsed));
        }

        // ==================== 4. ВЕРСТАК ТУЛБАРА: 2 ЛИСТА (СЛАЙД 58)

        /// <summary>
        /// ЛИСТЫ «KvToolbar / Workbench/Workbench_0 — «Робот (6 осей)»» и «Workbench_1 — «SCARA»»:
        /// сегмент — обычный `Button`, нажатие делает активным робота СВОЕГО типа
        /// (`SelectRobotByWorkbench`), прежний гаснет. Имя элемента в собранной оболочке —
        /// `Seg_Workbench/Seg0|Seg1` (в путеводителе — `Workbench_0|1`): сверяется СУЩНОСТЬ
        /// листа — выбор робота, а не имя строки отчёта.
        /// </summary>
        [Test]
        public void W5_WorkbenchSegments_ActivateRobotOfTheirOwnType()
        {
            KazistovVvUIManager ui = BuildShell();

            GameObject rigHost = NewHost("robots");
            RobotController sixAxis = rigHost.AddComponent<RobotController>();
            GameObject scaraObject = NewHost("scara");
            SCARAController scara = scaraObject.AddComponent<SCARAController>();
            sixAxis.isActive = false;
            scara.isActive = false;

            Transform workbench = FindDeep(ui.transform, "Seg_Workbench");
            Assert.IsNotNull(workbench, "в тулбаре нет строки «Верстак» — листья Workbench_0/1 не проверить");
            Transform seg0 = workbench.Find("Seg0");
            Transform seg1 = workbench.Find("Seg1");
            Assert.IsNotNull(seg0, "сегмент Seg0 «Робот (6 осей)» не построен");
            Assert.IsNotNull(seg1, "сегмент Seg1 «SCARA» не построен");

            Button b0 = seg0.GetComponent<Button>();
            Button b1 = seg1.GetComponent<Button>();
            Assert.IsNotNull(b0, "Seg0 не кнопка");
            Assert.IsNotNull(b1, "Seg1 не кнопка");

            Quietly(() => b0.onClick.Invoke());
            Assert.IsTrue(sixAxis.isActive, "Workbench_0 не сделал активным робота «6 осей»");
            Assert.IsFalse(scara.isActive, "Workbench_0 оставил активной SCARA — выбор должен быть однозначным");

            Quietly(() => b1.onClick.Invoke());
            Assert.IsTrue(scara.isActive, "Workbench_1 не сделал активным SCARA");
            Assert.IsFalse(sixAxis.isActive, "Workbench_1 не погасил робота «6 осей»");
        }

        // ==================== 5. ДЕРЕВО: ExpandAll/CollapseAll (СЛАЙД 61, 2 ЛИСТА)

        /// <summary>
        /// ЛИСТЫ 61 «KvTreeView / ExpandAll («+»)» и «CollapseAll («−»)»: кнопки собранного
        /// дерева раскрывают ВСЕ узлы с детьми (и пересобирают строки — `onExpansionChanged`),
        /// «−» очищает раскрытие и дерево схлопывается до корней. Повторные нажатия на уже
        /// раскрытом/свёрнутом дереве законно ничего не меняют — геттеры `ExpandedCount` и
        /// `RowCount` и отличают это от «кнопка молчит».
        /// </summary>
        [Test]
        public void W5_TreeExpandAllAndCollapseAll_RebuildRows()
        {
            // ДЕРЕВО СТРОИТСЯ САМО, а не из оболочки: у оболочки `onExpansionChanged` ведёт в
            // `RebuildTree()` по МОДЕЛИ СЦЕНЫ, и синтетический узел теста был бы выброшен из
            // проверки сразу после первого нажатия. Здесь событие считает сам тест — оно и есть
            // след «пересборки строк», который обещает лист.
            var canvasGo = NewHost("TreeCanvas");
            var canvasRect = canvasGo.AddComponent<RectTransform>();
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);

            GameObject host = NewHost("tree");
            KvTreeView tree = host.AddComponent<KvTreeView>();
            var model = new List<ProjectNode>();
            ProjectNode modelRoot = null;
            int expansions = 0;
            // `onExpansionChanged` делает РОВНО то, что делает оболочка (лист 61: «вызывает
            // onExpansionChanged → RebuildTree(true)»): пересобирает строки по той же модели.
            Quietly(() => tree.Build(canvasRect, canvas, null, null, null,
                delegate { expansions++; tree.Rebuild(model, modelRoot); }));

            modelRoot = new ProjectNode("w5:root", "Корень волны 5", ProjectNodeKind.Group, null, null);
            var child = new ProjectNode("w5:child", "Ребёнок волны 5", ProjectNodeKind.Point, null, null);
            modelRoot.Children.Add(child);
            model.Add(modelRoot);
            Quietly(() => tree.Rebuild(model, modelRoot));
            expansions = 0;

            Assert.AreEqual(1, tree.RowCount, "свёрнутое дерево не показало корень");
            Assert.AreEqual(0, tree.ExpandedCount, "дерево раскрыто до нажатия «+»");
            Assert.AreEqual(1, tree.ExpandableCount, "в модели нет узла с детьми — проверять нечего");

            Transform expandButton = FindDeep(canvasRect, "ExpandAll");
            Assert.IsNotNull(expandButton, "кнопки «+» (ExpandAll) в дереве нет — лист 61 без кнопки");
            Button expand = expandButton.GetComponent<Button>();
            Assert.IsNotNull(expand, "ExpandAll не кнопка");

            Quietly(() => expand.onClick.Invoke());
            Assert.AreEqual(1, tree.ExpandedCount,
                "«+» не раскрыл узлы с детьми: раскрыто " + tree.ExpandedCount +
                " из " + tree.ExpandableCount);
            Assert.IsTrue(tree.IsExpanded(modelRoot.Key), "«+» не раскрыл корень по его ключу");
            Assert.AreEqual(2, tree.RowCount, "после «+» строка ребёнка не появилась — пересборки не было");
            Assert.AreEqual(1, expansions,
                "«+» не вызвал onExpansionChanged — строки не пересобирались (обещание листа)");

            Transform collapseButton = FindDeep(canvasRect, "CollapseAll");
            Assert.IsNotNull(collapseButton, "кнопки «−» (CollapseAll) в дереве нет — лист 61 без кнопки");
            Button collapse = collapseButton.GetComponent<Button>();
            Assert.IsNotNull(collapse, "CollapseAll не кнопка");

            Quietly(() => collapse.onClick.Invoke());
            Assert.AreEqual(0, tree.ExpandedCount, "«−» не очистил раскрытие");
            Assert.AreEqual(1, tree.RowCount, "после «−» дерево не схлопнулось до корней");
            Assert.AreEqual(2, expansions,
                "«−» не вызвал onExpansionChanged — строки не пересобирались (обещание листа)");

            // ПОВТОР НА УЖЕ СДЕЛАННОМ: законно ничего не меняется (геттеры для самопрогона).
            Quietly(() => collapse.onClick.Invoke());
            Assert.AreEqual(0, tree.ExpandedCount, "повторный «−» изменил пустое раскрытие");
            Quietly(() => expand.onClick.Invoke());
            Assert.AreEqual(tree.ExpandableCount, tree.ExpandedCount,
                "повторный «+» не раскрыл всё дерево");
            Assert.AreEqual(2, tree.RowCount, "повторный «+» не вернул строку ребёнка");
        }

        // ==================== 6. СТАТУС-БАР: ИНДИКАТОР РЕЖИМА КУРСОРА (СЛАЙД 64, 1 ЛИСТ)

        /// <summary>
        /// ЛИСТ 64 «Статус-бар / CursorMode — индикатор режима курсора (кнопка)»: клик
        /// переключает режим «Камера ↔ UI» (`KvMouseCursor.Toggle` — то же, что Tab), текст
        /// индикатора показывает ФАКТ, при смене включается подсветка на 0,5 с, а подсказка
        /// называет «Tab — переключить режим».
        /// </summary>
        [Test]
        public void W5_StatusBarCursorMode_ClickTogglesModeWithTooltip()
        {
            KazistovVvUIManager ui = BuildShell();
            KvStatusBar status = ui.StatusBar;

            Transform indicator = FindDeep(ui.transform, "CursorMode");
            Assert.IsNotNull(indicator, "индикатора CursorMode в статус-баре нет — лист 64 без кнопки");
            Button button = indicator.GetComponent<Button>();
            Assert.IsNotNull(button, "CursorMode не кнопка — лист называет его кнопкой");

            KvTooltipTarget tip = indicator.GetComponent<KvTooltipTarget>();
            Assert.IsNotNull(tip, "у индикатора нет подсказки");
            StringAssert.Contains("Tab — переключить режим", tip.title,
                "подсказка не называет «Tab — переключить режим»: «" + tip.title + "»");

            KvCursorMode before = KvMouseCursor.CurrentMode;
            Quietly(() => button.onClick.Invoke());
            Assert.AreNotEqual(before, KvMouseCursor.CurrentMode,
                "клик по индикатору не переключил режим курсора — дубликата Tab нет");

            Assert.AreEqual(KvMouseCursor.CurrentMode, status.ShownCursorMode,
                "индикатор показывает режим, отличный от фактического");
            StringAssert.Contains("Режим", status.CursorModeText,
                "текст индикатора не называет режим: «" + status.CursorModeText + "»");

            // Подсветка 0,5 с включается событием смены режима (анимацию кадра EditMode не гоняет).
            Assert.IsTrue(GetBool(status, "blinkRunning"),
                "после смены режима не включилась подсветка индикатора (0,5 с)");

            // То же, что Tab: второе нажатие возвращает режим обратно.
            Quietly(() => button.onClick.Invoke());
            Assert.AreEqual(before, KvMouseCursor.CurrentMode,
                "второй клик не вернул прежний режим — переключение не циклично");
            Assert.AreEqual("Tab", tip.hotkey,
                "подсказка не называет Tab горячей клавишей индикатора: «" + tip.hotkey + "»");
            Assert.AreEqual(KvMouseCursor.CurrentMode, status.ShownCursorMode,
                "после второго клика индикатор разошёлся с фактическим режимом");
        }

        // ==================== 7. ПАЛИТРА КОМАНД: СТРОКА Pal_<id> (СЛАЙД 66, 1 ЛИСТ)

        /// <summary>
        /// ЛИСТ 66 «Палитра команд / Строка Pal_&lt;id&gt; (до 12)»: строка выполняет СВОЮ
        /// команду и закрывает палитру, подсвеченная строка меняется навигацией (`Move` — тот же
        /// путь, что стрелки/Tab в `Update`), Enter выполняет подсвеченную (`Execute`), Esc
        /// закрывает (`Close`). Строк в списке — не больше двенадцати.
        /// </summary>
        [Test]
        public void W5_CommandPalette_RowsExecuteTheirOwnCommand()
        {
            KazistovVvUIManager ui = BuildShell();
            KvCommandPalette palette = ui.Palette;
            Assert.IsNotNull(palette, "палитра не создана оболочкой");

            Quietly(palette.Open);
            Assert.IsTrue(palette.IsOpen, "палитра не открылась");

            // СТРОКИ: до 12, каждая — своя команда (`Pal_<id>`). СЧЁТ ПО РАЗНЫМ ИМЕНАМ:
            // `ClearRows` в EditMode зовёт отложенный `Destroy`, и старые строки ещё лежат
            // в списке до конца кадра — по childCount они выглядели бы как «36 строк».
            Transform list = FindDeep(palette.transform, "List");
            Assert.IsNotNull(list, "в палитре нет списка результатов");
            Quietly(() => palette.SetQuery(""));
            var rowNames = new HashSet<string>();
            for (int i = 0; i < list.childCount; i++)
                if (list.GetChild(i).name.StartsWith("Pal_", StringComparison.Ordinal))
                    rowNames.Add(list.GetChild(i).name);
            Assert.LessOrEqual(rowNames.Count, 12,
                "в списке палитры " + rowNames.Count + " разных строк — лист обещает «до 12»");

            // ПОИСК: берём запрос, у которого ПЕРВАЯ строка — доступная команда (после `Open`
            // подсветка всегда 0, а `Execute` честно отказывается от недоступной) и в выдаче есть
            // вторая строка — иначе навигацию не проверить.
            string[] queries = { "Тема", "Дерево", "Сетка", "Журнал", "Робот", "Панель" };
            string firstId = null;
            string usedQuery = null;
            foreach (string query in queries)
            {
                Quietly(() => palette.SetQuery(query));
                if (palette.ResultCount < 2) continue;
                Transform first = FreshFirstPalRow(list, palette);
                if (first == null) continue;
                KvCommand firstCommand = KvCommands.Get(first.name.Substring("Pal_".Length));
                if (firstCommand == null || !firstCommand.Enabled) continue;
                firstId = firstCommand.Id;
                usedQuery = query;
                break;
            }
            Assert.IsNotNull(firstId,
                "не нашёлся запрос палитры с доступной первой строкой и второй строкой выдачи");
            Assert.GreaterOrEqual(palette.ResultCount, 2, "выдача «" + usedQuery + "» меньше двух строк");

            Transform row = LastPalRow(list, firstId);
            Assert.IsNotNull(row, "строка Pal_" + firstId + " не построена");
            Button rowButton = row.GetComponent<Button>();
            Assert.IsNotNull(rowButton, "строка палитры не кнопка");

            int before = LogCount();
            Quietly(() => rowButton.onClick.Invoke());
            List<string> lines = LogSince(before);
            Assert.IsTrue(AnyContains(lines, "нажатие: " + firstId),
                "строка палитры не дошла до команды " + firstId + ": " +
                (lines.Count > 0 ? string.Join(" | ", lines.ToArray()) : "журнал пуст"));
            Assert.IsFalse(palette.IsOpen, "после выполнения палитра не закрылась");

            // НАВИГАЦИЯ: `Move` двигает подсветку (тот же путь, что стрелки/Tab в `Update`).
            Quietly(palette.Open);
            Quietly(() => palette.SetQuery(usedQuery));
            Assert.AreEqual(0, palette.Highlight, "после открытия подсветка не на первой строке");
            int highlightBefore = palette.Highlight;
            Quietly(() => palette.Move(1));
            Assert.AreNotEqual(highlightBefore, palette.Highlight,
                "навигация не подвинула подсветку — стрелки/Tab в палитре не работают");
            Quietly(() => palette.Move(-1));
            Assert.AreEqual(highlightBefore, palette.Highlight,
                "навигация назад не вернула подсветку на место");

            // ENTER: `Execute` выполняет подсвеченную команду (первая строка — доступная).
            before = LogCount();
            bool executed = false;
            Quietly(() => executed = palette.Execute());
            Assert.IsTrue(executed, "Execute не выполнил подсвеченную команду");
            lines = LogSince(before);
            Assert.IsTrue(AnyContains(lines, "нажатие: " + firstId),
                "после Enter в журнале нет строки нажатия " + firstId + ": " +
                string.Join(" | ", lines.ToArray()));

            // ESC: ветка закрытия (`Close`) гасит палитру.
            Quietly(palette.Open);
            Assert.IsTrue(palette.IsOpen, "повторное открытие не сработало");
            Quietly(palette.Close);
            Assert.IsFalse(palette.IsOpen, "Close не закрыл палитру — ветка Esc не работает");
        }

        /// <summary>
        /// ПЕРВАЯ СТРОКА СВЕЖЕЙ ВЫДАЧИ. `ClearRows` в EditMode зовёт отложенный `Destroy`,
        /// поэтому старые строки ещё лежат в списке: свежий блок всегда в КОНЦЕ и содержит
        /// ровно `min(ResultCount, MaxRows)` элементов.
        /// </summary>
        private static Transform FreshFirstPalRow(Transform list, KvCommandPalette palette)
        {
            int shown = Mathf.Min(palette.ResultCount, 12);
            if (shown <= 0) return null;
            int start = list.childCount - shown;
            if (start < 0) return null;
            for (int i = start; i < list.childCount; i++)
                if (list.GetChild(i).name.StartsWith("Pal_", StringComparison.Ordinal))
                    return list.GetChild(i);
            return null;
        }

        /// <summary>ПОСЛЕДНЯЯ (свежая) СТРОКА `Pal_&lt;id&gt;` — прежние отложенно уничтожены.</summary>
        private static Transform LastPalRow(Transform list, string id)
        {
            Transform found = null;
            for (int i = 0; i < list.childCount; i++)
                if (list.GetChild(i).name == "Pal_" + id) found = list.GetChild(i);
            return found;
        }

        // ==================== 8. СЛУЖЕБНЫЕ КНОПКИ: «Подробнее» И ВЫБОР РОБОТА (СЛАЙД 76, 2 ЛИСТА)

        /// <summary>
        /// ЛИСТЫ 76 «Прочие кнопки / More «Подробнее» (плашка ошибки)» — разворачивает текст
        /// стека ошибок (`Application.logMessageReceived` → `KvUiStates.ReportError`), и
        /// «Btn_SixAxis «6-осевой (1)»» — выбирает тип робота при размещении (панель
        /// `CenterWindow.ShowRobotChooser`).
        /// </summary>
        [Test]
        public void W5_ErrorPlateMoreAndRobotChooser_DoTheirJob()
        {
            KazistovVvUIManager ui = BuildShell();

            // --- «Подробнее»: плашка ошибки с деталями разворачивается КНОПКОЙ, а не молчит.
            Assert.IsNotNull(KvUiStates.Current, "плашка ошибки не создана оболочкой");
            KvUiStates.ReportError("Ошибка волны 5", "стек ошибки\nвторая строка стека");
            Assert.IsTrue(KvUiStates.Current.ErrorVisible, "плашка ошибки не показалась");
            Assert.IsFalse(KvUiStates.Current.DetailsOpen,
                "детали уже развёрнуты до нажатия — проверять нечего");

            Transform more = FindDeep(ui.transform, "More");
            Assert.IsNotNull(more, "кнопки «Подробнее» на плашке ошибки нет — лист 76 без кнопки");
            Button moreButton = more.GetComponent<Button>();
            Assert.IsNotNull(moreButton, "«Подробнее» не кнопка");
            Assert.IsTrue(moreButton.interactable, "«Подробнее» выключена у плашки с деталями");

            Quietly(() => moreButton.onClick.Invoke());
            Assert.IsTrue(KvUiStates.Current.DetailsOpen,
                "«Подробнее» не развернул текст стека ошибки");
            KvUiStates.Current.HideError();
            Assert.IsFalse(KvUiStates.Current.ErrorVisible, "плашка ошибки не закрылась");

            // --- Btn_SixAxis: панель выбора открывается и кнопка отдаёт ИНДЕКС типа робота.
            FieldInfo centerField = typeof(KazistovVvUIManager).GetField("centerWindow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(centerField, "в менеджере нет centerWindow — выборщик не проверить");
            var center = (CenterWindow)centerField.GetValue(ui);
            Assert.IsNotNull(center, "CenterWindow не создан при сборке оболочки");

            int picked = -1;
            center.ShowRobotChooser(delegate(int index) { picked = index; });
            Assert.IsTrue(center.RobotChooserOpen, "панель выбора робота не открылась");

            Transform chooser = FindDeep(ui.transform, "RobotChooser");
            Assert.IsNotNull(chooser, "панели RobotChooser нет в оболочке");
            Transform sixAxis = FindChild(chooser, "Btn_SixAxis");
            Assert.IsNotNull(sixAxis, "кнопки Btn_SixAxis в панели нет — лист 76 без кнопки");
            Button sixButton = sixAxis.GetComponent<Button>();
            Assert.IsNotNull(sixButton, "Btn_SixAxis не кнопка");

            Quietly(() => sixButton.onClick.Invoke());
            Assert.AreEqual(0, picked,
                "Btn_SixAxis не выбрал тип робота 0 («6-осевой (1)»), а выбрал " + picked);
            center.HideRobotChooser();
            Assert.IsFalse(center.RobotChooserOpen,
                "после выбора панель выбора робота не закрылась");
        }
    }
}
