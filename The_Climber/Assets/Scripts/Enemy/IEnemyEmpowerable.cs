// 층 난이도 배율과 제단의 '적 강화'를 받는 몬스터 AI가 구현한다.
public interface IEnemyEmpowerable
{
    void ApplyDamageMultiplier(float multiplier);
    void ApplyMoveSpeedMultiplier(float multiplier);
}
