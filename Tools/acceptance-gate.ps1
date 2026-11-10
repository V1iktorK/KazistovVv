<#
.SYNOPSIS
    ПРИЁМОЧНЫЙ ГЕЙТ ПРОЕКТА KazistovVv: машинный вердикт «работает / не работает».

.DESCRIPTION
    ЗАЧЕМ. Раньше вердикт выносил человек (я) по отчёту разработчика — и дважды принял
    несделанное за сделанное (артефакт от 05.11 как «свежий»; R6-1 «исправлен», хотя панель не
    открывалась). Теперь вердикт выносит ЭТОТ СКРИПТ: спорить с ним нельзя, «у меня зелёный отчёт»
    не принимается.

    КОДЫ ВОЗВРАТА:
      0 — ВСЁ зелёное, полный прогон инженера-тестировщика пройден идеально;
      1 — есть красные пункты (перечислены в выводе), работа продолжается;
      3 — СЧЁТЧИК ПОВТОРОВ: один и тот же пункт красный 3 прогона подряд. Это значит, что дело
          не в старании разработчика, а в том, что дефект не решается этим путём → стоп и отчёт
          владельцу, а не четвёртый круг.

    ПРОВЕРКИ (каждая — «код» + «что значит» + «как измерено»):
      EDITMODE      EditMode-тесты: упавших 0 И тестов не меньше 1264 (набор не уменьшился)
      REPO_GUARDS   Tools/repo-guards.ps1: провалов 0
      UI_STUBS      в реестре команд нет кнопок-заглушек (Stub = true) — требование владельца
      P4_3_RATCHET  число тестов, читающих исходники, не растёт (храповик)
      SECRET        пароля ключа подписи нет ни в отслеживаемых, ни в новых файлах
      GIT_ASSETS    после прогона нет изменённых .mat/.unity/.prefab (P4-15)
      APK_NAMES     оба APK существуют под правильными именами
      APK_FRESH     время APK новее последнего коммита (иначе это старая сборка)
      E2E_PC        Е2Е в редакторе: код 0 и ни одного [FAIL]  ← раскладка ПК
      E2E_AR        Е2Е в раскладке AR на планшете: панель «Настройки» реально на экране
      MUTATION      негативный контроль: откат правки обязан покрасить соответствующий тест
      HEADSET       шлем Pico: VR/MR, панель настроек, переключатель VR|MR
      UI_SWEEP      самопрогон интерфейса НА УСТРОЙСТВАХ: AR (планшет), VR и MR (шлем) — каждая
                    кнопка нажата в живом интерфейсе; «без эффекта» вне списка исключений — провал
      REAL_TABLE    распознавание РЕАЛЬНОГО стола: правила доказаны тестами, а устройства напечатали
                    вердикт С ЧИСЛАМИ (AR-плоскости / разметка комнаты PICO)

    ЧЕСТНО О ГРАНИЦАХ: проверки E2E_AR, HEADSET, UI_SWEEP и REAL_TABLE требуют физических устройств. Если устройство
    не подключено — пункт красный с причиной «устройство не найдено», и это НЕ обходится флагом:
    иначе гейт врал бы так же, как отчёты.

.PARAMETER ProjectPath
    Корень проекта. По умолчанию — родитель каталога со скриптом.

.PARAMETER UnityExe
    Путь к Unity.exe. По умолчанию — версия из ProjectSettings\ProjectVersion.txt.

.PARAMETER TabletId
    Серийный номер планшета (раскладка AR). По умолчанию — автоопределение.

.PARAMETER HeadsetId
    Серийный номер шлема. По умолчанию — автоопределение.

.PARAMETER SkipUnity
    Пропустить всё, что требует Unity (EditMode, Е2Е, мутации). Для быстрой проверки «железных»
    пунктов. ВНИМАНИЕ: пропущенные пункты помечаются КРАСНЫМИ как «не проверено», а не зелёными.

.PARAMETER NoState
    Не читать и не писать файл счётчика повторов (разовый прогон).

.EXAMPLE
    pwsh -File Tools\acceptance-gate.ps1

.EXAMPLE
    pwsh -File Tools\acceptance-gate.ps1 -SkipUnity -NoState
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = "",
    [string]$UnityExe = "",
    [string]$TabletId = "",
    [string]$HeadsetId = "",
    [switch]$SkipUnity,
    [switch]$NoState,
    [int]$TestsTimeoutMinutes = 60
)

$ErrorActionPreference = 'Continue'

# ================================================================ 0. ОКРУЖЕНИЕ
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $vf = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
    if (Test-Path -LiteralPath $vf) {
        $m = Select-String -LiteralPath $vf -Pattern 'm_EditorVersion:\s*(\S+)'
        if ($m) { $UnityExe = "C:\Program Files\Unity\Hub\Editor\$($m.Matches[0].Groups[1].Value)\Editor\Unity.exe" }
    }
}
$Adb = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
$LogsDir = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null
$StateFile = Join-Path $LogsDir 'gate-state.json'
$BuildDir = 'C:\KazistovVv_Build'

$MinTests = 1264
$results = New-Object System.Collections.Generic.List[object]

function Add-Result([string]$code, [bool]$ok, [string]$detail) {
    $results.Add([pscustomobject]@{ Code = $code; Ok = $ok; Detail = $detail })
    $mark = if ($ok) { '[ OK ]' } else { '[КРАСНО]' }
    Write-Host ("{0} {1,-12} {2}" -f $mark, $code, $detail)
}

function Stop-Unity {
    Get-Process Unity -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

function Invoke-Unity([string[]]$argList, [int]$timeoutMin) {
    Stop-Unity
    $p = Start-Process -FilePath $UnityExe -ArgumentList $argList -PassThru -NoNewWindow
    if (-not $p.WaitForExit($timeoutMin * 60 * 1000)) {
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        return 124
    }
    return 0
}

function Get-Devices {
    $list = @()
    if (-not (Test-Path $Adb)) { return $list }
    & $Adb start-server 2>&1 | Out-Null
    foreach ($line in (& $Adb devices 2>&1)) {
        if ($line -match '^(\S+)\s+device$') {
            $id = $matches[1]
            $model = (& $Adb -s $id shell getprop ro.product.model 2>&1) -join '' 
            $list += [pscustomobject]@{ Id = $id; Model = $model.Trim() }
        }
    }
    return $list
}

Write-Host ""
Write-Host "########################################################################"
Write-Host "#  ПРИЁМОЧНЫЙ ГЕЙТ KazistovVv"
Write-Host "#  проект: $ProjectPath"
Write-Host "#  время:  $(Get-Date -Format 'dd.MM.yyyy HH:mm:ss')"
Write-Host "########################################################################"
Write-Host ""

# ================================================================ 1. GIT_ASSETS
Write-Host "--- исходное состояние репозитория ---"
Push-Location -LiteralPath $ProjectPath
try {
    $dirty = @(git -c core.quotepath=false status --porcelain 2>&1)
    $assetDirty = @($dirty | Where-Object { $_ -match '\.(mat|unity|prefab)$' })
    if ($assetDirty.Count -eq 0) {
        Add-Result 'GIT_ASSETS' $true "изменённых .mat/.unity/.prefab нет (всего изменённых файлов: $($dirty.Count))"
    } else {
        Add-Result 'GIT_ASSETS' $false ("ассеты изменены — прогон пишет в проект: " + ($assetDirty -join '; '))
    }

    # ---------------------------------------------------------------- 2. SECRET
    # ПРАВИЛА ПРОВЕРКИ (иначе она ловит саму себя и обесценивается):
    #   • ищется ПРИСВОЕНИЕ ЛИТЕРАЛА, а не упоминание: значение в кавычках длиной >= 6;
    #   • значение, начинающееся с «<», — заглушка-плейсхолдер, а не секрет (в документах законно
    #     писать `<ПАРОЛЬ УДАЛЁН …>`);
    #   • имя ВНУТРИ кавычек не считается присвоением: `DoesNotContain("LegacyPassword =", …)` —
    #     это тест, который как раз проверяет ОТСУТСТВИЕ пароля. Отсекается `(?<!" )`.
    # ДВА ЭТАПА, А НЕ ОДИН: `git grep -E` использует POSIX ERE, где НЕТ lookaround — попытка
    # передать `(?!<)` валит git с «Invalid preceding regular expression» (проверено 12.11.2026).
    # Поэтому сначала грубый отбор средствами git (обычный ERE), затем точная проверка .NET-регуляркой,
    # которая lookaround поддерживает.
    $candidatePattern = '(LegacyPassword|KAZISTOVVV_KEYPASS)[[:space:]]*='
    $candidates = @(git grep --untracked -n -E $candidatePattern 2>&1 |
        Where-Object { $_ -notmatch 'cannot be used with revs' })
    $precisePattern = '(?<!")(?:LegacyPassword|KAZISTOVVV_KEYPASS)\s*=\s*"(?!<)[^"]{6,}"'
    $secretHits = @($candidates | Where-Object { $_ -match $precisePattern })
    if ($secretHits.Count -eq 0) {
        Add-Result 'SECRET' $true 'присвоений литерала пароля нет (tracked + новые файлы)'
    } else {
        Add-Result 'SECRET' $false ($secretHits -join ' | ')
    }
    # ---------------------------------------------------------------- 3. APK_NAMES / FRESH
    $vr = Join-Path $BuildDir 'Builds\KazistovVv-VR-vulkan.apk'
    $ar = Join-Path $BuildDir 'Builds\KazistovVv-AR-gles3.apk'
    $missing = @()
    if (-not (Test-Path -LiteralPath $vr)) { $missing += 'KazistovVv-VR-vulkan.apk' }
    if (-not (Test-Path -LiteralPath $ar)) { $missing += 'KazistovVv-AR-gles3.apk' }
    if ($missing.Count -eq 0) {
        Add-Result 'APK_NAMES' $true ("оба APK на месте: VR " + [math]::Round((Get-Item $vr).Length/1MB,1) +
            " МБ · AR " + [math]::Round((Get-Item $ar).Length/1MB,1) + " МБ")
    } else {
        Add-Result 'APK_NAMES' $false ("нет: " + ($missing -join ', '))
    }

    # СВЕЖЕСТЬ APK СРАВНИВАЕТСЯ С ИЗМЕНЕНИЕМ КОДА, А НЕ С ЛЮБЫМ КОММИТОМ (исправлено 12.11.2026).
    # Первая версия сравнивала со временем ПОСЛЕДНЕГО коммита — и краснела на коммите документации,
    # хотя код не менялся. Правильный эталон: самое позднее из (а) коммита, тронувшего Assets или
    # ProjectSettings, (б) времени изменения КОДА, КОТОРЫЙ УЕЗЖАЕТ В APK.
    #
    # ЧТО ИСПРАВЛЕНО ВТОРОЙ РАЗ (12.11.2026, мой же дефект). Эталон считался по ЛЮБОМУ `.cs` в
    # `Assets`, включая `08_Tests` и каталоги `Editor`. Тесты и редакторский код в APK НЕ ВХОДЯТ
    # (это Editor-сборки), поэтому правка комментария в тесте объявляла собранный APK «старым» и
    # требовала пересборки на 40 минут. Ложное красное хуже пропущенного: к нему привыкают и
    # перестают читать. Теперь из эталона исключены `08_Tests` и каталоги `Editor` — смысл проверки
    # сохранён («в APK лежит СВЕЖИЙ рантайм-код»), а шум убран.
    #
    # ЧТО ИСПРАВЛЕНО ТРЕТИЙ РАЗ (12.11.2026). Из эталона убрано ВРЕМЯ КОММИТА. Коммит не меняет
    # содержимое исходников, поэтому не делает собранный APK старым; но время коммита всегда позже
    # сборки, и любая фиксация работы объявляла артефакты «устаревшими» — то есть правильный порядок
    # «собрал → закоммитил» наказывался, а обходить это приходилось пересборкой на 40 минут.
    # Эталон теперь — ВРЕМЯ ИЗМЕНЕНИЯ КОДА, который уезжает в APK. Смысл прежний: если рантайм-код
    # новее артефакта, артефакт собран из старого кода. Время коммита остаётся только страховкой на
    # случай, когда список файлов получить не удалось.
    $codeCommitUnix = [int64](git log -1 --format=%ct -- Assets ProjectSettings 2>&1)
    $codeCommitTime = [DateTimeOffset]::FromUnixTimeSeconds($codeCommitUnix).LocalDateTime
    $allCode = @(Get-ChildItem -LiteralPath (Join-Path $ProjectPath 'Assets') -Recurse -File -Include *.cs,*.asmdef -ErrorAction SilentlyContinue)
    $shippedCode = @($allCode | Where-Object { $_.FullName -notmatch '[\\/]08_Tests[\\/]' -and $_.FullName -notmatch '[\\/]Editor[\\/]' })
    $baseline = if ($shippedCode.Count -gt 0) { ($shippedCode | Measure-Object -Property LastWriteTime -Maximum).Maximum } else { $codeCommitTime }

    $staleList = @()
    foreach ($pair in @(@('VR', $vr), @('AR', $ar))) {
        if (-not (Test-Path -LiteralPath $pair[1])) { continue }
        $t = (Get-Item -LiteralPath $pair[1]).LastWriteTime
        if ($t -lt $baseline) {
            $staleList += ("{0}: {1} СТАРЕЕ самого нового изменения кода {2}" -f $pair[0], $t, $baseline)
        }
    }
    if ($staleList.Count -eq 0) {
        Add-Result 'APK_FRESH' $true "оба APK новее изменений кода ($baseline)"
    } else {
        Add-Result 'APK_FRESH' $false ($staleList -join ' | ')
    }
}finally { Pop-Location }

# ================================================================ 4. REPO_GUARDS
Write-Host ""
Write-Host "--- сторожа репозитория ---"
$guardsOut = & pwsh -NoProfile -File (Join-Path $ProjectPath 'Tools\repo-guards.ps1') 2>&1
$guardsCode = $LASTEXITCODE
$guardsTail = ($guardsOut | Select-String -Pattern 'провалов:.*предупреждений' | Select-Object -Last 1)
if ($guardsCode -eq 0) {
    Add-Result 'REPO_GUARDS' $true ("код 0 · " + $(if ($guardsTail) { $guardsTail.Line.Trim() } else { 'пройдено' }))
} else {
    $fails = ($guardsOut | Select-String -Pattern 'ПРОВАЛЫ|::error' | Select-Object -First 5 | ForEach-Object { $_.Line.Trim() }) -join ' | '
    Add-Result 'REPO_GUARDS' $false ("код $guardsCode · $fails")
}

# ================================================================ 4в. P4_3_RATCHET
#
# ЗАЧЕМ. В наборе есть тесты, которые читают ТЕКСТ исходников и грепают его. Такой тест ломается от
# переформулировки комментария и, что хуже, ПРОХОДИТ на сломанном коде (проверяет «строка есть», а не
# «поведение верное»). Полностью переписать все за один заход нельзя, поэтому вводится ХРАПОВИК:
# число таких файлов не должно РАСТИ. Сегодня — 59; каждая волна правок обязана его уменьшать.
#
# ПОЧЕМУ ХРАПОВИК В ГЕЙТЕ, А НЕ ПОЖЕЛАНИЕ В ДОКУМЕНТЕ: без машинной проверки «не растёт» через
# месяц снова будет 90, и никто не заметит.
Write-Host ""
Write-Host "--- тесты, читающие исходники (P4-3) ---"
$MaxGrepTestFiles = 56
$testRoot = Join-Path $ProjectPath 'Assets\_Project\08_Tests'
$grepFiles = @()
if (Test-Path -LiteralPath $testRoot) {
    foreach ($f in @(Get-ChildItem -LiteralPath $testRoot -Recurse -Filter *.cs -File -ErrorAction SilentlyContinue)) {
        $txt = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
        if ($txt -match 'File\.ReadAllText|ReadScript\(') { $grepFiles += $f.Name }
    }
}
if ($grepFiles.Count -le $MaxGrepTestFiles) {
    Add-Result 'P4_3_RATCHET' $true ("грепающих тестов " + $grepFiles.Count + " — не больше порога $MaxGrepTestFiles")
} else {
    Add-Result 'P4_3_RATCHET' $false ("грепающих тестов СТАЛО БОЛЬШЕ: " + $grepFiles.Count + " > $MaxGrepTestFiles — набор деградирует")
}
# ================================================================ 4б. UI_STUBS (требование владельца)
#
# ВЛАДЕЛЕЦ 12.11.2026: «главное — чтобы все кнопочки были заполнены функционалом».
# Кнопка-заглушка в интерфейсе — обещание без функционала, то есть дефект.
#
# ВАЖНО ПРО МЕТОД (иначе проверка даёт ЛОЖНУЮ ТРЕВОГУ). Заглушка может быть ПЕРЕКРЫТА позже:
# `KvCommands.Register` заменяет команду с тем же id, и в проекте штатные заглушки
# (`edit.undo`, `edit.redo`, `file.export`, `tool.measure`, `tool.grid`) перерегистрируются
# реальными обработчиками в `FeatureHub`. Поэтому считается не «есть ли слово Stub», а:
#   для каждого id, зарегистрированного заглушкой, есть ли ВТОРАЯ регистрация того же id.
# Нет второй — кнопка в интерфейсе реально ничего не делает.
Write-Host ""
Write-Host "--- заглушки в реестре команд ---"
$uiRoots = @('Assets\_Project\06_KazistovVv_UI', 'Assets\_Project\01_Scripts')
$files = @()
foreach ($root in $uiRoots) {
    $full = Join-Path $ProjectPath $root
    if (Test-Path -LiteralPath $full) {
        $files += @(Get-ChildItem -LiteralPath $full -Recurse -Filter *.cs -File -ErrorAction SilentlyContinue)
    }
}

# 1) все регистрации: id -> сколько раз регистрируется
$idCounts = @{}
# 2) какие id зарегистрированы как заглушки (Stub = true внутри блока до следующего Id)
$stubIds = @{}
foreach ($f in $files) {
    $curId = $null
    $seenStub = $false
    $flush = {
        if ($curId) {
            if (-not $idCounts.ContainsKey($curId)) { $idCounts[$curId] = 0 }
            $idCounts[$curId]++
            if ($seenStub) { $stubIds[$curId] = ("{0}:{1}" -f $f.Name, $stubLine) }
        }
    }
    $lineNo = 0
    foreach ($line in [System.IO.File]::ReadLines($f.FullName)) {
        $lineNo++
        if ($line -match 'Id\s*=\s*"([^"]+)"') {
            if ($curId) {
                if (-not $idCounts.ContainsKey($curId)) { $idCounts[$curId] = 0 }
                $idCounts[$curId]++
                if ($seenStub) { $stubIds[$curId] = ("{0}:{1}" -f $f.Name, $stubLine) }
            }
            $curId = $matches[1]
            $seenStub = $false
            $stubLine = 0
        }
        if ($line -match '^\s*Stub\s*=\s*true\s*;?\s*$') { $seenStub = $true; $stubLine = $lineNo }
    }
}
if ($curId) {
    if (-not $idCounts.ContainsKey($curId)) { $idCounts[$curId] = 0 }
    $idCounts[$curId]++
    if ($seenStub) { $stubIds[$curId] = ("последний блок") }
}

$deadButtons = @()
$shadowed = @()
foreach ($id in $stubIds.Keys) {
    $n = if ($idCounts.ContainsKey($id)) { $idCounts[$id] } else { 1 }
    if ($n -ge 2) { $shadowed += $id } else { $deadButtons += ("{0} ({1})" -f $id, $stubIds[$id]) }
}

if ($deadButtons.Count -eq 0) {
    Add-Result 'UI_STUBS' $true ("кнопок без функционала нет (перекрытых заглушек-регистраций: " + $shadowed.Count + ")")
} else {
    Add-Result 'UI_STUBS' $false ("КНОПКИ БЕЗ ФУНКЦИОНАЛА: " + $deadButtons.Count + " → " + ($deadButtons -join ', '))
}
# ================================================================ 5. EDITMODE
Write-Host ""
Write-Host "--- EditMode-тесты ---"

# ЧУЖОЙ КЭШ КАНАЛОВ СКРИПТОВ — ДОЛОЙ (найдено 12.11.2026).
#
# ЧТО БЫЛО. `Tools\xr-build-apk-ascii.ps1` собирает APK в латинской копии проекта, у которой
# `Library` связан с оригиналом junction'ом. Unity кладёт в `Library\Bee\artifacts\rsp\*.rsp`
# имя своего канала связи `unity-ilpp-<хеш>`, и хеш считается ОТ ПУТИ ПРОЕКТА. После сборки в
# копии в общем кэше остаются rsp с ЧУЖИМ именем; прогон тестов в оригинале берёт их как
# «уже собранные» и падает:
#     ILPPTrigger: Can't find file \\.\pipe\unity-ilpp-…
#     Scripts have compiler errors.      ← и ни одной ошибки `error CS` в логе
# Это выглядело как «сломался код», хотя код был цел. Проверено: в кэше лежали ДВА разных имени
# канала; после очистки прогон стал зелёным. Гейт чистит кэш сам, чтобы вердикт не зависел от
# того, собирали ли APK перед прогоном.
if (-not $SkipUnity) {
    $staleRspDir = Join-Path $ProjectPath 'Library\Bee\artifacts\rsp'
    $staleRsp = 0
    if (Test-Path -LiteralPath $staleRspDir) {
        foreach ($rspFile in @(Get-ChildItem -LiteralPath $staleRspDir -Filter *.rsp -File -ErrorAction SilentlyContinue)) {
            Remove-Item -LiteralPath $rspFile.FullName -Force -ErrorAction SilentlyContinue
            if (-not (Test-Path -LiteralPath $rspFile.FullName)) { $staleRsp++ }
        }
        Write-Host ("  кэш каналов скриптов очищен заранее: rsp удалено " + $staleRsp)
    }
}
if ($SkipUnity) {
    Add-Result 'EDITMODE' $false 'НЕ ПРОВЕРЕНО (-SkipUnity) — это не «зелено», а «не смотрели»'
} else {
    $xmlPath = Join-Path $LogsDir 'gate-editmode.xml'
    $logPath = Join-Path $LogsDir 'gate-editmode.log'
    if (Test-Path $xmlPath) { Remove-Item $xmlPath -Force }
    $code = Invoke-Unity @('-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
        '-runTests', '-testPlatform', 'EditMode',
        '-testResults', ('"' + $xmlPath + '"'), '-logFile', ('"' + $logPath + '"')) $TestsTimeoutMinutes
    if ($code -eq 124) {
        Add-Result 'EDITMODE' $false "таймаут $TestsTimeoutMinutes мин"
    } elseif (-not (Test-Path $xmlPath)) {
        $err = (Select-String -Path $logPath -Pattern 'error CS' -ErrorAction SilentlyContinue | Select-Object -First 3 | ForEach-Object { $_.Line.Trim() }) -join ' | '
        Add-Result 'EDITMODE' $false ("XML результатов не создан (компиляция?) $err")
    } else {
        $run = ([xml](Get-Content $xmlPath -Raw)).'test-run'
        $failed = [int]$run.failed
        $total = [int]$run.total
        if ($failed -eq 0 -and $total -ge $MinTests) {
            Add-Result 'EDITMODE' $true "всего=$total прошло=$($run.passed) упало=0 пропущено=$($run.skipped)"
        } else {
            $names = @(([xml](Get-Content $xmlPath -Raw)).SelectNodes("//test-case[@result='Failed']") | ForEach-Object { $_.fullname } | Select-Object -First 5)
            Add-Result 'EDITMODE' $false ("упало=$failed из $total (нужно 0 и тестов >= $MinTests): " + ($names -join '; '))
        }
    }
}

# ================================================================ 6. E2E_PC
Write-Host ""
Write-Host "--- Е2Е в редакторе (раскладка ПК) ---"
if ($SkipUnity) {
    Add-Result 'E2E_PC' $false 'НЕ ПРОВЕРЕНО (-SkipUnity)'
} else {
    $e2eLog = Join-Path $LogsDir 'gate-e2e-pc.log'
    $e2eRep = "C:\Users\$env:USERNAME\AppData\LocalLow\KazistovVv\KazistovVv\KazistovVv\Reports\_e2e_settings.txt"
    if (Test-Path $e2eLog) { Remove-Item $e2eLog -Force }
    if (Test-Path $e2eRep) { Remove-Item $e2eRep -Force }
    $code = Invoke-Unity @('-batchmode', '-projectPath', ('"' + $ProjectPath + '"'),
        '-executeMethod', 'KvE2eSettingsDiag.Run', '-logFile', ('"' + $e2eLog + '"')) 25
    $fails = 0
    $totalChecks = 0
    if (Test-Path $e2eRep) {
        $repText = Get-Content $e2eRep -Raw
        $fails = ([regex]::Matches($repText, '\[FAIL\]')).Count
        $totalChecks = ([regex]::Matches($repText, '\[OK\]')).Count + $fails
    }
    if ($code -eq 0 -and $fails -eq 0 -and $totalChecks -ge 29) {
        Add-Result 'E2E_PC' $true "код 0 · проверок $totalChecks · [FAIL] 0"
    } else {
        Add-Result 'E2E_PC' $false "код $code · проверок $totalChecks · [FAIL] $fails"
    }
}

# ================================================================ 7. MUTATION (негативный контроль)
Write-Host ""
Write-Host "--- негативный контроль (мутации) ---"
if ($SkipUnity) {
    Add-Result 'MUTATION' $false 'НЕ ПРОВЕРЕНО (-SkipUnity)'
} else {
    $mutations = @(
        @{ File = 'Assets\_Project\01_Scripts\Trajectory\PostureSelector.cs';
           From = 'return bestCost < lockedCost * (1.0 - h);';
           To   = 'return bestCost > lockedCost * (1.0 - h);';
           Filter = 'KvP1FixTests'; Name = 'P1-9 гистерезис IK' },
        # ВАЖНО: мутация меняет СМЫСЛ, а не имя. Первая версия переименовывала метод — и это ломало
        # компиляцию («Scripts have compiler errors»), из-за чего тесты не запускались и проверка
        # краснела ПО ВИНЕ САМОЙ ПРОВЕРКИ, а не кода. Правильная мутация сохраняет сигнатуру.
        @{ File = 'Assets\_Project\01_Scripts\Xr\KvXrSettings.cs';
           From = 'return index == ModeMr ? ModeMr : ModeVr;';
           To   = 'return ModeVr;';
           Filter = 'KvVrMrSwitchTests'; Name = 'VR|MR ModeFromIndex' },
        # ДЕФЕКТ A (12.11.2026): пауза превращалась в аварийный останов «watchdog». Мутация
        # ВОЗВРАЩАЕТ дефект — проверка стоящей позы снова идёт через `ApproveStep` с замороженным
        # временем. Если `KvPauseSafetyTests` на сломанном коде останутся зелёными, значит тесты
        # паузы ничего не проверяют, и краснеть на них незачем.
        @{ File = 'Assets\_Project\01_Scripts\Trajectory\TrajectoryExecutor.cs';
           From = 'return !gate.ApproveStandingPose(q, clearance, validator, out reason);';
           To   = 'return !gate.ApproveStep(q, clearance, 0.0, validator, out reason);';
           Filter = 'KvPauseSafetyTests'; Name = 'пауза: стоящая поза без правила времени' },
        # ДЕФЕКТ B (12.11.2026): шапка `SafetyGate` заявляла «порог один», а код пропускал позу с
        # зазором ВДВОЕ меньше объявленного минимума. Мутация возвращает половину порога.
        @{ File = 'Assets\_Project\01_Scripts\Trajectory\SafetyGate.cs';
           From = 'if (clearance < minClearance) { reason = SafetyReason.Clearance;';
           To   = 'if (clearance < minClearance * 0.5f) { reason = SafetyReason.Clearance;';
           Filter = 'KvPauseSafetyTests'; Name = 'зазор: единый порог (без половины)' }
    )
    $mutProblems = @()
    foreach ($mu in $mutations) {
        $path = Join-Path $ProjectPath $mu.File
        if (-not (Test-Path -LiteralPath $path)) { $mutProblems += "$($mu.Name): файл не найден"; continue }
        # РЕЗЕРВНАЯ КОПИЯ — ВНЕ Assets. Иначе Unity импортирует файл рядом с исходником и создаёт
        # на него .meta, а после восстановления в проекте остаётся ОСИРОТЕВШИЙ .meta: дерево
        # грязнится на каждом прогоне гейта (найдено 12.11.2026 — мой же дефект).
        $backup = Join-Path $LogsDir (([System.IO.Path]::GetFileName($path)) + ".gatebak")
        Copy-Item -LiteralPath $path -Destination $backup -Force
        try {
            $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
            if (-not $text.Contains($mu.From)) { $mutProblems += "$($mu.Name): анкер не найден"; continue }
            [System.IO.File]::WriteAllText($path, $text.Replace($mu.From, $mu.To), (New-Object System.Text.UTF8Encoding($false)))

            $mx = Join-Path $LogsDir ("gate-mut-" + ($mu.Filter) + ".xml")
            $ml = Join-Path $LogsDir ("gate-mut-" + ($mu.Filter) + ".log")
            if (Test-Path $mx) { Remove-Item $mx -Force }
            $null = Invoke-Unity @('-batchmode', '-nographics', '-projectPath', ('"' + $ProjectPath + '"'),
                '-runTests', '-testPlatform', 'EditMode', '-testFilter', $mu.Filter,
                '-testResults', ('"' + $mx + '"'), '-logFile', ('"' + $ml + '"')) 20
            if (-not (Test-Path $mx)) { $mutProblems += "$($mu.Name): тесты не запустились"; continue }
            $mrun = ([xml](Get-Content $mx -Raw)).'test-run'
            if ([int]$mrun.failed -lt 1) { $mutProblems += "$($mu.Name): мутация НЕ поймана (тесты зелёные на сломанном коде)" }
        }
        finally {
            Copy-Item -LiteralPath $backup -Destination $path -Force
            Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue
        }
    }
    if ($mutProblems.Count -eq 0) {
        Add-Result 'MUTATION' $true "все мутации пойманы (тесты падают на сломанном коде)"
    } else {
        Add-Result 'MUTATION' $false ($mutProblems -join ' | ')
    }
}

# ================================================================ 8. УСТРОЙСТВА
Write-Host ""
Write-Host "--- устройства ---"
$devices = Get-Devices
Write-Host ("  найдено устройств: " + $devices.Count + " · " + (($devices | ForEach-Object { "$($_.Id) [$($_.Model)]" }) -join ', '))

$tablet = $null; $headset = $null
foreach ($d in $devices) {
    if ($d.Model -match '(?i)redmi|xiaomi|25099|flute|tab') { if (-not $tablet) { $tablet = $d } }
    elseif ($d.Model -match '(?i)pico|a9210|sparrow|quest|vive|pico') { if (-not $headset) { $headset = $d } }
}
if (-not $tablet -and $devices.Count -ge 1) { $tablet = $devices[0] }
if (-not $headset) { $headset = $devices | Where-Object { $_ -ne $tablet } | Select-Object -First 1 }
if ($TabletId) { $tablet = $devices | Where-Object { $_.Id -eq $TabletId } | Select-Object -First 1 }
if ($HeadsetId) { $headset = $devices | Where-Object { $_.Id -eq $HeadsetId } | Select-Object -First 1 }

# ---------------------------------------------------------------- 9. E2E_AR (планшет)
$package = 'com.KazistovVv.app'
if (-not $tablet) {
    Add-Result 'E2E_AR' $false 'планшет не найден в adb — проверить раскладку AR нечем'
} else {
    $arApk = Join-Path $BuildDir 'Builds\KazistovVv-AR-gles3.apk'
    $problems = @()
    if (Test-Path -LiteralPath $arApk) {
        $install = (& $Adb -s $tablet.Id install -r $arApk 2>&1) -join ' '
        if ($install -notmatch 'Success') { $problems += "установка: $install" }
    } else { $problems += 'нет AR-APK для установки' }

    & $Adb -s $tablet.Id shell am force-stop $package 2>&1 | Out-Null
    Start-Sleep -Seconds 3
    & $Adb -s $tablet.Id logcat -c 2>&1 | Out-Null
    & $Adb -s $tablet.Id shell monkey -p $package -c android.intent.category.LAUNCHER 1 2>&1 | Out-Null
    Start-Sleep -Seconds 35

    $lc = (& $Adb -s $tablet.Id logcat -d 2>&1) -join "`n"
    if ($lc -match 'FATAL EXCEPTION') { $problems += 'падение приложения (FATAL EXCEPTION)' }

    $shotBefore = Join-Path $LogsDir 'gate-tablet-before.png'
    $shotAfter = Join-Path $LogsDir 'gate-tablet-after.png'
    $beforeLen = 0
    try { & $Adb -s $tablet.Id exec-out screencap -p > $shotBefore 2>$null; $beforeLen = (Get-Item $shotBefore).Length } catch { }

    # ОТКРЫТЬ НАСТРОЙКИ — ДВУМЯ МАРШРУТАМИ (исправлено 12.11.2026).
    #
    # ЧТО БЫЛО. Один маршрут: меню «Вид» и пункт внизу списка. Вручную он работал, а в автопрогоне
    # после холодного запуска команда не срабатывала вовсе: состояние приложения другое (сохранённая
    # раскладка, стартовый экран), и те же координаты попадают в другое место.
    #
    # ЧТО СТАЛО. Два независимых маршрута, признак успеха — строка команды в журнале. В отчёт
    # попадает и список всех команд, которые реально нажались: при провале видно, КУДА попали тапы.
    $openedBy = $null
    $firedCommands = @()

    for ($attempt = 0; $attempt -lt 2 -and -not $openedBy; $attempt++) {
        & $Adb -s $tablet.Id shell input tap 660 48 2>&1 | Out-Null
        Start-Sleep -Seconds 2
        & $Adb -s $tablet.Id shell input tap 430 (1435 - $attempt * 40) 2>&1 | Out-Null
        Start-Sleep -Seconds 3
        $probe = (& $Adb -s $tablet.Id logcat -d 2>&1) -join "`n"
        if ($probe -match 'view\.panel\.settings|ui\.settings') { $openedBy = 'меню «Вид»' }
    }

    if (-not $openedBy) {
        foreach ($x in 1450,1500,1520,1560,1600) {
            & $Adb -s $tablet.Id shell input tap $x 139 2>&1 | Out-Null
            Start-Sleep -Seconds 2
            $probe2 = (& $Adb -s $tablet.Id logcat -d 2>&1) -join "`n"
            if ($probe2 -match 'ui\.settings') { $openedBy = ('кнопка тулбара x=' + $x); break }
        }
    }

    Start-Sleep -Seconds 3
    $afterLen = 0
    try { & $Adb -s $tablet.Id exec-out screencap -p > $shotAfter 2>$null; $afterLen = (Get-Item $shotAfter).Length } catch { }

    $lc2 = (& $Adb -s $tablet.Id logcat -d 2>&1) -join "`n"
    $cmdFired = ($null -ne $openedBy)
    $firedCommands = @([regex]::Matches($lc2, 'выбрана «([^»]+)»') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)


    # СЛОВАРИ ЯЗЫКОВ НА УСТРОЙСТВЕ (найдено 12.11.2026). В редакторе журнал содержит
    # «[KvLoc] словарь de прочитан: 535 строк», а в журнале ПЛАНШЕТА таких строк НЕТ вообще:
    # json-словари из StreamingAssets на Android не читаются, и переключение языка на устройстве
    # не работает (в списке пять языков показывали «· 0»). Требуется: не меньше двух словарей,
    # суммарно не меньше 100 строк.
    $dictRead = @([regex]::Matches($lc2, 'словарь\s+\S+\s+прочитан:\s+(\d+)\s+строк'))
    $dictTotal = 0
    foreach ($d in $dictRead) { $dictTotal += [int]$d.Groups[1].Value }
    $localeOk = ($dictRead.Count -ge 2 -and $dictTotal -ge 100)
    # КРИТЕРИЙ ВЕРДИКТА — ГЕОМЕТРИЯ ИЗ ЖУРНАЛА, А НЕ РАЗМЕР PNG (исправлено 12.11.2026).
    # Размер снимка — плохой признак: экран занят ЖИВОЙ камерой, кадр меняется сам, и панель
    # теряется в этом шуме (измерено: 0,3 % при РЕАЛЬНО показанной панели). Приложение печатает
    # сторону, толщину, размер и состояние канваса — по этим числам и выносится решение.
    # Пиксельная разница остаётся в отчёте как дополнительное число.
    $changed = $false
    $geomText = 'строки о показе панели нет'
    if ($beforeLen -gt 0 -and $afterLen -gt 0) {
        $delta = [math]::Abs($afterLen - $beforeLen) / [double]$beforeLen
        $geom = [regex]::Match($lc2, 'ПАНЕЛИ: панель «settings» показана[^\r\n]*')
        if ($geom.Success) {
        if (-not $localeOk) { $problems += ('словари языков на устройстве НЕ прочитаны: строк "словарь … прочитан" = ' + $dictRead.Count + ', суммарно строк ' + $dictTotal) }
            $geomText = $geom.Value
            $mSize = [regex]::Match($geom.Value, 'размер\s+(\d+)×(\d+)')
            $sizeOk = $mSize.Success -and ([int]$mSize.Groups[1].Value -gt 200) -and ([int]$mSize.Groups[2].Value -gt 100)
            $canvasOk = $geom.Value -match 'канвас[^·]*включён'
            $shownOk = $geom.Value -match 'включена да'
            $changed = $sizeOk -and $canvasOk -and $shownOk
        }
        if (-not $cmdFired) { $problems += ('команда открытия настроек не сработала; нажались: ' + (($firedCommands -join ', '))) }
        if (-not $changed) { $problems += ('панель не показана: ' + $geomText + ' · изменение снимка ' + [math]::Round($delta*100,2) + '%') }
    } else {
        $problems += 'не удалось снять экран планшета'
    }

    if ($problems.Count -eq 0) {
        Add-Result 'E2E_AR' $true ("панель показана через " + $openedBy + " · словарей " + $dictRead.Count + " (" + $dictTotal + " строк) · " + $geomText)
    } else {
        Add-Result 'E2E_AR' $false ($problems -join ' | ')
    }
}

# ---------------------------------------------------------------- 10. HEADSET (шлем)
if (-not $headset) {
    Add-Result 'HEADSET' $false 'шлем не найден в adb — VR/MR проверить нечем'
} else {
    $vrApk = Join-Path $BuildDir 'Builds\KazistovVv-VR-vulkan.apk'
    $problems = @()
    if (Test-Path -LiteralPath $vrApk) {
        $install = (& $Adb -s $headset.Id install -r $vrApk 2>&1) -join ' '
        if ($install -notmatch 'Success|already') { $problems += "установка: $install" }
    } else { $problems += 'нет VR-APK для установки' }

    & $Adb -s $headset.Id shell am force-stop $package 2>&1 | Out-Null
    Start-Sleep -Seconds 3
    & $Adb -s $headset.Id logcat -c 2>&1 | Out-Null
    & $Adb -s $headset.Id shell monkey -p $package -c android.intent.category.LAUNCHER 1 2>&1 | Out-Null
    Start-Sleep -Seconds 40

    $hlc = (& $Adb -s $headset.Id logcat -d 2>&1) -join "`n"
    if ($hlc -match 'FATAL EXCEPTION') { $problems += 'падение приложения (FATAL EXCEPTION)' }
    if ($hlc -notmatch '\[KvEnv\]') { $problems += 'нет строки [KvEnv] — приложение не стартовало' }

    # ЗАСТРЕВАНИЕ НА РАЗРЕШЕНИИ КАМЕРЫ — ОТДЕЛЬНЫЙ ПУНКТ.
    # Обнаружено 12.11.2026 на PICO A9210: приложение уходило в «[KvAr] КАМЕРА: ждём разрешение»
    # и висело вечно — AR-гейт срабатывал в VR-режиме, а диалог в шлеме нажать нечем.
    # Исправлено и ПРОВЕРЕНО 12.11.2026: сообщений «ждём разрешение» ноль, сцена XrMain грузится.
    $permWaits = ([regex]::Matches($hlc, 'ждём разрешение')).Count
    if ($permWaits -ge 2) {
        $problems += ("ВИСИТ на разрешении камеры (" + $permWaits + " сообщений «ждём разрешение») — AR-гейт сработал в VR-режиме")
    }

    # ПРИЗНАКИ РАБОТАЮЩЕГО VR: названный режим И загруженная VR-сцена.
    $vrMode = $hlc -match 'ВИРТУАЛЬНАЯ РЕАЛЬНОСТЬ|СМЕШАННАЯ РЕАЛЬНОСТЬ'
    $vrScene = $hlc -match 'ЗАГРУЗКА: сцена «XrMain»|сцена «XrMain»'
    if (-not $vrMode) { $problems += 'режим (VR/MR) в журнале не назван — фактический режим неизвестен' }
    if (-not $vrScene) { $problems += 'VR-сцена XrMain не загрузилась — приложение не дошло до работы' }

    if ($problems.Count -eq 0) {
    Add-Result 'HEADSET' $true ('шлем: запуск без падений, [KvEnv] есть, режим назван, VR-сцена XrMain загружена, зависания на разрешении камеры нет · ВНУТРИ шлема панель и переключатель VR|MR этой проверкой НЕ подтверждаются — нужен человек в шлеме')
    } else {
        Add-Result 'HEADSET' $false ($problems -join ' | ')
    }
}

# ================================================================ 10. UI_SWEEP (самопрогон интерфейса)
#
# ВЛАДЕЛЕЦ: «главное — чтобы все кнопочки были заполнены функционалом». Проверка `UI_STUBS` выше
# ловит ЗАГЛУШКИ в реестре команд, но ничего не говорит о том, что кнопка реально работает в ЖИВОМ
# интерфейсе: элемент может быть нарисован, зарегистрирован и всё равно не делать ничего.
#
# Поэтому приложение само обходит свои элементы (кнопка, переключатель, ползунок, список, поле),
# нажимает их настоящим путём uGUI и печатает в журнал строку на каждый: сработал или нет. Здесь
# прогон запускается на ТРЁХ раскладках — планшет (AR), шлем (VR) и шлем (MR) — и судится по журналу.
#
# ЧЕСТНО О ГРАНИЦАХ: самопрогон подтверждает, что элемент ДЕЛАЕТ РАБОТУ. Он не подтверждает, что
# нарисовано красиво и что в шлеме текст читается, — это остаётся человеку.
Write-Host ""
Write-Host "--- самопрогон интерфейса на устройствах ---"
$sweepRunner = Join-Path $ProjectPath 'Tools\ui-sweep-device.ps1'
$sweepProblems = @()
$sweepReport = @()
$sweepLayouts = @()
if (-not $tablet) { $sweepProblems += 'планшет не найден — раскладку AR проверить нечем' }
if (-not $headset) { $sweepProblems += 'шлем не найден — VR и MR проверить нечем' }
if (-not (Test-Path -LiteralPath $sweepRunner)) { $sweepProblems += 'нет Tools\ui-sweep-device.ps1' }

if ($sweepProblems.Count -eq 0) {
    $plans = @()
    $plans += @{ Id = $tablet.Id; Mode = 'auto'; Name = 'AR (планшет)' }
    $plans += @{ Id = $headset.Id; Mode = 'vr'; Name = 'VR (шлем)' }
    $plans += @{ Id = $headset.Id; Mode = 'mr'; Name = 'MR (шлем)' }
    foreach ($plan in $plans) {
        Write-Host ("  прогон: " + $plan.Name + " · устройство " + $plan.Id)
        $logFile = Join-Path $LogsDir ('device-' + $plan.Id + '-' + $plan.Mode + '.logcat.txt')
        if (Test-Path $logFile) { Remove-Item $logFile -Force }
        # ЧИСТОЕ СОСТОЯНИЕ ОБЯЗАТЕЛЬНО: раскладка панелей хранится в PlayerPrefs, и без сброса вердикт
# зависел бы от предыдущего прогона (наблюдалось: то «исчезло 0», то «исчезло 41 из 140»).
        # ПОВТОР ПРИ СИСТЕМНОМ УБИЙСТВЕ (добавлено тестировщиком 12.11.2026).
        #
        # ЧТО БЫЛО. На планшете Android дважды убивал приложение посреди обхода (нехватка памяти),
        # и прогон объявлялся не пройденным, хотя код был не при чём. Классический ложный вердикт.
        # ЧТО СТАЛО. Если процесса нет и итоговой строки нет — это состояние СТЕНДА, а не приложения:
        # прогон повторяется один раз. Второе убийство подряд уже считается результатом и честно
        # попадает в красное: два раза подряд память не хватает — это факт о приложении и устройстве.
        $out = & pwsh -NoProfile -File $sweepRunner -DeviceId $plan.Id -Mode $plan.Mode -LogOut $logFile -CleanState -TimeoutSeconds 600 2>&1
        $code = $LASTEXITCODE
        $killedBySystem = ($out | Where-Object { $_ -match "процесс завершился|экран перехватило" })
        $hasTotal = ($out | Where-Object { $_ -match "ИТОГ:" })
        if ($code -ne 0 -and $killedBySystem -and -not $hasTotal) {
            Write-Host "    повтор: приложение убила система (память устройства), не код — прогоняю ещё раз"
            $out = & pwsh -NoProfile -File $sweepRunner -DeviceId $plan.Id -Mode $plan.Mode -LogOut $logFile -CleanState -TimeoutSeconds 600 2>&1
            $code = $LASTEXITCODE
            $hasTotal = ($out | Where-Object { $_ -match "ИТОГ:" })
        }
        $code = $LASTEXITCODE
        $out | Where-Object { $_ -match 'элементов|ИТОГ|ВЕРДИКТ|САМОПРОГОН|  - ' } | Select-Object -First 8 | ForEach-Object { Write-Host ("    " + $_) }
        $layoutLine = @($out | Where-Object { $_ -match 'ИТОГ:' } | Select-Object -Last 1)
        if ($layoutLine.Count -gt 0) { $sweepReport += ($plan.Name + ': ' + $layoutLine[0].Trim()) }
        else { $sweepReport += ($plan.Name + ': итога нет') }
        if ($code -ne 0) { $sweepProblems += ($plan.Name + ': прогон не пройден (код ' + $code + ')') }
    }
}

if ($sweepProblems.Count -eq 0) {
    Add-Result 'UI_SWEEP' $true (($sweepReport -join ' · ') + ' · каждая кнопка проверена НАЖАТИЕМ в живом интерфейсе')
} else {
    Add-Result 'UI_SWEEP' $false (($sweepProblems + $sweepReport) -join ' | ')
}

# ================================================================ 11. REAL_TABLE (реальный стол)
#
# ЗАДАЧА ВЛАДЕЛЬЦА: автоматически распознавать РЕАЛЬНЫЙ (не виртуальный) стол в AR и MR.
#
# ПРОВЕРКА СОСТОИТ ИЗ ДВУХ ЧАСТЕЙ, и обе обязательны:
#   1) ПРАВИЛА доказаны тестами без устройств (`KvRealTableTests`): пол не считается столом, семантика
#      сильнее геометрии, наклон/площадь/уровень глаз — отказы с числами. Это проверяется по XML
#      прогона EditMode, то есть по факту, а не по обещанию.
#   2) КОНВЕЙЕР отработал на устройствах и напечатал ВЕРДИКТ С ЧИСЛАМИ (планшет — AR-плоскости,
#      шлем — разметка комнаты PICO). Журналы берутся из прогонов UI_SWEEP, отдельно устройства не
#      дёргаются.
#
# ЧЕСТНО О ГРАНИЦАХ: гейт не может потребовать, чтобы перед камерой лежал настоящий стол. Поэтому он
# требует не «стол найден», а ЧЕСТНЫЙ вердикт с числами: «стол найден · площадь … · высота над полом …»
# либо «пол (не стол) · площадь …» либо объяснение, что комната не размечена. Врать про стол нельзя —
# именно это и было дефектом.
Write-Host ""
Write-Host "--- распознавание реального стола ---"
$realProblems = @()
$realReport = @()

$rtTests = 0
$emXml = Join-Path $LogsDir 'gate-editmode.xml'
if (Test-Path -LiteralPath $emXml) {
    try {
        $emDoc = [xml](Get-Content $emXml -Raw)
        $rtTests = @($emDoc.SelectNodes('//test-case') | Where-Object { $_.fullname -match 'KvRealTableTests' }).Count
    } catch { $realProblems += 'XML прогона EditMode не читается' }
}
if ($rtTests -lt 15) { $realProblems += ("правил распознавания стола в наборе мало: тестов " + $rtTests + " (нужно не меньше 15)") }
else { $realReport += ("правила доказаны тестами: " + $rtTests) }

$tabletLog = if ($tablet) { Join-Path $LogsDir ('device-' + $tablet.Id + '-auto.logcat.txt') } else { $null }
$headsetLog = if ($headset) { Join-Path $LogsDir ('device-' + $headset.Id + '-mr.logcat.txt') } else { $null }

# ПЛАНШЕТ (AR) И ШЛЕМ (MR): ДОКАЗАТЕЛЬСТВО С УСТРОЙСТВ.
#
# ЧТО БЫЛО. Проверка читала журналы, оставшиеся от прогонов интерфейса. Это было ненадёжно, и вот
# почему: (1) прогон интерфейса переключает режим НА ЛЕТУ, а конвейер реальных поверхностей шлема
# поднимается только при СТАРТЕ в MR — в таких журналах строк о столе нет вовсе; (2) на планшете
# прогон короткий, и диагноз «плоскостей нет» мог не успеть напечататься. В итоге проверка краснела
# не из-за приложения, а из-за способа измерения.
# ЧТО СТАЛО. Отдельный замер `Tools\real-table-device.ps1`: он сам делает чистый старт (для шлема —
# сначала сохраняет режим MR, потом перезапускает приложение в MR) и требует ВЕРДИКТ С ЧИСЛАМИ:
#   планшет — «стол найден · площадь … · высота над полом …» / «пол (не стол) · площадь …» /
#             «поверхностей нет — физического стола в кадре не было» (с числами);
#   шлем    — «РЕАЛЬНЫЙ СТОЛ: запрос выполнен за … с · столов N · пол …» / объяснение про разметку.
# Молчание не принимается: именно молчание и было дефектом до правки.
$realProblems = @()
$realReport = @()

$rtTests = 0
$emXml = Join-Path $LogsDir 'gate-editmode.xml'
if (Test-Path -LiteralPath $emXml) {
    try {
        $emDoc = [xml](Get-Content $emXml -Raw)
        $rtTests = @($emDoc.SelectNodes('//test-case') | Where-Object { $_.fullname -match 'KvRealTableTests' }).Count
    } catch { $realProblems += 'XML прогона EditMode не читается' }
}
if ($rtTests -lt 15) { $realProblems += ("правил распознавания стола в наборе мало: тестов " + $rtTests + " (нужно не меньше 15)") }
else { $realReport += ("правила доказаны тестами: " + $rtTests) }

$rtRunner = Join-Path $ProjectPath 'Tools\real-table-device.ps1'
if (-not (Test-Path -LiteralPath $rtRunner)) {
    $realProblems += 'нет Tools\real-table-device.ps1 — доказывать распознавание нечем'
} else {
    if ($tablet) {
        $tabLog = Join-Path $LogsDir ('real-table-' + $tablet.Id + '-auto.logcat.txt')
        $out = & pwsh -NoProfile -File $rtRunner -DeviceId $tablet.Id -Mode auto -LogOut $tabLog 2>&1
        ($out | Where-Object { $_ -match 'ВЕРДИКТ ПОЛУЧЕН|  - ' } | Select-Object -First 4) | ForEach-Object { Write-Host ("    " + $_) }
        if ($LASTEXITCODE -eq 0) { $realReport += ($out | Where-Object { $_ -match 'ВЕРДИКТ ПОЛУЧЕН' } | Select-Object -Last 1) }
        else { $realProblems += 'планшет: вердикта о поверхностях нет' }
    } else { $realProblems += 'планшет не найден — распознавание в AR не проверить' }

    if ($headset) {
        $hsLog = Join-Path $LogsDir ('real-table-' + $headset.Id + '-mr.logcat.txt')
        $out2 = & pwsh -NoProfile -File $rtRunner -DeviceId $headset.Id -Mode mr -LogOut $hsLog 2>&1
        ($out2 | Where-Object { $_ -match 'ВЕРДИКТ ПОЛУЧЕН|  - ' } | Select-Object -First 4) | ForEach-Object { Write-Host ("    " + $_) }
        if ($LASTEXITCODE -eq 0) { $realReport += ($out2 | Where-Object { $_ -match 'ВЕРДИКТ ПОЛУЧЕН' } | Select-Object -Last 1) }
        else { $realProblems += 'шлем: вердикта о реальном столе нет' }
    } else { $realProblems += 'шлем не найден — распознавание в MR не проверить' }
}

if ($realProblems.Count -eq 0) {
    Add-Result 'REAL_TABLE' $true (($realReport | Where-Object { $_ }) -join ' · ')
} else {
    Add-Result 'REAL_TABLE' $false (($realProblems + ($realReport | Where-Object { $_ })) -join ' | ')
}


# ================================================================ ИТОГ + СЧЁТЧИК ПОВТОРОВ
$red = @($results | Where-Object { -not $_.Ok })
$redCodes = @($red | ForEach-Object { $_.Code })

Write-Host ""
Write-Host "########################################################################"
Write-Host "#  ИТОГ"
Write-Host "########################################################################"
Write-Host ("проверок: {0} · зелёных: {1} · КРАСНЫХ: {2}" -f $results.Count, ($results.Count - $red.Count), $red.Count)
if ($red.Count -gt 0) {
    Write-Host ""
    Write-Host "КРАСНОЕ (это задачи разработчику):"
    foreach ($r in $red) { Write-Host ("  - {0}: {1}" -f $r.Code, $r.Detail) }
}

# --- счётчик повторов: один и тот же пункт 3 прогона подряд = стоп
$repeatStop = $false
$repeatReport = @()
if (-not $NoState) {
    $state = $null
    if (Test-Path -LiteralPath $StateFile) {
        try { $state = Get-Content -LiteralPath $StateFile -Raw | ConvertFrom-Json } catch { $state = $null }
    }
    if ($null -eq $state) { $state = [pscustomobject]@{ Counts = [pscustomobject]@{} ; Runs = 0 } }
    if ($null -eq $state.PSObject.Properties['Counts'] -or $null -eq $state.Counts) {
        $state | Add-Member -NotePropertyName Counts -NotePropertyValue ([pscustomobject]@{}) -Force
    }
    $newCounts = [ordered]@{}
    foreach ($code in 'EDITMODE','REPO_GUARDS','SECRET','GIT_ASSETS','APK_NAMES','APK_FRESH','UI_STUBS','P4_3_RATCHET','E2E_PC','E2E_AR','MUTATION','HEADSET','UI_SWEEP','REAL_TABLE') {
        $prev = 0
        $prop = $state.Counts.PSObject.Properties[$code]
        if ($prop) { $prev = [int]$prop.Value }
        if ($redCodes -contains $code) { $newCounts[$code] = $prev + 1 } else { $newCounts[$code] = 0 }
    }
    $state.Counts = [pscustomobject]$newCounts
    $state | Add-Member -NotePropertyName Runs -NotePropertyValue ([int]$state.Runs + 1) -Force
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $StateFile -Encoding UTF8

    foreach ($code in $newCounts.Keys) {
        if ($newCounts[$code] -ge 3) {
            $repeatStop = $true
            $repeatReport += ("{0} — красный {1} прогона подряд" -f $code, $newCounts[$code])
        }
    }
    if ($repeatStop) {
        Write-Host ""
        Write-Host "!!! СЧЁТЧИК ПОВТОРОВ: СТОП !!!" -ForegroundColor Red
        foreach ($line in $repeatReport) { Write-Host ("  {0}" -f $line) -ForegroundColor Red }
        Write-Host "  Один и тот же пункт не закрывается третьим заходом — это не «разработчик старается»," -ForegroundColor Red
        Write-Host "  а дефект, который не решается этим путём. Нужен отчёт владельцу, а не четвёртый круг." -ForegroundColor Red
    }
}

Write-Host ""
Write-Host ("счётчик повторов: " + $StateFile)
Write-Host ""

if ($red.Count -eq 0) {
    Write-Host "ВЕРДИКТ: ВСЁ ЗЕЛЁНОЕ — полный прогон инженера-тестировщика пройден идеально." -ForegroundColor Green
    exit 0
}
if ($repeatStop) {
    Write-Host "ВЕРДИКТ: СТОП по счётчику повторов (код 3)." -ForegroundColor Red
    exit 3
}
Write-Host "ВЕРДИКТ: ЕСТЬ КРАСНОЕ (код 1) — работа продолжается." -ForegroundColor Yellow
exit 1
