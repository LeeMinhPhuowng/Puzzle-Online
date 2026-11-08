using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Audio;
using PuzzleOnline.Network;
using PuzzleOnline.Utility;
using FirstPersonCamera;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Điều khiển bảng Cài đặt (SettingsView) trong SCN_Room và SCN_Lobby:
    /// - Thanh trượt âm lượng SFX, Nhạc nền (BGM), Voice Chat (Bạn bè), Khuếch đại Mic (Gain)
    /// - Độ nhạy chuột Mouse Sensitivity (Lưu vào PlayerPrefs)
    /// - Toàn màn hình (Fullscreen)
    /// - Nút Lưu & Đóng dùng Sprite 9-slices Button_Royal_Gold
    /// - Thiết kế chuẩn Đen/Vàng (Black & Gold) sang trọng, không dùng emoji tránh lỗi font glyph.
    /// </summary>
    public class LobbySettingsModalUI : MonoBehaviour
    {
        public static LobbySettingsModalUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.gameObject != null && Instance.gameObject.activeSelf;

        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.22f, 1f);
        private static readonly Color CardBgColor = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        private static readonly Color BoxBgColor = new Color(0.12f, 0.14f, 0.19f, 0.95f);
        private static readonly Color DarkTrackColor = new Color(0.18f, 0.21f, 0.28f, 1f);
        private static readonly Color TextWhiteColor = new Color(0.95f, 0.96f, 0.98f, 1f);

        [SerializeField] private Sprite royalGoldSprite;

        private GameObject _contentRoot;
        private bool _isBuilt = false;

        // Sliders & Value texts
        private Slider _sfxSlider;
        private TMP_Text _sfxValText;

        private Slider _voiceSlider;
        private TMP_Text _voiceValText;

        private Slider _musicSlider;
        private TMP_Text _musicValText;

        private Slider _gainSlider;
        private TMP_Text _gainValText;

        private Slider _sensSlider;
        private TMP_Text _sensValText;

        // Display
        private Button _fullscreenBtn;
        private TMP_Text _fullscreenBtnText;

        private void Awake()
        {
            Instance = this;
            LoadRoyalGoldSprite();
            EnsureCardStyle();
            BuildSettingsUI();
        }

        private void OnEnable()
        {
            Instance = this;
            LoadRoyalGoldSprite();
            EnsureCardStyle();
            if (!_isBuilt) BuildSettingsUI();
            RefreshSettingsValues();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseModal();
            }
        }

        public void CloseModal()
        {
            gameObject.SetActive(false);
            string curScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool isGameplay = curScene != "SCN_Menu" && curScene != "SCN_MainMenu" && curScene != "SCN_Room" && curScene != "SCN_Lobby";
            if (isGameplay)
            {
                CursorManager.Lock();
            }
            else
            {
                CursorManager.Unlock();
            }
        }

        public static LobbySettingsModalUI ShowModal(Transform parentCanvas = null)
        {
            if (Instance != null && Instance.gameObject != null)
            {
                Instance.gameObject.transform.SetAsLastSibling();
                Instance.gameObject.SetActive(true);
                Instance.RefreshSettingsValues();
                CursorManager.Unlock();
                return Instance;
            }

            var existing = FindFirstObjectByType<LobbySettingsModalUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                existing.gameObject.transform.SetAsLastSibling();
                existing.gameObject.SetActive(true);
                existing.RefreshSettingsValues();
                CursorManager.Unlock();
                return existing;
            }

            Canvas targetCanvas = null;
            if (parentCanvas != null) targetCanvas = parentCanvas.GetComponent<Canvas>() ?? parentCanvas.GetComponentInParent<Canvas>();
            if (targetCanvas == null && GameplayHUD.Instance != null) targetCanvas = GameplayHUD.Instance.GetComponent<Canvas>() ?? GameplayHUD.Instance.GetComponentInParent<Canvas>();
            if (targetCanvas == null) targetCanvas = FindFirstObjectByType<Canvas>();

            if (targetCanvas != null)
            {
                var viewGo = new GameObject("SettingsView", typeof(RectTransform), typeof(Image), typeof(LobbySettingsModalUI));
                viewGo.transform.SetParent(targetCanvas.transform, false);
                var rt = viewGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(10f, 10f);
                rt.offsetMax = new Vector2(-10f, -10f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                var comp = viewGo.GetComponent<LobbySettingsModalUI>();
                Instance = comp;
                viewGo.transform.SetAsLastSibling();
                viewGo.SetActive(true);
                CursorManager.Unlock();
                return comp;
            }

            return null;
        }

        private void LoadRoyalGoldSprite()
        {
            if (royalGoldSprite == null)
            {
                royalGoldSprite = Resources.Load<Sprite>("GeneratedImgs/Button_Royal_Gold")
                               ?? Resources.Load<Sprite>("Button_Royal_Gold");
            }
#if UNITY_EDITOR
            if (royalGoldSprite == null)
            {
                royalGoldSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GeneratedImgs/Button_Royal_Gold.png")
                               ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/GeneratedImgs/Button_Royal_Gold.png");
            }
#endif
        }

        private void EnsureCardStyle()
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(10f, 10f);
                rt.offsetMax = new Vector2(-10f, -10f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            var img = GetComponent<Image>();
            if (img != null) img.color = CardBgColor;

            var outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.effectColor = GoldColor;
            outline.effectDistance = new Vector2(2f, -2f);

            var shadow = GetComponent<Shadow>() ?? gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(4f, -4f);

            // Title
            var titleComp = GetComponentInChildren<TMP_Text>(true);
            if (titleComp != null && (titleComp.name.Contains("Title") || titleComp.text.Contains("Cài đặt") || titleComp.text.Contains("Setting")))
            {
                titleComp.color = GoldColor;
                titleComp.fontStyle = FontStyles.Bold;
                titleComp.text = "CÀI ĐẶT";
            }
            else if (transform.Find("Title") == null)
            {
                var titleTxt = CreateText(transform, "Title", "CÀI ĐẶT", 24, GoldColor, TextAlignmentOptions.TopLeft, true);
                var trt = titleTxt.rectTransform;
                trt.anchorMin = new Vector2(0f, 1f);
                trt.anchorMax = new Vector2(0f, 1f);
                trt.pivot = new Vector2(0f, 1f);
                trt.anchoredPosition = new Vector2(40f, -20f);
                trt.sizeDelta = new Vector2(300f, 40f);
            }

            // Close button (X) top-right
            var closeBtn = transform.Find("CloseBtn") ?? transform.Find("Back");
            if (closeBtn == null)
            {
                var closeObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
                closeObj.transform.SetParent(transform, false);
                var crt = closeObj.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(1f, 1f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot = new Vector2(1f, 1f);
                crt.anchoredPosition = new Vector2(-30f, -20f);
                crt.sizeDelta = new Vector2(40f, 40f);
                var cimg = closeObj.GetComponent<Image>();
                cimg.color = new Color(0.18f, 0.22f, 0.30f, 0.9f);
                var cbtn = closeObj.GetComponent<Button>();
                cbtn.onClick.AddListener(CloseModal);
                var ctxt = CreateText(closeObj.transform, "X", "X", 18, TextWhiteColor, TextAlignmentOptions.Center, true);
                ctxt.rectTransform.anchorMin = Vector2.zero;
                ctxt.rectTransform.anchorMax = Vector2.one;
                ctxt.rectTransform.offsetMin = Vector2.zero;
                ctxt.rectTransform.offsetMax = Vector2.zero;
            }
            else
            {
                var btn = closeBtn.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveListener(CloseModal);
                    btn.onClick.AddListener(CloseModal);
                }
            }
        }

        public void BuildSettingsUI()
        {
            // Xóa content cũ nếu đã tồn tại để cập nhật cấu trúc mới
            var existingContent = transform.Find("SettingsDynamicContent");
            if (existingContent != null)
            {
                DestroyImmediate(existingContent.gameObject);
                _isBuilt = false;
            }

            _contentRoot = new GameObject("SettingsDynamicContent", typeof(RectTransform));
            _contentRoot.transform.SetParent(transform, false);

            var rt = _contentRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.04f, 0.05f);
            rt.anchorMax = new Vector2(0.96f, 0.89f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var vlg = _contentRoot.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Section 1: ÂM THANH & MICROPHONE
            CreateSectionHeader(_contentRoot.transform, "CÀI ĐẶT ÂM THANH & MICROPHONE");

            // Slider SFX
            _sfxSlider = CreateSettingSlider(_contentRoot.transform, "Hiệu ứng âm thanh (SFX)", out _sfxValText, (val) =>
            {
                AudioManager.Instance?.SetSFXVolume(val);
                if (_sfxValText != null) _sfxValText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            });

            // Slider Voice Chat
            _voiceSlider = CreateSettingSlider(_contentRoot.transform, "Âm lượng Voice Chat (Bạn bè)", out _voiceValText, (val) =>
            {
                AudioManager.Instance?.SetVoiceVolume(val);
                if (_voiceValText != null) _voiceValText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            });

            // Slider Nhạc nền (BGM)
            _musicSlider = CreateSettingSlider(_contentRoot.transform, "Nhạc nền (BGM)", out _musicValText, (val) =>
            {
                AudioManager.Instance?.SetMusicVolume(val);
                if (_musicValText != null) _musicValText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            });

            // Slider Mic Gain
            _gainSlider = CreateSettingSlider(_contentRoot.transform, "Khuếch đại Mic (Gain)", out _gainValText, (val) =>
            {
                var net = PuzzleNetworkManager.Instance;
                if (net != null && net.Voice != null)
                {
                    net.Voice.MicGain = 0.5f + val * 3.5f;
                }
                if (_gainValText != null) _gainValText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            });

            // Section 2: ĐIỀU KHIỂN & HIỂN THỊ
            CreateSectionHeader(_contentRoot.transform, "ĐIỀU KHIỂN & ĐỒ HỌA");

            // Slider Mouse Sensitivity
            _sensSlider = CreateSettingSlider(_contentRoot.transform, "Độ nhạy chuột (Mouse)", out _sensValText, (val) =>
            {
                float sens = 0.5f + val * 3.5f;
                PlayerPrefs.SetFloat("MouseSensitivity", sens);
                PlayerPrefs.Save();
                if (FirstPersonCameraScript.Instance != null)
                {
                    FirstPersonCameraScript.Instance.SetMouseSensitivity(sens, sens);
                }
                if (_sensValText != null) _sensValText.text = $"{sens:F1}x";
            });

            // Row Toàn màn hình (đã bỏ nút Đồ họa: PC theo yêu cầu)
            var displayRow = CreateBox(_contentRoot.transform, "DisplayRow", 38f, BoxBgColor);
            var dispHlg = displayRow.AddComponent<HorizontalLayoutGroup>();
            dispHlg.padding = new RectOffset(14, 14, 3, 3);
            dispHlg.spacing = 12f;
            dispHlg.childControlWidth = false;
            dispHlg.childControlHeight = true;
            dispHlg.childForceExpandWidth = false;
            dispHlg.childForceExpandHeight = true;

            var fullBtnGo = CreateBox(displayRow.transform, "FullscreenBtn", 32f, new Color(0.16f, 0.20f, 0.27f, 1f));
            var fullBtnRt = fullBtnGo.GetComponent<RectTransform>();
            fullBtnRt.sizeDelta = new Vector2(220f, 32f);
            var fullOutline = fullBtnGo.AddComponent<Outline>();
            fullOutline.effectColor = new Color(0.35f, 0.40f, 0.52f, 0.8f);
            fullOutline.effectDistance = new Vector2(1f, -1f);
            _fullscreenBtn = fullBtnGo.AddComponent<Button>();
            _fullscreenBtnText = CreateText(fullBtnGo.transform, "FullText", "Toàn màn hình: BẬT", 13, TextWhiteColor, TextAlignmentOptions.Center, true);
            _fullscreenBtnText.rectTransform.anchorMin = Vector2.zero;
            _fullscreenBtnText.rectTransform.anchorMax = Vector2.one;
            _fullscreenBtnText.rectTransform.offsetMin = Vector2.zero;
            _fullscreenBtnText.rectTransform.offsetMax = Vector2.zero;
            _fullscreenBtn.onClick.AddListener(ToggleFullscreen);

            // Nút điều hướng phía dưới - Tự động đồng bộ theo Scene
            var bottomRow = new GameObject("BottomRow", typeof(RectTransform));
            bottomRow.transform.SetParent(_contentRoot.transform, false);
            var bottomRt = bottomRow.GetComponent<RectTransform>();
            bottomRt.sizeDelta = new Vector2(0f, 48f);
            var bottomHlg = bottomRow.AddComponent<HorizontalLayoutGroup>();
            bottomHlg.spacing = 16f;
            bottomHlg.childControlWidth = true;
            bottomHlg.childControlHeight = true;
            bottomHlg.childForceExpandWidth = true;
            bottomHlg.childForceExpandHeight = true;

            string curScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool isMenu = curScene == "SCN_Menu" || curScene == "SCN_MainMenu";
            bool isRoom = curScene == "SCN_Room" || curScene == "SCN_Lobby";
            bool isGameplay = !isMenu && !isRoom;

            if (isRoom)
            {
                // SCN_Room / SCN_Lobby: 1. Nút LƯU & ĐÓNG | 2. Nút ĐĂNG XUẤT
                CreateActionButton(bottomRow.transform, "SaveCloseBtn", "LƯU & ĐÓNG", true, () =>
                {
                    CloseModal();
                });

                CreateActionButton(bottomRow.transform, "LogoutBtn", "ĐĂNG XUẤT", false, () =>
                {
                    CloseModal();
                    var net = PuzzleNetworkManager.Instance;
                    if (net != null)
                    {
                        net.LeaveRoom();
                        net.Logout();
                    }
                    AudioManager.Instance?.StopAmbience();
                    UnityEngine.SceneManagement.SceneManager.LoadScene("SCN_Menu");
                    CursorManager.Unlock();
                });
            }
            else if (isGameplay)
            {
                // Gameplay: 1. Nút TIẾP TỤC | 2. Nút RỜI PHÒNG
                CreateActionButton(bottomRow.transform, "ResumeBtn", "TIẾP TỤC", true, () =>
                {
                    CloseModal();
                });

                CreateActionButton(bottomRow.transform, "LeaveRoomBtn", "RỜI PHÒNG", false, () =>
                {
                    CloseModal();
                    AudioManager.Instance?.StopAmbience();
                    PuzzleNetworkManager.Instance?.LeaveRoom();
                });
            }
            else
            {
                // SCN_Menu: Nút LƯU & ĐÓNG
                CreateActionButton(bottomRow.transform, "SaveCloseBtn", "LƯU & ĐÓNG", true, () =>
                {
                    CloseModal();
                });
            }

            _isBuilt = true;
        }

        private GameObject CreateActionButton(Transform parent, string name, string text, bool isGold, Action onClick)
        {
            var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(0f, 48f);

            var img = btnObj.GetComponent<Image>();
            img.sprite = null;
            img.type = Image.Type.Simple;

            var outline = btnObj.AddComponent<Outline>();
            if (isGold)
            {
                // Nút Vàng Flat đồng bộ hoàn toàn kiểu dáng với nút Đỏ
                img.color = GoldColor;
                outline.effectColor = new Color(0.85f, 0.68f, 0.15f, 1f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            else
            {
                // Nút Đỏ Danger (Đăng xuất / Rời phòng)
                img.color = new Color(0.85f, 0.22f, 0.22f, 1f);
                outline.effectColor = new Color(1f, 0.45f, 0.45f, 0.9f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            var btn = btnObj.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                onClick?.Invoke();
            });

            Color txtColor = isGold ? new Color(0.12f, 0.08f, 0.02f, 1f) : TextWhiteColor;
            var btnTxt = CreateText(btnObj.transform, "Text", text, 16, txtColor, TextAlignmentOptions.Center, true);
            btnTxt.rectTransform.anchorMin = Vector2.zero;
            btnTxt.rectTransform.anchorMax = Vector2.one;
            btnTxt.rectTransform.offsetMin = Vector2.zero;
            btnTxt.rectTransform.offsetMax = Vector2.zero;

            return btnObj;
        }

        private Slider CreateSettingSlider(Transform parent, string title, out TMP_Text valueText, Action<float> onValueChanged)
        {
            var row = CreateBox(parent, "Row_" + title, 36f, BoxBgColor);
            var rowHlg = row.AddComponent<HorizontalLayoutGroup>();
            rowHlg.padding = new RectOffset(14, 14, 4, 4);
            rowHlg.spacing = 10f;
            rowHlg.childControlWidth = false;
            rowHlg.childControlHeight = true;
            rowHlg.childForceExpandWidth = false;
            rowHlg.childForceExpandHeight = true;

            var lbl = CreateText(row.transform, "Title", title, 13, TextWhiteColor, TextAlignmentOptions.Left, true);
            lbl.rectTransform.sizeDelta = new Vector2(240f, 28f);

            // Slider Container
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row.transform, false);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.sizeDelta = new Vector2(250f, 28f);
            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            // Background Track
            var bgGo = CreateBox(sliderGo.transform, "Background", 6f, DarkTrackColor);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.44f);
            bgRt.anchorMax = new Vector2(1f, 0.56f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill Area
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.44f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.56f);
            fillAreaRt.offsetMin = new Vector2(4f, 0f);
            fillAreaRt.offsetMax = new Vector2(-4f, 0f);

            var fill = CreateBox(fillArea.transform, "Fill", 6f, GoldColor);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            slider.fillRect = fillRt;

            // Handle Slide Area
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var handleAreaRt = handleArea.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(6f, 0f);
            handleAreaRt.offsetMax = new Vector2(-6f, 0f);

            // Handle nhỏ gọn (10px x 16px), bo viền vàng, không bị kéo dãn dọc
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0.5f, 0.5f);
            handleRt.anchorMax = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(10f, 16f);

            var handleImg = handle.GetComponent<Image>();
            handleImg.color = new Color(1f, 0.96f, 0.88f, 1f);

            var handleOutline = handle.AddComponent<Outline>();
            handleOutline.effectColor = GoldColor;
            handleOutline.effectDistance = new Vector2(1f, -1f);

            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;

            slider.onValueChanged.AddListener((v) => onValueChanged?.Invoke(v));

            // Value text
            valueText = CreateText(row.transform, "Val", "100%", 13, GoldColor, TextAlignmentOptions.Right, true);
            valueText.rectTransform.sizeDelta = new Vector2(55f, 28f);

            return slider;
        }

        private void CreateSectionHeader(Transform parent, string title)
        {
            var headerGo = new GameObject("Section_" + title, typeof(RectTransform));
            headerGo.transform.SetParent(parent, false);
            var rt = headerGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 24f);

            var txt = CreateText(headerGo.transform, "Title", title, 13, GoldColor, TextAlignmentOptions.Left, true);
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = new Vector2(4f, 0f);
            txt.rectTransform.offsetMax = Vector2.zero;
        }

        private GameObject CreateBox(Transform parent, string name, float height, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, height);
            var img = go.GetComponent<Image>();
            img.color = bgColor;
            return go;
        }

        private TMP_Text CreateText(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        public void RefreshSettingsValues()
        {
            var audio = AudioManager.Instance;
            var net = PuzzleNetworkManager.Instance;

            if (_sfxSlider != null && audio != null)
            {
                _sfxSlider.SetValueWithoutNotify(audio.SFXVolume);
                if (_sfxValText != null) _sfxValText.text = $"{Mathf.RoundToInt(audio.SFXVolume * 100f)}%";
            }

            if (_voiceSlider != null && audio != null)
            {
                _voiceSlider.SetValueWithoutNotify(audio.VoiceVolume);
                if (_voiceValText != null) _voiceValText.text = $"{Mathf.RoundToInt(audio.VoiceVolume * 100f)}%";
            }

            if (_musicSlider != null && audio != null)
            {
                _musicSlider.SetValueWithoutNotify(audio.MusicVolume);
                if (_musicValText != null) _musicValText.text = $"{Mathf.RoundToInt(audio.MusicVolume * 100f)}%";
            }

            if (_gainSlider != null && net != null && net.Voice != null)
            {
                float gainNorm = Mathf.Clamp01((net.Voice.MicGain - 0.5f) / 3.5f);
                _gainSlider.SetValueWithoutNotify(gainNorm);
                if (_gainValText != null) _gainValText.text = $"{Mathf.RoundToInt(gainNorm * 100f)}%";
            }

            if (_sensSlider != null)
            {
                float curSens = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
                float curSensNorm = Mathf.Clamp01((curSens - 0.5f) / 3.5f);
                _sensSlider.SetValueWithoutNotify(curSensNorm);
                if (_sensValText != null) _sensValText.text = $"{curSens:F1}x";
            }

            UpdateDisplayStatus();
        }

        private void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            UpdateDisplayStatus();
        }

        private void UpdateDisplayStatus()
        {
            if (_fullscreenBtnText != null)
            {
                _fullscreenBtnText.text = Screen.fullScreen ? "Toàn màn hình: BẬT" : "Cửa sổ: BẬT";
            }
        }
    }
}
