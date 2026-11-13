using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>
    /// РЕЗУЛЬТАТ ИМПОРТА СЦЕНАРИЯ ИЗ ФАЙЛА. Отказ — это НЕ исключение, а сообщение:
    /// кнопка меню не имеет права ронять интерфейс оператора (красный список §3, 13.11.2026).
    /// </summary>
    public class KvScenarioImportResult
    {
        /// <summary>Импорт прошёл: в <see cref="Session"/> разобранная сценария, в <see cref="Error"/> пусто.</summary>
        public bool Ok;
        /// <summary>Причина отказа с именем файла (пусто, если <see cref="Ok"/>).</summary>
        public string Error = "";
        /// <summary>Путь к разобранному файлу (пусто при отказе).</summary>
        public string FilePath = "";
        /// <summary>Разобранная сценария (null при отказе — применять нечего).</summary>
        public KvSession Session;
    }

    /// <summary>
    /// ИМПОРТ СЦЕНАРИЯ ИЗ ФАЙЛА (закрытие дефекта §3 красного списка 13.11.2026).
    ///
    /// ЧТО БЫЛО. Пункт меню «Файл → Импорт сценария» (`file.import`) был зарегистрирован как
    /// заглушка (`Stub = true`, `Execute = null`) и не делал ничего: пункт был виден в интерфейсе
    /// и обещал «загрузку точек и траекторий из файла». Требование владельца — «чтобы все
    /// кнопочки были заполнены функционалом».
    ///
    /// ЧТО СТАЛО (выбран вариант 1 из §3). Парная операция экспорта в папку данных уже
    /// реальная (`file.export` показывает папку, `session.save` пишет туда JSON сценария),
    /// поэтому импорт делает ОБРАТНЫЙ РАЗБОР того же формата:
    ///   файл → чтение → разбор `KvSession` → проверка, что это действительно сценарий →
    ///   приведение к рабочему виду (`Normalize`) → применение вызывающим кодом.
    ///
    /// ЧЕМ ОТЛИЧАЕТСЯ ОТ `KvSessionStore.Load`. Штатная загушка возвращает `null` при ЛЮБОЙ
    /// проблеме — нет файла, не читается, это не JSON, в файле пусто, — и оператор получает
    /// одно и то же «сессий нет». Здесь КАЖДАЯ проблема называется своим честным сообщением
    /// с именем файла: по журналу видно, что именно пошло не так и что делать дальше.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ. Никаких исключений наружу: всё, что может упасть, поймано здесь же.
    /// </summary>
    public static class KvScenarioFileImport
    {
        /// <summary>
        /// ФАЙЛЫ-КАНДИДАТЫ В ПАПКЕ. Берутся `*.json` (формат экспорта), временные файлы
        /// атомарной записи (`*.tmp`) отбрасываются — иначе импорт мог бы прочитать
        /// полузаписанный файл.
        /// </summary>
        public static List<string> CandidateFiles(string dir)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(dir)) return result;
            try
            {
                if (!Directory.Exists(dir)) return result;
                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(file);
                }
                result.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Features] импорт: каталог «" + dir + "» не прочитан: " + e.Message);
            }
            return result;
        }

        /// <summary>Импорт самой свежей сценарии из одной папки (по времени записи файла).</summary>
        public static KvScenarioImportResult ImportNewest(string dir)
        {
            return ImportNewestFromFolders(new string[] { dir });
        }

        /// <summary>
        /// Импорт самой свежей сценарии из НЕСКОЛЬКИХ папок (папка сценариев + папка сессий).
        /// Сравнивается время записи файлов во всех папках — берётся последнее сохранённое
        /// состояние, а не первое попавшееся.
        /// </summary>
        public static KvScenarioImportResult ImportNewestFromFolders(string[] dirs)
        {
            var result = new KvScenarioImportResult();
            if (dirs == null || dirs.Length == 0)
            {
                result.Error = "папка для импорта не указана — нечего разбирать";
                return result;
            }

            string newest = null;
            DateTime newestTime = DateTime.MinValue;
            int scanned = 0;

            for (int i = 0; i < dirs.Length; i++)
            {
                string dir = dirs[i];
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                scanned++;
                foreach (string file in CandidateFiles(dir))
                {
                    DateTime written;
                    try { written = File.GetLastWriteTimeUtc(file); }
                    catch { written = DateTime.MinValue; }
                    if (newest != null && written <= newestTime) continue;
                    newest = file;
                    newestTime = written;
                }
            }

            if (newest != null) return ImportFile(newest);

            if (scanned == 0)
            {
                result.Error = "папка(и) сценариев не найдены: " + string.Join(", ", dirs);
                return result;
            }

            result.Error = "в папке(ах) нет файлов сценария (" + string.Join(", ", dirs) + ")" +
                           " — сначала сохраните сессию («Файл → Сессия → Сохранить сессию»), " +
                           "папку данных смотрите в «Файл → Папка данных»";
            return result;
        }

        /// <summary>
        /// ОБРАТНЫЙ РАЗБОР ОДНОГО ФАЙЛА. Никогда не бросает исключение: любая проблема
        /// превращается в <see cref="KvScenarioImportResult.Error"/> с именем файла.
        /// </summary>
        public static KvScenarioImportResult ImportFile(string path)
        {
            var result = new KvScenarioImportResult();

            if (string.IsNullOrEmpty(path))
            {
                result.Error = "путь к файлу сценария не задан";
                return result;
            }

            if (!File.Exists(path))
            {
                result.Error = "файл сценария не найден: " + path;
                return result;
            }

            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception e)
            {
                result.Error = "файл не читается (" + path + "): " + e.Message;
                return result;
            }

            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0)
            {
                result.Error = "файл пуст: " + path;
                return result;
            }

            KvSession session;
            try
            {
                session = JsonUtility.FromJson<KvSession>(json);
            }
            catch (Exception e)
            {
                result.Error = "файл не разбирается как сценарий (" + path + "): " + e.Message;
                return result;
            }

            if (session == null)
            {
                result.Error = "файл не разбирается как сценарий (" + path + "): пустой результат разбора";
                return result;
            }

            // ПРОВЕРКА СОДЕРЖАНИЯ ДО `Normalize`. `JsonUtility` спокойно принимает посторонний
            // объект и возвращает `KvSession` со значениями по умолчанию; `Normalize` при этом
            // тут же выдал бы этому «сценарию» новый идентификатор — и отказ стал бы невозможен.
            if (!HasScenarioData(session))
            {
                result.Error = "в файле нет данных сценария (" + path + "): нужен JSON с " +
                               "идентификатором либо списками роботов, точек, зон, записей или поз";
                return result;
            }

            session.filePath = path;
            session.Normalize();

            result.Ok = true;
            result.Error = "";
            result.FilePath = path;
            result.Session = session;
            return result;
        }

        /// <summary>
        /// ЕСТЬ ЛИ В РАЗОБРАННОМ ОБЪЕКТЕ ЧТО-НИБУДЬ СЦЕНАРНОЕ. Идентификатор — признак файла,
        /// записанного штатным экспортом (`session.save` всегда пишет `id`); списки — признак
        /// файла, собранного вручную или другим инструментом.
        /// </summary>
        private static bool HasScenarioData(KvSession session)
        {
            if (session == null) return false;
            if (!string.IsNullOrEmpty(session.id)) return true;
            if (session.robots != null && session.robots.Count > 0) return true;
            if (session.points != null && session.points.Count > 0) return true;
            if (session.zones != null && session.zones.Count > 0) return true;
            if (session.recordFiles != null && session.recordFiles.Count > 0) return true;
            if (session.poseFiles != null && session.poseFiles.Count > 0) return true;
            return false;
        }
    }
}
