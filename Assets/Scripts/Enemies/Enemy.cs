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

    // Middle of the body - where shots should aim so they connect with short and tall enemies alike.
    public Vector3 AimPoint => transform.position + Vector3.up * height * 0.5f;

    private Transform coreTarget;
    private Health coreHealth;
    private Health health;
    private bool hasDespawned;
    private float attackTimer;
    private HitFlash hitFlash;
    private float height = 1.7f;

    // How close an enemy's body gets to a wall's face before it stops to attack.
    private const float WallReach = 0.45f;
    private const float EmergeSeconds = 0.8f;
    private static readonly Color DirtColor = new Color(0.36f, 0.26f, 0.16f);

    private const float HeroAggroRange = 6f;
    private const float HeroLeashRange = 9f;

    private float emergeTimer;
    private bool chasingHero;

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

    public void Initialize(Transform core, bool emergeFromGround = false)
    {
        coreTarget = core;
        coreHealth = core != null ? core.GetComponent<Health>() : null;

        if (emergeFromGround)
        {
            emergeTimer = EmergeSeconds;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, -height, p.z);
            transform.rotation = Quaternion.LookRotation(Flat(core.position - p));
            GameFx.Dust(p, 16);
            GameFx.Debris(p + Vector3.up * 0.2f, DirtColor, 8, 4f, 0.18f);
        }
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private void Update()
    {
        if (coreTarget == null || hasDespawned) return;

        // Climbing out of the dirt: rise in place, no walking or attacking yet.
        if (emergeTimer > 0f)
        {
            emergeTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(emergeTimer / EmergeSeconds);
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, Mathf.Lerp(-height, 0f, 1f - (1f - t) * (1f - t)), p.z);
            if (animator != null) animator.SetFloat(SpeedParam, 0f);
            return;
        }

        attackTimer -= Time.deltaTime;

        // The hero gets chased when they come close; otherwise everything marches on the core.
        PlayerController hero = UpdateHeroAggro();
        Vector3 targetPosition = hero != null ? hero.transform.position : coreTarget.position;
        Health targetHealth = hero != null ? hero.Health : coreHealth;
        float reach = hero != null ? Mathf.Max(0.9f, attackRange * 0.8f) : attackRange;

        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        // A wall in the way gets smashed first; the target is only reachable once it falls.
        Wall wall = toTarget.sqrMagnitude > 0.0001f ? Wall.FindBlocking(transform.position, toTarget.normalized, WallReach) : null;
        if (wall != null)
        {
            if (animator != null) animator.SetFloat(SpeedParam, 0f);
            if (attackTimer <= 0f) Attack(wall.Health);

            Vector3 toWall = wall.ClosestPoint(transform.position) - transform.position;
            toWall.y = 0f;
            if (toWall.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toWall);
            return;
        }

        if (toTarget.sqrMagnitude > reach * reach)
        {
            // Flattened so enemies stay on the ground instead of drifting up to the core's centre height.
            Vector3 destination = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
            transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
            if (animator != null) animator.SetFloat(SpeedParam, 1f);
        }
        else
        {
            if (animator != null) animator.SetFloat(SpeedParam, 0f);
            if (attackTimer <= 0f) Attack(targetHealth);
        }

        if (toTarget.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toTarget);
    }

    // Starts chasing a living hero who comes within aggro range, and gives up (back to the core)
    // once they get clear - the wider give-up range stops enemies flip-flopping at the edge.
    private PlayerController UpdateHeroAggro()
    {
        PlayerController hero = PlayerController.Instance;
        if (hero == null || hero.IsDead)
        {
            chasingHero = false;
            return null;
        }

        float range = chasingHero ? HeroLeashRange : HeroAggroRange;
        chasingHero = IsWithin(hero.transform.position, range);
        return chasingHero ? hero : null;
    }

    private void Attack(Health victim)
    {
        attackTimer = attackInterval;
        victim?.TakeDamage(attackDamage);
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

        GameManager.Instance?.AddScrap(Mathf.RoundToInt(scrapReward * RunModifiers.ScrapMultiplier), center);

        // Varied deaths for regular-sized enemies: the stock forward collapse, being blown off
        // their feet, or spinning round and crumpling. Scripted enemies (component disabled, e.g.
        // in the trailer) and the boss keep the stock animation so scenes stay in control.
        int style = enabled && height < 3f ? UnityEngine.Random.Range(0, 3) : 0;
        if (style == 0)
        {
            if (animator != null) animator.SetTrigger(DeathParam);
        }
        else
        {
            StartCoroutine(FallOver(backward: style == 1));
        }
        StartCoroutine(SinkAway());
        Despawn(CorpseSeconds);
    }

    private const float CorpseSeconds = 2.2f;

    private System.Collections.IEnumerator FallOver(bool backward)
    {
        if (animator != null) animator.enabled = false;
        Transform armL = FindPart("ArmL");
        Transform armR = FindPart("ArmR");
        Transform kneeL = FindPart("LegL_Knee");
        Transform kneeR = FindPart("LegR_Knee");

        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 endPos;
        Quaternion endRot;
        Quaternion armLEnd;
        Quaternion armREnd;
        float kneeBend;
        float duration;

        if (backward)
        {
            // Thrown onto their back, arms flung over their head.
            endRot = startRot * Quaternion.Euler(-88f, 0f, 0f);
            endPos = startPos - transform.forward * 1.1f + Vector3.up * 0.12f;
            armLEnd = Quaternion.Euler(-165f, 0f, -30f);
            armREnd = Quaternion.Euler(-160f, 0f, 30f);
            kneeBend = 25f;
            duration = 0.42f;
        }
        else
        {
            // Twisted round by the hit, folding over sideways with the knees buckling.
            endRot = startRot * Quaternion.Euler(0f, 150f * side, 0f) * Quaternion.Euler(0f, 0f, 82f * side);
            endPos = startPos + transform.right * side * 0.5f + Vector3.up * 0.1f;
            armLEnd = Quaternion.Euler(-40f, 0f, -70f);
            armREnd = Quaternion.Euler(-70f, 0f, 60f);
            kneeBend = 70f;
            duration = 0.55f;
        }

        Quaternion armLStart = armL != null ? armL.localRotation : Quaternion.identity;
        Quaternion armRStart = armR != null ? armR.localRotation : Quaternion.identity;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float fall = k * k; // accelerates like gravity
            float hop = backward ? Mathf.Sin(k * Mathf.PI) * 0.3f : 0f;
            transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, k) + Vector3.up * hop, Quaternion.Slerp(startRot, endRot, fall));
            if (armL != null) armL.localRotation = Quaternion.Slerp(armLStart, armLEnd, k);
            if (armR != null) armR.localRotation = Quaternion.Slerp(armRStart, armREnd, k);
            if (kneeL != null) kneeL.localRotation = Quaternion.Euler(kneeBend * k, 0f, 0f);
            if (kneeR != null) kneeR.localRotation = Quaternion.Euler(kneeBend * 0.7f * k, 0f, 0f);
            yield return null;
        }
        transform.SetPositionAndRotation(endPos, endRot);
    }

    // The body lies a moment, then settles into the ground instead of vanishing.
    private System.Collections.IEnumerator SinkAway()
    {
        yield return new WaitForSeconds(CorpseSeconds - 0.6f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            transform.position += Vector3.down * (0.75f * Time.deltaTime);
            yield return null;
        }
    }

    private Transform FindPart(string partName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            if (t.name == partName) return t;
        }
        return null;
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
