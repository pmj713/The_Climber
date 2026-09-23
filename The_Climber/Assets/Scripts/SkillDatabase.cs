using UnityEngine;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Skills/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    public SkillDefinition[] allSkills;
}
