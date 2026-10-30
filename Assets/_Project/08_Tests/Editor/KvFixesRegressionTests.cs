// ================================================================================================
//  KvFixesRegressionTests.cs — EditMode-СКВОЗНОЙ СТОРОЖ ИСПРАВЛЕНИЙ НОЧИ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. За ночь исправлено 18 дефектов. Отдельные тесты проверяют поведение каждого, но
//  ничто не проверяет, что САМА ЗАЩИТА осталась в коде. Между тем именно откат (ручной merge,
//  «упрощение» при рефакторинге, случайное удаление блока) возвращает дефект — и заметить это
//  трудно, потому что тесты соседнего класса продолжат проходить.
//
//  ЧТО ПРОВЕРЯЕТСЯ: для каждого исправления — наличие того элемента, без которого дефект вернётся.
//  Каждая строка списка — конкретный дефект из `Docs/ИТОГ_НОЧИ_S44.md`.
//
//  ГРАНИЦА ЧЕСТНОСТИ: это проверка НАЛИЧИЯ, а не поведения. Поведение проверяют отдельные тесты.
//  Здесь задача узкая и дешёвая: «защита на месте?» — чтобы откат ловился сразу.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Сквозной сторож: исправления ночи не откачены.</summary>
    public class KvFixesRegressionTests
    {
        /// <summary>
        /// ЧТО ДОЛЖНО БЫТЬ В КОДЕ, ЧТОБЫ ДЕФЕКТ НЕ ВЕРНУЛСЯ.
        /// Файл → (что ищем, какой дефект это закрывает).
        /// </summary>
        private static readonly (string file, string needle, string defect)[] Guards =
        {
            // --- NaN: восемь мест (E-21)
            ("KvXrControllerVisuals.cs", "IsNaN", "NaN-поза: контроллер исчезал из мира"),
            ("KvXrGestures.cs", "IsNotFinite", "NaN в жестах: жест молча не срабатывал"),
            ("KvXrGroupPanelLayout.cs", "IsNotFinite", "NaN в луче: панель нажималась сама"),
            ("KvXrWindowDrag.cs", "IsFinite", "NaN в переносе: окно уезжало в бесконечность"),
            ("KvXrGroupPanel.cs", "IsNotFinite", "NaN в позе головы: панель решала, что оператор ушёл"),
            ("NarrowPhase.cs", "IsFinite", "NaN в GJK: ложное препятствие"),
            ("RobotDH.cs", "IsNotFinite", "NaN в CCD: 24 итерации вхолостую, порча позы"),

            // --- безопасность
            ("SafetyGate.cs", "MaxJointVelocityDeg", "скорость никогда не проверялась"),
            ("SafetyGate.cs", "TimesMonotonic", "время назад проходило"),
            ("TrajectoryExecutor.cs", "CheckStandingPose", "на паузе контроль зазора выключался"),
            ("TrajectoryExecutor.cs", "Paused = false", "ПУСК после паузы не двигал робота"),

            // --- данные пользователя
            ("FeatureStorage.cs", "Trim('.", "SafeName пропускал имя из одних точек"),
            ("KvAutomation.cs", "MacroListChanged", "список макросов не обновлялся"),

            // --- MR
            ("KvXrMrRoom.cs", "KvXrPlatformModeSelector.Current", "MrActive верил тумблеру, а не железу"),
            ("KvXrPlatformMode.cs", "worldScale", "строка режима печатала константу вместо масштаба"),
            ("KvXrRig.cs", "KvXrCapabilities", "пробы возможностей были заглушками"),
        };

        private static string ProjectScripts
        {
            get { return Path.Combine(Application.dataPath, "_Project"); }
        }

        private static string Find(string fileName)
        {
            if (!Directory.Exists(ProjectScripts)) return null;
            string[] found = Directory.GetFiles(ProjectScripts, fileName, SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// СПИСОК НЕ ПУСТ и в нём есть записи. Без этого тест ниже зеленел бы впустую.
        /// </summary>
        [Test]
        public void GuardListIsPopulated()
        {
            Assert.Greater(Guards.Length, 10,
                "список защит подозрительно короток (" + Guards.Length + ")");
        }

        /// <summary>
        /// ВСЕ ЗАЩИТЫ НА МЕСТЕ. Прямая защита от отката исправлений.
        ///
        /// Если этот тест покраснел — значит кто-то (возможно, я сам при рефакторинге) убрал
        /// элемент, без которого дефект возвращается. Проверьте `Docs/ИТОГ_НОЧИ_S44.md`, раздел 2.
        /// </summary>
        [Test]
        public void AllNightFixesAreStillInPlace()
        {
            var missing = new List<string>();
            var notFound = new List<string>();

            foreach (var guard in Guards)
            {
                string path = Find(guard.file);
                if (path == null) { notFound.Add(guard.file); continue; }

                string text = File.ReadAllText(path);
                if (text.IndexOf(guard.needle, System.StringComparison.Ordinal) < 0)
                    missing.Add(guard.file + " → «" + guard.needle + "» (" + guard.defect + ")");
            }

            Assert.AreEqual(0, notFound.Count,
                "файлы из списка защит не найдены (переименованы?): " +
                string.Join(", ", notFound.ToArray()));

            Assert.AreEqual(0, missing.Count,
                "ИСПРАВЛЕНИЯ ОТКАЧЕНЫ — эти дефекты вернутся: " + string.Join("; ", missing.ToArray()));
        }

        /// <summary>
        /// КАЖДАЯ ЗАПИСЬ НАЗЫВАЕТ ДЕФЕКТ. Запись без описания бесполезна: при срабатывании непонятно,
        /// что именно проверяли.
        /// </summary>
        [Test]
        public void EveryGuardNamesItsDefect()
        {
            var vague = new List<string>();
            foreach (var guard in Guards)
                if (string.IsNullOrWhiteSpace(guard.defect) || guard.defect.Length < 15)
                    vague.Add(guard.file + " → «" + guard.needle + "»");

            Assert.AreEqual(0, vague.Count,
                "у этих защит нет внятного описания дефекта: " + string.Join("; ", vague.ToArray()));
        }

        /// <summary>
        /// ЗАЩИТЫ РАЗНЫЕ, А НЕ ОДНА И ТА ЖЕ СТРОКА. Если все записи указывают на одно место, список
        /// создаёт видимость покрытия, ничего не покрывая.
        /// </summary>
        [Test]
        public void GuardsAreActuallyDifferent()
        {
            var unique = new HashSet<string>();
            foreach (var guard in Guards) unique.Add(guard.file + "|" + guard.needle);

            Assert.AreEqual(Guards.Length, unique.Count,
                "в списке защит есть повторы (" + Guards.Length + " записей, " + unique.Count +
                " уникальных) — покрытие меньше, чем кажется");
        }
    }
}
