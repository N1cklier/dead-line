using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TutorialDialogueUI : MonoBehaviour
{
    public bool IsShowing { get; private set; }

    private GameObject _canvasObject;
    private Text _usernameText, _bodyText, _avatarText, _continueHintText;

    private readonly Queue<string> _queue = new Queue<string>();
    private Action _onComplete;
    private Font _font;
    private Sprite _uiSprite;

    private PlayerMovement[] _playerMovements;
    private readonly List<bool> _movementWasEnabled = new List<bool>();
    private bool _controlsLocked, _cursorWasVisible;
    private CursorLockMode _cursorModeWas;
    private bool _skipThisFrame;

    private static readonly Color COLOR_PANEL = new Color32(49, 51, 56, 255);
    private static readonly Color COLOR_AVATAR = new Color32(88, 101, 242, 255);
    private static readonly Color COLOR_TEXT = new Color32(219, 222, 225, 255);
    private static readonly Color COLOR_MUTED = new Color32(148, 155, 164, 255);
    private static readonly Color COLOR_NAME = new Color32(240, 178, 50, 255);
    private static readonly Color COLOR_OVERLAY = new Color(0f, 0f, 0f, 0.75f);

    void Awake() { BuildUI(); HideInternal(); }

    void BuildUI()
    {
        _font = Resources.Load<Font>("Fonts/THBiolinum");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white); tex.Apply();
        _uiSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));

        _canvasObject = new GameObject("TutorialDialogueCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvasObject.transform.SetParent(transform, false);
        var canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = _canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var overlay = CreatePanel("Overlay", _canvasObject.transform, COLOR_OVERLAY);
        Stretch(overlay.GetComponent<RectTransform>(), 0, 0, 0, 0);

        var panel = CreatePanel("DialoguePanel", overlay.transform, COLOR_PANEL);
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.sizeDelta = new Vector2(1100, 300);
        pr.anchoredPosition = new Vector2(0, 80);

        var avatar = CreatePanel("Avatar", panel.transform, COLOR_AVATAR);
        var ar = avatar.GetComponent<RectTransform>();
        ar.anchorMin = ar.anchorMax = new Vector2(0f, 1f);
        ar.pivot = new Vector2(0f, 1f);
        ar.sizeDelta = new Vector2(90, 90);
        ar.anchoredPosition = new Vector2(25, -25);

        var init = new GameObject("Initials", typeof(RectTransform), typeof(Text));
        init.transform.SetParent(avatar.transform, false);
        Stretch(init.GetComponent<RectTransform>(), 0, 0, 0, 0);
        _avatarText = init.GetComponent<Text>();
        _avatarText.font = _font; _avatarText.fontSize = 36;
        _avatarText.fontStyle = FontStyle.Bold; _avatarText.color = Color.white;
        _avatarText.alignment = TextAnchor.MiddleCenter; _avatarText.text = "Я";
        _avatarText.raycastTarget = false;

        var user = new GameObject("Username", typeof(RectTransform), typeof(Text));
        user.transform.SetParent(panel.transform, false);
        var ur = user.GetComponent<RectTransform>();
        ur.anchorMin = new Vector2(0, 1); ur.anchorMax = new Vector2(1, 1);
        ur.pivot = new Vector2(0, 1); ur.sizeDelta = new Vector2(-250, 40);
        ur.anchoredPosition = new Vector2(135, -25);
        _usernameText = user.GetComponent<Text>();
        _usernameText.font = _font; _usernameText.fontSize = 28;
        _usernameText.fontStyle = FontStyle.Bold; _usernameText.color = COLOR_NAME;
        _usernameText.alignment = TextAnchor.MiddleLeft;
        _usernameText.text = "Скуф  •  только что";
        _usernameText.raycastTarget = false;

        var body = new GameObject("Body", typeof(RectTransform), typeof(Text));
        body.transform.SetParent(panel.transform, false);
        var br = body.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(135, 70); br.offsetMax = new Vector2(-25, -75);
        _bodyText = body.GetComponent<Text>();
        _bodyText.font = _font; _bodyText.fontSize = 40; _bodyText.color = COLOR_TEXT;
        _bodyText.alignment = TextAnchor.UpperLeft;
        _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        _bodyText.raycastTarget = false;

        var hint = new GameObject("Hint", typeof(RectTransform), typeof(Text));
        hint.transform.SetParent(panel.transform, false);
        var hr = hint.GetComponent<RectTransform>();
        hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.sizeDelta = new Vector2(800, 40); hr.anchoredPosition = new Vector2(0, 15);
        _continueHintText = hint.GetComponent<Text>();
        _continueHintText.font = _font; _continueHintText.fontSize = 26;
        _continueHintText.fontStyle = FontStyle.Italic;
        _continueHintText.color = COLOR_MUTED;
        _continueHintText.alignment = TextAnchor.MiddleCenter;
        _continueHintText.text = "[Пробел] / [Enter] / [ЛКМ] — продолжить";
        _continueHintText.raycastTarget = false;
    }

    public void ShowLines(IEnumerable<string> lines, Action onComplete)
    {
        _queue.Clear();
        foreach (var l in lines) if (!string.IsNullOrWhiteSpace(l)) _queue.Enqueue(l);
        if (_queue.Count == 0) { onComplete?.Invoke(); return; }

        _onComplete = onComplete;
        IsShowing = true;
        _canvasObject.SetActive(true);
        LockControls();
        _skipThisFrame = true;
        ShowNext();
    }

    void ShowNext()
    {
        if (_queue.Count == 0) { HideInternal(); return; }
        _bodyText.text = _queue.Dequeue();
    }

    void HideInternal()
    {
        bool wasShowing = IsShowing;
        IsShowing = false;
        if (_canvasObject != null) _canvasObject.SetActive(false);
        UnlockControls();
        if (!wasShowing) return;
        var cb = _onComplete; _onComplete = null;
        cb?.Invoke();
    }

    void Update()
    {
        if (!IsShowing) return;
        if (_skipThisFrame) { _skipThisFrame = false; return; }

        bool advance = false;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame) advance = true;
            if (Keyboard.current.enterKey.wasPressedThisFrame) advance = true;
            if (Keyboard.current.numpadEnterKey.wasPressedThisFrame) advance = true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) advance = true;

        if (advance) ShowNext();
    }

    void LockControls()
    {
        if (_controlsLocked) return;
        _controlsLocked = true;
        _cursorWasVisible = Cursor.visible; _cursorModeWas = Cursor.lockState;
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;

        _playerMovements = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        _movementWasEnabled.Clear();
        foreach (var pm in _playerMovements)
        {
            _movementWasEnabled.Add(pm != null && pm.enabled);
            if (pm != null) pm.enabled = false;
        }
    }

    void UnlockControls()
    {
        if (!_controlsLocked) return;
        _controlsLocked = false;
        if (_playerMovements != null)
            for (int i = 0; i < _playerMovements.Length && i < _movementWasEnabled.Count; i++)
                if (_playerMovements[i] != null) _playerMovements[i].enabled = _movementWasEnabled[i];
        Cursor.visible = _cursorWasVisible; Cursor.lockState = _cursorModeWas;
    }

    GameObject CreatePanel(string n, Transform p, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(p, false);
        var img = go.GetComponent<Image>();
        img.sprite = _uiSprite; img.color = c;
        return go;
    }

    void Stretch(RectTransform r, float l, float b, float rr, float t)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
    }
}