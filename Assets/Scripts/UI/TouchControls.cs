using UnityEngine;
using UnityEngine.EventSystems;

// Full-screen touch surface that sits behind the HUD and tower cards. A press either places
// the selected tower at the tapped ground cell, sends the squad to the tapped spot, or spawns a
// floating joystick under the finger.
public class TouchControls : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private TowerPlacer placer;
    [SerializeField] private RectTransform joystickBase;
    [SerializeField] private RectTransform joystickKnob;
    [SerializeField] private float joystickRadius = 120f;

    public static Vector2 MoveInput { get; private set; }

    private RectTransform zone;
    private Canvas canvas;
    private int activePointerId = NoPointer;
    private Vector2 pressOrigin;

    private const int NoPointer = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => MoveInput = Vector2.zero;

    public void Configure(TowerPlacer towerPlacer, RectTransform baseRect, RectTransform knobRect)
    {
        placer = towerPlacer;
        joystickBase = baseRect;
        joystickKnob = knobRect;
    }

    private void Awake()
    {
        zone = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        SetJoystickVisible(false);
    }

    private void OnDisable()
    {
        ReleaseJoystick();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (SquadCommander.IsChoosing)
        {
            SquadCommander.Instance.SendToScreenPoint(eventData.position);
            return;
        }

        if (TowerPlacer.IsPlacing && placer != null)
        {
            placer.TryPlaceAtScreenPoint(eventData.position);
            return;
        }

        if (activePointerId != NoPointer) return;

        activePointerId = eventData.pointerId;
        pressOrigin = eventData.position;

        if (joystickBase != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, null, out Vector2 local))
        {
            joystickBase.anchoredPosition = local;
        }
        if (joystickKnob != null) joystickKnob.anchoredPosition = Vector2.zero;
        SetJoystickVisible(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 delta = (eventData.position - pressOrigin) / scale;
        Vector2 clamped = Vector2.ClampMagnitude(delta, joystickRadius);

        MoveInput = clamped / joystickRadius;
        if (joystickKnob != null) joystickKnob.anchoredPosition = clamped;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        ReleaseJoystick();
    }

    private void ReleaseJoystick()
    {
        activePointerId = NoPointer;
        MoveInput = Vector2.zero;
        SetJoystickVisible(false);
    }

    private void SetJoystickVisible(bool visible)
    {
        if (joystickBase != null) joystickBase.gameObject.SetActive(visible);
    }
}
