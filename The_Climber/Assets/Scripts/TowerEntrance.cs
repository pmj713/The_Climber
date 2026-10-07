using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 탑 입구. 마을에서는 탑 런을 시작하고(Floor1로), 탑 안에서는 같은 모델을 놓아 다음 층으로 넘어가는 데 쓴다.
// 콜라이더/트리거에 기대지 않고 플레이어와의 거리로만 판정한다
// (입구 3D 모델의 콜라이더는 물리적으로 막는 용도로 그대로 둬야 해서 건드리지 않는다).
public class TowerEntrance : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("이 입구로 들어가면 넘어갈 씬. Inspector에 씬 파일을 끌어다 놓으면 된다")]
    [SceneName, SerializeField] private string destinationScene = "Floor1";
    [Tooltip("체크: 새 탑 런 시작(레벨/스킬/체력 초기화) - 마을 입구용\n해제: 진행 상황을 그대로 들고 이동 - 탑 안 '다음 층' 입구용")]
    [SerializeField] private bool startNewRun = true;
    [Tooltip("체크하면 이 층의 방을 모두 정리해야 들어갈 수 있다 (탑 안 '다음 층' 입구용)")]
    [SerializeField] private bool requireFloorCleared;

    [Header("상호작용")]
    [SerializeField] private Transform player; // 비워두면 PlayerHealth.Instance에서 자동으로 찾음
    [Tooltip("새 런 시작 입구일 때만 필요. 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private TownController townController;
    [Tooltip("플레이어가 이 거리 안으로 들어오면 상호작용 힌트가 뜨고 키 입력을 받는다")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private Text hintText; // "E: 탑 입장" / "E: 다음 층" 같은 안내 문구, 없어도 동작함
    [SerializeField] private string lockedHint = "모든 방을 정리하면 열립니다";

    private bool playerInRange;
    private string openHint;

    private void Start()
    {
        if (player == null && PlayerHealth.Instance != null)
        {
            player = PlayerHealth.Instance.transform;
        }
        if (townController == null) townController = FindAnyObjectByType<TownController>();

        if (hintText != null)
        {
            openHint = hintText.text;
            hintText.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        PlayerInteraction.SetAvailable(this, false, interactKey);
    }

    private bool IsLocked =>
        requireFloorCleared && FloorManager.Instance != null &&
        FloorManager.Instance.ClearedRoomCount < FloorManager.Instance.TotalRoomCount;

    private void Update()
    {
        // 마을을 다시 불러오면 씬에 들어 있던 HUD/플레이어 사본이 중복으로 파괴되어 참조가 비므로, 살아 있는 본체에서 다시 찾는다
        if (townController == null) townController = FindAnyObjectByType<TownController>();
        if (player == null && PlayerHealth.Instance != null) player = PlayerHealth.Instance.transform;

        if (player == null) return;

        bool inRange = Vector3.Distance(transform.position, player.position) <= interactDistance;
        if (inRange != playerInRange)
        {
            playerInRange = inRange;
            if (hintText != null) hintText.gameObject.SetActive(inRange);
        }
        if (playerInRange && hintText != null) hintText.text = IsLocked ? lockedHint : openHint;
        PlayerInteraction.SetAvailable(this, playerInRange && !IsLocked, interactKey);

        // 일시정지/설정 등으로 게임이 멈춰있을 때는 상호작용 키 입력을 받지 않는다.
        if (Time.timeScale == 0f) return;

        if (playerInRange && Input.GetKeyDown(interactKey) && !IsLocked)
        {
            Enter();
        }
    }

    private void Enter()
    {
        if (string.IsNullOrEmpty(destinationScene))
        {
            Debug.LogWarning($"TowerEntrance({name}): 이동할 씬이 지정되지 않았습니다.");
            return;
        }

        if (startNewRun && townController != null)
        {
            townController.EnterTower(destinationScene);
        }
        else
        {
            // 다음 층: 레벨/스킬/체력은 그대로 들고 넘어간다. 도착 위치는 새 층의 FloorSpawnPoint가 정한다.
            SceneManager.LoadScene(destinationScene);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
