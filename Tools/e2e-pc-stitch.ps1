# СТЫК ГЕЙТА С Е2Е-ДИАГНОСТИКОЙ: Tools/acceptance-gate.ps1 (шаг E2E_PC) ↔ KvE2eSettingsDiag.Run.
#
# ЗАЧЕМ ОН ВЫНЕСЕН В ОТДЕЛЬНЫЙ ФАЙЛ. 16.11.2026 прогон гейта со шлемом дал «E2E_PC: код 0 ·
# проверок 0 · [FAIL] 0» — приёмка не могла ни НАЙТИ причину, ни отличить «диаг не запустился» от
# «диаг прошёл и не нашёл проблем». Логика стыка должна проверяться сама по себе, без полного
# прогона гейта и без Unity: pwsh -NoProfile -File Tools/tests/e2e-pc-stitch.Tests.ps1
#
# ТРИ ОШИБКИ, КОТОРЫЕ ЗДЕСЬ ЗАКРЫТЫ (все — из прогона 16.11.2026):
#   1. ПУТЬ. Гейт держал путь отчёта литералом `C:\Users\<юзер>\…\KazistovVv\Reports\…`. Правильный
#      путь — не «где-то в гейте», а то, что строит сама диаг: FeatureStorage.ReportPath
#      (Root = <persistentDataPath>/KazistovVv, отчёт = <Root>/Reports/<имя>). Здесь путь ВОСПРОИЗВОДИТСЯ
#      по тому же правилу из ProjectSettings (companyName/productName), поэтому переезд отчёта или
#      переименование продукта ломают проверку сразу и голосово, а не «проверок 0».
#   2. ПРИЧИНА. Если отчёта нет, раньше печаталось только «проверок 0» — ноль информации. Теперь
#      лог прогона разбирается, и в вердикт попадает причина («скрипты не собрались», «проект занят
#      другим экземпляром Unity», «Unity завершилась с кодом N» и т.д.).
#   3. КОД. `Invoke-Unity` возвращал 0 всякий раз, когда процесс вообще завершился (не 124-таймаут),
#      и настоящий код Unity терялся. Теперь настоящий код пробрасывается в вердикт E2E_PC.
#
# НИЧЕГО НЕ ОБХОДИТ: порог «не меньше 29 проверок» и «ни одного [FAIL]» остались прежними.
# Повторная попытка (см. Get-E2eVerdict/-Retry) выполняется РОВНО один раз и только когда отчёт
# вообще не появился, — она печатает причину в консоль гейта, а не прячет её.

# ---------------------------------------------------------------- путь отчёта (правило FeatureStorage)
<#
.SYNOPSIS
    Путь отчёта Е2Е-диагностики — точно там, где его пишет KvE2eSettingsDiag.Run.

.DESCRIPTION
    Диаг вызывает `KazistovVvFeatures.FeatureStorage.ReportPath("_e2e_settings.txt")`:
      Root       = <Application.persistentDataPath>/KazistovVv   (FeatureStorage.cs)
      ReportsDir = <Root>/Reports                                (FeatureStorage.cs, ФИКС 10)
      persistentDataPath (Windows) = <профиль>/AppData/LocalLow/<companyName>/<productName>
    companyName/productName берутся из ProjectSettings/ProjectSettings.asset, а не дублируются
    литералом: иначе переименование продукта снова выродится в «проверок 0».
#>
function Get-E2eReportPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [string]$UserName = '',
        [string]$DiagFileName = '_e2e_settings.txt',
        [string]$StorageFolder = 'KazistovVv',   # FeatureStorage.Root (имя подпапки в persistentDataPath)
        [string]$ReportsFolder = 'Reports'       # FeatureStorage.ReportsDir
    )

    $company = ''
    $product = ''
    $psFile = Join-Path $ProjectPath 'ProjectSettings\ProjectSettings.asset'
    if (Test-Path -LiteralPath $psFile) {
        $psText = [System.IO.File]::ReadAllText($psFile, [System.Text.Encoding]::UTF8)
        $mc = [regex]::Match($psText, '(?m)^\s*companyName:\s*(.+?)\s*$')
        $mp = [regex]::Match($psText, '(?m)^\s*productName:\s*(.+?)\s*$')
        if ($mc.Success) { $company = $mc.Groups[1].Value.Trim() }
        if ($mp.Success) { $product = $mp.Groups[1].Value.Trim() }
    }
    if ([string]::IsNullOrEmpty($company)) { $company = 'KazistovVv' }
    if ([string]::IsNullOrEmpty($product)) { $product = 'KazistovVv' }

    # AppData\LocalLow — постоянные данные приложения (это и есть persistentDataPath редактора).
    $localLow = ''
    if (-not [string]::IsNullOrEmpty($env:USERPROFILE)) { $localLow = Join-Path $env:USERPROFILE 'AppData\LocalLow' }
    if ([string]::IsNullOrEmpty($localLow) -or -not (Test-Path -LiteralPath $localLow)) {
        $appData = [Environment]::GetFolderPath('LocalApplicationData')   # …\AppData\Local
        $localLow = Join-Path (Split-Path -Parent $appData) 'LocalLow'
    }
    if (-not [string]::IsNullOrEmpty($UserName)) { $localLow = 'C:\Users\' + $UserName + '\AppData\LocalLow' }

    $dir = Join-Path $localLow $company
    $dir = Join-Path $dir $product
    $dir = Join-Path $dir $StorageFolder
    $dir = Join-Path $dir $ReportsFolder
    return (Join-Path $dir $DiagFileName)
}

# ---------------------------------------------------------------- счётчик проверок в отчёте
<#
.SYNOPSIS
    Считает [OK]/[FAIL] в отчёте диага — ровно так же, как это делал шаг E2E_PC гейта.
#>
function Get-E2eReportStats {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    $stats = [pscustomobject]@{ Path = $Path; Exists = $false; Oks = 0; Fails = 0; Total = 0; Text = '' }
    if ([string]::IsNullOrEmpty($Path) -or -not (Test-Path -LiteralPath $Path)) { return $stats }

    # Отчёт пишется UTF-8 без BOM (KvE2eSettingsDiag.Write) — читаем явно UTF-8, а не «как Бог даст».
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    $stats.Exists = $true
    $stats.Text = $text
    $stats.Oks = ([regex]::Matches($text, '\[OK\]')).Count
    $stats.Fails = ([regex]::Matches($text, '\[FAIL\]')).Count
    $stats.Total = $stats.Oks + $stats.Fails
    return $stats
}

# ---------------------------------------------------------------- почему отчёта нет
<#
.SYNOPSIS
    По логу прогона называет причину, по которой отчёт диага не появился.

.DESCRIPTION
    Пустой string — значит, в логе видно, что диаг ОТРАБОТАЛ ([E2E] ИТОГ в конце), т.е. лог ничего
    не объясняет. Непустая строка — готовое объяснение для вердикта гейта: диаг НЕ выполнялся, и
    вот на каком этапе Unity отвалилась. Ничего не выдумывается: незнакомый лог получает честное
    «причина не распознана, смотреть <лог>» с последними строками лога.
#>
function Get-E2eDiagReason {
    [CmdletBinding()]
    param(
        [string]$LogPath,
        [int]$Code = 0
    )

    if ($Code -eq 124) { return 'таймаут: Unity не завершилась за отведённое время — прогон убит до того, как KvE2eSettingsDiag.Run отработал' }

    if ([string]::IsNullOrEmpty($LogPath) -or -not (Test-Path -LiteralPath $LogPath)) {
        return "лог прогона не найден — запуск Unity не состоялся (ожидался лог: $LogPath)"
    }

    $text = [System.IO.File]::ReadAllText($LogPath, [System.Text.Encoding]::UTF8)

    # Диаг дошёл до конца — отчёт должен был быть, и лог причины не содержит.
    if ($text -match '\[E2E\] ИТОГ') { return '' }
    if ($text -match 'сцена MainScene открыта, вход в PlayMode') { return '' }   # Run() пошёл, но не закончил — причина в другом

    if ($text -match 'Scripts have compiler errors') {
        return 'Unity не собрала скрипты (Scripts have compiler errors) — -executeMethod KvE2eSettingsDiag.Run не вызван'
    }
    if ($text -match '(?i)another unity instance|already running with this project|is already open in another') {
        return 'проект занят другим экземпляром Unity — KvE2eSettingsDiag.Run не вызван'
    }
    if ($text -match '(?i)executeMethod[^.\r\n]{0,120}(not found|not a method|не найден)|Класс[^.\r\n]{0,120}не найден') {
        return 'метод KvE2eSettingsDiag.Run не найден Unity — точка входа диагностики недоступна в этой сборке скриптов'
    }
    if ($text -match 'Aborting batchmode due to failure:\s*\r?\n?([^\r\n]+)') {
        return ('Unity прервала batchmode: ' + $matches[1].Trim() + ' — KvE2eSettingsDiag.Run не выполнился')
    }
    if ($text -match 'Application will terminate with return code (\d+)') {
        $rc = $matches[1]
        if ($Code -eq 0 -or $Code -eq [int]$rc) {
            return "Unity завершилась с кодом $rc ДО вызова KvE2eSettingsDiag.Run (отчёт не создан) — лог: $LogPath"
        }
        return "Unity завершилась с кодом $rc — лог: $LogPath"
    }

    if ($Code -ne 0) { return "отчёт не создан, Unity вернула код $Code — причина в логе: $LogPath" }
    return "отчёт не создан, причина в логе не распознана — смотреть $LogPath"
}

# ---------------------------------------------------------------- вердикт шага E2E_PC
<#
.SYNOPSIS
    Вердикт шага E2E_PC: та самая формула «код · проверок · [FAIL]», только честная.

.OUTPUTS
    pscustomobject: Ok, Total, Fails, Code, Detail, Retry
    Retry = $true, когда ПОВТОР оправдан: отчёта нет, Unity не зависла (не 124) — т.е. диаг вообще
    не запустился, а не «запустился и нашёл дефекты».
#>
function Get-E2eVerdict {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ReportPath,
        [string]$LogPath = '',
        [int]$Code = 0,
        [int]$MinChecks = 29,
        [string]$Note = ''
    )

    $s = Get-E2eReportStats -Path $ReportPath
    $suffix = if ($Note) { ' · ' + $Note } else { '' }

    if ($s.Total -eq 0) {
        $reason = Get-E2eDiagReason -LogPath $LogPath -Code $Code
        if ([string]::IsNullOrEmpty($reason)) {
            $reason = 'диагностика отработала, но не записала ни одной проверки в отчёт'
        }
        return [pscustomobject]@{
            Ok    = $false
            Total = 0
            Fails = 0
            Code  = $Code
            Retry = ($Code -ne 124)
            Detail = ("код $Code · проверок 0 · [FAIL] 0 · ПРИЧИНА: " + $reason + $suffix)
        }
    }

    $ok = ($Code -eq 0 -and $s.Fails -eq 0 -and $s.Total -ge $MinChecks)
    $detail = if ($ok) { "код $Code · проверок $($s.Total) · [FAIL] 0" } else { "код $Code · проверок $($s.Total) · [FAIL] $($s.Fails)" }
    return [pscustomobject]@{
        Ok = $ok; Total = $s.Total; Fails = $s.Fails; Code = $Code; Retry = $false
        Detail = ($detail + $suffix)
    }
}
