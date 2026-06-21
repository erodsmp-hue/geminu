using UnityEngine;
using UnityEngine.UI;

public class BatteryInventorySlotUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Image borderImage;

    public void SetFilled(bool filled, Color activeColor, Color inactiveColor)
    {
        if (fillImage != null) fillImage.color = filled ? activeColor : inactiveColor;
        if (borderImage != null) borderImage.color = filled ? Color.white : new Color(1f,1f,1f,0.35f);
    }
}