using UnityEngine;

public class BatteryInventoryHUD : MonoBehaviour
{
    [SerializeField] private BatteryInventory inventory;
    [SerializeField] private Vector2 anchor = new Vector2(20f, 54f);
    [SerializeField] private bool showLabel = true;

    private GUIStyle textStyle;

    private void Awake()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<BatteryInventory>();

        textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 18;
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        if (inventory == null) return;

        string text = showLabel
            ? "Batteries: " + inventory.StoredBatteries + "/" + inventory.MaxStoredBatteries + "  [R to use]"
            : inventory.StoredBatteries + "/" + inventory.MaxStoredBatteries;

        GUI.Label(new Rect(anchor.x, Screen.height - anchor.y - 24f, 260f, 24f), text, textStyle);
    }
}