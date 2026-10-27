using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ПОДСТРОЙКА ЧИСЕЛ БЕЗ ПЕРЕСБОРКИ (сессия §37, требование владельца об опросах радиуса).
    ///
    /// ЗАЧЕМ ЭТО ВООБЩЕ. Владелец требует подбирать радиус панели ОПРОСАМИ: «ты показываешь
    /// вариант с конкретным значением, я смотрю в шлем и говорю больше/меньше/норма» ([S1.2]).
    /// Без такого механизма каждая итерация — это сборка APK (12–35 минут) и переустановка,
    /// то есть 2–3 часа на подбор одного числа. Файл подстройки сокращает итерацию до минуты:
    /// `adb push` нового значения — и приложение применяет его на ходу, печатая строку в журнал.
    ///
    /// ГДЕ ЛЕЖИТ ФАЙЛ. `Application.persistentDataPath/kv_xr_tuning.txt` — на шлеме это
    /// `/storage/emulated/0/Android/data/com.KazistovVv.app/files/kv_xr_tuning.txt`. Эта папка
    /// доступна `adb push` БЕЗ root, поэтому менять числа можно, не пересобирая приложение.
    /// Запуск по-прежнему ТОЛЬКО из библиотеки шлема ([F2]) — файл лишь меняет числа.
    ///
    /// ФОРМАТ. Построчно `ключ=значение`, строки, начинающиеся с `#`, — комментарии. Ключи
    /// приводятся к нижнему регистру (иначе `Panel.Radius` молча не сработал бы).
    /// Пустое значение, мусор и значения вне диапазона НЕ применяются — остаётся прежнее число:
    /// испортить настройку одной опечаткой нельзя.
    ///
    /// ЧЕГО ЭТО НЕ ДЕЛАЕТ: не заменяет настройки (PlayerPrefs) и не пишет ничего на диск.
    /// Файл — это ПОВЕРХ настройки: он только переопределяет число, пока файл существует.
    /// </summary>
    public static class KvXrTuning
    {
        /// <summary>Имя файла подстройки в папке данных приложения.</summary>
        public const string FileName = "kv_xr_tuning.txt";

        /// <summary>Как часто перечитывать файл, секунды. 2 с — «на ходу», без нагрузки на кадр.</summary>
        public const float ReloadIntervalSeconds = 2f;

        // ------------------------------------------------------------------ имена ключей

        public const string KeyPanelRadius = "panel.radius";
        public const string KeyPanelForward = "panel.forward";
        public const string KeyPanelUp = "panel.up";

        /// <summary>
        /// БЛИЖАЙШАЯ ДИСТАНЦИЯ ПАНЕЛИ ОТ ГЛАЗ (`panel.minDistanceEyes`). Владелец 21.10.2026:
        /// «сделай ближе на 30 см панельку на левой руке». Этот кламп и держал панель в 0,35 м от
        /// глаз — без него уменьшение выноса ничего не давало.
        /// </summary>
        public const string KeyPanelMinDistanceEyes = "panel.minDistanceEyes";
        public const string KeyRayHand = "ray.hand";
        public const string KeyVisuals = "visuals.mode";
        public const string KeyGestures = "gesture.enabled";

        /// <summary>
        /// РУЧНОЙ ПЕРЕКЛЮЧАТЕЛЬ СТОРОН (`sides.swap`). Владелец — единственный, кто видит, в какой
        /// руке контроллер; если автоматика ошиблась, он говорит «перепутано», и одна строка в файле
        /// меняет стороны на ходу, без пересборки. Значения: `1` — поменять местами, `0` — как есть,
        /// нет ключа — решает автоматика по геометрии.
        /// </summary>
        public const string KeySidesSwap = "sides.swap";

        /// <summary>
        /// ГЛУШИТЬ ЛИ ЖЕСТЫ РУКИ, В КОТОРОЙ КОНТРОЛЛЕР (`gesture.muteWithController`).
        ///
        /// ПО УМОЛЧАНИЮ ВЫКЛЮЧЕНО (21.10.2026). В третьей сборке §37 это было включено — и владелец,
        /// держа контроллеры, не мог показать ни один жест («щипок не работает, очень мало жестов»).
        /// Правильнее не глушить руку целиком, а требовать, чтобы жест был отчётливым: пороги,
        /// удержание и пауза уже это делают. Если появятся ложные срабатывания, ключ возвращает
        /// прежнее поведение без пересборки.
        /// </summary>
        public const string KeyMuteWithController = "gesture.muteWithController";

        /// <summary>
        /// ВТОРОЙ ЛУЧ ИЗ ВТОРОЙ РУКИ (`ray.secondary`): `1` — показывать (по умолчанию), `0` — выключить.
        /// Нужен, чтобы при желании убрать его без пересборки (§39).
        /// </summary>
        public const string KeySecondaryRay = "ray.secondary";

        // ------------------------------------------------------------------ границы

        /// <summary>Границы выноса панели вперёд от контроллера, метры.</summary>
        ///
        /// НИЖНЯЯ ГРАНИЦА СНИЖЕНА С 0,15 ДО 0,05 (21.10.2026): владелец попросил панель ближе,
        /// и прежний минимум не давал поставить её вплотную к руке.
        public const float MinPanelForwardMeters = 0.05f;
        public const float MaxPanelForwardMeters = 0.90f;

        /// <summary>Границы ближайшей дистанции панели от глаз, метры.</summary>
        public const float MinPanelDistanceEyesMeters = 0.10f;
        public const float MaxPanelDistanceEyesMeters = 1.20f;

        /// <summary>Границы подъёма панели над лучом, метры.</summary>
        public const float MinPanelUpMeters = -0.05f;
        public const float MaxPanelUpMeters = 0.25f;

        // ------------------------------------------------------------------ состояние

        private static Dictionary<string, string> values = new Dictionary<string, string>();
        private static bool fileFound;
        private static float nextReadTime;
        private static string lastRawText;
        private static string lastApplied;
        private static bool reportedAbsent;

        /// <summary>Полный путь к файлу подстройки (для журнала и для `adb push`).</summary>
        public static string FilePath
        {
            get
            {
                try { return Path.Combine(Application.persistentDataPath, FileName); }
                catch (Exception) { return FileName; }
            }
        }

        /// <summary>Файл подстройки найден и прочитан.</summary>
        public static bool FileFound { get { return fileFound; } }

        /// <summary>Сколько ключей применено (для журнала).</summary>
        public static int KeyCount { get { return values.Count; } }

        // ------------------------------------------------------------------ разбор (чистые функции)

        /// <summary>
        /// РАЗОБРАТЬ ТЕКСТ ПОДСТРОЙКИ. Чистая функция: ни файла, ни состояния — поэтому она
        /// проверяется тестами без шлема. Строки без `=`, пустые строки и комментарии
        /// пропускаются; ключ и значение обрезаются от пробелов; ключ приводится к нижнему регистру.
        /// </summary>
        public static Dictionary<string, string> Parse(string text)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return map;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line == null) continue;
                line = line.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#' || line[0] == ';') continue;

                int equals = line.IndexOf('=');
                if (equals <= 0 || equals >= line.Length - 1) continue;

                string key = line.Substring(0, equals).Trim().ToLowerInvariant();
                string value = line.Substring(equals + 1).Trim();
                if (key.Length == 0 || value.Length == 0) continue;
                map[key] = value;
            }
            return map;
        }

        /// <summary>
        /// ЧИСЛО ИЗ ФАЙЛА. Разбор НЕ зависит от языка системы (иначе на русской системе «0.125»
        /// не прочиталось бы, а «0,125» прочиталось — и числа вели бы себя по-разному на разных
        /// шлемах). NaN и бесконечность отвергаются: они превратили бы панель в ничто.
        /// </summary>
        public static bool TryParseFloat(string raw, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(raw)) return false;

            string trimmed = raw.Trim();
            if (trimmed.Length == 0) return false;

            float parsed;
            if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                return false;
            if (float.IsNaN(parsed) || float.IsInfinity(parsed)) return false;

            value = parsed;
            return true;
        }

        /// <summary>Радиус панели из карты ключей (ограниченный) либо прежнее значение.</summary>
        public static float ResolvePanelRadius(Dictionary<string, string> map, float fallback)
        {
            float value;
            if (map != null && map.TryGetValue(KeyPanelRadius, out string raw) && TryParseFloat(raw, out value))
                return KvXrGroupPanelLayout.ClampPanelRadius(value);
            return KvXrGroupPanelLayout.ClampPanelRadius(fallback);
        }

        /// <summary>Вынос панели вперёд из карты ключей (ограниченный) либо прежнее значение.</summary>
        public static float ResolvePanelForward(Dictionary<string, string> map, float fallback)
        {
            float value;
            if (map != null && map.TryGetValue(KeyPanelForward, out string raw) && TryParseFloat(raw, out value))
                return Mathf.Clamp(value, MinPanelForwardMeters, MaxPanelForwardMeters);
            return Mathf.Clamp(fallback, MinPanelForwardMeters, MaxPanelForwardMeters);
        }

        /// <summary>Подъём панели над лучом из карты ключей либо прежнее значение.</summary>
        public static float ResolvePanelUp(Dictionary<string, string> map, float fallback)
        {
            float value;
            if (map != null && map.TryGetValue(KeyPanelUp, out string raw) && TryParseFloat(raw, out value))
                return Mathf.Clamp(value, MinPanelUpMeters, MaxPanelUpMeters);
            return Mathf.Clamp(fallback, MinPanelUpMeters, MaxPanelUpMeters);
        }

        /// <summary>
        /// РУКА ЛУЧА ИЗ ФАЙЛА. Принимаются `right`/`правая` и `left`/`левая` — владелец может писать
        /// по-русски. Всё остальное оставляет прежнюю руку.
        /// </summary>
        public static KvXrQuickHand ResolveRayHand(Dictionary<string, string> map, KvXrQuickHand fallback)
        {
            string raw;
            if (map == null || !map.TryGetValue(KeyRayHand, out raw) || string.IsNullOrEmpty(raw)) return fallback;

            string v = raw.Trim().ToLowerInvariant();
            if (v == "left" || v == "левая" || v == "лево" || v == "0") return KvXrQuickHand.Left;
            if (v == "right" || v == "правая" || v == "право" || v == "1") return KvXrQuickHand.Right;
            return fallback;
        }

        /// <summary>Чем показывать контроллеры: `mesh`/`primitives`/`billboard`/`off` или 0…3.</summary>
        public static KvXrControllerVisualMode ResolveVisuals(Dictionary<string, string> map,
            KvXrControllerVisualMode fallback)
        {
            string raw;
            if (map == null || !map.TryGetValue(KeyVisuals, out raw) || string.IsNullOrEmpty(raw)) return fallback;

            string v = raw.Trim().ToLowerInvariant();
            switch (v)
            {
                case "mesh":
                case "меш":
                case "0": return KvXrControllerVisualMode.Mesh;
                case "primitives":
                case "примитивы":
                case "1": return KvXrControllerVisualMode.Primitives;
                case "billboard":
                case "значок":
                case "2": return KvXrControllerVisualMode.Billboard;
                case "off":
                case "выкл":
                case "3": return KvXrControllerVisualMode.Off;
                default: return fallback;
            }
        }

        /// <summary>Жесты включены: `1`/`on`/`вкл` — да, `0`/`off`/`выкл` — нет, иначе прежнее.</summary>
        public static bool ResolveGestures(Dictionary<string, string> map, bool fallback)
        {
            string raw;
            if (map == null || !map.TryGetValue(KeyGestures, out raw) || string.IsNullOrEmpty(raw)) return fallback;

            string v = raw.Trim().ToLowerInvariant();
            if (v == "1" || v == "on" || v == "вкл" || v == "да" || v == "true") return true;
            if (v == "0" || v == "off" || v == "выкл" || v == "нет" || v == "false") return false;
            return fallback;
        }

        /// <summary>
        /// РУЧНОЙ ПЕРЕКЛЮЧАТЕЛЬ СТОРОН: `true` — поменять руки местами, `false` — не менять,
        /// `null` — ключа нет, и стороны определяет автоматика по геометрии.
        /// </summary>
        public static bool? ResolveSidesSwap(Dictionary<string, string> map)
        {
            string raw;
            if (map == null || !map.TryGetValue(KeySidesSwap, out raw) || string.IsNullOrEmpty(raw)) return null;

            string v = raw.Trim().ToLowerInvariant();
            if (v == "1" || v == "on" || v == "да" || v == "true" || v == "поменять") return true;
            if (v == "0" || v == "off" || v == "нет" || v == "false" || v == "как есть") return false;
            return null;
        }

        // ------------------------------------------------------------------ чтение файла

        /// <summary>
        /// ПЕРЕЧИТАТЬ ФАЙЛ, ЕСЛИ ПОРА. Зовётся каждый кадр из `KvXrRig`; внутри — не чаще, чем
        /// раз в <see cref="ReloadIntervalSeconds"/> секунд. Возвращает строку для журнала, если
        /// что-то изменилось, иначе null.
        /// </summary>
        public static string Tick()
        {
            if (Time.realtimeSinceStartup < nextReadTime) return null;
            nextReadTime = Time.realtimeSinceStartup + ReloadIntervalSeconds;
            return Reload();
        }

        /// <summary>
        /// ПЕРЕЧИТАТЬ ФАЙЛ НЕМЕДЛЕННО. Возвращает строку для журнала, если значения изменились
        /// (или файл появился/пропал), иначе null: журнал не должен забиваться одним и тем же.
        /// </summary>
        public static string Reload()
        {
            string text = null;
            bool exists = false;
            try
            {
                string path = FilePath;
                if (File.Exists(path))
                {
                    exists = true;
                    text = File.ReadAllText(path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KvXr] подстройка: файл не прочитан (" + e.GetType().Name + ": " + e.Message + ")");
                text = null;
                exists = false;
            }

            if (!exists)
            {
                bool hadValues = values.Count > 0;
                fileFound = false;
                values = new Dictionary<string, string>();
                lastRawText = null;
                if (hadValues) return BuildLogLine("файл подстройки убран — работают настройки по умолчанию");
                if (reportedAbsent) return null;
                reportedAbsent = true;
                return "[KvXr] подстройка: файла нет (" + FilePath + ") — работают настройки по умолчанию";
            }

            if (string.Equals(text, lastRawText, StringComparison.Ordinal)) return null;

            lastRawText = text;
            fileFound = true;
            values = Parse(text);

            string line = BuildLogLine("файл подстройки применён");
            if (line == lastApplied) return null;
            lastApplied = line;
            return line;
        }

        /// <summary>
        /// СТРОКА-ДОКАЗАТЕЛЬСТВО ПОДСТРОЙКИ. В ней — ровно те числа, которые поедут в шлем,
        /// поэтому владелец видит в журнале то же, что и на картинке.
        /// </summary>
        public static string Describe()
        {
            return BuildLogLine("подстройка");
        }

        private static string BuildLogLine(string what)
        {
            bool? swap = SidesSwap;
            string sides = swap.HasValue ? (swap.Value ? "РУЧНОЙ: поменять" : "РУЧНОЙ: как есть") : "авто";
            return string.Format(CultureInfo.InvariantCulture,
                "[KvXr] ПОДСТРОЙКА: {0} · ключей {1} · панель.радиус {2:0.000} м · панель.вперёд {3:0.000} м · " +
                "панель.отГлаз {4:0.000} м · луч {5} · визуал {6} · жесты {7} · стороны {8}",
                what, values.Count, PanelRadiusMeters, PanelForwardMeters, PanelMinDistanceEyesMeters,
                RayHand == KvXrQuickHand.Right ? "правая" : "левая",
                KvXrControllerVisuals.ModeName(VisualsMode),
                GesturesEnabled ? "включены" : "выключены",
                sides);
        }

        // ------------------------------------------------------------------ действующие значения

        /// <summary>Радиус панели: файл → иначе настройка → иначе значение по умолчанию.</summary>
        public static float PanelRadiusMeters
        {
            get { return ResolvePanelRadius(values, KvXrSettings.GetGroupPanelRadius()); }
        }

        /// <summary>Вынос панели вперёд от контроллера, метры.</summary>
        public static float PanelForwardMeters
        {
            get { return ResolvePanelForward(values, KvXrGroupPanel.DefaultHandForwardMeters); }
        }

        /// <summary>Подъём панели над лучом контроллера, метры.</summary>
        public static float PanelUpMeters
        {
            get { return ResolvePanelUp(values, KvXrGroupPanel.DefaultHandUpMeters); }
        }

        /// <summary>Ближайшая дистанция панели от глаз: файл → иначе значение по умолчанию.</summary>
        public static float PanelMinDistanceEyesMeters
        {
            get
            {
                float value;
                if (values.TryGetValue(KeyPanelMinDistanceEyes, out string raw) && TryParseFloat(raw, out value))
                    return Mathf.Clamp(value, MinPanelDistanceEyesMeters, MaxPanelDistanceEyesMeters);
                return KvXrGroupPanel.DefaultHandMinDistanceFromEyes;
            }
        }

        /// <summary>Разбор `panel.minDistanceEyes` как чистая функция (для тестов).</summary>
        public static float ResolvePanelMinDistanceEyes(Dictionary<string, string> map, float fallback)
        {
            float value;
            if (map != null && map.TryGetValue(KeyPanelMinDistanceEyes, out string raw) && TryParseFloat(raw, out value))
                return Mathf.Clamp(value, MinPanelDistanceEyesMeters, MaxPanelDistanceEyesMeters);
            return Mathf.Clamp(fallback, MinPanelDistanceEyesMeters, MaxPanelDistanceEyesMeters);
        }

        /// <summary>Рука, из которой идёт луч: файл → иначе настройка.</summary>
        public static KvXrQuickHand RayHand
        {
            get { return ResolveRayHand(values, KvXrSettings.GetRayHand()); }
        }

        /// <summary>Чем показывать контроллеры.</summary>
        public static KvXrControllerVisualMode VisualsMode
        {
            get
            {
                return ResolveVisuals(values,
                    KvXrControllerVisuals.ModeFromIndex(KvXrSettings.GetControllerVisualMode()));
            }
        }

        /// <summary>Жесты включены.</summary>
        public static bool GesturesEnabled
        {
            get { return ResolveGestures(values, KvXrSettings.GetGesturesEnabled()); }
        }

        /// <summary>Глушить ли жесты руки с контроллером. По умолчанию — НЕТ (§37, третья сборка).</summary>
        public static bool MuteGesturesWithController
        {
            get
            {
                string raw;
                if (!values.TryGetValue(KeyMuteWithController, out raw) || string.IsNullOrEmpty(raw)) return false;

                string v = raw.Trim().ToLowerInvariant();
                return v == "1" || v == "on" || v == "вкл" || v == "да" || v == "true";
            }
        }

        /// <summary>Ручной переключатель сторон: `true`/`false` — из файла, `null` — автоматика.</summary>
        public static bool? SidesSwap
        {
            get { return ResolveSidesSwap(values); }
        }

        /// <summary>Показывать ли второй луч. Ключа нет — берётся НАСТРОЙКА (по умолчанию включено).</summary>
        public static bool SecondaryRayEnabled
        {
            get
            {
                string raw;
                if (!values.TryGetValue(KeySecondaryRay, out raw) || string.IsNullOrEmpty(raw))
                    return KvXrSettings.GetSecondaryRay();

                string v = raw.Trim().ToLowerInvariant();
                if (v == "0" || v == "off" || v == "выкл" || v == "нет" || v == "false") return false;
                return true;
            }
        }

        /// <summary>Разбор `ray.secondary` как чистая функция (для тестов).</summary>
        public static bool ResolveSecondaryRay(Dictionary<string, string> map)
        {
            if (map == null) return true;
            string raw;
            if (!map.TryGetValue(KeySecondaryRay, out raw) || string.IsNullOrEmpty(raw)) return true;

            string v = raw.Trim().ToLowerInvariant();
            if (v == "0" || v == "off" || v == "выкл" || v == "нет" || v == "false") return false;
            return true;
        }

        /// <summary>Разбор `gesture.muteWithController` как чистая функция (для тестов).</summary>
        public static bool ResolveMuteWithController(Dictionary<string, string> map)
        {
            string raw;
            if (map == null || !map.TryGetValue(KeyMuteWithController, out raw) || string.IsNullOrEmpty(raw))
                return false;

            string v = raw.Trim().ToLowerInvariant();
            return v == "1" || v == "on" || v == "вкл" || v == "да" || v == "true";
        }
    }
}
