using UnityEngine;

// Tilted perspective follow camera. Field of view is derived from the screen aspect each frame
// so a fixed slice of the arena (visibleWidth) fits across any phone in portrait.
public class IsoCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float pitch = 52f;
    [SerializeField] private float distance = 26f;
    [SerializeField] private float visibleWidth = 16f;
    [SerializeField] private float followSharpness = 8f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0f, 2f);

    [Header("Shake")]
    [SerializeField] private float maxShakeOffset = 0.7f;
    [SerializeField] private float maxShakeAngle = 2.5f;
    [SerializeField] private float traumaDecay = 1.6f;

    // Trauma (0-1) is squared into shake strength, so small hits stay subtle and big ones punch.
    private static float trauma;

    private Camera cam;
    private bool snapped;
    private Vector3 smoothedPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => trauma = 0f;

    public static void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        snapped = false;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null) cam.orthographic = false;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (cam != null)
        {
            float halfWidth = visibleWidth * 0.5f;
            float verticalFov = 2f * Mathf.Atan(halfWidth / (distance * Mathf.Max(cam.aspect, 0.01f))) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Clamp(verticalFov, 20f, 80f);
        }

        Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 desired = target.position + targetOffset + rotation * (Vector3.back * distance);

        if (!snapped)
        {
            smoothedPosition = desired;
            snapped = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            smoothedPosition = Vector3.Lerp(smoothedPosition, desired, t);
        }

        // Unscaled time so the shake still plays out during the game-over slow motion.
        float shake = trauma * trauma;
        float seed = Time.unscaledTime * 25f;
        Vector3 shakeOffset = new Vector3(Mathf.PerlinNoise(seed, 0f) - 0.5f, Mathf.PerlinNoise(0f, seed) - 0.5f, 0f) * 2f * maxShakeOffset * shake;
        float shakeRoll = (Mathf.PerlinNoise(seed, seed) - 0.5f) * 2f * maxShakeAngle * shake;
        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.unscaledDeltaTime);

        transform.rotation = rotation * Quaternion.Euler(0f, 0f, shakeRoll);
        transform.position = smoothedPosition + transform.rotation * shakeOffset;
    }
}
