using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 스킬 도감의 보유 스킬 항목과 장착 슬롯 양쪽에 붙는 드래그 컴포넌트.
// sourceSlotIndex가 -1이면 "보유 목록"에서 시작한 드래그(놓으면 장착만 가능),
// 0 이상이면 "장착 슬롯"에서 시작한 드래그(슬롯 밖에 놓으면 해제).
public class DraggableSkillIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public SkillDefinition skill;
    public int sourceSlotIndex = -1;
    public PlayerActiveSkillSlots slotManager;
    public RectTransform dragGhost;

    private Canvas canvas;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (skill == null || dragGhost == null) return;

        Text ghostText = dragGhost.GetComponent<Text>();
        if (ghostText != null) ghostText.text = skill.skillName;

        dragGhost.gameObject.SetActive(true);
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (skill == null || dragGhost == null || canvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
        dragGhost.anchoredPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        if (skill == null || slotManager == null) return;

        GameObject hit = eventData.pointerCurrentRaycast.gameObject;
        SkillSlotMarker slotMarker = hit != null ? hit.GetComponentInParent<SkillSlotMarker>() : null;

        if (slotMarker != null)
        {
            slotManager.Equip(slotMarker.slotIndex, skill);
        }
        else if (sourceSlotIndex >= 0)
        {
            // 슬롯 밖 아무 곳에나 놓으면 해제
            slotManager.Unequip(sourceSlotIndex);
        }
    }
}
