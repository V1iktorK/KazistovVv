<#
    ДОКАЗАТЕЛЬСТВО РАСПОЗНАВАНИЯ РЕАЛЬНОГО СТОЛА НА УСТРОЙСТВЕ (AR — планшет, MR — шлем).

    ЗАЧЕМ. Владелец просил «автоматическое распознавание не виртуального стола для AR/MR».
    Правила распознавания доказаны тестами без устройств, но это доказывает ЛОГИКУ, а не то, что
    конвейер живой на железе. Здесь мы смотрим на устройство и требуем, чтобы приложение САМО
    сказало вердикт с числами. Требование «стол найден» предъявить нельзя: физического стола перед
    камерой может не быть, и это не дефект кода. Поэтому принимается ЧЕСТНЫЙ вердикт одного из трёх
    видов: «стол найден · площадь … · высота над полом …», «пол (не стол) · площадь …» либо
    «поверхностей нет / разметки комнаты нет — что делать». Молчание — не принимается.

    ПОЧЕМУ СНАЧАЛА «РАЗОГРЕВ», А ПОТОМ ЧИСТЫЙ СТАРТ (только для шлема). Режим MR включается при
    СТАРТЕ приложения: переключение на лету настраивает режим, но конвейер реальных поверхностей
    поднимается только при запуске в MR (проверено: при старте в MR в журнале есть
    «[KvMr] === СМЕШАННАЯ РЕАЛЬНОСТЬ: включаю сквозной вид и реальные поверхности ===», при
    переключении на лету — нет). Поэтому: (1) прогон с `-e kv_mode mr` СОХРАНЯЕТ режим, (2) затем
    чистое перезапускное приложение стартует уже в MR, и мы читаем его журнал.

    ВОЗВРАТ: 0 — вердикт с числами получен; 1 — нет (причина в выводе).

    ПРИМЕРЫ:
      pwsh -File Tools\real-table-device.ps1 -DeviceId 91863b7e0983
      pwsh -File Tools\real-table-device.ps1 -DeviceId PA921DMGK4120784G -Mode mr
#>
param(
    [Parameter(Mandatory = $true)][string]$DeviceId,
    # auto — планшет (AR, стартовая сцена ArMain), mr — шлем в режиме смешанной реальности.
    [ValidateSet('auto', 'mr')][string]$Mode = 'auto',
    [string]$Package = 'com.KazistovVv.app',
    [string]$Activity = 'com.unity3d.player.UnityPlayerGameActivity',
    [int]$WarmupSeconds = 120,
    [int]$ObserveSeconds = 75,
    [string]$Adb = '',
    [string]$LogOut = ''
)

$ErrorActionPreference = 'Continue'

# КОДИРОВКА КОНСОЛИ — UTF8 (добавлено тестировщиком 13.11.2026).
# Иначе кириллица из adb (логи, отчёты) приходит мусором: совпадения вроде «ИТОГ»/«исчез»
# молча не срабатывают, и судья выносит ЛОЖНЫЙ вердикт (замерено: match False → True).
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

if (-not $Adb) {
    $Adb = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
    if (-not (Test-Path -LiteralPath $Adb)) {
        $cmd = Get-Command adb -ErrorAction SilentlyContinue
        if ($cmd) { $Adb = $cmd.Source }
    }
}
if (-not (Test-Path -LiteralPath $Adb)) { Write-Host "adb не найден" -ForegroundColor Red; exit 1 }

if (-not $LogOut) {
    $root = Split-Path $PSScriptRoot -Parent
    $LogOut = Join-Path $root ('Logs\real-table-' + $DeviceId + '-' + $Mode + '.logcat.txt')
}

function Adb-Run([string[]]$ArgList) { return ((& $Adb -s $DeviceId @ArgList 2>&1) -join "`n") }

Write-Host ("=== распознавание реального стола · устройство " + $DeviceId + " · режим " + $Mode + " ===")

$devices = (& $Adb devices 2>&1) -join "`n"
if ($devices -notmatch [regex]::Escape($DeviceId)) { Write-Host "устройство не подключено" -ForegroundColor Red; exit 1 }

$null = Adb-Run @('shell', 'input', 'keyevent', 'KEYCODE_WAKEUP')
$null = Adb-Run @('shell', 'am', 'kill-all')
$null = Adb-Run @('shell', 'pm', 'trim-caches', '1000G')
Start-Sleep -Seconds 2

# РАЗОГРЕВ ДЛЯ ШЛЕМА: сохранить режим MR. Без этого чистый старт поднимется в VR, и конвейер
# реальных поверхностей не включится вовсе (проверено на PICO 4 Ultra 12.11.2026).
if ($Mode -eq 'mr') {
    Write-Host "  разогрев: выставляю и СОХРАНЯЮ режим MR (прогон интерфейса с -e kv_mode mr)"
    $null = Adb-Run @('shell', 'am', 'force-stop', $Package)
    Start-Sleep -Seconds 2
    $null = Adb-Run @('logcat', '-c')
    $null = Adb-Run @('shell', 'am', 'start', '-n', ($Package + '/' + $Activity), '-e', 'kv_uisweep', '1', '-e', 'kv_mode', 'mr')
    $sweepDeadline = (Get-Date).AddSeconds($WarmupSeconds)
    while ((Get-Date) -lt $sweepDeadline) {
        Start-Sleep -Seconds 5
        $l = Adb-Run @('logcat', '-d')
        if ($l -match '\/KvUiSweep\/|\[KvUiSweep\]\s*ИТОГ') { break }
    }
    $null = Adb-Run @('shell', 'am', 'force-stop', $Package)
    Start-Sleep -Seconds 3
}

# ЧИСТЫЙ СТАРТ И НАБЛЮДЕНИЕ.
$null = Adb-Run @('shell', 'am', 'force-stop', $Package)
Start-Sleep -Seconds 2
$null = Adb-Run @('logcat', '-c')
$start = Adb-Run @('shell', 'am', 'start', '-n', ($Package + '/' + $Activity))
Write-Host ("  запуск: " + (@($start -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -First 1) -join ''))
Write-Host ("  наблюдаю " + $ObserveSeconds + " с...")
Start-Sleep -Seconds $ObserveSeconds

$log = Adb-Run @('logcat', '-d')
try {
    $dir = Split-Path $LogOut -Parent
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($LogOut, $log, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("  журнал сохранён: " + $LogOut)
} catch { Write-Host ("  журнал сохранить не удалось: " + $_.Exception.Message) }

$problems = @()
$report = @()
$lines = $log -split "`n"

if ($Mode -eq 'auto') {
    # ПЛАНШЕТ (AR): ждём вердикт классификатора по плоскостям либо честную диагностику.
    $verdict = @($lines | Where-Object { $_ -match '\[KvAr\] ПОВЕРХНОСТИ:' -and $_ -match '(стол найден|пол \(не стол\)|стол не найден|плоскостей 0|плоскостей нет|поверхностей нет|трекинг НЕ идёт|ДИАГНОСТИКА)' } | Select-Object -Last 1)
    $diag = @($lines | Where-Object { $_ -match '\[KvAr\] ПОВЕРХНОСТИ:' -and $_ -match '(плоскостей нет|поверхностей нет|ДИАГНОСТИКА|плоскостей 0|трекинг НЕ идёт)' } | Select-Object -Last 1)
    if ($verdict.Count -gt 0 -and $verdict[0] -match '\d') {
        $report += ('AR: ' + $verdict[0].Trim().Substring(0, [Math]::Min(160, $verdict[0].Trim().Length)))
    } elseif ($diag.Count -gt 0 -and $diag[0] -match '\d') {
        $report += ('AR: подсистема работает, поверхностей нет — физического стола в кадре не было (' +
            $diag[0].Trim().Substring(0, [Math]::Min(120, $diag[0].Trim().Length)) + ')')
    } else {
        $problems += 'планшет: ни вердикта классификатора с числами, ни диагностики «поверхностей нет»'
    }
} else {
    # ШЛЕМ (MR): ждём итоговую строку запроса разметки комнаты либо объяснение про разметку.
    $mrVerdict = @($lines | Where-Object { $_ -match '\[KvMr\] РЕАЛЬНЫЙ СТОЛ: запрос выполнен' } | Select-Object -Last 1)
    $mrHint = @($lines | Where-Object { $_ -match 'разметки помещения нет|включите разметку комнаты' } | Select-Object -Last 1)
    $mrStart = @($lines | Where-Object { $_ -match 'СМЕШАННАЯ РЕАЛЬНОСТЬ: включаю сквозной вид' } | Select-Object -Last 1)
    if ($mrStart.Count -eq 0) { $problems += 'шлем: приложение стартовало НЕ в MR — конвейер реальных поверхностей не включался' }
    if ($mrVerdict.Count -gt 0 -and $mrVerdict[0] -match '\d') {
        $report += ('MR: ' + $mrVerdict[0].Trim().Substring(0, [Math]::Min(160, $mrVerdict[0].Trim().Length)))
    } elseif ($mrHint.Count -gt 0) {
        $report += ('MR: разметки комнаты нет — приложение сказало об этом прямо (' +
            $mrHint[0].Trim().Substring(0, [Math]::Min(110, $mrHint[0].Trim().Length)) + ')')
    } else {
        $problems += 'шлем: ни вердикта «РЕАЛЬНЫЙ СТОЛ: запрос выполнен», ни объяснения про разметку комнаты'
    }
}

if ($problems.Count -eq 0) {
    Write-Host ("ВЕРДИКТ ПОЛУЧЕН: " + ($report -join ' · ')) -ForegroundColor Green
    exit 0
}
Write-Host "ВЕРДИКТА НЕТ:" -ForegroundColor Red
$problems | ForEach-Object { Write-Host ("  - " + $_) }
if ($report.Count -gt 0) { $report | ForEach-Object { Write-Host ("  было: " + $_) } }
exit 1
