# KazistovVv — DEVELOPER README

Руководство для разработчика: что это за проект, как его открыть, собрать, запустить и проверить,
что где лежит и какие договорённости нельзя нарушать.

Всё, что написано ниже, проверено по файлам проекта. Там, где чего-то в проекте нет, это написано
прямо словами «не найдено в проекте».

---

## Оглавление

1. [Что это за проект](#1-что-это-за-проект)
2. [Требования](#2-требования)
3. [Как открыть проект](#3-как-открыть-проект)
4. [Как собрать и запустить](#4-как-собрать-и-запустить)
   * [4.5 Сборка APK под шлем (Android) и проверка идентификатора](#45-сборка-apk-под-шлем-android-и-проверка-идентификатора)
5. [Карта репозитория](#5-карта-репозитория)
6. [Зависимости](#6-зависимости)
7. [Каталоги с данными во время выполнения](#7-каталоги-с-данными-во-время-выполнения)
8. [Управление: клавиатура, мышь, геймпад](#8-управление-клавиатура-мышь-геймпад)
9. [Как добавить функцию без перекомпиляции](#9-как-добавить-функцию-без-перекомпиляции)
10. [Как добавить язык](#10-как-добавить-язык)
11. [Тесты](#11-тесты)
12. [Типовые проблемы и решения](#12-типовые-проблемы-и-решения)
13. [Источники](#13-источники)

---

## 1. Что это за проект

**KazistovVv** — платформа управления промышленными роботами в Unity: инженерная среда, в которой
оператор ставит цель лучом, получает **8 вариантов траектории**, смотрит движение **фантомов**,
выбирает вариант и отправляет робота в путь — с проверкой кинематики, столкновений, лимитов
суставов и сингулярностей.

| Что | Как есть в проекте |
|---|---|
| Роботы | ровно **два** на сцену: **робот** (`Робот_6ос_Стенд1`, `SixAxisController`, 6 осей) и **SCARA** (`SCARA_Стенд2`, `SCARAController`, 3 оси: J1, J2, Z — призматическая) |
| Рендер | **два конвейера (решение [D9], §29)**: **URP 17.5.0 — глобальное умолчание графики** (сборка под шлем, Android), **HDRP 17.5.0 — уровень качества «PC»** (компьютер). Линейное цветовое пространство. Материалы, создаваемые кодом, обязаны идти через `KvRenderPipelineShaders` |
| Интерфейс | десктопный, **в стиле FreeCAD**, на **uGUI**, строится кодом в рантайме (сцена не переделывается): строка меню, тулбар-иконки, dock-панели, дерево моделей, свойства, статус-бар |
| VR / MR | ветка `Input/VRInputProvider`, `VR/*`, `Spatial/*`, XR Interaction Toolkit 3.5.1, AR Foundation 6.5.0, OpenXR-лоадер + Meta X API layer; **VR-интерфейс делается отдельно** — текущий UI десктопный |
| LLM | **в рантайме не используется**: только кинематика, коллизии, планировщик, IK, safety |
| Прочее | запись/воспроизведение траекторий, позы, зоны запрета, журнал действий, экспорт в KRL/KAREL/RAPID, импорт URDF/STEP, тепловые карты, health monitor, совместная работа по UDP, веб-дашборд, макросы, отчёт PDF |

Ключевой сценарий работы (State Machine, ведёт `TrajectoryFlowController`):

```
Idle ──красный лазер (Z) + ЛКМ по поверхности──▶ PointSelected
     ──(идёт планирование)──▶ TrajectoriesShown   (8 траекторий = 8 «колбасок» = 8 фантомов)
     ──зелёный лазер (X) + ЛКМ по траектории──▶ PhantomsMoving
     ──зелёный лазер + ЛКМ по фантому──▶ RobotMoving ──FinishMotion──▶ Idle
     Enter ──▶ PointMoveMode (Q/E, W/S, A/D двигают точку; Enter — подтвердить, Esc — отменить)
     Esc ──▶ ResetAll ──▶ Idle
```

---

## 2. Требования

### 2.1 Версия Unity

`ProjectSettings/ProjectVersion.txt`:

```
m_EditorVersion: 6000.5.6f1
m_EditorVersionWithRevision: 6000.5.6f1 (0e0577a1a2ac)
```

Проект открывается **только** Unity **6000.5.6f1** (или совместимой 6000.5.x — Unity предложит
обновление). Более старые редакторы проект не откроют: часть кода использует API Unity 6
(`FindObjectsByType`, `FindFirstObjectByType`, `FindAnyObjectByType`).

### 2.2 Обязательные пакеты и модули

Обязательны **оба** пакета конвейеров:

* **URP** (`com.unity.render-pipelines.universal` 17.5.0) — **глобальное умолчание графики** проекта
  и конвейер сборки под шлем (Android). Пакет HDRP не поддерживает Android вообще, поэтому без URP
  сборка APK невозможна (это и была карточка Б1, закрытая в §29).
* **HDRP** (`com.unity.render-pipelines.high-definition` 17.5.0) — уровень качества «PC»: на нём
  стоят материалы, свет и томы десктопной сцены.

Остальные пакеты перечислены в §6.

### 2.3 Требуемые SDK и платформы

| Что | Зачем | Где в проекте |
|---|---|---|
| **Windows (x86-64)** | основная рабочая ОС: пути «Документы»/«Видео» через `Environment.SpecialFolder`, курсоры `Cursor.SetCursor` + `ForceSoftware`, batch-скрипты PowerShell | весь контекст проекта |
| **Windows Build Support (IL2CPP)** в Unity Hub | сборка Standalone-плеера | `ProjectSettings.asset` → `scriptingBackend` |
| **Android Build Support** | сборка под шлем (Quest/PICO): `AndroidMinSdkVersion: 32`, `AndroidTargetSdkVersion: 34` | `ProjectSettings.asset` |
| **OpenXR-лоадер + слой Meta X** | MR-контур | `Assets/XR/Loaders/OpenXRLoader.asset`, `Assets/XR/APILayers~/WindowsLayers/x64/XrApiLayer_METAX_operator.dll` |
| **OpenXR-фичи для Android** | шлем PICO 4 Ultra (§31): рантайм `com.pico.xr.openxr_runtime` | включены `SpaceWarpFeature`, **`KHRSimpleControllerProfile`**, `OculusTouchControllerProfile` — `Logs/s31-readiness.log`; включение профиля — `Logs/s31-profile.log` |
| **Пакет Unity Recorder** | запись видео (MP4) — редакторский API под `#if UNITY_EDITOR` | `Packages/packages-lock.json` (`com.unity.recorder`), код в `Features/KvCaptures.cs` |
| **ROS 2 (внешне)** | `com.unity.robotics.ros-tcp-connector` подключён, но ROS-сторона в этом проекте не запускается | `Packages/manifest.json` |

### 2.4 Настройки плеера, которые полезно знать

Из `ProjectSettings/ProjectSettings.asset`:

| Параметр | Значение |
|---|---|
| `productName` / `companyName` | `KazistovVv` |
| `bundleVersion` | `0.1.0` |
| `m_ActiveColorSpace` | `1` (Linear) |
| `activeInputHandler` | `2` — **Both**: legacy `Input` и new Input System работают одновременно (на этом построены «мосты» ввода) |
| `apiCompatibilityLevel` | `6` (Unity-сгенерированные `*.csproj` — `netstandard2.1`, C# 9) |
| `defaultScreenWidth` / `Height` | 1024 × 768 |
| `applicationIdentifier` | **`com.KazistovVv.app`** — для **Android и Standalone** (применено в §31; до этого стояли значения шаблона URP-blank — APK с ними для приёмки не годился) |
| `runInBackground` | `1` |

### 2.5 Место на диске и время

Первое открытие проекта строит `Library` — **5–15 минут** (в проекте HDRP, шейдеры HDRP
импортируются долго). Полная пересборка `Library` с нуля — тоже в этих пределах.

---

## 3. Как открыть проект

1. Unity Hub → **Add** (или **Open**) → выбрать **папку Unity-проекта** — ту, внутри которой
   лежат `Assets`, `Packages` и `ProjectSettings` (имя папки на диске и имя проекта в Unity
   могут не совпадать: `productName` в `ProjectSettings.asset` — **KazistovVv**).
2. Дождаться импорта: первый запуск собирает `Library` (**5–15 минут**), в это время Unity может
   показывать прогресс импорта HDRP-шейдеров.
3. Открыть сцену **`Assets/_Project/00_Scenes/MainScene.unity`** — она единственная в Build Settings
   (`ProjectSettings/EditorBuildSettings.asset`). В папке сцен есть ещё три:
   `Boot.unity`, `Main Menu.unity`, `Simulation.unity` — в сборку они **не** входят, в рабочем
   цикле используется `MainScene`.
4. Проверка, что всё поднялось: в консоли при старте PlayMode должны появиться строки
   `[RobotInventory] Роботов в сцене: 2 …` и `Интерфейс KazistovVv собран · … · команд: N`, а в
   окне Hierarchy — объект `KazistovVv_UI`.

---

## 4. Как собрать и запустить

### 4.1 Запуск в редакторе (PlayMode)

1. Открыть `MainScene.unity`, нажать **Play**.
2. **TAB** — показать/скрыть оболочку интерфейса вместе с курсором (панели, тулбар, дерево).
3. Минимальная проверка «всё живо»:
   * **Z** — красный лазер, **ЛКМ** по столешнице → в статусе «Точка принята…», затем
     «Вариантов: 8 (уникальных 8 из 8)» и 8 «колбасок» + 8 фантомов;
   * **X** — зелёный лазер, **ЛКМ** по траектории → фантомы едут; **ЛКМ** по фантому → робот едет;
   * **Esc** → сброс в `Idle`.

### 4.2 Компиляция без редактора (`dotnet build`) — рабочий способ этого проекта

Когда редактор у пользователя уже открыт, второй инстанс Unity на той же `Library` запускать
нельзя, и проверка компиляции делается через Unity-сгенерированные `csproj`:

```powershell
cd "<путь к проекту>"          # там, где лежат Assembly-CSharp.csproj и Assembly-CSharp-Editor.csproj
dotnet build Assembly-CSharp.csproj
dotnet build Assembly-CSharp-Editor.csproj
```

* `error CS` в выводе = **настоящая** ошибка компиляции Unity-кода: csproj содержит все ссылки
  на DLL редактора, тот же набор, что собирает Unity.
* **Обязательное условие:** в проекте уже должна существовать `Library/ScriptAssemblies`. Иначе
  сборка выдаст `CS0246` на `Text`/`Image`/`Button` — это **не ошибки кода**, а отсутствие ссылок
  на `UnityEngine.UI.dll`. Лечится одним запуском Unity (он пересоберёт `Library`).
* **НОВОЕ (найдено 13.10.2026, сессия §32): `csproj` содержит ЯВНЫЙ список файлов.** В файле лежат
  строки `<Compile Include="Assets\…\Имя.cs" />`, а укрупнённый обход (`**/*.cs`) отключён. Значит
  **новый `.cs` не проверяется `dotnet build`, пока Unity не откроет проект** — сборка молча проходит
  «успешно», не увидев новый файл. Пока редактор не открывался, добавьте файл в список вручную
  (одна строка рядом с другими `<Compile Include=…>`) либо не доверяйте отчёту «0 ошибок»
  для новых файлов. Unity перегенерирует `.csproj` при следующем открытии — правка временная.
* После проверки удалить артефакты: `bin/`, `obj/`, `Temp/bin/`, `Temp/obj/`.
* `*.csproj` **в `.gitignore`**, поэтому `git status`/`git diff` по ним всегда пусты и как контроль
  «вернул ли я файл» не годятся — сверяйте содержимое.
* **Проверки только через `dotnet build` недостаточно**: часть ошибок видит лишь компилятор Unity
  (пример: `Object.GetInstanceID()` в Unity 6.5 — ошибка `CS0619`, а `dotnet build` её не видит).
  Поэтому нужен хотя бы один прогон Unity (§4.3).
* **Память для batch-прогона.** Unity в batch-режиме на этом проекте требует нескольких гигабайт
  свободной памяти. Замерено 13.10.2026: при 0,7–1,4 ГБ свободных редактор **зависает на этапе
  IL Post Processor** (`Connectivity with IL Post Processor runner cannot be established yet.
  Retrying.` — строка повторяется бесконечно, процесс держит ~30 МБ и не доходит до компиляции
  скриптов). Перед прогоном закройте MATLAB (до ~4,8 ГБ), браузер (~1,6 ГБ) и Discord (~0,8 ГБ).
  Признак живой работы: в логе растут строки, процесс Unity занимает сотни МБ памяти.
* **ЧИСТИТЕ АРТЕФАКТЫ `dotnet build` ПЕРЕД ПРОГОНОМ UNITY (замерено 13.10.2026).** Если после
  `dotnet build` оставить `Temp/bin`, `Temp/obj` или `obj`, Unity считает сборки изменившимися
  и пересобирает IL-код **с нуля**: в замерах это `Tundra build success (622 s)` и
  `AssetDatabase: script compilation time: 1019.9 s` — **17 минут** на каждый прогон вместо 40 секунд
  при чистом состоянии. Порядок такой: `dotnet build` → удалить `bin/ obj/ Temp/bin/ Temp/obj/` →
  прогон Unity.
* **Процесс Unity не завершается сам (замерено 13.10.2026).** После `Shut down.` в логе процесс
  остаётся жить; скрипты `Tools\*.ps1` снимают его по таймауту и возвращают ненулевой код.
  **Провалом это не является:** судить надо по числам в логе (`error CS`, `warning CS`,
  `Missing (Mono Script)`, `ProjectSettings`) и по `Logs\editmode-*.xml`. Признак выполненной
  работы — строка `Shut down.` и ноль ошибок компиляции.
* **Долгий прогон нельзя запускать «в лоб» из оболочки с лимитом 10 минут.** Компиляция IL после
  правок редакторской сборки занимает до 10–17 минут, и процесс снимается вместе с оболочкой.
  Запускайте прогон фоновой задачей (или через `Tools\stage-a-verify.ps1`) и ждите результат по логу.

### 4.3 Batch-запуск Unity

**Проверка компиляции (быстро, выходит сам):**

```powershell
Unity.exe -batchmode -nographics -quit -projectPath "<путь к проекту>" -logFile "<путь>\logs\_compile.log"
```

искать в логе `error CS`. `-nographics` **обязателен**: без него Unity тянет полный реимпорт
HDRP-шейдеров (десятки минут) и конфликтует с открытым редактором.

**Прогон диагностики (PlayMode внутри batch, выходит сама):**

```powershell
Unity.exe -batchmode -nographics -projectPath "<путь к проекту>" -executeMethod DshFullVerifyDiag.Run -logFile "_dsh_full.log"
```

Здесь **без `-quit`** — прогон сам останавливает PlayMode и завершает редактор. Список готовых
диагностик — в §4.4.

**Грабли командной строки (проверены на практике):**

* `Start-Process -ArgumentList` **не экранирует пробелы**: путь «новое пространство» разобьётся на
  два аргумента, Unity напишет `Couldn't set project path` и выйдет с кодом 1. Передавайте всю
  строку **одним элементом** с внутренними кавычками:
  `-ArgumentList "-batchmode -nographics -projectPath `"$p`" -logFile `"$p\_dsh.log`""`.
* Запускать через `&`: Unity — GUI-процесс и **не блокирует** консоль. Выход команды `0` не значит,
  что прогон закончился, — ждите появления файла-отчёта и/или исчезновения процесса.
* Batch-редактор открывает «последнюю» сцену, и это часто **пустая** сцена. Тогда диагностика
  честно пишет `[FAIL] менеджер … поднялся в PlayMode`. Лечение: перед `EditorApplication.EnterPlaymode()`
  вызвать `EditorSceneManager.OpenScene("Assets/_Project/00_Scenes/MainScene.unity", OpenSceneMode.Single)`.
* Если прогон падает **сразу** (лога нет, код возврата 1, в логе `Access token is unavailable`,
  `Code 10 while verifying Licensing Client signature`) — это залипший клиент лицензий, а не код:
  `Get-Process Unity.Licensing.Client | Stop-Process -Force`, после чего прогон проходит штатно.
* Запуск Unity в batch занимает ~2,5 минуты до PlayMode при тёплой `Library` — это не «завис».
* В `-nographics` HDRP пишет `No graphic device is available to initialize the view` — **ошибка
  среды, а не кода**. Провалом считаются только `LogType.Exception`.

### 4.4 Где взять отчёты диагностики

Все отчёты пишутся **в корень проекта** (`Path.Combine(Application.dataPath, "..", "_dsh_*.txt")`),
логи — рядом (`_dsh_*.log`). Скрипты диагностики лежат в `Assets/_Project/01_Scripts/Editor/` и
имеют префикс `Dsh` → в редакторскую сборку (`Assembly-CSharp-Editor`) они попадают, но **в
пользовательскую копию проекта не переносятся** (правило проекта: это инструмент агента).

| Диагностика | `-executeMethod` | Отчёт | Что проверяет |
|---|---|---|---|
| `DshDesktopUiDiag.cs` | `DshDesktopUiDiag.Run` | `_dsh_ui_verify.txt` | оболочка FreeCAD-UI (меню, тулбар, дерево, свойства, статус-бар, темы, dock-панели) |
| `DshFeaturesDiag.cs` | `DshFeaturesDiag.Run` | `_dsh_features_verify.txt` | 20 этапов новых функций (запись, позы, суставы, зоны, сравнение, графики, карты, гриппер, ETA, журнал, undo, звук, сценарии, сессии) |
| `DshFullVerifyDiag.cs` | `DshFullVerifyDiag.Run` | `_dsh_full_verify.txt` | сквозной цикл на роботе И на SCARA (точка → 8 траекторий → 8 фантомов → движение → режим точки → стоп → запись → позы → интерфейс → производительность → стабильность → консоль) |
| `DshStageDiag.cs` | `DshStageDiag.Run` | `_dsh_stage_verify.txt` | этапы 1–8 (фантомы без визуальной разницы, 7 языков, скриншоты/видео, сингулярности, waypoints, health monitor, динамические препятствия, teach pendant) |
| `DshStage2Diag.cs` | `DshStage2Diag.Run` | читается из `_dsh_stage2*.log` | этапы 1–6 (главное меню, туториал, демо, сглаживание, время-оптимальная, эко-профиль) |
| `DshStage3Diag.cs` | `DshStage3Diag.Run` | `_dsh_s3_verify.txt` | этапы 7–12 (ограничения waypoints, constrained planning, калибровка, нагрузка, экспорт KRL/KAREL/RAPID, импорт URDF/STEP) |
| `DshStage4Diag.cs` | `DshStage4Diag.Run` | `_dsh_s4_verify.txt` | этапы 13–36 (прокси, стенд планировщиков, дерево RRT, камеры/PiP, силы, тепло, PDF, сеть, XR-ввод, макросы, окружение/свет/материалы, кино, титры, отказы, проверка перед пуском, уровни журнала) |
| `DshScaraDiag.cs` | `DshScaraDiag.Run` | `_dsh_scara_verify.txt` | сквозной прогон 37 шагов **на SCARA** (3 оси) |
| `DshScaraEightDiag.cs` | `DshScaraEightDiag.Run` | `_dsh_scara8_verify.txt` | ровно **8 уникальных** траекторий на 5 точках рабочей зоны SCARA + честность отчёта о нехватке |
| `DshUiStagesDiag.cs` | `DshUiStagesDiag.Run` | `_dsh_ui_stages.txt` | UX/UI + геймпад: **вызов каждой команды реестра**, все меню, все вкладки настроек, окна, палитра, контекстное меню, F12, empty/error/loading, иконки, доступность; пишет сводную таблицу «команда → статус» |

Дополнительно в корне проекта может лежать `_dsh_offset_diag.txt` — диагностика отказов по точке
(`[OffsetDiag]`), включается флагом `TrajectoryFlowController.logOffsetDiagnostics`.

> **Особенность окружения.** В OneDrive файл отчёта **во время PlayMode может не дописываться** —
> в этом случае итог читают из лога Unity (`_dsh_*.log`), а не из `_dsh_*.txt`.

### 4.5 Сборка APK под шлем (Android) и проверка идентификатора

**Внимание: сборка меняет `Assets` на время работы** — папка тестов убирается из проекта
(см. ниже). Не правьте `Assets` и не запускайте вторую сборку, пока идёт первая.

#### Как запускать

```powershell
# 1) Штатный способ с доказательствами (рекомендуется) — сам соберёт строки-доказательства в лог:
powershell -ExecutionPolicy Bypass -File Tools\stage-a-build-apk.ps1       # таймаут 720 минут
# параметры: -Project C:\KazistovVv_Build  -LogName xr-build-apk-s31.log  -TimeoutMinutes 720

# 2) Напрямую редакторским методом:
Unity.exe -batchmode -nographics -quit -projectPath "C:\KazistovVv_Build" `
          -executeMethod KazistovVvXrEditor.KvXrTools.BuildAndroidApk `
          -logFile "C:\KazistovVv_Build\Logs\xr-build-apk-s31.log"
```

Скрипт `Tools\stage-a-build-apk.ps1` — обёртка вокруг `KazistovVvXrEditor.KvXrTools.BuildAndroidApk`
(метод живёт в `Assets/_Project/01_Scripts/Editor/KvXrTools.cs`). Возврат: **0** — APK собран,
**1** — сборка не удалась (причина в логе).

**Что скрипт задаёт дополнительно (без этого сборка упадёт):**

* **`GRADLE_USER_HOME=C:\gradle_home`** — латинский путь вместо профиля пользователя
  (`C:\Users\Ольга\.gradle`). Иначе Gradle падает на задаче
  `:unityLibrary:configureCMakeRelWithDebInfo[arm64-v8a]`: `C++ build system [prefab] failed`,
  `Error: Could not find or load main class com.google.prefab.cli.AppKt` / `ClassNotFoundException` —
  потому что в отчёте Gradle путь к кэшу приходит **искажённым** (`C:\Users\╬ы№ур\.gradle\…`).
  Это тот же класс проблемы, что «путь проекта с не-ASCII символами» (карточка A6 в `PROBLEMS.md`),
  только теперь путь **кэша**.
* **таймаут 720 минут** (`-TimeoutMinutes 720`) — сборка долгая: шейдеры + IL2CPP + Gradle.

**Сколько это заняло на практике (08.10.2026): 70,3 мин.** Итог в логе:
`[KvXr] APK собран: C:\KazistovVv_Build\KazistovVv.apk · 1345,6 МБ · Android · предупреждений 8 · ошибок 0`
(число в строке — общий размер сборки из отчёта Unity; сам файл APK — **90,8 МБ**).

**Если после сборки проект «остался на Android»:** `finally` возвращает платформу на
`StandaloneWindows64` только если переключала её **именно эта** сборка. Три прерванные сборки
оставили Android «исходным», поэтому успешная сборка платформу не вернула. Лечение — меню
**`Tools/KazistovVv XR/9. Вернуть платформу на компьютер`**
(`KazistovVvXrEditor.KvXrTools.RestoreStandalonePlatform`); проверка — проба §4.5/A4
(`[KvA] активная платформа: StandaloneWindows64`).

#### Что происходит с тестами на время сборки (дефекты A2 и инцидент с `Temp` — закрыты)

Тесты не компилируются в сборке плеера (`KvFlowPlayModeTests.cs` использует
`UnityPlatformAttribute`, которого в плеере нет — было `CS0246`), поэтому папка
`Assets/_Project/08_Tests` на время сборки **убирается** в **`_tests_parked_for_build`
в КОРНЕ проекта** и **возвращается** после.

> ⚠️ **Почему НЕ в `Temp`.** Сначала парковка делалась в `Temp\_tests_parked_for_build`,
> а Unity **при запуске вычищает `Temp`** — так были **уничтожены 30 файлов тестов**.
> Парковка перенесена в корень проекта (+ запись в `.gitignore`). `Temp` для хранения
> не годится принципиально — не возвращайте её туда.

Раньше возврат был сломан: он срабатывал **только если папки в `Assets` нет**, но Unity
**пересоздаёт пустую** папку (её `.meta` остаётся) — файлы тестов остались бы в парковке
**навсегда, и в журнале об этом не было ни строки**. Теперь парковка и возврат — отдельные методы
`KvXrTools.ParkTestsForBuild` / `KvXrTools.RestoreParkedTests`, разбираются четыре случая
(парковки нет / папки нет / папка есть и **пуста** — её пересоздала Unity / папка **не пуста** —
перенос пофайлово, ничего не затирается, конфликты называются числом), плюс самолечение остатка
**убитой** сборки (прежде он уничтожался).

**Что смотреть в логе — числа, а не «должно работать»** (сборка 08.10.2026, 32 файла / 15 тестов):

```
[KvXr] Тесты убраны из сборки APK: тесты обработаны: перенесено файлов 32 (тестов .cs 15),
       конфликтов 0, осталось в Temp 0 · убрано из Assets 32 файлов в …\_tests_parked_for_build
[KvXr] тесты возвращены в Assets (папку пересоздала Unity, пустую сняли): перенесено файлов 32
       (тестов .cs 15), конфликтов 0, осталось в Temp 0
```

**Проверить, что тесты вернулись:**

```powershell
Get-ChildItem "C:\KazistovVv_Build\_tests_parked_for_build" -ErrorAction SilentlyContinue  # ожидается: пусто/нет
Get-ChildItem "C:\KazistovVv_Build\Assets\_Project\08_Tests" -Recurse -File -Filter *.cs     # файлы тестов на месте (15)
powershell -ExecutionPolicy Bypass -File Tools\stage-a-restore-tests.ps1   # возврат со сверкой по SHA256
```

Отчёт возврата с числами (совпало/расхождения/новые): `Logs\stage-a-restore-tests.txt`.

#### Где окажется APK и как проверить идентификатор

APK кладётся в **корень проекта** (`KazistovVv.apk`) и **копией в `Builds\KazistovVv.apk`**
(метод собирает в ASCII-путь и копирует результат в `Builds\`; при неудаче копирования в лог идёт
`[KvXr] APK собран, но скопировать его в Builds не удалось: …`).

**Сборка 08.10.2026 (эталон):** файл **90,8 МБ**, SHA256
`E9B181BEC22030467FCD06F04664D5AEFC6D1703C77F259E72343B473EE3F53A` — у обеих копий хеш совпадает.

```powershell
powershell -ExecutionPolicy Bypass -File Tools\stage-a-apk-check.ps1 -ExpectedPackage com.KazistovVv.app
```

Скрипт берёт `aapt.exe` из Android-модуля Unity и разбирает `aapt dump badging`: печатает
идентификатор пакета, а при `-ExpectedPackage` **расхождение даёт код возврата 1** (без
`-ExpectedPackage` печатает то, что есть).

**Подтверждено разбором этой сборки:** пакет **`com.KazistovVv.app`**, метка `KazistovVv`,
sdkVersion **32**, targetSdkVersion **34**, native-code **`arm64-v8a`**, активность
`com.unity3d.player.UnityPlayerGameActivity`.

**Ожидаемый идентификатор — `com.KazistovVv.app`** (для Android и Standalone; применён в §31).
Прежнее значение было шаблонным (`com.UnityTechnologies.com.unity.template.urpblank`) — APK с ним
для приёмки не годился. Доказательство применения: `Logs/s31-identifier.log` →
`… → com.KazistovVv.app · ПРИМЕНЕНО`.

**Дальше — установка на шлем:** `Tools\device-install-and-log.ps1` (сам находит `adb`, ждёт шлем,
ставит APK, запускает приложение и пишет лог устройства в `Logs/device-<дата>.log`).
На 08.10.2026 **приложение в шлем ещё не устанавливалось**.

#### Почему сборка идёт часами и что с этим сделано

Длительность сборки определяет **компиляция вариантов шейдеров URP** (карточка Б2). Сделано
**четыре** вещи: выключены шейдеры террейна (`m_IncludeTerrainShaders: 0`), из
`DefaultVolumeProfile.asset` убран мусор, добавлен свой проход отсечения
`KvShaderVariantStripper` (`IPreprocessShaders`) и — **главное** — написана своя политика
ключевых слов **`KvUrpKeywordPolicy.cs`**.

**Настоящая причина долгих сборок** оказалась не в постобработке, а в шейдере
**`Universal Render Pipeline/Lit`, проход `ForwardLit`**: полный набор **36 238 786 560** →
фильтрация **113 246 208** → встроенное отсечение **442 368** — и URP это **не срезал**, потому что
в `Assets/Settings/Mobile_RPAsset.asset` значения предфильтрации остались по умолчанию
(`m_PrefilterModeMainLightShadows: 4` = SelectAll, `m_PrefilteringModeAdditionalLight: 5` = SelectAll,
остальные — Select), то есть «оставлять всё» — при том что мягкие тени выключены
(`m_SoftShadowsSupported: 0`), тени доп. света выключены (`m_AdditionalLightShadowsSupported: 0`),
доп. свет повершинный (`m_AdditionalLightsRenderingMode: 1`), каскад один (`m_ShadowCascadeCount: 1`),
SSAO в мобильном рендерере нет, тумана (`m_Fog: 0`) и карт освещения (`m_Lightmaps: []`) в сценах нет.

Политика снимает ключевые слова, которых проект **не исполняет**, **с причиной для каждого слова**,
и **если настройка не прочиталась — слово НЕ снимается** («не знаю» никогда не превращается
в «отсекаем»). Результат: `URP/Lit ForwardLit` **442 368 → 1 536** (обработка **18,9 с**),
`ClusterDeferred` 1 536 → 64, `FinalPost` 2 560 → 192, `UberPost` 46 080 → 48; шейдеров террейна
в сборке нет. **Две опровергнутые гипотезы** (не проверять снова): `-nographics` **не** причина
(без него те же 442 368 вариантов), и вывод §28 «виновата подмена глобального умолчания графики»
**не подтвердился**.

**Честная оговорка:** на сборке №2 помог и **прогретый кэш** `Library\ShaderCache` — он **частично
заменял** компиляцию, поэтому цена «холодной» сборки (без кэша) числами пока не подтверждена.
Таблица проходов шейдеров с ценами — `Logs\stage-a-shader-variants.md`.

⚠️ **Перед сборкой закройте браузер** (и выключите в нём аппаратное ускорение). На этой машине
видеодрайвер сбрасывается: за 8 суток в журнале Windows — **1 306** событий `Windows Error Reporting`
Id 1001 с подписью `LiveKernelEvent`, код **141**, последние во время сборки — каждые 5–10 минут
(04:56, 05:05, 05:10, 05:16); драйвер Intel UHD — `32.0.101.7088` от 17.06.2026. Из-за этого
компиляция шейдеров идёт медленнее возможного (замер: **8 вариантов за 70 с** против **4 за 60 с**),
а браузер с аппаратным ускорением держит **~1,9 ядра из 4**. Обновление драйвера Intel — отдельная
задача владельца. Подробно — `PROBLEMS.md` Б7.1.

---

## 5. Карта репозитория

### 5.1 Верхний уровень проекта

| Путь | Что там |
|---|---|
| `Assets/` | ассеты. Рабочий код — в `Assets/_Project`; во внешних папках лежит стороннее (`ROOMS/HQ Hangar Free` — ангар, `TABLES/Desk Table` — столы, `Premises`, `Robots/*.fbx`, `Settings` — HDRP/URP-профили, `XR`, `XRI`, `Resources`, `Oculus`, `Plugins`/`Plagins`, `Samples`, `TutorialInfo`, `CompositionLayers`, `HDRPDefaultResources`, `_Recovery` — сцены восстановления после падений редактора) |
| `Packages/` | `manifest.json`, `packages-lock.json` и **встроенные (embedded) пакеты**: `com.unity.xr.mock-hmd`, `io.realvirtual.starter` |
| `ProjectSettings/` | настройки проекта Unity (`ProjectVersion.txt`, `ProjectSettings.asset`, `EditorBuildSettings.asset`, `GraphicsSettings.asset`, `QualitySettings.asset`, `InputManager.asset`, `TagManager.asset` и др.) |
| `Docs/` | **эта документация** (`DEVELOPER_README.md`, `ARCHITECTURE.md`) |
| `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/` | служебные каталоги Unity (в git не попадают) |
| `_dsh_*.log`, `_dsh_*.txt` | логи и отчёты диагностических прогонов |
| `PROJECT_CONTEXT.md` | **главный источник знаний о проекте** (~3200 строк: история сессий, принятые решения, инварианты, известные баги, процедуры) |
| `MainScene.unity.bak` | резервная копия сцены |
| `KazistovVv.sln`, `*.csproj` | Unity-генерируемые файлы решения и проектов (в `.gitignore`) |
| `.gitignore`, `.gitattributes`, `ignore.conf` | правила VCS; `.gitattributes` помечает Unity-YAML как `merge=unityyamlmerge`, `*.cs` — `diff=csharp`; `ignore.conf` — правила для Plastic/иных VCS |

### 5.2 `Assets/_Project` — рабочий проект (17 папок)

| Папка | Фактическое содержимое |
|---|---|
| `00_Scenes/` | **4 сцены**: `MainScene.unity` (6254 строки, единственная в Build Settings; именно её правим только построчно), `Boot.unity`, `Main Menu.unity`, `Simulation.unity` + их `.meta` |
| `01_Scripts/` | **164 `.cs`** в 11 подпапках (см. §5.3) |
| `02_Editor/` | **пусто** (только `.meta` самой папки) — редакторские скрипты лежат в `01_Scripts/Editor` и `06_KazistovVv_UI/03_Scripts/Editor` |
| `02_Prefabs/` | **пусто по содержимому**: 4 подпапки-заготовки `Environment`, `FX`, `Robot`, `UI` (в них файлов нет). Префабов роботов нет — роботы стоят в сцене как instance из FBX |
| `03_Models/` | **пусто по содержимому**: подпапки `Environment`, `Props`, `Robot` без файлов. Модели роботов лежат в `Assets/Robots/Robot.fbx` и `Assets/Robots/ScaraRobot.fbx` |
| `04_Materials/` | 1 материал-ассет паков (`Props/IndicatorMat.mat`) + **материалы, созданные в §29/§30**: `URP/` (7 копий для шлема) и `Robot/` (`Robot_HDRP.mat`, `Robot_URP.mat`) |
| `05_Textures/` | **пусто по содержимому**: подпапки `Environment`, `Props`, `Robot` без файлов |
| `06_Animations/` | **пусто по содержимому**: подпапки `Robot`, `UI` без файлов |
| `06_KazistovVv_UI/` | **модуль интерфейса: 37 `.cs`** в `03_Scripts/{Core,Zones,Data,Camera,Editor}` (см. §5.4) |
| `06_Lighting/` | **пусто** (только `.meta`) |
| `07_Audio/` | **пусто по содержимому**: подпапки `Music`, `SFX` без файлов (звуки в проекте **синтезируются кодом**, файлов-ассетов нет) |
| `08_Resources/` | **пусто** (только `.meta`) |
| `08_Shaders/` | **пусто** (только `.meta`) |
| `09_Data/` | **пусто** (только `.meta`) |
| `09_Shaders/` | **пусто** (только `.meta`) |
| `10_StreamingAssets/` | **пусто** (только `.meta`). Внешние данные лежат в **`Assets/StreamingAssets`** (см. §5.5) |
| `Docs/` | 1 файл: `TrajectoryAlgorithmPlan.md` — исходный план вычислительного алгоритма траекторий (капсульная модель, BiRRT, деградация) |

### 5.3 `Assets/_Project/01_Scripts/` — 164 скрипта

_Числа сверены 08.10.2026 (§31): прежние 134 устарели — код вырос на XR-слой (§28), инструменты
конвейера (§29–§31) и проверки. Пересчитывать при каждой большой правке: `glob **/*.cs` по папке._

| Подпапка | Файлов | Расшифровка |
|---|---|---|
| `Core/` | 9 | модели роботов и оператор: `RobotController` (+`RobotInventory` в конце файла), `SixAxisController`, `SCARAController`, `RobotDH` (namespace `KazistovVvKinematics`), `FreeFlyCameraController`, `InverseKinematics`, `RobotSelfCollision`, `SixAxisAutoSetup`, `HDRPAutoLighting` |
| `Editor/` | **24** | редакторские утилиты и **диагностика**: `StandsMenu`, `HierarchyPhantomCleaner`, `HierarchyAutoRefresh`, `ConvertRobotMaterialsToHDRP` (с §29 создаёт материал ТЕКУЩЕГО конвейера), **`KvRenderPipelineTools`** (§29: отчёт, аудит материалов, начинка XR-сцены, материалы робота), **`KvXrTools`** (§28: лоадеры, сцена, сборка APK; §31: парковка тестов), **`KvXrDeviceTools`** (§31: идентификатор и профиль контроллеров), **`KvUrpKeywordPolicy`** + **`KvShaderVariantStripper`** (§31: отсечение вариантов, Б2), `Dsh*Diag.cs` (×10) |
| `Features/` | **67** | модуль «Функции»: `FeatureHub`, `KvStageHub`…`KvStageHub4` и сервисы 36 этапов (см. `ARCHITECTURE.md`); здесь же **`KvRenderPipelineShaders`** (§29: единый выбор шейдера/материала под конвейер) |
| `Input/` | 8 | `InputManager`, `InputProvider`, `KeyboardMouseInputProvider`, `GamepadInputProvider`, `VRInputProvider`, `MRInputProvider`, `KeyboardController`, `InputDeviceType` |
| `Integration/` | 3 | `CollisionGuard`, `RobotSelector`, `TargetMarker` |
| `Recording/` | 4 | `KvTrajectoryRecord`, `KvRecordingService` (рабочие) + `TrajectoryRecorder`, `TrajectoryPlayer` (старая VR-заготовка на `InputManager`, в потоке не используется) |
| `Spatial/` | 2 | `SpatialAnchorManager`, `CalibrationTool` |
| `Trajectory/` | **25** | ядро: `TrajectoryFlowController`, `Planner`, `IkSolver`, `PoseValidator`, `CollisionWorld`, `SafetyGate`, `ReachabilityOracle`, `MotionExecutor`, `TrajectoryExecutor`, `PlanMetrics`, `KinematicsJacobian`, `PostureSelector`, `PostureControl`, `NarrowPhase`, `ToolAlign`, `LaserAndPhantomManagers` (`LaserManager`+`PhantomManager`+`GhostMaterial`), `AimIndicator`, `TrajectoryTube`, `GhostView`, `SelectionTypes`, `PointMoveHud`, `TrajectoryMetricsPanel`, `WorkspaceVisualizer`, `StandBuilder`, `TrajectoryPlannerController` |
| `UI/` | 4 | старая заготовка UI на `InputManager`: `MainMenu`, `SettingsMenu`, `SettingsData`, `SettingsSaver` |
| `VR/` | 5 | `VRInputManager`, `VRHandTracker`, `PoseSelector`, `PlacementController`, `ARInputProvider` |
| **`Xr/`** | **13** | **XR-слой (§28)**: `KvXrRig`, `KvXrRigConfig`, `KvXrInputSource`, `KvXrLocomotion`, `KvXrUiPointer`, `KvXrUiPanel`, `KvXrPanelAnchor`, `KvXrSettings`, `KvXrSettingsChanged`, `KvXrCameraGuard`, `KvXrMrRoom`, `KvXrMrPlacement`, `KvXrSceneContent` |

**Namespaces:** `TrajectoryCore` — большинство файлов `Trajectory/`; `KazistovVvFeatures` — `Features/*`
и `Recording/Kv{TrajectoryRecord,RecordingService}.cs`; `KazistovVvKinematics` — `Core/RobotDH.cs`;
`KazistovVvUI` — модуль интерфейса; часть файлов (`TrajectoryFlowController`, `MotionExecutor`,
`TrajectoryExecutor`, `AimIndicator`, `FreeFlyCameraController`, `RobotController`, `SixAxisController`,
`SCARAController` и др.) лежат в **глобальном** пространстве имён — так исторически сложилось,
менять это без нужды не надо.

### 5.4 `Assets/_Project/06_KazistovVv_UI/03_Scripts/` — 37 скриптов

| Папка | Файлы |
|---|---|
| `Core/` | `KazistovVvUIManager` (оболочка + все привязки), `KvCommands`, `KvBindings`, `KvTheme` (+`UIFactory`), `KvIcons`, `KvWidgets` (`KvIconButton`, `KvSwitch`, `KvSegmented`), `KvTooltip` (+`KvTooltipTarget`), `KvSettings`, `KvLocalization` (`KvLoc`), `KvLayoutStore`, `KvCursors`, `KvUiStates`, `KvKeyboardNav`, `KvGamepadRouter`, `KvGamepadBridge` |
| `Zones/` | `KvMenuBar`, `KvToolbar`, `KvToolbarGroups`, `KvTreeView` (+`KvTreeRow`), `KvPropertiesView`, `KvStatusBar`, `KvSettingsView` (+`KvSettingsSchema`, `KvSettingsCallbacks`), `KvDockPanel` (+`KvDockDrag`, `KvDockIndicator`, `KvSplitter`, `KvResizeHandle`), `KvCommandPalette`, `KvContextMenu`, `KvHotkeyView`, `KvGamepadHud`, `KvSelectionHighlight`, `CenterWindow`, `ScaraCableFollow` |
| `Data/` | `RuntimeRegistry` (+`RegisteredObject`), `ProjectNode` (+`ProjectNodeKind`), `ObjectSpawner` |
| `Camera/` | `IdleCameraBrain` |
| `Editor/` | `KazistovVvMenu` (namespace `KazistovVvUI.EditorTools`) — меню `Tools/KazistovVv UI/…` |

### 5.5 `Assets/StreamingAssets/` — внешние данные

| Путь | Что | Зачем |
|---|---|---|
| `Assets/StreamingAssets/kazistovvv_i18n/` | **7 файлов словарей**: `ru.json`, `en.json`, `zh.json`, `es.json`, `de.json`, `fr.json`, `ja.json` (по 187 строк каждый) | мультиязычность без перекомпиляции |
| `Assets/StreamingAssets/kazistovvv_settings.json` | внешняя схема настроек (`items[]`: `id`, `tab`, `title`, `type`, `note`; `bindings[]`: `group`, `action`, `keys`, `note`) | новые пункты настроек без перекомпиляции |
| `Assets/StreamingAssets/robots/` | `sample_robot.urdf` (цепь из шести осей — пример для проверки импорта), `sample_step.step` | импорт моделей роботов |

> Корневого `StreamingAssets/` в проекте **нет** — внешние данные лежат только в `Assets/StreamingAssets`.

### 5.6 Сцена `MainScene.unity` — структура (через поиск по файлу)

Сцена — YAML на 6254 строки, LF, без BOM. Основные объекты (по `m_Name`):

| Объект | Примечание |
|---|---|
| `Main Camera` | компонент `FreeFlyCameraController` (в `m_EditorClassIdentifier: Assembly-CSharp::FreeFlyCameraController`) — создаёт лазеры, `AimIndicator`, `TrajectoryFlowController` в рантайме |
| `KazistovVv_UI` | объект интерфейса, компонент `KazistovVvUI.KazistovVvUIManager` (блок `MonoScript` имеет `m_Namespace: KazistovVvUI`) — строит оболочку кодом |
| `Робот_6ос_Стенд1` | prefab-instance `Robot.fbx` + `Assembly-CSharp::SixAxisController`, `isActive = 0` |
| `SCARA_Стенд2` | prefab-instance `ScaraRobot.fbx` + `Assembly-CSharp::SCARAController`, `isActive = 0` |
| `Стенд_1_Стол`, `Стенд_2_Стол`, `Столешница` (×2), `Ножка_1..4` (×2 стола) | стенды (в файле имена в юникод-escape, например `\u0421\u0442\u0435\u043d\u0434_1_\u0421\u0442\u043e\u043b`) |
| `Level`, `Plane`, `Directional Light`, `Cline_RimLight`, `Cline_FillLight`, `Reflection Probe`, `Reflection Probe (1)`, `Post Process Volume`, `Sky and Fog Volume`, `StaticLightingSky`, `Spline` | окружение, HDRP-свет и томы — **не трогать** |
| Объекты с `KazistovVvUI.RegisteredObject` (×2) | маркеры объектов проекта для дерева моделей |

В сцене **нет** компонентов `TrajectoryFlowController`, `AimIndicator`, `PhantomManager`, `LaserManager` —
всё это создаётся кодом (`AddComponent`) в `FreeFlyCameraController.Awake`. Поэтому **новые поля
скриптов не требуют правок сцены** — они берут значения из инициализаторов.

### 5.7 Правила VCS

| Файл | Содержимое |
|---|---|
| `.gitignore` | шаблон Unity: игнорируются `Library/`, `Temp/`, `obj/`, `Build(s)/`, `Logs/`, `UserSettings/`, `*.log`, `*.csproj`, `*.sln`, `*.apk/aab/app`, `.vs/`, `.codebuddy/`, `.gigacode_vsc/` и пр. |
| `.gitattributes` | макросы `unity-yaml` (merge через `unityyamlmerge`, `eol=lf`), `unity-json`, `*.cs text diff=csharp`, LFS-макрос |
| `ignore.conf` | те же исключения в формате Plastic SCM (`Library`, `Temp`, `obj`, `*.csproj`, `~UnityDirMonSyncFile~*`, …) |

---

## 6. Зависимости

`Packages/manifest.json` (полный список, 24 именованных пакета + модули Unity):

| Пакет | Версия | Зачем нужен |
|---|---|---|
| `com.unity.render-pipelines.high-definition` | 17.5.0 | **обязателен**: вся графика сцены (материалы роботов, свет, томы, туман, небо) |
| `com.unity.render-pipelines.universal` | 17.5.0 | второй RP в проекте (профили в `Assets/Settings`) |
| `com.unity.visualeffectgraph` | 17.5.0 | VFX Graph (в связке с HDRP) |
| `com.unity.inputsystem` | 1.20.0 | new Input System: геймпад, роутер UI, UI-модуль событий (`activeInputHandler = Both`) |
| `com.unity.ugui` | 2.5.0 | **весь интерфейс** (десктопная оболочка на uGUI, строится кодом) |
| `com.unity.xr.interaction.toolkit` | 3.5.1 | VR/MR-взаимодействие, VR-контур проекта |
| `com.unity.xr.arfoundation` | 6.5.0 | AR Foundation (AR-этап отложен ТЗ) |
| `com.unity.xr.management` | 4.7.0 | загрузчики XR (`Assets/XR/Loaders`: OpenXR, ARCore, MockHMD, Simulation) |
| `com.unity.robotics.ros-tcp-connector` | git (Unity-Technologies) | мост к ROS 2 (`Assets/Resources/ROSConnectionPrefab.prefab`) |
| `com.unity.cinemachine` | 3.1.7 | кинематографическая камера (презентационный/кинорежим) |
| `com.unity.timeline` | 1.8.12 | Timeline (в проекте подключён, активно используется Splines/Timeline-ассет в сцене) |
| `com.unity.ai.inference` | 2.6.1 | инференс моделей (пакетный, в рантайме проекта не задействован) |
| `com.unity.cloud.gltfast` | 6.20.0 | импорт glTF (сторонние модели) |
| `com.unity.probuilder` | 6.1.2 | редакторская работа с геометрией |
| `com.unity.2d.sprite` | 1.0.0 | спрайты (иконки/UI-заготовки) |
| `com.unity.test-framework` | 1.4.7 | Unity Test Runner (EditMode-тесты — см. §11) |
| `com.unity.collab-proxy` | 2.13.3 | Unity Version Control |
| `com.unity.ide.rider` | 3.0.38 | интеграция Rider |
| `com.unity.ide.visualstudio` | 2.0.26 | интеграция Visual Studio |
| ~~`com.coplaydev.unity-mcp`~~ | — | **УДАЛЁН 16.09.2026** (аудит, §0.10). Сторонний редакторский MCP-мост для агентов, на рантайм не влиял и кодом проекта не использовался |
| ~~`com.gladekit.mcp-bridge`~~ | — | **УДАЛЁН 16.09.2026** (аудит, §0.11). Занимал фиксированный порт 8765 и при открытом втором редакторе писал в лог `[UnityBridge] Failed to start server` |
| ~~`com.anklebreaker.unity-mcp`~~ | — | **УДАЛЁН 16.09.2026** (аудит, §0.10). Давал предупреждение `MenuItem Window/AB Unity MCP was added twice` |
| — | — | **В `manifest.json` не осталось ни одного MCP-моста.** Порт 8765 не занимается ни одним пакетом, поэтому батчмод-гейт работает даже при открытом втором редакторе |
| `com.unity.modules.*` | 1.0.0 | встроенные модули Unity: `physics`, `animation`, `audio`, `ui`, `uielements`, `jsonserialize`, `imgui`, `screencapture`, `video`, `xr`, `terrain`, `particlesystem`, `accessibility`, `adaptiveperformance`, `ai`, `androidjni`, `assetbundle`, `cloth`, `director`, `imageconversion`, `physics2d`, `tilemap`, `umbra`, `unityanalytics`, `unitywebrequest*`, `vehicles`, `wind` |

**Встроенные (embedded) пакеты в `Packages/`** — они **не** перечислены в `manifest.json`, но лежат
в репозитории:

| Пакет | Версия | Что это |
|---|---|---|
| `io.realvirtual.starter` | 6.3.5 | realvirtual Starter — фреймворк симуляции промышленной автоматизации (в проекте подключён как библиотека) |
| `com.unity.xr.mock-hmd` | — | Mock HMD loader для отладки XR без шлема (`xr.sdk.mock-hmd.settings` прописан в Build Settings) |

**Разрешённые транзитивные пакеты** (из `Packages/packages-lock.json`, в `manifest.json` их нет):
`com.unity.recorder` (запись видео), `com.unity.splines`, `com.unity.textmeshpro`,
`com.unity.postprocessing`, `com.unity.test-framework.performance`, `com.unity.burst`,
`com.unity.collections`, `com.unity.mathematics`, `com.unity.ai.navigation`, `com.unity.dt.app-ui`,
`com.unity.ext.nunit`, `com.unity.settings-manager`, `com.unity.shadergraph`,
`com.unity.render-pipelines.core`, `com.unity.xr.core-utils`, `com.unity.xr.legacyinputhelpers`,
`com.unity.nuget.newtonsoft-json`, `com.unity.nuget.mono-cecil` и др.

> В корне проекта лежат устаревшие `*.csproj` от когда-то установленных и затем удалённых пакетов
> (`realvirtual.*`, `NaughtyAttributes.*`, `IngameDebugConsole.*`, `meta.xr.mrutilitykit.*`,
> `Milovanovv30.Renderpipelineshighdefinition.*`). Это **остатки генерации**, в `manifest.json` и
> `packages-lock.json` таких пакетов нет; на сборку они не влияют (`*.csproj` в `.gitignore`).

---

## 7. Каталоги с данными во время выполнения

### 7.1 Данные приложения — `<persistentDataPath>/KazistovVv/`

Корень задаётся `FeatureStorage.Root` = `Path.Combine(Application.persistentDataPath, "KazistovVv")`
(в редакторе — вне `Assets`, поэтому файлы не попадают в сборку и не требуют `.meta`). Полный путь
всегда пишется в лог.

| Каталог | Что внутри |
|---|---|
| `Recordings/` | записи траекторий (JSON, сэмплы 20 Гц: углы суставов + время + TCP) |
| `Poses/` | preset-позы роботов |
| `Zones/` | зоны запрета (куб/сфера/цилиндр) |
| `Sessions/` | сессии (позы роботов, выбранная точка, зоны, ссылки на записи/позы, флаги функций) |
| `Logs/` | журнал действий `actions_ГГГГММДД.log`, телеметрия планировщика `planner_runs.json`, CSV мониторинга состояния `health_ГГГГММДД_HHMMSS.csv` |
| `Config/` | калибровка `calibration_<робот>.json`, правки материалов `materials_edits.json` |
| `Scenarios/` | сценарии (заготовка каталога из `FeatureStorage.ScenariosDir`) |
| `Macros/` | макросы `*.kvs` (движок макросов, `KvScriptEngine.MacrosFolder`) |
| `Bench/` | CSV стенда сравнения планировщиков (`KvPlannerLab` → `FeatureStorage.Root/Bench`) |

> **Уточнение к PROJECT_CONTEXT.** В §15.2 документации CSV стенда описан как
> «`Документы\KazistovVv\…\Bench`», но по коду (`KvPlannerLab.cs`) каталог — **`<persistentDataPath>/KazistovVv/Bench`**.
> Внешние папки в «Документах» — только `reports`, `robot_export`, `robot_models`, `voiceover` (ниже).

### 7.2 Документы пользователя

| Путь | Что |
|---|---|
| `Документы\KazistovVv\reports\` | PDF-отчёты `KazistovVv_report_<дата>.pdf` (`KvReportPdf`) |
| `Документы\KazistovVv\robot_export\` | `trajectory.src` (KUKA KRL), `trajectory.kl` (FANUC KAREL), `trajectory.mod` (ABB RAPID) |
| `Документы\KazistovVv\robot_models\` | модели для импорта (URDF/STEP), вторая папка поиска после `Assets/StreamingAssets/robots` |
| `Документы\KazistovVv\voiceover\` | запись голоса диктора WAV 16 бит/44,1 кГц + текстовый список меток (`KvVoiceOverService`) |
| `Видео` (`Environment.SpecialFolder.MyVideos`) | `KazistovVv_screenshot_YYYY-MM-DD_HH-MM-SS.png`, `KazistovVv_recording_YYYY-MM-DD_HH-MM-SS.mp4` (резерв при недоступности — `%USERPROFILE%\Videos`) |

Выбор `path:` для **SCARA**: 7.1 и 7.2 одинаковы для обоих роботов; отдельного хранилища у SCARA нет.

---

## 8. Управление: клавиатура, мышь, геймпад

**Единый реестр биндов — `Core/KvBindings.cs`** (`KvBindings.All()`). Именно из него читают и окно
**F12** (`Zones/KvHotkeyView.cs`), и вкладка «Управление» в настройках (`KvSettingsSchema.Bindings()`),
поэтому таблицы не расходятся. Ниже — таблица ровно по этому файлу.

### 8.1 Клавиатура и мышь

| Раздел | Действие | Клавиша | Примечание |
|---|---|---|---|
| Траектории | Выбор точки (красный лазер) | **Z** | включает/выключает левую указку; ЛКМ фиксирует точку |
| Траектории | Выбор траектории (зелёный лазер) | **X** | включает/выключает правую указку |
| Траектории | Подтверждение действия | **ЛКМ** | смысл зависит от включённого лазера (шаги 1–5) |
| Траектории | Режим перемещения точки | **Enter** | вход и подтверждение (из `PointSelected`/`TrajectoriesShown`) |
| Траектории | Движение точки в режиме | **Q/E, W/S, A/D** | мировые оси; **Shift** — втрое быстрее; работает **только** в режиме перемещения точки |
| Траектории | Глубина шарика прицела | **колесо мыши** | вперёд — от оператора, с прилипанием к первой поверхности; шаг `scrollStep = 0.08` |
| Траектории | Шарик к ближайшей поверхности | **средняя кнопка** | мгновенный возврат к ближайшей поверхности по лучу |
| Траектории | Отмена / сброс | **Esc** | в режиме перемещения — отмена перемещения, вне его — сброс сценария в `Idle` |
| Робот | Выбор робота по прицелу | **F** | переключение робот ⟷ SCARA |
| Робот | Фонарик | **G** | вкл/выкл с первого нажатия |
| Робот | Аварийная остановка | — | кнопка тулбара `estop` / **LB** на геймпаде |
| Робот | Слайдеры суставов | — | панель тулбара `joints.panel` |
| Робот | Захват: открыть/закрыть | **V** | гриппер |
| Интерфейс | Показать/скрыть интерфейс (режим UI) | **TAB** | освобождает курсор и показывает панели |
| Интерфейс | Палитра команд | **Ctrl+P** | `Ctrl+Shift+P` — тоже; поиск по всем языкам/id/описанию/клавише |
| Интерфейс | Горячие клавиши | **F12** | окно со списком и поиском (живёт в dock-панели) |
| Интерфейс | Движение камеры | **W/A/S/D, Q/E** | **Shift** — быстрее; в режиме перемещения клавиши отданы точке |
| Интерфейс | Обзор камеры | **мышь** (или правый стик) | — |
| Интерфейс | Размещение: тип робота | **1 / 2** | только в режиме «Добавить робота» |
| Интерфейс | Размещение: поворот | **← / →, A / D** | — |
| Интерфейс | Размещение: подтвердить | **ЛКМ / Enter** | — |
| Дерево моделей | Контекстное меню узла | **ПКМ** | Переименовать, Дублировать, Удалить, Скрыть/Показать, Фокус камеры, Свойства, Копировать имя |
| Дерево моделей | Мультивыбор узлов | **Ctrl + ЛКМ** | действие меню применяется ко всем выбранным узлам |
| Дерево моделей | Переименование узла | **двойной ЛКМ** | меняется ЯРЛЫК узла, имя объекта сцены — нет |
| Постобработка и экспорт | Тепловая карта достижимости | **H** | сфера у робота, кольцо у SCARA |
| Постобработка и экспорт | Тепловая карта зазоров | **J** | раскраска траектории по зазору |
| Постобработка и экспорт | Графики углов суставов | (панель, `F9*`) | помечено контекстным, открывается панелью |
| Постобработка и экспорт | Скриншот / видео / PDF | — | кнопки тулбара (`shot.screenshot` и др.) |

**Горячие клавиши модуля «Функции» и 36 этапов** (свободные, штатные бинды не тронуты):

| Клавиша | Действие |
|---|---|
| **R** | запись вкл/выкл |
| **P** | воспроизведение / пауза записи |
| **V** | захват (гриппер) |
| **H**, **J** | карта достижимости, карта зазоров |
| **F1** | главное меню при запуске (показать/скрыть) |
| **F2** | окно-верстак постобработки |
| **F3** | обучение (туториал) |
| **F4** | демонстрация Quick Start |
| **F5** | панель функций |
| **F6** | журнал действий |
| **F7** | сравнение траекторий A/B |
| **F8** | скриншот |
| **F9** | вкладка «Планирование с ограничениями» |
| **F10** | запись видео |
| **F11** | виртуальный пульт (teach pendant) |
| **F12** | список горячих клавиш / мониторинг состояния (панель `health.toggle`) |
| **Ctrl+Z / Ctrl+Y** | отмена / повтор (повтор **не** запускает робота сам) |
| **Alt+1 … Alt+0, Alt+−, Alt+=** | открытие вкладок этапов 13–36 (прокси, стенд, камеры, силы/тепло, PDF, сеть, голос, руки, взгляд, макросы, дерево поведения, кино, титры, диктор и т. д.) |
| Стрелки ↑/↓, W/S, Enter | навигация в списках (стартовое меню, палитра) |

**Навигация с клавиатуры (доступность).** Настройка «Навигация с клавиатуры» (`KvKeyboardNav`)
включает: **Tab / Shift+Tab** — по элементам, **стрелки** — по соседним кнопкам геометрически,
**Enter/пробел** — нажать, **Esc** — снять фокус. Важно: пока навигация включена, **Tab занят
фокусом**, поэтому показать/скрыть интерфейс можно **Esc** (показать) и командой
«Вид → Показать/скрыть интерфейс». При выключенной настройке поведение Tab прежнее.

### 8.2 Геймпад

Раскладка (ЭТАП 9 UX-сессии; роутер `Core/KvGamepadRouter.cs`, мост `Core/KvGamepadBridge.cs`,
экранный индикатор `Zones/KvGamepadHud.cs`):

| Раздел | Действие | Кнопка |
|---|---|---|
| Робот | Переключение робота (робот / SCARA) | **D-Pad ← / →** |
| Траектории | Глубина шарика прицела (глубже / ближе) | **D-Pad ↑ / ↓** |
| Траектории | Подтверждение (аналог ЛКМ): точка / траектория | **A** |
| Траектории | Отмена / Esc | **B** |
| Траектории | Красный лазер (выбор точки) | **X** |
| Траектории | Зелёный лазер (выбор траектории) | **Y** |
| Траектории | Вход/подтверждение режима перемещения точки | **LT** |
| Траектории | Запуск траектории | **RT** |
| Робот | Аварийная остановка | **LB** |
| Робот | Домой (preset-поза) | **RB** |
| Интерфейс | Палитра команд | **Start** |
| Интерфейс | Список горячих клавиш | **Select (Back)** |
| Интерфейс | Движение камеры (как ходьба) | **левый стик** |
| Интерфейс | Поворот камеры (обзор) | **правый стик** |
| Интерфейс | Ручной режим стиков (устаревший тумблер) | **R3** (оставлен для совместимости) |

**Особенности:** если геймпада нет — управление **не активируется** (`KvGamepadBridge.Active = false`,
флаги сброшены), клавиатура и мышь работают как раньше. Подключение определяется автоматически
(каждый кадр + `InputSystem.onDeviceChange`). Пока роутер активен, старые «геймпадные» пути в
контроллере камеры (LB, левый триггер) отключаются, чтобы одно нажатие не срабатывало дважды.
Заглушки «VR / MR» в таблице биндов — задел (VR-бинды будут добавлены позже).

---

## 9. Как добавить функцию без перекомпиляции

### 9.1 Пункт в панели настроек — через внешний JSON

Файл: **`Assets/StreamingAssets/kazistovvv_settings.json`**. Формат:

```json
{
  "items": [
    { "id": "scene.myflag", "tab": "Функции", "title": "Моя функция", "type": "toggle", "note": "пояснение" }
  ],
  "bindings": [
    { "group": "Робот", "action": "Моё действие", "keys": "Ctrl+K", "note": "пояснение" }
  ]
}
```

* `type`: `toggle` / `choice` / `info` (по коду `KvSettingsSchema`).
* Пункт появляется на указанной вкладке **сразу** при следующем построении панели настроек, без
  перекомпиляции.
* Чтобы пункт **работал**, его `id` надо добавить в обработчики менеджера:
  `KazistovVvUIManager.HasFeature` / `GetFeature` / `ApplyVisualization`. Пока id не обработан, пункт
  честно показывается как «нет обработчика».
* Строки `bindings[]` — только отображение (переназначение биндов в проекте не реализовано:
  в окне F12 кнопка «Переназначить» — заглушка).

### 9.2 Настройка через инспектор в PlayMode

Часть параметров вынесена в **публичные сериализуемые поля** и правится в инспекторе прямо во время
PlayMode (без перекомпиляции и без правок сцены, потому что компоненты создаются кодом):

| Компонент (объект) | Поля |
|---|---|
| `TrajectoryFlowController` (на `Main Camera`) | `trajectoryCount`, `distinctAttemptCap`, `detourVariants`, `distinctBudgetSeconds`, `tubeRadius`, `tubeSpreadStep`, `phantomCount`, `phantomSpeedMultiplier`, `robotMoveSpeed`, `pointMoveSpeed`, `pointMoveFastMultiplier`, `pointMoveOracleInterval`, `pointMoveHudEnabled`, `toolOffset` (**по умолчанию 0** — точка ставится ровно в точку попадания), `offsetMode` (`SurfaceNormal` / `WorldUpHack`), `alignToolToSurface`, `alignAngleToleranceDeg`, `logOffsetDiagnostics`, `logTcpFrame` |
| `FreeFlyCameraController` (на `Main Camera`) | `scrollStep` (0.08), `minDistance` (0.25), `maxDistance` (20), `stickyToSurface` (true), `surfaceLayer`, `scrollSmoothSpeed` (16), `surfaceStickTolerance` (0.005), `leftHandEnabled`, `rightHandEnabled`, `enableLaserPointer`, `suppressDirectTeleop` |
| `KazistovVvUIManager` (на `KazistovVv_UI`) | `treePanelWidth`, `propertiesPanelWidth`, `settingsPanelHeight`, `statusInterval`, `propertiesInterval`, `treeInterval`, `uiReferenceWidth/Height`, `logUiEvents` |
| `PointMoveHud` | `panelWidth`, `panelHeight`, `topOffset`, `sortingOrder`, `markerSize` |

### 9.3 Кнопка тулбара или пункт меню — без правок панелей

Кнопки и пункты меню строятся из **реестра команд**:

```csharp
KvCommands.Register(new KvCommand {
    Id = "my.command", Title = "Моё действие", Description = "что делает",
    Hotkey = "Ctrl+K", Icon = "settings", MenuPath = "Вид/Моё действие",
    Execute = delegate { /* действие */ },
    IsChecked = delegate { return false; },   // галка в меню
    IsEnabled = delegate { return true; },    // доступность кнопки
    Stub = false                              // true — «в разработке»
});
```

* Чтобы команда появилась **кнопкой**, её `Id` добавляется в группу тулбара
  (`Zones/KvToolbarGroups.cs` → `KvToolbarGroup.Commands`). Раскладка панели собирается из групп
  автоматически (`KazistovVvUIManager.ToolbarLayout()`), второй список вести не нужно;
  неизвестные id игнорируются, а команда, не попавшая ни в одну группу, уходит в «Прочее» — ни одна
  команда не теряется.
* Чтобы появилась **иконка**, id иконки должен существовать в `KvIcons` (`KvIcons.Has(id)`) или
  надо дорисовать новый `case` в `KvIcons.Draw` (примитивы `Line/Rect/Circle/Arc/Poly/Dot`,
  единая сетка: `GridSize = 24`, штрих 1.5 px).
* Пункт меню появляется сам — по строке `MenuPath`.
* Дамп реестра для диагностики: `KvCommands.Dump()`.

### 9.4 Другие данные, живущие вне кода

| Что | Где | Как применяется |
|---|---|---|
| Тема, язык, масштаб, плотность, раскладка окон/тулбара | `PlayerPrefs` (ключи `KazistovVv.*`: `KazistovVv.Theme.Mode`, `KazistovVv.Language`, `KazistovVv.Layout.<key>.*`, `KazistovVv.UI.ToolbarLayout`, `KazistovVv.UI.ToolbarCompact`, `KazistovVv.UI.ToolbarGroup.<id>`) | читаются при старте и при пересборке оболочки |
| Записи, позы, зоны, сессии, калибровка, макросы, правки материалов | §7.1 | JSON, читается `FeatureStorage` + `JsonUtility` |

---

## 10. Как добавить язык

1. Создать файл **`Assets/StreamingAssets/kazistovvv_i18n/<код>.json`** (например `it.json`).
   Формат (тот же, что у семи существующих словарей):

   ```json
   {
     "code": "it",
     "name": "Italiano",
     "english": "Italian",
     "cjk": false,
     "strings": [
       { "key": "cmd.point.select", "text": "Seleziona punto (laser rosso)" },
       { "key": "menu.file",        "text": "File" }
     ]
   }
   ```

   * `cjk: true` — только для языков с иероглификой (китайский/японский): тогда `KvTheme.Font`
     подбирает системный шрифт с CJK-глифами (`Microsoft YaHei UI`, `Meiryo`, `Yu Gothic`,
     `MS Gothic`, `SimHei`, `Noto Sans CJK …`), иначе текст будет «квадратами».
2. **Ничего перекомпилировать не нужно.** Каталог сканируется при старте классом
   **`KvLocalization`** (`KvLoc`, файл `06_KazistovVv_UI/03_Scripts/Core/KvLocalization.cs`), и язык
   сам появляется в списке «Настройки → Интерфейс» (плюс команда `lang.cycle`).
3. Цепочка подстановки строки: **каталог текущего языка → каталог `en` → русский текст из кода**
   (`KvLoc.T("ключ", "текст по умолчанию")`). Отсутствующий ключ не ломает интерфейс — вернётся
   текст, переданный кодом.
4. Выбор языка — в `PlayerPrefs` (`KazistovVv.Language`; значение `system` = брать
   `Application.systemLanguage`). Смена языка **мгновенная**: `KvLoc.Changed` →
   `KazistovVvUIManager.OnLanguageChanged` → пересборка оболочки + пересоздание шрифтов всех `Text`
   + обновление подписей окна функций (`KvFeatureWindow.RefreshLanguageLabels`). Перезагрузка не нужна.
5. Диагностика: `KvLoc.Status`, `KvLoc.CurrentCode`, `KvLoc.CountOf(code)`, `KvLoc.Reload()`
   (перечитать JSON без перезапуска).
6. **Строки, добавленные кодом.** Модули новых этапов добавляют свои тексты таблицами в коде
   (`Features/KvLocExtra.cs`, `KvLocExtra2.cs`, `KvLocExtra3.cs`) через `KvLoc.AddRuntimeStrings`.
   Такие строки регистрируются **только если ключа нет** во внешних словарях, то есть внешний JSON
   остаётся главнее — можно переопределить любой текст, не трогая код.
7. Язык **экспорта программ робота** (KUKA KRL / FANUC KAREL / ABB RAPID) — отдельная настройка
   (`KvRobotExport`), к языкам интерфейса отношения не имеет.

**Проверка после добавления языка:** сменить язык на новый и убедиться, что подписи меню, тулбара,
дерева и вкладок настроек изменились мгновенно, а в консоли нет исключений.

---

## 11. Тесты

### 11.1 Unit-тесты (появились 15.09.2026): `Assets/_Project/08_Tests/Editor/`

> **Состояние набора на 08.10.2026: 317 тестов** (`stage-a-final`: `317 / 315 / 0 / 2`, прогон
> воспроизведён дважды — `stage-a-restored` и `stage-a-final`). Таблица ниже — первая партия
> тестов 15.09.2026 (215 из 317); остальные описаны в §11.3 и §11.4.

| Файл | Тестов | Что проверяет |
|---|---:|---|
| `KvTrajMathTests.cs` | 44 | длины, кривизна, время, сглаживание, S-профиль, jerk, вырожденные и экстремальные входы |
| `KvTimeOptimalTests.cs` | 35 | S-профиль: лимиты скорости/ускорения/рывка по всем сэмплам, монотонность, клампы, сервис |
| `KvEnergyOptimalTests.cs` | 35 | энергия: эталон `τ·ω`, монотонность, аддитивность по суставам, экстремумы |
| `KvCalibrationTests.cs` | 30 | восстановление TCP из синтетики (ошибка 0,00036 мм), шум, вырожденные наборы |
| `KvPayloadCalculatorTests.cs` | 31 | моменты, лимиты, запас, модель и результат |
| `KvRobotExportTests.cs` | 40 | синтаксис KUKA KRL / FANUC KAREL / ABB RAPID: счётчики инструкций, баланс конструкций, формат чисел |

**Итого 215 тестов; прогон 15.09.2026: `total=215, passed=213, failed=0, skipped=2` (0,44 с).**

**Как запустить.**
- В редакторе: **Window → General → Test Runner → EditMode → Run All**.
- В batch-режиме (так проверялось):
  ```
  Unity.exe -batchmode -nographics -projectPath "<путь>" -runTests -testPlatform EditMode ^
            -testResults _stage5_tests.xml -logFile _stage5_tests.log
  ```
  Результат — `_stage5_tests.xml` в корне проекта (формат NUnit; `total/passed/failed/skipped` в атрибутах корневого `test-run`).

**Почему нет `.asmdef`.** В проекте **ни одного `.asmdef` нет** — код живёт в предопределённых сборках `Assembly-CSharp` (рантайм) и `Assembly-CSharp-Editor` (редактор). Ссылку на `Assembly-CSharp` из `.asmdef`-сборки получить **нельзя** (зависимость односторонняя), поэтому тесты размещены в подпапке `Editor` — тогда они автоматически попадают в `Assembly-CSharp-Editor`, у которой уже есть ссылки на `nunit.framework`, `UnityEngine.TestRunner` и `UnityEditor.TestRunner`. Отдельный `.asmdef` для тестов **не нужен и не создавался**.

**Что тестами не покрыто и почему.** Почти все сервисы модулей функций стоят за проверкой `flow.Validator.Ready`, а `PoseValidator.Ready` выставляется только в `Init(RobotController)` — то есть требует живого робота из сцены. Поэтому `KvCalibrationService.SolveTcp`, `KvTimeOptimal.Compute`, `KvEnergyOptimal.Compute`, `KvPayloadCalculator.Evaluate/JointTorques`, `KvRobotExport.Export` покрыты только защитными ветвями либо своим чистым ядром (`SolveToolOffset`, `SolvePlaneOffset`, `SProfileBuild`, `KvTrajMath.Energy`, `MeasureJerk`). Для полного покрытия нужны **PlayMode-тесты с загруженной `MainScene`** — это отдельная задача.

### 11.2 Сквозные батч-диагностики (основной способ проверки проекта)

`Assets/_Project/01_Scripts/Editor/Dsh*Diag.cs`, запуск `-executeMethod <Класс>.Run` — они
проходят полный рабочий цикл (точка → 8 траекторий → 8 фантомов → движение → режим перемещения →
Esc → аварийный стоп → запись → позы → интерфейс → производительность → стабильность → консоль)
и пишут отчёт `_dsh_*.txt`. Перечень — в §4.4. Именно эти прогоны проверяют проект «в сборе»;
unit-тесты (§11.1) дополняют их, проверяя математику по отдельности.

### 11.3 Тесты конвейера рендера (§29): `KvRenderPipelineTests.cs` — 18 тестов

Появились вместе с закрытием Б1 (URP глобальным умолчанием). Проверяют то, что нельзя проверить
глазами и что молча ломается:

| Что проверяется | Почему именно так |
|---|---|
| глобальное умолчание графики — **URP**, уровень «PC» — **HDRP**, уровень «Mobile» — URP, Android стартует на «Mobile» | иначе либо сборка под Android снова обрывается валидатором, либо компьютер теряет свой вид |
| оба набора глобальных настроек (HDRP и URP) привязаны | пропажа записи HDRP ломает десктопный вид молча |
| **настройка читается из ФАЙЛА проекта**, а не «из памяти редактора» | `SerializedObject` на словарях Unity 6 значений не отдаёт: проверка «в памяти» ничего не проверяла |
| цепочки запасных шейдеров **не пересекают конвейеры** | запас вида «URP → HDRP/Lit» — это тот же розовый материал, только другим путём |
| материал, созданный фабрикой, совместим с активным конвейером и получает заданный цвет | цвет пишется в `_BaseColor`/`_Color` — угадать имя нельзя, ошибка молчит |
| **в исходниках нет прямых `Shader.Find("HDRP/…")`** (кроме самой фабрики) | сторож против возврата к прежнему способу; комментарии при этом не считаются |
| в XR-сцене есть `KvXrSceneContent` и модели роботов; материалы XR-сцены совместимы с URP и без пропавших ссылок; материалы десктопной сцены — с HDRP | именно этого не хватало: §28 отчитался о начинке сцены, а сцена была пустой |

Запуск — как в §11.1. Дополнительно есть инструменты `Tools/KazistovVv Рендер/` в редакторе:
отчёт о конвейерах, аудит материалов («что станет розовым»), переключение умолчания, начинка
XR-сцены и перевод её материалов на URP; пункт **`7. Отсечь лишние варианты шейдеров (Б2)`** —
свой проход отсечения вариантов (§4.5).

### 11.4 Тесты Этапа А (08.10.2026): сборка, варианты шейдеров, политика URP, устройство

| Файл | Тестов | Что проверяет |
|---|---:|---|
| `KvBuildTests.cs` | 6 | возврат тестов из парковки (дефекты A2/A20): папка, пересозданная Unity **пустой**; папка с посторонними файлами — ничего не затирается; остаток **убитой** сборки не уничтожается |
| `KvShaderVariantTests.cs` | 5 | инварианты отсечения вариантов (Б2): в `DefaultVolumeProfile.asset` нет тестовых и потерявших скрипт компонентов; в сценах сборки нет камер с постобработкой; террейн-шейдеры выключены, а террейнов в проекте нет |
| **`KvUrpKeywordPolicyTests.cs`** | **4** | политика ключевых слов URP: проект сохраняет **используемые** слова; у **каждого** снятого слова есть причина; слова тумана и карт освещения снимаются только когда сцены их не используют; тени и доп. свет снимаются только когда это выключает сам ассет URP |
| `KvQualityLevelTests.cs` | 3 | разбор уровней качества: `m_PerPlatformDefaultQuality` — **словарь** (`Android: 0`, `Standalone: 1`), чтение и запись обеих форм; Android остаётся без HDRP-уровней |
| `KvDeviceTests.cs` | 4 | §31: идентификатор приложения — проектный (`com.KazistovVv.app`), XR-лоадер только OpenXR, профиль контроллеров для Android, состав XR-сцены для первого входа |

**Полный набор Этапа А — 317 тестов.** Числа прогонов (всего / прошло / упало / пропущено):
RED A2 `305 / 299 / 4 / 2` → GREEN A2 `308 / 305 / 1 / 2` → RED Б2 `313 / 307 / 4 / 2` →
GREEN Б2 `313 / 310 / 1 / 2` → после §31 `313 / 311 / 0 / 2` → `stage-a-restored` `317 / 315 / 0 / 2` →
**`stage-a-final` (повтор после сборки) `317 / 315 / 0 / 2`**. Гейт «чистой консоли» во всех
прогонах: `error CS` 0 · `warning CS` 0 · `Missing (Mono Script)` 0. Доказательства —
`Logs\editmode-stage-a-*.xml`, `Logs\ci-stage-a-*.log`, `Logs\ci-console-summary.json`.

**Проба состояния после сборки (A4):** `Tools/KazistovVv XR/8. Отчёт: состояние после сборки
(Этап А)` (`KazistovVvXrEditor.KvXrTools.ReportBuildReturnState`) печатает активную платформу,
умолчание графики, графический API Android, уровни качества с исключениями платформ и умолчаниями
по платформам, лоадеры XR, сцены сборки и где лежат тесты. Вывод — `Logs\stage-a-return-state.log`.
Одна команда «всё сразу»: `Tools\stage-a-verify.ps1` (проба A4 + гейт + EditMode-тесты).

---

## 12. Типовые проблемы и решения

### 12.1 На машине две копии проекта — и они разные

Симптом: «код в файлах есть, а в Unity его нет»; правки в одной копии не видны в редакторе.

* Рабочая копия агента и копия, открытая у пользователя в Unity, — **разные папки** с разными
  ветками git. Определить, какая копия открыта: `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"`
  → параметр `-projectpath`.
* Копии синхронизируются **только по скриптам**: сравниваются хеши `Assets/_Project/**/*.cs`
  (кроме `Editor/Dsh*Diag.cs` — диагностика агента не переносится), различающиеся файлы копируются
  вместе с `.meta`, заменяемые складываются в резерв `_dsh_backup_<дата>_<время>\`.
  Сцена, столы, роботы, материалы и `ProjectSettings` при синхронизации **не** переносятся.
* После синхронизации у второй копии может быть устаревший `Assembly-CSharp.csproj` — Unity
  перегенерирует его при следующем refresh.
* **Решение владельца 08.10.2026:** работать в **латинской** копии `C:\KazistovVv_Build`,
  а **в конце работ синхронизировать ИЗМЕНЁННЫЕ файлы** во вторую копию
  (`…\новое пространство\KazistovVv`). Копию не удалять (карточка Б10 в `PROBLEMS.md`).
  Для правок Этапа А путь проектных настроек сверялся снимком 57 файлов
  (`Logs\stage-a-snapshot-before.txt`), сверка — `Tools\stage-a-closeout.ps1 -Mode Compare`.

### 12.2 «Missing (Mono Script)» в сцене / скрипт не виден Unity

Причина в этом проекте — почти всегда **отсутствие `.meta`** (Unity ещё не импортировала файл) или
потерянный GUID при переименовании. Симптомы: у файла нет `.meta`, `Library/ScriptAssemblies/Assembly-CSharp.dll`
старше файла, в `Logs/Editor.log` нет упоминаний нового класса.

Решение:

1. `Assets → Refresh` (`Ctrl+R`) или любой прогон Unity в batch — Unity создаст `.meta`, пересоберёт
   `Assembly-CSharp.dll` и сама перегенерирует `*.csproj`.
2. Переименовывать `.cs` **только вместе с `.cs.meta`** — GUID внутри не меняется, и ссылки
   `m_Script: {guid: …}` в сцене продолжают работать. Класс внутри файла обязан совпадать с именем
   файла.
3. Дополнительно в сцене имя класса встречается в трёх местах: `m_Namespace` (блок `MonoScript`),
   `m_EditorClassIdentifier` (`Assembly-CSharp::Namespace.Class`) и `m_Name` объекта — править
   построчно.
4. «Фантомные» записи в Hierarchy, которых нет в сцене (`SCARA …`, `6-осевой робот …`, `Axis1..Axis6`):
   меню **`Tools/KazistovVv/Hierarchy/Очистить фантомные записи`**, затем
   **`Диагностика: объекты вне сцен`**; инвентарь роботов —
   **`Проверить инвентарь роботов (ожидается 2)`**; копии, созданные кнопкой «Добавить робота», —
   **`Удалить копии роботов, созданные через UI`** (по подтверждению). Иерархия обновляется сама
   (раз в 2 с, `Editor/HierarchyAutoRefresh.cs`), переключатель — в том же меню.

### 12.3 Кэш Library

* **`Library` не восстанавливают вместе с проектом** — Unity пересобирает её сама при первом
  запуске: **5–15 минут**. Не считать редактор «зависшим».
* Если `Library` нет, а нужно проверить компиляцию — `dotnet build` даст `CS0246` на
  `Text`/`Image`/`Button`. Это **не ошибки кода**, а отсутствие ссылок на `UnityEngine.UI.dll`;
  сначала один запуск Unity.
* Удалять `Library` «для чистоты» — крайняя мера: полный реимпорт HDRP-шейдеров занимает десятки
  минут. Сначала помогают `Assets → Refresh`, удаление `Library/ScriptAssemblies` или перезапуск
  редактора.

### 12.4 Batch-режим без графики

* `-nographics` **обязателен**: без него — полный реимпорт HDRP-шейдеров (десятки минут) и конфликт
  с открытым редактором пользователя.
* `No graphic device is available to initialize the view` — **ошибка среды, а не кода**. В отчётах
  диагностики она отделяется от ошибок кода (провал = только `LogType.Exception`).
* Без графики **не воспроизводятся**: скриншоты и видео (F8/F10), колесо мыши (глубина шарика),
  режим перемещения точки «под QWEASD», окна PiP с картинкой, помещение окружения, скелет рук,
  стрелки сил. Для этого добавлен общий страж `KvGraphics.Available`: без графики сервисы
  **считают, но не рисуют**. Проверять эти вещи нужно в живом редакторе.
* Прогон в batch **безопасен** рядом с открытым редактором **другой** копии проекта: у них разные
  `projectPath` → разные `Library`.
* Полезно знать: `EditorApplication.update` в batch вызывается чаще игровых кадров, поэтому
  ожидания в диагностиках считаются по `Time.frameCount`, а отчёты пишутся в файл сразу
  (`File.AppendAllText`), иначе буфер теряется при перезагрузке домена при входе в PlayMode.

### 12.5 Прочее, что уже случалось

| Симптом | Причина и решение |
|---|---|
| Batch-прогон падает сразу, лога нет, код возврата 1, в логе `Access token is unavailable` / `Code 10 while verifying Licensing Client signature` | залипший клиент лицензий: `Get-Process Unity.Licensing.Client \| Stop-Process -Force` |
| `Couldn't set project path`, лог уехал в обрезанный путь | `Start-Process -ArgumentList` не экранирует пробелы — передавайте строку одним элементом с внутренними кавычками |
| Диагностика пишет `[FAIL] менеджер … поднялся в PlayMode` | batch открыл пустую «последнюю» сцену: перед `EnterPlaymode()` открыть `MainScene.unity` |
| Отчёт `_dsh_*.txt` не дописывается во время PlayMode | особенность окружения (OneDrive) — читать итог из `_dsh_*.log` |
| `dotnet build` чист, а Unity выдаёт ошибку компиляции | компилятор Unity строже: например, `Object.GetInstanceID()` в Unity 6.5 — ошибка `CS0619`. Нужен хотя бы один прогон Unity |
| В консоли `[RobotInventory] Роботов в сцене: 3 …` | в открытой сцене есть лишние копии роботов (кнопка «Добавить робота» или сцена из `_Recovery`). Инвариант: **ровно 2** робота, рантайм их не создаёт |
| Кнопка «не работает» | (1) это заглушка (`KvCommand.Stub` — серая кнопка, по ТЗ); (2) команда недоступна по `IsEnabled` («Запуск/пауза» и «Остановка» — только когда робот едет); (3) интерфейс скрыт (`TAB`) |
| Сворачивание/раскладка панелей «сбилась» | команда **«Вид → Панели → Сбросить раскладку»** (`ui.resetlayout`) или кнопка в настройках: чистит и раскладку окон, и свёрнутые группы тулбара в `PlayerPrefs` |
| Сборка плеера падает/жалуется на ID приложения | **закрыто в §31:** идентификатор — свой, `com.KazistovVv.app` (для Android и Standalone). Если ID снова окажется шаблонным (`…urp-blank`) — проверить `ProjectSettings.asset` и перезапустить настройку `Tools/KazistovVv XR/5..7`, затем `Tools\stage-a-apk-check.ps1 -ExpectedPackage com.KazistovVv.app` |

---

## 13. Источники

Документ составлен по следующим файлам проекта:

| Файл / группа файлов | Что взято |
|---|---|
| `PROJECT_CONTEXT.md` (3183 строки) | история сессий, принятые решения, инварианты, известные баги, процедуры batch-прогонов, тексты команд и путей |
| `ProjectSettings/ProjectVersion.txt` | версия Unity 6000.5.6f1 (0e0577a1a2ac) |
| `ProjectSettings/ProjectSettings.asset` | `productName`, `companyName`, `bundleVersion`, `activeInputHandler`, `apiCompatibilityLevel`, `m_ActiveColorSpace`, разрешение по умолчанию, Android SDK, `applicationIdentifier` |
| `ProjectSettings/EditorBuildSettings.asset` | единственная сцена сборки — `Assets/_Project/00_Scenes/MainScene.unity`, настройки XR-загрузчиков |
| `Packages/manifest.json`, `Packages/packages-lock.json`, `Packages/io.realvirtual.starter/package.json` | список и версии зависимостей, встроенные и транзитивные пакеты |
| `Assets/_Project/00_Scenes/MainScene.unity` (поиск по файлу) | состав объектов сцены, привязки скриптов (`m_EditorClassIdentifier`, `m_Namespace`) |
| `Assets/_Project/01_Scripts/**` (134 `.cs`) | подпапки, имена классов, namespace, фактические значения по умолчанию (`TrajectoryFlowController.toolOffset = 0`, `robotMoveSpeed = 1/15`, `pointMoveSpeed = 0.5`, `detectAttemptCap`/`detourVariants`) |
| `Assets/_Project/06_KazistovVv_UI/03_Scripts/**` (35 `.cs`) | состав модуля интерфейса, `KvBindings.All()` (таблица биндов), `KvToolbarGroups.All` (8 групп), `KazistovVvUIManager` (жизненный цикл, `ToolbarLayout`, `RegisterCommands`), `KvLocalization` |
| `Assets/_Project/01_Scripts/Features/FeatureStorage.cs` | каталоги данных приложения |
| `Assets/_Project/01_Scripts/Features/{KvCinematics,KvReportPdf,KvRobotExport,KvRobotImport,KvPlannerLab,KvCaptures,KvAutomation}.cs` | пути «Документы», «Видео», `Bench`, `Macros` |
| `Assets/StreamingAssets/kazistovvv_settings.json`, `Assets/StreamingAssets/kazistovvv_i18n/*.json`, `Assets/StreamingAssets/robots/*` | внешние данные, форматы настроек и словарей |
| `Assets/_Project/Docs/TrajectoryAlgorithmPlan.md` | исходный план алгоритма траекторий |
| `.gitignore`, `.gitattributes`, `ignore.conf` | правила VCS |
| перечень файлов в `Assets/_Project/*`, `Assets/*`, `Packages/*`, корне проекта | фактическая карта репозитория (включая пустые папки и остатки `*.csproj`) |
| `Assets/_Project/01_Scripts/Editor/KvXrTools.cs`, `KvRenderPipelineTools.cs`, `KvShaderVariantStripper.cs`, `KvXrDeviceTools.cs` | сборка APK, парковка/возврат тестов, проба состояния (A4), отсечение вариантов шейдеров (Б2), настройки §31 |
| `Tools/stage-a-*.ps1`, `Tools/xr-checks.ps1` | инструменты Этапа А: сборка с доказательствами (в `stage-a-build-apk.ps1` — `GRADLE_USER_HOME` латиницей), проверка APK, возврат тестов, снимок/сверка состояния, шаги §31, гейт + EditMode-тесты |
| `Tools/_stage_a/СОСТОЯНИЕ_ЭТАПА_А.md` | рабочее состояние сессии «Этап А»: что сделано, что осталось, чем доказано (плюс черновики отчёта и текстов инструментов) |
| `Assets/_Project/01_Scripts/Editor/KvUrpKeywordPolicy.cs` | политика отсечения ключевых слов URP с причиной для каждого слова (настоящее решение Б2: `URP/Lit` 442 368 → 1 536) |
| `Logs/xr-build-apk-s30.log`, `Logs/xr-build-apk-s31.log`, `Logs/stage-a-*.txt/.md`, `Logs/s31-*.log`, `Logs/platform-return.log`, `Logs/ci-stage-a-*.log`, `Logs/ci-console-summary.json` | числа Этапа А: варианты шейдеров до/после отсечения, возврат тестов, состояние после сборки, идентификатор и профиль §31, возврат платформы, гейт «чистой консоли» |
