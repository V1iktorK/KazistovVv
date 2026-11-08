<#
.SYNOPSIS
    СТОРОЖА РЕПОЗИТОРИЯ: дешёвые проверки, которые не требуют Unity и ловят дефекты,
    из-за которых «зелёная сборка» ничего не значит.

.DESCRIPTION
    Каждая проверка отвечает на вопрос, который уже один раз стоил проекту времени:

      1. ЛИЦЕНЗИОННЫЕ И СБОРОЧНЫЕ АРТЕФАКТЫ В ИНДЕКСЕ. APK, Library, Logs, Builds в git —
         это репозиторий на сотни мегабайт и «битые» клоны у тестировщика.
      2. ФАЙЛЫ-ОСТАТКИ ВНУТРИ Assets. Файл вида `Scene.unity.dsh_backup_s22_lockcursor`
         Unity импортирует как АССЕТ: он попадает в базу, в поиск и в сборку.
      3. .META БЕЗ АССЕТА И АССЕТ БЕЗ .META. Потерянная .meta означает «новая случайная
         ссылка»: сцены и префабы теряют компоненты, и это видно только на устройстве.
      4. НЕ-ASCII В ПУТЯХ КОДА. Сборка Android падает на кириллице в пути (дефект A19/E-01);
         как минимум пути внутри репозитория обязаны быть ASCII.
      5. ГИГАНТСКИЕ ФАЙЛЫ В ИНДЕКСЕ (> 10 МБ) — предупреждение: их место в LFS.
      6. СЕКРЕТЫ: ключи подписи и файлы с паролями не должны быть в индексе.

    Возвращает 0, если проверки пройдены; 1, если есть провал.

.PARAMETER ProjectPath
    Корень Unity-проекта. По умолчанию — родитель каталога со скриптом (…\KazistovVv\Tools →
    …\KazistovVv).

.PARAMETER BigFileMb
    Порог «гигантского файла» в мегабайтах. По умолчанию 10.

.EXAMPLE
    pwsh -File Tools\repo-guards.ps1

.EXAMPLE
    pwsh -File Tools\repo-guards.ps1 -ProjectPath C:\KazistovVv_Test
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = "",
    [int]$BigFileMb = 10
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Split-Path -Parent $PSScriptRoot
}
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path

$failures = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Fail([string]$text) { $failures.Add($text); Write-Host "::error::$text" }
function Warn([string]$text) { $warnings.Add($text); Write-Host "::warning::$text" }
function Ok([string]$text)   { Write-Host "  OK  $text" }
function Head([string]$text) { Write-Host ""; Write-Host "=== $text ===" }

Push-Location -LiteralPath $ProjectPath
try {
    $isRepo = Test-Path -LiteralPath (Join-Path $ProjectPath '.git')
    if (-not $isRepo) {
        Warn "это не git-репозиторий — проверки индекса пропущены"
    }

    $tracked = @()
    if ($isRepo) { $tracked = @(git ls-files) }

    # ---------------------------------------------------------------- 1. артефакты в индексе
    Head "1. Сборочные и служебные артефакты в индексе git"
    $forbiddenPatterns = @(
        '^Library/', '^Logs/', '^Temp/', '^obj/', '^Builds/', '^Build/',
        '^UserSettings/', '^\.utmp/', '\.apk$', '\.aab$', '\.unitypackage$'
    )
    $bad = @($tracked | Where-Object {
        $path = $_
        ($forbiddenPatterns | Where-Object { $path -match $_ }).Count -gt 0
    })
    if ($bad.Count -gt 0) {
        foreach ($b in $bad | Select-Object -First 20) { Fail "в индексе артефакт сборки: $b" }
        if ($bad.Count -gt 20) { Fail "… и ещё $($bad.Count - 20) файлов того же рода" }
    } else { Ok "артефактов сборки в индексе нет" }

    # ---------------------------------------------------------------- 2. остатки в Assets
    Head "2. Файлы-остатки внутри Assets"
    $strayPatterns = @(
        '\.bak$', '\.bak-', '\.orig$', '\.rej$', '\.old$', '\.tmp$',
        '\.dsh_backup', '\.dsh_', '~$', '\.autosave', '\$RECYCLE'
    )
    $stray = @(Get-ChildItem -LiteralPath (Join-Path $ProjectPath 'Assets') -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $n = $_.Name
            ($strayPatterns | Where-Object { $n -match $_ }).Count -gt 0
        })
    if ($stray.Count -gt 0) {
        foreach ($s in $stray | Select-Object -First 20) {
            $rel = $s.FullName.Substring($ProjectPath.Length + 1)
            Fail "в Assets лежит остаток (Unity импортирует его как ассет): $rel"
        }
    } else { Ok "остатков в Assets нет" }

    # ---------------------------------------------------------------- 3. .meta в обе стороны
    Head "3. Соответствие ассетов и .meta"

    # ПАПКИ, КОТОРЫЕ UNITY НЕ ИМПОРТИРУЕТ. Имена, оканчивающиеся на `~`, и папки, начинающиеся
    # с точки, Unity пропускает целиком — и .meta им не нужна (и вредна). Без этого исключения
    # проверка ругалась на `Assets/XR/APILayers~/…` — то есть на КОРРЕКТНЫЙ проект, и её быстро
    # начали бы игнорировать.
    function Test-UnityIgnored([string]$relative) {
        foreach ($segment in ($relative -split '[\\/]')) {
            if ($segment.EndsWith('~')) { return $true }
            if ($segment.StartsWith('.')) { return $true }
        }
        return $false
    }

    $assetsRoot = Join-Path $ProjectPath 'Assets'
    if (Test-Path -LiteralPath $assetsRoot) {
        $all = @(Get-ChildItem -LiteralPath $assetsRoot -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Name -notlike '*.meta' -and
                -not (Test-UnityIgnored $_.FullName.Substring($assetsRoot.Length + 1))
            })
        $missingMeta = @($all | Where-Object { -not (Test-Path -LiteralPath ($_.FullName + '.meta')) })
        # Каталоги верхнего уровня Unity тоже требует с .meta, поэтому проверяются вместе с файлами.
        if ($missingMeta.Count -gt 0) {
            foreach ($m in $missingMeta | Select-Object -First 15) {
                $rel = $m.FullName.Substring($ProjectPath.Length + 1)
                Warn "нет .meta у ассета (Unity сгенерирует новую ссылку): $rel"
            }
            if ($missingMeta.Count -gt 15) { Warn "… и ещё $($missingMeta.Count - 15) ассетов без .meta" }
        } else { Ok "у всех ассетов есть .meta" }

        $orphanMeta = @(Get-ChildItem -LiteralPath $assetsRoot -Recurse -Force -Filter '*.meta' -ErrorAction SilentlyContinue |
            Where-Object {
                -not (Test-UnityIgnored $_.FullName.Substring($assetsRoot.Length + 1)) -and
                -not (Test-Path -LiteralPath ($_.FullName.Substring(0, $_.FullName.Length - 5)))
            })
        if ($orphanMeta.Count -gt 0) {
            foreach ($m in $orphanMeta | Select-Object -First 15) {
                $rel = $m.FullName.Substring($ProjectPath.Length + 1)
                Fail ".meta без ассета (потерянная ссылка): $rel"
            }
        } else { Ok "висячих .meta нет" }
    } else { Warn "нет папки Assets — проверка .meta пропущена" }

    # ---------------------------------------------------------------- 4. не-ASCII в путях кода
    Head "4. Не-ASCII в путях исходников и настроек"

    # ВАЖНО: проверяются ТОЛЬКО пути, которые участвуют в сборке. Документация и заметки на
    # русском — это норма и не дефект; предупреждать о `Docs/ОТЧЁТ.md` значит производить шум,
    # из-за которого настоящую находку перестают замечать.
    $buildExtensions = @('.cs', '.asmdef', '.asmref', '.ps1', '.asset', '.unity', '.prefab',
                         '.json', '.yml', '.yaml', '.shader', '.hlsl', '.cginc', '.gradle', '.xml')
    $codeDirs = @('Assets', 'ProjectSettings', 'Packages', 'Tools')
    $nonAscii = New-Object System.Collections.Generic.List[string]
    foreach ($dir in $codeDirs) {
        $full = Join-Path $ProjectPath $dir
        if (-not (Test-Path -LiteralPath $full)) { continue }
        Get-ChildItem -LiteralPath $full -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            $rel = $_.FullName.Substring($ProjectPath.Length + 1)
            if ($rel -notmatch '[^\u0000-\u007F]') { return }
            if ($buildExtensions -notcontains $_.Extension.ToLowerInvariant()) { return }
            $nonAscii.Add($rel)
        }
    }
    if ($nonAscii.Count -gt 0) {
        foreach ($p in $nonAscii | Select-Object -First 15) {
            Warn "не-ASCII в пути (Android-сборка падает на кириллице): $p"
        }
        if ($nonAscii.Count -gt 15) { Warn "… и ещё $($nonAscii.Count - 15) путей" }
    } else { Ok "пути исходников ASCII" }

    $cyrProjectPath = if ($ProjectPath -match '[^\u0000-\u007F]') { $true } else { $false }
    if ($cyrProjectPath) {
        Warn ("путь к САМОМУ проекту содержит не-ASCII: $ProjectPath — Android-сборка на таком пути " +
              "падает (дефект A19/E-01). Для сборки используйте ASCII-путь, например C:\KazistovVv_Build")
    }

    # ---------------------------------------------------------------- 5. гигантские файлы
    Head "5. Гигантские файлы в индексе (> $BigFileMb МБ)"
    $big = @()
    foreach ($f in $tracked) {
        $full = Join-Path $ProjectPath ($f -replace '/', [System.IO.Path]::DirectorySeparatorChar)
        # ЧТЕНИЕ РАЗМЕРА — В try. Индекс git может содержать имя, которого уже нет на диске
        # (файл переименован, рабочее дерево грязное), и `Get-Item` на таком имени валил ВЕСЬ
        # скрипт: одна проверка ломала остальные. Это и случилось на первом прогоне
        # (`Assets\CompositionLayers.meta`). Отсутствующий файл — не повод падать: его размер
        # неизвестен, а не «бесконечен».
        try {
            if (-not (Test-Path -LiteralPath $full)) { continue }
            $len = (Get-Item -LiteralPath $full -Force -ErrorAction Stop).Length
        } catch {
            continue
        }
        if ($len -gt ($BigFileMb * 1MB)) { $big += , @($f, [math]::Round($len / 1MB, 1)) }
    }
    if ($big.Count -gt 0) {
        foreach ($b in $big | Sort-Object -Property { $_[1] } -Descending | Select-Object -First 15) {
            Warn ("в индексе файл {0} МБ: {1} — место таких файлов в Git LFS" -f $b[1], $b[0])
        }
    } else { Ok "файлов больше $BigFileMb МБ в индексе нет" }

    # ---------------------------------------------------------------- 6. секреты
    Head "6. Ключи подписи и пароли"
    $secretTracked = @($tracked | Where-Object {
        $_ -match '\.(keystore|jks|p12|pfx|pem|key)$' -or
        $_ -match '(?i)(passwo?rd|secret|credential)s?\.(txt|json|xml|ya?ml)$'
    })
    if ($secretTracked.Count -gt 0) {
        foreach ($s in $secretTracked) { Fail "в индексе файл, похожий на секрет: $s (подпись нельзя хранить в git)" }
    } else { Ok "похожих на секреты файлов в индексе нет" }

    # ПАРОЛЬ ПОДПИСИ В ТЕКСТЕ ОТСЛЕЖИВАЕМОГО ФАЙЛА (добавлено 11.11.2026).
    #
    # ЗАЧЕМ. Пароль ключа подписи лежал не только в коде (`KvAndroidSigning`), но и в четырёх
    # ДОКУМЕНТАХ — в примерах вида `$env:KAZISTOVVV_KEYPASS="…"`. Проверка имён файлов такое не
    # видит: файл называется .md и секретом не выглядит. Теперь ищется ПРИСВОЕНИЕ НЕПУСТОГО
    # значения переменной пароля, а не имя файла.
    $passwordAssignments = New-Object System.Collections.Generic.List[string]
    foreach ($f in $tracked) {
        if ($f -notmatch '\.(md|cs|ps1|json|ya?ml|txt|asset)$') { continue }
        $full = Join-Path $ProjectPath ($f -replace '/', [System.IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $full)) { continue }
        try {
            $lineNo = 0
            foreach ($line in [System.IO.File]::ReadLines($full)) {
                $lineNo++
                if ($line -match 'KAZISTOVVV_KEYPASS\s*=\s*["'']([^"''<>]{4,})["'']') {
                    $passwordAssignments.Add("${f}:${lineNo}")
                }
            }
        } catch { continue }
    }
    if ($passwordAssignments.Count -gt 0) {
        foreach ($a in $passwordAssignments) {
            Fail "пароль ключа подписи присвоен в тексте: $a (секрет нельзя хранить в репозитории)"
        }
    } else { Ok "присвоений пароля ключа в тексте нет" }
}
finally {
    Pop-Location
}

# ---------------------------------------------------------------- 7. остатки XR-симуляции
#
# ЗАЧЕМ (задача P0-11, 11.11.2026). Сборщик AR Foundation на время сборки переносит
# `XRSimulationPreferences` и `XRSimulationRuntimeSettings` в `Assets/XR/Temp`, а в
# `OnPostprocessBuild` возвращает их назад. Если сборка или редактор прервались, ассеты остаются
# в Temp, и СЛЕДУЮЩАЯ сборка не может их перенести:
#     Failed to move the asset (…) to a temporary location: … Destination path name does not exist
# Каталог `Assets/XR/Temp/` внесён в `.gitignore`, поэтому git такие остатки НЕ видит — их
# приходится ловить сторожем, иначе дефект возвращается молча.
Head "7. Остатки ассетов XR-симуляции (Assets/XR/Temp)"
$xrTemp = Join-Path $ProjectPath 'Assets/XR/Temp'
if (Test-Path -LiteralPath $xrTemp) {
    $xrLeftovers = @(Get-ChildItem -LiteralPath $xrTemp -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -ne '.meta' })
    if ($xrLeftovers.Count -gt 0) {
        foreach ($l in $xrLeftovers) {
            Fail ("остаток прерванной сборки: Assets/XR/Temp/{0} — следующая сборка не сможет перенести ассет (P0-11); удалите каталог Assets/XR/Temp" -f $l.Name)
        }
    } else { Ok "остатков нет (каталог пуст)" }
} else { Ok "каталога Assets/XR/Temp нет (штатное состояние)" }

# ---------------------------------------------------------------- 8. эталон scriptingDefineSymbols
#
# ЗАЧЕМ (задача P4-16, 12.11.2026). Наблюдалось: во время прогона в `scriptingDefineSymbols` для
# Android появлялась строка `SENTIS_ANALYTICS_ENABLED`, затем исчезала. Источник в `Assets`,
# `Packages` и `Tools` не нашёлся НИ РАЗУ. `ProjectSettings/ProjectSettings.asset` —
# ОТСЛЕЖИВАЕМЫЙ файл, то есть сторонний код меняет настройки сборки у нас за спиной: символы
# определяют, какой код вообще компилируется в APK, и «само появилось» здесь недопустимо.
#
# ЧТО ДЕЛАЕТ СТОРОЖ. Сверяет фактический набор символов для Android с ЭТАЛОНОМ в этом же файле.
# Расхождение — провал с точным списком лишних и недостающих символов. Если символы осознанно
# поменяли, эталон правится В ТОМ ЖЕ коммите — тогда изменение видно в истории, а не «само».
#
# ИСТОЧНИК НАЙДЕН 12.11.2026 — ОН БЫЛ ВНЕ ТРЁХ ПРОВЕРЕННЫХ КАТАЛОГОВ:
#   Library/PackageCache/com.unity.ai.inference@…/Editor/Analytics/AnalyticsDefineManager.cs
# Пакет `com.unity.ai.inference` (бывший Sentis) в `[InitializeOnLoadMethod]` добавляет символ
# `SENTIS_ANALYTICS_ENABLED`, если включена аналитика редактора (`EditorAnalytics.enabled`), и
# убирает его, если выключена. Отсюда и «появился, затем исчез»: менялся переключатель аналитики,
# а не код проекта; в `Assets`/`Packages`/`Tools` этого не видно по определению, потому что код
# пакета распаковывается в `Library/PackageCache`.
# РЕШЕНИЕ: аналитика редактора выключена (`ProjectSettings/UnityConnectSettings.asset`,
# `m_Enabled: 0`), поэтому символ детерминированно ОТСУТСТВУЕТ, а эталон ниже — строгий.
Head "8. Эталон scriptingDefineSymbols для Android (P4-16)"
$projectSettings = Join-Path $ProjectPath 'ProjectSettings/ProjectSettings.asset'
if (-not (Test-Path -LiteralPath $projectSettings)) {
    Fail "нет ProjectSettings/ProjectSettings.asset — сверять символы не с чем"
} else {
    $expectedAndroidDefines = @(
        'UNITY_POST_PROCESSING_STACK_V2',
        'OVR_DISABLE_HAND_PINCH_BUTTON_MAPPING',
        'USE_INPUT_SYSTEM_POSE_CONTROL',
        'USE_STICK_CONTROL_THUMBSTICKS',
        'APP_UI_EDITOR_ONLY'
    )
    # СИМВОЛЫ, КОТОРЫМИ УПРАВЛЯЕТ ПАКЕТ, А НЕ ПРОЕКТ.
    #
    # `SENTIS_ANALYTICS_ENABLED` добавляет и убирает пакет `com.unity.ai.inference` по настройке
    # аналитики РЕДАКТОРА (`EditorAnalytics.enabled`) — см. шапку раздела. Эта настройка живёт в
    # профиле пользователя, а не в репозитории, поэтому закрепить её проектом нельзя: герметично
    # тут ничего не сделать. Честный вариант — назвать символ «пакетным» и ПРЕДУПРЕЖДАТЬ о нём,
    # а проваливать только расхождения по символам, которые принадлежат проекту.
    $packageManagedDefines = @('SENTIS_ANALYTICS_ENABLED')
    # Разбор YAML руками: блок `scriptingDefineSymbols:` и внутри него строка `Android: A;B;C`.
    # Читаем до первой строки, которая перестала быть вложенной, — так блок не «перетекает» в соседние.
    $actualDefines = @()
    $lines = Get-Content -LiteralPath $projectSettings
    $inBlock = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^\s*scriptingDefineSymbols\s*:') { $inBlock = $true; continue }
        if (-not $inBlock) { continue }
        if ($line -match '^\s{4}Android\s*:\s*(.*)$') {
            $actualDefines = @($Matches[1].Split(';') | Where-Object { $_ -ne '' } | ForEach-Object { $_.Trim() })
            break
        }
        if ($line -match '^\s{0,2}\S' -and $line.Trim() -ne '') { $inBlock = $false }
    }
    if ($actualDefines.Count -eq 0) {
        Warn "не удалось прочитать символы Android из ProjectSettings.asset — сверка не выполнена"
    } else {
        $owned = @($actualDefines | Where-Object { $packageManagedDefines -notcontains $_ })
        $managed = @($actualDefines | Where-Object { $packageManagedDefines -contains $_ })
        $missing = @($expectedAndroidDefines | Where-Object { $owned -notcontains $_ })
        $extra = @($owned | Where-Object { $expectedAndroidDefines -notcontains $_ })
        if ($missing.Count -gt 0) {
            Fail ("в символах Android ПРОПАЛИ: {0} — код в APK соберётся другим (P4-16)" -f ($missing -join ', '))
        }
        if ($extra.Count -gt 0) {
            Fail ("в символах Android ПОЯВИЛИСЬ посторонние: {0} — такой символ включает чужой код; обновите эталон в Tools/repo-guards.ps1 и объясните изменение в коммите (P4-16)" -f ($extra -join ', '))
        }
        if ($managed.Count -gt 0) {
            Warn ("символ {0} добавлен ПАКЕТОМ com.unity.ai.inference по настройке аналитики редактора — это не правка проекта (P4-16). Выключите аналитику в настройках редактора, если символ не нужен" -f ($managed -join ', '))
        }
        if ($missing.Count -eq 0 -and $extra.Count -eq 0) {
            Ok ("символы Android совпадают с эталоном: принадлежащих проекту {0} шт. · добавлено пакетом {1} шт." -f $owned.Count, $managed.Count)
        }
    }
}

# ---------------------------------------------------------------- итог
Write-Host ""
Write-Host "=== ИТОГ СТОРОЖЕЙ ==="
Write-Host ("провалов: {0} · предупреждений: {1}" -f $failures.Count, $warnings.Count)
if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "ПРОВАЛЫ:"
    foreach ($f in $failures) { Write-Host "  - $f" }
    exit 1
}
Write-Host "Сторожа пройдены."
exit 0
