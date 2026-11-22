using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PlayerHunger : MonoBehaviour
{
    public static bool IsCritical { get; private set; }

    [Header("Параметры голода игрока")]
    public float maxSatiety = 100f;
    public float currentSatiety = 100f;
    [Header("Начальная сытость")]
    [Range(0f, 1f)]
    public float startingSatietyPercent = 0.5f;
    public float decreaseRate = 2.5f;
    public float feedSelfRate = 35f;

    [Header("Предупреждение")]
    [Range(0f, 1f)]
    public float warningThreshold = 0.3f;

    [Header("Инвентарь (1 слот)")]
    public bool hasMeatInHand = false;

    [Header("UI Ссылки")]
    public Slider playerSatietySlider;
    public Image playerSliderFillImage;
    public GameObject meatIconInHand;
    public GameObject satietyIcon;
    public GameObject warningIcon;

    [Header("Цвета шкалы")]
    public Color normalColor = Color.green;
    public Color warningColor = new Color(1f, 0.5f, 0f);
    public Color criticalColor = Color.red;

    private bool isPlayerNearFridge = false;
    private Vector3 _warningBaseScale;
    private bool _hasWarningBaseScale = false;

    void Start()
    {
        IsCritical = false;
        currentSatiety = maxSatiety * Mathf.Clamp01(startingSatietyPercent);
        hasMeatInHand = false;

        if (warningIcon != null)
        {
            _warningBaseScale = warningIcon.transform.localScale;
            _hasWarningBaseScale = true;
            warningIcon.SetActive(false);
        }

        UpdateUI();
        HandleGlobalWarning();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            IsCritical = false;
            return;
        }

        if (GameManager.Instance == null || GameManager.Instance.HasShiftStarted)
        {
            currentSatiety -= decreaseRate * Time.deltaTime;
            currentSatiety = Mathf.Clamp(currentSatiety, 0f, maxSatiety);
        }

        if (currentSatiety <= 0f)
        {
            IsCritical = false;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver("ВЫ УМЕРИ С ГОЛОДУ!\nВы забыли поесть сами, работая за компьютером.");
            }
            enabled = false;
            return;
        }

        if (isPlayerNearFridge && Keyboard.current != null)
        {
            if (Keyboard.current.eKey.isPressed)
            {
                if (TutorialManager.Instance != null) TutorialManager.Instance.NotifyPlayerAte();
                currentSatiety = Mathf.Min(maxSatiety, currentSatiety + feedSelfRate * Time.deltaTime);
            }
            else if (Keyboard.current.rKey.wasPressedThisFrame && !hasMeatInHand)
            {
                hasMeatInHand = true;
            }
        }

        UpdateUI();
        HandleGlobalWarning();
    }

    public void SetNearFridge(bool near)
    {
        isPlayerNearFridge = near;
    }

    void UpdateUI()
    {
        if (playerSatietySlider != null)
        {
            playerSatietySlider.maxValue = maxSatiety;
            playerSatietySlider.value = currentSatiety;
        }

        if (playerSliderFillImage != null)
        {
            float ratio = maxSatiety > 0f ? currentSatiety / maxSatiety : 0f;
            if (ratio > warningThreshold) playerSliderFillImage.color = normalColor;
            else if (ratio > 0.15f) playerSliderFillImage.color = warningColor;
            else playerSliderFillImage.color = criticalColor;
        }

        if (meatIconInHand != null)
        {
            meatIconInHand.SetActive(hasMeatInHand);
        }
    }

    void HandleGlobalWarning()
    {
        float ratio = maxSatiety > 0f ? currentSatiety / maxSatiety : 0f;
        IsCritical = ratio <= warningThreshold;

        if (warningIcon == null) return;

        if (!_hasWarningBaseScale)
        {
            _warningBaseScale = warningIcon.transform.localScale;
            _hasWarningBaseScale = true;
        }

        bool anyCritical = IsCritical || DogNeed.IsCritical;
        warningIcon.SetActive(anyCritical);

        if (anyCritical)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.1f;
            warningIcon.transform.localScale = _warningBaseScale * pulse;
        }
        else
        {
            warningIcon.transform.localScale = _warningBaseScale;
        }
    }
}