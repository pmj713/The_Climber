using System.Collections.Generic;
using UnityEngine;

// 문/제단/무기 받침대처럼 키(E)로 상호작용하는 오브젝트가 "지금 플레이어가 쓸 수 있는 상태"를 알려주는 곳.
// 같은 키에 스킬이 묶여 있어도, 상호작용 중인 키로는 스킬이 나가지 않게 하는 데 쓴다.
public static class PlayerInteraction
{
    private static readonly Dictionary<Object, KeyCode> available = new Dictionary<Object, KeyCode>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => available.Clear();

    public static void SetAvailable(Object source, bool isAvailable, KeyCode key)
    {
        if (isAvailable) available[source] = key;
        else available.Remove(source);
    }

    public static bool IsKeyReserved(KeyCode key)
    {
        List<Object> destroyed = null;
        bool reserved = false;
        foreach (KeyValuePair<Object, KeyCode> entry in available)
        {
            if (entry.Key == null) { (destroyed ??= new List<Object>()).Add(entry.Key); continue; }
            if (entry.Value == key) reserved = true;
        }
        if (destroyed != null) foreach (Object o in destroyed) available.Remove(o);
        return reserved;
    }
}
