using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 移动位移节点 Executor（ClientPrediction 域）。
    /// 在节点 active 区间内每 tick 向 Motor 添加预测位移（按 delta 缩放），参与预测回滚。
    /// </summary>
    [SkillExecutor(SkillGeneratedIds.MoveDisplacementClip)]
    public sealed class MoveDisplacementClipExecutor : ClientPredictionSkillExecutor<MoveDisplacementNodeData>
    {
        protected override void OnTick(in SkillExecutionContext context, in MoveDisplacementNodeData data)
        {
            Vector3 displacement = SkillUtility.ResolveVector(data.Space, data.DisplacementPerSecond, context.Motor.Transform, context.AimDirection) * context.Delta;
            context.Motor.AddPredictedDisplacement(displacement);
        }
    }
}
