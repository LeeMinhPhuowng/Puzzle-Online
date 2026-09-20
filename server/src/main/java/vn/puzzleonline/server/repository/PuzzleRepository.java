package vn.puzzleonline.server.repository;

import vn.puzzleonline.server.db.DatabaseManager;
import vn.puzzleonline.server.model.Models.EnvironmentDef;
import vn.puzzleonline.server.model.Models.PuzzleDef;

import java.sql.*;
import java.util.*;

public class PuzzleRepository {
    private final DatabaseManager db;

    public PuzzleRepository(DatabaseManager db) {
        this.db = db;
    }

    public List<PuzzleDef> getAllPuzzles() {
        String sql = "SELECT id, name, rows_count, cols_count, price, is_active FROM puzzles WHERE is_active = TRUE ORDER BY price ASC";
        List<PuzzleDef> list = new ArrayList<>();
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql);
                 ResultSet rs = stmt.executeQuery()) {
                while (rs.next()) {
                    list.add(new PuzzleDef(
                        rs.getString("id"),
                        rs.getString("name"),
                        rs.getInt("rows_count"),
                        rs.getInt("cols_count"),
                        rs.getInt("price"),
                        rs.getBoolean("is_active")
                    ));
                }
            }
        } catch (SQLException e) {
            System.err.println("[PuzzleRepository] getAllPuzzles error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return list;
    }

    public List<EnvironmentDef> getAllEnvironments() {
        String sql = "SELECT id, name, description, price, is_active FROM room_environments WHERE is_active = TRUE ORDER BY price ASC";
        List<EnvironmentDef> list = new ArrayList<>();
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql);
                 ResultSet rs = stmt.executeQuery()) {
                while (rs.next()) {
                    list.add(new EnvironmentDef(
                        rs.getString("id"),
                        rs.getString("name"),
                        rs.getString("description"),
                        rs.getInt("price"),
                        rs.getBoolean("is_active")
                    ));
                }
            }
        } catch (SQLException e) {
            System.err.println("[PuzzleRepository] getAllEnvironments error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return list;
    }

    public Set<String> getUserUnlockedPuzzles(long userId) {
        String sql = "SELECT puzzle_id FROM user_puzzles WHERE user_id = ?";
        Set<String> set = new HashSet<>();
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, userId);
                try (ResultSet rs = stmt.executeQuery()) {
                    while (rs.next()) {
                        set.add(rs.getString("puzzle_id"));
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[PuzzleRepository] getUserUnlockedPuzzles error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return set;
    }

    public Set<String> getUserUnlockedEnvironments(long userId) {
        String sql = "SELECT environment_id FROM user_environments WHERE user_id = ?";
        Set<String> set = new HashSet<>();
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, userId);
                try (ResultSet rs = stmt.executeQuery()) {
                    while (rs.next()) {
                        set.add(rs.getString("environment_id"));
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[PuzzleRepository] getUserUnlockedEnvironments error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return set;
    }

    public boolean buyPuzzle(long userId, String puzzleId) {
        Connection conn = null;
        try {
            conn = db.getConnection();
            conn.setAutoCommit(false);

            int price = 0;
            String checkSql = "SELECT price FROM puzzles WHERE id = ? AND is_active = TRUE";
            try (PreparedStatement stmt = conn.prepareStatement(checkSql)) {
                stmt.setString(1, puzzleId);
                try (ResultSet rs = stmt.executeQuery()) {
                    if (!rs.next()) {
                        conn.rollback();
                        return false;
                    }
                    price = rs.getInt("price");
                }
            }

            // Trừ coins trong accounts
            String deductSql = "UPDATE accounts SET coins = coins - ? WHERE id = ? AND coins >= ?";
            try (PreparedStatement stmt = conn.prepareStatement(deductSql)) {
                stmt.setInt(1, price);
                stmt.setLong(2, userId);
                stmt.setInt(3, price);
                if (stmt.executeUpdate() <= 0) {
                    conn.rollback();
                    return false;
                }
            }

            // Mở khóa tranh
            String insertSql = "INSERT INTO user_puzzles (user_id, puzzle_id) VALUES (?, ?)";
            try (PreparedStatement stmt = conn.prepareStatement(insertSql)) {
                stmt.setLong(1, userId);
                stmt.setString(2, puzzleId);
                stmt.executeUpdate();
            }

            conn.commit();
            return true;
        } catch (SQLException e) {
            System.err.println("[PuzzleRepository] buyPuzzle error: " + e.getMessage());
            if (conn != null) {
                try { conn.rollback(); } catch (Exception ignored) {}
            }
        } finally {
            db.releaseConnection(conn);
        }
        return false;
    }

    public boolean buyEnvironment(long userId, String environmentId) {
        Connection conn = null;
        try {
            conn = db.getConnection();
            conn.setAutoCommit(false);

            int price = 0;
            String checkSql = "SELECT price FROM room_environments WHERE id = ? AND is_active = TRUE";
            try (PreparedStatement stmt = conn.prepareStatement(checkSql)) {
                stmt.setString(1, environmentId);
                try (ResultSet rs = stmt.executeQuery()) {
                    if (!rs.next()) {
                        conn.rollback();
                        return false;
                    }
                    price = rs.getInt("price");
                }
            }

            String deductSql = "UPDATE accounts SET coins = coins - ? WHERE id = ? AND coins >= ?";
            try (PreparedStatement stmt = conn.prepareStatement(deductSql)) {
                stmt.setInt(1, price);
                stmt.setLong(2, userId);
                stmt.setInt(3, price);
                if (stmt.executeUpdate() <= 0) {
                    conn.rollback();
                    return false;
                }
            }

            String insertSql = "INSERT INTO user_environments (user_id, environment_id) VALUES (?, ?)";
            try (PreparedStatement stmt = conn.prepareStatement(insertSql)) {
                stmt.setLong(1, userId);
                stmt.setString(2, environmentId);
                stmt.executeUpdate();
            }

            conn.commit();
            return true;
        } catch (SQLException e) {
            System.err.println("[PuzzleRepository] buyEnvironment error: " + e.getMessage());
            if (conn != null) {
                try { conn.rollback(); } catch (Exception ignored) {}
            }
        } finally {
            db.releaseConnection(conn);
        }
        return false;
    }

    public void recordCompletion(long userId, String puzzleId, int coinBonus) {
        Connection conn = null;
        try {
            conn = db.getConnection();
            conn.setAutoCommit(false);

            // Ghi nhận hoàn thành bộ tranh (first_completed_at)
            String compSql = "INSERT IGNORE INTO user_completed_puzzles (user_id, puzzle_id) VALUES (?, ?)";
            try (PreparedStatement stmt = conn.prepareStatement(compSql)) {
                stmt.setLong(1, userId);
                stmt.setString(2, puzzleId);
                stmt.executeUpdate();
            }

            // Thưởng coins
            String coinSql = "UPDATE accounts SET coins = coins + ? WHERE id = ?";
            try (PreparedStatement stmt = conn.prepareStatement(coinSql)) {
                stmt.setInt(1, coinBonus);
                stmt.setLong(2, userId);
                stmt.executeUpdate();
            }

            conn.commit();
        } catch (SQLException e) {
            System.err.println("[PuzzleRepository] recordCompletion error: " + e.getMessage());
            if (conn != null) {
                try { conn.rollback(); } catch (Exception ignored) {}
            }
        } finally {
            db.releaseConnection(conn);
        }
    }

    public int getUniquePuzzlesCompletedCount(long userId) {
        String sql = "SELECT COUNT(*) FROM user_completed_puzzles WHERE user_id = ?";
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, userId);
                try (ResultSet rs = stmt.executeQuery()) {
                    if (rs.next()) {
                        return rs.getInt(1);
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[PuzzleRepository] getUniquePuzzlesCompletedCount error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return 0;
    }
}
