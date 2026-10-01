using UnityEngine;

// 플레이어 공격 이펙트는 흰색 기준으로 만들어 두고, 일반공격에 붙은 속성(화상/빙결)에 맞춰 런타임에 물들인다.
public static class EffectTint
{
    public static Color ForElement(ElementType element)
    {
        switch (element)
        {
            case ElementType.Burn: return new Color(1f, 0.32f, 0.06f);
            case ElementType.Freeze: return new Color(0.35f, 0.8f, 1f);
            default: return new Color(0.85f, 0.93f, 1f);
        }
    }

    // 액티브 스킬은 속성이 없을 때 일반공격(은백색)과 구분되게 하늘빛 마력 색을 쓴다.
    public static Color ForSkill(ElementType element)
    {
        return element == ElementType.None ? new Color(0.45f, 0.75f, 1f) : ForElement(element);
    }

    // 지속형 이펙트를 끝낼 때: 새 파티클만 멈추고, 이미 뿌린 건 자연스럽게 사라진 뒤 제거한다.
    public static void StopAndDestroy(GameObject effect, float linger = 1f)
    {
        if (effect == null) return;
        effect.transform.SetParent(null, true);
        foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        foreach (Renderer r in effect.GetComponentsInChildren<Renderer>())
        {
            if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer)) r.enabled = false;
        }
        Object.Destroy(effect, linger);
    }

    // 파티클 시작 색과 트레일 색에 tint를 곱한다. 수명이 지나면 스스로 사라진다.
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Color tint, float lifetime = 1.5f)
    {
        if (prefab == null) return null;
        GameObject effect = Object.Instantiate(prefab, position, rotation);
        Apply(effect, tint);
        Object.Destroy(effect, lifetime);
        return effect;
    }

    public static void Apply(GameObject root, Color tint)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.name.Contains("NoTint")) continue; // 흙먼지처럼 속성과 상관없는 색은 그대로 둔다
            ParticleSystem.MainModule main = ps.main;
            main.startColor = main.startColor.color * tint;
        }
        foreach (TrailRenderer trail in root.GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.colorGradient = TrailGradient(tint);
        }
        foreach (GlowPulse glow in root.GetComponentsInChildren<GlowPulse>(true))
        {
            glow.SetTint(tint);
        }
    }

    public static Gradient TrailGradient(Color tint)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(tint, Color.white, 0.2f), 0f), new GradientColorKey(tint, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
