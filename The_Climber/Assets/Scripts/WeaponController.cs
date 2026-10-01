using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class WeaponController : MonoBehaviour
{
    [Tooltip("SwordWeapon 또는 BowWeapon 컴포넌트를 드래그해서 지금 장착할 무기를 지정")]
    [SerializeField] private MonoBehaviour equippedWeaponBehaviour;
    [SerializeField] private float attackCooldown = 0.4f;
    [SerializeField] private float attackSpeedPerLevel = 0.1f;
    [SerializeField] private Transform attackOrigin;
    [Tooltip("장착 시 자동으로 보이는 플레이어 손의 검 모델")]
    [SerializeField] private GameObject swordVisualModel;
    [Tooltip("장착 시 자동으로 보이는 플레이어 손의 활 모델")]
    [SerializeField] private GameObject bowVisualModel;
    [Tooltip("무기 장식대 위의 검 장식 모델 (검을 장착하지 않았을 때만 보임)")]
    [SerializeField] private GameObject swordPedestalDisplay;
    [Tooltip("무기 장식대 위의 활 장식 모델 (활을 장착하지 않았을 때만 보임)")]
    [SerializeField] private GameObject bowPedestalDisplay;
    [SerializeField] private Animator animator;
    [Tooltip("검 스윙 애니메이션 재생 배속. 클수록 스윙이 빨라지고, 제자리 고정 시간도 그만큼 짧아진다")]
    [SerializeField] private float swordAttackSpeed = 3.625f;
    [Tooltip("활 쏘기 애니메이션 재생 배속. 클수록 동작이 빨라지고, 제자리 고정 시간도 그만큼 짧아진다")]
    [SerializeField] private float bowAttackSpeed = 2.875f;
    [Tooltip("활을 당기는 동작과 실제 화살 발사 사이의 딜레이(초). 0이면 클릭 즉시 발사")]
    [SerializeField] private float bowReleaseDelay = 0.15f;
    [Tooltip("검 끝에 붙은 궤적. 검 스윙 동작 중에만 그린다 (선택)")]
    [SerializeField] private TrailRenderer swordTrail;

    [Header("무기 장착 위치 (모델 뼈 기준)")]
    [SerializeField] private Vector3 swordHandPosition = new Vector3(0f, 0.035f, 0f);
    [SerializeField] private Vector3 swordHandAngles = Vector3.zero;
    [SerializeField] private Vector3 swordHipPosition = new Vector3(-0.20f, -0.015f, 0.025f);
    [SerializeField] private Vector3 swordHipAngles = new Vector3(78f, 180f, 0f);
    [SerializeField] private Vector3 bowHandPosition = new Vector3(0f, 0.035f, 0f);
    [Tooltip("검 손잡이 아래쪽의 왼손 지지점 (검 로컬 좌표)")]
    [SerializeField] private Vector3 swordSupportGrip = new Vector3(0f, 0f, -0.15556f);
    private Transform leftUpperArm;
    private Transform leftForearm;
    private Transform rightHand;
    private Transform leftHand;
    private Transform hips;
    private float attackVisualEndTime;
    private bool weaponGripsReady;
    private SwordWeapon swordWeapon;
    private MeshFilter swordMeshFilter;
    private Mesh extendedSwordMesh;
    private Vector3[] baseSwordVertices;
    private float appliedSwordRangeBonus;

    // 각 애니메이션 클립의 원래 길이(초). 배속으로 나누면 실제 재생 시간이 나온다
    private const float SwordSwingBaseDuration = 1.45f;
    private const float BowShotBaseDuration = 1.15f;

    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int BowAttackHash = Animator.StringToHash("BowAttack");
    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeedMultiplier");
    private static readonly int BowAttackSpeedHash = Animator.StringToHash("BowAttackSpeedMultiplier");

    private PlayerMovement movement;
    private PlayerSkillManager skillManager;
    private IWeapon currentWeapon;
    private float lastAttackTime = -999f;
    private float hasteMultiplier = 1f;
    private float hasteEndTime;

    // 레벨업 선택지(LevelUpSkillOffer)가 지금 장착한 무기에 맞는 스킬만 보여주기 위해 확인한다.
    public bool IsSwordEquipped => currentWeapon is SwordWeapon;
    public bool IsBowEquipped => currentWeapon is BowWeapon;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        skillManager = GetComponent<PlayerSkillManager>();
        currentWeapon = equippedWeaponBehaviour as IWeapon;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        InitializeWeaponGrips();
        UpdateEquippedVisual();
    }

    private void LateUpdate()
    {
        UpdateWeaponPose();
        UpdateSwordSupportHand();
        UpdateSwordBladeLength();
        UpdateSwordTrail();
    }

    private void UpdateSwordTrail()
    {
        if (swordTrail == null) return;
        bool swinging = currentWeapon is SwordWeapon && Time.time < attackVisualEndTime;
        if (swinging && !swordTrail.emitting) swordTrail.Clear(); // 칼집에서 손으로 옮겨진 순간의 선이 남지 않게
        swordTrail.emitting = swinging;
    }

    private void InitializeWeaponGrips()
    {
        if (weaponGripsReady || animator == null) return;
        foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "RightHand") rightHand = bone;
            else if (bone.name == "LeftHand") leftHand = bone;
            else if (bone.name == "Hips") hips = bone;
            else if (bone.name == "LeftArm") leftUpperArm = bone;
            else if (bone.name == "LeftForeArm") leftForearm = bone;
        }

        // 씬의 DiabloSword GLB는 XY 평면에 놓여 있으며 손잡이가 원점에 있지 않다.
        // 손잡이를 원점으로 옮긴다. 검 길이는 +Z, 칼날 폭은 +Y로 맞춰 YZ 궤적을 날로 벤다.
        if (rightHand != null)
            AlignWeaponMesh(swordVisualModel, "SwordModel", new Vector3(-0.245f, 0.245f, 0f),
                Quaternion.AngleAxis(90f, Vector3.forward) * Quaternion.FromToRotation(new Vector3(1f, 0f, -1f), Vector3.forward) * Quaternion.Euler(90f, 0f, 0f));
        if (leftHand != null)
            AlignWeaponMesh(bowVisualModel, "BowModel", new Vector3(-0.09f, 0f, 0f),
                Quaternion.Euler(0f, 90f, 0f));
        weaponGripsReady = true;
    }

    // 손잡이와 가드는 그대로 두고 검날만 사거리 증가량(월드 단위)만큼 늘린다.
    private void UpdateSwordBladeLength()
    {
        if (swordWeapon == null) swordWeapon = GetComponent<SwordWeapon>();
        if (swordWeapon == null || swordVisualModel == null) return;
        float bonus = swordWeapon.RangeBonus;
        if (Mathf.Approximately(bonus, appliedSwordRangeBonus)) return;
        if (swordMeshFilter == null)
            swordMeshFilter = swordVisualModel.GetComponentInChildren<MeshFilter>(true);
        if (swordMeshFilter == null) return;
        if (extendedSwordMesh == null)
        {
            extendedSwordMesh = Instantiate(swordMeshFilter.sharedMesh);
            extendedSwordMesh.name = "PlayerSword_Extended";
            baseSwordVertices = extendedSwordMesh.vertices;
            swordMeshFilter.sharedMesh = extendedSwordMesh;
        }

        Transform sword = swordVisualModel.transform;
        Matrix4x4 toSword = sword.worldToLocalMatrix * swordMeshFilter.transform.localToWorldMatrix;
        Matrix4x4 toMesh = toSword.inverse;
        const float bladeStart = 0.10f;
        float tip = bladeStart;
        foreach (Vector3 vertex in baseSwordVertices)
            tip = Mathf.Max(tip, toSword.MultiplyPoint3x4(vertex).z);
        float extension = bonus / Mathf.Max(0.0001f, sword.lossyScale.z);
        var vertices = new Vector3[baseSwordVertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 point = toSword.MultiplyPoint3x4(baseSwordVertices[i]);
            point.z += Mathf.InverseLerp(bladeStart, tip, point.z) * extension;
            vertices[i] = toMesh.MultiplyPoint3x4(point);
        }
        extendedSwordMesh.vertices = vertices;
        extendedSwordMesh.RecalculateNormals();
        extendedSwordMesh.RecalculateBounds();
        appliedSwordRangeBonus = bonus;
    }

    private void OnDestroy()
    {
        if (extendedSwordMesh != null) Destroy(extendedSwordMesh);
    }
    private static void AlignWeaponMesh(GameObject visual, string modelName, Vector3 grip, Quaternion rotation)
    {
        if (visual == null) return;
        Transform mesh = visual.transform.Find(modelName);
        if (mesh == null) return;
        mesh.localRotation = rotation;
        mesh.localPosition = -(rotation * Vector3.Scale(grip, mesh.localScale));
    }

    private bool IsPlayingWeaponAttack()
    {
        if (Time.time < attackVisualEndTime) return true;
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Attack") || state.IsName("BowAttack")) return true;
        if (!animator.IsInTransition(0)) return false;
        state = animator.GetNextAnimatorStateInfo(0);
        return state.IsName("Attack") || state.IsName("BowAttack");
    }

    private void UpdateWeaponPose()
    {
        bool sheathed = !IsPlayingWeaponAttack();
        AttachWeapon(swordVisualModel, sheathed ? hips : rightHand,
            sheathed ? swordHipPosition : swordHandPosition,
            Quaternion.Euler(sheathed ? swordHipAngles : swordHandAngles));

        // 시위를 당기는 기준 자세의 왼손 축에 맞춘 회전. 달릴 때도 같은 손목을 따라간다.
        Quaternion bowGripRotation = Quaternion.LookRotation(
            new Vector3(-0.172f, 0.973f, -0.152f), new Vector3(-0.497f, 0.047f, 0.867f));
        AttachWeapon(bowVisualModel, leftHand, bowHandPosition, bowGripRotation);
    }

    // Generic 리그: 애니메이션 평가 후 손목 회전과 두 팔 관절을 보정한다.
    // 팔 길이는 유지하고 왼손 손바닥을 검의 두 번째 손잡이 지점에 맞춘다.
    private void UpdateSwordSupportHand()
    {
        if (!(currentWeapon is SwordWeapon) || !IsPlayingWeaponAttack() ||
            swordVisualModel == null || leftUpperArm == null || leftForearm == null || leftHand == null) return;

        Transform sword = swordVisualModel.transform;
        Quaternion handRotation = sword.rotation * Quaternion.Euler(0f, 0f, 180f);
        Vector3 palmOffset = handRotation * Vector3.Scale(new Vector3(0f, 0.035f, 0f), leftHand.lossyScale);
        Vector3 target = sword.TransformPoint(swordSupportGrip) - palmOffset;
        Vector3 shoulder = leftUpperArm.position;
        float upperLength = Vector3.Distance(shoulder, leftForearm.position);
        float lowerLength = Vector3.Distance(leftForearm.position, leftHand.position);
        Vector3 delta = target - shoulder;
        if (delta.sqrMagnitude < 0.00000001f || upperLength < 0.0001f || lowerLength < 0.0001f) return;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.00001f,
            upperLength + lowerLength - 0.00001f);
        Vector3 axis = delta.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(animator.transform.TransformDirection(new Vector3(-1f, -0.4f, -0.8f)), axis).normalized;
        if (bend.sqrMagnitude < 0.0001f) bend = Vector3.ProjectOnPlane(animator.transform.up, axis).normalized;
        float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
        Vector3 elbow = shoulder + axis * along + bend * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        leftUpperArm.rotation = Quaternion.FromToRotation(leftForearm.position - shoulder, elbow - shoulder) * leftUpperArm.rotation;
        leftForearm.rotation = Quaternion.FromToRotation(leftHand.position - leftForearm.position,
            shoulder + axis * distance - leftForearm.position) * leftForearm.rotation;
        leftHand.rotation = handRotation;
    }
    private static void AttachWeapon(GameObject visual, Transform bone, Vector3 position, Quaternion rotation)
    {
        if (visual == null || bone == null) return;
        Transform weapon = visual.transform;
        if (weapon.parent != bone) weapon.SetParent(bone, false);
        weapon.SetLocalPositionAndRotation(position, rotation);
    }

    private void Update()
    {
        if (animator != null)
        {
            animator.SetFloat(AttackSpeedHash, Mathf.Max(0.01f, swordAttackSpeed));
            animator.SetFloat(BowAttackSpeedHash, Mathf.Max(0.01f, bowAttackSpeed));
        }

        if (Input.GetKeyDown(KeyBindingManager.GetKey(RebindableAction.NormalAttack)) && Time.time >= lastAttackTime + EffectiveCooldown())
        {
            Attack();
        }
    }

    private float EffectiveCooldown()
    {
        if (Time.time >= hasteEndTime) hasteMultiplier = 1f;

        // 일반공격 키워드는 레벨이 오를 때마다 데미지/공격속도가 번갈아 증가한다 (짝수 레벨 = 공격속도).
        int basicAttackLevel = skillManager != null ? skillManager.GetKeywordLevel(KeywordType.BasicAttack) : 0;
        int speedStacks = basicAttackLevel / 2;
        return (attackCooldown / (1f + attackSpeedPerLevel * speedStacks)) * hasteMultiplier;
    }

    // 헤이스트 같은 일시적 버프용. cooldownMultiplier가 1보다 작으면 그만큼 공격 쿨타임이 짧아진다.
    public void ApplyTemporaryHaste(float cooldownMultiplier, float duration)
    {
        hasteMultiplier = cooldownMultiplier;
        hasteEndTime = Time.time + duration;
    }

    private void Attack()
    {
        if (currentWeapon == null) return;

        lastAttackTime = Time.time;
        bool isSword = currentWeapon is SwordWeapon;

        float rootDuration = isSword
            ? SwordSwingBaseDuration / Mathf.Max(0.01f, swordAttackSpeed)
            : BowShotBaseDuration / Mathf.Max(0.01f, bowAttackSpeed);
        attackVisualEndTime = Time.time + rootDuration;
        UpdateWeaponPose();
        if (isSword && swordTrail != null && currentWeapon is SwordWeapon sword)
            swordTrail.colorGradient = EffectTint.TrailGradient(EffectTint.ForElement(sword.CurrentElement));

        if (movement != null)
        {
            movement.LockMovementForAttack(rootDuration);
        }

        if (animator != null)
        {
            animator.SetTrigger(isSword ? AttackHash : BowAttackHash);
        }

        if (isSword || bowReleaseDelay <= 0f)
        {
            FireWeapon();
        }
        else
        {
            StartCoroutine(FireAfterDelay(bowReleaseDelay));
        }
    }

    // 활을 당기는 동작(애니메이션)이 어느 정도 진행된 뒤 실제로 화살이 발사되도록 지연시킨다
    private IEnumerator FireAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        FireWeapon();
    }

    private void FireWeapon()
    {
        if (currentWeapon == null) return;

        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up;
        currentWeapon.TryAttack(origin, movement.GetAimDirection());
    }

    public void EquipWeapon(MonoBehaviour weaponBehaviour)
    {
        equippedWeaponBehaviour = weaponBehaviour;
        currentWeapon = weaponBehaviour as IWeapon;
        UpdateEquippedVisual();
    }

    // 장착된 무기 종류에 맞춰 플레이어 손에 보이는 모델을 전환한다.
    private void UpdateEquippedVisual()
    {
        bool isSword = equippedWeaponBehaviour is SwordWeapon;
        if (swordVisualModel != null) swordVisualModel.SetActive(isSword);
        if (bowVisualModel != null) bowVisualModel.SetActive(!isSword);

        // 들고 있지 않은 무기만 장식대 위에 보이도록 한다
        if (swordPedestalDisplay != null) swordPedestalDisplay.SetActive(!isSword);
        if (bowPedestalDisplay != null) bowPedestalDisplay.SetActive(isSword);
        UpdateWeaponPose();
    }
}
