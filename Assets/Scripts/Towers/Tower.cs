using UnityEngine;

public class Tower : MonoBehaviour
{
    [SerializeField] private float range = 8f;
    [SerializeField] private float fireRate = 3f;
    [SerializeField] private float damage = 8f;
    [SerializeField] private float turretRotationSpeed = 360f;
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform turretPivot;

    private float cooldownTimer;
    private Enemy currentTarget;

    public void Configure(Projectile prefab, Transform firePointTransform, Transform turretPivotTransform)
    {
        projectilePrefab = prefab;
        firePoint = firePointTransform;
        turretPivot = turretPivotTransform;
    }

    public void ConfigureStats(float newRange, float newFireRate, float newDamage)
    {
        range = newRange;
        fireRate = newFireRate;
        damage = newDamage;
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;
        AcquireTarget();

        if (currentTarget == null) return;

        AimAtTarget();

        if (cooldownTimer <= 0f)
        {
            Fire();
            cooldownTimer = 1f / fireRate;
        }
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
        if (turretPivot == null) return;

        Vector3 direction = currentTarget.transform.position - turretPivot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        turretPivot.rotation = Quaternion.RotateTowards(turretPivot.rotation, targetRotation, turretRotationSpeed * Time.deltaTime);
    }

    private void Fire()
    {
        if (projectilePrefab == null || firePoint == null) return;

        Projectile projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        projectile.Launch(firePoint.forward, damage);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
