using MemoFramework.Extension;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 伤害系统统一入口。所有伤害来源（技能 Executor、环境、buff）
    /// 都必须通过此分发器施加伤害，确保缩放逻辑和事件分发一致。
    /// 是伤害框架的中心枢纽，后续 buff 系统将以此为基础接入。
    /// </summary>
    public static class BattleDamageDispatcher
    {
        /// <summary>
        /// 计算缩放后的最终伤害量。
        /// 公式：Amount × 攻击者 OutgoingDamageMultiplier × 目标 IncomingDamageMultiplier。
        /// outgoing 对所有目标生效（含可破坏物）。
        /// </summary>
        public static int ResolveAmount(in BattleDamageInfo info)
        {
            // --- 攻击者输出加成 ---
            float outgoing = 1f;
            if (info.Source != null)
            {
                AttributeSet attrs = info.Source.GetAttributeSet();
                if (attrs != null)
                    outgoing = Mathf.Max(0f, attrs.OutgoingDamageMultiplier);
            }

            // --- 目标受伤加成 ---
            float incoming = 1f;
            if (info.Target != null)
            {
                AttributeSet attrs = info.Target.GetAttributeSet();
                if (attrs != null)
                    incoming = Mathf.Max(0f, attrs.IncomingDamageMultiplier);
            }

            return Mathf.Max(0, Mathf.CeilToInt(info.Amount * outgoing * incoming));
        }

        /// <summary>
        /// 施加伤害：缩放 → 扣血 → 广播事件。
        /// 事件通过 MF.Event.Fire 下一帧分发，避免在 tick 内重入。
        /// </summary>
        public static void Apply(in BattleDamageInfo info)
        {
            if (info.Target == null)
                return;

            // --- 缩放并过滤零伤害 ---
            int final = ResolveAmount(info);
            if (final <= 0)
                return;

            // --- 施加伤害到目标 ---
            bool lethal = info.Target.ApplyDamageInternal(final, info.SourceConnection);

            // --- 广播伤害事件供订阅者处理 ---
            BattleDamageAppliedEvent evt = BattleDamageAppliedEvent.Acquire(info, final, lethal);
            MF.Event.Fire(info.Source, evt);
        }
    }
}
