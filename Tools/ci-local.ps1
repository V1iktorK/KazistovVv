<#
.SYNOPSIS
    ЛОКАЛЬНЫЙ ПРОГОН ТЕХ ЖЕ ПРОВЕРОК, ЧТО И В CI — одной командой.

.DESCRIPTION
    ЗАЧЕМ. Правило проекта: проверка, которую нельзя повторить локально, не проверка, а лотерея.
    Если CI красный, а локально «не воспроизводится», спорят не о коде, а о среде. Этот скрипт
    запускает РОВНО ТОТ ЖЕ набор шагов, что и `.github/workflows/unity-tests.yml`, в том же
    порядке, и печатает один итог.

    ШАГИ (каждый можно пропустить ключом):
      1. Сторожа репозитория (Tools/repo-guards.ps1) — без Unity, секунды.
      2. Разбор всех Tools/*.ps1 (BOM + синтаксис) — без Unity, секунды.
      3. Гейт «чистой консоли» (Tools/ci-console-check.ps1) — запускает Unity в batchmode.
      4. EditMode-тесты — запускает Unity в batchmode (~10–25 минут на холодной Library).
      5. Е2Е панели «Настройки» и переключателя VR/MR (KvE2eSettingsDiag) — поднимает сцену.

.PARAMETER ProjectPath
    Корень Unity-проекта. По умолчанию — родитель каталога со скриптом.

.PARAMETER UnityExe
    Полный путь к Unity.exe. По умолчанию — версия из ProjectSettings\ProjectVersion.txt.

.PARAMETER SkipGate
    Не запускать гейт «чистой консоли».

.PARAMETER SkipTests
    Не запускать EditMode-тесты.

.PARAMETER SkipE2e
    Не запускать Е2Е (требует графики; в -nographics может повиснуть).

.PARAMETER Build
    Дополнительно СОБРАТЬ APK — доказательство, что проект не только проходит тесты, но и
    собирается. Долго (30–90 минут) и требует Android SDK, поэтому по умолчанию выключено.
    В CI это отдельная задача (`android-build`, переменная RUN_ANDROID_BUILD).

.PARAMETER TestsTimeoutMinutes
    Жёсткий таймаут тестов. По умолчанию 60.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\ci-local.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\ci-local.ps1 -SkipE2e -SkipGate

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\ci-local.ps1 -SkipGate -SkipTests -SkipE2e -Build
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = "",
    [string]$UnityExe = "",
    [switch]$SkipGate,
    [switch]$SkipTests,
    [switch]$SkipE2e,
    [switch]$Build,
    [int]$TestsTimeoutMinutes = 60
)

$ErrorActionPreference = 'Continue'

if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionFile)) { throw "нет $versionFile — это не Unity-проект" }
    $m = Select-String -LiteralPath $versionFile -Pattern 'm_EditorVersion:\s*(\S+)'
    $version = $m.Matches[0].Groups[1].Value
    $UnityExe = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityExe)) {
    throw "Unity.exe не найден: $UnityExe. Укажите путь ключом -UnityExe."
}

$logs = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

$results = New-Object System.Collections.Generic.List[object]

# ----------------------------------------------------------------------------------- запуск Unity
#
# ЗАЧЕМ СВОЯ ФУНКЦИЯ (исправлено 12.11.2026). Здесь стояло
#     $p = Start-Process -FilePath $UnityExe -ArgumentList $argList -PassThru -NoNewWindow
#     ... $p.ExitCode
# и `$p.ExitCode` ВСЕГДА приходил ПУСТЫМ: `Start-Process` на Windows PowerShell запускает процесс
# через оболочку (UseShellExecute), поэтому у объекта нет дескриптора и код возврата недоступен.
# Последствие — не косметика: шаг Е2Е объявлял ПРОВАЛ ПРИ ПОЛНОСТЬЮ ЗЕЛЁНОМ ПРОГОНЕ. В журнале
# Е2Е стояло «ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ (30 из 30)», а сводка прогона печатала
# «5. Е2Е … ПРОВАЛ (код 1)». Проверка, которая краснеет на исправном коде, обесценивает все
# остальные проверки: её перестают читать.
# ЧТО СТАЛО. Процесс запускается напрямую (`UseShellExecute = $false`), таймаут остаётся, код
# возврата читается у живого дескриптора.
function Invoke-UnityProcess {
    param(
        [string[]]$ArgumentList,
        [int]$TimeoutMinutes = 20
    )
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $UnityExe
    $psi.Arguments = ($ArgumentList -join ' ')
    $psi.UseShellExecute = $false
    $psi.WorkingDirectory = $ProjectPath
    $proc = [System.Diagnostics.Process]::Start($psi)
    if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        Write-Host "ТАЙМАУТ $TimeoutMinutes мин — процесс снимается"
        try { $proc.Kill() } catch { }
        return 99
    }
    return $proc.ExitCode
}

function Step([string]$name, [scriptblock]$action) {
    Write-Host ""
    Write-Host ("=" * 78)
    Write-Host "ШАГ: $name"
    Write-Host ("=" * 78)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $code = 1
    try { & $action; $code = $LASTEXITCODE; if ($null -eq $code) { $code = 0 } }
    catch { Write-Host "ИСКЛЮЧЕНИЕ: $($_.Exception.Message)"; $code = 1 }
    $sw.Stop()
    $results.Add([pscustomobject]@{
        Шаг = $name
        Код = $code
        Секунд = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    })
    Write-Host ""
    Write-Host ("итог шага «{0}»: {1} ({2:N1} с)" -f $name, $(if ($code -eq 0) { 'ПРОШЁЛ' } else { "ПРОВАЛ (код $code)" }), $sw.Elapsed.TotalSeconds)
}

# -------------------------------------------------------------------------------- 1. сторожа
Step "1. Сторожа репозитория" {
    & pwsh -NoProfile -File (Join-Path $ProjectPath 'Tools\repo-guards.ps1') -ProjectPath $ProjectPath
}

# -------------------------------------------------------------------------------- 2. скрипты
Step "2. Разбор Tools/*.ps1 (BOM и синтаксис)" {
    $fail = $false
    $files = @(Get-ChildItem (Join-Path $ProjectPath 'Tools\*.ps1') -ErrorAction SilentlyContinue)
    if ($files.Count -eq 0) { Write-Host "скриптов нет"; return }
    foreach ($f in $files) {
        $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
        $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
        if (-not $hasBom) {
            Write-Host ("  [ПРОВАЛ] {0}: нет BOM (PowerShell 5.1 прочитает кириллицу как ANSI)" -f $f.Name)
            $fail = $true
        }
        $errors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$null, [ref]$errors)
        if ($errors -and $errors.Count -gt 0) {
            Write-Host ("  [ПРОВАЛ] {0}: {1} (строка {2})" -f $f.Name, $errors[0].Message, $errors[0].Extent.StartLineNumber)
            $fail = $true
        }
    }
    # ВНИМАНИЕ: внутри блока шага НЕЛЬЗЯ вызывать `exit` — это выход из ВСЕГО скрипта
    # (дефект найден 11.11.2026: шаг 2 завершался `exit 0`, и шаги 4–5 «тесты/Е2Е» вообще не
    # выполнялись, а прогон рапортовал успех). Результат шага передаётся через $LASTEXITCODE.
    if ($fail) { Write-Host "есть провалы"; $global:LASTEXITCODE = 1; return }
    Write-Host ("  все {0} скриптов с BOM и разбираются" -f $files.Count)
    $global:LASTEXITCODE = 0
}

# -------------------------------------------------------------------------------- 3. гейт консоли
if (-not $SkipGate) {
    Step "3. Гейт «чистой консоли» (Unity batchmode)" {
        & powershell -ExecutionPolicy Bypass -File (Join-Path $ProjectPath 'Tools\ci-console-check.ps1') `
            -ProjectPath $ProjectPath -LogName 'ci-local.log' -TimeoutMinutes 30 -UnityExe $UnityExe
    }
} else { Write-Host "шаг 3 пропущен ключом -SkipGate" }

# -------------------------------------------------------------------------------- 4. EditMode
if (-not $SkipTests) {
    Step "4. EditMode-тесты" {
        $xml = Join-Path $logs 'ci-local-editmode.xml'
        $log = Join-Path $logs 'ci-local-editmode.log'
        if (Test-Path -LiteralPath $xml) { Remove-Item -LiteralPath $xml -Force }
        # Путь проекта содержит пробелы и кириллицу, поэтому каждый путь — в кавычках:
        # иначе Start-Process рвёт аргумент по пробелу (проверено 02.10.2026).
        $argList = @(
            '-batchmode', '-nographics',
            '-projectPath', ('"' + $ProjectPath + '"'),
            '-runTests', '-testPlatform', 'EditMode',
            '-testResults', ('"' + $xml + '"'),
            '-logFile', ('"' + $log + '"')
        )
        $testCode = Invoke-UnityProcess -ArgumentList $argList -TimeoutMinutes $TestsTimeoutMinutes
        if ($testCode -eq 99) { $global:LASTEXITCODE = 99; return }
        if (-not (Test-Path -LiteralPath $xml)) {
            Write-Host "XML результатов НЕ создан — прогон не дошёл до отчёта"
            $global:LASTEXITCODE = 1; return
        }

        $run = ([xml](Get-Content -LiteralPath $xml -Raw)).'test-run'
        Write-Host ("тесты: всего={0} прошло={1} упало={2} пропущено={3} результат={4}" -f `
            $run.total, $run.passed, $run.failed, $run.skipped, $run.result)
        $failedCases = @(([xml](Get-Content -LiteralPath $xml -Raw)).SelectNodes("//test-case[@result='Failed']"))
        foreach ($f in $failedCases) { Write-Host ("  [УПАЛ] " + $f.fullname) }

        $skipped = [int]$run.skipped
        if ($skipped -gt 0) { Write-Host "::warning::есть пропущенные тесты ($skipped) — «пропущено» не значит «хорошо»" }
        if ([int]$run.failed -gt 0 -or $run.result -ne 'Passed') { $global:LASTEXITCODE = 1; return }
        $global:LASTEXITCODE = 0
    }
} else { Write-Host "шаг 4 пропущен ключом -SkipTests" }

# -------------------------------------------------------------------------------- 5. Е2Е
if (-not $SkipE2e) {
    Step "5. Е2Е: панель «Настройки» и переключатель VR/MR" {
        $log = Join-Path $logs 'ci-local-e2e.log'
        # БЕЗ -nographics: Е2Е поднимает настоящую сцену и строит настоящий uGUI.
        # KvE2eSettingsDiag сам завершает редактор: 0 — прошло, 2 — провалы.
        $argList = @(
            '-batchmode',
            '-projectPath', ('"' + $ProjectPath + '"'),
            '-executeMethod', 'KvE2eSettingsDiag.Run',
            '-logFile', ('"' + $log + '"')
        )
        $e2eCode = Invoke-UnityProcess -ArgumentList $argList -TimeoutMinutes 20
        Write-Host "код возврата Е2Е: $e2eCode"
        if ($e2eCode -ne 0) { Write-Host "разбор — в $log"; $global:LASTEXITCODE = 1; return }
        $global:LASTEXITCODE = 0
    }
} else { Write-Host "шаг 5 пропущен ключом -SkipE2e" }

# -------------------------------------------------------------------------------- 6. сборка APK
#
# ЗАЧЕМ В ПРОВЕРКАХ. Тесты доказывают, что ЛОГИКА верна; они не доказывают, что проект
# СБИРАЕТСЯ. В этом проекте уже был случай, когда тесты были зелёными, а сборка падала
# (и скрипт сборки при этом находил старый APK и сообщал успех). Поэтому сборка — отдельный шаг
# с ЯВНЫМ признаком свежести результата.
if ($Build) {
    Step "6. Сборка APK (проверка собираемости)" {
        $builder = Join-Path $ProjectPath 'Tools\xr-build-apk-ascii.ps1'
        if (-not (Test-Path -LiteralPath $builder)) {
            Write-Host "нет $builder — шаг невозможен"
            $global:LASTEXITCODE = 1; return
        }
        & pwsh -NoProfile -File $builder
        $global:LASTEXITCODE = $LASTEXITCODE
    }
} else { Write-Host "шаг 6 пропущен (включить: -Build; долго и нужен Android SDK)" }

# -------------------------------------------------------------------------------- итог
Write-Host ""
Write-Host ("=" * 78)
Write-Host "ИТОГ ЛОКАЛЬНОГО ПРОГОНА"
Write-Host ("=" * 78)
$results | Format-Table -AutoSize | Out-String -Width 200 | Write-Host

$bad = @($results | Where-Object { $_.Код -ne 0 })
if ($bad.Count -gt 0) {
    Write-Host "ПРОВАЛЫ:"
    foreach ($b in $bad) { Write-Host ("  - {0} (код {1})" -f $b.Шаг, $b.Код) }
    exit 1
}
Write-Host "Все выполненные шаги пройдены."
exit 0
