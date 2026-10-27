using NUnit.Framework;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты НАЖАТИЯ ВЗГЛЯДОМ ПО ЗАДЕРЖКЕ (сессия §33, дефект с живого шлема 15.10.2026).
    ///
    /// ЗАЧЕМ. Владелец в шлеме: «не могу навестись на оверлей». Причина: указатель проекта брал
    /// луч у правого контроллера, а контроллеры приложению не отдаются вовсе — луча не было,
    /// нажимать было нечем. Пока контроллеры не заведены, интерфейс должен управляться ВЗГЛЯДОМ:
    /// смотришь на кнопку — через время она нажимается.
    ///
    /// ЧТО ЗДЕСЬ ПРОВЕРЯЕТСЯ (всё — числа, без шлема и без сцены):
    ///   • время накопления идёт вверх и не перескакивает порог;
    ///   • «пора нажимать» наступает РОВНО на пороге, ни раньше, ни позже;
    ///   • сломанное время кадра (ноль и меньше) не отматывает счётчик назад;
    ///   • порог защищён от нуля — иначе кнопки нажимались бы мгновенно, «сами».
    ///
    /// Тесты написаны ДО подключения к сцене и живут отдельно от неё: это чистая арифметика
    /// решения, и она обязана быть верной независимо от того, что видно в шлеме.
    /// </summary>
    public class KvGazeDwellTests
    {
        /// <summary>Время накапливается шагами и упирается в порог, а не растёт бесконечно.</summary>
        [Test]
        public void Advance_AccumulatesAndStopsAtThreshold()
        {
            float progress = 0f;
            for (int i = 0; i < 100; i++)
                progress = KvGazeDwell.Advance(progress, 0.02f, 1.0f);

            Assert.AreEqual(1.0f, progress, 0.0001f,
                "накопленное время обязано упираться в порог: бесконечный рост не нужен и вреден");
        }

        /// <summary>Нажатие наступает РОВНО на пороге.</summary>
        [Test]
        public void IsReady_BecomesTrueExactlyAtThreshold()
        {
            Assert.IsFalse(KvGazeDwell.IsReady(0.99f, 1.0f), "за 0,01 с до порога нажимать рано");
            Assert.IsTrue(KvGazeDwell.IsReady(1.0f, 1.0f), "на пороге — пора");
            Assert.IsTrue(KvGazeDwell.IsReady(1.2f, 1.0f), "после порога — тем более пора");
        }

        /// <summary>Накопление с шагом, меньшим порога, доходит до готовности за ожидаемое число шагов.</summary>
        [Test]
        public void Advance_ReachesReadyInExpectedTime()
        {
            float progress = 0f;
            float threshold = 1.3f;
            float step = 1f / 90f;                   // кадр шлема при 90 Гц
            int frames = 0;
            while (!KvGazeDwell.IsReady(progress, threshold) && frames < 1000)
            {
                progress = KvGazeDwell.Advance(progress, step, threshold);
                frames++;
            }

            Assert.Less(frames, 1000, "взгляд обязан довести до нажатия");
            float seconds = frames * step;
            Assert.AreEqual(threshold, seconds, 0.05f,
                "на 90 кадрах в секунду нажатие обязано случиться примерно через порог секунд");
        }

        /// <summary>
        /// СЛОМАННОЕ ВРЕМЯ КАДРА НЕ ОТМАТЫВАЕТ СЧЁТЧИК. Отрицательная или нулевая дельта
        /// приходит при паузе редактора и при первом кадре — прогресс от неё только не растёт.
        /// </summary>
        [Test]
        public void Advance_IgnoresNonPositiveDeltaTime()
        {
            float progress = KvGazeDwell.Advance(0.5f, -1f, 1.0f);
            Assert.AreEqual(0.5f, progress, 0.0001f, "отрицательный шаг не должен уменьшать прогресс");

            progress = KvGazeDwell.Advance(0.5f, 0f, 1.0f);
            Assert.AreEqual(0.5f, progress, 0.0001f, "нулевой шаг ничего не меняет");
        }

        /// <summary>
        /// НУЛЕВОЙ ПОРОГ — ЭТО «НАЖИМАТЬ ВСЁ МГНОВЕННО», то есть дефект. Порог обязан
        /// подниматься до минимального осмысленного значения.
        /// </summary>
        [Test]
        public void Limit_ProtectsFromZeroAndNegativeThreshold()
        {
            Assert.AreEqual(KvGazeDwell.MinSeconds, KvGazeDwell.Limit(0f), 0.0001f, "нулевой порог");
            Assert.AreEqual(KvGazeDwell.MinSeconds, KvGazeDwell.Limit(-5f), 0.0001f, "отрицательный порог");
            Assert.AreEqual(1.3f, KvGazeDwell.Limit(1.3f), 0.0001f, "нормальный порог не портится");

            // И следствие: при нулевом пороге нажатие не происходит мгновенно, а требует MinSeconds.
            Assert.IsFalse(KvGazeDwell.IsReady(0.05f, 0f),
                "при «нулевом» пороге нажатие не должно случаться на первом же кадре");
            Assert.IsTrue(KvGazeDwell.IsReady(KvGazeDwell.MinSeconds, 0f),
                "но через минимальный порог — обязано");
        }

        /// <summary>Доля прогресса 0…1 — по ней подсвечивается луч, «сколько ещё смотреть».</summary>
        [Test]
        public void Progress01_IsAClampedFraction()
        {
            Assert.AreEqual(0f, KvGazeDwell.Progress01(0f, 1f), 0.0001f, "в начале — ноль");
            Assert.AreEqual(0.5f, KvGazeDwell.Progress01(0.5f, 1f), 0.0001f, "половина — половина");
            Assert.AreEqual(1f, KvGazeDwell.Progress01(2f, 1f), 0.0001f, "после порога — единица, не больше");
        }

        /// <summary>Порог по умолчанию осмысленный: не мгновенный и не мучительный.</summary>
        [Test]
        public void DefaultSeconds_IsReasonable()
        {
            Assert.GreaterOrEqual(KvGazeDwell.DefaultSeconds, 0.8f,
                "меньше 0,8 с — кнопки будут нажиматься от случайного взгляда");
            Assert.LessOrEqual(KvGazeDwell.DefaultSeconds, 2.5f,
                "больше 2,5 с — нажатие станет мучением");
        }
    }
}
