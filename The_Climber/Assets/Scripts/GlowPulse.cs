using UnityEngine;

// 지속형 이펙트(가드 방벽 등)의 밝기를 일정하게 맥동시킨다.
public class GlowPulse : MonoBehaviour
{
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private float frequency = 5f;
    [SerializeField] private float amount = 0.25f;
    [SerializeField] private float fadeInTime = 0.15f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MaterialPropertyBlock block;
    private Color[] baseColors;
    private float elapsed;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            baseColors[i] = renderers[i].sharedMaterial.GetColor(BaseColorId);
        }
    }

    // 스킬 속성 색처럼 런타임에 정해지는 색을 머티리얼 기본색에 곱한다.
    public void SetTint(Color tint)
    {
        if (baseColors == null) Awake();
        for (int i = 0; i < renderers.Length; i++)
        {
            baseColors[i] = renderers[i].sharedMaterial.GetColor(BaseColorId) * tint;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float fade = fadeInTime > 0f ? Mathf.Clamp01(elapsed / fadeInTime) : 1f;
        float pulse = fade * (1f + amount * Mathf.Sin(elapsed * frequency * Mathf.PI * 2f));
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, baseColors[i] * pulse);
            renderers[i].SetPropertyBlock(block);
        }
    }
}
