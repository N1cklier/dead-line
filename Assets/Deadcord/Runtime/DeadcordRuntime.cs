using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Self-contained v0.1 Deadcord prototype.
/// Attach this component to the same GameObject referenced by ComputerInteraction.chatPanel.
/// It builds its UI at runtime and optionally calls the existing GameManager.OnSuccess/OnError.
/// </summary>
public class DeadcordRuntime : MonoBehaviour
{
    [Header("Data (JSON files live under Assets/Deadcord/Resources/DeadcordData)")]
    [SerializeField] private string dataFolder = "DeadcordData";

    [Header("NPC timing")]
    [SerializeField, Min(0.5f)] private float npcMessageIntervalMin = 3.5f;
    [SerializeField, Min(1f)] private float npcMessageIntervalMax = 5.5f;
    [SerializeField, Range(0f, 1f)] private float violationChance = 0.42f;
    [SerializeField, Min(1f)] private float helperDelaySeconds = 15f;
    [SerializeField, Min(0f)] private float helperDelayVarianceSeconds = 5f;
    [SerializeField, Min(0.1f)] private float helperTypingSeconds = 1.4f;
    [SerializeField, Min(20f)] private float maxStoredMessages = 160f;

    [Header("Audio - optional; leave empty until a sound is chosen")]
    [Tooltip("Assign a short notification clip here later. Null is safe and intentionally silent.")]
    public AudioClip newMessageSound;
    [Tooltip("Optional AudioSource. If empty and a clip is assigned, one is created automatically.")]
    public AudioSource messageAudioSource;

    [Header("Interface")]
    [SerializeField] private bool hideOldPanelChildren = true;
    [SerializeField] private string helperUsername = "DeadcordHelper";

    private Font _font;
    private Sprite _uiSprite;
    private Sprite _avatarSprite;

    private GameManager _gameManager;
    private PlayerMovement[] _playerMovements;
    private readonly List<bool> _playerMovementWasEnabled = new List<bool>();
    private bool _cursorWasVisible;
    private CursorLockMode _cursorLockWas;
    private bool _controlsLocked;

    private RectTransform _runtimeRoot;
    private RectTransform _sidebar;
    private RectTransform _mainContent;
    private RectTransform _messagesContent;
    private ScrollRect _messagesScroll;
    private GameObject _messageScrollObject;
    private GameObject _typingRowObject;
    private GameObject _inputRowObject;
    private GameObject _rulesPanelObject;
    private Text _headerWaveText;
    private Text _channelHeaderText;
    private Text _reputationText;
    private Text _shiftStatusText;
    private Text _typingText;
    private Text _rulesText;
    private InputField _commandInput;
    private Image _reputationFill;
    private Button _generalButton;
    private Button _moderationButton;

    private bool _initialized;
    private bool _shiftStarted;
    private bool _shiftEnded;
    private bool _showingModeration;
    private float _shiftElapsed;
    private float _nextNpcMessageAt;
    private float _nextWaveAt;
    private int _waveIndex;
    private int _nextStoryIndex;
    private float _localReputation = 50f;
    private int _incidentSequence;

    private NamePartsData _nameParts;
    private NormalMessagesData _normalMessages;
    private ViolationsData _violations;
    private WaveRulesData _waveRules;
    private StoryData _story;

    private readonly List<UserData> _users = new List<UserData>();
    private readonly Dictionary<string, UserData> _usersByName = new Dictionary<string, UserData>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<ChatMessage> _messages = new List<ChatMessage>();
    private IncidentData _activeIncident;

    private readonly Color[] _avatarColors =
    {
        new Color32(88, 101, 242, 255),
        new Color32(67, 181, 129, 255),
        new Color32(237, 66, 69, 255),
        new Color32(235, 170, 65, 255),
        new Color32(155, 89, 182, 255),
        new Color32(52, 152, 219, 255),
        new Color32(230, 126, 34, 255),
        new Color32(46, 204, 113, 255)
    };

    private const string COLOR_BG = "#313338";
    private const string COLOR_HEADER = "#1E1F22";
    private const string COLOR_SIDEBAR = "#2B2D31";
    private const string COLOR_CHANNEL_ACTIVE = "#404249";
    private const string COLOR_CHANNEL_IDLE = "#2B2D31";
    private const string COLOR_TEXT = "#DBDEE1";
    private const string COLOR_MUTED = "#949BA4";
    private const string COLOR_GREEN = "#43B581";
    private const string COLOR_RED = "#ED4245";
    private const string COLOR_BLUE = "#5865F2";
    private const string COLOR_GOLD = "#F0B232";

    #region Unity lifecycle

    private void Awake()
    {
        LoadData();
        FindAssetsAndManagers();
        BuildInterface();
        _initialized = true;
    }

    private void OnEnable()
    {
        if (!_initialized)
            return;

        FindAssetsAndManagers();
        LockWorldControls();

        if (_gameManager != null)
            _gameManager.SetMode(GameManager.Mode.Chat);

        if (!_shiftStarted)
            BeginShift();

        SetChannel(false);
    }

    private void OnDisable()
    {
        RestoreWorldControls();
    }

    private void Update()
    {
        if (!_initialized || !isActiveAndEnabled)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseDeadcord();
            return;
        }

        if (_shiftEnded)
        {
            RefreshStatusUI();
            return;
        }

        if (!_showingModeration && _commandInput != null && _commandInput.isFocused && Keyboard.current != null)
        {
            bool enterPressed = Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
            if (enterPressed)
                SubmitCommandInput();
        }

        _shiftElapsed += Time.unscaledDeltaTime;
        AdvanceWaves();
        PlayStoryEvents();
        SpawnNpcMessageIfDue();
        UpdateIncidentTimer();
        RefreshStatusUI();
    }

    #endregion

    #region Data

    [Serializable] private class NamePartsData
    {
        public string[] first;
        public string[] second;
        public string[] third;
    }

    [Serializable] private class NormalMessagesData
    {
        public string[] messages;
    }

    [Serializable] private class ViolationsData
    {
        public ViolationDefinition[] incidents;
    }

    [Serializable] private class ViolationDefinition
    {
        public string id;
        public string category;
        public int minWave;
        public string requiredAction;
        public string[] messages;
    }

    [Serializable] private class WaveRulesData
    {
        public WaveDefinition[] waves;
    }

    [Serializable] private class WaveDefinition
    {
        public int wave;
        public string title;
        public float durationSeconds;
        public string announcement;
        public string[] instructions;
        public string[] unlockedCategories;
    }

    [Serializable] private class StoryData
    {
        public StoryEvent[] events;
    }

    [Serializable] private class StoryEvent
    {
        public float atSeconds;
        public string speakerKey;
        public string username;
        public string text;
        public string incidentCategory;
    }

    private class UserData
    {
        public string username;
        public int colorIndex;
        public bool banned;
        public float mutedUntil;
    }

    private class ChatMessage
    {
        public string username;
        public string text;
        public string role;
        public string reaction;
        public string timestamp;
    }

    private class IncidentData
    {
        public int id;
        public UserData target;
        public string category;
        public string requiredAction;
        public bool resolved;
        public bool helperTyping;
        public float deadlineAt;
        public float helperSendAt;
        public string originalText;
    }

    private T LoadJson<T>(string fileName) where T : class
    {
        TextAsset asset = Resources.Load<TextAsset>(dataFolder + "/" + fileName);
        if (asset == null)
        {
            Debug.LogWarning("[Deadcord] Не найден Resources/" + dataFolder + "/" + fileName + ".json; будут использованы встроенные запасные данные, где они предусмотрены.", this);
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(asset.text);
        }
        catch (Exception exception)
        {
            Debug.LogError("[Deadcord] Ошибка чтения " + fileName + ".json: " + exception.Message, this);
            return null;
        }
    }

    private void LoadData()
    {
        _nameParts = LoadJson<NamePartsData>("NameParts");
        _normalMessages = LoadJson<NormalMessagesData>("NormalMessages");
        _violations = LoadJson<ViolationsData>("Violations");
        _waveRules = LoadJson<WaveRulesData>("Day01Rules");
        _story = LoadJson<StoryData>("Day01Story");

        if (_nameParts == null || _nameParts.first == null || _nameParts.first.Length == 0)
        {
            _nameParts = new NamePartsData
            {
                first = new[] { "Penis", "Giga", "Mega", "Sigma", "Vasya" },
                second = new[] { "Titov", "Bober", "Nuub", "Hren", "Kaban" },
                third = new[] { "67", "_228", "-Xx", "#1337", "_lol" }
            };
        }

        if (_normalMessages == null || _normalMessages.messages == null || _normalMessages.messages.Length == 0)
        {
            _normalMessages = new NormalMessagesData
            {
                messages = new[]
                {
                    "Кто сегодня отвечает за музыку?",
                    "доброе утро чат",
                    "а где закреп с правилами",
                    "у кого-нибудь тоже лагает?",
                    "я на минуту отошёл",
                    "почему тут так тихо",
                    "кто-нибудь видел объявление начальника?",
                    "ладно, вернусь позже"
                }
            };
        }

        if (_violations == null || _violations.incidents == null || _violations.incidents.Length == 0)
        {
            _violations = new ViolationsData
            {
                incidents = new[]
                {
                    new ViolationDefinition { id = "insult", category = "insult", minWave = 1, requiredAction = "ban", messages = new[] { "Ты тупой кретин", "Админ лох и ничего не умеет", "Заткнись, жалкий идиот" } },
                    new ViolationDefinition { id = "laughter", category = "laughter", minWave = 2, requiredAction = "ban", messages = new[] { "АХАХАХАХАХАХАХА", "ХАХАХАХАХАХАХА", "АХАХАХА СИКС СЕВЕН" } },
                    new ViolationDefinition { id = "caps", category = "caps", minWave = 3, requiredAction = "ban", messages = new[] { "ПИСЬКА СИКС СЕВЕН", "ВСЕ СЮДА СРОЧНО", "Я СЕЙЧАС ВСЁ ВАМ ОБЪЯСНЮ" } }
                }
            };
        }

        if (_waveRules == null || _waveRules.waves == null || _waveRules.waves.Length == 0)
        {
            _waveRules = new WaveRulesData
            {
                waves = new[]
                {
                    new WaveDefinition { wave = 1, title = "Волна 1/3 — оскорбления", durationSeconds = 45f, announcement = "Волна 1. Баньте оскорбления.", instructions = new[] { "БАН: прямые оскорбления и травля.", "АХАХ-спам пока разрешён.", "КАПС ПОКА РАЗРЕШЁН." }, unlockedCategories = new[] { "insult" } },
                    new WaveDefinition { wave = 2, title = "Волна 2/3 — оскорбления и смехоспам", durationSeconds = 45f, announcement = "Волна 2. Теперь также баньте спам из повторяющегося «АХАХА». Правила первой волны остаются.", instructions = new[] { "БАН: оскорбления и травля.", "БАН: повторяющийся спам «АХАХА/ХАХАХА».", "КАПС ПОКА РАЗРЕШЁН." }, unlockedCategories = new[] { "insult", "laughter" } },
                    new WaveDefinition { wave = 3, title = "Волна 3/3 — добавляется капс", durationSeconds = 999999f, announcement = "Волна 3. Теперь баньте также сообщения, написанные ЦЕЛИКОМ КАПСОМ.", instructions = new[] { "БАН: оскорбления и травля.", "БАН: повторяющийся спам «АХАХА/ХАХАХА».", "БАН: сообщения, написанные ЦЕЛИКОМ ЗАГЛАВНЫМИ БУКВАМИ (кроме коротких аббревиатур)." }, unlockedCategories = new[] { "insult", "laughter", "caps" } }
                }
            };
        }

        if (_story == null || _story.events == null)
        {
            _story = new StoryData
            {
                events = new[]
                {
                    new StoryEvent { atSeconds = 1f, speakerKey = "boss", username = "StarshiyMod", text = "Добро пожаловать на смену. Правила смотрите в #модерация." },
                    new StoryEvent { atSeconds = 5f, speakerKey = "coworker", username = "Bober_Staff", text = "Главное — не пропускай нарушения, начальник сегодня злой." },
                    new StoryEvent { atSeconds = 13f, speakerKey = "plot_user_01", username = "VasyaKaban_228", text = "Ты тупой кретин, модератор", incidentCategory = "insult" }
                }
            };
        }
    }

    #endregion

    #region Build runtime UI

    private void FindAssetsAndManagers()
    {
        _gameManager = FindObjectOfType<GameManager>();
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _uiSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        _avatarSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");

        // Fallback texture prevents the UI from becoming invisible if a Unity version
        // does not expose the built-in uGUI sprites under these resource names.
        if (_uiSprite == null)
        {
            Texture2D whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.name = "Deadcord_RuntimeWhiteTexture";
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            _uiSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            _uiSprite.name = "Deadcord_RuntimeWhiteSprite";
        }
        if (_avatarSprite == null)
            _avatarSprite = _uiSprite;
    }

    private void BuildInterface()
    {
        if (hideOldPanelChildren)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                transform.GetChild(i).gameObject.SetActive(false);
        }

        EnsureEventSystem();

        GameObject rootObject = new GameObject("Deadcord_RuntimeUI", typeof(RectTransform), typeof(Image));
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

        CreateText("Brand", header.transform, "DEADCORD   /   Манулятник", 22, HexColor("#FFFFFF"), TextAnchor.MiddleLeft,
            rt => Stretch(rt, 20f, 0f, 240f, 0f), FontStyle.Bold);

        _headerWaveText = CreateText("DayWave", header.transform, "ДЕНЬ 01  •  ВОЛНА 1/3", 15, HexColor(COLOR_MUTED), TextAnchor.MiddleRight,
            rt => Stretch(rt, 240f, 0f, 72f, 0f), FontStyle.Bold);

        Button closeButton = CreateButton("CloseButton", header.transform, "×", HexColor("#3F4148"), HexColor("#FFFFFF"), 26,
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

        CreateText("ServerTitle", sidebarObject.transform, "МАНУЛЯТНИК", 16, HexColor("#FFFFFF"), TextAnchor.MiddleLeft,
            rt => SetTop(rt, 36f, 12f, 14f, 14f), FontStyle.Bold);
        CreateText("ChannelLabel", sidebarObject.transform, "ТЕКСТОВЫЕ КАНАЛЫ", 10, HexColor(COLOR_MUTED), TextAnchor.MiddleLeft,
            rt => SetTop(rt, 24f, 50f, 14f, 14f), FontStyle.Bold);

        _generalButton = CreateButton("GeneralChannelButton", sidebarObject.transform, "#   общий", HexColor(COLOR_CHANNEL_ACTIVE), HexColor(COLOR_TEXT), 15,
            rt => SetTop(rt, 38f, 76f, 10f, 10f), () => SetChannel(false));
        _moderationButton = CreateButton("ModerationChannelButton", sidebarObject.transform, "#   модерация", HexColor(COLOR_CHANNEL_IDLE), HexColor(COLOR_TEXT), 15,
            rt => SetTop(rt, 38f, 118f, 10f, 10f), () => SetChannel(true));

        GameObject stats = CreatePanel("ReputationPanel", sidebarObject.transform, HexColor("#232428"));
        SetBottom(stats.GetComponent<RectTransform>(), 168f, 12f, 12f, 12f);
        CreateText("ReputationLabel", stats.transform, "РЕПУТАЦИЯ", 11, HexColor(COLOR_MUTED), TextAnchor.MiddleLeft,
            rt => SetTop(rt, 24f, 8f, 10f, 10f), FontStyle.Bold);
        _reputationText = CreateText("ReputationValue", stats.transform, "50 / 100", 23, HexColor("#FFFFFF"), TextAnchor.MiddleLeft,
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

        _shiftStatusText = CreateText("ShiftStatus", stats.transform, "Смена активна", 12, HexColor(COLOR_GREEN), TextAnchor.UpperLeft,
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
        _channelHeaderText = CreateText("ChannelHeadingText", channelHeading.transform, "#  общий", 18, HexColor("#FFFFFF"), TextAnchor.MiddleLeft,
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
        _typingText = CreateText("TypingText", typing.transform, "DeadcordHelper печатает...", 12, HexColor(COLOR_MUTED), TextAnchor.MiddleLeft,
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

        Text valueText = CreateText("InputText", inputObject.transform, "", 15, HexColor(COLOR_TEXT), TextAnchor.MiddleLeft,
            rt => Stretch(rt, 12f, 3f, 12f, 3f), FontStyle.Normal);
        valueText.supportRichText = false;
        valueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        valueText.verticalOverflow = VerticalWrapMode.Truncate;
        _commandInput.textComponent = valueText;
        _commandInput.targetGraphic = inputObject.GetComponent<Image>();
        _commandInput.onEndEdit.AddListener(_ =>
        {
            // InputField fires onEndEdit both on Enter and on focus loss.
            // Only treat it as submit when Enter was actually pressed.
            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                SubmitCommandInput();
        });

        Text placeholder = CreateText("Placeholder", inputObject.transform, "Сообщение в #общий или команда /ban @Ник...", 14, HexColor(COLOR_MUTED), TextAnchor.MiddleLeft,
            rt => Stretch(rt, 12f, 3f, 12f, 3f), FontStyle.Italic);
        placeholder.supportRichText = false;
        _commandInput.placeholder = placeholder;

        CreateButton("SendButton", row.transform, "Отправить", HexColor(COLOR_BLUE), HexColor("#FFFFFF"), 14,
            rt => SetRight(rt, 86f, 7f, 7f, 0f), SubmitCommandInput);
        _inputRowObject = row;
    }

    private void BuildRulesPanel(RectTransform parent)
    {
        GameObject rules = CreatePanel("ModerationInstructions", parent, HexColor(COLOR_BG));
        Stretch(rules.GetComponent<RectTransform>(), 20f, 12f, 20f, 66f);
        _rulesText = CreateText("RulesText", rules.transform, "", 17, HexColor(COLOR_TEXT), TextAnchor.UpperLeft,
            rt => Stretch(rt, 0f, 0f, 0f, 0f), FontStyle.Normal);
        _rulesText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _rulesText.verticalOverflow = VerticalWrapMode.Overflow;
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
        text.fontSize = fontSize;
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

    #region Shift and waves

    private void BeginShift()
    {
        _shiftStarted = true;
        _shiftEnded = false;
        _shiftElapsed = 0f;
        _waveIndex = 0;
        _nextStoryIndex = 0;
        _nextNpcMessageAt = 3.5f;
        _activeIncident = null;
        GenerateInitialUsers(10);

        if (_waveRules.waves == null || _waveRules.waves.Length == 0)
            return;

        _nextWaveAt = Mathf.Max(1f, _waveRules.waves[0].durationSeconds);
        RefreshInstructions();
        AddSystemMessage("Смена 01 началась. Все сообщения идут в #общий; правила смены — в #модерация.");
        AddSystemMessage(_waveRules.waves[0].announcement);
        RefreshStatusUI();
    }

    private void AdvanceWaves()
    {
        if (_waveRules.waves == null || _waveRules.waves.Length == 0)
            return;

        while (_waveIndex < _waveRules.waves.Length - 1 && _shiftElapsed >= _nextWaveAt)
        {
            _waveIndex++;
            WaveDefinition wave = _waveRules.waves[_waveIndex];
            _nextWaveAt += Mathf.Max(1f, wave.durationSeconds);
            RefreshInstructions();
            AddSystemMessage(wave.announcement);
            RefreshStatusUI();
        }
    }

    private int CurrentWaveNumber
    {
        get
        {
            if (_waveRules == null || _waveRules.waves == null || _waveRules.waves.Length == 0) return 1;
            return Mathf.Clamp(_waveIndex + 1, 1, _waveRules.waves.Length);
        }
    }

    private void RefreshInstructions()
    {
        if (_rulesText == null || _waveRules == null || _waveRules.waves == null || _waveRules.waves.Length == 0)
            return;

        WaveDefinition wave = _waveRules.waves[Mathf.Clamp(_waveIndex, 0, _waveRules.waves.Length - 1)];
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("МАНУЛЯТНИК  /  ПАМЯТКА МОДЕРАТОРА");
        builder.AppendLine("ДЕНЬ 01  •  " + wave.title);
        builder.AppendLine();
        builder.AppendLine("ЧТО ДЕЛАТЬ");
        builder.AppendLine();

        if (wave.instructions != null)
        {
            for (int i = 0; i < wave.instructions.Length; i++)
            {
                builder.Append("•  ");
                builder.AppendLine(wave.instructions[i]);
                builder.AppendLine();
            }
        }

        builder.AppendLine("КОМАНДЫ");
        builder.AppendLine("/ban @Ник — забанить нарушителя");
        builder.AppendLine("/mute @Ник — замутить пользователя (в текущих правилах не заменяет требуемый бан)");
        builder.AppendLine();
        builder.AppendLine("Все сообщения пользователей появляются только в #общий.");
        builder.AppendLine("Если не успеть, DeadcordHelper напечатает правильную команду за вас.");
        _rulesText.text = builder.ToString();
    }

    private void PlayStoryEvents()
    {
        if (_story == null || _story.events == null)
            return;

        while (_nextStoryIndex < _story.events.Length && _story.events[_nextStoryIndex].atSeconds <= _shiftElapsed)
        {
            StoryEvent storyEvent = _story.events[_nextStoryIndex];
            _nextStoryIndex++;
            if (string.IsNullOrWhiteSpace(storyEvent.text) || string.IsNullOrWhiteSpace(storyEvent.username))
                continue;

            UserData user = GetOrCreateUser(storyEvent.username);
            AddMessage(user.username, storyEvent.text, GetStoryRole(storyEvent.speakerKey));

            if (!string.IsNullOrWhiteSpace(storyEvent.incidentCategory))
            {
                ViolationDefinition definition = FindViolationByCategory(storyEvent.incidentCategory);
                if (definition != null && IsCategoryUnlocked(definition.category) && _activeIncident == null && !user.banned)
                    StartIncident(user, definition, storyEvent.text);
            }
        }
    }

    private string GetStoryRole(string speakerKey)
    {
        if (string.Equals(speakerKey, "boss", StringComparison.OrdinalIgnoreCase)) return "boss";
        if (string.Equals(speakerKey, "helper", StringComparison.OrdinalIgnoreCase)) return "helper";
        return "npc";
    }

    #endregion

    #region NPCs and violations

    private void GenerateInitialUsers(int count)
    {
        for (int i = 0; i < count; i++)
            GetOrCreateUser(GenerateUsername());
    }

    private string GenerateUsername()
    {
        for (int attempt = 0; attempt < 150; attempt++)
        {
            string first = Pick(_nameParts.first, "Player");
            string second = Pick(_nameParts.second, "User");
            string third = Pick(_nameParts.third, UnityEngine.Random.Range(10, 99).ToString());
            string candidate = first + second + third;
            if (_usedNames.Add(candidate))
                return candidate;
        }

        string fallback;
        do { fallback = "Player_" + UnityEngine.Random.Range(1000, 999999); }
        while (!_usedNames.Add(fallback));
        return fallback;
    }

    private string Pick(string[] values, string fallback)
    {
        if (values == null || values.Length == 0) return fallback;
        return values[UnityEngine.Random.Range(0, values.Length)];
    }

    private UserData GetOrCreateUser(string username)
    {
        string safeName = string.IsNullOrWhiteSpace(username) ? GenerateUsername() : username.Trim().TrimStart('@');
        if (_usersByName.TryGetValue(safeName, out UserData existing))
            return existing;

        UserData user = new UserData
        {
            username = safeName,
            colorIndex = UnityEngine.Random.Range(0, _avatarColors.Length),
            banned = false,
            mutedUntil = 0f
        };
        _users.Add(user);
        _usersByName[safeName] = user;
        _usedNames.Add(safeName);
        return user;
    }

    private UserData PickActiveUser()
    {
        List<UserData> candidates = new List<UserData>();
        for (int i = 0; i < _users.Count; i++)
        {
            UserData user = _users[i];
            if (!user.banned && user.mutedUntil <= _shiftElapsed)
                candidates.Add(user);
        }

        if (candidates.Count == 0)
            return GetOrCreateUser(GenerateUsername());
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void SpawnNpcMessageIfDue()
    {
        if (_shiftElapsed < _nextNpcMessageAt)
            return;

        _nextNpcMessageAt = _shiftElapsed + UnityEngine.Random.Range(npcMessageIntervalMin, Mathf.Max(npcMessageIntervalMin + 0.1f, npcMessageIntervalMax));
        UserData speaker = PickActiveUser();

        if (_shiftElapsed >= 20f && _activeIncident == null && UnityEngine.Random.value < violationChance)
        {
            List<ViolationDefinition> available = GetAvailableViolations();
            if (available.Count > 0)
            {
                ViolationDefinition selected = available[UnityEngine.Random.Range(0, available.Count)];
                string violationText = Pick(selected.messages, "Нарушение правил");
                AddMessage(speaker.username, violationText, "npc");
                StartIncident(speaker, selected, violationText);
                return;
            }
        }

        AddMessage(speaker.username, Pick(_normalMessages.messages, "Привет, чат!"), "npc");
    }

    private List<ViolationDefinition> GetAvailableViolations()
    {
        List<ViolationDefinition> result = new List<ViolationDefinition>();
        if (_violations == null || _violations.incidents == null) return result;

        foreach (ViolationDefinition definition in _violations.incidents)
        {
            if (definition != null && definition.minWave <= CurrentWaveNumber && IsCategoryUnlocked(definition.category))
                result.Add(definition);
        }
        return result;
    }

    private ViolationDefinition FindViolationByCategory(string category)
    {
        if (_violations == null || _violations.incidents == null) return null;
        foreach (ViolationDefinition definition in _violations.incidents)
        {
            if (definition != null && string.Equals(definition.category, category, StringComparison.OrdinalIgnoreCase))
                return definition;
        }
        return null;
    }

    private bool IsCategoryUnlocked(string category)
    {
        if (string.IsNullOrWhiteSpace(category) || _waveRules == null || _waveRules.waves == null || _waveRules.waves.Length == 0)
            return false;
        WaveDefinition wave = _waveRules.waves[Mathf.Clamp(_waveIndex, 0, _waveRules.waves.Length - 1)];
        if (wave.unlockedCategories == null) return false;
        foreach (string unlocked in wave.unlockedCategories)
        {
            if (string.Equals(unlocked, category, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void StartIncident(UserData target, ViolationDefinition definition, string sourceText)
    {
        if (target == null || definition == null || _activeIncident != null || target.banned || !IsCategoryUnlocked(definition.category))
            return;

        float delay = helperDelaySeconds + UnityEngine.Random.Range(-helperDelayVarianceSeconds, helperDelayVarianceSeconds);
        delay = Mathf.Max(1f, delay);
        _activeIncident = new IncidentData
        {
            id = ++_incidentSequence,
            target = target,
            category = definition.category,
            requiredAction = string.IsNullOrWhiteSpace(definition.requiredAction) ? "ban" : definition.requiredAction.ToLowerInvariant(),
            resolved = false,
            helperTyping = false,
            deadlineAt = _shiftElapsed + delay,
            helperSendAt = -1f,
            originalText = sourceText
        };
        RefreshTypingIndicator();
    }

    private void UpdateIncidentTimer()
    {
        if (_activeIncident == null || _activeIncident.resolved)
        {
            RefreshTypingIndicator();
            return;
        }

        if (!_activeIncident.helperTyping && _shiftElapsed >= _activeIncident.deadlineAt)
        {
            _activeIncident.helperTyping = true;
            _activeIncident.helperSendAt = _shiftElapsed + helperTypingSeconds;
            RefreshTypingIndicator();
        }

        if (_activeIncident != null && _activeIncident.helperTyping && _shiftElapsed >= _activeIncident.helperSendAt)
            HelperResolveIncident(_activeIncident);
    }

    private void HelperResolveIncident(IncidentData incident)
    {
        if (incident == null || incident.resolved || _activeIncident == null || incident.id != _activeIncident.id)
            return;

        string command = "/" + incident.requiredAction + " @" + incident.target.username;
        AddMessage(helperUsername, command, "helper", "💀  +1");
        ApplyActionToUser(incident.target, incident.requiredAction);
        incident.resolved = true;
        _activeIncident = null;
        ApplyReputationChange(false);
        RefreshTypingIndicator();
        AddSystemMessage("Помощник перехватил нарушение. Репутация снижена.");
        RefreshStatusUI();
    }

    #endregion

    #region Input and moderation commands

    private void SubmitCommandInput()
    {
        if (_commandInput == null || _showingModeration)
            return;

        string input = _commandInput.text;
        if (string.IsNullOrWhiteSpace(input))
            return;

        _commandInput.SetTextWithoutNotify(string.Empty);
        ProcessPlayerInput(input.Trim());

        if (_commandInput != null && _commandInput.gameObject.activeInHierarchy)
        {
            _commandInput.ActivateInputField();
            _commandInput.Select();
        }
    }

    private void ProcessPlayerInput(string input)
    {
        if (!input.StartsWith("/", StringComparison.Ordinal))
        {
            AddMessage("Вы", input, "player");
            return;
        }

        string[] parts = input.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            AddMessage("Вы", input, "player", "💀  -1");
            AddSystemMessage("Формат команды: /ban @Ник или /mute @Ник");
            ApplyReputationChange(false);
            RefreshStatusUI();
            return;
        }

        string action = parts[0].Trim().ToLowerInvariant();
        if (action != "/ban" && action != "/mute" && action != "/unmute")
        {
            AddMessage("Вы", input, "player");
            AddSystemMessage("Неизвестная команда. Доступны /ban, /mute и /unmute.");
            return;
        }

        string targetName = parts[1].Trim().TrimStart('@');
        if (!_usersByName.TryGetValue(targetName, out UserData target))
        {
            AddMessage("Вы", input, "player", "💀  -1");
            AddSystemMessage("Пользователь не найден: " + targetName);
            ApplyReputationChange(false);
            RefreshStatusUI();
            return;
        }

        if (action == "/unmute")
        {
            if (!target.banned && target.mutedUntil > _shiftElapsed)
            {
                target.mutedUntil = 0f;
                AddMessage("Вы", input, "player", "🐸  +1");
                AddSystemMessage(target.username + " снова может писать. Это дополнительная команда прототипа.");
            }
            else
            {
                AddMessage("Вы", input, "player", "💀  -1");
                AddSystemMessage("Этот пользователь сейчас не замучен.");
                ApplyReputationChange(false);
            }
            RefreshStatusUI();
            return;
        }

        bool correctIncident = _activeIncident != null
            && !_activeIncident.resolved
            && _activeIncident.target == target
            && string.Equals(_activeIncident.requiredAction, action.Substring(1), StringComparison.OrdinalIgnoreCase);

        if (correctIncident)
        {
            ApplyActionToUser(target, action.Substring(1));
            _activeIncident.resolved = true;
            _activeIncident = null;
            AddMessage("Вы", input, "player", "🐸  +1");
            ApplyReputationChange(true);
            AddSystemMessage("Верное решение. Нарушение обработано.");
            RefreshTypingIndicator();
        }
        else
        {
            AddMessage("Вы", input, "player", "💀  -1");
            if (_activeIncident == null)
                AddSystemMessage("Сейчас нет активного нарушения для этой команды.");
            else if (_activeIncident.target != target)
                AddSystemMessage("Неверная цель. Проверьте ник активного нарушителя в #общий.");
            else
                AddSystemMessage("Неверное действие. Для этого нарушения требуется /" + _activeIncident.requiredAction + ".");
            ApplyReputationChange(false);
        }

        RefreshStatusUI();
    }

    private void ApplyActionToUser(UserData user, string action)
    {
        if (user == null) return;
        if (string.Equals(action, "ban", StringComparison.OrdinalIgnoreCase))
        {
            user.banned = true;
            user.mutedUntil = 0f;
        }
        else if (string.Equals(action, "mute", StringComparison.OrdinalIgnoreCase))
        {
            user.mutedUntil = _shiftElapsed + 20f;
        }
    }

    private void ApplyReputationChange(bool success)
    {
        if (_gameManager != null)
        {
            if (success) _gameManager.OnSuccess();
            else _gameManager.OnError();
        }
        else
        {
            _localReputation = Mathf.Clamp(_localReputation + (success ? 15f : -15f), 0f, 100f);
        }

        float reputation = CurrentReputation;
        if (!_shiftEnded && reputation <= 0f)
        {
            _shiftEnded = true;
            AddSystemMessage("Репутация обнулилась. Смена провалена: вас уволили.");
        }
        else if (!_shiftEnded && reputation >= 100f)
        {
            _shiftEnded = true;
            AddSystemMessage("Репутация достигла максимума. Вы получили повышение!");
        }
    }

    private float CurrentReputation
    {
        get { return _gameManager != null ? _gameManager.reputation : _localReputation; }
    }

    #endregion

    #region Chat messages and rendering

    private void AddSystemMessage(string text)
    {
        AddMessage("Система", text, "system");
    }

    private void AddMessage(string username, string text, string role, string reaction = "")
    {
        ChatMessage message = new ChatMessage
        {
            username = username,
            text = text,
            role = role,
            reaction = reaction,
            timestamp = FormatClock()
        };
        _messages.Add(message);

        if (_messages.Count > Mathf.RoundToInt(maxStoredMessages))
        {
            _messages.RemoveAt(0);
            if (!_showingModeration)
                RenderAllMessages();
        }
        else if (!_showingModeration)
        {
            RenderMessage(message);
        }

        PlayNewMessageSound();
    }

    private void RenderAllMessages()
    {
        if (_messagesContent == null) return;
        for (int i = _messagesContent.childCount - 1; i >= 0; i--)
            Destroy(_messagesContent.GetChild(i).gameObject);

        for (int i = 0; i < _messages.Count; i++)
            RenderMessage(_messages[i], false);

        ScrollToBottom();
    }

    private void RenderMessage(ChatMessage message, bool scrollAfter = true)
    {
        if (_messagesContent == null || message == null) return;

        GameObject row = new GameObject("Message_" + _messagesContent.childCount, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(_messagesContent, false);
        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.padding = new RectOffset(0, 0, 1, 1);
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        ContentSizeFitter rowFitter = row.GetComponent<ContentSizeFitter>();
        rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement rowElement = row.AddComponent<LayoutElement>();
        rowElement.minHeight = 42f;

        UserData user = (message.role == "system" || message.role == "player" || message.role == "helper")
            ? null
            : GetOrCreateUser(message.username);
        GameObject avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        avatar.transform.SetParent(row.transform, false);
        LayoutElement avatarElement = avatar.GetComponent<LayoutElement>();
        avatarElement.minWidth = 38f;
        avatarElement.preferredWidth = 38f;
        avatarElement.minHeight = 38f;
        avatarElement.preferredHeight = 38f;
        Image avatarImage = avatar.GetComponent<Image>();
        avatarImage.sprite = _avatarSprite != null ? _avatarSprite : _uiSprite;
        avatarImage.type = Image.Type.Simple;
        avatarImage.color = ColorForMessage(message, user);
        Text initials = CreateText("AvatarInitials", avatar.transform, GetInitials(message.username), 12, HexColor("#FFFFFF"), TextAnchor.MiddleCenter,
            rt => Stretch(rt, 0f, 0f, 0f, 0f), FontStyle.Bold);

        GameObject textColumn = new GameObject("MessageTextColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        textColumn.transform.SetParent(row.transform, false);
        LayoutElement textColumnElement = textColumn.GetComponent<LayoutElement>();
        textColumnElement.flexibleWidth = 1f;
        textColumnElement.minWidth = 60f;
        VerticalLayoutGroup textLayout = textColumn.GetComponent<VerticalLayoutGroup>();
        textLayout.spacing = 2f;
        textLayout.childAlignment = TextAnchor.UpperLeft;
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = true;
        textLayout.childForceExpandWidth = true;
        textLayout.childForceExpandHeight = false;

        string nameLine = message.username + "   " + message.timestamp;
        Text usernameText = CreateText("UsernameAndTime", textColumn.transform, nameLine, 13, ColorForMessage(message, user), TextAnchor.MiddleLeft,
            rt => rt.sizeDelta = new Vector2(0f, 19f), FontStyle.Bold);
        LayoutElement usernameLayout = usernameText.gameObject.AddComponent<LayoutElement>();
        usernameLayout.minHeight = 19f;
        usernameLayout.preferredHeight = 19f;

        Text bodyText = CreateText("MessageBody", textColumn.transform, message.text, 15, HexColor(COLOR_TEXT), TextAnchor.UpperLeft,
            rt => rt.sizeDelta = new Vector2(0f, 0f), FontStyle.Normal);
        bodyText.supportRichText = false;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        LayoutElement bodyLayout = bodyText.gameObject.AddComponent<LayoutElement>();
        bodyLayout.flexibleWidth = 1f;
        bodyLayout.minHeight = 18f;

        if (!string.IsNullOrWhiteSpace(message.reaction))
        {
            GameObject reaction = CreatePanel("ReactionChip", textColumn.transform, HexColor("#383A40"));
            LayoutElement reactionElement = reaction.AddComponent<LayoutElement>();
            reactionElement.minHeight = 24f;
            reactionElement.preferredHeight = 24f;
            reactionElement.preferredWidth = 88f;
            Text reactionText = CreateText("ReactionText", reaction.transform, message.reaction, 12, HexColor("#DBDEE1"), TextAnchor.MiddleCenter,
                rt => Stretch(rt, 7f, 1f, 7f, 1f), FontStyle.Bold);
            reactionText.supportRichText = false;
        }

        if (scrollAfter)
            ScrollToBottom();
    }

    private Color ColorForMessage(ChatMessage message, UserData user)
    {
        switch (message.role)
        {
            case "system": return HexColor(COLOR_GOLD);
            case "helper": return HexColor(COLOR_GREEN);
            case "player": return HexColor(COLOR_BLUE);
            case "boss": return HexColor("#F0B232");
            default:
                int index = user != null ? Mathf.Abs(user.colorIndex) % _avatarColors.Length : 0;
                return _avatarColors[index];
        }
    }

    private string GetInitials(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return "?";
        string cleaned = username.Replace("@", "").Replace("#", "").Replace("_", "").Replace("-", "").Trim();
        if (cleaned.Length >= 2) return cleaned.Substring(0, 2).ToUpperInvariant();
        return cleaned.ToUpperInvariant();
    }

    private void ScrollToBottom()
    {
        if (_messagesScroll == null) return;
        Canvas.ForceUpdateCanvases();
        _messagesScroll.verticalNormalizedPosition = 0f;
    }

    private string FormatClock()
    {
        int totalSeconds = 9 * 60 + Mathf.FloorToInt(_shiftElapsed);
        int hours = (totalSeconds / 3600) % 24;
        int minutes = (totalSeconds / 60) % 60;
        return hours.ToString("00") + ":" + minutes.ToString("00");
    }

    private void PlayNewMessageSound()
    {
        // TODO AUDIO: выберите короткий звук уведомления и назначьте его в инспекторе
        // компонента DeadcordRuntime в поле New Message Sound. Пока клип не назначен,
        // игра намеренно работает без звука и не выдаёт ошибок.
        if (newMessageSound == null)
            return;

        // TODO AUDIO: здесь воспроизводится звук каждого нового сообщения.
        // При желании позже добавьте настройки громкости/отдельные звуки для helper/system.
        if (messageAudioSource == null)
        {
            messageAudioSource = GetComponent<AudioSource>();
            if (messageAudioSource == null)
                messageAudioSource = gameObject.AddComponent<AudioSource>();
            messageAudioSource.playOnAwake = false;
            messageAudioSource.spatialBlend = 0f;
        }

        messageAudioSource.PlayOneShot(newMessageSound);
    }

    #endregion

    #region Channels, status and close

    private void SetChannel(bool showModeration)
    {
        _showingModeration = showModeration;
        if (_channelHeaderText != null)
            _channelHeaderText.text = showModeration ? "#  модерация" : "#  общий";
        if (_messageScrollObject != null) _messageScrollObject.SetActive(!showModeration);
        if (_typingRowObject != null) _typingRowObject.SetActive(!showModeration && _activeIncident != null && _activeIncident.helperTyping);
        if (_inputRowObject != null) _inputRowObject.SetActive(!showModeration);
        if (_rulesPanelObject != null) _rulesPanelObject.SetActive(showModeration);

        SetButtonColor(_generalButton, !showModeration ? HexColor(COLOR_CHANNEL_ACTIVE) : HexColor(COLOR_CHANNEL_IDLE));
        SetButtonColor(_moderationButton, showModeration ? HexColor(COLOR_CHANNEL_ACTIVE) : HexColor(COLOR_CHANNEL_IDLE));

        if (!showModeration)
            RenderAllMessages();
        else
            RefreshInstructions();

        RefreshTypingIndicator();
    }

    private void SetButtonColor(Button button, Color color)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = color;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.selectedColor = color;
        button.colors = colors;
    }

    private void RefreshTypingIndicator()
    {
        if (_typingRowObject == null) return;
        bool shouldShow = !_showingModeration && _activeIncident != null && _activeIncident.helperTyping && !_activeIncident.resolved;
        _typingRowObject.SetActive(shouldShow);
        if (_typingText != null)
            _typingText.text = helperUsername + " печатает...";
    }

    private void RefreshStatusUI()
    {
        float rep = CurrentReputation;
        float clampedRep = Mathf.Clamp(rep, 0f, 100f);
        if (_reputationText != null)
            _reputationText.text = Mathf.RoundToInt(rep) + " / 100";
        if (_reputationFill != null)
        {
            RectTransform rect = _reputationFill.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(clampedRep / 100f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _reputationFill.color = clampedRep >= 70f ? HexColor(COLOR_GREEN) : (clampedRep <= 25f ? HexColor(COLOR_RED) : HexColor(COLOR_BLUE));
        }

        if (_headerWaveText != null)
            _headerWaveText.text = "ДЕНЬ 01  •  ВОЛНА " + CurrentWaveNumber + "/" + Mathf.Max(1, _waveRules.waves.Length) + "  •  " + FormatClock();

        if (_shiftStatusText != null)
        {
            if (rep <= 0f)
            {
                _shiftStatusText.text = "СМЕНА ПРОВАЛЕНА";
                _shiftStatusText.color = HexColor(COLOR_RED);
            }
            else if (rep >= 100f)
            {
                _shiftStatusText.text = "ПОВЫШЕНИЕ";
                _shiftStatusText.color = HexColor(COLOR_GREEN);
            }
            else if (_shiftEnded)
            {
                _shiftStatusText.text = "СМЕНА ЗАВЕРШЕНА";
                _shiftStatusText.color = HexColor(COLOR_MUTED);
            }
            else if (_activeIncident != null)
            {
                _shiftStatusText.text = _activeIncident.helperTyping ? "Помощник печатает" : "Есть активное нарушение";
                _shiftStatusText.color = HexColor(COLOR_GOLD);
            }
            else
            {
                _shiftStatusText.text = "Смена активна";
                _shiftStatusText.color = HexColor(COLOR_GREEN);
            }
        }
    }

    private void LockWorldControls()
    {
        if (_controlsLocked) return;
        _controlsLocked = true;
        _cursorWasVisible = Cursor.visible;
        _cursorLockWas = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        _playerMovements = FindObjectsOfType<PlayerMovement>();
        _playerMovementWasEnabled.Clear();
        if (_playerMovements != null)
        {
            for (int i = 0; i < _playerMovements.Length; i++)
            {
                _playerMovementWasEnabled.Add(_playerMovements[i] != null && _playerMovements[i].enabled);
                if (_playerMovements[i] != null)
                    _playerMovements[i].enabled = false;
            }
        }
    }

    private void RestoreWorldControls()
    {
        if (!_controlsLocked) return;
        _controlsLocked = false;
        if (_playerMovements != null)
        {
            for (int i = 0; i < _playerMovements.Length && i < _playerMovementWasEnabled.Count; i++)
            {
                if (_playerMovements[i] != null)
                    _playerMovements[i].enabled = _playerMovementWasEnabled[i];
            }
        }
        Cursor.visible = _cursorWasVisible;
        Cursor.lockState = _cursorLockWas;
    }

    private void CloseDeadcord()
    {
        if (_gameManager == null)
            _gameManager = FindObjectOfType<GameManager>();

        if (_gameManager != null)
            _gameManager.SetMode(GameManager.Mode.Reality);
        else
            gameObject.SetActive(false);
    }

    #endregion
}
