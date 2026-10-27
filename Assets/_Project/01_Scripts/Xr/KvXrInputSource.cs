using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace KazistovVvXr
{
    /// <summary>
    /// СОСТОЯНИЕ КОНТРОЛЛЕРОВ И ГОЛОВЫ (сессия §28, этап 2 плана XR).
    ///
    /// ЗАЧЕМ. Позы, курок и стик надо читать из ОДНОГО места: и ввод для робота
    /// (<c>VRInputProvider</c>), и перемещение (<c>KvXrLocomotion</c>), и луч по интерфейсу
    /// (<c>KvXrUiPointer</c>) должны видеть одну и ту же картину кадра. Иначе получается
    /// классический дефект VR: «курок нажал два действия» — ровно та болезнь, которую
    /// в §27 лечили на десктопе арбитражем клавиш.
    ///
    /// ПОЧЕМУ НЕ XRI. Проект обходится без компонентов XR Interaction Toolkit: они требуют
    /// настроенного префаба рига, а собрать и проверить его без шлема нечем. Здесь читается
    /// сам <see cref="InputSystem"/> — он уже подключён (пакет 1.20.0, только new Input System),
    /// и устройство PICO 4 Ultra приходит через OpenXR тем же способом, что и любое XR-устройство.
    ///
    /// ЧЕСТНОЕ ОГРАНИЧЕНИЕ: имена устройств у PICO отличаются от Quest/Vive, поэтому поиск
    /// идёт ПО ВОЗМОЖНОСТЯМ (есть стик / есть курок / есть поза), а не по имени. Это работает
    /// до тех пор, пока платформа отдаёт контроллер как XRController, что и требуется OpenXR.
    /// </summary>
    public static class KvXrInputSource
    {
        /// <summary>Сколько кадров подряд состояние устройства считается «свежим» (защита от «залипших» данных).</summary>
        private static readonly List<InputDevice> controllers = new List<InputDevice>();
        private static XRController right;
        private static XRController left;
        private static XRHMD head;
        private static float lastScanTime = -100f;

        /// <summary>Есть ли живой шлем: устройство головы найдено И XR-подсистема работает.</summary>
        public static bool HeadsetPresent
        {
            get
            {
                EnsureScanned();
                return head != null && head.added;
            }
        }

        /// <summary>Голова (шлем). null — шлема нет.</summary>
        public static XRHMD Head { get { EnsureScanned(); return head; } }

        /// <summary>Правый контроллер. null — не найден.</summary>
        public static XRController Right { get { EnsureScanned(); return right; } }

        /// <summary>Левый контроллер. null — не найден.</summary>
        public static XRController Left { get { EnsureScanned(); return left; } }

        /// <summary>Поза правого контроллера (позиция, поворот) и признак её достоверности.</summary>
        public static bool TryRightPose(out Vector3 position, out Quaternion rotation)
        {
            return TryPose(right, out position, out rotation);
        }

        /// <summary>Поза левого контроллера.</summary>
        public static bool TryLeftPose(out Vector3 position, out Quaternion rotation)
        {
            return TryPose(left, out position, out rotation);
        }

        /// <summary>
        /// ПОЗА УКАЗАНИЯ ПРАВОГО КОНТРОЛЛЕРА — та, вдоль которой контроллер «смотрит».
        ///
        /// ЗАЧЕМ ОТДЕЛЬНО ОТ ПОЗЫ КИСТИ (сессия §36). OpenXR отдаёт у контроллера ДВЕ позы:
        /// позу кисти (`grip`) и позу указания (`aim`). Панель вешается у кисти, а луч обязан
        /// идти по указанию: иначе человек целится «как держит», а попадает мимо, и это
        /// выглядит как «интерфейс не нажимается». Если профиль позы указания не дал,
        /// честно откатываемся на позу кисти — но не выдумываем третью.
        /// </summary>
        public static bool TryRightAimPose(out Vector3 position, out Quaternion rotation)
        {
            return TryAimPose(right, out position, out rotation);
        }

        /// <summary>Поза указания левого контроллера.</summary>
        public static bool TryLeftAimPose(out Vector3 position, out Quaternion rotation)
        {
            return TryAimPose(left, out position, out rotation);
        }

        /// <summary>Поза указания устройства; без неё — поза кисти (см. описание выше).</summary>
        private static bool TryAimPose(XRController controller, out Vector3 position, out Quaternion rotation)
        {
            EnsureScanned();
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (controller != null && controller.added)
            {
                PoseControl pointer = controller.TryGetChildControl<PoseControl>("pointer");
                if (pointer != null)
                {
                    PoseState state = pointer.ReadValue();
                    if (state.isTracked || state.position != Vector3.zero)
                    {
                        position = state.position;
                        rotation = state.rotation;
                        return true;
                    }
                }
            }

            return TryPose(controller, out position, out rotation);
        }

        /// <summary>Есть ли у правого контроллера поза указания (для журнала и проверок).</summary>
        public static bool HasRightAim { get { EnsureScanned(); return right != null && HasControl(right, "pointer"); } }

        /// <summary>Есть ли у левого контроллера поза указания.</summary>
        public static bool HasLeftAim { get { EnsureScanned(); return left != null && HasControl(left, "pointer"); } }

        // ==================================================== §37: РУКА ЛУЧА — НАСТРОЙКА
        //
        // ЗАДАНИЕ ВЛАДЕЛЬЦА [S1.3]: «Сейчас луч идёт из левой руки, а мне нужно наоборот: правая
        // рука — основная, из неё луч, ею я нажимаю… Руку для луча нужно сделать настройкой
        // (правой/левой), по умолчанию — правой».
        //
        // ЧТО БЫЛО ДО §37: `KvXrUiPointer` звал `TryRightAimPose` напрямую, то есть рука луча была
        // ВШИТА в код. Здесь появляется одна точка выбора: все, кому нужна «рука луча» (указатель,
        // телепорт, жесты), спрашивают её ЗДЕСЬ, а не решают каждое по-своему — иначе через месяц
        // луч шёл бы из одной руки, а нажатие читалось бы с другой.

        /// <summary>Из какой руки идёт луч: файл подстройки → иначе настройка (по умолчанию правая).</summary>
        public static KvXrQuickHand RayHand { get { return KvXrTuning.RayHand; } }

        /// <summary>Рука луча словами — для строки журнала («правая»/«левая»).</summary>
        public static string RayHandName { get { return RayHand == KvXrQuickHand.Right ? "правая" : "левая"; } }

        /// <summary>Контроллер указанной руки (null — контроллера нет).</summary>
        public static XRController Controller(KvXrQuickHand hand)
        {
            EnsureScanned();
            return hand == KvXrQuickHand.Left ? left : right;
        }

        /// <summary>Определён ли контроллер этой руки.</summary>
        public static bool HasHand(KvXrQuickHand hand) { return Controller(hand) != null; }

        /// <summary>ПОЗА УКАЗАНИЯ руки, из которой идёт луч.</summary>
        public static bool TryRayAimPose(out Vector3 position, out Quaternion rotation)
        {
            return TryAimPose(Controller(RayHand), out position, out rotation);
        }

        /// <summary>ПОЗА КИСТИ (grip) руки, из которой идёт луч.</summary>
        public static bool TryRayPose(out Vector3 position, out Quaternion rotation)
        {
            return TryPose(Controller(RayHand), out position, out rotation);
        }

        /// <summary>Поза указания конкретной руки (для жестов и диагностики).</summary>
        public static bool TryAimPose(KvXrQuickHand hand, out Vector3 position, out Quaternion rotation)
        {
            return TryAimPose(Controller(hand), out position, out rotation);
        }

        /// <summary>Поза кисти конкретной руки.</summary>
        public static bool TryPose(KvXrQuickHand hand, out Vector3 position, out Quaternion rotation)
        {
            return TryPose(Controller(hand), out position, out rotation);
        }

        /// <summary>
        /// ПОЗА С ПРОВЕРКОЙ ОТСЛЕЖИВАНИЯ — для МОДЕЛЕЙ, которые нельзя ставить «на глаз».
        ///
        /// ПОЧЕМУ ОТДЕЛЬНЫЙ МЕТОД. Ловушка проекта [F2]: путь `devicePosition + deviceRotation`
        /// возвращает `true` ДАЖЕ ПРИ НУЛЕВЫХ значениях — то есть «поза есть» и «поза (0,0,0)»
        /// выглядят одинаково. Модель контроллера, поставленная по такой позе, оказалась бы в
        /// начале координат (на полу) и висела бы там — ровно то «свет в воздухе», которого
        /// владелец не хочет. Здесь сначала читается признак `isTracked` собранной позы, и только
        /// если его нет вовсе — работает прежний путь с проверкой на ноль.
        /// </summary>
        public static bool TryTrackedPose(KvXrQuickHand hand, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            XRController controller = Controller(hand);
            if (controller == null || !controller.added) return false;

            PoseControl pose = controller.TryGetChildControl<PoseControl>("devicePose");
            if (pose != null)
            {
                PoseState state = pose.ReadValue();
                if (!state.isTracked) return false;          // ← отказ по признаку, а не по нулю
                position = state.position;
                rotation = state.rotation;
                return true;
            }

            if (!TryPose(controller, out position, out rotation)) return false;
            return position.sqrMagnitude > 0.0001f;          // позы без признака: ноль отвергаем
        }

        /// <summary>Курок руки, из которой идёт луч (0…1).</summary>
        public static float RayTrigger
        {
            get { return Axis(ReadTrigger(Controller(RayHand))); }
        }

        /// <summary>Грип руки, из которой идёт луч (0…1).</summary>
        public static float RayGrip
        {
            get { return Axis(ReadGrip(Controller(RayHand))); }
        }

        /// <summary>Стик руки, из которой идёт луч.</summary>
        public static Vector2 RayStick
        {
            get { return Stick(Controller(RayHand)); }
        }

        // ==================================================== §37: СТОРОНЫ РУК ПО ГЕОМЕТРИИ
        //
        // ПОЧЕМУ ЭТО ПОЯВИЛОСЬ. На живом шлеме 21.10.2026 владелец сказал «контроллеры перепутаны
        // местами», и журнал это подтвердил числами: устройство, названное рантаймом ПРАВЫМ, стояло
        // на 5 см ЛЕВЕЕ оси взгляда, а «левое» — на 6 см правее. До этой правки проект верил метке
        // (`handedness`), поэтому модель правого контроллера появлялась в левой руке, а жесты правой
        // руки делали то, что положено левой.
        //
        // ЧТО ЗДЕСЬ СДЕЛАНО: сторона определяется по положению руки относительно ГОЛОВЫ
        // (`KvXrSideResolver`), решение принимается по нескольким кадрам и ЗАПОМИНАЕТСЯ. Метка
        // рантайма остаётся как первое приближение, но больше не является истиной.

        private static bool sidesLocked;
        private static bool sidesWereSwapped;
        private static bool manualSidesActive;
        private static int lockedRightDeviceId = -1;
        private static int lockedLeftDeviceId = -1;
        private static int sideVotes;
        private static int sideVerdict;

        /// <summary>Стороны определены (автоматикой или ручным переключателем).</summary>
        public static bool SidesLocked { get { return sidesLocked; } }

        /// <summary>Правая и левая назначены не так, как их назвал рантайм.</summary>
        public static bool SidesWereSwapped { get { return sidesWereSwapped; } }

        /// <summary>Стороны заданы ручным переключателем `sides.swap` (автоматика молчит).</summary>
        public static bool ManualSidesActive { get { return manualSidesActive; } }

        /// <summary>
        /// ОПРЕДЕЛИТЬ СТОРОНЫ (зовётся каждый кадр).
        ///
        /// ПОРЯДОК РЕШЕНИЙ:
        ///   1) РУЧНОЙ ПЕРЕКЛЮЧАТЕЛЬ `sides.swap` в файле подстройки — он сильнее всего: владелец
        ///      видит шлем своими глазами, и спорить с ним нечем;
        ///   2) АВТОМАТИКА ПО ГЕОМЕТРИИ: положение руки относительно головы, решение по 20 кадрам,
        ///      с пересмотром, если позже руки развели явно в разные стороны (≥ 5 см).
        /// </summary>
        public static void ResolveSides()
        {
            // 1. РУЧНОЙ ПЕРЕКЛЮЧАТЕЛЬ.
            bool? manual = KvXrTuning.SidesSwap;
            if (manual.HasValue)
            {
                if (!manualSidesActive || sidesWereSwapped != manual.Value)
                {
                    // Пересобрать назначение с нуля: сначала как назвал рантайм, затем (если надо) поменять.
                    Scan();
                    if (manual.Value) SwapRightLeft();
                    sidesWereSwapped = manual.Value;
                    manualSidesActive = true;
                    sidesLocked = true;
                    lockedRightDeviceId = right != null ? right.deviceId : -1;
                    lockedLeftDeviceId = left != null ? left.deviceId : -1;
                    Debug.Log("[KvXr] СТОРОНЫ РУК: включён РУЧНОЙ переключатель sides.swap=" +
                              (manual.Value ? "1" : "0") + " · стороны " +
                              (manual.Value ? "ПОМЕНЯНЫ местами" : "как назвал рантайм") +
                              " · правая " + (right != null ? right.name : "нет") +
                              " · левая " + (left != null ? left.name : "нет"));
                }
                return;
            }

            if (manualSidesActive)
            {
                // Переключатель убрали из файла — вернуть автоматику.
                manualSidesActive = false;
                UnlockSides();
            }

            if (right == null || left == null) return;

            Vector3 headPosition;
            Quaternion headRotation;
            if (!TryPoseQuiet(head, out headPosition, out headRotation)) return;

            Vector3 rightPosition;
            Vector3 leftPosition;
            Quaternion ignored;
            if (!TryPoseQuiet(right, out rightPosition, out ignored)) return;
            if (!TryPoseQuiet(left, out leftPosition, out ignored)) return;

            Vector3 headRight = headRotation * Vector3.right;
            int sideOfRight = KvXrSideResolver.SideOf(rightPosition, headPosition, headRight);
            int sideOfLeft = KvXrSideResolver.SideOf(leftPosition, headPosition, headRight);

            int verdict;
            bool swappedByGeometry = KvXrSideResolver.LabelsAreSwapped(sideOfRight, sideOfLeft);
            bool confirmedByGeometry = KvXrSideResolver.LabelsConfirmed(sideOfRight, sideOfLeft);
            if (swappedByGeometry) verdict = -1;
            else if (confirmedByGeometry) verdict = 1;
            else return;                                   // судить нельзя — ничего не меняем

            // ПЕРЕСМОТР ЗАКРЕПЛЁННОГО РЕШЕНИЯ (21.10.2026). Если стороны уже закреплены, но руки
            // развели ЯВНО в разные стороны и картина противоположна — прежнее решение было ошибкой.
            // Требование «явно» (5 см против 1,5 см при первом решении) защищает от дребезга.
            if (sidesLocked)
            {
                float projectionRight = KvXrSideResolver.ProjectionMeters(rightPosition, headPosition, headRight);
                float projectionLeft = KvXrSideResolver.ProjectionMeters(leftPosition, headPosition, headRight);
                bool stronglyOpposite =
                    Mathf.Abs(projectionRight) >= KvXrSideResolver.RelockMeters &&
                    Mathf.Abs(projectionLeft) >= KvXrSideResolver.RelockMeters;

                bool currentIsWrong = (sidesWereSwapped && !swappedByGeometry) ||
                                      (!sidesWereSwapped && swappedByGeometry);
                if (!stronglyOpposite || !currentIsWrong) return;

                Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[KvXr] СТОРОНЫ РУК: ПЕРЕСМОТР — руки разведены на {0:0.00} м и {1:0.00} м от оси взгляда, " +
                    "картина противоположна прежнему решению · стороны {2}",
                    projectionRight, projectionLeft, swappedByGeometry ? "поменяны" : "возвращены как назвал рантайм"));

                SwapRightLeft();
                sidesWereSwapped = swappedByGeometry;
                lockedRightDeviceId = right != null ? right.deviceId : -1;
                lockedLeftDeviceId = left != null ? left.deviceId : -1;
                return;
            }

            if (verdict != sideVerdict)
            {
                sideVerdict = verdict;
                sideVotes = 0;
            }
            sideVotes++;
            if (sideVotes < KvXrSideResolver.VotesToLock) return;

            float rightProjection = KvXrSideResolver.ProjectionMeters(rightPosition, headPosition, headRight);
            float leftProjection = KvXrSideResolver.ProjectionMeters(leftPosition, headPosition, headRight);

            if (verdict < 0)
            {
                SwapRightLeft();
                sidesWereSwapped = true;
            }

            sidesLocked = true;
            lockedRightDeviceId = right != null ? right.deviceId : -1;
            lockedLeftDeviceId = left != null ? left.deviceId : -1;

            if (sidesWereSwapped)
            {
                Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[KvXr] СТОРОНЫ РУК: метки рантайма НЕ СОВПАЛИ с положением — «правый» оказался на " +
                    "{0:0.00} м от оси взгляда (слева), «левый» на {1:0.00} м (справа) · стороны " +
                    "исправлены по геометрии · правая рука — {2} · левая — {3} · {4}",
                    rightProjection, leftProjection,
                    right != null ? right.name : "нет", left != null ? left.name : "нет",
                    DescribeHandedness()));
            }
            else
            {
                Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[KvXr] СТОРОНЫ РУК: метки рантайма совпали с положением — правая на {0:0.00} м " +
                    "вправо от оси взгляда, левая на {1:0.00} м · проверено {2} кадров · {3}",
                    rightProjection, leftProjection, sideVotes, DescribeHandedness()));
            }
        }

        /// <summary>
        /// ЧТО ИМЕННО ГОВОРИТ РАНТАЙМ О РУКАХ: метка `handedness` и метки-использования (`usages`).
        /// Две строки доказательства в одну: если они противоречат друг другу, это видно сразу.
        /// </summary>
        public static string DescribeHandedness()
        {
            return "метки: правая [" + DescribeDevice(right) + "] · левая [" + DescribeDevice(left) + "]";
        }

        private static string DescribeDevice(XRController controller)
        {
            if (controller == null) return "нет устройства";

            string handedness = "—";
            IntegerControl control = controller.TryGetChildControl<IntegerControl>("handedness");
            if (control != null) handedness = control.ReadValue().ToString();

            string usages = "нет usages";
            var usageList = controller.usages;
            for (int i = 0; i < usageList.Count; i++)
            {
                if (i == 0) usages = string.Empty;
                else usages += ",";
                usages += usageList[i].ToString();
            }

            return "handedness=" + handedness + " · usages=" + usages;
        }

        /// <summary>Поменять правую и левую местами (одно место, чтобы не разошлось по коду).</summary>
        private static void SwapRightLeft()
        {
            XRController swap = right;
            right = left;
            left = swap;
        }

        /// <summary>Снять закрепление сторон (после пробуждения шлема устройства могут быть другими).</summary>
        public static void UnlockSides()
        {
            sidesLocked = false;
            sideVotes = 0;
            sideVerdict = 0;
            manualSidesActive = false;
            lockedRightDeviceId = -1;
            lockedLeftDeviceId = -1;
        }

        /// <summary>
        /// СЛЕЖЕНИЕ ЗА ПОЗОЙ РУКИ ЛУЧА (сессия §37). Владелец сообщил: «после ухода в спящий режим
        /// указка на контроллерах не восстанавливается и веер тоже». Причину поймать не удалось —
        /// в журнале того сеанса сна не было. Чтобы в следующий раз причина была ВИДНА, здесь раз в
        /// 10 секунд печатается состояние: сколько секунд нет позы руки и что видно про устройства.
        /// </summary>
        private static void WatchRayPose()
        {
            Vector3 position;
            Quaternion rotation;
            bool haveRay = TryRayAimPose(out position, out rotation);

            if (haveRay)
            {
                if (rayPoseMissingSeconds >= 1f)
                {
                    Debug.Log("[KvXr] Указатель: поза руки ВЕРНУЛАСЬ через " +
                              rayPoseMissingSeconds.ToString("0.0") + " с · " + DescribeSides());
                }
                rayPoseMissingSeconds = 0f;
                nextRayPoseReport = Time.realtimeSinceStartup + 10f;
                return;
            }

            rayPoseMissingSeconds += Time.deltaTime;
            if (Time.realtimeSinceStartup < nextRayPoseReport) return;
            nextRayPoseReport = Time.realtimeSinceStartup + 10f;

            Debug.Log("[KvXr] Указатель: позы руки нет " + rayPoseMissingSeconds.ToString("0.0") + " с · " +
                      "рука луча " + (RayHand == KvXrQuickHand.Right ? "правая" : "левая") + " · " +
                      "шлем " + (head != null && head.added ? "есть" : "НЕТ") + " · " +
                      "правый " + (right != null ? "есть" : "НЕТ") + " · " +
                      "левый " + (left != null ? "есть" : "НЕТ") + " · " + DescribeSides());
        }

        private static float rayPoseMissingSeconds;
        private static float nextRayPoseReport;

        /// <summary>Строка состояния сторон для журнала и отчёта.</summary>
        public static string DescribeSides()        {
            if (manualSidesActive)
                return "стороны рук: РУЧНОЙ переключатель sides.swap=" + (sidesWereSwapped ? "1" : "0") +
                       " · правая " + (right != null ? right.name : "нет") +
                       " · левая " + (left != null ? left.name : "нет");

            if (!sidesLocked) return "стороны рук: ещё не определены (нужны две руки в стороны от взгляда либо sides.swap)";
            return "стороны рук: " + (sidesWereSwapped ? "ИСПРАВЛЕНЫ по геометрии" : "по меткам рантайма") +
                   " · правая " + (right != null ? right.name : "нет") +
                   " · левая " + (left != null ? left.name : "нет");
        }

        /// <summary>Поза устройства БЕЗ повторного сканирования (чтобы не было рекурсии из Scan).</summary>
        private static bool TryPoseQuiet(InputDevice device, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (device == null || !device.added) return false;

            PoseControl pose = device.TryGetChildControl<PoseControl>("devicePose");
            if (pose == null) return false;

            PoseState state = pose.ReadValue();
            if (!state.isTracked) return false;
            position = state.position;
            rotation = state.rotation;
            return true;
        }

        /// <summary>Восстановить закреплённые стороны после пересканирования устройств.</summary>
        private static void ApplyLockedSides()
        {
            if (!sidesLocked) return;

            XRController lockedRight = FindById(lockedRightDeviceId);
            XRController lockedLeft = FindById(lockedLeftDeviceId);
            if (lockedRight == null || lockedLeft == null)
            {
                // Устройства пересозданы (пробуждение шлема) — решаем заново, а не держим мёртвые ссылки.
                UnlockSides();
                return;
            }
            right = lockedRight;
            left = lockedLeft;
        }

        private static XRController FindById(int deviceId)
        {
            for (int i = 0; i < controllers.Count; i++)
            {
                InputDevice device = controllers[i];
                if (device != null && device.deviceId == deviceId) return device as XRController;
            }
            return null;
        }


        /// <summary>
        /// НИЖЕ ЭТОЙ ВЫСОТЫ ПОЗА ГОЛОВЫ СЧИТАЕТСЯ НЕДОСТОВЕРНОЙ, метры.
        ///
        /// НАЙДЕНО НА ЖИВОМ ШЛЕМЕ 23.10.2026 (сборка §38): в журнале появилось
        ///     `Камера следует за головой: поза (0.00, 0.00, 0.00) · высота 0,00 м · поворот (0, 0, 0)`
        /// — то есть поза головы пришла НУЛЕВОЙ и была принята как достоверная (та же ловушка [F2],
        /// что и у контроллеров). Последствия были тяжёлыми и выглядели как три разных дефекта:
        /// камера уезжала на пол, панель «у пояса» считалась от головы на высоте 0 и больше не
        /// возвращалась к руке, а проверка «рука в поле зрения» отвергала ВСЕ жесты.
        ///
        /// Человек не может стоять на высоте 0,30 м над полом — значит такая поза не настоящая.
        /// </summary>
        public const float MinPlausibleHeadHeight = 0.30f;

        /// <summary>
        /// Годится ли поза головы: высота не ниже <see cref="MinPlausibleHeadHeight"/>.
        /// Чистая функция — проверяется тестом без шлема.
        /// </summary>
        public static bool IsPlausibleHeadPosition(Vector3 position)
        {
            return position.y >= MinPlausibleHeadHeight;
        }

        /// <summary>Поза головы (для панели и проверки «шлем надет»). Нулевая поза ОТВЕРГАЕТСЯ.</summary>
        public static bool TryHeadPose(out Vector3 position, out Quaternion rotation)
        {
            EnsureScanned();
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (head == null || !head.added) return false;
            if (!TryPose(head, out position, out rotation)) return false;

            if (!IsPlausibleHeadPosition(position))
            {
                // Ловушка [F2]: «поза есть» и «поза ноль» выглядели одинаково. Теперь — отказ.
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }
            return true;
        }

        /// <summary>Курок (trigger) правого контроллера: 0…1. Нет контроллера — 0.</summary>
        public static float RightTrigger { get { return Axis(ReadTrigger(right)); } }

        /// <summary>Курок левого контроллера: 0…1.</summary>
        public static float LeftTrigger { get { return Axis(ReadTrigger(left)); } }

        /// <summary>Грип (боковая кнопка) правого контроллера: 0…1.</summary>
        public static float RightGrip { get { return Axis(ReadGrip(right)); } }

        /// <summary>Грип левого контроллера: 0…1.</summary>
        public static float LeftGrip { get { return Axis(ReadGrip(left)); } }

        /// <summary>Стик правого контроллера (поворот/телепорт).</summary>
        public static Vector2 RightStick { get { return Stick(right); } }

        /// <summary>Стик левого контроллера (движение/телепорт).</summary>
        public static Vector2 LeftStick { get { return Stick(left); } }

        /// <summary>Определён ли правый контроллер (для честного отчёта «контроллер не найден»).</summary>
        public static bool HasRight { get { EnsureScanned(); return right != null; } }

        /// <summary>Определён ли левый контроллер.</summary>
        public static bool HasLeft { get { EnsureScanned(); return left != null; } }

        /// <summary>Строка состояния для журнала: что найдено, что нет — без домыслов.</summary>
        public static string Describe()
        {
            EnsureScanned();
            return string.Format("XR-ввод: шлем {0} · правый контроллер {1} · левый контроллер {2} · устройств в системе {3}",
                head != null ? "есть" : "НЕТ",
                right != null ? "есть" : "нет",
                left != null ? "есть" : "нет",
                controllers.Count);
        }

        // ------------------------------------------------------------------ внутреннее

        private static float Axis(float value)
        {
            return Mathf.Clamp01(value);
        }

        /// <summary>
        /// ИМЕНА УПРАВЛЯЮЩИХ ЭЛЕМЕНТОВ, которые считаются курком. Порядок = порядок проверки:
        /// сначала богатый профиль (Oculus Touch — `trigger`), затем простой (KHR simple — `select`).
        ///
        /// ПОЧЕМУ ЭТО ВАЖНО. Для Android в проекте включён `KHRSimpleControllerProfile` (§31),
        /// а у него НЕТ `trigger` — есть `select`. Пока в списке не было `select`, курок на таком
        /// профиле не срабатывал бы вовсе, и это выглядело бы как «контроллеры не работают»,
        /// хотя устройства в системе есть.
        /// </summary>
        public static readonly string[] TriggerControlNames =
            { "triggerPressed", "trigger", "triggerButton", "selectPressed", "select", "primaryButton" };

        /// <summary>Имена элементов «грип» (боковая кнопка или сжатие).</summary>
        public static readonly string[] GripControlNames =
            { "gripPressed", "grip", "squeezePressed", "squeeze" };

        /// <summary>Имена элементов «стик» (`primary2DAxis` — имя из простых профилей OpenXR).</summary>
        public static readonly string[] StickControlNames = { "thumbstick", "primary2DAxis" };

        /// <summary>
        /// ПЕРВОЕ ИМЯ ИЗ СПИСКА, КОТОРОЕ ЕСТЬ У УСТРОЙСТВА. Логика чистая (принимает любую функцию
        /// «есть ли такое имя»), поэтому проверяется тестами без шлема и без контроллеров.
        /// </summary>
        public static string FirstPresent(string[] names, Func<string, bool> has)
        {
            if (names == null || has == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i])) continue;
                if (has(names[i])) return names[i];
            }
            return null;
        }

        /// <summary>Есть ли у контроллера элемент с таким именем (для поиска и для журнала).</summary>
        public static bool HasControl(XRController controller, string name)
        {
            if (controller == null || !controller.added || string.IsNullOrEmpty(name)) return false;
            return controller.TryGetChildControl<InputControl>(name) != null;
        }

        /// <summary>
        /// ЧТО РЕАЛЬНО ЕСТЬ У КОНТРОЛЛЕРА. Печатается в журнал устройства: по этой строке видно,
        /// нашёлся ли курок и стик, или контроллер пришёл «пустым» (тогда дело в профиле OpenXR).
        /// </summary>
        public static string ControlSummary(XRController controller)
        {
            if (controller == null) return "контроллера нет";

            var all = new List<string>();
            all.AddRange(TriggerControlNames);
            all.AddRange(GripControlNames);
            all.AddRange(StickControlNames);
            all.Add("devicePose");
            all.Add("devicePosition");
            all.Add("deviceRotation");
            all.Add("handedness");

            var found = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                if (found.Contains(all[i])) continue;
                if (HasControl(controller, all[i])) found.Add(all[i]);
            }
            return found.Count > 0 ? string.Join(", ", found.ToArray()) : "ни одного известного элемента";
        }

        /// <summary>
        /// Прочитать «курок», «грип» или «стик» по списку имён: сначала кнопка (нажата = 1),
        /// затем ось. Возвращает 0, если ни одного элемента нет, — это честный ноль,
        /// а не «работает». Обход списка, а не одного имени, — потому что профили OpenXR разные.
        /// </summary>
        private static float ReadFirst(XRController c, string[] names)
        {
            if (c == null || !c.added) return 0f;
            for (int i = 0; i < names.Length; i++)
            {
                var press = c.TryGetChildControl<ButtonControl>(names[i]);
                if (press != null && press.isPressed) return 1f;

                var axis = c.TryGetChildControl<AxisControl>(names[i]);
                if (axis != null) return axis.ReadValue();
            }
            return 0f;
        }

        private static float ReadTrigger(XRController c)
        {
            return ReadFirst(c, TriggerControlNames);
        }

        private static float ReadGrip(XRController c)
        {
            return ReadFirst(c, GripControlNames);
        }

        private static Vector2 Stick(XRController c)
        {
            if (c == null || !c.added) return Vector2.zero;
            string name = FirstPresent(StickControlNames, n => HasControl(c, n));
            if (string.IsNullOrEmpty(name)) return Vector2.zero;
            var stick = c.TryGetChildControl<Vector2Control>(name);
            return stick != null ? stick.ReadValue() : Vector2.zero;
        }

        /// <summary>
        /// ИМЕНА УПРАВЛЯЮЩИХ ЭЛЕМЕНТОВ, КОТОРЫЕ СЧИТАЮТСЯ НАЖАТИЕМ СТИКА (§41). Порядок — как у
        /// курка: сначала богатый профиль контроллера PICO, затем простой.
        /// </summary>
        public static readonly string[] StickClickControlNames =
        {
            "thumbstickClicked", "primary2DAxisClick", "stickClicked", "joystickClicked"
        };

        /// <summary>
        /// НАЖАТИЕ СТИКА ЛЕВОГО КОНТРОЛЛЕРА — ВХОД В СТАРТОВОЕ МЕНЮ (указание владельца 24.10.2026:
        /// «нет кнопки выхода в меню при случайном/не случайном сворачивании стартового окна»).
        /// Стик выбран потому, что он есть у любого профиля, а курок и щипок уже заняты нажатием
        /// по панели.
        /// </summary>
        public static bool LeftStickClick { get { EnsureScanned(); return Button(left, StickClickControlNames); } }

        /// <summary>Нажатие стика правого контроллера.</summary>
        public static bool RightStickClick { get { EnsureScanned(); return Button(right, StickClickControlNames); } }

        // ------------------------------------------------------------------ A/B/X/Y (стандарт Quest/PICO)

        /// <summary>
        /// ИМЕНА КНОПОК A/B/X/Y. Стандарт OpenXR: `primaryButton` (A на правом / X на левом),
        /// `secondaryButton` (B / Y). Запасные имена — для простых профилей и PICO-рантайма.
        /// </summary>
        public static readonly string[] PrimaryButtonControlNames =
            { "primaryButton", "primary", "aButton", "xButton", "menuButton" };

        public static readonly string[] SecondaryButtonControlNames =
            { "secondaryButton", "secondary", "bButton", "yButton" };

        /// <summary>Кнопка A (правый primary) — «подтвердить/выбрать» по стандарту Quest.</summary>
        public static bool RightPrimary { get { EnsureScanned(); return Button(right, PrimaryButtonControlNames); } }

        /// <summary>Кнопка B (правый secondary) — «назад/отмена» по стандарту Quest.</summary>
        public static bool RightSecondary { get { EnsureScanned(); return Button(right, SecondaryButtonControlNames); } }

        /// <summary>Кнопка X (левый primary) — «меню» по стандарту Quest.</summary>
        public static bool LeftPrimary { get { EnsureScanned(); return Button(left, PrimaryButtonControlNames); } }

        /// <summary>Кнопка Y (левый secondary) — «вторичное меню/настройки» по стандарту Quest.</summary>
        public static bool LeftSecondary { get { EnsureScanned(); return Button(left, SecondaryButtonControlNames); } }

        /// <summary>Кнопка системного меню (menu/hamburger) — «главное меню» на любом контроллере.</summary>
        public static bool MenuButton
        {
            get
            {
                EnsureScanned();
                return Button(right, MenuButtonControlNames) || Button(left, MenuButtonControlNames);
            }
        }

        private static readonly string[] MenuButtonControlNames =
            { "menuButton", "start", "systemButton", "menu" };

        /// <summary>Нажат ли управляющий элемент с одним из этих имён.</summary>
        private static bool Button(XRController c, string[] names)
        {
            if (c == null || !c.added) return false;

            string name = FirstPresent(names, n => HasControl(c, n));
            if (string.IsNullOrEmpty(name)) return false;

            var button = c.TryGetChildControl<ButtonControl>(name);
            return button != null && button.isPressed;
        }

        /// <summary>
        /// Поза устройства трекинга.
        ///
        /// ПОЧЕМУ ТРИ ПУТИ, А НЕ ОДИН. Состав управляющих элементов у XR-устройств разный:
        /// одни отдают собранную позу (`devicePose`), другие — отдельные `devicePosition`
        /// и `deviceRotation`, третьи не выставляют признак отслеживания вообще. Если
        /// опереться только на один вариант, на части платформ «луч просто не работает»,
        /// и причину в шлеме не видно. Поэтому пробуем по очереди и запоминаем, что сработало.
        /// </summary>
        private static bool TryPose(InputDevice device, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (device == null || !device.added) return false;

            // Путь 1: собранная поза.
            PoseControl pose = device.TryGetChildControl<PoseControl>("devicePose");
            if (pose != null)
            {
                PoseState state = pose.ReadValue();
                if (state.isTracked || state.position != Vector3.zero)
                {
                    position = state.position;
                    rotation = state.rotation;
                    return true;
                }
            }

            // Путь 2: отдельные позиция и поворот.
            Vector3Control pos = device.TryGetChildControl<Vector3Control>("devicePosition");
            QuaternionControl rot = device.TryGetChildControl<QuaternionControl>("deviceRotation");
            if (pos != null && rot != null)
            {
                position = pos.ReadValue();
                rotation = rot.ReadValue();
                return true;
            }

            // Путь 3: только позиция (поворот остаётся единичным — лучше, чем ничего).
            if (pos != null)
            {
                position = pos.ReadValue();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Поиск устройств: не чаще раза в секунду (устройства появляются позже запуска —
        /// шлем надевают уже после старта приложения).
        /// </summary>
        private static void EnsureScanned()
        {
            if (Time.realtimeSinceStartup - lastScanTime < 1f) return;
            Scan();
        }

        /// <summary>Пересканировать немедленно (нужно диагностике ввода).</summary>
        public static void ForceScan()
        {
            Scan();
        }

        private static void Scan()
        {
            lastScanTime = Time.realtimeSinceStartup;

            controllers.Clear();
            left = null;
            right = null;
            head = null;

            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice d = devices[i];
                if (d == null || !d.added) continue;

                XRHMD hmd = d as XRHMD;
                if (hmd != null) { head = hmd; continue; }

                XRController c = d as XRController;
                if (c == null) continue;
                controllers.Add(d);

                // ПОРЯДОК ОПРЕДЕЛЕНИЯ РУКИ (исправлено 21.10.2026). Сначала — МЕТКИ-ИСПОЛЬЗОВАНИЯ
                // (`LeftHand`/`RightHand`), которые Input System называет штатным способом опознания
                // руки у XR-контроллера; затем — число `handedness`; затем — порядок находки.
                // Раньше был только `handedness`, и на живом шлеме руки оказались перепутаны.
                if (UsageSays(c, "RightHand")) right = c;
                else if (UsageSays(c, "LeftHand")) left = c;
                else if (IsRightHanded(c)) right = c;
                else if (IsLeftHanded(c)) left = c;
                else if (right == null) right = c;
                else if (left == null) left = c;
            }

            // §37: если стороны уже определены по геометрии — вернуть именно их (пересканирование
            // происходит раз в секунду и иначе возвращало бы метки рантайма обратно).
            ApplyLockedSides();
        }

        // ------------------------------------------------------------------ диагностика (§32)

        private static float nextDiagnosticTime;
        private static int diagnosticsLeft = 8;
        private static bool controllerStateKnown;
        private static bool controllersWerePresent;

        /// <summary>
        /// ПЕРИОДИЧЕСКИЙ ОТЧЁТ О ВВОДЕ. Зачем: шлем надевают ПОСЛЕ запуска приложения, и
        /// контроллеры появляются в системе не сразу. Одиночная строка в `KvXrRig.Start()` этого
        /// не показывает — именно так в §31 остался без ответа вопрос «устройств в системе 0:
        /// это дефект или спящий шлем?». Здесь состав печатается несколько раз.
        ///
        /// ЧТО ИСПРАВЛЕНО 15.10.2026. Прежний отчёт был ограничен по ВРЕМЕНИ (8 отчётов по 2 с —
        /// 16 секунд от запуска), а появление контроллера отмечалось только внутри этого окна.
        /// На живом шлеме вышло так: окно закрылось, и через минуту после него рантайм привязал
        /// профиль контроллера — в журнале этого уже НЕ БЫЛО ВИДНО, и вопрос «контроллеры
        /// работают или нет» остался без ответа. Теперь слежение за появлением и пропажей
        /// контроллеров идёт ПОСТОЯННО, а ограничение по времени осталось только у подробного
        /// перечня устройств.
        /// </summary>
        public static void TickDiagnostics(float intervalSeconds = 2f, int reports = 8)
        {
            // §37: стороны рук определяются по геометрии (метки рантайма на живом шлеме соврали).
            ResolveSides();

            // §37: слежение за позой руки луча — чтобы «после сна указка не работает» было видно числами.
            WatchRayPose();

            WatchControllers();

            if (diagnosticsLeft <= 0) return;
            if (Time.realtimeSinceStartup < nextDiagnosticTime) return;

            nextDiagnosticTime = Time.realtimeSinceStartup + Mathf.Max(0.5f, intervalSeconds);
            diagnosticsLeft--;

            Scan();
            Debug.Log("[KvXr] " + DescribeDevices());
        }

        /// <summary>
        /// СЛЕЖЕНИЕ ЗА КОНТРОЛЛЕРАМИ — БЕЗ ОГРАНИЧЕНИЯ ПО ВРЕМЕНИ. Печатает строку только при
        /// СМЕНЕ состояния (появились / пропали), поэтому журнал не засоряется: на спокойном
        /// запуске это одна строка за весь сеанс. Первый взгляд «контроллеров нет» не печатается —
        /// это нормальное состояние на старте, а не событие.
        /// </summary>
        private static void WatchControllers()
        {
            EnsureScanned();

            bool present = right != null || left != null;
            if (controllerStateKnown && present == controllersWerePresent) return;

            bool firstLook = !controllerStateKnown;
            controllerStateKnown = true;
            controllersWerePresent = present;

            if (!present)
            {
                if (firstLook) return;
                Debug.Log("[KvXr] КОНТРОЛЛЕРЫ ПРОПАЛИ: ни правого, ни левого (" +
                          DescribeDevices().Replace("\n", " ") + ")");
                return;
            }

            Debug.Log("[KvXr] КОНТРОЛЛЕРЫ ПОЯВИЛИСЬ: правый " +
                      (right != null ? right.name : "нет") + " (" + ControlSummary(right) + ")" +
                      " · левый " + (left != null ? left.name : "нет") + " (" + ControlSummary(left) + ")" +
                      " · курок " + RightTrigger.ToString("0.00") +
                      " · стик " + RightStick.ToString("0.00"));
        }

        /// <summary>
        /// ПОДРОБНЫЙ СОСТАВ ВВОДА: сколько устройств видит Input System, какие у них layout,
        /// какие из них XR, и какие управляющие элементы нашлись у контроллеров. Это ответ
        /// на вопрос «контроллеры не обнаружены — почему»: по строке видно, пуст ли список
        /// устройств (шлем спит), или устройства есть, но у них нет нужных элементов (профиль).
        /// </summary>
        public static string DescribeDevices()
        {
            EnsureScanned();

            var sb = new System.Text.StringBuilder();
            sb.Append("XR-ввод: шлем ").Append(head != null ? "есть" : "НЕТ")
              .Append(" · правый ").Append(right != null ? "есть" : "нет")
              .Append(" · левый ").Append(left != null ? "есть" : "нет")
              .Append(" · устройств в InputSystem ").Append(InputSystem.devices.Count);

            var devices = InputSystem.devices;
            // Печатаем ВСЕ устройства: на шлеме их 24, и при ограничении в 10 строк шлем (XRHMD)
            // мог не попасть в список — именно так и вышло 13.10.2026 при разборе карточки Б14.
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice d = devices[i];
                if (d == null) continue;
                sb.Append("\n    [").Append(i).Append("] ").Append(d.displayName)
                  .Append(" · layout ").Append(d.layout)
                  .Append(" · added ").Append(d.added)
                  .Append(" · enabled ").Append(d.enabled)
                  .Append(" · XRController ").Append(d is XRController ? "да" : "нет")
                  .Append(" · XRHMD ").Append(d is XRHMD ? "да" : "нет");
            }

            if (devices.Count == 0)
                sb.Append(" — В СИСТЕМЕ НЕТ НИ ОДНОГО УСТРОЙСТВА ВВОДА");

            if (right != null) sb.Append("\n    правый: ").Append(ControlSummary(right));
            if (left != null) sb.Append("\n    левый: ").Append(ControlSummary(left));
            return sb.ToString();
        }

        /// <summary>Счётчики диагностики — для тестов: сколько отчётов уже напечатано.</summary>
        public static int DiagnosticsLeft { get { return diagnosticsLeft; } }

        private static bool IsRightHanded(XRController c)
        {
            var hand = c.TryGetChildControl<IntegerControl>("handedness");
            if (hand != null && hand.ReadValue() == 1) return true;        // Right
            return NameSays(c, "right");
        }

        private static bool IsLeftHanded(XRController c)
        {
            var hand = c.TryGetChildControl<IntegerControl>("handedness");
            if (hand != null && hand.ReadValue() == 2) return true;        // Left
            return NameSays(c, "left");
        }

        private static bool NameSays(XRController c, string needle)
        {
            return c.name != null && c.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// ЕСТЬ ЛИ У УСТРОЙСТВА МЕТКА-ИСПОЛЬЗОВАНИЕ (`LeftHand`/`RightHand`). Это штатный способ
        /// Input System опознать руку XR-контроллера, и он точнее числа `handedness`: метка берётся
        /// из пути OpenXR (`/user/hand/left`, `/user/hand/right`) напрямую.
        /// </summary>
        private static bool UsageSays(XRController controller, string usage)
        {
            if (controller == null) return false;

            var usages = controller.usages;                 // ReadOnlyArray<InternedString> — не null-тип
            for (int i = 0; i < usages.Count; i++)
            {
                if (string.Equals(usages[i].ToString(), usage, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
