// ================================================================================================
//  KvHotkeyConflictTests.cs — EditMode-тесты КОНФЛИКТОВ ГОРЯЧИХ КЛАВИШ (сессия S44, пункт 5.28).
//
//  ОТКУДА ЗАДАЧА. Клавиатурные обработчики разложены по шести хабам (`FeatureHub`,
//  `KvStageHub`…`KvStageHub4`, `KvStartMenu`), и КАЖДЫЙ держит СВОЙ приватный `KeyOf(...)` со
//  своим набором клавиш. Пока наборы разные — это нормально (у каждого свои действия), но никто
//  не проверял, что ОДНА клавиша не делает ДВА дела в одном кадре.
//
//  ЭТОТ КЛАСС ДЕФЕКТА В ПРОЕКТЕ УЖЕ БЫЛ: §27 — ENTER нажимал кнопку тулбара И подтверждал точку;
//  E-08 — курок выбирал лазер И телепортировал. Оба раза находил человек, а не тест.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ (по исходникам, без запуска Unity):
//    1) если ДВЕ части проекта РЕАГИРУЮТ на одну клавишу — это названо поимённо и падает;
//    2) «объявлена в KeyOf, но действия нет» — не конфликт, но и не повод считать клавишу
//       занятой: тест различает эти случаи, иначе он кричал бы на пустом месте;
//    3) список исключений — ЯВНЫЙ и с причиной у каждой строки.
//
//  ЧЕМ ЭТО ОТЛИЧАЕТСЯ ОТ «ДЛЯ ГАЛОЧКИ»: тест читает РЕАЛЬНЫЕ исходники и находит реальные
//  обработчики (`if (Down(KeyCode.X))`, `IsKeyDown(KeyCode.X)`). Если кто-то добавит в третий
//  хаб реакцию на F8, где уже снимается скриншот, — прогон покраснеет с именем файла.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Одна клавиша — одно действие: конфликты между хабами.</summary>
    public class KvHotkeyConflictTests
    {
        /// <summary>
        /// ФАЙЛЫ, КОТОРЫЕ РЕАЛЬНО ОБРАБАТЫВАЮТ КЛАВИАТУРУ. Список явный: новый хаб с клавишами
        /// обязан быть здесь, иначе его конфликты не проверятся.
        /// </summary>
        private static readonly string[] Hubs =
        {
            "FeatureHub.cs",
            "KvStageHub.cs",
            "KvStageHub2.cs",
            "KvStageHub3.cs",
            "KvStageHub4.cs",
            "KvStartMenu.cs",
        };

        /// <summary>
        /// КЛАВИШИ, КОТОРЫЕ ЗАКОННО РЕАГИРУЮТ В НЕСКОЛЬКИХ ЧАСТЯХ — с ПРИЧИНОЙ у каждой.
        ///
        /// ПУСТО — ЗНАЧИТ «НИ ОДНА». Так и должно быть: если появилась строка, её надо объяснить
        /// здесь, а не прятать в коде. Именно так выглядит честный арбитраж: не «замолчать
        /// конфликт», а «назвать и обосновать».
        /// </summary>
        private static readonly Dictionary<string, string> AllowedOverlaps = new Dictionary<string, string>
        {
            // Модификаторы — это НЕ действие, а условие: Ctrl+Z и Ctrl+Y обрабатываются одним
            // хабом, а второй лишь читает состояние Ctrl. Реакции на сам Ctrl нет.
            { "LeftControl", "модификатор: читается как условие, собственного действия не имеет" },
            { "RightControl", "модификатор: читается как условие, собственного действия не имеет" },

            // ESCAPE — ЗАКОННЫЙ ПРИОРИТЕТ ПО СОСТОЯНИЮ, а не «два действия в одном кадре».
            //
            // ДОКАЗАНО ЧТЕНИЕМ КОДА 30.10.2026, а не на слово:
            //   • `FeatureHub`: `if (Presentation != null && Presentation.Active)` — выход из
            //     презентации, и сразу `return`, то есть в этом режиме больше ничего не смотрит;
            //   • `KvStageHub2`: `if (QuickStart != null && QuickStart.Running && …)` — прервать
            //     демонстрацию, только пока она идёт;
            //   • `KvStartMenu`: `if (Down(KeyCode.Escape)) Hide()` — закрыть САМО меню, то есть
            //     срабатывает, только когда меню видно.
            //
            // ЭТИ ТРИ СОСТОЯНИЯ НЕ ПЕРЕСЕКАЮТСЯ: презентация активна — меню не обрабатывает
            // клавишу, меню скрыто — его обработчик не выполняется. Проверка «нет ли действий
            // в двух местах» видит три обработчика, но не может понять условий, поэтому здесь
            // это НАЗВАНО с причиной — ровно то, чего требует правило §27: не «замолчать
            // конфликт», а «назвать и обосновать».
            //
            // ЕСЛИ ПОЯВИТСЯ ЧЕТВЁРТЫЙ ОБРАБОТЧИК ESCAPE БЕЗ УСЛОВИЯ — этот тест его не поймает,
            // поэтому при правке ESCAPE смотреть сюда и в три обработчика выше.
            { "Escape", "законный приоритет: презентация (return) / демонстрация / видимое меню — состояния взаимоисключающие" },
        };

        // ---------------------------------------------------------------- разбор исходников

        /// <summary>Путь к файлу хаба по имени (ищется в `Assets/_Project`).</summary>
        private static string FindHub(string name)
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            foreach (string file in Directory.GetFiles(root, name, SearchOption.AllDirectories)) return file;
            return null;
        }

        /// <summary>
        /// КЛАВИШИ, НА КОТОРЫЕ ФАЙЛ ДЕЙСТВИТЕЛЬНО РЕАГИРУЕТ.
        ///
        /// Ищутся три формы, которые в проекте реально используются:
        ///   `if (Down(KeyCode.X))`, `if (IsKeyDown(KeyCode.X))`, `case KeyCode.X:` с последующим
        ///   вызовом — но ТОЛЬКО если рядом есть действие.
        ///
        /// ПОЧЕМУ НЕ ПО ОДНОМУ `case KeyCode.X`. В `KvStageHub` в таблице `KeyOf` объявлены
        /// F5–F12, но действуют он только на F8, F10, F11, F12. Считать объявленные клавиши
        /// «занятыми» значило бы кричать о конфликтах, которых нет (проверено 30.10.2026).
        /// </summary>
        private static HashSet<string> ReactingKeys(string path)
        {
            var keys = new HashSet<string>();
            string text = File.ReadAllText(path);

            // 1. Явные проверки нажатия: `Down(KeyCode.X)` / `IsKeyDown(KeyCode.X)` / `Pressed(KeyCode.X)`.
            foreach (Match m in Regex.Matches(text, @"\b(?:Down|IsKeyDown|Pressed|WasPressed)\s*\(\s*KeyCode\.(\w+)\s*\)"))
                keys.Add(m.Groups[1].Value);

            // 2. `switch (code) { case KeyCode.X: <действие> }` — таблица KeyOf. Действием считается
            //    только возврат клавиши; такие строки НЕ добавляются (см. пояснение выше).
            //    Поэтому `case` мы намеренно пропускаем: объявление ≠ реакция.

            return keys;
        }

        // ---------------------------------------------------------------- 1. КОНФЛИКТЫ

        /// <summary>
        /// НИ ОДНА КЛАВИША НЕ ДЕЛАЕТ ДВА ДЕЛА В ОДНОМ КАДРЕ.
        ///
        /// Если этот тест упал — у вас ДВА обработчика на одну клавишу. Это ровно класс дефекта
        /// §27 (ENTER делал два действия) и E-08 (курок делал два действия). Лечение: либо
        /// развести клавиши, либо ввести арбитраж (как сделано для ENTER в §27) и вписать
        /// исключение в `AllowedOverlaps` с причиной.
        /// </summary>
        [Test]
        public void NoKeyTriggersActionsInTwoPlaces()
        {
            var reactions = new Dictionary<string, List<string>>();

            foreach (string hub in Hubs)
            {
                string path = FindHub(hub);
                if (path == null) continue;    // файла нет — это проверит тест ниже
                foreach (string key in ReactingKeys(path))
                {
                    if (!reactions.ContainsKey(key)) reactions[key] = new List<string>();
                    reactions[key].Add(hub);
                }
            }

            Assert.Greater(reactions.Count, 5,
                "разбор не нашёл ни одной реакции на клавиши — проверка бессмысленна (сломался разбор)");

            var conflicts = new List<string>();
            foreach (var pair in reactions)
            {
                if (pair.Value.Count < 2) continue;
                if (AllowedOverlaps.ContainsKey(pair.Key)) continue;
                conflicts.Add(pair.Key + " → " + string.Join(" + ", pair.Value.ToArray()));
            }

            Assert.AreEqual(0, conflicts.Count,
                "одна клавиша делает два дела в одном кадре (класс дефекта §27/E-08): " +
                string.Join("; ", conflicts.ToArray()) +
                ". Разведите клавиши либо введите арбитраж и впишите исключение с причиной");
        }

        // ---------------------------------------------------------------- 2. САМОПРОВЕРКА

        /// <summary>
        /// РАЗБОР НАХОДИТ ИЗВЕСТНЫЕ РЕАКЦИИ. Если сломается разбор, тест выше станет «зелёным по
        /// определению» — ровно та ошибка, из-за которой в проекте было 13 самовыключающихся
        /// тестов (E-16). Здесь проверяется, что разбор видит заведомо существующие обработчики.
        /// </summary>
        [Test]
        public void ParserSeesKnownHandlers()
        {
            string stageHub = FindHub("KvStageHub.cs");
            Assert.IsNotNull(stageHub, "нет файла KvStageHub.cs — проверить нечего");
            var keys = ReactingKeys(stageHub);
            Assert.Contains("F8", new List<string>(keys),
                "KvStageHub обязан реагировать на F8 (снимок экрана). Разбор его не увидел — " +
                "проверка конфликтов обесценена");

            string featureHub = FindHub("FeatureHub.cs");
            Assert.IsNotNull(featureHub);
            var featureKeys = ReactingKeys(featureHub);
            Assert.Contains("F5", new List<string>(featureKeys),
                "FeatureHub обязан реагировать на F5. Разбор его не увидел");
        }

        /// <summary>
        /// ВСЕ ФАЙЛЫ ИЗ СПИСКА СУЩЕСТВУЮТ. Переименовали хаб — список обязан обновиться, иначе
        /// его клавиши выпадут из проверки МОЛЧА.
        /// </summary>
        [Test]
        public void AllListedHubsExist()
        {
            var missing = new List<string>();
            foreach (string hub in Hubs) if (FindHub(hub) == null) missing.Add(hub);

            Assert.AreEqual(0, missing.Count,
                "в списке проверяемых хабов есть несуществующие файлы: " + string.Join(", ", missing.ToArray()) +
                " — переименовали файл? Поправьте список, иначе его клавиши не проверяются");
        }

        // ---------------------------------------------------------------- 3. ИСКЛЮЧЕНИЯ ОБОСНОВАНЫ

        /// <summary>
        /// КАЖДОЕ ИСКЛЮЧЕНИЕ ИМЕЕТ ПРИЧИНУ. Пустая строка в `AllowedOverlaps` — это «разрешили
        /// молча», то есть то же самое, что `Assert.Ignore`: конфликт остаётся, но о нём молчат.
        /// </summary>
        [Test]
        public void EveryAllowedOverlapHasReason()
        {
            var unexplained = new List<string>();
            foreach (var pair in AllowedOverlaps)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length < 10)
                    unexplained.Add(pair.Key);
            }
            Assert.AreEqual(0, unexplained.Count,
                "исключения без объяснения причины: " + string.Join(", ", unexplained.ToArray()) +
                ". Причина обязательна — иначе это не арбитраж, а замолчанный конфликт");
        }
    }
}
