using UnityEngine;

// 직선 돌진 경로 예고. 폭 1 x 길이 1(앞쪽 +Z) 기준으로 만든 프리팹을 늘려 쓰고,
// 예고 시간 동안 시작점에서 끝까지 채움이 뻗어나가 출발 시점을 읽을 수 있게 한다.
public class LaneTelegraphVisual : MonoBehaviour
{
    [SerializeField] private Transform fill;
    [SerializeField] private Renderer[] tiledRenderers; // 길이에 맞춰 무늬(화살표)를 반복시킬 렌더러
    [SerializeField] private Renderer[] pulseRenderers;
    [SerializeField] private float scrollSpeed = 1.5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    private float duration = 1f;
    private float elapsed;
    private float tiling = 1f;
    private MaterialPropertyBlock block;
    private Color[] baseColors;
    private Material[] tiledMaterials; // 타일링/스크롤은 프로퍼티 블록으로 안 먹어서 인스턴스 머티리얼로 처리

    public void Begin(float length, float width, float telegraphDuration)
    {
        transform.localScale = new Vector3(width, 1f, length);
        duration = Mathf.Max(0.01f, telegraphDuration);
        elapsed = 0f;
        tiling = Mathf.Max(1f, length / width);
        if (fill != null) fill.localScale = new Vector3(1f, 1f, 0f);
    }

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        baseColors = new Color[pulseRenderers.Length];
        for (int i = 0; i < pulseRenderers.Length; i++)
        {
            baseColors[i] = pulseRenderers[i].sharedMaterial.GetColor(BaseColorId);
        }

        tiledMaterials = new Material[tiledRenderers.Length];
        for (int i = 0; i < tiledRenderers.Length; i++)
        {
            tiledMaterials[i] = tiledRenderers[i].material;
        }
    }

    private void OnDestroy()
    {
        if (tiledMaterials == null) return;
        foreach (Material m in tiledMaterials)
        {
            if (m != null) Destroy(m);
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        if (fill != null) fill.localScale = new Vector3(1f, 1f, t * t * (3f - 2f * t));

        foreach (Material m in tiledMaterials)
        {
            m.SetTextureScale(BaseMapId, new Vector2(1f, tiling));
            m.SetTextureOffset(BaseMapId, new Vector2(0f, -elapsed * scrollSpeed));
        }

        float pulse = 1f + 0.35f * Mathf.Sin(elapsed * Mathf.Lerp(6f, 28f, t));
        for (int i = 0; i < pulseRenderers.Length; i++)
        {
            pulseRenderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, baseColors[i] * pulse);
            pulseRenderers[i].SetPropertyBlock(block);
        }
    }
}
