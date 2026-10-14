using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using PuzzleOnline.UI;

// Asset authoring only: the game loads the saved prefab and never constructs its layout in code.
public static class GameplayUISetup
{
    private const string HudPath = "Assets/Resources/UI/GameplayHUD.prefab";
    private static readonly Color Card = new Color(.13f, .15f, .20f, .95f);
    private static readonly Color Ink = new Color(.94f, .95f, .97f);
    private static readonly Color Blue = new Color(.22f, .62f, .95f);
    private static TMP_FontAsset font;
    private static TMP_SpriteAsset sprites;

    [MenuItem("Tools/Puzzle Online/Apply Canvas Gameplay UI")]
    public static void Apply()
    {
        Directory.CreateDirectory("Assets/Resources/UI");
        AssetDatabase.Refresh();
        PrepareFonts();
        BuildHUD();
        ConfigureRoomItem();
        foreach (string name in new[] { "SCN_Room", "SCN_Lobby" }) ConfigureLobby(name);
        foreach (string name in new[] { "SCN_Nature", "SCN_Classroom", "SCN_Sakura", "Test" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var previous in UnityEngine.Object.FindObjectsByType<GameplayHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("PASS: Canvas HUD, room cards, quick join, invite and emoji references saved.");
    }

    private static void PrepareFonts()
    {
        const string path = "Assets/Fonts/Emoji/NotoEmoji SDF.asset";
        var emoji = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (emoji == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Emoji/NotoEmoji.ttf");
            if (source == null) throw new InvalidOperationException("Noto Emoji font was not imported.");
            emoji = TMP_FontAsset.CreateFontAsset(source, 48, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            emoji.name = "NotoEmoji SDF";
            emoji.TryAddCharacters("😊👍🎉🔥❤👏😀😂😍😎🤣🎁🔦🧪🧤🧲⛏⚔⚡🎯★∞", out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("Optional emoji glyphs missing: " + missing);
            // Variation selectors change emoji presentation, but must not become visible missing glyphs.
            var invisible = new Glyph(0xFFFFFF, new GlyphMetrics(0, 0, 0, 0, 0), new GlyphRect(0, 0, 0, 0), 1, 0);
            emoji.glyphTable.Add(invisible);
            emoji.characterTable.Add(new TMP_Character(0xFE0F, emoji, invisible));
            emoji.characterTable.Add(new TMP_Character(0xFE0E, emoji, invisible));
            emoji.ReadFontAssetDefinition();
            AssetDatabase.CreateAsset(emoji, path);
            emoji.material.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(emoji.material, emoji);
            foreach (var texture in emoji.atlasTextures)
            {
                texture.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(texture, emoji);
            }
        }
        var emojiSettings = new SerializedObject(emoji);
        emojiSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        emojiSettings.ApplyModifiedPropertiesWithoutUndo();
        emoji.TryAddCharacters("😊👍🎉🔥❤👏😀😂😍😎🤣🎁🔦🧪🧤🧲⛏⚔⚡🎯★∞");
        if (!emoji.characterTable.Any(c => c.unicode == 0xFE0F))
        {
            var invisible = new Glyph(0xFFFFFF, new GlyphMetrics(0, 0, 0, 0, 0), new GlyphRect(0, 0, 0, 0), 1, 0);
            emoji.glyphTable.Add(invisible);
            emoji.characterTable.Add(new TMP_Character(0xFE0F, emoji, invisible));
            emoji.characterTable.Add(new TMP_Character(0xFE0E, emoji, invisible));
            emoji.ReadFontAssetDefinition();
        }
        EditorUtility.SetDirty(emoji);
        const string body = "Assets/Resources/UI/HUDText.asset";
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(body);
        if (font == null)
        {
            font = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"));
            font.name = "HUDText";
            font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { emoji };
            AssetDatabase.CreateAsset(font, body);
        }
        sprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>("Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset");
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        rect.gameObject.layer = 5;
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var rect = Rect(name, parent, anchor, anchor, pos, size, pivot);
        var image = rect.gameObject.AddComponent<Image>(); image.color = Card; image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text Text(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, string value, float point = 23)
    {
        var rect = Rect(name, parent, min, max, pos, size, new Vector2(.5f, .5f));
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.spriteAsset = sprites; label.text = value; label.fontSize = point;
        label.color = Ink; label.raycastTarget = false; label.richText = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private static TMP_Text Label(string name, Transform parent, float x, float y, float w, float h, string value, float point = 23)
    {
        var label = Text(name, parent, Vector2.up, Vector2.up, new Vector2(x + w / 2, -y - h / 2), new Vector2(w, h), value, point);
        return label;
    }

    private static Button Button(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string value)
    {
        var rect = Rect(name, parent, anchor, anchor, pos, size, new Vector2(.5f, .5f));
        var image = rect.gameObject.AddComponent<Image>(); image.color = Blue;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Text("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, -4), value, 21);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static void Ref(SerializedObject target, string name, UnityEngine.Object value) => target.FindProperty(name).objectReferenceValue = value;
    private static void Refs(SerializedObject target, string name, UnityEngine.Object[] values)
    {
        var property = target.FindProperty(name); property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void BuildHUD()
    {
        var root = new GameObject("GameplayHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.layer = 5;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
        var scale = root.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1920, 1080); scale.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var hud = root.AddComponent<GameplayHUD>(); var data = new SerializedObject(hud);
        var area = Rect("HUD", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.one * .5f);
        Ref(data, "inputGroup", area.gameObject.AddComponent<CanvasGroup>());
        Ref(data, "hudRoot", area.gameObject);
        var top = Box("TopBar", area, Vector2.up, Vector2.zero, Vector2.zero, Vector2.up);
        top.anchorMax = Vector2.one; top.anchoredPosition = new Vector2(0, -24); top.sizeDelta = new Vector2(-48, 108); top.pivot = new Vector2(.5f, 1);
        Ref(data, "roomName", Text("RoomName", top, new Vector2(0, .52f), new Vector2(.28f, 1), Vector2.zero, new Vector2(-30, -6), "Phòng: Puzzle Online"));
        Ref(data, "puzzleName", Text("PuzzleName", top, Vector2.zero, new Vector2(.28f, .52f), Vector2.zero, new Vector2(-30, -6), "Bộ tranh: Hoàng Hôn"));
        Ref(data, "themeName", Text("ThemeName", top, new Vector2(.29f, .52f), new Vector2(.69f, 1), Vector2.zero, new Vector2(-20, -6), "Không gian: Thiên nhiên xanh mát"));
        var progress = Rect("ProgressBar", top, new Vector2(.29f, .1f), new Vector2(.69f, .43f), Vector2.zero, Vector2.zero, Vector2.one * .5f);
        progress.gameObject.AddComponent<Image>().color = new Color(.08f, .09f, .12f);
        progress.GetComponent<Image>().raycastTarget = false;
        var fillRect = Rect("Fill", progress, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.one * .5f);
        var fill = fillRect.gameObject.AddComponent<Image>(); fill.color = Blue; fill.raycastTarget = false;
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = .45f;
        Ref(data, "progressFill", fill);
        var progressText = Text("Progress", progress, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "4/9 mảnh (44%)", 21);
        progressText.alignment = TextAlignmentOptions.Center; Ref(data, "progress", progressText);
        var timer = Text("Timer", top, new Vector2(.72f, .1f), new Vector2(.85f, .9f), Vector2.zero, Vector2.zero, "04:30", 30);
        timer.alignment = TextAlignmentOptions.Center; Ref(data, "timer", timer);
        Ref(data, "menuButton", Button("MenuButton", top, new Vector2(.93f, .5f), Vector2.zero, new Vector2(170, 48), "Menu [Esc]"));
        var board = Box("RankingPanel", area, Vector2.up, new Vector2(24, -152), new Vector2(330, 210), Vector2.up);
        Label("Title", board, 16, 12, 298, 30, "BẢNG XẾP HẠNG", 24);
        Ref(data, "ranking", Label("Players", board, 16, 52, 298, 146, "1. Player 1    40 đ\n2. Player 2    20 đ", 22));
        var help = Box("TutorialPanel", area, Vector2.up, new Vector2(24, -378), new Vector2(330, 230), Vector2.up);
        Label("Title", help, 16, 12, 298, 30, "HƯỚNG DẪN", 24);
        Ref(data, "tutorial", Label("Controls", help, 16, 50, 298, 170, "[WASD] Di chuyển    [Space] Nhảy\n[Z] Góc nhìn trên cao\n[Chuột trái] Cầm / Thả\n[R / Cuộn chuột] Xoay 90°\n[T] Chat    [V] Micro    [Esc] Menu", 20));
        var mic = Box("MicrophonePanel", area, Vector2.up, new Vector2(24, -624), new Vector2(330, 100), Vector2.up);
        Ref(data, "microphone", Label("Status", mic, 12, 8, 306, 36, "Micro: Sẵn sàng [V]", 21));
        Ref(data, "microphoneButton", Button("MuteButton", mic, new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(286, 36), "Bật / Tắt tiếng"));
        var role = Box("RoleAnnouncement", area, new Vector2(.5f, 1), new Vector2(0, -156), new Vector2(650, 110), new Vector2(.5f, 1));
        var roleText = Label("Role", role, 16, 10, 618, 90, "ĐỘI XÂY DỰNG\nHợp tác ghép tranh trước khi hết giờ!", 25);
        roleText.alignment = TextAlignmentOptions.Center; Ref(data, "rolePanel", role.gameObject); Ref(data, "roleText", roleText);
        var item = Box("ItemGuide", area, new Vector2(.5f, 0), new Vector2(0, 150), new Vector2(630, 190), new Vector2(.5f, 0));
        Ref(data, "itemPanel", item.gameObject);
        Ref(data, "itemText", Label("Instruction", item, 14, 10, 602, 66, "ĐÈN PIN: Nhấp chuột trái vào người chơi để rọi mù màn hình.", 22));
        Ref(data, "targetText", Label("Target", item, 14, 78, 602, 30, "Nhắm vào mục tiêu hợp lệ", 21));
        Ref(data, "pickupText", Label("Pickup", item, 14, 112, 602, 30, "Nhấn [E] để mở Hộp Quà", 21));
        var targets = new Button[3]; var names = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            targets[i] = Button("Target" + i, item, new Vector2(0, 0), new Vector2(109 + i * 206, 24), new Vector2(194, 36), "Player " + (i + 1));
            names[i] = targets[i].GetComponentInChildren<TMP_Text>();
        }
        Refs(data, "targetButtons", targets); Refs(data, "targetNames", names);
        var dot = Box("Reticle", area, Vector2.one * .5f, Vector2.zero, new Vector2(6, 6), Vector2.one * .5f);
        dot.GetComponent<Image>().color = Color.white; Ref(data, "reticle", dot.gameObject);
        BuildChat(area, data);
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void BuildChat(Transform parent, SerializedObject data)
    {
        var chat = Box("ChatPanel", parent, Vector2.right, new Vector2(-24, 24), new Vector2(520, 360), Vector2.right);
        Ref(data, "chatPanel", chat.gameObject);
        Label("Title", chat, 16, 10, 420, 32, "TRÒ CHUYỆN [T]", 24);
        Ref(data, "chatClose", Button("CloseChat", chat, Vector2.one, new Vector2(-28, -25), new Vector2(32, 32), "×"));
        var viewport = Rect("Messages", chat, Vector2.zero, Vector2.one, new Vector2(0, 27), new Vector2(-32, -150), Vector2.one * .5f);
        viewport.gameObject.AddComponent<RectMask2D>();
        // Transparent image receives scroll events only inside the chat window.
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.viewport = viewport; scroll.scrollSensitivity = 24;
        var text = Text("History", viewport, Vector2.up, Vector2.one, Vector2.zero, new Vector2(-8, 0), "Alice: Xin chào 😊\nBob: 👍🎉🔥❤️👏", 22);
        text.rectTransform.pivot = new Vector2(.5f, 1);
        var fit = text.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = text.rectTransform; Ref(data, "chatScroll", scroll); Ref(data, "chatMessages", text);
        var inputRect = Rect("ChatInput", chat, Vector2.zero, Vector2.zero, new Vector2(220, 36), new Vector2(408, 42), Vector2.one * .5f);
        inputRect.gameObject.AddComponent<Image>().color = new Color(.08f, .09f, .12f);
        var input = inputRect.gameObject.AddComponent<TMP_InputField>();
        var inputViewport = Rect("Viewport", inputRect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16, -4), Vector2.one * .5f);
        inputViewport.gameObject.AddComponent<RectMask2D>(); input.textViewport = inputViewport;
        var inputText = Text("Text", inputViewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "", 22);
        var placeholder = Text("Placeholder", inputViewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "Nhập tin nhắn...", 22);
        placeholder.color = new Color(.58f, .62f, .70f);
        input.textComponent = (TextMeshProUGUI)inputText; input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine; input.fontAsset = font; input.richText = false;
        Ref(data, "chatInput", input); Ref(data, "sendButton", Button("Send", chat, Vector2.zero, new Vector2(468, 36), new Vector2(72, 42), "Gửi"));
        string[] emojis = { "😊", "👍", "🎉", "🔥", "❤️", "👏" }; var buttons = new Button[6];
        for (int i = 0; i < emojis.Length; i++) buttons[i] = Button("Emoji" + i, chat, Vector2.zero, new Vector2(55 + i * 82, 86), new Vector2(72, 36), emojis[i]);
        Refs(data, "emojiButtons", buttons); Ref(data, "emojiSprites", sprites);
        Ref(data, "chatToggle", Button("ChatToggle", parent, Vector2.right, new Vector2(-114, 50), new Vector2(180, 48), "Chat [T]"));
    }

    private static void ConfigureRoomItem()
    {
        string path = "Assets/Resources/RoomItem.prefab";
        var item = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var id = item.transform.Find("ID")?.GetComponent<TMP_Text>();
            var status = item.transform.Find("Status")?.GetComponent<TMP_Text>();
            var occupancy = item.transform.Find("Occupancy");
            if (occupancy != null) UnityEngine.Object.DestroyImmediate(occupancy.gameObject);
            var data = new SerializedObject(item.GetComponent<UIRoomItem>());
            Ref(data, "roomIdText", id);
            if (id != null) { id.text = "Mã: ROOM01"; id.gameObject.SetActive(true); }
            if (status != null) { status.text = "Đang chờ 2/4"; status.rectTransform.sizeDelta = new Vector2(230, status.rectTransform.sizeDelta.y); }
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(item, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(item); }
    }

    private static void ConfigureLobby(string name)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
        var lobby = UnityEngine.Object.FindFirstObjectByType<LobbyUIController>();
        var data = new SerializedObject(lobby);
        var rooms = UnityEngine.Object.FindFirstObjectByType<LobbyRoomsPanel>();
        var roomsData = new SerializedObject(rooms);
        var input = roomsData.FindProperty("roomQuickJoinInput").objectReferenceValue as TMP_InputField;
        input.interactable = true; input.readOnly = false;
        // The quick-join controls were behind the Rooms background in the saved hierarchy.
        input.transform.SetAsLastSibling();
        var join = roomsData.FindProperty("quickJoinButton").objectReferenceValue as Button;
        join.transform.SetAsLastSibling();
        var canvas = lobby.GetComponentInParent<Canvas>();
        var invite = canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "InviteBtn");
        if (invite == null)
            invite = Button("InviteBtn", canvas.transform, new Vector2(.78f, 1), new Vector2(-95, -220), new Vector2(170, 44), "Mời bạn");
        var sourceImage = join.GetComponent<Image>();
        var inviteImage = invite.GetComponent<Image>();
        inviteImage.sprite = sourceImage.sprite; inviteImage.type = sourceImage.type; inviteImage.color = sourceImage.color;
        invite.colors = join.colors;
        var sourceLabel = join.GetComponentInChildren<TMP_Text>();
        var inviteLabel = invite.GetComponentInChildren<TMP_Text>();
        inviteLabel.font = sourceLabel.font; inviteLabel.color = sourceLabel.color;
        Ref(data, "inviteButton", invite); invite.gameObject.SetActive(false);
        data.ApplyModifiedPropertiesWithoutUndo();
        ConfigureJoinDialogs(canvas.transform);
        EditorSceneManager.SaveScene(scene);
    }

    private static TMP_InputField DialogInput(Transform parent)
    {
        var rect = Rect("PasswordInput", parent, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(420, 48), Vector2.one * .5f);
        rect.gameObject.AddComponent<Image>().color = new Color(.08f, .09f, .12f);
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        var viewport = Rect("Viewport", rect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16, -4), Vector2.one * .5f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var label = Text("Text", viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "");
        input.textViewport = viewport; input.textComponent = (TextMeshProUGUI)label;
        input.fontAsset = font; input.contentType = TMP_InputField.ContentType.Password;
        return input;
    }

    private static void ConfigureJoinDialogs(Transform parent)
    {
        var previous = parent.GetComponentInChildren<RoomJoinDialogs>(true);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var root = Rect("RoomJoinDialogs", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.one * .5f);
        var component = root.gameObject.AddComponent<RoomJoinDialogs>(); var data = new SerializedObject(component);
        foreach (bool password in new[] { true, false })
        {
            var veil = Rect(password ? "PasswordDialog" : "InviteDialog", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            veil.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .75f);
            var card = Box("Card", veil, Vector2.one * .5f, Vector2.zero, new Vector2(600, 300), Vector2.one * .5f);
            var title = Label("Title", card, 24, 24, 552, 100, password ? "Mật khẩu phòng" : "Lời mời vào phòng", 27);
            title.alignment = TextAlignmentOptions.Center;
            if (password)
            {
                Ref(data, "passwordPanel", veil.gameObject); Ref(data, "passwordTitle", title);
                Ref(data, "passwordInput", DialogInput(card));
                Ref(data, "passwordSubmit", Button("Join", card, new Vector2(.5f, 0), new Vector2(-120, 55), new Vector2(200, 48), "Tham gia"));
                Ref(data, "passwordCancel", Button("Cancel", card, new Vector2(.5f, 0), new Vector2(120, 55), new Vector2(200, 48), "Hủy"));
            }
            else
            {
                Ref(data, "invitePanel", veil.gameObject); Ref(data, "inviteMessage", title);
                Ref(data, "inviteAccept", Button("Accept", card, new Vector2(.5f, 0), new Vector2(-120, 55), new Vector2(200, 48), "Tham gia"));
                Ref(data, "inviteDecline", Button("Decline", card, new Vector2(.5f, 0), new Vector2(120, 55), new Vector2(200, 48), "Từ chối"));
            }
            veil.gameObject.SetActive(false);
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
