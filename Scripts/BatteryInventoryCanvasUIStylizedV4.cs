using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BatteryInventoryCanvasUIStylizedV4 : MonoBehaviour
{
    [SerializeField] private BatteryInventory inventory;
    [SerializeField] private Key toggleInventoryKey = Key.Tab;
    [SerializeField] private Key useBatteryKey = Key.R;

    private Canvas canvas;
    private GameObject root;
    private GameObject panel;
    private RectTransform dimRect;
    private Text descTitle;
    private Text descBody;
    private Text qtyText;
    private readonly List<Button> slotButtons = new List<Button>();
    private readonly List<Image> slotFrames = new List<Image>();
    private readonly List<Image> slotBackgrounds = new List<Image>();
    private readonly List<Image> batteryBodies = new List<Image>();
    private readonly List<Text> qtyLabels = new List<Text>();
    private int selectedIndex;
    private bool inventoryOpen;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private readonly List<Behaviour> disabledLookScripts = new List<Behaviour>();

    private readonly Color frameIdle = new Color(1f, 1f, 1f, 0.08f);
    private readonly Color frameHover = new Color(0.95f, 0.90f, 0.72f, 0.42f);
    private readonly Color frameSelected = new Color(1f, 0.96f, 0.82f, 0.95f);
    private readonly Color slotIdle = new Color(0.10f, 0.10f, 0.10f, 0.94f);
    private readonly Color slotFilled = new Color(0.16f, 0.15f, 0.12f, 0.98f);
    private readonly Color batteryOn = new Color(0.88f, 0.79f, 0.50f, 1f);
    private readonly Color batteryOff = new Color(0.18f, 0.18f, 0.18f, 1f);

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<BatteryInventory>();
        if (inventory == null)
            inventory = FindFirstObjectByType<BatteryInventory>();

        BuildUI();
        SetInventoryOpen(false, true);
        Refresh();
    }

    private void Update()
    {
        if (Keyboard.current == null || inventory == null) return;

        if (Keyboard.current[toggleInventoryKey].wasPressedThisFrame)
            SetInventoryOpen(!inventoryOpen, false);

        if (!inventoryOpen) return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame) MoveSelection(-1, false);
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame) MoveSelection(1, false);
        if (Keyboard.current.upArrowKey.wasPressedThisFrame) MoveSelection(-3, true);
        if (Keyboard.current.downArrowKey.wasPressedThisFrame) MoveSelection(3, true);

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current[useBatteryKey].wasPressedThisFrame)
            TryUseSelectedBattery();

        Refresh();
    }

    private void MoveSelection(int delta, bool vertical)
    {
        int next = selectedIndex + delta;
        if (vertical)
            next = Mathf.Clamp(next, 0, 5);
        else
        {
            int row = selectedIndex / 3;
            int col = Mathf.Clamp((selectedIndex % 3) + delta, 0, 2);
            next = row * 3 + col;
        }

        selectedIndex = Mathf.Clamp(next, 0, 5);
        EventSystem.current?.SetSelectedGameObject(slotButtons[selectedIndex].gameObject);
    }

    private void TryUseSelectedBattery()
    {
        if (selectedIndex < inventory.StoredBatteries)
        {
            inventory.TryUseStoredBattery();
            if (selectedIndex >= inventory.StoredBatteries)
                selectedIndex = Mathf.Max(0, inventory.StoredBatteries - 1);
        }
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        root = new GameObject("StylizedBatteryInventoryCanvasV4");
        root.transform.SetParent(transform, false);
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        GameObject dim = CreateStretchedUIObject("Dim", root.transform);
        dimRect = dim.GetComponent<RectTransform>();
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.58f);

        panel = CreateUIObject("Panel", root.transform, new Vector2(0f, 0.5f), new Vector2(760f, 900f));
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.pivot = new Vector2(0f, 0.5f);
        panelRT.anchoredPosition = new Vector2(64f, 0f);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.01f, 0.01f, 0.01f, 0.88f);

        CreateText("Title", panel.transform, "Inventory", new Vector2(0f, 1f), new Vector2(56f, -52f), 42, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text subtitle = CreateText("Subtitle", panel.transform, "Emergency Supplies", new Vector2(0f, 1f), new Vector2(56f, -92f), 18, TextAnchor.MiddleLeft, FontStyle.Italic);
        subtitle.color = new Color(1f, 1f, 1f, 0.54f);
        AddDivider(panel.transform, new Vector2(56f, -126f), new Vector2(560f, 2f), 0.22f);

        GameObject grid = CreateUIObject("Grid", panel.transform, new Vector2(0f, 1f), new Vector2(480f, 480f));
        RectTransform gridRT = grid.GetComponent<RectTransform>();
        gridRT.pivot = new Vector2(0f, 1f);
        gridRT.anchoredPosition = new Vector2(56f, -160f);
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(140f, 140f);
        layout.spacing = new Vector2(20f, 20f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        for (int i = 0; i < 6; i++)
        {
            GameObject outer = CreateUIObject("Slot_" + i, grid.transform, new Vector2(0.5f, 0.5f), new Vector2(140f, 140f));
            Image frame = outer.AddComponent<Image>();
            frame.color = frameIdle;
            slotFrames.Add(frame);

            Button button = outer.AddComponent<Button>();
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            int index = i;
            button.onClick.AddListener(() => { selectedIndex = index; TryUseSelectedBattery(); Refresh(); });
            EventTrigger trigger = outer.AddComponent<EventTrigger>();
            AddEvent(trigger, EventTriggerType.PointerEnter, () => { selectedIndex = index; EventSystem.current?.SetSelectedGameObject(slotButtons[index].gameObject); Refresh(); });
            slotButtons.Add(button);

            GameObject inner = CreateUIObject("Inner", outer.transform, new Vector2(0.5f, 0.5f), new Vector2(128f, 128f));
            Image innerImage = inner.AddComponent<Image>();
            innerImage.color = slotIdle;
            slotBackgrounds.Add(innerImage);

            GameObject glow = CreateUIObject("Glow", inner.transform, new Vector2(0.5f, 0.5f), new Vector2(96f, 96f));
            Image glowImage = glow.AddComponent<Image>();
            glowImage.color = new Color(0.96f, 0.90f, 0.72f, 0.035f);

            GameObject body = CreateUIObject("BatteryBody", inner.transform, new Vector2(0.5f, 0.5f), new Vector2(40f, 78f));
            Image bodyImage = body.AddComponent<Image>();
            bodyImage.color = batteryOff;
            batteryBodies.Add(bodyImage);

            GameObject core = CreateUIObject("BatteryCore", body.transform, new Vector2(0.5f, 0.5f), new Vector2(18f, 44f));
            Image coreImage = core.AddComponent<Image>();
            coreImage.color = new Color(0f, 0f, 0f, 0.20f);

            GameObject cap = CreateUIObject("Cap", inner.transform, new Vector2(0.5f, 1f), new Vector2(14f, 7f));
            cap.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -8f);
            Image capImage = cap.AddComponent<Image>();
            capImage.color = new Color(1f, 1f, 1f, 0.48f);

            Text qty = CreateText("Qty", inner.transform, "", new Vector2(1f, 1f), new Vector2(-10f, -10f), 20, TextAnchor.UpperRight, FontStyle.Normal);
            qty.color = new Color(1f, 1f, 1f, 0.72f);
            qtyLabels.Add(qty);
        }

        GameObject helpRow = CreateUIObject("HelpRow", panel.transform, new Vector2(0f, 1f), new Vector2(600f, 40f));
        RectTransform helpRT = helpRow.GetComponent<RectTransform>();
        helpRT.pivot = new Vector2(0f, 1f);
        helpRT.anchoredPosition = new Vector2(56f, -664f);
        Text helpLeft = CreateText("HelpLeft", helpRow.transform, "TAB  Close", new Vector2(0f, 0.5f), new Vector2(0f, 0f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
        Text helpCenter = CreateText("HelpCenter", helpRow.transform, "ARROWS / MOUSE  Select", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), 18, TextAnchor.MiddleCenter, FontStyle.Normal);
        Text helpRight = CreateText("HelpRight", helpRow.transform, "ENTER / R  Use", new Vector2(1f, 0.5f), new Vector2(0f, 0f), 18, TextAnchor.MiddleRight, FontStyle.Normal);
        helpLeft.color = helpCenter.color = helpRight.color = new Color(1f, 1f, 1f, 0.78f);

        AddDivider(panel.transform, new Vector2(56f, -714f), new Vector2(600f, 2f), 0.22f);

        descTitle = CreateText("DescTitle", panel.transform, "Battery", new Vector2(0f, 1f), new Vector2(56f, -764f), 36, TextAnchor.MiddleLeft, FontStyle.Bold);
        qtyText = CreateText("QtyBig", panel.transform, "x0", new Vector2(1f, 1f), new Vector2(-104f, -764f), 30, TextAnchor.MiddleRight, FontStyle.Bold);
        qtyText.color = new Color(1f, 1f, 1f, 0.86f);
        descBody = CreateText("DescBody", panel.transform, "", new Vector2(0f, 1f), new Vector2(56f, -826f), 24, TextAnchor.UpperLeft, FontStyle.Normal);
        descBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        descBody.verticalOverflow = VerticalWrapMode.Overflow;
        descBody.GetComponent<RectTransform>().sizeDelta = new Vector2(560f, 120f);
        descBody.color = new Color(1f, 1f, 1f, 0.86f);
    }

    private void Refresh()
    {
        if (inventory == null) return;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, 5);

        for (int i = 0; i < slotFrames.Count; i++)
        {
            bool filled = i < inventory.StoredBatteries;
            bool selected = i == selectedIndex;
            bool hovered = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == slotButtons[i].gameObject;
            slotFrames[i].color = selected ? frameSelected : hovered ? frameHover : frameIdle;
            slotBackgrounds[i].color = filled ? slotFilled : slotIdle;
            batteryBodies[i].color = filled ? batteryOn : batteryOff;
            qtyLabels[i].text = filled ? "x1" : "";
        }

        bool hasItem = selectedIndex < inventory.StoredBatteries;
        descTitle.text = hasItem ? "Battery" : "Empty Slot";
        descBody.text = hasItem ? "Spare flashlight battery. Use it to restore full torch power before the hallway goes dark again." : "No item stored in this slot.";
        qtyText.text = hasItem ? "x1" : "x0";
    }

    private void SetInventoryOpen(bool open, bool instant)
    {
        inventoryOpen = open;
        if (root != null) root.SetActive(open);

        if (open)
        {
            previousTimeScale = Time.timeScale;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            DisableLikelyMouseLookScripts();
            EventSystem.current?.SetSelectedGameObject(slotButtons[Mathf.Clamp(selectedIndex, 0, slotButtons.Count - 1)].gameObject);
        }
        else
        {
            RestoreLookScripts();
            Time.timeScale = instant ? 1f : previousTimeScale <= 0f ? 1f : previousTimeScale;
            Cursor.visible = instant ? false : previousCursorVisible;
            Cursor.lockState = instant ? CursorLockMode.Locked : previousCursorLock;
        }
    }

    private void DisableLikelyMouseLookScripts()
    {
        disabledLookScripts.Clear();
        Behaviour[] behaviours = GetComponentsInParent<Behaviour>(true);
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour == null || !behaviour.enabled) continue;
            string name = behaviour.GetType().Name.ToLowerInvariant();
            if (name.Contains("look") || name.Contains("mouse") || name.Contains("camera"))
            {
                if (behaviour == this) continue;
                behaviour.enabled = false;
                disabledLookScripts.Add(behaviour);
            }
        }

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            foreach (Behaviour behaviour in mainCam.GetComponents<Behaviour>())
            {
                if (behaviour == null || !behaviour.enabled || behaviour == this) continue;
                string name = behaviour.GetType().Name.ToLowerInvariant();
                if ((name.Contains("look") || name.Contains("mouse") || name.Contains("camera")) && !disabledLookScripts.Contains(behaviour))
                {
                    behaviour.enabled = false;
                    disabledLookScripts.Add(behaviour);
                }
            }
        }
    }

    private void RestoreLookScripts()
    {
        foreach (Behaviour behaviour in disabledLookScripts)
            if (behaviour != null) behaviour.enabled = true;
        disabledLookScripts.Clear();
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
        rt.localScale = Vector3.one;
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

    private Text CreateText(string name, Transform parent, string value, Vector2 anchor, Vector2 pos, int fontSize, TextAnchor align, FontStyle style)
    {
        GameObject obj = CreateUIObject(name, parent, anchor, new Vector2(600f, 42f));
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        Text text = obj.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = align;
        text.color = Color.white;
        return text;
    }

    private void OnDestroy()
    {
        RestoreLookScripts();
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}