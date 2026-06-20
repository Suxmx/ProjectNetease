using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 移动速度节点 Executor（ClientPrediction 域，clipId=1002）。
    /// 在节点 active 区间内每 tick 向 Motor 添加预测速度（不乘 delta，速度直接累加），
    /// 由 PredictionRigidbody.Simulate 连续推进物理。突进结束速度归零即硬停。
    /// 旁观者（纯 Client，非 Owner 非 Server）不本地预测：FishNet 的 state forwarding + Appended state order
    /// 会让 spectator 先 replay 预跑多个 tick 导致视觉瞬跳，旁观者改由服务器 reconcile 传播 + TickSmoother 插值。
    /// Host 旁观（IsServerStarted）不跳过——Host 是服务器权威直接跑，无 replay 问题。
    /// </summary>
    [SkillExecutor(SkillGeneratedIds.MoveVelocityClip)]
    public sealed class MoveVelocityClipExecutor : ClientPredictionSkillExecutor<MoveVelocityNodeData>
    {
        protected override void OnTick(in SkillExecutionContext context, in MoveVelocityNodeData data)
        {
            // --- 旁观者跳过：避免 replay 预跑导致视觉瞬跳 ---
            if (!context.Player.IsOwner && !context.Player.IsServerStarted)
                return;

            // --- 速度方向（按 Space 解析到世界空间）---
            Vector3 velocity = SkillUtility.ResolveVector(data.Space, data.Velocity, context.Motor.Transform, context.AimDirection);

            // --- 曲线系数：按归一化进度求值，null/空曲线返回 1（恒速兼容）---
            float scale = EvaluateVelocityCurve(data.VelocityCurve, context);
            velocity *= scale;

            // --- 诊断：打印 curve 状态和最终速度 ---
            if (context.Player.DebugLog)
            {
                int curveLen = data.VelocityCurve != null ? data.VelocityCurve.length : -1;
                int total = context.Skill != null ? context.Skill.LengthTicks : 0;
                float t = total > 0 ? Mathf.Clamp01((float)context.ElapsedTicks / total) : 0f;
                SkillDiagLogger.Log($"[VEL] role={SkillDiagLogger.RoleOf(context.Player)} tick={context.CurrentTick} elapsed={context.ElapsedTicks} t={t:F3} curveLen={curveLen} scale={scale:F3} vel={velocity} velMag={velocity.magnitude:F3} state={context.ReplicateState}");
            }

            context.Motor.AddPredictedVelocity(velocity);
        }

        /// <summary>求曲线系数：按技能归一化进度求值，null/空曲线返回 1。</summary>
        private static float EvaluateVelocityCurve(AnimationCurve curve, in SkillExecutionContext context)
        {
            if (curve == null || curve.length == 0)
                return 1f;
            int total = context.Skill != null ? context.Skill.LengthTicks : 0;
            if (total <= 0)
                return 1f;
            float t = Mathf.Clamp01((float)context.ElapsedTicks / total);
            return curve.Evaluate(t);
        }
    }
}
