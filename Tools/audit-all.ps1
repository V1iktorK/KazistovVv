<#
    ПОЛНЫЙ ПРОГОН ПРОВЕРОК ОДНОЙ КОМАНДОЙ (сессия S44, 30.10.2026).

    ЗАЧЕМ. До этого каждый прогон собирался вручную из отдельных команд, и каждый раз
    что-то забывалось: то гейт без тестов, то тесты без гейта, то без проверки BOM.
    Здесь — один вход, одинаковый для машины владельца и для CI.

    ЧТО ДЕЛАЕТСЯ (по порядку, в одном процессе):
      1) проверка BOM и разбора всех Tools\*.ps1 — дешёвая, ловит дефект A19/E-11;
      2) EditMode-тесты в batchmode, с XML-отчётом;
      3) разбор лога ТЕМ ЖЕ гейтом, что в CI (-AnalyzeOnly), включая исключения приложения;
      4) сводка: сколько тестов, сколько упало, сколько пропущено, какой вердикт.

    КОД ВОЗВРАТА: 0 — всё прошло; 1 — есть провал. Пропущенные тесты БОЛЬШЕ НЕ «норма»:
    правило проекта с 30.10.2026 — самовыключающихся тестов быть не должно, поэтому любой
    пропуск печатается КРАСНЫМ и делает вердикт НЕ прошедшим (если только он не в списке
    известных допустимых ниже).

    ПРИМЕР:
        powershell -ExecutionPolicy Bypass -File Tools\audit-all.ps1
        powershell -ExecutionPolicy Bypass -File Tools\audit-all.ps1 -Tag s44 -KeepLogs
#>
param(
    [string]$Project = "C:\KazistovVv_Build",
    [string]$Tag = "audit",
    [switch]$KeepLogs,
    [int]$TestsTimeoutMinutes = 30
)

$ErrorActionPreference = 'Continue'
$script:failed = $false

function Write-Step([string]$text) {
    Write-Host ""
    Write-Host ("──── " + $text + " ────") -ForegroundColor Cyan
}

function Fail-Gate([string]$reason) {
    Write-Host ("ПРОВАЛ: " + $reason) -ForegroundColor Red
    $script:failed = $true
}

# ── 0. Окружение ────────────────────────────────────────────────────────────
if (-not (Test-Path -LiteralPath $Project)) {
    Write-Host ("нет папки проекта: " + $Project) -ForegroundColor Red
    exit 1
}
$Project = (Resolve-Path -LiteralPath $Project).Path

# НЕ-ASCII В ПУТИ — отдельная проверка, потому что Android-тулчейн на таком пути падает
# (дефекты E-03/A6/A21), а узнать об этом лучше здесь, чем через час сборки.
$physical = $Project
$item = Get-Item -LiteralPath $Project -Force
if ($item.LinkType -and $item.Target) { $physical = (Resolve-Path -LiteralPath $item.Target).Path }
Write-Host ("проект: логический " + $Project)
Write-Host ("        физический " + $physical)
if ($physical -match '[^\x20-\x7E]') {
    Write-Host "ВНИМАНИЕ: физический путь содержит не-ASCII символы — сборка APK может упасть (E-03/A6/A21)." -ForegroundColor Yellow
}

$logs = Join-Path $Project 'Logs'
if (-not (Test-Path -LiteralPath $logs)) { New-Item -ItemType Directory -Path $logs -Force | Out-Null }

$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
$testLog = Join-Path $logs ("editmode-" + $Tag + "-" + $stamp + ".log")
$testXml = Join-Path $logs ("editmode-" + $Tag + "-" + $stamp + ".xml")

# ── 1. Скрипты: BOM и разбор ────────────────────────────────────────────────
Write-Step "1/3  Скрипты Tools\*.ps1: BOM и разбор"
$toolsDir = Join-Path $Project 'Tools'
if (-not (Test-Path -LiteralPath $toolsDir)) {
    Fail-Gate ("нет папки Tools — проверка BOM (дефект A19) не выполнена")
} else {
    $scripts = Get-ChildItem -LiteralPath $toolsDir -Filter *.ps1 -Recurse
    $noBom = @()
    $badParse = @()
    foreach ($script in $scripts) {
        $bytes = [System.IO.File]::ReadAllBytes($script.FullName)
        $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
        if (-not $hasBom) { $noBom += $script.Name }
        $errs = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$null, [ref]$errs)
        if ($errs -and $errs.Count -gt 0) { $badParse += ($script.Name + ": " + $errs[0].Message) }
    }
    Write-Host ("скриптов: " + $scripts.Count)
    foreach ($n in $noBom) { Fail-Gate ("без BOM: " + $n + " — Windows PowerShell 5.1 не разберёт кириллицу (A19/E-11)") }
    foreach ($n in $badParse) { Fail-Gate ("не разбирается: " + $n) }
    if ($noBom.Count -eq 0 -and $badParse.Count -eq 0) { Write-Host "все скрипты с BOM и разбираются" -ForegroundColor Green }
}

# ── 2. EditMode-тесты ───────────────────────────────────────────────────────
Write-Step "2/3  EditMode-тесты (batchmode)"
$unityExe = Join-Path $Project 'ProjectSettings\ProjectVersion.txt'
$version = "6000.5.6f1"
if (Test-Path -LiteralPath $unityExe) {
    $line = (Get-Content -LiteralPath $unityExe | Select-String -Pattern 'm_EditorVersion:\s*(\S+)')
    if ($line) { $version = $line.Matches.Groups[1].Value }
}
$unity = Join-Path (Join-Path 'C:\Program Files\Unity\Hub\Editor' $version) 'Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unity)) {
    Fail-Gate ("нет Unity " + $version + " по пути " + $unity)
} else {
    Write-Host ("Unity: " + $unity)
    Write-Host ("лог:   " + $testLog)
    Remove-Item -LiteralPath $testXml -ErrorAction SilentlyContinue
    $args = @(
        '-batchmode',
        '-projectPath', ('"' + $Project + '"'),
        '-runTests', '-testPlatform', 'EditMode',
        '-testResults', ('"' + $testXml + '"'),
        '-logFile', ('"' + $testLog + '"')
    )
    $process = Start-Process -FilePath $unity -ArgumentList $args -PassThru
    $null = $process.WaitForExit($TestsTimeoutMinutes * 60 * 1000)
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Fail-Gate ("таймаут тестов " + $TestsTimeoutMinutes + " мин — процесс снят")
    }

    if (Test-Path -LiteralPath $testXml) {
        $xml = [xml](Get-Content -LiteralPath $testXml -Raw)
        $run = $xml.'test-run'
        Write-Host ("тесты: всего=" + $run.total + " прошло=" + $run.passed +
                    " упало=" + $run.failed + " пропущено=" + $run.skipped) -ForegroundColor Cyan
        if ([int]$run.failed -gt 0) {
            foreach ($case in $xml.SelectNodes('//test-case[@result="Failed"]')) {
                Fail-Gate ("упал тест: " + $case.fullname)
            }
        }
        if ([int]$run.skipped -gt 0) {
            # С 30.10.2026 самовыключающихся тестов быть не должно. Если пропуск всё же есть —
            # это не «норма», и вердикт не пройден, пока пропуск не объяснён в отчёте.
            foreach ($case in $xml.SelectNodes('//test-case[@result="Skipped"]')) {
                Fail-Gate ("ПРОПУЩЕН тест: " + $case.fullname + " — самовыключающийся тест не проверяет ничего")
            }
        }
    } else {
        Fail-Gate "XML-отчёт тестов не создан — прогон не дошёл до отчёта"
    }
}

# ── 3. Гейт по логу тестов ──────────────────────────────────────────────────
Write-Step "3/3  Гейт «чистой консоли» по логу тестов"
if (Test-Path -LiteralPath $testLog) {
    $gate = Join-Path $Project 'Tools\ci-console-check.ps1'
    if (Test-Path -LiteralPath $gate) {
        & powershell -ExecutionPolicy Bypass -File $gate -AnalyzeOnly -LogFile $testLog -LogName (Split-Path $testLog -Leaf)
        $gateExit = $LASTEXITCODE
        if ($gateExit -ne 0) { Fail-Gate ("гейт консоли: код " + $gateExit) }
    } else {
        Fail-Gate ("нет скрипта гейта: " + $gate)
    }
} else {
    Fail-Gate "лога тестов нет — гейт не выполнен"
}

if (-not $KeepLogs) {
    Write-Host ""
    Write-Host ("логи остались в " + $logs)
}

Write-Host ""
if ($script:failed) {
    Write-Host "ИТОГ: ЕСТЬ ПРОБЛЕМЫ (см. красные строки выше)" -ForegroundColor Red
    exit 1
}
Write-Host "ИТОГ: ВСЁ ПРОШЛО" -ForegroundColor Green
exit 0
