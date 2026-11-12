using System.Text.RegularExpressions;
using KazistovVvAr;
using NUnit.Framework;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace KazistovVvTests
{
    /// <summary>
    /// ПОЗА AR-КАМЕРЫ И ПЕРИОДИЧЕСКИЙ КАДР — ДВЕ ДИАГНОСТИКИ, КОТОРЫХ НЕ БЫЛО (12.11.2026).
    ///
    /// ОТКУДА ЗАДАЧА. На приёмке планшета получен ложный сигнал:
    ///     `[KvAr] ПОВЕРХНОСТИ: … плоскостей 0 …`
    /// и выглядел он как поломка распознавания. На самом деле снятый кадр камеры показал ПОТОЛОК:
    /// планшет лежал экраном вверх, камера смотрела в небо, настоящий стол в кадр не попадал —
    /// поэтому ARCore и не мог построить карту. Оператор и приёмка должны видеть это СРАЗУ, а не
    /// гадать по косвенным признакам.
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ И ПОЧЕМУ ИМЕННО ТАК.
    ///   1) ПОЗА КАМЕРЫ — `KvArPlaneVisualizer.CameraPoseState` / `CameraTiltDegrees` /
    ///      `DescribeCameraPose`. Все три — ЧИСТЫЕ функции: на вход одно число `dot(forward, up)`,
    ///      на выход состояние или строка. Поэтому три различимых состояния и границы порога
    ///      проверяются машиной, а не «посмотрели на планшет один раз».
    ///   2) СВЯЗКА ДВУХ ДИАГНОСТИК — `PoseCulpritSuffix` и `DescribeSurfaceState(..., cameraDotUp)`.
    ///      Строка «плоскостей 0» ОБЯЗАНА сослаться на позу, когда камера смотрит вверх, и
    ///      ОБЯЗАНА промолчать, когда поза нормальная. Второе — анти-вакуум: без него ссылка на
    ///      позу висела бы и на рабочей сессии, то есть «причина» находилась бы всегда.
    ///   3) ПЕРИОДИЧЕСКИЙ КАДР — `KvArCameraGate.FrameFileName` / `DescribeFrameSaved` /
    ///      `ShouldCaptureFrame`. Один файл за сеанс (`ar_camera_frame.png`) не доказывал, что
    ///      камера видела ВО ВРЕМЯ РАБОТЫ; кольцо имён `ar_frame_001…010` пишется периодически и
    ///      не копит файлы на устройстве.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ И ПОЧЕМУ. Нет ни сцены, ни устройства, ни файловой системы: `Camera` и
    /// `ARCameraManager` в EditMode не поднимаются, а единственное доказательство «на планшете
    /// работает» всё равно даёт прогон на приёмке. Задача теста — запереть ПРАВИЛО (порог, знак,
    /// кольцо номеров), чтобы его нельзя было сломать незаметно.
    /// </summary>
    [Category(KvTestCategories.Fast)]
    public class KvArCameraPoseFrameTests
    {
        /// <summary>
        /// «У КАЖДОЙ СТРОКИ ЕСТЬ ЧИСЛО» — анти-вакуум для этого файла.
        ///
        /// ЗАЧЕМ. Строка журнала без числа («камера смотрит не туда») не отличается от строки
        /// журнала без числа в соседнем дефекте, и приёмка не может сказать, НАСКОЛЬКО не туда.
        /// Требование владельца прямое: «строка с числами».
        /// </summary>
        private static readonly Regex HasNumber = new Regex(@"[0-9]", RegexOptions.CultureInvariant);

        /// <summary>Строка обязана содержать число — иначе она не доказательство, а пересказ.</summary>
        private static void AssertHasNumber(string line, string what)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(line), what + ": строка пустая");
            Assert.IsTrue(HasNumber.IsMatch(line),
                what + ": в строке НЕТ НИ ОДНОГО ЧИСЛА — по ней нельзя ничего измерить: " + line);
        }

        // ================================================================ (а) ПОЗА: ТРИ СОСТОЯНИЯ

        /// <summary>
        /// (а-1) ТРИ РАЗЛИЧИМЫХ СОСТОЯНИЯ. Камера смотрит вверх / вниз / ровно — три РАЗНЫХ
        /// значения enum, а не «два состояния и пустота». Ровно этот набор и позволяет отличить
        /// «планшет лежит экраном вверх» от «оператор держит планшет над столом».
        ///
        /// ЧТО БЫЛО НЕ ТАК (исправлено 12.11.2026). Тест ждал `Down` при `dot = −0.42`, а порог
        /// проекта — `±0.5`: `−0.42` по модулю МЕНЬШЕ 0.5, и код честно отвечал `Level`. То есть
        /// тест был написан ПОД ДРУГОЙ ПОРОГ, чем сам код, и прогон в Unity это показал. Ошибка была
        /// в тесте, а не в пороге: 25° вниз — это ещё не «уверенно на стол», а почти горизонт,
        /// и `Level` для него — верный ответ.
        ///
        /// ЧТО СТАЛО. Значения взяты ИЗ РЕАЛЬНОГО КОРИДОРА порога: `Down` — заведомо ниже `−0.5`
        /// (`−0.87`, это ~60° вниз, камера прямо над столешницей), `Level` — внутри коридора
        /// (`−0.42`, ~25° вниз: обычная рабочая поза у горизонта).
        /// </summary>
        [Test]
        public void CameraPoseState_ThreeDistinctStates()
        {
            KvArPlaneVisualizer.KvCameraPoseState up = KvArPlaneVisualizer.CameraPoseState(0.87f);
            KvArPlaneVisualizer.KvCameraPoseState down = KvArPlaneVisualizer.CameraPoseState(-0.87f);
            KvArPlaneVisualizer.KvCameraPoseState level = KvArPlaneVisualizer.CameraPoseState(0.05f);

            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Up, up,
                "dot(forward, up) = +0.87 — объектив смотрит ВВЕРХ (в потолок)");
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Down, down,
                "dot(forward, up) = −0.87 — объектив смотрит ВНИЗ (на стол)");
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Level, level,
                "dot(forward, up) ≈ 0 — объектив почти параллелен горизонту");

            Assert.AreNotEqual(up, down, "«вверх» и «вниз» обязаны быть РАЗНЫМИ состояниями");
            Assert.AreNotEqual(up, level, "«вверх» и «ровно» обязаны быть РАЗНЫМИ состояниями");
            Assert.AreNotEqual(down, level, "«вниз» и «ровно» обязаны быть РАЗНЫМИ состояниями");
        }

        /// <summary>
        /// (а-1б) РАБОЧАЯ ПОЗА У ГОРИЗОНТА — ЭТО `Level`, А НЕ ЛОЖНАЯ ТРЕВОГА.
        ///
        /// ЗАЧЕМ ОТДЕЛЬНО. Именно здесь я и ошибся в первом варианте теста: наклон ~25° вниз
        /// (`dot ≈ −0.42`) я объявил «смотрит вниз». Это НЕ ошибка кода, а нормальное поведение:
        /// планшет держат под небольшим углом к столу, и это уже не «на стол», но и не потолок.
        /// Тест запирает решение, чтобы следующая правка не «ужесточила» порог и не начала ругаться
        /// на рабочую позу.
        /// </summary>
        [Test]
        public void CameraPoseState_WorkingTiltNearHorizon_IsLevelNotAlarm()
        {
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Level,
                KvArPlaneVisualizer.CameraPoseState(-0.42f),
                "наклон ~25° вниз — внутри коридора ±0.5, значит `Level`: это рабочая поза, " +
                "а не тревога «камера смотрит не туда»");
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Level,
                KvArPlaneVisualizer.CameraPoseState(0.42f),
                "наклон ~25° вверх — тоже внутри коридора: тревоги нет (порог +0.5 не достигнут)");
        }

        /// <summary>
        /// (а-2) ГРАНИЦЫ ПОРОГА 0.5 — С ОБЕИХ СТОРОН И ВКЛЮЧАЮЩЕ.
        ///
        /// ПОЧЕМУ ИМЕННО ГРАНИЦЫ. Порог — единственное место, где правило может «перевернуться»
        /// незаметно: `>` вместо `>=`, потерянный минус. На приёмке такая правка выглядит как
        /// «предупреждение стало появляться на нормальной позе» или «перестало появляться вовсе»,
        /// и оба раза это ложь в журнале.
        ///
        /// РОВНО ±0.5 — ПОКА ЕЩЁ «НОРМА»: на самой границе ложная тревога «смотрит в потолок» была
        /// бы хуже молчания, потому что гнала бы оператора опускать уже правильно наклонённый
        /// планшет.
        /// </summary>
        [Test]
        public void CameraPoseState_ThresholdBoundariesAreInclusive()
        {
            const float threshold = KvArPlaneVisualizer.CameraPoseThreshold;
            Assert.AreEqual(0.5f, threshold, 0.0001f,
                "порог позы зафиксирован задачей: dot(forward, up) > +0.5 — вверх, < −0.5 — вниз");

            // РОВНО НА ГРАНИЦЕ — ещё «норма» с обеих сторон.
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Level,
                KvArPlaneVisualizer.CameraPoseState(threshold),
                "на самой границе +0.5 тревоги нет: планшет наклонён ниже порога");
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Level,
                KvArPlaneVisualizer.CameraPoseState(-threshold),
                "на самой границе −0.5 тревоги нет");

            // ЧУТЬ ЗА ГРАНИЦЕЙ — состояние переключается.
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Up,
                KvArPlaneVisualizer.CameraPoseState(threshold + 0.001f),
                "чуть выше +0.5 — уже «смотрит вверх»");
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Down,
                KvArPlaneVisualizer.CameraPoseState(-threshold - 0.001f),
                "чуть ниже −0.5 — уже «смотрит вниз»");
        }

        /// <summary>
        /// (а-3) КРАЙНИЕ ЗНАЧЕНИЯ И МУСОР. Строго в потолок — `Up`, строго в пол — `Down`; NaN и ∞
        /// приходят из трекинга штатно при потере позы и НЕ имеют права превратиться в «смотрит в
        /// потолок» (иначе на потере трекинга оператор получил бы неверную причину).
        /// </summary>
        [Test]
        public void CameraPoseState_ExtremesAndNaNAreSafe()
        {
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Up,
                KvArPlaneVisualizer.CameraPoseState(1f), "dot = +1 — строго в потолок");
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Down,
                KvArPlaneVisualizer.CameraPoseState(-1f), "dot = −1 — строго в пол");

            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Level,
                KvArPlaneVisualizer.CameraPoseState(float.NaN),
                "NaN — это «поза неизвестна», а не «смотрит в потолок»");
            Assert.AreEqual(KvArPlaneVisualizer.KvCameraPoseState.Level,
                KvArPlaneVisualizer.CameraPoseState(float.PositiveInfinity),
                "∞ — тоже мусор, а не «смотрит вверх»");
        }

        // ================================================================ (б) СТРОКА О ПОЗЕ

        /// <summary>
        /// (б-1) УГОЛ В ГРАДУСАХ ОТ ГОРИЗОНТА. Оператор на приёмке говорит «смотрит вверх на 62°», и
        /// это угол НАД ГОРИЗОНТОМ, а не от вертикали. Проверяется и знак (вверх — плюс, вниз —
        /// минус), и модуль.
        /// </summary>
        [Test]
        public void CameraTiltDegrees_MeasuredFromHorizon()
        {
            Assert.AreEqual(90f, KvArPlaneVisualizer.CameraTiltDegrees(1f), 0.01f,
                "dot = +1 — строго в потолок, это 90° над горизонтом");
            Assert.AreEqual(0f, KvArPlaneVisualizer.CameraTiltDegrees(0f), 0.01f,
                "dot = 0 — объектив параллелен горизонту, это 0°");
            Assert.AreEqual(-90f, KvArPlaneVisualizer.CameraTiltDegrees(-1f), 0.01f,
                "dot = −1 — строго вниз, это −90°");

            // Факт приёмки: планшет лежал экраном вверх, камера смотрела в небо примерно на 62°.
            float fact = KvArPlaneVisualizer.CameraTiltDegrees(0.883f);
            Assert.AreEqual(62f, fact, 0.5f,
                "проверка на факте приёмки: dot ≈ 0.883 — это ≈ +62° над горизонтом");

            Assert.AreEqual(0f, KvArPlaneVisualizer.CameraTiltDegrees(float.NaN), 0.0001f,
                "NaN — это 0°, а не 90: угол не должен врать при потере позы");
        }

        /// <summary>
        /// (б-2) СТРОКА «СМОТРИТ ВВЕРХ»: содержит префикс приёмки, УГОЛ с градусами и ПОДСКАЗКУ-
        /// ДЕЙСТВИЕ. Без подсказки строка сообщает о проблеме, но не говорит, что делать, — а
        /// именно действие и нужно оператору.
        /// </summary>
        [Test]
        public void DescribeCameraPose_Up_HasAngleAndAction()
        {
            string line = KvArPlaneVisualizer.DescribeCameraPose(0.883f);

            AssertHasNumber(line, "строка «смотрит вверх»");
            StringAssert.StartsWith(KvArPlaneVisualizer.PoseLogPrefix, line,
                "строку о позе ищет приёмка по префиксу: " + line);
            StringAssert.Contains("ВВЕРХ", line, "направление обязано быть названо словом: " + line);
            StringAssert.Contains("62°", line,
                "угол в градусах — главное число строки (факт приёмки: 62° над горизонтом): " + line);
            StringAssert.Contains("потолок", line,
                "журнал обязан назвать, что видно в кадре: " + line);
            StringAssert.Contains("стол", line,
                "журнал обязан сказать, что стола в кадре нет: " + line);
            StringAssert.Contains(KvArPlaneVisualizer.PoseUpHint, line,
                "без подсказки-действия строка не помогает оператору: " + line);
            StringAssert.Contains("опустите планшет", line,
                "действие должно быть конкретным: «опустите планшет»: " + line);
        }

        /// <summary>
        /// (б-3) СТРОКА «НОРМА»: угол есть, ПОДСКАЗКИ-ДЕЙСТВИЯ НЕТ, а НАПРАВЛЕНИЕ НАЗВАНО.
        ///
        /// ЧТО БЫЛО НЕ ТАК (исправлено 12.11.2026). Тест брал `dot = −0.42`, ждал в строке слово
        /// «ВНИЗ» и падал: `−0.42` — это `Level`, а первая версия строки печатала для `Level`
        /// «почти горизонтально, 25° — норма», БЕЗ слова направления и с МОДУЛЕМ угла. Прогон в
        /// Unity поймал ровно это: два разных состояния читались одинаково.
        ///
        /// ЧТО СТАЛО. Тест проверяет ОБА нормальных состояния, каждое по своему слову:
        ///   • `Down` (`−0.87`, ~60° вниз) — слово «ВНИЗ» и угол;
        ///   • `Level` (`−0.42` и `0`) — слово «РОВНО» и ЗНАКОВЫЙ угол (минус = наклон вниз).
        /// Ни одна из строк не содержит подсказки-действия «опустите планшет», и ни одна не
        /// упоминает потолок.
        ///
        /// АНТИ-ВАКУУМ. Если бы «опустите планшет» печаталось и при нормальной позе, оператор
        /// перестал бы читать строку: совет не отличался бы от совета в момент поломки.
        /// </summary>
        [Test]
        public void DescribeCameraPose_Normal_HasNoActionHint()
        {
            string down = KvArPlaneVisualizer.DescribeCameraPose(-0.87f);
            string level = KvArPlaneVisualizer.DescribeCameraPose(-0.42f);
            string levelZero = KvArPlaneVisualizer.DescribeCameraPose(0f);

            AssertHasNumber(down, "строка «смотрит вниз»");
            AssertHasNumber(level, "строка «ровно»");
            AssertHasNumber(levelZero, "строка «ровно» на горизонте");

            StringAssert.StartsWith(KvArPlaneVisualizer.PoseLogPrefix, down,
                "префикс един для всех строк о позе: " + down);
            StringAssert.StartsWith(KvArPlaneVisualizer.PoseLogPrefix, level,
                "префикс един для всех строк о позе: " + level);

            // DOWN — слово направления и градусы.
            StringAssert.Contains("ВНИЗ", down,
                "направление названо словом — иначе «норма» не отличима от «вверх»: " + down);
            StringAssert.Contains("норма", down, "рабочая поза обязана быть названа нормой: " + down);
            StringAssert.Contains("60°", down, "угол (|asin(−0.87)| ≈ 60°) обязан быть числом: " + down);

            // LEVEL — СВОЁ слово направления, не совпадающее с Down и Up.
            StringAssert.Contains("РОВНО", level,
                "у `Level` обязано быть СВОЁ слово направления: " + level);
            StringAssert.Contains("25°", level,
                "угол наклона обязан быть числом: " + level);
            StringAssert.Contains("-25°", level,
                "угол ЗНАКОВЫЙ: минус и отличает «чуть вниз» от «чуть вверх» на горизонте: " + level);
            StringAssert.Contains("норма", level, "поза у горизонта — тоже норма: " + level);
            StringAssert.Contains("норма", levelZero, "поза на горизонте — норма: " + levelZero);

            Assert.IsFalse(down.Contains(KvArPlaneVisualizer.PoseUpHint),
                "при нормальной позе НЕТ подсказки-действия: иначе совет перестанут читать: " + down);
            Assert.IsFalse(level.Contains(KvArPlaneVisualizer.PoseUpHint),
                "при нормальной позе НЕТ подсказки-действия: " + level);
            Assert.IsFalse(levelZero.Contains(KvArPlaneVisualizer.PoseUpHint),
                "при нормальной позе НЕТ подсказки-действия: " + levelZero);
            Assert.IsFalse(down.Contains("опустите планшет"),
                "опускать нечего — камера уже смотрит на стол: " + down);
            Assert.IsFalse(level.Contains("потолок"),
                "при нормальной позе «потолок» в строке — ложь: " + level);
            Assert.IsFalse(levelZero.Contains("потолок"),
                "при нормальной позе «потолок» в строке — ложь: " + levelZero);
        }

        /// <summary>
        /// (б-4) ТРИ СОСТОЯНИЯ РАЗЛИЧИМЫ ПО ТЕКСТУ, А НЕ ТОЛЬКО ПО ЧИСЛУ.
        ///
        /// ЭТО ТО ПРАВИЛО, которое проверяется прогоном: у каждого состояния своё СЛОВО
        /// направления (`ВВЕРХ` / `ВНИЗ` / `РОВНО`) и свои градусы, и ни одно слово не встречается
        /// в строке другого состояния. Без этого теста «25° — норма» снова могло бы означать и
        /// «вверх», и «вниз» — ровно та ошибка, которую поймал первый прогон.
        /// </summary>
        [Test]
        public void DescribeCameraPose_StatesDifferByWord()
        {
            string up = KvArPlaneVisualizer.DescribeCameraPose(0.883f);
            string down = KvArPlaneVisualizer.DescribeCameraPose(-0.87f);
            string level = KvArPlaneVisualizer.DescribeCameraPose(-0.42f);

            // Каждое состояние называет СВОЁ направление словом.
            StringAssert.Contains("ВВЕРХ", up, "Up обязан говорить «ВВЕРХ»: " + up);
            StringAssert.Contains("ВНИЗ", down, "Down обязан говорить «ВНИЗ»: " + down);
            StringAssert.Contains("РОВНО", level, "Level обязан говорить «РОВНО»: " + level);

            // И НЕ называет чужое: иначе три состояния снова путаются.
            Assert.IsFalse(up.Contains("РОВНО"), "в строке Up не должно быть слова «РОВНО»: " + up);
            Assert.IsFalse(up.Contains("ВНИЗ"), "в строке Up не должно быть слова «ВНИЗ»: " + up);
            Assert.IsFalse(down.Contains("ВВЕРХ"), "в строке Down не должно быть «ВВЕРХ»: " + down);
            Assert.IsFalse(down.Contains("РОВНО"), "в строке Down не должно быть «РОВНО»: " + down);
            Assert.IsFalse(level.Contains("ВВЕРХ"), "в строке Level не должно быть «ВВЕРХ»: " + level);
            Assert.IsFalse(level.Contains("ВНИЗ"), "в строке Level не должно быть «ВНИЗ»: " + level);

            // Все три строки РАЗНЫЕ — попарно, а не «две одинаковые и одна пустая».
            Assert.AreNotEqual(up, down, "строки «вверх» и «вниз» обязаны различаться");
            Assert.AreNotEqual(up, level, "строки «вверх» и «ровно» обязаны различаться");
            Assert.AreNotEqual(down, level, "строки «вниз» и «ровно» обязаны различаться");

            // И у каждой есть число (анти-вакуум).
            AssertHasNumber(up, "строка Up");
            AssertHasNumber(down, "строка Down");
            AssertHasNumber(level, "строка Level");
        }

        // ================================================================ (в) СВЯЗКА ДИАГНОСТИК

        /// <summary>
        /// (в-1) «ПЛОСКОСТЕЙ 0» ПРИ ВЗГЛЯДЕ ВВЕРХ — СО ССЫЛКОЙ НА ПОЗУ.
        ///
        /// ЭТО ГЛАВНАЯ ПРОВЕРКА ЗАДАЧИ: две диагностики обязаны читаться как ОДНА. Иначе на
        /// приёмке снова получится ложный сигнал «ARCore сломался», хотя планшет просто лежит
        /// экраном вверх (факт 12.11.2026).
        /// </summary>
        [Test]
        public void NoPlanes_WhenCameraLooksUp_BlamesCameraPose()
        {
            string line = KvArPlaneVisualizer.DescribeSurfaceState(
                0, 0, ARSessionState.SessionTracking, 4f, false, false,
                null, null, 0.883f);

            AssertHasNumber(line, "строка «плоскостей 0» при взгляде вверх");
            StringAssert.Contains("плоскостей 0", line,
                "состояние «плоскостей 0» обязано остаться названным: " + line);
            StringAssert.Contains("ПОЗА", line,
                "при взгляде вверх строка о плоскостях ОБЯЗАНА сослаться на позу: " + line);
            StringAssert.Contains("смотрит вверх", line,
                "ссылка обязана называть направление, а не только слово «поза»: " + line);
            StringAssert.Contains("вероятная причина", line,
                "приёмке нужно, чтобы причина была названа причиной: " + line);
        }

        /// <summary>
        /// (в-2) АНТИ-ВАКУУМ: ПРИ НОРМАЛЬНОЙ ПОЗЕ ССЫЛКИ НА ПОЗУ НЕТ.
        ///
        /// Без этой половины первая проверка была бы вакуумной: строка с «ПОЗА: смотрит вверх»
        /// проходила бы и тогда, когда ссылка висит ВСЕГДА — то есть когда она ничего не значит.
        /// Проверяются оба нормальных случая (вниз и ровно) и «поза неизвестна» (`null`).
        /// </summary>
        [Test]
        public void NoPlanes_WhenCameraPoseIsNormal_HasNoPoseBlame()
        {
            string down = KvArPlaneVisualizer.DescribeSurfaceState(
                0, 0, ARSessionState.SessionTracking, 4f, false, false,
                null, null, -0.42f);
            string level = KvArPlaneVisualizer.DescribeSurfaceState(
                0, 0, ARSessionState.SessionTracking, 4f, false, false,
                null, null, 0f);
            string unknown = KvArPlaneVisualizer.DescribeSurfaceState(
                0, 0, ARSessionState.SessionTracking, 4f, false, false);

            AssertHasNumber(down, "«плоскостей 0» при нормальной позе (вниз)");
            AssertHasNumber(level, "«плоскостей 0» при нормальной позе (ровно)");
            AssertHasNumber(unknown, "«плоскостей 0» без данных о позе");

            Assert.IsFalse(down.Contains("ПОЗА"),
                "при нормальной позе винить позу нельзя — иначе «причина» находится всегда: " + down);
            Assert.IsFalse(level.Contains("ПОЗА"),
                "при нормальной позе винить позу нельзя: " + level);
            Assert.IsFalse(unknown.Contains("ПОЗА"),
                "поза неизвестна — значит и ссылаться не на что (строка не врёт): " + unknown);

            Assert.IsEmpty(KvArPlaneVisualizer.PoseCulpritSuffix(-0.42f),
                "суффикс-обвинение обязан быть ПУСТЫМ при взгляде вниз");
            Assert.IsEmpty(KvArPlaneVisualizer.PoseCulpritSuffix(0f),
                "суффикс-обвинение обязан быть ПУСТЫМ при взгляде на горизонт");
            StringAssert.Contains("вероятная причина",
                KvArPlaneVisualizer.PoseCulpritSuffix(0.883f),
                "а при взгляде вверх суффикс обязан обвинять позу — иначе он бесполезен");
        }

        /// <summary>
        /// (в-3) ССЫЛКА НЕ ПЕРЕХВАТЫВАЕТ РАБОЧИЕ СОСТОЯНИЯ. Когда стол найден или найден пол,
        /// «причина» не ищется вовсе — иначе оператор получил бы «опустите планшет» при зелёном
        /// столе. Это то же правило приоритета, что у `SurfaceLogStateOf`.
        /// </summary>
        [Test]
        public void PoseBlame_DoesNotHijackWorkingStates()
        {
            string table = KvArPlaneVisualizer.DescribeSurfaceState(
                3, 3, ARSessionState.SessionTracking, 0f, true, false,
                "стол 1.20×0.70 м", null, 0.883f);
            string floorOnly = KvArPlaneVisualizer.DescribeSurfaceState(
                2, 2, ARSessionState.SessionTracking, 0f, false, true,
                "пол 6.00 м²", null, 0.883f);

            Assert.IsFalse(table.Contains("ПОЗА"),
                "стол найден — поза не может быть «вероятной причиной»: " + table);
            Assert.IsFalse(floorOnly.Contains("ПОЗА"),
                "пол найден — распознавание работает, винить позу нечего: " + floorOnly);
            StringAssert.Contains("стол", table, "рабочее состояние обязано остаться названным: " + table);
        }

        // ================================================================ (г) КОЛЬЦО ИМЁН КАДРОВ

        /// <summary>
        /// (г-1) ИМЯ ПО НОМЕРУ: 1 → `ar_frame_001.png`, 10 → `ar_frame_010.png`.
        ///
        /// ЧТО БЫЛО. Имя было ОДНО — `ar_camera_frame.png`, и файл писался один раз за сеанс. На
        /// приёмке в папке лежал один снимок 144 КБ с одним таймштампом: доказать, что камера
        /// видела ВО ВРЕМЯ РАБОТЫ, было нечем.
        /// </summary>
        [Test]
        public void FrameFileName_PadsWithThreeDigits()
        {
            Assert.AreEqual("ar_frame_001.png", KvArCameraGate.FrameFileName(1, 10),
                "первый кадр серии — ровно ar_frame_001.png");
            Assert.AreEqual("ar_frame_010.png", KvArCameraGate.FrameFileName(10, 10),
                "десятый кадр серии — ровно ar_frame_010.png");
            Assert.AreEqual("ar_frame_003.png", KvArCameraGate.FrameFileName(3, 10),
                "номер в середине серии не должен «съезжать»");
            Assert.AreEqual("ar_frame_001.png", KvArCameraGate.FrameFileName(1, 1),
                "при max = 1 существует ровно одно имя, и оно не пустое");
        }

        /// <summary>
        /// (г-2) КОЛЬЦО: ПОСЛЕ `max` — СНОВА С 1, ПРОВЕРКА ОБЕИХ СТОРОН.
        ///
        /// ПОЧЕМУ ОБЕ СТОРОНЫ. Номер, растущий без предела, копил бы файлы на устройстве до
        /// заполнения памяти; номер, замирающий на `max`, перестал бы показывать СВЕЖИЕ кадры.
        /// Поэтому проверяется и «на границе», и «сразу после», и «далеко после»: `max + 1` обязан
        /// дать ПЕРВОЕ имя, и старый файл с ним перезаписывается.
        /// </summary>
        [Test]
        public void FrameFileName_WrapsBackToFirstAfterMax()
        {
            const int max = 10;

            Assert.AreEqual("ar_frame_010.png", KvArCameraGate.FrameFileName(max, max),
                "последний кадр перед заворотом — №max (левая сторона проверки)");
            Assert.AreEqual("ar_frame_001.png", KvArCameraGate.FrameFileName(max + 1, max),
                "сразу после max счёт начинается СНАЧАЛА — №1 (правая сторона проверки)");
            Assert.AreEqual("ar_frame_002.png", KvArCameraGate.FrameFileName(max + 2, max),
                "и дальше по порядку — №2");
            Assert.AreEqual("ar_frame_001.png", KvArCameraGate.FrameFileName(2 * max + 1, max),
                "второй оборот кольца тоже возвращает №1");

            // ИМЯ ВСЕГДА ВНУТРИ ДИАПАЗОНА: 20 прогонов подряд не должны дать ни №0, ни №11.
            for (int index = 1; index <= 2 * max; index++)
            {
                string name = KvArCameraGate.FrameFileName(index, max);
                Match number = Regex.Match(name, @"^ar_frame_(\d{3})\.png$");
                Assert.IsTrue(number.Success,
                    "имя кадра обязано быть ровно ar_frame_NNN.png, получено «" + name + "»");
                int parsed = int.Parse(number.Groups[1].Value);
                Assert.GreaterOrEqual(parsed, 1, "номер кадра не может быть нулём: " + name);
                Assert.LessOrEqual(parsed, max,
                    "номер кадра не может быть больше max (" + max + "): " + name);
            }
        }

        /// <summary>
        /// (г-3) СТРОКА О СОХРАНЁННОМ КАДРЕ — С ЧИСЛАМИ (номер, размер, яркость, путь).
        ///
        /// ЧТО ТРЕБУЕТ ПРИЁМКА. Не «файл есть», а «№3, 320×240, яркость 0.412, путь…». По номеру
        /// видно, что снимки идут ПЕРИОДИЧЕСКИ; по яркости — что кадр не пустой (чёрный кадр уже
        /// один раз выдавался за «камеру работает»); по пути — что именно забирать через adb.
        /// </summary>
        [Test]
        public void DescribeFrameSaved_HasIndexSizeLuminanceAndPath()
        {
            string line = KvArCameraGate.DescribeFrameSaved(
                3, 320, 240, 0.412f, "/data/user/0/app/files/ar_frame_003.png");

            AssertHasNumber(line, "строка о сохранённом кадре");
            StringAssert.Contains("[KvAr] КАДР:", line, "строку ищет приёмка по этому префиксу: " + line);
            StringAssert.Contains("№3", line, "номер кадра обязан быть в строке: " + line);
            StringAssert.Contains("320×240", line, "размер кадра обязан быть в строке: " + line);
            StringAssert.Contains("0.412", line, "яркость обязана быть числом с точками: " + line);
            StringAssert.Contains("ar_frame_003.png", line,
                "путь обязан указывать на конкретный файл: " + line);
        }

        /// <summary>
        /// (г-4) ПРАВИЛО «ПИСАТЬ ЛИ КАДР СЕЙЧАС»: не чаще периода и НЕ ПОВЕРХ незаконченного
        /// снимка.
        ///
        /// ЧТО БЫЛО. Правила не было: единственный снимок запускался один раз и ничего не
        /// охраняло. При периодической записи две накладки дали бы битый PNG (файл открыт на
        /// запись дважды), а запись каждый кадр — просадку на планшете.
        /// </summary>
        [Test]
        public void ShouldCaptureFrame_RespectsPeriodAndRunningFlag()
        {
            Assert.IsFalse(KvArCameraGate.ShouldCaptureFrame(10f, 15f, false),
                "до назначенного времени снимок не пишется (10 < 15) — иначе это не «раз в период»");
            Assert.IsTrue(KvArCameraGate.ShouldCaptureFrame(15f, 15f, false),
                "в назначенное время — пишем");
            Assert.IsTrue(KvArCameraGate.ShouldCaptureFrame(99f, 15f, false),
                "после назначенного времени — пишем (иначе серия кадров встанет)");

            Assert.IsFalse(KvArCameraGate.ShouldCaptureFrame(99f, 15f, true),
                "пока предыдущий снимок не сохранён, новый НЕ начинаем (защита от накладок)");
        }
    }
}
