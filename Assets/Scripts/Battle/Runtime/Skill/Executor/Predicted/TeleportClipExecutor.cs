using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 传送节点 Executor（Predicted 域）。
    /// 在节点起始 tick 将 Motor 传送到目标位置（指令目标点或偏移位置）。
    /// 当前为测试态：仅 log 节点数据，实际功能代码已注释。
    /// </summary>
    [BattleSkillExecutor(SkillGeneratedIds.TeleportClip, SkillNodeExecutionDomain.Predicted)]
    public sealed class TeleportClipExecutor : BattleSkillNodeExecutor<TeleportNodeData>
    {
        protected override void Execute(in BattleSkillExecutionContext context, in TeleportNodeData data)
        {
            Debug.Log($"[SkillTest] {nameof(TeleportNodeData)} @ elapsed={context.ElapsedTicks} clipId={context.Node.ClipId} domain=Predicted: {JsonUtility.ToJson(data)}");
            // if (!context.IsNodeStartTick)
            //     return;
            //
            // Vector3 destination = data.UseCommandTargetPoint
            //     ? context.Command.TargetPoint
            //     : context.Motor.transform.position + BattleSkillNodeUtility.ResolveVector(data.Space, data.Offset, context.Motor.transform, context.AimDirection);
            //
            // context.Motor.TeleportPredicted(destination);
        }
    }
}
