using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float range = 10f;
    [SerializeField] private float fireRate = 4f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float rotationSpeed = 720f;

    private float cooldownTimer;
    private bool warnedMissingRefs;
    private Enemy currentTarget;

    public void Configure(Projectile prefab, Transform firePointTransform)
    {
        projectilePrefab = prefab;
        firePoint = firePointTransform;
    }

    private void LateUpdate()
    {
        cooldownTimer -= Time.deltaTime;

        if (TowerPlacer.IsPlacing) return;

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
        if (currentTarget != null)
        {
            float distSqr = (currentTarget.transform.position - transform.position).sqrMagnitude;
            if (distSqr <= range * range) return;
            currentTarget = null;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        float closestDistSqr = float.MaxValue;
        Enemy closest = null;

        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null) continue;

            float distSqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closest = enemy;
            }
        }

        currentTarget = closest;
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
        projectile.Launch(firePoint.forward, damage);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
