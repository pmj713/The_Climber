using System.Collections.Generic;
using UnityEngine;

// 근접 무기(몽둥이 등)에 붙여서, 실제로 무기가 플레이어와 닿았을 때만 데미지를 준다.
// MeleeEnemyAI가 공격 애니메이션이 재생되는 동안 Activate()로 잠깐 켜준다.
[RequireComponent(typeof(Collider))]
public class EnemyMeleeWeaponHitbox : MonoBehaviour
{
    private int damage;
    private bool isActive;
    private readonly HashSet<IDamageable> alreadyHitThisSwing = new HashSet<IDamageable>();

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    // duration 동안 무기 판정을 켠다. 같은 스윙 안에서는 대상 하나당 한 번만 데미지가 들어간다.
    public void Activate(int hitDamage, float duration)
    {
        damage = hitDamage;
        isActive = true;
        alreadyHitThisSwing.Clear();
        CancelInvoke(nameof(Deactivate));
        Invoke(nameof(Deactivate), duration);
    }

    public void Deactivate()
    {
        isActive = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        if (!other.TryGetComponent<IDamageable>(out var damageable)) return;
        if (!alreadyHitThisSwing.Add(damageable)) return;

        damageable.TakeDamage(damage);
    }
}
