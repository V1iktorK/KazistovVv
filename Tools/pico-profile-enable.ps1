<#
    ВКЛЮЧИТЬ ПРОФИЛЬ КОНТРОЛЛЕРОВ PICO ДЛЯ ANDROID (сессия §34).

    ЗАЧЕМ ОТДЕЛЬНЫЙ ШАГ. Состояние фич OpenXR хранится ПО ПЛАТФОРМЕ: фича, включённая для
    Standalone, в APK для шлема не попадает вовсе. На этом уже один раз потерялись контроллеры
    (§31: профиль был, а для Android выключен). Поэтому включение профиля — отдельное,
    ПРОВЕРЯЕМОЕ действие: скрипт печатает состояние ДО и ПОСЛЕ и падает с кодом 1, если
    фича не включилась.

    ЧТО ИМЕННО ВКЛЮЧАЕТСЯ. Метод `KazistovVvXrEditor.KvXrDeviceTools.ApplyControllerProfile`
    включает профили контроллеров, понятные рантайму PICO:
      • `KHRSimpleControllerProfile` (пакет Unity) — как в §31;
      • `KvPicoControllerProfile` (наш, §34) — профиль `/interaction_profiles/bytedance/pico4s_controller`,
        который рантайм шлема называет текущим для контроллеров PICO 4 Ultra.

    КАК ЧИТАЕТСЯ ОТЧЁТ. Метод печатает ОДНО сообщение из многих строк (первая — с меткой
    `[KvDevice]`, остальные — с отступом), поэтому ищется не «строка с меткой», а БЛОК строк
    после заголовка. Первая версия скрипта искала метку в каждой строке и потому объявляла
    «не выполнено», хотя метод отработал — урок записан сюда, чтобы не повторять.

    Возврат: 0 — фича включена и подтверждена; 1 — не подтвердилась (смотри лог).

    ПРИМЕР
      powershell -ExecutionPolicy Bypass -File Tools\pico-profile-enable.ps1
#>
param(
    [string]$Project = "C:\KazistovVv_Build",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe",
    [string]$LogName = "s34-pico-profile-enable.log",
    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Continue'
$logs = Join-Path $Project 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$logPath = Join-Path $logs $LogName

function Get-KvDeviceBlock {
    param([string]$Path, [string]$Header)
    if (-not (Test-Path $Path)) { return @() }
    $all = @(Get-Content $Path)
    $startIndex = -1
    # СРАВНЕНИЕ ИМЕННО ЧЕРЕЗ .Contains, А НЕ -like: в PowerShell `-like '*[KvDevice]*'` —
    # это ШАБЛОН, где квадратные скобки задают НАБОР символов, поэтому метка `[KvDevice]`
    # таким шаблоном не находится вовсе. Первая версия скрипта из-за этого объявила
    # «метод не выполнился», хотя он отработал и включил профиль (урок 15.10.2026).
    for ($i = 0; $i -lt $all.Count; $i++) {
        if ($all[$i].Contains($Header)) { $startIndex = $i; break }
    }
    if ($startIndex -lt 0) { return @() }
    $block = @($all[$startIndex].Trim())
    for ($i = $startIndex + 1; $i -lt $all.Count; $i++) {
        if (-not $all[$i].StartsWith('  ')) { break }
        $block += $all[$i].Trim()
    }
    return $block
}

if (Get-Process Unity -ErrorAction SilentlyContinue) {
    Write-Host "ДРУГОЙ ПРОЦЕСС Unity УЖЕ РАБОТАЕТ — второй инстанс не допускается. Останавливаюсь."
    exit 1
}

if (-not $env:GRADLE_USER_HOME -or $env:GRADLE_USER_HOME -match '[^\x20-\x7E]') {
    $env:GRADLE_USER_HOME = 'C:\gradle_home'
}

Write-Host "=== ВКЛЮЧЕНИЕ ПРОФИЛЯ КОНТРОЛЛЕРОВ PICO (Android) ==="
Write-Host ("лог: " + $logPath)

$argList = @(
    '-batchmode', '-quit',
    '-projectPath', ('"' + $Project + '"'),
    '-executeMethod', 'KazistovVvXrEditor.KvXrDeviceTools.ApplyControllerProfile',
    '-logFile', ('"' + $logPath + '"')
)

$p = Start-Process -FilePath $Unity -ArgumentList $argList -PassThru -NoNewWindow
$null = $p.WaitForExit($TimeoutMinutes * 60 * 1000)
if (-not $p.HasExited) {
    Write-Host ("ТАЙМАУТ " + $TimeoutMinutes + " мин — процесс снимается")
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

Write-Host ""
Write-Host "=== ОТЧЁТ МЕТОДА ==="
$block = Get-KvDeviceBlock -Path $logPath -Header '[KvDevice] === ПРОФИЛЬ'
$ok = $false
if ($block.Count -eq 0) {
    Write-Host "  блока [KvDevice] в логе нет — метод не выполнился (смотри лог)"
} else {
    foreach ($l in $block) { Write-Host ("  " + $l) }
    $ok = ($block | Where-Object { $_ -match 'ИТОГ:\s*KvPicoControllerProfile.*подтверждено' }).Count -gt 0
}

$compileErrors = @(Select-String -Path $logPath -Pattern 'error CS' -ErrorAction SilentlyContinue |
    ForEach-Object { $_.Line.Trim() } | Select-Object -Unique)
if ($compileErrors.Count -gt 0) {
    Write-Host ""
    Write-Host "ОШИБКИ КОМПИЛЯЦИИ:"
    foreach ($e in $compileErrors) { Write-Host ("  " + $e) }
}

Write-Host ""
Write-Host "=== ПРОВЕРКА, ЧТО ФИЧА ПОПАДЁТ В APK ==="
$reportLog = Join-Path $logs 's34-pico-device-readiness.log'
if (Test-Path $reportLog) { Remove-Item $reportLog -Force }
$argList2 = @(
    '-batchmode', '-quit',
    '-projectPath', ('"' + $Project + '"'),
    '-executeMethod', 'KazistovVvXrEditor.KvXrDeviceTools.ReportDeviceReadiness',
    '-logFile', ('"' + $reportLog + '"')
)
$p2 = Start-Process -FilePath $Unity -ArgumentList $argList2 -PassThru -NoNewWindow
$null = $p2.WaitForExit($TimeoutMinutes * 60 * 1000)
if (-not $p2.HasExited) {
    Stop-Process -Id $p2.Id -Force -ErrorAction SilentlyContinue
    Write-Host "отчёт о готовности: таймаут"
    exit 1
}

$report = Get-KvDeviceBlock -Path $reportLog -Header '[KvDevice] === ЧТО ПОПАДЁТ В APK'
$picoOk = $false
if ($report.Count -eq 0) {
    Write-Host ("  блока [KvDevice] нет в логе: " + $reportLog)
} else {
    foreach ($l in $report) { Write-Host ("  " + $l) }
    $picoOk = ($report | Where-Object { $_ -match 'KvPicoControllerProfile' }).Count -gt 0
}

Write-Host ""
Write-Host "=== ИТОГ ==="
Write-Host ("  включение подтверждено:            " + $(if ($ok) { 'да' } else { 'НЕТ' }))
Write-Host ("  фича видна в списке для Android:   " + $(if ($picoOk) { 'да' } else { 'НЕТ' }))

if ($ok -and $picoOk) { exit 0 } else { exit 1 }
