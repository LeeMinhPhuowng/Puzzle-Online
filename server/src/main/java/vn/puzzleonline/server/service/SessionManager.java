package vn.puzzleonline.server.service;

import vn.puzzleonline.server.model.Models.Account;

import java.security.SecureRandom;
import java.util.Base64;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;

public class SessionManager {
    private final SecureRandom random = new SecureRandom();
    private final Map<String, Account> tokenToAccount = new ConcurrentHashMap<>();
    private final Map<Long, String> userIdToToken = new ConcurrentHashMap<>();
    private final Map<String, Long> tokenToUserId = new ConcurrentHashMap<>();

    public String createSession(Account account) {
        String token = createSession(account.id(), account.username());
        tokenToAccount.put(token, account);
        return token;
    }

    public String createSession(long userId, String username) {
        String existingToken = userIdToToken.get(userId);
        if (existingToken != null) {
            return existingToken;
        }

        byte[] bytes = new byte[32];
        random.nextBytes(bytes);
        String token = Base64.getUrlEncoder().withoutPadding().encodeToString(bytes);

        userIdToToken.put(userId, token);
        tokenToUserId.put(token, userId);
        return token;
    }

    public Long getAccountId(String token) {
        if (token == null || token.isBlank()) return null;
        return tokenToUserId.get(token);
    }

    public Optional<Account> getAccountByToken(String token) {
        if (token == null || token.isBlank()) return Optional.empty();
        return Optional.ofNullable(tokenToAccount.get(token));
    }

    public void invalidateToken(String token) {
        if (token == null) return;
        Long userId = tokenToUserId.remove(token);
        tokenToAccount.remove(token);
        if (userId != null) {
            userIdToToken.remove(userId);
        }
    }

    public void invalidateSession(long userId) {
        String token = userIdToToken.remove(userId);
        if (token != null) {
            tokenToAccount.remove(token);
            tokenToUserId.remove(token);
        }
    }
}
