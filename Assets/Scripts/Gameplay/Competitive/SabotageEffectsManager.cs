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
        private string gameOverWinner = "";
        private int gameOverScore = 0;
        private float gameOverTimer = 0f;

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
            gameOverTeam = "";
            gameOverWinner = "";
            gameOverScore = 0;
            gameOverTimer = 0f;

            var net = PuzzleNetworkManager.Instance;

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

        private void HandleSabotageTriggered(string itemType, string targetUser, string byUser, float duration, float force)
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
                        // Kích hoạt rung giật camera mạnh mẽ qua FirstPersonCameraScript
                        var camScript = FirstPersonCamera.FirstPersonCameraScript.Instance ?? FindFirstObjectByType<FirstPersonCamera.FirstPersonCameraScript>();
                        if (camScript != null)
                        {
                            camScript.TriggerExternalShake(ItemConfig.GloveCameraShakeIntensity, ItemConfig.GloveCameraShakeDuration);
                        }
                        shakeIntensity = ItemConfig.GloveCameraShakeIntensity;

                        // Làm rơi mảnh đang cầm (nếu có)
                        var interactor = FindFirstObjectByType<PuzzleRaycastInteractor>();
                        if (interactor != null)
                        {
                            interactor.ForceDropHeldPiece();
                        }

                        // Hất tung người chơi lên trời theo phương thẳng đứng (trục Y) theo cấu hình ItemConfig
                        var playerCtrl = FindFirstObjectByType<PlayerController.PlayerController>();
                        if (playerCtrl != null)
                        {
                            float knockback = force > 0f ? force : ItemConfig.GloveKnockbackForce;
                            playerCtrl.LaunchUpward(knockback);
                        }
                    }
                    break;

                case "MAGNET":
                    if (isMe)
                    {
                        var interactor = FindFirstObjectByType<PuzzleRaycastInteractor>();
                        if (interactor != null)
                        {
                            interactor.ForceDropHeldPiece();
                        }
                    }
                    break;
            }
        }

        private void HandleGameOverResult(string result, string winnerTeam, string winner, int score)
        {
            gameOverResult = result;
            gameOverTeam = winnerTeam;
            gameOverWinner = winner;
            gameOverScore = score;
            gameOverTimer = 10f; // Hiển thị trong 10 giây
            CursorManager.Unlock();
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

        }

        private void OnGUI()
        {
            using var viewport = new ResponsiveGUI(fixedLayout: false);
            // Hiệu ứng Flashbang (màn hình trắng mờ dần)
            if (flashAlpha > 0f)
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, flashAlpha);
                GUI.DrawTexture(new Rect(0, 0, ResponsiveGUI.LogicalWidth, ResponsiveGUI.LogicalHeight), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }

            // Hiệu ứng Slap Glove (Bốp) đã được tắt theo yêu cầu người dùng (chỉ giữ hiệu ứng hất tung và rung camera)
            // Banner Game Over Thắng/Thua
            if (gameOverTimer > 0f && !string.IsNullOrEmpty(gameOverResult))
            {
                DrawGameOverBanner();
            }
        }



        private void DrawGameOverBanner()
        {
            float bannerWidth = 650f;
            float bannerHeight = 160f;
            float bx = (ResponsiveGUI.LogicalWidth - bannerWidth) / 2f;
            float by = ResponsiveGUI.LogicalHeight * 0.25f;

            bool isBattle = gameOverResult == "BATTLE_WIN" || gameOverResult == "BATTLE_TIMEOUT" || gameOverTeam == "BATTLE";
            bool buildersWon = (gameOverResult == "BUILDERS_WIN");

            Color oldBg = GUI.backgroundColor;
            if (isBattle)
            {
                GUI.backgroundColor = new Color(0.95f, 0.65f, 0.1f, 0.95f); // Vàng kim rực rỡ
            }
            else
            {
                GUI.backgroundColor = buildersWon ? new Color(0.1f, 0.7f, 0.2f, 0.95f) : new Color(0.8f, 0.15f, 0.15f, 0.95f);
            }

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

            string title;
            if (isBattle)
            {
                title = !string.IsNullOrEmpty(gameOverWinner)
                    ? $"👑 {gameOverWinner.ToUpper()} CHIẾN THẮNG! 👑"
                    : "👑 KẾT THÚC TRANH ĐẤU! 👑";
            }
            else
            {
                title = buildersWon ? "🏆 ĐỘI THỢ XÂY CHIẾN THẮNG! 🏆" : "😈 ĐỘI PHÁ HOẠI CHIẾN THẮNG! 😈";
            }
            GUI.Label(new Rect(bx, by + 15f, bannerWidth, 45f), title, titleStyle);

            var descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            descStyle.normal.textColor = Color.white;

            string desc;
            if (isBattle)
            {
                string leadText = !string.IsNullOrEmpty(gameOverWinner) ? $"{gameOverWinner} dẫn đầu với {gameOverScore} điểm!" : "";
                if (gameOverResult == "BATTLE_WIN")
                    desc = $"Bức tranh đã được hoàn thành! {leadText}";
                else
                    desc = $"Hết thời gian tranh đấu! {leadText}";
            }
            else
            {
                desc = buildersWon
                    ? "Bức tranh đã được hoàn thành trước khi hết thời gian!"
                    : "Hết thời gian mục tiêu! Tranh chưa được hoàn thành!";
            }
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
