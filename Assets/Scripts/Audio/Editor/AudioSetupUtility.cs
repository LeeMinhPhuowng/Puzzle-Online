#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PuzzleOnline.Audio.Editor
{
    public static class AudioSetupUtility
    {
        [MenuItem("Tools/Puzzle Online/🔊 Create Default SFX Catalog Asset", false, 30)]
        public static void CreateDefaultSFXCatalog()
        {
            string folder = "Assets/Resources";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string assetPath = "Assets/Resources/SFXCatalog.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SFXCatalog>(assetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                Debug.Log("<color=yellow>[AudioSetup] File SFXCatalog.asset đã tồn tại tại Assets/Resources/SFXCatalog.asset!</color>");
                return;
            }

            var catalog = ScriptableObject.CreateInstance<SFXCatalog>();
            AssetDatabase.CreateAsset(catalog, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = catalog;
            Debug.Log("<color=green><b>[AudioSetup] Đã tạo thành công file SFXCatalog.asset tại Assets/Resources/SFXCatalog.asset!</b></color>");
        }
    }
}
#endif
