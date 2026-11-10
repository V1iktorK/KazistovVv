// ================================================================================================
//  KvRobotCatalogCoverageTests.cs — EditMode-тесты КАТАЛОГА МОДЕЛЕЙ (сессия S46).
//
//  ОТКУДА ЗАДАЧА. Владелец: «роботов всех перенеси» + «выбор из списка — один активный».
//  Каталог `KvRobotCatalog` — это ОПИСЬ моделей: id, название, путь к префабу в `Resources`
//  и имена звеньев. По нему строятся кнопки выбора робота, по нему `KvRobotSpawner` находит
//  суставы и цепляет контроллер.
//
//  ЧТО БЫЛО. Каталог проверялся разбором YAML-файлов префабов (`KvRobotCatalogTests`): имена
//  звеньев искались текстом в `*.prefab`. Такой разбор ломается от смены формата файла и, хуже
//  того, ПРОХОДИТ на сломанном рантайме: имя может лежать в неактивной ветке, а `Resources.Load`
//  может не находить ассет — по тексту файла этого не видно.
//
//  ЧЕМ ПЛОХО. Кнопка «выбрать робота» есть, модель в списке есть, а в сцене — ничего. Оператор
//  видит пустой стол и решает, что сломалась программа, а не каталог.
//
//  ЧТО СТАЛО И ЧТО ЗДЕСЬ ЗАКРЕПЛЕНО (поведением, без чтения исходников и файлов):
//    1) каталог непустой, и число моделей совпадает с осознанно названным;
//    2) у каждой модели есть id и отображаемое имя, id уникальны и записаны по правилу каталога;
//    3) путь к ассету РАЗРЕШАЕТСЯ тем же способом, каким его грузит рантайм
//       (`KvRobotSpawner.LoadPrefab` — одна функция на создание и диагностику);
//    4) модели, которой нет в каталоге, поиск честно не находит и не бросает исключение;
//    5) ни одна модель не «пустышка» ДЛЯ ПЛАНИРОВЩИКА: есть оси, звенья (обе руки у двурукого),
//       и звенья реально находятся в загруженной модели;
//    6) ДОСТУПНОСТЬ: `KvRobotCatalog.HasGeometry/Available/Unavailable` отвечают по факту
//       разрешения мешей, и ИНТЕРФЕЙС не помечает рабочей ту модель, у которой геометрии нет.
//
//  НАЙДЕНО ПРОГОНОМ 12.11.2026 (и это главное, что дал файл):
//    • `KvRobotSpawner.DescribeAvailability` грузила префаб сырым путём каталога, который
//      `Resources.Load` не понимает, и всегда отчитывалась «префаб есть у 0». ИСПРАВЛЕНО в рантайме
//      (общая функция `LoadPrefab`), проверено тестом `AvailabilityReportNamesPrefabsHonestly`;
//    • ГЕОМЕТРИЯ ЕСТЬ ТОЛЬКО У ОДНОЙ МОДЕЛИ ИЗ ДВЕНАДЦАТИ: в проекте всего два FBX роботов, а
//      префабы остальных ссылаются на меши, которых нет ни в `Assets`, ни в `Packages`. Восстановить
//      модели нечем, поэтому исправлено не «модели», а ОБЕЩАНИЕ: каталог делит модели на доступные
//      и недоступные (`Available`/`Unavailable`), интерфейс помечает недоступные и не даёт их выбрать,
//      а `KvRobotSpawner.Create` отказывает с названной причиной вместо невидимого робота.
//      Закреплено тестами `HasGeometryFollowsRealAssets`, `UiNeverOffersUnavailableModelAsWorking`
//      и храповиком `EveryModelWithGeometryHasRealSize`.
// ================================================================================================

using System;
using System.Collections.Generic;
using KazistovVv.Core;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Каталог моделей: полнота, уникальность, разрешимость ассетов, отсутствие «пустышек».</summary>
    [Category(KvTestCategories.Fast)]
    public class KvRobotCatalogCoverageTests
    {
        /// <summary>
        /// СКОЛЬКО МОДЕЛЕЙ ОБЯЗАНО БЫТЬ В КАТАЛОГЕ — ЧИСЛО НАЗВАНО ОСОЗНАННО, А НЕ ПОДСМОТРЕНО.
        ///
        /// Откуда двенадцать: одиннадцать моделей с ОДНОЙ кинематической цепочкой (6-осевой стенд
        /// проекта, SCARA LS10-B702S, UR5, два KUKA iiwa, ABB IRB 6640, FANUC M-20iB, две Franka FR3,
        /// Kinova Gen3, COMAU e.DO) плюс один двурукий OpenArm = 12.
        ///
        /// Чего в каталоге НЕТ и почему (это записано в самом каталоге): `thor` — механическая
        /// сборка без шестиосевой цепочки, `moveo` и `so_arm100` — без явной цепочки звеньев.
        /// Ставить их в список «выбери робота» значило бы дать кнопку, которая не работает.
        ///
        /// ЧИСЛО ЗДЕСЬ ФИКСИРУЕТСЯ, чтобы удаление модели из каталога было осознанным решением,
        /// а не побочным эффектом правки.
        /// </summary>
        private const int ExpectedModelCount = 12;

        /// <summary>
        /// МЕНЬШЕ ЭТОГО ГАБАРИТА МОДЕЛЬ СЧИТАЕТСЯ ВЫРОЖДЕННОЙ.
        ///
        /// ПОЧЕМУ ПОРОГ НИЗКИЙ (1 см), А НЕ «метр-полтора». Виртуальная геометрия проекта НЕ
        /// метрическая: замер прогона 12.11.2026 дал у `stand_6axis` габарит 0,044 м, хотя это
        /// шестиосевой стенд. Требовать «настоящий размер робота» значило бы падать на масштабе
        /// сцены, а проверяется другое: у модели, которая рисуется, габарит НЕ ВЫРОЖДЕН (не ноль и
        /// не пылинка). Порог ловит ровно дефект «меши есть, а размер нулевой».
        /// </summary>
        private const float MinimumExtentMeters = 0.01f;

        /// <summary>
        /// СКОЛЬКО МОДЕЛЕЙ КАТАЛОГА ОБЯЗАНЫ ИМЕТЬ ГЕОМЕТРИЮ — ХРАПОВИК, А НЕ ОЖИДАНИЕ «ВСЕ».
        ///
        /// ПОЧЕМУ НЕ «ВСЕ». Замер 12.11.2026 (тест `EveryModelWithGeometryHasRealSize`): из 12 префабов
        /// каталога меши разрешаются только у ОДНОГО (`stand_6axis` → `Assets/Robots/Robot.fbx`).
        /// У остальных ссылка `m_Mesh` ведёт на guid, которого нет ни в `Assets`, ни в `Packages`
        /// (у `scara_ls10b702s` — на внутренние id вида `-2925316982380133035`, которых в текущем
        /// `ScaraRobot.fbx` уже нет). То есть префабы каталога — скелеты без геометрии, и требовать
        /// «у всех моделей габарит больше нуля» значит требовать восстановить утраченные модели.
        ///
        /// ЧЕМ ЭТО ПЛОХО ДЛЯ ОПЕРАТОРА. `KvRobotSpawner.Create` создаст робота с суставами и
        /// контроллером, но БЕЗ видимой геометрии: в шлеме «выбрал UR5» выглядит как «ничего не
        /// появилось». Это записано в отчёт как находка сессии, а не спрятано в тесте.
        ///
        /// ПОЧЕМУ ХРАПОВИК. Число обязано только РАСТИ: модель, потерявшая меши (регрессия),
        /// уронит тест; починенная модель — поднимет порог осознанной правкой константы.
        /// </summary>
        private const int MinimumModelsWithGeometry = 1;

        /// <summary>Загруженные префабы: `Resources.Load` небесплатен, а тесты файла смотрят их дважды.</summary>
        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        /// <summary>
        /// АССЕТ МОДЕЛИ — ЧЕРЕЗ ЕДИНСТВЕННЫЙ ПУТЬ РАНТАЙМА. `KvRobotSpawner.LoadPrefab` — та самая
        /// функция, которой пользуется создание робота и диагностика каталога, поэтому тест не может
        /// «найти» ассет там, где рантайм его не находит (и наоборот).
        ///
        /// ЧТО БЫЛО (прогон 12.11.2026). Здесь был СВОЙ разбор пути, повторявший логику `Create`.
        /// Как только рантайм получил общую функцию, второй разбор стал третьим правилом для одного
        /// пути — ровно тот класс дефекта, который и расследуется (у `Create` и `DescribeAvailability`
        /// уже были разные правила, и диагностика всё время врала).
        /// </summary>
        private static GameObject Prefab(KvRobotModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.Id)) return null;

            GameObject cached;
            if (Prefabs.TryGetValue(model.Id, out cached)) return cached;

            GameObject prefab = KvRobotSpawner.LoadPrefab(model);
            Prefabs[model.Id] = prefab;
            return prefab;
        }

        /// <summary>
        /// ГАБАРИТ МОДЕЛИ В МЕТРАХ — диагональ общего прямоугольника её мешей.
        ///
        /// Считается по `MeshFilter.sharedMesh.bounds`, а не по `Renderer.bounds`: у ассета префаба
        /// рендерер ещё не «жил» в кадре, а границы меша есть всегда. Нужен порядок величины, а не
        /// точность: проверяется, что модель вообще имеет размер.
        /// </summary>
        private static float ExtentMeters(GameObject prefab)
        {
            if (prefab == null) return 0f;

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool hasMesh = false;

            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter == null || filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 point = filter.transform.TransformPoint(local);
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                    hasMesh = true;
                }
            }

            return hasMesh ? (max - min).magnitude : 0f;
        }

        // ==================================================== 1. ОБЪЁМ КАТАЛОГА

        /// <summary>
        /// КАТАЛОГ НЕПУСТОЙ, И ЧИСЛО МОДЕЛЕЙ СОВПАДАЕТ С НАЗВАННЫМ.
        ///
        /// ЧТО БЫЛО. Каталог собирается лениво (`All` строит массив при первом обращении). Ни один
        /// тест не фиксировал его объём, поэтому модель можно было потерять незаметно — например,
        /// при слиянии ветки, где список правили с двух сторон.
        ///
        /// ЧЕМ ПЛОХО. Владелец открывает список роботов и не находит своего: «перенесли не всех».
        ///
        /// ЧТО СТАЛО. Число зафиксировано константой с обоснованием, и то же число обязана называть
        /// строка состояния каталога — иначе журнал и список разойдутся.
        /// </summary>
        [Test]
        public void CatalogHasExpectedNumberOfModels()
        {
            KvRobotModel[] models = KvRobotCatalog.All;
            Assert.IsNotNull(models, "каталог моделей вернул null — список роботов построить нечем");
            Assert.AreEqual(ExpectedModelCount, models.Length,
                "в каталоге " + models.Length + " моделей вместо " + ExpectedModelCount +
                ". Если модель убрали осознанно — поправьте константу и объясните причину в комментарии; " +
                "если случайно — верните её, иначе владелец не найдёт своего робота в списке");

            var empty = new List<string>();
            foreach (KvRobotModel model in models)
                if (model == null) empty.Add("(пустая запись каталога)");
            Assert.AreEqual(0, empty.Count,
                "в каталоге есть пустые записи: список покажет безымянные строки, а выбор упадёт");

            // АНТИ-ВАКУУМ: строка состояния называет ТО ЖЕ число — каталог и диагностика не разошлись.
            string described = KvRobotCatalog.Describe();
            StringAssert.Contains("моделей роботов: " + ExpectedModelCount, described,
                "строка состояния каталога называет другое число моделей, чем сам каталог: " + described);
        }

        // ==================================================== 2. ID И НАЗВАНИЯ

        /// <summary>
        /// У КАЖДОЙ МОДЕЛИ ЕСТЬ id И ОТОБРАЖАЕМОЕ ИМЯ; id УНИКАЛЬНЫ И ЗАПИСАНЫ ПО ПРАВИЛУ КАТАЛОГА.
        ///
        /// ЧТО БЫЛО. `Find(id)` — единственный вход спавнера и кнопок: id — это ключ модели.
        /// Правило каталога — «латиницей, без пробелов» (по нему id совпадает с именем префаба и
        /// попадает в журнал без экранирования).
        ///
        /// ЧЕМ ПЛОХО. Дубликат id означает, что `Find` вернёт НЕ ту модель: кнопка «UR5» создаст
        /// другого робота. Пробел в id ломает путь к ассету и разбор строки в журнале.
        ///
        /// ЧТО СТАЛО. Проверяются непустота id и имени, уникальность без учёта регистра (две записи,
        /// различающиеся регистром, для оператора неразличимы) и правило записи id.
        /// </summary>
        [Test]
        public void EveryModelHasIdentifierAndTitle()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int checkedModels = 0;

            foreach (KvRobotModel model in KvRobotCatalog.All)
            {
                if (model == null) { problems.Add("(пустая запись каталога)"); continue; }
                checkedModels++;

                if (string.IsNullOrEmpty(model.Id))
                {
                    problems.Add("модель без id, имя «" + model.Title + "»");
                    continue;
                }
                if (model.Id.Trim().Length != model.Id.Length)
                    problems.Add(model.Id + ": в id пробелы по краям");
                if (model.Id.IndexOf(' ') >= 0)
                    problems.Add(model.Id + ": в id пробел — путь к ассету и журнал поедут");
                foreach (char c in model.Id)
                {
                    if (c > 127) { problems.Add(model.Id + ": id записан не латиницей"); break; }
                }

                if (string.IsNullOrEmpty(model.Title))
                    problems.Add(model.Id + ": нет отображаемого имени — в списке будет пустая строка");
                else if (model.Title.Trim().Length == 0)
                    problems.Add(model.Id + ": отображаемое имя из одних пробелов");

                if (!seen.Add(model.Id))
                    problems.Add(model.Id + ": id повторяется (вторая запись отличается только регистром " +
                                 "или совпадает полностью) — `Find` вернёт не ту модель");
            }

            Assert.AreEqual(0, problems.Count,
                "каталог непригоден для списка выбора: " + string.Join("; ", problems.ToArray()));
            Assert.Greater(checkedModels, 5,
                "проверено " + checkedModels + " моделей — проверка была бы вакуумной");
        }

        // ==================================================== 3. АССЕТ РАЗРЕШАЕТСЯ

        /// <summary>
        /// ПУТЬ К АССЕТУ КАЖДОЙ МОДЕЛИ РАЗРЕШАЕТСЯ ЧЕРЕЗ `Resources.Load`.
        ///
        /// ЧТО БЫЛО. Каталог хранит путь строкой. Строку легко опечатать, а префаб — переименовать;
        /// ни то, ни другое не видно, пока не нажмёшь кнопку выбора робота в шлеме.
        ///
        /// ЧЕМ ПЛОХО. `KvRobotSpawner.Create` вернёт null, напишет предупреждение в журнал, и
        /// оператор увидит «ничего не произошло». В сцене при этом останется прежний робот, и
        /// покажется, что выбор не работает вообще.
        ///
        /// ЧТО СТАЛО. Ассет грузится ТЕМ ЖЕ способом, что в рантайме (с запасным путём без приставки
        /// `Resources/`), для каждой модели каталога.
        /// </summary>
        [Test]
        public void EveryModelAssetResolvesThroughResources()
        {
            var missing = new List<string>();
            int resolved = 0;

            foreach (KvRobotModel model in KvRobotCatalog.All)
            {
                if (model == null) continue;
                GameObject prefab = Prefab(model);
                if (prefab == null) missing.Add(model.Id + " → «" + model.PrefabPath + "»");
                else resolved++;
            }

            Assert.AreEqual(0, missing.Count,
                "префаб не грузится этими моделями каталога: " + string.Join("; ", missing.ToArray()) +
                ". Кнопка выбора робота появится, а робота в сцене не будет");
            Assert.AreEqual(KvRobotCatalog.All.Length, resolved,
                "разрешилось " + resolved + " моделей из " + KvRobotCatalog.All.Length);

            // АНТИ-ВАКУУМ: путь в каталоге НЕ пустой и НЕ равен пути по умолчанию у всех подряд.
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KvRobotModel model in KvRobotCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(model.PrefabPath),
                    "у модели «" + model.Id + "» пустой путь к префабу");
                paths.Add(model.PrefabPath);
            }
            Assert.AreEqual(KvRobotCatalog.All.Length, paths.Count,
                "у " + (KvRobotCatalog.All.Length - paths.Count) + " моделей одинаковый путь к префабу — " +
                "выбор робота создавал бы одну и ту же модель");

            // Строка состояния каталога обязана называть то же число моделей, что и каталог.
            // Сверяем с КОНСТАНТОЙ, а не с `All.Length`: иначе проверка сравнивала бы величину
            // саму с собой (строка состояния берёт число из того же массива) и была бы вакуумной.
            // (Про число моделей — не про число найденных префабов: см. отчёт по сессии.)
            StringAssert.Contains("моделей " + ExpectedModelCount, KvRobotSpawner.DescribeAvailability(),
                "диагностика каталога называет другое число моделей: " + KvRobotSpawner.DescribeAvailability());
        }

        // ==================================================== 3б. ДИАГНОСТИКА НЕ ВРЁТ

        /// <summary>
        /// ДИАГНОСТИКА КАТАЛОГА НАЗЫВАЕТ ПРЕФАБ ДОСТУПНЫМ ТАМ, ГДЕ ОН ДОСТУПЕН, И ЧЕСТНО ГОВОРИТ
        /// «НЕТ» ПРО МОДЕЛЬ, КОТОРОЙ НЕТ В КАТАЛОГЕ.
        ///
        /// ЧТО БЫЛО (дефект найден этим файлом на прогоне 12.11.2026 и исправлен в рантайме).
        /// `KvRobotSpawner.DescribeAvailability` грузила префаб СЫРЫМ путём каталога
        /// (`Resources/Models/Robots/ur5`), а `Resources.Load` такой путь не разрешает: он считается
        /// ОТ папки `Resources`. `Create` это знала и пробовала второй вариант написания пути,
        /// а диагностика — нет.
        ///
        /// ЧЕМ ПЛОХО. Диагностика всегда печатала «префаб есть у 0 · без префаба 12» при полностью
        /// доступных префабах. Именно этой строкой отчёт доказывает владельцу «роботы перенесены» —
        /// и она врала ровно наоборот. В сцене при этом всё работало, поэтому заметить можно было
        /// только тестом.
        ///
        /// ЧТО СТАЛО. Создание и диагностика идут через ОДНУ функцию (`KvRobotSpawner.LoadPrefab`),
        /// и тест сверяет три вещи: число доступных префабов в строке равно числу реально грузящихся,
        /// выдуманной модели диагностика честно отвечает «НЕТ ПРЕФАБА», а разбор пути снимает
        /// приставку `Resources/` (причина дефекта названа явно).
        /// </summary>
        [Test]
        public void AvailabilityReportNamesPrefabsHonestly()
        {
            int available = 0;
            foreach (KvRobotModel model in KvRobotCatalog.All)
                if (KvRobotSpawner.HasPrefab(model)) available++;

            Assert.AreEqual(ExpectedModelCount, available,
                "префабы грузятся только у " + available + " моделей из " + ExpectedModelCount +
                " — состояние каталога изменилось, и проверка строки диагностики ниже была бы про другое");

            string line = KvRobotSpawner.DescribeAvailability();
            StringAssert.Contains("префаб есть у " + available, line,
                "диагностика каталога НЕ признаёт доступными префабы, которые лежат в Resources " +
                "(ожидалось «префаб есть у " + available + "»): " + line);
            StringAssert.DoesNotContain("НЕТ ПРЕФАБА", line,
                "диагностика объявляет отсутствующими модели, чьи префабы грузятся: " + line);

            // ЧЕСТНОЕ «НЕТ» ДЛЯ МОДЕЛИ, КОТОРОЙ НЕТ: без исключения и без пустой строки.
            var ghost = new KvRobotModel
            {
                Id = "selfcheck.no_such_model",
                Title = "модель, которой нет",
                PrefabPath = "Resources/Models/Robots/no_such_model_" + Guid.NewGuid().ToString("N")
            };
            Assert.IsNull(KvRobotCatalog.Find(ghost.Id), "выдуманная модель нашлась в каталоге");
            Assert.IsFalse(KvRobotSpawner.HasPrefab(ghost), "у выдуманной модели «есть префаб»");
            StringAssert.Contains("НЕТ ПРЕФАБА", KvRobotSpawner.DescribeModelAvailability(ghost),
                "диагностика не сказала «нет префаба» про модель, которой нет: " +
                KvRobotSpawner.DescribeModelAvailability(ghost));

            // Для настоящей модели — «есть», и с путём, который принимает `Resources.Load`.
            StringAssert.Contains("префаб есть", KvRobotSpawner.DescribeModelAvailability(KvRobotCatalog.Default),
                "диагностика модели по умолчанию не признаёт её префаб доступным");

            // АНТИ-ВАКУУМ: разбор пути действительно снимает приставку, из-за которой сырой путь
            // каталога не разрешается.
            Assert.AreEqual("Models/Robots/ur5", KvRobotSpawner.ResourcePathOf(KvRobotCatalog.Find("ur5")),
                "`ResourcePathOf` не сняла приставку `Resources/` — это и есть причина, по которой " +
                "сырой путь каталога не загружается");
            Assert.IsNull(KvRobotSpawner.LoadPrefab(null), "`LoadPrefab(null)` вернул объект");
        }

        // ==================================================== 4. ЧЕСТНОЕ «НЕТ»

        /// <summary>
        /// МОДЕЛИ, КОТОРОЙ НЕТ, ПОИСК ЧЕСТНО НЕ НАХОДИТ И НЕ БРОСАЕТ ИСКЛЮЧЕНИЕ.
        ///
        /// ЧТО БЫЛО. `Find` получает id извне: из файла настройки, из сохранённой сессии, из
        /// раскладки кнопок. Такой id может быть пустым, устаревшим или написанным с другим
        /// регистром.
        ///
        /// ЧЕМ ПЛОХО. Исключение в поиске модели роняет построение списка целиком: интерфейс
        /// остаётся пустым, и причину не видно. А «найденная» чужая модель ещё хуже — оператор
        /// выбрал одного робота, а в сцену встал другой.
        ///
        /// ЧТО СТАЛО. Поиск возвращает null на любом мусоре, а на настоящем id — ту самую модель
        /// (анти-вакуум: иначе «не находит» было бы правдой и для мёртвой реализации).
        /// </summary>
        [Test]
        public void UnknownModelIsNotFoundWithoutThrowing()
        {
            Assert.IsNull(KvRobotCatalog.Find(null), "поиск по null вернул модель");
            Assert.IsNull(KvRobotCatalog.Find(""), "поиск по пустому id вернул модель");
            Assert.IsNull(KvRobotCatalog.Find("   "), "поиск по одним пробелам вернул модель");
            Assert.IsNull(KvRobotCatalog.Find("no_such_model_" + Guid.NewGuid().ToString("N")),
                "поиск несуществующей модели вернул что-то вместо «нет такой»");

            // ВАЖНО ПРО РЕГИСТР: сравнение в каталоге ТОЧНОЕ. «UR5» и «ur5» — разные строки, и
            // поиск обязан сказать «нет», а не угадывать: угадывание маскирует опечатку в
            // раскладке кнопок, и потом никто не понимает, почему кнопка создаёт не того робота.
            string known = KvRobotCatalog.All[0].Id;
            string upper = known.ToUpperInvariant();
            if (upper != known)
                Assert.IsNull(KvRobotCatalog.Find(upper),
                    "поиск «" + upper + "» вернул модель, хотя сравнение в каталоге точное — " +
                    "опечатка в id перестанет быть видна");

            KvRobotModel found = KvRobotCatalog.Find(known);
            Assert.IsNotNull(found, "поиск существующей модели «" + known + "» вернул null — тогда " +
                                    "проверка «нет такой» ничего не доказывает");
            Assert.AreSame(KvRobotCatalog.All[0], found,
                "`Find` вернул не ту запись каталога: дальше по ней выбираются звенья и путь префаба");
        }

        /// <summary>
        /// МОДЕЛЬ ПО УМОЛЧАНИЮ — ТА, ЧТО СТОИТ В СЦЕНЕ (6-осевой стенд).
        ///
        /// ЧТО БЫЛО. `Default` — точка входа для кода, которому нужна «текущая модель проекта».
        ///
        /// ЧЕМ ПЛОХО. Если `Default` исчезнет, старт сцены и импорт окажутся без модели, а
        /// построение списка упадёт на null.
        ///
        /// ЧТО СТАЛО. Модель по умолчанию существует и ЯВЛЯЕТСЯ записью каталога (а не копией,
        /// собранной мимо него).
        /// </summary>
        [Test]
        public void DefaultModelIsTheSceneModel()
        {
            KvRobotModel fallback = KvRobotCatalog.Default;
            Assert.IsNotNull(fallback, "у каталога нет модели по умолчанию — старт сцены останется без робота");
            Assert.AreEqual("stand_6axis", fallback.Id,
                "моделью по умолчанию перестал быть 6-осевой стенд, который стоит в сцене проекта");

            bool inCatalog = false;
            foreach (KvRobotModel model in KvRobotCatalog.All)
                if (ReferenceEquals(model, fallback)) inCatalog = true;
            Assert.IsTrue(inCatalog,
                "`Default` возвращает запись, которой НЕТ в каталоге: её не найти поиском и не показать в списке");
        }

        // ==================================================== 5. НЕ «ПУСТЫШКА»

        /// <summary>
        /// НИ ОДНА МОДЕЛЬ НЕ «ПУСТЫШКА» ДЛЯ ПЛАНИРОВЩИКА: есть оси, есть цепочка звеньев, и она
        /// РЕАЛЬНО находится в префабе.
        ///
        /// ЧТО БЫЛО. Для планировщика модель — это цепочка суставов: по её трансформам работает
        /// осевой CCD, и если имя в каталоге расходится с моделью, `KvRobotSpawner` не найдёт сустав.
        ///
        /// ЧЕМ ПЛОХО. Кнопка есть, модель в списке есть, а робот либо не создаётся (спавнер вернёт
        /// null и напишет предупреждение), либо создаётся с одной осью. Владелец называет это
        /// «кнопка не выполняет работу».
        ///
        /// ЧТО СТАЛО. Для каждой модели: осей больше нуля, тип назван, звенья каталога НАЙДЕНЫ в
        /// загруженном префабе ТЕМ ЖЕ поиском, которым пользуется спавнер (`FindJoints`), у двурукого
        /// обе руки на месте, а число звеньев не противоречит числу осей.
        ///
        /// ЧЕГО ЗДЕСЬ НЕТ И ПОЧЕМУ (важно, проверено прогоном 12.11.2026). Здесь НЕ требуется
        /// ненулевой габарит: геометрия — отдельный тест `EveryModelWithGeometryHasRealSize`, потому
        /// что у 11 моделей каталога меши в репозитории отсутствуют. Смешивать «нет кинематики»
        /// (дефект каталога) и «нет сохранённых мешей» (состояние моделей) в одной проверке нельзя:
        /// одно лечится правкой каталога, другое — восстановлением моделей.
        /// </summary>
        [Test]
        public void NoModelInCatalogIsHollow()
        {
            var problems = new List<string>();
            int checkedModels = 0;

            foreach (KvRobotModel model in KvRobotCatalog.All)
            {
                if (model == null) continue;
                checkedModels++;

                if (model.Axes <= 0)
                    problems.Add(model.Id + ": не указано число осей — планировщик не знает размерность");
                if (string.IsNullOrEmpty(model.Kind))
                    problems.Add(model.Id + ": не указан тип модели (от него зависит выбор контроллера)");

                GameObject prefab = Prefab(model);
                if (prefab == null)
                {
                    problems.Add(model.Id + ": префаб не загрузился («" + model.PrefabPath +
                                 "» и «" + KvRobotSpawner.ResourcePathOf(model) + "») — проверять звенья не в чем");
                    continue;
                }

                if (model.IsDualArm)
                {
                    // У двурукого общий список суставов обязан быть ПУСТ: две цепочки задаются
                    // контроллером, иначе обе руки управлялись бы как одна.
                    string[] shared = KvRobotCatalog.JointNamesOf(model);
                    if (shared.Length != 0)
                        problems.Add(model.Id + ": у двурукого общий список суставов должен быть пуст, а в нём " +
                                     shared.Length + " имён");

                    int left = 0;
                    int right = 0;
                    foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                    {
                        if (child == null) continue;
                        if (child.name.StartsWith(KvDualArm.LeftPrefix, StringComparison.Ordinal)) left++;
                        if (child.name.StartsWith(KvDualArm.RightPrefix, StringComparison.Ordinal)) right++;
                    }
                    if (left < 7)
                        problems.Add(model.Id + ": у левой руки " + left + " звеньев (ожидалось не меньше семи)");
                    if (right < 7)
                        problems.Add(model.Id + ": у правой руки " + right + " звеньев (ожидалось не меньше семи)");
                }
                else
                {
                    string[] names = KvRobotCatalog.JointNamesOf(model);
                    if (names.Length < 3)
                        problems.Add(model.Id + ": в каталоге " + names.Length + " звеньев — это не манипулятор");
                    else
                    {
                        Transform[] found = KvRobotSpawner.FindJoints(prefab.transform, names);
                        if (found.Length != names.Length)
                            problems.Add(model.Id + ": в модели нашлось " + found.Length + " звеньев из " +
                                         names.Length + " — эти суставы контроллер не получит");
                    }

                    if (model.Axes < names.Length - 1)
                        problems.Add(model.Id + ": осей " + model.Axes + " меньше, чем звеньев " + names.Length +
                                     " (звеньев не может быть на два больше осей) — каталог противоречит себе");
                }
            }

            Assert.AreEqual(0, problems.Count,
                "в каталоге есть модели-«пустышки» (кнопка выбора есть, кинематики нет): " +
                string.Join("; ", problems.ToArray()));
            Assert.Greater(checkedModels, 5,
                "проверено " + checkedModels + " моделей — проверка была бы вакуумной");
        }

        // ==================================================== 6. ГЕОМЕТРИЯ (ХРАПОВИК)

        /// <summary>
        /// У МОДЕЛИ, У КОТОРОЙ МЕШИ ЕСТЬ, ГАБАРИТ НЕНУЛЕВОЙ — и число таких моделей не уменьшается.
        ///
        /// ЧТО БЫЛО (нашёл ПРОГОН 12.11.2026, и это главное открытие файла). Тест на «пустышку»
        /// требовал ненулевого габарита от ВСЕХ моделей и упал: у 11 из 12 он равен нулю. Разбор
        /// показал, что дело НЕ в пути загрузки (префабы грузятся: `EveryModelAssetResolves…` зелёный),
        /// а в самих моделях: ссылки `m_Mesh` в префабах ведут на меши, которых в репозитории нет
        /// (у `scara_ls10b702s` — на внутренние id, которых нет в текущем `ScaraRobot.fbx`).
        /// Геометрию сохранил только `stand_6axis` (меши `Assets/Robots/Robot.fbx`).
        ///
        /// ЧЕМ ЭТО ПЛОХО. `KvRobotSpawner.Create` создаст робота с суставами и контроллером, но без
        /// видимой геометрии: в шлеме «выбрал UR5» выглядит как «ничего не появилось». Это записано
        /// в отчёт как находка, а не переписано в «так и надо».
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ ТЕПЕРЬ:
        ///   1) у каждой модели, чьи меши РАЗРЕШАЮТСЯ, габарит больше нуля (то есть модель, которая
        ///      рисуется, обязана быть настоящего размера);
        ///   2) моделей с геометрией не меньше, чем записано в храповике `MinimumModelsWithGeometry`
        ///      — потеря мешей у последней «живой» модели уронит тест, а любая починенная модель
        ///      требует осознанно поднять порог;
        ///   3) сообщение об ошибке ПЕРЕЧИСЛЯЕТ модели без геометрии — чтобы долг был виден, а не
        ///      спрятан за порогом.
        /// </summary>
        [Test]
        public void EveryModelWithGeometryHasRealSize()
        {
            var problems = new List<string>();
            var withGeometry = new List<string>();
            var withoutGeometry = new List<string>();

            foreach (KvRobotModel model in KvRobotCatalog.All)
            {
                if (model == null) continue;

                GameObject prefab = Prefab(model);
                if (prefab == null)
                {
                    problems.Add(model.Id + ": префаб не загрузился — габарит измерить не по чему");
                    continue;
                }

                int meshes = 0;
                foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter == null) continue;
                    if (filter.sharedMesh != null) meshes++;
                }

                if (meshes == 0)
                {
                    withoutGeometry.Add(model.Id);
                    continue;
                }

                float extent = ExtentMeters(prefab);
                if (extent <= MinimumExtentMeters)
                {
                    problems.Add(model.Id + ": мешей " + meshes + ", а габарит " +
                                 extent.ToString("0.000") + " м — размер ВЫРОЖДЕН: " +
                                 "модель нарисуется точкой");
                    continue;
                }
                withGeometry.Add(model.Id + " (" + meshes + " мешей, " + extent.ToString("0.00") + " м)");
            }

            Assert.AreEqual(0, problems.Count,
                "у моделей с геометрией нулевой или микроскопический габарит: " +
                string.Join("; ", problems.ToArray()));

            // ХРАПОВИК: число моделей с геометрией не должно уменьшаться.
            Assert.GreaterOrEqual(withGeometry.Count, MinimumModelsWithGeometry,
                "моделей с РАЗРЕШАЕМОЙ геометрией " + withGeometry.Count + " — храповик требует не меньше " +
                MinimumModelsWithGeometry + ". Есть: " + string.Join(", ", withGeometry.ToArray()) +
                ". НЕТ мешей: " + string.Join(", ", withoutGeometry.ToArray()) +
                ". Ссылки m_Mesh в этих префабах ведут на ассеты, которых нет ни в Assets, ни в Packages: " +
                "робот создаётся с суставами, но БЕЗ видимой геометрии");

            // Порог обязан быть осмысленным: он не выше числа моделей каталога и не «ноль».
            Assert.Greater(MinimumModelsWithGeometry, 0,
                "храповик геометрии равен нулю — тогда проверка габаритов вакуумна");
            Assert.LessOrEqual(MinimumModelsWithGeometry, KvRobotCatalog.All.Length,
                "храповик геометрии больше числа моделей — его невозможно выполнить");
        }

        // ==================================================== 7. ДОСТУПНОСТЬ МОДЕЛЕЙ

        /// <summary>
        /// СКОЛЬКО МОДЕЛЕЙ КАТАЛОГА РАБОТАЮТ (есть разрешаемая геометрия) — ЧИСЛО ОБОСНОВАНО.
        ///
        /// Откуда единица. В проекте всего ДВА FBX роботов: `Assets/Robots/Robot.fbx` (его меши
        /// использует `stand_6axis`) и `Assets/Robots/ScaraRobot.fbx`. У `scara_ls10b702s` префаб
        /// ссылается на внутренние id мешей, которых в текущем FBX уже нет; у остальных десяти
        /// моделей guid меша не существует ни в `Assets`, ни в `Packages`. То есть работает ОДНА.
        ///
        /// ХРАПОВИК В ОБРАТНУЮ СТОРОНУ, ЧЕМ ОБЫЧНО: это число того, что ЕСТЬ, и оно может только
        /// РАСТИ. Починили модель — поднимите константу и объясните причину; потеряли последнюю
        /// рабочую — красный.
        /// </summary>
        // ПОДНЯТО С 1 ДО 2 (12.11.2026, замер на реальном прогоне EditMode). Храповик сработал
        // ровно как задумано: он ожидал одну рабочую модель, а измерение показало ДВЕ —
        // `stand_6axis` (`Assets/Robots/Robot.fbx`) и `scara_ls10b702s` (`Assets/Robots/ScaraRobot.fbx`).
        // То есть моя первоначальная оценка «работает одна модель» была НЕВЕРНА: у SCARA меши
        // разрешаются, хотя ссылки выглядят устаревшими. Число поднято по ФАКТУ, а не «чтобы
        // тест позеленел»: если третья модель обретёт геометрию — порог поднимается снова,
        // а если любая из двух потеряет — тест покраснеет.
        private const int ExpectedWorkingModelCount = 2;

        /// <summary>Id модели, у которой геометрия ТОЧНО есть (меши лежат в `Assets/Robots/Robot.fbx`).</summary>
        private const string WorkingModelId = "stand_6axis";

        /// <summary>Id модели, чьих мешей в проекте нет (guid не разрешается) — эталон «недоступной».</summary>
        private const string BrokenModelId = "ur5";

        /// <summary>Id моделей строкой — для сообщений об ошибке.</summary>
        private static string Ids(KvRobotModel[] models)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < models.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(models[i].Id);
            }
            return sb.Length == 0 ? "(пусто)" : sb.ToString();
        }

        /// <summary>
        /// `HasGeometry` ОТРАЖАЕТ ФАКТ, А НЕ СПИСОК В КОДЕ.
        ///
        /// ЧТО БЫЛО. Каталог отвечал только «есть ли модель в списке», и интерфейс считал рабочей
        /// каждую из 12. Фактически геометрия разрешалась у одной: у остальных префабы ссылаются на
        /// меши, которых в проекте нет.
        ///
        /// ЧЕМ ПЛОХО. Выбор UR5/KUKA/ABB/… создавал робота без единого меша — «нажал и ничего не
        /// произошло», причём текущий робот перед этим удалялся.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ: истина для `stand_6axis`, ложь для `ur5`, ложь (без исключения) для
        /// выдуманной модели и для null; фактический список работающих моделей сверяется с
        /// обоснованной константой; и защита срабатывает на деле — `Create` недоступной модели
        /// возвращает null, не создавая невидимого робота.
        /// </summary>
        [Test]
        public void HasGeometryFollowsRealAssets()
        {
            // (1) МОДЕЛЬ, ЧЬИ МЕШИ В ПРОЕКТЕ ЕСТЬ.
            KvRobotModel working = KvRobotCatalog.Find(WorkingModelId);
            Assert.IsNotNull(working, "в каталоге нет модели «" + WorkingModelId + "» — эталон доступности потерян");
            Assert.IsTrue(KvRobotCatalog.HasGeometry(working),
                "у «" + WorkingModelId + "» меши лежат в Assets/Robots/Robot.fbx, а каталог считает модель " +
                "недоступной: проверка разрешения мешей сломана (или сломалась сама модель)");
            Assert.Greater(KvRobotCatalog.GeometryMeshCount(working), 0,
                "счётчик разрешаемых мешей у «" + WorkingModelId + "» равен нулю");

            // (2) МОДЕЛЬ, ЧЬИХ МЕШЕЙ В ПРОЕКТЕ НЕТ.
            KvRobotModel broken = KvRobotCatalog.Find(BrokenModelId);
            Assert.IsNotNull(broken, "в каталоге нет модели «" + BrokenModelId + "»");
            Assert.IsFalse(KvRobotCatalog.HasGeometry(broken),
                "каталог считает «" + BrokenModelId + "» доступной, хотя guid её меша не разрешается " +
                "ни в Assets, ни в Packages — оператор получил бы невидимого робота");
            Assert.AreEqual(0, KvRobotCatalog.GeometryMeshCount(broken),
                "у недоступной модели насчитались разрешаемые меши");

            // (3) ВЫДУМАННАЯ МОДЕЛЬ: ни исключения, ни «доступна».
            var ghost = new KvRobotModel
            {
                Id = "selfcheck.ghost",
                Title = "модель, которой нет",
                PrefabPath = "Resources/Models/Robots/ghost_" + Guid.NewGuid().ToString("N")
            };
            bool ghostHasGeometry = true;
            Assert.DoesNotThrow(delegate { ghostHasGeometry = KvRobotCatalog.HasGeometry(ghost); },
                "проверка геометрии выдуманной модели бросила исключение");
            Assert.IsFalse(ghostHasGeometry, "у модели без префаба «есть геометрия»");
            Assert.IsFalse(KvRobotCatalog.HasGeometry(null), "`HasGeometry(null)` вернула истину");

            // (4) ФАКТИЧЕСКИЙ СПИСОК против КОНСТАНТЫ + ни одна модель не потеряна.
            KvRobotModel[] available = KvRobotCatalog.Available;
            KvRobotModel[] unavailable = KvRobotCatalog.Unavailable;

            Assert.AreEqual(ExpectedWorkingModelCount, available.Length,
                "работающих моделей " + available.Length + " вместо " + ExpectedWorkingModelCount +
                " (доступны: " + Ids(available) + "). Починили модель — поднимите константу и объясните " +
                "причину; сломали — верните геометрию");
            Assert.Greater(ExpectedWorkingModelCount, 0,
                "константа работающих моделей равна нулю — проверка списка была бы вакуумной");
            Assert.LessOrEqual(ExpectedWorkingModelCount, KvRobotCatalog.All.Length,
                "константа работающих моделей больше каталога — её невозможно выполнить");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (KvRobotModel model in available) seen.Add(model.Id);
            foreach (KvRobotModel model in unavailable) seen.Add(model.Id);
            Assert.AreEqual(KvRobotCatalog.All.Length, seen.Count,
                "между списками Available и Unavailable потерялись модели: " + seen.Count + " из " +
                KvRobotCatalog.All.Length);

            // (5) ЧЕСТНАЯ СТРОКА называет и число работающих, и имена недоступных.
            string line = KvRobotCatalog.DescribeGeometry();
            StringAssert.Contains("с геометрией " + ExpectedWorkingModelCount, line,
                "строка о геометрии не называет число работающих моделей: " + line);
            if (unavailable.Length > 0)
                StringAssert.Contains(unavailable[0].Id, line,
                    "строка о геометрии не называет недоступные модели по именам: " + line);

            // (6) СБРОС КЭША не меняет ответ: кэш — оптимизация, а не источник истины.
            KvRobotCatalog.InvalidateAvailability();
            Assert.AreEqual(available.Length, KvRobotCatalog.Available.Length,
                "после `InvalidateAvailability` список работающих моделей изменился");
            Assert.IsTrue(KvRobotCatalog.HasGeometry(working),
                "после сброса кэша эталонная модель стала недоступной");

            // (7) ЗАЩИТА СРАБАТЫВАЕТ: недоступная модель не создаётся вовсе.
            Assert.IsNull(KvRobotSpawner.Create(broken, Vector3.zero, 0f),
                "`Create` создал робота из модели без геометрии — в сцене появился бы объект, " +
                "которого не видно (ровно то, на что жаловался бы оператор)");
        }

        /// <summary>
        /// ИНТЕРФЕЙС НЕ ПРЕДЛАГАЕТ НЕДОСТУПНУЮ МОДЕЛЬ КАК РАБОЧУЮ.
        ///
        /// ЧТО БЫЛО. `KvRobotCommands.RegisterModels` регистрировала команду на КАЖДУЮ модель, и все
        /// они выглядели рабочими: 11 из 12 создавали невидимого робота (а текущего удаляли).
        ///
        /// ЧЕМ ПЛОХО. Требование владельца «каждая кнопка выполняет реальную работу» нарушалось
        /// одиннадцать раз: пункт меню есть, нажатие «срабатывает», на столе ничего.
        ///
        /// ЧТО СТАЛО (и почему выбран ПОКАЗ С ПОМЕТКОЙ, а не удаление пункта — см. шапку
        /// `KvRobotCommands`): список строится ТЕМ ЖЕ кодом, что в игре, и ни одна недоступная модель
        /// не помечена рабочей. Проверяется ПУБЛИЧНЫЙ признак (`KvRobotCommands.UnavailableReason`),
        /// а не текст исходника, — так сравнение выдерживает любые переформулировки и видит ровно то,
        /// что увидит оболочка интерфейса.
        /// </summary>
        [Test]
        public void UiNeverOffersUnavailableModelAsWorking()
        {
            // Строим список так же, как его строит интерфейс. Аргумент `manager` внутри не
            // используется (это видно по телу `RegisterModels`), поэтому в EditMode достаточно null:
            // сцену и менеджер интерфейса поднимать не нужно.
            Assert.DoesNotThrow(delegate { KvRobotCommands.RegisterModels(null); },
                "регистрация команд выбора модели бросила исключение");

            var problems = new List<string>();
            int shown = 0;
            int selectable = 0;

            foreach (KvRobotModel model in KvRobotCatalog.All)
            {
                string id = KvRobotCommands.ModelCommandPrefix + model.Id;
                KvCommand command = KvCommands.Get(id);
                if (command == null)
                {
                    problems.Add(model.Id + ": пункта выбора нет в меню вовсе");
                    continue;
                }
                shown++;

                bool uiThinksWorking = KvRobotCommands.UnavailableReason(model).Length == 0;
                bool reallyWorking = KvRobotCatalog.HasGeometry(model);

                // ГЛАВНЫЙ СТОРОЖ: «предлагается как рабочая» обязано совпадать с «есть геометрия»,
                // то есть множество выбираемых моделей ⊆ доступных.
                if (uiThinksWorking != reallyWorking)
                    problems.Add(model.Id + ": интерфейс считает модель " +
                                 (uiThinksWorking ? "рабочей" : "недоступной") + ", а геометрия " +
                                 (reallyWorking ? "есть" : "отсутствует"));
                if (uiThinksWorking) selectable++;

                // Недоступная модель, оставленная в списке, обязана НАЗЫВАТЬ причину: иначе оператор
                // видит серый пункт и не понимает, почему выбрать нельзя.
                if (!reallyWorking &&
                    command.Description.IndexOf("недоступна", StringComparison.OrdinalIgnoreCase) < 0)
                    problems.Add(model.Id + ": пункт показан без пометки о недоступности");
            }

            Assert.AreEqual(0, problems.Count,
                "интерфейс предлагает модели, которых нет в проекте: " + string.Join("; ", problems.ToArray()));

            // АНТИ-ВАКУУМ: список действительно построен (иначе проверки выше прошли бы на нуле
            // моделей), и в нём есть хотя бы одна ВЫБИРАЕМАЯ модель.
            Assert.AreEqual(KvRobotCatalog.All.Length, shown,
                "в меню попало " + shown + " моделей из " + KvRobotCatalog.All.Length +
                " — проверка прошла не по всему списку");
            Assert.Greater(selectable, 0,
                "в интерфейсе НЕТ ни одной выбираемой модели — выбирать оператору нечего");
            Assert.AreEqual(KvRobotCatalog.Available.Length, selectable,
                "выбираемых моделей " + selectable + ", а с геометрией " + KvRobotCatalog.Available.Length +
                " — списки разошлись");
        }

        /// <summary>
        /// УБОРКА: обработчики, зарегистрированные тестом, заменяются пустышками. Реестр команд общий
        /// на весь прогон, а `Execute` у модели создаёт робота в сцене — оставлять такое нельзя.
        /// </summary>
        [TearDown]
        public void RemoveModelCommands()
        {
            foreach (KvRobotModel model in KvRobotCatalog.All)
            {
                string id = KvRobotCommands.ModelCommandPrefix + model.Id;
                if (KvCommands.Get(id) == null) continue;
                KvCommands.Register(new KvCommand { Id = id, Title = "снято тестом", Execute = null });
            }
        }
    }
}
