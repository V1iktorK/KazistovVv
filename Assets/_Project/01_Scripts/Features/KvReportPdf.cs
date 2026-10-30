using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// МИНИ-ГЕНЕРАТОР PDF (ЭТАП 19 ТЗ): пишет корректный PDF 1.4 вручную — без внешних
    /// библиотек. Поддерживает страницы с текстом (встроенный шрифт Helvetica) и страницы
    /// с изображением JPEG (кадры сцены и отрендеренные страницы отчёта).
    ///
    /// ПОЧЕМУ ТЕКСТ ЛАТИНИЦЕЙ: у встроенных шрифтов PDF (base-14) нет кириллицы, а встраивание
    /// TTF — отдельная большая задача. Поэтому в «текстовом» режиме русские строки
    /// транслитерируются (функция `Latin`), а полноценная вёрстка с кириллицей попадает
    /// в PDF как СТРАНИЦА-ИЗОБРАЖЕНИЕ: она рисуется средствами Unity (шрифт интерфейса,
    /// кириллица, CJK) и вкладывается в PDF как JPEG. Оба пути дают валидный PDF, разница —
    /// в способе набора текста.
    /// </summary>
    public class KvPdfWriter
    {
        /// <summary>Одна страница: текст (уже в латинице) и/или изображение JPEG.</summary>
        private class PageSpec
        {
            public string title = "";
            public List<string> lines = new List<string>();
            public byte[] jpeg;
            public int imageWidth;
            public int imageHeight;
            public string caption = "";
            public bool HasImage { get { return jpeg != null && jpeg.Length > 0; } }
        }

        private readonly List<PageSpec> pages = new List<PageSpec>();
        private float pageWidth = 595f;    // A4, пункты
        private float pageHeight = 842f;

        /// <summary>Страница с текстом (строки — латиницей: см. Latin()).</summary>
        public void AddTextPage(string title, IList<string> lines, float fontSize = 11f)
        {
            var spec = new PageSpec { title = title ?? "" , caption = fontSize.ToString("0") };
            if (lines != null) spec.lines.AddRange(lines);
            pages.Add(spec);
        }

        /// <summary>Страница с изображением JPEG (кадр сцены или отрендеренная страница).</summary>
        public void AddImagePage(byte[] jpeg, int width, int height, string caption = "")
        {
            if (jpeg == null || jpeg.Length == 0 || width <= 0 || height <= 0) return;
            pages.Add(new PageSpec
            {
                jpeg = jpeg,
                imageWidth = width,
                imageHeight = height,
                caption = caption ?? ""
            });
        }

        public int PageCount { get { return pages.Count; } }

        /// <summary>Собрать файл PDF.</summary>
        public byte[] Finish(string title)
        {
            var stream = new MemoryStream();
            var offsets = new List<long>();

            // --- предварительная нумерация объектов: 1 каталог, 2 дерево страниц,
            //     3 шрифт, 4 сведения, далее по странице: содержимое, страница, картинка.
            int catalogNo = 1, pagesNo = 2, fontNo = 3, infoNo = 4;
            int next = 5;
            var contentNo = new int[pages.Count];
            var pageNo = new int[pages.Count];
            var imageNo = new int[pages.Count];
            for (int i = 0; i < pages.Count; i++)
            {
                contentNo[i] = next++;
                pageNo[i] = next++;
                imageNo[i] = pages[i].HasImage ? next++ : -1;
            }

            Write(stream, "%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");

            Write(stream, catalogNo + " 0 obj\n<< /Type /Catalog /Pages " + pagesNo +
                        " 0 R >>\nendobj\n", offsets);
            Write(stream, pagesNo + " 0 obj\n<< /Type /Pages /Count " + pages.Count + " /Kids [" +
                        Kids(pageNo) + "] >>\nendobj\n", offsets);
            Write(stream, fontNo + " 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica " +
                        "/Encoding /WinAnsiEncoding >>\nendobj\n", offsets);
            Write(stream, infoNo + " 0 obj\n<< /Title (" + Escape(title) +
                        ") /Producer (KazistovVv) /Creator (KazistovVv report) >>\nendobj\n", offsets);

            for (int i = 0; i < pages.Count; i++)
            {
                PageSpec spec = pages[i];
                string content = spec.HasImage ? ImageContent(spec, i) : TextContent(spec);
                Write(stream, contentNo[i] + " 0 obj\n<< /Length " + content.Length +
                            " >>\nstream\n" + content + "\nendstream\nendobj\n", offsets);

                string resources = "<< /Font << /F1 " + fontNo + " 0 R >>" +
                                   (spec.HasImage ? " /XObject << /Im0 " + imageNo[i] + " 0 R >>" : "") +
                                   " >>";
                Write(stream, pageNo[i] + " 0 obj\n<< /Type /Page /Parent " + pagesNo +
                            " 0 R /MediaBox [0 0 595 842] /Resources " + resources +
                            " /Contents " + contentNo[i] + " 0 R >>\nendobj\n", offsets);

                if (!spec.HasImage) continue;
                // Тело потока картинки — БИНАРНЫЕ данные JPEG: пишем байтами.
                offsets.Add(stream.Position);
                Write(stream, imageNo[i] + " 0 obj\n<< /Type /XObject /Subtype /Image /Width " +
                            spec.imageWidth + " /Height " + spec.imageHeight +
                            " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode " +
                            "/Length " + spec.jpeg.Length + " >>\nstream\n");
                stream.Write(spec.jpeg, 0, spec.jpeg.Length);
                Write(stream, "\nendstream\nendobj\n");
            }

            // --- таблица перекрёстных ссылок
            long xref = stream.Position;
            Write(stream, "xref\n0 " + (next) + "\n0000000000 65535 f \n");
            for (int i = 0; i < offsets.Count; i++)
                Write(stream, offsets[i].ToString("0000000000") + " 00000 n \n");
            Write(stream, "trailer\n<< /Size " + next + " /Root " + catalogNo + " 0 R /Info " +
                        infoNo + " 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");

            return stream.ToArray();
        }

        private static string Kids(int[] pageNumbers)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < pageNumbers.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(pageNumbers[i]).Append(" 0 R");
            }
            return sb.ToString();
        }

        private string TextContent(PageSpec spec)
        {
            var content = new StringBuilder();
            float y = pageHeight - 60f;
            if (!string.IsNullOrEmpty(spec.title))
            {
                content.Append("BT /F1 18 Tf 50 ").Append(F(y)).Append(" Td (")
                       .Append(Escape(spec.title)).Append(") Tj ET\n");
                y -= 30f;
            }
            content.Append("BT /F1 11 Tf 50 ").Append(F(y)).Append(" Td 14 TL\n");
            foreach (string line in spec.lines)
                content.Append("(").Append(Escape(line ?? "")).Append(") Tj T*\n");
            content.Append("ET\n");
            return content.ToString();
        }

        private string ImageContent(PageSpec spec, int index)
        {
            float margin = 30f;
            float captionH = string.IsNullOrEmpty(spec.caption) ? 0f : 22f;
            float maxW = pageWidth - margin * 2f;
            float maxH = pageHeight - margin * 2f - captionH;
            float scale = Mathf.Min(maxW / spec.imageWidth, maxH / spec.imageHeight);
            float w = spec.imageWidth * scale;
            float h = spec.imageHeight * scale;
            float x = (pageWidth - w) * 0.5f;
            float y = margin + captionH;

            var content = new StringBuilder();
            content.Append("q\n").Append(F(w)).Append(" 0 0 ").Append(F(h)).Append(' ')
                   .Append(F(x)).Append(' ').Append(F(y)).Append(" cm\n/Im0 Do\nQ\n");
            if (!string.IsNullOrEmpty(spec.caption))
            {
                content.Append("BT /F1 10 Tf 50 ").Append(F(margin)).Append(" Td (")
                       .Append(Escape(spec.caption)).Append(") Tj ET\n");
            }
            return content.ToString();
        }

        private static void Write(MemoryStream stream, string text, List<long> offsets = null)
        {
            if (offsets != null) offsets.Add(stream.Position);
            byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string F(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// ЭКРАНИРОВАНИЕ ТЕКСТА ДЛЯ ФОРМАТА PDF (сделано публичным 30.10.2026, S44).
        ///
        /// Почему публичной: это ЧИСТАЯ функция (строка → строка) без состояния и побочных
        /// эффектов, а её поведение важно проверить. Неэкранированная скобка обрывает строку PDF, и
        /// файл перестаёт открываться — самая частая причина «PDF не читается» от обычного имени
        /// вроде `отчёт (2).pdf`. Пока функция была приватной, это нельзя было проверить тестом.
        ///
        /// ПОБОЧНЫЙ ЭФФЕКТ, КОТОРЫЙ ЗАФИКСИРОВАН ЯВНО: все символы вне печатного ASCII (32…126)
        /// заменяются на `?`. Для китайского и японского это значит, что отчёт выйдет строкой
        /// вопросительных знаков — известное ограничение, записанное в отчёте сессии.
        /// </summary>
        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length + 8);
            foreach (char c in text)
            {
                if (c == '(' || c == ')' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n' || c == '\r') sb.Append(' ');
                else if (c < 32 || c > 126) sb.Append('?');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Транслитерация кириллицы (у встроенных шрифтов PDF её нет).</summary>
        public static string Latin(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var map = new Dictionary<char, string>
            {
                {'а',"a"},{'б',"b"},{'в',"v"},{'г',"g"},{'д',"d"},{'е',"e"},{'ё',"e"},{'ж',"zh"},
                {'з',"z"},{'и',"i"},{'й',"y"},{'к',"k"},{'л',"l"},{'м',"m"},{'н',"n"},{'о',"o"},
                {'п',"p"},{'р',"r"},{'с',"s"},{'т',"t"},{'у',"u"},{'ф',"f"},{'х',"h"},{'ц',"c"},
                {'ч',"ch"},{'ш',"sh"},{'щ',"sch"},{'ъ',""},{'ы',"y"},{'ь',""},{'э',"e"},{'ю',"yu"},
                {'я',"ya"},
                {'А',"A"},{'Б',"B"},{'В',"V"},{'Г',"G"},{'Д',"D"},{'Е',"E"},{'Ё',"E"},{'Ж',"Zh"},
                {'З',"Z"},{'И',"I"},{'Й',"Y"},{'К',"K"},{'Л',"L"},{'М',"M"},{'Н',"N"},{'О',"O"},
                {'П',"P"},{'Р',"R"},{'С',"S"},{'Т',"T"},{'У',"U"},{'Ф',"F"},{'Х',"H"},{'Ц',"C"},
                {'Ч',"Ch"},{'Ш',"Sh"},{'Щ',"Sch"},{'Ъ',""},{'Ы',"Y"},{'Ь',""},{'Э',"E"},{'Ю',"Yu"},
                {'Я',"Ya"},
                {'—',"-"},{'–',"-"},{'«',"\""},{'»',"\""},{'·',"."},{'×',"x"},{'°',"deg"},
                {'≤',"<="},{'≥',">="},{'✓',"+"},{'⚠',"!"},{'◈',"*"},{'₽',"RUB"}
            };
            var sb = new StringBuilder(text.Length * 2);
            foreach (char c in text)
            {
                string replacement;
                if (map.TryGetValue(c, out replacement)) sb.Append(replacement);
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
    /// <summary>
    /// ЭТАП 19 ТЗ: ГЕНЕРАТОР ОТЧЁТОВ (PDF).
    ///
    /// Что попадает в отчёт: заголовок (проект, дата, робот, число осей), выбранная
    /// траектория и её метрики (время, длина, кривизна, зазор, запас лимитов, энергия),
    /// таблица суставов (текущий угол, лимиты, запас), кинематика (TCP, база), состояние
    /// постобработки (сглаживание, время-оптимальная, эко-профиль), ограничения планирования,
    /// калибровка, проверка перед запуском и хвост журнала, плюс СКРИНШОТЫ сцены.
    ///
    /// Сохранение: «Документы\KazistovVv\reports\KazistovVv_report_<дата>.pdf» (ТЗ).
    /// </summary>
    public class KvReportGenerator
    {
        public const string AutoReportPrefsKey = "KazistovVv.Report.Auto";

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;
        private KvStageHub3 stage3;

        private string lastFile = "";
        private int lastPages;
        private bool imagesIncluded;

        public string LastFile { get { return lastFile; } }
        public int LastPages { get { return lastPages; } }
        public bool ImagesIncluded { get { return imagesIncluded; } }

        /// <summary>Краткое состояние генератора (дерево моделей, свойства, вкладка).</summary>
        public string Status()
        {
            if (string.IsNullOrEmpty(lastFile))
                return "отчёт не формировался · папка: " + FolderPath;
            return "последний отчёт: " + Path.GetFileName(lastFile) + " · страниц " + lastPages +
                   (imagesIncluded ? " · со снимками" : " · без снимков (нет графики)");
        }

        /// <summary>
        /// Папка отчётов — ЕДИНЫЙ каталог отчётов проекта (ФИКС 11):
        /// `&lt;persistentDataPath&gt;/KazistovVv/Reports`, тот же, что у всех остальных отчётов
        /// и диагностик (`FeatureStorage.ReportPath`).
        ///
        /// ЧТО БЫЛО И ПОЧЕМУ ИЗМЕНЕНО. Раньше это была «Документы\KazistovVv\reports» (папка ТЗ
        /// на момент этапа 5). Но «Документы» на этой машине лежат в OneDrive, а во время PlayMode
        /// OneDrive-файлы не дописываются (§13.11) — PDF оставался пустым до остановки PlayMode.
        /// ФИКС 10 перенёс в `persistentDataPath` все отчёты, кроме этого; теперь путь один.
        /// </summary>
        public string FolderPath
        {
            get { return FeatureStorage.ReportsDir; }
        }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub, KvStageHub3 hub3)
        {
            flow = controller;
            features = hub;
            stage3 = hub3;
        }

        /// <summary>Собрать и сохранить PDF. Возвращает путь ("" — отказ).</summary>
        public string Generate()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report("отчёт невозможен: робот не определён");
                return "";
            }

            PoseValidator v = flow.Validator;
            var pages = new List<List<string>>();
            string title = "KazistovVv - report";

            // ---------------- страница 1: заголовок, траектория, метрики
            var page = new List<string>();
            page.Add("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            page.Add("Робот: " + (flow.Robot != null ? flow.Robot.robotName : "-") +
                     " · осей: " + v.Dof);
            page.Add("Состояние: " + flow.State.phase +
                     (flow.State.hasPoint ? " · точка зафиксирована" : " · точка не выбрана"));
            page.Add("");

            int index;
            TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
            if (candidate != null && candidate.plan != null)
            {
                KvTrajStats stats = KvTrajMath.Analyze(v, candidate.plan,
                    features != null ? features.World : null, null, v.BasePosition);
                page.Add("Выбранная траектория: " + KvVariantKit.Label(candidate, index));
                page.Add("Время: " + stats.time.ToString("0.000") + " с · длина TCP: " +
                         stats.length.ToString("0.000") + " м · сэмплов: " + stats.samples);
                page.Add("Максимальная скорость: " + stats.maxVel.ToString("0.0") + " °/s · " +
                         "ускорение: " + stats.maxAcc.ToString("0.0") + " °/s2 · jerk: " +
                         stats.maxJerk.ToString("0") + " °/s3");
                page.Add("Кривизна: " + stats.curvature.ToString("0.000") + " 1/m · зазор: " +
                         (stats.clearance * 1000f).ToString("0") + " mm · запас лимитов: " +
                         stats.limitMargin.ToString("0.0") + " deg");
                // Сглаживание и время-оптимальная перепараметризация живут в хабе этапов 4–6
                // (KvStageHub2), поэтому берём их оттуда — в отчёте нужны их результаты.
                KvStageHub2 hub2 = KvStageHub2.Current;
                if (hub2 != null && hub2.Smoothing != null)
                {
                    KvSmoothEntry entry = hub2.Smoothing.EntryOf(candidate);
                    if (entry != null)
                        page.Add("Сглаживание: " + (entry.applied
                            ? entry.level.ToString("0") + " % / " + hub2.Smoothing.MethodLabel +
                              " · jerk " + entry.Reference.maxJerk.ToString("0") + " -> " +
                              entry.after.maxJerk.ToString("0") + " °/s3"
                            : "не применялось"));
                }
                if (hub2 != null && hub2.TimeOptimal != null)
                {
                    KvTimeOptimal.Draft draft = hub2.TimeOptimal.Compute(candidate, true);
                    if (draft != null)
                        page.Add("Время-оптимальная: " + draft.stats.time.ToString("0.000") +
                                 " с (выигрыш " + draft.timeGain.ToString("+0.0;-0.0") + " %)");
                }
                if (stage3 != null && stage3.Payload != null)
                {
                    page.Add("Нагрузка в текущей позе: " + stage3.Payload.StatusLine());
                }
            }
            else
            {
                page.Add("Траектория не выбрана (в отчёт попали только состояние и кинематика).");
            }

            page.Add("");
            page.Add("База робота: " + v.BasePosition.x.ToString("0.000") + ", " +
                     v.BasePosition.y.ToString("0.000") + ", " + v.BasePosition.z.ToString("0.000"));
            Vector3 tcpNow = v.TcpAt(v.CopyCurrent());
            page.Add("TCP: " + tcpNow.x.ToString("0.000") + ", " + tcpNow.y.ToString("0.000") +
                     ", " + tcpNow.z.ToString("0.000"));
            page.Add("");
            page.Add("Ограничения планирования: " + (stage3 != null && stage3.Constrained.Active != null
                ? stage3.Constrained.Profile.Describe() + " · " +
                  (stage3.Constrained.ViolatingCount > 0
                      ? "нарушений " + stage3.Constrained.ViolatingCount
                      : "нарушений нет")
                : "не заданы"));
            if (stage3 != null && stage3.Calibration != null)
            {
                KvCalibrationData cal = stage3.Calibration.Data;
                page.Add("Калибровка: " + (cal.tcpSolved
                    ? "TCP " + (cal.tcpLength * 1000f).ToString("0.0") + " mm (остаток " +
                      cal.tcpResidualMm.ToString("0.00") + " mm), точек " + cal.tcpPoints
                    : "не выполнялась") +
                    (cal.baseSolved ? " · база " + cal.baseHeightMm.ToString("0.0") + " mm / " +
                                      cal.baseTiltDeg.ToString("0.00") + " deg" : ""));
            }
            pages.Add(page);

            // ---------------- страница 2: суставы и кинематика
            var joints = new List<string>();
            joints.Add("Сустав | угол, deg | мин | макс | запас, deg");
            double[] q = v.CopyCurrent();
            for (int j = 0; j < v.Dof; j++)
            {
                bool prismatic = v.IsPrismatic(j);
                joints.Add("J" + (j + 1) + " | " + q[j].ToString(prismatic ? "0.0000" : "0.00") +
                           (prismatic ? " m" : "") + " | " + v.Lower[j].ToString("0.00") + " | " +
                           v.Upper[j].ToString("0.00") + " | " +
                           v.LimitMargin(q).ToString("0.0"));
            }
            joints.Add("");
            joints.Add("Скорость суставов (лимиты проекта): " +
                       (stage3 != null && stage3.Limits != null
                           ? stage3.Limits.maxVelDeg.ToString("0") + " °/s, ускорение " +
                             stage3.Limits.maxAccDeg.ToString("0") + " °/s2, jerk " +
                             stage3.Limits.maxJerkDeg.ToString("0") + " °/s3"
                           : "-"));
            joints.Add("");
            joints.Add("Журнал (последние записи):");
            KvActionLog log = KvActionLog.Instance;
            if (log != null)
            {
                IReadOnlyList<KvLogEntry> entries = log.Entries;
                int from = Mathf.Max(0, entries.Count - 24);
                for (int i = from; i < entries.Count; i++)
                {
                    KvLogEntry e = entries[i];
                    if (e == null) continue;
                    joints.Add("  [" + KvActionLog.KindLabel(e.kind) + "] " + e.text);
                }
            }
            pages.Add(joints);

            // ---------------- страница 3: оценка и что проверить перед пуском
            var verdicts = new List<string>();
            verdicts.Add("Оценка выбранной траектории и рекомендации");
            verdicts.Add("");
            if (candidate != null && candidate.plan != null)
            {
                verdicts.Add("Безопасность (SafetyGate): " + (candidate.safe ? "пройдена" : "НЕ пройдена") +
                             (string.IsNullOrEmpty(candidate.why) ? "" : " · " + candidate.why));
                verdicts.Add("Минимальный зазор: " +
                             (candidate.plan.MinClearance * 1000f).ToString("0") + " мм" +
                             (candidate.plan.MinClearance < 0.03f
                                 ? "  <-- меньше 30 мм: пересчитайте траекторию"
                                 : "  (в норме)"));
                verdicts.Add("Запас до лимитов суставов: " +
                             candidate.plan.LimitMargin.ToString("0.0") + " deg" +
                             (candidate.plan.LimitMargin < 3f ? "  <-- меньше 3 deg" : ""));
                verdicts.Add("Близость к особенности (sigma_min): " +
                             candidate.plan.SigmaMin.ToString("0.000") +
                             (candidate.plan.SigmaMin < 0.02 ? "  <-- у самой особенности" : ""));
                verdicts.Add("Время / длина: " + candidate.timeS.ToString("0.000") + " s / " +
                             candidate.lengthM.ToString("0.000") + " m");
            }
            else
            {
                verdicts.Add("Траектория не выбрана: проверять нечего.");
            }
            verdicts.Add("");
            KvStageHub4 hub4 = KvStageHub4.Current;
            if (hub4 != null)
            {
                verdicts.Add("Проверка перед пуском: " + hub4.PreRun.LastSummary);
                verdicts.Add("Имитация отказов: " + hub4.Failures.Status());
                verdicts.Add("Коллизионные прокси: " + hub4.Proxies.Status());
            }
            KvStageHub hubStages = KvStageHub.Current;
            if (hubStages != null && hubStages.Health != null && hubStages.Health.Temperature != null)
            {
                float hottest = 0f;
                int hottestJoint = -1;
                for (int i = 0; i < hubStages.Health.Temperature.Length; i++)
                    if (hubStages.Health.Temperature[i] > hottest)
                    {
                        hottest = hubStages.Health.Temperature[i];
                        hottestJoint = i;
                    }
                if (hottestJoint >= 0)
                    verdicts.Add("Самый горячий сустав: J" + (hottestJoint + 1) + " " +
                                 hottest.ToString("0.0") + " degC (предел " +
                                 hubStages.Health.maxTemperature.ToString("0") + " degC)");
            }
            verdicts.Add("");
            verdicts.Add("Что проверить перед пуском:");
            verdicts.Add("  1) свободна ли рабочая зона и нет ли людей ближе 1.2 м;");
            verdicts.Add("  2) совпадает ли груз с заданным в калькуляторе нагрузки;");
            verdicts.Add("  3) выбран ли нужный вариант траектории (кнопка ПУСК с проверкой);");
            verdicts.Add("  4) включена ли аварийная остановка и доступна ли кнопка СТОП.");
            pages.Add(verdicts);

            // ---------------- сборка PDF
            var writer = new KvPdfWriter();
            imagesIncluded = false;

            // 1) отрендеренная страница-«титул» (кириллица как изображение)
            byte[] coverJpeg = null;
            int coverW = 0, coverH = 0;
            try
            {
                coverJpeg = RenderCover(title, page, out coverW, out coverH);
            }
            catch (Exception e)
            {
                Report("страница-изображение не построена (" + e.Message +
                       ") — отчёт будет текстовым");
            }

            if (coverJpeg != null && coverJpeg.Length > 0)
            {
                writer.AddImagePage(coverJpeg, coverW, coverH,
                    KvPdfWriter.Latin("KazistovVv - отчёт по траектории"));
                imagesIncluded = true;
            }
            else
            {
                var latin = new List<string>();
                foreach (string line in page) latin.Add(KvPdfWriter.Latin(line));
                writer.AddTextPage("KazistovVv report", latin);
            }

            // 2) текстовые страницы (латиница)
            var jointsLatin = new List<string>();
            foreach (string line in joints) jointsLatin.Add(KvPdfWriter.Latin(line));
            writer.AddTextPage("Joints, kinematics and log", jointsLatin);

            var verdictsLatin = new List<string>();
            foreach (string line in verdicts) verdictsLatin.Add(KvPdfWriter.Latin(line));
            writer.AddTextPage("Assessment and pre-run checklist", verdictsLatin);

            // 3) скриншоты сцены
            for (int i = 0; i < 2; i++)
            {
                byte[] shot;
                int w, h;
                if (!TryCaptureScene(out shot, out w, out h)) break;
                writer.AddImagePage(shot, w, h, KvPdfWriter.Latin(i == 0
                    ? "Сцена: общий вид"
                    : "Сцена: текущий кадр"));
                imagesIncluded = true;
            }

            byte[] pdf = writer.Finish(KvPdfWriter.Latin("KazistovVv - отчёт по траектории"));
            string path = Path.Combine(FolderPath,
                "KazistovVv_report_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".pdf");
            try
            {
                File.WriteAllBytes(path, pdf);
            }
            catch (Exception e)
            {
                Report("PDF не записан: " + e.Message);
                return "";
            }

            lastFile = path;
            lastPages = pages.Count + (imagesIncluded ? 2 : 0);
            Report(KvLocExtra3.T("report.done", "отчёт создан") + ": " + path + " · страниц " +
                   lastPages + " · изображений: " + (imagesIncluded ? "есть" : "нет") +
                   " · размер " + (pdf.Length / 1024f).ToString("0") + " КБ");
            return path;
        }

        // ================================================================== рендер страницы

        /// <summary>
        /// Нарисовать страницу отчёта средствами Unity (шрифт интерфейса, кириллица)
        /// и вернуть её JPEG. В среде без графического устройства возвращает null —
        /// вызывающий код честно переходит на текстовую страницу.
        /// </summary>
        private byte[] RenderCover(string title, List<string> lines, out int width, out int height)
        {
            width = 1240;
            height = 1754;      // A4 при 150 dpi

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return null;    // batch-режим без графики

            var rt = new RenderTexture(width, height, 24);
            var camGo = new GameObject("KvReportCamera", typeof(Camera));
            Camera cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.targetTexture = rt;

            GameObject canvasGo = new GameObject("KvReportCanvas", typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            UnityEngine.UI.CanvasScaler scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(width, height);

            try
            {
                RectTransform root = (RectTransform)canvasGo.transform;
                var bg = new GameObject("Page", typeof(UnityEngine.UI.Image));
                bg.transform.SetParent(root, false);
                UnityEngine.UI.Image img = bg.GetComponent<UnityEngine.UI.Image>();
                img.color = Color.white;
                RectTransform bgRect = (RectTransform)bg.transform;
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;

                Text head = KvTheme.CreateText(root, "Head", title, 34, TextAnchor.UpperLeft,
                    new Color(0.08f, 0.1f, 0.15f));
                head.rectTransform.anchorMin = new Vector2(0f, 1f);
                head.rectTransform.anchorMax = new Vector2(1f, 1f);
                head.rectTransform.pivot = new Vector2(0f, 1f);
                head.rectTransform.offsetMin = new Vector2(70f, 0f);
                head.rectTransform.offsetMax = new Vector2(-70f, 0f);
                head.rectTransform.sizeDelta = new Vector2(-140f, 50f);
                head.rectTransform.anchoredPosition = new Vector2(0f, -60f);

                float y = -130f;
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line)) { y -= 14f; continue; }
                    Text t = KvTheme.CreateText(root, "Line", line, 18, TextAnchor.UpperLeft,
                        new Color(0.12f, 0.14f, 0.18f));
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    t.rectTransform.anchorMin = new Vector2(0f, 1f);
                    t.rectTransform.anchorMax = new Vector2(1f, 1f);
                    t.rectTransform.pivot = new Vector2(0f, 1f);
                    t.rectTransform.offsetMin = new Vector2(70f, 0f);
                    t.rectTransform.offsetMax = new Vector2(-70f, 0f);
                    t.rectTransform.sizeDelta = new Vector2(-140f, 24f);
                    t.rectTransform.anchoredPosition = new Vector2(0f, y);
                    y -= 26f;
                    if (y < -(height - 100f)) break;
                }

                cam.Render();
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                byte[] jpeg = tex.EncodeToJPG(82);
                UnityEngine.Object.Destroy(tex);
                return jpeg;
            }
            finally
            {
                UnityEngine.Object.Destroy(canvasGo);
                cam.targetTexture = null;
                UnityEngine.Object.Destroy(camGo);
                UnityEngine.Object.Destroy(rt);
            }
        }

        /// <summary>Снимок текущего кадра (для страницы со скриншотами).</summary>
        private bool TryCaptureScene(out byte[] jpeg, out int width, out int height)
        {
            jpeg = null;
            width = 0;
            height = 0;
            try
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    return false;
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex == null) return false;
                width = tex.width;
                height = tex.height;
                jpeg = tex.EncodeToJPG(80);
                UnityEngine.Object.Destroy(tex);
                return jpeg != null && jpeg.Length > 0;
            }
            catch (Exception e)
            {
                Report("скриншот для отчёта не сделан: " + e.Message);
                return false;
            }
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Report] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>ВКЛАДКА «ОТЧЁТ (PDF)» (ЭТАП 19 ТЗ).</summary>
    public class KvReportTab : IKvWorkbenchTab
    {
        private readonly KvReportGenerator service;

        public KvReportTab(KvReportGenerator report)
        {
            service = report;
        }

        public string Key { get { return "report"; } }
        public string Title { get { return KvLocExtra3.T("report.title", "Отчёт (PDF)"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra3.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (service == null || kit == null) return;

            kit.Section(Title);
            kit.Buttons(new[] { T("report.make", "Экспорт отчёта") },
                new Action[] { delegate { service.Generate(); } });
            kit.Info(delegate
            {
                return string.IsNullOrEmpty(service.LastFile)
                    ? T("report.info", "Отчёт ещё не создавался")
                    : T("report.done", "отчёт создан") + ": " + service.LastFile + " · страниц " +
                      service.LastPages + (service.ImagesIncluded ? " · со скриншотами" : "");
            }, KvTheme.Ok);
            kit.Info(delegate { return T("report.folder", "Папка отчётов") + ": " + service.FolderPath; },
                KvTheme.TextDim);
            kit.Note(T("report.info",
                "PDF собирается кодом (без внешних библиотек): заголовок, метрики траектории, " +
                "таблица суставов, кинематика и скриншоты сцены."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
