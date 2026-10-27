# ПОТОКОВЫЙ ЖУРНАЛ ШЛЕМА (сессия §35). Ловушка F2.2: `adb logcat -d` ТЕРЯЕТ НАЧАЛО —
# кольцевой буфер проворачивается, и строки Unity могут начинаться позже старта процесса.
# Поэтому здесь журнал пишется ПОТОКОМ и запускается ДО старта приложения.
#
# Ничего в проекте не меняет: только читает устройство и пишет файл в Logs\.
param(
    [string]$Tag = 's35',
    [int]$Seconds = 75,
    [string]$Package = 'com.KazistovVv.app',
    [switch]$NoRestart
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $project 'Assets'))) { $project = 'C:\KazistovVv_Build' }

$adb = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
if (-not (Test-Path $adb)) {
    $cand = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Recurse -Filter 'adb.exe' -ErrorAction SilentlyContinue |
            Select-Object -First 1
    if ($cand) { $adb = $cand.FullName }
}
if (-not (Test-Path $adb)) { Write-Host 'ОШИБКА: adb не найден' -ForegroundColor Red; exit 1 }

$serial = (& $adb devices | Select-String -Pattern '^\S+\s+device$' | ForEach-Object { ($_ -split '\s+')[0] } | Select-Object -First 1)
if (-not $serial) { Write-Host 'ОШИБКА: устройство не подключено (adb devices пуст)' -ForegroundColor Red; exit 1 }

$logs = Join-Path $project 'Logs'
if (-not (Test-Path $logs)) { New-Item -ItemType Directory -Path $logs | Out-Null }
$logFile = Join-Path $logs "device-$Tag.log"
$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'

Write-Host "=== ПОТОКОВЫЙ ЖУРНАЛ ШЛЕМА ==="
Write-Host "  устройство: $serial"
Write-Host "  пакет:      $Package"
Write-Host "  файл:       $logFile"
Write-Host "  запуск:     $stamp"

# 1. Очистить буфер, чтобы в файл не попал прошлый запуск.
& $adb -s $serial logcat -c 2>&1 | Out-Null

# 2. ПОДНЯТЬ ПОТОК ДО СТАРТА ПРИЛОЖЕНИЯ (в этом весь смысл: начало не теряется).
$proc = Start-Process -FilePath $adb -ArgumentList @('-s', $serial, 'logcat', '-v', 'time') `
    -RedirectStandardOutput $logFile -PassThru -NoNewWindow
Start-Sleep -Milliseconds 800

# 3. Перезапустить приложение, чтобы журнал начинался с его старта.
if (-not $NoRestart) {
    & $adb -s $serial shell am force-stop $Package 2>&1 | Out-Null
    Start-Sleep -Milliseconds 400
    & $adb -s $serial shell monkey -p $Package -c android.intent.category.LAUNCHER 1 2>&1 | Select-Object -Last 1
}

# 4. Писать журнал заданное время.
Write-Host "  пишу журнал $Seconds с (наденьте шлем и возьмите контроллеры в руки)…"
Start-Sleep -Seconds $Seconds

# 5. Остановить поток.
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
Start-Sleep -Milliseconds 300

$size = (Get-Item $logFile).Length
Write-Host "  записано: $([math]::Round($size/1KB,1)) КБ"

# 6. Короткая сводка по важным строкам — чтобы не искать вручную.
Write-Host ''
Write-Host '=== ЧТО НАШЛОСЬ В ЖУРНАЛЕ ==='
$patterns = [ordered]@{
    'Сон по датчику присутствия (шлем не надет)' = 'gotoSleepFromPsensor|APP_CMD_STOP'
    'Наш профиль PICO предложен'                 = 'pico4s_controller'
    'Текущий профиль руки'                       = 'xrGetCurrentInteractionProfile'
    'Привязка набора действий'                   = 'oxr_session_attach_action_sets'
    'Расширение XR_BD_controller_interaction'    = 'XR_BD_controller_interaction'
    'Контроллеры появились'                      = 'КОНТРОЛЛЕРЫ ПОЯВИЛИСЬ'
    'Контроллеры пропали'                        = 'КОНТРОЛЛЕРЫ ПРОПАЛИ'
    'Состав устройств ввода'                     = 'XR-ввод:'
    'Окружение: ангар'                           = 'окружение: ангар'
    'Панель групп'                               = 'панель групп:'
    'Десктопный оверлей'                         = 'десктопный оверлей в шлеме'
    'Луч идёт по взгляду'                        = 'луч идёт ПО ВЗГЛЯДУ'
    'Контроллер появился (луч из руки)'          = 'вернулся луч из руки'
    'Пол XR-сцены'                               = 'Пол XR-сцены'
    'Стенды поставлены'                          = 'Лаборатория поставлена'
    'Ошибки Unity'                               = 'Error|Exception'
}
foreach ($k in $patterns.Keys) {
    $hits = Select-String -Path $logFile -Pattern $patterns[$k] -ErrorAction SilentlyContinue
    $n = ($hits | Measure-Object).Count
    $mark = if ($n -gt 0) { 'есть' } else { 'НЕТ ' }
    Write-Host ("  [{0}] {1,-46} строк: {2}" -f $mark, $k, $n)
}

Write-Host ''
Write-Host "Полный журнал: $logFile"
