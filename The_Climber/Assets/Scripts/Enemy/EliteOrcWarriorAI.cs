using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 엘리트 근접형(오크 전사). 일반 근접 몬스터와 달리 경직 저항을 갖고, 강타/돌진/가드 3가지
// 고유 패턴을 쓴다. NC AI-기획 요약 페이지의 "엘리트 몬스터 패턴 (4층)" 표를 기준으로 구현.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EliteOrcWarriorAI : MonoBehaviour, IEnemyEmpowerable
{
    private enum State { Idle, Chase, AttackPrepare, Busy, Cooldown }

    [Header("기본 이동/추적")]
    [SerializeField] private float detectRange = 9f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private Animator animator;

    [Header("기본 공격 (강타가 아닐 때)")]
    [SerializeField] private float attackPrepareTime = 0.4f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private int damage = 10;
    [SerializeField] private EnemyMeleeWeaponHitbox weaponHitbox;
    [SerializeField] private float weaponActiveDuration = 0.32f;
    [SerializeField] private float weaponHitDelay = 0.36f;

    [Header("강타 - 상시, 기본 공격 대체(확률), 0.9초 예고, 2배 피해+넉백")]
    [SerializeField, Range(0f, 1f)] private float slamChance = 0.4f;
    [SerializeField] private float slamTelegraphDuration = 0.9f;
    [SerializeField] private float slamDamageMultiplier = 2f;
    [SerializeField] private float slamRadius = 1.3f;
    [SerializeField] private float slamKnockbackForce = 10f;
    [SerializeField] private float slamRecovery = 0.4f;
    [SerializeField] private ZoneTelegraphVisual slamTelegraphPrefab; // 비워두면 기본 링 예고
    [SerializeField] private GameObject slamWeaponChargePrefab;       // 예고 동안 무기에 모이는 불꽃
    [SerializeField] private GameObject slamImpactPrefab;             // 반지름 1 기준, slamRadius 배로 키워서 터뜨림

    [Header("돌진 - 플레이어가 공격범위 밖에서 거리 유지 시, 0.9초 예고 후 직선 돌진")]
    [SerializeField] private float chargeTriggerRange = 8f; // 이 거리 이내에서 '거리 유지'로 판정
    [SerializeField] private float chargeKeepDistanceTime = 2f;
    [SerializeField] private float chargeTelegraphDuration = 0.9f;
    [SerializeField] private float chargeSpeed = 14f;
    [SerializeField] private float chargeMaxDistance = 7f;
    [SerializeField] private float chargeHitRadius = 1f;
    [SerializeField] private int chargeDamage = 14;
    [SerializeField] private float chargeWallStunDuration = 1f;
    [SerializeField] private float chargeCooldown = 5f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private LaneTelegraphVisual chargeTelegraphPrefab; // 비워두면 기본 선 예고
    [SerializeField] private GameObject chargeWindupPrefab;  // 예고 동안 발밑 흙먼지
    [SerializeField] private GameObject chargeTrailPrefab;   // 돌진 중 먼지/속도선
    [SerializeField] private GameObject chargeImpactPrefab;  // 플레이어/벽에 부딪힐 때

    [Header("가드 - 체력 50% 이하부터 쿨타임마다, 정면 피해 80% 감소")]
    [SerializeField, Range(0f, 1f)] private float guardHealthThreshold = 0.5f;
    [SerializeField] private float guardTelegraphDuration = 1.3f;
    [SerializeField] private float guardHoldDuration = 2f;
    [SerializeField, Range(0f, 1f)] private float guardDamageReduction = 0.8f;
    [SerializeField] private float guardCooldown = 8f;
    [SerializeField] private float guardCounterKnockback = 6f;
    [SerializeField] private GameObject guardChargePrefab;  // 가드 준비 동안 모이는 기운
    [SerializeField] private GameObject guardBarrierPrefab; // 가드 유지 동안 정면 방벽
    [SerializeField] private GameObject guardBlockPrefab;   // 가드 중 피격 시 막아내는 불꽃

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int SlamHash = Animator.StringToHash("Slam");
    private static readonly int ChargingHash = Animator.StringToHash("Charging");
    private static readonly int ChargeMotionSpeedHash = Animator.StringToHash("ChargeMotionSpeed");
    private static readonly int GuardingHash = Animator.StringToHash("Guarding");

    private NavMeshAgent agent;
    private EnemyHealth health;
    private EnemyStatusEffects status;
    private Transform target;
    private State state;
    private float stateTimer;
    private float keepDistanceTimer;
    private float nextChargeTime;
    private float nextGuardTime;
    private bool isGuarding;
    private bool guardInProgress; // 가드 준비~유지 전체 구간 (빙결로 끊겼을 때 쿨타임을 걸기 위함)
    private bool interruptedByFreeze;
    // 패턴 도중 사망하면 코루틴이 끊기므로, 예고/지속 이펙트를 모아뒀다가 HandleDeath에서 한 번에 지운다.
    private readonly List<GameObject> patternEffects = new List<GameObject>();

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        status = GetComponent<EnemyStatusEffects>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (weaponHitbox == null) weaponHitbox = GetComponentInChildren<EnemyMeleeWeaponHitbox>(true);
        agent.speed = moveSpeed;
        agent.updateRotation = false;
        health.SetStunResistant(true);
        state = State.Idle;
    }

    private void OnEnable()
    {
        health.OnDeath += HandleDeath;
        health.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        health.OnDeath -= HandleDeath;
        health.OnDamaged -= HandleDamaged;
        StopAllCoroutines();
    }

    private void Update()
    {
        if (health.IsDead) return;

        if (status != null && status.IsFrozen)
        {
            if (!interruptedByFreeze) InterruptForFreeze();
            agent.isStopped = true;
            return;
        }
        if (interruptedByFreeze) ResumeAfterFreeze();

        if (target == null) FindTarget();

        // 가드는 체력 조건 + 쿨타임만 맞으면 이동/추적/공격 전 단계를 가리지 않고 끼어든다.
        if (!isGuarding && state != State.Busy &&
            health.HealthFraction <= guardHealthThreshold && Time.time >= nextGuardTime)
        {
            StartCoroutine(GuardRoutine());
        }
        else
        {
            switch (state)
            {
                case State.Idle: TickIdle(); break;
                case State.Chase: TickChase(); break;
                case State.AttackPrepare: TickAttackPrepare(); break;
                case State.Cooldown: TickCooldown(); break;
                // Busy(강타/돌진/가드 진행 중)는 해당 코루틴이 전환까지 전부 담당한다.
            }
        }

        if (animator != null)
        {
            animator.SetBool(IsMovingHash, state == State.Chase && agent.velocity.sqrMagnitude > 0.01f);
        }

        if (state == State.Chase) FaceMovementDirection();
        else if (state == State.AttackPrepare) FaceTarget();
    }

    private void FindTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange, playerMask);
        if (hits.Length > 0) target = hits[0].transform;
    }

    private float DistanceToTarget() => target == null ? Mathf.Infinity : Vector3.Distance(transform.position, target.position);

    private void FaceTarget()
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    // 추적 중에는 목표 방향이 아니라 실제 이동 방향(velocity)을 바라봐야 경로가 꺾여도 걷는 방향과 시선이 어긋나지 않는다.
    private void FaceMovementDirection()
    {
        Vector3 dir = agent.velocity;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
        {
            FaceTarget();
            return;
        }
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private void TickIdle()
    {
        agent.isStopped = true;
        if (target != null && DistanceToTarget() <= detectRange) state = State.Chase;
    }

    private void TickChase()
    {
        if (target == null) { state = State.Idle; return; }

        agent.isStopped = false;
        agent.SetDestination(target.position);

        float dist = DistanceToTarget();

        if (dist <= attackRange)
        {
            keepDistanceTimer = 0f;
            agent.isStopped = true;
            state = State.AttackPrepare;
            stateTimer = attackPrepareTime;
            return;
        }

        if (dist <= chargeTriggerRange)
        {
            keepDistanceTimer += Time.deltaTime;
            if (keepDistanceTimer >= chargeKeepDistanceTime && Time.time >= nextChargeTime)
            {
                keepDistanceTimer = 0f;
                StartCoroutine(ChargeRoutine());
            }
        }
        else
        {
            keepDistanceTimer = 0f;
        }
    }

    private void TickAttackPrepare()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            bool isSlam = Random.value < slamChance;
            StartCoroutine(isSlam ? SlamRoutine() : AttackRoutine());
        }
    }

    private void TickCooldown()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            state = (target != null && DistanceToTarget() <= detectRange) ? State.Chase : State.Idle;
        }
    }

    // ---------------- 기본 공격 ----------------

    private IEnumerator AttackRoutine()
    {
        state = State.Busy;
        if (animator != null) animator.SetTrigger(AttackHash);

        yield return new WaitForSeconds(weaponHitDelay);
        if (!health.IsDead && weaponHitbox != null) weaponHitbox.Activate(damage, weaponActiveDuration);

        yield return new WaitForSeconds(weaponActiveDuration);
        EndBusy(attackCooldown);
    }

    // ---------------- 강타 ----------------

    private IEnumerator SlamRoutine()
    {
        state = State.Busy;
        if (animator != null) animator.SetTrigger(SlamHash); // 머리 위로 무기를 들어올리는 예고 동작
        Vector3 telegraphCenter = transform.position + transform.forward * attackRange;
        GameObject indicator;
        if (slamTelegraphPrefab != null)
        {
            ZoneTelegraphVisual telegraph = Instantiate(slamTelegraphPrefab, telegraphCenter + Vector3.up * 0.05f, transform.rotation);
            telegraph.Begin(slamRadius, slamTelegraphDuration);
            indicator = TrackEffect(telegraph.gameObject);
        }
        else
        {
            indicator = TrackEffect(CreateRingIndicator(telegraphCenter, slamRadius, new Color(1f, 0.25f, 0.1f, 0.9f)));
        }
        GameObject weaponCharge = slamWeaponChargePrefab != null && weaponHitbox != null
            ? TrackEffect(Instantiate(slamWeaponChargePrefab, weaponHitbox.transform.position, weaponHitbox.transform.rotation, weaponHitbox.transform))
            : null;

        yield return new WaitForSeconds(slamTelegraphDuration);
        RemoveEffect(indicator);
        RemoveEffect(weaponCharge);

        if (!health.IsDead)
        {
            Vector3 hitCenter = transform.position + transform.forward * attackRange;
            SpawnScaledEffect(slamImpactPrefab, hitCenter, slamRadius);
            int slamDamageAmount = Mathf.RoundToInt(damage * slamDamageMultiplier);
            Collider[] hits = Physics.OverlapSphere(hitCenter, slamRadius, playerMask);
            foreach (Collider hit in hits)
            {
                if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;
                damageable.TakeDamage(slamDamageAmount);

                if (hit.TryGetComponent<PlayerMovement>(out var movement))
                {
                    Vector3 dir = hit.transform.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f) movement.ApplyKnockback(dir.normalized * slamKnockbackForce);
                }
            }
        }

        yield return new WaitForSeconds(slamRecovery);
        EndBusy(attackCooldown);
    }

    // ---------------- 돌진 ----------------

    private IEnumerator ChargeRoutine()
    {
        state = State.Busy;
        agent.isStopped = true;
        if (animator != null)
        {
            animator.SetFloat(ChargeMotionSpeedHash, 0f); // 전조 동안 왼쪽 어깨를 내민 준비 자세를 유지한다.
            animator.SetBool(ChargingHash, true);
        }

        Vector3 direction = target != null ? target.position - transform.position : transform.forward;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(direction);

        GameObject pathIndicator;
        if (chargeTelegraphPrefab != null)
        {
            LaneTelegraphVisual lane = Instantiate(chargeTelegraphPrefab, transform.position + Vector3.up * 0.05f, Quaternion.LookRotation(direction));
            lane.Begin(chargeMaxDistance, chargeHitRadius * 2f, chargeTelegraphDuration);
            pathIndicator = TrackEffect(lane.gameObject);
        }
        else
        {
            pathIndicator = TrackEffect(CreateLineIndicator(direction, chargeMaxDistance, chargeHitRadius * 2f, new Color(1f, 0.25f, 0.1f, 0.5f)));
        }
        GameObject windup = chargeWindupPrefab != null
            ? TrackEffect(Instantiate(chargeWindupPrefab, transform.position, transform.rotation, transform))
            : null;

        yield return new WaitForSeconds(chargeTelegraphDuration);
        RemoveEffect(pathIndicator);
        RemoveEffect(windup);

        nextChargeTime = Time.time + chargeCooldown;

        if (health.IsDead)
        {
            if (animator != null) animator.SetBool(ChargingHash, false);
            yield break;
        }

        if (animator != null) animator.SetFloat(ChargeMotionSpeedHash, 1f);
        GameObject trail = chargeTrailPrefab != null
            ? TrackEffect(Instantiate(chargeTrailPrefab, transform.position, transform.rotation, transform))
            : null;

        var alreadyHit = new HashSet<IDamageable>();
        float traveled = 0f;
        bool hitWall = false;

        while (traveled < chargeMaxDistance)
        {
            float step = chargeSpeed * Time.deltaTime;

            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction, step + 0.3f, obstacleMask))
            {
                hitWall = true;
                break;
            }

            agent.Move(direction * step);
            traveled += step;

            Collider[] hits = Physics.OverlapSphere(transform.position, chargeHitRadius, playerMask);
            foreach (Collider hit in hits)
            {
                if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;
                if (!alreadyHit.Add(damageable)) continue;
                damageable.TakeDamage(chargeDamage);
                SpawnScaledEffect(chargeImpactPrefab, hit.bounds.center, 0.7f);
            }

            yield return null;
        }

        ReleaseTrail(trail);
        if (hitWall) SpawnScaledEffect(chargeImpactPrefab, transform.position + direction * 0.8f + Vector3.up * 1.1f, 1f);

        if (animator != null) animator.SetFloat(ChargeMotionSpeedHash, 0f);
        if (hitWall) yield return new WaitForSeconds(chargeWallStunDuration);

        if (animator != null) animator.SetBool(ChargingHash, false);
        EndBusy(0f);
    }

    // ---------------- 가드 ----------------

    private IEnumerator GuardRoutine()
    {
        state = State.Busy;
        agent.isStopped = true;
        guardInProgress = true;

        GameObject gather = guardChargePrefab != null
            ? TrackEffect(Instantiate(guardChargePrefab, transform.position, transform.rotation, transform))
            : null;
        yield return new WaitForSeconds(guardTelegraphDuration);
        RemoveEffect(gather);

        if (health.IsDead) yield break;

        isGuarding = true;
        if (animator != null) animator.SetBool(GuardingHash, true);
        health.IncomingDamageMultiplier = 1f - guardDamageReduction;
        GameObject barrier = guardBarrierPrefab != null
            ? TrackEffect(Instantiate(guardBarrierPrefab, transform.position, transform.rotation, transform))
            : null;

        yield return new WaitForSeconds(guardHoldDuration);

        RemoveEffect(barrier);
        isGuarding = false;
        if (animator != null) animator.SetBool(GuardingHash, false);
        health.IncomingDamageMultiplier = 1f;
        nextGuardTime = Time.time + guardCooldown;
        guardInProgress = false;

        EndBusy(0f);
    }

    private void HandleDamaged(int amount)
    {
        if (!isGuarding || amount <= 0) return;

        Vector3 dir = target != null ? target.position - transform.position : transform.forward;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

        if (guardBlockPrefab != null)
        {
            Destroy(Instantiate(guardBlockPrefab, transform.position + Vector3.up * 1.2f + dir * 1.1f, Quaternion.LookRotation(dir)), 2f);
        }

        if (target == null || !target.TryGetComponent<PlayerMovement>(out var movement)) return;
        movement.ApplyKnockback(dir * guardCounterKnockback);
    }

    // ---------------- 이펙트 관리 ----------------

    private GameObject TrackEffect(GameObject effect)
    {
        if (effect != null) patternEffects.Add(effect);
        return effect;
    }

    private void RemoveEffect(GameObject effect)
    {
        if (effect == null) return;
        patternEffects.Remove(effect);
        Destroy(effect);
    }

    private void ClearPatternEffects()
    {
        foreach (GameObject effect in patternEffects)
        {
            if (effect != null) Destroy(effect);
        }
        patternEffects.Clear();
    }

    // 돌진 잔상은 몸에서 떼어낸 뒤 방출만 멈춰서, 이미 뿌린 먼지가 자연스럽게 사라지게 한다.
    private void ReleaseTrail(GameObject trail)
    {
        if (trail == null) return;
        patternEffects.Remove(trail);
        trail.transform.SetParent(null, true);
        foreach (ParticleSystem ps in trail.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        Destroy(trail, 1.5f);
    }

    private static void SpawnScaledEffect(GameObject prefab, Vector3 position, float scale)
    {
        if (prefab == null) return;
        GameObject effect = Instantiate(prefab, position, Quaternion.identity);
        effect.transform.localScale = Vector3.one * scale;
        Destroy(effect, 3f);
    }

    private void EndBusy(float cooldown)
    {
        if (health.IsDead) return;

        agent.isStopped = false;
        if (cooldown > 0f)
        {
            state = State.Cooldown;
            stateTimer = cooldown;
        }
        else
        {
            state = (target != null && DistanceToTarget() <= detectRange) ? State.Chase : State.Idle;
        }
    }

    // ---------------- 예고 연출 ----------------

    private GameObject CreateRingIndicator(Vector3 center, float radius, Color color)
    {
        GameObject indicator = new GameObject("EliteTelegraphRing");
        indicator.transform.position = center + Vector3.up * 0.05f;

        const int segments = 28;
        LineRenderer ring = indicator.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = segments;
        ring.startWidth = ring.endWidth = 0.08f;
        ring.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        ring.startColor = ring.endColor = color;
        ring.material.SetColor("_BaseColor", color);

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        return indicator;
    }

    private GameObject CreateLineIndicator(Vector3 direction, float length, float width, Color color)
    {
        GameObject indicator = new GameObject("EliteTelegraphLine");
        LineRenderer line = indicator.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = line.endWidth = width;
        line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        line.startColor = line.endColor = color;
        line.material.SetColor("_BaseColor", color);

        Vector3 start = transform.position + Vector3.up * 0.05f;
        line.SetPosition(0, start);
        line.SetPosition(1, start + direction * length);

        return indicator;
    }

    // 빙결되면 강타/돌진/가드를 즉시 끊고 예고·이펙트·피해 감소를 모두 걷어낸다.
    private void InterruptForFreeze()
    {
        interruptedByFreeze = true;
        StopAllCoroutines();
        ClearPatternEffects();
        if (weaponHitbox != null) weaponHitbox.Deactivate();

        if (guardInProgress)
        {
            nextGuardTime = Time.time + guardCooldown; // 녹자마자 다시 가드하지 않게
            guardInProgress = false;
        }
        isGuarding = false;
        health.IncomingDamageMultiplier = 1f;

        if (animator != null)
        {
            animator.SetBool(ChargingHash, false);
            animator.SetBool(GuardingHash, false);
            animator.SetFloat(ChargeMotionSpeedHash, 1f);
        }
        keepDistanceTimer = 0f;
        state = State.Idle;
    }

    private void ResumeAfterFreeze()
    {
        interruptedByFreeze = false;
        EnemyFreezeInterrupt.ReturnToIdle(animator, AttackHash, SlamHash);
    }

    private void HandleDeath()
    {
        ClearPatternEffects();
        agent.isStopped = true;
        enabled = false;
        Destroy(gameObject, 0f);
    }

    // 제단의 '적 이동속도' 강화. 돌진 같은 패턴 속도는 그대로 두고 평소 이동 속도만 올린다.
    public void ApplyMoveSpeedMultiplier(float multiplier)
    {
        moveSpeed *= multiplier;
        if (agent != null) agent.speed = moveSpeed;
    }

    public void ApplyDamageMultiplier(float multiplier)
    {
        damage = Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
        chargeDamage = Mathf.Max(1, Mathf.RoundToInt(chargeDamage * multiplier));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(transform.position, chargeTriggerRange);
    }
}
