using UnityEngine;
using UnityEngine.EventSystems;

// Hold-to-shoot button for first-person mode. A plain Button only reports clicks, so this
// tracks press/release itself and grows slightly while held. Dragging the thumb while holding it
// also turns the camera - otherwise there'd be no thumb left to aim with while firing.
public class FireButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public static bool IsHeld { get; private set; }

    private int pointerId = int.MinValue;
    private Canvas canvas;

    private void Awake() => canvas = GetComponentInParent<Canvas>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsHeld = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerId = eventData.pointerId;
        IsHeld = true;
        transform.localScale = Vector3.one * 1.08f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        TouchControls.AddLookDelta(eventData.delta / scale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        Release();
    }

    private void OnDisable() => Release();

    private void Release()
    {
        pointerId = int.MinValue;
        IsHeld = false;
        transform.localScale = Vector3.one;
    }
}
