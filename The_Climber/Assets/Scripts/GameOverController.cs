using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 플레이어 사망 시 게임을 멈추고 결과 화면을 띄운다. 마을/영구 업그레이드가 생기기 전까지는
// "다시 시작"으로 같은 씬을 재시작하는 것으로 런을 마무리한다.
public class GameOverController : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text resultText;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private ActiveSkillCaster activeSkillCaster;

    private void Awake()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void Start()
    {
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnDeath += HandleDeath;
        }
        else
        {
            Debug.LogWarning("GameOverController: PlayerHealth.Instance를 찾을 수 없습니다.");
        }
    }

    private void OnDestroy()
    {
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnDeath -= HandleDeath;
        }
    }

    private void HandleDeath()
    {
        Time.timeScale = 0f;

        if (playerMovement != null) playerMovement.enabled = false;
        if (weaponController != null) weaponController.enabled = false;
        if (activeSkillCaster != null) activeSkillCaster.enabled = false;

        if (resultText != null)
        {
            int floor = FloorManager.Instance != null ? FloorManager.Instance.CurrentFloorNumber : 1;
            int level = PlayerLevel.Instance != null ? PlayerLevel.Instance.Level : 1;
            resultText.text = $"{floor}층에서 쓰러졌습니다\nLv.{level}";
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    public void RestartRun()
    {
        Time.timeScale = 1f;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (playerMovement != null) playerMovement.enabled = true;
        if (weaponController != null) weaponController.enabled = true;
        if (activeSkillCaster != null) activeSkillCaster.enabled = true;
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.ResetHealth();

        // 플레이어/HUD는 PersistentSceneObject로 씬 전환에도 유지되므로,
        // 죽은 층이 아니라 항상 마을(Village)로 돌아간다.
        SceneManager.LoadScene("Village");
    }
}
