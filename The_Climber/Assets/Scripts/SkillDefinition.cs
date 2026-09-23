using UnityEngine;

public enum WeaponRequirement
{
    Any,   // 검/활 상관없이 항상 후보로 나옴
    Sword,
    Bow
}

[CreateAssetMenu(fileName = "NewSkill", menuName = "Skills/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;

    // 이 스킬을 획득하면 여기 적힌 키워드들이 각각 1단계씩 오른다 (예: 검기 -> 투사체, 관통)
    public KeywordType[] keywords;

    // 레벨업 선택지에 이 스킬이 나오려면 지금 장착한 무기가 이 조건을 만족해야 한다
    public WeaponRequirement weaponRequirement = WeaponRequirement.Any;

    // None이 아니면 우클릭/E/Q 슬롯에 장착해서 직접 발동시킬 수 있는 액티브 스킬
    public ActiveSkillType activeType = ActiveSkillType.None;
    public float cooldown = 5f;
}
