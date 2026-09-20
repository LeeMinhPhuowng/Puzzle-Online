using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace PuzzleSystem.Core
{
    public static class PuzzleMaterialFactory
    {
        public static Shader GetLitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("URP/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Unlit/Texture");
            if (s == null) s = Shader.Find("Mobile/Diffuse");
            return s;
        }

        public static Material CreateFaceMaterial(Texture2D texture, string materialName = "Mat_PuzzleFace")
        {
            Shader shader = GetLitShader();
            Material mat = new Material(shader) { name = materialName };

            if (texture != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            return mat;
        }

        public static Material CreateSideBackMaterial(string materialName = "Mat_PuzzleSides")
        {
            Shader shader = GetLitShader();
            Material mat = new Material(shader) { name = materialName };

            Color cardboard = new Color(0.82f, 0.74f, 0.62f); // Màu giấy bồi / carton
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cardboard);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", cardboard);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            return mat;
        }

        public static Material CreateGhostGuideMaterial(Texture2D texture, float opacity = 0.28f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Transparent/Diffuse");
            if (shader == null) shader = GetLitShader();

            Material mat = new Material(shader) { name = "Mat_PuzzleGhostGuide" };
            Color col = new Color(1f, 1f, 1f, opacity);

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent in URP
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f); // 0 = Alpha blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            return mat;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Tạo và lưu file Material (.mat) trực tiếp vào thư mục Asset để Prefab không bị rỗng Material.
        /// </summary>
        public static (Material faceMat, Material sideMat) CreateAndSaveMaterialAssets(
            Texture2D texture,
            string targetFolder,
            string prefix = "Puzzle")
        {
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            // Nếu texture là in-memory (ví dụ được tạo từ Auto-Crop và chưa lưu vào AssetDatabase)
            // thì cần lưu ra file .png trên đĩa để Material và Prefab có thể reference bền vững qua GUID.
            if (texture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
            {
                string savedImgPath = $"{targetFolder}/{prefix}_Texture.png";
                byte[] bytes = texture.EncodeToPNG();
                File.WriteAllBytes(savedImgPath, bytes);
                AssetDatabase.ImportAsset(savedImgPath, ImportAssetOptions.ForceUpdate);

                TextureImporter importer = AssetImporter.GetAtPath(savedImgPath) as TextureImporter;
                if (importer != null)
                {
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }

                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(savedImgPath);
            }

            string facePath = $"{targetFolder}/{prefix}_Face.mat";
            string sidePath = $"{targetFolder}/{prefix}_Sides.mat";

            Material faceMat = AssetDatabase.LoadAssetAtPath<Material>(facePath);
            if (faceMat == null)
            {
                faceMat = CreateFaceMaterial(texture, $"{prefix}_Face");
                AssetDatabase.CreateAsset(faceMat, facePath);
            }
            else
            {
                if (texture != null)
                {
                    if (faceMat.HasProperty("_BaseMap")) faceMat.SetTexture("_BaseMap", texture);
                    if (faceMat.HasProperty("_MainTex")) faceMat.SetTexture("_MainTex", texture);
                }
                EditorUtility.SetDirty(faceMat);
            }

            Material sideMat = AssetDatabase.LoadAssetAtPath<Material>(sidePath);
            if (sideMat == null)
            {
                sideMat = CreateSideBackMaterial($"{prefix}_Sides");
                AssetDatabase.CreateAsset(sideMat, sidePath);
            }

            AssetDatabase.SaveAssets();

            return (faceMat, sideMat);
        }
#endif
    }
}
