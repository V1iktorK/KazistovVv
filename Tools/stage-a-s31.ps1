<#
    §31: ПРИМЕНЕНИЕ НАСТРОЕК ДЛЯ ЖИВОГО УСТРОЙСТВА И ДОКАЗАТЕЛЬСТВА.

    ЗАЧЕМ. Решения владельца §31.2: идентификатор приложения — `com.KazistovVv.app` (был шаблонный
    `com.UnityTechnologies.com.unity.template.urpblank`), для Android включается простой профиль
    контроллеров `KHRSimpleControllerProfile`. Обе правки делаются ТОЛЬКО внутри Unity (ассеты и
    проектные настройки), поэтому запускаются батчмодом — и каждая печатает состояние ДО и ПОСЛЕ.

    ПОЧЕМУ ТРИ ЗАПУСКА, А НЕ ОДИН: `-executeMethod` выполняет РОВНО один метод за запуск.
    Так честнее и видно: каждый шаг печатает своё доказательство в свой лог.

    Возврат: 0 — все три шага отработали; 1 — иначе (причина в логе).

    ПРИМЕР
      powershell -ExecutionPolicy Bypass -File Tools\stage-a-s31.ps1
#>
param(
    [string]$Project = (Split-Path -Parent $PSScriptRoot),
    [string]$Unity = "",
    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Continue'

# ================================================================================================
# ПУТЬ К UNITY — ИЗ ПРОЕКТА, А НЕ ЖЁСТКИЙ (P4-12, 12.11.2026).
#
# ЧТО БЫЛО. Версия движка была ВПИСАНА в скрипт: "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\...".
# После обновления Unity скрипт искал несуществующий каталог и падал «не найден Unity», хотя проект
# уже собирался другой версией. Версия проекта и так записана в `ProjectSettings\ProjectVersion.txt`
# — второго источника быть не должно.
#
# ЧТО СТАЛО. Путь по умолчанию пустой и ВЫЧИСЛЯЕТСЯ из ProjectVersion.txt; явный `-Unity` главнее.
# ================================================================================================
if (-not $Unity) {
    $versionFile = Join-Path $Project 'ProjectSettings\ProjectVersion.txt'
    if (Test-Path $versionFile) {
        $found = Select-String -Path $versionFile -Pattern 'm_EditorVersion:\s*(\S+)'
        if ($found) {
            $Unity = Join-Path $env:ProgramFiles ("Unity\Hub\Editor\" + $found.Matches[0].Groups[1].Value + "\Editor\Unity.exe")
        }
    }
    if (-not $Unity -or -not (Test-Path $Unity)) {
        Write-Host "ОШИБКА: Unity.exe не найден; версия из ProjectVersion.txt: $(if (Test-Path $versionFile) { (Get-Content $versionFile -TotalCount 1) } else { 'файла нет' }). Задайте путь ключом -Unity."
        exit 1
    }
}
$logs = Join-Path $Project 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

$steps = @(
    @{ Method = 'KazistovVvXrEditor.KvXrDeviceTools.ApplyOwnIdentifier';    Log = 's31-identifier.log';  Prefix = '[KvDevice]' },
    @{ Method = 'KazistovVvXrEditor.KvXrDeviceTools.ApplyControllerProfile'; Log = 's31-profile.log';     Prefix = '[KvDevice]' },
    @{ Method = 'KazistovVvXrEditor.KvXrDeviceTools.ReportDeviceReadiness';  Log = 's31-readiness.log';   Prefix = '[KvDevice]' }
)

$exit = 0
foreach ($step in $steps) {
    $logPath = Join-Path $logs $step.Log
    if (Test-Path $logPath) { Remove-Item $logPath -Force }

    Write-Host ("=== " + $step.Method + " ===")
    $argList = @(
        '-batchmode', '-nographics', '-quit',
        '-projectPath', ('"' + $Project + '"'),
        '-executeMethod', $step.Method,
        '-logFile', ('"' + $logPath + '"')
    )
    $p = Start-Process -FilePath $Unity -ArgumentList $argList -PassThru -NoNewWindow
    $null = $p.WaitForExit($TimeoutMinutes * 60 * 1000)
    if (-not $p.HasExited) {
        Write-Host ("  таймаут $TimeoutMinutes мин — процесс снимается")
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        $exit = 1
        continue
    }

    if (Test-Path $logPath) {
        $lines = Select-String -Path $logPath -Pattern ([regex]::Escape($step.Prefix)) | ForEach-Object { $_.Line }
        if ($lines) { foreach ($l in $lines) { Write-Host ("  " + $l.Trim()) } }
        else {
            Write-Host ("  строк " + $step.Prefix + " в логе нет — метод не выполнился")
            $exit = 1
        }
        $errors = Select-String -Path $logPath -Pattern 'error CS' -Quiet
        if ($errors) { Write-Host "  В ЛОГЕ ОШИБКИ КОМПИЛЯЦИИ — смотри $logPath"; $exit = 1 }
    } else {
        Write-Host ("  лог не создан: " + $logPath)
        $exit = 1
    }
    Write-Host ""
}

Write-Host "=== ИТОГ §31 ==="
Write-Host ("  код возврата: " + $exit + " (0 = все три шага отработали)")
exit $exit
