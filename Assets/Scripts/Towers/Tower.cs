using System.Collections.Generic;
using UnityEngine;

public class Tower : MonoBehaviour
{
    [SerializeField] private TowerKind kind = TowerKind.Gun;
    [SerializeField] private float range = 8f;
    [SerializeField] private float fireRate = 3f;
    [SerializeField] private float damage = 8f;
    [SerializeField] private float turretRotationSpeed = 360f;
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform turretPivot;
    [SerializeField] private SfxId fireSound = SfxId.LightShot;
    [SerializeField] private float firePitch = 1f;

    private const float FireConeDegrees = 12f;

    private static readonly List<Tower> placed = new List<Tower>();

    private float cooldownTimer;
    private Enemy currentTarget;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => placed.Clear();

    public static int CountOf(TowerKind towerKind)
    {
        int count = 0;
        foreach (Tower tower in placed)
        {
            if (tower.kind == towerKind) count++;
        }
        return count;
    }

    private float Range => range * RunModifiers.Range(kind);

    private void OnEnable() => placed.Add(this);

    private void OnDisable() => placed.Remove(this);

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

        bool aimed = AimAtTarget();

        if (aimed && cooldownTimer <= 0f)
        {
            Fire();
            cooldownTimer = 1f / (fireRate * RunModifiers.FireRate(kind));
        }
    }

    private void AcquireTarget()
    {
        float effectiveRange = Range;
        if (currentTarget != null && currentTarget.IsAlive && currentTarget.IsWithin(transform.position, effectiveRange)) return;
        currentTarget = Enemy.FindClosest(transform.position, effectiveRange);
    }

    // Returns true once the turret is facing the target closely enough to fire.
    private bool AimAtTarget()
    {
        if (turretPivot == null) return true;

        Vector3 direction = currentTarget.transform.position - turretPivot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return true;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        turretPivot.rotation = Quaternion.RotateTowards(turretPivot.rotation, targetRotation, turretRotationSpeed * Time.deltaTime);
        return Quaternion.Angle(turretPivot.rotation, targetRotation) <= FireConeDegrees;
    }

    private void Fire()
    {
        if (projectilePrefab == null || firePoint == null) return;

        Projectile.FireVolley(projectilePrefab, firePoint, damage * RunModifiers.Damage(kind), currentTarget, kind);
        Sfx.Play(fireSound, firePitch);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
