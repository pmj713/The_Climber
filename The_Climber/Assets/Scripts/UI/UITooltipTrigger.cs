using UnityEngine;
using UnityEngine.EventSystems;

// Text/Image 등에 붙여서 커서를 올리면 SkillTooltip에 설명을 띄운다.
public class UITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SkillTooltip tooltip;
    public string tooltipText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip == null || string.IsNullOrEmpty(tooltipText)) return;
        tooltip.Show(tooltipText, eventData.position, eventData.pressEventCamera);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Hide();
    }
}
