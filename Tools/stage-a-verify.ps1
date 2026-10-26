<#
    ЭТАП А: ПРОВЕРКА ПОСЛЕ ПРАВОК — ОТЧЁТ О СОСТОЯНИИ, ГЕЙТ И ТЕСТЫ (сессия §30/Этап А).

    ЗАЧЕМ ОТДЕЛЬНЫЙ СКРИПТ. Три проверки идут СТРОГО ПО ОЧЕРЕДИ и в одной команде, потому что
    Unity не допускает второй инстанс на тот же проект («It looks like another Unity instance
    is running with this project open») — параллельный запуск уже один раз провалил гейт и
    оставил висящий процесс (записано в §28).

    ЧТО ИМЕННО ПРОВЕРЯЕТСЯ
      1) ОТЧЁТ О ВОЗВРАТЕ СОСТОЯНИЯ (`KvXrTools.ReportBuildReturnState`, пункт A4): активная
         платформа, умолчание графики, уровни качества и платформы, графический API Android,
         лоадеры XR, сцены сборки, где лежат тесты. Печатается в Logs\stage-a-return-state.log.
         Это единственный способ узнать правду: активная платформа живёт в Library, а не
         в текстовом файле проекта, и глазами её не увидеть.
      2) ГЕЙТ «ЧИСТОЙ КОНСОЛИ» + EditMode-ТЕСТЫ (`Tools\xr-checks.ps1`) — штатным сценарием
         проекта, без своих копий логики.

    Возврат: 0 — проба, гейт и тесты прошли; 1 — есть провал (числа печатаются в любом случае).

    ПРИМЕР
      powershell -ExecutionPolicy Bypass -File Tools\stage-a-verify.ps1 -Tag stage-a-red
#>
param(
    [string]$Project = "C:\KazistovVv_Build",
    [string]$Tag = "stage-a",
    [int]$ProbeTimeoutMinutes = 20,
    [int]$GateTimeoutMinutes = 25,
    [int]$TestsTimeoutMinutes = 40,
    [switch]$SkipProbe
)

$ErrorActionPreference = 'Continue'
$unity = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe"
$logs = Join-Path $Project 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

function Wait-NoUnity {
    param([int]$Minutes = 2)
    $deadline = (Get-Date).AddMinutes($Minutes)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process -Name Unity -ErrorAction SilentlyContinue)) { return $true }
        Start-Sleep -Seconds 2
    }
    return $false
}

$probeExit = 0
if (-not $SkipProbe) {
    Write-Host "=== [1/2] ОТЧЁТ О СОСТОЯНИИ ПОСЛЕ СБОРКИ (A4) ==="
    $probeLog = Join-Path $logs 'stage-a-return-state.log'
    if (Test-Path $probeLog) { Remove-Item $probeLog -Force }
    $args = @(
        '-batchmode', '-nographics', '-quit',
        '-projectPath', ('"' + $Project + '"'),
        '-executeMethod', 'KazistovVvXrEditor.KvXrTools.ReportBuildReturnState',
        '-logFile', ('"' + $probeLog + '"')
    )
    $p = Start-Process -FilePath $unity -ArgumentList $args -PassThru -NoNewWindow
    $null = $p.WaitForExit($ProbeTimeoutMinutes * 60 * 1000)
    if (-not $p.HasExited) {
        Write-Host "ПРОБА: таймаут $ProbeTimeoutMinutes мин — процесс снимается"
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        $probeExit = 99
    } else {
        $probeExit = $p.ExitCode
    }
    if (Test-Path $probeLog) {
        $lines = Select-String -Path $probeLog -Pattern '^\[KvA\]' | ForEach-Object { $_.Line }
        if ($lines) { foreach ($l in $lines) { Write-Host $l } }
        else { Write-Host "проба: строк [KvA] в логе нет — метод не выполнился (смотри $probeLog)" }
    } else {
        Write-Host "проба: лог не создан: $probeLog"
    }
    Write-Host "проба: код возврата $probeExit"
    Write-Host ""
} else {
    Write-Host "=== [1/2] ОТЧЁТ О СОСТОЯНИИ — ПРОПУЩЕН (-SkipProbe) ==="
    Write-Host ""
}

Write-Host "=== [2/2] ГЕЙТ «ЧИСТОЙ КОНСОЛИ» + EditMode-ТЕСТЫ ==="
$null = Wait-NoUnity -Minutes 3
$checks = Join-Path $Project 'Tools\xr-checks.ps1'
powershell -ExecutionPolicy Bypass -File $checks -Project $Project -Tag $Tag `
    -GateTimeoutMinutes $GateTimeoutMinutes -TestsTimeoutMinutes $TestsTimeoutMinutes
$checksExit = $LASTEXITCODE

$results = Join-Path $logs "editmode-$Tag.xml"
if (Test-Path $results) {
    $xml = [xml](Get-Content $results -Raw)
    $run = $xml.'test-run'
    Write-Host ""
    Write-Host "=== ЧИСЛА ПРОГОНА $Tag ==="
    Write-Host ("  всего={0} прошло={1} упало={2} пропущено={3} результат={4}" -f `
        $run.total, $run.passed, $run.failed, $run.skipped, $run.result)

    $failed = $xml.SelectNodes("//test-case[@result='Failed']")
    if ($failed.Count -gt 0) {
        Write-Host "  УПАВШИЕ:"
        foreach ($c in $failed) { Write-Host ("    - " + $c.fullname) }
    }
    $skipped = $xml.SelectNodes("//test-case[@result='Skipped']")
    if ($skipped.Count -gt 0) {
        Write-Host "  ПРОПУЩЕННЫЕ (прежние пропуски):"
        foreach ($c in $skipped) { Write-Host ("    ~ " + $c.fullname) }
    }
}

Write-Host ""
Write-Host "=== ИТОГ ==="
Write-Host "  проба (A4):     $probeExit  (0 = отчёт напечатан)"
Write-Host "  гейт и тесты:   $checksExit  (0 = чисто)"

if (($probeExit -eq 0 -or $SkipProbe) -and $checksExit -eq 0) { exit 0 } else { exit 1 }
