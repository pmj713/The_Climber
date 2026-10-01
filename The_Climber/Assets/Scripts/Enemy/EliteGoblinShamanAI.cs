using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 엘리트 원거리형(고블린 주술사). 일반 원거리 몬스터와 달리 경직 저항을 갖고, 연속 사격/장판 설치/
// 후퇴 텔레포트 3가지 고유 패턴을 쓴다. NC AI-기획 요약 페이지의
// "엘리트 몬스터 패턴 (4층)" 표를 기준으로 구현.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EliteGoblinShamanAI : MonoBehaviour, IEnemyEmpowerable
{
    private enum State { Idle, Chase, AttackPrepare, Busy, Cooldown }

    [Header("기본 이동/추적")]
    [SerializeField] private float detectRange = 10f;
    [SerializeField] private float attackRange = 7f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private Animator animator;

    [Header("연속 사격 - 상시, 조준선 0.9초 예고 후 3발 연속 발사")]
    [SerializeField] private EnemyProjectile projectilePrefab;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private int damage = 6;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float burstTelegraphDuration = 0.9f;
    [SerializeField] private int burstShotCount = 3;
    [SerializeField] private float burstShotInterval = 0.15f;
    [SerializeField] private float burstCooldown = 1.6f;
    [SerializeField] private GameObject burstChargeEffectPrefab; // 조준 예고 동안 지팡이 끝에 모이는 기운
    [SerializeField] private GameObject burstMuzzleFlashPrefab;  // 한 발 쏠 때마다 지팡이 끝 섬광

    [Header("장판 설치 - 플레이어가 5m 이내로 접근 시, 발밑에 1.5초 지연 폭발")]
    [SerializeField] private float groundZoneTriggerRange = 5f;
    [SerializeField] private float groundZoneFuseDuration = 1.5f;
    [SerializeField] private float groundZoneRadius = 2.5f;
    [SerializeField] private int groundZoneDamage = 16;
    [SerializeField] private float groundZoneSlowMultiplier = 0.5f;
    [SerializeField] private float groundZoneSlowDuration = 2.5f;
    [SerializeField] private float groundZoneCooldown = 6f;
    [SerializeField] private ZoneTelegraphVisual groundZoneTelegraphPrefab; // 비워두면 기본 링 예고
    [SerializeField] private GameObject groundZoneExplosionPrefab;        // 반지름 1 기준, groundZoneRadius 배로 키워서 터뜨림
    [SerializeField] private GameObject groundZoneChannelEffectPrefab;    // 퓨즈 동안 지팡이 끝 기운

    [Header("후퇴 텔레포트 - 플레이어가 3m 이내로 근접 시, 0.65초 예고 후 반대 방향으로 순간이동")]
    [SerializeField] private float teleportTriggerRange = 3f;
    [SerializeField] private float teleportSmokeDuration = 0.65f;
    [SerializeField] private float teleportDistance = 7f;
    [SerializeField] private float teleportCooldown = 5f;
    [SerializeField] private GameObject teleportChargeEffectPrefab; // 예고 동안 발밑에서 휘감아 오르는 연기 (비우면 기본 회색 링)
    [SerializeField] private GameObject teleportVanishEffectPrefab; // 사라지는 자리
    [SerializeField] private GameObject teleportAppearEffectPrefab; // 나타나는 자리

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("BowAttack");
    private static readonly int GroundZoneHash = Animator.StringToHash("GroundZoneCast");
    private static readonly int RetreatingHash = Animator.StringToHash("Retreating");

    private NavMeshAgent agent;
    private EnemyHealth health;
    private EnemyStatusEffects status;
    private Transform target;
    private State state;
    private float stateTimer;
    private float nextGroundZoneTime;
    private float nextTeleportTime;
    private GameObject aimIndicator;
    private GameObject burstChargeEffect;
    private GameObject groundZoneTelegraph;
    private GameObject teleportCharge;
    private bool interruptedByFreeze;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        status = GetComponent<EnemyStatusEffects>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        agent.speed = moveSpeed;
        agent.updateRotation = false;
        agent.stoppingDistance = attackRange * 0.9f;
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

        if (health.IsStunned)
        {
            agent.isStopped = true;
            return;
        }

        if (target == null) FindTarget();

        // 근접 반응형 패턴(텔레포트/장판)은 공격 준비/쿨타임과 무관하게 가장 먼저 검사한다.
        if (target != null && state != State.Busy)
        {
            float dist = DistanceToTarget();

            if (dist <= teleportTriggerRange && Time.time >= nextTeleportTime)
            {
                StartCoroutine(RetreatTeleportRoutine());
                return;
            }

            if (dist <= groundZoneTriggerRange && Time.time >= nextGroundZoneTime)
            {
                StartCoroutine(GroundZoneRoutine());
                return;
            }
        }

        switch (state)
        {
            case State.Idle: TickIdle(); break;
            case State.Chase: TickChase(); break;
            case State.AttackPrepare: TickAttackPrepare(); break;
            case State.Cooldown: TickCooldown(); break;
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

        if (DistanceToTarget() <= attackRange)
        {
            agent.isStopped = true;
            state = State.AttackPrepare;
            stateTimer = burstTelegraphDuration;
            aimIndicator = CreateAimLineIndicator();
            if (burstChargeEffectPrefab != null && firePoint != null)
                burstChargeEffect = Instantiate(burstChargeEffectPrefab, firePoint.position, firePoint.rotation, firePoint);
        }
    }

    private void TickAttackPrepare()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            ClearBurstTelegraph();
            StartCoroutine(BurstShotRoutine());
        }
    }

    // 조준 예고(조준선 + 지팡이 끝 기운)를 지운다. 예고 도중 텔레포트/장판으로 넘어가도 남지 않도록 그쪽에서도 호출.
    private void ClearBurstTelegraph()
    {
        if (aimIndicator != null) { Destroy(aimIndicator); aimIndicator = null; }
        if (burstChargeEffect != null) { Destroy(burstChargeEffect); burstChargeEffect = null; }
    }

    // 퓨즈 도중 주술사가 죽으면 코루틴이 끊기므로 장판 예고가 바닥에 남지 않게 여기서도 지운다.
    private void ClearGroundZoneTelegraph()
    {
        if (groundZoneTelegraph != null) { Destroy(groundZoneTelegraph); groundZoneTelegraph = null; }
    }

    private void ClearTeleportCharge()
    {
        if (teleportCharge != null) { Destroy(teleportCharge); teleportCharge = null; }
    }

    private static void SpawnOneShotEffect(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;
        Destroy(Instantiate(prefab, position, Quaternion.identity), 3f);
    }

    private void TickCooldown()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            state = (target != null && DistanceToTarget() <= detectRange) ? State.Chase : State.Idle;
        }
    }

    // ---------------- 연속 사격 ----------------

    private IEnumerator BurstShotRoutine()
    {
        state = State.Busy;
        if (animator != null) animator.SetTrigger(AttackHash);

        for (int i = 0; i < burstShotCount; i++)
        {
            if (health.IsDead) break;
            FireProjectile();
            if (i < burstShotCount - 1) yield return new WaitForSeconds(burstShotInterval);
        }

        EndBusy(burstCooldown);
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null || target == null) return;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up;
        Vector3 direction = target.position - origin;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        EnemyProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
        projectile.Launch(direction, projectileSpeed, damage);

        if (burstMuzzleFlashPrefab != null) Destroy(Instantiate(burstMuzzleFlashPrefab, origin, Quaternion.LookRotation(direction)), 1f);
    }

    // ---------------- 장판 설치 ----------------

    private IEnumerator GroundZoneRoutine()
    {
        ClearBurstTelegraph();
        state = State.Busy;
        agent.isStopped = true;
        nextGroundZoneTime = Time.time + groundZoneCooldown;
        if (animator != null) animator.SetTrigger(GroundZoneHash);

        Vector3 zoneCenter = target != null ? target.position : transform.position;
        // 플레이어 피벗은 몸 중앙이라 그대로 쓰면 장판이 허리 높이에 뜬다 -> 바닥으로 내린다
        zoneCenter.y = NavMesh.SamplePosition(zoneCenter, out NavMeshHit groundHit, 2f, NavMesh.AllAreas)
            ? groundHit.position.y
            : transform.position.y;

        if (groundZoneTelegraphPrefab != null)
        {
            ZoneTelegraphVisual telegraph = Instantiate(groundZoneTelegraphPrefab, zoneCenter + Vector3.up * 0.05f, Quaternion.identity);
            telegraph.Begin(groundZoneRadius, groundZoneFuseDuration);
            groundZoneTelegraph = telegraph.gameObject;
        }
        else
        {
            groundZoneTelegraph = CreateRingIndicator(zoneCenter, groundZoneRadius, new Color(1f, 0.15f, 0.15f, 0.85f));
        }

        GameObject channel = groundZoneChannelEffectPrefab != null && firePoint != null
            ? Instantiate(groundZoneChannelEffectPrefab, firePoint.position, firePoint.rotation, firePoint)
            : null;

        yield return new WaitForSeconds(groundZoneFuseDuration);
        ClearGroundZoneTelegraph();
        if (channel != null) Destroy(channel);

        if (!health.IsDead)
        {
            if (groundZoneExplosionPrefab != null)
            {
                GameObject explosion = Instantiate(groundZoneExplosionPrefab, zoneCenter, Quaternion.identity);
                explosion.transform.localScale = Vector3.one * groundZoneRadius;
                Destroy(explosion, 3f);
            }

            Collider[] hits = Physics.OverlapSphere(zoneCenter, groundZoneRadius, playerMask);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent<IDamageable>(out var damageable)) damageable.TakeDamage(groundZoneDamage);
                if (hit.TryGetComponent<PlayerMovement>(out var movement))
                {
                    movement.ApplyTemporarySpeedBoost(groundZoneSlowMultiplier, groundZoneSlowDuration);
                }
            }
        }

        EndBusy(0f);
    }

    // ---------------- 후퇴 텔레포트 ----------------

    private IEnumerator RetreatTeleportRoutine()
    {
        ClearBurstTelegraph();
        state = State.Busy;
        agent.isStopped = true;
        nextTeleportTime = Time.time + teleportCooldown;
        if (animator != null) animator.SetBool(RetreatingHash, true);

        teleportCharge = teleportChargeEffectPrefab != null
            ? Instantiate(teleportChargeEffectPrefab, transform.position, Quaternion.identity)
            : CreateRingIndicator(transform.position, 1f, new Color(0.7f, 0.7f, 0.7f, 0.6f));
        yield return new WaitForSeconds(teleportSmokeDuration);
        ClearTeleportCharge();

        if (!health.IsDead && target != null)
        {
            Vector3 away = transform.position - target.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
            Vector3 destination = transform.position + away * teleportDistance;

            if (NavMesh.SamplePosition(destination, out NavMeshHit navHit, teleportDistance, NavMesh.AllAreas))
            {
                SpawnOneShotEffect(teleportVanishEffectPrefab, transform.position);
                agent.Warp(navHit.position);
                SpawnOneShotEffect(teleportAppearEffectPrefab, navHit.position);
            }
        }

        if (animator != null) animator.SetBool(RetreatingHash, false);
        EndBusy(0f);
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

    private GameObject CreateAimLineIndicator()
    {
        if (target == null) return null;

        GameObject indicator = new GameObject("EliteAimTelegraph");
        LineRenderer line = indicator.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        Color color = new Color(0.4f, 0.9f, 0.3f, 0.8f);
        line.startColor = line.endColor = color;
        line.material.SetColor("_BaseColor", color);

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up;
        line.SetPosition(0, origin);
        line.SetPosition(1, target.position);

        return indicator;
    }

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

    // 빙결되면 연속사격/장판/텔레포트를 즉시 끊고 예고를 걷어낸다 (장판은 터지지 않는다).
    private void InterruptForFreeze()
    {
        interruptedByFreeze = true;
        StopAllCoroutines();
        ClearBurstTelegraph();
        ClearGroundZoneTelegraph();
        ClearTeleportCharge();
        if (animator != null) animator.SetBool(RetreatingHash, false);
        state = State.Idle;
    }

    private void ResumeAfterFreeze()
    {
        interruptedByFreeze = false;
        EnemyFreezeInterrupt.ReturnToIdle(animator, AttackHash, GroundZoneHash);
    }

    private void HandleDeath()
    {
        ClearBurstTelegraph();
        ClearGroundZoneTelegraph();
        ClearTeleportCharge();
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
        groundZoneDamage = Mathf.Max(1, Mathf.RoundToInt(groundZoneDamage * multiplier));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.15f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, groundZoneTriggerRange);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, teleportTriggerRange);
    }
}
