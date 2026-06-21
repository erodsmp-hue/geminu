using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Reflection;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class BackroomsAutoHorrorHUD : MonoBehaviour
{
    [Header("Auto refs")]
    [SerializeField] private BackroomsAutoPlayerVitals vitals;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private MonoBehaviour flashlightBatterySource;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
    [SerializeField] private Volume globalVolume;
#endif

    [Header("Stamina UI")]
    [SerializeField] private CanvasGroup staminaGroup;
    [SerializeField] private RectTransform staminaRoot;
    [SerializeField] private Image staminaFill;
    [SerializeField] private Image staminaFrame;
    [SerializeField] private Image staminaPulse;
    [SerializeField] private RectTransform staminaShine;
    [SerializeField] private Text staminaLabel;

    [Header("Battery UI")]
    [SerializeField] private CanvasGroup batteryGroup;
    [SerializeField] private RectTransform batteryRoot;
    [SerializeField] private Image batteryFill;
    [SerializeField] private Image batteryFrame;
    [SerializeField] private Image batteryPulse;
    [SerializeField] private RectTransform batteryShine;
    [SerializeField] private Text batteryLabel;

    [Header("HUD Feel")]
    [SerializeField] private float visibleAfterUse = 1.6f;
    [SerializeField] private float fadeSpeed = 7.5f;
    [SerializeField] private float slideDistance = 12f;
    [SerializeField] private float lowThreshold = 0.33f;

    [Header("Run FX")]
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float sprintFov = 68.5f;
    [SerializeField] private float fovSmooth = 5.5f;
    [SerializeField] private float sprintTilt = 1.0f;
    [SerializeField] private float tiltSmooth = 6f;
    [SerializeField] private float blurSecondsOnEmpty = 3f;

    private float staminaVisibleTimer;
    private float batteryVisibleTimer;
    private bool blurRunning;
    private bool exhaustionLatched;
    private Quaternion baseRotation;
    private Vector2 staminaBasePos;
    private Vector2 batteryBasePos;
    private readonly Color fullColor = new Color(0.83f, 0.86f, 0.79f, 1f);
    private readonly Color lowColor = new Color(0.47f, 0.50f, 0.45f, 1f);
    private readonly Color frameNormal = new Color(0.67f, 0.71f, 0.65f, 0.82f);
    private readonly Color frameDanger = new Color(0.92f, 0.94f, 0.90f, 1f);
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
        if (vitals == null) vitals = FindObjectOfType<BackroomsAutoPlayerVitals>();
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (flashlightBatterySource == null) flashlightBatterySource = FindBatterySource();
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (globalVolume == null) globalVolume = FindObjectOfType<Volume>();
#endif
        if (targetCamera != null)
        {
            baseFov = targetCamera.fieldOfView;
            baseRotation = targetCamera.transform.localRotation;
        }
        if (staminaGroup != null) staminaGroup.alpha = 0f;
        if (batteryGroup != null) batteryGroup.alpha = 0f;
        if (staminaRoot != null) staminaBasePos = staminaRoot.anchoredPosition;
        if (batteryRoot != null) batteryBasePos = batteryRoot.anchoredPosition;
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
        if (vitals == null) vitals = FindObjectOfType<BackroomsAutoPlayerVitals>();
        if (flashlightBatterySource == null) flashlightBatterySource = FindBatterySource();
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

        float targetAlpha = (staminaVisibleTimer > 0f || blurRunning) ? 1f : 0f;
        AnimateBar(staminaGroup, staminaRoot, staminaBasePos, targetAlpha, stamina01, exhausted, staminaFill, staminaFrame, staminaPulse, staminaShine, staminaLabel, "STM");

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
        if (battery01 < 0f) return;

        bool low = battery01 <= lowThreshold;
        if (low || battery01 < 0.999f)
            batteryVisibleTimer = visibleAfterUse;
        else
            batteryVisibleTimer -= Time.deltaTime;

        float targetAlpha = batteryVisibleTimer > 0f ? 1f : 0f;
        AnimateBar(batteryGroup, batteryRoot, batteryBasePos, targetAlpha, battery01, low, batteryFill, batteryFrame, batteryPulse, batteryShine, batteryLabel, "BAT");
    }

    private void AnimateBar(CanvasGroup group, RectTransform root, Vector2 basePos, float targetAlpha, float value01, bool warning, Image fill, Image frame, Image pulse, RectTransform shine, Text label, string text)
    {
        if (group != null)
            group.alpha = Mathf.Lerp(group.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        if (root != null)
        {
            Vector2 hidden = basePos + new Vector2(0f, -slideDistance);
            root.anchoredPosition = Vector2.Lerp(root.anchoredPosition, targetAlpha > 0.5f ? basePos : hidden, Time.deltaTime * fadeSpeed);
        }
        if (fill != null)
        {
            fill.fillAmount = value01;
            fill.color = Color.Lerp(lowColor, fullColor, value01);
        }
        if (frame != null)
            frame.color = Color.Lerp(frame.color, warning ? frameDanger : frameNormal, Time.deltaTime * 8f);
        if (pulse != null)
        {
            float pulseAlpha = warning ? (0.08f + Mathf.Abs(Mathf.Sin(Time.time * 4.8f)) * 0.20f) : 0f;
            Color c = pulse.color;
            c.a = Mathf.Lerp(c.a, pulseAlpha, Time.deltaTime * 8f);
            pulse.color = c;
        }
        if (shine != null)
        {
            float width = 180f;
            float travel = Mathf.Lerp(-width, width, Mathf.PingPong(Time.time * 0.22f, 1f));
            Vector2 p = shine.anchoredPosition;
            p.x = travel;
            shine.anchoredPosition = p;
        }
        if (label != null)
        {
            label.text = text + "  " + Mathf.RoundToInt(value01 * 100f).ToString("000") + "%";
            Color c = label.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * fadeSpeed);
            label.color = c;
        }
    }

    private float GetBattery01()
    {
        if (flashlightBatterySource == null) return -1f;
        var type = flashlightBatterySource.GetType();

        float v;
        if (TryGetNormalized(type, flashlightBatterySource, "Battery01", out v)) return Mathf.Clamp01(v);
        if (TryGetNormalized(type, flashlightBatterySource, "battery01", out v)) return Mathf.Clamp01(v);

        float current = GetFloat(type, flashlightBatterySource, "currentBattery", float.NaN);
        float max = GetFloat(type, flashlightBatterySource, "maxBattery", float.NaN);
        if (!float.IsNaN(current) && !float.IsNaN(max) && max > 0f) return Mathf.Clamp01(current / max);

        current = GetFloat(type, flashlightBatterySource, "batteryCharge", float.NaN);
        max = GetFloat(type, flashlightBatterySource, "maxCharge", float.NaN);
        if (!float.IsNaN(current) && !float.IsNaN(max) && max > 0f) return Mathf.Clamp01(current / max);

        current = GetFloat(type, flashlightBatterySource, "batteryLife", float.NaN);
        max = GetFloat(type, flashlightBatterySource, "batteryLifeMax", float.NaN);
        if (!float.IsNaN(current) && !float.IsNaN(max) && max > 0f) return Mathf.Clamp01(current / max);

        return -1f;
    }

    private MonoBehaviour FindBatterySource()
    {
        var all = FindObjectsOfType<MonoBehaviour>(true);
        foreach (var mb in all)
        {
            if (mb == null) continue;
            var t = mb.GetType();
            float v;
            if (TryGetNormalized(t, mb, "Battery01", out v)) return mb;
            if (TryGetNormalized(t, mb, "battery01", out v)) return mb;
            if (HasMember(t, "currentBattery") && HasMember(t, "maxBattery")) return mb;
            if (HasMember(t, "batteryCharge") && HasMember(t, "maxCharge")) return mb;
            if (HasMember(t, "batteryLife") && HasMember(t, "batteryLifeMax")) return mb;
        }
        return null;
    }

    private bool HasMember(System.Type type, string name)
    {
        return type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null || type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null;
    }

    private bool TryGetNormalized(System.Type type, object source, string name, out float value)
    {
        value = 0f;
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(float))
        {
            value = (float)prop.GetValue(source, null);
            return true;
        }
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(float))
        {
            value = (float)field.GetValue(source);
            return true;
        }
        return false;
    }

    private float GetFloat(System.Type type, object source, string name, float fallback)
    {
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(float)) return (float)prop.GetValue(source, null);
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(float)) return (float)field.GetValue(source);
        return fallback;
    }

    private void UpdateRunFX()
    {
        if (vitals == null || targetCamera == null) return;

        float stamina01 = vitals.Stamina01;
        bool sprinting = vitals.IsSprintingNow;
        bool exhausted = vitals.IsExhausted;
        float lowFactor = 1f - stamina01;

        float targetFov = sprinting ? Mathf.Lerp(sprintFov - 1f, sprintFov, stamina01) : baseFov;
        if (exhausted) targetFov = baseFov - 1.2f;
        targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Time.deltaTime * fovSmooth);

        float microRoll = sprinting ? Mathf.Sin(Time.time * 8.2f) * Mathf.Lerp(0.08f, 0.30f, lowFactor) : 0f;
        float exhaustedRoll = exhausted ? Mathf.Sin(Time.time * 4.0f) * 0.45f : 0f;
        Quaternion targetRot = baseRotation * Quaternion.Euler(0f, 0f, sprinting ? sprintTilt + microRoll : exhaustedRoll);
        targetCamera.transform.localRotation = Quaternion.Slerp(targetCamera.transform.localRotation, targetRot, Time.deltaTime * tiltSmooth);

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (!blurRunning)
        {
            if (vignette != null) vignette.intensity.Override(vignetteBase + (sprinting ? lowFactor * 0.10f : 0f) + (exhausted ? 0.08f : 0f));
            if (chromatic != null) chromatic.intensity.Override(chromaticBase + (sprinting ? lowFactor * 0.06f : 0f) + (exhausted ? 0.05f : 0f));
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
        if (chromatic != null) chromatic.intensity.Override(0.26f);
#endif
        yield return new WaitForSeconds(blurSecondsOnEmpty);
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (dof != null) dof.active = dofWasActive;
        if (vignette != null) vignette.intensity.Override(vignetteBase + 0.08f);
        if (chromatic != null) chromatic.intensity.Override(chromaticBase + 0.05f);
#endif
        blurRunning = false;
    }
}
