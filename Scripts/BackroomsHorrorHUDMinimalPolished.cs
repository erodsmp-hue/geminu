using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class BackroomsHorrorHUDMinimalPolished : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BackroomsPlayerVitals vitals;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private MonoBehaviour flashlightBatterySource;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
    [SerializeField] private Volume globalVolume;
#endif

    [Header("Visibility")]
    [SerializeField] private float visibleAfterUse = 1.8f;
    [SerializeField] private float fadeSpeed = 7f;

    [Header("Run FX")]
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float sprintFov = 67f;
    [SerializeField] private float fovSmooth = 5.5f;
    [SerializeField] private float sprintTilt = 0.8f;
    [SerializeField] private float tiltSmooth = 6f;
    [SerializeField] private float lowThreshold = 0.33f;
    [SerializeField] private float blurSecondsOnEmpty = 2.8f;

    private Canvas canvas;
    private CanvasScaler scaler;
    private GraphicRaycaster raycaster;
    private CanvasGroup hudGroup;

    private CanvasGroup staminaGroup;
    private Image staminaBar;
    private Image staminaFrame;
    private Image staminaGlow;
    private Text staminaLabel;
    private Text staminaPercent;

    private CanvasGroup batteryGroup;
    private Image batteryBar;
    private Image batteryFrame;
    private Image batteryGlow;
    private Text batteryLabel;
    private Text batteryPercent;

    private float staminaVisibleTimer;
    private float batteryVisibleTimer;
    private bool blurRunning;
    private bool exhaustionLatched;
    private Quaternion baseRotation;

    private readonly Color textColor = new Color(0.93f, 0.95f, 0.92f, 0.92f);
    private readonly Color dimText = new Color(0.72f, 0.75f, 0.72f, 0.84f);
    private readonly Color frameNormal = new Color(0.84f, 0.87f, 0.84f, 0.18f);
    private readonly Color frameDanger = new Color(0.94f, 0.96f, 0.94f, 0.44f);
    private readonly Color panelBack = new Color(0.03f, 0.035f, 0.035f, 0.34f);
    private readonly Color fillFull = new Color(0.86f, 0.89f, 0.83f, 0.95f);
    private readonly Color fillLow = new Color(0.43f, 0.47f, 0.43f, 0.95f);

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

        BuildHUD();
        hudGroup.alpha = 1f;
        staminaGroup.alpha = 0f;
        batteryGroup.alpha = 0f;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (globalVolume != null)
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

        if (sprinting || stamina01 < 0.999f || exhausted) staminaVisibleTimer = visibleAfterUse;
        else staminaVisibleTimer -= Time.deltaTime;

        float targetAlpha = staminaVisibleTimer > 0f || blurRunning ? 1f : 0f;
        staminaGroup.alpha = Mathf.Lerp(staminaGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        bool low = stamina01 <= lowThreshold || exhausted;
        staminaBar.fillAmount = stamina01;
        staminaBar.color = Color.Lerp(fillLow, fillFull, stamina01);
        staminaFrame.color = Color.Lerp(staminaFrame.color, low ? frameDanger : frameNormal, Time.deltaTime * 8f);
        staminaLabel.color = low ? textColor : dimText;
        staminaPercent.text = Mathf.RoundToInt(stamina01 * 100f) + "%";
        staminaPercent.color = low ? textColor : dimText;

        Color glow = staminaGlow.color;
        float pulse = low ? 0.12f + Mathf.Abs(Mathf.Sin(Time.time * 4.8f)) * 0.10f : 0f;
        glow.a = Mathf.Lerp(glow.a, pulse, Time.deltaTime * 8f);
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
        float battery01 = GetBattery01();
        if (battery01 < 0f) return;

        bool low = battery01 <= lowThreshold;
        if (battery01 < 0.999f) batteryVisibleTimer = visibleAfterUse;
        else batteryVisibleTimer -= Time.deltaTime;

        float targetAlpha = batteryVisibleTimer > 0f ? 1f : 0f;
        batteryGroup.alpha = Mathf.Lerp(batteryGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        batteryBar.fillAmount = battery01;
        batteryBar.color = Color.Lerp(fillLow, fillFull, battery01);
        batteryFrame.color = Color.Lerp(batteryFrame.color, low ? frameDanger : frameNormal, Time.deltaTime * 8f);
        batteryLabel.color = low ? textColor : dimText;
        batteryPercent.text = Mathf.RoundToInt(battery01 * 100f) + "%";
        batteryPercent.color = low ? textColor : dimText;

        Color glow = batteryGlow.color;
        float pulse = low ? 0.10f + Mathf.Abs(Mathf.Sin(Time.time * 4.3f)) * 0.08f : 0f;
        glow.a = Mathf.Lerp(glow.a, pulse, Time.deltaTime * 8f);
        batteryGlow.color = glow;
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

        float targetFov = sprinting ? Mathf.Lerp(sprintFov - 0.8f, sprintFov, stamina01) : baseFov;
        if (exhausted) targetFov = baseFov - 1f;
        targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, Time.deltaTime * fovSmooth);

        float microTilt = sprinting ? Mathf.Sin(Time.time * 8.2f) * Mathf.Lerp(0.08f, 0.24f, lowFactor) : 0f;
        float exhaustedTilt = exhausted ? Mathf.Sin(Time.time * 4.2f) * 0.42f : 0f;
        Quaternion targetRot = baseRotation * Quaternion.Euler(0f, 0f, sprinting ? sprintTilt + microTilt : exhaustedTilt);
        targetCamera.transform.localRotation = Quaternion.Slerp(targetCamera.transform.localRotation, targetRot, Time.deltaTime * tiltSmooth);

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (!blurRunning)
        {
            if (vignette != null) vignette.intensity.Override(vignetteBase + (sprinting ? lowFactor * 0.08f : 0f) + (exhausted ? 0.06f : 0f));
            if (chromatic != null) chromatic.intensity.Override(chromaticBase + (sprinting ? lowFactor * 0.04f : 0f) + (exhausted ? 0.04f : 0f));
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
        if (vignette != null) vignette.intensity.Override(0.40f);
        if (chromatic != null) chromatic.intensity.Override(0.20f);
#endif
        yield return new WaitForSeconds(blurSecondsOnEmpty);
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        if (dof != null) dof.active = dofWasActive;
        if (vignette != null) vignette.intensity.Override(vignetteBase + 0.04f);
        if (chromatic != null) chromatic.intensity.Override(chromaticBase + 0.03f);
#endif
        blurRunning = false;
    }

    private void BuildHUD()
    {
        GameObject root = new GameObject("BackroomsMinimalPolishedHUD");
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        raycaster = root.AddComponent<GraphicRaycaster>();
        hudGroup = root.AddComponent<CanvasGroup>();

        staminaGroup = BuildMeter(root.transform, "StaminaPanel", "STAMINA", new Vector2(40f, 40f), out staminaBar, out staminaFrame, out staminaGlow, out staminaLabel, out staminaPercent);
        batteryGroup = BuildMeter(root.transform, "BatteryPanel", "BATTERY", new Vector2(40f, 112f), out batteryBar, out batteryFrame, out batteryGlow, out batteryLabel, out batteryPercent);
    }

    private CanvasGroup BuildMeter(Transform parent, string name, string label, Vector2 anchoredPos, out Image fill, out Image frame, out Image glow, out Text labelText, out Text percentText)
    {
        GameObject panel = CreateRect(name, parent, new Vector2(0f, 0f), new Vector2(250f, 56f), anchoredPos);
        CanvasGroup group = panel.AddComponent<CanvasGroup>();
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelBack;

        frame = CreateRectImage("Frame", panel.transform, new Vector2(0f, 0.5f), new Vector2(136f, 16f), new Vector2(16f, 0f), frameNormal);
        CreateRectImage("Inner", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(130f, 10f), Vector2.zero, new Color(0.02f, 0.025f, 0.025f, 0.90f));
        fill = CreateFilledBar("Fill", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(130f, 10f), Vector2.zero, fillFull);
        glow = CreateFilledBar("Glow", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(130f, 10f), Vector2.zero, new Color(0.95f, 0.97f, 0.95f, 0f));

        labelText = CreateText("Label", panel.transform, label, 12, FontStyle.Bold, dimText, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(170f, 10f), new Vector2(70f, 16f));
        percentText = CreateText("Percent", panel.transform, "100%", 12, FontStyle.Bold, dimText, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(170f, -10f), new Vector2(64f, 16f));

        return group;
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
