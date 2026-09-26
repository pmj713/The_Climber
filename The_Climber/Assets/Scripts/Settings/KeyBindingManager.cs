using System;
using System.Collections.Generic;
using UnityEngine;

public enum RebindableAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Dodge,
    NormalAttack,
    SkillRightClick,
    SkillE,
    SkillQ,
}

// 플레이어가 바꿀 수 있는 키(이동/스킬/회피)를 관리하고 PlayerPrefs에 저장한다.
public static class KeyBindingManager
{
    private const string PrefPrefix = "KeyBind_";

    private static readonly Dictionary<RebindableAction, KeyCode> Defaults = new Dictionary<RebindableAction, KeyCode>
    {
        { RebindableAction.MoveUp, KeyCode.W },
        { RebindableAction.MoveDown, KeyCode.S },
        { RebindableAction.MoveLeft, KeyCode.A },
        { RebindableAction.MoveRight, KeyCode.D },
        { RebindableAction.Dodge, KeyCode.Space },
        { RebindableAction.NormalAttack, KeyCode.Mouse0 },
        { RebindableAction.SkillRightClick, KeyCode.Mouse1 },
        { RebindableAction.SkillE, KeyCode.E },
        { RebindableAction.SkillQ, KeyCode.Q },
    };

    private static readonly Dictionary<RebindableAction, string> DisplayNames = new Dictionary<RebindableAction, string>
    {
        { RebindableAction.MoveUp, "이동: 위" },
        { RebindableAction.MoveDown, "이동: 아래" },
        { RebindableAction.MoveLeft, "이동: 왼쪽" },
        { RebindableAction.MoveRight, "이동: 오른쪽" },
        { RebindableAction.Dodge, "회피" },
        { RebindableAction.NormalAttack, "일반공격" },
        { RebindableAction.SkillRightClick, "스킬 (우클릭)" },
        { RebindableAction.SkillE, "스킬 (E)" },
        { RebindableAction.SkillQ, "스킬 (Q)" },
    };

    private static Dictionary<RebindableAction, KeyCode> current;

    public static event Action OnBindingsChanged;

    private static void EnsureLoaded()
    {
        if (current != null) return;

        current = new Dictionary<RebindableAction, KeyCode>();
        foreach (var pair in Defaults)
        {
            string saved = PlayerPrefs.GetString(PrefPrefix + pair.Key, pair.Value.ToString());
            current[pair.Key] = Enum.TryParse(saved, out KeyCode parsed) ? parsed : pair.Value;
        }
    }

    public static KeyCode GetKey(RebindableAction action)
    {
        EnsureLoaded();
        return current[action];
    }

    // 이 키를 이미 쓰고 있는 다른 동작이 있으면 그 동작을 돌려준다 (중복 배정 방지용).
    public static RebindableAction? FindActionForKey(KeyCode key)
    {
        EnsureLoaded();
        foreach (var pair in current)
        {
            if (pair.Value == key) return pair.Key;
        }
        return null;
    }

    public static string GetDisplayName(RebindableAction action)
    {
        return DisplayNames.TryGetValue(action, out string name) ? name : action.ToString();
    }

    public static IEnumerable<RebindableAction> AllActions => Defaults.Keys;

    public static void Rebind(RebindableAction action, KeyCode newKey)
    {
        EnsureLoaded();
        current[action] = newKey;
        PlayerPrefs.SetString(PrefPrefix + action, newKey.ToString());
        PlayerPrefs.Save();
        OnBindingsChanged?.Invoke();
    }

    public static void ResetToDefaults()
    {
        EnsureLoaded();
        foreach (var pair in Defaults)
        {
            current[pair.Key] = pair.Value;
            PlayerPrefs.SetString(PrefPrefix + pair.Key, pair.Value.ToString());
        }
        PlayerPrefs.Save();
        OnBindingsChanged?.Invoke();
    }
}
