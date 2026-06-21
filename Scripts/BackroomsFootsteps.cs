using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackroomsFootsteps : MonoBehaviour
{
    [SerializeField] private BackroomsPlayer player;
    [SerializeField] private CharacterController controller;
    [SerializeField] private AudioClip[] carpetSteps;
    [SerializeField] private AudioClip[] landingClips;
    [SerializeField] private float walkStepInterval = 0.52f;
    [SerializeField] private float sprintStepInterval = 0.36f;
    [SerializeField] private float crouchStepInterval = 0.72f;
    [SerializeField] private float walkVolume = 0.16f;
    [SerializeField] private float sprintVolume = 0.24f;
    [SerializeField] private float crouchVolume = 0.10f;
    [SerializeField] private float landingVolume = 0.20f;

    private AudioSource source;
    private float stepTimer;
    private bool wasGrounded;

    private void Awake()
    {
        if (player == null) player = GetComponent<BackroomsPlayer>();
        if (controller == null) controller = GetComponent<CharacterController>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    private void Update()
    {
        if (player == null || controller == null) return;

        float interval = player.IsCrouching ? crouchStepInterval : (player.IsSprinting ? sprintStepInterval : walkStepInterval);
        float volume = player.IsCrouching ? crouchVolume : (player.IsSprinting ? sprintVolume : walkVolume);

        if (player.IsMoving && controller.isGrounded)
        {
            stepTimer -= Time.deltaTime * Mathf.Lerp(0.75f, 1.25f, player.MoveAmount01);
            if (stepTimer <= 0f)
            {
                PlayRandom(carpetSteps, volume);
                stepTimer = interval;
            }
        }
        else
        {
            stepTimer = Mathf.Min(stepTimer, interval * 0.5f);
        }

        if (!wasGrounded && controller.isGrounded && player.LandingImpact01 > 0.02f)
            PlayRandom(landingClips.Length > 0 ? landingClips : carpetSteps, landingVolume * Mathf.Lerp(0.65f, 1.2f, player.LandingImpact01));

        wasGrounded = controller.isGrounded;
    }

    private void PlayRandom(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) source.PlayOneShot(clip, volume);
    }
}
