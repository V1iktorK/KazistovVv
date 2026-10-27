#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// СОБСТВЕННАЯ ПОДПИСЬ СБОРКИ (сессия §36, шаг «регистрация в PICO Developer Center»).
    ///
    /// ЗАЧЕМ. Проверено на живом шлеме 20.10.2026: магазинному приложению (Angry Birds VR)
    /// рантайм PICO отдаёт контроллеры — `CursorManager … TrggerClick … currentMainController:0/1`
    /// и работающий курсор в самом приложении, — а нашему, установленному сбоку, отвечает
    /// `xrGetCurrentInteractionProfile` = `XR_NULL_PATH`. Наш код привязок при этом верен:
    /// расширение выдано, привязки приняты, профиль при сборке действий выбирается наш,
    /// нарушений правила по рукам 0. Значит различие в том, КАК система опознаёт приложение.
    ///
    /// В каталоге PICO приложение опознаётся парой «имя пакета + подпись», поэтому подпись
    /// должна быть постоянной и известной: без неё регистрация в Developer Center бессмысленна
    /// (её сверяют с отпечатком сертификата).
    ///
    /// ЧТО ДЕЛАЕТ. Включает собственную подпись и задаёт путь к хранилищу ключей, псевдоним и
    /// пароли. Путь абсолютный: относительный Unity ищет от папки проекта и путается, если
    /// сборка запускается не из корня.
    ///
    /// ЧЕСТНО О ГРАНИЦАХ. Это **отладочный ключ проекта**, а не секрет: он лежит в репозитории,
    /// чтобы любая сборка была подписана одинаково и отпечаток не менялся. Для публикации в
    /// каталоге такой ключ не годится — там нужен отдельный, хранимый вне репозитория.
    /// </summary>
    public static class KvAndroidSigning
    {
        /// <summary>Имя меню — рядом с остальными инструментами устройства.</summary>
        private const string MenuRoot = "KazistovVv/Устройство/";

        /// <summary>Абсолютный путь к хранилищу ключей проекта.</summary>
        public const string KeystorePath = @"C:\KazistovVv_Build\keystore\kazistovvv.keystore";

        /// <summary>Псевдоним ключа в хранилище.</summary>
        public const string KeyAlias = "kazistovvv";

        /// <summary>Пароль хранилища и ключа (одинаковый — так требует настройка Unity).</summary>
        public const string Password = "KazistovVv2026";

        [MenuItem(MenuRoot + "8. Подпись сборки: собственный ключ", false, 73)]
        public static void Configure()
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = KeystorePath;
            PlayerSettings.Android.keystorePass = Password;
            PlayerSettings.Android.keyaliasName = KeyAlias;
            PlayerSettings.Android.keyaliasPass = Password;

            AssetDatabase.SaveAssets();

            Debug.Log("[KvSign] подпись сборки настроена: useCustomKeystore=" +
                      PlayerSettings.Android.useCustomKeystore +
                      " · keystore=" + PlayerSettings.Android.keystoreName +
                      " · alias=" + PlayerSettings.Android.keyaliasName +
                      " · пакет=" + PlayerSettings.applicationIdentifier);
        }

        /// <summary>
        /// СОБРАТЬ APK, ПОДПИСАННЫЙ НАШИМ КЛЮЧОМ. Настройки подписи и сборка обязаны идти в
        /// ОДНОМ сеансе редактора.
        ///
        /// ПОЧЕМУ ОТДЕЛЬНЫЙ МЕТОД (найдено 20.10.2026). Пароли хранилища Unity **в проект не
        /// сохраняет** (это правильно: секрет не должен лежать в репозитории). Поэтому запуск
        /// «сначала настроить подпись, потом отдельной командой собрать» даёт сборку, подписанную
        /// отладочным ключом, и в журнале остаётся:
        ///     `UnityException: Unable to sign the Android application … No keystore passwords were found`
        /// Здесь настройка и сборка вызываются подряд, в одном процессе.
        /// </summary>
        public static void BuildSignedApk()
        {
            Configure();
            KazistovVvXrEditor.KvXrTools.BuildAndroidApk();
        }
    }
}
#endif
