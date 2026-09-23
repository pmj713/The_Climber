using System;
using UnityEngine;
using Random = UnityEngine.Random;

// 탑 안의 방 하나에 붙이는 컴포넌트. 플레이어가 triggerDistance 안으로 들어오면
// 방 범위(roomSize) 안의 랜덤 위치에 몬스터를 스폰한다. 한 번 발동되면 다시 스폰하지 않는다.
// 스폰한 몬스터를 전부 잡으면 OnCleared를 쏴서 FloorManager가 층 진행도를 추적할 수 있게 한다.
public class RoomEncounter : MonoBehaviour
{
    [SerializeField] private Transform player; // 비워두면 PlayerHealth.Instance에서 자동으로 찾음
    [Tooltip("플레이어가 이 거리 안으로 들어오면 몬스터가 스폰된다")]
    [SerializeField] private float triggerDistance = 8f;
    [Tooltip("방의 가로(x) x 세로(z) 크기. 이 범위 안에서 랜덤한 위치에 몬스터가 스폰된다")]
    [SerializeField] private Vector2 roomSize = new Vector2(10f, 10f);

    [SerializeField] private MeleeEnemyAI meleeEnemyPrefab;
    [SerializeField] private RangedEnemyAI rangedEnemyPrefab;
    [SerializeField] private int meleeCount = 3;
    [SerializeField] private int rangedCount = 0;
    [SerializeField] private float healthMultiplier = 1f;
    [SerializeField] private float damageMultiplier = 1f;

    private bool hasTriggered;
    private int aliveCount;

    // 이 방이 스폰한 몬스터를 전부 잡으면 호출된다 (FloorManager가 층 클리어 판정에 사용).
    public event Action<RoomEncounter> OnCleared;

    private void Start()
    {
        if (player == null && PlayerHealth.Instance != null)
        {
            player = PlayerHealth.Instance.transform;
        }
    }

    // FloorManager가 이 층의 난이도 배율을 방 자체 배율에 곱해서 적용한다.
    public void ApplyFloorMultipliers(float floorHealthMultiplier, float floorDamageMultiplier)
    {
        healthMultiplier *= floorHealthMultiplier;
        damageMultiplier *= floorDamageMultiplier;
    }

    private void Update()
    {
        if (hasTriggered || player == null) return;

        if (Vector3.Distance(transform.position, player.position) <= triggerDistance)
        {
            SpawnMonsters();
        }
    }

    private void SpawnMonsters()
    {
        hasTriggered = true;
        aliveCount = 0;

        for (int i = 0; i < meleeCount; i++)
        {
            SpawnEnemy(meleeEnemyPrefab);
        }

        for (int i = 0; i < rangedCount; i++)
        {
            SpawnEnemy(rangedEnemyPrefab);
        }

        // 프리팹이 하나도 없어 실제로 몬스터를 못 만들었으면 바로 클리어 처리
        if (aliveCount == 0) OnCleared?.Invoke(this);
    }

    private void SpawnEnemy(Component prefab)
    {
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab.gameObject, GetRandomPointInRoom(), Quaternion.identity);
        aliveCount++;

        if (instance.TryGetComponent<EnemyHealth>(out var health))
        {
            health.ApplyHealthMultiplier(healthMultiplier);
            health.OnDeath += HandleEnemyDeath;
        }

        if (instance.TryGetComponent<MeleeEnemyAI>(out var melee))
        {
            melee.ApplyDamageMultiplier(damageMultiplier);
        }

        if (instance.TryGetComponent<RangedEnemyAI>(out var ranged))
        {
            ranged.ApplyDamageMultiplier(damageMultiplier);
        }
    }

    private void HandleEnemyDeath()
    {
        aliveCount--;
        if (aliveCount <= 0) OnCleared?.Invoke(this);
    }

    private Vector3 GetRandomPointInRoom()
    {
        float x = Random.Range(-roomSize.x / 2f, roomSize.x / 2f);
        float z = Random.Range(-roomSize.y / 2f, roomSize.y / 2f);
        return transform.position + new Vector3(x, 0f, z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(roomSize.x, 0.1f, roomSize.y));

        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, triggerDistance);
    }
}
