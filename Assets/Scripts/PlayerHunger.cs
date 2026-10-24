using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PlayerHunger : MonoBehaviour
{
    [Header("Параметры голода игрока")]
    public float maxSatiety = 100f;
    public float currentSatiety = 100f;
    public float decreaseRate = 2.5f; // Скорость, с которой голодает сам персонаж
    public float feedSelfRate = 35f;  // Скорость восстановления при еде у холодильника

    [Header("Инвентарь (1 слот)")]
    public bool hasMeatInHand = false; // Есть ли в руке мясо для собаки

    [Header("UI Ссылки")]
    public Slider playerSatietySlider;
    public Image playerSliderFillImage;
    public GameObject meatIconInHand; // Иконка в UI, показывающая, что в руке мясо

    [Header("Цвета шкалы")]
    public Color normalColor = Color.green;
    public Color warningColor = new Color(1f, 0.5f, 0f);
    public Color criticalColor = Color.red;

    private bool isPlayerNearFridge = false;

    void Start()
    {
        currentSatiety = maxSatiety;
        hasMeatInHand = false;
        UpdateUI();
    }

    void Update()
    {
        // 1. Персонаж постоянно расходует сытость
        currentSatiety -= decreaseRate * Time.deltaTime;
        currentSatiety = Mathf.Clamp(currentSatiety, 0f, maxSatiety);

        // 2. Геймовер, если игрок умер от голода сам
        if (currentSatiety <= 0f)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver("ВЫ УМЕРИ С ГОЛОДУ!\nВы забыли поесть сами, работая за компьютером.");
            }
            enabled = false;
            return;
        }

        // 3. Взаимодействие у холодильника
        if (isPlayerNearFridge && Keyboard.current != null)
        {
            // Удерживание E — едим сами
            if (Keyboard.current.eKey.isPressed)
            {
                currentSatiety = Mathf.Min(maxSatiety, currentSatiety + feedSelfRate * Time.deltaTime);
            }
            // Нажатие R — берем мясо в руку для собаки (если слот свободен)
            else if (Keyboard.current.rKey.wasPressedThisFrame && !hasMeatInHand)
            {
                hasMeatInHand = true;
            }
        }

        UpdateUI();
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
            float ratio = currentSatiety / maxSatiety;
            if (ratio > 0.3f) playerSliderFillImage.color = normalColor;
            else if (ratio > 0.15f) playerSliderFillImage.color = warningColor;
            else playerSliderFillImage.color = criticalColor;
        }

        if (meatIconInHand != null)
        {
            meatIconInHand.SetActive(hasMeatInHand);
        }
    }
}