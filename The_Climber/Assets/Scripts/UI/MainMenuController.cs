using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 메인 화면: 시작/설정/게임종료. 시작을 누르면 세이브 칸 3개가 뜨고,
// 칸을 고르면 그 칸을 현재 세이브로 지정한 뒤 마을(Village)로 들어간다.
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject mainButtonsPanel;
    [SerializeField] private GameObject saveSlotPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Text[] slotLabels; // SaveSlotManager.SlotCount(3)개

    private void Awake()
    {
        // 일시정지 중에 게임을 종료했다가 다시 들어온 경우를 대비해 항상 정상 속도로 시작한다.
        Time.timeScale = 1f;
        SaveSlotManager.PurgeLegacyData(); // 골드/플레이어 영구 강화는 제거된 기능이라 남은 저장 값을 지운다
    }

    private void Start()
    {
        ShowMainButtons();
    }

    public void ShowMainButtons()
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
        if (saveSlotPanel != null) saveSlotPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OpenStart()
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (saveSlotPanel != null) saveSlotPanel.SetActive(true);
        RefreshSlotLabels();
    }

    public void OpenSettings()
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 세이브 칸 버튼(0/1/2)의 OnClick에서 호출.
    public void SelectSlot(int slot)
    {
        SaveSlotManager.SelectSlot(slot);
        SceneManager.LoadScene("Village");
    }

    private void RefreshSlotLabels()
    {
        if (slotLabels == null) return;

        for (int i = 0; i < slotLabels.Length; i++)
        {
            if (slotLabels[i] == null) continue;

            if (SaveSlotManager.SlotExists(i))
            {
                int boostLevels = SaveSlotManager.GetTotalEnemyBoostLevels(i);
                slotLabels[i].text = $"세이브 {i + 1}\n적 강화 합계 {boostLevels}단계";
            }
            else
            {
                slotLabels[i].text = $"세이브 {i + 1}\n(비어있음)\n새 게임 시작";
            }
        }
    }
}
