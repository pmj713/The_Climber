using System;
using UnityEngine;

// 우클릭 / E / Q 세 개의 액티브 스킬 슬롯을 관리한다 (0: 우클릭, 1: E, 2: Q).
// 실제로 슬롯에 든 스킬을 "발동"시키는 로직은 별도 컨트롤러에서 이 슬롯 정보를 읽어 처리한다.
public class PlayerActiveSkillSlots : MonoBehaviour
{
    public const int SlotCount = 3;

    public static PlayerActiveSkillSlots Instance { get; private set; }

    private readonly SkillDefinition[] slots = new SkillDefinition[SlotCount];

    public event Action OnSlotsChanged;

    private void Awake()
    {
        Instance = this;
    }

    // 새 런을 시작할 때(탑 입장) 호출. 이전 런에서 장착했던 액티브 스킬을 비운다.
    public void ResetSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = null;
        }
        OnSlotsChanged?.Invoke();
    }

    public SkillDefinition GetSlot(int index)
    {
        return (index >= 0 && index < slots.Length) ? slots[index] : null;
    }

    public void Equip(int index, SkillDefinition skill)
    {
        if (index < 0 || index >= slots.Length) return;
        if (skill != null && skill.activeType == ActiveSkillType.None) return; // 액티브 스킬만 장착 가능

        // 같은 스킬이 다른 슬롯에 이미 장착되어 있으면 그쪽은 비워서 중복 장착을 막는다
        if (skill != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (i != index && slots[i] == skill) slots[i] = null;
            }
        }

        slots[index] = skill;
        OnSlotsChanged?.Invoke();
    }

    public void Unequip(int index)
    {
        if (index < 0 || index >= slots.Length) return;

        slots[index] = null;
        OnSlotsChanged?.Invoke();
    }
}
