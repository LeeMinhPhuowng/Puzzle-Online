using System;
using System.Collections.Generic;
using UnityEngine;
using PuzzleOnline.Utility;

namespace PuzzleSystem.Gameplay
{
    /// <summary>
    /// Quản lý vị trí ghế ngồi cho người chơi cục bộ và đồng bộ Avatar kèm vị trí di chuyển thực tế (real-time position/movement)
    /// của các người chơi khác trong phòng online.
    /// </summary>
    public class PuzzlePlayerSyncManager : MonoBehaviour
    {
        [Header("Seat Configurations")]
        [Tooltip("Danh sách vị trí ghế ngồi (nếu để trống sẽ tự động dò tìm bằng SeatFinder)")]
        public List<Transform> chairTransforms = new List<Transform>();

        private readonly Dictionary<string, RemotePlayerAvatar> _remoteAvatars =
            new Dictionary<string, RemotePlayerAvatar>(StringComparer.OrdinalIgnoreCase);

        private bool playerPositioned = false;

        public Dictionary<string, RemotePlayerAvatar> RemoteAvatars => _remoteAvatars;

        private void OnEnable()
        {
            RegisterNetworkEvents();
        }

        private void OnDisable()
        {
            UnregisterNetworkEvents();
            ClearRemotePlayerAvatars();
        }

        private void RegisterNetworkEvents()
        {
            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net == null) return;

            net.OnPlayerLookReceived += HandleRemotePlayerLook;
            net.OnPlayerPoseReceived += HandleRemotePlayerPose;
            net.OnRoomUpdated += HandleRoomUpdatedAvatars;
        }

        private void UnregisterNetworkEvents()
        {
            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net == null) return;

            net.OnPlayerLookReceived -= HandleRemotePlayerLook;
            net.OnPlayerPoseReceived -= HandleRemotePlayerPose;
            net.OnRoomUpdated -= HandleRoomUpdatedAvatars;
        }

        private void HandleRoomUpdatedAvatars()
        {
            SyncRemotePlayerAvatars();
        }

        public void HandleRemotePlayerLook(string username, float yaw, float pitch)
        {
            if (string.IsNullOrEmpty(username)) return;

            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net != null && string.Equals(username.Trim(), net.Username?.Trim(), StringComparison.OrdinalIgnoreCase))
                return;

            if (_remoteAvatars.TryGetValue(username, out var avatar) && avatar != null)
            {
                avatar.SetTargetLook(yaw, pitch);
            }
        }

        public void HandleRemotePlayerPose(string username, Vector3 position, float yaw, float pitch)
        {
            if (string.IsNullOrEmpty(username)) return;

            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net != null && string.Equals(username.Trim(), net.Username?.Trim(), StringComparison.OrdinalIgnoreCase))
                return;

            if (_remoteAvatars.TryGetValue(username, out var avatar) && avatar != null)
            {
                avatar.SetTargetPose(position, yaw, pitch);
            }
        }

        public List<Transform> GetEffectiveSeatTargets()
        {
            var seats = SeatFinder.FindSeats();
            if (seats.Count > 0) return seats;

            if (chairTransforms != null && chairTransforms.Count > 0)
            {
                var result = new List<Transform>();
                foreach (var t in chairTransforms)
                {
                    if (t != null) result.Add(t);
                }
                if (result.Count > 0) return result;
            }

            return new List<Transform>();
        }

        /// <summary>
        /// Đặt người chơi cục bộ vào ghế ngồi tương ứng với thứ tự slot trong phòng.
        /// </summary>
        public void PositionLocalPlayer(bool force = false)
        {
            if (playerPositioned && !force) return;

            GameObject playerObj = null;
            var pc = FindFirstObjectByType<PlayerController.PlayerController>();
            if (pc != null)
            {
                playerObj = pc.gameObject;
            }
            else
            {
                playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            }

            if (playerObj == null) return;

            // Nếu đang test trực tiếp không qua room server, không force ép vị trí Player vào ghế
            var netCheck = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (netCheck == null || (netCheck.CurrentRoom == null && !netCheck.IsGameActive))
            {
                return;
            }

            var seats = GetEffectiveSeatTargets();
            if (seats == null || seats.Count == 0)
            {
                Debug.LogWarning("[PuzzlePlayerSyncManager] Không tìm thấy ghế ngồi nào để định vị Player!");
                return;
            }

            int slotIndex = 0;
            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net != null && net.RoomPlayers != null && net.RoomPlayers.Count > 0)
            {
                slotIndex = SeatFinder.GetSlotIndex(net.Username, net.RoomPlayers);
            }

            Transform seatTarget = seats[slotIndex % seats.Count];

            if (seatTarget != null)
            {
                var positionsParent = SeatFinder.FindPositionsParent();
                Vector3 lookTarget = positionsParent != null ? positionsParent.position : transform.position;
                Vector3 lookDirection = lookTarget - seatTarget.position;
                lookDirection.y = 0f;

                Quaternion lookRotation = lookDirection.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                    : seatTarget.rotation;

                if (pc != null)
                {
                    pc.TeleportTo(seatTarget.position, lookRotation);
                }
                else
                {
                    var cc = playerObj.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    playerObj.transform.position = seatTarget.position;
                    playerObj.transform.rotation = lookRotation;
                    Physics.SyncTransforms();
                    if (cc != null) cc.enabled = true;
                }

                playerPositioned = true;
                Debug.Log($"<color=cyan>[PuzzlePlayerSyncManager] Đã định vị Player vào ghế '{seatTarget.name}' (Slot {slotIndex + 1})</color>");
            }
        }

        private static readonly Color[] AvatarColors = new Color[]
        {
            new Color(0.35f, 0.75f, 1f, 1f),   // Cyan
            new Color(0.45f, 0.85f, 0.45f, 1f), // Green
            new Color(1f, 0.75f, 0.35f, 1f),   // Orange/Gold
            new Color(0.85f, 0.5f, 1f, 1f),    // Purple
            new Color(1f, 0.45f, 0.6f, 1f),    // Pink
            new Color(0.95f, 0.9f, 0.4f, 1f),  // Yellow
        };

        /// <summary>
        /// Khởi tạo và cập nhật Avatar của các bạn chơi khác trong phòng.
        /// </summary>
        public void SyncRemotePlayerAvatars()
        {
            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net == null) return;
            string myName = net.Username?.Trim();
            var players = net.RoomPlayers;
            if (players == null || players.Count == 0) return;

            GameObject localPlayerObj = null;
            var pc = FindFirstObjectByType<PlayerController.PlayerController>();
            if (pc != null) localPlayerObj = pc.gameObject;
            else localPlayerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");

            if (localPlayerObj == null) return;

            var seats = GetEffectiveSeatTargets();
            var activeUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null) continue;
                if (string.Equals(p.Username?.Trim(), myName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!p.Connected) continue;

                activeUsers.Add(p.Username);

                Transform seatTarget = (seats != null && seats.Count > 0) ? seats[i % seats.Count] : null;
                Vector3 targetPos = seatTarget != null ? seatTarget.position : localPlayerObj.transform.position;
                Quaternion targetRot = seatTarget != null ? seatTarget.rotation : Quaternion.identity;

                if (!_remoteAvatars.TryGetValue(p.Username, out var avatar) || avatar == null)
                {
                    GameObject remoteObj = Instantiate(localPlayerObj, targetPos, targetRot);
                    remoteObj.name = "[Avatar_" + p.Username + "]";
                    remoteObj.tag = "Untagged";

                    // 1. Xóa triệt để các GameObject Camera con (chứa Camera, FirstPersonCameraScript,
                    // AudioListener, PuzzleRaycastInteractor...). Tránh việc script điều khiển trên avatar clone
                    // tiếp tục nhận chuột local player và gọi SendLook() gây xung đột tọa độ giật cục!
                    var cams = remoteObj.GetComponentsInChildren<Camera>(true);
                    foreach (var c in cams)
                    {
                        Destroy(c.gameObject);
                    }

                    // Quét sạch các script điều khiển còn sót lại nếu có
                    var fpsScripts = remoteObj.GetComponentsInChildren<FirstPersonCamera.FirstPersonCameraScript>(true);
                    foreach (var fps in fpsScripts) Destroy(fps);

                    var pcs = remoteObj.GetComponentsInChildren<PlayerController.PlayerController>(true);
                    foreach (var pcComp in pcs) Destroy(pcComp);

                    var ccs = remoteObj.GetComponentsInChildren<CharacterController>(true);
                    foreach (var ccComp in ccs) Destroy(ccComp);

                    // 2. Xóa TẤT CẢ Colliders trên Avatar để tránh xung đột vật lý với CharacterController
                    // của người chơi thật (nguyên nhân gây rung giật kẹt người) và tránh chắn tia raycast mảnh ghép
                    var colliders = remoteObj.GetComponentsInChildren<Collider>(true);
                    foreach (var col in colliders)
                    {
                        Destroy(col);
                    }

                    // 3. Khôi phục hiển thị 3D cho toàn bộ MeshRenderer!
                    // Do FirstPersonCameraScript trên local player đã set shadowCastingMode = ShadowsOnly,
                    // nếu không bật lại thì client khác sẽ chỉ thấy mỗi cái bóng đổ mà không thấy người!
                    var renderers = remoteObj.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        r.receiveShadows = true;
                        r.enabled = true;
                    }

                    // 4. Tìm Head transform (chứa mắt Eye1, Eye1 (1)) để avatar gật/ngước theo pitch
                    Transform headObj = remoteObj.transform.Find("Head");

                    avatar = remoteObj.AddComponent<RemotePlayerAvatar>();
                    avatar.Setup(p.Username, headObj);
                    avatar.SetAvatarColor(AvatarColors[i % AvatarColors.Length]);
                    _remoteAvatars[p.Username] = avatar;

                    Debug.Log($"<color=green>[PuzzlePlayerSyncManager] Đã tạo Avatar 3D cho bạn chơi: {p.Username}</color>");
                }
            }

            // Dọn dẹp avatar của người chơi đã rời phòng
            var toRemove = new List<string>();
            foreach (var kvp in _remoteAvatars)
            {
                if (!activeUsers.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                    if (kvp.Value != null)
                    {
                        Destroy(kvp.Value.gameObject);
                    }
                }
            }
            foreach (var rem in toRemove)
            {
                _remoteAvatars.Remove(rem);
            }
        }

        public void ClearRemotePlayerAvatars()
        {
            foreach (var kvp in _remoteAvatars)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _remoteAvatars.Clear();
        }
    }
}
