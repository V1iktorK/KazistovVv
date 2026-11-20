// ================================================================================================
//  KvHandMenuLayerTests.cs — ОДИН ЯРУС МЕНЮ НА ЛЕВОЙ РУКЕ (20.11.2026).
//
//  ОТКУДА ЗАДАЧА. Слово владельца дословно: «два слоя менюшек на левой руке — оставить тот, что
//  с меньшими кружочками».
//
//  ЧТО БЫЛО. На левой руке висели ДВЕ панели сразу:
//    • `KvXrGroupPanel` — иконки групп и веер команд, кружочки `IconSizeMeters` = 4,2 см;
//    • `KvXrQuickPanel` — восемь частых команд, кружочки 7,5 см (кнопка 76 px при масштабе
//      канваса `radius * 2.6 / 420`, радиус по умолчанию 0,16 м).
//  Включались и гасились они ОДНОЙ настройкой (`KeyQuickPanelVisible`), поэтому развести ярусы
//  было нечем: нажатие Y прятало и показывало оба слоя сразу.
//
//  ЧТО КРЕПИТСЯ ЗДЕСЬ:
//    1) по умолчанию у руки ОДИН ярус — тот, у кружочки МЕНЬШЕ (панель групп);
//    2) выбор «меньших кружочков» — это число, а не глаза: диаметр крупного яруса измеряется
//       той же формулой, которой панель рисуется (`CanvasScale` → `ButtonDiameterMeters`);
//    3) общий тумблер (Y) по-прежнему гасит всё — правило не потеряно;
//    4) второй ярус возвращается БЕЗ пересборки: строка `menu.quickLayer=1` в файле подстройки.
// ================================================================================================

using System.Collections.Generic;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Сколько ярусов меню висит на левой руке и какой из них оставлен.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvHandMenuLayerTests
    {
        // ==================================================== 1. ПО УМОЛЧАНИЮ ОДИН ЯРУС

        /// <summary>
        /// ПО УМОЛЧАНИЮ У РУКИ РОВНО ОДИН ЯРУС — С МЕНЬШИМИ КРУЖОЧКАМИ.
        ///
        /// Что ловит: возврат общего ключа видимости на оба яруса (тогда на левую руку снова
        /// лягут две менюшки) и включение второго яруса по умолчанию.
        /// </summary>
        [Test]
        public void ByDefault_OnlyOneLayerHangsOnTheHand()
        {
            // Общий тумблер панели у руки (Y) по умолчанию ВКЛЮЧЕН — иначе не будет и одного.
            Assert.AreEqual(1, KvXrSettings.DefaultQuickPanelVisible,
                "по умолчанию панель у руки должна быть включена — с нуля оператор не увидит ничего");

            // Второй ярус (крупные кнопки) по умолчанию СКРЫТ — это и есть решение владельца.
            Assert.AreEqual(0, KvXrSettings.DefaultQuickLayerVisible,
                "второй ярус обязан быть скрыт по умолчанию: два слоя менюшек на одной руке — дефект");

            bool hadMaster = PlayerPrefs.HasKey(KvXrSettings.KeyQuickPanelVisible);
            int masterValue = PlayerPrefs.GetInt(KvXrSettings.KeyQuickPanelVisible, 1);
            bool hadLayer = PlayerPrefs.HasKey(KvXrSettings.KeyQuickLayerVisible);
            int layerValue = PlayerPrefs.GetInt(KvXrSettings.KeyQuickLayerVisible, 0);
            try
            {
                KvXrSettings.SetQuickPanelVisible(true);
                KvXrSettings.SetQuickLayerVisible(false);

                Assert.IsFalse(KvXrSettings.GetQuickLayerVisible(),
                    "хранилище подтверждает: второго яруса по умолчанию нет");

                // Ни одно сочетание «по умолчанию» не даёт второй ярус.
                Assert.IsFalse(KvXrQuickPanel.LayerVisible(KvXrSettings.GetQuickLayerVisible(),
                    KvXrSettings.GetQuickPanelVisible(), true),
                    "при настройках по умолчанию второй ярус показываться не обязан");

                // Включённый вручную ярус, наоборот, показывается — правило не «спрятать навсегда».
                Assert.IsTrue(KvXrQuickPanel.LayerVisible(true, true, true));
            }
            finally
            {
                if (hadMaster) PlayerPrefs.SetInt(KvXrSettings.KeyQuickPanelVisible, masterValue);
                else PlayerPrefs.DeleteKey(KvXrSettings.KeyQuickPanelVisible);
                if (hadLayer) PlayerPrefs.SetInt(KvXrSettings.KeyQuickLayerVisible, layerValue);
                else PlayerPrefs.DeleteKey(KvXrSettings.KeyQuickLayerVisible);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// ОСТАВЛЯЕМЫЙ ЯРУС ДЕЙСТВИТЕЛЬНО ТОТ, У КОГО КРУЖОЧКИ МЕНЬШЕ.
        ///
        /// Что ловит: смену выбора «на глаз» — если завтра размеры панелей поменяются так, что
        /// крупный ярус станет меньше иконок групп, выбор «меньших кружочков» станет ложью,
        /// а тест это поймает числом.
        /// </summary>
        [Test]
        public void KeptLayer_IsTheOneWithSmallerCircles()
        {
            float quickCircle = KvXrQuickPanel.ButtonDiameterMeters(
                KvXrSettings.DefaultQuickPanelRadius,
                KvXrQuickPanel.DefaultCanvasSize,
                KvXrQuickPanel.DefaultButtonSize);

            float groupCircle = KvXrGroupPanelLayout.IconSizeMeters;

            Assert.Greater(groupCircle, 0f, "иконки групп обязаны иметь размер");
            Assert.Greater(quickCircle, 0f, "кнопки панели быстрых команд обязаны иметь размер");

            Assert.Greater(quickCircle, groupCircle,
                "панель быстрых команд крупнее иконок групп — значит «оставить с меньшими " +
                "кружочками» означает оставить ПАНЕЛЬ ГРУПП. Если это не так, выбор яруса надо " +
                "пересмотреть, а не переписывать тест");

            // Числа — те, что стоят в коде: 7,5 см у крупного яруса и 4,2 см у оставленного.
            Assert.AreEqual(0.0752f, quickCircle, 0.0005f,
                "диаметр кнопки второго яруса перестал быть ~7,5 см — пересчитайте выбор яруса");
            Assert.AreEqual(0.042f, groupCircle, 0.0001f,
                "иконка оставленного яруса перестала быть 4,2 см");

            // Оставленный ярус обязан читаться с расстояния вытянутой руки, но не раздуваться.
            Assert.LessOrEqual(groupCircle, 0.06f,
                "кружочки оставленного яруса больше 6 см — это уже крупный ярус");
        }

        // ==================================================== 2. ОБЩИЙ ТУМБЛЕР (Y) СОХРАНЁН

        /// <summary>
        /// КНОПКА Y ПО-ПРЕЖНЕМУ ГАСИТ ВСЁ, ЧТО ВИСИТ У РУКИ.
        ///
        /// Что ловит: вынос видимости второго яруса в отдельную ветку так, что общий тумблер
        /// перестал бы его касаться, — тогда Y прятал бы один ярус, а второй оставлял висеть.
        /// </summary>
        [Test]
        public void MasterToggle_StillHidesWhateverHangsOnTheHand()
        {
            Assert.IsFalse(KvXrQuickPanel.LayerVisible(true, false, true),
                "тумблер панели у руки выключен — показывать нечего, даже если ярус включён");
            Assert.IsFalse(KvXrQuickPanel.LayerVisible(true, true, false),
                "шлема на голове нет — панель некому показывать");
            Assert.IsFalse(KvXrQuickPanel.LayerVisible(false, true, false));
            Assert.IsTrue(KvXrQuickPanel.LayerVisible(true, true, true));
        }

        // ==================================================== 3. ВОЗВРАТ ВТОРОГО ЯРУСА

        /// <summary>
        /// ВТОРОЙ ЯРУС ВОЗВРАЩАЕТСЯ СТРОКОЙ В ФАЙЛЕ ПОДСТРОЙКИ, БЕЗ ПЕРЕСБОРКИ.
        ///
        /// Что ловит: зашитое «второй ярус выключен навсегда» — владелец обязан иметь возможность
        /// сравнить оба яруса глазами, не дожидаясь новой сборки.
        /// </summary>
        [Test]
        public void SecondLayer_CanBeReturnedWithoutRebuild()
        {
            // Ключа нет — решает настройка (по умолчанию скрыт).
            Assert.IsFalse(KvXrTuning.ResolveQuickLayer(
                new Dictionary<string, string>(), KvXrSettings.DefaultQuickLayerVisible != 0),
                "без ключа в файле решение остаётся за настройкой");
            Assert.IsFalse(KvXrTuning.ResolveQuickLayer(null,
                KvXrSettings.DefaultQuickLayerVisible != 0),
                "нет файла подстройки — нечему падать, решение за настройкой");

            var on = new Dictionary<string, string> { { KvXrTuning.KeyQuickLayer, "1" } };
            Assert.IsTrue(KvXrTuning.ResolveQuickLayer(on, false),
                "menu.quickLayer=1 обязан возвращать второй ярус");

            var off = new Dictionary<string, string> { { KvXrTuning.KeyQuickLayer, "0" } };
            Assert.IsFalse(KvXrTuning.ResolveQuickLayer(off, true),
                "menu.quickLayer=0 обязан его гасить — иначе ключ односторонний");

            var word = new Dictionary<string, string> { { KvXrTuning.KeyQuickLayer, "вкл" } };
            Assert.IsTrue(KvXrTuning.ResolveQuickLayer(word, false),
                "человеческие написания читаются так же, как у остальных ключей файла подстройки");

            StringAssert.Contains("menu.quickLayer", KvXrTuning.KeyQuickLayer,
                "ключ обязан называться как задокументировано — иначе строка в файле не сработает");
        }

        // ==================================================== 4. НАСТРОЙКА ЖИВЁТ В ХРАНИЛИЩЕ

        /// <summary>НАСТРОЯКА ВТОРОГО ЯРУСА ПИШЕТСЯ И ЧИТАЕТСЯ ОБРАТНО (и не теряется).</summary>
        [Test]
        public void SecondLayerSetting_RoundTripsThroughStorage()
        {
            bool hadKey = PlayerPrefs.HasKey(KvXrSettings.KeyQuickLayerVisible);
            int original = PlayerPrefs.GetInt(KvXrSettings.KeyQuickLayerVisible, 0);
            try
            {
                KvXrSettings.SetQuickLayerVisible(true);
                Assert.IsTrue(KvXrSettings.GetQuickLayerVisible(), "записали true — читается true");

                KvXrSettings.SetQuickLayerVisible(false);
                Assert.IsFalse(KvXrSettings.GetQuickLayerVisible(), "записали false — читается false");
            }
            finally
            {
                if (hadKey) PlayerPrefs.SetInt(KvXrSettings.KeyQuickLayerVisible, original);
                else PlayerPrefs.DeleteKey(KvXrSettings.KeyQuickLayerVisible);
                PlayerPrefs.Save();
            }
        }
    }
}
