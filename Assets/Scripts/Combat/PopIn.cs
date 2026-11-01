using UnityEngine;

// Scales an object up from nothing with a little overshoot, then removes itself.
public class PopIn : MonoBehaviour
{
    private const float Duration = 0.35f;

    private Vector3 targetScale;
    private float elapsed;

    private void Awake()
    {
        targetScale = transform.localScale;
        transform.localScale = targetScale * 0.01f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Duration);

        // Ease-out-back: overshoots ~10% before settling.
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        transform.localScale = targetScale * eased;

        if (t >= 1f)
        {
            transform.localScale = targetScale;
            Destroy(this);
        }
    }
}
