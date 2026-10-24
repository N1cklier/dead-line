using UnityEngine;

public class FridgeInteraction : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHunger hunger = other.GetComponent<PlayerHunger>();
            if (hunger != null) hunger.SetNearFridge(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHunger hunger = other.GetComponent<PlayerHunger>();
            if (hunger != null) hunger.SetNearFridge(false);
        }
    }
}