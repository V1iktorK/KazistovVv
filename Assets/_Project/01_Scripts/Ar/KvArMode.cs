using System;

namespace KazistovVvAr
{
    /// <summary>
    /// КАКОЙ РЕЖИМ ЗАПУСКАТЬ: VR (шлем) ИЛИ AR (телефон/планшет).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ ТИП. Приложение ОДНО, а устройства разные: шлем поднимает OpenXR,
    /// планшет — ARCore. Разница обязана решаться по ФАКТУ поднятого провайдера, а не по
    /// модели устройства и не по настройке перед запуском (требование S1.7 промта).
    ///
    /// ПОЧЕМУ ИМЕННО ИМЯ ЛОАДЕРА. XR Management инициализирует лоадеры по порядку и берёт
    /// ПЕРВЫЙ успешный (`XRManagerSettings.cs:163-186`). Если первым стоит OpenXR, шлем
    /// получает OpenXR, а на планшете OpenXR не поднимается и успешным становится ARCore.
    /// Имя типа активного лоадера — единственный признак, который виден на старте до загрузки
    /// любой игровой сцены.
    ///
    /// ФУНКЦИИ ЧИСТЫЕ: их можно проверить тестом без устройства (правило проекта — «пункт,
    /// который нельзя проверить без шлема, проверяется таблицей»).
    /// </summary>
    public enum KvArBootKind
    {
        /// <summary>Ни один провайдер не поднялся — устройство не умеет ни VR, ни AR.</summary>
        Unknown = 0,

        /// <summary>Поднялся OpenXR (шлем): грузим прежнюю VR/MR-сцену.</summary>
        Vr = 1,

        /// <summary>Поднялся ARCore (телефон/планшет): грузим AR-сцену.</summary>
        Ar = 2
    }

    public static class KvArMode
    {
        /// <summary>Сцена шлема — та же, что была до AR (VR/MR не меняются).</summary>
        public const string VrSceneName = "XrMain";

        /// <summary>Новая AR-сцена (создаётся редакторным инструментом `KvArSceneBuilder`).</summary>
        public const string ArSceneName = "ArMain";

        /// <summary>Путь VR-сцены в проекте (для тестов и сборщика сцен).</summary>
        public const string VrScenePath = "Assets/_Project/00_Scenes/XrMain.unity";

        /// <summary>Путь AR-сцены в проекте.</summary>
        public const string ArScenePath = "Assets/_Project/00_Scenes/ArMain.unity";

        /// <summary>Путь загрузочной сцены (индекс 0 в Build Settings).</summary>
        public const string BootScenePath = "Assets/_Project/00_Scenes/BootAr.unity";

        /// <summary>Часть имени типа лоадера, по которой узнаётся ARCore.</summary>
        public const string ArCoreLoaderMarker = "ARCore";

        /// <summary>Часть имени типа лоадера, по которой узнаётся OpenXR.</summary>
        public const string OpenXrLoaderMarker = "OpenXR";

        /// <summary>Строка режима, которую ищет приёмка A3 (дословно из чек-листа S2).</summary>
        public const string ArModeLineMarker = "РЕЖИМ: ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ";

        /// <summary>Строка режима VR (для честного журнала на шлеме).</summary>
        public const string VrModeLineMarker = "РЕЖИМ: ВИРТУАЛЬНАЯ РЕАЛЬНОСТЬ";

        /// <summary>
        /// ОПРЕДЕЛИТЬ РЕЖИМ ПО ИМЕНИ ТИПА АКТИВНОГО ЛОАДЕРА.
        ///
        /// ПОРЯДОК ПРОВЕРОК ВАЖЕН: ARCore проверяется первым. Имя `ARCoreLoader` не содержит
        /// «OpenXR», поэтому перепутать нельзя; но если появится составной лоадер с обоими
        /// словами, AR должен победить — на телефоне OpenXR не поднимается вовсе.
        /// </summary>
        public static KvArBootKind Classify(string loaderTypeName)
        {
            if (string.IsNullOrEmpty(loaderTypeName)) return KvArBootKind.Unknown;
            string name = loaderTypeName.Trim();
            if (name.IndexOf(ArCoreLoaderMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                return KvArBootKind.Ar;
            if (name.IndexOf(OpenXrLoaderMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                return KvArBootKind.Vr;
            return KvArBootKind.Unknown;
        }

        /// <summary>Имя сцены для режима (пустая строка — сцены нет, показываем сообщение).</summary>
        public static string SceneFor(KvArBootKind kind)
        {
            if (kind == KvArBootKind.Ar) return ArSceneName;
            if (kind == KvArBootKind.Vr) return VrSceneName;
            return string.Empty;
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА О ЗАПУСКЕ. Для AR обязана содержать дословно
        /// `РЕЖИМ: ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ` (пункт A3 приёмки), для VR — честную строку VR.
        /// </summary>
        public static string ModeLine(KvArBootKind kind, string loaderTypeName)
        {
            string loader = string.IsNullOrEmpty(loaderTypeName) ? "нет" : loaderTypeName;
            switch (kind)
            {
                case KvArBootKind.Ar:
                    return "[KvAr] " + ArModeLineMarker + " — камера устройства, объекты ставятся на " +
                           "найденные плоскости через якоря AR Foundation (провайдер: " + loader + ")";
                case KvArBootKind.Vr:
                    return "[KvAr] " + VrModeLineMarker + " — загружается прежняя сцена шлема " +
                           VrSceneName + " (провайдер: " + loader + ")";
                default:
                    return "[KvAr] РЕЖИМ: НЕ ОПРЕДЕЛЁН — ни OpenXR, ни ARCore не поднялись " +
                           "(активный лоадер: " + loader + ")";
            }
        }

        /// <summary>
        /// ПОНЯТНОЕ СООБЩЕНИЕ ДЛЯ ЭКРАНА ОТКАЗА (Ж1). Без технического жаргона: оператор должен
        /// понять, что делать, а не читать имя подсистемы.
        /// </summary>
        public static string UnsupportedMessage()
        {
            return "Это устройство не поддерживает дополненную реальность.\n\n" +
                   "Нужен телефон или планшет Android с поддержкой ARCore " +
                   "(в Google Play — «Google Play Services for AR»).\n\n" +
                   "Приложение не упало: просто на этом устройстве режим AR недоступен.";
        }

        /// <summary>Короткое название режима для экрана состояния.</summary>
        public static string KindLabel(KvArBootKind kind)
        {
            if (kind == KvArBootKind.Ar) return "AR";
            if (kind == KvArBootKind.Vr) return "VR";
            return "—";
        }

        /// <summary>Проверка «это AR» одной функцией — чтобы не сравнивать enum в пяти местах.</summary>
        public static bool IsAr(KvArBootKind kind) { return kind == KvArBootKind.Ar; }
    }
}
