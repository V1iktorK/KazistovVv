# ОТЧЁТ ПО AR (дополненная реальность)

_Правило проекта: документ без числа — это мнение, а не отчёт._
_План — `Docs/ПЛАН_AR.md`. Причины решений — `Docs/ЖУРНАЛ_РЕШЕНИЙ_AR.md`._

**Сессия:** S46, 02–03.11.2026. **Агент:** AR. **Задача:** `Docs/ПРОМТ_АГЕНТУ_AR.md`.
**Параллельно:** агент MR (закончил 02.11, `Docs/ОТЧЁТ_MR.md`); файлы разделены по
`Docs/КООРДИНАЦИЯ_АГЕНТОВ_MR_AR.md`.

**Статус:** заполняется. Реализация и сцены готовы, идёт приёмка (тесты/APK/устройство).

---

## 1. ЧТО СДЕЛАНО (по блокам S2)

| Блок | Что сделано | Где в коде |
|---|---|---|
| **А. Камера и плоскости** | AR-сцена с `ARSession` + `XR Origin` + `ArCamera` (`ARCameraManager`, `ARCameraBackground`, `TrackedPoseDriver`); `ARPlaneManager` с визуализацией; маркер под центром экрана по лучу `ARRaycastManager`; строка A3 `РЕЖИМ: ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ` в журнале | `KvArSceneBuilder.cs`, `KvArPlacement.cs`, `KvArRuntime.cs`, `KvArBoot.cs` |
| **Б. Привязка к реальному столу** | Тап по плоскости → `ARAnchorManager.AttachAnchor(ARPlane, Pose)` (запасной путь `TryAddAnchorAsync`) → `ArLabRoot` ребёнком якоря → робот основанием ровно в точку стола. Поза робота от камеры не зависит | `KvArWorld.cs`, `KvArPlacement.cs` |
| **В. Масштаб 1:1** | Робот не масштабируется (`localScale = 1`), высота измеряется и печатается; виртуальный стол проекта 0,72 × 0,504 = 10:7, как реальный 100 × 70 см | `KvArContent.cs`, `KvArPlacementMath` |
| **Г. Касания** | Тап по меню (луч → `KvXrGroupPanel.TryAim`/`PressIndex`), выбор объекта (кольцо + статус), постановка тапом, подтверждение точки в `TrajectoryFlowController.UpdateAim`, поворот вида перетаскиванием, щипок = масштаб ВИДА (FOV), отмена двумя пальцами, режим «Двигать точку» | `KvArTouchInput.cs`, `KvArRigBridge.cs` |
| **Д. Весь функционал** | Меню 110 команд, каталог 12 моделей (`KvRobotSpawner`), двурукий, лазеры/фантомы/траектории (поток шлема), столы/автостол (`KvXrObjectFactory` + AR-хуки), удаление, экспорт — те же системы, что в шлеме | `KvArRuntime.cs`, `KvArTouchInput.cs`, `KvXrObjectFactory.cs` (хуки) |
| **Е. Производительность** | `Application.targetFrameRate = 30`, `vSyncCount = 0`, строка `кадр/с` в журнале состояния | `KvArRuntime.cs` |
| **Ж. Отказ** | `Unsupported` → экран Ж1; `NeedsInstall` → кнопка установки ARCore; нет камеры → Ж2; `notTrackingReason` → подсказки. Приложение не падает | `KvArSessionController.cs`, `KvArBoot.cs` |

**Сборка приложения:** одно приложение, два режима. `BootAr` (сцена 0) выбирает сцену по активному
XR-лоадеру: OpenXR → `XrMain` (VR/MR как раньше), ARCore → `ArMain` (AR). ARCore-провайдер
`com.unity.xr.arcore` 6.5.0 добавлен (в проекте его не было), `ARCoreLoader` включён ВТОРЫМ
после OpenXR, ARCore Requirement = Optional. Рендер-фича AR для Vulkan добавлена в мобильный URP-рендерер.

---

## 2. ЧЕМ ДОКАЗАНО (числа и строки)

### 2.1. Тесты (EditMode, batchmode)

- До начала работ (базовая линия MR): **1051 из 1051** зелёные, 0 пропусков.
- После добавления AR: **(заполняется после прогона ar2)**.
- Новых тестов: **60** в двух файлах `KvArModeTests.cs` и `KvArPlacementTests.cs`.
- Тестовые файлы: было 103, стало **105** (счётчик в `ИТОГ_НОЧИ_S44.md` обновлён).

### 2.2. Сборка сцен (Unity batchmode, `Logs/ar-builder4.log`)

```
[KvArBuilder] ArMain: сцена скопирована из Assets/_Project/00_Scenes/XrMain.unity
[KvArBuilder] ArMain: выключен KvXrHeadTracking на «XrCamera»
[KvArBuilder] ArMain: выключен KvXrLocomotion на «XrRig»
[KvArBuilder] ArMain: выключен KvXrMrRoom на «XrMrRoom»
[KvArBuilder] ArMain: выключен KvXrHangarAnchor на «XrSceneContent»
[KvArBuilder] ArMain: выключен KvXrSceneContent на «XrSceneContent»
[KvArBuilder] ArMain: логическая голова «XrCamera» — рендер выключен
[KvArBuilder] ArMain: ангар «XrHangar» погашен
[KvArBuilder] ArMain: AR-риг добавлен (ARSession, XR Origin, ArCamera, ARPlaneManager, ARAnchorManager, ARRaycastManager)
[KvArBuilder] ArMain: сохранена · BootAr: сцена создана
[KvArBuilder] Build Settings: сцен 4 · первая — Assets/_Project/00_Scenes/BootAr.unity
[KvArBuilder] рендерер Assets/Settings/Mobile_Renderer.asset: добавлена фича AR-фона (Vulkan)
[KvArBuilder] ГОТОВО
```
Сцены: `ArMain.unity` 243 541 байт, `BootAr.unity` 7 158 байт; порядок сборки:
`BootAr → XrMain → MainScene → ArMain`.

### 2.3. Импорт пакета-провайдера ARCore

`Library/PackageCache/com.unity.xr.arcore@faa8081af93f` — установлен; `packages-lock.json` обновлён;
`Assets/XR/Loaders/ARCoreLoader.asset` (guid `36b59452d5fab7a4d…`) включён в Android Providers вторым.

### 2.4. Сборка APK и проверка на устройстве

_Заполняется после сборки и `adb logcat`._

---

## 3. ЧТО ПРОВЕРЕНО НА УСТРОЙСТВЕ, А ЧТО В РЕДАКТОРЕ

| Что | Где проверено |
|---|---|
| Компиляция всех скриптов (0 ошибок CS) | редактор, batchmode |
| Сборка сцен `ArMain`/`BootAr` | редактор, batchmode (лог выше) |
| Порядок лоадеров, пакет ARCore, Optional, рендер-фича | EditMode-тесты |
| Математика постановки: основание 5 мм, поза не зависит от камеры, стол 10:7, масштаб 1:1 | EditMode-тесты |
| Отказы Ж1/Ж2 (сообщения и состояния) | EditMode-тесты |
| Пути касаний/меню/потока (те же функции, что в шлеме) | EditMode-тесты + разбор исходников |
| **VR не сломан (PICO)** | _заполняется после установки APK: `adb logcat` — BootAr → XrMain, режим VR/MR_ |
| Камера/плоскости/якорь/робот на столе | **только владелец на планшете** (чек-лист S2) |
| Кадры/батарея/память (Е) | **только на устройстве** (владелец) |

---

## 4. КОММИТЫ

| Хеш | Сообщение |
|---|---|
| _заполняется_ | `[AR] …` |

---

## 5. ЧТО ОСТАЛОСЬ

_Если ничего — «ничего». Если есть — это БЛОКЕР, с причиной и тремя испробованными подходами._

**Важно (R1):** целевой планшет владельца — Redmi Pad 2 Pro. В списке сертифицированных Google
(`developers.google.com/ar/devices`, 1708 устройств) есть «Redmi Pad 2 Pro 5G», но Wi-Fi-версии
«Redmi Pad 2 Pro» в списке нет. Если планшет не сертифицирован под ARCore — AR на нём не запустится
(приложение честно покажет сообщение Ж1, без падения), и для приёмки по чек-листу S2 нужен другой
ARCore-телефон/планшет. Проверка: Play Маркет → «Google Play Services for AR».
