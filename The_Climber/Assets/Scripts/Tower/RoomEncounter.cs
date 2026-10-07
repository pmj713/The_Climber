using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// 탑 안의 방 하나에 붙이는 컴포넌트 (Hades 방식).
// 플레이어가 triggerDistance 안으로 들어오면 입구를 잠그고, 몬스터를 웨이브로 나눠 소환한다.
// 웨이브마다 바닥에 소환 표시(붉은 원)가 먼저 뜨고, spawnTelegraphTime 뒤에 그 자리에 몬스터가 나타난다.
// 앞 웨이브가 거의 정리되면 다음 웨이브가 시작되고, 마지막 웨이브를 전부 잡으면 문이 열리며 OnCleared를 쏜다.
public class RoomEncounter : MonoBehaviour
{
    // 근접/원거리 외에 엘리트·보스 같은 특수 몬스터를 소환하는 설정
    [Serializable]
    public class ExtraSpawn
    {
        public GameObject prefab;
        public int count = 1;
        [Tooltip("체크: 마지막에 이 몬스터만 따로 한 웨이브로 소환 (보스용)\n해제: 마지막 일반 웨이브에 섞어서 소환 (엘리트용)")]
        public bool ownWave;
    }

    [SerializeField] private Transform player; // 비워두면 PlayerHealth.Instance에서 자동으로 찾음
    [Tooltip("플레이어가 이 거리 안으로 들어오면 전투가 시작된다")]
    [SerializeField] private float triggerDistance = 8f;
    [Tooltip("방의 가로(x) x 세로(z) 크기. 이 범위 안에서 몬스터가 소환된다")]
    [SerializeField] private Vector2 roomSize = new Vector2(10f, 10f);

    [SerializeField] private MeleeEnemyAI meleeEnemyPrefab;
    [SerializeField] private RangedEnemyAI rangedEnemyPrefab;
    [Tooltip("방 전체에서 소환할 몬스터 총 수 (웨이브들에 나눠서 소환된다)")]
    [SerializeField] private int meleeCount = 3;
    [SerializeField] private int rangedCount = 0;
    [Tooltip("엘리트/보스 등 특수 몬스터")]
    [SerializeField] private ExtraSpawn[] extraSpawns;
    [SerializeField] private float healthMultiplier = 1f;
    [SerializeField] private float damageMultiplier = 1f;

    [Header("웨이브 (Hades 방식)")]
    [SerializeField] private int waveCount = 3;
    [Tooltip("소환 표시가 뜬 뒤 몬스터가 실제로 나타날 때까지의 시간(초)")]
    [SerializeField] private float spawnTelegraphTime = 1.5f;
    [Tooltip("남은 몬스터가 이 수 이하가 되면 다음 웨이브를 시작한다 (0이면 전부 잡아야 시작)")]
    [SerializeField] private int nextWaveRemainingThreshold = 0;
    [Tooltip("다음 웨이브 시작 조건을 채운 뒤 소환 표시가 뜨기까지의 대기 시간(초)")]
    [SerializeField] private float wavePause = 1f;
    [Tooltip("플레이어와 이 거리 안에는 몬스터를 소환하지 않는다")]
    [SerializeField] private float minSpawnDistanceFromPlayer = 8f;
    [Tooltip("소환 지점끼리 최소 간격")]
    [SerializeField] private float minSpawnSpacing = 2.5f;

    [Header("입구 잠금")]
    [Tooltip("전투 중에만 켜지는 문. 비워두면 잠그지 않는다")]
    [SerializeField] private GameObject[] lockGates;

    private bool hasTriggered;
    private int aliveCount;
    private Material markerMaterial;

    // 이 방이 스폰한 몬스터를 전부 잡으면 호출된다 (FloorManager가 층 클리어 판정에 사용).
    public event Action<RoomEncounter> OnCleared;

    private void Start()
    {
        if (player == null && PlayerHealth.Instance != null)
        {
            player = PlayerHealth.Instance.transform;
        }
        SetGates(false);
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
            hasTriggered = true;
            StartCoroutine(EncounterRoutine());
        }
    }

    private IEnumerator EncounterRoutine()
    {
        int waves = Mathf.Max(1, waveCount);
        var plan = new List<List<GameObject>>();
        for (int w = 0; w < waves; w++)
        {
            var wave = new List<GameObject>();
            AddShare(wave, meleeEnemyPrefab, meleeCount, w, waves);
            AddShare(wave, rangedEnemyPrefab, rangedCount, w, waves);
            if (wave.Count > 0) plan.Add(wave);
        }

        if (extraSpawns != null)
        {
            var ownWaveList = new List<GameObject>();
            foreach (ExtraSpawn extra in extraSpawns)
            {
                if (extra == null || extra.prefab == null) continue;
                if (!extra.ownWave && plan.Count == 0) plan.Add(new List<GameObject>());
                List<GameObject> target = extra.ownWave ? ownWaveList : plan[plan.Count - 1];
                for (int i = 0; i < extra.count; i++) target.Add(extra.prefab);
            }
            if (ownWaveList.Count > 0) plan.Add(ownWaveList);
        }

        // 프리팹이 하나도 없어 실제로 몬스터를 못 만들면 바로 클리어 처리
        if (plan.Count == 0)
        {
            OnCleared?.Invoke(this);
            yield break;
        }

        SetGates(true);

        for (int w = 0; w < plan.Count; w++)
        {
            yield return SpawnWave(plan[w]);

            if (w < plan.Count - 1)
            {
                yield return new WaitUntil(() => aliveCount <= nextWaveRemainingThreshold);
                yield return new WaitForSeconds(wavePause);
            }
        }

        yield return new WaitUntil(() => aliveCount <= 0);

        SetGates(false);
        OnCleared?.Invoke(this);
    }

    // total마리를 waves개 웨이브에 고르게 나눴을 때 wave번째 몫을 추가한다
    private static void AddShare(List<GameObject> wave, Component prefab, int total, int wave_, int waves)
    {
        if (prefab == null) return;
        int share = total * (wave_ + 1) / waves - total * wave_ / waves;
        for (int i = 0; i < share; i++) wave.Add(prefab.gameObject);
    }

    private IEnumerator SpawnWave(List<GameObject> prefabs)
    {
        var points = new List<Vector3>();
        var markers = new List<Transform>();
        foreach (var _ in prefabs)
        {
            Vector3 point = PickSpawnPoint(points);
            points.Add(point);
            markers.Add(CreateMarker(point));
        }

        for (float t = 0f; t < spawnTelegraphTime; t += Time.deltaTime)
        {
            float progress = t / spawnTelegraphTime;
            foreach (Transform marker in markers)
            {
                if (marker != null) marker.GetChild(1).localScale = Vector3.one * progress;
            }
            yield return null;
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            Destroy(markers[i].gameObject);
            SpawnEnemy(prefabs[i], points[i]);
        }
    }

    private void SpawnEnemy(GameObject prefab, Vector3 position)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        aliveCount++;

        if (instance.TryGetComponent<EnemyHealth>(out var health))
        {
            health.ApplyHealthMultiplier(healthMultiplier);
            health.OnDeath += HandleEnemyDeath;
        }

        foreach (IEnemyEmpowerable ai in instance.GetComponents<IEnemyEmpowerable>())
        {
            ai.ApplyDamageMultiplier(damageMultiplier);
        }

        // 방 안 어디에 소환돼도 플레이어를 바로 인식하도록, 인식 범위를 방 대각선 길이로 늘린다
        foreach (IEnemyDetectionConfigurable ai in instance.GetComponents<IEnemyDetectionConfigurable>())
        {
            ai.SetDetectRange(roomSize.magnitude);
        }
    }

    private void HandleEnemyDeath()
    {
        aliveCount--;
    }

    // 방 안의 내비메시 위 지점 중 플레이어/다른 소환 지점과 충분히 떨어진 곳을 고른다
    private Vector3 PickSpawnPoint(List<Vector3> taken)
    {
        Vector3 fallback = transform.position;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector3 candidate = transform.position + new Vector3(
                Random.Range(-roomSize.x / 2f, roomSize.x / 2f), 0f,
                Random.Range(-roomSize.y / 2f, roomSize.y / 2f));
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;

            fallback = hit.position;
            if (player != null && HorizontalDistance(hit.position, player.position) < minSpawnDistanceFromPlayer) continue;
            bool tooClose = false;
            foreach (Vector3 other in taken)
            {
                if (HorizontalDistance(hit.position, other) < minSpawnSpacing) { tooClose = true; break; }
            }
            if (!tooClose) return hit.position;
        }
        return fallback;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // 바깥 고리(고정) + 안쪽 고리(소환까지 커짐)로 이루어진 소환 표시
    private Transform CreateMarker(Vector3 position)
    {
        var root = new GameObject("SpawnMarker").transform;
        root.position = position + Vector3.up * 0.05f;
        CreateRing(root, "Outer", 0.9f);
        CreateRing(root, "Inner", 0.9f);
        root.GetChild(1).localScale = Vector3.zero;
        return root;
    }

    private void CreateRing(Transform parent, string name, float radius)
    {
        if (markerMaterial == null)
        {
            markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            markerMaterial.SetColor("_BaseColor", new Color(1f, 0.15f, 0.1f));
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = markerMaterial;
        line.useWorldSpace = false;
        line.loop = true;
        line.widthMultiplier = 0.1f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.TransformZ;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        const int segments = 32;
        line.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    private void SetGates(bool locked)
    {
        if (lockGates == null) return;
        foreach (GameObject gate in lockGates)
        {
            if (gate != null) gate.SetActive(locked);
        }
    }

    private void OnDestroy()
    {
        if (markerMaterial != null) Destroy(markerMaterial);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(roomSize.x, 0.1f, roomSize.y));

        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, triggerDistance);
    }
}
