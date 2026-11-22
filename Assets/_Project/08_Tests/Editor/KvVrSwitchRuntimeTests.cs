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
//    3) КАЖДЫЙ ШАГ ВОЗВРАТА ВЫПОЛНЯЕТСЯ НА ЖИВЫХ ОБЪЕКТАХ (правка 21.11.2026, P4-3). Раньше
//       шаги проверялись ТЕКСТОМ исходника (`StringAssert.Contains` по файлам `KvXrMrRoom.cs`
//       и `KvMrRuntime.cs`) — такой тест ПРОХОДИЛ НА СЛОМАННОМ КОДЕ («строка есть», а не
//       «шаг отработал») и ломался от правки комментария. Теперь шаги выполняются: пасс-слой
//       гаснет (флаг PICO и ветка Meta — тоже), фон камеры возвращается к виду до MR,
//       отслеживание комнаты и покрытие выключаются, робот возвращается на стенд в прежнюю
//       позу, виртуальная лаборатория снова видна;
//    4) журнал переключателя больше не обещает «перезапуск» для направления VR, которое
//       применяется в том же кадре — строку считает чистая функция
//       `KvXrGroupPanel.ModeDeadlineNote`, и тест проверяет ЕЁ вывод, а не текст файла;
//    5) старт решает по ФАКТИЧЕСКОМУ режиму устройства, а не по тумблеру: корутина `Start`
//       ВЫПОЛНЯЕТСЯ в тесте (режим уже определён) — сеанс начинается/не начинается по факту.
//
//  ЧЕСТНО О ГРАНИЦЕ. Включение MR по-прежнему требует перезапуска приложения (пасс-слой
//  поднимается только при старте) — это не менялось и закреплено `ModeRestartNote`.
// ================================================================================================

using System.Collections;
using System.Reflection;
using KazistovVvXr;
using NUnit.Framework;
using Unity.XR.OpenXR.Features.PICOSupport;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace KazistovVvTests
{
    /// <summary>Переключение VR/MR: VR должен выключать passthrough и MR-мир, а не жить с ними.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvVrSwitchRuntimeTests
    {
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

        // ========================================== 3. ВСЕ ШАГИ ВОЗВРАТА ВЫПОЛНЯЮТСЯ

        /// <summary>
        /// КАЖДЫЙ ШАГ ВОЗВРАТА ОТРАБАТЫВАЕТ НА ЖИВЫХ ОБЪЕКТАХ.
        ///
        /// Что ловит (класс дефектов прежнего текстового теста): УДАЛЁННЫЙ ШАГ ВОЗВРАТА. Раньше
        /// здесь стояли `StringAssert.Contains` по исходникам — «строка в файле есть», а проверить
        /// по тексту нельзя, отработал ли шаг. Теперь шаги ВЫПОЛНЯЮТСЯ:
        ///   1) пасс-слой гаснет, включая флаг PICO `EnableVideoSeeThrough` и ветку Meta
        ///      (`ARCameraManager.enabled = false`);
        ///   2) фон камеры возвращается к виду ДО MR (иначе VR остался бы с прозрачным, то есть
        ///      чёрным, фоном);
        ///   3) отслеживание комнаты и покрытие (`showSurfaces`) выключаются;
        ///   4) робот возвращается на стенд в сохранённую при входе в MR позу.
        /// </summary>
        [Test]
        public void ReturnPath_EveryStepActuallyRuns()
        {
            var roomGo = new GameObject("KvVrSwitchRuntime_room2");
            var surfacesGo = new GameObject("KvVrSwitchRuntime_surfaces2");
            var painterGo = new GameObject("KvVrSwitchRuntime_painter");
            var standGo = new GameObject("ТестСтенд_KvVrSwitch");
            var robotGo = new GameObject("ТестРобот_KvVrSwitch");
            var cameraGo = new GameObject("KvVrSwitchRuntime_camera");
            var managerGo = new GameObject("KvVrSwitchRuntime_ar_camera");
            bool hadPassthroughFlag = false;
            int passthroughFlag = -1;
            KvMrBackend backendBefore = KvMrRuntime.Backend;
            bool passthroughBefore = KvMrRuntime.PassthroughOn;
            try
            {
                KvXrMrRoom room = roomGo.AddComponent<KvXrMrRoom>();
                KvMrSurfaces surfaces = surfacesGo.AddComponent<KvMrSurfaces>();
                KvMrSurfacePainter painter = painterGo.AddComponent<KvMrSurfacePainter>();
                Camera camera = cameraGo.AddComponent<Camera>();
                CameraClearFlags flagsBefore = camera.clearFlags;
                Color colorBefore = camera.backgroundColor;

                // --- подготовка: робот лежит ребёнком стенда (как в сцене), камера «в MR».
                robotGo.transform.SetParent(standGo.transform, false);
                robotGo.AddComponent<SixAxisController>();
                Vector3 standLocal = robotGo.transform.localPosition;

                room.headCamera = camera;
                Assert.IsTrue(room.PrepareRobotForWorld(),
                    "робот не подготовился к MR: " + room.LastPlacementError);
                Assert.AreNotSame(standGo.transform, robotGo.transform.parent,
                    "подготовка: робот должен уйти из стенда в мировой корень MR");

                // ПАСС-СЛОЙ «ВКЛЮЧЁН» (так было в MR) + ВЕТКА META С ЖИВОЙ AR-КАМЕРОЙ.
                // Менеджер ставится НА ТОТ ЖЕ ОБЪЕКТ, что и камера: ветка Meta ищет его через
                // `headCamera.GetComponent<ARCameraManager>()` — на чужом объекте его нет.
                // Ветка PICO проверяется ОТДЕЛЬНЫМ тестом `DisablePassthrough_TurnsOffPicoFlag`:
                // у вызова один Backend, поэтому в одном прогоне их не проверить честно.
                SetStaticProperty(typeof(KvMrRuntime), "PassthroughOn", true);
                ARCameraManager manager = cameraGo.AddComponent<ARCameraManager>();
                Assert.IsTrue(manager.enabled, "подготовка: ARCameraManager должен быть включён");
                SetStaticProperty(typeof(KvMrRuntime), "Backend", KvMrBackend.Meta);

                InvokeNonPublicStatic(typeof(KvMrRuntime), "MakeCameraBackgroundTransparent",
                    camera);
                Assert.AreEqual(CameraClearFlags.SolidColor, camera.clearFlags,
                    "подготовка: фон камеры в MR должен стать прозрачным");

                Assert.IsTrue(surfaces.Tracking, "подготовка: комната отслеживается");
                painter.showSurfaces = true;

                // --- ВОЗВРАТ В VR.
                room.EndMrSession();

                Assert.IsFalse(KvMrRuntime.PassthroughOn, "шаг 1: сквозной вид обязан погаснуть");
                Assert.IsFalse(manager.enabled,
                    "шаг 1 (Meta): ARCameraManager обязан выключиться — иначе камера продолжает " +
                    "отдавать комнату (класс дефекта «ветка Meta не выключает менеджер»)");
                Assert.AreEqual(flagsBefore, camera.clearFlags,
                    "шаг 1: фон камеры обязан вернуться к виду ДО MR — иначе VR выглядит чёрным");
                Assert.AreEqual(colorBefore, camera.backgroundColor,
                    "шаг 1: цвет фона камеры обязан вернуться к виду ДО MR");

                Assert.IsFalse(surfaces.Tracking,
                    "шаг 2: отслеживание комнаты обязано остановиться — иначе меш комнаты " +
                    "продолжает варить физику на главном потоке");
                Assert.IsFalse(painter.showSurfaces,
                    "шаг 2: покрытие реальных поверхностей обязано погаснуть — иначе линии " +
                    "останутся на месте стола");

                Assert.AreSame(standGo.transform, robotGo.transform.parent,
                    "шаг 3: робот обязан вернуться на стенд");
                Assert.AreEqual(standLocal, robotGo.transform.localPosition,
                    "шаг 3: поза робота на стенде должна быть ровно прежней");
            }
            finally
            {
                RestorePassthroughStatics(hadPassthroughFlag, passthroughFlag,
                    passthroughBefore, backendBefore);
                Object.DestroyImmediate(roomGo);
                Object.DestroyImmediate(surfacesGo);
                Object.DestroyImmediate(painterGo);
                Object.DestroyImmediate(standGo);
                Object.DestroyImmediate(robotGo);
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(managerGo);
                DestroyLeftovers();
            }
        }

        /// <summary>
        /// ШАГ 1, ВЕТКА PICO: ПОСЛЕ ВОЗВРАТА ФЛАГ `EnableVideoSeeThrough` СБРОШЕН.
        ///
        /// Что ловит (класс дефектов прежнего текстового теста): возврат ветки PICO внутри
        /// `KvMrRuntime.DisablePassthrough` — раньше проверялось `StringAssert` по исходнику
        /// («строка `EnableVideoSeeThrough = false` есть»), теперь флаг реально читается ПОСЛЕ
        /// выключения. У вызова один `Backend`, поэтому ветка PICO проверяется отдельно от
        /// ветки Meta (`ReturnPath_EveryStepActuallyRuns`).
        /// </summary>
        [Test]
        public void DisablePassthrough_TurnsOffPicoFlag()
        {
            var cameraGo = new GameObject("KvVrSwitchRuntime_pico_camera");
            bool hadFlag = false;
            int flagBefore = -1;
            KvMrBackend backendBefore = KvMrRuntime.Backend;
            bool passthroughBefore = KvMrRuntime.PassthroughOn;
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                SetStaticProperty(typeof(KvMrRuntime), "Backend", KvMrBackend.Pico);
                SetStaticProperty(typeof(KvMrRuntime), "PassthroughOn", true);

                // ФЛАГ СТАВИМ ЧЕРЕЗ ПОЛЕ: штатный путь инициализирует расширение, а в редакторе
                // его нет — флаг же тот же, что читает оператор на устройстве.
                FieldInfo flag = typeof(PassthroughFeature).GetField(
                    "_enableVideoSeeThrough", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(flag, "в PassthroughFeature нет флага _enableVideoSeeThrough");
                hadFlag = true;
                flagBefore = (int)flag.GetValue(null);
                flag.SetValue(null, 1);
                Assert.IsTrue(PassthroughFeature.EnableVideoSeeThrough,
                    "подготовка: флаг PICO должен показывать включённый пасс-слой");

                KvMrRuntime.DisablePassthrough(camera);

                Assert.IsFalse(KvMrRuntime.PassthroughOn, "сквозной вид обязан погаснуть");
                Assert.IsFalse(PassthroughFeature.EnableVideoSeeThrough,
                    "ветка PICO обязана сбросить EnableVideoSeeThrough — иначе комната " +
                    "остаётся на экране в VR (класс дефекта «пасс-слой не выключается»)");
            }
            finally
            {
                RestorePassthroughStatics(hadFlag, flagBefore, passthroughBefore, backendBefore);
                Object.DestroyImmediate(cameraGo);
            }
        }

        /// <summary>
        /// СМЕНА НАСТРОЙКИ (ТУТ ЖЕ, В ТОТ ЖЕ КАДР) ГАСИТ СЕАНС MR — ПРАВИЛО ПОДКЛЮЧЕНО К
        /// `ApplyLayers`, А НЕ ЛЕЖИТ МЁРТВЫМ.
        ///
        /// Что ловит: потерю строки `MustEndMrSession(mrSessionStarted, MrActive)` из `ApplyLayers`
        /// и отключение слушателя `KvXrSettingsChanged → ApplyLayers` — раньше это проверялось
        /// текстом исходника, теперь правилом, которое ВЫПОЛНЯЕТСЯ.
        /// </summary>
        [Test]
        public void ApplyLayers_EndsTheSessionWhenModeLeftMr()
        {
            var roomGo = new GameObject("KvVrSwitchRuntime_room3");
            var surfacesGo = new GameObject("KvVrSwitchRuntime_surfaces3");
            var painterGo = new GameObject("KvVrSwitchRuntime_painter3");
            var standGo = new GameObject("ТестСтенд_KvVrSwitch3");
            var robotGo = new GameObject("ТестРобот_KvVrSwitch3");
            try
            {
                KvXrMrRoom room = roomGo.AddComponent<KvXrMrRoom>();
                KvMrSurfaces surfaces = surfacesGo.AddComponent<KvMrSurfaces>();
                KvMrSurfacePainter painter = painterGo.AddComponent<KvMrSurfacePainter>();

                robotGo.transform.SetParent(standGo.transform, false);
                robotGo.AddComponent<SixAxisController>();
                Assert.IsTrue(room.PrepareRobotForWorld(),
                    "робот не подготовился: " + room.LastPlacementError);

                // Сеанс запущен, режим больше не MR (в редакторе `MrActive` ложь).
                SetInstanceField(room, "mrSessionStarted", true);
                Assert.IsFalse(room.MrActive,
                    "подготовка: в редакторе MR не активен — значит правило требует погасить сеанс");

                surfaces.StartTracking();
                Assert.IsTrue(surfaces.Tracking, "подготовка: комната отслеживается");
                painter.showSurfaces = true;
                standGo.SetActive(false);        // в MR стенды погашены
                Assert.IsFalse(KvXrMrRoom.HidesVirtualStands(mrActive: false, hideField: true,
                    showToggle: false), "в VR стенды гасить нельзя — правило возврата");

                room.ApplyLayers();               // тот же путь, что и смена настройки

                Assert.IsFalse(GetInstanceField<bool>(room, "mrSessionStarted"),
                    "сеанс обязан закрыться — признак снимается только в EndMrSession");
                Assert.IsFalse(surfaces.Tracking,
                    "смена настройки обязана погасить конвейер MR в ТОТ ЖЕ кадр");
                Assert.IsFalse(painter.showSurfaces,
                    "покрытие обязано погаснуть вместе с сеансом");
                Assert.IsTrue(standGo.activeSelf,
                    "виртуальная лаборатория должна снова быть видна: в MR она гасится, в VR — нет; " +
                    "если стенды остались выключенными, возврат в VR оставил MR-мир на экране");
            }
            finally
            {
                Object.DestroyImmediate(roomGo);
                Object.DestroyImmediate(surfacesGo);
                Object.DestroyImmediate(painterGo);
                Object.DestroyImmediate(standGo);
                Object.DestroyImmediate(robotGo);
                DestroyLeftovers();
            }
        }

        // ==================================================== 4. ЖУРНАЛ ПЕРЕКЛЮЧАТЕЛЯ

        /// <summary>
        /// ЖУРНАЛ НЕ ОБЕЩАЕТ «ПЕРЕЗАПУСК» ДЛЯ НАПРАВЛЕНИЯ VR, КОТОРОЕ ПРИМЕНЯЕТСЯ СРАЗУ.
        ///
        /// Что ловит: старую строку, которая печаталась в обоих направлениях, — после неё
        /// оператор ждал перезапуска там, где всё уже работало, и не находил изменения.
        /// Проверяется ВЫВОД чистой функции `ModeDeadlineNote` (тот же вызов, что идёт в
        /// `ToggleMixedReality`), а не текст файла: текстовый греп ломался от правки комментария.
        /// </summary>
        [Test]
        public void ToggleLog_DistinguishesImmediateVrFromRestartedMr()
        {
            string forVr = KvXrGroupPanel.ModeDeadlineNote(KvXrSettings.ModeVr);
            StringAssert.Contains("БЕЗ перезапуска", forVr,
                "направление VR обязано быть названо как применяющееся сразу");
            Assert.IsFalse(forVr.Contains("нужен перезапуск"),
                "VR не может требовать перезапуска — он применяется в том же кадре");

            string forMr = KvXrGroupPanel.ModeDeadlineNote(KvXrSettings.ModeMr);
            StringAssert.Contains("нужен перезапуск", forMr,
                "направление MR обязано честно назвать свой срок — включение по-прежнему при старте");
            Assert.IsFalse(forMr.Contains("БЕЗ перезапуска"),
                "MR нельзя обещать «сразу» — пасс-слой поднимается только при старте");
        }

        // ==================================================== 5. СТАРТ НЕ ЛОМАЕТСЯ

        /// <summary>
        /// ЗАПУСК В VR НЕ ПЫТАЕТСЯ ЗАКРЫТЬ НЕСУЩЕСТВУЮЩИЙ СЕАНС, А ЗАПУСК В MR — ЕГО ОТКРЫВАЕТ.
        ///
        /// Корутина `Start` ВЫПОЛНЯЕТСЯ в тесте (режим уже определён — иначе она ждала бы его до
        /// 15 с). Вариант VR при ВКЛЮЧЁННОМ тумблере MR — ровно тот класс дефекта, что ловил
        /// прежний текстовый тест: решение принимается по ФАКТИЧЕСКОМУ режиму устройства, а не по
        /// тумблеру настройки; вариант MR доказывает, что старт вообще умеет открыть сеанс.
        /// </summary>
        [Test]
        public void StartDecidesByActualModeNotByTheToggle()
        {
            var roomGo = new GameObject("KvVrSwitchRuntime_room4");
            var standGo = new GameObject("ТестСтенд_KvVrSwitch4");
            var robotGo = new GameObject("ТестРобот_KvVrSwitch4");

            KvXrPlatformMode modeBefore = KvXrPlatformModeSelector.Current;
            bool appliedBefore = KvXrPlatformModeSelector.Applied;
            bool hadPassthrough = PlayerPrefs.HasKey(KvXrSettings.KeyPassthrough);
            int passthroughBefore = PlayerPrefs.GetInt(KvXrSettings.KeyPassthrough, 0);
            try
            {
                KvXrSettings.SetShowWorkZone(false);   // сетка/круг не должны строиться в тесте
                KvXrSettings.SetShowReach(false);

                robotGo.transform.SetParent(standGo.transform, false);
                robotGo.AddComponent<SixAxisController>();

                // РЕЖИМ ОПРЕДЕЛЁН, ИНАЧЕ `Start` ЖДЁТ ЕГО ДО 15 СЕКУНД.
                SetStaticProperty(typeof(KvXrPlatformModeSelector), "Applied", true);

                // --- ВАРИАНТ VR при ВКЛЮЧЁННОМ тумблере MR: сеанс НЕ начинается.
                SetStaticProperty(typeof(KvXrPlatformModeSelector), "Current", KvXrPlatformMode.Vr);
                PlayerPrefs.SetInt(KvXrSettings.KeyPassthrough, 1);
                PlayerPrefs.Save();
                Assert.IsTrue(KvXrSettings.GetPassthrough(),
                    "подготовка: тумблер MR включён — а режим всё равно VR");

                KvXrMrRoom room = roomGo.AddComponent<KvXrMrRoom>();
                RunStart(room);
                Assert.IsFalse(GetInstanceField<bool>(room, "mrSessionStarted"),
                    "режим устройства VR — сеанс MR начинать нельзя, даже если тумблер MR включён: " +
                    "решение принимается по фактическом режиме, а не по тумблеру");

                Object.DestroyImmediate(roomGo);
                roomGo = new GameObject("KvVrSwitchRuntime_room4b");

                // --- ВАРИАНТ MR при ВКЛЮЧЁННОМ тумблере: сеанс НАЧИНАЕТСЯ.
                SetStaticProperty(typeof(KvXrPlatformModeSelector), "Current", KvXrPlatformMode.Mr);
                room = roomGo.AddComponent<KvXrMrRoom>();
                RunStart(room);
                Assert.IsTrue(GetInstanceField<bool>(room, "mrSessionStarted"),
                    "режим устройства MR и включённый тумблер — сеанс обязан начаться: без этого " +
                    "«запуск в MR» молча оставлял бы оператора в VR");

                // Возврат в VR после MR-сеанса гасит то, что открыл старт.
                room.EndMrSession();
                Assert.IsFalse(GetInstanceField<bool>(room, "mrSessionStarted"),
                    "EndMrSession обязан снять признак сеанса");
            }
            finally
            {
                SetStaticPropertyIfPossible(typeof(KvXrPlatformModeSelector), "Current", modeBefore);
                SetStaticPropertyIfPossible(typeof(KvXrPlatformModeSelector), "Applied", appliedBefore);
                if (hadPassthrough) PlayerPrefs.SetInt(KvXrSettings.KeyPassthrough, passthroughBefore);
                else PlayerPrefs.DeleteKey(KvXrSettings.KeyPassthrough);
                PlayerPrefs.Save();

                Object.DestroyImmediate(roomGo);
                Object.DestroyImmediate(standGo);
                Object.DestroyImmediate(robotGo);
                DestroyLeftovers();
            }
        }

        // ==================================================== ВСПОМОГАТЕЛЬНОЕ

        /// <summary>Выполнить корутину `Start` до конца (режим в тесте уже определён).</summary>
        private static void RunStart(KvXrMrRoom room)
        {
            MethodInfo start = typeof(KvXrMrRoom).GetMethod("Start",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(start, "у KvXrMrRoom нет приватного Start — корутина переименована?");
            var routine = (IEnumerator)start.Invoke(room, null);
            Assert.IsNotNull(routine, "Start не вернул корутину");
            int guard = 0;
            while (routine.MoveNext())
            {
                if (++guard > 1000) Assert.Fail("корутина Start не завершилась за 1000 шагов");
            }
        }

        private static void SetStaticProperty(System.Type type, string name, object value)
        {
            PropertyInfo prop = type.GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(prop, "нет свойства " + type.Name + "." + name);
            MethodInfo setter = prop.GetSetMethod(true);
            Assert.IsNotNull(setter, "у свойства " + type.Name + "." + name + " нет сеттера");
            setter.Invoke(null, new[] { value });
        }

        private static void SetStaticPropertyIfPossible(System.Type type, string name, object value)
        {
            try { SetStaticProperty(type, name, value); }
            catch (AssertionException) { throw; }
            catch { }
        }

        private static void SetInstanceField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "нет поля " + target.GetType().Name + "." + name);
            field.SetValue(target, value);
        }

        private static T GetInstanceField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "нет поля " + target.GetType().Name + "." + name);
            return (T)field.GetValue(target);
        }

        private static void InvokeNonPublicStatic(System.Type type, string name, params object[] args)
        {
            MethodInfo method = type.GetMethod(name,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, "нет метода " + type.Name + "." + name);
            method.Invoke(null, args);
        }

        /// <summary>Вернуть статическое состояние пасс-слоя к тому, что было до теста.</summary>
        private static void RestorePassthroughStatics(bool hadFlag, int flagBefore,
            bool passthroughBefore, KvMrBackend backendBefore)
        {
            try
            {
                System.Reflection.FieldInfo flag = typeof(PassthroughFeature).GetField(
                    "_enableVideoSeeThrough", BindingFlags.NonPublic | BindingFlags.Static);
                if (hadFlag && flag != null) flag.SetValue(null, flagBefore);
            }
            catch { }
            SetStaticPropertyIfPossible(typeof(KvMrRuntime), "PassthroughOn", passthroughBefore);
            SetStaticPropertyIfPossible(typeof(KvMrRuntime), "Backend", backendBefore);
            SetStaticFieldIfPossible(typeof(KvMrRuntime), "backgroundSaved", false);
        }

        private static void SetStaticFieldIfPossible(System.Type type, string name, object value)
        {
            try
            {
                FieldInfo field = type.GetField(name,
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null) field.SetValue(null, value);
            }
            catch { }
        }

        /// <summary>Убрать объекты, созданные компонентами MR в ходе теста (в EditMode Awake не звался).</summary>
        private static void DestroyLeftovers()
        {
            string[] names =
            {
                KvXrMrRoom.WorldRootName, "KvMrSurfaces", "KvMrSurfacePainter",
                "KvMrPlacement", "KvMrRobotPick"
            };
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.parent != null) continue;
                for (int n = 0; n < names.Length; n++)
                {
                    if (t.name != names[n]) continue;
                    Object.DestroyImmediate(t.gameObject);
                    break;
                }
            }
        }
    }
}
