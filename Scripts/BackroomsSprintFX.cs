using System.Collections;
using UnityEngine;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class BackroomsSprintFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BackroomsPlayer player;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform cameraEffectsPivot;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
    [SerializeField] private Volume globalVolume;
#endif

    [Header("Sprint Feel")]
    [SerializeField] private float sprintFovBoost = 5.5f;
    [SerializeField] private float fovSmooth = 5.2f;
    [SerializeField] private float sprintTilt = 0.85f;
    [SerializeField] private float lowStaminaBreathRoll = 0.65f;
    [SerializeField] private float exhaustedBreathRoll = 1.15f;
    [SerializeField] private float rotationSmooth = 6.0f;

    [Header("Breathing Rhythm")]
    [SerializeField] private float sprintBreathSpeed = 2.2f;
    [SerializeField] private float lowStaminaBreathSpeed = 3.4f;
    [SerializeField] private float exhaustedBreathSpeed = 4.4f;
    [SerializeField] private float impactBlurSeconds = 1.1f;

    [Header("Stamina Source")]
    [SerializeField] private MonoBehaviour staminaSource;
    [SerializeField] private bool autoFindStaminaSource = true;
    [SerializeField] private float fallbackMaxStamina = 5f;
    [SerializeField] private float fallbackCurrentStamina = 5f;
    [SerializeField] private float fallbackDrainPerSecond = 1f;
    [SerializeField] private float fallbackRecoverPerSecond = 0.85f;
    [SerializeField] private float sprintUnlockThreshold01 = 0.20f;

    private float currentStamina;
    private bool fallbackSprintBlocked;
    private bool impactRoutineRunning;
    private Quaternion baseCameraLocalRotation;
    private float baseFov;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
    private DepthOfField dof;
    private Vignette vignette;
    private ChromaticAberration chromatic;
    private LensDistortion lensDistortion;
    private FilmGrain filmGrain;
    private bool dofWasActive;
    private float vignetteBase;
    private float chromaticBase;
    private float distortionBase;
    private float grainBase;
#endif

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<BackroomsPlayer>();
        if (player == null) player = FindFirstObjectByType<BackroomsPlayer>();
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;

        if (cameraEffectsPivot == null && targetCamera != null)
        {
            Transform found = targetCamera.transform.Find("CameraEffectsPivot");
            cameraEffectsPivot = found != null ? found : targetCamera.transform;
        }

        if (targetCamera != null)
        {
            baseFov = targetCamera.fieldOfView;
            baseCameraLocalRotation = targetCamera.transform.localRotation;
        }

        currentStamina = fallbackCurrentStamina;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (globalVolume == null) globalVolume = FindFirstObjectByType<Volume>();
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out dof);
            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out chromatic);
            globalVolume.profile.TryGet(out lensDistortion);
            globalVolume.profile.TryGet(out filmGrain);
            if (dof != null) dofWasActive = dof.active;
            if (vignette != null) vignetteBase = vignette.intensity.value;
            if (chromatic != null) chromaticBase = chromatic.intensity.value;
            if (lensDistortion != null) distortionBase = lensDistortion.intensity.value;
            if (filmGrain != null) grainBase = filmGrain.intensity.value;
        }
#endif
    }

    private void Update()
    {
        if (player == null) player = FindFirstObjectByType<BackroomsPlayer>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null || player == null) return;
        if (autoFindStaminaSource && staminaSource == null) staminaSource = FindBestStaminaSource();

        bool usingExternalStamina = TryReadExternalStamina(out float stamina01, out bool exhausted, out bool sprintingNow);
        if (!usingExternalStamina)
            SimulateFallbackStamina(out stamina01, out exhausted, out sprintingNow);

        ApplySprintFX(stamina01, exhausted, sprintingNow);
    }

    private void SimulateFallbackStamina(out float stamina01, out bool exhausted, out bool sprintingNow)
    {
        bool wantsSprint = player.IsMoving && player.IsSprinting && !player.IsCrouching;

        if (fallbackSprintBlocked && GetFallbackStamina01() >= sprintUnlockThreshold01)
            fallbackSprintBlocked = false;

        sprintingNow = wantsSprint && !fallbackSprintBlocked;

        if (sprintingNow)
        {
            currentStamina = Mathf.Max(0f, currentStamina - fallbackDrainPerSecond * Time.deltaTime);
            if (currentStamina <= 0.001f)
            {
                currentStamina = 0f;
                fallbackSprintBlocked = true;
                sprintingNow = false;
                if (!impactRoutineRunning) StartCoroutine(ExhaustionImpactRoutine());
            }
        }
        else
        {
            currentStamina = Mathf.Min(fallbackMaxStamina, currentStamina + fallbackRecoverPerSecond * Time.deltaTime);
        }

        stamina01 = GetFallbackStamina01();
        exhausted = fallbackSprintBlocked;
    }

    private float GetFallbackStamina01()
    {
        return fallbackMaxStamina <= 0f ? 0f : Mathf.Clamp01(currentStamina / fallbackMaxStamina);
    }

    private bool TryReadExternalStamina(out float stamina01, out bool exhausted, out bool sprintingNow)
    {
        stamina01 = 1f;
        exhausted = false;
        sprintingNow = player.IsMoving && player.IsSprinting && !player.IsCrouching;
        if (staminaSource == null) return false;

        System.Type type = staminaSource.GetType();
        if (!TryReadFloat(type, staminaSource, "Stamina01", out stamina01))
        {
            float current;
            float max;
            if (TryReadFloat(type, staminaSource, "CurrentStamina", out current) && TryReadFloat(type, staminaSource, "MaxStamina", out max) && max > 0f)
                stamina01 = Mathf.Clamp01(current / max);
            else
                return false;
        }

        bool hasExhausted = TryReadBool(type, staminaSource, "IsExhausted", out exhausted);
        bool hasSprintNow = TryReadBool(type, staminaSource, "IsSprintingNow", out sprintingNow);
        if (!hasExhausted) exhausted = stamina01 <= 0.001f;
        if (!hasSprintNow) sprintingNow = player.IsMoving && player.IsSprinting && !player.IsCrouching && !exhausted;
        return true;
    }

    private void ApplySprintFX(float stamina01, bool exhausted, bool sprintingNow)
    {
        float lowFactor = 1f - stamina01;
        float breathLow = Mathf.Clamp01(Mathf.InverseLerp(0.45f, 0.08f, lowFactor));
        float heavyBreath = Mathf.Clamp01(Mathf.InverseLerp(0.70f, 1f, lowFactor));

        float targetFov = baseFov;
        if (sprintingNow)
            targetFov = baseFov + Mathf.Lerp(3.2f, sprintFovBoost, stamina01);
        else if (exhausted)
            targetFov = baseFov - 1.15f;
        targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Time.deltaTime * fovSmooth);

        float roll = 0f;
        if (sprintingNow)
            roll += sprintTilt + Mathf.Sin(Time.time * 8.3f) * Mathf.Lerp(0.04f, 0.18f, lowFactor);
        if (breathLow > 0f)
            roll += Mathf.Sin(Time.time * Mathf.Lerp(sprintBreathSpeed, lowStaminaBreathSpeed, breathLow)) * (lowStaminaBreathRoll * breathLow);
        if (exhausted)
            roll += Mathf.Sin(Time.time * exhaustedBreathSpeed) * exhaustedBreathRoll;

        Quaternion targetRot = baseCameraLocalRotation * Quaternion.Euler(0f, 0f, roll);
        targetCamera.transform.localRotation = Quaternion.Slerp(targetCamera.transform.localRotation, targetRot, Time.deltaTime * rotationSmooth);

        if (cameraEffectsPivot != null && cameraEffectsPivot != targetCamera.transform)
        {
            Vector3 basePos = Vector3.zero;
            Vector3 targetPos = basePos;
            if (sprintingNow)
                targetPos += new Vector3(0f, Mathf.Sin(Time.time * 8.3f) * Mathf.Lerp(0.002f, 0.006f, lowFactor), 0f);
            if (exhausted)
                targetPos += new Vector3(0f, Mathf.Sin(Time.time * exhaustedBreathSpeed) * 0.010f, -0.004f);
            cameraEffectsPivot.localPosition = Vector3.Lerp(cameraEffectsPivot.localPosition, targetPos, Time.deltaTime * 6f);
        }

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (!impactRoutineRunning)
        {
            if (vignette != null)
                vignette.intensity.Override(vignetteBase + breathLow * 0.12f + (exhausted ? 0.16f : 0f));
            if (chromatic != null)
                chromatic.intensity.Override(chromaticBase + heavyBreath * 0.07f + (exhausted ? 0.08f : 0f));
            if (lensDistortion != null)
                lensDistortion.intensity.Override(distortionBase - heavyBreath * 0.08f - (exhausted ? 0.10f : 0f));
            if (filmGrain != null)
                filmGrain.intensity.Override(grainBase + heavyBreath * 0.12f + (exhausted ? 0.10f : 0f));
            if (dof != null)
                dof.active = exhausted;
        }
#endif
    }

    private IEnumerator ExhaustionImpactRoutine()
    {
        impactRoutineRunning = true;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (dof != null)
        {
            dof.active = true;
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0.01f);
            dof.gaussianEnd.Override(0.18f);
            dof.highQualitySampling.Override(false);
        }
        if (vignette != null) vignette.intensity.Override(vignetteBase + 0.24f);
        if (chromatic != null) chromatic.intensity.Override(chromaticBase + 0.14f);
        if (lensDistortion != null) lensDistortion.intensity.Override(distortionBase - 0.16f);
        if (filmGrain != null) filmGrain.intensity.Override(grainBase + 0.18f);
#endif
        yield return new WaitForSeconds(impactBlurSeconds);
        impactRoutineRunning = false;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (dof != null) dof.active = dofWasActive;
#endif
    }

    private MonoBehaviour FindBestStaminaSource()
    {
        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour mb in all)
        {
            if (mb == null) continue;
            string name = mb.GetType().Name;
            if (name != "BackroomsPlayerVitals" && name != "BackroomsAutoPlayerVitals") continue;
            return mb;
        }
        return null;
    }

    private bool TryReadFloat(System.Type type, object source, string member, out float value)
    {
        value = 0f;
        var prop = type.GetProperty(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(float))
        {
            value = (float)prop.GetValue(source, null);
            return true;
        }
        var field = type.GetField(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(float))
        {
            value = (float)field.GetValue(source);
            return true;
        }
        return false;
    }

    private bool TryReadBool(System.Type type, object source, string member, out bool value)
    {
        value = false;
        var prop = type.GetProperty(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(bool))
        {
            value = (bool)prop.GetValue(source, null);
            return true;
        }
        var field = type.GetField(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(bool))
        {
            value = (bool)field.GetValue(source);
            return true;
        }
        return false;
    }
}
