// 몬스터에게 걸 수 있는 상태이상 속성. 한 몬스터에게는 한 번에 하나만 적용되며,
// 빌드 단위로도 플레이어는 이 중 하나만 선택할 수 있다 (상호 배타적).
public enum ElementType
{
    None,
    Freeze, // 빙결
    Burn    // 화상
}
