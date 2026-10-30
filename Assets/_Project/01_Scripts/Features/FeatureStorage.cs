using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>
    /// Единое хранилище файлов новых функций (этапы 1–20 ТЗ): записи траекторий,
    /// позы, зоны запрета, журнал действий, сценарии, сессии.
    ///
    /// ФОРМАТ — JSON (текстовый), сериализация — <see cref="JsonUtility"/>.
    /// Обоснование выбора (ТЗ этапа 1 требует обосновать):
    ///   1) читается и правится человеком, диффится в git — важно для инженерного проекта;
    ///   2) расширяемость без ломки старых файлов: <see cref="JsonUtility"/> заполняет только
    ///      ПРИСУТСТВУЮЩИЕ поля, отсутствующие остаются значениями инициализаторов
    ///      (`Normalize()` дополнительно чинит битые/старые файлы) — новые поля можно
    ///      добавлять, не трогая уже сохранённые записи;
    ///   3) не нужны ассеты, .meta, ScriptableObject и импорт — всё живёт в рантайме;
    ///   4) объём даже большой записи невелик (10 Гц × 60 с × 9 чисел ≈ 500 КБ текста),
    ///      а бинарный формат выиграл бы лишь в разы, потеряв читаемость и совместимость.
    ///
    /// Каталог по умолчанию: `&lt;persistentDataPath&gt;/KazistovVv` (в редакторе — вне Assets,
    /// поэтому файлы не попадают в сборку и не требуют .meta). Путь всегда пишется в лог.
    /// </summary>
    public static class FeatureStorage
    {
        /// <summary>Корневой каталог данных функций.</summary>
        public static string Root
        {
            get
            {
                string root = Path.Combine(Application.persistentDataPath, "KazistovVv");
                EnsureDir(root);
                return root;
            }
        }

        public static string RecordingsDir { get { return EnsureDir(Path.Combine(Root, "Recordings")); } }
        public static string PosesDir { get { return EnsureDir(Path.Combine(Root, "Poses")); } }
        public static string ZonesDir { get { return EnsureDir(Path.Combine(Root, "Zones")); } }
        public static string LogsDir { get { return EnsureDir(Path.Combine(Root, "Logs")); } }
        public static string SessionsDir { get { return EnsureDir(Path.Combine(Root, "Sessions")); } }
        public static string ScenariosDir { get { return EnsureDir(Path.Combine(Root, "Scenarios")); } }
        public static string ConfigDir { get { return EnsureDir(Path.Combine(Root, "Config")); } }

        /// <summary>
        /// Каталог отчётов прогонов и диагностики (ФИКС 10): `&lt;persistentDataPath&gt;/KazistovVv/Reports`.
        /// </summary>
        public static string ReportsDir { get { return EnsureDir(Path.Combine(Root, "Reports")); } }

        /// <summary>
        /// ФИКС 10. Путь отчётного файла прогона/диагностики. Пишем в `persistentDataPath`,
        /// а НЕ в корень проекта: проект лежит в OneDrive, и во время PlayMode файл там
        /// не дописывается (§12.10, §13.11) — строки отчёта терялись, и результат приходилось
        /// вычитывать из лога Unity. `persistentDataPath` — локальный каталог вне синхронизации,
        /// записи идут нормально и в редакторе, и в собранной игре.
        /// Это РЕШЕНИЕ по расположению отчётов, а не обход ошибки.
        /// </summary>
        public static string ReportPath(string fileName)
        {
            string name = SafeName(string.IsNullOrEmpty(fileName) ? "report.txt" : fileName, "report.txt");
            return Path.Combine(ReportsDir, name);
        }

        /// <summary>Каталог существующей заготовки записи (ТЗ этапа 1: «используй существующую»).</summary>
        public static string LegacyRecordingFolder
        {
            get
            {
                // Заготовка `01_Scripts/Recording` писала в persistentDataPath (или путь из SettingsData).
                return Application.persistentDataPath;
            }
        }

        public static string EnsureDir(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && !Directory.Exists(path)) Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Features] не удалось создать каталог «" + path + "»: " + e.Message);
            }
            return path;
        }

        /// <summary>
        /// Безопасное имя файла из произвольной пользовательской строки.
        ///
        /// ИСПРАВЛЕНО 30.10.2026 (S44): имя из одних точек («.», «..», «...») раньше проходило
        /// очистку как есть. А это НЕ имя файла, а СОСЕДНЯЯ ПАПКА: `.` — текущая, `..` —
        /// родительская. Имя приходит от оператора, и точка в поле — правдоподобный ввод (человек
        /// нажал точку, чтобы проверить, работает ли поле). Последствие: сохранение либо
        /// проваливается, либо пишет файл ВНЕ папки приложения — и найти его потом невозможно.
        ///
        /// Почему проверка именно «одни точки», а не «начинается с точки»: имя `.скрытый` —
        /// законное (скрытый файл), и запрещать его незачем. Опасны только имена, где КРОМЕ точек
        /// ничего нет.
        ///
        /// Причина заменять пробелы и точки в КОНЦЕ: Windows молча отбрасывает их при создании
        /// файла, а потом ищет файл по имени С ними — и не находит. Это выглядело бы как «макрос
        /// сохранился, но пропал».
        /// </summary>
        public static string SafeName(string name, string fallback = "item")
        {
            if (string.IsNullOrEmpty(name)) return fallback;
            StringBuilder sb = new StringBuilder(name.Length);
            char[] bad = Path.GetInvalidFileNameChars();
            foreach (char c in name)
            {
                bool ok = true;
                for (int i = 0; i < bad.Length; i++) if (bad[i] == c) { ok = false; break; }
                sb.Append(ok && c != '/' && c != '\\' ? c : '_');
            }
            string result = sb.ToString().Trim();

            // Одни точки — это не файл, а папка («.» / «..»). Заменяем на запасное имя.
            if (result.Length == 0 || result.Trim('.').Length == 0) return fallback;

            // Конечные точки и пробелы Windows отбрасывает молча — убираем сами, чтобы имя,
            // которое мы записали, совпадало с именем, по которому будем искать.
            result = result.TrimEnd('.', ' ');
            return result.Length == 0 ? fallback : result;
        }

        /// <summary>Сохранение JSON-объекта в файл (атомарно: временный файл + замена).</summary>
        public static bool SaveJson<T>(string path, T value, bool pretty = true)
        {
            try
            {
                EnsureDir(Path.GetDirectoryName(path));
                string json = JsonUtility.ToJson(value, pretty);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Features] не удалось сохранить «" + path + "»: " + e.Message);
                return false;
            }
        }

        /// <summary>Чтение JSON-объекта из файла (null при любой проблеме — вызывающий решает).</summary>
        public static T LoadJson<T>(string path) where T : class
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrEmpty(json)) return null;
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Features] не удалось прочитать «" + path + "»: " + e.Message);
                return null;
            }
        }

        /// <summary>Список файлов каталога по маске (без временных).</summary>
        public static List<string> ListFiles(string dir, string pattern)
        {
            List<string> result = new List<string>();
            try
            {
                if (!Directory.Exists(dir)) return result;
                string[] files = Directory.GetFiles(dir, pattern);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string f in files)
                {
                    if (f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(f);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Features] не удалось прочитать каталог «" + dir + "»: " + e.Message);
            }
            return result;
        }

        /// <summary>Имя файла без расширения (для подписи в дереве/списке).</summary>
        public static string FileStem(string path)
        {
            try { return Path.GetFileNameWithoutExtension(path); }
            catch { return path; }
        }

        /// <summary>Удалить файл (false — файла не было или он занят).</summary>
        public static bool DeleteFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Features] не удалось удалить «" + path + "»: " + e.Message);
                return false;
            }
        }

        /// <summary>Дописать строку в файл (журнал действий, телеметрия прогонов).</summary>
        public static bool AppendLine(string path, string line)
        {
            try
            {
                EnsureDir(Path.GetDirectoryName(path));
                File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Features] не удалось дописать в «" + path + "»: " + e.Message);
                return false;
            }
        }

        /// <summary>Метка времени для имён файлов: 20260101_120000.</summary>
        public static string TimeStamp()
        {
            DateTime now = DateTime.Now;
            return string.Format("{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}",
                now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        }

        /// <summary>ISO-время для полей «created».</summary>
        public static string IsoNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
