using System;
using UnityEngine;

// 런과 무관하게 영구히 쌓이는 골드. PlayerPrefs로 저장되어 씬을 다시 시작하거나
// 게임을 껐다 켜도 유지된다.
public class PlayerGold : MonoBehaviour
{
    private const string SaveKey = "PlayerGold";

    public static PlayerGold Instance { get; private set; }

    private int gold;

    public int Gold => gold;

    public event Action<int> OnGoldChanged;

    private void Awake()
    {
        Instance = this;
        gold = PlayerPrefs.GetInt(SaveKey, 0);
    }

    private void Start()
    {
        OnGoldChanged?.Invoke(gold);
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;

        gold += amount;
        Save();
        OnGoldChanged?.Invoke(gold);
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || gold < amount) return false;

        gold -= amount;
        Save();
        OnGoldChanged?.Invoke(gold);
        return true;
    }

    private void Save()
    {
        PlayerPrefs.SetInt(SaveKey, gold);
        PlayerPrefs.Save();
    }
}
