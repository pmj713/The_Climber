using System.Collections.Generic;
using UnityEngine;

// EnemyStatusEffects의 상태(화상/냉기/빙결)를 매 프레임 읽어서 몸에 붙는 이펙트를 켜고 끈다.
// 상태 로직은 건드리지 않고 보여주기만 담당한다. 이펙트는 키 2m 몸 기준으로 만들어 두고 몸집에 맞춰 키운다.
[RequireComponent(typeof(EnemyStatusEffects))]
public class EnemyStatusVisual : MonoBehaviour
{
    [SerializeField] private GameObject burnAuraPrefab;    // 화상: 몸에서 타오르는 불길 (스택만큼 세짐)
    [SerializeField] private GameObject chillAuraPrefab;   // 냉기 스택: 발밑 냉기와 서리 (스택만큼 세짐)
    [SerializeField] private GameObject frozenShellPrefab; // 완전 빙결: 몸을 감싸는 얼음 (반지름 1 x 높이 1 기준)
    [SerializeField] private GameObject freezeBurstPrefab; // 완전 빙결되는 순간
    [SerializeField] private GameObject shatterPrefab;     // 빙결이 깨지는 순간

    private const float ReferenceHeight = 2f;

    private EnemyStatusEffects status;
    private EnemyHealth health;
    private Animator animator;
    private float bodyHeight = ReferenceHeight;
    private float bodyRadius = 0.5f;

    private GameObject burnAura;
    private GameObject chillAura;
    private GameObject frozenShell;
    private bool wasFrozen;
    private float animatorSpeedBeforeFreeze = 1f;
    private readonly Dictionary<ParticleSystem, float> baseRates = new Dictionary<ParticleSystem, float>();

    private void Awake()
    {
        status = GetComponent<EnemyStatusEffects>();
        health = GetComponent<EnemyHealth>();
        animator = GetComponentInChildren<Animator>();

        // 몸 콜라이더로 몸집을 잰다 (발밑 피벗 기준)
        if (TryGetComponent<Collider>(out var body))
        {
            Bounds b = body.bounds;
            bodyHeight = Mathf.Max(0.5f, b.max.y - transform.position.y);
            bodyRadius = Mathf.Max(0.3f, Mathf.Max(b.extents.x, b.extents.z));
        }
    }

    private void Update()
    {
        bool dead = health != null && health.IsDead;
        bool burning = !dead && status.ActiveStatus == EnemyStatusEffects.StatusType.Burn && status.BurnStacks > 0;
        bool frozen = !dead && status.IsFrozen;
        bool chilled = !dead && !frozen && status.ActiveStatus == EnemyStatusEffects.StatusType.Freeze && status.FreezeStacks > 0;

        burnAura = Toggle(burnAura, burning, burnAuraPrefab);
        if (burning) SetIntensity(burnAura, status.BurnStackRatio);

        chillAura = Toggle(chillAura, chilled, chillAuraPrefab);
        if (chilled) SetIntensity(chillAura, status.FreezeStackRatio);

        if (frozen && !wasFrozen) EnterFrozen();
        else if (!frozen && wasFrozen) ExitFrozen(!dead);
        wasFrozen = frozen;
    }

    private GameObject Toggle(GameObject current, bool on, GameObject prefab)
    {
        if (on && current == null && prefab != null)
        {
            GameObject effect = Instantiate(prefab, transform.position, transform.rotation, transform);
            effect.transform.localScale = Vector3.one * (bodyHeight / ReferenceHeight);
            foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>())
                baseRates[ps] = ps.emission.rateOverTimeMultiplier;
            return effect;
        }
        if (!on && current != null)
        {
            foreach (ParticleSystem ps in current.GetComponentsInChildren<ParticleSystem>()) baseRates.Remove(ps);
            EffectTint.StopAndDestroy(current);
            return null;
        }
        return current;
    }

    // 스택이 적을 때도 보이도록 35%에서 시작해 최대 스택에서 100%
    private void SetIntensity(GameObject effect, float ratio)
    {
        float k = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(ratio));
        foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>())
        {
            if (!baseRates.TryGetValue(ps, out float rate)) continue;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTimeMultiplier = rate * k;
        }
    }

    private void EnterFrozen()
    {
        SpawnOneShot(freezeBurstPrefab);
        if (frozenShellPrefab != null && frozenShell == null)
        {
            frozenShell = Instantiate(frozenShellPrefab, transform.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            frozenShell.transform.localScale = new Vector3(bodyRadius * 1.35f, bodyHeight * 1.05f, bodyRadius * 1.35f);
        }

        // 얼음 속에서 동작이 멈춘 것처럼 애니메이션도 정지
        if (animator != null)
        {
            animatorSpeedBeforeFreeze = animator.speed;
            animator.speed = 0f;
        }
    }

    private void ExitFrozen(bool shattered)
    {
        if (frozenShell != null) { Destroy(frozenShell); frozenShell = null; }
        if (shattered) SpawnOneShot(shatterPrefab);
        if (animator != null) animator.speed = animatorSpeedBeforeFreeze;
    }

    private void SpawnOneShot(GameObject prefab)
    {
        if (prefab == null) return;
        GameObject effect = Instantiate(prefab, transform.position, Quaternion.identity);
        effect.transform.localScale = Vector3.one * (bodyHeight / ReferenceHeight);
        Destroy(effect, 2.5f);
    }
}
