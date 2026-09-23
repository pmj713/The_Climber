using UnityEngine;
using UnityEngine.UI;

// 항상 화면에 떠 있는 우클릭/E/Q 슬롯 쿨타임 표시줄
public class SkillHotbarUI : MonoBehaviour
{
    private static readonly string[] SlotNames = { "우클릭", "E", "Q" };

    [SerializeField] private PlayerActiveSkillSlots slotManager;
    [SerializeField] private ActiveSkillCaster caster;
    [SerializeField] private Text[] slotTexts;
    [SerializeField] private Color readyColor = Color.white;
    [SerializeField] private Color cooldownColor = new Color(1f, 0.4f, 0.4f, 1f);

    private void Update()
    {
        if (slotManager == null || caster == null || slotTexts == null) return;

        for (int i = 0; i < slotTexts.Length; i++)
        {
            if (slotTexts[i] == null) continue;

            string name = i < SlotNames.Length ? SlotNames[i] : $"슬롯{i + 1}";
            SkillDefinition skill = slotManager.GetSlot(i);

            if (skill == null)
            {
                slotTexts[i].text = $"{name}\nI로 장착";
                slotTexts[i].color = readyColor;
                continue;
            }

            float remaining = caster.GetCooldownRemaining(i);
            if (remaining > 0f)
            {
                slotTexts[i].text = $"{name}\n{skill.skillName}\n{remaining:F1}s";
                slotTexts[i].color = cooldownColor;
            }
            else
            {
                slotTexts[i].text = $"{name}\n{skill.skillName}\n준비";
                slotTexts[i].color = readyColor;
            }
        }
    }
}
