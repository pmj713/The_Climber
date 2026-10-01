using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 고블린 대장(보스). NC AI-기획 요약 페이지의 "패턴 목록" 표를 기준으로 구현.
// 체력 구간(75/50/25%)에 따라 패턴이 하나씩 풀리고, 각 구간 최초 진입 시 졸개를 소환하며,
// 25% 이하에서는 돌진 베기 -> 광역 강타를 예고 없이 즉시 연계하는 분노 연계를 쓴다.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class GoblinChiefBossAI : MonoBehaviour, IEnemyEmpowerable
{
    private enum State { Idle, Chase, Busy }

    [Header("기본 이동/추적")]
    [SerializeField] private float detectRange = 14f;
    [SerializeField] private float moveSpeed = 3.2f;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Animator animator;

    [Header("행동 선택 (패턴 사이 랜덤 대기)")]
    [SerializeField] private float actionIntervalMin = 2f;
    [SerializeField] private float actionIntervalMax = 3.5f;

    [Header("돌진 베기 - 상시, 0.6초 예고, 직선 큰 피해+넉백")]
    [SerializeField] private float dashTelegraphDuration = 0.6f;
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashMaxDistance = 9f;
    [SerializeField] private float dashHitRadius = 1.2f;
    [SerializeField] private int dashDamage = 20;
    [SerializeField] private float dashKnockbackForce = 12f;
    [SerializeField] private float dashCooldown = 6f;

    [Header("광역 강타 - 75% 이하부터, 도약 후 착지 지점 예고(0.8초), 원형 큰 피해+경직")]
    [SerializeField, Range(0f, 1f)] private float slamUnlockHealthFraction = 0.75f;
    [SerializeField] private float slamJumpDuration = 0.8f;
    [SerializeField] private float slamJumpHeight = 3f;
    [SerializeField] private float slamRadius = 3f;
    [SerializeField] private int slamDamage = 26;
    [SerializeField] private float slamPlayerStunDuration = 0.8f;
    [SerializeField] private float slamCooldown = 8f;

    [Header("투척 난사 - 50% 이하부터, 조준선 부채꼴 3방향 예고, 투사체 3발")]
    [SerializeField, Range(0f, 1f)] private float barrageUnlockHealthFraction = 0.5f;
    [SerializeField] private float barrageTelegraphDuration = 0.5f;
    [SerializeField] private EnemyProjectile barrageProjectilePrefab;
    [SerializeField] private float barrageProjectileSpeed = 12f;
    [SerializeField] private int barrageDamage = 12;
    [SerializeField] private float barrageSpreadAngle = 50f;
    [SerializeField] private float barrageCooldown = 7f;

    [Header("졸개 소환 - 75/50/25% 최초 진입 시 1회씩, 포효+무적, 소환 중 후퇴")]
    [SerializeField] private float[] summonThresholds = { 0.75f, 0.5f, 0.25f };
    [SerializeField] private float summonRoarDuration = 1.2f;
    [SerializeField] private float summonRetreatDistance = 4f;
    [SerializeField] private GameObject minionPrefab;
    [SerializeField] private int minionSpawnCountMin = 1;
    [SerializeField] private int minionSpawnCountMax = 2;

    [Header("분노 연계 - 25% 이하 전용, 예고 없이 돌진 베기 -> 광역 강타 연속")]
    [SerializeField, Range(0f, 1f)] private float rageHealthThreshold = 0.25f;
    [SerializeField] private float rageComboCooldown = 5f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int RoarHash = Animator.StringToHash("Roar");

    private NavMeshAgent agent;
    private EnemyHealth health;
    private EnemyStatusEffects status;
    private Transform target;
    private State state;
    private float nextActionTime;
    private float nextDashTime;
    private float nextSlamTime;
    private float nextBarrageTime;
    private float nextRageComboTime;
    private bool[] summonUsed;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        status = GetComponent<EnemyStatusEffects>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        agent.speed = moveSpeed;
        agent.updateRotation = false;
        health.SetStunResistant(true);
        summonUsed = new bool[summonThresholds.Length];
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
            agent.isStopped = true;
            return;
        }

        if (target == null) FindTarget();

        if (state != State.Busy) CheckSummonThresholds();

        if (state != State.Busy)
        {
            switch (state)
            {
                case State.Idle: TickIdle(); break;
                case State.Chase: TickChase(); break;
            }
        }

        if (animator != null)
        {
            animator.SetBool(IsMovingHash, state == State.Chase && agent.velocity.sqrMagnitude > 0.01f);
        }

        if (state == State.Chase) FaceTarget();
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

        if (Time.time < nextActionTime) return;

        if (health.HealthFraction <= rageHealthThreshold && Time.time >= nextRageComboTime)
        {
            StartCoroutine(RageComboRoutine());
            return;
        }

        List<int> options = new List<int>();
        if (Time.time >= nextDashTime) options.Add(0);
        if (health.HealthFraction <= slamUnlockHealthFraction && Time.time >= nextSlamTime) options.Add(1);
        if (health.HealthFraction <= barrageUnlockHealthFraction && Time.time >= nextBarrageTime) options.Add(2);

        if (options.Count == 0) return;

        switch (options[Random.Range(0, options.Count)])
        {
            case 0: StartCoroutine(DashSlashRoutine()); break;
            case 1: StartCoroutine(AoeSlamRoutine()); break;
            case 2: StartCoroutine(ThrowBarrageRoutine()); break;
        }
    }

    private void CheckSummonThresholds()
    {
        float hpFraction = health.HealthFraction;
        for (int i = 0; i < summonThresholds.Length; i++)
        {
            if (!summonUsed[i] && hpFraction <= summonThresholds[i])
            {
                summonUsed[i] = true;
                StartCoroutine(SummonRoutine());
                return;
            }
        }
    }

    private void EndBusy()
    {
        if (health.IsDead) return;

        agent.isStopped = false;
        nextActionTime = Time.time + Random.Range(actionIntervalMin, actionIntervalMax);
        state = (target != null && DistanceToTarget() <= detectRange) ? State.Chase : State.Idle;
    }

    // ---------------- 돌진 베기 ----------------

    private IEnumerator DashSlashRoutine()
    {
        state = State.Busy;
        nextDashTime = Time.time + dashCooldown;
        yield return DashSlashCore();
        EndBusy();
    }

    private IEnumerator DashSlashCore()
    {
        agent.isStopped = true;

        Vector3 direction = target != null ? target.position - transform.position : transform.forward;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(direction);

        GameObject indicator = CreateLineIndicator(direction, dashMaxDistance, dashHitRadius * 2f, new Color(1f, 0.2f, 0.1f, 0.5f));
        yield return new WaitForSeconds(dashTelegraphDuration);
        Destroy(indicator);

        if (health.IsDead) yield break;

        if (animator != null) animator.SetTrigger(AttackHash);

        var alreadyHit = new HashSet<IDamageable>();
        float traveled = 0f;
        while (traveled < dashMaxDistance)
        {
            float step = dashSpeed * Time.deltaTime;

            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction, step + 0.3f, obstacleMask)) break;

            agent.Move(direction * step);
            traveled += step;

            Collider[] hits = Physics.OverlapSphere(transform.position, dashHitRadius, playerMask);
            foreach (Collider hit in hits)
            {
                if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;
                if (!alreadyHit.Add(damageable)) continue;

                damageable.TakeDamage(dashDamage);
                if (hit.TryGetComponent<PlayerMovement>(out var movement))
                {
                    Vector3 dir = hit.transform.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f) movement.ApplyKnockback(dir.normalized * dashKnockbackForce);
                }
            }

            yield return null;
        }
    }

    // ---------------- 광역 강타 ----------------

    private IEnumerator AoeSlamRoutine()
    {
        state = State.Busy;
        nextSlamTime = Time.time + slamCooldown;
        yield return AoeSlamCore();
        EndBusy();
    }

    private IEnumerator AoeSlamCore()
    {
        agent.isStopped = true;
        Vector3 landingPoint = target != null ? target.position : transform.position;
        Vector3 startPos = transform.position;

        GameObject indicator = CreateRingIndicator(landingPoint, slamRadius, new Color(1f, 0.2f, 0.1f, 0.85f));
        if (animator != null) animator.SetTrigger(AttackHash);

        float elapsed = 0f;
        while (elapsed < slamJumpDuration)
        {
            float t = elapsed / slamJumpDuration;
            Vector3 horizontal = Vector3.Lerp(startPos, landingPoint, t);
            float height = Mathf.Sin(t * Mathf.PI) * slamJumpHeight;
            transform.position = new Vector3(horizontal.x, startPos.y + height, horizontal.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = landingPoint;
        agent.Warp(transform.position); // 직접 옮긴 위치로 NavMeshAgent 내부 상태 동기화
        Destroy(indicator);

        if (!health.IsDead)
        {
            Collider[] hits = Physics.OverlapSphere(landingPoint, slamRadius, playerMask);
            foreach (Collider hit in hits)
            {
                if (hit.TryGetComponent<IDamageable>(out var damageable)) damageable.TakeDamage(slamDamage);
                if (hit.TryGetComponent<PlayerMovement>(out var movement)) movement.ApplyStun(slamPlayerStunDuration);
            }
        }
    }

    // ---------------- 투척 난사 ----------------

    private IEnumerator ThrowBarrageRoutine()
    {
        state = State.Busy;
        nextBarrageTime = Time.time + barrageCooldown;
        yield return ThrowBarrageCore();
        EndBusy();
    }

    private IEnumerator ThrowBarrageCore()
    {
        agent.isStopped = true;

        Vector3 direction = target != null ? target.position - transform.position : transform.forward;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(direction);

        List<GameObject> aimLines = CreateFanAimIndicators(direction);
        yield return new WaitForSeconds(barrageTelegraphDuration);
        foreach (GameObject line in aimLines) Destroy(line);

        if (health.IsDead) yield break;

        if (animator != null) animator.SetTrigger(AttackHash);

        const int shotCount = 3;
        for (int i = 0; i < shotCount; i++)
        {
            float angle = shotCount <= 1 ? 0f : -barrageSpreadAngle / 2f + (barrageSpreadAngle / (shotCount - 1)) * i;
            Vector3 shotDirection = Quaternion.Euler(0f, angle, 0f) * direction;
            FireProjectile(shotDirection);
        }
    }

    private void FireProjectile(Vector3 direction)
    {
        if (barrageProjectilePrefab == null) return;

        Vector3 origin = transform.position + Vector3.up * 1.2f;
        EnemyProjectile projectile = Instantiate(barrageProjectilePrefab, origin, Quaternion.LookRotation(direction));
        projectile.Launch(direction, barrageProjectileSpeed, barrageDamage);
    }

    // ---------------- 졸개 소환 ----------------

    private IEnumerator SummonRoutine()
    {
        state = State.Busy;
        agent.isStopped = true;

        float previousMultiplier = health.IncomingDamageMultiplier;
        health.IncomingDamageMultiplier = 0f; // 무적
        if (animator != null) animator.SetTrigger(RoarHash);

        Vector3 retreatDir = target != null ? transform.position - target.position : -transform.forward;
        retreatDir.y = 0f;
        retreatDir = retreatDir.sqrMagnitude > 0.0001f ? retreatDir.normalized : -transform.forward;
        Vector3 retreatDestination = transform.position + retreatDir * summonRetreatDistance;

        if (NavMesh.SamplePosition(retreatDestination, out NavMeshHit navHit, summonRetreatDistance, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(navHit.position);
        }

        yield return new WaitForSeconds(summonRoarDuration);

        if (!health.IsDead && minionPrefab != null)
        {
            int count = Random.Range(minionSpawnCountMin, minionSpawnCountMax + 1);
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 2.5f;
                Vector3 spawnPos = transform.position + new Vector3(offset.x, 0f, offset.y);
                Instantiate(minionPrefab, spawnPos, Quaternion.identity);
            }
        }

        health.IncomingDamageMultiplier = previousMultiplier;
        EndBusy();
    }

    // ---------------- 분노 연계 (돌진 베기 -> 광역 강타, 예고 대기 없이 연속) ----------------

    private IEnumerator RageComboRoutine()
    {
        state = State.Busy;
        nextRageComboTime = Time.time + rageComboCooldown;
        nextDashTime = Time.time + dashCooldown;
        nextSlamTime = Time.time + slamCooldown;

        yield return DashSlashCore();
        if (!health.IsDead) yield return AoeSlamCore();

        EndBusy();
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

    private List<GameObject> CreateFanAimIndicators(Vector3 direction)
    {
        var lines = new List<GameObject>();
        const int shotCount = 3;
        for (int i = 0; i < shotCount; i++)
        {
            float angle = -barrageSpreadAngle / 2f + (barrageSpreadAngle / (shotCount - 1)) * i;
            Vector3 shotDirection = Quaternion.Euler(0f, angle, 0f) * direction;
            lines.Add(CreateLineIndicator(shotDirection, 8f, 0.05f, new Color(0.4f, 0.9f, 0.3f, 0.8f)));
        }
        return lines;
    }

    private void HandleDeath()
    {
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
        dashDamage = Mathf.Max(1, Mathf.RoundToInt(dashDamage * multiplier));
        slamDamage = Mathf.Max(1, Mathf.RoundToInt(slamDamage * multiplier));
        barrageDamage = Mathf.Max(1, Mathf.RoundToInt(barrageDamage * multiplier));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, dashMaxDistance);
        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
