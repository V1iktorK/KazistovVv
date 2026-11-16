# ТЕСТ СТЫКА E2E_PC: Tools/acceptance-gate.ps1 (шаг E2E_PC) ↔ KvE2eSettingsDiag.Run (отчёт в FeatureStorage).
#
# ЗАЧЕМ. 16.11.2026 прогон гейта со шлемом дал красный E2E_PC «код 0 · проверок 0»: диагностика
# в тот прогон не отработала (отчёт, который гейт удаляет перед запуском, так и не появился), а
# сам гейт не умел сказать ПОЧЕМУ — `Invoke-Unity` возвращал фиктивный 0, и «код 0 · проверок 0»
# выглядел как «диаг прошёл, но проверок нет». Стык вынесен в Tools/e2e-pc-stitch.ps1, чтобы его
# можно было проверить БЕЗ запуска полного гейта и БЕЗ Unity.
#
# ЗАПУСК:  pwsh -NoProfile -File Tools/tests/e2e-pc-stitch.Tests.ps1
# КОДЫ:    0 — все проверки стыка зелёные; 1 — есть провал (выводится список).
#
# Тест НЕ запускает Unity и НЕ трогает сцену/сборки: он работает с УЖЕ ЛЕЖАЩИМ отчётом диага и
# с логами прошлых прогонов (Logs/).
[CmdletBinding()]
param()

# КОДИРОВКА КОНСОЛИ — UTF8 (как в Tools/acceptance-gate.ps1): иначе кириллица вердикта в логе
# превращается в мусор, и причину поломки прочитать нельзя.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ErrorActionPreference = 'Stop'

$testsDir = $PSScriptRoot
$toolsDir = Split-Path -Parent $testsDir
$projectDir = Split-Path -Parent $toolsDir

$script:fail = New-Object System.Collections.Generic.List[string]
$script:passed = 0
$script:e2eChecks = -1

function Check([bool]$cond, [string]$name, [string]$detail = '') {
    $suffix = if ($detail) { " — " + $detail } else { "" }
    if ($cond) {
        $script:passed++
        Write-Host ("  [OK]   " + $name + $suffix)
    } else {
        $script:fail.Add($name + $suffix)
        Write-Host ("  [FAIL] " + $name + $suffix)
    }
}

function Step([string]$title) {
    Write-Host ""
    Write-Host ("--- " + $title + " ---")
}

# ================================================================ 1. модуль стыка подключается
Step "модуль стыка"
$helper = Join-Path $toolsDir 'e2e-pc-stitch.ps1'
$helperOk = Test-Path -LiteralPath $helper
Check $helperOk 'файл Tools/e2e-pc-stitch.ps1 существует' $helper
if ($helperOk) { . $helper }

# ================================================================ 2. ПУТЬ ОТЧЁТА = ПУТЬ FEATURESTORAGE
#
# Гейт обязан строить путь ТАК ЖЕ, как он строится в коде диага:
#   KvE2eSettingsDiag.ReportPath → KazistovVvFeatures.FeatureStorage.ReportPath(name)
#   FeatureStorage.Root       = <persistentDataPath>/KazistovVv
#   FeatureStorage.ReportsDir = <Root>/Reports
#   persistentDataPath        = <профиль>/AppData/LocalLow/<companyName>/<productName>
Step "путь отчёта"
$repPath = $null
if ($helperOk) {
    try {
        $repPath = Get-E2eReportPath -ProjectPath $projectDir
        Check (Test-Path -LiteralPath $repPath) 'путь из правила FeatureStorage указывает на СУЩЕСТВУЮЩИЙ отчёт диага' $repPath
    } catch {
        Check $false 'путь из правила FeatureStorage указывает на СУЩЕСТВУЮЩИЙ отчёт диага' ($_.Exception.Message)
    }
}

# Сверка с тем, что сама диагностика написала в отчёте строкой «отчёт: …»:
# это независимая (со стороны C#-кода) подсказка того же самого пути.
if ($repPath -and (Test-Path -LiteralPath $repPath)) {
    try {
        $text = [System.IO.File]::ReadAllText($repPath)
        $m = [regex]::Match($text, '(?m)^отчёт:\s*(.+)$')
        Check $m.Success 'в отчёте есть строка «отчёт: …» (ею диаг называет свой путь)'
        if ($m.Success) {
            $declared = [System.IO.Path]::GetFullPath($m.Groups[1].Value.Trim().TrimEnd("`r"))
            $built = [System.IO.Path]::GetFullPath($repPath)
            Check ($declared -ieq $built) 'путь гейта и путь из отчёта диага — один и тот же файл' ("гейт: " + $built + " · диаг: " + $declared)
        }
    } catch {
        Check $false 'сверка пути гейта с путём из отчёта диага' ($_.Exception.Message)
    }
}

# ================================================================ 3. ПАРСЕР ГЕЙТА НА СУЩЕСТВУЮЩЕМ ОТЧЁТЕ
#
# Ровно та же функция, которую вызывает шаг E2E_PC гейта: «проверок > 0» здесь и есть доказательство,
# что стык «гейт читает отчёт там, где его пишет FeatureStorage» цел.
Step "парсер шага гейта на существующем отчёте"
if ($helperOk) {
    try {
        $goodLog = Join-Path $projectDir 'Logs\gate-e2e-pc.log'
        $v = Get-E2eVerdict -ReportPath $repPath -LogPath $goodLog -Code 0
        $script:e2eChecks = $v.Total
        Check $v.Ok 'шаг гейта E2E_PC на этом отчёте зелёный' $v.Detail
        Check ($v.Total -ge 29) 'проверок больше нуля (порог гейта — 29)' ("проверок " + $v.Total)
        Check ($v.Detail -notmatch 'ПРИЧИНА') 'на живом отчёте причина поломки не выдумывается' $v.Detail
    } catch {
        Check $false 'шаг гейта E2E_PC на этом отчёте зелёный' ($_.Exception.Message)
    }
}

# ================================================================ 4. «ПРОВЕРОК 0» ТЕПЕРЬ НАЗЫВАЕТ ПРИЧИНУ
#
# Ровно тот сценарий 16.11.2026: отчёта нет (гейт удалил его перед запуском, диаг не отработал),
# Unity завершилась с ненулевым кодом. Лог Logs/ci-local-editmode.log — НАСТОЯЩИЙ артефакт
# отказавшего экземпляра Unity (16.11.2026 20:24): «Exiting without the bug reporter … return code 1».
Step "разбор отказа запуска"
if ($helperOk) {
    $missingReport = Join-Path $env:TEMP 'kvv-e2e-report-absent.txt'
    if (Test-Path -LiteralPath $missingReport) { Remove-Item -LiteralPath $missingReport -Force }
    $refusedLog = Join-Path $projectDir 'Logs\ci-local-editmode.log'
    try {
        $v2 = Get-E2eVerdict -ReportPath $missingReport -LogPath $refusedLog -Code 1
        Check (-not $v2.Ok) 'отсутствие отчёта не проходит как зелёный' $v2.Detail
        Check ($v2.Detail -match 'ПРИЧИНА:') 'в вердикте названа причина, а не только «проверок 0»' $v2.Detail
        Check ($v2.Detail -match 'код 1') 'в вердикте настоящий код Unity, а не фиктивный 0' $v2.Detail
        Check ($v2.Detail -match 'KvE2eSettingsDiag') 'причина объясняет, что метод диага не выполнился' $v2.Detail
    } catch {
        Check $false 'разбор отказа запуска (отчёт отсутствует)' ($_.Exception.Message)
    }

    # Ложные срабатывания недопустимы: на УСПЕВШЕМ логе причина должна быть пустой.
    try {
        $goodLog2 = Join-Path $projectDir 'Logs\gate-e2e-pc.log'
        if (Test-Path -LiteralPath $goodLog2) {
            $reason = Get-E2eDiagReason -LogPath $goodLog2 -Code 0
            Check ([string]::IsNullOrEmpty($reason)) 'на успешном логе причина не выдумывается' $reason
        } else {
            Write-Host "  [SKIP] успешный лог Logs/gate-e2e-pc.log не найден"
        }
    } catch {
        Check $false 'отсутствие ложных срабатываний на успешном логе' ($_.Exception.Message)
    }
}

# ================================================================ 5. ГЕЙТ ДЕЙСТВИТЕЛЬНО ВЫЗЫВАЕТ ПРОВЕРЕННЫЙ КОД
#
# Иначе тест проверяет функции, которыми гейт не пользуется, — а это ровно тот дефект, который и
# привёл к «проверок 0»: проверка смотрит не туда, куда смотрит приёмка.
Step "подключение гейта"
if ($helperOk) {
    $gatePath = Join-Path $toolsDir 'acceptance-gate.ps1'
    try {
        $gate = [System.IO.File]::ReadAllText($gatePath)
        Check ($gate -match 'e2e-pc-stitch\.ps1') 'гейт подключает модуль стыка'
        Check ($gate -match 'Get-E2eReportPath') 'гейт берёт путь отчёта у правила FeatureStorage, а не литералом'
        Check ($gate -match 'Get-E2eVerdict') 'гейт считает вердикт E2E_PC через проверенную функцию'
    } catch {
        Check $false 'чтение Tools/acceptance-gate.ps1' ($_.Exception.Message)
    }
}

# ================================================================ итог
Write-Host ""
if ($script:e2eChecks -ge 0) {
    Write-Host ("шаг гейта на существующем отчёте: проверок " + $script:e2eChecks)
}
if ($script:fail.Count -eq 0) {
    Write-Host ("ИТОГ: СТЫК ЦЕЛ (" + $script:passed + " проверок стыка, провалов 0)")
    exit 0
}
Write-Host ("ИТОГ: ПРОВАЛЕНО " + $script:fail.Count + " из " + ($script:passed + $script:fail.Count))
foreach ($f in $script:fail) { Write-Host ("  - " + $f) }
exit 1
