using System;
using System.Collections.Generic;
using KazistovVvFeatures;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    // ================================================================================================
    //  ПАНЕЛИ РАСКЛАДКИ BLENDER + их малая логика (очередь, избранное, тосты, телеметрия).
    //
    //  ПАНЕЛИ ПРИВЯЗАНЫ К РЕАЛЬНЫМ ДАННЫМ И КОМАНДАМ:
    //    • Тема — `KvTheme.SetMode` (переключатель реально меняет тему);
    //    • Зоны и коллизии — команды `zone.*` / `obstacle.*` через `KvCommands.Invoke`;
    //    • Горячие клавиши — `KvHotkeyView` (существующая панель);
    //    • Жесты — команды пульта/геймпада/обучения через `KvCommands.Invoke`;
    //    • Команды Ctrl+K — `KvCommandPalette` (существующая палитра);
    //    • Суставы HUD — телеметрия `RobotController.GetJointAngles()` активного робота;
    //    • Тосты — механизм Report (`KvUiStates.Reported` + `KvBlenderToasts`);
    //    • Очередь задач — `KvTaskQueue` (шаги добавляются командами/сценариями);
    //    • Миникарта — позиция робота/камеры в плоскости стола;
    //    • Камера-гизмо — ориентация камеры (рыскание) тройкой осей;
    //    • Таймлайн — шаги сценария `FeatureHub.Current.Scenarios`;
    //    • Избранное — закреплённые команды (`KvFavoritesStore`);
    //    • Консоль — журнал `KvActionLog` с подсветкой по важности.
    // ================================================================================================

    /// <summary>ОЧЕРЕДЬ ЗАДАЧ: шаги, поставленные в очередь (сценарии, ручные задачи).</summary>
    public static class KvTaskQueue
    {
        /// <summary>Предел очереди (старые задачи вытесняются).</summary>
        public const int MaxItems = 50;

        private static readonly List<string> items = new List<string>();

        public static IReadOnlyList<string> Items
        {
            get { return items; }
        }

        public static int Count
        {
            get { return items.Count; }
        }

        public static void Add(string task)
        {
            if (string.IsNullOrEmpty(task)) return;
            items.Add(task);
            while (items.Count > MaxItems) items.RemoveAt(0);
        }

        /// <summary>Взять первую задачу (null — очередь пуста).</summary>
        public static string Take()
        {
            if (items.Count == 0) return null;
            string task = items[0];
            items.RemoveAt(0);
            return task;
        }

        public static void Remove(string task)
        {
            items.Remove(task);
        }

        public static void Clear()
        {
            items.Clear();
        }
    }

    /// <summary>
    /// ИЗБРАННОЕ: закреплённые команды оператора. Список переживает перезапуск
    /// (PlayerPrefs, CSV id через запятую).
    /// </summary>
    public static class KvFavoritesStore
    {
        public const string PrefsKey = "KazistovVv.Blender.Favorites";

        private static readonly List<string> items = new List<string>();

        public static IReadOnlyList<string> Items
        {
            get
            {
                EnsureLoaded();
                return items;
            }
        }

        public static bool Contains(string id)
        {
            EnsureLoaded();
            return items.Contains(id ?? "");
        }

        public static void Add(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            EnsureLoaded();
            if (!items.Contains(id)) items.Add(id);
            Save();
        }

        public static void Remove(string id)
        {
            EnsureLoaded();
            items.Remove(id);
            Save();
        }

        public static void Clear()
        {
            EnsureLoaded();
            items.Clear();
            Save();
        }

        private static bool loaded;

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            items.Clear();
            string csv = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(csv)) return;
            string[] parts = csv.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string id = parts[i].Trim();
                if (id.Length > 0 && !items.Contains(id)) items.Add(id);
            }
        }

        private static void Save()
        {
            PlayerPrefs.SetString(PrefsKey, string.Join(",", items.ToArray()));
            PlayerPrefs.Save();
        }

        /// <summary>Сбросить кэш (тесты: состояние читается заново).</summary>
        public static void ResetCache()
        {
            loaded = false;
        }
    }

    /// <summary>
    /// ТОСТЫ раскладки Blender. Привязаны к существующему механизму Report:
    /// `KvUiStates.ReportError` (плашка ошибок) публикует `Reported`, и тост показывается
    /// и для явных сообщений (`Show`), и для отчётов об ошибках.
    /// </summary>
    public static class KvBlenderToasts
    {
        /// <summary>Сколько тостов хранит история.</summary>
        public const int MaxHistory = 20;

        private static readonly List<string> history = new List<string>();

        /// <summary>История тостов (новые — в начале).</summary>
        public static IReadOnlyList<string> History
        {
            get { return history; }
        }

        /// <summary>Показан новый тост (панель/слой подхватывают).</summary>
        public static event Action<string> Shown;

        /// <summary>Показать тост (в историю + событие).</summary>
        public static void Show(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            history.Insert(0, text);
            while (history.Count > MaxHistory) history.RemoveAt(history.Count - 1);
            if (Shown != null) Shown(text);
        }

        /// <summary>Отчёт (механизм Report): сообщение об ошибке/событии как тост.</summary>
        public static void Report(string text)
        {
            Show(text);
        }

        public static void Clear()
        {
            history.Clear();
        }
    }

    /// <summary>
    /// ТЕЛЕМЕТРИЯ СУСТАВОВ: углы активного робота (`RobotController.GetJointAngles()`).
    /// Без активного робота честно отвечает «нет данных» — панель показывает это строкой.
    /// </summary>
    public static class KvJointsSource
    {
        /// <summary>Активный робот (null — в сцене нет).</summary>
        public static RobotController FindActiveRobot()
        {
            RobotController[] all = UnityEngine.Object.FindObjectsOfType<RobotController>();
            RobotController fallback = null;
            for (int i = 0; i < all.Length; i++)
            {
                RobotController r = all[i];
                if (r == null) continue;
                if (r.isActive) return r;
                if (fallback == null) fallback = r;
            }
            return fallback;
        }

        /// <summary>Углы суставов активного робота (false — нет робота/нет данных).</summary>
        public static bool TryGetAngles(List<float> into)
        {
            if (into == null) return false;
            into.Clear();
            RobotController robot = FindActiveRobot();
            if (robot == null) return false;
            float[] angles;
            try { angles = robot.GetJointAngles(); }
            catch { return false; }
            if (angles == null || angles.Length == 0) return false;
            for (int i = 0; i < angles.Length; i++) into.Add(angles[i]);
            return true;
        }
    }

    /// <summary>
    /// СТРОИТЕЛЬ ПАНЕЛЕЙ раскладки Blender. Каждая панель — стеклянное окно
    /// (<see cref="KvGlassWindow"/>) с содержимым, привязанным к реальным командам/данным.
    /// </summary>
    public static class KvBlenderPanels
    {
        /// <summary>Высота строки-кнопки панели (px).</summary>
        public const float RowHeight = 24f;

        /// <summary>Команды панели «Зоны и коллизии».</summary>
        public static readonly string[] ZoneCommandIds =
        {
            "zone.box", "zone.sphere", "zone.cylinder", "zone.discard",
            "obstacle.toggle", "obstacle.discard", "obstacle.evaluate", "zone.panel"
        };

        /// <summary>Команды панели «Жесты» (пульт, геймпад, обучение).</summary>
        public static readonly string[] GestureCommandIds =
        {
            "gamepad.hud", "pendant.toggle", "pendant.variant", "pendant.attach",
            "pendant.jogmode", "tut.toggle"
        };

        // ------------------------------------------------------------------ строка-кнопка

        /// <summary>
        /// Строка-кнопка: нажатие вызывает `KvCommands.Invoke(id)` — реальную команду реестра.
        /// Подпись — локализованное название команды.
        /// </summary>
        public static Button AddCommandRow(RectTransform content, string commandId, float y)
        {
            KvCommand command = KvCommands.Get(commandId);
            string label = command != null ? command.LocalizedTitle : commandId;

            GameObject go = new GameObject("Row_" + commandId, typeof(Image), typeof(Button));
            go.transform.SetParent(content, false);
            RectTransform rt = (RectTransform)go.transform;
            PlaceRow(rt, y);

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvGlassStyle.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = KvTheme.ButtonBg;
            bg.raycastTarget = true;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.ButtonBg);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.ButtonBg);
            cb.disabledColor = new Color(1f, 1f, 1f, 0.55f);
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            btn.interactable = command == null || command.Enabled;

            Text text = KvTheme.CreateText(rt, "Label", label, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, btn.interactable ? KvTheme.TextMain : KvTheme.TextDisabled);
            KvTheme.Stretch(text.rectTransform, 8f, 8f, 2f, 2f);

            if (command != null)
            {
                KvTooltipTarget tip = go.AddComponent<KvTooltipTarget>();
                tip.Set(command.LocalizedTitle, command.TooltipBody, command.Hotkey);
                tip.stub = command.Stub;
            }

            string captured = commandId;
            btn.onClick.AddListener(delegate { KvCommands.Invoke(captured); });
            return btn;
        }

        /// <summary>Подпись-строка (не кнопка).</summary>
        public static Text AddLabel(RectTransform content, string text, float y, Color color)
        {
            Text t = KvTheme.CreateText(content, "Label", text, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, color);
            RectTransform rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-12f, 18f);
            rt.anchoredPosition = new Vector2(7f, -y);
            return t;
        }

        /// <summary>Кнопка с произвольным действием (не команда реестра).</summary>
        public static Button AddTextButton(RectTransform content, string label, float y,
            UnityEngine.Events.UnityAction action)
        {
            Button btn = KvTheme.CreateButton(content, "Btn_" + label, label, null, 20, true);
            RectTransform rt = (RectTransform)btn.transform;
            PlaceRow(rt, y);
            if (action != null) btn.onClick.AddListener(action);
            return btn;
        }

        private static void PlaceRow(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(5f, 0f);
            rt.offsetMax = new Vector2(-5f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, RowHeight);
            rt.anchoredPosition = new Vector2(5f, -y);
        }

        // ------------------------------------------------------------------ Тема

        /// <summary>Панель «ТЕМА»: переключатель тёмная/светлая реально меняет тему.</summary>
        public static void BuildTheme(KvGlassWindow window)
        {
            RectTransform c = window.Content;
            Text status = AddLabel(c, ThemeStatusText(), 4f, KvTheme.TextDim);

            AddTextButton(c, KvLoc.T("blender.theme.dark", "Тёмная"), 26f, delegate
            {
                KvTheme.SetMode(KvThemeMode.Dark);
                status.text = ThemeStatusText();
            });
            AddTextButton(c, KvLoc.T("blender.theme.light", "Светлая"), 50f, delegate
            {
                KvTheme.SetMode(KvThemeMode.Light);
                status.text = ThemeStatusText();
            });
            AddCommandRow(c, "view.theme", 76f);
        }

        private static string ThemeStatusText()
        {
            return KvLoc.T("blender.theme.current", "Текущая тема: ") + KvTheme.PaletteLabel;
        }

        // ------------------------------------------------------------------ Зоны и коллизии

        /// <summary>Панель «ЗОНЫ И КОЛЛИЗИИ»: команды зон/препятствий.</summary>
        public static void BuildZones(KvGlassWindow window)
        {
            RectTransform c = window.Content;
            float y = 4f;
            for (int i = 0; i < ZoneCommandIds.Length; i++)
            {
                AddCommandRow(c, ZoneCommandIds[i], y);
                y += RowHeight + 2f;
            }
        }

        // ------------------------------------------------------------------ Горячие клавиши

        /// <summary>Панель «ГОРЯЧИЕ КЛАВИШИ»: существующая `KvHotkeyView` с поиском.</summary>
        public static void BuildHotkeys(KvGlassWindow window)
        {
            KvHotkeyView view = window.Content.gameObject.AddComponent<KvHotkeyView>();
            view.Build(window.Content);
        }

        // ------------------------------------------------------------------ Жесты

        /// <summary>Панель «ЖЕСТЫ»: команды пульта/геймпада/обучения.</summary>
        public static void BuildGestures(KvGlassWindow window)
        {
            RectTransform c = window.Content;
            float y = 4f;
            for (int i = 0; i < GestureCommandIds.Length; i++)
            {
                AddCommandRow(c, GestureCommandIds[i], y);
                y += RowHeight + 2f;
            }
        }

        // ------------------------------------------------------------------ Команды (Ctrl+K)

        /// <summary>Панель «КОМАНДЫ»: кнопка открывает палитру `KvCommandPalette` (Ctrl+K).</summary>
        public static void BuildCommands(KvGlassWindow window, KvCommandPalette palette)
        {
            RectTransform c = window.Content;
            AddLabel(c, KvLoc.T("blender.commands.hint", "Палитра команд: Ctrl+K"), 4f, KvTheme.TextDim);
            AddTextButton(c, KvLoc.T("blender.commands.open", "Открыть палитру команд"), 26f, delegate
            {
                if (palette != null) palette.Toggle();
            });
            AddTextButton(c, KvLoc.T("blender.commands.close", "Закрыть палитру"), 50f, delegate
            {
                if (palette != null) palette.Close();
            });
        }

        // ------------------------------------------------------------------ Суставы HUD

        /// <summary>Панель «СУСТАВЫ HUD»: углы суставов активного робота (обновляется перезапуском).</summary>
        public static void BuildJoints(KvGlassWindow window, List<Text> rows)
        {
            RectTransform c = window.Content;
            Text state = AddLabel(c, "", 4f, KvTheme.TextDim);
            rows.Add(state);
            float y = 24f;
            for (int i = 0; i < 6; i++)
            {
                Text row = AddLabel(c, "J" + (i + 1) + ": —", y, KvTheme.TextMain);
                rows.Add(row);
                y += 19f;
            }
            RefreshJoints(rows);
        }

        /// <summary>Обновить строки телеметрии суставов (вызывается таймером оболочки/тестами).</summary>
        public static void RefreshJoints(List<Text> rows)
        {
            if (rows == null || rows.Count == 0) return;
            List<float> angles = new List<float>();
            bool ok = KvJointsSource.TryGetAngles(angles);
            rows[0].text = ok
                ? KvLoc.T("blender.joints.live", "телеметрия активна")
                : KvLoc.T("blender.joints.nodata", "нет данных: робот не выбран");
            for (int i = 1; i < rows.Count; i++)
            {
                int joint = i - 1;
                rows[i].text = ok && joint < angles.Count
                    ? "J" + (joint + 1) + ": " + angles[joint].ToString("0.0") + "°"
                    : "J" + (joint + 1) + ": —";
            }
        }

        // ------------------------------------------------------------------ Тосты

        /// <summary>Панель «ТОСТЫ»: история сообщений механизма Report.</summary>
        public static void BuildToasts(KvGlassWindow window, List<Text> rows)
        {
            RectTransform c = window.Content;
            Text state = AddLabel(c, "", 4f, KvTheme.TextDim);
            rows.Add(state);
            for (int i = 0; i < 6; i++)
            {
                Text row = AddLabel(c, "", 24f + i * 19f, KvTheme.TextMain);
                rows.Add(row);
            }
            RefreshToasts(rows);
        }

        /// <summary>Обновить строки тостов.</summary>
        public static void RefreshToasts(List<Text> rows)
        {
            if (rows == null || rows.Count == 0) return;
            rows[0].text = KvLoc.T("blender.toasts.title", "Последние сообщения: ") +
                           KvBlenderToasts.History.Count;
            for (int i = 1; i < rows.Count; i++)
            {
                int index = i - 1;
                rows[i].text = index < KvBlenderToasts.History.Count
                    ? KvBlenderToasts.History[index]
                    : "";
            }
        }

        // ------------------------------------------------------------------ Очередь задач

        /// <summary>Панель «ОЧЕРЕДЬ ЗАДАЧ»: шаги очереди + управление.</summary>
        public static void BuildQueue(KvGlassWindow window, List<Text> rows)
        {
            RectTransform c = window.Content;
            Text state = AddLabel(c, "", 4f, KvTheme.TextDim);
            rows.Add(state);
            for (int i = 0; i < 5; i++)
            {
                Text row = AddLabel(c, "", 24f + i * 19f, KvTheme.TextMain);
                rows.Add(row);
            }
            AddTextButton(c, KvLoc.T("blender.queue.take", "Взять задачу из очереди"), 122f, delegate
            {
                KvTaskQueue.Take();
                RefreshQueue(rows);
            });
            AddTextButton(c, KvLoc.T("blender.queue.clear", "Очистить очередь"), 146f, delegate
            {
                KvTaskQueue.Clear();
                RefreshQueue(rows);
            });
            RefreshQueue(rows);
        }

        /// <summary>Обновить строки очереди.</summary>
        public static void RefreshQueue(List<Text> rows)
        {
            if (rows == null || rows.Count == 0) return;
            rows[0].text = KvLoc.T("blender.queue.count", "Задач в очереди: ") + KvTaskQueue.Count;
            for (int i = 1; i < rows.Count; i++)
            {
                int index = i - 1;
                rows[i].text = index < KvTaskQueue.Items.Count ? KvTaskQueue.Items[index] : "";
            }
        }

        // ------------------------------------------------------------------ Миникарта

        /// <summary>Панель «МИНИКАРТА»: плоскость стола, робот и камера.</summary>
        public static void BuildMinimap(KvGlassWindow window, out Text state,
            out RectTransform robotMarker, out RectTransform cameraMarker)
        {
            RectTransform c = window.Content;

            Image map = KvGlassStyle.CreateGlassRect(c, "Map", KvGlassRole.Border);
            RectTransform mr = map.rectTransform;
            mr.anchorMin = new Vector2(0f, 1f);
            mr.anchorMax = new Vector2(1f, 1f);
            mr.pivot = new Vector2(0f, 1f);
            mr.offsetMin = new Vector2(6f, 0f);
            mr.offsetMax = new Vector2(-6f, 0f);
            mr.sizeDelta = new Vector2(mr.sizeDelta.x, 110f);
            mr.anchoredPosition = new Vector2(6f, -4f);

            robotMarker = CreateMarker(mr, "RobotMarker", KvTheme.Ok);
            cameraMarker = CreateMarker(mr, "CameraMarker", KvTheme.Accent);
            state = AddLabel(c, "", 120f, KvTheme.TextDim);
            RefreshMinimap(state, robotMarker, cameraMarker);
        }

        private static RectTransform CreateMarker(RectTransform parent, string name, Color color)
        {
            Image img = KvTheme.CreatePanel(parent, name, color);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(10f, 10f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>Обновить миникарту по позиции робота/камеры.</summary>
        public static void RefreshMinimap(Text state, RectTransform robotMarker,
            RectTransform cameraMarker)
        {
            RobotController robot = KvJointsSource.FindActiveRobot();
            Camera cam = Camera.main;
            if (state != null)
            {
                state.text = KvLoc.T("blender.minimap.robot", "Робот: ") +
                             (robot != null ? robot.robotName : "—") + " · " +
                             KvLoc.T("blender.minimap.camera", "Камера: ") +
                             (cam != null ? cam.name : "—");
            }
            if (robotMarker != null && robot != null)
                robotMarker.anchoredPosition = new Vector2(
                    Mathf.Clamp(robot.transform.position.x * 20f, -50f, 50f),
                    Mathf.Clamp(robot.transform.position.z * 20f, -50f, 50f));
            if (cameraMarker != null && cam != null)
                cameraMarker.anchoredPosition = new Vector2(
                    Mathf.Clamp(cam.transform.position.x * 20f, -50f, 50f),
                    Mathf.Clamp(cam.transform.position.z * 20f, -50f, 50f));
        }

        // ------------------------------------------------------------------ Камера-гизмо

        /// <summary>Панель «КАМЕРА-ГИЗМО»: тройка осей по ориентации камеры.</summary>
        public static void BuildCameraGizmo(KvGlassWindow window, out Text state,
            out RectTransform triad)
        {
            RectTransform c = window.Content;
            Image area = KvGlassStyle.CreateGlassRect(c, "TriadArea", KvGlassRole.Border);
            triad = area.rectTransform;
            triad.anchorMin = new Vector2(0f, 1f);
            triad.anchorMax = new Vector2(1f, 1f);
            triad.pivot = new Vector2(0f, 1f);
            triad.offsetMin = new Vector2(6f, 0f);
            triad.offsetMax = new Vector2(-6f, 0f);
            triad.sizeDelta = new Vector2(triad.sizeDelta.x, 100f);
            triad.anchoredPosition = new Vector2(6f, -4f);

            CreateAxis(triad, "AxisX", new Color(0.92f, 0.32f, 0.30f), 0f);
            CreateAxis(triad, "AxisY", new Color(0.36f, 0.80f, 0.40f), 90f);
            CreateAxis(triad, "AxisZ", new Color(0.36f, 0.62f, 0.95f), 45f);

            state = AddLabel(c, "", 110f, KvTheme.TextDim);
            RefreshCameraGizmo(state, triad);
        }

        private static void CreateAxis(RectTransform parent, string name, Color color, float zAngle)
        {
            Image img = KvTheme.CreatePanel(parent, name, color);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(42f, 3f);
            rt.anchoredPosition = Vector2.zero;
            rt.localEulerAngles = new Vector3(0f, 0f, zAngle);
        }

        /// <summary>Обновить гизмо по ориентации камеры.</summary>
        public static void RefreshCameraGizmo(Text state, RectTransform triad)
        {
            Camera cam = Camera.main;
            if (state != null)
            {
                state.text = cam != null
                    ? KvLoc.T("blender.camera.yaw", "Рыскание: ") +
                      cam.transform.eulerAngles.y.ToString("0") + "°"
                    : KvLoc.T("blender.camera.nodata", "нет данных: камера не найдена");
            }
            if (triad != null && cam != null)
                triad.localEulerAngles = new Vector3(0f, 0f, -cam.transform.eulerAngles.y);
        }

        // ------------------------------------------------------------------ Таймлайн

        /// <summary>Панель «ТАЙМЛАЙН»: шаги текущего сценария (`FeatureHub.Current.Scenarios`).</summary>
        public static void BuildTimeline(KvGlassWindow window, List<Text> rows)
        {
            RectTransform c = window.Content;
            Text state = AddLabel(c, "", 4f, KvTheme.TextDim);
            rows.Add(state);
            for (int i = 0; i < 6; i++)
            {
                Text row = AddLabel(c, "", 24f + i * 19f, KvTheme.TextMain);
                rows.Add(row);
            }
            AddCommandRow(c, "scenarios.open", 142f);
            AddCommandRow(c, "scenarios.run", 168f);
            RefreshTimeline(rows);
        }

        /// <summary>Обновить строки таймлайна по активному сценарию.</summary>
        public static void RefreshTimeline(List<Text> rows)
        {
            if (rows == null || rows.Count == 0) return;
            KazistovVvFeatures.FeatureHub hub = KazistovVvFeatures.FeatureHub.Current;
            KvScenarioManager manager = hub != null ? hub.Scenarios : null;
            if (manager == null)
            {
                rows[0].text = KvLoc.T("blender.timeline.nodata", "нет данных: сценарий не запущен");
                for (int i = 1; i < rows.Count; i++) rows[i].text = "";
                return;
            }
            rows[0].text = KvLoc.T("blender.timeline.running", "Сценарий: ") +
                           (manager.Running ? manager.RunningTitle : "—") +
                           " · " + manager.StepIndex + "/" + manager.StepCount;
            if (manager.Running && manager.StepCount > 0)
            {
                for (int i = 1; i < rows.Count; i++)
                {
                    int index = i - 1;
                    rows[i].text = index == manager.StepIndex
                        ? "▶ " + manager.CurrentStepText
                        : (index < manager.StepCount ? index + 1 + ". " + StepTextAt(manager, index) : "");
                }
            }
            else
            {
                for (int i = 1; i < rows.Count; i++)
                {
                    int index = i - 1;
                    rows[i].text = index < manager.All.Count
                        ? manager.All[index].Title
                        : "";
                }
            }
        }

        private static string StepTextAt(KvScenarioManager manager, int index)
        {
            // `KvScenarioManager` не отдаёт шаги по индексу напрямую — читаем из сценария.
            for (int s = 0; s < manager.All.Count; s++)
            {
                KvScenario scenario = manager.All[s];
                if (scenario != null && scenario.Title == manager.RunningTitle &&
                    index < scenario.Steps.Count)
                    return scenario.Steps[index].Text;
            }
            return "";
        }

        // ------------------------------------------------------------------ Избранное

        /// <summary>Панель «ИЗБРАННОЕ»: закреплённые команды + закрепление по id.</summary>
        public static void BuildFavorites(KvGlassWindow window, List<Button> rowButtons,
            out InputField pinField, out Text stateText)
        {
            RectTransform c = window.Content;
            Text state = AddLabel(c, "", 4f, KvTheme.TextDim);
            stateText = state;

            GameObject inputGo = new GameObject("PinField", typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(c, false);
            RectTransform ir = (RectTransform)inputGo.transform;
            PlaceRow(ir, 24f);
            Image inputBg = inputGo.GetComponent<Image>();
            inputBg.sprite = KvGlassStyle.Rounded;
            inputBg.type = Image.Type.Sliced;
            inputBg.color = KvTheme.InputBg;
            inputBg.raycastTarget = true;

            Text placeholder = KvTheme.CreateText(ir, "Placeholder",
                KvLoc.T("blender.favorites.pin", "id команды для закрепления…"),
                KvTheme.FontSizeSmall, TextAnchor.MiddleLeft, KvTheme.TextDisabled);
            KvTheme.Stretch(placeholder.rectTransform, 6f, 6f, 2f, 2f);
            Text fieldText = KvTheme.CreateText(ir, "Text", "", KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            KvTheme.Stretch(fieldText.rectTransform, 6f, 6f, 2f, 2f);
            pinField = inputGo.GetComponent<InputField>();
            pinField.targetGraphic = inputBg;
            pinField.textComponent = fieldText;
            pinField.placeholder = placeholder;

            // Локальная копия: `out`-параметр нельзя захватывать в лямбду (CS1628).
            InputField field = pinField;
            AddTextButton(c, KvLoc.T("blender.favorites.add", "Закрепить"), 52f, delegate
            {
                if (field != null) KvFavoritesStore.Add(field.text);
                RefreshFavorites(rowButtons, state);
            });

            for (int i = 0; i < 5; i++)
            {
                Button row = AddCommandRow(c, "robot.playpause", 78f + i * (RowHeight + 2f));
                rowButtons.Add(row);
            }
            RefreshFavorites(rowButtons, state);
        }

        /// <summary>Обновить строки избранного.</summary>
        public static void RefreshFavorites(List<Button> rowButtons, Text state)
        {
            if (rowButtons == null) return;
            IReadOnlyList<string> items = KvFavoritesStore.Items;
            if (state != null)
                state.text = KvLoc.T("blender.favorites.count", "Закреплено: ") + items.Count;
            for (int i = 0; i < rowButtons.Count; i++)
            {
                Button row = rowButtons[i];
                if (row == null) continue;
                if (i < items.Count)
                {
                    row.gameObject.SetActive(true);
                    KvCommand command = KvCommands.Get(items[i]);
                    Text label = row.GetComponentInChildren<Text>();
                    if (label != null)
                        label.text = command != null ? command.LocalizedTitle : items[i];
                    string captured = items[i];
                    row.onClick.RemoveAllListeners();
                    row.onClick.AddListener(delegate { KvCommands.Invoke(captured); });
                }
                else
                {
                    row.gameObject.SetActive(false);
                }
            }
        }

        // ------------------------------------------------------------------ Консоль

        /// <summary>Панель «КОНСОЛЬ»: журнал `KvActionLog` с подсветкой важности.</summary>
        public static void BuildConsole(KvGlassWindow window, List<Text> rows)
        {
            RectTransform c = window.Content;
            Text state = AddLabel(c, "", 4f, KvTheme.TextDim);
            rows.Add(state);
            for (int i = 0; i < 8; i++)
            {
                Text row = AddLabel(c, "", 24f + i * 18f, KvTheme.TextDim);
                rows.Add(row);
            }
            RefreshConsole(rows);
        }

        /// <summary>Обновить строки консоли (подсветка: ошибка — красным, предупреждение — жёлтым).</summary>
        public static void RefreshConsole(List<Text> rows)
        {
            if (rows == null || rows.Count == 0) return;
            KazistovVvFeatures.KvActionLog log = KazistovVvFeatures.KvActionLog.Instance;
            IReadOnlyList<KazistovVvFeatures.KvLogEntry> entries =
                log != null ? log.Entries : null;
            int total = entries != null ? entries.Count : 0;
            rows[0].text = KvLoc.T("blender.console.count", "Записей журнала: ") + total;
            for (int i = 1; i < rows.Count; i++)
            {
                int index = total - i;   // новые сверху
                if (index < 0 || index >= total)
                {
                    rows[i].text = "";
                    continue;
                }
                KazistovVvFeatures.KvLogEntry e = entries[index];
                rows[i].text = e != null ? e.Line : "";
                rows[i].color = e == null ? KvTheme.TextDim
                    : e.kind == KazistovVvFeatures.KvLogKind.Error ? KvTheme.Error
                    : e.kind == KazistovVvFeatures.KvLogKind.Warning ? KvTheme.Warn
                    : KvTheme.TextDim;
            }
        }
    }
}
