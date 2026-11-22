using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TutorialHint : MonoBehaviour
{
    [Header("Что показывать")]
    public string keyLabel = "E";
    [TextArea] public string actionText = "взаимодействовать";

    [Header("Поведение")]
    public float displayDuration = 4f;
    public bool showOnlyOnce = true;

    [Header("Триггер (если на объекте нет своего)")]
    public bool autoCreateTrigger = true;
    public Vector3 triggerSize = new Vector3(2f, 2f, 2f);
    public Vector3 triggerOffset = Vector3.zero;

    private static TutorialHintUI _ui;
    private bool _shown, _playerInside;

    void Awake()
    {
        if (autoCreateTrigger && GetComponent<Collider>() == null)
        {
            var c = gameObject.AddComponent<BoxCollider>();
            c.isTrigger = true;
            c.size = triggerSize;
            c.center = triggerOffset;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = true;
        if (showOnlyOnce && _shown) return;
        if (TutorialManager.Instance != null && TutorialManager.IsDialogueActive)
        {
            StartCoroutine(ShowWhenDialogueClosed());
            return;
        }
        _shown = true;
        ShowHint();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) _playerInside = false;
    }

    IEnumerator ShowWhenDialogueClosed()
    {
        while (TutorialManager.Instance != null && TutorialManager.IsDialogueActive)
            yield return null;
        if (!_playerInside) yield break;
        _shown = true;
        ShowHint();
    }

    void ShowHint()
    {
        if (_ui == null) _ui = TutorialHintUI.GetOrCreate();
        _ui.Show(keyLabel, actionText, displayDuration);
    }
}

public class TutorialHintUI : MonoBehaviour
{
    private static TutorialHintUI _instance;
    public static TutorialHintUI GetOrCreate()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("TutorialHintUI");
        _instance = go.AddComponent<TutorialHintUI>();
        return _instance;
    }

    private Transform _container;
    private Font _font;
    private Sprite _uiSprite;

    static readonly Color COLOR_PANEL = new Color32(49, 51, 56, 240);
    static readonly Color COLOR_KEY_BG = new Color32(45, 47, 52, 255);
    static readonly Color COLOR_BORDER = new Color32(88, 101, 242, 255);
    static readonly Color COLOR_TEXT = new Color32(219, 222, 225, 255);

    void Awake()
    {
        _instance = this;
        BuildCanvas();
    }

    void BuildCanvas()
    {
        _font = Resources.Load<Font>("Fonts/THBiolinum");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _uiSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));

        var canvasObj = new GameObject("HintCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var containerObj = new GameObject("HintsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        containerObj.transform.SetParent(canvasObj.transform, false);
        var cr = containerObj.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
        cr.pivot = new Vector2(0.5f, 0f);
        cr.anchoredPosition = new Vector2(0, 180);

        var layout = containerObj.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        var fitter = containerObj.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _container = containerObj.transform;
    }

    public void Show(string key, string action, float duration)
    {
        GameObject panel = CreateHintPanel(key, action);
        StartCoroutine(DestroyAfter(panel, duration));
    }

    private GameObject CreateHintPanel(string key, string action)
    {
        var panel = new GameObject("HintPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_container, false);
        var pr = panel.GetComponent<RectTransform>();
        pr.sizeDelta = new Vector2(840, 144);
        var pimg = panel.GetComponent<Image>();
        pimg.sprite = _uiSprite;
        pimg.color = COLOR_PANEL;

        var keyBox = new GameObject("KeyBox", typeof(RectTransform), typeof(Image), typeof(Outline));
        keyBox.transform.SetParent(panel.transform, false);
        var kr = keyBox.GetComponent<RectTransform>();
        kr.anchorMin = kr.anchorMax = new Vector2(0f, 0.5f);
        kr.pivot = new Vector2(0f, 0.5f);
        kr.sizeDelta = new Vector2(104, 104);
        kr.anchoredPosition = new Vector2(20, 0);
        var kimg = keyBox.GetComponent<Image>();
        kimg.sprite = _uiSprite;
        kimg.color = COLOR_KEY_BG;
        var outline = keyBox.GetComponent<Outline>();
        outline.effectColor = COLOR_BORDER;
        outline.effectDistance = new Vector2(4, 4);

        var kl = new GameObject("KeyLabel", typeof(RectTransform), typeof(Text));
        kl.transform.SetParent(keyBox.transform, false);
        var klr = kl.GetComponent<RectTransform>();
        klr.anchorMin = Vector2.zero;
        klr.anchorMax = Vector2.one;
        klr.offsetMin = Vector2.zero;
        klr.offsetMax = Vector2.zero;
        var keyText = kl.GetComponent<Text>();
        keyText.font = _font;
        keyText.fontSize = 56;
        keyText.fontStyle = FontStyle.Bold;
        keyText.color = Color.white;
        keyText.alignment = TextAnchor.MiddleCenter;
        keyText.text = key;

        var at = new GameObject("ActionText", typeof(RectTransform), typeof(Text));
        at.transform.SetParent(panel.transform, false);
        var ar = at.GetComponent<RectTransform>();
        ar.anchorMin = Vector2.zero;
        ar.anchorMax = Vector2.one;
        ar.offsetMin = new Vector2(144, 0);
        ar.offsetMax = new Vector2(-20, 0);
        var actionText = at.GetComponent<Text>();
        actionText.font = _font;
        actionText.fontSize = 44;
        actionText.color = COLOR_TEXT;
        actionText.alignment = TextAnchor.MiddleLeft;
        actionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        actionText.text = action;

        return panel;
    }

    IEnumerator DestroyAfter(GameObject panel, float d)
    {
        yield return new WaitForSeconds(d);
        if (panel != null) Destroy(panel);
    }
}