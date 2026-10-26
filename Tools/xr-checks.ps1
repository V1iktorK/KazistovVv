<#
    ПРОГОН БАЗОВЫХ ПРОВЕРОК ПРОЕКТА (задача XR), ПОСЛЕДОВАТЕЛЬНО.

    Почему сценарий, а не два запуска: Unity НЕ ДОПУСКАЕТ два инстанса на один проект
    («It looks like another Unity instance is running with this project open») — параллельный
    запуск гейта и тестов уже один раз провалил гейт и оставил висящий процесс Unity.
    Здесь всё идёт строго по очереди: гейт → EditMode-тесты.

    Возвращает: 0 — обе проверки прошли; 1 — есть провал (числа печатаются в любом случае).

    ПУТЬ ПРОЕКТА ПО УМОЛЧАНИЮ (исправлено в §29): был `…\новое пространство\KazistovVv` —
    то есть СТАРАЯ папка, без `Library` и с не-ASCII символами в пути. Проверки уходили
    не в тот проект. Рабочий проект после §28 — `C:\KazistovVv_Build\`.
#>
param(
    [string]$Project = "C:\KazistovVv_Build",
    [string]$Tag = "xr-baseline",
    [int]$GateTimeoutMinutes = 25,
    [int]$TestsTimeoutMinutes = 30
)

$ErrorActionPreference = 'Continue'
$unity = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe"
$logs = Join-Path $Project 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

function Wait-NoUnity {
    $deadline = (Get-Date).AddMinutes(2)
    while ((Get-Date) -lt $deadline) {
        $p = Get-Process -Name Unity -ErrorAction SilentlyContinue
        if (-not $p) { return $true }
        Start-Sleep -Seconds 2
    }
    return $false
}

Write-Host "=== [1/2] БАТЧМОД-ГЕЙТ «ЧИСТОЙ КОНСОЛИ» ==="
Push-Location $Project
powershell -ExecutionPolicy Bypass -File (Join-Path $Project 'Tools\ci-console-check.ps1') `
    -LogName "ci-$Tag.log" -TimeoutMinutes $GateTimeoutMinutes
$gateExit = $LASTEXITCODE
Pop-Location
Write-Host "гейт: код возврата $gateExit"
Write-Host ""

Write-Host "=== [2/2] EditMode-ТЕСТЫ ==="
$null = Wait-NoUnity
$results = Join-Path $logs "editmode-$Tag.xml"
$testLog = Join-Path $logs "editmode-$Tag.log"
if (Test-Path $results) { Remove-Item $results -Force }

# ВАЖНО: путь проекта содержит ПРОБЕЛЫ («новое пространство»), поэтому каждый путь
# передаётся в кавычках — иначе Start-Process рвёт аргумент по пробелу и Unity получает
# «Couldn't set project path to: …/KazistovVv/C:/Users/…» (проверено 02.10.2026).
$argList = @(
    '-batchmode', '-nographics',
    '-projectPath', ('"' + $Project + '"'),
    '-runTests', '-testPlatform', 'EditMode',
    '-testResults', ('"' + $results + '"'),
    '-logFile', ('"' + $testLog + '"')
)
$p = Start-Process -FilePath $unity -ArgumentList $argList -PassThru -NoNewWindow
$null = $p.WaitForExit($TestsTimeoutMinutes * 60 * 1000)
if (-not $p.HasExited) {
    Write-Host "ТЕСТЫ: таймаут $TestsTimeoutMinutes мин — процесс снимается"
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    $testExit = 99
} else {
    $testExit = $p.ExitCode
}

if (Test-Path $results) {
    $xml = [xml](Get-Content $results -Raw)
    $run = $xml.'test-run'
    Write-Host ("тесты: всего={0} прошло={1} упало={2} пропущено={3} результат={4}" -f `
        $run.total, $run.passed, $run.failed, $run.skipped, $run.result)
} else {
    Write-Host "тесты: XML-файл результатов НЕ создан (прогон не дошёл до отчёта)"
}

Write-Host ""
Write-Host "=== ИТОГ ==="
Write-Host "гейт:  $gateExit  (0 = чисто)"
Write-Host "тесты: $testExit"

if ($gateExit -eq 0 -and $testExit -eq 0) { exit 0 } else { exit 1 }
