using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvAr
{
    /// <summary>
    /// ДЕТЕКТОР QR-МАРКЕРА: четыре угла кода из кадра (добавлено 16.11.2026, волна «Маркерная постановка»).
    ///
    /// ЧТО ЭТО И ПОЧЕМУ ЗДЕСЬ. Волна A–D требует переиспользовать «существующий сканер QR» —
    /// его в репозитории НЕТ: `KvToolKeyMouse` и «Найти (QR)» в истории git (все ветки), в
    /// `Assets`, в бэкапах и в локализации отсутствуют (проверено поиском по всему дому и
    /// `git log -S`). Поэтому источнику четырёх углов построен свой детектор — БЕЗ сторонних
    /// CV-библиотек (правило «реальные файлы»/детерминированные ворота): классика QR — три
    /// «угловых ушка» с пропорцией рамки 1:1:3:1:1, они же находятся сканированием строк.
    ///
    /// АЛГОРИТМ (по ZXING-логике, свой код):
    ///  1. Яркость → порог (среднее по кадру), бинаризация.
    ///  2. По каждой строке — последовательности запусков чёрное/белое; ищется пятерка
    ///     чёрное-белое-чёрное-белое-чёрное с пропорцией 1:1:3:1:1 (допуск ±50%).
    ///     Кандидат = центр среднего (широкого) запуска + модуль (ширина/3).
    ///  3. Кластеризация кандидатов по близости (±4 модуля): три ушка дают три плотных
    ///     кластера; воронки данных дают единичные кандидаты и отсеиваются голосами.
    ///  4. Геометрия: вершина с прямым углом — начало координат кода (ушко TL), два
    ///     равных катета — направления к ушкам TR и BL; четвёртое ушко достраивается,
    ///     внешние углы кода — по смещению3.5 модуля от центров ушек вдоль осей кода.
    ///
    /// ТРЕБОВАНИЯ К ТЕСТАМ (D). Класс чистый: тест рисует синтетический код (три ушка в
    /// `Color32[]`) и сверяет углы с известной геометрией; пустой кадр — честный отказ.
    /// </summary>
    public static class KvQrMarkerDetector
    {
        /// <summary>Минимум кандидатов в кластере, чтобы считать его ушком (полоса ушка даёт строки).</summary>
        public const int MinClusterVotes = 3;

        /// <summary>
        /// Найти четыре угла QR в кадре. Пиксели — RGB/RGBA32 кадра, порядок строк сверху вниз.
        /// Порядок возвращаемых углов: TL, TR, BR, BL кода (дальше нормализуется в `KvMarkerPose`).
        /// </summary>
        public static bool TryFindCorners(Color32[] pixels, int width, int height,
            out Vector2[] corners, out string reason)
        {
            corners = null;
            reason = null;

            if (pixels == null || width < 64 || height < 64 || pixels.Length < width * height)
            {
                reason = "кадр для сканирования маркера пуст или слишком мал";
                return false;
            }

            int count = width * height;
            byte[] lum = new byte[count];
            int min = 255;
            int max = 0;
            long sum = 0;
            for (int i = 0; i < count; i++)
            {
                Color32 c = pixels[i];
                int l = (c.r * 299 + c.g * 587 + c.b * 114) / 1000;
                lum[i] = (byte)l;
                sum += l;
                if (l < min) min = l;
                if (l > max) max = l;
            }
            if (max - min < 40)
            {
                reason = "контраст кадра слишком низкий — маркер не видно";
                return false;
            }

            int threshold = (int)(sum / count);
            List<Candidate> candidates = new List<Candidate>(256);
            int[] runStart = new int[width + 1];
            int[] runLength = new int[width + 1];
            bool[] runDark = new bool[width + 1];

            int patternLimit = Mathf.Min(width, height) / 2;
            if (patternLimit < 7)
            {
                reason = "кадр слишком мал для сканирования маркера";
                return false;
            }

            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                int runCount = 0;
                int x = 0;
                while (x < width)
                {
                    bool dark = lum[rowBase + x] < threshold;
                    int start = x;
                    while (x < width && (lum[rowBase + x] < threshold) == dark)
                    {
                        x++;
                    }
                    runStart[runCount] = start;
                    runLength[runCount] = x - start;
                    runDark[runCount] = dark;
                    runCount++;
                }

                for (int i = 0; i + 4 < runCount; i++)
                {
                    if (!runDark[i] || runDark[i + 1] || !runDark[i + 2] ||
                        runDark[i + 3] || !runDark[i + 4])
                    {
                        continue;
                    }
                    int l0 = runLength[i];
                    int l1 = runLength[i + 1];
                    int l2 = runLength[i + 2];
                    int l3 = runLength[i + 3];
                    int l4 = runLength[i + 4];
                    int total = l0 + l1 + l2 + l3 + l4;
                    if (total < 7 || total > patternLimit)
                    {
                        continue;
                    }
                    float module = total / 7f;
                    if (module < 1.5f)
                    {
                        continue;
                    }
                    // Пропорция 1:1:3:1:1 с допуском ±50% на каждый запуск.
                    if (!Within(l0, module, 1.5f) || !Within(l1, module, 1.5f) ||
                        !Within(l2, module * 3f, 1.5f) ||
                        !Within(l3, module, 1.5f) || !Within(l4, module, 1.5f))
                    {
                        continue;
                    }
                    Candidate cand;
                    cand.x = runStart[i + 2] + runLength[i + 2] * 0.5f;
                    cand.y = y;
                    cand.module = module;
                    candidates.Add(cand);
                }
            }

            if (candidates.Count == 0)
            {
                reason = "не найдены три уголка QR: в кадре нет рисунка кода";
                return false;
            }

            // КЛАСТЕРИЗАЦИЯ: кандидаты одного ушка лежат в пределах ±4 модулей от его центра.
            List<Cluster> clusters = new List<Cluster>(16);
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate c = candidates[i];
                Cluster best = null;
                double bestDist = double.MaxValue;
                for (int j = 0; j < clusters.Count; j++)
                {
                    Cluster cl = clusters[j];
                    double tol = cl.AverageModule * 4.0;
                    double dx = c.x - cl.Cx;
                    double dy = c.y - cl.Cy;
                    if (System.Math.Abs(dx) > tol || System.Math.Abs(dy) > tol)
                    {
                        continue;
                    }
                    double d = dx * dx + dy * dy;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = cl;
                    }
                }
                if (best != null)
                {
                    best.Add(c);
                }
                else
                {
                    Cluster fresh = new Cluster();
                    fresh.Add(c);
                    clusters.Add(fresh);
                }
            }

            // ТРИ САМЫХ ПЛОТНЫХ КЛАСТЕРА = три ушка. Ушко даёт голоса на всю полосу своего
            // центра (≈3 модуля строк), единичный мусор из данных кода — на единицы.
            List<Cluster> strong = new List<Cluster>(4);
            for (int i = 0; i < clusters.Count; i++)
            {
                if (clusters[i].Votes >= MinClusterVotes)
                {
                    strong.Add(clusters[i]);
                }
            }
            if (strong.Count < 3)
            {
                reason = "не найдены три уголка QR: кластеров с ушками " + strong.Count +
                         " из3 — поднесите камеру к коду и держите его целиком в кадре";
                return false;
            }
            strong.Sort(CompareByVotes);
            Vector2 p0 = strong[0].Center;
            Vector2 p1 = strong[1].Center;
            Vector2 p2 = strong[2].Center;
            float m0 = (float)strong[0].AverageModule;
            float m1 = (float)strong[1].AverageModule;
            float m2 = (float)strong[2].AverageModule;

            // НАЧАЛО КООРДИНАТ КОДА — вершина с прямым углом (против самой длинной стороны).
            Vector2 origin;
            Vector2 legA;
            Vector2 legB;
            float mOrigin;
            float mA;
            float mB;
            float d01 = (p0 - p1).magnitude;
            float d02 = (p0 - p2).magnitude;
            float d12 = (p1 - p2).magnitude;
            if (d12 >= d01 && d12 >= d02)
            {
                origin = p0; legA = p1; legB = p2;
                mOrigin = m0; mA = m1; mB = m2;
            }
            else if (d02 >= d01)
            {
                origin = p1; legA = p0; legB = p2;
                mOrigin = m1; mA = m0; mB = m2;
            }
            else
            {
                origin = p2; legA = p0; legB = p1;
                mOrigin = m2; mA = m0; mB = m1;
            }

            Vector2 u = legA - origin;
            Vector2 v = legB - origin;
            float ulen = u.magnitude;
            float vlen = v.magnitude;
            if (ulen < 1f || vlen < 1f)
            {
                reason = "уголки QR слиплись — поднесите камеру ближе";
                return false;
            }
            u /= ulen;
            v /= vlen;

            // Ориентация осей кода: приводим к положительному повороту (детерминизм порядка углов).
            if (u.x * v.y - u.y * v.x < 0f)
            {
                Vector2 tmpU = u;
                u = v;
                v = tmpU;
                float tmpLen = ulen;
                ulen = vlen;
                vlen = tmpLen;
                Vector2 tmpP = legA;
                legA = legB;
                legB = tmpP;
                float tmpM = mA;
                mA = mB;
                mB = tmpM;
            }

            // Мягкая проверка «прямой угол»: под перспективой катеты наклоняются, поэтому
            // порог мягкий — жёсткая геометрия уже проверена в позе (слишком косо → отказ).
            if (System.Math.Abs(u.x * v.x + u.y * v.y) > 0.9f)
            {
                reason = "уголки QR не образуют квадрат — пойман мусор, а не код";
                return false;
            }

            // ВНЕШНИЕ УГЛЫ КОДА: центр ушка отстоит от краёв кода на 3.5 модуля вдоль осей.
            Vector2 tl = origin - (u + v) * (3.5f * mOrigin);
            Vector2 tr = legA + (u * 3.5f * mA) - (v * 3.5f * mA);
            Vector2 bl = legB - (u * 3.5f * mB) + (v * 3.5f * mB);
            Vector2 br = tr + bl - tl;

            // Четвёртый угол (BR) достраивается по параллелограмму — при фронтальном виде
            // это точно угол кода, при лёгком наклоне ошибка меньше порога «слишком мал».
            Vector2[] found = { tl, tr, br, bl };
            float limitX = width * 1.5f;
            float limitY = height * 1.5f;
            for (int i = 0; i < 4; i++)
            {
                if (float.IsNaN(found[i].x) || float.IsNaN(found[i].y) ||
                    System.Math.Abs(found[i].x) > limitX || System.Math.Abs(found[i].y) > limitY)
                {
                    reason = "геометрия QR вне кадра — код не читается целиком";
                    return false;
                }
            }

            corners = found;
            reason = null;
            return true;
        }

        /// <summary>Проверка «значение в пределах factor×эталона» (пропорции ушка).</summary>
        private static bool Within(int value, float expected, float factor)
        {
            float low = expected / factor;
            float high = expected * factor;
            return value >= low && value <= high;
        }

        private static int CompareByVotes(Cluster a, Cluster b)
        {
            return b.Votes.CompareTo(a.Votes);
        }

        /// <summary>Кандидат-центр ушка в одной строке кадра.</summary>
        private struct Candidate
        {
            public float x;
            public float y;
            public float module;
        }

        /// <summary>Кластер кандидатов одного углового ушка.</summary>
        private sealed class Cluster
        {
            private double sumX;
            private double sumY;
            private double sumModule;

            public int Votes { get; private set; }

            public double Cx => Votes == 0 ? 0 : sumX / Votes;

            public double Cy => Votes == 0 ? 0 : sumY / Votes;

            public double AverageModule => Votes == 0 ? 0 : sumModule / Votes;

            public Vector2 Center => new Vector2((float)Cx, (float)Cy);

            public void Add(Candidate c)
            {
                sumX += c.x;
                sumY += c.y;
                sumModule += c.module;
                Votes++;
            }
        }
    }
}
