using UnityEngine;
using UnityEngine.InputSystem;

public class BackroomsPlayer : MonoBehaviour
{
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private CharacterController controller;
    [SerializeField] private float walkSpeed = 3.6f;
    [SerializeField] private float sprintSpeed = 6.0f;
    [SerializeField] private float crouchSpeed = 1.8f;
    [SerializeField] private float jumpHeight = 1.1f;
    [SerializeField] private float gravity = -24f;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private float standingCameraHeight = 1.6f;
    [SerializeField] private float crouchingCameraHeight = 1.0f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 14f;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float pitch;
    private bool wantsToCrouch;
    private float currentCameraHeight;
    private float landingImpact;
    private bool wasGrounded;

    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool InventoryPaused { get; private set; }
    public float MoveAmount01 { get; private set; }
    public float LookDeltaX { get; private set; }
    public float LookDeltaY { get; private set; }
    public float LandingImpact01 => landingImpact;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        currentCameraHeight = standingCameraHeight;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetInventoryPaused(bool paused)
    {
        InventoryPaused = paused;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        planarVelocity = Vector3.zero;
        IsMoving = false;
        IsSprinting = false;
        MoveAmount01 = 0f;
        LookDeltaX = 0f;
        LookDeltaY = 0f;
    }

    private void Update()
    {
        if (InventoryPaused)
            return;

        ReadInput();
        Look();
        Move();
        UpdateCameraHeight();
        landingImpact = Mathf.MoveTowards(landingImpact, 0f, Time.deltaTime * 3.5f);

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void ReadInput()
    {
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
            wantsToCrouch = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;
        }

        if (Mouse.current != null)
            lookInput = Mouse.current.delta.ReadValue();

        LookDeltaX = lookInput.x;
        LookDeltaY = lookInput.y;
    }

    private void Look()
    {
        float yaw = lookInput.x * lookSensitivity;
        float pitchDelta = lookInput.y * lookSensitivity;
        transform.Rotate(Vector3.up * yaw);
        pitch = Mathf.Clamp(pitch - pitchDelta, -85f, 85f);
        if (cameraRoot != null)
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        if (controller == null) return;

        IsCrouching = wantsToCrouch;
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        input = Vector3.ClampMagnitude(input, 1f);
        Vector3 world = transform.TransformDirection(input);

        IsSprinting = !IsCrouching && input.z > 0.1f && Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        float targetSpeed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
        Vector3 targetPlanar = world * targetSpeed;
        float lerpRate = targetPlanar.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;
        planarVelocity = Vector3.Lerp(planarVelocity, targetPlanar, Time.deltaTime * lerpRate);

        IsMoving = new Vector2(planarVelocity.x, planarVelocity.z).magnitude > 0.08f;
        MoveAmount01 = Mathf.Clamp01(new Vector2(planarVelocity.x, planarVelocity.z).magnitude / sprintSpeed);

        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (grounded && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && !IsCrouching)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = planarVelocity;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        if (!wasGrounded && controller.isGrounded && verticalVelocity < -6f)
            landingImpact = Mathf.InverseLerp(-6f, -18f, verticalVelocity * 1.15f);

        wasGrounded = controller.isGrounded;
    }

    private void UpdateCameraHeight()
    {
        if (cameraRoot == null) return;
        float target = IsCrouching ? crouchingCameraHeight : standingCameraHeight;
        currentCameraHeight = Mathf.Lerp(currentCameraHeight, target, Time.deltaTime * crouchTransitionSpeed);
        Vector3 p = cameraRoot.localPosition;
        p.y = currentCameraHeight;
        cameraRoot.localPosition = p;
    }
}
