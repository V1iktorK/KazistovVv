# Разбор MainScene.unity (ТОЛЬКО ЧТЕНИЕ): имена -> мировые/локальные координаты.
# Ничего не меняет. Нужен, чтобы понять, где стоят стенды и ангар ДРУГ ОТНОСИТЕЛЬНО ДРУГА.
param(
    [string]$Scene = 'C:\KazistovVv_Build\Assets\_Project\00_Scenes\MainScene.unity',
    [string[]]$Want = @('Стенд_1_Стол','Стенд_2_Стол','Столешница','Level','KazistovVv_UI','Spline','Ножка_1')
)

$raw = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($Scene))
# Раскодировать \uXXXX (Unity пишет кириллицу экранированной)
$raw = [regex]::Replace($raw, '\\u([0-9A-Fa-f]{4})', { param($m) [char][int]("0x" + $m.Groups[1].Value) })
$lines = $raw -split "`r?`n"

# Документы YAML: --- !u!<class> &<fileID>
$docs = @{}
$order = @()
$curId = $null; $curLines = @()
foreach ($ln in $lines) {
    if ($ln -match '^--- !u!(\d+) &(\d+)') {
        if ($curId) { $docs[$curId] = $curLines }
        $curId = $Matches[2]; $curLines = @($ln); $order += $curId
    } elseif ($curId) { $curLines += $ln }
}
if ($curId) { $docs[$curId] = $curLines }

# GameObject(class1) имя -> fileID
$goName = @{}
foreach ($id in $docs.Keys) {
    if ($docs[$id][0] -notmatch '^--- !u!1 &') { continue }
    foreach ($l in $docs[$id]) {
        if ($l -match '^\s+m_Name:\s*(.*)$') {
            $nm = $Matches[1].Trim().Trim('"')
            if ($nm -ne '') { $goName[$id] = $nm; break }
        }
    }
}

# Transform(class4) m_GameObject -> localPos / parent
$trByGo = @{}
foreach ($id in $docs.Keys) {
    if ($docs[$id][0] -notmatch '^--- !u!4 &') { continue }
    $go = $null; $pos = $null; $par = $null
    foreach ($l in $docs[$id]) {
        if ($l -match 'm_GameObject: \{fileID: (-?\d+)\}') { $go = $Matches[1] }
        if ($l -match 'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}') { $pos = @([double]$Matches[1], [double]$Matches[2], [double]$Matches[3]) }
        if ($l -match 'm_Father: \{fileID: (-?\d+)\}') { $par = $Matches[1] }
    }
    if ($go) { $trByGo[$go] = @{ id = $id; pos = $pos; par = $par } }
}

function Get-WorldPos([string]$goId) {
    $p = @(0.0, 0.0, 0.0); $guard = 0
    $g = $goId
    while ($g -and $trByGo.ContainsKey($g) -and $guard -lt 50) {
        $t = $trByGo[$g]
        if ($t.pos) { $p[0] += $t.pos[0]; $p[1] += $t.pos[1]; $p[2] += $t.pos[2] }
        if (-not $t.par -or $t.par -eq '0') { break }
        # parent transform id -> its gameobject id
        $pg = $null
        foreach ($k in $trByGo.Keys) { if ($trByGo[$k].id -eq $t.par) { $pg = $k; break } }
        $g = $pg; $guard++
    }
    return $p
}

Write-Output ("объектов GameObject с именем: " + $goName.Count)
Write-Output "=== ИНТЕРЕСУЮЩИЕ ОБЪЕКТЫ ==="
foreach ($w in $Want) {
    $found = $false
    foreach ($id in $goName.Keys) {
        if ($goName[$id] -eq $w) {
            $wp = Get-WorldPos $id
            $lp = if ($trByGo.ContainsKey($id) -and $trByGo[$id].pos) { $trByGo[$id].pos } else { @('?','?','?') }
            Write-Output ("  {0,-18} world({1,8:0.00},{2,8:0.00},{3,8:0.00})  local({4},{5},{6})" -f $w, $wp[0], $wp[1], $wp[2], $lp[0], $lp[1], $lp[2])
            $found = $true
        }
    }
    if (-not $found) { Write-Output ("  {0,-18} НЕ НАЙДЕН" -f $w) }
}

Write-Output ""
Write-Output "=== ЧТО ЗА РОДИТЕЛЬ У ЧАСТЕЙ АНГАРА: fileID 428515267 ==="
foreach ($k in $docs.Keys) {
    if ($docs[$k][0] -match '^--- !u!(\d+) &428515267') {
        Write-Output ("  класс " + $Matches[1])
        $docs[$k] | Select-Object -First 12 | ForEach-Object { Write-Output ("    " + $_) }
    }
}
