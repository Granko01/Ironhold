using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 3f;

    [Header("Homing")]
    [SerializeField] private bool homing;
    [SerializeField] private float turnRate = 540f;

    [Header("Splash")]
    [SerializeField] private float splashRadius;
    [SerializeField, Range(0f, 1f)] private float splashFalloff = 0.5f;

    [Header("Pierce")]
    [SerializeField] private int pierceCount;

    [Header("Chain")]
    [SerializeField] private int chainCount;
    [SerializeField] private float chainRange = 4f;
    [SerializeField, Range(0f, 1f)] private float chainDamageFalloff = 0.75f;

    [SerializeField] private Color fxColor = new Color(1f, 0.6f, 0.2f);

    // Gunfire: a muzzle flash and an ejected casing when fired, blood when it hits flesh.
    [SerializeField] private bool isBullet;

    private readonly List<Enemy> hitEnemies = new List<Enemy>();
    private float damage;
    private Vector3 direction;
    private Enemy target;
    private int piercesLeft;
    private bool spent;
    private TowerKind source = TowerKind.Hero;

    // Lob over walls: the shot follows its straight line plus a parabolic lift that peaks high
    // enough to clear the stone between shooter and target.
    private float arcHeight;
    private float arcDistance;
    private float traveled;
    private Vector3 linePosition;
    private Wall ignoreWall;

    private const float VolleySpreadDegrees = 9f;
    // Hits are found by sweeping a small sphere along each frame's travel, so fast rounds can't
    // skip past a target between frames.
    private const float HitRadius = 0.14f;
    private const float WallCheckStep = 0.75f;

    private static readonly Color BloodColor = new Color(0.55f, 0.06f, 0.05f);
    private static readonly Color BrassColor = new Color(0.95f, 0.72f, 0.3f);
    private static readonly RaycastHit[] sweepHits = new RaycastHit[16];

    // Fires 1 + upgrade-granted extra projectiles in a fan centred on the fire point's aim.
    public static void FireVolley(Projectile prefab, Transform firePoint, float damage, Enemy target, TowerKind source)
    {
        FireVolley(prefab, firePoint.position, AimWithPitch(firePoint, target), damage, target, source);
    }

    // Same fan, from an explicit position and aim (first-person shots aim along the crosshair).
    public static void FireVolley(Projectile prefab, Vector3 origin, Quaternion aim, float damage, Enemy target, TowerKind source)
    {
        if (prefab.isBullet)
        {
            Vector3 forward = aim * Vector3.forward;
            CombatFx.MuzzleFlash(origin, forward, prefab.fxColor);
            GameFx.Debris(origin + aim * new Vector3(0.12f, 0.05f, -0.25f), BrassColor, 1, 2.5f, 0.05f);
        }

        int count = 1 + RunModifiers.ExtraProjectiles(source);
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) * 0.5f) * VolleySpreadDegrees;
            Quaternion rotation = aim * Quaternion.Euler(0f, angle, 0f);
            Projectile projectile = Instantiate(prefab, origin, rotation);
            projectile.Launch(rotation * Vector3.forward, damage, target, source);
        }
    }

    // Keeps the shooter's facing (yaw) but tilts the shot up/down at the target's body centre.
    // Shots used to fly dead level from the muzzle, passing over short enemies (a muzzle held at
    // chest height clears a Runner's head) and under nothing at all from a watchtower.
    private static Quaternion AimWithPitch(Transform firePoint, Enemy target)
    {
        Vector3 forward = firePoint.forward;
        forward.y = 0f;
        if (target == null || forward.sqrMagnitude < 0.0001f) return firePoint.rotation;

        Vector3 toTarget = target.AimPoint - firePoint.position;
        float horizontal = new Vector2(toTarget.x, toTarget.z).magnitude;
        if (horizontal < 0.1f) return firePoint.rotation;

        float pitch = Mathf.Atan2(toTarget.y, horizontal);
        Vector3 direction = forward.normalized * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
        return Quaternion.LookRotation(direction);
    }

    public void Launch(Vector3 launchDirection, float launchDamage, Enemy launchTarget = null, TowerKind launchSource = TowerKind.Hero)
    {
        direction = launchDirection.normalized;
        damage = launchDamage;
        target = launchTarget;
        source = launchSource;
        piercesLeft = pierceCount + RunModifiers.ExtraPierce(source);
        // A guard firing from their own watchtower shouldn't have their shots stopped by it.
        ignoreWall = Wall.FindUnder(transform.position);
        if (!homing && target != null) PlanArc();
        Destroy(gameObject, lifetime);
    }

    private void PlanArc()
    {
        Vector3 from = transform.position;
        Vector3 to = target.AimPoint;
        float arc = Wall.RequiredArc(from, to, ignoreWall);
        if (arc <= 0f) return;

        // Capped so a target pressed flat against the far face doesn't send shots into the sky;
        // those rare shots just stop at the wall instead.
        arcHeight = Mathf.Min(arc, 6f);
        arcDistance = Mathf.Max(0.5f, Vector3.Distance(from, to));
        linePosition = from;
    }

    private float SplashRadius => splashRadius * RunModifiers.Splash(source);

    private void Update()
    {
        if (homing) SteerTowardsTarget();

        Vector3 previous = transform.position;
        float step = speed * Time.deltaTime;
        if (arcHeight > 0f)
        {
            linePosition += direction * step;
            traveled += step;
            float t = traveled / arcDistance;
            float lift = t < 1f ? arcHeight * 4f * t * (1f - t) : 0f;
            Vector3 next = linePosition + Vector3.up * lift;
            Vector3 motion = next - transform.position;
            if (motion.sqrMagnitude > 0.000001f) transform.rotation = Quaternion.LookRotation(motion);
            transform.position = next;
        }
        else
        {
            transform.position += direction * step;
        }

        SweepForHits(previous, transform.position);
    }

    // Everything between last frame's position and this one: enemies in order along the path
    // (so pierce goes front to back), then stone.
    private void SweepForHits(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.0001f) return;
        Vector3 dir = delta / distance;

        int count = Physics.SphereCastNonAlloc(from, HitRadius, dir, sweepHits, distance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(sweepHits, 0, count, HitDistanceComparer.Instance);
        for (int i = 0; i < count && !spent; i++)
        {
            Enemy enemy = sweepHits[i].collider.GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            Vector3 point = sweepHits[i].distance > 0f ? sweepHits[i].point : from;
            HitEnemy(enemy, point);
        }
        if (spent) return;

        int samples = Mathf.Max(1, Mathf.CeilToInt(distance / WallCheckStep));
        for (int i = 1; i <= samples; i++)
        {
            Vector3 point = from + delta * (i / (float)samples);
            if (!Wall.IsInsideSolid(point, ignoreWall)) continue;
            transform.position = point;
            HitWall();
            return;
        }
    }

    private class HitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    // Shots that still meet stone (e.g. aimed straight at a wall) stop there instead of passing through.
    private void HitWall()
    {
        spent = true;
        if (splashRadius > 0f)
        {
            Explode(transform.position, SplashRadius);
        }
        else
        {
            GameFx.Sparks(transform.position, new Color(0.75f, 0.72f, 0.66f), 5);
            Sfx.Play(SfxId.Ricochet);
        }
        Destroy(gameObject);
    }

    private void SteerTowardsTarget()
    {
        if (target == null || !target.IsAlive)
        {
            target = Enemy.FindClosest(transform.position, 12f);
            if (target == null) return;
        }

        // Missiles climb over any wall in the way, then dive once the line to the target is clear.
        Vector3 aimPoint = target.AimPoint;
        float needed = Wall.ClearanceNeeded(transform.position, aimPoint, ignoreWall, out _);
        if (needed > 0f) aimPoint += Vector3.up * (needed + 1f);
        Vector3 desired = (aimPoint - transform.position).normalized;
        direction = Vector3.RotateTowards(direction, desired, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void HitEnemy(Enemy enemy, Vector3 point)
    {
        if (spent || !enemy.IsAlive || hitEnemies.Contains(enemy)) return;

        hitEnemies.Add(enemy);

        if (splashRadius > 0f)
        {
            Explode(enemy.transform.position, SplashRadius);
        }
        else
        {
            enemy.Health.TakeDamage(damage);
            if (isBullet)
            {
                GameFx.Debris(point, BloodColor, 5, 3f, 0.1f);
                GameFx.Sparks(point, fxColor, 2);
            }
            else
            {
                GameFx.Sparks(point, fxColor);
            }
            Sfx.Play(SfxId.Hit);
        }

        int chains = chainCount > 0 ? chainCount + RunModifiers.ExtraChains(source) : 0;
        if (chains > 0) Chain(enemy, chains);

        if (piercesLeft > 0)
        {
            piercesLeft--;
            return;
        }

        spent = true;
        Destroy(gameObject);
    }

    private void Explode(Vector3 center, float radius)
    {
        CombatFx.Blast(center + Vector3.up * 0.5f, radius, fxColor);
        GameFx.Debris(center + Vector3.up * 0.3f, fxColor, 10, 6f, 0.2f);
        IsoCameraFollow.AddTrauma(0.12f + radius * 0.03f);
        Sfx.Play(SfxId.Boom, 1.4f - radius * 0.15f, 0.7f);

        // Copy first: TakeDamage can kill enemies, which removes them from Enemy.Active.
        var victims = new List<Enemy>();
        foreach (Enemy enemy in Enemy.Active)
        {
            if (enemy.IsWithin(center, radius)) victims.Add(enemy);
        }

        foreach (Enemy enemy in victims)
        {
            float distance01 = Vector3.Distance(enemy.transform.position, center) / radius;
            enemy.Health.TakeDamage(damage * Mathf.Lerp(1f, splashFalloff, distance01));
        }
    }

    private void Chain(Enemy firstHit, int chains)
    {
        var chained = new List<Enemy> { firstHit };
        Enemy from = firstHit;
        float chainDamage = damage;
        Vector3 fromPos = firstHit.transform.position + Vector3.up;

        if (splashRadius <= 0f) CombatFx.Line(transform.position, fromPos, fxColor);
        Sfx.Play(SfxId.Zap);

        for (int i = 0; i < chains; i++)
        {
            Enemy next = Enemy.FindClosest(from.transform.position, chainRange, chained);
            if (next == null) break;

            chainDamage *= chainDamageFalloff;
            Vector3 nextPos = next.transform.position + Vector3.up;
            CombatFx.Line(fromPos, nextPos, fxColor);
            next.Health.TakeDamage(chainDamage);

            chained.Add(next);
            from = next;
            fromPos = nextPos;
        }
    }
}
