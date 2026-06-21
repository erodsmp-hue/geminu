using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BatteryInventoryCanvasUIBackroomsHorror : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BatteryInventory inventory;
    [SerializeField] private Sprite batteryIcon;

    [Header("Input")]
    [SerializeField] private Key toggleInventoryKey = Key.Tab;
    [SerializeField] private Key useBatteryKey = Key.R;

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.16f;
    [SerializeField] private float selectedPulseSpeed = 2.7f;
    [SerializeField] private float noiseSpeed = 0.9f;

    [Header("Palette")]
    [SerializeField] private Color overlayColor = new Color(0.03f, 0.03f, 0.025f, 0.78f);
    [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.065f, 0.96f);
    [SerializeField] private Color panelEdge = new Color(0.70f, 0.68f, 0.55f, 0.30f);
    [SerializeField] private Color dirtyLine = new Color(0.58f, 0.54f, 0.40f, 0.16f);
    [SerializeField] private Color textMain = new Color(0.88f, 0.86f, 0.74f, 1f);
    [SerializeField] private Color textDim = new Color(0.69f, 0.67f, 0.58f, 0.78f);
    [SerializeField] private Color slotIdle = new Color(0.10f, 0.10f, 0.085f, 0.96f);
    [SerializeField] private Color slotFilled = new Color(0.17f, 0.16f, 0.12f, 0.98f);
    [SerializeField] private Color slotSelected = new Color(0.78f, 0.74f, 0.50f, 0.95f);
    [SerializeField] private Color batteryOn = new Color(0.84f, 0.80f, 0.55f, 1f);
    [SerializeField] private Color batteryOff = new Color(0.18f, 0.18f, 0.16f, 1f);
    [SerializeField] private Color warningColor = new Color(0.78f, 0.26f, 0.18f, 0.92f);

    private Canvas canvas;
    private CanvasScaler scaler;
    private GraphicRaycaster raycaster;
    private CanvasGroup canvasGroup;
    private GameObject root;
    private RectTransform panelRT;
    private Image panelImage;
    private Image panelBorder;
    private Image staticNoise;
    private Image scanlineImage;
    private Text titleText;
    private Text subtitleText;
    private Text helperText;
    private Text detailTitleText;
    private Text detailBodyText;
    private Text footerText;
    private readonly List<Button> slotButtons = new();
    private readonly List<Image> slotBackgrounds = new();
    private readonly List<Image> slotBorders = new();
    private readonly List<Image> slotIcons = new();
    private readonly List<Text> slotIndexTexts = new();
    private readonly List<Text> slotStateTexts = new();
    private readonly List<GameObject> slotGlows = new();
    private readonly List<Behaviour> pausedInputScripts = new();

    private int selectedIndex;
    private bool inventoryOpen;
    private bool transitionBusy;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private Texture2D noiseTexture;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<BatteryInventory>();
        if (inventory == null) inventory = FindFirstObjectByType<BatteryInventory>();

        EnsureEventSystem();
        BuildUI();
        root.SetActive(false);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        Refresh();
    }

    private void Update()
    {
        if (Keyboard.current == null || inventory == null) return;

        if (Keyboard.current[toggleInventoryKey].wasPressedThisFrame && !transitionBusy)
        {
            if (inventoryOpen) StartCoroutine(CloseInventoryRoutine());
            else StartCoroutine(OpenInventoryRoutine());
        }

        if (!inventoryOpen || transitionBusy) return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame) MoveSelection(-1, false);
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame) MoveSelection(1, false);
        if (Keyboard.current.upArrowKey.wasPressedThisFrame) MoveSelection(-3, true);
        if (Keyboard.current.downArrowKey.wasPressedThisFrame) MoveSelection(3, true);
        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current[useBatteryKey].wasPressedThisFrame)
        {
            TryUseSelectedBattery();
        }

        AnimateVisuals();
    }

    private IEnumerator OpenInventoryRoutine()
    {
        transitionBusy = true;
        inventoryOpen = true;
        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;

        PauseLikelyInputScripts();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;

        root.SetActive(true);
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        EventSystem.current?.SetSelectedGameObject(slotButtons[Mathf.Clamp(selectedIndex, 0, slotButtons.Count - 1)].gameObject);

        yield return FadeCanvasGroup(0f, 1f);
        transitionBusy = false;
    }

    private IEnumerator CloseInventoryRoutine()
    {
        transitionBusy = true;
        yield return FadeCanvasGroup(1f, 0f);

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        root.SetActive(false);
        inventoryOpen = false;

        RestorePausedInputScripts();
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLock;
        transitionBusy = false;
    }

    private IEnumerator FadeCanvasGroup(float from, float to)
    {
        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            t = t * t * (3f - 2f * t);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private void MoveSelection(int delta, bool vertical)
    {
        int next = selectedIndex + delta;
        if (vertical)
        {
            next = Mathf.Clamp(next, 0, 5);
        }
        else
        {
            int row = selectedIndex / 3;
            int col = Mathf.Clamp((selectedIndex % 3) + delta, 0, 2);
            next = row * 3 + col;
        }

        selectedIndex = Mathf.Clamp(next, 0, 5);
        EventSystem.current?.SetSelectedGameObject(slotButtons[selectedIndex].gameObject);
        Refresh();
    }

    private void TryUseSelectedBattery()
    {
        if (selectedIndex < inventory.StoredBatteries)
        {
            inventory.TryUseStoredBattery();
            if (selectedIndex >= inventory.StoredBatteries)
                selectedIndex = Mathf.Max(0, inventory.StoredBatteries - 1);
            Refresh();
        }
    }

    private void PauseLikelyInputScripts()
    {
        pausedInputScripts.Clear();
        Behaviour[] behaviours = FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour == null || !behaviour.enabled || behaviour == this) continue;
            string name = behaviour.GetType().Name.ToLowerInvariant();
            if (!name.Contains("look") && !name.Contains("mouse") && !name.Contains("input") && !name.Contains("controller") && !name.Contains("player"))
                continue;
            if (behaviour is GraphicRaycaster || behaviour is EventSystem || behaviour is StandaloneInputModule || behaviour is CanvasScaler || behaviour is Canvas || behaviour is CanvasGroup)
                continue;
            pausedInputScripts.Add(behaviour);
            behaviour.enabled = false;
        }
    }

    private void RestorePausedInputScripts()
    {
        foreach (Behaviour behaviour in pausedInputScripts)
            if (behaviour != null) behaviour.enabled = true;
        pausedInputScripts.Clear();
    }

    private void BuildUI()
    {
        root = new GameObject("BackroomsHorrorInventoryCanvas");
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        raycaster = root.AddComponent<GraphicRaycaster>();
        canvasGroup = root.AddComponent<CanvasGroup>();

        GameObject overlay = CreateStretch("Overlay", root.transform);
        Image overlayImage = overlay.AddComponent<Image>();
        overlayImage.color = overlayColor;

        GameObject panel = CreateRect("Panel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(860f, 620f), Vector2.zero);
        panelRT = panel.GetComponent<RectTransform>();
        panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;

        GameObject border = CreateStretch("PanelBorder", panel.transform);
        panelBorder = border.AddComponent<Image>();
        panelBorder.color = panelEdge;
        panelBorder.type = Image.Type.Sliced;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
        outline.effectDistance = new Vector2(2f, -2f);

        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(18f, -18f);

        GameObject grimeTop = CreateRect("GrimeTop", panel.transform, new Vector2(0.5f, 1f), new Vector2(780f, 2f), new Vector2(0f, -64f));
        grimeTop.AddComponent<Image>().color = dirtyLine;

        GameObject grimeBottom = CreateRect("GrimeBottom", panel.transform, new Vector2(0.5f, 0f), new Vector2(780f, 2f), new Vector2(0f, 92f));
        grimeBottom.AddComponent<Image>().color = dirtyLine;

        GameObject title = CreateTextObject("Title", panel.transform, "BATTERY CACHE", 30, FontStyle.Bold, textMain, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(44f, -40f), new Vector2(360f, 40f));
        titleText = title.GetComponent<Text>();

        GameObject subtitle = CreateTextObject("Subtitle", panel.transform, "Recovered utility cells / maintenance stock", 16, FontStyle.Normal, textDim, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(46f, -76f), new Vector2(420f, 24f));
        subtitleText = subtitle.GetComponent<Text>();

        GameObject helper = CreateTextObject("Helper", panel.transform, "TAB close   ARROWS move   ENTER or R use", 15, FontStyle.Normal, textDim, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-44f, -52f), new Vector2(360f, 24f));
        helperText = helper.GetComponent<Text>();

        GameObject leftLabel = CreateTextObject("SectionLabel", panel.transform, "STORAGE GRID", 14, FontStyle.Bold, new Color(textDim.r, textDim.g, textDim.b, 0.92f), TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(46f, -122f), new Vector2(180f, 20f));
        leftLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

        GameObject grid = CreateRect("Grid", panel.transform, new Vector2(0f, 1f), new Vector2(498f, 356f), new Vector2(46f, -312f));
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(150f, 150f);
        layout.spacing = new Vector2(24f, 24f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        for (int i = 0; i < 6; i++)
        {
            GameObject slot = CreateRect("Slot_" + i, grid.transform, new Vector2(0.5f, 0.5f), new Vector2(150f, 150f), Vector2.zero);
            Image slotFrame = slot.AddComponent<Image>();
            slotFrame.color = panelEdge;
            slotBorders.Add(slotFrame);

            Button button = slot.AddComponent<Button>();
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            int index = i;
            button.onClick.AddListener(() => { selectedIndex = index; TryUseSelectedBattery(); Refresh(); });
            slotButtons.Add(button);

            EventTrigger trigger = slot.AddComponent<EventTrigger>();
            AddEvent(trigger, EventTriggerType.PointerEnter, () =>
            {
                selectedIndex = index;
                EventSystem.current?.SetSelectedGameObject(slotButtons[index].gameObject);
                Refresh();
            });

            GameObject inner = CreateRect("Inner", slot.transform, new Vector2(0.5f, 0.5f), new Vector2(138f, 138f), Vector2.zero);
            Image innerImage = inner.AddComponent<Image>();
            innerImage.color = slotIdle;
            slotBackgrounds.Add(innerImage);

            Outline innerOutline = inner.AddComponent<Outline>();
            innerOutline.effectColor = new Color(0f, 0f, 0f, 0.45f);
            innerOutline.effectDistance = new Vector2(1f, -1f);

            GameObject glow = CreateRect("Glow", inner.transform, new Vector2(0.5f, 0.5f), new Vector2(126f, 126f), Vector2.zero);
            Image glowImage = glow.AddComponent<Image>();
            glowImage.color = new Color(slotSelected.r, slotSelected.g, slotSelected.b, 0f);
            slotGlows.Add(glow);

            GameObject number = CreateTextObject("Number", inner.transform, (i + 1).ToString("00"), 14, FontStyle.Normal, textDim, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(48f, 20f));
            slotIndexTexts.Add(number.GetComponent<Text>());

            GameObject iconObj = CreateRect("BatteryShape", inner.transform, new Vector2(0.5f, 0.5f), new Vector2(54f, 98f), new Vector2(0f, -2f));
            Image icon = iconObj.AddComponent<Image>();
            if (batteryIcon != null)
            {
                icon.sprite = batteryIcon;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
            }
            icon.color = batteryOff;
            slotIcons.Add(icon);

            GameObject cap = CreateRect("Cap", inner.transform, new Vector2(0.5f, 1f), new Vector2(16f, 8f), new Vector2(0f, -20f));
            cap.AddComponent<Image>().color = new Color(0.95f, 0.92f, 0.77f, 0.46f);

            GameObject state = CreateTextObject("State", inner.transform, "EMPTY", 13, FontStyle.Bold, textDim, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(100f, 18f));
            slotStateTexts.Add(state.GetComponent<Text>());
        }

        GameObject sidePanel = CreateRect("DetailPanel", panel.transform, new Vector2(1f, 1f), new Vector2(252f, 356f), new Vector2(-46f, -312f));
        Image sidePanelImage = sidePanel.AddComponent<Image>();
        sidePanelImage.color = new Color(0.06f, 0.06f, 0.05f, 0.98f);
        Outline sideOutline = sidePanel.AddComponent<Outline>();
        sideOutline.effectColor = new Color(panelEdge.r, panelEdge.g, panelEdge.b, 0.24f);
        sideOutline.effectDistance = new Vector2(1f, -1f);

        CreateTextObject("DetailLabel", sidePanel.transform, "SELECTED SLOT", 14, FontStyle.Bold, textDim, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(20f, -22f), new Vector2(180f, 18f));
        detailTitleText = CreateTextObject("DetailTitle", sidePanel.transform, "EMPTY SLOT", 24, FontStyle.Bold, textMain, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(20f, -62f), new Vector2(200f, 34f)).GetComponent<Text>();
        detailBodyText = CreateTextObject("DetailBody", sidePanel.transform, "No replacement cell is stored here. Running out of power in the maze leaves you exposed.", 17, FontStyle.Normal, textDim, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -108f), new Vector2(206f, 168f)).GetComponent<Text>();

        GameObject warningBar = CreateRect("WarningBar", sidePanel.transform, new Vector2(0.5f, 0f), new Vector2(210f, 34f), new Vector2(0f, 24f));
        Image warningImage = warningBar.AddComponent<Image>();
        warningImage.color = new Color(warningColor.r, warningColor.g, warningColor.b, 0.12f);
        CreateTextObject("WarningText", warningBar.transform, "LOW LIGHT = HIGH RISK", 13, FontStyle.Bold, warningColor, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 20f));

        footerText = CreateTextObject("Footer", panel.transform, "Recovered cells keep the flashlight alive. Do not waste them in safe corridors.", 15, FontStyle.Normal, textDim, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(46f, 34f), new Vector2(680f, 24f)).GetComponent<Text>();

        GameObject scan = CreateStretch("Scanlines", panel.transform);
        scanlineImage = scan.AddComponent<Image>();
        scanlineImage.color = new Color(1f, 1f, 1f, 0.035f);
        scanlineImage.material = null;
        CreateScanlineTexture(scanlineImage);

        GameObject noise = CreateStretch("StaticNoise", panel.transform);
        staticNoise = noise.AddComponent<Image>();
        staticNoise.color = new Color(1f, 1f, 1f, 0.045f);
        CreateNoiseTexture(staticNoise);
    }

    private void Refresh()
    {
        if (inventory == null) return;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, 5);

        for (int i = 0; i < slotButtons.Count; i++)
        {
            bool filled = i < inventory.StoredBatteries;
            bool selected = i == selectedIndex;

            slotBackgrounds[i].color = selected ? Color.Lerp(slotFilled, slotSelected, 0.18f) : filled ? slotFilled : slotIdle;
            slotBorders[i].color = selected ? slotSelected : new Color(panelEdge.r, panelEdge.g, panelEdge.b, filled ? 0.24f : 0.14f);
            slotIcons[i].color = filled ? batteryOn : batteryOff;
            slotStateTexts[i].text = filled ? "READY" : "EMPTY";
            slotStateTexts[i].color = selected ? textMain : filled ? new Color(0.78f, 0.76f, 0.60f, 0.82f) : textDim;
            slotIndexTexts[i].color = selected ? textMain : textDim;

            Image glowImage = slotGlows[i].GetComponent<Image>();
            glowImage.color = selected ? new Color(slotSelected.r, slotSelected.g, slotSelected.b, 0.11f) : new Color(slotSelected.r, slotSelected.g, slotSelected.b, 0f);
        }

        bool hasItem = selectedIndex < inventory.StoredBatteries;
        detailTitleText.text = hasItem ? "UTILITY CELL" : "EMPTY SLOT";
        detailBodyText.text = hasItem
            ? "A charged maintenance battery. Use it to restore flashlight power before the corridor swallows your visibility."
            : "No replacement cell is stored here. Running out of power in the maze leaves you exposed.";
        footerText.text = inventory.StoredBatteries > 0
            ? "Stored cells: " + inventory.StoredBatteries + " / " + inventory.MaxStoredBatteries
            : "No spare batteries stored. Search dead corridors, corners, and maintenance pockets.";
    }

    private void AnimateVisuals()
    {
        float t = Time.unscaledTime;
        if (selectedIndex >= 0 && selectedIndex < slotGlows.Count)
        {
            Image glowImage = slotGlows[selectedIndex].GetComponent<Image>();
            float pulse = 0.08f + Mathf.Abs(Mathf.Sin(t * selectedPulseSpeed)) * 0.08f;
            glowImage.color = new Color(slotSelected.r, slotSelected.g, slotSelected.b, pulse);
        }

        if (panelImage != null)
        {
            float drift = Mathf.PerlinNoise(t * 0.28f, 0.17f);
            panelImage.color = panelColor * (0.98f + drift * 0.04f);
            panelImage.color = new Color(panelImage.color.r, panelImage.color.g, panelImage.color.b, panelColor.a);
        }

        if (staticNoise != null)
        {
            staticNoise.rectTransform.anchoredPosition = new Vector2(
                Mathf.Sin(t * 1.3f) * 2f,
                Mathf.Cos(t * 1.7f) * 2f
            );
            staticNoise.color = new Color(1f, 1f, 1f, 0.03f + Mathf.PerlinNoise(t * noiseSpeed, 1.7f) * 0.02f);
        }

        if (scanlineImage != null)
        {
            scanlineImage.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Repeat(t * 18f, 64f));
        }
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private void AddEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private GameObject CreateStretch(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        return obj;
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

    private GameObject CreateTextObject(string name, Transform parent, string value, int fontSize, FontStyle style, Color color, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject obj = CreateRect(name, parent, anchor, size, pos);
        Text text = obj.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return obj;
    }

    private void CreateNoiseTexture(Image target)
    {
        noiseTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        noiseTexture.wrapMode = TextureWrapMode.Repeat;
        Color32[] pixels = new Color32[64 * 64];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)Random.Range(0, 36);
            pixels[i] = new Color32(v, v, v, v);
        }
        noiseTexture.SetPixels32(pixels);
        noiseTexture.Apply(false);
        Sprite sprite = Sprite.Create(noiseTexture, new Rect(0, 0, noiseTexture.width, noiseTexture.height), new Vector2(0.5f, 0.5f));
        target.sprite = sprite;
        target.type = Image.Type.Tiled;
    }

    private void CreateScanlineTexture(Image target)
    {
        Texture2D tex = new Texture2D(4, 64, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        Color32[] pixels = new Color32[4 * 64];
        for (int y = 0; y < 64; y++)
        {
            byte a = (byte)((y % 4 == 0) ? 32 : 0);
            for (int x = 0; x < 4; x++)
                pixels[y * 4 + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        target.sprite = sprite;
        target.type = Image.Type.Tiled;
    }

    private void OnDestroy()
    {
        RestorePausedInputScripts();
        Time.timeScale = 1f;
    }
}
