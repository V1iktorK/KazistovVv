<#
    УСТРОЙСТВО PICO 4 ULTRA: ПРОВЕРКА, УСТАНОВКА APK, ЛОГ, СНИМОК ЭКРАНА (сессия §31).

    ЗАЧЕМ. Приёмка «в шлеме видно то, что нужно» до этой сессии была словами: «выглядит нормально».
    Теперь у нас есть adb, и каждый пункт чек-листа можно подтвердить ФАЙЛОМ:
      • лог устройства — что приложение запустилось и что оно написало;
      • снимок экрана — что на экране нет розового/чёрного/двоения (снимок смотрит человек и агент);
      • факты об устройстве — что версия Android, SDK и архитектура совпадают с настройками проекта.

    РЕЖИМЫ:
      -Mode Check    только факты об устройстве и сравнение с требованиями проекта (ничего не ставит)
      -Mode Install  установить APK, запустить, снять лог и снимки  (по умолчанию)
      -Mode Log      ничего не ставить: снять лог и снимки с уже установленного приложения

    ПАРАМЕТРЫ:
      -Apk      путь к APK; имя пакета читается из самого APK (aapt), а не угадывается
      -Package  имя пакета, если APK не указан (по умолчанию com.KazistovVv.app)
      -Seconds  сколько секунд писать лог (по умолчанию 45)
      -Shots     через сколько секунд после запуска делать снимки (по умолчанию 8,20,35)
      -Analyze  сразу разобрать снимки числами (Tools\device-screenshot-check.ps1)

    Код возврата: 0 — устройство найдено и действия выполнены; 1 — устройства нет или ошибка;
                  2 — приложение усыплено датчиком присутствия (шлем не надет).
#>
param(
    [ValidateSet('Check', 'Install', 'Log')]
    [string]$Mode = 'Install',
    [string]$Apk,
    [string]$Package = 'com.KazistovVv.app',
    [int]$Seconds = 45,
    [int[]]$Shots = @(8, 20, 35),
    [int]$WaitDeviceSeconds = 120,
    [switch]$Analyze
)

$ErrorActionPreference = 'Continue'
$project = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'

# ---------------------------------------------------------------- поиск инструментов

function Find-Adb {
    $inPath = Get-Command adb -ErrorAction SilentlyContinue
    if ($inPath) { return $inPath.Source }

    $hub = "C:\Program Files\Unity\Hub\Editor"
    if (Test-Path $hub) {
        foreach ($dir in (Get-ChildItem $hub -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
            $c = Join-Path $dir.FullName 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
            if (Test-Path $c) { return $c }
        }
    }
    return $null
}

function Find-Aapt {
    $hub = "C:\Program Files\Unity\Hub\Editor"
    if (-not (Test-Path $hub)) { return $null }
    foreach ($dir in (Get-ChildItem $hub -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
        $bt = Join-Path $dir.FullName 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools'
        if (-not (Test-Path $bt)) { continue }
        foreach ($v in (Get-ChildItem $bt -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
            $aapt = Join-Path $v.FullName 'aapt2.exe'
            if (Test-Path $aapt) { return $aapt }
            $aapt = Join-Path $v.FullName 'aapt.exe'
            if (Test-Path $aapt) { return $aapt }
        }
    }
    return $null
}

$adb = Find-Adb
if (-not $adb) { Write-Host 'ОШИБКА: adb не найден.'; exit 1 }
Write-Host "adb: $adb"

# ---------------------------------------------------------------- ожидание устройства

function Wait-Device([int]$seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        $out = & $adb devices 2>&1
        $line = $out | Where-Object { $_ -match "^\S+\s+device$" } | Select-Object -First 1
        if ($line) { return ($line -split '\s+')[0] }
        Start-Sleep -Seconds 3
    }
    return $null
}

$serial = Wait-Device $WaitDeviceSeconds
if (-not $serial) {
    Write-Host ''
    Write-Host 'УСТРОЙСТВО НЕ НАЙДЕНО. Проверить НА ШЛЕМЕ (это настройка шлема, не компьютера):'
    Write-Host '  1) Настройки → Общие → О системе → нажать «Версия ПО» 7 раз (режим разработчика).'
    Write-Host '  2) Настройки → Общие → Разработчик → включить «Отладка по USB».'
    Write-Host '  3) Подключить кабель USB-C; в шлеме подтвердить «Разрешить отладку по USB?» (галочка «всегда»).'
    Write-Host '  4) Повторить запуск этого сценария.'
    Write-Host ''
    Write-Host 'Текущее состояние:'; & $adb devices -l 2>&1
    exit 1
}

Write-Host "Устройство: $serial"

function Prop([string]$name) {
    return ((& $adb -s $serial shell getprop $name 2>&1) -join '').Trim()
}

# ---------------------------------------------------------------- факты об устройстве

$model  = Prop 'ro.product.model'
$brand  = Prop 'ro.product.brand'
$rel    = Prop 'ro.build.version.release'
$sdk    = Prop 'ro.build.version.sdk'
$abi    = Prop 'ro.product.cpu.abi'
$batt   = ((& $adb -s $serial shell dumpsys battery 2>&1 | Select-String 'level:' | Select-Object -First 1).Line -replace '\D','')

Write-Host ''
Write-Host '=== ФАКТЫ ОБ УСТРОЙСТВЕ ==='
Write-Host "  модель:        $brand $model"
Write-Host "  Android:       $rel (SDK $sdk)"
Write-Host "  архитектура:   $abi"
Write-Host "  батарея:       $batt %"

# сверка с настройками проекта (читаем из ProjectSettings, а не из головы)
$projSettings = Join-Path $project 'ProjectSettings\ProjectSettings.asset'
if (Test-Path $projSettings) {
    $text = Get-Content $projSettings -Raw
    $minSdk = ([regex]::Match($text, 'AndroidMinSdkVersion:\s*(\d+)')).Groups[1].Value
    $tgtSdk = ([regex]::Match($text, 'AndroidTargetSdkVersion:\s*(\d+)')).Groups[1].Value
    $arch   = ([regex]::Match($text, 'AndroidTargetArchitectures:\s*(\d+)')).Groups[1].Value
    Write-Host "  проект требует: min SDK $minSdk · target SDK $tgtSdk · архитектуры (код) $arch"
    if ($tgtSdk -and $sdk -ne $tgtSdk) {
        Write-Host "  ВНИМАНИЕ: target SDK проекта ($tgtSdk) не совпадает с Android на устройстве ($sdk)" -ForegroundColor Yellow
    } else {
        Write-Host '  совпадение версий: OK'
    }
}

# ---------------------------------------------------------------- имя пакета

$installPackage = $null
if ($Apk -and (Test-Path $Apk)) {
    $aapt = Find-Aapt
    if ($aapt) {
        $badging = & $aapt dump badging $Apk 2>&1
        $m = ($badging | Select-String "package: name='([^']+)'" | Select-Object -First 1)
        if ($m) { $installPackage = $m.Matches[0].Groups[1].Value }
    }
}
if (-not $installPackage) { $installPackage = $Package }
Write-Host "  имя пакета:    $installPackage"

if ($Mode -eq 'Check') { exit 0 }

# ---------------------------------------------------------------- установка

if ($Mode -eq 'Install') {
    if (-not $Apk) {
        foreach ($c in @((Join-Path $project 'Builds\KazistovVv.apk'), (Join-Path $project 'KazistovVv.apk'))) {
            if (Test-Path $c) { $Apk = $c; break }
        }
    }
    if (-not $Apk -or -not (Test-Path $Apk)) {
        Write-Host 'ОШИБКА: APK не найден. Укажите путь: -Apk <путь>' -ForegroundColor Red
        exit 1
    }
    Write-Host ''
    Write-Host "=== УСТАНОВКА: $Apk ==="
    $install = & $adb -s $serial install -r $Apk 2>&1
    $install | ForEach-Object { "   $_" }
    # ВНИМАНИЕ: `$install` — МАССИВ строк, и `-notmatch` на массиве проверяет каждый элемент
    # (первый же «Performing Streamed Install» делал проверку ложной). Поэтому склеиваем в одну строку.
    if (-not (($install -join "`n") -match 'Success')) { Write-Host 'ОШИБКА установки.' -ForegroundColor Red; exit 1 }
}

# ---------------------------------------------------------------- запуск, лог, снимки

& $adb -s $serial logcat -c 2>&1 | Out-Null
Write-Host ''
Write-Host "=== ЗАПУСК $installPackage ==="
& $adb -s $serial shell monkey -p $installPackage -c android.intent.category.LAUNCHER 1 2>&1 | Select-Object -Last 2

$logFile = Join-Path $logs "device-$stamp.log"
$logJob = Start-Process -FilePath $adb -ArgumentList @('-s', $serial, 'logcat', '-v', 'time') -RedirectStandardOutput $logFile -PassThru -NoNewWindow

# РАННЯЯ ПРОВЕРКА «ШЛЕМ СПИТ». PICO усыпляет экран по датчику присутствия, и приложение получает
# APP_CMD_STOP через ~1,5 с после запуска — это НЕ дефект проекта, но без этой проверки выглядит
# как «приложение не работает». Проверяем через 6 с и говорим прямо, что делать.
Start-Sleep -Seconds 6
$early = & $adb -s $serial logcat -d -v brief 2>&1
$asleep = ($early | Select-String -Pattern 'ProximityPowerPolicyController.*gotoSleepFromPsensor' -Quiet) -and
          ($early | Select-String -Pattern 'APP_CMD_STOP' -Quiet)
if ($asleep) {
    Write-Host ''
    Write-Host 'ВНИМАНИЕ: ШЛЕМ НЕ НАДЕТ — система усыпила экран, приложение поставлено на паузу.' -ForegroundColor Yellow
    Write-Host '  В журнале: ProximityPowerPolicyController: psensor gotoSleepFromPsensor + APP_CMD_STOP.' -ForegroundColor Yellow
    Write-Host '  ЧТО ДЕЛАТЬ: надеть шлем ИЛИ закрыть датчик присутствия (положить линзами вниз на мягкое,' -ForegroundColor Yellow
    Write-Host '  либо накрыть внутреннюю часть тканью) и запустить сценарий снова.' -ForegroundColor Yellow
    Write-Host '  Обойти программно нельзя: setprop отклоняется (production-сборка), adb root недоступен.' -ForegroundColor Yellow
    if (-not $logJob.HasExited) { Stop-Process -Id $logJob.Id -Force -ErrorAction SilentlyContinue }
    exit 2
}

$shotFiles = @()
foreach ($t in ($Shots | Sort-Object)) {
    $already = [int]((Get-Date) - $logJob.StartTime).TotalSeconds
    $wait = $t - $already
    if ($wait -gt 0) { Start-Sleep -Seconds $wait }

    $png = Join-Path $logs "device-$stamp-t$t.png"
    # screencap отдаёт ДВОИЧНЫЙ PNG. Через PowerShell-конвейер он портится (строки вместо байт),
    # поэтому пишем через редирект cmd — это проверено: файл начинается с 89 50 4E 47 (сигнатура PNG).
    cmd /c "`"$adb`" -s $serial exec-out screencap -p > `"$png`"" 2>$null
    if (Test-Path $png) {
        $len = (Get-Item $png).Length
        if ($len -gt 1000) { $shotFiles += $png; Write-Host ("   снимок через {0} с: {1} ({2:N0} КБ)" -f $t, (Split-Path $png -Leaf), ($len / 1KB)) }
        else { Remove-Item $png -Force -ErrorAction SilentlyContinue; Write-Host ("   снимок через {0} с: ПУСТО (screencap не отдал кадр)" -f $t) }
    }
}

$elapsed = [int]((Get-Date) - $logJob.StartTime).TotalSeconds
if ($elapsed -lt $Seconds) { Start-Sleep -Seconds ($Seconds - $elapsed) }
if (-not $logJob.HasExited) { Stop-Process -Id $logJob.Id -Force -ErrorAction SilentlyContinue }

# ---------------------------------------------------------------- разбор

Write-Host ''
Write-Host '=== СТРОКИ ПРИЛОЖЕНИЯ ==='
if (Test-Path $logFile) {
    $app = Select-String -Path $logFile -Pattern 'Unity|KvXr|KvRender|KvDevice|openxr|OpenXR' | Select-Object -Last 30
    if ($app) { $app | ForEach-Object { '   ' + $_.Line } } else { Write-Host '   (строк приложения нет — приложение могло не запуститься)' }
}

Write-Host ''
Write-Host '=== ОШИБКИ И ИСКЛЮЧЕНИЯ ==='
$errs = Select-String -Path $logFile -Pattern 'FATAL|AndroidRuntime|Exception|Error|error' -ErrorAction SilentlyContinue |
        Where-Object { $_.Line -notmatch 'DebugUtils|permission|no error' } | Select-Object -Last 20
if ($errs) { $errs | ForEach-Object { '   ' + $_.Line } } else { Write-Host '   ошибок не найдено' }

Write-Host ''
Write-Host '=== ОБЯЗАТЕЛЬНЫЕ СТРОКИ ПРИЛОЖЕНИЯ (проверка по коду XR-слоя) ==='
# Эти строки пишут сами компоненты XR-сцены. Их наличие — доказательство, что подсистема поднялась,
# а не «должно было подняться». Отсутствие строки — повод смотреть причину, а не радоваться.
$expected = @(
    @{ Name = 'начинка XR-сцены (столы и роботы)'; Pattern = 'Начинка XR-сцены' },
    @{ Name = 'робот найден для показа';           Pattern = 'Робот для показа найден' },
    @{ Name = 'сторож камеры шлема';               Pattern = 'Сторож камеры' },
    @{ Name = 'риг и целевые кадры';               Pattern = 'Статус рига|риг:' },
    @{ Name = 'источники ввода (голова, руки)';    Pattern = 'источник|контроллер' },
    @{ Name = 'MR-комната (масштаб и стол)';       Pattern = 'MR|стол' }
)
$logText = if (Test-Path $logFile) { Get-Content $logFile -Raw } else { '' }
foreach ($e in $expected) {
    $found = ($logText -match $e.Pattern)
    $mark = if ($found) { 'есть  ' } else { 'НЕТ   ' }
    Write-Host ("   {0}{1}" -f $mark, $e.Name) -ForegroundColor $(if ($found) { 'Gray' } else { 'Yellow' })
}

Write-Host ''
Write-Host '=== ИТОГ ==='
Write-Host "  лог:      $logFile"
foreach ($s in $shotFiles) { Write-Host "  снимок:   $s" }
if ($shotFiles.Count -eq 0) { Write-Host '  ВНИМАНИЕ: снимков нет — принимать картинку придётся глазами в шлеме.' -ForegroundColor Yellow }

if ($Analyze -and $shotFiles.Count -gt 0) {
    Write-Host ''
    Write-Host '=== РАЗБОР СНИМКОВ ЧИСЛАМИ ==='
    $checker = Join-Path $PSScriptRoot 'device-screenshot-check.ps1'
    if (Test-Path $checker) {
        foreach ($s in $shotFiles) { & pwsh -NoProfile -ExecutionPolicy Bypass -File $checker -Image $s }
    } else {
        Write-Host "   не найден $checker"
    }
}
exit 0
