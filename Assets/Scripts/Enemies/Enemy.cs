using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackInterval = 1f;
    [SerializeField] private bool dieOnAttack;
    [SerializeField] private int scrapReward = 10;
    [SerializeField] private Animator animator;

    [Header("Feedback")]
    [SerializeField] private Color deathColor = new Color(0.85f, 0.2f, 0.15f);
    [SerializeField] private int deathBurstCount = 14;
    [SerializeField] private float attackShake;
    [SerializeField] private float deathShake;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int AttackParam = Animator.StringToHash("Attack");
    private static readonly int DeathParam = Animator.StringToHash("Death");

    private static readonly List<Enemy> active = new List<Enemy>();

    // Living enemies only - dying/despawning enemies are removed immediately so towers
    // and projectiles stop wasting shots on corpses playing their death animation.
    public static IReadOnlyList<Enemy> Active => active;

    public event Action Despawned;

    public Health Health => health;
    public bool IsAlive => !hasDespawned;

    private Transform coreTarget;
    private Health coreHealth;
    private Health health;
    private bool hasDespawned;
    private float attackTimer;
    private HitFlash hitFlash;
    private float height = 1.7f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    public static Enemy FindClosest(Vector3 position, float range, ICollection<Enemy> exclude = null)
    {
        float closestDistSqr = range * range;
        Enemy closest = null;

        for (int i = 0; i < active.Count; i++)
        {
            Enemy enemy = active[i];
            if (exclude != null && exclude.Contains(enemy)) continue;

            float distSqr = (enemy.transform.position - position).sqrMagnitude;
            if (distSqr <= closestDistSqr)
            {
                closestDistSqr = distSqr;
                closest = enemy;
            }
        }

        return closest;
    }

    public bool IsWithin(Vector3 position, float range)
    {
        return (transform.position - position).sqrMagnitude <= range * range;
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        health.Died += HandleDied;
        health.Damaged += HandleDamaged;
        if (animator == null) animator = GetComponentInChildren<Animator>();

        hitFlash = GetComponent<HitFlash>();
        if (hitFlash == null) hitFlash = gameObject.AddComponent<HitFlash>();

        Collider body = GetComponent<Collider>();
        if (body != null) height = body.bounds.max.y - transform.position.y;
    }

    private void OnEnable()
    {
        if (!hasDespawned) active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    public void Initialize(Transform core)
    {
        coreTarget = core;
        coreHealth = core != null ? core.GetComponent<Health>() : null;
    }

    private void Update()
    {
        if (coreTarget == null || hasDespawned) return;

        attackTimer -= Time.deltaTime;

        Vector3 toCore = coreTarget.position - transform.position;
        toCore.y = 0f;

        if (toCore.sqrMagnitude > attackRange * attackRange)
        {
            transform.position = Vector3.MoveTowards(transform.position, coreTarget.position, moveSpeed * Time.deltaTime);
            if (animator != null) animator.SetFloat(SpeedParam, 1f);
        }
        else
        {
            if (animator != null) animator.SetFloat(SpeedParam, 0f);
            if (attackTimer <= 0f) AttackCore();
        }

        if (toCore.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toCore);
    }

    private void AttackCore()
    {
        attackTimer = attackInterval;
        coreHealth?.TakeDamage(attackDamage);
        if (animator != null) animator.SetTrigger(AttackParam);
        if (attackShake > 0f) IsoCameraFollow.AddTrauma(attackShake);

        if (dieOnAttack)
        {
            Vector3 center = transform.position + Vector3.up * height * 0.5f;
            CombatFx.Blast(center, 1.8f, deathColor);
            GameFx.Debris(center, deathColor, deathBurstCount, 7f);
            Sfx.Play(SfxId.Boom);
            Despawn(0f);
        }
    }

    private void HandleDamaged(float amount)
    {
        hitFlash.Flash();
        DamageNumbers.Show(transform.position + Vector3.up * (height + 0.3f), amount);
    }

    private void HandleDied()
    {
        Vector3 center = transform.position + Vector3.up * height * 0.5f;
        GameFx.Debris(center, deathColor, deathBurstCount, 5f + height);
        Sfx.Play(SfxId.Pop, Mathf.Lerp(1.3f, 0.6f, Mathf.InverseLerp(1.2f, 5f, height)));
        if (deathShake > 0f) IsoCameraFollow.AddTrauma(deathShake);

        GameManager.Instance?.AddScrap(scrapReward, center);
        if (animator != null) animator.SetTrigger(DeathParam);
        Despawn(1f);
    }

    private void Despawn(float delay)
    {
        if (hasDespawned) return;
        hasDespawned = true;
        active.Remove(this);

        foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;

        Despawned?.Invoke();
        Destroy(gameObject, delay);
    }
}
