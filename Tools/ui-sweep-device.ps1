<#
    САМОПРОГОН ИНТЕРФЕЙСА НА УСТРОЙСТВЕ (VR / MR / AR).

    ЗАЧЕМ. Требование владельца: «каждая кнопочка обязана выполнять реальную работу». Проверить это
    глазами можно только в шлеме и только вручную, а такой проверке нельзя верить на слово. Поэтому
    в приложение встроен самопрогон (`KvUiSweepRuntime`): он сам обходит элементы интерфейса,
    нажимает их настоящим путём uGUI и печатает в журнал строку на каждый элемент. Этот скрипт
    запускает самопрогон на устройстве и СУДИТ по журналу, а не по обещаниям.

    ЧТО ЗНАЧИТ «ПРОЙДЕНО»:
      • в журнале есть итоговая строка `[KvUiSweep] ИТОГ:` (прогон дошёл до конца, а не упал);
      • элементов не меньше порога (иначе «прогон» зеленел бы на пустой сцене);
      • «без эффекта» (F) равно нулю вне списка разрешённых исключений;
      • в журнале нет `FATAL EXCEPTION`.

    ЖУРНАЛ УСТРОЙСТВА СОХРАНЯЕТСЯ В ФАЙЛ. По нему же проверяется распознавание РЕАЛЬНОГО стола
    («[KvAr] ПОВЕРХНОСТИ: …» на планшете и «[KvMr] РЕАЛЬНЫЙ СТОЛ: …» в шлеме) — иначе одну и ту же
    работу с устройством пришлось бы гонять дважды.

    ВОЗВРАТ: 0 — прогон пройден; 1 — не пройден (причина в выводе).

    ПРИМЕРЫ:
      pwsh -File Tools\ui-sweep-device.ps1 -DeviceId 91863b7e0983
      pwsh -File Tools\ui-sweep-device.ps1 -DeviceId PA921DMGK4120784G -Mode mr
#>
param(
    [Parameter(Mandatory = $true)][string]$DeviceId,
    # Режим, который надо ВЫСТАВИТЬ перед прогоном: auto — не трогать (для планшета AR),
    # vr / mr — выставить соответствующий режим (для шлема).
    [ValidateSet('auto', 'vr', 'mr')][string]$Mode = 'auto',
    [string]$Package = 'com.KazistovVv.app',
    [string]$Activity = 'com.unity3d.player.UnityPlayerGameActivity',
    [int]$TimeoutSeconds = 300,
    # ПОРОГ ЭЛЕМЕНТОВ. Пустой прогон не считается успехом: если интерфейс не построился,
    # «нет провалов» означало бы «нет проверок».
    [int]$MinElements = 20,
    [string]$Adb = '',
    # Куда сохранить полный журнал устройства (по нему же проверяется реальный стол).
    [string]$LogOut = '',
    # ЧИСТОЕ СОСТОЯНИЕ: сбросить настройки приложения перед прогоном (раскладка панелей пишется в
    # PlayerPrefs и меняет стартовую картину — из-за этого один и тот же код давал то 0, то 41
    # «исчезнувших» элементов). После сброса разрешения выдаются заново.
    [switch]$CleanState
)

$ErrorActionPreference = 'Continue'

if (-not $Adb) {
    $Adb = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
    if (-not (Test-Path -LiteralPath $Adb)) {
        $cmd = Get-Command adb -ErrorAction SilentlyContinue
        if ($cmd) { $Adb = $cmd.Source }
    }
}
if (-not (Test-Path -LiteralPath $Adb)) { Write-Host "adb не найден — проверять нечем" -ForegroundColor Red; exit 1 }

if (-not $LogOut) {
    $projectRoot = Split-Path $PSScriptRoot -Parent
    $LogOut = Join-Path $projectRoot ('Logs\device-' + $DeviceId + '-' + $Mode + '.logcat.txt')
}

function Adb-Run([string[]]$ArgList) {
    return ((& $Adb -s $DeviceId @ArgList 2>&1) -join "`n")
}

Write-Host ("=== самопрогон UI · устройство " + $DeviceId + " · режим " + $Mode + " ===")

$devices = (& $Adb devices 2>&1) -join "`n"
if ($devices -notmatch [regex]::Escape($DeviceId)) {
    Write-Host "устройство $DeviceId не подключено" -ForegroundColor Red
    exit 1
}

# ЭКРАН ДОЛЖЕН БЫТЬ ЖИВ: на уснувшем устройстве активность не запускается, и прогон молча «не идёт».
$null = Adb-Run @('shell', 'input', 'keyevent', 'KEYCODE_WAKEUP')

# ОСВОБОДИТЬ ПАМЯТЬ ПЕРЕД ПРОГОНОМ (добавлено тестировщиком 12.11.2026).
#
# ЧТО БЫЛО. Два прогона подряд оборвались одинаково: система УБИВАЛА приложение посреди обхода
# (в журнале десятки строк «Process … has died», за ними — рабочий стол). Прогон выглядел как
# «не дошёл до конца», хотя причина не в коде: не хватило памяти устройства.
# ЧТО СТАЛО. Перед запуском освобождаем память: закрываем фоновые приложения и чистим кэши. Это
# подготовка испытательного стенда, а не подмена результата: самопрогон по-прежнему обязан пройти
# все элементы, а если система всё равно убьёт приложение — это будет названо прямо, без догадок.
# ЧИСТОЕ СОСТОЯНИЕ ПЕРЕД ПРОГОНОМ (добавлено тестировщиком 12.11.2026).
#
# ЧТО БЫЛО. Раскладка панелей сохраняется в PlayerPrefs, поэтому каждый прогон начинался с того
# состояния, в котором закончился предыдущий. Один и тот же код давал разное: то «исчезло 0»,
# то «исчезло 41 из 140». Приёмка не может зависеть от того, что было в прошлый раз.
# ЧТО СТАЛО. По флагу `-CleanState` настройки приложения сбрасываются (`pm clear`), после чего
# разрешения выдаются заново — стартовая картина одинаковая у всех прогонов.
if ($CleanState) {
    Write-Host "  чистое состояние: сбрасываю настройки приложения"
    $null = Adb-Run @('shell', 'pm', 'clear', $Package)
    Start-Sleep -Seconds 2
    foreach ($perm in @('android.permission.CAMERA', 'android.permission.RECORD_AUDIO', 'com.picovr.permission.SPATIAL_DATA')) {
        $null = Adb-Run @('shell', 'pm', 'grant', $Package, $perm)
    }
    Write-Host "  разрешения выданы заново (камера, микрофон, пространственные данные)"
}

$null = Adb-Run @('shell', 'am', 'kill-all')
$null = Adb-Run @('shell', 'pm', 'trim-caches', '1000G')
Start-Sleep -Seconds 2

$null = Adb-Run @('shell', 'am', 'force-stop', $Package)
Start-Sleep -Seconds 3
$null = Adb-Run @('logcat', '-c')

# ЗАПУСК С EXTRA-ФЛАГАМИ. `monkey` из гейта extras передать не может, поэтому здесь явный `am start`:
# без флага `kv_uisweep` приложение обязано вести себя как обычно и НЕ начинать обход.
$startArgs = @('shell', 'am', 'start', '-n', ($Package + '/' + $Activity), '-e', 'kv_uisweep', '1')
if ($Mode -ne 'auto') { $startArgs += @('-e', 'kv_mode', $Mode) }
$launch = Adb-Run $startArgs
$launchHead = (@($launch -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -First 2) -join ' ')
Write-Host ("  запуск: " + $launchHead)
if ($launch -match 'Error') {
    Write-Host ("  ОШИБКА ЗАПУСКА: " + $launch) -ForegroundColor Red
    exit 1
}

# ЖДЁМ ИТОГОВУЮ СТРОКУ. Приложение после отчёта завершается само, поэтому ожидание конечно.
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$log = ''
$finished = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 5
    $log = Adb-Run @('logcat', '-d')
    if ($log -match '\[KvUiSweep\]\s*ИТОГ') { $finished = $true; break }
    if ($log -match 'FATAL EXCEPTION') { break }
}

# ЖУРНАЛ УСТРОЙСТВА — В ФАЙЛ (доказательство и для прогона, и для проверки реального стола).
try {
    $logDirFull = Split-Path $LogOut -Parent
    if ($logDirFull -and -not (Test-Path -LiteralPath $logDirFull)) { New-Item -ItemType Directory -Path $logDirFull -Force | Out-Null }
    [System.IO.File]::WriteAllText($LogOut, $log, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("  журнал устройства сохранён: " + $LogOut)
} catch {
    Write-Host ("  журнал устройства сохранить не удалось: " + $_.Exception.Message)
}

$problems = @()
if ($log -match 'FATAL EXCEPTION') { $problems += 'приложение упало во время прогона (FATAL EXCEPTION)' }
if (-not $finished) { $problems += ("итоговой строки нет за " + $TimeoutSeconds + " с — прогон не дошёл до конца") }

# ЧТО ПЕРЕХВАТИЛО ЭКРАН (проверено на практике 12.11.2026).
#
# ЧТО БЫЛО. Прогон на планшете вставал после четвёртого элемента: приложение живо, а кадров больше
# нет. Причина оказалась не в приложении, а в СИСТЕМЕ: нажатие одного из элементов увело приложение
# на экран MIUI «О приложении» (`com.miui.securitycenter/...ApplicationsDetailsActivity`), Unity
# получил `APP_CMD_TERM_WINDOW`, цикл кадров встал, и корутина прогона замерла без итоговой строки.
# Молчаливый таймаут в такой ситуации — бесполезный вердикт: непонятно ни кто виноват, ни что делать.
# Теперь причина называется прямо, а виновник берётся из последней строки `press=`, которую
# приложение печатает ПЕРЕД нажатием.
if (-not $finished) {
    # ПЕРВЫМ ДЕЛОМ — ЖИВ ЛИ ПРОЦЕСС (добавлено тестировщиком 12.11.2026).
    #
    # ЧТО БЫЛО. Скрипт сообщал «экран перехватило постороннее окно», хотя на самом деле приложение
    # БЫЛО УБИТО системой: Android чистил память (в журнале десятки строк «Process … has died»), после
    # чего выходил рабочий стол. Вердикт «перехватило окно» уводил бы в ложную сторону — искали бы
    # чужое окно вместо нехватки памяти.
    $pidText = ''
    try { $pidText = (Adb-Run @('shell', 'pidof', $Package)).Trim() } catch { $pidText = '' }
    if ([string]::IsNullOrWhiteSpace($pidText) -or $pidText -match 'not found|error') {
        $problems += 'приложение не работает: процесс завершился во время прогона (вероятно, система освобождала память)'
    }

    $focused = ''
    try {
        $focused = Adb-Run @('shell', 'dumpsys', 'activity', 'activities')
        $m = [regex]::Match($focused, 'topResumedActivity=ActivityRecord\{\S+ \S+ ([^/\s]+)/(\S+)')
        if ($m.Success) { $focused = $m.Groups[1].Value + '/' + $m.Groups[2].Value } else { $focused = '' }
    } catch { $focused = '' }

    if ($focused -and $focused -notmatch [regex]::Escape($Package)) {
        $problems += ("экран перехватило постороннее окно: " + $focused + " — приложение ушло в фон, прогон встал")
    }

    $lastPress = ($log -split "`n" | Where-Object { $_ -match '\[KvUiSweep\]\s*press=' } | Select-Object -Last 1)
    if ($lastPress) {
        $problems += ("последнее нажатие перед остановкой: " + $lastPress.Trim())
    } else {
        $problems += 'строки press= нет — по журналу нельзя понять, на каком элементе прогон встал'
    }
}

# УБОРКА ЗА ПРОГОНОМ. Если приложение ушло на системный экран, оставлять устройство в этом
# состоянии нельзя: следующий прогон стартовал бы поверх чужого окна и падал бы по той же причине.
if ($focused -and $focused -notmatch [regex]::Escape($Package)) {
    $null = Adb-Run @('shell', 'input', 'keyevent', '4')
    Start-Sleep -Seconds 1
}
$null = Adb-Run @('shell', 'am', 'force-stop', $Package)

# СТРОКА СТАРТА: без неё нельзя понять, на каком элементе всё встало.
$startLine = ($log -split "`n" | Where-Object { $_ -match '\[KvUiSweep\]\s*СТАРТ' } | Select-Object -Last 1)
if ($startLine) { Write-Host ("  " + $startLine.Trim()) }
else { $problems += 'нет строки СТАРТ — неизвестно, что вообще начало выполняться' }

$elements = @($log -split "`n" | Where-Object { $_ -match '\[KvUiSweep\]\s*element=' })
$failed = @($elements | Where-Object { $_ -match 'ok=0' })
$exempt = @($elements | Where-Object { $_ -match 'exempt=(?!-)\S' })
# ИСЧЕЗЛО (12.11.2026). Элемент не найден по пути после перепоиска и пересборки списка: интерфейс
# перестроился, проверять стало нечего. Это НЕ «кнопка не работает» и провалом не считается —
# иначе прогон обвинял бы исправный интерфейс (на планшете так были обвинены 45 работающих
# переключателей панели настроек). Считается ОТДЕЛЬНО и печатается числом.
$vanished = @($elements | Where-Object { $_ -match ' исчез' })

Write-Host ("  элементов в журнале: " + $elements.Count + " · провалов: " + $failed.Count +
            " · исключений: " + $exempt.Count + " · исчезло: " + $vanished.Count)

$totalLine = ($log -split "`n" | Where-Object { $_ -match '\[KvUiSweep\]\s*ИТОГ' } | Select-Object -Last 1)
if ($totalLine) { Write-Host ("  " + $totalLine.Trim()) }
$verdictLine = ($log -split "`n" | Where-Object { $_ -match '\[KvUiSweep\]\s*ВЕРДИКТ' } | Select-Object -Last 1)
if ($verdictLine) { Write-Host ("  " + $verdictLine.Trim()) }

if ($elements.Count -lt $MinElements) {
    $problems += ("элементов " + $elements.Count + " меньше порога " + $MinElements + " — такой прогон ничего не доказывает")
}

foreach ($f in $failed) {
    # ИСКЛЮЧЕНИЯ (элемент физически не работает без железа) провалом не считаются, но печатаются с
    # причиной: «мы это знаем» — не то же самое, что «мы это скрыли».
    if ($f -match 'exempt=(?!-)\S') { continue }
    # ИСЧЕЗНУВШИЕ — тоже не провал: это дефект СНИМКА, а не кнопки.
    if ($f -match ' исчез') { continue }
    $problems += ("без эффекта: " + $f.Trim())
}

# НО МАССОВОЕ ИСЧЕЗНОВЕНИЕ — ПРОВАЛ, А НЕ «ТАК ПОЛУЧИЛОСЬ» (добавлено тестировщиком 12.11.2026).
#
# ЗАЧЕМ ОГРАНИЧИТЕЛЬ. Фраза «элемент исчез после перестроения» законна: интерфейс действительно
# пересобирается, и путь может не найтись. Но если таких элементов МНОГО, значит прогон проверил
# меньшую часть интерфейса, а вердикт «пройдено» относился бы только к тому, что успели потрогать.
# Порог — доля от найденных элементов: небольшое «мельтешение» допустимо, массовая потеря — нет.
$vanishedLimit = [Math]::Max(3, [int]($elements.Count * 0.10))
if ($vanished.Count -gt $vanishedLimit) {
    $problems += ("исчезло " + $vanished.Count + " элементов из " + $elements.Count +
        " (предел " + $vanishedLimit + ") — интерфейс перестраивался так сильно, что прогон проверил не всё")
}

# ФАЙЛ ОТЧЁТА — если доступен (release-сборка может закрыть каталог; тогда доказательством служит
# журнал, и это честно написано в выводе).
$reportList = Adb-Run @('shell', 'ls', ('/storage/emulated/0/Android/data/' + $Package + '/files/Reports'))
if ($reportList -match '_ui_sweep') {
    Write-Host ("  файлы отчёта на устройстве: " + ((@($reportList -split "`n" | Where-Object { $_ -match '_ui_sweep' })) -join ', '))
} else {
    Write-Host "  файл отчёта на устройстве недоступен (release-подпись) — доказательство берём из журнала"
}

if ($problems.Count -eq 0) {
    Write-Host "САМОПРОГОН ПРОЙДЕН" -ForegroundColor Green
    exit 0
}
Write-Host "САМОПРОГОН НЕ ПРОЙДЕН:" -ForegroundColor Red
$problems | Select-Object -First 25 | ForEach-Object { Write-Host ("  - " + $_) }
exit 1
