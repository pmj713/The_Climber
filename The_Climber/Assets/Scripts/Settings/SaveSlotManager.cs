using UnityEngine;

// 3개의 세이브 칸을 관리한다. 실제 저장 값(제단의 적 강화 단계 등)은 각자의 PlayerPrefs 키 앞에
// "Slot{N}_" 접두어를 붙이는 방식으로 칸별로 분리된다 (EnemyEmpowerment 참고).
public static class SaveSlotManager
{
    public const int SlotCount = 3;

    // 예전 버전의 골드/플레이어 영구 강화 저장 키. 더 이상 쓰지 않으므로 메인 메뉴에서 지운다.
    private static readonly string[] LegacyKeySuffixes =
        { "PlayerGold", "Upgrade_AttackPower", "Upgrade_MaxHealth", "Upgrade_MoveSpeed" };

    // 메인 메뉴에서 세이브 칸을 고르기 전까지는 -1이라 아무 키에도 접근할 수 없다.
    public static int CurrentSlot { get; private set; } = -1;

    public static string Prefix => $"Slot{CurrentSlot}_";

    public static void SelectSlot(int slot)
    {
        CurrentSlot = slot;
        if (!SlotExists(slot))
        {
            PlayerPrefs.SetInt(ExistsKey(slot), 1);
            PlayerPrefs.Save();
        }
    }

    public static bool SlotExists(int slot)
    {
        return PlayerPrefs.GetInt(ExistsKey(slot), 0) == 1;
    }

    public static int GetTotalEnemyBoostLevels(int slot) => EnemyEmpowerment.GetTotalLevels(slot);

    // 세이브 칸 삭제(초기화). 지금 UI에서는 쓰지 않지만 나중에 "칸 비우기" 기능을 붙일 때 대비.
    public static void DeleteSlot(int slot)
    {
        PlayerPrefs.DeleteKey(ExistsKey(slot));
        EnemyEmpowerment.DeleteSlot(slot);
        DeleteLegacyKeys(slot);
        PlayerPrefs.Save();
    }

    public static void PurgeLegacyData()
    {
        for (int slot = 0; slot < SlotCount; slot++) DeleteLegacyKeys(slot);
        PlayerPrefs.Save();
    }

    private static void DeleteLegacyKeys(int slot)
    {
        foreach (string suffix in LegacyKeySuffixes) PlayerPrefs.DeleteKey($"Slot{slot}_{suffix}");
    }

    private static string ExistsKey(int slot) => $"Slot{slot}_Exists";
}
