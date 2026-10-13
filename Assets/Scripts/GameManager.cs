using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public enum Mode { Reality, Chat }
    public Mode currentMode = Mode.Reality;
    public GameObject chatPanel;

    [Header("Stats")]
    public float reputation = 50f;
    public float rhytmInterval = 15f; // секунды между событиями
    public float minRhytmInterval = 3f; // быстрее не будет
    public Button toRealityButton;// кнопка, которой выключается чат

    private float timer; // время до следующего события
    private bool isGameOver = false;

    void Start()
    {
        SetMode(Mode.Reality);
        toRealityButton.onClick.AddListener(() => SetMode(Mode.Reality));
        timer = rhytmInterval;
    }

    void Update()
    {
        if (isGameOver) return;

        timer -= Time.deltaTime;
        if (timer < 0)
        {
            OnRhythmEvent();
            timer = rhytmInterval;
        }

        if (reputation > 75)
        {
            rhytmInterval -= Time.deltaTime * 0.03f;
            rhytmInterval = Mathf.Max(minRhytmInterval, rhytmInterval);
        }

        HandlePanicVisuals();
    }

    // Убрать окно дискорда или оставить
    public void SetMode(Mode mode)
    {
        currentMode = mode;
        chatPanel.SetActive(mode == Mode.Chat);
    }

    // Игрок допустил ошибку
    public void OnError()
    {
        reputation -= 15f;
        Debug.Log($"Репутация: {reputation}");
        CheckChanges();
    }

    // Игрок сделал всё правильно
    public void OnSuccess()
    {
        reputation += 15f;
        Debug.Log($"Репутация: {reputation}");
        CheckChanges();
    }

    void OnRhythmEvent()
    {
        // Здесь могут быть факты о новых сообщениях в чате или события в жизни?
        Debug.Log("[РИТМ] Пришло новое событие");
    }

    // Проверка на увольнение
    void CheckChanges()
    {
        if (reputation <= 0 && !isGameOver)
        {
            isGameOver = true;
            Debug.Log("Смена провалена: уволен!");
        }
        else if (reputation >= 100 && !isGameOver)
        {
            isGameOver = true;
            Debug.Log("Смена завершена: повышение!");
        }
    }

    void HandlePanicVisuals()
    {
        // Потом можно добавить эффекты
    }
}