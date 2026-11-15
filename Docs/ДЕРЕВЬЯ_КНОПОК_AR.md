# ДЕРЕВЬЯ КНОПОК AR (планшет, Android) — глубокий разбор по коду, v2

Версия: 13.11.2026 (v2). Часть каталога `Docs/КОНСПЕКТ_КНОПОК_таблица.md` (v1) — разбор **AR-приложения
на планшете**: сцена `ArMain`, экран в ПК-вид (меню сверху, дерево слева, правый ряд AR-кнопок).

**Источники (только чтение, ничего в проекте не менялось):**

- код: `Assets/_Project/01_Scripts/Ar/` — все 14 `.cs` (`KvArUi`, `KvArRuntime`, `KvArPlacement`,
  `KvArTouchInput`, `KvArCameraGate`, `KvArBoot`, `KvArSessionController`, `KvArWorld`, `KvArContent`,
  `KvArPlaneVisualizer`, `KvArRigBridge`, `KvArMode`, `KvArLoaderBootstrap`, `KvArGyroFallback`);
- оболочка ПК-интерфейса, которую AR включает себе: `06_KazistovVv_UI/03_Scripts/`
  (`KvMenuBar`, `KvToolbar`, `KvToolbarGroups`, `KvCommands`, `KvDockPanel`, `KvWidgets`,
  `KazistovVvUIManager`, `KvRobotCommands`);
- реестр команд и обработчики: `01_Scripts/Features/FeatureHub.cs`, `KvStageHub.cs`,
  `KvStageHub2.cs`, `KvStageHub3.cs`;
- самопрогон: `01_Scripts/Diagnostics/KvUiSweepRuntime.cs` (перечислитель элементов, лимиты, исключения);
- замер: `Logs/uisweep_reports/_ui_sweep_ar.txt` (**351 строка**, `ArMain`, 11.11.2026, `макет=ar`,
  `устройство=Android`) и машинный инвентарь `Logs/uisweep_reports/inventory.json` (140 записей `layout=ar`).

**Обозначения:** ✓ — сработало по замеру (`ok=1` без исключения); ✓! — `ok=1`, но прогон пометил
элемент исключением; ✗ — не нажато (`ok=0`); «—» — элемент **не попал в инвентарь прогона**
(лимит `MaxElements=140` либо создался после сборки списка). Цитаты `effect=` даются дословно
из отчёта с номером строки (`отчёт:N`).

---

## 0. ИТОГИ ОХВАТА (числа)

| Показатель | Значение | Где видно |
|---|---|---|
| Строк в отчёте | **351** — разобраны все | `_ui_sweep_ar.txt` |
| Элементов в инвентаре AR | **140** (`элементов 140 · сработало 99 · без эффекта 0 · исключений 41 · исчезло 0`) | отчёт:342 |
| Нажатий (`press=`) | **118** за весь отчёт (106 штатным `uGUI` + 12 с `interactable=false`): 117 в первом проходе (отчёт:58–292) + 1 во втором (отчёт:309); 22 элемента нажатий не имеют вовсе | отчёт:58–309 |
| Строк `element=` | **140** (каждый элемент отчёта, 59–338) | отчёт:59–338 |
| Сработало без исключения | **99** | отчёт:342 |
| Исключений | **41** = 12 недоступных + 21 в скрытой панели + 5 «нечего ставить» + 1 «вне панелей» + 1 калибровка + 1 закрывающая плашка | отчёт:13–55, 345 |
| «Без эффекта» / «исчезло» | **0 / 0** | отчёт:342 |
| Панелей, которые не открыла ни одна кнопка | **3**: `ArUiCanvas/PcCommandPanel`, `ArUiCanvas/ArMessage`, `KazistovVvCanvas` | отчёт:346–349 |
| Предел инвентаря | **`KvUiSweepRuntime.MaxElements = 140`** (`KvUiSweepRuntime.cs:2720`) | код |
| Элементов в деревьях **по коду** | **660** кнопок в 4 корнях (см. §1 и §3.4) | этот документ |
| Из них замерено | **140 из 660 (21 %)** — остальные не попали в прогон | §3 |

**Главная находка по охвату.** Отчёт честно пишет «проверено объектов 140 из найденных 140»
(отчёт:343), но 140 — это не размер интерфейса, а **жёсткий предел перечислителя**
(`MaxElements = 140`, `KvUiSweepRuntime.cs:2720`, комментарий: «предел числа проверяемых элементов»).
Сбор останавливается на 140-м `Selectable`, поэтому в AR-инвентарь не попали: 41 кнопка группового
тулбара оболочки (группы «Сеть», «Настройки», «Прочее» целиком + хвост «Записи и экспорта»),
все 75 пунктов выпадающих меню, все 122 строки команд AR-панели вместе с пагинацией, сегменты
«Верстак», выпадающие списки групп и кнопки шапок трёх док-панелей из четырёх. Это **не дефект
кнопок**, а граница замера — она честно названа здесь и в §3.

---

## 1. ДЕРЕВЬЯ AR-ИНТЕРФЕЙСА

Корней (верхнеуровневых окон) в AR-раскладке **четыре**. Порядок в дереве — порядок создания в коде.

### 1.1 Корень A — `ArRuntime/ArUiCanvas` (собственный ПК-вид планшета)

`KvArUi.EnsureBuilt()` (`KvArUi.cs:189`) создаёт `ArUiCanvas` (ScreenSpaceOverlay, `sortingOrder = 300`)
под объектом `ArRuntime`. Это главный экран AR: верхнее меню, строка инструментов, левое дерево,
правый ряд кнопок, окно сообщений и панель команд.

```
ArRuntime/ArUiCanvas                                  [KvArUi, overlay, order 300]
├── PcMenuBar                                         верхнее меню, 6 кнопок (KvArUi.cs:250–270)
│   ├── Menu_Файл«Файл»           → ToggleMenu("Файл")      KvArUi.cs:266/635      ✓
│   ├── Menu_Правка«Правка»       → ToggleMenu("Правка")                               ✓
│   ├── Menu_Вид«Вид»             → ToggleMenu("Вид")                                 ✓
│   ├── Menu_Робот«Робот»         → ToggleMenu("Робот")                               ✓
│   ├── Menu_Сервис«Сервис»       → ToggleMenu("Сервис")                              ✓
│   └── Menu_Справка«Справка»     → ToggleMenu("Справка")                             ✓
├── PcMenuDropdown                                    выпадающее меню, создаётся при открытии
│   ├── MenuTitle (текст, не кнопка)
│   ├── MenuItem_<commandId> × 75                     по 6 разделам — раскрыто в §2.2
│   │     Файл 12 · Правка 6 · Вид 15 · Робот 20 · Сервис 18 · Справка 4
│   └── (высота строк сжимается под экран: KvArUi.cs:673–679)
├── PcToolbar                                         строка инструментов, 13 кнопок (KvArUi.cs:273–308)
│   ├── Tool_workspace.clear«Очистить»   → Execute("workspace.clear")   ✓ (подтверждение в ArMessage)
│   ├── Tool_shot.screenshot«Скриншот»   → Execute("shot.screenshot")   ✓
│   ├── Tool_record.toggle«Запись»       → Execute("record.toggle")     ✓
│   ├── Tool_estop«Стоп»                 → Execute("estop")             ✓
│   ├── Tool_tool.grid«Сетка»            → Execute("tool.grid")         ✓
│   ├── Tool_view.limits«Лимиты»         → Execute("view.limits")       ✓
│   ├── Tool_view.metrics«Метрики»       → Execute("view.metrics")      ✓
│   ├── Tool_view.workspace«Раб.зона»    → Execute("view.workspace")    ✓
│   ├── Tool_ui.settings«Настройки»      → Execute("ui.settings")       ✗ (правило прогона, §4.5)
│   ├── Tool_view.panel.tree«Объекты»    → Execute("view.panel.tree")   ✓
│   ├── Tool_file.export«Экспорт»        → Execute("file.export")       ✓
│   ├── Tool_session.save«Сохранить»     → Execute("session.save")      ✓
│   └── Tool_demo.quick«Демо»            → Execute("demo.quick")        ✓! (исключение «нечего ставить»)
├── PcTreePanel                                       левое дерево проекта, 10 строк (KvArUi.cs:311–359)
│   ├── Tree_6-осевой робот«▸ 6-осевой робот»          → TreeAction("robot.model.stand_6axis")      ✓
│   ├── Tree_SCARA LS10-B702S«▸ SCARA LS10-B702S»      → TreeAction("robot.model.scara_ls10b702s")  ✓
│   ├── Tree_Квадратный стол«▸ Квадратный стол»        → TreeAction("table.square")                ✓!
│   ├── Tree_Авто стол«▸ Авто стол»                    → TreeAction("table.auto")                  ✓!
│   ├── Tree_Траектория«▸ Траектория»                  → TreeAction("group:path")                  ✓
│   ├── Tree_Постобработка«▸ Постобработка»            → TreeAction("group:post")                  ✓
│   ├── Tree_Ограничения«▸ Ограничения»                → TreeAction("view.limits")                 ✓
│   ├── Tree_Калибровка«▸ Калибровка»                  → TreeAction("calib.tab")                   ✓!
│   ├── Tree_Этапы 13–36«▸ Этапы 13–36»                → TreeAction("features.open")               ✓
│   └── Tree_Все команды«▸ Все команды»                 → TreeAction("group:robot")                 ✓
│                                                    (это нажатие и раскрыло панель команд, §2.3)
├── PcCommandPanel                                    окно «Команды», скрыто по умолчанию (KvArUi.cs:424–486)
│   ├── CommandTitle («Команды · <группа> (N)») / CommandPage («1 / N») — тексты
│   ├── GroupTab_robot«Робот»        → OpenCommands("robot")   ✓   (замер при открытой панели)
│   ├── GroupTab_path«Траектория»    → OpenCommands("path")    ✗   (панель закрыта, §3)
│   ├── GroupTab_post«Постобр.»      → OpenCommands("post")    ✗
│   ├── GroupTab_view«Визуал.»       → OpenCommands("view")    ✗
│   ├── GroupTab_export«Экспорт»     → OpenCommands("export")  ✗
│   ├── GroupTab_net«Сеть»           → OpenCommands("net")     ✗
│   ├── GroupTab_ui«Настройки»       → OpenCommands("ui")      ✗
│   ├── GroupTab_misc«Прочее»        → OpenCommands("misc")     ✗
│   ├── CloseCommands«Закрыть»       → ClosePanels()           ✗
│   └── CommandRows (создаёт FillCommandRows, KvArUi.cs:720–765)
│       ├── Cmd_<commandId> × 8 на странице (всего 122 команды по 8 группам) — §2.3
│       └── CmdPrev« ◀» / CmdNext« ▶» — только при страницах > 1 (7 групп из 8)
├── ArButtons                                         правый верхний ряд (KvArRuntime.BuildScreenButtons:845)
│   ├── ArButton_Поставить«Поставить»       → placement.TryPlaceAtCenter()      ✓! (0 плоскостей)
│   ├── ArButton_Переставить«Переставить»   → placement.RePlace()               ✓! (0 плоскостей)
│   ├── ArButton_Команды«Команды»           → ui.OpenCommands("robot")          ✓
│   └── ArButton_Сброс вида«Сброс вида»     → touch.ResetView()                 ✓
├── ArMessage                                         модальное окно (показывается всегда, скрыто)
│   ├── ArMessageText (текст сообщения)
│   ├── ArMessageButton            → OnMessageButtonClicked (KvArUi.cs:1115)   ✓ (второй проход)
│   │     подписи: «ОЧИСТИТЬ» / «РАЗРЕШИТЬ КАМЕРУ» / «ОТКРЫТЬ НАСТРОЙКИ» / «Продолжить в VR» …
│   └── ArMessageCancel«ОТМЕНА»   → OnMessageCancelClicked (KvArUi.cs:1124)   ✗ (не нажималась, §4.1)
├── ArHint / ArHintText                              нижняя подсказка — текст, не кнопка
└── PcStatusBar: FooterLeft / FooterRight            строка состояния — тексты, не кнопки
```

### 1.2 Корень B — `ArRuntime/KvArCameraGateCanvas` (шлюз «ДОСТУП К КАМЕРЕ»)

Создаётся `KvArCameraGate.BuildUi()` (`KvArCameraGate.cs:663`), `sortingOrder = 450` (выше главного экрана).

```
ArRuntime/KvArCameraGateCanvas                       [KvArCameraGate, overlay, order 450]
└── GatePanel
    ├── GateTitle«ДОСТУП К КАМЕРЕ»    — текст
    ├── GateStatus                     — текст (состояния: «Проверяю доступ…», «КАМЕРА НЕ РАЗРЕШЕНА…»,
    │                                    «Камера работает: получено кадров N…», «КАМЕРА: кадры идут, но
    │                                    изображение ПУСТОЕ…», «Камера включена (резервный режим)…»)
    └── GateButton  → OnActionPressed (KvArCameraGate.cs:724/744)   ✗ не нажимался
          подписи (одна кнопка, три состояния):
          «РАЗРЕШИТЬ КАМЕРУ» (KvArCameraGate.cs:739) → системный диалог Android
          «НАСТРОЙКИ ПРИЛОЖЕНИЯ» (KvArCameraGate.cs:772) → android.settings.APPLICATION_DETAILS_SETTINGS
          «ПРОДОЛЖИТЬ» (KvArCameraGate.cs:284) → ContinueToAr (KvArCameraGate.cs:761)
        + любое касание экрана = «ПРОДОЛЖИТЬ» (KvArCameraGate.cs:196),
          автопродолжение через 15 с (KvArCameraGate.cs:210) и снятие окна через 20 с трекинга
          из KvArRuntime.CheckCameraGate (KvArRuntime.cs:367–385)
```

Прогон **никогда не нажимает** этот корень: правила `KvUiSweepExemptions` («системный шлюз»,
`KvUiSweepRuntime.cs:2124–2151`) запрещают `GateButton`/`GatePanel`/`KvArCameraGateCanvas`
и все подписи «разрешить камеру / allow camera / grant permission» — нажатие уводит приложение
в системный диалог и обрывает прогон (отчёт:47–55).

### 1.3 Корень C — `KazistovVv_UI/KazistovVvCanvas` (оболочка ПК-интерфейса в AR)

Оболочка **не своя для AR**: её включает `KvArRuntime.EnablePcUiForTablet()` (`KvArRuntime.cs:946`)
после трекинга (или через 8 с, корутина `EnablePcUiAfterTracking`, `KvArRuntime.cs:672`), а до этого
`HideForeignCanvases()` (`KvArRuntime.cs:744`) её держит выключенной. Именно поэтому в AR живут
**два** набора меню/тулбара: свой (`ArUiCanvas`) и оболочки.

```
KazistovVv_UI/KazistovVvCanvas                       [KazistovVvUIManager, overlay, order ≥ 40]
├── KvMenuBar                                         6 кнопок (KvMenuBar.cs:42–110)
│   ├── Menu_Файл / Menu_Правка / Menu_Вид / Menu_Робот / Menu_Сервис / Menu_Справка
│   │        → ToggleMenu(menu) (KvMenuBar.cs:93/135) → BuildDropdown (KvMenuBar.cs:171)    ✓ все 6
│   └── Info (текст «имя проекта»)
├── KvMenuBlocker                                     закрывающая плашка (KvMenuBar.cs:118–132)
│        → CloseMenu() (KvMenuBar.cs:129/163)         ✓! «меню не открыто — нажимать нечего» (отчёт:111)
├── KvMenuDropdown                                    создаётся при открытии (KvMenuBar.cs:175)
│   ├── Item_<подпись> × 122                          пункты всех команд, у которых есть MenuPath (§2.4)
│   │        → CloseMenu + KvCommands.Invoke(id) (KvMenuBar.cs:329–334)
│   └── Group/заголовки и Sep/разделители — тексты и картинки, не кнопки
├── KvToolbar                                         (KvToolbar.cs:104–172, раскладка RebuildGrid:185)
│   └── Groups                                        (KvToolbar.cs:129)
│       ├── Group_robot … Group_misc  × 8 ручек       → ShowGroupMenu (KvToolbar.cs:382/434)   ✓ 5 из 8
│       ├── GroupSep × 7                              — линии-разделители, не кнопки
│       └── Cmd_<commandId> × 122                     → KvCommands.Invoke(id) (KvToolbar.cs:349)
│                ✓ 81 · ✗ 12 (в скрытом канвасе) · — 41 (не попал в инвентарь)
│   └── Workbench
│       ├── WbLabel«Верстак:»                         — текст
│       └── Seg_Workbench/Seg0«Робот (6 осей)», Seg1«SCARA»
│                → SelectRobotByWorkbench (KvToolbar.cs:161–167)        — в замер не попали
├── KvToolbarGroupLayer / KvGroupMenu                 выпадающий список группы (KvToolbarGroups.cs:210)
│   ├── GroupTitle (текст), Sep (картинка)
│   ├── GM_<commandId> × 122                          → Close + KvCommands.Invoke(id) (KvToolbarGroups.cs:431)
│   ├── GM_Action«Свернуть группу в иконку / Развернуть группу» × 8
│   │                                                 → KvToolbar.SetGroupCollapsed (KvToolbarGroups.cs:343)
│   └── KvGroupMenuBlocker                            → Close() (KvToolbarGroups.cs:273–285)
│        (все элементы Korня C создаются по нажатию — в инвентарь прогона не попали, лимит 140)
└── Кнопки содержимого док-панелей (дерево/свойства/настройки) живут в Корне D — §1.4, граница §6
```

### 1.4 Корень D — `KazistovVv_UI/KazistovVvDockCanvas` (док-панели)

Панели строятся в своём канвасе (`KazistovVvUIManager.MakeDock`, `KazistovVvUIManager.cs:802–835`,
комментарий R6-1). В AR поднимается **четыре** дока: `tree`, `properties`, `settings`, `hotkeys`.

```
KazistovVv_UI/KazistovVvDockCanvas
├── Dock_Дерево моделей/Header    → Collapse / Dock / Close   (KvDockPanel.cs:194–199)   — в замер не попали
├── Dock_Свойства/Header
│   ├── Collapse → ToggleCollapsed()  (KvDockPanel.cs:194/317)   ✓  «панель properties: свёрнута 0→1»
│   ├── Dock     → ToggleFloat()      (KvDockPanel.cs:196/379)   ✓  «панель properties: сторона 1→3»
│   └── Close    → ToRail()           (KvDockPanel.cs:198/335)   ✓  «панель properties: в рейле 0→1»
├── Dock_Настройки и справка/Header → Collapse / Dock / Close    — в замер не попали
└── Dock_Горячие клавиши/Header     → Collapse / Dock / Close    — в замер не попали
```

**Итог по корням:** A — 255 кнопок (44 статических + 75 пунктов меню + 136 строк команд с пагинацией);
B — 1; C — 392 (7 + 122 + 132 + 131); D — 12. **Всего 660.**


---

## 2. РАЗБОР КАЖДОГО ЭЛЕМЕНТА

Формат: **позиция в дереве → обработчик (`класс.метод`, `файл:строка`) → `command id` → что реально
делает код → замер из `_ui_sweep_ar.txt` → дефект/потенциал.**

### 2.1 Корень A — главный экран (44 кнопки статического уровня: 35 в §2.1.1–2.1.5 + 9 в §2.1.6)

#### 2.1.1 Верхнее меню `PcMenuBar` (6)


| Элемент | Обработчик | command id | Что делает код | Замер (effect=) | Дефект / потенциал |
|---|---|---|---|---|---|
| `Menu_Файл«Файл»` | KvArUi.ToggleMenu — KvArUi.cs:635 (создание: KvArUi.cs:266–269) | — (не команда, а раздел) | Снимает/строит `PcMenuDropdown`: уничтожает старые строки (KvArUi.cs:653), берёт список id из `MenuCommands("Файл")` (KvArUi.cs:785–837), подписи — из реестра `KvCommands`, недоступные пункты рисуются тёмным (KvArUi.cs:693). | «выделение uGUI: 0→1; объектов сцены: 1365→1390» (отчёт:59) | Если раздел уже открыт — закрывается (KvArUi.cs:640). Потенциал: нет индикации «открыто» (выделение только что нажатой кнопки) и нет закрытия по тапу вне меню (в AR нет блокера вроде `KvMenuBlocker` оболочки). |
| `Menu_Правка«Правка»` | KvArUi.ToggleMenu — KvArUi.cs:635 (создание: KvArUi.cs:266–269) | — (не команда, а раздел) | Снимает/строит `PcMenuDropdown`: уничтожает старые строки (KvArUi.cs:653), берёт список id из `MenuCommands("Правка")` (KvArUi.cs:785–837), подписи — из реестра `KvCommands`, недоступные пункты рисуются тёмным (KvArUi.cs:693). | «выделение uGUI: 0→1; объектов сцены: 1390→1378» (отчёт:62) | Если раздел уже открыт — закрывается (KvArUi.cs:640). Потенциал: нет индикации «открыто» (выделение только что нажатой кнопки) и нет закрытия по тапу вне меню (в AR нет блокера вроде `KvMenuBlocker` оболочки). |
| `Menu_Вид«Вид»` | KvArUi.ToggleMenu — KvArUi.cs:635 (создание: KvArUi.cs:266–269) | — (не команда, а раздел) | Снимает/строит `PcMenuDropdown`: уничтожает старые строки (KvArUi.cs:653), берёт список id из `MenuCommands("Вид")` (KvArUi.cs:785–837), подписи — из реестра `KvCommands`, недоступные пункты рисуются тёмным (KvArUi.cs:693). | «выделение uGUI: 0→1; объектов сцены: 1378→1396» (отчёт:64) | Если раздел уже открыт — закрывается (KvArUi.cs:640). Потенциал: нет индикации «открыто» (выделение только что нажатой кнопки) и нет закрытия по тапу вне меню (в AR нет блокера вроде `KvMenuBlocker` оболочки). |
| `Menu_Робот«Робот»` | KvArUi.ToggleMenu — KvArUi.cs:635 (создание: KvArUi.cs:266–269) | — (не команда, а раздел) | Снимает/строит `PcMenuDropdown`: уничтожает старые строки (KvArUi.cs:653), берёт список id из `MenuCommands("Робот")` (KvArUi.cs:785–837), подписи — из реестра `KvCommands`, недоступные пункты рисуются тёмным (KvArUi.cs:693). | «выделение uGUI: 0→1; объектов сцены: 1396→1406» (отчёт:66) | Если раздел уже открыт — закрывается (KvArUi.cs:640). Потенциал: нет индикации «открыто» (выделение только что нажатой кнопки) и нет закрытия по тапу вне меню (в AR нет блокера вроде `KvMenuBlocker` оболочки). |
| `Menu_Сервис«Сервис»` | KvArUi.ToggleMenu — KvArUi.cs:635 (создание: KvArUi.cs:266–269) | — (не команда, а раздел) | Снимает/строит `PcMenuDropdown`: уничтожает старые строки (KvArUi.cs:653), берёт список id из `MenuCommands("Сервис")` (KvArUi.cs:785–837), подписи — из реестра `KvCommands`, недоступные пункты рисуются тёмным (KvArUi.cs:693). | «выделение uGUI: 0→1; объектов сцены: 1406→1402» (отчёт:68) | Если раздел уже открыт — закрывается (KvArUi.cs:640). Потенциал: нет индикации «открыто» (выделение только что нажатой кнопки) и нет закрытия по тапу вне меню (в AR нет блокера вроде `KvMenuBlocker` оболочки). |
| `Menu_Справка«Справка»` | KvArUi.ToggleMenu — KvArUi.cs:635 (создание: KvArUi.cs:266–269) | — (не команда, а раздел) | Снимает/строит `PcMenuDropdown`: уничтожает старые строки (KvArUi.cs:653), берёт список id из `MenuCommands("Справка")` (KvArUi.cs:785–837), подписи — из реестра `KvCommands`, недоступные пункты рисуются тёмным (KvArUi.cs:693). | «выделение uGUI: 0→1; объектов сцены: 1402→1374» (отчёт:70) | Если раздел уже открыт — закрывается (KvArUi.cs:640). Потенциал: нет индикации «открыто» (выделение только что нажатой кнопки) и нет закрытия по тапу вне меню (в AR нет блокера вроде `KvMenuBlocker` оболочки). |


#### 2.1.2 Строка инструментов `PcToolbar` (13)


| Элемент | Обработчик | command id | Что делает код | Замер (effect=) | Дефект / потенциал |
|---|---|---|---|---|---|
| `Tool_workspace.clear«Очистить»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `workspace.clear` | перехват: ShowConfirm(«Очистить рабочее пространство?») → KvArRuntime.ClearWorkspace() | ✓ «выделение uGUI: 0→1» (отчёт:287) | Открывает подтверждение, поэтому по замеру — только подсветка; реальная работа доказана **поздним** нажатием `ArMessageButton` (отчёт:310, «строки дерева 9→7; объектов сцены 1605→1578»). Потенциал: нет кнопки «повторить очистку». |
| `Tool_shot.screenshot«Скриншот»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `shot.screenshot` | hub.TakeScreenshot(); | ✓ «выделение uGUI: 0→1; команд выполнено: 10→11» (отчёт:133) | См. §4.3: v1 считал эти команды «не сработавшими в AR», замер опровергает. |
| `Tool_record.toggle«Запись»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `record.toggle` | hub.ToggleRecord(); | ✓ «выделение uGUI: 0→1; команд выполнено: 69→70» (отчёт:289) | См. §4.2: v1 считал кнопку «без эффекта», замер 11.11 опровергает. |
| `Tool_estop«Стоп»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `estop` | hub.EmergencyStop(); | ✓ «выделение uGUI: 0→1; команд выполнено: 11→12» (отчёт:135) | Потенциал: нет состояния «авария» (цвет/звук), нет журнала аварии на экране. |
| `Tool_tool.grid«Сетка»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `tool.grid` |  | ✓ «выделение uGUI: 0→1; команд выполнено: 12→13; объектов сцены: 1505→1524» (отчёт:137) | Потенциал: см. §5 (v1) — индикация состояния тумблера на кнопке AR-тулбара отсутствует (цвет не меняется). |
| `Tool_view.limits«Лимиты»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `view.limits` | Кольца у суставов (зелёный/жёлтый/красный) и панель «угол / лимит / запас» | ✓ «выделение uGUI: 0→1; команд выполнено: 13→14» (отчёт:139) | Потенциал: см. §5 (v1) — индикация состояния тумблера на кнопке AR-тулбара отсутствует (цвет не меняется). |
| `Tool_view.metrics«Метрики»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `view.metrics` | bool value = !KvSettings.ShowMetrics; KvSettings.ShowMetrics = value; ApplyVisualization("vis.metrics", value); | ✓ «выделение uGUI: 0→1; команд выполнено: 14→15» (отчёт:141) | Потенциал: см. §5 (v1) — индикация состояния тумблера на кнопке AR-тулбара отсутствует (цвет не меняется). |
| `Tool_view.workspace«Раб.зона»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `view.workspace` | bool value = !KvSettings.ShowWorkspace; KvSettings.ShowWorkspace = value; ApplyVisualization("vis.workspace", value); | ✓ «выделение uGUI: 0→1; команд выполнено: 15→16» (отчёт:143) | Потенциал: см. §5 (v1) — индикация состояния тумблера на кнопке AR-тулбара отсутствует (цвет не меняется). |
| `Tool_ui.settings«Настройки»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `ui.settings` | ShowSettings(0); | ✗ «не нажимали — правило запрещает (опасно для прогона)» (отчёт:71) | **Правило прогона запрещает нажатие** («вне панелей проекта: возможно системный экран», отчёт:71) — но это кнопка проекта: `ui.settings → ShowSettings(0)` открывает панель настроек оболочки, не экран Android. Ложное срабатывание правила: кнопка не проверена (§4.5). |
| `Tool_view.panel.tree«Объекты»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `view.panel.tree` | TogglePanel("tree"); | ✓ «выделение uGUI: 0→1; команд выполнено: 0→1» (отчёт:73) | Потенциал: см. §5 (v1) — индикация состояния тумблера на кнопке AR-тулбара отсутствует (цвет не меняется). |
| `Tool_file.export«Экспорт»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `file.export` | hub.Log.Add(KvLogKind.System, "папка данных: " + FeatureStorage.Root); Debug.Log("[Features] папка данных: " + FeatureStorage.Root + "\n записи: " + FeatureSto… | ✓ «выделение uGUI: 0→1; команд выполнено: 70→71» (отчёт:291) | См. §4.3: v1 считал эти команды «не сработавшими в AR», замер опровергает. |
| `Tool_session.save«Сохранить»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `session.save` | hub.Sessions.Save("Сессия " + FeatureStorage.TimeStamp()); | ✓ «выделение uGUI: 0→1; команд выполнено: 16→17» (отчёт:145) | См. §4.3: v1 считал эти команды «не сработавшими в AR», замер опровергает. |
| `Tool_demo.quick«Демо»` | KvArUi.Execute — KvArUi.cs:555 (создание: KvArUi.cs:305–307) | `demo.quick` | hub.PlayDemo(); | ✓! «строки дерева: 7→9; выделение uGUI: 0→1; объектов сцены: 1597→1605» (отчёт:285) | Замер пометил исключением «нечего ставить: найденных плоскостей 0» — демо-сцена не собралась в AR без плоскостей. |


#### 2.1.3 Левое дерево проекта `PcTreePanel` (10)


| Элемент | Обработчик | command id | Что делает код | Замер (effect=) | Дефект / потенциал |
|---|---|---|---|---|---|
| `Tree_6-осевой робот«▸ 6-осевой робот»` | KvArUi.TreeAction — KvArUi.cs:601 (создание: KvArUi.cs:355–358) | `robot.model.stand_6axis` | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель | ✓ «выделение uGUI: 0→1» (отчёт:75) | Модель с геометрией — команда доступна. |
| `Tree_SCARA LS10-B702S«▸ SCARA LS10-B702S»` | KvArUi.TreeAction — KvArUi.cs:601 (создание: KvArUi.cs:355–358) | `robot.model.scara_ls10b702s` | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель | ✓ «выделение uGUI: 0→1» (отчёт:77) | Модель с геометрией — команда доступна. |
| `Tree_Квадратный стол«▸ Квадратный стол»` | KvArUi.TreeAction — KvArUi.cs:601 (создание: KvArUi.cs:355–358) | `table.square` | перехват: KvArUi.CreateTable() — стол фабрикой KvXrObjectFactory на найденной плоскости | ✓! «выделение uGUI: 0→1; объектов сцены: 1374→1380» (отчёт:79) | **Исключение прогона** «нечего ставить: найденных плоскостей 0 (подтверждено состоянием AR)» — но объекты сцены изменились (1374→1380), то есть стол создался в сцене, а постановка в AR не удалась честно. |
| `Tree_Авто стол«▸ Авто стол»` | KvArUi.TreeAction — KvArUi.cs:601 (создание: KvArUi.cs:355–358) | `table.auto` | перехват: KvArUi.CreateTable() — стол фабрикой KvXrObjectFactory на найденной плоскости | ✓! «выделение uGUI: 0→1; объектов сцены: 1412→1392» (отчёт:81) | **Исключение прогона** «нечего ставить: найденных плоскостей 0 (подтверждено состоянием AR)» — но объекты сцены изменились (1374→1380), то есть стол создался в сцене, а постановка в AR не удалась честно. |
| `Tree_Траектория«▸ Траектория»` | KvArUi.TreeAction — KvArUi.cs:601 (создание: KvArUi.cs:355–358) | `group:path` | перехват: OpenCommands(<группа>) — открывает панель команд | ✓ «выделение uGUI: 0→1; объектов сцены: 1392→1412» (отчёт:83) | Открывает панель команд соответствующей группы (`group:*`) либо выполняет команду. |
| `Tree_Постобработка«▸ Постобработка»` | KvArUi.TreeAction — KvArUi.cs:601 (создание: KvArUi.cs:355–358) | `group:post` | перехват: OpenCommands(<группа>) — открывает панель команд | ✓ «выделение uGUI: 0→1» (отчёт:85) | Открывает панель команд соответствующей группы (`group:*`) либо выполняет команду. |
| `Tree_Ограничения«▸ Ограничения»` | KvArUi.TreeAction — KvArUi.cs:601 (создание: KvArUi.cs:355–358) | `view.limits` | Кольца у суставов (зелёный/жёлтый/красный) и панель «угол / лимит / запас» | ✓ «выделение uGUI: 0→1; команд выполнено: 1→2; объектов сцены: 1444→1412» (отчёт:87) | Открывает панель команд соответствующей группы (`group:*`) либо выполняет команду. |
| `Tree_Калибровка«▸ Калибровка»` | KvArUi.TreeAction — KvArUi.cs:601 (создание: KvArUi.cs:355–358) | `calib.tab` | hub.OpenTab("calibration"); | ✓! «выделение uGUI: 0→1; команд выполнено: 2→3; объектов сцены: 1412→1487» (отчёт:89) | **Исключение** «калибровка: нужен реальный трекинг шлема» — команда всё же выполнена («команд выполнено 2→3»): открылся калибровочный мастер `hub.OpenTab("calibration")`, недоступен сам процесс. |
| `Tree_Этапы 13–36«▸ Этапы 13–36»` | KvArUi.TreeAction — KvArUi.cs:601 (создание: KvArUi.cs:355–358) | `features.open` | hub.Window.Toggle(0); | ✓ «выделение uGUI: 0→1; команд выполнено: 3→4; объектов сцены: 1487→1522» (отчёт:91) | Открывает панель команд соответствующей группы (`group:*`) либо выполняет команду. |
| `Tree_Все команды«▸ Все команды»` | KvArUi.TreeAction — KvArUi.cs:601 (создание: KvArUi.cs:355–358) | `group:robot` | перехват: OpenCommands(<группа>) — открывает панель команд | ✓ «выделение uGUI: 0→1» (отчёт:93) | Именно это нажатие **открыло панель команд** (следующий элемент прогона — вкладка «Робот» на открытой панели, отчёт:94). |


#### 2.1.4 Правый ряд `ArButtons` (4) — создаются в рантайме

`KvArRuntime.BuildScreenButtons()` (`KvArRuntime.cs:845–869`) вызывается из `Start` и кладёт 4 кнопки
в `VerticalLayoutGroup` через `KvArUi.AddButton` (`KvArUi.cs:990`); размер — `max(180, buttonWidth) ×
max(TouchRowHeightUi, buttonHeight)`, т.е. «под палец» (≈10 мм).


| Элемент | Обработчик | command id | Что делает код | Замер (effect=) | Дефект / потенциал |
|---|---|---|---|---|---|
| `ArButton_Поставить«Поставить»` | KvArRuntime.BuildScreenButtons → KvArUi.AddButton — KvArRuntime.cs:849/854/858/862, KvArUi.cs:990 | — (своя логика, не реестр) | KvArPlacement.TryPlaceAtCenter — KvArPlacement.cs:461: 1) луч из центра экрана; 2) иначе авто-стол из классификатора (`TryAutoTable`, KvArPlacement.cs:499); 3) иначе честный отказ с числом «Стол не найден: …» (KvArPlacement.cs:480–487). | ✓! «выделение uGUI: 0→1» (отчёт:147) | **Исключение** «нечего ставить: найденных плоскостей 0» — отказ честный. Потенциал: выбор конкретной поверхности из списка найденных (см. v1 §6). |
| `ArButton_Переставить«Переставить»` | KvArRuntime.BuildScreenButtons → KvArUi.AddButton — KvArRuntime.cs:849/854/858/862, KvArUi.cs:990 | — (своя логика, не реестр) | KvArPlacement.RePlace — KvArPlacement.cs:626: снимает якорь (`world.Clear`), возвращает состояние ReadyToPlace/WaitingForPlanes, пишет подсказку. | ✓! «выделение uGUI: 0→1» (отчёт:149) | **Исключение** «нечего ставить…» (при 0 плоскостях снимать нечего). Потенциал: drag-перемещение пальцем. |
| `ArButton_Команды«Команды»` | KvArRuntime.BuildScreenButtons → KvArUi.AddButton — KvArRuntime.cs:849/854/858/862, KvArUi.cs:990 | — (своя логика, не реестр) | KvArUi.OpenCommands — KvArUi.cs:703: если панель уже открыта той же группой — **закрывает** (`ClosePanels`, KvArUi.cs:708–712); иначе открывает `PcCommandPanel` и строит строки (`FillCommandRows`, KvArUi.cs:720). | ✓ «выделение uGUI: 0→1» (отчёт:97) | Замер даёт **только подсветку** — открытие панели не измерено (нет метрики «панель команд»). Косвенное доказательство: вкладка «Робот» нажата на уже открытой панели (отчёт:94). Дефект: **переключатель без индикации** — второе нажатие молча закрывает окно. |
| `ArButton_Сброс вида«Сброс вида»` | KvArRuntime.BuildScreenButtons → KvArUi.AddButton — KvArRuntime.cs:849/854/858/862, KvArUi.cs:990 | — (своя логика, не реестр) | KvArTouchInput.ResetView — KvArTouchInput.cs:741: `bridge.ResetView()` (FOV к базовому) + `world.ResetViewRotation()` (поворот мира к позе якоря) + подсказка «Вид сброшен: масштаб 1.00, поворот по якорю». | ✓ «выделение uGUI: 0→1» (отчёт:293) | Только подсветка в замере. Потенциал: пресеты вида («на стол», «сбоку») — v1 §6. |


#### 2.1.5 Окно сообщений `ArMessage` (2 кнопки)

Окно создаётся всегда (`KvArUi.BuildMessage`, `KvArUi.cs:902`), показывается `ShowMessage`
(`KvArUi.cs:1053`) и `ShowConfirm` (`KvArUi.cs:1072`). `ArMessageButton` есть не всегда — она
скрывается, если не переданы и подпись, и действие (`KvArUi.cs:1062–1063`).


| Элемент | Обработчик | command id | Что делает код | Замер (effect=) | Дефект / потенциал |
|---|---|---|---|---|---|
| `ArMessageButton` (без подписи в пути) | KvArUi.OnMessageButtonClicked — KvArUi.cs:1115 (подключение: KvArUi.cs:943) | — (действие задаёт ShowMessage/ShowConfirm) | `messageAction` (закрывает окно и выполняет действие, KvArUi.cs:1118–1121). Реальные подписи: «ОЧИСТИТЬ» (KvArUi.cs:563), «РАЗРЕШИТЬ КАМЕРУ»/«ОТКРЫТЬ НАСТРОЙКИ» (KvArBoot.cs:197), «Продолжить в VR (для шлема)» (KvArBoot.cs:302). | ✓ «строки дерева: 9→7; выделение uGUI: 0→1; объектов сцены: 1605→1578» (отчёт:310) | Второй проход: «строки дерева 9→7; объектов сцены 1605→1578» — по нажатию реально очищено рабочее пространство. Дефект: подпись окна не показывает, **какое** действие будет выполнено, кроме текста сообщения. |
| `ArMessageCancel«ОТМЕНА»` | KvArUi.OnMessageCancelClicked — KvArUi.cs:1124 (подключение: KvArUi.cs:972) | — | `HideMessage()` (KvArUi.cs:1106): окно закрывается, `messageAction` и `messageCancelAction` обнуляются — **действие отбрасывается**. В `ShowConfirm` cancel-действие присваивается как `HideMessage` (KvArUi.cs:1079). | ✗ «не нажимали — панель так и не открылась» (отчёт:312) | **Кнопка ни разу не нажата самопрогоном** (панель подтверждения не открылась на втором проходе) — см. §4.1: утверждение v1 «не отменяет» замером не подтверждается, по коду отмена работает. Кнопка видна только в `ShowConfirm` (KvArUi.cs:1082), в обычных сообщениях скрыта (KvArUi.cs:1061). |


#### 2.1.6 Панель команд `PcCommandPanel` — вкладки (9)


| Элемент | Обработчик | command id | Что делает код | Замер (effect=) | Дефект / потенциал |
|---|---|---|---|---|---|
| `GroupTab_robot«Робот»` | KvArUi.OpenCommands — KvArUi.cs:703 (создание: KvArUi.cs:474–477) | группа `robot` («Робот») | `OpenCommands("robot")`: повторное нажатие той же группы закрывает панель (KvArUi.cs:708), иначе `commandGroupId = gid`, `commandPage = 0`, `FillCommandRows()` — **21 команд, 3 стр.** по 8 строк. | ✓ «выделение uGUI: 0→1» (отчёт:95) |  |
| `GroupTab_path«Траектория»` | KvArUi.OpenCommands — KvArUi.cs:703 (создание: KvArUi.cs:474–477) | группа `path` («Траектория») | `OpenCommands("path")`: повторное нажатие той же группы закрывает панель (KvArUi.cs:708), иначе `commandGroupId = gid`, `commandPage = 0`, `FillCommandRows()` — **13 команд, 2 стр.** по 8 строк. | ✗ «не нажимали — панель так и не открылась» (отчёт:295) |  |
| `GroupTab_post«Постобр.»` | KvArUi.OpenCommands — KvArUi.cs:703 (создание: KvArUi.cs:474–477) | группа `post` («Постобработка») | `OpenCommands("post")`: повторное нажатие той же группы закрывает панель (KvArUi.cs:708), иначе `commandGroupId = gid`, `commandPage = 0`, `FillCommandRows()` — **17 команд, 3 стр.** по 8 строк. | ✗ «не нажимали — панель так и не открылась» (отчёт:297) |  |
| `GroupTab_view«Визуал.»` | KvArUi.OpenCommands — KvArUi.cs:703 (создание: KvArUi.cs:474–477) | группа `view` («Визуализация») | `OpenCommands("view")`: повторное нажатие той же группы закрывает панель (KvArUi.cs:708), иначе `commandGroupId = gid`, `commandPage = 0`, `FillCommandRows()` — **22 команд, 3 стр.** по 8 строк. | ✗ «не нажимали — панель так и не открылась» (отчёт:299) |  |
| `GroupTab_export«Экспорт»` | KvArUi.OpenCommands — KvArUi.cs:703 (создание: KvArUi.cs:474–477) | группа `export` («Запись и экспорт») | `OpenCommands("export")`: повторное нажатие той же группы закрывает панель (KvArUi.cs:708), иначе `commandGroupId = gid`, `commandPage = 0`, `FillCommandRows()` — **17 команд, 3 стр.** по 8 строк. | ✗ «не нажимали — панель так и не открылась» (отчёт:324) |  |
| `GroupTab_net«Сеть»` | KvArUi.OpenCommands — KvArUi.cs:703 (создание: KvArUi.cs:474–477) | группа `net` («Сеть и автоматизация») | `OpenCommands("net")`: повторное нажатие той же группы закрывает панель (KvArUi.cs:708), иначе `commandGroupId = gid`, `commandPage = 0`, `FillCommandRows()` — **7 команд, 1 стр.** по 8 строк. | ✗ «не нажимали — панель так и не открылась» (отчёт:301) |  |
| `GroupTab_ui«Настройки»` | KvArUi.OpenCommands — KvArUi.cs:703 (создание: KvArUi.cs:474–477) | группа `ui` («Настройки») | `OpenCommands("ui")`: повторное нажатие той же группы закрывает панель (KvArUi.cs:708), иначе `commandGroupId = gid`, `commandPage = 0`, `FillCommandRows()` — **13 команд, 2 стр.** по 8 строк. | ✗ «не нажимали — панель так и не открылась» (отчёт:303) |  |
| `GroupTab_misc«Прочее»` | KvArUi.OpenCommands — KvArUi.cs:703 (создание: KvArUi.cs:474–477) | группа `misc` («Прочее») | `OpenCommands("misc")`: повторное нажатие той же группы закрывает панель (KvArUi.cs:708), иначе `commandGroupId = gid`, `commandPage = 0`, `FillCommandRows()` — **12 команд, 2 стр.** по 8 строк. | ✗ «не нажимали — панель так и не открылась» (отчёт:305) |  |
| `CloseCommands«Закрыть»` | KvArUi.ClosePanels — KvArUi.cs:840 (создание: KvArUi.cs:480–483) | — | `SetActive(false)` для `PcMenuDropdown` и `PcCommandPanel`. | ✗ «не нажимали — панель так и не открылась» (отчёт:307) | Не проверена: панель на втором проходе не открылась (отчёт:307). Потенциал: закрытие по тапу вне панели/Escape — в AR не реализовано. |

---

### 2.2 Корень A — пункты выпадающих меню `PcMenuDropdown` (75 кнопок)

Строка создаётся при каждом открытии раздела: `KvArUi.ToggleMenu` → `CreateButton(menuPanel,
"MenuItem_" + commandId, caption, …)`, обработчик `Execute(commandId)` (`KvArUi.cs:685–689`).
Подпись берётся из реестра: `cmd != null ? LocalizedTitle : commandId + "  (нет)"`
(`KvArUi.cs:684`), цвет строки — «доступна/недоступна» (`KvArUi.cs:693–696`).
**Замер:** ни один пункт меню не попал в инвентарь прогона — список элементов собирается до открытия
меню, а лимит `MaxElements=140` уже исчерпан; поэтому по каждому пункту стоит «— (не в инвентаре)».


#### Меню «Файл» — 12 пунктов


| command id | Подпись в меню | Регистрация (файл:строка) | Что делает код | Замер |
|---|---|---|---|---|
| | |   | ` | s | e | s | s | i | o | n | . | s | a | v | e | ` |   | | |   | С | о | х | р | а | н | и | т | ь |   | с | е | с | с | и | ю |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 8 | 4 |   | | |   | h | u | b | . | S | e | s | s | i | o | n | s | . | S | a | v | e | ( | " | С | е | с | с | и | я |   | " |   | + |   | F | e | a | t | u | r | e | S | t | o | r | a | g | e | . | T | i | m | e | S | t | a | m | p | ( | ) | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | s | e | s | s | i | o | n | . | l | o | a | d | ` |   | | |   | З | а | г | р | у | з | и | т | ь |   | с | е | с | с | и | ю |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 9 | 3 |   | | |   | K | v | S | e | s | s | i | o | n |   | n | e | w | e | s | t |   | = |   | h | u | b | . | S | e | s | s | i | o | n | s | . | N | e | w | e | s | t | ; |   | i | f |   | ( | n | e | w | e | s | t |   | = | = |   | n | u | l | l | ) |   | h | u | b | . | L | o | g | . | A | d | d | ( | K | v | L | o | g | K | i | n | d | . | E | r | r | o | r | , |   | " | с | е | с | с | и | й |   | н | е | т |   | — |   | с | н | а | ч | а | л | а |   | с | о | х | р | а | н | и | т | е | " | ) | ; |   | e | l | s | e |   | h | u | b | . | S | e | s | s | i | o | n | s | . | L | o | a | d | ( | n | e | … |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | e | x | p | o | r | t | . | r | o | b | o | t | ` |   | | |   | Э | к | с | п | о | р | т |   | в |   | я | з | ы | к |   | р | о | б | о | т | а |   | | |   | K | v | S | t | a | g | e | H | u | b | 3 | . | c | s | : | 8 | 4 | 5 |   | | |   | h | u | b | . | E | x | p | o | r | t | S | e | l | e | c | t | e | d | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | f | i | l | e | . | e | x | p | o | r | t | ` |   | | |   | Э | к | с | п | о | р | т | : |   | п | а | п | к | а |   | д | а | н | н | ы | х |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 5 | 3 | 0 |   | | |   | h | u | b | . | L | o | g | . | A | d | d | ( | K | v | L | o | g | K | i | n | d | . | S | y | s | t | e | m | , |   | " | п | а | п | к | а |   | д | а | н | н | ы | х | : |   | " |   | + |   | F | e | a | t | u | r | e | S | t | o | r | a | g | e | . | R | o | o | t | ) | ; |   | D | e | b | u | g | . | L | o | g | ( | " | [ | F | e | a | t | u | r | e | s | ] |   | п | а | п | к | а |   | д | а | н | н | ы | х | : |   | " |   | + |   | F | e | a | t | u | r | e | S | t | o | r | a | g | e | . | R | o | o | t |   | + |   | " | \ | n |   | з | а | п | и | с | и | : |   | " |   | + |   | … |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | f | i | l | e | . | i | m | p | o | r | t | ` |   | | |   | И | м | п | о | р | т |   | с | ц | е | н | а | р | и | я | : |   | с | о | х | р | а | н | ё | н | н | ы | е |   | с | е | с | с | и | и |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 5 | 6 | 7 |   | | |   | h | u | b | . | W | i | n | d | o | w | . | T | o | g | g | l | e | ( | 9 | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | l | o | g | . | e | x | p | o | r | t | ` |   | | |   | В | ы | г | р | у | з | и | т | ь |   | ж | у | р | н | а | л |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 0 | 6 |   | | |   | h | u | b | . | L | o | g | . | E | x | p | o | r | t | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | s | h | o | t | . | s | c | r | e | e | n | s | h | o | t | ` |   | | |   | С | д | е | л | а | т | ь |   | с | к | р | и | н | ш | о | т |   | | |   | K | v | S | t | a | g | e | H | u | b | . | c | s | : | 6 | 2 | 0 |   | | |   | h | u | b | . | T | a | k | e | S | c | r | e | e | n | s | h | o | t | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | e | c | o | r | d | . | t | o | g | g | l | e | ` |   | | |   | З | а | п | и | с | а | т | ь |   | д | в | и | ж | е | н | и | е |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 1 | 6 | 2 |   | | |   | h | u | b | . | T | o | g | g | l | e | R | e | c | o | r | d | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | v | i | d | e | o | . | t | o | g | g | l | e | ` |   | | |   | Н | а | ч | а | т | ь |   | / |   | о | с | т | а | н | о | в | и | т | ь |   | з | а | п | и | с | ь |   | | |   | K | v | S | t | a | g | e | H | u | b | . | c | s | : | 6 | 3 | 2 |   | | |   | h | u | b | . | T | o | g | g | l | e | V | i | d | e | o | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | e | x | p | o | r | t | . | l | a | n | g | ` |   | | |   | Я | з | ы | к |   | э | к | с | п | о | р | т | а |   | ( | K | R | L |   | / |   | K | A | R | E | L |   | / |   | R | A | P | I | D | ) |   | | |   | K | v | S | t | a | g | e | H | u | b | 3 | . | c | s | : | 8 | 5 | 4 |   | | |   | h | u | b | . | C | y | c | l | e | E | x | p | o | r | t | L | a | n | g | u | a | g | e | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | e | c | o | r | d | . | s | p | e | e | d | ` |   | | |   | С | к | о | р | о | с | т | ь |   | в | о | с | п | р | о | и | з | в | е | д | е | н | и | я |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 1 | 8 | 8 |   | | |   | h | u | b | . | C | y | c | l | e | P | l | a | y | b | a | c | k | S | p | e | e | d | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | i | m | p | o | r | t | . | s | c | a | n | ` |   | | |   | О | б | н | о | в | и | т | ь |   | с | п | и | с | о | к |   | м | о | д | е | л | е | й |   | | |   | K | v | S | t | a | g | e | H | u | b | 3 | . | c | s | : | 8 | 8 | 3 |   | | |   | h | u | b | . | I | m | p | o | r | t | . | R | e | f | r | e | s | h | F | i | l | e | s | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |



#### Меню «Правка» — 6 пунктов


| command id | Подпись в меню | Регистрация (файл:строка) | Что делает код | Замер |
|---|---|---|---|---|
| | |   | ` | w | o | r | k | s | p | a | c | e | . | c | l | e | a | r | ` |   | | |   | О | ч | и | с | т | и | т | ь |   | р | а | б | о | ч | е | е |   | п | р | о | с | т | р | а | н | с | т | в | о |   | | |   | н | е | т |   | в |   | р | е | е | с | т | р | е |   | ( | п | е | р | е | х | в | а | т |   | K | v | A | r | U | i | . | c | s | : | 5 | 5 | 9 | ) |   | | |   | п | е | р | е | х | в | а | т | : |   | S | h | o | w | C | o | n | f | i | r | m | ( | « | О | ч | и | с | т | и | т | ь |   | р | а | б | о | ч | е | е |   | п | р | о | с | т | р | а | н | с | т | в | о | ? | » | ) |   | → |   | K | v | A | r | R | u | n | t | i | m | e | . | C | l | e | a | r | W | o | r | k | s | p | a | c | e | ( | ) |   | ⚠ |   | * | * | п | у | н | к | т |   | р | и | с | у | е | т | с | я |   | т | ё | м | н | ы | м |   | и |   | с |   | п | о | д | п | и | с | ь | ю |   | « | w | o | r | k | s | p | a | c | e | . | c | l | e | a | r |   |   | ( | н | е | т | ) | » | * | * |   | — |   | к | о | м | а | н | д | а |   | н | е |   | з | а | р | е | г | и | с | т | р | и | р | о | в | а | н | а |   | в |   | ` | K | v | C | o | m | m | a | n | d | s | ` |   | ( | е | ё |   | п | е | р | е | х | в | а | т | ы | в | а | е | т |   | ` | K | v | A | r | U | i | . | E | x | e | c | u | t | e | ` | , |   | K | v | A | r | U | i | . | c | s | : | 5 | 5 | 9 | ) | , |   | а |   | п | о | д | п | и | с | ь |   | с | ч | и | т | а | е | т | с | я |   | п | о |   | р | е | е | с | т | р | у |   | ( | K | v | A | r | U | i | . | c | s | : | 6 | 8 | 4 | ) | . |   | Н | а | ж | а | т | и | е |   | п | р | и |   | э | т | о | м |   | р | а | б | о | т | а | е | т |   | ( | § | 4 | . | 6 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | e | d | i | t | . | u | n | d | o | ` |   | | |   | О | т | м | е | н | и | т | ь |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 2 | 8 |   | | |   | h | u | b | . | U | n | d | o | L | a | s | t | ( | ) | ; |   | Н | е | д | о | с | т | у | п | н | а |   | б | е | з |   | с | т | е | к | а |   | о | т | м | е | н | ы |   | ( | ` | I | s | E | n | a | b | l | e | d | : |   | h | u | b | . | U | n | d | o | . | C | a | n | U | n | d | o | ` | , |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 3 | 0 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | e | d | i | t | . | r | e | d | o | ` |   | | |   | В | е | р | н | у | т | ь |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 3 | 9 |   | | |   | П | о | в | т | о | р |   | о | т | м | е | н | ё | н | н | о | г | о |   | д | е | й | с | т | в | и | я |   | Н | е | д | о | с | т | у | п | н | а |   | б | е | з |   | с | т | е | к | а |   | п | о | в | т | о | р | а |   | ( | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 3 | 9 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | e | d | i | t | . | r | e | s | e | t | ` |   | | |   | С | б | р | о | с |   | с | ц | е | н | а | р | и | я |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 2 | 6 | 5 |   | | |   | T | r | a | j | e | c | t | o | r | y | F | l | o | w | C | o | n | t | r | o | l | l | e | r |   | f |   | = |   | F | l | o | w | ; |   | i | f |   | ( | f |   | = | = |   | n | u | l | l | ) |   | r | e | t | u | r | n | ; |   | f | . | R | e | s | e | t | F | l | o | w | ( | " | С | б | р | о | ш | е | н | о |   | ( | к | н | о | п | к | а |   | и | н | т | е | р | ф | е | й | с | а | ) | " | ) | ; |   | p | o | i | n | t | H | i | s | t | o | r | y | . | C | l | e | a | r | ( | ) | ; |   | L | o | g | ( | " | С | ц | е | н | а | р | и | й |   | с | б | р | о | ш | е | н |   | к | н | … |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | u | i | . | r | e | s | e | t | l | a | y | o | u | t | ` |   | | |   | С | б | р | о | с | и | т | ь |   | р | а | с | к | л | а | д | к | у |   | о | к | о | н |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 5 | 0 | 1 |   | | |   | R | e | s | e | t | L | a | y | o | u | t | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | u | i | . | t | o | g | g | l | e | ` |   | | |   | П | о | к | а | з | а | т | ь |   | / |   | с | к | р | ы | т | ь |   | и | н | т | е | р | ф | е | й | с |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 5 | 2 | 7 |   | | |   |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |



#### Меню «Вид» — 15 пунктов


| command id | Подпись в меню | Регистрация (файл:строка) | Что делает код | Замер |
|---|---|---|---|---|
| | |   | ` | v | i | e | w | . | w | o | r | k | s | p | a | c | e | ` |   | | |   | З | о | н | а |   | д | о | с | т | и | ж | и | м | о | с | т | и |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 3 | 0 | 7 |   | | |   | b | o | o | l |   | v | a | l | u | e |   | = |   | ! | K | v | S | e | t | t | i | n | g | s | . | S | h | o | w | W | o | r | k | s | p | a | c | e | ; |   | K | v | S | e | t | t | i | n | g | s | . | S | h | o | w | W | o | r | k | s | p | a | c | e |   | = |   | v | a | l | u | e | ; |   | A | p | p | l | y | V | i | s | u | a | l | i | z | a | t | i | o | n | ( | " | v | i | s | . | w | o | r | k | s | p | a | c | e | " | , |   | v | a | l | u | e | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | v | i | e | w | . | l | i | m | i | t | s | ` |   | | |   | Л | и | м | и | т | ы |   | с | у | с | т | а | в | о | в |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 3 | 2 | 4 |   | | |   | К | о | л | ь | ц | а |   | у |   | с | у | с | т | а | в | о | в |   | ( | з | е | л | ё | н | ы | й | / | ж | ё | л | т | ы | й | / | к | р | а | с | н | ы | й | ) |   | и |   | п | а | н | е | л | ь |   | « | у | г | о | л |   | / |   | л | и | м | и | т |   | / |   | з | а | п | а | с | » |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | v | i | e | w | . | m | e | t | r | i | c | s | ` |   | | |   | П | а | н | е | л | ь |   | м | е | т | р | и | к |   | т | р | а | е | к | т | о | р | и | й |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 3 | 4 | 1 |   | | |   | b | o | o | l |   | v | a | l | u | e |   | = |   | ! | K | v | S | e | t | t | i | n | g | s | . | S | h | o | w | M | e | t | r | i | c | s | ; |   | K | v | S | e | t | t | i | n | g | s | . | S | h | o | w | M | e | t | r | i | c | s |   | = |   | v | a | l | u | e | ; |   | A | p | p | l | y | V | i | s | u | a | l | i | z | a | t | i | o | n | ( | " | v | i | s | . | m | e | t | r | i | c | s | " | , |   | v | a | l | u | e | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | v | i | e | w | . | s | i | n | g | u | l | a | r | i | t | y | ` |   | | |   | С | и | н | г | у | л | я | р | н | о | с | т | и |   | | |   | K | v | S | t | a | g | e | H | u | b | . | c | s | : | 6 | 6 | 2 |   | | |   | h | u | b | . | T | o | g | g | l | e | S | i | n | g | u | l | a | r | i | t | i | e | s | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | v | i | e | w | . | h | e | a | t | m | a | p | ` |   | | |   | Т | е | п | л | о | в | а | я |   | к | а | р | т | а |   | д | о | с | т | и | ж | и | м | о | с | т | и |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 3 | 3 | 4 |   | | |   | h | u | b | . | H | e | a | t | m | a | p | . | T | o | g | g | l | e | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | v | i | e | w | . | c | l | e | a | r | a | n | c | e | ` |   | | |   | Т | е | п | л | о | в | а | я |   | к | а | р | т | а |   | з | а | з | о | р | о | в |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 3 | 4 | 7 |   | | |   | h | u | b | . | C | l | e | a | r | a | n | c | e | . | T | o | g | g | l | e | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | h | e | a | l | t | h | . | t | o | g | g | l | e | ` |   | | |   | М | о | н | и | т | о | р | и | н | г |   | с | о | с | т | о | я | н | и | я |   | | |   | K | v | S | t | a | g | e | H | u | b | . | c | s | : | 7 | 4 | 9 |   | | |   | h | u | b | . | T | o | g | g | l | e | H | e | a | l | t | h | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | o | b | s | t | a | c | l | e | . | t | o | g | g | l | e | ` |   | | |   | Д | и | н | а | м | и | ч | е | с | к | и | е |   | п | р | е | п | я | т | с | т | в | и | я |   | | |   | K | v | S | t | a | g | e | H | u | b | . | c | s | : | 7 | 6 | 2 |   | | |   | h | u | b | . | T | o | g | g | l | e | O | b | s | t | a | c | l | e | s | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | t | o | o | l | . | g | r | i | d | ` |   | | |   | С | е | т | к | а |   | р | а | б | о | ч | е | й |   | з | о | н | ы |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 6 | 1 | 7 |   | | |   |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | t | o | o | l | . | g | r | i | d | . | c | l | e | a | r | ` |   | | |   | С | е | т | к | а | : |   | у | б | р | а | т | ь |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 6 | 3 | 6 |   | | |   | h | u | b | . | C | l | e | a | r | W | o | r | k | s | p | a | c | e | G | r | i | d | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | t | o | o | l | . | m | e | a | s | u | r | e | ` |   | | |   | И | з | м | е | р | е | н | и | е |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 5 | 8 | 3 |   | | |   |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | t | o | o | l | . | m | e | a | s | u | r | e | . | r | e | s | e | t | ` |   | | |   | И | з | м | е | р | е | н | и | е | : |   | с | б | р | о | с | и | т | ь |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 6 | 0 | 3 |   | | |   | h | u | b | . | R | e | s | e | t | M | e | a | s | u | r | e | ( | ) | ; |   | h | u | b | . | L | o | g | . | A | d | d | ( | K | v | L | o | g | K | i | n | d | . | S | y | s | t | e | m | , |   | " | л | и | н | е | й | к | а | : |   | с | б | р | о | ш | е | н | а | " | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | v | i | e | w | . | p | a | n | e | l | . | t | r | e | e | ` |   | | |   | Д | е | р | е | в | о |   | м | о | д | е | л | е | й |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 3 | 7 | 9 |   | | |   | T | o | g | g | l | e | P | a | n | e | l | ( | " | t | r | e | e | " | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | v | i | e | w | . | p | a | n | e | l | . | p | r | o | p | e | r | t | i | e | s | ` |   | | |   | С | в | о | й | с | т | в | а |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 3 | 9 | 1 |   | | |   | T | o | g | g | l | e | P | a | n | e | l | ( | " | p | r | o | p | e | r | t | i | e | s | " | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | v | i | e | w | . | p | a | n | e | l | . | s | e | t | t | i | n | g | s | ` |   | | |   | Н | а | с | т | р | о | й | к | и |   | и |   | с | п | р | а | в | к | а |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 4 | 0 | 3 |   | | |   | T | o | g | g | l | e | P | a | n | e | l | ( | " | s | e | t | t | i | n | g | s | " | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |



#### Меню «Робот» — 20 пунктов


| command id | Подпись в меню | Регистрация (файл:строка) | Что делает код | Замер |
|---|---|---|---|---|
| | |   | ` | r | o | b | o | t | . | s | w | i | t | c | h | ` |   | | |   | П | е | р | е | к | л | ю | ч | е | н | и | е |   | р | о | б | о | т | а |   | ( | р | о | б | о | т |   | / |   | S | C | A | R | A | ) |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 2 | 8 | 3 |   | | |   | S | w | i | t | c | h | R | o | b | o | t | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | p | l | a | y | p | a | u | s | e | ` |   | | |   | З | а | п | у | с | к |   | / |   | п | а | у | з | а |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 2 | 1 | 4 |   | | |   | T | r | a | j | e | c | t | o | r | y | F | l | o | w | C | o | n | t | r | o | l | l | e | r |   | f |   | = |   | F | l | o | w | ; |   | i | f |   | ( | f |   | = | = |   | n | u | l | l |   | / | / |   | f | . | M | o | t | i | o | n |   | = | = |   | n | u | l | l | ) |   | r | e | t | u | r | n | ; |   | b | o | o | l |   | p | a | u | s | e | d |   | = |   | f | . | M | o | t | i | o | n | . | P | a | u | s | e | d | ; |   | f | . | M | o | t | i | o | n | . | S | e | t | P | a | u | s | e | d | ( | ! | p | a | u | s | e | d | ) | ; |   | L | o | g | ( | p | a | u | s | e | d |   | … |   | * | * | Н | е | д | о | с | т | у | п | н | а | , |   | п | о | к | а |   | н | е |   | и | д | ё | т |   | д | в | и | ж | е | н | и | е | * | * |   | ( | ` | I | s | E | n | a | b | l | e | d | : |   | M | o | t | i | o | n | . | I | s | R | u | n | n | i | n | g | ` | / | ` | M | o | t | i | o | n |   | ! | = |   | n | u | l | l | ` | , |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 2 | 3 | 6 | / | 1 | 2 | 5 | 8 | ) |   | — |   | в |   | A | R | - | з | а | м | е | р | е |   | э | т | и |   | п | у | н | к | т | ы |   | с | в | е | т | и | л | и | с | ь |   | в |   | о | б | о | л | о | ч | к | е |   | к | а | к |   | ` | i | n | t | e | r | a | c | t | a | b | l | e | = | f | a | l | s | e | ` | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | s | t | o | p | ` |   | | |   | О | с | т | а | н | о | в | к | а |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 2 | 4 | 3 |   | | |   | T | r | a | j | e | c | t | o | r | y | F | l | o | w | C | o | n | t | r | o | l | l | e | r |   | f |   | = |   | F | l | o | w | ; |   | i | f |   | ( | f |   | = | = |   | n | u | l | l |   | / | / |   | f | . | M | o | t | i | o | n |   | = | = |   | n | u | l | l | ) |   | r | e | t | u | r | n | ; |   | f | . | M | o | t | i | o | n | . | S | t | o | p | ( | ) | ; |   | L | o | g | ( | " | О | с | т | а | н | о | в | к | а |   | р | о | б | о | т | а |   | ( | к | н | о | п | к | а |   | и | н | т | е | р | ф | е | й | с | а | ) | " | ) | ; |   | * | * | Н | е | д | о | с | т | у | п | н | а | , |   | п | о | к | а |   | н | е |   | и | д | ё | т |   | д | в | и | ж | е | н | и | е | * | * |   | ( | ` | I | s | E | n | a | b | l | e | d | : |   | M | o | t | i | o | n | . | I | s | R | u | n | n | i | n | g | ` | / | ` | M | o | t | i | o | n |   | ! | = |   | n | u | l | l | ` | , |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 2 | 3 | 6 | / | 1 | 2 | 5 | 8 | ) |   | — |   | в |   | A | R | - | з | а | м | е | р | е |   | э | т | и |   | п | у | н | к | т | ы |   | с | в | е | т | и | л | и | с | ь |   | в |   | о | б | о | л | о | ч | к | е |   | к | а | к |   | ` | i | n | t | e | r | a | c | t | a | b | l | e | = | f | a | l | s | e | ` | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | e | s | t | o | p | ` |   | | |   | А | В | А | Р | И | Й | Н | А | Я |   | О | С | Т | А | Н | О | В | К | А |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 2 | 3 | 7 |   | | |   | h | u | b | . | E | m | e | r | g | e | n | c | y | S | t | o | p | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | j | o | i | n | t | s | . | p | a | n | e | l | ` |   | | |   | П | а | н | е | л | ь |   | с | у | с | т | а | в | о | в |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 2 | 2 | 5 |   | | |   | h | u | b | . | W | i | n | d | o | w | . | T | o | g | g | l | e | ( | 2 | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | p | o | s | e | . | s | a | v | e | ` |   | | |   | С | о | х | р | а | н | и | т | ь |   | т | е | к | у | щ | у | ю |   | п | о | з | у |   | к | а | к | … |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 1 | 9 | 9 |   | | |   | С | н | и | м | о | к |   | у | г | л | о | в |   | с | у | с | т | а | в | о | в |   | а | к | т | и | в | н | о | г | о |   | р | о | б | о | т | а |   | в |   | ф | а | й | л |   | ( | п | а | н | е | л | ь |   | « | П | о | з | ы | » |   | — |   | б | ы | с | т | р | ы | й |   | в | ы | б | о | р |   | и | м | е | н | и | ) |   | Н | е | д | о | с | т | у | п | н | а | , |   | е | с | л | и |   | н | е | т |   | а | к | т | и | в | н | о | г | о |   | р | о | б | о | т | а |   | ( | ` | I | s | E | n | a | b | l | e | d | : |   | h | u | b | . | P | o | s | e | s |   | ! | = |   | n | u | l | l | ` | , |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 1 | 9 | 9 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | p | o | s | e | . | g | o | t | o | ` |   | | |   | П | е | р | е | й | т | и |   | в |   | п | о | з | у |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 2 | 1 | 4 |   | | |   | h | u | b | . | W | i | n | d | o | w | . | S | h | o | w | ( | 1 | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | g | r | i | p | p | e | r | . | t | o | g | g | l | e | ` |   | | |   | З | а | х | в | а | т | : |   | о | т | к | р | ы | т | ь |   | / |   | з | а | к | р | ы | т | ь |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 3 | 6 | 0 |   | | |   | h | u | b | . | G | r | i | p | p | e | r | . | T | o | g | g | l | e | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | s | t | a | n | d | _ | 6 | a | x | i | s | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | s | t | a | n | d | _ | 6 | a | x | i | s |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | s | c | a | r | a | _ | l | s | 1 | 0 | b | 7 | 0 | 2 | s | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | s | c | a | r | a | _ | l | s | 1 | 0 | b | 7 | 0 | 2 | s |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | u | r | 5 | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | u | r | 5 |   | | |   | K | v | X | r | M | e | n | u | C | o | v | e | r | a | g | e | T | e | s | t | s | . | c | s | : | 1 | 6 | 7 |   | | |   |   | * | * | Н | е | д | о | с | т | у | п | н | а | * | * | : |   | у |   | п | р | е | ф | а | б | а |   | н | е | т |   | р | а | з | р | е | ш | а | е | м | о | й |   | г | е | о | м | е | т | р | и | и |   | ( | ` | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | U | n | a | v | a | i | l | a | b | l | e | R | e | a | s | o | n | ` | , |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 7 | 1 | / | 1 | 2 | 1 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | k | u | k | a | _ | i | i | w | a | 7 | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | k | u | k | a | _ | i | i | w | a | 7 |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | * | * | Н | е | д | о | с | т | у | п | н | а | * | * | : |   | у |   | п | р | е | ф | а | б | а |   | н | е | т |   | р | а | з | р | е | ш | а | е | м | о | й |   | г | е | о | м | е | т | р | и | и |   | ( | ` | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | U | n | a | v | a | i | l | a | b | l | e | R | e | a | s | o | n | ` | , |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 7 | 1 | / | 1 | 2 | 1 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | k | u | k | a | _ | i | i | w | a | 1 | 4 | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | k | u | k | a | _ | i | i | w | a | 1 | 4 |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | * | * | Н | е | д | о | с | т | у | п | н | а | * | * | : |   | у |   | п | р | е | ф | а | б | а |   | н | е | т |   | р | а | з | р | е | ш | а | е | м | о | й |   | г | е | о | м | е | т | р | и | и |   | ( | ` | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | U | n | a | v | a | i | l | a | b | l | e | R | e | a | s | o | n | ` | , |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 7 | 1 | / | 1 | 2 | 1 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | a | b | b | _ | i | r | b | 6 | 6 | 4 | 0 | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | a | b | b | _ | i | r | b | 6 | 6 | 4 | 0 |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | * | * | Н | е | д | о | с | т | у | п | н | а | * | * | : |   | у |   | п | р | е | ф | а | б | а |   | н | е | т |   | р | а | з | р | е | ш | а | е | м | о | й |   | г | е | о | м | е | т | р | и | и |   | ( | ` | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | U | n | a | v | a | i | l | a | b | l | e | R | e | a | s | o | n | ` | , |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 7 | 1 | / | 1 | 2 | 1 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | f | a | n | u | c | _ | m | 2 | 0 | i | b | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | f | a | n | u | c | _ | m | 2 | 0 | i | b |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | * | * | Н | е | д | о | с | т | у | п | н | а | * | * | : |   | у |   | п | р | е | ф | а | б | а |   | н | е | т |   | р | а | з | р | е | ш | а | е | м | о | й |   | г | е | о | м | е | т | р | и | и |   | ( | ` | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | U | n | a | v | a | i | l | a | b | l | e | R | e | a | s | o | n | ` | , |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 7 | 1 | / | 1 | 2 | 1 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | f | r | a | n | k | a | _ | f | r | 3 | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | f | r | a | n | k | a | _ | f | r | 3 |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | * | * | Н | е | д | о | с | т | у | п | н | а | * | * | : |   | у |   | п | р | е | ф | а | б | а |   | н | е | т |   | р | а | з | р | е | ш | а | е | м | о | й |   | г | е | о | м | е | т | р | и | и |   | ( | ` | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | U | n | a | v | a | i | l | a | b | l | e | R | e | a | s | o | n | ` | , |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 7 | 1 | / | 1 | 2 | 1 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | f | r | a | n | k | a | _ | f | r | 3 | _ | v | 2 | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | f | r | a | n | k | a | _ | f | r | 3 | _ | v | 2 |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | * | * | Н | е | д | о | с | т | у | п | н | а | * | * | : |   | у |   | п | р | е | ф | а | б | а |   | н | е | т |   | р | а | з | р | е | ш | а | е | м | о | й |   | г | е | о | м | е | т | р | и | и |   | ( | ` | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | U | n | a | v | a | i | l | a | b | l | e | R | e | a | s | o | n | ` | , |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 7 | 1 | / | 1 | 2 | 1 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | k | i | n | o | v | a | _ | g | e | n | 3 | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | k | i | n | o | v | a | _ | g | e | n | 3 |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | * | * | Н | е | д | о | с | т | у | п | н | а | * | * | : |   | у |   | п | р | е | ф | а | б | а |   | н | е | т |   | р | а | з | р | е | ш | а | е | м | о | й |   | г | е | о | м | е | т | р | и | и |   | ( | ` | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | U | n | a | v | a | i | l | a | b | l | e | R | e | a | s | o | n | ` | , |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 7 | 1 | / | 1 | 2 | 1 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | c | o | m | a | u | _ | e | d | o | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | c | o | m | a | u | _ | e | d | o |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | * | * | Н | е | д | о | с | т | у | п | н | а | * | * | : |   | у |   | п | р | е | ф | а | б | а |   | н | е | т |   | р | а | з | р | е | ш | а | е | м | о | й |   | г | е | о | м | е | т | р | и | и |   | ( | ` | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | U | n | a | v | a | i | l | a | b | l | e | R | e | a | s | o | n | ` | , |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 7 | 1 | / | 1 | 2 | 1 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | r | o | b | o | t | . | m | o | d | e | l | . | o | p | e | n | a | r | m | ` |   | | |   | r | o | b | o | t | . | m | o | d | e | l | . | o | p | e | n | a | r | m |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 1 | 0 | 8 |   | ( | R | e | g | i | s | t | e | r | M | o | d | e | l | s | : | 8 | 8 | ) |   | | |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | A | p | p | l | y | M | o | d | e | l | ( | m | o | d | e | l | ) | : |   | у | д | а | л | и | т | ь |   | т | е | к | у | щ | е | г | о |   | р | о | б | о | т | а | , |   | с | о | з | д | а | т | ь |   | в | ы | б | р | а | н | н | у | ю |   | м | о | д | е | л | ь |   | * | * | Н | е | д | о | с | т | у | п | н | а | * | * | : |   | у |   | п | р | е | ф | а | б | а |   | н | е | т |   | р | а | з | р | е | ш | а | е | м | о | й |   | г | е | о | м | е | т | р | и | и |   | ( | ` | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | U | n | a | v | a | i | l | a | b | l | e | R | e | a | s | o | n | ` | , |   | K | v | R | o | b | o | t | C | o | m | m | a | n | d | s | . | c | s | : | 7 | 1 | / | 1 | 2 | 1 | ) | . |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |



#### Меню «Сервис» — 18 пунктов


| command id | Подпись в меню | Регистрация (файл:строка) | Что делает код | Замер |
|---|---|---|---|---|
| | |   | ` | u | i | . | s | e | t | t | i | n | g | s | ` |   | | |   | Н | а | с | т | р | о | й | к | и |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 4 | 4 | 3 |   | | |   | S | h | o | w | S | e | t | t | i | n | g | s | ( | 0 | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | v | i | e | w | . | t | h | e | m | e | ` |   | | |   | Т | е | м | а | : |   | т | ё | м | н | а | я |   | / |   | с | в | е | т | л | а | я |   | / |   | с | и | с | т | е | м | н | а | я |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 4 | 1 | 6 |   | | |   | C | y | c | l | e | T | h | e | m | e | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | l | a | n | g | . | c | y | c | l | e | ` |   | | |   | Я | з | ы | к |   | и | н | т | е | р | ф | е | й | с | а |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 4 | 2 | 8 |   | | |   | s | t | r | i | n | g |   | c | o | d | e |   | = |   | K | v | L | o | c | . | C | y | c | l | e | ( | ) | ; |   | L | o | g | ( | " | я | з | ы | к |   | и | н | т | е | р | ф | е | й | с | а | : |   | " |   | + |   | K | v | L | o | c | . | C | u | r | r | e | n | t | N | a | m | e |   | + |   | " |   | ( | " |   | + |   | c | o | d | e |   | + |   | " | ) | " | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | u | i | . | p | a | l | e | t | t | e | ` |   | | |   | П | а | л | и | т | р | а |   | к | о | м | а | н | д |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 4 | 7 | 7 |   | | |   | O | p | e | n | C | o | m | m | a | n | d | P | a | l | e | t | t | e | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | h | e | l | p | . | h | o | t | k | e | y | s | ` |   | | |   | Г | о | р | я | ч | и | е |   | к | л | а | в | и | ш | и |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 4 | 8 | 9 |   | | |   | T | o | g | g | l | e | H | o | t | k | e | y | s | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | f | e | a | t | u | r | e | s | . | o | p | e | n | ` |   | | |   | П | а | н | е | л | ь |   | ф | у | н | к | ц | и | й |   | ( | э | т | а | п | ы |   | 1 | – | 2 | 0 | ) |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 5 | 1 | 8 |   | | |   | h | u | b | . | W | i | n | d | o | w | . | T | o | g | g | l | e | ( | 0 | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | p | e | r | f | . | o | p | e | n | ` |   | | |   | З | а | м | е | р |   | п | р | о | и | з | в | о | д | и | т | е | л | ь | н | о | с | т | и |   | п | л | а | н | и | р | о | в | щ | и | к | а |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 1 | 7 |   | | |   | h | u | b | . | W | i | n | d | o | w | . | T | o | g | g | l | e | ( | 6 | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | l | o | g | . | o | p | e | n | ` |   | | |   | Ж | у | р | н | а | л |   | д | е | й | с | т | в | и | й |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 3 | 9 | 6 |   | | |   | h | u | b | . | W | i | n | d | o | w | . | T | o | g | g | l | e | ( | 7 | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | d | e | m | o | . | q | u | i | c | k | ` |   | | |   | П | о | к | а | з | а | т | ь |   | д | е | м | о |   | | |   | K | v | S | t | a | g | e | H | u | b | 2 | . | c | s | : | 6 | 6 | 0 |   | | |   | h | u | b | . | P | l | a | y | D | e | m | o | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | d | e | m | o | . | p | i | c | k | p | l | a | c | e | ` |   | | |   | P | i | c | k | - | a | n | d | - | p | l | a | c | e |   | д | е | м | о |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 3 | 7 | 3 |   | | |   | h | u | b | . | R | u | n | P | i | c | k | A | n | d | P | l | a | c | e | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | c | a | l | i | b | . | t | a | b | ` |   | | |   | К | а | л | и | б | р | о | в | о | ч | н | ы | й |   | м | а | с | т | е | р |   | | |   | K | v | S | t | a | g | e | H | u | b | 3 | . | c | s | : | 8 | 2 | 3 |   | | |   | h | u | b | . | O | p | e | n | T | a | b | ( | " | c | a | l | i | b | r | a | t | i | o | n | " | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | p | a | y | l | o | a | d | . | t | a | b | ` |   | | |   | К | а | л | ь | к | у | л | я | т | о | р |   | н | а | г | р | у | з | к | и |   | | |   | K | v | S | t | a | g | e | H | u | b | 3 | . | c | s | : | 8 | 3 | 4 |   | | |   | h | u | b | . | O | p | e | n | T | a | b | ( | " | p | a | y | l | o | a | d | " | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | s | c | e | n | a | r | i | o | s | . | o | p | e | n | ` |   | | |   | С | ц | е | н | а | р | и | и |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 5 | 2 |   | | |   | h | u | b | . | W | i | n | d | o | w | . | T | o | g | g | l | e | ( | 8 | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | s | c | e | n | a | r | i | o | s | . | r | u | n | ` |   | | |   | З | а | п | у | с | т | и | т | ь |   | с | ц | е | н | а | р | и | й | … |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 6 | 1 |   | | |   | З | а | п | у | с | т | и | т | ь |   | в | ы | б | р | а | н | н | ы | й |   | в |   | п | а | н | е | л | и |   | с | ц | е | н | а | р | и | й |   | ( | п | а | у | з | а | / | о | т | м | е | н | а |   | — |   | т | а | м |   | ж | е | ) |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | p | e | n | d | a | n | t | . | t | o | g | g | l | e | ` |   | | |   | В | и | р | т | у | а | л | ь | н | ы | й |   | п | у | л | ь | т |   | | |   | K | v | S | t | a | g | e | H | u | b | . | c | s | : | 7 | 9 | 9 |   | | |   | h | u | b | . | T | o | g | g | l | e | P | e | n | d | a | n | t | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | g | a | m | e | p | a | d | . | h | u | d | ` |   | | |   | В | и | р | т | у | а | л | ь | н | ы | й |   | г | е | й | м | п | а | д |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 5 | 4 | 9 |   | | |   | b | o | o | l |   | v | a | l | u | e |   | = |   | ! | K | v | S | e | t | t | i | n | g | s | . | G | a | m | e | p | a | d | H | u | d | ; |   | K | v | S | e | t | t | i | n | g | s | . | G | a | m | e | p | a | d | H | u | d |   | = |   | v | a | l | u | e | ; |   | A | p | p | l | y | V | i | s | u | a | l | i | z | a | t | i | o | n | ( | " | u | i | . | g | a | m | e | p | a | d | h | u | d | " | , |   | v | a | l | u | e | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | e | t | a | . | t | o | g | g | l | e | ` |   | | |   | П | а | н | е | л | ь |   | E | T | A |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 3 | 8 | 4 |   | | |   | h | u | b | . | W | i | n | d | o | w | . | T | o | g | g | l | e | E | t | a | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | p | r | e | s | e | n | t | a | t | i | o | n | . | t | o | g | g | l | e | ` |   | | |   | П | р | е | з | е | н | т | а | ц | и | о | н | н | ы | й |   | р | е | ж | и | м |   | | |   | F | e | a | t | u | r | e | H | u | b | . | c | s | : | 1 | 4 | 7 | 2 |   | | |   | h | u | b | . | P | r | e | s | e | n | t | a | t | i | o | n | . | T | o | g | g | l | e | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |



#### Меню «Справка» — 4 пунктов


| command id | Подпись в меню | Регистрация (файл:строка) | Что делает код | Замер |
|---|---|---|---|---|
| | |   | ` | h | e | l | p | . | o | p | e | n | ` |   | | |   | П | о | м | о | щ | ь |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 4 | 5 | 4 |   | | |   | S | h | o | w | S | e | t | t | i | n | g | s | ( | 4 | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | h | e | l | p | . | a | b | o | u | t | ` |   | | |   | О |   | п | р | о | г | р | а | м | м | е |   | | |   | K | a | z | i | s | t | o | v | V | v | U | I | M | a | n | a | g | e | r | . | c | s | : | 1 | 4 | 6 | 5 |   | | |   | S | h | o | w | S | e | t | t | i | n | g | s | ( | 3 | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | t | u | t | . | t | o | g | g | l | e | ` |   | | |   | О | б | у | ч | е | н | и | е |   | | |   | K | v | S | t | a | g | e | H | u | b | 2 | . | c | s | : | 6 | 7 | 4 |   | | |   | h | u | b | . | T | o | g | g | l | e | T | u | t | o | r | i | a | l | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |
| | |   | ` | t | u | t | . | r | e | s | t | a | r | t | ` |   | | |   | О | б | у | ч | е | н | и | е |   | з | а | н | о | в | о |   | | |   | K | v | S | t | a | g | e | H | u | b | 2 | . | c | s | : | 6 | 8 | 5 |   | | |   | i | f |   | ( | h | u | b | . | T | u | t | o | r | i | a | l |   | ! | = |   | n | u | l | l | ) |   | h | u | b | . | T | u | t | o | r | i | a | l | . | R | e | s | t | a | r | t | T | u | t | o | r | i | a | l | ( | ) | ; |   | | |   | — |   | н | е |   | в |   | и | н | в | е | н | т | а | р | е |   | | |




---

### 2.3 Корень A — строки команд `PcCommandPanel/CommandRows` (122 команды + 14 кнопок пагинации)

Строки создаются `KvArUi.FillCommandRows` (`KvArUi.cs:720–765`): до **8 строк на странице**
(`pageSize = 8`, KvArUi.cs:731), подпись — `CleanLabel(cmd.LocalizedTitle)` (убирает слова
«Виртуальный/Виртуальная», KvArUi.cs:768), обработчик `Execute(id)` (KvArUi.cs:749–753).
Кнопки `CmdPrev« ◀»` / `CmdNext« ▶»` создаются, только если страниц больше одной
(`KvArUi.cs:756–764`), и у `robot/path/post/view/export/ui/misc` (7 групп из 8) — то есть **14 кнопок
пагинации**; у `net` (7 команд) пагинации нет.

**Замер:** строки AR-панели не в инвентаре (панель закрыта + лимит). Но **те же команды** измерены
в оболочке (`KazistovVvCanvas/KvToolbar/Groups/Cmd_<id>`) — колонка «Замер в оболочке» даёт доказательство
поведения команды в той же сцене ArMain.


#### Группа `robot` — вкладка «Робот» (полное название «Робот»): 21 команд, 3 стр.


| command id | Подпись (реестр) | Регистрация (файл:строка) | Что делает код | Замер в оболочке (ArMain) |
|---|---|---|---|---|
| `robot.switch` | Переключение робота (робот / SCARA) | KazistovVvUIManager.cs:1283 | SwitchRobot(); | ✓ «выделение uGUI: 0→1; команд выполнено: 17→18» (отчёт:153) |
| `robot.playpause` | Запуск / пауза | KazistovVvUIManager.cs:1214 | TrajectoryFlowController f = Flow; if (f == null // f.Motion == null) return; bool paused = f.Motion.Paused; f.Motion.SetPaused(!paused); L… ⚠ недоступна без движения робота | ✗ «нет изменений» (отчёт:155) |
| `robot.stop` | Остановка | KazistovVvUIManager.cs:1243 | TrajectoryFlowController f = Flow; if (f == null // f.Motion == null) return; f.Motion.Stop(); Log("Остановка робота (кнопка интерфейса)"); ⚠ недоступна без движения робота | ✗ «нет изменений» (отчёт:157) |
| `estop` | АВАРИЙНАЯ ОСТАНОВКА | FeatureHub.cs:1237 | hub.EmergencyStop(); | ✓ «выделение uGUI: 0→1; команд выполнено: 18→19» (отчёт:159) |
| `edit.reset` | Сброс сценария | KazistovVvUIManager.cs:1265 | TrajectoryFlowController f = Flow; if (f == null) return; f.ResetFlow("Сброшено (кнопка интерфейса)"); pointHistory.Clear(); Log("Сценарий … | ✗ «не нажимали — панель так и не открылась» (отчёт:326) |
| `joints.panel` | Панель суставов | FeatureHub.cs:1225 | hub.Window.Toggle(2); | ✓ «выделение uGUI: 0→1; команд выполнено: 4→5; объектов сцены: 1536→1505» (отчёт:113) |
| `pose.save` | Сохранить текущую позу как… | FeatureHub.cs:1199 | Снимок углов суставов активного робота в файл (панель «Позы» — быстрый выбор имени) | ✓ «выделение uGUI: 0→1; команд выполнено: 19→20; объектов сцены: 1598→1618» (отчёт:161) |
| `pose.goto` | Перейти в позу | FeatureHub.cs:1214 | hub.Window.Show(1); | ✓ «выделение uGUI: 0→1; команд выполнено: 20→21» (отчёт:163) |
| `gripper.toggle` | Захват: открыть / закрыть | FeatureHub.cs:1360 | hub.Gripper.Toggle(); | ✓ «выделение uGUI: 0→1; команд выполнено: 21→22» (отчёт:165) |
| `robot.model.stand_6axis` | Робот: robot.model.stand_6axis | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель | ✓ «строки дерева: 6→7; выделение uGUI: 0→1; команд выполнено: 22→23; объектов сцены: 1618→168…» (отчёт:167) |
| `robot.model.scara_ls10b702s` | Робот: robot.model.scara_ls10b702s | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель | ✓ «выделение uGUI: 0→1; команд выполнено: 23→24; объектов сцены: 1661→1634» (отчёт:169) |
| `robot.model.ur5` | Робот: robot.model.ur5 | KvXrMenuCoverageTests.cs:167 |  ⚠ недоступна (нет геометрии) | ✗ «нет изменений» (отчёт:171) |
| `robot.model.kuka_iiwa7` | Робот: robot.model.kuka_iiwa7 | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель ⚠ недоступна (нет геометрии) | ✗ «нет изменений» (отчёт:173) |
| `robot.model.kuka_iiwa14` | Робот: robot.model.kuka_iiwa14 | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель ⚠ недоступна (нет геометрии) | ✗ «нет изменений» (отчёт:175) |
| `robot.model.abb_irb6640` | Робот: robot.model.abb_irb6640 | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель ⚠ недоступна (нет геометрии) | ✗ «нет изменений» (отчёт:177) |
| `robot.model.fanuc_m20ib` | Робот: robot.model.fanuc_m20ib | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель ⚠ недоступна (нет геометрии) | ✗ «нет изменений» (отчёт:179) |
| `robot.model.franka_fr3` | Робот: robot.model.franka_fr3 | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель ⚠ недоступна (нет геометрии) | ✗ «нет изменений» (отчёт:181) |
| `robot.model.franka_fr3_v2` | Робот: robot.model.franka_fr3_v2 | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель ⚠ недоступна (нет геометрии) | ✓! «объектов сцены: 1634→1641» (отчёт:183) |
| `robot.model.kinova_gen3` | Робот: robot.model.kinova_gen3 | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель ⚠ недоступна (нет геометрии) | ✗ «нет изменений» (отчёт:185) |
| `robot.model.comau_edo` | Робот: robot.model.comau_edo | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель ⚠ недоступна (нет геометрии) | ✗ «нет изменений» (отчёт:187) |
| `robot.model.openarm` | Робот: robot.model.openarm | KvRobotCommands.cs:108 (RegisterModels:88) | KvRobotCommands.ApplyModel(model): удалить текущего робота, создать выбранную модель ⚠ недоступна (нет геометрии) | ✗ «нет изменений» (отчёт:115) |



#### Группа `path` — вкладка «Траектория» (полное название «Траектория»): 13 команд, 2 стр.


| command id | Подпись (реестр) | Регистрация (файл:строка) | Что делает код | Замер в оболочке (ArMain) |
|---|---|---|---|---|
| `point.select` | Выбор точки (красный лазер) | KazistovVvUIManager.cs:1176 | Включает красную указку: ЛКМ фиксирует точку и запускает расчёт траекторий | ✓ «выделение uGUI: 0→1; команд выполнено: 24→25» (отчёт:191) |
| `path.select` | Выбор траектории (зелёный лазер) | KazistovVvUIManager.cs:1195 | FreeFlyCameraController cam = CameraRig; if (cam == null) return; cam.rightHandEnabled = !cam.rightHandEnabled; Log("Зелёный лазер: " + (ca… | ✓ «выделение uGUI: 0→1; команд выполнено: 25→26» (отчёт:193) |
| `waypoint.add` | Добавить waypoint | KvStageHub.cs:675 | hub.Waypoints.AddFromAim(); | ✓ «выделение uGUI: 0→1; команд выполнено: 26→27» (отчёт:195) |
| `waypoint.move` | Переместить waypoint | KvStageHub.cs:684 | Перенести выбранную промежуточную точку в текущую точку прицела | ✓ «выделение uGUI: 0→1; команд выполнено: 27→28» (отчёт:197) |
| `waypoint.delete` | Удалить waypoint | KvStageHub.cs:711 | hub.Waypoints.RemoveSelected(); | ✗ «не нажимали — панель так и не открылась» (отчёт:328) |
| `waypoint.build` | Перестроить маршрут | KvStageHub.cs:729 | hub.Waypoints.RebuildRoute(); | ✓ «выделение uGUI: 0→1; команд выполнено: 28→29» (отчёт:199) |
| `waypoint.play` | Выполнить маршрут | KvStageHub.cs:738 | hub.Waypoints.PlayRoute(); | ✓ «выделение uGUI: 0→1; команд выполнено: 29→30» (отчёт:201) |
| `compare.mark` | Отметить траекторию для сравнения | FeatureHub.cs:1301 | hub.MarkForComparison(); | ✓ «выделение uGUI: 0→1; команд выполнено: 30→31» (отчёт:203) |
| `compare.open` | Сравнение траекторий | FeatureHub.cs:1310 | hub.Window.Toggle(4); | ✓ «выделение uGUI: 0→1; команд выполнено: 5→6» (отчёт:117) |
| `graph.open` | Графики углов суставов | FeatureHub.cs:1322 | hub.Window.Toggle(5); | ✓ «выделение uGUI: 0→1; команд выполнено: 6→7» (отчёт:119) |
| `waypoint.earlier` | Waypoint раньше | KvStageHub.cs:693 | hub.Waypoints.MoveUp(hub.Waypoints.SelectedIndex); | ✓ «выделение uGUI: 0→1; команд выполнено: 31→32» (отчёт:205) |
| `waypoint.later` | Waypoint позже | KvStageHub.cs:702 | hub.Waypoints.MoveDown(hub.Waypoints.SelectedIndex); | ✓ «выделение uGUI: 0→1; команд выполнено: 32→33» (отчёт:207) |
| `waypoint.clear` | Очистить waypoints | KvStageHub.cs:720 | hub.Waypoints.Clear("кнопка интерфейса"); | ✓ «выделение uGUI: 0→1; команд выполнено: 33→34» (отчёт:209) |



#### Группа `post` — вкладка «Постобр.» (полное название «Постобработка»): 17 команд, 3 стр.


| command id | Подпись (реестр) | Регистрация (файл:строка) | Что делает код | Замер в оболочке (ArMain) |
|---|---|---|---|---|
| `post.smooth` | Сглаживание траектории | KvStageHub2.cs:707 | hub.OpenWorkbench("smooth"); | ✓ «выделение uGUI: 0→1; команд выполнено: 34→35» (отчёт:213) |
| `post.timeoptimal` | Время-оптимальная | KvStageHub2.cs:716 | hub.OpenWorkbench("topt"); | ✓ «выделение uGUI: 0→1; команд выполнено: 35→36; объектов сцены: 1615→1617» (отчёт:215) |
| `post.energy` | Эко-профиль | KvStageHub2.cs:725 | hub.OpenWorkbench("energy"); | ✓ «выделение uGUI: 0→1; команд выполнено: 36→37; объектов сцены: 1617→1586» (отчёт:217) |
| `post.auto` | Автосглаживание после планирования | KvStageHub2.cs:761 | if (hub.Smoothing == null) return; hub.Smoothing.Auto = !hub.Smoothing.Auto; Debug.Log("[Stages2] автосглаживание " + (hub.Smoothing.Auto ?… | ✓ «выделение uGUI: 0→1; команд выполнено: 37→38» (отчёт:219) |
| `workbench.toggle` | Верстак постобработки | KvStageHub2.cs:696 | hub.ToggleWorkbench(); | ✓ «выделение uGUI: 0→1; команд выполнено: 38→39» (отчёт:221) |
| `constr.tab` | Вкладка ограничений | KvStageHub3.cs:812 | hub.OpenTab("constraints"); | ✓ «выделение uGUI: 0→1; команд выполнено: 39→40» (отчёт:223) |
| `constr.toggle` | Ограничения планирования | KvStageHub3.cs:801 | Проверка ограничений: вертикальный инструмент, предел наклона, взгляд на объект | ✓ «выделение uGUI: 0→1; команд выполнено: 40→41» (отчёт:225) |
| `wp.tab` | Ограничения точки | KvStageHub3.cs:745 | hub.OpenTab("waypoints"); | ✓ «выделение uGUI: 0→1; команд выполнено: 41→42» (отчёт:227) |
| `post.apply.smooth` | Сгладить выбранную траекторию | KvStageHub2.cs:734 | hub.SmoothSelected(); | ✓ «выделение uGUI: 0→1; команд выполнено: 42→43» (отчёт:229) |
| `post.apply.topt` | Переключиться на время-оптимальную | KvStageHub2.cs:743 | Заменить время выбранного варианта на время-оптимальное при текущих лимитах | ✓ «выделение uGUI: 0→1; команд выполнено: 43→44» (отчёт:231) |
| `post.apply.energy` | Переключиться на эко-профиль | KvStageHub2.cs:752 | Заменить профиль времени выбранного варианта на энергоэффективный | ✓ «выделение uGUI: 0→1; команд выполнено: 44→45» (отчёт:233) |
| `post.auto.selected` | Сглаживать только выбранный вариант | KvStageHub2.cs:781 | if (hub.Smoothing == null) return; hub.Smoothing.AutoSelectedOnly = !hub.Smoothing.AutoSelectedOnly; Debug.Log("[Stages2] автосглаживание "… | ✓ «выделение uGUI: 0→1; команд выполнено: 45→46» (отчёт:235) |
| `wp.orient` | Ориентация waypoint | KvStageHub3.cs:754 | hub.WaypointOrientationFromCurrent(); | ✓ «выделение uGUI: 0→1; команд выполнено: 46→47» (отчёт:237) |
| `wp.speed` | Скорость waypoint | KvStageHub3.cs:763 | hub.WaypointSpeedLimit(); | ✓ «выделение uGUI: 0→1; команд выполнено: 47→48» (отчёт:239) |
| `wp.detour` | Обход препятствия (waypoint) | KvStageHub3.cs:772 | hub.WaypointDetour(); | ✓ «выделение uGUI: 0→1; команд выполнено: 48→49» (отчёт:241) |
| `wp.pause` | Пауза waypoint | KvStageHub3.cs:781 | hub.WaypointPause(); | ✓ «выделение uGUI: 0→1; команд выполнено: 49→50» (отчёт:243) |
| `wp.clear` | Снять ограничения waypoint | KvStageHub3.cs:790 | hub.WaypointClearLimits(); | ✓ «выделение uGUI: 0→1; команд выполнено: 50→51» (отчёт:245) |



#### Группа `view` — вкладка «Визуал.» (полное название «Визуализация»): 22 команд, 3 стр.


| command id | Подпись (реестр) | Регистрация (файл:строка) | Что делает код | Замер в оболочке (ArMain) |
|---|---|---|---|---|
| `view.workspace` | Зона достижимости | KazistovVvUIManager.cs:1307 | bool value = !KvSettings.ShowWorkspace; KvSettings.ShowWorkspace = value; ApplyVisualization("vis.workspace", value); | ✓ «выделение uGUI: 0→1; команд выполнено: 51→52» (отчёт:249) |
| `view.limits` | Лимиты суставов | KazistovVvUIManager.cs:1324 | Кольца у суставов (зелёный/жёлтый/красный) и панель «угол / лимит / запас» | ✓ «выделение uGUI: 0→1; команд выполнено: 52→53» (отчёт:251) |
| `view.metrics` | Панель метрик траекторий | KazistovVvUIManager.cs:1341 | bool value = !KvSettings.ShowMetrics; KvSettings.ShowMetrics = value; ApplyVisualization("vis.metrics", value); | ✓ «выделение uGUI: 0→1; команд выполнено: 53→54» (отчёт:253) |
| `view.singularity` | Сингулярности | KvStageHub.cs:662 | hub.ToggleSingularities(); | ✓ «выделение uGUI: 0→1; команд выполнено: 54→55» (отчёт:255) |
| `view.heatmap` | Тепловая карта достижимости | FeatureHub.cs:1334 | hub.Heatmap.Toggle(); | ✓ «выделение uGUI: 0→1; команд выполнено: 55→56» (отчёт:257) |
| `view.clearance` | Тепловая карта зазоров | FeatureHub.cs:1347 | hub.Clearance.Toggle(); | ✓ «выделение uGUI: 0→1; команд выполнено: 56→57» (отчёт:259) |
| `health.toggle` | Мониторинг состояния | KvStageHub.cs:749 | hub.ToggleHealth(); | ✓ «выделение uGUI: 0→1; команд выполнено: 57→58» (отчёт:261) |
| `obstacle.toggle` | Динамические препятствия | KvStageHub.cs:762 | hub.ToggleObstacles(); | ✓ «выделение uGUI: 0→1; команд выполнено: 58→59; объектов сцены: 1586→1616» (отчёт:263) |
| `zone.box` | Зона запрета: куб | FeatureHub.cs:1248 | hub.CreateZone(KvZoneShape.Box); | ✓ «выделение uGUI: 0→1; команд выполнено: 59→60» (отчёт:265) |
| `zone.sphere` | Зона запрета: сфера | FeatureHub.cs:1257 | Создать зону запрета-сферу в точке прицела | ✓ «выделение uGUI: 0→1; команд выполнено: 60→61» (отчёт:267) |
| `zone.cylinder` | Зона запрета: цилиндр | FeatureHub.cs:1266 | Создать зону запрета-цилиндр в точке прицела | ✓ «выделение uGUI: 0→1; команд выполнено: 61→62» (отчёт:269) |
| `zone.discard` | Отбрасывать опасные траектории | FeatureHub.cs:1284 | hub.Zones.discardDangerous = !hub.Zones.discardDangerous; hub.Log.Add(KvLogKind.Zone, "опасные траектории " + (hub.Zones.discardDangerous ?… | ✓ «выделение uGUI: 0→1; команд выполнено: 62→63» (отчёт:271) |
| `tool.measure` | Измерение | FeatureHub.cs:1583 |  | ✓ «выделение uGUI: 0→1; команд выполнено: 63→64» (отчёт:273) |
| `tool.measure.reset` | Измерение: сбросить | FeatureHub.cs:1603 | hub.ResetMeasure(); hub.Log.Add(KvLogKind.System, "линейка: сброшена"); | ✗ «не нажимали — панель так и не открылась» (отчёт:330) |
| `tool.grid` | Сетка рабочей зоны | FeatureHub.cs:1617 |  | ✓ «выделение uGUI: 0→1; команд выполнено: 64→65; объектов сцены: 1616→1635» (отчёт:275) |
| `tool.grid.clear` | Сетка: убрать | FeatureHub.cs:1636 | hub.ClearWorkspaceGrid(); | ✓ «выделение uGUI: 0→1; команд выполнено: 65→66; объектов сцены: 1635→1597» (отчёт:277) |
| `obstacle.discard` | Отбрасывать рискованные траектории | KvStageHub.cs:782 | hub.Obstacles.discardRisky = !hub.Obstacles.discardRisky; Debug.Log("[Obstacle] рискованные траектории " + (hub.Obstacles.discardRisky ? "о… | ✓ «выделение uGUI: 0→1; команд выполнено: 66→67» (отчёт:279) |
| `tool.flashlight` | Фонарик | KazistovVvUIManager.cs:1358 | Освещает рабочую зону (те же параметры света, что у бинда G) | ✓ «выделение uGUI: 0→1; команд выполнено: 67→68» (отчёт:281) |
| `view.panel.tree` | Дерево моделей | KazistovVvUIManager.cs:1379 | TogglePanel("tree"); | ✓ «выделение uGUI: 0→1; команд выполнено: 7→8» (отчёт:121) |
| `view.panel.properties` | Свойства | KazistovVvUIManager.cs:1391 | TogglePanel("properties"); | ✓ «панели: 2→1; выделение uGUI: 0→1; команд выполнено: 8→9; отпечаток панелей дока: -28534259…» (отчёт:123) |
| `view.panel.settings` | Настройки и справка | KazistovVvUIManager.cs:1403 | TogglePanel("settings"); | ✓ «панели: 2→3; выделение uGUI: 0→1; команд выполнено: 9→10; отпечаток панелей дока: -2853425…» (отчёт:125) |
| `ui.toggle` | Показать / скрыть интерфейс | KazistovVvUIManager.cs:1527 |  | ✓ «выделение uGUI: 0→1; команд выполнено: 68→69» (отчёт:283) |



#### Группа `export` — вкладка «Экспорт» (полное название «Запись и экспорт»): 17 команд, 3 стр.


| command id | Подпись (реестр) | Регистрация (файл:строка) | Что делает код | Замер в оболочке (ArMain) |
|---|---|---|---|---|
| `record.toggle` | Записать движение | FeatureHub.cs:1162 | hub.ToggleRecord(); | ✗ «не нажимали — панель так и не открылась» (отчёт:334) |
| `record.play` | Проиграть запись | FeatureHub.cs:1175 | hub.TogglePlayback(); | ✗ «не нажимали — панель так и не открылась» (отчёт:336) |
| `shot.screenshot` | Сделать скриншот | KvStageHub.cs:620 | hub.TakeScreenshot(); | ✗ «не нажимали — панель так и не открылась» (отчёт:314) |
| `video.toggle` | Начать / остановить запись | KvStageHub.cs:632 | hub.ToggleVideo(); | ✗ «не нажимали — панель так и не открылась» (отчёт:316) |
| `export.robot` | Экспорт в язык робота | KvStageHub3.cs:845 | hub.ExportSelected(); | ✗ «не нажимали — панель так и не открылась» (отчёт:338) |
| `import.tab` | Импорт модели робота | KvStageHub3.cs:874 | hub.OpenTab("import"); | ✗ «не нажимали — панель так и не открылась» (отчёт:318) |
| `session.save` | Сохранить сессию | FeatureHub.cs:1484 | hub.Sessions.Save("Сессия " + FeatureStorage.TimeStamp()); | ✗ «не нажимали — панель так и не открылась» (отчёт:320) |
| `session.load` | Загрузить сессию | FeatureHub.cs:1493 | KvSession newest = hub.Sessions.Newest; if (newest == null) hub.Log.Add(KvLogKind.Error, "сессий нет — сначала сохраните"); else hub.Sessio… | ✗ «не нажимали — панель так и не открылась» (отчёт:322) |
| `record.speed` | Скорость воспроизведения | FeatureHub.cs:1188 | hub.CyclePlaybackSpeed(); | — не в инвентаре |
| `log.export` | Выгрузить журнал | FeatureHub.cs:1406 | hub.Log.Export(); | — не в инвентаре |
| `session.panel` | Список сессий | FeatureHub.cs:1507 | hub.Window.Toggle(9); | — не в инвентаре |
| `file.export` | Экспорт: папка данных | FeatureHub.cs:1530 | hub.Log.Add(KvLogKind.System, "папка данных: " + FeatureStorage.Root); Debug.Log("[Features] папка данных: " + FeatureStorage.Root + "\n за… | — не в инвентаре |
| `shot.annotate` | Подпись на скриншоте | KvStageHub.cs:645 | Рисовать дату, состояние State Machine и выбранного робота поверх снимка | — не в инвентаре |
| `export.lang` | Язык экспорта (KRL / KAREL / RAPID) | KvStageHub3.cs:854 | hub.CycleExportLanguage(); | — не в инвентаре |
| `export.tab` | Вкладка экспорта траектории | KvStageHub3.cs:863 | hub.OpenTab("export"); | — не в инвентаре |
| `import.scan` | Обновить список моделей | KvStageHub3.cs:883 | hub.Import.RefreshFiles(); | — не в инвентаре |
| `file.import` | Импорт сценария: сохранённые сессии | FeatureHub.cs:1567 | hub.Window.Toggle(9); | — не в инвентаре |



#### Группа `net` — вкладка «Сеть» (полное название «Сеть и автоматизация»): 7 команд, 1 стр.


| command id | Подпись (реестр) | Регистрация (файл:строка) | Что делает код | Замер в оболочке (ArMain) |
|---|---|---|---|---|
| `scenarios.open` | Сценарии | FeatureHub.cs:1452 | hub.Window.Toggle(8); | — не в инвентаре |
| `scenarios.run` | Запустить сценарий… | FeatureHub.cs:1461 | Запустить выбранный в панели сценарий (пауза/отмена — там же) | — не в инвентаре |
| `pendant.toggle` | Виртуальный пульт | KvStageHub.cs:799 | hub.TogglePendant(); | — не в инвентаре |
| `pendant.variant` | Вариант пульта (A / B) | KvStageHub.cs:810 | Переключить оформление: A — промышленный, B — стиль проекта АДДОН | — не в инвентаре |
| `pendant.attach` | Закрепление пульта | KvStageHub.cs:819 | Пульт парит в сцене → в руке оператора → над роботом | — не в инвентаре |
| `pendant.jogmode` | Режим джойстика (JOINT / TCP) | KvStageHub.cs:836 | Джойстик покачивает выбранный сустав либо сдвигает TCP в плоскости | — не в инвентаре |
| `gamepad.hud` | Виртуальный геймпад | KazistovVvUIManager.cs:1549 | bool value = !KvSettings.GamepadHud; KvSettings.GamepadHud = value; ApplyVisualization("ui.gamepadhud", value); | — не в инвентаре |



#### Группа `ui` — вкладка «Настройки» (полное название «Настройки»): 13 команд, 2 стр.


| command id | Подпись (реестр) | Регистрация (файл:строка) | Что делает код | Замер в оболочке (ArMain) |
|---|---|---|---|---|
| `view.theme` | Тема: тёмная / светлая / системная | KazistovVvUIManager.cs:1416 | CycleTheme(); | — не в инвентаре |
| `lang.cycle` | Язык интерфейса | KazistovVvUIManager.cs:1428 | string code = KvLoc.Cycle(); Log("язык интерфейса: " + KvLoc.CurrentName + " (" + code + ")"); | — не в инвентаре |
| `ui.settings` | Настройки | KazistovVvUIManager.cs:1443 | ShowSettings(0); | — не в инвентаре |
| `ui.palette` | Палитра команд | KazistovVvUIManager.cs:1477 | OpenCommandPalette(); | — не в инвентаре |
| `help.hotkeys` | Горячие клавиши | KazistovVvUIManager.cs:1489 | ToggleHotkeys(); | — не в инвентаре |
| `ui.resetlayout` | Сбросить раскладку окон | KazistovVvUIManager.cs:1501 | ResetLayout(); | — не в инвентаре |
| `features.open` | Панель функций (этапы 1–20) | FeatureHub.cs:1518 | hub.Window.Toggle(0); | — не в инвентаре |
| `startmenu.show` | Главное меню | KvStageHub2.cs:647 | hub.ToggleStartMenu(); | — не в инвентаре |
| `tut.toggle` | Обучение | KvStageHub2.cs:674 | hub.ToggleTutorial(); | — не в инвентаре |
| `help.open` | Помощь | KazistovVvUIManager.cs:1454 | ShowSettings(4); | — не в инвентаре |
| `tut.restart` | Обучение заново | KvStageHub2.cs:685 | if (hub.Tutorial != null) hub.Tutorial.RestartTutorial(); | — не в инвентаре |
| `help.about` | О программе | KazistovVvUIManager.cs:1465 | ShowSettings(3); | — не в инвентаре |
| `ui.access` | Доступность | KazistovVvUIManager.cs:1513 | ShowSettings(2); | — не в инвентаре |



#### Группа `misc` — вкладка «Прочее» (полное название «Прочее»): 12 команд, 2 стр.


| command id | Подпись (реестр) | Регистрация (файл:строка) | Что делает код | Замер в оболочке (ArMain) |
|---|---|---|---|---|
| `eta.toggle` | Панель ETA | FeatureHub.cs:1384 | hub.Window.ToggleEta(); | — не в инвентаре |
| `presentation.toggle` | Презентационный режим | FeatureHub.cs:1472 | hub.Presentation.Toggle(); | — не в инвентаре |
| `log.open` | Журнал действий | FeatureHub.cs:1396 | hub.Window.Toggle(7); | — не в инвентаре |
| `demo.quick` | Показать демо | KvStageHub2.cs:660 | hub.PlayDemo(); | — не в инвентаре |
| `obstacle.evaluate` | Проверить траектории на риск | KvStageHub.cs:773 | hub.Obstacles.EvaluateVariants(); | — не в инвентаре |
| `zone.panel` | Зоны запрета: список | FeatureHub.cs:1275 | hub.Window.Toggle(3); | — не в инвентаре |
| `perf.open` | Замер производительности планировщика | FeatureHub.cs:1417 | hub.Window.Toggle(6); | — не в инвентаре |
| `demo.pickplace` | Pick-and-place демо | FeatureHub.cs:1373 | hub.RunPickAndPlace(); | — не в инвентаре |
| `edit.undo` | Отменить | FeatureHub.cs:1428 | hub.UndoLast(); ⚠ недоступна без стека отмены | — не в инвентаре |
| `edit.redo` | Вернуть | FeatureHub.cs:1439 | Повтор отменённого действия ⚠ недоступна без стека повтора | — не в инвентаре |
| `calib.tab` | Калибровочный мастер | KvStageHub3.cs:823 | hub.OpenTab("calibration"); | — не в инвентаре |
| `payload.tab` | Калькулятор нагрузки | KvStageHub3.cs:834 | hub.OpenTab("payload"); | — не в инвентаре |



#### Пагинация `CommandRows` (14 кнопок)


| Элемент | Обработчик | command id | Что делает код | Замер | Дефект / потенциал |
|---|---|---|---|---|---|
| `CmdPrev« ◀» / CmdNext« ▶»` (группа `robot`, 3 стр.) | KvArUi.FillCommandRows — анонимный делегат `commandPage--/++; FillCommandRows()` (KvArUi.cs:758–763) | — | `commandPage` ±1 и полная перестройка строк; номер страницы пишется в `CommandPage` («1 / 3», KvArUi.cs:734). | — не в инвентаре (панель не открывалась) | ⚠ **Дефект раскладки по коду**: позиция пагинации задана константой `y = -4 − 12·48 = -580` (KvArUi.cs:760/763), тогда как шаг строк `rowPitch` считается от экрана (`max(30, min(TouchRowHeightUi, rowsSpace/8))`, KvArUi.cs:739). На планшете 2560×1600 `rowPitch ≈ 74`, восьмая строка занимает `−522…−590` — кнопки страниц **наезжают на последнюю строку**. Замером не проверялось именно поэтому (панель не открывалась). |
| `CmdPrev« ◀» / CmdNext« ▶»` (группа `path`, 2 стр.) | KvArUi.FillCommandRows — анонимный делегат `commandPage--/++; FillCommandRows()` (KvArUi.cs:758–763) | — | `commandPage` ±1 и полная перестройка строк; номер страницы пишется в `CommandPage` («1 / 3», KvArUi.cs:734). | — не в инвентаре (панель не открывалась) | ⚠ **Дефект раскладки по коду**: позиция пагинации задана константой `y = -4 − 12·48 = -580` (KvArUi.cs:760/763), тогда как шаг строк `rowPitch` считается от экрана (`max(30, min(TouchRowHeightUi, rowsSpace/8))`, KvArUi.cs:739). На планшете 2560×1600 `rowPitch ≈ 74`, восьмая строка занимает `−522…−590` — кнопки страниц **наезжают на последнюю строку**. Замером не проверялось именно поэтому (панель не открывалась). |
| `CmdPrev« ◀» / CmdNext« ▶»` (группа `post`, 3 стр.) | KvArUi.FillCommandRows — анонимный делегат `commandPage--/++; FillCommandRows()` (KvArUi.cs:758–763) | — | `commandPage` ±1 и полная перестройка строк; номер страницы пишется в `CommandPage` («1 / 3», KvArUi.cs:734). | — не в инвентаре (панель не открывалась) | ⚠ **Дефект раскладки по коду**: позиция пагинации задана константой `y = -4 − 12·48 = -580` (KvArUi.cs:760/763), тогда как шаг строк `rowPitch` считается от экрана (`max(30, min(TouchRowHeightUi, rowsSpace/8))`, KvArUi.cs:739). На планшете 2560×1600 `rowPitch ≈ 74`, восьмая строка занимает `−522…−590` — кнопки страниц **наезжают на последнюю строку**. Замером не проверялось именно поэтому (панель не открывалась). |
| `CmdPrev« ◀» / CmdNext« ▶»` (группа `view`, 3 стр.) | KvArUi.FillCommandRows — анонимный делегат `commandPage--/++; FillCommandRows()` (KvArUi.cs:758–763) | — | `commandPage` ±1 и полная перестройка строк; номер страницы пишется в `CommandPage` («1 / 3», KvArUi.cs:734). | — не в инвентаре (панель не открывалась) | ⚠ **Дефект раскладки по коду**: позиция пагинации задана константой `y = -4 − 12·48 = -580` (KvArUi.cs:760/763), тогда как шаг строк `rowPitch` считается от экрана (`max(30, min(TouchRowHeightUi, rowsSpace/8))`, KvArUi.cs:739). На планшете 2560×1600 `rowPitch ≈ 74`, восьмая строка занимает `−522…−590` — кнопки страниц **наезжают на последнюю строку**. Замером не проверялось именно поэтому (панель не открывалась). |
| `CmdPrev« ◀» / CmdNext« ▶»` (группа `export`, 3 стр.) | KvArUi.FillCommandRows — анонимный делегат `commandPage--/++; FillCommandRows()` (KvArUi.cs:758–763) | — | `commandPage` ±1 и полная перестройка строк; номер страницы пишется в `CommandPage` («1 / 3», KvArUi.cs:734). | — не в инвентаре (панель не открывалась) | ⚠ **Дефект раскладки по коду**: позиция пагинации задана константой `y = -4 − 12·48 = -580` (KvArUi.cs:760/763), тогда как шаг строк `rowPitch` считается от экрана (`max(30, min(TouchRowHeightUi, rowsSpace/8))`, KvArUi.cs:739). На планшете 2560×1600 `rowPitch ≈ 74`, восьмая строка занимает `−522…−590` — кнопки страниц **наезжают на последнюю строку**. Замером не проверялось именно поэтому (панель не открывалась). |
| `CmdPrev« ◀» / CmdNext« ▶»` (группа `ui`, 2 стр.) | KvArUi.FillCommandRows — анонимный делегат `commandPage--/++; FillCommandRows()` (KvArUi.cs:758–763) | — | `commandPage` ±1 и полная перестройка строк; номер страницы пишется в `CommandPage` («1 / 3», KvArUi.cs:734). | — не в инвентаре (панель не открывалась) | ⚠ **Дефект раскладки по коду**: позиция пагинации задана константой `y = -4 − 12·48 = -580` (KvArUi.cs:760/763), тогда как шаг строк `rowPitch` считается от экрана (`max(30, min(TouchRowHeightUi, rowsSpace/8))`, KvArUi.cs:739). На планшете 2560×1600 `rowPitch ≈ 74`, восьмая строка занимает `−522…−590` — кнопки страниц **наезжают на последнюю строку**. Замером не проверялось именно поэтому (панель не открывалась). |
| `CmdPrev« ◀» / CmdNext« ▶»` (группа `misc`, 2 стр.) | KvArUi.FillCommandRows — анонимный делегат `commandPage--/++; FillCommandRows()` (KvArUi.cs:758–763) | — | `commandPage` ±1 и полная перестройка строк; номер страницы пишется в `CommandPage` («1 / 3», KvArUi.cs:734). | — не в инвентаре (панель не открывалась) | ⚠ **Дефект раскладки по коду**: позиция пагинации задана константой `y = -4 − 12·48 = -580` (KvArUi.cs:760/763), тогда как шаг строк `rowPitch` считается от экрана (`max(30, min(TouchRowHeightUi, rowsSpace/8))`, KvArUi.cs:739). На планшете 2560×1600 `rowPitch ≈ 74`, восьмая строка занимает `−522…−590` — кнопки страниц **наезжают на последнюю строку**. Замером не проверялось именно поэтому (панель не открывалась). |

---

### 2.4 Корень C — оболочка ПК-интерфейса (93 элемента в инвентаре + динамика)

#### 2.4.1 Строка меню `KvMenuBar` (6) и плашка `KvMenuBlocker` (1)


| Элемент | Обработчик | command id | Что делает код | Замер (effect=) | Дефект / потенциал |
|---|---|---|---|---|---|
| `Menu_Файл«Файл»` | KvMenuBar.ToggleMenu — KvMenuBar.cs:93→135 (создание: KvMenuBar.cs:64) | — (раздел) | `BuildDropdown(menu)` (KvMenuBar.cs:171): перебор `KvCommands.All` по `MenuPath`, первый сегмент = раздел; строки `Item_<подпись>` → `CloseMenu(); KvCommands.Invoke(id)` (KvMenuBar.cs:329–334). Подсказка состояния — `MenuFingerprint` (KvMenuBar.cs:402). | ✓ «элементы оболочки: 438→486; выделение uGUI: 0→1; объектов сцены: 1522→1570» (отчёт:99) | Замер фиксирует и счётчик «элементы оболочки» (438→486 и т.д.) — выпадающий список реально строился. Потенциал: v1 §1 «Файл пустой по сути» неверен для AR: в оболочке 15 пунктов, в AR-меню — 12. |
| `Menu_Правка«Правка»` | KvMenuBar.ToggleMenu — KvMenuBar.cs:93→135 (создание: KvMenuBar.cs:64) | — (раздел) | `BuildDropdown(menu)` (KvMenuBar.cs:171): перебор `KvCommands.All` по `MenuPath`, первый сегмент = раздел; строки `Item_<подпись>` → `CloseMenu(); KvCommands.Invoke(id)` (KvMenuBar.cs:329–334). Подсказка состояния — `MenuFingerprint` (KvMenuBar.cs:402). | ✓ «элементы оболочки: 486→448; выделение uGUI: 0→1; объектов сцены: 1570→1532» (отчёт:101) | Замер фиксирует и счётчик «элементы оболочки» (438→486 и т.д.) — выпадающий список реально строился. Потенциал: v1 §1 «Файл пустой по сути» неверен для AR: в оболочке 15 пунктов, в AR-меню — 12. |
| `Menu_Вид«Вид»` | KvMenuBar.ToggleMenu — KvMenuBar.cs:93→135 (создание: KvMenuBar.cs:64) | — (раздел) | `BuildDropdown(menu)` (KvMenuBar.cs:171): перебор `KvCommands.All` по `MenuPath`, первый сегмент = раздел; строки `Item_<подпись>` → `CloseMenu(); KvCommands.Invoke(id)` (KvMenuBar.cs:329–334). Подсказка состояния — `MenuFingerprint` (KvMenuBar.cs:402). | ✓ «элементы оболочки: 448→576; выделение uGUI: 0→1; объектов сцены: 1532→1660» (отчёт:103) | Замер фиксирует и счётчик «элементы оболочки» (438→486 и т.д.) — выпадающий список реально строился. Потенциал: v1 §1 «Файл пустой по сути» неверен для AR: в оболочке 15 пунктов, в AR-меню — 12. |
| `Menu_Робот«Робот»` | KvMenuBar.ToggleMenu — KvMenuBar.cs:93→135 (создание: KvMenuBar.cs:64) | — (раздел) | `BuildDropdown(menu)` (KvMenuBar.cs:171): перебор `KvCommands.All` по `MenuPath`, первый сегмент = раздел; строки `Item_<подпись>` → `CloseMenu(); KvCommands.Invoke(id)` (KvMenuBar.cs:329–334). Подсказка состояния — `MenuFingerprint` (KvMenuBar.cs:402). | ✓ «элементы оболочки: 576→580; выделение uGUI: 0→1; объектов сцены: 1660→1664» (отчёт:105) | Замер фиксирует и счётчик «элементы оболочки» (438→486 и т.д.) — выпадающий список реально строился. Потенциал: v1 §1 «Файл пустой по сути» неверен для AR: в оболочке 15 пунктов, в AR-меню — 12. |
| `Menu_Сервис«Сервис»` | KvMenuBar.ToggleMenu — KvMenuBar.cs:93→135 (создание: KvMenuBar.cs:64) | — (раздел) | `BuildDropdown(menu)` (KvMenuBar.cs:171): перебор `KvCommands.All` по `MenuPath`, первый сегмент = раздел; строки `Item_<подпись>` → `CloseMenu(); KvCommands.Invoke(id)` (KvMenuBar.cs:329–334). Подсказка состояния — `MenuFingerprint` (KvMenuBar.cs:402). | ✓ «элементы оболочки: 580→535; выделение uGUI: 0→1; объектов сцены: 1664→1619» (отчёт:107) | Замер фиксирует и счётчик «элементы оболочки» (438→486 и т.д.) — выпадающий список реально строился. Потенциал: v1 §1 «Файл пустой по сути» неверен для AR: в оболочке 15 пунктов, в AR-меню — 12. |
| `Menu_Справка«Справка»` | KvMenuBar.ToggleMenu — KvMenuBar.cs:93→135 (создание: KvMenuBar.cs:64) | — (раздел) | `BuildDropdown(menu)` (KvMenuBar.cs:171): перебор `KvCommands.All` по `MenuPath`, первый сегмент = раздел; строки `Item_<подпись>` → `CloseMenu(); KvCommands.Invoke(id)` (KvMenuBar.cs:329–334). Подсказка состояния — `MenuFingerprint` (KvMenuBar.cs:402). | ✓ «элементы оболочки: 535→452; выделение uGUI: 0→1; объектов сцены: 1619→1536» (отчёт:109) | Замер фиксирует и счётчик «элементы оболочки» (438→486 и т.д.) — выпадающий список реально строился. Потенциал: v1 §1 «Файл пустой по сути» неверен для AR: в оболочке 15 пунктов, в AR-меню — 12. |
| `KvMenuBlocker` | KvMenuBar.BuildBlocker → CloseMenu — KvMenuBar.cs:120–131/163 | — | Прозрачная плашка во весь канвас; `raycastTarget = true`, `transition = None`; клик закрывает открытое меню. Скрыта, пока меню не открыто (`SetActive(false)`, KvMenuBar.cs:130). | ✓! «выделение uGUI: 0→1» (отчёт:111) | **Исключение прогона**: «закрывающая плашка: меню не открыто — нажимать нечего» (отчёт:111) — геттер `AnyOpen` (KvMenuBar.cs:39) честно отличает «нечего закрывать» от «кнопка молчит». Потенциал: подсказка «нажмите для меню» (v1 §6). |


#### 2.4.2 Пункты выпадающего меню оболочки `KvMenuDropdown` — 122 кнопки

Строка `Item_<подпись>` создаётся `KvMenuBar.AddItem` (`KvMenuBar.cs:260–350`): галка `Check`
для тумблеров, `interactable = command.Enabled` (KvMenuBar.cs:318), подпись/горячая клавиша из
реестра, обработчик — `CloseMenu(); KvCommands.Invoke(id)` (KvMenuBar.cs:329–334).
Заголовки групп (`Group`) и разделители (`Sep`) — тексты/картинки, кнопок не дают.
Состав разделов — по `MenuPath` реестра (первый сегмент): **в инвентарь не попал** (лимит 140,
список собирается до открытия меню). Полный состав (тот же реестр, что в §2.3):


- **«Файл» — 15 строк**: `export.lang`, `export.robot`, `export.tab`, `file.export`, `file.import`, `import.scan`, `import.tab`, `log.export`, `session.load`, `session.panel`, `session.save`, `shot.annotate`, `shot.screenshot`, `startmenu.show`, `video.toggle`

- **«Правка» — 3 строк**: `edit.redo`, `edit.reset`, `edit.undo`

- **«Вид» — 30 строк**: `compare.mark`, `compare.open`, `eta.toggle`, `features.open`, `graph.open`, `health.toggle`, `obstacle.discard`, `obstacle.evaluate`, `obstacle.toggle`, `presentation.toggle`, `tool.flashlight`, `ui.palette`, `ui.resetlayout`, `ui.toggle`, `view.clearance`, `view.heatmap`, `view.limits`, `view.metrics`, `view.panel.properties`, `view.panel.settings`, `view.panel.tree`, `view.singularity`, `view.theme`, `view.workspace`, `workbench.toggle`, `zone.box`, `zone.cylinder`, `zone.discard`, `zone.panel`, `zone.sphere`

- **«Робот» — 46 строк**: `constr.tab`, `constr.toggle`, `estop`, `gripper.toggle`, `joints.panel`, `path.select`, `point.select`, `pose.goto`, `pose.save`, `post.apply.energy`, `post.apply.smooth`, `post.apply.topt`, `post.auto`, `post.auto.selected`, `post.energy`, `post.smooth`, `post.timeoptimal`, `robot.playpause`, `robot.stop`, `robot.switch`, `waypoint.add`, `waypoint.build`, `waypoint.clear`, `waypoint.delete`, `waypoint.earlier`, `waypoint.later`, `waypoint.move`, `waypoint.play`, `wp.clear`, `wp.detour`, `wp.orient`, `wp.pause`, `wp.speed`, `wp.tab`, `robot.model.stand_6axis`, `robot.model.scara_ls10b702s`, `robot.model.ur5`, `robot.model.kuka_iiwa7`, `robot.model.kuka_iiwa14`, `robot.model.abb_irb6640`, `robot.model.fanuc_m20ib`, `robot.model.franka_fr3`, `robot.model.franka_fr3_v2`, `robot.model.kinova_gen3`, `robot.model.comau_edo`, `robot.model.openarm`

- **«Сервис» — 23 строк**: `calib.tab`, `demo.pickplace`, `demo.quick`, `gamepad.hud`, `lang.cycle`, `log.open`, `payload.tab`, `pendant.attach`, `pendant.jogmode`, `pendant.toggle`, `pendant.variant`, `perf.open`, `record.play`, `record.speed`, `record.toggle`, `scenarios.open`, `scenarios.run`, `tool.grid`, `tool.grid.clear`, `tool.measure`, `tool.measure.reset`, `ui.access`, `ui.settings`

- **«Справка» — 5 строк**: `help.about`, `help.hotkeys`, `help.open`, `tut.restart`, `tut.toggle`


Обработчик у всех один: `KvCommands.Invoke` (`KvCommands.cs:131`) → `Execute()` + счётчик `ExecutedCount` (`KvCommands.cs:146–154`) + строка в `Logs/actions_*.log` (`KvUiJournal.Command`, KvCommands.cs:163). Недоступные команды (`Enabled == false`) не выполняются и пишут причину (KvCommands.cs:135–144).


#### 2.4.3 Ручки групп `KvToolbar/Groups/Group_*` (8)


| Элемент | Обработчик | command id | Что делает код | Замер (effect=) | Дефект / потенциал |
|---|---|---|---|---|---|
| `Group_robot` | KvToolbar.AddGroupHandle → ShowGroupMenu — KvToolbar.cs:382/434 (создание: KvToolbar.cs:379–394) | группа `robot` («Робот») | `KvToolbarGroupMenu.Toggle` (KvToolbarGroups.cs:237): открывает/закрывает список `KvGroupMenu` под кнопкой; в списке 21 строк команд + действие «Свернуть/развернуть группу» + блокер `KvGroupMenuBlocker`. Состояние «свёрнута» хранится в `PlayerPrefs` (`KvSettings.IsToolbarGroupCollapsed`, KvToolbar.cs:486). | ✓ «элементы оболочки: 452→526; выделение uGUI: 0→1; объектов сцены: 1524→1598» (отчёт:151) | ✓ группа проверена целиком |
| `Group_path` | KvToolbar.AddGroupHandle → ShowGroupMenu — KvToolbar.cs:382/434 (создание: KvToolbar.cs:379–394) | группа `path` («Траектория») | `KvToolbarGroupMenu.Toggle` (KvToolbarGroups.cs:237): открывает/закрывает список `KvGroupMenu` под кнопкой; в списке 13 строк команд + действие «Свернуть/развернуть группу» + блокер `KvGroupMenuBlocker`. Состояние «свёрнута» хранится в `PlayerPrefs` (`KvSettings.IsToolbarGroupCollapsed`, KvToolbar.cs:486). | ✓ «выделение uGUI: 0→1» (отчёт:189) | ✓ группа проверена целиком |
| `Group_post` | KvToolbar.AddGroupHandle → ShowGroupMenu — KvToolbar.cs:382/434 (создание: KvToolbar.cs:379–394) | группа `post` («Постобработка») | `KvToolbarGroupMenu.Toggle` (KvToolbarGroups.cs:237): открывает/закрывает список `KvGroupMenu` под кнопкой; в списке 17 строк команд + действие «Свернуть/развернуть группу» + блокер `KvGroupMenuBlocker`. Состояние «свёрнута» хранится в `PlayerPrefs` (`KvSettings.IsToolbarGroupCollapsed`, KvToolbar.cs:486). | ✓ «элементы оболочки: 526→513; выделение uGUI: 0→1; объектов сцены: 1628→1615» (отчёт:211) | ✓ группа проверена целиком |
| `Group_view` | KvToolbar.AddGroupHandle → ShowGroupMenu — KvToolbar.cs:382/434 (создание: KvToolbar.cs:379–394) | группа `view` («Визуализация») | `KvToolbarGroupMenu.Toggle` (KvToolbarGroups.cs:237): открывает/закрывает список `KvGroupMenu` под кнопкой; в списке 22 строк команд + действие «Свернуть/развернуть группу» + блокер `KvGroupMenuBlocker`. Состояние «свёрнута» хранится в `PlayerPrefs` (`KvSettings.IsToolbarGroupCollapsed`, KvToolbar.cs:486). | ✓ «выделение uGUI: 0→1» (отчёт:247) | ✓ группа проверена целиком |
| `Group_export` | KvToolbar.AddGroupHandle → ShowGroupMenu — KvToolbar.cs:382/434 (создание: KvToolbar.cs:379–394) | группа `export` («Запись и экспорт») | `KvToolbarGroupMenu.Toggle` (KvToolbarGroups.cs:237): открывает/закрывает список `KvGroupMenu` под кнопкой; в списке 17 строк команд + действие «Свернуть/развернуть группу» + блокер `KvGroupMenuBlocker`. Состояние «свёрнута» хранится в `PlayerPrefs` (`KvSettings.IsToolbarGroupCollapsed`, KvToolbar.cs:486). | ✗ «не нажимали — панель так и не открылась» (отчёт:332) | ✓ группа проверена целиком |
| `Group_net` | KvToolbar.AddGroupHandle → ShowGroupMenu — KvToolbar.cs:382/434 (создание: KvToolbar.cs:379–394) | группа `net` («Сеть и автоматизация») | `KvToolbarGroupMenu.Toggle` (KvToolbarGroups.cs:237): открывает/закрывает список `KvGroupMenu` под кнопкой; в списке 7 строк команд + действие «Свернуть/развернуть группу» + блокер `KvGroupMenuBlocker`. Состояние «свёрнута» хранится в `PlayerPrefs` (`KvSettings.IsToolbarGroupCollapsed`, KvToolbar.cs:486). | — не в инвентаре (лимит 140: сбор оборван в группе «Запись и экспорт») | ⚠ **группы `net`/`ui`/`misc` не попали в инвентарь** — их ручки и кнопки не проверены вовсе (§3). |
| `Group_ui` | KvToolbar.AddGroupHandle → ShowGroupMenu — KvToolbar.cs:382/434 (создание: KvToolbar.cs:379–394) | группа `ui` («Настройки») | `KvToolbarGroupMenu.Toggle` (KvToolbarGroups.cs:237): открывает/закрывает список `KvGroupMenu` под кнопкой; в списке 13 строк команд + действие «Свернуть/развернуть группу» + блокер `KvGroupMenuBlocker`. Состояние «свёрнута» хранится в `PlayerPrefs` (`KvSettings.IsToolbarGroupCollapsed`, KvToolbar.cs:486). | — не в инвентаре (лимит 140: сбор оборван в группе «Запись и экспорт») | ⚠ **группы `net`/`ui`/`misc` не попали в инвентарь** — их ручки и кнопки не проверены вовсе (§3). |
| `Group_misc` | KvToolbar.AddGroupHandle → ShowGroupMenu — KvToolbar.cs:382/434 (создание: KvToolbar.cs:379–394) | группа `misc` («Прочее») | `KvToolbarGroupMenu.Toggle` (KvToolbarGroups.cs:237): открывает/закрывает список `KvGroupMenu` под кнопкой; в списке 12 строк команд + действие «Свернуть/развернуть группу» + блокер `KvGroupMenuBlocker`. Состояние «свёрнута» хранится в `PlayerPrefs` (`KvSettings.IsToolbarGroupCollapsed`, KvToolbar.cs:486). | — не в инвентаре (лимит 140: сбор оборван в группе «Запись и экспорт») | ⚠ **группы `net`/`ui`/`misc` не попали в инвентарь** — их ручки и кнопки не проверены вовсе (§3). |


#### 2.4.4 Кнопки команд `KvToolbar/Groups/Cmd_*` — 122 по коду, 81 в инвентаре

Создание: `KvToolbar.AddButton` (`KvToolbar.cs:344–359`) — `KvWidgets.IconButton(gridRect, "Cmd_" + id, …)`
с обработчиком `KvCommands.Invoke(id)` (`KvToolbar.cs:350`), подсказка собирается один раз при сборке
(`TooltipBodyFor`, KvToolbar.cs:365), недоступность/галка обновляются в `Refresh` (`KvToolbar.cs:573`).
Компактный режим `KvSettings.ToolbarCompact` выкидывает 10 «редких» id (`RareCommandIds`, KvToolbar.cs:62) —
если включён, часть кнопок не рисуется вовсе.


**Замерено 81 из 122**:


| Группа | command id | Подпись | Замер | effect= (дословно) | Строка / исключение |
|---|---|---|---|---|---|
| `robot` | `robot.switch` | Переключение робота (робот / SCARA) | ✓ | выделение uGUI: 0→1; команд выполнено: 17→18 | отчёт:153 |
| `robot` | `robot.playpause` | Запуск / пауза | ✗ | нет изменений | отчёт:155 · исключение: элемент недоступен (interactable=false) |
| `robot` | `robot.stop` | Остановка | ✗ | нет изменений | отчёт:157 · исключение: элемент недоступен (interactable=false) |
| `robot` | `estop` | АВАРИЙНАЯ ОСТАНОВКА | ✓ | выделение uGUI: 0→1; команд выполнено: 18→19 | отчёт:159 |
| `robot` | `edit.reset` | Сброс сценария | ✗ | не нажимали — панель так и не открылась | отчёт:326 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |
| `robot` | `joints.panel` | Панель суставов | ✓ | выделение uGUI: 0→1; команд выполнено: 4→5; объектов сцены: 1536→1505 | отчёт:113 |
| `robot` | `pose.save` | Сохранить текущую позу как… | ✓ | выделение uGUI: 0→1; команд выполнено: 19→20; объектов сцены: 1598→1618 | отчёт:161 |
| `robot` | `pose.goto` | Перейти в позу | ✓ | выделение uGUI: 0→1; команд выполнено: 20→21 | отчёт:163 |
| `robot` | `gripper.toggle` | Захват: открыть / закрыть | ✓ | выделение uGUI: 0→1; команд выполнено: 21→22 | отчёт:165 |
| `robot` | `robot.model.stand_6axis` | Робот: robot.model.stand_6axis | ✓ | строки дерева: 6→7; выделение uGUI: 0→1; команд выполнено: 22→23; объектов сцены: 1618→1680 | отчёт:167 |
| `robot` | `robot.model.scara_ls10b702s` | Робот: robot.model.scara_ls10b702s | ✓ | выделение uGUI: 0→1; команд выполнено: 23→24; объектов сцены: 1661→1634 | отчёт:169 |
| `robot` | `robot.model.ur5` | Робот: robot.model.ur5 | ✗ | нет изменений | отчёт:171 · исключение: элемент недоступен (interactable=false) |
| `robot` | `robot.model.kuka_iiwa7` | Робот: robot.model.kuka_iiwa7 | ✗ | нет изменений | отчёт:173 · исключение: элемент недоступен (interactable=false) |
| `robot` | `robot.model.kuka_iiwa14` | Робот: robot.model.kuka_iiwa14 | ✗ | нет изменений | отчёт:175 · исключение: элемент недоступен (interactable=false) |
| `robot` | `robot.model.abb_irb6640` | Робот: robot.model.abb_irb6640 | ✗ | нет изменений | отчёт:177 · исключение: элемент недоступен (interactable=false) |
| `robot` | `robot.model.fanuc_m20ib` | Робот: robot.model.fanuc_m20ib | ✗ | нет изменений | отчёт:179 · исключение: элемент недоступен (interactable=false) |
| `robot` | `robot.model.franka_fr3` | Робот: robot.model.franka_fr3 | ✗ | нет изменений | отчёт:181 · исключение: элемент недоступен (interactable=false) |
| `robot` | `robot.model.franka_fr3_v2` | Робот: robot.model.franka_fr3_v2 | ✓! | объектов сцены: 1634→1641 | отчёт:183 · исключение: элемент недоступен (interactable=false) |
| `robot` | `robot.model.kinova_gen3` | Робот: robot.model.kinova_gen3 | ✗ | нет изменений | отчёт:185 · исключение: элемент недоступен (interactable=false) |
| `robot` | `robot.model.comau_edo` | Робот: robot.model.comau_edo | ✗ | нет изменений | отчёт:187 · исключение: элемент недоступен (interactable=false) |
| `robot` | `robot.model.openarm` | Робот: robot.model.openarm | ✗ | нет изменений | отчёт:115 · исключение: элемент недоступен (interactable=false) |
| `path` | `point.select` | Выбор точки (красный лазер) | ✓ | выделение uGUI: 0→1; команд выполнено: 24→25 | отчёт:191 |
| `path` | `path.select` | Выбор траектории (зелёный лазер) | ✓ | выделение uGUI: 0→1; команд выполнено: 25→26 | отчёт:193 |
| `path` | `waypoint.add` | Добавить waypoint | ✓ | выделение uGUI: 0→1; команд выполнено: 26→27 | отчёт:195 |
| `path` | `waypoint.move` | Переместить waypoint | ✓ | выделение uGUI: 0→1; команд выполнено: 27→28 | отчёт:197 |
| `path` | `waypoint.delete` | Удалить waypoint | ✗ | не нажимали — панель так и не открылась | отчёт:328 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |
| `path` | `waypoint.build` | Перестроить маршрут | ✓ | выделение uGUI: 0→1; команд выполнено: 28→29 | отчёт:199 |
| `path` | `waypoint.play` | Выполнить маршрут | ✓ | выделение uGUI: 0→1; команд выполнено: 29→30 | отчёт:201 |
| `path` | `compare.mark` | Отметить траекторию для сравнения | ✓ | выделение uGUI: 0→1; команд выполнено: 30→31 | отчёт:203 |
| `path` | `compare.open` | Сравнение траекторий | ✓ | выделение uGUI: 0→1; команд выполнено: 5→6 | отчёт:117 |
| `path` | `graph.open` | Графики углов суставов | ✓ | выделение uGUI: 0→1; команд выполнено: 6→7 | отчёт:119 |
| `path` | `waypoint.earlier` | Waypoint раньше | ✓ | выделение uGUI: 0→1; команд выполнено: 31→32 | отчёт:205 |
| `path` | `waypoint.later` | Waypoint позже | ✓ | выделение uGUI: 0→1; команд выполнено: 32→33 | отчёт:207 |
| `path` | `waypoint.clear` | Очистить waypoints | ✓ | выделение uGUI: 0→1; команд выполнено: 33→34 | отчёт:209 |
| `post` | `post.smooth` | Сглаживание траектории | ✓ | выделение uGUI: 0→1; команд выполнено: 34→35 | отчёт:213 |
| `post` | `post.timeoptimal` | Время-оптимальная | ✓ | выделение uGUI: 0→1; команд выполнено: 35→36; объектов сцены: 1615→1617 | отчёт:215 |
| `post` | `post.energy` | Эко-профиль | ✓ | выделение uGUI: 0→1; команд выполнено: 36→37; объектов сцены: 1617→1586 | отчёт:217 |
| `post` | `post.auto` | Автосглаживание после планирования | ✓ | выделение uGUI: 0→1; команд выполнено: 37→38 | отчёт:219 |
| `post` | `workbench.toggle` | Верстак постобработки | ✓ | выделение uGUI: 0→1; команд выполнено: 38→39 | отчёт:221 |
| `post` | `constr.tab` | Вкладка ограничений | ✓ | выделение uGUI: 0→1; команд выполнено: 39→40 | отчёт:223 |
| `post` | `constr.toggle` | Ограничения планирования | ✓ | выделение uGUI: 0→1; команд выполнено: 40→41 | отчёт:225 |
| `post` | `wp.tab` | Ограничения точки | ✓ | выделение uGUI: 0→1; команд выполнено: 41→42 | отчёт:227 |
| `post` | `post.apply.smooth` | Сгладить выбранную траекторию | ✓ | выделение uGUI: 0→1; команд выполнено: 42→43 | отчёт:229 |
| `post` | `post.apply.topt` | Переключиться на время-оптимальную | ✓ | выделение uGUI: 0→1; команд выполнено: 43→44 | отчёт:231 |
| `post` | `post.apply.energy` | Переключиться на эко-профиль | ✓ | выделение uGUI: 0→1; команд выполнено: 44→45 | отчёт:233 |
| `post` | `post.auto.selected` | Сглаживать только выбранный вариант | ✓ | выделение uGUI: 0→1; команд выполнено: 45→46 | отчёт:235 |
| `post` | `wp.orient` | Ориентация waypoint | ✓ | выделение uGUI: 0→1; команд выполнено: 46→47 | отчёт:237 |
| `post` | `wp.speed` | Скорость waypoint | ✓ | выделение uGUI: 0→1; команд выполнено: 47→48 | отчёт:239 |
| `post` | `wp.detour` | Обход препятствия (waypoint) | ✓ | выделение uGUI: 0→1; команд выполнено: 48→49 | отчёт:241 |
| `post` | `wp.pause` | Пауза waypoint | ✓ | выделение uGUI: 0→1; команд выполнено: 49→50 | отчёт:243 |
| `post` | `wp.clear` | Снять ограничения waypoint | ✓ | выделение uGUI: 0→1; команд выполнено: 50→51 | отчёт:245 |
| `view` | `view.workspace` | Зона достижимости | ✓ | выделение uGUI: 0→1; команд выполнено: 51→52 | отчёт:249 |
| `view` | `view.limits` | Лимиты суставов | ✓ | выделение uGUI: 0→1; команд выполнено: 52→53 | отчёт:251 |
| `view` | `view.metrics` | Панель метрик траекторий | ✓ | выделение uGUI: 0→1; команд выполнено: 53→54 | отчёт:253 |
| `view` | `view.singularity` | Сингулярности | ✓ | выделение uGUI: 0→1; команд выполнено: 54→55 | отчёт:255 |
| `view` | `view.heatmap` | Тепловая карта достижимости | ✓ | выделение uGUI: 0→1; команд выполнено: 55→56 | отчёт:257 |
| `view` | `view.clearance` | Тепловая карта зазоров | ✓ | выделение uGUI: 0→1; команд выполнено: 56→57 | отчёт:259 |
| `view` | `health.toggle` | Мониторинг состояния | ✓ | выделение uGUI: 0→1; команд выполнено: 57→58 | отчёт:261 |
| `view` | `obstacle.toggle` | Динамические препятствия | ✓ | выделение uGUI: 0→1; команд выполнено: 58→59; объектов сцены: 1586→1616 | отчёт:263 |
| `view` | `zone.box` | Зона запрета: куб | ✓ | выделение uGUI: 0→1; команд выполнено: 59→60 | отчёт:265 |
| `view` | `zone.sphere` | Зона запрета: сфера | ✓ | выделение uGUI: 0→1; команд выполнено: 60→61 | отчёт:267 |
| `view` | `zone.cylinder` | Зона запрета: цилиндр | ✓ | выделение uGUI: 0→1; команд выполнено: 61→62 | отчёт:269 |
| `view` | `zone.discard` | Отбрасывать опасные траектории | ✓ | выделение uGUI: 0→1; команд выполнено: 62→63 | отчёт:271 |
| `view` | `tool.measure` | Измерение | ✓ | выделение uGUI: 0→1; команд выполнено: 63→64 | отчёт:273 |
| `view` | `tool.measure.reset` | Измерение: сбросить | ✗ | не нажимали — панель так и не открылась | отчёт:330 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |
| `view` | `tool.grid` | Сетка рабочей зоны | ✓ | выделение uGUI: 0→1; команд выполнено: 64→65; объектов сцены: 1616→1635 | отчёт:275 |
| `view` | `tool.grid.clear` | Сетка: убрать | ✓ | выделение uGUI: 0→1; команд выполнено: 65→66; объектов сцены: 1635→1597 | отчёт:277 |
| `view` | `obstacle.discard` | Отбрасывать рискованные траектории | ✓ | выделение uGUI: 0→1; команд выполнено: 66→67 | отчёт:279 |
| `view` | `tool.flashlight` | Фонарик | ✓ | выделение uGUI: 0→1; команд выполнено: 67→68 | отчёт:281 |
| `view` | `view.panel.tree` | Дерево моделей | ✓ | выделение uGUI: 0→1; команд выполнено: 7→8 | отчёт:121 |
| `view` | `view.panel.properties` | Свойства | ✓ | панели: 2→1; выделение uGUI: 0→1; команд выполнено: 8→9; отпечаток панелей дока: -285342597→1370569357; панель properties: видна 1→0; панель settings: ширина 157→194 | отчёт:123 |
| `view` | `view.panel.settings` | Настройки и справка | ✓ | панели: 2→3; выделение uGUI: 0→1; команд выполнено: 9→10; отпечаток панелей дока: -285342597→110853938; панель settings: видна 0→1 | отчёт:125 |
| `view` | `ui.toggle` | Показать / скрыть интерфейс | ✓ | выделение uGUI: 0→1; команд выполнено: 68→69 | отчёт:283 |
| `export` | `record.toggle` | Записать движение | ✗ | не нажимали — панель так и не открылась | отчёт:334 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |
| `export` | `record.play` | Проиграть запись | ✗ | не нажимали — панель так и не открылась | отчёт:336 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |
| `export` | `shot.screenshot` | Сделать скриншот | ✗ | не нажимали — панель так и не открылась | отчёт:314 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |
| `export` | `video.toggle` | Начать / остановить запись | ✗ | не нажимали — панель так и не открылась | отчёт:316 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |
| `export` | `export.robot` | Экспорт в язык робота | ✗ | не нажимали — панель так и не открылась | отчёт:338 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |
| `export` | `import.tab` | Импорт модели робота | ✗ | не нажимали — панель так и не открылась | отчёт:318 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |
| `export` | `session.save` | Сохранить сессию | ✗ | не нажимали — панель так и не открылась | отчёт:320 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |
| `export` | `session.load` | Загрузить сессию | ✗ | не нажимали — панель так и не открылась | отчёт:322 · исключение: элемент в скрытой панели — сначала откройте её штатной кнопкой (KazistovVv_UI/KazistovVvCanvas«Файл») |


**Не попали в инвентарь 41 кнопок** (лимит `MaxElements=140` — сбор оборвался в середине группы «Запись и экспорт»):


- `export` (9): `record.speed`, `log.export`, `session.panel`, `file.export`, `shot.annotate`, `export.lang`, `export.tab`, `import.scan`, `file.import`

- `net` (7): `scenarios.open`, `scenarios.run`, `pendant.toggle`, `pendant.variant`, `pendant.attach`, `pendant.jogmode`, `gamepad.hud`

- `ui` (13): `view.theme`, `lang.cycle`, `ui.settings`, `ui.palette`, `help.hotkeys`, `ui.resetlayout`, `features.open`, `startmenu.show`, `tut.toggle`, `help.open`, `tut.restart`, `help.about`, `ui.access`

- `misc` (12): `eta.toggle`, `presentation.toggle`, `log.open`, `demo.quick`, `obstacle.evaluate`, `zone.panel`, `perf.open`, `demo.pickplace`, `edit.undo`, `edit.redo`, `calib.tab`, `payload.tab`


Эти кнопки **в AR существуют и рабочие** (все 122 id зарегистрированы и имеют `Execute`), но прогон их не видел — замера нет ни по одной. Проверка: поднять `KvUiSweepRuntime.MaxElements` (сейчас 140) либо снять компактный режим и прогнать заново.


#### 2.4.5 «Верстак» `KvToolbar/Workbench` (2 сегмента)


| Элемент | Обработчик | command id | Что делает код | Замер | Дефект / потенциал |
|---|---|---|---|---|---|
| `Seg_Workbench/Seg0«Робот (6 осей)»` | KvWidgets.Segmented → `onWorkbenchChanged` — KvWidgets.cs:374–417, KvToolbar.cs:161–167 | — (переключатель, не команда) | `KazistovVvUIManager.SelectRobotByWorkbench(i)` — быстрый выбор активного робота; сегменты создаются обычными `Button` (`KvTheme.CreateButton(row, "Seg"+i, …)`, KvWidgets.cs:393). | — не в инвентаре (лимит 140) | В AR не проверено. Потенциал: дублирует `robot.switch`; на планшете сегменты мелкие (60 px ширины по `Fit`). |
| `Seg_Workbench/Seg1«SCARA»` | то же | — | то же | — не в инвентаре (лимит 140) | то же |


#### 2.4.6 Список группы `KvGroupMenu` (динамика: 131 кнопка)

Строятся при нажатии `Group_*`: заголовок (текст), строки `GM_<id>` → `Close(); KvCommands.Invoke(id)`
(`KvToolbarGroups.cs:431–436`), разделитель `Sep` (картинка), действие `GM_Action`
«Свернуть группу в иконку / Развернуть группу» → `KvToolbar.SetGroupCollapsed`
(`KvToolbarGroups.cs:343–348`, `KvToolbar.cs:492`) и плашка `KvGroupMenuBlocker` → `Close()`
(`KvToolbarGroups.cs:273–285`). Кнопок: 122 (`GM_*`) + 8 (`GM_Action`) + 1 (блокер) = **131**.
Недоступные команды серые и не нажимаются (`btn.interactable = enabled`, KvToolbarGroups.cs:379);
заглушки (`command.Stub`) помечались бы подсказкой «в разработке» (KvToolbarGroups.cs:419) —
**сейчас заглушек в реестре нет ни одной** (поиск `Stub = true` находит только комментарии и тесты:
`KazistovVvUIManager.cs:1568`, `FeatureHub.cs:1552`, `KvMeasureKitTests.cs:5`).
**Замер:** ни одна строка списка в инвентарь не попала (список создаётся после сбора, лимит 140);
открытие списка видно косвенно — «элементы оболочки: 452→526» при `Group_robot` (отчёт:151).

#### 2.4.7 Заголовки док-панелей `KazistovVvDockCanvas` (12 кнопок: 4 дока × 3)


| Элемент | Обработчик | command id | Что делает код | Замер (effect=) | Дефект / потенциал |
|---|---|---|---|---|---|
| `Dock_Дерево моделей/Header/Collapse` | KvDockPanel.ToggleCollapsed — KvDockPanel.cs:317 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `SetCollapsed(!Collapsed)` — свернуть/развернуть тело панели (KvDockPanel.cs:317–333). | — не в инвентаре (лимит 140) | Панель не проверена: доки `tree`/`settings`/`hotkeys` не попали в прогон вообще. |
| `Dock_Дерево моделей/Header/Dock` | KvDockPanel.ToggleFloat — KvDockPanel.cs:379 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `SetSide(Float ↔ Left)` — открепить в плавающее окно / прикрепить к краю (KvDockPanel.cs:379–394). | — не в инвентаре (лимит 140) | Панель не проверена: доки `tree`/`settings`/`hotkeys` не попали в прогон вообще. |
| `Dock_Дерево моделей/Header/Close` | KvDockPanel.ToRail — KvDockPanel.cs:335 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `InRail = true` — свернуть окно в боковую «полочку»; клик по полочке вернёт (KvDockPanel.cs:335–344). | — не в инвентаре (лимит 140) | Панель не проверена: доки `tree`/`settings`/`hotkeys` не попали в прогон вообще. |
| `Dock_Свойства/Header/Collapse` | KvDockPanel.ToggleCollapsed — KvDockPanel.cs:317 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `SetCollapsed(!Collapsed)` — свернуть/развернуть тело панели (KvDockPanel.cs:317–333). | ✓ «выделение uGUI: 0→1; отпечаток панелей дока: -285342597→1927212508; панель properties: свёрнута 0→1; панель properties: ширина 37→3; панель settings: ширина 157→191» (отчёт:127) | Отпечаток панелей дока изменился; состояние хранится в `PlayerPrefs` (`KvLayoutStore`), поэтому нажатие меняет раскладку навсегда (отчёт:127–131). |
| `Dock_Свойства/Header/Dock` | KvDockPanel.ToggleFloat — KvDockPanel.cs:379 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `SetSide(Float ↔ Left)` — открепить в плавающее окно / прикрепить к краю (KvDockPanel.cs:379–394). | ✓ «выделение uGUI: 0→1; отпечаток панелей дока: -285342597→1216293327; панель properties: сторона 1→3; панель properties: высота 126→71; панель settings: ширина 157→194» (отчёт:129) | Отпечаток панелей дока изменился; состояние хранится в `PlayerPrefs` (`KvLayoutStore`), поэтому нажатие меняет раскладку навсегда (отчёт:127–131). |
| `Dock_Свойства/Header/Close` | KvDockPanel.ToRail — KvDockPanel.cs:335 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `InRail = true` — свернуть окно в боковую «полочку»; клик по полочке вернёт (KvDockPanel.cs:335–344). | ✓ «выделение uGUI: 0→1; отпечаток панелей дока: -285342597→2143380522; панель properties: в рейле 0→1; панель properties: ширина 37→3; панель settings: ширина 157→191» (отчёт:131) | Отпечаток панелей дока изменился; состояние хранится в `PlayerPrefs` (`KvLayoutStore`), поэтому нажатие меняет раскладку навсегда (отчёт:127–131). |
| `Dock_Настройки и справка/Header/Collapse` | KvDockPanel.ToggleCollapsed — KvDockPanel.cs:317 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `SetCollapsed(!Collapsed)` — свернуть/развернуть тело панели (KvDockPanel.cs:317–333). | — не в инвентаре (лимит 140) | Панель не проверена: доки `tree`/`settings`/`hotkeys` не попали в прогон вообще. |
| `Dock_Настройки и справка/Header/Dock` | KvDockPanel.ToggleFloat — KvDockPanel.cs:379 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `SetSide(Float ↔ Left)` — открепить в плавающее окно / прикрепить к краю (KvDockPanel.cs:379–394). | — не в инвентаре (лимит 140) | Панель не проверена: доки `tree`/`settings`/`hotkeys` не попали в прогон вообще. |
| `Dock_Настройки и справка/Header/Close` | KvDockPanel.ToRail — KvDockPanel.cs:335 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `InRail = true` — свернуть окно в боковую «полочку»; клик по полочке вернёт (KvDockPanel.cs:335–344). | — не в инвентаре (лимит 140) | Панель не проверена: доки `tree`/`settings`/`hotkeys` не попали в прогон вообще. |
| `Dock_Горячие клавиши/Header/Collapse` | KvDockPanel.ToggleCollapsed — KvDockPanel.cs:317 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `SetCollapsed(!Collapsed)` — свернуть/развернуть тело панели (KvDockPanel.cs:317–333). | — не в инвентаре (лимит 140) | Панель не проверена: доки `tree`/`settings`/`hotkeys` не попали в прогон вообще. |
| `Dock_Горячие клавиши/Header/Dock` | KvDockPanel.ToggleFloat — KvDockPanel.cs:379 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `SetSide(Float ↔ Left)` — открепить в плавающее окно / прикрепить к краю (KvDockPanel.cs:379–394). | — не в инвентаре (лимит 140) | Панель не проверена: доки `tree`/`settings`/`hotkeys` не попали в прогон вообще. |
| `Dock_Горячие клавиши/Header/Close` | KvDockPanel.ToRail — KvDockPanel.cs:335 (кнопка: KvDockPanel.cs:194–199) | — (панель, не команда) | `InRail = true` — свернуть окно в боковую «полочку»; клик по полочке вернёт (KvDockPanel.cs:335–344). | — не в инвентаре (лимит 140) | Панель не проверена: доки `tree`/`settings`/`hotkeys` не попали в прогон вообще. |

---

### 2.5 Корень B — шлюз камеры и экраны сообщений (4 кнопки)


| Элемент | Обработчик | command id | Что делает код | Замер | Дефект / потенциал |
|---|---|---|---|---|---|
| `GatePanel/GateButton` (подписи: «РАЗРЕШИТЬ КАМЕРУ» / «НАСТРОЙКИ ПРИЛОЖЕНИЯ» / «ПРОДОЛЖИТЬ») | KvArCameraGate.OnActionPressed — KvArCameraGate.cs:744 (подключение: KvArCameraGate.cs:724) | — (системный шлюз, не команда проекта) | `awaitingContinue` → `ContinueToAr()` (KvArCameraGate.cs:761); иначе Android: `Permission.RequestUserPermission(Camera)` (KvArCameraGate.cs:425) либо `OpenAppSettings()` — Intent `APPLICATION_DETAILS_SETTINGS` (KvArCameraGate.cs:775–798). Подпись переключается сама: «ПРОДОЛЖИТЬ» после 30 кадров с содержательным изображением (KvArCameraGate.cs:284), «НАСТРОЙКИ ПРИЛОЖЕНИЯ» через 3 с без разрешения (KvArCameraGate.cs:226/769). | — исключено правилом прогона: «системный шлюз (разрешения/предполётная заглушка)» (отчёт:47–55) | Прогон сознательно не нажимает: нажатие уводит приложение в системный диалог и обрывало прогон (KvUiSweepRuntime.cs:2112–2118). Дополнительные пути закрытия: любое касание экрана (KvArCameraGate.cs:196), автопродолжение 15 с (KvArCameraGate.cs:210), снятие окна через 20 с трекинга из `KvArRuntime.CheckCameraGate` (KvArRuntime.cs:367–385). Потенциал: кнопка «ПРОДОЛЖИТЬ» дублируется касанием — нажатие оператором необязательно. |
| `ArMessageButton` на экране ожидания разрешения (Корень A, подписи из `KvArBoot`) | KvArBoot.EnsureCameraPermissionForAr → ShowMessage(…, RequestCameraOrSettings) — KvArBoot.cs:194–198, обработчик KvArBoot.cs:212 | — | `Permission.RequestUserPermission`; после двух отказов — `OpenAndroidAppSettings()` (KvArBoot.cs:216–219). Пока разрешения нет, сообщение перерисовывается каждое обновление; правый ряд AR-кнопок при этом скрыт (`ui.SetButtonsVisible(false)`, KvArBoot.cs:188). | — не в инвентаре (экран живёт до сцены ArMain) | Экран появляется только при ОТСУТСТВИИ разрешения; после выдачи — `HideMessage()` (KvArBoot.cs:208). |
| `ArMessageButton` на экране отказа «AR недоступен» | KvArBoot.ShowUnsupported → ShowMessage(…, LoadVrFallback) — KvArBoot.cs:302, обработчик KvArBoot.cs:309 | — | `SceneManager.LoadScene(KvArMode.VrSceneName)` — переход в VR-сцену; правый ряд AR-кнопок тоже скрыт (`SetButtonsVisible(false)`, KvArBoot.cs:300). | — не в инвентаре (экран отказа в прогоне не встречался) | ⚠ На телефоне без ARCore кнопка ведёт в пустую VR-сцену — оговорка есть только в журнале (KvArBoot.cs:311–313), на экране подпись «Продолжить в VR (для шлема)» без предупреждения. |
| `ArMessageButton` на сообщении «Установить ARCore не удалось» | KvArSessionController.TryInstallArCore → ui.ShowMessage(текст) — KvArSessionController.cs:256 | — | `ARSession.Install()` бросил исключение → показан текст **без подписи кнопки и без действия**. | — не в инвентаре (состояние в прогоне не встречалось) | ⚠ **Ловушка**: `ShowMessage(text)` без `buttonLabel`/`action` скрывает и кнопку действия, и «ОТМЕНА» (`KvArUi.cs:1061–1063`) — закрыть окно нечем (нет тапа вне окна, нет Escape). Закроется, только если код вызовет `HideMessage` (`KvArSessionController.ClearCameraMessage`, KvArSessionController.cs:235). Дефект: сообщению без кнопки нужен таймаут автозакрытия. |

---

## 3. ОХВАТ: ВСЕ 351 СТРОКА ОТЧЁТА И ВСЕ КНОПКИ ИЗ КОДА

### 3.1 Разбор отчёта по блокам (351 строка)

| Строки | Что это | Разобрано где |
|---|---|---|
| 1–11 | шапка: дата, сцена `ArMain`, макет `ar`, пороги (бюджет элемента 2 с, общий предел 240 с), «как читать обрыв», честность прогона | §0 |
| 12–29 | исключения по железу (голос, калибр, сквозн, кисти, вибро, «выйти») — в AR не встречаются | §3.3 |
| 30 | «недоступный элемент — interactable=false» | §4.4 |
| 31–46 | системные пункты Android («настройки android», «app info», «permissions», …) — вне панелей проекта, не нажимаются | §3.3 |
| 47–55 | системные шлюзы `KvArCameraGateCanvas/GatePanel/GateButton` — **наши**, но не нажимаются правилом | §2.5 |
| 56 | `режим: запрошен=не трогали факт=vr · платформа=Ar · сцена=ArMain · макет=ar` | §4.8 |
| 57 | `СТАРТ: элементов=140 · панелей 6 · пропущено: ползунков 0, шаблонов 0, окон 0 · дублей 0 · ярусы: открывающие 41, обычные 86, стартовое меню 0, разрушительные 13` (41+86+13 = 140) | §3.2 |
| 58–293 | первый проход: 117 строк `press=` (105 штатных + 12 на `interactable=false`) и строки `element=` | §2.1, §2.4 |
| 294–339 | второй проход: 22 отложенных элемента — 1 нажат (`ArMessageButton`, отчёт:309–310), 21 не нажат | §4.7 |
| 340–351 | итоги: `проверено 19 · панель не открыта 3`, `ИТОГ: элементов 140 · сработало 99 · без эффекта 0 · исключений 41 · исчезло 0`, `ПОКРЫТИЕ: 140 из найденных 140`, `ВЕРДИКТ: успешно`, `ВНИМАНИЕ: недоступных 12`, три н открывшихся панели, `ВОССТАНОВЛЕНИЕ ПАНЕЛЕЙ: не восстановлено 0`, `не нажимались (запрещено правилом): 1` | §0, §4.4, §4.5, §4.7 |

### 3.2 Все 140 элементов учтены в §2

| Корень | Элементов в инвентаре | Где разобраны |
|---|---|---|
| `ArRuntime/ArUiCanvas` | **44** — 6 меню + 13 тулбар + 10 дерева + 9 вкладок/«Закрыть» + 4 правый ряд + 2 сообщения | §2.1.1–§2.1.6 |
| `KazistovVv_UI/KazistovVvCanvas` | **93** — 6 меню + 1 плашка + 5 ручек групп + 81 кнопка `Cmd_*` | §2.4.1, §2.4.3, §2.4.4 |
| `KazistovVv_UI/KazistovVvDockCanvas` | **3** — `Dock_Свойства/Header/{Collapse,Dock,Close}` | §2.4.7 |
| **Итого** | **140** = 99 сработало + 41 исключение | — |

Из 140: `ok=1` — **107** (99 без исключения + 8 с исключением), `ok=0` — **33**.
Строк `press=` — 118 (106 штатных + 12 «нажал, но кнопка недоступна»), без нажатия — 22
(1 запрет правила + 21 в скрытой панели). Суммы сходятся с отчётом: 99 + 41 = 140;
106 + 12 + 1 + 21 = 140.

### 3.3 Что НЕ вошло в инвентарь (честная граница)

1. **Лимит `MaxElements = 140`** (`KvUiSweepRuntime.cs:2720`, «предел числа проверяемых элементов»).
   Доказательство обрыва — сам состав: сбор дошёл до середины группы «Запись и экспорт» оболочки
   (`Cmd_session.load` — 8-я из 17) и оборвался; группы `net`, `ui`, `misc` и 9 команд `export`
   отсутствуют целиком, а ручка `Group_export`, стоящая **перед** ними, — присутствует.
2. **Элементы, создающиеся после сбора**: 75 пунктов `PcMenuDropdown`, 122 пунктов `KvMenuDropdown`,
   122 строк `Cmd_*` AR-панели + 14 кнопок пагинации, 131 кнопка `KvGroupMenu`, 2 сегмента
   `Workbench`. Правило: `MergeFreshTargets` не добавляет новое при `queue.Total >= MaxElements`
   (`KvUiSweepRuntime.cs:4878`).
3. **Скрытые панели не включаются принудительно**: `MayActivate()` всегда `false`
   (`KvUiSweepRuntime.cs:2165–2168`) — элемент из закрытой панели откладывается и, если панель не
   открылась, получает «не нажимали» (§4.7).
4. **Системные пункты и шлюзы** (отчёт:31–55) — не нажимаются правилом; к кнопкам проекта не относятся.
5. **Стартовое меню шлема** — ярус «стартовое меню 0» (отчёт:57): в AR оно подавлено
   (`KvXrStartMenu.SuppressAutoShow = true`, `KvArRuntime.cs:152`).
6. **Внутренние контролы док-панелей** (`KvTreeView`, `KvPropertiesView`, `KvSettingsView`,
   `KvHotkeyView`, палитра команд Ctrl+P, геймпад-HUD) — общий ПК-код; в этом документе названы
   границей (§6), поштучно не перечислены.

### 3.4 Полный перечень кнопок по коду (660)

| Корень | Статических | Динамических | Всего | Из них замерено |
|---|---|---|---|---|
| A `ArRuntime/ArUiCanvas` | 44 | 75 (меню) + 136 (строки команд + пагинация) = 211 | **255** | 44 |
| B `ArRuntime/KvArCameraGateCanvas` | 1 | 0 | **1** | 0 (правило прогона) |
| C `KazistovVv_UI/KazistovVvCanvas` | 7 + 8 + 122 + 2 = 139 | 122 (меню) + 131 (списки групп) = 253 | **392** | 93 |
| D `KazistovVv_UI/KazistovVvDockCanvas` | 12 | 0 | **12** | 3 |
| **Итого** | **196** | **464** | **660** | **140 (21 %)** |

**ИТОГО элементов (по отчёту самопрогона): 140** — ровно столько записей в `inventory.json` с
`layout=ar` и столько строк `element=` в `_ui_sweep_ar.txt`. **ИТОГО кнопок в деревьях по коду: 660**
(§3.4) — разница и есть непроверенная часть интерфейса.

---

## 4. ОСОБО: ЧТО ПРОСИЛ РАЗОБРАТЬ ВЛАДЕЛЕЦ

### 4.1 «ОТМЕНА» (`ArMessage/ArMessageCancel`)

- **Позиция:** Корень A → `ArMessage` → `ArMessageCancel«ОТМЕНА»` (подпись: `KvArUi.cs:980`).
- **Обработчик:** `KvArUi.OnMessageCancelClicked` — `KvArUi.cs:1124–1128` (подключение `KvArUi.cs:972`).
- **Что делает код:** `HideMessage()` (`KvArUi.cs:1106–1113`) — окно скрывается, `messageAction` и
  `messageCancelAction` обнуляются, то есть **отложенное действие отбрасывается**; в журнал пишется
  «[KvAr] ИНТЕРФЕЙС: действие отменено» (`KvArUi.cs:1127`). В `ShowConfirm` cancel-действие
  присваивается как `delegate { HideMessage(); }` (`KvArUi.cs:1079`) — фактически то же самое.
- **Замер:** **нажатий 0.** `element=…ArMessageCancel«ОТМЕНА» ok=0 effect=не нажимали — панель так и не
  открылась exempt=элемент в скрытой панели — сначала откройте её штатной кнопкой
  (ArRuntime/ArUiCanvas/ArMessage«Очистить рабочее простра…») путь=пропущен` (отчёт:312); повторно
  «панель не открыта» (отчёт:313).
- **Вывод:** утверждение v1 (§6) «„ОТМЕНА" — „без эффекта" по замеру; дефект — не отменяет»
  **этот отчёт не подтверждает**: замера по кнопке нет вообще, а по коду отмена работает.
  Реальные риски остаются тремя:
  1. окно подтверждения открывается только из «Очистить» — других опасных действий с «ОТМЕНА» нет,
     поэтому кнопка проверена быть не могла (нужен прогон с открытым подтверждением);
  2. `messageCancelAction` в обработчике **не вызывается** (присвоено, но игнорируется) — сейчас
     эквивалентно, при расширении `ShowConfirm` станет дефектом;
  3. в обычном `ShowMessage` кнопка отмены скрыта (`KvArUi.cs:1061`), а если подпись/действие не
     заданы — скрыта и основная кнопка (`KvArUi.cs:1062–1063`) → окно нечем закрыть (§2.5,
     сообщение про ARCore).

### 4.2 `record.toggle` («Запись»)

- **Позиция:** A → `PcToolbar/Tool_record.toggle«Запись»` (3-я кнопка AR-тулбара) и
  C → `KvToolbar/Groups/Cmd_record.toggle` (оболочка).
- **Обработчик:** `KvArUi.Execute("record.toggle")` → `KvCommands.Invoke` →
  `FeatureHub.cs:1164` — `hub.ToggleRecord();` (запись действий в файл записи проекта).
- **Замер:** AR-тулбар ✓ — `выделение uGUI: 0→1; команд выполнено: 69→70` (отчёт:289).
  Кнопка оболочки — отложена и **не нажата** (отчёт:334–335, «панель так и не открылась»).
- **Вывод:** v1 §2 «⚠ в AR „без эффекта" по замеру» — **опровергнуто** замером 11.11.
  Что не закрыто: на AR-кнопке нет индикации REC (цвет/иконка не меняются — `CreateButton`
  не перекрашивается), единственное доказательство — счётчик `KvCommands.ExecutedCount`
  (`KvCommands.cs:88/154`) и строка `Logs/actions_*.log`; `record.play` и `record.speed` в AR
  не проверены вовсе.

### 4.3 `screenshot` / `video` / `import` / `session` (и соседи)

| Команда | Где кнопка | Замер | Что делает код | Вердикт по v1 §4 |
|---|---|---|---|---|
| `shot.screenshot` | A: `Tool_shot.screenshot` · C: `Cmd_shot.screenshot` | ✓ AR: «команд выполнено: 10→11» (отчёт:133); C: не нажата (отчёт:314) | `KvStageHub.cs:622` — `hub.TakeScreenshot()` | **v1 неверен**: в AR сработало |
| `session.save` | A: `Tool_session.save` · C: `Cmd_session.save` | ✓ AR: «16→17» (отчёт:145); C: не нажата (отчёт:320) | `FeatureHub.cs:1486` — `hub.Sessions.Save("Сессия <штамп>")` | **v1 неверен**: в AR сработало |
| `file.export` | A: `Tool_file.export` | ✓ AR: «70→71» (отчёт:291) | `FeatureHub.cs:1532` — пишет в лог **пути папок данных** (записи/позы/зоны/сессии/журнал), файла не создаёт | v1 прав по сути («функционала экспорта нет»), неверен по термину: `Stub = true` не стоит ни у одной команды |
| `video.toggle` | только C: `Cmd_video.toggle` | ✗ не нажата (отчёт:316–317) | `KvStageHub.cs:634` — `hub.ToggleVideo()` | «не сработало» **не доказано** — не проверялось |
| `import.tab` | только C | ✗ не нажата (отчёт:318–319) | `KvStageHub3.cs:876` — `hub.OpenTab("import")` | то же |
| `session.load` | только C | ✗ не нажата (отчёт:322–323) | `FeatureHub.cs:1495` — загрузка последней сессии, иначе «сессий нет» | то же |
| `export.robot` | только C | ✗ не нажата (отчёт:338–339) | `KvStageHub3.cs:847` — `hub.ExportSelected()` | то же |
| `import.scan`, `export.lang`, `export.tab`, `log.export`, `record.speed`, `session.panel`, `shot.annotate` | только C, вне инвентаря | — (лимит 140) | `KvStageHub3.cs:885/856/865`, `FeatureHub.cs:1408/1190/1509`, `KvStageHub.cs:647` | не проверены вовсе |

**Итог:** из «screenshot/video/import/session» замер подтверждает **screenshot** и **session.save**
(обе — в AR-тулбаре), остальные не проверены. Формулировку v1 «в AR не сработали по замеру» заменить
на «screenshot/session.save — сработали; video/import/session.load/export.robot — не проверены
из-за лимита 140».

### 4.4 Двенадцать недоступных элементов (`interactable=false`)

`ВНИМАНИЕ: недоступных элементов (interactable=false) 12` (отчёт:345). Состав ровно такой:

| # | Элемент | Причина по коду | Строки отчёта |
|---|---|---|---|
| 1–10 | `Cmd_robot.model.{ur5, kuka_iiwa7, kuka_iiwa14, abb_irb6640, fanuc_m20ib, franka_fr3, franka_fr3_v2, kinova_gen3, comau_edo, openarm}` | `IsEnabled = UnavailableReason(model)=="" && SceneHasTable()` (`KvRobotCommands.cs:121`) — у префаба нет разрешаемой геометрии (`KvRobotCatalog.HasGeometry`, `KvRobotCommands.cs:74`) | 115, 171, 173, 175, 177, 179, 181, 183, 185, 187 |
| 11 | `Cmd_robot.playpause` | `IsEnabled: Flow.Motion != null && Motion.IsRunning` (`KazistovVvUIManager.cs:1236`) — движения нет | 155 |
| 12 | `Cmd_robot.stop` | `IsEnabled: Flow.Motion != null` (`KazistovVvUIManager.cs:1258`) | 157 |

Доступны `robot.model.stand_6axis` и `robot.model.scara_ls10b702s` — нажаты и выполнены
(отчёт:167 «строки дерева 6→7; команд выполнено 22→23», отчёт:169 «23→24»).

⚠ **Рассинхрон замера:** `Cmd_robot.model.franka_fr3_v2` помечен `interactable=false`, но эффект
зафиксирован — `объектов сцены: 1634→1641` (отчёт:183), при этом `путь=не нажимали`. Значит, изменение
сцены пришло из другого источника (параллельная пересборка/очистка) — эффект приписан элементу
ошибочно. Единственный случай «ok=1 при недоступной кнопке».

### 4.5 «Настройки» в AR-тулбаре — ложное правило прогона

`Tool_ui.settings«Настройки»`: `ok=0 effect=не нажимали — правило запрещает (опасно для прогона)
exempt=вне панелей проекта: возможно системный экран — не нажимаем путь=не нажимали` (отчёт:71).
По коду это **кнопка проекта**: `Execute("ui.settings") → ShowSettings(0)`
(`KazistovVvUIManager.cs:1445`) — вкладка панели «Настройки и справка» оболочки, а не экран Android.
Правило сработало по подписи «Настройки»/«settings» (список системных пунктов — отчёт:46).
Следствие: **единственная кнопка AR-тулбара, которую прогон не проверил в принципе**. Итог по
корню A: 34 кнопки из 44 сработали, **10 не нажаты** — 9 из-за скрытых панелей (8 вкладок +
`CloseCommands` + `ArMessageCancel`) и 1 по правилу (`Tool_ui.settings`). Остальные 12
ненажатых элементов отчёта — оболочка (§4.4, §4.7).

### 4.6 `workspace.clear` — пункт меню с подписью «(нет)»

`workspace.clear` **не зарегистрирована** в `KvCommands` (единственные упоминания в коде —
`KvArUi.cs:287/559/799` и тесты `KvArPlacementTests.cs:647/750`). В AR-меню «Правка» пункт строится
как обычный: подпись считается по реестру — `cmd != null ? … : commandId + "  (нет)"`
(`KvArUi.cs:684`), цвет строки — тёмный (`KvArUi.cs:693–696`). То есть на планшете первый пункт
меню «Правка» выглядит **нерабочим** («workspace.clear  (нет)»), хотя нажатие работает: `Execute`
перехватывает id до поиска в реестре (`KvArUi.cs:559`) и открывает подтверждение.
Замер: `Tool_workspace.clear` ✓ (отчёт:287) — окно подтверждения построено; реальное удаление
доказано поздним нажатием `ArMessageButton` (отчёт:310).
**Дефект подписи** — исправлять: либо зарегистрировать команду, либо подписывать caption в AR-меню.

### 4.7 Три панели, которые не открыла ни одна кнопка (21 элемент не нажат)

`ПАНЕЛЬ НЕ ОТКРЫВАЕТСЯ НИ ОДНОЙ КНОПКОЙ (элементы внутри не проверены):`
`ArRuntime/ArUiCanvas/PcCommandPanel«Команды · Робот (21)»`,
`ArRuntime/ArUiCanvas/ArMessage«Очистить рабочее простра…»`,
`KazistovVv_UI/KazistovVvCanvas«Файл»` (отчёт:346–349).

| Панель | Не нажатые элементы | Почему по коду/прогону |
|---|---|---|
| `PcCommandPanel` (8) | `GroupTab_path/post/view/export/net/ui/misc`, `CloseCommands` | Панель собрана заранее (`SetActive(false)`, `KvArUi.cs:485`), вкладки попали в инвентарь как «в скрытой панели»; `MayActivate=false` (`KvUiSweepRuntime.cs:2165`) — принудительно не включается, а штатная кнопка «Команды» на втором проходе не нажималась (контекст прогона панель закрыл: `Restore` возвращает состояние после каждого элемента, `KvUiSweepRuntime.cs:4511`). Единственная проверенная вкладка — `GroupTab_robot` (отчёт:94–95), открытая нажатием `Tree_Все команды` (отчёт:92–93) |
| `ArMessage` (1) | `ArMessageCancel«ОТМЕНА»` | Окно подтверждения закрылось после нажатия `ArMessageButton` (отчёт:310) и на втором проходе не открывалось — §4.1 |
| `KazistovVvCanvas` (12) | `Cmd_shot.screenshot, Cmd_video.toggle, Cmd_import.tab, Cmd_session.save, Cmd_session.load, Cmd_edit.reset, Cmd_waypoint.delete, Cmd_tool.measure.reset, Group_export, Cmd_record.toggle, Cmd_record.play, Cmd_export.robot` | «гейт» элемента — **корень канваса** (`KazistovVv_UI/KazistovVvCanvas«Файл»`), то есть канвас был неактивен. Вероятная причина — команда `ui.toggle` «Показать/скрыть интерфейс» (`KazistovVvUIManager.cs:1529`), нажатая в отчёт:283 (`команд выполнено 68→69`): `Restore` в промене возвращает док-панели, режим, язык и флаги `KvSettings`, но **не `uiVisible`** (`KvUiSweepRuntime.cs:4511–4577`). Гипотеза — проверить отдельным прогоном |

Все 21 — `ok=0 effect=не нажимали … путь=пропущен` (отчёт:295–338). Это **не дефект кнопок**, а
граница прогона.

### 4.8 Прочие находки по замеру

- `режим: запрошен=не трогали факт=vr · платформа=Ar` (отчёт:56): прогон шёл с режимом **VR** внутри
  AR-сцены (настройку не трогали). Для `ArMain` корректно (сцена определяет макет `ar`,
  `KvUiSweepLayout.IsArScene`, `KvUiSweepRuntime.cs:2681`), но строка сбивает с толку при разборе.
- `панелей 6` (отчёт:57) — док-панели оболочки; восстановились все (`ВОССТАНОВЛЕНИЕ ПАНЕЛЕЙ:
  не восстановлено 0`, отчёт:350).
- `не нажимались (запрещено правилом): 1` (отчёт:351) — ровно `Tool_ui.settings` (§4.5).
- `команд выполнено: N→N+1` — это `KvCommands.ExecutedCount` (`KvCommands.cs:88`); `элементы оболочки`,
  `строки дерева`, `объектов сцены`, `панели`, `отпечаток панелей дока` — метрики самого прогона.
  **Ни одного «без эффекта»** (отчёт:342).

---

## 5. СВЕРКА С v1 (`КОНСПЕКТ_КНОПОК_таблица.md`): ПРОПУЩЕННОЕ И ОШИБОЧНОЕ

### 5.1 Пропущено в v1 (нет ни одной строки)

1. **AR-тулбар: «Очистить» (`workspace.clear`), «Скриншот» (`shot.screenshot`), «Сохранить»
   (`session.save`), «Демо» (`demo.quick`)** — в v1 §2 перечислено 9 кнопок из 13.
2. **10 строк собственного дерева AR** (`Tree_*`: две модели, два стола, `group:path`, `group:post`,
   `view.limits`, `calib.tab`, `features.open`, `group:robot`) — v1 §3 описывает дерево **оболочки**
   («+/−», узлы, стрелки), а не эти кнопки.
3. **8 вкладок `GroupTab_*` и кнопка `CloseCommands«Закрыть»`** панели команд AR (v1 §4 — про группы
   оболочки, не про вкладки AR).
4. **75 пунктов AR-меню** (`MenuItem_*`) и **122 пункта меню оболочки** (`Item_*`).
5. **14 кнопок пагинации** `CmdPrev/CmdNext` AR-панели команд.
6. **Шлюз `KvArCameraGateCanvas/GateButton`** — v1 не упоминает вообще (прогон его сознательно
   исключает, отчёт:47–55).
7. **Экраны сообщений `KvArBoot`** («РАЗРЕШИТЬ КАМЕРУ»/«ОТКРЫТЬ НАСТРОЙКИ», «Продолжить в VR») и
   `KvArSessionController` («Установить ARCore…»).
8. **«Верстак» `Seg0/Seg1`**, выпадающие списки групп `GM_*` и `KvGroupMenuBlocker`.
9. **Кнопки шапок четырёх док-панелей** (v1 §3 знает Collapse/Dock/Close, но не привязывает их к
   4 панелям × 3 кнопки и не фиксирует, что в AR проверена только `Dock_Свойства`).
10. **Элементы, которые прогон не видит из-за лимита 140** (§3.3) — v1 этот лимит не называет.

### 5.2 Ошибочное в v1

| № | Где в v1 | Что написано | Что на самом деле (доказательство) |
|---|---|---|---|
| 1 | §2, «Запись» | «⚠ в AR „без эффекта" по замеру» | ✓ `команд выполнено: 69→70` (отчёт:289) — **сработало** |
| 2 | §2, «Экспорт» | «⚠ заглушка в реестре (перерегистрируется)» | `Stub = true` не стоит **ни у одной** команды (поиск по проекту находит только комментарии и тесты: `KazistovVvUIManager.cs:1568`, `FeatureHub.cs:1552`); команда выполняется (отчёт:291). Суть верна иначе: `file.export` только пишет пути папок данных (`FeatureHub.cs:1532`) — экспорта нет, но это не «заглушка в реестре» |
| 3 | §4, «Прочее» | «screenshot/video/import/session — в AR не сработали по замеру» | `shot.screenshot` ✓ (отчёт:133), `session.save` ✓ (отчёт:145), `file.export` ✓ (отчёт:291); `video.toggle`, `import.tab`, `session.load`, `export.robot` — **не проверялись** (отчёт:314–339) |
| 4 | §4, «Сеть/автоматизация» | «⚠ в AR не проверены (скрытая панель)» | Причина другая: группы `net`, `ui` и `misc` **отсутствуют в инвентаре из-за лимита 140** — их ручек в замере нет вообще (§3.3), это не «скрытая панель» |
| 5 | §4, «robot.model.*» | «⚠⚠ 10 из 12 без геометрии (храповик „доступно = 2")» | Совпадает: 10 моделей `interactable=false` (§4.4). **Не сказано**, что ещё 2 из 12 недоступных элементов — `robot.playpause`/`robot.stop` (условие «идёт движение»), и что `franka_fr3_v2` дал эффект при `interactable=false` (отчёт:183) |
| 6 | §6, «ОТМЕНА» | «„без эффекта" по замеру; дефект — не отменяет» | **Замера нет**: `ok=0 … путь=пропущен` (отчёт:312–313). По коду `OnMessageCancelClicked → HideMessage` (`KvArUi.cs:1124`) окно закрывает и действие отбрасывает. Формулировку заменить на «не проверена прогона (нужен прогон с открытым подтверждением)» |
| 7 | §6, «KvMenuBlocker» | «законно „ничего не меняет"» | Прогон засчитал `ok=1` («выделение uGUI: 0→1») + исключение «меню не открыто — нажимать нечего» (отчёт:111). Это не «ничего не меняет», а **честное исключение** по геттеру `AnyOpen` (`KvMenuBar.cs:39`) |
| 8 | §1, «Файл» | «Раздел „Файл" сейчас пустой по сути (сессии есть в подменю)» | В AR 12 пунктов, в оболочке 15 (по `MenuPath`) — раздел не пустой |
| 9 | §3, «+/− (шапка дерева)» | «Добавить/убрать узел (замер: есть)» | В AR-раскладке **своего** дерева «+/−» нет: это док-панель оболочки `Dock_Дерево моделей`, в прогон не попавшая; AR-дерево — 10 кнопок «▸» (§2.1.3) |
| 10 | §6, «ArButton Команды» | «Показывает панель команд (замер: есть)» | Замер фиксирует **только подсветку** `выделение uGUI: 0→1` (отчёт:97) — открытие панели метрикой не измерялось; косвенное доказательство — открытая панель у вкладки «Робот» (отчёт:94) |

### 5.3 Подтверждено v1 замером (оставить как есть)

`Поставить`/`Переставить` — честный отказ «нечего ставить: найденных плоскостей 0» (отчёт:147, 149);
`Сброс вида` — есть (отчёт:293); `ArMessageButton` — есть (отчёт:310); `robot.playpause`/`robot.stop`
недоступны (отчёт:155, 157); 10 моделей недоступны (отчёт:171–187); команды `post.*`, `wp.*`,
`view.*`, `waypoint.*`, `zone.*`, `tool.*`, `constr.*`, `health/obstacle` оболочки выполняются
(отчёт:212–283); док-кнопки `Collapse/Dock/Close` меняют состояние панели (отчёт:127–131).

---

## 6. ГРАНИЦЫ РАЗБОРА И ПОТЕНЦИАЛ

**Границы (честно):**

1. Разобраны **кнопки** и их обработчики. Внутренние контролы док-панелей (`KvTreeView`: узлы/«+»/«−»/
   галки видимости; `KvPropertiesView`; `KvSettingsView`: тумблеры «Функции», слайдеры, сегменты
   темы/языка/масштаба/строк тулбара; `KvHotkeyView`; палитра команд; геймпад-HUD) — общий ПК-код,
   включается в AR теми же командами (`view.panel.*`, `ui.settings`); названы границей, поштучно не
   перечислены.
2. Панель шлема `KvXrGroupPanel` (веер 110 команд) в AR **скрыта и недостижима**: `SetArVisible(false)`
   (`KvArRuntime.cs:295`), и ни одна кнопка AR-интерфейса её не показывает — комментарий
   «открывается кнопкой „Команды"» (`KvArRuntime.cs:295`) **не соответствует коду**: «Команды» открывает
   собственную `PcCommandPanel` (`KvArRuntime.cs:858–861`). Иконок панели в инвентаре нет.
3. Стартовое меню шлема в AR подавлено (`KvArRuntime.cs:152`), ярус «стартовое меню 0» (отчёт:57).
4. Замер относится к сборке 11.11.2026; более поздние правки кода в нём не отражены.

**Потенциал (приоритеты с привязкой к коду):**

1. Поднять `KvUiSweepRuntime.MaxElements` (сейчас 140) — иначе 464 из 660 кнопок не проверяются
   никогда (`KvUiSweepRuntime.cs:2720`).
2. Прогнать «ОТМЕНА» и вкладки `PcCommandPanel` при открытом окне/панели (§4.1, §4.7).
3. Подпись `workspace.clear  (нет)` в AR-меню (§4.6) и правило прогона для `ui.settings` (§4.5).
4. Позицию пагинации `CmdPrev/CmdNext` считать от `rowPitch`, а не константой (§2.3).
5. Индикация состояния на AR-кнопках (REC, тумблеры, открытое меню/панель) — сейчас состояние видно
   только по подсветке нажатия.
6. `record.*`, `video.toggle`, `export.*` — реальная запись и экспорт занятия (v1 §8 п. 2, 4),
   с учётом уточнений §4.2/§4.3.
7. Геометрия 10 моделей (`robot.model.*`, §4.4) — главный долг каталога (v1 §8 п. 1).

---

*Документ составлен только чтением кода и отчёта самопрогона; ничего в проекте не изменялось.*
