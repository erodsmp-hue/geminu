using UnityEngine;

public class BackroomsCameraProceduralMotion : MonoBehaviour
{
    [SerializeField] private BackroomsPlayer player;
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private Transform motionPivot;

    [Header("Step Timing")]
    [SerializeField] private float walkStepRate = 1.9f;
    [SerializeField] private float sprintStepRate = 2.5f;
    [SerializeField] private float crouchStepRate = 1.2f;

    [Header("Step Position")]
    [SerializeField] private float walkSide = 0.018f;
    [SerializeField] private float sprintSide = 0.026f;
    [SerializeField] private float crouchSide = 0.010f;
    [SerializeField] private float walkDrop = 0.010f;
    [SerializeField] private float sprintDrop = 0.016f;
    [SerializeField] private float crouchDrop = 0.006f;
    [SerializeField] private float walkForward = 0.004f;
    [SerializeField] private float sprintForward = 0.007f;
    [SerializeField] private float crouchForward = 0.002f;

    [Header("Step Rotation")]
    [SerializeField] private float walkRoll = 1.3f;
    [SerializeField] private float sprintRoll = 2.2f;
    [SerializeField] private float crouchRoll = 0.7f;
    [SerializeField] private float walkYaw = 0.35f;
    [SerializeField] private float sprintYaw = 0.55f;
    [SerializeField] private float crouchYaw = 0.2f;

    [Header("Idle")]
    [SerializeField] private float idleBreathX = 0.0015f;
    [SerializeField] private float idleBreathY = 0.0025f;
    [SerializeField] private float idleBreathSpeed = 1.25f;

    [Header("Landing")]
    [SerializeField] private float landingDrop = 0.020f;
    [SerializeField] private float landingRecover = 10f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmooth = 14f;
    [SerializeField] private float rotationSmooth = 14f;

    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;
    private float stepClock;
    private int footSide = 1;
    private float sideOffset;
    private float dropOffset;
    private float forwardOffset;
    private float rollOffset;
    private float yawOffset;
    private float landOffset;
    private Vector3 posVelocity;

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<BackroomsPlayer>();
        if (cameraRoot == null) cameraRoot = transform;
        if (motionPivot == null)
        {
            Transform found = cameraRoot.Find("CameraMotionPivot");
            if (found != null) motionPivot = found;
        }
        if (motionPivot == null) motionPivot = cameraRoot;

        baseLocalPos = motionPivot.localPosition;
        baseLocalRot = motionPivot.localRotation;
    }

    private void LateUpdate()
    {
        if (player == null || motionPivot == null) return;

        bool moving = player.IsMoving && player.MoveAmount01 > 0.08f;
        float moveBlend = moving ? Mathf.Lerp(0.45f, 1f, player.MoveAmount01) : 0f;

        float stepRate = walkStepRate;
        float sideAmount = walkSide;
        float dropAmount = walkDrop;
        float forwardAmount = walkForward;
        float rollAmount = walkRoll;
        float yawAmount = walkYaw;

        if (player.IsSprinting)
        {
            stepRate = sprintStepRate;
            sideAmount = sprintSide;
            dropAmount = sprintDrop;
            forwardAmount = sprintForward;
            rollAmount = sprintRoll;
            yawAmount = sprintYaw;
        }
        else if (player.IsCrouching)
        {
            stepRate = crouchStepRate;
            sideAmount = crouchSide;
            dropAmount = crouchDrop;
            forwardAmount = crouchForward;
            rollAmount = crouchRoll;
            yawAmount = crouchYaw;
        }

        if (moving)
        {
            stepClock += Time.deltaTime * stepRate * Mathf.Lerp(0.8f, 1.2f, player.MoveAmount01);
            if (stepClock >= 1f)
            {
                stepClock -= 1f;
                footSide *= -1;
                sideOffset += sideAmount * footSide * moveBlend;
                dropOffset += dropAmount * moveBlend;
                forwardOffset += forwardAmount * moveBlend;
                rollOffset += rollAmount * -footSide * moveBlend;
                yawOffset += yawAmount * footSide * moveBlend;
            }
        }
        else
        {
            stepClock = 0f;
        }

        sideOffset = Mathf.Lerp(sideOffset, 0f, Time.deltaTime * positionSmooth * 0.9f);
        dropOffset = Mathf.Lerp(dropOffset, 0f, Time.deltaTime * positionSmooth * 0.8f);
        forwardOffset = Mathf.Lerp(forwardOffset, 0f, Time.deltaTime * positionSmooth * 0.7f);
        rollOffset = Mathf.Lerp(rollOffset, 0f, Time.deltaTime * rotationSmooth);
        yawOffset = Mathf.Lerp(yawOffset, 0f, Time.deltaTime * rotationSmooth * 0.9f);
        landOffset = Mathf.Lerp(landOffset, player.LandingImpact01 * landingDrop, Time.deltaTime * landingRecover);

        float idleX = moving ? 0f : Mathf.Sin(Time.time * idleBreathSpeed) * idleBreathX;
        float idleY = moving ? 0f : Mathf.Cos(Time.time * idleBreathSpeed * 1.15f) * idleBreathY;

        Vector3 targetPos = baseLocalPos + new Vector3(idleX + sideOffset, idleY - dropOffset - landOffset, forwardOffset);
        motionPivot.localPosition = Vector3.SmoothDamp(motionPivot.localPosition, targetPos, ref posVelocity, 0.04f);

        Quaternion targetRot = baseLocalRot * Quaternion.Euler(0f, yawOffset, rollOffset);
        motionPivot.localRotation = Quaternion.Slerp(motionPivot.localRotation, targetRot, Time.deltaTime * rotationSmooth);
    }
}
