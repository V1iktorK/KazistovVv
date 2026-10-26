using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// Центральное окно (главная сцена): подсказки режимов и предпросмотр размещения.
    /// Для робота поддерживается выбор направления в горизонтали:
    ///   ←/→ или A/D (мышь/клавиатура), левый стик X (геймпад) — вращение;
    ///   при приближении к углу, кратному 90°, направление «примагничивается».
    /// </summary>
    public class CenterWindow : MonoBehaviour
    {
        private Text hintText;
        private GameObject preview;
        private Renderer previewRenderer;
        private SpawnKind activeKind = SpawnKind.None;
        private RectTransform chooserRoot;
        private System.Action<int> onRobotTypeChosen;
        private RobotController phantomTemplate; // реальная модель-«фантом» для выбора направления

        public SpawnKind ActiveKind => activeKind;

        // Выборщик типа робота открыт?
        public bool RobotChooserOpen => chooserRoot != null && chooserRoot.gameObject.activeSelf;

        // Режим робота: угол поворота в горизонтали
        public float RobotYaw { get; private set; }

        private float yawSnapThreshold = 6f; // градусов, в пределах которых «прилипаем» к 90°

        public void Build(RectTransform parent)
        {
            hintText = KvTheme.CreateText(parent, "CenterHint", "",
                KvTheme.FontSizeSmall, TextAnchor.MiddleCenter, KvTheme.TextDim);
            RectTransform hr = hintText.rectTransform;
            hr.anchorMin = new Vector2(0.5f, 1f);
            hr.anchorMax = new Vector2(0.5f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.anchoredPosition = new Vector2(0f, -52f);
            hr.sizeDelta = new Vector2(900f, 24f);

            BuildRobotChooser(parent);
        }

        /// <summary>Панель выбора типа робота (появляется по «Добавить робота»).</summary>
        private void BuildRobotChooser(RectTransform parent)
        {
            Image panel = KvTheme.CreatePanel(parent, "RobotChooser", KvTheme.PanelHeader);
            chooserRoot = panel.rectTransform;
            chooserRoot.anchorMin = new Vector2(0.5f, 0.5f);
            chooserRoot.anchorMax = new Vector2(0.5f, 0.5f);
            chooserRoot.pivot = new Vector2(0.5f, 0.5f);
            chooserRoot.sizeDelta = new Vector2(480f, 190f);
            chooserRoot.anchoredPosition = new Vector2(0f, 0f);
            chooserRoot.gameObject.SetActive(false);

            Text title = KvTheme.CreateText(chooserRoot, "Title",
                "Выберите робота для размещения", KvTheme.FontSize + 2,
                TextAnchor.MiddleCenter, KvTheme.TextMain);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(0f, -40f);
            title.rectTransform.offsetMax = new Vector2(0f, -8f);

            Button six = KvTheme.CreateButton(chooserRoot, "Btn_SixAxis", "6-осевой  (1)",
                () => onRobotTypeChosen?.Invoke(0), 34);
            RectTransform sr = (RectTransform)six.transform;
            sr.anchorMin = new Vector2(0.5f, 1f);
            sr.anchorMax = new Vector2(0.5f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.sizeDelta = new Vector2(220f, 34f);
            sr.anchoredPosition = new Vector2(-118f, -56f);

            Button scara = KvTheme.CreateButton(chooserRoot, "Btn_Scara", "SCARA  (2)",
                () => onRobotTypeChosen?.Invoke(1), 34);
            RectTransform cr2 = (RectTransform)scara.transform;
            cr2.anchorMin = new Vector2(0.5f, 1f);
            cr2.anchorMax = new Vector2(0.5f, 1f);
            cr2.pivot = new Vector2(0.5f, 1f);
            cr2.sizeDelta = new Vector2(220f, 34f);
            cr2.anchoredPosition = new Vector2(118f, -56f);

            Text sub = KvTheme.CreateText(chooserRoot, "Sub", "Esc — отмена",
                KvTheme.FontSizeSmall, TextAnchor.MiddleCenter, KvTheme.TextDim);
            sub.rectTransform.anchorMin = new Vector2(0f, 0f);
            sub.rectTransform.anchorMax = new Vector2(1f, 0f);
            sub.rectTransform.pivot = new Vector2(0.5f, 0f);
            sub.rectTransform.offsetMin = new Vector2(0f, 10f);
            sub.rectTransform.offsetMax = new Vector2(0f, 30f);
        }

        public void ShowRobotChooser(System.Action<int> onPick)
        {
            onRobotTypeChosen = onPick;
            if (chooserRoot != null) chooserRoot.gameObject.SetActive(true);
        }

        public void HideRobotChooser()
        {
            onRobotTypeChosen = null;
            if (chooserRoot != null) chooserRoot.gameObject.SetActive(false);
        }

        public void SetHint(string text)
        {
            if (hintText != null) hintText.text = text;
        }

        public void StartPlacement(SpawnKind kind)
        {
            activeKind = kind;
            if (kind == SpawnKind.None)
            {
                HideRobotChooser();
                phantomTemplate = null;
                SetHint("");
                DestroyPreview();
                return;
            }

            RobotYaw = 0f;
            HideRobotChooser();
            SetHint(kind == SpawnKind.Table
                ? "Стол: наведите на пол/поверхность (низ стола приклеится), ЛКМ/Enter — поставить. Esc — отмена."
                : "Робот: наведите на СТОЛ, ←/→ или A/D — направление (магнит к 90°), ЛКМ/Enter — поставить в центр стола. Esc — отмена.");

            CreatePreview(kind);
        }

        /// <summary>Вращение робота в горизонтали (вызывается UIManager'ом из ввода).</summary>
        public void RotateRobot(float deltaDegrees)
        {
            if (activeKind != SpawnKind.Robot) return;
            RobotYaw = (RobotYaw + deltaDegrees) % 360f;
            if (RobotYaw < 0f) RobotYaw += 360f;
            ApplySnap();
        }

        /// <summary>Задать направление напрямую (уже кратно 90°).</summary>
        public void SetRobotYaw(float degrees)
        {
            if (activeKind != SpawnKind.Robot) return;
            RobotYaw = degrees % 360f;
            if (RobotYaw < 0f) RobotYaw += 360f;
        }

        /// <summary>Реальная модель-«фантом» для предпросмотра (вместо стилизованной).</summary>
        public void SetPhantomTemplate(RobotController template)
        {
            phantomTemplate = template;
        }

        private void ApplySnap()
        {
            // Магнит: если угол близок к кратному 90 — прилипаем.
            float nearest = Mathf.Round(RobotYaw / 90f) * 90f;
            if (Mathf.Abs(RobotYaw - nearest) <= yawSnapThreshold)
            {
                RobotYaw = nearest;
            }
        }

        public void UpdatePreview(Vector3 position, bool valid)
        {
            if (preview == null) return;
            // Стол «низом» к поверхности (центр приподнят на половину высоты).
            Vector3 pos = position;
            if (activeKind == SpawnKind.Table && preview.transform.localScale.y > 0.001f)
                pos += Vector3.up * (preview.transform.localScale.y * 0.5f);
            preview.transform.position = pos;
            // Направление робота — вращение вокруг вертикали
            Vector3 euler = preview.transform.eulerAngles;
            euler.y = RobotYaw;
            preview.transform.eulerAngles = euler;

            if (previewRenderer != null)
            {
                Color c = valid
                    ? new Color(0.3f, 1f, 0.4f, 0.45f)
                    : new Color(1f, 0.3f, 0.3f, 0.45f);
                previewRenderer.material.color = c;
            }
        }

        private void CreatePreview(SpawnKind kind)
        {
            DestroyPreview();

            if (kind == SpawnKind.Table)
            {
                preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                preview.name = "Preview_Table";
                preview.transform.localScale = new Vector3(1.2f, 0.05f, 0.8f);
            }
            else if (kind == SpawnKind.Robot)
            {
                if (phantomTemplate != null)
                {
                    // «Фантом» = копия настоящей модели (выбор направления по реальным габаритам).
                    GameObject ghost = Object.Instantiate(phantomTemplate.gameObject);
                    ghost.name = "Phantom_Robot";
                    preview = ghost;

                    // Гасим всю «живую» логику копии и коллайдеры.
                    foreach (MonoBehaviour mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null) continue;
                        mb.enabled = false;
                    }
                    foreach (Collider c in ghost.GetComponentsInChildren<Collider>(true))
                    {
                        Object.Destroy(c);
                    }
                }
                else
                {
                    // Тело робота
                    preview = new GameObject("Preview_Robot");
                    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    body.transform.SetParent(preview.transform, false);
                    body.transform.localPosition = Vector3.up * 0.4f;
                    body.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                    // «Нос» — направление робота
                    GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    nose.transform.SetParent(preview.transform, false);
                    nose.transform.localPosition = new Vector3(0f, 0.4f, 0.75f);
                    nose.transform.localScale = new Vector3(0.18f, 0.18f, 0.6f);
                }
            }

            if (preview == null) return;
            if (kind == SpawnKind.Robot && phantomTemplate != null)
            {
                // Фантом из реальной модели: свои материалы, но с зелёным «призрачным» тоном.
                foreach (Renderer r in preview.GetComponentsInChildren<Renderer>())
                {
                    Material m = r.material;
                    if (m != null && m.HasProperty("_BaseColor"))
                    {
                        Color c = m.GetColor("_BaseColor");
                        m.SetColor("_BaseColor", new Color(
                            Mathf.Min(1f, c.r * 0.75f),
                            Mathf.Min(1f, c.g * 1.2f),
                            Mathf.Min(1f, c.b * 0.8f),
                            c.a));
                    }
                }
                previewRenderer = preview.GetComponentInChildren<Renderer>();
            }
            else
            {
                foreach (Renderer r in preview.GetComponentsInChildren<Renderer>())
                {
                    Material previewMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLine(
                        "Материал_превью", Color.white);
                    if (previewMaterial != null) r.material = previewMaterial;
                }
                previewRenderer = preview.GetComponentInChildren<Renderer>();
            }
        }

        public void DestroyPreview()
        {
            if (preview != null) Destroy(preview);
            preview = null;
            previewRenderer = null;
        }

        /// <summary>
        /// Копия модели для предпросмотра размещения не должна переживать окно/режим:
        /// иначе она остаётся в сцене отдельным объектом («фантомная» копия робота).
        /// </summary>
        private void OnDisable() { DestroyPreview(); }
        private void OnDestroy() { DestroyPreview(); }
    }
}
