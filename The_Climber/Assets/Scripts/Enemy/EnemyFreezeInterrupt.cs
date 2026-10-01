using UnityEngine;

// 패턴 도중 빙결되면 각 AI가 진행 중인 패턴을 끊는다(예고/이펙트 정리, 무적·가드 해제 등은 AI별로 처리).
// 얼어 있는 동안에는 몸이 그 자세 그대로 멈춰 있다가(EnemyStatusVisual이 애니메이션 정지),
// 녹는 순간 하던 동작을 이어가지 않도록 대기 자세로 되돌린다.
public static class EnemyFreezeInterrupt
{
    private static readonly int IdleState = Animator.StringToHash("Idle");

    public static void ReturnToIdle(Animator animator, params int[] triggersToReset)
    {
        if (animator == null) return;
        foreach (int trigger in triggersToReset) animator.ResetTrigger(trigger);
        if (animator.HasState(0, IdleState)) animator.CrossFadeInFixedTime(IdleState, 0.15f, 0);
    }
}
