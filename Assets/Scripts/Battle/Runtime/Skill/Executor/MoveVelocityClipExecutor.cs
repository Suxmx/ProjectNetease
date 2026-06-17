using Hoshino;
using UnityEngine;

namespace Battle
{
    [BattleSkillExecutor(SkillGeneratedIds.MoveVelocityClip, SkillNodeExecutionDomain.Predicted)]
    public sealed class MoveVelocityClipExecutor : BattleSkillNodeExecutor<MoveVelocityNodeData>
    {
        protected override void Execute(in BattleSkillExecutionContext context, in MoveVelocityNodeData data)
        {
            Vector3 velocity = BattleSkillNodeUtility.ResolveVector(data.Space, data.Velocity, context.Motor.transform, context.AimDirection);
            context.Motor.AddPredictedVelocity(velocity);
        }
    }
}
