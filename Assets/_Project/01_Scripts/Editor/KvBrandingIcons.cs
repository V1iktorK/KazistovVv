using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// ЗНАЧОК ПРИЛОЖЕНИЯ — В ШЛЕМЕ И НА КОМПЬЮТЕРЕ (указание владельца 24.10.2026: «добавь иконку…
    /// добавь ещё и в ПК версии, там как раз два разных разрешения»).
    ///
    /// ЧТО ДЕЛАЕТ. Берёт два файла значка из `Assets/_Project/Branding` (1024 и 512) и ставит их:
    ///   • Android — значок 512 (круглый, как требует PICO);
    ///   • Standalone (ПК) — оба разрешения, 1024 и 512.
    ///
    /// ПОЧЕМУ ОТДЕЛЬНЫМ ИНСТРУМЕНТОМ, А НЕ «РУКАМИ В ИНСПЕКТОРЕ». Значок — часть сборки, и он должен
    /// выставляться командой, которая видна в журнале: тогда в отчёте есть строка с числами, а не
    /// «владелец где-то нажал». Метод вызывается и из меню, и из batchmode:
    ///   Unity.exe -batchmode -quit -projectPath C:\KazistovVv_Build
    ///     -executeMethod KazistovVvXrEditor.KvBrandingIcons.Apply
    ///
    /// Предупреждение об устаревшем API подавлено осознанно: `SetIconsForBuildTarget` — самый
    /// устойчивый путь, который работает и для Android, и для Standalone; замена ему в 6000.5.6f1
    /// различается по платформам (разные перечисления), и это дало бы больше риска, чем пользы.
    /// </summary>
    public static class KvBrandingIcons
    {
        /// <summary>Значок 1024 — для ПК-сборки.</summary>
        public const string Logo1024Path = "Assets/_Project/Branding/kazistovvv_logo.png";

        /// <summary>Значок 512 — для шлема (Android).</summary>
        public const string Logo512Path = "Assets/_Project/Branding/kazistovvv_logo_512.png";

        /// <summary>ПРИМЕНИТЬ ЗНАЧОК ко всем платформам проекта.</summary>
        [MenuItem("KazistovVv/Значок приложения — применить")]
        public static void Apply()
        {
            Texture2D logo1024 = Load(Logo1024Path);
            Texture2D logo512 = Load(Logo512Path);

            if (logo1024 == null || logo512 == null)
            {
                Debug.LogError("[Значок] файлы не найдены: " + Logo1024Path + " / " + Logo512Path);
                return;
            }

            Prepare(Logo1024Path);
            Prepare(Logo512Path);

            // ВЕРНЫЙ ПУТЬ ДЛЯ 6000.5.6f1 (сверено с документацией Unity 6):
            //   PlayerSettings.SetIcons(NamedBuildTarget, Texture2D[], IconKind)
            // Длина массива ОБЯЗАНА совпадать с числом размеров из `GetIconSizes`, иначе Unity молча
            // ничего не назначит. Поэтому массив собирается по фактическим размерам платформы, а не
            // «двумя картинками на глаз».
            ApplyIcons(NamedBuildTarget.Android, logo512, "шлем (Android)");
            ApplyIcons(NamedBuildTarget.Standalone, logo1024, "компьютер (Standalone)");

            AssetDatabase.SaveAssets();
        }

        /// <summary>Назначить значок платформе: один и тот же файл на все требуемые размеры.</summary>
        private static void ApplyIcons(NamedBuildTarget target, Texture2D logo, string title)
        {
            int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
            if (sizes == null || sizes.Length == 0)
            {
                Debug.LogWarning("[Значок] платформа «" + title + "» не сообщила размеров — значок не применён");
                return;
            }

            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = logo;

            PlayerSettings.SetIcons(target, icons, IconKind.Application);

            Debug.Log("[Значок] " + title + ": файл " + logo.width + "×" + logo.height +
                      " · назначено размеров " + sizes.Length + " (от " + sizes[0] + " до " + sizes[sizes.Length - 1] + ")");
        }

        /// <summary>Загрузить текстуру значка.</summary>
        private static Texture2D Load(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// ПРАВИЛЬНЫЕ НАСТРОЙКИ ИМПОРТА: значок не должен сжиматься, терять альфу или обрезаться до
        /// степени двойки — иначе на устройстве он «поплывёт» или станет прозрачным по краям.
        /// </summary>
        private static void Prepare(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = false;

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }

            if (importer.alphaIsTransparency == false)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (importer.maxTextureSize < 1024)
            {
                importer.maxTextureSize = 1024;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
        }
    }
}

