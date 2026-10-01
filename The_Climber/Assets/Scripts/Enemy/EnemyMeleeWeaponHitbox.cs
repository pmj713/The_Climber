using System.Collections.Generic;
using UnityEngine;

// 근접 무기(몽둥이 등)에 붙여서, 실제로 무기가 플레이어와 닿았을 때만 데미지를 준다.
// MeleeEnemyAI가 공격 애니메이션이 재생되는 동안 Activate()로 잠깐 켜준다.
[RequireComponent(typeof(Collider))]
public class EnemyMeleeWeaponHitbox : MonoBehaviour
{
    [SerializeField] private LayerMask targetMask; // 비워두면 Player 레이어
    [SerializeField] private TrailRenderer swingTrail;   // 판정이 켜져 있는 동안만 그리는 휘두르기 궤적 (선택)
    [SerializeField] private GameObject hitEffectPrefab; // 대상을 맞힌 자리에 터지는 이펙트 (선택)

    private int damage;
    private bool isActive;
    private Collider hitCollider;
    private readonly HashSet<IDamageable> alreadyHitThisSwing = new HashSet<IDamageable>();
    private readonly Collider[] overlapBuffer = new Collider[8];

    private void Awake()
    {
        hitCollider = GetComponent<Collider>();
        hitCollider.isTrigger = true;
        if (targetMask == 0) targetMask = LayerMask.GetMask("Player");
        if (swingTrail != null) swingTrail.emitting = false;
    }

    // duration 동안 무기 판정을 켠다. 같은 스윙 안에서는 대상 하나당 한 번만 데미지가 들어간다.
    public void Activate(int hitDamage, float duration)
    {
        damage = hitDamage;
        isActive = true;
        alreadyHitThisSwing.Clear();
        CancelInvoke(nameof(Deactivate));
        Invoke(nameof(Deactivate), duration);
        if (swingTrail != null)
        {
            swingTrail.Clear();
            swingTrail.emitting = true;
        }
        HitOverlappingTargets();
    }

    public void Deactivate()
    {
        isActive = false;
        if (swingTrail != null) swingTrail.emitting = false;
    }

    // 트리거는 '들어올 때'만 이벤트가 오므로, 판정이 켜지기 전부터 무기 범위 안에 가만히 서 있던 대상은
    // OnTriggerEnter 로는 맞지 않는다. 판정이 켜져 있는 동안은 매 물리 스텝마다 직접 겹침 검사를 한다.
    private void FixedUpdate()
    {
        if (isActive) HitOverlappingTargets();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        TryHit(other);
    }

    private void HitOverlappingTargets()
    {
        int count = OverlapOwnShape();
        for (int i = 0; i < count; i++) TryHit(overlapBuffer[i]);
    }

    private int OverlapOwnShape()
    {
        Transform t = hitCollider.transform;
        Vector3 s = t.lossyScale;
        switch (hitCollider)
        {
            case SphereCollider sphere:
                float sphereRadius = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                return Physics.OverlapSphereNonAlloc(t.TransformPoint(sphere.center), sphereRadius, overlapBuffer, targetMask, QueryTriggerInteraction.Ignore);
            case BoxCollider box:
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                return Physics.OverlapBoxNonAlloc(t.TransformPoint(box.center), halfExtents, overlapBuffer, t.rotation, targetMask, QueryTriggerInteraction.Ignore);
            default:
                Bounds b = hitCollider.bounds;
                return Physics.OverlapBoxNonAlloc(b.center, b.extents, overlapBuffer, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);
        }
    }

    private void TryHit(Collider other)
    {
        if (!other.TryGetComponent<IDamageable>(out var damageable)) return;
        if (!alreadyHitThisSwing.Add(damageable)) return;

        damageable.TakeDamage(damage);

        if (hitEffectPrefab != null)
        {
            // CharacterController는 Collider.ClosestPoint를 지원하지 않아서 바운드 기준으로 접점을 잡는다
            Vector3 contact = other.bounds.ClosestPoint(transform.position);
            Vector3 outward = contact - other.bounds.center;
            Quaternion rotation = outward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(outward) : Quaternion.identity;
            Destroy(Instantiate(hitEffectPrefab, contact, rotation), 1.5f);
        }
    }
}
