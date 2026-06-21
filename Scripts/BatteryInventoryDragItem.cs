using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BatteryInventoryDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int SourceSlotIndex { get; set; }

    private Transform originalParent;
    private Canvas rootCanvas;
    private CanvasGroup group;
    private RectTransform rect;
    private Image image;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        rootCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (image == null || !image.enabled || rootCanvas == null) return;
        originalParent = transform.parent;
        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();
        group.blocksRaycasts = false;
        group.alpha = 0.85f;
        image.raycastTarget = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rect.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(originalParent, false);
        rect.anchoredPosition = Vector2.zero;
        group.blocksRaycasts = true;
        group.alpha = 1f;
        if (image != null) image.raycastTarget = true;
    }
}
