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
    private int shotsLeftInBurst;
    private bool moving;

    // Kneeling: when holding a post, a soldier can drop to one knee. Applied after the Animator
    // each frame (it would otherwise put the legs back), blended in so it doesn't snap.
    private bool kneeling;
    private float kneelBlend;
    private Transform hips;
    private Transform legL;
    private Transform legR;
    private Transform kneeL;
    private Transform kneeR;
    private float hipsRestY;
    private Vector3 firePointRest;

    private static readonly Quaternion KneelLegL = Quaternion.Euler(-85f, 0f, 0f);
    private static readonly Quaternion KneelKneeL = Quaternion.Euler(85f, 0f, 0f);
    private static readonly Quaternion KneelLegR = Quaternion.Euler(8f, 0f, 0f);
    private static readonly Quaternion KneelKneeR = Quaternion.Euler(95f, 0f, 0f);
    private const float KneelHipsFactor = 0.57f;

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

        hips = FindPart("Hips");
        legL = FindPart("LegL");
        legR = FindPart("LegR");
        kneeL = FindPart("LegL_Knee");
        kneeR = FindPart("LegR_Knee");
        if (hips != null) hipsRestY = hips.localPosition.y;
        if (firePoint != null) firePointRest = firePoint.localPosition;

        // Out of step with every other soldier from the first shot.
        cooldownTimer = Random.Range(0f, 0.8f);
        shotsLeftInBurst = Random.Range(2, 5);
    }

    private Transform FindPart(string partName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            if (t.name == partName) return t;
        }
        return null;
    }

    public void SetKneeling(bool kneel) => kneeling = kneel;

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
        moving = speed > 0f;

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

    // Kneels only while posted and standing still; stands up to move.
    private void LateUpdate()
    {
        bool kneel = kneeling && leader == null && !moving;
        kneelBlend = Mathf.MoveTowards(kneelBlend, kneel ? 1f : 0f, Time.deltaTime * 4f);
        if (hips == null) return;
        if (kneelBlend <= 0f)
        {
            if (firePoint != null) firePoint.localPosition = firePointRest;
            return;
        }

        float k = kneelBlend * kneelBlend * (3f - 2f * kneelBlend);
        Vector3 hipsPos = hips.localPosition;
        float drop = hipsRestY * (1f - KneelHipsFactor) * k;
        hips.localPosition = new Vector3(hipsPos.x, hipsPos.y - drop, hipsPos.z);
        if (legL != null) legL.localRotation = Quaternion.Slerp(legL.localRotation, KneelLegL, k);
        if (kneeL != null) kneeL.localRotation = Quaternion.Slerp(kneeL.localRotation, KneelKneeL, k);
        if (legR != null) legR.localRotation = Quaternion.Slerp(legR.localRotation, KneelLegR, k);
        if (kneeR != null) kneeR.localRotation = Quaternion.Slerp(kneeR.localRotation, KneelKneeR, k);
        // Shots leave the lowered muzzle, not standing height.
        if (firePoint != null) firePoint.localPosition = firePointRest + Vector3.down * drop;
    }

    // Fires in short bursts with uneven timing, then pauses as if re-aiming - so a line of
    // soldiers never shoots in lockstep.
    private void Fire()
    {
        cooldownTimer = 1f / (fireRate * RunModifiers.FireRate(TowerKind.Hero)) * Random.Range(0.8f, 1.25f);
        shotsLeftInBurst--;
        if (shotsLeftInBurst <= 0)
        {
            cooldownTimer += Random.Range(0.5f, 1.1f);
            shotsLeftInBurst = Random.Range(2, 5);
        }
        if (projectilePrefab == null || firePoint == null) return;

        Projectile.FireVolley(projectilePrefab, firePoint, damage * RunModifiers.Damage(TowerKind.Hero), target, TowerKind.Hero);
        Sfx.Play(SfxId.Shoot, 1.45f, 0.45f);
        if (animator != null) animator.SetTrigger(ShootParam);
    }
}
