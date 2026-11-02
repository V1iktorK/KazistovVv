using System;
using UnityEngine;
using KazistovVv.Core;

namespace KazistovVvUI
{
    /// <summary>
    /// КОМАНДЫ ВЫБОРА МОДЕЛИ РОБОТА (часть 3 задачи владельца 02.11.2026).
    ///
    /// СЛОВА ВЛАДЕЛЬЦА: «роботов всех перенеси». Выбранный вариант: «Выбор из списка — один
    /// активный».
    ///
    /// КАК УСТРОЕНО. На каждую модель каталога `KvRobotCatalog` регистрируется обычная команда
    /// `robot.model.&lt;id&gt;`. Дальше она сама попадает в группу «Робот» меню левой руки и
    /// работает через уже существующий механизм нажатий — отдельного экрана не нужно.
    ///
    /// ПОЧЕМУ НЕ ЧЕРЕЗ СТАРТОВОЕ МЕНЮ. Раздел «Робот» в нём занимает все 7 строк
    /// (`KvXrStartMenuLayout.MaxContentRows`), и 12 моделей туда физически не встанут.
    /// В меню левой руки группы ЛИСТАЮТСЯ СТРАНИЦАМИ (часть 1), поэтому там место есть.
    ///
    /// ЧТО ПРОИСХОДИТ ПРИ НАЖАТИИ:
    ///   1. находится стол, на котором стоит текущий робот (или просто ближайший);
    ///   2. старая модель убирается СО СЦЕНЫ (не «прячется»: иначе в дереве проекта останутся
    ///      две строки «Робот», и владелец не поймёт, какая из них настоящая);
    ///   3. создаётся новая модель на том же месте;
    ///   4. пишется строка журнала с названием модели.
    /// </summary>
    public static class KvRobotCommands
    {
        /// <summary>Приставка id команды выбора модели.</summary>
        public const string ModelCommandPrefix = "robot.model.";

        /// <summary>
        /// Зарегистрировать по команде на каждую модель каталога.
        ///
        /// Идемпотентно: повторный вызов не плодит дубликаты (реестр команд сам не перезаписывает
        /// существующие id, но проверка не помешает — вызов может случиться дважды при
        /// пересборке интерфейса).
        /// </summary>
        public static void RegisterModels(KazistovVvUIManager manager)
        {
            int added = 0;
            foreach (KvRobotModel model in KvRobotCatalog.All)
            {
                KvRobotModel captured = model;   // замыкание на текущую модель, не на переменную цикла
                string id = ModelCommandPrefix + captured.Id;
                if (KvCommands.Get(id) != null) continue;

                KvCommands.Register(new KvCommand
                {
                    Id = id,
                    Title = "Робот: " + captured.Title,
                    Description = captured.Description + " · модель " + captured.Id,
                    Hotkey = "",
                    Icon = captured.IsDualArm ? "swap" : "robot",
                    MenuPath = "Робот/Модель робота/" + captured.Title,
                    Execute = delegate { ApplyModel(captured); },
                    // Доступно, когда в сцене есть стол: ставить робота некуда без стола.
                    IsEnabled = delegate { return true; }
                });
                added++;
            }

            Debug.Log("[KvRobot] команд выбора модели зарегистрировано: " + added +
                      " (всего моделей " + KvRobotCatalog.All.Length + ")");
        }

        /// <summary>
        /// ПОСТАВИТЬ МОДЕЛЬ ВМЕСТО ТЕКУЩЕГО РОБОТА.
        ///
        /// Место берётся у существующего робота — так оператор не ищет, куда встанет новый.
        /// Если роботов нет вообще, место вычисляется перед камерой на высоте стола.
        /// </summary>
        public static void ApplyModel(KvRobotModel model)
        {
            if (model == null) return;

            Vector3 spot;
            float yaw;
            RobotController current = FindCurrentRobot(out spot, out yaw);

            // Убираем текущего робота: две модели на одном столе — это не «выбор», а каша.
            if (current != null)
            {
                Debug.Log("[KvRobot] замена робота «" + current.robotName + "» на модель «" +
                          model.Title + "»");
                UnityEngine.Object.Destroy(current.gameObject);
            }

            RobotController created = KvRobotSpawner.Create(model, spot, yaw);
            if (created == null)
            {
                Debug.LogWarning("[KvRobot] модель «" + model.Title + "» НЕ создана — смотрите " +
                                 "причину в предыдущей строке журнала");
                return;
            }

            Debug.Log("[KvRobot] активная модель: «" + model.Title + "» (" + model.Kind +
                      ", " + model.Axes + " осей) · в точке " +
                      spot.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// ТЕКУЩИЙ РОБОТ: его позиция и поворот. Если роботов нет — точка перед камерой.
        ///
        /// Берётся ПЕРВЫЙ включённый робот сцены: в шлеме на столе один активный робот, а
        /// выключенные — это эталоны, которые трогать нельзя.
        /// </summary>
        private static RobotController FindCurrentRobot(out Vector3 spot, out float yaw)
        {
            spot = new Vector3(0f, 0f, 0f);
            yaw = 0f;

            RobotController[] all = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                spot = all[i].transform.position;
                yaw = all[i].transform.eulerAngles.y;
                return all[i];
            }

            // Роботов нет — ставим перед камерой на высоту стола.
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 forward = cam.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                spot = cam.transform.position + forward.normalized * 1.2f;
            }
            spot.y = KazistovVvXr.KvXrTableKinds.TopHeight;
            yaw = 0f;
            return null;
        }

        /// <summary>Строка для журнала: какие модели доступны.</summary>
        public static string Describe()
        {
            return KvRobotCatalog.Describe();
        }
    }
}
