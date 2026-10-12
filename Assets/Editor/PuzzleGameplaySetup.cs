using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PuzzleOnline.Gameplay.Competitive;

// Idempotent asset migration; also callable in batch mode with -executeMethod PuzzleGameplaySetup.Apply.
public static class PuzzleGameplaySetup
{
    private static readonly string[] ItemNames = { "portable_searchlight", "encre", "gloves", "Magnet", "crowbar" };
    private static readonly string[] Fields = { "flashlight", "ink", "glove", "magnet", "crowbar" };

    [MenuItem("Tools/Puzzle Online/Apply Gameplay and Responsive Setup")]
    public static void Apply()
    {
        GameObject source = PrefabUtility.LoadPrefabContents("Assets/Player.prefab");
        try
        {
            ConfigureItems(source);
            PrefabUtility.SaveAsPrefabAsset(source, "Assets/Player.prefab");
            GameObject resource = PrefabUtility.LoadPrefabContents("Assets/Resources/Player.prefab");
            try
            {
                foreach (string name in ItemNames)
                {
                    if (Find(resource.transform, name) != null) continue;
                    Transform original = Find(source.transform, name);
                    if (original == null) throw new InvalidOperationException("Missing authored item: " + name);
                    string parentPath = AnimationUtility.CalculateTransformPath(original.parent, source.transform);
                    Transform parent = string.IsNullOrEmpty(parentPath) ? resource.transform : resource.transform.Find(parentPath);
                    var model = PrefabUtility.GetCorrespondingObjectFromSource(original.gameObject);
                    GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                    copy.name = name;
                    copy.transform.localPosition = original.localPosition;
                    copy.transform.localRotation = original.localRotation;
                    copy.transform.localScale = original.localScale;
                    copy.SetActive(false);
                }
                ConfigureItems(resource);
                PrefabUtility.SaveAsPrefabAsset(resource, "Assets/Resources/Player.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(resource); }
        }
        finally { PrefabUtility.UnloadPrefabContents(source); }

        foreach (var floor in new[] {
            (scene: "SCN_Nature", path: "Scene/ground_1"),
            (scene: "SCN_Classroom", path: "Classroom/Cube.009"),
            (scene: "SCN_Sakura", path: "picnic/Sphere.003") })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + floor.scene + ".unity", OpenSceneMode.Single);
            var ground = GameObject.Find(floor.path);
            if (ground == null || ground.GetComponent<Collider>() == null)
                throw new InvalidOperationException("Missing map floor collider: " + floor.path);
            ground.layer = LayerMask.NameToLayer("Ground");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        foreach (string name in new[] { "SCN_Menu", "SCN_Lobby", "SCN_Room" })
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
            foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
            {
                if (!canvas.isRootCanvas) continue;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                foreach (RectTransform rect in canvas.transform.Cast<Transform>().OfType<RectTransform>())
                {
                    if (rect.name == "Rooms" || rect.name.EndsWith("View", StringComparison.Ordinal))
                        SetAnchors(rect, Vector2.zero, Vector2.one);
                    else if (rect.name == "CreateRoom") SetAnchors(rect, Vector2.zero, Vector2.up);
                    else if (rect.name == "Image" && rect.sizeDelta.x > 1400)
                        SetAnchors(rect, Vector2.up, Vector2.one);
                    else if (rect.name == "Map Preview" || rect.name == "Puzzle Preview")
                        SetAnchors(rect, Vector2.one, Vector2.one);
                    else if (rect.name.StartsWith("ImageBack", StringComparison.Ordinal))
                    {
                        if (rect.anchoredPosition.y > 300)
                            SetAnchors(rect, rect.sizeDelta.x > 1000 ? Vector2.up : Vector2.one, Vector2.one);
                        else if (rect.sizeDelta.y > 700)
                            SetAnchors(rect, Vector2.zero,
                                rect.anchoredPosition.x < 0 ? Vector2.up : Vector2.one);
                    }
                    // Keep the form's controls attached to the same edge when its column grows taller.
                    if (rect.name == "CreateRoom" || rect.name == "Rooms")
                    {
                        foreach (RectTransform child in rect.Cast<Transform>().OfType<RectTransform>())
                        {
                            if (child.anchorMin.y != 0.5f || child.anchorMax.y != 0.5f) continue;
                            bool bottomAction = child.name == "Create / Start / Ready" || child.name == "Leave" ||
                                child.name == "Peaceful" || child.name == "Mayhem";
                            float y = bottomAction ? 0f : 1f;
                            SetAnchors(child, new Vector2(child.anchorMin.x, y), new Vector2(child.anchorMax.x, y));
                        }
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Puzzle gameplay prefab references and responsive anchors saved.");
    }

    private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true)
        .FirstOrDefault(t => t.name == name);

    private static void ConfigureItems(GameObject player)
    {
        var visuals = player.GetComponent<PlayerHeldItemVisuals>() ?? player.AddComponent<PlayerHeldItemVisuals>();
        var serialized = new SerializedObject(visuals);
        for (int i = 0; i < Fields.Length; i++)
        {
            Transform item = Find(player.transform, ItemNames[i]);
            if (item == null) throw new InvalidOperationException("Missing item " + ItemNames[i]);
            item.gameObject.SetActive(false);
            serialized.FindProperty(Fields[i]).objectReferenceValue = item.gameObject;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector2 DesignSize(RectTransform rect)
    {
        if (rect.GetComponent<Canvas>() != null) return new Vector2(1920, 1080);
        Vector2 parent = rect.parent is RectTransform p ? DesignSize(p) : new Vector2(1920, 1080);
        return Vector2.Scale(rect.anchorMax - rect.anchorMin, parent) + rect.sizeDelta;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        Vector2 size = DesignSize((RectTransform)rect.parent);
        Vector2 offsetMin = rect.offsetMin + Vector2.Scale(rect.anchorMin - min, size);
        Vector2 offsetMax = rect.offsetMax + Vector2.Scale(rect.anchorMax - max, size);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
