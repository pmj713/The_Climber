using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class MeleeEnemyAI : MonoBehaviour
{
    // 기획 문서 상태: Idle -> Detect -> Chase -> Attack Prepare -> Attack -> Cooldown -> Chase
    // Detect는 별도 상태 대신 Idle/Chase 안의 거리 판정으로 처리 (초기 버전은 단순하게)
    private enum State { Idle, Chase, AttackPrepare, Attack, Cooldown }

    [SerializeField] private float detectRange = 8f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float attackPrepareTime = 0.5f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int damage = 8;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyMeleeWeaponHitbox weaponHitbox;
    [Tooltip("공격 애니메이션이 시작된 뒤 무기 판정이 켜져 있는 시간(초)")]
    [SerializeField] private float weaponActiveDuration = 0.3f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private NavMeshAgent agent;
    private EnemyHealth health;
    private Transform target;
    private State state;
    private float stateTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
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
        CancelInvoke(nameof(ActivateWeaponHitbox));
        if (weaponHitbox != null) weaponHitbox.Deactivate();
    }

    private void Update()
    {
        if (health.IsDead) return;

        if (health.IsStunned)
        {
            agent.isStopped = true;
            return;
        }

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

        if (DistanceToTarget() <= attackRange)
        {
            EnterAttackPrepare();
        }
    }

    private void EnterAttackPrepare()
    {
        state = State.AttackPrepare;
        stateTimer = attackPrepareTime;
        agent.isStopped = true;
        // TODO: 공격 예고(경고 표시) 이펙트/애니메이션 트리거
    }

    private void TickAttackPrepare()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            state = State.Attack;
        }
    }

    private void TickAttack()
    {
        if (animator != null) animator.SetTrigger(AttackHash);

        // 실제 데미지는 몽둥이가 플레이어에 닿는 순간 EnemyMeleeWeaponHitbox가 처리한다
        Invoke(nameof(ActivateWeaponHitbox), 0.3f);

        state = State.Cooldown;
        stateTimer = attackCooldown;
    }

    private void ActivateWeaponHitbox()
    {
        if (!health.IsDead && !health.IsStunned && weaponHitbox != null)
            weaponHitbox.Activate(damage, weaponActiveDuration);
    }

    private void TickCooldown()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            state = (target != null && DistanceToTarget() <= detectRange) ? State.Chase : State.Idle;
        }
    }

    private void HandleDeath()
    {
        agent.isStopped = true;
        enabled = false;
        // TODO: 사망 애니메이션/이펙트 재생 후 제거
        Destroy(gameObject, 0f);
    }

    public void ApplyDamageMultiplier(float multiplier)
    {
        damage = Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
