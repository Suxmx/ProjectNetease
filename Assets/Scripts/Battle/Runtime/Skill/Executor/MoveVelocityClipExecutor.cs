using Hoshino;
using UnityEngine;

namespace Battle
{
    [BattleSkillExecutor(SkillGeneratedIds.MoveVelocityClip, SkillNodeExecutionDomain.Predicted)]
    public sealed class MoveVelocityClipExecutor : BattleSkillNodeExecutor<MoveVelocityNodeData>
    {
        protected override void Execute(in BattleSkillExecutionContext context, in MoveVelocityNodeData data)
        {
            Debug.Log($"[SkillTest] {nameof(MoveVelocityNodeData)} @ elapsed={context.ElapsedTicks} clipId={context.Node.ClipId} domain=Predicted: {JsonUtility.ToJson(data)}");
            // Vector3 velocity = BattleSkillNodeUtility.ResolveVector(data.Space, data.Velocity, context.Motor.transform, context.AimDirection);
            // context.Motor.AddPredictedVelocity(velocity);
        }
    }
}
