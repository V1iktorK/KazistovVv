<#
    РАЗБОР СНИМКА ЭКРАНА ШЛЕМА — ЧИСЛАМИ (сессия §31, пункт B3).

    ЗАЧЕМ. Приёмка «в шлеме нет розового» — это утверждение о КАРТИНКЕ. Глазами её смотрит
    владелец, но проверить это можно и измеримо: у розового «отсутствующего материала» в Unity
    характерный цвет (насыщенно-пурпурный), у чёрного экрана — почти полная чернота. Скрипт
    считает доли пикселей и говорит, что на снимке:
      • пусто/чёрный экран (шлем спит или приложение не рисует);
      • есть розовые (пурпурные) области — признак отсутствующего шейдера/материала;
      • картинка есть, и какая она по яркости и цвету.

    ВАЖНО ПРО ФОРМАТ. PICO снимает экран как 4320×2160 — это ДВА кадра рядом (левый и правый глаз).
    Скрипт считает их отдельно: если половины резко разные, это признак проблемы со стерео.

    ПАРАМЕТРЫ:
      -Image  путь к PNG (обязательно) или папка — тогда берутся все *.png по времени
      -Step   шаг выборки пикселей (по умолчанию 6: 4320×2160 → ~260 тыс. точек, быстро)

    Код возврата: 0 — снимок разобран; 1 — файла нет или не читается.
#>
param(
    [Parameter(Mandatory = $true)][string]$Image,
    [int]$Step = 6
)

$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue

$files = @()
if (Test-Path $Image -PathType Container) {
    $files = Get-ChildItem $Image -Filter *.png -File | Sort-Object LastWriteTime
} elseif (Test-Path $Image) {
    $files = @(Get-Item $Image)
} else {
    Write-Host "ОШИБКА: не найден $Image"
    exit 1
}

function Analyze-Region {
    param([System.Drawing.Bitmap]$Bmp, [int]$X0, [int]$X1, [int]$Y0, [int]$Y1, [int]$Step)

    $total = 0; $black = 0; $magenta = 0; $white = 0; $colorful = 0
    $sumR = 0.0; $sumG = 0.0; $sumB = 0.0
    $hist = @{}

    for ($y = $Y0; $y -lt $Y1; $y += $Step) {
        for ($x = $X0; $x -lt $X1; $x += $Step) {
            $c = $Bmp.GetPixel($x, $y)
            $total++
            $sumR += $c.R; $sumG += $c.G; $sumB += $c.B

            if ($c.R -lt 12 -and $c.G -lt 12 -and $c.B -lt 12) { $black++ }
            elseif ($c.R -gt 170 -and $c.G -lt 90 -and $c.B -gt 170) { $magenta++ }
            elseif ($c.R -gt 235 -and $c.G -gt 235 -and $c.B -gt 235) { $white++ }

            $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
            $min = [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
            if (($max - $min) -gt 40) { $colorful++ }

            $key = '{0},{1},{2}' -f ([int]($c.R / 64)), ([int]($c.G / 64)), ([int]($c.B / 64))
            if (-not $hist.ContainsKey($key)) { $hist[$key] = 0 }
            $hist[$key]++
        }
    }

    if ($total -eq 0) { return $null }
    return [pscustomobject]@{
        Total     = $total
        BlackPct  = [Math]::Round(100.0 * $black / $total, 1)
        MagentaPct= [Math]::Round(100.0 * $magenta / $total, 2)
        WhitePct  = [Math]::Round(100.0 * $white / $total, 1)
        ColorPct  = [Math]::Round(100.0 * $colorful / $total, 1)
        AvgR      = [Math]::Round($sumR / $total, 0)
        AvgG      = [Math]::Round($sumG / $total, 0)
        AvgB      = [Math]::Round($sumB / $total, 0)
        Kinds     = $hist.Count
    }
}

foreach ($f in $files) {
    Write-Host ''
    Write-Host ("=== " + $f.Name + " (" + [Math]::Round($f.Length / 1KB) + " КБ) ===")
    try {
        $img = [System.Drawing.Image]::FromFile($f.FullName)
    } catch {
        Write-Host '   НЕ ЧИТАЕТСЯ как изображение'; continue
    }

    $bmp = New-Object System.Drawing.Bitmap $img
    $w = $img.Width; $h = $img.Height
    Write-Host ("   размер кадра: {0}x{1}" -f $w, $h)

    $all = Analyze-Region -Bmp $bmp -X0 0 -X1 $w -Y0 0 -Y1 $h -Step $Step
    Write-Host ("   ВЕСЬ КАДР:    чёрных {0}% · белых {1}% · розовых {2}% · цветных {3}% · средний цвет RGB({4},{5},{6}) · оттенков {7}" -f `
        $all.BlackPct, $all.WhitePct, $all.MagentaPct, $all.ColorPct, $all.AvgR, $all.AvgG, $all.AvgB, $all.Kinds)

    # стерео: левая и правая половины должны быть похожи
    $half = [int]($w / 2)
    $left  = Analyze-Region -Bmp $bmp -X0 0 -X1 $half -Y0 0 -Y1 $h -Step $Step
    $right = Analyze-Region -Bmp $bmp -X0 $half -X1 $w -Y0 0 -Y1 $h -Step $Step
    Write-Host ("   ЛЕВЫЙ глаз:   чёрных {0}% · розовых {1}% · средний RGB({2},{3},{4})" -f `
        $left.BlackPct, $left.MagentaPct, $left.AvgR, $left.AvgG, $left.AvgB)
    Write-Host ("   ПРАВЫЙ глаз:  чёрных {0}% · розовых {1}% · средний RGB({2},{3},{4})" -f `
        $right.BlackPct, $right.MagentaPct, $right.AvgR, $right.AvgG, $right.AvgB)

    $delta = [Math]::Abs($left.BlackPct - $right.BlackPct) + [Math]::Abs($left.AvgR - $right.AvgR) / 2 +
             [Math]::Abs($left.AvgG - $right.AvgG) / 2 + [Math]::Abs($left.AvgB - $right.AvgB) / 2

    Write-Host ''
    Write-Host '   ВЕРДИКТ:'
    if ($all.BlackPct -gt 97) {
        Write-Host '     • ЭКРАН ПУСТ (чёрный). Либо шлем не надет (система усыпляет экран), либо приложение не рисует.' -ForegroundColor Yellow
    }
    if ($all.MagentaPct -gt 0.5) {
        Write-Host ("     • НАЙДЕНО РОЗОВОЕ: {0}% пикселей — признак отсутствующего шейдера/материала." -f $all.MagentaPct) -ForegroundColor Red
    } else {
        Write-Host ("     • розового нет (пурпурных пикселей {0}% — ниже порога 0,5%)." -f $all.MagentaPct)
    }
    if ($all.Kinds -le 2 -and $all.BlackPct -lt 97) {
        Write-Host ("     • картинка однородная (оттенков {0}) — похоже на заливку, а не на сцену." -f $all.Kinds) -ForegroundColor Yellow
    }
    if ($delta -gt 25) {
        Write-Host ("     • ПОЛОВИНЫ КАДРА РАЗЛИЧАЮТСЯ (метрика {0:N0}) — возможна проблема со стерео." -f $delta) -ForegroundColor Yellow
    } else {
        Write-Host ("     • левый и правый кадр согласованы (метрика {0:N0})." -f $delta)
    }

    $bmp.Dispose(); $img.Dispose()
}
exit 0
