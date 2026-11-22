using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class DogNeed : MonoBehaviour
{
    public static bool IsCritical { get; private set; }

    [Header("Параметры")]
    public float maxSatiety = 100f;
    public float currentSatiety = 100f;
    public float decreaseRate = 4f;

    [Header("Начальная сытость")]
    [Range(0f, 1f)]
    public float startingSatietyPercent = 0.5f;

    [Header("Пороги")]
    public float warningThreshold = 0.3f;

    [Header("UI Ссылки")]
    public GameObject dogCanvasObject;
    public Slider satietySlider;
    public Image sliderFillImage;
    public GameObject warningIcon;

    [Header("Цвета")]
    public Color normalColor = Color.green;
    public Color warningColor = new Color(1f, 0.5f, 0f);
    public Color criticalColor = Color.red;

    private bool isPlayerInTrigger = false;
    private PlayerHunger playerHunger;
    private Vector3 _warningBaseScale;
    private bool _hasWarningBaseScale = false;

    void Start()
    {
        IsCritical = false;
        currentSatiety = maxSatiety * Mathf.Clamp01(startingSatietyPercent);
        if (dogCanvasObject != null) dogCanvasObject.SetActive(false);

        if (warningIcon != null)
        {
            _warningBaseScale = warningIcon.transform.localScale;
            _hasWarningBaseScale = true;
            warningIcon.SetActive(false);
        }

        UpdateUI();
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

        if (isPlayerInTrigger && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (playerHunger != null && playerHunger.hasMeatInHand)
            {
                currentSatiety = maxSatiety;
                playerHunger.hasMeatInHand = false;
                if (TutorialManager.Instance != null) TutorialManager.Instance.NotifyDogFed();
            }
        }

        if (currentSatiety <= 0f)
        {
            IsCritical = false;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver(GameManager.Instance.dogGameOverText);
            }
            enabled = false;
            return;
        }

        if (isPlayerInTrigger)
        {
            UpdateUI();
        }

        HandleGlobalWarning();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            playerHunger = other.GetComponent<PlayerHunger>();
            if (dogCanvasObject != null) dogCanvasObject.SetActive(true);
            UpdateUI();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            playerHunger = null;
            if (dogCanvasObject != null) dogCanvasObject.SetActive(false);
        }
    }

    void UpdateUI()
    {
        if (satietySlider != null)
        {
            satietySlider.maxValue = maxSatiety;
            satietySlider.value = currentSatiety;
        }

        if (sliderFillImage != null)
        {
            float ratio = currentSatiety / maxSatiety;
            if (ratio > warningThreshold)
                sliderFillImage.color = normalColor;
            else if (ratio > 0.15f)
                sliderFillImage.color = warningColor;
            else
                sliderFillImage.color = criticalColor;
        }
    }

    void HandleGlobalWarning()
    {
        float ratio = currentSatiety / maxSatiety;
        IsCritical = ratio <= warningThreshold;

        if (warningIcon == null) return;

        if (!_hasWarningBaseScale)
        {
            _warningBaseScale = warningIcon.transform.localScale;
            _hasWarningBaseScale = true;
        }

        bool anyCritical = PlayerHunger.IsCritical || IsCritical;
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