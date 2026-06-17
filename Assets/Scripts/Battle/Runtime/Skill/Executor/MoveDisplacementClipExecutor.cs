using Hoshino;
using UnityEngine;

namespace Battle
{
    [BattleSkillExecutor(SkillGeneratedIds.MoveDisplacementClip, SkillNodeExecutionDomain.Predicted)]
    public sealed class MoveDisplacementClipExecutor : BattleSkillNodeExecutor<MoveDisplacementNodeData>
    {
        protected override void Execute(in BattleSkillExecutionContext context, in MoveDisplacementNodeData data)
        {
            Vector3 displacement = BattleSkillNodeUtility.ResolveVector(data.Space, data.DisplacementPerSecond, context.Motor.transform, context.AimDirection) * context.Delta;
            context.Motor.AddPredictedDisplacement(displacement);
        }
    }
}
