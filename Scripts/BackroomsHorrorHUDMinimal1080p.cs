using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class BackroomsHorrorHUDMinimal1080p : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BackroomsPlayerVitals vitals;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private MonoBehaviour flashlightBatterySource;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
    [SerializeField] private Volume globalVolume;
#endif

    [Header("HUD Timing")]
    [SerializeField] private float visibleAfterUse = 1.8f;
    [SerializeField] private float fadeSpeed = 7.5f;
    [SerializeField] private float lowThreshold = 0.33f;

    [Header("Screen Layout")]
    [SerializeField] private Vector2 batteryAnchorOffset = new Vector2(48f, 46f);
    [SerializeField] private Vector2 batteryPanelSize = new Vector2(340f, 88f);
    [SerializeField] private float batteryBarWidth = 212f;
    [SerializeField] private float batteryBarHeight = 22f;
    [SerializeField] private float staminaBottomOffset = 54f;
    [SerializeField, Range(0.12f, 0.4f)] private float staminaWidthPercent = 0.24f;
    [SerializeField] private float staminaBarHeight = 18f;
    [SerializeField] private float staminaPanelHeight = 56f;

    [Header("Run FX")]
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float sprintFov = 66.5f;
    [SerializeField] private float fovSmooth = 5.5f;
    [SerializeField] private float sprintTilt = 0.65f;
    [SerializeField] private float tiltSmooth = 6f;
    [SerializeField] private float blurSecondsOnEmpty = 2.8f;

    private Canvas canvas;
    private CanvasScaler scaler;
    private CanvasGroup staminaGroup;
    private Image staminaFill;
    private Image staminaFrame;
    private Image staminaGlow;
    private Text staminaPercent;

    private CanvasGroup batteryGroup;
    private Image batteryFill;
    private Image batteryFrame;
    private Image batteryGlow;
    private Text batteryPercent;

    private RectTransform staminaPanelRT;
    private RectTransform staminaTrackRT;
    private RectTransform staminaInnerRT;
    private RectTransform staminaFillRT;
    private RectTransform staminaGlowRT;

    private RectTransform batteryPanelRT;
    private RectTransform batteryTrackRT;
    private RectTransform batteryInnerRT;
    private RectTransform batteryFillRT;
    private RectTransform batteryGlowRT;

    private float staminaVisibleTimer;
    private float batteryVisibleTimer;
    private bool blurRunning;
    private bool exhaustionLatched;
    private Quaternion baseRotation;
    private float lastAppliedStamina = -1f;
    private float lastAppliedBattery = -1f;

    private readonly Color panelBatteryColor = new Color(0.02f, 0.025f, 0.025f, 0.34f);
    private readonly Color frameNormal = new Color(0.90f, 0.92f, 0.90f, 0.16f);
    private readonly Color frameDanger = new Color(0.97f, 0.98f, 0.97f, 0.42f);
    private readonly Color textDim = new Color(0.82f, 0.85f, 0.82f, 0.92f);
    private readonly Color textBright = new Color(0.97f, 0.98f, 0.97f, 0.98f);
    private readonly Color fillFull = new Color(0.90f, 0.92f, 0.88f, 0.98f);
    private readonly Color fillLow = new Color(0.46f, 0.49f, 0.47f, 0.98f);

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
        TryResolveReferences();

        if (targetCamera != null)
        {
            baseFov = targetCamera.fieldOfView;
            baseRotation = targetCamera.transform.localRotation;
        }

        BuildHUD();
        SetAlphaImmediate(staminaGroup, 0f);
        SetAlphaImmediate(batteryGroup, 0f);
        RefreshResponsiveLayout(true);

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out dof);
            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out chromatic);
            if (dof != null) dofWasActive = dof.active;
            if (vignette != null) vignetteBase = vignette.intensity.value;
            if (chromatic != null) chromaticBase = chromatic.intensity.value;
        }
#endif
    }

    private void OnEnable()
    {
        TryResolveReferences();
        RefreshResponsiveLayout(true);
    }

    private void LateUpdate()
    {
        TryResolveReferences();
        RefreshResponsiveLayout(false);
        UpdateStaminaHUD();
        UpdateBatteryHUD();
        UpdateRunFX();
    }

    private void TryResolveReferences()
    {
        if (vitals == null) vitals = GetComponentInParent<BackroomsPlayerVitals>();
        if (vitals == null) vitals = FindFirstObjectByType<BackroomsPlayerVitals>();

        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;

        if (flashlightBatterySource == null)
            flashlightBatterySource = FindFirstObjectByType<FlashlightBatterySystem>() as MonoBehaviour;
    }

    private void RefreshResponsiveLayout(bool force)
    {
        if (canvas == null || scaler == null || staminaPanelRT == null || batteryPanelRT == null) return;

        float minSide = Mathf.Min(Screen.width, Screen.height);
        float uiScale = Mathf.Clamp(minSide / 1080f, 0.85f, 1.55f);

        float staminaWidth = Mathf.Clamp(Screen.width * staminaWidthPercent, 320f, 760f);
        float staminaHeight = staminaPanelHeight * uiScale;
        float staminaTrackHeight = staminaBarHeight * uiScale;
        float staminaInnerHeight = Mathf.Max(6f, staminaTrackHeight - 4f);
        float staminaY = Mathf.Max(28f, staminaBottomOffset * uiScale);

        staminaPanelRT.sizeDelta = new Vector2(staminaWidth, staminaHeight);
        staminaPanelRT.anchoredPosition = new Vector2(0f, staminaY);
        staminaTrackRT.sizeDelta = new Vector2(staminaWidth, staminaTrackHeight);
        staminaInnerRT.sizeDelta = new Vector2(staminaWidth - 4f, staminaInnerHeight);
        staminaFillRT.sizeDelta = staminaInnerRT.sizeDelta;
        staminaGlowRT.sizeDelta = staminaInnerRT.sizeDelta;
        staminaPercent.fontSize = Mathf.RoundToInt(14f * uiScale);
        staminaPercent.rectTransform.anchoredPosition = new Vector2(0f, (staminaTrackHeight * 0.5f) + (14f * uiScale));
        staminaPercent.rectTransform.sizeDelta = new Vector2(120f * uiScale, 24f * uiScale);

        Vector2 batterySize = batteryPanelSize * uiScale;
        float batteryTrackW = batteryBarWidth * uiScale;
        float batteryTrackH = batteryBarHeight * uiScale;
        float batteryInnerH = Mathf.Max(8f, batteryTrackH - 6f);
        Vector2 batteryOffset = batteryAnchorOffset * uiScale;

        batteryPanelRT.sizeDelta = batterySize;
        batteryPanelRT.anchoredPosition = batteryOffset;
        batteryTrackRT.sizeDelta = new Vector2(batteryTrackW, batteryTrackH);
        batteryInnerRT.sizeDelta = new Vector2(batteryTrackW - 6f, batteryInnerH);
        batteryFillRT.sizeDelta = batteryInnerRT.sizeDelta;
        batteryGlowRT.sizeDelta = batteryInnerRT.sizeDelta;
    }

    private void UpdateStaminaHUD()
    {
        if (vitals == null || staminaGroup == null || staminaFill == null) return;

        float stamina01 = Safe01(vitals.Stamina01);
        bool sprinting = vitals.IsSprintingNow;
        bool exhausted = vitals.IsExhausted;
        bool low = stamina01 <= lowThreshold || exhausted;

        if (sprinting || stamina01 < 0.999f || exhausted) staminaVisibleTimer = visibleAfterUse;
        else staminaVisibleTimer = Mathf.Max(0f, staminaVisibleTimer - Time.deltaTime);

        float targetAlpha = (staminaVisibleTimer > 0f || blurRunning) ? 1f : 0f;
        staminaGroup.alpha = Mathf.Lerp(staminaGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        if (Mathf.Abs(lastAppliedStamina - stamina01) > 0.0001f)
        {
            staminaFill.fillAmount = stamina01;
            lastAppliedStamina = stamina01;
        }

        staminaFill.color = Color.Lerp(fillLow, fillFull, stamina01);
        staminaFrame.color = Color.Lerp(staminaFrame.color, low ? frameDanger : frameNormal, Time.deltaTime * 10f);
        staminaPercent.text = Mathf.RoundToInt(stamina01 * 100f) + "%";
        staminaPercent.color = low ? textBright : textDim;

        Color glow = staminaGlow.color;
        float pulse = low ? 0.10f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4.8f)) * 0.10f : 0f;
        glow.a = Mathf.Lerp(glow.a, pulse, Time.deltaTime * 10f);
        staminaGlow.color = glow;

        if (exhausted && !exhaustionLatched)
        {
            exhaustionLatched = true;
            if (!blurRunning) StartCoroutine(BlurRoutine());
        }
        else if (!exhausted && stamina01 > 0.12f)
        {
            exhaustionLatched = false;
        }
    }

    private void UpdateBatteryHUD()
    {
        if (batteryGroup == null || batteryFill == null) return;

        float battery01 = GetBattery01();
        if (battery01 < 0f)
        {
            SetAlphaImmediate(batteryGroup, 0f);
            return;
        }

        battery01 = Safe01(battery01);
        bool low = battery01 <= lowThreshold;

        if (battery01 < 0.999f || low) batteryVisibleTimer = visibleAfterUse;
        else batteryVisibleTimer = Mathf.Max(0f, batteryVisibleTimer - Time.deltaTime);

        float targetAlpha = batteryVisibleTimer > 0f ? 1f : 0f;
        batteryGroup.alpha = Mathf.Lerp(batteryGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        if (Mathf.Abs(lastAppliedBattery - battery01) > 0.0001f)
        {
            batteryFill.fillAmount = battery01;
            lastAppliedBattery = battery01;
        }

        batteryFill.color = Color.Lerp(fillLow, fillFull, battery01);
        batteryFrame.color = Color.Lerp(batteryFrame.color, low ? frameDanger : frameNormal, Time.deltaTime * 10f);
        batteryPercent.text = Mathf.RoundToInt(battery01 * 100f) + "%";
        batteryPercent.color = low ? textBright : textDim;

        Color glow = batteryGlow.color;
        float pulse = low ? 0.08f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4.1f)) * 0.10f : 0f;
        glow.a = Mathf.Lerp(glow.a, pulse, Time.deltaTime * 10f);
        batteryGlow.color = glow;
    }

    private float GetBattery01()
    {
        if (flashlightBatterySource == null) return -1f;

        System.Type type = flashlightBatterySource.GetType();

        MethodInfo percentMethod = type.GetMethod("GetBatteryPercent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (percentMethod != null)
        {
            object value = percentMethod.Invoke(flashlightBatterySource, null);
            if (TryConvertToFloat(value, out float p)) return p > 1.01f ? p / 100f : p;
        }

        MethodInfo normalizedMethod = type.GetMethod("GetBattery01", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (normalizedMethod != null)
        {
            object value = normalizedMethod.Invoke(flashlightBatterySource, null);
            if (TryConvertToFloat(value, out float p)) return p;
        }

        PropertyInfo prop = type.GetProperty("Battery01", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null)
        {
            object value = prop.GetValue(flashlightBatterySource, null);
            if (TryConvertToFloat(value, out float p)) return p;
        }

        FieldInfo field = type.GetField("battery01", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            object value = field.GetValue(flashlightBatterySource);
            if (TryConvertToFloat(value, out float p)) return p;
        }

        PropertyInfo currentBattery = type.GetProperty("CurrentBattery", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        PropertyInfo maxBattery = type.GetProperty("MaxBattery", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (currentBattery != null && maxBattery != null)
        {
            object cur = currentBattery.GetValue(flashlightBatterySource, null);
            object max = maxBattery.GetValue(flashlightBatterySource, null);
            if (TryConvertToFloat(cur, out float c) && TryConvertToFloat(max, out float m) && m > 0f) return c / m;
        }

        FieldInfo currentField = type.GetField("currentBattery", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo maxField = type.GetField("maxBattery", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (currentField != null && maxField != null)
        {
            object cur = currentField.GetValue(flashlightBatterySource);
            object max = maxField.GetValue(flashlightBatterySource);
            if (TryConvertToFloat(cur, out float c) && TryConvertToFloat(max, out float m) && m > 0f) return c / m;
        }

        return -1f;
    }

    private void UpdateRunFX()
    {
        if (vitals == null || targetCamera == null) return;

        float stamina01 = Safe01(vitals.Stamina01);
        bool sprinting = vitals.IsSprintingNow;
        bool exhausted = vitals.IsExhausted;
        float lowFactor = 1f - stamina01;

        float targetFov = sprinting ? Mathf.Lerp(sprintFov - 0.8f, sprintFov, stamina01) : baseFov;
        if (exhausted) targetFov = baseFov - 0.85f;
        targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Time.deltaTime * fovSmooth);

        float microTilt = sprinting ? Mathf.Sin(Time.time * 8.2f) * Mathf.Lerp(0.05f, 0.18f, lowFactor) : 0f;
        float exhaustedTilt = exhausted ? Mathf.Sin(Time.time * 4.2f) * 0.30f : 0f;
        Quaternion targetRot = baseRotation * Quaternion.Euler(0f, 0f, sprinting ? sprintTilt + microTilt : exhaustedTilt);
        targetCamera.transform.localRotation = Quaternion.Slerp(targetCamera.transform.localRotation, targetRot, Time.deltaTime * tiltSmooth);

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (!blurRunning)
        {
            if (vignette != null) vignette.intensity.Override(vignetteBase + (sprinting ? lowFactor * 0.06f : 0f) + (exhausted ? 0.05f : 0f));
            if (chromatic != null) chromatic.intensity.Override(chromaticBase + (sprinting ? lowFactor * 0.025f : 0f) + (exhausted ? 0.025f : 0f));
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
            dof.gaussianEnd.Override(0.20f);
            dof.highQualitySampling.Override(false);
        }
        if (vignette != null) vignette.intensity.Override(0.34f);
        if (chromatic != null) chromatic.intensity.Override(0.16f);
#endif
        yield return new WaitForSeconds(blurSecondsOnEmpty);
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (dof != null) dof.active = dofWasActive;
        if (vignette != null) vignette.intensity.Override(vignetteBase + 0.03f);
        if (chromatic != null) chromatic.intensity.Override(chromaticBase + 0.02f);
#endif
        blurRunning = false;
    }

    private void BuildHUD()
    {
        Transform existing = transform.Find("BackroomsHorrorHUDMinimal1080pRoot");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject root = new GameObject("BackroomsHorrorHUDMinimal1080pRoot");
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        BuildStamina(root.transform);
        BuildBattery(root.transform);
    }

    private void BuildStamina(Transform parent)
    {
        GameObject panel = CreateRect("StaminaPanel", parent, new Vector2(0.5f, 0f), new Vector2(460f, staminaPanelHeight), new Vector2(0f, staminaBottomOffset));
        staminaPanelRT = panel.GetComponent<RectTransform>();
        staminaGroup = panel.AddComponent<CanvasGroup>();

        staminaFrame = CreateRectImage("StaminaTrack", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(460f, staminaBarHeight), Vector2.zero, frameNormal);
        staminaTrackRT = staminaFrame.rectTransform;
        Image staminaInner = CreateRectImage("StaminaInner", staminaFrame.transform, new Vector2(0.5f, 0.5f), new Vector2(456f, staminaBarHeight - 4f), Vector2.zero, new Color(0.01f, 0.015f, 0.015f, 0.88f));
        staminaInnerRT = staminaInner.rectTransform;
        staminaFill = CreateFilledBar("StaminaFill", staminaFrame.transform, new Vector2(0.5f, 0.5f), staminaInnerRT.sizeDelta, Vector2.zero, fillFull);
        staminaFillRT = staminaFill.rectTransform;
        staminaGlow = CreateFilledBar("StaminaGlow", staminaFrame.transform, new Vector2(0.5f, 0.5f), staminaInnerRT.sizeDelta, Vector2.zero, new Color(0.98f, 0.99f, 0.98f, 0f));
        staminaGlowRT = staminaGlow.rectTransform;
        staminaPercent = CreateText("StaminaPercent", panel.transform, "100%", 14, FontStyle.Bold, textDim, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, 18f), new Vector2(120f, 28f));
    }

    private void BuildBattery(Transform parent)
    {
        GameObject panel = CreateRect("BatteryPanel", parent, new Vector2(0f, 0f), batteryPanelSize, batteryAnchorOffset);
        batteryPanelRT = panel.GetComponent<RectTransform>();
        batteryGroup = panel.AddComponent<CanvasGroup>();
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelBatteryColor;

        Text nameText = CreateText("BatteryName", panel.transform, "BATTERY", 16, FontStyle.Bold, textDim, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(94f, 24f));
        batteryPercent = CreateText("BatteryPercent", panel.transform, "100%", 18, FontStyle.Bold, textBright, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(74f, 26f));
        nameText.resizeTextForBestFit = true;
        batteryPercent.resizeTextForBestFit = true;

        batteryFrame = CreateRectImage("BatteryTrack", panel.transform, new Vector2(0f, 0.5f), new Vector2(batteryBarWidth, batteryBarHeight), new Vector2(104f, 0f), frameNormal);
        batteryTrackRT = batteryFrame.rectTransform;
        Image batteryInner = CreateRectImage("BatteryInner", batteryFrame.transform, new Vector2(0.5f, 0.5f), new Vector2(batteryBarWidth - 6f, batteryBarHeight - 6f), Vector2.zero, new Color(0.01f, 0.015f, 0.015f, 0.90f));
        batteryInnerRT = batteryInner.rectTransform;
        batteryFill = CreateFilledBar("BatteryFill", batteryFrame.transform, new Vector2(0.5f, 0.5f), batteryInnerRT.sizeDelta, Vector2.zero, fillFull);
        batteryFillRT = batteryFill.rectTransform;
        batteryGlow = CreateFilledBar("BatteryGlow", batteryFrame.transform, new Vector2(0.5f, 0.5f), batteryInnerRT.sizeDelta, Vector2.zero, new Color(0.98f, 0.99f, 0.98f, 0f));
        batteryGlowRT = batteryGlow.rectTransform;
    }

    private void SetAlphaImmediate(CanvasGroup group, float alpha)
    {
        if (group != null) group.alpha = alpha;
    }

    private static bool TryConvertToFloat(object value, out float result)
    {
        if (value is float f)
        {
            result = f;
            return true;
        }
        if (value is int i)
        {
            result = i;
            return true;
        }
        if (value is double d)
        {
            result = (float)d;
            return true;
        }
        result = 0f;
        return false;
    }

    private static float Safe01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
        return Mathf.Clamp01(value);
    }

    private GameObject CreateRect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x == 0f ? 0f : anchor.x == 1f ? 1f : 0.5f, anchor.y == 0f ? 0f : anchor.y == 1f ? 1f : 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return obj;
    }

    private Image CreateRectImage(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos, Color color)
    {
        GameObject obj = CreateRect(name, parent, anchor, size, pos);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Image CreateFilledBar(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos, Color color)
    {
        Image image = CreateRectImage(name, parent, anchor, size, pos, color);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 1f;
        return image;
    }

    private Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, Color color, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject obj = CreateRect(name, parent, anchor, size, pos);
        Text text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
