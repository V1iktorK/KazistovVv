// ================================================================================================
//  KvInputProvidersTests.cs — ТЕСТЫ ПОВЕДЕНИЯ ВВОДА (P4-8, третий круг приёмки 12.11.2026).
//
//  ОТКУДА ЗАДАЧА. Находка P4-8: для папки `01_Scripts/Input` (8 скриптов) не было НИ ОДНОГО теста,
//  при том что именно здесь жили два блокера: P0-4 (`GamepadInputProvider` читал необъявленные
//  legacy-кнопки и каждый кадр бросал ArgumentException, а `IsAvailable()` из-за этого выбирала
//  нерабочий провайдер) и P0-5 (`VRInputProvider` отдавал УРОВЕНЬ курка вместо ФРОНТА нажатия).
//  Проверялось это греп-тестами по исходнику — то есть проверялось, что «в файле написано
//  правильное слово», а не что код ведёт себя правильно (P4-3).
//
//  ЧТО ЗДЕСЬ. Только ПОВЕДЕНИЕ, без чтения исходников и без глянца:
//    1) `VRInputProvider.Edge` — фронт нажатия: одно нажатие = одно событие, сколько бы кадров
//       его ни удерживали. Это ровно тот контракт, который нарушал P0-5;
//    2) `InputDeviceType` ⟷ `InputManager.InputDevice` — явное соответствие и обратимость, а не
//       «числа совпали» (P3-9);
//    3) `MRInputProvider` — честно недоступен и не отдаёт действий: на нём держится отказ
//       `InputManager` выбирать нерабочий провайдер;
//    4) `InputManager.SetInputDevice` — РЕАЛЬНЫЙ вызов: недоступный провайдер не выбирается и не
//       записывается в `PlayerPrefs`, рабочий — выбирается и записывается (P3-10).
//       Именно этот пункт раньше проверялся только грепом по тексту.
// ================================================================================================

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Ввод: фронт нажатия, соответствие устройств и отказ от нерабочего провайдера.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvInputProvidersTests
    {
        // ================================================================ 1. ФРОНТ НАЖАТИЯ (P0-5)

        /// <summary>
        /// P0-5: ОДНО НАЖАТИЕ КУРКА — ОДНО СОБЫТИЕ, А НЕ ОДНО НА КАДР.
        ///
        /// ЧТО БЫЛО. `GetSelectDown()` возвращал уровень (`RightTrigger >= порог`), то есть был
        /// равен `GetSelectHeld()`. Любой вызывающий срабатывал каждый кадр удержания: живой пример
        /// — `CalibrationTool`, где одно длинное нажатие заполняло все три точки калибровки и
        /// применяло случайную калибровку робота.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Прогон «удержание N кадров» через ту самую функцию, которой пользуется
        /// провайдер: событие обязано быть РОВНО одно. На старом коде (уровень вместо фронта) здесь
        /// было бы N.
        /// </summary>
        [Test]
        public void VrProvider_OnePress_ProducesExactlyOneDownEvent()
        {
            bool wasHeld = false;
            int downs = 0;

            // Курок удерживают пять кадров подряд.
            for (int frame = 0; frame < 5; frame++)
            {
                bool heldNow = true;
                if (VRInputProvider.Edge(heldNow, wasHeld)) downs++;
                wasHeld = heldNow;
            }

            Assert.AreEqual(1, downs,
                "удержание курка дало " + downs + " нажатий вместо одного: «Down» снова равен " +
                "«Held» (регресс P0-5) — калибровка и запись сработают каждый кадр");

            // Второе нажатие после отпускания — снова ровно одно событие.
            wasHeld = false;  // отпустили
            if (VRInputProvider.Edge(false, wasHeld)) downs++;
            if (VRInputProvider.Edge(true, false)) downs++;
            if (VRInputProvider.Edge(true, true)) downs++;
            Assert.AreEqual(2, downs, "второе нажатие после отпускания не дало ровно одного события");
        }

        /// <summary>
        /// P0-5: ФРОНТ — ЭТО ПЕРЕХОД, А НЕ УРОВЕНЬ. Все четыре состояния перехода проверяются
        /// явно: именно по этой таблице видно, что «нажато» и «нажато только что» — разные вещи.
        /// </summary>
        [Test]
        public void VrProvider_EdgeIsATransition_NotALevel()
        {
            Assert.IsTrue(VRInputProvider.Edge(true, false),
                "переход «не нажато → нажато» обязан быть фронтом нажатия");
            Assert.IsFalse(VRInputProvider.Edge(true, true),
                "удержание не является новым нажатием — иначе действие повторяется каждый кадр");
            Assert.IsFalse(VRInputProvider.Edge(false, true),
                "отпускание не является нажатием");
            Assert.IsFalse(VRInputProvider.Edge(false, false),
                "отсутствие нажатия не является нажатием");
        }

        // ================================================================ 2. СООТВЕТСТВИЕ УСТРОЙСТВ (P3-9)

        /// <summary>
        /// P3-9: ПЕРЕЧИСЛЕНИЯ УСТРОЙСТВ СООТВЕТСТВУЮТ ПО ИМЕНАМ И ОБРАТИМЫ.
        ///
        /// ЧТО БЫЛО. Между `InputDeviceType` и `InputManager.InputDevice` не было ни одного вызова
        /// конвертера: `SettingsMenu` делал сырой каст `(InputManager.InputDevice)v`, и соответствие
        /// держалось только на совпадении чисел. Стоило вставить элемент в любое перечисление — и
        /// «Геймпад» в списке включал бы MR-провайдер: молча, без исключения, оператор без ввода.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Для КАЖДОГО значения: конвертация туда и обратно возвращает исходное, и
        /// каждая пара названа одинаково. Плюс неизвестное значение не превращается в «первое
        /// попавшееся»: возвращается клавиатура с мышью — она доступна всегда.
        /// </summary>
        [Test]
        public void DeviceTypeConverters_AreExplicitAndRoundTrip()
        {
            InputDeviceType[] values = (InputDeviceType[])Enum.GetValues(typeof(InputDeviceType));
            Assert.GreaterOrEqual(values.Length, 5,
                "в InputDeviceType меньше пяти устройств — проверка была бы неполной");

            foreach (InputDeviceType value in values)
            {
                InputManager.InputDevice asManager = value.ToInputManagerDevice();
                Assert.AreEqual(value.ToString(), asManager.ToString(),
                    "«" + value + "» превратилось в «" + asManager + "»: соответствие устройств " +
                    "снова держится на числах, а не на именах (P3-9)");

                InputDeviceType back = asManager.ToInputDeviceType();
                Assert.AreEqual(value, back,
                    "обратная конвертация «" + value + "» дала «" + back + "» — соответствие необратимо");
            }

            // Неизвестное значение: честный откат на клавиатуру с мышью, а не «что попало».
            Assert.AreEqual(InputManager.InputDevice.KeyboardMouse,
                ((InputDeviceType)99).ToInputManagerDevice(),
                "неизвестное устройство не откатилось на клавиатуру с мышью");
            Assert.AreEqual(InputDeviceType.KeyboardMouse,
                ((InputManager.InputDevice)99).ToInputDeviceType(),
                "неизвестное устройство не откатилось на клавиатуру с мышью");
        }

        // ================================================================ 3. НЕРАБОЧИЙ ПРОВАЙДЕР

        /// <summary>
        /// MR-ПРОВАЙДЕР ЧЕСТНО СООБЩАЕТ, ЧТО НЕ УМЕЕТ НИЧЕГО.
        ///
        /// На этом держится всё лечение P3-10: `InputManager` отказывается выбирать провайдер с
        /// `IsAvailable() == false`. Если однажды `MRInputProvider` начнёт врать про доступность,
        /// отказ перестанет срабатывать, и приложение останется без ввода — без единой ошибки.
        /// </summary>
        [Test]
        public void MrProvider_ReportsUnavailable_AndGivesNoActions()
        {
            GameObject go = new GameObject("MrProviderProbe");
            MRInputProvider provider = null;
            try
            {
                provider = go.AddComponent<MRInputProvider>();
                Assert.IsNotNull(provider, "MRInputProvider не создаётся — проверка недоступна");

                Assert.IsFalse(provider.IsAvailable(),
                    "MR-провайдер объявил себя доступным, не умея ни одного действия (P3-10)");
                Assert.IsFalse(provider.GetSelectDown(), "MR-провайдер отдал нажатие выбора");
                Assert.IsFalse(provider.GetGrabDown(), "MR-провайдер отдал нажатие захвата");
                Assert.IsFalse(provider.GetSwitchRobotDown(), "MR-провайдер отдал смену робота");
                Assert.IsFalse(provider.GetRecordDown(), "MR-провайдер отдал запись");
                Assert.IsFalse(provider.GetPlayDown(), "MR-провайдер отдал пуск");
                Assert.AreEqual(Vector2.zero, provider.GetMovement(), "MR-провайдер отдал перемещение");
                Assert.AreEqual(Vector2.zero, provider.GetRotation(), "MR-провайдер отдал обзор");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ================================================================ 4. ВЫБОР УСТРОЙСТВА (P3-10)

        /// <summary>
        /// P3-10: НЕРАБОЧИЙ ПРОВАЙДЕР НЕ ВЫБИРАЕТСЯ И НЕ ЗАПИСЫВАЕТСЯ В `PlayerPrefs`.
        ///
        /// ЧТО БЫЛО. `SetInputDevice(InputDevice.MR)` выставлял `mrProvider`, который возвращает
        /// `false`/`zero` во всех действиях. Приложение оставалось без ввода (ни осмотра, ни
        /// выбора, ни телеоперации) и без единой ошибки в консоли, потому что провайдер формально
        /// существует. Хуже того, выбор писался в `PlayerPrefs` — нерабочее закреплялось на
        /// следующие запуски.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. РЕАЛЬНЫЙ вызов `SetInputDevice(MR)` на живом компоненте: активным
        /// остаётся рабочий провайдер, а `PlayerPrefs` не получает значение MR.
        /// </summary>
        [Test]
        public void InputManager_DoesNotSelectUnavailableProvider()
        {
            const string prefsKey = "InputDevice";
            int savedPrefs = PlayerPrefs.GetInt(prefsKey, -1);
            GameObject go = null;
            try
            {
                PlayerPrefs.DeleteKey(prefsKey);
                go = new GameObject("InputManagerProbe");
                InputManager manager = go.AddComponent<InputManager>();
                Assert.IsNotNull(manager, "InputManager не создаётся — проверка недоступна");
                EnsureProviders(manager);

                // 1. Нерабочий провайдер отклоняется, а вместо пустоты берётся рабочий.
                manager.SetInputDevice(InputManager.InputDevice.MR);
                Assert.IsNotNull(manager.ActiveProvider,
                    "после выбора MR приложение осталось БЕЗ ВВОДА (P3-10)");
                Assert.IsFalse(manager.ActiveProvider is MRInputProvider,
                    "активным выбран MR-провайдер, который не умеет ни одного действия (P3-10)");
                Assert.IsTrue(manager.ActiveProvider.IsAvailable(),
                    "активным оказался недоступный провайдер — оператор без управления (P3-10)");

                // 2. Нерабочий выбор НЕ записан: иначе он же станет стартовым в следующий раз.
                Assert.AreNotEqual((int)InputManager.InputDevice.MR, PlayerPrefs.GetInt(prefsKey, -1),
                    "нерабочий выбор MR записан в PlayerPrefs и закрепится на следующие запуски (P3-10)");

                // 3. Рабочий провайдер выбирается и запоминается — иначе настройка бессмысленна.
                InputProvider before = manager.ActiveProvider;
                manager.SetInputDevice(InputManager.InputDevice.KeyboardMouse);
                Assert.IsTrue(manager.ActiveProvider is KeyboardMouseInputProvider,
                    "рабочий провайдер «клавиатура с мышью» не выбрался");
                Assert.AreSame(before, manager.ActiveProvider,
                    "выбор того же устройства пересоздал провайдер: сбросились бы его сглаживание и " +
                    "состояние фронтов нажатий");
                Assert.AreEqual((int)InputManager.InputDevice.KeyboardMouse,
                    PlayerPrefs.GetInt(prefsKey, -1),
                    "рабочий выбор не сохранён — после перезапуска устройство выберется заново");

                // 4. И обратный случай: недоступный VR не вытесняет уже работающий ввод.
                manager.SetInputDevice(InputManager.InputDevice.VR);
                Assert.IsTrue(manager.ActiveProvider is KeyboardMouseInputProvider,
                    "недоступный VR-провайдер вытеснил рабочий ввод — оператор остался без управления");
                Assert.AreEqual((int)InputManager.InputDevice.KeyboardMouse,
                    PlayerPrefs.GetInt(prefsKey, -1),
                    "после отказа от VR в PlayerPrefs остался нерабочий выбор");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                if (savedPrefs == -1) PlayerPrefs.DeleteKey(prefsKey);
                else PlayerPrefs.SetInt(prefsKey, savedPrefs);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// СОЗДАТЬ ПРОВАЙДЕРОВ НА ЖИВОМ КОМПОНЕНТЕ.
        ///
        /// `EnsureProvidersExist` — приватный и вызывается из `Start`, а `Start` в EditMode не
        /// выполняется. Звать его отражением честнее, чем копировать создание провайдеров в тест:
        /// копия разошлась бы с настоящим кодом, и проверка перестала бы что-либо доказывать.
        /// </summary>
        private static void EnsureProviders(InputManager manager)
        {
            MethodInfo method = typeof(InputManager).GetMethod("EnsureProvidersExist",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method,
                "в InputManager нет метода EnsureProvidersExist — проверка выбора устройства " +
                "потеряла способ создать провайдеров");
            method.Invoke(manager, null);
        }
    }
}
