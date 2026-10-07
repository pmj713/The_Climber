using System;
using System.Collections.Generic;
using UnityEngine;

// 키워드 = 성장 방향(예: 투사체 Lv.2), 스킬 = 그 키워드를 올려주는 개별 습득 요소.
// 실제 투사체 개수/화상 데미지 등 수치 적용은 각 무기/스킬 실행부(SwordWeapon, BowWeapon,
// ActiveSkillCaster, EnemyStatusEffects 등)가 GetKeywordLevel/GetCustomEffectTotal을 읽어서
// 스스로 처리한다 (여기서는 레벨과 획득 목록만 관리).
public class PlayerSkillManager : MonoBehaviour
{
    public static PlayerSkillManager Instance { get; private set; }

    private readonly Dictionary<KeywordType, int> keywordLevels = new Dictionary<KeywordType, int>();
    private readonly List<SkillDefinition> acquiredSkills = new List<SkillDefinition>();

    public IReadOnlyList<SkillDefinition> AcquiredSkills => acquiredSkills;

    // 빙결/화상 중 이번 런에서 먼저 선택한 속성. 반대 속성의 "◯◯-속성부여" 스킬은
    // 이 값이 정해지는 순간부터 레벨업 선택지에서 완전히 배제된다.
    public ElementType LockedElement { get; private set; } = ElementType.None;

    public event Action<KeywordType, int> OnKeywordLevelChanged;
    public event Action OnFirstActiveSkillAcquired;
    public event Action OnSkillsReset;

    private bool hasNotifiedActiveSkill;

    private void Awake()
    {
        // 마을을 다시 불러올 때 씬에 들어 있는 사본은 중복으로 파괴되므로, 이미 살아 있는 본체의 싱글톤을 덮어쓰지 않는다
        if (Instance == null || Instance == this) Instance = this;
    }

    // 새 런을 시작할 때(탑 입장) 호출. "첫 액티브 스킬 획득" 튜토리얼은 런과 무관하게
    // 한 번만 보여주면 되므로 hasNotifiedActiveSkill은 초기화하지 않는다.
    public void ResetSkills()
    {
        keywordLevels.Clear();
        acquiredSkills.Clear();
        LockedElement = ElementType.None;
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

    public bool HasAcquired(SkillDefinition skill) => CountAcquired(skill) > 0;

    // 특정 액티브 스킬(검기/힐윈드/화살비/부채살 등)을 이미 습득했는지. 속성부여 스킬이
    // 그 액티브를 아직 안 배운 상태에서 나오지 않도록 선행조건으로 쓰인다.
    public bool HasAcquiredActiveType(ActiveSkillType type)
    {
        if (type == ActiveSkillType.None) return false;
        foreach (SkillDefinition acquired in acquiredSkills)
        {
            if (acquired.activeType == type) return true;
        }
        return false;
    }

    // 특정 공격 수단(elementSource)이 지금 이 속성으로 공격하는지 (◯◯-속성부여 스킬 획득 여부)
    public bool HasElement(ElementSource source, ElementType element)
    {
        if (element == ElementType.None || source == ElementSource.None) return false;
        foreach (SkillDefinition acquired in acquiredSkills)
        {
            if (acquired.grantsElement == element && acquired.elementSource == source) return true;
        }
        return false;
    }

    // 키워드 레벨만으로 표현 안 되는 개별 수치 보너스 합산 (같은 타입을 여러 번 획득하면 누적)
    public float GetCustomEffectTotal(CustomEffectType type)
    {
        if (type == CustomEffectType.None) return 0f;
        float total = 0f;
        foreach (SkillDefinition acquired in acquiredSkills)
        {
            if (acquired.customEffect == type) total += acquired.customEffectValue;
        }
        return total;
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

        if (skill.grantsElement != ElementType.None && LockedElement == ElementType.None)
        {
            LockedElement = skill.grantsElement;
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
