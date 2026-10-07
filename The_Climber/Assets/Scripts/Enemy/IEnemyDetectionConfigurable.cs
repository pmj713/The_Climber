// 방(RoomEncounter)이 몬스터의 플레이어 인식 범위를 방 크기에 맞춰 덮어쓸 때 쓴다.
public interface IEnemyDetectionConfigurable
{
    void SetDetectRange(float range);
}
