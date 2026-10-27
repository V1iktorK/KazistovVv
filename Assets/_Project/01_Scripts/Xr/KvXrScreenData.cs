using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ДАННЫЕ ДЛЯ СТРОК ЭКРАНОВ-ОКОН (§42). Одна структура на все экраны: строки остаются ЧИСТОЙ
    /// функцией, которую проверяют тестом без шлема и без живых компонентов.
    /// </summary>
    public struct KvXrScreenData
    {
        // --- общее
        public string RobotName;
        public string Version;
        public string Language;

        // --- «Настройки ПК · Интерфейс/Графика»
        public float UiScale;
        public string GraphicsPreset;
        public float Fps;
        public float FrameMs;
        public int Commands;

        // --- «Панель функций · Суставы»
        public bool JointsReady;
        public double[] JointAngles;

        // --- «Панель функций · Журнал»
        public string[] LogLines;
        public int LogTotal;

        // --- списки (Позы, Сценарии, Сессии, Запись, Зоны)
        public string[] Poses;
        public string[] Scenarios;
        public string[] Sessions;
        public string[] Records;
        public string[] Zones;

        // --- «Замер»
        public int FrameCount;
        public float UptimeSeconds;

        // --- «Верстак»
        public float PayloadKg;
        public float ReferencePlaneHeight;
        public int Waypoints;
        public int Constraints;
        public int RrtNodes;
        public bool CollisionProxies;
        public bool ForcesVisible;
        public bool HeatmapVisible;

        // --- «Виртуальный пульт»
        public bool PendantVisible;
        public string PendantMode;
        public float JogStepMm;

        // --- «Горячие клавиши»
        public string[] Bindings;
        public int BindingsPage;
        public int BindingsPerPage;
    }
}
