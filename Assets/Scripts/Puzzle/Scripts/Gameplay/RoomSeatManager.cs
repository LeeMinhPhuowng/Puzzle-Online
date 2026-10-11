using System.Collections.Generic;
using UnityEngine;

namespace PuzzleSystem.Gameplay
{
    /// <summary>
    /// Component quản lý thứ tự các vị trí ghế ngồi trong phòng chơi.
    /// Có thể gắn trực tiếp vào GameObject 'Positions' trong Scene hoặc bất kỳ GameObject nào.
    /// Cho phép Designer/Developer kéo thả trực tiếp các Transform vị trí vào Inspector để quyết định thứ tự:
    /// Slot 1 (Chủ phòng), Slot 2, Slot 3, Slot 4...
    /// </summary>
    [AddComponentMenu("Puzzle System/Room Seat Manager")]
    public class RoomSeatManager : MonoBehaviour
    {
        [Header("Seat Positions (Thứ tự ghế ngồi)")]
        [Tooltip("Kéo thả các vị trí ngồi (Transform) theo đúng thứ tự từ phòng chờ: Slot 1 (Chủ phòng), Slot 2, Slot 3, Slot 4...")]
        public List<Transform> seats = new List<Transform>();

        public List<Transform> GetValidSeats()
        {
            var result = new List<Transform>();
            if (seats != null)
            {
                foreach (var s in seats)
                {
                    if (s != null) result.Add(s);
                }
            }
            return result;
        }

        private void Reset()
        {
            seats.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                seats.Add(transform.GetChild(i));
            }
        }
    }
}
