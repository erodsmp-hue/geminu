using UnityEngine;
using UnityEngine.InputSystem;

public class FlashlightBatterySystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light flashlightLight;
    [SerializeField] private Transform flashlightModel;
    [SerializeField] private Renderer lensRenderer;

    [Header("Battery")]
    [SerializeField] private float maxBattery = 100f;
    [SerializeField] private float currentBattery = 100f;
    [SerializeField] private float drainPerSecond = 4.5f;
    [SerializeField] private float rechargeAmount = 25f;

    [Header("Beam")]
    [SerializeField] private float fullIntensity = 13f;
    [SerializeField] private float lowBatteryIntensity = 7f;
    [SerializeField] private float emptyIntensity = 0f;
    [SerializeField] private float fullRange = 19f;
    [SerializeField] private float lowBatteryRange = 12f;

    [Header("Flicker")]
    [SerializeField] private float flickerStartPercent = 0.25f;
    [SerializeField] private float criticalPercent = 0.10f;
    [SerializeField] private float flickerIntensityDrop = 0.55f;
    [SerializeField] private Vector2 flickerIntervalRange = new Vector2(0.05f, 0.20f);
    [SerializeField] private Vector2 flickerOffTimeRange = new Vector2(0.02f, 0.08f);

    [Header("State")]
    [SerializeField] private bool startsOn = false;
    [SerializeField] private Key toggleKey = Key.F;

    private bool isOn;
    private bool isFlickering;
    private float flickerTimer;
    private float flickerOffTimer;
    private bool flickerOffPhase;
    private Material lensMaterialInstance;

    public bool IsFlashlightOn => isOn;

    private void Awake()
    {
        if (flashlightLight == null)
            flashlightLight = GetComponent<Light>();

        if (flashlightModel == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Transform pivot = cam.transform.Find("HeldFlashlightPivot");
                if (pivot != null)
                    flashlightModel = pivot.Find("HeldFlashlightModel");
            }
        }

        if (lensRenderer == null && flashlightModel != null)
        {
            Transform lens = flashlightModel.Find("Lens");
            if (lens != null) lensRenderer = lens.GetComponent<Renderer>();
        }

        if (lensRenderer != null)
            lensMaterialInstance = lensRenderer.material;

        currentBattery = Mathf.Clamp(currentBattery, 0f, maxBattery);
        isOn = startsOn && currentBattery > 0f;
        ResetFlickerTimer();
        ApplyVisual(true);
    }

    private void Update()
    {
        HandleInput();
        if (isOn)
        {
            currentBattery = Mathf.Max(0f, currentBattery - drainPerSecond * Time.deltaTime);
            HandleFlicker();
            if (currentBattery <= 0f) isOn = false;
        }
        else
        {
            isFlickering = false;
            flickerOffPhase = false;
        }

        ApplyVisual(false);
    }

    private void HandleInput()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current[toggleKey].wasPressedThisFrame) return;

        if (!isOn && currentBattery > 0f)
        {
            isOn = true;
            ResetFlickerTimer();
        }
        else if (isOn)
        {
            isOn = false;
        }
    }

    private void HandleFlicker()
    {
        float batteryPercent = maxBattery <= 0f ? 0f : currentBattery / maxBattery;
        isFlickering = batteryPercent <= flickerStartPercent;
        if (!isFlickering) return;

        if (flickerOffPhase)
        {
            flickerOffTimer -= Time.deltaTime;
            if (flickerOffTimer <= 0f)
            {
                flickerOffPhase = false;
                ResetFlickerTimer();
            }
            return;
        }

        flickerTimer -= Time.deltaTime;
        if (flickerTimer <= 0f)
        {
            float criticalness = batteryPercent <= criticalPercent ? 1f : Mathf.InverseLerp(flickerStartPercent, criticalPercent, batteryPercent);
            float chance = Mathf.Lerp(0.15f, 0.65f, 1f - criticalness);
            if (Random.value < chance)
            {
                flickerOffPhase = true;
                flickerOffTimer = Random.Range(flickerOffTimeRange.x, flickerOffTimeRange.y);
            }
            ResetFlickerTimer();
        }
    }

    private void ResetFlickerTimer()
    {
        flickerTimer = Random.Range(flickerIntervalRange.x, flickerIntervalRange.y);
    }

    private void ApplyVisual(bool instant)
    {
        if (flashlightLight == null) return;

        float batteryPercent = maxBattery <= 0f ? 0f : currentBattery / maxBattery;
        float targetIntensity = isOn ? Mathf.Lerp(lowBatteryIntensity, fullIntensity, batteryPercent) : emptyIntensity;
        float targetRange = Mathf.Lerp(lowBatteryRange, fullRange, batteryPercent);
        bool enableLight = isOn;

        if (isFlickering)
        {
            targetIntensity *= Random.Range(0.85f, 1f);
            if (flickerOffPhase)
            {
                targetIntensity *= flickerIntensityDrop * 0.15f;
                enableLight = Random.value > 0.35f;
            }
        }

        flashlightLight.enabled = enableLight;
        if (instant)
        {
            flashlightLight.intensity = targetIntensity;
            flashlightLight.range = targetRange;
        }
        else
        {
            flashlightLight.intensity = Mathf.Lerp(flashlightLight.intensity, targetIntensity, Time.deltaTime * 16f);
            flashlightLight.range = Mathf.Lerp(flashlightLight.range, targetRange, Time.deltaTime * 12f);
        }

        if (lensMaterialInstance != null && lensMaterialInstance.HasProperty("_EmissionColor"))
        {
            Color glow = new Color(1f, 0.93f, 0.72f) * Mathf.Lerp(0.15f, 1.35f, batteryPercent);
            if (isFlickering && flickerOffPhase) glow *= 0.2f;
            lensMaterialInstance.SetColor("_EmissionColor", enableLight ? glow : Color.black);
        }
    }

    public void AddBattery(float amount)
    {
        currentBattery = Mathf.Clamp(currentBattery + amount, 0f, maxBattery);
    }

    public void RefillBatteryToFull()
    {
        currentBattery = maxBattery;
    }

    public float GetBatteryPercent()
    {
        return maxBattery <= 0f ? 0f : (currentBattery / maxBattery) * 100f;
    }
}
