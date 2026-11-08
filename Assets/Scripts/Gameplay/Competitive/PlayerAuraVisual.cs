using System;
using UnityEngine;
using PuzzleSystem.Gameplay;

namespace PuzzleOnline.Gameplay.Competitive
{
    /// <summary>
    /// Điều khiển hiển thị Aura (vfx_LootDrop_02) dưới chân người chơi.
    /// Tự động bật khi người chơi nhấc/cầm mảnh ghép và đổi màu theo độ hiếm (1-5 điểm: Trắng, Xanh lá, Xanh dương, Tím, Vàng).
    /// Tự động tắt khi thả hoặc ghép mảnh xong.
    /// </summary>
    public class PlayerAuraVisual : MonoBehaviour
    {
        [SerializeField] private GameObject auraRoot;
        private ParticleSystem[] particleSystems;
        private bool isInitialized = false;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (isInitialized && auraRoot != null) return;

            // 1. Tìm vfx_LootDrop_02 trong các GameObject con
            if (auraRoot == null)
            {
                var t = transform.Find("vfx_LootDrop_02");
                if (t == null)
                {
                    foreach (Transform child in transform)
                    {
                        if (child.name.IndexOf("LootDrop", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            t = child;
                            break;
                        }
                    }
                }
                if (t != null) auraRoot = t.gameObject;
            }

            // 2. Nếu chưa có, nạp prefab từ Resources và khởi tạo làm con
            if (auraRoot == null)
            {
                var prefab = Resources.Load<GameObject>("vfx_LootDrop_02");
                if (prefab != null)
                {
                    auraRoot = Instantiate(prefab, transform);
                    auraRoot.name = "vfx_LootDrop_02";
                    auraRoot.transform.localPosition = Vector3.zero;
                    auraRoot.transform.localRotation = Quaternion.identity;
                    auraRoot.transform.localScale = new Vector3(2f, 2f, 2f);
                }
            }

            if (auraRoot != null)
            {
                particleSystems = auraRoot.GetComponentsInChildren<ParticleSystem>(true);
                // Mặc định ban đầu tắt aura (chỉ bật khi cầm mảnh)
                auraRoot.SetActive(false);
            }

            isInitialized = true;
        }

        private void OnDisable()
        {
            if (auraRoot != null && auraRoot.activeSelf) auraRoot.SetActive(false);
        }

        /// <summary>
        /// Kiểm tra xem phòng chơi có phải chế độ Hỗn Loạn (không có team, đua điểm tự do và bật aura) không.
        /// Chế độ thường (Classic) và Tranh Đấu chia team (Competitive) sẽ không bật Aura khi nhấc mảnh.
        /// </summary>
        public static bool IsWarfieldModeActive()
        {
            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net == null || net.CurrentRoom == null) return false;
            string mode = net.CurrentRoom.GameMode?.ToUpperInvariant() ?? "";
            return mode == "BATTLE" || mode == "MAYHEM" || mode == "WARFIELD";
        }

        public void SetAura(bool active, int points = 1)
        {
            EnsureInitialized();
            if (auraRoot == null) return;

            // Chỉ kích hoạt Aura khi nhấc mảnh trong chế độ Warfield / Competitive / Battle
            if (!active || !IsWarfieldModeActive())
            {
                if (auraRoot.activeSelf) auraRoot.SetActive(false);
                return;
            }

            Color color = PieceRarity.GetColor(points);

            if (particleSystems == null || particleSystems.Length == 0)
            {
                particleSystems = auraRoot.GetComponentsInChildren<ParticleSystem>(true);
            }

            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startColor = color;
            }

            if (!auraRoot.activeSelf)
            {
                auraRoot.SetActive(true);
            }

            // Xóa hạt cũ và phát lại ngay lập tức với màu mới
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                {
                    ps.Clear();
                    ps.Play();
                }
            }
        }
    }
}
