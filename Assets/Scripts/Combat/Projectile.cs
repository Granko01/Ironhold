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

    public void Launch(Vector3 launchDirection, float launchDamage, Enemy launchTarget = null)
    {
        direction = launchDirection.normalized;
        damage = launchDamage;
        target = launchTarget;
        piercesLeft = pierceCount;
        Destroy(gameObject, lifetime);
    }

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
            Explode(enemy.transform.position);
        }
        else
        {
            enemy.Health.TakeDamage(damage);
            GameFx.Sparks(transform.position, fxColor);
            Sfx.Play(SfxId.Hit);
        }

        if (chainCount > 0) Chain(enemy);

        if (piercesLeft > 0)
        {
            piercesLeft--;
            return;
        }

        spent = true;
        Destroy(gameObject);
    }

    private void Explode(Vector3 center)
    {
        CombatFx.Blast(center + Vector3.up * 0.5f, splashRadius, fxColor);
        GameFx.Debris(center + Vector3.up * 0.3f, fxColor, 10, 6f, 0.2f);
        IsoCameraFollow.AddTrauma(0.12f + splashRadius * 0.03f);
        Sfx.Play(SfxId.Boom, 1.4f - splashRadius * 0.15f, 0.7f);

        // Copy first: TakeDamage can kill enemies, which removes them from Enemy.Active.
        var victims = new List<Enemy>();
        foreach (Enemy enemy in Enemy.Active)
        {
            if (enemy.IsWithin(center, splashRadius)) victims.Add(enemy);
        }

        foreach (Enemy enemy in victims)
        {
            float distance01 = Vector3.Distance(enemy.transform.position, center) / splashRadius;
            enemy.Health.TakeDamage(damage * Mathf.Lerp(1f, splashFalloff, distance01));
        }
    }

    private void Chain(Enemy firstHit)
    {
        var chained = new List<Enemy> { firstHit };
        Enemy from = firstHit;
        float chainDamage = damage;
        Vector3 fromPos = firstHit.transform.position + Vector3.up;

        if (splashRadius <= 0f) CombatFx.Line(transform.position, fromPos, fxColor);
        Sfx.Play(SfxId.Zap);

        for (int i = 0; i < chainCount; i++)
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
