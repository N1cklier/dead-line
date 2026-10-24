using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class DogNeed : MonoBehaviour
{
    [Header("Параметры")]
    public float maxSatiety = 100f;
    public float currentSatiety = 100f;
    public float decreaseRate = 4f;

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

    void Start()
    {
        currentSatiety = maxSatiety;
        if (dogCanvasObject != null) dogCanvasObject.SetActive(false);
        if (warningIcon != null) warningIcon.SetActive(false);
        UpdateUI();
    }

    void Update()
    {
        // 1. Собака постоянно голодает
        currentSatiety -= decreaseRate * Time.deltaTime;
        currentSatiety = Mathf.Clamp(currentSatiety, 0f, maxSatiety);

        // 2. Кормление собаки: если игрок рядом, в руке есть мясо и нажата E
        if (isPlayerInTrigger && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (playerHunger != null && playerHunger.hasMeatInHand)
            {
                currentSatiety = maxSatiety; // Полностью восстанавливаем сытость пса
                playerHunger.hasMeatInHand = false; // Тратим мясо из руки
            }
        }

        // 3. Геймовер: собака съела игрока
        if (currentSatiety <= 0f)
        {
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
        bool isCritical = ratio <= warningThreshold;

        if (warningIcon != null)
        {
            warningIcon.SetActive(isCritical);

            if (isCritical)
            {
                float scale = 1f + Mathf.Sin(Time.time * 10f) * 0.2f;
                warningIcon.transform.localScale = new Vector3(scale, scale, scale);
            }
        }
    }
}