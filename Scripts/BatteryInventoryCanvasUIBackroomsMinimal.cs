using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BatteryInventoryCanvasUIBackroomsMinimal : MonoBehaviour
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
    [SerializeField] private float fadeDuration = 0.12f;

    [Header("Style")]
    [SerializeField] private Color overlayColor = new Color(0.015f, 0.015f, 0.015f, 0.54f);
    [SerializeField] private Color frameColor = new Color(0.75f, 0.77f, 0.75f, 0.10f);
    [SerializeField] private Color panelColor = new Color(0.06f, 0.07f, 0.07f, 0.26f);
    [SerializeField] private Color slotColor = new Color(0.03f, 0.035f, 0.035f, 0.78f);
    [SerializeField] private Color slotSelected = new Color(0.82f, 0.84f, 0.80f, 0.18f);
    [SerializeField] private Color borderColor = new Color(0.88f, 0.90f, 0.88f, 0.25f);
    [SerializeField] private Color borderSelected = new Color(0.92f, 0.95f, 0.92f, 0.62f);
    [SerializeField] private Color titleColor = new Color(0.93f, 0.94f, 0.92f, 0.96f);
    [SerializeField] private Color textColor = new Color(0.82f, 0.84f, 0.82f, 0.88f);
    [SerializeField] private Color dimColor = new Color(0.70f, 0.72f, 0.70f, 0.70f);
    [SerializeField] private Color batteryOnColor = new Color(0.82f, 0.86f, 0.78f, 0.95f);
    [SerializeField] private Color batteryOffColor = new Color(0.12f, 0.14f, 0.14f, 0.92f);

    private Canvas canvas;
    private CanvasScaler scaler;
    private GraphicRaycaster raycaster;
    private CanvasGroup canvasGroup;
    private GameObject root;
    private Text headerText;
    private Text helperText;
    private Text footerText;
    private Text detailTitleText;
    private Text detailBodyText;
    private readonly List<Button> slotButtons = new();
    private readonly List<Image> slotBorders = new();
    private readonly List<Image> slotPanels = new();
    private readonly List<Image> slotIcons = new();
    private readonly List<Text> slotNumbers = new();
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
        selectedIndex = Mathf.Clamp(row * columns + col, 0, slotCount - 1);
        EventSystem.current?.SetSelectedGameObject(slotButtons[selectedIndex].gameObject);
        Refresh();
    }

    private void MoveVertical(int direction)
    {
        int row = selectedIndex / columns;
        int col = selectedIndex % columns;
        int maxRow = Mathf.CeilToInt(slotCount / (float)columns) - 1;
        row = Mathf.Clamp(row + direction, 0, maxRow);
        selectedIndex = Mathf.Clamp(row * columns + col, 0, slotCount - 1);
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
        root = new GameObject("BackroomsBatteryInventoryMinimal");
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        raycaster = root.AddComponent<GraphicRaycaster>();
        canvasGroup = root.AddComponent<CanvasGroup>();

        CreateStretchImage("Overlay", root.transform, overlayColor);

        headerText = CreateText("Header", root.transform, "Inventory", 34, FontStyle.Bold, titleColor, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-42f, -34f), new Vector2(320f, 38f));
        helperText = CreateText("Helper", root.transform, "TAB close   ARROWS move   ENTER / R equip", 14, FontStyle.Normal, dimColor, TextAnchor.MiddleRight, new Vector2(1f, 0f), new Vector2(-42f, 32f), new Vector2(420f, 20f));

        GameObject leftColumn = CreateRect("LeftColumn", root.transform, new Vector2(0f, 0.5f), new Vector2(240f, 520f), new Vector2(186f, 0f));
        leftColumn.AddComponent<Image>().color = frameColor;
        CreateRectImage("LeftInner", leftColumn.transform, new Vector2(0.5f, 0.5f), new Vector2(224f, 504f), Vector2.zero, panelColor);
        CreateLabelPlate(leftColumn.transform, "HOTBAR", new Vector2(0f, 1f), new Vector2(16f, -18f), new Vector2(190f, 34f));
        CreateLabelPlate(leftColumn.transform, "MAIN HAND", new Vector2(0f, 1f), new Vector2(16f, -128f), new Vector2(190f, 34f));
        CreateRectImage("MainHandSlot", leftColumn.transform, new Vector2(0.5f, 1f), new Vector2(110f, 110f), new Vector2(0f, -218f), slotColor);
        CreateLabelPlate(leftColumn.transform, "POCKETS", new Vector2(0f, 0f), new Vector2(16f, 120f), new Vector2(190f, 34f));
        CreateRectImage("PocketA", leftColumn.transform, new Vector2(0.5f, 0f), new Vector2(110f, 110f), new Vector2(0f, 98f), slotColor);
        CreateRectImage("PocketB", leftColumn.transform, new Vector2(0.5f, 0f), new Vector2(110f, 110f), new Vector2(0f, 220f), slotColor);

        GameObject centerPanel = CreateRect("CenterPanel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(430f, 430f), new Vector2(10f, 8f));
        centerPanel.AddComponent<Image>().color = new Color(frameColor.r, frameColor.g, frameColor.b, 0.08f);
        CreateLabelPlate(centerPanel.transform, "BACKPACK", new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(180f, 36f));

        GameObject grid = CreateRect("Grid", centerPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(372f, 372f), new Vector2(0f, 18f));
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(114f, 114f);
        layout.spacing = new Vector2(12f, 12f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slot = CreateRect("Slot_" + i, grid.transform, new Vector2(0.5f, 0.5f), new Vector2(114f, 114f), Vector2.zero);
            Image border = slot.AddComponent<Image>();
            border.color = borderColor;
            slotBorders.Add(border);

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
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => { selectedIndex = captured; Refresh(); });
            trigger.triggers.Add(entry);

            GameObject inner = CreateRect("Inner", slot.transform, new Vector2(0.5f, 0.5f), new Vector2(106f, 106f), Vector2.zero);
            Image panel = inner.AddComponent<Image>();
            panel.color = slotColor;
            slotPanels.Add(panel);

            Image icon = CreateRectImage("Icon", inner.transform, new Vector2(0.5f, 0.5f), new Vector2(42f, 74f), new Vector2(0f, -2f), batteryOffColor);
            if (batteryIcon != null)
            {
                icon.sprite = batteryIcon;
                icon.preserveAspect = true;
            }
            slotIcons.Add(icon);

            Text num = CreateText("Number", inner.transform, (i + 1).ToString("00"), 13, FontStyle.Bold, dimColor, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(8f, -10f), new Vector2(34f, 16f));
            slotNumbers.Add(num);
        }

        GameObject detailPanel = CreateRect("DetailPanel", root.transform, new Vector2(1f, 0.5f), new Vector2(250f, 430f), new Vector2(-170f, 8f));
        detailPanel.AddComponent<Image>().color = frameColor;
        CreateRectImage("DetailInner", detailPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(236f, 416f), Vector2.zero, panelColor);
        GameObject preview = CreateRect("Preview", detailPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(180f, 280f), new Vector2(0f, 18f));
        preview.AddComponent<Image>().color = new Color(0.07f, 0.10f, 0.11f, 0.55f);
        detailTitleText = CreateText("DetailTitle", detailPanel.transform, "EMPTY SLOT", 24, FontStyle.Bold, titleColor, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(200f, 28f));
        detailBodyText = CreateText("DetailBody", detailPanel.transform, "No cell stored.", 15, FontStyle.Normal, textColor, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(210f, 42f));

        footerText = CreateText("Footer", root.transform, "Drag / Equip", 15, FontStyle.Normal, textColor, TextAnchor.MiddleRight, new Vector2(1f, 0f), new Vector2(-42f, 90f), new Vector2(180f, 20f));
    }

    private void CreateLabelPlate(Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject plate = CreateRect("Label_" + text, parent, anchor, size, pos);
        plate.AddComponent<Image>().color = new Color(0.88f, 0.90f, 0.88f, 0.14f);
        CreateText("Text", plate.transform, text, 14, FontStyle.Bold, titleColor, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(10f, 10f));
    }

    private void Refresh()
    {
        if (inventory == null) return;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, slotCount - 1);
        int stored = inventory.StoredBatteries;

        for (int i = 0; i < slotCount; i++)
        {
            bool selected = i == selectedIndex;
            bool filled = i < stored;
            slotBorders[i].color = selected ? borderSelected : borderColor;
            slotPanels[i].color = selected ? slotSelected : slotColor;
            slotIcons[i].color = filled ? batteryOnColor : batteryOffColor;
            slotNumbers[i].color = selected ? titleColor : dimColor;
        }

        bool hasCell = selectedIndex < stored;
        detailTitleText.text = hasCell ? "UTILITY CELL" : "EMPTY SLOT";
        detailBodyText.text = hasCell ? "Charged replacement battery." : "No cell stored.";
        footerText.text = stored > 0 ? "Stored cells: " + stored + " / " + inventory.MaxStoredBatteries : "No spare cells";
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject obj = new GameObject("EventSystem");
        obj.AddComponent<EventSystem>();
        obj.AddComponent<StandaloneInputModule>();
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
