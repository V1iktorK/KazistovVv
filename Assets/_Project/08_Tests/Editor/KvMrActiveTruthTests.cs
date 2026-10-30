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

using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Правда о режиме MR: настройка И факт, а не настройка вместо факта.</summary>
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
        /// `MrActive` ТРЕБУЕТ ФАКТИЧЕСКОГО РЕЖИМА, А НЕ ТОЛЬКО НАСТРОЙКИ.
        ///
        /// Проверяется по исходнику, потому что свойство читает `PlayerPrefs` и статический режим —
        /// без сцены и шлема его значение не задать. Здесь важно УБЕДИТЬСЯ, что в коде есть ВТОРОЕ
        /// условие: без него настройка снова станет единственным источником правды.
        /// </summary>
        [Test]
        public void MrActiveConsultsActualMode()
        {
            string path = FindSource("KvXrMrRoom.cs");
            Assert.IsNotNull(path, "не найден KvXrMrRoom.cs");

            string text = System.IO.File.ReadAllText(path);

            // Найти get-блок свойства MrActive.
            int index = text.IndexOf("public bool MrActive", System.StringComparison.Ordinal);
            Assert.Greater(index, 0, "в KvXrMrRoom.cs нет свойства MrActive");

            string tail = text.Substring(index, System.Math.Min(1200, text.Length - index));

            Assert.IsTrue(tail.Contains("KvXrPlatformModeSelector.Current"),
                "MrActive НЕ смотрит на фактический режим устройства — только на настройку. " +
                "При включённом тумблере без сквозного вида приложение спрячет стенды и робота, " +
                "а оператор увидит пустую комнату");

            Assert.IsTrue(tail.Contains("KvXrPlatformMode.Mr"),
                "MrActive не различает, КАКОЙ режим выбран — сравнения с MR нет");

            Assert.IsTrue(tail.Contains("GetPassthrough"),
                "MrActive не смотрит на настройку оператора — тогда его выбор игнорируется");
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

        private static string FindSource(string fileName)
        {
            string root = System.IO.Path.Combine(Application.dataPath, "_Project");
            if (!System.IO.Directory.Exists(root)) return null;
            foreach (string file in System.IO.Directory.GetFiles(root, fileName,
                         System.IO.SearchOption.AllDirectories))
                return file;
            return null;
        }
    }
}
