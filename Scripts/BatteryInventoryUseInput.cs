using UnityEngine;
using UnityEngine.InputSystem;

public class BatteryInventoryUseInput : MonoBehaviour
{
    [SerializeField] private BatteryInventory inventory;
    [SerializeField] private Key useBatteryKey = Key.R;

    private void Awake()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<BatteryInventory>();
    }

    private void Update()
    {
        if (inventory == null || Keyboard.current == null) return;
        if (Keyboard.current[useBatteryKey].wasPressedThisFrame)
            inventory.TryUseStoredBattery();
    }
}