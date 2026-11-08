using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
/// <summary>
/// УСТАРЕЛО (S44, 30.10.2026). Мёртвый код: используется только сам собой (TrajectoryPlayer). Запись делает KvRecordingService.
/// Оставлено помеченным, а не удалено: правило проекта — сначала метка с причиной, удаление отдельным решением.
/// </summary>
[System.Obsolete("Мёртвый код: используется только сам собой (TrajectoryPlayer). Запись делает KvRecordingService.")]
public class TrajectoryPoint
{
    public float time;
    public Vector3 position;
    public Quaternion rotation;
    public float[] jointAngles;
}

public class TrajectoryRecorder : MonoBehaviour
{
    public RobotController robot;
    private List<TrajectoryPoint> trajectory = new List<TrajectoryPoint>();
    private bool isRecording = false;
    private float startTime;
    private float lastPointTime = -1f;

    // ------------------------------------------------- ПРЕДОХРАНИТЕЛИ ЗАПИСИ (P2-9, 11.11.2026)
    //
    // ЧТО БЫЛО. Список точек пополнялся КАЖДЫЙ кадр и ничем не ограничивался: `new TrajectoryPoint`
    // плюс `new float[]` из `GetJointAngles()` на кадр, без предела и без прореживания. Запись
    // в файл (`File.WriteAllText`) шла без `try/catch` и без создания каталога.
    //
    // ЧЕМ ЭТО ГРОЗИЛО. Длительная запись (а оператор пишет движение минутами) съедала память
    // шлема мусором и в конце падала на записи файла: несуществующий каталог из настроек или
    // занятый файл — и исключение из `Update`/`StopRecording` без сохранения. Траектория
    // терялась целиком, хотя «Запись окончена. Точек: N» уже было напечатано.
    //
    // ТЕПЕРЬ: предел точек, прореживание по времени (меньше мусора на кадр), запись файла в
    // `try/catch` с созданием каталога и честным сообщением об отказе.
    /// <summary>Предел числа точек в одной записи: ~10 минут на 20 Гц.</summary>
    public const int MaxPoints = 12000;

    /// <summary>Минимальный интервал между точками, с (50 Гц — быстрее ручного движения не нужно).</summary>
    public const float MinPointIntervalSeconds = 0.02f;

    /// <summary>Сколько точек записано сейчас (для журнала и проверки предела).</summary>
    public int PointCount { get { return trajectory.Count; } }

    void Update()
    {
        if (InputManager.Instance == null)
        {
            return;
        }

        if (InputManager.Instance.GrabDown)
        {
            if (!isRecording) StartRecording();
            else StopRecording();
        }

        if (isRecording && robot != null && robot.tcp != null)
        {
            // ПРЕДЕЛ И ПРОРЕЖИВАНИЕ (P2-9): без них список рос без границы, а `GetJointAngles()`
            // создавал новый массив на каждый кадр.
            if (trajectory.Count >= MaxPoints)
            {
                Debug.LogWarning("[TrajectoryRecorder] достигнут предел " + MaxPoints +
                                 " точек — запись остановлена, дальше точки не добавляются");
                StopRecording();
                return;
            }
            if (lastPointTime >= 0f && Time.time - lastPointTime < MinPointIntervalSeconds) return;
            lastPointTime = Time.time;

            trajectory.Add(new TrajectoryPoint
            {
                time = Time.time - startTime,
                position = robot.tcp.position,
                rotation = robot.tcp.rotation,
                jointAngles = robot.GetJointAngles()
            });
        }
    }

    public void StartRecording()
    {
        trajectory.Clear();
        isRecording = true;
        startTime = Time.time;
        lastPointTime = -1f;
        Debug.Log("[TrajectoryRecorder] ▶ Запись начата");
    }

    public void StopRecording()
    {
        isRecording = false;
        Debug.Log("[TrajectoryRecorder] ⏹ Запись окончена. Точек: " + trajectory.Count);

        if (SettingsData.Instance != null && SettingsData.Instance.autoSaveTrajectory)
        {
            SaveToFile();
        }
    }

    public List<TrajectoryPoint> GetTrajectory() => trajectory;

    void SaveToFile()
    {
        // Каталог: из настроек или persistentDataPath по умолчанию.
        string directory = Application.persistentDataPath;
        if (SettingsData.Instance != null &&
            SettingsData.Instance.trajectorySavePath != null &&
            SettingsData.Instance.trajectorySavePath.Length > 0)
        {
            directory = SettingsData.Instance.trajectorySavePath;
        }

        System.DateTime now = System.DateTime.Now;
        string stamp = string.Format(
            "{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}",
            now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        string path = Path.Combine(directory, "trajectory_" + stamp + ".json");

        // ЗАПИСЬ ПОД ЗАЩИТОЙ (P2-9). ЧТО БЫЛО: `File.WriteAllText` без `try/catch` и без
        // `Directory.CreateDirectory` — каталога из настроек могло не быть, и исключение
        // уносило всю запись молча. ТЕПЕРЬ каталог создаётся, а отказ виден в журнале.
        try
        {
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(new TrajectoryDataWrapper { points = trajectory.ToArray() }, true);
            File.WriteAllText(path, json);
            Debug.Log("[TrajectoryRecorder] Сохранено: " + path);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[TrajectoryRecorder] НЕ сохранено (" + path + "): " +
                           e.GetType().Name + ": " + e.Message +
                           " · точек в памяти: " + trajectory.Count + " — их можно записать повторно");
        }
    }
}

[System.Serializable]
public class TrajectoryDataWrapper
{
    public TrajectoryPoint[] points;
}
