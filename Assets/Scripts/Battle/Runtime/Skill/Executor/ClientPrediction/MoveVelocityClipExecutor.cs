using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 移动速度节点 Executor（ClientPrediction 域）。
    /// 在节点 active 区间内每 tick 向 Motor 添加预测速度，参与预测回滚。
    /// 当前为测试态：仅 log 节点数据，实际功能代码已注释。
    /// </summary>
    [BattleSkillExecutor(SkillGeneratedIds.MoveVelocityClip)]
    public sealed class MoveVelocityClipExecutor : ClientPredictionSkillExecutor<MoveVelocityNodeData>
    {
        protected override void OnTick(in BattleSkillExecutionContext context, in MoveVelocityNodeData data)
        {
            Debug.Log($"[SkillTest] {nameof(MoveVelocityNodeData)} @ elapsed={context.ElapsedTicks} phase={context.LifecyclePhase}: {JsonUtility.ToJson(data)}");
            // Vector3 velocity = BattleSkillNodeUtility.ResolveVector(data.Space, data.Velocity, context.Motor.transform, context.AimDirection);
            // context.Motor.AddPredictedVelocity(velocity);
        }
    }
}
