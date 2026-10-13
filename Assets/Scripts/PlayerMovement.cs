using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;

    void Update()
    {
        float h = Keyboard.current.aKey.isPressed ? -1f : (Keyboard.current.dKey.isPressed ? 1f : 0f);
        float v = Keyboard.current.wKey.isPressed ? 1f : (Keyboard.current.sKey.isPressed ? -1f : 0f);

        Vector3 move = new Vector3(h, 0, v);
        transform.Translate(move * speed * Time.deltaTime);
    }
}
