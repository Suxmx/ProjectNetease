using Hoshino;
using UnityEngine;

namespace Battle
{
    [BattleSkillExecutor(SkillGeneratedIds.TeleportClip, SkillNodeExecutionDomain.Predicted)]
    public sealed class TeleportClipExecutor : BattleSkillNodeExecutor<TeleportNodeData>
    {
        protected override void Execute(in BattleSkillExecutionContext context, in TeleportNodeData data)
        {
            if (!context.IsNodeStartTick)
                return;

            Vector3 destination = data.UseCommandTargetPoint
                ? context.Command.TargetPoint
                : context.Motor.transform.position + BattleSkillNodeUtility.ResolveVector(data.Space, data.Offset, context.Motor.transform, context.AimDirection);

            context.Motor.TeleportPredicted(destination);
        }
    }
}
