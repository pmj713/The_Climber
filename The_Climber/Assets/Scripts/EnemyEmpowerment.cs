using System;
using UnityEngine;

public enum EnemyBoostType
{
    Health,    // 적 체력
    Damage,    // 적 공격력
    MoveSpeed  // 적 이동속도
}

// 마을 제단에서 고르는 '적 강화' 단계. 재화 없이 자유롭게 올리고 내릴 수 있고, 세이브 칸별로 PlayerPrefs에 저장된다.
// 탑 씬에서도 읽어야 해서 씬 오브젝트가 아니라 정적 클래스로 둔다. 적이 생성될 때(EnemyHealth.Start) 적용된다.
public static class EnemyEmpowerment
{
    public const int MaxLevel = 10;

    private static readonly EnemyBoostType[] AllTypes = { EnemyBoostType.Health, EnemyBoostType.Damage, EnemyBoostType.MoveSpeed };

    public static event Action OnChanged;

    public static string DisplayName(EnemyBoostType type)
    {
        switch (type)
        {
            case EnemyBoostType.Health: return "적 체력";
            case EnemyBoostType.Damage: return "적 공격력";
            default: return "적 이동속도";
        }
    }

    // 단계당 증가율 (예: 0.1 = 단계마다 +10%)
    public static float PercentPerLevel(EnemyBoostType type) => type == EnemyBoostType.MoveSpeed ? 0.05f : 0.1f;

    public static string SaveKey(int slot, EnemyBoostType type) => $"Slot{slot}_EnemyBoost_{type}";

    // 메인 메뉴를 거치지 않고 마을 씬을 바로 실행하면 칸이 -1인데, 그때도 조절이 되도록
    // 메뉴에 보이지 않는 임시 칸("Slot-1_")에 그대로 저장한다.
    public static int GetLevel(EnemyBoostType type)
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(SaveKey(SaveSlotManager.CurrentSlot, type), 0), 0, MaxLevel);
    }

    public static float GetMultiplier(EnemyBoostType type) => 1f + GetLevel(type) * PercentPerLevel(type);

    public static int GetTotalLevels(int slot)
    {
        int total = 0;
        foreach (EnemyBoostType type in AllTypes) total += Mathf.Clamp(PlayerPrefs.GetInt(SaveKey(slot, type), 0), 0, MaxLevel);
        return total;
    }

    public static void ChangeLevel(EnemyBoostType type, int delta)
    {
        int level = Mathf.Clamp(GetLevel(type) + delta, 0, MaxLevel);
        PlayerPrefs.SetInt(SaveKey(SaveSlotManager.CurrentSlot, type), level);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static void DeleteSlot(int slot)
    {
        foreach (EnemyBoostType type in AllTypes) PlayerPrefs.DeleteKey(SaveKey(slot, type));
    }

    // 새로 생성된 적에게 현재 강화 단계를 적용한다.
    public static void ApplyTo(GameObject enemy)
    {
        float health = GetMultiplier(EnemyBoostType.Health);
        float damage = GetMultiplier(EnemyBoostType.Damage);
        float speed = GetMultiplier(EnemyBoostType.MoveSpeed);

        if (!Mathf.Approximately(health, 1f) && enemy.TryGetComponent<EnemyHealth>(out var enemyHealth))
            enemyHealth.ApplyHealthMultiplier(health);

        foreach (IEnemyEmpowerable ai in enemy.GetComponents<IEnemyEmpowerable>())
        {
            if (!Mathf.Approximately(damage, 1f)) ai.ApplyDamageMultiplier(damage);
            if (!Mathf.Approximately(speed, 1f)) ai.ApplyMoveSpeedMultiplier(speed);
        }
    }
}
