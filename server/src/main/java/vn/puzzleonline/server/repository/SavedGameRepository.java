package vn.puzzleonline.server.repository;

import vn.puzzleonline.server.db.DatabaseManager;

import java.sql.*;
import java.util.*;

public class SavedGameRepository {
    private final DatabaseManager db;

    public SavedGameRepository(DatabaseManager db) {
        this.db = db;
    }

    public record SavedGameRecord(
        String id,
        long ownerId,
        String roomName,
        String label,
        boolean isAutomatic,
        String puzzleId,
        String environmentId,
        int elapsedSeconds,
        boolean isCompleted,
        String scoresJson,
        String piecesJson,
        Timestamp updatedAt
    ) {}

    public void saveGame(
        String id,
        long ownerId,
        String roomName,
        String label,
        boolean isAutomatic,
        String puzzleId,
        String environmentId,
        int elapsedSeconds,
        boolean isCompleted,
        String scoresJson,
        String piecesJson
    ) {
        String sql = """
            INSERT INTO saved_games (id, owner_id, room_name, label, is_automatic, puzzle_id, environment_id, elapsed_seconds, is_completed, scores, pieces)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON DUPLICATE KEY UPDATE
                room_name = VALUES(room_name),
                label = VALUES(label),
                is_automatic = VALUES(is_automatic),
                puzzle_id = VALUES(puzzle_id),
                environment_id = VALUES(environment_id),
                elapsed_seconds = VALUES(elapsed_seconds),
                is_completed = VALUES(is_completed),
                scores = VALUES(scores),
                pieces = VALUES(pieces)
        """;
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setString(1, id);
                stmt.setLong(2, ownerId);
                stmt.setString(3, roomName != null ? roomName : "Phòng chơi");
                stmt.setString(4, label != null ? label : "");
                stmt.setBoolean(5, isAutomatic);
                stmt.setString(6, puzzleId);
                stmt.setString(7, environmentId);
                stmt.setInt(8, elapsedSeconds);
                stmt.setBoolean(9, isCompleted);
                stmt.setString(10, scoresJson != null && !scoresJson.isBlank() ? scoresJson : "{}");
                stmt.setString(11, piecesJson != null && !piecesJson.isBlank() ? piecesJson : "[]");
                stmt.executeUpdate();
            }
        } catch (SQLException e) {
            System.err.println("[SavedGameRepository] saveGame error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
    }

    public Optional<SavedGameRecord> loadLatest(long ownerId) {
        String sql = """
            SELECT id, owner_id, room_name, label, is_automatic, puzzle_id, environment_id, elapsed_seconds, is_completed, scores, pieces, updated_at
            FROM saved_games
            WHERE owner_id = ?
            ORDER BY updated_at DESC
            LIMIT 1
        """;
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, ownerId);
                try (ResultSet rs = stmt.executeQuery()) {
                    if (rs.next()) {
                        return Optional.of(new SavedGameRecord(
                            rs.getString("id"),
                            rs.getLong("owner_id"),
                            rs.getString("room_name"),
                            rs.getString("label"),
                            rs.getBoolean("is_automatic"),
                            rs.getString("puzzle_id"),
                            rs.getString("environment_id"),
                            rs.getInt("elapsed_seconds"),
                            rs.getBoolean("is_completed"),
                            rs.getString("scores"),
                            rs.getString("pieces"),
                            rs.getTimestamp("updated_at")
                        ));
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[SavedGameRepository] loadLatest error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return Optional.empty();
    }

    public List<SavedGameRecord> getSavedGames(long ownerId) {
        String sql = """
            SELECT id, owner_id, room_name, label, is_automatic, puzzle_id, environment_id, elapsed_seconds, is_completed, scores, pieces, updated_at
            FROM saved_games
            WHERE owner_id = ?
            ORDER BY updated_at DESC
        """;
        List<SavedGameRecord> list = new ArrayList<>();
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, ownerId);
                try (ResultSet rs = stmt.executeQuery()) {
                    while (rs.next()) {
                        list.add(new SavedGameRecord(
                            rs.getString("id"),
                            rs.getLong("owner_id"),
                            rs.getString("room_name"),
                            rs.getString("label"),
                            rs.getBoolean("is_automatic"),
                            rs.getString("puzzle_id"),
                            rs.getString("environment_id"),
                            rs.getInt("elapsed_seconds"),
                            rs.getBoolean("is_completed"),
                            rs.getString("scores"),
                            rs.getString("pieces"),
                            rs.getTimestamp("updated_at")
                        ));
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[SavedGameRepository] getSavedGames error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return list;
    }
}
