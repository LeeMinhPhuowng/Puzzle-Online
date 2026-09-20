package vn.puzzleonline.server.model;

import java.sql.Timestamp;

public final class Models {
    private Models() {}

    public record Account(
        long id,
        String username,
        String passwordHash,
        String salt,
        int coins,
        String role
    ) {}

    public record UserProfile(
        long userId,
        String displayName,
        String avatarUrl,
        String bio,
        long totalScore
    ) {}

    public record FriendRequest(
        long id,
        long senderId,
        String senderUsername,
        long receiverId,
        String receiverUsername,
        String status,
        Timestamp createdAt,
        Timestamp respondedAt
    ) {}

    public record PuzzleDef(
        String id,
        String name,
        int rows,
        int cols,
        int price,
        boolean isActive
    ) {}

    public record EnvironmentDef(
        String id,
        String name,
        String description,
        int price,
        boolean isActive
    ) {}

    public record FriendInfo(
        long userId,
        String username,
        String displayName,
        boolean isOnline,
        String currentRoomId
    ) {}
}
