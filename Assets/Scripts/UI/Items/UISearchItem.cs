using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Hiển thị 1 mục chọn lựa trong ChoosePuzzleView hoặc ChooseEnvironmentView.
    /// Cho phép người dùng duyệt và chọn bộ tranh hoặc không gian 3D khi tạo phòng.
    /// </summary>
    public class UISearchItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI detailText;
        [SerializeField] private Button chooseButton;
        [SerializeField] private TextMeshProUGUI chooseButtonText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;

        private string _itemId;
        private string _displayName;
        private Action<string, string> _onSelectCallback;

        private void Awake()
        {
            EnsureComponents();
        }

        public void EnsureComponents()
        {
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();

            if (nameText == null)
            {
                var t = transform.Find("Name") ?? transform.Find("Title") ?? transform.Find("ItemName");
                if (t != null) nameText = t.GetComponent<TextMeshProUGUI>();
            }

            if (detailText == null)
            {
                var t = transform.Find("Price") ?? transform.Find("Detail") ?? transform.Find("Description");
                if (t != null) detailText = t.GetComponent<TextMeshProUGUI>();
            }

            if (chooseButton == null)
            {
                var t = transform.Find("Choose") ?? transform.Find("Buy") ?? transform.Find("Button");
                if (t != null) chooseButton = t.GetComponent<Button>();
            }

            if (chooseButton != null && chooseButtonText == null)
            {
                chooseButtonText = chooseButton.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (iconImage == null)
            {
                var t = transform.Find("Icon") ?? transform.Find("Thumbnail");
                if (t != null) iconImage = t.GetComponent<Image>();
            }
        }

        public void Bind(string id, string displayName, string detail, Sprite icon, Action<string, string> onSelect)
        {
            EnsureComponents();

            _itemId = id;
            _displayName = !string.IsNullOrEmpty(displayName) ? displayName : id;
            _onSelectCallback = onSelect;

            if (nameText != null)
            {
                nameText.text = _displayName;
                nameText.color = Color.white;
            }

            if (detailText != null)
            {
                detailText.text = !string.IsNullOrEmpty(detail) ? detail : "";
                detailText.color = new Color(0.85f, 0.85f, 0.9f, 0.85f);
            }

            if (iconImage != null)
            {
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            if (chooseButton != null)
            {
                chooseButton.onClick.RemoveAllListeners();
                if (chooseButtonText != null)
                {
                    chooseButtonText.text = "Chọn";
                    chooseButtonText.color = Color.white;
                }
                chooseButton.onClick.AddListener(OnChooseClicked);
            }
        }

        private void OnChooseClicked()
        {
            _onSelectCallback?.Invoke(_itemId, _displayName);
        }
    }
}
