using UnityEngine;
using UnityEngine.InputSystem;

public class FlashlightViewSway : MonoBehaviour
{
    [Header("Base Pose")]
    public bool readBasePoseOnAwake = true;
    public Vector3 baseLocalPosition;
    public Vector3 baseLocalEuler;

    [Header("General")]
    public float smoothness = 10f;
    public float rotationSmoothness = 12f;

    [Header("Idle")]
    public float idleSwayAmount = 0.0025f;
    public float idleSwaySpeed = 1.4f;
    public float idleRollAmount = 0.35f;

    [Header("Walk")]
    public float walkBobAmount = 0.009f;
    public float walkBobSpeed = 7.6f;
    public float walkStrafeOffset = 0.011f;
    public float walkRoll = 1.1f;

    [Header("Sprint")]
    public float sprintBobAmount = 0.016f;
    public float sprintBobSpeed = 10.8f;
    public float sprintStrafeOffset = 0.014f;
    public float sprintRoll = 1.8f;
    public float sprintForwardPush = 0.009f;

    [Header("Crouch")]
    public float crouchBobAmount = 0.0045f;
    public float crouchBobSpeed = 4.6f;
    public float crouchStrafeOffset = 0.007f;
    public float crouchRoll = 0.65f;
    public Vector3 crouchPoseOffset = new Vector3(0.008f, -0.012f, 0.004f);

    [Header("Legacy Compatibility")]
    public float moveSwayAmount = 0.010f;
    public float moveSwaySpeed = 7.5f;
    public float mouseTiltAmount = 1.15f;

    [Header("Look Lag")]
    public float yawLag = 0.014f;
    public float pitchLag = 0.010f;
    public float rollLag = 0.016f;
    public float positionalLookLag = 0.00075f;

    [Header("Acceleration Feel")]
    public float movementResponse = 7.5f;
    public float stopDamping = 8.5f;

    private BackroomsPlayer player;
    private Vector3 currentLocalPos;
    private Vector3 currentEuler;
    private float bobTimer;
    private Vector2 smoothedMouse;
    private Vector2 smoothedStrafe;

    private void Awake()
    {
        if (readBasePoseOnAwake)
        {
            baseLocalPosition = transform.localPosition;
            baseLocalEuler = transform.localEulerAngles;
        }

        currentLocalPos = baseLocalPosition;
        currentEuler = baseLocalEuler;
        player = FindFirstObjectByType<BackroomsPlayer>();
    }

    private void LateUpdate()
    {
        if (player != null && player.InventoryPaused)
            return;

        Vector2 mouse = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        mouse = Vector2.ClampMagnitude(mouse, 80f);
        smoothedMouse = Vector2.Lerp(smoothedMouse, mouse, Time.deltaTime * 14f);

        bool w = Keyboard.current != null && Keyboard.current.wKey.isPressed;
        bool s = Keyboard.current != null && Keyboard.current.sKey.isPressed;
        bool a = Keyboard.current != null && Keyboard.current.aKey.isPressed;
        bool d = Keyboard.current != null && Keyboard.current.dKey.isPressed;

        Vector2 moveAxes = Vector2.zero;
        moveAxes.x = (d ? 1f : 0f) - (a ? 1f : 0f);
        moveAxes.y = (w ? 1f : 0f) - (s ? 1f : 0f);
        moveAxes = Vector2.ClampMagnitude(moveAxes, 1f);

        bool isMoving = player != null ? player.IsMoving : moveAxes.sqrMagnitude > 0.01f;
        bool isSprinting = player != null && player.IsSprinting;
        bool isCrouching = player != null && player.IsCrouching;
        float move01 = player != null ? player.MoveAmount01 : moveAxes.magnitude;

        float bobAmount = walkBobAmount;
        float bobSpeed = walkBobSpeed;
        float strafeAmount = walkStrafeOffset;
        float rollAmount = walkRoll;
        Vector3 stateOffset = Vector3.zero;

        if (isSprinting)
        {
            bobAmount = sprintBobAmount;
            bobSpeed = sprintBobSpeed;
            strafeAmount = sprintStrafeOffset;
            rollAmount = sprintRoll;
            stateOffset += new Vector3(0f, 0f, sprintForwardPush);
        }
        else if (isCrouching)
        {
            bobAmount = crouchBobAmount;
            bobSpeed = crouchBobSpeed;
            strafeAmount = crouchStrafeOffset;
            rollAmount = crouchRoll;
            stateOffset += crouchPoseOffset;
        }

        if (isMoving)
            bobTimer += Time.deltaTime * bobSpeed * Mathf.Lerp(0.7f, 1.15f, move01);
        else
            bobTimer += Time.deltaTime * idleSwaySpeed;

        float idleX = Mathf.Sin(Time.time * idleSwaySpeed) * idleSwayAmount;
        float idleY = Mathf.Cos(Time.time * idleSwaySpeed * 1.27f) * idleSwayAmount;
        float idleRoll = Mathf.Sin(Time.time * idleSwaySpeed * 0.85f) * idleRollAmount;

        float bobX = 0f;
        float bobY = 0f;
        float bobRoll = 0f;

        if (isMoving)
        {
            bobX = Mathf.Cos(bobTimer * 0.5f) * bobAmount * 0.75f;
            bobY = Mathf.Sin(bobTimer) * bobAmount;
            bobRoll = Mathf.Sin(bobTimer) * rollAmount * Mathf.Lerp(0.5f, 1f, move01);
        }

        smoothedStrafe = Vector2.Lerp(smoothedStrafe, moveAxes, Time.deltaTime * (isMoving ? movementResponse : stopDamping));

        Vector3 strafeOffset = new Vector3(smoothedStrafe.x * strafeAmount, 0f, 0f);
        Vector3 lookOffset = new Vector3(
            -smoothedMouse.x * positionalLookLag,
            -smoothedMouse.y * positionalLookLag * 0.75f,
            0f
        );

        Vector3 targetPos =
            baseLocalPosition +
            stateOffset +
            new Vector3(idleX + bobX, idleY + bobY, 0f) +
            strafeOffset +
            lookOffset;

        float pitch = (-smoothedMouse.y * pitchLag * mouseTiltAmount) + (isMoving ? -bobY * 18f : 0f);
        float yaw = (-smoothedMouse.x * yawLag * mouseTiltAmount) + (-smoothedStrafe.x * 1.4f);
        float roll = idleRoll + bobRoll + (smoothedMouse.x * rollLag * mouseTiltAmount);

        Vector3 targetEuler = baseLocalEuler + new Vector3(pitch, yaw, roll);

        currentLocalPos = Vector3.Lerp(currentLocalPos, targetPos, Time.deltaTime * smoothness);
        currentEuler = Vector3.Lerp(currentEuler, targetEuler, Time.deltaTime * rotationSmoothness);

        transform.localPosition = currentLocalPos;
        transform.localRotation = Quaternion.Euler(currentEuler);
    }
}
