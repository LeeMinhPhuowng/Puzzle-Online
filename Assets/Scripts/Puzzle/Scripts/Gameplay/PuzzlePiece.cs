using System;
using UnityEngine;

namespace PuzzleSystem.Gameplay
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PuzzlePiece : MonoBehaviour
    {
        [Header("Piece Identity")]
        public int pieceId;
        public int row;
        public int col;

        [Header("Target Snap Position (Local to Board)")]
        public Vector3 correctLocalPos;
        public Quaternion correctLocalRot = Quaternion.identity;

        [Header("State")]
        public bool isPlaced = false;
        public bool isBeingHeld = false;
        public string lockedByPlayer = "";

        [Header("Battle Mode Score")]
        public int points = 1;
        public bool isObscuredByInk = false;
        private TextMesh pointsTextMesh;
        private LineRenderer borderLine;
        private Material borderLineMaterial;

        [Header("Remote Sync")]
        private Vector3 targetRemotePos;
        private Quaternion targetRemoteRot;
        private bool hasRemoteTarget = false;

        public bool isRemoteControlled
        {
            get
            {
                if (string.IsNullOrEmpty(lockedByPlayer)) return false;
                string myName = PuzzleOnline.Core.ServiceLocator.Get<PuzzleOnline.Core.INetworkService>()?.Username;
                if (!string.IsNullOrEmpty(myName) && lockedByPlayer.Equals(myName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                return true;
            }
        }

        public int[] neighborIds = new int[4]; // 0: Top, 1: Right, 2: Bottom, 3: Left

        public event Action<PuzzlePiece> OnPiecePlaced;

        [Header("Lift & Remote Settings")]
        public float liftHeight = 0.035f;

        private Rigidbody rb;
        private Collider colComponent;
        private MeshRenderer meshRenderer;
        private Color originalColor = Color.white;
        private Material pieceMaterial;

        private void Awake()
        {
            // Tự động gán Layer "Piece" nếu đã tạo trong Project Settings
            int pieceLayer = LayerMask.NameToLayer("Piece");
            if (pieceLayer != -1)
            {
                gameObject.layer = pieceLayer;
            }

            rb = GetComponent<Rigidbody>();
            colComponent = GetComponent<Collider>();
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.material != null)
            {
                pieceMaterial = meshRenderer.material;
                if (pieceMaterial.HasProperty("_BaseColor")) originalColor = pieceMaterial.GetColor("_BaseColor");
                else if (pieceMaterial.HasProperty("_Color")) originalColor = pieceMaterial.color;
            }
            targetRemotePos = transform.localPosition;
            targetRemoteRot = transform.localRotation;
        }

        private void Update()
        {
            if (hasRemoteTarget && !isBeingHeld && !isPlaced)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetRemotePos, Time.deltaTime * 20f);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRemoteRot, Time.deltaTime * 20f);
                if (Vector3.Distance(transform.localPosition, targetRemotePos) < 0.0005f)
                {
                    hasRemoteTarget = false;
                }
            }
        }

        public void SetRemoteLock(string playerName)
        {
            string oldPlayer = lockedByPlayer;
            bool wasRemote = isRemoteControlled;
            lockedByPlayer = playerName ?? "";
            if (rb != null) rb.isKinematic = isRemoteControlled;

            var syncMgr = FindFirstObjectByType<PuzzlePlayerSyncManager>();
            if (syncMgr != null && syncMgr.RemoteAvatars != null)
            {
                if (isRemoteControlled && !wasRemote && !isPlaced)
                {
                    if (!string.IsNullOrEmpty(lockedByPlayer) && syncMgr.RemoteAvatars.TryGetValue(lockedByPlayer, out var avatar) && avatar != null)
                    {
                        avatar.GetComponent<PuzzleOnline.Gameplay.Competitive.PlayerAuraVisual>()?.SetAura(true, points);
                    }
                }
                else if (!isRemoteControlled && wasRemote)
                {
                    if (!string.IsNullOrEmpty(oldPlayer) && syncMgr.RemoteAvatars.TryGetValue(oldPlayer, out var oldAvatar) && oldAvatar != null)
                    {
                        oldAvatar.GetComponent<PuzzleOnline.Gameplay.Competitive.PlayerAuraVisual>()?.SetAura(false);
                    }
                }
            }

            if (isRemoteControlled && !wasRemote && !isPlaced)
            {
                // Khi người khác vừa nhấc mảnh lên: Nâng bổng mảnh lên không trung để mọi người đều nhìn thấy
                Vector3 current = transform.localPosition;
                current.y = correctLocalPos.y + (pieceId * 0.0003f) + liftHeight;
                targetRemotePos = current;
                hasRemoteTarget = true;

                // Đổi ánh màu báo hiệu đang có người giữ
                if (pieceMaterial != null)
                {
                    Color baseCol = isObscuredByInk ? new Color(0.08f, 0.08f, 0.08f, 1f) : originalColor;
                    Color heldColor = Color.Lerp(baseCol, new Color(1f, 0.75f, 0.4f), 0.45f);
                    if (pieceMaterial.HasProperty("_BaseColor")) pieceMaterial.SetColor("_BaseColor", heldColor);
                    else if (pieceMaterial.HasProperty("_Color")) pieceMaterial.color = heldColor;
                }
            }
            else if (!isRemoteControlled && wasRemote && !isPlaced)
            {
                // Khi người khác thả mảnh: Hạ mảnh xuống chạm mặt phẳng chuẩn của bàn
                Vector3 current = transform.localPosition;
                current.y = correctLocalPos.y + (pieceId * 0.0003f);
                targetRemotePos = current;
                hasRemoteTarget = true;

                // Khôi phục màu gốc (hoặc màu mực nếu vẫn đang trong thời gian bôi đen)
                if (pieceMaterial != null)
                {
                    Color targetCol = isObscuredByInk ? new Color(0.08f, 0.08f, 0.08f, 1f) : originalColor;
                    if (pieceMaterial.HasProperty("_BaseColor")) pieceMaterial.SetColor("_BaseColor", targetCol);
                    else if (pieceMaterial.HasProperty("_Color")) pieceMaterial.color = targetCol;
                }
            }
        }

        public void SetRemoteTarget(Vector3 localPos, Quaternion localRot)
        {
            if (isRemoteControlled && !isPlaced)
            {
                // Khi đang bị người khác kéo rê, giữ nguyên độ cao nâng bổng trên không
                localPos.y = correctLocalPos.y + (pieceId * 0.0003f) + liftHeight;
            }
            targetRemotePos = localPos;
            targetRemoteRot = localRot;
            hasRemoteTarget = true;
        }

        /// <summary>
        /// Thử kiểm tra và hút mảnh ghép vào vị trí đúng trên bàn cờ.
        /// </summary>
        public bool TrySnap(float posTolerance = 0.06f, float angleTolerance = 35f)
        {
            if (isPlaced) return true;

            Transform board = transform.parent;
            if (board == null) return false;

            Vector3 currentLocalPos = transform.localPosition;
            Quaternion currentLocalRot = transform.localRotation;

            // Tính khoảng cách trên mặt phẳng ngang X-Z của bàn cờ (bỏ qua độ nhấc Y khi đang được cầm)
            float distXZ = Vector2.Distance(
                new Vector2(currentLocalPos.x, currentLocalPos.z),
                new Vector2(correctLocalPos.x, correctLocalPos.z)
            );

            // Chênh lệch độ cao Y không quá 12cm
            float distY = Mathf.Abs(currentLocalPos.y - correctLocalPos.y);

            float angle = Quaternion.Angle(currentLocalRot, correctLocalRot);

            // Kiểm tra dung sai khoảng cách và góc xoay
            if (distXZ <= posTolerance && distY <= 0.12f && (angle <= angleTolerance || angle >= 360f - angleTolerance))
            {
                SnapToTarget();
                return true;
            }

            return false;
        }

        public void SnapToTarget()
        {
            if (isPlaced) return;
            isPlaced = true;
            isBeingHeld = false;
            if (pointsTextMesh != null) pointsTextMesh.gameObject.SetActive(false);
            if (borderLine != null) borderLine.enabled = false;
            UpdatePointsVisual();

            transform.localPosition = correctLocalPos;
            transform.localRotation = correctLocalRot;

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var localPlayer = FindFirstObjectByType<PlayerController.PlayerController>();
            if (localPlayer != null)
            {
                localPlayer.GetComponent<PuzzleOnline.Gameplay.Competitive.PlayerAuraVisual>()?.SetAura(false);
            }

            OnPiecePlaced?.Invoke(this);
            PuzzleOnline.Audio.AudioManager.Instance?.PlaySnap();
        }

        public void SetHeld(bool held)
        {
            isBeingHeld = held;
            if (rb != null)
            {
                rb.isKinematic = held;
            }

            var localPlayer = FindFirstObjectByType<PlayerController.PlayerController>();
            if (localPlayer != null)
            {
                var aura = localPlayer.GetComponent<PuzzleOnline.Gameplay.Competitive.PlayerAuraVisual>();
                if (aura != null)
                {
                    aura.SetAura(held, points);
                }
            }
        }

        public void Unfit(Vector3 newLocalPos, Quaternion newLocalRot)
        {
            isPlaced = false;
            isBeingHeld = false;
            hasRemoteTarget = false;
            transform.localPosition = newLocalPos;
            transform.localRotation = newLocalRot;
            targetRemotePos = newLocalPos;
            targetRemoteRot = newLocalRot;
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            UpdatePointsVisual();
        }

        public void SetPoints(int pts)
        {
            points = pts;
            UpdatePointsVisual();
        }

        private void EnsureBorderLine()
        {
            if (borderLine != null) return;

            var mesh = GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) return;

            var verts = mesh.vertices;
            var norms = mesh.normals;
            int topCount = 0;
            while (topCount < verts.Length && norms[topCount].y > 0.9f)
            {
                topCount++;
            }

            if (topCount < 3) return;

            var borderObj = new GameObject("RarityBorder");
            borderObj.transform.SetParent(transform, false);
            borderLine = borderObj.AddComponent<LineRenderer>();
            borderLine.useWorldSpace = false;
            borderLine.loop = true;
            borderLine.startWidth = 0.0025f;
            borderLine.endWidth = 0.0025f;
            borderLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            borderLine.receiveShadows = false;

            Shader s = Shader.Find("Universal Render Pipeline/Unlit") 
                    ?? Shader.Find("Sprites/Default") 
                    ?? Shader.Find("Unlit/Color");
            borderLineMaterial = new Material(s);
            borderLine.material = borderLineMaterial;

            borderLine.positionCount = topCount;
            for (int i = 0; i < topCount; i++)
            {
                Vector3 v = verts[i];
                v.y += 0.0006f; // Nhô lên 0.6mm so với mặt tranh để hiển thị sắc nét không bị z-fighting
                borderLine.SetPosition(i, v);
            }
        }

        public void UpdatePointsVisual()
        {
            if (pointsTextMesh != null)
            {
                pointsTextMesh.gameObject.SetActive(false);
            }

            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            bool isBattle = net != null ? net.IsBattleMode : true;
            bool showBorder = isBattle && !isPlaced;

            if (showBorder)
            {
                EnsureBorderLine();
                Color rarityColor = PieceRarity.GetColor(points);
                if (isObscuredByInk)
                {
                    rarityColor = new Color(0.12f, 0.12f, 0.12f, 1f);
                }

                if (borderLine != null)
                {
                    borderLine.startColor = rarityColor;
                    borderLine.endColor = rarityColor;
                    if (borderLineMaterial != null)
                    {
                        borderLineMaterial.color = rarityColor;
                        if (borderLineMaterial.HasProperty("_BaseColor")) borderLineMaterial.SetColor("_BaseColor", rarityColor);
                    }
                    borderLine.enabled = true;
                }

                // Tô màu cạnh bên 3D của mảnh ghép
                if (meshRenderer != null && meshRenderer.materials.Length > 1)
                {
                    var sideMat = meshRenderer.materials[1];
                    if (sideMat != null)
                    {
                        sideMat.color = rarityColor;
                        if (sideMat.HasProperty("_BaseColor")) sideMat.SetColor("_BaseColor", rarityColor);
                    }
                }
            }
            else
            {
                if (borderLine != null)
                {
                    borderLine.enabled = false;
                }

                if (meshRenderer != null && meshRenderer.materials.Length > 1)
                {
                    var sideMat = meshRenderer.materials[1];
                    if (sideMat != null)
                    {
                        Color cardboard = new Color(0.82f, 0.74f, 0.62f);
                        sideMat.color = cardboard;
                        if (sideMat.HasProperty("_BaseColor")) sideMat.SetColor("_BaseColor", cardboard);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (borderLineMaterial != null)
            {
                Destroy(borderLineMaterial);
            }
        }

        private Coroutine inkCoroutine;
        public void ApplyInkEffect(float duration)
        {
            if (inkCoroutine != null) StopCoroutine(inkCoroutine);
            inkCoroutine = StartCoroutine(InkRoutine(duration));
        }

        private System.Collections.IEnumerator InkRoutine(float duration)
        {
            isObscuredByInk = true;
            UpdatePointsVisual();
            if (meshRenderer != null && meshRenderer.material != null)
            {
                if (meshRenderer.material.HasProperty("_BaseColor"))
                    meshRenderer.material.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.08f, 1f));
                else if (meshRenderer.material.HasProperty("_Color"))
                    meshRenderer.material.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            }
            yield return new WaitForSeconds(duration);
            isObscuredByInk = false;
            UpdatePointsVisual();
            if (meshRenderer != null && meshRenderer.material != null)
            {
                if (meshRenderer.material.HasProperty("_BaseColor"))
                    meshRenderer.material.SetColor("_BaseColor", originalColor);
                else if (meshRenderer.material.HasProperty("_Color"))
                    meshRenderer.material.color = originalColor;
            }
            inkCoroutine = null;
        }
    }
}
