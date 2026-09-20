package vn.puzzleonline.server.service;

import vn.puzzleonline.server.model.Models.Account;
import vn.puzzleonline.server.model.Models.UserProfile;
import vn.puzzleonline.server.repository.AccountRepository;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.util.Base64;
import java.util.Optional;
import java.util.regex.Pattern;

public class AuthService {
    private static final Pattern USERNAME_PATTERN = Pattern.compile("^[a-zA-Z0-9_]{3,20}$");
    private final AccountRepository accountRepo;
    private final SecureRandom random = new SecureRandom();

    public AuthService(AccountRepository accountRepo) {
        this.accountRepo = accountRepo;
    }

    public record AuthResult(boolean success, String errorCode, String message, Account account) {}

    public AuthResult register(String username, String password) {
        if (username == null || !USERNAME_PATTERN.matcher(username.trim()).matches()) {
            return new AuthResult(false, "INVALID_USERNAME", "Tên tài khoản cần 3-20 ký tự chữ, số hoặc dấu gạch dưới.", null);
        }
        if (password == null || password.length() < 4 || password.length() > 64) {
            return new AuthResult(false, "INVALID_PASSWORD", "Mật khẩu cần từ 4 đến 64 ký tự.", null);
        }
        String cleanUsername = username.trim();
        if (accountRepo.findByUsername(cleanUsername).isPresent()) {
            return new AuthResult(false, "USERNAME_TAKEN", "Tên tài khoản đã được sử dụng.", null);
        }

        String salt = generateSalt(18);
        String passwordHash = hashPassword(salt, password);
        Optional<Account> created = accountRepo.createAccountWithProfile(cleanUsername, passwordHash, salt, 300);

        if (created.isPresent()) {
            return new AuthResult(true, null, "Đăng ký thành công.", created.get());
        } else {
            return new AuthResult(false, "DB_ERROR", "Lỗi tạo tài khoản trên cơ sở dữ liệu.", null);
        }
    }

    public AuthResult login(String username, String password) {
        if (username == null || password == null) {
            return new AuthResult(false, "INVALID_CREDENTIALS", "Vui lòng nhập đầy đủ tài khoản và mật khẩu.", null);
        }
        Optional<Account> opt = accountRepo.findByUsername(username.trim());
        if (opt.isEmpty()) {
            return new AuthResult(false, "INVALID_CREDENTIALS", "Tài khoản hoặc mật khẩu không chính xác.", null);
        }
        Account account = opt.get();
        String expectedHash = hashPassword(account.salt(), password);
        if (!MessageDigest.isEqual(expectedHash.getBytes(StandardCharsets.UTF_8), account.passwordHash().getBytes(StandardCharsets.UTF_8))) {
            return new AuthResult(false, "INVALID_CREDENTIALS", "Tài khoản hoặc mật khẩu không chính xác.", null);
        }
        return new AuthResult(true, null, "Đăng nhập thành công.", account);
    }

    public Optional<UserProfile> getProfile(long userId) {
        return accountRepo.getProfile(userId);
    }

    public boolean updateCoins(long userId, int deltaCoins) {
        return accountRepo.updateCoins(userId, deltaCoins);
    }

    public void addScore(long userId, int deltaScore) {
        accountRepo.updateScore(userId, deltaScore);
    }

    private String generateSalt(int length) {
        byte[] bytes = new byte[length];
        random.nextBytes(bytes);
        return Base64.getUrlEncoder().withoutPadding().encodeToString(bytes);
    }

    public static String hashPassword(String salt, String password) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            digest.update(salt.getBytes(StandardCharsets.UTF_8));
            byte[] hash = digest.digest(password.getBytes(StandardCharsets.UTF_8));
            return Base64.getUrlEncoder().withoutPadding().encodeToString(hash);
        } catch (Exception e) {
            throw new RuntimeException("SHA-256 not available", e);
        }
    }
}
