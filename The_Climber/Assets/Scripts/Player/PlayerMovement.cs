using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement Instance { get; private set; }

    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Animator animator;
    [SerializeField] private float runAnimationYawOffset = -80f;

    [SerializeField] private float dodgeDistance = 4f;
    [SerializeField] private float dodgeDuration = 0.2f;
    [SerializeField] private float dodgeCooldown = 10f;
    [SerializeField] private float invulnerabilityDuration = 0.3f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    private CharacterController controller;
    private Transform modelTransform;
    private Vector3 moveInput;
    private Vector3 aimPoint;
    private float verticalVelocity;
    private float lastDodgeTime = -999f;
    private bool isDodging;
    private float speedMultiplier = 1f;
    private float speedBoostEndTime;
    private float attackRootEndTime;
    private float effectiveMoveSpeed;

    public bool IsInvulnerable { get; private set; }
    public Vector3 MoveDirection => moveInput;

    public float GetDodgeCooldownRemaining()
    {
        return Mathf.Max(0f, (lastDodgeTime + dodgeCooldown) - Time.time);
    }

    public float DodgeCooldown => dodgeCooldown;

    private void Awake()
    {
        Instance = this;
        controller = GetComponent<CharacterController>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) modelTransform = animator.transform;
        effectiveMoveSpeed = moveSpeed;
    }

    private void Update()
    {
        // 일시정지(스킬 선택/설정 등으로 Time.timeScale이 0이 된 상태)에는 조작을 완전히 무시한다.
        // Update 자체는 timeScale과 무관하게 계속 돌기 때문에, 안 막아두면 이동은 멈춰도
        // 마우스 방향으로 캐릭터가 계속 회전하는 등 입력이 새어 들어간다.
        if (Time.timeScale == 0f) return;

        ReadMoveInput();
        UpdateAimPoint();

        if (!isDodging)
        {
            if (Time.time >= attackRootEndTime) Move();
            RotateTowardsFacingTarget();
        }

        ApplyGravity();
        UpdateAnimator();

        if (Input.GetKeyDown(KeyBindingManager.GetKey(RebindableAction.Dodge)) && !isDodging && Time.time >= lastDodgeTime + dodgeCooldown)
        {
            StartCoroutine(DoDodge());
        }
    }

    private void ReadMoveInput()
    {
        float h = 0f;
        if (Input.GetKey(KeyBindingManager.GetKey(RebindableAction.MoveRight))) h += 1f;
        if (Input.GetKey(KeyBindingManager.GetKey(RebindableAction.MoveLeft))) h -= 1f;

        float v = 0f;
        if (Input.GetKey(KeyBindingManager.GetKey(RebindableAction.MoveUp))) v += 1f;
        if (Input.GetKey(KeyBindingManager.GetKey(RebindableAction.MoveDown))) v -= 1f;

        // 카메라 기준 방향으로 이동해야 화면 위쪽으로 W가 먹힘
        Vector3 camForward = mainCamera.transform.forward;
        Vector3 camRight = mainCamera.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        moveInput = camForward * v + camRight * h;
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
    }

    private void Move()
    {
        if (Time.time >= speedBoostEndTime) speedMultiplier = 1f;

        controller.Move(moveInput * effectiveMoveSpeed * speedMultiplier * Time.deltaTime);
    }

    // 씬을 넘나들 때(예: 탑 입구 -> Floor1) 정해진 스폰 지점으로 순간이동시키는 용도.
    // CharacterController는 매 프레임 Move()로만 움직이는 게 정상 동작이라, 직접
    // transform.position을 바꾸는 동안엔 잠깐 꺼뒀다 켜서 내부 상태가 꼬이지 않게 한다.
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        if (controller != null) controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        verticalVelocity = 0f;
        if (controller != null) controller.enabled = true;
    }

    // 헤이스트 같은 일시적 이동속도 버프용
    public void ApplyTemporarySpeedBoost(float multiplier, float duration)
    {
        speedMultiplier = multiplier;
        speedBoostEndTime = Time.time + duration;
    }

    // 엘리트 몬스터의 강타 등 넉백 효과용. 회피 중에는 무시한다.
    public void ApplyKnockback(Vector3 velocity, float duration = 0.25f)
    {
        if (isDodging) return;
        StartCoroutine(KnockbackRoutine(velocity, duration));
    }

    // 보스 광역 강타 등 "경직" 효과용. 공격 중 이동을 묶어두는 것과 같은 잠금을 재사용한다.
    public void ApplyStun(float duration)
    {
        attackRootEndTime = Mathf.Max(attackRootEndTime, Time.time + duration);
    }

    private IEnumerator KnockbackRoutine(Vector3 velocity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float remaining = 1f - elapsed / duration;
            controller.Move(velocity * remaining * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // 공격 중에는 제자리에서 휘두르도록 이동을 잠깐 묶어둔다 (검/활 공용)
    public void LockMovementForAttack(float duration)
    {
        attackRootEndTime = Mathf.Max(attackRootEndTime, Time.time + duration);

        // 무기 Update가 먼저 실행돼도 이번 프레임의 커서를 향한 상태로 공격을 시작한다.
        UpdateAimPoint();
        transform.rotation = Quaternion.LookRotation(GetAimDirection());
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        bool moving = Time.time >= attackRootEndTime
            && (isDodging || moveInput.sqrMagnitude > 0.0001f);
        animator.SetBool(IsMovingHash, moving);

        // 달리기 클립의 진행 방향이 모델 정면과 어긋나 있어서, 재생 중에는 모델만 보정 회전을 적용한다
        if (modelTransform != null)
        {
            modelTransform.localRotation = moving ? Quaternion.Euler(0f, runAnimationYawOffset, 0f) : Quaternion.identity;
        }
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += Physics.gravity.y * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private void UpdateAimPoint()
    {
        // 마우스 커서 아래 지점을 플레이어 높이의 가상 평면에 투영해서 조준점으로 사용
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (plane.Raycast(ray, out float distance))
        {
            aimPoint = ray.GetPoint(distance);
        }
    }

    // 이동 중에는 이동 방향을, 멈춰있을 땐 조준점(마우스)을 바라본다.
    // 단, 공격 중에는 이동 방향과 상관없이 항상 조준점(공격 대상 방향)을 바라본다
    private void RotateTowardsFacingTarget()
    {
        bool isAttacking = Time.time < attackRootEndTime;
        Vector3 dir = (!isAttacking && moveInput.sqrMagnitude > 0.0001f) ? moveInput : aimPoint - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(dir);
        transform.rotation = isAttacking
            ? targetRotation
            : Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    public Vector3 GetAimPoint() => aimPoint;

    public Vector3 GetAimDirection()
    {
        Vector3 dir = aimPoint - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
    }

    private IEnumerator DoDodge()
    {
        lastDodgeTime = Time.time;
        isDodging = true;
        IsInvulnerable = true;

        // 이동 입력이 있으면 그 방향으로, 없으면 바라보는 방향으로 회피
        Vector3 direction = moveInput.sqrMagnitude > 0.01f
            ? moveInput.normalized
            : transform.forward;

        float speed = dodgeDistance / dodgeDuration;
        float elapsed = 0f;

        while (elapsed < dodgeDuration)
        {
            controller.Move(direction * speed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        isDodging = false;

        float remainingInvuln = invulnerabilityDuration - dodgeDuration;
        if (remainingInvuln > 0f) yield return new WaitForSeconds(remainingInvuln);
        IsInvulnerable = false;
    }
}
