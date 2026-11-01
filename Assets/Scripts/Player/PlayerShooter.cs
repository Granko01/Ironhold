using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private HeroWeapon[] weapons;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float rotationSpeed = 1080f;
    [SerializeField] private float fireConeDegrees = 15f;
    [SerializeField] private Animator animator;

    [Header("First Person")]
    [SerializeField] private float aimAssistDegrees = 5f;
    [SerializeField] private float recoilKickDegrees = 1.2f;

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

        if (FirstPersonView.IsActive)
        {
            // Manual aim: no auto-targeting, fire only while FIRE (or Space) is held.
            currentTarget = null;
            if (cooldownTimer <= 0f && IsFireHeld() && weapon.projectilePrefab != null && firePoint != null)
            {
                FireFirstPerson(weapon);
                cooldownTimer = 1f / (weapon.fireRate * RunModifiers.FireRate(TowerKind.Hero));
            }
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

    private static bool IsFireHeld()
    {
        if (FireButton.IsHeld) return true;
        Keyboard kb = Keyboard.current;
        return kb != null && kb.spaceKey.isPressed;
    }

    // Shoots from the muzzle toward whatever is under the crosshair. A zombie within a few degrees
    // of the crosshair gets the shot instead (aim assist), since thumbs aren't precise.
    private void FireFirstPerson(HeroWeapon weapon)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 eye = cam.transform.position;
        Vector3 look = cam.transform.forward;
        float maxDistance = weapon.range * 2.5f;

        Enemy assisted = FindAimAssistTarget(eye, look, maxDistance);
        Vector3 aimPoint;
        if (assisted != null) aimPoint = assisted.AimPoint;
        else if (Physics.Raycast(eye, look, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Ignore)) aimPoint = hit.point;
        else aimPoint = eye + look * maxDistance;

        Vector3 direction = aimPoint - firePoint.position;
        if (direction.sqrMagnitude < 0.0001f) direction = look;

        Projectile.FireVolley(weapon.projectilePrefab, firePoint.position, Quaternion.LookRotation(direction),
            weapon.damage * RunModifiers.Damage(TowerKind.Hero), assisted, TowerKind.Hero);
        Sfx.Play(weapon.fireSound, weapon.firePitch, weapon.fireVolume);
        if (animator != null) animator.SetTrigger(ShootParam);
        FirstPersonView.Instance.Kick(recoilKickDegrees);
    }

    private Enemy FindAimAssistTarget(Vector3 eye, Vector3 look, float maxDistance)
    {
        Enemy best = null;
        float bestAngle = aimAssistDegrees;
        foreach (Enemy enemy in Enemy.Active)
        {
            Vector3 toEnemy = enemy.AimPoint - eye;
            if (toEnemy.sqrMagnitude > maxDistance * maxDistance) continue;

            float angle = Vector3.Angle(look, toEnemy);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = enemy;
            }
        }
        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, Equipped != null ? Equipped.range : 10f);
    }
}
