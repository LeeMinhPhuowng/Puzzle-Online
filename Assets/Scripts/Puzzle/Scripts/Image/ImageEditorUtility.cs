using UnityEngine;

namespace PuzzleSystem.Image
{
    public static class ImageEditorUtility
    {
        /// <summary>
        /// Tạo bản sao Texture2D có thể đọc được (isReadable = true) từ bất kỳ Texture nào thông qua GPU RenderTexture Blit.
        /// Giúp người dùng import ảnh bất kỳ mà không cần chỉnh Texture Import Settings thủ công trong Editor.
        /// </summary>
        public static Texture2D CreateReadableTexture(Texture2D source)
        {
            if (source == null) return null;

            RenderTexture tmp = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            );

            Graphics.Blit(source, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;

            Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.name = source.name + "_Readable";
            readable.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            return readable;
        }

        /// <summary>
        /// Cắt ảnh theo vùng toạ độ chuẩn hoá [0, 1].
        /// </summary>
        public static Texture2D CropTexture(Texture2D source, Rect normalizedRect)
        {
            if (source == null) return null;

            Texture2D readableSource = source.isReadable ? source : CreateReadableTexture(source);

            int startX = Mathf.Clamp((int)(normalizedRect.x * readableSource.width), 0, readableSource.width - 1);
            int startY = Mathf.Clamp((int)(normalizedRect.y * readableSource.height), 0, readableSource.height - 1);
            int cropW = Mathf.Clamp((int)(normalizedRect.width * readableSource.width), 1, readableSource.width - startX);
            int cropH = Mathf.Clamp((int)(normalizedRect.height * readableSource.height), 1, readableSource.height - startY);

            Color[] pixels = readableSource.GetPixels(startX, startY, cropW, cropH);

            Texture2D result = new Texture2D(cropW, cropH, TextureFormat.RGBA32, false);
            result.name = source.name + "_Cropped";
            result.SetPixels(pixels);
            result.Apply();

            return result;
        }

        /// <summary>
        /// Tự động crop ảnh ở trung tâm sao cho khớp hoàn hảo với tỉ lệ mục tiêu (targetAspect = targetWidth / targetHeight).
        /// </summary>
        public static Texture2D AutoFitAspect(Texture2D source, float targetAspect)
        {
            if (source == null || targetAspect <= 0.001f) return source;

            float currentAspect = (float)source.width / source.height;
            Rect normRect;

            if (currentAspect > targetAspect)
            {
                // Ảnh hiện tại rộng hơn tỉ lệ mục tiêu -> Cắt bớt 2 bên trái/phải
                float visibleWidthRatio = targetAspect / currentAspect;
                float startX = (1f - visibleWidthRatio) * 0.5f;
                normRect = new Rect(startX, 0f, visibleWidthRatio, 1f);
            }
            else
            {
                // Ảnh hiện tại cao hơn tỉ lệ mục tiêu -> Cắt bớt trên/dưới
                float visibleHeightRatio = currentAspect / targetAspect;
                float startY = (1f - visibleHeightRatio) * 0.5f;
                normRect = new Rect(0f, startY, 1f, visibleHeightRatio);
            }

            return CropTexture(source, normRect);
        }
    }
}
