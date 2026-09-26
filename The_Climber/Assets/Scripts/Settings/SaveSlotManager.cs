using UnityEngine;

// 3개의 세이브 칸을 관리한다. 실제 저장 값(골드/영구 강화 등)은 각자의 PlayerPrefs 키 앞에
// "Slot{N}_" 접두어를 붙이는 방식으로 칸별로 분리된다 (PlayerGold, PermanentUpgrades 참고).
public static class SaveSlotManager
{
    public const int SlotCount = 3;

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

    public static int GetGold(int slot)
    {
        return PlayerPrefs.GetInt($"Slot{slot}_PlayerGold", 0);
    }

    public static int GetTotalUpgradeLevels(int slot)
    {
        int total = 0;
        foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
        {
            total += PlayerPrefs.GetInt($"Slot{slot}_Upgrade_{type}", 0);
        }
        return total;
    }

    // 세이브 칸 삭제(초기화). 지금 UI에서는 쓰지 않지만 나중에 "칸 비우기" 기능을 붙일 때 대비.
    public static void DeleteSlot(int slot)
    {
        PlayerPrefs.DeleteKey(ExistsKey(slot));
        PlayerPrefs.DeleteKey($"Slot{slot}_PlayerGold");
        foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
        {
            PlayerPrefs.DeleteKey($"Slot{slot}_Upgrade_{type}");
        }
        PlayerPrefs.Save();
    }

    private static string ExistsKey(int slot) => $"Slot{slot}_Exists";
}
