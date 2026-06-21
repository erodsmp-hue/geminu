using UnityEngine;

public class FlashlightTopSwitchAnimator : MonoBehaviour
{
    [SerializeField] private FlashlightBatterySystem flashlightBatterySystem;
    [SerializeField] private Light flashlightLight;
    [SerializeField] private Transform topSwitch;

    [Header("Off Pose")]
    [SerializeField] private Vector3 offLocalPosition = new Vector3(0f, 0.0512f, 0.095f);
    [SerializeField] private Vector3 offLocalEuler = Vector3.zero;

    [Header("On Rotation")]
    [SerializeField] private Vector3 onRotationOffset = new Vector3(12f, 0f, 0f);
    [SerializeField] private float rotationSmooth = 18f;
    [SerializeField] private bool alsoMoveSlightly = false;
    [SerializeField] private Vector3 onPositionOffset = Vector3.zero;

    private Vector3 onLocalPosition;
    private Vector3 onLocalEuler;

    private void Awake()
    {
        if (topSwitch == null)
            topSwitch = FindDeepChild(transform, "TopSwitch");

        if (flashlightBatterySystem == null)
            flashlightBatterySystem = FindFirstObjectByType<FlashlightBatterySystem>();

        if (flashlightLight == null && flashlightBatterySystem != null)
            flashlightLight = flashlightBatterySystem.GetComponent<Light>();

        onLocalPosition = offLocalPosition + (alsoMoveSlightly ? onPositionOffset : Vector3.zero);
        onLocalEuler = offLocalEuler + onRotationOffset;

        if (topSwitch != null)
        {
            topSwitch.localPosition = offLocalPosition;
            topSwitch.localRotation = Quaternion.Euler(offLocalEuler);
        }
    }

    private void LateUpdate()
    {
        if (topSwitch == null) return;

        bool isOn = false;
        if (flashlightBatterySystem != null)
            isOn = flashlightBatterySystem.IsFlashlightOn;
        else if (flashlightLight != null)
            isOn = flashlightLight.enabled;

        Vector3 targetPos = isOn ? onLocalPosition : offLocalPosition;
        Quaternion targetRot = Quaternion.Euler(isOn ? onLocalEuler : offLocalEuler);

        topSwitch.localPosition = Vector3.Lerp(topSwitch.localPosition, targetPos, Time.deltaTime * rotationSmooth);
        topSwitch.localRotation = Quaternion.Slerp(topSwitch.localRotation, targetRot, Time.deltaTime * rotationSmooth);
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }
}
