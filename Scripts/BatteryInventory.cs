using System;
using UnityEngine;

public class BatteryInventory : MonoBehaviour
{
    [SerializeField] private int maxStoredBatteries = 6;
    private bool[] slots;

    public int SlotCount => slots != null ? slots.Length : 0;
    public int MaxStoredBatteries => SlotCount;
    public event Action OnInventoryChanged;

    private void Awake()
    {
        slots = new bool[Mathf.Max(1, maxStoredBatteries)];
    }

    public bool HasBatteryInSlot(int index)
    {
        return slots != null && index >= 0 && index < slots.Length && slots[index];
    }

    public int StoredBatteries
    {
        get
        {
            if (slots == null) return 0;
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i]) count++;
            return count;
        }
    }

    public bool TryAddBattery()
    {
        return TryAddBattery(1);
    }

    public bool TryAddBattery(int amount)
    {
        if (slots == null || amount <= 0) return false;

        bool addedAny = false;
        for (int add = 0; add < amount; add++)
        {
            bool placed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i])
                {
                    slots[i] = true;
                    placed = true;
                    addedAny = true;
                    break;
                }
            }
            if (!placed) break;
        }

        if (addedAny) OnInventoryChanged?.Invoke();
        return addedAny;
    }

    public bool TryUseStoredBattery()
    {
        for (int i = 0; i < SlotCount; i++)
            if (TryUseBatteryFromSlot(i))
                return true;
        return false;
    }

    public bool TryUseBatteryFromSlot(int index)
    {
        if (!HasBatteryInSlot(index)) return false;

        FlashlightBatterySystem flashlight = FindFirstObjectByType<FlashlightBatterySystem>();
        if (flashlight == null) return false;

        slots[index] = false;
        flashlight.RefillBatteryToFull();
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void SwapSlots(int a, int b)
    {
        if (slots == null || a < 0 || a >= slots.Length || b < 0 || b >= slots.Length || a == b) return;
        bool temp = slots[a];
        slots[a] = slots[b];
        slots[b] = temp;
        OnInventoryChanged?.Invoke();
    }
}
