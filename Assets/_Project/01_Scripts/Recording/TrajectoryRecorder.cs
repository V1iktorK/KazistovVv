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

        string json = JsonUtility.ToJson(new TrajectoryDataWrapper { points = trajectory.ToArray() }, true);
        File.WriteAllText(path, json);
        Debug.Log("[TrajectoryRecorder] Сохранено: " + path);
    }
}

[System.Serializable]
public class TrajectoryDataWrapper
{
    public TrajectoryPoint[] points;
}
