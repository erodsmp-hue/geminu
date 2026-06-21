using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BatteryInventoryCanvasUIBackroomsHorrorV2 : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BatteryInventory inventory;
    [SerializeField] private Sprite batteryIcon;

    [Header("Input")]
    [SerializeField] private Key toggleInventoryKey = Key.Tab;
    [SerializeField] private Key useBatteryKey = Key.R;

    [Header("Layout")]
    [SerializeField] private int slotCount = 6;
    [SerializeField] private int columns = 3;
    [SerializeField] private float fadeDuration = 0.14f;

    [Header("Palette")]
    [SerializeField] private Color overlayColor = new Color(0.02f, 0.02f, 0.018f, 0.82f);
    [SerializeField] private Color panelColor = new Color(0.075f, 0.073f, 0.060f, 0.94f);
    [SerializeField] private Color panelInnerColor = new Color(0.095f, 0.092f, 0.076f, 0.96f);
    [SerializeField] private Color borderColor = new Color(0.62f, 0.58f, 0.44f, 0.22f);
    [SerializeField] private Color dividerColor = new Color(0.60f, 0.55f, 0.40f, 0.11f);
    [SerializeField] private Color titleColor = new Color(0.84f, 0.81f, 0.68f, 0.96f);
    [SerializeField] private Color textColor = new Color(0.78f, 0.76f, 0.67f, 0.92f);
    [SerializeField] private Color mutedTextColor = new Color(0.62f, 0.60f, 0.53f, 0.78f);
    [SerializeField] private Color slotColor = new Color(0.10f, 0.10f, 0.085f, 0.92f);
    [SerializeField] private Color slotFilledColor = new Color(0.14f, 0.13f, 0.10f, 0.94f);
    [SerializeField] private Color selectedBorderColor = new Color(0.83f, 0.78f, 0.56f, 0.92f);
    [SerializeField] private Color selectedGlowColor = new Color(0.84f, 0.80f, 0.58f, 0.12f);
    [SerializeField] private Color batteryOnColor = new Color(0.86f, 0.80f, 0.52f, 0.98f);
    [SerializeField] private Color batteryOffColor = new Color(0.20f, 0.20f, 0.18f, 0.90f);
    [SerializeField] private Color alertColor = new Color(0.78f, 0.26f, 0.16f, 0.90f);

    private Canvas canvas;
    private CanvasScaler scaler;
    private GraphicRaycaster raycaster;
    private CanvasGroup canvasGroup;
    private GameObject root;
    private RectTransform panelRT;
    private Text helperText;
    private Text statusText;
    private Text detailTitleText;
    private Text detailBodyText;
    private Text detailMetaText;
    private readonly List<Button> slotButtons = new();
    private readonly List<Image> slotOuterBorders = new();
    private readonly List<Image> slotInnerPanels = new();
    private readonly List<Image> slotGlowImages = new();
    private readonly List<Text> slotLabels = new();
    private readonly List<Text> slotStates = new();
    private readonly List<Image> slotBatteryBodies = new();
    private readonly List<Image> slotBatteryCaps = new();
    private readonly List<Image> slotBatteryFills = new();
    private readonly List<Image> slotBatteryGlass = new();
    private readonly List<Behaviour> pausedInputScripts = new();

    private bool inventoryOpen;
    private bool transitionBusy;
    private int selectedIndex;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<BatteryInventory>();
        if (inventory == null) inventory = FindFirstObjectByType<BatteryInventory>();

        EnsureEventSystem();
        BuildUI();
        SetVisibleImmediate(false);
        Refresh();
    }

    private void Update()
    {
        if (Keyboard.current == null || inventory == null) return;

        if (Keyboard.current[toggleInventoryKey].wasPressedThisFrame && !transitionBusy)
        {
            if (inventoryOpen) StartCoroutine(CloseRoutine());
            else StartCoroutine(OpenRoutine());
        }

        if (!inventoryOpen || transitionBusy) return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame) MoveHorizontal(-1);
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame) MoveHorizontal(1);
        if (Keyboard.current.upArrowKey.wasPressedThisFrame) MoveVertical(-1);
        if (Keyboard.current.downArrowKey.wasPressedThisFrame) MoveVertical(1);

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current[useBatteryKey].wasPressedThisFrame)
            TryUseSelectedBattery();

        AnimateUI();
    }

    private IEnumerator OpenRoutine()
    {
        transitionBusy = true;
        inventoryOpen = true;

        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;

        PauseLikelyInputScripts();
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        root.SetActive(true);
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        yield return FadeCanvas(0f, 1f);
        EventSystem.current?.SetSelectedGameObject(slotButtons[Mathf.Clamp(selectedIndex, 0, slotButtons.Count - 1)].gameObject);
        transitionBusy = false;
    }

    private IEnumerator CloseRoutine()
    {
        transitionBusy = true;
        yield return FadeCanvas(1f, 0f);
        SetVisibleImmediate(false);

        RestorePausedInputScripts();
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLock;

        inventoryOpen = false;
        transitionBusy = false;
    }

    private IEnumerator FadeCanvas(float from, float to)
    {
        float elapsed = 0f;
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

    private void SetVisibleImmediate(bool visible)
    {
        root.SetActive(visible);
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    private void PauseLikelyInputScripts()
    {
        pausedInputScripts.Clear();
        Behaviour[] behaviours = FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour == null || !behaviour.enabled || behaviour == this) continue;
            string name = behaviour.GetType().Name.ToLowerInvariant();
            bool likelyInput = name.Contains("player") || name.Contains("input") || name.Contains("look") || name.Contains("controller") || name.Contains("mouse");
            if (!likelyInput) continue;
            if (behaviour is EventSystem || behaviour is StandaloneInputModule || behaviour is GraphicRaycaster || behaviour is Canvas || behaviour is CanvasScaler || behaviour is CanvasGroup)
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

    private void MoveHorizontal(int direction)
    {
        int col = selectedIndex % columns;
        int row = selectedIndex / columns;
        col = Mathf.Clamp(col + direction, 0, columns - 1);
        int next = row * columns + col;
        selectedIndex = Mathf.Clamp(next, 0, slotCount - 1);
        EventSystem.current?.SetSelectedGameObject(slotButtons[selectedIndex].gameObject);
        Refresh();
    }

    private void MoveVertical(int direction)
    {
        int row = selectedIndex / columns;
        int col = selectedIndex % columns;
        int maxRow = Mathf.CeilToInt(slotCount / (float)columns) - 1;
        row = Mathf.Clamp(row + direction, 0, maxRow);
        int next = row * columns + col;
        selectedIndex = Mathf.Clamp(next, 0, slotCount - 1);
        EventSystem.current?.SetSelectedGameObject(slotButtons[selectedIndex].gameObject);
        Refresh();
    }

    private void TryUseSelectedBattery()
    {
        if (inventory == null) return;
        if (selectedIndex >= inventory.StoredBatteries) return;
        inventory.TryUseStoredBattery();
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, inventory.StoredBatteries - 1));
        Refresh();
    }

    private void BuildUI()
    {
        root = new GameObject("BackroomsBatteryInventoryUI");
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        raycaster = root.AddComponent<GraphicRaycaster>();
        canvasGroup = root.AddComponent<CanvasGroup>();

        Image overlay = CreateStretchImage("Overlay", root.transform, overlayColor);

        GameObject panel = CreateRect("Panel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(930f, 520f), Vector2.zero);
        panelRT = panel.GetComponent<RectTransform>();
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;
        Shadow panelShadow = panel.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.58f);
        panelShadow.effectDistance = new Vector2(22f, -22f);

        GameObject inner = CreateRect("InnerFrame", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 490f), Vector2.zero);
        inner.AddComponent<Image>().color = panelInnerColor;
        Outline innerOutline = inner.AddComponent<Outline>();
        innerOutline.effectColor = borderColor;
        innerOutline.effectDistance = new Vector2(1f, -1f);

        CreateRectImage("TopDivider", inner.transform, new Vector2(0.5f, 1f), new Vector2(820f, 1f), new Vector2(0f, -84f), dividerColor);
        CreateRectImage("BottomDivider", inner.transform, new Vector2(0.5f, 0f), new Vector2(820f, 1f), new Vector2(0f, 70f), dividerColor);
        CreateRectImage("CenterDivider", inner.transform, new Vector2(0.67f, 0.5f), new Vector2(1f, 300f), new Vector2(0f, -6f), dividerColor);

        Text title = CreateText("Title", inner.transform, "BATTERY CACHE", 34, FontStyle.Bold, titleColor, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(42f, -42f), new Vector2(340f, 34f));
        CreateText("Subtitle", inner.transform, "Recovered utility cells / maintenance stock", 16, FontStyle.Normal, mutedTextColor, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(42f, -74f), new Vector2(360f, 20f));
        helperText = CreateText("Helper", inner.transform, "TAB CLOSE   ARROWS MOVE   ENTER / R USE", 14, FontStyle.Normal, mutedTextColor, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-40f, -50f), new Vector2(360f, 20f));
        CreateText("StorageLabel", inner.transform, "STORAGE GRID", 14, FontStyle.Bold, mutedTextColor, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(42f, -112f), new Vector2(160f, 18f));
        statusText = CreateText("StatusText", inner.transform, "", 15, FontStyle.Normal, mutedTextColor, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(42f, 42f), new Vector2(500f, 20f));

        GameObject grid = CreateRect("Grid", inner.transform, new Vector2(0f, 1f), new Vector2(540f, 286f), new Vector2(42f, -278f));
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(164f, 130f);
        layout.spacing = new Vector2(18f, 18f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slot = CreateRect("Slot_" + i, grid.transform, new Vector2(0.5f, 0.5f), new Vector2(164f, 130f), Vector2.zero);
            Image slotOuter = slot.AddComponent<Image>();
            slotOuter.color = borderColor;
            slotOuterBorders.Add(slotOuter);

            Button button = slot.AddComponent<Button>();
            int captured = i;
            button.onClick.AddListener(() =>
            {
                selectedIndex = captured;
                EventSystem.current?.SetSelectedGameObject(button.gameObject);
                Refresh();
            });
            slotButtons.Add(button);

            EventTrigger trigger = slot.AddComponent<EventTrigger>();
            AddHover(trigger, captured);

            GameObject slotInner = CreateRect("SlotInner", slot.transform, new Vector2(0.5f, 0.5f), new Vector2(156f, 122f), Vector2.zero);
            Image slotInnerImage = slotInner.AddComponent<Image>();
            slotInnerImage.color = slotColor;
            slotInnerPanels.Add(slotInnerImage);

            Image slotGlow = CreateStretchImage("SlotGlow", slotInner.transform, new Color(selectedGlowColor.r, selectedGlowColor.g, selectedGlowColor.b, 0f));
            slotGlowImages.Add(slotGlow);

            CreateText("SlotNumber", slotInner.transform, (i + 1).ToString("00"), 14, FontStyle.Bold, mutedTextColor, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(12f, -14f), new Vector2(40f, 16f));

            GameObject batteryRoot = CreateRect("BatteryRoot", slotInner.transform, new Vector2(0.5f, 0.5f), new Vector2(44f, 78f), new Vector2(0f, -2f));
            Image batteryBody = batteryRoot.AddComponent<Image>();
            batteryBody.color = new Color(0.05f, 0.05f, 0.045f, 1f);
            slotBatteryBodies.Add(batteryBody);

            GameObject fillRoot = CreateRect("BatteryFillMask", batteryRoot.transform, new Vector2(0.5f, 0f), new Vector2(28f, 52f), new Vector2(0f, 8f));
            Image fillBg = fillRoot.AddComponent<Image>();
            fillBg.color = new Color(0.09f, 0.11f, 0.08f, 1f);

            GameObject fill = CreateRect("BatteryFill", fillRoot.transform, new Vector2(0.5f, 0f), new Vector2(26f, 36f), new Vector2(0f, 2f));
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = batteryOnColor;
            slotBatteryFills.Add(fillImage);

            GameObject glass = CreateRect("BatteryGlass", batteryRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(36f, 66f), Vector2.zero);
            Image glassImage = glass.AddComponent<Image>();
            glassImage.color = new Color(1f, 1f, 1f, 0.05f);
            slotBatteryGlass.Add(glassImage);

            GameObject cap = CreateRect("BatteryCap", batteryRoot.transform, new Vector2(0.5f, 1f), new Vector2(16f, 8f), new Vector2(0f, -2f));
            Image capImage = cap.AddComponent<Image>();
            capImage.color = new Color(0.88f, 0.84f, 0.70f, 0.85f);
            slotBatteryCaps.Add(capImage);

            Text slotLabel = CreateText("SlotLabel", slotInner.transform, "CELL", 13, FontStyle.Bold, textColor, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(90f, 16f));
            slotLabels.Add(slotLabel);

            Text slotState = CreateText("SlotState", slotInner.transform, "EMPTY", 12, FontStyle.Bold, mutedTextColor, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(80f, 16f));
            slotStates.Add(slotState);
        }

        GameObject detailPanel = CreateRect("DetailPanel", inner.transform, new Vector2(1f, 0.5f), new Vector2(270f, 286f), new Vector2(-40f, -6f));
        Image detailBg = detailPanel.AddComponent<Image>();
        detailBg.color = new Color(0.07f, 0.07f, 0.058f, 0.96f);
        Outline detailOutline = detailPanel.AddComponent<Outline>();
        detailOutline.effectColor = new Color(borderColor.r, borderColor.g, borderColor.b, 0.35f);
        detailOutline.effectDistance = new Vector2(1f, -1f);

        CreateText("DetailHeader", detailPanel.transform, "SELECTED SLOT", 14, FontStyle.Bold, mutedTextColor, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(18f, -20f), new Vector2(160f, 16f));
        detailTitleText = CreateText("DetailTitle", detailPanel.transform, "EMPTY SLOT", 28, FontStyle.Bold, titleColor, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(18f, -60f), new Vector2(200f, 32f));
        detailMetaText = CreateText("DetailMeta", detailPanel.transform, "STATUS: NONE", 13, FontStyle.Bold, mutedTextColor, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(18f, -96f), new Vector2(180f, 16f));
        detailBodyText = CreateText("DetailBody", detailPanel.transform, "No replacement cell is stored here. Search maintenance pockets, side corridors, and dead ends.", 17, FontStyle.Normal, textColor, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(18f, -126f), new Vector2(228f, 108f));

        GameObject warning = CreateRect("Warning", detailPanel.transform, new Vector2(0.5f, 0f), new Vector2(226f, 34f), new Vector2(0f, 18f));
        warning.AddComponent<Image>().color = new Color(alertColor.r, alertColor.g, alertColor.b, 0.14f);
        CreateText("WarningText", warning.transform, "LOW LIGHT = HIGH RISK", 13, FontStyle.Bold, alertColor, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210f, 16f));
    }

    private void Refresh()
    {
        if (inventory == null) return;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, slotCount - 1);

        int stored = inventory.StoredBatteries;
        for (int i = 0; i < slotCount; i++)
        {
            bool filled = i < stored;
            bool selected = i == selectedIndex;

            slotOuterBorders[i].color = selected ? selectedBorderColor : new Color(borderColor.r, borderColor.g, borderColor.b, filled ? 0.24f : 0.12f);
            slotInnerPanels[i].color = selected ? Color.Lerp(slotFilledColor, selectedBorderColor, 0.10f) : filled ? slotFilledColor : slotColor;
            slotGlowImages[i].color = selected ? selectedGlowColor : new Color(selectedGlowColor.r, selectedGlowColor.g, selectedGlowColor.b, 0f);
            slotBatteryBodies[i].color = filled ? new Color(0.07f, 0.07f, 0.058f, 1f) : new Color(0.05f, 0.05f, 0.045f, 1f);
            slotBatteryCaps[i].color = filled ? new Color(0.88f, 0.84f, 0.70f, 0.90f) : new Color(0.50f, 0.48f, 0.42f, 0.55f);
            slotBatteryGlass[i].color = filled ? new Color(1f, 1f, 1f, 0.06f) : new Color(1f, 1f, 1f, 0.02f);
            slotBatteryFills[i].color = filled ? batteryOnColor : batteryOffColor;
            slotBatteryFills[i].rectTransform.sizeDelta = new Vector2(26f, filled ? 36f : 8f);

            slotLabels[i].text = filled ? "UTILITY CELL" : "NO CELL";
            slotLabels[i].color = selected ? titleColor : textColor;

            slotStates[i].text = filled ? "READY" : "EMPTY";
            slotStates[i].color = selected ? titleColor : filled ? new Color(0.76f, 0.74f, 0.64f, 0.82f) : mutedTextColor;
        }

        bool hasCell = selectedIndex < stored;
        detailTitleText.text = hasCell ? "UTILITY CELL" : "EMPTY SLOT";
        detailMetaText.text = hasCell ? "STATUS: CHARGED" : "STATUS: NONE";
        detailBodyText.text = hasCell
            ? "Recovered flashlight battery. Use it when your beam drops and the maze starts swallowing the walls around you."
            : "No replacement cell is stored here. Search maintenance pockets, side corridors, and dead ends.";

        statusText.text = stored > 0
            ? "Stored cells: " + stored + " / " + inventory.MaxStoredBatteries
            : "No spare batteries stored. Do not overuse the flashlight in open corridors.";
    }

    private void AnimateUI()
    {
        float t = Time.unscaledTime;
        if (selectedIndex >= 0 && selectedIndex < slotGlowImages.Count)
        {
            float a = 0.06f + Mathf.Abs(Mathf.Sin(t * 2.5f)) * 0.08f;
            slotGlowImages[selectedIndex].color = new Color(selectedGlowColor.r, selectedGlowColor.g, selectedGlowColor.b, a);
        }

        if (panelRT != null)
            panelRT.anchoredPosition = new Vector2(Mathf.Sin(t * 0.9f) * 0.7f, Mathf.Cos(t * 0.7f) * 0.5f);
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject obj = new GameObject("EventSystem");
        obj.AddComponent<EventSystem>();
        obj.AddComponent<StandaloneInputModule>();
    }

    private void AddHover(EventTrigger trigger, int index)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener(_ =>
        {
            selectedIndex = index;
            Refresh();
        });
        trigger.triggers.Add(entry);
    }

    private Image CreateStretchImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image image = obj.AddComponent<Image>();
        image.color = color;
        return image;
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

    private void OnDestroy()
    {
        RestorePausedInputScripts();
        if (!inventoryOpen) return;
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
