-- ===================================================================
-- PUZZLE ONLINE DATABASE SCHEMA
-- ===================================================================

CREATE DATABASE IF NOT EXISTS puzzle_online
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE puzzle_online;

-- =========================================================
-- 1. ACCOUNTS
-- =========================================================
CREATE TABLE IF NOT EXISTS accounts (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(20) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    salt VARCHAR(128) NOT NULL,
    coins INT NOT NULL DEFAULT 300,
    role ENUM('PLAYER', 'ADMIN') NOT NULL DEFAULT 'PLAYER',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_accounts_coins CHECK (coins >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =========================================================
-- 2. USER PROFILES
-- =========================================================
CREATE TABLE IF NOT EXISTS user_profiles (
    user_id BIGINT PRIMARY KEY,
    display_name VARCHAR(50) NOT NULL,
    avatar_url VARCHAR(255) NOT NULL DEFAULT 'default_avatar',
    bio VARCHAR(255) NOT NULL DEFAULT '',
    total_score BIGINT NOT NULL DEFAULT 0,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_profiles_user FOREIGN KEY (user_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT chk_profiles_score CHECK (total_score >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =========================================================
-- 3. FRIENDS
-- =========================================================
CREATE TABLE IF NOT EXISTS friends (
    user_id BIGINT NOT NULL,
    friend_id BIGINT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, friend_id),
    CONSTRAINT fk_friends_user FOREIGN KEY (user_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_friends_friend FOREIGN KEY (friend_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT chk_friends_not_self CHECK (user_id <> friend_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =========================================================
-- 4. FRIEND REQUESTS
-- =========================================================
CREATE TABLE IF NOT EXISTS friend_requests (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    sender_id BIGINT NOT NULL,
    receiver_id BIGINT NOT NULL,
    status ENUM('PENDING', 'ACCEPTED', 'REJECTED', 'CANCELLED') NOT NULL DEFAULT 'PENDING',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    responded_at DATETIME DEFAULT NULL,
    CONSTRAINT fk_friend_requests_sender FOREIGN KEY (sender_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_friend_requests_receiver FOREIGN KEY (receiver_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT chk_friend_requests_not_self CHECK (sender_id <> receiver_id),
    INDEX idx_friend_requests_receiver_status (receiver_id, status),
    INDEX idx_friend_requests_sender_status (sender_id, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =========================================================
-- 5. PUZZLES
-- =========================================================
CREATE TABLE IF NOT EXISTS puzzles (
    id VARCHAR(32) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    rows_count INT NOT NULL,
    cols_count INT NOT NULL,
    price INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT chk_puzzles_rows CHECK (rows_count > 0),
    CONSTRAINT chk_puzzles_cols CHECK (cols_count > 0),
    CONSTRAINT chk_puzzles_price CHECK (price >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =========================================================
-- 6. ROOM ENVIRONMENTS
-- =========================================================
CREATE TABLE IF NOT EXISTS room_environments (
    id VARCHAR(32) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(255) NOT NULL DEFAULT '',
    price INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT chk_environments_price CHECK (price >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =========================================================
-- 7. USER PUZZLES
-- =========================================================
CREATE TABLE IF NOT EXISTS user_puzzles (
    user_id BIGINT NOT NULL,
    puzzle_id VARCHAR(32) NOT NULL,
    unlocked_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, puzzle_id),
    CONSTRAINT fk_user_puzzles_user FOREIGN KEY (user_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_user_puzzles_puzzle FOREIGN KEY (puzzle_id) REFERENCES puzzles(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =========================================================
-- 8. USER ENVIRONMENTS
-- =========================================================
CREATE TABLE IF NOT EXISTS user_environments (
    user_id BIGINT NOT NULL,
    environment_id VARCHAR(32) NOT NULL,
    unlocked_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, environment_id),
    CONSTRAINT fk_user_environments_user FOREIGN KEY (user_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_user_environments_environment FOREIGN KEY (environment_id) REFERENCES room_environments(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =========================================================
-- 9. USER COMPLETED PUZZLES
-- =========================================================
CREATE TABLE IF NOT EXISTS user_completed_puzzles (
    user_id BIGINT NOT NULL,
    puzzle_id VARCHAR(32) NOT NULL,
    first_completed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, puzzle_id),
    CONSTRAINT fk_completed_user FOREIGN KEY (user_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_completed_puzzle FOREIGN KEY (puzzle_id) REFERENCES puzzles(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =========================================================
-- 10. SAVED GAMES
-- =========================================================
CREATE TABLE IF NOT EXISTS saved_games (
    id CHAR(36) PRIMARY KEY,
    owner_id BIGINT NOT NULL,
    room_name VARCHAR(100) NOT NULL,
    label VARCHAR(100) NOT NULL DEFAULT '',
    is_automatic BOOLEAN NOT NULL DEFAULT FALSE,
    puzzle_id VARCHAR(32) NOT NULL,
    environment_id VARCHAR(32) NOT NULL,
    elapsed_seconds INT NOT NULL DEFAULT 0,
    is_completed BOOLEAN NOT NULL DEFAULT FALSE,
    scores JSON NOT NULL,
    pieces JSON NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_saved_games_owner FOREIGN KEY (owner_id) REFERENCES accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_saved_games_puzzle FOREIGN KEY (puzzle_id) REFERENCES puzzles(id) ON DELETE RESTRICT,
    CONSTRAINT fk_saved_games_environment FOREIGN KEY (environment_id) REFERENCES room_environments(id) ON DELETE RESTRICT,
    CONSTRAINT chk_saved_games_elapsed CHECK (elapsed_seconds >= 0),
    INDEX idx_saved_games_owner (owner_id),
    INDEX idx_saved_games_updated (updated_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =========================================================
-- SEED DATA
-- =========================================================
INSERT INTO puzzles (id, name, rows_count, cols_count, price)
VALUES
    ('sunset_3x3', 'Sunset (Hoàng hôn)', 3, 3, 0),
    ('forest_4x3', 'Forest (Rừng xanh)', 4, 3, 160),
    ('ocean_4x4', 'Ocean (Đại dương)', 4, 4, 240)
ON DUPLICATE KEY UPDATE name=VALUES(name), rows_count=VALUES(rows_count), cols_count=VALUES(cols_count), price=VALUES(price);

INSERT INTO room_environments (id, name, description, price)
VALUES
    ('SCN_Nature', 'Nature Garden (Thiên nhiên xanh mát)', 'Không gian thiên nhiên trong lành', 0),
    ('SCN_Classroom', 'Classic Classroom (Phòng học cổ điển)', 'Không gian lớp học ấm cúng', 150),
    ('SCN_Sakura', 'Sakura Garden (Vườn hoa anh đào)', 'Không gian hoa anh đào lãng mạn', 200)
ON DUPLICATE KEY UPDATE name=VALUES(name), description=VALUES(description), price=VALUES(price);
