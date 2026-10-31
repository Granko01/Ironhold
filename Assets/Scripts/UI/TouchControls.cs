using UnityEngine;
using UnityEngine.EventSystems;

// Full-screen touch surface that sits behind the HUD and tower cards. A press either places
// the selected tower at the tapped ground cell, sends the squad to the tapped spot, or spawns a
// floating joystick under the finger. In first-person, presses on the right half of the screen
// drag the camera around instead, so one thumb moves while the other looks.
public class TouchControls : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private TowerPlacer placer;
    [SerializeField] private RectTransform joystickBase;
    [SerializeField] private RectTransform joystickKnob;
    [SerializeField] private float joystickRadius = 120f;

    public static Vector2 MoveInput { get; private set; }

    private static Vector2 lookDelta;

    private RectTransform zone;
    private Canvas canvas;
    private int activePointerId = NoPointer;
    private int lookPointerId = NoPointer;
    private Vector2 pressOrigin;
    private Vector2 lastLookPosition;

    private const int NoPointer = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        MoveInput = Vector2.zero;
        lookDelta = Vector2.zero;
    }

    // Other controls (the FIRE button) feed drags in here so the player can aim while shooting.
    public static void AddLookDelta(Vector2 canvasDelta) => lookDelta += canvasDelta;

    // Look drag accumulated since the last call, in canvas units (independent of screen resolution).
    public static Vector2 ConsumeLookDelta()
    {
        Vector2 delta = lookDelta;
        lookDelta = Vector2.zero;
        return delta;
    }

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
        lookPointerId = NoPointer;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (FirstPersonView.IsActive)
        {
            if (eventData.position.x > Screen.width * 0.5f)
            {
                if (lookPointerId == NoPointer)
                {
                    lookPointerId = eventData.pointerId;
                    lastLookPosition = eventData.position;
                }
                return;
            }
        }
        else if (SquadCommander.IsChoosing)
        {
            SquadCommander.Instance.SendToScreenPoint(eventData.position);
            return;
        }
        else if (TowerPlacer.IsPlacing && placer != null)
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
        float scale = canvas != null ? canvas.scaleFactor : 1f;

        if (eventData.pointerId == lookPointerId)
        {
            lookDelta += (eventData.position - lastLookPosition) / scale;
            lastLookPosition = eventData.position;
            return;
        }

        if (eventData.pointerId != activePointerId) return;

        Vector2 delta = (eventData.position - pressOrigin) / scale;
        Vector2 clamped = Vector2.ClampMagnitude(delta, joystickRadius);

        MoveInput = clamped / joystickRadius;
        if (joystickKnob != null) joystickKnob.anchoredPosition = clamped;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == lookPointerId)
        {
            lookPointerId = NoPointer;
            return;
        }

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
