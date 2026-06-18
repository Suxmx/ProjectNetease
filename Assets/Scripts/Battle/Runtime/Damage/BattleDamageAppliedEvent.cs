using FishNet.Connection;
using MemoFramework;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 伤害施加后的事件参数。通过 <c>MF.Event.Fire</c> 分发，
    /// 供 buff 系统、UI、音效等订阅者接收。使用引用池回收。
    /// </summary>
    public sealed class BattleDamageAppliedEvent : MFEventArgs
    {
        /// <summary>伤害类型。</summary>
        public BattleDamageType Type;
        /// <summary>缩放后的最终伤害量。</summary>
        public int FinalAmount;
        /// <summary>是否造成致命伤害（目标死亡/破坏）。</summary>
        public bool Lethal;
        /// <summary>攻击者的战斗体。</summary>
        public BattleCombatState Source;
        /// <summary>受击目标。</summary>
        public IBattleDamageTarget Target;
        /// <summary>攻击者的网络连接。</summary>
        public NetworkConnection SourceConnection;
        /// <summary>伤害来源的技能节点 ClipId。</summary>
        public uint SourceClipId;
        /// <summary>命中世界坐标点。</summary>
        public Vector3 HitPoint;
        /// <summary>伤害发生时的网络 tick。</summary>
        public uint Tick;

        /// <summary>从引用池获取实例并填充伤害数据。</summary>
        public static BattleDamageAppliedEvent Acquire(in BattleDamageInfo info, int finalAmount, bool lethal)
        {
            BattleDamageAppliedEvent evt = MFRefPool.Acquire<BattleDamageAppliedEvent>();
            evt.Type = info.Type;
            evt.FinalAmount = finalAmount;
            evt.Lethal = lethal;
            evt.Source = info.Source;
            evt.Target = info.Target;
            evt.SourceConnection = info.SourceConnection;
            evt.SourceClipId = info.SourceClipId;
            evt.HitPoint = info.HitPoint;
            evt.Tick = info.Tick;
            return evt;
        }

        /// <summary>清空所有字段（引用池回收时调用）。</summary>
        public override void Clear()
        {
            Type = default;
            FinalAmount = 0;
            Lethal = false;
            Source = null;
            Target = null;
            SourceConnection = default;
            SourceClipId = 0u;
            HitPoint = Vector3.zero;
            Tick = 0u;
        }
    }
}
