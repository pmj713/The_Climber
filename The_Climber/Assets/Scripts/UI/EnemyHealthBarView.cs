using UnityEngine;
using UnityEngine.UI;

// 몬스터 발밑 체력바. 실제 체력은 바로 줄고, 뒤의 밝은 '피해 잔상'이 잠깐 머물다 따라 줄어들어 맞은 양이 보인다.
public class EnemyHealthBarView : MonoBehaviour
{
    [SerializeField] private RectTransform fill;
    [SerializeField] private RectTransform damageTrail;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private float trailDelay = 0.35f;
    [SerializeField] private float trailSpeed = 1.2f; // 초당 줄어드는 비율

    private float shown = 1f;
    private float trail = 1f;
    private float trailHoldUntil;

    public void Configure(Color fillColor, Color frameColor)
    {
        fillImage.color = fillColor;
        frameImage.color = frameColor;
    }

    public void SetFraction(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        if (fraction < shown) trailHoldUntil = Time.time + trailDelay;
        shown = fraction;
        if (trail < shown) trail = shown; // 회복 시에는 잔상 없이 바로 따라감

        if (Time.time >= trailHoldUntil) trail = Mathf.MoveTowards(trail, shown, trailSpeed * Time.deltaTime);

        SetWidth(fill, shown);
        SetWidth(damageTrail, trail);
    }

    private static void SetWidth(RectTransform rect, float ratio)
    {
        Vector2 max = rect.anchorMax;
        max.x = ratio;
        rect.anchorMax = max;
    }
}
