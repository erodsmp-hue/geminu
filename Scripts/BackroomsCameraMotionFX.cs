using UnityEngine;

public class BackroomsCameraMotionFX : MonoBehaviour
{
    [SerializeField] private BackroomsPlayer player;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform cameraHolder;

    [Header("Breathing / Idle")]
    [SerializeField] private float idleVerticalAmount = 0.004f;
    [SerializeField] private float idleHorizontalAmount = 0.002f;
    [SerializeField] private float idleSpeed = 1.35f;

    [Header("Walk")]
    [SerializeField] private float walkPosAmount = 0.010f;
    [SerializeField] private float walkSpeed = 7.2f;

    [Header("Sprint")]
    [SerializeField] private float sprintPosAmount = 0.016f;
    [SerializeField] private float sprintSpeed = 9.2f;
    [SerializeField] private float sprintFov = 67f;

    [Header("Crouch")]
    [SerializeField] private float crouchPosAmount = 0.006f;
    [SerializeField] private float crouchSpeed = 4.0f;
    [SerializeField] private float crouchFov = 58f;

    [Header("General")]
    [SerializeField] private float defaultFov = 60f;
    [SerializeField] private float fovSmooth = 6f;
    [SerializeField] private float positionSmooth = 10f;
    [SerializeField] private float landingDipAmount = 0.022f;

    private Vector3 baseLocalPos;
    private float timer;
    private float landingDip;

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<BackroomsPlayer>();
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (cameraHolder == null) cameraHolder = transform;
        baseLocalPos = cameraHolder.localPosition;
        if (targetCamera != null) defaultFov = targetCamera.fieldOfView;
    }

    private void LateUpdate()
    {
        if (player == null || cameraHolder == null) return;

        float posAmount = walkPosAmount;
        float bobSpeed = walkSpeed;
        float targetFov = defaultFov;

        if (player.IsSprinting)
        {
            posAmount = sprintPosAmount;
            bobSpeed = sprintSpeed;
            targetFov = sprintFov;
        }
        else if (player.IsCrouching)
        {
            posAmount = crouchPosAmount;
            bobSpeed = crouchSpeed;
            targetFov = crouchFov;
        }

        if (player.IsMoving)
            timer += Time.deltaTime * bobSpeed * Mathf.Lerp(0.7f, 1.15f, player.MoveAmount01);
        else
            timer += Time.deltaTime * idleSpeed;

        float idleX = Mathf.Sin(Time.time * idleSpeed) * idleHorizontalAmount;
        float idleY = Mathf.Cos(Time.time * idleSpeed * 1.15f) * idleVerticalAmount;

        float moveX = 0f;
        float moveY = 0f;
        if (player.IsMoving)
        {
            moveX = Mathf.Cos(timer * 0.5f) * posAmount * 0.4f;
            moveY = Mathf.Sin(timer) * posAmount;
        }

        landingDip = Mathf.Lerp(landingDip, player.LandingImpact01 * landingDipAmount, Time.deltaTime * 12f);
        Vector3 targetPos = baseLocalPos + new Vector3(idleX + moveX, idleY + moveY - landingDip, 0f);
        cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, targetPos, Time.deltaTime * positionSmooth);

        if (targetCamera != null)
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Time.deltaTime * fovSmooth);
    }
}
