using System.Collections.Generic;
using PuzzleSystem.Core;

namespace PuzzleOnline.Core
{
    /// <summary>
    /// Giao diện kho dữ liệu tranh puzzle (Puzzle Catalog Contract).
    /// </summary>
    public interface IPuzzleCatalog
    {
        PuzzleData GetPuzzleData(string puzzleId);
        IReadOnlyList<PuzzleData> GetAllPuzzleData();
        PuzzleData DefaultPuzzle { get; }
    }
}
