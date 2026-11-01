<#
    СБОРКА И УСТАНОВКА APK ОДНОЙ КОМАНДОЙ.

    ЗАЧЕМ. 31.10.2026 сборка APK из консоли не работала ВООБЩЕ, и на это ушло несколько часов.
    Нашлись ДВЕ причины (подробно — `Docs/FIXES.md`, E-24), и обе теперь обходятся здесь:

      1. ПОДПИСЬ НЕ НАСТРАИВАЛАСЬ. `KvAndroidSigning.Configure()` вызывался только из пункта меню,
         а в пакетном режиме меню никто не нажимает. Теперь вызов добавлен в сам метод сборки
         (`KvXrTools.BuildAndroidApk`), а пароль этот скрипт передаёт через переменную окружения.

      2. КЭШ GRADLE ЛЕЖАЛ В ПАПКЕ С КИРИЛЛИЦЕЙ. Java не находила класс
         `com.google.prefab.cli.AppKt`, потому что путь `C:\Users\...\ .gradle\...` уходил в
         командную строку в неверной кодировке. Скрипт выставляет латинский `GRADLE_USER_HOME`
         через junction — и проблема исчезает.

    ЧТО ДЕЛАЕТ:
      1. находит Unity, ключ и пароль;
      2. готовит латинский `GRADLE_USER_HOME` (junction, права не нужны);
      3. запускает сборку с правильным окружением;
      4. по желанию ставит APK на подключённый шлем и запускает.

    ПРИМЕРЫ:
      .\Tools\build-android.ps1                  # только собрать
      .\Tools\build-android.ps1 -Install         # собрать и поставить на шлем
      .\Tools\build-android.ps1 -Install -Run    # собрать, поставить, запустить

    ВОЗВРАТ: 0 — успех; 1 — не удалось (причина в выводе).

    ТРЕБОВАНИЯ: .ps1 в проекте обязаны быть в UTF-8 С BOM (дефект A19) — этот файл уже с BOM.
#>
param(
    [switch]$Install,                                  # поставить на подключённый шлем
    [switch]$Run,                                      # запустить после установки
    [string]$Project,                                  # корень проекта (по умолчанию — этот)
    [string]$Keystore,                                 # путь к ключу (по умолчанию — в проекте)
    [string]$KeyPass = $env:KAZISTOVVV_KEYPASS,        # пароль (по умолчанию — из окружения)
    [string]$GradleHome = "C:\GradleHome",             # латинский путь для кэша Gradle
    [string]$Adb                                       # путь к adb (найдётся сам при необходимости)
)

$ErrorActionPreference = 'Stop'

# ------------------------------------------------------------------ 0. ГДЕ МЫ

if (-not $Project) {
    $Project = Split-Path -Parent $PSScriptRoot
}
if (-not (Test-Path $Project)) {
    Write-Host "НЕТ проекта по пути: $Project" -ForegroundColor Red
    exit 1
}

$projectAscii = $Project -match '^[\x00-\x7F]+$'
Write-Host "=== СБОРКА APK ==="
Write-Host "  проект: $Project"
if (-not $projectAscii) {
    Write-Host "  путь содержит НЕ-ASCII символы — сборка идёт через латинскую копию (так и задумано)"
}

# ------------------------------------------------------------------ 1. ПАРОЛЬ ПОДПИСИ

# Порядок: параметр → переменная окружения → EditorPrefs (не читаем отсюда: это редактор Unity).
# Встроенный в код пароль намеренно НЕ дублируем: если его нет в окружении, сборка честно скажет.

if (-not $KeyPass) {
    Write-Host ""
    Write-Host "ПАРОЛЬ КЛЮЧА НЕ ЗАДАН." -ForegroundColor Yellow
    Write-Host "  Задайте переменную окружения KAZISTOVVV_KEYPASS или параметр -KeyPass."
    Write-Host "  Без пароля сборка упадёт с «No keystore passwords were found»."
    Write-Host ""
    Write-Host "  Подсказка: пароль записан в KvAndroidSigning.LegacyPassword (Assets/_Project/01_Scripts/Editor)."
}

# Ключ: параметр → переменная окружения → файл в проекте.
if (-not $Keystore) {
    $Keystore = $env:KAZISTOVVV_KEYSTORE
}
if (-not $Keystore) {
    $candidate = Join-Path $Project 'keystore\kazistovvv.keystore'
    if (Test-Path $candidate) { $Keystore = $candidate }
}

if ($Keystore -and (Test-Path $Keystore)) {
    Write-Host "  ключ: $Keystore"
} else {
    Write-Host "  ключ: НЕ НАЙДЕН — сборка будет без подписи" -ForegroundColor Yellow
}

# ------------------------------------------------------------------ 2. ЛАТИНСКИЙ GRADLE_USER_HOME

# ЭТО ГЛАВНЫЙ ОБХОД. Gradle хранит кэш в домашней папке пользователя. Если в её имени есть
# кириллица, Java получает путь в неверной кодировке и не находит классы prefab.
# Junction (не копия!) перенаправляет на латинский путь, не занимая места.

$userGradle = Join-Path $env:USERPROFILE '.gradle'

if ($GradleHome -match '^[\x00-\x7F]+$') {
    if (-not (Test-Path $GradleHome)) {
        if (Test-Path $userGradle) {
            New-Item -ItemType Junction -Path $GradleHome -Target $userGradle -ErrorAction SilentlyContinue | Out-Null
        } else {
            New-Item -ItemType Directory -Path $GradleHome -Force | Out-Null
        }
    }

    if (Test-Path $GradleHome) {
        $env:GRADLE_USER_HOME = $GradleHome
        Write-Host "  кэш Gradle: $GradleHome (латинский путь)"
    } else {
        Write-Host "  кэш Gradle: НЕ УДАЛОСЬ подготовить $GradleHome — сборка может упасть на prefab" -ForegroundColor Yellow
    }
} else {
    Write-Host "  кэш Gradle: путь содержит не-ASCII, обход не применён" -ForegroundColor Yellow
}

# ------------------------------------------------------------------ 3. ОКРУЖЕНИЕ СБОРКИ

if ($KeyPass) { $env:KAZISTOVVV_KEYPASS = $KeyPass }
if ($Keystore) { $env:KAZISTOVVV_KEYSTORE = $Keystore }

# ------------------------------------------------------------------ 4. СБОРКА

$builder = Join-Path $PSScriptRoot 'xr-build-apk-ascii.ps1'
if (-not (Test-Path $builder)) {
    Write-Host "НЕТ скрипта сборки: $builder" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== ЗАПУСК СБОРКИ (может занять 10-90 минут) ==="

& powershell -ExecutionPolicy Bypass -File $builder
$buildExit = $LASTEXITCODE

if ($buildExit -ne 0) {
    Write-Host ""
    Write-Host "СБОРКА НЕ УДАЛАСЬ (код $buildExit)." -ForegroundColor Red
    Write-Host "  Смотрите: C:\KazistovVv_Build\build.log"
    Write-Host "  Частые причины — Docs/FIXES.md, E-24 (подпись и кириллица в пути)."
    exit 1
}

# ------------------------------------------------------------------ 5. ГДЕ APK

$apkCandidates = @(
    (Join-Path $Project 'Builds\KazistovVv.apk'),
    'C:\KazistovVv_Build\Builds\KazistovVv.apk',
    'C:\KazistovVv_Build\KazistovVv.apk'
)

$apk = $null
foreach ($candidate in $apkCandidates) {
    if (Test-Path $candidate) { $apk = $candidate; break }
}

if (-not $apk) {
    Write-Host "СБОРКА ЗАВЕРШИЛАСЬ, но APK не найден ни по одному пути." -ForegroundColor Red
    exit 1
}

$sizeMb = [math]::Round((Get-Item $apk).Length / 1MB, 1)
Write-Host ""
Write-Host "APK: $apk ($sizeMb МБ)" -ForegroundColor Green

# ------------------------------------------------------------------ 6. УСТАНОВКА (по флагу)

if (-not $Install) {
    Write-Host "Поставить на шлем: .\Tools\build-android.ps1 -Install -Run"
    exit 0
}

if (-not $Adb) {
    $Adb = Get-Command adb -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
}
if (-not $Adb) {
    $Adb = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
}
if (-not (Test-Path $Adb)) {
    Write-Host "НЕ НАЙДЕН adb. Укажите -Adb <путь>." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== УСТАНОВКА НА ШЛЕМ ==="

$devices = & $Adb devices 2>&1 | Select-String -Pattern "\sdevice$"
if (-not $devices) {
    Write-Host "ШЛЕМ НЕ ПОДКЛЮЧЁН. Подключите по USB и включите отладку." -ForegroundColor Red
    exit 1
}

Write-Host "  устройств: $($devices.Count)"
& $Adb install -r $apk
if ($LASTEXITCODE -ne 0) {
    Write-Host "УСТАНОВКА НЕ УДАЛАСЬ." -ForegroundColor Red
    Write-Host "  Если «signatures do not match» — на шлеме сборка другим ключом:"
    Write-Host "    adb uninstall com.KazistovVv.app"
    exit 1
}

Write-Host "  установлено" -ForegroundColor Green

# ------------------------------------------------------------------ 7. ЗАПУСК (по флагу)

if ($Run) {
    Write-Host ""
    Write-Host "=== ЗАПУСК ==="
    & $Adb shell am start -n com.KazistovVv.app/com.unity3d.player.UnityPlayerGameActivity | Out-Null
    Write-Host "  приложение запущено" -ForegroundColor Green
    Write-Host ""
    Write-Host "НАДЕНЬТЕ ШЛЕМ — без него трекинг не работает и проверять нечего."
}

Write-Host ""
Write-Host "ГОТОВО."
exit 0
