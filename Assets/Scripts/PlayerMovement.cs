using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;

    public Transform cameraPivot;
    [Min(0.01f)] public float sensitivityX = 0.15f;
    [Min(0.01f)] public float sensitivityY = 0.15f;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public bool invertY = false;

    private Rigidbody _rb;
    private float _pitch;
    private Vector3 _moveInput;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (cameraPivot == null && Camera.main != null)
            cameraPivot = Camera.main.transform;

        if (cameraPivot != null)
        {
            float startX = cameraPivot.localEulerAngles.x;
            if (startX > 180f) startX -= 360f;
            _pitch = Mathf.Clamp(startX, minPitch, maxPitch);
        }
    }

    void Update()
    {
        if (!CanAct())
        {
            _moveInput = Vector3.zero;
            return;
        }

        HandleLook();
        ReadMoveInput();
    }

    void FixedUpdate()
    {
        if (!CanAct())
            return;

        Vector3 worldMove = transform.TransformDirection(_moveInput);
        _rb.MovePosition(_rb.position + worldMove * speed * Time.fixedDeltaTime);
    }

    bool CanAct()
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.IsGameOver) return false;
            if (GameManager.Instance.currentMode != GameManager.Mode.Reality) return false;
        }

        if (TutorialManager.IsDialogueActive)
            return false;

        return true;
    }

    void HandleLook()
    {
        if (Mouse.current == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        transform.Rotate(Vector3.up * (delta.x * sensitivityX), Space.World);

        float pitchDelta = delta.y * sensitivityY;
        if (!invertY) pitchDelta = -pitchDelta;

        _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    void ReadMoveInput()
    {
        if (Keyboard.current == null)
        {
            _moveInput = Vector3.zero;
            return;
        }

        float h = Keyboard.current.aKey.isPressed ? -1f : (Keyboard.current.dKey.isPressed ? 1f : 0f);
        float v = Keyboard.current.wKey.isPressed ? 1f : (Keyboard.current.sKey.isPressed ? -1f : 0f);

        Vector3 move = new Vector3(h, 0f, v);
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        _moveInput = move;
    }
}