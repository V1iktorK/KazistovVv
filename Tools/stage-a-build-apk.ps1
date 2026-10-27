<#
    ПЕРЕСБОРКА APK ПОСЛЕ ПРАВОК (Этап А: Б2 + A2 + §31) — С ДОКАЗАТЕЛЬСТВАМИ.

    ПОЧЕМУ ОТДЕЛЬНЫЙ СКРИПТ, А НЕ «ЗАПУСТИЛ И ЖДУ». Сборка APK — это часы, и весь смысл правок
    Б2 в том, что она станет КОРОЧЕ. Значит, нужны числа: сколько вариантов осталось отсечь,
    что напечатал `IPreprocessShaders`, вернулись ли тесты на место (живое доказательство A2),
    и только потом — файл APK. Скрипт собирает ровно эти строки.

    ЧТО СЧИТАЕТСЯ ДОКАЗАТЕЛЬСТВОМ:
      • `[KvStrip]` — сколько вариантов постобработки снято (до/после) и по какому основанию;
      • `Full variant space` / `After scriptable stripping` для `UberPost` — цена шейдера
        до отсечения (в §30 было 92 160) и после;
      • `[KvXr] Тесты убраны из сборки APK: … файлов N` и `… тесты возвращены в Assets …` —
        живое доказательство исправления дефекта A2 (прежде возврат не срабатывал и молчал);
      • `[KvXr] APK собран: …` + файл APK (размер, SHA256).

    Возврат: 0 — APK собран; 1 — сборка не удалась (причина в логе).

    ПРИМЕР
      powershell -ExecutionPolicy Bypass -File Tools\stage-a-build-apk.ps1
#>
param(
    [string]$Project = "C:\KazistovVv_Build",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe",
    # ИМЯ ЛОГА С МЕТКОЙ ВРЕМЕНИ — НЕ КОСМЕТИКА: Unity при запуске с -logFile ПЕРЕЗАПИСЫВАЕТ файл,
# и лог предыдущей сборки теряется. Именно так пропали строки с числами 442 368 → 1 536
# (они были сняты с лога неудачной сборки, а успешная сборка его затёрла).
[string]$LogName = ("xr-build-apk-" + (Get-Date -Format 'yyyy-MM-dd_HHmm') + ".log"),
    [int]$TimeoutMinutes = 720
)

$ErrorActionPreference = 'Continue'
$logs = Join-Path $Project 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$logPath = Join-Path $logs $LogName

# ЗАЩИТА ОТ ВТОРОГО РЕДАКТОРА. Два редактора Unity на одном проекте портят `Library`, поэтому
# запускать сборку при работающем редакторе нельзя.
#
# ДЕФЕКТ, ИСПРАВЛЕННЫЙ 16.10.2026 (§35). Прежняя проверка была `Get-Process Unity` — то есть
# «есть процесс с именем Unity». Под неё попадала СЛУЖБА ЛИЦЕНЗИЙ Unity Hub: она запускается
# как `unity.exe serve` из папки Hub, весит 4 МБ и РАБОТАТЬ ОБЯЗАНА — именно она отдаёт
# лицензию редактору. Итог: сборка отказывалась стартовать «из-за второго инстанса», которого
# нет, а останавливать службу нельзя — без неё редактор не запустится вовсе (дефект A19-класса:
# штатная команда из документации не работает).
#
# ТЕПЕРЬ РЕДАКТОРОМ СЧИТАЕТСЯ ТОЛЬКО ТО, ЧТО ИМ ЯВЛЯЕТСЯ: процесс, запущенный ИЗ ПАПКИ РЕДАКТОРА
# (`...\Hub\Editor\<версия>\Editor\Unity.exe`). Служба лицензий живёт в `...\Unity Hub\resources\`
# и под проверку больше не попадает.
$editors = @()
foreach ($p in (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue)) {
    $exe = $p.ExecutablePath
    if (-not $exe) { continue }
    $isHubService = $exe -match '\\Unity Hub\\' -or ($p.CommandLine -and $p.CommandLine -match '\bserve\b')
    $isEditor = $exe -match '\\Hub\\Editor\\' -or $exe -match '\\Editor\\Unity\.exe$'
    if ($isEditor -and -not $isHubService) {
        $editors += ("PID {0} · {1}" -f $p.ProcessId, $exe)
    }
}
if ($editors.Count -gt 0) {
    Write-Host "ДРУГОЙ РЕДАКТОР Unity УЖЕ РАБОТАЕТ — второй инстанс не допускается. Останавливаюсь."
    foreach ($e in $editors) { Write-Host ("  " + $e) }
    Write-Host "  (служба лицензий Unity Hub работе не мешает и намеренно не считается редактором)"
    exit 1
}

# КЭШ GRADLE — НА ЛАТИНСКОМ ПУТИ, И ЭТО НЕ КОСМЕТИКА (замерено 08.10.2026).
# Шаг сборки `prefab` (часть Android Gradle Plugin) запускает java с классом
# `com.google.prefab.cli.AppKt`, взяв путь из кэша Gradle — а кэш по умолчанию лежит
# в профиле пользователя. Профиль называется `C:\Users\Ольга`, и в отчёте Gradle путь
# пришёл искажённым: `C:\Users\╬ы№ур\.gradle\...` → `ClassNotFoundException`.
# Сборка обрывалась на `:unityLibrary:configureCMakeRelWithDebInfo[arm64-v8a]`.
# Это тот же класс проблемы, что и «путь проекта с не-ASCII символами» из §28, только
# теперь — путь КЭША. Лечение: перенести кэш Gradle на латинский путь через переменную
# окружения (Unity передаёт её дочернему процессу Gradle).
if (-not $env:GRADLE_USER_HOME -or $env:GRADLE_USER_HOME -match '[^\x20-\x7E]') {
    $env:GRADLE_USER_HOME = 'C:\gradle_home'
    Write-Host ("кэш Gradle перенесён на латинский путь: " + $env:GRADLE_USER_HOME)
}
Write-Host "=== СБОРКА APK (Этап А) ==="
Write-Host ("лог: " + $logPath)
$argList = @(
    '-batchmode', '-quit',
    '-projectPath', ('"' + $Project + '"'),
    '-executeMethod', 'KazistovVvXrEditor.KvXrTools.BuildAndroidApk',
    '-logFile', ('"' + $logPath + '"')
)
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $Unity -ArgumentList $argList -PassThru -NoNewWindow

# Ждём выход, изредка печатая прогресс отсечения вариантов.
while (-not $p.HasExited -and $sw.Elapsed.TotalMinutes -lt $TimeoutMinutes) {
    Start-Sleep -Seconds 60
    if (Test-Path $logPath) {
        $m = Select-String -Path $logPath -Pattern '\[(\d+)s\]\s+(\d+) / (\d+) variants ready' -ErrorAction SilentlyContinue |
             Select-Object -Last 1
        if ($m) { Write-Host ("  " + (Get-Date -Format 'HH:mm:ss') + " · " + $m.Matches[0].Groups[2].Value + "/" + $m.Matches[0].Groups[3].Value + " вариантов") }
    }
}

if (-not $p.HasExited) {
    Write-Host ("ТАЙМАУТ " + $TimeoutMinutes + " мин — процесс снимается")
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    exit 1
}
Write-Host ("сборка заняла: " + [math]::Round($sw.Elapsed.TotalMinutes,1) + " мин")

Write-Host ""
Write-Host "=== ДОКАЗАТЕЛЬСТВА ИЗ ЛОГА ==="
foreach ($pattern in @('\[KvStrip\]', '\[KvXr\] Тесты', '\[KvXr\] APK собран', '\[KvXr\] СБОРКА APK НЕ УДАЛАСЬ',
                       'Compiling shader "Hidden/Universal Render Pipeline/UberPost"', 'Pass "UberPostXR"',
                       'Serialized binary data for shader Hidden/Universal Render Pipeline/UberPost',
                       'After scriptable stripping', 'Pass ForwardLit \(vp, vulkan\) finished')) {
    $hits = Select-String -Path $logPath -Pattern $pattern -ErrorAction SilentlyContinue | Select-Object -First 6
    foreach ($h in $hits) { Write-Host ("  " + $h.Line.Trim()) }
}

Write-Host ""
Write-Host "=== ФАЙЛ APK ==="
$ok = $false
foreach ($apk in @((Join-Path $Project 'Builds\KazistovVv.apk'), (Join-Path $Project 'KazistovVv.apk'))) {
    if (-not (Test-Path $apk)) { continue }
    $item = Get-Item $apk
    Write-Host ("  {0} · {1:N1} МБ · {2} · SHA256 {3}" -f $apk, ($item.Length / 1MB), $item.LastWriteTime, (Get-FileHash $apk -Algorithm SHA256).Hash)
    $ok = $true
}
if (-not $ok) { Write-Host "  APK НЕ НАЙДЕН — смотри лог" }

if ($ok) { exit 0 } else { exit 1 }
