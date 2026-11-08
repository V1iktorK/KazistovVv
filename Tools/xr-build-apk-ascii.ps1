<#
    ПРОВЕРКА СБОРКИ APK ПО ЛАТИНСКОМУ ПУТИ (сессия §28).

    ЗАЧЕМ. Android-инструменты Unity (Gradle) отказываются работать, если ПУТЬ ПРОЕКТА
    содержит не-ASCII символы: «Invalid project path … contains non-ASCII characters».
    Рабочий каталог владельца — «…\новое пространство\KazistovVv», и выше по пути есть
    «Ольга». Этот скрипт ДОКАЗЫВАЕТ (или опровергает), что дело именно в пути: он делает
    копию проекта в латинский каталог, связывает тяжёлые каталоги (Library, Temp, obj)
    с оригиналом через junction (копирование 8 ГБ не требуется) и собирает APK.
    Оригинал при этом НЕ изменяется: в него пишет только Unity — в ту же Library, что
    и при обычной работе редактора.

    Возврат: 0 — APK собран; 1 — не собран (причина в логе).
#>
param(
    [string]$Source = "C:\Users\Ольга\OneDrive\Documentos\новое пространство\KazistovVv",
    [string]$Target = "C:\KazistovVv_Build",
    [string]$Unity  = "",
    [int]$TimeoutMinutes = 45,
    # ВАРИАНТ ГРАФИЧЕСКОГО API (добавлено 11.11.2026).
    #
    # ЗАЧЕМ ПАРАМЕТР. У проекта ДВА варианта сборки из одного кода, и это не прихоть:
    # `KvXrTools.cs:1034-1036` фиксирует, что на планшете Redmi Pad 2 Pro Unity 6.5/Vulkan
    # НЕ ВЫВОДИТ КАДРЫ (чёрный экран при живом процессе), а на шлеме PICO с GLES3 не
    # активируется VR-дисплей. Выбор делался переменной окружения `KAZISTOVVV_GFX`, но её не
    # выставлял НИ ОДИН скрипт и ни одна задача CI — то есть вариант для планшета (а именно на нём
    # владелец проверяет AR) собрать документированной командой было НЕЛЬЗЯ. Проверено:
    # Vulkan-сборка на подключённом планшете даёт ПОЛНОСТЬЮ чёрный экран при живом процессе и
    # нуле ошибок в логе.
    #   -Gfx headset (по умолчанию) — Vulkan, для шлема PICO;
    #   -Gfx tablet                  — OpenGLES3, для планшета/телефона (AR).
    [ValidateSet('headset', 'tablet')]
    [string]$Gfx = 'headset'
)

$ErrorActionPreference = 'Continue'

# ================================================================================================
# ПУТЬ К UNITY — ИЗ ПРОЕКТА, А НЕ ЖЁСТКИЙ (P4-12, 12.11.2026).
#
# ЧТО БЫЛО. Версия движка была ВПИСАНА в скрипт: "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\...".
# После обновления Unity скрипт искал несуществующий каталог и падал «не найден Unity», хотя проект
# уже собирался другой версией. Версия проекта и так записана в `ProjectSettings\ProjectVersion.txt`
# — второго источника быть не должно.
#
# ЧТО СТАЛО. Путь по умолчанию пустой и ВЫЧИСЛЯЕТСЯ из ProjectVersion.txt; явный `-Unity` главнее.
# ================================================================================================
if (-not $Unity) {
    $versionFile = Join-Path $Source 'ProjectSettings\ProjectVersion.txt'
    if (Test-Path $versionFile) {
        $found = Select-String -Path $versionFile -Pattern 'm_EditorVersion:\s*(\S+)'
        if ($found) {
            $Unity = Join-Path $env:ProgramFiles ("Unity\Hub\Editor\" + $found.Matches[0].Groups[1].Value + "\Editor\Unity.exe")
        }
    }
    if (-not $Unity -or -not (Test-Path $Unity)) {
        Write-Host "ОШИБКА: Unity.exe не найден; версия из ProjectVersion.txt: $(if (Test-Path $versionFile) { (Get-Content $versionFile -TotalCount 1) } else { 'файла нет' }). Задайте путь ключом -Unity."
        exit 1
    }
}
$log = Join-Path $Target 'build.log'

# ВАРИАНТ ГРАФИЧЕСКОГО API — ВЫСТАВЛЯЕМ ДО ЗАПУСКА UNITY. `KvXrTools` читает эту переменную и
# выбирает `SetGraphicsAPIs` (см. `KvXrTools.cs:1033-1040`). По умолчанию — шлем (Vulkan).
if ($Gfx -eq 'tablet') {
    $env:KAZISTOVVV_GFX = 'gl'
    Write-Host "  вариант сборки: ПЛАНШЕТ/ТЕЛЕФОН (OpenGLES3, KAZISTOVVV_GFX=gl)"
} else {
    $env:KAZISTOVVV_GFX = $null
    Write-Host "  вариант сборки: ШЛЕМ (Vulkan)"
}

Write-Host "=== [1/3] ПОДГОТОВКА ASCII-КОПИИ ==="
if (Test-Path $Target) {
    Write-Host "Целевой каталог уже есть: $Target (переиспользуем)"
} else {
    New-Item -ItemType Directory -Path $Target -Force | Out-Null
}

# Тяжёлые каталоги НЕ копируем — связываем с оригиналом (junction):
# так мы не тратим 8 ГБ и минуты, а Unity работает с той же библиотекой.
$junctionDirs = @('Library', 'Temp', 'obj', 'Logs', 'UserSettings', 'Builds')
$copyDirs     = @('Assets', 'Packages', 'ProjectSettings', 'Tools', 'Docs')

foreach ($d in $junctionDirs) {
    $src = Join-Path $Source $d
    $dst = Join-Path $Target $d
    if (-not (Test-Path $src)) { continue }
    if (Test-Path $dst) { Write-Host "  уже есть: $d"; continue }
    cmd /c mklink /J "`"$dst`"" "`"$src`"" | Out-Null
    Write-Host "  связан: $d"
}

foreach ($d in $copyDirs) {
    $src = Join-Path $Source $d
    $dst = Join-Path $Target $d
    if (-not (Test-Path $src)) { continue }
    robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
    Write-Host "  скопирован: $d"
}

# Одиночные файлы проекта, нужные Unity и сборке.
foreach ($f in @('PROJECT_CONTEXT.md')) {
    $src = Join-Path $Source $f
    if (Test-Path $src) { Copy-Item $src $Target -Force }
}

# ================================================================================================
# КЛЮЧ ПОДПИСИ: ПЕРЕДАЁТСЯ ПУТЁМ, А НЕ КОПИРОВАНИЕМ (исправлено 11.11.2026).
#
# ЧТО БЫЛО. Папка `keystore` не входила в список копируемых, а `KvAndroidSigning.KeystorePath` по
# умолчанию смотрит ровно в `C:\KazistovVv_Build\keystore\kazistovvv.keystore`. Файла там нет →
# `Configure()` пишет «нет файла хранилища ключей» и ВЫХОДИТ, не выставив пароли. При этом в
# `ProjectSettings.asset` остаётся `androidUseCustomKeystore: 1`, и сборка падает на
#     UnityException: Unable to sign the Android application
#     No keystore passwords were found
# То есть сборка APK из консоли была НЕВОЗМОЖНА, а сообщение указывало на «пароли», хотя дело было
# в отсутствующем ФАЙЛЕ. Проверено прогоном 11.11.2026.
#
# ЧТО СТАЛО. Ключ НЕ копируется (это секрет — второй экземпляр на диске не нужен), а путь
# передаётся переменной окружения `KAZISTOVVV_KEYSTORE`, которую `KvAndroidSigning` читает первым
# делом. Нет ключа вовсе — предупреждение ДО сборки, а не загадочная ошибка подписи после неё.
# ================================================================================================
if (-not $env:KAZISTOVVV_KEYSTORE) {
    foreach ($k in @((Join-Path $Source 'keystore\kazistovvv.keystore'),
                     (Join-Path $Target 'keystore\kazistovvv.keystore'))) {
        if (Test-Path -LiteralPath $k) {
            $env:KAZISTOVVV_KEYSTORE = (Resolve-Path -LiteralPath $k).Path
            Write-Host "  ключ подписи: $($env:KAZISTOVVV_KEYSTORE) (путь передан окружением)"
            break
        }
    }
}
if (-not $env:KAZISTOVVV_KEYSTORE) {
    Write-Host "  ключ подписи НЕ НАЙДЕН — сборка пойдёт БЕЗ подписи" -ForegroundColor Yellow
    Write-Host "    задайте KAZISTOVVV_KEYSTORE (путь к .keystore) и KAZISTOVVV_KEYPASS (пароль)"
}

# ================================================================================================
# ЛАТИНСКИЙ КЭШ GRADLE — ВНУТРИ САМОГО СБОРЩИКА (исправлено 11.11.2026).
#
# ЧТО БЫЛО. Обход кириллицы в пути к кэшу Gradle жил ТОЛЬКО в обёртке `Tools/build-android.ps1`
# (она выставляла `GRADLE_USER_HOME=C:\GradleHome` и уже потом звала этот скрипт). Прямой вызов
# сборщика — а именно так его зовёт `Tools/ci-local.ps1 -Build` и задачи CI — обход не получал, и
# сборка падала на ровно том дефекте, ради которого обход и завели:
#     Error: Could not find or load main class com.google.prefab.cli.AppKt
#     Caused by: java.lang.ClassNotFoundException (путь C:\Users\<кириллица>\.gradle\…)
# То есть «одна команда сборки» работала, а её собственная внутренность — нет.
#
# ЧТО СТАЛО. Скрипт сам готовит латинский `GRADLE_USER_HOME` (junction на настоящий `~\.gradle`,
# места не занимает) и выставляет переменную окружения. Теперь он самодостаточен при любом вызове.
# ================================================================================================
if (-not $env:GRADLE_USER_HOME -or $env:GRADLE_USER_HOME -match '[^\x00-\x7F]') {
    $latinGradle = 'C:\GradleHome'
    $userGradle = Join-Path $env:USERPROFILE '.gradle'
    if (-not (Test-Path -LiteralPath $latinGradle)) {
        try {
            if (Test-Path -LiteralPath $userGradle) {
                New-Item -ItemType Junction -Path $latinGradle -Target $userGradle -ErrorAction Stop | Out-Null
            } else {
                New-Item -ItemType Directory -Path $latinGradle -Force -ErrorAction Stop | Out-Null
            }
        } catch {
            Write-Host "  кэш Gradle: НЕ удалось подготовить $latinGradle — $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    if (Test-Path -LiteralPath $latinGradle) {
        $env:GRADLE_USER_HOME = $latinGradle
        Write-Host "  кэш Gradle: $latinGradle (латинский путь — иначе Java не находит prefab)"
    }
}

Write-Host ""
Write-Host "=== [2/3] СБОРКА APK (латинский путь) ==="

# ================================================================================================
# ОСТАТКИ XR-СИМУЛЯЦИИ УБИРАЮТСЯ ДО СБОРКИ (задача P0-11, 11.11.2026).
#
# ЧТО БЫЛО. Сборщик AR Foundation (`SimulationSettingsBuildProcessor`) на время сборки переносит
# `XRSimulationPreferences` и `XRSimulationRuntimeSettings` в `Assets/XR/Temp`, а после сборки
# возвращает их назад. Если сборка прервалась (или редактор закрыли), ассеты ОСТАЮТСЯ в Temp —
# и следующая сборка уже не может их перенести:
#     Failed to move the asset (…) to a temporary location: … Destination path name does not exist
# Факт 11.11.2026: в проекте лежали остатки от 03.10.2026 — каталог `Assets/XR/Temp` с обоими
# ассетами. Git их не видит (`.gitignore` строка 178), поэтому дефект жил незаметно.
#
# ЧТО СТАЛО. Перед сборкой каталог остатков удаляется и в источнике, и в ASCII-копии: правильные
# копии живут в `Assets/XR/Resources` и `Assets/XR/UserSimulationSettings/Resources`.
# ================================================================================================
foreach ($root in @($Source, $Target)) {
    $xrTemp = Join-Path $root 'Assets/XR/Temp'
    if (Test-Path -LiteralPath $xrTemp) {
        try {
            Get-ChildItem -LiteralPath $xrTemp -Force | Remove-Item -Recurse -Force -ErrorAction Stop
            Remove-Item -LiteralPath $xrTemp -Force -ErrorAction Stop
            Write-Host "  остатки XR-симуляции убраны: $xrTemp (иначе перенос ассетов на сборке падает)"
        } catch {
            Write-Host "  НЕ удалось убрать $xrTemp : $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

# ================================================================================================
# СТАРЫЙ APK УБИРАЕТСЯ ДО СБОРКИ, И ЭТО ПРИНЦИПИАЛЬНО (исправлено 11.11.2026).
#
# ЧТО БЫЛО. Скрипт проверял только «есть ли файл». При этом рядом ЛЕЖАЛИ сборки от 05–06.11.2026
# (`Builds\KazistovVv.apk` — junction в проект владельца, и `C:\KazistovVv_Build\KazistovVv.apk` в
# корне). Если Unity падала раньше упаковки (например, на ПОДПИСИ — это и случилось 11.11.2026),
# скрипт находил СТАРЫЙ файл и сообщал успех. Ровно этот класс дефекта проект уже проходил
# 05.11.2026, когда «пропали» изменения ПК-интерфейса.
#
# ЧТО СТАЛО. Все прежние APK переименовываются, а результат признаётся ТОЛЬКО если файл появился
# ПОСЛЕ начала прогона. Проверяются ОБА места, куда Unity реально пишет (в этом проекте сборщик
# кладёт APK в КОРЕНЬ целевого каталога, а не в `Builds`).
# ================================================================================================
# ИМЯ АРТЕФАКТА — ПО ВАРИАНТУ СБОРКИ (P4-19, 11.11.2026).
#
# ЧТО БЫЛО. Оба варианта писали ОДИН файл `KazistovVv.apk`: собрав шлем, легко было поставить
# планшетный APK на шлем (и наоборот) и искать «чёрный экран» вместо «не тот APK».
# ЧТО СТАЛО. Имя говорит, для какого устройства сборка:
#   -Gfx tablet  → KazistovVv-AR-gles3.apk   (планшет/телефон, OpenGLES3)
#   (по умолчанию) → KazistovVv-VR-vulkan.apk (шлем PICO/Quest, Vulkan)
$apkName = if ($Gfx -eq 'tablet') { 'KazistovVv-AR-gles3.apk' } else { 'KazistovVv-VR-vulkan.apk' }
Write-Host "  имя артефакта: $apkName"

# ================================================================================================
# КУДА ПУБЛИКУЕТСЯ РЕЗУЛЬТАТ (добавлено 12.11.2026, находка приёмки R1-1).
#
# ЧТО БЫЛО. Unity пишет APK в КОРЕНЬ целевого ASCII-каталога (`C:\KazistovVv_Build\…`), а
# тестировщик и владелец берут артефакт из `KazistovVv\Builds\`. Скрипт считал успехом НАЛИЧИЕ
# свежего файла в любом из двух мест — и рапортовал «APK СОБРАН ЗАНОВО», хотя в `Builds` попрежнему
# лежал файл от 05.11.2026, то есть сборка БЕЗ правок. Тестировщик так и записал: «Builds\
# KazistovVv-VR-vulkan.apk — 189,4 МБ от 05.11.2026, свежая сборка лежит под именем
# KazistovVv.previous.apk». Это тот же класс дефекта, что чинили в P4-17.
#
# ЧТО СТАЛО. После успешной сборки свежий файл КОПИРУЕТСЯ в `$Source\Builds\<правильное имя>`,
# прежний артефакт с этим именем уезжает в `KazistovVv.previous.apk`, а в журнал печатается
# отдельная строка «APK ОПУБЛИКОВАН» с ВРЕМЕНЕМ и размером. Проверка времени при этом остаётся:
# публикуется только файл, появившийся после начала прогона.
# ================================================================================================
$publishDir = Join-Path $Source 'Builds'

# Оба имени проверяются в обоих местах, куда Unity реально пишет (корень целевого каталога и
# `Builds`), плюс прежнее общее имя — иначе сборка «не найдёт» свой же свежий файл.
$apkCandidatesAfter = @(
    (Join-Path $Target $apkName),
    (Join-Path $Target "Builds\$apkName"),
    (Join-Path $Target 'KazistovVv.apk'),
    (Join-Path $Target 'Builds\KazistovVv.apk')
)
$staleApk = Join-Path $Target 'KazistovVv.previous.apk'
foreach ($c in $apkCandidatesAfter) {
    if (-not (Test-Path -LiteralPath $c)) { continue }
    try {
        Move-Item -LiteralPath $c -Destination $staleApk -Force
        Write-Host "  прежний APK убран: $c → $staleApk (иначе успех нельзя отличить от него)"
    } catch {
        Write-Host "  НЕ УДАЛОСЬ убрать прежний APK $c : $($_.Exception.Message)" -ForegroundColor Yellow
    }
}
$buildStarted = Get-Date

$argList = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', ('"' + $Target + '"'),
    '-executeMethod', 'KazistovVvXrEditor.KvXrTools.BuildAndroidApk',
    '-logFile', ('"' + $log + '"')
)
$p = Start-Process -FilePath $Unity -ArgumentList $argList -PassThru -NoNewWindow
$null = $p.WaitForExit($TimeoutMinutes * 60 * 1000)
if (-not $p.HasExited) {
    Write-Host "ТАЙМАУТ $TimeoutMinutes мин — процесс снимается"
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

Write-Host ""
Write-Host "=== [3/3] РЕЗУЛЬТАТ ==="
if (Test-Path $log) {
    Select-String -Path $log -Pattern '\[KvXr\]|Invalid project path|BuildFailedException' |
        Select-Object -First 20 | ForEach-Object { $_.Line }
}

# ПРОВЕРКА ПРОВАЛА КОМПИЛЯЦИИ: Unity при ошибках пишет "Scripts have compiler errors" и
# выходит, но НЕ создаёт APK. Раньше скрипт всё равно находил старый APK и сообщал успех —
# владелец получал сборку без правок (05.11.2026: так пропали изменения ПК-интерфейса).
if (Test-Path $log) {
    $failed = Select-String -Path $log -Pattern 'Scripts have compiler errors|Aborting batchmode due to failure|BuildFailedException' -Quiet
    if ($failed) {
        Write-Host "СБОРКА ПРОВАЛЕНА: компиляция/сборка Unity завершилась ошибкой" -ForegroundColor Red
        Select-String -Path $log -Pattern 'error CS|Aborting batchmode|BuildFailedException' |
            Select-Object -First 15 | ForEach-Object { $_.Line }
        exit 1
    }
}

# ПРОВЕРКА ПРОВАЛА СБОРКИ — ПО СЛОВУ UNITY, А НЕ ТОЛЬКО ПО КОМПИЛЯЦИИ C#.
# «Scripts have compiler errors» покрывает только C#; падение на ПОДПИСИ, на Gradle или на
# валидации размера даёт `Build Finished, Result: Failure` и `UnityException`. Раньше эти строки
# не проверялись вовсе — и провал маскировался найденным старым APK.
if (Test-Path $log) {
    $failed = Select-String -Path $log -Pattern 'Scripts have compiler errors|Aborting batchmode due to failure|BuildFailedException|Build Finished, Result: Failure|UnityException: Unable to sign' -Quiet
    if ($failed) {
        Write-Host "СБОРКА ПРОВАЛЕНА: Unity сообщила об ошибке" -ForegroundColor Red
        Select-String -Path $log -Pattern 'error CS|Aborting batchmode|BuildFailedException|Build Finished, Result: Failure|UnityException|No keystore passwords' |
            Select-Object -First 15 | ForEach-Object { $_.Line.Substring(0, [Math]::Min(220, $_.Line.Length)) }
        exit 1
    }
}

# РЕЗУЛЬТАТ — ТОЛЬКО СВЕЖИЙ ФАЙЛ. Сравнение по времени, а не по факту существования.
$fresh = $null
foreach ($c in $apkCandidatesAfter) {
    if (-not (Test-Path -LiteralPath $c)) { continue }
    $info = Get-Item -LiteralPath $c
    if ($info.LastWriteTime -lt $buildStarted.AddSeconds(-5)) {
        Write-Host "APK есть, но СТАРШЕ начала сборки — это не результат прогона: $c" -ForegroundColor Yellow
        continue
    }
    $fresh = $info
    break
}

if ($fresh) {
    $mb = [math]::Round($fresh.Length / 1MB, 1)
    Write-Host "APK СОБРАН ЗАНОВО: $($fresh.FullName) ($mb МБ, $($fresh.LastWriteTime))" -ForegroundColor Green

    # ПУБЛИКАЦИЯ В `Builds` — ОТДЕЛЬНЫМ ШАГОМ, С ПРОВЕРКОЙ ВРЕМЕНИ (R1-1).
    if (-not (Test-Path -LiteralPath $publishDir)) {
        New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    }
    $publishPath = Join-Path $publishDir $apkName
    if (Test-Path -LiteralPath $publishPath) {
        $old = Get-Item -LiteralPath $publishPath
        $archive = Join-Path $publishDir 'KazistovVv.previous.apk'
        try {
            Move-Item -LiteralPath $publishPath -Destination $archive -Force
            Write-Host "  прежний артефакт убран: $publishPath → $archive ($($old.LastWriteTime))"
        } catch {
            Write-Host "  НЕ УДАЛОСЬ убрать прежний артефакт $publishPath : $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    try {
        Copy-Item -LiteralPath $fresh.FullName -Destination $publishPath -Force
        $published = Get-Item -LiteralPath $publishPath
        if ($published.LastWriteTime -lt $buildStarted.AddSeconds(-5)) {
            Write-Host "АРТЕФАКТ В Builds СТАРШЕ НАЧАЛА СБОРКИ — публикация не удалась: $publishPath" -ForegroundColor Red
            exit 1
        }
        $pmb = [math]::Round($published.Length / 1MB, 1)
        Write-Host "APK ОПУБЛИКОВАН: $($published.FullName) ($pmb МБ, $($published.LastWriteTime))" -ForegroundColor Green
    } catch {
        Write-Host "ПУБЛИКАЦИЯ НЕ УДАЛАСЬ: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
    exit 0
}

Write-Host "APK НЕ СОЗДАН — сборка не дошла до упаковки (свежего файла нет ни по одному пути)" -ForegroundColor Red
if (Test-Path $staleApk) { Write-Host "  (прежний APK лежит в $staleApk)" }
exit 1
