using UnityEngine;

public class FlashlightModelFollowLight : MonoBehaviour
{
    [SerializeField] private Light flashlightLight;
    [SerializeField] private Renderer lensRenderer;
    [SerializeField] private Color onEmission = new Color(1f, 0.93f, 0.72f) * 1.5f;
    [SerializeField] private Color offEmission = Color.black;

    public void TryAutoFindLight()
    {
        if (flashlightLight == null)
            flashlightLight = GetComponentInParent<Camera>() != null ? GetComponentInParent<Camera>().GetComponentInChildren<Light>() : null;
        if (lensRenderer == null)
        {
            Transform lens = transform.Find("Lens");
            if (lens != null) lensRenderer = lens.GetComponent<Renderer>();
        }
    }

    private void Awake()
    {
        TryAutoFindLight();
        ApplyVisual();
    }

    private void Update()
    {
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (lensRenderer == null) return;
        Material mat = lensRenderer.material;
        if (!mat.HasProperty("_EmissionColor")) return;
        bool on = flashlightLight != null && flashlightLight.enabled;
        mat.SetColor("_EmissionColor", on ? onEmission : offEmission);
    }
}