using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public event Action Died;

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

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (CurrentHealth <= 0f)
        {
            Died?.Invoke();
        }
    }
}
