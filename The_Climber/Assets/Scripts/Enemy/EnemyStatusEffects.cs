using System.Collections;
using UnityEngine;

// 몬스터 개체별 빙결/화상 상태이상. 두 속성은 동시에 걸리지 않으며, 먼저 걸린 상태이상이
// 풀릴 때까지는 반대 속성이 무시된다 (플레이어 빌드 자체도 한쪽만 고를 수 있어 실전에서는
// 거의 발생하지 않지만, 방어적으로 막아둔다).
[RequireComponent(typeof(EnemyHealth))]
public class EnemyStatusEffects : MonoBehaviour
{
    private const int BaseMaxFreezeStack = 10;
    private const int BaseMaxBurnStack = 10;
    private const float BaseFreezeStackDuration = 4f;
    private const float BaseBurnStackDuration = 4f;
    private const float BaseBurnTickInterval = 1f;
    private const float FrozenAutoThawDelay = 3f; // 완전 빙결 후 이 시간 동안 맞지 않으면 데미지 없이 저절로 풀린다

    public enum StatusType { None, Freeze, Burn }

    public StatusType ActiveStatus { get; private set; } = StatusType.None;
    public int FreezeStacks { get; private set; }
    public int BurnStacks { get; private set; }
    public bool IsFrozen { get; private set; }

    // 상태이상 이펙트의 세기 조절용 (0~1)
    public float FreezeStackRatio => (float)FreezeStacks / MaxFreezeStack;
    public float BurnStackRatio => (float)BurnStacks / MaxBurnStack;

    // 몬스터 AI가 이동 속도에 곱해서 쓴다. 빙결 스택이 쌓일수록 느려지고, 완전 빙결이면 0.
    public float MoveSpeedMultiplier
    {
        get
        {
            if (IsFrozen) return 0f;
            if (ActiveStatus != StatusType.Freeze || FreezeStacks <= 0) return 1f;
            return Mathf.Lerp(1f, 0.35f, (float)FreezeStacks / MaxFreezeStack);
        }
    }

    private static int MaxFreezeStack => Mathf.Max(3, BaseMaxFreezeStack -
        Mathf.RoundToInt(CustomEffect(CustomEffectType.FreezeMaxStackDelta)));

    private static int MaxBurnStack => Mathf.Max(3, BaseMaxBurnStack +
        Mathf.RoundToInt(CustomEffect(CustomEffectType.BurnMaxStackDelta)));

    private static float FreezeStackDuration => BaseFreezeStackDuration + CustomEffect(CustomEffectType.FreezeDuration);
    private static float BurnStackDuration => BaseBurnStackDuration + CustomEffect(CustomEffectType.BurnDuration);

    private static float BurnTickInterval =>
        Mathf.Max(0.2f, BaseBurnTickInterval - CustomEffect(CustomEffectType.BurnTickSpeed));

    private static float CustomEffect(CustomEffectType type) =>
        PlayerSkillManager.Instance != null ? PlayerSkillManager.Instance.GetCustomEffectTotal(type) : 0f;

    private EnemyHealth health;
    private float statusEndTime;
    private float frozenThawTime;
    private Coroutine burnRoutine;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        health.OnDeath -= HandleDeath;
    }

    private void Update()
    {
        // 완전 빙결은 맞는 순간 깨지거나(EnemyHealth.TakeDamage -> ConsumeFreezeShatter),
        // 그동안 한 번도 맞지 않으면 일정 시간 뒤 데미지 없이 저절로 풀린다.
        if (IsFrozen)
        {
            if (Time.time >= frozenThawTime) ClearStatus();
            return;
        }

        if (ActiveStatus != StatusType.None && Time.time >= statusEndTime)
        {
            ClearStatus();
        }
    }

    public void ApplyFreeze(int stacks)
    {
        if (health.IsDead) return;
        if (ActiveStatus == StatusType.Burn) return; // 먼저 걸린 상태이상을 계속 적용

        int stackPerHit = stacks + Mathf.RoundToInt(CustomEffect(CustomEffectType.FreezeStackPerHit));
        ActiveStatus = StatusType.Freeze;
        FreezeStacks = Mathf.Min(MaxFreezeStack, FreezeStacks + Mathf.Max(1, stackPerHit));
        statusEndTime = Time.time + FreezeStackDuration;

        if (FreezeStacks >= MaxFreezeStack && !IsFrozen)
        {
            IsFrozen = true;
            frozenThawTime = Time.time + FrozenAutoThawDelay;
        }
    }

    // 빙결(완전 정지)된 적을 타격했을 때 호출. 빙결 키워드 레벨에 비례한 해제 데미지를 돌려주고 해제한다.
    public int ConsumeFreezeShatter()
    {
        if (!IsFrozen) return 0;

        int keywordLevel = PlayerSkillManager.Instance != null
            ? PlayerSkillManager.Instance.GetKeywordLevel(KeywordType.Freeze) : 0;
        float explosionBonus = CustomEffect(CustomEffectType.FreezeExplosionBonus);
        int damage = Mathf.RoundToInt((10 + keywordLevel * 6) * (1f + explosionBonus));

        ClearStatus();
        return damage;
    }

    public void ApplyBurn(int stacks)
    {
        if (health.IsDead) return;
        if (ActiveStatus == StatusType.Freeze) return; // 먼저 걸린 상태이상을 계속 적용

        bool alreadyBurning = ActiveStatus == StatusType.Burn && BurnStacks > 0;
        ActiveStatus = StatusType.Burn;
        BurnStacks = Mathf.Min(MaxBurnStack, BurnStacks + Mathf.Max(1, stacks));
        statusEndTime = Time.time + BurnStackDuration;

        if (!alreadyBurning)
        {
            burnRoutine = StartCoroutine(BurnTickRoutine());
        }
    }

    // 화상폭발 액티브: 화상 스택 수 x 화상 키워드 레벨 데미지를 주고 스택을 초기화한다.
    public int ConsumeBurnExplosion()
    {
        if (ActiveStatus != StatusType.Burn || BurnStacks <= 0) return 0;

        int keywordLevel = Mathf.Max(1, PlayerSkillManager.Instance != null
            ? PlayerSkillManager.Instance.GetKeywordLevel(KeywordType.Burn) : 1);
        int damage = BurnStacks * keywordLevel;
        ClearStatus();
        return damage;
    }

    private IEnumerator BurnTickRoutine()
    {
        while (ActiveStatus == StatusType.Burn && BurnStacks > 0 && !health.IsDead)
        {
            yield return new WaitForSeconds(BurnTickInterval);
            if (ActiveStatus != StatusType.Burn || BurnStacks <= 0 || health.IsDead) break;

            // 저점은 낮게, 고점은 높게: 스택 비율의 제곱에 비례해서 틱 데미지가 커진다.
            float stackRatio = (float)BurnStacks / MaxBurnStack;
            float damageMultiplier = 1f + CustomEffect(CustomEffectType.BurnDamageMultiplier);
            int tickDamage = Mathf.Max(1, Mathf.RoundToInt(BurnStacks * stackRatio * 2f * damageMultiplier));
            health.TakeDamage(tickDamage, false);
        }
    }

    private void ClearStatus()
    {
        ActiveStatus = StatusType.None;
        FreezeStacks = 0;
        BurnStacks = 0;
        IsFrozen = false;
        if (burnRoutine != null)
        {
            StopCoroutine(burnRoutine);
            burnRoutine = null;
        }
    }

    private void HandleDeath()
    {
        ClearStatus();
    }
}
