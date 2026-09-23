using System.Collections.Generic;
using UnityEngine;

public class SwordWeapon : MonoBehaviour, IWeapon
{
    [SerializeField] private float range = 0.45f;
    [SerializeField] private float hitRadius = 0.45f;
    [SerializeField] private int damage = 12; // 영구 업그레이드 적용 전 기본값
    [SerializeField] private float powerDamagePerLevel = 0.15f;
    [SerializeField] private float cleaveRadiusPerLevel = 0.3f;

    [Header("공격 범위 표시")]
    [SerializeField] private float attackRangeVisualLifetime = 0.25f;
    [SerializeField] private float attackRangeLineWidth = 0.035f;

    private PlayerSkillManager skillManager;
    private int effectiveDamage;

    public float RangeBonus => cleaveRadiusPerLevel * GetLevel(KeywordType.Cleave);

    private void Awake()
    {
        skillManager = GetComponent<PlayerSkillManager>();
    }

    private void Start()
    {
        ApplyPermanentUpgrades();
    }

    // 영구 업그레이드(공격력 %)를 기본값에 다시 적용한다. 플레이어는 씬을 넘어가도
    // 파괴되지 않아서 Start()는 한 번만 실행되므로, 제단에서 구매하거나 새 런을 시작할 때
    // TownController가 이 메서드를 직접 호출해줘야 한다.
    public void ApplyPermanentUpgrades()
    {
        float bonus = PermanentUpgrades.Instance != null ? PermanentUpgrades.Instance.GetBonusPercent(UpgradeType.AttackPower) : 0f;
        effectiveDamage = Mathf.RoundToInt(damage * (1f + bonus));
    }

    public void TryAttack(Vector3 origin, Vector3 direction)
    {
        int finalDamage = Mathf.RoundToInt(effectiveDamage * (1f + powerDamagePerLevel * GetLevel(KeywordType.Power)));
        float bonus = RangeBonus;
        float effectiveHitRadius = hitRadius + bonus * 0.5f;

        Vector3 hitCenter = origin + direction.normalized * (range + bonus * 0.5f);
        SpawnAttackRangeVisual(hitCenter, direction, effectiveHitRadius);

        // 레이어 마스크를 안 쓰고 IDamageable 여부로만 판단 (마스크를 안 맞춰놨을 때 아무도
        // 안 맞는 사고를 방지). 대신 자기 자신(플레이어)은 명시적으로 제외한다.
        Collider[] hits = Physics.OverlapSphere(hitCenter, effectiveHitRadius);

        var damagedAlready = new HashSet<IDamageable>();
        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;
            if (!damagedAlready.Add(damageable)) continue;

            damageable.TakeDamage(finalDamage);
        }
    }

    private int GetLevel(KeywordType keyword)
    {
        return skillManager != null ? skillManager.GetKeywordLevel(keyword) : 0;
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