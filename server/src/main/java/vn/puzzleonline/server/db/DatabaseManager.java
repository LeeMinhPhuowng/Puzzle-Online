package vn.puzzleonline.server.db;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.SQLException;
import java.util.Properties;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.TimeUnit;

public final class DatabaseManager {
    private static DatabaseManager instance;

    private final String url;
    private final String user;
    private final String password;
    private final int maxPoolSize;
    private final BlockingQueue<Connection> pool;

    private DatabaseManager(String url, String user, String password, int maxPoolSize) {
        this.url = url;
        this.user = user;
        this.password = password;
        this.maxPoolSize = Math.max(2, maxPoolSize);
        this.pool = new ArrayBlockingQueue<>(this.maxPoolSize);
    }

    public static synchronized void initialize(String configPath) {
        if (instance != null) return;

        Properties props = new Properties();
        File propFile = new File(configPath != null ? configPath : "db.properties");
        if (!propFile.exists()) {
            propFile = new File("server/db.properties");
        }

        if (propFile.exists()) {
            try (InputStream in = new FileInputStream(propFile)) {
                props.load(in);
                System.out.println("[DB] Loaded configuration from: " + propFile.getAbsolutePath());
            } catch (Exception e) {
                System.err.println("[DB] Warning: Could not read " + propFile.getPath() + ": " + e.getMessage());
            }
        }

        String url = props.getProperty("db.url", "jdbc:mysql://localhost:3306/puzzle_online?useSSL=false&allowPublicKeyRetrieval=true&serverTimezone=UTC&characterEncoding=UTF-8");
        String user = props.getProperty("db.user", "root");
        String pass = props.getProperty("db.password", "123456");
        int maxPool = Integer.parseInt(props.getProperty("db.pool.max", "10"));

        try {
            Class.forName("com.mysql.cj.jdbc.Driver");
        } catch (ClassNotFoundException e) {
            System.err.println("[DB] MySQL JDBC Driver not found in classpath! Trying default DriverManager.");
        }

        instance = new DatabaseManager(url, user, pass, maxPool);
        instance.testConnection();
    }

    public static DatabaseManager getInstance() {
        if (instance == null) {
            initialize(null);
        }
        return instance;
    }

    private void testConnection() {
        try (Connection conn = createConnection()) {
            System.out.println("[DB] Connection to MySQL successfully verified on: " + url);
        } catch (SQLException e) {
            System.err.println("[DB] ERROR: Could not connect to MySQL database: " + e.getMessage());
            throw new RuntimeException("Database connection failure", e);
        }
    }

    private Connection createConnection() throws SQLException {
        return DriverManager.getConnection(url, user, password);
    }

    public Connection getConnection() throws SQLException {
        Connection conn = pool.poll();
        if (conn != null) {
            try {
                if (conn.isValid(1)) {
                    return conn;
                } else {
                    try { conn.close(); } catch (Exception ignored) {}
                }
            } catch (SQLException e) {
                try { conn.close(); } catch (Exception ignored) {}
            }
        }
        return createConnection();
    }

    public void releaseConnection(Connection conn) {
        if (conn == null) return;
        try {
            if (!conn.isClosed() && conn.isValid(1)) {
                if (!conn.getAutoCommit()) {
                    conn.setAutoCommit(true);
                }
                if (!pool.offer(conn)) {
                    conn.close();
                }
                return;
            }
        } catch (Exception ignored) {}
        try { conn.close(); } catch (Exception ignored) {}
    }
}
