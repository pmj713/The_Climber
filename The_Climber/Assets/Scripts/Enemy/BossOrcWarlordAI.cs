using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 4층 보스(오크 대전사). "엘리트 몬스터 패턴 (4층)" 문서의 보스 패턴 목록을 기준으로 구현:
// 돌진 베기(상시) / 광역 강타(75% 이하) / 투척 난사(50% 이하) / 졸개 소환(75·50·25% 진입 1회씩) / 분노 연계(25% 이하 전용).
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class BossOrcWarlordAI : MonoBehaviour, IEnemyEmpowerable, IEnemyDetectionConfigurable, IEnemyPhasing
{
    private enum State { Idle, Chase, AttackPrepare, Busy, Cooldown }

    [Header("기본 이동/추적")]
    [SerializeField] private float detectRange = 12f;
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Animator animator;

    [Header("기본 공격 (근접, 패턴 표에 없는 평타) - 타이밍은 OrcWarlord_Attack_Heavy 클립 기준 (임팩트 1.08초)")]
    [SerializeField] private float attackPrepareTime = 0.15f;
    [SerializeField] private int attackDamage = 12;
    [SerializeField] private EnemyMeleeWeaponHitbox weaponHitbox;
    [SerializeField] private float weaponActiveDuration = 0.3f;
    [SerializeField] private float weaponHitDelay = 0.98f;
    [SerializeField] private float attackRecovery = 0.25f; // 판정 뒤 팔로스루 동안 제자리에 둔다
    [SerializeField] private float attackCooldown = 0.8f;

    [Header("돌진 베기 - 상시, 0.6초 예고 후 직선 경로 큰 피해 + 넉백")]
    [SerializeField] private float chargeSlashTriggerRange = 9f;
    [SerializeField] private float chargeSlashKeepDistanceTime = 1.2f;
    [SerializeField] private float chargeSlashTelegraphDuration = 0.6f;
    [SerializeField] private float chargeSlashSpeed = 16f;
    [SerializeField] private float chargeSlashMaxDistance = 8f;
    [SerializeField] private float chargeSlashHitRadius = 1.3f;
    [SerializeField] private int chargeSlashDamage = 22;
    [SerializeField] private float chargeSlashKnockbackForce = 14f;
    [SerializeField] private float chargeSlashWallStunDuration = 0.5f;
    [SerializeField] private float chargeSlashCooldown = 4f;
    [SerializeField] private LaneTelegraphVisual chargeSlashTelegraphPrefab; // 비워두면 기본 선 예고
    [SerializeField] private GameObject chargeSlashWindupPrefab;  // 예고 동안 발밑 흙먼지/분노 기운
    [SerializeField] private GameObject chargeSlashTrailPrefab;   // 돌진 중 먼지 꼬리/어깨 불꽃
    [SerializeField] private GameObject chargeSlashImpactPrefab;  // 플레이어/벽 충돌 (반지름 1 기준)

    [Header("광역 강타 - 체력 75% 이하부터, 도약 후 착지 지점 확장 원(0.8초) 큰 피해 + 경직")]
    [SerializeField, Range(0f, 1f)] private float aoeSlamHealthThreshold = 0.75f;
    [SerializeField, Range(0f, 1f)] private float aoeSlamChance = 0.4f;
    [SerializeField] private float aoeSlamWindupDuration = 0.55f;   // 웅크리는 준비 동작 (착지 예고 원은 이후 0.8초)
    [SerializeField] private float aoeSlamTelegraphDuration = 0.8f;
    [SerializeField] private float aoeSlamRecovery = 0.6f;         // 착지 후 무릎 꿇은 자세 유지
    [SerializeField] private float aoeSlamRadius = 2.6f;
    [SerializeField] private int aoeSlamDamage = 32;
    [SerializeField] private float aoeSlamStunDuration = 1.2f;
    [SerializeField] private float aoeSlamCooldown = 6f;
    [SerializeField] private ZoneTelegraphVisual aoeSlamTelegraphPrefab; // 비워두면 기본 링 예고
    [SerializeField] private GameObject aoeSlamWeaponChargePrefab;       // 준비~착지 동안 도끼에 붙는 불꽃
    [SerializeField] private GameObject aoeSlamImpactPrefab;             // 반지름 1 기준, aoeSlamRadius 배로 키움

    [Header("투척 난사 - 체력 50% 이하부터, 부채꼴 조준선 예고 후 투사체 3발")]
    [SerializeField, Range(0f, 1f)] private float throwBarrageHealthThreshold = 0.5f;
    [SerializeField] private EnemyProjectile throwBarrageProjectilePrefab;
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private float throwBarrageTelegraphDuration = 1.08f; // 크게 뒤로 젖히는 동안 조준선 표시, 릴리스 순간 발사
    [SerializeField] private float throwBarrageRecovery = 0.6f;
    [SerializeField] private float throwBarrageProjectileScale = 1f; // 공용 EnemyProjectile 프리팹은 그대로 두고 보스가 쏜 것만 키운다
    [SerializeField] private float throwBarrageRange = 10f;
    [SerializeField] private float throwBarrageProjectileSpeed = 11f;
    [SerializeField] private int throwBarrageDamage = 14;
    [SerializeField] private float throwBarrageSpreadAngle = 25f;
    [SerializeField] private float throwBarrageCooldown = 5f;
    [SerializeField] private LaneTelegraphVisual throwBarrageTelegraphPrefab; // 비워두면 기본 선 예고
    [SerializeField] private float throwBarrageTelegraphWidth = 1.2f;
    [SerializeField] private GameObject throwBarrageChargePrefab;  // 뒤로 젖히는 동안 투척 손에 모이는 불꽃
    [SerializeField] private GameObject throwBarrageReleasePrefab; // 놓는 순간 터지는 불꽃 (반지름 1 기준)

    [Header("졸개 소환 - 체력 75/50/25% 진입 시 1회씩, 포효+무적, 근접 몹 1~2마리 소환 후 후퇴")]
    [SerializeField] private GameObject minionPrefab;
    [SerializeField] private float summonTelegraphDuration = 1.7f; // 포효가 끝나는 순간 소환
    [SerializeField] private float summonRecovery = 0.35f;
    [SerializeField] private float summonRetreatDistance = 5f;
    [SerializeField] private int summonMinionCountMin = 1;
    [SerializeField] private int summonMinionCountMax = 2;
    [SerializeField] private float summonSpawnRadius = 3f;
    [SerializeField] private GameObject summonRoarPrefab;  // 포효(무적) 동안 몸에 붙는 충격파/보호막
    [SerializeField] private GameObject summonSpawnPrefab; // 졸개가 나타나는 자리

    [Header("분노 연계 - 체력 25% 이하 전용, 돌진 베기 → 광역 강타 즉시 연속")]
    [SerializeField, Range(0f, 1f)] private float rageComboHealthThreshold = 0.25f;
    [SerializeField] private float rageComboCooldown = 6f;
    [SerializeField] private GameObject rageBurstPrefab; // 연계 시작 순간 터지는 분노
    [SerializeField] private GameObject rageAuraPrefab;  // 연계 동안 몸에 붙는 분노 기운

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int ChargeSlashingHash = Animator.StringToHash("ChargeSlashing");
    private static readonly int ChargeMotionSpeedHash = Animator.StringToHash("ChargeMotionSpeed");
    private static readonly int AoeSlamHash = Animator.StringToHash("AoeSlam");
    private static readonly int ThrowBarrageHash = Animator.StringToHash("ThrowBarrage");
    private static readonly int SummonHash = Animator.StringToHash("Summon");

    private NavMeshAgent agent;
    private EnemyHealth health;
    private EnemyStatusEffects status;
    private Transform target;
    private State state;
    private float stateTimer;
    private float keepDistanceTimer;

    private float nextChargeSlashTime;
    private float nextAoeSlamTime;
    private float nextThrowBarrageTime;
    private float nextRageComboTime;

    private bool hasSummonedAt75;
    private bool hasSummonedAt50;
    private bool hasSummonedAt25;
    // 진행 중인 소환이 사용한 체력 구간(75/50/25). 졸개가 나오기 전에 빙결로 끊기면 이 구간을 되돌려 녹은 뒤 다시 소환한다.
    private int pendingSummonThreshold = -1;

    // 패턴 도중 사망하면 코루틴이 끊기므로, 예고/지속 이펙트를 모아뒀다가 HandleDeath에서 한 번에 지운다.
    private readonly List<GameObject> patternEffects = new List<GameObject>();
    private bool interruptedByFreeze;

    private Collider[] ownColliders;
    private bool phasing;

    public bool IsPhasingThroughPlayer => phasing;

    // 돌진하는 동안은 플레이어와 서로 밀지 않고 통과한다
    private void SetPhasing(bool value)
    {
        phasing = value;
        EnemyPlayerPhasing.Apply(ownColliders, value);
    }

    private void Awake()
    {
        ownColliders = GetComponentsInChildren<Collider>(true);
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
    }

    private void OnDisable()
    {
        health.OnDeath -= HandleDeath;
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

        // 졸개 소환은 체력 구간 진입 시 1회씩, 진행 중인 다른 패턴을 제외하고 최우선으로 끼어든다.
        float healthFraction = health.HealthFraction;
        if (state != State.Busy && TryConsumeSummonThreshold(healthFraction))
        {
            StartCoroutine(SummonRoutine());
        }
        else if (state != State.Busy && healthFraction <= rageComboHealthThreshold && Time.time >= nextRageComboTime &&
                 target != null && DistanceToTarget() <= chargeSlashTriggerRange)
        {
            StartCoroutine(RageComboRoutine());
        }
        else
        {
            switch (state)
            {
                case State.Idle: TickIdle(); break;
                case State.Chase: TickChase(); break;
                case State.AttackPrepare: TickAttackPrepare(); break;
                case State.Cooldown: TickCooldown(); break;
                // Busy(각 패턴 코루틴 진행 중)는 해당 코루틴이 전환까지 전부 담당한다.
            }
        }

        // 패턴 후 Cooldown 중에도 에이전트는 이전 경로를 따라 계속 움직이므로, 추적과 똑같이 이동 방향을 보고 달리기 모션을 쓴다.
        bool locomoting = state == State.Chase || state == State.Cooldown;

        if (animator != null)
        {
            animator.SetBool(IsMovingHash, locomoting && agent.velocity.sqrMagnitude > 0.01f);
        }

        if (locomoting) FaceMovementDirection();
        else if (state == State.AttackPrepare) FaceTarget();
    }

    private bool TryConsumeSummonThreshold(float healthFraction)
    {
        if (!hasSummonedAt75 && healthFraction <= 0.75f) { hasSummonedAt75 = true; pendingSummonThreshold = 75; return true; }
        if (!hasSummonedAt50 && healthFraction <= 0.5f) { hasSummonedAt50 = true; pendingSummonThreshold = 50; return true; }
        if (!hasSummonedAt25 && healthFraction <= 0.25f) { hasSummonedAt25 = true; pendingSummonThreshold = 25; return true; }
        return false;
    }

    // 빙결로 소환이 졸개가 나오기 전에 끊겼을 때 해당 체력 구간을 다시 쓸 수 있게 되돌린다.
    private void RestorePendingSummonThreshold()
    {
        switch (pendingSummonThreshold)
        {
            case 75: hasSummonedAt75 = false; break;
            case 50: hasSummonedAt50 = false; break;
            case 25: hasSummonedAt25 = false; break;
        }
        pendingSummonThreshold = -1;
    }

    private void FindTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange, playerMask);
        if (hits.Length > 0) target = hits[0].transform;
    }

    private float DistanceToTarget() => target == null ? Mathf.Infinity : Vector3.Distance(transform.position, target.position);

    private Vector3 TargetBodyCenter()
    {
        if (target == null) return transform.position + transform.forward * throwBarrageRange + Vector3.up;
        return target.TryGetComponent<Collider>(out var col) ? col.bounds.center : target.position;
    }

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

        if (dist <= chargeSlashTriggerRange)
        {
            keepDistanceTimer += Time.deltaTime;
            if (keepDistanceTimer >= chargeSlashKeepDistanceTime)
            {
                bool canThrow = health.HealthFraction <= throwBarrageHealthThreshold && Time.time >= nextThrowBarrageTime && dist <= throwBarrageRange;
                bool canCharge = Time.time >= nextChargeSlashTime;

                if (canThrow && canCharge)
                {
                    keepDistanceTimer = 0f;
                    StartCoroutine(Random.value < 0.5f ? ThrowBarrageRoutine() : ChargeSlashRoutine());
                }
                else if (canCharge)
                {
                    keepDistanceTimer = 0f;
                    StartCoroutine(ChargeSlashRoutine());
                }
                else if (canThrow)
                {
                    keepDistanceTimer = 0f;
                    StartCoroutine(ThrowBarrageRoutine());
                }
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
            bool canSlam = health.HealthFraction <= aoeSlamHealthThreshold && Time.time >= nextAoeSlamTime;
            bool doSlam = canSlam && Random.value < aoeSlamChance;
            StartCoroutine(doSlam ? AoeSlamRoutine() : AttackRoutine());
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

    // ---------------- 기본 공격 (패턴 표에 없는 평타) ----------------

    private IEnumerator AttackRoutine()
    {
        state = State.Busy;
        if (animator != null) animator.SetTrigger(AttackHash);

        yield return new WaitForSeconds(weaponHitDelay);
        if (!health.IsDead && weaponHitbox != null) weaponHitbox.Activate(attackDamage, weaponActiveDuration);

        yield return new WaitForSeconds(weaponActiveDuration + attackRecovery);
        EndBusy(attackCooldown);
    }

    // ---------------- 돌진 베기 ----------------

    private IEnumerator ChargeSlashRoutine()
    {
        state = State.Busy;
        agent.isStopped = true;
        nextChargeSlashTime = Time.time + chargeSlashCooldown;
        if (animator != null)
        {
            animator.SetFloat(ChargeMotionSpeedHash, 0f); // 예고 동안 질주 자세를 멈춰 웅크린 준비 동작처럼 보이게 한다.
            animator.SetBool(ChargeSlashingHash, true);
        }

        Vector3 direction = target != null ? target.position - transform.position : transform.forward;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(direction);

        GameObject pathIndicator = CreateLaneTelegraph(chargeSlashTelegraphPrefab, direction, chargeSlashMaxDistance, chargeSlashHitRadius * 2f,
            chargeSlashTelegraphDuration, new Color(1f, 0.15f, 0.05f, 0.55f));
        GameObject windup = AttachEffect(chargeSlashWindupPrefab, transform);
        yield return new WaitForSeconds(chargeSlashTelegraphDuration);
        RemoveEffect(pathIndicator);
        RemoveEffect(windup);

        if (health.IsDead)
        {
            if (animator != null) animator.SetBool(ChargeSlashingHash, false);
            yield break;
        }

        if (animator != null) animator.SetFloat(ChargeMotionSpeedHash, 1f);
        GameObject trail = AttachEffect(chargeSlashTrailPrefab, transform);

        var alreadyHit = new HashSet<IDamageable>();
        float traveled = 0f;
        bool hitWall = false;

        SetPhasing(true);
        while (traveled < chargeSlashMaxDistance)
        {
            float step = chargeSlashSpeed * Time.deltaTime;

            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction, step + 0.3f, obstacleMask))
            {
                hitWall = true;
                break;
            }

            agent.Move(direction * step);
            traveled += step;

            Collider[] hits = Physics.OverlapSphere(transform.position, chargeSlashHitRadius, playerMask);
            foreach (Collider hit in hits)
            {
                if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;
                if (!alreadyHit.Add(damageable)) continue;
                damageable.TakeDamage(chargeSlashDamage);
                if (hit.TryGetComponent<PlayerMovement>(out var movement)) movement.ApplyKnockback(direction * chargeSlashKnockbackForce);
                SpawnScaledEffect(chargeSlashImpactPrefab, hit.bounds.center, 1.2f);
            }

            yield return null;
        }

        SetPhasing(false);
        ReleaseTrail(trail);
        if (hitWall) SpawnScaledEffect(chargeSlashImpactPrefab, transform.position + direction * chargeSlashHitRadius + Vector3.up * 2f, 1.8f);

        if (animator != null) animator.SetFloat(ChargeMotionSpeedHash, 0f);
        if (hitWall) yield return new WaitForSeconds(chargeSlashWallStunDuration);

        if (animator != null) animator.SetBool(ChargeSlashingHash, false);
        EndBusy(0.4f);
    }

    // ---------------- 광역 강타 ----------------

    private IEnumerator AoeSlamRoutine()
    {
        state = State.Busy;
        agent.isStopped = true;
        nextAoeSlamTime = Time.time + aoeSlamCooldown;
        if (animator != null) animator.SetTrigger(AoeSlamHash);
        GameObject weaponCharge = weaponHitbox != null ? AttachEffect(aoeSlamWeaponChargePrefab, weaponHitbox.transform) : null;

        // 웅크리며 도끼를 치켜드는 준비 동작 동안 플레이어를 계속 바라본다. 착지 지점은 준비가 끝난 순간의 위치로 잡는다.
        float windup = 0f;
        while (windup < aoeSlamWindupDuration)
        {
            windup += Time.deltaTime;
            FaceTarget();
            yield return null;
        }
        if (health.IsDead) { RemoveEffect(weaponCharge); yield break; }

        Vector3 landCenter = target != null ? target.position : transform.position + transform.forward * attackRange;
        landCenter.y = transform.position.y; // 플레이어 피벗은 몸 중앙(약 0.8m)이라 그대로 쓰면 예고 원이 공중에 뜬다
        GameObject indicator;
        if (aoeSlamTelegraphPrefab != null)
        {
            ZoneTelegraphVisual telegraph = Instantiate(aoeSlamTelegraphPrefab, landCenter + Vector3.up * 0.05f, Quaternion.identity);
            telegraph.Begin(aoeSlamRadius, aoeSlamTelegraphDuration);
            indicator = TrackEffect(telegraph.gameObject);
        }
        else
        {
            indicator = TrackEffect(CreateRingIndicator(landCenter, aoeSlamRadius, new Color(1f, 0.2f, 0.1f, 0.9f)));
        }

        Vector3 leapDir = landCenter - transform.position;
        leapDir.y = 0f;
        float leapDistance = leapDir.magnitude;
        leapDir = leapDistance > 0.0001f ? leapDir.normalized : transform.forward;
        if (leapDistance > 0.0001f) transform.rotation = Quaternion.LookRotation(leapDir);

        float elapsed = 0f;
        while (elapsed < aoeSlamTelegraphDuration)
        {
            elapsed += Time.deltaTime;
            float step = (leapDistance / aoeSlamTelegraphDuration) * Time.deltaTime;
            agent.Move(leapDir * Mathf.Min(step, leapDistance));
            leapDistance = Mathf.Max(0f, leapDistance - step);
            yield return null;
        }
        RemoveEffect(indicator);
        RemoveEffect(weaponCharge);

        if (!health.IsDead)
        {
            SpawnScaledEffect(aoeSlamImpactPrefab, landCenter, aoeSlamRadius);
            Collider[] hits = Physics.OverlapSphere(landCenter, aoeSlamRadius, playerMask);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent<IDamageable>(out var damageable)) damageable.TakeDamage(aoeSlamDamage);
                if (hit.TryGetComponent<PlayerMovement>(out var movement)) movement.ApplyStun(aoeSlamStunDuration);
            }
        }

        yield return new WaitForSeconds(aoeSlamRecovery);
        EndBusy(0.3f);
    }

    // ---------------- 투척 난사 ----------------

    private IEnumerator ThrowBarrageRoutine()
    {
        state = State.Busy;
        agent.isStopped = true;
        nextThrowBarrageTime = Time.time + throwBarrageCooldown;
        if (animator != null) animator.SetTrigger(ThrowBarrageHash);

        Vector3 baseDir = target != null ? target.position - transform.position : transform.forward;
        baseDir.y = 0f;
        baseDir = baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(baseDir);

        float[] angles = { -throwBarrageSpreadAngle, 0f, throwBarrageSpreadAngle };
        var indicators = new List<GameObject>(angles.Length);
        foreach (float angle in angles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
            indicators.Add(CreateLaneTelegraph(throwBarrageTelegraphPrefab, dir, throwBarrageRange, throwBarrageTelegraphWidth,
                throwBarrageTelegraphDuration, new Color(1f, 0.5f, 0.1f, 0.6f), 0.12f));
        }
        GameObject handCharge = throwOrigin != null ? AttachEffect(throwBarrageChargePrefab, throwOrigin) : null;

        yield return new WaitForSeconds(throwBarrageTelegraphDuration);
        foreach (GameObject indicator in indicators) RemoveEffect(indicator);
        RemoveEffect(handCharge);

        if (!health.IsDead && throwBarrageProjectilePrefab != null)
        {
            Vector3 origin = throwOrigin != null ? throwOrigin.position : transform.position + Vector3.up * 1.6f;
            SpawnScaledEffect(throwBarrageReleasePrefab, origin, 1f);
            // 도끼를 치켜든 높이(약 2.7m)에서 수평으로 쏘면 플레이어 머리 위로 지나가므로, 플레이어 몸 중심을 향해 아래로 겨눈다.
            Vector3 aimPoint = TargetBodyCenter();
            float horizontalDist = Mathf.Max(2f, Vector3.ProjectOnPlane(aimPoint - origin, Vector3.up).magnitude);
            float drop = origin.y - aimPoint.y;
            foreach (float angle in angles)
            {
                Vector3 dir = (Quaternion.Euler(0f, angle, 0f) * baseDir * horizontalDist + Vector3.down * drop).normalized;
                EnemyProjectile projectile = Instantiate(throwBarrageProjectilePrefab, origin, Quaternion.LookRotation(dir));
                projectile.transform.localScale *= throwBarrageProjectileScale;
                projectile.Launch(dir, throwBarrageProjectileSpeed, throwBarrageDamage);
            }
        }

        yield return new WaitForSeconds(throwBarrageRecovery);
        EndBusy(0.4f);
    }

    // ---------------- 졸개 소환 ----------------

    private IEnumerator SummonRoutine()
    {
        state = State.Busy;
        if (animator != null) animator.SetTrigger(SummonHash);
        health.IncomingDamageMultiplier = 0f; // 포효 중 무적
        GameObject roar = AttachEffect(summonRoarPrefab, transform);

        Vector3 retreatDir = target != null ? transform.position - target.position : -transform.forward;
        retreatDir.y = 0f;
        retreatDir = retreatDir.sqrMagnitude > 0.0001f ? retreatDir.normalized : -transform.forward;

        agent.isStopped = false;
        if (NavMesh.SamplePosition(transform.position + retreatDir * summonRetreatDistance, out NavMeshHit navHit, summonRetreatDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);
        }

        yield return new WaitForSeconds(summonTelegraphDuration);

        pendingSummonThreshold = -1; // 포효를 끝까지 마쳤으니 이 구간 소환은 확정
        agent.isStopped = true;
        health.IncomingDamageMultiplier = 1f;
        RemoveEffect(roar);

        if (!health.IsDead && minionPrefab != null)
        {
            int count = Random.Range(summonMinionCountMin, summonMinionCountMax + 1);
            for (int i = 0; i < count; i++)
            {
                Vector2 offset2d = Random.insideUnitCircle * summonSpawnRadius;
                Vector3 spawnPos = transform.position + new Vector3(offset2d.x, 0f, offset2d.y);
                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit spawnHit, summonSpawnRadius, NavMesh.AllAreas))
                {
                    Instantiate(minionPrefab, spawnHit.position, Quaternion.identity);
                    SpawnScaledEffect(summonSpawnPrefab, spawnHit.position, 1f);
                }
            }
        }

        yield return new WaitForSeconds(summonRecovery);
        EndBusy(0.5f);
    }

    // ---------------- 분노 연계 (돌진 베기 → 광역 강타 즉시 연속) ----------------

    private IEnumerator RageComboRoutine()
    {
        nextRageComboTime = Time.time + rageComboCooldown;
        SpawnScaledEffect(rageBurstPrefab, transform.position, 1f);
        GameObject aura = AttachEffect(rageAuraPrefab, transform);

        yield return StartCoroutine(ChargeSlashRoutine());
        if (health.IsDead) yield break;
        yield return StartCoroutine(AoeSlamRoutine());
        RemoveEffect(aura);
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
        GameObject indicator = new GameObject("BossTelegraphRing");
        indicator.transform.position = center + Vector3.up * 0.05f;

        const int segments = 32;
        LineRenderer ring = indicator.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = segments;
        ring.startWidth = ring.endWidth = 0.1f;
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
        GameObject indicator = new GameObject("BossTelegraphLine");
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

    private GameObject CreateLaneTelegraph(LaneTelegraphVisual prefab, Vector3 direction, float length, float width, float duration,
        Color fallbackColor, float fallbackWidth = -1f)
    {
        if (prefab == null)
        {
            return TrackEffect(CreateLineIndicator(direction, length, fallbackWidth > 0f ? fallbackWidth : width, fallbackColor));
        }

        LaneTelegraphVisual lane = Instantiate(prefab, transform.position + Vector3.up * 0.05f, Quaternion.LookRotation(direction));
        lane.Begin(length, width, duration);
        return TrackEffect(lane.gameObject);
    }

    // ---------------- 이펙트 관리 ----------------

    private GameObject AttachEffect(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null;
        return TrackEffect(Instantiate(prefab, parent.position, parent.rotation, parent));
    }

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

    // 빙결되면 진행 중인 패턴(분노 연계 포함)을 즉시 끊는다. 예고/이펙트를 걷어내고,
    // 포효(졸개 소환) 중이었다면 무적과 후퇴 이동을 풀고, 녹은 뒤 소환을 처음부터 다시 하게 한다.
    private void InterruptForFreeze()
    {
        interruptedByFreeze = true;
        StopAllCoroutines();
        SetPhasing(false);
        ClearPatternEffects();
        if (weaponHitbox != null) weaponHitbox.Deactivate();
        health.IncomingDamageMultiplier = 1f;
        if (agent.isOnNavMesh) agent.ResetPath();
        RestorePendingSummonThreshold(); // 소환 포효 중이었다면 녹은 뒤 다시 소환

        if (animator != null)
        {
            animator.SetBool(ChargeSlashingHash, false);
            animator.SetFloat(ChargeMotionSpeedHash, 1f);
        }
        keepDistanceTimer = 0f;
        state = State.Idle;
    }

    private void ResumeAfterFreeze()
    {
        interruptedByFreeze = false;
        EnemyFreezeInterrupt.ReturnToIdle(animator, AttackHash, AoeSlamHash, ThrowBarrageHash, SummonHash);
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
        attackDamage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * multiplier));
        chargeSlashDamage = Mathf.Max(1, Mathf.RoundToInt(chargeSlashDamage * multiplier));
        aoeSlamDamage = Mathf.Max(1, Mathf.RoundToInt(aoeSlamDamage * multiplier));
        throwBarrageDamage = Mathf.Max(1, Mathf.RoundToInt(throwBarrageDamage * multiplier));
    }

    public void SetDetectRange(float range)
    {
        detectRange = range;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(transform.position, chargeSlashTriggerRange);
        Gizmos.color = new Color(1f, 0.7f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, throwBarrageRange);
    }
}
