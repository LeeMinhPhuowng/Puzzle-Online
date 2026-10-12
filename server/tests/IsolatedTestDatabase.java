import java.nio.file.*;
import java.sql.*;
import java.util.*;
import java.io.*;

/** Creates a disposable schema so network regression tests never add users to the real database. */
public final class IsolatedTestDatabase {
    public static void main(String[] args) throws Exception {
        String name = args[2];
        if (!name.matches("puzzle_codex_test_[a-f0-9]+")) throw new IllegalArgumentException("Invalid test schema");
        Properties config = new Properties();
        try (InputStream in = Files.newInputStream(Path.of(args[1]))) { config.load(in); }
        String url = config.getProperty("db.url");
        int query = url.indexOf('?');
        String base = query < 0 ? url : url.substring(0, query);
        String serverUrl = base.substring(0, base.lastIndexOf('/') + 1) + (query < 0 ? "" : url.substring(query));
        try (Connection admin = DriverManager.getConnection(serverUrl, config.getProperty("db.user"), config.getProperty("db.password"));
             Statement statement = admin.createStatement()) {
            if (args[0].equals("drop")) { statement.executeUpdate("DROP DATABASE IF EXISTS `" + name + "`"); return; }
            statement.executeUpdate("CREATE DATABASE `" + name + "` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        }
        String testUrl = base.substring(0, base.lastIndexOf('/') + 1) + name + (query < 0 ? "" : url.substring(query));
        config.setProperty("db.url", testUrl);
        try (Connection test = DriverManager.getConnection(testUrl, config.getProperty("db.user"), config.getProperty("db.password"));
             Statement statement = test.createStatement()) {
            String schema = Files.readString(Path.of(args[3])).replaceAll("(?m)^--.*$", "");
            for (String sql : schema.split(";")) {
                sql = sql.trim();
                if (!sql.isEmpty() && !sql.startsWith("CREATE DATABASE") && !sql.startsWith("USE ")) statement.execute(sql);
            }
        }
        try (OutputStream out = Files.newOutputStream(Path.of(args[4]))) { config.store(out, "Disposable regression database"); }
        System.out.println("Created isolated test schema");
    }
}
