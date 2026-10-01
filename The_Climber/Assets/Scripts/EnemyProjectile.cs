using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private LayerMask ignoreMask;
    [SerializeField] private GameObject impactEffectPrefab; // 비워두면 맞아도 이펙트 없음

    private Vector3 direction;
    private Vector3 startPosition;
    private float speed;
    private int damage;

    public void Launch(Vector3 dir, float projectileSpeed, int projectileDamage)
    {
        direction = dir.normalized;
        speed = projectileSpeed;
        damage = projectileDamage;
        startPosition = transform.position;
        Destroy(gameObject, lifetime);
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

        if ((otherLayerBit & playerMask) != 0)
        {
            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(damage);
            }
            SpawnImpact();
            Destroy(gameObject);
            return;
        }

        // 대미지 대상은 아니지만 벽 같은 장애물이면 여기서 막힌다
        if ((otherLayerBit & obstacleMask) != 0)
        {
            SpawnImpact();
            Destroy(gameObject);
        }
    }

    private void SpawnImpact()
    {
        if (impactEffectPrefab == null) return;
        Destroy(Instantiate(impactEffectPrefab, transform.position, Quaternion.LookRotation(-direction)), 2f);
    }
}
