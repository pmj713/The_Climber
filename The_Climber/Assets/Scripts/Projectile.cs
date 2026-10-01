using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private LayerMask ignoreMask;
    [SerializeField] private TrailRenderer trail;        // 속성 색으로 물드는 화살 꼬리 (선택)
    [SerializeField] private GameObject hitEffectPrefab; // 적/벽에 맞은 자리 (선택)

    private Vector3 direction;
    private Vector3 startPosition;
    private float speed;
    private int damage;
    private int pierceRemaining;
    private ElementType element;
    private int elementStacks;

    public void Launch(Vector3 dir, float projectileSpeed, int projectileDamage, int pierceCount = 0,
        ElementType appliedElement = ElementType.None, int appliedElementStacks = 1)
    {
        direction = dir.normalized;
        speed = projectileSpeed;
        damage = projectileDamage;
        pierceRemaining = pierceCount;
        element = appliedElement;
        elementStacks = appliedElementStacks;
        startPosition = transform.position;
        if (trail != null) trail.colorGradient = EffectTint.TrailGradient(EffectTint.ForElement(element));
        Destroy(gameObject, lifetime);
    }

    private void SpawnHitEffect()
    {
        EffectTint.Spawn(hitEffectPrefab, transform.position, Quaternion.LookRotation(direction), EffectTint.ForElement(element));
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;

        if (Vector3.Distance(startPosition, transform.position) >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        int otherLayerBit = 1 << other.gameObject.layer;

        // 여기 포함된 레이어는 데미지도, 소멸도 없이 그냥 통과
        if ((otherLayerBit & ignoreMask) != 0) return;

        // 적 무기의 공격 판정은 몸이 아니라서 맞아도 체력이 없다. 여기서 막히면 화살이 사라져 버리니 그냥 통과시킨다.
        if (other.TryGetComponent<EnemyMeleeWeaponHitbox>(out _)) return;

        if ((otherLayerBit & enemyMask) != 0)
        {
            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                if (damageable is EnemyHealth enemy) enemy.TakeElementalDamage(damage, element, elementStacks, false);
                else damageable.TakeDamage(damage);
            }
            SpawnHitEffect();

            if (pierceRemaining <= 0)
            {
                Destroy(gameObject);
            }
            else
            {
                pierceRemaining--;
            }
            return;
        }

        // 대미지 대상은 아니지만 벽 같은 장애물이면 여기서 막힌다
        if ((otherLayerBit & obstacleMask) != 0)
        {
            SpawnHitEffect();
            Destroy(gameObject);
        }
    }
}
