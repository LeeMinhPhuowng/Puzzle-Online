using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleSystem.Core
{
    [Serializable]
    public class PuzzlePieceData
    {
        public int pieceId;
        public int row;
        public int col;

        public Vector3 correctLocalPosition;
        public Quaternion correctLocalRotation;

        public int[] neighborIds = new int[4]; // 0: Top, 1: Right, 2: Bottom, 3: Left
        public Mesh mesh;
    }

    [CreateAssetMenu(fileName = "NewPuzzleData", menuName = "Puzzle Online/Puzzle Data")]
    public class PuzzleData : ScriptableObject
    {
        [Header("Pack Information & Display")]
        public string puzzleId = "sunset_3x3";
        public string displayName = "Sunset";
        [TextArea(2, 4)] public string description = "Hoàng hôn";
        public Sprite thumbnail;

        [Header("Price & Reward")]
        public int price = 0;
        public int reward = 50;

        [Header("Board Setup")]
        public int rows = 3;
        public int columns = 3;
        public float boardWidth = 0.8f;
        public float boardHeight = 0.8f;
        public float thickness = 0.005f;

        [Header("Materials & Assets")]
        public Texture2D sourceImage;
        public Material puzzleMaterial;
        public Material sideMaterial;

        [Header("Piece Data (Auto Generated)")]
        [HideInInspector]
        public List<PuzzlePieceData> pieces = new List<PuzzlePieceData>();

        public int TotalPieces => rows * columns;
    }
}
