using System;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private int scrapReward = 10;

    public event Action Despawned;

    private Transform coreTarget;
    private Health health;
    private bool hasDespawned;

    private void Awake()
    {
        health = GetComponent<Health>();
        health.Died += HandleDied;
    }

    public void Initialize(Transform core)
    {
        coreTarget = core;
    }

    private void Update()
    {
        if (coreTarget == null || hasDespawned) return;

        transform.position = Vector3.MoveTowards(transform.position, coreTarget.position, moveSpeed * Time.deltaTime);

        float distance = Vector3.Distance(transform.position, coreTarget.position);
        if (distance <= attackRange)
        {
            AttackCore();
        }
    }

    private void AttackCore()
    {
        Health coreHealth = coreTarget.GetComponent<Health>();
        coreHealth?.TakeDamage(attackDamage);
        Despawn();
    }

    private void HandleDied()
    {
        GameManager.Instance?.AddScrap(scrapReward);
        Despawn();
    }

    private void Despawn()
    {
        if (hasDespawned) return;
        hasDespawned = true;
        Despawned?.Invoke();
        Destroy(gameObject);
    }
}
