using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BatteryInventoryCanvasUIStylizedV2 : MonoBehaviour
{
    [SerializeField] private BatteryInventory inventory;
    [SerializeField] private Key toggleInventoryKey = Key.Tab;
    [SerializeField] private Key useBatteryKey = Key.R;

    private Canvas canvas;
    private GameObject root;
    private GameObject panel;
    private Text titleText;
    private Text helpLeftText;
    private Text helpCenterText;
    private Text helpRightText;
    private Text descTitle;
    private Text descBody;
    private Text qtyText;
    private readonly List<Image> slotFrames = new List<Image>();
    private readonly List<Image> slotBackgrounds = new List<Image>();
    private readonly List<Image> batteryBodies = new List<Image>();
    private readonly List<Text> qtyLabels = new List<Text>();
    private int selectedIndex;

    private readonly Color frameIdle = new Color(1f, 1f, 1f, 0.10f);
    private readonly Color frameSelected = new Color(1f, 1f, 1f, 0.88f);
    private readonly Color slotIdle = new Color(0.12f, 0.12f, 0.12f, 0.92f);
    private readonly Color slotFilled = new Color(0.16f, 0.16f, 0.16f, 0.96f);
    private readonly Color batteryOn = new Color(0.88f, 0.82f, 0.50f, 1f);
    private readonly Color batteryOff = new Color(0.22f, 0.22f, 0.22f, 0.95f);

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<BatteryInventory>();
        if (inventory == null)
            inventory = FindFirstObjectByType<BatteryInventory>();

        BuildUI();
        SetVisible(false);
        Refresh();
    }

    private void Update()
    {
        if (Keyboard.current == null || inventory == null) return;

        if (Keyboard.current[toggleInventoryKey].wasPressedThisFrame)
            SetVisible(!root.activeSelf);

        if (!root.activeSelf) return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
            selectedIndex = Mathf.Max(0, selectedIndex - 1);
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
            selectedIndex = Mathf.Min(5, selectedIndex + 1);
        if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
            selectedIndex = Mathf.Max(0, selectedIndex - 3);
        if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
            selectedIndex = Mathf.Min(5, selectedIndex + 3);

        if (Keyboard.current[useBatteryKey].wasPressedThisFrame && selectedIndex < inventory.StoredBatteries)
            inventory.TryUseStoredBattery();

        Refresh();
    }

    private void BuildUI()
    {
        Transform existing = transform.Find("StylizedBatteryInventoryCanvasV2");
        if (existing != null)
        {
            root = existing.gameObject;
            canvas = existing.GetComponent<Canvas>();
            return;
        }

        root = new GameObject("StylizedBatteryInventoryCanvasV2");
        root.transform.SetParent(transform, false);
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        GameObject fade = CreateUIObject("Fade", root.transform, new Vector2(0.5f, 0.5f), new Vector2(1920, 1080));
        Image fadeImage = fade.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0.22f);

        panel = CreateUIObject("Panel", root.transform, new Vector2(0f, 0.5f), new Vector2(700f, 860f));
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.pivot = new Vector2(0f, 0.5f);
        panelRT.anchoredPosition = new Vector2(72f, 0f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.82f);

        titleText = CreateText("Title", panel.transform, "Inventory", new Vector2(0f, 1f), new Vector2(56f, -56f), 40, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddDivider(panel.transform, new Vector2(56f, -100f), new Vector2(520f, 2f), 0.22f);

        GameObject grid = CreateUIObject("Grid", panel.transform, new Vector2(0f, 1f), new Vector2(470f, 470f));
        RectTransform gridRT = grid.GetComponent<RectTransform>();
        gridRT.pivot = new Vector2(0f, 1f);
        gridRT.anchoredPosition = new Vector2(56f, -136f);
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(136f, 136f);
        layout.spacing = new Vector2(18f, 18f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        for (int i = 0; i < 6; i++)
        {
            GameObject outer = CreateUIObject("Slot_" + i, grid.transform, new Vector2(0.5f, 0.5f), new Vector2(136f, 136f));
            Image frame = outer.AddComponent<Image>();
            frame.color = frameIdle;
            slotFrames.Add(frame);

            GameObject inner = CreateUIObject("Inner", outer.transform, new Vector2(0.5f, 0.5f), new Vector2(126f, 126f));
            Image innerImage = inner.AddComponent<Image>();
            innerImage.color = slotIdle;
            slotBackgrounds.Add(innerImage);

            GameObject glow = CreateUIObject("Glow", inner.transform, new Vector2(0.5f, 0.5f), new Vector2(92f, 92f));
            Image glowImage = glow.AddComponent<Image>();
            glowImage.color = new Color(1f, 1f, 1f, 0.03f);

            GameObject body = CreateUIObject("BatteryBody", inner.transform, new Vector2(0.5f, 0.5f), new Vector2(36f, 70f));
            Image bodyImage = body.AddComponent<Image>();
            bodyImage.color = batteryOff;
            batteryBodies.Add(bodyImage);

            GameObject core = CreateUIObject("BatteryCore", body.transform, new Vector2(0.5f, 0.5f), new Vector2(18f, 42f));
            Image coreImage = core.AddComponent<Image>();
            coreImage.color = new Color(0f, 0f, 0f, 0.18f);

            GameObject cap = CreateUIObject("Cap", inner.transform, new Vector2(0.5f, 1f), new Vector2(12f, 6f));
            cap.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -8f);
            Image capImage = cap.AddComponent<Image>();
            capImage.color = new Color(1f, 1f, 1f, 0.48f);

            Text qty = CreateText("Qty", inner.transform, "", new Vector2(1f, 1f), new Vector2(-12f, -12f), 20, TextAnchor.UpperRight, FontStyle.Normal);
            qty.color = new Color(1f, 1f, 1f, 0.72f);
            qtyLabels.Add(qty);
        }

        GameObject helpRow = CreateUIObject("HelpRow", panel.transform, new Vector2(0f, 1f), new Vector2(540f, 40f));
        RectTransform helpRT = helpRow.GetComponent<RectTransform>();
        helpRT.pivot = new Vector2(0f, 1f);
        helpRT.anchoredPosition = new Vector2(56f, -616f);

        helpLeftText = CreateText("HelpLeft", helpRow.transform, "TAB  Close", new Vector2(0f, 0.5f), new Vector2(0f, 0f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
        helpCenterText = CreateText("HelpCenter", helpRow.transform, "WASD  Select", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), 18, TextAnchor.MiddleCenter, FontStyle.Normal);
        helpRightText = CreateText("HelpRight", helpRow.transform, "R  Use", new Vector2(1f, 0.5f), new Vector2(0f, 0f), 18, TextAnchor.MiddleRight, FontStyle.Normal);
        helpLeftText.color = helpCenterText.color = helpRightText.color = new Color(1f, 1f, 1f, 0.76f);

        AddDivider(panel.transform, new Vector2(56f, -666f), new Vector2(540f, 2f), 0.22f);

        descTitle = CreateText("DescTitle", panel.transform, "Battery", new Vector2(0f, 1f), new Vector2(56f, -716f), 34, TextAnchor.MiddleLeft, FontStyle.Bold);
        qtyText = CreateText("QtyBig", panel.transform, "x0", new Vector2(1f, 1f), new Vector2(-104f, -716f), 30, TextAnchor.MiddleRight, FontStyle.Bold);
        qtyText.color = new Color(1f, 1f, 1f, 0.82f);

        descBody = CreateText("DescBody", panel.transform, "", new Vector2(0f, 1f), new Vector2(56f, -778f), 24, TextAnchor.UpperLeft, FontStyle.Normal);
        descBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        descBody.verticalOverflow = VerticalWrapMode.Overflow;
        descBody.GetComponent<RectTransform>().sizeDelta = new Vector2(520f, 120f);
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

            slotFrames[i].color = selected ? frameSelected : frameIdle;
            slotBackgrounds[i].color = filled ? slotFilled : slotIdle;
            batteryBodies[i].color = filled ? batteryOn : batteryOff;
            qtyLabels[i].text = filled ? "x1" : "";
        }

        bool hasItem = selectedIndex < inventory.StoredBatteries;
        descTitle.text = hasItem ? "Battery" : "Empty Slot";
        descBody.text = hasItem
            ? "Spare flashlight battery. Use it to restore full charge before the corridor goes dark again."
            : "Nothing is stored in this slot.";
        qtyText.text = hasItem ? "x1" : "x0";
    }

    private void SetVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);
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
        GameObject obj = CreateUIObject(name, parent, anchor, new Vector2(540f, 40f));
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
}