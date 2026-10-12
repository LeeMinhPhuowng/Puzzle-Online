using UnityEngine;

namespace PuzzleSystem.Gameplay
{
    public class RemotePlayerAvatar : MonoBehaviour
    {
        public string username;
        public Transform headTransform;

        private float targetYaw;
        private float targetPitch;
        private float currentYaw;
        private float currentPitch;

        private Vector3 targetPosition;
        private bool hasTargetPosition = false;

        private GameObject nameplateObj;
        private TextMesh nameplateText;

        public void Setup(string user, Transform head)
        {
            this.username = user;
            this.headTransform = head;
            this.targetYaw = transform.eulerAngles.y;
            this.currentYaw = this.targetYaw;
            this.targetPitch = headTransform != null ? headTransform.localEulerAngles.x : 0f;
            if (this.targetPitch > 180f) this.targetPitch -= 360f;
            this.currentPitch = this.targetPitch;
            this.targetPosition = transform.position;
            this.hasTargetPosition = true;

            CreateNameplate();
        }

        public void SetAvatarColor(Color color)
        {
            var items = GetComponent<PuzzleOnline.Gameplay.Competitive.PlayerHeldItemVisuals>();
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.name == "TargetOutline" || r.GetComponent<TextMesh>() != null ||
                    (items != null && items.Contains(r.transform))) continue;
                if (r.name.StartsWith("Eye", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (r is MeshRenderer mr)
                {
                    mr.material.color = color;
                }
            }
        }

        public void SetTargetLook(float yaw, float pitch)
        {
            targetYaw = yaw;
            targetPitch = pitch;
        }

        public void SetTargetPosition(Vector3 pos)
        {
            targetPosition = pos;
            hasTargetPosition = true;
        }

        public void SetTargetPose(Vector3 pos, float yaw, float pitch)
        {
            SetTargetPosition(pos);
            SetTargetLook(yaw, pitch);
        }

        private void CreateNameplate()
        {
            if (nameplateObj != null) return;

            nameplateObj = new GameObject("Nameplate");
            nameplateObj.transform.SetParent(transform, false);
            nameplateObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            nameplateText = nameplateObj.AddComponent<TextMesh>();
            nameplateText.text = username;
            nameplateText.fontSize = 32;
            nameplateText.characterSize = 0.04f;
            nameplateText.alignment = TextAlignment.Center;
            nameplateText.anchor = TextAnchor.MiddleCenter;
            nameplateText.color = new Color(0.3f, 0.85f, 1f, 1f); // Cyan nameplate

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                nameplateText.font = font;
                var mr = nameplateObj.GetComponent<MeshRenderer>();
                if (mr != null) mr.material = font.material;
            }
        }

        private void Update()
        {
            // Smoothly interpolate Position with distance thresholding
            if (hasTargetPosition)
            {
                float dist = Vector3.Distance(transform.position, targetPosition);
                if (dist > 5f)
                {
                    transform.position = targetPosition; // Snap if too far or teleport
                }
                else if (dist > 0.003f)
                {
                    transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 12f);
                }
            }

            // Smoothly interpolate Yaw and Pitch
            if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, targetYaw)) > 0.1f)
            {
                currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * 15f);
            }
            else
            {
                currentYaw = targetYaw;
            }

            if (Mathf.Abs(currentPitch - targetPitch) > 0.1f)
            {
                currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 15f);
            }
            else
            {
                currentPitch = targetPitch;
            }

            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

            if (headTransform != null)
            {
                headTransform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            }

            // Nameplate always billboarding to face the active main camera
            if (nameplateObj != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 lookDir = nameplateObj.transform.position - cam.transform.position;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        nameplateObj.transform.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
            }
        }
    }
}
