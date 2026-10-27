# Разбор MainScene.unity (ТОЛЬКО ЧТЕНИЕ) — где стоят части ангара и стенды.
# Ничего не меняет: .unity не правится, файл открывается на чтение.
param(
    [string]$Scene = 'C:\KazistovVv_Build\Assets\_Project\00_Scenes\MainScene.unity'
)

$lines = Get-Content -LiteralPath $Scene -Encoding UTF8
Write-Output ("файл: " + $Scene + " · строк " + $lines.Count)

$instances = @()
$cur = $null
for ($i = 0; $i -lt $lines.Count; $i++) {
    $ln = $lines[$i]
    if ($ln -match '^--- !u!1001 &(\d+)') {
        if ($cur) { $instances += $cur }
        $cur = [ordered]@{ fileID = $Matches[1]; name = $null; guid = $null; pos = @{}; scale = @{}; rot = @{}; parent = $null }
        continue
    }
    if ($null -eq $cur) { continue }
    if ($ln -match '^--- ') { $instances += $cur; $cur = $null; continue }
    if ($ln -match 'm_TransformParent: \{fileID: (-?\d+)') { $cur.parent = $Matches[1] }
    if ($ln -match '^\s+propertyPath: (m_Name|m_LocalPosition\.(x|y|z)|m_LocalScale\.(x|y|z)|m_LocalRotation\.(x|y|z|w))\s*$') {
        $prop = $Matches[1]
        if ($i + 1 -lt $lines.Count) {
            $val = ($lines[$i + 1] -replace '^\s+value:\s*', '').Trim()
            switch -Regex ($prop) {
                '^m_Name$'      { $cur.name = $val }
                '^m_LocalPosition\.' { $cur.pos[$prop.Split('.')[1]] = $val }
                '^m_LocalScale\.'    { $cur.scale[$prop.Split('.')[1]] = $val }
                '^m_LocalRotation\.' { $cur.rot[$prop.Split('.')[1]] = $val }
            }
        }
    }
}
if ($cur) { $instances += $cur }

Write-Output ("экземпляров префабов (PrefabInstance): " + $instances.Count)

$hangar = $instances | Where-Object { $_.name -match '^Hangar_part' }
$floor  = $instances | Where-Object { $_.name -match '^Floor' }

Write-Output ""
Write-Output ("=== ЧАСТИ АНГАРА: " + $hangar.Count + " ===")
foreach ($h in $hangar | Sort-Object { $_.name }) {
    $p = "{0},{1},{2}" -f $h.pos['x'], $h.pos['y'], $h.pos['z']
    $s = "{0},{1},{2}" -f $h.scale['x'], $h.scale['y'], $h.scale['z']
    Write-Output ("  {0,-22} pos({1,-24}) scale({2}) parent={3}" -f $h.name, $p, $s, $h.parent)
}

Write-Output ""
Write-Output ("=== ПОЛ: " + $floor.Count + " ===")
foreach ($f in $floor) {
    $p = "{0},{1},{2}" -f $f.pos['x'], $f.pos['y'], $f.pos['z']
    $s = "{0},{1},{2}" -f $f.scale['x'], $f.scale['y'], $f.scale['z']
    Write-Output ("  {0,-22} pos({1,-24}) scale({2}) parent={3}" -f $f.name, $p, $s, $f.parent)
}

# Стенды: ищем по имени в обычных GameObject (не prefab instance)
Write-Output ""
Write-Output "=== СТЕНДЫ И СТОЛЕШНИЦА (обычные объекты сцены) ==="
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s+m_Name: (Стенд_\d+_Стол|Столешница|Стенд_\d+)$') {
        $nm = $Matches[1]
        # назад искать m_LocalPosition в пределах того же документа
        $pos = 'не найдено'
        for ($j = $i; $j -ge [Math]::Max(0, $i - 60); $j--) {
            if ($lines[$j] -match '^--- ') { break }
            if ($lines[$j] -match 'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}') {
                $pos = "$($Matches[1]),$($Matches[2]),$($Matches[3])"; break
            }
        }
        Write-Output ("  строка {0,-7} {1,-20} localPos {2}" -f $lines[$i].Length, $nm, $pos)
    }
    if ($lines[$i] -match '^\s+m_Name: (Стенд_[^\s]*)') {
        Write-Output ("  прочее имя стенда на строке " + ($i + 1) + ": " + $Matches[1])
    }
}
