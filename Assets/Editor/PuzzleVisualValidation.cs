using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PuzzleOnline.Utility;

public static class PuzzleVisualValidation
{
    private static readonly string Output = Path.Combine(Path.GetTempPath(), "puzzle-gameplay-qa");
    private static readonly Vector2Int[] Sizes = { new Vector2Int(1920,1080), new Vector2Int(1366,768),
        new Vector2Int(2560,1440), new Vector2Int(1920,1200), new Vector2Int(1280,800) };

    public static void Run()
    {
        PuzzleResponsiveGUIValidation.Run();
        Directory.CreateDirectory(Output);
        DumpMapGeometry();
        foreach (string sceneName in new[] { "SCN_Menu", "SCN_Lobby", "SCN_Room" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
            Canvas[] canvases = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas).ToArray();
            var camera = new GameObject("QA Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            camera.cullingMask = 1 << 5;
            var baseline = new Dictionary<Button, float>();
            foreach (Canvas canvas in canvases)
            {
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvas.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                canvas.transform.localScale = Vector3.one;
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            }
            foreach (Vector2Int size in Sizes)
            {
                float scale = Mathf.Min(size.x / 1920f, size.y / 1080f);
                foreach (Canvas canvas in canvases) ((RectTransform)canvas.transform).sizeDelta = new Vector2(size.x / scale, size.y / scale);
                camera.orthographicSize = size.y / scale * 0.5f;
                camera.aspect = (float)size.x / size.y;
                Canvas.ForceUpdateCanvases();
                foreach (Canvas canvas in canvases)
                {
                    Rect screen = ((RectTransform)canvas.transform).rect;
                    foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
                    {
                        if (button.GetComponentInParent<ScrollRect>(true) != null) continue;
                        var corners = new Vector3[4];
                        ((RectTransform)button.transform).GetWorldCorners(corners);
                        Vector2 min = canvas.transform.InverseTransformPoint(corners[0]);
                        Vector2 max = min;
                        foreach (Vector3 corner in corners)
                        {
                            Vector2 point = canvas.transform.InverseTransformPoint(corner);
                            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                        }
                        float total = (max.x - min.x) * (max.y - min.y);
                        float visible = Mathf.Max(0, Mathf.Min(max.x, screen.xMax) - Mathf.Max(min.x, screen.xMin)) *
                            Mathf.Max(0, Mathf.Min(max.y, screen.yMax) - Mathf.Max(min.y, screen.yMin));
                        float fraction = total > 0 ? visible / total : 0;
                        if (size == Sizes[0]) baseline[button] = fraction;
                        else if (baseline[button] >= 0.9f && fraction < baseline[button] - 0.015f)
                            throw new InvalidOperationException(sceneName + " clips button " + button.name + " at " + size);
                    }
                }
                Save(camera, size.x, size.y, sceneName + "-" + size.x + "x" + size.y + ".png");
                Debug.Log("PASS: actual button bounds and UI render " + sceneName + " " + size);
            }
        }
        CaptureOutline();
    }

    private static void DumpMapGeometry()
    {
        var lines = new List<string>();
        foreach (string name in new[] { "SCN_Nature", "SCN_Classroom", "SCN_Sakura", "Test" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
            foreach (var player in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerController.PlayerController>(true)))
                lines.Add(name + " PLAYER " + player.transform.position);
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)))
            {
                Bounds b = renderer.bounds;
                if (b.size.x * b.size.z > 6 && b.size.y < 1)
                    lines.Add(name + " | " + UnityEditor.AnimationUtility.CalculateTransformPath(renderer.transform, null) +
                        " | collider=" + (renderer.GetComponent<Collider>() != null) + " | " + b);
            }
        }
        File.WriteAllLines(Path.Combine(Output, "map-geometry.txt"), lines);
    }

    private static void CaptureOutline()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("QA Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0.4f, -4);
        camera.transform.LookAt(Vector3.zero);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = Color.red;
        target.GetComponent<Renderer>().sharedMaterial = material;
        var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.transform.SetParent(target.transform);
        eye.transform.localPosition = new Vector3(0.5f, 0.3f, -0.2f);
        eye.transform.localScale = Vector3.one * 0.4f;
        eye.GetComponent<Renderer>().sharedMaterial = material;
        Save(camera, 800, 600, "outline-before.png");
        var outline = target.AddComponent<TargetOutline>();
        outline.Show(true);
        Save(camera, 800, 600, "outline-after.png");
        if (target.GetComponent<Renderer>().sharedMaterial != material)
            throw new InvalidOperationException("Outline replaced the original material");
        Debug.Log("PASS: multi-renderer outline preserves original materials");
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(material);

        var template = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPuzzles/Sunset/sunset.prefab")
            .GetComponentInChildren<PuzzleSystem.Gameplay.PuzzlePiece>();
        var piece = UnityEngine.Object.Instantiate(template.gameObject);
        piece.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        piece.transform.localScale = Vector3.one * 4;
        var original = piece.GetComponent<Renderer>().sharedMaterials;
        camera.transform.position = new Vector3(0, 1.4f, -1.2f);
        camera.transform.LookAt(piece.GetComponent<Renderer>().bounds.center);
        var light = new GameObject("QA light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        light.transform.rotation = Quaternion.Euler(45, -30, 0);
        Save(camera, 800, 600, "puzzle-outline-before.png");
        piece.AddComponent<TargetOutline>().Show(true);
        Save(camera, 800, 600, "puzzle-outline-after.png");
        if (!piece.GetComponent<Renderer>().sharedMaterials.SequenceEqual(original))
            throw new InvalidOperationException("Puzzle outline replaced its face/side materials");
        Debug.Log("PASS: actual puzzle mesh outline preserves face and side materials");
    }

    private static void Save(Camera camera, int width, int height, string file)
    {
        var render = new RenderTexture(width, height, 24);
        camera.targetTexture = render;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = render;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        File.WriteAllBytes(Path.Combine(Output, file), texture.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(texture);
        render.Release();
        UnityEngine.Object.DestroyImmediate(render);
    }
}

// Exercise the real IMGUI event lifecycle; Canvas-only renders do not cover GUI.matrix scopes.
public sealed class PuzzleResponsiveGUIValidation : UnityEditor.EditorWindow
{
    private bool tested;

    [UnityEditor.MenuItem("Tools/Puzzle Online/Validate IMGUI Matrix")]
    public static void Run()
    {
        var window = CreateInstance<PuzzleResponsiveGUIValidation>();
        try
        {
            window.Show();
            window.SendEvent(new Event { type = EventType.Layout });
            if (!window.tested) throw new InvalidOperationException("IMGUI validation event was not dispatched.");
            Debug.Log("PASS: responsive IMGUI viewports initialize and restore a valid GUI.matrix.");
        }
        finally { window.Close(); }
    }

    private void OnGUI()
    {
        Matrix4x4 before = GUI.matrix;
        using (var viewport = new ResponsiveGUI(fixedLayout: false))
        {
            if (viewport.Width <= 0 || viewport.Height <= 0 || !GUI.matrix.ValidTRS())
                throw new InvalidOperationException("Responsive IMGUI viewport is uninitialized.");
            using (var centered = new ResponsiveGUI(fixedLayout: true))
            {
                if (!GUI.matrix.ValidTRS()) throw new InvalidOperationException("Centered IMGUI matrix is invalid.");
            }
            if (!GUI.matrix.ValidTRS()) throw new InvalidOperationException("Nested IMGUI matrix restoration failed.");
        }
        if (GUI.matrix != before) throw new InvalidOperationException("IMGUI matrix was not restored.");
        tested = true;
    }
}
