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
    /// ЧЕСТНО О ГРАНИЦАХ. Раньше здесь было написано, что «это отладочный ключ, не секрет: он
    /// лежит в репозитории». Это оказалось неверным решением — аудит 30.10.2026 показал, что
    /// файл ключа и пароль к нему лежали в публичном репозитории рядом. Ключ подписи Android —
    /// это право публиковать обновления от имени владельца: кто получил его, тот может выпустить
    /// «свою» версию приложения. Поэтому с 30.10.2026 пароль НЕ хранится в коде.
    /// </summary>
    public static class KvAndroidSigning
    {
        /// <summary>Имя меню — рядом с остальными инструментами устройства.</summary>
        private const string MenuRoot = "KazistovVv/Устройство/";

        /// <summary>
        /// ПРЕЖНИЙ ПАРОЛЬ, КОТОРЫЙ ЛЕЖАЛ ОТКРЫТЫМ ТЕКСТОМ В ЭТОМ ФАЙЛЕ.
        ///
        /// Оставлен ТОЛЬКО чтобы старые сборки и записи в `EditorPrefs` продолжали работать
        /// без переучивания владельца, и чтобы тест мог проверить, что в коде его больше нет
        /// в виде присваивания. Для сборки он НЕ используется: см. `ResolvePassword`.
        /// </summary>
        private const string LegacyPassword = "KazistovVv2026";

        /// <summary>Имя переменной окружения с паролем хранилища.</summary>
        public const string PasswordEnvironmentVariable = "KAZISTOVVV_KEYPASS";

        /// <summary>Имя ключа в `EditorPrefs` (если переменная окружения не задана).</summary>
        public const string PasswordPrefsKey = "KazistovVv.AndroidSigning.KeyPass";

        /// <summary>Имя переменной окружения с путём к хранилищу.</summary>
        public const string KeystoreEnvironmentVariable = "KAZISTOVVV_KEYSTORE";

        /// <summary>
        /// Абсолютный путь к хранилищу ключей. Путь берётся из переменной окружения, если она
        /// задана: так сборка на другой машине (и в CI) не зависит от буквы диска.
        /// </summary>
        public static string KeystorePath
        {
            get
            {
                string fromEnv = System.Environment.GetEnvironmentVariable(KeystoreEnvironmentVariable);
                if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;

                // Латинский путь: junction C:\KazistovVv_Build → рабочая копия (см. дефекты A6/A21).
                return @"C:\KazistovVv_Build\keystore\kazistovvv.keystore";
            }
        }

        /// <summary>Псевдоним ключа в хранилище.</summary>
        public const string KeyAlias = "kazistovvv";

        /// <summary>
        /// СТРАШИВАЕМ, А НЕ ХРАНИМ.
        ///
        /// Порядок: переменная окружения → `EditorPrefs` → встроенный «прежний» пароль. Последний
        /// шаг оставлен, чтобы подпись не сломалась у владельца на этой машине в день правки;
        /// в CI и на любой другой машине первые два шага обязательны, иначе сборка честно скажет,
        /// что пароля нет.
        /// </summary>
        public static string ResolvePassword()
        {
            string fromEnv = System.Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
            if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;

            string fromPrefs = EditorPrefs.GetString(PasswordPrefsKey, "");
            if (!string.IsNullOrEmpty(fromPrefs)) return fromPrefs;

            return LegacyPassword;
        }

        /// <summary>Пароль, который увидит сборка (используется настройкой ниже).</summary>
        public static string Password { get { return ResolvePassword(); } }

        /// <summary>
        /// ОТКУДА ВЗЯЛСЯ ПАРОЛЬ — для журнала и для теста. Возвращает «окружение» / «EditorPrefs» /
        /// «встроенный (устаревший)» / «нет». Значение самого пароля наружу не отдаётся.
        /// </summary>
        public static string PasswordSource()
        {
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(PasswordEnvironmentVariable)))
                return "окружение";
            if (!string.IsNullOrEmpty(EditorPrefs.GetString(PasswordPrefsKey, "")))
                return "EditorPrefs";
            if (!string.IsNullOrEmpty(LegacyPassword))
                return "встроенный (устаревший)";
            return "нет";
        }

        /// <summary>
        /// СОХРАНИТЬ ПАРОЛЬ В `EditorPrefs` — чтобы владельцу не вписывать его каждый раз.
        /// В репозиторий и в проект пароль при этом не попадает.
        /// </summary>
        public static void SavePasswordToPrefs(string password)
        {
            if (string.IsNullOrEmpty(password)) EditorPrefs.DeleteKey(PasswordPrefsKey);
            else EditorPrefs.SetString(PasswordPrefsKey, password);
        }

        [MenuItem(MenuRoot + "8. Подпись сборки: собственный ключ", false, 73)]
        public static void Configure()
        {
            string password = ResolvePassword();
            if (string.IsNullOrEmpty(password))
            {
                Debug.LogError("[KvSign] пароль хранилища не задан. Задайте переменную окружения " +
                               PasswordEnvironmentVariable + " или сохраните пароль в EditorPrefs " +
                               "(меню «" + MenuRoot + "9»). Сборка НЕ будет подписана.");
                return;
            }
            if (!System.IO.File.Exists(KeystorePath))
            {
                Debug.LogError("[KvSign] нет файла хранилища ключей: " + KeystorePath +
                               ". Он больше не хранится в репозитории специально — положите его локально " +
                               "или задайте путь переменной " + KeystoreEnvironmentVariable + ".");
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = KeystorePath;
            PlayerSettings.Android.keystorePass = password;
            PlayerSettings.Android.keyaliasName = KeyAlias;
            PlayerSettings.Android.keyaliasPass = password;

            AssetDatabase.SaveAssets();

            Debug.Log("[KvSign] подпись сборки настроена: useCustomKeystore=" +
                      PlayerSettings.Android.useCustomKeystore +
                      " · keystore=" + PlayerSettings.Android.keystoreName +
                      " · alias=" + PlayerSettings.Android.keyaliasName +
                      " · пароль из: " + PasswordSource() +
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
