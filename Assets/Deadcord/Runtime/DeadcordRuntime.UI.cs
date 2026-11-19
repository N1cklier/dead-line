using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class DeadcordRuntime
{
    #region Build runtime UI

    private void FindAssetsAndManagers()
    {
        _playerHunger = FindObjectOfType<PlayerHunger>();
        if (_playerHunger == null)
            Debug.LogWarning("[Deadcord] PlayerHunger не найден: шкала голода и иконка мяса не будут скрываться автоматически.", this);

        _font = interfaceFont;
        if (_font == null)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            Debug.LogWarning(
                "[Deadcord] Interface Font is not assigned. Cyrillic characters may be missing in WebGL. " +
                "Import a Cyrillic TTF/OTF font and assign it to the Interface Font field.", this);
        }

        if (_uiSprite == null)
        {
            Texture2D whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.name = "Deadcord_RuntimeWhiteTexture";
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            _uiSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
            _uiSprite.name = "Deadcord_RuntimeUISprite";
        }

        if (_avatarSprite == null)
        {
            _avatarSprite = _uiSprite;
        }
    }

    private void BuildInterface()
    {
        EnsureEventSystem();

        GameObject rootObject = new GameObject("Deadcord_RuntimeUI", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        rootObject.transform.SetParent(transform, false);
        _runtimeRoot = rootObject.GetComponent<RectTransform>();
        Stretch(_runtimeRoot, 0f, 0f, 0f, 0f);
        SetImageColor(rootObject.GetComponent<Image>(), HexColor(COLOR_BG));
        rootObject.GetComponent<Image>().raycastTarget = true;

        BuildHeader(_runtimeRoot);

        GameObject bodyObject = new GameObject("Body", typeof(RectTransform));
        bodyObject.transform.SetParent(_runtimeRoot, false);
        RectTransform body = bodyObject.GetComponent<RectTransform>();
        Stretch(body, 0f, 0f, 0f, 58f);

        BuildSidebar(body);
        BuildMainContent(body);
        RefreshStatusUI();
    }

    private void BuildHeader(RectTransform parent)
    {
        GameObject header = CreatePanel("TopBar", parent, HexColor(COLOR_HEADER));
        SetTop(header.GetComponent<RectTransform>(), 58f, 0f, 0f, 0f);

        CreateText("Brand", header.transform, "DEADCORD   /   Манулятник", 36, HexColor("#FFFFFF"), TextAnchor.MiddleLeft,
            rt => Stretch(rt, 20f, 0f, 240f, 0f), FontStyle.Bold);

        _headerWaveText = CreateText("DayWave", header.transform, "ДЕНЬ 01  •  ВОЛНА 1/3", 18, HexColor(COLOR_MUTED), TextAnchor.MiddleRight,
            rt => Stretch(rt, 240f, 0f, 72f, 0f), FontStyle.Bold);

        Button closeButton = CreateButton("CloseButton", header.transform, "×", HexColor("#3F4148"), HexColor("#FFFFFF"), 30,
            rt => SetRight(rt, 42f, 8f, 12f, 8f), CloseDeadcord);
    }

    private void BuildSidebar(RectTransform body)
    {
        GameObject sidebarObject = CreatePanel("ServerSidebar", body, HexColor(COLOR_SIDEBAR));
        _sidebar = sidebarObject.GetComponent<RectTransform>();
        _sidebar.anchorMin = new Vector2(0f, 0f);
        _sidebar.anchorMax = new Vector2(0f, 1f);
        _sidebar.pivot = new Vector2(0.5f, 0.5f);
        _sidebar.sizeDelta = new Vector2(236f, 0f);
        _sidebar.anchoredPosition = new Vector2(118f, 0f);

        CreateText("ServerTitle", sidebarObject.transform, "МАНУЛЯТНИК", 21, HexColor("#FFFFFF"), TextAnchor.MiddleLeft,
            rt => SetTop(rt, 36f, 12f, 14f, 14f), FontStyle.Bold);
        CreateText("ChannelLabel", sidebarObject.transform, "ТЕКСТОВЫЕ КАНАЛЫ", 13, HexColor(COLOR_MUTED), TextAnchor.MiddleLeft,
            rt => SetTop(rt, 24f, 50f, 14f, 14f), FontStyle.Bold);

        _generalButton = CreateButton("GeneralChannelButton", sidebarObject.transform, "#   общий", HexColor(COLOR_CHANNEL_ACTIVE), HexColor(COLOR_TEXT), 18,
            rt => SetTop(rt, 38f, 76f, 10f, 10f), () => SetChannel(false));
        _moderationButton = CreateButton("ModerationChannelButton", sidebarObject.transform, "#   модерация", HexColor(COLOR_CHANNEL_IDLE), HexColor(COLOR_TEXT), 18,
            rt => SetTop(rt, 38f, 118f, 10f, 10f), () => SetChannel(true));

        GameObject stats = CreatePanel("ReputationPanel", sidebarObject.transform, HexColor("#232428"));
        SetBottom(stats.GetComponent<RectTransform>(), 168f, 12f, 12f, 12f);
        CreateText("ReputationLabel", stats.transform, "РЕПУТАЦИЯ", 13, HexColor(COLOR_MUTED), TextAnchor.MiddleLeft,
            rt => SetTop(rt, 24f, 8f, 10f, 10f), FontStyle.Bold);
        _reputationText = CreateText("ReputationValue", stats.transform, "50 / 100", 29, HexColor("#FFFFFF"), TextAnchor.MiddleLeft,
            rt => SetTop(rt, 34f, 30f, 10f, 10f), FontStyle.Bold);

        GameObject bar = CreatePanel("ReputationBar", stats.transform, HexColor("#404249"));
        SetTop(bar.GetComponent<RectTransform>(), 8f, 72f, 10f, 10f);
        GameObject fill = CreatePanel("ReputationFill", bar.transform, HexColor(COLOR_GREEN));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0.5f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        _reputationFill = fill.GetComponent<Image>();

        _shiftStatusText = CreateText("ShiftStatus", stats.transform, "Смена активна", 15, HexColor(COLOR_GREEN), TextAnchor.UpperLeft,
            rt => Stretch(rt, 10f, 10f, 10f, 88f), FontStyle.Normal);
    }

    private void BuildMainContent(RectTransform body)
    {
        GameObject main = new GameObject("MainContent", typeof(RectTransform));
        main.transform.SetParent(body, false);
        _mainContent = main.GetComponent<RectTransform>();
        Stretch(_mainContent, 236f, 0f, 0f, 0f);

        GameObject channelHeading = CreatePanel("ChannelHeading", _mainContent, HexColor(COLOR_BG));
        SetTop(channelHeading.GetComponent<RectTransform>(), 58f, 0f, 0f, 0f);
        _channelHeaderText = CreateText("ChannelHeadingText", channelHeading.transform, "#   общий", 23, HexColor("#FFFFFF"), TextAnchor.MiddleLeft,
            rt => Stretch(rt, 18f, 0f, 18f, 0f), FontStyle.Bold);
        GameObject divider = CreatePanel("HeadingDivider", channelHeading.transform, HexColor("#242529"));
        SetBottom(divider.GetComponent<RectTransform>(), 1f, 0f, 0f, 0f);

        BuildMessagesArea(_mainContent);
        BuildTypingIndicator(_mainContent);
        BuildInputRow(_mainContent);
        BuildRulesPanel(_mainContent);
    }

    private void BuildMessagesArea(RectTransform parent)
    {
        GameObject scrollObject = new GameObject("MessagesScrollRect", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        Stretch(scrollRectTransform, 12f, 82f, 12f, 60f);
        Image scrollImage = scrollObject.GetComponent<Image>();
        SetImageColor(scrollImage, new Color(0f, 0f, 0f, 0.001f));

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        Stretch(viewportRect, 0f, 0f, 0f, 0f);
        SetImageColor(viewportObject.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.001f));

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        _messagesContent = contentObject.GetComponent<RectTransform>();
        _messagesContent.anchorMin = new Vector2(0f, 1f);
        _messagesContent.anchorMax = new Vector2(1f, 1f);
        _messagesContent.pivot = new Vector2(0.5f, 1f);
        _messagesContent.anchoredPosition = Vector2.zero;
        _messagesContent.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 12, 12);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _messagesScroll = scrollObject.GetComponent<ScrollRect>();
        _messagesScroll.viewport = viewportRect;
        _messagesScroll.content = _messagesContent;
        _messagesScroll.horizontal = false;
        _messagesScroll.vertical = true;
        _messagesScroll.movementType = ScrollRect.MovementType.Clamped;
        _messageScrollObject = scrollObject;
    }

    private void BuildTypingIndicator(RectTransform parent)
    {
        GameObject typing = new GameObject("HelperTypingIndicator", typeof(RectTransform));
        typing.transform.SetParent(parent, false);
        SetBottom(typing.GetComponent<RectTransform>(), 22f, 58f, 12f, 12f);
        _typingText = CreateText("TypingText", typing.transform, "DeadcordHelper печатает...", 16, HexColor(COLOR_MUTED), TextAnchor.MiddleLeft,
            rt => Stretch(rt, 0f, 0f, 0f, 0f), FontStyle.Italic);
        _typingRowObject = typing;
        typing.SetActive(false);
    }

    private void BuildInputRow(RectTransform parent)
    {
        GameObject row = CreatePanel("MessageInputRow", parent, HexColor(COLOR_BG));
        SetBottom(row.GetComponent<RectTransform>(), 54f, 0f, 12f, 12f);

        GameObject inputObject = CreatePanel("CommandInputBackground", row.transform, HexColor("#383A40"));
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        Stretch(inputRect, 0f, 7f, 100f, 7f);
        _commandInput = inputObject.AddComponent<InputField>();
        _commandInput.lineType = InputField.LineType.SingleLine;
        _commandInput.characterLimit = 180;
        _commandInput.contentType = InputField.ContentType.Standard;
        _commandInput.shouldHideMobileInput = true;

        Text valueText = CreateText("InputText", inputObject.transform, "", 19, HexColor(COLOR_TEXT), TextAnchor.MiddleLeft,
            rt => Stretch(rt, 12f, 3f, 12f, 3f), FontStyle.Normal);
        valueText.supportRichText = false;
        valueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        valueText.verticalOverflow = VerticalWrapMode.Truncate;
        _commandInput.textComponent = valueText;
        _commandInput.targetGraphic = inputObject.GetComponent<Image>();
        _commandInput.onEndEdit.AddListener(_ =>
        {
            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                SubmitCommandInput();
        });

        Text placeholder = CreateText("Placeholder", inputObject.transform, "Сообщение в #общий или команда /ban @Ник...", 17, HexColor(COLOR_MUTED), TextAnchor.MiddleLeft,
            rt => Stretch(rt, 12f, 3f, 12f, 3f), FontStyle.Italic);
        placeholder.supportRichText = false;
        _commandInput.placeholder = placeholder;

        CreateButton("SendButton", row.transform, "Отправить", HexColor(COLOR_BLUE), HexColor("#FFFFFF"), 17,
            rt => SetRight(rt, 86f, 7f, 7f, 0f), SubmitCommandInput);
        _inputRowObject = row;
    }

    private void BuildRulesPanel(RectTransform parent)
    {
        GameObject rules = CreatePanel("ModerationInstructions", parent, HexColor(COLOR_BG));
        Stretch(rules.GetComponent<RectTransform>(), 20f, 12f, 20f, 66f);
        _rulesText = CreateText("RulesText", rules.transform, "", 20, HexColor(COLOR_TEXT), TextAnchor.UpperLeft,
            rt => Stretch(rt, 0f, 68f, 0f, 0f), FontStyle.Normal);
        _rulesText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _rulesText.verticalOverflow = VerticalWrapMode.Overflow;

        _acceptRulesButton = CreateButton("AcceptRulesReaction", rules.transform, "✅  ПРИНЯТЬ ПРАВИЛА",
            HexColor("#248046"), HexColor("#FFFFFF"), 18,
            rt => SetBottom(rt, 44f, 10f, 10f, 10f), AcceptRulesReaction);
        _rulesPanelObject = rules;
        rules.SetActive(false);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("Deadcord_EventSystem", typeof(EventSystem));
        Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModuleType != null)
        {
            Component module = eventSystemObject.AddComponent(inputSystemModuleType);
            MethodInfo assignDefaults = inputSystemModuleType.GetMethod("AssignDefaultActions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (assignDefaults != null)
            {
                try { assignDefaults.Invoke(module, null); }
                catch (Exception exception) { Debug.LogWarning("[Deadcord] Не удалось назначить стандартные UI actions: " + exception.Message); }
            }
        }
        else
        {
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }
    }

    private GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = _uiSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return go;
    }

    private Text CreateText(string name, Transform parent, string value, int fontSize, Color color, TextAnchor alignment,
        Action<RectTransform> configureRect, FontStyle style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        configureRect(rect);
        Text text = go.GetComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = Mathf.Max(1, Mathf.RoundToInt(fontSize * Mathf.Clamp(fontScale, 0.75f, 2f)));
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.supportRichText = true;
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Color bg, Color fg, int fontSize,
        Action<RectTransform> configureRect, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        configureRect(rect);
        Image image = go.GetComponent<Image>();
        image.sprite = _uiSprite;
        image.type = Image.Type.Sliced;
        image.color = bg;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        ColorBlock colors = button.colors;
        colors.normalColor = bg;
        colors.highlightedColor = Color.Lerp(bg, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(bg, Color.black, 0.18f);
        colors.selectedColor = bg;
        colors.disabledColor = new Color(bg.r, bg.g, bg.b, 0.5f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        CreateText("Label", go.transform, label, fontSize, fg, TextAnchor.MiddleCenter,
            childRect => Stretch(childRect, 8f, 2f, 8f, 2f), FontStyle.Normal);
        return button;
    }

    private void SetTop(RectTransform rect, float height, float top, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -(top + height));
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void SetBottom(RectTransform rect, float height, float bottom, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
    }

    private void SetRight(RectTransform rect, float width, float top, float right, float bottom)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(-right - width, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void SetImageColor(Image image, Color color)
    {
        if (image == null) return;
        image.sprite = _uiSprite;
        image.color = color;
    }

    private Color HexColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color)) return color;
        return Color.white;
    }

    #endregion
}
