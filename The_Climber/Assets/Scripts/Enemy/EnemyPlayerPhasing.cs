using UnityEngine;

// 몬스터가 돌진하는 동안 플레이어와 서로 밀지 않고 통과해야 할 때 쓰는 상태 표시.
// 플레이어 회피가 끝날 때 돌진 중인 몬스터와는 통과 상태를 유지하는 데 사용한다.
public interface IEnemyPhasing
{
    bool IsPhasingThroughPlayer { get; }
}

public static class EnemyPlayerPhasing
{
    // ignore=false(충돌 복원)는 플레이어가 회피 중이면 건너뛴다. 그 경우 회피가 끝날 때 플레이어 쪽에서 복원한다.
    public static void Apply(Collider[] colliders, bool ignore)
    {
        PlayerMovement player = PlayerMovement.Instance;
        if (player == null || colliders == null || (!ignore && player.IsDodging)) return;
        foreach (Collider c in colliders) player.IgnoreCollisionWith(c, ignore);
    }
}
