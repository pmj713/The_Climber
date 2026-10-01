using UnityEngine;

// 검 일반공격의 초승달 베기. 반지름 1 기준 프리팹을 판정 반지름만큼 키우고,
// 옆에서 정면으로 쓸어오면서 밝게 번쩍였다가 사라진다.
public class SlashVisual : MonoBehaviour
{
    [SerializeField] private Transform sweepPivot;
    [SerializeField] private Renderer slashRenderer;
    [SerializeField] private float duration = 0.22f;
    [SerializeField] private float sweepAngle = 75f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MaterialPropertyBlock block;
    private Color color = Color.white;
    private float baseScale = 1f;
    private float elapsed;

    public void Play(Color tint, float radius)
    {
        block ??= new MaterialPropertyBlock();
        color = slashRenderer.sharedMaterial.GetColor(BaseColorId) * tint;
        baseScale = radius;
        elapsed = 0f;
        Apply(0f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / Mathf.Max(0.01f, duration);
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Apply(t);
    }

    private void Apply(float t)
    {
        block ??= new MaterialPropertyBlock();
        float sweep = 1f - Mathf.Pow(1f - Mathf.Clamp01(t * 2.5f), 3f); // 앞쪽 40% 동안 빠르게 쓸어옴
        sweepPivot.localRotation = Quaternion.Euler(0f, Mathf.Lerp(sweepAngle, 0f, sweep), 0f);
        transform.localScale = Vector3.one * baseScale * Mathf.Lerp(0.9f, 1.1f, t);

        float alpha = t < 0.35f ? 1f : 1f - (t - 0.35f) / 0.65f;
        Color c = color;
        c.a *= alpha;
        slashRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        slashRenderer.SetPropertyBlock(block);
    }
}
