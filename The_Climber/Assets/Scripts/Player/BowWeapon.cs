using UnityEngine;

public class BowWeapon : MonoBehaviour, IWeapon
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private int damage = 8; // 영구 업그레이드 적용 전 기본값
    [SerializeField] private float multiShotSpreadAngle = 10f;
    [SerializeField] private float powerDamagePerLevel = 0.15f;

    private PlayerSkillManager skillManager;
    private int effectiveDamage;

    private void Awake()
    {
        skillManager = GetComponent<PlayerSkillManager>();
    }

    private void Start()
    {
        ApplyPermanentUpgrades();
    }

    // 영구 업그레이드(공격력 %)를 기본값에 다시 적용한다. 플레이어는 씬을 넘어가도
    // 파괴되지 않아서 Start()는 한 번만 실행되므로, 제단에서 구매하거나 새 런을 시작할 때
    // TownController가 이 메서드를 직접 호출해줘야 한다.
    public void ApplyPermanentUpgrades()
    {
        float bonus = PermanentUpgrades.Instance != null ? PermanentUpgrades.Instance.GetBonusPercent(UpgradeType.AttackPower) : 0f;
        effectiveDamage = Mathf.RoundToInt(damage * (1f + bonus));
    }

    public void TryAttack(Vector3 origin, Vector3 direction)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("BowWeapon: projectilePrefab이 지정되지 않았습니다.");
            return;
        }

        int projectileCount = 1 + GetLevel(KeywordType.Projectile);
        int pierceCount = GetLevel(KeywordType.Pierce);
        int finalDamage = Mathf.RoundToInt(effectiveDamage * (1f + powerDamagePerLevel * GetLevel(KeywordType.Power)));

        for (int i = 0; i < projectileCount; i++)
        {
            Vector3 shotDirection = ApplySpread(direction, i, projectileCount);
            Projectile projectile = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(shotDirection));
            projectile.Launch(shotDirection, projectileSpeed, finalDamage, pierceCount);
        }
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
