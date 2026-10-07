using System;
using UnityEngine;

public class PlayerLevel : MonoBehaviour
{
    public static PlayerLevel Instance { get; private set; }

    [SerializeField] private int level = 1;
    [SerializeField] private int currentExp = 0;
    [SerializeField] private int baseExpToLevel = 50;
    [SerializeField] private float growthExponent = 1.2f;

    public int Level => level;
    public int CurrentExp => currentExp;
    public int ExpToNextLevel => CalculateExpToLevel(level);

    public event Action<int, int> OnExpChanged; // currentExp, expToNextLevel
    public event Action<int> OnLevelUp;         // newLevel
    public event Action<int> OnLevelReset;       // newLevel (런 초기화용, 레벨업 선택창을 열지 않음)

    private void Awake()
    {
        // 마을을 다시 불러올 때 씬에 들어 있는 사본은 중복으로 파괴되므로, 이미 살아 있는 본체의 싱글톤을 덮어쓰지 않는다
        if (Instance == null || Instance == this) Instance = this;
    }

    private void Start()
    {
        OnExpChanged?.Invoke(currentExp, ExpToNextLevel);
    }

    // 새 런을 시작할 때(탑 입장) 호출. OnLevelUp이 아니라 OnLevelReset을 쏘기 때문에
    // 레벨업 선택창이 열리지 않는다.
    public void ResetProgress()
    {
        level = 1;
        currentExp = 0;
        OnLevelReset?.Invoke(level);
        OnExpChanged?.Invoke(currentExp, ExpToNextLevel);
    }

    public void AddExp(int amount)
    {
        if (amount <= 0) return;

        currentExp += amount;
        Debug.Log($"EXP +{amount} ({currentExp}/{ExpToNextLevel})");

        while (currentExp >= ExpToNextLevel)
        {
            currentExp -= ExpToNextLevel;
            level++;
            Debug.Log($"Level Up! -> Lv.{level}");
            OnLevelUp?.Invoke(level);
        }

        OnExpChanged?.Invoke(currentExp, ExpToNextLevel);
    }

    private int CalculateExpToLevel(int forLevel)
    {
        return Mathf.RoundToInt(baseExpToLevel * Mathf.Pow(forLevel, growthExponent));
    }
}
