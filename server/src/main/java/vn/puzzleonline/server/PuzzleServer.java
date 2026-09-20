package vn.puzzleonline.server;

import java.io.*;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.nio.file.AtomicMoveNotSupportedException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.time.Instant;
import java.util.*;
import java.util.concurrent.*;
import java.util.regex.Pattern;
import java.util.stream.Collectors;

import vn.puzzleonline.server.db.DatabaseManager;
import vn.puzzleonline.server.model.Models;
import vn.puzzleonline.server.repository.AccountRepository;
import vn.puzzleonline.server.repository.FriendRepository;
import vn.puzzleonline.server.repository.PuzzleRepository;
import vn.puzzleonline.server.repository.SavedGameRepository;
import vn.puzzleonline.server.service.AuthService;
import vn.puzzleonline.server.service.FriendService;
import vn.puzzleonline.server.service.SessionManager;
import vn.puzzleonline.server.service.StateMigrationHelper;

/** Authoritative multiplayer server for Puzzle Together. Uses only the Java standard library. */
public final class PuzzleServer {
    private static final Pattern USERNAME = Pattern.compile("[A-Za-z0-9_]{3,20}");
    private static final long LOCK_TIMEOUT_MS = 12_000;
    private static final long RECONNECT_GRACE_MS = 120_000;
    private static final long AUTO_SAVE_MS = 15_000;

    private final Object stateLock = new Object();
    private final int port;
    private final Path dataDirectory;
    private final Path stateFile;
    private final Map<String, ClientHandler> online = new HashMap<>();
    private final Map<String, Room> rooms = new LinkedHashMap<>();
    private final ScheduledExecutorService scheduler = Executors.newScheduledThreadPool(2);
    private final ExecutorService clientPool = Executors.newCachedThreadPool();
    private final SecureRandom random = new SecureRandom();
    private volatile boolean running = true;
    private ServerData data;

    // Database & 3-Tier Enterprise Services
    private final DatabaseManager db;
    private final AccountRepository accountRepo;
    private final FriendRepository friendRepo;
    private final PuzzleRepository puzzleRepo;
    private final SavedGameRepository savedGameRepo;
    private final AuthService authService;
    private final FriendService friendService;
    private final SessionManager sessionManager;
    private final Map<String, Account> activeAccounts = new ConcurrentHashMap<>();

    private static final LinkedHashMap<String, PuzzleDefinition> PUZZLES = new LinkedHashMap<>();
    private static final LinkedHashMap<String, ThemeDefinition> THEMES = new LinkedHashMap<>();

    static {
        PUZZLES.put("sunset_3x3", new PuzzleDefinition("sunset_3x3", "Hoàng hôn", 3, 3, 300, 0));
        PUZZLES.put("forest_4x3", new PuzzleDefinition("forest_4x3", "Rừng xanh", 3, 4, 420, 160));
        PUZZLES.put("ocean_4x4", new PuzzleDefinition("ocean_4x4", "Đại dương", 4, 4, 600, 240));
        THEMES.put("SCN_Nature", new ThemeDefinition("SCN_Nature", "Thiên nhiên xanh mát", 0));
        THEMES.put("SCN_Classroom", new ThemeDefinition("SCN_Classroom", "Phòng học cổ điển", 150));
        THEMES.put("SCN_Sakura", new ThemeDefinition("SCN_Sakura", "Vườn hoa anh đào", 200));
    }

    private PuzzleServer(int port, Path dataDirectory) {
        this.port = port;
        this.dataDirectory = dataDirectory;
        this.stateFile = dataDirectory.resolve("state.bin");
        DatabaseManager.initialize(null);
        this.db = DatabaseManager.getInstance();
        this.accountRepo = new AccountRepository(db);
        this.friendRepo = new FriendRepository(db);
        this.puzzleRepo = new PuzzleRepository(db);
        this.savedGameRepo = new SavedGameRepository(db);
        this.authService = new AuthService(accountRepo);
        this.friendService = new FriendService(friendRepo, accountRepo);
        this.sessionManager = new SessionManager();
        this.data = loadState();
        StateMigrationHelper.checkAndMigrate(stateFile, db, accountRepo);
    }

    public static void main(String[] args) throws Exception {
        int port = 7777;
        Path dataPath = Path.of("server-data");
        for (String argument : args) {
            if (argument.startsWith("--port=")) port = Integer.parseInt(argument.substring(7));
            if (argument.startsWith("--data=")) dataPath = Path.of(argument.substring(7));
        }
        PuzzleServer server = new PuzzleServer(port, dataPath.toAbsolutePath().normalize());
        Runtime.getRuntime().addShutdownHook(new Thread(server::shutdown, "puzzle-shutdown"));
        server.start();
    }

    private void start() throws IOException {
        Files.createDirectories(dataDirectory);
        scheduler.scheduleAtFixedRate(this::tickSafely, 1, 1, TimeUnit.SECONDS);
        scheduler.scheduleAtFixedRate(this::saveSafely, 10, 10, TimeUnit.SECONDS);
        try (ServerSocket serverSocket = new ServerSocket(port)) {
            System.out.println("Puzzle Together server listening on 0.0.0.0:" + port);
            System.out.println("Persistent data: " + stateFile);
            while (running) {
                Socket socket = serverSocket.accept();
                socket.setTcpNoDelay(true);
                socket.setKeepAlive(true);
                clientPool.execute(new ClientHandler(socket));
            }
        }
    }

    private void shutdown() {
        if (!running) return;
        running = false;
        saveSafely();
        scheduler.shutdownNow();
        clientPool.shutdownNow();
    }

    private void handle(ClientHandler client, Wire.Message message) {
        synchronized (stateLock) {
            try {
                switch (message.type) {
                    case "REGISTER" -> register(client, message);
                    case "LOGIN" -> login(client, message);
                    case "RESUME" -> resume(client, message);
                    case "PING" -> client.send("PONG", "time", Instant.now().toEpochMilli());
                    default -> {
                        if (!requireAuth(client)) return;
                        dispatchAuthenticated(client, message);
                    }
                }
            } catch (RuntimeException exception) {
                exception.printStackTrace();
                client.error("SERVER_ERROR", "Server không thể xử lý thao tác này.");
            }
        }
    }

    private void dispatchAuthenticated(ClientHandler client, Wire.Message message) {
        switch (message.type) {
            case "LOBBY_GET" -> sendLobby(client);
            case "SHOP_LIST" -> sendShop(client);
            case "FRIEND_ADD", "FRIEND_REQUEST_SEND" -> friendRequestSend(client, message);
            case "FRIEND_REQUEST_LIST" -> friendRequestList(client);
            case "FRIEND_REQUEST_ACCEPT" -> friendRequestAccept(client, message);
            case "FRIEND_REQUEST_REJECT" -> friendRequestReject(client, message);
            case "FRIEND_REQUEST_CANCEL" -> friendRequestCancel(client, message);
            case "CREATE_ROOM" -> createRoom(client, message);
            case "JOIN_ROOM" -> joinRoom(client, message.get("roomId"), message.get("password"), false);
            case "LEAVE_ROOM" -> leaveRoom(client, true);
            case "INVITE" -> invite(client, message);
            case "INVITE_RESPONSE" -> inviteResponse(client, message);
            case "READY" -> ready(client, message);
            case "SELECT_PUZZLE" -> selectPuzzle(client, message);
            case "SELECT_THEME" -> selectTheme(client, message);
            case "SELECT_TIME_LIMIT" -> selectTimeLimit(client, message);
            case "START_GAME" -> startGame(client);
            case "GAME_GET" -> sendCurrentGame(client);
            case "CHAT" -> chat(client, message);
            case "EMOJI" -> emoji(client, message);
            case "VOICE" -> voice(client, message);
            case "LOCK_PIECE" -> lockPiece(client, message);
            case "MOVE_PIECE" -> movePiece(client, message);
            case "ROTATE_PIECE" -> rotatePiece(client, message);
            case "PLACE_PIECE" -> placePiece(client, message);
            case "RELEASE_PIECE" -> releasePiece(client, message);
            case "SAVE_GAME" -> saveGameCommand(client, message);
            case "LOAD_GAME" -> loadGame(client);
            case "RESTART_GAME" -> restartGame(client);
            case "CONTINUE_GAME" -> continueGame(client);
            case "BUY" -> buy(client, message);
            case "EQUIP_THEME" -> equipTheme(client, message);
            case "LOOK" -> playerLook(client, message);
            case "RECONNECT_DECISION" -> handleReconnectDecision(client, message);
            case "LOGOUT" -> logout(client);
            default -> client.error("UNKNOWN_COMMAND", "Lệnh không được hỗ trợ: " + message.type);
        }
    }

    private void register(ClientHandler client, Wire.Message message) {
        String username = message.get("username").trim();
        String password = message.get("password");
        if (!USERNAME.matcher(username).matches()) {
            client.error("INVALID_USERNAME", "Tên tài khoản cần 3-20 ký tự chữ, số hoặc dấu gạch dưới.");
            return;
        }
        if (password.length() < 4 || password.length() > 64) {
            client.error("INVALID_PASSWORD", "Mật khẩu cần từ 4 đến 64 ký tự.");
            return;
        }
        var result = authService.register(username, password);
        if (!result.success()) {
            client.error(result.errorCode(), result.message());
            return;
        }
        Account account = createRuntimeAccount(result.account());
        authenticate(client, account, false);
    }

    private void login(ClientHandler client, Wire.Message message) {
        String username = message.get("username").trim();
        String password = message.get("password");
        var result = authService.login(username, password);
        if (!result.success()) {
            client.error(result.errorCode(), result.message());
            return;
        }
        Account account = createRuntimeAccount(result.account());
        authenticate(client, account, false);
    }

    private void resume(ClientHandler client, Wire.Message message) {
        String token = message.get("token");
        if (token == null || token.isBlank()) {
            client.error("SESSION_INVALID", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
            return;
        }
        Long accountId = sessionManager.getAccountId(token);
        if (accountId == null) {
            client.error("SESSION_INVALID", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
            return;
        }
        var opt = accountRepo.findById(accountId);
        if (opt.isEmpty()) {
            client.error("SESSION_INVALID", "Không tìm thấy tài khoản.");
            return;
        }
        Account account = createRuntimeAccount(opt.get());
        authenticate(client, account, true);
    }

    private void authenticate(ClientHandler client, Account account, boolean resumed) {
        ClientHandler old = online.get(account.username.toLowerCase(Locale.ROOT));
        if (old != null && old != client) old.closeSilently();
        account.sessionToken = sessionManager.createSession(account.id, account.username);
        client.account = account;
        online.put(account.username.toLowerCase(Locale.ROOT), client);
        Room room = roomOf(account);
        if (room == null) {
            for (Room r : rooms.values()) {
                if (r.members.containsKey(account.username)) {
                    room = r;
                    break;
                }
            }
        }
        boolean hasPendingReconnect = false;
        if (room != null && room.members.containsKey(account.username)) {
            // Không tự động đưa vào phòng ngay, cho người chơi ở sảnh và gửi RECONNECT_PROMPT
            account.status = "IDLE";
            hasPendingReconnect = true;
            account.roomId = "";
        } else {
            account.roomId = "";
            account.status = "IDLE";
        }
        client.send(resumed ? "RESUME_OK" : "AUTH_OK", "username", account.username,
                "token", account.sessionToken, "coins", account.coins, "theme", account.equippedTheme);
        sendShop(client);
        sendLobby(client);
        if (hasPendingReconnect && room != null) {
            long connectedCount = room.members.values().stream().filter(m -> m.connected).count();
            client.send("RECONNECT_PROMPT",
                    "roomId", room.id,
                    "roomName", room.name,
                    "count", connectedCount,
                    "max", room.maxPlayers,
                    "state", room.state,
                    "puzzleId", room.puzzleId);
        }
        broadcastLobbyAll();
    }

    private void refreshAccountFriends(Account account) {
        if (account == null) return;
        account.friends.clear();
        account.friends.addAll(friendRepo.getFriendUsernames(account.id));
    }

    private void friendRequestSend(ClientHandler client, Wire.Message message) {
        String targetUsername = message.get("username").trim();
        FriendService.FriendResult result = friendService.sendFriendRequest(client.account.id, targetUsername);
        switch (result.code()) {
            case SUCCESS -> {
                client.send("FRIEND_REQUEST_SENT", "receiver", result.receiverUsername(), "requestId", result.requestId());
                ClientHandler targetClient = online.get(result.receiverUsername().toLowerCase(Locale.ROOT));
                if (targetClient != null) {
                    targetClient.send("FRIEND_REQUEST_RECEIVED", "sender", client.account.username, "requestId", result.requestId());
                    friendRequestList(targetClient);
                }
            }
            case AUTO_ACCEPTED -> {
                client.send("FRIEND_REQUEST_AUTO_ACCEPTED", "receiver", result.receiverUsername());
                refreshAccountFriends(client.account);
                sendLobby(client);
                ClientHandler targetClient = online.get(result.receiverUsername().toLowerCase(Locale.ROOT));
                if (targetClient != null) {
                    refreshAccountFriends(targetClient.account);
                    sendLobby(targetClient);
                }
            }
            case REQUEST_ALREADY_PENDING -> client.error("REQUEST_ALREADY_PENDING", result.message());
            case ALREADY_FRIENDS -> client.error("ALREADY_FRIENDS", result.message());
            case CANNOT_FRIEND_SELF -> client.error("CANNOT_FRIEND_SELF", result.message());
            case USER_NOT_FOUND -> client.error("FRIEND_NOT_FOUND", result.message());
            default -> client.error("FRIEND_ERROR", result.message());
        }
    }

    private void friendRequestList(ClientHandler client) {
        List<Models.FriendRequest> list = friendService.getPendingRequests(client.account.id);
        List<List<?>> rows = new ArrayList<>();
        for (Models.FriendRequest fr : list) {
            rows.add(List.of(fr.id(), fr.senderId(), fr.senderUsername(), fr.createdAt() != null ? fr.createdAt().toString() : ""));
        }
        client.send("FRIEND_REQUEST_LIST", "requests", Wire.pack(rows));
    }

    private void friendRequestAccept(ClientHandler client, Wire.Message message) {
        long requestId = message.integer("requestId", 0);
        if (requestId <= 0) {
            client.error("INVALID_REQUEST_ID", "Mã lời mời không hợp lệ.");
            return;
        }
        boolean ok = friendService.acceptFriendRequest(client.account.id, requestId);
        if (!ok) {
            client.error("ACCEPT_FAILED", "Không thể chấp nhận lời mời (lời mời có thể đã hết hạn hoặc không tồn tại).");
            return;
        }
        client.send("FRIEND_REQUEST_ACCEPTED", "requestId", requestId);
        refreshAccountFriends(client.account);
        sendLobby(client);
        friendRequestList(client);
        broadcastLobbyAll();
    }

    private void friendRequestReject(ClientHandler client, Wire.Message message) {
        long requestId = message.integer("requestId", 0);
        if (requestId <= 0) {
            client.error("INVALID_REQUEST_ID", "Mã lời mời không hợp lệ.");
            return;
        }
        boolean ok = friendService.rejectFriendRequest(client.account.id, requestId);
        if (!ok) {
            client.error("REJECT_FAILED", "Không thể từ chối lời mời.");
            return;
        }
        client.send("FRIEND_REQUEST_REJECTED", "requestId", requestId);
        friendRequestList(client);
    }

    private void friendRequestCancel(ClientHandler client, Wire.Message message) {
        long requestId = message.integer("requestId", 0);
        if (requestId <= 0) {
            client.error("INVALID_REQUEST_ID", "Mã lời mời không hợp lệ.");
            return;
        }
        boolean ok = friendService.cancelFriendRequest(client.account.id, requestId);
        if (!ok) {
            client.error("CANCEL_FAILED", "Không thể hủy lời mời.");
            return;
        }
        client.send("FRIEND_REQUEST_CANCELLED", "requestId", requestId);
    }

    private void createRoom(ClientHandler client, Wire.Message message) {
        if (!client.account.roomId.isBlank()) {
            client.error("ALREADY_IN_ROOM", "Bạn đang ở trong một phòng khác.");
            return;
        }
        String name = cleanText(message.get("name"), 32);
        if (name.isBlank()) name = "Phòng của " + client.account.username;
        int maxPlayers = Math.max(2, Math.min(4, message.integer("max", 4)));
        String puzzleId = ownedPuzzleOrDefault(client.account, message.get("puzzleId"));
        String themeId = client.account.equippedTheme != null && THEMES.containsKey(client.account.equippedTheme) ? client.account.equippedTheme : "SCN_Nature";
        String requestedTheme = message.get("themeId");
        if (requestedTheme != null && THEMES.containsKey(requestedTheme) && client.account.ownedThemes.contains(requestedTheme)) {
            themeId = requestedTheme;
        }
        String password = cleanText(message.get("password"), 32);
        int targetTime = message.integer("targetTime", 300);
        Room room = new Room();
        room.id = uniqueRoomId();
        room.name = name;
        room.host = client.account.username;
        room.maxPlayers = maxPlayers;
        room.puzzleId = puzzleId;
        room.themeId = themeId;
        room.password = password != null ? password.trim() : "";
        room.targetTimeSeconds = Math.max(0, Math.min(7200, targetTime));
        room.saveOwner = client.account.username;
        room.hasSave = !savedGames(client.account.username).isEmpty();
        room.members.put(client.account.username, new Member(client.account.username));
        rooms.put(room.id, room);
        enterRoom(client, room);
        broadcastRoom(room);
        broadcastLobbyAll();
    }

    private void joinRoom(ClientHandler client, String requestedId, String password, boolean bypassPassword) {
        if (requestedId == null) return;
        String id = requestedId.trim().toUpperCase(Locale.ROOT);
        Room room = rooms.get(id);
        if (room == null) {
            client.error("ROOM_NOT_FOUND", "Không tìm thấy phòng với mã " + id + ".");
            return;
        }
        Member existing = room.members.get(client.account.username);
        if (existing == null && !"WAITING".equals(room.state)) {
            client.error("GAME_STARTED", "Ván chơi đã bắt đầu.");
            return;
        }
        if (existing == null && room.members.size() >= room.maxPlayers) {
            client.error("ROOM_FULL", "Phòng đã đủ người.");
            return;
        }
        if (existing == null && room.isPrivate() && !bypassPassword) {
            String supplied = password != null ? password.trim() : "";
            if (supplied.isEmpty()) {
                client.send("ERROR", "code", "PASSWORD_REQUIRED", "message", "Phòng này yêu cầu mật khẩu.", "roomId", room.id, "roomName", room.name);
                return;
            }
            if (!room.password.equals(supplied)) {
                client.send("ERROR", "code", "WRONG_PASSWORD", "message", "Mật khẩu phòng không đúng.", "roomId", room.id, "roomName", room.name);
                return;
            }
        }
        if (!client.account.roomId.isBlank() && !client.account.roomId.equals(room.id)) leaveRoom(client, false);
        if (existing == null) room.members.put(client.account.username, new Member(client.account.username));
        else { existing.connected = true; existing.lastSeen = System.currentTimeMillis(); }
        enterRoom(client, room);
        broadcastPlayerEvent(room, client.account.username + " đã tham gia phòng.");
        broadcastRoom(room);
        if (room.game != null) broadcastGame(room);
        broadcastLobbyAll();
    }

    private void enterRoom(ClientHandler client, Room room) {
        client.account.roomId = room.id;
        client.account.status = room.game == null ? "IN_ROOM" : "PLAYING";
    }

    private void leaveRoom(ClientHandler client, boolean notifySelf) {
        Room room = roomOf(client.account);
        if (room == null) {
            client.account.roomId = "";
            client.account.status = "IDLE";
            if (notifySelf) client.send("ROOM_LEFT");
            return;
        }
        releaseLocksFor(room, client.account.username);
        room.members.remove(client.account.username);
        client.account.roomId = "";
        client.account.status = "IDLE";
        if (notifySelf) client.send("ROOM_LEFT");
        if (room.members.isEmpty()) {
            rooms.remove(room.id);
        } else {
            if (room.host.equals(client.account.username)) {
                room.host = room.members.keySet().iterator().next();
                room.saveOwner = room.host;
            }
            broadcastPlayerEvent(room, client.account.username + " đã rời phòng. " + room.host + " đang là chủ phòng.");
            broadcastRoom(room);
            if (room.game != null) broadcastGame(room);
        }
        broadcastLobbyAll();
    }

    private void invite(ClientHandler client, Wire.Message message) {
        Room room = roomOf(client.account);
        if (room == null) { client.error("NOT_IN_ROOM", "Bạn cần vào phòng trước khi gửi lời mời."); return; }
        String targetName = message.get("username").trim();
        ClientHandler target = online.get(targetName.toLowerCase(Locale.ROOT));
        if (target == null || target.account == null || !"IDLE".equals(target.account.status)) {
            client.error("FRIEND_BUSY", "Người chơi hiện không rảnh để nhận lời mời.");
            return;
        }
        if (!client.account.friends.contains(target.account.username)) {
            client.error("NOT_FRIEND", "Bạn chỉ có thể mời người trong danh sách bạn bè.");
            return;
        }
        target.send("INVITE", "from", client.account.username, "roomId", room.id, "roomName", room.name);
        client.send("PLAYER_EVENT", "message", "Đã gửi lời mời cho " + target.account.username + ".");
    }

    private void inviteResponse(ClientHandler client, Wire.Message message) {
        if (message.bool("accept", false)) joinRoom(client, message.get("roomId"), message.get("password"), true);
        else client.send("PLAYER_EVENT", "message", "Bạn đã từ chối lời mời.");
    }

    private void ready(ClientHandler client, Wire.Message message) {
        Room room = waitingRoom(client);
        if (room == null) return;
        Member member = room.members.get(client.account.username);
        member.ready = message.bool("ready", !member.ready);
        broadcastRoom(room);
    }

    private void selectPuzzle(ClientHandler client, Wire.Message message) {
        Room room = waitingRoom(client);
        if (room == null || !requireHost(client, room)) return;
        String puzzle = message.get("puzzleId");
        if (!PUZZLES.containsKey(puzzle) || !client.account.ownedPuzzles.contains(puzzle)) {
            client.error("PUZZLE_LOCKED", "Bạn chưa sở hữu bộ xếp hình này.");
            return;
        }
        room.puzzleId = puzzle;
        room.members.values().forEach(member -> member.ready = false);
        broadcastRoom(room);
        broadcastLobbyAll();
    }

    private void selectTheme(ClientHandler client, Wire.Message message) {
        Room room = waitingRoom(client);
        if (room == null || !requireHost(client, room)) return;
        String theme = message.get("themeId");
        if (!THEMES.containsKey(theme) || !client.account.ownedThemes.contains(theme)) {
            client.error("THEME_LOCKED", "Bạn chưa sở hữu không gian 3D này.");
            return;
        }
        room.themeId = theme;
        broadcastRoom(room);
        broadcastLobbyAll();
    }

    private void selectTimeLimit(ClientHandler client, Wire.Message message) {
        Room room = waitingRoom(client);
        if (room == null || !requireHost(client, room)) return;
        int seconds = message.integer("seconds", 300);
        room.targetTimeSeconds = Math.max(0, Math.min(7200, seconds));
        broadcastRoom(room);
        String label = room.targetTimeSeconds <= 0 ? "Không giới hạn (Thư giãn)" : (room.targetTimeSeconds / 60 + " phút");
        broadcastPlayerEvent(room, "Chủ phòng đã đặt mốc thời gian mục tiêu: " + label);
        broadcastLobbyAll();
    }

    private void startGame(ClientHandler client) {
        Room room = waitingRoom(client);
        if (room == null || !requireHost(client, room)) return;
        if (room.members.values().stream().anyMatch(member -> !member.ready || !member.connected)) {
            client.error("NOT_READY", "Tất cả thành viên đang kết nối phải sẵn sàng.");
            return;
        }
        room.game = createGame(room);
        room.state = "PLAYING";
        room.members.values().forEach(member -> member.score = 0);
        setRoomStatuses(room, "PLAYING");
        broadcastRoom(room);
        broadcastGame(room);
        broadcastPlayerEvent(room, "Ván chơi đã bắt đầu. Cùng nhau hoàn thành bức tranh!");
        broadcastLobbyAll();
    }

    private GameState createGame(Room room) {
        PuzzleDefinition definition = PUZZLES.getOrDefault(room.puzzleId, PUZZLES.get("sunset_3x3"));
        GameState game = new GameState();
        game.instanceId = UUID.randomUUID().toString();
        game.puzzleId = definition.id;
        game.rows = definition.rows;
        game.columns = definition.columns;
        game.durationSeconds = room.targetTimeSeconds;
        game.deadlineEpochMs = room.targetTimeSeconds > 0 ? System.currentTimeMillis() + room.targetTimeSeconds * 1000L : 0;
        Random seeded = new Random(game.instanceId.hashCode());
        for (int row = 0; row < game.rows; row++) {
            for (int column = 0; column < game.columns; column++) {
                Piece piece = new Piece();
                piece.id = row * game.columns + column;
                piece.row = row;
                piece.column = column;
                piece.x = .08f + seeded.nextFloat() * .84f;
                piece.y = .08f + seeded.nextFloat() * .84f;
                piece.rotation = (1 + seeded.nextInt(3)) * 90;
                game.pieces.add(piece);
            }
        }
        return game;
    }

    private void sendCurrentGame(ClientHandler client) {
        Room room = roomOf(client.account);
        if (room == null || room.game == null) client.error("NO_GAME", "Phòng chưa có ván chơi.");
        else sendGame(client, room);
    }

    private void chat(ClientHandler client, Wire.Message message) {
        Room room = roomOf(client.account);
        if (room == null) { client.error("NOT_IN_ROOM", "Bạn chưa ở trong phòng."); return; }
        String text = cleanText(message.get("text"), 240);
        if (text.isBlank()) return;
        broadcast(room, "CHAT", "from", client.account.username, "text", text, "time", Instant.now().toEpochMilli());
    }

    private void emoji(ClientHandler client, Wire.Message message) {
        Room room = roomOf(client.account);
        if (room == null) return;
        String emoji = cleanText(message.get("emoji"), 12);
        broadcast(room, "EMOJI", "from", client.account.username, "emoji", emoji);
    }

    private void voice(ClientHandler client, Wire.Message message) {
        Room room = roomOf(client.account);
        if (room == null) return;
        String pcm = message.get("pcm");
        if (pcm.length() > 24_000) { client.error("VOICE_TOO_LARGE", "Gói âm thanh quá lớn."); return; }
        for (Member member : room.members.values()) {
            if (member.username.equals(client.account.username)) continue;
            ClientHandler target = online.get(member.username.toLowerCase(Locale.ROOT));
            if (target != null) target.send("VOICE", "from", client.account.username, "pcm", pcm);
        }
    }

    private int extractPieceId(Wire.Message message) {
        if (message.get("pieceId") != null) return message.integer("pieceId", -1);
        if (message.get("id") != null) return message.integer("id", -1);
        return -1;
    }

    private void lockPiece(ClientHandler client, Wire.Message message) {
        Room room = playingRoom(client);
        if (room == null) return;
        Piece piece = piece(room, extractPieceId(message), client);
        if (piece == null) return;
        long now = System.currentTimeMillis();
        if (piece.placed) { client.error("PIECE_PLACED", "Mảnh này đã được đặt đúng."); return; }
        if (!piece.lockedBy.isBlank() && !piece.lockedBy.equals(client.account.username) && piece.lockUntil > now) {
            client.error("PIECE_LOCKED", "Mảnh đang được " + piece.lockedBy + " giữ.");
            return;
        }
        piece.lockedBy = client.account.username;
        piece.lockUntil = now + LOCK_TIMEOUT_MS;
        // Broadcast PIECE_LOCKED ngay lập tức tới tất cả người chơi trong phòng để nâng mảnh và khóa chống xung đột
        broadcast(room, "PIECE_LOCKED", "pieceId", piece.id, "id", piece.id, "by", piece.lockedBy,
                "x", piece.x, "y", piece.y, "rot", piece.rotation);
    }

    private void movePiece(ClientHandler client, Wire.Message message) {
        Room room = playingRoom(client);
        if (room == null) return;
        Piece piece = piece(room, extractPieceId(message), client);
        if (!ownsLock(client, piece)) return;
        piece.x = clamp(message.decimal("x", piece.x), -50f, 50f);
        piece.y = clamp(message.decimal("y", piece.y), -50f, 50f);
        if (message.get("rot") != null) {
            piece.rotation = message.integer("rot", piece.rotation);
        }
        piece.lockUntil = System.currentTimeMillis() + LOCK_TIMEOUT_MS;
        // Phát sóng gói tin nhẹ PIECE_MOVED tới các người chơi khác trong phòng để cập nhật vị trí thời gian thực
        for (Member member : room.members.values()) {
            if (!member.connected) continue;
            if (member.username.equalsIgnoreCase(client.account.username)) continue;
            ClientHandler other = online.get(member.username.toLowerCase(Locale.ROOT));
            if (other != null) {
                other.send("PIECE_MOVED", "pieceId", piece.id, "id", piece.id,
                        "x", piece.x, "y", piece.y, "rot", piece.rotation, "by", client.account.username);
            }
        }
    }

    private void rotatePiece(ClientHandler client, Wire.Message message) {
        Room room = playingRoom(client);
        if (room == null) return;
        Piece piece = piece(room, extractPieceId(message), client);
        if (!ownsLock(client, piece)) return;
        int amount = message.integer("degrees", message.integer("delta", 90));
        piece.rotation = normalizeRotation(piece.rotation + (amount >= 0 ? 90 : -90));
        piece.lockUntil = System.currentTimeMillis() + LOCK_TIMEOUT_MS;
        for (Member member : room.members.values()) {
            if (!member.connected) continue;
            if (member.username.equalsIgnoreCase(client.account.username)) continue;
            ClientHandler other = online.get(member.username.toLowerCase(Locale.ROOT));
            if (other != null) {
                other.send("PIECE_MOVED", "pieceId", piece.id, "id", piece.id,
                        "x", piece.x, "y", piece.y, "rot", piece.rotation, "by", client.account.username);
            }
        }
    }

    private void placePiece(ClientHandler client, Wire.Message message) {
        Room room = playingRoom(client);
        if (room == null) return;
        if (room.game.timedOut) { client.error("GAME_OVER", "Đã hết thời gian. Chủ phòng có thể tải lại hoặc chơi lại."); return; }
        Piece piece = piece(room, extractPieceId(message), client);
        if (!ownsLock(client, piece)) return;
        float targetX = (piece.column + .5f) / room.game.columns;
        float targetY = (piece.row + .5f) / room.game.rows;
        float dx = piece.x - targetX;
        float dy = piece.y - targetY;
        boolean clientPlaced = message.bool("placed", false) || message.bool("snapped", false);
        boolean correct = clientPlaced || (dx * dx + dy * dy <= .08f && normalizeRotation(piece.rotation) == 0);
        if (correct) {
            piece.x = targetX;
            piece.y = targetY;
            piece.placed = true;
            Member member = room.members.get(client.account.username);
            if (member != null) member.score++;
        }
        piece.lockedBy = "";
        piece.lockUntil = 0;

        int placedCount = (int) room.game.pieces.stream().filter(p -> p.placed).count();
        broadcast(room, "PIECE_PLACED", "pieceId", piece.id, "id", piece.id,
                "x", piece.x, "y", piece.y, "rot", piece.rotation, "by", client.account.username,
                "placed", placedCount, "total", room.game.pieces.size());
        broadcastGame(room);
        if (correct && room.game.pieces.stream().allMatch(item -> item.placed)) completeGame(room);
    }

    private void releasePiece(ClientHandler client, Wire.Message message) {
        Room room = playingRoom(client);
        if (room == null) return;
        Piece piece = piece(room, extractPieceId(message), client);
        if (piece != null && piece.lockedBy.equals(client.account.username)) {
            if (message.get("x") != null) {
                piece.x = clamp(message.decimal("x", piece.x), -50f, 50f);
            }
            if (message.get("y") != null) {
                piece.y = clamp(message.decimal("y", piece.y), -50f, 50f);
            }
            if (message.get("rot") != null) {
                piece.rotation = message.integer("rot", piece.rotation);
            }
            piece.lockedBy = "";
            piece.lockUntil = 0;
            // Phát sóng PIECE_RELEASED kèm tọa độ và góc quay mới để mọi client hạ mảnh xuống đúng vị trí
            broadcast(room, "PIECE_RELEASED", "pieceId", piece.id, "id", piece.id,
                    "x", piece.x, "y", piece.y, "rot", piece.rotation);
        }
    }

    private void completeGame(Room room) {
        room.state = "COMPLETE";
        room.game.completed = true;
        setRoomStatuses(room, "IN_ROOM");

        // Người đóng góp nhiều nhất là người đặt được nhiều mảnh đúng nhất.
        // Giữ thứ tự tham gia phòng để chọn ổn định khi hòa điểm.
        Member winner = room.members.values().stream()
                .max(Comparator.comparingInt(member -> member.score))
                .orElse(null);
        String winnerName = winner != null ? winner.username : "";

        for (Member member : room.members.values()) {
            Account account = findAccount(member.username);
            if (account == null) continue;
            int reward = 50 + member.score * 10;
            puzzleRepo.recordCompletion(account.id, room.puzzleId, reward);
            accountRepo.updateScore(account.id, member.score);
            var accOpt = accountRepo.findById(account.id);
            accOpt.ifPresent(a -> account.coins = a.coins());
            ClientHandler client = online.get(member.username.toLowerCase(Locale.ROOT));
            if (client != null) {
                client.send("GAME_COMPLETE", "winner", winnerName, "bonus", reward,
                        "coins", account.coins, "score", member.score);
            }
        }
        saveGame(room, "Hoàn thành " + Instant.now(), false);
        broadcastGame(room);
        saveSafely();
    }

    private void saveGameCommand(ClientHandler client, Wire.Message message) {
        Room room = roomOf(client.account);
        if (room == null || !requireHost(client, room)) return;
        if (room.game == null) { client.error("NO_GAME", "Chưa có tiến trình để lưu."); return; }
        String label = cleanText(message.get("label"), 40);
        if (label.isBlank()) label = "Lưu thủ công";
        SavedGame saved = saveGame(room, label, false);
        client.send("SAVE_CREATED", "saveId", saved.id, "label", saved.label);
    }

    private SavedGame saveGame(Room room, String label, boolean automatic) {
        List<SavedGame> saves = savedGames(room.saveOwner);
        SavedGame save;
        if (automatic) {
            save = saves.stream().filter(item -> item.automatic && item.roomName.equals(room.name)).findFirst().orElse(null);
            if (save == null) { save = new SavedGame(); saves.add(save); }
        } else {
            save = new SavedGame();
            saves.add(save);
        }
        save.id = save.id == null ? UUID.randomUUID().toString() : save.id;
        save.owner = room.saveOwner;
        save.roomName = room.name;
        save.label = automatic ? "Tự động lưu" : label;
        save.createdAt = System.currentTimeMillis();
        save.automatic = automatic;
        save.game = copyGame(room.game, remainingSeconds(room.game));
        save.scores = room.members.values().stream().collect(Collectors.toMap(member -> member.username, member -> member.score));
        room.hasSave = true;
        room.lastAutoSave = System.currentTimeMillis();
        while (saves.size() > 20) saves.remove(0);

        // Persist to MySQL saved_games table (scores & pieces as JSON)
        try {
            Account ownerAcc = findAccount(room.saveOwner);
            if (ownerAcc != null && save.game != null) {
                StringBuilder scoresJson = new StringBuilder("{");
                boolean first = true;
                for (var entry : save.scores.entrySet()) {
                    if (!first) scoresJson.append(",");
                    scoresJson.append("\"").append(entry.getKey()).append("\":").append(entry.getValue());
                    first = false;
                }
                scoresJson.append("}");

                StringBuilder piecesJson = new StringBuilder("[");
                first = true;
                for (Piece p : save.game.pieces) {
                    if (!first) piecesJson.append(",");
                    piecesJson.append(String.format(Locale.ROOT,
                            "{\"id\":%d,\"row\":%d,\"col\":%d,\"x\":%.2f,\"y\":%.2f,\"rot\":%d,\"placed\":%b}",
                            p.id, p.row, p.column, p.x, p.y, p.rotation, p.placed));
                    first = false;
                }
                piecesJson.append("]");

                int elapsed = save.game.durationSeconds > 0 ? Math.max(0, save.game.durationSeconds - remainingSeconds(save.game)) : 0;
                savedGameRepo.saveGame(
                        save.id,
                        ownerAcc.id,
                        room.name,
                        save.label,
                        automatic,
                        room.puzzleId,
                        room.themeId,
                        elapsed,
                        save.game.completed,
                        scoresJson.toString(),
                        piecesJson.toString()
                );
            }
        } catch (Exception e) {
            System.err.println("[SavedGame] Error persisting to MySQL: " + e.getMessage());
        }

        saveSafely();
        return save;
    }

    private void loadGame(ClientHandler client) {
        Room room = roomOf(client.account);
        if (room == null || !requireHost(client, room)) return;
        List<SavedGame> saves = savedGames(room.saveOwner);
        if (saves.isEmpty()) { client.error("NO_SAVE", "Không tìm thấy bản lưu nào của chủ phòng."); return; }
        SavedGame latest = saves.stream().max(Comparator.comparingLong(item -> item.createdAt)).orElseThrow();
        room.game = copyGame(latest.game, latest.game.savedRemainingSeconds);
        room.puzzleId = room.game.puzzleId;
        room.state = room.game.completed ? "COMPLETE" : "PLAYING";
        for (Member member : room.members.values()) member.score = latest.scores.getOrDefault(member.username, 0);
        setRoomStatuses(room, "PLAYING");
        broadcastRoom(room);
        broadcastGame(room);
        broadcastPlayerEvent(room, "Đã tải bản lưu “" + latest.label + "”.");
        broadcastLobbyAll();
    }

    private void restartGame(ClientHandler client) {
        Room room = roomOf(client.account);
        if (room == null || !requireHost(client, room)) return;
        if (room.game != null) saveGame(room, "Bản trước khi chơi lại", false);
        room.game = createGame(room);
        room.state = "PLAYING";
        room.members.values().forEach(member -> member.score = 0);
        setRoomStatuses(room, "PLAYING");
        broadcastRoom(room);
        broadcastGame(room);
        broadcastPlayerEvent(room, "Chủ phòng đã tạo một bản chơi mới. Bản cũ vẫn được lưu.");
        broadcastLobbyAll();
    }

    private void continueGame(ClientHandler client) {
        Room room = roomOf(client.account);
        if (room == null || !requireHost(client, room)) return;
        if (room.game == null) return;
        room.game.timedOut = false;
        room.game.durationSeconds = 0;
        room.game.deadlineEpochMs = 0;
        broadcast(room, "GAME_CONTINUED");
        broadcastPlayerEvent(room, "Chủ phòng đã chọn tiếp tục ván chơi (Không giới hạn thời gian).");
        broadcastGame(room);
    }

    private void buy(ClientHandler client, Wire.Message message) {
        String kind = message.get("kind");
        String id = message.get("id");
        if (kind.equals("puzzle") && PUZZLES.containsKey(id)) {
            if (client.account.ownedPuzzles.contains(id)) { sendShop(client); return; }
            boolean ok = puzzleRepo.buyPuzzle(client.account.id, id);
            if (!ok) {
                client.error("NOT_ENOUGH_COINS", "Bạn không đủ xu hoặc giao dịch không thành công.");
                return;
            }
            var accOpt = accountRepo.findById(client.account.id);
            accOpt.ifPresent(a -> client.account.coins = a.coins());
            client.account.ownedPuzzles.add(id);
        } else if (kind.equals("theme") && THEMES.containsKey(id)) {
            if (client.account.ownedThemes.contains(id)) { sendShop(client); return; }
            boolean ok = puzzleRepo.buyEnvironment(client.account.id, id);
            if (!ok) {
                client.error("NOT_ENOUGH_COINS", "Bạn không đủ xu hoặc giao dịch không thành công.");
                return;
            }
            var accOpt = accountRepo.findById(client.account.id);
            accOpt.ifPresent(a -> client.account.coins = a.coins());
            client.account.ownedThemes.add(id);
        } else {
            client.error("ITEM_NOT_FOUND", "Không tìm thấy vật phẩm.");
            return;
        }
        client.send("PURCHASED", "kind", kind, "id", id, "coins", client.account.coins);
        sendShop(client);
        sendLobby(client);
    }

    private void equipTheme(ClientHandler client, Wire.Message message) {
        String id = message.get("id");
        if (!client.account.ownedThemes.contains(id)) { client.error("THEME_LOCKED", "Bạn chưa sở hữu giao diện này."); return; }
        client.account.equippedTheme = id;
        client.send("THEME_EQUIPPED", "theme", id);
        saveSafely();
    }

    private void logout(ClientHandler client) {
        leaveRoom(client, true);
        sessionManager.invalidateToken(client.account.sessionToken);
        client.account.sessionToken = "";
        client.account.status = "OFFLINE";
        online.remove(client.account.username.toLowerCase(Locale.ROOT));
        client.closeSilently();
        broadcastLobbyAll();
    }

    private void sendLobby(ClientHandler client) {
        if (client.account == null) return;
        List<List<?>> roomRows = new ArrayList<>();
        for (Room room : rooms.values()) {
            if (!"WAITING".equals(room.state)) continue;
            roomRows.add(List.of(room.id, room.name, room.host, room.members.size(), room.maxPlayers,
                    room.state, room.puzzleId, room.hasSave, room.themeId, room.isPrivate(), room.targetTimeSeconds));
        }
        List<List<?>> friendRows = new ArrayList<>();
        for (String friendName : client.account.friends) {
            Account friend = findAccount(friendName);
            if (friend != null) friendRows.add(List.of(friend.username, friend.status, online.containsKey(friend.username.toLowerCase(Locale.ROOT))));
        }
        client.send("LOBBY_SNAPSHOT", "rooms", Wire.pack(roomRows), "friends", Wire.pack(friendRows), "coins", client.account.coins);
    }

    private void sendShop(ClientHandler client) {
        if (client.account == null) return;
        List<List<?>> puzzleRows = new ArrayList<>();
        for (PuzzleDefinition definition : PUZZLES.values())
            puzzleRows.add(List.of(definition.id, definition.name, definition.rows, definition.columns,
                    definition.seconds, definition.price, client.account.ownedPuzzles.contains(definition.id)));
        List<List<?>> themeRows = new ArrayList<>();
        for (ThemeDefinition definition : THEMES.values())
            themeRows.add(List.of(definition.id, definition.name, definition.price,
                    client.account.ownedThemes.contains(definition.id), definition.id.equals(client.account.equippedTheme)));
        client.send("SHOP_LIST", "puzzles", Wire.pack(puzzleRows), "themes", Wire.pack(themeRows), "coins", client.account.coins);
    }

    private void broadcastRoom(Room room) {
        PuzzleDefinition definition = PUZZLES.getOrDefault(room.puzzleId, PUZZLES.get("sunset_3x3"));
        List<List<?>> players = new ArrayList<>();
        for (Member member : room.members.values()) {
            Account account = findAccount(member.username);
            players.add(List.of(member.username, account == null ? "OFFLINE" : account.status, member.ready,
                    member.score, member.connected, member.username.equals(room.host)));
        }
        String packed = Wire.pack(players);
        broadcast(room, "ROOM_SNAPSHOT", "roomId", room.id, "name", room.name, "host", room.host,
                "max", room.maxPlayers, "state", room.state, "puzzleId", room.puzzleId, "themeId", room.themeId,
                "rows", definition.rows, "columns", definition.columns, "hasSave", room.hasSave, "hasPassword", room.isPrivate(),
                "targetTime", room.targetTimeSeconds, "players", packed);
    }

    private void sendGame(ClientHandler client, Room room) {
        GameState game = room.game;
        List<List<?>> pieces = new ArrayList<>();
        int placed = 0;
        for (Piece piece : game.pieces) {
            if (piece.placed) placed++;
            pieces.add(List.of(piece.id, piece.row, piece.column, piece.x, piece.y, piece.rotation, piece.placed, piece.lockedBy));
        }
        List<List<?>> players = new ArrayList<>();
        for (Member member : room.members.values()) {
            Account account = findAccount(member.username);
            players.add(List.of(member.username, account == null ? "OFFLINE" : account.status, member.ready,
                    member.score, member.connected, member.username.equals(room.host)));
        }
        client.send("GAME_SNAPSHOT", "roomId", room.id, "roomName", room.name, "host", room.host,
                "puzzleId", game.puzzleId, "themeId", room.themeId, "rows", game.rows, "columns", game.columns,
                "remaining", remainingSeconds(game), "placed", placed, "total", game.pieces.size(),
                "instanceId", game.instanceId, "hasSave", room.hasSave, "timedOut", game.timedOut,
                "pieces", Wire.pack(pieces), "players", Wire.pack(players));
    }

    private void broadcastGame(Room room) {
        for (Member member : room.members.values()) {
            if (!member.connected) continue;
            ClientHandler client = online.get(member.username.toLowerCase(Locale.ROOT));
            if (client != null) sendGame(client, room);
        }
    }

    private void broadcast(Room room, String type, Object... values) {
        for (Member member : room.members.values()) {
            if (!member.connected) continue;
            ClientHandler client = online.get(member.username.toLowerCase(Locale.ROOT));
            if (client != null) client.send(type, values);
        }
    }

    private void playerLook(ClientHandler client, Wire.Message message) {
        Room room = roomOf(client.account);
        if (room == null) return;
        float yaw = message.decimal("yaw", 0f);
        float pitch = message.decimal("pitch", 0f);
        for (Member member : room.members.values()) {
            if (!member.connected) continue;
            if (member.username.equalsIgnoreCase(client.account.username)) continue;
            ClientHandler other = online.get(member.username.toLowerCase(Locale.ROOT));
            if (other != null) {
                other.send("PLAYER_LOOK", "username", client.account.username, "yaw", yaw, "pitch", pitch);
            }
        }
    }

    private void handleReconnectDecision(ClientHandler client, Wire.Message message) {
        boolean accept = message.bool("accept", false);
        String roomId = message.get("roomId");
        Room room = rooms.get(roomId);
        if (!accept) {
            // Người chơi chọn ở lại sảnh, hủy kết nối phòng cũ
            if (room != null) {
                room.members.remove(client.account.username);
                if (room.members.isEmpty()) {
                    rooms.remove(room.id);
                } else {
                    if (room.host.equalsIgnoreCase(client.account.username)) {
                        room.host = room.members.keySet().iterator().next();
                    }
                    broadcastRoom(room);
                }
            }
            client.account.roomId = "";
            client.account.status = "IDLE";
            sendLobby(client);
            broadcastLobbyAll();
            saveSafely();
            return;
        }

        // Người chơi chọn quay lại phòng
        if (room == null) {
            client.account.roomId = "";
            client.account.status = "IDLE";
            client.error("ROOM_NOT_FOUND", "Phòng trước đó đã kết thúc hoặc không còn tồn tại.");
            sendLobby(client);
            return;
        }

        // Kiểm tra xem phòng có bị đầy không (đếm số thành viên khác đang kết nối)
        long otherConnected = room.members.values().stream()
                .filter(m -> m.connected && !m.username.equalsIgnoreCase(client.account.username))
                .count();
        if (otherConnected >= room.maxPlayers) {
            client.account.roomId = "";
            client.account.status = "IDLE";
            room.members.remove(client.account.username);
            client.error("ROOM_FULL", "Phòng đã đầy người (" + otherConnected + "/" + room.maxPlayers + ")! Không thể quay lại phòng.");
            sendLobby(client);
            broadcastLobbyAll();
            saveSafely();
            return;
        }

        // Kết nối lại vào phòng
        Member member = room.members.get(client.account.username);
        if (member == null) {
            member = new Member(client.account.username);
            room.members.put(client.account.username, member);
        }
        member.connected = true;
        member.lastSeen = System.currentTimeMillis();
        client.account.roomId = room.id;
        client.account.status = room.game == null ? "IN_ROOM" : "PLAYING";

        broadcastPlayerEvent(room, client.account.username + " đã kết nối lại phòng.");
        broadcastRoom(room);
        if (room.game != null) {
            broadcastGame(room);
        }
        broadcastLobbyAll();
    }

    private void broadcastPlayerEvent(Room room, String message) {
        broadcast(room, "PLAYER_EVENT", "message", message);
    }

    private void broadcastLobbyAll() {
        for (ClientHandler client : new ArrayList<>(online.values())) sendLobby(client);
    }

    private void tickSafely() {
        try {
            synchronized (stateLock) {
                long now = System.currentTimeMillis();
                List<Room> roomSnapshot = new ArrayList<>(rooms.values());
                for (Room room : roomSnapshot) {
                    boolean changed = false;
                    if (room.game != null) {
                        for (Piece piece : room.game.pieces) {
                            if (!piece.lockedBy.isBlank() && piece.lockUntil <= now) {
                                piece.lockedBy = "";
                                piece.lockUntil = 0;
                                changed = true;
                            }
                        }
                        if (room.game.durationSeconds > 0 && !room.game.completed && !room.game.timedOut && remainingSeconds(room.game) <= 0) {
                            room.game.timedOut = true;
                            broadcast(room, "GAME_TIMEOUT");
                            broadcastPlayerEvent(room, "Đã hết thời gian mục tiêu! Chủ phòng có thể chọn tiếp tục hoặc bắt đầu lại.");
                            changed = true;
                        }
                        if (!room.game.completed && !room.game.timedOut && now - room.lastAutoSave >= AUTO_SAVE_MS)
                            saveGame(room, "Tự động lưu", true);
                        broadcastGame(room); // also keeps every client's countdown aligned with the server
                    } else if (changed) broadcastRoom(room);

                    List<String> expired = room.members.values().stream()
                            .filter(member -> !member.connected && now - member.lastSeen > RECONNECT_GRACE_MS)
                            .map(member -> member.username).toList();
                    for (String username : expired) removeExpiredMember(room, username);
                }
            }
        } catch (Exception exception) {
            exception.printStackTrace();
        }
    }

    private void removeExpiredMember(Room room, String username) {
        room.members.remove(username);
        Account account = findAccount(username);
        if (account != null) account.roomId = "";
        if (room.members.isEmpty()) rooms.remove(room.id);
        else {
            if (room.host.equals(username)) room.host = room.members.keySet().iterator().next();
            broadcastPlayerEvent(room, username + " đã hết thời gian kết nối lại và rời phòng.");
            broadcastRoom(room);
            if (room.game != null) broadcastGame(room);
        }
        broadcastLobbyAll();
    }

    private void disconnected(ClientHandler client) {
        synchronized (stateLock) {
            if (client.account == null) return;
            ClientHandler current = online.get(client.account.username.toLowerCase(Locale.ROOT));
            if (current != client) return;
            online.remove(client.account.username.toLowerCase(Locale.ROOT));
            client.account.status = "OFFLINE";
            Room room = roomOf(client.account);
            if (room != null) {
                Member member = room.members.get(client.account.username);
                if (member != null) { member.connected = false; member.lastSeen = System.currentTimeMillis(); }
                releaseLocksFor(room, client.account.username);
                broadcastPlayerEvent(room, client.account.username + " mất kết nối. Mảnh đang giữ đã được mở khóa.");
                broadcastRoom(room);
                if (room.game != null) broadcastGame(room);
            }
            broadcastLobbyAll();
        }
    }

    private void releaseLocksFor(Room room, String username) {
        if (room.game == null) return;
        for (Piece piece : room.game.pieces) {
            if (piece.lockedBy.equals(username)) { piece.lockedBy = ""; piece.lockUntil = 0; }
        }
    }

    private Room roomOf(Account account) {
        return account == null || account.roomId == null ? null : rooms.get(account.roomId);
    }

    private Room waitingRoom(ClientHandler client) {
        Room room = roomOf(client.account);
        if (room == null) client.error("NOT_IN_ROOM", "Bạn chưa ở trong phòng.");
        else if (!"WAITING".equals(room.state)) { client.error("GAME_STARTED", "Ván chơi đã bắt đầu."); return null; }
        return room;
    }

    private Room playingRoom(ClientHandler client) {
        Room room = roomOf(client.account);
        if (room == null || room.game == null) { client.error("NO_GAME", "Không có ván chơi đang hoạt động."); return null; }
        return room;
    }

    private boolean requireAuth(ClientHandler client) {
        if (client.account != null) return true;
        client.error("AUTH_REQUIRED", "Vui lòng đăng nhập trước.");
        return false;
    }

    private boolean requireHost(ClientHandler client, Room room) {
        if (room.host.equals(client.account.username)) return true;
        client.error("NOT_HOST", "Chỉ chủ phòng mới có thể thực hiện thao tác này.");
        return false;
    }

    private Piece piece(Room room, int id, ClientHandler client) {
        if (id >= 0 && id < room.game.pieces.size()) return room.game.pieces.get(id);
        client.error("PIECE_NOT_FOUND", "Không tìm thấy mảnh ghép.");
        return null;
    }

    private boolean ownsLock(ClientHandler client, Piece piece) {
        if (piece == null) return false;
        long now = System.currentTimeMillis();
        if (piece.lockedBy.equals(client.account.username) && piece.lockUntil > now) return true;
        if (piece.lockedBy.isBlank() || piece.lockUntil <= now) {
            piece.lockedBy = client.account.username;
            piece.lockUntil = now + LOCK_TIMEOUT_MS;
            return true;
        }
        client.error("PIECE_LOCKED", "Mảnh đang được " + piece.lockedBy + " giữ.");
        return false;
    }

    private void setRoomStatuses(Room room, String status) {
        for (Member member : room.members.values()) {
            Account account = findAccount(member.username);
            if (account != null && member.connected) account.status = status;
        }
    }

    private Account findAccount(String username) {
        if (username == null || username.isBlank()) return null;
        Account active = activeAccounts.get(username.toLowerCase(Locale.ROOT));
        if (active != null) return active;
        var opt = accountRepo.findByUsername(username);
        return opt.map(this::createRuntimeAccount).orElse(null);
    }

    private Account createRuntimeAccount(Models.Account entity) {
        Account acc = activeAccounts.computeIfAbsent(entity.username().toLowerCase(Locale.ROOT), k -> new Account());
        acc.id = entity.id();
        acc.username = entity.username();
        acc.coins = entity.coins();
        acc.salt = entity.salt();
        acc.passwordHash = entity.passwordHash();
        acc.friends.clear();
        acc.friends.addAll(friendRepo.getFriendUsernames(entity.id()));
        acc.ownedPuzzles.clear();
        acc.ownedPuzzles.addAll(puzzleRepo.getUserUnlockedPuzzles(entity.id()));
        acc.ownedThemes.clear();
        acc.ownedThemes.addAll(puzzleRepo.getUserUnlockedEnvironments(entity.id()));
        return acc;
    }

    private String ownedPuzzleOrDefault(Account account, String requested) {
        return PUZZLES.containsKey(requested) && account.ownedPuzzles.contains(requested) ? requested : "sunset_3x3";
    }

    private String uniqueRoomId() {
        final String alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        String id;
        do {
            StringBuilder value = new StringBuilder();
            for (int i = 0; i < 6; i++) value.append(alphabet.charAt(random.nextInt(alphabet.length())));
            id = value.toString();
        } while (rooms.containsKey(id));
        return id;
    }

    private String randomToken(int bytes) {
        byte[] value = new byte[bytes];
        random.nextBytes(value);
        return Base64.getUrlEncoder().withoutPadding().encodeToString(value);
    }

    private String passwordHash(String salt, String password) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            return Base64.getEncoder().encodeToString(digest.digest((salt + ":" + password).getBytes(StandardCharsets.UTF_8)));
        } catch (Exception exception) { throw new IllegalStateException(exception); }
    }

    private String cleanText(String value, int maxLength) {
        if (value == null) return "";
        String clean = value.replace('\r', ' ').replace('\n', ' ').trim();
        return clean.length() <= maxLength ? clean : clean.substring(0, maxLength);
    }

    private int remainingSeconds(GameState game) {
        if (game.durationSeconds <= 0) return -1;
        if (game.completed || game.timedOut) return Math.max(0, game.savedRemainingSeconds);
        return (int) Math.max(0, (game.deadlineEpochMs - System.currentTimeMillis() + 999) / 1000);
    }

    private int normalizeRotation(int rotation) {
        int value = rotation % 360;
        return value < 0 ? value + 360 : value;
    }

    private float clamp(float value, float min, float max) { return Math.max(min, Math.min(max, value)); }

    private List<SavedGame> savedGames(String owner) {
        return data.saves.computeIfAbsent(owner.toLowerCase(Locale.ROOT), ignored -> new ArrayList<>());
    }

    private GameState copyGame(GameState source, int remaining) {
        GameState copy = new GameState();
        copy.instanceId = source.instanceId;
        copy.puzzleId = source.puzzleId;
        copy.rows = source.rows;
        copy.columns = source.columns;
        copy.durationSeconds = source.durationSeconds;
        copy.savedRemainingSeconds = Math.max(0, remaining);
        copy.deadlineEpochMs = System.currentTimeMillis() + copy.savedRemainingSeconds * 1000L;
        copy.completed = source.completed;
        copy.timedOut = source.timedOut;
        for (Piece sourcePiece : source.pieces) {
            Piece piece = new Piece();
            piece.id = sourcePiece.id; piece.row = sourcePiece.row; piece.column = sourcePiece.column;
            piece.x = sourcePiece.x; piece.y = sourcePiece.y; piece.rotation = sourcePiece.rotation;
            piece.placed = sourcePiece.placed; piece.lockedBy = ""; piece.lockUntil = 0;
            copy.pieces.add(piece);
        }
        return copy;
    }

    private ServerData loadState() {
        if (!Files.exists(stateFile)) return new ServerData();
        try (ObjectInputStream input = new ObjectInputStream(Files.newInputStream(stateFile))) {
            Object value = input.readObject();
            if (value instanceof ServerData loaded) return loaded;
        } catch (Exception exception) {
            System.err.println("Could not load persisted state; starting clean: " + exception.getMessage());
        }
        return new ServerData();
    }

    private void saveSafely() {
        synchronized (stateLock) {
            try {
                Files.createDirectories(dataDirectory);
                Path temporary = stateFile.resolveSibling("state.bin.tmp");
                try (ObjectOutputStream output = new ObjectOutputStream(Files.newOutputStream(temporary))) {
                    output.writeObject(data);
                }
                try {
                    Files.move(temporary, stateFile, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE);
                } catch (AtomicMoveNotSupportedException ignored) {
                    Files.move(temporary, stateFile, StandardCopyOption.REPLACE_EXISTING);
                }
            } catch (Exception exception) {
                System.err.println("Could not save state: " + exception.getMessage());
            }
        }
    }

    private final class ClientHandler implements Runnable {
        private final Socket socket;
        private final Object writeLock = new Object();
        private BufferedReader reader;
        private BufferedWriter writer;
        private Account account;
        private volatile boolean closed;

        ClientHandler(Socket socket) { this.socket = socket; }

        @Override public void run() {
            try {
                reader = new BufferedReader(new InputStreamReader(socket.getInputStream(), StandardCharsets.UTF_8));
                writer = new BufferedWriter(new OutputStreamWriter(socket.getOutputStream(), StandardCharsets.UTF_8));
                send("SERVER_HELLO", "version", "1.0", "time", System.currentTimeMillis());
                String line;
                while (!closed && (line = reader.readLine()) != null) {
                    if (line.length() > 128_000) { error("MESSAGE_TOO_LARGE", "Tin nhắn vượt quá giới hạn."); break; }
                    if (!line.isBlank()) handle(this, Wire.decode(line));
                }
            } catch (IOException ignored) {
            } finally {
                closeSilently();
                disconnected(this);
            }
        }

        void send(String type, Object... values) {
            if (closed || writer == null) return;
            try {
                synchronized (writeLock) {
                    writer.write(Wire.encode(type, values));
                    writer.newLine();
                    writer.flush();
                }
            } catch (IOException exception) { closeSilently(); }
        }

        void error(String code, String message) { send("ERROR", "code", code, "message", message); }

        void closeSilently() {
            closed = true;
            try { socket.close(); } catch (IOException ignored) {}
        }
    }

    private static final class PuzzleDefinition {
        final String id, name; final int rows, columns, seconds, price;
        PuzzleDefinition(String id, String name, int rows, int columns, int seconds, int price) {
            this.id = id; this.name = name; this.rows = rows; this.columns = columns; this.seconds = seconds; this.price = price;
        }
    }

    private static final class ThemeDefinition {
        final String id, name; final int price;
        ThemeDefinition(String id, String name, int price) { this.id = id; this.name = name; this.price = price; }
    }

    private static final class Room {
        String id, name, host, puzzleId, saveOwner;
        String themeId = "SCN_Nature";
        String password = "";
        int targetTimeSeconds = 300;
        String state = "WAITING";
        int maxPlayers;
        boolean hasSave;
        long lastAutoSave = System.currentTimeMillis();
        final LinkedHashMap<String, Member> members = new LinkedHashMap<>();
        GameState game;

        boolean isPrivate() {
            return password != null && !password.isBlank();
        }
    }

    private static final class Member {
        final String username;
        boolean ready;
        boolean connected = true;
        int score;
        long lastSeen = System.currentTimeMillis();
        Member(String username) { this.username = username; }
    }

    private static final class ServerData implements Serializable {
        @Serial private static final long serialVersionUID = 1L;
        final Map<String, Account> accounts = new HashMap<>();
        final Map<String, List<SavedGame>> saves = new HashMap<>();
    }

    private static final class Account implements Serializable {
        @Serial private static final long serialVersionUID = 1L;
        long id;
        String username, salt, passwordHash, sessionToken = "", equippedTheme = "SCN_Nature", roomId = "", status = "OFFLINE";
        int coins;
        final Set<String> friends = new TreeSet<>(String.CASE_INSENSITIVE_ORDER);
        final Set<String> ownedPuzzles = new HashSet<>();
        final Set<String> ownedThemes = new HashSet<>();
    }

    private static final class SavedGame implements Serializable {
        @Serial private static final long serialVersionUID = 1L;
        String id, owner, roomName, label;
        long createdAt;
        boolean automatic;
        GameState game;
        Map<String, Integer> scores = new HashMap<>();
    }

    private static final class GameState implements Serializable {
        @Serial private static final long serialVersionUID = 1L;
        String instanceId, puzzleId;
        int rows, columns, durationSeconds, savedRemainingSeconds;
        long deadlineEpochMs;
        boolean completed, timedOut;
        final List<Piece> pieces = new ArrayList<>();
    }

    private static final class Piece implements Serializable {
        @Serial private static final long serialVersionUID = 1L;
        int id, row, column, rotation;
        float x, y;
        boolean placed;
        String lockedBy = "";
        long lockUntil;
    }
}
