using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Hiển thị và điều khiển 1 vật phẩm trong ShopView (Bộ xếp hình hoặc Không gian 3D).
    /// Hỗ trợ kiểm tra trạng thái đã sở hữu / chưa sở hữu, giá tiền xu, và gửi yêu cầu mua lên server.
    /// </summary>
    public class UIShopItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;

        [Header("Button Sprites")]
        [SerializeField] private Sprite activeGoldSprite;
        [SerializeField] private Sprite disabledDarkSprite;

        private string _itemId;
        private string _itemKind; // "puzzle" hoặc "theme"
        private int _price;
        private bool _isOwned;
        private Action<string, string> _onBuyCallback;

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

            if (priceText == null)
            {
                var t = transform.Find("Price") ?? transform.Find("Cost") ?? transform.Find("Detail");
                if (t != null) priceText = t.GetComponent<TextMeshProUGUI>();
            }

            if (actionButton == null)
            {
                var t = transform.Find("Buy") ?? transform.Find("Button") ?? transform.Find("ActionBtn");
                if (t != null) actionButton = t.GetComponent<Button>();
            }

            if (actionButton != null && actionButtonText == null)
            {
                actionButtonText = actionButton.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (iconImage == null)
            {
                var t = transform.Find("Icon") ?? transform.Find("Thumbnail");
                if (t != null) iconImage = t.GetComponent<Image>();
            }
        }

        public void Bind(string id, string kind, string displayName, int price, bool isOwned, Sprite icon = null, string extraDetail = null, Action<string, string> onBuy = null)
        {
            EnsureComponents();

            _itemId = id;
            _itemKind = kind;
            _price = price;
            _isOwned = isOwned;
            _onBuyCallback = onBuy;

            if (nameText != null)
            {
                nameText.text = !string.IsNullOrEmpty(displayName) ? displayName : id;
                nameText.color = Color.white;
            }

            if (priceText != null)
            {
                if (_isOwned)
                {
                    priceText.text = "<color=#6ee7b7>✓ Đã sở hữu</color>";
                }
                else if (price == 0)
                {
                    priceText.text = "<color=#fbbf24>★ Miễn phí</color>";
                }
                else
                {
                    string suffix = !string.IsNullOrEmpty(extraDetail) ? $" • {extraDetail}" : "";
                    priceText.text = $"<color=#fbbf24>💰 {price:N0} Xu</color>{suffix}";
                }
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

            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                var btnImg = actionButton.GetComponent<Image>();

                if (_isOwned)
                {
                    actionButton.interactable = false;
                    if (actionButtonText != null)
                    {
                        actionButtonText.text = "Đã có";
                        actionButtonText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    }
                    if (btnImg != null && disabledDarkSprite != null)
                        btnImg.sprite = disabledDarkSprite;
                }
                else
                {
                    actionButton.interactable = true;
                    if (actionButtonText != null)
                    {
                        actionButtonText.text = price == 0 ? "Nhận" : "Mua";
                        actionButtonText.color = Color.white;
                    }
                    if (btnImg != null && activeGoldSprite != null)
                        btnImg.sprite = activeGoldSprite;

                    actionButton.onClick.AddListener(OnBuyClicked);
                }
            }
        }

        private void OnBuyClicked()
        {
            if (_isOwned) return;

            if (_onBuyCallback != null)
            {
                _onBuyCallback.Invoke(_itemKind, _itemId);
            }
            else
            {
                PuzzleNetworkManager.Instance?.SendBuy(_itemKind, _itemId);
            }
        }
    }
}
