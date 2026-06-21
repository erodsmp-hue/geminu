using UnityEngine;
using UnityEngine.EventSystems;

public class BatteryInventoryDropSlot : MonoBehaviour, IDropHandler
{
    public int slotIndex;
    public BatteryInventoryCanvasUISimpleCleanV2 owner;

    public void OnDrop(PointerEventData eventData)
    {
        BatteryInventoryDragItem drag = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<BatteryInventoryDragItem>() : null;
        if (drag == null || owner == null) return;
        owner.HandleDrop(drag.SourceSlotIndex, slotIndex);
    }
}
