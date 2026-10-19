using UnityEngine;
using UnityEngine.InputSystem;

public class ComputerInteraction : MonoBehaviour
{
    public GameObject chatPanel;
    private bool canInteract = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) canInteract = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canInteract = false;
        }
    }

    void Update()
    {
        // Открываем чат по E, только если мы в зоне и играем в режиме Reality
        if (canInteract && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (GameManager.Instance != null && GameManager.Instance.currentMode == GameManager.Mode.Reality)
            {
                GameManager.Instance.SetMode(GameManager.Mode.Chat);
            }
        }
    }
}