
using UnityEngine;

namespace PuzzleOnline.Utility
{
    /// <summary>
    /// Cung cấp các phương thức dùng chung để khóa, mở khóa
    /// và kiểm tra trạng thái con trỏ chuột.
    /// </summary>
    public static class CursorManager
    {
        /// <summary>
        /// Trả về true nếu con trỏ chuột đang ở trạng thái Locked.
        /// </summary>
        public static bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>
        /// Khóa con trỏ chuột và ẩn nó.
        /// </summary>
        public static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Bỏ khóa con trỏ chuột và hiển thị nó.
        /// </summary>
        public static void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Thiết lập trạng thái con trỏ chuột theo tham số locked.
        /// </summary>
        /// <param name="locked">
        /// True để khóa và ẩn con trỏ; false để mở khóa và hiển thị.
        /// </param>
        public static void SetLocked(bool locked)
        {
            if (locked) Lock();
            else Unlock();
        }
    }
}
