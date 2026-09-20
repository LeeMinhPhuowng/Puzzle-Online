package vn.puzzleonline.server.repository;

import vn.puzzleonline.server.db.DatabaseManager;
import vn.puzzleonline.server.model.Models.FriendRequest;

import java.sql.*;
import java.util.ArrayList;
import java.util.List;

public class FriendRepository {
    private final DatabaseManager db;

    public FriendRepository(DatabaseManager db) {
        this.db = db;
    }

    public boolean isFriend(long u1, long u2) {
        String sql = "SELECT 1 FROM friends WHERE user_id = ? AND friend_id = ?";
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, u1);
                stmt.setLong(2, u2);
                try (ResultSet rs = stmt.executeQuery()) {
                    return rs.next();
                }
            }
        } catch (SQLException e) {
            System.err.println("[FriendRepository] isFriend error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return false;
    }

    public Long findPendingRequestId(long senderId, long receiverId) {
        String sql = "SELECT id FROM friend_requests WHERE sender_id = ? AND receiver_id = ? AND status = 'PENDING'";
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, senderId);
                stmt.setLong(2, receiverId);
                try (ResultSet rs = stmt.executeQuery()) {
                    if (rs.next()) {
                        return rs.getLong("id");
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[FriendRepository] findPendingRequestId error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return null;
    }

    public long insertFriendRequest(long senderId, long receiverId) {
        String sql = "INSERT INTO friend_requests (sender_id, receiver_id, status) VALUES (?, ?, 'PENDING')";
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql, Statement.RETURN_GENERATED_KEYS)) {
                stmt.setLong(1, senderId);
                stmt.setLong(2, receiverId);
                stmt.executeUpdate();
                try (ResultSet rs = stmt.getGeneratedKeys()) {
                    if (rs.next()) {
                        return rs.getLong(1);
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[FriendRepository] insertFriendRequest error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return -1;
    }

    public List<FriendRequest> getPendingRequests(long receiverId) {
        String sql = """
            SELECT fr.id, fr.sender_id, a.username AS sender_username, fr.receiver_id, fr.status, fr.created_at, fr.responded_at
            FROM friend_requests fr
            JOIN accounts a ON a.id = fr.sender_id
            WHERE fr.receiver_id = ? AND fr.status = 'PENDING'
            ORDER BY fr.created_at DESC
        """;
        List<FriendRequest> result = new ArrayList<>();
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, receiverId);
                try (ResultSet rs = stmt.executeQuery()) {
                    while (rs.next()) {
                        result.add(new FriendRequest(
                            rs.getLong("id"),
                            rs.getLong("sender_id"),
                            rs.getString("sender_username"),
                            rs.getLong("receiver_id"),
                            "",
                            rs.getString("status"),
                            rs.getTimestamp("created_at"),
                            rs.getTimestamp("responded_at")
                        ));
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[FriendRepository] getPendingRequests error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return result;
    }

    public boolean acceptFriendRequest(long receiverId, long requestId) {
        Connection conn = null;
        try {
            conn = db.getConnection();
            conn.setAutoCommit(false);

            Long senderId = null;
            String querySql = "SELECT sender_id FROM friend_requests WHERE id = ? AND receiver_id = ? AND status = 'PENDING' FOR UPDATE";
            try (PreparedStatement qStmt = conn.prepareStatement(querySql)) {
                qStmt.setLong(1, requestId);
                qStmt.setLong(2, receiverId);
                try (ResultSet rs = qStmt.executeQuery()) {
                    if (rs.next()) {
                        senderId = rs.getLong("sender_id");
                    }
                }
            }

            if (senderId == null) {
                conn.rollback();
                return false;
            }

            String updateSql = "UPDATE friend_requests SET status = 'ACCEPTED', responded_at = CURRENT_TIMESTAMP WHERE id = ?";
            try (PreparedStatement uStmt = conn.prepareStatement(updateSql)) {
                uStmt.setLong(1, requestId);
                uStmt.executeUpdate();
            }

            // Lưu bạn bè 2 chiều (Alice -> Bob và Bob -> Alice)
            String insertSql = "INSERT IGNORE INTO friends (user_id, friend_id) VALUES (?, ?), (?, ?)";
            try (PreparedStatement iStmt = conn.prepareStatement(insertSql)) {
                iStmt.setLong(1, senderId);
                iStmt.setLong(2, receiverId);
                iStmt.setLong(3, receiverId);
                iStmt.setLong(4, senderId);
                iStmt.executeUpdate();
            }

            conn.commit();
            return true;
        } catch (SQLException e) {
            System.err.println("[FriendRepository] acceptFriendRequest error: " + e.getMessage());
            if (conn != null) {
                try { conn.rollback(); } catch (Exception ignored) {}
            }
        } finally {
            db.releaseConnection(conn);
        }
        return false;
    }

    public boolean rejectFriendRequest(long receiverId, long requestId) {
        String sql = "UPDATE friend_requests SET status = 'REJECTED', responded_at = CURRENT_TIMESTAMP WHERE id = ? AND receiver_id = ? AND status = 'PENDING'";
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, requestId);
                stmt.setLong(2, receiverId);
                return stmt.executeUpdate() > 0;
            }
        } catch (SQLException e) {
            System.err.println("[FriendRepository] rejectFriendRequest error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return false;
    }

    public boolean cancelFriendRequest(long senderId, long requestId) {
        String sql = "UPDATE friend_requests SET status = 'CANCELLED', responded_at = CURRENT_TIMESTAMP WHERE id = ? AND sender_id = ? AND status = 'PENDING'";
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, requestId);
                stmt.setLong(2, senderId);
                return stmt.executeUpdate() > 0;
            }
        } catch (SQLException e) {
            System.err.println("[FriendRepository] cancelFriendRequest error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return false;
    }

    public List<String> getFriendUsernames(long userId) {
        String sql = """
            SELECT a.username
            FROM friends f
            JOIN accounts a ON a.id = f.friend_id
            WHERE f.user_id = ?
            ORDER BY a.username ASC
        """;
        List<String> friends = new ArrayList<>();
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, userId);
                try (ResultSet rs = stmt.executeQuery()) {
                    while (rs.next()) {
                        friends.add(rs.getString("username"));
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[FriendRepository] getFriendUsernames error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return friends;
    }
}
