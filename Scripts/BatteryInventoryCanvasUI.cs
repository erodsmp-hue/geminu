using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BatteryInventoryCanvasUI : MonoBehaviour
{
    [SerializeField] private BatteryInventory inventory;
    [SerializeField] private Key toggleInventoryKey = Key.Tab;
    [SerializeField] private Key useBatteryKey = Key.R;

    private Canvas canvas;
    private GameObject panel;
    private Text titleText;
    private Text countText;
    private Text hintText;
    private readonly List<Image> slotFills = new List<Image>();

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
            SetVisible(!panel.activeSelf);

        if (Keyboard.current[useBatteryKey].wasPressedThisFrame)
            inventory.TryUseStoredBattery();

        Refresh();
    }

    private void BuildUI()
    {
        Transform existing = transform.Find("BatteryInventoryCanvas");
        if (existing != null)
        {
            canvas = existing.GetComponent<Canvas>();
            panel = existing.Find("InventoryPanel")?.gameObject;
            if (panel != null) return;
        }

        GameObject canvasObj = new GameObject("BatteryInventoryCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        panel = CreateUIObject("InventoryPanel", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(460, 240));
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.04f, 0.04f, 0.90f);

        titleText = CreateText("Title", panel.transform, "BATTERY INVENTORY", new Vector2(0.5f, 1f), new Vector2(0, -28), 24, TextAnchor.MiddleCenter);
        countText = CreateText("Count", panel.transform, "0 / 0", new Vector2(0.5f, 1f), new Vector2(0, -62), 18, TextAnchor.MiddleCenter);
        hintText = CreateText("Hint", panel.transform, "TAB = close   |   R = use one battery", new Vector2(0.5f, 0f), new Vector2(0, 22), 16, TextAnchor.MiddleCenter);
        hintText.color = new Color(1f, 1f, 1f, 0.72f);

        GameObject grid = CreateUIObject("Slots", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(380, 88));
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(50, 74);
        layout.spacing = new Vector2(10, 0);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 6;

        for (int i = 0; i < 6; i++)
        {
            GameObject slot = CreateUIObject("Slot_" + i, grid.transform, new Vector2(0.5f, 0.5f), new Vector2(50, 74));
            Image bg = slot.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.16f, 0.16f, 0.95f);

            GameObject fillObj = CreateUIObject("Fill", slot.transform, new Vector2(0.5f, 0.5f), new Vector2(28, 46));
            Image fill = fillObj.AddComponent<Image>();
            fill.color = new Color(0.20f, 0.20f, 0.20f, 0.95f);
            slotFills.Add(fill);

            GameObject cap = CreateUIObject("Cap", slot.transform, new Vector2(0.5f, 1f), new Vector2(12, 6));
            cap.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -4);
            Image capImage = cap.AddComponent<Image>();
            capImage.color = new Color(1f, 1f, 1f, 0.55f);
        }
    }

    private void Refresh()
    {
        if (inventory == null || countText == null) return;

        countText.text = inventory.StoredBatteries + " / " + inventory.MaxStoredBatteries;
        for (int i = 0; i < slotFills.Count; i++)
        {
            bool filled = i < inventory.StoredBatteries;
            slotFills[i].color = filled ? new Color(0.78f, 0.95f, 0.42f, 1f) : new Color(0.20f, 0.20f, 0.20f, 0.95f);
        }
    }

    private void SetVisible(bool visible)
    {
        if (panel != null) panel.SetActive(visible);
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
        GameObject obj = CreateUIObject(name, parent, anchor, new Vector2(400, 30));
        obj.GetComponent<RectTransform>().anchoredPosition = pos;
        Text text = obj.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        return text;
    }
}