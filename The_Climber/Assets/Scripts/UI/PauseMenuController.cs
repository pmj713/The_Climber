using UnityEngine;
using UnityEngine.SceneManagement;

// ESC로 여닫는 일시정지 메뉴. SkillCodexUI/LevelUpSkillOffer보다 먼저 Update가 돌아야
// 같은 ESC 입력에 두 시스템이 동시에 반응하는 걸 막을 수 있다.
[DefaultExecutionOrder(-10)]
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private SkillCodexUI skillCodexUI;
    [SerializeField] private LevelUpSkillOffer levelUpOffer;
    [SerializeField] private TownController townController;
    [SerializeField] private GameObject returnToTownButton; // 탑(Floor1 등)에서만 보이고 마을에서는 숨김

    private bool isPaused;

    private void Awake()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (isPaused)
        {
            Resume();
            return;
        }

        // 보유 스킬 창/레벨업 선택/제단 업그레이드 패널이 열려있으면 그쪽이 ESC를 처리하도록 양보한다.
        if (skillCodexUI != null && skillCodexUI.IsOpen) return;
        if (levelUpOffer != null && levelUpOffer.IsChoicePanelOpen) return;
        if (townController != null && townController.IsUpgradeMenuOpen) return;

        Pause();
    }

    private void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);

        // 마을(Village)에서는 이미 마을이라 이 버튼이 필요 없으니 숨긴다.
        if (returnToTownButton != null)
        {
            returnToTownButton.SetActive(SceneManager.GetActiveScene().name != "Village");
        }
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // 일시정지 메뉴의 "마을로 돌아가기" 버튼에서 호출.
    public void ReturnToTown()
    {
        Resume();
        SceneManager.LoadScene("Village");
    }

    // 일시정지 메뉴의 "메인화면으로" 버튼에서 호출.
    public void ReturnToMainMenu()
    {
        Resume();
        SceneManager.LoadScene("MainMenu");
    }

    // 일시정지 메뉴의 "설정" 버튼에서 호출.
    public void OpenSettings()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    // 설정 패널의 "닫기" 버튼에서 호출.
    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
