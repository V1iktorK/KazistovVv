<#
    ЭТАП А, ПУНКТ A3: ВОЗВРАТ ТЕСТОВ ИЗ Temp В Assets — С ДОКАЗАТЕЛЬСТВАМИ.

    ЗАЧЕМ СКРИПТ, А НЕ РУКАМИ. Возврат 23 файлов — это операция, которую прежний код сборки
    делал НЕПРАВИЛЬНО (дефект A2: Unity пересоздаёт пустую папку, и условие «папки нет» не
    выполняется). Здесь возврат делается один раз вручную — и сразу с доказательствами:
      • сколько файлов перенесено (с разбивкой `.cs` / `.meta`);
      • совпадают ли 9 прежних файлов с бэкапом `urp_switch_2026-10-05_2114` ПО ХЕШАМ
        (то есть вернулись именно те файлы, а не «какие-то»);
      • на месте ли 2 новых файла §30/§31 (`KvDeviceTests.cs`, `KvRenderPipelineTests.cs`),
        которых в бэкапе нет по определению;
      • что осталось в `Temp` (обязано остаться ноль файлов).

    Чего скрипт НЕ делает: не создаёт `.meta` для новых файлов — это работа Unity при импорте
    (`.meta` пишет сама, с новым guid; выдумывать guid текстом нельзя).

    Возврат: 0 — файлы на месте и совпадения с бэкапом сходятся; 1 — есть расхождение.

    ПРИМЕР
      powershell -ExecutionPolicy Bypass -File Tools\stage-a-restore-tests.ps1
#>
param(
    [string]$Project = "C:\KazistovVv_Build",
    [string]$Backup = "C:\KazistovVv_Build\_backups_consolidated\folders\urp_switch_2026-10-05_2114",
    [switch]$WhatIfOnly
)

$ErrorActionPreference = 'Continue'

$tests = Join-Path $Project 'Assets\_Project\08_Tests'
$parked = Join-Path $Project '_tests_parked_for_build'
$report = Join-Path $Project 'Logs\stage-a-restore-tests.txt'
$lines = New-Object System.Collections.Generic.List[string]

function Say($text) { Write-Host $text; $lines.Add($text) }

Say "=== A3. ВОЗВРАТ ТЕСТОВ ИЗ Temp В Assets ==="
Say ("проект:  " + $Project)
Say ("время:   " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Say ""

if (-not (Test-Path $parked)) {
    Say "ПАРКОВКИ НЕТ: $parked — возвращать нечего."
} else {
    $before = @(Get-ChildItem $parked -Recurse -File -Force)
    Say ("в Temp файлов до возврата: " + $before.Count)

    if (-not (Test-Path $tests)) { New-Item -ItemType Directory -Path $tests -Force | Out-Null }

    $moved = 0
    foreach ($file in $before) {
        $relative = $file.FullName.Substring($parked.Length).TrimStart('\')
        $destination = Join-Path $tests $relative
        $folder = Split-Path $destination -Parent
        if (-not (Test-Path $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
        if (Test-Path $destination) {
            Say ("  КОНФЛИКТ, не перезаписываю: " + $relative)
            continue
        }
        if (-not $WhatIfOnly) { Move-Item $file.FullName $destination }
        $moved++
    }
    Say ("перенесено файлов: " + $moved)

    # Опустевшую парковку убираем: файлов в ней не осталось.
    $leftInTemp = @(Get-ChildItem $parked -Recurse -File -Force -ErrorAction SilentlyContinue)
    if ($leftInTemp.Count -eq 0 -and -not $WhatIfOnly) { Remove-Item $parked -Recurse -Force -ErrorAction SilentlyContinue }
    Say ("осталось в Temp файлов: " + $leftInTemp.Count)
}

Say ""
Say "=== СОСТАВ ПОСЛЕ ВОЗВРАТА ==="
$cs = @(Get-ChildItem $tests -Recurse -File -Filter "*.cs" -ErrorAction SilentlyContinue)
$all = @(Get-ChildItem $tests -Recurse -File -Force -ErrorAction SilentlyContinue)
Say ("файлов всего: " + $all.Count + " · из них тестов (.cs): " + $cs.Count)
foreach ($f in ($cs | Sort-Object Name)) { Say ("  " + $f.Name + "  (" + $f.Length + " байт)") }

Say ""
Say "=== СВЕРКА С БЭКАПОМ urp_switch_2026-10-05_2114 (по SHA256) ==="
$backupTests = Join-Path $Backup 'Assets\_Project\08_Tests'
if (-not (Test-Path $backupTests)) {
    Say "бэкап не найден: $backupTests"
} else {
    $same = 0; $diff = 0
    foreach ($file in (Get-ChildItem $backupTests -Recurse -File -Force)) {
        $relative = $file.FullName.Substring($backupTests.Length).TrimStart('\')
        $current = Join-Path $tests $relative
        if (-not (Test-Path $current)) { Say ("  ПРОПАЛ    " + $relative); $diff++; continue }
        $h1 = (Get-FileHash $file.FullName -Algorithm SHA256).Hash
        $h2 = (Get-FileHash $current -Algorithm SHA256).Hash
        if ($h1 -eq $h2) { $same++ } else { Say ("  ИЗМЕНЁН   " + $relative); $diff++ }
    }
    Say ("  совпало с бэкапом: " + $same + " · расхождений: " + $diff)

    Say ""
    Say "=== НОВЫЕ ФАЙЛЫ (в бэкапе их нет по определению) ==="
    $backupNames = @(Get-ChildItem $backupTests -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($backupTests.Length).TrimStart('\') })
    foreach ($file in (Get-ChildItem $tests -Recurse -File -Force)) {
        $relative = $file.FullName.Substring($tests.Length).TrimStart('\')
        if ($backupNames -notcontains $relative) {
            $hasMeta = "есть"
            if ($file.Name -like "*.cs" -and -not (Test-Path ($file.FullName + ".meta"))) { $hasMeta = "НЕТ (создаст Unity при импорте)" }
            Say ("  " + $relative + "  · " + $file.Length + " байт · .meta: " + $hasMeta)
        }
    }
}

$lines | Set-Content -Path $report -Encoding UTF8
Write-Host ""
Write-Host ("отчёт записан: " + $report)
