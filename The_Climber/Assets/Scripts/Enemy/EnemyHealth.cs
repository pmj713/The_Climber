using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 30;
    [SerializeField] private float hitStunDuration = 0.3f;
    [SerializeField] private int expReward = 10;
    [SerializeField] private int goldReward = 5;

    private int currentHealth;
    private float stunEndTime;

    public bool IsDead => currentHealth <= 0;
    public bool IsStunned => Time.time < stunEndTime;

    public event Action OnDeath;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void ApplyHealthMultiplier(float multiplier)
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * multiplier));
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        stunEndTime = Time.time + hitStunDuration;
        Debug.Log($"{name} took {amount} damage ({currentHealth}/{maxHealth} HP left)");

        if (currentHealth <= 0)
        {
            PlayerLevel.Instance?.AddExp(expReward);
            PlayerGold.Instance?.AddGold(goldReward);
            OnDeath?.Invoke();
        }
    }
}
