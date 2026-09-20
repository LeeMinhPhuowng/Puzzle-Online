package vn.puzzleonline.server.service;

import vn.puzzleonline.server.model.Models.Account;
import vn.puzzleonline.server.model.Models.FriendRequest;
import vn.puzzleonline.server.repository.AccountRepository;
import vn.puzzleonline.server.repository.FriendRepository;

import java.util.List;
import java.util.Optional;

public class FriendService {
    private final FriendRepository friendRepo;
    private final AccountRepository accountRepo;

    public FriendService(FriendRepository friendRepo, AccountRepository accountRepo) {
        this.friendRepo = friendRepo;
        this.accountRepo = accountRepo;
    }

    public enum FriendResultCode {
        SUCCESS,
        AUTO_ACCEPTED,
        USER_NOT_FOUND,
        CANNOT_FRIEND_SELF,
        ALREADY_FRIENDS,
        REQUEST_ALREADY_PENDING,
        REQUEST_NOT_FOUND,
        INTERNAL_ERROR
    }

    public record FriendResult(FriendResultCode code, String message, Long requestId, String receiverUsername) {}

    /**
     * 1. Gửi lời mời kết bạn (Alice -> Bob)
     * Đảm bảo: Chống gửi trùng request, kiểm tra bạn bè, và tự động accept nếu đối phương đã gửi trước đó.
     */
    public synchronized FriendResult sendFriendRequest(long senderId, String receiverUsername) {
        if (receiverUsername == null || receiverUsername.trim().isBlank()) {
            return new FriendResult(FriendResultCode.USER_NOT_FOUND, "Tên tài khoản không hợp lệ.", null, null);
        }
        Optional<Account> receiverOpt = accountRepo.findByUsername(receiverUsername.trim());
        if (receiverOpt.isEmpty()) {
            return new FriendResult(FriendResultCode.USER_NOT_FOUND, "Không tìm thấy người chơi '" + receiverUsername + "'.", null, null);
        }
        Account receiver = receiverOpt.get();
        long receiverId = receiver.id();

        // Không được gửi cho chính mình
        if (senderId == receiverId) {
            return new FriendResult(FriendResultCode.CANNOT_FRIEND_SELF, "Bạn không thể gửi lời mời kết bạn cho chính mình.", null, receiver.username());
        }

        // Kiểm tra xem đã là bạn bè chưa
        if (friendRepo.isFriend(senderId, receiverId)) {
            return new FriendResult(FriendResultCode.ALREADY_FRIENDS, "Hai bạn đã là bạn bè.", null, receiver.username());
        }

        // 1. Kiểm tra xem Alice -> Bob đã có PENDING chưa (Chống trùng request)
        Long pendingId = friendRepo.findPendingRequestId(senderId, receiverId);
        if (pendingId != null) {
            return new FriendResult(FriendResultCode.REQUEST_ALREADY_PENDING, "Lời mời kết bạn đang chờ đối phương phản hồi.", pendingId, receiver.username());
        }

        // 2. Kiểm tra xem Bob -> Alice có đang PENDING không? (Gửi chéo)
        Long reversePendingId = friendRepo.findPendingRequestId(receiverId, senderId);
        if (reversePendingId != null) {
            // Đối phương đã từng gửi cho mình trước đó -> Tự động Chấp nhận luôn!
            boolean accepted = friendRepo.acceptFriendRequest(senderId, reversePendingId);
            if (accepted) {
                return new FriendResult(FriendResultCode.AUTO_ACCEPTED, "Đối phương đã gửi lời mời trước đó. Hai bạn đã trở thành bạn bè!", reversePendingId, receiver.username());
            }
        }

        // 3. Tạo lời mời mới với status = 'PENDING'
        long createdId = friendRepo.insertFriendRequest(senderId, receiverId);
        if (createdId > 0) {
            return new FriendResult(FriendResultCode.SUCCESS, "Đã gửi lời mời kết bạn tới " + receiver.username() + ".", createdId, receiver.username());
        } else {
            return new FriendResult(FriendResultCode.INTERNAL_ERROR, "Không thể tạo lời mời kết bạn lúc này.", null, receiver.username());
        }
    }

    /**
     * 2. Lấy danh sách lời mời đang chờ gửi tới Bob (receiver_id = Bob)
     */
    public List<FriendRequest> getPendingRequests(long receiverId) {
        return friendRepo.getPendingRequests(receiverId);
    }

    /**
     * 3. Bob Chấp nhận lời mời (ACCEPT) trong 1 Transaction an toàn
     */
    public boolean acceptFriendRequest(long receiverId, long requestId) {
        return friendRepo.acceptFriendRequest(receiverId, requestId);
    }

    /**
     * 4. Bob Từ chối lời mời (REJECT) - Vẫn lưu log lịch sử
     */
    public boolean rejectFriendRequest(long receiverId, long requestId) {
        return friendRepo.rejectFriendRequest(receiverId, requestId);
    }

    /**
     * 5. Alice Hủy lời mời đã gửi nhầm (CANCEL)
     */
    public boolean cancelFriendRequest(long senderId, long requestId) {
        return friendRepo.cancelFriendRequest(senderId, requestId);
    }

    /**
     * Lấy danh sách bạn bè 2 chiều
     */
    public List<String> getFriendUsernames(long userId) {
        return friendRepo.getFriendUsernames(userId);
    }
}
