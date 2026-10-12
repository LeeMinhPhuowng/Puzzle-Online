using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PuzzleOnline.Network;
using PuzzleSystem.Gameplay;
using PuzzleOnline.Utility;

namespace PuzzleOnline.Gameplay.Competitive
{
    /// <summary>
    /// Điều phối viên hiển thị hiệu ứng phá hoại (VFX, Camera Shake, Flashbang, Banner Thắng Thua)
    /// và quản lý vòng đời của các vật phẩm Air-drop SaboteurWorldItem trong thế giới 3D.
    /// </summary>
    public class SabotageEffectsManager : MonoBehaviour
    {
        public static SabotageEffectsManager Instance { get; private set; }

        private readonly Dictionary<string, SaboteurWorldItem> spawnedItems = new Dictionary<string, SaboteurWorldItem>();

        // Flashbang
        private float flashAlpha = 0f;

        // Slap & Camera Shake
        private float shakeIntensity = 0f;
        private float slapHandAlpha = 0f;
        private Vector3 originalCameraPos;
        private Camera targetCamera;

        // Game Over Banner
        private string gameOverResult = "";
        private string gameOverTeam = "";
        private float gameOverTimer = 0f;

        // Role Announcement Banner
        private float roleBannerTimer = 0f;
        private string roleBannerTeam = "";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnItemSpawned += HandleItemSpawned;
                net.OnItemPickedUp += HandleItemPickedUp;
                net.OnSabotageTriggered += HandleSabotageTriggered;
                net.OnGameOverResult += HandleGameOverResult;
                net.OnGameStarted += HandleGameStarted;
            }
        }

        private void OnDisable()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnItemSpawned -= HandleItemSpawned;
                net.OnItemPickedUp -= HandleItemPickedUp;
                net.OnSabotageTriggered -= HandleSabotageTriggered;
                net.OnGameOverResult -= HandleGameOverResult;
                net.OnGameStarted -= HandleGameStarted;
            }
        }

        private void HandleGameStarted()
        {
            // Xóa sạch các vật phẩm cũ từ ván trước
            foreach (var item in spawnedItems.Values)
            {
                if (item != null) Destroy(item.gameObject);
            }
            spawnedItems.Clear();

            gameOverResult = "";
            gameOverTimer = 0f;

            var net = PuzzleNetworkManager.Instance;
            if (net != null && net.IsCompetitive)
            {
                roleBannerTeam = net.LocalPlayerTeam;
                roleBannerTimer = 6f; // Hiển thị 6 giây đầu trận
            }
            else
            {
                roleBannerTimer = 0f;
                roleBannerTeam = "";
            }

            // Kiểm tra xem đã có sẵn các item trong snapshot ván chơi không
            if (net != null && net.ActiveWorldItems != null)
            {
                foreach (var info in net.ActiveWorldItems)
                {
                    HandleItemSpawned(info);
                }
            }
        }

        private void HandleItemSpawned(WorldItemInfo info)
        {
            if (info == null || spawnedItems.ContainsKey(info.Id)) return;

            var go = new GameObject($"WorldItem_{info.ItemType}_{info.Id}");
            var itemComp = go.AddComponent<SaboteurWorldItem>();
            itemComp.Initialize(info);
            spawnedItems[info.Id] = itemComp;
        }

        private void HandleItemPickedUp(string itemId, string itemType, string byUser)
        {
            if (spawnedItems.TryGetValue(itemId, out var item))
            {
                spawnedItems.Remove(itemId);
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }
        }

        private void HandleSabotageTriggered(string itemType, string targetUser, string byUser, float duration)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            bool isMe = string.Equals(targetUser, net.Username, StringComparison.OrdinalIgnoreCase);

            switch (itemType)
            {
                case "FLASHLIGHT":
                    if (isMe)
                    {
                        flashAlpha = 1f;
                        PuzzleOnline.Audio.AudioManager.Instance?.PlaySnap();
                    }
                    break;

                case "SLAP":
                    if (isMe)
                    {
                        shakeIntensity = 0.4f;
                        slapHandAlpha = 1f;

                        // Làm choáng và rơi mảnh đang cầm
                        var interactor = FindFirstObjectByType<PuzzleRaycastInteractor>();
                        if (interactor != null)
                        {
                            interactor.Stun(duration);
                        }
                    }
                    break;

                case "MAGNET":
                    if (isMe)
                    {
                        var interactor = FindFirstObjectByType<PuzzleRaycastInteractor>();
                        if (interactor != null)
                        {
                            interactor.ApplyMagnetDebuff(6f);
                        }
                    }
                    break;
            }
        }

        private void HandleGameOverResult(string result, string winnerTeam)
        {
            gameOverResult = result;
            gameOverTeam = winnerTeam;
            gameOverTimer = 10f; // Hiển thị trong 10 giây
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            // Flashbang fade out
            if (flashAlpha > 0f)
            {
                flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.deltaTime * 0.35f);
            }

            // Slap hand fade out
            if (slapHandAlpha > 0f)
            {
                slapHandAlpha = Mathf.MoveTowards(slapHandAlpha, 0f, Time.deltaTime * 0.5f);
            }

            // Camera shake
            if (shakeIntensity > 0f)
            {
                if (targetCamera == null) targetCamera = Camera.main;
                if (targetCamera != null)
                {
                    targetCamera.transform.position += UnityEngine.Random.insideUnitSphere * shakeIntensity * 0.1f;
                }
                shakeIntensity = Mathf.MoveTowards(shakeIntensity, 0f, Time.deltaTime * 1.5f);
            }

            // Game over timer countdown
            if (gameOverTimer > 0f)
            {
                gameOverTimer -= Time.deltaTime;
            }

            // Role banner countdown
            if (roleBannerTimer > 0f)
            {
                roleBannerTimer -= Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            using var viewport = new ResponsiveGUI(fixedLayout: false);
            // Banner Thông Báo Vai Trò (Role Reveal) đầu trận đấu Cạnh Tranh
            if (roleBannerTimer > 0f && !string.IsNullOrEmpty(roleBannerTeam))
            {
                DrawRoleRevealBanner();
            }

            // Hiệu ứng Flashbang (màn hình trắng mờ dần)
            if (flashAlpha > 0f)
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, flashAlpha);
                GUI.DrawTexture(new Rect(0, 0, ResponsiveGUI.LogicalWidth, ResponsiveGUI.LogicalHeight), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }

            // Hiệu ứng Slap Glove (dấu bàn tay đỏ to tướng)
            if (slapHandAlpha > 0f)
            {
                var slapStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 120,
                    alignment = TextAnchor.MiddleCenter
                };
                slapStyle.normal.textColor = new Color(1f, 0.2f, 0.2f, slapHandAlpha);
                GUI.Label(new Rect(0, 0, ResponsiveGUI.LogicalWidth, ResponsiveGUI.LogicalHeight), "💥🧤 BỐP!", slapStyle);

                var stunStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                stunStyle.normal.textColor = new Color(1f, 0.9f, 0.2f, slapHandAlpha);
                GUI.Label(new Rect(0, ResponsiveGUI.LogicalHeight * 0.65f, ResponsiveGUI.LogicalWidth, 40f), "BẠN ĐÃ BỊ TÁT CHOÁNG! KHÔNG THỂ CẦM MẢNH GHÉP!", stunStyle);
            }

            // Banner Game Over Thắng/Thua
            if (gameOverTimer > 0f && !string.IsNullOrEmpty(gameOverResult))
            {
                DrawGameOverBanner();
            }
        }

        private void DrawRoleRevealBanner()
        {
            float bannerWidth = 720f;
            float bannerHeight = 130f;
            float bx = (ResponsiveGUI.LogicalWidth - bannerWidth) / 2f;
            float by = 60f;

            bool isSaboteur = (roleBannerTeam == "SABOTEUR");

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = isSaboteur ? new Color(0.6f, 0.1f, 0.7f, 0.95f) : new Color(0.1f, 0.45f, 0.8f, 0.95f);

            var boxStyle = new GUIStyle(GUI.skin.box);
            GUI.Box(new Rect(bx, by, bannerWidth, bannerHeight), GUIContent.none, boxStyle);
            GUI.backgroundColor = oldBg;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = isSaboteur ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.9f, 0.2f);

            string title = isSaboteur ? "😈 BẠN ĐÃ ĐƯỢC CHỈ ĐỊNH: ĐỘI PHÁ HOẠI!" : "🔨 BẠN ĐÃ ĐƯỢC CHỈ ĐỊNH: ĐỘI THỢ XÂY!";
            GUI.Label(new Rect(bx, by + 12f, bannerWidth, 40f), title, titleStyle);

            var descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            descStyle.normal.textColor = Color.white;

            string desc = isSaboteur
                ? "Di chuyển nhặt các vật phẩm rơi quanh bàn [E] để cản trở đội thợ xây!"
                : "Tập trung phối hợp ghép hoàn thành bức tranh trước khi hết thời gian!";
            GUI.Label(new Rect(bx, by + 58f, bannerWidth, 30f), desc, descStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 0.8f);
            string hint = isSaboteur ? "Phím tắt chọn item trong túi: [1], [2], [3]" : "Cẩn thận các cạm bẫy phá hoại từ đối thủ!";
            GUI.Label(new Rect(bx, by + 92f, bannerWidth, 25f), hint, subStyle);
        }

        private void DrawGameOverBanner()
        {
            float bannerWidth = 650f;
            float bannerHeight = 160f;
            float bx = (ResponsiveGUI.LogicalWidth - bannerWidth) / 2f;
            float by = ResponsiveGUI.LogicalHeight * 0.25f;

            bool buildersWon = (gameOverResult == "BUILDERS_WIN");

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = buildersWon ? new Color(0.1f, 0.7f, 0.2f, 0.95f) : new Color(0.8f, 0.15f, 0.15f, 0.95f);

            var boxStyle = new GUIStyle(GUI.skin.box);
            GUI.Box(new Rect(bx, by, bannerWidth, bannerHeight), GUIContent.none, boxStyle);
            GUI.backgroundColor = oldBg;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.yellow;

            string title = buildersWon ? "🏆 ĐỘI THỢ XÂY CHIẾN THẮNG! 🏆" : "😈 ĐỘI PHÁ HOẠI CHIẾN THẮNG! 😈";
            GUI.Label(new Rect(bx, by + 15f, bannerWidth, 45f), title, titleStyle);

            var descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            descStyle.normal.textColor = Color.white;

            string desc = buildersWon
                ? "Bức tranh đã được hoàn thành trước khi hết thời gian!"
                : "Hết thời gian mục tiêu! Tranh chưa được hoàn thành!";
            GUI.Label(new Rect(bx, by + 70f, bannerWidth, 35f), desc, descStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = Color.cyan;
            GUI.Label(new Rect(bx, by + 115f, bannerWidth, 25f), "Chủ phòng có thể bắt đầu lại hoặc quay về sảnh chờ.", subStyle);
        }
    }
}
