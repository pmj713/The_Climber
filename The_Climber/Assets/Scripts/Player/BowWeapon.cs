using UnityEngine;

public class BowWeapon : MonoBehaviour, IWeapon
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private int damage = 8;
    [SerializeField] private float multiShotSpreadAngle = 10f;
    [SerializeField] private float powerDamagePerLevel = 0.15f;
    [Tooltip("화살을 놓는 순간 활 앞에서 터지는 이펙트 (선택)")]
    [SerializeField] private GameObject releaseEffectPrefab;

    private PlayerSkillManager skillManager;
    private int effectiveDamage;

    private void Awake()
    {
        skillManager = GetComponent<PlayerSkillManager>();
        effectiveDamage = damage;
    }

    public void TryAttack(Vector3 origin, Vector3 direction)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("BowWeapon: projectilePrefab이 지정되지 않았습니다.");
            return;
        }

        // 일반공격 키워드는 레벨이 오를 때마다 데미지/공격속도가 번갈아 증가한다 (홀수 레벨 = 데미지).
        int basicAttackLevel = GetLevel(KeywordType.BasicAttack);
        int powerStacks = (basicAttackLevel + 1) / 2;
        int finalDamage = Mathf.RoundToInt(effectiveDamage * (1f + powerDamagePerLevel * powerStacks));

        int projectileLevel = GetLevel(KeywordType.Projectile);
        int projectileCount = 1 + projectileLevel;
        int pierceCount = projectileLevel;
        ElementType element = ResolveElement();
        EffectTint.Spawn(releaseEffectPrefab, origin, Quaternion.LookRotation(direction), EffectTint.ForElement(element), 1f);

        for (int i = 0; i < projectileCount; i++)
        {
            Vector3 shotDirection = ApplySpread(direction, i, projectileCount);
            Projectile projectile = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(shotDirection));
            projectile.Launch(shotDirection, projectileSpeed, finalDamage, pierceCount, element, 1);
        }
    }

    private ElementType ResolveElement()
    {
        if (skillManager == null) return ElementType.None;
        if (skillManager.HasElement(ElementSource.BasicAttack, ElementType.Freeze)) return ElementType.Freeze;
        if (skillManager.HasElement(ElementSource.BasicAttack, ElementType.Burn)) return ElementType.Burn;
        return ElementType.None;
    }

    private int GetLevel(KeywordType keyword)
    {
        return skillManager != null ? skillManager.GetKeywordLevel(keyword) : 0;
    }

    private Vector3 ApplySpread(Vector3 direction, int index, int count)
    {
        if (count <= 1) return direction;

        // 여러 발일 때는 조준 방향을 중심으로 부채꼴로 퍼뜨려서 발사
        float totalSpread = multiShotSpreadAngle * (count - 1);
        float angle = -totalSpread / 2f + multiShotSpreadAngle * index;
        return Quaternion.Euler(0f, angle, 0f) * direction;
    }
}
