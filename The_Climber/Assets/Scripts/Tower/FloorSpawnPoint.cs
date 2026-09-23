using UnityEngine;

// 탑 층 씬(Floor1 등)에 하나 배치해두면, 그 씬이 로드될 때 플레이어를 이 위치/방향으로
// 순간이동시킨다. 탑 입구에서 들어올 때마다 항상 정해진 자리에서 시작하도록 하기 위함.
public class FloorSpawnPoint : MonoBehaviour
{
    private void Awake()
    {
        if (PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.Teleport(transform.position, transform.rotation);
        }
    }
}
