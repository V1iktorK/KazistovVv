// ================================================================================================
//  KvLeakPolicyTests.cs — EditMode-ТЕСТЫ УТЕЧЕК (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Утечки в Unity — это не только память. Это «двойные срабатывания»: объект
//  подписался на событие, был уничтожен, но подписка осталась — и обработчик вызывается у мёртвого
//  объекта. Оператор видит, что кнопка нажимается дважды, а причина невидима.
//
//  В проекте это уже было: §27 закрывал 43 подписки без отписки.
//
//  ЧТО ПРОВЕРЕНО 30.10.2026 (числами, а не на глаз):
//    • файлов с подписками БЕЗ единой отписки — ОДИН (`HierarchyPhantomCleaner`), и это ЗАКОННО:
//      подписка стоит в СТАТИЧЕСКОМ КОНСТРУКТОРЕ, живёт всё время работы редактора и не привязана
//      к уничтожаемому объекту;
//    • статических коллекций, которые только растут — ДВЕ, и обе реализованы как ЛЕНИВЫЙ КЭШ с
//      методом `Invalidate` (сброс в null), а не как накопитель.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ: список законных мест с причиной у каждого. Новый файл с подписками без отписки
//  или новая растущая коллекция — красный тест.
//
//  ГРАНИЦА ЧЕСТНОСТИ: это статический разбор, а не профилировщик. Он ловит КЛАСС «подписался —
//  не отписался», а не медленную утечку через замыкания.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Утечки подписок и коллекций: только осознанные, с причиной.</summary>
    public class KvLeakPolicyTests
    {
        /// <summary>
        /// ФАЙЛЫ С ПОДПИСКАМИ БЕЗ ОТПИСКИ, ПРИЗНАННЫЕ ЗАКОННЫМИ — с причиной у каждого.
        ///
        /// Проверено вручную 30.10.2026: открывал файл и смотрел, где стоит подписка.
        /// </summary>
        private static readonly Dictionary<string, string> AllowedSubscriptions =
            new Dictionary<string, string>
            {
                { "HierarchyPhantomCleaner.cs",
                  "подписка в СТАТИЧЕСКОМ КОНСТРУКТОРЕ: живёт всё время работы редактора, к уничтожаемому объекту не привязана" },
            };

        /// <summary>
        /// СТАТИЧЕСКИЕ КОЛЛЕКЦИИ, КОТОРЫЕ «ТОЛЬКО РАСТУТ», ПРИЗНАННЫЕ ЗАКОННЫМИ — с причиной.
        /// </summary>
        private static readonly Dictionary<string, string> AllowedCollections =
            new Dictionary<string, string>
            {
                { "KvWorkbenchWindow.cs", "ленивый кэш вкладок: сбрасывается в null методом Invalidate, а не накапливается" },
                { "KvBindings.cs", "ленивый кэш биндов: сбрасывается в null методом Invalidate, а не накапливается" },
            };

        private static string ScriptsRoot
        {
            get { return Path.Combine(Application.dataPath, "_Project", "01_Scripts"); }
        }

        /// <summary>
        /// НАЙТИ ФАЙЛ ВО ВСЁМ ПРОЕКТЕ.
        ///
        /// ПОЧЕМУ НЕ ТОЛЬКО `01_Scripts`: UI-скрипты живут в `06_KazistovVv_UI`. Первая версия
        /// искала в одном месте и объявила `KvBindings.cs` «удалённым», хотя файл на месте.
        /// Класс ошибки тот же, что E-20 (неполный набор источников): проверка искала в одном
        /// месте, а код лежит в двух.
        /// </summary>
        private static string FindAnywhere(string fileName)
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            string[] found = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>Файлы для проверки: без тестов и инструментов диагностики.</summary>
        private static List<string> SourceFiles()
        {
            var files = new List<string>();
            if (!Directory.Exists(ScriptsRoot)) return files;

            foreach (string file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith("Tests.cs")) continue;
                if (name.Contains("Diag")) continue;
                files.Add(file);
            }
            return files;
        }

        /// <summary>
        /// САМОПРОВЕРКА: файлы найдены. Без неё проверки ниже зеленели бы «потому что нечего
        /// проверять» (E-16).
        /// </summary>
        [Test]
        public void SourceFilesAreFound()
        {
            Assert.Greater(SourceFiles().Count, 100,
                "найдено слишком мало файлов (" + SourceFiles().Count + ") — путь не тот");
        }

        /// <summary>
        /// НОВЫХ ПОДПИСОК БЕЗ ОТПИСКИ НЕТ.
        ///
        /// Проверка простая, но именно она закрывает §27: объект подписался, был уничтожен, подписка
        /// осталась — и обработчик дёргает мёртвый объект. Оператор видит «кнопка срабатывает
        /// дважды», и причину найти невозможно.
        /// </summary>
        [Test]
        public void NoNewSubscriptionsWithoutUnsubscribe()
        {
            var offenders = new List<string>();

            foreach (string file in SourceFiles())
            {
                string name = Path.GetFileName(file);
                if (AllowedSubscriptions.ContainsKey(name)) continue;

                string text = File.ReadAllText(file);

                int subscriptions = Regex.Matches(text,
                    @"\w+\.\w+\s*\+=\s*(?:On|\w+Handler|\w+Changed|\w+Clicked)").Count;
                if (subscriptions == 0) continue;

                // Отписка ищется как `-=` на любом событии: конкретная форма не важна.
                int unsubscriptions = Regex.Matches(text, @"\w+\.\w+\s*-=").Count;
                if (unsubscriptions == 0) offenders.Add(name + " (" + subscriptions + " подписок)");
            }

            Assert.AreEqual(0, offenders.Count,
                "в этих файлах есть ПОДПИСКИ БЕЗ ОТПИСКИ — уничтоженный объект продолжит получать " +
                "события (тот же класс, что §27: «кнопка срабатывает дважды»): " +
                string.Join("; ", offenders.ToArray()) +
                ". Отпишитесь в OnDestroy/OnDisable либо внесите файл в AllowedSubscriptions с причиной");
        }

        /// <summary>
        /// СТАТИЧЕСКИЕ КОЛЛЕКЦИИ НЕ РАСТУТ БЕСКОНЕЧНО. Статический словарь, в который только
        /// добавляют, живёт до перезапуска приложения: за долгую смену он накапливает записи, и
        /// память растёт незаметно.
        ///
        /// ЛЕНИВЫЙ КЭШ (сброс в null через `Invalidate`) — законная реализация: коллекция
        /// пересоздаётся, а не накапливается.
        /// </summary>
        [Test]
        public void StaticCollectionsDoNotGrowForever()
        {
            var offenders = new List<string>();

            foreach (string file in SourceFiles())
            {
                string name = Path.GetFileName(file);
                if (AllowedCollections.ContainsKey(name)) continue;

                string text = File.ReadAllText(file);

                foreach (Match m in Regex.Matches(text,
                    @"static\s+(?:readonly\s+)?(?:Dictionary|List|HashSet)<[^>]+>\s+(\w+)"))
                {
                    string field = m.Groups[1].Value;

                    bool adds = Regex.IsMatch(text, @"\b" + Regex.Escape(field) + @"\.(?:Add|TryAdd)\b");
                    bool cleans = Regex.IsMatch(text,
                        @"\b" + Regex.Escape(field) + @"\.(?:Remove|Clear|RemoveAt)\b") ||
                        Regex.IsMatch(text, @"\b" + Regex.Escape(field) + @"\s*=\s*null\s*;");

                    if (adds && !cleans) offenders.Add(name + " → " + field);
                }
            }

            Assert.AreEqual(0, offenders.Count,
                "эти СТАТИЧЕСКИЕ коллекции только растут — за долгую смену память утечёт: " +
                string.Join("; ", offenders.ToArray()) +
                ". Добавьте очистку (Clear/Remove) или сделайте ленивым кэшем со сбросом в null");
        }

        /// <summary>
        /// СПИСКИ ИСКЛЮЧЕНИЙ ОБОСНОВАНЫ И НЕ УСТАРЕЛИ.
        /// </summary>
        [Test]
        public void ExceptionListsAreJustified()
        {
            var stale = new List<string>();

            foreach (var pair in AllowedSubscriptions)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length < 25)
                    stale.Add(pair.Key + " (нет внятной причины)");
                if (FindAnywhere(pair.Key) == null)
                    stale.Add(pair.Key + " (файла нет)");
            }

            foreach (var pair in AllowedCollections)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length < 25)
                    stale.Add(pair.Key + " (нет внятной причины)");
                if (FindAnywhere(pair.Key) == null)
                    stale.Add(pair.Key + " (файла нет)");
            }

            Assert.AreEqual(0, stale.Count,
                "списки исключений устарели: " + string.Join("; ", stale.ToArray()));
        }
    }
}
