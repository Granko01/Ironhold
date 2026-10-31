using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private HeroWeapon[] weapons;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float rotationSpeed = 1080f;
    [SerializeField] private float fireConeDegrees = 15f;
    [SerializeField] private Animator animator;

    private static readonly int ShootParam = Animator.StringToHash("Shoot");

    private float cooldownTimer;
    private bool warnedMissingRefs;
    private bool isDead;
    private Enemy currentTarget;
    private int equippedIndex;

    // PlayerController leaves facing to the shooter while it has something to aim at.
    public bool HasTarget => currentTarget != null && !isDead;
    public HeroWeaponKind EquippedKind => Equipped != null ? Equipped.kind : HeroWeaponKind.Rifle;

    private HeroWeapon Equipped => weapons != null && equippedIndex < weapons.Length ? weapons[equippedIndex] : null;

    public void Configure(HeroWeapon[] heroWeapons, Transform firePointTransform)
    {
        weapons = heroWeapons;
        firePoint = firePointTransform;
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        Health health = GetComponent<Health>();
        if (health != null)
        {
            health.Died += () => isDead = true;
            health.Revived += () => isDead = false;
        }

        Equip(HeroWeaponKind.Rifle);
    }

    public void Equip(HeroWeaponKind kind)
    {
        if (weapons == null) return;

        for (int i = 0; i < weapons.Length; i++)
        {
            bool match = weapons[i].kind == kind;
            if (match) equippedIndex = i;
            if (weapons[i].model != null) weapons[i].model.SetActive(match);
        }

        currentTarget = null;
        cooldownTimer = 0.2f;
    }

    private void LateUpdate()
    {
        cooldownTimer -= Time.deltaTime;

        HeroWeapon weapon = Equipped;
        if (isDead || TowerPlacer.IsPlacing || weapon == null)
        {
            currentTarget = null;
            return;
        }

        AcquireTarget(weapon.range);
        if (currentTarget == null) return;

        float angleToTarget = AimAtTarget();

        // Waiting until the hero roughly faces the target stops shots spraying sideways mid-turn.
        if (cooldownTimer > 0f || angleToTarget > fireConeDegrees) return;

        if (weapon.projectilePrefab == null || firePoint == null)
        {
            if (!warnedMissingRefs)
            {
                Debug.LogWarning("[PlayerShooter] Missing projectile prefab or fire point - re-run 'Tools > Iron Hold > 2) Build Prototype Scene'.");
                warnedMissingRefs = true;
            }
            return;
        }

        Fire(weapon);
        cooldownTimer = 1f / (weapon.fireRate * RunModifiers.FireRate(TowerKind.Hero));
    }

    private void AcquireTarget(float range)
    {
        if (currentTarget != null && currentTarget.IsAlive && currentTarget.IsWithin(transform.position, range)) return;
        currentTarget = Enemy.FindClosest(transform.position, range);
    }

    // Returns the remaining yaw (degrees) between the hero's facing and the target.
    private float AimAtTarget()
    {
        Vector3 direction = currentTarget.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return 0f;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        return Quaternion.Angle(transform.rotation, targetRotation);
    }

    private void Fire(HeroWeapon weapon)
    {
        Projectile.FireVolley(weapon.projectilePrefab, firePoint, weapon.damage * RunModifiers.Damage(TowerKind.Hero), currentTarget, TowerKind.Hero);
        Sfx.Play(weapon.fireSound, weapon.firePitch, weapon.fireVolume);
        if (animator != null) animator.SetTrigger(ShootParam);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, Equipped != null ? Equipped.range : 10f);
    }
}
