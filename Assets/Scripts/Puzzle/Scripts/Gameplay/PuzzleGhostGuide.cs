using System.Collections.Generic;
using UnityEngine;
using PuzzleSystem.Core;

namespace PuzzleSystem.Gameplay
{
    /// <summary>
    /// Quản lý hình ảnh và khung tranh mẫu mờ (Ghost Target Guide) trên mặt bàn.
    /// Giúp người chơi định vị vị trí ghép chính xác và kích thước tranh.
    /// </summary>
    public class PuzzleGhostGuide : MonoBehaviour
    {
        [Header("Ghost Settings")]
        public bool showGhostTarget = true;
        public float ghostAlpha = 0.32f;
        public float ghostHeightOffset = 0.0015f;

        private GameObject guideBoardObj;

        public GameObject GuideBoardObject => guideBoardObj;

        public void EnsureGhostGuideBoard(PuzzleData puzzleData, PuzzleConfiguration config, List<PuzzlePiece> activePieces)
        {
            if (guideBoardObj == null)
            {
                CreateGhostGuideBoard(puzzleData, config, activePieces);
            }
        }

        public void CreateGhostGuideBoard(PuzzleData puzzleData, PuzzleConfiguration config, List<PuzzlePiece> activePieces)
        {
            if (!showGhostTarget) return;

            ClearGhostGuide();

            float bw = 0.85f;
            float bh = 0.63f;

            if (puzzleData != null && puzzleData.boardWidth > 0.01f && puzzleData.boardHeight > 0.01f)
            {
                bw = puzzleData.boardWidth;
                bh = puzzleData.boardHeight;
            }
            else if (config != null && config.boardWidth > 0.01f && config.boardHeight > 0.01f)
            {
                bw = config.boardWidth;
                bh = config.boardHeight;
            }
            else if (activePieces != null && activePieces.Count > 0)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                int validCount = 0;
                foreach (var p in activePieces)
                {
                    if (p == null) continue;
                    minX = Mathf.Min(minX, p.correctLocalPos.x);
                    maxX = Mathf.Max(maxX, p.correctLocalPos.x);
                    minZ = Mathf.Min(minZ, p.correctLocalPos.z);
                    maxZ = Mathf.Max(maxZ, p.correctLocalPos.z);
                    validCount++;
                }
                if (validCount > 0 && maxX > minX && maxZ > minZ)
                {
                    int cols = (puzzleData != null && puzzleData.columns > 1) ? puzzleData.columns : (config != null && config.columns > 1 ? config.columns : 4);
                    int rows = (puzzleData != null && puzzleData.rows > 1) ? puzzleData.rows : (config != null && config.rows > 1 ? config.rows : 3);
                    float padX = (maxX - minX) / Mathf.Max(1, cols - 1);
                    float padZ = (maxZ - minZ) / Mathf.Max(1, rows - 1);
                    bw = (maxX - minX) + padX;
                    bh = (maxZ - minZ) + padZ;
                }
            }

            float halfW = bw * 0.5f;
            float halfH = bh * 0.5f;

            // Tìm Texture của bức tranh
            Texture targetTexture = null;
            if (puzzleData != null)
            {
                targetTexture = puzzleData.sourceImage;
                if (targetTexture == null && puzzleData.puzzleMaterial != null)
                {
                    targetTexture = puzzleData.puzzleMaterial.mainTexture;
                }
            }
            if (targetTexture == null && activePieces != null && activePieces.Count > 0)
            {
                foreach (var p in activePieces)
                {
                    if (p != null)
                    {
                        var r = p.GetComponent<Renderer>();
                        if (r != null && r.sharedMaterial != null && r.sharedMaterial.mainTexture != null)
                        {
                            targetTexture = r.sharedMaterial.mainTexture;
                            break;
                        }
                    }
                }
            }
            if (targetTexture == null && config != null)
            {
                targetTexture = config.sourceTexture;
            }

            // Tạo GameObject gốc chứa toàn bộ khung tranh mờ
            guideBoardObj = new GameObject("[PuzzleGuideBoard]");
            guideBoardObj.transform.SetParent(transform, false);
            guideBoardObj.transform.localPosition = Vector3.zero;
            guideBoardObj.transform.localRotation = Quaternion.identity;
            guideBoardObj.transform.localScale = Vector3.one;

            // 1. Mặt phẳng ảnh mờ (Ghost Target Quad)
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "GhostPlane";
            var quadCol = quad.GetComponent<Collider>();
            if (quadCol != null) DestroyImmediate(quadCol);

            quad.transform.SetParent(guideBoardObj.transform, false);
            quad.transform.localPosition = new Vector3(0f, ghostHeightOffset, 0f); // Nằm sát trên mặt bàn (+1.5mm)
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Xoay ngang hướng lên trên
            quad.transform.localScale = new Vector3(bw, bh, 1f);

            var mr = quad.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = PuzzleMaterialFactory.CreateGhostGuideMaterial(targetTexture, ghostAlpha);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // 2. Khung viền viền ngoài nổi bật (Ghost Border)
            GameObject borderObj = new GameObject("GhostBorder");
            borderObj.transform.SetParent(guideBoardObj.transform, false);
            borderObj.transform.localPosition = new Vector3(0f, ghostHeightOffset + 0.0007f, 0f);
            borderObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            borderObj.transform.localScale = Vector3.one;

            var lr = borderObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 4;
            lr.startWidth = 0.012f;
            lr.endWidth = 0.012f;

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Sprites/Default")
                                 ?? Shader.Find("Unlit/Color");
            Material lineMat = new Material(unlitShader);
            lineMat.color = new Color(1f, 1f, 1f, 0.9f);
            lr.material = lineMat;
            lr.startColor = new Color(1f, 1f, 1f, 0.9f);
            lr.endColor = new Color(1f, 1f, 1f, 0.9f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            lr.SetPosition(0, new Vector3(-halfW, -halfH, 0f));
            lr.SetPosition(1, new Vector3(halfW, -halfH, 0f));
            lr.SetPosition(2, new Vector3(halfW, halfH, 0f));
            lr.SetPosition(3, new Vector3(-halfW, halfH, 0f));

            Debug.Log($"<color=cyan><b>[PuzzleGhostGuide] Đã tạo Khung Mờ Target của bàn cờ ({bw:F2}m x {bh:F2}m) thành công!</b></color>");
        }

        public void ClearGhostGuide()
        {
            if (guideBoardObj != null)
            {
                if (Application.isPlaying) Destroy(guideBoardObj);
                else DestroyImmediate(guideBoardObj);
                guideBoardObj = null;
            }
        }
    }
}
