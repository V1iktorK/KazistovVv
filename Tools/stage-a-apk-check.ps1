<#
    ПРОВЕРКА ИДЕНТИФИКАТОРА И СОДЕРЖИМОГО APK (Этап А / §31).

    ЗАЧЕМ. «APK собран» — ещё не «в шлеме будет наше приложение»: идентификатор приложения
    виден не в файле проекта, а ВНУТРИ APK. Этап А даёт ровно этот факт для двух сборок:
      • APK №1 (сборка §30) — собран ДО правок §31, поэтому в нём обязан быть ШАБЛОННЫЙ
        идентификатор `com.UnityTechnologies.com.unity.template.urpblank`
        (`-ExpectedPackage` не задаём — печатаем то, что есть);
      • APK №2 — собран ПОСЛЕ правок §31, поэтому ожидается `com.KazistovVv.app`
        (`-ExpectedPackage com.KazistovVv.app` — расхождение даёт код возврата 1).

    ПОЧЕМУ aapt, А НЕ «посмотреть в Unity». `aapt` из состава Android-модуля Unity читает
    манифест так, как его прочитает установщик на шлеме. В PATH его нет — путь ищется здесь.

    Возврат: 0 — APK разобран (и, если задано ожидание, идентификатор совпал); 1 — иначе.

    ПРИМЕРЫ
      powershell -ExecutionPolicy Bypass -File Tools\stage-a-apk-check.ps1
      powershell -ExecutionPolicy Bypass -File Tools\stage-a-apk-check.ps1 -ExpectedPackage com.KazistovVv.app
#>
param(
    [string]$Project = "C:\KazistovVv_Build",
    [string[]]$Apk = @(),
    [string]$ExpectedPackage = ""
)

$ErrorActionPreference = 'Continue'

if ($Apk.Count -eq 0) {
    $Apk = @(
        (Join-Path $Project 'Builds\KazistovVv.apk'),
        (Join-Path $Project 'KazistovVv.apk')
    )
}

$aapt = Get-ChildItem "C:\Program Files\Unity\Hub\Editor\*\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\*\aapt.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $aapt) {
    Write-Host "aapt.exe не найден (Android-модуль Unity не установлен?) — разбор APK невозможен"
    exit 1
}
Write-Host "aapt: $($aapt.FullName)"

$found = $false
$ok = $true

foreach ($path in $Apk) {
    if (-not (Test-Path $path)) { continue }
    $found = $true
    $item = Get-Item $path
    Write-Host ""
    Write-Host "=== APK: $path ==="
    Write-Host ("  размер:  {0:N1} МБ" -f ($item.Length / 1MB))
    Write-Host ("  создан:  {0}" -f $item.LastWriteTime)
    Write-Host ("  SHA256:  {0}" -f (Get-FileHash $path -Algorithm SHA256).Hash)

    $badging = & $aapt.FullName dump badging $path 2>&1
    $package = ($badging | Select-String -Pattern "^package: name='([^']+)'" | Select-Object -First 1)
    if ($package) {
        $name = $package.Matches[0].Groups[1].Value
        Write-Host ("  ИДЕНТИФИКАТОР: {0}" -f $name)
        if ($ExpectedPackage -ne "") {
            if ($name -eq $ExpectedPackage) {
                Write-Host ("  ожидание {0}: СОВПАЛО" -f $ExpectedPackage) -ForegroundColor Green
            } else {
                Write-Host ("  ожидание {0}: НЕ СОВПАЛО" -f $ExpectedPackage) -ForegroundColor Red
                $ok = $false
            }
        }
    } else {
        Write-Host "  идентификатор прочитать не удалось" -ForegroundColor Red
        $ok = $false
    }

    foreach ($pattern in @("^application-label:", "^sdkVersion:", "^targetSdkVersion:", "^native-code:", "^launchable-activity:")) {
        $line = $badging | Select-String -Pattern $pattern | Select-Object -First 1
        if ($line) { Write-Host ("  {0}" -f $line.Line.Trim()) }
    }
}

if (-not $found) {
    Write-Host "APK не найден ни по одному из путей: $($Apk -join '; ')"
    exit 1
}

if ($ok) { exit 0 } else { exit 1 }
