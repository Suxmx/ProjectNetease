using FishNet.Connection;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 伤害请求的完整描述。由伤害来源（Executor/环境/buff）构造，
    /// 交由 <see cref="BattleDamageDispatcher"/> 统一处理缩放和施加。
    /// </summary>
    public struct BattleDamageInfo
    {
        /// <summary>伤害类型，用于抗性/触发器分类。</summary>
        public BattleDamageType Type;
        /// <summary>缩放前的原始伤害量。</summary>
        public int Amount;
        /// <summary>攻击者的战斗体（可能为 null，如环境伤害）。</summary>
        public CombatState Source;
        /// <summary>受击目标，实现 <see cref="IBattleDamageTarget"/>。</summary>
        public IBattleDamageTarget Target;
        /// <summary>攻击者的网络连接，用于击杀归属。</summary>
        public NetworkConnection SourceConnection;
        /// <summary>伤害来源的技能节点 ClipId（0 = 非技能来源）。</summary>
        public uint SourceClipId;
        /// <summary>命中世界坐标点。</summary>
        public Vector3 HitPoint;
        /// <summary>命中表面法线。</summary>
        public Vector3 HitNormal;
        /// <summary>伤害发生时的网络 tick。</summary>
        public uint Tick;
    }
}
