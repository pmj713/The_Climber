using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 플레이어가 마을을 자유롭게 돌아다니다가 StatAltar에서 '적 강화' 패널을 열고,
// TowerEntrance에서 "탑 입장"을 하면 Floor1 씬으로 이동해서 실제 런이 시작된다.
public class TownController : MonoBehaviour
{
    private static readonly EnemyBoostType[] IndexToType = { EnemyBoostType.Health, EnemyBoostType.Damage, EnemyBoostType.MoveSpeed };

    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private GameObject upgradeMenuBackdrop; // 패널 뒤에 깔리는 전체화면 클릭 감지용, 없어도 동작함
    [FormerlySerializedAs("goldText")]
    [SerializeField] private Text summaryText;               // 적 강화 합계 표시
    [FormerlySerializedAs("upgradeLabels")]
    [SerializeField] private Text[] boostLabels;             // 3개: 적 체력, 적 공격력, 적 이동속도 순서

    // PauseMenuController가 ESC를 먼저 처리하는데, 이 패널이 열려있으면 그쪽이 양보하도록
    // 확인하는 용도로 사용한다.
    public bool IsUpgradeMenuOpen => upgradePanel != null && upgradePanel.activeSelf;

    private void Awake()
    {
        // 마을에서는 자유롭게 걸어다녀야 하므로 패널을 띄우지도, 게임을 멈추지도 않는다.
        if (upgradePanel != null) upgradePanel.SetActive(false);
        if (upgradeMenuBackdrop != null) upgradeMenuBackdrop.SetActive(false);
        Time.timeScale = 1f;
    }

    private void Start()
    {
        RefreshUI();
        EnemyEmpowerment.OnChanged += RefreshUI;
    }

    private void OnDestroy()
    {
        EnemyEmpowerment.OnChanged -= RefreshUI;
    }

    private void Update()
    {
        if (upgradePanel != null && upgradePanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseUpgradeMenu();
        }
    }

    // 각 줄의 [+] / [-] 버튼 OnClick에서 호출 (0=적 체력, 1=적 공격력, 2=적 이동속도)
    public void IncreaseBoost(int index) => ChangeBoost(index, +1);
    public void DecreaseBoost(int index) => ChangeBoost(index, -1);

    private void ChangeBoost(int index, int delta)
    {
        if (index < 0 || index >= IndexToType.Length) return;
        EnemyEmpowerment.ChangeLevel(IndexToType[index], delta);
    }

    // 제단(StatAltar)의 상호작용 키에서 호출. 적 강화 패널을 열고 닫는다.
    public void ToggleUpgradeMenu()
    {
        if (upgradePanel == null) return;

        if (upgradePanel.activeSelf)
        {
            CloseUpgradeMenu();
            return;
        }

        upgradePanel.SetActive(true);
        if (upgradeMenuBackdrop != null) upgradeMenuBackdrop.SetActive(true);
        Time.timeScale = 0f;
        RefreshUI();
    }

    // ESC 키 / 배경 클릭(UpgradeMenuBackdrop의 OnClick)에서 호출.
    public void CloseUpgradeMenu()
    {
        if (upgradePanel != null) upgradePanel.SetActive(false);
        if (upgradeMenuBackdrop != null) upgradeMenuBackdrop.SetActive(false);
        Time.timeScale = 1f;
    }

    // 탑 입구(TowerEntrance)에서 호출. 새 런을 시작하고 입구에 지정된 씬(보통 Floor1)으로 이동한다.
    public void EnterTower(string sceneName)
    {
        CloseUpgradeMenu();

        // 탑 = 런마다 초기화되는 임시 진행도. 제단의 적 강화 단계는 그대로 둔다.
        PlayerLevel.Instance?.ResetProgress();
        PlayerSkillManager.Instance?.ResetSkills();
        PlayerActiveSkillSlots.Instance?.ResetSlots();
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.ResetHealth();

        SceneManager.LoadScene(sceneName);
    }

    private void RefreshUI()
    {
        if (summaryText != null)
        {
            int total = 0;
            foreach (EnemyBoostType type in IndexToType) total += EnemyEmpowerment.GetLevel(type);
            summaryText.text = total > 0 ? $"적 강화 합계 {total}단계 — 탑의 모든 적이 강해집니다" : "적 강화 없음";
        }

        if (boostLabels == null) return;

        for (int i = 0; i < boostLabels.Length && i < IndexToType.Length; i++)
        {
            if (boostLabels[i] == null) continue;

            EnemyBoostType type = IndexToType[i];
            int level = EnemyEmpowerment.GetLevel(type);
            int percentPerLevel = Mathf.RoundToInt(EnemyEmpowerment.PercentPerLevel(type) * 100);
            boostLabels[i].text = $"{EnemyEmpowerment.DisplayName(type)} Lv.{level}/{EnemyEmpowerment.MaxLevel}\n" +
                                  $"현재 +{level * percentPerLevel}%  (단계당 +{percentPerLevel}%)";
        }
    }
}
