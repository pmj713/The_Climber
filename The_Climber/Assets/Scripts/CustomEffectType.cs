// 키워드 레벨(성장 방향)만으로는 표현할 수 없는, 스킬별 개별 수치 보너스.
// PlayerSkillManager.GetCustomEffectTotal()이 이 타입을 가진 모든 획득 스킬의 값을 합산한다.
public enum CustomEffectType
{
    None,
    FreezeMaxStackDelta,   // 서리 취약: 완전 빙결에 필요한 최대 스택 감소
    FreezeStackPerHit,     // 냉기 집중: 한 번에 쌓이는 빙결 스택 증가
    FreezeDuration,        // 냉기 지속: 스택 유지 시간 증가
    FreezeAuraStackPerSecond, // 냉기 확산: 주변 몬스터에게 자동으로 쌓이는 스택(느림)
    FreezeExplosionBonus,  // 냉기 폭발: 빙결 해제 데미지 배율 증가
    BurnMaxStackDelta,     // 겁화 축적: 최대 화상 스택 증가
    BurnDamageMultiplier,  // 화염 강화: 화상 틱 데미지 배율 증가
    BurnDuration,          // 화염 지속: 화상 지속시간 증가
    BurnTickSpeed,         // 화염 가속: 화상 틱 간격 감소
    AreaRadiusMultiplier,  // 범위 확장
    AreaDamageMultiplier,  // 여파 강화
    AreaDurationMultiplier,// 여운 지속
    WhirlwindTickSpeed,    // 선풍 가속 (검 전용, Area 키워드 소속)
    CharacterPower,        // 공격력 강화: 캐릭터 공격력 증가. 일반공격(무기 기본공격)에는 영향 없이,
                           // 화상폭발을 제외한 다른 액티브 스킬들의 데미지에 곱연산으로 적용된다.
    SwordRangeBonus,       // 검 공격범위 증가
    SwordWaveSizeBonus     // 검기 크기 증가 (검 전용, 투사체 키워드 소속)
}
