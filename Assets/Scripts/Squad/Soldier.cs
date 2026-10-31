using System.Collections.Generic;
using UnityEngine;

// An allied rifleman. Either trails a leader (the hero) in formation or holds a post, and
// shoots the closest enemy in range from wherever it stands.
public class Soldier : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float range = 9f;
    [SerializeField] private float fireRate = 2f;
    [SerializeField] private float damage = 7f;
    [SerializeField] private float fireConeDegrees = 20f;
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Animator animator;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int ShootParam = Animator.StringToHash("Shoot");

    private static readonly List<Soldier> all = new List<Soldier>();

    // Every living soldier, hired or garrisoned - used by crates to see who's standing nearby.
    public static IReadOnlyList<Soldier> All => all;

    private Transform leader;
    private Vector3 offset;
    private Vector3 post;
    private Enemy target;
    private float cooldownTimer;

    public void Configure(Projectile projectile, Transform firePointTransform)
    {
        projectilePrefab = projectile;
        firePoint = firePointTransform;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        post = transform.position;
    }

    public void SetRange(float value) => range = value;

    public void Follow(Transform leaderTransform, Vector3 formationOffset)
    {
        leader = leaderTransform;
        offset = formationOffset;
    }

    public void HoldPosition(Vector3 position)
    {
        leader = null;
        post = position;
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;

        Vector3 destination = leader != null ? leader.position + offset : post;
        Vector3 toDestination = destination - transform.position;
        toDestination.y = 0f;
        float distance = toDestination.magnitude;

        float speed = 0f;
        if (distance > 0.25f)
        {
            // Jog faster to catch up when the formation has been left behind.
            speed = moveSpeed * (distance > 4f ? 1.4f : 1f);
            transform.position += toDestination / distance * Mathf.Min(speed * Time.deltaTime, distance);
        }
        if (animator != null) animator.SetFloat(SpeedParam, speed / moveSpeed);

        AcquireTarget();
        if (target != null)
        {
            float angle = Face(target.transform.position - transform.position);
            if (cooldownTimer <= 0f && angle <= fireConeDegrees) Fire();
        }
        else if (speed > 0f)
        {
            Face(toDestination);
        }
    }

    private void AcquireTarget()
    {
        if (target != null && target.IsAlive && target.IsWithin(transform.position, range)) return;
        target = Enemy.FindClosest(transform.position, range);
    }

    private float Face(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return 0f;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        return Quaternion.Angle(transform.rotation, targetRotation);
    }

    private void Fire()
    {
        cooldownTimer = 1f / (fireRate * RunModifiers.FireRate(TowerKind.Hero));
        if (projectilePrefab == null || firePoint == null) return;

        Projectile.FireVolley(projectilePrefab, firePoint, damage * RunModifiers.Damage(TowerKind.Hero), target, TowerKind.Hero);
        Sfx.Play(SfxId.Shoot, 1.45f, 0.45f);
        if (animator != null) animator.SetTrigger(ShootParam);
    }
}
