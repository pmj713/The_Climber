using UnityEngine;
using UnityEngine.UI;

public class SkillCodexUI : MonoBehaviour
{
    private static readonly string[] SlotNames = { "우클릭", "E", "Q" };

    [SerializeField] private SkillDatabase database;
    [SerializeField] private PlayerSkillManager skillManager;
    [SerializeField] private PlayerActiveSkillSlots slotManager;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Text[] entryLabels;
    [SerializeField] private Image[] slotBackgrounds;
    [SerializeField] private Text[] slotLabels;
    [SerializeField] private RectTransform dragGhost;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;
    [SerializeField] private Color acquiredColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(1f, 1f, 1f, 0.4f);
    [SerializeField] private LevelUpSkillOffer levelUpOffer;

    private bool isOpen;
    public bool IsOpen => isOpen;
    private DraggableSkillIcon[] entryDragIcons;
    private DraggableSkillIcon[] slotDragIcons;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (dragGhost != null) dragGhost.gameObject.SetActive(false);

        SetupEntryDragIcons();
        SetupSlotDragIcons();
    }

    private void OnEnable()
    {
        if (slotManager != null) slotManager.OnSlotsChanged += RefreshSlots;
    }

    private void OnDisable()
    {
        if (slotManager != null) slotManager.OnSlotsChanged -= RefreshSlots;
    }

    private void SetupEntryDragIcons()
    {
        if (entryLabels == null) return;

        entryDragIcons = new DraggableSkillIcon[entryLabels.Length];
        for (int i = 0; i < entryLabels.Length; i++)
        {
            if (entryLabels[i] == null) continue;

            entryLabels[i].raycastTarget = true;
            DraggableSkillIcon icon = entryLabels[i].gameObject.AddComponent<DraggableSkillIcon>();
            icon.slotManager = slotManager;
            icon.dragGhost = dragGhost;
            icon.sourceSlotIndex = -1;
            entryDragIcons[i] = icon;
        }
    }

    private void SetupSlotDragIcons()
    {
        if (slotBackgrounds == null) return;

        slotDragIcons = new DraggableSkillIcon[slotBackgrounds.Length];
        for (int i = 0; i < slotBackgrounds.Length; i++)
        {
            if (slotBackgrounds[i] == null) continue;

            slotBackgrounds[i].raycastTarget = true;

            SkillSlotMarker marker = slotBackgrounds[i].gameObject.AddComponent<SkillSlotMarker>();
            marker.slotIndex = i;

            DraggableSkillIcon icon = slotBackgrounds[i].gameObject.AddComponent<DraggableSkillIcon>();
            icon.slotManager = slotManager;
            icon.dragGhost = dragGhost;
            icon.sourceSlotIndex = i;
            slotDragIcons[i] = icon;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetOpen(!isOpen);
        }
        else if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            SetOpen(false);
        }
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        if (panelRoot != null) panelRoot.SetActive(open);
        if (open) Refresh();

        // 드래그 도중에 패널이 닫히면 OnEndDrag가 못 불려서 고스트가 화면에 남을 수 있어 안전하게 같이 꺼준다.
        if (!open && dragGhost != null) dragGhost.gameObject.SetActive(false);

        if (open)
        {
            Time.timeScale = 0f;
        }
        else if (levelUpOffer == null || !levelUpOffer.IsWaitingForChoice)
        {
            // 레벨업 선택이 아직 안 끝난 상태라면 그쪽이 다시 풀어줄 때까지 멈춰있는다.
            Time.timeScale = 1f;
        }
    }

    private void Refresh()
    {
        if (database == null || database.allSkills == null || entryLabels == null) return;

        for (int i = 0; i < entryLabels.Length; i++)
        {
            if (entryLabels[i] == null) continue;

            if (i < database.allSkills.Length && database.allSkills[i] != null)
            {
                SkillDefinition skill = database.allSkills[i];
                int count = CountAcquired(skill);
                string countText = count > 0 ? $" (획득 x{count})" : "";
                string typeTag = skill.activeType == ActiveSkillType.None ? "(패시브)" : "(액티브)";
                entryLabels[i].text = $"{skill.skillName} {typeTag}{countText} - {skill.description}";
                entryLabels[i].color = count > 0 ? acquiredColor : lockedColor;
                entryLabels[i].gameObject.SetActive(true);

                if (entryDragIcons != null && entryDragIcons[i] != null)
                {
                    entryDragIcons[i].skill = count > 0 ? skill : null;
                }
            }
            else
            {
                entryLabels[i].gameObject.SetActive(false);
            }
        }

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (slotLabels == null || slotManager == null) return;

        for (int i = 0; i < slotLabels.Length; i++)
        {
            if (slotLabels[i] == null) continue;

            SkillDefinition equipped = slotManager.GetSlot(i);
            string slotName = i < SlotNames.Length ? SlotNames[i] : $"슬롯{i + 1}";
            slotLabels[i].text = equipped != null ? $"{slotName}\n{equipped.skillName}" : $"{slotName}\n(비어있음)";

            if (slotDragIcons != null && slotDragIcons[i] != null)
            {
                slotDragIcons[i].skill = equipped;
            }
        }
    }

    private int CountAcquired(SkillDefinition skill)
    {
        return skillManager != null ? skillManager.CountAcquired(skill) : 0;
    }
}
