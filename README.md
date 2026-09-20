# 🧩 Puzzle Online (Cooperative Real-time Jigsaw Puzzle 3D)

> **Hệ thống trò chơi ghép tranh 3D trực tuyến nhiều người chơi theo thời gian thực (Cooperative Multiplayer)**  
> **Đồ án môn học / Bài tập lớn Lập trình Mạng - Học viện Công nghệ Bưu chính Viễn thông (PTIT)**

---

## 📖 Giới Thiệu Đề Tài

**Puzzle Online** là nền tảng game ghép hình (Jigsaw Puzzle) 3D trực tuyến hỗ trợ tương tác nhiều người chơi cùng lúc trong một phòng chơi ảo (Cooperative Gameplay). Trò chơi được xây dựng theo mô hình **3-Tier Enterprise** với kiến trúc **Client-Server Authoritative**, đảm bảo tính toàn vẹn dữ liệu, chống gian lận và triệt tiêu hoàn toàn xung đột thao tác (Race Conditions) giữa các người chơi bằng cơ chế khóa độc quyền phân tán (Pessimistic Piece Locking).

---

## 🛠️ Công Nghệ Sử Dụng (Tech Stack)

| Thành phần | Công nghệ / Thư viện | Vai trò & Đặc điểm |
| :--- | :--- | :--- |
| **Client Engine** | **Unity 2022.3+ (URP)**, C# | Hiển thị đồ họa 3D thời gian thực, Camera Raycast tương tác, UI Canvas, First-Person Controller. |
| **Game Server** | **Java SE 17+ Socket Server** | Server Authoritative lõi, định tuyến gói tin đa luồng, kiểm tra thẩm định snap và phát sóng thời gian thực. |
| **Database** | **MySQL 8.0+ / 9.4** | Lưu trữ bền vững 10 bảng (Users, Sessions, Placements, Friends, Chat,...), quan hệ chuẩn 3NF, bảo đảm ACID. |
| **Connection Pool**| **HikariCP / Custom Pool** | Tối ưu hóa truy vấn CSDL hiệu năng cao, tái sử dụng kết nối. |
| **Giao thức Mạng** | **Custom TCP Wire Protocol** | Đóng gói nhị phân Length-Prefixed (`[2 bytes Length] + [UTF-8 JSON]`) chống vỡ gói TCP, `TcpNoDelay = true`. |
| **Bảo mật** | **SHA-256 + Salt ngẫu nhiên** | Băm mật khẩu người dùng nhiều lớp, chống tấn công Rainbow Table. |

---

## ✨ Tính Năng Nổi Bật

### 🎮 1. Gameplay 3D & Đồng Bộ Thời Gian Thực (Real-time Sync 20Hz)
- **Tương tác trực quan:** Nhấp chuột nhặt (Pick), kéo rê bám theo mặt bàn (Drag), xoay 90 độ (phím `R` hoặc lăn chuột) và đặt mảnh ghép (Snap).
- **Đồng bộ chuyển động mượt mà:** Server phát sóng gói tin nhẹ `PIECE_MOVED` ở tần số 20Hz; Client dùng thuật toán nội suy `Vector3.Lerp` và `Quaternion.Slerp` giúp mảnh ghép lướt đi mượt mà trên mọi máy.
- **Đồng bộ thả tự do:** Khi người chơi thả mảnh ở bất kỳ đâu trên bàn (không cần snap), tọa độ và góc xoay được cập nhật tức thời cho tất cả người chơi trong phòng.

### 🔒 2. Khóa Độc Quyền Phân Tán (Pessimistic Piece Locking) & Hiệu Ứng Nâng Mảnh
- **Chống xung đột đa người chơi:** Khi một người chơi nhặt mảnh, Server khóa mảnh và thông báo tới cả phòng. Người khác không thể nhặt mảnh đang bị giữ.
- **Hiệu ứng bay bổng trên không (Remote Visual Lift):** Khi Người chơi A nhấc mảnh lên, Người chơi B nhìn thấy mảnh ghép **lập tức bay bổng lên độ cao 3.5cm (`liftHeight = 0.035m`)** và ánh màu hổ phách cảnh báo. Mảnh ghép lơ lửng trên không trong suốt quá trình kéo rê và chỉ hạ xuống khi nhả chuột.

### 🛡️ 3. Thành Chắn Viền Bàn Chơi (Table Boundary Clamp & Invisible Colliders)
- **Tự động giới hạn di chuyển:** Hàm `ClampToTableBounds()` giữ con trỏ mảnh ghép luôn trượt bên trong mặt bàn. Dù người chơi vung chuột ra ngoài phòng hay trỏ xuống sàn, mảnh ghép không bao giờ bị kéo rơi ra ngoài bàn.
- **Tường chắn vô hình:** Tự động sinh 4 `BoxCollider` vô hình cao 0.8m bao bọc quanh 4 mép bàn để bảo vệ các mảnh ghép.

### 🏫 4. Không Gian Phòng Chơi 3D Đa Dạng (Themes / Environments)
- **Thiên nhiên dã ngoại (`SCN_Nature`):** Bàn dã ngoài trời giữa rừng cây xanh mát.
- **Lớp học phong cách Mỹ (`SCN_Classroom`):** Phòng học tiện nghi với bàn ghế học sinh.
- **Vườn hoa anh đào (`SCN_Sakura`):** Khung cảnh mùa xuân Nhật Bản lãng mạn.
- **Tự động định vị chỗ ngồi (1/4 Chair Positions):** Người chơi khi vào phòng được tự động xếp vào 4 ghế ngồi xung quanh bàn và quay mặt hướng vào trung tâm bàn cờ.

### 💬 5. Tương Tác Xã Hội & Bạn Bè
- **Chat phòng & Biểu cảm Emoji:** Trò chuyện văn bản và thả biểu cảm hoạt họa theo thời gian thực.
- **Hệ thống bạn bè:** Tìm kiếm người dùng, gửi/chấp nhận/từ chối lời mời kết bạn, danh sách bạn bè với trạng thái hoạt động (Trực tuyến, Trong phòng, Ngoại tuyến).
- **Voice Chat:** Tích hợp bộ truyền nhận âm thanh qua mạng.

### 💾 6. Lưu Trữ Bền Vững & Khôi Phục Kết Nối (Reconnection)
- **Auto-Save:** Máy chủ tự động lưu tiến trình ván chơi định kỳ vào CSDL.
- **Host Controls:** Chủ phòng có thể chủ động Lưu ván chơi (`SAVE_GAME`), Tải lại (`LOAD_GAME`), hoặc Chơi lại ván mới (`RESTART_GAME`).
- **Giữ chỗ khi rớt mạng (Grace Period 120s):** Khi người chơi bị ngắt kết nối đột ngột, server giữ nguyên trạng thái ván đấu trong 120 giây và hiển thị hộp thoại gợi ý kết nối lại ngay khi đăng nhập lại.

---

## 🏛️ Kiến Trúc Hệ Thống (3-Tier Architecture)

```
+-------------------------------------------------------------------------+
|                       PRESENTATION LAYER (UNITY 3D)                     |
|  - PuzzleBoard (Mesh & Snapping)          - PuzzleRaycastInteractor     |
|  - PuzzlePiece (20Hz Lerp & Visual Lift)  - RemotePlayerAvatar (Look)   |
|  - PuzzleNetworkManager (Client Socket)   - PuzzleOnlineUIManager (HUD) |
+------------------------------------+------------------------------------+
                                     |
                       [TCP Socket - Custom WireProtocol]
                       Frame: [2B Length] + [UTF-8 JSON]
                                     |
+------------------------------------+------------------------------------+
|                   APPLICATION LAYER (JAVA SERVER CORE)                  |
|  - Packet Router & Dispatcher             - ClientHandler (Non-blocking)|
|  - RoomManager & Host Migration           - GameSession & Validation    |
|  - Pessimistic Locking (ConcurrentMap)    - Real-time Broadcaster       |
|  - AuthService, FriendService, SavedGameService                         |
+------------------------------------+------------------------------------+
                                     |
                                 [JDBC / HikariCP Pool]
                                     |
+------------------------------------+------------------------------------+
|                     DATA STORAGE LAYER (MYSQL 9.4)                      |
|  10 Bảng chuẩn 3NF: accounts, user_profiles, puzzle_catalog,           |
|  game_sessions, session_players, piece_placements, friends,             |
|  friend_messages, chat_channels, shop_items, user_inventory.            |
+-------------------------------------------------------------------------+
```

---

## 🚀 Hướng Dẫn Cài Đặt & Khởi Chạy

### 1. Yêu Cầu Môi Trường
- **Hệ điều hành:** Windows 10 / 11.
- **Java:** JDK 17 trở lên (đã cài đặt biến môi trường `JAVA_HOME` và `PATH`).
- **Cơ sở dữ liệu:** MySQL 8.0 hoặc MySQL 9.4 Server.
- **Game Engine:** Unity Editor 2022.3 LTS (hoặc chạy trực tiếp file build standalone).

---

### 2. Thiết Lập Cơ Sở Dữ Liệu MySQL
1. Mở MySQL Workbench hoặc Command Line Client.
2. Thực thi script khởi tạo cơ sở dữ liệu có sẵn tại `server/schema.sql`:
   ```sql
   source D:/Projects/Puzzle Online/server/schema.sql;
   ```
3. Kiểm tra cấu hình kết nối trong file `server/db.properties`:
   ```properties
   db.url=jdbc:mysql://localhost:3306/puzzle_online_db?useSSL=false&allowPublicKeyRetrieval=true&serverTimezone=UTC
   db.user=root
   db.password=root
   db.pool.size=10
   ```

---

### 3. Khởi Động Java Server
Có 2 cách khởi động máy chủ:

- **Cách 1 (Từ PowerShell):**
  ```powershell
  cd "D:\Projects\Puzzle Online\server"
  powershell -ExecutionPolicy Bypass -File .\run-server.ps1
  ```
- **Cách 2 (Từ Unity Editor):**
  - Mở Unity Editor dự án.
  - Chọn thanh menu: **Tools > Puzzle Online > Start Local Java Server**.

Khi màn hình console hiển thị `Starting server on port 7777 with MySQL backend...`, máy chủ đã sẵn sàng tiếp nhận kết nối.

---

### 4. Khởi Chạy Game Client
- **Trong Unity Editor:**
  - Mở Scene `Assets/Scenes/SCN_MainMenu.unity`.
  - Bấm nút **Play**.
- **Bản Build Standalone:**
  - Mở file thực thi `Puzzle Online.exe` (đã biên dịch sẵn cho Windows 64-bit).
  - Có thể mở nhiều cửa sổ `.exe` cùng lúc để thử nghiệm tính năng nhiều người chơi cùng phòng.

---

## 🎮 Hướng Dẫn Điều Khiển (Game Controls)

| Phím / Thao tác | Chức năng trong Game |
| :--- | :--- |
| **Chuột trái (LMB)** | Nhấp để nhặt mảnh ghép / Giữ kéo rê trên bàn / Thả để hạ hoặc ghép vào khung. |
| **Phím `R`** | Xoay mảnh ghép đang cầm theo góc vuông $90^\circ$. |
| **Cuộn chuột (Scroll Wheel)** | Lăn chuột lên/xuống để xoay nhanh mảnh ghép $90^\circ$. |
| **Di chuột (Mouse Look)** | Xoay góc nhìn camera của nhân vật (đồng bộ xoay đầu Avatar cho người khác thấy). |
| **`W, A, S, D`** | Di chuyển vị trí góc nhìn khi đứng dậy khỏi ghế. |
| **Phím `Tab`** | Bật / tắt con trỏ chuột tự do để tương tác với menu và chat. |
| **Phím `Enter`** | Mở ô nhập nội dung trò chuyện phòng (Chat). |

---

## 👥 Phân Công Nhiệm Vụ Thành Viên (Nhóm PTIT)

| STT | Thành viên | Mã sinh viên | Nhiệm vụ phụ trách chính |
| :---: | :--- | :---: | :--- |
| **1** | **Nguyễn Đình Đạt** | B23DCCN131 | Hệ thống Chat phòng, Kết bạn & Lời mời chơi, Quản lý trạng thái trực tuyến (Presence), Biểu cảm/Voice chat. |
| **2** | **Lê Minh Phương** | B23DCCN663 | Logic Jigsaw Puzzle 3D, bàn chơi chung, thao tác nhặt/kéo/xoay/đặt mảnh, kiểm tra tính hợp lệ trên Server, cơ chế khóa độc quyền (Piece Locking), hiển thị nhấc bổng trên không (Visual Lift), thành chắn viền bàn (Table Boundary Clamp), đồng bộ thời gian thực và tính điểm/tiến độ nhóm. |
| **3** | **Lê Thành Thông** | B23DCCN793 | Quản lý cơ chế lưu trữ bền vững (Persistence), Auto-save phiên chơi, chức năng Save/Resume/Restart của Host, khôi phục trạng thái khi kết nối lại (Reconnection). |
| **4** | **Nguyễn Bá Tú** | B23DCCN873 | Phát triển Java Socket Server lõi, kiến trúc xác thực đăng nhập/đăng ký, quản lý danh sách phòng chơi (Lobby/Rooms), Host Migration và phát hiện ngắt kết nối. |

---

## 📄 Tài Liệu Báo Cáo Đi Kèm

- 📄 **Báo cáo Word hoàn chỉnh:** [`BaoCao_HeThong_PuzzleOnline_Nhom_va_CaNhan.docx`](./BaoCao_HeThong_PuzzleOnline_Nhom_va_CaNhan.docx)
- 📑 **Báo cáo PDF hoàn chỉnh:** [`BaoCao_HeThong_PuzzleOnline_Nhom_va_CaNhan.pdf`](./BaoCao_HeThong_PuzzleOnline_Nhom_va_CaNhan.pdf)
- 📝 **Ghi chú kiến trúc & CSDL chi tiết:** [`notes.md`](./notes.md)
