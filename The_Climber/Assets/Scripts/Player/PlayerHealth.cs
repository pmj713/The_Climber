using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public static PlayerHealth Instance { get; private set; }

    [SerializeField] private int maxHealth = 100;

    private int effectiveMaxHealth;
    private int currentHealth;
    private PlayerMovement movement;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => effectiveMaxHealth;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        Instance = this;
        effectiveMaxHealth = maxHealth;
        currentHealth = effectiveMaxHealth;
        movement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        // 체력바 UI가 첫 값을 받을 수 있도록 한 번 알려준다.
        OnHealthChanged?.Invoke(currentHealth, effectiveMaxHealth);
    }

    // 마을로 복귀했을 때(사망 후 다시 시작) 체력을 회복시키기 위해 호출.
    public void ResetHealth()
    {
        currentHealth = effectiveMaxHealth;
        OnHealthChanged?.Invoke(currentHealth, effectiveMaxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0) return;
        if (movement != null && movement.IsInvulnerable) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"Player took {amount} damage ({currentHealth}/{effectiveMaxHealth} HP left)");
        OnHealthChanged?.Invoke(currentHealth, effectiveMaxHealth);

        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
    }
}
