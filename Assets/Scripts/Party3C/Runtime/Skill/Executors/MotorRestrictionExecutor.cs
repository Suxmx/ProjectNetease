using Hoshino;
using Hoshino.Skill.Executor;

namespace Party3C
{
    /// <summary>
    /// Applies and clears motor restrictions when a skill timeline node is active.
    /// </summary>
    [SkillExecutor(SkillGeneratedIds.MotorRestrictionClip)]
    public sealed class MotorRestrictionExecutor : PartyPresentationSkillExecutor<MotorRestrictionNodeData>
    {
        /// <summary>
        /// Adds the configured motor restriction for this skill node.
        /// </summary>
        protected override void OnStart(in PartySkillExecutionContext context, in MotorRestrictionNodeData data)
        {
            if (data.Restrictions == EPartyKccMotorRestriction.None)
                return;

            context.Motor?.AddMotorRestriction(CreateRestrictionSourceKey(context.Sequence, context.Node.NodeId), data.Restrictions);
        }

        /// <summary>
        /// Removes the motor restriction owned by this skill node.
        /// </summary>
        protected override void OnEnd(in PartySkillExecutionContext context, in MotorRestrictionNodeData data)
        {
            context.Motor?.RemoveMotorRestriction(CreateRestrictionSourceKey(context.Sequence, context.Node.NodeId));
        }

        /// <summary>
        /// Creates a stable runtime key for one skill sequence and timeline node.
        /// </summary>
        private static int CreateRestrictionSourceKey(int sequence, int nodeId)
        {
            unchecked
            {
                return (sequence * 397) ^ nodeId;
            }
        }
    }
}
