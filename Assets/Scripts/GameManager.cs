using UnityEngine;
using UnityEngine.UI; // Обязательно для работы с GraphicRaycaster
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum Mode { Reality, Chat }
    public Mode currentMode = Mode.Reality;
    public GameObject chatPanel; // Сюда закинь ChatPanel

    [Header("Stats")]
    public float reputation = 50f;

    [Header("UI Game Over (TextMeshPro)")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverReasonText;

    [Header("Настройки текстов смерти")]
    [TextArea(2, 5)]
    public string dogGameOverText = "ВАС СЪЕЛА СОБАКА!\nВы слишком долго игнорировали потребности питомца.";
    [TextArea(2, 5)]
    public string reputationGameOverText = "ВАС УВОЛИЛИ!\nРепутация модератора упала до нуля.";
    [TextArea(2, 5)]
    public string winText = "СМЕНА ЗАВЕРШЕНА!\nВы успешно отработали день и получили повышение!";

    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (gameOverReasonText == null && gameOverPanel != null)
        {
            gameOverReasonText = gameOverPanel.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        SetMode(Mode.Reality);
    }

    public void SetMode(Mode mode)
    {
        if (isGameOver) return;
        currentMode = mode;

        if (chatPanel != null)
        {
            // Панель в иерархии ВСЕГДА активна, таймеры и чат идут в фоне!
            chatPanel.SetActive(true);

            // Ищем корневой элемент интерфейса Deadcord, который мы создали в коде
            Transform runtimeRoot = chatPanel.transform.Find("Deadcord_RuntimeUI");
            if (runtimeRoot != null)
            {
                CanvasGroup cg = runtimeRoot.GetComponent<CanvasGroup>();
                if (cg == null) cg = runtimeRoot.gameObject.AddComponent<CanvasGroup>();

                if (mode == Mode.Chat)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
                else
                {
                    cg.alpha = 0f;             // Скрываем с глаз, но чат продолжает жить внутри!
                    cg.interactable = false;
                    cg.blocksRaycasts = false; // Мышь летит в игру
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }
        }
    }

    public void OnError()
    {
        if (isGameOver) return;
        reputation = Mathf.Clamp(reputation - 15f, 0f, 100f);
        CheckChanges();
    }

    public void OnSuccess()
    {
        if (isGameOver) return;
        reputation = Mathf.Clamp(reputation + 15f, 0f, 100f);
        CheckChanges();
    }

    void CheckChanges()
    {
        if (reputation <= 0 && !isGameOver) GameOver(reputationGameOverText);
        else if (reputation >= 100 && !isGameOver) GameOver(winText);
    }

    public void GameOver(string reason)
    {
        isGameOver = true;
        if (chatPanel != null) chatPanel.SetActive(false);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverReasonText != null) gameOverReasonText.text = reason;
        }
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}