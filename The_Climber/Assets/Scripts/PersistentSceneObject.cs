using System.Collections.Generic;
using UnityEngine;

// 마을 <-> 탑(Floor1, Floor2...) 씬 전환에도 파괴되면 안 되는 오브젝트(Player, HUD Canvas,
// Main Camera, EventSystem)에 붙인다. GameOverController.RestartRun()처럼 씬을 이름으로
// 다시 로드할 때 같은 key를 가진 오브젝트가 이미 떠 있으면 새로 생긴 쪽을 파괴해서
// 중복 생성을 막는다.
public class PersistentSceneObject : MonoBehaviour
{
    [SerializeField] private string key = "Player";

    private static readonly Dictionary<string, PersistentSceneObject> Instances = new Dictionary<string, PersistentSceneObject>();

    private void Awake()
    {
        if (Instances.TryGetValue(key, out PersistentSceneObject existing) && existing != null)
        {
            Destroy(gameObject);
            return;
        }

        Instances[key] = this;
        DontDestroyOnLoad(gameObject);
    }
}
