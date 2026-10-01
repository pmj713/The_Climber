using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 30;
    [SerializeField] private float hitStunDuration = 0.3f;
    [SerializeField] private int expReward = 10;

    [Header("사망 이펙트 (발밑 기준, 비워두면 없음)")]
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private float deathEffectScale = 1f;
    [SerializeField] private float deathEffectLifetime = 3f;

    private int currentHealth;
    private float stunEndTime;
    private bool stunResistant;

    public bool IsDead => currentHealth <= 0;
    public bool IsStunned => !stunResistant && Time.time < stunEndTime;
    public float HealthFraction => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

    // 가드 같은 일시적 피해 감소 상태에서 사용. 1이면 평소대로, 0.2면 받는 피해가 80% 줄어든다.
    public float IncomingDamageMultiplier { get; set; } = 1f;

    // 같은 오브젝트에 EnemyStatusEffects가 있으면 자동으로 연결된다 (없어도 정상 동작).
    public EnemyStatusEffects Status { get; private set; }

    public event Action OnDeath;
    // 실제로 적용된(피해 감소 반영 후) 데미지 양을 알려준다. 가드 중 반격 넉백 같은 반응에 사용.
    public event Action<int> OnDamaged;

    private void Awake()
    {
        currentHealth = maxHealth;
        Status = GetComponent<EnemyStatusEffects>();
    }

    private void Start()
    {
        // 마을 제단에서 고른 '적 강화' 단계 (층 난이도 배율과 곱해짐)
        EnemyEmpowerment.ApplyTo(gameObject);
    }

    // 엘리트 몬스터처럼 히트 경직에 영향받지 않아야 하는 대상에 사용.
    public void SetStunResistant(bool resistant)
    {
        stunResistant = resistant;
    }

    public void ApplyHealthMultiplier(float multiplier)
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * multiplier));
        currentHealth = maxHealth;
    }

    public void ApplyRewardMultiplier(float multiplier)
    {
        expReward = Mathf.Max(1, Mathf.RoundToInt(expReward * multiplier));
    }

    public void TakeDamage(int amount) => TakeDamage(amount, true);

    // 속성이 실린 공격 전용 진입점. 기본 데미지를 주면서 해당 속성 스택을 쌓는다.
    // 이미 빙결(완전 정지)된 적이면 스택은 쌓지 않고, TakeDamage에서 얼음이 깨지며 해제 데미지가 더해진다.
    public void TakeElementalDamage(int amount, ElementType element, int stacks, bool applyHitStun = true)
    {
        if (Status != null && element != ElementType.None && !Status.IsFrozen)
        {
            if (element == ElementType.Freeze) Status.ApplyFreeze(stacks);
            else if (element == ElementType.Burn) Status.ApplyBurn(stacks);
        }

        TakeDamage(amount, applyHitStun);
    }

    public void TakeDamage(int amount, bool applyHitStun)
    {
        if (IsDead) return;

        // 빙결된 적은 속성과 상관없이 어떤 공격에 맞든 얼음이 깨지면서 해제 데미지를 추가로 받는다.
        // (맞지 않으면 EnemyStatusEffects에서 3초 뒤 데미지 없이 저절로 풀린다)
        if (amount > 0 && Status != null && Status.IsFrozen)
        {
            amount += Status.ConsumeFreezeShatter();
        }

        int adjustedAmount = Mathf.RoundToInt(amount * IncomingDamageMultiplier);
        currentHealth = Mathf.Max(0, currentHealth - adjustedAmount);
        if (applyHitStun) stunEndTime = Time.time + hitStunDuration;
        OnDamaged?.Invoke(adjustedAmount);
        Debug.Log($"{name} took {adjustedAmount} damage ({currentHealth}/{maxHealth} HP left)");

        if (currentHealth <= 0)
        {
            PlayerLevel.Instance?.AddExp(expReward);
            SpawnDeathEffect();
            OnDeath?.Invoke();
        }
    }

    // 사망 직후 AI가 몸을 바로 지우므로, 그 자리를 덮는 이펙트를 몸과 별개로 띄운다.
    private void SpawnDeathEffect()
    {
        if (deathEffectPrefab == null) return;
        GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        effect.transform.localScale = Vector3.one * deathEffectScale;
        Destroy(effect, deathEffectLifetime);
    }
}
