using System;
using System.Collections.Generic;
using UnityEngine;

// 키워드 = 성장 방향(예: 관통 Lv.2), 스킬 = 그 키워드를 올려주는 개별 습득 요소.
// 실제 관통 개수/투사체 개수 등 수치 적용은 각 무기(SwordWeapon, BowWeapon 등)가
// GetKeywordLevel(KeywordType)을 읽어서 스스로 처리한다 (여기서는 레벨만 관리).
public class PlayerSkillManager : MonoBehaviour
{
    public static PlayerSkillManager Instance { get; private set; }

    private readonly Dictionary<KeywordType, int> keywordLevels = new Dictionary<KeywordType, int>();
    private readonly List<SkillDefinition> acquiredSkills = new List<SkillDefinition>();

    public IReadOnlyList<SkillDefinition> AcquiredSkills => acquiredSkills;

    public event Action<KeywordType, int> OnKeywordLevelChanged;
    public event Action OnFirstActiveSkillAcquired;
    public event Action OnSkillsReset;

    private bool hasNotifiedActiveSkill;

    private void Awake()
    {
        Instance = this;
    }

    // 새 런을 시작할 때(탑 입장) 호출. "첫 액티브 스킬 획득" 튜토리얼은 런과 무관하게
    // 한 번만 보여주면 되므로 hasNotifiedActiveSkill은 초기화하지 않는다.
    public void ResetSkills()
    {
        keywordLevels.Clear();
        acquiredSkills.Clear();
        OnSkillsReset?.Invoke();
    }

    public int GetKeywordLevel(KeywordType keyword)
    {
        return keywordLevels.TryGetValue(keyword, out int level) ? level : 0;
    }

    // 특정 스킬을 몇 번 획득했는지 (액티브 스킬 레벨, 도감 표시 등에 사용)
    public int CountAcquired(SkillDefinition skill)
    {
        int count = 0;
        foreach (SkillDefinition acquired in acquiredSkills)
        {
            if (acquired == skill) count++;
        }
        return count;
    }

    public void AcquireSkill(SkillDefinition skill)
    {
        if (skill == null) return;

        acquiredSkills.Add(skill);
        Debug.Log($"스킬 획득: {skill.skillName}");

        if (!hasNotifiedActiveSkill && skill.activeType != ActiveSkillType.None)
        {
            hasNotifiedActiveSkill = true;
            OnFirstActiveSkillAcquired?.Invoke();
        }

        foreach (KeywordType keyword in skill.keywords)
        {
            AddKeywordLevel(keyword);
        }
    }

    private void AddKeywordLevel(KeywordType keyword, int amount = 1)
    {
        int newLevel = GetKeywordLevel(keyword) + amount;
        keywordLevels[keyword] = newLevel;
        Debug.Log($"[Keyword] {keyword} -> Lv.{newLevel}");
        OnKeywordLevelChanged?.Invoke(keyword, newLevel);
    }
}
