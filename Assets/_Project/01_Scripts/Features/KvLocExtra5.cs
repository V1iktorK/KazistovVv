using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЛОКАЛИЗАЦИЯ XR-ОВЕРЛЕЯ: ПАНЕЛЬ У РУКИ, ОКНА И ПОЯС (сессия §32, решения владельца [D20]–[D22]).
    ///
    /// Устроено так же, как <see cref="KvLocExtra"/>, <see cref="KvLocExtra2"/>,
    /// <see cref="KvLocExtra3"/> и <see cref="KvLocExtra4"/>: таблица «ключ + семь переводов»
    /// регистрируется во время работы через `KvLoc.AddRuntimeStrings`. Внешние словари
    /// (`StreamingAssets/kazistovvv_i18n/*.json`) остаются ГЛАВНЫМИ: если ключ там есть,
    /// значение отсюда его не перекрывает.
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ МОДУЛЬ, А НЕ СТРОКИ В КОДЕ. Правило проекта: у каждой новой строки
    /// интерфейса есть перевод на все 7 языков — иначе растёт долг локализации
    /// (`Docs/LOCALIZATION_AUDIT.md`: 205 непереведённых ключей). Все ключи раздела начинаются
    /// с `xr.`, поэтому полнота перевода проверяется тестом `KvXrOverlayTests`.
    /// </summary>
    public static class KvLocExtra5
    {
        private static bool installed;
        private static int registered;

        /// <summary>Сколько строк зарегистрировано модулем (диагностика).</summary>
        public static int RegisteredCount { get { return registered; } }

        /// <summary>Сколько строк в таблице модуля (для диагностики и тестов полноты).</summary>
        public static int RowCount { get { return Rows.Length; } }

        /// <summary>Ключ строки таблицы по номеру (для тестов полноты перевода 7 языков).</summary>
        public static string KeyAt(int index)
        {
            if (index < 0 || index >= Rows.Length) return "";
            return Rows[index] != null && Rows[index].Length > 0 ? Rows[index][0] : "";
        }

        /// <summary>
        /// ТЕКСТ СТРОКИ НА ЯЗЫКЕ ПО НОМЕРУ (0 — ru, 1 — en, … 6 — ja; порядок как в
        /// `KvLocExtra.Codes`). Нужен тесту полноты: у каждой новой строки обязан быть перевод
        /// на все 7 языков, иначе растёт долг локализации.
        /// </summary>
        public static string TextAt(int index, int languageIndex)
        {
            if (index < 0 || index >= Rows.Length) return "";
            string[] row = Rows[index];
            if (row == null) return "";
            int at = languageIndex + 1;
            if (at < 1 || at >= row.Length) return "";
            return row[at] ?? "";
        }

        /// <summary>Сколько языков в строке таблицы (без ключа) — для проверки полноты.</summary>
        public static int LanguageCount(int index)
        {
            if (index < 0 || index >= Rows.Length || Rows[index] == null) return 0;
            return Rows[index].Length - 1;
        }

        /// <summary>Перевод по ключу (fallback — русский текст из кода).</summary>
        public static string T(string key, string fallback)
        {
            return KvLoc.T(key, fallback);
        }

        /// <summary>Перевод с подстановкой: {0}, {1} — как в string.Format.</summary>
        public static string F(string key, string fallback, params object[] args)
        {
            string pattern = KvLoc.T(key, fallback);
            try { return string.Format(pattern, args); }
            catch { return pattern; }
        }

        /// <summary>Зарегистрировать строки раздела (вызывается один раз при сборке интерфейса).</summary>
        public static void Install()
        {
            if (installed) return;
            installed = true;

            int count = 0;
            int skipped = 0;
            for (int r = 0; r < Rows.Length; r++)
            {
                string[] row = Rows[r];
                if (row == null || row.Length < 1 + KvLocExtra.Codes.Length) continue;
                string key = row[0];
                if (string.IsNullOrEmpty(key)) continue;
                if (KvLoc.Has(key)) { skipped++; continue; }

                bool wrote = false;
                for (int c = 0; c < KvLocExtra.Codes.Length; c++)
                {
                    string text = row[c + 1];
                    if (string.IsNullOrEmpty(text)) continue;
                    KvLoc.AddRuntimeStrings(KvLocExtra.Codes[c], key, text);
                    wrote = true;
                }
                if (wrote) count++;
            }
            registered = count;
            UnityEngine.Debug.Log("[Loc] строки XR-оверлея зарегистрированы: " + count +
                                  " (уже были в словарях: " + skipped + ")" +
                                  " · всего в словарях: " + KvLoc.TotalStrings);
        }

        // ================================================================== таблица строк

        private static readonly string[][] Rows =
        {
            // ---------------------------------------------------------- панель у руки [D20]
            Row("xr.quick.title",
                "Быстрые кнопки", "Quick buttons", "快捷按钮", "Botones rápidos",
                "Schnellzugriff", "Boutons rapides", "クイックボタン"),
            Row("xr.quick.section",
                "Панель у руки", "Panel at hand", "手部面板", "Panel en la mano",
                "Panel an der Hand", "Panneau à la main", "手元パネル"),
            Row("xr.quick.shape",
                "Форма панели", "Panel shape", "面板形状", "Forma del panel",
                "Form des Panels", "Forme du panneau", "パネルの形状"),
            Row("xr.quick.shape.ring",
                "Бублик", "Ring", "圆环", "Anillo",
                "Ring", "Anneau", "リング"),
            Row("xr.quick.shape.crescent",
                "Полумесяц", "Crescent", "月牙", "Media luna",
                "Halbmond", "Croissant", "三日月"),
            Row("xr.quick.hand",
                "Рука", "Hand", "手", "Mano",
                "Hand", "Main", "手"),
            Row("xr.quick.hand.left",
                "Левая", "Left", "左手", "Izquierda",
                "Links", "Gauche", "左手"),
            Row("xr.quick.hand.right",
                "Правая", "Right", "右手", "Derecha",
                "Rechts", "Droite", "右手"),
            Row("xr.quick.radius",
                "Радиус панели", "Panel radius", "面板半径", "Radio del panel",
                "Panelradius", "Rayon du panneau", "パネルの半径"),
            Row("xr.quick.visible",
                "Показывать панель у руки", "Show hand panel", "显示手部面板",
                "Mostrar el panel en la mano", "Panel an der Hand zeigen",
                "Afficher le panneau à la main", "手元パネルを表示"),

            // ---------------------------------------------------------- окна [D21]
            Row("xr.window.expanded",
                "Открыто", "Open", "已打开", "Abierta",
                "Geöffnet", "Ouverte", "開いています"),
            Row("xr.window.collapse",
                "Свернуть", "Collapse", "折叠", "Contraer",
                "Einklappen", "Réduire", "折りたたむ"),
            Row("xr.window.close",
                "Закрыть", "Close", "关闭", "Cerrar",
                "Schließen", "Fermer", "閉じる"),

            // ---------------------------------------------------------- пояс [D22]
            Row("xr.belt.title",
                "Пояс", "Belt", "腰带", "Cinturón",
                "Gürtel", "Ceinture", "ベルト"),
            Row("xr.belt.hint",
                "Скрытые окна лежат на поясе — возьмите окно, чтобы вернуть его на место",
                "Hidden windows wait on the belt — take one to bring it back",
                "隐藏的窗口放在腰带上——取回即可恢复",
                "Las ventanas ocultas esperan en el cinturón: tome una para devolverla",
                "Versteckte Fenster liegen am Gürtel — zum Zurückholen greifen",
                "Les fenêtres masquées attendent à la ceinture — prenez-en une pour la rouvrir",
                "隠したウィンドウはベルトにあります — 取ると元に戻ります"),
            Row("xr.belt.full",
                "На поясе нет места", "The belt is full", "腰带已满", "El cinturón está lleno",
                "Der Gürtel ist voll", "La ceinture est pleine", "ベルトがいっぱいです"),
            Row("xr.belt.pin",
                "Закрепить на поясе", "Pin to belt", "固定到腰带", "Fijar al cinturón",
                "Am Gürtel befestigen", "Épingler à la ceinture", "ベルトに固定"),
        };

        private static string[] Row(string key, string ru, string en, string zh, string es,
            string de, string fr, string ja)
        {
            return new[] { key, ru, en, zh, es, de, fr, ja };
        }
    }
}
