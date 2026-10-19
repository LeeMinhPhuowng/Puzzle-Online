package vn.puzzleonline.server;

/**
 * Cấu hình trung tâm cho tất cả các vật phẩm và cơ chế phá hoại / tranh đấu trên server:
 * - Thời gian ảnh hưởng, bán kính, số lượng mảnh che khuất, chu kỳ sinh hộp quà,...
 * Giúp dễ dàng cân bằng game (balance) tại một nơi duy nhất mà không bị hardcode rải rác.
 */
public final class ItemConfig {
    private ItemConfig() {}

    // ----------------- FLASHLIGHT (ĐÈN PIN) -----------------
    public static final double FLASHLIGHT_DURATION_SEC = 4.5;

    // ----------------- SLAP GLOVE (GĂNG TAY) ----------------
    public static final double GLOVE_KNOCKBACK_FORCE = 18.0;   // Lực hất tung người chơi theo phương thẳng đứng (trục Y)
    public static final double GLOVE_STUN_DURATION_SEC = 0.0; // Không choáng, chỉ hất tung lên trời

    // ----------------- MAGNET (NAM CHÂM) --------------------
    public static final double MAGNET_HOLD_DURATION_SEC = 0.0;
    public static final long MAGNET_HOLD_DURATION_MS = 0L; // 0 = Điều khiển tự do ngay sau khi hút

    // ----------------- INK BOTTLE (BÌNH MỰC) ----------------
    public static final double INK_DURATION_SEC = 10.0;
    public static final int INK_OBSCURE_COUNT = 3;
    public static final int INK_RANDOM_EXTRA = 1; // Số mảnh ngẫu nhiên thêm (3 + 0..1 = 3 đến 4 mảnh)

    // ----------------- CROWBAR / SHOVEL (XÀ BENG) -----------
    public static final double SHOVEL_SCATTER_MIN = 0.20;
    public static final double SHOVEL_SCATTER_MAX = 0.45;

    // ----------------- GIFTBOX SPAWN (HỘP QUÀ) --------------
    public static final long GIFTBOX_SPAWN_INTERVAL_MS = 10_000L;
    public static final int GIFTBOX_MAX_COUNT = 6;
    public static final double GIFTBOX_SPAWN_MIN_RADIUS = 1.2;
    public static final double GIFTBOX_SPAWN_MAX_RADIUS = 5.2;

    // ----------------- SCORING (ĐIỂM SỐ) --------------------
    public static final int DEFAULT_PIECE_POINTS = 10;
}
