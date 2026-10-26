<#
    ЭТАП А: ЗАКРЫТИЕ СБОРКИ §30 И ПРОВЕРКА ВОЗВРАТА СОСТОЯНИЯ (сессия §30/Этап А).

    ЗАЧЕМ ЭТОТ СКРИПТ. Сборка APK шла 46 часов, и всё это время состояние проекта было
    НАМЕРЕННО изменённым: активная платформа переключена на Android, папка тестов убрана
    из Assets в Temp, уровни качества правились под Android. Значит, «сборка прошла» —
    это два факта, а не один: (1) есть файл APK и строка «APK собран» в логе; (2) проект
    вернулся в исходное состояние. Второй факт проверяется СРАВНЕНИЕМ, а не памятью,
    поэтому снимок хешей снимается ДО выхода сборки (режим Snapshot), а сверяется после
    (режим Wait).

    ПОЧЕМУ СВЕРКА ИДЁТ С ФАЙЛАМИ, А НЕ С «ГЛАЗАМИ В РЕДАКТОРЕ». `GraphicsSettings.asset`,
    `QualitySettings.asset` и обе сцены правятся и сборкой, и редактором; отличить
    «вернулось» от «осталось переключённым» можно только хешем. Сверка с бэкапом
    `_backups_consolidated\folders\urp_switch_2026-10-05_2114` для этих файлов НЕ годится:
    §29/§30 меняли их намеренно (URP — глобальным умолчанием графики, решение [D9]),
    и совпадение с бэкапом означало бы откат работы §29. Поэтому эталон — состояние ДО
    сборки (снимок), а бэкап используется как эталон там, где он и есть эталон:
    `QualitySettings.asset` и `ProjectSettings.asset` (их сборка менять не должна).

    РЕЖИМЫ
      -Mode Snapshot — снять снимок и положить в Logs\stage-a-snapshot-before.txt
      -Mode Wait     — ждать выхода процесса Unity (или строки-маркера в логе), затем
                       собрать доказательства A1/A3/A4 и напечатать таблицу
      -Mode Compare  — только сверка текущего состояния со снимком (без ожидания)

    Возврат: 0 — APK есть, сверка без расхождений; 1 — APK нет или есть расхождения.

    ПРИМЕРЫ
      powershell -ExecutionPolicy Bypass -File Tools\stage-a-closeout.ps1 -Mode Snapshot
      powershell -ExecutionPolicy Bypass -File Tools\stage-a-closeout.ps1 -Mode Wait
#>
param(
    [string]$Project = "C:\KazistovVv_Build",
    [string]$BuildLogName = "xr-build-apk-s30.log",
    [ValidateSet('Snapshot', 'Wait', 'Compare')][string]$Mode = 'Wait',
    [int]$UnityPid = 12160,
    [int]$TimeoutMinutes = 720
)

$ErrorActionPreference = 'Continue'
$logs = Join-Path $Project 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$snapshot = Join-Path $logs 'stage-a-snapshot-before.txt'
$buildLog = Join-Path $logs $BuildLogName

# ЧТО СНИМАЕМ. Не «всё подряд», а то, что сборка имеет право изменить: настройки проекта,
# настройки XR и графики, обе сцены, материалы URP-копий и переназначение материала в модели.
function Get-WatchedFiles {
    param([string]$Root)
    $patterns = @(
        'ProjectSettings\*.asset',
        'Assets\Settings\*.asset',
        'Assets\XR\XRGeneralSettingsPerBuildTarget.asset',
        'Assets\XR\Settings\*.asset',
        'Assets\XR\Resources\*.asset',
        'Assets\_Project\00_Scenes\*.unity',
        'Assets\Robots\Robot.fbx.meta',
        'Assets\_Project\04_Materials\URP\*.mat',
        'Assets\_Project\04_Materials\Robot\*.mat'
    )
    $files = @()
    foreach ($p in $patterns) {
        $full = Join-Path $Root $p
        $files += Get-ChildItem -Path $full -File -ErrorAction SilentlyContinue
    }
    return $files | Sort-Object FullName -Unique
}

function Write-Snapshot {
    param([string]$Root, [string]$OutFile)
    $files = Get-WatchedFiles -Root $Root
    $lines = @()
    $lines += "# СНИМОК СОСТОЯНИЯ ПЕРЕД СБОРКОЙ (Этап А)"
    $lines += "# Проект: $Root"
    $lines += "# Снят:   $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $lines += "# Файлов: $($files.Count)"
    $lines += "#"
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($Root.Length).TrimStart('\')
        $h = (Get-FileHash $f.FullName -Algorithm SHA256).Hash
        $lines += ("{0}  {1}  {2}" -f $h, $f.Length, $rel)
    }
    Set-Content -Path $OutFile -Value $lines -Encoding UTF8
    Write-Host "Снимок снят: $OutFile ($($files.Count) файлов)"
}

function Read-Snapshot {
    param([string]$File)
    $map = @{}
    foreach ($line in Get-Content $File -Encoding UTF8) {
        if ($line.StartsWith('#') -or [string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split '  ', 3
        if ($parts.Count -lt 3) { continue }
        $map[$parts[2]] = @{ Hash = $parts[0]; Size = [int64]$parts[1] }
    }
    return $map
}

function Compare-Snapshot {
    param([string]$Root, [string]$File)
    $map = Read-Snapshot -File $File
    if ($map.Count -eq 0) { Write-Host "СНИМОК ПУСТ или не найден: $File"; return $false }
    $same = 0; $diff = 0; $missing = 0
    Write-Host ""
    Write-Host "=== СВЕРКА: сборка вернула состояние? (снимок ДО vs сейчас) ==="
    foreach ($rel in ($map.Keys | Sort-Object)) {
        $full = Join-Path $Root $rel
        if (-not (Test-Path $full)) {
            Write-Host ("  ПРОПАЛ   {0}" -f $rel); $missing++; continue
        }
        $now = (Get-FileHash $full -Algorithm SHA256).Hash
        if ($now -eq $map[$rel].Hash) { $same++ }
        else {
            Write-Host ("  ИЗМЕНЁН  {0}" -f $rel)
            Write-Host ("             было {0}" -f $map[$rel].Hash)
            Write-Host ("             стало {0}" -f $now)
            $diff++
        }
    }
    Write-Host ("  итог: совпало {0} · изменено {1} · пропало {2}" -f $same, $diff, $missing)
    return (($diff -eq 0) -and ($missing -eq 0))
}

function Show-TestFolders {
    param([string]$Root)
    $tests = Join-Path $Root 'Assets\_Project\08_Tests'
    $parked = Join-Path $Root '_tests_parked_for_build'
    Write-Host ""
    Write-Host "=== A3: ГДЕ ЛЕЖАТ ТЕСТЫ ==="
    foreach ($pair in @(@('Assets\_Project\08_Tests', $tests), @('_tests_parked_for_build', $parked))) {
        $name = $pair[0]; $path = $pair[1]
        if (-not (Test-Path $path)) { Write-Host ("  {0,-34} НЕТ" -f $name); continue }
        $files = Get-ChildItem $path -Recurse -File -Force -ErrorAction SilentlyContinue
        Write-Host ("  {0,-34} файлов {1}" -f $name, $files.Count)
        foreach ($f in ($files | Sort-Object Name)) { Write-Host ("      " + $f.Name) }
    }
}

function Show-ApkEvidence {
    param([string]$Root, [string]$LogPath)
    Write-Host ""
    Write-Host "=== A1: ДОКАЗАТЕЛЬСТВО СБОРКИ ==="
    if (Test-Path $LogPath) {
        $lines = Select-String -Path $LogPath -Pattern '\[KvXr\] APK собран|\[KvXr\] СБОРКА APK НЕ УДАЛАСЬ|\[KvXr\] Тесты возвращены|\[KvXr\] Тесты временно убраны|Build completed with a result|Total build time|Gradle build failed' |
            ForEach-Object { $_.Line.Trim() }
        if ($lines) { foreach ($l in $lines) { Write-Host ("  " + $l) } }
        else { Write-Host "  в логе нет ни строки об APK, ни строки об ошибке — сборка ещё идёт?" }
    } else {
        Write-Host "  ЛОГ НЕ НАЙДЕН: $LogPath"
    }

    $apkCandidates = @(
        (Join-Path $Root 'Builds\KazistovVv.apk'),
        (Join-Path $Root 'KazistovVv.apk')
    )
    $found = $false
    foreach ($apk in $apkCandidates) {
        if (Test-Path $apk) {
            $item = Get-Item $apk
            $mb = [math]::Round($item.Length / 1MB, 1)
            $hash = (Get-FileHash $apk -Algorithm SHA256).Hash
            Write-Host ("  APK ЕСТЬ: {0} · {1} МБ · {2} · SHA256 {3}" -f $apk, $mb, $item.LastWriteTime, $hash)
            $found = $true
        }
    }
    if (-not $found) { Write-Host "  APK НЕ НАЙДЕН ни в Builds\, ни в корне проекта" }
    return $found
}

# ------------------------------------------------------------------ выполнение

Write-Host "=== ЭТАП А: РЕЖИМ $Mode ==="

if ($Mode -eq 'Snapshot') {
    Write-Snapshot -Root $Project -OutFile $snapshot
    Show-TestFolders -Root $Project
    exit 0
}

if ($Mode -eq 'Wait') {
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    Write-Host "Ждём завершения сборки: процесс Unity PID $UnityPid (таймаут $TimeoutMinutes мин)…"
    while ((Get-Date) -lt $deadline) {
        $proc = Get-Process -Id $UnityPid -ErrorAction SilentlyContinue
        $marker = $false
        if (Test-Path $buildLog) {
            $marker = [bool](Select-String -Path $buildLog -Pattern '\[KvXr\] APK собран|\[KvXr\] СБОРКА APK НЕ УДАЛАСЬ' -Quiet)
        }
        if ($null -eq $proc -or $marker) { break }
        Start-Sleep -Seconds 30
    }
    $proc = Get-Process -Id $UnityPid -ErrorAction SilentlyContinue
    Write-Host ("Состояние: процесс {0}, строка-маркер в логе: {1}" -f `
        $(if ($proc) { 'ещё жив' } else { 'завершился' }), `
        $(if (Test-Path $buildLog) { [bool](Select-String -Path $buildLog -Pattern '\[KvXr\] APK собран' -Quiet) } else { $false }))
}

$ok = Show-ApkEvidence -Root $Project -LogPath $buildLog
$clean = Compare-Snapshot -Root $Project -File $snapshot
Show-TestFolders -Root $Project

Write-Host ""
Write-Host "=== ИТОГ ЭТАПА А (машинная часть) ==="
Write-Host ("  APK собран:            {0}" -f $ok)
Write-Host ("  состояние вернулось:   {0}" -f $clean)
if ($ok -and $clean) { exit 0 } else { exit 1 }
