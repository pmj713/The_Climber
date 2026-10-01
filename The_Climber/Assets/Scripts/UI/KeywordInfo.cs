// 키워드 UI(스킬 코덱스 상단 바 등)에 쓰이는 표시용 이름/설명. 게임 로직에는 영향 없음.
public static class KeywordInfo
{
    public static string GetDisplayName(KeywordType keyword)
    {
        switch (keyword)
        {
            case KeywordType.BasicAttack: return "일반공격";
            case KeywordType.Projectile: return "투사체";
            case KeywordType.Freeze: return "빙결";
            case KeywordType.Burn: return "화상";
            case KeywordType.Area: return "범위";
            case KeywordType.Buff: return "버프";
            default: return keyword.ToString();
        }
    }

    public static string GetDescription(KeywordType keyword)
    {
        switch (keyword)
        {
            case KeywordType.BasicAttack:
                return "일반공격 데미지와 공격속도가 번갈아 가며 증가합니다.";
            case KeywordType.Projectile:
                return "검기/부채살의 투사체 개수가 레벨에 비례해 증가합니다.";
            case KeywordType.Freeze:
                return "빙결 속성 공격이 몬스터에게 스택을 쌓습니다. 스택이 쌓일수록 이동/공격속도가 느려지고,\n최대 스택에 도달하면 완전히 얼어붙습니다. 그 상태에서 타격하면 추가 데미지를 주며 빙결이 해제됩니다.\n빙결/화상은 한 런에서 하나만 선택할 수 있습니다.";
            case KeywordType.Burn:
                return "화상 속성 공격이 몬스터에게 스택을 쌓아 지속 피해를 줍니다. 스택이 높을수록 틱 데미지가 커집니다.\n빙결/화상은 한 런에서 하나만 선택할 수 있습니다.";
            case KeywordType.Area:
                return "레벨이 오르면 힐윈드/화살비의 범위가 증가합니다.";
            case KeywordType.Buff:
                return "버프의 지속시간이 늘어납니다.";
            default:
                return "";
        }
    }
}
