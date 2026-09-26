using UnityEngine;
using UnityEngine.UI;

// 항상 화면에 떠 있는 우클릭/E/Q 슬롯 쿨타임 표시줄
public class SkillHotbarUI : MonoBehaviour
{
    private static readonly RebindableAction[] SlotActions =
        { RebindableAction.SkillRightClick, RebindableAction.SkillE, RebindableAction.SkillQ };

    [SerializeField] private PlayerActiveSkillSlots slotManager;
    [SerializeField] private ActiveSkillCaster caster;
    [SerializeField] private Text[] slotTexts;
    [SerializeField] private Color readyColor = Color.white;
    [SerializeField] private Color cooldownColor = new Color(1f, 0.4f, 0.4f, 1f);

    [Header("회피")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private GameObject dodgeIndicatorRoot;
    [SerializeField] private Text dodgeText;

    private void Update()
    {
        UpdateDodgeIndicator();

        if (slotManager == null || caster == null || slotTexts == null) return;

        for (int i = 0; i < slotTexts.Length; i++)
        {
            if (slotTexts[i] == null) continue;

            string name = i < SlotActions.Length ? KeyBindingManager.GetKey(SlotActions[i]).ToString() : $"슬롯{i + 1}";
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

    // 회피(스페이스)는 슬롯에 장착하는 스킬이 아니라 쿨타임 중일 때만 단축키 위에 표시한다.
    private void UpdateDodgeIndicator()
    {
        if (movement == null || dodgeIndicatorRoot == null) return;

        float remaining = movement.GetDodgeCooldownRemaining();
        bool onCooldown = remaining > 0f;
        dodgeIndicatorRoot.SetActive(onCooldown);

        if (onCooldown && dodgeText != null)
        {
            dodgeText.text = $"회피\n{remaining:F1}s";
            dodgeText.color = cooldownColor;
        }
    }
}
