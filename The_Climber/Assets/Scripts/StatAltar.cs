using UnityEngine;
using UnityEngine.UI;

// 마을의 적 강화 제단. 콜라이더/트리거에 기대지 않고 플레이어와의 거리로만 판정한다
// (제단 3D 모델의 콜라이더는 물리적으로 막는 용도로 그대로 둬야 해서 건드리지 않는다).
// 범위 안에서 상호작용 키를 누르면 '적 강화' 단계 조절 패널을 열고 닫는다.
public class StatAltar : MonoBehaviour
{
    [SerializeField] private Transform player; // 비워두면 PlayerHealth.Instance에서 자동으로 찾음
    [SerializeField] private TownController townController;
    [Tooltip("플레이어가 이 거리 안으로 들어오면 상호작용 힌트가 뜨고 키 입력을 받는다")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private Text hintText; // "E: 적 강화" 같은 안내 문구, 없어도 동작함

    private bool playerInRange;

    private void Start()
    {
        if (player == null && PlayerHealth.Instance != null)
        {
            player = PlayerHealth.Instance.transform;
        }

        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (player == null) return;

        bool inRange = Vector3.Distance(transform.position, player.position) <= interactDistance;
        if (inRange != playerInRange)
        {
            playerInRange = inRange;
            if (hintText != null) hintText.gameObject.SetActive(inRange);
        }

        // 일시정지/설정 등으로 게임이 멈춰있을 때는 상호작용 키 입력을 받지 않는다.
        if (Time.timeScale == 0f) return;

        if (playerInRange && Input.GetKeyDown(interactKey) && townController != null)
        {
            townController.ToggleUpgradeMenu();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
