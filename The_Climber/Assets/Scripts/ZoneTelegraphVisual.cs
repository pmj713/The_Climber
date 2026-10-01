using UnityEngine;

// 바닥 장판 예고 연출. 반지름 1 기준으로 만든 프리팹을 radius 배로 키우고,
// fuse 동안 안쪽 채움이 가장자리까지 차오르며 터지는 순간을 읽을 수 있게 한다.
public class ZoneTelegraphVisual : MonoBehaviour
{
    [SerializeField] private Transform fill;
    [SerializeField] private Transform rune;
    [SerializeField] private Renderer[] pulseRenderers;
    [SerializeField] private float runeSpinSpeed = 35f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private float duration = 1f;
    private float elapsed;
    private MaterialPropertyBlock block;
    private Color[] baseColors;

    public void Begin(float radius, float fuseDuration)
    {
        transform.localScale = Vector3.one * radius;
        duration = Mathf.Max(0.01f, fuseDuration);
        elapsed = 0f;
        if (fill != null) fill.localScale = Vector3.zero;
    }

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        baseColors = new Color[pulseRenderers.Length];
        for (int i = 0; i < pulseRenderers.Length; i++)
        {
            baseColors[i] = pulseRenderers[i].sharedMaterial.GetColor(BaseColorId);
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        if (fill != null) fill.localScale = Vector3.one * (t * t * (3f - 2f * t));
        if (rune != null) rune.Rotate(Vector3.up, runeSpinSpeed * Time.deltaTime, Space.World);

        // 폭발 직전일수록 빠르게 깜빡여서 "곧 터진다"를 알린다
        float pulse = 1f + 0.35f * Mathf.Sin(elapsed * Mathf.Lerp(6f, 28f, t));
        for (int i = 0; i < pulseRenderers.Length; i++)
        {
            pulseRenderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, baseColors[i] * pulse);
            pulseRenderers[i].SetPropertyBlock(block);
        }
    }
}
