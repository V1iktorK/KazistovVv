#if UNITY_EDITOR
// ================================================================================================
//  KvRecorderBridge.cs — МОСТ К UNITY RECORDER ИЗ РЕДАКТОРНОЙ СБОРКИ (P4-4, 12.11.2026).
//
//  ЗАЧЕМ ОТДЕЛЬНЫЙ ФАЙЛ. Код рекордера — редакторский API (`UnityEditor.Recorder`, `RecorderController`,
//  `MovieRecorderSettings`). Раньше он лежал прямо в рантайм-скрипте `Features/KvCaptures.cs` под
//  `#if UNITY_EDITOR` и компилировался, потому что весь проект жил в предопределённой
//  `Assembly-CSharp`, которой Unity в редакторе добавляет ссылку на `UnityEditor`.
//  После разделения на сборки так нельзя: сборке `KazistovVv.Runtime` запрещено ссылаться на
//  редакторную, и прогон это подтвердил —
//      error CS0234: The type or namespace name 'Recorder' does not exist in the namespace 'UnityEditor'
//  (проверено компиляцией 12.11.2026, файл Logs/asmdef-check.log).
//
//  ЧТО ДЕЛАЕТ. При загрузке домена редактора назначает `KvCaptureService.RecorderStartHook` и
//  `RecorderStopHook`. Рантайм о рекордере ничего не знает: в APK хуки пусты, и работает штатный
//  резерв — последовательность PNG-кадров с манифестом. Это НЕ потеря функции: тот же резерв
//  включался и раньше, когда пакет рекордера был недоступен.
// ================================================================================================

using System;
using UnityEditor;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>Подключает Unity Recorder к рантайм-сервису захвата через мост.</summary>
    [InitializeOnLoad]
    internal static class KvRecorderBridge
    {
        /// <summary>Активный контроллер записи (один на редактор — второй рекордер не нужен).</summary>
        private static UnityEditor.Recorder.RecorderController controller;

        static KvRecorderBridge()
        {
            KvCaptureService.RecorderStartHook = Start;
            KvCaptureService.RecorderStopHook = Stop;
        }

        /// <summary>Начать запись MP4 по запросу рантайма. Возвращает false, если рекордер
        /// недоступен — тогда рантайм включит резерв кадров PNG (законный исход, не ошибка).</summary>
        private static bool Start(KvRecorderRequest request)
        {
            try
            {
                int fps = Mathf.Clamp(request.fps, 1, 120);
                var settings = ScriptableObject.CreateInstance<
                    UnityEditor.Recorder.RecorderControllerSettings>();
                settings.SetRecordModeToManual();
                settings.FrameRate = fps;
                settings.FrameRatePlayback = UnityEditor.Recorder.FrameRatePlayback.Constant;
                settings.CapFrameRate = true;

                var movie = ScriptableObject.CreateInstance<UnityEditor.Recorder.MovieRecorderSettings>();
                movie.name = "KazistovVv Movie";
                movie.Enabled = true;
                movie.OutputFile = request.pathWithoutExtension;
                movie.FrameRate = fps;
                movie.FrameRatePlayback = UnityEditor.Recorder.FrameRatePlayback.Constant;
                movie.CapFrameRate = true;

                var encoder = new UnityEditor.Recorder.Encoder.CoreEncoderSettings();
                encoder.Codec = UnityEditor.Recorder.Encoder.CoreEncoderSettings.OutputCodec.MP4;
                encoder.EncodingQuality =
                    UnityEditor.Recorder.Encoder.CoreEncoderSettings.VideoEncodingQuality.High;
                encoder.TargetBitRate = Mathf.Clamp(request.bitrateKbps / 1000f, 0.1f, 100f);  // Мбит/с
                movie.EncoderSettings = encoder;

                var input = new UnityEditor.Recorder.Input.GameViewInputSettings();
                input.OutputWidth = request.width > 0 ? request.width : 0;
                input.OutputHeight = request.height > 0 ? request.height : 0;
                movie.ImageInputSettings = input;

                settings.AddRecorderSettings(movie);

                controller = new UnityEditor.Recorder.RecorderController(settings);
                controller.PrepareRecording();
                if (!controller.StartRecording())
                {
                    Debug.LogWarning("[Capture] Unity Recorder не запустился — включён резервный режим кадров");
                    controller = null;
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Capture] Unity Recorder недоступен (" + e.Message +
                                 ") — включён резервный режим кадров PNG");
                controller = null;
                return false;
            }
        }

        /// <summary>Остановить запись, начатую мостом.</summary>
        private static void Stop()
        {
            try
            {
                if (controller != null && controller.IsRecording()) controller.StopRecording();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Capture] остановка рекордера: " + e.Message);
            }
            finally
            {
                controller = null;
            }
        }
    }
}
#endif
