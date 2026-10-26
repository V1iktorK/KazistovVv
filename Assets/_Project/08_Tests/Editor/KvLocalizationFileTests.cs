using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KazistovVvUI;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты СЛОВАРЕЙ ЛОКАЛИЗАЦИИ (сессия §32, карточка **Б15**).
    ///
    /// ЗАЧЕМ. На живом шлеме 13.10.2026 приложение написало:
    /// `[KazistovVv] Язык интерфейса: Русский · языков: 7 · **файлов прочитано: 0** · строк всего: 14`.
    /// Причина: на Android `Application.streamingAssetsPath` — это адрес ВНУТРИ пакета
    /// (`jar:file:///data/app/…/base.apk!/assets`), а не папка. `Directory.Exists` его не видит,
    /// `File.ReadAllText` не читает, и семь словарей (по ~187 строк каждый) на устройстве
    /// не подставлялись: интерфейс в шлеме оставался на русских литералах кода.
    ///
    /// ЧТО ИМЕННО ЗАКРЕПЛЕНО ЗДЕСЬ (и почему именно это, а не «чтение с Android» — его в EditMode
    /// не воспроизвести):
    ///   1) **в редакторе асинхронный путь НЕ включается** — папка словарей видна, значит
    ///      десктопный путь остался прежним (правило [K3]: компьютер не трогаем);
    ///   2) **список кодов = список файлов**: если добавят восьмой язык и забудут код в таблице
    ///      `KnownFileCodes`, на устройстве этот язык молча не загрузится — тест это назовёт;
    ///   3) **файлы словарей существуют и не пусты** — иначе «прочитано 7» на устройстве
    ///      превратилось бы в «прочитано 7, строк 0».
    ///
    /// Ни одного GameObject, ни одной записи, ни зависимости от времени.
    /// </summary>
    public class KvLocalizationFileTests
    {
        private static string LanguageFolder
        {
            get { return Path.Combine(Application.dataPath, "StreamingAssets", KvLoc.FolderName); }
        }

        /// <summary>В редакторе словари читаются с диска — асинхронная догрузка не нужна.</summary>
        [Test]
        public void Editor_DoesNotNeedAsyncFileLoad()
        {
            Assert.IsTrue(Directory.Exists(LanguageFolder),
                "не найдена папка словарей " + LanguageFolder + " — тогда и в редакторе их не прочитать");
            Assert.IsFalse(KvLoc.NeedsAsyncFileLoad(),
                "в редакторе папка словарей видна, значит асинхронный путь (для Android) включаться НЕ должен: " +
                "иначе мы меняем поведение компьютера");
        }

        /// <summary>
        /// КАЖДЫЙ КОД ИЗ ТАБЛИЦЫ ИМЕЕТ ФАЙЛ, И НАОБОРОТ. На устройстве каталог перечислить нельзя
        /// (он внутри пакета), поэтому файлы запрашиваются по именам из `KnownFileCodes`:
        /// забытый код = язык, которого в шлеме не будет.
        /// </summary>
        [Test]
        public void KnownCodes_MatchFilesOnDisk()
        {
            Assert.Greater(KvLoc.KnownFileCodes.Length, 0, "таблица кодов словарей пуста");

            string[] onDisk = Directory.GetFiles(LanguageFolder, "*.json");
            var diskCodes = new List<string>();
            foreach (string file in onDisk)
                diskCodes.Add(Path.GetFileNameWithoutExtension(file).ToLowerInvariant());

            var missingFiles = new List<string>();
            foreach (string code in KvLoc.KnownFileCodes)
            {
                string expected = code.ToLowerInvariant();
                if (!diskCodes.Contains(expected)) missingFiles.Add(expected);
            }

            Assert.AreEqual(0, missingFiles.Count,
                "эти коды есть в таблице, но файлов словарей нет: " + string.Join(", ", missingFiles.ToArray()));

            var missingCodes = new List<string>();
            foreach (string code in diskCodes)
            {
                bool known = false;
                foreach (string knownCode in KvLoc.KnownFileCodes)
                    if (knownCode.ToLowerInvariant() == code) { known = true; break; }
                if (!known) missingCodes.Add(code);
            }

            Assert.AreEqual(0, missingCodes.Count,
                "в проекте есть словари, которых НЕТ в таблице кодов — на устройстве они не загрузятся " +
                "(Android не перечисляет каталог внутри пакета): " + string.Join(", ", missingCodes.ToArray()));
        }

        /// <summary>Файлы словарей не пусты: иначе «прочитано 7» даст ноль строк.</summary>
        [Test]
        public void LanguageFiles_AreNotEmpty()
        {
            var empty = new List<string>();
            foreach (string code in KvLoc.KnownFileCodes)
            {
                string file = Path.Combine(LanguageFolder, code + ".json");
                if (!File.Exists(file)) { empty.Add(code + " (нет файла)"); continue; }
                string text = File.ReadAllText(file);
                if (string.IsNullOrEmpty(text) || text.Length < 100) empty.Add(code + " (пустой)");
            }

            Assert.AreEqual(0, empty.Count,
                "эти словари пусты или отсутствуют: " + string.Join(", ", empty.ToArray()));
        }

        /// <summary>
        /// ПУТЬ К ФАЙЛУ СЛОВАРЯ — это адрес, а не строка склейки. На устройстве он начинается
        /// с `jar:file://` (внутри пакета), и `UnityWebRequest` его понимает; на диске — обычный
        /// путь. Проверяется, что имя каталога берётся из одного места (`KvLoc.FolderName`).
        /// </summary>
        [Test]
        public void FolderName_IsUsedForLanguageFolder()
        {
            Assert.AreEqual("kazistovvv_i18n", KvLoc.FolderName,
                "имя каталога словарей изменилось — тогда на Android файлы будут запрашиваться не там");
        }
    }
}
