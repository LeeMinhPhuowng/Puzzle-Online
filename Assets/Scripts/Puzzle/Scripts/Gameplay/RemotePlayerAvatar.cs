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

            CreateNameplate();
        }

        public void SetTargetLook(float yaw, float pitch)
        {
            targetYaw = yaw;
            targetPitch = pitch;
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
        }

        private void Update()
        {
            // Smoothly interpolate Yaw and Pitch
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * 15f);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 15f);

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
