using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private LayerMask ignoreMask;

    private Vector3 direction;
    private Vector3 startPosition;
    private float speed;
    private int damage;
    private int pierceRemaining;

    public void Launch(Vector3 dir, float projectileSpeed, int projectileDamage, int pierceCount = 0)
    {
        direction = dir.normalized;
        speed = projectileSpeed;
        damage = projectileDamage;
        pierceRemaining = pierceCount;
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

        if ((otherLayerBit & enemyMask) != 0)
        {
            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(damage);
            }

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
            Destroy(gameObject);
        }
    }
}
