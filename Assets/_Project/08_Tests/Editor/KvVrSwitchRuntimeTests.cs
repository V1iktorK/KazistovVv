// ================================================================================================
//  KvVrSwitchRuntimeTests.cs — ПЕРЕКЛЮЧАТЕЛЬ VR/MR: VR ВКЛЮЧАЕТСЯ СРАЗУ (20.11.2026).
//
//  ОТКУДА ЗАДАЧА. Слово владельца дословно: «режим VR не переносит на сцену VR, а продолжает
//  работать в MR».
//
//  ЧТО БЫЛО. Переключатель (`KvXrSettings.ToggleMode`) писал настройку, `KvXrMrRoom.MrActive`
//  становился ложью, виртуальные стенды возвращались — а конвейер MR жил дальше:
//    • `KvMrRuntime.DisablePassthrough` не звался НИОТКУДА (в коде не было ни одного вызова);
//    • пасс-слой PICO оставался включённым, то есть оператор по-прежнему видел комнату;
//    • `KvMrSurfaces` продолжал отслеживать меш комнаты и печатать «РЕАЛЬНЫЙ СТОЛ»;
//    • покрытие рисовалось, робот оставался на реальном столе.
//  Итог: «VR» выглядел как MR со следами виртуального — ровно жалоба владельца.
//
//  ЧТО КРЕПИТСЯ ЗДЕСЬ:
//    1) правило «гасить сеанс» — чистая функция с жёсткой таблицей истинности;
//    2) сам возврат: отслеживание комнаты выключается, сквозной вид гаснет (проверяется на
//       живых компонентах, без шлема);
//    3) все четыре шага возврата присутствуют в коде — пустой `EndMrSession` тест поймает;
//    4) журнал переключателя больше не обещает «перезапуск» для направления VR, которое
//       применяется в том же кадре.
//
//  ЧЕСТНО О ГРАНИЦЕ. Включение MR по-прежнему требует перезапуска приложения (пасс-слой
//  поднимается только при старте) — это не менялось и закреплено `ModeRestartNote`.
// ================================================================================================

using System.IO;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Переключение VR/MR: VR должен выключать passthrough и MR-мир, а не жить с ними.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvVrSwitchRuntimeTests
    {
        /// <summary>Прочитать исходник XR-скрипта (для проверок «в коде есть шаг возврата»).</summary>
        private static string ReadScript(string fileName)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "01_Scripts", "Xr",
                fileName));
        }

        // ==================================================== 1. ПРАВИЛО

        /// <summary>
        /// ГАСИТЬ КОНВЕЙЕР MR — ТОЛЬКО КОГДА ОН ЗАПУЩЕН И РЕЖИМ СТАЛ VR.
        ///
        /// Что ловит: гашение без сеанса (при старте в VR выключать нечего — и выключать нельзя
        /// чужое) и гашение при включённом MR (оператор как раз просил комнату).
        /// </summary>
        [Test]
        public void SwitchToVr_EndsTheSessionOnlyWhenItWasRunning()
        {
            Assert.IsTrue(KvXrMrRoom.MustEndMrSession(sessionStarted: true, mrActive: false),
                "сеанс MR запущен, режим переключили в VR — конвейер обязан погаснуть");

            Assert.IsFalse(KvXrMrRoom.MustEndMrSession(sessionStarted: false, mrActive: false),
                "сеанса не было — гасить нечего (старт в VR)");
            Assert.IsFalse(KvXrMrRoom.MustEndMrSession(sessionStarted: true, mrActive: true),
                "режим остался MR — гасить нельзя: оператор просил комнату");
            Assert.IsFalse(KvXrMrRoom.MustEndMrSession(sessionStarted: false, mrActive: true));
        }

        // ==================================================== 2. ВОЗВРАТ ЖИВЬЁМ

        /// <summary>
        /// ПОСЛЕ ВОЗВРАТА В VR КОМНАТА БОЛЬШЕ НЕ ОТСЛЕЖИВАЕТСЯ, А ПАСС-СЛОЙ ВЫКЛЮЧЕН.
        ///
        /// Что ловит: пустой `EndMrSession` («лог напечатал, а конвейер оставил») — проверяется на
        /// настоящих компонентах в EditMode, без шлема: в редакторе сквозной вид и так выключен,
        /// поэтому честно проверяется то, что проверяемо, — отслеживание комнаты и признак
        /// пасс-слоя.
        /// </summary>
        [Test]
        public void EndMrSession_StopsRoomTrackingAndPassthrough()
        {
            var roomGo = new GameObject("KvVrSwitchRuntime_room");
            var surfacesGo = new GameObject("KvVrSwitchRuntime_surfaces");
            try
            {
                KvXrMrRoom room = roomGo.AddComponent<KvXrMrRoom>();
                KvMrSurfaces surfaces = surfacesGo.AddComponent<KvMrSurfaces>();

                Assert.IsNotNull(room, "комната MR не создалась — проверять возврат нечего");
                Assert.IsTrue(surfaces.Tracking,
                    "до переключения комната отслеживается (иначе MR не работал бы)");

                room.EndMrSession();

                Assert.IsFalse(surfaces.Tracking,
                    "после возврата в VR отслеживание комнаты обязано быть выключено — иначе " +
                    "меш комнаты продолжает варить физику на главном потоке, то есть MR живёт дальше");
                Assert.IsFalse(KvMrRuntime.PassthroughOn,
                    "после возврата в VR сквозной вид обязан быть выключен");
            }
            finally
            {
                Object.DestroyImmediate(roomGo);
                Object.DestroyImmediate(surfacesGo);
            }
        }

        // ==================================================== 3. ВСЕ ШАГИ ВОЗВРАТА В КОДЕ

        /// <summary>
        /// ЧЕТЫРЕ ШАГА ВОЗВРАТА ПРИСУТСТВУЮТ В КОДЕ — иначе `EndMrSession` снова станет пустым.
        ///
        /// Проверяется текстом, потому что каждый шаг требует оборудования (пасс-слой PICO,
        /// AR-камера Meta), а в EditMode его нет. Смысл проверки: удалённый шаг ловится сразу.
        /// </summary>
        [Test]
        public void ReturnPath_StepsAreAllInCode()
        {
            string room = ReadScript("KvXrMrRoom.cs");
            StringAssert.Contains("MustEndMrSession(mrSessionStarted, MrActive)", room,
                "правило возврата не подключено к ApplyLayers — переключатель снова не сработает");
            StringAssert.Contains("KvMrRuntime.DisablePassthrough(headCamera)", room,
                "возврат не выключает пасс-слой — MR останется видимым в VR (корень жалобы)");
            StringAssert.Contains("painter.showSurfaces = false", room,
                "возврат не гасит покрытие реальных поверхностей — линии останутся на месте стола");
            StringAssert.Contains("surfaces.StopTracking()", room,
                "возврат не останавливает отслеживание комнаты");
            StringAssert.Contains("ReturnRobotToStand", room,
                "робот остаётся на реальном столе в VR — его некуда вернуть");
            StringAssert.Contains("robotStandParent = root.parent", room,
                "поза стенда не сохраняется при входе в MR — вернуть робота некуда");

            string runtime = ReadScript("KvMrRuntime.cs");
            StringAssert.Contains("RestoreCameraBackground", runtime,
                "фон камеры не возвращается к виду до MR — VR выглядит чёрным");
            StringAssert.Contains("EnableVideoSeeThrough = false", runtime,
                "пасс-слой PICO не выключается — комната остаётся на экране");
            StringAssert.Contains("manager.enabled = false", runtime,
                "ветка Meta не выключает ARCameraManager — камера продолжает отдавать комнату");

            // Стенды возвращаются тем же ApplyLayers: MR выключен → прятать их нельзя.
            StringAssert.Contains("HidesVirtualStands(MrActive", room,
                "виртуальная лаборатория возвращается тем же правилом, что и гасилась");
        }

        // ==================================================== 4. ЖУРНАЛ ПЕРЕКЛЮЧАТЕЛЯ

        /// <summary>
        /// ЖУРНАЛ НЕ ОБЕЩАЕТ «ПЕРЕЗАПУСК» ДЛЯ НАПРАВЛЕНИЯ VR, КОТОРОЕ ПРИМЕНЯЕТСЯ СРАЗУ.
        ///
        /// Что ловит: старую строку, которая печаталась в обоих направлениях, — после неё
        /// оператор ждал перезапуска там, где всё уже работало, и не находил изменения.
        /// </summary>
        [Test]
        public void ToggleLog_DistinguishesImmediateVrFromRestartedMr()
        {
            string panel = ReadScript("KvXrGroupPanel.cs");
            StringAssert.Contains("выбран VR — сквозной вид и MR-мир выключаются БЕЗ перезапуска",
                panel, "направление VR обязано быть названо как применяющееся сразу");
            StringAssert.Contains("для полного включения MR нужен перезапуск", panel,
                "направление MR обязано честно назвать свой срок — включение по-прежнему при старте");
        }

        // ==================================================== 5. СТАРТ НЕ ЛОМАЕТСЯ

        /// <summary>
        /// ЗАПУСК В VR НЕ ПЫТАЕТСЯ ЗАКРЫТЬ НЕСУЩЕСТВУЮЩИЙ СЕАНС, А ЗАПУСК В MR — ЕГО ОТКРЫВАЕТ.
        /// </summary>
        [Test]
        public void StartDecidesByActualModeNotByTheToggle()
        {
            string room = ReadScript("KvXrMrRoom.cs");
            StringAssert.Contains("if (ShouldPlaceRobotOnRealTable(MrActive)) BeginMrSession();", room,
                "сеанс MR начинается по ФАКТИЧЕСКОМУ режиму, а не по тумблеру");
            StringAssert.Contains("if (MustEndMrSession(mrSessionStarted, MrActive)) EndMrSession();",
                room, "возврат в VR повешен на смену настроек — без этого переключатель молчит");
        }
    }
}
