using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PlayerActiveSkillSlots에 장착된 스킬을 실제로 발동시킨다 (0: 우클릭, 1: E, 2: Q).
// 모든 액티브 스킬은 1회 한정 획득이라 CountAcquired는 항상 0/1이다. 따라서 위력은
// "몇 번 획득했는지"가 아니라 각 스킬이 속한 키워드(투사체/범위/버프/화상 등)의 레벨과
// 개별 성장 스킬(CustomEffectType)이 쌓아준 값을 그때그때 읽어서 계산한다.
[RequireComponent(typeof(PlayerActiveSkillSlots))]
public class ActiveSkillCaster : MonoBehaviour
{
    private static readonly RebindableAction[] SlotActions =
        { RebindableAction.SkillRightClick, RebindableAction.SkillE, RebindableAction.SkillQ };

    [SerializeField] private LayerMask enemyMask = 8;

    [Header("화살 비 (지속 범위 피해)")]
    [SerializeField] private float arrowRainRadius = 2.8f;
    [SerializeField] private float arrowRainDuration = 3f;
    [SerializeField] private float arrowRainTickInterval = 0.5f;
    [SerializeField] private int arrowRainTickDamage = 6;
    [SerializeField] private float arrowRainWarmup = 0.8f;
    [SerializeField] private GameObject arrowRainArrowPrefab;
    [SerializeField] private float arrowRainArrowsPerRadius = 4f;
    [SerializeField] private float arrowRainArrowSpawnHeight = 9f;
    [SerializeField] private float arrowRainArrowFallSpeed = 22f;
    [SerializeField] private GameObject arrowRainAreaPrefab;   // 범위 마법진 (반지름 1 기준, 비우면 파란 링)
    [SerializeField] private GameObject arrowRainImpactPrefab; // 화살이 땅에 박히는 자리

    [Header("헤이스트")]
    [SerializeField] private float hastePercentPerLevel = 0.10f;
    [SerializeField] private float hasteDuration = 5f;
    [SerializeField] private float hasteDurationPercentPerLevel = 0.15f;
    [SerializeField] private GameObject hasteBurstPrefab; // 발동 순간 (비우면 기존 링 연출)
    [SerializeField] private GameObject hasteAuraPrefab;  // 지속시간 동안 몸에 붙는 기운 (비우면 기존 파티클)

    [Header("검기")]
    [SerializeField] private float swordWaveSpeed = 12f;
    [SerializeField] private float swordWaveMaxDistance = 14f;
    [SerializeField] private float swordWaveRadius = 0.8f;
    [SerializeField] private float swordWaveLineSpacing = 0.6f; // 투사체 키워드: 개수 증가 시 나란히 추가되는 줄 사이 간격
    [SerializeField] private int swordWaveDamage = 15;
    [SerializeField] private GameObject swordWaveVisualPrefab;
    [SerializeField] private GameObject swordWaveTrailPrefab; // 검기 외형에 붙는 잔상/불꽃

    [Header("범위 키워드 (레벨업 시 자동 증가하는 범위)")]
    [SerializeField] private float areaRadiusPercentPerLevel = 0.12f;

    [Header("힐윈드")]
    [SerializeField] private float whirlwindDuration = 3f;
    [SerializeField] private float whirlwindRadius = 2f;
    [SerializeField] private float whirlwindTickInterval = 0.5f;
    [SerializeField] private int whirlwindTickDamage = 8;
    [SerializeField] private GameObject whirlwindPrefab; // 회전 베기 소용돌이 (반지름 1 기준, 비우면 파란 링)

    [Header("부채살")]
    [SerializeField] private Projectile fanShotProjectilePrefab;
    [SerializeField] private float fanShotProjectileSpeed = 16f;
    [SerializeField] private int fanShotDamage = 10;
    [SerializeField] private int fanShotBaseArrowCount = 3;
    [SerializeField] private float fanShotSpreadAngle = 50f;
    [SerializeField] private GameObject fanShotBurstPrefab; // 발사 순간 활 앞에서 퍼지는 섬광

    [Header("화상폭발")]
    [SerializeField] private GameObject burnNovaVisualPrefab;
    [SerializeField] private GameObject burnNovaCastPrefab; // 시전 순간 플레이어 주변 불꽃 파동

    [Header("냉기 확산 (오라)")]
    [SerializeField] private float freezeAuraRadius = 4f;
    [SerializeField] private float freezeAuraInterval = 1f;
    [SerializeField] private GameObject freezeAuraPulsePrefab; // 틱마다 퍼지는 냉기 파동 (반지름 1 기준)
    [SerializeField] private GameObject freezeAuraHitPrefab;   // 빙결 스택이 쌓인 적 위의 서리

    [Header("스킬 타격")]
    [SerializeField] private GameObject skillHitEffectPrefab; // 검기/휠윈드에 맞은 적 (속성 색으로 물듦)

    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private PlayerActiveSkillSlots slots;
    private PlayerSkillManager skillManager;
    private PlayerMovement movement;
    private WeaponController weaponController;
    private Animator animator;
    private readonly float[] nextCastTime = new float[PlayerActiveSkillSlots.SlotCount];
    private float nextAuraTickTime;

    private void Awake()
    {
        slots = GetComponent<PlayerActiveSkillSlots>();
        skillManager = GetComponent<PlayerSkillManager>();
        movement = GetComponent<PlayerMovement>();
        weaponController = GetComponent<WeaponController>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        for (int i = 0; i < SlotActions.Length; i++)
        {
            if (Input.GetKeyDown(KeyBindingManager.GetKey(SlotActions[i])))
            {
                TryCast(i);
            }
        }

        TickFreezeAura();
    }

    // 냉기 확산: 빙결 속성을 고른 상태에서만, 주변 몬스터에게 자동으로 빙결 스택을 서서히 쌓는다.
    private void TickFreezeAura()
    {
        if (Time.time < nextAuraTickTime) return;
        nextAuraTickTime = Time.time + freezeAuraInterval;

        if (skillManager == null || skillManager.LockedElement != ElementType.Freeze) return;
        float stacksPerTick = CustomEffect(CustomEffectType.FreezeAuraStackPerSecond) * freezeAuraInterval;
        if (stacksPerTick <= 0f) return;

        int stacks = Mathf.Max(1, Mathf.RoundToInt(stacksPerTick));
        SpawnScaledEffect(freezeAuraPulsePrefab, transform.position + Vector3.down * (GroundOffset() - 0.05f), Quaternion.identity, freezeAuraRadius, Color.white, 1.5f);

        Collider[] hits = Physics.OverlapSphere(transform.position, freezeAuraRadius, enemyMask);
        foreach (Collider hit in hits)
        {
            if (hit.TryGetComponent<EnemyHealth>(out var enemy) && enemy.Status != null)
            {
                enemy.Status.ApplyFreeze(stacks);
                if (freezeAuraHitPrefab != null) EffectTint.Spawn(freezeAuraHitPrefab, hit.bounds.center, Quaternion.identity, Color.white);
            }
        }
    }

    // 반지름 1 기준으로 만든 이펙트를 원하는 크기로 키워서 띄운다.
    private static GameObject SpawnScaledEffect(GameObject prefab, Vector3 position, Quaternion rotation, float scale, Color tint, float lifetime)
    {
        if (prefab == null) return null;
        GameObject effect = Object.Instantiate(prefab, position, rotation);
        effect.transform.localScale = Vector3.one * scale;
        EffectTint.Apply(effect, tint);
        if (lifetime > 0f) Destroy(effect, lifetime);
        return effect;
    }

    // 맞은 적의 몸통, 시전 위치 쪽 표면에 타격 불꽃을 띄운다.
    private void SpawnSkillHit(Collider hit, Vector3 from, Color tint)
    {
        if (skillHitEffectPrefab == null) return;
        Bounds bounds = hit.bounds;
        Vector3 contact = bounds.ClosestPoint(new Vector3(from.x, bounds.center.y, from.z));
        Vector3 away = Vector3.ProjectOnPlane(contact - from, Vector3.up);
        EffectTint.Spawn(skillHitEffectPrefab, contact, away.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(away) : Quaternion.identity, tint);
    }

    public float GetCooldownRemaining(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= nextCastTime.Length) return 0f;
        return Mathf.Max(0f, nextCastTime[slotIndex] - Time.time);
    }

    private void TryCast(int slotIndex)
    {
        SkillDefinition skill = slots.GetSlot(slotIndex);
        if (skill == null || skill.activeType == ActiveSkillType.None) return;
        if (Time.time < nextCastTime[slotIndex]) return;

        nextCastTime[slotIndex] = Time.time + Mathf.Max(0.1f, skill.cooldown);
        Cast(skill);
    }

    private void Cast(SkillDefinition skill)
    {
        if (animator != null && !IsBuff(skill.activeType))
        {
            animator.SetTrigger(AttackHash);
        }

        switch (skill.activeType)
        {
            case ActiveSkillType.ArrowRain:
                Vector3 targetPoint = movement != null ? movement.GetAimPoint() : transform.position;
                StartCoroutine(ArrowRainRoutine(targetPoint));
                break;
            case ActiveSkillType.Haste:
                CastHaste();
                break;
            case ActiveSkillType.SwordWave:
                Vector3 waveOrigin = transform.position + Vector3.up;
                Vector3 waveDirection = movement != null ? movement.GetAimDirection() : transform.forward;
                CastSwordWave(waveOrigin, waveDirection);
                break;
            case ActiveSkillType.Whirlwind:
                StartCoroutine(WhirlwindRoutine());
                break;
            case ActiveSkillType.FanShot:
                Vector3 fanOrigin = transform.position + Vector3.up;
                Vector3 fanDirection = movement != null ? movement.GetAimDirection() : transform.forward;
                CastFanShot(fanOrigin, fanDirection);
                break;
            case ActiveSkillType.BurnNova:
                CastBurnNova();
                break;
        }
    }

    // 버프성 스킬(지속 효과만 있고 별도 동작이 없는 스킬)은 공격 애니메이션을 재생하지 않는다
    private bool IsBuff(ActiveSkillType type)
    {
        return type == ActiveSkillType.Haste;
    }

    private int GetLevel(KeywordType keyword) => skillManager != null ? skillManager.GetKeywordLevel(keyword) : 0;
    private float CustomEffect(CustomEffectType type) => skillManager != null ? skillManager.GetCustomEffectTotal(type) : 0f;

    // "공격력 강화" 스킬로 얻는 캐릭터 공격력. 일반공격(무기 기본 공격)에는 영향을 주지 않고,
    // 화상폭발을 제외한 다른 액티브 스킬들의 데미지에 곱연산으로 적용된다.
    private float CharacterPowerMultiplier => 1f + CustomEffect(CustomEffectType.CharacterPower);

    private ElementType ResolveElement(ElementSource source)
    {
        if (skillManager == null) return ElementType.None;
        if (skillManager.HasElement(source, ElementType.Freeze)) return ElementType.Freeze;
        if (skillManager.HasElement(source, ElementType.Burn)) return ElementType.Burn;
        return ElementType.None;
    }

    // 실제로 데미지를 넣었으면 true (타격 이펙트를 띄울지 판단용)
    private bool DealDamage(Collider hit, int damage, ElementType element, HashSet<IDamageable> alreadyHit = null)
    {
        if (!hit.TryGetComponent<IDamageable>(out var damageable)) return false;
        if (alreadyHit != null && !alreadyHit.Add(damageable)) return false;

        if (damageable is EnemyHealth enemy)
        {
            if (element != ElementType.None) enemy.TakeElementalDamage(damage, element, 1, false);
            else enemy.TakeDamage(damage, false);
        }
        else
        {
            damageable.TakeDamage(damage);
        }
        return true;
    }

    // ---------------- 화살 비 ----------------

    private IEnumerator ArrowRainRoutine(Vector3 targetPoint)
    {
        float radius = arrowRainRadius * (1f + areaRadiusPercentPerLevel * GetLevel(KeywordType.Area));
        float duration = arrowRainDuration * (1f + CustomEffect(CustomEffectType.AreaDurationMultiplier));
        int tickDamage = Mathf.RoundToInt(arrowRainTickDamage * (1f + CustomEffect(CustomEffectType.AreaDamageMultiplier)) * CharacterPowerMultiplier);
        ElementType element = ResolveElement(ElementSource.ArrowRain);

        // 조준점은 플레이어 몸 중앙 높이의 평면에서 잡히므로, 바닥으로 내려야 범위 표시와 화살이 땅에 박힌다
        targetPoint.y = transform.position.y - GroundOffset();

        Color tint = EffectTint.ForSkill(element);
        GameObject indicator = arrowRainAreaPrefab != null
            ? SpawnScaledEffect(arrowRainAreaPrefab, targetPoint + Vector3.up * 0.05f, Quaternion.identity, radius, tint, 0f)
            : CreateRangeRingIndicator(targetPoint, radius);

        if (arrowRainArrowPrefab != null)
        {
            // 예고 시간부터 지속시간이 끝날 때까지, 화살이 끊이지 않고 계속 떨어지는 연출을 유지한다.
            StartCoroutine(ArrowRainVisualLoop(targetPoint, radius, arrowRainWarmup + duration, tint));
        }

        yield return new WaitForSeconds(arrowRainWarmup);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            Collider[] hits = Physics.OverlapSphere(targetPoint, radius, enemyMask);
            foreach (Collider hit in hits) DealDamage(hit, tickDamage, element);

            yield return new WaitForSeconds(arrowRainTickInterval);
            elapsed += arrowRainTickInterval;
        }

        EffectTint.StopAndDestroy(indicator);
    }

    // 화살비 지속시간 내내 화살이 끊기지 않고 계속 떨어지도록 일정 간격으로 계속 스폰한다.
    private IEnumerator ArrowRainVisualLoop(Vector3 targetPoint, float radius, float totalDuration, Color tint)
    {
        const float spawnInterval = 0.15f;
        int arrowsPerBurst = Mathf.Max(1, Mathf.RoundToInt(radius * arrowRainArrowsPerRadius * spawnInterval));

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            for (int i = 0; i < arrowsPerBurst; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 landingPoint = targetPoint + new Vector3(offset.x, 0f, offset.y);
                StartCoroutine(FallArrow(landingPoint, 0f, tint));
            }

            yield return new WaitForSeconds(spawnInterval);
            elapsed += spawnInterval;
        }
    }

    // 스킬 범위를 파란색 원 테두리(링)로만 표시한다 (채워진 원판이 아님).
    private GameObject CreateRangeRingIndicator(Vector3 center, float radius)
    {
        GameObject indicator = new GameObject("ArrowRainRangeIndicator");
        indicator.transform.position = center + Vector3.up * 0.05f;

        const int segments = 48;
        LineRenderer ring = indicator.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = segments;
        ring.startWidth = ring.endWidth = 0.12f;
        ring.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        Color ringColor = new Color(0.25f, 0.55f, 1f, 0.9f);
        ring.startColor = ring.endColor = ringColor;
        ring.material.SetColor("_BaseColor", ringColor);

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        return indicator;
    }

    // 화살 하나가 목표 지점 위에서 촉을 아래로 향한 채 비처럼 떨어져 박히는 연출.
    private IEnumerator FallArrow(Vector3 landingPoint, float startDelay, Color tint)
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        float tiltAngle = Random.Range(0f, 12f);
        float tiltDirection = Random.Range(0f, 360f);
        Vector3 fallDirection = Quaternion.AngleAxis(tiltDirection, Vector3.up) *
            Quaternion.AngleAxis(tiltAngle, Vector3.right) * Vector3.down;

        Quaternion rotation = Quaternion.LookRotation(fallDirection) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Vector3 startPos = landingPoint + Vector3.up * arrowRainArrowSpawnHeight;
        GameObject arrow = Instantiate(arrowRainArrowPrefab, startPos, rotation);
        EffectTint.Apply(arrow, tint);

        float fallSpeed = arrowRainArrowFallSpeed * Random.Range(0.85f, 1.15f);
        float traveled = 0f;
        while (traveled < arrowRainArrowSpawnHeight)
        {
            traveled += fallSpeed * Time.deltaTime;
            arrow.transform.position = Vector3.Lerp(startPos, landingPoint, Mathf.Clamp01(traveled / arrowRainArrowSpawnHeight));
            yield return null;
        }

        arrow.transform.position = landingPoint;
        if (arrowRainImpactPrefab != null) EffectTint.Spawn(arrowRainImpactPrefab, landingPoint, Quaternion.identity, tint, 1f);
        yield return new WaitForSeconds(0.4f);
        Destroy(arrow);
    }

    // ---------------- 헤이스트 ----------------

    private void CastHaste()
    {
        int buffLevel = Mathf.Max(1, GetLevel(KeywordType.Buff));
        float bonusPercent = hastePercentPerLevel * buffLevel;
        float duration = hasteDuration * (1f + hasteDurationPercentPerLevel * (buffLevel - 1));
        float cooldownMultiplier = Mathf.Max(0.05f, 1f - bonusPercent);
        float moveMultiplier = 1f + bonusPercent;

        if (weaponController != null) weaponController.ApplyTemporaryHaste(cooldownMultiplier, duration);
        if (movement != null) movement.ApplyTemporarySpeedBoost(moveMultiplier, duration);

        if (hasteBurstPrefab != null) EffectTint.Spawn(hasteBurstPrefab, transform.position + Vector3.down * GroundOffset(), transform.rotation, Color.white, 2f);
        else StartCoroutine(HasteActivationBurst());

        if (hasteAuraPrefab != null) StartCoroutine(AttachedEffectRoutine(hasteAuraPrefab, duration, Color.white));
        else StartCoroutine(HasteAuraRoutine(duration));
    }

    // 지속시간 동안 플레이어 발밑에 붙어 있다가 방출을 멈추고 사라지는 이펙트
    private IEnumerator AttachedEffectRoutine(GameObject prefab, float duration, Color tint)
    {
        GameObject effect = Instantiate(prefab, transform.position + Vector3.down * GroundOffset(), transform.rotation, transform);
        EffectTint.Apply(effect, tint);
        yield return new WaitForSeconds(duration);
        EffectTint.StopAndDestroy(effect);
    }

    private IEnumerator HasteActivationBurst()
    {
        const int segments = 40;
        GameObject burst = new GameObject("HasteBurstRing");
        burst.transform.SetParent(transform, false);
        burst.transform.localPosition = Vector3.up * 0.05f;

        LineRenderer ring = burst.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = segments;
        ring.startWidth = ring.endWidth = 0.08f;
        Material ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        ring.material = ringMat;
        Color baseColor = new Color(1f, 0.82f, 0.2f, 1f);

        const float duration = 0.35f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float radius = Mathf.Lerp(0.2f, 1.6f, p);
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            Color faded = baseColor;
            faded.a = Mathf.Lerp(1f, 0f, p);
            ring.startColor = ring.endColor = faded;
            ringMat.SetColor("_BaseColor", faded);
            yield return null;
        }

        Destroy(burst);
    }

    private IEnumerator HasteAuraRoutine(float duration)
    {
        GameObject aura = new GameObject("HasteAura");
        aura.transform.SetParent(transform, false);
        aura.transform.localPosition = Vector3.zero;

        ParticleSystem ps = aura.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 0.6f;
        main.startSpeed = 1.5f;
        main.startSize = 0.15f;
        main.startColor = new Color(1f, 0.85f, 0.25f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 30f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(1.2f, 2f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.25f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        ps.GetComponent<ParticleSystemRenderer>().material = new Material(particleShader);

        ps.Play();

        yield return new WaitForSeconds(duration);

        var stoppedEmission = ps.emission;
        stoppedEmission.rateOverTime = 0f;

        yield return new WaitForSeconds(main.startLifetime.constantMax);
        Destroy(aura);
    }

    // ---------------- 검기 ----------------

    private void CastSwordWave(Vector3 origin, Vector3 direction)
    {
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        int projectileLevel = GetLevel(KeywordType.Projectile);
        int lineCount = 1 + projectileLevel; // 투사체 키워드 공통: 개수 증가
        float radius = swordWaveRadius + CustomEffect(CustomEffectType.SwordWaveSizeBonus); // "검기 크기 증가" 스킬 전용
        int damage = Mathf.RoundToInt(swordWaveDamage * CharacterPowerMultiplier);
        ElementType element = ResolveElement(ElementSource.SwordWave);

        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        float totalWidth = swordWaveLineSpacing * (lineCount - 1);
        for (int i = 0; i < lineCount; i++)
        {
            float lateralOffset = -totalWidth * 0.5f + swordWaveLineSpacing * i;
            Vector3 lineOrigin = origin + right * lateralOffset;
            StartCoroutine(SwordWaveRoutine(lineOrigin, direction, damage, radius, element));
        }
    }

    private IEnumerator SwordWaveRoutine(Vector3 origin, Vector3 direction, int damage, float radius, ElementType element)
    {
        GameObject waveObject = swordWaveVisualPrefab != null
            ? Instantiate(swordWaveVisualPrefab, origin, Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -90f, 0f))
            : null;
        Color tint = EffectTint.ForSkill(element);
        // 잔상은 검기 외형(스케일 1.5)이 아니라 진행 방향 기준으로 따로 띄워서 같이 옮긴다
        GameObject trail = swordWaveTrailPrefab != null
            ? SpawnScaledEffect(swordWaveTrailPrefab, origin, Quaternion.LookRotation(direction), radius / 0.8f, tint, 0f)
            : null;
        Vector3 position = origin;
        var alreadyHit = new HashSet<IDamageable>();
        float traveled = 0f;

        while (traveled < swordWaveMaxDistance)
        {
            float step = swordWaveSpeed * Time.deltaTime;
            position += direction * step;
            traveled += step;

            if (waveObject != null) waveObject.transform.position = position;
            if (trail != null) trail.transform.position = position;

            Collider[] hits = Physics.OverlapSphere(position, radius, enemyMask);
            foreach (Collider hit in hits)
            {
                if (DealDamage(hit, damage, element, alreadyHit)) SpawnSkillHit(hit, position - direction, tint);
            }

            yield return null;
        }

        if (waveObject != null) Destroy(waveObject);
        EffectTint.StopAndDestroy(trail);
    }

    // ---------------- 힐윈드 ----------------

    private IEnumerator WhirlwindRoutine()
    {
        float radius = whirlwindRadius * (1f + areaRadiusPercentPerLevel * GetLevel(KeywordType.Area));
        float duration = whirlwindDuration * (1f + CustomEffect(CustomEffectType.AreaDurationMultiplier));
        float tickInterval = Mathf.Max(0.1f, whirlwindTickInterval - CustomEffect(CustomEffectType.WhirlwindTickSpeed));
        int tickDamage = Mathf.RoundToInt(whirlwindTickDamage * (1f + CustomEffect(CustomEffectType.AreaDamageMultiplier)) * CharacterPowerMultiplier);
        ElementType element = ResolveElement(ElementSource.Whirlwind);

        Color tint = EffectTint.ForSkill(element);
        GameObject ring;
        if (whirlwindPrefab != null)
        {
            // 플레이어 피벗은 몸 중앙이라, 소용돌이는 발밑(바닥)에 맞춰 붙인다
            ring = SpawnScaledEffect(whirlwindPrefab, transform.position, transform.rotation, radius, tint, 0f);
            ring.transform.SetParent(transform, true);
            ring.transform.localPosition = Vector3.down * GroundOffset();
        }
        else
        {
            ring = CreateRangeRingIndicator(transform.position, radius);
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.up * 0.05f;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyMask);
            foreach (Collider hit in hits)
            {
                if (DealDamage(hit, tickDamage, element)) SpawnSkillHit(hit, transform.position, tint);
            }

            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }

        EffectTint.StopAndDestroy(ring);
    }

    // 플레이어 피벗(몸 중앙)에서 발바닥까지의 높이
    private float GroundOffset()
    {
        return TryGetComponent<CharacterController>(out var cc) ? cc.height * 0.5f - cc.center.y : 1f;
    }

    // ---------------- 부채살 ----------------

    private void CastFanShot(Vector3 origin, Vector3 direction)
    {
        if (fanShotProjectilePrefab == null)
        {
            Debug.LogWarning("ActiveSkillCaster: fanShotProjectilePrefab이 지정되지 않았습니다.");
            return;
        }

        int arrowCount = fanShotBaseArrowCount + GetLevel(KeywordType.Projectile); // 투사체 키워드 공통: 개수 증가
        int damage = Mathf.RoundToInt(fanShotDamage * CharacterPowerMultiplier);
        ElementType element = ResolveElement(ElementSource.FanShot);
        float totalSpread = fanShotSpreadAngle * (arrowCount - 1) / Mathf.Max(1, fanShotBaseArrowCount - 1);
        EffectTint.Spawn(fanShotBurstPrefab, origin, Quaternion.LookRotation(direction), EffectTint.ForSkill(element), 1f);

        for (int i = 0; i < arrowCount; i++)
        {
            float angle = arrowCount <= 1 ? 0f : -totalSpread / 2f + (totalSpread / (arrowCount - 1)) * i;
            Vector3 shotDirection = Quaternion.Euler(0f, angle, 0f) * direction;
            Projectile projectile = Instantiate(fanShotProjectilePrefab, origin, Quaternion.LookRotation(shotDirection));
            projectile.Launch(shotDirection, fanShotProjectileSpeed, damage, 0, element, 1);
        }
    }

    // ---------------- 화상폭발 ----------------

    private void CastBurnNova()
    {
        EffectTint.Spawn(burnNovaCastPrefab, transform.position + Vector3.down * (GroundOffset() - 0.05f), Quaternion.identity, Color.white, 2f);

        EnemyStatusEffects[] allStatus = FindObjectsByType<EnemyStatusEffects>(FindObjectsSortMode.None);
        foreach (EnemyStatusEffects status in allStatus)
        {
            if (status.ActiveStatus != EnemyStatusEffects.StatusType.Burn || status.BurnStacks <= 0) continue;

            int damage = status.ConsumeBurnExplosion();
            if (damage <= 0) continue;

            if (status.TryGetComponent<EnemyHealth>(out var enemy)) enemy.TakeDamage(damage, false);

            if (burnNovaVisualPrefab != null)
            {
                GameObject fx = Instantiate(burnNovaVisualPrefab, status.transform.position, Quaternion.identity);
                Destroy(fx, 1.5f);
            }
        }
    }
}
