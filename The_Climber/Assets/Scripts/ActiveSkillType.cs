public enum ActiveSkillType
{
    None,
    ArrowRain,   // 화살비: 지정 범위에 지속시간 동안 틱 데미지
    Haste,       // 헤이스트
    SwordWave,   // 검기: 검을 휘둘러 전방으로 베기 에너지를 날린다
    Whirlwind,   // 힐윈드: 캐스팅 중 이동 가능, 틱마다 주변 데미지
    FanShot,     // 부채살: 시작부터 여러 방향으로 동시 발사
    BurnNova     // 화상폭발: 화상 걸린 모든 몬스터에게 스택×키워드레벨 데미지, 스택 초기화
}
