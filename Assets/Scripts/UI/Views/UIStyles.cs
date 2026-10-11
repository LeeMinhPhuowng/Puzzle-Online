using System;
using UnityEngine;
using PuzzleOnline.Network;
using PuzzleOnline.Audio;
using PuzzleOnline.Core;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Bảng màu, GUIStyles và các hàm vẽ giao diện IMGUI dùng chung cho Puzzle Online.
    /// </summary>
    public class UIStyles
    {
        public const float VirtualWidth = 1920f;
        public const float VirtualHeight = 1080f;

        // Palette
        public readonly Color BgColor = new Color(0.08f, 0.09f, 0.12f, 1f);
        public readonly Color CardColor = new Color(0.13f, 0.15f, 0.20f, 0.95f);
        public readonly Color CardHoverColor = new Color(0.18f, 0.20f, 0.27f, 0.95f);
        public readonly Color AccentColor = new Color(0.22f, 0.62f, 0.95f, 1f);
        public readonly Color AccentHoverColor = new Color(0.35f, 0.72f, 1f, 1f);
        public readonly Color SuccessColor = new Color(0.24f, 0.75f, 0.45f, 1f);
        public readonly Color DangerColor = new Color(0.90f, 0.28f, 0.28f, 1f);
        public readonly Color GoldColor = new Color(1f, 0.82f, 0.22f, 1f);
        public readonly Color TextColor = new Color(0.94f, 0.95f, 0.97f, 1f);
        public readonly Color MutedColor = new Color(0.58f, 0.62f, 0.70f, 1f);

        private Texture2D _pixelTex;
        public GUIStyle TitleStyle { get; private set; }
        public GUIStyle HeaderStyle { get; private set; }
        public GUIStyle BodyStyle { get; private set; }
        public GUIStyle MutedStyle { get; private set; }
        public GUIStyle BoldStyle { get; private set; }
        public GUIStyle CardStyle { get; private set; }
        public GUIStyle InputStyle { get; private set; }
        public GUIStyle BtnStyle { get; private set; }
        public GUIStyle PrimaryBtnStyle { get; private set; }
        public GUIStyle GoldBtnStyle { get; private set; }
        public GUIStyle DangerBtnStyle { get; private set; }
        public GUIStyle BadgeStyle { get; private set; }
        private bool _stylesInitialized = false;

        public void EnsureResources()
        {
            if (_pixelTex == null)
            {
                _pixelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _pixelTex.hideFlags = HideFlags.DontSave;
                _pixelTex.SetPixel(0, 0, Color.white);
                _pixelTex.Apply();
                _stylesInitialized = false;
            }
            if (!_stylesInitialized)
            {
                InitStyles();
            }
        }

        private Texture2D MakeSolidTex(Color col)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            TitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TextColor }
            };

            HeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TextColor }
            };

            BodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TextColor }
            };

            BoldStyle = new GUIStyle(BodyStyle) { fontStyle = FontStyle.Bold };

            MutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = MutedColor }
            };

            BadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            CardStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeSolidTex(CardColor) }
            };

            InputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                normal = { background = MakeSolidTex(new Color(0.09f, 0.10f, 0.14f, 1f)), textColor = TextColor }
            };

            BtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = MakeSolidTex(new Color(0.18f, 0.22f, 0.30f, 1f)), textColor = TextColor },
                hover = { background = MakeSolidTex(CardHoverColor), textColor = Color.white }
            };

            PrimaryBtnStyle = new GUIStyle(BtnStyle)
            {
                normal = { background = MakeSolidTex(AccentColor), textColor = Color.white },
                hover = { background = MakeSolidTex(AccentHoverColor), textColor = Color.white }
            };

            GoldBtnStyle = new GUIStyle(BtnStyle)
            {
                normal = { background = MakeSolidTex(GoldColor), textColor = Color.black },
                hover = { background = MakeSolidTex(new Color(1f, 0.88f, 0.35f, 1f)), textColor = Color.black }
            };

            DangerBtnStyle = new GUIStyle(BtnStyle)
            {
                normal = { background = MakeSolidTex(DangerColor), textColor = Color.white },
                hover = { background = MakeSolidTex(new Color(1f, 0.35f, 0.35f, 1f)), textColor = Color.white }
            };

            _stylesInitialized = true;
        }

        public void DrawSolidRect(Rect rect, Color col)
        {
            EnsureResources();
            var oldCol = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(rect, _pixelTex);
            GUI.color = oldCol;
        }

        public void DrawBorder(Rect rect, float thickness, Color col)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), col);
            DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), col);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), col);
            DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), col);
        }

        public bool DrawButton(Rect rect, string text, bool isPrimary, bool isGold = false, bool isDanger = false, int fontSize = 16)
        {
            EnsureResources();
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color bgColor;
            Color textColor;

            if (isPrimary)
            {
                bgColor = hovered ? AccentHoverColor : AccentColor;
                textColor = Color.white;
            }
            else if (isGold)
            {
                bgColor = hovered ? new Color(1f, 0.88f, 0.35f, 1f) : GoldColor;
                textColor = Color.black;
            }
            else if (isDanger)
            {
                bgColor = hovered ? new Color(1f, 0.35f, 0.35f, 1f) : DangerColor;
                textColor = Color.white;
            }
            else
            {
                bgColor = hovered ? CardHoverColor : new Color(0.18f, 0.22f, 0.30f, 1f);
                textColor = TextColor;
            }

            DrawSolidRect(rect, bgColor);

            if (isPrimary)
            {
                DrawBorder(rect, 2f, new Color(0.65f, 0.88f, 1f, 1f));
            }
            else
            {
                DrawBorder(rect, 1f, new Color(0.28f, 0.33f, 0.44f, 0.7f));
            }

            var style = isPrimary ? BoldStyle : BodyStyle;
            int oldSize = style.fontSize;
            var oldAlign = style.alignment;
            var oldColor = style.normal.textColor;

            style.fontSize = fontSize;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = textColor;

            GUI.Label(rect, text, style);

            style.fontSize = oldSize;
            style.alignment = oldAlign;
            style.normal.textColor = oldColor;

            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (clicked)
            {
                ServiceLocator.Get<IAudioService>()?.PlayClick();
            }
            return clicked;
        }

        public float DrawSlider(Rect rect, string label, float value, string unit = "%")
        {
            EnsureResources();
            float labelW = 210f;
            float textW = 55f;
            float sliderW = rect.width - labelW - textW - 20f;

            Rect lblRect = new Rect(rect.x, rect.y, labelW, rect.height);
            Rect trackRect = new Rect(rect.x + labelW + 10f, rect.y + (rect.height - 10f) * 0.5f, sliderW, 10f);
            Rect valRect = new Rect(rect.xMax - textW, rect.y, textW, rect.height);

            GUI.Label(lblRect, label, BoldStyle);
            GUI.Label(valRect, $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}{unit}", MutedStyle);

            // Track background
            DrawSolidRect(trackRect, new Color(0.12f, 0.14f, 0.20f, 1f));

            // Track fill
            float fillW = Mathf.Clamp01(value) * sliderW;
            if (fillW > 0)
            {
                DrawSolidRect(new Rect(trackRect.x, trackRect.y, fillW, 10f), AccentColor);
            }

            // Knob handle
            float knobSize = 18f;
            Rect knobRect = new Rect(trackRect.x + fillW - knobSize * 0.5f, trackRect.y + 5f - knobSize * 0.5f, knobSize, knobSize);
            bool isHovered = trackRect.Contains(Event.current.mousePosition) || knobRect.Contains(Event.current.mousePosition);
            DrawSolidRect(knobRect, isHovered ? Color.white : new Color(0.85f, 0.88f, 0.95f, 1f));

            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && (trackRect.Contains(evt.mousePosition) || knobRect.Contains(evt.mousePosition)) && evt.button == 0)
            {
                GUIUtility.hotControl = controlID;
                value = Mathf.Clamp01((evt.mousePosition.x - trackRect.x) / sliderW);
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == controlID)
            {
                value = Mathf.Clamp01((evt.mousePosition.x - trackRect.x) / sliderW);
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && GUIUtility.hotControl == controlID)
            {
                GUIUtility.hotControl = 0;
                evt.Use();
            }

            return Mathf.Clamp01(value);
        }

        public void DrawTopBar(string title)
        {
            var net = ServiceLocator.Get<INetworkService>();
            Rect topRect = new Rect(80, 30, 1760, 65);
            DrawSolidRect(topRect, CardColor);

            GUI.Label(new Rect(110, 42, 600, 40), title, HeaderStyle);

            if (net != null)
            {
                GUI.Label(new Rect(1180, 45, 400, 35), $"Tài khoản: {net.Username}  |  {net.Coins} xu", BoldStyle);

                if (DrawButton(new Rect(1650, 40, 160, 45), "Đăng xuất", false, false, true))
                {
                    ServiceLocator.Get<IAudioService>()?.PlayClick();
                    net.Logout();
                }
            }
        }

        public static string GetPuzzleName(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId)) return "";
            var cat = ServiceLocator.Get<IPuzzleCatalog>();
            if (cat is ContentRegistry reg)
            {
                return reg.GetPuzzleDisplayName(puzzleId);
            }
            var p = cat?.GetPuzzleData(puzzleId);
            return p != null ? $"{p.displayName} ({p.rows}×{p.columns})" : ContentRegistry.Instance.GetPuzzleDisplayName(puzzleId);
        }

        public static string GetThemeName(string themeId)
        {
            if (string.IsNullOrEmpty(themeId)) return "";
            var themeCat = ServiceLocator.Get<IThemeCatalog>() ?? ContentRegistry.Instance;
            return themeCat.GetThemeDisplayName(themeId);
        }

        public static string FormatTimeLimit(int seconds)
        {
            if (seconds <= 0) return "Không giới hạn (Thư giãn)";
            int m = seconds / 60;
            return $"{m} phút ({seconds}s)";
        }
    }
}
