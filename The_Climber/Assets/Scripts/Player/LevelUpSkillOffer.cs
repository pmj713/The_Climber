using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 레벨업 시 스킬 3개를 랜덤으로 뽑아 제시한다. 선택 UI가 생기기 전까지는
// 콘솔에 후보를 출력하고 1/2/3 키로 고르는 방식으로 테스트한다.
[RequireComponent(typeof(PlayerSkillManager))]
public class LevelUpSkillOffer : MonoBehaviour
{
    [SerializeField] private SkillDefinition[] skillPool;
    [SerializeField] private WeaponController weaponController; // 지금 장착한 무기에 맞는 스킬만 후보로 거르는 데 사용
    [SerializeField] private int choiceCount = 3;
    [SerializeField] private KeyCode pauseKey = KeyCode.G;
    [SerializeField] private Text promptText;
    [SerializeField] private float promptBlinkInterval = 0.5f;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Text[] choiceTexts;

    private PlayerSkillManager skillManager;
    private readonly List<SkillDefinition> currentChoices = new List<SkillDefinition>();
    private bool isPaused;
    private int pendingOffers; // 한꺼번에 여러 번 레벨업했을 때, 아직 고르지 않은 선택 횟수

    // 스킬 도감 창이 이 값을 확인해서, 레벨업 선택이 아직 안 끝났으면 시간을 다시 풀지 않는다.
    public bool IsWaitingForChoice => pendingOffers > 0;

    // 일시정지 메뉴가 이 값을 확인해서, 선택 패널이 실제로 화면에 떠 있을 때만 ESC를 양보받는다.
    // (G를 누르기 전, 즉 선택 대기 중이지만 패널이 아직 안 떠 있을 때는 ESC로 일시정지를 열 수 있어야 한다.)
    public bool IsChoicePanelOpen => isPaused;

    private void Awake()
    {
        skillManager = GetComponent<PlayerSkillManager>();
    }

    private void Start()
    {
        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.OnLevelUp += HandleLevelUp;
        }
        else
        {
            Debug.LogWarning("LevelUpSkillOffer: PlayerLevel.Instance를 찾을 수 없습니다.");
        }
    }

    private void OnDestroy()
    {
        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.OnLevelUp -= HandleLevelUp;
        }
    }

    private void Update()
    {
        UpdatePrompt();

        if (currentChoices.Count == 0) return;

        if (!isPaused)
        {
            // 선택지가 떠 있어도 실제로 고를 준비가 될 때까지는 게임이 계속 흘러간다.
            // 여유가 생기면 pauseKey를 눌러 멈추고 그때 고른다.
            if (Input.GetKeyDown(pauseKey))
            {
                Pause();
            }
            return;
        }

        for (int i = 0; i < currentChoices.Count; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                ChooseSkill(i);
                return;
            }
        }
    }

    private void UpdatePrompt()
    {
        if (promptText == null) return;

        bool shouldBlink = currentChoices.Count > 0 && !isPaused;
        // Time.timeScale이 0이 돼도 깜빡임 판정 자체는 unscaled 시간으로 계산 (일시정지 중엔 안 보이게 꺼둘 뿐)
        promptText.enabled = shouldBlink && Mathf.PingPong(Time.unscaledTime, promptBlinkInterval * 2f) < promptBlinkInterval;
    }

    private void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;
        ShowChoicePanel();
    }

    private void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (choicePanel != null) choicePanel.SetActive(false);
    }

    private void ShowChoicePanel()
    {
        if (choicePanel == null || choiceTexts == null) return;

        for (int i = 0; i < choiceTexts.Length; i++)
        {
            if (choiceTexts[i] == null) continue;

            if (i < currentChoices.Count)
            {
                SkillDefinition skill = currentChoices[i];
                string typeTag = skill.activeType == ActiveSkillType.None ? "(패시브)" : "(액티브)";
                choiceTexts[i].text = $"[{i + 1}] {skill.skillName} {typeTag} - {skill.description}";
                choiceTexts[i].gameObject.SetActive(true);
            }
            else
            {
                choiceTexts[i].gameObject.SetActive(false);
            }
        }

        choicePanel.SetActive(true);
    }

    private void HandleLevelUp(int newLevel)
    {
        // 한 번에 여러 레벨업이 몰려도(exp 몰빵 등) 선택 횟수를 큐처럼 쌓아둔다.
        // 이미 선택지가 떠 있는 중이면 여기서 덮어쓰지 않고, 다 고르고 나서 다음 걸 띄운다.
        pendingOffers++;

        if (currentChoices.Count == 0)
        {
            GenerateChoices(newLevel);
            if (isPaused) ShowChoicePanel();
        }
    }

    private void GenerateChoices(int newLevel)
    {
        currentChoices.Clear();

        if (skillPool == null || skillPool.Length == 0)
        {
            Debug.LogWarning("LevelUpSkillOffer: skillPool이 비어 있습니다.");
            pendingOffers = Mathf.Max(0, pendingOffers - 1);
            return;
        }

        List<SkillDefinition> available = new List<SkillDefinition>();
        foreach (SkillDefinition skill in skillPool)
        {
            if (skill != null && IsUsableWithCurrentWeapon(skill) && IsEligible(skill)) available.Add(skill);
        }

        if (available.Count == 0)
        {
            Debug.LogWarning("LevelUpSkillOffer: 지금 무기로 고를 수 있는 스킬이 없습니다.");
            pendingOffers = Mathf.Max(0, pendingOffers - 1);
            return;
        }

        List<SkillDefinition> shuffled = available;
        for (int i = 0; i < shuffled.Count; i++)
        {
            int swapIndex = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[i]);
        }

        int count = Mathf.Min(choiceCount, shuffled.Count);
        currentChoices.AddRange(shuffled.GetRange(0, count));

        Debug.Log($"Lv.{newLevel} 달성! 스킬을 선택하세요 (숫자 키): (남은 선택 {pendingOffers}회)");
        for (int i = 0; i < currentChoices.Count; i++)
        {
            Debug.Log($"  [{i + 1}] {currentChoices[i].skillName} - {currentChoices[i].description}");
        }
    }

    // 검을 들고 있으면 검 전용/공용 스킬만, 활을 들고 있으면 활 전용/공용 스킬만 후보로 남긴다.
    private bool IsUsableWithCurrentWeapon(SkillDefinition skill)
    {
        if (skill.weaponRequirement == WeaponRequirement.Any) return true;
        if (weaponController == null) return true; // 무기 컨트롤러를 못 찾으면 필터링하지 않는다

        return skill.weaponRequirement == WeaponRequirement.Sword
            ? weaponController.IsSwordEquipped
            : weaponController.IsBowEquipped;
    }

    // 1회 한정 스킬 중복 방지 / 선행 키워드 레벨 조건 / 빙결·화상 상호 배제를 확인한다.
    private bool IsEligible(SkillDefinition skill)
    {
        if (skill.oneTimeOnly && skillManager.HasAcquired(skill)) return false;

        if (skill.prerequisiteLevel > 0 &&
            skillManager.GetKeywordLevel(skill.prerequisiteKeyword) < skill.prerequisiteLevel) return false;

        if (skill.grantsElement != ElementType.None &&
            skillManager.LockedElement != ElementType.None &&
            skill.grantsElement != skillManager.LockedElement) return false;

        // 속성부여 스킬 자체가 아닌, 빙결/화상 키워드에 속한 "성장" 스킬은 해당 속성을
        // 먼저 선택하지 않은 이상 나오면 안 된다 (안 그러면 아직 못 거는 속성의 스택 증가 등이 뜬다).
        if (skill.grantsElement == ElementType.None)
        {
            if (HasKeyword(skill, KeywordType.Freeze) && skillManager.LockedElement != ElementType.Freeze) return false;
            if (HasKeyword(skill, KeywordType.Burn) && skillManager.LockedElement != ElementType.Burn) return false;
        }

        // 검기/힐윈드/화살비/부채살 전용 속성부여는 그 액티브 스킬 자체를 먼저 배워야 나온다
        // (예: 화살비를 아직 안 배웠으면 "화살비-속성부여"는 뜨지 않는다).
        if (skill.grantsElement != ElementType.None)
        {
            ActiveSkillType requiredActive = ToRequiredActiveType(skill.elementSource);
            if (requiredActive != ActiveSkillType.None && !skillManager.HasAcquiredActiveType(requiredActive)) return false;
        }

        return true;
    }

    private static ActiveSkillType ToRequiredActiveType(ElementSource source)
    {
        switch (source)
        {
            case ElementSource.SwordWave: return ActiveSkillType.SwordWave;
            case ElementSource.Whirlwind: return ActiveSkillType.Whirlwind;
            case ElementSource.ArrowRain: return ActiveSkillType.ArrowRain;
            case ElementSource.FanShot: return ActiveSkillType.FanShot;
            default: return ActiveSkillType.None;
        }
    }

    private static bool HasKeyword(SkillDefinition skill, KeywordType keyword)
    {
        if (skill.keywords == null) return false;
        foreach (KeywordType k in skill.keywords)
        {
            if (k == keyword) return true;
        }
        return false;
    }

    private void ChooseSkill(int index)
    {
        SkillDefinition chosen = currentChoices[index];
        currentChoices.Clear();
        skillManager.AcquireSkill(chosen);
        pendingOffers = Mathf.Max(0, pendingOffers - 1);

        if (pendingOffers > 0)
        {
            // 아직 고를 게 남아있으면 이어서 다음 선택지를 띄운다 (계속 일시정지 상태 유지).
            GenerateChoices(PlayerLevel.Instance != null ? PlayerLevel.Instance.Level : 0);
            if (isPaused) ShowChoicePanel();
        }
        else if (isPaused)
        {
            Resume();
        }
    }

    // 선택지 버튼의 OnClick에서 호출 (마우스 클릭 지원)
    public void OnChoiceButtonClicked(int index)
    {
        if (!isPaused) return;
        if (index < 0 || index >= currentChoices.Count) return;

        ChooseSkill(index);
    }
}
