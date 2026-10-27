using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace KazistovVvUI
{
    /// <summary>Описание языка в списке доступных (для панели настроек).</summary>
    public class KvLangInfo
    {
        public string code = "";          // «ru», «en», «zh», «it» …
        public string name = "";          // название на самом языке («Русский», «中文»)
        public string englishName = "";   // английское название (для fallback-подписи)
        public bool needsCjk;             // требует шрифт с CJK-глифами
        public int strings;               // сколько строк загружено (диагностика)
        public bool fromFile;             // загружен из StreamingAssets (а не встроенный)

        public string Label
        {
            get
            {
                if (string.IsNullOrEmpty(name)) return code;
                if (string.IsNullOrEmpty(englishName) || englishName == name) return name;
                return name + " (" + englishName + ")";
            }
        }
    }

    /// <summary>
    /// СИСТЕМА ЛОКАЛИЗАЦИИ (ЭТАП 2 ТЗ): 7 языков (RU / EN / ZH / ES / DE / FR / JA)
    /// плюс любые дополнительные, добавленные файлом.
    ///
    /// ПОЧЕМУ JSON, А НЕ ScriptableObject (обоснование по ТЗ):
    ///   * текст правится без Unity и без перекомпиляции (у ScriptableObject нужен редактор
    ///     и .meta-ассет на каждый язык, а на 7 языков это 7 ассетов + риск конфликтов GUID);
    ///   * файл читается человеком и диффится в git построчно (переводы удобно ревьюить);
    ///   * новый язык = новый файл `StreamingAssets/kazistovvv_i18n/<код>.json`, который
    ///     подхватывается АВТОМАТИЧЕСКИ (сканирование каталога) — правок кода не нужно;
    ///   * отсутствующие ключи не ломают интерфейс: цепочка «текущий язык → английский →
    ///     русский текст, переданный вызывающим кодом».
    ///
    /// ЦЕПОЧКА FALLBACK (требование ТЗ «отсутствующие переводы — fallback на английский»):
    ///   T(key, fallbackRu) = каталог(текущий)[key] ?? каталог(en)[key] ?? fallbackRu ?? key
    ///
    /// Язык по умолчанию — СИСТЕМНЫЙ (`Application.systemLanguage`); если системный не
    /// поддерживается — английский. Выбор хранится в PlayerPrefs (`KazistovVv.Language`).
    /// Смена языка мгновенная: событие <see cref="Changed"/> пересобирает оболочку интерфейса.
    /// </summary>
    public static class KvLoc
    {
        /// <summary>Ключ PlayerPrefs с выбранным языком («system» — автоматически).</summary>
        public const string PrefsKey = "KazistovVv.Language";
        /// <summary>Каталог словарей в StreamingAssets.</summary>
        public const string FolderName = "kazistovvv_i18n";
        /// <summary>Значение «язык как в системе».</summary>
        public const string SystemCode = "system";

        /// <summary>Смена языка (интерфейс пересобирается по этому событию).</summary>
        public static event Action Changed;

        // ------------------------------------------------------------------ внутреннее состояние

        private static readonly List<KvLangInfo> langs = new List<KvLangInfo>();
        private static readonly Dictionary<string, Dictionary<string, string>> catalogs =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static bool loaded;
        private static string preference = SystemCode;   // что выбрал оператор
        private static string current = "en";           // фактический язык
        private static string loadStatus = "не загружено";

        /// <summary>Сколько словарей-файлов прочитано (накопительно: диск + устройство).</summary>
        private static int fileCount;

        /// <summary>Идёт ли асинхронная загрузка словарей с устройства (защита от повторного запуска).</summary>
        private static bool asyncRunning;

        /// <summary>
        /// КОДЫ ФАЙЛОВ СЛОВАРЕЙ. На Android перечислить каталог нельзя (он внутри пакета),
        /// поэтому файлы запрашиваются по имени — те семь, что лежат в проекте.
        /// </summary>
        public static readonly string[] KnownFileCodes = { "ru", "en", "zh", "es", "de", "fr", "ja" };
        private static Font cjkFont;
        private static string cjkFontName = "";

        // ------------------------------------------------------------------ публичный API

        /// <summary>Доступные языки (встроенные + найденные в StreamingAssets).</summary>
        public static IReadOnlyList<KvLangInfo> Languages { get { Ensure(); return langs; } }

        /// <summary>Выбор оператора: код языка или «system».</summary>
        public static string PreferenceCode { get { Ensure(); return preference; } }

        /// <summary>Фактически применённый язык (никогда не «system»).</summary>
        public static string CurrentCode { get { Ensure(); return current; } }

        /// <summary>Название текущего языка на нём самом.</summary>
        public static string CurrentName
        {
            get
            {
                KvLangInfo info = Find(CurrentCode);
                return info != null ? info.Label : CurrentCode;
            }
        }

        /// <summary>Нужен ли шрифт с CJK-глифами (китайский/японский/корейский).</summary>
        public static bool NeedsCjk
        {
            get
            {
                string c = CurrentCode;
                KvLangInfo info = Find(c);
                if (info != null && info.needsCjk) return true;
                return c == "zh" || c == "ja" || c == "ko";
            }
        }

        /// <summary>Диагностика загрузки (для отчётов и панели настроек).</summary>
        public static string Status
        {
            get { Ensure(); return loadStatus + " · активный: " + CurrentName + " (" + current + ")"; }
        }

        /// <summary>Каталог словарей на диске (может не существовать — работают встроенные).</summary>
        public static string FolderPath
        {
            get
            {
                try { return Path.Combine(Application.streamingAssetsPath, FolderName); }
                catch { return FolderName; }
            }
        }

        /// <summary>
        /// Перевод по ключу. `fallback` — русский исходный текст из кода: он используется,
        /// если ключа нет ни в текущем языке, ни в английском.
        /// </summary>
        public static string T(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? "";
            Ensure();

            Dictionary<string, string> map;
            if (catalogs.TryGetValue(current, out map))
            {
                string s;
                if (map.TryGetValue(key, out s) && !string.IsNullOrEmpty(s)) return s;
            }
            if (catalogs.TryGetValue("en", out map))
            {
                string s;
                if (map.TryGetValue(key, out s) && !string.IsNullOrEmpty(s)) return s;
            }
            return string.IsNullOrEmpty(fallback) ? key : fallback;
        }

        /// <summary>Перевод заголовка команды интерфейса (ключ «cmd.&lt;id&gt;»).</summary>
        public static string Cmd(string id, string fallbackTitle)
        {
            return T("cmd." + id, fallbackTitle);
        }

        /// <summary>Перевод пояснения команды интерфейса (ключ «cmd.&lt;id&gt;.desc»).</summary>
        public static string CmdDesc(string id, string fallbackDescription)
        {
            return T("cmd." + id + ".desc", fallbackDescription);
        }

        /// <summary>
        /// Перевод пункта СТРОКИ МЕНЮ по его русскому имени: «Файл» → menu.file → «File».
        /// Пути команд (`MenuPath`) остаются русскими (они — часть кода), а подписи
        /// переводятся здесь, поэтому меню и раскладка не расходятся.
        /// </summary>
        public static string Menu(string ruName)
        {
            if (string.IsNullOrEmpty(ruName)) return ruName;
            switch (ruName)
            {
                case "Файл": return T("menu.file", ruName);
                case "Правка": return T("menu.edit", ruName);
                case "Вид": return T("menu.view", ruName);
                case "Робот": return T("menu.robot", ruName);
                case "Сервис": return T("menu.service", ruName);
                case "Справка": return T("menu.help", ruName);
                default: return T("menu.extra." + ruName, ruName);
            }
        }

        /// <summary>Перевод подписи-группы внутри выпадающего меню (ключ «menu.group.&lt;имя&gt;»).</summary>
        public static string MenuGroup(string ruName)
        {
            if (string.IsNullOrEmpty(ruName)) return ruName;
            return T("menu.group." + ruName, ruName);
        }

        /// <summary>
        /// Добавить строки в каталог языка ВО ВРЕМЯ РАБОТЫ (модули новых функций:
        /// стартовое меню, туториал, постобработка траекторий). Формат — пары
        /// «ключ, текст»; файлы-словари при этом не трогаются, а ключ ведёт себя
        /// как обычный: цепочка «текущий язык → английский → русский исходный текст»
        /// сохраняется полностью, поэтому неполный перевод не ломает интерфейс.
        /// </summary>
        public static void AddRuntimeStrings(string code, params string[] keyValuePairs)
        {
            if (string.IsNullOrEmpty(code) || keyValuePairs == null) return;
            Ensure();
            string c = code.Trim().ToLowerInvariant();
            for (int i = 0; i + 1 < keyValuePairs.Length; i += 2)
                AddString(c, keyValuePairs[i], keyValuePairs[i + 1]);
        }

        /// <summary>Сколько строк во всех каталогах (диагностика).</summary>
        public static int TotalStrings
        {
            get
            {
                Ensure();
                int total = 0;
                foreach (KeyValuePair<string, Dictionary<string, string>> kv in catalogs)
                    if (kv.Value != null) total += kv.Value.Count;
                return total;
            }
        }

        /// <summary>Есть ли ключ в текущем каталоге (диагностика покрытия).</summary>
        public static bool Has(string key)
        {
            Ensure();
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in catalogs)
                if (kv.Value != null && kv.Value.ContainsKey(key)) return true;
            return false;
        }

        /// <summary>
        /// Все ключи языка (диагностика и проверка ПОЛНОТЫ покрытия: любой ключ русского
        /// каталога обязан присутствовать в английском — это и есть гарантия «fallback на
        /// английский» из ТЗ).
        /// </summary>
        public static List<string> KeysOf(string code)
        {
            Ensure();
            var result = new List<string>();
            Dictionary<string, string> map;
            if (catalogs.TryGetValue(code ?? "", out map) && map != null)
                foreach (KeyValuePair<string, string> kv in map) result.Add(kv.Key);
            return result;
        }

        /// <summary>Сколько строк в каталоге языка (диагностика).</summary>
        public static int CountOf(string code)
        {
            Ensure();
            Dictionary<string, string> map;
            if (catalogs.TryGetValue(code ?? "", out map) && map != null) return map.Count;
            return 0;
        }

        /// <summary>
        /// ЭТАП 5: ВСЕ переводы ключа на всех языках одной строкой. Нужен палитре команд:
        /// ТЗ требует, чтобы команда находилась и по текущему языку, И по английскому,
        /// поэтому поиск идёт по этому тексту, а не только по подписи на экране.
        /// </summary>
        public static string AllLanguages(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            Ensure();
            StringBuilder sb = new StringBuilder(64);
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in catalogs)
            {
                if (kv.Value == null) continue;
                string s;
                if (kv.Value.TryGetValue(key, out s) && !string.IsNullOrEmpty(s))
                {
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(s);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Установить язык (код или «system»). Мгновенно применяется: перекрашивается тема
        /// (шрифт), пересобирается оболочка интерфейса и обновляются тексты панелей.
        /// </summary>
        public static void SetLanguage(string code, bool save = true)
        {
            Ensure();
            string wanted = string.IsNullOrEmpty(code) ? SystemCode : code;

            if (wanted != SystemCode && Find(wanted) == null)
            {
                Debug.LogWarning("[KvLoc] язык «" + wanted + "» неизвестен — оставлен текущий (" + current + ")");
                return;
            }

            preference = wanted;
            string resolved = Resolve(wanted);
            if (save)
            {
                try { PlayerPrefs.SetString(PrefsKey, preference); PlayerPrefs.Save(); }
                catch (Exception e) { Debug.LogWarning("[KvLoc] PlayerPrefs: " + e.Message); }
            }

            bool changed = resolved != current;
            current = resolved;
            if (changed) ReleaseFont();          // шрифт зависит от языка (CJK)

            Debug.Log("[KvLoc] язык: " + CurrentName + " (код " + current + ", выбор " + preference +
                      ") · строк " + CountOf(current) + (changed ? " · интерфейс пересобирается" : ""));

            if (Changed != null) Changed();
        }

        /// <summary>Следующий язык в списке (кнопка «Язык» в тулбаре/меню).</summary>
        public static string Cycle()
        {
            Ensure();
            if (langs.Count == 0) return current;
            int index = 0;
            for (int i = 0; i < langs.Count; i++)
                if (string.Equals(langs[i].code, current, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
            index = (index + 1) % langs.Count;
            SetLanguage(langs[index].code);
            return langs[index].code;
        }

        /// <summary>
        /// Шрифт интерфейса с учётом языка: для китайского/японского берётся системный шрифт
        /// с CJK-глифами (встроенный LegacyRuntime.ttf их не содержит — были бы «квадратики»).
        /// </summary>
        public static Font UiFont(int size)
        {
            Ensure();
            if (!NeedsCjk) return BuiltinFont();
            if (cjkFont != null) return cjkFont;

            string[] candidates =
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "Microsoft JhengHei",
                "Meiryo UI", "Meiryo", "Yu Gothic UI", "Yu Gothic", "MS Gothic", "MS UI Gothic",
                "SimHei", "SimSun", "Noto Sans CJK SC", "Noto Sans CJK JP", "Source Han Sans SC",
                "Arial Unicode MS"
            };
            cjkFont = UnityEngine.Font.CreateDynamicFontFromOSFont(candidates, Mathf.Max(12, size));
            if (cjkFont != null)
            {
                cjkFontName = cjkFont.name;
                Debug.Log("[KvLoc] шрифт CJK: " + cjkFontName + " (язык " + current + ")");
                return cjkFont;
            }

            string[] installed = UnityEngine.Font.GetOSInstalledFontNames();
            if (installed != null)
            {
                foreach (string n in installed)
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    if (n.IndexOf("Gothic", StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("Ming", StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("Hei", StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("Song", StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("YaHei", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    cjkFont = UnityEngine.Font.CreateDynamicFontFromOSFont(n, Mathf.Max(12, size));
                    if (cjkFont != null)
                    {
                        cjkFontName = n;
                        Debug.Log("[KvLoc] шрифт CJK подобран из системы: " + n);
                        return cjkFont;
                    }
                }
            }

            Debug.LogWarning("[KvLoc] системный шрифт с CJK-глифами не найден — " +
                             "китайский/японский текст может отображаться квадратами");
            return BuiltinFont();
        }

        /// <summary>Имя фактического шрифта (диагностика).</summary>
        public static string FontName
        {
            get { return NeedsCjk && !string.IsNullOrEmpty(cjkFontName) ? cjkFontName : "LegacyRuntime.ttf"; }
        }

        /// <summary>Сбросить кэш шрифта (после смены языка).</summary>
        public static void ReleaseFont()
        {
            cjkFont = null;
            cjkFontName = "";
        }

        /// <summary>Перечитать словари с диска (правка JSON без перезапуска).</summary>
        public static void Reload()
        {
            loaded = false;
            langs.Clear();
            catalogs.Clear();
            ReleaseFont();
            Ensure();
        }

        // ------------------------------------------------------------------ загрузка

        private static void Ensure()
        {
            if (loaded) return;
            loaded = true;
            filesLoaded = true;                 // §42/Д19: на диске словари читаются сразу

            try { preference = PlayerPrefs.GetString(PrefsKey, SystemCode); }
            catch { preference = SystemCode; }

            RegisterBuiltin();
            fileCount = LoadFromDisk();
            current = Resolve(preference);

            int total = 0;
            foreach (KvLangInfo l in langs)
            {
                l.strings = CountOf(l.code);
                total += l.strings;
            }
            loadStatus = "языков: " + langs.Count + " · файлов прочитано: " + fileCount +
                         " · строк всего: " + total + " · каталог: " + FolderPath +
                         (NeedsAsyncFileLoad() ? " · словари в пакете: читаются отдельно (UnityWebRequest)" : "");
        }

        /// <summary>
        /// НУЖНА ЛИ АСИНХРОННАЯ ЗАГРУЗКА СЛОВАРЕЙ. На Android и WebGL
        /// `Application.streamingAssetsPath` — это адрес ВНУТРИ пакета
        /// (`jar:file:///data/app/…/base.apk!/assets`), а не папка: `Directory.Exists` его не
        /// видит. Именно поэтому на шлеме 13.10.2026 в журнале было «файлов прочитано: 0»,
        /// и интерфейс оставался на русских литералах. Такие адреса читает `UnityWebRequest`.
        /// </summary>
        public static bool NeedsAsyncFileLoad()
        {
            try { return !Directory.Exists(FolderPath); }
            catch { return false; }
        }

        /// <summary>Адрес файла словаря: на устройстве он уже URL (`jar:`), на диске — путь.</summary>
        private static string FileUrl(string fileName)
        {
            string path = Path.Combine(FolderPath, fileName);
            if (path.IndexOf("://", StringComparison.Ordinal) >= 0) return path;
            return "file://" + path.Replace("\\", "/");
        }

        /// <summary>
        /// ВСЕ СЛОВАРИ С УСТРОЙСТВА ПРОЧИТАНЫ (§42, дефект Д19). На Android словари читаются
        /// асинхронно, по одному файлу (`ru.json`, `en.json`, `de.json`…). Пока они подгружаются,
        /// любой текст через `KvLoc.T` меняет значение на каждом шаге. В журнале шлема 25.10.2026
        /// одна и та же строка сингулярности напечаталась трижды: «СИНГУЛЯРНОСТЬ · Wrist · запястье»,
        /// «SINGULARITY · wrist · wrist», «SINGULARITÄT · Handgelenk · Handgelenk».
        /// Потребители журнальных строк спрашивают этот признак и не печатают до готовности каталога.
        /// </summary>
        public static bool FilesLoaded { get { return filesLoaded; } }

        private static bool filesLoaded;

        /// <summary>
        /// ДОГРУЗИТЬ СЛОВАРИ С УСТРОЙСТВА (Android/WebGL). Вызывается coroutine-ом из интерфейса
        /// ПОСЛЕ сборки оболочки: файлы читаются по одному, затем подписи пересобираются
        /// (событие <see cref="Changed"/>), поэтому оператор видит уже переведённый интерфейс.
        /// Ничего не ломает, если файлов нет: язык остаётся на встроенных строках.
        /// </summary>
        public static IEnumerator LoadFilesAsync()
        {
            if (asyncRunning) yield break;
            asyncRunning = true;

            int loaded = 0;
            int failed = 0;
            for (int i = 0; i < KnownFileCodes.Length; i++)
            {
                string code = KnownFileCodes[i];
                string url = FileUrl(code + ".json");
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        failed++;
                        Debug.LogWarning("[KvLoc] словарь " + code + " не прочитан с устройства: " +
                                         request.error + " · " + url);
                        continue;
                    }
                    string json = request.downloadHandler != null ? request.downloadHandler.text : null;
                    if (string.IsNullOrEmpty(json)) continue;
                    if (ApplyLanguageJson(json, code + ".json")) loaded++;
                }
            }

            asyncRunning = false;
            filesLoaded = true;                 // §42/Д19: каталог готов — журнальным строкам можно печатать
            fileCount += loaded;
            loadStatus = "языков: " + langs.Count + " · файлов прочитано: " + fileCount +
                         " · с устройства: " + loaded + (failed > 0 ? " (не прочитано: " + failed + ")" : "") +
                         " · каталог: " + FolderPath;
            Debug.Log("[KvLoc] словари с устройства: прочитано " + loaded +
                      (failed > 0 ? ", не удалось " + failed : "") + " · " + loadStatus);

            if (loaded > 0 && Changed != null) Changed();   // пересобрать подписи на загруженных словарях
        }

        /// <summary>
        /// Встроенный минимум (нужен, даже если StreamingAssets недоступен: подписи языков
        /// и самые ходовые строки). Основные словари лежат в файлах — их правят без кода.
        /// </summary>
        private static void RegisterBuiltin()
        {
            AddLang("ru", "Русский", "Russian", false);
            AddLang("en", "English", "English", false);
            AddLang("zh", "中文", "Chinese", true);
            AddLang("es", "Español", "Spanish", false);
            AddLang("de", "Deutsch", "German", false);
            AddLang("fr", "Français", "French", false);
            AddLang("ja", "日本語", "Japanese", true);

            AddString("en", "app.title", "KazistovVv");
            AddString("en", "common.stub", "in development");
            AddString("en", "common.on", "on");
            AddString("en", "common.off", "off");
            AddString("en", "status.theme", "Theme");
            AddString("en", "status.language", "Language");
            AddString("en", "settings.header.language", "Language");
            AddString("ru", "app.title", "KazistovVv");
            AddString("ru", "common.stub", "в разработке");
            AddString("ru", "common.on", "включено");
            AddString("ru", "common.off", "выключено");
            AddString("ru", "status.theme", "Тема");
            AddString("ru", "status.language", "Язык");
            AddString("ru", "settings.header.language", "Язык интерфейса");
        }

        /// <summary>
        /// Чтение каталогов из `StreamingAssets/kazistovvv_i18n/*.json`.
        /// Формат файла: { "code": "de", "name": "Deutsch", "english": "German",
        ///                 "cjk": false, "strings": [ { "key": "…", "text": "…" } ] }
        /// Незнакомый код файла автоматически становится новым языком в списке.
        /// </summary>
        /// <summary>
        /// ПРИМЕНИТЬ ОДИН СЛОВАРЬ ИЗ JSON. Общий путь для диска и для устройства: разбор один
        /// и тот же, поэтому словарь, прочитанный из пакета, ведёт себя как прочитанный с диска.
        /// Возвращает true, если язык добавлен и строки приняты.
        /// </summary>
        private static bool ApplyLanguageJson(string json, string fileLabel)
        {
            if (string.IsNullOrEmpty(json)) return false;

            KvLangFile parsed;
            try { parsed = JsonUtility.FromJson<KvLangFile>(json); }
            catch (Exception e)
            {
                Debug.LogWarning("[KvLoc] " + fileLabel + " — плохой JSON: " + e.Message);
                return false;
            }
            if (parsed == null) return false;

            string code = !string.IsNullOrEmpty(parsed.code)
                ? parsed.code.Trim().ToLowerInvariant()
                : Path.GetFileNameWithoutExtension(fileLabel).ToLowerInvariant();
            if (string.IsNullOrEmpty(code)) return false;

            bool cjk = parsed.cjk || code == "zh" || code == "ja" || code == "ko";
            string name = string.IsNullOrEmpty(parsed.name) ? code : parsed.name;
            string english = string.IsNullOrEmpty(parsed.english) ? name : parsed.english;
            AddLang(code, name, english, cjk, true);

            int added = 0;
            if (parsed.strings != null)
            {
                foreach (KvLangString s in parsed.strings)
                {
                    if (s == null || string.IsNullOrEmpty(s.key)) continue;
                    AddString(code, s.key, s.text);
                    added++;
                }
            }
            Debug.Log("[KvLoc] словарь " + code + " прочитан: " + added + " строк (" + fileLabel + ")");
            return true;
        }

        private static int LoadFromDisk()
        {
            int count = 0;
            try
            {
                string dir = FolderPath;
                if (!Directory.Exists(dir)) return 0;

                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    string json;
                    try { json = File.ReadAllText(file); }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[KvLoc] " + Path.GetFileName(file) + " не прочитан: " + e.Message);
                        continue;
                    }
                    if (string.IsNullOrEmpty(json)) continue;
                    if (ApplyLanguageJson(json, Path.GetFileName(file))) count++;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KvLoc] каталог словарей не прочитан: " + e.Message);
            }
            return count;
        }

        private static void AddLang(string code, string name, string english, bool cjk, bool fromFile = false)
        {
            KvLangInfo existing = Find(code);
            if (existing != null)
            {
                existing.name = name;
                existing.englishName = english;
                existing.needsCjk = cjk;
                existing.fromFile = existing.fromFile || fromFile;
                return;
            }
            KvLangInfo info = new KvLangInfo();
            info.code = code;
            info.name = name;
            info.englishName = english;
            info.needsCjk = cjk;
            info.fromFile = fromFile;
            langs.Add(info);
            catalogs[code] = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private static void AddString(string code, string key, string text)
        {
            if (string.IsNullOrEmpty(key)) return;
            Dictionary<string, string> map;
            if (!catalogs.TryGetValue(code, out map) || map == null)
            {
                map = new Dictionary<string, string>(StringComparer.Ordinal);
                catalogs[code] = map;
            }
            map[key] = text ?? "";
        }

        private static KvLangInfo Find(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (KvLangInfo l in langs)
                if (string.Equals(l.code, code, StringComparison.OrdinalIgnoreCase)) return l;
            return null;
        }

        /// <summary>Системный язык → код; неподдерживаемый системный → английский (ТЗ).</summary>
        private static string Resolve(string wanted)
        {
            if (!string.IsNullOrEmpty(wanted) && wanted != SystemCode && Find(wanted) != null)
                return wanted.ToLowerInvariant();

            string sys = SystemLanguageCode();
            if (Find(sys) != null) return sys;
            return "en";
        }

        private static string SystemLanguageCode()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                case SystemLanguage.Ukrainian:
                case SystemLanguage.Belarusian:
                    return "ru";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional:
                    return "zh";
                case SystemLanguage.Spanish:
                    return "es";
                case SystemLanguage.German:
                    return "de";
                case SystemLanguage.French:
                    return "fr";
                case SystemLanguage.Japanese:
                    return "ja";
                case SystemLanguage.Korean:
                    return "ko";
                default:
                    return "en";
            }
        }

        private static Font builtin;

        private static Font BuiltinFont()
        {
            if (builtin != null) return builtin;
            builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin == null) builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return builtin;
        }
    }

    /// <summary>Файл словаря (StreamingAssets/kazistovvv_i18n/&lt;код&gt;.json).</summary>
    [Serializable]
    public class KvLangFile
    {
        public string code = "";
        public string name = "";
        public string english = "";
        public bool cjk;
        public KvLangString[] strings = new KvLangString[0];
    }

    /// <summary>Одна строка словаря.</summary>
    [Serializable]
    public class KvLangString
    {
        public string key = "";
        public string text = "";
    }
}
