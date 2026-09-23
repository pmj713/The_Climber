using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class RangedEnemyAI : MonoBehaviour
{
    // 근접 몬스터와 동일한 상태 흐름이지만 Attack에서 직접 타격 대신 투사체를 발사한다.
    private enum State { Idle, Chase, AttackPrepare, Attack, Cooldown }

    [SerializeField] private float detectRange = 8f;
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackPrepareTime = 0.6f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private int damage = 6;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private EnemyProjectile projectilePrefab;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Animator animator;
    [Tooltip("BowAttack 애니메이션이 시작된 뒤, 실제로 화살이 발사될 때까지의 지연 시간(초). 활을 쏘는 릴리즈 동작과 맞도록 조절")]
    [SerializeField] private float fireDelay = 0f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int BowAttackHash = Animator.StringToHash("BowAttack");

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
        agent.speed = moveSpeed;
        agent.updateRotation = false;
        agent.stoppingDistance = attackRange * 0.9f;
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
        // TODO: 조준선/경고 이펙트 트리거
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
        if (animator != null) animator.SetTrigger(BowAttackHash);

        if (target != null && DistanceToTarget() <= attackRange)
        {
            // 화살은 애니메이션에서 실제로 시위를 놓는 시점(fireDelay)에 맞춰 발사한다
            StartCoroutine(FireAfterDelay(fireDelay));
        }

        state = State.Cooldown;
        stateTimer = attackCooldown + fireDelay;
    }

    private IEnumerator FireAfterDelay(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        FireProjectile();
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("RangedEnemyAI: projectilePrefab이 지정되지 않았습니다.");
            return;
        }
        if (target == null) return;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up;
        Vector3 direction = (target.position - origin);
        direction.y = 0f;
        direction.Normalize();

        EnemyProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
        projectile.Launch(direction, projectileSpeed, damage);
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
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
