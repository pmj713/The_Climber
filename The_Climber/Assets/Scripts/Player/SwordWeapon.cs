using System.Collections.Generic;
using UnityEngine;

public class SwordWeapon : MonoBehaviour, IWeapon
{
    [SerializeField] private float range = 0.45f;
    [SerializeField] private float hitRadius = 0.45f;
    [SerializeField] private int damage = 12;
    [SerializeField] private float powerDamagePerLevel = 0.15f;

    [Header("공격 범위 표시")]
    [SerializeField] private float attackRangeVisualLifetime = 0.25f;
    [SerializeField] private float attackRangeLineWidth = 0.035f;

    [Header("공격 이펙트 (비워두면 기존 범위 선만 표시)")]
    [SerializeField] private SlashVisual slashEffectPrefab; // 판정 앞쪽 반원을 덮는 초승달 베기
    [SerializeField] private GameObject hitEffectPrefab;    // 맞힌 적마다 터지는 타격 불꽃

    private PlayerSkillManager skillManager;
    private int effectiveDamage;

    // "검 공격범위 증가" 스킬을 획득한 만큼 늘어나는 사거리 보너스
    public float RangeBonus => GetCustomEffect(CustomEffectType.SwordRangeBonus);

    private void Awake()
    {
        skillManager = GetComponent<PlayerSkillManager>();
        effectiveDamage = damage;
    }

    public void TryAttack(Vector3 origin, Vector3 direction)
    {
        // 일반공격 키워드는 레벨이 오를 때마다 데미지/공격속도가 번갈아 증가한다 (홀수 레벨 = 데미지).
        int basicAttackLevel = skillManager != null ? skillManager.GetKeywordLevel(KeywordType.BasicAttack) : 0;
        int powerStacks = (basicAttackLevel + 1) / 2;
        int finalDamage = Mathf.RoundToInt(effectiveDamage * (1f + powerDamagePerLevel * powerStacks));

        float bonus = RangeBonus;
        float effectiveHitRadius = hitRadius + bonus * 0.5f;

        Vector3 hitCenter = origin + direction.normalized * (range + bonus * 0.5f);
        ElementType element = ResolveElement();
        Color tint = EffectTint.ForElement(element);

        if (slashEffectPrefab != null) SpawnSlash(hitCenter, direction, effectiveHitRadius, tint);
        else SpawnAttackRangeVisual(hitCenter, direction, effectiveHitRadius);

        // 레이어 마스크를 안 쓰고 IDamageable 여부로만 판단 (마스크를 안 맞춰놨을 때 아무도
        // 안 맞는 사고를 방지). 대신 자기 자신(플레이어)은 명시적으로 제외한다.
        Collider[] hits = Physics.OverlapSphere(hitCenter, effectiveHitRadius);

        var damagedAlready = new HashSet<IDamageable>();
        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;
            if (!damagedAlready.Add(damageable)) continue;

            if (element != ElementType.None && damageable is EnemyHealth enemy)
                enemy.TakeElementalDamage(finalDamage, element, 1);
            else
                damageable.TakeDamage(finalDamage);

            SpawnHitEffect(hit, origin, tint);
        }
    }

    // 일반공격에 붙은 속성. 칼날 궤적 색도 이걸로 맞춘다.
    public ElementType CurrentElement => ResolveElement();

    private void SpawnSlash(Vector3 hitCenter, Vector3 direction, float effectiveHitRadius, Color tint)
    {
        Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        SlashVisual slash = Instantiate(slashEffectPrefab, hitCenter, Quaternion.LookRotation(forward));
        slash.Play(tint, effectiveHitRadius);
    }

    // 적 몸에서 플레이어 쪽 표면, 몸통 높이에 불꽃을 터뜨리고 플레이어 반대쪽으로 튀게 한다.
    private void SpawnHitEffect(Collider hit, Vector3 origin, Color tint)
    {
        if (hitEffectPrefab == null) return;
        Bounds bounds = hit.bounds;
        Vector3 contact = bounds.ClosestPoint(new Vector3(origin.x, bounds.center.y, origin.z));
        Vector3 away = Vector3.ProjectOnPlane(contact - origin, Vector3.up);
        Quaternion rotation = away.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(away) : transform.rotation;
        EffectTint.Spawn(hitEffectPrefab, contact, rotation, tint);
    }

    private ElementType ResolveElement()
    {
        if (skillManager == null) return ElementType.None;
        if (skillManager.HasElement(ElementSource.BasicAttack, ElementType.Freeze)) return ElementType.Freeze;
        if (skillManager.HasElement(ElementSource.BasicAttack, ElementType.Burn)) return ElementType.Burn;
        return ElementType.None;
    }

    private float GetCustomEffect(CustomEffectType type)
    {
        return skillManager != null ? skillManager.GetCustomEffectTotal(type) : 0f;
    }

    // 판정의 앞쪽 반원을 끝이 가늘어지는 풍압 곡선으로 표시한다.
    private void SpawnAttackRangeVisual(Vector3 hitCenter, Vector3 direction, float effectiveHitRadius)
    {
        const int segments = 33;
        var visual = new GameObject("SwordAttackRange");
        var line = visual.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = segments;
        line.widthMultiplier = attackRangeLineWidth;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
        line.startColor = line.endColor = Color.white;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        var material = new Material(Shader.Find("Sprites/Default"));
        line.sharedMaterial = material;

        Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, i / (float)(segments - 1));
            line.SetPosition(i, hitCenter + (forward * Mathf.Cos(angle) + right * Mathf.Sin(angle)) * effectiveHitRadius);
        }

        float lifetime = Mathf.Max(0.01f, attackRangeVisualLifetime);
        Destroy(visual, lifetime);
        Destroy(material, lifetime);
    }
}
