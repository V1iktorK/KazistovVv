#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// МЕТА-ДАННЫЕ PICO В ANDROIDMANIFEST — ЗАЯВКА «ЭТО VR-ПРИЛОЖЕНИЕ, НУЖНЫ КОНТРОЛЛЕРЫ» (§36).
    ///
    /// ОТКУДА ВЗЯТО. Собственный SDK PICO ставит в манифест мета-данные одним и тем же способом:
    /// хук после генерации gradle-проекта, класс `PICOModifyAndroidManifest` в
    /// `PICO-Unity-OpenXR-SDK/Editor/`. Его ключевые строки:
    ///     CreateOrUpdateAndroidMetaData("pvr.app.type", "vr");
    ///     CreateOrUpdateAndroidMetaData("controller", "1");
    /// а при включённом отслеживании рук — `handtracking = 1` (и, если режим «только руки»,
    /// мета-данная `controller` УДАЛЯЕТСЯ). То есть `controller = 1` — это и есть заявка системы:
    /// «приложению нужны контроллеры».
    ///
    /// ПОЧЕМУ ЭТО ПРОВЕРЯЕТСЯ. В нашем APK этих мета-данных НЕ БЫЛО (проверено `aapt2 dump xmltree`:
    /// есть `com.oculus.*`, `unityplayer.*`, `org.khronos.openxr.*` — и ни одного `pvr.*`).
    /// А рантайм PICO при этом возвращает `xrGetCurrentInteractionProfile` = `XR_NULL_PATH` для
    /// обеих рук, хотя расширение `XR_BD_controller_interaction` выдано, привязки приняты и
    /// система непрерывно трекает контроллеры (`logDebugControllerPose … confidence 2`).
    ///
    /// ЧТО ЭТО НЕ ДЕЛАЕТ: не ставит PICO SDK ([R3] не нарушается — здесь только две строки XML,
    /// ничего вендорского не подключается) и не меняет сцену, префабы или ассеты.
    /// </summary>
    public class KvPicoManifestMetaData : IPostGenerateGradleAndroidProject
    {
        /// <summary>Порядок: после хуков пакета OpenXR, чтобы не затереть их правки.</summary>
        public int callbackOrder { get { return 100; } }

        /// <summary>Мета-данные, которые обязаны быть в манифесте — те же, что ставит SDK PICO.</summary>
        public static readonly string[][] RequiredMetaData =
        {
            new[] { "controller", "1" },   // PICO SDK ставит её сам; оставлено как страховка и как утверждение «нужны контроллеры»

            // ---------------------------------------------------------------- MR/AR (30.10.2026, S44)
            //
            // ЗАЧЕМ. Аудит S44 показал: манифест проекта был МЕТА-ориентированным
            // (`com.oculus.*`), а PICO-метаданных для сквозного вида не было вовсе. То есть
            // карточка Б3 «passthrough включён» опиралась на фичу OpenXR, но НЕ на манифест —
            // и на устройстве приложение запускалось в чистом VR, показывая виртуальную
            // лабораторию вместо комнаты оператора. Для MR (суббота) это первое, что нужно.
            //
            // ПОЧЕМУ ИМЕННО ЭТИ ПАРЫ. `seeThroughMode` = 1 включает сквозной вид; `virtualEnvironment`
            // = 0 выключает фон PICO (иначе поверх камеры висит их заставка); `handtracking` = 1
            // включает отслеживание рук (жесты, этап 6). Значения — те, что ставит SDK PICO;
            // здесь они только в XML, вендорский SDK по-прежнему НЕ подключается ([R3] в силе).
            //
            // ВАЖНО. Ни одна из них не тянет за собой проверку «приложение из магазина» — в отличие
            // от `pvr.app.type = vr`, которая снимает сбоку установленное приложение (см. ниже).
            new[] { "seeThroughMode", "1" },
            new[] { "virtualEnvironment", "0" },
            new[] { "handtracking", "1" },
        };

        /// <summary>
        /// РАЗРЕШЕНИЯ, КОТОРЫЕ ОБЯЗАНЫ БЫТЬ: камера мира и отслеживание рук.
        ///
        /// ПОЧЕМУ ЧЕРЕЗ МАНИФЕСТ, А НЕ «НАДЕЖДА НА ПАКЕТ». OpenXR — платформенный слой, он не
        /// обязан добавлять PICO-разрешения. Без `CAMERA` сквозной вид не дадут, без
        /// `com.picovr.permission.HAND_TRACKING` подсистема кистей не поднимется — и в журнале
        /// это будет выглядеть как «кисти НЕТ», то есть как поломка железа (карточка Б41).
        /// </summary>
        public static readonly string[] RequiredPermissions =
        {
            "android.permission.CAMERA",
            "com.picovr.permission.HAND_TRACKING",
        };


        /// <summary>
        /// ЧЕГО ЗДЕСЬ СОЗНАТЕЛЬНО НЕТ: `pvr.app.type = vr`, хотя SDK PICO его ставит.
        ///
        /// ПОЧЕМУ (найдено 19.10.2026 на живом шлеме). С этой мета-данной система PICO считает
        /// приложение «нативным VR-приложением» и требует сведений о нём, как для приложения из
        /// магазина:
        ///     `W XrAppIorapServer: update appinfo: failed to query the AppInfo of app = com.KazistovVv.app ret = 2`
        /// — и через 7 миллисекунд после выхода на передний план снимает процесс:
        ///     `W ActivityManager: Force stopping com.KazistovVv.app … pkg=com.bytedance.pico.matrix`
        ///     `W ActivityTaskManager: … app died, no saved state`
        /// Наша сборка ставится сбоку (adb), в каталоге магазина её нет — поэтому заявка
        /// «я из магазина» делает приложение незапускаемым. `controller = 1` такой реакции не даёт.
        ///
        /// Если когда-нибудь приложение пойдёт в каталог PICO, `pvr.app.type = vr` можно вернуть —
        /// тогда сведения о нём у системы будут.
        /// </summary>
        public const string SkippedMetaDataReason = "pvr.app.type не ставится: с этой мета-данной " +
                                                   "система снимает сбоку установленное приложение (pico.matrix)";

        /// <summary>
        /// МЕТА-ДАННЫЕ, КОТОРЫЕ ОБЯЗАНЫ БЫТЬ УДАЛЕНЫ. Одного «не добавлять» мало: сгенерированный
        /// манифест лежит в `Library\Bee\Android\…\Gradle\unityLibrary` и НЕ пересоздаётся каждой
        /// сборкой, поэтому ранее добавленная мета-данная остаётся сама. Проверено 19.10.2026:
        /// убрал из списка — а в APK `pvr.app.type` всё равно была.
        /// </summary>
        public static readonly string[] ForbiddenMetaData = { };

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            try
            {
                string manifestPath = Path.Combine(
                    Path.Combine(Path.Combine(basePath, "src"), "main"), "AndroidManifest.xml");

                if (!File.Exists(manifestPath))
                {
                    Debug.LogWarning("[KvManifest] манифест не найден: " + manifestPath +
                                     " — мета-данные PICO не добавлены.");
                    return;
                }

                var document = new XmlDocument();
                using (var reader = new XmlTextReader(manifestPath)) document.Load(reader);

                XmlElement application = document.SelectSingleNode("/manifest/application") as XmlElement;
                if (application == null)
                {
                    Debug.LogWarning("[KvManifest] в манифесте нет узла <application> — мета-данные не добавлены.");
                    return;
                }

                var sb = new StringBuilder();
                int added = 0;
                int updated = 0;
                int removed = 0;

                // СНАЧАЛА УБИРАЕМ ЗАПРЕЩЁННЫЕ: манифест переиспользуется между сборками.
                for (int i = 0; i < ForbiddenMetaData.Length; i++)
                {
                    string forbidden = ForbiddenMetaData[i];
                    var toDelete = new System.Collections.Generic.List<XmlNode>();
                    foreach (XmlNode node in application.SelectNodes("meta-data"))
                    {
                        XmlAttribute name = node.Attributes != null ? node.Attributes["android:name"] : null;
                        if (name != null && name.Value == forbidden) toDelete.Add(node);
                    }
                    for (int k = 0; k < toDelete.Count; k++)
                    {
                        application.RemoveChild(toDelete[k]);
                        removed++;
                    }
                    if (toDelete.Count > 0)
                        sb.Append("  удалено (мешало запуску): ").Append(forbidden).AppendLine();
                }

                foreach (string[] pair in RequiredMetaData)
                {
                    string key = pair[0];
                    string value = pair[1];

                    XmlElement existing = null;
                    foreach (XmlNode node in application.SelectNodes("meta-data"))
                    {
                        XmlAttribute name = node.Attributes != null ? node.Attributes["android:name"] : null;
                        if (name != null && name.Value == key) { existing = node as XmlElement; break; }
                    }

                    if (existing == null)
                    {
                        XmlElement metaData = document.CreateElement("meta-data");
                        XmlAttribute nameAttribute = document.CreateAttribute("android", "name",
                            "http://schemas.android.com/apk/res/android");
                        nameAttribute.Value = key;
                        XmlAttribute valueAttribute = document.CreateAttribute("android", "value",
                            "http://schemas.android.com/apk/res/android");
                        valueAttribute.Value = value;
                        metaData.Attributes.Append(nameAttribute);
                        metaData.Attributes.Append(valueAttribute);
                        application.AppendChild(metaData);
                        added++;
                        sb.Append("  добавлено: ").Append(key).Append(" = ").Append(value).AppendLine();
                    }
                    else
                    {
                        XmlAttribute valueAttribute = existing.Attributes["android:value"];
                        if (valueAttribute == null)
                        {
                            valueAttribute = document.CreateAttribute("android", "value",
                                "http://schemas.android.com/apk/res/android");
                            existing.Attributes.Append(valueAttribute);
                        }
                        valueAttribute.Value = value;
                        updated++;
                        sb.Append("  значение выставлено: ").Append(key).Append(" = ").Append(value).AppendLine();
                    }
                }

                // ---------------------------------------------------------------- РАЗРЕШЕНИЯ (30.10.2026)
                //
                // ПОЧЕМУ ЗДЕСЬ, А НЕ В Assets/Plugins/Android/AndroidManifest.xml. Тот файл —
                // МЕТА-ориентированный (`com.oculus.*`) и служит основой; PICO-разрешения
                // добавляются здесь, в сгенерированный манифест, и в строке журнала видно, что
                // именно добавлено. Так же уже сделано с мета-данными выше.
                int permissionsAdded = 0;
                foreach (string permission in RequiredPermissions)
                {
                    bool present = false;
                    foreach (XmlNode node in document.SelectNodes("/manifest/uses-permission"))
                    {
                        XmlAttribute name = node.Attributes != null ? node.Attributes["android:name"] : null;
                        if (name != null && name.Value == permission) { present = true; break; }
                    }
                    if (present) continue;

                    XmlElement element = document.CreateElement("uses-permission");
                    XmlAttribute nameAttribute = document.CreateAttribute("android", "name",
                        "http://schemas.android.com/apk/res/android");
                    nameAttribute.Value = permission;
                    element.Attributes.Append(nameAttribute);
                    document.DocumentElement.AppendChild(element);
                    permissionsAdded++;
                    sb.Append("  разрешение добавлено: ").Append(permission).AppendLine();
                }

                using (var writer = new XmlTextWriter(manifestPath, new UTF8Encoding(false)))
                {
                    writer.Formatting = Formatting.Indented;
                    document.Save(writer);
                }

                // СТРОКА-ДОКАЗАТЕЛЬСТВО: попала ли заявка в манифест сборки.
                Debug.Log("[KvManifest] мета-данные PICO в AndroidManifest: добавлено " + added +
                          " · выставлено " + updated + " · удалено " + removed +
                          " · разрешений добавлено " + permissionsAdded +
                          " · файл " + manifestPath + Environment.NewLine + sb +
                          "  (" + SkippedMetaDataReason + ")");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KvManifest] не удалось дописать мета-данные PICO: " + e.GetType().Name +
                                 ": " + e.Message);
            }
        }
    }
}
#endif
