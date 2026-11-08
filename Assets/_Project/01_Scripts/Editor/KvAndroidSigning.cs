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
        /// ПАРОЛЬ ХРАНИЛИЩА БОЛЬШЕ НЕ ЛЕЖИТ В ЭТОМ ФАЙЛЕ (удалено 11.11.2026).
        ///
        /// ЧТО БЫЛО. Здесь стояла приватная константа с ПАРОЛЕМ ДЕЙСТВУЮЩЕГО КЛЮЧА
        /// подписи — прямо в ОТСЛЕЖИВАЕМОМ исходнике, да ещё и как последний шаг `ResolvePassword()`.
        /// Сам keystore при этом лежал в истории git (blob `ae0263e`). Вместе это означало: любой,
        /// у кого есть репозиторий, может выпускать обновления от имени владельца.
        ///
        /// ПОЧЕМУ УДАЛЕНО, А НЕ «ПОМЕЧЕНО УСТАРЕВШИМ». Метка `[Obsolete]` не прячет строку: пароль
        /// остаётся в файле и в истории. Единственное лечение — убрать значение и заставить сборку
        /// ЧЕСТНО падать, если владелец не передал пароль.
        ///
        /// ЧТО ТЕПЕРЬ. Пароль берётся ТОЛЬКО из переменной окружения `KAZISTOVVV_KEYPASS` или из
        /// `EditorPrefs` (пункт меню 9). Нет пароля — сборка не начнётся, и в сообщении будет
        /// написано, что именно задать.
        ///
        /// ЧТО СДЕЛАТЬ ВЛАДЕЛЬЦУ (это НЕ задача агента): ключ считать утёкшим, выпустить новый
        /// keystore, а старый удалить из истории (`git filter-repo`).
        /// </summary>

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
        /// Порядок: переменная окружения → `EditorPrefs`. Встроенного значения БОЛЬШЕ НЕТ (см.
        /// комментарий выше): последний шаг раньше возвращал пароль из исходника, и это делало
        /// секрет общедоступным. Возвращает `null`, если пароля нет нигде, — и вызывающий обязан
        /// это обработать, а не подставить что-нибудь похожее.
        /// </summary>
        public static string ResolvePassword()
        {
            string fromEnv = System.Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
            if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;

            string fromPrefs = EditorPrefs.GetString(PasswordPrefsKey, "");
            if (!string.IsNullOrEmpty(fromPrefs)) return fromPrefs;

            return null;
        }

        /// <summary>Пароль, который увидит сборка (может быть null — тогда подпись невозможна).</summary>
        public static string Password { get { return ResolvePassword(); } }

        /// <summary>
        /// ОТКУДА ВЗЯЛСЯ ПАРОЛЬ — для журнала и для теста. Возвращает «окружение» / «EditorPrefs» /
        /// «нет». Значение самого пароля наружу не отдаётся.
        /// </summary>
        public static string PasswordSource()
        {
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(PasswordEnvironmentVariable)))
                return "окружение";
            if (!string.IsNullOrEmpty(EditorPrefs.GetString(PasswordPrefsKey, "")))
                return "EditorPrefs";
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

        /// <summary>
        /// ПОПЫТАТЬСЯ НАСТРОИТЬ ПОДПИСЬ. Возвращает false и ПРИЧИНУ, если не вышло.
        ///
        /// ВЫДЕЛЕНО ИЗ `Configure()` 11.11.2026. Раньше `Configure()` был `void` и при неудаче
        /// просто писал ошибку и выходил. Сборка при этом продолжалась с `useCustomKeystore = 1`
        /// и без паролей, и падала сообщением
        ///     UnityException: Unable to sign the Android application / No keystore passwords…
        /// которое указывает на ПАРОЛИ, хотя причиной чаще был отсутствующий ФАЙЛ ключа. Теперь
        /// причину можно вернуть вызывающему и показать одну понятную строку.
        /// </summary>
        public static bool TryConfigure(out string error)
        {
            error = null;

            string password = ResolvePassword();
            if (string.IsNullOrEmpty(password))
            {
                error = "пароль хранилища ключей не задан. Задайте переменную окружения " +
                        PasswordEnvironmentVariable + " (её видит и CI) либо сохраните пароль в " +
                        "EditorPrefs вызовом KvAndroidSigning.SavePasswordToPrefs(...) из пункта " +
                        "меню/скрипта. Встроенного пароля в коде БОЛЬШЕ НЕТ — он удалён как утёкший.";
                return false;
            }

            if (!System.IO.File.Exists(KeystorePath))
            {
                error = "нет файла хранилища ключей: " + KeystorePath +
                        ". Он намеренно не хранится в репозитории — положите файл локально " +
                        "или задайте путь переменной " + KeystoreEnvironmentVariable + ".";
                return false;
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
            return true;
        }

        /// <summary>
        /// НАСТРОИТЬ ПОДПИСЬ ИЗ ПУНКТА МЕНЮ: ошибка — в журнал, диалог не нужен.
        /// </summary>
        [MenuItem(MenuRoot + "8. Подпись сборки: собственный ключ", false, 73)]
        public static void Configure()
        {
            string error;
            if (!TryConfigure(out error))
                Debug.LogError("[KvSign] " + error);
        }

        /// <summary>
        /// НАСТРОИТЬ ПОДПИСЬ ПЕРЕД СБОРКОЙ И УПАСТЬ, ЕСЛИ НЕЛЬЗЯ.
        ///
        /// ЗАЧЕМ БРОСАТЬ (11.11.2026). Сборка без настроенной подписи не «соберётся как-нибудь»:
        /// она упадёт в Gradle сообщением про пароли. Лучше остановиться ДО неё и назвать
        /// настоящую причину — тогда CI-задача `android-build` и `Tools/ci-local.ps1 -Build`
        /// дают владельцу инструкцию, а не загадку.
        /// </summary>
        public static void ConfigureOrThrow()
        {
            string error;
            if (TryConfigure(out error))
                return;
            throw new UnityEditor.Build.BuildFailedException(
                "[KvSign] подпись не настроена, сборка остановлена. " + error);
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
