using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>

/// УСТАРЕЛО (S44, 30.10.2026). Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Роль исполнения траекторий выполняет MotionExecutor через TrajectoryFlowController.

/// Оставлено помеченным, а не удалено: правило проекта — сначала метка с причиной, удаление отдельным решением.

/// </summary>

[System.Obsolete("Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Роль исполнения траекторий выполняет MotionExecutor через TrajectoryFlowController.")]

public class TrajectoryPlayer : MonoBehaviour
{
    public RobotController robot;
    public TrajectoryRecorder recorder;
    
    private List<TrajectoryPoint> trajectory;
    private bool isPlaying = false;
    private int currentIndex = 0;
    private float playStartTime = 0f;
    
    [Header("Playback")]
    public bool loop = false;
    public float timeScale = 1f;

    void Update()
    {
        if (InputManager.Instance != null && InputManager.Instance.PlayDown && !isPlaying)
        Play();
    }
    
        public void Play()
    {
        if (isPlaying) return; // <-- добавь
        trajectory = recorder.GetTrajectory();
        if (trajectory == null || trajectory.Count < 2) return;

        isPlaying = true;
        currentIndex = 0;
        playStartTime = Time.time;
        Debug.Log($"[TrajectoryPlayer] ▶ Воспроизведение: {trajectory.Count} точек");
        StartCoroutine(PlaybackCoroutine());
    }
    
    public void Stop()
    {
        isPlaying = false;
        StopAllCoroutines();
        Debug.Log("[TrajectoryPlayer] ⏹ Остановлено");
    }
    
    IEnumerator PlaybackCoroutine()
    {
        while (isPlaying && currentIndex < trajectory.Count - 1)
        {
            float elapsed = (Time.time - playStartTime) * timeScale;
            
            while (currentIndex < trajectory.Count - 1 && trajectory[currentIndex + 1].time <= elapsed)
                currentIndex++;
            
            if (currentIndex >= trajectory.Count - 1) break;
            
            var p1 = trajectory[currentIndex];
            var p2 = trajectory[currentIndex + 1];
            float t = Mathf.InverseLerp(p1.time, p2.time, elapsed);
            
            Vector3 pos = Vector3.Lerp(p1.position, p2.position, t);
            Quaternion rot = Quaternion.Slerp(p1.rotation, p2.rotation, t);
            robot.SetTarget(pos, rot);
            
            yield return null;
        }
        
        if (loop && isPlaying) Play();
        else isPlaying = false;
    }
    
    public bool IsPlaying => isPlaying;
}
