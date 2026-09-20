package vn.puzzleonline.server.service;

import vn.puzzleonline.server.db.DatabaseManager;
import vn.puzzleonline.server.model.Models.Account;
import vn.puzzleonline.server.repository.AccountRepository;

import java.io.*;
import java.nio.file.Files;
import java.nio.file.Path;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.util.*;

public final class StateMigrationHelper {
    private StateMigrationHelper() {}

    public static void checkAndMigrate(Path stateFile, DatabaseManager db, AccountRepository accountRepo) {
        if (!Files.exists(stateFile)) return;

        Connection conn = null;
        try {
            conn = db.getConnection();
            try (PreparedStatement stmt = conn.prepareStatement("SELECT COUNT(*) FROM accounts");
                 ResultSet rs = stmt.executeQuery()) {
                if (rs.next() && rs.getInt(1) > 0) {
                    System.out.println("[Migration] MySQL already has " + rs.getInt(1) + " accounts. Skipping state.bin migration.");
                    return;
                }
            }

            System.out.println("[Migration] Detected legacy state.bin. Migrating accounts and data to MySQL...");
            try (ObjectInputStream input = new ObjectInputStream(Files.newInputStream(stateFile))) {
                Object value = input.readObject();
                if (value != null) {
                    migrateLegacyData(value, conn, accountRepo);
                }
            }

            Path backupPath = stateFile.resolveSibling("state.bin.migrated");
            try {
                Files.move(stateFile, backupPath);
                System.out.println("[Migration] Successfully migrated legacy data to MySQL. Renamed state.bin to state.bin.migrated.");
            } catch (Exception e) {
                System.out.println("[Migration] Legacy data migrated to MySQL (could not rename file: " + e.getMessage() + ").");
            }
        } catch (Exception e) {
            System.err.println("[Migration] Migration from state.bin encountered an error: " + e.getMessage());
        } finally {
            db.releaseConnection(conn);
        }
    }

    @SuppressWarnings("unchecked")
    private static void migrateLegacyData(Object serverData, Connection conn, AccountRepository accountRepo) {
        try {
            var accountsField = serverData.getClass().getDeclaredField("accounts");
            accountsField.setAccessible(true);
            Map<String, Object> accounts = (Map<String, Object>) accountsField.get(serverData);

            if (accounts == null || accounts.isEmpty()) {
                System.out.println("[Migration] Legacy state.bin had no accounts to import.");
                return;
            }

            for (var entry : accounts.entrySet()) {
                Object acc = entry.getValue();
                String username = (String) acc.getClass().getDeclaredField("username").get(acc);
                String salt = (String) acc.getClass().getDeclaredField("salt").get(acc);
                String passwordHash = (String) acc.getClass().getDeclaredField("passwordHash").get(acc);
                int coins = (int) acc.getClass().getDeclaredField("coins").get(acc);

                Optional<Account> created = accountRepo.createAccountWithProfile(username, passwordHash, salt, coins);
                if (created.isPresent()) {
                    long userId = created.get().id();
                    // Import owned puzzles
                    Set<String> ownedPuzzles = (Set<String>) acc.getClass().getDeclaredField("ownedPuzzles").get(acc);
                    if (ownedPuzzles != null) {
                        for (String p : ownedPuzzles) {
                            try (PreparedStatement s = conn.prepareStatement("INSERT IGNORE INTO user_puzzles (user_id, puzzle_id) VALUES (?, ?)")) {
                                s.setLong(1, userId);
                                s.setString(2, p);
                                s.executeUpdate();
                            }
                        }
                    }

                    // Import owned themes
                    Set<String> ownedThemes = (Set<String>) acc.getClass().getDeclaredField("ownedThemes").get(acc);
                    if (ownedThemes != null) {
                        for (String t : ownedThemes) {
                            try (PreparedStatement s = conn.prepareStatement("INSERT IGNORE INTO user_environments (user_id, environment_id) VALUES (?, ?)")) {
                                s.setLong(1, userId);
                                s.setString(2, t);
                                s.executeUpdate();
                            }
                        }
                    }
                    System.out.println("[Migration] Imported account: " + username + " (coins: " + coins + ")");
                }
            }
        } catch (Exception e) {
            System.err.println("[Migration] Warning during legacy import: " + e.getMessage());
        }
    }
}
