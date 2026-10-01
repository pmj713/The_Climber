using UnityEngine;
using UnityEngine.UI;

// 몬스터 머리 위 상태이상 배지 (아이콘 + 스택 수 + 스택 게이지). EnemyStatusIndicator가 값만 넘겨준다.
public class StatusStackIndicatorView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Text countText;
    [SerializeField] private RectTransform barFill;
    [SerializeField] private Image barFillImage;
    [SerializeField] private Sprite burnSprite;
    [SerializeField] private Sprite freezeSprite;
    [SerializeField] private Color burnColor = new Color(1f, 0.55f, 0.15f);
    [SerializeField] private Color freezeColor = new Color(0.45f, 0.85f, 1f);
    [SerializeField] private Color frozenColor = new Color(0.75f, 0.95f, 1f);

    private float pulseTime;

    public void Show(EnemyStatusEffects.StatusType type, int stacks, float ratio, bool frozen)
    {
        bool burn = type == EnemyStatusEffects.StatusType.Burn;
        Color color = burn ? burnColor : (frozen ? frozenColor : freezeColor);

        icon.sprite = burn ? burnSprite : freezeSprite;
        icon.color = color;
        countText.text = frozen ? "빙결" : stacks.ToString();
        countText.color = color;
        barFillImage.color = color;

        Vector2 max = barFill.anchorMax;
        max.x = Mathf.Clamp01(frozen ? 1f : ratio);
        barFill.anchorMax = max;

        // 최대 스택(=곧 터짐/빙결)일 때는 맥동해서 눈에 띄게
        bool alert = frozen || ratio >= 0.999f;
        pulseTime = alert ? pulseTime + Time.deltaTime : 0f;
        transform.localScale = Vector3.one * (alert ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(pulseTime * 6f)) : 1f);
    }
}
