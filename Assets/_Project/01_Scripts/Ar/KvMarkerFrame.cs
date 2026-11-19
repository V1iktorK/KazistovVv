using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace KazistovVvAr
{
    /// <summary>
    /// КАДР AR-КАМЕРЫ → ПОЗА QR-МАРКЕРА (19.11.2026).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ КЛАСС. Один и тот же путь «кадр → четыре угла → гомография → поза» нужен
    /// дважды: разовой постановке (`KvArPlacement.TryPlaceByMarkerFromCamera`, кнопка «По маркеру»)
    /// и НЕПРЕРЫВНОМУ якорю (`KvArMarkerAnchor`, скан 15 раз в секунду). Общий код означает, что
    /// постановка и следование не могут разойтись в интринсиках, порогах и системе координат углов.
    ///
    /// БУФЕРЫ. Покадровый скан иначе выделял бы на каждый проход текстуру, массив пикселей и массив
    /// яркости — сотни килобайт мусора в секунду, то есть сборки мусора ровно в момент, когда
    /// оператор целится. `Buffers` живёт вне кадра: текстура пересоздаётся только при смене
    /// разрешения, массивы — при смене размера.
    ///
    /// ЧЕСТНОСТЬ ОТКАЗОВ. Каждый отказ возвращается с причиной для статуса: нет менеджера камеры /
    /// кадр не пришёл / кода нет в кадре / поза не пригодна. Ничего не «додумывается».
    /// </summary>
    public static class KvMarkerFrame
    {
        /// <summary>Причина отказа, когда AR-камеры в сцене нет (её читают оператор, журнал и тесты).</summary>
        public const string NoCameraReason = "камера AR не найдена — сканировать нечего";

        /// <summary>Переиспользуемые буферы кадра (создаются один раз, живут столько, сколько якорь).</summary>
        public sealed class Buffers
        {
            /// <summary>Текстура-приёмник конверсии кадра (пересоздаётся только при смене разрешения).</summary>
            public Texture2D texture;

            /// <summary>Пиксели кадра для детектора (размер = ширина × высота).</summary>
            public Color32[] pixels;

            /// <summary>Буфер яркости детектора (размер = ширина × высота).</summary>
            public byte[] luminance;
        }

        /// <summary>
        /// СВЕЖИЙ КАМЕРНЫЙ КАДР → ПОЗА МАРКЕРА. false — причина в `reason`; true — поза пригодна
        /// (`pose.usable`), и её можно нести в якорь/постановку.
        /// </summary>
        public static bool TryPoseFromCamera(ARCameraManager cameraManager, float markerSizeMeters,
            Buffers buffers, bool requireExactIntrinsics, out KvMarkerPoseResult pose, out string reason)
        {
            pose = default(KvMarkerPoseResult);
            reason = null;

            if (cameraManager == null)
            {
                reason = NoCameraReason;
                return false;
            }

            XRCpuImage image;
            bool acquired;
            try
            {
                acquired = cameraManager.TryAcquireLatestCpuImage(out image);
            }
            catch (Exception error)
            {
                image = default(XRCpuImage);
                acquired = false;
                Debug.LogWarning("[KvAr] МАРКЕР: кадр не получен (" + error.GetType().Name + ")");
            }
            if (!acquired)
            {
                reason = "кадр камеры недоступен — повторите попытку";
                return false;
            }

            Color32[] pixels;
            int width;
            int height;
            using (image)
            {
                width = image.width;
                height = image.height;

                // Конструктор ARSubsystems 6000.5 принимает ИСХОДНЫЙ КАДР: ставит inputRect
                // и outputDimensions во весь кадр (сверено по исходнику ConversionParams.cs).
                XRCpuImage.ConversionParams conversion = new XRCpuImage.ConversionParams(
                    image, TextureFormat.RGBA32, XRCpuImage.Transformation.None);

                Texture2D frame = TextureFor(buffers, width, height);
                if (frame == null)
                {
                    reason = "кадр камеры не помещается в буфер — повторите попытку";
                    return false;
                }

                // Без MirrorY: углы должны лежать в той системе кадра, к которой относятся
                // интринсики ARFoundation. `Apply` не нужен — данные читаются из CPU-буфера.
                image.Convert(conversion, frame.GetRawTextureData<byte>());

                NativeArray<Color32> raw = frame.GetRawTextureData<Color32>();
                pixels = PixelsFor(buffers, raw.Length);
                NativeArray<Color32>.Copy(raw, pixels);
            }

            byte[] luminance = LuminanceFor(buffers, width * height);

            Vector2[] corners;
            if (!KvQrMarkerDetector.TryFindCorners(pixels, width, height, luminance,
                    out corners, out reason))
            {
                return false;
            }

            KvMarkerIntrinsics intrinsics;
            XRCameraIntrinsics camIntrinsics;
            if (cameraManager.TryGetIntrinsics(out camIntrinsics) &&
                camIntrinsics.resolution.x > 0 && camIntrinsics.resolution.y > 0)
            {
                float sx = width / (float)camIntrinsics.resolution.x;
                float sy = height / (float)camIntrinsics.resolution.y;
                intrinsics = new KvMarkerIntrinsics
                {
                    fx = camIntrinsics.focalLength.x * sx,
                    fy = camIntrinsics.focalLength.y * sy,
                    cx = camIntrinsics.principalPoint.x * sx,
                    cy = camIntrinsics.principalPoint.y * sy,
                    approximate = false
                };
            }
            else
            {
                // ФОЛБЭК без интринсик камеры: fx ≈ ширина кадра — поза считается,
                // но статус честно скажет, что она приблизительная.
                intrinsics = KvMarkerIntrinsics.FromFrame(width, height);
            }

            pose = KvMarkerPose.Compute(corners, markerSizeMeters, intrinsics, requireExactIntrinsics);
            if (!pose.usable)
            {
                reason = pose.reason;
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- буферы

        private static Texture2D TextureFor(Buffers buffers, int width, int height)
        {
            if (buffers == null) return null;
            if (buffers.texture != null && buffers.texture.width == width &&
                buffers.texture.height == height)
                return buffers.texture;

            if (buffers.texture != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(buffers.texture);
                else UnityEngine.Object.DestroyImmediate(buffers.texture);
                buffers.texture = null;
                buffers.pixels = null;
            }
            buffers.texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            return buffers.texture;
        }

        private static Color32[] PixelsFor(Buffers buffers, int length)
        {
            if (buffers == null) return new Color32[length];
            if (buffers.pixels == null || buffers.pixels.Length != length)
                buffers.pixels = new Color32[length];
            return buffers.pixels;
        }

        private static byte[] LuminanceFor(Buffers buffers, int length)
        {
            if (buffers == null) return new byte[length];
            if (buffers.luminance == null || buffers.luminance.Length < length)
                buffers.luminance = new byte[length];
            return buffers.luminance;
        }
    }
}
