using Hoshino;
using Hoshino.Skill.Executor;

namespace Party3C
{
    /// <summary>
    /// Registers and unregisters active combo input windows from skill timelines.
    /// </summary>
    [SkillExecutor(SkillGeneratedIds.ComboWindowClip)]
    public sealed class ComboWindowExecutor : PartyPresentationSkillExecutor<ComboWindowNodeData>
    {
        /// <summary>
        /// Opens the combo window when the timeline node becomes active.
        /// </summary>
        protected override void OnStart(in PartySkillExecutionContext context, in ComboWindowNodeData data)
        {
            context.ComboController?.OpenComboWindow(context.Sequence, context.Node.NodeId, data.InputAction, data.NextSkillId, data.CancelCurrentSkill);
        }

        /// <summary>
        /// Closes the combo window when the timeline node ends.
        /// </summary>
        protected override void OnEnd(in PartySkillExecutionContext context, in ComboWindowNodeData data)
        {
            context.ComboController?.CloseComboWindow(context.Sequence, context.Node.NodeId);
        }
    }
}
