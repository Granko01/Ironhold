using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public event Action Died;
    public event Action<float> Damaged;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void Configure(float newMaxHealth)
    {
        maxHealth = newMaxHealth;
        CurrentHealth = maxHealth;
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
