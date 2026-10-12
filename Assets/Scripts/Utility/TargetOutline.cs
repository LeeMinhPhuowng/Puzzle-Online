using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleOnline.Utility
{
    // Separate renderers preserve the target's materials, textures and colours.
    public sealed class TargetOutline : MonoBehaviour
    {
        private readonly List<Renderer> shells = new List<Renderer>();
        private Material maskMaterial;
        private Material outlineMaterial;
        private bool initialized;

        public void Show(bool visible)
        {
            if (visible && !initialized) Initialize();
            foreach (var shell in shells)
                if (shell != null) shell.enabled = visible;
        }

        private void Initialize()
        {
            initialized = true;
            Shader maskShader = Resources.Load<Shader>("Rendering/TargetOutlineMask");
            Shader outlineShader = Resources.Load<Shader>("Rendering/TargetOutline");
            if (maskShader == null || outlineShader == null) return;
            maskMaterial = new Material(maskShader);
            outlineMaterial = new Material(outlineShader);
            foreach (var source in GetComponentsInChildren<Renderer>())
            {
                if (source is not MeshRenderer && source is not SkinnedMeshRenderer) continue;
                if (source.GetComponent<TextMesh>() != null) continue;
                AddShell(source, maskMaterial);
                AddShell(source, outlineMaterial);
            }
        }

        private void AddShell(Renderer source, Material material)
        {
            var child = new GameObject("TargetOutline") { layer = source.gameObject.layer };
            child.transform.SetParent(source.transform, false);
            Renderer shell;
            Mesh mesh;
            if (source is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
                var skin = child.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.bones = skinned.bones;
                skin.rootBone = skinned.rootBone;
                skin.localBounds = skinned.localBounds;
                shell = skin;
            }
            else
            {
                mesh = source.GetComponent<MeshFilter>()?.sharedMesh;
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                shell = child.AddComponent<MeshRenderer>();
            }
            if (mesh == null) { Destroy(child); return; }
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            shell.sharedMaterials = materials;
            shell.shadowCastingMode = ShadowCastingMode.Off;
            shell.receiveShadows = false;
            shell.enabled = false;
            shells.Add(shell);
        }

        private void OnDisable() => Show(false);
        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (maskMaterial != null) Destroy(maskMaterial);
                if (outlineMaterial != null) Destroy(outlineMaterial);
            }
            else
            {
                if (maskMaterial != null) DestroyImmediate(maskMaterial);
                if (outlineMaterial != null) DestroyImmediate(outlineMaterial);
            }
        }
    }

    public sealed class TargetOutlineSelection
    {
        private TargetOutline current;
        public void Set(GameObject target)
        {
            if (current != null && current.gameObject == target) return;
            if (current != null) current.Show(false);
            current = target != null ? target.GetComponent<TargetOutline>() ?? target.AddComponent<TargetOutline>() : null;
            if (current != null) current.Show(true);
        }
    }
}
