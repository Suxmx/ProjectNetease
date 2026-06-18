namespace Battle
{
    /// <summary>
    /// 伤害类型分类。用于伤害缩放、抗性计算和 buff 触发条件判断。
    /// 后续可按需追加新类型（如元素、真实伤害等）。
    /// </summary>
    public enum BattleDamageType : byte
    {
        Unspecified = 0,
        Melee = 1,
        Skill = 2,
        Burn = 3,
        Poison = 4,
        Environmental = 5
    }
}
