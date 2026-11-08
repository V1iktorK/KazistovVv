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

            // ПОДСВЕТКА ДОПУСТИМОСТИ (закрыто 30.10.2026, аудит S43).
            //
            // ПОЧЕМУ ОТДЕЛЬНЫЙ МЕТОД. Раньше покраска жила прямо здесь и молча пропускалась, если
            // `previewRenderer` пуст: пустой префаб, модель без мешей, выключенные визуалы. Тогда
            // призрак ставился без цвета, и оператор не понимал, можно ли ставить робота в это
            // место. Теперь число покрашенных рендереров видно, а при нуле пишется причина.
            int painted = PaintPreview(previewRenderers, valid);
            if (painted == 0 && Time.frameCount % 120 == 0)
            {
                Debug.LogWarning("[CenterWindow] превью не подсвечено: рендереров для покраски нет");
            }
        }

        /// <summary>
        /// ВСЕ РЕНДЕРЕРЫ ПРЕВЬЮ (30.10.2026). У модели робота их много, у стола — один. Пустой
        /// массив — законное состояние (пустой префаб), и обрабатывается без исключения.
        /// </summary>
        private Renderer[] previewRenderers = new Renderer[0];

        /// <summary>
        /// ЦВЕТА — КОНТРАКТ: зелёный «можно», красный «нельзя», оба полупрозрачные (0,45).
        /// Их читает и тест, и глаз оператора; менять значения без теста нельзя.
        /// </summary>
        public static Color PreviewColor(bool valid)
        {
            return valid ? new Color(0.3f, 1f, 0.4f, 0.45f) : new Color(1f, 0.3f, 0.3f, 0.45f);
        }

        /// <summary>
        /// ПОКРАСИТЬ ПРЕВЬЮ В ЦВЕТ ДОПУСТИМОСТИ. Вынесено статическим методом — чтобы поведение
        /// проверялось тестом без сцены и без GameObject. Возвращает число покрашенных рендереров
        /// (0 — красить нечего, и это не ошибка вызывающего).
        ///
        /// ЦВЕТА — КОНТРАКТ: зелёный «можно», красный «нельзя». Их читает и тест, и глаз оператора.
        ///
        /// ЦВЕТ ПИШЕТСЯ В `MaterialPropertyBlock`, А НЕ В КОПИЮ МАТЕРИАЛА (P2-7 / R1-2, 11.11.2026).
        ///
        /// ЧТО БЫЛО. `Material m = r.material;` — на КАЖДЫЙ рендерер превью создавалась личная
        /// копия материала, и копии никто не уничтожал (`DestroyPreview` уничтожал только
        /// GameObject). В редакторе на каждую копию печаталось предупреждение Unity
        /// «Instantiating material due to calling renderer.material». Превью пересоздаётся при
        /// каждом движении курсора размещения, то есть копии копились на каждое наведение.
        ///
        /// ЧТО СТАЛО. `MaterialPropertyBlock` — значения на рендерер, без копий и без предупреждений.
        /// Оригинал материала модели по-прежнему не меняется: блок свойств живёт рядом с рендерером.
        /// </summary>
        public static int PaintPreview(Renderer[] renderers, bool valid)
        {
            if (renderers == null) return 0;
            Color c = PreviewColor(valid);
            int painted = 0;
            MaterialPropertyBlock block = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (block == null) block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                // Оба имени: `_BaseColor` у HDRP/URP, `_Color` у встроенного конвейера.
                // Неизвестное шейдеру свойство блок просто игнорирует — падения не будет.
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                r.SetPropertyBlock(block);
                painted++;
            }
            return painted;
        }

        /// <summary>
        /// ЕДИНСТВЕННЫЙ МАТЕРИАЛ ПРЕВЬЮ, СОЗДАННЫЙ НАМИ (уничтожается в `DestroyPreview`).
        ///
        /// Раньше материал создавался В ЦИКЛЕ по рендерерам и присваивался через `r.material` —
        /// то есть на каждый рендерер появлялась ещё и копия только что созданного материала.
        /// Теперь материал один, присваивается как общий (`sharedMaterial`) и учитывается здесь.
        /// </summary>
        private Material previewMaterial;

        /// <summary>Цвет «призрака» пишется блоком свойств — копий материала не создаём (P2-7).</summary>
        private static void TintGhost(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0) return;
            MaterialPropertyBlock block = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material source = r.sharedMaterial;
                if (source == null || !source.HasProperty("_BaseColor")) continue;
                if (block == null) block = new MaterialPropertyBlock();
                Color c = source.GetColor("_BaseColor");
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(
                    Mathf.Min(1f, c.r * 0.75f),
                    Mathf.Min(1f, c.g * 1.2f),
                    Mathf.Min(1f, c.b * 0.8f),
                    c.a));
                r.SetPropertyBlock(block);
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
                // Фантом из реальной модели: свой зеленоватый «призрачный» тон — блоком свойств,
                // без копий материала на каждый рендерер (P2-7 / R1-2).
                TintGhost(preview.GetComponentsInChildren<Renderer>());
            }
            else
            {
                // ОДИН материал на всё превью (раньше — материал и его копия на КАЖДЫЙ рендерер).
                if (previewMaterial == null)
                {
                    previewMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLine(
                        "Материал_превью", Color.white);
                }
                foreach (Renderer r in preview.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || previewMaterial == null) continue;
                    r.sharedMaterial = previewMaterial;
                }
            }

            // ВСЕ рендереры собираются ОДИН раз, после того как превью собрано (30.10.2026):
            // у фантома робота их десятки, и красить только первый — значит показывать
            // наполовину зелёный призрак.
            CollectPreviewRenderers();
        }

        public void DestroyPreview()
        {
            if (preview != null) Destroy(preview);
            preview = null;
            previewRenderer = null;
            previewRenderers = new Renderer[0];
            // Материал превью создан нами — GameObject его не уничтожит (P2-7 / R1-2).
            if (previewMaterial != null) Destroy(previewMaterial);
            previewMaterial = null;
        }

        /// <summary>
        /// СОБРАТЬ РЕНДЕРЕРЫ ПРЕВЬЮ (30.10.2026). Вызывается один раз после сборки призрака.
        ///
        /// ПОЧЕМУ ОТДЕЛЬНЫЙ МЕТОД. `GetComponentInChildren<Renderer>()` возвращает ПЕРВЫЙ рендерер
        /// или null. Для покраски «можно ставить / нельзя ставить» нужны все: у модели робота их
        /// десятки, и первый попавшийся красил бы только одну часть призрака.
        /// `GetComponentsInChildren(..., includeInactive: true)` — намеренно: часть узлов модели
        /// бывает выключена, но видимые рендереры остаются.
        /// </summary>
        private void CollectPreviewRenderers()
        {
            if (preview == null)
            {
                previewRenderers = new Renderer[0];
                return;
            }
            previewRenderers = preview.GetComponentsInChildren<Renderer>(true);
            if (previewRenderers == null) previewRenderers = new Renderer[0];
            previewRenderer = previewRenderers.Length > 0 ? previewRenderers[0] : null;

            if (previewRenderer == null)
            {
                Debug.LogWarning("[CenterWindow] в превью «" + preview.name +
                                 "» нет ни одного рендерера — подсветка допустимости ставить не покажется");
            }
        }

        /// <summary>
        /// Копия модели для предпросмотра размещения не должна переживать окно/режим:
        /// иначе она остаётся в сцене отдельным объектом («фантомная» копия робота).
        /// </summary>
        private void OnDisable() { DestroyPreview(); }
        private void OnDestroy() { DestroyPreview(); }
    }
}
