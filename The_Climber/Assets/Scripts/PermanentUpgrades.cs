using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UpgradeDefinition
{
    public UpgradeType type;
    public string displayName = "";
    public float percentPerLevel = 0.1f;
    public int baseCost = 50;
    public float costGrowth = 1.3f;
}

// 마을 업그레이드(공격력/생명력/이동속도 %)를 관리한다. 레벨은 PlayerPrefs에 저장되어
// 런이 끝나도(씬 재시작/재실행에도) 영구히 유지된다.
public class PermanentUpgrades : MonoBehaviour
{
    public static PermanentUpgrades Instance { get; private set; }

    [SerializeField]
    private UpgradeDefinition[] upgrades = new UpgradeDefinition[]
    {
        new UpgradeDefinition { type = UpgradeType.AttackPower, displayName = "공격력", percentPerLevel = 0.1f, baseCost = 50, costGrowth = 1.3f },
        new UpgradeDefinition { type = UpgradeType.MaxHealth, displayName = "생명력", percentPerLevel = 0.1f, baseCost = 50, costGrowth = 1.3f },
        new UpgradeDefinition { type = UpgradeType.MoveSpeed, displayName = "이동속도", percentPerLevel = 0.05f, baseCost = 40, costGrowth = 1.3f },
    };

    private readonly Dictionary<UpgradeType, int> levels = new Dictionary<UpgradeType, int>();

    private void Awake()
    {
        Instance = this;

        foreach (UpgradeDefinition def in upgrades)
        {
            levels[def.type] = PlayerPrefs.GetInt(SaveKeyFor(def.type), 0);
        }
    }

    private string SaveKeyFor(UpgradeType type) => SaveSlotManager.Prefix + $"Upgrade_{type}";

    public UpgradeDefinition GetDefinition(UpgradeType type)
    {
        foreach (UpgradeDefinition def in upgrades)
        {
            if (def.type == type) return def;
        }
        return null;
    }

    public int GetLevel(UpgradeType type)
    {
        return levels.TryGetValue(type, out int level) ? level : 0;
    }

    // 실제 스탯에 곱할 보너스 비율 (예: 0.2 = +20%)
    public float GetBonusPercent(UpgradeType type)
    {
        UpgradeDefinition def = GetDefinition(type);
        return def == null ? 0f : GetLevel(type) * def.percentPerLevel;
    }

    public int GetNextCost(UpgradeType type)
    {
        UpgradeDefinition def = GetDefinition(type);
        if (def == null) return 0;
        return Mathf.RoundToInt(def.baseCost * Mathf.Pow(def.costGrowth, GetLevel(type)));
    }

    public bool TryPurchase(UpgradeType type)
    {
        int cost = GetNextCost(type);
        if (PlayerGold.Instance == null || !PlayerGold.Instance.TrySpend(cost)) return false;

        levels[type] = GetLevel(type) + 1;
        PlayerPrefs.SetInt(SaveKeyFor(type), levels[type]);
        PlayerPrefs.Save();
        return true;
    }
}
