using UnityEngine;

namespace KazistovVvAr
{
    /// <summary>
    /// ИНТРИНСИКЕ КАМЕРЫ ДЛЯ ПОЗЫ МАРКЕРА (добавлено 16.11.2026, волна «Маркерная постановка»).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНАЯ СТРУКТУРА. Поза считается из четырёх углов QR и известного физического
    /// размера маркера; без фокусного расстояния из метры не получить пиксели. Камера приходит
    /// либо настоящими интринсиками ARCore/ARFoundation (`approximate == false`), либо фолбэком
    /// `fx ≈ ширина кадра` — тогда результат помечается `approximate == true`, и статус честно
    /// сообщает, что поза приблизительная (см. `KvMarkerPoseStatus.IntrinsicsApproximate`).
    ///
    /// ФОЛБЭК ЗАЧЕМ. Планшет-инициатор волни (Redmi Pad 2 Pro) не ARCore-совместим: трекаблов 0,
    /// плоскостей 0, но кадр живой и повороты камеры отдаются. Ждать сертификации устройства
    /// нельзя — фолбэк даёт рабочую (пусть и грубую) позу, а честный статус не даёт оператору
    /// принять грубую позу за точную.
    /// </summary>
    public struct KvMarkerIntrinsics
    {
        /// <summary>Фокус по X, в пикселях того же кадра, что и углы маркера.</summary>
        public float fx;

        /// <summary>Фокус по Y, в пикселях.</summary>
        public float fy;

        /// <summary>Главная точка по X (центр кадра, если интринсики приблизительные).</summary>
        public float cx;

        /// <summary>Главная точка по Y (центр кадра, если интринсики приблизительные).</summary>
        public float cy;

        /// <summary>true — интринсики приблизительные (фолбэк): поза считается, но помечается как неточная.</summary>
        public bool approximate;

        /// <summary>
        /// ФОЛБЭК-ИНТРИНСИКИ: fx = fy ≈ ширина кадра в пикселях, главная точка — центр кадра.
        /// Точность грубая (ошибка фокуса на реальной камере десятки процентов), поэтому
        /// `approximate = true` — статус обязан это показать (честность §1).
        /// </summary>
        public static KvMarkerIntrinsics FromFrame(float frameWidthPx, float frameHeightPx)
        {
            return new KvMarkerIntrinsics
            {
                fx = frameWidthPx,
                fy = frameWidthPx,
                cx = frameWidthPx * 0.5f,
                cy = frameHeightPx * 0.5f,
                approximate = true
            };
        }
    }

    /// <summary>ИСХОД ПОСЧЁТА ПОЗЫ МАРКЕРА (честный: отказ ≠ успех).</summary>
    public enum KvMarkerPoseStatus
    {
        /// <summary>Поза посчитана настоящими интринсиками.</summary>
        Ok = 0,

        /// <summary>Углов нет или они не пришли от сканера — маркер не виден.</summary>
        MarkerNotVisible = 1,

        /// <summary>Маркер проецируется меньше порога в пикселях — из него нечего считать.</summary>
        MarkerTooSmall = 2,

        /// <summary>Маркер слишком косо к камере — поза ненадёжна (угол выше допуска).</summary>
        MarkerTooOblique = 3,

        /// <summary>Поза посчитана фолбэк-интринсиками: результат есть, но он приблизительный.</summary>
        IntrinsicsApproximate = 4,

        /// <summary>Углы вырождены (коллинеарны/повторяются) — гомография не строится.</summary>
        Degenerate = 5
    }

    /// <summary>РЕЗУЛЬТАТ ПОЗЫ МАРКЕРА (значение, без ссылок на сцену).</summary>
    public struct KvMarkerPoseResult
    {
        /// <summary>Честный исход: успех или конкретный отказ.</summary>
        public KvMarkerPoseStatus status;

        /// <summary>Человеческая причина для статуса/журнала; null на успехе без предупреждений.</summary>
        public string reason;

        /// <summary>true — позой можно пользоваться (Ok либо приблизительная без запрета).</summary>
        public bool usable;

        /// <summary>Поза посчитана фолбэк-интринсиками (fx ≈ ширина кадра).</summary>
        public bool intrinsicsApproximate;

        /// <summary>Позиция центра маркера в системе координат камеры, метры.</summary>
        public Vector3 position;

        /// <summary>Ориентация маркера в системе координат камеры: `rotation * Vector3.forward` — нормаль маркера.</summary>
        public Quaternion rotation;

        /// <summary>Дистанция камера → маркер, метры.</summary>
        public float distance;

        /// <summary>Угол между нормалью маркера и направлением на него: 0° — фронтально, 90° — с ребра.</summary>
        public float tiltDegrees;

        /// <summary>Средняя сторона маркера в пикселях кадра (диагностика «слишком мал»).</summary>
        public float markerPixels;
    }

    /// <summary>
    /// ПОЗА QR-МАРКЕРА ПО ЧЕТЫРЁМ УГЛАМ — чистая статическая математика (добавлено 16.11.2026).
    ///
    /// АЛГОРИТМ. Гомография из четырёх соответствий (углы маркера в пикселях ↔ углы маркера
    /// в метрах его плоскости) решается ДЛТ-системой8×8, затем H = K·[r1 r2 t] разкладывается:
    /// M = K⁻¹·H, r1/r2/t — столбцы M с нормировкой, r3 = r1×r2. См. документацию по шагам ниже.
    ///
    /// ТРЕБОВАНИЯ К ТЕСТАМ (D). Класс не зависит от сцены: тест подставляет синтетические углы,
    /// полученные обратной проекцией известной позы, и сверяет восстановленную позу.
    ///
    /// ПОЧЕМУ БЕЗ ARCORE. Поза считается из кадра камеры — сессия AR трекается и без плоскостей
    /// (диагностика «плоскостей 0» при живой камере), поэтому постановка по маркеру работает
    /// на устройствах без ARCore-сертификации.
    /// </summary>
    public static class KvMarkerPose
    {
        /// <summary>Минимальная средняя сторона маркера в пикселях: меньше — поза из шума.</summary>
        public const float MinMarkerPixels = 40f;

        /// <summary>Максимальный угол косизны: выше — отказ «слишком под углом».</summary>
        public const float MaxTiltDegrees = 60f;

        /// <summary>
        /// Посчитать позу маркера относительно камеры по четырём углам в пикселях кадра.
        ///
        /// Порядок углов произвольен: обмотка нормализуется (шейлес), зеркальность развёртки
        /// снимается поворотом нормали лицом к камере. Маркер квадратный — для постановки стола
        /// важны центр, плоскость и наклон, а не ориентация рисунка в плоскости.
        ///
        /// <paramref name="requireExactIntrinsics"/> = true превращает фолбэк-интринсики в отказ:
        /// так вызывающий может выбрать «лучше не ставить, чем ставить приблизительно».
        /// </summary>
        public static KvMarkerPoseResult Compute(
            Vector2[] imageCorners,
            float markerSizeMeters,
            KvMarkerIntrinsics intrinsics,
            bool requireExactIntrinsics = false)
        {
            KvMarkerPoseResult result = default(KvMarkerPoseResult);
            result.intrinsicsApproximate = intrinsics.approximate;
            result.usable = false;

            if (imageCorners == null || imageCorners.Length < 4)
            {
                result.status = KvMarkerPoseStatus.MarkerNotVisible;
                result.reason = "маркер не виден: сканер не вернул четыре угла";
                return result;
            }

            if (markerSizeMeters <= 0f)
            {
                result.status = KvMarkerPoseStatus.Degenerate;
                result.reason = "размер маркера не задан — позу не посчитать";
                return result;
            }

            if (intrinsics.fx <= 0f || intrinsics.fy <= 0f ||
                float.IsNaN(intrinsics.fx) || float.IsNaN(intrinsics.fy))
            {
                result.status = KvMarkerPoseStatus.Degenerate;
                result.reason = "интринсики камеры не заданы — позу не посчитать";
                return result;
            }

            Vector2[] p = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 c = imageCorners[i];
                if (float.IsNaN(c.x) || float.IsNaN(c.y) ||
                    float.IsInfinity(c.x) || float.IsInfinity(c.y))
                {
                    result.status = KvMarkerPoseStatus.Degenerate;
                    result.reason = "координаты углов маркера некорректны (NaN/Infinity)";
                    return result;
                }
                p[i] = c;
            }

            // РАЗМЕР В ПИКСЕЛЯХ: средняя сторона четырёхугольника. Порог «слишком мал» —
            // честный отказ до всякой алгебры: из двадцати пикселей поза не вычисляется.
            float side = (Distance(p[0], p[1]) + Distance(p[1], p[2]) +
                          Distance(p[2], p[3]) + Distance(p[3], p[0])) * 0.25f;
            result.markerPixels = side;
            if (side < MinMarkerPixels)
            {
                result.status = KvMarkerPoseStatus.MarkerTooSmall;
                result.reason = "маркер слишком мал: " + Format(side) + " px (минимум " +
                                Format(MinMarkerPixels) + ") — поднесите камеру ближе";
                return result;
            }

            // ПЛОЩАДЬ: нулевая площадь = коллинеарные углы, гомография вырождена.
            float area = Mathf.Abs(SignedArea(p)) * 0.5f;
            if (area < 4f)
            {
                result.status = KvMarkerPoseStatus.Degenerate;
                result.reason = "углы маркера вырождены: площадь " + Format(area) + " px²";
                return result;
            }

            // ОБМОТКА: приводим к положительному шейлесу (TL,TR,BR,BL в кадре).
            // Тогда углы в кадре и углы в метрах плоскости маркера гарантированно одного смысла,
            // и ДЛТ не выдаёт зеркальное (левое) разложение.
            if (SignedArea(p) < 0f)
            {
                Vector2 tmp = p[1];
                p[1] = p[3];
                p[3] = tmp;
            }

            // Метры плоскости маркера: квадрат со стороной markerSizeMeters, центр в начале,
            // вершины TL,TR,BR,BL против часовой (при взгляде на маркер со стороны камеры).
            double a = markerSizeMeters * 0.5;
            double[] objX = { -a, a, a, -a };
            double[] objY = { a, a, -a, -a };
            double[] u = { p[0].x, p[1].x, p[2].x, p[3].x };
            double[] v = { p[0].y, p[1].y, p[2].y, p[3].y };

            // ДЛТ: H (3×3, h33 = 1) из восьми уравнений — по два на соответствие.
            double[,] eq = new double[8, 9];
            for (int i = 0; i < 4; i++)
            {
                int r = i * 2;
                eq[r, 0] = objX[i];
                eq[r, 1] = objY[i];
                eq[r, 2] = 1.0;
                eq[r, 6] = -u[i] * objX[i];
                eq[r, 7] = -u[i] * objY[i];
                eq[r, 8] = u[i];

                eq[r + 1, 3] = objX[i];
                eq[r + 1, 4] = objY[i];
                eq[r + 1, 5] = 1.0;
                eq[r + 1, 6] = -v[i] * objX[i];
                eq[r + 1, 7] = -v[i] * objY[i];
                eq[r + 1, 8] = v[i];
            }

            double[] h = new double[8];
            if (!Solve8x8(eq, h))
            {
                result.status = KvMarkerPoseStatus.Degenerate;
                result.reason = "углы маркера вырождены: гомография не решается";
                return result;
            }

            double[,] hm =
            {
                { h[0], h[1], h[2] },
                { h[3], h[4], h[5] },
                { h[6], h[7], 1.0 }
            };

            // K в проекции кадра: u = fx·x/z + cx, v = cy − fy·y/z (ось Y камеры вверх,
            // ось Y кадра вниз — знак минус в матрице K).
            double[,] kinv =
            {
                { 1.0 / intrinsics.fx, 0.0, -intrinsics.cx / intrinsics.fx },
                { 0.0, -1.0 / intrinsics.fy, intrinsics.cy / intrinsics.fy },
                { 0.0, 0.0, 1.0 }
            };

            double[,] m = Multiply3x3(kinv, hm);

            // Столбцы M: r1 ∝ m1, r2 ∝ m2, t ∝ m3. Нормировка λ = 2/(‖m1‖+‖m2‖).
            double m1x = m[0, 0], m1y = m[1, 0], m1z = m[2, 0];
            double m2x = m[0, 1], m2y = m[1, 1], m2z = m[2, 1];
            double m3x = m[0, 2], m3y = m[1, 2], m3z = m[2, 2];

            double n1 = System.Math.Sqrt(m1x * m1x + m1y * m1y + m1z * m1z);
            double n2 = System.Math.Sqrt(m2x * m2x + m2y * m2y + m2z * m2z);
            if (n1 < 1e-12 || n2 < 1e-12)
            {
                result.status = KvMarkerPoseStatus.Degenerate;
                result.reason = "углы маркера вырождены: направление не определяется";
                return result;
            }

            double lambda = 2.0 / (n1 + n2);
            double r1x = lambda * m1x, r1y = lambda * m1y, r1z = lambda * m1z;
            double r2x = lambda * m2x, r2y = lambda * m2y, r2z = lambda * m2z;
            double tx = lambda * m3x, ty = lambda * m3y, tz = lambda * m3z;

            // Знак H: маркер должен быть ПЕРЕД камерой (t.z > 0). Смена знака H не меняет
            // нормаль (r3 = r1×r2 — квадрат знака) и разворачивает t наружу.
            if (tz < 0.0)
            {
                r1x = -r1x; r1y = -r1y; r1z = -r1z;
                r2x = -r2x; r2y = -r2y; r2z = -r2z;
                tx = -tx; ty = -ty; tz = -tz;
            }

            // Ортонормализация (Грам-Шмидт): данные с шумом дают нестрого ортонормированные r1/r2.
            double r1len = System.Math.Sqrt(r1x * r1x + r1y * r1y + r1z * r1z);
            if (r1len < 1e-12)
            {
                result.status = KvMarkerPoseStatus.Degenerate;
                result.reason = "углы маркера вырождены: базис не построился";
                return result;
            }
            r1x /= r1len; r1y /= r1len; r1z /= r1len;

            double proj = r2x * r1x + r2y * r1y + r2z * r1z;
            r2x -= proj * r1x; r2y -= proj * r1y; r2z -= proj * r1z;
            double r2len = System.Math.Sqrt(r2x * r2x + r2y * r2y + r2z * r2z);
            if (r2len < 1e-9)
            {
                result.status = KvMarkerPoseStatus.Degenerate;
                result.reason = "углы маркера вырождены: плоскость не определяется";
                return result;
            }
            r2x /= r2len; r2y /= r2len; r2z /= r2len;

            double r3x = r1y * r2z - r1z * r2y;
            double r3y = r1z * r2x - r1x * r2z;
            double r3z = r1x * r2y - r1y * r2x;

            // НОРМАЛЬ ЛИЦОМ К КАМЕРЕ: маркер лежит на столе и виден с его стороны —
            // нормаль (r3) обязана смотреть назад, на камеру: dot(r3, t) < 0. Иначе —
            // развёртка зеркальная; поворот на π вокруг r1 (r2→−r2, r3→−r3) сохраняет
            // ортонормированный базис (det +1), центр и плоскость, меняя только знак нормали.
            if (r3x * tx + r3y * ty + r3z * tz > 0.0)
            {
                r2x = -r2x; r2y = -r2y; r2z = -r2z;
                r3x = -r3x; r3y = -r3y; r3z = -r3z;
            }

            Vector3 position = new Vector3((float)tx, (float)ty, (float)tz);
            Vector3 forward = new Vector3((float)r3x, (float)r3y, (float)r3z);
            Vector3 up = new Vector3((float)r2x, (float)r2y, (float)r2z);

            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsNaN(forward.x) || float.IsNaN(forward.y) || float.IsNaN(forward.z) ||
                forward.sqrMagnitude < 0.25f || up.sqrMagnitude < 0.25f)
            {
                result.status = KvMarkerPoseStatus.Degenerate;
                result.reason = "углы маркера вырождены: поза не определяется";
                return result;
            }

            Quaternion rotation = Quaternion.LookRotation(forward, up);
            float distance = position.magnitude;
            Vector3 view = position / distance;

            // КОСИЗНА: угол между нормалью маркера и направлением на него. 0° — фронтально.
            float cosTilt = Mathf.Clamp(-(forward.x * view.x + forward.y * view.y + forward.z * view.z), -1f, 1f);
            float tilt = Mathf.Acos(cosTilt) * Mathf.Rad2Deg;
            if (tilt > MaxTiltDegrees)
            {
                result.status = KvMarkerPoseStatus.MarkerTooOblique;
                result.reason = "маркер слишком под углом: " + Format(tilt) + "° (максимум " +
                                Format(MaxTiltDegrees) + "°) — поднесите камеру перпендикулярно маркеру";
                return result;
            }

            result.position = position;
            result.rotation = rotation;
            result.distance = distance;
            result.tiltDegrees = tilt;

            if (intrinsics.approximate)
            {
                // ФОЛБЭК-ИНТРИНСИКИ: поза есть, но точность грубая. Статус честно называет
                // её приблизительной; `requireExactIntrinsics` даёт вызывающему право отказать.
                result.status = KvMarkerPoseStatus.IntrinsicsApproximate;
                result.intrinsicsApproximate = true;
                result.reason = requireExactIntrinsics
                    ? "интринсики приблизительные (fx ≈ ширина кадра) — точная постановка невозможна"
                    : "интринсики приблизительные (fx ≈ ширина кадра) — поза приблизительная";
                result.usable = !requireExactIntrinsics;
            }
            else
            {
                result.status = KvMarkerPoseStatus.Ok;
                result.reason = null;
                result.usable = true;
            }

            return result;
        }

        /// <summary>
        /// СТРОКА УСПЕХА МАРКЕРНОЙ ПОСТАНОВКИ: «стол поставлен по маркеру: дистанция X м,
        /// наклон Y°». Формат фиксируется тестом — оператор видит именно эти числа.
        /// </summary>
        public static string SuccessStatus(float distanceMeters, float tiltDegrees, bool intrinsicsApproximate = false)
        {
            string line = "стол поставлен по маркеру: дистанция " + Format(distanceMeters) +
                          " м, наклон " + Format(tiltDegrees) + "°";
            if (intrinsicsApproximate)
            {
                line += " · внимание: интринсики приблизительные (fx ≈ ширина кадра)";
            }
            return line;
        }

        // ------------------------------------------------------------ чистая алгебра

        private static float Distance(Vector2 a, Vector2 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Двукратная площадь со знаком (шейлес по четырём точкам).</summary>
        private static float SignedArea(Vector2[] p)
        {
            float sum = 0f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 c = p[i];
                Vector2 n = p[(i + 1) % 4];
                sum += c.x * n.y - n.x * c.y;
            }
            return sum;
        }

        /// <summary>Решение СЛАУ 8×8 методом Гаусса с выбором ведущего элемента.</summary>
        private static bool Solve8x8(double[,] eq, double[] solution)
        {
            const int n = 8;
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double best = System.Math.Abs(eq[col, col]);
                for (int row = col + 1; row < n; row++)
                {
                    double val = System.Math.Abs(eq[row, col]);
                    if (val > best)
                    {
                        best = val;
                        pivot = row;
                    }
                }
                if (best < 1e-12)
                {
                    return false;
                }
                if (pivot != col)
                {
                    for (int k = col; k <= n; k++)
                    {
                        double tmp = eq[col, k];
                        eq[col, k] = eq[pivot, k];
                        eq[pivot, k] = tmp;
                    }
                }
                double diag = eq[col, col];
                for (int k = col; k <= n; k++)
                {
                    eq[col, k] /= diag;
                }
                for (int row = 0; row < n; row++)
                {
                    if (row == col)
                    {
                        continue;
                    }
                    double factor = eq[row, col];
                    if (factor == 0.0)
                    {
                        continue;
                    }
                    for (int k = col; k <= n; k++)
                    {
                        eq[row, k] -= factor * eq[col, k];
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                solution[i] = eq[i, n];
            }
            return true;
        }

        private static double[,] Multiply3x3(double[,] a, double[,] b)
        {
            double[,] r = new double[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < 3; k++)
                    {
                        sum += a[i, k] * b[k, j];
                    }
                    r[i, j] = sum;
                }
            }
            return r;
        }

        /// <summary>Число для статуса: точка, без локали (иначе «0,85» vs «0.85» ломает сверку).</summary>
        private static string Format(float value)
        {
            return value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
