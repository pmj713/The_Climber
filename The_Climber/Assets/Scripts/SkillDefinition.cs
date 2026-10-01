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

    [Header("1회 한정 획득")]
    [Tooltip("한 번 획득하면 이후 레벨업 선택지에 다시 나오지 않는다 (액티브 스킬, 속성부여 스킬용)")]
    public bool oneTimeOnly = false;

    [Header("선행 조건 (선택)")]
    [Tooltip("None이 아니면, prerequisiteLevel 이상일 때만 선택지에 나온다 (예: 화상폭발 -> 화상 Lv.1 이상)")]
    public KeywordType prerequisiteKeyword = KeywordType.BasicAttack;
    public int prerequisiteLevel = 0; // 0이면 조건 없음

    [Header("속성 부여 (1회 한정 스킬 전용)")]
    [Tooltip("이 스킬이 부여하는 상태이상 속성. 빙결/화상 중 하나를 고르면 반대 속성의 모든 속성부여 스킬은 이후 선택지에서 사라진다")]
    public ElementType grantsElement = ElementType.None;
    [Tooltip("grantsElement가 부여되는 대상 공격 수단")]
    public ElementSource elementSource = ElementSource.None;

    [Header("성장 스킬 전용 (반복 획득 가능)")]
    [Tooltip("키워드 레벨만으로 표현되지 않는 개별 수치 보너스. 같은 타입을 여러 번 획득하면 합산된다")]
    public CustomEffectType customEffect = CustomEffectType.None;
    public float customEffectValue = 0f;
}
