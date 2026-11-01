using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public event Action Died;
    public event Action<float> Damaged;
    public event Action Revived;

    public bool IsDead => CurrentHealth <= 0f;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void Configure(float newMaxHealth)
    {
        maxHealth = newMaxHealth;
        CurrentHealth = maxHealth;
    }

    public void Heal(float amount)
    {
        if (CurrentHealth <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    // Brings a dead (or hurt) character back at full health, e.g. the hero respawning.
    public void Revive()
    {
        CurrentHealth = maxHealth;
        Revived?.Invoke();
    }

    public void IncreaseMax(float amount)
    {
        maxHealth += amount;
        Heal(amount);
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHealth <= 0f) return;

        float applied = Mathf.Min(amount, CurrentHealth);
        CurrentHealth -= applied;
        Damaged?.Invoke(applied);
        if (CurrentHealth <= 0f)
        {
            Died?.Invoke();
        }
    }
}
