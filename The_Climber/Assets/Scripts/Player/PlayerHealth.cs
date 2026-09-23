using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public static PlayerHealth Instance { get; private set; }

    [SerializeField] private int maxHealth = 100; // 영구 업그레이드 적용 전 기본값

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
        ApplyPermanentUpgrades();
    }

    // 영구 업그레이드(생명력 %)를 기본값에 다시 적용해서 effectiveMaxHealth를 갱신한다.
    // 플레이어는 씬을 넘어가도 파괴되지 않아서 Start()는 한 번만 실행되므로, 제단에서
    // 구매하거나 새 런을 시작할 때 TownController가 이 메서드를 직접 호출해줘야 한다.
    public void ApplyPermanentUpgrades()
    {
        float bonus = PermanentUpgrades.Instance != null ? PermanentUpgrades.Instance.GetBonusPercent(UpgradeType.MaxHealth) : 0f;
        effectiveMaxHealth = Mathf.RoundToInt(maxHealth * (1f + bonus));
        currentHealth = effectiveMaxHealth;
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
