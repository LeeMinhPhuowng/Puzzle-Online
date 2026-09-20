package vn.puzzleonline.server.repository;

import vn.puzzleonline.server.db.DatabaseManager;
import vn.puzzleonline.server.model.Models.Account;
import vn.puzzleonline.server.model.Models.UserProfile;

import java.sql.*;
import java.util.Optional;

public class AccountRepository {
    private final DatabaseManager db;

    public AccountRepository(DatabaseManager db) {
        this.db = db;
    }

    public Optional<Account> findByUsername(String username) {
        String sql = "SELECT id, username, password_hash, salt, coins, role FROM accounts WHERE LOWER(username) = LOWER(?)";
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setString(1, username.trim());
                try (ResultSet rs = stmt.executeQuery()) {
                    if (rs.next()) {
                        return Optional.of(new Account(
                            rs.getLong("id"),
                            rs.getString("username"),
                            rs.getString("password_hash"),
                            rs.getString("salt"),
                            rs.getInt("coins"),
                            rs.getString("role")
                        ));
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[AccountRepository] findByUsername error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return Optional.empty();
    }

    public Optional<Account> findById(long id) {
        String sql = "SELECT id, username, password_hash, salt, coins, role FROM accounts WHERE id = ?";
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, id);
                try (ResultSet rs = stmt.executeQuery()) {
                    if (rs.next()) {
                        return Optional.of(new Account(
                            rs.getLong("id"),
                            rs.getString("username"),
                            rs.getString("password_hash"),
                            rs.getString("salt"),
                            rs.getInt("coins"),
                            rs.getString("role")
                        ));
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[AccountRepository] findById error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return Optional.empty();
    }

    public Optional<Account> createAccountWithProfile(String username, String passwordHash, String salt, int initialCoins) {
        Connection conn = null;
        try {
            conn = db.getConnection();
            conn.setAutoCommit(false);

            long accountId;
            String insertAccountSql = "INSERT INTO accounts (username, password_hash, salt, coins, role) VALUES (?, ?, ?, ?, 'PLAYER')";
            try (PreparedStatement stmt = conn.prepareStatement(insertAccountSql, Statement.RETURN_GENERATED_KEYS)) {
                stmt.setString(1, username);
                stmt.setString(2, passwordHash);
                stmt.setString(3, salt);
                stmt.setInt(4, initialCoins);
                stmt.executeUpdate();

                try (ResultSet rs = stmt.getGeneratedKeys()) {
                    if (!rs.next()) {
                        conn.rollback();
                        return Optional.empty();
                    }
                    accountId = rs.getLong(1);
                }
            }

            // 2. Tạo profile mặc định
            String insertProfileSql = "INSERT INTO user_profiles (user_id, display_name, avatar_url, bio, total_score) VALUES (?, ?, 'default_avatar', '', 0)";
            try (PreparedStatement stmt = conn.prepareStatement(insertProfileSql)) {
                stmt.setLong(1, accountId);
                stmt.setString(2, username);
                stmt.executeUpdate();
            }

            // 3. Mở khóa bộ tranh mặc định (sunset_3x3)
            String insertPuzzleSql = "INSERT IGNORE INTO user_puzzles (user_id, puzzle_id) VALUES (?, 'sunset_3x3')";
            try (PreparedStatement stmt = conn.prepareStatement(insertPuzzleSql)) {
                stmt.setLong(1, accountId);
                stmt.executeUpdate();
            }

            // 4. Mở khóa môi trường 3D mặc định (SCN_Nature)
            String insertEnvSql = "INSERT IGNORE INTO user_environments (user_id, environment_id) VALUES (?, 'SCN_Nature')";
            try (PreparedStatement stmt = conn.prepareStatement(insertEnvSql)) {
                stmt.setLong(1, accountId);
                stmt.executeUpdate();
            }

            conn.commit();
            return Optional.of(new Account(accountId, username, passwordHash, salt, initialCoins, "PLAYER"));
        } catch (SQLException e) {
            System.err.println("[AccountRepository] createAccount error: " + e.getMessage());
            if (conn != null) {
                try { conn.rollback(); } catch (Exception ignored) {}
            }
        } finally {
            db.releaseConnection(conn);
        }
        return Optional.empty();
    }

    public boolean updateCoins(long userId, int deltaCoins) {
        String sql = deltaCoins >= 0
            ? "UPDATE accounts SET coins = coins + ? WHERE id = ?"
            : "UPDATE accounts SET coins = coins + ? WHERE id = ? AND coins + ? >= 0";

        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setInt(1, deltaCoins);
                stmt.setLong(2, userId);
                if (deltaCoins < 0) {
                    stmt.setInt(3, deltaCoins);
                }
                return stmt.executeUpdate() > 0;
            }
        } catch (SQLException e) {
            System.err.println("[AccountRepository] updateCoins error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return false;
    }

    public void updateScore(long userId, int deltaScore) {
        if (deltaScore <= 0) return;
        String sql = "UPDATE user_profiles SET total_score = total_score + ? WHERE user_id = ?";
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setInt(1, deltaScore);
                stmt.setLong(2, userId);
                stmt.executeUpdate();
            }
        } catch (SQLException e) {
            System.err.println("[AccountRepository] updateScore error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
    }

    public Optional<UserProfile> getProfile(long userId) {
        String sql = "SELECT user_id, display_name, avatar_url, bio, total_score FROM user_profiles WHERE user_id = ?";
        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement(sql)) {
                stmt.setLong(1, userId);
                try (ResultSet rs = stmt.executeQuery()) {
                    if (rs.next()) {
                        return Optional.of(new UserProfile(
                            rs.getLong("user_id"),
                            rs.getString("display_name"),
                            rs.getString("avatar_url"),
                            rs.getString("bio"),
                            rs.getLong("total_score")
                        ));
                    }
                }
            }
        } catch (SQLException e) {
            System.err.println("[AccountRepository] getProfile error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
        return Optional.empty();
    }
}
