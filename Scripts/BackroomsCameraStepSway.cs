using UnityEngine;

public class BackroomsCameraStepSway : MonoBehaviour
{
    [SerializeField] private BackroomsPlayer player;
    [SerializeField] private Transform visualSwayTarget;
    [SerializeField] private string autoFindChildName = "CameraEffectsPivot";

    [Header("Step Detection")]
    [SerializeField] private float walkStepRate = 1.85f;
    [SerializeField] private float sprintStepRate = 2.45f;
    [SerializeField] private float crouchStepRate = 1.2f;

    [Header("Impulse Strength")]
    [SerializeField] private float walkSideImpulse = 0.020f;
    [SerializeField] private float sprintSideImpulse = 0.030f;
    [SerializeField] private float crouchSideImpulse = 0.012f;
    [SerializeField] private float walkRollImpulse = 1.3f;
    [SerializeField] private float sprintRollImpulse = 2.0f;
    [SerializeField] private float crouchRollImpulse = 0.8f;
    [SerializeField] private float walkDropImpulse = 0.007f;
    [SerializeField] private float sprintDropImpulse = 0.011f;
    [SerializeField] private float crouchDropImpulse = 0.004f;

    [Header("Recovery")]
    [SerializeField] private float positionReturnSpeed = 10f;
    [SerializeField] private float rotationReturnSpeed = 10f;

    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;
    private float stepTimer;
    private int stepSide = 1;
    private Vector3 posVelocity;
    private float currentSide;
    private float currentDrop;
    private float currentRoll;

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<BackroomsPlayer>();

        if (visualSwayTarget == null)
        {
            Transform found = transform.Find(autoFindChildName);
            if (found != null) visualSwayTarget = found;
        }

        if (visualSwayTarget == null)
            visualSwayTarget = transform;

        baseLocalPos = visualSwayTarget.localPosition;
        baseLocalRot = visualSwayTarget.localRotation;
    }

    private void LateUpdate()
    {
        if (player == null || visualSwayTarget == null) return;

        float stepRate = walkStepRate;
        float sideImpulse = walkSideImpulse;
        float rollImpulse = walkRollImpulse;
        float dropImpulse = walkDropImpulse;

        if (player.IsSprinting)
        {
            stepRate = sprintStepRate;
            sideImpulse = sprintSideImpulse;
            rollImpulse = sprintRollImpulse;
            dropImpulse = sprintDropImpulse;
        }
        else if (player.IsCrouching)
        {
            stepRate = crouchStepRate;
            sideImpulse = crouchSideImpulse;
            rollImpulse = crouchRollImpulse;
            dropImpulse = crouchDropImpulse;
        }

        bool stepping = player.IsMoving && player.MoveAmount01 > 0.1f;
        if (stepping)
        {
            stepTimer += Time.deltaTime * stepRate * Mathf.Lerp(0.75f, 1.25f, player.MoveAmount01);
            if (stepTimer >= 1f)
            {
                stepTimer -= 1f;
                stepSide *= -1;
                float blend = Mathf.Lerp(0.5f, 1f, player.MoveAmount01);
                currentSide += sideImpulse * stepSide * blend;
                currentRoll += rollImpulse * -stepSide * blend;
                currentDrop += dropImpulse * blend;
            }
        }
        else
        {
            stepTimer = 0f;
        }

        currentSide = Mathf.Lerp(currentSide, 0f, Time.deltaTime * positionReturnSpeed);
        currentDrop = Mathf.Lerp(currentDrop, 0f, Time.deltaTime * positionReturnSpeed * 0.85f);
        currentRoll = Mathf.Lerp(currentRoll, 0f, Time.deltaTime * rotationReturnSpeed);

        Vector3 targetPos = baseLocalPos + new Vector3(currentSide, -currentDrop, 0f);
        visualSwayTarget.localPosition = Vector3.SmoothDamp(visualSwayTarget.localPosition, targetPos, ref posVelocity, 0.045f);

        Quaternion targetRot = baseLocalRot * Quaternion.Euler(0f, 0f, currentRoll);
        visualSwayTarget.localRotation = Quaternion.Slerp(visualSwayTarget.localRotation, targetRot, Time.deltaTime * rotationReturnSpeed);
    }
}
