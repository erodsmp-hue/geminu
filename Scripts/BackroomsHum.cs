using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackroomsHum : MonoBehaviour
{
    [SerializeField] private float baseVolume = 0.08f;
    [SerializeField] private float driftAmount = 0.018f;
    [SerializeField] private float pitchDrift = 0.02f;
    [SerializeField] private float zonePulseAmount = 0.012f;

    private AudioSource source;
    private float basePitch;
    private float seed;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 0f;
        source.volume = baseVolume;
        basePitch = 1f;
        seed = Random.value * 100f;
        if (source.clip == null) source.clip = CreateHumClip();
        source.Play();
    }

    private void Update()
    {
        float slow = Mathf.Sin(Time.time * 0.27f + seed) * pitchDrift;
        float zone = (Mathf.PerlinNoise(Time.time * 0.05f, seed) - 0.5f) * 2f * zonePulseAmount;
        source.pitch = basePitch + slow;
        source.volume = baseVolume + zone + Mathf.Sin(Time.time * 0.12f + seed) * driftAmount;
    }

    private AudioClip CreateHumClip()
    {
        int sampleRate = 44100;
        int length = sampleRate * 3;
        float[] data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float hum = Mathf.Sin(t * Mathf.PI * 2f * 60f) * 0.14f;
            hum += Mathf.Sin(t * Mathf.PI * 2f * 120f) * 0.05f;
            hum += Mathf.Sin(t * Mathf.PI * 2f * 180f) * 0.025f;
            hum += (Mathf.PerlinNoise(i * 0.0015f, 0f) - 0.5f) * 0.018f;
            data[i] = hum;
        }
        AudioClip clip = AudioClip.Create("BackroomsHum", length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
