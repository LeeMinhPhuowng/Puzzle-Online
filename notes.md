# TỔNG HỢP KIẾN TRÚC & THIẾT KẾ HỆ THỐNG PUZZLE TOGETHER

Tài liệu ghi lại toàn bộ các phân tích, quyết định kỹ thuật và thiết kế cơ sở dữ liệu cho dự án **Puzzle Together** (Game xếp hình phối hợp online - Unity + Java Authoritative Server).

---

## 1. Phân Tích Backend Java Hiện Tại

- **Vị trí mã nguồn:** Thư mục `server/` (gồm `PuzzleServer.java`, `Wire.java`, `run-server.ps1`, `tests/integration_test.py`).
- **Mô hình kiến trúc:** **Authoritative Game Server** thuần Java SE 17+ (không phụ thuộc framework bên ngoài).
- **Trách nhiệm của Server (Authoritative):**
  - Xác thực tài khoản (Salt ngẫu nhiên + SHA-256 hash).
  - Quản lý Sảnh (Lobby), Bạn bè (Friends) và Phòng chơi (Rooms, mã phòng 6 ký tự ngẫu nhiên).
  - Khóa mảnh ghép độc quyền (`LOCK_PIECE` - timeout 12s) chống tranh chấp thao tác giữa nhiều người chơi. Tự giải phóng lock khi mất kết nối.
  - Kiểm tra tính hợp lệ khi ráp mảnh (`PLACE_PIECE`: sai số khoảng cách $dx^2 + dy^2 \le 0.018$ và góc quay chuẩn $0^\circ$).
  - Đồng bộ trạng thái bàn cờ theo thời gian thực (Real-time broadcasting).
  - Tính điểm cá nhân và cộng thưởng xu khi hoàn thành bức tranh.
  - Tự động lưu game (Auto-save) mỗi 15 giây; cho phép lưu/tải/chơi lại ván cũ.
  - Cơ chế giữ chỗ người chơi khi ngắt kết nối (Reconnect grace period: 120 giây).

---

## 2. Đánh Giá Giao Thức Truyền Thông (TCP vs UDP, RMI)

| Giao thức | Đánh giá | Lý do & Khuyến nghị |
| :--- | :--- | :--- |
| **TCP** *(Hiện tại)* | ⭐ **Rất hợp lý** | • Phù hợp nhất cho thể loại Puzzle / Board game vì đòi hỏi tính toàn vẹn 100% và đúng thứ tự (tránh desync bàn cờ).<br>• Đã bật `TcpNoDelay = true` giúp gửi gói tin ngay lập tức, đảm bảo độ mượt kéo thả. |
| **UDP** | ⚠️ **Không nên cho logic game** | • Không đảm bảo thứ tự và rơi rớt gói tin. Nếu dùng phải tự code lại Reliable UDP (tốn công vô ích).<br>• **Chỉ nên dùng riêng cho Voice Chat** để giảm độ trễ tối đa. |
| **RMI** | ❌ **Không thể dùng** | • Client là C# Unity còn Server là Java (RMI là Java-to-Java thuần túy).<br>• Mô hình RPC đồng bộ, không phù hợp cho game real-time cần server push sự kiện liên tục. |
| **WebSocket** | 💡 **Hướng nâng cấp** | • Thích hợp nếu muốn mở rộng game sang nền tảng WebGL (chạy trên trình duyệt). |

---

## 3. Làm Rõ Nghiệp Vụ: "Theme" & "UI"

- **UI 2D (Menus, HUD, Buttons, Chat):** Đồng nhất, cố định cho tất cả người chơi. Không hỗ trợ đổi theme UI cá nhân.
- **Theme = Không gian phòng chơi 3D (`room_environments`):**
  - Bao gồm: Mô hình bàn, ghế, ánh sáng và khung cảnh 3D xung quanh (Ví dụ: *Bàn gỗ ngoài thiên nhiên*, *Phòng khách sang trọng*, *Quán cà phê cổ điển*...).
  - **Quyền chọn:** Chủ phòng (Host) là người chọn **Bộ tranh xếp hình** VÀ chọn **Không gian phòng chơi 3D**.
  - Tất cả người chơi tham gia phòng sẽ cùng ngồi vào chung một không gian 3D đó.
  - Tiền xu được dùng để mua **Bộ tranh mới** hoặc mua **Không gian 3D mới** để làm chủ phòng.

---

## 4. Hệ Thống Thống Kê Trong Profile (4 Chỉ Số)

1. **Tổng số điểm đã kiếm (`total_score`):** Lưu tích lũy trong bảng `accounts`.
2. **Tổng số theme/không gian 3D sở hữu:** Đếm từ bảng `user_environments`.
3. **Tổng số bộ puzzle sở hữu:** Đếm từ bảng `user_puzzles`.
4. **Số bộ puzzle đã hoàn thành (Unique):**
   - Hoàn thành 1 bộ nhiều lần vẫn chỉ tính là 1 bộ.
   - Được quản lý bằng bảng `user_completed_puzzles` với khóa chính kép `(user_id, puzzle_id)` đảm bảo chặn trùng lặp tuyệt đối ở tầng CSDL.

---

## 5. Thiết Kế Cơ Sở Dữ Liệu Hoàn Chỉnh (MySQL 8.0+)

```sql
CREATE DATABASE IF NOT EXISTS puzzle_online_db
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE puzzle_online_db;

-- 1. BẢNG TÀI KHOẢN (ACCOUNTS - BẢO MẬT & XÁC THỰC)
CREATE TABLE accounts (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(20) NOT NULL UNIQUE,
    password_hash VARCHAR(128) NOT NULL,
    salt VARCHAR(64) NOT NULL,
    coins INT NOT NULL DEFAULT 300,
    role VARCHAR(16) NOT NULL DEFAULT 'PLAYER',  -- 'PLAYER' hoặc 'ADMIN'
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_accounts_username (username)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. BẢNG THÔNG TIN CÁ NHÂN & THỐNG KÊ (USER_PROFILES - QUAN HỆ 1-1 VỚI ACCOUNTS)
CREATE TABLE user_profiles (
    user_id BIGINT PRIMARY KEY,                      -- Khóa chính đồng thời là Khóa ngoại
    display_name VARCHAR(32) DEFAULT NULL,           -- Tên hiển thị công khai
    avatar_url VARCHAR(255) DEFAULT 'default_avatar.png',
    bio VARCHAR(255) DEFAULT 'Yêu thích xếp hình!',
    
    -- 4 chỉ số thống kê Profile
    total_score INT NOT NULL DEFAULT 0,              -- 1. Tổng điểm đã kiếm
    total_themes_owned INT NOT NULL DEFAULT 1,       -- 2. Tổng số theme sở hữu (mặc định có sẵn 1)
    total_puzzles_owned INT NOT NULL DEFAULT 1,      -- 3. Tổng số puzzle sở hữu (mặc định có sẵn 1)
    unique_puzzles_completed INT NOT NULL DEFAULT 0, -- 4. Số bộ hoàn thành unique
    
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_profiles_account FOREIGN KEY (user_id) REFERENCES accounts(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. BẢNG BẠN BÈ (FRIENDS)
CREATE TABLE friends (
    user_id BIGINT NOT NULL,
    friend_id BIGINT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, friend_id),
    CONSTRAINT fk_friends_user FOREIGN KEY (user_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_friends_friend FOREIGN KEY (friend_id) REFERENCES accounts(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. BẢNG DANH MỤC TRANH (PUZZLES)
CREATE TABLE puzzles (
    id VARCHAR(32) PRIMARY KEY,
    name VARCHAR(64) NOT NULL,
    rw INT NOT NULL,
    col INT NOT NULL,
    price INT NOT NULL DEFAULT 0,
    asset_path VARCHAR(255) DEFAULT NULL,        -- URL tải ảnh hoặc path texture trong Unity
    is_active BOOLEAN NOT NULL DEFAULT TRUE      -- Admin bật/tắt hiển thị
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. BẢNG DANH MỤC KHÔNG GIAN PHÒNG 3D (ROOM_ENVIRONMENTS)
CREATE TABLE room_environments (
    id VARCHAR(32) PRIMARY KEY,
    name VARCHAR(64) NOT NULL,
    description VARCHAR(255) DEFAULT NULL,
    price INT NOT NULL DEFAULT 0,
    prefab_3d_path VARCHAR(255) DEFAULT NULL,    -- Đường dẫn Prefab 3D (bàn, ghế, đèn)
    is_active BOOLEAN NOT NULL DEFAULT TRUE      -- Admin bật/tắt hiển thị
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 6. KHO TRANH ĐÃ SỞ HỮU (USER_PUZZLES)
CREATE TABLE user_puzzles (
    user_id BIGINT NOT NULL,
    puzzle_id VARCHAR(32) NOT NULL,
    unlocked_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, puzzle_id),
    CONSTRAINT fk_user_puzzles_user FOREIGN KEY (user_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_user_puzzles_puzzle FOREIGN KEY (puzzle_id) REFERENCES puzzles(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 7. KHO KHÔNG GIAN 3D ĐÃ SỞ HỮU (USER_ENVIRONMENTS)
CREATE TABLE user_environments (
    user_id BIGINT NOT NULL,
    environment_id VARCHAR(32) NOT NULL,
    unlocked_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, environment_id),
    CONSTRAINT fk_user_env_user FOREIGN KEY (user_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_user_env_env FOREIGN KEY (environment_id) REFERENCES room_environments(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 8. BẢNG CÁC BỘ TRANH ĐÃ HOÀN THÀNH (USER_COMPLETED_PUZZLES)
CREATE TABLE user_completed_puzzles (
    user_id BIGINT NOT NULL,
    puzzle_id VARCHAR(32) NOT NULL,
    first_completed_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, puzzle_id),            -- Khóa kép chặn trùng lặp hoàn thành
    CONSTRAINT fk_completed_user FOREIGN KEY (user_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_completed_puzzle FOREIGN KEY (puzzle_id) REFERENCES puzzles(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 9. BẢN LƯU GAME TIẾN TRÌNH (SAVED_GAMES) - Tối ưu lưu trữ bằng JSON
CREATE TABLE saved_games (
    id VARCHAR(36) PRIMARY KEY,                  -- UUID sinh từ Java
    owner_id BIGINT NOT NULL,
    room_name VARCHAR(64) NOT NULL,
    label VARCHAR(64) NOT NULL,
    is_automatic BOOLEAN NOT NULL DEFAULT FALSE,
    puzzle_id VARCHAR(32) NOT NULL,
    environment_id VARCHAR(32) NOT NULL,
    elapsed_seconds INT NOT NULL DEFAULT 0,
    is_completed BOOLEAN NOT NULL DEFAULT FALSE,
    scores JSON NOT NULL,                        -- Lưu điểm số: {"alice": 5, "bob": 3}
    pieces JSON NOT NULL,                        -- Lưu tọa độ, góc xoay các mảnh ghép
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_saved_games_owner (owner_id, created_at DESC),
    CONSTRAINT fk_saved_games_owner FOREIGN KEY (owner_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_saved_games_puzzle FOREIGN KEY (puzzle_id) REFERENCES puzzles(id) ON DELETE CASCADE,
    CONSTRAINT fk_saved_games_env FOREIGN KEY (environment_id) REFERENCES room_environments(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 10. DỮ LIỆU BAN ĐẦU (SEED DATA)
INSERT INTO puzzles (id, name, rw, col, price, asset_path) VALUES
('sunset_3x3', 'Hoàng hôn', 3, 3, 0,   'Puzzles/sunset_3x3'),
('forest_4x3', 'Rừng xanh', 3, 4, 160, 'Puzzles/forest_4x3'),
('ocean_4x4',  'Đại dương', 4, 4, 240, 'Puzzles/ocean_4x4');

INSERT INTO room_environments (id, name, description, price, prefab_3d_path) VALUES
('nature_garden', 'Bàn gỗ ngoài thiên nhiên', 'Không gian sân vườn thoáng đãng với bàn ghế gỗ mộc mạc', 0,   'Environments/NatureGarden3D'),
('luxury_lounge', 'Phòng khách sang trọng',   'Không gian ấm cúng, bàn đá cẩm thạch sang trọng',       150, 'Environments/LuxuryLounge3D'),
('cozy_cafe',     'Quán cà phê cổ điển',       'Góc quán cà phê phong cách vintage hoài niệm',          200, 'Environments/CozyCafe3D');



```

---

## 6. Cơ Chế Quản Trị Cho Admin

1. **Kiểm soát bằng cờ `is_active` (Soft Disable):** Không xóa cứng dữ liệu để tránh lỗi ràng buộc với những người chơi đã mua tranh/theme trước đó.
2. **Cập nhật danh mục động (Hot-Reload):** Khi Admin thay đổi giá hoặc thêm tranh/theme mới vào CSDL, Server Java nạp lại từ DB vào RAM và phát lệnh `SHOP_LIST` mới cho toàn bộ người chơi mà không cần tắt/bật lại server.
3. **Cơ chế tải tranh động ở Client:**
   - Trường `asset_path` trong bảng `puzzles` có thể lưu URL ảnh online.
   - Khi vào phòng, Unity Client tải ảnh qua HTTP/HTTPS và tự động cắt thành các mảnh ghép theo $Rows \times Columns$ nhận từ Server. Admin có thể thêm vô số tranh mới mà không cần build lại bản cài đặt game.

