using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public static PlayerHealth Instance { get; private set; }

    [SerializeField] private int maxHealth = 100;
    [Tooltip("레벨업할 때마다 회복되는 체력 (최대 체력 대비 비율)")]
    [SerializeField, Range(0f, 1f)] private float levelUpHealFraction = 0.1f;

    private int effectiveMaxHealth;
    private int currentHealth;
    private PlayerMovement movement;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => effectiveMaxHealth;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        // 마을을 다시 불러올 때 씬에 들어 있는 사본은 중복으로 파괴되므로, 이미 살아 있는 본체의 싱글톤을 덮어쓰지 않는다
        if (Instance == null || Instance == this) Instance = this;
        effectiveMaxHealth = maxHealth;
        currentHealth = effectiveMaxHealth;
        movement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        // 체력바 UI가 첫 값을 받을 수 있도록 한 번 알려준다.
        OnHealthChanged?.Invoke(currentHealth, effectiveMaxHealth);

        // PlayerLevel.Awake가 끝난 뒤에 구독해야 하므로 Start에서 한다
        if (PlayerLevel.Instance != null) PlayerLevel.Instance.OnLevelUp += HandleLevelUp;
    }

    private void OnDestroy()
    {
        if (PlayerLevel.Instance != null) PlayerLevel.Instance.OnLevelUp -= HandleLevelUp;
    }

    private void HandleLevelUp(int newLevel)
    {
        Heal(Mathf.CeilToInt(effectiveMaxHealth * levelUpHealFraction));
    }

    public void Heal(int amount)
    {
        if (currentHealth <= 0 || amount <= 0) return;

        currentHealth = Mathf.Min(effectiveMaxHealth, currentHealth + amount);
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
