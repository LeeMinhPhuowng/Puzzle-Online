using System;
using UnityEngine;

namespace PuzzleOnline.Gameplay.Competitive
{
    /// <summary>
    /// Cấu hình trung tâm cho tất cả các vật phẩm (Items) trong trò chơi:
    /// Tầm sử dụng, thời gian ảnh hưởng, mức độ ảnh hưởng (lực đẩy găng tay, số mảnh mực che,...).
    /// </summary>
    public static class ItemConfig
    {
        // ----------------- FLASHLIGHT (ĐÈN PIN) -----------------
        [Header("Flashlight Settings")]
        public static float FlashlightRange = 15f;           // Tầm chiếu rọi tối đa (mét)
        public static float FlashlightBlindDuration = 4.5f;   // Thời gian làm mù màn hình (giây)

        // ----------------- SLAP GLOVE (GĂNG TAY) ----------------
        [Header("Slap Glove Settings")]
        public static float GloveRange = 3.8f;                // Tầm tát người chơi (mét)
        public static float GloveKnockbackForce = 18f;        // Lực hất tung lên trời theo phương thẳng đứng (trục Y)
        public static float GloveStunDuration = 0f;           // Không gây choáng, chỉ hất tung lên trời
        public static float GloveCameraShakeIntensity = 0.85f; // Cường độ rung giật màn hình khi bị tát
        public static float GloveCameraShakeDuration = 0.65f;  // Thời gian rung camera khi bị tát (giây)
 
        // ----------------- MAGNET (NAM CHÂM) --------------------
        [Header("Magnet Settings")]
        public static float MagnetRange = 20f;                // Tầm hút mảnh ghép từ bàn hoặc người chơi (mét)
        public static float MagnetHoldDuration = 0f;          // 0f = Điều khiển tự do ngay sau khi hút (như găng tay)

        // ----------------- INK BOTTLE (BÌNH MỰC) ----------------
        [Header("Ink Settings")]
        public static float InkRange = 12f;                   // Tầm ném / tác dụng (mét)
        public static float InkDuration = 10f;                // Thời gian mực che khuất mảnh (giây)
        public static int InkObscurePieceCount = 3;           // Số lượng mảnh cơ bản bị bôi đen và giấu điểm số
        public static int InkRandomExtra = 1;                 // Số lượng mảnh thêm ngẫu nhiên (3 đến 4 mảnh)

        // ----------------- CROWBAR / SHOVEL (XÀ BENG) -----------
        [Header("Crowbar Settings")]
        public static float CrowbarRange = 5.0f;              // Tầm cạy mảnh ghép đã đặt (mét)
        public static float CrowbarScatterRadius = 0.4f;      // Độ văng mảnh sau khi cạy

        // ----------------- SCORING (ĐIỂM SỐ) --------------------
        [Header("Scoring Settings")]
        public static int DefaultPiecePoints = 10;            // Điểm mặc định mỗi mảnh ghép đúng (chế độ Cổ điển)
    }
}
