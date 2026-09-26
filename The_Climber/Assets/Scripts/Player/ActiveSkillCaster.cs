using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PlayerActiveSkillSlots에 장착된 스킬을 실제로 발동시킨다 (0: 우클릭, 1: E, 2: Q).
// 스킬 효과는 해당 스킬을 몇 번 획득했는지(레벨)에 비례해 커진다.
[RequireComponent(typeof(PlayerActiveSkillSlots))]
public class ActiveSkillCaster : MonoBehaviour
{
    private static readonly RebindableAction[] SlotActions =
        { RebindableAction.SkillRightClick, RebindableAction.SkillE, RebindableAction.SkillQ };

    [SerializeField] private LayerMask enemyMask = 8;

    [Header("화살 비")]
    [SerializeField] private float arrowRainRadius = 2.8f;
    [SerializeField] private float arrowRainRadiusPercentPerLevel = 0.10f;
    [SerializeField] private float arrowRainWarmup = 0.8f;
    [SerializeField] private int arrowRainDamage = 20;
    [SerializeField] private GameObject arrowRainArrowPrefab;
    [SerializeField] private float arrowRainArrowsPerRadius = 4f;
    [SerializeField] private float arrowRainArrowSpawnHeight = 9f;
    [SerializeField] private float arrowRainArrowFallSpeed = 22f;

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
        for (int i = 0; i < SlotActions.Length; i++)
        {
            if (Input.GetKeyDown(KeyBindingManager.GetKey(SlotActions[i])))
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
                StartCoroutine(HasteActivationBurst());
                StartCoroutine(HasteAuraRoutine(hasteDuration));
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
        GameObject indicator = CreateRangeRingIndicator(targetPoint, radius);

        if (arrowRainArrowPrefab != null)
        {
            int arrowCount = Mathf.Clamp(Mathf.RoundToInt(radius * arrowRainArrowsPerRadius), 10, 40);
            for (int i = 0; i < arrowCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 landingPoint = targetPoint + new Vector3(offset.x, 0f, offset.y);
                float startDelay = Random.Range(0f, arrowRainWarmup * 0.6f);
                StartCoroutine(FallArrow(landingPoint, startDelay));
            }
        }

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

    // 헤이스트 발동 순간, 발밑에서 확 퍼졌다 사라지는 원형 파동.
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

    // 헤이스트 지속시간 동안 발밑에서 위로 올라가는 노란 스파클 오라.
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

    // 화살 하나가 목표 지점 위에서 촉을 아래로 향한 채 비처럼 떨어져 박히는 연출.
    private IEnumerator FallArrow(Vector3 landingPoint, float startDelay)
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        // 완전히 수직은 아니고 살짝 기울어진 채로 떨어지게 해서 빗줄기 같은 느낌을 준다.
        float tiltAngle = Random.Range(0f, 12f);
        float tiltDirection = Random.Range(0f, 360f);
        Vector3 fallDirection = Quaternion.AngleAxis(tiltDirection, Vector3.up) *
            Quaternion.AngleAxis(tiltAngle, Vector3.right) * Vector3.down;

        // Z축(화살촉 축) 기준 회전만 무작위로 줘서, 항상 촉이 낙하 방향(아래쪽)을 향하게 한다.
        Quaternion rotation = Quaternion.LookRotation(fallDirection) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Vector3 startPos = landingPoint + Vector3.up * arrowRainArrowSpawnHeight;
        GameObject arrow = Instantiate(arrowRainArrowPrefab, startPos, rotation);

        float fallSpeed = arrowRainArrowFallSpeed * Random.Range(0.85f, 1.15f);
        float traveled = 0f;
        while (traveled < arrowRainArrowSpawnHeight)
        {
            traveled += fallSpeed * Time.deltaTime;
            arrow.transform.position = Vector3.Lerp(startPos, landingPoint, Mathf.Clamp01(traveled / arrowRainArrowSpawnHeight));
            yield return null;
        }

        arrow.transform.position = landingPoint;
        yield return new WaitForSeconds(0.4f);
        Destroy(arrow);
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
