using UnityEngine;

public class BackroomsHeadBob : MonoBehaviour
{
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Transform visualBobTarget;
    [SerializeField] private float walkBobSpeed = 7.0f;
    [SerializeField] private float sprintBobSpeed = 9.2f;
    [SerializeField] private float crouchBobSpeed = 4.0f;
    [SerializeField] private float walkBobAmount = 0.014f;
    [SerializeField] private float sprintBobAmount = 0.022f;
    [SerializeField] private float crouchBobAmount = 0.008f;
    [SerializeField] private float horizontalBobMultiplier = 0.4f;
    [SerializeField] private float landingDipAmount = 0.030f;
    [SerializeField] private float smoothness = 10f;
    [SerializeField] private bool affectMainCameraPosition = false;

    private BackroomsPlayer player;
    private Vector3 startLocalPos;
    private Vector3 startBobTargetLocalPos;
    private float timer;
    private float currentLandingDip;

    private void Awake()
    {
        player = GetComponent<BackroomsPlayer>();

        if (cameraHolder != null)
            startLocalPos = cameraHolder.localPosition;

        if (visualBobTarget == null && cameraHolder != null)
        {
            Transform pivot = cameraHolder.Find("HeldFlashlightPivot");
            if (pivot != null)
                visualBobTarget = pivot;
        }

        if (visualBobTarget != null)
            startBobTargetLocalPos = visualBobTarget.localPosition;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        float bobSpeed = player.IsCrouching ? crouchBobSpeed : (player.IsSprinting ? sprintBobSpeed : walkBobSpeed);
        float bobAmount = player.IsCrouching ? crouchBobAmount : (player.IsSprinting ? sprintBobAmount : walkBobAmount);

        if (player.IsMoving)
            timer += Time.deltaTime * bobSpeed * Mathf.Lerp(0.7f, 1.15f, player.MoveAmount01);
        else
            timer = Mathf.Lerp(timer, 0f, Time.deltaTime * 5f);

        float bobX = 0f;
        float bobY = 0f;
        if (player.IsMoving)
        {
            bobX = Mathf.Cos(timer * 0.5f) * bobAmount * horizontalBobMultiplier;
            bobY = Mathf.Sin(timer) * bobAmount;
        }

        currentLandingDip = Mathf.Lerp(currentLandingDip, player.LandingImpact01 * landingDipAmount, Time.deltaTime * 12f);
        Vector3 offset = new Vector3(bobX, bobY - currentLandingDip, 0f);

        if (visualBobTarget != null)
        {
            Vector3 target = startBobTargetLocalPos + offset;
            visualBobTarget.localPosition = Vector3.Lerp(visualBobTarget.localPosition, target, Time.deltaTime * smoothness);
        }

        if (cameraHolder != null)
        {
            if (affectMainCameraPosition)
            {
                Vector3 camTarget = startLocalPos + offset * 0.35f;
                cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, camTarget, Time.deltaTime * smoothness);
            }
            else
            {
                cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, startLocalPos, Time.deltaTime * smoothness);
            }
        }
    }
}
