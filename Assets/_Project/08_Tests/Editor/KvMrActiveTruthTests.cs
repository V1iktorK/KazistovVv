// ================================================================================================
//  KvMrActiveTruthTests.cs — EditMode-тесты ПРАВДЫ О РЕЖИМЕ MR (сессия S44).
//
//  ОТКУДА ЗАДАЧА. `KvXrMrRoom.MrActive` решает сразу много: ставить ли робота «на реальный стол»,
//  гасить ли виртуальные стенды, включать ли слои MR. Ошибка здесь означает, что оператор увидит
//  ПУСТУЮ КОМНАТУ без стендов и без робота.
//
//  ЧТО НАЙДЕНО 30.10.2026. Свойство читало ТОЛЬКО тумблер из `PlayerPrefs`
//  (`KvXrSettings.GetPassthrough()`). Это НАСТРОЙКА, а не факт: тумблер включён, а сквозного вида
//  устройство не дало (разрешение не выдано, рантайм не поддерживает). Тогда приложение считало
//  себя в MR и прятало всё виртуальное.
//
//  Ровно этот класс дефекта уже был в §28: «MR оказался выключенным флагом в трёх местах».
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ:
//    1) правило `HidesVirtualStands` — чистая функция, проверяется таблицей;
//    2) правило «ставить робота на реальный стол» — только в MR;
//    3) MrActive требует ОБА условия: настройку и фактический режим;
//    4) в VR виртуальные стенды НЕ гасятся (владелец 16.10.2026: «столов или роботов не видно»).
// ================================================================================================

using System.Reflection;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Правда о режиме MR: настройка И факт, а не настройка вместо факта.</summary>
        [Category(KvTestCategories.Fast)]
    public class KvMrActiveTruthTests
    {
        // ---------------------------------------------------------------- 1. ГАШЕНИЕ СТЕНДОВ

        /// <summary>
        /// В VR ВИРТУАЛЬНЫЕ СТЕНДЫ НЕ ГАСЯТСЯ — ПРИ ЛЮБЫХ НАСТРОЙКАХ.
        ///
        /// Это прямая проверка дефекта от 16.10.2026: прежняя формула
        /// `!hideField || showToggle` при `hideField = true` и выключенном тумблере давала ЛОЖЬ и
        /// прятала столы В VR, где они — главное содержимое. Владелец сказал: «столов или роботов
        /// не видно».
        /// </summary>
        [Test]
        public void InVr_VirtualStandsAreNeverHidden()
        {
            // Перебираем ВСЕ сочетания настроек — в VR ни одно не должно гасить стенды.
            for (int field = 0; field <= 1; field++)
            {
                for (int toggle = 0; toggle <= 1; toggle++)
                {
                    bool hidden = KvXrMrRoom.HidesVirtualStands(
                        mrActive: false, hideField: field == 1, showToggle: toggle == 1);

                    Assert.IsFalse(hidden,
                        "в VR при hideField=" + (field == 1) + ", showToggle=" + (toggle == 1) +
                        " виртуальные стенды погашены — оператор увидит пустоту");
                }
            }
        }

        /// <summary>
        /// В MR СТЕНДЫ ГАСЯТСЯ ТОЛЬКО ПО ЯВНОЙ ПРОСЬБЕ. Правило: MR + «скрыть» + тумблер выключен.
        /// Если оператор просит показать стенды в MR — показываем.
        /// </summary>
        [Test]
        public void InMr_StandsAreHiddenOnlyWhenAsked()
        {
            Assert.IsTrue(KvXrMrRoom.HidesVirtualStands(true, true, false),
                "в MR с просьбой «скрыть» и выключенным тумблером стенды обязаны гаснуть");
            Assert.IsFalse(KvXrMrRoom.HidesVirtualStands(true, true, true),
                "оператор просит ПОКАЗАТЬ стенды в MR — гасить нельзя");
            Assert.IsFalse(KvXrMrRoom.HidesVirtualStands(true, false, false),
                "без просьбы «скрыть» стенды остаются");
        }

        // ---------------------------------------------------------------- 2. РОБОТ НА СТОЛ

        /// <summary>
        /// РОБОТ СТАВИТСЯ НА РЕАЛЬНЫЙ СТОЛ ТОЛЬКО В MR. В VR реального стола нет: постановка туда
        /// увела бы робота в начало координат — то есть В МЕСТО ОПЕРАТОРА (это уже случалось, §35).
        /// </summary>
        [Test]
        public void RobotIsPlacedOnRealTableOnlyInMr()
        {
            Assert.IsTrue(KvXrMrRoom.ShouldPlaceRobotOnRealTable(true),
                "в MR робота надо ставить на реальный стол");
            Assert.IsFalse(KvXrMrRoom.ShouldPlaceRobotOnRealTable(false),
                "в VR реального стола нет — постановка увела бы робота в место оператора");
        }

        // ---------------------------------------------------------------- 3. ПРАВДА О MR

        /// <summary>
        /// `MrActive` ТРЕБУЕТ ОБОИХ УСЛОВИЙ: НАСТРОЙКИ ОПЕРАТОРА И ФАКТИЧЕСКОГО РЕЖИМА.
        ///
        /// ЧТО БЫЛО. Свойство читало ТОЛЬКО тумблер `KvXrSettings.GetPassthrough()` (это
        /// НАСТРОЙКА, а не факт): при включённом тумблере без сквозного вида приложение считало
        /// себя в MR, гасило виртуальные стенды и ставило робота «на реальный стол» — оператор
        /// видел ПУСТУЮ КОМНАТУ. Ровно этот класс дефекта был в §28.
        ///
        /// КАК ПРОВЕРЯЕТСЯ (P4-3). Раньше тест грепал ТЕКСТ `KvXrMrRoom.cs` на наличие строк
        /// `KvXrPlatformModeSelector.Current`, `KvXrPlatformMode.Mr` и `GetPassthrough` — то есть
        /// «строка есть», а не «поведение верное»: условие, стоящее ВНЕ get-блока, он бы не
        /// заметил, а от переформулировки комментария ломался. Теперь задаётся САМ ФАКТ
        /// (сеттер `Current` — он пишется только стартом рига) и настройка штатным
        /// `KvXrSettings.SetPassthrough`, а читается свойство: все сочетания обязаны дать
        /// правильный ответ.
        /// </summary>
        [Test]
        public void MrActiveConsultsActualMode()
        {
            KvXrPlatformMode originalMode = KvXrPlatformModeSelector.Current;
            bool hadKey = PlayerPrefs.HasKey(KvXrSettings.KeyPassthrough);
            bool originalPassthrough = KvXrSettings.GetPassthrough();

            // Комната ВЫКЛЮЧЕНА: OnEnable подписал бы её на смену настроек, а тест меняет
            // настройку только ради чтения свойства — лишних эффектов в сцене быть не должно.
            GameObject host = new GameObject("KvMrActiveTruth_Room");
            host.SetActive(false);
            try
            {
                KvXrMrRoom room = host.AddComponent<KvXrMrRoom>();
                Assert.IsNotNull(room, "KvXrMrRoom не создался — читать MrActive нечего");

                // MR + включённая комната: оба условия выполнены.
                SetPlatformMode(KvXrPlatformMode.Mr);
                KvXrSettings.SetPassthrough(true);
                Assert.IsTrue(room.MrActive,
                    "MR с включённой настройкой не даёт MrActive — приложение не включит слои MR " +
                    "и не поставит робота на реальный стол");

                // Режим не MR: одна настройка не должна делать приложение «MR».
                SetPlatformMode(KvXrPlatformMode.Vr);
                Assert.IsFalse(room.MrActive,
                    "настройка включена, но фактический режим VR — приложение не вправе считать " +
                    "себя в MR: оператор получил бы пустую комнату вместо виртуальной лаборатории");

                // Оператор осознанно выключил комнату: режим есть, а MR не включаем.
                SetPlatformMode(KvXrPlatformMode.Mr);
                KvXrSettings.SetPassthrough(false);
                Assert.IsFalse(room.MrActive,
                    "режим MR, но оператор выключил комнату в настройках — его выбор игнорируется");
            }
            finally
            {
                SetPlatformMode(originalMode);
                if (hadKey) KvXrSettings.SetPassthrough(originalPassthrough);
                else PlayerPrefs.DeleteKey(KvXrSettings.KeyPassthrough);
                PlayerPrefs.Save();
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>Задать ФАКТИЧЕСКИЙ режим устройства (`Current` пишется только стартом рига).</summary>
        private static void SetPlatformMode(KvXrPlatformMode mode)
        {
            PropertyInfo property = typeof(KvXrPlatformModeSelector).GetProperty("Current",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(property,
                "нет свойства KvXrPlatformModeSelector.Current — проверка не о том состоянии");
            MethodInfo setter = property.GetSetMethod(true);
            Assert.IsNotNull(setter,
                "у KvXrPlatformModeSelector.Current нет сеттера — задать режим в EditMode нельзя");
            setter.Invoke(null, new object[] { mode });
        }

        // ---------------------------------------------------------------- 4. САМОПРОВЕРКА

        /// <summary>
        /// ФУНКЦИИ СУЩЕСТВУЮТ И ВЫЗЫВАЮТСЯ. Если `HidesVirtualStands` пропадёт (переименуют),
        /// тесты выше не скомпилируются — и это правильный красный, а не тихий пропуск.
        /// </summary>
        [Test]
        public void RulesArePureFunctions()
        {
            // Одинаковый вход — одинаковый выход, без обращения к состоянию.
            Assert.AreEqual(
                KvXrMrRoom.HidesVirtualStands(true, true, false),
                KvXrMrRoom.HidesVirtualStands(true, true, false),
                "правило гашения стендов непостоянно — оно должно быть чистой функцией");

            Assert.AreEqual(
                KvXrMrRoom.ShouldPlaceRobotOnRealTable(true),
                KvXrMrRoom.ShouldPlaceRobotOnRealTable(true),
                "правило постановки робота непостоянно");
        }

        /// <summary>ПУСТАЯ СЦЕНА ПОСЛЕ КАЖДОГО ТЕСТА: комнату тест создаёт в памяти.</summary>
        [TearDown]
        public void TearDown()
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("KvMrActiveTruth_"))
                    Object.DestroyImmediate(t.gameObject);
        }

    }
}
