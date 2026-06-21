using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BatteryPickup : MonoBehaviour
{
    [SerializeField] private int batteryAmount = 1;
    [SerializeField] private float rotateSpeed = 65f;

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        BatteryInventory inventory = other.GetComponentInParent<BatteryInventory>();
        if (inventory == null) inventory = FindFirstObjectByType<BatteryInventory>();
        if (inventory == null) return;

        if (inventory.TryAddBattery(batteryAmount))
            Destroy(gameObject);
    }
}