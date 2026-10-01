using UnityEngine;
using UnityEngine.UI;

// 스킬 코덱스 등에서 커서를 올리면 나오는 공용 설명 툴팁. GetOrCreate로 캔버스당 하나만 만들어 쓴다.
public class SkillTooltip : MonoBehaviour
{
    private RectTransform panelRect;
    private RectTransform canvasRect;
    private Text label;

    public static SkillTooltip GetOrCreate(Transform canvasTransform, Font font)
    {
        Transform existing = canvasTransform.Find("SkillTooltip");
        if (existing != null && existing.TryGetComponent<SkillTooltip>(out var found)) return found;

        GameObject go = new GameObject("SkillTooltip", typeof(RectTransform));
        go.transform.SetParent(canvasTransform, false);
        SkillTooltip tooltip = go.AddComponent<SkillTooltip>();
        tooltip.Build(canvasTransform as RectTransform, font);
        return tooltip;
    }

    private void Build(RectTransform canvasRectTransform, Font font)
    {
        canvasRect = canvasRectTransform;
        panelRect = GetComponent<RectTransform>();
        // 앵커는 캔버스의 피벗(보통 0.5,0.5)과 맞춰야 ScreenPointToLocalPointInRectangle이 돌려주는
        // 좌표를 그대로 anchoredPosition으로 써도 어긋나지 않는다. 피벗만 (0,1)로 둬서 커서 기준
        // 오른쪽 아래로 박스가 펼쳐지게 한다.
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0f, 1f);

        // 씬 안의 다른 서브 캔버스(정렬 순서)에 상관없이 항상 맨 위에 그려지도록 자체 캔버스로 분리한다.
        Canvas overrideCanvas = gameObject.AddComponent<Canvas>();
        overrideCanvas.overrideSorting = true;
        overrideCanvas.sortingOrder = 1000;

        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        bg.raycastTarget = false;

        VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(transform, false);
        label = textGo.AddComponent<Text>();
        label.font = font;
        label.fontSize = 16;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;

        LayoutElement le = textGo.AddComponent<LayoutElement>();
        le.preferredWidth = 340;

        gameObject.SetActive(false);
    }

    public void Show(string text, Vector2 screenPosition, Camera eventCamera)
    {
        gameObject.SetActive(true);
        label.text = text;
        transform.SetAsLastSibling();

        // 폭/높이가 새 텍스트 기준으로 확정되도록 강제로 즉시 리빌드한 뒤에 화면 경계 계산에 사용한다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

        if (canvasRect == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 localPoint))
        {
            return;
        }

        const float offset = 18f;
        Vector2 desired = localPoint + new Vector2(offset, -offset);
        float width = panelRect.rect.width;
        float height = panelRect.rect.height;
        Rect bounds = canvasRect.rect;

        // 화면(캔버스) 오른쪽/아래쪽 밖으로 나가면 커서 반대쪽에 뜨도록 뒤집는다.
        if (desired.x + width > bounds.xMax) desired.x = localPoint.x - offset - width;
        if (desired.y - height < bounds.yMin) desired.y = localPoint.y + offset + height;

        // 그래도 반대쪽 경계를 넘으면 화면 안에 붙인다.
        desired.x = Mathf.Clamp(desired.x, bounds.xMin, bounds.xMax - width);
        desired.y = Mathf.Clamp(desired.y, bounds.yMin + height, bounds.yMax);

        panelRect.anchoredPosition = desired;
    }

    public void Hide()
    {
        if (this == null) return;
        gameObject.SetActive(false);
    }
}
