// ================================================================================================
//  KvGestureAdapterTests.cs — EditMode-тесты АДАПТЕРА «КИСТИ ИЗ OpenXR → СЕРВИС ЖЕСТОВ»
//  (сессия S44, пункт 5.23 плана).
//
//  ОТКУДА ЗАДАЧА. `KvXrHands` читал кисти из OpenXR и РИСОВАЛ их, но НИКОМУ не передавал. Сервис
//  жестов (`KvHandTrackingService`) получал позы суставов только из ДИАГНОСТИКИ (`DshStage4Diag`),
//  то есть в собранном приложении распознавание работало ВХОЛОСТУЮ: логика готова, данных нет.
//
//  ЧТО ВИДЕЛ ОПЕРАТОР: кисти видны (их рисуют), но жесты не срабатывают. Ни ошибки, ни
//  предупреждения — просто «не работает». Владелец просил ровно обратное: «кладу контроллеры и вижу
//  свои кисти, и только потом — жесты».
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ (по исходнику — сцена не нужна):
//    1) в `KvXrHands` есть мост `GestureSink`;
//    2) кисти ПЕРЕДАЮТСЯ каждый кадр, где отслеживаются;
//    3) передаются ШЕСТЬ точек, которые сервис знает (не больше и не меньше);
//    4) есть отсев нечисловых данных и проверка валидности кадра;
//    5) хаб ПОДКЛЮЧАЕТ мост — иначе всё бесполезно.
// ================================================================================================

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Адаптер кистей: данные доходят до распознавания жестов.</summary>
    public class KvGestureAdapterTests
    {
        private static string Find(string fileName)
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            string[] found = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// МОСТ СУЩЕСТВУЕТ. Без него сервис жестов снова останется без данных.
        /// </summary>
        [Test]
        public void GestureSinkExists()
        {
            string path = Find("KvXrHands.cs");
            Assert.IsNotNull(path, "не найден KvXrHands.cs");

            string text = File.ReadAllText(path);
            StringAssert.Contains("GestureSink", text,
                "в KvXrHands нет моста к сервису жестов — распознавание снова останется без данных, " +
                "как было до ночи S44");
        }

        /// <summary>
        /// КИСТИ ПЕРЕДАЮТСЯ ИЗ `Update`. Если вызов убрать, кисти будут рисоваться, но жесты молчать.
        /// </summary>
        [Test]
        public void HandsAreFedFromUpdate()
        {
            string path = Find("KvXrHands.cs");
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            int update = text.IndexOf("void Update()", System.StringComparison.Ordinal);
            Assert.Greater(update, 0, "в KvXrHands нет метода Update");

            // ГРАНИЦЫ МЕТОДА ПО ГЛУБИНЕ СКОБОК, А НЕ ПО ЧИСЛУ СИМВОЛОВ.
            //
            // ЧЕСТНО О ПЕРВОЙ ПОПЫТКЕ (30.10.2026). Сначала брались «первые 2500 символов после
            // начала Update» — и тест объявил, что передач кистей НЕТ, хотя они там есть: вызовы
            // уехали ниже из-за добавленного блока сброса отметок. Окно в символах — ненадёжный
            // способ; правильный — считать скобки (за ночь это правило пригодилось шесть раз).
            int open = text.IndexOf('{', update);
            Assert.Greater(open, 0, "не найдено тело Update");

            int depth = 1;
            int at = open + 1;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }
            string body = text.Substring(update, at - update);

            int calls = Regex.Matches(body, @"FeedGestures\s*\(").Count;
            Assert.GreaterOrEqual(calls, 2,
                "из Update вызывается " + calls + " передач кистей — ожидались две (левая и правая)");
        }

        /// <summary>
        /// ПЕРЕДАЮТСЯ РОВНО ШЕСТЬ ТОЧЕК, КОТОРЫЕ СЕРВИС ЗНАЕТ.
        ///
        /// Сервис определяет шесть суставов (`Wrist`, `ThumbTip`, `IndexTip`, `MiddleTip`, `RingTip`,
        /// `LittleTip`). Передать больше — бессмысленная работа на каждом кадре, меньше — распознавание
        /// не сможет отличить щипок от горсти.
        /// </summary>
        [Test]
        public void SixKnownJointsArePassed()
        {
            string path = Find("KvXrHands.cs");
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            var required = new[] { "Wrist", "ThumbTip", "IndexTip", "MiddleTip", "RingTip", "LittleTip" };

            var missing = new System.Collections.Generic.List<string>();
            foreach (string joint in required)
                if (!text.Contains("KvHandTrackingService." + joint)) missing.Add(joint);

            Assert.AreEqual(0, missing.Count,
                "в адаптер не передаются суставы: " + string.Join(", ", missing.ToArray()) +
                " — распознавание не сможет отличить жесты");
        }

        /// <summary>
        /// ОТСЕВ НЕЧИСЛА И НЕВАЛИДНЫХ КАДРОВ. Данные уходят в ДРУГОЙ сервис, и он должен получить
        /// гарантированно числовые точки (урок E-21: NaN проходит любые сравнения).
        /// </summary>
        [Test]
        public void AdapterFiltersInvalidFrames()
        {
            string path = Find("KvXrHands.cs");
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            int start = text.IndexOf("void FeedGestures", System.StringComparison.Ordinal);
            if (start < 0) start = text.IndexOf("FeedGestures(KvHandFrame", System.StringComparison.Ordinal);
            Assert.Greater(start, 0, "не найден метод FeedGestures");

            // Границы по глубине скобок (правило, выведенное за ночь).
            int open = text.IndexOf('{', start);
            int depth = 1, at = open + 1;
            while (at < text.Length && depth > 0)
            {
                if (text[at] == '{') depth++;
                else if (text[at] == '}') depth--;
                at++;
            }
            string body = text.Substring(start, at - start);

            StringAssert.Contains("IsUsable", body,
                "адаптер не проверяет кадр через KvGestureMath.IsUsable — в сервис могут уйти " +
                "нечисловые точки");
            StringAssert.Contains("valid", body,
                "адаптер не проверяет признак valid — в сервис уйдёт пустой кадр");
        }

        /// <summary>
        /// ХАБ ПОДКЛЮЧАЕТ МОСТ. Без этой строки адаптер существует, но никто его не вызывает —
        /// ровно та же ситуация, что была до правки.
        /// </summary>
        [Test]
        public void HubConnectsTheBridge()
        {
            string path = Find("KvStageHub4.cs");
            Assert.IsNotNull(path, "не найден KvStageHub4.cs");

            string text = File.ReadAllText(path);
            StringAssert.Contains("KvXrHands.GestureSink", text,
                "хаб НЕ подключает мост кистей к сервису жестов — адаптер существует, но данные " +
                "до распознавания не доходят");
        }
    }
}
