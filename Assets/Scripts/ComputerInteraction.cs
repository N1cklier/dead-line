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
            if (chatPanel.activeSelf) chatPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (canInteract && Keyboard.current.eKey.isPressed)
        {
            if (chatPanel != null)
                chatPanel.SetActive(true);
        }
    }
}
