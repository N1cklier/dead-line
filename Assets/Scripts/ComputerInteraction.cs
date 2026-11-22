using UnityEngine;
using UnityEngine.InputSystem;

public class ComputerInteraction : MonoBehaviour
{
    public GameObject chatPanel;
    private bool canInteract = false;

    void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) canInteract = true; }
    void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) canInteract = false; }

    void Update()
    {
        if (!canInteract || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;
        if (GameManager.Instance == null || GameManager.Instance.currentMode != GameManager.Mode.Reality)
            return;
        if (TutorialManager.IsDialogueActive) return;

        if (TutorialManager.Instance != null && !TutorialManager.IsTutorialComplete)
        {
            TutorialManager.Instance.ShowComputerBlockedMessage();
            return;
        }
        GameManager.Instance.SetMode(GameManager.Mode.Chat);
    }
}