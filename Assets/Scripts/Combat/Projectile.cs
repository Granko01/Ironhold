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

    private readonly List<Enemy> hitEnemies = new List<Enemy>();
    private float damage;
    private Vector3 direction;
    private Enemy target;
    private int piercesLeft;
    private bool spent;
    private TowerKind source = TowerKind.Hero;

    private const float VolleySpreadDegrees = 9f;

    // Fires 1 + upgrade-granted extra projectiles in a fan centred on the fire point's aim.
    public static void FireVolley(Projectile prefab, Transform firePoint, float damage, Enemy target, TowerKind source)
    {
        int count = 1 + RunModifiers.ExtraProjectiles(source);
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) * 0.5f) * VolleySpreadDegrees;
            Quaternion rotation = firePoint.rotation * Quaternion.Euler(0f, angle, 0f);
            Projectile projectile = Instantiate(prefab, firePoint.position, rotation);
            projectile.Launch(rotation * Vector3.forward, damage, target, source);
        }
    }

    public void Launch(Vector3 launchDirection, float launchDamage, Enemy launchTarget = null, TowerKind launchSource = TowerKind.Hero)
    {
        direction = launchDirection.normalized;
        damage = launchDamage;
        target = launchTarget;
        source = launchSource;
        piercesLeft = pierceCount + RunModifiers.ExtraPierce(source);
        Destroy(gameObject, lifetime);
    }

    private float SplashRadius => splashRadius * RunModifiers.Splash(source);

    private void Update()
    {
        if (homing) SteerTowardsTarget();
        transform.position += direction * speed * Time.deltaTime;
    }

    private void SteerTowardsTarget()
    {
        if (target == null || !target.IsAlive)
        {
            target = Enemy.FindClosest(transform.position, 12f);
            if (target == null) return;
        }

        Vector3 aimPoint = target.transform.position + Vector3.up * 0.8f;
        Vector3 desired = (aimPoint - transform.position).normalized;
        direction = Vector3.RotateTowards(direction, desired, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (spent) return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || !enemy.IsAlive || hitEnemies.Contains(enemy)) return;

        hitEnemies.Add(enemy);

        if (splashRadius > 0f)
        {
            Explode(enemy.transform.position, SplashRadius);
        }
        else
        {
            enemy.Health.TakeDamage(damage);
            GameFx.Sparks(transform.position, fxColor);
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
