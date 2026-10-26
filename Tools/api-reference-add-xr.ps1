<#
    ДОПОЛНЕНИЕ СПРАВОЧНИКА API: XR-СЛОЙ И ИНСТРУМЕНТЫ КОНВЕЙЕРА (§28–§31).

    ЗАЧЕМ. `Docs\API_REFERENCE.md` — снимок от 15.09.2026: в нём нет ни одного типа из XR-слоя
    (`KazistovVvXr`, 13 классов, §28) и ни одного инструмента конвейера рендера
    (`KazistovVvXrEditor`, §29–§31). То есть половина того, чем сейчас живёт проект, в справочнике
    отсутствует — а справочником пользуются, чтобы не читать код.

    ЧТО ДЕЛАЕТ. Читает исходники, достаёт пространство имён, объявление типа, первый блок
    `/// <summary>` и публичные члены, печатает раздел ТЕМ ЖЕ форматом, что и остальной справочник
    (заголовок `### \`class X\``, строка «Файл», строка «Назначение», таблица членов).
    Ничего не выдумывает: только то, что написано в коде.

    ПАРАМЕТРЫ:
      -Project  корень проекта (по умолчанию C:\KazistovVv_Build)
      -Apply    дописать раздел в `Docs\API_REFERENCE.md` перед разделом «Источники»
      -Out      путь для файла-черновика (по умолчанию Tools\_stage_a\API_S28_31.md)

    Код возврата: 0 — раздел собран; 1 — не найден проект или справочник.
#>
param(
    [string]$Project = 'C:\KazistovVv_Build',
    [switch]$Apply,
    [string]$Out
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Project)) { Write-Host "ОШИБКА: нет проекта $Project"; exit 1 }
if (-not $Out) { $Out = Join-Path $Project 'Tools\_stage_a\API_S28_31.md' }

$scripts = Join-Path $Project 'Assets\_Project\01_Scripts'
$targets = @()
$targets += Get-ChildItem (Join-Path $scripts 'Xr') -Filter *.cs -File
$targets += @(
    (Join-Path $scripts 'Features\KvRenderPipelineShaders.cs'),
    (Join-Path $scripts 'Editor\KvXrTools.cs'),
    (Join-Path $scripts 'Editor\KvXrDeviceTools.cs'),
    (Join-Path $scripts 'Editor\KvRenderPipelineTools.cs'),
    (Join-Path $scripts 'Editor\KvUrpKeywordPolicy.cs'),
    (Join-Path $scripts 'Editor\KvShaderVariantStripper.cs')
) | Where-Object { Test-Path $_ } | ForEach-Object { Get-Item $_ }

# ---------------------------------------------------------------- разбор одного файла

function Get-FirstSummary {
    param([string[]]$Lines, [int]$DeclIndex)
    # Ищем блок summary ВЫШЕ объявления. Начинаем с предыдущей строки: сама строка объявления
    # (`public class …`) под прежний «стоп по признаку кода» и обрывала поиск сразу.
    for ($i = $DeclIndex - 1; $i -ge 0; $i--) {
        if ($Lines[$i] -match '^\s*///\s*<summary>') {
            $buf = @()
            for ($j = $i; $j -lt $Lines.Count; $j++) {
                $t = $Lines[$j] -replace '^\s*///\s?', ''
                if ($t -match '</summary>') { break }
                if ($t -match '<summary>') { continue }
                $buf += $t
            }
            $text = ($buf -join ' ').Trim()
            $text = $text -replace '\s+', ' '
            if ($text.Length -gt 700) { $text = $text.Substring(0, 700) + '…' }
            return $text
        }
        # Между объявлением и summary начинается КОД другого члена — значит, своего summary нет.
        if ($Lines[$i] -notmatch '^\s*///' -and $Lines[$i].Trim().Length -gt 0 -and
            $Lines[$i] -match '^\s*(\}|\]|public|private|internal|protected)') { break }
    }
    return ''
}

function Get-EnumValues {
    param([string[]]$Lines, [int]$DeclIndex)
    $rows = @()
    for ($i = $DeclIndex + 1; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '^\s*\}') { break }
        if ($Lines[$i] -match '^\s*([A-Za-z_]\w*)\s*(=\s*[^,]+)?\s*,?\s*$') {
            $decl = $Lines[$i].Trim().TrimEnd(',')
            $rows += [pscustomobject]@{ Kind = 'значение'; Name = $Matches[1]; Decl = $decl }
        }
    }
    return $rows
}

function Get-PublicMembers {
    param([string[]]$Lines, [int]$DeclIndex, [string]$TypeName, [string]$TypeKind)
    if ($TypeKind -eq 'enum') { return (Get-EnumValues -Lines $Lines -DeclIndex $DeclIndex) }

    $rows = @()
    for ($i = $DeclIndex + 1; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]
        # Закрывающая скобка типа: отступ ЛЮБОЙ (прежнее условие «строго с нулевого отступа»
        # не срабатывало — и перечисление затягивало в себя члены следующего типа).
        if ($line -match '^\s*\}') { break }
        if ($line -notmatch '^\s*public\s' -or $line -match '^\s*///') { continue }

        $decl = $line.Trim()
        if ($decl.Length -gt 160) { $decl = $decl.Substring(0, 160) + '…' }

        # ВАЖНО: $Matches перезаписывается КАЖДЫМ -match. Поэтому имя вытаскиваем в переменную
        # сразу после основного совпадения, а классификацию вида делаем уже потом.
        $name = ''
        $kind = 'метод'
        if ($line -match '^\s*public\s+(static\s+)?(const\s+|readonly\s+)?[A-Za-z_][\w<>\[\],\.\?]*\s+([A-Za-z_]\w*)\s*[=;]') {
            $name = $Matches[3]
            if ($line -match 'const\s') { $kind = 'константа' }
            elseif ($line -match 'readonly\s') { $kind = 'поле (только чтение)' }
            else { $kind = 'поле' }
        } elseif ($line -match '^\s*public\s+[A-Za-z_][\w<>\[\],\.\?]*\s+([A-Za-z_]\w*)\s*\{\s*(get|set)') {
            $name = $Matches[1]
            $kind = 'свойство'
        } elseif ($line -match '^\s*public\s+(static\s+)?[A-Za-z_][\w<>\[\],\.\?]*\s+([A-Za-z_]\w*)\s*\(') {
            $name = $Matches[2]
            $kind = 'метод'
        } elseif ($line -match '^\s*public\s+(enum|struct|class|interface)\s+([A-Za-z_]\w*)') {
            $name = $Matches[2]
            $kind = 'тип'
        } else {
            continue
        }
        $rows += [pscustomobject]@{ Kind = $kind; Name = $name; Decl = $decl }
    }
    return $rows
}

# ---------------------------------------------------------------- сборка раздела

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine()
[void]$sb.AppendLine('---')
[void]$sb.AppendLine()
[void]$sb.AppendLine('## Дополнение 08.10.2026: XR-слой и конвейер рендера (§28–§31)')
[void]$sb.AppendLine()
[void]$sb.AppendLine('_Этот раздел добавлен к справочнику, потому что снимок от 15.09.2026 не содержит')
[void]$sb.AppendLine('ни одного типа XR-слоя (§28) и ни одного инструмента конвейера рендера (§29–§31).')
[void]$sb.AppendLine('Собран из исходников скриптом `Tools\api-reference-add-xr.ps1`, формат — как у остальных')
[void]$sb.AppendLine('разделов: файл, назначение, таблица публичных членов._')
[void]$sb.AppendLine()

$byNamespace = @{}
foreach ($file in ($targets | Sort-Object FullName)) {
    $lines = Get-Content $file.FullName
    $declIndex = -1
    $typeKind = ''
    $typeName = ''
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*public\s+(static\s+|sealed\s+|abstract\s+|partial\s+)*(class|enum|struct)\s+([A-Za-z_]\w*)') {
            $declIndex = $i
            $typeKind = $Matches[2]
            $typeName = $Matches[3]
            break
        }
    }
    if ($declIndex -lt 0) { continue }

    $ns = ''
    foreach ($l in $lines) { if ($l -match '^\s*namespace\s+(.+)$') { $ns = $Matches[1].Trim(); break } }
    if (-not $ns) { $ns = '(глобальное пространство имён)' }

    $rel = $file.FullName.Replace($Project + '\', '')
    $entry = [pscustomobject]@{
        Kind    = $typeKind
        Name    = $typeName
        File    = $rel
        Line    = $declIndex + 1
        Summary = (Get-FirstSummary -Lines $lines -DeclIndex $declIndex)
        Members = (Get-PublicMembers -Lines $lines -DeclIndex $declIndex -TypeName $typeName -TypeKind $typeKind)
    }
    if (-not $byNamespace.ContainsKey($ns)) { $byNamespace[$ns] = @() }
    $byNamespace[$ns] += $entry
}

foreach ($ns in ($byNamespace.Keys | Sort-Object)) {
    [void]$sb.AppendLine("## Пространство имён ``$ns``")
    [void]$sb.AppendLine()
    foreach ($t in ($byNamespace[$ns] | Sort-Object Name)) {
        [void]$sb.AppendLine("### ``$($t.Kind) $($t.Name)``")
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("- **Файл:** ``$($t.File)`` (строка $($t.Line))")
        $sum = if ($t.Summary) { $t.Summary } else { '—' }
        [void]$sb.AppendLine("- **Назначение:** $sum")
        [void]$sb.AppendLine()
        if ($t.Members.Count -gt 0) {
            [void]$sb.AppendLine('| Тип | Имя | Объявление | Описание |')
            [void]$sb.AppendLine('|---|---|---|---|')
            foreach ($m in $t.Members) {
                [void]$sb.AppendLine("| $($m.Kind) | ``$($m.Name)`` | ``$($m.Decl)`` |  |")
            }
            [void]$sb.AppendLine()
        }
    }
}

$text = $sb.ToString()
New-Item -ItemType Directory -Path (Split-Path $Out -Parent) -Force | Out-Null
$text | Set-Content -Path $Out -Encoding UTF8
Write-Host "Черновик раздела: $Out"
Write-Host ("  пространств имён: {0} · типов: {1} · строк: {2}" -f $byNamespace.Keys.Count, (($byNamespace.Values | ForEach-Object { $_.Count }) | Measure-Object -Sum).Sum, ($text -split "`n").Count)

if ($Apply) {
    $docPath = Join-Path $Project 'Docs\API_REFERENCE.md'
    if (-not (Test-Path $docPath)) { Write-Host "ОШИБКА: нет $docPath"; exit 1 }
    $doc = Get-Content $docPath -Raw
    if ($doc -match 'Дополнение 08\.10\.2026: XR-слой') {
        Write-Host 'Раздел уже есть в справочнике — правка не нужна.'
    } else {
        $marker = '## Источники'
        $at = $doc.IndexOf($marker)
        if ($at -lt 0) { Write-Host "ОШИБКА: не найден раздел «Источники» — вставка невозможна"; exit 1 }
        $new = $doc.Substring(0, $at) + $text + "`n" + $doc.Substring($at)
        Set-Content -Path $docPath -Value $new -Encoding UTF8
        Write-Host ("Справочник дополнен: {0} → {1} байт" -f $doc.Length, $new.Length)
    }
}
exit 0
