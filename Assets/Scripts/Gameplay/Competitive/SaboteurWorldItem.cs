using System;
using UnityEngine;
using PuzzleOnline.Network;

namespace PuzzleOnline.Gameplay.Competitive
{
    /// <summary>
    /// Vật phẩm rơi từ trên trời xuống sàn quanh bàn chơi trong Chế độ Cạnh Tranh (Competitive Mode).
    /// Thành viên Đội Phá Hoại (Saboteur) có thể lại gần và nhấn [E] để nhặt vào túi đồ.
    /// </summary>
    public class SaboteurWorldItem : MonoBehaviour
    {
        public string ItemId { get; private set; }
        public string ItemType { get; private set; }

        private Vector3 groundPosition;
        private bool hasLanded = false;
        private float fallSpeed = 9f;
        private float bobTimer = 0f;

        private GameObject visualObject;
        private TextMesh labelMesh;
        private Light itemLight;

        public static event Action<SaboteurWorldItem> OnItemNearby;

        public void Initialize(WorldItemInfo info)
        {
            ItemId = info.Id;
            ItemType = info.ItemType;
            groundPosition = new Vector3(info.X, info.Y + 0.15f, info.Z);

            // Bắt đầu từ trên cao 6 mét để rơi xuống
            transform.position = new Vector3(info.X, info.Y + 6f, info.Z);

            CreateVisual();
        }

        private void CreateVisual()
        {
            visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(transform, false);

            Color mainColor = Color.yellow;
            string displayName = "Hộp Quà 🎁";

            if (ItemType == "GIFTBOX")
            {
                mainColor = new Color(1f, 0.3f, 0.3f);
                displayName = "🎁 Hộp Quà Bí Mật";
                LoadOrFallbackModel("Items/GiftboxModel", visualObject, 0.015f, mainColor, CreateGiftboxFallback);
            }
            else if (ItemType == "FLASHLIGHT")
            {
                mainColor = new Color(1f, 0.9f, 0.2f);
                displayName = "🔦 Đèn Pin";
                LoadOrFallbackModel("Items/FlashlightModel", visualObject, 0.15f, mainColor, CreateFlashlightMesh);
            }
            else if (ItemType == "INK")
            {
                mainColor = new Color(0.2f, 0.2f, 0.3f);
                displayName = "🧪 Bình Mực";
                var potionGo = LoadOrFallbackModel("Items/PotionModel", visualObject, 0.2f, mainColor, CreateInkMesh);
                if (potionGo != null)
                {
                    CustomizePotionMaterials(potionGo);
                }
            }
            else if (ItemType == "SLAP")
            {
                mainColor = new Color(0.9f, 0.2f, 0.2f);
                displayName = "🧤 Găng Tay";
                LoadOrFallbackModel("Items/GloveModel", visualObject, 0.12f, mainColor, CreateGloveMesh);
            }
            else if (ItemType == "MAGNET")
            {
                mainColor = new Color(0.2f, 0.5f, 1f);
                displayName = "🧲 Nam Châm";
                LoadOrFallbackModel("Items/MagnetModel", visualObject, 0.15f, mainColor, CreateMagnetMesh);
            }
            else if (ItemType == "SHOVEL")
            {
                mainColor = new Color(0.8f, 0.8f, 0.85f);
                displayName = "⛏️ Xà Beng";
                LoadOrFallbackModel("Items/CrowbarModel", visualObject, 0.15f, mainColor, CreateShovelMesh);
            }

            // Tạo PointLight tỏa sáng nhẹ
            var lightObj = new GameObject("ItemLight");
            lightObj.transform.SetParent(visualObject.transform, false);
            itemLight = lightObj.AddComponent<Light>();
            itemLight.type = LightType.Point;
            itemLight.color = mainColor;
            itemLight.range = 2.5f;
            itemLight.intensity = 1.2f;

            // Tạo Label chữ 3D nổi trên đầu vật phẩm
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(transform, false);
            labelObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelMesh = labelObj.AddComponent<TextMesh>();
            labelMesh.text = displayName;
            labelMesh.characterSize = 0.08f;
            labelMesh.fontSize = 32;
            labelMesh.alignment = TextAlignment.Center;
            labelMesh.anchor = TextAnchor.MiddleCenter;
            labelMesh.color = Color.white;

            // Box collider để nhấp click hoặc trigger
            var col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(0.8f, 1.2f, 0.8f);
            col.isTrigger = true;
        }

        private GameObject LoadOrFallbackModel(string resourcePath, GameObject parent, float scale, Color fallbackColor, Action<GameObject, Color> fallbackAction)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, parent.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * scale;

                // Tắt tất cả collider trên model con để không cản trở raycast trigger
                foreach (var c in instance.GetComponentsInChildren<Collider>())
                {
                    Destroy(c);
                }
                return instance;
            }
            else
            {
                fallbackAction(parent, fallbackColor);
                return null;
            }
        }

        private void CustomizePotionMaterials(GameObject potionInstance)
        {
            if (potionInstance == null) return;

            // Model có 3 mesh con:
            // 1) "glass": Bình thủy tinh bên ngoài -> làm trong suốt (transparent glass)
            // 2) "inside": Dung dịch bên trong -> làm đen tuyền (black ink)
            // 3) "probka": Nút bần ở cổ chai -> giữ nguyên hoặc màu gỗ nắp chai
            Shader standardShader = Shader.Find("Standard");

            foreach (var renderer in potionInstance.GetComponentsInChildren<Renderer>())
            {
                string rName = renderer.gameObject.name.ToLower();

                if (rName.Contains("glass"))
                {
                    // Tạo vật liệu thủy tinh trong suốt (Transparent glass)
                    Material glassMat = new Material(standardShader);
                    glassMat.SetFloat("_Mode", 3f); // 3 = Transparent mode
                    glassMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    glassMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    glassMat.SetInt("_ZWrite", 0);
                    glassMat.DisableKeyword("_ALPHATEST_ON");
                    glassMat.DisableKeyword("_ALPHABLEND_ON");
                    glassMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    glassMat.renderQueue = 3000;

                    // Màu kính: xanh nhạt phớt nhẹ hoặc trắng hơi xanh, alpha mỏng 0.25f
                    Color glassCol = new Color(0.7f, 0.85f, 0.95f, 0.28f);
                    glassMat.color = glassCol;
                    if (glassMat.HasProperty("_BaseColor")) glassMat.SetColor("_BaseColor", glassCol);
                    glassMat.SetFloat("_Metallic", 0.1f);
                    glassMat.SetFloat("_Glossiness", 0.92f); // Bề mặt bóng loáng như thủy tinh

                    renderer.material = glassMat;
                }
                else if (rName.Contains("inside"))
                {
                    // Tạo mực đen tuyền bên trong (Dark black ink liquid)
                    Material inkMat = new Material(standardShader);
                    Color inkCol = new Color(0.04f, 0.04f, 0.06f, 1f); // Đen tuyền
                    inkMat.color = inkCol;
                    if (inkMat.HasProperty("_BaseColor")) inkMat.SetColor("_BaseColor", inkCol);
                    inkMat.SetFloat("_Metallic", 0.05f);
                    inkMat.SetFloat("_Glossiness", 0.85f); // Chất lỏng có độ bóng

                    renderer.material = inkMat;
                }
                else if (rName.Contains("probka"))
                {
                    // Nút chai gỗ bần màu nâu
                    Material corkMat = new Material(standardShader);
                    Color corkCol = new Color(0.55f, 0.38f, 0.22f, 1f);
                    corkMat.color = corkCol;
                    if (corkMat.HasProperty("_BaseColor")) corkMat.SetColor("_BaseColor", corkCol);
                    corkMat.SetFloat("_Metallic", 0f);
                    corkMat.SetFloat("_Glossiness", 0.15f);

                    renderer.material = corkMat;
                }
            }
        }

        private void CreateGiftboxFallback(GameObject parent, Color col)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(parent.transform, false);
            box.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            SetColor(box, col);
            DestroyCollider(box);
        }

        private void CreateFlashlightMesh(GameObject parent, Color col)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(parent.transform, false);
            body.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);
            body.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            SetColor(body, col);
            DestroyCollider(body);
        }

        private void CreateInkMesh(GameObject parent, Color col)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(parent.transform, false);
            body.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            SetColor(body, col);
            DestroyCollider(body);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.transform.SetParent(parent.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            cap.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
            SetColor(cap, Color.black);
            DestroyCollider(cap);
        }

        private void CreateGloveMesh(GameObject parent, Color col)
        {
            var hand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hand.transform.SetParent(parent.transform, false);
            hand.transform.localScale = new Vector3(0.25f, 0.35f, 0.08f);
            SetColor(hand, col);
            DestroyCollider(hand);
        }

        private void CreateMagnetMesh(GameObject parent, Color col)
        {
            var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.transform.SetParent(parent.transform, false);
            left.transform.localPosition = new Vector3(-0.1f, 0f, 0f);
            left.transform.localScale = new Vector3(0.08f, 0.3f, 0.08f);
            SetColor(left, Color.red);
            DestroyCollider(left);

            var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.transform.SetParent(parent.transform, false);
            right.transform.localPosition = new Vector3(0.1f, 0f, 0f);
            right.transform.localScale = new Vector3(0.08f, 0.3f, 0.08f);
            SetColor(right, Color.blue);
            DestroyCollider(right);

            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.transform.SetParent(parent.transform, false);
            top.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            top.transform.localScale = new Vector3(0.28f, 0.08f, 0.08f);
            SetColor(top, Color.gray);
            DestroyCollider(top);
        }

        private void CreateShovelMesh(GameObject parent, Color col)
        {
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.transform.SetParent(parent.transform, false);
            handle.transform.localScale = new Vector3(0.04f, 0.4f, 0.04f);
            handle.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            SetColor(handle, new Color(0.6f, 0.4f, 0.2f));
            DestroyCollider(handle);

            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(parent.transform, false);
            blade.transform.localPosition = new Vector3(0f, -0.3f, 0.15f);
            blade.transform.localScale = new Vector3(0.22f, 0.2f, 0.04f);
            blade.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            SetColor(blade, Color.gray);
            DestroyCollider(blade);
        }

        private void SetColor(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Standard") ?? rend.material.shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                rend.material = mat;
            }
        }

        private void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        private void Update()
        {
            // Hiệu ứng rơi từ trên cao
            if (!hasLanded)
            {
                transform.position = Vector3.MoveTowards(transform.position, groundPosition, fallSpeed * Time.deltaTime);
                if (Vector3.Distance(transform.position, groundPosition) < 0.05f)
                {
                    transform.position = groundPosition;
                    hasLanded = true;
                }
            }
            else
            {
                // Bobbing & Rotating animation nhẹ khi đã chạm đất
                bobTimer += Time.deltaTime * 3f;
                float bobOffset = Mathf.Sin(bobTimer) * 0.05f;
                if (visualObject != null)
                {
                    visualObject.transform.localPosition = new Vector3(0f, bobOffset, 0f);
                    visualObject.transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
                }
            }

            // Billboard label hướng về Camera
            if (labelMesh != null && Camera.main != null)
            {
                labelMesh.transform.rotation = Quaternion.LookRotation(labelMesh.transform.position - Camera.main.transform.position);
            }

            // Kiểm tra người chơi Saboteur lại gần và nhấn [E] để nhặt
            CheckPickupInteraction();
        }

        private void CheckPickupInteraction()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null || !net.IsSaboteur) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist <= 3.2f)
            {
                OnItemNearby?.Invoke(this);

                if (Input.GetKeyDown(KeyCode.E))
                {
                    if (SaboteurInventoryHUD.Instance != null && !string.IsNullOrEmpty(SaboteurInventoryHUD.Instance.CurrentItem))
                    {
                        // Đang giữ item, không nhặt
                        return;
                    }
                    Pickup();
                }
            }
        }

        public void Pickup()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.PickupItem(ItemId);
            }
        }
    }
}
