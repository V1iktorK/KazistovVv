<#
    ПРОВЕРКА ИСПРАВЛЕНИЙ AR НА ПЛАНШЕТЕ (07.11.2026).

    ЧТО ДЕЛАЕТ: снимает экран планшета, тянет журнал Unity и выводит ТОЛЬКО строки-доказательства
    по исправлениям: пропорции экрана, включённое обнаружение плоскостей, материалы робота,
    размеры интерфейса, честный счётчик столов.

    ЗАЧЕМ ОТДЕЛЬНЫМ СКРИПТОМ: проверка должна быть повторяемой одной командой, а не «посмотрел
    глазами один раз».

    ПРИМЕР: .\Tools\ar-planes-verify.ps1 -Wait 30
#>
param(
    [string]$Device = "91863b7e0983",
    [int]$Wait = 20,
    [string]$Prefix = "ar-fix",
    [string]$Adb
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $project 'Logs'

if (-not $Adb) {
    $Adb = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
    if (-not (Test-Path $Adb)) {
        $cmd = Get-Command adb -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($cmd) { $Adb = $cmd.Source }
    }
}
if (-not (Test-Path $Adb)) { Write-Host "НЕ НАЙДЕН adb" -ForegroundColor Red; exit 1 }

Write-Host "=== ПРОВЕРКА AR НА ПЛАНШЕТЕ $Device ==="
Write-Host "  пауза $Wait с (камера должна успеть получить кадры и найти плоскости)"
Start-Sleep -Seconds $Wait

$shot = Join-Path $logs "$Prefix-screen.png"
& $Adb -s $Device shell screencap -p /sdcard/$Prefix-screen.png | Out-Null
& $Adb -s $Device pull /sdcard/$Prefix-screen.png $shot | Out-Null
Write-Host "  скриншот: $shot"

$log = Join-Path $logs "$Prefix-logcat.log"
& $Adb -s $Device logcat -d -s Unity > $log
Write-Host "  журнал:   $log"

$patterns = @(
    "ЭКРАН: пропорции",
    "ЭКРАН: ориентация",
    "ПОВЕРХНОСТИ: ДИАГНОСТИКА",
    "ПОВЕРХНОСТИ: обнаружение восстановлено",
    "ПОВЕРХНОСТИ: подсистема ОСТАНОВЛЕНА",
    "ПОВЕРХНОСТИ: подсистема работает, поверхностей нет",
    "МАТЕРИАЛЫ:",
    "ИНТЕРФЕЙС: ПК-вид построен",
    "ИНТЕРФЕЙС: точка создания вышла за край",
    "ВЫБОР: сброшен",
    "СОСТОЯНИЕ:"
)

Write-Host ""
Write-Host "=== СТРОКИ-ДОКАЗАТЕЛЬСТВА ==="
foreach ($p in $patterns) {
    $hits = Select-String -Path $log -Pattern $p -ErrorAction SilentlyContinue
    if (-not $hits) { Write-Host ("  [нет] " + $p) -ForegroundColor Yellow; continue }
    $last = $hits | Select-Object -Last 1
    $text = $last.Line -replace '^\S+\s+\S+\s+\d+\s+\d+\s+\w\s+Unity\s+:\s*', ''
    Write-Host ("  [есть] " + $text)
}

Write-Host ""
Write-Host "=== СТРОКА СОСТОЯНИЯ (последняя) ==="
$state = Select-String -Path $log -Pattern "СОСТОЯНИЕ:" | Select-Object -Last 1
if ($state) { Write-Host ("  " + ($state.Line -replace '^\S+\s+\S+\s+\d+\s+\d+\s+\w\s+Unity\s+:\s*', '')) }
