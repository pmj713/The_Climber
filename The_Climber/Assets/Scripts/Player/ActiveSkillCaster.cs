using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PlayerActiveSkillSlots에 장착된 스킬을 실제로 발동시킨다 (0: 우클릭, 1: E, 2: Q).
// 스킬 효과는 해당 스킬을 몇 번 획득했는지(레벨)에 비례해 커진다.
[RequireComponent(typeof(PlayerActiveSkillSlots))]
public class ActiveSkillCaster : MonoBehaviour
{
    private static readonly KeyCode[] SlotKeys = { KeyCode.Mouse1, KeyCode.E, KeyCode.Q };

    [SerializeField] private LayerMask enemyMask = 8;

    [Header("화살 비")]
    [SerializeField] private float arrowRainRadius = 4f;
    [SerializeField] private float arrowRainRadiusPercentPerLevel = 0.10f;
    [SerializeField] private float arrowRainWarmup = 0.8f;
    [SerializeField] private int arrowRainDamage = 20;

    [Header("헤이스트")]
    [SerializeField] private float hastePercentPerLevel = 0.10f;
    [SerializeField] private float hasteDuration = 5f;

    [Header("검기")]
    [SerializeField] private float swordWaveSpeed = 12f;
    [SerializeField] private float swordWaveMaxDistance = 8f;
    [SerializeField] private float swordWaveRadius = 0.8f;
    [SerializeField] private int swordWaveDamage = 15;
    [SerializeField] private float swordWaveDamagePercentPerLevel = 0.2f;
    [SerializeField] private GameObject swordWaveVisualPrefab;

    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private PlayerActiveSkillSlots slots;
    private PlayerSkillManager skillManager;
    private PlayerMovement movement;
    private WeaponController weaponController;
    private Animator animator;
    private readonly float[] nextCastTime = new float[PlayerActiveSkillSlots.SlotCount];

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
        for (int i = 0; i < SlotKeys.Length; i++)
        {
            if (Input.GetKeyDown(SlotKeys[i]))
            {
                TryCast(i);
            }
        }
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
        int level = Mathf.Max(1, skillManager != null ? skillManager.CountAcquired(skill) : 1);

        if (animator != null && !IsBuff(skill.activeType))
        {
            animator.SetTrigger(AttackHash);
        }

        switch (skill.activeType)
        {
            case ActiveSkillType.ArrowRain:
                Vector3 targetPoint = movement != null ? movement.GetAimPoint() : transform.position;
                float radius = arrowRainRadius * (1f + arrowRainRadiusPercentPerLevel * (level - 1));
                StartCoroutine(ArrowRainRoutine(targetPoint, radius));
                break;
            case ActiveSkillType.Haste:
                float bonusPercent = hastePercentPerLevel * level; // 1레벨 10%, 2레벨 20% ...
                float cooldownMultiplier = Mathf.Max(0.05f, 1f - bonusPercent);
                float moveMultiplier = 1f + bonusPercent;
                if (weaponController != null) weaponController.ApplyTemporaryHaste(cooldownMultiplier, hasteDuration);
                if (movement != null) movement.ApplyTemporarySpeedBoost(moveMultiplier, hasteDuration);
                break;
            case ActiveSkillType.SwordWave:
                Vector3 waveOrigin = transform.position + Vector3.up;
                Vector3 waveDirection = movement != null ? movement.GetAimDirection() : transform.forward;
                int waveDamage = Mathf.RoundToInt(swordWaveDamage * (1f + swordWaveDamagePercentPerLevel * (level - 1)));
                StartCoroutine(SwordWaveRoutine(waveOrigin, waveDirection, waveDamage));
                break;
        }
    }

    // 버프성 스킬(지속 효과만 있고 별도 동작이 없는 스킬)은 공격 애니메이션을 재생하지 않는다
    private bool IsBuff(ActiveSkillType type)
    {
        return type == ActiveSkillType.Haste;
    }

    private IEnumerator ArrowRainRoutine(Vector3 targetPoint, float radius)
    {
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(indicator.GetComponent<Collider>());
        indicator.transform.position = targetPoint + Vector3.up * 0.05f;
        indicator.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);

        Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
        if (indicatorRenderer != null) indicatorRenderer.material.color = new Color(1f, 0.2f, 0.2f, 0.5f);

        yield return new WaitForSeconds(arrowRainWarmup);

        Collider[] hits = Physics.OverlapSphere(targetPoint, radius, enemyMask);
        foreach (Collider hit in hits)
        {
            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(arrowRainDamage);
            }
        }

        Destroy(indicator);
    }

    // VARCO3D 검기의 모델 정면(+X)을 조준 방향에 맞춰 발사한다.
    private IEnumerator SwordWaveRoutine(Vector3 origin, Vector3 direction, int damage)
    {
        if (swordWaveVisualPrefab == null) yield break;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        GameObject waveObject = Instantiate(swordWaveVisualPrefab, origin,
            Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -90f, 0f));
        Vector3 position = origin;
        var alreadyHit = new HashSet<IDamageable>();
        float traveled = 0f;

        while (traveled < swordWaveMaxDistance)
        {
            float step = swordWaveSpeed * Time.deltaTime;
            position += direction * step;
            traveled += step;

            waveObject.transform.position = position;

            Collider[] hits = Physics.OverlapSphere(position, swordWaveRadius, enemyMask);
            foreach (Collider hit in hits)
            {
                if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;
                if (!alreadyHit.Add(damageable)) continue;

                damageable.TakeDamage(damage);
            }

            yield return null;
        }

        Destroy(waveObject);
    }
}
