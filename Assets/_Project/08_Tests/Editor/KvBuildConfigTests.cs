// ================================================================================================
//  KvBuildConfigTests.cs — EditMode-тесты КОНФИГУРАЦИИ СБОРКИ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Собрать APK на этой машине нельзя (нет шлема для проверки, сборка ~70 минут), но
//  ПРОВЕРИТЬ настройки, без которых APK не поставится или не запустится, — можно и нужно. Ошибка
//  здесь обнаруживается владельцем через час сборки, а не мной через секунду.
//
//  ЧТО ПРОВЕРЯЕТСЯ (по `ProjectSettings/ProjectSettings.asset` и `ProjectVersion.txt`):
//    1) минимальная и целевая версия Android соответствуют требованиям PICO;
//    2) архитектура включает ARM64 (32-битная сборка не встанет на современный шлем);
//    3) идентификатор приложения задан и похож на пакет Android;
//    4) номер версии задан (без него не обновиться поверх предыдущей установки);
//    5) версия Unity в проекте — та, под которую собран пакет XR.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Настройки сборки Android/шлема: то, без чего APK не заработает.</summary>
    public class KvBuildConfigTests
    {
        private static string Root
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        private static string ProjectSettingsPath
        {
            get { return Path.Combine(Root, "ProjectSettings", "ProjectSettings.asset"); }
        }

        private static string SettingsText()
        {
            string path = ProjectSettingsPath;
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>Значение числового/строкового поля из `ProjectSettings.asset`.</summary>
        private static string Field(string name)
        {
            string text = SettingsText();
            if (text == null) return null;
            Match m = Regex.Match(text, @"^\s*" + Regex.Escape(name) + @":\s*(.+?)\s*$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        /// <summary>
        /// ФАЙЛ НАСТРОЕК ЧИТАЕТСЯ. Без него следующие проверки «пройдут» на пустоте — тест зеленел бы
        /// по определению (E-16).
        /// </summary>
        [Test]
        public void ProjectSettingsAreReadable()
        {
            string text = SettingsText();
            Assert.IsNotNull(text, "нет ProjectSettings/ProjectSettings.asset");
            Assert.Greater(text.Length, 5000, "файл настроек подозрительно мал");
        }

        // ---------------------------------------------------------------- ANDROID SDK

        /// <summary>
        /// МИНИМАЛЬНАЯ ВЕРСИЯ ANDROID НЕ НИЖЕ 29. PICO 4 работает на Android 10 (API 29) и выше.
        /// Слишком низкий минимум не мешает установке, но разрешает системе запустить приложение в
        /// режиме совместимости, где XR-расширения ведут себя непредсказуемо.
        ///
        /// В проекте стоит 32 — это выше требуемого, и это правильно: значение проверяется как
        /// «не ниже порога», а не «равно».
        /// </summary>
        [Test]
        public void AndroidMinSdkIsSufficient()
        {
            string raw = Field("AndroidMinSdkVersion");
            Assert.IsNotNull(raw, "в настройках нет AndroidMinSdkVersion");
            Assert.IsTrue(int.TryParse(raw, out int value), "AndroidMinSdkVersion не число: " + raw);

            Assert.GreaterOrEqual(value, 29,
                "минимальная версия Android " + value + " ниже 29 — на PICO приложение может " +
                "запуститься в режиме совместимости, где XR работает непредсказуемо");
        }

        /// <summary>
        /// ЦЕЛЕВАЯ ВЕРСИЯ ANDROID НЕ НИЖЕ 32. Google Play и современные прошивки PICO требуют
        /// targetSdk не ниже 32; при меньшем значении установка из магазина отклоняется.
        /// </summary>
        [Test]
        public void AndroidTargetSdkIsSufficient()
        {
            string raw = Field("AndroidTargetSdkVersion");
            Assert.IsNotNull(raw, "в настройках нет AndroidTargetSdkVersion");
            Assert.IsTrue(int.TryParse(raw, out int value), "AndroidTargetSdkVersion не число: " + raw);

            Assert.GreaterOrEqual(value, 32,
                "целевая версия Android " + value + " ниже 32 — установка из магазина будет отклонена");
        }

        /// <summary>
        /// АРХИТЕКТУРА ВКЛЮЧАЕТ ARM64. В Unity это битовая маска: 1 — ARMv7, 2 — ARM64.
        /// Только 32-битная сборка не запустится на современных шлемах, а на некоторых прошивках
        /// PICO установка будет отклонена.
        /// </summary>
        [Test]
        public void AndroidArchitectureIncludesArm64()
        {
            string raw = Field("AndroidTargetArchitectures");
            Assert.IsNotNull(raw, "в настройках нет AndroidTargetArchitectures");
            Assert.IsTrue(int.TryParse(raw, out int mask), "AndroidTargetArchitectures не число: " + raw);

            Assert.IsTrue((mask & 2) != 0,
                "в архитектурах нет ARM64 (маска " + mask + ") — APK не запустится на современном шлеме");
        }

        // ---------------------------------------------------------------- ИДЕНТИФИКАТОР

        /// <summary>
        /// ИДЕНТИФИКАТОР ПРИЛОЖЕНИЯ ЗАДАН И ПОХОЖ НА ПАКЕТ ANDROID. Пустой или кривой идентификатор
        /// (например с пробелом) роняет сборку на этапе упаковки — то есть после часа компиляции.
        /// </summary>
        [Test]
        public void AndroidApplicationIdIsValid()
        {
            string text = SettingsText();
            Match block = Regex.Match(text, @"applicationIdentifier:(.*?)(?=\n  \w|\z)",
                RegexOptions.Singleline);
            Assert.IsTrue(block.Success, "в настройках нет блока applicationIdentifier");

            Match android = Regex.Match(block.Groups[1].Value, @"Android:\s*(\S+)");
            Assert.IsTrue(android.Success, "не задан идентификатор приложения для Android");

            string id = android.Groups[1].Value;
            Assert.IsTrue(Regex.IsMatch(id, @"^[a-zA-Z][\w]*(\.[a-zA-Z][\w]*)+$"),
                "идентификатор Android «" + id + "» не соответствует формату пакета " +
                "(нужно вида com.company.app) — сборка упадёт на упаковке");
        }

        /// <summary>
        /// НОМЕР ВЕРСИИ ЗАДАН. Без него нельзя установить обновление поверх предыдущей версии:
        /// Android считает, что версия не изменилась, и отказывает в установке.
        /// </summary>
        [Test]
        public void BundleVersionAndCodeAreSet()
        {
            string version = Field("bundleVersion");
            Assert.IsNotNull(version, "не задан bundleVersion");
            Assert.IsTrue(Regex.IsMatch(version, @"^\d+(\.\d+)+$"),
                "bundleVersion «" + version + "» не похож на версию (ожидается вида 1.2.3)");

            string code = Field("AndroidBundleVersionCode");
            Assert.IsNotNull(code, "не задан AndroidBundleVersionCode");
            Assert.IsTrue(int.TryParse(code, out int codeValue), "AndroidBundleVersionCode не число: " + code);
            Assert.Greater(codeValue, 0,
                "AndroidBundleVersionCode = 0 — Android не установит обновление поверх предыдущей версии");
        }

        // ---------------------------------------------------------------- ВЕРСИЯ UNITY

        /// <summary>
        /// ВЕРСИЯ UNITY ЗАДАНА И НЕ ПУСТА. Пакеты XR собраны под конкретную версию; при
        /// несовпадении часть API молча расходится, и ошибки появляются на устройстве, а не здесь.
        /// </summary>
        [Test]
        public void UnityVersionIsPinned()
        {
            string path = Path.Combine(Root, "ProjectSettings", "ProjectVersion.txt");
            Assert.IsTrue(File.Exists(path), "нет ProjectSettings/ProjectVersion.txt");

            string text = File.ReadAllText(path);
            Match m = Regex.Match(text, @"m_EditorVersion:\s*(\S+)");
            Assert.IsTrue(m.Success, "не удалось прочитать версию Unity");

            string version = m.Groups[1].Value;
            Assert.IsTrue(Regex.IsMatch(version, @"^\d{4}\.\d+\.\d+f\d+$"),
                "версия Unity «" + version + "» записана не в стандартном виде — сборка в CI может " +
                "взять другую версию");
        }

        /// <summary>
        /// КАСТОМНЫЙ МАНИФЕСТ НЕ ВКЛЮЧЁН. Проект правит манифест через пост-обработчик
        /// (`KvPicoManifestMetaData`), а не заменяет его целиком. Включённый `useCustomMainManifest`
        /// означал бы, что пост-обработчик может не сработать, и метаданные PICO не попадут в APK —
        /// а без них сквозной вид и разрешения не работают.
        /// </summary>
        [Test]
        public void CustomManifestIsNotEnabledUnexpectedly()
        {
            string raw = Field("useCustomMainManifest");
            if (raw == null) Assert.Pass("поля нет — используется стандартный манифест");

            Assert.AreEqual("0", raw,
                "включён свой главный манифест (useCustomMainManifest = " + raw + "): пост-обработчик " +
                "KvPicoManifestMetaData может не сработать, и метаданные PICO (сквозной вид, " +
                "разрешения) не попадут в APK");
        }
    }
}
