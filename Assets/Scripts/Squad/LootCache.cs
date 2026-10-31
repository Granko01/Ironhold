using UnityEngine;

// A supply crate dropped somewhere on the map. Whoever stands next to it (hero or soldier) pries it
// open over a moment and it pays out scrap; left alone too long, it vanishes.
public class LootCache : MonoBehaviour
{
    [SerializeField] private float collectRadius = 1.8f;
    [SerializeField] private float collectSeconds = 1.2f;
    [SerializeField] private float lifetime = 45f;
    [SerializeField] private Transform progressRing;
    [SerializeField] private Transform beam;

    private int reward;
    private float progress;
    private float age;
    private Transform hero;
    private Vector3 beamBaseScale;

    public void Configure(Transform progressRingTransform, Transform beamTransform)
    {
        progressRing = progressRingTransform;
        beam = beamTransform;
    }

    public void Initialize(int scrapReward, Transform heroTransform)
    {
        reward = scrapReward;
        hero = heroTransform;
    }

    private void Awake()
    {
        if (beam != null) beamBaseScale = beam.localScale;
        if (progressRing != null) progressRing.localScale = Vector3.zero;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            GameFx.Dust(transform.position, 8);
            Destroy(gameObject);
            return;
        }

        bool claimed = IsNear(hero) || SoldierNearby();
        progress = Mathf.MoveTowards(progress, claimed ? collectSeconds : 0f, Time.deltaTime);
        float t = progress / collectSeconds;
        if (progressRing != null) progressRing.localScale = new Vector3(t * collectRadius * 2f, 1f, t * collectRadius * 2f);

        if (t >= 1f)
        {
            Collect();
            return;
        }

        // Beam pulses so the crate is easy to spot, and flickers in its last seconds.
        if (beam != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.15f;
            bool expiring = lifetime - age < 8f;
            beam.gameObject.SetActive(!expiring || Mathf.Repeat(Time.time * 4f, 1f) > 0.3f);
            beam.localScale = new Vector3(beamBaseScale.x * pulse, beamBaseScale.y, beamBaseScale.z * pulse);
        }
    }

    private bool IsNear(Transform other)
    {
        if (other == null) return false;
        Vector3 delta = other.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= collectRadius * collectRadius;
    }

    private bool SoldierNearby()
    {
        foreach (Soldier soldier in Soldier.All)
        {
            if (IsNear(soldier.transform)) return true;
        }
        return false;
    }

    private void Collect()
    {
        Vector3 center = transform.position + Vector3.up * 0.6f;
        GameManager.Instance?.AddScrap(reward, center);
        GameFx.Debris(center, new Color(1f, 0.82f, 0.3f), 18, 6f, 0.2f);
        Sfx.Play(SfxId.Coin, 1.1f);
        Destroy(gameObject);
    }
}
