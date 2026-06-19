using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 移动位移节点 Executor（Predicted 域）。
    /// 在节点 active 区间内每 tick 向 Motor 添加预测位移（按 delta 缩放），参与预测回滚。
    /// 当前为测试态：仅 log 节点数据，实际功能代码已注释。
    /// </summary>
    [BattleSkillExecutor(SkillGeneratedIds.MoveDisplacementClip, SkillNodeExecutionDomain.Predicted)]
    public sealed class MoveDisplacementClipExecutor : BattleSkillNodeExecutor<MoveDisplacementNodeData>
    {
        protected override void Execute(in BattleSkillExecutionContext context, in MoveDisplacementNodeData data)
        {
            Debug.Log($"[SkillTest] {nameof(MoveDisplacementNodeData)} @ elapsed={context.ElapsedTicks} clipId={context.Node.ClipId} domain=Predicted: {JsonUtility.ToJson(data)}");
            // Vector3 displacement = BattleSkillNodeUtility.ResolveVector(data.Space, data.DisplacementPerSecond, context.Motor.transform, context.AimDirection) * context.Delta;
            // context.Motor.AddPredictedDisplacement(displacement);
        }
    }
}
