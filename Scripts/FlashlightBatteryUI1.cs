using UnityEngine;

public class FlashlightBatteryUI : MonoBehaviour
{
    [SerializeField] private FlashlightBatterySystem batterySystem;
    [SerializeField] private Vector2 panelSize = new Vector2(180f, 18f);
    [SerializeField] private Vector2 panelOffset = new Vector2(20f, 20f);

    private void Awake()
    {
        if (batterySystem == null)
            batterySystem = FindFirstObjectByType<FlashlightBatterySystem>();
    }

    private void OnGUI()
    {
        if (batterySystem == null) return;

        float percent = batterySystem.GetBatteryPercent();
        Rect bg = new Rect(panelOffset.x, Screen.height - panelOffset.y - panelSize.y, panelSize.x, panelSize.y);
        Rect fill = new Rect(bg.x + 2f, bg.y + 2f, (bg.width - 4f) * (percent / 100f), bg.height - 4f);

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(bg, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(new Color(0.85f, 0.15f, 0.15f), new Color(0.9f, 0.95f, 0.45f), percent / 100f);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(bg.x, bg.y - 20f, 100f, 20f), "Battery " + Mathf.RoundToInt(percent) + "%");
        GUI.color = old;
    }
}