using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillCodexUI : MonoBehaviour
{
    private static readonly RebindableAction[] SlotActions =
        { RebindableAction.SkillRightClick, RebindableAction.SkillE, RebindableAction.SkillQ };

    private static readonly KeywordType[] AllKeywords =
    {
        KeywordType.BasicAttack, KeywordType.Projectile, KeywordType.Freeze,
        KeywordType.Burn, KeywordType.Area, KeywordType.Buff
    };

    [SerializeField] private SkillDatabase database;
    [SerializeField] private PlayerSkillManager skillManager;
    [SerializeField] private PlayerActiveSkillSlots slotManager;
    [SerializeField] private GameObject panelRoot;
    [Tooltip("예전 고정 8슬롯 라벨. 더 이상 쓰지 않고 자동으로 숨긴다 (지워도 무방).")]
    [SerializeField] private Text[] entryLabels;
    [SerializeField] private Image[] slotBackgrounds;
    [SerializeField] private Text[] slotLabels;
    [SerializeField] private RectTransform dragGhost;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;
    [SerializeField] private Color acquiredColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(1f, 1f, 1f, 0.4f);
    [SerializeField] private LevelUpSkillOffer levelUpOffer;

    [Header("보유 스킬 패널 자동 생성 설정")]
    [SerializeField] private Vector2 panelSize = new Vector2(1750f, 880f);
    [SerializeField] private int rowFontSize = 22;
    [SerializeField] private int headerFontSize = 24;
    [SerializeField] private int keywordChipFontSize = 22;
    [SerializeField] private int panelTitleFontSize = 26;
    [SerializeField] private float panelTitleRowHeight = 46f;
    [SerializeField] private float keywordRowHeight = 60f;
    [SerializeField] private float slotBarHeight = 100f;

    [Header("패널이 열려있는 동안 숨길 다른 HUD 요소 (이름으로 자동 탐색)")]
    [SerializeField]
    private string[] hudElementNamesToHide =
        { "SkillCodexHint", "HotbarSlot0_RightClick", "HotbarSlot1_E", "HotbarSlot2_Q", "HotbarSlot_Dodge" };

    private bool isOpen;
    public bool IsOpen => isOpen;
    private DraggableSkillIcon[] slotDragIcons;

    private RectTransform generatedRoot;
    private Text[] keywordChipLabels;
    private SkillTooltip tooltip;
    private Font sourceFont;
    private bool slotBarRelocated;
    private readonly List<GameObject> hudElementsToHide = new List<GameObject>();
    private bool hudElementsCached;
    private GridLayoutGroup sectionGridLayout;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (dragGhost != null) dragGhost.gameObject.SetActive(false);

        HideLegacyEntryLabels();
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

    private void HideLegacyEntryLabels()
    {
        Transform legacyTitle = panelRoot != null ? panelRoot.transform.Find("Title") : null;
        if (legacyTitle != null) legacyTitle.gameObject.SetActive(false);
        if (entryLabels == null) return;
        foreach (Text label in entryLabels)
        {
            if (label != null) label.gameObject.SetActive(false);
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
        if (!open && tooltip != null) tooltip.Hide();

        // 패널이 열려있는 동안에는 겹치는 다른 HUD(코덱스 힌트, 스킬 쿨타임 핫바)를 숨긴다.
        foreach (GameObject go in hudElementsToHide)
        {
            if (go != null) go.SetActive(!open);
        }

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
        if (database == null || database.allSkills == null || panelRoot == null) return;

        EnsureBuilt();
        UpdateGridCellSize();
        RebuildKeywordRow();
        RebuildSections();
        RefreshSlots();

        // 방금 새로 만든 Text/LayoutGroup들은 다음 레이아웃 패스가 돌기 전까지 크기가 0으로
        // 잡혀 있어서(그래서 안 보임) 즉시 강제로 한 번 레이아웃을 계산시킨다.
        Canvas.ForceUpdateCanvases();
    }

    // ---------------- 자동 UI 생성 ----------------

    private void EnsureBuilt()
    {
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect != null) panelRect.sizeDelta = panelSize;

        if (sourceFont == null)
        {
            Text anyText = panelRoot.GetComponentInChildren<Text>(true);
            sourceFont = anyText != null ? anyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
        if (canvas != null && tooltip == null)
        {
            tooltip = SkillTooltip.GetOrCreate(canvas.transform, sourceFont);
        }

        if (!hudElementsCached && canvas != null)
        {
            hudElementsCached = true;
            foreach (string elementName in hudElementNamesToHide)
            {
                Transform found = canvas.transform.Find(elementName);
                if (found != null) hudElementsToHide.Add(found.gameObject);
            }
        }

        if (!slotBarRelocated)
        {
            RelocateSlotBar();
            slotBarRelocated = true;
        }

        if (generatedRoot != null) return;

        GameObject rootGo = new GameObject("GeneratedContent", typeof(RectTransform));
        generatedRoot = rootGo.GetComponent<RectTransform>();
        generatedRoot.SetParent(panelRoot.transform, false);
        generatedRoot.anchorMin = Vector2.zero;
        generatedRoot.anchorMax = Vector2.one;
        generatedRoot.offsetMin = new Vector2(18f, 18f);
        generatedRoot.offsetMax = new Vector2(-18f, -18f);

        const float rowGap = 10f;

        // 맨 위: 패널 제목 ("보유 스킬"). 키워드 바와 완전히 다른 행에 둔다.
        GameObject titleGo = CreateRow(generatedRoot, "PanelTitle", out Text titleText, panelTitleFontSize, TextAnchor.MiddleCenter);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(0f, panelTitleRowHeight);
        titleText.text = "보유 스킬";
        titleText.fontStyle = FontStyle.Bold;
        titleText.raycastTarget = false;

        // 그 아래: 키워드 가로 바 (고정 높이, 제목과 별도 행)
        GameObject rowGo = new GameObject("KeywordRow", typeof(RectTransform));
        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.SetParent(generatedRoot, false);
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -panelTitleRowHeight);
        rowRect.sizeDelta = new Vector2(0f, keywordRowHeight);

        HorizontalLayoutGroup rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;

        keywordChipLabels = new Text[AllKeywords.Length];
        for (int i = 0; i < AllKeywords.Length; i++)
        {
            GameObject chip = CreateRow(rowRect, $"Chip_{AllKeywords[i]}", out Text chipLabel, keywordChipFontSize, TextAnchor.MiddleCenter);
            chipLabel.fontStyle = FontStyle.Bold;
            keywordChipLabels[i] = chipLabel;

            UITooltipTrigger trigger = chip.AddComponent<UITooltipTrigger>();
            trigger.tooltip = tooltip;
        }

        // 아래: 키워드별 섹션 그리드. 위의 키워드 바와 열이 1:1로 맞도록 키워드 개수만큼 한 줄로 배치하고,
        // 맨 아래는 장착 슬롯 바 자리만큼 비워둔다.
        GameObject gridGo = new GameObject("SectionGrid", typeof(RectTransform));
        RectTransform gridRect = gridGo.GetComponent<RectTransform>();
        gridRect.SetParent(generatedRoot, false);
        gridRect.anchorMin = new Vector2(0f, 0f);
        gridRect.anchorMax = new Vector2(1f, 1f);
        gridRect.offsetMax = new Vector2(0f, -(panelTitleRowHeight + keywordRowHeight + rowGap));
        gridRect.offsetMin = new Vector2(0f, slotBarHeight);
        gridGo.name = "SectionGrid";

        GridLayoutGroup grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.spacing = new Vector2(rowGap, rowGap);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = AllKeywords.Length;
        grid.childAlignment = TextAnchor.UpperLeft;

        this.sectionGrid = gridRect;
        this.sectionGridLayout = grid;
    }

    // 그리드 셀 크기는 매번 다시 계산한다 (Play 진입 직전/직후 해상도가 바뀌는 순간에 패널을
    // 처음 열면 그 시점의 잘못된 크기가 영구히 굳어버리는 문제를 피하기 위해).
    private void UpdateGridCellSize()
    {
        if (sectionGrid == null || sectionGridLayout == null) return;

        const float rowGap = 10f;
        float cellWidth = (sectionGrid.rect.width - rowGap * (AllKeywords.Length - 1)) / AllKeywords.Length;
        float cellHeight = sectionGrid.rect.height;
        if (cellWidth <= 1f || cellHeight <= 1f) return; // 아직 레이아웃이 안 잡힌 프레임이면 이전 값을 유지한다

        sectionGridLayout.cellSize = new Vector2(Mathf.Max(80f, cellWidth), cellHeight);
    }

    // 원래 씬에 있던 "장착 슬롯(우클릭/E/Q)" UI는 예전 작은 패널 기준으로 배치돼 있어서,
    // 패널을 키우면 새로 생성한 콘텐츠와 겹친다. 패널 하단의 고정 바 영역으로 옮겨준다.
    private void RelocateSlotBar()
    {
        Transform header = panelRoot.transform.Find("SlotsHeader");
        if (header != null && header.TryGetComponent<RectTransform>(out var headerRect))
        {
            headerRect.anchorMin = headerRect.anchorMax = new Vector2(0.5f, 0f);
            headerRect.pivot = new Vector2(0.5f, 0f);
            headerRect.anchoredPosition = new Vector2(0f, slotBarHeight - 26f);
        }

        if (slotBackgrounds == null) return;

        const float slotWidth = 200f;
        const float slotSpacing = 30f;
        float totalWidth = slotBackgrounds.Length * slotWidth + (slotBackgrounds.Length - 1) * slotSpacing;
        float startX = (panelSize.x - totalWidth) / 2f;

        for (int i = 0; i < slotBackgrounds.Length; i++)
        {
            if (slotBackgrounds[i] == null) continue;

            RectTransform slotRect = slotBackgrounds[i].rectTransform;
            slotRect.anchorMin = slotRect.anchorMax = new Vector2(0f, 0f);
            slotRect.pivot = new Vector2(0f, 0f);
            slotRect.anchoredPosition = new Vector2(startX + i * (slotWidth + slotSpacing), 10f);
        }
    }

    private RectTransform sectionGrid;

    private GameObject CreateRow(Transform parent, string name, out Text text, int fontSize, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        text = go.AddComponent<Text>();
        text.font = sourceFont;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        return go;
    }

    private void RebuildKeywordRow()
    {
        if (keywordChipLabels == null || skillManager == null) return;

        for (int i = 0; i < AllKeywords.Length; i++)
        {
            KeywordType keyword = AllKeywords[i];
            int level = skillManager.GetKeywordLevel(keyword);
            keywordChipLabels[i].text = $"{KeywordInfo.GetDisplayName(keyword)}\nLv.{level}";

            UITooltipTrigger trigger = keywordChipLabels[i].GetComponent<UITooltipTrigger>();
            if (trigger != null) trigger.tooltipText = $"[{KeywordInfo.GetDisplayName(keyword)} Lv.{level}]\n{KeywordInfo.GetDescription(keyword)}";
        }

        float height = keywordRowHeight;
        foreach (Text label in keywordChipLabels)
            height = Mathf.Max(height, Mathf.Ceil(label.preferredHeight) + 4f);
        ((RectTransform)keywordChipLabels[0].transform.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        sectionGrid.offsetMax = new Vector2(0f, -(panelTitleRowHeight + height + 10f));
        UpdateGridCellSize();
    }

    private void RebuildSections()
    {
        if (sectionGrid == null) return;

        for (int i = sectionGrid.childCount - 1; i >= 0; i--)
        {
            GameObject oldSection = sectionGrid.GetChild(i).gameObject;
            oldSection.SetActive(false);
            Destroy(oldSection);
        }

        // 키워드 태그가 없는 액티브 전용 스킬(검기/힐윈드/부채살/화상폭발)은 일반공격 섹션 밑에 붙여준다.
        var activeOnlySkills = new List<SkillDefinition>();
        foreach (SkillDefinition skill in database.allSkills)
        {
            if (skill != null && (skill.keywords == null || skill.keywords.Length == 0)) activeOnlySkills.Add(skill);
        }

        foreach (KeywordType keyword in AllKeywords)
        {
            var skillsForKeyword = new List<SkillDefinition>();
            foreach (SkillDefinition skill in database.allSkills)
            {
                if (skill == null) continue;
                if (ContainsKeyword(skill, keyword)) skillsForKeyword.Add(skill);
            }

            List<SkillDefinition> extra = keyword == KeywordType.BasicAttack ? activeOnlySkills : null;
            BuildSection(KeywordInfo.GetDisplayName(keyword), null, skillsForKeyword, extra);
        }
    }

    private bool ContainsKeyword(SkillDefinition skill, KeywordType keyword)
    {
        if (skill.keywords == null) return false;
        foreach (KeywordType k in skill.keywords)
        {
            if (k == keyword) return true;
        }
        return false;
    }

    // 중첩된 LayoutGroup(그리드 셀 안에 또 VerticalLayoutGroup)이 생성 직후 리빌드 타이밍 문제로
    // 자식 크기를 못 잡는 경우가 있어서, 이 단계는 LayoutGroup 없이 직접 좌표를 계산해 배치한다.
    private void BuildSection(string title, string sectionTooltip, List<SkillDefinition> skills, List<SkillDefinition> extraSkillsBelowGap = null)
    {
        GameObject sectionGo = new GameObject($"Section_{title}", typeof(RectTransform));
        RectTransform sectionRect = sectionGo.GetComponent<RectTransform>();
        sectionRect.SetParent(sectionGrid, false);
        sectionRect.sizeDelta = sectionGridLayout.cellSize;

        Image sectionBg = sectionGo.AddComponent<Image>();
        sectionBg.color = new Color(1f, 1f, 1f, 0.05f);

        const float padding = 8f;
        const float spacing = 4f;
        float cursorY = -padding;

        GameObject headerGo = CreateRow(sectionRect, "Header", out Text headerText, headerFontSize, TextAnchor.MiddleLeft);
        headerText.text = title;
        headerText.fontStyle = FontStyle.Bold;
        PlaceRow(headerGo.GetComponent<RectTransform>(), padding, headerFontSize + 10, spacing, ref cursorY);

        if (!string.IsNullOrEmpty(sectionTooltip))
        {
            UITooltipTrigger headerTrigger = headerGo.AddComponent<UITooltipTrigger>();
            headerTrigger.tooltip = tooltip;
            headerTrigger.tooltipText = sectionTooltip;
        }

        foreach (SkillDefinition skill in skills)
        {
            BuildSkillRow(sectionRect, skill, padding, spacing, ref cursorY);
        }

        if (extraSkillsBelowGap != null && extraSkillsBelowGap.Count > 0)
        {
            cursorY -= 12f;

            foreach (SkillDefinition skill in extraSkillsBelowGap)
            {
                BuildSkillRow(sectionRect, skill, padding, spacing, ref cursorY);
            }
        }
    }

    // 위에서부터 차곡차곡 쌓아 내려가며 한 행을 배치한다 (좌우로 꽉 채우고, 높이는 고정).
    private void PlaceRow(RectTransform rect, float horizontalPadding, float height, float spacing, ref float cursorY)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, cursorY);
        rect.sizeDelta = new Vector2(-2f * horizontalPadding, height);
        cursorY -= height + spacing;
    }

    private void BuildSkillRow(Transform parent, SkillDefinition skill, float padding, float spacing, ref float cursorY)
    {
        int count = skillManager != null ? skillManager.CountAcquired(skill) : 0;
        string countText = count > 0 ? $" x{count}" : "";
        string typeTag = skill.activeType == ActiveSkillType.None ? "" : " (액티브)";

        GameObject rowGo = CreateRow(parent, skill.skillName, out Text rowText, rowFontSize, TextAnchor.MiddleLeft);
        rowText.text = $"{skill.skillName}{typeTag}{countText}";
        rowText.color = count > 0 ? acquiredColor : lockedColor;

        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        float rowTop = cursorY;
        PlaceRow(rowRect, padding, rowFontSize + 8, spacing, ref cursorY);
        cursorY = rowTop;
        PlaceRow(rowRect, padding, Mathf.Max(rowFontSize + 8, Mathf.Ceil(rowText.preferredHeight) + 4f), spacing, ref cursorY);

        UITooltipTrigger trigger = rowGo.AddComponent<UITooltipTrigger>();
        trigger.tooltip = tooltip;
        trigger.tooltipText = $"[{skill.skillName}]\n{skill.description}";

        // 장착 슬롯은 액티브 스킬 전용이라, 패시브/속성부여 스킬은 끌어서 장착할 수 없게 한다
        if (count > 0 && skill.activeType != ActiveSkillType.None)
        {
            DraggableSkillIcon dragIcon = rowGo.AddComponent<DraggableSkillIcon>();
            dragIcon.skill = skill;
            dragIcon.sourceSlotIndex = -1;
            dragIcon.slotManager = slotManager;
            dragIcon.dragGhost = dragGhost;
        }
    }

    // ---------------- 장착 슬롯 ----------------

    private void RefreshSlots()
    {
        if (slotLabels == null || slotManager == null) return;

        for (int i = 0; i < slotLabels.Length; i++)
        {
            if (slotLabels[i] == null) continue;

            SkillDefinition equipped = slotManager.GetSlot(i);
            string slotName = i < SlotActions.Length ? KeyBindingManager.GetKey(SlotActions[i]).ToString() : $"슬롯{i + 1}";
            slotLabels[i].text = equipped != null ? $"{slotName}\n{equipped.skillName}" : $"{slotName}\n(비어있음)";

            if (slotDragIcons != null && slotDragIcons[i] != null)
            {
                slotDragIcons[i].skill = equipped;
            }
        }
    }
}
