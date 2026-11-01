using UnityEngine;

// Flashes every renderer white and squashes the model briefly when hit.
public class HitFlash : MonoBehaviour
{
    private const float FlashDuration = 0.08f;
    private const float PunchDuration = 0.14f;
    private const float PunchAmount = 0.18f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer[] renderers;
    private MaterialPropertyBlock flashBlock;
    private Vector3 baseScale;
    private float flashTimer;
    private float punchTimer;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        flashBlock = new MaterialPropertyBlock();
        flashBlock.SetColor(BaseColorId, Color.white);
        baseScale = transform.localScale;
    }

    public void Flash()
    {
        if (flashTimer <= 0f) SetFlash(true);
        flashTimer = FlashDuration;
        punchTimer = PunchDuration;
    }

    private void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f) SetFlash(false);
        }

        if (punchTimer > 0f)
        {
            punchTimer = Mathf.Max(0f, punchTimer - Time.deltaTime);
            float t = punchTimer / PunchDuration;
            float squash = PunchAmount * t;
            transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + squash, 1f - squash, 1f + squash));
        }
    }

    private void SetFlash(bool on)
    {
        foreach (Renderer r in renderers)
        {
            if (r != null) r.SetPropertyBlock(on ? flashBlock : null);
        }
    }
}
