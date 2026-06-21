using UnityEngine;
using System.Reflection;

public class BackroomsAutoPlayerVitals : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float maxStamina = 5f;
    [SerializeField] private float currentStamina = 5f;
    [SerializeField] private float drainPerSecond = 1f;
    [SerializeField] private float recoverPerSecond = 0.85f;
    [SerializeField] private float exhaustedLockSeconds = 5f;

    [Header("Debug")]
    [SerializeField] private bool sprintRequested;
    [SerializeField] private bool sprintActive;
    [SerializeField] private Object playerSource;

    private float exhaustedTimer;

    public float Stamina01 => maxStamina <= 0f ? 0f : currentStamina / maxStamina;
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public bool CanSprint => exhaustedTimer <= 0f && currentStamina > 0.01f;
    public bool IsExhausted => exhaustedTimer > 0f;
    public bool IsSprintingNow => sprintActive;
    public float ExhaustedTimer => Mathf.Max(0f, exhaustedTimer);

    private void Awake()
    {
        if (playerSource == null)
            playerSource = FindPlayerSource();
    }

    private void Update()
    {
        if (playerSource == null)
            playerSource = FindPlayerSource();

        if (exhaustedTimer > 0f)
            exhaustedTimer -= Time.deltaTime;

        bool moving = GetBool(playerSource, "IsMoving");
        bool sprintingIntent = GetBool(playerSource, "IsSprinting") || GetBool(playerSource, "wantsToSprint") || GetBool(playerSource, "isRunning");
        float moveAmount = GetFloat(playerSource, "MoveAmount01", 1f);
        moving = moving || moveAmount > 0.08f;

        sprintRequested = moving && sprintingIntent;
        sprintActive = sprintRequested && CanSprint;

        if (sprintActive)
        {
            currentStamina = Mathf.Max(0f, currentStamina - drainPerSecond * Time.deltaTime);
            if (currentStamina <= 0.001f)
            {
                currentStamina = 0f;
                exhaustedTimer = exhaustedLockSeconds;
                sprintActive = false;
            }
        }
        else if (exhaustedTimer <= 0f)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + recoverPerSecond * Time.deltaTime);
        }
    }

    private Object FindPlayerSource()
    {
        var components = GetComponents<MonoBehaviour>();
        foreach (var c in components)
        {
            if (c == null) continue;
            if (c.GetType().Name == "BackroomsPlayer") return c;
        }
        return GetComponent<MonoBehaviour>();
    }

    private bool GetBool(Object source, string name)
    {
        if (source == null) return false;
        var type = source.GetType();
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(bool)) return (bool)prop.GetValue(source, null);
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(bool)) return (bool)field.GetValue(source);
        return false;
    }

    private float GetFloat(Object source, string name, float fallback)
    {
        if (source == null) return fallback;
        var type = source.GetType();
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(float)) return (float)prop.GetValue(source, null);
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(float)) return (float)field.GetValue(source);
        return fallback;
    }
}
