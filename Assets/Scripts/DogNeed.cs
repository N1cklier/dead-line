using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class DogNeed : MonoBehaviour
{
    [Header("Параметры")]
    public float maxSatiety = 100f;
    public float currentSatiety = 100f;
    public float decreaseRate = 4f;
    public float feedRate = 35f;

    [Header("Пороги")]
    public float warningThreshold = 0.3f;

    [Header("UI Ссылки")]
    public GameObject dogCanvasObject; // UI над собакой (слайдер) — включается только рядом
    public Slider satietySlider;
    public Image sliderFillImage;
    public GameObject warningIcon;    // Значок тревоги на экране/в UI (ВИДЕН ВСЕГДА ПРИ ОПАСНОСТИ)

    [Header("Цвета")]
    public Color normalColor = Color.green;
    public Color warningColor = new Color(1f, 0.5f, 0f);
    public Color criticalColor = Color.red;

    private bool isPlayerInTrigger = false;

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

        // 2. Кормление на E рядом с собакой
        if (isPlayerInTrigger && Keyboard.current != null && Keyboard.current.eKey.isPressed)
        {
            currentSatiety = Mathf.Min(maxSatiety, currentSatiety + feedRate * Time.deltaTime);
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

        // 4. Полоска обновляется только если мы рядом
        if (isPlayerInTrigger)
        {
            UpdateUI();
        }

        // 5. ГЛОБАЛЬНАЯ ТРЕВОГА: видна везде, если сытость меньше 30%
        HandleGlobalWarning();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
            if (dogCanvasObject != null) dogCanvasObject.SetActive(true);
            UpdateUI();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
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
            // Показываем иконку независимо от того, где находится игрок!
            warningIcon.SetActive(isCritical);

            if (isCritical)
            {
                float scale = 1f + Mathf.Sin(Time.time * 10f) * 0.2f;
                warningIcon.transform.localScale = new Vector3(scale, scale, scale);
            }
        }
    }
}