using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 플레이어가 마을을 자유롭게 돌아다니다가 StatAltar에서 업그레이드 패널을 열고,
// TowerEntrance에서 "탑 입장"을 하면 Floor1 씬으로 이동해서 실제 런이 시작된다.
public class TownController : MonoBehaviour
{
    private static readonly UpgradeType[] IndexToType = { UpgradeType.AttackPower, UpgradeType.MaxHealth, UpgradeType.MoveSpeed };

    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private GameObject upgradeMenuBackdrop; // 패널 뒤에 깔리는 전체화면 클릭 감지용, 없어도 동작함
    [SerializeField] private Text goldText;
    [SerializeField] private Text[] upgradeLabels; // 3개: 공격력, 생명력, 이동속도 순서

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

    private void Update()
    {
        if (upgradePanel != null && upgradePanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseUpgradeMenu();
        }
    }

    private void Start()
    {
        RefreshUI();

        if (PlayerGold.Instance != null)
        {
            PlayerGold.Instance.OnGoldChanged += HandleGoldChanged;
        }
    }

    private void OnDestroy()
    {
        if (PlayerGold.Instance != null)
        {
            PlayerGold.Instance.OnGoldChanged -= HandleGoldChanged;
        }
    }

    private void HandleGoldChanged(int gold)
    {
        RefreshUI();
    }

    // 업그레이드 버튼의 OnClick에서 호출 (0=공격력, 1=생명력, 2=이동속도)
    public void PurchaseUpgrade(int index)
    {
        if (index < 0 || index >= IndexToType.Length) return;
        if (PermanentUpgrades.Instance == null) return;

        PermanentUpgrades.Instance.TryPurchase(IndexToType[index]);
        ReapplyPermanentUpgrades();
        RefreshUI();
    }

    // 플레이어는 씬을 넘어가도 파괴되지 않아서(PersistentSceneObject) 각 스탯 스크립트의
    // Start()는 게임 세션 중 딱 한 번만 실행된다. 그래서 영구 업그레이드를 새로 사거나
    // 새 런을 시작할 때마다 여기서 직접 다시 적용해줘야 한다.
    private void ReapplyPermanentUpgrades()
    {
        PlayerHealth.Instance?.ApplyPermanentUpgrades();
        PlayerMovement.Instance?.ApplyPermanentUpgrades();

        if (PlayerHealth.Instance != null)
        {
            if (PlayerHealth.Instance.TryGetComponent<BowWeapon>(out var bow)) bow.ApplyPermanentUpgrades();
            if (PlayerHealth.Instance.TryGetComponent<SwordWeapon>(out var sword)) sword.ApplyPermanentUpgrades();
        }
    }

    // 스탯 제단(StatAltar)의 상호작용 키에서 호출. 업그레이드 패널을 열고 닫는다.
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

    public void EnterTower()
    {
        CloseUpgradeMenu();

        // 탑 = 런마다 초기화되는 임시 진행도. 마을의 영구 업그레이드는 그대로 둔다.
        PlayerLevel.Instance?.ResetProgress();
        PlayerSkillManager.Instance?.ResetSkills();
        PlayerActiveSkillSlots.Instance?.ResetSlots();
        ReapplyPermanentUpgrades(); // 그 사이에 산 영구 업그레이드가 이번 런에 확실히 반영되도록
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.ResetHealth();

        SceneManager.LoadScene("Floor1");
    }

    private void RefreshUI()
    {
        if (goldText != null)
        {
            int gold = PlayerGold.Instance != null ? PlayerGold.Instance.Gold : 0;
            goldText.text = $"보유 골드: {gold}G";
        }

        if (upgradeLabels == null || PermanentUpgrades.Instance == null) return;

        for (int i = 0; i < upgradeLabels.Length && i < IndexToType.Length; i++)
        {
            if (upgradeLabels[i] == null) continue;

            UpgradeType type = IndexToType[i];
            UpgradeDefinition def = PermanentUpgrades.Instance.GetDefinition(type);
            int level = PermanentUpgrades.Instance.GetLevel(type);
            int cost = PermanentUpgrades.Instance.GetNextCost(type);
            string name = def != null ? def.displayName : type.ToString();
            int percentPerLevel = def != null ? Mathf.RoundToInt(def.percentPerLevel * 100) : 0;

            upgradeLabels[i].text = $"{name} Lv.{level} (+{percentPerLevel}%/lv)\n다음 비용: {cost}G";
        }
    }
}
