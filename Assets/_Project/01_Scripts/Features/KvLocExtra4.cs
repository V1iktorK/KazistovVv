using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЛОКАЛИЗАЦИЯ РАЗДЕЛА «ГРАФИКА» (ЭТАП 6 ТЗ) — 7 языков: RU / EN / ZH / ES / DE / FR / JA.
    ///
    /// Устроено так же, как <see cref="KvLocExtra"/>, <see cref="KvLocExtra2"/> и
    /// <see cref="KvLocExtra3"/>: таблица «ключ + семь переводов» регистрируется во время
    /// работы через `KvLoc.AddRuntimeStrings`. Внешние словари
    /// (`StreamingAssets/kazistovvv_i18n/*.json`) остаются ГЛАВНЫМИ: если ключ там есть,
    /// значение отсюда не перекрывает его. Цепочка «текущий язык → английский → русский
    /// текст из кода» сохраняется полностью, поэтому неполный перевод ничего не ломает.
    ///
    /// Все ключи раздела начинаются с `graphics.`; заголовки команд — штатные `cmd.*`.
    /// </summary>
    public static class KvLocExtra4
    {
        private static bool installed;
        private static int registered;

        /// <summary>Сколько строк зарегистрировано модулем (диагностика).</summary>
        public static int RegisteredCount { get { return registered; } }

        /// <summary>Сколько строк в таблице модуля (для диагностики и тестов покрытия).</summary>
        public static int RowCount { get { return Rows.Length; } }

        /// <summary>Ключ строки таблицы по номеру (для тестов полноты перевода 7 языков).</summary>
        public static string KeyAt(int index)
        {
            if (index < 0 || index >= Rows.Length) return "";
            return Rows[index] != null && Rows[index].Length > 0 ? Rows[index][0] : "";
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

        /// <summary>Зарегистрировать строки раздела (вызывается службой графики один раз).</summary>
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
            UnityEngine.Debug.Log("[Loc] строки раздела «Графика» зарегистрированы: " + count +
                                  " (уже были в словарях: " + skipped + ")" +
                                  " · всего в словарях: " + KvLoc.TotalStrings);
        }

        // ================================================================== таблица строк

        private static readonly string[][] Rows =
        {
            // ---------------------------------------------------------- заголовки разделов
            Row("graphics.tab.title",
                "Графика", "Graphics", "图形", "Gráficos", "Grafik", "Graphismes", "グラフィック"),
            Row("graphics.section.preset",
                "Режим", "Preset", "预设", "Preset", "Voreinstellung", "Préréglage", "プリセット"),
            Row("graphics.section.quality",
                "Качество", "Quality", "画质", "Calidad", "Qualität", "Qualité", "画質"),
            Row("graphics.section.performance",
                "Производительность", "Performance", "性能", "Rendimiento", "Leistung",
                "Performances", "パフォーマンス"),
            Row("graphics.section.diagnostics",
                "Диагностика", "Diagnostics", "诊断", "Diagnóstico", "Diagnose", "Diagnostic", "診断"),
            Row("graphics.section.service",
                "Служебное (для разработки)", "Service (development)", "开发用", "Servicio (desarrollo)",
                "Service (Entwicklung)", "Service (développement)", "開発用"),

            // ---------------------------------------------------------- пресеты
            Row("graphics.preset.auto", "Авто", "Auto", "自动", "Automático", "Automatisch", "Auto", "自動"),
            Row("graphics.preset.low", "Низкое", "Low", "低", "Bajo", "Niedrig", "Faible", "低"),
            Row("graphics.preset.medium", "Среднее", "Medium", "中", "Medio", "Mittel", "Moyen", "中"),
            Row("graphics.preset.high", "Высокое", "High", "高", "Alto", "Hoch", "Élevé", "高"),
            Row("graphics.preset.ultra", "Ультра", "Ultra", "超高", "Ultra", "Ultra", "Ultra", "ウルトラ"),
            Row("graphics.preset.vrready", "VR-Ready", "VR-Ready", "VR-Ready", "VR-Ready",
                "VR-Ready", "VR-Ready", "VR-Ready"),
            Row("graphics.preset.custom", "Свой", "Custom", "自定义", "Personalizado", "Eigene",
                "Personnalisé", "カスタム"),
            Row("graphics.preset.note",
                "«Авто» выбирает пресет по железу и пересчитывает его, если конфигурация изменилась. " +
                "Любая правка ползунка ниже переводит режим в «Свой»: сам пресет при этом не портится — " +
                "вернуться к нему можно выбором режима заново.",
                "Auto picks a preset from your hardware and recalculates it when the configuration " +
                "changes. Editing any control below switches the mode to Custom: the preset itself is " +
                "untouched, so you can always select it again.",
                "“自动”会根据硬件选择预设，并在配置变化时重新计算。修改下方任意控件会切换到“自定义”，" +
                "预设本身不会被破坏，随时可以重新选择。",
                "«Automático» elige el preset según el hardware y lo recalcula si la configuración cambia. " +
                "Al tocar cualquier control el modo pasa a «Personalizado»: el preset no se altera y " +
                "puede volver a elegirlo.",
                "„Automatisch“ wählt die Voreinstellung anhand der Hardware und berechnet sie bei " +
                "geänderter Konfiguration neu. Jede Änderung unten schaltet auf „Eigene“ um — die " +
                "Voreinstellung selbst bleibt unverändert.",
                "« Auto » choisit le préréglage selon le matériel et le recalcule si la configuration " +
                "change. Toute modification ci-dessous passe en « Personnalisé » : le préréglage reste " +
                "intact et peut être resélectionné.",
                "「自動」はハードウェアに応じてプリセットを選び、構成が変われば再計算します。下の項目を" +
                "変更すると「カスタム」に切り替わりますが、プリセット自体は壊れず、再選択できます。"),

            // ---------------------------------------------------------- автоопределение
            Row("graphics.detect",
                "Определить по железу", "Detect by hardware", "按硬件检测", "Detectar por hardware",
                "Anhand der Hardware erkennen", "Détecter selon le matériel", "ハードウェアで判定"),
            Row("graphics.detect.result",
                "Обнаружено: {0} · Автовыбор: {1}", "Detected: {0} · Auto choice: {1}",
                "已检测：{0} · 自动选择：{1}", "Detectado: {0} · Selección automática: {1}",
                "Erkannt: {0} · Automatische Wahl: {1}", "Détecté : {0} · Choix automatique : {1}",
                "検出：{0} · 自動選択：{1}"),
            Row("graphics.detect.done",
                "Определение по железу: {0} · автовыбор: {1}", "Hardware detection: {0} · auto choice: {1}",
                "硬件检测：{0} · 自动选择：{1}", "Detección de hardware: {0} · elección automática: {1}",
                "Hardware-Erkennung: {0} · automatische Wahl: {1}",
                "Détection matérielle : {0} · choix automatique : {1}",
                "ハードウェア検出：{0} · 自動選択：{1}"),
            Row("graphics.detect.nodata",
                "сведения о железе недоступны", "hardware information is unavailable",
                "无法获取硬件信息", "no hay datos del hardware", "Hardware-Informationen nicht verfügbar",
                "informations matérielles indisponibles", "ハードウェア情報を取得できません"),
            Row("graphics.class.unknown",
                "не определён", "unknown", "未确定", "sin determinar", "unbestimmt", "indéterminé", "未判定"),

            // ---------------------------------------------------------- обоснование выбора
            Row("graphics.reason.xr",
                "обнаружен VR-шлем {0} — пресет VR-Ready", "VR headset {0} detected — VR-Ready preset",
                "检测到 VR 头显 {0} — 使用 VR-Ready 预设", "se detectó el visor VR {0} — preset VR-Ready",
                "VR-Headset {0} erkannt — Voreinstellung VR-Ready",
                "casque VR {0} détecté — préréglage VR-Ready", "VR ヘッドセット {0} を検出 — VR-Ready"),
            Row("graphics.reason.nocompute",
                "нет поддержки вычислительных шейдеров — выбран низкий пресет",
                "no compute shader support — low preset selected",
                "不支持计算着色器 — 已选择低画质", "sin soporte de compute shaders — preset bajo",
                "keine Compute-Shader-Unterstützung — niedrige Voreinstellung",
                "pas de prise en charge des compute shaders — préréglage faible",
                "コンピュートシェーダ非対応 — 低プリセットを選択"),
            Row("graphics.reason.integrated",
                "встроенная графика {0} — выбран низкий пресет", "integrated graphics {0} — low preset selected",
                "集成显卡 {0} — 已选择低画质", "gráficos integrados {0} — preset bajo",
                "integrierte Grafik {0} — niedrige Voreinstellung",
                "graphique intégré {0} — préréglage faible", "内蔵 GPU {0} — 低プリセットを選択"),
            Row("graphics.reason.ultra.rt",
                "VRAM {0} ГБ и поддержка трассировки лучей — пресет «Ультра»",
                "VRAM {0} GB and ray tracing support — Ultra preset",
                "显存 {0} GB 且支持光线追踪 — 超高预设", "VRAM {0} GB y trazado de rayos — preset Ultra",
                "VRAM {0} GB und Raytracing — Voreinstellung Ultra",
                "VRAM {0} Go et ray tracing — préréglage Ultra", "VRAM {0} GB とレイトレーシング対応 — ウルトラ"),
            Row("graphics.reason.ultra",
                "VRAM {0} ГБ — пресет «Ультра»", "VRAM {0} GB — Ultra preset",
                "显存 {0} GB — 超高预设", "VRAM {0} GB — preset Ultra", "VRAM {0} GB — Voreinstellung Ultra",
                "VRAM {0} Go — préréglage Ultra", "VRAM {0} GB — ウルトラ"),
            Row("graphics.reason.high",
                "VRAM {0} ГБ, дискретная видеокарта среднего уровня — пресет «Высокое»",
                "VRAM {0} GB, mid-range discrete GPU — High preset",
                "显存 {0} GB，中端独立显卡 — 高预设", "VRAM {0} GB, GPU dedicada de gama media — preset Alto",
                "VRAM {0} GB, dedizierte Mittelklasse-GPU — Voreinstellung Hoch",
                "VRAM {0} Go, GPU dédiée milieu de gamme — préréglage Élevé",
                "VRAM {0} GB、ミドルクラス GPU — 高プリセット"),
            Row("graphics.reason.medium",
                "VRAM {0} ГБ, видеокарта начального уровня — пресет «Среднее»",
                "VRAM {0} GB, entry-level GPU — Medium preset",
                "显存 {0} GB，入门级显卡 — 中预设", "VRAM {0} GB, GPU de gama básica — preset Medio",
                "VRAM {0} GB, Einstiegs-GPU — Voreinstellung Mittel",
                "VRAM {0} Go, GPU d'entrée de gamme — préréglage Moyen",
                "VRAM {0} GB、エントリー GPU — 中プリセット"),
            Row("graphics.reason.vram.low",
                "VRAM {0} ГБ (меньше 2 ГБ) — выбран низкий пресет",
                "VRAM {0} GB (less than 2 GB) — low preset selected",
                "显存 {0} GB（不足 2 GB）— 已选择低画质", "VRAM {0} GB (menos de 2 GB) — preset bajo",
                "VRAM {0} GB (unter 2 GB) — niedrige Voreinstellung",
                "VRAM {0} Go (moins de 2 Go) — préréglage faible", "VRAM {0} GB（2 GB 未満）— 低プリセット"),
            Row("graphics.reason.ram.low",
                "оперативной памяти {0} ГБ (меньше 4) — пресет понижен до низкого",
                "system memory {0} GB (less than 4) — preset lowered to Low",
                "内存 {0} GB（不足 4）— 已降至低预设", "memoria {0} GB (menos de 4) — preset rebajado a Bajo",
                "Arbeitsspeicher {0} GB (unter 4) — Voreinstellung auf Niedrig gesenkt",
                "mémoire {0} Go (moins de 4) — préréglage abaissé à Faible",
                "メモリ {0} GB（4 未満）— 低プリセットに降格"),
            Row("graphics.reason.ram.mid",
                "оперативной памяти {0} ГБ (меньше 8) — пресет понижен до среднего",
                "system memory {0} GB (less than 8) — preset lowered to Medium",
                "内存 {0} GB（不足 8）— 已降至中预设", "memoria {0} GB (menos de 8) — preset rebajado a Medio",
                "Arbeitsspeicher {0} GB (unter 8) — Voreinstellung auf Mittel gesenkt",
                "mémoire {0} Go (moins de 8) — préréglage abaissé à Moyen",
                "メモリ {0} GB（8 未満）— 中プリセットに降格"),
            Row("graphics.reason.cpu",
                "два ядра процессора — пресет не выше среднего",
                "two CPU cores — preset not above Medium",
                "处理器仅两个核心 — 预设不高于中", "dos núcleos de CPU — preset no superior a Medio",
                "zwei CPU-Kerne — Voreinstellung höchstens Mittel",
                "deux cœurs CPU — préréglage au plus Moyen", "CPU 2 コア — 中プリセットまで"),
            Row("graphics.hw.changed",
                "конфигурация железа изменилась — пресет автоопределения пересчитан",
                "hardware configuration changed — auto preset recalculated",
                "硬件配置已变化 — 已重新计算自动预设",
                "la configuración de hardware cambió — preset automático recalculado",
                "Hardware-Konfiguration geändert — automatische Voreinstellung neu berechnet",
                "configuration matérielle modifiée — préréglage automatique recalculé",
                "ハードウェア構成が変化 — 自動プリセットを再計算"),
            Row("graphics.hw.auto",
                "хеш железа изменился — пресет «Авто» пересчитан: {0} ({1})",
                "hardware hash changed — Auto preset recalculated: {0} ({1})",
                "硬件哈希已变化 — 已重算“自动”预设：{0}（{1}）",
                "el hash del hardware cambió — preset «Automático» recalculado: {0} ({1})",
                "Hardware-Hash geändert — „Automatisch“ neu berechnet: {0} ({1})",
                "empreinte matérielle modifiée — préréglage « Auto » recalculé : {0} ({1})",
                "ハードウェアハッシュが変化 — 「自動」を再計算：{0}（{1}）"),

            // ---------------------------------------------------------- первый запуск и плашки
            Row("graphics.firstrun",
                "Графика настроена автоматически: {0} ({1}). Изменить: Настройки → Графика.",
                "Graphics configured automatically: {0} ({1}). Change it in Settings → Graphics.",
                "已自动配置图形：{0}（{1}）。可在 设置 → 图形 中修改。",
                "Gráficos configurados automáticamente: {0} ({1}). Cambiar en Ajustes → Gráficos.",
                "Grafik automatisch eingestellt: {0} ({1}). Ändern unter Einstellungen → Grafik.",
                "Graphismes configurés automatiquement : {0} ({1}). Modifier dans Paramètres → Graphismes.",
                "グラフィックを自動設定しました：{0}（{1}）。設定 → グラフィック で変更できます。"),
            Row("graphics.toast.autoset",
                "Графика: {0} · подобрано по железу", "Graphics: {0} · matched to hardware",
                "图形：{0} · 已按硬件匹配", "Gráficos: {0} · ajustado al hardware",
                "Grafik: {0} · an Hardware angepasst", "Graphismes : {0} · adapté au matériel",
                "グラフィック：{0} · ハードウェアに合わせて設定"),
            Row("graphics.toast.detect",
                "Обнаружено: {0} · автовыбор: {1}", "Detected: {0} · auto choice: {1}",
                "已检测：{0} · 自动选择：{1}", "Detectado: {0} · elección automática: {1}",
                "Erkannt: {0} · automatische Wahl: {1}", "Détecté : {0} · choix automatique : {1}",
                "検出：{0} · 自動選択：{1}"),
            Row("graphics.toast.hwchanged",
                "Железо изменилось · графика: {0}", "Hardware changed · graphics: {0}",
                "硬件已变化 · 图形：{0}", "Hardware cambiado · gráficos: {0}",
                "Hardware geändert · Grafik: {0}", "Matériel modifié · graphismes : {0}",
                "ハードウェアが変化 · グラフィック：{0}"),
            Row("graphics.toast.reset",
                "Графика сброшена · режим «Авто» · {0}", "Graphics reset · Auto mode · {0}",
                "图形已重置 · 自动模式 · {0}", "Gráficos restablecidos · modo «Automático» · {0}",
                "Grafik zurückgesetzt · Modus „Automatisch“ · {0}",
                "Graphismes réinitialisés · mode « Auto » · {0}", "グラフィックを初期化 · 自動モード · {0}"),
            Row("graphics.toast.benchmark",
                "Замер: средний {0} FPS · минимум {1}", "Measurement: average {0} FPS · minimum {1}",
                "测量：平均 {0} FPS · 最低 {1}", "Medición: media {0} FPS · mínimo {1}",
                "Messung: Ø {0} FPS · Minimum {1}", "Mesure : moyenne {0} ips · minimum {1}",
                "計測：平均 {0} FPS · 最低 {1}"),

            // ---------------------------------------------------------- состояние
            Row("graphics.applied",
                "Графика: {0} · масштаб {1} · тени {2} · AA {3} · VSync {4} · предел {5}",
                "Graphics: {0} · scale {1} · shadows {2} · AA {3} · VSync {4} · cap {5}",
                "图形：{0} · 缩放 {1} · 阴影 {2} · AA {3} · VSync {4} · 上限 {5}",
                "Gráficos: {0} · escala {1} · sombras {2} · AA {3} · VSync {4} · límite {5}",
                "Grafik: {0} · Skalierung {1} · Schatten {2} · AA {3} · VSync {4} · Limit {5}",
                "Graphismes : {0} · échelle {1} · ombres {2} · AA {3} · VSync {4} · limite {5}",
                "グラフィック：{0} · 倍率 {1} · 影 {2} · AA {3} · VSync {4} · 上限 {5}"),
            Row("graphics.status", "Графика", "Graphics", "图形", "Gráficos", "Grafik", "Graphismes", "グラフィック"),
            Row("graphics.badge.off",
                "без графики", "no graphics", "无图形", "sin gráficos", "ohne Grafik", "sans graphismes",
                "グラフィックなし"),
            Row("graphics.skip.nographics",
                "графика недоступна (пакетный режим без видеокарты) — настройки рассчитаны, но НЕ применены",
                "graphics unavailable (batch mode without a GPU) — settings computed but NOT applied",
                "图形不可用（无 GPU 的批处理模式）— 设置已计算但未应用",
                "gráficos no disponibles (modo por lotes sin GPU) — ajustes calculados pero NO aplicados",
                "Grafik nicht verfügbar (Batch-Modus ohne GPU) — Einstellungen berechnet, aber NICHT angewendet",
                "graphismes indisponibles (mode batch sans GPU) — réglages calculés mais NON appliqués",
                "グラフィック利用不可（GPU なしのバッチ実行）— 設定は計算のみで未適用"),
            Row("graphics.skip.notplaying",
                "настройки рассчитаны, но не применены: применение выполняется в режиме воспроизведения, " +
                "чтобы не менять параметры проекта",
                "settings computed but not applied: applying runs in Play mode so project settings stay untouched",
                "设置已计算但未应用：仅在播放模式下应用，以免修改项目设置",
                "ajustes calculados pero no aplicados: se aplican en modo reproducción para no tocar el proyecto",
                "Einstellungen berechnet, aber nicht angewendet: dies geschieht im Wiedergabemodus, " +
                "damit die Projektdateien unverändert bleiben",
                "réglages calculés mais non appliqués : l'application a lieu en mode lecture pour ne pas " +
                "modifier les paramètres du projet",
                "設定は計算のみ：プロジェクト設定を変更しないよう、適用は再生モードで行います"),
            Row("graphics.reset",
                "Сбросить графику по умолчанию", "Reset graphics to defaults", "恢复默认图形设置",
                "Restablecer gráficos", "Grafik zurücksetzen", "Réinitialiser les graphismes",
                "グラフィックを初期化"),
            Row("graphics.reset.done",
                "графика сброшена к значениям по умолчанию: режим «Авто»",
                "graphics reset to defaults: Auto mode", "图形已恢复默认：自动模式",
                "gráficos restablecidos: modo «Automático»", "Grafik zurückgesetzt: Modus „Automatisch“",
                "graphismes réinitialisés : mode « Auto »", "グラフィックを初期化：自動モード"),
            Row("graphics.debug",
                "Полный журнал применения настроек", "Full settings application log",
                "完整应用日志", "Registro completo de aplicación", "Vollständiges Anwendungsprotokoll",
                "Journal complet d'application", "適用ログ（詳細）"),

            // ---------------------------------------------------------- подписи качества
            Row("graphics.shadow.low", "Низкое (512)", "Low (512)", "低（512）", "Bajo (512)",
                "Niedrig (512)", "Faible (512)", "低（512）"),
            Row("graphics.shadow.medium", "Среднее (1024)", "Medium (1024)", "中（1024）", "Medio (1024)",
                "Mittel (1024)", "Moyen (1024)", "中（1024）"),
            Row("graphics.shadow.high", "Высокое (2048)", "High (2048)", "高（2048）", "Alto (2048)",
                "Hoch (2048)", "Élevé (2048)", "高（2048）"),
            Row("graphics.shadow.ultra", "Ультра (4096)", "Ultra (4096)", "超高（4096）", "Ultra (4096)",
                "Ultra (4096)", "Ultra (4096)", "ウルトラ（4096）"),
            Row("graphics.q.low", "Низкое", "Low", "低", "Bajo", "Niedrig", "Faible", "低"),
            Row("graphics.q.medium", "Среднее", "Medium", "中", "Medio", "Mittel", "Moyen", "中"),
            Row("graphics.q.high", "Высокое", "High", "高", "Alto", "Hoch", "Élevé", "高"),
            Row("graphics.q.ultra", "Ультра", "Ultra", "超高", "Ultra", "Ultra", "Ultra", "ウルトラ"),
            Row("graphics.aa.off", "Выкл", "Off", "关闭", "Desactivado", "Aus", "Désactivé", "オフ"),

            // ---------------------------------------------------------- подписи параметров
            Row("graphics.renderScale",
                "Масштаб отрисовки", "Render scale", "渲染缩放", "Escala de renderizado",
                "Render-Skalierung", "Échelle de rendu", "レンダー倍率"),
            Row("graphics.renderScale.note",
                "Масштаб отрисовки задаётся штатным динамическим разрешением HDRP и применяется без " +
                "перезагрузки сцены. В VR разрешение шлема задаёт среда XR — формат стерео " +
                "(Single Pass Instanced) служба не меняет.",
                "Render scale uses the built-in HDRP dynamic resolution and applies without reloading " +
                "the scene. In VR the headset resolution is owned by the XR runtime; the stereoscopic " +
                "format (Single Pass Instanced) is never changed.",
                "渲染缩放使用 HDRP 内置动态分辨率，无需重载场景即可生效。VR 中头显分辨率由 XR 运行时决定，" +
                "不会更改立体格式（Single Pass Instanced）。",
                "La escala de renderizado usa la resolución dinámica de HDRP y se aplica sin recargar la " +
                "escena. En VR la resolución la fija el entorno XR; no se cambia el formato estéreo " +
                "(Single Pass Instanced).",
                "Die Render-Skalierung nutzt die HDRP-Dynamic-Resolution und wirkt ohne Szenen-Neuladen. " +
                "In VR bestimmt die XR-Laufzeit die Auflösung; das Stereoverfahren (Single Pass Instanced) " +
                "wird nie geändert.",
                "L'échelle de rendu utilise la résolution dynamique HDRP et s'applique sans recharger la " +
                "scène. En VR, la résolution est gérée par l'environnement XR ; le format stéréo " +
                "(Single Pass Instanced) n'est jamais modifié.",
                "レンダー倍率は HDRP 標準の動的解像度を使用し、シーン再読み込みなしで反映されます。VR の解像度は" +
                "XR ランタイムが管理し、ステレオ形式（Single Pass Instanced）は変更しません。"),
            Row("graphics.shadows", "Тени", "Shadows", "阴影", "Sombras", "Schatten", "Ombres", "影"),
            Row("graphics.shadowDistance",
                "Дистанция теней, м", "Shadow distance, m", "阴影距离（米）", "Distancia de sombras, m",
                "Schattendistanz, m", "Distance des ombres, m", "影の距離（m）"),
            Row("graphics.cascades",
                "Каскады теней", "Shadow cascades", "阴影级联", "Cascadas de sombras",
                "Schatten-Kaskaden", "Cascades d'ombres", "シャドウカスケード"),
            Row("graphics.aa", "Сглаживание", "Anti-aliasing", "抗锯齿", "Antialiasing",
                "Kantenglättung", "Anticrénelage", "アンチエイリアス"),

            // ------------------------------------------------ ФИКС 1: режим отрисовки HDRP
            Row("graphics.raster",
                "Режим отрисовки", "Rasterization mode", "光栅化模式", "Modo de rasterizado",
                "Rasterisierungsmodus", "Mode de rastérisation", "ラスタライズ方式"),
            Row("graphics.raster.deferred",
                "Отложенная (Deferred)", "Deferred", "延迟（Deferred）", "Diferido (Deferred)",
                "Verzögert (Deferred)", "Différé (Deferred)", "遅延（Deferred）"),
            Row("graphics.raster.forward",
                "Прямая (Forward)", "Forward", "前向（Forward）", "Directo (Forward)",
                "Vorwärts (Forward)", "Direct (Forward)", "フォワード（Forward）"),
            Row("graphics.raster.both",
                "Обе (Forward + Deferred)", "Both (Forward + Deferred)", "两者（Forward + Deferred）",
                "Ambas (Forward + Deferred)", "Beide (Forward + Deferred)",
                "Les deux (Forward + Deferred)", "両方（Forward + Deferred）"),
            Row("graphics.raster.note",
                "Прямая отрисовка нужна для MSAA; отложенная быстрее на сложной геометрии. Смена режима " +
                "пересобирает шейдерные варианты HDRP (несколько секунд) и выполняется ТОЛЬКО по " +
                "подтверждению оператора. Формат стерео XR (Single Pass Instanced) при этом не меняется.",
                "Forward rendering is required for MSAA; deferred is faster with complex geometry. " +
                "Switching rebuilds the HDRP shader variants (a few seconds) and happens ONLY after " +
                "operator confirmation. The XR stereo format (Single Pass Instanced) is never changed.",
                "MSAA 需要前向渲染；复杂几何下延迟渲染更快。切换会重建 HDRP 着色器变体（数秒），" +
                "且仅在操作员确认后执行。XR 立体格式（Single Pass Instanced）不会改变。",
                "El renderizado directo es necesario para MSAA; el diferido es más rápido con geometría " +
                "compleja. El cambio reconstruye las variantes de shader de HDRP (unos segundos) y solo " +
                "se hace con confirmación del operador. El formato estéreo XR no se modifica.",
                "Für MSAA ist Vorwärts-Rendering nötig; verzögert ist bei komplexer Geometrie schneller. " +
                "Das Umschalten baut die HDRP-Shadervarianten neu (einige Sekunden) und erfolgt NUR nach " +
                "Bestätigung. Das XR-Stereoformat (Single Pass Instanced) bleibt unverändert.",
                "Le rendu direct est requis pour le MSAA ; le différé est plus rapide en géométrie " +
                "complexe. Le changement reconstruit les variantes de shader HDRP (quelques secondes) et " +
                "n'a lieu qu'après confirmation. Le format stéréo XR (Single Pass Instanced) est inchangé.",
                "MSAA にはフォワード描画が必要です。複雑な形状では遅延描画が高速です。切り替えは HDRP の" +
                "シェーダバリアントを再構築するため（数秒）、オペレータの確認後のみ実行します。" +
                "XR のステレオ形式（Single Pass Instanced）は変更しません。"),
            Row("graphics.raster.warn",
                "Смена режима отрисовки на «{0}»: HDRP пересоберёт набор шейдерных вариантов — это " +
                "займёт несколько секунд, кадр может замереть. Подтвердите действие.",
                "Switching the rasterization mode to \"{0}\": HDRP will rebuild its shader variants — " +
                "this takes a few seconds and the frame may freeze. Please confirm.",
                "将光栅化模式切换为“{0}”：HDRP 将重建着色器变体，需要数秒，画面可能卡顿。请确认。",
                "Cambiar el modo de rasterizado a «{0}»: HDRP reconstruirá las variantes de shader, " +
                "tardará unos segundos y el cuadro puede congelarse. Confirme la acción.",
                "Umschalten des Rasterisierungsmodus auf „{0}“: HDRP baut die Shadervarianten neu — das " +
                "dauert einige Sekunden, das Bild kann stocken. Bitte bestätigen.",
                "Passage du mode de rastérisation à « {0} » : HDRP reconstruira les variantes de shader — " +
                "quelques secondes, l'image peut se figer. Veuillez confirmer.",
                "ラスタライズ方式を「{0}」に変更します。HDRP がシェーダバリアントを再構築するため数秒かかり、" +
                "画面が固まることがあります。確認してください。"),
            Row("graphics.raster.pending",
                "Подтвердите смену режима на «{0}»: {1:0} с. Во время перекомпиляции шейдерных " +
                "вариантов кадр может замереть — это ожидаемо.",
                "Confirm switching the mode to \"{0}\": {1:0} s left. The frame may freeze while the " +
                "shader variants recompile — that is expected.",
                "请确认切换到“{0}”：剩余 {1:0} 秒。着色器变体重编译期间画面可能卡顿，这属正常。",
                "Confirme el cambio a «{0}»: quedan {1:0} s. El cuadro puede congelarse mientras se " +
                "recompilan las variantes de shader; es normal.",
                "Bestätigen Sie den Wechsel zu „{0}“: noch {1:0} s. Während die Shadervarianten neu " +
                "kompiliert werden, kann das Bild stocken — das ist normal.",
                "Confirmez le passage à « {0} » : {1:0} s restantes. L'image peut se figer pendant la " +
                "recompilation des variantes de shader — c'est normal.",
                "「{0}」への切り替えを確認してください：残り {1:0} 秒。シェーダバリアントの再コンパイル中は" +
                "画面が固まることがありますが想定内です。"),
            Row("graphics.raster.confirm",
                "Подтвердить смену режима", "Confirm mode switch", "确认切换模式",
                "Confirmar el cambio de modo", "Moduswechsel bestätigen",
                "Confirmer le changement", "モード変更を確認"),
            Row("graphics.raster.cancelBtn",
                "Отмена", "Cancel", "取消", "Cancelar", "Abbrechen", "Annuler", "キャンセル"),
            Row("graphics.raster.same",
                "Режим отрисовки уже такой — перекомпиляция не нужна.",
                "The rasterization mode is already set — no recompilation needed.",
                "光栅化模式已是该值——无需重新编译。",
                "El modo de rasterizado ya es ese: no hace falta recompilar.",
                "Der Rasterisierungsmodus ist bereits gesetzt — keine Neukompilierung nötig.",
                "Le mode de rastérisation est déjà celui-ci — aucune recompilation nécessaire.",
                "すでに同じ方式です。再コンパイルは不要です。"),
            Row("graphics.raster.cancel",
                "Смена режима отрисовки отменена оператором — HDRP-ассет не изменялся.",
                "The rasterization mode switch was cancelled by the operator — the HDRP asset is unchanged.",
                "操作员已取消切换光栅化模式——HDRP 资源未更改。",
                "El operador canceló el cambio de modo de rasterizado: el asset HDRP no se modificó.",
                "Der Moduswechsel wurde abgebrochen — das HDRP-Asset ist unverändert.",
                "Changement annulé par l'opérateur — l'asset HDRP est inchangé.",
                "オペレータが切り替えを中止しました。HDRP アセットは変更されていません。"),
            Row("graphics.raster.timeout",
                "Смена режима отрисовки не подтверждена за 20 с — отменена, HDRP-ассет не изменялся.",
                "The rasterization mode switch was not confirmed within 20 s — cancelled, the HDRP asset " +
                "is unchanged.",
                "20 秒内未确认切换光栅化模式——已取消，HDRP 资源未更改。",
                "El cambio de modo no se confirmó en 20 s: cancelado, el asset HDRP no se modificó.",
                "Der Moduswechsel wurde nicht innerhalb von 20 s bestätigt — abgebrochen, HDRP-Asset unverändert.",
                "Changement non confirmé sous 20 s — annulé, l'asset HDRP est inchangé.",
                "20 秒以内に確認されなかったため中止しました。HDRP アセットは変更されていません。"),
            Row("graphics.raster.nogfx",
                "Смена режима отрисовки недоступна: графики нет (пакетный режим) — настройки только считаются.",
                "Cannot switch the rasterization mode: no graphics device (batch mode) — settings are only computed.",
                "无法切换光栅化模式：无图形设备（批处理模式）——仅计算设置。",
                "No se puede cambiar el modo: sin dispositivo gráfico (modo por lotes); solo se calculan los ajustes.",
                "Moduswechsel nicht möglich: kein Grafikgerät (Batch-Modus) — Einstellungen werden nur berechnet.",
                "Changement impossible : pas de périphérique graphique (mode batch) — les réglages sont seulement calculés.",
                "切り替えできません：グラフィックデバイスがありません（バッチモード）。設定は計算のみ行います。"),
            Row("graphics.raster.noasset",
                "HDRP-ассет не найден: режим отрисовки не изменялся.",
                "HDRP asset not found: the rasterization mode was not changed.",
                "未找到 HDRP 资源：光栅化模式未更改。",
                "No se encontró el asset HDRP: el modo de rasterizado no se modificó.",
                "HDRP-Asset nicht gefunden: Der Rasterisierungsmodus wurde nicht geändert.",
                "Asset HDRP introuvable : le mode de rastérisation n'a pas été modifié.",
                "HDRP アセットが見つかりません。方式は変更していません。"),
            Row("graphics.raster.done",
                "режим отрисовки применён: ", "rasterization mode applied: ", "已应用光栅化模式：",
                "modo de rasterizado aplicado: ", "Rasterisierungsmodus angewendet: ",
                "mode de rastérisation appliqué : ", "ラスタライズ方式を適用しました："),
            Row("graphics.raster.saved",
                "В прошлой сессии был выбран режим отрисовки «{0}», сейчас в HDRP-ассете «{1}». Режим " +
                "не включается автоматически (пересборка шейдерных вариантов) — выберите его на " +
                "вкладке «Графика» и подтвердите.",
                "The \"{0}\" rasterization mode was selected in the previous session; the HDRP asset now " +
                "uses \"{1}\". The mode is not enabled automatically (shader variant rebuild) — pick it " +
                "on the Graphics tab and confirm.",
                "上次会话选择了“{0}”光栅化模式，当前 HDRP 资源为“{1}”。不会自动启用（需重建着色器变体），" +
                "请在“图形”选项卡中选择并确认。",
                "En la sesión anterior se eligió el modo «{0}»; ahora el asset HDRP usa «{1}». No se " +
                "activa automáticamente (reconstrucción de variantes): selecciónelo en la pestaña " +
                "«Gráficos» y confirme.",
                "In der letzten Sitzung war „{0}“ gewählt, das HDRP-Asset nutzt jetzt „{1}“. Der Modus " +
                "wird nicht automatisch aktiviert (Shadervarianten) — im Tab „Grafik“ wählen und bestätigen.",
                "Le mode « {0} » avait été choisi lors de la session précédente ; l'asset HDRP utilise " +
                "« {1} ». Le mode n'est pas activé automatiquement — sélectionnez-le dans l'onglet " +
                "« Graphique » et confirmez.",
                "前回のセッションでは「{0}」を選択しましたが、現在の HDRP アセットは「{1}」です。" +
                "自動では有効化しません（シェーダバリアント再構築）。「グラフィック」タブで選択し確認してください。"),
            Row("graphics.xr.off",
                "шлем не активен, стерео-формат не задействован",
                "headset is not active, the stereo format is not in use",
                "头显未激活，未使用立体格式",
                "el casco no está activo, el formato estéreo no se usa",
                "Headset ist nicht aktiv, das Stereoformat wird nicht genutzt",
                "le casque n'est pas actif, le format stéréo n'est pas utilisé",
                "ヘッドセットが未接続のためステレオ形式は使用していません"),
            Row("graphics.aa.note",
                "FXAA и SMAA — самый дешёвый вариант; TAA сглаживает лучше, но требует устойчивого " +
                "кадра; MSAA работает только при прямой отрисовке HDRP.",
                "FXAA and SMAA are the cheapest; TAA looks better but needs a steady frame rate; " +
                "MSAA only works with HDRP forward rendering.",
                "FXAA 与 SMAA 开销最低；TAA 效果更好但需要稳定帧率；MSAA 仅在 HDRP 前向渲染下有效。",
                "FXAA y SMAA son los más económicos; TAA se ve mejor pero exige una tasa estable; " +
                "MSAA solo funciona con renderizado directo de HDRP.",
                "FXAA und SMAA sind am günstigsten; TAA sieht besser aus, braucht aber stabile Bildraten; " +
                "MSAA funktioniert nur beim HDRP-Forward-Rendering.",
                "FXAA et SMAA sont les moins coûteux ; TAA est plus net mais exige un débit stable ; " +
                "MSAA ne fonctionne qu'avec le rendu direct HDRP.",
                "FXAA と SMAA は最も軽量、TAA は高品質ですが安定したフレームレートが必要、MSAA は HDRP の" +
                "フォワード描画時のみ有効です。"),
            Row("graphics.aa.note.deferred",
                "В HDRP-ассете выбран только отложенный режим отрисовки, поэтому MSAA недоступен: при " +
                "выборе MSAA применяется TAA. Чтобы MSAA заработал, в HDRP Asset → Rendering → " +
                "Lit Shader Mode нужно включить прямую отрисовку (это меняет набор шейдерных вариантов " +
                "и требует перекомпиляции — служба графики этого НЕ делает).",
                "The HDRP asset uses deferred-only rendering, so MSAA is unavailable: choosing MSAA " +
                "applies TAA instead. To enable MSAA, turn on forward rendering in HDRP Asset → " +
                "Rendering → Lit Shader Mode (this changes shader variants and needs recompilation — " +
                "the graphics service does NOT do that).",
                "HDRP 资源仅启用延迟渲染，因此 MSAA 不可用：选择 MSAA 时会改用 TAA。若需 MSAA，请在 " +
                "HDRP Asset → Rendering → Lit Shader Mode 中启用前向渲染（会改变着色器变体并需要重新编译，" +
                "图形服务不会执行此操作）。",
                "El asset HDRP usa solo renderizado diferido, así que MSAA no está disponible: al " +
                "elegirlo se aplica TAA. Para habilitarlo, active el renderizado directo en HDRP Asset → " +
                "Rendering → Lit Shader Mode (cambia las variantes de shader y requiere recompilar; " +
                "el servicio NO lo hace).",
                "Das HDRP-Asset nutzt nur Deferred-Rendering, daher ist MSAA nicht verfügbar: bei Auswahl " +
                "wird TAA angewendet. Für MSAA muss im HDRP Asset → Rendering → Lit Shader Mode das " +
                "Forward-Rendering aktiviert werden (ändert Shader-Varianten, erfordert Neukompilierung — " +
                "der Grafikdienst tut das NICHT).",
                "L'asset HDRP n'utilise que le rendu différé, donc MSAA est indisponible : choisir MSAA " +
                "applique TAA. Pour l'activer, activez le rendu direct dans HDRP Asset → Rendering → " +
                "Lit Shader Mode (cela change les variantes de shader et exige une recompilation — " +
                "le service graphique ne le fait PAS).",
                "HDRP アセットが遅延描画のみのため MSAA は使用できません。MSAA を選ぶと TAA を適用します。" +
                "有効にするには HDRP Asset → Rendering → Lit Shader Mode でフォワード描画を有効化してください" +
                "（シェーダバリアントが変わり再コンパイルが必要 — 本サービスは行いません）。"),
            Row("graphics.aa.note.forward",
                "MSAA доступен: в HDRP-ассете включена прямая отрисовка. MSAA и TAA взаимоисключающи — " +
                "при выборе MSAA сглаживание камеры отключается.",
                "MSAA is available: HDRP forward rendering is enabled. MSAA and TAA are mutually " +
                "exclusive — choosing MSAA turns camera anti-aliasing off.",
                "MSAA 可用：HDRP 已启用前向渲染。MSAA 与 TAA 互斥，选择 MSAA 时会关闭相机抗锯齿。",
                "MSAA disponible: el renderizado directo de HDRP está activo. MSAA y TAA son " +
                "excluyentes: al elegir MSAA se desactiva el antialiasing de cámara.",
                "MSAA ist verfügbar: HDRP-Forward-Rendering ist aktiv. MSAA und TAA schließen sich aus — " +
                "bei MSAA wird die Kamera-Kantenglättung deaktiviert.",
                "MSAA disponible : le rendu direct HDRP est actif. MSAA et TAA s'excluent — choisir MSAA " +
                "désactive l'anticrénelage de la caméra.",
                "MSAA が利用可能です（HDRP のフォワード描画が有効）。MSAA と TAA は排他で、MSAA 選択時は" +
                "カメラのアンチエイリアスを無効にします。"),
            Row("graphics.ssao",
                "Затенение в экранном пространстве (SSAO)", "Screen space ambient occlusion (SSAO)",
                "屏幕空间环境光遮蔽（SSAO）", "Oclusión ambiental en pantalla (SSAO)",
                "Umgebungsverdeckung im Bildraum (SSAO)", "Occlusion ambiante en espace écran (SSAO)",
                "スクリーンスペース AO（SSAO）"),
            Row("graphics.ssr",
                "Отражения в экранном пространстве (SSR)", "Screen space reflections (SSR)",
                "屏幕空间反射（SSR）", "Reflejos en pantalla (SSR)", "Bildraum-Reflexionen (SSR)",
                "Réflexions en espace écran (SSR)", "スクリーンスペース反射（SSR）"),
            Row("graphics.reflections", "Отражения", "Reflections", "反射", "Reflejos", "Reflexionen",
                "Réflexions", "反射"),
            Row("graphics.refl.baked", "Запечённые", "Baked", "烘焙", "Precalculados", "Gebacken",
                "Précalculées", "ベイク"),
            Row("graphics.refl.realtime",
                "Запечённые + реального времени", "Baked + realtime", "烘焙 + 实时",
                "Precalculados + tiempo real", "Gebacken + Echtzeit", "Précalculées + temps réel",
                "ベイク + リアルタイム"),
            Row("graphics.bloom", "Свечение (Bloom)", "Bloom", "泛光", "Resplandor (Bloom)",
                "Bloom", "Bloom", "ブルーム"),
            Row("graphics.motionblur",
                "Размытие в движении", "Motion blur", "运动模糊", "Desenfoque de movimiento",
                "Bewegungsunschärfe", "Flou de mouvement", "モーションブラー"),
            Row("graphics.dof", "Глубина резкости", "Depth of field", "景深", "Profundidad de campo",
                "Tiefenschärfe", "Profondeur de champ", "被写界深度"),
            Row("graphics.fog", "Объёмный туман", "Volumetric fog", "体积雾", "Niebla volumétrica",
                "Volumetrischer Nebel", "Brouillard volumétrique", "ボリュームフォグ"),
            Row("graphics.fogQuality", "Качество тумана", "Fog quality", "雾质量", "Calidad de niebla",
                "Nebelqualität", "Qualité du brouillard", "フォグ品質"),
            Row("graphics.rt", "Трассировка лучей", "Ray tracing", "光线追踪", "Trazado de rayos",
                "Raytracing", "Ray tracing", "レイトレーシング"),
            Row("graphics.rt.unsupported",
                "Трассировка лучей недоступна: видеокарта или драйвер её не поддерживают.",
                "Ray tracing is unavailable: the GPU or driver does not support it.",
                "光线追踪不可用：显卡或驱动不支持。",
                "El trazado de rayos no está disponible: la GPU o el controlador no lo admiten.",
                "Raytracing ist nicht verfügbar: GPU oder Treiber unterstützen es nicht.",
                "Le ray tracing est indisponible : le GPU ou le pilote ne le prend pas en charge.",
                "レイトレーシングは利用できません（GPU またはドライバが非対応）。"),
            Row("graphics.textures",
                "Предел текстур", "Texture limit", "纹理限制", "Límite de texturas", "Texturlimit",
                "Limite de textures", "テクスチャ制限"),
            Row("graphics.textures.note",
                "0 — полные текстуры, 1 — 1/2, 2 — 1/4, 3 — 1/8",
                "0 — full textures, 1 — 1/2, 2 — 1/4, 3 — 1/8",
                "0 — 完整，1 — 1/2，2 — 1/4，3 — 1/8",
                "0 — texturas completas, 1 — 1/2, 2 — 1/4, 3 — 1/8",
                "0 — volle Texturen, 1 — 1/2, 2 — 1/4, 3 — 1/8",
                "0 — textures complètes, 1 — 1/2, 2 — 1/4, 3 — 1/8",
                "0 — 原寸、1 — 1/2、2 — 1/4、3 — 1/8"),
            Row("graphics.lodbias",
                "Смещение уровня детализации (LOD)", "Level of detail bias (LOD)",
                "细节层次偏移（LOD）", "Sesgo de nivel de detalle (LOD)", "LOD-Verschiebung",
                "Biais de niveau de détail (LOD)", "LOD バイアス"),
            Row("graphics.decals", "Декали и детализация", "Decals and detail", "贴花与细节",
                "Calcomanías y detalle", "Decals und Details", "Décals et détails", "デカールと詳細"),
            Row("graphics.terrain", "Качество рельефа", "Terrain quality", "地形质量",
                "Calidad del terreno", "Terrain-Qualität", "Qualité du terrain", "地形品質"),
            Row("graphics.vsync", "Синхронизация кадров", "V-Sync", "垂直同步", "Sincronización vertical",
                "V-Sync", "Synchronisation verticale", "垂直同期"),
            Row("graphics.vsync.off", "Выключена", "Off", "关闭", "Desactivada", "Aus", "Désactivée", "オフ"),
            Row("graphics.vsync.on", "Включена", "On", "开启", "Activada", "An", "Activée", "オン"),
            Row("graphics.vsync.half",
                "Каждый второй кадр", "Every second V-blank", "每隔一个垂直消隐",
                "Cada segundo barrido vertical", "Jeder zweite Bildwechsel",
                "Un rafraîchissement sur deux", "2 回に 1 回"),
            Row("graphics.framecap", "Предел кадров", "Frame rate cap", "帧率上限", "Límite de FPS",
                "Bildratenlimit", "Limite d'images", "フレームレート上限"),
            Row("graphics.fps.unlimited", "Без предела", "Unlimited", "不限", "Sin límite", "Unbegrenzt",
                "Illimité", "無制限"),
            Row("graphics.vsync.note",
                "При включённой синхронизации предел кадров задаёт видеодрайвер, а не приложение — " +
                "это штатное поведение Unity, значение предела всё равно сохраняется.",
                "With V-Sync on, the frame cap is enforced by the graphics driver rather than the " +
                "application — this is standard Unity behaviour; the cap value is still saved.",
                "开启垂直同步后，帧率上限由显卡驱动而非应用决定，这是 Unity 的标准行为；上限值仍会保存。",
                "Con la sincronización activada, el límite lo impone el controlador gráfico y no la " +
                "aplicación: es el comportamiento estándar de Unity; el valor se guarda igualmente.",
                "Bei aktivem V-Sync setzt der Grafiktreiber das Limit, nicht die Anwendung — " +
                "Standardverhalten von Unity; der Wert wird trotzdem gespeichert.",
                "Avec la synchronisation active, la limite est imposée par le pilote graphique et non " +
                "par l'application — comportement standard d'Unity ; la valeur reste enregistrée.",
                "垂直同期が有効な場合、上限はアプリではなくグラフィックスドライバが決めます（Unity の標準動作）。" +
                "設定値は保存されます。"),
            Row("graphics.dynres",
                "Динамическое разрешение (HDRP)", "Dynamic resolution (HDRP)",
                "动态分辨率（HDRP）", "Resolución dinámica (HDRP)", "Dynamische Auflösung (HDRP)",
                "Résolution dynamique (HDRP)", "動的解像度（HDRP）"),
            Row("graphics.adaptive",
                "Адаптивное качество (снижение при просадке)",
                "Adaptive quality (lower on frame drops)", "自适应画质（掉帧时降低）",
                "Calidad adaptativa (baja al perder FPS)", "Adaptive Qualität (senkt bei Einbrüchen)",
                "Qualité adaptative (baisse en cas de chute)", "適応画質（低下時に下げる）"),
            Row("graphics.targetfps", "Целевой FPS", "Target FPS", "目标 FPS", "FPS objetivo",
                "Ziel-FPS", "IPS cible", "目標 FPS"),
            Row("graphics.adaptive.note",
                "Адаптивное качество снижает масштаб отрисовки шагом 0,05 при устойчивой просадке ниже " +
                "цели и возвращает его при запасе. По ТЗ масштаб меняется только в пресете VR-Ready; " +
                "в остальных режимах выдаётся мягкое предупреждение в журнал.",
                "Adaptive quality lowers the render scale in 0.05 steps when the frame rate stays below " +
                "target and restores it when there is headroom. Per specification the scale is only " +
                "changed in the VR-Ready preset; other modes get a soft journal warning.",
                "自适应画质在持续低于目标时以 0.05 为步长降低渲染缩放，富余时恢复。按规范仅在 VR-Ready " +
                "预设下改缩放，其他模式只记录温和警告。",
                "La calidad adaptativa reduce la escala de renderizado en pasos de 0,05 si los FPS se " +
                "mantienen por debajo del objetivo y la restaura cuando hay margen. Según la especificación " +
                "el escalado solo cambia en VR-Ready; en otros modos solo se avisa en el registro.",
                "Adaptive Qualität senkt die Render-Skalierung in 0,05-Schritten bei anhaltender " +
                "Unterschreitung und stellt sie bei Reserve wieder her. Laut Spezifikation ändert sich die " +
                "Skalierung nur im Preset VR-Ready; sonst gibt es nur einen Hinweis im Journal.",
                "La qualité adaptative réduit l'échelle de rendu par pas de 0,05 en cas de baisse " +
                "durable et la restaure en cas de marge. Selon la spécification, l'échelle ne change " +
                "qu'en préréglage VR-Ready ; sinon un simple avertissement est journalisé.",
                "適応画質は目標を下回り続けるとレンダー倍率を 0.05 刻みで下げ、余裕があれば戻します。" +
                "仕様により倍率変更は VR-Ready のみ、それ以外はジャーナルへの警告のみです。"),

            // ---------------------------------------------------------- метрики
            Row("graphics.metrics.show",
                "Показать текущие метрики", "Show current metrics", "显示当前指标", "Mostrar métricas",
                "Aktuelle Messwerte anzeigen", "Afficher les mesures", "現在の指標を表示"),
            Row("graphics.metrics.title", "МЕТРИКИ ГРАФИКИ", "GRAPHICS METRICS", "图形指标",
                "MÉTRICAS DE GRÁFICOS", "GRAFIK-MESSWERTE", "MESURES GRAPHIQUES", "グラフィック指標"),
            Row("graphics.metrics.hide", "Скрыть", "Hide", "隐藏", "Ocultar", "Ausblenden", "Masquer", "非表示"),
            Row("graphics.metrics.frametime", "кадр", "frame", "帧", "fotograma", "Bild", "image", "フレーム"),
            Row("graphics.metrics.drawcalls",
                "вызовов отрисовки", "draw calls", "绘制调用", "llamadas de dibujo", "Draw Calls",
                "appels de dessin", "ドローコール"),
            Row("graphics.metrics.triangles",
                "треугольников", "triangles", "三角形", "triángulos", "Dreiecke", "triangles", "三角形"),
            Row("graphics.metrics.setpass",
                "проходов материала", "set-pass calls", "材质通道", "pasos de material", "Material-Pässe",
                "passes matériau", "マテリアルパス"),
            Row("graphics.metrics.vram",
                "видеопамять (карта)", "video memory (card)", "显存（显卡）", "memoria de vídeo (tarjeta)",
                "VRAM (Karte)", "mémoire vidéo (carte)", "ビデオメモリ（カード）"),
            Row("graphics.metrics.textures", "текстуры", "textures", "纹理", "texturas", "Texturen",
                "textures", "テクスチャ"),

            // ---------------------------------------------------------- бенчмарк
            Row("graphics.benchmark.run",
                "Запустить бенчмарк 5 секунд", "Run 5-second benchmark", "运行 5 秒基准测试",
                "Ejecutar prueba de 5 segundos", "5-Sekunden-Benchmark starten",
                "Lancer un test de 5 secondes", "5 秒ベンチマークを実行"),
            Row("graphics.benchmark.running", "Замер графики", "Graphics measurement", "图形测量",
                "Medición de gráficos", "Grafikmessung", "Mesure graphique", "グラフィック計測"),
            Row("graphics.benchmark.started",
                "бенчмарк графики: {0} с", "graphics benchmark: {0} s", "图形基准测试：{0} 秒",
                "prueba de gráficos: {0} s", "Grafik-Benchmark: {0} s",
                "test graphique : {0} s", "グラフィック計測：{0} 秒"),
            Row("graphics.benchmark.none",
                "Замер ещё не выполнялся: он считает средний, минимальный FPS и просадки.",
                "No measurement yet: it reports average, minimum FPS and frame drops.",
                "尚未测量：将给出平均、最低 FPS 与掉帧。",
                "Aún no hay medición: calcula FPS medio, mínimo y caídas.",
                "Noch keine Messung: ermittelt Durchschnitt, Minimum und Einbrüche.",
                "Pas encore de mesure : moyenne, minimum et chutes d'images.",
                "未計測：平均・最低 FPS と落ち込みを算出します。"),
            Row("graphics.benchmark.tooshort",
                "замер слишком короткий — повторите", "measurement too short — please repeat",
                "测量时间过短，请重试", "medición demasiado corta: repita",
                "Messung zu kurz — bitte wiederholen", "mesure trop courte — recommencez",
                "計測が短すぎます。再実行してください"),
            Row("graphics.benchmark.ok",
                "запас есть — пресет подходит", "there is headroom — the preset fits",
                "余量充足 — 预设合适", "hay margen: el preset es adecuado",
                "Reserve vorhanden — die Voreinstellung passt", "marge suffisante — le préréglage convient",
                "余裕あり — このプリセットが適切です"),
            Row("graphics.benchmark.mid",
                "почти хватает — можно снизить тени или объёмный туман",
                "almost enough — consider lowering shadows or volumetric fog",
                "接近足够 — 可降低阴影或体积雾",
                "casi suficiente: reduzca sombras o niebla volumétrica",
                "fast ausreichend — Schatten oder volumetrischen Nebel senken",
                "presque suffisant — réduisez les ombres ou le brouillard volumétrique",
                "ほぼ十分 — 影かボリュームフォグを下げてください"),
            Row("graphics.benchmark.bad",
                "недостаточно — рекомендуются пресет ниже и масштаб 0,85",
                "not enough — a lower preset and 0.85 scale are recommended",
                "不足 — 建议降低预设并使用 0.85 缩放",
                "insuficiente: se recomienda un preset menor y escala 0,85",
                "nicht ausreichend — niedrigere Voreinstellung und Skalierung 0,85 empfohlen",
                "insuffisant — préréglage inférieur et échelle 0,85 recommandés",
                "不足 — 低いプリセットと倍率 0.85 を推奨"),
            Row("graphics.benchmark.report",
                "Замер {0} с · кадров {1}", "Measurement {0} s · frames {1}",
                "测量 {0} 秒 · 帧数 {1}", "Medición {0} s · fotogramas {1}",
                "Messung {0} s · Bilder {1}", "Mesure {0} s · images {1}", "計測 {0} 秒 · フレーム {1}"),
            Row("graphics.benchmark.avg", "Средний FPS: {0}", "Average FPS: {0}", "平均 FPS：{0}",
                "FPS medio: {0}", "Ø FPS: {0}", "IPS moyen : {0}", "平均 FPS：{0}"),
            Row("graphics.benchmark.min", "Минимальный FPS: {0}", "Minimum FPS: {0}", "最低 FPS：{0}",
                "FPS mínimo: {0}", "Minimale FPS: {0}", "IPS minimum : {0}", "最低 FPS：{0}"),
            Row("graphics.benchmark.max", "Максимальный FPS: {0}", "Maximum FPS: {0}", "最高 FPS：{0}",
                "FPS máximo: {0}", "Maximale FPS: {0}", "IPS maximal : {0}", "最高 FPS：{0}"),
            Row("graphics.benchmark.low1", "Просадки (1 %): {0}", "Frame drops (1 %): {0}",
                "掉帧（1 %）：{0}", "Caídas (1 %): {0}", "Einbrüche (1 %): {0}",
                "Chutes (1 %) : {0}", "落ち込み（1 %）：{0}"),
            Row("graphics.benchmark.target",
                "Цель: {0} FPS · масштаб {1}", "Target: {0} FPS · scale {1}",
                "目标：{0} FPS · 缩放 {1}", "Objetivo: {0} FPS · escala {1}",
                "Ziel: {0} FPS · Skalierung {1}", "Cible : {0} ips · échelle {1}",
                "目標：{0} FPS · 倍率 {1}"),

            // ---------------------------------------------------------- адаптивный режим
            Row("graphics.adaptive.hint.shadows",
                "рекомендую снизить тени", "consider lowering shadows",
                "建议降低阴影", "se recomienda reducir las sombras",
                "Schatten reduzieren empfohlen", "réduisez les ombres", "影を下げることを推奨"),
            Row("graphics.adaptive.hint.fog",
                "рекомендую снизить объёмный туман", "consider lowering volumetric fog",
                "建议降低体积雾", "se recomienda reducir la niebla volumétrica",
                "volumetrischen Nebel reduzieren empfohlen", "réduisez le brouillard volumétrique",
                "ボリュームフォグを下げることを推奨"),
            Row("graphics.adaptive.low",
                "FPS {0} < целевого {1} · {2}", "FPS {0} < target {1} · {2}",
                "FPS {0} < 目标 {1} · {2}", "FPS {0} < objetivo {1} · {2}",
                "FPS {0} < Ziel {1} · {2}", "IPS {0} < cible {1} · {2}",
                "FPS {0} < 目標 {1} · {2}"),
            Row("graphics.adaptive.down",
                "масштаб отрисовки снижен до {0}", "render scale lowered to {0}",
                "渲染缩放降至 {0}", "escala de renderizado reducida a {0}",
                "Render-Skalierung auf {0} gesenkt", "échelle de rendu réduite à {0}",
                "レンダー倍率を {0} に低減"),
            Row("graphics.adaptive.up",
                "запас по кадрам — масштаб отрисовки возвращён к {0}",
                "frame headroom — render scale restored to {0}",
                "帧率有余量 — 渲染缩放恢复到 {0}",
                "hay margen: escala de renderizado restaurada a {0}",
                "Bildreserve — Render-Skalierung auf {0} zurückgesetzt",
                "marge disponible — échelle de rendu rétablie à {0}",
                "余裕あり — レンダー倍率を {0} に復帰"),
            Row("graphics.adaptive.vronly",
                "адаптивное снижение масштаба по ТЗ работает только в пресете VR-Ready — " +
                "выполнено только предупреждение в журнал",
                "per specification adaptive scaling only works in the VR-Ready preset — " +
                "only a journal warning was issued",
                "按规范自适应缩放仅在 VR-Ready 预设下生效 — 此处只记录警告",
                "según la especificación el escalado adaptativo solo actúa en VR-Ready — " +
                "solo se registró un aviso",
                "laut Spezifikation wirkt adaptives Skalieren nur im Preset VR-Ready — " +
                "es wurde nur ein Journal-Hinweis ausgegeben",
                "selon la spécification, la mise à l'échelle adaptative n'agit qu'en VR-Ready — " +
                "seul un avertissement a été journalisé",
                "仕様により適応スケーリングは VR-Ready のみ — ここでは警告のみ記録しました"),
            // ЗАПРЕТ ВЛАДЕЛЬЦА НА СНЯТИЕ МАСШТАБА (20.11.2026): служба больше не урезает масштаб
            // при просадке, а только пишет в журнал. Ключ обязателен: без него `KvGraphicsService`
            // показал бы русский текст посреди переведённого интерфейса (тест
            // `KvLocalizationFileTests.KeysRequestedByCode_ExistInDictionaries`).
            Row("graphics.adaptive.owner",
                "масштаб отрисовки НЕ снижаю (запрет владельца: качество не трогаем) — " +
                "осталось только предупреждение в журнале; смотрите причину просадки, а не картинку",
                "render scale is NOT lowered (owner's rule: quality stays untouched) — only the log " +
                "warning remains; look at the cause of the frame drop, not at the picture",
                "不降低渲染缩放（业主规则：画质不动）— 只剩日志警告；请看掉帧的原因，而不是画面",
                "no se reduce la escala de renderizado (regla del propietario: la calidad no se toca) — " +
                "solo queda el aviso en el registro; mire la causa de la caída, no la imagen",
                "die Render-Skalierung wird NICHT gesenkt (Regel des Eigentümers: Qualität bleibt " +
                "unangetastet) — im Journal bleibt nur der Hinweis; sehen Sie die Ursache des " +
                "Einbruchs an, nicht das Bild",
                "l'échelle de rendu n'est pas réduite (règle du propriétaire : la qualité reste " +
                "intacte) — seul l'avertissement reste dans le journal ; regardez la cause de la " +
                "chute, pas l'image",
                "レンダー倍率は下げません（オーナーの規則：画質は触らない）— ジャーナルには警告のみ；" +
                "絵ではなく落ちの原因を見てください"),

            // ---------------------------------------------------------- диагностика
            Row("graphics.diag.state", "Состояние: ", "State: ", "状态：", "Estado: ", "Status: ",
                "État : ", "状態："),
            Row("graphics.diag.on",
                "графика доступна, настройки применяются",
                "graphics available, settings are applied",
                "图形可用，设置已应用", "gráficos disponibles, ajustes aplicados",
                "Grafik verfügbar, Einstellungen werden angewendet",
                "graphismes disponibles, réglages appliqués", "グラフィック利用可、設定を適用中"),
            Row("graphics.diag.off",
                "графика недоступна (пакетный режим) — настройки только рассчитываются",
                "graphics unavailable (batch mode) — settings are only computed",
                "图形不可用（批处理模式）— 仅计算设置",
                "gráficos no disponibles (modo por lotes) — los ajustes solo se calculan",
                "Grafik nicht verfügbar (Batch-Modus) — Einstellungen werden nur berechnet",
                "graphismes indisponibles (mode batch) — réglages seulement calculés",
                "グラフィック利用不可（バッチ実行）— 設定は計算のみ"),
            Row("graphics.diag.hdrp", "Конвейер HDRP: ", "HDRP pipeline: ", "HDRP 管线：",
                "Canal HDRP: ", "HDRP-Pipeline: ", "Pipeline HDRP : ", "HDRP パイプライン："),

            // ---------------------------------------------------------- команды интерфейса
            Row("cmd.graphics.tab",
                "Графика: настройки", "Graphics: settings", "图形：设置", "Gráficos: ajustes",
                "Grafik: Einstellungen", "Graphismes : réglages", "グラフィック：設定"),
            Row("cmd.graphics.tab.desc",
                "Раздел «Графика» панели настроек: пресеты, качество, производительность, диагностика",
                "Graphics section of the settings panel: presets, quality, performance, diagnostics",
                "设置面板的图形部分：预设、画质、性能、诊断",
                "Sección Gráficos de los ajustes: presets, calidad, rendimiento, diagnóstico",
                "Abschnitt Grafik der Einstellungen: Voreinstellungen, Qualität, Leistung, Diagnose",
                "Section Graphismes des paramètres : préréglages, qualité, performances, diagnostic",
                "設定パネルのグラフィック項目：プリセット・画質・性能・診断"),
            Row("cmd.graphics.detect",
                "Определить графику по железу", "Detect graphics by hardware",
                "按硬件检测图形", "Detectar gráficos por hardware",
                "Grafik anhand der Hardware erkennen", "Détecter les graphismes selon le matériel",
                "ハードウェアでグラフィックを判定"),
            Row("cmd.graphics.detect.desc",
                "Повторно определить видеокарту, память и процессор и пересчитать пресет",
                "Re-detect GPU, memory and CPU and recalculate the preset",
                "重新检测显卡、内存与处理器并重算预设",
                "Volver a detectar GPU, memoria y CPU y recalcular el preset",
                "GPU, Speicher und CPU erneut erkennen und Voreinstellung neu berechnen",
                "Redétecter GPU, mémoire et CPU et recalculer le préréglage",
                "GPU・メモリ・CPU を再検出してプリセットを再計算"),
            Row("cmd.graphics.benchmark",
                "Бенчмарк графики", "Graphics benchmark", "图形基准测试", "Prueba de gráficos",
                "Grafik-Benchmark", "Test graphique", "グラフィック計測"),
            Row("cmd.graphics.benchmark.desc",
                "Замер кадровой частоты на 5 секунд: средний, минимальный FPS и просадки",
                "5-second frame rate measurement: average, minimum FPS and frame drops",
                "5 秒帧率测量：平均、最低 FPS 与掉帧",
                "Medición de 5 segundos: FPS medio, mínimo y caídas",
                "5-Sekunden-Messung: Ø-, Minimal-FPS und Einbrüche",
                "Mesure de 5 secondes : IPS moyen, minimum et chutes",
                "5 秒間のフレームレート計測：平均・最低 FPS と落ち込み"),
            Row("cmd.graphics.metrics",
                "Метрики графики", "Graphics metrics", "图形指标", "Métricas de gráficos",
                "Grafik-Messwerte", "Mesures graphiques", "グラフィック指標"),
            Row("cmd.graphics.metrics.desc",
                "Оверлей: FPS, время кадра, вызовы отрисовки, треугольники, видеопамять",
                "Overlay: FPS, frame time, draw calls, triangles, video memory",
                "叠加层：FPS、帧时间、绘制调用、三角形、显存",
                "Superposición: FPS, tiempo de fotograma, llamadas, triángulos, memoria de vídeo",
                "Overlay: FPS, Bildzeit, Draw Calls, Dreiecke, VRAM",
                "Superposition : IPS, temps d'image, appels, triangles, mémoire vidéo",
                "オーバーレイ：FPS・フレーム時間・ドローコール・三角形・ビデオメモリ"),
            Row("cmd.graphics.reset",
                "Сбросить графику", "Reset graphics", "重置图形", "Restablecer gráficos",
                "Grafik zurücksetzen", "Réinitialiser les graphismes", "グラフィックを初期化"),
            Row("cmd.graphics.reset.desc",
                "Вернуть режим «Авто» и настройки графики по умолчанию",
                "Return to Auto mode and default graphics settings",
                "恢复自动模式与默认图形设置",
                "Volver al modo «Automático» y a los ajustes por defecto",
                "Zurück zu Modus „Automatisch“ und Standardeinstellungen",
                "Revenir au mode « Auto » et aux réglages par défaut",
                "自動モードと既定のグラフィック設定に戻す"),
        };

        private static string[] Row(string key, string ru, string en, string zh, string es,
            string de, string fr, string ja)
        {
            return new[] { key, ru, en, zh, es, de, fr, ja };
        }
    }
}
