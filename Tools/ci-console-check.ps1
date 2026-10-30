#requires -Version 5.1
<#
.SYNOPSIS
    Батчмод-гейт «чистой консоли» для проекта KazistovVv: компиляция Unity без графики
    и разбор лога на ошибки/предупреждения. «Чистая консоль» становится воспроизводимым
    гейтом, а не разовым нажатием Play.

.DESCRIPTION
    Скрипт делает ровно одно: запускает Unity в batchmode (или разбирает уже готовый лог)
    и считает по нему ошибки компиляции, предупреждения и служебные сообщения редактора.

    Что считается (категории независимы, счётчики попадают в сводку и в JSON):
      errors        'error CS', 'Shader error'            — гейт: >0 → exit 1
      warningsCs    'warning CS'                          — предупреждения компилятора C#
      menuItems     'MenuItem'                            — в т.ч. «MenuItem … was added twice»
      inputManager  'Input Manager'                       — в т.ч. сообщение об устаревании
      exceptions    'Exception:' / 'Unhandled exception'  — падения рантайма/редактора
      missingScripts 'Missing (Mono Script)'              — потерянные ссылки на скрипты

    Дополнительно (важно для этого проекта — см. PROJECT_CONTEXT.md §0.8.4): батч-прогон
    Unity иногда перезаписывает файлы ProjectSettings. Скрипт снимает хеши
    ProjectSettings\* ДО и ПОСЛЕ прогона и печатает изменившиеся файлы, чтобы их можно
    было осознанно откатить (cm undo) или принять.

    Два предупреждения из этого проекта — «MenuItem Window/AB Unity MCP was added twice»
    и «This project uses Input Manager, which is marked for deprecation» — печатает только
    интерактивный редактор (они возникают при построении меню и загрузке layout). Поэтому
    у скрипта есть режим -AnalyzeOnly: разобрать Logs\Editor.log живой сессии теми же
    шаблонами.

.PARAMETER ProjectPath
    Корень Unity-проекта. По умолчанию — родитель каталога со скриптом (…\KazistovVv\Tools → …\KazistovVv).

.PARAMETER UnityExe
    Полный путь к Unity.exe. По умолчанию берётся версия, записанная в
    ProjectSettings\ProjectVersion.txt; если её нет — самая новая в Unity Hub.

.PARAMETER LogName
    Имя файла лога внутри <ProjectPath>\Logs. По умолчанию ci-console.log.

.PARAMETER AnalyzeOnly
    Не запускать Unity, а разобрать существующий лог (см. -LogFile).

.PARAMETER LogFile
    Путь к разбираемому логу в режиме -AnalyzeOnly. По умолчанию <ProjectPath>\Logs\Editor.log.

.PARAMETER TimeoutMinutes
    Жёсткий таймаут прогона. Unity убивается, гейт падает. По умолчанию 30 минут.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\ci-console-check.ps1

.EXAMPLE
    # разобрать лог живой сессии редактора (там видны MenuItem / Input Manager)
    powershell -ExecutionPolicy Bypass -File Tools\ci-console-check.ps1 -AnalyzeOnly

.NOTES
    Код возврата: 0 — ошибок нет; 1 — есть ошибки, Unity упал или сработал таймаут.
#>
[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$UnityExe,
    [string]$LogName = 'ci-console.log',
    [switch]$AnalyzeOnly,
    [string]$LogFile,
    [int]$TimeoutMinutes = 30
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot НЕЛЬЗЯ читать в значении по умолчанию для параметра. При [CmdletBinding()]
# Windows PowerShell 5.1 связывает параметры РАНЬШЕ, чем переменная заполнена: получается
# пустая строка, Split-Path падает с ParameterArgumentValidationErrorEmptyStringNotAllowed,
# и скрипт не выполняется вообще. В PowerShell 7 тот же код работает — поэтому дефект не
# проявлялся. Вычисляем корень проекта здесь, в теле: работает и в 5.1, и в 7.
if ([string]::IsNullOrEmpty($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
if ([string]::IsNullOrEmpty($ProjectPath)) {
    throw 'Не удалось определить корень проекта: пуст $PSScriptRoot. Запускайте скрипт файлом: -File <путь>\Tools\ci-console-check.ps1'
}

function Resolve-UnityExe {
    param([string]$Project, [string]$Explicit)

    if ($Explicit) {
        if (-not (Test-Path -LiteralPath $Explicit)) { throw "Unity.exe не найден: $Explicit" }
        return (Resolve-Path -LiteralPath $Explicit).Path
    }

    # 1) Версия, которой проект открыт по факту (ProjectVersion.txt) — единственный
    #    правильный выбор: другая версия пересоберёт Library и ProjectSettings.
    $versionFile = Join-Path $Project 'ProjectSettings\ProjectVersion.txt'
    if (Test-Path -LiteralPath $versionFile) {
        $line = (Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(\S+)' |
                 Select-Object -First 1).Matches.Groups[1].Value
        if ($line) {
            $candidate = Join-Path (Join-Path 'C:\Program Files\Unity\Hub\Editor' $line) 'Editor\Unity.exe'
            if (Test-Path -LiteralPath $candidate) { return $candidate }
            Write-Warning "Версия из ProjectVersion.txt ($line) не установлена — беру самую новую из Hub."
        }
    }

    # 2) Fallback: самая новая установленная версия (сортировка по [version], а не по строке,
    #    иначе '6000.5.9f1' оказалась бы «меньше» '6000.5.10f1').
    $hubRoot = 'C:\Program Files\Unity\Hub\Editor'
    if (-not (Test-Path -LiteralPath $hubRoot)) { throw "Нет каталога Unity Hub: $hubRoot" }
    $installs = Get-ChildItem -LiteralPath $hubRoot -Directory | ForEach-Object {
        $parsed = $null
        $clean = ($_.Name -replace '[fabcpx]\d+$', '')
        if ([version]::TryParse($clean, [ref]$parsed)) { [pscustomobject]@{ Name = $_.Name; Version = $parsed } }
    } | Sort-Object Version -Descending
    if (-not $installs) { throw "В $hubRoot нет ни одной установки Unity" }
    $newest = Join-Path (Join-Path $hubRoot $installs[0].Name) 'Editor\Unity.exe'
    if (-not (Test-Path -LiteralPath $newest)) { throw "Unity.exe не найден: $newest" }
    return $newest
}

function Get-ProjectSettingsHashes {
    param([string]$Project)
    $dir = Join-Path $Project 'ProjectSettings'
    $map = @{}
    if (-not (Test-Path -LiteralPath $dir)) { return $map }
    foreach ($file in Get-ChildItem -LiteralPath $dir -File) {
        $map[$file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    return $map
}

function Measure-LogPattern {
    param([string]$Path, [string[]]$Pattern)
    if (-not (Test-Path -LiteralPath $Path)) { return 0 }
    $hits = Select-String -Path $Path -Pattern $Pattern -ErrorAction SilentlyContinue
    if ($null -eq $hits) { return 0 }
    return @($hits).Count
}

function Get-LogStats {
    param([string]$Path)

    $stats = [ordered]@{
        errors              = Measure-LogPattern -Path $Path -Pattern @('error CS', 'Shader error')
        warningsCs          = Measure-LogPattern -Path $Path -Pattern @('warning CS')
        menuItems           = Measure-LogPattern -Path $Path -Pattern @('MenuItem')
        menuItemsAddedTwice = Measure-LogPattern -Path $Path -Pattern @('was added twice')
        inputManager        = Measure-LogPattern -Path $Path -Pattern @('Input Manager')
        exceptions          = Measure-LogPattern -Path $Path -Pattern @('Exception:', 'Unhandled exception')
        missingScripts      = Measure-LogPattern -Path $Path -Pattern @('Missing \(Mono Script\)')
    }
    # 30.10.2026: `warning UAC1001` раньше не считался вовсе — сводка говорила «предупреждений
    # в Assets нет», хотя три штуки лежат в Assets (KvGraphicsService, KvXrMrRoom). Считаем
    # отдельной строкой, чтобы число было видно в отчёте.
    $stats.warningsUac = Measure-LogPattern -Path $Path -Pattern @('warning UAC')
    $stats.logBytes = if (Test-Path -LiteralPath $Path) { (Get-Item -LiteralPath $Path).Length } else { 0 }

    # Что именно нашлось — чтобы в отчёте были ФАКТЫ, а не счётчики. Кадры стека
    # (UnityEditor.*, UnityEngine.*, System.*) отбрасываем: у Debug.LogWarning их три на
    # одно сообщение, и без фильтра счётчик показывал бы «4 предупреждения» вместо одного.
    $isStackFrame = '^\s*(UnityEditor|UnityEngine|System|\(wrapper)'
    $stats.menuItemLines = @()
    $stats.inputManagerLines = @()
    $stats.errorLines = @()
    if ($stats.menuItems -gt 0) {
        $stats.menuItemLines = @(Select-String -Path $Path -Pattern 'MenuItem' -ErrorAction SilentlyContinue |
            Where-Object { $_.Line -notmatch $isStackFrame } |
            Select-Object -First 10 | ForEach-Object { $_.Line.Trim() })
    }
    if ($stats.inputManager -gt 0) {
        $stats.inputManagerLines = @(Select-String -Path $Path -Pattern 'Input Manager' -ErrorAction SilentlyContinue |
            Where-Object { $_.Line -notmatch $isStackFrame } |
            Select-Object -First 10 | ForEach-Object { $_.Line.Trim() })
    }
    if ($stats.errors -gt 0) {
        $stats.errorLines = @(Select-String -Path $Path -Pattern 'error CS', 'Shader error' -ErrorAction SilentlyContinue |
            Select-Object -First 10 | ForEach-Object { $_.Line.Trim() })
    }

    # 30.10.2026: ИСКЛЮЧЕНИЯ РАЗДЕЛЯЮТСЯ НА ДЕФЕКТ И ШУМ.
    #
    # ПОЧЕМУ ЭТО ВАЖНО. До этой правки поле `exceptions` считалось, печаталось и попадало
    # в JSON — и НИКОГДА не участвовало в вердикте. История `Logs\ci-console-history.jsonl`
    # показывает шесть «зелёных» прогонов с непустыми исключениями (16.09 — 4, 17.09 — 2,
    # 03.10 — 1 и 2, 05.10 — 2). То есть «гейт пройден» не означал «консоль чистая».
    #
    # ШУМ. `Pipe is broken` и HTTP-2 abort приходят от транспорта редактора, а не от кода
    # проекта; если валить гейт на них, он станет красным навсегда и его перестанут читать.
    # Поэтому шум считается отдельно и печатается, но не валит гейт.
    $noise = 'Pipe is broken|request stream was aborted|ConnectionAbortedException|abort_threads|Failed aborting id'
    $stats.exceptionLines = @()
    $stats.exceptionsApp = 0
    $stats.exceptionsNoise = 0
    if ($stats.exceptions -gt 0) {
        $exc = @(Select-String -Path $Path -Pattern 'Exception:', 'Unhandled exception' -ErrorAction SilentlyContinue |
            ForEach-Object { $_.Line.Trim() })
        $stats.exceptionsNoise = @($exc | Where-Object { $_ -match $noise }).Count
        $appExc = @($exc | Where-Object { $_ -notmatch $noise })
        $stats.exceptionsApp = $appExc.Count
        $stats.exceptionLines = @($appExc | Select-Object -First 10)
    }
    return $stats
}

# ── подготовка ──────────────────────────────────────────────────────────────
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'ProjectSettings'))) {
    throw "Не похоже на Unity-проект (нет ProjectSettings): $ProjectPath"
}
$logsDir = Join-Path $ProjectPath 'Logs'
if (-not (Test-Path -LiteralPath $logsDir)) { New-Item -ItemType Directory -Path $logsDir -Force | Out-Null }
if (-not $LogFile) { $LogFile = Join-Path $logsDir 'Editor.log' }
$logPath = if ($AnalyzeOnly) { $LogFile } else { Join-Path $logsDir $LogName }

Write-Host "Проект : $ProjectPath"
Write-Host "Лог    : $logPath"

$settingsBefore = Get-ProjectSettingsHashes -Project $ProjectPath
# Код возврата берём из ДВУХ источников: из лога Unity (надёжный) и из объекта процесса
# (может быть недоступен) — см. комментарий у WaitForExit ниже.
$logReturnCode = $null
$processExitCode = $null
$batchExitOk = $false
$killedByTimeout = $false

if (-not $AnalyzeOnly) {
    $unity = Resolve-UnityExe -Project $ProjectPath -Explicit $UnityExe
    Write-Host "Unity  : $unity"
    if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }
    Write-Host "Запуск : -batchmode -nographics -quit (таймаут $TimeoutMinutes мин)…"

    # Пути ОБЯЗАНЫ быть в кавычках: Start-Process склеивает элементы -ArgumentList через
    # пробел и сам ничего не экранирует, а путь проекта содержит пробел
    # («…\новое пространство\KazistovVv»). Без кавычек Unity получает обрезанный
    # -projectPath, мгновенно падает с кодом 1 и НЕ создаёт лог.
    $arguments = @(
        '-batchmode', '-nographics', '-quit',
        '-projectPath', ('"' + $ProjectPath + '"'),
        '-logFile', ('"' + $logPath + '"')
    )
    $stdoutPath = Join-Path $logsDir 'ci-console.stdout.log'
    $stderrPath = Join-Path $logsDir 'ci-console.stderr.log'
    $process = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    # Ждём выход и забираем код. Process.ExitCode у объекта из `Start-Process -PassThru`
    # в Windows PowerShell 5.1 для Unity приходит пустым (проверено: у cmd.exe тот же код
    # читается, у Unity — нет), поэтому единственным НАДЁЖНЫМ источником кода возврата
    # остаётся сам лог Unity: он печатает «…will terminate with return code N» последней
    # строкой. ExitCode процесса используется как дополнительный сигнал, если доступен.
    $timeoutMs = [int]($TimeoutMinutes * 60 * 1000)
    if ($process.WaitForExit($timeoutMs)) {
        $processExitCode = $process.ExitCode
    } else {
        $killedByTimeout = $true
        Write-Warning "Таймаут $TimeoutMinutes мин — Unity убит (PID $($process.Id))."
        try { Stop-Process -Id $process.Id -Force -ErrorAction Stop } catch { }
        $processExitCode = -1
    }

    if (Test-Path -LiteralPath $logPath) {
        $rcLine = Select-String -Path $logPath -Pattern 'terminate with return code (\d+)' -ErrorAction SilentlyContinue |
            Select-Object -Last 1
        if ($rcLine) { $logReturnCode = [int]$rcLine.Matches.Groups[1].Value }
        $batchExitOk = [bool](Select-String -Path $logPath -Pattern 'Exiting batchmode successfully now!' -Quiet)
    }
    Write-Host ("Код возврата Unity: лог={0}, процесс={1}, маркер успешного выхода={2}" -f `
        $(if ($null -eq $logReturnCode) { 'н/д' } else { $logReturnCode }), `
        $(if ($null -eq $processExitCode) { 'н/д' } else { $processExitCode }), `
        $batchExitOk)
    foreach ($stream in @($stdoutPath, $stderrPath)) {
        if ((Test-Path -LiteralPath $stream) -and (Get-Item -LiteralPath $stream).Length -gt 0) {
            Write-Host ("--- {0} ---" -f (Split-Path -Leaf $stream))
            Get-Content -LiteralPath $stream -Tail 20 | ForEach-Object { Write-Host ("  " + $_) }
        }
    }
}

# Гейт обязан падать ЧИСТО, а не исключением: отсутствие лога — это тоже провал прогона.
if (-not (Test-Path -LiteralPath $logPath)) {
    Write-Host ("ГЕЙТ ПРОВАЛЕН: Unity не создал лог {0}." -f $logPath) -ForegroundColor Red
    Write-Host ("  Смотри stdout/stderr рядом: {0}" -f (Join-Path $logsDir 'ci-console.stdout.log'))
    exit 1
}

# ── разбор ──────────────────────────────────────────────────────────────────
$stats = Get-LogStats -Path $logPath

$settingsChanged = @()
$settingsAfter = Get-ProjectSettingsHashes -Project $ProjectPath
foreach ($name in $settingsAfter.Keys) {
    if ($settingsBefore.ContainsKey($name)) {
        if ($settingsBefore[$name] -ne $settingsAfter[$name]) { $settingsChanged += $name }
    } else {
        $settingsChanged += "$name (новый)"
    }
}

# ── сводка ──────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '────────── СВОДКА ──────────'
Write-Host ("Errors: {0}, Warnings: {1}" -f $stats.errors, $stats.warningsCs)
Write-Host ("  error CS / Shader error        : {0}" -f $stats.errors)
Write-Host ("  warning CS                     : {0}" -f $stats.warningsCs)
Write-Host ("  warning UAC (анализатор)       : {0}" -f $stats.warningsUac)
Write-Host ("  MenuItem (в т.ч. added twice)   : {0}  (added twice: {1})" -f $stats.menuItems, $stats.menuItemsAddedTwice)
Write-Host ("  Input Manager (в т.ч. deprecat) : {0}" -f $stats.inputManager)
Write-Host ("  Exception / Unhandled exception : {0}  (дефект: {1}, шум: {2})" -f $stats.exceptions, $stats.exceptionsApp, $stats.exceptionsNoise)
Write-Host ("  Missing (Mono Script)           : {0}" -f $stats.missingScripts)
Write-Host ("  размер лога                     : {0:N0} байт" -f $stats.logBytes)
foreach ($line in $stats.menuItemLines) { Write-Host ("    [MenuItem] {0}" -f $line) }
foreach ($line in $stats.inputManagerLines) { Write-Host ("    [Input]    {0}" -f $line) }
foreach ($line in $stats.exceptionLines) { Write-Host ("    [Exc]      {0}" -f $line) -ForegroundColor Red }
foreach ($line in $stats.errorLines) { Write-Host ("    [Error]    {0}" -f $line) -ForegroundColor Red }
if ($settingsChanged.Count -gt 0) {
    Write-Warning ("Батч-прогон изменил ProjectSettings: {0}" -f ($settingsChanged -join ', '))
    Write-Host '    Проверь изменения и при необходимости откати: cm undo <файл>'
} else {
    Write-Host '  ProjectSettings: без изменений'
}
if ($AnalyzeOnly) {
    Write-Host '  Режим -AnalyzeOnly: Unity не запускался, код возврата определяется только ошибками.'
}

$summary = [ordered]@{
    ts               = (Get-Date).ToString('s')
    project          = $ProjectPath
    log              = $logPath
    analyzeOnly      = [bool]$AnalyzeOnly
    unityReturnCode  = $logReturnCode      # из самого лога Unity (надёжный источник)
    processExitCode  = $processExitCode    # из объекта процесса (может быть н/д)
    batchExitMarker  = $batchExitOk        # 'Exiting batchmode successfully now!'
    timeout          = $killedByTimeout
    stats            = $stats
    projectSettingsChanged = $settingsChanged
    ok               = ($stats.errors -eq 0 -and $stats.exceptionsApp -eq 0 -and -not $killedByTimeout -and
                        ($AnalyzeOnly -or ($batchExitOk -and
                         ($null -eq $logReturnCode -or $logReturnCode -eq 0) -and
                         ($null -eq $processExitCode -or $processExitCode -eq 0))))
}
$jsonPath = Join-Path $logsDir 'ci-console-summary.json'
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
Add-Content -LiteralPath (Join-Path $logsDir 'ci-console-history.jsonl') `
    -Value ($summary | ConvertTo-Json -Depth 6 -Compress) -Encoding UTF8
Write-Host "Сводка : $jsonPath"

# ── гейт ────────────────────────────────────────────────────────────────────
if ($stats.errors -gt 0) {
    Write-Host ("ГЕЙТ ПРОВАЛЕН: ошибок компиляции/шейдеров — {0}." -f $stats.errors) -ForegroundColor Red
    exit 1
}
# 30.10.2026: исключение приложения — провал гейта. Раньше это поле не проверялось, и гейт
# был зелёным при реальных исключениях (см. историю прогонов: 16.09, 17.09, 03.10 ×2, 05.10).
if ($stats.exceptionsApp -gt 0) {
    Write-Host ("ГЕЙТ ПРОВАЛЕН: исключений приложения — {0} (шум транспорта: {1})." -f $stats.exceptionsApp, $stats.exceptionsNoise) -ForegroundColor Red
    foreach ($line in $stats.exceptionLines) { Write-Host ("    [Exc] " + $line) -ForegroundColor Red }
    exit 1
}
if ($killedByTimeout) { Write-Host 'ГЕЙТ ПРОВАЛЕН: таймаут прогона Unity.' -ForegroundColor Red; exit 1 }
if (-not $AnalyzeOnly) {
    if (-not $batchExitOk) {
        Write-Host 'ГЕЙТ ПРОВАЛЕН: в логе нет маркера «Exiting batchmode successfully now!» — прогон не завершился штатно.' -ForegroundColor Red
        exit 1
    }
    if ($null -ne $logReturnCode -and $logReturnCode -ne 0) {
        Write-Host ("ГЕЙТ ПРОВАЛЕН: Unity вернул код {0} (из лога)." -f $logReturnCode) -ForegroundColor Red
        exit 1
    }
    if ($null -ne $processExitCode -and $processExitCode -ne 0) {
        Write-Host ("ГЕЙТ ПРОВАЛЕН: процесс Unity завершился с кодом {0}." -f $processExitCode) -ForegroundColor Red
        exit 1
    }
}

Write-Host 'ГЕЙТ ПРОЙДЕН: ошибок компиляции нет, исключений приложения нет.' -ForegroundColor Green
exit 0
