using UnityEngine;

public class BackroomsPlayerVitals : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float maxStamina = 5f;
    [SerializeField] private float currentStamina = 5f;
    [SerializeField] private float drainPerSecond = 1f;
    [SerializeField] private float recoverPerSecond = 0.85f;
    [SerializeField] private float exhaustedLockSeconds = 5f;

    [Header("Runtime")]
    [SerializeField] private bool sprintRequested;
    [SerializeField] private bool sprintActive;

    private BackroomsPlayer player;
    private float exhaustedTimer;

    public float Stamina01 => maxStamina <= 0f ? 0f : currentStamina / maxStamina;
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public bool IsExhausted => exhaustedTimer > 0f;
    public bool CanSprint => exhaustedTimer <= 0f && currentStamina > 0.01f;
    public bool IsSprintingNow => sprintActive;

    private void Awake()
    {
        player = GetComponent<BackroomsPlayer>();
    }

    private void Update()
    {
        if (exhaustedTimer > 0f)
            exhaustedTimer -= Time.deltaTime;

        sprintRequested = player != null && player.IsMoving && player.IsSprinting;
        sprintActive = sprintRequested && CanSprint;

        if (sprintActive)
        {
            currentStamina = Mathf.Max(0f, currentStamina - drainPerSecond * Time.deltaTime);
            if (currentStamina <= 0.001f)
            {
                currentStamina = 0f;
                exhaustedTimer = exhaustedLockSeconds;
                sprintActive = false;
            }
        }
        else if (exhaustedTimer <= 0f)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + recoverPerSecond * Time.deltaTime);
        }
    }
}
