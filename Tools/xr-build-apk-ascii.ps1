<#
    ПРОВЕРКА СБОРКИ APK ПО ЛАТИНСКОМУ ПУТИ (сессия §28).

    ЗАЧЕМ. Android-инструменты Unity (Gradle) отказываются работать, если ПУТЬ ПРОЕКТА
    содержит не-ASCII символы: «Invalid project path … contains non-ASCII characters».
    Рабочий каталог владельца — «…\новое пространство\KazistovVv», и выше по пути есть
    «Ольга». Этот скрипт ДОКАЗЫВАЕТ (или опровергает), что дело именно в пути: он делает
    копию проекта в латинский каталог, связывает тяжёлые каталоги (Library, Temp, obj)
    с оригиналом через junction (копирование 8 ГБ не требуется) и собирает APK.
    Оригинал при этом НЕ изменяется: в него пишет только Unity — в ту же Library, что
    и при обычной работе редактора.

    Возврат: 0 — APK собран; 1 — не собран (причина в логе).
#>
param(
    [string]$Source = "C:\Users\Ольга\OneDrive\Documentos\новое пространство\KazistovVv",
    [string]$Target = "C:\KazistovVv_Build",
    [string]$Unity  = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe",
    [int]$TimeoutMinutes = 45
)

$ErrorActionPreference = 'Continue'
$log = Join-Path $Target 'build.log'

Write-Host "=== [1/3] ПОДГОТОВКА ASCII-КОПИИ ==="
if (Test-Path $Target) {
    Write-Host "Целевой каталог уже есть: $Target (переиспользуем)"
} else {
    New-Item -ItemType Directory -Path $Target -Force | Out-Null
}

# Тяжёлые каталоги НЕ копируем — связываем с оригиналом (junction):
# так мы не тратим 8 ГБ и минуты, а Unity работает с той же библиотекой.
$junctionDirs = @('Library', 'Temp', 'obj', 'Logs', 'UserSettings', 'Builds')
$copyDirs     = @('Assets', 'Packages', 'ProjectSettings', 'Tools', 'Docs')

foreach ($d in $junctionDirs) {
    $src = Join-Path $Source $d
    $dst = Join-Path $Target $d
    if (-not (Test-Path $src)) { continue }
    if (Test-Path $dst) { Write-Host "  уже есть: $d"; continue }
    cmd /c mklink /J "`"$dst`"" "`"$src`"" | Out-Null
    Write-Host "  связан: $d"
}

foreach ($d in $copyDirs) {
    $src = Join-Path $Source $d
    $dst = Join-Path $Target $d
    if (-not (Test-Path $src)) { continue }
    robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
    Write-Host "  скопирован: $d"
}

# Одиночные файлы проекта, нужные Unity и сборке.
foreach ($f in @('PROJECT_CONTEXT.md')) {
    $src = Join-Path $Source $f
    if (Test-Path $src) { Copy-Item $src $Target -Force }
}

Write-Host ""
Write-Host "=== [2/3] СБОРКА APK (латинский путь) ==="
$argList = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', ('"' + $Target + '"'),
    '-executeMethod', 'KazistovVvXrEditor.KvXrTools.BuildAndroidApk',
    '-logFile', ('"' + $log + '"')
)
$p = Start-Process -FilePath $Unity -ArgumentList $argList -PassThru -NoNewWindow
$null = $p.WaitForExit($TimeoutMinutes * 60 * 1000)
if (-not $p.HasExited) {
    Write-Host "ТАЙМАУТ $TimeoutMinutes мин — процесс снимается"
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

Write-Host ""
Write-Host "=== [3/3] РЕЗУЛЬТАТ ==="
if (Test-Path $log) {
    Select-String -Path $log -Pattern '\[KvXr\]|Invalid project path|BuildFailedException' |
        Select-Object -First 20 | ForEach-Object { $_.Line }
}

$apk = Join-Path $Target 'Builds\KazistovVv.apk'
if (Test-Path $apk) {
    $mb = [math]::Round((Get-Item $apk).Length / 1MB, 1)
    Write-Host "APK ЕСТЬ: $apk ($mb МБ)"
    exit 0
} else {
    Write-Host "APK НЕТ по пути $apk"
    exit 1
}
