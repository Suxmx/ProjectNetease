using FishNet.Connection;

namespace Battle
{
    /// <summary>
    /// 伤害目标统一接口。由 <see cref="CombatState"/>（角色/小兵）
    /// 和 <see cref="BattleDestructibleObject"/>（可破坏物）实现，
    /// 使 <see cref="BattleDamageDispatcher"/> 无需区分目标类型。
    /// </summary>
    public interface IBattleDamageTarget
    {
        /// <summary>获取目标的属性集（可破坏物返回 null）。</summary>
        AttributeSet GetAttributeSet();

        /// <summary>施加最终伤害（已缩放），返回是否致命。仅由 Dispatcher 调用。</summary>
        bool ApplyDamageInternal(int amount, NetworkConnection attacker);
    }
}
