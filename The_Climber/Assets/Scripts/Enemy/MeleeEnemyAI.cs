using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class MeleeEnemyAI : MonoBehaviour, IEnemyEmpowerable, IEnemyDetectionConfigurable, IEnemyPhasing
{
    // 상태: Idle -> Chase -> Attack Prepare(예고) -> Attack(돌진) -> Cooldown -> Chase
    // 공격은 돌진 준비자세(예고) 다음에 하는 돌진 자체다. 돌진하는 동안 몬스터 몸이 플레이어와 부딪히면 데미지가 들어간다.
    private enum State { Idle, Chase, AttackPrepare, Attack, Cooldown }

    [SerializeField] private float detectRange = 8f;
    [Tooltip("플레이어가 이 거리 안으로 들어오면 돌진 준비자세를 취한다")]
    [SerializeField] private float lungeTriggerRange = 3.5f;
    [Tooltip("돌진 거리(m). 플레이어가 더 가까워도 이 거리만큼 끝까지 돌진한다(플레이어를 뚫고 지나감)")]
    [SerializeField] private float lungeDistance = 3f;
    [Tooltip("돌진에 걸리는 시간(초)")]
    [SerializeField] private float lungeDuration = 0.25f;
    [Tooltip("돌진 중 플레이어와 부딪혔다고 보는 거리(m). 몬스터와 플레이어의 몸 반지름을 합친 정도")]
    [SerializeField] private float contactRadius = 1f;
    [SerializeField] private float moveSpeed = 3.5f;
    [Tooltip("돌진 준비자세를 유지하는 시간(초)")]
    [SerializeField] private float attackPrepareTime = 0.5f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int damage = 8;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private Animator animator;
    [Tooltip("준비자세에 들어갈 때 번쩍이는 예고 이펙트를 붙일 위치 (선택, 보통 무기)")]
    [SerializeField] private EnemyMeleeWeaponHitbox weaponHitbox;
    [Tooltip("준비자세에 들어갈 때 무기에서 번쩍이는 예고 이펙트 (선택)")]
    [SerializeField] private GameObject attackWarningPrefab;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int WindupHash = Animator.StringToHash("Windup");

    private NavMeshAgent agent;
    private EnemyHealth health;
    private EnemyStatusEffects status;
    private Transform target;
    private State state;
    private float stateTimer;
    private bool interruptedByFreeze;
    private Vector3 lungeDirection;
    private float lungeRemaining;
    private bool lungeHitPlayer;
    private Collider[] ownColliders;
    private LineRenderer pathLine;
    private static Material pathMaterial;

    public bool IsPhasingThroughPlayer => state == State.Attack;

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
        state = State.Idle;
    }

    private void OnEnable()
    {
        health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        health.OnDeath -= HandleDeath;
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
            if (state == State.Attack) EndLunge(); // 돌진 중에 맞아서 경직되면 돌진이 끊긴다
            return;
        }

        agent.speed = moveSpeed * (status != null ? status.MoveSpeedMultiplier : 1f);

        if (target == null)
        {
            FindTarget();
        }

        switch (state)
        {
            case State.Idle:
                TickIdle();
                break;
            case State.Chase:
                TickChase();
                break;
            case State.AttackPrepare:
                TickAttackPrepare();
                break;
            case State.Attack:
                TickAttack();
                break;
            case State.Cooldown:
                TickCooldown();
                break;
        }

        UpdatePathIndicator();

        if (animator != null) animator.SetBool(IsMovingHash, state == State.Chase && agent.velocity.sqrMagnitude > 0.01f);

        Vector3 facing = Vector3.zero;
        if (state == State.Chase)
            facing = agent.velocity.sqrMagnitude > 0.01f ? agent.velocity : agent.desiredVelocity;
        else if (state == State.AttackPrepare && target != null)
            facing = target.position - transform.position;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(facing);
    }

    private void FindTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange, playerMask);
        if (hits.Length > 0)
        {
            target = hits[0].transform;
        }
    }

    private float DistanceToTarget()
    {
        return target == null ? Mathf.Infinity : Vector3.Distance(transform.position, target.position);
    }

    private void TickIdle()
    {
        agent.isStopped = true;
        if (target != null && DistanceToTarget() <= detectRange)
        {
            state = State.Chase;
        }
    }

    private void TickChase()
    {
        if (target == null)
        {
            state = State.Idle;
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(target.position);

        if (DistanceToTarget() <= lungeTriggerRange)
        {
            EnterAttackPrepare();
        }
    }

    private void EnterAttackPrepare()
    {
        state = State.AttackPrepare;
        stateTimer = attackPrepareTime;
        agent.isStopped = true;
        if (animator != null) animator.SetTrigger(WindupHash); // 무기를 머리 위로 들어올리는 준비 동작

        if (attackWarningPrefab != null && weaponHitbox != null)
        {
            Transform weapon = weaponHitbox.transform;
            Destroy(Instantiate(attackWarningPrefab, weapon.position, weapon.rotation, weapon), 1f);
        }
    }

    private void TickAttackPrepare()
    {
        stateTimer -= Time.deltaTime * (status != null ? status.MoveSpeedMultiplier : 1f);
        if (stateTimer <= 0f)
        {
            BeginLunge();
        }
    }

    // 예고가 끝난 이 순간의 플레이어 방향으로 방향을 고정한다 (그 뒤에 움직여서 피할 수 있다)
    private void BeginLunge()
    {
        Vector3 toTarget = target != null ? target.position - transform.position : transform.forward;
        toTarget.y = 0f;
        lungeDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
        lungeRemaining = lungeDistance;
        lungeHitPlayer = false;

        SetPlayerCollisionIgnored(true);
        state = State.Attack;
        if (animator != null) animator.SetTrigger(AttackHash);
    }

    // 돌진하는 동안 매 프레임 몸통이 플레이어와 겹쳤는지 보고, 돌진 한 번에 한 번만 데미지를 준다
    private void TickAttack()
    {
        float speedFactor = status != null ? status.MoveSpeedMultiplier : 1f;
        float step = Mathf.Min(lungeDistance / lungeDuration * Time.deltaTime * speedFactor, lungeRemaining);
        agent.Move(lungeDirection * step);
        lungeRemaining -= step;

        if (!lungeHitPlayer) TryHitPlayerByContact();

        if (lungeRemaining <= 0.001f) EndLunge();
    }

    private void TryHitPlayerByContact()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.9f, contactRadius, playerMask, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;
            damageable.TakeDamage(damage);
            lungeHitPlayer = true;
            return;
        }
    }

    private void EndLunge()
    {
        SetPlayerCollisionIgnored(false);
        state = State.Cooldown;
        stateTimer = attackCooldown;
    }

    // 돌진하는 동안은 플레이어와 서로 밀지 않고 통과한다. 플레이어가 회피 중이면 회피 쪽에서 계속 통과를 유지한다.
    private void SetPlayerCollisionIgnored(bool ignore)
    {
        EnemyPlayerPhasing.Apply(ownColliders, ignore);
    }

    // 예고 중에만 바닥에 돌진 경로(실제 피격 폭)를 붉게 보여준다
    private void UpdatePathIndicator()
    {
        if (state != State.AttackPrepare || target == null)
        {
            if (pathLine != null && pathLine.enabled) pathLine.enabled = false;
            return;
        }

        if (pathLine == null) CreatePathLine();
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        Vector3 start = transform.position + Vector3.up * 0.06f;
        pathLine.SetPosition(0, start);
        pathLine.SetPosition(1, start + dir.normalized * lungeDistance);
        pathLine.enabled = true;
    }

    private void CreatePathLine()
    {
        if (pathMaterial == null) pathMaterial = new Material(Shader.Find("Sprites/Default"));

        var go = new GameObject("LungePath");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        pathLine = go.AddComponent<LineRenderer>();
        pathLine.sharedMaterial = pathMaterial;
        pathLine.useWorldSpace = true;
        pathLine.positionCount = 2;
        pathLine.widthMultiplier = contactRadius * 2f;
        pathLine.alignment = LineAlignment.TransformZ;
        pathLine.numCapVertices = 0;
        pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pathLine.receiveShadows = false;
        pathLine.startColor = pathLine.endColor = new Color(1f, 0.1f, 0.1f, 0.45f);
        pathLine.enabled = false;
    }

    private void TickCooldown()
    {
        stateTimer -= Time.deltaTime * (status != null ? status.MoveSpeedMultiplier : 1f);
        if (stateTimer <= 0f)
        {
            state = (target != null && DistanceToTarget() <= detectRange) ? State.Chase : State.Idle;
        }
    }

    // 빙결되면 준비자세/돌진을 취소한다.
    private void InterruptForFreeze()
    {
        interruptedByFreeze = true;
        if (state == State.Attack) SetPlayerCollisionIgnored(false);
        state = State.Idle;
        if (pathLine != null) pathLine.enabled = false;
    }

    private void ResumeAfterFreeze()
    {
        interruptedByFreeze = false;
        EnemyFreezeInterrupt.ReturnToIdle(animator, AttackHash, WindupHash);
    }

    private void HandleDeath()
    {
        if (state == State.Attack) SetPlayerCollisionIgnored(false);
        agent.isStopped = true;
        enabled = false;
        // TODO: 사망 애니메이션/이펙트 재생 후 제거
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
        Gizmos.DrawWireSphere(transform.position, lungeTriggerRange);
    }
}
