using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Temporary keyboard player for testing in the Unity Editor.
// Do not add this to XR Origin. Disable/delete Player_Test when moving to VR.
[RequireComponent(typeof(CharacterController))]
public class EditorPlayerController : MonoBehaviour
{
    [Header("Keyboard movement")]
    [Min(0f)] public float moveSpeed = 3f;
    [Min(0f)] public float turnSpeed = 120f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        float horizontal = 0f;
        float forward = 0f;
        float turn = 0f;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed) horizontal += 1f;
            if (keyboard.wKey.isPressed) forward += 1f;
            if (keyboard.sKey.isPressed) forward -= 1f;
            if (keyboard.qKey.isPressed) turn -= 1f;
            if (keyboard.eKey.isPressed) turn += 1f;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.D)) horizontal += 1f;
        if (Input.GetKey(KeyCode.W)) forward += 1f;
        if (Input.GetKey(KeyCode.S)) forward -= 1f;
        if (Input.GetKey(KeyCode.Q)) turn -= 1f;
        if (Input.GetKey(KeyCode.E)) turn += 1f;
#endif

        transform.Rotate(0f, turn * turnSpeed * Time.deltaTime, 0f);

        Vector3 move = transform.right * horizontal + transform.forward * forward;
        move = Vector3.ClampMagnitude(move, 1f);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        Vector3 displacement = (move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime;
        controller.Move(displacement);
    }
}
