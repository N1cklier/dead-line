using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class DeadcordRuntime : MonoBehaviour
{
    [Header("Data (JSON files live under Assets/Deadcord/Resources/DeadcordData)")]
    [SerializeField] private string dataFolder = "DeadcordData";

    [Header("NPC timing")]
    [SerializeField, Min(0.5f)] private float npcMessageIntervalMin = 2.1f;
    [SerializeField, Min(1f)] private float npcMessageIntervalMax = 3.5f;
    [SerializeField, Range(1f, 2.5f)] private float npcMessageIntervalMultiplier = 1.2f;
    [SerializeField, Range(0f, 1f)] private float violationChance = 0.42f;
    [SerializeField, Min(17f)] private float helperDeadlineBaseSeconds = 20f;
    [SerializeField, Min(0f)] private float helperDeadlineVarianceSeconds = 5f;
    [SerializeField, Min(0.1f)] private float helperTypingSeconds = 9.5f;
    [SerializeField, Min(20f)] private float maxStoredMessages = 160f;

    [Header("Audio - optional; leave empty until a sound is chosen")]
    [Tooltip("Assign a short notification clip here later. Null is safe and intentionally silent.")]
    public AudioClip newMessageSound;
    [Tooltip("Optional AudioSource. If empty and a clip is assigned, one is created automatically.")]
    public AudioSource messageAudioSource;

    [Header("Font")]
    [Tooltip("Assign a TTF/OTF Font asset with Cyrillic glyphs (for example Noto Sans). Important for WebGL builds.")]
    [SerializeField] private Font interfaceFont;

    [Header("Text sizing")]
    [Tooltip("Global multiplier for every Deadcord label. 1.0 = designed defaults; try 1.15–1.3 if text still looks small.")]
    [SerializeField, Range(0.75f, 2f)] private float fontScale = 1f;

    [Header("Interface")]
    [SerializeField] private bool hideOldPanelChildren = true;
    [SerializeField] private string helperUsername = "DeadcordHelper";

    private Font _font;
    private Sprite _uiSprite;
    private Sprite _avatarSprite;

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
    private bool _shiftAccepted;
    private bool _shiftEnded;
    private bool _showingModeration;
    private Button _acceptRulesButton;

    // Only these two gameplay HUD widgets are hidden in Deadcord. DogNeed.warningIcon is intentionally untouched.
    private PlayerHunger _playerHunger;
    private bool _worldHudStateCaptured;
    private float _nextPlayerHungerSearchAt;
    private bool _playerHungerSliderWasActive;
    private bool _meatIconWasActive;
    private bool _satietyIconWasActive;
    private float _shiftElapsed;
    private float _nextNpcMessageAt;
    private float _nextWaveAt;
    private int _waveIndex;
    private int _nextStoryIndex;
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
    private readonly List<IncidentData> _activeIncidents = new List<IncidentData>();

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

        if (!_shiftStarted)
            BeginShift();

        SetChannel(true);
    }

    private void Update()
    {
        if (!_initialized)
            return;

        bool inChatMode = GameManager.Instance != null && GameManager.Instance.currentMode == GameManager.Mode.Chat;
        if (inChatMode && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseDeadcord();
            return;
        }

        if (_shiftEnded)
            return;

        if (!_shiftAccepted)// без подтверждения смена не начнётся
        {
            if (inChatMode)
                RefreshStatusUI();
            return;
        }

        _shiftElapsed += Time.unscaledDeltaTime;
        if (_shiftElapsed >= GetShiftDurationSeconds())
        {
            CompleteShift();
            return;
        }
        AdvanceWaves();
        PlayStoryEvents();
        SpawnNpcMessageIfDue();
        UpdateIncidentTimer();

        if (inChatMode)
        {
            if (!_shiftEnded && !_showingModeration && _commandInput != null && _commandInput.isFocused && Keyboard.current != null)
            {
                bool enterPressed = Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
                if (enterPressed)
                    SubmitCommandInput();
            }

            RefreshStatusUI();
        }
    }

    private void LateUpdate()
    {
        if (!_initialized)
            return;

        bool deadcordVisible;
        if (GameManager.Instance != null)
        {
            deadcordVisible = GameManager.Instance.currentMode == GameManager.Mode.Chat;
        }
        else
        {
            CanvasGroup group = _runtimeRoot != null ? _runtimeRoot.GetComponent<CanvasGroup>() : null;
            deadcordVisible = group != null ? group.alpha > 0.5f : gameObject.activeInHierarchy;
        }

        SyncGameplayHudVisibility(deadcordVisible);
    }

    private void OnDisable()
    {
        // If the entire computer panel is disabled (for example when leaving its trigger), restore the HUD.
        SyncGameplayHudVisibility(false);
    }

    #endregion

    #region Data

    [Serializable]
    private class NamePartsData
    {
        public string[] first;
        public string[] second;
        public string[] third;
    }

    [Serializable]
    private class NormalMessagesData
    {
        public string[] messages;
    }

    [Serializable]
    private class ViolationsData
    {
        public ViolationDefinition[] incidents;
    }

    [Serializable]
    private class ViolationDefinition
    {
        public string id;
        public string category;
        public int minWave;
        public string requiredAction;
        public string[] messages;
    }

    [Serializable]
    private class WaveRulesData
    {
        public WaveDefinition[] waves;
    }

    [Serializable]
    private class WaveDefinition
    {
        public int wave;
        public string title;
        public float durationSeconds;
        public string announcement;
        public string[] instructions;
        public string[] unlockedCategories;
    }

    [Serializable]
    private class StoryData
    {
        public StoryEvent[] events;
    }

    [Serializable]
    private class StoryEvent
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
    }

    #endregion
}
