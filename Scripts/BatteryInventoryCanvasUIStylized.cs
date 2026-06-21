using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BatteryInventoryCanvasUIStylized : MonoBehaviour
{
    [SerializeField] private BatteryInventory inventory;
    [SerializeField] private Key toggleInventoryKey = Key.Tab;
    [SerializeField] private Key useBatteryKey = Key.R;

    private Canvas canvas;
    private GameObject root;
    private Image backdrop;
    private Text titleText;
    private Text helpText;
    private Text descTitle;
    private Text descBody;
    private Text qtyText;
    private readonly List<Image> slotBorders = new List<Image>();
    private readonly List<Image> slotFills = new List<Image>();
    private int selectedIndex;

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
        if (Keyboard.current[useBatteryKey].wasPressedThisFrame && selectedIndex < inventory.StoredBatteries)
            inventory.TryUseStoredBattery();

        Refresh();
    }

    private void BuildUI()
    {
        Transform existing = transform.Find("StylizedBatteryInventoryCanvas");
        if (existing != null)
        {
            root = existing.gameObject;
            canvas = existing.GetComponent<Canvas>();
            return;
        }

        root = new GameObject("StylizedBatteryInventoryCanvas");
        root.transform.SetParent(transform, false);
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        root.AddComponent<GraphicRaycaster>();

        GameObject dim = CreateUIObject("Dim", root.transform, new Vector2(0.5f, 0.5f), new Vector2(1920, 1080));
        backdrop = dim.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.35f);

        GameObject panel = CreateUIObject("Panel", root.transform, new Vector2(0f, 0.5f), new Vector2(610, 820));
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.pivot = new Vector2(0f, 0.5f);
        prt.anchoredPosition = new Vector2(90f, 0f);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.02f, 0.02f, 0.02f, 0.92f);

        titleText = CreateText("Title", panel.transform, "Inventory", new Vector2(0f, 1f), new Vector2(40f, -46f), 36, TextAnchor.MiddleLeft);
        AddDivider(panel.transform, new Vector2(275f, -78f), new Vector2(390f, 2f));

        GameObject grid = CreateUIObject("Grid", panel.transform, new Vector2(0f, 1f), new Vector2(430, 430));
        grid.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
        grid.GetComponent<RectTransform>().anchoredPosition = new Vector2(40f, -110f);
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(122, 122);
        layout.spacing = new Vector2(18, 18);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        for (int i = 0; i < 6; i++)
        {
            GameObject slot = CreateUIObject("Slot_" + i, grid.transform, new Vector2(0.5f, 0.5f), new Vector2(122, 122));
            Image border = slot.AddComponent<Image>();
            border.color = new Color(1f, 1f, 1f, 0.20f);
            slotBorders.Add(border);

            GameObject inner = CreateUIObject("Inner", slot.transform, new Vector2(0.5f, 0.5f), new Vector2(112, 112));
            Image innerImage = inner.AddComponent<Image>();
            innerImage.color = new Color(0.16f, 0.16f, 0.16f, 0.92f);

            GameObject fill = CreateUIObject("BatteryCell", inner.transform, new Vector2(0.5f, 0.5f), new Vector2(34, 62));
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.20f, 0.20f, 0.20f, 0.95f);
            slotFills.Add(fillImage);

            GameObject cap = CreateUIObject("Cap", inner.transform, new Vector2(0.5f, 1f), new Vector2(14, 8));
            cap.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -6f);
            Image capImage = cap.AddComponent<Image>();
            capImage.color = new Color(0.95f, 0.95f, 0.95f, 0.55f);

            Text qty = CreateText("Qty", inner.transform, "", new Vector2(1f, 1f), new Vector2(-12f, -12f), 20, TextAnchor.UpperRight);
            qty.color = new Color(1f, 1f, 1f, 0.65f);
            qty.name = "QtyLabel";
        }

        helpText = CreateText("Help", panel.transform, "TAB Close   A/D Select   R Use", new Vector2(0f, 1f), new Vector2(40f, -582f), 22, TextAnchor.MiddleLeft);
        helpText.color = new Color(1f, 1f, 1f, 0.78f);
        AddDivider(panel.transform, new Vector2(255f, -615f), new Vector2(430f, 2f));

        descTitle = CreateText("DescTitle", panel.transform, "Battery", new Vector2(0f, 1f), new Vector2(40f, -660f), 34, TextAnchor.MiddleLeft);
        descBody = CreateText("DescBody", panel.transform, "Spare flashlight battery used to fully recharge your torch when things get dangerous.", new Vector2(0f, 1f), new Vector2(40f, -720f), 24, TextAnchor.UpperLeft);
        descBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        descBody.verticalOverflow = VerticalWrapMode.Overflow;
        descBody.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 120f);

        qtyText = CreateText("QtyBig", panel.transform, "x0", new Vector2(1f, 1f), new Vector2(-50f, -660f), 28, TextAnchor.MiddleRight);
        qtyText.color = new Color(1f, 1f, 1f, 0.8f);
    }

    private void Refresh()
    {
        if (inventory == null || slotFills.Count == 0) return;

        selectedIndex = Mathf.Clamp(selectedIndex, 0, 5);
        for (int i = 0; i < slotFills.Count; i++)
        {
            bool filled = i < inventory.StoredBatteries;
            bool selected = i == selectedIndex;

            slotFills[i].color = filled ? new Color(0.84f, 0.90f, 0.52f, 1f) : new Color(0.20f, 0.20f, 0.20f, 0.95f);
            slotBorders[i].color = selected ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.20f);

            Transform qty = slotFills[i].transform.parent.Find("QtyLabel");
            if (qty != null)
            {
                Text label = qty.GetComponent<Text>();
                label.text = filled ? "x1" : "";
            }
        }

        bool selectedHasBattery = selectedIndex < inventory.StoredBatteries;
        descTitle.text = selectedHasBattery ? "Battery" : "Empty Slot";
        descBody.text = selectedHasBattery
            ? "Spare flashlight battery. Use it to refill your torch to full power when your light is dying in the dark corridors."
            : "Nothing is stored in this slot.";
        qtyText.text = selectedHasBattery ? "x1" : "x0";
    }

    private void SetVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }

    private void AddDivider(Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject obj = CreateUIObject("Divider", parent, new Vector2(0f, 1f), size);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        Image img = obj.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.35f);
    }

    private GameObject CreateUIObject(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        return obj;
    }

    private Text CreateText(string name, Transform parent, string value, Vector2 anchor, Vector2 pos, int fontSize, TextAnchor align)
    {
        GameObject obj = CreateUIObject(name, parent, anchor, new Vector2(520, 40));
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.pivot = new Vector2(anchor.x == 1f ? 1f : 0f, 0.5f);
        rt.anchoredPosition = pos;
        Text text = obj.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        return text;
    }
}