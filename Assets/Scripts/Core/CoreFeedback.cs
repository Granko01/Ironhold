using UnityEngine;

// Makes the core react when it takes damage: the crystal pulses, the camera shakes and it thumps.
[RequireComponent(typeof(Health))]
public class CoreFeedback : MonoBehaviour
{
    [SerializeField] private Transform crystal;
    [SerializeField] private float pulseScale = 0.35f;
    [SerializeField] private float pulseDuration = 0.25f;

    private Vector3 crystalBaseScale;
    private float pulseTimer;

    public void Configure(Transform crystalTransform)
    {
        crystal = crystalTransform;
    }

    private void Awake()
    {
        if (crystal != null) crystalBaseScale = crystal.localScale;
        GetComponent<Health>().Damaged += HandleDamaged;
    }

    private void HandleDamaged(float amount)
    {
        pulseTimer = pulseDuration;
        IsoCameraFollow.AddTrauma(Mathf.Clamp(amount / 100f, 0.12f, 0.5f));
        GameFx.Sparks(transform.position + Vector3.up * 2f, new Color(0.4f, 0.9f, 1f), 8);
        Sfx.Play(SfxId.CoreHit);
    }

    private void Update()
    {
        if (crystal == null || pulseTimer <= 0f) return;

        pulseTimer = Mathf.Max(0f, pulseTimer - Time.deltaTime);
        float t = pulseTimer / pulseDuration;
        crystal.localScale = crystalBaseScale * (1f + pulseScale * t * t);
    }
}
