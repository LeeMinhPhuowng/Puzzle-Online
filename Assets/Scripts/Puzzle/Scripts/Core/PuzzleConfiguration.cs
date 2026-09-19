using System;
using UnityEngine;

namespace PuzzleSystem.Core
{
    [Serializable]
    public class PuzzleConfiguration
    {
        [Header("Grid Dimensions")]
        [Range(2, 50)] public int rows = 4;
        [Range(2, 50)] public int columns = 4;

        [Header("Board Physical Size (Meters)")]
        [Range(0.2f, 8f)] public float boardWidth = 0.8f;
        [Range(0.2f, 8f)] public float boardHeight = 0.6f;

        [Header("Piece 3D Profile")]
        [Range(0.001f, 0.03f)] public float thickness = 0.005f; // 5mm
        [Range(0.0002f, 0.005f)] public float bevelRadius = 0.0008f; // 0.8mm
        [Range(2, 8)] public int samplesPerSegment = 5;

        [Header("Tab & Hole Shape")]
        [Range(0.1f, 0.35f)] public float tabHeightRatio = 0.22f;
        [Range(0.1f, 0.3f)] public float neckWidthRatio = 0.16f;
        [Range(0.2f, 0.5f)] public float headWidthRatio = 0.32f;

        [Header("Seed & Randomness")]
        public int seed = 12345;
        public bool randomizeSeed = false;
        [Tooltip("Tự động sinh seed mới mỗi khi import ảnh hoặc tạo mới")]
        public bool autoRandomizeSeed = true;

        [Header("Textures & Materials")]
        public Texture2D sourceTexture;
        public Material frontMaterial;
        public Material sideBackMaterial;

        public int TotalPieces => rows * columns;
        public float CellWidth => boardWidth / columns;
        public float CellHeight => boardHeight / rows;
    }
}
