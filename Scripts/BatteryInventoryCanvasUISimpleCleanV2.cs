using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BatteryInventoryCanvasUISimpleCleanV2 : MonoBehaviour
{
    [SerializeField] private BatteryInventory inventory;
    [SerializeField] private Sprite batteryIcon;
    [SerializeField] private Key toggleInventoryKey = Key.Tab;
    [SerializeField] private Key useBatteryKey = Key.R;
    [SerializeField] private float fadeDuration = 0.14f;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private GameObject root;
    private Text descTitle;
    private Text descBody;
    private Text qtyText;
    private readonly List<Button> slotButtons = new List<Button>();
    private readonly List<Image> slotFrames = new List<Image>();
    private readonly List<Image> slotBackgrounds = new List<Image>();
    private readonly List<Image> itemIcons = new List<Image>();
    private readonly List<Text> qtyLabels = new List<Text>();
    private readonly List<BatteryInventoryDragItem> dragItems = new List<BatteryInventoryDragItem>();
    private BackroomsPlayer player;
    private int selectedIndex;
    private bool inventoryOpen;
    private bool transitionBusy;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;

    private readonly Color dimColor = new Color(0f, 0f, 0f, 0.42f);
    private readonly Color panelColor = new Color(0.02f, 0.02f, 0.02f, 0.84f);
    private readonly Color slotFrameIdle = new Color(1f, 1f, 1f, 0.10f);
    private readonly Color slotFrameHover = new Color(1f, 1f, 1f, 0.22f);
    private readonly Color slotFrameSelected = new Color(0.95f, 0.91f, 0.76f, 0.96f);
    private readonly Color slotBgIdle = new Color(0.10f, 0.10f, 0.10f, 0.92f);
    private readonly Color slotBgFilled = new Color(0.14f, 0.14f, 0.14f, 0.96f);

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<BatteryInventory>();
        if (inventory == null)
            inventory = FindFirstObjectByType<BatteryInventory>();

        player = FindFirstObjectByType<BackroomsPlayer>();
        BuildUI();
        root.SetActive(false);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        if (inventory != null) inventory.OnInventoryChanged += Refresh;
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
            TryUseSelectedBattery();

    }

    private IEnumerator OpenInventoryRoutine()
    {
        transitionBusy = true;
        inventoryOpen = true;
        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;

        if (player != null) player.SetInventoryPaused(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;

        root.SetActive(true);
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, slotButtons.Count - 1);
        EventSystem.current?.SetSelectedGameObject(slotButtons[selectedIndex].gameObject);
        Refresh();

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
        if (player != null) player.SetInventoryPaused(false);
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
        int next = selectedIndex;
        if (vertical)
        {
            int row = selectedIndex / 3;
            int col = selectedIndex % 3;
            row = Mathf.Clamp(row + (delta < 0 ? -1 : 1), 0, 1);
            next = row * 3 + col;
        }
        else
        {
            int row = selectedIndex / 3;
            int col = Mathf.Clamp((selectedIndex % 3) + (delta < 0 ? -1 : 1), 0, 2);
            next = row * 3 + col;
        }

        selectedIndex = Mathf.Clamp(next, 0, slotButtons.Count - 1);
        EventSystem.current?.SetSelectedGameObject(slotButtons[selectedIndex].gameObject);
        Refresh();
    }

    private void TryUseSelectedBattery()
    {
        if (inventory.TryUseBatteryFromSlot(selectedIndex))
        {
            if (selectedIndex >= inventory.SlotCount)
                selectedIndex = Mathf.Max(0, inventory.SlotCount - 1);
        }
    }

    public void HandleDrop(int fromIndex, int toIndex)
    {
        if (inventory == null) return;
        inventory.SwapSlots(fromIndex, toIndex);
        selectedIndex = toIndex;
        EventSystem.current?.SetSelectedGameObject(slotButtons[selectedIndex].gameObject);
        Refresh();
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        root = new GameObject("SimpleCleanBatteryInventoryCanvasV2");
        root.transform.SetParent(transform, false);
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGroup = root.AddComponent<CanvasGroup>();
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        GameObject dim = CreateStretchedUIObject("Dim", root.transform);
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = dimColor;

        GameObject panel = CreateUIObject("Panel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(620f, 540f));
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchoredPosition = Vector2.zero;
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;

        Text titleText = CreateText("Title", panel.transform, "Inventory", new Vector2(0f, 1f), new Vector2(36f, -36f), new Vector2(280f, 40f), 30, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text infoText = CreateText("Info", panel.transform, "Arrow keys or mouse to select   •   Enter or R to use", new Vector2(0f, 1f), new Vector2(36f, -74f), new Vector2(500f, 28f), 16, TextAnchor.MiddleLeft, FontStyle.Normal);
        infoText.color = new Color(1f, 1f, 1f, 0.58f);
        AddDivider(panel.transform, new Vector2(36f, -102f), new Vector2(548f, 1f), 0.12f);

        GameObject grid = CreateUIObject("Grid", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(432f, 292f));
        RectTransform gridRT = grid.GetComponent<RectTransform>();
        gridRT.anchoredPosition = new Vector2(0f, 24f);
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(128f, 128f);
        layout.spacing = new Vector2(24f, 24f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        for (int i = 0; i < 6; i++)
        {
            GameObject outer = CreateUIObject("Slot_" + i, grid.transform, new Vector2(0.5f, 0.5f), new Vector2(128f, 128f));
            Image frame = outer.AddComponent<Image>();
            frame.color = slotFrameIdle;
            slotFrames.Add(frame);

            Button button = outer.AddComponent<Button>();
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.Explicit;
            button.navigation = nav;
            int index = i;
            button.onClick.AddListener(() => { selectedIndex = index; TryUseSelectedBattery(); Refresh(); });
            EventTrigger trigger = outer.AddComponent<EventTrigger>();
            AddEvent(trigger, EventTriggerType.PointerEnter, () => { selectedIndex = index; EventSystem.current?.SetSelectedGameObject(slotButtons[index].gameObject); Refresh(); });
            slotButtons.Add(button);

            GameObject inner = CreateUIObject("Inner", outer.transform, new Vector2(0.5f, 0.5f), new Vector2(120f, 120f));
            Image innerImage = inner.AddComponent<Image>();
            innerImage.color = slotBgIdle;
            slotBackgrounds.Add(innerImage);

            GameObject icon = CreateUIObject("ItemIcon", inner.transform, new Vector2(0.5f, 0.5f), new Vector2(56f, 56f));
            Image iconImage = icon.AddComponent<Image>();
            iconImage.preserveAspect = true;
            BatteryInventoryDragItem dragItem = icon.AddComponent<BatteryInventoryDragItem>();
            iconImage.enabled = false;
            itemIcons.Add(iconImage);

            Text qty = CreateText("Qty", inner.transform, "", new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(40f, 24f), 18, TextAnchor.UpperRight, FontStyle.Normal);
            qty.color = new Color(1f, 1f, 1f, 0.68f);
            qtyLabels.Add(qty);
        }

        AddDivider(panel.transform, new Vector2(36f, -420f), new Vector2(548f, 1f), 0.12f);
        descTitle = CreateText("DescTitle", panel.transform, "Empty Slot", new Vector2(0f, 1f), new Vector2(36f, -456f), new Vector2(280f, 36f), 24, TextAnchor.MiddleLeft, FontStyle.Bold);
        qtyText = CreateText("Qty", panel.transform, "x0", new Vector2(1f, 1f), new Vector2(-36f, -456f), new Vector2(80f, 36f), 24, TextAnchor.MiddleRight, FontStyle.Bold);
        qtyText.color = new Color(1f, 1f, 1f, 0.76f);
        descBody = CreateText("DescBody", panel.transform, "No item stored in this slot.", new Vector2(0f, 1f), new Vector2(36f, -496f), new Vector2(520f, 64f), 18, TextAnchor.UpperLeft, FontStyle.Normal);
        descBody.color = new Color(1f, 1f, 1f, 0.72f);
    }

    private void Refresh()
    {
        if (inventory == null) return;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, 5);

        for (int i = 0; i < slotFrames.Count; i++)
        {
            bool filled = inventory.HasBatteryInSlot(i);
            bool selected = i == selectedIndex;
            bool hovered = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == slotButtons[i].gameObject;
            slotFrames[i].color = selected ? slotFrameSelected : hovered ? slotFrameHover : slotFrameIdle;
            slotBackgrounds[i].color = filled ? slotBgFilled : slotBgIdle;
            qtyLabels[i].text = filled ? "x1" : "";

            if (filled && batteryIcon != null)
            {
                itemIcons[i].enabled = true;
                itemIcons[i].sprite = batteryIcon;
                itemIcons[i].color = Color.white;
            }
            else
            {
                itemIcons[i].enabled = false;
                itemIcons[i].sprite = null;
            }
        }

        bool hasItem = selectedIndex < inventory.StoredBatteries;
        descTitle.text = hasItem ? "Battery" : "Empty Slot";
        descBody.text = hasItem ? "Restores your flashlight so you do not get stranded in the dark." : "No item stored in this slot.";
        qtyText.text = hasItem ? "x1" : "x0";
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
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener((_) => action());
        trigger.triggers.Add(entry);
    }

    private GameObject CreateStretchedUIObject(string name, Transform parent)
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

    private void AddDivider(Transform parent, Vector2 pos, Vector2 size, float alpha)
    {
        GameObject line = CreateUIObject("Divider", parent, new Vector2(0f, 1f), size);
        RectTransform rt = line.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        Image image = line.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, alpha);
    }

    private GameObject CreateUIObject(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x == 0f ? 0f : anchor.x == 1f ? 1f : 0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        return obj;
    }

    private Text CreateText(string name, Transform parent, string value, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, FontStyle style)
    {
        GameObject obj = CreateUIObject(name, parent, anchor, size);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        Text text = obj.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = align;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= Refresh;
        Time.timeScale = 1f;
    }
}