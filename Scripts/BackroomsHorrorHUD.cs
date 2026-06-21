using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class BackroomsHorrorHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BackroomsPlayerVitals vitals;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private CanvasGroup staminaGroup;
    [SerializeField] private Image staminaFill;
    [SerializeField] private Image staminaFrame;
    [SerializeField] private Image staminaPulse;
    [SerializeField] private CanvasGroup batteryGroup;
    [SerializeField] private Image batteryFill;
    [SerializeField] private Image batteryFrame;
    [SerializeField] private Image batteryPulse;
    [SerializeField] private MonoBehaviour flashlightBatterySource;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
    [SerializeField] private Volume globalVolume;
#endif

    [Header("HUD")]
    [SerializeField] private float visibleAfterUse = 1.5f;
    [SerializeField] private float fadeSpeed = 6.5f;

    [Header("Run FX")]
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float sprintFov = 69f;
    [SerializeField] private float fovSmooth = 5.5f;
    [SerializeField] private float sprintTilt = 1.1f;
    [SerializeField] private float tiltSmooth = 6f;
    [SerializeField] private float lowThreshold = 0.33f;
    [SerializeField] private float blurSecondsOnEmpty = 3f;

    private float staminaVisibleTimer;
    private float batteryVisibleTimer;
    private bool blurRunning;
    private bool exhaustionLatched;
    private Quaternion baseRotation;
    private Color fullColor = new Color(0.82f, 0.86f, 0.78f, 1f);
    private Color lowColor = new Color(0.50f, 0.54f, 0.48f, 1f);
    private Color frameNormal = new Color(0.68f, 0.72f, 0.66f, 0.85f);
    private Color frameDanger = new Color(0.88f, 0.90f, 0.86f, 1f);
#if UNITY_RENDER_PIPELINE_UNIVERSAL
    private DepthOfField dof;
    private Vignette vignette;
    private ChromaticAberration chromatic;
    private bool dofWasActive;
    private float vignetteBase;
    private float chromaticBase;
#endif

    private void Awake()
    {
        if (vitals == null) vitals = GetComponentInParent<BackroomsPlayerVitals>();
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera != null)
        {
            baseFov = targetCamera.fieldOfView;
            baseRotation = targetCamera.transform.localRotation;
        }
        if (staminaGroup != null) staminaGroup.alpha = 0f;
        if (batteryGroup != null) batteryGroup.alpha = 0f;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (globalVolume != null)
        {
            globalVolume.profile.TryGet(out dof);
            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out chromatic);
        }
        if (dof != null) dofWasActive = dof.active;
        if (vignette != null) vignetteBase = vignette.intensity.value;
        if (chromatic != null) chromaticBase = chromatic.intensity.value;
#endif
    }

    private void Update()
    {
        UpdateStaminaHUD();
        UpdateBatteryHUD();
        UpdateRunFX();
    }

    private void UpdateStaminaHUD()
    {
        if (vitals == null) return;

        float stamina01 = vitals.Stamina01;
        bool sprinting = vitals.IsSprintingNow;
        bool exhausted = vitals.IsExhausted;

        if (sprinting || stamina01 < 0.999f || exhausted)
            staminaVisibleTimer = visibleAfterUse;
        else
            staminaVisibleTimer -= Time.deltaTime;

        if (staminaGroup != null)
        {
            float targetAlpha = (staminaVisibleTimer > 0f || blurRunning) ? 1f : 0f;
            staminaGroup.alpha = Mathf.Lerp(staminaGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }

        if (staminaFill != null)
        {
            staminaFill.fillAmount = stamina01;
            staminaFill.color = Color.Lerp(lowColor, fullColor, stamina01);
        }

        bool low = stamina01 <= lowThreshold || exhausted;
        if (staminaFrame != null)
            staminaFrame.color = Color.Lerp(staminaFrame.color, low ? frameDanger : frameNormal, Time.deltaTime * 7f);
        if (staminaPulse != null)
        {
            float pulse = low ? (0.18f + Mathf.Abs(Mathf.Sin(Time.time * 5.2f)) * 0.2f) : 0f;
            Color c = staminaPulse.color;
            c.a = Mathf.Lerp(c.a, pulse, Time.deltaTime * 8f);
            staminaPulse.color = c;
        }

        if (exhausted && !exhaustionLatched)
        {
            exhaustionLatched = true;
            if (!blurRunning) StartCoroutine(BlurRoutine());
        }
        else if (!exhausted && stamina01 > 0.1f)
        {
            exhaustionLatched = false;
        }
    }

    private void UpdateBatteryHUD()
    {
        float battery01 = GetBattery01();
        bool known = battery01 >= 0f;
        if (!known) return;

        bool low = battery01 <= lowThreshold;
        if (low || battery01 < 0.999f)
            batteryVisibleTimer = visibleAfterUse;
        else
            batteryVisibleTimer -= Time.deltaTime;

        if (batteryGroup != null)
        {
            float targetAlpha = batteryVisibleTimer > 0f ? 1f : 0f;
            batteryGroup.alpha = Mathf.Lerp(batteryGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }

        if (batteryFill != null)
        {
            batteryFill.fillAmount = battery01;
            batteryFill.color = Color.Lerp(lowColor, fullColor, battery01);
        }

        if (batteryFrame != null)
            batteryFrame.color = Color.Lerp(batteryFrame.color, low ? frameDanger : frameNormal, Time.deltaTime * 7f);
        if (batteryPulse != null)
        {
            float pulse = low ? (0.1f + Mathf.Abs(Mathf.Sin(Time.time * 4.8f)) * 0.16f) : 0f;
            Color c = batteryPulse.color;
            c.a = Mathf.Lerp(c.a, pulse, Time.deltaTime * 8f);
            batteryPulse.color = c;
        }
    }

    private float GetBattery01()
    {
        if (flashlightBatterySource == null) return -1f;
        var type = flashlightBatterySource.GetType();
        var prop = type.GetProperty("Battery01");
        if (prop != null) return (float)prop.GetValue(flashlightBatterySource, null);
        var field = type.GetField("battery01");
        if (field != null) return (float)field.GetValue(flashlightBatterySource);
        return -1f;
    }

    private void UpdateRunFX()
    {
        if (vitals == null || targetCamera == null) return;

        float stamina01 = vitals.Stamina01;
        bool sprinting = vitals.IsSprintingNow;
        bool exhausted = vitals.IsExhausted;
        float lowFactor = 1f - stamina01;

        float targetFov = sprinting ? Mathf.Lerp(sprintFov - 1f, sprintFov, stamina01) : baseFov;
        if (exhausted) targetFov = baseFov - 1.25f;
        targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Time.deltaTime * fovSmooth);

        float microShake = sprinting ? Mathf.Sin(Time.time * 8.5f) * Mathf.Lerp(0.1f, 0.35f, lowFactor) : 0f;
        float exhaustedTilt = exhausted ? Mathf.Sin(Time.time * 4.5f) * 0.55f : 0f;
        Quaternion targetRot = baseRotation * Quaternion.Euler(0f, 0f, sprinting ? sprintTilt + microShake : exhaustedTilt);
        targetCamera.transform.localRotation = Quaternion.Slerp(targetCamera.transform.localRotation, targetRot, Time.deltaTime * tiltSmooth);

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (!blurRunning)
        {
            if (vignette != null) vignette.intensity.Override(vignetteBase + (sprinting ? lowFactor * 0.12f : 0f) + (exhausted ? 0.08f : 0f));
            if (chromatic != null) chromatic.intensity.Override(chromaticBase + (sprinting ? lowFactor * 0.08f : 0f) + (exhausted ? 0.06f : 0f));
        }
#endif
    }

    private IEnumerator BlurRoutine()
    {
        blurRunning = true;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (dof != null)
        {
            dof.active = true;
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0.01f);
            dof.gaussianEnd.Override(0.22f);
            dof.highQualitySampling.Override(false);
        }
        if (vignette != null) vignette.intensity.Override(0.48f);
        if (chromatic != null) chromatic.intensity.Override(0.28f);
#endif
        yield return new WaitForSeconds(blurSecondsOnEmpty);
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (dof != null) dof.active = dofWasActive;
        if (vignette != null) vignette.intensity.Override(vignetteBase + 0.08f);
        if (chromatic != null) chromatic.intensity.Override(chromaticBase + 0.06f);
#endif
        blurRunning = false;
    }
}
