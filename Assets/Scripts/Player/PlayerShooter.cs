using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float range = 10f;
    [SerializeField] private float fireRate = 4f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private Animator animator;

    private static readonly int ShootParam = Animator.StringToHash("Shoot");

    private float cooldownTimer;
    private bool warnedMissingRefs;
    private bool isDead;
    private Enemy currentTarget;

    public void Configure(Projectile prefab, Transform firePointTransform)
    {
        projectilePrefab = prefab;
        firePoint = firePointTransform;
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        Health health = GetComponent<Health>();
        if (health != null) health.Died += () => isDead = true;
    }

    private void LateUpdate()
    {
        cooldownTimer -= Time.deltaTime;

        if (isDead || TowerPlacer.IsPlacing) return;

        AcquireTarget();
        if (currentTarget == null) return;

        AimAtTarget();

        if (cooldownTimer > 0f) return;

        if (projectilePrefab == null || firePoint == null)
        {
            if (!warnedMissingRefs)
            {
                Debug.LogWarning("[PlayerShooter] Missing projectile prefab or fire point - re-run 'Tools > Iron Hold > 2) Build Prototype Scene'.");
                warnedMissingRefs = true;
            }
            return;
        }

        Fire();
        cooldownTimer = 1f / fireRate;
    }

    private void AcquireTarget()
    {
        if (currentTarget != null && currentTarget.IsAlive && currentTarget.IsWithin(transform.position, range)) return;
        currentTarget = Enemy.FindClosest(transform.position, range);
    }

    private void AimAtTarget()
    {
        Vector3 direction = currentTarget.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void Fire()
    {
        Projectile projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        projectile.Launch(firePoint.forward, damage, currentTarget);
        Sfx.Play(SfxId.Shoot, 1.25f, 0.8f);
        if (animator != null) animator.SetTrigger(ShootParam);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
