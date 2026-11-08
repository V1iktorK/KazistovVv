using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// КАЛИБРОВКА «ВИРТУАЛЬНЫЙ РОБОТ = РЕАЛЬНЫЙ» ПО ТРЁМ ТОЧКАМ (создано ранее, переработано 11.11.2026).
///
/// ЧТО БЫЛО (дефект P1-6, замерено тестировщиком: записей ключей `Cal*` — 7, ЧТЕНИЙ — 0).
/// Инструмент аккуратно сохранял смещение в `PlayerPrefs` и НИКОГДА его не восстанавливал.
/// Смысл калибровки — «после перезапуска виртуальный робот совпадает с реальным», а совпадения не
/// было: приложение стартовало с позы сцены, и наладчик каждый раз калибровал заново, считая, что
/// «сохранение не работает». Хуже того, поворот накладывался на ТЕКУЩИЙ
/// (`rotation = rotOffset * rotation`), поэтому вторая калибровка подряд складывала перекос.
///
/// ЧТО СТАЛО:
///   • смещение восстанавливается в `Start` (`LoadCalibration`);
///   • поворот хранится один раз как абсолютная поза базы (не накапливается);
///   • маркеры калибровки уничтожаются (раньше `Instantiate` терялся — объекты копились в сцене);
///   • обращения к `calibrationUI` проверяются на null;
///   • есть `ClearCalibration()` — «вернуть как было» без правки реестра Windows/Android.
/// </summary>
public class CalibrationTool : MonoBehaviour
{
    [Header("Calibration")]
    public Transform virtualRobotBase;
    public GameObject calibrationPointPrefab; // Красная сфера
    public GameObject calibrationUI;

    private Vector3[] realPoints = new Vector3[3];
    private Vector3[] modelPoints = new Vector3[3];
    private int currentPoint = 0;
    private bool isCalibrating = false;

    /// <summary>Созданные маркеры калибровки: раньше результат `Instantiate` отбрасывался.</summary>
    private readonly List<GameObject> markers = new List<GameObject>();

    /// <summary>КЛЮЧИ ХРАНЕНИЯ — в одном месте, чтобы «сохранили» и «прочитали» не разъехались.</summary>
    public const string KeyX = "CalX", KeyY = "CalY", KeyZ = "CalZ";
    public const string KeyQx = "CalQx", KeyQy = "CalQy", KeyQz = "CalQz", KeyQw = "CalQw";

    /// <summary>Есть ли сохранённая калибровка (по ключу позиции).</summary>
    public static bool HasSavedCalibration()
    {
        return PlayerPrefs.HasKey(KeyX);
    }

    /// <summary>
    /// ПРИМЕНИТЬ СОХРАНЁННУЮ КАЛИБРОВКУ К БАЗЕ РОБОТА. Позиция и поворот задаются АБСОЛЮТНО —
    /// повторный вызов даёт то же состояние, а не складывает перекос (дефект P1-6).
    /// </summary>
    public void LoadCalibration()
    {
        if (virtualRobotBase == null) return;
        if (!HasSavedCalibration())
        {
            Debug.Log("[Calibration] сохранённой калибровки нет — база робота остаётся в позе сцены");
            return;
        }

        Vector3 position = new Vector3(
            PlayerPrefs.GetFloat(KeyX, 0f),
            PlayerPrefs.GetFloat(KeyY, 0f),
            PlayerPrefs.GetFloat(KeyZ, 0f));
        Quaternion rotation = new Quaternion(
            PlayerPrefs.GetFloat(KeyQx, 0f),
            PlayerPrefs.GetFloat(KeyQy, 0f),
            PlayerPrefs.GetFloat(KeyQz, 0f),
            PlayerPrefs.GetFloat(KeyQw, 1f));

        virtualRobotBase.position = position;
        virtualRobotBase.rotation = rotation;
        Debug.Log("[Calibration] калибровка восстановлена · база " +
                  position.ToString("0.000") + " · поворот " + rotation.eulerAngles.ToString("0.0"));
    }

    /// <summary>УБРАТЬ СОХРАНЁННУЮ КАЛИБРОВКУ (вернуть базу в позу сцены).</summary>
    public void ClearCalibration()
    {
        PlayerPrefs.DeleteKey(KeyX); PlayerPrefs.DeleteKey(KeyY); PlayerPrefs.DeleteKey(KeyZ);
        PlayerPrefs.DeleteKey(KeyQx); PlayerPrefs.DeleteKey(KeyQy);
        PlayerPrefs.DeleteKey(KeyQz); PlayerPrefs.DeleteKey(KeyQw);
        PlayerPrefs.Save();
        ClearMarkers();
        Debug.Log("[Calibration] сохранённая калибровка удалена");
    }

    void Start()
    {
        // P1-6: калибровка обязана восстанавливаться САМА — иначе сохранение бессмысленно.
        LoadCalibration();
    }

    public void StartCalibration()
    {
        isCalibrating = true;
        currentPoint = 0;
        ClearMarkers();
        if (calibrationUI != null) calibrationUI.SetActive(true);   // P1-6: без null — падало в сцене без UI
        Debug.Log("[Calibration] Наведите луч на 3 точки реального робота и нажмите Select");
    }

    void Update()
    {
        if (!isCalibrating) return;

        var input = InputManager.Instance;
        if (input == null) return;

        // Визуализация луча
        Debug.DrawRay(input.PointerPosition, input.PointerDirection * 3f, Color.yellow);

        if (input.SelectDown)
        {
            Ray ray = new Ray(input.PointerPosition, input.PointerDirection);
            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                SetPoint(hit.point);
            }
        }
    }

    void SetPoint(Vector3 realWorldPos)
    {
        if (currentPoint >= realPoints.Length) return;

        realPoints[currentPoint] = realWorldPos;

        // Соответствующая точка на модели (заранее заданные)
        modelPoints[currentPoint] = GetModelCalibrationPoint(currentPoint);

        // Визуальный маркер — и он же СОХРАНЯЕТСЯ, чтобы его можно было убрать (P1-6).
        if (calibrationPointPrefab != null)
        {
            GameObject marker = Instantiate(calibrationPointPrefab, realWorldPos, Quaternion.identity);
            if (marker != null) markers.Add(marker);
        }

        currentPoint++;
        Debug.Log("[Calibration] Точка " + currentPoint + "/3");

        if (currentPoint >= 3)
        {
            ApplyCalibration();
        }
    }

    Vector3 GetModelCalibrationPoint(int index)
    {
        // Три ключевые точки на модели робота
        switch (index)
        {
            case 0: return Vector3.zero;                    // Основание
            case 1: return new Vector3(0, 0.4f, 0);         // Сустав 2
            case 2: return new Vector3(0.3f, 0.8f, 0);      // Фланец (пример)
            default: return Vector3.zero;
        }
    }

    /// <summary>
    /// СМЕЩЕНИЕ ПО ТРЁМ ТОЧКАМ: позиция базы + поворот, переводящий направление модели в реальное.
    /// Вынесено в чистую статическую функцию — так правило «поворот один раз, а не накоплением»
    /// проверяется тестом без сцены (P1-6).
    /// </summary>
    public static void ComputeOffsets(Vector3[] real, Vector3[] model,
        out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (real == null || model == null || real.Length < 2 || model.Length < 2) return;

        Vector3 realDir = real[1] - real[0];
        Vector3 modelDir = model[1] - model[0];
        if (realDir.sqrMagnitude < 1e-8f || modelDir.sqrMagnitude < 1e-8f) return;

        rotation = Quaternion.FromToRotation(modelDir.normalized, realDir.normalized);
        position = real[0] - rotation * model[0];
    }

    void ApplyCalibration()
    {
        if (virtualRobotBase == null)
        {
            Debug.LogWarning("[Calibration] не задана база виртуального робота — калибровать нечего");
            isCalibrating = false;
            if (calibrationUI != null) calibrationUI.SetActive(false);
            return;
        }

        Vector3 posOffset;
        Quaternion rotOffset;
        ComputeOffsets(realPoints, modelPoints, out posOffset, out rotOffset);

        // АБСОЛЮТНАЯ поза, а не «наложить на текущую»: вторая калибровка не должна складывать перекос.
        virtualRobotBase.SetPositionAndRotation(posOffset, rotOffset);

        PlayerPrefs.SetFloat(KeyX, posOffset.x);
        PlayerPrefs.SetFloat(KeyY, posOffset.y);
        PlayerPrefs.SetFloat(KeyZ, posOffset.z);
        PlayerPrefs.SetFloat(KeyQx, rotOffset.x);
        PlayerPrefs.SetFloat(KeyQy, rotOffset.y);
        PlayerPrefs.SetFloat(KeyQz, rotOffset.z);
        PlayerPrefs.SetFloat(KeyQw, rotOffset.w);
        PlayerPrefs.Save();

        isCalibrating = false;
        ClearMarkers();
        if (calibrationUI != null) calibrationUI.SetActive(false);
        Debug.Log("[Calibration] Готово! Виртуальный робот совмещён с реальным (сохранено на диск).");
    }

    /// <summary>УБРАТЬ МАРКЕРЫ КАЛИБРОВКИ (раньше объекты копились в сцене навсегда).</summary>
    private void ClearMarkers()
    {
        for (int i = 0; i < markers.Count; i++)
            if (markers[i] != null) Destroy(markers[i]);
        markers.Clear();
    }
}
