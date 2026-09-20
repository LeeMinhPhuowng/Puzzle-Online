# Puzzle Online - Cooperative Real-time 3D Jigsaw Puzzle

> Hệ thống trò chơi ghép tranh 3D trực tuyến nhiều người chơi theo thời gian thực (Cooperative Multiplayer).

---

## 1. Tổng Quan Hệ Thống

Puzzle Online là nền tảng trò chơi ghép hình (Jigsaw Puzzle) 3D trực tuyến hỗ trợ nhiều người chơi tương tác đồng thời trong cùng một phòng chơi ảo (Cooperative Gameplay). Hệ thống được thiết kế theo mô hình 3-Tier Enterprise kết hợp kiến trúc Client-Server Authoritative, bảo đảm tính toàn vẹn dữ liệu, độ trễ thấp và ngăn ngừa xung đột thao tác (Race Conditions) bằng cơ chế khóa mảnh ghép độc quyền phân tán (Pessimistic Piece Locking).

---

## 2. Công Nghệ Sử Dụng (Tech Stack)

| Thành phần | Công nghệ / Thư viện | Vai trò và đặc điểm |
| :--- | :--- | :--- |
| Client Engine | Unity 2022.3+ (URP), C# | Render 3D thời gian thực, Camera Raycast tương tác, UI Canvas, First-Person Controller. |
| Game Server | Java SE 17+ Socket Server | Server Authoritative lõi, định tuyến gói tin đa luồng, kiểm tra thẩm định snap, broadcast thời gian thực. |
| Database | MySQL 8.0+ / 9.4 | Lưu trữ bền vững 10 bảng theo chuẩn 3NF (Accounts, Sessions, Placements, Friends,...), bảo đảm ACID. |
| Connection Pool | HikariCP / Custom Pool | Tối ưu hóa truy vấn cơ sở dữ liệu, tái sử dụng kết nối. |
| Giao thức mạng | Custom TCP Wire Protocol | Đóng gói nhị phân Length-Prefixed ([2 bytes Length] + [UTF-8 JSON]), TcpNoDelay = true. |
| Bảo mật | SHA-256 + Salt ngẫu nhiên | Băm mật khẩu người dùng nhiều lớp, chống tấn công Rainbow Table. |

---

## 3. Các Tính Năng Chính

### 3.1. Tương tác 3D và Đồng bộ Thời gian thực (Real-time Sync 20Hz)
- Tương tác trực quan: Nhặt mảnh ghép (Pick), kéo rê bám mặt phẳng bàn (Drag), xoay vuông góc 90 độ (phím R hoặc cuộn chuột) và ghép vào khung (Snap).
- Đồng bộ nhẹ và mượt: Server phát sóng gói tin PIECE_MOVED ở tần số 20Hz; Client sử dụng nội suy Vector3.Lerp và Quaternion.Slerp giúp chuyển động lướt êm trên mọi máy.
- Đồng bộ thả tự do: Khi người chơi thả mảnh ở bất kỳ vị trí nào trên bàn (không snap), tọa độ và góc xoay được cập nhật tức thì cho toàn bộ phòng chơi.

### 3.2. Khóa Độc Quyền (Pessimistic Piece Locking) và Hiệu ứng Nhấc Mảnh
- Chống xung đột đa người chơi: Khi một người nhặt mảnh, Server khóa mảnh và phát tin tới các client khác. Người khác không thể tương tác vào mảnh đang bị giữ.
- Hiệu ứng nhấc bổng trên không: Khi người chơi A nhấc mảnh, các người chơi khác lập tức nhìn thấy mảnh bay lên độ cao 3.5cm (liftHeight = 0.035m) và đổi ánh màu cảnh báo. Mảnh hạ xuống mặt bàn khi nhả chuột.

### 3.3. Giới hạn Viền Bàn Chơi (Table Boundary Clamp & Colliders)
- Clamp tọa độ di chuyển: Hàm ClampToTableBounds() giữ con trỏ mảnh ghép luôn trượt trong phạm vi mặt bàn, ngăn không cho mảnh rơi khỏi bàn.
- Thành chắn vô hình: Tự động khởi tạo 4 BoxCollider vô hình bao quanh các mép bàn để cản vật lý.

### 3.4. Không Gian Phòng Chơi 3D (Environments / Themes)
- Hỗ trợ nhiều không gian phòng chơi: Thiên nhiên ngoài trời (SCN_Nature), Lớp học (SCN_Classroom), Vườn hoa anh đào (SCN_Sakura).
- Tự động xếp chỗ ngồi (1/4 Chair Positions): Người chơi tham gia phòng được tự động định vị vào 4 vị trí ghế ngồi quanh bàn và hướng mắt vào trung tâm bàn cờ.

### 3.5. Giao Tiếp và Bạn Bè
- Chat phòng và biểu cảm Emoji thời gian thực.
- Hệ thống bạn bè: Gửi, chấp nhận, từ chối lời mời kết bạn; theo dõi trạng thái hoạt động (Online, In-Room, Offline).
- Voice chat tích hợp truyền nhận âm thanh qua mạng.

### 3.6. Lưu Trữ Bền Vững và Khôi Phục Kết Nối (Reconnection)
- Tự động lưu tiến trình ván chơi (Auto-save) định kỳ vào CSDL.
- Quyền của Chủ phòng (Host): Chủ động Lưu ván chơi (SAVE_GAME), Tải lại (LOAD_GAME), hoặc Tạo ván mới (RESTART_GAME).
- Giữ chỗ khi mất kết nối (Grace Period 120s): Server duy trì trạng thái phòng trong 120 giây và hỗ trợ hộp thoại hỏi kết nối lại ngay khi đăng nhập.

---

## 4. Kiến Trúc Hệ Thống (3-Tier Architecture)

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
+------------------------------------+------------------------------------+
```

---

## 5. Hướng Dẫn Cài Đặt và Khởi Chạy

### 5.1. Yêu cầu hệ thống
- Windows 10 hoặc Windows 11.
- Java JDK 17 trở lên (đã cấu hình biến môi trường JAVA_HOME và PATH).
- MySQL Server 8.0 hoặc 9.4.
- Unity Editor 2022.3 LTS hoặc bản build chạy trực tiếp.

### 5.2. Thiết lập Cơ sở dữ liệu
1. Mở MySQL Client hoặc Workbench.
2. Chạy script khởi tạo CSDL tại file `server/schema.sql`:
   ```sql
   source D:/Projects/Puzzle Online/server/schema.sql;
   ```
3. Cấu hình thông số kết nối tại file `server/db.properties`:
   ```properties
   db.url=jdbc:mysql://localhost:3306/puzzle_online_db?useSSL=false&allowPublicKeyRetrieval=true&serverTimezone=UTC
   db.user=root
   db.password=root
   db.pool.size=10
   ```

### 5.3. Khởi động Java Server
Có thể khởi động theo 1 trong 2 cách:
- Cách 1 (PowerShell):
  ```powershell
  cd "D:\Projects\Puzzle Online\server"
  powershell -ExecutionPolicy Bypass -File .\run-server.ps1
  ```
- Cách 2 (Unity Editor):
  Chọn menu: Tools > Puzzle Online > Start Local Java Server.

### 5.4. Khởi chạy Game Client
- Trong Unity Editor: Mở scene `Assets/Scenes/SCN_MainMenu.unity` và bấm nút Play.
- Bản Build Standalone: Chạy file thực thi `Puzzle Online.exe`.

---

## 6. Hướng Dẫn Điều Khiển (Controls)

| Phím / Thao tác | Chức năng |
| :--- | :--- |
| Chuột trái (LMB) | Nhặt mảnh ghép / Giữ kéo rê trên bàn / Thả để hạ xuống hoặc ghép vào khung. |
| Phím R | Xoay mảnh ghép đang cầm 90 độ. |
| Cuộn chuột (Scroll Wheel) | Lăn chuột lên/xuống để xoay nhanh mảnh ghép 90 độ. |
| Di chuột (Mouse Look) | Xoay góc nhìn camera (đồng bộ hướng đầu Avatar cho người chơi khác thấy). |
| W, A, S, D | Di chuyển vị trí khi đứng dậy khỏi ghế. |
| Phim Tab | Bật / tắt con trỏ chuột tự do để tương tác với menu và chat. |
| Phím Enter | Mở ô nhập nội dung chat phòng. |

---

## 7. Tài Liệu Kỹ Thuật
- Ghi chú kiến trúc và thiết kế cơ sở dữ liệu: `notes.md`
- Kịch bản khởi tạo CSDL 10 bảng: `server/schema.sql`
