# КООРДИНАЦИЯ ДВУХ АГЕНТОВ: MR и AR (02.11.2026)

**Зачем этот файл.** В одном проекте `KazistovVv` параллельно работают два агента:
- **MR-агент** — чат «Запуск промта агента MR» (`Docs/ПРОМТ_АГЕНТУ_MR.md`);
- **AR-агент** — чат «Промт агенту AR» (`Docs/ПРОМТ_АГЕНТУ_AR.md`).

Владелец 02.11.2026 выбрал режим «параллельно, с разделением файлов». Этот файл — договор:
кто какие файлы трогает, как делить единственный Unity и сборку APK. **Оба агента обязаны его
прочитать и соблюдать.** AR-агент ведёт файл и дописывает изменения; MR-агенту при споре — писать
сюда же и передавать владельцу.

---

## 1. РАЗДЕЛЕНИЕ ФАЙЛОВ

### 1.1 Только AR-агент (MR не трогает)

| Что | Файлы |
|---|---|
| Новый код AR | `Assets/_Project/01_Scripts/Ar/**` |
| Новые редакторные инструменты AR | `Assets/_Project/01_Scripts/Editor/KvAr*.cs` |
| Новые тесты AR | `Assets/_Project/08_Tests/Editor/KvAr*.cs` |
| Новые сцены | `Assets/_Project/00_Scenes/ArMain.unity`, `Assets/_Project/00_Scenes/BootAr.unity` |
| Документы AR | `Docs/*AR*` |
| Общие файлы (правит ТОЛЬКО AR) | `Packages/manifest.json`, `Packages/packages-lock.json` (пакет `com.unity.xr.arcore`)<br>`Assets/XR/XRGeneralSettingsPerBuildTarget.asset` (ARCoreLoader вторым)<br>`Assets/Plugins/Android/AndroidManifest.xml` (камера, ARCore optional)<br>`ProjectSettings/EditorBuildSettings.asset` (сцены: BootAr, XrMain, MainScene, ArMain)<br>`Assets/_Project/08_Tests/Editor/KvMrArReadinessTests.cs` (новый инвариант «OpenXR первый + ARCore второй»)<br>`Assets/_Project/01_Scripts/Xr/KvXrObjectFactory.cs` (хук постановки, по умолчанию выключен — VR не меняется) |

### 1.2 Только MR-агент (AR не трогает)

`Assets/_Project/01_Scripts/Xr/KvMr*.cs`, `KvXrMr*.cs`, `KvXrCapabilities.cs`, `KvXrActions.cs`,
`KvXrSettings.cs`; `Assets/_Project/01_Scripts/Editor/KvMrSetupTools.cs`,
`Assets/_Project/01_Scripts/Editor/KvPicoManifestMetaData.cs`; `Assets/_Project/00_Scenes/XrMain.unity`;
`Assets/XR/Settings/OpenXR Package Settings.asset`; `Assets/XR/Loaders/*`; `Docs/*MR*`.

### 1.3 Что считается «своим» для обоих

`Assets/_Project/01_Scripts/Editor/KvXrTools.cs`, `KvXrHangarTools.cs`, `Tools/*.ps1` — правит тот,
кому действительно нужно; **чужие правки не откатывать**, а сообщать владельцу.

---

## 2. ЕДИНСТВЕННЫЙ UNITY (важно: Library ОДНА)

`C:\KazistovVv_Build` связан junction-ом с `Library` исходного проекта (`Tools/xr-build-apk-ascii.ps1`).
Поэтому **Unity на `C:\KazistovVv_Build` и на исходном проекте одновременно работать не может**:
второй процесс получает «Exiting … return code 1» или портит Library.

### 2.1 Блокировка (обязательна перед ЛЮБЫМ запуском Unity)

```powershell
# взять блокировку (ждать до 2 часов)
$lock = 'C:\KazistovVv_Build\.unity_lock'
$deadline = (Get-Date).AddHours(2)
while (Test-Path $lock) {
    if ((Get-Date) -gt $deadline) { throw 'Unity lock timeout' }
    Start-Sleep -Seconds 15
}
New-Item -ItemType Directory -Path $lock -Force | Out-Null
Set-Content -Path (Join-Path $lock 'owner.txt') -Value ("AR / " + (Get-Date -Format 'HH:mm:ss'))

# ... здесь запуск Unity / сборка APK / тесты ...

Remove-Item -Recurse -Force $lock   # отпустить в конце (в finally)
```

Правила:
1. Перед запуском Unity — проверить `Get-Process -Name Unity`; если Unity уже запущен, но блокировки
   нет (старый процесс без протокола) — **ждать**, не убивать.
2. Блокировку держать до конца прогона; отпускать сразу.
3. Если каталог блокировки старше 3 часов — считается зависшим, его можно удалить, предварительно
   убедившись, что процессов Unity нет.

### 2.2 Сборка APK

`Tools/build-android.ps1` → `Tools/xr-build-apk-ascii.ps1` собирает в `C:\KazistovVv_Build` и пишет
`C:\KazistovVv_Build\Builds\KazistovVv.apk`. Сборка **перезаписывает** APK — одновременно две сборки
невозможны. Очередность: та же блокировка.

---

## 3. ПРАВИЛА, ЧТОБЫ НЕ СЛОМАТЬ ДРУГ ДРУГУ

1. **Закоммиченное — свято:** не делать `git checkout --`, `git reset`, `git stash` по чужим файлам.
2. **Коммиты:** только свои файлы (`git add <свои пути>`), сообщение с префиксом `[AR]` или `[MR]`.
3. **Держать дерево собираемым:** не оставлять синтаксически сломанный C# после своего шага;
   проверять компиляцией при ближайшем прогоне Unity.
4. **Если нужен чужой/общий файл** — сначала записать намерение в этот файл и передать владельцу;
   не править молча.
5. **Стартовая сцена APK меняется на `BootAr.unity` (AR).** На шлеме она грузит `XrMain` как раньше
   (проверяется на живом PICO). Если VR-поведение изменилось — это дефект AR, сообщать владельцу.
6. **На шлеме активным лоадером остаётся OpenXR** (он первый в списке). ARCore — второй, для телефонов.
7. **Тест `KvMrArReadinessTests` теперь проверяет «OpenXR первый + ARCore вторым»** — это осознанное
   изменение AR-агента, MR-агент его не откатывает.
8. **ArMain — копия XrMain на момент сборки сцены.** Если MR-агент существенно меняет XrMain,
   AR-агент пересобирает ArMain из актуальной XrMain (этап 3 плана AR).

---

## 4. СОСТОЯНИЕ НА 02.11.2026 22:05

| Что | AR-агент | MR-агент |
|---|---|---|
| Этап | план согласуется, начинается этап 0 | правит `KvMr*.cs`/`KvXrMr*.cs`, запускает сборку APK |
| Unity | ждёт освобождения Unity | сборка APK с 21:44 на `C:\KazistovVv_Build` |
| APK | — | `Builds\KazistovVv.apk` (сборка в процессе) |
| План | `Docs/ПЛАН_AR.md` | `Docs/ПЛАН_MR.md` |
