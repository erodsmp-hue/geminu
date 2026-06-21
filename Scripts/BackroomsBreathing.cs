using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackroomsBreathing : MonoBehaviour
{
    [SerializeField] private BackroomsPlayer player;
    [SerializeField] private float baseVolume = 0.0f;
    [SerializeField] private float walkVolume = 0.015f;
    [SerializeField] private float sprintVolume = 0.065f;
    [SerializeField] private float crouchVolume = 0.008f;
    [SerializeField] private float volumeSmooth = 3.5f;

    private AudioSource source;
    private float seed;

    private void Awake()
    {
        if (player == null) player = GetComponent<BackroomsPlayer>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = true;
        source.loop = true;
        source.spatialBlend = 0f;
        seed = Random.value * 10f;
        if (source.clip == null) source.clip = CreateBreathClip();
        if (!source.isPlaying) source.Play();
    }

    private void Update()
    {
        if (player == null) return;

        float target = baseVolume;
        if (player.IsSprinting && player.IsMoving) target = sprintVolume;
        else if (player.IsCrouching && player.IsMoving) target = crouchVolume;
        else if (player.IsMoving) target = walkVolume;

        source.volume = Mathf.Lerp(source.volume, target, Time.deltaTime * volumeSmooth);
        source.pitch = 1f + Mathf.Sin(Time.time * 0.7f + seed) * 0.03f + (player.IsSprinting ? 0.05f : 0f);
    }

    private AudioClip CreateBreathClip()
    {
        int sampleRate = 44100;
        int length = sampleRate * 2;
        float[] data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI));
            float noise = (Mathf.PerlinNoise(i * 0.0025f, 0.37f) - 0.5f) * 2f;
            data[i] = noise * 0.055f * env;
        }
        AudioClip clip = AudioClip.Create("BackroomsBreath", length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
